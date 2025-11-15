namespace EntitlementManagement.SecurityGroup;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bicep.Local.Extension.Host.Handlers;

/// <summary>
/// Handler for Entra ID Security Groups via Microsoft Graph API.
/// Uses GroupUserToken (requires Group.ReadWrite.All permission).
///
/// Graph API v1.0 Endpoints:
/// - Create: POST /groups
/// - Get: GET /groups?$filter=mailNickname eq '{uniqueName}'
/// - Update: PATCH /groups/{id}
/// - Delete: DELETE /groups/{id}
///
/// Idempotency: Uses uniqueName (mailNickname) as immutable identifier.
/// </summary>
public class SecurityGroupHandler : GroupUserResourceHandlerBase<SecurityGroup, SecurityGroupIdentifiers>
{
    protected override async Task<ResourceResponse> Preview(ResourceRequest request, CancellationToken cancellationToken)
    {
        var props = request.Properties;

        Console.WriteLine($"PREVIEW: SecurityGroup uniqueName={props.UniqueName}, displayName={props.DisplayName}");

        var existing = await GetGroupByUniqueNameAsync(request.Config, props.UniqueName, cancellationToken);

        if (existing is not null)
        {
            Console.WriteLine($"PREVIEW: Found existing group ID={existing.id}");
            props.Id = existing.id;
            props.CreatedDateTime = existing.createdDateTime;
        }
        else
        {
            Console.WriteLine($"PREVIEW: No existing group found");
        }

        return GetResponse(request);
    }

    protected override async Task<ResourceResponse> CreateOrUpdate(ResourceRequest request, CancellationToken cancellationToken)
    {
        var props = request.Properties;

        Console.WriteLine($"CREATE/UPDATE: uniqueName={props.UniqueName}, displayName={props.DisplayName}");

        // Check if group already exists by uniqueName (immutable identifier)
        var existing = await GetGroupByUniqueNameAsync(request.Config, props.UniqueName, cancellationToken);

        if (existing is null)
        {
            Console.WriteLine($"No existing group found - creating new one");
            // Create new group
            existing = await CreateGroupAsync(request.Config, props, cancellationToken);
        }
        else
        {
            Console.WriteLine($"Group exists - updating displayName and description");
            // Update existing group (displayName and description can change, mailNickname cannot)
            await UpdateGroupAsync(request.Config, existing.id!, props, cancellationToken);

            // FIX: Ensure uniqueName is populated after update
            // Graph API might not return it, so populate from mailNickname or props
            if (string.IsNullOrEmpty(existing.uniqueName))
            {
                existing.uniqueName = existing.GetUniqueName() ?? props.UniqueName;
            }
        }

        // Populate outputs
        props.Id = existing.id;
        props.CreatedDateTime = existing.createdDateTime;

        Console.WriteLine($"Group ready: ID={props.Id}, UniqueName={existing.GetUniqueName() ?? existing.mailNickname}");
        Console.WriteLine($"   DisplayName={existing.displayName}");
        Console.WriteLine($"   MailNickname={existing.mailNickname}");
        Console.WriteLine($"   CreatedDateTime={existing.createdDateTime}");

        // MEMBER MANAGEMENT - Sync members array
        if (props.Members is { Length: > 0 } memberIds && !string.IsNullOrEmpty(props.Id))
        {
            Console.WriteLine($"Syncing {memberIds.Length} members to security group");
            await SyncGroupMembersAsync(request.Config, props.Id, memberIds, cancellationToken);
        }

        return GetResponse(request);
    }

    protected override SecurityGroupIdentifiers GetIdentifiers(SecurityGroup properties) => new()
    {
        UniqueName = properties.UniqueName
    };

