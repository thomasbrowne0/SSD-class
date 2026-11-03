using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VulnTracker.Authorization;
using VulnTracker.Data;

namespace VulnTracker.Pages;

// FR-RPT-01: open findings by severity and by status.
[Authorize(Policy = Policies.ViewFindings)]
public class DashboardModel(AppDbContext db) : PageModel
{
    // Remediated and Accepted Risk are closed.
    public static readonly FindingStatus[] OpenStatuses =
        [FindingStatus.New, FindingStatus.Triaged, FindingStatus.InProgress];

    public Dictionary<Severity, int> BySeverity { get; private set; } = [];

    public Dictionary<FindingStatus, int> ByStatus { get; private set; } = [];

    public int TotalOpen { get; private set; }

    public async Task OnGetAsync()
    {
        // Counts only what the user is allowed to see (a Developer sees their own findings).
        var open = db.Findings.VisibleTo(User).Where(f => OpenStatuses.Contains(f.Status));

        var bySeverity = await open.GroupBy(f => f.Severity)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        var byStatus = await open.GroupBy(f => f.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        // Show every severity/status, also those with 0 findings. Most severe first.
        BySeverity = Enum.GetValues<Severity>().Reverse().ToDictionary(s => s, s => bySeverity.GetValueOrDefault(s));
        ByStatus = OpenStatuses.ToDictionary(s => s, s => byStatus.GetValueOrDefault(s));
        TotalOpen = ByStatus.Values.Sum();
    }
}
