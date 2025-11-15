namespace EntitlementManagement.GroupPimEligibility;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bicep.Local.Extension.Host.Handlers;

/// <summary>
/// Handler for Group PIM Eligibility via Microsoft Graph API.
/// Configures PIM eligibility between TWO EXISTING security groups.
///
/// Pattern:
/// 1. Retrieve ELIGIBLE group by uniqueName (must exist!)
/// 2. Retrieve ACTIVATED group by uniqueName (must exist!)
/// 3. Create PIM eligibility schedule request (eligible group → activated group)
/// 4. Optionally apply role management policy (approval workflows, activation duration, etc.)
///
/// Graph API Endpoints:
/// - Groups: GET /v1.0/groups?$filter=mailNickname eq '{uniqueName}'
/// - PIM: POST /v1.0/identityGovernance/privilegedAccess/group/eligibilityScheduleRequests
/// - Policy: GET/PATCH /v1.0/policies/roleManagementPolicies
///
/// Required Permissions:
/// - Group.Read.All
/// - PrivilegedEligibilitySchedule.ReadWrite.AzureADGroup
/// - RoleManagementPolicy.ReadWrite.AzureADGroup
/// </summary>
public class GroupPimEligibilityHandler : GroupUserResourceHandlerBase<GroupPimEligibility, GroupPimEligibilityIdentifiers>
{
    // Track which group policies we've patched in this session to serialize policy updates
    private static readonly HashSet<string> PatchedPoliciesInSession = new();
    private static readonly SemaphoreSlim PolicyPatchLock = new(1, 1);

    protected override async Task<ResourceResponse> Preview(ResourceRequest request, CancellationToken cancellationToken)
    {
        var props = request.Properties;

        Console.WriteLine($"PREVIEW: GroupPimEligibility");

        // Resolve eligible group (either by uniqueName or id)
        var eligibleGroupId = await ResolveGroupIdAsync(
            request.Config,
            props.EligibleGroupUniqueName,
            props.EligibleGroupId,
            "ELIGIBLE",
            cancellationToken);

        props.EligibleGroupId = eligibleGroupId;

        // Resolve activated group (either by uniqueName or id)
        var activatedGroupId = await ResolveGroupIdAsync(
            request.Config,
            props.ActivatedGroupUniqueName,
            props.ActivatedGroupId,
            "ACTIVATED",
            cancellationToken);

        props.ActivatedGroupId = activatedGroupId;

        Console.WriteLine($"PREVIEW: Eligible={props.EligibleGroupId}, Activated={props.ActivatedGroupId}");

        return GetResponse(request);
    }

