using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using VulnTracker.Authorization;
using VulnTracker.Data;

namespace VulnTracker.Pages.Findings;

// Analyst, PM and the assigned Developer update details and status.
[Authorize(Policy = Policies.ViewFindings)]
public class EditModel(AppDbContext db, IAuthorizationService authorization) : PageModel
{
    [BindProperty]
    public EditInput Input { get; set; } = new();

    public int Id { get; private set; }

    public List<SelectListItem> StatusOptions { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var finding = await db.Findings.FindAsync(id);
        if (finding is null)
        {
            return NotFound();
        }
        if (!await IsAllowed(finding, FindingOperations.Update))
        {
            return Forbid();
        }

        Input = new EditInput
        {
            Title = finding.Title,
            Description = finding.Description,
            AffectedAsset = finding.AffectedAsset,
            Severity = finding.Severity,
            DiscoveryDate = finding.DiscoveryDate,
            Status = finding.Status,
        };
        await LoadPage(finding);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var finding = await db.Findings.FindAsync(id);
        if (finding is null)
        {
            return NotFound();
        }
        if (!await IsAllowed(finding, FindingOperations.Update))
        {
            return Forbid();
        }
        // The dropdown hides Accepted Risk, but the server must check too: the form can be edited by hand.
        var acceptsRisk = Input.Status == FindingStatus.AcceptedRisk && finding.Status != FindingStatus.AcceptedRisk;
        if (acceptsRisk && !await IsAllowed(finding, FindingOperations.AcceptRisk))
        {
            return Forbid();
        }
        if (!ModelState.IsValid)
        {
            await LoadPage(finding);
            return Page();
        }

        finding.Title = Input.Title;
        finding.Description = Input.Description;
        finding.AffectedAsset = Input.AffectedAsset;
        finding.Severity = Input.Severity;
        finding.DiscoveryDate = Input.DiscoveryDate;
        finding.Status = Input.Status;
        await db.SaveChangesAsync();

        return RedirectToPage("Details", new { id });
    }

    private async Task<bool> IsAllowed(Finding finding, OperationAuthorizationRequirement operation) =>
        (await authorization.AuthorizeAsync(User, finding, operation)).Succeeded;

    private async Task LoadPage(Finding finding)
    {
        Id = finding.Id;
        var canAcceptRisk = await IsAllowed(finding, FindingOperations.AcceptRisk);
        StatusOptions = Enum.GetValues<FindingStatus>()
            .Where(s => s != FindingStatus.AcceptedRisk || canAcceptRisk || finding.Status == FindingStatus.AcceptedRisk)
            .Select(s => new SelectListItem(s.ToString(), s.ToString()))
            .ToList();
    }
}
