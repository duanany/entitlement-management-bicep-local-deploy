# Group PIM Eligibility Resource

## Overview

The `groupPimEligibility` resource configures **Just-In-Time (JIT) access** between two **existing** security groups using Entra ID Privileged Identity Management (PIM).

**Key Principle**: This resource does NOT create groups - it only establishes PIM eligibility rules between groups you've already created.

## Workflow

```bicep
// STEP 1: Create ELIGIBLE group with members
resource eligibleGroup 'securityGroup' = {
  uniqueName: 'pim-eligible-devs'
  displayName: 'PIM Eligible Developers'
  members: ['user-guid-1', 'user-guid-2']  // Permanent members
}

// STEP 2: Create ACTIVATED group (usually empty)
resource activatedGroup 'securityGroup' = {
  uniqueName: 'pim-activated-devs'
  displayName: 'PIM Activated Developers'
  members: []  // PIM controls membership
}

// STEP 3: Configure PIM eligibility
resource pimEligibility 'groupPimEligibility' = {
  eligibleGroupUniqueName: eligibleGroup.uniqueName
  activatedGroupUniqueName: activatedGroup.uniqueName
  accessId: 'member'
  justification: 'Developer JIT access'
  policyTemplateJson: loadTextContent('./pim-policy.json')
  maxActivationDuration: 'PT2H'  // 2 hours max
}
```

## How It Works

1. **Add users to ELIGIBLE group** → Permanent membership
2. **Users activate PIM** → Temporarily join ACTIVATED group (e.g., 2 hours)
3. **Activation expires** → Users automatically removed from ACTIVATED group
4. **Eligible members approve requests** (by default) or specify custom approvers

## Properties

### Required Properties

| Property | Type | Description |
|----------|------|-------------|
| `eligibleGroupUniqueName` | `string` | Unique name (mailNickname) of the ELIGIBLE group. Must exist before configuring PIM. |
| `activatedGroupUniqueName` | `string` | Unique name (mailNickname) of the ACTIVATED group. Must exist before configuring PIM. |

### Optional Configuration

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `accessId` | `string` | `'member'` | Access type: `'member'` or `'owner'` |
| `justification` | `string` | `null` | Justification for PIM eligibility configuration |
| `expirationDateTime` | `string` | `null` | When eligibility expires (ISO 8601). Omit for permanent eligibility. Example: `'2026-05-08T23:59:59Z'` |
| `policyTemplateJson` | `string` | `null` | Full PIM policy template (approval workflows, MFA, activation rules). Load via `loadTextContent('./policy.json')` |
| `approverGroupId` | `string` | `null` | Custom approver group ID. If omitted, ELIGIBLE group members approve each other (self-approval). |
| `maxActivationDuration` | `string` | `'PT2H'` | Maximum activation duration (ISO 8601 duration). Example: `'PT2H'` = 2 hours, `'PT30M'` = 30 minutes |

### Read-Only Outputs

| Property | Type | Description |
|----------|------|-------------|
| `eligibleGroupId` | `string` | Entra ID Object ID of the eligible group (resolved from uniqueName) |
| `activatedGroupId` | `string` | Entra ID Object ID of the activated group (resolved from uniqueName) |
| `pimEligibilityRequestId` | `string` | PIM eligibility schedule request ID |
| `pimEligibilityScheduleId` | `string` | PIM eligibility schedule ID (created after request is processed) |

## Usage Examples

### Example 1: Basic Self-Approval Workflow

```bicep
resource eligibleGroup 'securityGroup' = {
  uniqueName: 'pim-eligible-developers'
  displayName: 'PIM Eligible Developers'
  members: [
    '7a72c098-a42d-489f-a3fa-c2445dec6f9c'  // Developer 1
    '0afd1da6-51fb-450f-bf1a-069a85dcacad'  // Developer 2
  ]
}

resource activatedGroup 'securityGroup' = {
  uniqueName: 'pim-activated-developers'
  displayName: 'PIM Activated Developers'
  members: []
}

resource pim 'groupPimEligibility' = {
  eligibleGroupUniqueName: eligibleGroup.uniqueName
  activatedGroupUniqueName: activatedGroup.uniqueName
  accessId: 'member'
  justification: 'Developer JIT access - self-approval'
  policyTemplateJson: loadTextContent('./pim-policy-basic.json')
  maxActivationDuration: 'PT2H'
}
```

### Example 2: Contractor Workflow with Custom Approvers

