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
// SECURITY GROUPS: Access Package Workflow
// ==========================================

// REQUESTOR GROUP: Users who can REQUEST access to the access package
resource requestorGroup 'securityGroup' = {
  uniqueName: 'bicep-pim-requestors'
  displayName: 'Bicep Local - PIM Access Requestors'
  description: 'Users who can request access to the PIM access package'

  members: [
    testUserId
  ]
}

// APPROVER GROUP: Users who approve access package requests
resource approverGroup 'securityGroup' = {
  uniqueName: 'bicep-pim-approvers'
  displayName: 'Bicep Local - PIM Access Approvers'
  description: 'Users who approve access package requests'

  members: [
    testUserId // In production, this would be different users
  ]
}

// REVIEWER GROUP: Users who review access package assignments
resource reviewerGroup 'securityGroup' = {
  uniqueName: 'bicep-pim-reviewers'
  displayName: 'Bicep Local - PIM Access Reviewers'
  description: 'Users who review ongoing access package assignments'

  members: [
    testUserId
  ]
}

// ==========================================
// PIM SECURITY GROUPS
// ==========================================

// ELIGIBLE GROUP: Granted by access package - users who CAN activate PIM for Azure resource access
// This group is added to the catalog as a resource and assigned via the access package
resource pimEligibleGroup 'securityGroup' = {
  uniqueName: 'bicep-pim-eligible-developers'
  displayName: 'Bicep Local - PIM Eligible Developers'
  description: 'Eligible for JIT activation to activated group - granted via access package assignment'

  members: [] // Access package controls membership - leave empty
}

// ACTIVATED GROUP: Temporary membership granted after PIM activation
// This group has RBAC assignments on Azure resources (not managed in this template)
// When eligible users activate PIM, they temporarily join this group and gain Azure resource access
resource pimActivatedGroup 'securityGroup' = {
  uniqueName: 'bicep-pim-activated-developers'
  displayName: 'Bicep Local - PIM Activated Developers'
  description: 'Temporary membership granted via PIM activation - has RBAC on Azure resources'

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
// CATALOG RESOURCE: Add Eligible Group to Catalog
// ==========================================
// The ELIGIBLE group (not activated) is added to the catalog because:
// - Access package assigns membership to the ELIGIBLE group
// - ACTIVATED group has RBAC on Azure resources (managed outside this template)
// - Flow: Request access → Get eligible membership → Activate PIM → Get activated membership → Access Azure resources

resource catalogResourcePimEligible 'accessPackageCatalogResource' = {
  catalogId: pimCatalog.id
  originId: pimEligibleGroup.id
  originSystem: 'AadGroup'
  displayName: pimEligibleGroup.displayName
  description: 'PIM eligible group added to catalog - granted by access package assignment'
}

// ==========================================
// ACCESS PACKAGE: PIM JIT Activation
// ==========================================

// ==========================================
// ACCESS PACKAGE: PIM JIT Activation
// ==========================================
// This access package grants membership to the ELIGIBLE group
// Once users have eligible membership, they can activate PIM to join the ACTIVATED group

resource pimAccessPackage 'accessPackage' = {
  displayName: 'Bicep Local - PIM JIT Developer Activation'
  description: 'Grants eligible group membership - enables PIM activation for Azure resource access'
  catalogId: catalogResourcePimEligible.catalogId
  isHidden: false
}

// ==========================================
// RESOURCE ROLE SCOPE: Assign Eligible Group Member Role
// ==========================================
// Access package assigns "Member" role of the ELIGIBLE group
// Flow: Access package grants → Eligible group membership → User can activate PIM → Activated group membership → Azure RBAC

resource pimResourceRole 'accessPackageResourceRoleScope' = {
  accessPackageId: pimAccessPackage.id
  resourceOriginId: pimEligibleGroup.id
  roleOriginId: 'Member_${pimEligibleGroup.id}'
  resourceOriginSystem: 'AadGroup'
  roleDisplayName: 'Member'
}

// ==========================================
// ASSIGNMENT POLICY: Requestor → Approver Workflow
// ==========================================
// Requestors request access → Approvers approve → User gets eligible group membership

resource pimAccessPolicy 'accessPackageAssignmentPolicy' = {
  displayName: 'Policy: Requestor Access with Approver Workflow'
  description: 'Requestors request access, approvers approve, eligible membership granted'
  accessPackageId: pimAccessPackage.id
  allowedTargetScope: 'SpecificDirectoryUsers'

  requestorSettings: {
    scopeType: 'SpecificDirectorySubjects'
    acceptRequests: true
    allowedRequestors: [
      {
        oDataType: '#microsoft.graph.groupMembers'
        groupId: requestorGroup.id
        description: 'PIM requestors - can request access to eligible group'
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
        approvalStageTimeOutInDays: 14
        isApproverJustificationRequired: false
        isEscalationEnabled: false
        primaryApprovers: [
          {
            oDataType: '#microsoft.graph.groupMembers'
            groupId: approverGroup.id
            description: 'PIM approvers - approve access package requests'
          }
        ]
      }
    ]
  }

  durationInDays: 180 // 6 months eligible group membership
  canExtend: true
}

// ==========================================
// GROUP PIM ELIGIBILITY: Link Eligible → Activated
// ==========================================

resource pimEligibility 'groupPimEligibility' = {
  // Option 1: Reference by uniqueName (original approach)
  // eligibleGroupUniqueName: pimEligibleGroup.uniqueName
  // activatedGroupUniqueName: pimActivatedGroup.uniqueName

  // Option 2: Reference by ID (supports cross-deployment scenarios)
  eligibleGroupId: pimEligibleGroup.id
  activatedGroupId: pimActivatedGroup.id

  accessId: 'member'
  justification: 'JIT developer access - eligible members can activate for 2 hours'
  expirationDateTime: '2026-05-15T00:00:00Z' // 6 months eligibility

  policyTemplateJson: loadTextContent('../pim-policy-template.json')
  maxActivationDuration: 'PT2H' // 2 hours max per activation
}

// ==========================================
// OUTPUTS
// ==========================================

// Access Package Workflow Groups
output requestorGroupId string = requestorGroup.id
output approverGroupId string = approverGroup.id
output reviewerGroupId string = reviewerGroup.id

// PIM Groups
output pimEligibleGroupId string = pimEligibleGroup.id
@description('Activated group ID - assign this group RBAC on Azure resources (e.g., Contributor on resource group)')
output pimActivatedGroupId string = pimActivatedGroup.id

// Catalog and Access Package
output pimCatalogId string = pimCatalog.id
output catalogResourceId string = catalogResourcePimEligible.id
output pimAccessPackageId string = pimAccessPackage.id
output pimResourceRoleId string = pimResourceRole.id
output pimAccessPolicyId string = pimAccessPolicy.id
output pimEligibilityId string = pimEligibility.pimEligibilityRequestId
