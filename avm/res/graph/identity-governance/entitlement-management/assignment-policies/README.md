# Assignment Policies

This module deploys an Entra ID Access Package Assignment Policy.

## Navigation

- [Resource Types](#resource-types)
- [Usage examples](#usage-examples)
- [Parameters](#parameters)
- [Outputs](#outputs)

## Resource Types

| Resource Type | API Version |
| :-- | :-- |
| `accessPackageAssignmentPolicy` | Local |

## Usage examples

The following section provides usage examples for the module, which were used to validate and deploy the module successfully. For a full reference, please review the module's test folder in the repository.

>**Note**: Each example lists all the required parameters first, followed by the rest - each in alphabetical order.

>**Note**: To reference the module, please use the following syntax `'res/graph/identity-governance/entitlement-management/assignment-policies/main.bicep'`.

### Example 1: _All Users - No Approval_

This example deploys a policy allowing all member users to request without approval.

<details>

<summary>via Bicep module</summary>

```bicep
module policy 'res/graph/identity-governance/entitlement-management/assignment-policies/main.bicep' = {
  name: 'policyDeployment'
  params: {
    entitlementToken: '<entitlementToken>'
    name: 'All Users - No Approval'
    accessPackageName: 'Developer Access'
    catalogName: 'Engineering Resources'
    allowedTargetScope: 'AllMemberUsers'
    durationInDays: 90
    canExtend: true
  }
}
```

</details>

### Example 2: _Manager Approval Required_

This example deploys a policy requiring manager approval.

<details>

<summary>via Bicep module</summary>

```bicep
module policy 'res/graph/identity-governance/entitlement-management/assignment-policies/main.bicep' = {
  name: 'policyDeployment'
  params: {
    entitlementToken: '<entitlementToken>'
    name: 'All Users - Manager Approval'
    accessPackageName: 'Developer Access'
    catalogName: 'Engineering Resources'
    policyDescription: 'Requires manager approval for access'
    allowedTargetScope: 'AllMemberUsers'
    durationInDays: 180
    canExtend: true
    requestApprovalSettings: {
      isApprovalRequired: true
      approvalMode: 'SingleStage'
      approvalStages: [
        {
          approvalStageTimeOutInDays: 14
          isApproverJustificationRequired: true
          primaryApprovers: [
            {
              oDataType: '#microsoft.graph.requestorManager'
              managerLevel: 1
            }
          ]
        }
      ]
    }
  }
}
```

</details>

### Example 3: _Specific Users with Two-Stage Approval_

This example deploys a policy for specific users with two-stage approval.

<details>

<summary>via Bicep module</summary>

```bicep
module policy 'res/graph/identity-governance/entitlement-management/assignment-policies/main.bicep' = {
  name: 'policyDeployment'
  params: {
    entitlementToken: '<entitlementToken>'
    name: 'Contractors - Two Stage Approval'
    accessPackageName: 'Privileged Access'
    catalogName: 'Security Resources'
    policyDescription: 'Contractors require manager + security team approval'
    allowedTargetScope: 'SpecificDirectoryUsers'
    specificAllowedTargets: [
      {
        oDataType: '#microsoft.graph.groupMembers'
        id: '<contractors-group-guid>'
        description: 'Contractors group'
      }
    ]
    durationInDays: 30
    canExtend: false
    requestApprovalSettings: {
      isApprovalRequired: true
      isRequestorJustificationRequired: true
      approvalMode: 'Serial'
      approvalStages: [
        {
          approvalStageTimeOutInDays: 7
          isApproverJustificationRequired: true
          primaryApprovers: [
            {
              oDataType: '#microsoft.graph.requestorManager'
              managerLevel: 1
            }
          ]
        }
        {
          approvalStageTimeOutInDays: 7
          isApproverJustificationRequired: true
          primaryApprovers: [
            {
              oDataType: '#microsoft.graph.groupMembers'
              id: '<security-team-group-guid>'
              description: 'Security Team'
            }
          ]
        }
      ]
    }
  }
}
```

</details>

### Example 4: _Policy with Access Reviews_

This example deploys a policy with quarterly access reviews.

<details>

<summary>via Bicep module</summary>

```bicep
module policy 'res/graph/identity-governance/entitlement-management/assignment-policies/main.bicep' = {
  name: 'policyDeployment'
  params: {
    entitlementToken: '<entitlementToken>'
    name: 'High Privilege - Quarterly Review'
    accessPackageName: 'Admin Access'
    catalogName: 'Security Resources'
    allowedTargetScope: 'SpecificDirectoryUsers'
    specificAllowedTargets: [
      {
        oDataType: '#microsoft.graph.groupMembers'
        id: '<admins-group-guid>'
      }
    ]
    durationInDays: 365
    canExtend: true
    reviewSettings: {
      isEnabled: true
      recurrenceType: 'quarterly'
      reviewerType: 'Manager'
      startDateTime: '2025-01-01T00:00:00Z'
      durationInDays: 14
      isAccessRecommendationEnabled: true
      isApprovalJustificationRequired: true
      accessReviewTimeoutBehavior: 'removeAccess'
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
| [`catalogName`](#parameter-catalogname) | string | The name of the catalog. |
| [`entitlementToken`](#parameter-entitlementtoken) | securestring | Entitlement Management API token (Graph API token with EntitlementManagement.ReadWrite.All permission) |
| [`name`](#parameter-name) | string | The display name of the assignment policy. |

**Optional parameters**

| Parameter | Type | Description |
| :-- | :-- | :-- |
| [`allowedTargetScope`](#parameter-allowedtargetscope) | string | Who can request this access package. |
| [`automaticRequestSettings`](#parameter-automaticrequestsettings) | object | Automatic request configuration for attribute-based flows. |
| [`canExtend`](#parameter-canextend) | bool | Allow assignees to request more time before expiration. |
| [`durationInDays`](#parameter-durationindays) | int | Number of days assignments remain active. |
| [`expirationDateTime`](#parameter-expirationdatetime) | string | Specific expiration date/time (UTC ISO 8601). |
| [`isCustomAssignmentScheduleAllowed`](#parameter-iscustomassignmentscheduleallowed) | bool | Permit requestors to specify custom start/end dates. |
| [`policyDescription`](#parameter-policydescription) | string | Description of the policy. |
| [`questions`](#parameter-questions) | array | Custom questions to present during submission. |
| [`requestApprovalSettings`](#parameter-requestapprovalsettings) | object | Approval workflow definition. |
| [`requestorSettings`](#parameter-requestorsettings) | object | Requestor settings defining who can submit requests. |
| [`reviewSettings`](#parameter-reviewsettings) | object | Access review configuration for periodic attestation. |
| [`specificAllowedTargets`](#parameter-specificallowedtargets) | array | Specific users, groups, or connected organizations who can request. |

### Parameter: `accessPackageName`

The name of the access package.

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

### Parameter: `name`

The display name of the assignment policy.

- Required: Yes
- Type: string

### Parameter: `allowedTargetScope`

Who can request this access package.

- Required: No
- Type: string
- Default: `'AllMemberUsers'`
- Allowed:
  ```Bicep
  [
    'AllConfiguredConnectedOrganizationUsers'
    'AllDirectoryUsers'
    'AllExistingConnectedOrganizationUsers'
    'AllExternalUsers'
    'AllMemberUsers'
    'NoSubjects'
    'NotSpecified'
    'SpecificConnectedOrganizationUsers'
    'SpecificDirectoryUsers'
  ]
  ```

### Parameter: `automaticRequestSettings`

Automatic request configuration for attribute-based flows.

- Required: No
- Type: object

### Parameter: `canExtend`

Allow assignees to request more time before expiration.

- Required: No
- Type: bool
- Default: `False`

### Parameter: `durationInDays`

Number of days assignments remain active.

- Required: No
- Type: int
- Default: `365`

### Parameter: `expirationDateTime`

Specific expiration date/time (UTC ISO 8601).

- Required: No
- Type: string

### Parameter: `isCustomAssignmentScheduleAllowed`

Permit requestors to specify custom start/end dates.

- Required: No
- Type: bool
- Default: `False`

### Parameter: `policyDescription`

Description of the policy.

- Required: No
- Type: string

### Parameter: `questions`

Custom questions to present during submission.

- Required: No
- Type: array

### Parameter: `requestApprovalSettings`

Approval workflow definition.

- Required: No
- Type: object

### Parameter: `requestorSettings`

Requestor settings defining who can submit requests.

- Required: No
- Type: object

### Parameter: `reviewSettings`

Access review configuration for periodic attestation.

- Required: No
- Type: object

### Parameter: `specificAllowedTargets`

Specific users, groups, or connected organizations who can request.

- Required: No
- Type: array

## Outputs

| Output | Type | Description |
| :-- | :-- | :-- |
| `accessPackageId` | string | The ID of the access package this policy applies to. |
| `allowedTargetScope` | string | The allowed target scope. |
| `createdDateTime` | string | The date/time when the policy was created. |
| `description` | string | The description of the assignment policy. |
| `modifiedDateTime` | string | The date/time when the policy was last modified. |
| `name` | string | The name of the assignment policy. |
| `resourceId` | string | The ID of the created assignment policy. |

## Notes

### Approval Settings Structure

When configuring approval workflows, use the following structure:

```bicep
requestApprovalSettings: {
  isApprovalRequired: true
  isApprovalRequiredForExtension: false
  isRequestorJustificationRequired: true
  approvalMode: 'SingleStage' // or 'Serial', 'Parallel'
  approvalStages: [
    {
      approvalStageTimeOutInDays: 14
      isApproverJustificationRequired: true
      primaryApprovers: [
        {
          oDataType: '#microsoft.graph.requestorManager'
          managerLevel: 1
        }
      ]
    }
  ]
}
```

### Access Review Settings Structure

For periodic access reviews:

```bicep
reviewSettings: {
  isEnabled: true
  recurrenceType: 'quarterly' // or 'monthly', 'annual'
  reviewerType: 'Manager' // or 'Self', 'Reviewers'
  startDateTime: '2025-01-01T00:00:00Z'
  durationInDays: 14
  isAccessRecommendationEnabled: true
  isApprovalJustificationRequired: true
  accessReviewTimeoutBehavior: 'removeAccess' // or 'keepAccess'
}
```

### Prerequisites

- An existing catalog (created via `catalogs/main.bicep`)
- An existing access package (created via `access-package/main.bicep`)
- Graph API token with `EntitlementManagement.ReadWrite.All` permission
