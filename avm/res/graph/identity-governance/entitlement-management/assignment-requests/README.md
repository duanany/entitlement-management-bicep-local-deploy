# Access Package Assignment Request

This module creates an Entra ID Access Package Assignment by submitting an assignment request. This assigns an access package to a user, granting them the roles and permissions defined in the access package.

## Navigation

- [Resource Types](#resource-types)
- [Usage examples](#usage-examples)
- [Parameters](#parameters)
- [Outputs](#outputs)

## Resource Types

| Resource Type | API Version |
| :-- | :-- |
| `accessPackageAssignment` | Local |

## Usage examples

The following section provides usage examples for the module, which were used to validate and deploy the module successfully. For a full reference, please review the module's test folder in the repository.

>**Note**: Each example lists all the required parameters first, followed by the rest - each in alphabetical order.

>**Note**: To reference the module, please use the following syntax `'res/graph/identity-governance/entitlement-management/assignment-requests/main.bicep'`.

### Example 1: _Assign to Internal User_

This example assigns an access package to an internal user by user ID.

<details>

<summary>via Bicep module</summary>

```bicep
module assignment 'res/graph/identity-governance/entitlement-management/assignment-requests/main.bicep' = {
  name: 'assignmentDeployment'
  params: {
    entitlementToken: '<entitlementToken>'
    accessPackageName: 'Developer Access'
    catalogName: 'Engineering Resources'
    assignmentPolicyName: 'All Users - No Approval'
    targetUserId: '<user-guid>'
  }
}
```

</details>

<details>

<summary>via Bicep parameters file</summary>

```bicep
using 'res/graph/identity-governance/entitlement-management/assignment-requests/main.bicep'

param entitlementToken = '<entitlementToken>'
param accessPackageName = 'Developer Access'
param catalogName = 'Engineering Resources'
param assignmentPolicyName = 'All Users - No Approval'
param targetUserId = '<user-guid>'
```

</details>

### Example 2: _Assign to External User_

This example assigns an access package to an external user by email.

<details>

<summary>via Bicep module</summary>

```bicep
module assignment 'res/graph/identity-governance/entitlement-management/assignment-requests/main.bicep' = {
  name: 'assignmentDeployment'
  params: {
    entitlementToken: '<entitlementToken>'
    accessPackageName: 'Contractor Access'
    catalogName: 'External Resources'
    assignmentPolicyName: 'External Users - Manager Approval'
    targetUserEmail: 'contractor@external.com'
    justification: 'Contractor requires access for Q1 project work'
  }
}
```

</details>

### Example 3: _Time-Bound Assignment_

This example assigns an access package with a specific start and end date.

<details>

<summary>via Bicep module</summary>

```bicep
module assignment 'res/graph/identity-governance/entitlement-management/assignment-requests/main.bicep' = {
  name: 'assignmentDeployment'
  params: {
    entitlementToken: '<entitlementToken>'
    accessPackageName: 'Project Access'
    catalogName: 'Project Resources'
    assignmentPolicyName: 'Time-Bound Policy'
    targetUserId: '<user-guid>'
    justification: 'Project team member for Q1 initiative'
    schedule: {
      startDateTime: '2025-01-01T00:00:00Z'
      expiration: {
        endDateTime: '2025-03-31T23:59:59Z'
        type: 'afterDateTime'
      }
    }
  }
}
```

</details>

### Example 4: _Assignment with Duration_

This example assigns an access package for a specific number of days.

<details>

<summary>via Bicep module</summary>

```bicep
module assignment 'res/graph/identity-governance/entitlement-management/assignment-requests/main.bicep' = {
  name: 'assignmentDeployment'
  params: {
    entitlementToken: '<entitlementToken>'
    accessPackageName: 'Temporary Admin Access'
    catalogName: 'Admin Resources'
    assignmentPolicyName: 'Admin Policy'
    targetUserId: '<user-guid>'
    justification: 'Emergency access for system maintenance'
    schedule: {
      startDateTime: '2025-01-15T09:00:00Z'
      expiration: {
        duration: 'P7D' // 7 days in ISO 8601 duration format
        type: 'afterDuration'
      }
    }
  }
}
```

</details>

## Parameters

**Required parameters**

| Parameter | Type | Description |
| :-- | :-- | :-- |
| [`accessPackageName`](#parameter-accesspackagename) | string | The name of the access package. |
| [`assignmentPolicyName`](#parameter-assignmentpolicyname) | string | The name of the assignment policy. |
| [`catalogName`](#parameter-catalogname) | string | The name of the catalog. |
| [`entitlementToken`](#parameter-entitlementtoken) | securestring | Entitlement Management API token (Graph API token with EntitlementManagement.ReadWrite.All permission) |

**Optional parameters**

| Parameter | Type | Description |
| :-- | :-- | :-- |
| [`justification`](#parameter-justification) | string | Justification for the assignment request. |
| [`schedule`](#parameter-schedule) | object | Schedule for time-bound access. |
| [`targetUserEmail`](#parameter-targetuseremail) | string | The email of the user to assign (for external users). |
| [`targetUserId`](#parameter-targetuserid) | string | The object ID (GUID) of the user to assign. |

### Parameter: `accessPackageName`

The name of the access package.

- Required: Yes
- Type: string

### Parameter: `assignmentPolicyName`

The name of the assignment policy.

- Required: Yes
- Type: string

### Parameter: `catalogName`

The name of the catalog.

- Required: Yes
- Type: string

### Parameter: `entitlementToken`

Entitlement Management API token (Graph API token with EntitlementManagement.ReadWrite.All permission)

- Required: Yes
- Type: securestring

### Parameter: `justification`

Justification for the assignment request.

- Required: No
- Type: string

### Parameter: `schedule`

Schedule for time-bound access.

- Required: No
- Type: object

**Required parameters**

| Parameter | Type | Description |
| :-- | :-- | :-- |
| [`startDateTime`](#parameter-schedulestartdatetime) | string | Start date/time for the assignment (UTC ISO 8601 format). |

**Optional parameters**

| Parameter | Type | Description |
| :-- | :-- | :-- |
| [`expiration`](#parameter-scheduleexpiration) | object | Expiration configuration for the assignment. |

### Parameter: `schedule.startDateTime`

Start date/time for the assignment (UTC ISO 8601 format).

- Required: Yes
- Type: string
- Example: `'2025-01-01T00:00:00Z'`

### Parameter: `schedule.expiration`

Expiration configuration for the assignment.

- Required: No
- Type: object

**Required parameters**

| Parameter | Type | Description |
| :-- | :-- | :-- |
| [`type`](#parameter-scheduleexpirationtype) | string | Type of expiration. |

**Optional parameters**

| Parameter | Type | Description |
| :-- | :-- | :-- |
| [`duration`](#parameter-scheduleexpirationduration) | string | Duration in ISO 8601 format (e.g., 'P30D' for 30 days). |
| [`endDateTime`](#parameter-scheduleexpirationenddatetime) | string | Specific end date/time (UTC ISO 8601 format). |

### Parameter: `schedule.expiration.type`

Type of expiration.

- Required: Yes
- Type: string
- Allowed:
  ```Bicep
  [
    'afterDateTime'
    'afterDuration'
    'noExpiration'
  ]
  ```

### Parameter: `schedule.expiration.duration`

Duration in ISO 8601 format (e.g., 'P30D' for 30 days).

- Required: No (required when `type` is `afterDuration`)
- Type: string
- Examples:
  - `'P7D'` - 7 days
  - `'P30D'` - 30 days
  - `'P90D'` - 90 days
  - `'P1Y'` - 1 year

### Parameter: `schedule.expiration.endDateTime`

Specific end date/time (UTC ISO 8601 format).

- Required: No (required when `type` is `afterDateTime`)
- Type: string
- Example: `'2025-12-31T23:59:59Z'`

### Parameter: `targetUserEmail`

The email of the user to assign (for external users).

- Required: No (either `targetUserId` or `targetUserEmail` must be specified)
- Type: string

### Parameter: `targetUserId`

The object ID (GUID) of the user to assign.

- Required: No (either `targetUserId` or `targetUserEmail` must be specified)
- Type: string

## Outputs

| Output | Type | Description |
| :-- | :-- | :-- |
| `accessPackageId` | string | The ID of the access package. |
| `assignmentPolicyId` | string | The ID of the assignment policy. |
| `resourceId` | string | The ID of the created assignment. |
| `state` | string | The state of the assignment. |
| `targetUserId` | string | The object ID of the target user. |

## Notes

### User Identification

You must specify **either** `targetUserId` **or** `targetUserEmail`:

- **Internal Users**: Use `targetUserId` with the user's object ID (GUID)
- **External Users**: Use `targetUserEmail` with the user's email address
- **Do not specify both parameters**

### Schedule Configuration

#### No Expiration

For assignments without expiration:

```bicep
schedule: {
  startDateTime: '2025-01-01T00:00:00Z'
  expiration: {
    type: 'noExpiration'
  }
}
```

#### Fixed End Date

For assignments with a specific end date:

```bicep
schedule: {
  startDateTime: '2025-01-01T00:00:00Z'
  expiration: {
    type: 'afterDateTime'
    endDateTime: '2025-12-31T23:59:59Z'
  }
}
```

#### Duration-Based

For assignments with a duration from start:

```bicep
schedule: {
  startDateTime: '2025-01-01T00:00:00Z'
  expiration: {
    type: 'afterDuration'
    duration: 'P90D' // 90 days
  }
}
```

### ISO 8601 Duration Format

When using `afterDuration`, use ISO 8601 duration format:

- `P#D` - Days (e.g., `P7D` = 7 days)
- `P#W` - Weeks (e.g., `P4W` = 4 weeks)
- `P#M` - Months (e.g., `P6M` = 6 months)
- `P#Y` - Years (e.g., `P1Y` = 1 year)

You can combine units: `P1Y6M` = 1 year and 6 months

### Assignment States

The `state` output indicates the assignment status:

- **Delivered**: Assignment successfully processed, user has access
- **Delivering**: Assignment is being processed
- **DeliveryFailed**: Assignment failed (check permissions and configuration)
- **Denied**: Assignment request was denied
- **Scheduled**: Assignment is scheduled to start in the future

### Prerequisites

- An existing catalog (created via `catalogs/main.bicep`)
- An existing access package (created via `access-package/main.bicep`)
- An existing assignment policy (created via `assignment-policies/main.bicep`)
- Graph API token with `EntitlementManagement.ReadWrite.All` permission
- For internal users: User must exist in the tenant
- For external users: Tenant must be configured to allow external users

### Justification Requirements

Some assignment policies require justification. If a policy has `isRequestorJustificationRequired: true`, you **must** provide the `justification` parameter, or the assignment will fail.

### Approval Workflows

If the assignment policy requires approval:

1. The assignment will be created with state `Scheduled` or pending approval
2. Designated approvers will receive approval requests
3. The assignment state will update to `Delivered` after approval
4. If denied, the state will update to `Denied`

### Troubleshooting

**Assignment fails with "User not found":**
- Verify `targetUserId` is correct (must be object ID, not UPN)
- For external users, use `targetUserEmail` instead

**Assignment fails with "Policy not found":**
- Ensure the policy exists and is associated with the access package
- Verify `assignmentPolicyName` matches exactly (case-sensitive)

**Assignment stuck in "Delivering":**
- Check if approval is required (view assignment policy settings)
- Verify resource role scopes are correctly configured
- Check Azure AD logs for detailed error information

**"Justification required" error:**
- Add the `justification` parameter
- Ensure the justification is not empty
