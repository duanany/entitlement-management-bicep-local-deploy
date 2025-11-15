# accessPackageCatalog

Create and manage catalogs in Azure Entitlement Management. Catalogs are containers for access packages and their associated resources.

## Example usage

### Creating a basic catalog

This example shows how to create a simple internal catalog for engineering resources.

```bicep
resource engineeringCatalog 'accessPackageCatalog' = {
  displayName: 'Engineering Resources'
  description: 'Access packages for engineering team - GitHub, Azure, Jira'
  isExternallyVisible: false  // Internal only
}

output catalogId string = engineeringCatalog.id
```

### Creating a catalog for external partners

This example shows how to create a catalog visible to external users and partners.

```bicep
resource partnerCatalog 'accessPackageCatalog' = {
  displayName: 'Partner Resources'
  description: 'Access packages for external partners and vendors'
  isExternallyVisible: true  // ✅ Visible to external users!
}

output partnerCatalogId string = partnerCatalog.id
```

### Creating multiple catalogs for different teams

This example shows how to organize resources by department or team.

```bicep
resource engineeringCatalog 'accessPackageCatalog' = {
  displayName: 'Engineering Resources'
  description: 'Developer tools, Azure subscriptions, GitHub repos'
  isExternallyVisible: false
}

resource hrCatalog 'accessPackageCatalog' = {
  displayName: 'HR Resources'
  description: 'Workday, BambooHR, payroll systems'
  isExternallyVisible: false
}

resource salesCatalog 'accessPackageCatalog' = {
  displayName: 'Sales Resources'
  description: 'Salesforce, HubSpot, sales enablement tools'
  isExternallyVisible: false
}

output engineeringId string = engineeringCatalog.id
output hrId string = hrCatalog.id
output salesId string = salesCatalog.id
```

## Argument reference

The following arguments are available:

- `displayName` - **(Required, Mutable)** The display name of the catalog. **Used as unique identifier for idempotency**. Can be updated after creation.

- `description` - **(Optional, Mutable)** Description of the catalog's purpose. Helps administrators understand what resources belong in this catalog.

- `isExternallyVisible` - **(Optional, Mutable)** Whether access packages in this catalog are visible to external users (guests). Default: `false`
  - `false`: Only internal organization users can see access packages
  - `true`: External users from connected organizations can see and request access packages

- `catalogType` - **(Optional, Immutable)** Type of catalog. Default: `'UserManaged'`
  - `'UserManaged'`: Standard catalog managed by administrators
  - `'ServiceDefault'`: Built-in system catalog (rarely used)

- `state` - **(Optional, Mutable)** Catalog state. Default: `'Published'`
  - `'Published'`: Catalog is active and access packages are available
  - `'Unpublished'`: Catalog is hidden (access packages cannot be requested)

## Attribute reference

In addition to all arguments above, the following attributes are outputted:

- `id` - **[OUTPUT]** The unique identifier (GUID) of the catalog
- `displayName` - **[OUTPUT]** The display name (same as input or updated value)

## Notes

### 🔄 Idempotency

The handler implements idempotency by:

1. Querying for existing catalog by `displayName` using `$filter=displayName eq '{name}'`
2. If exists with matching properties → returns existing (no-op)
3. If exists with different properties → updates via PATCH
4. If not exists → creates via POST

**Example**:

```bash
# First run: Creates catalog
$ bicep local-deploy main.bicepparam
✅ Created catalog: ID=8f483f48-5069-4d5e-8ef2-cdce17929e84

# Second run (no changes): Returns existing catalog (no-op)
$ bicep local-deploy main.bicepparam
✅ Found existing catalog: ID=8f483f48-5069-4d5e-8ef2-cdce17929e84

# Change description, deploy again: Updates catalog
$ bicep local-deploy main.bicepparam
✅ Updated catalog description
```

### 🔒 Security and Permissions

This resource requires the **`entitlementToken`** with the following permission:

- **EntitlementManagement.ReadWrite.All**: Required to create, read, update, and delete catalogs

```bicep
extension entitlementmgmt with {
  entitlementToken: parEntitlementToken    // ✅ For catalogs!
  groupUserToken: parGroupUserToken        // For security groups only
}
```

