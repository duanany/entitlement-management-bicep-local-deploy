using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace EntitlementManagement.AccessPackageCatalog;

public class AccessPackageCatalogHandler : EntitlementManagementResourceHandlerBase<AccessPackageCatalog, AccessPackageCatalogIdentifiers>
{
    protected override async Task<ResourceResponse> Preview(ResourceRequest request, CancellationToken cancellationToken)
    {
        var existing = await GetCatalogAsync(request.Config, request.Properties, cancellationToken);
        if (existing is not null)
        {
            request.Properties.Id = existing.id;
            request.Properties.Description = existing.description;
            request.Properties.IsExternallyVisible = existing.isExternallyVisible;
            request.Properties.CatalogType = existing.catalogType;
            request.Properties.State = existing.state;
        }
        return GetResponse(request);
    }

    protected override async Task<ResourceResponse> CreateOrUpdate(ResourceRequest request, CancellationToken cancellationToken)
    {
        var props = request.Properties;
        var existing = await GetCatalogAsync(request.Config, props, cancellationToken);

        if (existing is null)
        {
            await CreateCatalogAsync(request.Config, props, cancellationToken);
            existing = await GetCatalogAsync(request.Config, props, cancellationToken)
                ?? throw new InvalidOperationException("Catalog creation did not return catalog.");
        }
        else
        {
            if (HasChanges(props, existing))
            {
                await UpdateCatalogAsync(request.Config, existing.id, props, cancellationToken);
                existing = await GetCatalogAsync(request.Config, props, cancellationToken) ?? existing;
            }
        }

        props.Id = existing.id;
        props.Description = existing.description;
        props.IsExternallyVisible = existing.isExternallyVisible;
        props.CatalogType = existing.catalogType;
        props.State = existing.state;
        return GetResponse(request);
    }

    protected override AccessPackageCatalogIdentifiers GetIdentifiers(AccessPackageCatalog properties) => new()
    {
        DisplayName = properties.DisplayName,
    };

    private async Task<dynamic?> GetCatalogAsync(Configuration config, AccessPackageCatalog props, CancellationToken ct)
    {
        try
        {
            using var client = CreateGraphClient(config);
            var filter = Uri.EscapeDataString($"displayName eq '{props.DisplayName}'");
            var resp = await client.GetAsync($"identityGovernance/entitlementManagement/catalogs?$filter={filter}", ct);
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
                        isExternallyVisible = item.TryGetProperty("isExternallyVisible", out var v) && v.GetBoolean(),
                        catalogType = item.TryGetProperty("catalogType", out var t) ? t.GetString() : null,
                        state = item.TryGetProperty("state", out var s) ? s.GetString() : null,
                    };
                }
            }
            return null;
        }
        catch { return null; }
    }

    private async Task CreateCatalogAsync(Configuration config, AccessPackageCatalog props, CancellationToken ct)
    {
        using var client = CreateGraphClient(config);
        var body = new
        {
            displayName = props.DisplayName,
            description = props.Description,
            isExternallyVisible = props.IsExternallyVisible,
            state = props.State?.ToLowerInvariant() ?? "published",
        };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var resp = await client.PostAsync("identityGovernance/entitlementManagement/catalogs", content, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Failed to create catalog: {(int)resp.StatusCode} {resp.ReasonPhrase} {err}");
        }
    }

    private async Task UpdateCatalogAsync(Configuration config, string catalogId, AccessPackageCatalog props, CancellationToken ct)
    {
        using var client = CreateGraphClient(config);
        var body = new
        {
            description = props.Description,
            isExternallyVisible = props.IsExternallyVisible,
            state = props.State?.ToLowerInvariant(),
        };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var req = new HttpRequestMessage(new HttpMethod("PATCH"), $"identityGovernance/entitlementManagement/catalogs/{catalogId}")
        {
            Content = content
        };
        var resp = await client.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Failed to update catalog: {(int)resp.StatusCode} {resp.ReasonPhrase} {err}");
        }
    }

    private static bool HasChanges(AccessPackageCatalog props, dynamic existing)
    {
        return props.Description != existing.description
            || props.IsExternallyVisible != existing.isExternallyVisible
            || (props.CatalogType != null && props.CatalogType != existing.catalogType)
            || (props.State != null && props.State != existing.state);
    }
}
