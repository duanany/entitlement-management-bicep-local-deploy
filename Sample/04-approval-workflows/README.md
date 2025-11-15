# Approval Workflows

Demonstrates 4 different approval patterns for access package requests.

## What This Deploys

- **1 Catalog**: Container for all approval workflow examples
- **4 Access Packages**: Each demonstrating a different approval pattern
- **4 Assignment Policies**: Different approval configurations

## Approval Patterns

### 1️⃣ Manager Approval (`requestorManager`)

```bicep
primaryApprovers: [
  {
    oDataType: '#microsoft.graph.requestorManager'
    managerLevel: 1  // Direct manager
  }
]
```

**Use Case**: Standard corporate approval - user's direct manager approves

### 2️⃣ Specific User Approver (`singleUser`)

```bicep
primaryApprovers: [
  {
    oDataType: '#microsoft.graph.singleUser'
    userId: '<user-guid>'
    description: 'Security team lead'
  }
]
```

**Use Case**: Designated approver (security lead, team lead, resource owner)

### 3️⃣ Group-Based Approval (`groupMembers`)

```bicep
allowedRequestors: [
  {
    oDataType: '#microsoft.graph.groupMembers'
    groupId: '<group-guid>'
  }
]
primaryApprovers: [
  {
    oDataType: '#microsoft.graph.groupMembers'
    groupId: '<same-group-guid>'  // Peer approval
  }
]
```

**Use Case**: Peer approval within a team + quarterly access reviews

### 4️⃣ Two-Stage Approval (`Serial` mode)

```bicep
approvalMode: 'Serial'  // Sequential stages
approvalStages: [
  {  // Stage 1
    primaryApprovers: [{ oDataType: '#microsoft.graph.singleUser' }]
  }
  {  // Stage 2
    primaryApprovers: [{ oDataType: '#microsoft.graph.groupMembers' }]
  }
]
```

**Use Case**: Multi-level approval (team lead → security team)

## Flow Diagram

```
REQUEST
  │
  ▼
┌─────────────────────────────────────┐
│ APPROVAL PATTERN DECISION           │
└──┬────────┬──────────┬──────────┬───┘
   │        │          │          │
   ▼        ▼          ▼          ▼
MANAGER  SPECIFIC  GROUP     TWO-STAGE
APPROVAL  USER    PEER      (User→Group)
         APPROVE  APPROVE
   │        │          │          │
   │        │          │       ┌──┴───┐
   │        │          │       │Stage1│
   │        │          │       └──┬───┘
   │        │          │          │
   │        │          │       ┌──▼───┐
   │        │          │       │Stage2│
   │        │          │       └──┬───┘
   └────────┴──────────┴──────────┘
              │
              ▼
         ✅ APPROVED
              │
              ▼
         ACCESS GRANTED
```

## Prerequisites

- Microsoft Graph API token:
  - `ENTITLEMENT_TOKEN`: `EntitlementManagement.ReadWrite.All`
- Published extension in `../entitlementmgmt-ext/`

## Edit Parameters

Update `main.bicepparam`:
- `testUserId`: User who will be the approver in patterns 2 & 4
- `testGroupId`: Group for peer approval in patterns 3 & 4

```bash
# Get user ID
az ad user show --id user@domain.com --query id -o tsv

# Get group ID
az ad group show --group "TeamName" --query id -o tsv
```

## Deploy

```bash
# From this directory
bicep local-deploy main.bicepparam
```

## What You'll See

```
✓ catalog (0.7s)
✓ managerApprovalPackage (0.5s)
✓ userApproverPackage (0.5s)
✓ groupAccessPackage (0.5s)
✓ twoStagePackage (0.5s)
✓ managerApprovalPolicy (1.4s)
✓ userApproverPolicy (1.6s)
✓ groupAccessPolicy (1.8s)
✓ twoStagePolicy (1.7s)
```

## Testing Approval Workflows

After deployment, test each pattern:

1. **Azure Portal** → **Entra ID** → **Identity Governance** → **Access Packages**
2. Find the access package
3. Click **Request access** (as a test user)
4. Submit request with justification
5. Verify approval flow works as expected

## Comparison Table

| Pattern | Requestors | Approvers | Review | Best For |
|---------|-----------|-----------|--------|----------|
| Manager | All users | Direct manager | None | Standard corporate access |
| Specific User | All users | 1 designated user | None | Resource owner approval |
| Group Peer | Group members | Same group members | Quarterly | Team self-management |
| Two-Stage | All users | User → Group | None | Multi-level security |

## Next Steps

- **Add resources**: Combine with `02-security-groups/` to grant actual access
- **Direct assignments**: Use `accessPackageAssignment` to bypass approval for specific users
- **PIM integration**: Combine with `03-pim-jit-access/` for time-limited approvals

## Clean Up

Remove resources in Azure Portal:
**Entra ID** → **Identity Governance** → **Entitlement Management** → **Catalogs** → Delete catalog (removes all packages and policies)