```bicep
resource contractorEligibleGroup 'securityGroup' = {
  uniqueName: 'pim-eligible-contractors'
  displayName: 'PIM Eligible Contractors'
  members: [
    'contractor-guid-1'
    'contractor-guid-2'
  ]
}

resource contractorActivatedGroup 'securityGroup' = {
  uniqueName: 'pim-activated-contractors'
  displayName: 'PIM Activated Contractors'
  members: []
}

resource approverGroup 'securityGroup' = {
  uniqueName: 'contractor-approvers'
  displayName: 'Contractor Approvers (Managers)'
  members: [
    'manager-guid-1'
    'manager-guid-2'
  ]
}

resource contractorPim 'groupPimEligibility' = {
  eligibleGroupUniqueName: contractorEligibleGroup.uniqueName
  activatedGroupUniqueName: contractorActivatedGroup.uniqueName
  accessId: 'member'
  justification: 'Contractor JIT access - manager approval required'
  policyTemplateJson: loadTextContent('./pim-policy-contractor-strict.json')
  approverGroupId: approverGroup.id  // Managers approve contractor requests
  maxActivationDuration: 'PT1H'  // Only 1 hour for contractors
  expirationDateTime: '2026-03-31T23:59:59Z'  // 3-month eligibility
}
```

### Example 3: Time-Limited Project Access

```bicep
resource projectEligibleGroup 'securityGroup' = {
  uniqueName: 'pim-eligible-project-alpha'
  displayName: 'PIM Eligible - Project Alpha Team'
  members: [
    'team-member-1'
    'team-member-2'
  ]
}

resource projectActivatedGroup 'securityGroup' = {
  uniqueName: 'pim-activated-project-alpha'
  displayName: 'PIM Activated - Project Alpha Resources'
  members: []
}

resource projectPim 'groupPimEligibility' = {
  eligibleGroupUniqueName: projectEligibleGroup.uniqueName
  activatedGroupUniqueName: projectActivatedGroup.uniqueName
  accessId: 'member'
  justification: 'Project Alpha JIT access'
  expirationDateTime: '2025-06-30T23:59:59Z'  // Eligibility ends when project ends
  policyTemplateJson: loadTextContent('./pim-policy-project.json')
  maxActivationDuration: 'PT4H'  // 4-hour work sessions
}
```

## PIM Policy Template

The `policyTemplateJson` property accepts a full PIM policy configuration with approval workflows, MFA requirements, notifications, etc.

**Policy Template Structure**:

```json
{
  "rules": [
    {
      "id": "Approval_EndUser_Assignment",
      "target": {
        "caller": "EndUser",
        "operations": ["All"],
        "level": "Assignment"
      },
      "setting": {
        "isApprovalRequired": true,
        "isApprovalRequiredForExtension": false,
        "isRequestorJustificationRequired": true,
        "approvalMode": "SingleStage",
        "approvalStages": [
          {
            "approvalStageTimeOutInDays": 1,
            "isApproverJustificationRequired": true,
            "escalationTimeInMinutes": 0,
            "primaryApprovers": [
              {
                "id": "{approverGroupId}",
                "type": "groupMembers"
              }
            ],
            "isEscalationEnabled": false,
            "escalationApprovers": []
          }
        ]
      }
    },
    {
      "id": "Expiration_EndUser_Assignment",
      "target": {
        "caller": "EndUser",
        "operations": ["All"],
        "level": "Assignment"
      },
      "setting": {
        "permanentAssignment": false,
        "maximumGrantPeriod": "{maxActivationDuration}"
      }
    }
  ]
}
```

**Placeholder Replacement**:
- `{approverGroupId}` → Replaced with `approverGroupId` property (or eligible group ID if omitted)
- `{maxActivationDuration}` → Replaced with `maxActivationDuration` property (default: `PT2H`)

## Graph API Endpoints Used

| Operation | Endpoint | Permission Required |
|-----------|----------|---------------------|
| Retrieve groups | `GET /v1.0/groups?$filter=mailNickname eq '{uniqueName}'` | `Group.Read.All` |
| Create PIM eligibility | `POST /v1.0/identityGovernance/privilegedAccess/group/eligibilityScheduleRequests` | `PrivilegedEligibilitySchedule.ReadWrite.AzureADGroup` |
| Check existing eligibility | `GET /v1.0/identityGovernance/privilegedAccess/group/eligibilityScheduleInstances` | `PrivilegedEligibilitySchedule.ReadWrite.AzureADGroup` |
| Apply policy | `PATCH /v1.0/policies/roleManagementPolicies/{policyId}/rules/{ruleId}` | `RoleManagementPolicy.ReadWrite.AzureADGroup` |

## Required Permissions

Your service principal or user must have:

- `Group.Read.All` - Read existing groups
- `PrivilegedEligibilitySchedule.ReadWrite.AzureADGroup` - Configure PIM eligibility
- `RoleManagementPolicy.ReadWrite.AzureADGroup` - Configure approval/activation policies

## Idempotency

The resource is idempotent based on `eligibleGroupUniqueName`:

