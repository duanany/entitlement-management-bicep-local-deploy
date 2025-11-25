# Group PIM Eligibility Enhanced - Standalone Handler Test

**🔥 NEW!** The `groupPimEligibilityEnhanced` resource provides **fine-grained control** over PIM policies directly in Bicep — no JSON template files, no catalog setup!

This sample demonstrates the **enhanced handler** for PIM eligibility configuration across three scenarios that can be toggled independently.

- **Eligibility Schedule**: Duration-based or date-based expiration
- **Activation Policy**: MFA, justification, ticketing requirements
- **Conditional Access**: Enforce authentication context (AuthenticationContext_EndUser_Assignment)
- **Approval Workflow**: Single or multi-stage approval with group/user approvers
- **Notification Settings**: Admin, requestor, approver notifications
- **Admin Assignment Policy**: Controls for programmatic eligibility assignments

## Scenario Overview

| Scenario | Description | How to enable |
|----------|-------------|---------------|
| **Scenario 1 – UniqueName (default)** | Deploys fresh eligible/activated/approver groups and configures PIM with MFA **and** authentication context enforcement. | Enabled by default; controlled through `namePrefix`, `requireConditionalAccessContext`, and `conditionalAccessContextId` parameters. |
| **Scenario 2 – Existing group IDs** | Reuses pre-created groups (e.g., production IDs) and optionally skips approval/TLS requirements. | Set `enableExistingGroupScenario` to `true` and provide `existing*Id` values and CA settings. |
| **Scenario 3 – Authentication context only** | Deploys a second set of groups that require Conditional Access authentication context **without MFA**, ideal for contractor or contextual sessions. | Controlled via `enableAuthContextOnlyScenario` (defaults to `true`) and `authContextOnlyId`. |

> Each scenario deploys its own `groupPimEligibilityEnhanced` resource. You can run all of them together (default) or disable scenarios by flipping the corresponding flags. The JSON reference policy in `sample/pim-policy-contractor-strict.json` mirrors the strict CA-only settings from Scenario 3 if you need to troubleshoot Graph responses.

## Complete Flow: Eligible Group → PIM Link → Activation → Azure Access

```text
┌─────────────────────────────────────────────────────────────────────────┐
│                    ENHANCED PIM ELIGIBILITY FLOW                        │
└─────────────────────────────────────────────────────────────────────────┘

   ┌───────────────────────┐
   │   Bicep Deployment    │
   │   (this template)     │
   └───────────┬───────────┘
               │
               │ 1. Creates security groups + configures PIM policies
               ▼
   ┌───────────────────────┐
   │   ELIGIBLE GROUP      │  ← Users added here CAN activate PIM
   │   bicep-pim-enhanced  │
   │   -eligible           │
   │   └─ (empty)          │
   └───────────┬───────────┘
               │
               │ groupPimEligibilityEnhanced resource
               │ ├── schedule (180 days eligibility)
               │ ├── activationPolicy (2hr max, MFA + justification)
               │ ├── approvalPolicy (single-stage, group approvers)
               │ ├── notificationPolicy (all notifications enabled)
               │ └── adminAssignmentPolicy (MFA + justification)
               │
               ▼
   ┌───────────────────────┐
   │   PIM ELIGIBILITY     │  ← Graph eligibilityScheduleRequest created
   │   LINK ESTABLISHED    │
   └───────────┬───────────┘
               │
               │ 2. Admin adds User A to Eligible Group (manually or via access package)
               ▼

┌─────────────────────────────────────────────────────────────────────────┐
│                    USER ACTIVATION WORKFLOW                             │
└─────────────────────────────────────────────────────────────────────────┘

   ┌───────────────────────┐
   │   User A              │  ← Member of Eligible Group
   │   (in Eligible Group) │
   └───────────┬───────────┘
               │
               │ 3. User A requests PIM activation
               │    └─ Provides justification (required by activationPolicy)
               │    └─ Completes MFA (required by activationPolicy)
               ▼
   ┌───────────────────────┐
   │   APPROVER GROUP      │  ← Receives approval request
   │   bicep-pim-enhanced  │
   │   -approvers          │
   │   └─ Approver         │
   └───────────┬───────────┘
               │
               │ 4. Approver reviews and approves (3-day timeout)
               │    └─ Provides justification (required by approvalPolicy)
               ▼
   ┌───────────────────────┐
   │   ACTIVATED GROUP     │  ← User A gets TEMPORARY membership
   │   bicep-pim-enhanced  │     (max 2 hours per activationPolicy)
   │   -activated          │
   │   └─ User A ⏰        │
   └───────────┬───────────┘
               │
               │ 5. Activated Group has RBAC on Azure resources
               ▼
   ┌───────────────────────┐
   │   AZURE RESOURCES     │
   │   (assigned manually) │
   │   - Contributor       │
   │   - Reader            │
   │   - Custom Role       │
   └───────────────────────┘
               │
               │ 6. After 2 hours: PIM auto-removes User A from Activated Group
               │    └─ User A loses Azure resource access
               │    └─ User A remains in Eligible Group (can reactivate)
               ▼

KEY:
⏰ = Temporary membership (max 2 hours, controlled by activationPolicy)
```

