using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bicep.Local.Extension.Host.Handlers;

namespace EntitlementManagement;

/// <summary>
/// Base handler for Entitlement Management resources (catalogs, access packages, assignments, policies).
/// Uses EntitlementToken (requires EntitlementManagement.ReadWrite.All permission).
/// </summary>
public abstract class EntitlementManagementResourceHandlerBase<TProps, TIdentifiers>
    : TypedResourceHandler<TProps, TIdentifiers, Configuration>
    where TProps : class
    where TIdentifiers : class
{
    protected HttpClient CreateGraphClient(Configuration config, bool useBeta = false, bool useGroupUserToken = false)
    {
        var baseUrl = useBeta
            ? "https://graph.microsoft.com/beta/"
            : "https://graph.microsoft.com/v1.0/";

        var client = new HttpClient
        {
            BaseAddress = new Uri(config.GraphApiBaseUrl ?? baseUrl),
            Timeout = TimeSpan.FromSeconds(120)  // Increased to 120s for PIM policy patching workflows
        };

        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        // Allow switching between EntitlementToken and GroupUserToken
        string? token;
        if (useGroupUserToken)
        {
            token = config.GroupUserToken ?? Environment.GetEnvironmentVariable("GROUP_USER_TOKEN");
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new Exception("Group/User token is required. Set via 'groupUserToken' in extension configuration or GROUP_USER_TOKEN environment variable.");
            }
        }
        else
        {
            token = config.EntitlementToken ?? Environment.GetEnvironmentVariable("ENTITLEMENT_TOKEN");
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new Exception("Entitlement Management token is required. Set via 'entitlementToken' in extension configuration or ENTITLEMENT_TOKEN environment variable.");
            }
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    protected static async Task<T?> HandleGraphApiCall<T>(
        Func<Task<HttpResponseMessage>> apiCall,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await apiCall();

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return default; // Resource doesn't exist (OK for GET operations)
            }

            if (!response.IsSuccessStatusCode)
            {
                // Read the error response body for detailed error information
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

                Console.WriteLine($"Graph API Error Response:");
                Console.WriteLine($"   Status Code: {(int)response.StatusCode} ({response.StatusCode})");
                Console.WriteLine($"   Response Body: {errorBody}");

                // Try to parse Graph API error format
                try
                {
                    var errorJson = JsonDocument.Parse(errorBody);
                    if (errorJson.RootElement.TryGetProperty("error", out var errorElement))
                    {
                        var code = errorElement.TryGetProperty("code", out var codeElement) ? codeElement.GetString() : "Unknown";
                        var message = errorElement.TryGetProperty("message", out var msgElement) ? msgElement.GetString() : errorBody;

                        Console.WriteLine($"   Error Code: {code}");
                        Console.WriteLine($"   Error Message: {message}");

                        if (errorElement.TryGetProperty("innerError", out var innerError))
                        {
                            Console.WriteLine($"   Inner Error: {innerError}");
                        }

                        throw new Exception($"Graph API Error [{code}]: {message}");
                    }
                }
                catch (JsonException)
                {
                    // If JSON parsing fails, just throw with the raw body
                }

                throw new Exception($"Graph API request failed with status {response.StatusCode}: {errorBody}");
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new Exception("Graph API authentication failed. Verify token is valid and not expired.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new Exception("Access denied. Verify the service principal has 'EntitlementManagement.ReadWrite.All' permission granted and admin consent is given.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            var errorContent = ex.Message;
            throw new Exception($"Graph API request validation failed: {errorContent}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            throw new Exception("Resource already exists with conflicting properties. Check displayName or other unique identifiers.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == (HttpStatusCode)429)
        {
            throw new Exception("Graph API rate limit exceeded. Implement retry with exponential backoff.");
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Graph API call failed with status {ex.StatusCode}: {ex.Message}");
        }
    }
}

/// <summary>
/// Base handler for Group and User resources.
/// Uses GroupUserToken (requires Group.ReadWrite.All and User.Read.All permissions).
/// </summary>
public abstract class GroupUserResourceHandlerBase<TProps, TIdentifiers>
    : TypedResourceHandler<TProps, TIdentifiers, Configuration>
    where TProps : class
    where TIdentifiers : class
{
    protected HttpClient CreateGraphClient(Configuration config, bool useBeta = false)
    {
        var baseUrl = useBeta
            ? "https://graph.microsoft.com/beta/"
            : "https://graph.microsoft.com/v1.0/";

        var client = new HttpClient
        {
            BaseAddress = new Uri(config.GraphApiBaseUrl ?? baseUrl),
            Timeout = TimeSpan.FromSeconds(120)  // Increased to 120s for PIM policy patching workflows
        };

        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        // Use GroupUserToken for group/user operations
        var token = config.GroupUserToken ?? Environment.GetEnvironmentVariable("GROUP_USER_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new Exception("Group/User token is required. Set via 'groupUserToken' in extension configuration or GROUP_USER_TOKEN environment variable.");
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    protected static async Task<T?> HandleGraphApiCall<T>(
        Func<Task<HttpResponseMessage>> apiCall,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await apiCall();

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return default; // Resource doesn't exist (OK for GET operations)
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

                Console.WriteLine($"Graph API Error Response:");
                Console.WriteLine($"   Status Code: {(int)response.StatusCode} ({response.StatusCode})");
                Console.WriteLine($"   Response Body: {errorBody}");

                try
                {
                    var errorJson = JsonDocument.Parse(errorBody);
                    if (errorJson.RootElement.TryGetProperty("error", out var errorElement))
                    {
                        var code = errorElement.TryGetProperty("code", out var codeElement) ? codeElement.GetString() : "Unknown";
                        var message = errorElement.TryGetProperty("message", out var msgElement) ? msgElement.GetString() : errorBody;

                        Console.WriteLine($"   Error Code: {code}");
                        Console.WriteLine($"   Error Message: {message}");

                        if (errorElement.TryGetProperty("innerError", out var innerError))
                        {
                            Console.WriteLine($"   Inner Error: {innerError}");
                        }

                        throw new Exception($"Graph API Error [{code}]: {message}");
                    }
                }
                catch (JsonException)
                {
                    // If JSON parsing fails, just throw with the raw body
                }

                throw new Exception($"Graph API request failed with status {response.StatusCode}: {errorBody}");
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new Exception("Graph API authentication failed. Verify token is valid and not expired.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new Exception("Access denied. Verify the service principal has 'Group.ReadWrite.All' and 'User.Read.All' permissions granted and admin consent is given.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            var errorContent = ex.Message;
            throw new Exception($"Graph API request validation failed: {errorContent}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            throw new Exception("Resource already exists with conflicting properties. Check unique identifiers.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == (HttpStatusCode)429)
        {
            throw new Exception("Graph API rate limit exceeded. Implement retry with exponential backoff.");
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Graph API call failed with status {ex.StatusCode}: {ex.Message}");
        }
    }
}
