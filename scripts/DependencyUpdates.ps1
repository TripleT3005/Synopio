# Parameters
param(
    [string]$Repository = (Join-Path $PSScriptRoot '..'),
    [string]$Since,
    [string]$TagPattern = 'v*',
    [string]$Check,
    [string]$Changelog = 'CHANGELOG.md',
    [string[]]$Lockfiles = @('Synopio/packages.lock.json', 'Synopio.Engine/packages.lock.json'),
    [string]$ShippedLockfile = 'Synopio/packages.lock.json'
)

$ErrorActionPreference = 'Stop'

# Configuration
$buildOnlyPackages = @('Microsoft.NET.ILLink.Tasks')
$developmentType = 'direct:development'
$languages = [ordered]@{ English = '### English'; Deutsch = '### Deutsch' }
$entryTemplates = [ordered]@{
    English = "#### Changed`n- Updated third-party components: {0}."
    Deutsch = "#### Ge$([char]0x00E4)ndert`n- Fremdkomponenten aktualisiert: {0}."
}

# Git
function Invoke-Git([string[]]$Arguments) {
    $output = & git -C $Repository @Arguments
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') failed." }
    return ($output -join "`n")
}

function Get-LatestTag {
    $tag = & git -C $Repository describe --tags --abbrev=0 --match $TagPattern HEAD 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    return $tag
}

function Get-DependabotMessages([string]$Start) {
    $range = if ($Start) { "$Start..HEAD" } else { 'HEAD' }
    $log = Invoke-Git (@('log', '--reverse', '--author=dependabot\[bot\]', '--format=%B%x1e', $range, '--') + $Lockfiles)
    return @($log -split [char]0x1e | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}

# Commit messages
function Get-Updates([string]$Message) {
    $updates = @()
    $current = $null
    $inBlock = $false
    foreach ($line in $Message -split "`n") {
        $stripped = $line.Trim()
        if ($stripped -eq 'updated-dependencies:') { $inBlock = $true; continue }
        if (-not $inBlock) { continue }
        if ($stripped -eq '...') { break }
        if ($stripped -notmatch '^(-\s+)?([\w-]+):\s*(.*)$') { continue }
        if ($Matches[1]) {
            if ($current) { $updates += [pscustomobject]$current }
            $current = @{}
        }
        if ($null -ne $current) { $current[$Matches[2]] = $Matches[3].Trim().Trim("'", '"') }
    }
    if ($current) { $updates += [pscustomobject]$current }
    return @($updates | Where-Object { $_.'dependency-name' -and $_.'dependency-version' })
}

function Get-CollectedUpdates([string[]]$Messages) {
    $latest = [ordered]@{}
    foreach ($message in $Messages) {
        foreach ($update in Get-Updates $message) {
            if ($update.'dependency-type' -ne $developmentType) {
                $latest[$update.'dependency-name'.ToLowerInvariant()] = $update
            }
        }
    }
    return $latest
}

function Get-ShippedPackages {
    $lock = Get-Content -Raw -Encoding UTF8 (Join-Path $Repository $ShippedLockfile) | ConvertFrom-Json
    $packages = @{}
    foreach ($framework in $lock.dependencies.PSObject.Properties) {
        foreach ($package in $framework.Value.PSObject.Properties) {
            if ($package.Value.type -eq 'Project' -or $buildOnlyPackages -contains $package.Name) { continue }
            $packages[$package.Name.ToLowerInvariant()] = [pscustomobject]@{ Name = $package.Name; Version = $package.Value.resolved }
        }
    }
    return $packages
}

# Changelog
function Get-ChangelogSection([string]$Text, [string]$Version) {
    $heading = "## [$Version]"
    $lines = @()
    $found = $false
    foreach ($line in $Text -split "`r?`n") {
        if ($line.StartsWith($heading)) { $found = $true; continue }
        if ($found -and $line.StartsWith('## [')) { break }
        if ($found) { $lines += $line }
    }
    if (-not $found) { return $null }
    return ($lines -join "`n")
}

function Get-LanguageParts([string]$Section) {
    $parts = @{}
    $current = $null
    foreach ($line in $Section -split "`n") {
        $language = $languages.Keys | Where-Object { $line.Trim() -eq $languages[$_] } | Select-Object -First 1
        if ($language) { $current = $language; $parts[$current] = @() }
        elseif ($current) { $parts[$current] += $line }
    }
    $result = @{}
    foreach ($key in $parts.Keys) { $result[$key] = $parts[$key] -join "`n" }
    return $result
}

function Test-Mention([string]$Text, $Update) {
    $pattern = '(?<![\w.-])`?' + [regex]::Escape($Update.Name) + '`?\s+`?v?' + [regex]::Escape($Update.Version) + '`?(?!\w|\.\d)'
    return [regex]::IsMatch($Text, $pattern)
}

function Format-UpdateList($Updates) {
    return (($Updates | ForEach-Object { "$($_.Name) $($_.Version)" }) -join ', ')
}

function Get-SuggestedEntry($Updates) {
    $list = Format-UpdateList $Updates
    return (($entryTemplates.Keys | ForEach-Object { "$($languages[$_])`n`n$($entryTemplates[$_] -f $list)" }) -join "`n`n")
}

# Main
$Repository = (Resolve-Path $Repository).Path
if (-not $Since) { $Since = Get-LatestTag }
$start = if ($Since) { $Since } else { 'the first commit' }
$collected = Get-CollectedUpdates (Get-DependabotMessages $Since)
$shippedPackages = Get-ShippedPackages
$shipped = @(@(
        foreach ($key in $collected.Keys) {
            if (-not $shippedPackages.ContainsKey($key)) { continue }
            $package = $shippedPackages[$key]
            [pscustomobject]@{ Name = $package.Name; Version = $package.Version }
        }
    ) | Sort-Object Name)

if ($shipped.Count -eq 0) {
    Write-Output "No Dependabot updates of shipped components since $start"
    exit 0
}

Write-Output "Dependabot updates of shipped components since ${start}: $(Format-UpdateList $shipped)"
if (-not $Check) {
    Write-Output "`nSuggested changelog entry:`n`n$(Get-SuggestedEntry $shipped)"
    exit 0
}

$changelogPath = if ([IO.Path]::IsPathRooted($Changelog)) { $Changelog } else { Join-Path $Repository $Changelog }
$section = Get-ChangelogSection (Get-Content -Raw -Encoding UTF8 $changelogPath) $Check
if ($null -eq $section) {
    Write-Output "$Changelog has no section for $Check"
    exit 1
}
$parts = Get-LanguageParts $section
$missing = @(
    foreach ($language in $languages.Keys) {
        $text = if ($parts.ContainsKey($language)) { $parts[$language] } else { '' }
        foreach ($update in $shipped) {
            if (-not (Test-Mention $text $update)) { "Missing in ${language}: $($update.Name) $($update.Version)" }
        }
    }
)
if ($missing.Count -gt 0) {
    $missing | ForEach-Object { Write-Output $_ }
    Write-Output "`nAdd an entry to the section for $Check, for example:`n`n$(Get-SuggestedEntry $shipped)"
    exit 1
}
Write-Output "The changelog section for $Check mentions every update"
