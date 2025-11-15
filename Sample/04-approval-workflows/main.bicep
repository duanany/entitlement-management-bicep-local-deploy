targetScope = 'local'

extension entitlementmgmt with {
  entitlementToken: entitlementToken
}

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

resource catalog 'accessPackageCatalog' = {
  displayName: 'Bicep Local - Approval Workflows Catalog'
  description: 'Demonstrates different approval patterns: manager, user, group, multi-stage'
  isExternallyVisible: false
  catalogType: 'userManaged'
  state: 'published'
}

// ==========================================
// PACKAGE 1: Manager Approval
// ==========================================

resource managerApprovalPackage 'accessPackage' = {
  displayName: 'Bicep Local - Manager Approval Required'
  description: 'Requires direct manager approval'
  catalogId: catalog.id
  isHidden: false
}

resource managerApprovalPolicy 'accessPackageAssignmentPolicy' = {
  displayName: 'Policy: Manager Must Approve'
  description: 'Any user can request - direct manager approves'
  accessPackageId: managerApprovalPackage.id
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

// ==========================================
// PACKAGE 2: Specific User Approver
// ==========================================

resource userApproverPackage 'accessPackage' = {
  displayName: 'Bicep Local - Specific User Must Approve'
  description: 'All users can request - specific user approves'
  catalogId: catalog.id
  isHidden: false
}

resource userApproverPolicy 'accessPackageAssignmentPolicy' = {
  displayName: 'Policy: Specific User Approves All'
  description: 'Any user can request - specific user approves'
  accessPackageId: userApproverPackage.id
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

// ==========================================
// PACKAGE 3: Group-Based Approval
// ==========================================

resource groupAccessPackage 'accessPackage' = {
  displayName: 'Bicep Local - Group Peer Approval'
  description: 'Group members can request - other group members approve'
  catalogId: catalog.id
  isHidden: false
}

resource groupAccessPolicy 'accessPackageAssignmentPolicy' = {
  displayName: 'Policy: Group Members Approve Peers'
  description: 'Group members request - peers approve + quarterly reviews'
  accessPackageId: groupAccessPackage.id
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

// ==========================================
// PACKAGE 4: Two-Stage Approval
// ==========================================

resource twoStagePackage 'accessPackage' = {
  displayName: 'Bicep Local - Two-Stage Approval'
  description: 'User approves first, then group members approve'
  catalogId: catalog.id
  isHidden: false
}

resource twoStagePolicy 'accessPackageAssignmentPolicy' = {
  displayName: 'Policy: Two-Stage (User → Group)'
  description: 'Stage 1: User approves, Stage 2: Group approves'
  accessPackageId: twoStagePackage.id
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

// ==========================================
// OUTPUTS
// ==========================================

output catalogId string = catalog.id

// Package IDs
output managerApprovalPackageId string = managerApprovalPackage.id
output userApproverPackageId string = userApproverPackage.id
output groupAccessPackageId string = groupAccessPackage.id
output twoStagePackageId string = twoStagePackage.id

// Policy IDs
output managerApprovalPolicyId string = managerApprovalPolicy.id
output userApproverPolicyId string = userApproverPolicy.id
output groupAccessPolicyId string = groupAccessPolicy.id
output twostagePolicyId string = twoStagePolicy.id
