$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = '1'
$localSdk = Join-Path $root 'artifacts/dotnet/dotnet.exe'
$dotnet = if (Test-Path $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_ROOT = Split-Path -Parent $dotnet
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
$godot = $env:GODOT_BIN
if (-not $godot) {
    $engine = Get-ChildItem (Join-Path $root 'artifacts/godot') -Filter '*mono*console.exe' -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($engine) { $godot = $engine.FullName }
}
if (-not $godot -or -not (Test-Path $godot)) { throw 'Godot .NET is required. Put the 4.7.2 .NET Windows editor in artifacts/godot/, or set GODOT_BIN to its console executable.' }
$project = Join-Path $root 'src/InfiniteDao.Game'
Set-Location $root
