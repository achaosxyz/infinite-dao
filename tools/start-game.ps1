param([switch]$NoBuild, [switch]$Verify, [switch]$VerifyModel)
. "$PSScriptRoot/godot-environment.ps1"
if ($VerifyModel -and -not $Verify) { throw '-VerifyModel also requires -Verify. It sends a real, potentially billable Anthropic request.' }
if (-not $NoBuild) {
    & $dotnet build "$project/InfiniteDao.Game.csproj" --configuration Debug --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Native game build failed.' }
    & $godot --headless --editor --path $project --import
    if ($LASTEXITCODE -ne 0) { throw 'Godot resource import failed.' }
}
$gameArgs = @('--path', $project)
if ($Verify) { $gameArgs += @('--', '--verify-native'); if ($VerifyModel) { $gameArgs += '--verify-model' } }
& $godot @gameArgs
if ($LASTEXITCODE -ne 0) { throw "Native game exited with code $LASTEXITCODE." }
