# 无限修仙 · 宗门纪

**中文** · [English](README_EN.md)

[![CI main](https://github.com/achaosxyz/infinite-dao/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/achaosxyz/infinite-dao/actions/workflows/ci.yml?query=branch%3Amain)
[![CI develop](https://github.com/achaosxyz/infinite-dao/actions/workflows/ci.yml/badge.svg?branch=develop)](https://github.com/achaosxyz/infinite-dao/actions/workflows/ci.yml?query=branch%3Adevelop)
[![Windows release](https://github.com/achaosxyz/infinite-dao/actions/workflows/release.yml/badge.svg)](https://github.com/achaosxyz/infinite-dao/actions/workflows/release.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

选一宗门，定一条发展之道，看门徒成长、山河易主与道统兴衰。这是一款 Windows x64 原生单机修仙宗门模拟游戏：你决定战略和关键选择，执事负责日常经营；即使山门被灭，也能带幸存门徒在原世界重建。

![青岚域山河图](src/InfiniteDao.Game/Assets/qinglan-world.png)

## 游戏特色

- **十宗开局、十二府192地域**：选择不同宗门、难度与世界种子，争夺接壤灵地。
- **只定战略，不做重复日课**：经营、招募、修炼、历练与战争筹备自动执行，关键事件由你裁定。
- **门人有真实人生**：重点培养、实际随行护道人、突破、伤势、寿尽与战死共同影响宗门。
- **统一国风 HUD 与50张人物立绘**：宗门用宗徽识别，人物创建时选定立绘，掌门继任保留自己的容貌；支持原六宗旧档迁移。
- **多年战役与境界差**：高境修士可同轮压制多名低境敌人；补给、护阵、军阵、法宝与伤势仍可改变结果，不只比总战力。
- **灭门后仍可继续**：幸存门徒携道统流亡，择地重建；终极目标是消灭其余宗门道统。
- **随时暂停观察**：查看地图、宗门、人物、外交、道藏与史书，支持本地自动和手动存档。
- **可选智能世界**：接入 Anthropic 兼容接口，让各敌宗以独立公开记忆和长期计划在后台决策。

游戏界面目前为中文。完整离线游玩不需要模型、浏览器、Node.js 或游戏服务器。

## 开始游玩

### Windows 便携版

从 [GitHub Releases](https://github.com/achaosxyz/infinite-dao/releases) 下载 Windows x64 ZIP 并完整解压。自动构建以构建号发布预发行包，同时提供 SHA256 校验文件；EXE 尚未代码签名。

使用完整的 `InfiniteDao-Godot-Windows` 目录，运行 `InfiniteDao.exe` 或 `Play.cmd`。便携目录包含原生引擎、.NET 运行时和美术，**不要只复制 EXE**。

从本仓库导出的便携目录位于：

```text
artifacts/releases/InfiniteDao-Godot-Windows/
```

### 从源码运行

准备项目指定的 .NET SDK、Godot .NET 引擎后，双击根目录 [启动游戏.cmd](启动游戏.cmd)，或运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/start-game.ps1
```

根目录启动器优先使用已有原生导出包，并沿用工作区配置与存档；没有导出包时从源码启动。环境和导出步骤见 [开发变更流程](docs/DEVELOPMENT.md)。

## 怎么玩

1. 选择宗门、难度、种子与离线/智能模式，完成开宗议事。
2. 选择长期战略，按需要重点培养门人、指定更高境护道人。
3. 世界自动演进，遇到议事后暂停，作出选择再继续。
4. 随时按空格或暂停按钮查看局势；失焦会暂停，单纯切换资料页不会暂停。
5. 保持供养、积累人才、打赢多年战争；若流亡，处理薪火议事并选择落脚地继续。

详细操作见 [游玩指南](docs/PLAYING.md)。

## 可选：接入大模型

按 [配置说明](config/README.md) 配置游戏数据根目录下的 `config/llm.local.json`，不要将密钥提交到仓库或分享给他人。默认配置使用 HIGH/adaptive、256000-token 上下文和90% compact 阈值。

Agent 按事件和复盘需要调用，**不是每月都调用**；默认最多10个同时运行，不限制累计调用次数。等待时沿用现有战略，时间不因模型变慢而停止。初次失败后自动重试3次，仍失败则明确转为离线并记入史书，可在设置中手动恢复智能世界。

**真实请求、上下文计数、compact、超时和重试均可能产生服务费用。** 离线模式仍能完整游玩；配置模板不包含你的私密密钥。

## 存档与数据

- 工作区启动使用根目录配置，存档位于 `artifacts/game/saves/`。
- 便携版独立运行使用其自身目录中的配置与同一路径下的存档。
- 自动档之外有三个人工位置，并支持 JSON 导入/导出；移动游戏前请自行备份存档。
- 关闭原生窗口即可退出，无需另行关闭浏览器或后台游戏服务。

## 文档与参与

- [文档索引](docs/README.md)：玩家、开发者与 Agent 的分类入口。
- [核心设计](docs/design/PROJECT_DESIGN.md)：当前模块、数据模型与关键流程。
- [开发变更流程](docs/DEVELOPMENT.md) / [验证边界](docs/VERIFICATION.md)：开发、测试与原生验收。
- [发布流程](docs/RELEASE.md)：`develop` 开发、`main` 合并和通过 CI 后的 Windows 自动发布。
- [AGENTS.md](AGENTS.md)：AI Agent 的渐进式开发约束，不是玩家手册。

反馈问题时请附系统环境、操作步骤及不含密钥的相关日志；不要公开完整私密配置或供应商原始响应。提交代码或文档前，请先阅读开发变更流程 并同步受影响的中英文说明。

## 许可证

本项目采用 [MIT License](LICENSE)，版权署名为 `infinite-dao contributors`。第三方引擎、运行时与依赖保留各自的许可证。
