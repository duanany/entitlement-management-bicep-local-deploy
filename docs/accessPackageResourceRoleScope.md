# accessPackageResourceRoleScope

Add specific resource roles (like "Member" or "Owner") from catalog resources to access packages.

## Example usage

### Adding "Member" role of a group to an access package

This example shows how to grant group membership when users get the access package.

```bicep
resource engineeringCatalog 'accessPackageCatalog' = {
  displayName: 'Engineering Resources'
}

resource devAccessPackage 'accessPackage' = {
  displayName: 'Developer Access'
  catalogId: engineeringCatalog.id
}

// Create security group
resource devGroup 'securityGroup' = {
  uniqueName: 'bicep-dev-group'
  displayName: 'Developer Group'
}

// Add group to catalog
resource devGroupInCatalog 'accessPackageCatalogResource' = {
  catalogId: engineeringCatalog.id
  originId: devGroup.id
  originSystem: 'AadGroup'
}

// Add "Member" role to access package (grants group membership!)
resource memberRoleScope 'accessPackageResourceRoleScope' = {
  accessPackageId: devAccessPackage.id
  resourceOriginId: devGroup.id
  roleOriginId: 'Member_${devGroup.id}'  // ✅ Member role format
  roleDisplayName: 'Member'
  catalogResourceId: devGroupInCatalog.id
}

output roleScopeId string = memberRoleScope.id
```

### Adding "Owner" role of a group to an access package

This example shows how to grant group ownership (for admin access packages).

```bicep
// Add "Owner" role to access package (grants group ownership!)
resource ownerRoleScope 'accessPackageResourceRoleScope' = {
  accessPackageId: adminAccessPackage.id
  resourceOriginId: devGroup.id
  roleOriginId: 'Owner_${devGroup.id}'  // ✅ Owner role format
  roleDisplayName: 'Owner'
  catalogResourceId: devGroupInCatalog.id
}
```

### Adding multiple resource roles to one access package

This example shows how to grant multiple group memberships in one access package.

```bicep
resource devAccessPackage 'accessPackage' = {
  displayName: 'Full Developer Access'
  catalogId: catalog.id
}

// Create multiple groups
resource githubGroup 'securityGroup' = {
  uniqueName: 'bicep-github-devs'
  displayName: 'GitHub Developers'
}

resource azureGroup 'securityGroup' = {
  uniqueName: 'bicep-azure-devs'
  displayName: 'Azure Developers'
}

resource jiraGroup 'securityGroup' = {
  uniqueName: 'bicep-jira-devs'
  displayName: 'Jira Developers'
}

// Add all groups to catalog
resource githubInCatalog 'accessPackageCatalogResource' = {
  catalogId: catalog.id
  originId: githubGroup.id
  originSystem: 'AadGroup'
}

resource azureInCatalog 'accessPackageCatalogResource' = {
  catalogId: catalog.id
  originId: azureGroup.id
  originSystem: 'AadGroup'
}

resource jiraInCatalog 'accessPackageCatalogResource' = {
  catalogId: catalog.id
  originId: jiraGroup.id
  originSystem: 'AadGroup'
}

// Add all "Member" roles to access package
resource githubMemberRole 'accessPackageResourceRoleScope' = {
  accessPackageId: devAccessPackage.id
  resourceOriginId: githubGroup.id
  roleOriginId: 'Member_${githubGroup.id}'
  roleDisplayName: 'Member'
  catalogResourceId: githubInCatalog.id
}

resource azureMemberRole 'accessPackageResourceRoleScope' = {
  accessPackageId: devAccessPackage.id
  resourceOriginId: azureGroup.id
  roleOriginId: 'Member_${azureGroup.id}'
  roleDisplayName: 'Member'
  catalogResourceId: azureInCatalog.id
}

resource jiraMemberRole 'accessPackageResourceRoleScope' = {
  accessPackageId: devAccessPackage.id
  resourceOriginId: jiraGroup.id
  roleOriginId: 'Member_${jiraGroup.id}'
  roleDisplayName: 'Member'
  catalogResourceId: jiraInCatalog.id
}

// ✅ Result: Users assigned this access package get added to all 3 groups!
```

