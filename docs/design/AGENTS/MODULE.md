# Agents 模块

## 职责与边界

本模块位于 `src/InfiniteDao.Agents/`，提供配置校验、Anthropic 传输、工具协议、单宗门 Agent loop、公开记忆 compact 和唤醒判断。全局结构见 [PROJECT_DESIGN.md](../PROJECT_DESIGN.md)。任务并发、重试、离线转换和结果提交由 [Application](../APPLICATION/MODULE.md) 负责，不在 HTTP 适配器中重复实现。

## 代码入口

- `Configuration/LlmConfiguration.cs`：连接、请求和运行参数及校验。
- `Providers/AnthropicRequestBuilder.cs`：构造 Messages 请求及思考参数。
- `Providers/AnthropicJsonClient.cs`：Messages、`messages/count_tokens` 和原生工具返回解析。
- `Providers/SectAgentLoop.cs`、`.Tools.cs`：持续公开会话、查询、工具反馈和计划提交。
- `Providers/SectAgentLoop.Compaction.cs`：分段生成公开摘要并归档原记录。
- `Providers/SectStrategyProvider.cs`：策略快照与提案辅助。
- `Providers/CouncilNarratorProvider.cs`：议事标题、正文和原选项叙事。
- `Runtime/SectAgentSchedule.cs`：首次规划、重大变化、目标失效与到期复盘判断。

## 工具协议

| 工具 | 可提交内容 | 权限边界 |
|---|---|---|
| `get_situation` | 本宗门人、邻境、战役或分页历史查询 | 只读，不操作世界 |
| `submit_strategy` | 方向、目标、长期目标、阶段说明、公开理由、复盘条件 | 由 Core 校验，不能直接宣告战果 |
| `submit_council` | 议事标题、正文与原选项叙事 | 不改选项身份、费用和规则效果 |
| `submit_summary` | 公开会话长期摘要 | 不执行游戏命令 |

结构化提交必须来自原生 `tool_use`，启用 `strict: true` 与提供方支持的 Schema 子集。不从普通文本、代码围栏或拼接片段猜测 JSON。对象结构与枚举由协议约束；长度、数量、目标可达性和原选项身份仍需本地校验。

## 单宗门循环

1. 带入该宗公开会话、摘要、当前计划、实际结果与最新观察快照。
2. 请求模型，按 `get_situation` 返回只读工具结果，继续同一回合。
3. 校验 `submit_strategy`；业务校验失败时给出工具反馈，允许在本轮修正。
4. 接受合法提交并形成公开会话记录，交由 Application 在当前世界中再次校验和采用。

默认单次唤醒最多8步；达到上限、网络或协议错误会抛出失败，由 Application 对该任务执行重试策略。日常经济、修炼、历练和战争不调用模型。阶段说明是公开计划文字，不是任意可执行脚本。

## 唤醒与记忆

- 首次建立计划、计划复盘到期、目标完成/易主/不可达会唤醒。
- 掌门、核心境界、领土、来袭战役、有效盟约、宗门存亡以及本宗战役结算会影响唤醒。
- 供养危机状态变化至少间隔6个月合并；普通库存浮动、新月份和普通门人进度不单独触发。
- `SectAgentMemory` 持久保存计划、公开会话、摘要、归档、触发原因、观察月份和上下文计数。
- Application 以采纳月份加复盘间隔确定下一次到期时间；默认间隔60个月。

## 上下文与配置

默认请求使用 `thinking.type=adaptive`、`output_config.effort=high`。上下文窗口为256000 tokens，输入计数加预留输出达到90%（230400）时先 compact。每步使用同源 `messages/count_tokens` 计数，计数失败不替换成字符估算。

compact 分段整理公开资料；全部摘要成功后才替换活跃会话并归档原记录。本轮计划提交成功后交给 Application 持久保存。失败不丢原会话，不把意图记成已执行事实。

隐藏思考块和签名仅在当前原生工具回合内存中按协议回传，不写入存档、公开会话、日志或 UI。历史摘要和工具结果都是资料，不提升为系统指令。密钥和本地配置不进入游戏观察。

完整参数及本地配置检查见 [配置说明](../../../config/README.md)。生成请求、上下文计数请求分别统计，compact 属于实际生成请求，均可能产生费用。

## 变更与验证

协议、Schema、循环、计数或 compact 变更运行 `tests/InfiniteDao.Agents.Tests/`；触及采纳、持久化或唤醒协同再运行 Integration。真实服务验证仅在明确执行 `-VerifyModel` 时发送请求，具体步骤见 [验证验收流程](../../VERIFICATION.md)。