    private async Task<GroupResponse?> GetGroupByUniqueNameAsync(
        Configuration config,
        string uniqueName,
        CancellationToken cancellationToken)
    {
        // Query by mailNickname (which is what uniqueName maps to in Graph API)
        // Graph API Beta has uniqueName property but it's always NULL!
        // The REAL immutable identifier is mailNickname
        using var client = CreateGraphClient(config, useBeta: true);

        // Query by mailNickname (the actual immutable identifier in Graph API)
        var filter = $"mailNickname eq '{Uri.EscapeDataString(uniqueName)}'";
        var url = $"groups?$filter={filter}";

        Console.WriteLine($"Querying Graph API (BETA): GET {url}");

        var response = await client.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            Console.WriteLine($"Graph API returned 404 - no groups found");
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"Graph API error {response.StatusCode}: {errorBody}");
            response.EnsureSuccessStatusCode();
        }

        // DEBUG: Log raw JSON response to see what Graph API returns!
        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
        Console.WriteLine($"RAW GRAPH API QUERY RESPONSE (first 500 chars): {rawJson.Substring(0, Math.Min(500, rawJson.Length))}");

        var result = System.Text.Json.JsonSerializer.Deserialize<ODataListResponse<GroupResponse>>(rawJson, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false
        });

        if (result?.value == null || result.value.Count == 0)
        {
            Console.WriteLine($"No group found with uniqueName '{uniqueName}'");
            return null;
        }

        // Return first match (uniqueName should be unique)
        var group = result.value.FirstOrDefault();

        if (group is null)
        {
            Console.WriteLine($"No matching group found in results");
            return null;
        }

        Console.WriteLine($"Found group: ID={group.id}, DisplayName={group.displayName}, UniqueName={group.GetUniqueName()}");
        return group;
    }

    private async Task<GroupResponse> CreateGroupAsync(
        Configuration config,
        SecurityGroup props,
        CancellationToken cancellationToken)
    {
        // CORRECT PATTERN FOR MICROSOFT GRAPH GROUPS WITH uniqueName:
        // 1. POST /groups to create (returns generated ID)
        // 2. Immediately PATCH /groups/{id} to set uniqueName property
        // 3. uniqueName is a Bicep/Graph extension property for idempotency
        //
        // See: https://learn.microsoft.com/en-us/graph/templates/bicep/how-to-reference-existing-resources
        // "Set the uniqueName property for an existing group"
        using var client = CreateGraphClient(config, useBeta: false); // v1.0 supports uniqueName!

        var requestBody = new
        {
            displayName = props.DisplayName,
            mailNickname = props.UniqueName,  // mailNickname must match uniqueName for consistency
            description = props.Description ?? string.Empty,
            mailEnabled = props.MailEnabled,
            securityEnabled = props.SecurityEnabled,
            groupTypes = Array.Empty<string>()
        };

        Console.WriteLine($"Creating group: displayName={props.DisplayName}, mailNickname={props.UniqueName}");

        // Step 1: Create group with POST /groups
        var createResponse = await client.PostAsJsonAsync("groups", requestBody, cancellationToken);

        if (!createResponse.IsSuccessStatusCode)
        {
            var errorBody = await createResponse.Content.ReadAsStringAsync(cancellationToken);

            // Handle conflict (group already exists with same mailNickname)
            if (createResponse.StatusCode == HttpStatusCode.Conflict || createResponse.StatusCode == HttpStatusCode.BadRequest)
            {
                Console.WriteLine($"Group might already exist - attempting to retrieve by mailNickname");
                await Task.Delay(1000, cancellationToken); // Brief delay for consistency
                var existing = await GetGroupByUniqueNameAsync(config, props.UniqueName, cancellationToken);
                if (existing is not null)
                {
                    Console.WriteLine($"Retrieved existing group (ID={existing.id})");
                    return existing;
                }
            }

            Console.WriteLine($"Failed to create group: {createResponse.StatusCode} {errorBody}");
            createResponse.EnsureSuccessStatusCode();
        }

        // Parse created group response
        var rawJson = await createResponse.Content.ReadAsStringAsync(cancellationToken);
        var createdGroup = System.Text.Json.JsonSerializer.Deserialize<GroupResponse>(rawJson, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false
        });

        if (createdGroup is null || string.IsNullOrEmpty(createdGroup.id))
        {
            throw new Exception("Failed to deserialize created group or missing ID");
        }

        Console.WriteLine($"Created group: ID={createdGroup.id}, DisplayName={createdGroup.displayName}");

        // Step 2: PATCH /groups/{id} to set uniqueName property
        // This is REQUIRED for Bicep Graph extension compatibility!
        Console.WriteLine($"CRITICAL: Setting uniqueName property via PATCH /groups/{createdGroup.id}");
        Console.WriteLine($"PATCH URL: groups/{createdGroup.id}");
        Console.WriteLine($"PATCH BODY: {{\"uniqueName\": \"{props.UniqueName}\"}}");

        var uniqueNameBody = new
        {
            uniqueName = props.UniqueName  // This makes the group findable via Bicep!
        };

        var patchResponse = await client.PatchAsJsonAsync($"groups/{createdGroup.id}", uniqueNameBody, cancellationToken);

        Console.WriteLine($"PATCH RESPONSE STATUS: {patchResponse.StatusCode}");

        if (!patchResponse.IsSuccessStatusCode)
        {
            var patchError = await patchResponse.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"ERROR: CRITICAL ERROR: Failed to set uniqueName!");
            Console.WriteLine($"Status: {patchResponse.StatusCode}");
            Console.WriteLine($"Error Body: {patchError}");

            // FAIL THE DEPLOYMENT if uniqueName can't be set!
            throw new Exception($"Failed to set uniqueName property: {patchResponse.StatusCode} - {patchError}");
        }
        else
        {
            Console.WriteLine($"SUCCESS: SUCCESS: uniqueName set to '{props.UniqueName}'");
            // Update our local object to reflect the uniqueName
            createdGroup.uniqueName = props.UniqueName;
        }

        return createdGroup;
    }

    private async Task UpdateGroupAsync(
        Configuration config,
        string groupId,
        SecurityGroup props,
        CancellationToken cancellationToken)
    {
        using var client = CreateGraphClient(config);

        var requestBody = new
        {
            displayName = props.DisplayName,
            description = props.Description ?? string.Empty
        };

        Console.WriteLine($"Updating group: ID={groupId}, displayName={props.DisplayName}");

        var response = await client.PatchAsJsonAsync($"groups/{groupId}", requestBody, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"Failed to update group: {response.StatusCode} {errorBody}");
            response.EnsureSuccessStatusCode();
        }

        Console.WriteLine($"Updated group: ID={groupId}");
    }

    private async Task DeleteGroupAsync(
        Configuration config,
        string groupId,
        CancellationToken cancellationToken)
    {
        using var client = CreateGraphClient(config);

        Console.WriteLine($"Deleting group: ID={groupId}");

        var response = await client.DeleteAsync($"groups/{groupId}", cancellationToken);

        // 404 is OK - group already deleted
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            Console.WriteLine($"Group already deleted (404)");
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"Failed to delete group: {response.StatusCode} {errorBody}");
            response.EnsureSuccessStatusCode();
        }

        Console.WriteLine($"Deleted group: ID={groupId}");
    }

    /// <summary>
    /// Adds members (users or groups) to the specified group.
    /// Graph API: POST /v1.0/groups/{groupId}/members/$ref
    /// Max 20 members per request - batches automatically.
    /// </summary>
    private async Task AddMembersToGroupAsync(
        Configuration config,
        string groupId,
        string[] memberIds,
        CancellationToken cancellationToken)
    {
        if (memberIds == null || memberIds.Length == 0)
        {
            Console.WriteLine($"No members to add");
            return;
        }

        using var client = CreateGraphClient(config);

        Console.WriteLine($"Adding {memberIds.Length} members to group {groupId}");

        // Process in batches of 20 (Graph API limit)
        var batches = memberIds
            .Select((id, index) => new { id, index })
            .GroupBy(x => x.index / 20)
            .Select(g => g.Select(x => x.id).ToArray());

        foreach (var batch in batches)
        {
            Console.WriteLine($"Processing batch of {batch.Length} members");

            foreach (var memberId in batch)
            {
                // Microsoft Graph requires full OData reference URL
                // Can use /directoryObjects/{id} to support users, groups, service principals
                // Using directoryObjects is most flexible (supports users, groups, devices, service principals)
                var requestBody = new Dictionary<string, string>
                {
                    ["@odata.id"] = $"https://graph.microsoft.com/v1.0/directoryObjects/{memberId}"
                };

                Console.WriteLine($"Adding member: {memberId}");

                var response = await client.PostAsJsonAsync($"groups/{groupId}/members/$ref", requestBody, cancellationToken);

                // Handle 400 error - member already exists (this is OK, idempotent!)
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (errorBody.Contains("already exist") || errorBody.Contains("already a member"))
                    {
                        Console.WriteLine($"Member {memberId} already exists in group - skipping");
                        continue;
                    }

                    // Some other 400 error - fail
                    Console.WriteLine($"Failed to add member {memberId}: {response.StatusCode} {errorBody}");
                    response.EnsureSuccessStatusCode();
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    Console.WriteLine($"Failed to add member {memberId}: {response.StatusCode} {errorBody}");
                    response.EnsureSuccessStatusCode();
                }

                Console.WriteLine($"Added member: {memberId}");
            }
        }

        Console.WriteLine($"All members added to group {groupId}");
    }

    /// <summary>
    /// Removes members from the group that are not in the desired member list.
    /// Used for idempotency - ensures group has EXACTLY the members specified.
    /// Graph API: DELETE /v1.0/groups/{groupId}/members/{memberId}/$ref
    /// </summary>
    private async Task SyncGroupMembersAsync(
        Configuration config,
        string groupId,
        string[] desiredMemberIds,
        CancellationToken cancellationToken)
    {
        using var client = CreateGraphClient(config);

        Console.WriteLine($"Syncing members for group {groupId}");

        // Get current members
        var response = await client.GetAsync($"groups/{groupId}/members?$select=id", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"Failed to get current members: {response.StatusCode} {errorBody}");
            response.EnsureSuccessStatusCode();
        }

        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = System.Text.Json.JsonSerializer.Deserialize<ODataListResponse<MemberResponse>>(rawJson, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false
        });

        var currentMemberIds = result?.value?.Select(m => m.id).Where(id => !string.IsNullOrEmpty(id)).ToHashSet() ?? new HashSet<string>();

        Console.WriteLine($"Current members: {currentMemberIds.Count}");
        Console.WriteLine($"Desired members: {desiredMemberIds.Length}");

        // Find members to remove (in current but not in desired)
        var membersToRemove = currentMemberIds.Except(desiredMemberIds).ToList();

        // Find members to add (in desired but not in current)
        var membersToAdd = desiredMemberIds.Except(currentMemberIds).ToArray();

        // Remove extra members
        foreach (var memberId in membersToRemove)
        {
            Console.WriteLine($"Removing member: {memberId}");

            var deleteResponse = await client.DeleteAsync($"groups/{groupId}/members/{memberId}/$ref", cancellationToken);

            // 404 is OK - member already removed
            if (deleteResponse.StatusCode == HttpStatusCode.NotFound)
            {
                Console.WriteLine($"Member {memberId} already removed (404)");
                continue;
            }

            if (!deleteResponse.IsSuccessStatusCode)
            {
                var errorBody = await deleteResponse.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"Failed to remove member {memberId}: {deleteResponse.StatusCode} {errorBody}");
                deleteResponse.EnsureSuccessStatusCode();
            }

            Console.WriteLine($"Removed member: {memberId}");
        }

        // Add missing members
        if (membersToAdd.Length > 0)
        {
            await AddMembersToGroupAsync(config, groupId, membersToAdd, cancellationToken);
        }

        Console.WriteLine($"Group members synced: {desiredMemberIds.Length} members");
    }

    // Response models
    private class ODataListResponse<T>
    {
        [JsonPropertyName("value")]
        public List<T>? value { get; set; }
    }

    private class MemberResponse
    {
        [JsonPropertyName("id")]
        public string? id { get; set; }
    }

    private class GroupResponse
    {
        [JsonPropertyName("id")]
        public string? id { get; set; }

        [JsonPropertyName("displayName")]
        public string? displayName { get; set; }

        [JsonPropertyName("mailNickname")]
        public string? mailNickname { get; set; }

        [JsonPropertyName("createdDateTime")]
        public string? createdDateTime { get; set; }

        // FIX: uniqueName can be in root OR in AdditionalProperties (depends on Graph API version/client)
        // Some clients (like PowerShell SDK) put it in AdditionalProperties
        // Raw Graph API puts it in root object
        [JsonPropertyName("uniqueName")]
        public string? uniqueName { get; set; }

        // AdditionalProperties might contain uniqueName if not in root
        [JsonExtensionData]
        public Dictionary<string, object>? AdditionalProperties { get; set; }

        // Helper to get uniqueName from either location
        public string? GetUniqueName()
        {
            // Try root property first
            if (!string.IsNullOrEmpty(uniqueName))
                return uniqueName;

            // Try AdditionalProperties next
            if (AdditionalProperties?.TryGetValue("uniqueName", out var uniqueNameObj) == true)
            {
                return uniqueNameObj?.ToString();
            }

            // Fallback to mailNickname (should be the same)
            return mailNickname;
        }
    }
}
