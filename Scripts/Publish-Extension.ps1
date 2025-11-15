#!/usr/bin/env pwsh
[cmdletbinding()]
param(
   [Parameter(Mandatory=$false)][string]$Target = "./entitlementmgmt-ext"
)

$ErrorActionPreference = "Stop"

function ExecSafe([scriptblock] $ScriptBlock) {
  & $ScriptBlock
  if ($LASTEXITCODE -ne 0) {
      exit $LASTEXITCODE
  }
}

function Clone-JsonObject([object] $Object) {
  return $Object | ConvertTo-Json -Depth 100 | ConvertFrom-Json
}

function Fix-RequestorSettingsTypes([string] $TypesPath) {
  if (-not (Test-Path $TypesPath)) {
    throw "Types file not found: $TypesPath"
  }

  $jsonText = Get-Content -Raw -Path $TypesPath
  $types = $jsonText | ConvertFrom-Json -AsHashtable
  $patched = $false

  $requestorSettings = $types | Where-Object { $_['name'] -eq "AccessPackageAssignmentRequestorSettings" }
  if (-not $requestorSettings) {
    throw "AccessPackageAssignmentRequestorSettings type not found in $TypesPath"
  }

  $properties = [ordered]@{}
  foreach ($prop in $requestorSettings.properties.GetEnumerator()) {
    $properties[$prop.Key] = $prop.Value
  }

  $allowed = $properties["allowedRequestors"]
  $onBehalf = $properties["onBehalfRequestors"]

  if (-not $allowed -and $onBehalf) {
    $clone = Clone-JsonObject $onBehalf
    $properties["allowedRequestors"] = $clone
    $properties["allowedRequestors"].description = "Specific users or groups allowed to request when allowedTargetScope uses SpecificDirectoryUsers or SpecificConnectedOrganizationUsers."
    $patched = $true
  }

  if (-not $onBehalf -and $allowed) {
    $clone = Clone-JsonObject $allowed
    $properties["onBehalfRequestors"] = $clone
    $properties["onBehalfRequestors"].description = "Who can request on behalf of others (self-service or manager scenarios)."
    $patched = $true
  }

  if ($patched) {
    $requestorSettings.properties = $properties
  }

  if ($patched) {
    $types | ConvertTo-Json -Depth 100 | Set-Content -Path $TypesPath -Encoding utf8NoBOM
  }

  return $patched
}

function Patch-ExtensionTypes([string] $ExtensionPath, [string] $SampleTypesPath, [string] $SampleTypesArchive) {
  $resolvedExtension = (Resolve-Path $ExtensionPath).Path
  $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid())
  New-Item -ItemType Directory -Path $tempDir | Out-Null

  try {
    ExecSafe { tar -xzf $resolvedExtension -C $tempDir }

    $typesArchive = Join-Path $tempDir "types.tgz"
    if (-not (Test-Path $typesArchive)) {
      throw "types.tgz not found inside extension package"
    }

    ExecSafe { tar -xf $typesArchive -C $tempDir }

    $typesJsonPath = Join-Path $tempDir "types.json"
    if (-not (Test-Path $typesJsonPath)) {
      throw "types.json not found inside types.tgz"
    }

    $patched = Fix-RequestorSettingsTypes -TypesPath $typesJsonPath

    if ($patched) {
      if ($SampleTypesPath) {
        Copy-Item -Path $typesJsonPath -Destination $SampleTypesPath -Force
      }

      if ($SampleTypesArchive -and $SampleTypesPath) {
        $sampleDir = Split-Path -Parent $SampleTypesPath
        $sampleFile = Split-Path -Leaf $SampleTypesPath
        ExecSafe { tar -czf $SampleTypesArchive -C $sampleDir $sampleFile }
      }
    }

    $typesIndexPath = Join-Path $tempDir "index.json"
    ExecSafe { tar -czf $typesArchive -C $tempDir index.json types.json }
    Remove-Item $typesJsonPath -Force
    if (Test-Path $typesIndexPath) {
      Remove-Item $typesIndexPath -Force
    }

    $childNames = Get-ChildItem -Name -Path $tempDir
    if (-not $childNames) {
      throw "No files found to package in extension archive"
    }

    $fileList = Join-Path $tempDir ".bicep-ext-filelist"
    $childNames | Set-Content -Path $fileList -Encoding utf8
    ExecSafe { tar -czf $resolvedExtension -C $tempDir -T $fileList }
    Remove-Item $fileList -Force
  }
  finally {
    Remove-Item -Recurse -Force $tempDir
  }
}

$root = "$PSScriptRoot/.."
$extName = "entitlementmgmt"

Write-Host "Building extension for all platforms..." -ForegroundColor Cyan

# build various flavors
ExecSafe { dotnet publish --configuration Release "$root/src" -r osx-arm64 }
ExecSafe { dotnet publish --configuration Release "$root/src" -r linux-x64 }
ExecSafe { dotnet publish --configuration Release "$root/src" -r win-x64 }

Write-Host "Publishing extension to $Target..." -ForegroundColor Cyan

# publish to the registry
ExecSafe { bicep publish-extension `
  --bin-osx-arm64 "$root/src/bin/Release/net9.0/osx-arm64/publish/$extName" `
  --bin-linux-x64 "$root/src/bin/Release/net9.0/linux-x64/publish/$extName" `
  --bin-win-x64 "$root/src/bin/Release/net9.0/win-x64/publish/$extName.exe" `
  --target "$Target" `
  --force }

Write-Host "Patching generated type metadata to expose both requestor arrays..." -ForegroundColor Cyan
Patch-ExtensionTypes -ExtensionPath $Target -SampleTypesPath "$root/Sample/types.json" -SampleTypesArchive "$root/Sample/types.tgz"

Write-Host "Extension published successfully to $Target" -ForegroundColor Green