    protected override async Task<ResourceResponse> CreateOrUpdate(ResourceRequest request, CancellationToken cancellationToken)
    {
        var props = request.Properties;

        Console.WriteLine($"CREATE/UPDATE: GroupPimEligibility");

        // Step 1: Resolve ELIGIBLE group (either by uniqueName or id)
        var eligibleGroupId = await ResolveGroupIdAsync(
            request.Config,
            props.EligibleGroupUniqueName,
            props.EligibleGroupId,
            "ELIGIBLE",
            cancellationToken);

        props.EligibleGroupId = eligibleGroupId;
        Console.WriteLine($"Found ELIGIBLE group: {props.EligibleGroupId}");

        // Step 2: Resolve ACTIVATED group (either by uniqueName or id)
        var activatedGroupId = await ResolveGroupIdAsync(
            request.Config,
            props.ActivatedGroupUniqueName,
            props.ActivatedGroupId,
            "ACTIVATED",
            cancellationToken);

        props.ActivatedGroupId = activatedGroupId;
        Console.WriteLine($"Found ACTIVATED group: {props.ActivatedGroupId}");

        // Step 3: Check if PIM eligibility already exists
        var existingEligibility = await GetExistingPimEligibilityAsync(
            request.Config,
            eligibleGroupId,
            activatedGroupId,
            cancellationToken);

        if (existingEligibility is not null)
        {
            Console.WriteLine($"PIM eligibility already exists: {existingEligibility.id}");
            props.PimEligibilityRequestId = existingEligibility.id;
            props.PimEligibilityScheduleId = existingEligibility.scheduleId;

            // Apply policy if provided (group already PIM-onboarded)
            if (!string.IsNullOrWhiteSpace(props.PolicyTemplateJson))
            {
                var approverGroupId = props.ApproverGroupId ?? eligibleGroupId;
                Console.WriteLine($"Applying role management policy (group already PIM-enabled)...");
                Console.WriteLine($"Approvers: {(props.ApproverGroupId == null ? "ELIGIBLE group members (self-approval)" : "Custom approver group")}");

                await ApplyFullRoleManagementPolicyAsync(
                    request.Config,
                    activatedGroupId,
                    props.PolicyTemplateJson,
                    approverGroupId,
                    props.MaxActivationDuration,
                    cancellationToken);
            }
        }
        else
        {
            // Step 4: Create PIM eligibility schedule request with retries
            Console.WriteLine($"Creating PIM eligibility: Eligible={eligibleGroupId} → Activated={activatedGroupId}");
            var pimResult = await CreatePimEligibilityWithRetriesAsync(
                request.Config,
                eligibleGroupId,
                activatedGroupId,
                props.AccessId,
                props.Justification ?? "PIM eligibility configured via Bicep local-deploy",
                props.ExpirationDateTime,
                cancellationToken);

            props.PimEligibilityRequestId = pimResult.id;
            props.PimEligibilityScheduleId = pimResult.scheduleId;

            // Step 5: Wait for PIM automatic onboarding to complete
            Console.WriteLine($"Waiting 10 seconds for PIM automatic onboarding to complete...");
            await Task.Delay(10000, cancellationToken);

            // Step 6: Apply role management policy AFTER onboarding (if provided)
            if (!string.IsNullOrWhiteSpace(props.PolicyTemplateJson))
            {
                var approverGroupId = props.ApproverGroupId ?? eligibleGroupId;
                Console.WriteLine($"Applying role management policy with approval workflows (AFTER onboarding)...");
                Console.WriteLine($"Approvers: {(props.ApproverGroupId == null ? "ELIGIBLE group members (self-approval)" : "Custom approver group")}");

                await ApplyFullRoleManagementPolicyAsync(
                    request.Config,
                    activatedGroupId,
                    props.PolicyTemplateJson,
                    approverGroupId,
                    props.MaxActivationDuration,
                    cancellationToken);
            }
        }

        Console.WriteLine($"PIM eligibility ready: Eligible={props.EligibleGroupId}, Activated={props.ActivatedGroupId}, PIM={props.PimEligibilityRequestId}");

        return GetResponse(request);
    }

    protected override GroupPimEligibilityIdentifiers GetIdentifiers(GroupPimEligibility properties) => new()
    {
        EligibleGroupUniqueName = properties.EligibleGroupUniqueName,
        EligibleGroupId = properties.EligibleGroupId
    };

    // ========================================
    // HELPER METHODS
    // ========================================

    /// <summary>
    /// Resolve group ID from either uniqueName or direct ID.
    /// Supports both workflows: reference by name OR reference by ID.
    /// </summary>
    private async Task<string> ResolveGroupIdAsync(
        Configuration config,
        string? uniqueName,
        string? groupId,
        string groupType,
        CancellationToken cancellationToken)
    {
        // Validate: at least one must be provided
        if (string.IsNullOrWhiteSpace(uniqueName) && string.IsNullOrWhiteSpace(groupId))
        {
            throw new Exception($"{groupType} group requires either uniqueName or ID. Both are missing.");
        }

        // If ID provided directly, use it
        if (!string.IsNullOrWhiteSpace(groupId))
        {
            Console.WriteLine($"{groupType} group ID provided directly: {groupId}");
            return groupId;
        }

        // Otherwise, lookup by uniqueName
        Console.WriteLine($"Resolving {groupType} group by uniqueName: {uniqueName}");
        var group = await GetGroupByUniqueNameAsync(config, uniqueName!, cancellationToken);
        if (group is null)
        {
            throw new Exception($"{groupType} group not found: '{uniqueName}'. Create this group first using 'securityGroup' resource.");
        }

        Console.WriteLine($"{groupType} group resolved: {group.id}");
        return group.id!;
    }

