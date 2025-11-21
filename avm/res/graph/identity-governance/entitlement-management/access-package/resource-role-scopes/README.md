# Access Package Resource Role Scope

This module adds a resource role scope to an Access Package. Resource role scopes define specific roles (like Group Member, SharePoint Contributor) from catalog resources that are granted when the access package is assigned.

## Navigation

- [Resource Types](#resource-types)
- [Usage examples](#usage-examples)
- [Parameters](#parameters)
- [Outputs](#outputs)

## Resource Types

| Resource Type | API Version |
| :-- | :-- |
| `accessPackageResourceRoleScope` | Local |

## Usage examples

The following section provides usage examples for the module, which were used to validate and deploy the module successfully. For a full reference, please review the module's test folder in the repository.

>**Note**: Each example lists all the required parameters first, followed by the rest - each in alphabetical order.

>**Note**: To reference the module, please use the following syntax `'res/graph/identity-governance/entitlement-management/access-package/resource-role-scopes/main.bicep'`.

>**Note**: This module is typically called from the parent access-package module, but can be used standalone.

### Example 1: _Add Group Member Role_

This example adds a group member role to an access package.

<details>

<summary>via Bicep module</summary>

```bicep
module resourceRoleScope 'res/graph/identity-governance/entitlement-management/access-package/resource-role-scopes/main.bicep' = {
  name: 'resourceRoleScopeDeployment'
  params: {
    entitlementToken: '<entitlementToken>'
    accessPackageName: 'Developer Access'
    catalogName: 'Engineering Resources'
    resourceOriginId: '<group-guid>'
    roleOriginId: 'Member_<group-guid>'
    resourceOriginSystem: 'AadGroup'
    roleDisplayName: 'Member'
  }
}
```

</details>

### Example 2: _Add Group Owner Role_

This example adds a group owner role to an access package.

<details>

<summary>via Bicep module</summary>

```bicep
module resourceRoleScope 'res/graph/identity-governance/entitlement-management/access-package/resource-role-scopes/main.bicep' = {
  name: 'resourceRoleScopeDeployment'
  params: {
    entitlementToken: '<entitlementToken>'
    accessPackageName: 'Admin Access'
    catalogName: 'Engineering Resources'
    resourceOriginId: '<group-guid>'
    roleOriginId: 'Owner_<group-guid>'
    resourceOriginSystem: 'AadGroup'
    roleDisplayName: 'Owner'
  }
}
```

</details>

### Example 3: _Add SharePoint Contributor Role_

This example adds a SharePoint Contributors role to an access package.

<details>

<summary>via Bicep module</summary>

```bicep
module resourceRoleScope 'res/graph/identity-governance/entitlement-management/access-package/resource-role-scopes/main.bicep' = {
  name: 'resourceRoleScopeDeployment'
  params: {
    entitlementToken: '<entitlementToken>'
    accessPackageName: 'SharePoint Contributors'
    catalogName: 'Engineering Resources'
    resourceOriginId: 'https://contoso.sharepoint.com/sites/engineering'
    roleOriginId: '4'
    resourceOriginSystem: 'SharePointOnline'
    roleDisplayName: 'Contributors'
  }
}
```

</details>

### Example 4: _With Catalog Resource Reference_

This example uses a catalog resource ID reference (from catalog resource output).

<details>

<summary>via Bicep module</summary>

```bicep
module catalogResource 'res/graph/identity-governance/entitlement-management/catalogs/resources/main.bicep' = {
  name: 'catalogResourceDeployment'
  params: {
    entitlementToken: entitlementToken
    catalogName: 'Engineering Resources'
    originId: groupId
    originSystem: 'AadGroup'
  }
}

module resourceRoleScope 'res/graph/identity-governance/entitlement-management/access-package/resource-role-scopes/main.bicep' = {
  name: 'resourceRoleScopeDeployment'
  params: {
    entitlementToken: '<entitlementToken>'
    accessPackageName: 'Developer Access'
    catalogName: 'Engineering Resources'
    resourceOriginId: '<group-guid>'
    roleOriginId: 'Member_<group-guid>'
    resourceOriginSystem: 'AadGroup'
    roleDisplayName: 'Member'
    catalogResourceId: catalogResource.outputs.resourceId
  }
}
```

</details>

## Parameters

**Required parameters**

| Parameter | Type | Description |
| :-- | :-- | :-- |
| [`accessPackageName`](#parameter-accesspackagename) | string | The name of the access package. |
| [`catalogName`](#parameter-catalogname) | string | The name of the catalog. |
| [`entitlementToken`](#parameter-entitlementtoken) | securestring | Entitlement Management API token (Graph API token with EntitlementManagement.ReadWrite.All permission) |
| [`resourceOriginId`](#parameter-resourceoriginid) | string | The origin ID of the resource (Entra ID Group GUID, Application GUID, or SharePoint site URL). |
| [`roleOriginId`](#parameter-roleoriginid) | string | The origin ID of the role. Groups: 'Member_{groupGuid}' or 'Owner_{groupGuid}'. SharePoint: '3','4','5'. |

**Optional parameters**

| Parameter | Type | Description |
| :-- | :-- | :-- |
| [`catalogResourceId`](#parameter-catalogresourceid) | string | The ID of the catalog resource (from accessPackageCatalogResource.id output). |
| [`resourceOriginSystem`](#parameter-resourceoriginsystem) | string | The origin system of the resource. |
| [`roleDisplayName`](#parameter-roledisplayname) | string | Display name of the role. |

### Parameter: `accessPackageName`

The name of the access package.

- Required: Yes
- Type: string

### Parameter: `catalogName`

The name of the catalog.

- Required: Yes
- Type: string

### Parameter: `entitlementToken`

Entitlement Management API token (Graph API token with EntitlementManagement.ReadWrite.All permission)

- Required: Yes
- Type: securestring

### Parameter: `resourceOriginId`

The origin ID of the resource (Entra ID Group GUID, Application GUID, or SharePoint site URL).

- Required: Yes
- Type: string

### Parameter: `roleOriginId`

The origin ID of the role. Groups: 'Member_{groupGuid}' or 'Owner_{groupGuid}'. SharePoint: '3','4','5'.

- Required: Yes
- Type: string

### Parameter: `catalogResourceId`

The ID of the catalog resource (from accessPackageCatalogResource.id output).

- Required: No
- Type: string

### Parameter: `resourceOriginSystem`

The origin system of the resource.

- Required: No
- Type: string
- Default: `'AadGroup'`
- Allowed:
  ```Bicep
  [
    'AadApplication'
    'AadGroup'
    'SharePointOnline'
  ]
  ```

### Parameter: `roleDisplayName`

Display name of the role.

- Required: No
- Type: string

## Outputs

| Output | Type | Description |
| :-- | :-- | :-- |
| `createdDateTime` | string | The date/time when the resource role scope was created. |
| `resourceId` | string | The unique identifier of the resource role scope. |
| `resourceOriginId` | string | The origin ID of the resource. |
| `roleDisplayName` | string | The display name of the role. |
| `roleOriginId` | string | The origin ID of the role. |

## Notes

### Role Origin ID Formats

The `roleOriginId` parameter format depends on the resource type:

#### Entra ID Groups

- **Member Role**: `Member_{groupGuid}`
  - Example: `Member_12345678-1234-1234-1234-123456789abc`
- **Owner Role**: `Owner_{groupGuid}`
  - Example: `Owner_12345678-1234-1234-1234-123456789abc`

Replace `{groupGuid}` with the actual group's GUID (same as `resourceOriginId`).

#### SharePoint Sites

- **Viewers (Read)**: `5`
- **Contributors (Edit)**: `4`
- **Creators (Design)**: `3`

Use the numeric role ID as a string.

#### Applications

For application roles, use the role ID from the application's manifest. This varies by application.

### Resource Origin ID Formats

Use the following formats for the `resourceOriginId` parameter:

1. **Entra ID Groups**: Group GUID (e.g., `12345678-1234-1234-1234-123456789abc`)
2. **Entra ID Applications**: Application GUID
3. **SharePoint Sites**: Full site URL (e.g., `https://contoso.sharepoint.com/sites/engineering`)

### Prerequisites

- An existing catalog (created via `catalogs/main.bicep`)
- An existing access package (created via `access-package/main.bicep`)
- The resource must exist in the catalog (added via `catalogs/resources/main.bicep` or during catalog creation)
- Graph API token with `EntitlementManagement.ReadWrite.All` permission

### Common Patterns

#### Multiple Roles from Same Group

You can add multiple roles from the same group (e.g., both Member and Owner):

```bicep
resourceRoleScopes: [
  {
    resourceOriginId: '<group-guid>'
    roleOriginId: 'Member_<group-guid>'
    resourceOriginSystem: 'AadGroup'
    roleDisplayName: 'Member'
  }
  {
    resourceOriginId: '<group-guid>'
    roleOriginId: 'Owner_<group-guid>'
    resourceOriginSystem: 'AadGroup'
    roleDisplayName: 'Owner'
  }
]
```

#### Combining Different Resource Types

You can combine groups, applications, and SharePoint sites in one access package:

```bicep
resourceRoleScopes: [
  {
    resourceOriginId: '<group-guid>'
    roleOriginId: 'Member_<group-guid>'
    resourceOriginSystem: 'AadGroup'
  }
  {
    resourceOriginId: 'https://contoso.sharepoint.com/sites/engineering'
    roleOriginId: '4'
    resourceOriginSystem: 'SharePointOnline'
  }
]
```

### Troubleshooting

If resource role scope addition fails:

1. **Verify the resource exists in the catalog**: Check the catalog's resources using the Azure Portal or Graph API
2. **Confirm role ID format**: Ensure `roleOriginId` matches the expected format for the resource type
3. **Check resource origin ID**: Verify `resourceOriginId` matches what was used when adding the resource to the catalog
4. **Validate resource origin system**: Ensure `resourceOriginSystem` matches the actual resource type

### Usage in Parent Module

This module is automatically called by the parent access-package module when `resourceRoleScopes` are specified. You typically don't need to call it directly unless adding roles to an existing access package.
