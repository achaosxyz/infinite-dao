using System.Collections.Concurrent;
using System.Text.Json;
using Godot;
using InfiniteDao.Agents.Providers;
using InfiniteDao.Application;
using InfiniteDao.Core.Game;
using InfiniteDao.Infrastructure.Persistence;
using HttpClient = System.Net.Http.HttpClient;
using static InfiniteDao.Game.NativeTheme;

namespace InfiniteDao.Game;

public partial class GameClient : Control
{
    private readonly ConcurrentQueue<Action> mainThread = new();
    private readonly CancellationTokenSource lifetime = new();
    private HttpClient http = null!;
    private GameSession session = null!;
    private GameState? world;
    private ModelConnectionStatus model = new(false, "high", 0, 0);
    private Texture2D landscape = null!;
    private Texture2D[] portraits = [];
    private Control surface = null!;
    private Control? overlay;
    private Label? statusLabel;
    private string root = "";
    private string page = "map";
    private string selectedRegion = "r06";
    private string selectedSect = "qingyun";
    private string selectedPersonSect = "";
    private string personSearch = "";
    private string historyFilter = "all";
    private string mode = "llm";
    private string difficulty = "normal";
    private string seed = "8192";
    private string notice = "";
    private bool busy;
    private bool showTitle = true;
    private bool paused = true;
    private bool hasSave;
    private bool verification;
    private double elapsed;
    private double secondsPerMonth = .25;
    private bool pauseRequested;
    private bool renderRequested;
    private bool backgroundPumpBusy;
    private double backgroundPoll;
    private int displayedEvent = -1;
    private List<SaveSummary> saveSummaries = [];

