using Azure.Bicep.Types.Concrete;

namespace EntitlementManagement.AccessPackageAssignmentPolicy;

public class AccessPackageAssignmentPolicyIdentifiers
{
    [TypeProperty("The ID of the access package this policy applies to.", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string AccessPackageId { get; set; }

    [TypeProperty("The display name of the policy. Used to uniquely identify the policy within an access package.", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string DisplayName { get; set; }
}
