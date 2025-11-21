# Access Package Catalog

This module deploys an Entra ID Access Package Catalog with optional resources.

## Navigation

- [Resource Types](#resource-types)
- [Usage examples](#usage-examples)
- [Parameters](#parameters)
- [Outputs](#outputs)
- [Cross-referenced modules](#cross-referenced-modules)

## Resource Types

| Resource Type | API Version |
| :-- | :-- |
| `accessPackageCatalog` | Local |
| `accessPackageCatalogResource` | Local |

## Usage examples

The following section provides usage examples for the module, which were used to validate and deploy the module successfully. For a full reference, please review the module's test folder in the repository.

>**Note**: Each example lists all the required parameters first, followed by the rest - each in alphabetical order.

>**Note**: To reference the module, please use the following syntax `'res/graph/identity-governance/entitlement-management/catalogs/main.bicep'`.

### Example 1: _Minimal Catalog_

This example deploys a catalog with minimal parameters.

<details>

<summary>via Bicep module</summary>

```bicep
module catalog 'res/graph/identity-governance/entitlement-management/catalogs/main.bicep' = {
  name: 'catalogDeployment'
  params: {
    entitlementToken: '<entitlementToken>'
    name: 'Engineering Resources'
  }
}
```

</details>

<details>

<summary>via Bicep parameters file</summary>

```bicep
using 'res/graph/identity-governance/entitlement-management/catalogs/main.bicep'

param entitlementToken = '<entitlementToken>'
param name = 'Engineering Resources'
```

</details>

### Example 2: _Catalog with Group Resources_

This example deploys a catalog with Entra ID Group resources.

<details>

<summary>via Bicep module</summary>

```bicep
module catalog 'res/graph/identity-governance/entitlement-management/catalogs/main.bicep' = {
  name: 'catalogDeployment'
  params: {
    entitlementToken: '<entitlementToken>'
    name: 'Engineering Resources'
    catalogDescription: 'Contains all engineering team groups and applications'
    isExternallyVisible: true
    resources: [
      {
        originId: '<group-guid-1>'
        originSystem: 'AadGroup'
        displayName: 'Engineering Team'
        description: 'Main engineering team group'
      }
      {
        originId: '<group-guid-2>'
        originSystem: 'AadGroup'
        displayName: 'DevOps Team'
        description: 'DevOps engineering group'
      }
    ]
  }
}
```

</details>

### Example 3: _Catalog with Mixed Resources_

This example deploys a catalog with groups, applications, and SharePoint sites.

<details>

<summary>via Bicep module</summary>

```bicep
module catalog 'res/graph/identity-governance/entitlement-management/catalogs/main.bicep' = {
  name: 'catalogDeployment'
  params: {
    entitlementToken: '<entitlementToken>'
    name: 'Engineering Resources'
    catalogDescription: 'Complete engineering resource catalog'
    isExternallyVisible: true
    catalogType: 'UserManaged'
    state: 'Published'
    resources: [
      {
        originId: '<group-guid>'
        originSystem: 'AadGroup'
        displayName: 'Engineering Team'
      }
      {
        originId: '<app-guid>'
        originSystem: 'AadApplication'
        displayName: 'Engineering Portal'
      }
      {
        originId: 'https://contoso.sharepoint.com/sites/engineering'
        originSystem: 'SharePointOnline'
        displayName: 'Engineering SharePoint Site'
      }
    ]
  }
}
```

</details>

## Parameters

**Required parameters**

| Parameter | Type | Description |
| :-- | :-- | :-- |
| [`entitlementToken`](#parameter-entitlementtoken) | securestring | Entitlement Management API token (Graph API token with EntitlementManagement.ReadWrite.All permission) |
| [`name`](#parameter-name) | string | The display name of the catalog. |

**Optional parameters**

| Parameter | Type | Description |
| :-- | :-- | :-- |
| [`catalogDescription`](#parameter-catalogdescription) | string | Description of the catalog. |
| [`catalogType`](#parameter-catalogtype) | string | Catalog type. |
| [`isExternallyVisible`](#parameter-isexternallyvisible) | bool | Whether the catalog is visible to external users. |
| [`resources`](#parameter-resources) | array | Resources to add to the catalog. |
| [`state`](#parameter-state) | string | State of the catalog. |

### Parameter: `entitlementToken`

Entitlement Management API token (Graph API token with EntitlementManagement.ReadWrite.All permission)

- Required: Yes
- Type: securestring

### Parameter: `name`

The display name of the catalog.

- Required: Yes
- Type: string

### Parameter: `catalogDescription`

Description of the catalog.

- Required: No
- Type: string

### Parameter: `catalogType`

Catalog type.

- Required: No
- Type: string
- Default: `'UserManaged'`
- Allowed:
  ```Bicep
  [
    'ServiceDefault'
    'ServiceManaged'
    'UserManaged'
  ]
  ```

### Parameter: `isExternallyVisible`

Whether the catalog is visible to external users.

- Required: No
- Type: bool
- Default: `False`

### Parameter: `resources`

Resources to add to the catalog.

- Required: No
- Type: array

**Required parameters**

| Parameter | Type | Description |
| :-- | :-- | :-- |
| [`originId`](#parameter-resourcesoriginid) | string | The origin ID of the resource (Entra ID Group GUID, Application GUID, or SharePoint site URL). |
| [`originSystem`](#parameter-resourcesoriginsystem) | string | The type of resource. |

**Optional parameters**

| Parameter | Type | Description |
| :-- | :-- | :-- |
| [`description`](#parameter-resourcesdescription) | string | Description of the resource. |
| [`displayName`](#parameter-resourcesdisplayname) | string | Display name of the resource. |
| [`justification`](#parameter-resourcesjustification) | string | Justification for adding the resource to the catalog. |

### Parameter: `resources.originId`

The origin ID of the resource (Entra ID Group GUID, Application GUID, or SharePoint site URL).

- Required: Yes
- Type: string

### Parameter: `resources.originSystem`

The type of resource.

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

### Parameter: `resources.description`

Description of the resource.

- Required: No
- Type: string

### Parameter: `resources.displayName`

Display name of the resource.

- Required: No
- Type: string

### Parameter: `resources.justification`

Justification for adding the resource to the catalog.

- Required: No
- Type: string

### Parameter: `state`

State of the catalog.

- Required: No
- Type: string
- Default: `'Published'`
- Allowed:
  ```Bicep
  [
    'Published'
    'Unpublished'
  ]
  ```

## Outputs

| Output | Type | Description |
| :-- | :-- | :-- |
| `name` | string | The name of the created catalog. |
| `resourceId` | string | The ID of the created catalog. |
| `resources` | array | The resources added to the catalog. |

## Cross-referenced modules

This section gives you an overview of all local-referenced module files (i.e., other modules that are referenced in this module) and all remote-referenced files (i.e., Bicep modules that are referenced from a Bicep Registry or Template Specs).

| Reference | Type |
| :-- | :-- |
| `resources/main.bicep` | Local reference |

## Notes

### Resource Origin IDs

When adding resources to a catalog, use the following origin ID formats:

1. **Entra ID Groups**: Use the group's GUID (e.g., `12345678-1234-1234-1234-123456789abc`)
2. **Entra ID Applications**: Use the application's GUID
3. **SharePoint Sites**: Use the full SharePoint site URL (e.g., `https://contoso.sharepoint.com/sites/engineering`)

### Catalog Types

- **UserManaged**: Standard catalog managed by catalog owners
- **ServiceManaged**: System-managed catalog (generally for internal use)
- **ServiceDefault**: Default service catalog

### Prerequisites

- Graph API token with `EntitlementManagement.ReadWrite.All` permission
- Resources (groups, applications, SharePoint sites) must exist in the tenant
- Appropriate permissions to access the resources being added

### Resource Addition Process

Resources are added asynchronously. The module returns immediately after initiating the request. Check the `requestState` and `requestStatus` in the outputs to verify the status:

- `requestState`: `Delivered`, `DeliveryFailed`, `Denied`, `Scheduled`, etc.
- `requestStatus`: Additional status information

It may take a few minutes for resources to become available in the catalog.
