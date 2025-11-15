# PIM Just-In-Time (JIT) Access with Access Package Workflow

**⭐ UNIQUE VALUE!** Microsoft Graph Bicep extension does **NOT** have `groupPimEligibility` resource!

This sample demonstrates a **complete PIM workflow** combining:
- **Access Package Governance**: Requestor → Approver → Eligible Group assignment
- **PIM Activation**: Eligible → Activated group (time-limited Azure resource access)

## Complete Flow: Request → Approve → Eligible → Activate → Azure Access

```
┌─────────────────────────────────────────────────────────────────────┐
│                    ACCESS PACKAGE WORKFLOW                          │
└─────────────────────────────────────────────────────────────────────┘

   ┌──────────────────┐
   │ Requestor Group  │ (Users who CAN request access package)
   │ └─ User A        │
   └────────┬─────────┘
            │
            │ 1. Request Access Package
            ▼
   ┌──────────────────┐
   │ Approver Group   │ (Approves access package requests)
   │ └─ Manager       │
   └────────┬─────────┘
            │
            │ 2. Approve Request
            ▼
   ┌──────────────────┐
   │ Access Package   │ (Grants Eligible Group membership)
   │ - Eligible Group │
   └────────┬─────────┘
            │
            │ 3. Access Package Assigns Membership
            ▼

┌─────────────────────────────────────────────────────────────────────┐
│                    PIM ELIGIBILITY WORKFLOW                         │
└─────────────────────────────────────────────────────────────────────┘

   ┌──────────────────┐
   │ Eligible Group   │ (Can activate PIM for Azure resource access)
   │ └─ User A ✅     │ ← Granted by access package assignment
   └────────┬─────────┘
            │
            │ PIM Eligibility Link (2-hour max activation)
            │
            │ 4. User A Requests PIM Activation
            ▼
   ┌──────────────────┐
   │ Activated Group  │ (Temporary membership = Azure RBAC access)
   │ └─ User A ⏰     │ ← PIM grants for 2 hours max
   └────────┬─────────┘
            │
            │ 5. Activated Group Has RBAC on Azure Resources
            ▼
   ┌──────────────────┐
   │ Azure Resources  │ (Production VMs, Storage, Databases, etc.)
   │ - Contributor    │
   │ - Reader         │
   └──────────────────┘

KEY:
✅ = Permanent membership (via access package)
⏰ = Temporary membership (via PIM activation, max 2 hours)
```

## What This Deploys

### Access Package Governance (6 Groups + Catalog)
1. **Requestor Group**: Users who can request access to the access package
2. **Approver Group**: Users who approve access package requests
3. **Reviewer Group**: Users who review ongoing access package assignments
4. **Eligible Group**: Granted by access package - enables PIM activation
5. **Activated Group**: Temporary membership via PIM - has RBAC on Azure resources
6. **PIM Catalog**: Container for access packages
7. **Catalog Resource**: Eligible group added to catalog (NOT activated group!)
8. **Access Package**: Grants eligible group membership
9. **Resource Role**: Member role of eligible group
10. **Assignment Policy**: Requestor → Approver approval workflow

### PIM Eligibility Configuration
11. **PIM Eligibility** 🔥: Links eligible → activated groups with activation policy

## The Complete User Journey

### Step 1: Request Access Package
```
User A (in Requestor Group) logs into Azure Portal
→ Entra ID → Identity Governance → Access Packages
→ Finds "Bicep Local - PIM JIT Developer Activation"
→ Clicks "Request Access" → Provides justification
→ Request sent to Approver Group
```

### Step 2: Approval
```
Manager (in Approver Group) receives notification
→ Reviews request → Approves
→ User A is granted membership to Eligible Group (permanent, via access package)
```

### Step 3: PIM Activation
```
User A (now in Eligible Group) needs Azure resource access
→ Azure Portal → Entra ID → Groups → "Bicep Local - PIM Activated Developers"
→ Clicks "Activate" → Provides justification
→ PIM grants temporary membership to Activated Group (max 2 hours)
→ User A now has RBAC on Azure resources (e.g., Contributor on production RG)
```

### Step 4: Automatic Removal
```
After 2 hours:
→ PIM automatically removes User A from Activated Group
→ User A no longer has Azure resource access
→ User A still has Eligible Group membership (can reactivate if needed)
```

## Why Eligible Group is in Catalog (NOT Activated)

**Critical Design Decision**:

```bicep
// ✅ CORRECT: Eligible group is catalog resource
resource catalogResourcePimEligible 'accessPackageCatalogResource' = {
  catalogId: pimCatalog.id
  originId: pimEligibleGroup.id  // ← Eligible group!
  // ...
}

// ❌ WRONG: Don't add activated group to catalog
// Activated group has RBAC on Azure resources (managed outside this template)
```

**Reason**:
- **Access package assigns** → Eligible group membership (permanent, governance-controlled)
- **PIM controls** → Activated group membership (temporary, time-limited)
- **Activated group** → Has RBAC on Azure resources (assigned manually or via other automation)

**Flow**:
1. Access package grants → Eligible group membership
2. User activates PIM → Activated group membership (temporary)
3. Activated group RBAC → Azure resource access

## Deployment Outputs

After deployment, you'll receive these IDs:

```bicep
// Access Package Workflow Groups
output requestorGroupId string  // Add users who should request access
output approverGroupId string   // Add managers who approve requests
output reviewerGroupId string   // Add users who review assignments

// PIM Groups
output pimEligibleGroupId string   // Granted by access package
output pimActivatedGroupId string  // ⭐ Assign this group RBAC on Azure resources!

// Catalog and Policy
output pimCatalogId string
output pimAccessPackageId string
output pimAccessPolicyId string
output pimEligibilityId string
```

**IMPORTANT**: The `pimActivatedGroupId` is the group you assign RBAC to on Azure resources!

Example (Azure CLI):
```bash
# Get activated group ID from deployment output
ACTIVATED_GROUP_ID="<pimActivatedGroupId from output>"

# Assign Contributor role on production resource group
az role assignment create \
  --assignee "$ACTIVATED_GROUP_ID" \
  --role "Contributor" \
  --resource-group "production-rg"

# Now when users activate PIM, they get Contributor access for 2 hours!
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

- **Approval workflows**: See `04-catalog-approval-workflows/` for different approval patterns
- **Production**: Use activated group in Azure RBAC, Entra ID roles, or application permissions

## Clean Up

Remove resources in Azure Portal:
- **Entra ID** → **Groups** → Delete both groups
- **Entra ID** → **Identity Governance** → **Entitlement Management** → Delete PIM catalog
- PIM eligibility is automatically removed when groups are deleted
