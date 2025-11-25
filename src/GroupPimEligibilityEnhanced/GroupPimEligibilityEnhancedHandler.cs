namespace EntitlementManagement.GroupPimEligibilityEnhanced;

using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Handler for the strongly-typed Group PIM eligibility resource.
/// </summary>
public class GroupPimEligibilityEnhancedHandler : GroupUserResourceHandlerBase<GroupPimEligibilityEnhanced, GroupPimEligibilityEnhancedIdentifiers>
{
    protected override async Task<ResourceResponse> Preview(ResourceRequest request, CancellationToken cancellationToken)
    {
        var props = request.Properties;

        props.EligibleGroupId = await ResolveGroupIdAsync(request.Config, props.EligibleGroupUniqueName, props.EligibleGroupId, "ELIGIBLE", cancellationToken);
        props.ActivatedGroupId = await ResolveGroupIdAsync(request.Config, props.ActivatedGroupUniqueName, props.ActivatedGroupId, "TARGET", cancellationToken);

        if (!string.IsNullOrWhiteSpace(props.EligibleGroupId) && !string.IsNullOrWhiteSpace(props.ActivatedGroupId))
        {
            var existing = await GetExistingPimEligibilityAsync(
                request.Config,
                props.EligibleGroupId!,
                props.ActivatedGroupId!,
                props.AccessId,
                cancellationToken);

            if (existing is not null)
            {
                props.PimEligibilityRequestId = existing.id;
                props.PimEligibilityScheduleId = existing.ResolvedScheduleId;
            }
        }

        return GetResponse(request);
    }

    protected override async Task<ResourceResponse> CreateOrUpdate(ResourceRequest request, CancellationToken cancellationToken)
    {
        var props = request.Properties;

        props.EligibleGroupId = await ResolveGroupIdAsync(request.Config, props.EligibleGroupUniqueName, props.EligibleGroupId, "ELIGIBLE", cancellationToken);
        props.ActivatedGroupId = await ResolveGroupIdAsync(request.Config, props.ActivatedGroupUniqueName, props.ActivatedGroupId, "TARGET", cancellationToken);

        await EnsureEligibilityAsync(request.Config, props, cancellationToken);
        await ApplyPolicySettingsAsync(request.Config, props, cancellationToken);

        return GetResponse(request);
    }

    protected override GroupPimEligibilityEnhancedIdentifiers GetIdentifiers(GroupPimEligibilityEnhanced properties) => new()
    {
        EligibleGroupId = properties.EligibleGroupId,
        EligibleGroupUniqueName = properties.EligibleGroupUniqueName
    };

    private async Task EnsureEligibilityAsync(Configuration config, GroupPimEligibilityEnhanced props, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(props.EligibleGroupId) || string.IsNullOrWhiteSpace(props.ActivatedGroupId))
        {
            throw new Exception("Eligible and target group identifiers are required before onboarding to PIM.");
        }

        var existing = await GetExistingPimEligibilityAsync(config, props.EligibleGroupId!, props.ActivatedGroupId!, props.AccessId, cancellationToken);
        if (existing is not null)
        {
            props.PimEligibilityRequestId = existing.id;
            props.PimEligibilityScheduleId = existing.ResolvedScheduleId;
            Console.WriteLine($"[PIM] Eligibility already present (scheduleId={existing.ResolvedScheduleId})");
            return;
        }

        var scheduleInfo = BuildScheduleInfo(props.Schedule);
        var result = await CreatePimEligibilityWithRetriesAsync(
            config,
            props.EligibleGroupId!,
            props.ActivatedGroupId!,
            props.AccessId,
            props.Justification ?? "Configured via GroupPimEligibilityEnhanced",
            scheduleInfo,
            cancellationToken);

