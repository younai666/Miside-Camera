param(
    [Parameter(Mandatory = $true)][string]$HostDirectory,
    [string]$OutputDirectory,
    [Parameter(Mandatory = $true)][string]$MediaDirectory,
    [string]$Dotnet = 'dotnet',
    [string]$DesktopDotnet = $Dotnet
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$hostRoot = (Resolve-Path -LiteralPath $HostDirectory).Path
$mediaRoot = (Resolve-Path -LiteralPath $MediaDirectory).Path
foreach ($required in @('ffmpeg/ffmpeg.exe', 'ffmpeg/licenses/COPYING.LGPLv2.1', 'ffmpeg/sources/ffmpeg-n9.0.2.tar.gz',
    'ffmpeg/sources/nv-codec-headers-n13.0.19.0.tar.gz', 'ffmpeg/sources/build_ffmpeg.sh', 'mediamtx/mediamtx.exe', 'mediamtx/LICENSE', 'mediamtx/licenses/Go-LICENSE.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $mediaRoot $required))) { throw "Missing bundled media component: $required" }
}
if (-not (Test-Path -LiteralPath (Join-Path $hostRoot 'BepInEx/interop/Assembly-CSharp.dll'))) {
    throw 'HostDirectory must contain the matching locally generated IL2CPP interop.'
}
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'artifacts/desktop' }
$adapterDirectory = Join-Path $repoRoot 'artifacts/adapters'
New-Item -ItemType Directory -Path $adapterDirectory -Force | Out-Null

foreach ($project in @('Miside.Camera.Plugin', 'Miside.Camera.NeuroMita', 'Miside.Camera.Streaming')) {
    & $Dotnet build (Join-Path $repoRoot "pc/$project/$project.csproj") -c Release "-p:HostDirectory=$hostRoot" -v:q
    if ($LASTEXITCODE -ne 0) { throw "Adapter build failed: $project" }
}
# Explicit allowlist: never package the host, interop or third-party loader DLLs.
foreach ($project in @('Miside.Camera.Plugin', 'Miside.Camera.Core', 'Miside.Camera.NeuroMita', 'Miside.Camera.Streaming')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot "pc/$project/bin/Release/net6.0/$project.dll") -Destination $adapterDirectory
}
& $DesktopDotnet publish (Join-Path $repoRoot 'desktop/Miside.Camera.Desktop/Miside.Camera.Desktop.csproj') `
    -c Release -r win-x64 --self-contained true "-p:AdapterDirectory=$adapterDirectory" -o $OutputDirectory -v:q
if ($LASTEXITCODE -ne 0) { throw 'Desktop publish failed' }
New-Item -ItemType Directory -Path (Join-Path $OutputDirectory 'tools') -Force | Out-Null
foreach ($name in @('ffmpeg', 'mediamtx')) {
    Copy-Item -LiteralPath (Join-Path $mediaRoot $name) -Destination (Join-Path $OutputDirectory 'tools') -Recurse -Force
}

# Self-contained .NET/WPF belongs to the bundle; retain notices from the exact runtime packs.
$packageLocation = & $DesktopDotnet nuget locals global-packages --list --force-english-output
if ($LASTEXITCODE -ne 0 -or $packageLocation -notmatch '^global-packages:\s*(.+)$') { throw 'Cannot locate runtime package notices' }
$packageRoot = $Matches[1].Trim()
$dependencies = Get-Content -LiteralPath (Join-Path $OutputDirectory 'Miside Camera Desktop.deps.json') -Raw | ConvertFrom-Json
foreach ($library in $dependencies.libraries.psobject.Properties.Name) {
    if (-not $library.StartsWith('runtimepack.')) { continue }
    $package = $library.Substring('runtimepack.'.Length).ToLowerInvariant()
    $source = Join-Path $packageRoot $package
    $notices = Get-ChildItem -LiteralPath $source -File | Where-Object { $_.Name -match '^(LICENSE(\.TXT)?|THIRD-PARTY-NOTICES\.TXT)$' }
    if (-not ($notices | Where-Object Name -Match '^LICENSE')) { throw "Missing runtime license: $package" }
    $destination = Join-Path $OutputDirectory "licenses/$package"
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    $notices | Copy-Item -Destination $destination
}
foreach ($notice in @('LICENSE', 'THIRD_PARTY_NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $notice) -Destination $OutputDirectory
}
Write-Output "Complete Desktop bundle: $OutputDirectory"
