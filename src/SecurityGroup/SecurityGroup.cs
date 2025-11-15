using Azure.Bicep.Types.Concrete;

namespace EntitlementManagement.SecurityGroup;

/// <summary>
/// Identifiers for Security Group resource.
/// uniqueName is used as mailNickname in Graph API for idempotency.
/// </summary>
public class SecurityGroupIdentifiers
{
    [TypeProperty("Unique mail nickname for the group (immutable identifier, e.g., 'platform-team-admins'). Used as mailNickname in Graph API.", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string UniqueName { get; set; }
}

/// <summary>
/// Members configuration for security group.
/// </summary>
public class SecurityGroupMembers
{
    [TypeProperty("Array of Object IDs (users or groups) for group members. Supports nested groups. Example: ['userId-guid', 'groupId-guid']")]
    public string[]? MemberIds { get; set; }
}

/// <summary>
/// Properties for creating/managing an Entra ID security group.
/// Idempotent based on uniqueName (mailNickname).
/// </summary>
[ResourceType("securityGroup")]
public class SecurityGroup : SecurityGroupIdentifiers
{
    [TypeProperty("Display name of the security group (can be updated)")]
    public required string DisplayName { get; set; }

    [TypeProperty("Description for the security group (can be updated)")]
    public string? Description { get; set; }

    [TypeProperty("Whether the group is mail-enabled. Default: false (security groups are not mail-enabled)")]
    public bool MailEnabled { get; set; } = false;

    [TypeProperty("Whether the group is security-enabled. Default: true (this is a security group)")]
    public bool SecurityEnabled { get; set; } = true;

    [TypeProperty("Array of Object IDs (users or groups) for group members. Supports nested groups. Example: ['userId-guid', 'groupId-guid']")]
    public string[]? Members { get; set; }

    // Outputs
    [TypeProperty("The unique identifier (GUID) of the security group", ObjectTypePropertyFlags.ReadOnly)]
    public string? Id { get; set; }

    [TypeProperty("The date/time when the security group was created (ISO 8601 format)", ObjectTypePropertyFlags.ReadOnly)]
    public string? CreatedDateTime { get; set; }
}
