param(
    [Parameter(Mandatory = $true)]
    [string] $ManifestPath,

    [Parameter(Mandatory = $true)]
    [string] $ReleaseTag,

    [Parameter(Mandatory = $true)]
    [string] $AssetName,

    [string] $FeedPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'repo.json')
)

$ErrorActionPreference = 'Stop'
$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($manifest.InternalName) -or [string]::IsNullOrWhiteSpace($manifest.AssemblyVersion)) {
    throw 'The packaged plugin manifest is missing its internal name or assembly version.'
}
if ([string]::IsNullOrWhiteSpace([string]$manifest.Changelog)) {
    throw 'The packaged plugin manifest must include user-facing Changelog text before a release can update the feed.'
}

$feed = @(Get-Content -LiteralPath $FeedPath -Raw | ConvertFrom-Json)
$entry = $feed | Where-Object { $_.InternalName -eq $manifest.InternalName } | Select-Object -First 1
if ($null -eq $entry) {
    throw "No root repo.json entry exists for $($manifest.InternalName)."
}

$entry.Name = $manifest.Name
$entry.AssemblyVersion = $manifest.AssemblyVersion
$entry.RepoUrl = 'https://github.com/Jacob5800/DalamudPlugins'
$entry.ApplicableVersion = $manifest.ApplicableVersion
$entry.DalamudApiLevel = $manifest.DalamudApiLevel
$entry.Punchline = $manifest.Punchline
$entry.Description = $manifest.Description
if (-not [string]::IsNullOrWhiteSpace([string]$manifest.IconUrl)) {
    $entry | Add-Member -NotePropertyName IconUrl -NotePropertyValue $manifest.IconUrl -Force
}
$entry.Changelog = $manifest.Changelog
$download = "https://github.com/Jacob5800/DalamudPlugins/releases/download/$ReleaseTag/$AssetName"
$entry.DownloadLinkInstall = $download
$entry.DownloadLinkUpdate = $download
$entry.DownloadLinkTesting = $download
$entry.DownloadLinkTesting = $null
$entry.LastUpdate = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds().ToString()

ConvertTo-Json -InputObject @($feed) -Depth 10 | Set-Content -LiteralPath $FeedPath -Encoding utf8
