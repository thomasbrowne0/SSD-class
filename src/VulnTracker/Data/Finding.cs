using System.ComponentModel.DataAnnotations;

namespace VulnTracker.Data;

// Fields from spec §3 "Finding information", plus the fields FIND-04 (assign + SLA) and intake channel need.
public class Finding
{
    public int Id { get; set; }

    [MaxLength(200)]
    public required string Title { get; set; }

    // Will be encrypted in W44 (FIND-11).
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

    // Only set for external reports (spec §3). Personal data, so it will be encrypted too.
    [MaxLength(320)]
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
