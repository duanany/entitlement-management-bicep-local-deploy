metadata name = 'Access Package Assignment Policy'
metadata description = 'This module deploys an Entra ID Access Package Assignment Policy.'
metadata owner = 'Bicep Local Deploy'

targetScope = 'local'

extension entitlementmgmt with {
  entitlementToken: entitlementToken
}

@secure()
@description('Required. Entitlement Management API token (Graph API token with EntitlementManagement.ReadWrite.All permission)')
param entitlementToken string

@description('Required. The display name of the assignment policy.')
param name string

@description('Required. The name of the access package.')
param accessPackageName string

@description('Required. The name of the catalog.')
param catalogName string

@description('Optional. Description of the policy.')
param policyDescription string?

@description('Optional. Who can request this access package.')
@allowed([
  'NotSpecified'
  'AllMemberUsers'
  'AllDirectoryUsers'
  'AllConfiguredConnectedOrganizationUsers'
  'AllExistingConnectedOrganizationUsers'
  'AllExternalUsers'
  'SpecificDirectoryUsers'
  'SpecificConnectedOrganizationUsers'
  'NoSubjects'
])
param allowedTargetScope string = 'AllMemberUsers'

@description('Optional. Specific users, groups, or connected organizations who can request.')
param specificAllowedTargets array?

@description('Optional. Allow assignees to request more time before expiration.')
param canExtend bool = false

@description('Optional. Number of days assignments remain active.')
param durationInDays int = 365

@description('Optional. Specific expiration date/time (UTC ISO 8601).')
param expirationDateTime string?

@description('Optional. Permit requestors to specify custom start/end dates.')
param isCustomAssignmentScheduleAllowed bool = false

@description('Optional. Requestor settings defining who can submit requests.')
param requestorSettings object?

@description('Optional. Approval workflow definition.')
param requestApprovalSettings object?

@description('Optional. Access review configuration for periodic attestation.')
param reviewSettings object?

@description('Optional. Automatic request configuration for attribute-based flows.')
param automaticRequestSettings object?

@description('Optional. Custom questions to present during submission.')
param questions array?

// Lookup existing resources by name
resource catalog 'accessPackageCatalog' existing = {
  displayName: catalogName
}

resource accessPackage 'accessPackage' existing = {
  displayName: accessPackageName
  catalogId: catalog.id
}

resource policy 'accessPackageAssignmentPolicy' = {
  displayName: name
  accessPackageId: accessPackage.id
  description: policyDescription
  allowedTargetScope: allowedTargetScope
  specificAllowedTargets: specificAllowedTargets
  canExtend: canExtend
  durationInDays: durationInDays
  expirationDateTime: expirationDateTime
  isCustomAssignmentScheduleAllowed: isCustomAssignmentScheduleAllowed
  requestorSettings: requestorSettings
  requestApprovalSettings: requestApprovalSettings
  reviewSettings: reviewSettings
  automaticRequestSettings: automaticRequestSettings
  questions: questions
}

@description('The ID of the created assignment policy.')
output resourceId string = policy.id ?? ''

@description('The name of the assignment policy.')
output name string = policy.displayName

@description('The description of the assignment policy.')
output description string = policy.description ?? ''

@description('The allowed target scope.')
output allowedTargetScope string = allowedTargetScope

@description('The ID of the access package this policy applies to.')
output accessPackageId string = accessPackage.id

@description('The date/time when the policy was created.')
output createdDateTime string = policy.createdDateTime ?? ''

@description('The date/time when the policy was last modified.')
output modifiedDateTime string = policy.modifiedDateTime ?? ''
