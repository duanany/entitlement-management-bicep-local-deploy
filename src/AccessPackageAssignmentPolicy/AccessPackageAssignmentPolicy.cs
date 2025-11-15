using System.Text.Json.Serialization;
using Azure.Bicep.Types.Concrete;
using Bicep.Local.Extension.Types.Attributes;

namespace EntitlementManagement.AccessPackageAssignmentPolicy;

/// <summary>
/// Represents an Azure Entitlement Management access package assignment policy.
/// Aligns with the Microsoft Graph beta schema so we can send the same payload as the PowerShell sample.
/// </summary>
[ResourceType("accessPackageAssignmentPolicy")]
public class AccessPackageAssignmentPolicy : AccessPackageAssignmentPolicyIdentifiers
{
    [TypeProperty("Optional description for the policy shown to requestors.")]
    public string? Description { get; set; }

    [TypeProperty("Who can request this access package. Matches Microsoft Graph allowedTargetScope enum.")]
    public AllowedTargetScope? AllowedTargetScope { get; set; }

    [TypeProperty("Specific users, groups, or connected organizations who can request when allowedTargetScope is Specific*.")]
    [JsonPropertyName("specificAllowedTargets")]
    public SubjectSet[]? SpecificAllowedTargets { get; set; }

    [TypeProperty("Identifier of an existing policy to link to (Graph customPolicyId).")]
    [JsonPropertyName("customPolicyId")]
    public string? CustomPolicyId { get; set; }

