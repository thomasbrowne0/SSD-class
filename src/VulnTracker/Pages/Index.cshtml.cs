using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VulnTracker.Data;

namespace VulnTracker.Pages;

public class IndexModel(AppDbContext db) : PageModel
{
    public List<Finding> Findings { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Findings = await db.Findings.OrderByDescending(f => f.DiscoveryDate).ToListAsync();
    }
}