## What This Deploys

### Security Groups (3)

| Group | Purpose | Members |
|-------|---------|---------|
| **Eligible Group** | Users who CAN activate PIM | Empty (add users manually or via access package) |
| **Activated Group** | Temporary membership via PIM activation | Empty (PIM controls membership) |
| **Approver Group** | Members approve PIM activation requests | Test user (configurable) |

> Scenario 3 creates a second trio of groups with the `-caonly` suffix when `enableAuthContextOnlyScenario` is `true`. Scenario 2 does **not** create groups; it reuses the IDs you provide.

### PIM Eligibility Configuration

The `groupPimEligibilityEnhanced` resource configures **all** PIM policies in a single declaration:

```bicep
resource pimEligibility 'groupPimEligibilityEnhanced' = {
  // Group references
  eligibleGroupUniqueName: pimEligibleGroup.uniqueName
  activatedGroupUniqueName: pimActivatedGroup.uniqueName
  accessId: 'member'  // or 'owner' for group ownership

  // Justification for the eligibility assignment itself
  justification: 'Enhanced handler sample'

  // ┌─────────────────────────────────────────────────────────┐
  // │ SCHEDULE: When does the eligibility expire?            │
  // └─────────────────────────────────────────────────────────┘
  schedule: {
    expirationType: 'AfterDuration'  // or 'AfterDateTime', 'NoExpiration'
    expirationDuration: 'P180D'      // 180 days from now
  }

  // ┌─────────────────────────────────────────────────────────┐
  // │ ELIGIBILITY EXPIRATION: Policy for admin assignments   │
  // │ Graph rule: Expiration_Admin_Eligibility               │
  // └─────────────────────────────────────────────────────────┘
  eligibilityExpiration: {
    requireExpiration: true
    maximumDuration: 'P365D'  // Max 1 year eligibility
  }

  // ┌─────────────────────────────────────────────────────────┐
  // │ ACTIVATION POLICY: What's required to activate?        │
  // │ Graph rules: Expiration_EndUser_Assignment             │
  // │              Enablement_EndUser_Assignment             │
  // └─────────────────────────────────────────────────────────┘
  activationPolicy: {
    maxActivationDuration: 'PT2H'    // 2 hours max
    requireJustification: true
    requireMfa: true
    requireTicket: false
    requireConditionalAccessContext: true
    conditionalAccessContextId: conditionalAccessContextId
  }

> **Note**: Per [Microsoft Learn \"Rules in PIM - Mapping guide\"](https://learn.microsoft.com/en-us/graph/identity-governance-pim-rules-overview#mapping-of-rule-ids-to-pim-role-settings-on-the-microsoft-entra-admin-center), MFA (`Enablement_EndUser_Assignment`) and Conditional Access authentication context (`AuthenticationContext_EndUser_Assignment`) are independent rule IDs. You can safely enable both, as this sample does, to require MFA **and** an authentication context policy during activation.

  // ┌─────────────────────────────────────────────────────────┐
  // │ APPROVAL POLICY: Who approves activations?             │
  // │ Graph rule: Approval_EndUser_Assignment                │
  // └─────────────────────────────────────────────────────────┘
  approvalPolicy: {
    isApprovalRequired: true
    approvalMode: 'SingleStage'  // or 'Serial', 'Parallel'
    stages: [
      {
        approvalStageTimeoutInDays: 3
        isApproverJustificationRequired: true
        primaryApprovers: [
          {
            type: 'group'
            groupId: pimApproverGroup.id
          }
        ]
      }
    ]
  }

  // ┌─────────────────────────────────────────────────────────┐
  // │ NOTIFICATION POLICY: Who gets notified?                │
  // │ Graph rules: Notification_*                            │
  // └─────────────────────────────────────────────────────────┘
  notificationPolicy: {
    notifyAdminsOnEligibility: true
    notifyRequestorsOnEligibility: true
    notifyApproversOnEligibility: true
    notifyAdminsOnActivation: true
    notifyRequestorsOnActivation: true
    notifyApproversOnActivation: true
  }

  // ┌─────────────────────────────────────────────────────────┐
  // │ ADMIN ASSIGNMENT POLICY: Controls for automation       │
  // │ Graph rule: Enablement_Admin_Assignment                │
  // └─────────────────────────────────────────────────────────┘
  adminAssignmentPolicy: {
    requireJustification: true
    requireMfa: true
    requireTicket: false
  }
}
```

