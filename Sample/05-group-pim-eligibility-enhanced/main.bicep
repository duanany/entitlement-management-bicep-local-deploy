targetScope = 'local'

extension entitlementmgmt with {
  entitlementToken: entitlementToken
  groupUserToken: groupUserToken
}

@secure()
@description('Graph API token with EntitlementManagement.ReadWrite.All (used for catalog + PIM policy operations)')
param entitlementToken string

@secure()
@description('Graph API token with Group.ReadWrite.All + User.Read.All (used for security groups + schedule requests)')
param groupUserToken string

@description('Optional prefix to keep group uniqueName values distinct per environment')
param namePrefix string = 'bicep-pim-enhanced'

@description('Seed user ID added to approver group for local testing')
param testUserId string = '11111111-1111-1111-1111-111111111111'

@description('Conditional Access authentication context ID (c1-c99) published in Entra ID for Scenario 1 (standard)')
param conditionalAccessContextId string = 'c1'

@description('Require Conditional Access authentication context on every Scenario 1 activation request')
param requireConditionalAccessContext bool = true

@description('Enable scenario that references existing groups by ID instead of uniqueName')
param enableExistingGroupScenario bool = false

@description('Existing ELIGIBLE group object ID for the by-ID scenario (required when enabled)')
param existingEligibleGroupId string = ''

@description('Existing ACTIVATED group object ID for the by-ID scenario (required when enabled)')
param existingActivatedGroupId string = ''

@description('Optional approver group object ID for the by-ID scenario (empty = no approval)')
param existingApproverGroupId string = ''

@description('Conditional Access authentication context ID for the by-ID scenario')
param existingConditionalAccessContextId string = 'c1'

@description('Require Conditional Access authentication context for the by-ID scenario')
param existingRequireConditionalAccessContext bool = true

@description('Enable scenario that enforces authentication context WITHOUT MFA (auth-context only)')
param enableAuthContextOnlyScenario bool = true

@description('Authentication context ID dedicated to the auth-context-only scenario (e.g., c2)')
param authContextOnlyId string = 'c2'

var eligibleGroupName = '${namePrefix}-eligible'
var activatedGroupName = '${namePrefix}-activated'
var approverGroupName = '${namePrefix}-approvers'

var caOnlyEligibleGroupName = '${namePrefix}-caonly-eligible'
var caOnlyActivatedGroupName = '${namePrefix}-caonly-activated'
var caOnlyApproverGroupName = '${namePrefix}-caonly-approvers'

// ==========================================
// SECURITY GROUPS - Scenario 1 (standard)
// ==========================================

resource pimEligibleGroup 'securityGroup' = {
  uniqueName: eligibleGroupName
  displayName: 'Bicep Local - Enhanced PIM Eligible'
  description: 'Members can activate JIT access through enhanced handler'
  members: []
}

resource pimActivatedGroup 'securityGroup' = {
  uniqueName: activatedGroupName
  displayName: 'Bicep Local - Enhanced PIM Activated'
  description: 'Temporary group granted by PIM activation; assign RBAC manually'
  members: []
}

resource pimApproverGroup 'securityGroup' = {
  uniqueName: approverGroupName
  displayName: 'Bicep Local - Enhanced PIM Approvers'
  description: 'Members approve activations through entitlement workflow'
  members: [
    testUserId
  ]
}

// ==========================================
// SECURITY GROUPS - Scenario 3 (auth-context only)
// ==========================================

resource pimEligibleGroupCaOnly 'securityGroup' = if (enableAuthContextOnlyScenario) {
  uniqueName: caOnlyEligibleGroupName
  displayName: 'Bicep Local - CA-Only PIM Eligible'
  description: 'Eligibility used for auth-context-only scenario'
  members: []
}

resource pimActivatedGroupCaOnly 'securityGroup' = if (enableAuthContextOnlyScenario) {
  uniqueName: caOnlyActivatedGroupName
  displayName: 'Bicep Local - CA-Only PIM Activated'
  description: 'Temporary membership requiring Conditional Access authentication context'
  members: []
}

resource pimApproverGroupCaOnly 'securityGroup' = if (enableAuthContextOnlyScenario) {
  uniqueName: caOnlyApproverGroupName
  displayName: 'Bicep Local - CA-Only PIM Approvers'
  description: 'Approvers for auth-context-only scenario'
  members: [
    testUserId
  ]
}

