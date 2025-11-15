namespace EntitlementManagement.AccessPackageAssignment;

using Bicep.Local.Extension;

/// <summary>
/// Identifiers for an access package assignment.
/// Used to uniquely identify an assignment for updates/deletes.
/// </summary>
public class AccessPackageAssignmentIdentifiers
{
    [TypeProperty("The unique ID of the assignment (returned after creation)")]
    public string? Id { get; set; }

    [TypeProperty("The ID of the access package")]
    public string? AccessPackageId { get; set; }

    [TypeProperty("The target user's object ID")]
    public string? TargetUserId { get; set; }
}