### Adding application roles to an access package

This example shows how to grant application role assignments (for enterprise apps).

```bicep
param salesforceAppId string = '<salesforce-enterprise-app-id>'

resource salesforceInCatalog 'accessPackageCatalogResource' = {
  catalogId: catalog.id
  originId: salesforceAppId
  originSystem: 'AadApplication'
  displayName: 'Salesforce'
}

// Grant "Sales User" role in Salesforce
resource salesforceUserRole 'accessPackageResourceRoleScope' = {
  accessPackageId: salesAccessPackage.id
  resourceOriginId: salesforceAppId
  roleOriginId: '<salesforce-sales-user-role-id>'  // Get from app manifest
  roleDisplayName: 'Sales User'
  catalogResourceId: salesforceInCatalog.id
}
```

## Argument reference

The following arguments are available:

- `accessPackageId` - **(Required, Immutable)** The ID of the access package to add the role to. Get from `accessPackage.id`.

- `resourceOriginId` - **(Required, Immutable)** The Object ID of the resource (group, app, site). **Same as `originId` in `accessPackageCatalogResource`**.

- `roleOriginId` - **(Required, Immutable)** The role identifier. **Format depends on resource type**:
  - **For Groups**: `Member_<group-id>` or `Owner_<group-id>`
  - **For Apps**: App-specific role ID (from app manifest)
  - **For SharePoint**: Site role ID

- `roleDisplayName` - **(Required, Immutable)** Display name of the role. Common values:
  - `'Member'` - Group membership
  - `'Owner'` - Group ownership
  - Custom app role names (e.g., `'Sales User'`, `'Administrator'`)

- `catalogResourceId` - **(Required, Immutable)** The ID of the catalog resource. Get from `accessPackageCatalogResource.id`.

## Attribute reference

In addition to all arguments above, the following attributes are outputted:

- `id` - **[OUTPUT]** The unique identifier (GUID) of the resource role scope
- `accessPackageId` - **[OUTPUT]** The access package ID (same as input)

## Notes

### 🔄 Idempotency

The handler implements idempotency by:

1. Querying for existing role scope by `accessPackageId` + `resourceOriginId` + `roleOriginId`
2. If exists → returns existing (no-op)
3. If not exists → creates via POST

**Example**:

```bash
# First run: Adds role to access package
$ bicep local-deploy main.bicepparam
✅ Added Member role to access package

# Second run: Returns existing (no-op)
$ bicep local-deploy main.bicepparam
✅ Found existing resource role scope (no changes)
```

### 🔒 Security and Permissions

This resource requires the **`entitlementToken`** with:

- **EntitlementManagement.ReadWrite.All**: Required to add resource roles to access packages

```bicep
extension entitlementmgmt with {
  entitlementToken: parEntitlementToken  // ✅ For resource roles!
}
```

### 📦 Resource Role Lifecycle - Complete Flow

```
1. Create Resource (Group/App)
   ↓
2. Add to Catalog (accessPackageCatalogResource)
   ↓
3. Add Role to Access Package (THIS RESOURCE!)
   ↓
4. Create Policy
   ↓
5. Assign Users
```

**Complete Flow Example**:

