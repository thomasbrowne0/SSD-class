using System.ComponentModel.DataAnnotations;
using VulnTracker.Data;

namespace VulnTracker.Pages.Findings;

// Forms bind to these instead of the Finding entity, so a form can only set the fields listed here
// (prevents mass assignment of e.g. AssigneeId or Source).
public class FindingInput
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = "";

    [Required, MaxLength(10_000)]
    public string Description { get; set; } = "";

    [Required, MaxLength(200), Display(Name = "Affected asset/component")]
    public string AffectedAsset { get; set; } = "";

    // EnumDataType rejects numbers that aren't a defined value, e.g. Severity=99.
    [EnumDataType(typeof(Severity))]
    public Severity Severity { get; set; }

    [Display(Name = "Discovery date")]
    public DateOnly DiscoveryDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
}

public class EditInput : FindingInput
{
    [EnumDataType(typeof(FindingStatus))]
    public FindingStatus Status { get; set; }
}

public class AssignInput
{
    [MaxLength(100), Display(Name = "Assigned to")]
    public string? AssigneeId { get; set; }

    [Display(Name = "Due date / SLA target")]
    public DateOnly? DueDate { get; set; }
}
