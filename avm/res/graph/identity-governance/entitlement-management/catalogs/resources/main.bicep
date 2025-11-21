metadata name = 'Access Package Catalog Resource'
metadata description = 'This module adds a resource (Group, App, SharePoint Site) to an Access Package Catalog.'
metadata owner = 'Bicep Local Deploy'

targetScope = 'local'

extension entitlementmgmt with {
  entitlementToken: entitlementToken
}

@secure()
@description('Required. Entitlement Management API token (Graph API token with EntitlementManagement.ReadWrite.All permission)')
param entitlementToken string

@description('Required. The name of the catalog to add the resource to.')
param catalogName string

@description('Required. The origin ID of the resource (Entra ID Group GUID, Application GUID, or SharePoint site URL).')
param originId string

@description('Required. The type of resource.')
@allowed([
  'AadGroup'
  'AadApplication'
  'SharePointOnline'
])
param originSystem string

@description('Optional. Display name of the resource.')
param displayName string?

@description('Optional. Description of the resource.')
param resourceDescription string?

@description('Optional. Justification for adding the resource to the catalog.')
param justification string?

resource catalog 'accessPackageCatalog' existing = {
  displayName: catalogName
}

resource catalogResource 'accessPackageCatalogResource' = {
  catalogId: catalog.id
  originId: originId
  originSystem: originSystem
  displayName: displayName
  description: resourceDescription
  justification: justification
}

@description('The unique identifier of the resource in the catalog.')
output resourceId string = catalogResource.id

@description('The display name of the resource.')
output displayName string = catalogResource.displayName

@description('The description of the resource.')
output description string = catalogResource.description

@description('The origin ID of the resource.')
output originId string = catalogResource.originId

@description('The origin system of the resource.')
output originSystem string = catalogResource.originSystem

@description('The state of the resource request.')
output requestState string = catalogResource.requestState

@description('The status of the resource request.')
output requestStatus string = catalogResource.requestStatus
