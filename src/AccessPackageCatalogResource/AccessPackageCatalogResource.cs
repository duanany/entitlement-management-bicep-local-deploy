using Azure.Bicep.Types.Concrete;
using Bicep.Local.Extension.Types.Attributes;

namespace EntitlementManagement.AccessPackageCatalogResource;

/// <summary>
/// Identifiers for an Access Package Catalog Resource
/// </summary>
public class AccessPackageCatalogResourceIdentifiers
{
    [TypeProperty("The ID of the catalog to add the resource to", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string CatalogId { get; set; }

    [TypeProperty("The origin ID of the resource (Entra ID Group GUID, Application GUID, or SharePoint site URL)", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string OriginId { get; set; }
}

/// <summary>
/// Represents a resource added to an access package catalog (Entra ID Group, Application, or SharePoint site)
/// </summary>
[ResourceType("accessPackageCatalogResource")]
public class AccessPackageCatalogResource : AccessPackageCatalogResourceIdentifiers
{
    [TypeProperty("The type of resource. Allowed values: 'AadGroup', 'AadApplication', 'SharePointOnline'", ObjectTypePropertyFlags.Required)]
    public required string OriginSystem { get; set; }

    [TypeProperty("Display name of the resource (optional, will be retrieved from Entra ID if not provided)")]
    public string? DisplayName { get; set; }

    [TypeProperty("Description of the resource (optional)")]
    public string? Description { get; set; }

    [TypeProperty("Justification for adding the resource to the catalog (optional)")]
    public string? Justification { get; set; }

    // Read-only outputs
    [TypeProperty("The unique identifier of the resource in the catalog", ObjectTypePropertyFlags.ReadOnly)]
    public string? Id { get; set; }

    [TypeProperty("The state of the resource request (Delivered, DeliveryFailed, etc.)", ObjectTypePropertyFlags.ReadOnly)]
    public string? RequestState { get; set; }

    [TypeProperty("The status of the resource request (Fulfilled, Failed, etc.)", ObjectTypePropertyFlags.ReadOnly)]
    public string? RequestStatus { get; set; }

    [TypeProperty("The resource type as shown in the catalog", ObjectTypePropertyFlags.ReadOnly)]
    public string? ResourceType { get; set; }

    [TypeProperty("When the resource was added to the catalog", ObjectTypePropertyFlags.ReadOnly)]
    public string? CreatedDateTime { get; set; }
}
