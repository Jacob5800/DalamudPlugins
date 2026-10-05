[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ReleaseTag,

    [string]$FeedPath = (Join-Path $PSScriptRoot '..\repo.json')
)

$ErrorActionPreference = 'Stop'

$webhookUrl = [Environment]::GetEnvironmentVariable('DISCORD_WEBHOOK_URL')
if ([string]::IsNullOrWhiteSpace($webhookUrl)) {
    Write-Host 'DISCORD_WEBHOOK_URL is not configured; skipping the Discord announcement.'
    exit 0
}

$tagMatch = [System.Text.RegularExpressions.Regex]::Match(
    $ReleaseTag,
    '^(?<internalName>[A-Za-z0-9]+)-v(?<version>\d+(?:\.\d+){1,3})$'
)
if (-not $tagMatch.Success) {
    throw "Release tag '$ReleaseTag' does not match the expected PluginName-vVersion format."
}
$internalName = $tagMatch.Groups['internalName'].Value
$version = $tagMatch.Groups['version'].Value

$resolvedFeedPath = [System.IO.Path]::GetFullPath($FeedPath)
if (-not (Test-Path -LiteralPath $resolvedFeedPath -PathType Leaf)) {
    throw "Plugin feed was not found at '$resolvedFeedPath'."
}

$feed = Get-Content -LiteralPath $resolvedFeedPath -Raw | ConvertFrom-Json
$entries = @($feed | Where-Object {
    $_.InternalName -eq $internalName -and
    $_.AssemblyVersion -eq $version
})
if ($entries.Count -ne 1) {
    throw "Expected exactly one feed entry for release tag '$ReleaseTag'; found $($entries.Count)."
}

$plugin = $entries[0]
$notes = [string]$plugin.Changelog
$notes = [System.Text.RegularExpressions.Regex]::Replace(
    $notes,
    '^\s*#{1,6}\s+[^\r\n]+(?:\r?\n)?',
    ''
).Trim()

if ([string]::IsNullOrWhiteSpace($notes)) {
    throw "The feed changelog for '$ReleaseTag' is empty."
}
if ($notes.Length -gt 4000) {
    $notes = $notes.Substring(0, 3997).TrimEnd() + '...'
}

$payload = [ordered]@{
    username = 'Dalamud Plugin Releases'
    allowed_mentions = @{ parse = @() }
    embeds = @(
        [ordered]@{
            title = "$($plugin.Name) $($plugin.AssemblyVersion) released"
            url = "https://github.com/Jacob5800/DalamudPlugins/releases/tag/$ReleaseTag"
            description = $notes
            color = 14202275
            footer = @{ text = 'Jacob5800/DalamudPlugins' }
        }
    )
}
$json = ConvertTo-Json -InputObject $payload -Depth 8 -Compress
$body = [System.Text.Encoding]::UTF8.GetBytes($json)

try {
    Invoke-RestMethod -Uri $webhookUrl -Method Post -ContentType 'application/json; charset=utf-8' -Body $body | Out-Null
    Write-Host "Discord announcement posted for $($plugin.Name) $($plugin.AssemblyVersion)."
}
catch {
    Write-Error 'Discord announcement failed. The webhook URL has been redacted; the plugin release can continue.'
    exit 1
}
