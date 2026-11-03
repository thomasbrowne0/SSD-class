using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VulnTracker.Authorization;
using VulnTracker.Data;

namespace VulnTracker.Pages;

[Authorize(Policy = Policies.ViewFindings)]
public class IndexModel(AppDbContext db) : PageModel
{
    // FIND-05: filter by status, severity, assigned owner and affected asset (from the query string).
    [BindProperty(SupportsGet = true)]
    public FindingStatus? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public Severity? Severity { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? AssigneeId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Asset { get; set; }

    public List<Finding> Findings { get; private set; } = [];

    public SelectList UserOptions { get; private set; } = default!;

    public async Task OnGetAsync()
    {
        // Filters only narrow down what the user is allowed to see.
        var query = db.Findings.VisibleTo(User);

        if (Status is not null)
        {
            query = query.Where(f => f.Status == Status);
        }
        if (Severity is not null)
        {
            query = query.Where(f => f.Severity == Severity);
        }
        if (!string.IsNullOrWhiteSpace(AssigneeId))
        {
            query = query.Where(f => f.AssigneeId == AssigneeId);
        }
        if (!string.IsNullOrWhiteSpace(Asset))
        {
            query = query.Where(f => f.AffectedAsset.ToLower().Contains(Asset.ToLower()));
        }

        Findings = await query
            .Include(f => f.Assignee)
            .OrderByDescending(f => f.DiscoveryDate)
            .ToListAsync();

        UserOptions = new SelectList(
            await db.Users.OrderBy(u => u.Username).ToListAsync(), nameof(AppUser.Id), nameof(AppUser.Username));
    }
}