```bicep
// Step 1: Create group
resource devGroup 'securityGroup' = {
  uniqueName: 'bicep-devs'
  displayName: 'Developers'
}

// Step 2: Add to catalog
resource groupInCatalog 'accessPackageCatalogResource' = {
  catalogId: catalog.id
  originId: devGroup.id
  originSystem: 'AadGroup'
}

// Step 3: Add "Member" role to access package (THIS RESOURCE!)
resource memberRole 'accessPackageResourceRoleScope' = {
  accessPackageId: package.id
  resourceOriginId: devGroup.id
  roleOriginId: 'Member_${devGroup.id}'
  roleDisplayName: 'Member'
  catalogResourceId: groupInCatalog.id
}

// Step 4: Create policy
resource policy 'accessPackageAssignmentPolicy' = {
  displayName: 'Direct Assignment'
  accessPackageId: package.id
}

// Step 5: Assign user
resource assignment 'accessPackageAssignment' = {
  accessPackageId: package.id
  assignmentPolicyId: policy.id
  targetUserId: '<user-id>'
}

// ✅ User gets added to "Developers" group!
```

### 🎯 Role ID Format Reference

#### For Entra ID Groups:

```bicep
// Member role (most common - grants group membership)
roleOriginId: 'Member_${groupId}'
roleDisplayName: 'Member'

// Owner role (grants group ownership)
roleOriginId: 'Owner_${groupId}'
roleDisplayName: 'Owner'
```

**How to get Group ID**:
```bash
# Get group Object ID
az ad group show --group "Developer Group" --query id -o tsv
```

#### For Entra ID Applications:

```bicep
// App-specific role ID (from app manifest)
roleOriginId: '<role-id-from-app-manifest>'
roleDisplayName: '<role-display-name>'
```

**How to get App Role ID**:
```bash
# Get enterprise app Object ID
az ad sp show --id <app-id> --query id -o tsv

# Get app roles (from app manifest)
az ad sp show --id <app-id> --query appRoles -o json
```

**Example App Roles**:
- Salesforce: `'Sales User'`, `'Marketing User'`, `'Admin'`
- Custom SAML apps: Check app manifest for role definitions

#### For SharePoint Sites:

```bicep
// SharePoint role (e.g., Member, Owner, Visitor)
roleOriginId: '<site-role-id>'
roleDisplayName: '<role-name>'
```

### 🎯 Best Practices

1. **Use "Member" Role for Standard Access**: Most common use case
   ```bicep
   resource memberRole 'accessPackageResourceRoleScope' = {
     roleOriginId: 'Member_${group.id}'  // ✅ Standard membership
     roleDisplayName: 'Member'
   }
   ```

2. **Use "Owner" Role Only for Admin Packages**: Grants full control
   ```bicep
   resource ownerRole 'accessPackageResourceRoleScope' = {
     roleOriginId: 'Owner_${group.id}'  // ⚠️ Admin only!
     roleDisplayName: 'Owner'
   }
   ```

3. **Add Multiple Roles to One Package for "Bundle" Access**:
   ```bicep
   // ✅ Good: One package grants multiple group memberships
   resource role1 'accessPackageResourceRoleScope' = {
     roleOriginId: 'Member_${githubGroup.id}'
   }
   resource role2 'accessPackageResourceRoleScope' = {
     roleOriginId: 'Member_${azureGroup.id}'
   }
   resource role3 'accessPackageResourceRoleScope' = {
     roleOriginId: 'Member_${jiraGroup.id}'
   }
   // ✅ Result: One assignment = 3 group memberships!
   ```

4. **Document What Each Role Grants**:
   ```bicep
   // ✅ Good: Clear description
   resource role 'accessPackageResourceRoleScope' = {
     resourceOriginId: devGroup.id
     roleOriginId: 'Member_${devGroup.id}'
     roleDisplayName: 'Member'  // Grants: GitHub org access, Azure dev subscription contributor
   }
   ```

### ⚠️ Common Mistakes

#### Mistake 1: Wrong Role ID Format

```bicep
// ❌ Wrong: Missing "Member_" prefix
roleOriginId: devGroup.id  // Invalid!

// ✅ Correct: Include role prefix
roleOriginId: 'Member_${devGroup.id}'
```

#### Mistake 2: Resource Not in Catalog

