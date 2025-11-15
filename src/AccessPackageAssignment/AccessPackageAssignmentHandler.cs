namespace EntitlementManagement.AccessPackageAssignment;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bicep.Local.Extension.Host.Handlers;

/// <summary>
/// Handler for Access Package Assignments via Assignment Requests.
///
/// IMPORTANT: Graph API doesn't create assignments directly!
/// - To CREATE: POST assignmentRequest with requestType="adminAdd"
/// - To DELETE: POST assignmentRequest with requestType="adminRemove"
///
/// Graph API v1.0 Endpoints:
/// - Create: POST /identityGovernance/entitlementManagement/assignmentRequests
/// - List assignments: GET /identityGovernance/entitlementManagement/assignments
/// - Get assignment: GET /identityGovernance/entitlementManagement/assignments/{id}
/// </summary>
public class AccessPackageAssignmentHandler : EntitlementManagementResourceHandlerBase<AccessPackageAssignment, AccessPackageAssignmentIdentifiers>
{
    protected override async Task<ResourceResponse> Preview(ResourceRequest request, CancellationToken cancellationToken)
    {
        var props = request.Properties;

        Console.WriteLine($"PREVIEW: AccessPackageId={props.AccessPackageId}, PolicyId={props.AssignmentPolicyId}, UserId={props.TargetUserId}");

        var existing = await GetAssignmentAsync(request.Config, props, cancellationToken);

        if (existing is not null)
        {
            Console.WriteLine($"PREVIEW: Found existing assignment ID={existing.id}");
            props.Id = existing.id;
            props.State = existing.state;
            // TargetUserId might be populated from objectId if email was used
            if (string.IsNullOrWhiteSpace(props.TargetUserId) && !string.IsNullOrWhiteSpace(existing.target?.objectId))
            {
                props.TargetUserId = existing.target.objectId;
            }
        }
        else
        {
            Console.WriteLine($"PREVIEW: No existing assignment found");
        }

        return GetResponse(request);
    }

    protected override async Task<ResourceResponse> CreateOrUpdate(ResourceRequest request, CancellationToken cancellationToken)
    {
        var props = request.Properties;

        Console.WriteLine($"DEBUG: props.AccessPackageId = '{props.AccessPackageId}'");
        Console.WriteLine($"DEBUG: props.AssignmentPolicyId = '{props.AssignmentPolicyId}'");
        Console.WriteLine($"DEBUG: props.TargetUserId = '{props.TargetUserId}'");
        Console.WriteLine($"DEBUG: props.Id = '{props.Id}'");

        // If props.Id is already set from Preview, assignment already exists!
        if (!string.IsNullOrWhiteSpace(props.Id))
        {
            Console.WriteLine($"Assignment already exists (ID from Preview: {props.Id}) - skipping creation");
            return GetResponse(request);
        }

        Console.WriteLine($"Checking for existing assignment: AccessPackage={props.AccessPackageId}, User={props.TargetUserId}");
        var existing = await GetAssignmentAsync(request.Config, props, cancellationToken);

        if (existing is null)
        {
            Console.WriteLine($"No existing assignment found - creating new one");
            // Create new assignment via assignment request
            await CreateAssignmentAsync(request.Config, props, cancellationToken);

            // Query for the created assignment (retry a few times as it's async)
            // Graph API can take 10-30 seconds to process assignment requests
            // Sometimes it takes up to 60+ seconds!
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(3000, cancellationToken); // Wait 3 seconds
                existing = await GetAssignmentAsync(request.Config, props, cancellationToken);
                if (existing != null) break;

                if (i % 5 == 0) // Log every 15 seconds
                {
                    Console.WriteLine($"Waiting for assignment to be created ({(i + 1) * 3}s elapsed)...");
                }
            }

            if (existing is null)
            {
                Console.WriteLine($"Assignment request succeeded but assignment not found after 90 seconds.");
                Console.WriteLine($"The assignment may still be processing in the background.");
                Console.WriteLine($"Check the Azure portal or re-run the deployment in a few minutes.");
                // Don't throw - just return without populating ID
                // This makes the deployment succeed but with a warning
                return GetResponse(request);
            }            props.Id = existing.id;
            props.State = existing.state;
            if (string.IsNullOrWhiteSpace(props.TargetUserId) && !string.IsNullOrWhiteSpace(existing.target?.objectId))
            {
                props.TargetUserId = existing.target.objectId;
            }
        }
        else
        {
            // Assignment already exists
            Console.WriteLine($"Assignment already exists (ID: {existing.id}) - no changes needed");
            props.Id = existing.id;
            props.State = existing.state;
        }

