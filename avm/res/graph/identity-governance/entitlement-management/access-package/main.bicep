metadata name = 'Access Package'
metadata description = 'This module deploys an Entra ID Access Package with optional resource role scopes.'
metadata owner = 'Bicep Local Deploy'

targetScope = 'local'

extension entitlementmgmt with {
  entitlementToken: entitlementToken
}

@secure()
@description('Required. Entitlement Management API token (Graph API token with EntitlementManagement.ReadWrite.All permission)')
param entitlementToken string

@description('Required. The display name of the access package.')
param name string

@description('Required. The name of the catalog this access package belongs to.')
param catalogName string

@description('Optional. Description of the access package.')
param accessPackageDescription string?

@description('Optional. Whether the access package is hidden from requestors.')
param isHidden bool = false

@description('Optional. Resource role scopes to add to the access package.')
param resourceRoleScopes resourceRoleScopeType[]?

// Lookup existing catalog by name
resource catalog 'accessPackageCatalog' existing = {
  displayName: catalogName
}

resource accessPackage 'accessPackage' = {
  displayName: name
  catalogId: catalog.id
  description: accessPackageDescription
  isHidden: isHidden
}

module access_package_resource_role_scopes 'resource-role-scopes/main.bicep' = [for (resourceRoleScope, index) in (resourceRoleScopes ?? []): {
  params: {
    entitlementToken: entitlementToken
    accessPackageName: accessPackage.displayName
    catalogName: catalogName
    resourceOriginId: resourceRoleScope.resourceOriginId
    roleOriginId: resourceRoleScope.roleOriginId
    resourceOriginSystem: resourceRoleScope.?resourceOriginSystem
    roleDisplayName: resourceRoleScope.?roleDisplayName
    catalogResourceId: resourceRoleScope.?catalogResourceId
  }
}]

@description('The ID of the created access package.')
output resourceId string = accessPackage.id

@description('The name of the created access package.')
output name string = accessPackage.displayName

@description('The description of the access package.')
output description string = accessPackage.description ?? ''

@description('The ID of the catalog this access package belongs to.')
output catalogId string = accessPackage.catalogId

@description('Whether the access package is hidden.')
output isHidden bool = accessPackage.isHidden

@description('The date/time when the access package was created.')
output createdDateTime string = accessPackage.createdDateTime ?? ''

@description('The date/time when the access package was last modified.')
output modifiedDateTime string = accessPackage.modifiedDateTime ?? ''

@description('The resource role scopes added to the access package.')
output resourceRoleScopes resourceRoleScopeOutputType[] = [
  for index in range(0, length(resourceRoleScopes ?? [])): {
    resourceId: access_package_resource_role_scopes[index].outputs.resourceId
    resourceOriginId: access_package_resource_role_scopes[index].outputs.resourceOriginId
    roleOriginId: access_package_resource_role_scopes[index].outputs.roleOriginId
    roleDisplayName: access_package_resource_role_scopes[index].outputs.roleDisplayName
    createdDateTime: access_package_resource_role_scopes[index].outputs.createdDateTime
  }
]

// ================ //
// Definitions      //
// ================ //

@export()
type resourceRoleScopeType = {
  @description('Required. The origin ID of the resource (Entra ID Group GUID, Application GUID, or SharePoint site URL).')
  resourceOriginId: string

  @description('Required. The origin ID of the role. Groups: \'Member_{groupGuid}\' or \'Owner_{groupGuid}\'. SharePoint: \'3\',\'4\',\'5\'.')
  roleOriginId: string

  @description('Optional. The origin system of the resource.')
  resourceOriginSystem: ('AadGroup' | 'AadApplication' | 'SharePointOnline')?

  @description('Optional. Display name of the role.')
  roleDisplayName: string?

  @description('Optional. The ID of the catalog resource (from accessPackageCatalogResource.id output).')
  catalogResourceId: string?
}

@export()
type resourceRoleScopeOutputType = {
  @description('The unique identifier of the resource role scope.')
  resourceId: string

  @description('The origin ID of the resource.')
  resourceOriginId: string

  @description('The origin ID of the role.')
  roleOriginId: string

  @description('The display name of the role.')
  roleDisplayName: string

  @description('The date/time when the resource role scope was created.')
  createdDateTime: string
}