**How to get tokens**: See the [Authentication](../README.md#authentication) section in the main README for detailed steps using Microsoft Graph CLI (`mgc`).

### 📦 Catalog Lifecycle - Foundation Resource

Catalogs are the **foundation** of Entitlement Management. They must be created first before other resources:

```
1. Create Catalog (THIS RESOURCE!)
   ↓
2. Create Access Package (in catalog)
   ↓
3. Add Resources to Catalog (groups, apps, sites)
   ↓
4. Add Resource Roles to Access Package
   ↓
5. Create Assignment Policy
   ↓
6. Assign Users
```

**Complete Flow Example**:

```bicep
// Step 1: Create catalog (THIS RESOURCE!)
resource catalog 'accessPackageCatalog' = {
  displayName: 'Engineering Resources'
}

// Step 2: Create access package
resource package 'accessPackage' = {
  displayName: 'Developer Access'
  catalogId: catalog.id  // ✅ Depends on catalog!
}

// Step 3: Add group to catalog
resource catalogResource 'accessPackageCatalogResource' = {
  catalogId: catalog.id
  originId: '<group-id>'
  originSystem: 'AadGroup'
}

// Step 4: Add role to access package
resource roleScope 'accessPackageResourceRoleScope' = {
  accessPackageId: package.id
  catalogResourceId: catalogResource.id
}

// Step 5: Create policy
resource policy 'accessPackageAssignmentPolicy' = {
  displayName: 'Direct Assignment'
  accessPackageId: package.id
}

// Step 6: Assign user
resource assignment 'accessPackageAssignment' = {
  accessPackageId: package.id
  assignmentPolicyId: policy.id
  targetUserId: '<user-id>'
}
```

### 🎯 Best Practices

1. **Use Descriptive Names**: Catalog names appear in My Access portal
   - ✅ Good: "Engineering Resources", "Partner Access Packages"
   - ❌ Bad: "Catalog 1", "Test", "MyPackages"

2. **Organize by Department or Function**: Keep resources logically grouped
   ```bicep
   // ✅ Good: Separate catalogs for different teams
   resource engCatalog 'accessPackageCatalog' = {
     displayName: 'Engineering Resources'
   }

   resource hrCatalog 'accessPackageCatalog' = {
     displayName: 'HR Resources'
   }
   ```

3. **Set isExternallyVisible for Partner Catalogs**: Enable external user access
   ```bicep
   resource partnerCatalog 'accessPackageCatalog' = {
     displayName: 'Partner Resources'
     isExternallyVisible: true  // ✅ Allows partner requests!
   }
   ```

4. **Write Clear Descriptions**: Help administrators understand catalog purpose
   ```bicep
   resource catalog 'accessPackageCatalog' = {
     displayName: 'Engineering Resources'
     description: 'Contains access packages for engineering team: GitHub repos, Azure subscriptions, dev tools, Jira projects'
   }
   ```

5. **Use Environment-Specific Names**: Avoid conflicts across environments
   ```bicep
   param environment string = 'dev'  // dev, staging, prod

   resource catalog 'accessPackageCatalog' = {
     displayName: 'Engineering Resources - ${environment}'
   }
   ```

## Troubleshooting

### Error: "Catalog already exists with the same displayName"

**Cause**: Another catalog with the same `displayName` already exists.

**Solution**: The handler automatically handles this via idempotency. If you see this error, it means:
- If properties match → Returns existing catalog (no-op)
- If properties differ → Updates the catalog

No action needed! This is expected behavior.

### Error: "403 Forbidden" when creating catalog

**Cause**: The `entitlementToken` doesn't have `EntitlementManagement.ReadWrite.All` permission.

**Solution**:

1. Verify permission is granted to the service principal:
   ```bash
   # Get fresh token with mgc
   mgc login --scopes EntitlementManagement.ReadWrite.All
   mgc token
   ```

2. Verify admin consent was granted (not just permission added):
   ```bash
   az ad app permission admin-consent --id <app-id>
   ```

3. Wait a few minutes for permission propagation

### Catalog created but access packages not visible

**Cause**: Catalog state is `'Unpublished'` or `isExternallyVisible` is incorrectly set.

**Solution**: Verify catalog settings:

```bicep
resource catalog 'accessPackageCatalog' = {
  displayName: 'My Catalog'
  state: 'Published'  // ✅ Must be Published!
  isExternallyVisible: true  // ✅ If external users need access
}
```

### How to delete a catalog?

**Note**: Catalog deletion is not yet implemented. To remove a catalog:

1. **Option 1**: Delete all access packages in the catalog first (via Azure Portal)
2. **Option 2**: Set state to `'Unpublished'` to hide it
3. **Option 3**: Use Azure Portal or Microsoft Graph API directly

## Additional reference

For more information, see the following links:

- [Microsoft Graph Access Package Catalog API][00]
- [Entitlement Management Catalogs][01]
- [Create and Manage Catalogs][02]

<!-- Link reference definitions -->
[00]: https://learn.microsoft.com/en-us/graph/api/resources/accesspackagecatalog?view=graph-rest-1.0
[01]: https://learn.microsoft.com/en-us/azure/active-directory/governance/entitlement-management-catalog-create
[02]: https://learn.microsoft.com/en-us/graph/api/entitlementmanagement-post-catalogs?view=graph-rest-1.0
