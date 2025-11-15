---
description: Coding guidelines and best practices for Bicep local-deploy extension development
applyTo: '**/*.{cs,bicep,bicepparam}'
---

# Bicep Local-Deploy Extension Development Guidelines

This file contains coding patterns, best practices, and quick reference commands for developing Bicep local-deploy extensions in C# and .NET.

## Common Task Patterns

### Adding a New Resource Type
1. **Create Models**:
   - `src/[Resource]/[Resource].cs` - Resource properties (what users configure)
   - `src/[Resource]/[Resource]Identifiers.cs` - Resource identifiers (what uniquely identifies it)
2. **Create Handler**: `src/[Resource]/[Resource]Handler.cs` extending `TypedResourceHandler<TProps, TIdentifiers, Configuration>`
   - Implement `Save(TProps properties, CancellationToken cancellationToken)` method
   - Implement `Delete(TIdentifiers identifiers, CancellationToken cancellationToken)` method
3. **Register in Program.cs**: Add `.WithResourceHandler<YourResourceHandler>()` to the service registration
4. **Document**: Create `docs/[resource].md` with API details, examples, known issues
5. **Publish Extension**: Run build/publish script or commands
6. **Test**: Add example to `Sample/main.bicep` and deploy with `bicep local-deploy ./Sample/main.bicepparam`

### Implementing Idempotent Save
```csharp
public async Task<ResourceResult> Save(MyResourceProperties properties, CancellationToken cancellationToken)
{
    var config = GetConfiguration();
    var client = CreateClient(config);

    // 1. Try to GET existing resource by identifier (name, ID, etc.)
    var existing = await GetExistingResource(client, properties.Name, cancellationToken);

    // 2. If exists, compare and update only if different
    if (existing != null)
    {
        if (IsEqual(existing, properties))
        {
            // No-op: return existing state with identifiers
            return new ResourceResult
            {
                Identifiers = new MyResourceIdentifiers { Id = existing.Id, Name = existing.Name },
                Properties = properties
            };
        }

        // Update existing resource
        var updated = await UpdateResource(client, existing.Id, properties, cancellationToken);
        return new ResourceResult
        {
            Identifiers = new MyResourceIdentifiers { Id = updated.Id, Name = updated.Name },
            Properties = updated
        };
    }

    // 3. If not exists, create new
    var created = await CreateResource(client, properties, cancellationToken);
    return new ResourceResult
    {
        Identifiers = new MyResourceIdentifiers { Id = created.Id, Name = created.Name },
        Properties = created
    };
}
```

### Implementing Delete Operation
```csharp
public async Task Delete(MyResourceIdentifiers identifiers, CancellationToken cancellationToken)
{
    var config = GetConfiguration();
    var client = CreateClient(config);

    try
    {
        // Call API to delete resource
        var response = await client.DeleteAsync(
            $"/api/resources/{identifiers.Id}",
            cancellationToken
        );

        // 404 is acceptable - resource already deleted
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }
    catch (HttpRequestException ex)
    {
        throw new Exception($"Failed to delete resource: {ex.Message}", ex);
    }
}
```

### Creating HTTP Client with Authentication
```csharp
protected static HttpClient CreateClient(Configuration configuration)
{
    var client = new HttpClient
    {
        BaseAddress = new Uri(configuration.ApiBaseUrl ?? "https://api.example.com"),
        Timeout = TimeSpan.FromSeconds(30)
    };

    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json")
    );

    // Example: Bearer token authentication
    var token = configuration.ApiToken ?? Environment.GetEnvironmentVariable("API_TOKEN");
    if (!string.IsNullOrWhiteSpace(token))
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    // Example: API Key authentication
    var apiKey = configuration.ApiKey ?? Environment.GetEnvironmentVariable("API_KEY");
    if (!string.IsNullOrWhiteSpace(apiKey))
    {
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
    }

    return client;
}
```

## Authentication Patterns

### Configuration Class with Credentials
```csharp
namespace MyExtension;

public class Configuration
{
    [TypeProperty("API token for authentication. If omitted, environment variable API_TOKEN is used.")]
    public string? ApiToken { get; set; }

    [TypeProperty("API base URL. Defaults to https://api.example.com")]
    public string? ApiBaseUrl { get; set; }

    [TypeProperty("API timeout in seconds. Defaults to 30.")]
    public int? TimeoutSeconds { get; set; }
}
```

