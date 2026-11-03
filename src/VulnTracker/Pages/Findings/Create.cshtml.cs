using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VulnTracker.Authorization;
using VulnTracker.Data;

namespace VulnTracker.Pages.Findings;

// FIND-01: Analyst and PM create findings manually.
[Authorize(Policy = Policies.CreateFinding)]
public class CreateModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public FindingInput Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var finding = new Finding
        {
            Title = Input.Title,
            Description = Input.Description,
            AffectedAsset = Input.AffectedAsset,
            Severity = Input.Severity,
            DiscoveryDate = Input.DiscoveryDate,
            Source = FindingSource.Internal,
        };
        db.Findings.Add(finding);
        await db.SaveChangesAsync();

        return RedirectToPage("Details", new { id = finding.Id });
    }
}
