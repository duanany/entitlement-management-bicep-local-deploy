# accessPackage

Define collections of resource roles that can be assigned to users through Entitlement Management.

## Example usage

### Creating a basic access package

This example shows how to create a simple access package in a catalog.

```bicep
resource engineeringCatalog 'accessPackageCatalog' = {
  displayName: 'Engineering Resources'
  description: 'Access packages for engineering team'
  isExternallyVisible: false
}

resource devAccessPackage 'accessPackage' = {
  displayName: 'Developer Access'
  catalogId: engineeringCatalog.id
  description: 'Standard developer access package - grants access to dev resources'
}

output accessPackageId string = devAccessPackage.id
```

### Creating multiple access packages in the same catalog

This example shows how to create different access packages for different roles.

```bicep
resource engineeringCatalog 'accessPackageCatalog' = {
  displayName: 'Engineering Resources'
  description: 'Access packages for engineering team'
}

resource seniorDevPackage 'accessPackage' = {
  displayName: 'Senior Developer Access'
  catalogId: engineeringCatalog.id
  description: 'Full access for senior developers - includes prod access'
  isHidden: false
}

resource juniorDevPackage 'accessPackage' = {
  displayName: 'Junior Developer Access'
  catalogId: engineeringCatalog.id
  description: 'Limited access for junior developers - dev/test only'
  isHidden: false
}

resource contractorPackage 'accessPackage' = {
  displayName: 'Contractor Access'
  catalogId: engineeringCatalog.id
  description: 'Temporary contractor access - no prod'
  isHidden: true  // Hidden from self-service catalog
}
```

### Creating access package for external users

This example shows how to create an access package visible to external partners.

```bicep
resource partnerCatalog 'accessPackageCatalog' = {
  displayName: 'Partner Resources'
  description: 'Access packages for external partners'
  isExternallyVisible: true  // ✅ Visible to external users!
}

resource partnerAccessPackage 'accessPackage' = {
  displayName: 'Partner Collaboration Access'
  catalogId: partnerCatalog.id
  description: 'Access package for external partners and vendors'
  isHidden: false
}
```

## Argument reference

The following arguments are available:

- `displayName` - **(Required, Mutable)** The display name of the access package. Can be updated after creation. Must be unique within the catalog.

- `catalogId` - **(Required, Immutable)** The ID of the catalog that will contain this access package. **Cannot be changed after creation**. Get this from the `accessPackageCatalog.id` property.

- `description` - **(Optional, Mutable)** Description of what this access package grants. Shown to requestors in the My Access portal.

- `isHidden` - **(Optional, Mutable)** Whether this access package is hidden from requestors in the My Access portal. Default: `false`
  - `false`: Users can see and request this package
  - `true`: Only admins can assign this package (direct assignment only)

## Attribute reference

In addition to all arguments above, the following attributes are outputted:

- `id` - **[OUTPUT]** The unique identifier (GUID) of the access package
- `displayName` - **[OUTPUT]** The display name (same as input or updated value)
- `catalogId` - **[OUTPUT]** The catalog ID (same as input)

## Notes

### 🔄 Idempotency

The handler implements idempotency by:

1. Querying for existing access package by `catalogId` + `displayName`
2. If exists with matching properties → returns existing (no-op)
3. If exists with different properties → updates via PATCH
4. If not exists → creates via POST

**Example**:

```bash
# First run: Creates access package
$ bicep local-deploy main.bicepparam
✅ Created access package: ID=8f483f48-5069-4d5e-8ef2-cdce17929e84

# Second run (no changes): Returns existing package (no-op)
$ bicep local-deploy main.bicepparam
✅ Found existing access package: ID=8f483f48-5069-4d5e-8ef2-cdce17929e84

# Change description, deploy again: Updates access package
$ bicep local-deploy main.bicepparam
✅ Updated access package description
```

### 🔒 Security and Permissions

This resource requires the **`entitlementToken`** with the following permission:

- **EntitlementManagement.ReadWrite.All**: Required to create, read, update, and delete access packages

```bicep
extension entitlementmgmt with {
  entitlementToken: parEntitlementToken    // ✅ For access packages!
  groupUserToken: parGroupUserToken        // For security groups only
}
```

### 📦 Access Package Lifecycle

An access package typically requires these steps:

1. **Create Catalog** → Container for access packages
2. **Create Access Package** → Empty package (this resource)
3. **Add Resources to Catalog** → Add groups/apps/sites to catalog
4. **Add Resource Roles to Package** → Add "Member" role of group to package
5. **Create Assignment Policy** → Define who can request and how
6. **Assign Users** → Direct assignment or user self-service

**Complete Example**:

