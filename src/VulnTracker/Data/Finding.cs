using System.ComponentModel.DataAnnotations;

namespace VulnTracker.Data;

// Fields from spec §3 "Finding information", plus the fields FIND-04 (assign + SLA) and intake channel need.
public class Finding
{
    public int Id { get; set; }

    // Title, Description and ReporterEmail are encrypted in the database (FIND-11, see AppDbContext).
    // No MaxLength on them: the ciphertext is longer than the text. Forms limit the input length.
    public required string Title { get; set; }

    public required string Description { get; set; }

    [MaxLength(200)]
    public required string AffectedAsset { get; set; }

    public Severity Severity { get; set; }

    public DateOnly DiscoveryDate { get; set; }

    public FindingStatus Status { get; set; } = FindingStatus.New;

    public FindingSource Source { get; set; }

    // Keycloak user id (the "sub" claim) of the assigned user.
    [MaxLength(100)]
    public string? AssigneeId { get; set; }

    public AppUser? Assignee { get; set; }

    public DateOnly? DueDate { get; set; }

    // Only set for external reports (spec §3). Personal data (GDPR).
    public string? ReporterEmail { get; set; }
}

public enum Severity
{
    Low,
    Medium,
    High,
    Critical,
}

public enum FindingStatus
{
    New,
    Triaged,
    InProgress,
    Remediated,
    AcceptedRisk,
}

public enum FindingSource
{
    Internal,
    External,
    Scan,
}
