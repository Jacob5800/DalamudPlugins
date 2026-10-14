param([Parameter(Mandatory=$true)][string]$PackageDirectory,[Parameter(Mandatory=$true)][string]$InternalName)
$ErrorActionPreference='Stop'
$manifestPath=Join-Path $PackageDirectory "$InternalName.json"
$manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
$manifest|Add-Member -NotePropertyName LastUpdate -NotePropertyValue ([DateTimeOffset]::UtcNow.ToUnixTimeSeconds()) -Force
$manifest|ConvertTo-Json -Depth 20|Set-Content -LiteralPath $manifestPath -Encoding utf8
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open((Join-Path $PackageDirectory 'latest.zip'),[IO.Compression.ZipArchiveMode]::Update)
try {
$old=$archive.GetEntry("$InternalName.json")
if($null-eq$old){throw 'Packaged manifest missing'}
$old.Delete()
[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$manifestPath,"$InternalName.json")|Out-Null
} finally {$archive.Dispose()}