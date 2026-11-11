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
        var view = Section("青岚十二府", $"{state.Regions.Count} 处灵地 · 六宗分据山河。滚轮放大，右键拖动；执事依战略自动发展，战争历时数年。");
        var legend = Row(14);
        foreach (var sect in state.Sects) legend.AddChild(Text($"● {sect.Name}  {GameEngine.Lands(state, sect).Count}", 12, new Color(sect.Color)));
        legend.AddChild(Button("复位地图", "reset-map", () => { mapZoom = 1; mapPan = Vector2.Zero; Render(); })); view.AddChild(legend);
        var military = Row(); military.AddChild(Text($"天下战线 {state.Campaigns.Count} 处 · 本宗战疲 {state.Player.WarExhaustion}% · {(GameEngine.IsMobilized(state, state.Player) ? "远征军在外" : "主力驻守山门")}", 13, Gold));
        military.AddChild(Button("查看战役与补给", "campaigns", ShowCampaigns)); view.AddChild(military);
        var row = Row(16); row.SizeFlagsVertical = SizeFlags.ExpandFill;
        var map = new WorldMap { World = state, Landscape = landscape, Selected = selectedRegion, Zoom = mapZoom, Pan = mapPan };
        map.ViewChanged += (zoom, pan) => { mapZoom = zoom; mapPan = pan; };
        map.RegionSelected += id => { selectedRegion = id; Render(); };
        var mapCard = Card(map, padding: 0); mapCard.SizeFlagsVertical = SizeFlags.ExpandFill; row.AddChild(mapCard);
        var region = state.Regions.SingleOrDefault(r => r.Id == selectedRegion) ?? state.Regions[0];
        var owner = state.Sects.SingleOrDefault(s => s.Id == region.Owner);
        var detail = Stack(14); detail.AddChild(Text(owner?.Name ?? "尚无宗门占据", 13, owner is null ? Gold : new Color(owner.Color)));
        detail.AddChild(Heading(region.Name, 31)); detail.AddChild(Text(region.Terrain + (owner?.Capital == region.Id ? "  /  宗门山门" : "  /  灵地"), 13, Muted));
        detail.AddChild(Text(region.Description, 14, Paper, true));
        var metrics = new GridContainer { Columns = 2 }; metrics.AddChild(Metric("凡民人口", Number(region.Population)));
        metrics.AddChild(Metric("灵脉品阶", region.Spirit.ToString())); metrics.AddChild(Metric("地域发展", $"{region.Development} / 5")); metrics.AddChild(Metric("护山阵法", $"{region.Fort} / 5")); detail.AddChild(metrics);
        detail.AddChild(Text($"矿藏 {region.Mine} 阶 · 灵材 {region.HerbYield} 阶\n月产灵石基础 {12 + region.Mine * 7 + region.Development * 8}\n月产灵材基础 {2 + region.HerbYield * 3 + region.Development}", 13, Muted));
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
            detail.AddChild(Text("坊市、灵田、护山阵由执事依宗门战略自动营建，优先保障半年供养储备。无需逐月下令。", 14, Jade, true));
        }
        else
        {
            var border = GameEngine.Border(state, state.Player, region); var peace = owner is not null && GameEngine.AtPeace(state, state.Player, owner);
            var deployed = GameEngine.IsMobilized(state, state.Player);
            detail.AddChild(Text(!border ? "此地尚未与本宗接壤，不能出征。" : peace ? $"盟约持续至 {Date(state.Player.Truces[owner!.Id])}，不可背盟。" : deployed ? "本宗主力正在远征，不能同时开启另一进攻。" : $"预计战役至少 {GameEngine.MinimumBattleMonths(state, region)} 月，守备约 {GameEngine.DefensePower(state, region)}。先议战法，再逐月行军围城。", 13, Muted, true));
            detail.AddChild(Text("宗门自行选择接壤目标、评估军力与数年补给。切换「开疆拓土」可提高扩张倾向；战略并非保证必胜。", 12, Muted, true));
        }
        detail.AddChild(Text("相邻地域", 13, Gold));
        var neighbors = new GridContainer { Columns = 2 };
        foreach (var id in region.Neighbors)
        {
            var next = state.Regions.Single(r => r.Id == id); neighbors.AddChild(Button(next.Name, "region-" + id, () => { selectedRegion = id; Render(); }));
        }
        detail.AddChild(neighbors);
        var detailCard = Card(Scroll(detail)); detailCard.CustomMinimumSize = new Vector2(300, 0); detailCard.SizeFlagsHorizontal = SizeFlags.Fill; detailCard.SizeFlagsVertical = SizeFlags.ExpandFill;
        row.AddChild(detailCard); view.AddChild(row); return view;
    }

    private Control SectView()
    {
        var state = world!; var sect = state.Player; var view = Section("宗门战略", "只定方向，不逐月发令。执事自动纳新、营建、传功、疗伤、外交与筹备战争；遇关键抉择才请掌门裁定。");
        var overview = Row(24); overview.AddChild(Portrait(portraits, sect.Portrait, 150, 150)); var bio = Stack(9);
        var leader = state.People.Single(p => p.Id == sect.LeaderId);
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
        var row = Row(28); row.AddChild(Portrait(portraits, state.Player.Portrait, 260, 260)); var text = Stack(20);
        text.AddChild(Heading(state.Player.Name, 46, Gold)); text.AddChild(Text($"历经 {state.Month / 12} 年 {state.Month % 12} 月，{state.Rebirths} 次薪火重建。\n{GameEngine.Members(state, state.Player).Count} 位门徒见证了这一天。", 20, Paper, true));
        text.AddChild(Text("胜利并不抹去来路。门徒、战役、故人、宝物与所有因果，仍留在世界与史书中。", 16, Muted, true));
        text.AddChild(Button("重读青岚史书", "ending-history", () => Navigate("history"), primary: true));
        text.AddChild(Button("另启一段宗门史", "return-title", () => { showTitle = true; page = "map"; Render(); })); row.AddChild(text); view.AddChild(Card(row, Gold, 30)); return Scroll(view);
    }
}
