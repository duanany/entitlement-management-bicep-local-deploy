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

@description('User ID for eligible member')
param testUserId string = '7a72c098-a42d-489f-a3fa-c2445dec6f9c'

// ==========================================
// PIM SECURITY GROUPS
// ==========================================

// ELIGIBLE GROUP: Users who CAN request PIM activation
resource pimEligibleGroup 'securityGroup' = {
  uniqueName: 'bicep-pim-eligible-developers'
  displayName: 'Bicep Local - PIM Eligible Developers'
  description: 'Eligible developers who can request JIT activation'

  members: [
    testUserId
  ]
}

// ACTIVATED GROUP: Temporary membership granted after PIM activation
resource pimActivatedGroup 'securityGroup' = {
  uniqueName: 'bicep-pim-activated-developers'
  displayName: 'Bicep Local - PIM Activated Developers'
  description: 'Temporary membership granted via PIM activation'

  members: [] // PIM controls membership - leave empty
}

// ==========================================
// PIM CATALOG
// ==========================================

resource pimCatalog 'accessPackageCatalog' = {
  displayName: 'Bicep Local - PIM JIT Access Catalog'
  description: 'Dedicated catalog for PIM eligibility assignments - manages JIT activation'
  isExternallyVisible: false
  catalogType: 'userManaged'
  state: 'published'
}

// ==========================================
// CATALOG RESOURCE: Add Activated Group
// ==========================================

resource catalogResourcePimActivated 'accessPackageCatalogResource' = {
  catalogId: pimCatalog.id
  originId: pimActivatedGroup.id
  originSystem: 'AadGroup'
  displayName: pimActivatedGroup.displayName
  description: 'PIM activated group added to catalog - grants temporary membership via JIT activation'
}

// ==========================================
// ACCESS PACKAGE: PIM JIT Activation
// ==========================================

resource pimAccessPackage 'accessPackage' = {
  displayName: 'Bicep Local - PIM JIT Developer Activation'
  description: 'Grants time-limited membership to activated group'
  catalogId: catalogResourcePimActivated.catalogId
  isHidden: false
}

resource pimResourceRole 'accessPackageResourceRoleScope' = {
  accessPackageId: pimAccessPackage.id
  resourceOriginId: pimActivatedGroup.id
  roleOriginId: 'Member_${pimActivatedGroup.id}'
  resourceOriginSystem: 'AadGroup'
  roleDisplayName: 'Member'
}

resource pimAccessPolicy 'accessPackageAssignmentPolicy' = {
  displayName: 'Policy: PIM JIT Activation (Eligible Members)'
  description: 'Eligible members can request time-limited activation - peer approval required'
  accessPackageId: pimAccessPackage.id
  allowedTargetScope: 'SpecificDirectoryUsers'

  requestorSettings: {
    scopeType: 'SpecificDirectorySubjects'
    acceptRequests: true
    allowedRequestors: [
      {
        oDataType: '#microsoft.graph.groupMembers'
        groupId: pimEligibleGroup.id
        description: 'PIM eligible developers - can request JIT activation'
      }
    ]
  }

  requestApprovalSettings: {
    isApprovalRequired: true
    isApprovalRequiredForExtension: false
    isRequestorJustificationRequired: true
    approvalMode: 'SingleStage'
    approvalStages: [
      {
        approvalStageTimeOutInDays: 1 // Fast approval for JIT access
        isApproverJustificationRequired: false
        isEscalationEnabled: false
        primaryApprovers: [
          {
            oDataType: '#microsoft.graph.groupMembers'
            groupId: pimEligibleGroup.id
            description: 'Peer approval - eligible members approve each other'
          }
        ]
      }
    ]
  }

  durationInDays: 1 // 1 day max assignment (individual activations are shorter)
  canExtend: false
}

// ==========================================
// GROUP PIM ELIGIBILITY: Link Eligible → Activated
// ==========================================

resource pimEligibility 'groupPimEligibility' = {
  eligibleGroupUniqueName: pimEligibleGroup.uniqueName
  activatedGroupUniqueName: pimActivatedGroup.uniqueName

  accessId: 'member'
  justification: 'JIT developer access - eligible members can activate for 2 hours'
  expirationDateTime: '2026-05-15T00:00:00Z' // 6 months eligibility

  policyTemplateJson: loadTextContent('../pim-policy-template.json')
  maxActivationDuration: 'PT2H' // 2 hours max per activation
}

// ==========================================
// OUTPUTS
// ==========================================

output pimCatalogId string = pimCatalog.id
output pimEligibleGroupId string = pimEligibleGroup.id
output pimActivatedGroupId string = pimActivatedGroup.id
output catalogResourceId string = catalogResourcePimActivated.id
output pimAccessPackageId string = pimAccessPackage.id
output pimResourceRoleId string = pimResourceRole.id
output pimAccessPolicyId string = pimAccessPolicy.id
output pimEligibilityId string = pimEligibility.pimEligibilityRequestId
