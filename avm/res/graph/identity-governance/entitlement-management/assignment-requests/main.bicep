metadata name = 'Access Package Assignment Request'
metadata description = 'This module creates an Entra ID Access Package Assignment (via assignment request).'
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

@description('Required. The name of the assignment policy.')
param assignmentPolicyName string

@description('Optional. The object ID (GUID) of the user to assign.')
param targetUserId string?

@description('Optional. The email of the user to assign (for external users).')
param targetUserEmail string?

@description('Optional. Justification for the assignment request.')
param justification string?

@description('Optional. Schedule for time-bound access.')
param schedule object?

// Lookup existing resources by name
resource catalog 'accessPackageCatalog' existing = {
  displayName: catalogName
}

resource accessPackage 'accessPackage' existing = {
  displayName: accessPackageName
  catalogId: catalog.id
}

resource assignmentPolicy 'accessPackageAssignmentPolicy' existing = {
  accessPackageId: accessPackage.id
  displayName: assignmentPolicyName
}

resource assignment 'accessPackageAssignment' = {
  accessPackageId: accessPackage.id
  assignmentPolicyId: assignmentPolicy.id
  targetUserId: targetUserId
  targetUserEmail: targetUserEmail
  justification: justification
  schedule: schedule
}

@description('The ID of the created assignment.')
output resourceId string = assignment.id ?? ''

@description('The ID of the access package.')
output accessPackageId string = accessPackage.id

@description('The ID of the assignment policy.')
output assignmentPolicyId string = assignmentPolicy.id

@description('The object ID of the target user.')
output targetUserId string = targetUserId ?? ''

@description('The state of the assignment.')
output state string = assignment.state ?? ''
