# Sample Deployments

Production-ready Bicep templates demonstrating Azure Entitlement Management patterns.

## 📁 Folder Structure

```text
Sample/
├── 01-catalog-basic/               # Minimal deployment (catalog + access package)
├── 02-catalog-with-groups/         # Security group → catalog → access package workflow
├── 03-catalog-pim-jit-access/      # PIM Just-In-Time activation (UNIQUE VALUE! ⭐)
├── 04-catalog-approval-workflows/  # 4 approval patterns: manager, user, group, multi-stage
├── 05-group-pim-eligibility-enhanced/ # Multi-scenario test for groupPimEligibilityEnhanced
├── entitlementmgmt-ext/        # Published extension binaries (auto-generated)
├── pim-policy-template.json    # PIM activation policy template
├── pim-policy-contractor-strict.json # Strict Conditional Access + approval reference
└── README.md                   # This file
```

## 🚀 Quick Start

### 1. Publish the Extension

```bash
# From the repo root
cd entitlement-management
pwsh Scripts/Publish-Extension.ps1 -Target "./Sample/entitlementmgmt-ext"
```

### 2. Get API Tokens

```bash
# Run the token script (adjust path as needed)
python3 entitlement-management/Scripts/get_access_token.py

# This sets environment variables:
# - ENTITLEMENT_TOKEN (EntitlementManagement.ReadWrite.All)
# - GROUP_USER_TOKEN (Group.ReadWrite.All + User.Read.All)
```

### 3. Choose a Sample

| Sample | What It Does | Tokens Needed | Deploy Time |
|--------|-------------|---------------|-------------|
| **01-catalog-basic** | Minimal setup: catalog + package + policy | Entitlement only | ~3s |
| **02-catalog-with-groups** | Create group + add to access package | Both tokens | ~30s |
| **03-catalog-pim-jit-access** ⭐ | PIM eligibility + JIT activation | Both tokens | ~60s |
| **04-catalog-approval-workflows** | 4 approval patterns (manager, user, group, 2-stage) | Entitlement only | ~8s |
| **05-group-pim-eligibility-enhanced** | Multi-scenario validation for `groupPimEligibilityEnhanced` (uniqueName, existing IDs, auth-context-only) | Both tokens | ~40s |

### 4. Deploy

```bash
# Navigate to sample folder
cd 01-catalog-basic

# Edit parameters (optional)
code main.bicepparam

# Deploy
bicep local-deploy main.bicepparam
```

## 📖 Sample Details

### 01-catalog-basic

**Minimal deployment** - catalog + access package + policy.

**What you'll learn**:

- Create access package catalog
- Define access package
- Configure assignment policy
- Deploy with single token

**Resources created**: 3
**Deploy time**: ~3 seconds

[View README](./01-catalog-basic/README.md)

---

### 02-catalog-with-groups

#### Security Group → Catalog → Access Package

Demonstrates the full workflow:

1. Create security group with members
2. Add group to catalog as resource
3. Create access package granting group membership
4. Users request access → Get assigned to security group

Uses `securityGroup` resource (⚠️ for testing only - use Microsoft Graph Bicep for production).

[View README](./02-catalog-with-groups/README.md)

---

### 03-catalog-pim-jit-access ⭐

**UNIQUE VALUE!** Microsoft Graph Bicep does **NOT** have `groupPimEligibility` resource.

**What you'll learn**:

- Privileged Identity Management (PIM) for groups
- Just-In-Time (JIT) access activation
- Time-limited group membership (2-hour max)
- Peer approval workflows
- Why this is the ONLY IaC solution for PIM eligibility

**Resources created**: 8
**Deploy time**: ~60 seconds (pimEligibility takes ~46s)

[View README](./03-catalog-pim-jit-access/README.md)

---

### 04-catalog-approval-workflows

**4 approval patterns** - manager, specific user, group peer, two-stage.

**What you'll learn**:

