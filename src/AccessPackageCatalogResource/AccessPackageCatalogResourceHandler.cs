using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace EntitlementManagement.AccessPackageCatalogResource;

public class AccessPackageCatalogResourceHandler : EntitlementManagementResourceHandlerBase<AccessPackageCatalogResource, AccessPackageCatalogResourceIdentifiers>
{
    protected override async Task<ResourceResponse> Preview(ResourceRequest request, CancellationToken cancellationToken)
    {
        // Fast check - no retry needed (404 = resource doesn't exist yet, that's OK in preview)
        var existing = await TryGetCatalogResourceAsync(request.Config, request.Properties, cancellationToken);

        var props = request.Properties;

        if (existing != null)
        {
            // Resource exists in catalog - populate outputs from existing resource
            props.Id = existing.id;
            props.RequestState = "Delivered";
            props.RequestStatus = "Fulfilled";
            props.DisplayName = existing.displayName;
            props.Description = existing.description;
            props.ResourceType = existing.resourceType;
            props.CreatedDateTime = existing.createdDateTime;
        }

        return GetResponse(request);
    }

    protected override async Task<ResourceResponse> CreateOrUpdate(ResourceRequest request, CancellationToken cancellationToken)
    {
        var props = request.Properties;
        // Fast check - no retry needed
        var existing = await TryGetCatalogResourceAsync(request.Config, request.Properties, cancellationToken);

        if (existing == null)
        {
            // Resource doesn't exist in catalog - create resource request to add it
            var created = await AddResourceToCatalogAsync(request.Config, props, cancellationToken);

            // Extract resource ID from the nested accessPackageResource object
            var resourceId = created.accessPackageResource?.id;

            Console.WriteLine($"DEBUG: Resource ID from API response: '{resourceId ?? "(null)"}'");

            props.Id = resourceId;
            props.RequestState = created.requestState;
            props.RequestStatus = created.requestStatus;

            // Try to get display name from the create response first
            if (created.accessPackageResource != null)
            {
                props.DisplayName = created.accessPackageResource.displayName;
                Console.WriteLine($"DEBUG: DisplayName from create response: '{props.DisplayName ?? "(null)"}'");
            }

            Console.WriteLine($"DEBUG: props.Id set to: '{props.Id ?? "(null)"}'");
            Console.WriteLine($"Resource added to catalog: ID={resourceId}");

            // If we still need details, fetch with retry logic (resource was just created)
            if (!string.IsNullOrEmpty(resourceId) && string.IsNullOrEmpty(props.DisplayName))
            {
                var resource = await GetCatalogResourceWithRetryAsync(request.Config, request.Properties, cancellationToken);
                if (resource != null)
                {
                    props.DisplayName = resource.displayName;
                    props.Description = resource.description;
                    props.ResourceType = resource.resourceType;
                    props.CreatedDateTime = resource.createdDateTime;

                    Console.WriteLine($"DEBUG: Resource details fetched with retry - DisplayName: '{resource.displayName}'");
                }
                else
                {
                    Console.WriteLine($"Warning: GetCatalogResourceWithRetryAsync returned null");
                }
            }
        }
        else
        {
            // Resource already exists in catalog - no update operation needed
            // (Resources can't be updated, only added or removed)
            Console.WriteLine($"DEBUG: Resource already exists - ID: '{existing.id ?? "(null)"}'");

            props.Id = existing.id;
            props.RequestState = "Delivered";
            props.RequestStatus = "Fulfilled";
            props.DisplayName = existing.displayName;
            props.Description = existing.description;
            props.ResourceType = existing.resourceType;
            props.CreatedDateTime = existing.createdDateTime;

            Console.WriteLine($"DEBUG: props.Id set from existing: '{props.Id ?? "(null)"}'");
        }

        Console.WriteLine($"DEBUG: Before GetResponse - props.Id = '{props.Id ?? "(null)"}'");
        var response = GetResponse(request);
        Console.WriteLine($"DEBUG: After GetResponse - returning response");

        return response;
    }

