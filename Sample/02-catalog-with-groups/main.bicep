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
// CATALOG WITH RESOURCES
// ==========================================

module catalog '../../avm/res/graph/identity-governance/entitlement-management/catalogs/main.bicep' = {
  params: {
    entitlementToken: entitlementToken
    name: 'Bicep Local - Security Group Catalog'
    catalogDescription: 'Catalog for managing security group membership via access packages'
    isExternallyVisible: false
    catalogType: 'UserManaged'
    state: 'Published'
    resources: [
      {
        originId: demoSecurityGroup.id
        originSystem: 'AadGroup'
        displayName: demoSecurityGroup.displayName
        description: 'Security group "${demoSecurityGroup.displayName}" added to catalog'
      }
    ]
  }
}

// ==========================================
// ACCESS PACKAGE: Security Group Membership
// ==========================================

module securityGroupAccessPackage '../../avm/res/graph/identity-governance/entitlement-management/access-package/main.bicep' = {
  params: {
    entitlementToken: entitlementToken
    name: 'Bicep Local - Security Group Membership'
    catalogName: catalog.name
    accessPackageDescription: 'Grants membership to "${demoSecurityGroup.displayName}" security group'
    isHidden: false
    resourceRoleScopes: [
      {
        resourceOriginId: demoSecurityGroup.id
        roleOriginId: 'Member_${demoSecurityGroup.id}'
        resourceOriginSystem: 'AadGroup'
        roleDisplayName: 'Member'
      }
    ]
  }
}

module securityGroupAccessPolicy '../../avm/res/graph/identity-governance/entitlement-management/assignment-policies/main.bicep' = {
  params: {
    entitlementToken: entitlementToken
    name: 'Policy: All Users Can Request Group Membership'
    accessPackageName: securityGroupAccessPackage.name
    catalogName: catalog.name
    policyDescription: 'Any member user can request membership to "${demoSecurityGroup.displayName}"'
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
}

// ==========================================
// OUTPUTS
// ==========================================

output catalogId string = catalog.outputs.resourceId
output securityGroupId string = demoSecurityGroup.id
output catalogResourceId string = catalog.outputs.resources[0].resourceId
output accessPackageId string = securityGroupAccessPackage.outputs.resourceId
output resourceRoleId string = securityGroupAccessPackage.outputs.resourceRoleScopes[0].resourceId
output policyId string = securityGroupAccessPolicy.outputs.resourceId
