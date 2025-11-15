# Basic Catalog

The simplest entitlement management deployment: a catalog, access package, and policy.

## What This Deploys

- **1 Catalog**: Container for access packages
- **1 Access Package**: Grants access to resources (empty in this example)
- **1 Assignment Policy**: Defines who can request access

## Prerequisites

- Microsoft Graph API token with `EntitlementManagement.ReadWrite.All` permission
- Published extension in `../entitlementmgmt-ext/`

## Get Your Token

```bash
# Run the token script (stores token in ENTITLEMENT_TOKEN env var)
python3 Scripts/get_access_token.py
```

## Deploy

```bash
# From this directory
bicep local-deploy main.bicepparam
```

## What You'll See

```
✓ catalog (0.7s)
✓ accessPackage (0.5s)
✓ policyId (1.2s)
```

## Next Steps

- **Add resources**: See `02-security-groups/` to add groups to your access package
- **Approval workflows**: See `04-approval-workflows/` for manager/peer approval
- **PIM JIT access**: See `03-pim-jit-access/` for time-limited activation

## Clean Up

Currently no delete operation. Remove resources in Azure Portal:
**Entra ID** → **Identity Governance** → **Entitlement Management** → **Catalogs**