    protected override AccessPackageCatalogResourceIdentifiers GetIdentifiers(AccessPackageCatalogResource properties) => new()
    {
        CatalogId = properties.CatalogId,
        OriginId = properties.OriginId
    };

    /// <summary>
    /// Fast check if catalog resource exists. No retry logic - 404 means resource doesn't exist (expected).
    /// Use this for Preview and initial check in CreateOrUpdate.
    /// </summary>
    private async Task<CatalogResourceResponse?> TryGetCatalogResourceAsync(
        Configuration config,
        AccessPackageCatalogResource properties,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = CreateGraphClient(config, useBeta: true);

            // List resources in the catalog and filter by originId
            var filter = $"originId eq '{properties.OriginId}'";
            var response = await client.GetAsync(
                $"identityGovernance/entitlementManagement/accessPackageCatalogs/{properties.CatalogId}/accessPackageResources?$filter={Uri.EscapeDataString(filter)}",
                cancellationToken
            );

            // 404 = Resource not in catalog yet (expected in greenfield scenarios)
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GraphListResponse<CatalogResourceResponse>>(cancellationToken: cancellationToken);

            return result?.value?.FirstOrDefault();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Resource doesn't exist - that's OK
            return null;
        }
    }

    /// <summary>
    /// Get catalog resource with retry logic for replication delays.
    /// Use ONLY after creating a resource when you expect it to exist.
    /// </summary>
    private async Task<CatalogResourceResponse?> GetCatalogResourceWithRetryAsync(
        Configuration config,
        AccessPackageCatalogResource properties,
        CancellationToken cancellationToken)
    {
        // BEAST MODE: Retry up to 10 times with exponential backoff
        const int maxRetries = 10;
        int delayMs = 2000; // Start with 2 seconds

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var client = CreateGraphClient(config, useBeta: true);

                // List resources in the catalog and filter by originId
                var filter = $"originId eq '{properties.OriginId}'";
                var response = await client.GetAsync(
                    $"identityGovernance/entitlementManagement/accessPackageCatalogs/{properties.CatalogId}/accessPackageResources?$filter={Uri.EscapeDataString(filter)}",
                    cancellationToken
                );

                // 404 = Resource not in catalog yet (expected in greenfield)
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    if (attempt < maxRetries)
                    {
                        Console.WriteLine($"Catalog or resource not found (attempt {attempt}/{maxRetries}) - waiting {delayMs}ms for replication...");
                        await Task.Delay(delayMs, cancellationToken);
                        delayMs = Math.Min(delayMs * 2, 16000); // Exponential backoff, max 16s
                        continue;
                    }
                    // MAX RETRIES REACHED - THROW ERROR!
                    throw new Exception($"Catalog or resource with originId '{properties.OriginId}' not found after {maxRetries} attempts. Replication may be taking longer than expected.");
                }

                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<GraphListResponse<CatalogResourceResponse>>(cancellationToken: cancellationToken);

                // SUCCESS! Resource found
                var resource = result?.value?.FirstOrDefault();
                if (resource != null || attempt >= maxRetries)
                {
                    return resource;
                }

                // Empty result but not last attempt - might be replication delay
                Console.WriteLine($"Resource list empty (attempt {attempt}/{maxRetries}) - waiting {delayMs}ms for replication...");
                await Task.Delay(delayMs, cancellationToken);
                delayMs = Math.Min(delayMs * 2, 16000);
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                Console.WriteLine($"HTTP error during catalog GET (attempt {attempt}/{maxRetries}): {ex.Message} - retrying in {delayMs}ms...");
                await Task.Delay(delayMs, cancellationToken);
                delayMs = Math.Min(delayMs * 2, 16000);
                continue;
            }
            catch (Exception ex) when (attempt < maxRetries && (
                ex.Message.Contains("replication", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("ResourceNotFound", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"Potential replication error (attempt {attempt}/{maxRetries}): {ex.Message} - retrying in {delayMs}ms...");
                await Task.Delay(delayMs, cancellationToken);
                delayMs = Math.Min(delayMs * 2, 16000);
                continue;
            }
        }

        // BEAST MODE: If we exit the loop without returning, ALL retries failed - THROW!
        throw new Exception($"Failed to get catalog resource with originId '{properties.OriginId}' after {maxRetries} attempts. Resource may not exist or replication is taking longer than expected.");
    }

    private async Task<ResourceRequestResponse> AddResourceToCatalogAsync(
        Configuration config,
        AccessPackageCatalogResource properties,
        CancellationToken cancellationToken)
    {
        // CRITICAL: ALWAYS wait before adding to catalog (group replication delay)
        // Wait 20 seconds to ensure Groups API has replicated to Entitlement Management API
        await Task.Delay(20000, cancellationToken);

        // RETRY LOGIC: Even after wait, the catalog API might not see it yet
        // Retry the catalog add operation with exponential backoff
        int maxRetries = 10;
        int delayMs = 2000; // Start with 2 seconds

        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                Console.WriteLine($"Attempt {attempt}/{maxRetries}: Adding resource to catalog...");
                var result = await AddResourceToCatalogAttemptAsync(config, properties, cancellationToken);
                Console.WriteLine($"Successfully added resource on attempt {attempt}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Attempt {attempt} failed: {ex.Message}");
                Console.WriteLine($"Exception type: {ex.GetType().Name}");

                // BEAST MODE: Retry on ANY error that might be replication-related
                bool isReplicationError =
                    ex.Message.Contains("ResourceNotFoundInOriginSystem", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("not present", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("replication", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("unavailable", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase);

                if (isReplicationError && attempt < maxRetries)
                {
                    lastException = ex;
                    Console.WriteLine($"Potential replication error detected");
                    Console.WriteLine($"Waiting {delayMs}ms before retry {attempt + 1}/{maxRetries}...");
                    await Task.Delay(delayMs, cancellationToken);
                    delayMs = Math.Min(delayMs * 2, 16000); // Exponential backoff, max 16s
                }
                else
                {
                    Console.WriteLine($"Not retrying - either not a replication issue or max retries reached");
                    throw;
                }
            }
        }

        throw lastException ?? new Exception("Failed to add resource to catalog after retries");
    }

    private async Task<ResourceRequestResponse> AddResourceToCatalogAttemptAsync(
        Configuration config,
        AccessPackageCatalogResource properties,
        CancellationToken cancellationToken)
    {
        using var client = CreateGraphClient(config, useBeta: true);

        var requestBody = new
        {
            catalogId = properties.CatalogId,
            requestType = "AdminAdd",
            justification = properties.Justification ?? "Added via Bicep local-deploy",
            accessPackageResource = new
            {
                originId = properties.OriginId,
                originSystem = properties.OriginSystem,
                displayName = properties.DisplayName, // Optional, will be retrieved from AD if not provided
                description = properties.Description,  // Optional
            }
        };

        var json = JsonSerializer.Serialize(requestBody);

        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await client.PostAsync(
                "identityGovernance/entitlementManagement/accessPackageResourceRequests",
                content,
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"Failed to add resource to catalog:");
                Console.WriteLine($"   Status: {(int)response.StatusCode} ({response.StatusCode})");
                Console.WriteLine($"   Error Body: {errorBody}");

                // Try to parse Graph API error
                try
                {
                    var errorJson = JsonDocument.Parse(errorBody);
                    if (errorJson.RootElement.TryGetProperty("error", out var errorElement))
                    {
                        var code = errorElement.TryGetProperty("code", out var codeElement) ? codeElement.GetString() : "Unknown";
                        var message = errorElement.TryGetProperty("message", out var msgElement) ? msgElement.GetString() : errorBody;
                        throw new Exception($"Failed to add resource to catalog. Graph API Error [{code}]: {message}");
                    }
                }
                catch (JsonException)
                {
                    // JSON parsing failed, throw with raw body
                }

                throw new Exception($"Failed to add resource to catalog. Status: {response.StatusCode}, Body: {errorBody}");
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ResourceRequestResponse>(cancellationToken: cancellationToken);

            Console.WriteLine($"Resource request submitted successfully:");
            Console.WriteLine($"   Request ID: {result?.id}");
            Console.WriteLine($"   Request State: {result?.requestState}");
            Console.WriteLine($"   Request Status: {result?.requestStatus}");

            return result ?? throw new Exception("Failed to add resource to catalog - null response");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception adding resource to catalog: {ex.Message}");
            throw;
        }
    }

    private async Task RemoveResourceFromCatalogAsync(
        Configuration config,
        string catalogId,
        string resourceId,
        CancellationToken cancellationToken)
    {
        using var client = CreateGraphClient(config);

        var requestBody = new
        {
            catalogId = catalogId,
            requestType = "AdminRemove",
            accessPackageResource = new
            {
                id = resourceId
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(
            "identityGovernance/entitlementManagement/accessPackageResourceRequests",
            content,
            cancellationToken
        );

        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Verifies that a group exists in Entra ID before attempting to add it to a catalog.
    /// Retries with exponential backoff since groups might not be immediately available after creation.
    /// </summary>
    private async Task VerifyGroupExistsAsync(
        Configuration config,
        string groupId,
        CancellationToken cancellationToken)
    {
        using var groupClient = CreateGraphClient(config, useBeta: false, useGroupUserToken: true);

        int maxRetries = 15; // Increased from 5 to handle slow replication
        int delayMs = 2000; // Start with 2 seconds instead of 1

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                Console.WriteLine($"Attempt {attempt}/{maxRetries}: Checking if group {groupId} exists...");

                var response = await groupClient.GetAsync($"groups/{groupId}", cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var group = await response.Content.ReadFromJsonAsync<GroupVerifyResponse>(cancellationToken: cancellationToken);

                    // CRITICAL: Even after group is found, wait additional time for catalog API replication
                    // 20 seconds ensures Entitlement Management API has DEFINITELY replicated the group from Groups API
                    await Task.Delay(20000, cancellationToken);
                    return;
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    if (attempt < maxRetries)
                    {
                        Console.WriteLine($"Group not found yet, waiting {delayMs}ms before retry...");
                        await Task.Delay(delayMs, cancellationToken);
                        delayMs = Math.Min(delayMs * 2, 8000); // Exponential backoff, max 8s
                        continue;
                    }

                    throw new Exception($"Group {groupId} does not exist in Entra ID. Ensure the security group was created successfully before adding it to the catalog.");
                }

                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                if (attempt < maxRetries)
                {
                    Console.WriteLine($"Group not found (HTTP exception), waiting {delayMs}ms before retry...");
                    await Task.Delay(delayMs, cancellationToken);
                    delayMs = Math.Min(delayMs * 2, 8000);
                    continue;
                }

                throw new Exception($"Group {groupId} does not exist in Entra ID after {maxRetries} attempts. Ensure the security group was created successfully.", ex);
            }
        }

        throw new Exception($"Failed to verify group {groupId} exists after {maxRetries} attempts");
    }

    // Response models for Graph API
    private class GroupVerifyResponse
    {
        public string? id { get; set; }
        public string? displayName { get; set; }
    }

    private class CatalogResourceResponse
    {
        public string? id { get; set; }
        public string? displayName { get; set; }
        public string? description { get; set; }
        public string? originId { get; set; }
        public string? originSystem { get; set; }
        public string? resourceType { get; set; }
        public string? createdDateTime { get; set; }
    }

    private class ResourceRequestResponse
    {
        public string? id { get; set; }
        public string? catalogId { get; set; }
        public string? requestType { get; set; }
        public string? requestState { get; set; }
        public string? requestStatus { get; set; }

        // The resource is returned in accessPackageResource object
        public AccessPackageResourceInfo? accessPackageResource { get; set; }
    }

    private class AccessPackageResourceInfo
    {
        public string? id { get; set; }
        public string? displayName { get; set; }
        public string? originId { get; set; }
    }

    private class GraphListResponse<T>
    {
        public List<T>? value { get; set; }
    }
}
