namespace EntitlementManagement.AccessPackageAssignment;

using Bicep.Local.Extension;

/// <summary>
/// Properties for creating an access package assignment.
/// In Graph API, assignments are created via assignmentRequests, not directly!
/// </summary>
[ResourceType("accessPackageAssignment")]
public class AccessPackageAssignment
{
    [TypeProperty("The Graph API ID of the assignment (populated after creation)")]
    public string? Id { get; set; }

    [TypeProperty("The ID of the access package to assign")]
    public required string AccessPackageId { get; set; }

    [TypeProperty("The ID of the assignment policy to use")]
    public required string AssignmentPolicyId { get; set; }

    [TypeProperty("The object ID (GUID) of the user to assign")]
    public string? TargetUserId { get; set; }

    [TypeProperty("The email of the user to assign (for external users not yet in directory)")]
    public string? TargetUserEmail { get; set; }

    [TypeProperty("Justification for the assignment request")]
    public string? Justification { get; set; }

    [TypeProperty("The current state of the assignment (e.g., 'Delivered', 'Delivering', 'Failed')")]
    public string? State { get; set; }

    [TypeProperty("Schedule for time-bound access. Supports startDateTime and expiration settings.")]
    public AccessPackageAssignmentSchedule? Schedule { get; set; }
}

/// <summary>
/// Schedule settings for time-bound access package assignments.
/// </summary>
public class AccessPackageAssignmentSchedule
{
    [TypeProperty("When the assignment should become active (ISO 8601 format, e.g., '2025-01-01T00:00:00Z'). If omitted, assignment is active immediately.")]
    public string? StartDateTime { get; set; }

    [TypeProperty("Expiration settings for the assignment")]
    public AssignmentExpiration? Expiration { get; set; }
}

/// <summary>
/// Expiration settings for access package assignments.
/// </summary>
public class AssignmentExpiration
{
    [TypeProperty("Type of expiration: 'noExpiration', 'afterDateTime', or 'afterDuration'")]
    public string? Type { get; set; }

    [TypeProperty("Specific end date/time for the assignment (ISO 8601 format, e.g., '2025-12-31T23:59:59Z'). Used when type='afterDateTime'.")]
    public string? EndDateTime { get; set; }

    [TypeProperty("Duration after which assignment expires (ISO 8601 duration format, e.g., 'P90D' for 90 days, 'PT24H' for 24 hours). Used when type='afterDuration'.")]
    public string? Duration { get; set; }
}