1. **First deployment**: Creates PIM eligibility schedule request
2. **Subsequent deployments**:
   - Checks if eligibility exists (using `eligibilityScheduleInstances` API)
   - If exists with same config → no-op
   - If exists with different policy → applies policy updates
   - If not exists → creates new eligibility

## Best Practices

### 1. Use Self-Approval for Trusted Teams

```bicep
// Eligible members approve each other - fastest activation
resource pim 'groupPimEligibility' = {
  eligibleGroupUniqueName: 'trusted-team-eligible'
  activatedGroupUniqueName: 'trusted-team-activated'
  // No approverGroupId = self-approval!
}
```

### 2. Use Custom Approvers for External/Contractor Access

```bicep
// Managers approve contractor requests
resource contractorPim 'groupPimEligibility' = {
  eligibleGroupUniqueName: 'contractors-eligible'
  activatedGroupUniqueName: 'contractors-activated'
  approverGroupId: managersGroup.id  // Separate approval group
}
```

### 3. Set Expiration for Temporary Projects

```bicep
resource projectPim 'groupPimEligibility' = {
  eligibleGroupUniqueName: 'project-eligible'
  activatedGroupUniqueName: 'project-activated'
  expirationDateTime: '2025-12-31T23:59:59Z'  // Project ends Dec 31
}
```

### 4. Use Shorter Activation for High-Privilege Access

```bicep
resource adminPim 'groupPimEligibility' = {
  eligibleGroupUniqueName: 'admin-eligible'
  activatedGroupUniqueName: 'admin-activated'
  maxActivationDuration: 'PT30M'  // Only 30 minutes for admin access
}
```

## Troubleshooting

### Error: "ELIGIBLE group not found"

**Cause**: Group with `eligibleGroupUniqueName` doesn't exist.

**Solution**: Create the group first using `securityGroup` resource:

```bicep
resource eligibleGroup 'securityGroup' = {
  uniqueName: 'my-eligible-group'
  displayName: 'My Eligible Group'
  members: [...]
}

resource pim 'groupPimEligibility' = {
  eligibleGroupUniqueName: eligibleGroup.uniqueName  // ✅ References created group
  // ...
}
```

### Error: "ACTIVATED group not found"

**Cause**: Group with `activatedGroupUniqueName` doesn't exist.

**Solution**: Same as above - create both groups first.

### Error: "PIM eligibility creation failed: 404"

**Cause**: Groups exist but Graph API replication delay.

**Solution**: Handler automatically retries up to 15 times with 2-second delays (30 seconds total). If persistent, check:
1. Groups actually exist in Entra ID portal
2. Service principal has `Group.Read.All` permission

### Error: "Access denied. Verify permissions."

**Cause**: Service principal lacks required permissions.

**Solution**: Grant these permissions in Entra ID:
- `Group.Read.All`
- `PrivilegedEligibilitySchedule.ReadWrite.AzureADGroup`
- `RoleManagementPolicy.ReadWrite.AzureADGroup`

## Migration from `pimEnabledGroup`

If you're using the older `pimEnabledGroup` resource (which creates groups), migrate to the new pattern:

**Old Pattern** (creates groups):
```bicep
resource oldPim 'pimEnabledGroup' = {
  eligibleGroupUniqueName: 'pim-eligible-devs'
  eligibleGroupDisplayName: 'PIM Eligible Developers'
  activatedGroupUniqueName: 'pim-activated-devs'
  activatedGroupDisplayName: 'PIM Activated Developers'
  eligibleMembers: 'guid1, guid2'  // Comma-separated workaround
}
```

**New Pattern** (uses existing groups):
```bicep
resource eligibleGroup 'securityGroup' = {
  uniqueName: 'pim-eligible-devs'
  displayName: 'PIM Eligible Developers'
  members: ['guid1', 'guid2']  // ✅ Native array support!
}

resource activatedGroup 'securityGroup' = {
  uniqueName: 'pim-activated-devs'
  displayName: 'PIM Activated Developers'
  members: []
}

resource pim 'groupPimEligibility' = {
  eligibleGroupUniqueName: eligibleGroup.uniqueName
  activatedGroupUniqueName: activatedGroup.uniqueName
  policyTemplateJson: loadTextContent('./pim-policy.json')
}
```

**Benefits of New Pattern**:
- ✅ Native array support for group members
- ✅ Cleaner separation of concerns
- ✅ More flexible (reuse groups, change PIM config independently)
- ✅ Simpler resource model (fewer properties)

## See Also

- [SecurityGroup Resource](./securityGroup.md)
- [PIM Policy Templates](../Sample/pim-policy-template.json)
- [Microsoft Graph PIM API Documentation](https://learn.microsoft.com/en-us/graph/api/resources/privilegedidentitymanagementv3-overview?view=graph-rest-1.0)
