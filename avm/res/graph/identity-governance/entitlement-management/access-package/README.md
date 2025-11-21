# Access Package

This module deploys an Entra ID Access Package with optional resource role scopes.

## Navigation

- [Resource Types](#resource-types)
- [Usage examples](#usage-examples)
- [Parameters](#parameters)
- [Outputs](#outputs)
- [Cross-referenced modules](#cross-referenced-modules)

## Resource Types

| Resource Type | API Version |
| :-- | :-- |
| `accessPackage` | Local |
| `accessPackageResourceRoleScope` | Local |

## Usage examples

The following section provides usage examples for the module, which were used to validate and deploy the module successfully. For a full reference, please review the module's test folder in the repository.

>**Note**: Each example lists all the required parameters first, followed by the rest - each in alphabetical order.

>**Note**: To reference the module, please use the following syntax `'res/graph/identity-governance/entitlement-management/access-package/main.bicep'`.

### Example 1: _Minimal Access Package_

This example deploys an access package with minimal parameters.

<details>

<summary>via Bicep module</summary>

```bicep
module accessPackage 'res/graph/identity-governance/entitlement-management/access-package/main.bicep' = {
  name: 'accessPackageDeployment'
  params: {
    entitlementToken: '<entitlementToken>'
    name: 'Developer Access'
    catalogName: 'Engineering Resources'
  }
}
```

</details>

<details>

<summary>via Bicep parameters file</summary>

```bicep
using 'res/graph/identity-governance/entitlement-management/access-package/main.bicep'

param entitlementToken = '<entitlementToken>'
param name = 'Developer Access'
param catalogName = 'Engineering Resources'
```

</details>

### Example 2: _Access Package with Resource Role Scopes_

This example deploys an access package with resource role scopes (group membership).

<details>

<summary>via Bicep module</summary>

```bicep
module accessPackage 'res/graph/identity-governance/entitlement-management/access-package/main.bicep' = {
  name: 'accessPackageDeployment'
  params: {
    entitlementToken: '<entitlementToken>'
    name: 'Developer Access Bundle'
    catalogName: 'Engineering Resources'
    accessPackageDescription: 'Access bundle for developers with group memberships'
    isHidden: false
    resourceRoleScopes: [
      {
        resourceOriginId: '<group-guid-1>'
        roleOriginId: 'Member_<group-guid-1>'
        resourceOriginSystem: 'AadGroup'
        roleDisplayName: 'Member'
      }
      {
        resourceOriginId: '<group-guid-2>'
        roleOriginId: 'Owner_<group-guid-2>'
        resourceOriginSystem: 'AadGroup'
        roleDisplayName: 'Owner'
      }
    ]
  }
}
```

</details>

### Example 3: _Access Package with SharePoint Access_

This example deploys an access package with SharePoint site access.

<details>

<summary>via Bicep module</summary>

```bicep
module accessPackage 'res/graph/identity-governance/entitlement-management/access-package/main.bicep' = {
  name: 'accessPackageDeployment'
  params: {
    entitlementToken: '<entitlementToken>'
    name: 'SharePoint Contributors'
    catalogName: 'Engineering Resources'
    accessPackageDescription: 'Contributors access to Engineering SharePoint site'
    resourceRoleScopes: [
      {
        resourceOriginId: 'https://contoso.sharepoint.com/sites/engineering'
        roleOriginId: '4' // Contributors role
        resourceOriginSystem: 'SharePointOnline'
        roleDisplayName: 'Contributors'
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
| [`catalogName`](#parameter-catalogname) | string | The name of the catalog this access package belongs to. |
| [`entitlementToken`](#parameter-entitlementtoken) | securestring | Entitlement Management API token (Graph API token with EntitlementManagement.ReadWrite.All permission) |
| [`name`](#parameter-name) | string | The display name of the access package. |

**Optional parameters**

| Parameter | Type | Description |
| :-- | :-- | :-- |
| [`accessPackageDescription`](#parameter-accesspackagedescription) | string | Description of the access package. |
| [`isHidden`](#parameter-ishidden) | bool | Whether the access package is hidden from requestors. |
| [`resourceRoleScopes`](#parameter-resourcerolescopes) | array | Resource role scopes to add to the access package. |

### Parameter: `catalogName`

The name of the catalog this access package belongs to.

- Required: Yes
- Type: string

### Parameter: `entitlementToken`

Entitlement Management API token (Graph API token with EntitlementManagement.ReadWrite.All permission)

- Required: Yes
- Type: securestring

### Parameter: `name`

The display name of the access package.

- Required: Yes
- Type: string

### Parameter: `accessPackageDescription`

Description of the access package.

- Required: No
- Type: string

### Parameter: `isHidden`

Whether the access package is hidden from requestors.

- Required: No
- Type: bool
- Default: `False`

### Parameter: `resourceRoleScopes`

Resource role scopes to add to the access package.

- Required: No
- Type: array

**Required parameters**

| Parameter | Type | Description |
| :-- | :-- | :-- |
| [`resourceOriginId`](#parameter-resourcerolescopesresourceoriginid) | string | The origin ID of the resource (Entra ID Group GUID, Application GUID, or SharePoint site URL). |
| [`roleOriginId`](#parameter-resourcerolescopesroleoriginid) | string | The origin ID of the role. Groups: 'Member_{groupGuid}' or 'Owner_{groupGuid}'. SharePoint: '3','4','5'. |

**Optional parameters**

| Parameter | Type | Description |
| :-- | :-- | :-- |
| [`catalogResourceId`](#parameter-resourcerolescopescatalogresourceid) | string | The ID of the catalog resource (from accessPackageCatalogResource.id output). |
| [`resourceOriginSystem`](#parameter-resourcerolescopesresourceoriginsystem) | string | The origin system of the resource. |
| [`roleDisplayName`](#parameter-resourcerolescopesroledisplayname) | string | Display name of the role. |

### Parameter: `resourceRoleScopes.resourceOriginId`

The origin ID of the resource (Entra ID Group GUID, Application GUID, or SharePoint site URL).

- Required: Yes
- Type: string

### Parameter: `resourceRoleScopes.roleOriginId`

The origin ID of the role. Groups: 'Member_{groupGuid}' or 'Owner_{groupGuid}'. SharePoint: '3','4','5'.

- Required: Yes
- Type: string

### Parameter: `resourceRoleScopes.catalogResourceId`

The ID of the catalog resource (from accessPackageCatalogResource.id output).

- Required: No
- Type: string

### Parameter: `resourceRoleScopes.resourceOriginSystem`

The origin system of the resource.

- Required: No
- Type: string
- Allowed:
  ```Bicep
  [
    'AadApplication'
    'AadGroup'
    'SharePointOnline'
  ]
  ```

### Parameter: `resourceRoleScopes.roleDisplayName`

Display name of the role.

- Required: No
- Type: string

## Outputs

| Output | Type | Description |
| :-- | :-- | :-- |
| `catalogId` | string | The ID of the catalog this access package belongs to. |
| `createdDateTime` | string | The date/time when the access package was created. |
| `description` | string | The description of the access package. |
| `isHidden` | bool | Whether the access package is hidden. |
| `modifiedDateTime` | string | The date/time when the access package was last modified. |
| `name` | string | The name of the created access package. |
| `resourceId` | string | The ID of the created access package. |
| `resourceRoleScopes` | array | The resource role scopes added to the access package. |

## Cross-referenced modules

This section gives you an overview of all local-referenced module files (i.e., other modules that are referenced in this module) and all remote-referenced files (i.e., Bicep modules that are referenced from a Bicep Registry or Template Specs).

| Reference | Type |
| :-- | :-- |
| `resource-role-scope/main.bicep` | Local reference |

## Notes

### Resource Role Scope Configuration

When adding resource role scopes, you must specify:

1. **For Entra ID Groups:**
   - `resourceOriginId`: Group GUID (e.g., `12345678-1234-1234-1234-123456789abc`)
   - `roleOriginId`: `Member_{groupGuid}` or `Owner_{groupGuid}` (e.g., `Member_12345678-1234-1234-1234-123456789abc`)
   - `resourceOriginSystem`: `AadGroup`

2. **For Applications:**
   - `resourceOriginId`: Application GUID
   - `roleOriginId`: Application role ID
   - `resourceOriginSystem`: `AadApplication`

3. **For SharePoint Sites:**
   - `resourceOriginId`: SharePoint site URL (e.g., `https://contoso.sharepoint.com/sites/engineering`)
   - `roleOriginId`: Numeric role ID (`3` = Creators, `4` = Contributors, `5` = Viewers)
   - `resourceOriginSystem`: `SharePointOnline`

### Prerequisites

- An existing catalog (created via `catalogs/main.bicep` module)
- Resources must already be added to the catalog (via `catalogs/resources/main.bicep` or included in catalog creation)
- Graph API token with `EntitlementManagement.ReadWrite.All` permission
