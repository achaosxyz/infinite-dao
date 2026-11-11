param([string]$OutputDirectory = '')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'artifacts/distribution' }
$OutputDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
$archive = Join-Path $OutputDirectory 'InfiniteDao-Windows-x64.zip'
$checksum = "$archive.sha256"
if ((Test-Path -LiteralPath $archive) -or (Test-Path -LiteralPath $checksum)) {
    throw 'Distribution files already exist. Choose a new OutputDirectory; existing releases are not overwritten.'
}
$stage = Join-Path $root ('artifacts/releases/package-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
try {
    $bundle = Join-Path $stage 'InfiniteDao-Godot-Windows'
    & "$PSScriptRoot/publish-game.ps1" -OutputDirectory $bundle
    # This fresh export owns this placeholder; no existing player directory is touched.
    Remove-Item -LiteralPath (Join-Path $bundle 'config/llm.local.json')
    foreach ($file in @('InfiniteDao.exe', 'InfiniteDao.pck', 'Play.cmd', 'LICENSE', 'PLAYING.md', 'config/llm.example.json', 'licenses/GODOT_LICENSE.txt', 'licenses/GODOT_COPYRIGHT.txt', 'licenses/DOTNET_LICENSE.txt', 'licenses/DOTNET_THIRD_PARTY_NOTICES.txt')) {
        if (-not (Test-Path -LiteralPath (Join-Path $bundle $file) -PathType Leaf)) { throw "Missing portable file: $file" }
    }
    if (-not (Get-ChildItem -LiteralPath $bundle -Filter 'coreclr.dll' -Recurse -File)) { throw 'The self-contained .NET runtime is missing.' }
    if (Test-Path -LiteralPath (Join-Path $bundle 'artifacts')) { throw 'Player data or verification output must not enter the public package.' }

    $process = [Diagnostics.Process]::new()
    $process.StartInfo.FileName = Join-Path $bundle 'InfiniteDao.exe'
    $process.StartInfo.Arguments = '--headless --quit-after 5'
    $process.StartInfo.WorkingDirectory = $bundle
    $process.StartInfo.UseShellExecute = $false
    $process.StartInfo.CreateNoWindow = $true
    $process.StartInfo.RedirectStandardOutput = $true
    $process.StartInfo.RedirectStandardError = $true
    try {
        $null = $process.Start()
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $exited = $process.WaitForExit(60000)
        if (-not $exited) {
            $process.Kill()
        }
        $process.WaitForExit()
        $log = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
        if (-not $exited -or $process.ExitCode -ne 0 -or $log -match '(?im)ERROR:|Native startup failed:|Unhandled exception') {
            Write-Host $log
            if (-not $exited) { throw 'Packaged startup timed out.' }
            throw "Packaged headless startup failed (exit $($process.ExitCode))."
        }
    } finally { $process.Dispose() }
    # The smoke test is startup-only; native UI acceptance remains a separate check.
    if (Test-Path -LiteralPath (Join-Path $bundle 'artifacts')) { throw 'Startup unexpectedly wrote player data into the package.' }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $temporaryArchive = Join-Path $stage 'package.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($bundle, $temporaryArchive, [IO.Compression.CompressionLevel]::Optimal, $true)
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    Move-Item -LiteralPath $temporaryArchive -Destination $archive
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText($checksum, "$hash  $([IO.Path]::GetFileName($archive))`n", [Text.Encoding]::ASCII)
    Write-Output "Windows archive: $archive"
    Write-Output "SHA256: $checksum"
} finally {
    Remove-Item -LiteralPath $stage -Recurse -Force
}