// ==========================================
// SCENARIO 1: UniqueName pattern (MFA + CA)
// ==========================================

resource pimEligibilityScenario1 'groupPimEligibilityEnhanced' = {
  eligibleGroupUniqueName: pimEligibleGroup.uniqueName
  activatedGroupUniqueName: pimActivatedGroup.uniqueName
  accessId: 'member'
  justification: 'Scenario 1 - Enhanced handler sample (uniqueName)'
  schedule: {
    expirationType: 'AfterDuration'
    expirationDuration: 'P180D'
  }
  eligibilityExpiration: {
    requireExpiration: true
    maximumDuration: 'P365D'
  }
  activationPolicy: {
    maxActivationDuration: 'PT2H'
    requireJustification: true
    requireMfa: true
    requireTicket: false
    requireConditionalAccessContext: requireConditionalAccessContext
    conditionalAccessContextId: conditionalAccessContextId
  }
  approvalPolicy: {
    isApprovalRequired: true
    isApprovalRequiredForExtension: false
    isRequestorJustificationRequired: true
    approvalMode: 'SingleStage'
    stages: [
      {
        approvalStageTimeoutInDays: 3
        isApproverJustificationRequired: true
        escalationTimeInMinutes: 0
        isEscalationEnabled: false
        primaryApprovers: [
          {
            type: 'group'
            groupId: pimApproverGroup.id
          }
        ]
      }
    ]
  }
  notificationPolicy: {
    notifyAdminsOnEligibility: true
    notifyRequestorsOnEligibility: true
    notifyApproversOnEligibility: true
    notifyAdminsOnActivation: true
    notifyRequestorsOnActivation: true
    notifyApproversOnActivation: true
  }
  adminAssignmentPolicy: {
    requireJustification: true
    requireMfa: true
    requireTicket: false
  }
}

// ==========================================
// SCENARIO 2: Existing group IDs (optional)
// ==========================================

resource pimEligibilityExistingGroups 'groupPimEligibilityEnhanced' = if (enableExistingGroupScenario) {
  eligibleGroupId: existingEligibleGroupId
  activatedGroupId: existingActivatedGroupId
  accessId: 'member'
  justification: 'Scenario 2 - Existing group IDs (by ID pattern)'
  schedule: {
    expirationType: 'AfterDuration'
    expirationDuration: 'P180D'
  }
  eligibilityExpiration: {
    requireExpiration: true
    maximumDuration: 'P365D'
  }
  activationPolicy: {
    maxActivationDuration: 'PT4H'
    requireJustification: true
    requireMfa: true
    requireTicket: false
    requireConditionalAccessContext: existingRequireConditionalAccessContext
    conditionalAccessContextId: existingConditionalAccessContextId
  }
  approvalPolicy: {
    isApprovalRequired: !empty(existingApproverGroupId)
    isApprovalRequiredForExtension: false
    isRequestorJustificationRequired: true
    approvalMode: 'SingleStage'
    stages: !empty(existingApproverGroupId) ? [
      {
        approvalStageTimeoutInDays: 7
        isApproverJustificationRequired: true
        escalationTimeInMinutes: 0
        isEscalationEnabled: false
        primaryApprovers: [
          {
            type: 'group'
            groupId: existingApproverGroupId
          }
        ]
      }
    ] : []
  }
  notificationPolicy: {
    notifyAdminsOnEligibility: true
    notifyRequestorsOnEligibility: true
    notifyApproversOnEligibility: true
    notifyAdminsOnActivation: true
    notifyRequestorsOnActivation: true
    notifyApproversOnActivation: true
  }
  adminAssignmentPolicy: {
    requireJustification: true
    requireMfa: false
    requireTicket: false
  }
}

// ==========================================
// SCENARIO 3: Auth-context only (no MFA)
// ==========================================

