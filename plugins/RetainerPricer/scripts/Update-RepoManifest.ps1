param(
    [Parameter(Mandatory = $true)]
    [string] $ManifestPath,

    [Parameter(Mandatory = $true)]
    [string] $OutputPath,

    [Parameter(Mandatory = $true)]
    [string] $ReleaseTag,

    [string] $AssetName = 'RetainerPricer.zip'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$feedPath = Join-Path $repositoryRoot 'repo.json'
& (Join-Path $repositoryRoot 'scripts\Update-RepoFeed.ps1') -ManifestPath $ManifestPath -ReleaseTag $ReleaseTag -AssetName $AssetName -FeedPath $feedPath
if ((Resolve-Path -LiteralPath $OutputPath -ErrorAction SilentlyContinue) -ne $feedPath) {
    Copy-Item -LiteralPath $feedPath -Destination $OutputPath -Force
}