        return GetResponse(request);
    }

    protected override AccessPackageAssignmentIdentifiers GetIdentifiers(AccessPackageAssignment properties) => new()
    {
        AccessPackageId = properties.AccessPackageId,
        TargetUserId = properties.TargetUserId,
    };

    private async Task<AssignmentResponse?> GetAssignmentAsync(
        Configuration config,
        AccessPackageAssignment props,
        CancellationToken cancellationToken)
    {
        using var client = CreateGraphClient(config);

        // Determine target identifier (userId or email)
        string? targetUserId = props.TargetUserId;

        if (string.IsNullOrWhiteSpace(targetUserId) && string.IsNullOrWhiteSpace(props.TargetUserEmail))
        {
            throw new Exception("Either TargetUserId or TargetUserEmail must be provided");
        }

        // Query for existing assignments by accessPackageId and target user
        string filter;
        if (!string.IsNullOrWhiteSpace(targetUserId))
        {
            filter = $"accessPackage/id eq '{props.AccessPackageId}' and target/objectId eq '{targetUserId}'";
        }
        else
        {
            // For email-based targets, filter by accessPackage only
            filter = $"accessPackage/id eq '{props.AccessPackageId}'";
        }

        var url = $"identityGovernance/entitlementManagement/assignments?$filter={Uri.EscapeDataString(filter)}&$select=id,target,accessPackage,assignmentPolicy,state";
        Console.WriteLine($"Querying Graph API: GET {url}");

        var response = await client.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            Console.WriteLine($"Graph API returned 404 - no assignments found");
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"Graph API error {response.StatusCode}: {errorBody}");
            response.EnsureSuccessStatusCode();
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        Console.WriteLine($"Graph API response: {responseBody.Substring(0, Math.Min(500, responseBody.Length))}...");

        var result = System.Text.Json.JsonSerializer.Deserialize<ODataListResponse>(responseBody);
        Console.WriteLine($"Parsed {result?.value?.Count ?? 0} assignments");

        if (result?.value == null || result.value.Count == 0)
        {
            return null;
        }

        // If filtering by email, find matching assignment
        AssignmentResponse? existingAssignment;
        if (!string.IsNullOrWhiteSpace(props.TargetUserEmail))
        {
            existingAssignment = result.value.FirstOrDefault(a =>
                a.target?.email?.Equals(props.TargetUserEmail, StringComparison.OrdinalIgnoreCase) == true
            );
        }
        else
        {
            existingAssignment = result.value.FirstOrDefault();
        }

        if (existingAssignment == null)
        {
            return null;
        }

        // Check if it's in a valid state (not expired/delivering/failed)
        // Graph API returns lowercase "delivered", so use case-insensitive comparison
        if (!string.Equals(existingAssignment.state, "delivered", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Found assignment {existingAssignment.id} but state is '{existingAssignment.state}' (not 'delivered') - treating as non-existent");
            return null; // Treat as non-existent if not delivered
        }

        Console.WriteLine($"Found existing delivered assignment: ID={existingAssignment.id}, State={existingAssignment.state}");
        return existingAssignment;
    }

    private async Task CreateAssignmentAsync(
        Configuration config,
        AccessPackageAssignment props,
        CancellationToken cancellationToken)
    {
        using var client = CreateGraphClient(config);

        // Build the request body with optional schedule
        var requestBody = new
        {
            requestType = "adminAdd",
            assignment = new
            {
                targetId = props.TargetUserId, // Can be null if using email
                target = string.IsNullOrWhiteSpace(props.TargetUserEmail) ? null : new
                {
                    email = props.TargetUserEmail
                },
                assignmentPolicyId = props.AssignmentPolicyId,
                accessPackageId = props.AccessPackageId
            },
            justification = props.Justification,
            // ADD SCHEDULE SUPPORT! Time-bound assignments!
            schedule = props.Schedule is null ? null : new
            {
                startDateTime = props.Schedule.StartDateTime,
                recurrence = (object?)null,  // Must be explicitly null per Graph API docs
                expiration = props.Schedule.Expiration is null ? null : new
                {
                    endDateTime = props.Schedule.Expiration.EndDateTime,  // Graph API expects camelCase JSON
                    duration = props.Schedule.Expiration.Duration,
                    type = props.Schedule.Expiration.Type
                }
            }
        };

        Console.WriteLine($"Creating assignment request with schedule: {(props.Schedule != null ? "YES" : "NO")}");
        if (props.Schedule?.StartDateTime != null)
        {
            Console.WriteLine($"   ⏰ Start: {props.Schedule.StartDateTime}");
        }
        if (props.Schedule?.Expiration?.EndDateTime != null)
        {
            Console.WriteLine($"   End: {props.Schedule.Expiration.EndDateTime}");
        }
        if (props.Schedule?.Expiration?.Duration != null)
        {
            Console.WriteLine($"   Duration: {props.Schedule.Expiration.Duration}");
        }

        // LOG THE FULL REQUEST BODY FOR DEBUGGING
        var requestBodyJson = System.Text.Json.JsonSerializer.Serialize(requestBody, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine($"Request Body:\n{requestBodyJson}");

        var response = await client.PostAsJsonAsync(
            "identityGovernance/entitlementManagement/assignmentRequests",
            requestBody,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase },
            cancellationToken
        );

        // 409 Conflict = assignment request already exists (idempotent - this is OK!)
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            Console.WriteLine($"Assignment request already exists (409 Conflict) - treating as success");
            return;
        }

        // 400 BadRequest with "ExistingOpenRequest" = pending assignment request still being processed (idempotent - OK!)
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (errorBody.Contains("ExistingOpenRequest", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Assignment request already pending (ExistingOpenRequest) - treating as success");
                return;
            }

            // Other BadRequest errors should still fail
            Console.WriteLine($"Graph API Error Response:\n{errorBody}");
            throw new Exception($"Failed to create assignment request (HTTP {response.StatusCode}): {errorBody}");
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"Graph API Error Response:\n{errorBody}");
            throw new Exception($"Failed to create assignment request (HTTP {response.StatusCode}): {errorBody}");
        }

        response.EnsureSuccessStatusCode();

        Console.WriteLine($"Created assignment request");
    }

    private async Task DeleteAssignmentAsync(
        Configuration config,
        string assignmentId,
        CancellationToken cancellationToken)
    {
        using var client = CreateGraphClient(config);

        // Create removal request
        var requestBody = new
        {
            requestType = "adminRemove",
            assignment = new
            {
                id = assignmentId
            }
        };

        var response = await client.PostAsJsonAsync(
            "identityGovernance/entitlementManagement/assignmentRequests",
            requestBody,
            cancellationToken
        );

        // 404 is OK - assignment already gone
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            Console.WriteLine($"Assignment {assignmentId} not found (may have been already deleted)");
            return;
        }

        response.EnsureSuccessStatusCode();

        Console.WriteLine($"Deleted assignment {assignmentId}");
    }

    // ===== HELPER CLASSES FOR JSON RESPONSES =====

    private class ODataListResponse
    {
        [JsonPropertyName("value")]
        public List<AssignmentResponse>? value { get; set; }
    }

    private class AssignmentResponse
    {
        [JsonPropertyName("id")]
        public string? id { get; set; }

        [JsonPropertyName("state")]
        public string? state { get; set; }

        [JsonPropertyName("target")]
        public TargetInfo? target { get; set; }

        [JsonPropertyName("accessPackage")]
        public PackageInfo? accessPackage { get; set; }

        [JsonPropertyName("assignmentPolicy")]
        public PolicyInfo? assignmentPolicy { get; set; }
    }

    private class TargetInfo
    {
        [JsonPropertyName("objectId")]
        public string? objectId { get; set; }

        [JsonPropertyName("email")]
        public string? email { get; set; }
    }

    private class PackageInfo
    {
        [JsonPropertyName("id")]
        public string? id { get; set; }
    }

    private class PolicyInfo
    {
        [JsonPropertyName("id")]
        public string? id { get; set; }
    }
}
