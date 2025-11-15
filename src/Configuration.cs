using Azure.Bicep.Types.Concrete;

namespace EntitlementManagement;

public class Configuration
{
    [TypeProperty("Microsoft Graph API Bearer Token for Entitlement Management operations (requires EntitlementManagement.ReadWrite.All permission). If omitted, uses ENTITLEMENT_TOKEN environment variable.")]
    public string? EntitlementToken { get; set; }

    [TypeProperty("Microsoft Graph API Bearer Token for Group and User operations (requires Group.ReadWrite.All and User.Read.All permissions). If omitted, uses GROUP_USER_TOKEN environment variable.")]
    public string? GroupUserToken { get; set; }

    [TypeProperty("Graph API base URL. Defaults to https://graph.microsoft.com/v1.0")]
    public string? GraphApiBaseUrl { get; set; }
}
