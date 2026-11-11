# 文档索引

本目录只记录当前游戏与开发流程。项目结构和设计的唯一事实来源（SSOT）位于 `design/`，核心入口是 [PROJECT_DESIGN.md](design/PROJECT_DESIGN.md)。

## 玩家入口

- [中文 README](../README.md) / [English README](../README_EN.md)：游戏介绍、启动、费用与反馈。
- [PLAYING.md](PLAYING.md)：战略、时间、门人、战争、流亡、存档与快捷键。
- [配置说明](../config/README.md)：可选 Anthropic 接入、参数和本地检查。
- [LICENSE](../LICENSE)：项目 MIT 许可证。

## 开发与 Agent 入口

```text
AGENTS.md
  → docs/design/PROJECT_DESIGN.md
    → docs/design/<相关模块>/MODULE.md
```

先读 [Agent 开发约束](../AGENTS.md) 和核心设计，再按任务选择相关模块；只在涉及直接依赖时增加阅读范围。索引不要求把所有模块全文加入上下文。

| 文档 | 职责 |
|---|---|
| [PROJECT_DESIGN.md](design/PROJECT_DESIGN.md) | 当前产品边界、模块划分、数据模型、关键流程与不变量 |
| [STRUCTURE.md](STRUCTURE.md) | 真实目录职责、文档放置和大写命名规范 |
| [DEVELOPMENT.md](DEVELOPMENT.md) | 实际开发变更流程：定位、环境、同步、实现与提交 |
| [RELEASE.md](RELEASE.md) | 实际发布流程：隔离导出、干净打包、EXE 验收与 GitHub Windows 发布 |
| [VERIFICATION.md](VERIFICATION.md) | 当前验收入口、证据位置和验证边界 |

## 模块文档

| 修改对象 | 按需阅读 |
|---|---|
| 规则、地图、经营、人物、历练、战役 | [CORE/MODULE.md](design/CORE/MODULE.md) |
| Anthropic、工具、上下文、摘要与唤醒 | [AGENTS/MODULE.md](design/AGENTS/MODULE.md) |
| 会话、后台任务、提交、重试与离线 | [APPLICATION/MODULE.md](design/APPLICATION/MODULE.md) |
| 保存、导入校验与存档列表 | [INFRASTRUCTURE/MODULE.md](design/INFRASTRUCTURE/MODULE.md) |
| Godot、视图、控件、时间与地图输入 | [GAME/MODULE.md](design/GAME/MODULE.md) |
| 人物画法、宗徽、HUD 配色和美术生产验收 | [GAME/ART_STYLE.md](design/GAME/ART_STYLE.md) |
| 配置检查命令行 | [SIMULATOR/MODULE.md](design/SIMULATOR/MODULE.md) |

## 维护规则

文档文件名采用大写下划线形式，扩展名为 `.md`；固定入口保留 `docs/design/PROJECT_DESIGN.md`。结构或逻辑变化必须同步核心设计，模块细节同步对应 `MODULE.md`；摘要变化同步 `AGENTS.md`。SOP 入口只在 `AGENTS.md` 简述并索引实际流程，流程文档、索引和 README 不定义第二套架构，不保留历史草案、路线图或 TODO。
