param(
    [Parameter(Mandatory)] [string] $OutputDirectory,
    [string] $CacheDirectory = (Join-Path $PSScriptRoot '../artifacts/third-party-sources')
)

$ErrorActionPreference = 'Stop'
$lockPath = Join-Path $PSScriptRoot 'native-source-lock.json'
$sourceLock = Get-Content $lockPath -Raw | ConvertFrom-Json
[xml]$viewerProject = Get-Content (Join-Path $PSScriptRoot '../XISFLib.Viewer.Core/XISFLib.Viewer.Core.csproj') -Raw
$packageVersion = ($viewerProject.Project.ItemGroup.PackageReference | Where-Object Include -eq 'Magick.NET-Q16-AnyCPU').Version
if ($packageVersion -ne $sourceLock.magickNetVersion) {
    throw 'The native source lock must be reviewed for the new Magick.NET version.'
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$CacheDirectory = [IO.Path]::GetFullPath($CacheDirectory)
$null = New-Item -ItemType Directory -Path $OutputDirectory,$CacheDirectory -Force
$stage = Join-Path $OutputDirectory ('native-sources-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $stage
foreach ($source in $sourceLock.sources) {
    $cached = Join-Path $CacheDirectory $source.filename
    if (-not (Test-Path -LiteralPath $cached)) {
        Invoke-WebRequest -Uri $source.url -OutFile $cached
    }
    if ((Get-FileHash -LiteralPath $cached -Algorithm SHA256).Hash -ine $source.sha256) {
        throw "Native source checksum mismatch: $($source.repository)"
    }
    Copy-Item -LiteralPath $cached -Destination $stage
}
Copy-Item -LiteralPath $lockPath -Destination (Join-Path $stage 'manifest.json')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../docs/native-rebuild.md') -Destination (Join-Path $stage 'REBUILD.md')
$archive = Join-Path $OutputDirectory "XISFLib.NativeSources-Magick.NET-$($sourceLock.magickNetVersion).zip"
if (Test-Path -LiteralPath $archive) {
    throw 'A native-source ZIP already exists; use a fresh output directory.'
}
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $archive, [IO.Compression.CompressionLevel]::NoCompression, $false)
Write-Output $archive
