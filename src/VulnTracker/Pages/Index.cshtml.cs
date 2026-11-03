using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VulnTracker.Authorization;
using VulnTracker.Data;

namespace VulnTracker.Pages;

[Authorize(Policy = Policies.ViewFindings)]
public class IndexModel(AppDbContext db) : PageModel
{
    public List<Finding> Findings { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Findings = await db.Findings
            .VisibleTo(User)
            .OrderByDescending(f => f.DiscoveryDate)
            .ToListAsync();
    }
}
