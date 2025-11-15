using System.Text.Json.Serialization;
using Azure.Bicep.Types.Concrete;

namespace EntitlementManagement.AccessPackageResourceRoleScope;

/// <summary>
/// Resource origin system types.
/// </summary>
public enum ResourceOriginSystem
{
    AadGroup,
    AadApplication,
    SharePointOnline
}

/// <summary>
/// Identifiers for an access package resource role scope.
/// </summary>
public class AccessPackageResourceRoleScopeIdentifiers
{
    /// <summary>
    /// The ID of the access package that contains this resource role scope.
    /// </summary>
    [TypeProperty(
        "The ID of the access package that contains this resource role scope.",
        ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required
    )]
    public required string AccessPackageId { get; set; }

    /// <summary>
    /// The origin ID of the resource (e.g., Entra ID Group GUID, App GUID).
    /// </summary>
    [TypeProperty(
        "The origin ID of the resource (e.g., Entra ID Group GUID, App GUID).",
        ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required
    )]
    public required string ResourceOriginId { get; set; }

    /// <summary>
    /// The origin ID of the role (e.g., 'Member_guid' for groups, numeric ID for SharePoint).
    /// For groups: Use 'Member_{groupGuid}' or 'Owner_{groupGuid}'.
    /// For SharePoint: Use numeric role ID like '3' (Creators), '4' (Contributors), '5' (Viewers).
    /// This specifies WHICH ROLE from the resource users will get when assigned the access package.
    /// </summary>
    [TypeProperty(
        "The role ID. Groups: 'Member_{groupGuid}' or 'Owner_{groupGuid}'. SharePoint: '3','4','5'. Required to specify which role users get.",
        ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required
    )]
    public required string RoleOriginId { get; set; }
}

/// <summary>
/// Represents a resource role scope within an access package.
/// Links a specific role from a catalog resource to the access package.
/// </summary>
[ResourceType("accessPackageResourceRoleScope")]
public class AccessPackageResourceRoleScope : AccessPackageResourceRoleScopeIdentifiers
{
    /// <summary>
    /// The origin system of the resource.
    /// </summary>
    [TypeProperty("The origin system of the resource (AadGroup, AadApplication, or SharePointOnline).")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ResourceOriginSystem? ResourceOriginSystem { get; set; }

    /// <summary>
    /// Display name of the role (e.g., 'Member', 'Owner', 'Contributors').
    /// </summary>
    [TypeProperty("Display name of the role (e.g., 'Member', 'Owner' for groups).")]
    public string? RoleDisplayName { get; set; }

    /// <summary>
    /// The ID of the catalog resource (obtained from accessPackageCatalogResource deployment).
    /// </summary>
    [TypeProperty("The ID of the catalog resource (from accessPackageCatalogResource.id output).")]
    public string? CatalogResourceId { get; set; }

    // Read-only outputs

    /// <summary>
    /// The unique identifier of this resource role scope.
    /// </summary>
    [TypeProperty("[OUTPUT] The unique identifier of this resource role scope.", ObjectTypePropertyFlags.ReadOnly)]
    public string? Id { get; set; }

    /// <summary>
    /// The date and time when the resource role scope was created.
    /// </summary>
    [TypeProperty("[OUTPUT] The date and time when the resource role scope was created.", ObjectTypePropertyFlags.ReadOnly)]
    public string? CreatedDateTime { get; set; }
}
