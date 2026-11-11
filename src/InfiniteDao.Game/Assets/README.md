# 宗门纪原生美术

- `qinglan-world.png`：1671 × 941，淡青玉绿的动漫山河背景，用于标题与实时地域地图；不包含界面文字、地域归属或战线。
- `cultivator-00.png`—`cultivator-49.png`：50张512 × 768、真实透明通道的独立人物立绘，男女、年龄、轮廓与服装各有区分。Godot 按人物的 `Portrait` 加载并裁切头像，人物小传显示完整立绘；不按掌门职位或宗门重抽。
- `icon.svg`：山形与月的宗门印记，用于原生窗口标识。
- `relic-attack_1_1.png`—`relic-attack_3_5.png`、`relic-protection_1_1.png`—`relic-protection_3_5.png`：30张512×512真实透明的独立随身法宝图标，按 Core 固定模板 ID 加载，不随持有人改变。
- `hud-panel.svg`、`hud-nav.svg`、`hud-action.svg`：原创的月白面板、选中页签与玉青主操作切角框，使用 Godot `StyleBoxTexture` 九宫格缩放；不包含文字、按钮内容或游戏数据。
- `hud-title.svg`、`hud-header.svg`、`hud-calendar.svg`：地图内浮动切角题签、资源/时间带与单层日期饰牌；日期牌不叠弧顶或回纹，文字、月份刻度和交互控件均由原生界面实时构造。
- `hud-control.svg`：通用按钮与速度下拉框的月白切角细框，与玉青主操作区分；悬停、按下与禁用色由原生主题控制。

人物采用已确认的清晰赛璐璐画法；HUD 使用玉青、月白和浅灰青边线，哑金只作少量点缀。题签装饰不侵入文字区。宗门使用独立宗徽识别，原生矢量图标和实时控件由 `NativeTheme.cs` 绘制，地图徽记保留文字印记。地图上建筑只作背景，地域、归属、选择和阵法标记由实时世界数据绘制，不把背景画作不可变世界事实。

统一生产标准与0—49角色描述见 [ART_STYLE.md](../../../docs/design/GAME/ART_STYLE.md)。`tools/prepare-portraits.ps1 -ManifestPath artifacts/verification/art/portrait-source-manifest.json` 从25组双人原稿裁切单图，并输出分组审查图与总览；原稿清单和审查图位于忽略的 `artifacts/verification/art/`。游戏仅使用本目录的单图，不依赖生成服务或本机 Session 目录。

此目录是原生客户端实际使用的资源，不依赖网页、CDN 或个人临时目录。中文字体由 Windows 的楷体、微软雅黑系统字体提供；未复制或分发 Windows 字体文件。模块契约见 [Game MODULE.md](../../../docs/design/GAME/MODULE.md)。
