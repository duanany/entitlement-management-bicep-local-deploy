namespace EntitlementManagement.GroupPimEligibility;

using Azure.Bicep.Types.Concrete;

/// <summary>
/// Identifiers for Group PIM Eligibility resource.
/// Uses uniqueName of eligible group for idempotency.
/// </summary>
public class GroupPimEligibilityIdentifiers
{
    [TypeProperty("Unique name (mailNickname) of the ELIGIBLE group. This group's ID will be used as the principal in the PIM eligibility.", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string EligibleGroupUniqueName { get; set; }
}

/// <summary>
/// Group PIM Eligibility - Configures Just-In-Time (JIT) access between two EXISTING security groups.
///
/// SIMPLIFIED WORKFLOW:
/// 1. Create ELIGIBLE group with members via 'securityGroup' resource
/// 2. Create ACTIVATED group via 'securityGroup' resource
/// 3. Use THIS resource to link them with PIM eligibility
/// 4. Users in ELIGIBLE group can now activate → temporarily join ACTIVATED group
///
/// This resource does NOT create groups - it only establishes PIM eligibility between existing groups.
///
/// Graph API Endpoints Used:
/// 1. GET /v1.0/groups?$filter=mailNickname eq '{uniqueName}' (retrieve group IDs)
/// 2. POST /v1.0/identityGovernance/privilegedAccess/group/eligibilityScheduleRequests (create PIM link)
/// 3. GET/PATCH /v1.0/policies/roleManagementPolicies (configure approval/activation rules)
///
/// Required Permissions:
/// - Group.Read.All (retrieve groups)
/// - PrivilegedEligibilitySchedule.ReadWrite.AzureADGroup (PIM configuration)
/// - RoleManagementPolicy.ReadWrite.AzureADGroup (policy configuration)
/// </summary>
[ResourceType("groupPimEligibility")]
public class GroupPimEligibility : GroupPimEligibilityIdentifiers
{
    [TypeProperty("Unique name (mailNickname) of the ACTIVATED group. This is the target group where users get TEMPORARY membership via PIM.")]
    public required string ActivatedGroupUniqueName { get; set; }

    [TypeProperty("Access type for PIM eligibility. Valid values: 'member' (default) or 'owner'.")]
    public string AccessId { get; set; } = "member";

    [TypeProperty("Justification for the PIM eligibility configuration.")]
    public string? Justification { get; set; }

    [TypeProperty("Expiration date/time for PIM eligibility (ISO 8601 format). If omitted, eligibility never expires. Example: '2026-05-08T23:59:59Z'")]
    public string? ExpirationDateTime { get; set; }

    [TypeProperty("PIM policy template JSON content loaded via loadTextContent(). If provided, full policy is applied. Example: loadTextContent('./pim-policy-template.json')")]
    public string? PolicyTemplateJson { get; set; }

    [TypeProperty("Entra ID Object ID of the approver group. If omitted, ELIGIBLE group members approve each other's PIM requests (self-approval model).")]
    public string? ApproverGroupId { get; set; }

    [TypeProperty("Maximum activation duration (ISO 8601 duration). Default: PT2H (2 hours). Used to replace {maxActivationDuration} placeholder in policy template.")]
    public string? MaxActivationDuration { get; set; }

    // Read-only outputs (8 properties total = simple resource!)
    [TypeProperty("Entra ID Object ID of the eligible group (resolved from uniqueName)", ObjectTypePropertyFlags.ReadOnly)]
    public string? EligibleGroupId { get; set; }

    [TypeProperty("Entra ID Object ID of the activated group (resolved from uniqueName)", ObjectTypePropertyFlags.ReadOnly)]
    public string? ActivatedGroupId { get; set; }

    [TypeProperty("PIM eligibility schedule request ID", ObjectTypePropertyFlags.ReadOnly)]
    public string? PimEligibilityRequestId { get; set; }

    [TypeProperty("PIM eligibility schedule ID (created after request is processed)", ObjectTypePropertyFlags.ReadOnly)]
    public string? PimEligibilityScheduleId { get; set; }
}
