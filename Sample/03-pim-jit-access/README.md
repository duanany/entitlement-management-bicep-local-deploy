# PIM Just-In-Time (JIT) Access

**⭐ UNIQUE VALUE!** Microsoft Graph Bicep extension does **NOT** have `groupPimEligibility` resource!

This sample demonstrates Privileged Identity Management (PIM) for time-limited group membership.

## What This Deploys

- **2 Security Groups**: Eligible (who can request) + Activated (temporary membership)
- **1 PIM Catalog**: Dedicated catalog for JIT access
- **1 Catalog Resource**: Activated group added to catalog
- **1 Access Package**: Grants time-limited activated group membership
- **1 Resource Role**: Member role of activated group
- **1 Assignment Policy**: Peer approval for JIT activation
- **1 PIM Eligibility** 🔥: Links eligible → activated groups with activation policy

## Flow Diagram

```
┌──────────────────────┐
│ Eligible Group       │ (Users who CAN request activation)
│ └─ User A (member)   │
│ └─ User B (member)   │
└──────────┬───────────┘
           │
           │ PIM Eligibility Link
           │ (2-hour max activation)
           │
           ▼
┌──────────────────────┐
│ Activated Group      │ (Temporary membership granted via JIT)
│ └─ (empty - PIM      │
│    controls this)    │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│ PIM Catalog          │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│ Access Package       │ (Grants activated group membership)
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│ Assignment Policy    │ (Peer approval + JIT rules)
└──────────────────────┘

USER FLOW:
1. User A (in Eligible Group) requests activation
2. User B (peer) approves the request
3. User A gets temporary membership in Activated Group (max 2 hours)
4. After expiration, membership automatically removed
```

## Why Is This Important?

### Microsoft Graph Bicep Does NOT Have This!

The official **Microsoft Graph Bicep extension** provides:
- ✅ `microsoft.graph/groups` (security groups)
- ✅ `microsoft.graph/users`
- ✅ `microsoft.graph/applications`
- ❌ **NO `groupPimEligibility` resource!**

**This Bicep local extension is the ONLY way** to manage PIM eligibility via infrastructure-as-code!

### Use Cases

- **Production access**: Developers request production group membership only when needed
- **Admin rights**: Temporary elevation to admin groups
- **Compliance**: Enforce time-limited access with justification and approval
- **Zero standing privilege**: No permanent memberships, only JIT activation

## Group Reference Options

The `groupPimEligibility` resource supports **two ways** to reference groups:

### Option 1: Reference by uniqueName (convenient for all-in-one deployments)

```bicep
resource pimEligibility 'groupPimEligibility' = {
  eligibleGroupUniqueName: pimEligibleGroup.uniqueName
  activatedGroupUniqueName: pimActivatedGroup.uniqueName
  accessId: 'member'
  expirationDateTime: '2026-05-15T00:00:00Z'
  policyTemplateJson: loadTextContent('../pim-policy-template.json')
  maxActivationDuration: 'PT2H'
}
```

**Best for**: When creating groups AND PIM eligibility in the same template.

### Option 2: Reference by ID (enables cross-deployment scenarios)

```bicep
resource pimEligibility 'groupPimEligibility' = {
  eligibleGroupId: pimEligibleGroup.id
  activatedGroupId: pimActivatedGroup.id
  accessId: 'member'
  expirationDateTime: '2026-05-15T00:00:00Z'
  policyTemplateJson: loadTextContent('../pim-policy-template.json')
  maxActivationDuration: 'PT2H'
}
```

**Best for**:
- Referencing groups created in **separate deployments**
- Using groups from **standard Bicep** (microsoft.graph/groups@1.0)
- Referencing **existing Entra ID groups** (pass GUID as parameter)

**Example**: Cross-deployment scenario

```bicep
// File 1: groups.bicep (standard Bicep - deploy with 'az deployment group create')
targetScope = 'resourceGroup'

extension microsoftGraph

resource eligibleGroup 'Microsoft.Graph/groups@1.0' = {
  displayName: 'PIM Eligible Developers'
  mailEnabled: false
  securityEnabled: true
  uniqueName: 'pim-eligible-developers'
}

output groupId string = eligibleGroup.id

// File 2: pim-eligibility.bicep (local-deploy - deploy with 'bicep local-deploy')
targetScope = 'local'

extension entitlementmgmt

param eligibleGroupId string  // From output of groups.bicep

resource pimEligibility 'groupPimEligibility' = {
  eligibleGroupId: eligibleGroupId  // ← Direct ID reference!
  activatedGroupId: '...'  // Reference activated group ID
  // ... rest of config
}
```

**Both options work identically** - use whichever fits your deployment strategy!

## Prerequisites

- Microsoft Graph API tokens:
  - `ENTITLEMENT_TOKEN`: `EntitlementManagement.ReadWrite.All`
  - `GROUP_USER_TOKEN`: `Group.ReadWrite.All` + `User.Read.All`
- Published extension in `../entitlementmgmt-ext/`
- PIM policy template: `../pim-policy-template.json`

## Deploy

```bash
# From this directory
bicep local-deploy main.bicepparam
```

## What You'll See

```
✓ pimEligibleGroup (0.7s)
✓ pimActivatedGroup (0.3s)
✓ pimCatalog (0.5s)
✓ catalogResourcePimActivated (0.4s)
✓ pimAccessPackage (0.6s)
✓ pimResourceRole (0.4s)
✓ pimAccessPolicy (1.8s)
✓ pimEligibility (46.1s)  ← The magic happens here! 🔥
```

## How Users Activate Access

After deployment, users in the **Eligible Group** can:

1. Go to **Azure Portal** → **Entra ID** → **Groups** → **PIM Activated Developers**
2. Click **Activate** → Provide justification → Request approval
3. Peer approves → User gets temporary membership (max 2 hours)
4. After expiration → Membership automatically removed

## PIM Policy Template

The `pim-policy-template.json` defines:
- Activation requirements (MFA, justification)
- Approval workflow
- Notification settings
- Maximum activation duration

See `../pim-policy-template.json` for the full policy.

## Next Steps

- **Approval workflows**: See `04-approval-workflows/` for different approval patterns
- **Production**: Use activated group in Azure RBAC, Entra ID roles, or application permissions

## Clean Up

Remove resources in Azure Portal:
- **Entra ID** → **Groups** → Delete both groups
- **Entra ID** → **Identity Governance** → **Entitlement Management** → Delete PIM catalog
- PIM eligibility is automatically removed when groups are deleted
