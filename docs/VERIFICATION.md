# 验证范围与证据

## 验收原则

当前客户端为 Godot .NET 原生 Windows 游戏。规则、HTTP 测试桩、真实窗口、构造夹具和真实提供方验证各有边界，不相互冒充。标准世界为192地域；终局夹具不能证明未修改开局可以在相同时长自然通关。

设计与不变量见 [PROJECT_DESIGN.md](design/PROJECT_DESIGN.md)，开发和导出分别见 [开发变更流程](DEVELOPMENT.md)、[原生发布流程](RELEASE.md)。本文是验证验收的实际流程与范围说明，不记录修复历史或路线。

## 执行流程

1. 从核心设计和相关模块确定受影响入口、数据及不变量，选择相应测试套件。
2. 规则运行 Core，协议运行 Agents，会话/存档/并发运行 Integration；跨模块核心变更补充全量测试。
3. 界面、输入、时钟和原生资源变更使用真实窗口；菜单必须打开、选择、关闭并覆盖失焦，不能只发选中信号。
4. 需要验证真实模型时先确认明确的联网和费用授权，使用隔离存档；常规检查不联网。
5. 检查退出码、报告、日志和测试数量，区分测试桩、真实窗口与真实服务证据；导出包按发布流程另验实际 EXE。
6. 同步受影响的覆盖说明，交付时明确未执行项目和限制，不把自然通关、满窗压测或服务可用性当作已验证。

仅文档或许可证变化时，检查名称、链接、`@` 引用、设计摘要和相关脚本语法；不为此重跑游戏、全量测试或付费验证。

## 自动化覆盖

默认命令：

```powershell
dotnet test InfiniteDao.slnx --configuration Release --nologo -v minimal
```

使用仓库本地 SDK 时，以 `artifacts/dotnet/dotnet.exe` 替换 `dotnet`。当前完整代码验收报告为102项通过，无失败或跳过，输出位于 `artifacts/verification/current-tests.log`。

| 套件 | 通过数 | 当前主要覆盖 |
|---|---:|---|
| Core | 39 | 6000月标准世界、玩家自动扩张、资源、历练生死、护道、军需、逐人战斗、流亡重建与统一 |
| Agents | 38 | 配置、原生工具循环、非法响应、公开记忆、上下文计数、90% compact 阈值与唤醒 |
| Integration | 25 | CLI、保存恢复、慢模型下推进、Pump 不增加回合版本、独立重试、耗尽离线、重载隔离、失效目标、并发上限和迟到议事 |

异步故障注入报告位于 `artifacts/verification/async-agent-integration.log`。慢响应、服务失败、取消和迟到响应均由隔离 HTTP 测试桩控制，不代表真实付费服务的可用性。

## 文档与发布检查

`tools/check-docs.ps1` 检查文档大写命名、本地链接大小写、设计入口、六模块、许可证及所有工具 PowerShell 脚本语法，写入 `artifacts/verification/docs-check.json`。GitHub CI 在干净 Windows runner 执行此脚本、完整离线测试与原生编译，TRX 和文档报告作为 Actions 附件保存。

`tools/package-game.ps1` 从新的隔离目录导出，检查自包含运行时和便携文件，排除本地配置与玩家数据，并使用导出 EXE 的 `--headless --quit-after 5` 检查启动错误。此检查不覆盖显示、鼠标、菜单生命周期或真实窗口，不能替代下节验收。Windows 自动发布的提交来源、权限和附件校验见 [发布流程](RELEASE.md)。

本地发布链的全量离线测试为102项通过，记录位于 `artifacts/verification/release-tests.log`。干净打包和启动检查通过，记录位于 `artifacts/verification/package-final.log`；ZIP 内容审查确认包含四份第三方声明、不含本地配置或存档。解压该 ZIP 后的真实窗口验收也通过，报告为 `artifacts/verification/windows-package/InfiniteDao-Godot-Windows/artifacts/verification/native/report.json`，日志为 `artifacts/verification/zip-native.stdout.log`。这些本地产物不进入源码提交。

## 原生窗口覆盖

执行真实原生窗口验收，不发送模型请求：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/start-game.ps1 -Verify
```

它使用隔离存档，报告位于 `artifacts/verification/native/report.json`。当前报告 `passed: true`，`liveModel: null`，覆盖：

- 开宗选择、六个信息页、地图输入、键盘暂停、重点培养与更高境护道人。
- 真实鼠标打开速度和宗门筛选菜单，失焦处理、键盘选择与切回全部宗门；不绕过弹出过程。
- 未修改标准 seed8192 推进到70月，观察自动经营和118条历练相关记录。
- 显式终局夹具中87月的最后山门战役、个体交锋、统一界面与存档往返。
- 显式流亡夹具的撤离、薪火选择、购买落脚地与同一世界重建。

独立导出 EXE 使用 `-- --verify-native` 验收，当前报告为 `artifacts/releases/InfiniteDao-Godot-Windows/artifacts/verification/native/report.json`，`passed: true` 且退出码0。输出见 `artifacts/verification/export-native.stdout.log`。不能以源码窗口通过代替导出包检查。

## 模型与费用边界

仅在明确允许联网和费用后执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/start-game.ps1 -Verify -VerifyModel
```

- 当前原生报告未发送真实模型请求；异步与重试由集成测试覆盖，不据此宣称服务在线或费用已验证。
- 上下文上限256000、90%触发阈值和 compact 失败保留由隔离测试覆盖；不宣称完成了满窗真实服务压测。
- 严格工具约束结构，目标可达性、费用、选项身份和规则效果仍由本地校验。
- 常规测试、配置检查和 `-Verify` 不联网；只有显式 `-VerifyModel` 运行可能计费的真实服务验证。
- 服务延迟、可用上下文、计数接口与费用取决于用户配置；重试耗尽后离线，恢复联网需手动操作。

## 使用证据的限制

- 上述结果证明所列覆盖，不证明每个种子或策略都必胜，也不证明标准世界的自然统一已经验收。
- 自动化和原生验收使用隔离目录，不覆盖玩家存档；发布模板不复制工作区私密配置。
- 文档整理只需检查名称、链接、内容一致性和涉及的脚本，不重复触发窗口或付费验证。
- 代码、资源、接口或构建方式改变后，按影响范围更新实际证据和本文，不沿用不适用的通过结论。