### Base Handler with HTTP Client Setup
```csharp
public abstract class MyServiceResourceHandlerBase<TProps, TIdentifiers>
    : TypedResourceHandler<TProps, TIdentifiers, Configuration>
    where TProps : class
    where TIdentifiers : class
{
    protected HttpClient CreateClient()
    {
        var config = GetConfiguration();

        var client = new HttpClient
        {
            BaseAddress = new Uri(config.ApiBaseUrl ?? "https://api.example.com"),
            Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds ?? 30)
        };

        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json")
        );

        // Try configuration first, then environment variable
        var token = config.ApiToken ?? Environment.GetEnvironmentVariable("API_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new Exception("API token is required. Set via extension configuration or API_TOKEN environment variable.");
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    protected static async Task<T> HandleApiCall<T>(Func<Task<HttpResponseMessage>> apiCall, CancellationToken cancellationToken)
    {
        try
        {
            var response = await apiCall();
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
                ?? throw new Exception("API returned null response");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new Exception("Authentication failed. Verify API token is valid and not expired.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new Exception("Access denied. Verify API token has required permissions.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new Exception("Resource not found. Verify resource identifiers are correct.");
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"API call failed with status {ex.StatusCode}: {ex.Message}");
        }
    }
}
```

### Extension Declaration in Bicep
```bicep
targetScope = 'local'

extension myextension with {
  apiToken: apiToken
  apiBaseUrl: 'https://api.myservice.com'
}

@secure()
@description('API token for MyService authentication')
param apiToken string

resource myResource 'MyResource' = {
  name: 'my-resource-name'
  description: 'My resource description'
  // ... other properties
}
```

### Configuration in bicepconfig.json
```json
{
  "experimentalFeaturesEnabled": {
    "localDeploy": true
  },
  "extensions": {
    // For local development: point to locally published extension folder
    "myextension": "./my-extension"
  },
  "implicitExtensions": []
}
```

**Note**: The extension path points to a folder containing platform-specific binaries (linux-x64, osx-arm64, win-x64), not directly to a DLL. Configuration settings like API tokens are passed via the extension declaration in Bicep or as parameters, not in `bicepconfig.json`.

## Building & Testing Workflow

**Option 1: Using PowerShell Script (if available)**
```bash
# Build and publish extension (builds for all platforms + publishes locally)
./Infra/Scripts/Publish-Extension.ps1 -Target ./my-extension

# Verify extension folder structure
ls ./my-extension/
# Should show: linux-x64/, osx-arm64/, win-x64/, types/

# Build Bicep template (validates syntax and types)
bicep build ./Sample/main.bicep

# Deploy with local-deploy
bicep local-deploy ./Sample/main.bicepparam
```

**Option 2: Using Individual Commands**
```bash
# Step 1: Publish for all platforms
dotnet publish --configuration Release -r osx-arm64
dotnet publish --configuration Release -r linux-x64
dotnet publish --configuration Release -r win-x64

# Step 2: Publish extension to local registry
bicep publish-extension \
  --bin-osx-arm64 "src/bin/Release/net9.0/osx-arm64/publish/my-extension" \
  --bin-linux-x64 "src/bin/Release/net9.0/linux-x64/publish/my-extension" \
  --bin-win-x64 "src/bin/Release/net9.0/win-x64/publish/my-extension.exe" \
  --target "Sample/my-extension" \
  --force

# Step 3: Clear Bicep cache if needed (helps with stale type issues)
rm -rf ~/.bicep/cache

# Step 4: Build Bicep template
cd Sample && bicep build main.bicep

# Step 5: Deploy with local-deploy
bicep local-deploy main.bicepparam
```

**Quick Build for Current Platform Only**
```bash
# Build for current platform only (faster during development)
dotnet build -c Release src/MyExtension.csproj

# Note: bicep publish-extension requires binaries for all platforms
# Use the full workflow above for complete publishing
```

### Debugging Extension Execution
1. **Use Console.WriteLine for code readability**: Add console output in handlers for flow clarity
   ```csharp
   Console.WriteLine($"Creating resource: {properties.Name}");
   ```
   **Note**: `bicep local-deploy` does NOT display Console.WriteLine output during deployment. These logs are for developers reading the code, not for runtime debugging.
