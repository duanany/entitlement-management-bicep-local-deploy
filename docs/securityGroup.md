# securityGroup

Create and manage Entra ID security groups with idempotency based on `mailNickname`.

## Example usage

### Creating a basic security group

This example shows how to create a basic security group for team access.

```bicep
resource engineeringTeam 'securityGroup' = {
  uniqueName: 'engineering-team'  // Immutable identifier (mailNickname)
  displayName: 'Engineering Team'
  description: 'All engineering team members'
}

output groupId string = engineeringTeam.id
output groupName string = engineeringTeam.displayName
```

### Creating multiple security groups

This example shows how to create multiple security groups for different teams.

```bicep
resource platformTeam 'securityGroup' = {
  uniqueName: 'platform-admins'
  displayName: 'Platform Administrators'
  description: 'Platform team with admin access'
}

resource devopsTeam 'securityGroup' = {
  uniqueName: 'devops-engineers'
  displayName: 'DevOps Engineers'
  description: 'DevOps team members'
}

resource dataTeam 'securityGroup' = {
  uniqueName: 'data-analytics'
  displayName: 'Data Analytics Team'
  description: 'Data analysts and scientists'
}
```

### Using group ID in other resources

This example shows how to use the created group in catalog resources.

```bicep
// Create the group
resource engineeringGroup 'securityGroup' = {
  uniqueName: 'engineering-team'
  displayName: 'Engineering Team'
  description: 'All engineering team members'
}

// Create a catalog
resource engineeringCatalog 'accessPackageCatalog' = {
  displayName: 'Engineering Resources'
  description: 'Access packages for engineering team'
  isExternallyVisible: false
}

// Add the group to the catalog
resource catalogGroupResource 'accessPackageCatalogResource' = {
  catalogId: engineeringCatalog.id
  originId: engineeringGroup.id  // ✅ Use the group ID!
  originSystem: 'AadGroup'
  displayName: engineeringGroup.displayName
}
```

## Argument reference

The following arguments are available:

- `uniqueName` - **(Required, Immutable)** Unique mail nickname for the group (becomes `mailNickname` in Graph API). **Cannot be changed after creation**. Must be unique across the entire tenant. Examples: `platform-admins`, `engineering-team-2024`, `data-analytics-prod`

- `displayName` - **(Required, Mutable)** Display name of the security group. Can be updated after creation. This is what users see in Entra ID.

- `description` - **(Optional, Mutable)** Description of the security group's purpose. Can be updated after creation.

- `mailEnabled` - **(Optional)** Whether the group is mail-enabled. **For security groups, this should always be `false`** (default). Only change this if you know what you're doing.

- `securityEnabled` - **(Optional)** Whether the group is security-enabled. **For security groups, this should always be `true`** (default). Only change this if you know what you're doing.

## Attribute reference

In addition to all arguments above, the following attributes are outputted:

- `id` - **[OUTPUT]** The unique identifier (GUID) of the security group in Entra ID
- `createdDateTime` - **[OUTPUT]** The date/time when the security group was created (ISO 8601 format)
- `uniqueName` - **[OUTPUT]** The mail nickname of the group (same as input)
- `displayName` - **[OUTPUT]** The display name of the group (same as input or updated value)

## Notes

### ⚠️ Important: uniqueName is IMMUTABLE!

The `uniqueName` property maps to `mailNickname` in Microsoft Graph API, which **cannot be changed** after group creation. If you need to change the mail nickname:

1. Delete the existing group (manual operation in Azure Portal)
2. Change the `uniqueName` in your Bicep template
3. Redeploy to create a new group

**Idempotency**: The handler uses `uniqueName` (mailNickname) as the unique identifier. Running the deployment multiple times with the same `uniqueName` will:
- ✅ **First run**: Create the group
- ✅ **Second run**: Find existing group, return same ID (no-op if properties match)
- ✅ **Third run with changed `displayName`**: Update the group with new display name

### 🔒 Security and Permissions

This resource requires the **`groupUserToken`** (not `entitlementToken`) with the following permissions:

- **Group.ReadWrite.All**: Required to create, read, update, and delete security groups
- **User.Read.All**: Optional, but recommended for bulk assignment scenarios

