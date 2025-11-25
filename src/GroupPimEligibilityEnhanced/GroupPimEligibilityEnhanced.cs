namespace EntitlementManagement.GroupPimEligibilityEnhanced
{
    using Azure.Bicep.Types.Concrete;

    /// <summary>
    /// Identifiers for Group PIM Eligibility Enhanced resource.
    /// Uses either uniqueName or ID of eligible group (principal) for idempotency.
    /// </summary>
    public class GroupPimEligibilityEnhancedIdentifiers
    {
        [TypeProperty("Unique name (mailNickname) of the ELIGIBLE group (principal). Either this or eligibleGroupId must be provided.", ObjectTypePropertyFlags.Identifier)]
        public string? EligibleGroupUniqueName { get; set; }

        [TypeProperty("Entra ID Object ID of the ELIGIBLE group (principal). Either this or eligibleGroupUniqueName must be provided.", ObjectTypePropertyFlags.Identifier)]
        public string? EligibleGroupId { get; set; }
    }

    /// <summary>
    /// Group PIM Eligibility Enhanced - Configures eligible membership/ownership between two existing security groups
    /// by orchestrating Microsoft Graph privilegedAccessGroup eligibility schedule requests and explicit policy rules.
    /// </summary>
    [ResourceType("groupPimEligibilityEnhanced")]
    public class GroupPimEligibilityEnhanced : GroupPimEligibilityEnhancedIdentifiers
    {
        [TypeProperty("Unique name (mailNickname) of the target group that users will activate into. Either this or activatedGroupId must be provided.")]
        public string? ActivatedGroupUniqueName { get; set; }

        [TypeProperty("Entra ID Object ID of the target group. Either this or activatedGroupUniqueName must be provided.")]
        public string? ActivatedGroupId { get; set; }

        [TypeProperty("Graph privilegedAccessGroupRelationships value for the assignment. Valid values: 'member' (default) or 'owner'.")]
        public string AccessId { get; set; } = "member";

        [TypeProperty("Justification passed to POST /identityGovernance/privilegedAccess/group/eligibilityScheduleRequests.")]
        public string? Justification { get; set; }

        [TypeProperty("Controls requestSchedule payload for POST /identityGovernance/privilegedAccess/group/eligibilityScheduleRequests.", ObjectTypePropertyFlags.Required)]
        public EligibilityScheduleSettings Schedule { get; set; } = new();

        [TypeProperty("Maps to Graph rule Expiration_Admin_Eligibility (PATCH /policies/roleManagementPolicies/{policyId}/rules/Expiration_Admin_Eligibility).")]
        public EligibilityExpirationPolicy? EligibilityExpiration { get; set; }

        [TypeProperty("Maps to Graph rules Expiration_EndUser_Assignment and Enablement_EndUser_Assignment for activation constraints.")]
        public ActivationPolicySettings? ActivationPolicy { get; set; }

        [TypeProperty("Maps to Graph rule Approval_EndUser_Assignment for multi-stage approvals.")]
        public ApprovalPolicySettings? ApprovalPolicy { get; set; }

        [TypeProperty("Maps to Graph notification rules (Notification_*). Allows fine-grained admin/requestor/approver toggles.")]
        public NotificationPolicySettings? NotificationPolicy { get; set; }

        [TypeProperty("Maps to Graph rule Enablement_Admin_Assignment for admin-side justification/MFA/ticketing requirements.")]
        public AdminAssignmentPolicySettings? AdminAssignmentPolicy { get; set; }

        [TypeProperty("Eligibility schedule request ID returned by Graph.", ObjectTypePropertyFlags.ReadOnly)]
        public string? PimEligibilityRequestId { get; set; }

        [TypeProperty("Eligibility schedule ID created after request succeeds.", ObjectTypePropertyFlags.ReadOnly)]
        public string? PimEligibilityScheduleId { get; set; }
    }

    /// <summary>
    /// Represents requestSchedule for privilegedAccessGroupEligibilityScheduleRequest.
    /// </summary>
    public class EligibilityScheduleSettings
    {
        [TypeProperty("Start date/time (ISO 8601). If omitted Graph starts at request time e.g. 2025-02-01T12:00:00Z.")]
        public string? StartDateTime { get; set; }

        [TypeProperty("Expiration type for scheduleInfo.expiration. Allowed: 'NoExpiration', 'AfterDateTime', 'AfterDuration'. Default 'NoExpiration'.")]
        public string ExpirationType { get; set; } = "NoExpiration";

        [TypeProperty("End date/time when ExpirationType = 'AfterDateTime'. Example: '2026-05-08T23:59:59Z'.")]
        public string? ExpirationDateTime { get; set; }

        [TypeProperty("ISO 8601 duration when ExpirationType = 'AfterDuration'. Example: 'P180D'.")]
        public string? ExpirationDuration { get; set; }
    }

    /// <summary>
    /// Maps to unifiedRoleManagementPolicyExpirationRule (id: Expiration_Admin_Eligibility).
    /// </summary>
    public class EligibilityExpirationPolicy
    {
        [TypeProperty("When true Graph enforces expiration for admin eligibility assignments (isExpirationRequired). Default false (permanent eligible allowed).")]
        public bool RequireExpiration { get; set; }

        [TypeProperty("Maximum eligibility duration (ISO 8601 e.g. 'P365D'). Required when RequireExpiration = true.")]
        public string? MaximumDuration { get; set; }
    }

    /// <summary>
    /// Combines Expiration_EndUser_Assignment + Enablement_EndUser_Assignment + AuthenticationContext_EndUser_Assignment.
    /// </summary>
    public class ActivationPolicySettings
    {
        [TypeProperty("Maximum activation duration (ISO 8601) used for Expiration_EndUser_Assignment.maximumDuration. Example 'PT2H'. Default PT2H.")]
        public string MaxActivationDuration { get; set; } = "PT2H";

        [TypeProperty("Require justification on activation (Enablement_EndUser_Assignment rule list includes 'Justification'). Default true.")]
        public bool RequireJustification { get; set; } = true;

        [TypeProperty("Require MFA on activation (Enablement_EndUser_Assignment includes 'MultiFactorAuthentication'). Default true.")]
        public bool RequireMfa { get; set; } = true;

        [TypeProperty("Require ticket info on activation (Enablement_EndUser_Assignment includes 'Ticketing'). Default false.")]
        public bool RequireTicket { get; set; }

        [TypeProperty("Enable Conditional Access authentication context on activation (AuthenticationContext_EndUser_Assignment rule). Default false.")]
        public bool RequireConditionalAccessContext { get; set; }

        [TypeProperty("Conditional Access authentication context claim value (e.g., 'c1', 'c2'). Required when RequireConditionalAccessContext is true. Must match an authentication context ID configured in Entra ID Conditional Access.")]
        public string? ConditionalAccessContextId { get; set; }
    }

    /// <summary>
    /// Maps to Approval_EndUser_Assignment rule with optional multi-stage approval chains.
    /// </summary>
    public class ApprovalPolicySettings
    {
        [TypeProperty("When true Graph approvalSettings.isApprovalRequired = true. Default false.")]
        public bool IsApprovalRequired { get; set; }

        [TypeProperty("Requires approval for extension (approvalSettings.isApprovalRequiredForExtension).")]
        public bool IsApprovalRequiredForExtension { get; set; }

        [TypeProperty("Require requestor justification (approvalSettings.isRequestorJustificationRequired). Default true.")]
        public bool IsRequestorJustificationRequired { get; set; } = true;

        [TypeProperty("Approval mode per Graph approvalSettings.approvalMode. Allowed: 'SingleStage', 'Serial', 'Parallel'. Default SingleStage.")]
        public string ApprovalMode { get; set; } = "SingleStage";

        [TypeProperty("Approval stages aligned with unifiedApprovalStage collection. Provide at most 2 stages per Graph limits.")]
        public ApprovalStageSettings[]? Stages { get; set; }
    }

    public class ApprovalStageSettings
    {
        [TypeProperty("Timeout in days before request auto-cancels (approvalStageTimeOutInDays).")]
        public int ApprovalStageTimeoutInDays { get; set; } = 1;

        [TypeProperty("Require approver justification (isApproverJustificationRequired).")]
        public bool IsApproverJustificationRequired { get; set; } = true;

        [TypeProperty("Escalation time in minutes (escalationTimeInMinutes). Default 0 disabled.")]
        public int EscalationTimeInMinutes { get; set; }

        [TypeProperty("Enable escalation to escalationApprovers when EscalationTimeInMinutes > 0 (isEscalationEnabled).")]
        public bool IsEscalationEnabled { get; set; }

        [TypeProperty("Primary approvers translated to Graph primaryApprovers subjectSet array. At least one approver required when approvals enabled.")]
        public ApprovalStageApprover[]? PrimaryApprovers { get; set; }

        [TypeProperty("Escalation approvers translated to Graph escalationApprovers subjectSet array.")]
        public ApprovalStageApprover[]? EscalationApprovers { get; set; }
    }

    public class ApprovalStageApprover
    {
        [TypeProperty("Approver subject type. Allowed values: 'user', 'group'.")]
        public string Type { get; set; } = "group";

        [TypeProperty("Entra ID object ID for the user when Type = 'user'.")]
        public string? UserId { get; set; }

        [TypeProperty("Entra ID object ID for the group when Type = 'group'.")]
        public string? GroupId { get; set; }
    }

    /// <summary>
    /// Maps to Notification_* rules. Each channel enables the corresponding Graph notification rule IDs.
    /// </summary>
    public class NotificationPolicySettings
    {
        [TypeProperty("Notify admins when eligibility is granted (Notification_Admin_Admin_Eligibility).")]
        public bool NotifyAdminsOnEligibility { get; set; } = true;

        [TypeProperty("Notify requestors when eligibility is granted (Notification_Requestor_Admin_Eligibility).")]
        public bool NotifyRequestorsOnEligibility { get; set; } = true;

        [TypeProperty("Notify approvers for eligibility renewals (Notification_Approver_Admin_Eligibility).")]
        public bool NotifyApproversOnEligibility { get; set; } = true;

        [TypeProperty("Notify admins when activations occur (Notification_Admin_EndUser_Assignment).")]
        public bool NotifyAdminsOnActivation { get; set; } = true;

        [TypeProperty("Notify requestors when their activation occurs (Notification_Requestor_EndUser_Assignment).")]
        public bool NotifyRequestorsOnActivation { get; set; } = true;

        [TypeProperty("Notify approvers for activation approvals (Notification_Approver_EndUser_Assignment).")]
        public bool NotifyApproversOnActivation { get; set; } = true;
    }

    /// <summary>
    /// Maps to Enablement_Admin_Assignment rule for admin operations executed via Graph automation.
    /// </summary>
    public class AdminAssignmentPolicySettings
    {
        [TypeProperty("Require justification when admins create/modify eligibility assignments (Enablement_Admin_Assignment includes 'Justification'). Default true.")]
        public bool RequireJustification { get; set; } = true;

        [TypeProperty("Require MFA for admin operations (Enablement_Admin_Assignment includes 'MultiFactorAuthentication'). Default true.")]
        public bool RequireMfa { get; set; } = true;

        [TypeProperty("Require ticket info for admin operations (Enablement_Admin_Assignment includes 'Ticketing'). Default false.")]
        public bool RequireTicket { get; set; }
    }
}
