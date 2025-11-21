targetScope = 'local'

@secure()
@description('Entitlement Management API token (Graph API token with EntitlementManagement.ReadWrite.All permission)')
param entitlementToken string

@description('User ID for specific user approver')
param testUserId string = '7a72c098-a42d-489f-a3fa-c2445dec6f9c'

@description('Group ID for group-based approval')
param testGroupId string = '0afd1da6-51fb-450f-bf1a-069a85dcacad'

// ==========================================
// CATALOG
// ==========================================

module catalog '../../avm/res/graph/identity-governance/entitlement-management/catalogs/main.bicep' = {
  params: {
    entitlementToken: entitlementToken
    name: 'Bicep Local - Approval Workflows Catalog'
    catalogDescription: 'Demonstrates different approval patterns: manager, user, group, multi-stage'
    isExternallyVisible: false
    catalogType: 'UserManaged'
    state: 'Published'
  }
}

// ==========================================
// PACKAGE 1: Manager Approval
// ==========================================

module managerApprovalPackage '../../avm/res/graph/identity-governance/entitlement-management/access-package/main.bicep' = {
    params: {
    entitlementToken: entitlementToken
    name: 'Bicep Local - Manager Approval Required'
    catalogName: catalog.name
    accessPackageDescription: 'Requires direct manager approval'
    isHidden: false
  }
  dependsOn: [
    catalog
  ]
}

module managerApprovalPolicy '../../avm/res/graph/identity-governance/entitlement-management/assignment-policies/main.bicep' = {
  params: {
    entitlementToken: entitlementToken
    name: 'Policy: Manager Must Approve'
    accessPackageName: managerApprovalPackage.name
    catalogName: catalog.name
    policyDescription: 'Any user can request - direct manager approves'
    allowedTargetScope: 'AllMemberUsers'
    requestorSettings: {
      scopeType: 'AllExistingDirectoryMemberUsers'
      acceptRequests: true
    }
    requestApprovalSettings: {
      isApprovalRequired: true
      isApprovalRequiredForExtension: false
      isRequestorJustificationRequired: true
      approvalMode: 'SingleStage'
      approvalStages: [
        {
          approvalStageTimeOutInDays: 14
          isApproverJustificationRequired: true
          isEscalationEnabled: false
          primaryApprovers: [
            {
              oDataType: '#microsoft.graph.requestorManager'
              managerLevel: 1 // Direct manager
            }
          ]
        }
      ]
    }
    durationInDays: 90
    canExtend: true
  }
  dependsOn: [
    managerApprovalPackage
  ]
}

// ==========================================
// PACKAGE 2: Specific User Approver
// ==========================================

module userApproverPackage '../../avm/res/graph/identity-governance/entitlement-management/access-package/main.bicep' = {
  params: {
    entitlementToken: entitlementToken
    name: 'Bicep Local - Specific User Must Approve'
    catalogName: catalog.name
    accessPackageDescription: 'All users can request - specific user approves'
    isHidden: false
  }
  dependsOn: [
    catalog
  ]
}

module userApproverPolicy '../../avm/res/graph/identity-governance/entitlement-management/assignment-policies/main.bicep' = {
  params: {
    entitlementToken: entitlementToken
    name: 'Policy: Specific User Approves All'
    accessPackageName: userApproverPackage.name
    catalogName: catalog.name
    policyDescription: 'Any user can request - specific user approves'
    allowedTargetScope: 'AllMemberUsers'
    requestorSettings: {
      scopeType: 'AllExistingDirectoryMemberUsers'
      acceptRequests: true
    }
    requestApprovalSettings: {
      isApprovalRequired: true
      isApprovalRequiredForExtension: false
      isRequestorJustificationRequired: true
      approvalMode: 'SingleStage'
      approvalStages: [
        {
          approvalStageTimeOutInDays: 7
          isApproverJustificationRequired: false
          isEscalationEnabled: false
          primaryApprovers: [
            {
              oDataType: '#microsoft.graph.singleUser'
              userId: testUserId
              description: 'Specific user as approver'
            }
          ]
        }
      ]
    }
    durationInDays: 60
    canExtend: false
  }
}

// ==========================================
// PACKAGE 3: Group-Based Approval
// ==========================================

module groupAccessPackage '../../avm/res/graph/identity-governance/entitlement-management/access-package/main.bicep' = {
  params: {
    entitlementToken: entitlementToken
    name: 'Bicep Local - Group Peer Approval'
    catalogName: catalog.name
    accessPackageDescription: 'Group members can request - other group members approve'
    isHidden: false
  }
}