## Graph API Mapping

The enhanced handler orchestrates multiple Graph API calls:

| Bicep Property | Graph API Endpoint | Rule ID |
|----------------|-------------------|---------|
| `schedule` | `POST /identityGovernance/privilegedAccess/group/eligibilityScheduleRequests` | — |
| `eligibilityExpiration` | `PATCH /policies/roleManagementPolicies/{id}/rules/Expiration_Admin_Eligibility` | `Expiration_Admin_Eligibility` |
| `activationPolicy.maxActivationDuration` | `PATCH .../rules/Expiration_EndUser_Assignment` | `Expiration_EndUser_Assignment` |
| `activationPolicy.require*` | `PATCH .../rules/Enablement_EndUser_Assignment` | `Enablement_EndUser_Assignment` |
| `approvalPolicy` | `PATCH .../rules/Approval_EndUser_Assignment` | `Approval_EndUser_Assignment` |
| `notificationPolicy` | `PATCH .../rules/Notification_*` | 6 notification rules |
| `adminAssignmentPolicy` | `PATCH .../rules/Enablement_Admin_Assignment` | `Enablement_Admin_Assignment` |

## Comparison: Enhanced vs Legacy Handler

| Feature | `groupPimEligibility` (legacy) | `groupPimEligibilityEnhanced` (new) |
|---------|-------------------------------|-------------------------------------|
| Policy configuration | Via JSON template file | Declarative Bicep properties |
| Approval stages | Limited | Full support (1-2 stages) |
| Notification control | All or nothing | Per-channel toggles |
| Admin policy | Not configurable | Full control |
| Eligibility expiration | Basic | Policy-level control |
| Activation requirements | Basic | MFA, justification, ticketing |
| IntelliSense | Limited | Full type completion |

## Parameters

