param([Parameter(Mandatory)][string]$PackageDirectory)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $PackageDirectory 'latest.zip'
if (-not (Test-Path -LiteralPath $zip -PathType Leaf)) { throw 'Packaged Workshoppa ZIP missing' }
$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Update)
try {
    foreach ($name in @('AutoRetainer-LICENSE.md','VOYAGE-REFERENCES.md','Argus-LICENSE.md','Argus-THIRD-PARTY-NOTICES.txt','SubmarineTracker-LICENSE.md')) {
        $source = Join-Path (Join-Path $PSScriptRoot 'Workshoppa') $name
        $entry = $archive.GetEntry($name)
        if ($null -eq $entry) { $entry = $archive.CreateEntry($name) }
        $stream = $entry.Open()
        try {
            $stream.SetLength(0)
            $bytes = [IO.File]::ReadAllBytes($source)
            $stream.Write($bytes, 0, $bytes.Length)
        } finally { $stream.Dispose() }
        Copy-Item -LiteralPath $source -Destination (Join-Path $PackageDirectory $name) -Force
    }
} finally { $archive.Dispose() }
