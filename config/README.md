# 本地 LLM 配置

请填写 `llm.local.json`，不要把密钥填入 `llm.example.json`。本地文件已被仓库根目录的 `*.local.json` 忽略规则覆盖；这不会加密文件，也不会阻止手动强制添加，请勿分享该文件。

首次初始化会提供一份空白本地文件。换机器时可自行从模板复制；不要用复制命令覆盖已经配置过的本地文件。

## 必填

| 参数 | 填写方式 |
|---|---|
| `endpoint` | 完整的 Anthropic Messages 请求地址，包括服务商要求的路径，例如 `https://api.anthropic.com/v1/messages`；使用代理时填写代理实际的 Messages 地址，程序不自动拼接路径 |
| `model` | 服务端实际接受的模型 ID，不是界面昵称 |
| `apiKey` | 可直接填本地文件；也可以留空并设置下述环境变量 |
| `apiKeyEnvironmentVariable` | 默认 `INFINITE_DAO_LLM_API_KEY`；该环境变量非空时优先于 `apiKey` |

默认 `authMode` 为 `x-api-key`，对应同名请求头；代理要求 Authorization 时可设为 `bearer`。本地无认证服务可设为 `none`，此时不能配置密钥，防止误以为密钥会被使用。远程地址要求 HTTPS；HTTP 仅允许明确的本机回环地址。地址中不放用户密码、查询参数或密钥。

## 请求参数

| 参数 | 默认 | 说明 |
|---|---|---|
| `protocol` | `anthropic-messages` | 使用 Anthropic 原生 Messages 协议，不发送 OpenAI Chat Completions 格式 |
| `anthropicVersion` | `2023-06-01` | `anthropic-version` 请求头；此处固定兼容协议版本，不声称它是最新版本，按服务端支持调整 |
| `request.timeoutSeconds` | 90 | 单次请求超时，1–600 秒 |
| `request.maxOutputTokens` | 4096 | 单次输出 token 上限；需确认模型支持的范围，不是费用总额上限 |
| `request.temperature` | `null` | 空值表示不发送；支持时可设 0–1 内的值，不强行要求所选模型支持温度 |
| `request.thinkingMode` | `adaptive` | 映射为 `thinking.type`；当前已配置服务的真实战略请求已接受此设置 |
| `request.reasoningEffort` | `high` | 映射为 `output_config.effort`，不是 temperature，也不是 OpenAI 的 reasoning_effort 字段 |

当前已按要求设置为 **high**。关闭思考时使用 `thinkingMode: "disabled"` 且将 `reasoningEffort` 设为 `null`；两者都为 `null` 时不发送相关字段。其他等级是否支持由所用服务决定，本地参数校验不等于供应商能力保证。思考也会占用输出预算，不应将 `maxOutputTokens` 误解为最终答案独占的 token 数。

换用其他 Anthropic 原生模型时需确认其 thinking / effort 支持；不提供手动 `budget_tokens` 配置。当前请求路径使用 adaptive + high，参数映射由自动化测试覆盖，服务可用性需单独进行明确授权的真实验证。

适配器使用 `max_tokens`、顶层 `system` 与 `messages`，只从原生 `tool_use` 块接收结构化提交；不保存或展示隐藏 thinking 块，不发送 OpenAI 的 `response_format` 或 `max_completion_tokens`。结构化意图通过 Core 校验后才参与结算，模型叙事不能直接成为世界事实。

## 自动调用与并发

| 参数 | 默认 | 说明 |
|---|---|---|
| `runtime.maxConcurrentRequests` | 10 | 最多同时运行10个宗门Agent循环；不限制Agent总数或总调用次数，超出并发数的已唤醒Agent排队执行 |
| `runtime.maxRequestsPerRun` | 已废弃 | 只兼容已有私密配置，值被忽略；不作为额度，也无续授权流程 |
| `runtime.failurePolicy` | `retry_then_rules` | 初次失败后自动重试3次（最多4轮），仍失败则明确切换离线并记录；兼容解析值`stop`也遵循同一运行策略 |

各宗门独立保存公开会话、长期计划及执行结果。只在首次议政、重大变化、计划失效或到期复盘时进入后台工具循环，平静月份复用计划；关键议事另调用主持工具。等待模型不阻塞时间：沿用当前合法战略，返回后在串行提交点验证当前目标，只接受仍有效的计划。议事先提供规则选项，后台叙事只更新仍待决的同一事件。加载或新建世界会取消并作废旧任务。`submit_strategy`、`submit_council`、`submit_summary` 均使用严格工具结构，不解析普通文本 JSON。失败按1/2/4秒退避重试3次，耗尽后显示离线提示并记入史书，时间继续流逝；不会自动恢复联网，需玩家手动切换。修炼、历练和战果仍由可复现规则执行，请求及重试可能持续计费。

`runtime.contextWindowTokens=256000`、`runtime.compactionThreshold=0.9`：每宗会话独立计数，输入计数加预留输出达到230400 tokens时 compact。需要提供方支持同源的 `messages/count_tokens`；计数失败走相同的重试与离线流程，不悄悄改用字符估算。摘要与原公开归档随存档保留，隐藏思考不落盘。`strategyReviewMonths=60` 是默认最迟复盘时间，重大事件可提前唤醒；`maxAgentSteps=8` 只限制单次唤醒的工具循环步数，不是全局调用额度。计数HTTP请求与模型生成请求分开统计。

常规构建、C# 测试、本地配置校验和离线原生验证不发真实请求。`tools/start-game.ps1 -Verify -VerifyModel` 主动验证真实叙事工具、五宗工具循环、持久会话与平静月零调用；生成与计数的实际请求数由工具步骤、压缩和重试决定，可能计费。使用隔离存档，不替换玩家世界；浏览器服务脚本不能作为原生验收证据。

## 本地校验

在仓库根目录执行（需 .NET 10 SDK）：

```powershell
dotnet run --project src/InfiniteDao.Simulator -- check-config --config config/llm.local.json
```

校验只报告字段名与错误原因，不输出密钥、请求地址、模型返回或原始配置。成功仅说明本地格式和必填项通过，不能证明鉴权、模型能力和服务可用性。

无需将密钥发到对话里。原生游戏界面和导出存档不显示密钥，配置不打入 PCK 或程序集。便携包不自动携带本工作区的密钥；独立运行时请在便携版的本地配置中填写。根目录启动入口通过 `--data-root` 明确沿用工作区配置，SDK 源码启动也使用工作区配置。
