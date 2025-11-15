# accessPackageAssignmentPolicy

Define who can request an access package, approval requirements, and access duration.

## Example usage

### Direct assignment policy (admin-only)

This example shows how to create a policy for admin-only assignments with no expiration.

```bicep
resource engineeringCatalog 'accessPackageCatalog' = {
  displayName: 'Engineering Resources'
}

resource devAccessPackage 'accessPackage' = {
  displayName: 'Developer Access'
  catalogId: engineeringCatalog.id
}

resource directAssignmentPolicy 'accessPackageAssignmentPolicy' = {
  displayName: 'Direct Assignment Only'
  description: 'Admin assigns users directly - no self-service'
  accessPackageId: devAccessPackage.id
  allowedTargetScope: 'NotSpecified'  // Admin-only
  expiration: {
    type: 'noExpiration'  // No expiration
  }
}
```

### Self-service policy with approval

This example shows how to enable user self-service requests with manager approval.

```bicep
resource selfServicePolicy 'accessPackageAssignmentPolicy' = {
  displayName: 'Manager Approval Required'
  description: 'Users can request access with manager approval'
  accessPackageId: devAccessPackage.id
  allowedTargetScope: 'AllExistingDirectoryMemberUsers'  // All internal users

  expiration: {
    type: 'afterDuration'
    duration: 'P90D'  // 90 days
  }

  requestApprovalSettings: {
    isApprovalRequiredForAdd: true
    isApprovalRequiredForUpdate: false
    stages: [
      {
        durationBeforeAutomaticDenial: 'P14D'  // Auto-deny after 14 days
        isApproverJustificationRequired: true
        isEscalationEnabled: false
        durationBeforeEscalation: 'P0D'
        primaryApprovers: [
          {
            '@odata.type': '#microsoft.graph.requestorManager'
            managerLevel: 1  // Direct manager
          }
        ]
        fallbackPrimaryApprovers: [
          {
            '@odata.type': '#microsoft.graph.singleUser'
            userId: '<fallback-admin-user-id>'  // If no manager
          }
        ]
      }
    ]
  }

  requestorSettings: {
    allowCustomAssignmentSchedule: false
    enableTargetsToSelfAddAccess: true
    enableTargetsToSelfUpdateAccess: false
    enableTargetsToSelfRemoveAccess: true
  }
}
```

### Policy for specific users or groups

This example shows how to restrict access requests to specific users or groups.

```bicep
resource specificUsersPolicy 'accessPackageAssignmentPolicy' = {
  displayName: 'Engineering Team Only'
  description: 'Only engineering team members can request'
  accessPackageId: devAccessPackage.id
  allowedTargetScope: 'SpecificDirectorySubjects'  // Specific users/groups!

  specificAllowedTargets: [
    {
      '@odata.type': '#microsoft.graph.groupMembers'
      groupId: '<engineering-group-id>'  // Only members of this group
      description: 'Engineering Team'
    }
  ]

  expiration: {
    type: 'afterDuration'
    duration: 'P30D'  // 30 days
  }

  requestApprovalSettings: {
    isApprovalRequiredForAdd: false  // Auto-approved for engineering team
  }
}
```

### Time-limited contractor policy

This example shows how to create a policy for contractors with automatic 30-day expiration.

```bicep
resource contractorPolicy 'accessPackageAssignmentPolicy' = {
  displayName: 'Contractor Access - 30 Days'
  description: 'Temporary contractor access with automatic cleanup'
  accessPackageId: devAccessPackage.id
  allowedTargetScope: 'NotSpecified'  // Admin assigns contractors

  expiration: {
    type: 'afterDuration'
    duration: 'P30D'  // Auto-expires after 30 days
  }
}
```

## Argument reference

The following arguments are available:

- `displayName` - **(Required, Mutable)** Display name of the policy. Can be updated.

- `description` - **(Optional, Mutable)** Description of the policy purpose.

- `accessPackageId` - **(Required, Immutable)** The ID of the access package this policy applies to. Get from `accessPackage.id`.

- `allowedTargetScope` - **(Required, Mutable)** Who can request access. Options:
  - `'NotSpecified'`: Admin-only assignments (no self-service)
  - `'AllExistingDirectoryMemberUsers'`: All internal users can request
  - `'AllExistingDirectorySubjects'`: All users (internal + guests)
  - `'SpecificDirectorySubjects'`: Specific users/groups (requires `specificAllowedTargets`)
  - `'AllConfiguredConnectedOrganizationSubjects'`: All connected orgs
  - `'AllExistingConnectedOrganizationSubjects'`: Existing connected org users
  - `'SpecificConnectedOrganizationSubjects'`: Specific connected org users

- `specificAllowedTargets` - **(Optional, Mutable)** Array of allowed subjects when `allowedTargetScope` is `'SpecificDirectorySubjects'`. Each entry contains:
  - `'@odata.type'`: `'#microsoft.graph.groupMembers'` | `'#microsoft.graph.singleUser'`
  - `groupId` or `userId`: Object ID of group or user
  - `description`: Optional description

- `expiration` - **(Optional, Mutable)** How long assignments last:
  - `type`: `'noExpiration'` | `'afterDuration'` | `'afterDateTime'`
  - `duration`: ISO 8601 duration (e.g., `'P90D'` = 90 days) if `type` is `'afterDuration'`
  - `endDateTime`: End date/time if `type` is `'afterDateTime'`

- `requestApprovalSettings` - **(Optional, Mutable)** Approval configuration:
  - `isApprovalRequiredForAdd`: Require approval for new requests?
  - `isApprovalRequiredForUpdate`: Require approval for updates?
  - `stages`: Array of approval stages (see example above)

