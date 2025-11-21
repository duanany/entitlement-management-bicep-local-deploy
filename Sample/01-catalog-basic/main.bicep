targetScope = 'local'

@secure()
@description('Entitlement Management API token (Graph API token with EntitlementManagement.ReadWrite.All permission)')
param entitlementToken string

// ==========================================
// BASIC CATALOG
// ==========================================

module catalog '../../avm/res/graph/identity-governance/entitlement-management/catalogs/main.bicep' = {

  name: 'catalogDeployment'
  params: {
    entitlementToken: entitlementToken
    name: 'Bicep Local - Basic Catalog'
    catalogDescription: 'Simple catalog for organizing access packages'
    isExternallyVisible: false
    catalogType: 'UserManaged'
    state: 'Published'
  }
}

// ==========================================
// ACCESS PACKAGE
// ==========================================

module accessPackage '../../avm/res/graph/identity-governance/entitlement-management/access-package/main.bicep' = {

  name: 'accessPackageDeployment'
  params: {
    entitlementToken: entitlementToken
    name: 'Bicep Local - Basic Access Package'
    catalogName: 'Bicep Local - Basic Catalog'
    accessPackageDescription: 'Simple access package in basic catalog'
    isHidden: false
  }
  dependsOn: [
    catalog
  ]
}

// ==========================================
// ASSIGNMENT POLICY
// ==========================================

module assignmentPolicy '../../avm/res/graph/identity-governance/entitlement-management/assignment-policies/main.bicep' = {

  name: 'assignmentPolicyDeployment'
  params: {
    entitlementToken: entitlementToken
    name: 'Policy: All Users Can Request'
    accessPackageName: 'Bicep Local - Basic Access Package'
    catalogName: 'Bicep Local - Basic Catalog'
    policyDescription: 'Any member user can request access - no approval required'
    allowedTargetScope: 'AllMemberUsers'
    requestorSettings: {
      scopeType: 'AllExistingDirectoryMemberUsers'
      acceptRequests: true
    }
    requestApprovalSettings: {
      isApprovalRequired: false
      isApprovalRequiredForExtension: false
      isRequestorJustificationRequired: true
      approvalMode: 'NoApproval'
    }
    durationInDays: 365
    canExtend: true
  }
  dependsOn: [
    accessPackage
  ]
}

// ==========================================
// OUTPUTS
// ==========================================

output catalogId string = catalog.outputs.resourceId
output accessPackageId string = accessPackage.outputs.resourceId
output policyId string = assignmentPolicy.outputs.resourceId
