---
applyTo: "**/*.cs"
description: "C# guidelines for the Azure DevOps Bicep local-deploy extension"
---

# C# Guidelines (Authoritative Style)
- **Adopt Microsoft's Common C# Code Conventions** as the canonical style reference for this project.
- Prefer modern C# features and clarity over cleverness; keep methods small and testable.
- Nullability: enabled; avoid `!` unless justified in a comment.
- Logging: never log secrets.
- **No emoji icons in code**: Keep code professional - avoid emoji icons in Console.WriteLine, comments, or any code. Use clear descriptive text instead.
- **Use "Entra ID" terminology**: Always use "Entra ID" (not "Azure AD") in code, comments, and documentation when referring to Microsoft's identity platform.
- Avoid abbreviations, for example in this context use 'request' instead of 'req' and 'cancellationToken' instead of 'ct':

```csharp
    protected override async Task<ResourceResponse> Preview(ResourceRequest req, CancellationToken ct)
    {
        // Implementation here
    }
```

# Documentation
- Keep names consistent with existing resources (e.g., `AzureDevOpsProject`, `AzureDevOpsRepository`).
- Document the ResourceTypes with `BicepDocHeading`, `BicepDocExample` and `BicepDocCustom`.