        props.PimEligibilityRequestId = result.id;
        props.PimEligibilityScheduleId = result.ResolvedScheduleId;
    }

    private async Task ApplyPolicySettingsAsync(Configuration config, GroupPimEligibilityEnhanced props, CancellationToken cancellationToken)
    {
        if (props.ActivatedGroupId is null)
        {
            Console.WriteLine("[PIM] Skipping policy configuration because activated group ID is missing.");
            return;
        }

        if (props.EligibilityExpiration is null &&
            props.ActivationPolicy is null &&
            props.ApprovalPolicy is null &&
            props.NotificationPolicy is null &&
            props.AdminAssignmentPolicy is null)
        {
            Console.WriteLine("[PIM] No policy settings supplied - skipping policy patching.");
            return;
        }

        var policyId = await ResolvePolicyIdAsync(config, props.ActivatedGroupId, props.AccessId, cancellationToken);
        if (policyId is null)
        {
            Console.WriteLine("[PIM] Could not find role management policy for group; skipping policy updates.");
            return;
        }

        using var client = CreateGraphClient(config);

        if (props.EligibilityExpiration is not null)
        {
            await UpdateEligibilityExpirationAsync(client, policyId, props.EligibilityExpiration, cancellationToken);
        }

        if (props.ActivationPolicy is not null)
        {
            await UpdateActivationPoliciesAsync(client, policyId, props.ActivationPolicy, cancellationToken);
            await UpdateAuthenticationContextPolicyAsync(client, policyId, props.ActivationPolicy, cancellationToken);
        }

        if (props.ApprovalPolicy is not null)
        {
            await UpdateApprovalPolicyAsync(client, policyId, props.ApprovalPolicy, cancellationToken);
        }

        if (props.NotificationPolicy is not null)
        {
            await UpdateNotificationPoliciesAsync(client, policyId, props.NotificationPolicy, cancellationToken);
        }

        if (props.AdminAssignmentPolicy is not null)
        {
            await UpdateAdminAssignmentPolicyAsync(client, policyId, props.AdminAssignmentPolicy, cancellationToken);
        }
    }

    private async Task<string?> ResolveGroupIdAsync(
        Configuration config,
        string? uniqueName,
        string? groupId,
        string label,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(groupId))
        {
            Console.WriteLine($"[PIM] {label} group resolved via explicit ID {groupId}");
            return groupId;
        }

        if (string.IsNullOrWhiteSpace(uniqueName))
        {
            throw new Exception($"{label} group requires either a unique name or an object ID.");
        }

        Console.WriteLine($"[PIM] Resolving {label} group by uniqueName '{uniqueName}'...");
        using var client = CreateGraphClient(config);
        var filter = Uri.EscapeDataString($"mailNickname eq '{uniqueName}'");
        var response = await client.GetAsync($"groups?$filter={filter}", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to resolve {label} group '{uniqueName}': {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var groups = await response.Content.ReadFromJsonAsync<ODataListResponse<GroupResponse>>(cancellationToken: cancellationToken);
        var match = groups?.value?.FirstOrDefault();
        if (match?.id is null)
        {
            throw new Exception($"{label} group '{uniqueName}' not found. Provision it with the securityGroup resource first.");
        }

        Console.WriteLine($"[PIM] {label} group resolved to {match.id}");
        return match.id;
    }

    private async Task<PimEligibilityResponse?> GetExistingPimEligibilityAsync(
        Configuration config,
        string eligibleGroupId,
        string activatedGroupId,
        string accessId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = CreateGraphClient(config);
            var filter = $"principalId eq '{eligibleGroupId}' and groupId eq '{activatedGroupId}' and accessId eq '{accessId}'";
            var response = await client.GetAsync($"identityGovernance/privilegedAccess/group/eligibilityScheduleInstances?$filter={Uri.EscapeDataString(filter)}&$top=1", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<ODataListResponse<PimEligibilityResponse>>(cancellationToken: cancellationToken);
            return payload?.value?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PIM] Failed to query existing eligibility: {ex.Message}");
            return null;
        }
    }

    private async Task<PimEligibilityResponse> CreatePimEligibilityAsync(
        Configuration config,
        string principalId,
        string groupId,
        string accessId,
        string justification,
        object scheduleInfo,
        CancellationToken cancellationToken)
    {
        using var client = CreateGraphClient(config);

        var body = new
        {
            accessId,
            principalId,
            groupId,
            action = "adminAssign",
            justification,
            scheduleInfo
        };

        var response = await client.PostAsJsonAsync(
            "identityGovernance/privilegedAccess/group/eligibilityScheduleRequests",
            body,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Failed to create eligibility: {response.StatusCode} - {errorBody}", null, response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<PimEligibilityResponse>(cancellationToken: cancellationToken)
            ?? throw new Exception("Graph did not return an eligibility schedule response");
    }

    private async Task<PimEligibilityResponse> CreatePimEligibilityWithRetriesAsync(
        Configuration config,
        string principalId,
        string groupId,
        string accessId,
        string justification,
        object scheduleInfo,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 12;
        var delay = TimeSpan.FromSeconds(2);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                Console.WriteLine($"[PIM] Creating eligibility (attempt {attempt}/{maxAttempts})");
                return await CreatePimEligibilityAsync(config, principalId, groupId, accessId, justification, scheduleInfo, cancellationToken);
            }
            catch (HttpRequestException ex) when ((ex.StatusCode == HttpStatusCode.NotFound || ex.StatusCode == HttpStatusCode.BadRequest) && attempt < maxAttempts)
            {
                Console.WriteLine($"[PIM] Graph returned {ex.StatusCode}. Waiting {delay.TotalSeconds}s for replication before retrying...");
                await Task.Delay(delay, cancellationToken);
            }
        }

        Console.WriteLine("[PIM] Final attempt to create eligibility after retries exhausted.");
        return await CreatePimEligibilityAsync(config, principalId, groupId, accessId, justification, scheduleInfo, cancellationToken);
    }

    private object BuildScheduleInfo(EligibilityScheduleSettings schedule)
    {
        var expirationType = schedule.ExpirationType?.Trim() ?? "NoExpiration";
        var normalized = expirationType.ToLowerInvariant();

        if (normalized == "afterdatetime" && string.IsNullOrWhiteSpace(schedule.ExpirationDateTime))
        {
            throw new Exception("schedule.expirationDateTime is required when ExpirationType is 'AfterDateTime'.");
        }

        if (normalized == "afterduration" && string.IsNullOrWhiteSpace(schedule.ExpirationDuration))
        {
            throw new Exception("schedule.expirationDuration is required when ExpirationType is 'AfterDuration'.");
        }

        return new
        {
            startDateTime = string.IsNullOrWhiteSpace(schedule.StartDateTime) ? DateTime.UtcNow.ToString("o") : schedule.StartDateTime,
            expiration = normalized switch
            {
                "afterdatetime" => new { type = "afterDateTime", endDateTime = schedule.ExpirationDateTime, duration = (string?)null },
                "afterduration" => new { type = "afterDuration", endDateTime = (string?)null, duration = schedule.ExpirationDuration },
                _ => new { type = "noExpiration", endDateTime = (string?)null, duration = (string?)null }
            }
        };
    }

    private async Task<string?> ResolvePolicyIdAsync(Configuration config, string groupId, string accessId, CancellationToken cancellationToken)
    {
        using var client = CreateGraphClient(config);
        var url = $"policies/roleManagementPolicies?$filter=scopeId eq '{groupId}' and scopeType eq 'Group'";
        var response = await client.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[PIM] Failed to query roleManagementPolicies: {response.StatusCode}");
            return null;
        }

        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
        if (payload is null || !payload.RootElement.TryGetProperty("value", out var values) || values.GetArrayLength() == 0)
        {
            return null;
        }

        string? selected = null;
        foreach (var item in values.EnumerateArray())
        {
            var id = item.GetProperty("id").GetString();
            var displayName = item.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;

            if (!string.IsNullOrWhiteSpace(displayName) && displayName.Contains(accessId, StringComparison.OrdinalIgnoreCase))
            {
                selected = id;
                break;
            }
        }

        selected ??= values.EnumerateArray().First().GetProperty("id").GetString();
        return selected;
    }

    private async Task UpdateEligibilityExpirationAsync(HttpClient client, string policyId, EligibilityExpirationPolicy settings, CancellationToken cancellationToken)
    {
        var rule = await GetPolicyRuleAsync(client, policyId, "Expiration_Admin_Eligibility", cancellationToken);
        if (rule is null)
        {
            Console.WriteLine("[PIM] Expiration rule not found; skipping expiration update.");
            return;
        }

        rule["isExpirationRequired"] = settings.RequireExpiration;
        if (!string.IsNullOrWhiteSpace(settings.MaximumDuration))
        {
            rule["maximumDuration"] = settings.MaximumDuration;
        }

        await PatchPolicyRuleAsync(client, policyId, "Expiration_Admin_Eligibility", rule, cancellationToken);
    }

    private async Task UpdateActivationPoliciesAsync(HttpClient client, string policyId, ActivationPolicySettings settings, CancellationToken cancellationToken)
    {
        var expirationRule = await GetPolicyRuleAsync(client, policyId, "Expiration_EndUser_Assignment", cancellationToken);
        if (expirationRule is not null)
        {
            expirationRule["maximumDuration"] = settings.MaxActivationDuration;
            await PatchPolicyRuleAsync(client, policyId, "Expiration_EndUser_Assignment", expirationRule, cancellationToken);
        }

        var enablementRule = await GetPolicyRuleAsync(client, policyId, "Enablement_EndUser_Assignment", cancellationToken);
        if (enablementRule is not null)
        {
            var enabled = new List<string>();
            if (settings.RequireJustification)
            {
                enabled.Add("Justification");
            }

            if (settings.RequireMfa)
            {
                enabled.Add("MultiFactorAuthentication");
            }

            if (settings.RequireTicket)
            {
                enabled.Add("Ticketing");
            }

            enablementRule["enabledRules"] = BuildStringArray(enabled);
            await PatchPolicyRuleAsync(client, policyId, "Enablement_EndUser_Assignment", enablementRule, cancellationToken);
        }
    }

    private async Task UpdateAuthenticationContextPolicyAsync(HttpClient client, string policyId, ActivationPolicySettings settings, CancellationToken cancellationToken)
    {
        var rule = await GetPolicyRuleAsync(client, policyId, "AuthenticationContext_EndUser_Assignment", cancellationToken);
        if (rule is null)
        {
            Console.WriteLine("[PIM] AuthenticationContext rule not found; skipping conditional access context update.");
            return;
        }

        rule["isEnabled"] = settings.RequireConditionalAccessContext;

        if (settings.RequireConditionalAccessContext)
        {
            if (string.IsNullOrWhiteSpace(settings.ConditionalAccessContextId))
            {
                throw new Exception("ConditionalAccessContextId is required when RequireConditionalAccessContext is true. Provide a valid authentication context ID (e.g., 'c1') configured in Entra ID Conditional Access.");
            }

            rule["claimValue"] = settings.ConditionalAccessContextId;
            Console.WriteLine($"[PIM] Enabling conditional access context with claim value '{settings.ConditionalAccessContextId}'");
        }
        else
        {
            // When disabling, clear the claim value
            rule["claimValue"] = "";
            Console.WriteLine("[PIM] Disabling conditional access context");
        }

        await PatchPolicyRuleAsync(client, policyId, "AuthenticationContext_EndUser_Assignment", rule, cancellationToken);
    }

    private async Task UpdateApprovalPolicyAsync(HttpClient client, string policyId, ApprovalPolicySettings settings, CancellationToken cancellationToken)
    {
        var rule = await GetPolicyRuleAsync(client, policyId, "Approval_EndUser_Assignment", cancellationToken);
        if (rule is null)
        {
            Console.WriteLine("[PIM] Approval rule not found; skipping approval update.");
            return;
        }

        var settingNode = rule["setting"] as JsonObject ?? new JsonObject();
        settingNode["isApprovalRequired"] = settings.IsApprovalRequired;
        settingNode["isApprovalRequiredForExtension"] = settings.IsApprovalRequiredForExtension;
        settingNode["isRequestorJustificationRequired"] = settings.IsRequestorJustificationRequired;
        settingNode["approvalMode"] = settings.IsApprovalRequired ? settings.ApprovalMode : "NoApproval";

        JsonArray stagesArray;
        if (!settings.IsApprovalRequired)
        {
            stagesArray = new JsonArray();
        }
        else
        {
            if (settings.Stages is null || settings.Stages.Length == 0)
            {
                throw new Exception("Approval policy requires at least one stage when approvals are enabled.");
            }

            stagesArray = new JsonArray();
            foreach (var stage in settings.Stages.Take(2))
            {
                var stageObject = new JsonObject
                {
                    ["approvalStageTimeOutInDays"] = stage.ApprovalStageTimeoutInDays,
                    ["isApproverJustificationRequired"] = stage.IsApproverJustificationRequired,
                    ["escalationTimeInMinutes"] = stage.EscalationTimeInMinutes,
                    ["isEscalationEnabled"] = stage.IsEscalationEnabled
                };

                stageObject["primaryApprovers"] = BuildSubjectSetArray(stage.PrimaryApprovers);
                stageObject["escalationApprovers"] = BuildSubjectSetArray(stage.EscalationApprovers);

                stagesArray.Add(stageObject);
            }
        }

        settingNode["approvalStages"] = stagesArray;
        rule["setting"] = settingNode;

        await PatchPolicyRuleAsync(client, policyId, "Approval_EndUser_Assignment", rule, cancellationToken);
    }

    private async Task UpdateNotificationPoliciesAsync(HttpClient client, string policyId, NotificationPolicySettings settings, CancellationToken cancellationToken)
    {
        await UpdateNotificationRuleAsync(client, policyId, "Notification_Admin_Admin_Eligibility", settings.NotifyAdminsOnEligibility, cancellationToken);
        await UpdateNotificationRuleAsync(client, policyId, "Notification_Requestor_Admin_Eligibility", settings.NotifyRequestorsOnEligibility, cancellationToken);
        await UpdateNotificationRuleAsync(client, policyId, "Notification_Approver_Admin_Eligibility", settings.NotifyApproversOnEligibility, cancellationToken);
        await UpdateNotificationRuleAsync(client, policyId, "Notification_Admin_EndUser_Assignment", settings.NotifyAdminsOnActivation, cancellationToken);
        await UpdateNotificationRuleAsync(client, policyId, "Notification_Requestor_EndUser_Assignment", settings.NotifyRequestorsOnActivation, cancellationToken);
        await UpdateNotificationRuleAsync(client, policyId, "Notification_Approver_EndUser_Assignment", settings.NotifyApproversOnActivation, cancellationToken);
    }

    private async Task UpdateAdminAssignmentPolicyAsync(HttpClient client, string policyId, AdminAssignmentPolicySettings settings, CancellationToken cancellationToken)
    {
        var rule = await GetPolicyRuleAsync(client, policyId, "Enablement_Admin_Assignment", cancellationToken);
        if (rule is null)
        {
            Console.WriteLine("[PIM] Admin enablement rule not found; skipping update.");
            return;
        }

        var enabled = new List<string>();
        if (settings.RequireJustification)
        {
            enabled.Add("Justification");
        }

        if (settings.RequireMfa)
        {
            enabled.Add("MultiFactorAuthentication");
        }

        if (settings.RequireTicket)
        {
            enabled.Add("Ticketing");
        }

        rule["enabledRules"] = BuildStringArray(enabled);
        await PatchPolicyRuleAsync(client, policyId, "Enablement_Admin_Assignment", rule, cancellationToken);
    }

    private async Task UpdateNotificationRuleAsync(HttpClient client, string policyId, string ruleId, bool enabled, CancellationToken cancellationToken)
    {
        var rule = await GetPolicyRuleAsync(client, policyId, ruleId, cancellationToken);
        if (rule is null)
        {
            Console.WriteLine($"[PIM] Notification rule {ruleId} not found; skipping.");
            return;
        }

        rule["isDefaultRecipientsEnabled"] = enabled;
        rule["notificationLevel"] = enabled ? "All" : "None";

        await PatchPolicyRuleAsync(client, policyId, ruleId, rule, cancellationToken);
    }

    private async Task<JsonObject?> GetPolicyRuleAsync(HttpClient client, string policyId, string ruleId, CancellationToken cancellationToken)
    {
        var response = await client.GetAsync($"policies/roleManagementPolicies/{policyId}/rules/{ruleId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var node = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
        return node as JsonObject;
    }

    private async Task PatchPolicyRuleAsync(HttpClient client, string policyId, string ruleId, JsonObject rule, CancellationToken cancellationToken)
    {
        var response = await client.PatchAsJsonAsync($"policies/roleManagementPolicies/{policyId}/rules/{ruleId}", rule, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"[PIM] Failed to patch rule {ruleId}: {response.StatusCode} - {error}");
        }
        else
        {
            Console.WriteLine($"[PIM] Updated rule {ruleId}");
        }
    }

    private JsonArray BuildSubjectSetArray(ApprovalStageApprover[]? approvers)
    {
        var array = new JsonArray();
        if (approvers is null)
        {
            return array;
        }

        foreach (var approver in approvers)
        {
            var type = approver.Type?.Equals("user", StringComparison.OrdinalIgnoreCase) == true ? "user" : "group";

            if (type == "user")
            {
                if (string.IsNullOrWhiteSpace(approver.UserId))
                {
                    throw new Exception("Approval stage approver of type 'user' requires userId.");
                }

                array.Add(new JsonObject
                {
                    ["@odata.type"] = "#microsoft.graph.singleUser",
                    ["userId"] = approver.UserId
                });
            }
            else
            {
                if (string.IsNullOrWhiteSpace(approver.GroupId))
                {
                    throw new Exception("Approval stage approver of type 'group' requires groupId.");
                }

                array.Add(new JsonObject
                {
                    ["@odata.type"] = "#microsoft.graph.groupMembers",
                    ["groupId"] = approver.GroupId
                });
            }
        }

        return array;
    }

    private JsonArray BuildStringArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private class ODataListResponse<T>
    {
        public List<T>? value { get; set; }
    }

    private class GroupResponse
    {
        public string? id { get; set; }
    }

    private class PimEligibilityResponse
    {
        public string? id { get; set; }
        public string? scheduleId { get; set; }
        public string? targetScheduleId { get; set; }

        /// <summary>
        /// Graph returns targetScheduleId for requests, scheduleId for instances.
        /// </summary>
        public string? ResolvedScheduleId => scheduleId ?? targetScheduleId;
    }
}