targetScope = 'local'

extension entitlementmgmt with {
  entitlementToken: entitlementToken
}

@secure()
@description('Entitlement Management API token (Graph API token with EntitlementManagement.ReadWrite.All permission)')
param entitlementToken string

// ==========================================
// BASIC CATALOG
// ==========================================

resource catalog 'accessPackageCatalog' = {
  displayName: 'Bicep Local - Basic Catalog'
  description: 'Simple catalog for organizing access packages'
  isExternallyVisible: false
  catalogType: 'userManaged'
  state: 'published'
}

// ==========================================
// ACCESS PACKAGE
// ==========================================

resource accessPackage 'accessPackage' = {
  displayName: 'Bicep Local - Basic Access Package'
  description: 'Simple access package in basic catalog'
  catalogId: catalog.id
  isHidden: false
}

// ==========================================
// ASSIGNMENT POLICY
// ==========================================

resource assignmentPolicy 'accessPackageAssignmentPolicy' = {
  displayName: 'Policy: All Users Can Request'
  description: 'Any member user can request access - no approval required'
  accessPackageId: accessPackage.id
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

// ==========================================
// OUTPUTS
// ==========================================

output catalogId string = catalog.id
output accessPackageId string = accessPackage.id
output policyId string = assignmentPolicy.id
