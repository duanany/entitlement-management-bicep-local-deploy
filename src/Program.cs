using Microsoft.AspNetCore.Builder;
using Bicep.Local.Extension.Host.Extensions;
using Microsoft.Extensions.DependencyInjection;
using EntitlementManagement;
using EntitlementManagement.AccessPackageCatalog;
using EntitlementManagement.AccessPackage;
using EntitlementManagement.AccessPackageCatalogResource;
using EntitlementManagement.AccessPackageResourceRoleScope;
using EntitlementManagement.AccessPackageAssignmentPolicy;
using EntitlementManagement.AccessPackageAssignment;
using EntitlementManagement.SecurityGroup;
using EntitlementManagement.GroupPimEligibility;

var builder = WebApplication.CreateBuilder();

builder.AddBicepExtensionHost(args);
builder.Services
    .AddBicepExtension(
        name: "EntitlementManagement",
        version: "0.1.0",
        isSingleton: true,
        typeAssembly: typeof(Program).Assembly,
        configurationType: typeof(Configuration))
    .WithResourceHandler<SecurityGroupHandler>()
    .WithResourceHandler<GroupPimEligibilityHandler>()
    .WithResourceHandler<AccessPackageCatalogHandler>()
    .WithResourceHandler<AccessPackageHandler>()
    .WithResourceHandler<AccessPackageCatalogResourceHandler>()
    .WithResourceHandler<AccessPackageResourceRoleScopeHandler>()
    .WithResourceHandler<AccessPackageAssignmentPolicyHandler>()
    .WithResourceHandler<AccessPackageAssignmentHandler>();

var app = builder.Build();

app.MapBicepExtension();

await app.RunAsync();
