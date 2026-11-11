# Simulator 模块

## 职责与边界

本模块位于 `src/InfiniteDao.Simulator/`，是本地 LLM 配置检查命令行程序，不是游戏客户端，也不提供命令行世界演化流程。全局结构见 [PROJECT_DESIGN.md](../PROJECT_DESIGN.md)。

## 代码入口

- `Program.cs`：程序入口。
- `SimulatorCli.cs`：参数、退出码与输出。
- `Configuration/LlmConfigurationLoader.cs`：配置文件读取与解析错误转换。
- 具体字段校验调用 Agents 的 `LlmConfiguration.Validate`；项目工程引用 Core、Agents 和 Infrastructure。

## 当前命令

```powershell
dotnet run --project src/InfiniteDao.Simulator -- check-config --config config/llm.local.json
```

省略 `--config` 时检查工作目录的 `config/llm.local.json`，`--help` 显示已支持的命令。成功退出码0，配置无效或不可读为2，参数错误为64。

## 关键流程

1. 检查命令和参数，只接受已实现的配置检查入口。
2. 读取 JSON，结合环境变量调用配置校验。
3. 输出字段级结果与退出码；不输出密钥、完整配置或供应商原始响应。

检查不发送网络请求。成功只说明本地格式与必填项有效，不能据此宣称模型连接可用。使用方式见 [配置说明](../../../config/README.md)。

## 变更与验证

修改参数、输出或退出码时核对 Integration 中的 CLI 与配置加载测试。不得在 README 或设计中把尚未实现的 CLI 命令写成可用接口，也不得用配置检查代替真实服务验收。开发流程见 [开发变更流程](../../DEVELOPMENT.md)。
