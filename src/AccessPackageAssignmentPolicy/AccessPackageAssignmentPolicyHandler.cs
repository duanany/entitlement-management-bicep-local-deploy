using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;

namespace EntitlementManagement.AccessPackageAssignmentPolicy;

public class AccessPackageAssignmentPolicyHandler
    : EntitlementManagementResourceHandlerBase<AccessPackageAssignmentPolicy, AccessPackageAssignmentPolicyIdentifiers>
{
    protected override async Task<ResourceResponse> Preview(ResourceRequest request, CancellationToken cancellationToken)
    {
        var props = request.Properties;
        var config = request.Config;

        var accessPackageId = props.AccessPackageId;
        var displayName = props.DisplayName;

        Console.WriteLine($"[POLICY] Previewing policy '{displayName}' for access package '{accessPackageId}'...");

        // Check if policy already exists (by displayName + accessPackageId)
        using var client = CreateGraphClient(config, useBeta: true); // CHANGED: Use beta for approval stages support

        var existingPolicy = await FindPolicyByNameAndPackageIdAsync(
            client,
            displayName,
            accessPackageId,
            cancellationToken
        );

        if (existingPolicy != null)
        {
            // Policy exists - populate outputs
            props.Id = existingPolicy.Value.id;
            props.CreatedDateTime = existingPolicy.Value.createdDateTime;
            props.ModifiedDateTime = existingPolicy.Value.modifiedDateTime;

            Console.WriteLine($"Policy already exists:");
            Console.WriteLine($"   ID: {existingPolicy.Value.id}");
            Console.WriteLine($"   Scope: {props.AllowedTargetScope}");
        }
        else
        {
            Console.WriteLine($"Policy does not exist - will be created");
        }

        return GetResponse(request);
    }

    protected override async Task<ResourceResponse> CreateOrUpdate(ResourceRequest request, CancellationToken cancellationToken)
    {
        var props = request.Properties;
        var config = request.Config;
        using var client = CreateGraphClient(config, useBeta: true); // CHANGED: Use beta for approval stages support

        var accessPackageId = props.AccessPackageId;
        var displayName = props.DisplayName;

        Console.WriteLine($"[POLICY] Creating/updating policy '{displayName}' for access package '{accessPackageId}'...");

        // Check if policy already exists
        var existingPolicy = await FindPolicyByNameAndPackageIdAsync(
            client,
            displayName,
            accessPackageId,
            cancellationToken
        );

        // Prepare request body
        // NOTE: Graph API expects camelCase enum values (notSpecified, not NotSpecified)
        var allowedTargetScopeValue = props.AllowedTargetScope?.ToString();
        if (!string.IsNullOrEmpty(allowedTargetScopeValue))
        {
            // Convert first character to lowercase for camelCase
            allowedTargetScopeValue = char.ToLowerInvariant(allowedTargetScopeValue[0]) + allowedTargetScopeValue.Substring(1);
        }

        ValidateApprovalSettings(props.RequestApprovalSettings);

        HydrateSubjectSets(props.SpecificAllowedTargets);
        HydrateRequestorSettings(props.RequestorSettings);
        HydrateApprovalSettings(props.RequestApprovalSettings);
        HydrateReviewSettings(props.ReviewSettings);

        var requestBody = new
        {
            displayName = props.DisplayName,
            description = props.Description,
            allowedTargetScope = allowedTargetScopeValue,
            specificAllowedTargets = props.SpecificAllowedTargets,
            requestorSettings = props.RequestorSettings,
            requestApprovalSettings = props.RequestApprovalSettings,
            accessReviewSettings = props.ReviewSettings,
            automaticRequestSettings = NormalizeAutomaticRequestSettings(props.AutomaticRequestSettings),
            questions = props.Questions,
            customPolicyId = props.CustomPolicyId,
            // Only include these if explicitly set (non-zero/non-default)
            canExtend = props.CanExtend ? (bool?)props.CanExtend : null,
            durationInDays = props.DurationInDays > 0 ? (int?)props.DurationInDays : null,
            expirationDateTime = props.ExpirationDateTime,
            isCustomAssignmentScheduleAllowed = props.IsCustomAssignmentScheduleAllowed ? (bool?)props.IsCustomAssignmentScheduleAllowed : null,
            accessPackage = new { id = accessPackageId }
        };

        var jsonBody = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault,
            WriteIndented = false
        });

        // Graph requires '@odata.type' while Bicep inputs use 'oDataType'. Patch the serialized payload
        jsonBody = jsonBody.Replace("\"oDataType\":", "\"@odata.type\":");

        Console.WriteLine($"[POLICY] Request body: {jsonBody}");

        if (existingPolicy != null)
        {
            // UPDATE existing policy (PUT)
            Console.WriteLine($"[POLICY] Updating existing policy {existingPolicy.Value.id}...");
            var updateUrl = $"identityGovernance/entitlementManagement/accessPackageAssignmentPolicies/{existingPolicy.Value.id}";
            var updateRequest = new HttpRequestMessage(HttpMethod.Put, updateUrl)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };

            var updateResponse = await client.SendAsync(updateRequest, cancellationToken);

            if (!updateResponse.IsSuccessStatusCode)
            {
                var errorContent = await updateResponse.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"[POLICY] Update failed: {updateResponse.StatusCode} - {errorContent}");
                throw new Exception($"Failed to update policy: {updateResponse.StatusCode} - {errorContent} | Payload: {jsonBody}");
            }

            var updatedJson = await updateResponse.Content.ReadAsStringAsync(cancellationToken);
            var updatedPolicy = JsonSerializer.Deserialize<JsonElement>(updatedJson);

            // Update props with response
            props.Id = updatedPolicy.GetProperty("id").GetString();
            props.CreatedDateTime = updatedPolicy.TryGetProperty("createdDateTime", out var cd) ? cd.GetString() : null;
            props.ModifiedDateTime = updatedPolicy.TryGetProperty("modifiedDateTime", out var md) ? md.GetString() : null;

            Console.WriteLine($"[POLICY] Policy updated successfully: {props.Id}");

            return GetResponse(request);
        }
        else
        {
            // CREATE new policy (POST)
            Console.WriteLine($"[POLICY] Creating new policy '{displayName}'...");
            var createUrl = "identityGovernance/entitlementManagement/accessPackageAssignmentPolicies";
            var createRequest = new HttpRequestMessage(HttpMethod.Post, createUrl)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };

            var createResponse = await client.SendAsync(createRequest, cancellationToken);

            if (!createResponse.IsSuccessStatusCode)
            {
                var errorContent = await createResponse.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"[POLICY] Create failed: {createResponse.StatusCode} - {errorContent}");
                throw new Exception($"Failed to create policy: {createResponse.StatusCode} - {errorContent} | Payload: {jsonBody}");
            }

            var createdJson = await createResponse.Content.ReadAsStringAsync(cancellationToken);
            var createdPolicy = JsonSerializer.Deserialize<JsonElement>(createdJson);

            // Update props with response
            props.Id = createdPolicy.GetProperty("id").GetString();
            props.CreatedDateTime = createdPolicy.TryGetProperty("createdDateTime", out var cd) ? cd.GetString() : null;
            props.ModifiedDateTime = createdPolicy.TryGetProperty("modifiedDateTime", out var md) ? md.GetString() : null;

            Console.WriteLine($"[POLICY] Policy created successfully: {props.Id}");

            return GetResponse(request);
        }
    }

    protected override AccessPackageAssignmentPolicyIdentifiers GetIdentifiers(AccessPackageAssignmentPolicy properties) => new()
    {
        AccessPackageId = properties.AccessPackageId,
        DisplayName = properties.DisplayName
    };

    // Helper method to find policy by name + package ID
    private async Task<(string id, string createdDateTime, string? modifiedDateTime)?> FindPolicyByNameAndPackageIdAsync(
        HttpClient client,
        string displayName,
        string accessPackageId,
        CancellationToken cancellationToken
    )
    {
        var filter = $"displayName eq '{displayName.Replace("'", "''")}'";
        var listUrl = $"identityGovernance/entitlementManagement/accessPackageAssignmentPolicies?$filter={Uri.EscapeDataString(filter)}&$expand=accessPackage";

        Console.WriteLine($"[POLICY] Querying for existing policy: {listUrl}");

        var listResponse = await client.GetAsync(listUrl, cancellationToken);

        if (listResponse.StatusCode == HttpStatusCode.NotFound)
        {
            Console.WriteLine($"[POLICY] No policies found with displayName '{displayName}'");
            return null;
        }

        listResponse.EnsureSuccessStatusCode();
        var listJson = await listResponse.Content.ReadAsStringAsync(cancellationToken);
        Console.WriteLine($"[POLICY] Query response: {listJson}");
        var listData = JsonSerializer.Deserialize<JsonElement>(listJson);

        if (listData.TryGetProperty("value", out var policiesArray) && policiesArray.GetArrayLength() > 0)
        {
            Console.WriteLine($"[POLICY] Found {policiesArray.GetArrayLength()} policy/policies with displayName '{displayName}'");

            foreach (var policyElement in policiesArray.EnumerateArray())
            {
                // Check if this policy belongs to the same access package
                if (policyElement.TryGetProperty("accessPackage", out var apRef) &&
                    apRef.TryGetProperty("id", out var apId) &&
                    apId.GetString() == accessPackageId)
                {
                    var id = policyElement.GetProperty("id").GetString() ?? throw new Exception("Policy ID is null");
                    var created = policyElement.GetProperty("createdDateTime").GetString() ?? string.Empty;
                    var modified = policyElement.TryGetProperty("modifiedDateTime", out var md) ? md.GetString() : null;

                    Console.WriteLine($"[POLICY] Found matching policy: {id} for access package {accessPackageId}");
                    return (id, created, modified);
                }
                else
                {
                    var apIdFound = policyElement.TryGetProperty("accessPackage", out var ap) && ap.TryGetProperty("id", out var foundId) ? foundId.GetString() : "N/A";
                    Console.WriteLine($"[POLICY] Skipping policy - belongs to different access package: {apIdFound} (looking for {accessPackageId})");
                }
            }
        }
        else
        {
            Console.WriteLine($"[POLICY] No policies found with displayName '{displayName}'");
        }

        return null;
    }

    /// <summary>
    /// Merges two objects (user-provided approvalStage + fixed properties).
    /// </summary>
    private static object? MergeObjects(object? userObject, object fixedProperties)
    {
        if (userObject == null)
        {
            return fixedProperties;
        }

        // Serialize both to JSON and merge
        var userJson = JsonSerializer.Serialize(userObject);
        var fixedJson = JsonSerializer.Serialize(fixedProperties);

        var userDict = JsonSerializer.Deserialize<Dictionary<string, object>>(userJson);
        var fixedDict = JsonSerializer.Deserialize<Dictionary<string, object>>(fixedJson);

        if (userDict != null && fixedDict != null)
        {
            foreach (var kvp in fixedDict)
            {
                userDict[kvp.Key] = kvp.Value;
            }
            return userDict;
        }

        return fixedProperties;
    }

    private static AccessPackageAutomaticRequestSettings? NormalizeAutomaticRequestSettings(AccessPackageAutomaticRequestSettings? settings)
    {
        if (settings is null)
        {
            return null;
        }

        var hasValue = settings.RequestAccessForAllowedTargets
                        || settings.RemoveAccessWhenTargetLeavesAllowedTargets
                        || !string.IsNullOrWhiteSpace(settings.GracePeriodBeforeAccessRemoval);

        return hasValue ? settings : null;
    }

    private static void ValidateApprovalSettings(AccessPackageApprovalSettings? approval)
    {
        if (approval is null)
        {
            return;
        }

        if (approval.IsApprovalRequired || approval.IsApprovalRequiredForExtension)
        {
            if (approval.ApprovalStages is null || approval.ApprovalStages.Length == 0)
            {
                throw new Exception("requestApprovalSettings.approvalStages must include at least one stage when approval is required.");
            }

            if (approval.ApprovalStages.Any(stage => stage.PrimaryApprovers == null || stage.PrimaryApprovers.Length == 0))
            {
                throw new Exception("Each approval stage must include primaryApprovers when approval is required.");
            }
        }
    }

    private static void HydrateRequestorSettings(AccessPackageAssignmentRequestorSettings? settings)
    {
        if (settings?.AllowedRequestors is null)
        {
            return;
        }

        foreach (var subject in settings.AllowedRequestors)
        {
            HydrateRequestorSubject(subject);
        }
    }

    private static void HydrateApprovalSettings(AccessPackageApprovalSettings? settings)
    {
        if (settings?.ApprovalStages is null)
        {
            return;
        }

        foreach (var stage in settings.ApprovalStages)
        {
            HydrateApproverSubjects(stage.PrimaryApprovers);
            HydrateApproverSubjects(stage.FallbackPrimaryApprovers);
            HydrateApproverSubjects(stage.EscalationApprovers);
            HydrateApproverSubjects(stage.FallbackEscalationApprovers);
        }
    }

    private static void HydrateReviewSettings(AccessPackageAssignmentReviewSettings? settings)
    {
        HydrateReviewerSubjects(settings?.Reviewers);
    }

    private static void HydrateSubjectSets(SubjectSet[]? subjectSets)
    {
        if (subjectSets is null)
        {
            return;
        }

        foreach (var subject in subjectSets)
        {
            HydrateSubjectSet(subject);
        }
    }

    private static void HydrateApproverSubjects(ApproverSubject[]? subjects)
    {
        if (subjects is null)
        {
            return;
        }

        foreach (var subject in subjects)
        {
            if (subject is null)
            {
                continue;
            }

            subject.Id ??= subject.UserId ?? subject.GroupId ?? subject.ConnectedOrganizationId;
            subject.UserId = null;
            subject.GroupId = null;

            // Only keep managerLevel for requestorManager type
            if (subject.ODataType != "#microsoft.graph.requestorManager")
            {
                subject.ManagerLevel = 0; // Reset to default so it won't be serialized
            }
        }
    }

    private static void HydrateReviewerSubjects(ReviewerSubject[]? subjects)
    {
        if (subjects is null)
        {
            return;
        }

        foreach (var subject in subjects)
        {
            if (subject is null)
            {
                continue;
            }

            subject.Id ??= subject.UserId ?? subject.GroupId ?? subject.ConnectedOrganizationId;
            subject.UserId = null;
            subject.GroupId = null;

            // Only keep managerLevel for requestorManager type
            if (subject.ODataType != "#microsoft.graph.requestorManager")
            {
                subject.ManagerLevel = 0; // Reset to default so it won't be serialized
            }
        }
    }

    private static void HydrateRequestorSubject(RequestorSubject? subject)
    {
        if (subject is null)
        {
            return;
        }

        subject.Id ??= subject.UserId ?? subject.GroupId ?? subject.ConnectedOrganizationId;
        subject.UserId = null;
        subject.GroupId = null;

        // Only keep managerLevel for requestorManager type
        if (subject.ODataType != "#microsoft.graph.requestorManager")
        {
            subject.ManagerLevel = 0; // Reset to default so it won't be serialized
        }
    }

    private static void HydrateSubjectSet(SubjectSet? subject)
    {
        if (subject is null)
        {
            return;
        }

        subject.Id ??= subject.UserId ?? subject.GroupId ?? subject.ConnectedOrganizationId;
        subject.UserId = null;
        subject.GroupId = null;

        // Only keep managerLevel for requestorManager type
        if (subject.ODataType != "#microsoft.graph.requestorManager")
        {
            subject.ManagerLevel = 0; // Reset to default so it won't be serialized
        }
    }
}