    [TypeProperty("Allow assignees to request more time before their assignment expires.")]
    [JsonPropertyName("canExtend")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool CanExtend { get; set; }

    [TypeProperty("Number of days assignments created by this policy remain active before expiring.")]
    [JsonPropertyName("durationInDays")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int DurationInDays { get; set; }

    [TypeProperty("Specific expiration date/time for assignments created by this policy (UTC ISO 8601).")]
    [JsonPropertyName("expirationDateTime")]
    public string? ExpirationDateTime { get; set; }

    [TypeProperty("Permit requestors to specify custom start/end dates when submitting requests.")]
    [JsonPropertyName("isCustomAssignmentScheduleAllowed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsCustomAssignmentScheduleAllowed { get; set; }

    [TypeProperty("Defines which subjects can submit requests under this policy.")]
    [JsonPropertyName("requestorSettings")]
    public AccessPackageAssignmentRequestorSettings? RequestorSettings { get; set; }

    [TypeProperty("Approval workflow definition (Graph approvalSettings contract).")]
    [JsonPropertyName("requestApprovalSettings")]
    public AccessPackageApprovalSettings? RequestApprovalSettings { get; set; }

    [TypeProperty("Access review configuration that enforces periodic attestation.")]
    [JsonPropertyName("accessReviewSettings")]
    public AccessPackageAssignmentReviewSettings? ReviewSettings { get; set; }

    [TypeProperty("Automatic request configuration for attribute-based assignment flows.")]
    public AccessPackageAutomaticRequestSettings? AutomaticRequestSettings { get; set; }

    [TypeProperty("Custom questions to present to the requestor during submission.")]
    [JsonPropertyName("questions")]
    public AccessPackageQuestion[]? Questions { get; set; }

    [TypeProperty("[OUTPUT] Unique policy identifier assigned by Graph.")]
    public string? Id { get; set; }

    [TypeProperty("[OUTPUT] Creation timestamp (UTC).")]
    public string? CreatedDateTime { get; set; }

    [TypeProperty("[OUTPUT] Last modification timestamp (UTC).")]
    public string? ModifiedDateTime { get; set; }
}

/// <summary>
/// Enum describing who can request the access package (scopeType equivalent).
/// </summary>
public enum AllowedTargetScope
{
    NotSpecified,
    AllMemberUsers,
    AllDirectoryUsers,
    AllConfiguredConnectedOrganizationUsers,
    AllExistingConnectedOrganizationUsers,
    AllExternalUsers,
    SpecificDirectoryUsers,
    SpecificConnectedOrganizationUsers,
    NoSubjects
}

/// <summary>
/// Beta requestorSettings shape from Graph.
/// </summary>
public class AccessPackageAssignmentRequestorSettings
{
    [TypeProperty("Scope describing who can request: SpecificDirectorySubjects, AllExistingDirectoryMemberUsers, etc.")]
    [JsonPropertyName("scopeType")]
    public string? ScopeType { get; set; }

    [TypeProperty("When false the policy temporarily blocks new incoming requests.")]
    [JsonPropertyName("acceptRequests")]
    public bool AcceptRequests { get; set; } = true;

    [TypeProperty("Specific requestors (userSets) allowed when scopeType is Specific*.")]
    [JsonPropertyName("allowedRequestors")]
    public RequestorSubject[]? AllowedRequestors { get; set; }
}

/// <summary>
/// approvalSettings contract with beta-only approvalStages support.
/// </summary>
public class AccessPackageApprovalSettings
{
    [TypeProperty("When true, approval is required for initial assignments.")]
    [JsonPropertyName("isApprovalRequired")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsApprovalRequired { get; set; }

    [TypeProperty("When true, extending an assignment also requires approval.")]
    [JsonPropertyName("isApprovalRequiredForExtension")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsApprovalRequiredForExtension { get; set; }

    [TypeProperty("Require the requestor to provide justification in the request form.")]
    [JsonPropertyName("isRequestorJustificationRequired")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsRequestorJustificationRequired { get; set; }

    [TypeProperty("Approval routing mode: SingleStage, Serial, Parallel, or NoApproval.")]
    [JsonPropertyName("approvalMode")]
    public string? ApprovalMode { get; set; }

    [TypeProperty("One or two stages containing approvers, escalation settings, and timeouts.")]
    [JsonPropertyName("approvalStages")]
    public AccessPackageApprovalStage[]? ApprovalStages { get; set; }
}

/// <summary>
/// Approval stage definition with primary/fallback/escalation approvers.
/// </summary>
public class AccessPackageApprovalStage
{
    [TypeProperty("Days before a pending request is automatically denied.")]
    [JsonPropertyName("approvalStageTimeOutInDays")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ApprovalStageTimeOutInDays { get; set; }

    [TypeProperty("Require approvers to provide justification when approving or denying.")]
    [JsonPropertyName("isApproverJustificationRequired")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsApproverJustificationRequired { get; set; }

    [TypeProperty("Enable escalations when primary approvers do not respond.")]
    [JsonPropertyName("isEscalationEnabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsEscalationEnabled { get; set; }

    [TypeProperty("Minutes to wait before escalating to escalationApprovers.")]
    [JsonPropertyName("escalationTimeInMinutes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int EscalationTimeInMinutes { get; set; }

    [TypeProperty("Primary approvers invoked at the start of the stage.")]
    [JsonPropertyName("primaryApprovers")]
    public ApproverSubject[]? PrimaryApprovers { get; set; }

    [TypeProperty("Fallback reviewers for the primary approver set.")]
    [JsonPropertyName("fallbackPrimaryApprovers")]
    public ApproverSubject[]? FallbackPrimaryApprovers { get; set; }

    [TypeProperty("Escalation approvers engaged when escalation is enabled.")]
    [JsonPropertyName("escalationApprovers")]
    public ApproverSubject[]? EscalationApprovers { get; set; }

    [TypeProperty("Optional fallback escalation approvers.")]
    [JsonPropertyName("fallbackEscalationApprovers")]
    public ApproverSubject[]? FallbackEscalationApprovers { get; set; }
}

/// <summary>
/// assignmentReviewSettings contract for periodic reviews.
/// </summary>
public class AccessPackageAssignmentReviewSettings
{
    [TypeProperty("Turn periodic access reviews on or off for this policy.")]
    [JsonPropertyName("isEnabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsEnabled { get; set; }

    [TypeProperty("Interval for recurring reviews, e.g. monthly or quarterly.")]
    [JsonPropertyName("recurrenceType")]
    public string? RecurrenceType { get; set; }

    [TypeProperty("Who must perform the review: Self, Reviewers, or Manager.")]
    [JsonPropertyName("reviewerType")]
    public string? ReviewerType { get; set; }

    [TypeProperty("When the first review should start (UTC ISO 8601).")]
    [JsonPropertyName("startDateTime")]
    public string? StartDateTime { get; set; }

    [TypeProperty("How long reviewers have to respond for each review cycle (days).")]
    [JsonPropertyName("durationInDays")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int DurationInDays { get; set; }

    [TypeProperty("Specific reviewers (userSets) when reviewerType is Reviewers.")]
    [JsonPropertyName("reviewers")]
    public ReviewerSubject[]? Reviewers { get; set; }

    [TypeProperty("Show Graph recommendations to reviewers.")]
    [JsonPropertyName("isAccessRecommendationEnabled")]
    public bool IsAccessRecommendationEnabled { get; set; } = true;

    [TypeProperty("Require reviewers to provide justification when completing reviews.")]
    [JsonPropertyName("isApprovalJustificationRequired")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsApprovalJustificationRequired { get; set; } = true;

    [TypeProperty("Default action when reviewers do not respond: keepAccess, removeAccess, acceptAccessRecommendation.")]
    [JsonPropertyName("accessReviewTimeoutBehavior")]
    public string? AccessReviewTimeoutBehavior { get; set; }
}

/// <summary>
/// Automatic request settings for attribute-based policies.
/// </summary>
public class AccessPackageAutomaticRequestSettings
{
    [TypeProperty("Automatically request access for users that match allowed targets.")]
    [JsonPropertyName("requestAccessForAllowedTargets")]
    public bool RequestAccessForAllowedTargets { get; set; }

    [TypeProperty("Remove assignments when users no longer match allowed targets.")]
    [JsonPropertyName("removeAccessWhenTargetLeavesAllowedTargets")]
    public bool RemoveAccessWhenTargetLeavesAllowedTargets { get; set; }

    [TypeProperty("Grace period before access is removed once the rule is no longer met (ISO 8601 duration).")]
    [JsonPropertyName("gracePeriodBeforeAccessRemoval")]
    public string? GracePeriodBeforeAccessRemoval { get; set; }
}

/// <summary>
/// Represents a request question shown during self-service requests.
/// </summary>
public class AccessPackageQuestion
{
    [TypeProperty("Question type identifier, e.g. #microsoft.graph.accessPackageMultipleChoiceQuestion.")]
    [JsonPropertyName("oDataType")]
    public string? ODataType { get; set; }

    [TypeProperty("Indicates whether requestors must answer this question.")]
    [JsonPropertyName("isRequired")]
    public bool IsRequired { get; set; }

    [TypeProperty("Question text or localized content.")]
    [JsonPropertyName("text")]
    public object? Text { get; set; }

    [TypeProperty("Display order of the question in the form.")]
    [JsonPropertyName("sequence")]
    public int Sequence { get; set; }

    [TypeProperty("If multiple choice, requestors can pick several answers when true.")]
    [JsonPropertyName("allowsMultipleSelection")]
    public bool AllowsMultipleSelection { get; set; }

    [TypeProperty("Choices available for multiple-choice questions.")]
    [JsonPropertyName("choices")]
    public AccessPackageAnswerChoice[]? Choices { get; set; }
}

/// <summary>
/// Choice entry for AccessPackageQuestion.
/// </summary>
public class AccessPackageAnswerChoice
{
    [TypeProperty("Unique value submitted when this choice is selected.")]
    [JsonPropertyName("actualValue")]
    public string? ActualValue { get; set; }

    [TypeProperty("Display value or localized content shown to requestors.")]
    [JsonPropertyName("displayValue")]
    public object? DisplayValue { get; set; }
}

/// <summary>
/// Represents a userSet entry used for approvers.
/// </summary>
public class ApproverSubject
{
    [TypeProperty("Subject type: #microsoft.graph.singleUser, #microsoft.graph.groupMembers, #microsoft.graph.requestorManager, etc.")]
    [JsonPropertyName("oDataType")]
    public string? ODataType { get; set; }

    [TypeProperty("Object ID for the referenced subject (id property on userSet).")]
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [TypeProperty("Provide context for the approver set.")]
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [TypeProperty("Treat this subject as backup when true.")]
    [JsonPropertyName("isBackup")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsBackup { get; set; }

    [TypeProperty("Manager depth for requestorManager subject type. Defaults to 1.")]
    [JsonPropertyName("managerLevel")]
    public int ManagerLevel { get; set; } = 1;

    [TypeProperty("Optional legacy userId helper for templates. If provided, handler maps it to id.")]
    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [TypeProperty("Optional legacy groupId helper for templates. If provided, handler maps it to id.")]
    [JsonPropertyName("groupId")]
    public string? GroupId { get; set; }

    [TypeProperty("Connected organization ID for connectedOrganizationMembers subject type.")]
    [JsonPropertyName("connectedOrganizationId")]
    public string? ConnectedOrganizationId { get; set; }
}

/// <summary>
/// Reviewer subject reuses the same shape as userSet.
/// </summary>
public class ReviewerSubject
{
    [TypeProperty("Subject type: #microsoft.graph.singleUser, #microsoft.graph.groupMembers, #microsoft.graph.requestorManager, etc.")]
    [JsonPropertyName("oDataType")]
    public string? ODataType { get; set; }

    [TypeProperty("Object ID for the reviewer.")]
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [TypeProperty("Optional description for the reviewer set.")]
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [TypeProperty("Treat this reviewer set as backup when true.")]
    [JsonPropertyName("isBackup")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsBackup { get; set; }

    [TypeProperty("Manager depth for requestorManager reviewer type.")]
    [JsonPropertyName("managerLevel")]
    public int ManagerLevel { get; set; } = 1;

    [TypeProperty("Optional legacy helper for single user scenarios.")]
    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [TypeProperty("Optional legacy helper for group scenarios.")]
    [JsonPropertyName("groupId")]
    public string? GroupId { get; set; }

    [TypeProperty("Connected organization ID for external reviewers.")]
    [JsonPropertyName("connectedOrganizationId")]
    public string? ConnectedOrganizationId { get; set; }
}

/// <summary>
/// Requestor subject used inside requestorSettings.allowedRequestors.
/// </summary>
public class RequestorSubject
{
    [TypeProperty("Subject type: #microsoft.graph.singleUser, #microsoft.graph.groupMembers, #microsoft.graph.connectedOrganizationMembers, etc.")]
    [JsonPropertyName("oDataType")]
    public string? ODataType { get; set; }

    [TypeProperty("Object ID of the allowed requestor (maps to id property).")]
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [TypeProperty("Optional description for the requestor set.")]
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [TypeProperty("Treat this requester set as backup when true.")]
    [JsonPropertyName("isBackup")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsBackup { get; set; }

    [TypeProperty("Manager depth when using requestorManager subject type.")]
    [JsonPropertyName("managerLevel")]
    public int ManagerLevel { get; set; } = 1;

    [TypeProperty("Legacy helper for single user scenarios (handler maps to id).")]
    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [TypeProperty("Legacy helper for group scenarios (handler maps to id).")]
    [JsonPropertyName("groupId")]
    public string? GroupId { get; set; }

    [TypeProperty("Connected organization ID for connectedOrganizationMembers subject type.")]
    [JsonPropertyName("connectedOrganizationId")]
    public string? ConnectedOrganizationId { get; set; }
}

/// <summary>
/// Generic subjectSet used by specificAllowedTargets and other collections.
/// </summary>
public class SubjectSet
{
    [TypeProperty("Subject type identifier, e.g. #microsoft.graph.singleUser, #microsoft.graph.groupMembers.")]
    [JsonPropertyName("oDataType")]
    public string? ODataType { get; set; }

    [TypeProperty("Object ID referenced by this subject.")]
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [TypeProperty("Optional description to make templates easier to understand.")]
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [TypeProperty("Manager depth when subject type is requestorManager.")]
    [JsonPropertyName("managerLevel")]
    public int ManagerLevel { get; set; } = 1;

    [TypeProperty("Legacy helper field for single user subjects (handler maps to id).")]
    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [TypeProperty("Legacy helper field for group subjects (handler maps to id).")]
    [JsonPropertyName("groupId")]
    public string? GroupId { get; set; }

    [TypeProperty("Connected organization identifier for connectedOrganizationMembers subjects.")]
    [JsonPropertyName("connectedOrganizationId")]
    public string? ConnectedOrganizationId { get; set; }

    [TypeProperty("Optional membership rule used by attributeRuleMembers subject type.")]
    [JsonPropertyName("membershipRule")]
    public string? MembershipRule { get; set; }
}