- `requestorSettings` - **(Optional, Mutable)** Requestor behavior:
  - `enableTargetsToSelfAddAccess`: Users can request access?
  - `enableTargetsToSelfUpdateAccess`: Users can extend access?
  - `enableTargetsToSelfRemoveAccess`: Users can remove their own access?
  - `allowCustomAssignmentSchedule`: Users can specify custom start/end dates?

## Attribute reference

In addition to all arguments above, the following attributes are outputted:

- `id` - **[OUTPUT]** The unique identifier (GUID) of the policy
- `accessPackageId` - **[OUTPUT]** The access package ID (same as input)

## Notes

### 🔒 Security and Permissions

This resource requires the **`entitlementToken`** with:

- **EntitlementManagement.ReadWrite.All**: Required to create/manage policies

```bicep
extension entitlementmgmt with {
  entitlementToken: parEntitlementToken  // ✅ For policies!
}
```

### 🎯 Common Policy Patterns

#### Pattern 1: Admin-Only (No Self-Service)

```bicep
resource adminOnlyPolicy 'accessPackageAssignmentPolicy' = {
  displayName: 'Admin Assignment Only'
  accessPackageId: package.id
  allowedTargetScope: 'NotSpecified'  // ✅ Admin-only!
  expiration: { type: 'noExpiration' }
}
```

#### Pattern 2: Self-Service with No Approval

```bicep
resource autoApprovalPolicy 'accessPackageAssignmentPolicy' = {
  displayName: 'Auto-Approved for All'
  accessPackageId: package.id
  allowedTargetScope: 'AllExistingDirectoryMemberUsers'
  expiration: { type: 'afterDuration', duration: 'P90D' }
  requestApprovalSettings: {
    isApprovalRequiredForAdd: false  // ✅ Auto-approved!
  }
}
```

#### Pattern 3: Manager Approval Required

```bicep
resource managerApprovalPolicy 'accessPackageAssignmentPolicy' = {
  displayName: 'Manager Approval'
  accessPackageId: package.id
  allowedTargetScope: 'AllExistingDirectoryMemberUsers'
  requestApprovalSettings: {
    isApprovalRequiredForAdd: true
    stages: [{
      primaryApprovers: [{
        '@odata.type': '#microsoft.graph.requestorManager'
        managerLevel: 1
      }]
    }]
  }
}
```

#### Pattern 4: Specific Group Only

```bicep
resource groupOnlyPolicy 'accessPackageAssignmentPolicy' = {
  displayName: 'Engineering Team Only'
  accessPackageId: package.id
  allowedTargetScope: 'SpecificDirectorySubjects'
  specificAllowedTargets: [{
    '@odata.type': '#microsoft.graph.groupMembers'
    groupId: '<group-id>'
  }]
}
```

### 🎯 Best Practices

1. **Use Descriptive Names**: Policy names appear in My Access portal
2. **Set Expiration for Temporary Access**: Use `afterDuration` for contractors
3. **Enable Self-Removal**: Allow users to remove their own access when done
4. **Use Fallback Approvers**: Handle cases where user has no manager
5. **Document Approval Stages**: Explain approval workflow in description

## Troubleshooting

### Error: "Policy already exists with same displayName"

**Cause**: Another policy with the same `displayName` exists for this access package.

**Solution**: Change the `displayName` to be unique:

```bicep
resource policy1 'accessPackageAssignmentPolicy' = {
  displayName: 'Direct Assignment - Full-Time Employees'  // ✅ Unique!
}
```

### Error: "specificAllowedTargets required when allowedTargetScope is SpecificDirectorySubjects"

**Cause**: You set `allowedTargetScope: 'SpecificDirectorySubjects'` but didn't provide `specificAllowedTargets`.

**Solution**: Add the allowed subjects:

```bicep
resource policy 'accessPackageAssignmentPolicy' = {
  allowedTargetScope: 'SpecificDirectorySubjects'
  specificAllowedTargets: [{
    '@odata.type': '#microsoft.graph.groupMembers'
    groupId: '<group-id>'
  }]
}
```

### Error: "Invalid expiration duration format"

**Cause**: Duration is not in ISO 8601 format.

**Solution**: Use ISO 8601 duration format:

```bicep
// ✅ Correct
expiration: {
  type: 'afterDuration'
  duration: 'P90D'  // 90 days
}

// ❌ Wrong
expiration: {
  type: 'afterDuration'
  duration: '90'  // Invalid!
}
```

**Common ISO 8601 Durations**:
- `'P1D'` = 1 day
- `'P7D'` = 7 days
- `'P30D'` = 30 days
- `'P90D'` = 90 days
- `'P180D'` = 180 days
- `'P365D'` = 365 days

### Policy created but users can't request access

**Cause**: `allowedTargetScope` is `'NotSpecified'` (admin-only).

**Solution**: Change to allow self-service:

```bicep
resource policy 'accessPackageAssignmentPolicy' = {
  allowedTargetScope: 'AllExistingDirectoryMemberUsers'  // ✅ Users can request!
  requestorSettings: {
    enableTargetsToSelfAddAccess: true  // ✅ Enable self-service!
  }
}
```

## Additional reference

For more information, see the following links:

- [Microsoft Graph Assignment Policy API][00]
- [Entitlement Management Policies][01]
- [Configure Approval Settings][02]

<!-- Link reference definitions -->
[00]: https://learn.microsoft.com/en-us/graph/api/resources/accesspackageassignmentpolicy?view=graph-rest-1.0
[01]: https://learn.microsoft.com/en-us/azure/active-directory/governance/entitlement-management-access-package-assignment-policy
[02]: https://learn.microsoft.com/en-us/azure/active-directory/governance/entitlement-management-access-package-approval-policy
