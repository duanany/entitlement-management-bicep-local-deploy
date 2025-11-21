metadata name = 'Access Package Catalog'
metadata description = 'This module deploys an Entra ID Access Package Catalog.'
metadata owner = 'Bicep Local Deploy'

targetScope = 'local'

extension entitlementmgmt with {
  entitlementToken: entitlementToken
}

@secure()
@description('Required. Entitlement Management API token (Graph API token with EntitlementManagement.ReadWrite.All permission)')
param entitlementToken string

@description('Required. The display name of the catalog.')
param name string

@description('Optional. Description of the catalog.')
param catalogDescription string?

@description('Optional. Whether the catalog is visible to external users.')
param isExternallyVisible bool = false

@description('Optional. Catalog type.')
@allowed([
  'UserManaged'
  'ServiceDefault'
  'ServiceManaged'
])
param catalogType string = 'UserManaged'

@description('Optional. State of the catalog.')
@allowed([
  'Published'
  'Unpublished'
])
param state string = 'Published'

@description('Optional. Resources to add to the catalog.')
param resources resourceType[]?

resource catalog 'accessPackageCatalog' = {
  displayName: name
  description: catalogDescription
  isExternallyVisible: isExternallyVisible
  catalogType: catalogType
  state: state
}

module catalog_resources 'resources/main.bicep' = [for (resource, index) in (resources ?? []): {
  params: {
    entitlementToken: entitlementToken
    catalogName: catalog.displayName
    originId: resource.originId
    originSystem: resource.originSystem
    displayName: resource.?displayName
    resourceDescription: resource.?description
    justification: resource.?justification
  }
}]

// ================ //
// Outputs          //
// ================ //

@description('The ID of the created catalog.')
output resourceId string = catalog.id

@description('The name of the created catalog.')
output name string = catalog.displayName

@description('The resources added to the catalog.')
output resources resourceOutputType[] = [
  for index in range(0, length(resources ?? [])): {
    resourceId: catalog_resources[index].outputs.resourceId
    displayName: catalog_resources[index].outputs.displayName
    description: catalog_resources[index].outputs.description
    originId: catalog_resources[index].outputs.originId
    originSystem: catalog_resources[index].outputs.originSystem
    requestState: catalog_resources[index].outputs.requestState
    requestStatus: catalog_resources[index].outputs.requestStatus
  }
]

// ================ //
// Definitions      //
// ================ //

@export()
type resourceType = {
  @description('Required. The origin ID of the resource (Entra ID Group GUID, Application GUID, or SharePoint site URL).')
  originId: string

  @description('Required. The type of resource.')
  originSystem: ('AadGroup' | 'AadApplication' | 'SharePointOnline')

  @description('Optional. Display name of the resource.')
  displayName: string?

  @description('Optional. Description of the resource.')
  description: string?

  @description('Optional. Justification for adding the resource to the catalog.')
  justification: string?
}

@export()
type resourceOutputType = {
  @description('The unique identifier of the resource in the catalog.')
  resourceId: string

  @description('The display name of the resource.')
  displayName: string

  @description('The description of the resource.')
  description: string

  @description('The origin ID of the resource.')
  originId: string

  @description('The origin system of the resource.')
  originSystem: string

  @description('The state of the resource request.')
  requestState: string

  @description('The status of the resource request.')
  requestStatus: string
}
