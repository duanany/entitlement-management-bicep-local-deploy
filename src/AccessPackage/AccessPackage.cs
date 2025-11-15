using Azure.Bicep.Types.Concrete;
using Bicep.Local.Extension.Types.Attributes;

namespace EntitlementManagement.AccessPackage;

/// <summary>
/// Identifiers for an Access Package resource
/// </summary>
public class AccessPackageIdentifiers
{
    [TypeProperty("The display name of the access package", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string DisplayName { get; set; }

    [TypeProperty("The ID of the catalog this access package belongs to", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string CatalogId { get; set; }
}

/// <summary>
/// Represents an Azure Entitlement Management Access Package
/// </summary>
[ResourceType("accessPackage")]
public class AccessPackage : AccessPackageIdentifiers
{
    [TypeProperty("The description of the access package")]
    public string? Description { get; set; }

    [TypeProperty("Whether the access package is hidden from requestors")]
    public bool IsHidden { get; set; } = false;

    [TypeProperty("The unique ID of the access package (output only)", ObjectTypePropertyFlags.ReadOnly)]
    public string? Id { get; set; }

    [TypeProperty("The date/time when the access package was created (output only)", ObjectTypePropertyFlags.ReadOnly)]
    public string? CreatedDateTime { get; set; }

    [TypeProperty("The date/time when the access package was last modified (output only)", ObjectTypePropertyFlags.ReadOnly)]
    public string? ModifiedDateTime { get; set; }
}
