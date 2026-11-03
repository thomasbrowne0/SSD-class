using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VulnTracker.Authorization;
using VulnTracker.Data;

namespace VulnTracker.Pages.Findings;

[Authorize(Policy = Policies.ViewFindings)]
public class DetailsModel(AppDbContext db, IAuthorizationService authorization) : PageModel
{
    public Finding Finding { get; private set; } = default!;

    public bool CanEdit { get; private set; }

    public bool CanAssign { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var finding = await db.Findings.Include(f => f.Assignee).FirstOrDefaultAsync(f => f.Id == id);
        if (finding is null)
        {
            return NotFound();
        }
        // Checked against this finding, not just the role (Developers only see their own).
        if (!(await authorization.AuthorizeAsync(User, finding, FindingOperations.View)).Succeeded)
        {
            return Forbid();
        }

        Finding = finding;
        CanEdit = (await authorization.AuthorizeAsync(User, finding, FindingOperations.Update)).Succeeded;
        CanAssign = (await authorization.AuthorizeAsync(User, Policies.AssignFinding)).Succeeded;
        return Page();
    }
}
