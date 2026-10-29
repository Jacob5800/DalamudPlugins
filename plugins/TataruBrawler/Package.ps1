param()
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$archivePath = Join-Path $PSScriptRoot 'vendor\TataruBrawler-1.0.8.0.zip'
$expectedHash = '20A6FA784FF086DF349DE3C70AF0409A9D3855408D93D05EC84BA7E724E3A47C'
if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Supplied archive hash mismatch' }
& dotnet build (Join-Path $PSScriptRoot 'src/TataruBrawler.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Custom source build failed' }
$output = Join-Path $PSScriptRoot 'dist\package'
New-Item -ItemType Directory -Path $output -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $originalManifestEntry = $archive.GetEntry('TataruBrawler.json')
    $reader = [IO.StreamReader]::new($originalManifestEntry.Open())
    try { $originalManifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    $manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'TataruBrawler.json') -Raw | ConvertFrom-Json
    if ($manifest.InternalName -ne 'TataruBrawler') { throw 'Manifest identity/version mismatch' }
    if ([string]::IsNullOrWhiteSpace($manifest.Changelog)) { throw 'Missing changelog' }
    foreach ($name in @('TataruBrawler.dll','TataruBrawler.deps.json','LICENSE','NOTICE','README.md','VALIDATION.md','images/icon.png')) {
        $entry = $archive.GetEntry($name)
        if ($null -eq $entry) { throw "Missing supplied file: $name" }
        $dest = Join-Path $output $name
        New-Item -ItemType Directory -Path (Split-Path $dest -Parent) -Force | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $dest, $true)
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src/bin/Release/net10.0-windows7.0/TataruBrawler.dll') -Destination (Join-Path $output 'TataruBrawler.dll') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src/bin/Release/net10.0-windows7.0/TataruBrawler.deps.json') -Destination (Join-Path $output 'TataruBrawler.deps.json') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'NOTICE') -Destination (Join-Path $output 'NOTICE') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $output 'README.md') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'VALIDATION.md') -Destination (Join-Path $output 'VALIDATION.md') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE-Saucy.md') -Destination (Join-Path $output 'LICENSE-Saucy.md') -Force
    foreach ($notice in @('LICENSE-AGPL.md','NOTICE-AutoKeyboundBrawler')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $notice) -Destination (Join-Path $output $notice) -Force }
    $assembly = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $output 'TataruBrawler.dll'))
    if ($assembly.Name -ne $manifest.InternalName -or $assembly.Version.ToString() -ne $manifest.AssemblyVersion) { throw 'DLL identity/version mismatch' }
    $manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $output 'TataruBrawler.json') -Encoding utf8
    $zipPath = Join-Path $output 'latest.zip'
    # Create truncates only this owned output ZIP; no recursive deletion needed.
    $stream = [IO.File]::Open($zipPath, [IO.FileMode]::Create)
    $package = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($name in @('TataruBrawler.dll','TataruBrawler.json','TataruBrawler.deps.json','LICENSE','LICENSE-Saucy.md','LICENSE-AGPL.md','NOTICE-AutoKeyboundBrawler','NOTICE','README.md','VALIDATION.md','images/icon.png')) {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($package,(Join-Path $output $name),$name) | Out-Null
        }
    } finally { $package.Dispose(); $stream.Dispose() }
} finally { $archive.Dispose() }
& (Join-Path $root 'scripts\Stamp-PackageDate.ps1') -PackageDirectory $output -InternalName 'TataruBrawler'
$check = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $dllStream = $check.GetEntry('TataruBrawler.dll').Open()
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $packagedHash = [Convert]::ToHexString($sha.ComputeHash($dllStream)) } finally { $dllStream.Dispose(); $sha.Dispose() }
    if ($packagedHash -ne (Get-FileHash (Join-Path $output 'TataruBrawler.dll') -Algorithm SHA256).Hash) { throw 'Packaged DLL changed' }
    $reader = [IO.StreamReader]::new($check.GetEntry('TataruBrawler.json').Open())
    try { $saved = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    if ($saved.LastUpdate -le 0 -or $saved.AssemblyVersion -ne $manifest.AssemblyVersion -or $saved.Changelog -ne $manifest.Changelog) { throw 'Packaged metadata mismatch' }
} finally { $check.Dispose() }
Write-Output "Packaged $($manifest.Name) $($manifest.AssemblyVersion); custom source built and packaged."

# Corresponding source accompanies the AGPL derivative release. Exclude outputs and
# build outputs; all required source/build scripts and original notices are included.
$sourceZipPath = Join-Path $output 'TataruBrawler-source.zip'
$sourceStream = [IO.File]::Open($sourceZipPath, [IO.FileMode]::Create)
$sourceZip = [IO.Compression.ZipArchive]::new($sourceStream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File) {
        $relative = [IO.Path]::GetRelativePath($PSScriptRoot, $file.FullName).Replace('\','/')
        if ($relative -match '(^|/)(bin|obj|dist)/') { continue }
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($sourceZip,$file.FullName,$relative) | Out-Null
    }
} finally { $sourceZip.Dispose(); $sourceStream.Dispose() }
