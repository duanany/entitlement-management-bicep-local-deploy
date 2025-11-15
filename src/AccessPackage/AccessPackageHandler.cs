using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace EntitlementManagement.AccessPackage;

public class AccessPackageHandler : EntitlementManagementResourceHandlerBase<AccessPackage, AccessPackageIdentifiers>
{
    protected override async Task<ResourceResponse> Preview(ResourceRequest request, CancellationToken cancellationToken)
    {
        var existing = await GetAccessPackageAsync(request.Config, request.Properties, cancellationToken);
        if (existing is not null)
        {
            request.Properties.Id = existing.id;
            request.Properties.Description = existing.description;
            request.Properties.IsHidden = existing.isHidden;
            request.Properties.CreatedDateTime = existing.createdDateTime;
            request.Properties.ModifiedDateTime = existing.modifiedDateTime;
        }
        return GetResponse(request);
    }

    protected override async Task<ResourceResponse> CreateOrUpdate(ResourceRequest request, CancellationToken cancellationToken)
    {
        var props = request.Properties;
        var existing = await GetAccessPackageAsync(request.Config, props, cancellationToken);

        if (existing is null)
        {
            await CreateAccessPackageAsync(request.Config, props, cancellationToken);
            existing = await GetAccessPackageAsync(request.Config, props, cancellationToken)
                ?? throw new InvalidOperationException("Access package creation did not return access package.");
        }
        else
        {
            if (HasChanges(props, existing))
            {
                await UpdateAccessPackageAsync(request.Config, existing.id, props, cancellationToken);
                existing = await GetAccessPackageAsync(request.Config, props, cancellationToken) ?? existing;
            }
        }

        props.Id = existing.id;
        props.Description = existing.description;
        props.IsHidden = existing.isHidden;
        props.CreatedDateTime = existing.createdDateTime;
        props.ModifiedDateTime = existing.modifiedDateTime;

        return GetResponse(request);
    }

    protected override AccessPackageIdentifiers GetIdentifiers(AccessPackage properties) => new()
    {
        DisplayName = properties.DisplayName,
        CatalogId = properties.CatalogId
    };

    private async Task<dynamic?> GetAccessPackageAsync(Configuration config, AccessPackage props, CancellationToken ct)
    {
        try
        {
            using var client = CreateGraphClient(config);
            var filter = Uri.EscapeDataString($"displayName eq '{props.DisplayName}' and catalog/id eq '{props.CatalogId}'");
            var resp = await client.GetAsync($"identityGovernance/entitlementManagement/accessPackages?$filter={filter}&$expand=catalog", ct);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            if (json.TryGetProperty("value", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    return new
                    {
                        id = item.GetProperty("id").GetString()!,
                        displayName = item.GetProperty("displayName").GetString()!,
                        description = item.TryGetProperty("description", out var d) ? d.GetString() : null,
                        isHidden = item.TryGetProperty("isHidden", out var h) && h.GetBoolean(),
                        createdDateTime = item.TryGetProperty("createdDateTime", out var c) ? c.GetString() : null,
                        modifiedDateTime = item.TryGetProperty("modifiedDateTime", out var m) ? m.GetString() : null
                    };
                }
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task CreateAccessPackageAsync(Configuration config, AccessPackage props, CancellationToken ct)
    {
        using var client = CreateGraphClient(config);
        var body = new
        {
            displayName = props.DisplayName,
            description = props.Description,
            isHidden = props.IsHidden,
            catalog = new
            {
                id = props.CatalogId
            }
        };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var resp = await client.PostAsync("identityGovernance/entitlementManagement/accessPackages", content, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Failed to create access package: {(int)resp.StatusCode} {resp.ReasonPhrase} {err}");
        }
    }

    private async Task UpdateAccessPackageAsync(Configuration config, string accessPackageId, AccessPackage props, CancellationToken ct)
    {
        using var client = CreateGraphClient(config);
        var body = new
        {
            displayName = props.DisplayName,
            description = props.Description,
            isHidden = props.IsHidden
        };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var req = new HttpRequestMessage(new HttpMethod("PATCH"), $"identityGovernance/entitlementManagement/accessPackages/{accessPackageId}")
        {
            Content = content
        };
        var resp = await client.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Failed to update access package: {(int)resp.StatusCode} {resp.ReasonPhrase} {err}");
        }
    }

    private static bool HasChanges(AccessPackage props, dynamic existing)
    {
        return props.DisplayName != existing.displayName ||
               props.Description != existing.description ||
               props.IsHidden != existing.isHidden;
    }
}
