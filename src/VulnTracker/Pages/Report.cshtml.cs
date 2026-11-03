using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VulnTracker.Data;

namespace VulnTracker.Pages;

// AUTH-03 / FIND-02: public form for external reporters. It can only create findings, never read them.
[AllowAnonymous]
public class ReportModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public ReportInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public bool Submitted { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        db.Findings.Add(new Finding
        {
            Title = Input.Title,
            Description = Input.Description,
            AffectedAsset = Input.AffectedAsset,
            // The reporter's estimate. Analyst/PM can change it during triage.
            Severity = Input.Severity,
            DiscoveryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = FindingStatus.New,
            Source = FindingSource.External,
            ReporterEmail = Input.ContactEmail,
        });
        await db.SaveChangesAsync();

        // Redirect so refreshing the page doesn't submit the report twice.
        return RedirectToPage(new { submitted = true });
    }
}

public class ReportInput
{
    [Required, MaxLength(200), Display(Name = "Short summary")]
    public string Title { get; set; } = "";

    [Required, MaxLength(10_000), Display(Name = "Description and steps to reproduce")]
    public string Description { get; set; } = "";

    [Required, MaxLength(200), Display(Name = "Affected product/component")]
    public string AffectedAsset { get; set; } = "";

    [EnumDataType(typeof(Severity)), Display(Name = "How severe do you think it is?")]
    public Severity Severity { get; set; }

    [Required, EmailAddress, MaxLength(320), Display(Name = "Your email (so we can contact you)")]
    public string ContactEmail { get; set; } = "";
}