| Name | Description | Default |
|------|-------------|---------|
| `entitlementToken` | Graph token with `EntitlementManagement.ReadWrite.All` | _required_ |
| `groupUserToken` | Graph token with `Group.ReadWrite.All` + `User.Read.All` | _required_ |
| `namePrefix` | Prefix for Scenario 1 & 3 group `uniqueName` values | `bicep-pim-enhanced` |
| `testUserId` | User object ID added to approver groups that the template creates | `7a72c098-...` |
| `conditionalAccessContextId` | Authentication context ID (c1-c99) enforced in Scenario 1 | `c1` |
| `requireConditionalAccessContext` | Toggle to enforce authentication context in Scenario 1 | `true` |
| `enableExistingGroupScenario` | Deploy Scenario 2 (reuse existing group IDs) | `false` |
| `existingEligibleGroupId` | Eligible group object ID used in Scenario 2 | `''` |
| `existingActivatedGroupId` | Activated group object ID used in Scenario 2 | `''` |
| `existingApproverGroupId` | Optional approver group ID for Scenario 2 (blank = no approval) | `''` |
| `existingConditionalAccessContextId` | Authentication context ID for Scenario 2 | `c1` |
| `existingRequireConditionalAccessContext` | Toggle to enforce authentication context in Scenario 2 | `true` |
| `enableAuthContextOnlyScenario` | Deploy Scenario 3 (Conditional Access only, no MFA) | `true` |
| `authContextOnlyId` | Authentication context ID enforced in Scenario 3 | `c2` |

## Deployment

### Prerequisites

1. **Bicep CLI** 0.38.33+ with experimental `localDeploy` enabled
2. **Published extension** in `../entitlementmgmt-ext/`
3. **Graph API tokens** with required scopes

### Quick Start

```bash
# 1. Navigate to repo root
cd /path/to/entitlement-management-bicep-local-deploy

# 2. Publish extension (if not done)
pwsh Scripts/Publish-Extension.ps1 -Target "./sample/entitlementmgmt-ext"

# 3. Get fresh Graph token
python3 Scripts/get_access_token.py

# 4. Export tokens (token auto-copied to clipboard)
export GRAPH_TOKEN=$(pbpaste | tr -d '\n')
export ENTITLEMENT_TOKEN=$GRAPH_TOKEN
export GROUP_USER_TOKEN=$GRAPH_TOKEN

# 5. Deploy
cd sample/05-group-pim-eligibility-enhanced
bicep local-deploy main.bicepparam
```

### Expected Output

```text
╭───────────────────┬──────────┬───────────╮
│ Resource          │ Duration │ Status    │
├───────────────────┼──────────┼───────────┤
│ pimActivatedGroup │ 0,5s     │ Succeeded │
│ pimEligibleGroup  │ 0,5s     │ Succeeded │
│ pimApproverGroup  │ 0,7s     │ Succeeded │
│ pimEligibility    │ 30,2s    │ Succeeded │  ← Policy patching happens here
╰───────────────────┴──────────┴───────────╯
╭──────────────────────────┬─────────────────────────────────────────────╮
│ Output                   │ Value                                       │
├──────────────────────────┼─────────────────────────────────────────────┤
│ pimActivatedGroupId      │ d5cb6269-4352-4e8f-9d9d-bf85e5cd7937        │
│ pimApproverGroupId       │ 2a3f2ed5-a32d-4354-894a-d628a015f5da        │
│ pimEligibilityRequestId  │ d5cb6269-..._member_51bf11dd-...            │
│ pimEligibilityScheduleId │ not-available                               │
│ pimEligibleGroupId       │ 51bf11dd-c815-45b3-9534-e71081061962        │
╰──────────────────────────┴─────────────────────────────────────────────╯
```

### Idempotency

Running the deployment again will:

1. Detect existing groups → no-op
2. Detect existing eligibility schedule → skip creation
3. Apply policy patches → update if changed

```bash
# Second run - idempotent
bicep local-deploy main.bicepparam

# Output shows same resources, eligibility detected as existing
```

## Outputs

