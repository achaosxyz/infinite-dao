# 目录说明与命名规范

本文件说明当前目录及文档放置规则，不定义新模块。模块职责和依赖以 [PROJECT_DESIGN.md](design/PROJECT_DESIGN.md) 为准。

## 当前主要目录

| 路径 | 职责 |
|---|---|
| `src/InfiniteDao.Core/Game/` | 权威世界、人物、事件、经营和战斗规则 |
| `src/InfiniteDao.Agents/` | LLM 配置、HTTP 与工具适配、循环、计数及记忆 |
| `src/InfiniteDao.Application/` | 游戏会话、提交、后台任务与失败处理 |
| `src/InfiniteDao.Infrastructure/Persistence/` | JSON 保存、导入验证和摘要 |
| `src/InfiniteDao.Game/` | Godot 工程、场景、界面、地图和原生验收 |
| `src/InfiniteDao.Game/Assets/` | 游戏实际使用的图片、图标与资源说明 |
| `src/InfiniteDao.Simulator/` | 本地配置检查 CLI |
| `tests/InfiniteDao.Core.Tests/` | 规则、确定性和长线回归 |
| `tests/InfiniteDao.Agents.Tests/` | 配置、协议、工具、计数和 compact 测试 |
| `tests/InfiniteDao.IntegrationTests/` | 会话、存档、CLI 与异步任务集成测试 |
| `config/` | 公共配置示例、说明和不入库的本地私密配置 |
| `tools/` | 固定版本环境安装、启动、导出、干净打包、人物美术资源处理、文档检查及辅助验证脚本 |
| `.github/workflows/` | develop/main 的 CI 检查和 main 的 Windows 连续构建发布 |
| `docs/` | 玩家指南、索引、目录规范、开发/验证/发布流程及固定版本 Godot 许可文本 |
| `docs/design/` | 当前设计 SSOT 与六个模块的 `MODULE.md` |
| `artifacts/` | 本地 SDK、引擎、模板、导出包、存档和验证产物，不是源码 |
| `content/` | 当前仅包含占位文件，默认世界由 Core 生成，不从此处加载内容 |

`src/InfiniteDao.LegacyWeb/`、`tests/browser/` 和根目录 npm/Playwright 配置存在，但不属于默认原生解决方案或游戏启动链。其他空目录不是已实现模块，不据此推导服务、数据模型或扩展层。

## 根目录文件

- `README.md`、`README_EN.md`：面向玩家的中文与英文入口。
- `AGENTS.md`：项目上下文摘要与 Agent 行为约束。
- `LICENSE`：MIT 许可证；发布时随项目内容分发。
- `InfiniteDao.slnx`：当前六个源工程、三个测试工程的默认解决方案。
- `global.json`、`Directory.Build.props`：SDK、目标框架、空值检查与构建约束。
- `启动游戏.cmd`：原生客户端启动入口。

## 文档布局

```text
AGENTS.md
README.md
README_EN.md
LICENSE
docs/
  README.md
  STRUCTURE.md
  DEVELOPMENT.md
  RELEASE.md
  PLAYING.md
  VERIFICATION.md
  design/
    PROJECT_DESIGN.md
    CORE/MODULE.md
    AGENTS/MODULE.md
    APPLICATION/MODULE.md
    INFRASTRUCTURE/MODULE.md
    GAME/MODULE.md
    SIMULATOR/MODULE.md
```

这是现有文档的位置说明，不是目录扩张计划。源码、配置和其他资源的大小写沿用实际工程约定。

## 命名与内容边界

- Markdown 文件名使用 `UPPER_SNAKE_CASE.md`，例如 `PROJECT_DESIGN.md`、`MODULE.md`；不混用小写连字符名称。
- `docs/`、`docs/design/` 是固定目录入口；模块文档子目录采用与真实模块对应的大写标识。
- 核心设计写全局模块、数据和流程；模块文档只细化各自边界，不复制另一套全局设计。
- 玩家操作写 `PLAYING.md`，开发写 `DEVELOPMENT.md`，验收写 `VERIFICATION.md`，发布写 `RELEASE.md`，索引写 `README.md`；SOP 的一句说明与流程索引保留在 `AGENTS.md`。
- 文档移动、重命名或删除时，同步修改链接、`@` 引用、README 和脚本中的文档路径。
- 不把草案、未实现功能、历史路线或 TODO 放入当前设计文档；不把生成目录当成需提交的项目结构。