    /// <summary>
    /// Retrieve group by uniqueName (mailNickname) from Graph API.
    /// Returns null if not found.
    /// </summary>
    private async Task<GroupResponse?> GetGroupByUniqueNameAsync(
        Configuration config,
        string uniqueName,
        CancellationToken cancellationToken)
    {
        using var client = CreateGraphClient(config);

        var filter = $"mailNickname eq '{Uri.EscapeDataString(uniqueName)}'";
        var url = $"groups?$filter={filter}";

        var response = await client.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ODataListResponse<GroupResponse>>(cancellationToken: cancellationToken);

        return result?.value?.FirstOrDefault();
    }

    /// <summary>
    /// Create PIM eligibility schedule request.
    /// Auto-detects expiration based on expirationDateTime presence.
    /// </summary>
    private async Task<PimEligibilityResponse> CreatePimEligibilityAsync(
        Configuration config,
        string principalId,  // eligible group ID
        string groupId,      // activated/target group ID
        string accessId,     // "member" or "owner"
        string justification,
        string? expirationDateTime,
        CancellationToken cancellationToken)
    {
        using var client = CreateGraphClient(config);

        var hasExpiration = !string.IsNullOrWhiteSpace(expirationDateTime);

        var requestBody = new
        {
            accessId,
            principalId,
            groupId,
            action = "adminAssign",
            justification,
            scheduleInfo = new
            {
                startDateTime = DateTime.UtcNow.ToString("o"),
                expiration = hasExpiration
                    ? new { type = "afterDateTime", endDateTime = expirationDateTime }
                    : new { type = "noExpiration", endDateTime = (string?)null }
            }
        };

        Console.WriteLine($"POST /identityGovernance/privilegedAccess/group/eligibilityScheduleRequests");
        Console.WriteLine($"   principalId={principalId}, groupId={groupId}, accessId={accessId}");
        Console.WriteLine($"   expiration={(hasExpiration ? expirationDateTime : "noExpiration")}");

        var response = await client.PostAsJsonAsync(
            "identityGovernance/privilegedAccess/group/eligibilityScheduleRequests",
            requestBody,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"Failed to create PIM eligibility: {response.StatusCode}");
            Console.WriteLine($"Error body: {errorBody}");
            throw new HttpRequestException($"PIM eligibility creation failed: {response.StatusCode} - {errorBody}", null, response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<PimEligibilityResponse>(cancellationToken: cancellationToken)
            ?? throw new Exception("Failed to deserialize PIM eligibility response");

        Console.WriteLine($"PIM eligibility created: requestId={result.id}, scheduleId={result.scheduleId}");

        return result;
    }

    /// <summary>
    /// Create PIM eligibility with retry logic (handles Graph API replication delay).
    /// Following PowerShell pattern: 15 retries with 2s delay.
    /// </summary>
    private async Task<PimEligibilityResponse> CreatePimEligibilityWithRetriesAsync(
        Configuration config,
        string principalId,
        string groupId,
        string accessId,
        string justification,
        string? expirationDateTime,
        CancellationToken cancellationToken)
    {
        const int maxRetries = 15;
        const int baseDelaySeconds = 2;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                Console.WriteLine($"PIM eligibility attempt {attempt}/{maxRetries}");
                return await CreatePimEligibilityAsync(
                    config,
                    principalId,
                    groupId,
                    accessId,
                    justification,
                    expirationDateTime,
                    cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound && attempt < maxRetries)
            {
                Console.WriteLine($"Group not found (404) - likely replication delay. Retry {attempt}/{maxRetries} in {baseDelaySeconds}s...");
                await Task.Delay(TimeSpan.FromSeconds(baseDelaySeconds), cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest && attempt < maxRetries)
            {
                if (ex.Message.Contains("already exists") || ex.Message.Contains("duplicate"))
                {
                    Console.WriteLine($"PIM eligibility might already exist (400). Checking...");
                    var existing = await GetExistingPimEligibilityAsync(config, principalId, groupId, cancellationToken);
                    if (existing is not null)
                    {
                        Console.WriteLine($"Confirmed - PIM eligibility exists: {existing.id}");
                        return existing;
                    }
                }

                Console.WriteLine($"Bad request (400) - retry {attempt}/{maxRetries} in {baseDelaySeconds}s...");
                await Task.Delay(TimeSpan.FromSeconds(baseDelaySeconds), cancellationToken);
            }
        }

        // Final attempt without catch
        Console.WriteLine($"Final PIM eligibility attempt {maxRetries}/{maxRetries}");
        return await CreatePimEligibilityAsync(
            config,
            principalId,
            groupId,
            accessId,
            justification,
            expirationDateTime,
            cancellationToken);
    }

    /// <summary>
    /// Check if PIM eligibility already exists for these groups.
    /// Uses eligibilityScheduleInstances (current eligibilities only) - Microsoft best practice.
    /// </summary>
    private async Task<PimEligibilityResponse?> GetExistingPimEligibilityAsync(
        Configuration config,
        string principalId,
        string groupId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = CreateGraphClient(config);

            var response = await client.GetAsync(
                $"identityGovernance/privilegedAccess/group/eligibilityScheduleInstances?$filter=principalId eq '{principalId}' and groupId eq '{groupId}'&$top=1",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ODataListResponse<PimEligibilityResponse>>(cancellationToken: cancellationToken);

            if (result?.value?.Count > 0)
            {
                Console.WriteLine($"Found existing PIM eligibility: {result.value[0].id}");
                return result.value[0];
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not check existing PIM eligibility: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Apply FULL role management policy from template with approval workflows, notifications, MFA, etc.
    /// </summary>
    private async Task ApplyFullRoleManagementPolicyAsync(
        Configuration config,
        string groupId,
        string policyTemplateJson,
        string? approverGroupId,
        string? maxActivationDuration,
        CancellationToken cancellationToken)
    {
        await PolicyPatchLock.WaitAsync(cancellationToken);
        try
        {
            var policyKey = $"{groupId}_full";
            if (PatchedPoliciesInSession.Contains(policyKey))
            {
                Console.WriteLine($"Full policy for group {groupId} already applied in this session");
                Console.WriteLine($"Waiting 10 seconds for previous policy patch to propagate...");
                await Task.Delay(10000, cancellationToken);
                return;
            }

            using var client = CreateGraphClient(config);

            // Step 1: Get the role management policy for this group
            Console.WriteLine($"Applying FULL role management policy for group {groupId}...");
            var policiesResponse = await client.GetAsync(
                $"policies/roleManagementPolicies?$filter=scopeId eq '{groupId}' and scopeType eq 'Group'",
                cancellationToken);

            if (!policiesResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Could not fetch role management policy: {policiesResponse.StatusCode}");
                return;
            }

            var policiesResult = await policiesResponse.Content.ReadFromJsonAsync<ODataListResponse<RoleManagementPolicy>>(cancellationToken: cancellationToken);
            if (policiesResult?.value?.Count == 0)
            {
                Console.WriteLine($"No role management policy found for group {groupId}");
                return;
            }

            if (policiesResult!.value!.Count < 2)
            {
                Console.WriteLine($"Expected 2 policies (owner+member), found {policiesResult.value.Count}");
                return;
            }

            var policyId = policiesResult!.value![1].id;  // Index [1] = MEMBER policy
            Console.WriteLine($"Found MEMBER role management policy: {policyId}");

            // Step 2: Fetch existing policy FIRST to extract current values for placeholders
            Console.WriteLine($"Fetching existing policy to check if update needed...");
            var existingPolicyResponse = await client.GetAsync(
                $"policies/roleManagementPolicies/{policyId}?$expand=rules",
                cancellationToken);

            RoleManagementPolicy? existingPolicyData = null;
            if (existingPolicyResponse.IsSuccessStatusCode)
            {
                existingPolicyData = await existingPolicyResponse.Content.ReadFromJsonAsync<RoleManagementPolicy>(cancellationToken: cancellationToken);
                Console.WriteLine($"Existing policy has {existingPolicyData?.rules?.Count ?? 0} rules");
            }
            else
            {
                Console.WriteLine($"Could not fetch existing policy: {existingPolicyResponse.StatusCode}");
            }

            // Step 3: Parse policy template and replace placeholders with CURRENT values (for comparison)
            // Use approverGroupId from parameter, fallback to existing policy value if available
            var approverGroupIdValue = approverGroupId ?? "00000000-0000-0000-0000-000000000000";
            policyTemplateJson = policyTemplateJson.Replace("{approverGroupId}", approverGroupIdValue);

            var maxDuration = maxActivationDuration ?? "PT2H";
            policyTemplateJson = policyTemplateJson.Replace("{maxActivationDuration}", maxDuration);

            var templateData = System.Text.Json.JsonSerializer.Deserialize<PolicyTemplate>(policyTemplateJson);
            if (templateData?.rules is null || templateData.rules.Count == 0)
            {
                Console.WriteLine($"Invalid policy template format");
                return;
            }

            Console.WriteLine($"Parsed {templateData.rules.Count} policy rules from template");

            // Step 4: Compare template with existing policy
            if (existingPolicyData != null && IsPolicyUpToDate(existingPolicyData, templateData))
            {
                Console.WriteLine($"Policy is already up-to-date - skipping patch operations!");
                PatchedPoliciesInSession.Add(policyKey);
                return;
            }

            Console.WriteLine($"Policy needs updating - proceeding with patch operations...");

            // Step 5: PATCH each rule in the policy
            var successCount = 0;
            var failCount = 0;

            foreach (var rule in templateData.rules)
            {
                try
                {
                    Console.WriteLine($"Applying rule: {rule.id}...");

                    var patchResponse = await client.PatchAsJsonAsync(
                        $"policies/roleManagementPolicies/{policyId}/rules/{rule.id}",
                        rule,
                        cancellationToken);

                    if (patchResponse.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"  {rule.id} applied successfully");
                        successCount++;
                    }
                    else
                    {
                        var errorBody = await patchResponse.Content.ReadAsStringAsync(cancellationToken);
                        Console.WriteLine($"  {rule.id} failed: {patchResponse.StatusCode} - {errorBody}");
                        failCount++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  {rule.id} error: {ex.Message}");
                    failCount++;
                }
            }

            Console.WriteLine($"Policy application complete: {successCount} succeeded, {failCount} failed");

            PatchedPoliciesInSession.Add(policyKey);

            Console.WriteLine($"Waiting 30 seconds for policy changes to fully replicate...");
            await Task.Delay(30000, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error applying full role management policy: {ex.Message}");
        }
        finally
        {
            PolicyPatchLock.Release();
        }
    }

    // ========================================
    // RESPONSE MODELS
    // ========================================

    private class ODataListResponse<T>
    {
        [JsonPropertyName("value")]
        public List<T>? value { get; set; }
    }

    private class GroupResponse
    {
        [JsonPropertyName("id")]
        public string? id { get; set; }

        [JsonPropertyName("displayName")]
        public string? displayName { get; set; }

        [JsonPropertyName("mailNickname")]
        public string? mailNickname { get; set; }
    }

    private class PimEligibilityResponse
    {
        [JsonPropertyName("id")]
        public string? id { get; set; }

        [JsonPropertyName("scheduleId")]
        public string? scheduleId { get; set; }

        [JsonPropertyName("status")]
        public string? status { get; set; }
    }

    private class RoleManagementPolicy
    {
        [JsonPropertyName("id")]
        public string? id { get; set; }

        [JsonPropertyName("scopeId")]
        public string? scopeId { get; set; }

        [JsonPropertyName("scopeType")]
        public string? scopeType { get; set; }

        [JsonPropertyName("rules")]
        public List<PolicyRule>? rules { get; set; }
    }

    private class PolicyTemplate
    {
        [JsonPropertyName("rules")]
        public List<PolicyRule>? rules { get; set; }
    }

    private class PolicyRule
    {
        [JsonPropertyName("id")]
        public string? id { get; set; }

        [JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement>? ExtensionData { get; set; }
    }

    /// <summary>
    /// Checks if the existing policy matches the desired template.
    /// Compares rule IDs and their extension data (settings).
    /// Returns true if policy is already up-to-date (no changes needed).
    /// </summary>
    private bool IsPolicyUpToDate(RoleManagementPolicy? existingPolicy, PolicyTemplate desiredTemplate)
    {
        Console.WriteLine($"IsPolicyUpToDate called");

        if (existingPolicy?.rules == null)
        {
            Console.WriteLine($"  Existing policy or rules is null");
            return false;
        }

        if (desiredTemplate?.rules == null)
        {
            Console.WriteLine($"  Desired template or rules is null");
            return false;
        }

        Console.WriteLine($"Comparing {desiredTemplate.rules.Count} desired rules against {existingPolicy.rules.Count} existing rules");

        // Check if all desired rules exist in current policy with matching content
        foreach (var desiredRule in desiredTemplate.rules)
        {
            var existingRule = existingPolicy.rules.FirstOrDefault(r => r.id == desiredRule.id);

            if (existingRule == null)
            {
                Console.WriteLine($"  Rule {desiredRule.id} not found in existing policy");
                return false;
            }

            // Compare extension data (the actual rule settings)
            var areEqual = AreRulesEqual(existingRule, desiredRule);
            Console.WriteLine($"  Rule {desiredRule.id}: {(areEqual ? "SAME" : "DIFFERENT")}");

            if (!areEqual)
            {
                return false;
            }
        }

        Console.WriteLine($"All rules match - policy is up-to-date!");
        return true;
    }

    /// <summary>
    /// Compares two policy rules by their extension data.
    /// Returns true if they have the same settings.
    /// Uses deep JSON element comparison instead of string comparison to avoid property order issues.
    /// </summary>
    private bool AreRulesEqual(PolicyRule existing, PolicyRule desired)
    {
        if (existing.ExtensionData == null && desired.ExtensionData == null)
        {
            return true;
        }

        if (existing.ExtensionData == null || desired.ExtensionData == null)
        {
            return false;
        }

        // Check if both have same number of properties
        if (existing.ExtensionData.Count != desired.ExtensionData.Count)
        {
            return false;
        }

        // Deep compare each property (ignores property order)
        foreach (var kvp in desired.ExtensionData)
        {
            if (!existing.ExtensionData.TryGetValue(kvp.Key, out var existingValue))
            {
                return false; // Property missing in existing
            }

            if (!JsonElementsEqual(existingValue, kvp.Value))
            {
                return false; // Property value different
            }
        }

        return true;
    }

    /// <summary>
    /// Deep comparison of two JsonElement objects.
    /// Handles objects, arrays, primitives, nulls.
    /// </summary>
    private bool JsonElementsEqual(System.Text.Json.JsonElement a, System.Text.Json.JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
            return false;

        switch (a.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                var aProps = a.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
                var bProps = b.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

                if (aProps.Count != bProps.Count)
                    return false;

                foreach (var prop in aProps)
                {
                    if (!bProps.TryGetValue(prop.Key, out var bValue))
                        return false;

                    if (!JsonElementsEqual(prop.Value, bValue))
                        return false;
                }

                return true;

            case System.Text.Json.JsonValueKind.Array:
                var aArray = a.EnumerateArray().ToList();
                var bArray = b.EnumerateArray().ToList();

                if (aArray.Count != bArray.Count)
                    return false;

                for (int i = 0; i < aArray.Count; i++)
                {
                    if (!JsonElementsEqual(aArray[i], bArray[i]))
                        return false;
                }

                return true;

            case System.Text.Json.JsonValueKind.String:
                return a.GetString() == b.GetString();

            case System.Text.Json.JsonValueKind.Number:
                return a.GetRawText() == b.GetRawText();

            case System.Text.Json.JsonValueKind.True:
            case System.Text.Json.JsonValueKind.False:
                return a.GetBoolean() == b.GetBoolean();

            case System.Text.Json.JsonValueKind.Null:
                return true;

            default:
                return false;
        }
    }
}
