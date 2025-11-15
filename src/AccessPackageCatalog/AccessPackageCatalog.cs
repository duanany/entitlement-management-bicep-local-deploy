using Azure.Bicep.Types.Concrete;

namespace EntitlementManagement.AccessPackageCatalog;

public class AccessPackageCatalogIdentifiers
{
    [TypeProperty("The display name of the catalog", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string DisplayName { get; set; }
}

[ResourceType("accessPackageCatalog")]
public class AccessPackageCatalog : AccessPackageCatalogIdentifiers
{
    [TypeProperty("Description of the catalog")]
    public string? Description { get; set; }

    [TypeProperty("Whether the catalog is visible to external users (default: false)")]
    public bool IsExternallyVisible { get; set; } = false;

    [TypeProperty("Catalog type (default: UserManaged)")]
    public string? CatalogType { get; set; } = "UserManaged";

    [TypeProperty("State of the catalog (default: Published)")]
    public string? State { get; set; } = "Published";

    // Outputs
    [TypeProperty("[OUTPUT] Catalog ID (GUID)", ObjectTypePropertyFlags.ReadOnly)]
    public string? Id { get; set; }
}
