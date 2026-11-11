using Godot;
using InfiniteDao.Core.Game;
using static InfiniteDao.Game.NativeTheme;

namespace InfiniteDao.Game;

public partial class GameClient
{
    private Control Landing()
    {
        var rootView = new Control();
        var image = new TextureRect { Texture = landscape, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, Modulate = new Color(.6f, .75f, .72f, .62f), MouseFilter = MouseFilterEnum.Ignore };
        rootView.AddChild(image); image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var veil = new ColorRect { Color = new Color(.025f, .07f, .09f, .55f), MouseFilter = MouseFilterEnum.Ignore };
        rootView.AddChild(veil); veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var main = Stack(20); var header = Row();
        var brand = Stack(2); brand.AddChild(Heading("无限修仙 · 宗门纪", 30)); brand.AddChild(Text("INFINITE DAO  /  青岚山河录", 11, Gold));
        header.AddChild(brand); header.AddChild(Button("掌门手札", "help", ShowHelp)); header.AddChild(Button("世界与模型", "settings", ShowSettings));
        header.AddChild(Button("读取存档", "saves", ShowSaves, busy)); main.AddChild(header);
        var hero = Stack(8); hero.AddChild(Space(10)); hero.AddChild(Text("一宗兴衰，千载山河", 15, Gold));
        hero.AddChild(Heading("执掌一宗，问鼎万里山河。", 52));
        hero.AddChild(Text("六宗并立，众生自行修行。你只需在命运的关口，落下宗门的选择。\n山门可以倾覆；只要薪火尚存，道统便能重来。", 17, Paper));
        main.AddChild(hero); main.AddChild(Space(0, true));
        var selectionHeading = Row(); selectionHeading.AddChild(Heading("选择你的道统", 23)); selectionHeading.AddChild(Text("六种传承，同一片会记住你的山河。", 14, Muted)); main.AddChild(selectionHeading);
        var sectRow = Row(12);
        string[] traits = ["剑修战力 +12%", "进攻战力 +8%", "灵材产出 +40%", "每月额外 15 灵石", "外交与情报先机", "修炼每月额外 +2"];
        foreach (var sect in WorldFactory.Templates())
        {
            var selected = sect.Id == selectedSect;
            var button = Button("", "select-" + sect.Id, () => { selectedSect = sect.Id; Render(); }, busy);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill; button.CustomMinimumSize = new Vector2(0, 278);
            button.AddThemeStyleboxOverride("normal", Box(selected ? new Color("#213f3b") : new Color("#102630"), selected ? new Color(sect.Color) : Line, selected ? 2 : 1, 0));
            var content = Stack(7); content.MouseFilter = MouseFilterEnum.Ignore;
            var portrait = Portrait(portraits, sect.Portrait, 100, 158); portrait.SizeFlagsHorizontal = SizeFlags.ExpandFill; content.AddChild(portrait);
            content.AddChild(Heading(sect.Name, 23, new Color(sect.Color)));
            content.AddChild(Text(sect.Motto, 12, Muted)); content.AddChild(Text(traits[sect.Portrait], 13, selected ? Paper : Gold));
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
        main.AddChild(Text("终极目标：灭掉其余五宗。  /  原生桌面 · 选项驱动 · 随时暂停察看", 12, Muted));
        var padded = Pad(main, 30); rootView.AddChild(padded); padded.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); return rootView;
    }