- `requestorManager` approver type (manager approval)
- `singleUser` approver type (designated approver)
- `groupMembers` approver type (peer approval + reviews)
- Multi-stage approval (Serial mode)
- When to use each pattern

**Resources created**: 9
**Deploy time**: ~8 seconds

[View README](./04-catalog-approval-workflows/README.md)

---

### 05-group-pim-eligibility-enhanced

**Multi-scenario validation** for the enhanced handler (uniqueName, existing IDs, auth-context-only).

**What you'll learn**:

- Create eligible + activated security groups dedicated to PIM (Scenario 1 & 3)
- Submit privilegedAccessGroup eligibility schedule requests without catalogs/access packages
- Reuse existing group IDs without relying on `uniqueName` lookups (Scenario 2)
- Configure activation/approval/notification/admin policy rules through a single resource

**Resources created**: 4–7 depending on toggles
**Deploy time**: ~40 seconds (schedule processing dominates)

[View README](./05-group-pim-eligibility-enhanced/README.md)

## 🔧 Prerequisites

### Required Tools

- **Bicep CLI** 0.38.33 or later (with experimental local-deploy enabled)
- **PowerShell** 7.x (for extension publishing)
- **.NET 9 SDK** (for building the extension)

### Enable Experimental Features

Add to `bicepconfig.json`:

```json
{
  "experimentalFeaturesEnabled": {
    "localDeploy": true
  }
}
```

### Required Permissions

Your service principal or user account needs:

| Token | Permissions | Used For |
|-------|------------|----------|
| `ENTITLEMENT_TOKEN` | `EntitlementManagement.ReadWrite.All` | Catalogs, packages, policies, assignments |
| `GROUP_USER_TOKEN` | `Group.ReadWrite.All` + `User.Read.All` | Security groups, PIM eligibility |

## 🎯 Learning Path

**Beginner** → **Intermediate** → **Advanced**

```text
01-catalog-basic
↓
02-catalog-with-groups
↓
04-catalog-approval-workflows
↓
03-catalog-pim-jit-access ⭐
↓
05-group-pim-eligibility-enhanced (covers uniqueName + existing ID scenarios)
```

## 📚 Additional Resources

- **Full Documentation**: See `../docs/` for detailed resource reference
- **Handler Source Code**: See `../src/` for implementation details
- **Microsoft Docs**: [Entitlement Management Overview](https://learn.microsoft.com/en-us/azure/active-directory/governance/entitlement-management-overview)

## 🧹 Clean Up

**Important**: Delete operations are not yet implemented in this extension.

To remove deployed resources:

1. **Azure Portal** → **Entra ID** → **Identity Governance** → **Entitlement Management**
2. Navigate to **Catalogs**
3. Delete the catalog (removes all access packages and policies)
4. For security groups: **Entra ID** → **Groups** → Delete manually

## 🐛 Troubleshooting

### "Extension not found" error

```bash
# Re-publish the extension
pwsh Scripts/Publish-Extension.ps1 -Target "./Sample/entitlementmgmt-ext"

# Clear Bicep cache
rm -rf ~/.bicep/local

# Try deployment again
bicep local-deploy main.bicepparam
```

### "401 Unauthorized" error

```bash
# Token expired - regenerate. Needed for entitlement management! -> SCOPES = ["https://graph.microsoft.com/EntitlementManagement.ReadWrite.All"]
python3 entitlement-management/Scripts/get_access_token.py

# Verify token is set
echo $ENTITLEMENT_TOKEN
```

### "Catalog resource not found" (timing issue)

**Cause**: Entra ID replication delay (security groups → entitlement management)

**Solution**: Deployment already includes retry logic with exponential backoff. If still fails, wait 30 seconds and redeploy.

## 🤝 Contributing

Found an issue or want to add a sample? See the main repo README for contribution guidelines.

## 📄 License

See LICENSE file in repository root.
