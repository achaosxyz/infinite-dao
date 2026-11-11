$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$root = Split-Path -Parent $PSScriptRoot
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'src/InfiniteDao.Game/InfiniteDao.Game.csproj') -Raw
if ($project.Project.Sdk -notmatch '^Godot\.NET\.Sdk/(?<version>\d+\.\d+\.\d+)$') { throw 'Expected a pinned stable Godot .NET SDK.' }
$version = $Matches['version']
$downloads = Join-Path $root 'artifacts/downloads'
$engineDirectory = Join-Path $root 'artifacts/godot'
$templates = Join-Path $root 'artifacts/godot-templates/templates'
New-Item -ItemType Directory -Path $downloads, $engineDirectory, $templates -Force | Out-Null
$base = "https://github.com/godotengine/godot/releases/download/$version-stable"

function Download([string]$name) {
    $path = Join-Path $downloads $name
    if (-not (Test-Path -LiteralPath $path)) {
        & curl.exe --fail --location --retry 3 --output "$path.partial" "$base/$name"
        if ($LASTEXITCODE -ne 0) { throw "Godot download failed: $name" }
        Move-Item -LiteralPath "$path.partial" -Destination $path
    }
    return $path
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$engine = Get-ChildItem -LiteralPath $engineDirectory -Filter "Godot_v$version-stable_mono_win64_console.exe" -Recurse -File | Select-Object -First 1
if (-not $engine) {
    $archive = Download "Godot_v$version-stable_mono_win64.zip"
    [IO.Compression.ZipFile]::ExtractToDirectory($archive, $engineDirectory)
    $engine = Get-ChildItem -LiteralPath $engineDirectory -Filter "Godot_v$version-stable_mono_win64_console.exe" -Recurse -File | Select-Object -First 1
}
if (-not $engine) { throw 'The Godot .NET console executable was not extracted.' }
$engineVersion = & $engine.FullName --version
if ($LASTEXITCODE -ne 0 -or "$engineVersion" -notlike "$version.stable.mono.*") { throw 'The Godot engine does not match the project SDK.' }

$names = @('windows_release_x86_64.exe', 'windows_debug_x86_64.exe')
$missing = @($names | Where-Object { -not (Test-Path -LiteralPath (Join-Path $templates $_)) })
if ($missing.Count -gt 0) {
    $archive = Download "Godot_v$version-stable_mono_export_templates.tpz"
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        foreach ($name in $missing) {
            $entry = $zip.GetEntry("templates/$name")
            if (-not $entry) { throw "Missing Godot .NET Windows template: $name" }
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $templates $name), $false)
        }
    } finally { $zip.Dispose() }
}
$env:GODOT_BIN = $engine.FullName
if ($env:GITHUB_ENV) { Add-Content -LiteralPath $env:GITHUB_ENV -Value "GODOT_BIN=$($engine.FullName)" -Encoding utf8 }
Write-Output "Godot .NET $version is ready: $($engine.FullName)"