```bicep
// 1. Create catalog
resource catalog 'accessPackageCatalog' = {
  displayName: 'Engineering Resources'
}

// 2. Create access package (this resource!)
resource accessPackage 'accessPackage' = {
  displayName: 'Developer Access'
  catalogId: catalog.id
}

// 3. Add group to catalog
resource catalogGroup 'accessPackageCatalogResource' = {
  catalogId: catalog.id
  originId: '<group-object-id>'
  originSystem: 'AadGroup'
}

// 4. Add "Member" role to package
resource memberRole 'accessPackageResourceRoleScope' = {
  accessPackageId: accessPackage.id
  resourceOriginId: '<group-object-id>'
  roleOriginId: 'Member_<group-object-id>'
  catalogResourceId: catalogGroup.id
}

// 5. Create policy
resource policy 'accessPackageAssignmentPolicy' = {
  displayName: 'Direct Assignment Only'
  accessPackageId: accessPackage.id
}

// 6. Assign user
resource assignment 'accessPackageAssignment' = {
  accessPackageId: accessPackage.id
  assignmentPolicyId: policy.id
  targetUserId: '<user-object-id>'
}
```

### 🎯 Best Practices

1. **Use Descriptive Names**: Access package names appear in My Access portal
   - ✅ Good: "Senior Developer Access", "Partner Collaboration Tools"
   - ❌ Bad: "Package 1", "Test", "Group Access"

2. **Write Clear Descriptions**: Users see this when requesting access
   ```bicep
   resource package 'accessPackage' = {
     displayName: 'Developer Access'
     description: 'Grants access to dev Azure subscriptions, GitHub repos, and Jira projects. Includes VPN and dev tool licenses.'
   }
   ```

3. **Use isHidden for Admin-Only Packages**: Hide sensitive packages from self-service
   ```bicep
   resource prodAdminPackage 'accessPackage' = {
     displayName: 'Production Administrator Access'
     catalogId: catalog.id
     description: 'Full production access - admin assignment only'
     isHidden: true  // ✅ Not visible in My Access
   }
   ```

4. **Group Related Packages in Same Catalog**: Keep organizational structure clear
   ```bicep
   // ✅ Good: Engineering packages in engineering catalog
   resource engCatalog 'accessPackageCatalog' = {
     displayName: 'Engineering Resources'
   }

   resource devPackage 'accessPackage' = {
     catalogId: engCatalog.id
   }

   resource qPackage 'accessPackage' = {
     catalogId: engCatalog.id
   }
   ```

## Troubleshooting

### Error: "Access package already exists with the same displayName"

**Cause**: Another access package with the same `displayName` exists in the same catalog.

**Solution**: Change the `displayName` to be unique within the catalog:

```bicep
// Before (conflict)
resource package1 'accessPackage' = {
  displayName: 'Developer Access'  // ❌ Already exists!
  catalogId: catalog.id
}

// After (unique)
resource package1 'accessPackage' = {
  displayName: 'Developer Access - US Region'  // ✅ Unique!
  catalogId: catalog.id
}
```

### Error: "403 Forbidden" when creating access package

**Cause**: The `entitlementToken` doesn't have `EntitlementManagement.ReadWrite.All` permission.

**Solution**: Verify and grant permission (see Prerequisites section in main README)

### Error: "Catalog not found"

**Cause**: The `catalogId` doesn't exist or references the wrong catalog.

**Solution**: Use the catalog resource ID directly:

```bicep
resource catalog 'accessPackageCatalog' = {
  displayName: 'My Catalog'
}

resource package 'accessPackage' = {
  catalogId: catalog.id  // ✅ Direct reference!
}
```

### Access package created but appears empty

**Cause**: You created the access package but haven't added resource roles yet.

**Solution**: Add resources to the catalog and then add resource roles to the package:

```bicep
// After creating access package, add resources:
resource catalogResource 'accessPackageCatalogResource' = {
  catalogId: catalog.id
  originId: '<group-id>'
  originSystem: 'AadGroup'
}

resource resourceRole 'accessPackageResourceRoleScope' = {
  accessPackageId: package.id
  resourceOriginId: '<group-id>'
  roleOriginId: 'Member_<group-id>'
  catalogResourceId: catalogResource.id
}
```

## Additional reference

For more information, see the following links:

- [Microsoft Graph Access Package API][00]
- [Entitlement Management Overview][01]
- [Create Access Packages Tutorial][02]

<!-- Link reference definitions -->
[00]: https://learn.microsoft.com/en-us/graph/api/resources/accesspackage?view=graph-rest-1.0
[01]: https://learn.microsoft.com/en-us/azure/active-directory/governance/entitlement-management-overview
[02]: https://learn.microsoft.com/en-us/graph/tutorial-access-package-api
