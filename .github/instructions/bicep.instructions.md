---
applyTo: "**/*.bicep,**/*.bicepparam"
description: "Bicep local-deploy usage patterns for Entitlement Management extension"
---

# Bicep Local-Deploy Rules for Entitlement Management

## Core Principles
- Always set `targetScope = 'local'` and declare `extension entitlementmgmt`.
- Keep templates minimal; pass values via `param` blocks and `.bicepparam` files.
- **Never** output or log secrets (API tokens, user GUIDs). Avoid linter suppressions for secret outputs.
- Use `@secure()` decorator for all token parameters.
- Use `readEnvironmentVariable()` in `.bicepparam` files for tokens - NEVER hardcode credentials.

## Token Acquisition Workflow (CRITICAL - Run Before Every Deployment)

**ALWAYS acquire a fresh token before running `bicep local-deploy`:**

```bash
# Step 1: Navigate to repo root and get fresh Graph API token
cd "$(git rev-parse --show-toplevel)"
python3 Scripts/get_access_token.py

# Step 2: Token is auto-copied to clipboard - export to environment variables
export GRAPH_TOKEN=$(pbpaste)
export ENTITLEMENT_TOKEN=$GRAPH_TOKEN
export GROUP_USER_TOKEN=$GRAPH_TOKEN

# Step 3: Verify token is set (should show "eyJ...")
echo "Token set: ${ENTITLEMENT_TOKEN:0:50}..."

# Step 4: NOW you can deploy (from any sample directory)
cd sample/01-catalog-basic
bicep local-deploy main.bicepparam
```

> **Note**: `Scripts/get_access_token.py` launches an interactive browser window via MSAL. This step must be executed by a signed-in user on a device that can open the Entra ID login page; it cannot be automated by headless agents.

**Token validity**: ~60-90 minutes. If deployment fails with 401/403, re-run token acquisition.

**Required scopes**:
- `EntitlementManagement.ReadWrite.All`
- `Group.ReadWrite.All` (for samples with groups/PIM)

**IMPORTANT**: Always use relative paths from repo root. Never hardcode absolute paths or usernames.

## Token Management
- **Two-token architecture** supports least privilege:
  - `entitlementToken`: For catalogs, packages, policies, assignments
  - `groupUserToken`: For security groups, PIM, user operations
- If you have all permissions, use the same token for both parameters.
- **Never commit `.bicepparam` files with real tokens** - use environment variables!

## Resource Dependency Patterns

### Pattern 1: Basic Catalog → Package → Policy
```bicep
targetScope = 'local'

extension entitlementmgmt with {
  entitlementToken: entitlementToken
}

@secure()
param entitlementToken string

// Step 1: Create catalog (foundation)
resource catalog 'accessPackageCatalog' = {
  displayName: 'Engineering Resources'
  description: 'Access packages for engineering team'
  isExternallyVisible: false
  catalogType: 'userManaged'
  state: 'published'
}

// Step 2: Create access package (depends on catalog)
resource accessPackage 'accessPackage' = {
  displayName: 'Developer Access'
  catalogId: catalog.id  // Reference catalog ID
  description: 'Standard developer access bundle'
  isHidden: false
}

// Step 3: Create assignment policy (depends on access package)
resource policy 'accessPackageAssignmentPolicy' = {
  displayName: 'All Users - Manager Approval'
  accessPackageId: accessPackage.id  // Reference package ID
  allowedTargetScope: 'AllMemberUsers'

  requestApprovalSettings: {
    isApprovalRequired: true
    approvalMode: 'SingleStage'
    approvalStages: [
      {
        approvalStageTimeOutInDays: 14
        primaryApprovers: [
          {
            oDataType: '#microsoft.graph.requestorManager'
            managerLevel: 1
          }
        ]
      }
    ]
  }

  durationInDays: 90
  canExtend: true
}

output catalogId string = catalog.id
output accessPackageId string = accessPackage.id
output policyId string = policy.id
```

### Pattern 2: PIM Eligibility (⭐ UNIQUE!)
```bicep
targetScope = 'local'

extension entitlementmgmt with {
  entitlementToken: entitlementToken
  groupUserToken: groupUserToken
}

@secure()
param entitlementToken string
@secure()
param groupUserToken string
param testUserId string

// Step 1: Create eligible group (who CAN activate)
resource eligibleGroup 'securityGroup' = {
  uniqueName: 'pim-eligible-developers'
  displayName: 'PIM Eligible Developers'
  members: [testUserId]
}

// Step 2: Create activated group (JIT membership)
resource activatedGroup 'securityGroup' = {
  uniqueName: 'pim-activated-developers'
  displayName: 'PIM Activated Developers'
  members: []  // PIM controls this
}

// Step 3: Configure PIM eligibility
resource pimEligibility 'groupPimEligibility' = {
  eligibleGroupUniqueName: eligibleGroup.uniqueName
  activatedGroupUniqueName: activatedGroup.uniqueName
  accessId: 'member'
  maxActivationDuration: 'PT2H'
  expirationDateTime: '2026-12-31T23:59:59Z'
  policyTemplateJson: loadTextContent('../pim-policy.json')
}
```

## Best Practices

- Use descriptive `displayName` - used for idempotency
- Follow dependency flow: Catalog → Package → Policy → Assignment
- Extension handles Entra ID replication delays automatically
- `catalogResource` takes ~22s, `pimEligibility` ~47s
- Use `loadTextContent()` for complex JSON policies

## Common Pitfalls

❌ Don't hardcode tokens
✅ Use `readEnvironmentVariable('TOKEN_NAME')`

❌ Don't use this extension's `securityGroup` for production
✅ Use microsoft.graph/groups@1.0.0

✅ DO use `groupPimEligibility` - ONLY IaC solution for PIM!

---

**This extension = Infrastructure as Code. For ad-hoc ops, use Azure Portal!** 🚀
