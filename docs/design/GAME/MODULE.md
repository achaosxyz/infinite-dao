# Game 模块

## 职责与边界

本模块位于 `src/InfiniteDao.Game/`，是 Godot 4.7.2 .NET 原生桌面客户端，只直接引用 Application。全局结构见 [PROJECT_DESIGN.md](../PROJECT_DESIGN.md)。表现层展示规则事实、接收选项与控制时间，不自行生成资源、判定战果或解释自然语言命令。

## 代码入口

- `project.godot`、`Main.tscn`：原生工程和启动场景。
- `GameClient.cs`：主线程队列、时间推进、会话调用、后台结果轮询、重绘与文件对话框。
- `GameViews.cs`：开局、导航、世界页和时间控制。
- `GameViews.Records.cs`：门人、外交、Agent 记忆、史册、配置与存档相关视图。
- `GameViews.Strategy.cs`、`GameViews.Military.cs`：内政、培养、军情、议事及重建等策略视图。
- `WorldMap.cs`：从当前地域数据绘制地图标记、选择、缩放和拖动。
- `NativeTheme.cs`、`Assets/`：主题、青绿山河图、六掌门图集与图标。
- `NativeVerification.cs`：真实窗口、原生控件、保存恢复、战役和流亡边界验收。

## 数据与运行

`world` 是最近提交的 `GameState` 引用；界面页、地图选择、暂停和速度属于表现状态。`GameSession` 操作通过调用方获取 `Gate` 后在工作任务执行，完成后把 UI 更新排入 Godot 主线程。模型工作由 Application 独立运行，客户端轮询 `PumpAgents`，不把模型等待计入前台忙碌月份。

玩家能查看山河、内政、门人、外交、道藏与史书，待决议事独立呈现。三档自动速度和逐月推进一载都受议事、流亡待重建和局终约束；空格、窗口失焦和手动暂停会停止后续推进，切换资料页本身不暂停。

议政记忆以中文显示当前计划、长期摘要与最近三次计划回顾。历史只读取现有 `submit_strategy` 公开记录中可识别的合法展示字段；观察快照、查询反馈和无效历史包不直接显示。此展示转换不改变底层公开会话、归档、模型上下文或存档格式。

## 控件生命周期约束

- `Render` 仅请求重绘，在帧边界合并执行实际界面重建。
- 弹出菜单仍可见时暂缓重建，不能在控件信号或失焦通知中销毁正在使用的下拉框。
- 后台工作不能直接访问 Godot 节点；节点修改由主线程队列处理。
- 失焦仍暂停时间，但不在通知调用栈内立即移除界面树。
- 回归必须真实打开菜单并选择，不能只发 `ItemSelected` 信号绕过弹出过程。

## 配置、美术与文件

- 根目录 `启动游戏.cmd` 优先使用原生导出包并传入工作区数据根；独立包 `Play.cmd` 使用自身目录。
- 配置与存档保存在资源包外；客户端不开放游戏 HTTP 服务。
- 文件导入入口限制为8000000字节，随后交由 Infrastructure 校验；导入失败不替换世界。
- 山河图是美术背景，地域、归属、人物和战役必须取自当前数据，不能把画中建筑当作固定世界事实。
- 中文字体由本机系统字体提供，资源目录不分发 Windows 字体文件；资源说明见 [Assets/README.md](../../../src/InfiniteDao.Game/Assets/README.md)。
- Windows 导出随包携带项目 MIT 和 `licenses/` 中的引擎、.NET 许可声明。Godot 官方固定版本文本保存在 `docs/GODOT_LICENSE.txt`、`docs/GODOT_COPYRIGHT.txt`；.NET 声明从构建 SDK 分发复制，不混同为项目许可证。

## 变更与验证

视图、计时、菜单、主线程调度或输入变更须运行真实原生窗口回归，并覆盖打开、选择、关闭、失焦与恢复。导出包还需直接启动其 EXE 检查，不能用浏览器或 headless 结果替代。命令与数据隔离要求见 [验证验收流程](../../VERIFICATION.md)，验收边界见 [VERIFICATION.md](../../VERIFICATION.md)。
