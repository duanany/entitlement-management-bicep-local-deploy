---
applyTo: "**/*.cs"
description: "C# guidelines for the Entitlement Management Bicep local-deploy extension"
---

# C# Guidelines for Entitlement Management Extension (Kick-Ass Edition)

## Microsoft C# Standards + Our Rules
- **Follow Microsoft's Common C# Code Conventions** religiously
- Modern C# features over clever tricks - clarity wins every time
- Keep methods small, testable, and single-purpose
- Nullability: enabled; avoid `!` unless justified with a comment
- **No emoji icons in code** - professional text only (Console.WriteLine, comments, etc.)
- **Use "Entra ID" terminology** - never "Azure AD" in code/comments/docs

## Naming & Style

### Variables & Parameters
- Full names, no abbreviations:
  ```csharp
  // ✅ GOOD
  protected override async Task<ResourceResponse> Preview(
      ResourceRequest request, 
      CancellationToken cancellationToken)
  
  // ❌ BAD
  protected override async Task<ResourceResponse> Preview(
      ResourceRequest req, 
      CancellationToken ct)
  ```

### Resource Types
- Keep names consistent: `AccessPackageCatalog`, `AccessPackage`, etc.
- Use full descriptive names matching Azure terminology

## Entitlement Management Patterns

### 1. Graph API Authentication
Every handler needs OAuth token:

```csharp
protected HttpClient CreateGraphClient(Configuration config, bool useBeta = false)
{
    var client = new HttpClient
    {
        BaseAddress = new Uri(useBeta 
            ? "https://graph.microsoft.com/beta" 
            : "https://graph.microsoft.com/v1.0"),
        Timeout = TimeSpan.FromSeconds(30)
    };

    var token = config.EntitlementToken ?? config.GroupUserToken 
        ?? throw new Exception("No authentication token provided");

    client.DefaultRequestHeaders.Authorization = 
        new AuthenticationHeaderValue("Bearer", token);
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));

    return client;
}
```

### 2. Idempotent Operations
ALWAYS check if resource exists before creating:

```csharp
protected override async Task<ResourceResponse> CreateOrUpdate(
    ResourceRequest request, 
    CancellationToken cancellationToken)
{
    var properties = request.Properties;
    var config = request.Config;
    using var client = CreateGraphClient(config);

    // STEP 1: Check if exists (by displayName or other unique property)
    var existing = await FindExistingAsync(client, properties.DisplayName, cancellationToken);

    if (existing != null)
    {
        // STEP 2a: Resource exists - UPDATE if properties changed
        Console.WriteLine($"Resource '{properties.DisplayName}' exists - checking for updates...");
        
        if (PropertiesChanged(existing, properties))
        {
            Console.WriteLine($"Properties changed - updating resource {existing.Id}...");
            return await UpdateResourceAsync(client, existing.Id, properties, cancellationToken);
        }
        
        Console.WriteLine($"No changes detected - reusing existing resource {existing.Id}");
        properties.Id = existing.Id;
        return GetResponse(request);
    }
    else
    {
        // STEP 2b: Resource doesn't exist - CREATE
        Console.WriteLine($"Resource '{properties.DisplayName}' not found - creating...");
        return await CreateResourceAsync(client, properties, cancellationToken);
    }
}
```

### 3. Retry Logic for Entra ID Replication
CRITICAL for cross-API operations (Groups → Entitlement Management):

```csharp
private async Task<T?> GetWithRetryAsync<T>(
    Func<Task<T?>> operation,
    CancellationToken cancellationToken,
    int maxRetries = 10)
{
    int delayMs = 2000; // Start with 2 seconds

    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            var result = await operation();
            if (result != null || attempt >= maxRetries)
            {
                return result;
            }

            Console.WriteLine($"Resource not found (attempt {attempt}/{maxRetries}) - " +
                            $"waiting {delayMs}ms for Entra ID replication...");
            await Task.Delay(delayMs, cancellationToken);
            delayMs = Math.Min(delayMs * 2, 16000); // Exponential backoff, max 16s
        }
        catch (HttpRequestException ex) when (attempt < maxRetries && 
                                             ex.StatusCode == HttpStatusCode.NotFound)
        {
            Console.WriteLine($"404 Not Found (attempt {attempt}/{maxRetries}) - " +
                            $"retrying in {delayMs}ms...");
            await Task.Delay(delayMs, cancellationToken);
            delayMs = Math.Min(delayMs * 2, 16000);
            continue;
        }
    }

    throw new Exception($"Failed to get resource after {maxRetries} attempts. " +
                       "Entra ID replication may be delayed.");
}
```

### 4. Graph API Error Handling
Map Graph errors to user-friendly messages:

```csharp
protected static async Task<T?> HandleGraphApiCall<T>(
    Func<Task<HttpResponseMessage>> apiCall,
    CancellationToken cancellationToken)
{
    try
    {
        var response = await apiCall();

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default; // Resource doesn't exist - OK for GET
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(
            cancellationToken: cancellationToken);
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
    {
        throw new Exception("Graph API authentication failed. " +
                          "Verify token is valid and has required permissions.");
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
    {
        throw new Exception("Access denied. Verify token has required Graph API permissions " +
                          "(e.g., EntitlementManagement.ReadWrite.All).");
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
    {
        var errorContent = await ex.Message;
        throw new Exception($"Graph API request validation failed: {errorContent}");
    }
    catch (HttpRequestException ex) when (ex.StatusCode == (HttpStatusCode)429)
    {
        throw new Exception("Graph API rate limit exceeded. Implement exponential backoff.");
    }
}
```

