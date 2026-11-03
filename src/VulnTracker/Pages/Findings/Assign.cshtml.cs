using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VulnTracker.Authorization;
using VulnTracker.Data;

namespace VulnTracker.Pages.Findings;

// FIND-04: Analyst and PM assign a finding to a user and set a due date / SLA target.
[Authorize(Policy = Policies.AssignFinding)]
public class AssignModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public AssignInput Input { get; set; } = new();

    public int Id { get; private set; }

    public string Title { get; private set; } = "";

    public SelectList UserOptions { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var finding = await db.Findings.FindAsync(id);
        if (finding is null)
        {
            return NotFound();
        }

        Input = new AssignInput { AssigneeId = finding.AssigneeId, DueDate = finding.DueDate };
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
        var assigneeId = string.IsNullOrEmpty(Input.AssigneeId) ? null : Input.AssigneeId;
        if (assigneeId is not null && !await db.Users.AnyAsync(u => u.Id == assigneeId))
        {
            ModelState.AddModelError("Input.AssigneeId", "Unknown user.");
        }
        if (!ModelState.IsValid)
        {
            await LoadPage(finding);
            return Page();
        }

        finding.AssigneeId = assigneeId;
        finding.DueDate = Input.DueDate;
        await db.SaveChangesAsync();

        return RedirectToPage("Details", new { id });
    }

    private async Task LoadPage(Finding finding)
    {
        Id = finding.Id;
        Title = finding.Title;
        UserOptions = new SelectList(
            await db.Users.OrderBy(u => u.Username).ToListAsync(), nameof(AppUser.Id), nameof(AppUser.Username));
    }
}