module groupAccessPolicy '../../avm/res/graph/identity-governance/entitlement-management/assignment-policies/main.bicep' = {
  params: {
    entitlementToken: entitlementToken
    name: 'Policy: Group Members Approve Peers'
    accessPackageName: groupAccessPackage.name
    catalogName: catalog.name
    policyDescription: 'Group members request - peers approve + quarterly reviews'
    allowedTargetScope: 'SpecificDirectoryUsers'
    requestorSettings: {
      scopeType: 'SpecificDirectorySubjects'
      acceptRequests: true
      allowedRequestors: [
        {
          oDataType: '#microsoft.graph.groupMembers'
          groupId: testGroupId
          description: 'Group members can request'
        }
      ]
    }
    requestApprovalSettings: {
      isApprovalRequired: true
      isApprovalRequiredForExtension: true
      isRequestorJustificationRequired: true
      approvalMode: 'SingleStage'
      approvalStages: [
        {
          approvalStageTimeOutInDays: 14
          isApproverJustificationRequired: true
          isEscalationEnabled: false
          primaryApprovers: [
            {
              oDataType: '#microsoft.graph.groupMembers'
              groupId: testGroupId
              description: 'Group members as approvers'
            }
          ]
        }
      ]
    }
    reviewSettings: {
      isEnabled: true
      recurrenceType: 'quarterly'
      reviewerType: 'Reviewers'
      startDateTime: '2025-12-01T00:00:00Z'
      durationInDays: 14
      reviewers: [
        {
          oDataType: '#microsoft.graph.groupMembers'
          groupId: testGroupId
          description: 'Group members as reviewers'
        }
      ]
    }
    durationInDays: 90
    canExtend: true
  }
  dependsOn: [
    groupAccessPackage
  ]
}

// ==========================================
// PACKAGE 4: Two-Stage Approval
// ==========================================

module twoStagePackage '../../avm/res/graph/identity-governance/entitlement-management/access-package/main.bicep' = {
  params: {
    entitlementToken: entitlementToken
    name: 'Bicep Local - Two-Stage Approval'
    catalogName: catalog.name
    accessPackageDescription: 'User approves first, then group members approve'
    isHidden: false
  }
  dependsOn: [
    catalog
  ]
}

module twoStagePolicy '../../avm/res/graph/identity-governance/entitlement-management/assignment-policies/main.bicep' = {
  params: {
    entitlementToken: entitlementToken
    name: 'Policy: Two-Stage (User → Group)'
    accessPackageName: twoStagePackage.name
    catalogName: catalog.name
    policyDescription: 'Stage 1: User approves, Stage 2: Group approves'
    allowedTargetScope: 'AllMemberUsers'
    requestorSettings: {
      scopeType: 'AllExistingDirectoryMemberUsers'
      acceptRequests: true
    }
    requestApprovalSettings: {
      isApprovalRequired: true
      isApprovalRequiredForExtension: false
      isRequestorJustificationRequired: true
      approvalMode: 'Serial' // Sequential approval stages
      approvalStages: [
        {
          approvalStageTimeOutInDays: 5
          isApproverJustificationRequired: false
          isEscalationEnabled: false
          primaryApprovers: [
            {
              oDataType: '#microsoft.graph.singleUser'
              userId: testUserId
              description: 'Stage 1: User approval'
            }
          ]
        }
        {
          approvalStageTimeOutInDays: 7
          isApproverJustificationRequired: true
          isEscalationEnabled: false
          primaryApprovers: [
            {
              oDataType: '#microsoft.graph.groupMembers'
              groupId: testGroupId
              description: 'Stage 2: Group approval'
            }
          ]
        }
      ]
    }
    durationInDays: 60
    canExtend: false
  }
}

// ==========================================
// OUTPUTS
// ==========================================

output catalogId string = catalog.outputs.resourceId

// Package IDs
output managerApprovalPackageId string = managerApprovalPackage.outputs.resourceId
output userApproverPackageId string = userApproverPackage.outputs.resourceId
output groupAccessPackageId string = groupAccessPackage.outputs.resourceId
output twoStagePackageId string = twoStagePackage.outputs.resourceId

// Policy IDs
output managerApprovalPolicyId string = managerApprovalPolicy.outputs.resourceId
output userApproverPolicyId string = userApproverPolicy.outputs.resourceId
output groupAccessPolicyId string = groupAccessPolicy.outputs.resourceId
output twostagePolicyId string = twoStagePolicy.outputs.resourceId