| Output | Description |
|--------|-------------|
| `scenario1EligibleGroupId` / `scenario1ActivatedGroupId` / `scenario1ApproverGroupId` | Object IDs for the default uniqueName scenario. Assign RBAC to the activated group ID. |
| `scenario1EligibilityRequestId` / `scenario1EligibilityScheduleId` | Graph identifiers from the Scenario 1 deployment. |
| `scenario2*` outputs | Echo the IDs and request metadata when `enableExistingGroupScenario` is `true`; otherwise they return `scenario-disabled`. |
| `scenario3EligibleGroupId` / `scenario3ActivatedGroupId` / `scenario3ApproverGroupId` | IDs for the Conditional Access-only scenario (only populated when enabled). |
| `scenario3EligibilityRequestId` / `scenario3EligibilityScheduleId` | Authentication-context-only eligibility metadata. |

## Post-Deployment: Assign RBAC

The `pimActivatedGroupId` is the group that needs RBAC on Azure resources:

```bash
# Get activated group ID from deployment output
ACTIVATED_GROUP_ID="<pimActivatedGroupId>"

# Option 1: Azure CLI
az role assignment create \
  --assignee "$ACTIVATED_GROUP_ID" \
  --role "Contributor" \
  --resource-group "production-rg"

# Option 2: Bicep (separate deployment)
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, activatedGroupId, 'Contributor')
  properties: {
    principalId: activatedGroupId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b24988ac-6180-42a0-ab88-20f7382dd24c')
    principalType: 'Group'
  }
}
```

## Use Cases

| Scenario | Configuration |
|----------|---------------|
| **Production access** | `maxActivationDuration: 'PT2H'`, approval required |
| **Emergency access** | `maxActivationDuration: 'PT4H'`, no approval, notification only |
| **Compliance-heavy** | MFA + justification + ticketing + 2-stage approval |
| **Self-service** | No approval, justification only |

## Troubleshooting

### ExpirationRule Policy Validation Failed

**Error**: `RoleAssignmentRequestPolicyValidationFailed: ["ExpirationRule"]`

**Cause**: The `schedule.expirationType` doesn't match the group's `Expiration_Admin_Eligibility` policy.

**Fix**: Use `AfterDuration` with a duration ≤ the policy's `maximumDuration`:

```bicep
schedule: {
  expirationType: 'AfterDuration'
  expirationDuration: 'P180D'  // Must be ≤ policy maximum
}
```

### 401 Unauthorized

**Cause**: Token expired or missing required scopes.

**Fix**: Re-run token acquisition:

```bash
python3 Scripts/get_access_token.py
export GRAPH_TOKEN=$(pbpaste | tr -d '\n')
export ENTITLEMENT_TOKEN=$GRAPH_TOKEN
export GROUP_USER_TOKEN=$GRAPH_TOKEN
```

### 404 Not Found on Approver Group

**Cause**: `testUserId` doesn't exist in your tenant.

**Fix**: Update `main.bicepparam` with a valid user object ID from your tenant.

## Clean Up

Delete resources via Azure Portal:

1. **Entra ID** → **Groups** → Delete:
   - `Bicep Local - Enhanced PIM Eligible`
   - `Bicep Local - Enhanced PIM Activated`
   - `Bicep Local - Enhanced PIM Approvers`

2. PIM eligibility schedules are automatically removed when the activated group is deleted.

Or use Azure CLI:

```bash
# Get group IDs from deployment output
az ad group delete --group "<pimEligibleGroupId>"
az ad group delete --group "<pimActivatedGroupId>"
az ad group delete --group "<pimApproverGroupId>"
```

## Related Samples

| Sample | Description |
|--------|-------------|
| **03-catalog-pim-jit-access** | Full access package workflow + legacy PIM handler |
| **04-catalog-approval-workflows** | Different approval patterns (manager, user, group, 2-stage) |

## Why Use This Over Sample 03?

- **No catalog/access package overhead** — test PIM configuration directly
- **Declarative policies** — no external JSON template files
- **Full IntelliSense** — all policy options visible in Bicep
- **Faster iteration** — change policies, redeploy, see results

Use **sample 03** when you need the full access package governance workflow.
Use **this sample** when you just want to configure PIM policies quickly.
