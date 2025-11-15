# accessPackageCatalogResource

Add resources (groups, apps, SharePoint sites) to a catalog so they can be included in access packages.

## Example usage

### Adding a security group to a catalog

This example shows how to add an Entra ID security group to a catalog.

```bicep
resource engineeringCatalog 'accessPackageCatalog' = {
  displayName: 'Engineering Resources'
  description: 'Resources for engineering team'
}

// Create security group
resource devGroup 'securityGroup' = {
  uniqueName: 'bicep-dev-group'
  displayName: 'Developer Group'
  description: 'Developers with repo and Azure access'
}

// Add group to catalog
resource devGroupInCatalog 'accessPackageCatalogResource' = {
  catalogId: engineeringCatalog.id
  originId: devGroup.id  // Group Object ID
  originSystem: 'AadGroup'  // ✅ Entra ID Group
  displayName: devGroup.displayName
  description: devGroup.description
}

output catalogResourceId string = devGroupInCatalog.id
```

### Adding multiple resources to a catalog

This example shows how to add different types of resources to the same catalog.

```bicep
resource hrCatalog 'accessPackageCatalog' = {
  displayName: 'HR Resources'
}

// Add HR group
resource hrGroup 'securityGroup' = {
  uniqueName: 'bicep-hr-group'
  displayName: 'HR Team'
}

resource hrGroupInCatalog 'accessPackageCatalogResource' = {
  catalogId: hrCatalog.id
  originId: hrGroup.id
  originSystem: 'AadGroup'
  displayName: 'HR Team Group'
}

// Add Workday application
resource workdayApp 'accessPackageCatalogResource' = {
  catalogId: hrCatalog.id
  originId: '<workday-app-object-id>'
  originSystem: 'AadApplication'  // ✅ Entra ID Application
  displayName: 'Workday HR System'
  description: 'Workday SAML application'
}

// Add SharePoint HR site
resource hrSharePoint 'accessPackageCatalogResource' = {
  catalogId: hrCatalog.id
  originId: '<sharepoint-site-id>'
  originSystem: 'SharePointOnline'  // ✅ SharePoint Site
  displayName: 'HR SharePoint Site'
  description: 'HR team document library'
}
```

### Adding existing Entra ID resources

This example shows how to add existing Entra ID groups and apps using their Object IDs.

```bicep
param existingGroupId string = '7a72c098-a42d-489f-a3fa-c2445dec6f9c'
param existingAppId string = '550e8400-e29b-41d4-a716-446655440000'

resource catalog 'accessPackageCatalog' = {
  displayName: 'Enterprise Apps'
}

resource existingGroup 'accessPackageCatalogResource' = {
  catalogId: catalog.id
  originId: existingGroupId  // ✅ Existing group Object ID
  originSystem: 'AadGroup'
  displayName: 'Existing Sales Team Group'
  description: 'Pre-existing group managed outside Bicep'
}

resource existingApp 'accessPackageCatalogResource' = {
  catalogId: catalog.id
  originId: existingAppId  // ✅ Existing app Object ID
  originSystem: 'AadApplication'
  displayName: 'Salesforce Enterprise App'
}
```

## Argument reference

The following arguments are available:

- `catalogId` - **(Required, Immutable)** The ID of the catalog to add the resource to. Get from `accessPackageCatalog.id`.

- `originId` - **(Required, Immutable)** The Object ID (GUID) of the resource in its origin system:
  - For `AadGroup`: Group Object ID (get from `securityGroup.id` or `az ad group show`)
  - For `AadApplication`: Enterprise Application Object ID (NOT App ID)
  - For `SharePointOnline`: SharePoint site ID

- `originSystem` - **(Required, Immutable)** The type of resource. Options:
  - `'AadGroup'`: Entra ID Security Group
  - `'AadApplication'`: Entra ID Enterprise Application (SAML, OAuth)
  - `'SharePointOnline'`: SharePoint Online site

- `displayName` - **(Optional, Mutable)** Display name shown in catalog. Defaults to resource's name.

- `description` - **(Optional, Mutable)** Description of what this resource grants access to.

## Attribute reference

In addition to all arguments above, the following attributes are outputted:

- `id` - **[OUTPUT]** The unique identifier (GUID) of the catalog resource
- `catalogId` - **[OUTPUT]** The catalog ID (same as input)
- `originId` - **[OUTPUT]** The origin resource ID (same as input)
- `originSystem` - **[OUTPUT]** The origin system (same as input)

## Notes

### 🔄 Idempotency

The handler implements idempotency by:

1. Querying for existing resource by `catalogId` + `originId` + `originSystem`
2. If exists with matching properties → returns existing (no-op)
3. If exists with different properties → updates displayName/description
4. If not exists → creates via POST

**Example**:

```bash
# First run: Adds group to catalog
$ bicep local-deploy main.bicepparam
✅ Added group to catalog: ID=8f483f48-5069-4d5e-8ef2-cdce17929e84

# Second run: Returns existing (no-op)
$ bicep local-deploy main.bicepparam
✅ Found existing catalog resource

# Change description, deploy again: Updates resource
$ bicep local-deploy main.bicepparam
✅ Updated catalog resource description
```

### 🔒 Security and Permissions

This resource requires the **`entitlementToken`** with:

- **EntitlementManagement.ReadWrite.All**: Required to add resources to catalogs

```bicep
extension entitlementmgmt with {
  entitlementToken: parEntitlementToken  // ✅ For catalog resources!
}
```

### 📦 Resource Lifecycle - How Resources Flow

