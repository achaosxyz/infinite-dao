# 开发变更流程

## 适用入口

本流程处理代码、设计或文档变更，不替代模块设计。先读 [AGENTS.md](../AGENTS.md)，再读 [PROJECT_DESIGN.md](design/PROJECT_DESIGN.md) 和本次涉及的 `MODULE.md`；文档按需加载，不全量加入上下文。

## 1. 定位真实实现

1. 从核心设计的模块表定位代码入口，通过符号和相关测试核实当前行为。
2. 检查现有改动，保留不属于本次任务的用户工作；不为定位而扫描生成目录。
3. 若代码与设计不一致，先按真实实现和可验证行为修正文档，再调整代码。

## 2. 同步设计与实现

| 变更内容 | 同步文档 |
|---|---|
| 模块、职责、结构 | 核心设计“模块划分”、相关 `MODULE.md`、目录说明与索引 |
| 实体、字段关系、持久化 | 核心设计“数据模型”及对应模块 |
| 命令、月结、提交和任务时序 | 核心设计“关键流程”及对应模块 |
| 可见玩法、配置参数或默认值 | 玩家指南、中英文 README、配置说明及公共示例中的受影响部分 |
| 项目上下文摘要 | `AGENTS.md` 的项目简述、核心逻辑、模块或目录说明 |

保持最小改动，不新增无需求的模块、依赖或通用封装。设计和代码在同次交付中一致，最终文档只记录已实现状态，不留下历史、规划或 TODO。

## 3. 准备环境与启动

- Windows x64，Godot 4.7.2 .NET，SDK 版本遵守根目录 `global.json`。
- `tools/godot-environment.ps1` 优先使用 `artifacts/dotnet/dotnet.exe`，否则选择 PATH 中的 `dotnet`。
- 使用 `GODOT_BIN` 指定 Godot .NET 控制台可执行文件，或放在 `artifacts/godot/` 供脚本定位。
- `tools/install-godot.ps1` 从官方固定版本下载 .NET 引擎及 Windows 模板；版本取自 Game 工程 SDK，不安装 .NET SDK，不读取私密模型配置。

在仓库根目录运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/start-game.ps1
```

玩家入口和数据路径见 [PLAYING.md](PLAYING.md)。不通过 npm 或浏览器服务启动原生游戏。

## 4. 处理配置

按 [配置说明](../config/README.md) 使用公共示例建立本地配置，不覆盖已有密钥或回显私密内容。仅检查配置时运行：

```powershell
dotnet run --project src/InfiniteDao.Simulator -- check-config --config config/llm.local.json
```

本地 SDK 可用 `artifacts/dotnet/dotnet.exe` 替换 `dotnet`。该命令不联网，成功不能证明提供方在线。常规开发不得把密钥、玩家存档或隐藏思考放入提交、截图和日志。

## 5. 验证与检查

按影响范围执行 [验证验收流程](VERIFICATION.md)，发布按 [原生发布流程](RELEASE.md)。仅文档改动核对命名、相对链接、`@` 引用、设计一致性和涉及的脚本语法，不重复运行游戏或付费请求。

检查 `docs/` Markdown 文件名符合大写下划线约定；移动或删除文档时更新索引、README 和脚本路径。核心设计与 Agent 摘要一致，中英文 README 的能力、费用和 MIT 许可一致。目录放置规则见 [STRUCTURE.md](STRUCTURE.md)。

## 6. 提交与交付

仅在有明确提交授权时创建 Git commit；消息始终中文，带 Conventional Commits 类型，如 `docs: 统一设计与开发约束`。在 Git 工作区检查实际差异和空白错误，不提交生成物、私密配置或玩家数据。

开发使用 `develop`，修复复查后合并 `main`。两分支的 push 和以两分支为目标的 PR 都触发 [CI](../.github/workflows/ci.yml)，提交页面显示对应检查状态；绿勾表示检查通过，不是提交签名。合并 `main` 会在其 CI 成功后自动触发公开 Windows 预发行，执行前须有相应推送与发布授权。发布的触发、权限和产物见 [发布流程](RELEASE.md)。

合并不要求 PR：等待 `develop` 的提交检查成功后，在本地将其合入 `main` 并推送即可。仅在用户明确要求时创建 PR；CI 对 PR 的支持不代表开发流程强制使用 PR。

交付只陈述实际变更、验证、限制和未完成事项；未运行的测试或联网验证明确说明，不把计划当作结果。
