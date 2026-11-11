using Godot;
using InfiniteDao.Core.Game;
using static InfiniteDao.Game.NativeTheme;

namespace InfiniteDao.Game;

public partial class GameClient
{
    private float mapZoom = 1;
    private Vector2 mapPan;
    private Control MapView()
    {
        var state = world!;
        var row = Row(8); row.SizeFlagsVertical = SizeFlags.ExpandFill;
        var map = new WorldMap { World = state, Landscape = landscape, Selected = selectedRegion, Zoom = mapZoom, Pan = mapPan };
        map.ViewChanged += (zoom, pan) => { mapZoom = zoom; mapPan = pan; };
        map.RegionSelected += id => { selectedRegion = id; Render(); };
        var stage = new Control { CustomMinimumSize = new Vector2(440, 400), SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        map.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); stage.AddChild(map);
        var title = Stack(3); title.MouseFilter = MouseFilterEnum.Ignore; title.SetMeta("hud_map_title", true);
        title.AddChild(Heading("青岚十二府", 27, DeepJade));
        title.AddChild(Text($"{state.Regions.Count}处灵地 · {state.Sects.Count}宗并立 · 战线{state.Campaigns.Count}处", 12, Muted));
        var titlePlate = new PanelContainer { Position = new Vector2(14, 12), CustomMinimumSize = new Vector2(318, 86), MouseFilter = MouseFilterEnum.Ignore };
        titlePlate.SetMeta("hud_map_title_plate", true);
        var titleFrame = Frame("hud-title", 16, 16); titleFrame.ContentMarginLeft = 24; titleFrame.ContentMarginRight = 24;
        titlePlate.AddThemeStyleboxOverride("panel", titleFrame); titlePlate.AddChild(title); stage.AddChild(titlePlate);
        var mapTools = Row(8); mapTools.MouseFilter = MouseFilterEnum.Stop;
        var legend = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        foreach (var sect in state.Sects)
        {
            var entry = Row(4); entry.MouseFilter = MouseFilterEnum.Ignore; entry.AddChild(Emblem(sect, 21));
            entry.AddChild(Text($"{sect.Name} {GameEngine.Lands(state, sect).Count}", 12, new Color(sect.Color).Darkened(.4f))); legend.AddChild(entry);
        }
        mapTools.AddChild(legend);
        foreach (var (label, key, delta) in new[] { ("－", "zoom-out", -.3f), ("＋", "zoom-in", .3f) })
        {
            var zoomButton = Button(label, key, () =>
            {
                mapZoom = Mathf.Clamp(mapZoom + delta, 1, 5.5f);
                var limit = map.Size * (mapZoom - 1) / 2;
                mapPan = new Vector2(Mathf.Clamp(mapPan.X, -limit.X, limit.X), Mathf.Clamp(mapPan.Y, -limit.Y, limit.Y)); Render();
            });
            zoomButton.CustomMinimumSize = new Vector2(32, 34); zoomButton.TooltipText = label == "＋" ? "放大地图，也可使用滚轮" : "缩小地图"; mapTools.AddChild(zoomButton);
        }
        var reset = Button("复位", "reset-map", () => { mapZoom = 1; mapPan = Vector2.Zero; Render(); }); reset.CustomMinimumSize = new Vector2(48, 34); mapTools.AddChild(reset);
        var mapDock = new PanelContainer { AnchorTop = 1, AnchorBottom = 1, AnchorRight = 1, OffsetLeft = 8, OffsetRight = -8, OffsetTop = -72, OffsetBottom = -8, MouseFilter = MouseFilterEnum.Stop };
        mapDock.AddThemeStyleboxOverride("panel", Box(new Color(NativeTheme.Panel, .95f), Brass, 1, 6)); mapDock.AddChild(mapTools); stage.AddChild(mapDock);
        var mapCard = Card(stage, padding: 0); mapCard.SizeFlagsVertical = SizeFlags.ExpandFill; row.AddChild(mapCard);
        var region = state.Regions.SingleOrDefault(r => r.Id == selectedRegion) ?? state.Regions[0];
        var owner = state.Sects.SingleOrDefault(s => s.Id == region.Owner);
        var detail = Stack(8); var inspectorHeading = Row(4);
        var tab = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 34) };
        tab.AddThemeStyleboxOverride("panel", Frame("hud-action", 4, 16));
        var tabLabel = Heading("地域", 16, NativeTheme.Panel); tabLabel.HorizontalAlignment = HorizontalAlignment.Center; tab.AddChild(tabLabel); inspectorHeading.AddChild(tab);
        var militaryLink = Button("军情", "campaigns", ShowCampaigns); militaryLink.CustomMinimumSize = new Vector2(0, 34); militaryLink.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        militaryLink.AddThemeFontOverride("font", Display); militaryLink.AddThemeFontSizeOverride("font_size", 16); inspectorHeading.AddChild(militaryLink); detail.AddChild(inspectorHeading);
        var affiliation = Row(10); affiliation.AddChild(owner is null ? Icon("land", 42, Gold) : Emblem(owner, 44));
        var regionName = Stack(0); regionName.AddChild(Heading(region.Name.Split('·').Last(), 29, DeepJade));
        regionName.AddChild(Text((owner?.Name ?? "无主灵地") + (owner?.Capital == region.Id ? " · 山门" : " · " + region.Terrain), 12, Muted)); affiliation.AddChild(regionName); detail.AddChild(affiliation);
        var cropWidth = Math.Min(560, landscape.GetWidth()); var cropHeight = Math.Min(180, landscape.GetHeight());
        var centerX = ((region.Index % state.MapColumns + .5f) / state.MapColumns) * landscape.GetWidth();
        var centerY = ((region.Index / state.MapColumns + .5f) / state.MapRows) * landscape.GetHeight();
        detail.AddChild(new TextureRect
        {
            Texture = new AtlasTexture { Atlas = landscape, Region = new Rect2(Math.Clamp(centerX - cropWidth / 2f, 0, landscape.GetWidth() - cropWidth), Math.Clamp(centerY - cropHeight / 2f, 0, landscape.GetHeight() - cropHeight), cropWidth, cropHeight) },
            CustomMinimumSize = new Vector2(0, 96), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            TooltipText = "山河地貌写意；地域归属、产出与建筑等级以实时数据为准。", MouseFilter = MouseFilterEnum.Pass
        });
        var metrics = new GridContainer { Columns = 2 }; metrics.AddChild(Metric("凡民人口", Number(region.Population)));
        metrics.AddChild(Metric("灵脉品阶", region.Spirit.ToString())); metrics.AddChild(Metric("地域发展", $"{region.Development} / 5")); metrics.AddChild(Metric("护山阵法", $"{region.Fort} / 5")); detail.AddChild(metrics);
        var yield = Row(6); yield.AddChild(Text("月产底数", 12, Muted)); yield.AddChild(Icon("stone", 18, Jade)); yield.AddChild(Value((12 + region.Mine * 7 + region.Development * 8).ToString(), 17, Jade));
        yield.AddChild(Icon("herb", 18, Jade)); yield.AddChild(Value((2 + region.HerbYield * 3 + region.Development).ToString(), 17, Jade)); detail.AddChild(Pad(yield, 4));
        var front = state.Campaigns.SingleOrDefault(c => c.Target == region.Id);
        if (front is not null) detail.AddChild(Text($"战役进行中 · 余 {front.MonthsRemaining}/{front.Duration} 月\n补给完整度 {front.Supply:P0} · 月耗 {front.MonthlyStones} 石 / {front.MonthlyHerbs} 材", 14, Cinnabar, true));
        if (state.Player.Exiled)
        {
            var noFreeLand = state.Regions.All(r => r.Owner != ""); var allowed = owner is null || (noFreeLand && owner.Capital != region.Id);
            var cost = owner is null ? 150 : 350;
            detail.AddChild(Text("薪火重建", 17, Cinnabar)); detail.AddChild(Text("优先落脚无主地；若天下已无空地，可赎买他宗非山门地域。同一世界、幸存门徒与旧日恩怨都会延续。", 13, Muted, true));
            detail.AddChild(Button($"在此重建 · {cost} 灵石", "settle-" + region.Id, () => Command("settle", region.Id), busy || state.PendingEvent is not null || state.CommandsLeft == 0 || !allowed || state.Player.Stones < cost, true));
        }
        else if (region.Owner == state.PlayerId)
        {
            detail.AddChild(Text("执事依宗策自动经营，优先保障半年供养。", 12, Jade, true));
        }
        else
        {
            var border = GameEngine.Border(state, state.Player, region); var peace = owner is not null && GameEngine.AtPeace(state, state.Player, owner);
            var deployed = GameEngine.IsMobilized(state, state.Player);
            detail.AddChild(Text(!border ? "此地尚未与本宗接壤，不能出征。" : peace ? $"盟约持续至 {Date(state.Player.Truces[owner!.Id])}，不可背盟。" : deployed ? "本宗主力正在远征，不能同时开启另一进攻。" : $"预计战役至少 {GameEngine.MinimumBattleMonths(state, region)} 月，守备约 {GameEngine.DefensePower(state, region)}。先议战法，再逐月行军围城。", 13, Muted, true));
        }
        var regionAction = Button("查看地域  ›", "region-details", () => ShowRegionDetails(region.Id), primary: true);
        regionAction.AddThemeFontOverride("font", Display); regionAction.AddThemeFontSizeOverride("font_size", 20); detail.AddChild(regionAction);
        detail.AddChild(new ColorRect { Color = new Color(Brass, .55f), CustomMinimumSize = new Vector2(0, 1), MouseFilter = MouseFilterEnum.Ignore });
        var currentLeader = owner is null ? null : state.People.SingleOrDefault(p => p.Id == owner.LeaderId && p.Alive);
        if (currentLeader is not null)
        {
            var profile = Row(10); profile.AddChild(Portrait(portraits, currentLeader.Portrait, 96, 112));
            var person = Stack(3); person.AddChild(Text("现任掌门", 12, Muted)); person.AddChild(Heading(currentLeader.Name, 24, DeepJade));
            person.AddChild(Text(WorldFactory.RealmNames[currentLeader.Realm], 13, Gold));
            var biography = Button("人物小传  ›", "map-leader-" + currentLeader.Id, () => ShowPerson(currentLeader)); biography.CustomMinimumSize = new Vector2(0, 34); biography.AddThemeFontSizeOverride("font_size", 14);
            person.AddChild(biography); profile.AddChild(person); detail.AddChild(profile);
        }
        var detailCard = Card(Scroll(detail)); detailCard.CustomMinimumSize = new Vector2(334, 0); detailCard.SizeFlagsHorizontal = SizeFlags.Fill; detailCard.SizeFlagsVertical = SizeFlags.ExpandFill;
        row.AddChild(detailCard); return row;
    }

    private void ShowRegionDetails(string id)
    {
        var state = world!; var region = state.Regions.Single(r => r.Id == id);
        var owner = state.Sects.SingleOrDefault(s => s.Id == region.Owner);
        var content = Stack(12); var identity = Row(12);
        if (owner is not null) identity.AddChild(Emblem(owner, 50));
        var heading = Stack(3); heading.AddChild(Heading(region.Name, 28, DeepJade));
        heading.AddChild(Text($"{owner?.Name ?? "无主灵地"} · {region.Terrain}" + (owner?.Capital == region.Id ? " · 宗门山门" : ""), 14, Muted)); identity.AddChild(heading); content.AddChild(identity);
        content.AddChild(Text(region.Description, 14, Muted, true));
        var metrics = new GridContainer { Columns = 4 };
        metrics.AddChild(Metric("凡民人口", Number(region.Population))); metrics.AddChild(Metric("灵脉品阶", region.Spirit.ToString()));
        metrics.AddChild(Metric("地域发展", $"{region.Development}/5")); metrics.AddChild(Metric("护山阵法", $"{region.Fort}/5")); content.AddChild(metrics);
        content.AddChild(Text($"矿藏 {region.Mine} 阶 · 灵材 {region.HerbYield} 阶\n月产底数：灵石 {12 + region.Mine * 7 + region.Development * 8}，灵材 {2 + region.HerbYield * 3 + region.Development}。宗策与宗门加成另外结算。", 15, Muted, true));
        var front = state.Campaigns.SingleOrDefault(c => c.Target == id);
        if (front is not null)
            content.AddChild(Text($"当前战役：余 {front.MonthsRemaining}/{front.Duration} 月，补给完整度 {front.Supply:P0}，月耗 {front.MonthlyStones} 灵石 / {front.MonthlyHerbs} 灵材。", 15, Cinnabar, true));
        else content.AddChild(Text("当前没有战役在此进行。", 14, Jade));
        content.AddChild(Text("建设、纳新与战争由宗门依战略自动安排；此面板只查看地域，不直接决定战果。", 14, Muted, true));
        content.AddChild(Heading("相邻地域", 20, DeepJade));
        var neighbors = new GridContainer { Columns = 2 };
        foreach (var neighborId in region.Neighbors)
        {
            var next = state.Regions.Single(r => r.Id == neighborId); var nextOwner = state.Sects.SingleOrDefault(s => s.Id == next.Owner);
            neighbors.AddChild(Button(next.Name + " · " + (nextOwner?.Name ?? "无主"), "region-" + neighborId, () => { CloseOverlay(); selectedRegion = neighborId; Render(); }));
        }
        content.AddChild(neighbors);
        var scroll = Scroll(content); scroll.CustomMinimumSize = new Vector2(740, 400); ShowOverlay("地域档案", scroll);
    }

    private Control SectView()
    {
        var state = world!; var sect = state.Player; var view = Section("宗门战略", "只定方向，不逐月发令。执事自动纳新、营建、传功、疗伤、外交与筹备战争；遇关键抉择才请掌门裁定。");
        var leader = state.People.Single(p => p.Id == sect.LeaderId);
        var overview = Row(24); overview.AddChild(Portrait(portraits, leader.Portrait, 162, 246)); var bio = Stack(9);
        bio.AddChild(Emblem(sect, 42));
        bio.AddChild(Heading($"{leader.Name} · {leader.Role}", 24)); bio.AddChild(Text(sect.Motto, 18, Gold)); bio.AddChild(Text(sect.Personality, 14, Muted, true));
        bio.AddChild(Text($"道藏 {sect.Doctrine} / 8   ·   军阵 {sect.Training} / 10   ·   情报 {sect.Intel} / 5\n声望 {sect.Renown} · 战疲 {sect.WarExhaustion}%（驻守每月恢复 4）", 14)); overview.AddChild(bio);
        overview.AddChild(Metric("每月灵石净入", $"{GameEngine.Income(state, sect) - GameEngine.Upkeep(state, sect):+0;-0;0}", Gold)); overview.AddChild(Metric("每月灵材产出", "+" + GameEngine.HerbIncome(state, sect), Jade)); view.AddChild(Card(overview));
        var policies = Row(8);
        for (var i = 0; i < WorldFactory.Policies.Length; i++)
        {
            var key = WorldFactory.Policies[i]; var button = Button(WorldFactory.PolicyNames[i], "policy-" + key, () => Command("policy", option: key), busy || state.PendingEvent is not null || state.Status != "playing", sect.Policy == key);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill; policies.AddChild(button);
        }
        view.AddChild(policies);
        var guide = Stack(14); guide.AddChild(Heading("一纸宗策，静观百年", 26, Gold));
        guide.AddChild(Text("均衡发展：兼顾道藏、民生与防务。\n休养生息：优先坊市与储备，减少战争倾向。\n潜心修炼：优先道藏，长期培养高阶修士。\n开疆拓土：提高扩张倾向，但须先备足军需。\n远交近攻：经营关系与五年盟约，择机发展。", 16, Paper, true));
        guide.AddChild(Text("门人按修龄自行破境，不能以数次演武跳过境界积累。战事由阵法、实际参战门人和跨境界压制决定，战力总和只供参考。", 14, Muted, true));
        var target = state.Regions.FirstOrDefault(r => r.Id == sect.Target);
        guide.AddChild(Text($"当前筹划：{target?.Name ?? "观察边境"}\n经营保留灵石 {GameEngine.ReservedStones(state, sect)} / 现有 {sect.Stones}；军需保留丹材 {GameEngine.ReservedHerbs(state, sect)} / 现有 {sect.Herbs}。采办与营建不会挪用战略现金；满修为等待稳固与返虚道成者不再消耗修炼丹材。", 14, Gold, true));
        guide.AddChild(Button("安排重点培养与护道人   →", "strategy-people", () => { selectedPersonSect = state.PlayerId; Navigate("people"); }));
        view.AddChild(Card(guide)); return Scroll(view);
    }

    private Control CouncilView()
    {
        var state = world!; var pending = state.PendingEvent;
        if (pending is null) return Section("静候风起", "当前没有议事。可以查看山河与门人，或继续自动演进。");
        var view = Section(pending.Eyebrow, "世界已自动暂停。你仍可切换所有概况面板；落下选择后，世界才会继续。");
        var narrative = Stack(18); narrative.AddChild(Text($"{Date(state.Month)}  /  宗门抉择", 13, Gold)); narrative.AddChild(Heading(pending.Title, 36));
        narrative.AddChild(Text(pending.Text, 18, Paper, true));
        if (pending.Type is "attack" or "defense")
        {
            var attacker = state.Sects.Single(s => s.Id == pending.Actor); var target = state.Regions.Single(r => r.Id == pending.Target);
            narrative.AddChild(Text($"出征方：{attacker.Name} · 战力 {GameEngine.Power(state, attacker)}\n争夺地：{target.Name} · 阵法 {target.Fort} 阶\n战役预计至少 {GameEngine.MinimumBattleMonths(state, target)} 月，选择战法不会立即夺地；每月消耗补给，伤亡与结果会记入史书。", 14, Muted, true));
        }
        view.AddChild(Card(narrative, Gold, 25));
        foreach (var choice in pending.Choices)
        {
            var row = Row(20); var words = Stack(6); words.AddChild(Heading(choice.Label, 24));
            if (choice.Flavor.Length > 0) words.AddChild(Text(choice.Flavor, 15, Paper, true));
            words.AddChild(Text("规则效果：" + choice.Description, 14, Muted, true)); row.AddChild(words);
            var action = Stack(6); action.SizeFlagsHorizontal = SizeFlags.Fill; action.CustomMinimumSize = new Vector2(210, 0);
            if (choice.Stones > 0 || choice.Herbs > 0) action.AddChild(Text($"需 {choice.Stones} 灵石 / {choice.Herbs} 灵材", 12, Gold));
            action.AddChild(Button("就这样决定   →", "choose-" + choice.Id, () => Command("choose", option: choice.Id), busy || state.Player.Stones < choice.Stones || state.Player.Herbs < choice.Herbs, true)); row.AddChild(action); view.AddChild(Card(row));
        }
        return Scroll(view);
    }

    private Control EndingView()
    {
        var state = world!; var view = Section("万宗归一，山河共主", "青岚域只余一脉道统。这片山河，终于记住了你的宗门。");
        var row = Row(28); var leader = state.People.Single(p => p.Id == state.Player.LeaderId);
        row.AddChild(Portrait(portraits, leader.Portrait, 230, 350)); var text = Stack(20); text.AddChild(Emblem(state.Player, 60));
        text.AddChild(Heading(state.Player.Name, 46, Gold)); text.AddChild(Text($"历经 {state.Month / 12} 年 {state.Month % 12} 月，{state.Rebirths} 次薪火重建。\n{GameEngine.Members(state, state.Player).Count} 位门徒见证了这一天。", 20, Paper, true));
        text.AddChild(Text("胜利并不抹去来路。门徒、战役、故人、宝物与所有因果，仍留在世界与史书中。", 16, Muted, true));
        text.AddChild(Button("重读青岚史书", "ending-history", () => Navigate("history"), primary: true));
        text.AddChild(Button("另启一段宗门史", "return-title", () => { showTitle = true; page = "map"; Render(); })); row.AddChild(text); view.AddChild(Card(row, Gold, 30)); return Scroll(view);
    }
}
