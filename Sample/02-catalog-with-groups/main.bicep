targetScope = 'local'

extension entitlementmgmt with {
  entitlementToken: entitlementToken
  groupUserToken: groupUserToken
}

@secure()
@description('Entitlement Management API token (Graph API token with EntitlementManagement.ReadWrite.All permission)')
param entitlementToken string

@secure()
@description('Microsoft Graph API Bearer Token for Group and User operations (requires Group.ReadWrite.All and User.Read.All)')
param groupUserToken string

@description('User ID to add as group member')
param testUserId string = '7a72c098-a42d-489f-a3fa-c2445dec6f9c'

// ==========================================
// SECURITY GROUP
// ==========================================

resource demoSecurityGroup 'securityGroup' = {
  uniqueName: 'bicep-demo-security-group'
  displayName: 'Bicep Local - Demo Security Group'
  description: 'Security group managed by Bicep local-deploy'

  members: [
    testUserId
  ]
}

// ==========================================
// CATALOG
// ==========================================

resource catalog 'accessPackageCatalog' = {
  displayName: 'Bicep Local - Security Group Catalog'
  description: 'Catalog for managing security group membership via access packages'
  isExternallyVisible: false
  catalogType: 'userManaged'
  state: 'published'
}

// ==========================================
// CATALOG RESOURCE: Add Group to Catalog
// ==========================================

resource catalogResourceSecurityGroup 'accessPackageCatalogResource' = {
  catalogId: catalog.id
  originId: demoSecurityGroup.id
  originSystem: 'AadGroup'
  displayName: demoSecurityGroup.displayName
  description: 'Security group "${demoSecurityGroup.displayName}" added to catalog "${catalog.displayName}"'
}

// ==========================================
// ACCESS PACKAGE: Security Group Membership
// ==========================================

resource securityGroupAccessPackage 'accessPackage' = {
  displayName: 'Bicep Local - Security Group Membership'
  description: 'Grants membership to "${demoSecurityGroup.displayName}" security group'
  catalogId: catalogResourceSecurityGroup.catalogId
  isHidden: false
}

resource securityGroupResourceRole 'accessPackageResourceRoleScope' = {
  accessPackageId: securityGroupAccessPackage.id
  resourceOriginId: demoSecurityGroup.id
  roleOriginId: 'Member_${demoSecurityGroup.id}'
  resourceOriginSystem: 'AadGroup'
  roleDisplayName: 'Member'
}

resource securityGroupAccessPolicy 'accessPackageAssignmentPolicy' = {
  displayName: 'Policy: All Users Can Request Group Membership'
  description: 'Any member user can request membership to "${demoSecurityGroup.displayName}"'
  accessPackageId: securityGroupAccessPackage.id
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

  durationInDays: 180
  canExtend: true
}

// ==========================================
// OUTPUTS
// ==========================================

output catalogId string = catalog.id
output securityGroupId string = demoSecurityGroup.id
output catalogResourceId string = catalogResourceSecurityGroup.id
output accessPackageId string = securityGroupAccessPackage.id
output resourceRoleId string = securityGroupResourceRole.id
output policyId string = securityGroupAccessPolicy.id