resource pimEligibilityAuthContextOnly 'groupPimEligibilityEnhanced' = if (enableAuthContextOnlyScenario) {
  eligibleGroupUniqueName: pimEligibleGroupCaOnly!.uniqueName
  activatedGroupUniqueName: pimActivatedGroupCaOnly!.uniqueName
  accessId: 'member'
  justification: 'Scenario 3 - Authentication context only (no MFA)'
  schedule: {
    expirationType: 'AfterDuration'
    expirationDuration: 'P90D'
  }
  eligibilityExpiration: {
    requireExpiration: true
    maximumDuration: 'P180D'
  }
  activationPolicy: {
    maxActivationDuration: 'PT1H'
    requireJustification: false
    requireMfa: false
    requireTicket: false
    requireConditionalAccessContext: true
    conditionalAccessContextId: authContextOnlyId
  }
  approvalPolicy: {
    isApprovalRequired: false
    isApprovalRequiredForExtension: false
    isRequestorJustificationRequired: false
    approvalMode: 'SingleStage'
    stages: []
  }
  notificationPolicy: {
    notifyAdminsOnEligibility: true
    notifyRequestorsOnEligibility: true
    notifyApproversOnEligibility: false
    notifyAdminsOnActivation: true
    notifyRequestorsOnActivation: true
    notifyApproversOnActivation: false
  }
  adminAssignmentPolicy: {
    requireJustification: false
    requireMfa: false
    requireTicket: false
  }
}

// ==========================================
// OUTPUTS
// ==========================================

// Scenario 1 outputs
@description('Scenario 1 - Eligible group object ID')
output scenario1EligibleGroupId string = pimEligibleGroup.id

@description('Scenario 1 - Activated group object ID (assign RBAC manually)')
output scenario1ActivatedGroupId string = pimActivatedGroup.id

@description('Scenario 1 - Approver group object ID')
output scenario1ApproverGroupId string = pimApproverGroup.id

@description('Scenario 1 - Eligibility request ID (Graph)')
output scenario1EligibilityRequestId string = pimEligibilityScenario1.?pimEligibilityRequestId ?? 'not-available'

@description('Scenario 1 - Eligibility schedule ID (Graph)')
output scenario1EligibilityScheduleId string = pimEligibilityScenario1.?pimEligibilityScheduleId ?? 'not-available'

// Scenario 2 outputs (conditional)
@description('Scenario 2 - Eligible group ID (echo input when enabled)')
output scenario2EligibleGroupId string = enableExistingGroupScenario ? existingEligibleGroupId : 'scenario-disabled'

@description('Scenario 2 - Activated group ID (echo input when enabled)')
output scenario2ActivatedGroupId string = enableExistingGroupScenario ? existingActivatedGroupId : 'scenario-disabled'

@description('Scenario 2 - Eligibility request ID (Graph)')
output scenario2EligibilityRequestId string = enableExistingGroupScenario ? (pimEligibilityExistingGroups.?pimEligibilityRequestId ?? 'not-available') : 'scenario-disabled'

@description('Scenario 2 - Eligibility schedule ID (Graph)')
output scenario2EligibilityScheduleId string = enableExistingGroupScenario ? (pimEligibilityExistingGroups.?pimEligibilityScheduleId ?? 'not-available') : 'scenario-disabled'

// Scenario 3 outputs (conditional)
@description('Scenario 3 - Eligible group object ID')
output scenario3EligibleGroupId string = enableAuthContextOnlyScenario ? pimEligibleGroupCaOnly!.id : 'scenario-disabled'

@description('Scenario 3 - Activated group object ID')
output scenario3ActivatedGroupId string = enableAuthContextOnlyScenario ? pimActivatedGroupCaOnly!.id : 'scenario-disabled'

@description('Scenario 3 - Approver group object ID')
output scenario3ApproverGroupId string = enableAuthContextOnlyScenario ? pimApproverGroupCaOnly!.id : 'scenario-disabled'

@description('Scenario 3 - Eligibility request ID (Graph)')
output scenario3EligibilityRequestId string = enableAuthContextOnlyScenario ? (pimEligibilityAuthContextOnly.?pimEligibilityRequestId ?? 'not-available') : 'scenario-disabled'

@description('Scenario 3 - Eligibility schedule ID (Graph)')
output scenario3EligibilityScheduleId string = enableAuthContextOnlyScenario ? (pimEligibilityAuthContextOnly.?pimEligibilityScheduleId ?? 'not-available') : 'scenario-disabled'