```
1. Create Resource (Group/App/Site)
   ↓
2. Add to Catalog (this resource!)
   ↓
3. Add to Access Package (accessPackageResourceRoleScope)
   ↓
4. Assign to Users
```

**Complete Flow Example**:

```bicep
// Step 1: Create group
resource devGroup 'securityGroup' = {
  uniqueName: 'bicep-devs'
  displayName: 'Developers'
}

// Step 2: Add to catalog (THIS RESOURCE!)
resource groupInCatalog 'accessPackageCatalogResource' = {
  catalogId: catalog.id
  originId: devGroup.id
  originSystem: 'AadGroup'
}

// Step 3: Add "Member" role to access package
resource memberRole 'accessPackageResourceRoleScope' = {
  accessPackageId: package.id
  resourceOriginId: devGroup.id
  roleOriginId: 'Member_${devGroup.id}'
  catalogResourceId: groupInCatalog.id  // ✅ Links to catalog resource!
}

// Step 4: Assign to users
resource assignment 'accessPackageAssignment' = {
  accessPackageId: package.id
  targetUserId: '<user-id>'
}
```

### 🎯 Best Practices

1. **Add Resources to Catalog Before Adding to Access Packages**: Catalog must contain resource first!

2. **Use Descriptive Names**: Help admins understand what resource grants
   ```bicep
   resource groupInCatalog 'accessPackageCatalogResource' = {
     displayName: 'Developer Group - Grants GitHub & Azure Access'
     description: 'Membership grants access to eng-dev GitHub org and dev Azure subscriptions'
   }
   ```

3. **Verify originId Before Deployment**: Ensure resource exists in Entra ID
   ```bash
   # Verify group exists
   az ad group show --group <group-id>

   # Verify app exists (enterprise app, not app registration!)
   az ad sp show --id <app-id>
   ```

4. **Keep Catalog Organized**: Group related resources in same catalog
   ```bicep
   // ✅ Good: Engineering resources in engineering catalog
   resource engCatalog 'accessPackageCatalog' = {
     displayName: 'Engineering Resources'
   }

   resource devGroup 'accessPackageCatalogResource' = {
     catalogId: engCatalog.id
     originSystem: 'AadGroup'
   }

   resource cicdApp 'accessPackageCatalogResource' = {
     catalogId: engCatalog.id
     originSystem: 'AadApplication'
   }
   ```

### 🔍 How to Find Origin IDs

**For Groups**:
```bash
# Get group Object ID by display name
az ad group show --group "Developer Group" --query id -o tsv

# List all groups
az ad group list --query "[].{name:displayName, id:id}" -o table
```

**For Enterprise Applications**:
```bash
# Get app Object ID (service principal ID, NOT app registration ID!)
az ad sp list --display-name "Salesforce" --query "[0].id" -o tsv

# List all enterprise apps
az ad sp list --query "[].{name:displayName, id:id}" -o table
```

**For SharePoint Sites**:
```bash
# Use Microsoft Graph API
# GET https://graph.microsoft.com/v1.0/sites/{site-id}
```

## Troubleshooting

### Error: "Resource not found in origin system"

**Cause**: The `originId` doesn't exist in Entra ID or SharePoint.

**Solution**: Verify the resource exists:

```bash
# For groups
az ad group show --group <origin-id>

# For apps
az ad sp show --id <origin-id>
```

### Error: "Catalog not found"

**Cause**: The `catalogId` doesn't exist.

**Solution**: Ensure catalog is created first:

```bicep
resource catalog 'accessPackageCatalog' = {
  displayName: 'My Catalog'
}

resource resource 'accessPackageCatalogResource' = {
  catalogId: catalog.id  // ✅ Direct reference!
}
```

### Error: "Invalid originSystem value"

**Cause**: `originSystem` has incorrect value.

**Solution**: Use exact values (case-sensitive):

```bicep
// ✅ Correct
originSystem: 'AadGroup'
originSystem: 'AadApplication'
originSystem: 'SharePointOnline'

// ❌ Wrong
originSystem: 'Group'  // Invalid!
originSystem: 'aadgroup'  // Case matters!
```

### Resource added to catalog but can't be added to access package

**Cause**: Missing resource role scope (need to add role, not just resource).

**Solution**: After adding to catalog, add resource role to package:

```bicep
// 1. Add to catalog (this resource)
resource groupInCatalog 'accessPackageCatalogResource' = {
  catalogId: catalog.id
  originId: group.id
  originSystem: 'AadGroup'
}

// 2. Add role to access package (required!)
resource memberRole 'accessPackageResourceRoleScope' = {
  accessPackageId: package.id
  resourceOriginId: group.id
  roleOriginId: 'Member_${group.id}'
  catalogResourceId: groupInCatalog.id  // ✅ Links catalog resource!
}
```

### Error: "403 Forbidden" when adding resource to catalog

**Cause**: Token doesn't have permissions or resource already exists in another catalog.

**Solution**:

1. Verify `entitlementToken` has `EntitlementManagement.ReadWrite.All`
2. Check if resource is already in another catalog (resources can only be in one catalog at a time)
3. Remove from other catalog first, then add to new catalog

## Additional reference

For more information, see the following links:

- [Microsoft Graph Catalog Resource API][00]
- [Add Resources to Catalog][01]
- [Manage Catalog Resources][02]

<!-- Link reference definitions -->
[00]: https://learn.microsoft.com/en-us/graph/api/resources/accesspackagecatalogresource?view=graph-rest-1.0
[01]: https://learn.microsoft.com/en-us/graph/api/entitlementmanagement-post-accesspackageresources?view=graph-rest-1.0
[02]: https://learn.microsoft.com/en-us/azure/active-directory/governance/entitlement-management-catalog-create
