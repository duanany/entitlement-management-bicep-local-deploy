# Sample Deployments

Production-ready Bicep templates demonstrating Azure Entitlement Management patterns.

## 📁 Folder Structure

```
Sample/
├── 01-basic-catalog/          # Simplest deployment: catalog + access package + policy
├── 02-security-groups/         # Security group → catalog → access package workflow
├── 03-pim-jit-access/          # PIM Just-In-Time activation (UNIQUE VALUE! ⭐)
├── 04-approval-workflows/      # 4 approval patterns: manager, user, group, multi-stage
├── entitlementmgmt-ext/        # Published extension binaries (auto-generated)
├── pim-policy-template.json    # PIM activation policy template
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
| **01-basic-catalog** | Minimal setup: catalog + package + policy | Entitlement only | ~3s |
| **02-security-groups** | Create group + add to access package | Both tokens | ~30s |
| **03-pim-jit-access** ⭐ | PIM eligibility + JIT activation | Both tokens | ~60s |
| **04-approval-workflows** | 4 approval patterns (manager, user, group, 2-stage) | Entitlement only | ~8s |

### 4. Deploy

```bash
# Navigate to sample folder
cd 01-basic-catalog

# Edit parameters (optional)
code main.bicepparam

# Deploy
bicep local-deploy main.bicepparam
```

## 📖 Sample Details

### 01-basic-catalog

**Simplest deployment** - understand the core entitlement management concepts.

**What you'll learn**:
- How catalogs organize access packages
- How policies define who can request access
- Basic deployment workflow

**Resources created**: 3
**Deploy time**: ~3 seconds

[View README](./01-basic-catalog/README.md)

---

### 02-security-groups

**Complete workflow** - create a security group and manage membership via access packages.

**What you'll learn**:
- Adding groups as catalog resources
- Resource roles (Member role assignment)
- Linking groups to access packages
- Why this extension vs. Microsoft Graph Bicep

**Resources created**: 6
**Deploy time**: ~30 seconds

[View README](./02-security-groups/README.md)

---

### 03-pim-jit-access ⭐

**UNIQUE VALUE!** Microsoft Graph Bicep does **NOT** have `groupPimEligibility` resource.

**What you'll learn**:
- Privileged Identity Management (PIM) for groups
- Just-In-Time (JIT) access activation
- Time-limited group membership (2-hour max)
- Peer approval workflows
- Why this is the ONLY IaC solution for PIM eligibility

**Resources created**: 8
**Deploy time**: ~60 seconds (pimEligibility takes ~46s)

[View README](./03-pim-jit-access/README.md)

---

### 04-approval-workflows

**4 approval patterns** - manager, specific user, group peer, two-stage.

**What you'll learn**:
- `requestorManager` approver type (manager approval)
- `singleUser` approver type (designated approver)
- `groupMembers` approver type (peer approval + reviews)
- Multi-stage approval (Serial mode)
- When to use each pattern

**Resources created**: 9
**Deploy time**: ~8 seconds

[View README](./04-approval-workflows/README.md)

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
| `GROUP_USER_TOKEN` | `Group.ReadWrite.All`<br/>`User.Read.All` | Security groups, PIM eligibility |

## 🎯 Learning Path

**Beginner** → **Intermediate** → **Advanced**

```
01-basic-catalog
    ↓
02-security-groups
    ↓
04-approval-workflows
    ↓
03-pim-jit-access ⭐
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