2. **Inspect Bicep Output**: Run with verbose flag to see deployment status (Console.WriteLine still won't appear)
   ```bash
   bicep local-deploy ./Sample/main.bicepparam --verbose
   ```
3. **Test Handlers Directly**: Create unit tests that call `Save`/`Delete` methods directly (Console.WriteLine visible here)
4. **Use Debugger**: Attach VS Code debugger to running extension process
5. **Check HTTP Traffic**: Use tools like Fiddler or Wireshark to inspect API calls

## Best Practices

### Code Quality
- ✅ **Use async/await**: All HTTP calls and I/O operations should be asynchronous
- ✅ **Handle errors gracefully**: Return meaningful diagnostics to Bicep CLI
- ✅ **Validate inputs**: Check required properties before making API calls
- ✅ **Log operations**: Use Console.WriteLine for code readability and flow clarity. Note: `bicep local-deploy` does NOT display console output by default - logs are for developers reading the code, not runtime debugging.
- ✅ **Follow .NET conventions**: Use proper naming, formatting, and patterns
- ✅ **Implement cancellation**: Respect `CancellationToken` throughout the call chain
- ✅ **No emoji icons**: Keep code professional - avoid emoji icons in Console.WriteLine, comments, or any code. Use clear descriptive text instead.
- ✅ **Use "Entra ID" terminology**: Always use "Entra ID" (not "Azure AD") in code, comments, and documentation when referring to Microsoft's identity platform.
- ✅ **Defensive programming for Graph API**: Entra ID/Graph API has replication delays. Resources created (groups, catalogs, access packages) may not be immediately available. ALWAYS implement try-catch with retry logic (exponential backoff) when querying or using newly created resources. Expect sync/timing issues and code defensively.

### Type Definitions
- ✅ **Use descriptive names**: Match target service terminology
- ✅ **Add helpful descriptions**: Help users understand each property's purpose
- ✅ **Mark required properties**: Use `required: true` in `[TypeProperty]`
- ✅ **Avoid special chars**: Escape single quotes, brackets in descriptions
- ✅ **Test generation**: Validate types after every model change

### HTTP Client Management
- ✅ **Reuse HttpClient**: Create once and reuse, don't create per request
- ✅ **Set timeouts**: Configure appropriate timeout values
- ✅ **Handle transient failures**: Implement retry logic for network issues
- ✅ **Use cancellation tokens**: Support operation cancellation properly
- ✅ **Dispose resources**: Ensure proper cleanup of HTTP resources

### Defensive Programming & Retry Logic (CRITICAL for Graph API)

Entra ID and Graph API have **replication delays** - resources created are not immediately available across all API endpoints. This causes timing/sync issues.

**Common scenarios requiring retry logic**:
- Creating a group, then immediately adding it to a catalog → Group may not be visible yet
- Creating an access package, then adding resource role scopes → Access package may not be queryable
- Creating a catalog resource, then querying it by ID → Resource may return 404 temporarily
- Any cross-API operation (Groups API → Entitlement Management API)

**Retry pattern with exponential backoff**:
```csharp
private async Task<T?> GetWithRetryAsync<T>(Func<Task<T?>> operation, CancellationToken cancellationToken)
{
    const int maxRetries = 10;
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

            Console.WriteLine($"Resource not found (attempt {attempt}/{maxRetries}) - waiting {delayMs}ms for replication...");
            await Task.Delay(delayMs, cancellationToken);
            delayMs = Math.Min(delayMs * 2, 16000); // Exponential backoff, max 16s
        }
        catch (HttpRequestException ex) when (attempt < maxRetries)
        {
            Console.WriteLine($"HTTP error (attempt {attempt}/{maxRetries}): {ex.Message} - retrying in {delayMs}ms...");
            await Task.Delay(delayMs, cancellationToken);
            delayMs = Math.Min(delayMs * 2, 16000);
            continue;
        }
        catch (Exception ex) when (attempt < maxRetries && IsReplicationError(ex))
        {
            Console.WriteLine($"Potential replication error (attempt {attempt}/{maxRetries}): {ex.Message} - retrying...");
            await Task.Delay(delayMs, cancellationToken);
            delayMs = Math.Min(delayMs * 2, 16000);
            continue;
        }
    }

    throw new Exception($"Failed after {maxRetries} attempts. Resource may not exist or replication is taking longer than expected.");
}

private static bool IsReplicationError(Exception ex)
{
    return ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
           ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
           ex.Message.Contains("not present", StringComparison.OrdinalIgnoreCase) ||
           ex.Message.Contains("replication", StringComparison.OrdinalIgnoreCase) ||
           ex.Message.Contains("ResourceNotFoundInOriginSystem", StringComparison.OrdinalIgnoreCase) ||
           ex.Message.Contains("unavailable", StringComparison.OrdinalIgnoreCase);
}
```

**Additional wait time for cross-API operations**:
```csharp
// After creating a group, wait before using it in Entitlement Management API
await Task.Delay(20000, cancellationToken); // 20 seconds for Groups → Entitlement Management replication
```

### Bicep Patterns
- ✅ **Use parameters**: Externalize environment-specific values
- ✅ **Secure secrets**: Use `@secure()` decorator for sensitive parameters
- ✅ **Create modules**: Break complex deployments into reusable pieces
- ✅ **Document examples**: Show working usage in `Sample/`
- ✅ **Version control**: Keep `bicepconfig.json` generic (no secrets)

### Documentation
- ✅ **Resource docs**: Create `docs/[resource].md` for each new type
- ✅ **API references**: Include links to target service REST API docs
- ✅ **Authentication guide**: Document credential setup and permissions needed
- ✅ **Known issues**: Document common errors and solutions
- ✅ **Examples**: Provide working code snippets and sample deployments

### Security
- ✅ **Never log secrets**: Avoid logging tokens, passwords, or sensitive data
- ✅ **Use environment variables**: Allow credentials from environment as fallback
- ✅ **Validate input**: Sanitize user input to prevent injection attacks
- ✅ **Use HTTPS**: Always use secure connections for API calls
- ✅ **Handle errors safely**: Don't expose sensitive information in error messages

### Error Handling
- ✅ **Provide context**: Include operation details in error messages
- ✅ **Map HTTP status codes**: Convert API errors to meaningful Bicep diagnostics
- ✅ **Handle 404 gracefully**: Treat missing resources appropriately in Delete operations
- ✅ **Retry transient failures**: Implement exponential backoff for network issues
- ✅ **Respect rate limits**: Check Retry-After headers and implement delays

### Idempotency
- ✅ **Check existing state**: Query for resource before creating
- ✅ **Compare properties**: Only update when changes detected
- ✅ **Return consistent results**: Same input should produce same output
- ✅ **Handle no-ops**: Return existing resource when nothing changed
- ✅ **Use unique identifiers**: Match resources by name, ID, or other unique key

## Testing Strategy

### Unit Testing
- Mock HTTP API responses using `HttpMessageHandler`
- Test `Save` returns existing state correctly when resource exists
- Test `Save` creates new resource when it doesn't exist
- Test `Save` updates resource when properties change
- Test `Delete` removes resource successfully
- Test authentication failure scenarios
- Test API error response handling

### Integration Testing
- Deploy Sample to isolated test environment
- Verify idempotency by running deployment twice
- Test parameter variations
- Validate cleanup (delete operations)
- Test authentication with real credentials
- Verify error handling with invalid inputs

### Manual Testing Checklist
- [ ] Extension builds without errors for all platforms
- [ ] Type generation completes successfully (`types/index.json` exists)
- [ ] Sample Bicep template compiles without errors
- [ ] Deployment creates resources as expected
- [ ] Re-deployment is idempotent (no changes on second run)
- [ ] Parameter changes trigger updates correctly
- [ ] Resource deletion works correctly
- [ ] Authentication errors are handled gracefully
- [ ] API errors provide clear diagnostic messages

## Quick Reference

### Essential Commands
```bash
# Build and publish extension for all platforms (using script if available)
./Infra/Scripts/Publish-Extension.ps1 -Target ./my-extension

# OR: Manual build and publish
dotnet publish --configuration Release -r osx-arm64
dotnet publish --configuration Release -r linux-x64
dotnet publish --configuration Release -r win-x64
bicep publish-extension \
  --bin-osx-arm64 "src/bin/Release/net9.0/osx-arm64/publish/my-extension" \
  --bin-linux-x64 "src/bin/Release/net9.0/linux-x64/publish/my-extension" \
  --bin-win-x64 "src/bin/Release/net9.0/win-x64/publish/my-extension.exe" \
  --target "Sample/my-extension" \
  --force

# Quick build (current platform only, for development)
dotnet build -c Release src/MyExtension.csproj

# Clear Bicep cache (helps resolve stale type issues)
rm -rf ~/.bicep/cache

# Build Bicep template (validates syntax and extension types)
bicep build ./Sample/main.bicep

# Deploy with local-deploy
bicep local-deploy ./Sample/main.bicepparam

# Check versions
bicep --version
dotnet --version
```

### Key Files
- `Program.cs`: ASP.NET Core host setup with `AddBicepExtensionHost()`, resource handler registration with `.WithResourceHandler<>()`
- `Configuration.cs`: Settings model with `[TypeProperty]` attributes (becomes extension configuration parameters)
- `[Resource]Handler.cs`: Implements `TypedResourceHandler<TProps, TIdentifiers, Configuration>` with `Save` and `Delete` methods
- `[Resource].cs`: Model classes for resource properties
- `[Resource]Identifiers.cs`: Model classes for resource identifiers
- `bicepconfig.json`: Extension configuration pointing to published extension folder
- `main.bicep`: Resource definitions with `targetScope = 'local'` and `extension myextension with { ... }`
- `main.bicepparam`: Environment-specific parameters including secure credentials

## Known Issues & Solutions

### Issue: Type Generation Failures
**Symptom**: `bicep publish-extension` fails or extension types are not recognized in Bicep
**Causes**:
- Single quotes in `[TypeProperty]` descriptions (e.g., `['value1','value2']`)
- Special characters not properly escaped
- Circular type references
- Missing `[TypeProperty]` attributes on Configuration class properties

**Solution**:
- Use double quotes or escape sequences in descriptions
- Ensure Configuration class properties have `[TypeProperty]` attributes (these become extension configuration parameters)
- Test publishing after each model change
- Check generated extension folder contains `types/index.json`

### Issue: Extension Not Found
**Symptom**: `bicep local-deploy` fails with "cannot find extension" or "extension not loaded"
**Solution**:
1. Verify extension was published: Check extension folder has platform subdirectories (osx-arm64, linux-x64, win-x64)
2. Check `bicepconfig.json` extension path matches published location: `"myextension": "./my-extension-folder"`
3. Ensure Bicep CLI version supports `localDeploy` (v0.37.4+)
4. Confirm `experimentalFeaturesEnabled.localDeploy: true` in bicepconfig.json

### Issue: Authentication Failures
**Symptom**: HTTP 401/403 errors from target API
**Solution**:
1. Verify credentials have required permissions/scopes
2. Check authentication format matches API expectations (header name, prefix, encoding)
3. Validate API endpoint URLs are correct
4. Test credentials directly with curl/Postman before implementing in extension
5. Check for token expiration or rate limiting

### Issue: Idempotency Violations
**Symptom**: Re-running deployment creates duplicates or fails
**Solution**:
1. Implement proper check for existing state in `Save` method
2. Use unique identifiers (names, IDs) to match resources
3. Compare current vs. desired state before creating/updating
4. Return existing resource data on no-op updates with unchanged identifiers

### Issue: HTTP Client Timeouts
**Symptom**: Operations timeout or hang indefinitely
**Solution**:
1. Configure appropriate `HttpClient.Timeout` for long-running operations
2. Implement cancellation token support properly
3. Use async/await consistently throughout the call chain
4. Consider implementing retry policies for transient failures

## Common Pitfalls

- ❌ **Forgetting to re-publish** after model changes (run publish command to rebuild)
- ❌ **Using single quotes in descriptions** (breaks type generation in `[TypeProperty]` attributes)
- ❌ **Not checking existing state** in `Save` method (causes duplicates instead of idempotent updates)
- ❌ **Hardcoding secrets** in Bicep files (use `@secure() param` instead)
- ❌ **Missing `[TypeProperty]` on Configuration** (configuration properties won't be available in extension declaration)
- ❌ **Wrong bicepconfig.json path** (must point to published extension folder, not source DLL)
- ❌ **Forgetting `targetScope = 'local'`** in main.bicep (required for local-deploy extensions)
- ❌ **Not handling cancellation** properly (operations may not respond to cancellation requests)
- ❌ **Creating new HttpClient per request** (causes socket exhaustion)
- ❌ **Not implementing retry logic** (transient network failures cause deployment failures)
- ❌ **Ignoring Entra ID replication delays** (Graph API resources may not be immediately available after creation; implement retry logic with exponential backoff)
- ❌ **Using emoji icons in code** (unprofessional; use clear descriptive text in Console.WriteLine and comments instead)
- ❌ **Using "Azure AD" terminology** (outdated; always use "Entra ID" instead)

---

**Remember**: Always prioritize **idempotency**, **clear error messages**, **security**, and **comprehensive documentation**. Test thoroughly with real credentials before committing changes.