    private Control Campaign()
    {
        var state = world!; var sect = state.Player; var shell = Stack(12);
        var header = Row(24); var identity = Stack(3); identity.CustomMinimumSize = new Vector2(210, 0);
        identity.AddChild(Text("无限修仙  /  宗门纪", 12, Gold)); identity.AddChild(Heading(sect.Name, 29, new Color(sect.Color))); identity.AddChild(Text(Date(state.Month), 13, Muted)); header.AddChild(identity);
        header.AddChild(Metric("宗门灵石", Number(sect.Stones), Gold)); header.AddChild(Metric("丹药灵材", Number(sect.Herbs), Jade));
        header.AddChild(Metric("在世门徒", GameEngine.Members(state, sect).Count.ToString())); header.AddChild(Metric("掌控山河", $"{GameEngine.Lands(state, sect).Count} / {state.Regions.Count}"));
        header.AddChild(Metric("宗门战力", Number(GameEngine.Power(state, sect)))); header.AddChild(Metric("山门安定", sect.Stability + "%"));
        var utility = Stack(6); utility.SizeFlagsHorizontal = SizeFlags.ShrinkEnd; utility.AddChild(Button("宗门存档", "saves", ShowSaves, busy)); utility.AddChild(Button("设置与手札", "settings", ShowSettings)); header.AddChild(utility);
        shell.AddChild(Card(header, padding: 16));
        var body = Row(16); body.SizeFlagsVertical = SizeFlags.ExpandFill;
        var nav = Stack(8); nav.CustomMinimumSize = new Vector2(144, 0); nav.SizeFlagsHorizontal = SizeFlags.Fill;
        nav.AddChild(Text("山 河 纪 略", 12, Muted)); nav.AddChild(Space(6));
        (string Id, string Label)[] pages = [("map", "山河舆图"), ("sect", "山门内政"), ("people", "门徒名册"), ("diplomacy", "诸宗外交"), ("treasury", "道统宝库"), ("history", "青岚史书")];
        foreach (var entry in pages)
        {
            var button = Button(entry.Label, "nav-" + entry.Id, () => Navigate(entry.Id));
            if (page == entry.Id) button.AddThemeStyleboxOverride("normal", Box(new Color("#24423b"), Jade));
            nav.AddChild(button);
        }
        nav.AddChild(Space(10));
        if (state.PendingEvent is not null) nav.AddChild(Button("议事待决  ●", "nav-council", () => Navigate("council"), primary: true));
        if (state.Status == "victory") nav.AddChild(Button("天下归一", "nav-ending", () => Navigate("ending"), primary: true));
        nav.AddChild(Space(0, true));
        nav.AddChild(Text(sect.Exiled ? "薪火流亡中" : $"宗门策略\n{Policy(sect.Policy)}", 14, sect.Exiled ? Cinnabar : Muted));
        nav.AddChild(Text($"已灭他宗  {state.Sects.Count(s => s.Id != state.PlayerId && s.Eliminated)} / 5\n薪火重建  {state.Rebirths} 次", 12, Muted));
        body.AddChild(nav);
        Control content = page switch
        {
            "sect" => SectView(), "people" => PeopleView(), "diplomacy" => DiplomacyView(), "treasury" => TreasuryView(),
            "history" => HistoryView(), "council" => CouncilView(), "ending" => EndingView(), _ => MapView()
        };
        content.SizeFlagsHorizontal = SizeFlags.ExpandFill; content.SizeFlagsVertical = SizeFlags.ExpandFill; body.AddChild(content); shell.AddChild(body);
        var footer = Row(14); var message = Stack(5);
        message.AddChild(Text(state.PendingEvent is not null ? "议事待决 · 时间已暂停" : sect.Exiled ? "山门虽失，道统尚存 · 请选择落脚地重建" : state.Status == "victory" ? "青岚已归一宗 · 仍可查看天下与史书" : $"{(paused ? "世界已暂停" : "世界自动演进中")}  /  执事自动经营 · {Policy(sect.Policy)}", 14, state.PendingEvent is not null || sect.Exiled ? Gold : Paper));
        var latest = state.History.LastOrDefault(); statusLabel = Text(busy ? notice : string.IsNullOrEmpty(notice) ? latest?.Title ?? "" : notice, 12, Muted, true); message.AddChild(statusLabel); footer.AddChild(message);
        if (state.PendingEvent is not null) footer.AddChild(Button("前往议事", "open-council", () => Navigate("council")));
        var speed = new OptionButton { CustomMinimumSize = new Vector2(155, 42) }; foreach (var label in new[] { "观岁 · 一载12秒", "流年 · 一载3秒", "闭关 · 一载0.6秒" }) speed.AddItem(label);
        speed.SetMeta("action", "time-speed");
        speed.TooltipText = "月度规则逐次结算；Agent在后台规划，不阻塞时间，未返回时沿用已有战略。";
        speed.Select(Array.IndexOf(new[] { 1d, .25d, .05d }, secondsPerMonth)); speed.ItemSelected += i => secondsPerMonth = new[] { 1d, .25d, .05d }[i]; footer.AddChild(speed);
        footer.AddChild(Button(busy ? "月末暂停  Ⅱ" : paused ? "继续演进  ▷" : "暂停演进  Ⅱ", "pause", TogglePause, state.Status != "playing" || (!busy && paused && (state.PendingEvent is not null || sect.Exiled))));
        footer.AddChild(Button("推演一载   →", "advance", AdvanceYear, busy || state.Status != "playing" || state.PendingEvent is not null || sect.Exiled, true));
        shell.AddChild(Card(footer, padding: 14)); return Pad(shell, 18);
    }

    private static Control Metric(string name, string value, Color? color = null)
    {
        var metric = Stack(5); metric.AddChild(Text(name, 12, Muted)); metric.AddChild(Text(value, 24, color ?? Paper)); return metric;
    }

    private static VBoxContainer Section(string title, string subtitle)
    {
        var section = Stack(14); section.AddChild(Heading(title, 30)); section.AddChild(Text(subtitle, 13, Muted, true)); return section;
    }
}
