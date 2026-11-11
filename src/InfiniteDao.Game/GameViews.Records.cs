using System.Text.Json;
using Godot;
using InfiniteDao.Core.Game;
using static InfiniteDao.Game.NativeTheme;

namespace InfiniteDao.Game;

public partial class GameClient
{
    private bool showDeceased;
    private int historyVisible = 120;
    private static string CultivationText(Cultivator person) => person.Ascended ? "已飞升 · 道统传承"
        : person.Realm == WorldFactory.MaximumRealm ? $"大乘 {person.RealmMonths}/120 月 · {(person.RetainInWorld ? "申请留世，足资逐月供给" : "满十年自动飞升") }"
        : !GameEngine.CanAdvanceRealm(person) ? "已达先天上限 · 仍需日常供养"
        : person.Progress >= 110 && person.RealmMonths >= WorldFactory.MinimumRealmMonths[person.Realm] && person.Injury < 30
            ? "修为已足 · 待归山/备资破境"
        : person.Progress >= 120 && person.RealmMonths < WorldFactory.MinimumRealmMonths[person.Realm]
            ? $"修为已满 · 稳固境界还需{WorldFactory.MinimumRealmMonths[person.Realm] - person.RealmMonths}月"
            : $"修为 {person.Progress} / 120";

    private Control PeopleView()
    {
        var state = world!; var view = Section("门徒名册", "自动修炼、破境、历练与老去，亦可能战死。最多重点培养三人；护道人须境界更高，随行也有风险。");
        var controls = Row(); var sectFilter = new OptionButton { CustomMinimumSize = new Vector2(180, 42) }; sectFilter.AddItem("天下所有门徒");
        sectFilter.SetMeta("action", "people-sect-filter");
        foreach (var sect in state.Sects) sectFilter.AddItem(sect.Name);
        sectFilter.Select(state.Sects.FindIndex(s => s.Id == selectedPersonSect) + 1); controls.AddChild(sectFilter);
        var search = new LineEdit { Text = personSearch, PlaceholderText = "按姓名查找", CustomMinimumSize = new Vector2(230, 42), ClearButtonEnabled = true }; controls.AddChild(search);
        var deceased = new CheckButton { Text = "包含先辈（故去/飞升）", ButtonPressed = showDeceased }; controls.AddChild(deceased); view.AddChild(controls);
        var list = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        void Fill()
        {
            foreach (var child in list.GetChildren()) { list.RemoveChild(child); child.QueueFree(); }
            var people = state.People.Where(p => (showDeceased || p.Alive) && (selectedPersonSect == "" || p.SectId == selectedPersonSect) && p.Name.Contains(personSearch, StringComparison.OrdinalIgnoreCase)).OrderByDescending(p => p.Alive).ThenByDescending(p => p.Role == "掌门").ThenByDescending(p => p.Focused).ThenByDescending(p => p.InnateTrait).ThenByDescending(p => p.Realm).ThenByDescending(p => p.Potential).ToList();
            foreach (var person in people)
            {
                var sect = state.Sects.SingleOrDefault(s => s.Id == person.SectId); var card = Stack(10); var profile = Row(15);
                var portrait = Portrait(portraits, person.Portrait, 82, 100); if (!person.Alive && !person.Ascended) portrait.Modulate = new Color(.5f, .55f, .55f); profile.AddChild(portrait);
                var info = Stack(5); info.AddChild(Heading(person.Name + (person.Ascended ? " · 飞升" : person.Alive ? "" : " · 已故"), 23, person.Ascended ? Gold : person.Alive ? Paper : Muted));
                var role = person.Role == "掌门" ? person.Alive && sect?.LeaderId == person.Id ? "现任掌门" : "历任掌门" : person.Role;
                info.AddChild(Text($"{sect?.Name ?? "散修"} · {role} · {WorldFactory.RealmNames[person.Realm]}", 13, sect is null ? Muted : new Color(sect.Color).Darkened(.4f)));
                info.AddChild(Text($"{person.AgeMonths / 12} 岁 / 寿元 {GameEngine.MaximumAgeMonths(person) / 12}   ·   资质 {person.Potential}   ·   忠诚 {person.Loyalty}", 12, Muted));
                info.AddChild(Text($"{WorldFactory.InnateTraitNames[person.InnateTrait]} · 上限{WorldFactory.RealmNames[WorldFactory.InnateRealmLimits[person.InnateTrait]]}", 12, person.InnateTrait >= 3 ? Gold : Muted));
                info.AddChild(Text($"{CultivationText(person)}   ·   伤势 {person.Injury}   ·   {person.Temperament}", 12, person.Injury > 40 ? Cinnabar : Muted, true)); profile.AddChild(info); card.AddChild(profile);
                if (person.Alive) card.AddChild(Text("随身法宝：" + (GameEngine.HeldRelic(state, person.Id)?.Name ?? "暂无，执事自动安排"), 12, Muted));
                var actions = Row(); actions.AddChild(Button("生平与修行", "person-" + person.Id, () => ShowPerson(person)));
                if (person.Alive && person.SectId == state.PlayerId)
                {
                    actions.AddChild(Button(person.Focused ? "取消重点培养" : "立为重点培养", "focus-" + person.Id, () => Command("focus", person.Id.ToString(), person.Focused ? "off" : "on"), busy || state.PendingEvent is not null || state.Status != "playing"));
                    actions.AddChild(Button("护道人", "guardian-" + person.Id, () => ShowGuardians(person), busy || state.PendingEvent is not null || state.Status != "playing" || GameEngine.IsAway(state, person.Id)));
                }
                var guardian = state.People.SingleOrDefault(p => p.Id == person.GuardianId);
                card.AddChild(Text($"{(person.Focused ? "道种 · 重点培养" : "常规修行")}  /  {(GameEngine.IsAway(state, person.Id) ? "在外历练或征战" : "山门静修")}\n护道人：{guardian?.Name ?? "无"}", 12, person.Focused ? Gold : Muted, true));
                card.AddChild(actions); list.AddChild(Card(card));
            }
            if (people.Count == 0) list.AddChild(Text("没有符合条件的门徒。可以切换宗门或清空姓名。", color: Muted));
        }
        sectFilter.ItemSelected += i => { selectedPersonSect = i == 0 ? "" : state.Sects[(int)i - 1].Id; Fill(); };
        search.TextChanged += text => { personSearch = text; Fill(); }; deceased.Toggled += value => { showDeceased = value; Fill(); };
        Fill(); view.AddChild(Scroll(list)); return view;
    }

