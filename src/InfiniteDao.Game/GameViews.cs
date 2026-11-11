using Godot;
using InfiniteDao.Core.Game;
using static InfiniteDao.Game.NativeTheme;

namespace InfiniteDao.Game;

public partial class GameClient
{
    private Control Landing()
    {
        var rootView = new Control();
        var image = new TextureRect { Texture = landscape, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, Modulate = new Color(1, 1, 1, .42f), MouseFilter = MouseFilterEnum.Ignore };
        rootView.AddChild(image); image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var veil = new ColorRect { Color = new Color(NativeTheme.Panel, .58f), MouseFilter = MouseFilterEnum.Ignore };
        rootView.AddChild(veil); veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var main = Stack(15); var header = Row();
        var brand = Stack(2); brand.AddChild(Heading("无限修仙 · 宗门纪", 30, DeepJade)); brand.AddChild(Text("青岚山河录  /  道统薪传", 12, Gold));
        header.AddChild(brand); header.AddChild(Button("掌门手札", "help", ShowHelp)); header.AddChild(Button("世界与模型", "settings", ShowSettings));
        header.AddChild(Button("读取存档", "saves", ShowSaves, busy)); main.AddChild(header);
        var hero = Stack(6); hero.AddChild(Text("一宗兴衰，千载山河", 14, Gold));
        hero.AddChild(Heading("执掌一宗，问鼎万里山河。", 42, DeepJade));
        hero.AddChild(Text("十宗并立，众生自行修行。山门可以倾覆；只要薪火尚存，道统便能重来。", 16, Paper));
        main.AddChild(hero); main.AddChild(Space(0, true));
        var selectionHeading = Row(); selectionHeading.AddChild(Heading("选择你的道统", 23)); selectionHeading.AddChild(Text("十种传承 · 众生各有生平 · 同一片山河", 14, Muted)); main.AddChild(selectionHeading);
        var sectRow = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var sect in WorldFactory.Templates())
        {
            var selected = sect.Id == selectedSect;
            var button = Button("", "select-" + sect.Id, () => { selectedSect = sect.Id; Render(); }, busy);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill; button.CustomMinimumSize = new Vector2(0, 165);
            button.AddThemeStyleboxOverride("normal", Box(selected ? Raised : NativeTheme.Panel, selected ? Jade : Line, selected ? 2 : 1, 0));
            var content = Stack(8); content.MouseFilter = MouseFilterEnum.Ignore;
            var identity = Row(9); identity.MouseFilter = MouseFilterEnum.Ignore; identity.AddChild(Emblem(sect, 46));
            identity.AddChild(Heading(sect.Name, 21, DeepJade)); content.AddChild(identity);
            content.AddChild(Text(sect.Motto, 12, Muted)); content.AddChild(Text(WorldFactory.TraitDescription(sect.Trait), 13, selected ? Jade : Gold, true));
            content.AddChild(Text(sect.Personality, 12, Muted, true));
            var padding = Pad(content, 12); padding.MouseFilter = MouseFilterEnum.Ignore; button.AddChild(padding); padding.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            sectRow.AddChild(button);
        }
        main.AddChild(sectRow);
        var options = Row(16);
        var worldMode = new OptionButton { CustomMinimumSize = new Vector2(230, 46) };
        worldMode.AddItem("智能世界 · Anthropic"); worldMode.AddItem("离线世界 · 本地演算"); worldMode.Select(mode == "llm" ? 0 : 1);
        worldMode.ItemSelected += i => mode = i == 0 ? "llm" : "rules"; worldMode.SetMeta("action", "world-mode"); options.AddChild(worldMode);
        var level = new OptionButton { CustomMinimumSize = new Vector2(160, 46) }; string[] levels = ["story", "normal", "hard"];
        foreach (var name in new[] { "山门叙事 · 宽裕", "群雄并立 · 标准", "逆势问道 · 艰难" }) level.AddItem(name);
        level.Select(Array.IndexOf(levels, difficulty)); level.ItemSelected += i => difficulty = levels[i]; options.AddChild(level);
        options.AddChild(Text("世界种子", 13, Muted));
        var input = new LineEdit { Text = seed, CustomMinimumSize = new Vector2(120, 46), MaxLength = 11 }; input.TextChanged += value => seed = value; options.AddChild(input);
        var modelText = Text(model.Configured ? $"模型已配置 · 思考 {model.Effort ?? "默认"}" : "模型未配置；可明确选择离线世界", 13, Muted, true); options.AddChild(modelText);
        options.AddChild(Button(busy ? "正在开启山门…" : "执掌宗门   →", "new-game", StartWorld, busy, true)); main.AddChild(Card(options, padding: 14));
        main.AddChild(Text("终极目标：统一其余九宗道统。  /  原生桌面 · 选项驱动 · 随时暂停察看", 12, Muted));
        var padded = Pad(main, 30); rootView.AddChild(padded); padded.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); return rootView;
    }

    private Control Campaign()
    {
        var state = world!; var sect = state.Player; var shell = Stack(0);
        var header = Row(14); var identity = Row(10); identity.CustomMinimumSize = new Vector2(195, 0); identity.SizeFlagsHorizontal = SizeFlags.Fill;
        identity.AddChild(Emblem(sect, 54)); var brand = Stack(0); brand.AddChild(Heading(sect.Name, 27, DeepJade)); brand.AddChild(Text("宗 门 纪  /  道 统 薪 传", 12, Gold)); identity.AddChild(brand); header.AddChild(identity);
        header.AddChild(new ColorRect { Color = new Color(Brass, .45f), CustomMinimumSize = new Vector2(1, 38), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore });
        header.AddChild(ResourceMetric("stone", "灵石", Number(sect.Stones)));
        header.AddChild(ResourceMetric("herb", "灵材", Number(sect.Herbs)));
        header.AddChild(ResourceMetric("people", "门人", GameEngine.Members(state, sect).Count.ToString()));
        header.AddChild(ResourceMetric("lands", "疆域", $"{GameEngine.Lands(state, sect).Count} / {state.Regions.Count}"));
        header.AddChild(ResourceMetric("power", "战力", Number(GameEngine.Power(state, sect))));
        header.AddChild(ResourceMetric("stability", "安定", sect.Stability + "%"));
        var utility = Row(6); utility.SizeFlagsHorizontal = SizeFlags.Fill;
        utility.AddChild(Button("存档", "saves", ShowSaves, busy)); utility.AddChild(Button("设置", "settings", ShowSettings)); header.AddChild(utility);
        var headerPlate = Card(header, padding: 12); headerPlate.AddThemeStyleboxOverride("panel", Frame("hud-header", 12, 40)); shell.AddChild(headerPlate);
        var body = Row(0); body.SizeFlagsVertical = SizeFlags.ExpandFill;
        var nav = Stack(7); nav.CustomMinimumSize = new Vector2(130, 0); nav.SizeFlagsHorizontal = SizeFlags.Fill;
        nav.AddChild(Space(6));
        (string Id, string Label)[] pages = [("map", "山河舆图"), ("sect", "山门内政"), ("people", "门徒名册"), ("diplomacy", "诸宗外交"), ("treasury", "道统宝库"), ("history", "青岚史书")];
        foreach (var entry in pages)
        {
            var active = page == entry.Id;
            var button = Button("", "nav-" + entry.Id, () => Navigate(entry.Id)); button.CustomMinimumSize = new Vector2(130, 58);
            button.AddThemeStyleboxOverride("normal", active ? Frame("hud-nav", 0, 16) : Box(Colors.Transparent, Colors.Transparent, 0, 0));
            button.AddThemeStyleboxOverride("hover", active ? Frame("hud-nav", 0, 16) : Box(Jade, Brass, 1, 0));
            button.AddThemeStyleboxOverride("pressed", active ? Frame("hud-nav", 0, 16) : Box(DeepJade, Brass, 1, 0));
            button.AddThemeStyleboxOverride("focus", Box(Colors.Transparent, Brass, 1, 0));
            var label = Row(8); label.MouseFilter = MouseFilterEnum.Ignore;
            label.AddChild(Icon(entry.Id, 23, active ? DeepJade : NativeTheme.Panel)); label.AddChild(Heading(entry.Label, 17, active ? DeepJade : NativeTheme.Panel));
            var inset = Pad(label, 8); inset.MouseFilter = MouseFilterEnum.Ignore; button.AddChild(inset); inset.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            nav.AddChild(button);
        }
        nav.AddChild(Space(10));
        if (state.PendingEvent is not null) nav.AddChild(Button("议事待决  ●", "nav-council", () => Navigate("council"), primary: true));
        if (state.Status == "victory") nav.AddChild(Button("天下归一", "nav-ending", () => Navigate("ending"), primary: true));
        nav.AddChild(Space(0, true));
        var policySummary = Stack(6); policySummary.SetMeta("hud_rail_summary", true);
        policySummary.AddChild(Text("当前宗门策略", 12, Mist));
        policySummary.AddChild(Heading(sect.Exiled ? "薪火流亡中" : Policy(sect.Policy), 18, sect.Exiled ? new Color("#efb7a2") : NativeTheme.Panel));
        policySummary.AddChild(new ColorRect { Color = new Color(Brass, .4f), CustomMinimumSize = new Vector2(0, 1), MouseFilter = MouseFilterEnum.Ignore });
        policySummary.AddChild(Text($"已灭他宗  {state.Sects.Count(s => s.Id != state.PlayerId && s.Eliminated)} / {state.Sects.Count - 1}\n薪火重建  {state.Rebirths} 次", 12, Mist)); nav.AddChild(policySummary);
        var rail = Card(nav, padding: 16); rail.SetMeta("hud_rail", true); rail.SizeFlagsHorizontal = SizeFlags.Fill; rail.SizeFlagsVertical = SizeFlags.ExpandFill;
        var railStyle = Box(DeepJade, new Color(Mist, .55f), 1, 16); railStyle.SetCornerRadiusAll(0);
        rail.AddThemeStyleboxOverride("panel", railStyle); body.AddChild(rail);
        Control content = page switch
        {
            "sect" => SectView(), "people" => PeopleView(), "diplomacy" => DiplomacyView(), "treasury" => TreasuryView(),
            "history" => HistoryView(), "council" => CouncilView(), "ending" => EndingView(), _ => MapView()
        };
        content.SizeFlagsHorizontal = SizeFlags.ExpandFill; content.SizeFlagsVertical = SizeFlags.ExpandFill; body.AddChild(Pad(content, page == "map" ? 6 : 18)); shell.AddChild(body);
        var footer = Row(12); var message = Stack(3); message.CustomMinimumSize = new Vector2(320, 0); message.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        var messageHeading = Row(8);
        var alert = state.PendingEvent is not null || sect.Exiled || state.Status == "victory";
        messageHeading.AddChild(Text(alert ? state.PendingEvent is not null ? "议事待决 · 时间已暂停" : sect.Exiled ? "薪火流亡 · 请选落脚地" : "天下归一 · 史书长存" : "宗门纪事", 12, alert ? Cinnabar : Jade));
        statusLabel = Text(busy ? notice : string.IsNullOrEmpty(notice) ? "操作成功后自动存档" : notice, 12, Muted);
        statusLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill; statusLabel.HorizontalAlignment = HorizontalAlignment.Right;
        statusLabel.ClipText = true; statusLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        messageHeading.AddChild(statusLabel); message.AddChild(messageHeading);
        foreach (var entry in state.History.Where(e => e.Sects.Contains(sect.Id)).OrderByDescending(e => e.Id).Take(2))
        {
            var eventRow = Row(5); eventRow.AddChild(Icon(entry.Kind is "war" or "death" ? "power" : "history", 16, entry.Kind is "war" or "death" ? Cinnabar : Jade));
            var eventButton = Button(entry.Title, "chronicle-" + entry.Id, () => ShowMessage(entry.Title, Date(entry.Month) + "\n" + entry.Text + (entry.Delta.Length > 0 ? "\n" + entry.Delta : "")));
            eventButton.CustomMinimumSize = new Vector2(0, 22); eventButton.SizeFlagsHorizontal = SizeFlags.ExpandFill; eventButton.Alignment = HorizontalAlignment.Left;
            eventButton.AddThemeFontSizeOverride("font_size", 13); eventButton.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
            eventButton.AddThemeStyleboxOverride("hover", Box(new Color(Jade, .06f), Colors.Transparent, 0, 0));
            eventButton.ClipText = true; eventButton.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; eventButton.TooltipText = entry.Text;
            eventRow.AddChild(eventButton); eventRow.AddChild(Text($"{entry.Month / 12 + 1}年{entry.Month % 12 + 1}月", 12, Muted)); message.AddChild(eventRow);
        }
        var bulletin = Card(message, padding: 7); bulletin.AddThemeStyleboxOverride("panel", Box(Raised, Colors.Transparent, 0, 7)); footer.AddChild(bulletin);
        var calendar = Row(10); calendar.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        calendar.AddChild(Calendar(state.Month, 44)); var date = Stack(1); date.SizeFlagsVertical = SizeFlags.ShrinkCenter; date.AddChild(Heading(Date(state.Month), 18, DeepJade));
        date.AddChild(Text(paused || state.PendingEvent is not null || sect.Exiled || state.Status != "playing" ? "Ⅱ 世界已暂停" : "▷ 岁月演进中", 12, state.PendingEvent is not null ? Cinnabar : Jade)); calendar.AddChild(date);
        var calendarPlate = new PanelContainer { CustomMinimumSize = new Vector2(292, 76), SizeFlagsHorizontal = SizeFlags.Fill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        calendarPlate.SetMeta("hud_calendar", true);
        var calendarFrame = Frame("hud-calendar", 8, 34); calendarFrame.ContentMarginTop = 18;
        calendarPlate.AddThemeStyleboxOverride("panel", calendarFrame); calendarPlate.AddChild(calendar); footer.AddChild(calendarPlate);
        if (state.PendingEvent is not null) footer.AddChild(Button("前往议事", "open-council", () => Navigate("council")));
        var speed = new OptionButton { CustomMinimumSize = new Vector2(155, 42), SizeFlagsVertical = SizeFlags.ShrinkCenter }; foreach (var label in new[] { "观岁 · 一载12秒", "流年 · 一载3秒", "闭关 · 一载0.6秒" }) speed.AddItem(label);
        speed.SetMeta("action", "time-speed");
        speed.TooltipText = "月度规则逐次结算；Agent在后台规划，不阻塞时间，未返回时沿用已有战略。";
        speed.Select(Array.IndexOf(new[] { 1d, .25d, .05d }, secondsPerMonth)); speed.ItemSelected += i => secondsPerMonth = new[] { 1d, .25d, .05d }[i]; footer.AddChild(speed);
        var advance = Button("推演一载  →", "advance", AdvanceYear, busy || state.Status != "playing" || state.PendingEvent is not null || sect.Exiled);
        advance.AddThemeFontOverride("font", Display); advance.AddThemeFontSizeOverride("font_size", 17); footer.AddChild(advance);
        var pause = Button(busy ? "月末暂停  Ⅱ" : paused ? "▷ 继续演进" : "Ⅱ 暂停演进", "pause", TogglePause, state.Status != "playing" || (!busy && paused && (state.PendingEvent is not null || sect.Exiled)), true);
        pause.AddThemeFontOverride("font", Display); pause.AddThemeFontSizeOverride("font_size", 17); footer.AddChild(pause);
        var footerPlate = Card(footer, padding: 10); footerPlate.SetMeta("hud_footer", true); footerPlate.AddThemeStyleboxOverride("panel", Frame("hud-header", 10, 40)); shell.AddChild(footerPlate); return shell;
    }

    private static Control ResourceMetric(string icon, string name, string value)
    {
        var row = Row(7); row.AddChild(Icon(icon, 26)); var words = Stack(0);
        words.AddChild(Text(name, 12, Muted)); words.AddChild(Value(value, 24)); row.AddChild(words); return row;
    }

    private static Control Metric(string name, string value, Color? color = null)
    {
        var metric = Stack(5); metric.AddChild(Text(name, 12, Muted)); metric.AddChild(Value(value, 25, color ?? Paper)); return metric;
    }

    private static VBoxContainer Section(string title, string subtitle)
    {
        var section = Stack(14); section.AddChild(Heading(title, 30)); section.AddChild(Text(subtitle, 13, Muted, true)); return section;
    }
}