```bicep
extension entitlementmgmt with {
  entitlementToken: parEntitlementToken    // For catalogs, packages, policies
  groupUserToken: parGroupUserToken        // ✅ For security groups!
}
```

### 🎯 Best Practices

1. **Use Descriptive uniqueName**: Choose names that are easy to identify:
   - ✅ Good: `platform-admins-prod`, `engineering-team-us`
   - ❌ Bad: `group1`, `temp`, `test123`

2. **Include Environment in Name**: For multi-environment deployments:
   ```bicep
   resource prodAdmins 'securityGroup' = {
     uniqueName: 'platform-admins-${environment}'  // e.g., platform-admins-prod
     displayName: 'Platform Administrators (${toUpper(environment)})'
   }
   ```

3. **Document Group Purpose**: Always include a meaningful description:
   ```bicep
   resource devTeam 'securityGroup' = {
     uniqueName: 'developers-${projectName}'
     displayName: 'Developers - ${projectName}'
     description: 'Development team for ${projectName} project. Grants access to dev resources and repos.'
   }
   ```

4. **Don't Change mailEnabled/securityEnabled**: The defaults are correct for security groups. Only change if you're creating a different type of group.

### 🔄 Idempotency Example

```bash
# First deployment: Creates group
$ bicep local-deploy main.bicepparam
✅ Created security group: ID=a8a9a0e0-6b99-42b2-b36c-c8cc4cc5f752

# Second deployment (no changes): Returns existing group (no-op)
$ bicep local-deploy main.bicepparam
✅ Found existing group: ID=a8a9a0e0-6b99-42b2-b36c-c8cc4cc5f752
✅ No changes needed

# Change displayName in Bicep, deploy again: Updates group
$ vim main.bicep  # Change displayName from "Engineering Team" to "Engineering Team (US)"
$ bicep local-deploy main.bicepparam
✅ Updated group: ID=a8a9a0e0-6b99-42b2-b36c-c8cc4cc5f752
✅ Display name changed to "Engineering Team (US)"
```

## Troubleshooting

### Error: "A conflicting object with one or more of the specified property values is present"

**Cause**: The `uniqueName` (mailNickname) already exists in your tenant.

**Solution**: Change the `uniqueName` to something unique:
```bicep
// Before (conflict)
resource myGroup 'securityGroup' = {
  uniqueName: 'admins'  // ❌ Too common, likely already exists
  displayName: 'My Admins'
}

// After (unique)
resource myGroup 'securityGroup' = {
  uniqueName: 'my-project-admins-2025'  // ✅ Unique!
  displayName: 'My Admins'
}
```

### Error: "403 Forbidden" when creating group

**Cause**: The `groupUserToken` doesn't have `Group.ReadWrite.All` permission.

**Solution**:
1. Verify permission is granted:
   ```bash
   az ad app permission list --id <group-appId>
   ```

2. Grant permission if missing:
   ```bash
   # Add Group.ReadWrite.All permission
   az ad app permission add --id <group-appId> \
     --api 00000003-0000-0000-c000-000000000000 \
     --api-permissions 62a82d76-70ea-41e2-9197-370581804d09=Role

   # Grant admin consent
   az ad app permission admin-consent --id <group-appId>
   ```

3. Get fresh token:
   ```bash
   mgc login --scopes Group.ReadWrite.All User.Read.All --strategy DeviceCode
   export GROUP_USER_TOKEN=$(mgc auth token)
   ```

### Error: "Group not found" after creation

**Cause**: Entra ID replication delay (rare, but can happen in global deployments).

**Solution**: Wait 5-10 seconds and retry the deployment. The handler will find the existing group.

## Additional reference

For more information, see the following links:

- [Microsoft Graph Groups API][00]
- [Entra ID Security Groups][01]
- [Group Properties Reference][02]
- [mailNickname Property][03]

<!-- Link reference definitions -->
[00]: https://learn.microsoft.com/en-us/graph/api/resources/group?view=graph-rest-1.0
[01]: https://learn.microsoft.com/en-us/azure/active-directory/fundamentals/how-to-manage-groups
[02]: https://learn.microsoft.com/en-us/graph/api/group-post-groups?view=graph-rest-1.0
[03]: https://learn.microsoft.com/en-us/graph/api/resources/group?view=graph-rest-1.0#properties