    public override void _Ready()
    {
        Theme = NativeTheme.Create();
        var backdrop = new ColorRect { Color = Ink, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(backdrop); backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        surface = new Control(); AddChild(surface); surface.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        try
        {
            verification = OS.GetCmdlineUserArgs().Contains("--verify-native");
            root = LocateRoot();
            http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
            var saves = verification ? Path.Combine(root, "artifacts", "verification", "native", "saves") : null;
            session = new GameSession(root, http, saves);
            model = session.ModelStatus();
            if (!model.Configured) mode = "rules";
            landscape = GD.Load<Texture2D>("res://Assets/qinglan-world.png");
            portraits = Enumerable.Range(0, WorldFactory.PortraitCount).Select(i => GD.Load<Texture2D>($"res://Assets/cultivator-{i:00}.png")).ToArray();
            hasSave = File.Exists(Path.Combine(saves ?? Path.Combine(root, "artifacts", "game", "saves"), "auto.json"));
            Render();
            if (verification) BeginNativeVerification();
        }
        catch (Exception error)
        {
            GD.PrintErr($"Native startup failed: {error.GetType().Name}");
            var content = Stack(); content.AddChild(Heading("山门暂未开启"));
            content.AddChild(Text("游戏资源或运行目录不完整。请使用根目录的启动游戏.cmd，或重新导出完整 Windows 游戏目录。", wrap: true));
            surface.AddChild(Pad(content, 60));
            if (verification) GetTree().Quit(1);
        }
    }

    private static string LocateRoot()
    {
        var arguments = OS.GetCmdlineUserArgs();
        var rootOption = Array.IndexOf(arguments, "--data-root");
        if (rootOption >= 0)
        {
            if (rootOption + 1 >= arguments.Length) throw new DirectoryNotFoundException("Missing data root.");
            var explicitRoot = Path.GetFullPath(arguments[rootOption + 1]);
            if (!Directory.Exists(Path.Combine(explicitRoot, "config"))) throw new DirectoryNotFoundException("Data root must contain config.");
            return explicitRoot;
        }
        var start = OS.HasFeature("editor") ? ProjectSettings.GlobalizePath("res://") : Path.GetDirectoryName(OS.GetExecutablePath())!;
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "InfiniteDao.slnx")) || Directory.Exists(Path.Combine(directory.FullName, "config"))) return directory.FullName;
        throw new DirectoryNotFoundException("Game configuration root was not found.");
    }

    public override void _Process(double delta)
    {
        while (mainThread.TryDequeue(out var action)) action();
        if (renderRequested)
        {
            // A popup can emit focus notifications while Godot is still opening it.
            // Never detach its OptionButton from that callback or while the popup is visible.
            if (HasVisiblePopup(surface)) return;
            renderRequested = false;
            RebuildView();
        }
        backgroundPoll += delta;
        if (backgroundPoll >= .2 && !busy && !backgroundPumpBusy && !showTitle && world is not null && (world.Mode == "llm" || session.HasBackgroundWork))
        {
            backgroundPoll = 0;
            PumpBackground();
        }
        if (busy && statusLabel is not null && IsInstanceValid(statusLabel))
            statusLabel.Text = (session.Activity.Length > 0 ? session.Activity : notice) + new string('·', 1 + (int)(Time.GetTicksMsec() / 500 % 3));
        else if (statusLabel is not null && IsInstanceValid(statusLabel) && session.Activity.Length > 0)
            statusLabel.Text = session.Activity;
        if (paused || page != "map" || busy || overlay is not null || world is null || showTitle) return;
        elapsed += delta;
        if (elapsed >= secondsPerMonth) { elapsed = 0; Advance(false); }
    }

    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (GetViewport().GuiGetFocusOwner() is LineEdit) return;
        if (key.Keycode == Key.Space && world is not null && !showTitle && overlay is null) { TogglePause(); GetViewport().SetInputAsHandled(); }
        if (key.Keycode == Key.Escape)
        {
            if (overlay is not null) CloseOverlay(); else { paused = true; Render(); }
            GetViewport().SetInputAsHandled();
        }
        if (key.CtrlPressed && key.Keycode == Key.S && world is not null) { Save("one"); GetViewport().SetInputAsHandled(); }
    }

    public override void _ExitTree() { lifetime.Cancel(); http?.Dispose(); lifetime.Dispose(); }

    public override void _Notification(int what)
    {
        if (what == NotificationWMWindowFocusOut) { paused = true; pauseRequested = true; if (surface is not null && world is not null) Render(); }
    }

    private void Run(Func<Task> work, string message, Action? after = null)
    {
        if (busy) return;
        busy = true; notice = message; Render();
        _ = Task.Run(async () =>
        {
            Exception? failure = null;
            try { await session.Gate.WaitAsync(lifetime.Token); try { await work(); } finally { session.Gate.Release(); } }
            catch (Exception error) { failure = error; }
            mainThread.Enqueue(() =>
            {
                var previousStatus = world?.Status;
                busy = false; world = session.State; model = session.ModelStatus();
                if (failure is not null)
                {
                    paused = true;
                    notice = failure is GameRuleException or ModelDecisionException ? failure.Message : "操作未完成。原有世界仍保留，请检查存档目录与模型配置后再试。";
                    GD.PrintErr($"Native action failed: {failure.GetType().Name}");
                    Render(); ShowMessage("此次操作未完成", notice); return;
                }
                hasSave = hasSave || world is not null;
                notice = "已自动保存";
                if (world?.PendingEvent is { } pending && displayedEvent != pending.Id)
                { displayedEvent = pending.Id; page = "council"; paused = true; }
                if (world is not null && (world.Player.Exiled || world.Status != "playing")) paused = true;
                if (world?.Status == "victory" && previousStatus != "victory") page = "ending";
                if (page == "council" && world?.PendingEvent is null) page = "map";
                after?.Invoke(); Render();
            });
        });
    }

    private void PumpBackground()
    {
        backgroundPumpBusy = true;
        _ = Task.Run(async () =>
        {
            var changed = false; Exception? failure = null;
            try
            {
                await session.Gate.WaitAsync(lifetime.Token);
                try { changed = await session.PumpAgents(lifetime.Token); }
                finally { session.Gate.Release(); }
            }
            catch (Exception error) { failure = error; }
            mainThread.Enqueue(() =>
            {
                backgroundPumpBusy = false;
                if (failure is not null)
                {
                    paused = true; notice = "后台结果暂未保存，请检查存档目录；现有世界仍保留。";
                    GD.PrintErr($"Background result persistence failed: {failure.GetType().Name}");
                    Render(); return;
                }
                if (!changed) return;
                world = session.State; model = session.ModelStatus();
                notice = !string.IsNullOrEmpty(world?.AgentStatus) ? world.AgentStatus : "已自动保存";
                Render();
            });
        });
    }

    private void StartWorld()
    {
        if (!int.TryParse(seed, out var number)) { ShowMessage("种子无效", "世界种子需要是 -2147483648 至 2147483647 的整数。"); return; }
        var sect = selectedSect; var selectedMode = mode; var selectedDifficulty = difficulty;
        void Begin()
        {
            CloseOverlay(); paused = true; displayedEvent = -1;
            Run(async () =>
            {
                await session.NewGame(sect, number, selectedMode, selectedDifficulty, lifetime.Token);
            }, "立宗建档", () => { showTitle = false; selectedRegion = world!.Player.Capital; selectedPersonSect = world.PlayerId; });
        }
        if (!hasSave) { Begin(); return; }
        var content = Stack(); content.AddChild(Text("开始新世界会覆盖自动存档。三个手动存档仍保留；如需保留当前进度，请先手动存档。", wrap: true));
        content.AddChild(Button("确认开始新世界", "confirm-new", Begin, primary: true));
        ShowOverlay("另启一段宗门史", content);
    }

    private void Command(string type, string target = "", string option = "")
    {
        if (world is null) return;
        var revision = world.Revision;
        Run(async () =>
        {
            await session.Execute(revision, new GameCommand(type, target, option), lifetime.Token);
        }, "宗门安排结算中", () =>
        {
            if (type == "choose" && page == "map" && overlay is null && !pauseRequested && world is { PendingEvent: null, Status: "playing" } && !world.Player.Exiled && !verification)
            { paused = false; elapsed = 0; }
        });
    }

    private void Advance(bool manual = true)
    {
        if (world is null || busy || world.Status != "playing" || world.PendingEvent is not null || world.Player.Exiled) return;
        if (manual) paused = true;
        var revision = world.Revision;
        Run(() => session.Advance(revision, lifetime.Token), world.Mode == "llm" ? "各宗 Agent 依流程议政" : "推演本月山河");
    }

    private void AdvanceYear()
    {
        if (world is null || busy || world.PendingEvent is not null || world.Player.Exiled || world.Status != "playing") return;
        paused = true; pauseRequested = false;
        Run(async () =>
        {
            for (var i = 0; i < 12 && !pauseRequested; i++)
            {
                var state = session.State!;
                if (state.PendingEvent is not null || state.Player.Exiled || state.Status != "playing") break;
                await session.Advance(state.Revision, lifetime.Token);
            }
        }, "推演一载山河 · 遇事即停");
    }

    private void TogglePause()
    {
        if (world is null) return;
        if (busy) { pauseRequested = true; paused = true; notice = "已请求暂停；当前月结算后停止"; Render(); return; }
        if (!paused) { paused = true; Render(); return; }
        if (busy || world.PendingEvent is not null || world.Player.Exiled || world.Status != "playing")
        { notice = "请先处理议事或重建山门，再继续演进。"; Render(); return; }
        page = "map"; paused = false; pauseRequested = false; elapsed = 0; Render();
    }

    private void Navigate(string destination)
    {
        if (destination != "map") { paused = true; pauseRequested = true; elapsed = 0; }
        page = destination; Render();
    }

    private void Render() => renderRequested = true;

    private static bool HasVisiblePopup(Node parent)
    {
        foreach (var child in parent.GetChildren(true))
        {
            if (child is Popup { Visible: true } || HasVisiblePopup(child)) return true;
        }
        return false;
    }

    private void RebuildView()
    {
        foreach (var child in surface.GetChildren()) { surface.RemoveChild(child); child.QueueFree(); }
        statusLabel = null;
        var view = world is null || showTitle ? Landing() : Campaign();
        surface.AddChild(view); view.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    private void Save(string slot)
    {
        if (world is null) return; var revision = world.Revision; paused = true;
        Run(() => session.Saves.SaveAsync(session.Require(revision), slot), "正在保存", () => { CloseOverlay(); notice = "已存入手动档"; });
    }

    private void Load(string slot)
    {
        paused = true; CloseOverlay(); displayedEvent = -1;
        Run(async () => await session.Commit(await session.Saves.LoadAsync(slot)), "正在读取存档", () => { showTitle = false; selectedRegion = world!.Player.Capital; selectedPersonSect = world.PlayerId; page = world.PendingEvent is null ? "map" : "council"; });
    }

    private void ShowSaves()
    {
        paused = true;
        Run(async () => saveSummaries = await session.Saves.ListAsync(), "读取存档目录", () =>
        {
            var content = Stack();
            content.AddChild(Text("策略、培养、关键选择与月度结算都会原子写入自动档。手动档不会被新局覆盖。", color: Muted, wrap: true));
            foreach (var slot in new[] { "auto", "one", "two", "three" })
            {
                var summary = saveSummaries.FirstOrDefault(s => s.Slot == slot);
                var row = Row(); var info = Stack(4);
                info.AddChild(Text(slot == "auto" ? "自动存档" : $"手动存档 {Array.IndexOf(new[] { "one", "two", "three" }, slot) + 1}", 17));
                info.AddChild(Text(summary is null ? "尚无存档" : $"{summary.Sect} · {Date(summary.Month)} · {summary.UpdatedUtc.ToLocalTime():MM-dd HH:mm}", 13, Muted));
                row.AddChild(info); row.AddChild(Button("读取", "load-" + slot, () => Load(slot), summary is null || summary.Status == "invalid"));
                if (slot != "auto") row.AddChild(Button("写入", "save-" + slot, () => Save(slot), world is null));
                content.AddChild(Card(row));
            }
            var files = Row(); files.AddChild(Button("导出独立存档", "export", () => SelectSaveFile(false), world is null));
            files.AddChild(Button("导入独立存档", "import", () => SelectSaveFile(true))); content.AddChild(files);
            ShowOverlay("宗门存档", content);
        });
    }

    private void SelectSaveFile(bool import)
    {
        paused = true;
        var dialog = new FileDialog
        {
            Title = import ? "导入宗门存档" : "导出宗门存档", Access = FileDialog.AccessEnum.Filesystem,
            FileMode = import ? FileDialog.FileModeEnum.OpenFile : FileDialog.FileModeEnum.SaveFile,
            UseNativeDialog = true, Filters = ["*.json ; 宗门存档"], CurrentFile = import ? "" : "无限修仙-宗门存档.json"
        };
        AddChild(dialog);
        dialog.FileSelected += path =>
        {
            var snapshot = world; CloseOverlay();
            Run(async () =>
            {
                if (import) await session.Commit(await ReadSaveImport(session.Saves, path));
                else if (snapshot is not null) await File.WriteAllTextAsync(path, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            }, import ? "校验并导入存档" : "导出存档", () => { if (world is not null) { selectedRegion = world.Player.Capital; if (import) showTitle = false; } });
            dialog.QueueFree();
        };
        dialog.Canceled += () => dialog.QueueFree(); dialog.PopupCentered(new Vector2I(900, 600));
    }

    private static async Task<GameState> ReadSaveImport(GameSaveStore saves, string path)
    {
        if (new FileInfo(path).Length > 3L * GameSaveStore.MaximumSaveCharacters) throw new GameRuleException("存档文件超过导入大小限制。");
        return saves.Parse(await File.ReadAllTextAsync(path));
    }

    private void ShowOverlay(string title, Control content)
    {
        paused = true; pauseRequested = true; elapsed = 0; CloseOverlay();
        overlay = new Control { MouseFilter = MouseFilterEnum.Stop }; AddChild(overlay); overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(0, .025f, .035f, .86f) }; overlay.AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer(); overlay.AddChild(center); center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var stack = Stack(20); var heading = Row(); var label = Heading(title); label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        heading.AddChild(label); heading.AddChild(Button("关闭  Esc", "close-dialog", CloseOverlay)); stack.AddChild(heading); stack.AddChild(content);
        var card = Card(stack, Gold, 28); card.CustomMinimumSize = new Vector2(780, 0); center.AddChild(card);
    }

    private void CloseOverlay() { if (overlay is null) return; RemoveChild(overlay); overlay.QueueFree(); overlay = null; }
    private void ShowMessage(string title, string message) { var text = Text(message, wrap: true); text.CustomMinimumSize = new Vector2(700, 0); ShowOverlay(title, text); }

    private void ShowHelp() => ShowMessage("掌门手札", "一、处理开宗之议后，世界自动演进。你只定战略方向，可重点培养三名门人，安排更高境护道人。纳新、建设、传功、疗伤与战争筹备均自动进行。\n\n二、十二府192处灵地。点击地图查看产出、阵法与边界。战争持续数年，月月供给；按实际门人逐一交锋，低一大境界伤害仅12%，总战力不代表胜负。\n\n三、门人自动修行与历练；突破须修龄，历练一至三年。护道人须实际同行，可能受伤或殉身；历练者不可同时出征。史书与人物资料保留亡者。\n\n四、各宗Agent独立在后台按需规划，默认最多10个同时运行，不阻塞时间；等待时沿用已有战略。返回计划须通过当前世界校验。调用可能持续计费；失败自动重试3次，仍失败会明确切换离线并记入史书，可在设置中手动恢复智能世界。\n\n五、末座山门失守，幸存门徒携道统出逃，选薪火去向并择地重建，仍是同一世界。消灭其余宗门的道统即可统一青岚。\n\n空格：暂停／继续；推演一载：逐月最多一年，遇事即停；Esc：关闭面板／暂停；Ctrl+S：保存档一。可以随时暂停检查世界、宗门与人物。");
}
