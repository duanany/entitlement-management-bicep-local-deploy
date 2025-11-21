# Catalog Resource

This module adds a resource (Group, Application, or SharePoint Site) to an Entra ID Access Package Catalog.

## Navigation

- [Resource Types](#resource-types)
- [Usage examples](#usage-examples)
- [Parameters](#parameters)
- [Outputs](#outputs)

## Resource Types

| Resource Type | API Version |
| :-- | :-- |
| `accessPackageCatalogResource` | Local |

## Usage examples

The following section provides usage examples for the module, which were used to validate and deploy the module successfully. For a full reference, please review the module's test folder in the repository.

>**Note**: Each example lists all the required parameters first, followed by the rest - each in alphabetical order.

>**Note**: To reference the module, please use the following syntax `'res/graph/identity-governance/entitlement-management/catalogs/resources/main.bicep'`.

>**Note**: This module is typically called from the parent catalog module, but can be used standalone.

### Example 1: _Add Group to Catalog_

This example adds an Entra ID Group to an existing catalog.

<details>

<summary>via Bicep module</summary>

```bicep
module catalogResource 'res/graph/identity-governance/entitlement-management/catalogs/resources/main.bicep' = {
  name: 'catalogResourceDeployment'
  params: {
    entitlementToken: '<entitlementToken>'
    catalogName: 'Engineering Resources'
    originId: '<group-guid>'
    originSystem: 'AadGroup'
    displayName: 'Engineering Team'
    resourceDescription: 'Main engineering team group'
  }
}
```

</details>

### Example 2: _Add Application to Catalog_

This example adds an Entra ID Application to a catalog.

<details>

<summary>via Bicep module</summary>

```bicep
module catalogResource 'res/graph/identity-governance/entitlement-management/catalogs/resources/main.bicep' = {
  name: 'catalogResourceDeployment'
  params: {
    entitlementToken: '<entitlementToken>'
    catalogName: 'Engineering Resources'
    originId: '<application-guid>'
    originSystem: 'AadApplication'
    displayName: 'Engineering Portal'
    resourceDescription: 'Internal engineering portal application'
    justification: 'Required for engineering team access package'
  }
}
```

</details>

### Example 3: _Add SharePoint Site to Catalog_

This example adds a SharePoint site to a catalog.

<details>

<summary>via Bicep module</summary>

```bicep
module catalogResource 'res/graph/identity-governance/entitlement-management/catalogs/resources/main.bicep' = {
  name: 'catalogResourceDeployment'
  params: {
    entitlementToken: '<entitlementToken>'
    catalogName: 'Engineering Resources'
    originId: 'https://contoso.sharepoint.com/sites/engineering'
    originSystem: 'SharePointOnline'
    displayName: 'Engineering SharePoint'
    resourceDescription: 'Engineering team collaboration site'
  }
}
```

</details>

## Parameters

**Required parameters**

| Parameter | Type | Description |
| :-- | :-- | :-- |
| [`catalogName`](#parameter-catalogname) | string | The name of the catalog. |
| [`entitlementToken`](#parameter-entitlementtoken) | securestring | Entitlement Management API token (Graph API token with EntitlementManagement.ReadWrite.All permission) |
| [`originId`](#parameter-originid) | string | The origin ID of the resource (Entra ID Group GUID, Application GUID, or SharePoint site URL). |
| [`originSystem`](#parameter-originsystem) | string | The origin system of the resource. |

**Optional parameters**

| Parameter | Type | Description |
| :-- | :-- | :-- |
| [`displayName`](#parameter-displayname) | string | Display name of the resource. |
| [`justification`](#parameter-justification) | string | Justification for adding the resource to the catalog. |
| [`resourceDescription`](#parameter-resourcedescription) | string | Description of the resource. |

### Parameter: `catalogName`

The name of the catalog.

- Required: Yes
- Type: string

### Parameter: `entitlementToken`

Entitlement Management API token (Graph API token with EntitlementManagement.ReadWrite.All permission)

- Required: Yes
- Type: securestring

### Parameter: `originId`

The origin ID of the resource (Entra ID Group GUID, Application GUID, or SharePoint site URL).

- Required: Yes
- Type: string

### Parameter: `originSystem`

The origin system of the resource.

- Required: Yes
- Type: string
- Allowed:
  ```Bicep
  [
    'AadApplication'
    'AadGroup'
    'SharePointOnline'
  ]
  ```

### Parameter: `displayName`

Display name of the resource.

- Required: No
- Type: string

### Parameter: `justification`

Justification for adding the resource to the catalog.

- Required: No
- Type: string

### Parameter: `resourceDescription`

Description of the resource.

- Required: No
- Type: string

## Outputs

| Output | Type | Description |
| :-- | :-- | :-- |
| `description` | string | The description of the resource. |
| `displayName` | string | The display name of the resource. |
| `originId` | string | The origin ID of the resource. |
| `originSystem` | string | The origin system of the resource. |
| `requestState` | string | The state of the resource request. |
| `requestStatus` | string | The status of the resource request. |
| `resourceId` | string | The unique identifier of the resource in the catalog. |

## Notes

### Origin ID Formats

Use the following formats for the `originId` parameter:

1. **Entra ID Groups**: Group GUID (e.g., `12345678-1234-1234-1234-123456789abc`)
2. **Entra ID Applications**: Application GUID (e.g., `87654321-4321-4321-4321-cba987654321`)
3. **SharePoint Sites**: Full site URL (e.g., `https://contoso.sharepoint.com/sites/engineering`)

### Request States

The resource addition is asynchronous. Common request states include:

- **Delivered**: Resource successfully added to catalog
- **DeliveryFailed**: Failed to add resource (check permissions and resource existence)
- **Denied**: Request was denied (check catalog permissions)
- **Scheduled**: Request is scheduled for processing
- **PartiallyDelivered**: Some aspects of the request succeeded

### Prerequisites

- An existing catalog (created via `catalogs/main.bicep` or manually)
- Graph API token with `EntitlementManagement.ReadWrite.All` permission
- The resource (group, application, or SharePoint site) must exist
- Appropriate permissions to access the resource:
  - For groups: Group read permissions
  - For applications: Application read permissions
  - For SharePoint: SharePoint site access

### Troubleshooting

If the resource fails to add (`requestState: 'DeliveryFailed'`):

1. Verify the resource exists (group, application, or SharePoint site)
2. Confirm the `originId` is correct
3. Check the service principal has permissions to read the resource
4. For SharePoint sites, ensure the site URL is exact (including https://)
5. Review the `requestStatus` output for detailed error information

### Usage in Parent Module

This module is automatically called by the parent catalog module when resources are specified. You typically don't need to call it directly unless adding resources to an existing catalog.