    private void ShowPerson(Cultivator person)
    {
        var content = Stack(18); var row = Row(24); var picture = Portrait(portraits, person.Portrait, 196, 294);
        if (!person.Alive && !person.Ascended) picture.Modulate = new Color(.65f, .68f, .65f); row.AddChild(picture); var facts = Stack(10);
        var sect = world!.Sects.SingleOrDefault(s => s.Id == person.SectId);
        if (sect is not null) { var identity = Row(9); identity.AddChild(Emblem(sect, 38)); identity.AddChild(Text(sect.Name, 15, DeepJade)); facts.AddChild(identity); }
        facts.AddChild(Heading(person.Name, 32)); facts.AddChild(Text($"{WorldFactory.RealmNames[person.Realm]} · {person.Role} · {(person.Ascended ? "已飞升" : person.Alive ? "在世" : "故去")}", 16, Gold));
        facts.AddChild(Text($"先天特性：{WorldFactory.InnateTraitNames[person.InnateTrait]}\n修炼 ×{WorldFactory.InnateCultivationMultipliers[person.InnateTrait]:0.0} · 交锋伤害 ×{WorldFactory.InnateCombatMultipliers[person.InnateTrait]:0.0} · 上限{WorldFactory.RealmNames[WorldFactory.InnateRealmLimits[person.InnateTrait]]}\n年龄 {person.AgeMonths / 12} / 寿元 {GameEngine.MaximumAgeMonths(person) / 12} 年", 13, person.InnateTrait >= 3 ? Gold : Muted, true));
        facts.AddChild(Text($"所修功法：{person.Technique}\n资质 {person.Potential} · 忠诚 {person.Loyalty}\n{CultivationText(person)}\n伤势 {person.Injury} · 战功 {person.Kills} · 心性 {person.Temperament}", 14, Paper, true)); row.AddChild(facts); content.AddChild(row);
        content.AddChild(Text(person.Biography, 16, Paper, true));
        var relic = GameEngine.HeldRelic(world, person.Id);
        if (person.Alive && relic is not null)
        {
            var template = PersonalRelics.Find(relic.TemplateId)!;
            var relicRow = Row(12); var path = $"res://Assets/relic-{relic.TemplateId}.png";
            if (ResourceLoader.Exists(path)) relicRow.AddChild(new TextureRect { Name = "personal-relic-icon-" + relic.Id, Texture = GD.Load<Texture2D>(path), CustomMinimumSize = new Vector2(84, 84), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
            relicRow.AddChild(Text($"随身：{relic.Name} · {template.EffectText}\n{(relic.KeepAssigned ? "已保留此物" : "执事自动安排")} · 来历可追溯", 14, Paper, true));
            content.AddChild(relicRow);
            var relicHistory = Row(8);
            relicHistory.AddChild(Button("追溯法宝来历", "person-relic-origin", () => ShowChronicle(relic.OriginEvent)));
            relicHistory.AddChild(Button("查看最近流转", "person-relic-latest-" + relic.Id, () => ShowChronicle(relic.LastEventId)));
            content.AddChild(relicHistory);
        }
        else if (person.Alive) content.AddChild(Text("暂无随身法宝，执事自动安排。", 14, Muted, true));
        else
        {
            foreach (var item in world.History.Where(e => e.RelicChange?.ToHolderId == person.Id).Select(e => e.RelicChange!.RelicId).Distinct())
            {
                var old = world.Relics.SingleOrDefault(r => r.Id == item);
                if (old is null) continue;
                var deathTransfer = world.History.LastOrDefault(e => e.RelicChange is { } change && change.RelicId == item && change.FromHolderId == person.Id && change.ToHolderId != person.Id
                    && e.Causes.Any(id => world.History.Any(cause => cause.Id == id && cause.Kind is "death" or "war" or "ascension")));
                var atDeath = deathTransfer?.RelicChange is { } deathChange
                    ? deathChange.ToHolderId is int recipient ? "交予" + world.People.Single(p => p.Id == recipient).Name
                        : deathChange.ToOwner == "" ? "遗失于" + world.Regions.Single(r => r.Id == deathChange.RegionId).Name : "归入" + world.Sects.Single(s => s.Id == deathChange.ToOwner).Name + "库存"
                    : "未持此物";
                var now = old.HolderId is int holder ? world.People.Single(p => p.Id == holder).Name + "持有" : old.Owner == "" ? "遗失在" + world.Regions.Single(r => r.Id == old.RegionId).Name : "存于" + world.Sects.Single(s => s.Id == old.Owner).Name;
                content.AddChild(Text($"曾持：{old.Name}\n{(person.Ascended ? "飞升" : "故去")}时：{atDeath}；如今：{now}", 13, Muted, true));
                if (deathTransfer is not null) content.AddChild(Button("追溯当时去向", "person-relic-death-" + old.Id, () => ShowChronicle(deathTransfer.Id)));
                content.AddChild(Button("查看最近流转", "person-relic-latest-" + old.Id, () => ShowChronicle(old.LastEventId)));
            }
        }
        if (person.Alive && person.SectId == world.PlayerId && relic?.KeepAssigned == true)
            content.AddChild(Button(GameEngine.IsAway(world, person.Id) ? "交给执事（归山后安排）" : "交给执事", "relic-auto-" + person.Id,
                () => { CloseOverlay(); Command("relic_auto", person.Id.ToString()); }, busy || world.PendingEvent is not null || world.Status != "playing"));
        if (person.Alive && person.SectId == world.PlayerId && person.Focused)
            content.AddChild(Button("赐予法宝", "relic-give-" + person.Id, () => ShowRelicChoices(person), busy || world.PendingEvent is not null || world.Status != "playing" || GameEngine.IsAway(world, person.Id) || world.Player.Exiled));
        if (person.Alive && person.SectId == world.PlayerId && person.Realm == WorldFactory.MaximumRealm)
        {
            var cost = GameEngine.RetentionCost(world, world.Player, person);
            content.AddChild(Text($"大乘满120个月自动飞升：宗门底蕴 +1，全宗生产与修炼 +5%（上限50%），法宝留宗。\n若留世：当前预估每月 {cost.Stones} 灵石 / {cost.Herbs} 灵材，逐年加价；必须同时保住供养军需，缺资自动飞升。", 14, Gold, true));
            content.AddChild(Button(person.RetainInWorld ? "不再留世，顺应飞升" : "申请留世，承担逐月巨额供给", "retention-" + person.Id,
                () => { CloseOverlay(); Command("retention", person.Id.ToString(), person.RetainInWorld ? "off" : "on"); }, busy || world.PendingEvent is not null || world.Status != "playing"));
        }
        var events = world!.History.Where(e => e.Text.Contains(person.Name, StringComparison.Ordinal)).TakeLast(4);
        foreach (var entry in events) content.AddChild(Button($"{Date(entry.Month)} · {entry.Title}", "person-event-" + entry.Id, () => ShowChronicle(entry.Id)));
        var profileScroll = Scroll(content);
        profileScroll.Name = "person-profile-scroll";
        profileScroll.CustomMinimumSize = new Vector2(700, Math.Max(250, GetViewportRect().Size.Y - 180));
        ShowOverlay("人物小传", profileScroll);
    }

    private void ShowRelicChoices(Cultivator person)
    {
        var state = world!; var content = Stack(10);
        content.AddChild(Text("选择一件归山的法宝。目标旧物回库，来源人物的空位由执事安排；不要求双方交换。", 14, Muted, true));
        var list = Stack(6);
        foreach (var relic in state.Relics.Where(r => r.Scope == "personal" && r.Owner == state.PlayerId && PersonalRelics.Find(r.TemplateId) is { } t && t.Tier <= person.Realm && (r.HolderId is null || !GameEngine.IsAway(state, r.HolderId.Value))).OrderBy(r => r.Id))
        {
            var source = relic.HolderId is int holder ? state.People.Single(p => p.Id == holder).Name : "库存";
            list.AddChild(Button($"{relic.Name} · {PersonalRelics.Find(relic.TemplateId)!.EffectText} · {source}", "relic-choice-" + relic.Id,
                () => { CloseOverlay(); Command("relic_give", person.Id.ToString(), relic.Id.ToString()); }));
        }
        if (list.GetChildCount() == 0) list.AddChild(Text("暂无可御使且已归山的法宝。", 14, Muted));
        var scroll = Scroll(list); scroll.CustomMinimumSize = new Vector2(650, 300); content.AddChild(scroll); ShowOverlay("赐予随身法宝", content);
    }

    private void ShowGuardians(Cultivator disciple)
    {
        var state = world!; var content = Stack(12);
        content.AddChild(Text($"为{disciple.Name}安排护道人。须高一大境界、伤势低于30，且未外出、未受其他护道委托。受托者将随弟子历练，不能同时出征；这不等于免死。", 14, Muted, true));
        content.AddChild(Button("不安排护道人", "guardian-none", () => { CloseOverlay(); Command("guardian", disciple.Id.ToString()); }));
        foreach (var guardian in GameEngine.Members(state, state.Player).Where(p => p.Realm > disciple.Realm && p.Injury < 30 && !GameEngine.IsAway(state, p.Id)
            && p.GuardianId is null && !state.People.Any(d => d.Alive && d.GuardianId == p.Id && d.Id != disciple.Id)).OrderByDescending(p => p.Realm).ThenBy(p => p.Id))
            content.AddChild(Button($"{guardian.Name} · {WorldFactory.RealmNames[guardian.Realm]}", "assign-guardian-" + guardian.Id,
                () => { CloseOverlay(); Command("guardian", disciple.Id.ToString(), guardian.Id.ToString()); }));
        ShowOverlay("护道人 · 同行守护", content);
    }

    private Control DiplomacyView()
    {
        var state = world!; var view = Section("诸宗风云", $"各宗依战略自行经营、交涉与征伐。「远交近攻」经营五年盟约，最终仍须统一其余{state.Sects.Count - 1}宗道统。");
        var list = Stack(12);
        foreach (var sect in state.Sects.Where(s => s.Id != state.PlayerId).OrderByDescending(s => !s.Eliminated))
        {
            var card = Stack(12); var row = Row(16); row.AddChild(Emblem(sect, 60));
            var leader = state.People.SingleOrDefault(p => p.Id == sect.LeaderId && p.Alive);
            if (leader is not null) row.AddChild(Portrait(portraits, leader.Portrait, 75, 90)); var info = Stack(5);
            var relation = state.Player.Relations.GetValueOrDefault(sect.Id); var truce = state.Player.Truces.GetValueOrDefault(sect.Id) - state.Month;
            info.AddChild(Heading(sect.Name + (sect.Eliminated ? " · 道统已断" : sect.Exiled ? " · 流亡" : ""), 25, new Color(sect.Color).Darkened(.4f)));
            if (leader is not null) info.AddChild(Text($"现任掌门 · {leader.Name} · {WorldFactory.RealmNames[leader.Realm]}", 12, Muted));
            info.AddChild(Text($"关系 {relation:+0;-0;0}  ·  {(truce > 0 ? $"盟约余 {truce} 月" : "无盟约")}  ·  策略：{Policy(sect.Policy)}", 13, Gold));
            info.AddChild(Text($"战力 {GameEngine.Power(state, sect)}  /  在世门徒 {GameEngine.Members(state, sect).Count}  /  领地 {GameEngine.Lands(state, sect).Count}\n灵石 {sect.Stones} · 灵材 {sect.Herbs} · 道藏 {sect.Doctrine} · 军阵 {sect.Training} · 安定 {sect.Stability}", 13, Muted, true)); row.AddChild(info);
            var actions = Stack(8); actions.SizeFlagsHorizontal = SizeFlags.Fill;
            actions.AddChild(Button("查看门下人物", "people-" + sect.Id, () => { selectedPersonSect = sect.Id; Navigate("people"); }));
            actions.AddChild(Button("议政计划与记忆", "agent-memory-" + sect.Id, () => ShowAgentMemory(sect.Id))); row.AddChild(actions); card.AddChild(row);
            card.AddChild(Text(sect.Eliminated ? "此宗已无法重建，不再参与世界演进。" : $"战略意图：{sect.Intent}", 14, Muted, true)); list.AddChild(Card(card));
        }
        view.AddChild(Scroll(list)); return view;
    }

    private void ShowAgentMemory(string sectId)
    {
        paused = true; var content = Stack(14); var state = world!;
        var sect = state.Sects.Single(s => s.Id == sectId); var memory = state.AgentMemories.SingleOrDefault(m => m.SectId == sectId);
        content.AddChild(Heading(sect.Name + " · 议政记忆", 27, Gold));
        if (memory?.Plan is not { } plan)
        { content.AddChild(Text("此宗尚未建立智能会话。离线模式依规则发展；启用智能世界并推进后才会制定模型计划。", 15, Muted, true)); ShowOverlay("议政计划与记忆", content); return; }
        content.AddChild(Text($"上次议政：{Date(memory.LastReviewMonth)}\n下次例行复盘：{Date(memory.NextReviewMonth)}；重大变化可提前唤醒\n触发原因：{memory.LastTrigger}\n最近输入：{memory.ContextTokens:N0} tokens · compact {memory.Compactions} 次\n公开会话 {memory.Conversation.Count} 条 · 归档 {memory.Archive.Count} 条", 14, Muted, true));
        content.AddChild(Heading(plan.Goal, 24));
        content.AddChild(Text($"{Policy(plan.Policy)} · 目标：{state.Regions.SingleOrDefault(r => r.Id == plan.Target)?.Name ?? "暂不出征"}\n{plan.Reason}", 16, Paper, true));
        foreach (var stage in plan.Stages) content.AddChild(Text("· " + stage, 15, Paper, true));
        content.AddChild(Text("重新考虑条件：" + plan.ReconsiderWhen, 14, Gold, true));
        if (memory.Summary.Length > 0) { content.AddChild(Heading("长期记忆摘要", 21)); content.AddChild(Text(memory.Summary, 14, Muted, true)); }
        content.AddChild(Heading("最近议政回顾", 21));
        var reviews = memory.Archive.Concat(memory.Conversation).Reverse()
            .Where(e => e.Role == "assistant").Select(e => (Entry: e, Plan: ReadPublicPlan(e)))
            .Where(e => e.Plan is not null).Take(3).Reverse().ToArray();
        foreach (var review in reviews)
        {
            var previous = review.Plan!;
            var target = state.Regions.SingleOrDefault(r => r.Id == previous.Target)?.Name ?? "暂无出征目标";
            content.AddChild(Text($"{Date(review.Entry.Month)} · {Policy(previous.Policy)} · {target}", 12, Gold));
            content.AddChild(Text(previous.Goal + "\n" + previous.Reason, 14, Paper, true));
        }
        if (reviews.Length == 0) content.AddChild(Text("尚无可展示的历史议政，当前计划见上方。", 14, Muted, true));
        content.AddChild(Text("界面仅展示公开计划摘要；局势快照与工具记录保存在本地，供宗门延续记忆，不在此回显原始数据包。", 12, Muted, true));
        ShowOverlay("议政计划与记忆", content);
    }

    private static SectAgentPlan? ReadPublicPlan(AgentConversationEntry entry)
    {
        const string prefix = "submit_strategy 公开提交：";
        if (!entry.Text.StartsWith(prefix, StringComparison.Ordinal)) return null;
        try
        {
            var plan = JsonSerializer.Deserialize<SectAgentPlan>(entry.Text[prefix.Length..], new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return plan is { Goal.Length: > 0 and <= 500, Reason.Length: > 0 and <= 300 } && WorldFactory.Policies.Contains(plan.Policy) ? plan : null;
        }
        catch (JsonException) { return null; }
    }

    private Control TreasuryView()
    {
        var state = world!; var view = Section("道统宝库", "道藏与军阵影响长期修行和实战；遗宝随探索入世，所有来源都可在史书追溯。");
        var header = Row(20); header.AddChild(Metric("本宗道藏", $"{state.Player.Doctrine} / 8", Gold)); header.AddChild(Metric("传功军阵", $"{state.Player.Training} / 10", Jade));
        header.AddChild(Metric("本宗遗宝", state.Relics.Count(r => r.Owner == state.PlayerId).ToString())); header.AddChild(Metric("遗宝战力加成", "+" + state.Relics.Where(r => r.Owner == state.PlayerId).Sum(r => r.Power))); view.AddChild(Card(header));
        var techniques = Stack(10); techniques.AddChild(Heading("宗门功法", 24));
        foreach (var group in GameEngine.Members(state, state.Player).GroupBy(p => p.Technique)) techniques.AddChild(Text($"{group.Key}  /  修习者 {group.Count()} 人", 15)); view.AddChild(Card(techniques));
        var list = Stack(); list.AddChild(Heading("天下遗宝", 24));
        foreach (var relic in state.Relics.OrderByDescending(r => r.FoundMonth))
        {
            var owner = state.Sects.SingleOrDefault(s => s.Id == relic.Owner); var content = Stack(8);
            var location = relic.HolderId is int holder ? state.People.Single(p => p.Id == holder).Name + "随身" : owner is null ? "遗失于" + state.Regions.Single(r => r.Id == relic.RegionId).Name : owner.Name + "库存";
            content.AddChild(Heading(relic.Name, 23, Gold)); content.AddChild(Text($"{location} · {(relic.Scope == "sect" ? "镇宗战力 +" + relic.Power : PersonalRelics.Find(relic.TemplateId)!.EffectText)} · {Date(relic.FoundMonth)}入世", 13, owner is null ? Muted : new Color(owner.Color)));
            content.AddChild(Text(relic.Effect, 15, Paper, true)); content.AddChild(Button("追溯来源", "relic-origin-" + relic.Id, () => ShowChronicle(relic.OriginEvent))); list.AddChild(Card(content));
        }
        if (state.Relics.Count == 0) list.AddChild(Text("尚无遗宝入世。门人会自动下山探访机缘，关键秘境也会带来掌门抉择。", color: Muted, wrap: true));
        view.AddChild(Scroll(list)); return view;
    }

    private Control HistoryView()
    {
        var state = world!; var view = Section("青岚史书", "不是重新开局，而是同一个世界。战役、突破、亡者与重建皆有记录，因果可以逐层追索。");
        var filters = Row(8);
        foreach (var entry in new[] { ("all", "天下纪事"), ("player", "本宗兴衰"), ("battle", "战役与亡者"), ("cultivation", "修行与机缘") })
            filters.AddChild(Button(entry.Item2, "history-" + entry.Item1, () => { historyFilter = entry.Item1; Render(); }, primary: historyFilter == entry.Item1));
        view.AddChild(filters); var list = Stack(12);
        var entries = state.History.Where(e => historyFilter switch { "player" => e.Sects.Contains(state.PlayerId), "battle" => e.Kind is "war" or "campaign" or "death" or "exile" or "rebirth", "cultivation" => e.Kind is "cultivation" or "relic" or "relic_discovery" or "explore" or "journey", _ => true }).Reverse().Take(historyVisible);
        foreach (var entry in entries)
        {
            var content = Stack(8); var heading = Row(); var title = Heading(entry.Title, 23); title.SizeFlagsHorizontal = SizeFlags.ExpandFill; heading.AddChild(title); heading.AddChild(Text(Date(entry.Month), 12, Gold)); content.AddChild(heading);
            content.AddChild(Text(entry.Text, 15, Paper, true)); if (entry.Delta != "") content.AddChild(Text(entry.Delta, 13, Jade, true));
            if (entry.Causes.Count > 0) content.AddChild(Button($"追溯 {entry.Causes.Count} 条前因", "chronicle-" + entry.Id, () => ShowChronicle(entry.Id)));
            list.AddChild(Card(content));
        }
        if (list.GetChildCount() == 0) list.AddChild(Text("尚无此类纪事。", color: Muted));
        if (state.History.Count > historyVisible) list.AddChild(Button("翻阅更早的纪事", "history-more", () => { historyVisible += 120; Render(); }));
        view.AddChild(Scroll(list)); return view;
    }

    private void ShowChronicle(int id)
    {
        var entry = world!.History.Single(e => e.Id == id); var content = Stack(16);
        content.AddChild(Text(Date(entry.Month), 13, Gold)); content.AddChild(Heading(entry.Title, 30)); content.AddChild(Text(entry.Text, 16, Paper, true));
        if (entry.Delta != "") content.AddChild(Text(entry.Delta, 14, Jade, true));
        if (entry.RelicChange is { } change)
        {
            var relic = world.Relics.Single(r => r.Id == change.RelicId);
            string Place(int? holder, string owner) => holder is int personId
                ? world.People.Single(p => p.Id == personId).Name + (owner == "" ? "（散修）" : "（" + world.Sects.Single(s => s.Id == owner).Name + "）")
                : owner == "" ? "无持有人、无宗门归属" : world.Sects.Single(s => s.Id == owner).Name + "库存";
            content.AddChild(Text($"{relic.Name} · 当时流转\n从：{Place(change.FromHolderId, change.FromOwner)}\n至：{Place(change.ToHolderId, change.ToOwner)}\n地域：{world.Regions.Single(r => r.Id == change.RegionId).Name}", 14, Paper, true));
            if (entry.Id != relic.LastEventId) content.AddChild(Button("查看此物最近流转", "chronicle-relic-latest-" + relic.Id, () => ShowChronicle(relic.LastEventId)));
        }
        foreach (var causeId in entry.Causes)
        {
            var cause = world.History.Single(e => e.Id == causeId); content.AddChild(Button($"前因：{Date(cause.Month)} · {cause.Title}", "cause-" + causeId, () => ShowChronicle(causeId)));
        }
        ShowOverlay("因果纪事", content);
    }

    private void ShowSettings()
    {
        paused = true; var content = Stack(16);
        model = session.ModelStatus();
        content.AddChild(Text($"Anthropic Messages · {(model.Configured ? "配置有效" : "尚未配置")}\n思考等级：{model.Effort ?? "接口默认"}\n独立宗门会话：每宗 {model.ContextWindowTokens:N0} tokens · 达{model.CompactionThreshold:P0}（{model.ContextWindowTokens * model.CompactionThreshold:N0}）compact\n本局生成请求：{world?.ModelRequests ?? 0} 次 · 已报告 {world?.ModelTokens ?? 0:N0} tokens\n另有上下文计数请求：{world?.ModelContextChecks ?? 0} 次 · 并发上限 {model.MaxConcurrentRequests}", 16, Paper, true));
        content.AddChild(Text($"宗门Agent保留独立公开会话与计划；重大事件、目标失效或每{model.StrategyReviewMonths / 12.0:0.#}年复盘时在后台唤醒，最多{model.MaxConcurrentRequests}个同时运行。等待期间沿用既定战略，不阻塞时间；返回时校验最新目标。上下文经count_tokens计数，输入加预留输出达阈值时compact，原公开记录仍存档，不保存隐藏思考。真实调用及重试可能持续计费，无总次数额度；失败自动重试3次，仍失败会明确切换离线并记入史书。手动恢复智能世界才重新联网。", 14, Muted, true));
        var files = Row(); files.AddChild(Button("打开本地配置目录", "open-config", () => OS.ShellOpen(Path.Combine(root, "config")))); files.AddChild(Button("掌门手札", "help", ShowHelp)); content.AddChild(files);
        if (world is not null && world.Status == "playing")
        {
            var modes = Row(); modes.AddChild(Button("启用智能世界", "mode-llm", () => { CloseOverlay(); Command("mode", option: "llm"); }, busy || !model.Configured || world.Mode == "llm"));
            modes.AddChild(Button("明确切换为离线世界", "mode-rules", () => { CloseOverlay(); Command("mode", option: "rules"); }, busy || world.Mode == "rules")); content.AddChild(modes);
        }
        if (world is not null && !showTitle) content.AddChild(Button("返回标题 · 当前进度已自动保存", "return-title", () => { CloseOverlay(); showTitle = true; page = "map"; Render(); }, busy));
        content.AddChild(Text("原生 Godot .NET 桌面客户端  /  无浏览器、无 localhost 服务\n空格暂停或继续；Esc 关闭面板；Ctrl+S 写入手动档一。", 12, Muted, true));
        ShowOverlay("世界与模型", content);
    }
}
