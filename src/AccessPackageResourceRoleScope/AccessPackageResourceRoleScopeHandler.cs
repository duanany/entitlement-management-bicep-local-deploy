using System.Net.Http.Json;
using System.Text.Json;

namespace EntitlementManagement.AccessPackageResourceRoleScope;

/// <summary>
/// Handler for access package resource role scopes.
/// Adds resource roles (like "Member" of a group) to access packages.
/// </summary>
public class AccessPackageResourceRoleScopeHandler
    : EntitlementManagementResourceHandlerBase<AccessPackageResourceRoleScope, AccessPackageResourceRoleScopeIdentifiers>
{
    protected override async Task<ResourceResponse> Preview(ResourceRequest request, CancellationToken cancellationToken)
    {
        var props = request.Properties;
        var config = request.Config;

        // Check if role scope already exists
        using var client = CreateGraphClient(config, useBeta: true);

        var existingRoleScope = await GetAccessPackageResourceRoleScopeAsync(
            client,
            props.AccessPackageId,
            props.ResourceOriginId,
            props.RoleOriginId,
            cancellationToken
        );

        if (existingRoleScope != null)
        {
            // Role scope exists - populate outputs
            props.Id = existingRoleScope.id;
            props.CreatedDateTime = existingRoleScope.createdDateTime;

            Console.WriteLine($"Resource role scope already exists:");
            Console.WriteLine($"   ID: {existingRoleScope.id}");
            Console.WriteLine($"   Role: {props.RoleDisplayName ?? props.RoleOriginId}");
        }
        else
        {
            Console.WriteLine($"Resource role scope does not exist - will be created");
        }

        return GetResponse(request);
    }

    protected override async Task<ResourceResponse> CreateOrUpdate(ResourceRequest request, CancellationToken cancellationToken)
    {
        var props = request.Properties;
        var config = request.Config;

        using var client = CreateGraphClient(config, useBeta: true);

        // Check if role scope already exists
        var existingRoleScope = await GetAccessPackageResourceRoleScopeAsync(
            client,
            props.AccessPackageId,
            props.ResourceOriginId,
            props.RoleOriginId,
            cancellationToken
        );

        if (existingRoleScope != null)
        {
            // Already exists - idempotent
            props.Id = existingRoleScope.id;
            props.CreatedDateTime = existingRoleScope.createdDateTime;

            Console.WriteLine($"Resource role scope already exists - no changes needed:");
            Console.WriteLine($"   ID: {existingRoleScope.id}");
            Console.WriteLine($"   Role: {props.RoleDisplayName ?? props.RoleOriginId}");

            return GetResponse(request);
        }

        // Create new role scope
        var createdRoleScope = await AddResourceRoleScopeToAccessPackageAsync(
            client,
            props,
            cancellationToken
        );

        props.Id = createdRoleScope.id;
        props.CreatedDateTime = createdRoleScope.createdDateTime;

        Console.WriteLine($"Resource role scope created successfully:");
        Console.WriteLine($"   ID: {createdRoleScope.id}");
        Console.WriteLine($"   Role: {props.RoleDisplayName ?? props.RoleOriginId}");
        Console.WriteLine($"   Resource: {props.ResourceOriginId}");

        return GetResponse(request);
    }

    protected override AccessPackageResourceRoleScopeIdentifiers GetIdentifiers(AccessPackageResourceRoleScope properties) => new()
    {
        AccessPackageId = properties.AccessPackageId,
        ResourceOriginId = properties.ResourceOriginId,
        RoleOriginId = properties.RoleOriginId
    };

    /// <summary>
    /// Gets a resource role scope from an access package by matching resource and role origin IDs.
    /// Includes retry logic to handle Entra ID replication delays.
    /// </summary>
    private async Task<AccessPackageResourceRoleScopeResponse?> GetAccessPackageResourceRoleScopeAsync(
        HttpClient client,
        string accessPackageId,
        string resourceOriginId,
        string roleOriginId,
        CancellationToken cancellationToken)
    {
        // Retry up to 10 times with 2-second delays to handle replication lag
        const int maxRetries = 10;
        const int delaySeconds = 2;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await client.GetAsync(
                    $"identityGovernance/entitlementManagement/accessPackages/{accessPackageId}/accessPackageResourceRoleScopes?$expand=accessPackageResourceRole($expand=accessPackageResource),accessPackageResourceScope",
                    cancellationToken
                );

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    if (attempt < maxRetries)
                    {
                        Console.WriteLine($"Access package not found (attempt {attempt}/{maxRetries}) - waiting {delaySeconds}s for replication...");
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                        continue;
                    }
                    return null;
                }

                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<AccessPackageResourceRoleScopeListResponse>(cancellationToken: cancellationToken);

                // Find matching role scope by resource originId and role originId
                var matchingRoleScope = result?.value?.FirstOrDefault(rs =>
                    rs.accessPackageResourceScope?.originId == resourceOriginId &&
                    rs.accessPackageResourceRole?.originId == roleOriginId
                );

                return matchingRoleScope;
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                // Retry on any HTTP error (could be replication-related)
                Console.WriteLine($"HTTP error during GET (attempt {attempt}/{maxRetries}): {ex.Message} - waiting {delaySeconds}s for replication...");
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                continue;
            }
            catch (Exception ex) when (attempt < maxRetries && (
                ex.Message.Contains("replication") ||
                ex.Message.Contains("not found") ||
                ex.Message.Contains("does not exist") ||
                ex.Message.Contains("invalid")))
            {
                // Retry on errors that might be replication-related
                Console.WriteLine($"Potential replication error (attempt {attempt}/{maxRetries}): {ex.Message} - waiting {delaySeconds}s...");
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                continue;
            }
        }

        // BEAST MODE: If we exhausted all retries without finding the role scope, THROW!
        throw new Exception($"Failed to get access package resource role scope with resource originId '{resourceOriginId}' and role originId '{roleOriginId}' after {maxRetries} attempts. Resource may not exist or replication is taking longer than expected.");
    }

    /// <summary>
    /// Adds a resource role scope to an access package.
    /// </summary>
    private async Task<AccessPackageResourceRoleScopeResponse> AddResourceRoleScopeToAccessPackageAsync(
        HttpClient client,
        AccessPackageResourceRoleScope properties,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Adding resource role scope to access package:");
        Console.WriteLine($"   Access Package ID: {properties.AccessPackageId}");
        Console.WriteLine($"   Resource Origin ID: {properties.ResourceOriginId}");
        Console.WriteLine($"   Role Origin ID: {properties.RoleOriginId}");
        Console.WriteLine($"   Role Display Name: {properties.RoleDisplayName}");

        // Convert enum to string for Graph API
        var originSystemString = properties.ResourceOriginSystem?.ToString() ?? "AadGroup";

        // AUTO-RESOLVE catalogResourceId if not provided
        // We need the catalog resource ID from the access package's catalog
        string? catalogResourceId = properties.CatalogResourceId;

        if (string.IsNullOrEmpty(catalogResourceId))
        {
            Console.WriteLine($"CatalogResourceId not provided - auto-resolving from access package catalog...");
            catalogResourceId = await ResolveCatalogResourceIdAsync(client, properties, cancellationToken);
            Console.WriteLine($"   Resolved Catalog Resource ID: {catalogResourceId}");
        }

        // Construct request body
        var requestBody = new
        {
            accessPackageResourceRole = new
            {
                originId = properties.RoleOriginId,
                displayName = properties.RoleDisplayName ?? "",
                originSystem = originSystemString,
                accessPackageResource = new
                {
                    id = catalogResourceId,
                    originId = properties.ResourceOriginId,
                    originSystem = originSystemString
                }
            },
            accessPackageResourceScope = new
            {
                originId = properties.ResourceOriginId,
                originSystem = originSystemString
            }
        };

        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine($"   Request Body: {json}");

        var response = await client.PostAsJsonAsync(
            $"identityGovernance/entitlementManagement/accessPackages/{properties.AccessPackageId}/accessPackageResourceRoleScopes",
            requestBody,
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"Failed to add resource role scope:");
            Console.WriteLine($"   Status: {(int)response.StatusCode} ({response.StatusCode})");
            Console.WriteLine($"   Response: {errorBody}");
        }

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AccessPackageResourceRoleScopeResponse>(cancellationToken: cancellationToken)
            ?? throw new Exception("Failed to parse resource role scope response");

        Console.WriteLine($"Resource role scope added successfully:");
        Console.WriteLine($"   ID: {result.id}");
        Console.WriteLine($"   Created: {result.createdDateTime}");

        return result;
    }

    /// <summary>
    /// Resolves the catalog resource ID by looking up the resource in the access package's catalog.
    /// This allows us to avoid requiring catalogResourceId as input - we can auto-resolve it!
    /// </summary>
    private async Task<string> ResolveCatalogResourceIdAsync(
        HttpClient client,
        AccessPackageResourceRoleScope properties,
        CancellationToken cancellationToken)
    {
        // Step 1: Get the access package to find its catalog ID
        // Use $select to get catalog property (it's a navigation property)
        var accessPackageResponse = await client.GetAsync(
            $"identityGovernance/entitlementManagement/accessPackages/{properties.AccessPackageId}?$select=id,displayName,catalogId",
            cancellationToken
        );

        accessPackageResponse.EnsureSuccessStatusCode();

        var accessPackage = await accessPackageResponse.Content.ReadFromJsonAsync<AccessPackageResponse>(cancellationToken: cancellationToken)
            ?? throw new Exception($"Failed to get access package {properties.AccessPackageId}");

        var catalogId = accessPackage.catalogId
            ?? throw new Exception($"Access package {properties.AccessPackageId} has no catalogId property");

        Console.WriteLine($"   Access Package Catalog ID: {catalogId}");

        // Step 2: Find the catalog resource by originId in that catalog
        var filter = $"originId eq '{properties.ResourceOriginId}'";
        var catalogResourcesResponse = await client.GetAsync(
            $"identityGovernance/entitlementManagement/accessPackageCatalogs/{catalogId}/accessPackageResources?$filter={Uri.EscapeDataString(filter)}",
            cancellationToken
        );

        catalogResourcesResponse.EnsureSuccessStatusCode();

        var catalogResources = await catalogResourcesResponse.Content.ReadFromJsonAsync<CatalogResourceListResponse>(cancellationToken: cancellationToken);

        var catalogResource = catalogResources?.value?.FirstOrDefault()
            ?? throw new Exception($"Catalog resource with originId '{properties.ResourceOriginId}' not found in catalog {catalogId}. Make sure the resource was added to the catalog first.");

        return catalogResource.id
            ?? throw new Exception($"Catalog resource found but has no ID");
    }

    // Response models

    private class AccessPackageResourceRoleScopeListResponse
    {
        public List<AccessPackageResourceRoleScopeResponse>? value { get; set; }
    }

    private class AccessPackageResourceRoleScopeResponse
    {
        public string? id { get; set; }
        public string? createdDateTime { get; set; }
        public AccessPackageResourceRoleResponse? accessPackageResourceRole { get; set; }
        public AccessPackageResourceScopeResponse? accessPackageResourceScope { get; set; }
    }

    private class AccessPackageResourceRoleResponse
    {
        public string? id { get; set; }
        public string? originId { get; set; }
        public string? displayName { get; set; }
        public string? originSystem { get; set; }
        public AccessPackageResourceResponse? accessPackageResource { get; set; }
    }

    private class AccessPackageResourceScopeResponse
    {
        public string? id { get; set; }
        public string? originId { get; set; }
        public string? originSystem { get; set; }
    }

    private class AccessPackageResourceResponse
    {
        public string? id { get; set; }
        public string? originId { get; set; }
        public string? originSystem { get; set; }
    }

    private class AccessPackageResponse
    {
        public string? id { get; set; }
        public string? displayName { get; set; }
        public string? catalogId { get; set; }
    }

    private class CatalogResourceListResponse
    {
        public List<CatalogResourceResponse>? value { get; set; }
    }

    private class CatalogResourceResponse
    {
        public string? id { get; set; }
        public string? originId { get; set; }
        public string? displayName { get; set; }
    }
}