### 5. Console Logging for Debugging
Use Console.WriteLine strategically (NOT visible in `bicep local-deploy` output, but helps developers):

```csharp
// ✅ GOOD - explains what's happening
Console.WriteLine($"[POLICY] Querying for existing policy: {displayName}");
Console.WriteLine($"[POLICY] Found {count} policies - checking access package match...");
Console.WriteLine($"[POLICY] Policy {policyId} updated successfully");

// ❌ BAD - too verbose or uses emoji
Console.WriteLine($"🎉 Success! Policy created!");  // No emoji!
Console.WriteLine($"Doing a thing...");              // Too vague
```

## Resource Handler Template

```csharp
using System.Net;
using System.Text.Json;

namespace EntitlementManagement.MyResource;

public class MyResourceHandler 
    : EntitlementManagementResourceHandlerBase<MyResource, MyResourceIdentifiers>
{
    protected override async Task<ResourceResponse> Preview(
        ResourceRequest request, 
        CancellationToken cancellationToken)
    {
        var properties = request.Properties;
        var config = request.Config;

        Console.WriteLine($"[MYRESOURCE] Previewing '{properties.DisplayName}'...");

        using var client = CreateGraphClient(config);
        var existing = await FindExistingAsync(client, properties.DisplayName, cancellationToken);

        if (existing != null)
        {
            properties.Id = existing.Id;
            Console.WriteLine($"[MYRESOURCE] Resource exists: {existing.Id}");
        }
        else
        {
            Console.WriteLine($"[MYRESOURCE] Resource does not exist - will be created");
        }

        return GetResponse(request);
    }

    protected override async Task<ResourceResponse> CreateOrUpdate(
        ResourceRequest request, 
        CancellationToken cancellationToken)
    {
        var properties = request.Properties;
        var config = request.Config;

        Console.WriteLine($"[MYRESOURCE] Creating/updating '{properties.DisplayName}'...");

        using var client = CreateGraphClient(config);
        var existing = await FindExistingAsync(client, properties.DisplayName, cancellationToken);

        if (existing != null)
        {
            // UPDATE path
            Console.WriteLine($"[MYRESOURCE] Updating {existing.Id}...");
            var updated = await UpdateResourceAsync(client, existing.Id, properties, cancellationToken);
            properties.Id = updated.Id;
            Console.WriteLine($"[MYRESOURCE] Updated successfully");
        }
        else
        {
            // CREATE path
            Console.WriteLine($"[MYRESOURCE] Creating new resource...");
            var created = await CreateResourceAsync(client, properties, cancellationToken);
            properties.Id = created.Id;
            Console.WriteLine($"[MYRESOURCE] Created successfully: {created.Id}");
        }

        return GetResponse(request);
    }

    // Helper methods...
    private async Task<ExistingResource?> FindExistingAsync(...)
    private async Task<CreatedResource> CreateResourceAsync(...)
    private async Task<UpdatedResource> UpdateResourceAsync(...)
}
```

## Documentation Attributes
Use Bicep documentation attributes on resource types:

```csharp
[BicepDocHeading("Access Package Catalogs")]
[BicepDocExample(@"
resource catalog 'accessPackageCatalog' = {
  displayName: 'Engineering Resources'
  description: 'Catalog for engineering access packages'
  isExternallyVisible: false
}
")]
[BicepDocCustom("Graph API", "Uses /identityGovernance/entitlementManagement/catalogs")]
public class AccessPackageCatalog : TypedResourceBase
{
    [TypeProperty("The display name (used for idempotency checks)")]
    public string? DisplayName { get; set; }
    
    // ... other properties
}
```

## Key Principles for This Extension

1. **Idempotency is King** - Always query before creating
2. **Retry Logic is Critical** - Entra ID replication takes time
3. **Error Messages Must Help Users** - Map Graph errors to actionable advice
4. **Console Logging for Developers** - Explain what's happening (no emoji!)
5. **Use `@secure()` in Bicep** - Never log tokens
6. **Two-token architecture** - Support least privilege (entitlementToken + groupUserToken)

## Testing Your Handler

```csharp
// Manual test in Program.cs or unit test
var config = new Configuration
{
    EntitlementToken = Environment.GetEnvironmentVariable("ENTITLEMENT_TOKEN"),
    GroupUserToken = Environment.GetEnvironmentVariable("GROUP_USER_TOKEN")
};

var handler = new MyResourceHandler();
var request = new ResourceRequest
{
    Properties = new MyResource { DisplayName = "Test Resource" },
    Config = config
};

var response = await handler.CreateOrUpdate(request, CancellationToken.None);
Console.WriteLine($"Created/Updated: {response.Properties.Id}");
```

---

**Remember**: This extension is built with AI assistance (GitHub Copilot Agent Mode). Code quality matters! 🚀
