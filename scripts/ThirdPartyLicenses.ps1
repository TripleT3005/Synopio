# Parameters
param(
    [string]$AssetsFile = (Join-Path $PSScriptRoot '..\Synopio\obj\project.assets.json'),
    [string]$LicenseDirectory = (Join-Path $PSScriptRoot '..\licenses'),
    [string]$Title = 'Synopio',
    [string]$Output
)

$ErrorActionPreference = 'Stop'

# Configuration
$allowedLicenses = @('MIT', 'Apache-2.0', 'BSD-2-Clause', 'BSD-3-Clause', 'ISC', 'Zlib')
$buildOnlyPackages = @('Microsoft.NET.ILLink.Tasks')
$separator = '=' * 78

# Helpers
function Get-PackageDirectory([string]$Id, [string]$Version) {
    foreach ($folder in $packageFolders) {
        $directory = Join-Path $folder (Join-Path $Id.ToLowerInvariant() $Version.ToLowerInvariant())
        if (Test-Path $directory) { return $directory }
    }
    throw "Package $Id $Version was not found in the NuGet package folders. Run dotnet restore first."
}

function Get-LicenseFiles([string]$Id, [string]$Directory) {
    $stored = @(Get-ChildItem -Path $LicenseDirectory -Filter "$Id.*" -File -ErrorAction SilentlyContinue | Sort-Object Name)
    if ($stored.Count -gt 0) { return $stored }
    return @(Get-ChildItem -Path $Directory -File | Where-Object { $_.Name -match '^(LICENSE|LICENCE|THIRD-PARTY-NOTICES|NOTICE)' } | Sort-Object Name)
}

function Get-NuspecLicense([string]$Directory) {
    $nuspec = Get-ChildItem -Path $Directory -Filter '*.nuspec' | Select-Object -First 1
    [xml]$xml = Get-Content -Raw -Encoding UTF8 $nuspec.FullName
    $metadata = $xml.package.metadata
    return [pscustomobject]@{
        License = if ($metadata.license -and $metadata.license.type -eq 'expression') { $metadata.license.'#text' } else { $null }
        Url = if ($metadata.projectUrl) { $metadata.projectUrl } else { $metadata.repository.url }
    }
}

# Components
$assets = Get-Content -Raw -Encoding UTF8 $AssetsFile | ConvertFrom-Json
$packageFolders = @($assets.packageFolders.PSObject.Properties.Name)
$components = @()

foreach ($library in $assets.libraries.PSObject.Properties) {
    if ($library.Value.type -ne 'package') { continue }
    $id, $version = $library.Name.Split('/')
    if ($buildOnlyPackages -contains $id) { continue }
    $directory = Get-PackageDirectory $id $version
    $metadata = Get-NuspecLicense $directory
    if ($allowedLicenses -notcontains $metadata.License) {
        throw "Package $id $version uses the license '$($metadata.License)', which is not on the allow list."
    }
    $components += [pscustomobject]@{ Name = $id; Version = $version; License = $metadata.License; Url = $metadata.Url; Files = Get-LicenseFiles $id $directory }
}

foreach ($component in $components) {
    if ($component.Files.Count -eq 0) {
        throw "No license text was found for $($component.Name) $($component.Version). Add it to $LicenseDirectory."
    }
}

# Output
$builder = New-Object Text.StringBuilder
[void]$builder.AppendLine("$Title includes the following third-party components.")
[void]$builder.AppendLine("$Title enth$([char]0x00E4)lt die folgenden Komponenten von Drittanbietern.")
[void]$builder.AppendLine()
foreach ($component in $components | Sort-Object Name) {
    [void]$builder.AppendLine("- $($component.Name) $($component.Version) ($($component.License)) $($component.Url)")
}

foreach ($component in $components | Sort-Object Name) {
    foreach ($file in $component.Files) {
        [void]$builder.AppendLine()
        [void]$builder.AppendLine($separator)
        [void]$builder.AppendLine("$($component.Name) $($component.Version) - $($file.Name)")
        [void]$builder.AppendLine($separator)
        [void]$builder.AppendLine()
        [void]$builder.AppendLine((Get-Content -Raw -Encoding UTF8 $file.FullName).Replace("`r`n", "`n").TrimEnd())
    }
}

$text = $builder.ToString().Replace("`r`n", "`n")
if ($Output) {
    [IO.File]::WriteAllText($Output, $text, (New-Object Text.UTF8Encoding $false))
    Write-Output "Wrote $Output with $($components.Count) components."
} else {
    Write-Output "$($components.Count) components checked."
}
