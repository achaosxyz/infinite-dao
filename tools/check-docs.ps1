$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$failures = [System.Collections.Generic.List[string]]::new()
$known = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
function Relative([string]$path) { $path.Substring($root.Length + 1).Replace('\', '/') }

$rootFiles = @(Get-ChildItem -LiteralPath $root -File)
$docs = @(Get-ChildItem -LiteralPath (Join-Path $root 'docs') -File -Recurse)
$configFiles = @(Get-ChildItem -LiteralPath (Join-Path $root 'config') -File)
$toolFiles = @(Get-ChildItem -LiteralPath (Join-Path $root 'tools') -File)
$workflowFiles = @(Get-ChildItem -LiteralPath (Join-Path $root '.github/workflows') -File)
$assets = @(Get-ChildItem -LiteralPath (Join-Path $root 'src/InfiniteDao.Game/Assets') -File)
foreach ($file in @($rootFiles) + @($docs) + @($configFiles) + @($assets) + @($toolFiles) + @($workflowFiles)) {
    $null = $known.Add((Relative $file.FullName))
}
$sources = @('AGENTS.md', 'README.md', 'README_EN.md', 'config/README.md', 'src/InfiniteDao.Game/Assets/README.md') |
    ForEach-Object { Get-Item -LiteralPath (Join-Path $root $_) }
$sources = @($sources) + @($docs | Where-Object Extension -eq '.md')
$links = 0
foreach ($doc in $docs) {
    if ($doc.Extension -eq '.md' -and $doc.Name -cnotmatch '^[A-Z][A-Z0-9_]*\.md$') {
        $failures.Add('Invalid uppercase document name: ' + (Relative $doc.FullName))
    }
}
foreach ($file in $sources) {
    $text = [IO.File]::ReadAllText($file.FullName)
    foreach ($match in [regex]::Matches($text, '\]\((?<target>[^)\r\n]+)\)')) {
        $target = $match.Groups['target'].Value
        if ($target -match '^(https?://|mailto:|#)') { continue }
        $target = [Uri]::UnescapeDataString(($target -split '#', 2)[0])
        $full = [IO.Path]::GetFullPath((Join-Path $file.DirectoryName $target))
        $links++
        if (-not $full.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            $failures.Add('Out-of-workspace link: ' + (Relative $file.FullName) + ' -> ' + $target)
        } elseif (-not $known.Contains((Relative $full))) {
            $failures.Add('Missing or incorrectly cased target: ' + (Relative $file.FullName) + ' -> ' + $target)
        }
    }
}
$agent = [IO.File]::ReadAllText((Join-Path $root 'AGENTS.md'))
foreach ($heading in @('# Agent 开发约束', '## 项目简述', '## 核心逻辑', '## 模块说明', '## 项目目录说明', '## 唯一事实来源', '## 开发行为规则', '## 文档更新规则', '## 禁止行为', '## SOP')) {
    if (-not $agent.Contains($heading)) { $failures.Add('Missing AGENTS section: ' + $heading) }
}
if (-not $agent.Contains('@docs/design/PROJECT_DESIGN.md')) { $failures.Add('Missing core-design @ reference') }
if (-not $known.Contains('docs/design/PROJECT_DESIGN.md')) { $failures.Add('Missing canonical core design') }
$design = [IO.File]::ReadAllText((Join-Path $root 'docs/design/PROJECT_DESIGN.md'))
foreach ($heading in @('## 模块划分', '## 数据模型', '## 关键流程')) {
    if (-not $design.Contains($heading)) { $failures.Add('Missing core-design section: ' + $heading) }
}
foreach ($module in @('CORE','AGENTS','APPLICATION','INFRASTRUCTURE','GAME','SIMULATOR')) {
    if (-not $known.Contains('docs/design/' + $module + '/MODULE.md')) { $failures.Add('Missing module: ' + $module) }
}
if (Test-Path -LiteralPath (Join-Path $root 'docs/SOP.md')) { $failures.Add('Standalone SOP.md must not exist') }
foreach ($file in $sources) {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -match 'SOP\.md|sect-simulator\.md|agent-loop\.md|world-simulation[^\s]*\.md|character-and-loadout\.md|initial-world-map\.md|foundations\.md|architecture\.md|roadmap\.md') {
        $failures.Add('Removed-document reference: ' + (Relative $file.FullName))
    }
}
$scriptErrors = 0
foreach ($script in @($toolFiles | Where-Object Extension -eq '.ps1')) {
    $tokens = $null
    $parseErrors = $null
    $null = [System.Management.Automation.Language.Parser]::ParseFile($script.FullName, [ref]$tokens, [ref]$parseErrors)
    foreach ($error in $parseErrors) { $failures.Add($script.Name + ': ' + $error.Message); $scriptErrors++ }
}
$publisher = [IO.File]::ReadAllText((Join-Path $root 'tools/publish-game.ps1'))
if (-not $publisher.Contains("Copy-Item 'LICENSE'")) { $failures.Add('Publisher must copy project LICENSE') }
$license = [IO.File]::ReadAllText((Join-Path $root 'LICENSE'))
if (-not $license.StartsWith('MIT License') -or -not $license.Contains('Copyright (c) 2026 infinite-dao contributors')) { $failures.Add('MIT license header/attribution missing') }
$report = [ordered]@{ passed = ($failures.Count -eq 0); documents = $sources.Count; docsFiles = $docs.Count; localLinks = $links; modules = 6; standaloneSop = $false; scriptsParsed = ($scriptErrors -eq 0); failures = @($failures.ToArray()) }
$json = $report | ConvertTo-Json -Depth 5
$evidence = Join-Path $root 'artifacts/verification'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $evidence 'docs-check.json'), $json, [Text.UTF8Encoding]::new($false))
$json
if ($failures.Count -gt 0) { exit 1 }
