# Security Groups

Complete workflow: create a security group and manage its membership via access packages.

## What This Deploys

- **1 Security Group**: Entra ID security group with initial members
- **1 Catalog**: Container for the group resource
- **1 Catalog Resource**: Adds the group to the catalog
- **1 Access Package**: Grants group membership
- **1 Resource Role**: Assigns "Member" role of the group
- **1 Assignment Policy**: Defines who can request membership

## Flow Diagram

```
┌─────────────────┐
│ Security Group  │ (Created with initial members)
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│    Catalog      │ (Container for resources)
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Catalog Resource│ (Group added to catalog)
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Access Package  │ (Defines what users get)
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Resource Role   │ (Member role of the group)
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Assignment Policy│ (Who can request + approval rules)
└─────────────────┘
```

## Prerequisites

- Microsoft Graph API tokens:
  - `ENTITLEMENT_TOKEN`: `EntitlementManagement.ReadWrite.All`
  - `GROUP_USER_TOKEN`: `Group.ReadWrite.All` + `User.Read.All`
- Published extension in `../entitlementmgmt-ext/`

## Get Your Tokens

```bash
# Run the token script
python3 /Users/bregr00/Documents/PSScripts/get_token.py
```

## Edit Parameters

Update `main.bicepparam` with your user's Object ID:

```bash
# Get your user ID
az ad user show --id your.email@domain.com --query id -o tsv
```

## Deploy

```bash
# From this directory
bicep local-deploy main.bicepparam
```

## What You'll See

```
✓ demoSecurityGroup (1.5s)
✓ catalog (0.7s)
✓ catalogResourceSecurityGroup (22.6s)  ← Longest operation
✓ securityGroupAccessPackage (1.4s)
✓ securityGroupResourceRole (2.7s)
✓ securityGroupAccessPolicy (0.9s)
```

## Why Use This vs. Microsoft Graph Bicep?

**Microsoft Graph Bicep Extension** (`az/microsoft-graph@1.0.0`) already provides `securityGroup` resource!

**This extension adds value by**:
- **All-in-one testing**: Quickly test full entitlement workflows
- **Integrated samples**: Security groups + access packages in one deployment
- **Learning tool**: Understand the full entitlement management flow

**For production**:
- Use Microsoft Graph Bicep for security groups
- Use this extension for entitlement management resources only
- Combine them in your infrastructure-as-code

## Next Steps

- **PIM JIT access**: See `03-pim-jit-access/` for time-limited group membership
- **Approval workflows**: See `04-approval-workflows/` for manager/peer approval before granting access

## Clean Up

Remove resources in Azure Portal:
- **Entra ID** → **Groups** → Delete the security group
- **Entra ID** → **Identity Governance** → **Entitlement Management** → Delete catalog
