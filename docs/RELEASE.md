# 原生发布流程

## 1. 确认输入

阅读 [Game 模块](design/GAME/MODULE.md) 与 [验证验收流程](VERIFICATION.md)，确认交付范围和现有改动。本文说明本地导出与 GitHub 自动发布，不自行授予提交、推送、合并或外部发布权限。`main` 合并会在 CI 成功后自动公开 Windows 包，执行前必须确认已有对应授权。

环境与源码启动见 [开发变更流程](DEVELOPMENT.md)。导出需要 Godot 4.7.2 .NET 及匹配模板：

```text
artifacts/godot-templates/templates/windows_release_x86_64.exe
```

引擎通过 `GODOT_BIN` 或 `artifacts/godot/` 定位。`tools/install-godot.ps1` 按 Game 工程中的固定 SDK 版本从 Godot 官方下载引擎与 Windows 模板；.NET SDK 仍遵守 `global.json`。模板、SDK 与缓存属于本地工具，不作为源代码提交。

## 2. 执行导出

在仓库根目录运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/publish-game.ps1
```

脚本先构建 Debug 编辑器程序集，再导入资源并执行 Windows ExportRelease，输出到 `artifacts/releases/InfiniteDao-Godot-Windows/`。编辑器程序集只用于工具链，便携包仍是发布版本；首次构建不能依赖已有 `.godot/mono/temp/bin/Debug/` 缓存。各阶段和导出日志实时输出；检查退出码和 Godot ERROR，不能因为出现 EXE 就宣布成功。

## 3. 生成公开 ZIP

本地导出目录可能已包含玩家配置、存档和验证产物，不能直接压缩上传。发布使用独立新目录重新导出，不改动现有便携目录：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/package-game.ps1
```

脚本检查必需文件、自包含 .NET 运行时以及导出 EXE 的 headless 启动，再输出：

```text
artifacts/distribution/InfiniteDao-Windows-x64.zip
artifacts/distribution/InfiniteDao-Windows-x64.zip.sha256
```

ZIP 内包含完整的 `InfiniteDao-Godot-Windows/` 目录。脚本移除新建的空 `llm.local.json`，只保留公共示例；不读取工作区私密配置，不复制旧导出目录。不覆盖已有 ZIP 或校验文件；重做本地包时用 `-OutputDirectory` 指定新的产物目录。headless 只检查引擎、程序集和资源可启动，不等于真实窗口验收。

## 4. 检查便携内容

- 保留整个输出目录，包括 EXE、PCK、.NET 运行时、美术和 `Play.cmd`；不能只分发 EXE。
- 带上 `PLAYING.md`、配置说明、公共配置示例和根目录 MIT `LICENSE`。
- `licenses/` 携带 Godot 4.7.2 官方许可与第三方版权文本，以及实际使用的 .NET SDK 分发中的许可和第三方声明；不能用项目 MIT 文件替代引擎和运行时声明。
- 脚本不复制工作区 `llm.local.json`；已有便携本地配置不得被公共示例覆盖。
- 不复制玩家存档、工作区密钥、隐藏思考或供应商响应；第三方组件保留各自的许可要求。
- 源码分发包含中文、英文 README、设计与模块文档，链接和大写路径与实际文件一致。

## 5. 验证本地导出 EXE

代码或资源变更后，按影响范围运行本地导出版本；本节不是发布后下载包的默认复测步骤：

```powershell
& artifacts/releases/InfiniteDao-Godot-Windows/InfiniteDao.exe -- --verify-native
```

确认真实窗口、正常退出及隔离报告：

```text
artifacts/releases/InfiniteDao-Godot-Windows/artifacts/verification/native/report.json
```

报告必须为 `passed: true`。检查地图、菜单打开/选择/失焦、存档恢复、培养护道、战争、流亡与终局夹具。源码窗口通过不代替导出包验证，构造终局也不等于标准开局自然通关。

文档或许可证单独变化时，核对复制路径、脚本语法和实际交付文件，不为此重新构建游戏或联网。真实模型验证与费用授权单独按验证流程执行。

## 6. GitHub Actions 发布

1. 在 `develop` 完成实现、review/fix/review，推送后等待 [CI](../.github/workflows/ci.yml) 成功；有修复则再 review 一轮。
2. 默认直接合并到 `main` 并推送，不要求创建 PR。CI 在 Windows 执行文档/PowerShell 检查、全量离线测试和原生客户端编译，检查状态关联对应 commit。
3. [Windows release](../.github/workflows/release.yml) 只接受本仓库 `main` push CI 的成功结果，检出该次通过检查的确切 SHA，不使用可能已前进的分支头。PR、fork、develop 和失败 CI 不发布。
4. Windows 只读构建任务安装固定版本工具，调用干净打包脚本，上传 ZIP 与 SHA256；不配置模型密钥，不发送真实模型请求。
5. 独立的最小写权限任务校验传递产物，先创建带完整附件的草稿，再公开 GitHub 预发行。标签为 `windows-运行号-重试号`，避免捏造正式语义版本或覆盖旧包。
6. 失败时检查对应 Actions 日志。可在 `main` 手动运行 Windows release，但该 SHA 必须已有成功的 main push CI；重跑使用新的重试号，旧发布不删除、不覆盖。
7. 从对应公开 Release 下载 ZIP 和 `.sha256` 文件，使用 `Get-FileHash -Algorithm SHA256` 计算下载 ZIP 的哈希并与公开校验值比较；不一致时不能宣布校验通过。默认不解压启动下载包、不重复执行窗口或全量游戏测试，只有用户明确要求时才做下载包运行验收；本地导出和 CI 的既有验证照常执行。

所有使用的 Actions 固定到完整 commit SHA。构建不需要私密仓库 secret；发布使用任务级 `GITHUB_TOKEN`，仅发布任务拥有 `contents: write`。不自动改分支保护、账号权限或代码签名设置。

在 [Releases](https://github.com/achaosxyz/infinite-dao/releases) 检查公开状态、源提交、ZIP 与 SHA256 附件，在两分支 commit 页面确认 CI 绿勾。Windows 包尚未代码签名，来源和校验文件应同时核对。

## 7. 交付说明

说明实际输出目录、验证结果和必要限制。当前原生客户端不需要浏览器或后台游戏服务；独立包使用自己的配置和存档。若无单独外部发布授权，停在本地交付，不上传或部署。