```bicep
// ❌ Wrong: Adding role before adding resource to catalog
resource role 'accessPackageResourceRoleScope' = {
  catalogResourceId: groupInCatalog.id  // Error: Doesn't exist!
}

// ✅ Correct: Add to catalog first
resource groupInCatalog 'accessPackageCatalogResource' = {
  catalogId: catalog.id
  originId: group.id
}

resource role 'accessPackageResourceRoleScope' = {
  catalogResourceId: groupInCatalog.id  // ✅ Works!
}
```

#### Mistake 3: roleOriginId != resourceOriginId

```bicep
// ❌ Wrong: Mismatched group IDs
resource role 'accessPackageResourceRoleScope' = {
  resourceOriginId: groupA.id
  roleOriginId: 'Member_${groupB.id}'  // ❌ Different group!
}

// ✅ Correct: Same group ID
resource role 'accessPackageResourceRoleScope' = {
  resourceOriginId: groupA.id
  roleOriginId: 'Member_${groupA.id}'  // ✅ Same group!
}
```

## Troubleshooting

### Error: "Resource not found in catalog"

**Cause**: The resource hasn't been added to the catalog yet.

**Solution**: Add the resource to the catalog first:

```bicep
// 1. Add to catalog
resource groupInCatalog 'accessPackageCatalogResource' = {
  catalogId: catalog.id
  originId: group.id
  originSystem: 'AadGroup'
}

// 2. Then add role
resource memberRole 'accessPackageResourceRoleScope' = {
  catalogResourceId: groupInCatalog.id  // ✅ Resource exists in catalog!
}
```

### Error: "Invalid roleOriginId format"

**Cause**: The `roleOriginId` doesn't match the expected format.

**Solution**: Use correct format for resource type:

```bicep
// ✅ For groups: "Member_<group-id>" or "Owner_<group-id>"
roleOriginId: 'Member_${group.id}'

// ❌ Wrong formats:
roleOriginId: group.id  // Missing role prefix
roleOriginId: 'member_${group.id}'  // Lowercase "member"
roleOriginId: '${group.id}_Member'  // Wrong order
```

### Role added but users don't get access

**Cause**: The resource itself doesn't grant access (e.g., group has no permissions).

**Solution**: Verify the underlying resource grants access:

```bash
# Check group exists and has members
az ad group show --group <group-id>

# Check group has permissions (e.g., Azure RBAC roles, app assignments)
az role assignment list --assignee <group-id>
```

### Error: "catalogResourceId not found"

**Cause**: The `catalogResourceId` doesn't exist or references wrong resource.

**Solution**: Use the catalog resource ID directly:

```bicep
resource groupInCatalog 'accessPackageCatalogResource' = {
  catalogId: catalog.id
  originId: group.id
}

resource memberRole 'accessPackageResourceRoleScope' = {
  catalogResourceId: groupInCatalog.id  // ✅ Direct reference!
}
```

### How to find app role IDs?

**Solution**: Use Azure CLI to get app roles:

```bash
# Get enterprise app service principal
az ad sp show --id <app-id> --query appRoles -o json

# Example output:
# [
#   {
#     "id": "e7a73d5f-1234-5678-90ab-cdef12345678",
#     "displayName": "Sales User",
#     "value": "SalesUser"
#   }
# ]

# Use the "id" field as roleOriginId
roleOriginId: 'e7a73d5f-1234-5678-90ab-cdef12345678'
```

## Additional reference

For more information, see the following links:

- [Microsoft Graph Resource Role Scope API][00]
- [Add Resources to Access Package][01]
- [Access Package Resources][02]

<!-- Link reference definitions -->
[00]: https://learn.microsoft.com/en-us/graph/api/resources/accesspackageresourcerolescope?view=graph-rest-1.0
[01]: https://learn.microsoft.com/en-us/graph/api/accesspackage-post-accesspackageresourcerolescopes?view=graph-rest-1.0
[02]: https://learn.microsoft.com/en-us/azure/active-directory/governance/entitlement-management-access-package-resources
