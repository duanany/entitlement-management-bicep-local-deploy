metadata name = 'Access Package Resource Role Scope'
metadata description = 'This module adds a resource role scope to an Access Package.'
metadata owner = 'Bicep Local Deploy'

targetScope = 'local'

extension entitlementmgmt with {
  entitlementToken: entitlementToken
}

@secure()
@description('Required. Entitlement Management API token (Graph API token with EntitlementManagement.ReadWrite.All permission)')
param entitlementToken string

@description('Required. The name of the access package.')
param accessPackageName string

@description('Required. The name of the catalog.')
param catalogName string

@description('Required. The origin ID of the resource (Entra ID Group GUID, Application GUID, or SharePoint site URL).')
param resourceOriginId string

@description('Required. The origin ID of the role. Groups: \'Member_{groupGuid}\' or \'Owner_{groupGuid}\'. SharePoint: \'3\',\'4\',\'5\'.')
param roleOriginId string

@description('Optional. The origin system of the resource.')
@allowed([
  'AadGroup'
  'AadApplication'
  'SharePointOnline'
])
param resourceOriginSystem string = 'AadGroup'

@description('Optional. Display name of the role.')
param roleDisplayName string?

@description('Optional. The ID of the catalog resource (from accessPackageCatalogResource.id output).')
param catalogResourceId string?

resource catalog 'accessPackageCatalog' existing = {
  displayName: catalogName
}

resource accessPackage 'accessPackage' existing = {
  displayName: accessPackageName
  catalogId: catalog.id
}

resource resourceRoleScope 'accessPackageResourceRoleScope' = {
  accessPackageId: accessPackage.id
  resourceOriginId: resourceOriginId
  roleOriginId: roleOriginId
  resourceOriginSystem: resourceOriginSystem
  roleDisplayName: roleDisplayName
  catalogResourceId: catalogResourceId
}

@description('The unique identifier of the resource role scope.')
output resourceId string = resourceRoleScope.id

@description('The origin ID of the resource.')
output resourceOriginId string = resourceRoleScope.resourceOriginId

@description('The origin ID of the role.')
output roleOriginId string = resourceRoleScope.roleOriginId

@description('The display name of the role.')
output roleDisplayName string = resourceRoleScope.roleDisplayName ?? ''

@description('The date/time when the resource role scope was created.')
output createdDateTime string = resourceRoleScope.createdDateTime ?? ''
