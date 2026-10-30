param()
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
& dotnet build (Join-Path $PSScriptRoot 'BiSplanner/BiSplanner.csproj') -c Release --nologo -p:RestoreLockedMode=true -p:RestorePackagesWithLockFile=true
if ($LASTEXITCODE -ne 0) { throw 'BiSplanner build failed' }
$built = Join-Path $PSScriptRoot 'BiSplanner/bin/Release/BiSplanner'
$manifestPath = Join-Path $built 'BiSplanner.json'
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
[xml]$project = Get-Content (Join-Path $PSScriptRoot 'BiSplanner/BiSplanner.csproj') -Raw
$version = [string]$project.Project.PropertyGroup.Version
if ($manifest.InternalName -ne 'BiSplanner' -or $manifest.AssemblyVersion -ne $version) { throw 'Plugin identity/version mismatch' }
$notes = (Get-Content (Join-Path $PSScriptRoot 'CHANGELOG.md') -Raw) -split "(?m)^## ", 3
if (!(($notes[1] -split "`r?`n", 2)[0]).Trim().Equals($version)) { throw 'Changelog version mismatch' }
$manifest.Changelog = '## ' + $notes[1].Trim() + "`n"
$manifest | ConvertTo-Json -Depth 20 | Set-Content $manifestPath -Encoding utf8
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open((Join-Path $built 'latest.zip'), [IO.Compression.ZipArchiveMode]::Update)
try {
    $archive.GetEntry('BiSplanner.json').Delete()
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $manifestPath, 'BiSplanner.json') | Out-Null
    foreach ($entry in @(@('LICENSE.md','LICENSE.md'), @('KamiToolKit/LICENSE','LICENSE-KamiToolKit.txt'), @('UPSTREAM.md','UPSTREAM.md'), @('README.md','README.md'))) {
        $old = $archive.GetEntry($entry[1]); if ($old) { $old.Delete() }
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $PSScriptRoot $entry[0]), $entry[1]) | Out-Null
    }
} finally { $archive.Dispose() }
$stampScript = Join-Path $repository 'scripts/Stamp-PackageDate.ps1'
if (!(Test-Path $stampScript)) { $stampScript = Join-Path $PSScriptRoot 'build-support/Stamp-PackageDate.ps1' }
& $stampScript -PackageDirectory $built -InternalName 'BiSplanner'
$output = Join-Path $PSScriptRoot 'dist/package'
New-Item -ItemType Directory -Path $output -Force | Out-Null
Copy-Item (Join-Path $built 'latest.zip') (Join-Path $output 'BiSplanner.zip') -Force
Copy-Item $manifestPath (Join-Path $output 'BiSplanner.json') -Force
$archive = [IO.Compression.ZipFile]::OpenRead((Join-Path $output 'BiSplanner.zip'))
try {
    foreach ($required in @('BiSplanner.dll','KamiToolKit.dll','LICENSE.md','LICENSE-KamiToolKit.txt','UPSTREAM.md')) {
        if (!$archive.GetEntry($required)) { throw "Missing package entry: $required" }
    }
    $reader = [IO.StreamReader]::new($archive.GetEntry('BiSplanner.json').Open())
    try { $saved = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    if ($saved.LastUpdate -le 0 -or $saved.Changelog -ne $manifest.Changelog -or $saved.AssemblyVersion -ne $version) { throw 'Packaged metadata mismatch' }
}
finally { $archive.Dispose() }
$sourceZip = Join-Path $output 'BiSplanner-source.zip'
$stream = [IO.File]::Open($sourceZip, [IO.FileMode]::Create)
$source = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($source, $stampScript, 'build-support/Stamp-PackageDate.ps1') | Out-Null
    foreach ($file in Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File) {
        $relative = [IO.Path]::GetRelativePath($PSScriptRoot, $file.FullName).Replace('\','/')
        if ($relative -match '(^|/)(bin|obj|dist|__pycache__|[.]git|[.]vs)/' -or $relative -eq '.git') { continue }
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($source, $file.FullName, $relative) | Out-Null
    }
} finally { $source.Dispose(); $stream.Dispose() }
Write-Output "Packaged BiSplanner $version with licenses and corresponding source."
