param([string]$OutputDirectory = '')

. "$PSScriptRoot/godot-environment.ps1"
$output = if ($OutputDirectory) { $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory) } else { Join-Path $root 'artifacts/releases/InfiniteDao-Godot-Windows' }
$template = Join-Path $root 'artifacts/godot-templates/templates/windows_release_x86_64.exe'
if (-not (Test-Path $template)) { throw 'Godot 4.7.2 .NET export templates are required in artifacts/godot-templates/templates/.' }
if (-not (Test-Path (Join-Path $project 'InfiniteDao.Game.sln'))) { throw 'Godot .NET export requires src/InfiniteDao.Game/InfiniteDao.Game.sln.' }
New-Item -ItemType Directory $output -Force | Out-Null
& $godot --headless --editor --path $project --import
if ($LASTEXITCODE -ne 0) { throw 'Godot resource import failed.' }
$exportLog = & $godot --headless --path $project --export-release 'Windows Desktop' (Join-Path $output 'InfiniteDao.exe') 2>&1
$exportCode = $LASTEXITCODE
$exportLog | ForEach-Object { Write-Host $_ }
if ($exportCode -ne 0 -or ($exportLog | Where-Object { "$_" -match '(^|\s)ERROR:' })) { throw 'Native Windows export failed; inspect the Godot export errors above.' }
New-Item -ItemType Directory (Join-Path $output 'config') -Force | Out-Null
Copy-Item 'config/llm.example.json' (Join-Path $output 'config/llm.example.json') -Force
if (-not (Test-Path (Join-Path $output 'config/llm.local.json'))) {
    Copy-Item 'config/llm.example.json' (Join-Path $output 'config/llm.local.json')
}
Copy-Item 'config/README.md' (Join-Path $output 'config/README.md') -Force
Copy-Item 'docs/PLAYING.md' (Join-Path $output 'PLAYING.md') -Force
Copy-Item 'LICENSE' (Join-Path $output 'LICENSE') -Force
$notices = Join-Path $output 'licenses'
New-Item -ItemType Directory -Path $notices -Force | Out-Null
Copy-Item 'docs/GODOT_LICENSE.txt', 'docs/GODOT_COPYRIGHT.txt' $notices -Force
Copy-Item (Join-Path $env:DOTNET_ROOT 'LICENSE.txt') (Join-Path $notices 'DOTNET_LICENSE.txt') -Force
Copy-Item (Join-Path $env:DOTNET_ROOT 'ThirdPartyNotices.txt') (Join-Path $notices 'DOTNET_THIRD_PARTY_NOTICES.txt') -Force
$launcher = "@echo off`r`ncd /d `"%~dp0`"`r`nstart `"`" `"%~dp0InfiniteDao.exe`"`r`n"
[IO.File]::WriteAllText((Join-Path $output 'Play.cmd'), $launcher, [Text.Encoding]::ASCII)
Write-Output "Portable Windows game: $output"
Write-Output 'Native Godot window, bundled .NET runtime and art. No browser or local web service. No workspace API credentials were copied.'
