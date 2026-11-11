namespace InfiniteDao.Core.Game;

public static partial class GameEngine
{
    private static GameEvent GenerateEvent(GameState world)
    {
        var type = world.Roll(0, 11);
        var e = new GameEvent { Id = world.NewId(), Type = "council" + type };
        (string Title, string Text, List<EventChoice> Choices) definition = type switch
        {
            0 => ("山门外，少年叩首", "一位灵根出众的少年跪在山门外。他家乡遭劫，愿以一生报答庇护之恩。长老提议收徒，也有人担忧会惹来旧怨。", [C("accept", "收入亲传", "新增一名高资质筑基弟子，威望 +8。", 120), C("gift", "赠资助其另寻仙路", "安定 +8，威望 +4。", 40), C("decline", "山门暂闭", "不消耗资源，安定 -3。")]),
            1 => ("灵脉异动", "灵脉深处传来潮鸣，矿工发现一条未曾开采的支脉。深入开采可获得灵石，但护脉阵需要额外投入。", [C("mine", "布阵开采", "灵石净收益 +230，首府发展 +1（上限 5）。", 120), C("preserve", "封存温养", "灵材 +90，全体修为 +8。"), C("study", "参悟灵潮", "道藏 +1（上限 8）。", 160, 40)]),
            2 => ("两峰争席", "两位长老因修炼资源分配发生争执，门下弟子各执一词。此事若处置失当，恐伤宗门根基。", [C("equal", "公议分配，补足所需", "安定 +18。", 130), C("discipline", "立规矩，禁私争", "安定 +6，军阵 +1。", 60), C("ignore", "让两峰自行解决", "节省资源，安定 -16。")]),
            3 => ("古战场的残卷", "游历弟子带回半页功法残卷。字迹与本宗道藏隐隐呼应，似可推演出一条新路。", [C("research", "闭门推演", "道藏 +1，威望 +8。", 180, 45), C("sell", "交予坊市", "灵石 +250。"), C("teach", "抄录分授弟子", "全体修为 +20。", 50)]),
            4 => ("灵田染病", "连日阴雨，灵田受了浊气。丹师建议焚去病株，也有弟子提出以灵药净化土壤。", [C("cleanse", "以丹净土", "安定 +10，灵材 +100。", 100, 30), C("burn", "焚株止损", "灵材 -30，避免扩大。"), C("risk", "任其自愈", "灵材 -80，安定 -8。")]),
            5 => ("夜访的使者", "邻宗使者携玉简前来，希望缓和近年的边境纷争。接受善意，可以换取一段休整时间。", [C("peace", "以礼相待", "所有存续宗门关系 +12。", 90), C("intel", "借机探听虚实", "情报 +1，灵材 +30。", 50), C("reject", "闭门谢客", "威望 +5，所有关系 -8。")]),
            6 => ("天才与旧法", "一位年轻弟子认为旧功法存在破绽，希望自行推演。他的眼睛亮得让长老不安。", [C("support", "准其开新道", "一名弟子资质 +15，修为 +45；道藏 +1。", 150, 50), C("steady", "先打牢根基", "全体修为 +15。", 60), C("suppress", "严禁离经叛道", "安定 +4，但一名弟子忠诚 -20。")]),
            7 => ("兽潮逼近", "山林中的妖兽向村落迁移，凡人点起烽火求救。出手会有消耗，但也能赢得人心。", [C("rescue", "派遣弟子护民", "威望 +18，新增 2 名弟子，部分修士轻伤。", 90, 20), C("array", "布阵驱散", "威望 +10，首府阵法 +1。", 160), C("close", "暂闭山门", "安定 -10，不消耗物资。")]),
            8 => ("行商的旧匣", "行商带来一只封有古纹的旧匣。他不知道其中是什么，只说出自覆灭已久的洞府。", [C("buy", "买下古匣", "必得一件宗门法宝，随机战力 +25～55。", 200), C("herbs", "只换取丹材", "灵材 +120。", 90), C("pass", "不涉未知因果", "不改变资源。")]),
            9 => ("长老请辞", "一位长老因旧伤生出归隐之意。留下他需要灵丹与诚意，顺其心意则可换来临别传功。", [C("keep", "诚心挽留", "全体伤势 -25，安定 +10。", 100, 50), C("teach", "尊其志，行传功礼", "全体修为 +25，威望 +5。", 60), C("release", "清风送别", "安定 -5，灵材 +20。")]),
            10 => ("论道大会", "各宗约定在云河渡论道。此行或可扬名，也可能暴露本宗的根底。", [C("attend", "遣精锐赴会", "威望 +20，全体修为 +12。", 120), C("trade", "带丹材换灵石", "灵石 +240。", 0, 70), C("quiet", "留山闭关", "全体修为 +10。")]),
            _ => ("岁时祭祖", "山门悬灯，弟子请掌门主持祭祖。有人怀念战死的同门，也有人在灯下写下自己的长生愿。", [C("ceremony", "为先辈立碑", "安定 +15，全体忠诚 +10。", 100), C("reward", "犒赏全宗", "安定 +10，威望 +8。", 60, 20), C("simple", "清茶一盏，铭记初心", "安定 +4。")])
        };
        e.Title = definition.Title; e.Text = definition.Text; e.Choices = definition.Choices;
        return e;
    }

    private static EventChoice C(string id, string label, string description, int stones = 0, int herbs = 0) => new() { Id = id, Label = label, Description = description, Stones = stones, Herbs = herbs };

    private static void Choose(GameState world, string choiceId)
    {
        var e = world.PendingEvent ?? throw new GameRuleException("当前没有待处理议事。");
        var choice = e.Choices.SingleOrDefault(c => c.Id == choiceId) ?? throw new GameRuleException("选项已失效。");
        var sect = world.Player;
        if (choice.Stones > sect.Stones || choice.Herbs > sect.Herbs) throw new GameRuleException("资源不足，无法选择此项。请选择不消耗资源的方案。");
        world.PendingEvent = null;
        if (e.Type == "attack")
        {
            if (choiceId == "cancel") return;
            StartCampaign(world, sect, world.Regions.Single(r => r.Id == e.Target), choiceId); world.CommandsLeft--;
            return;
        }
        if (e.Type == "defense")
        {
            var attacker = world.Sects.Single(s => s.Id == e.Actor); var region = world.Regions.Single(r => r.Id == e.Target);
            if (choiceId == "retreat")
            {
                region.Owner = attacker.Id;
                var id = Log(world, "war", "退一步，留薪火", $"{sect.Name}主动撤离{region.Name}，保存全部门人。", [sect.Id, attacker.Id], $"领地转移至{attacker.Name}");
                if (Lands(world, sect).Count == 0) LoseSect(world, sect, attacker, id);
                else if (sect.Capital == region.Id) sect.Capital = Lands(world, sect).First().Id;
            }
            else
            {
                Spend(sect, choice.Stones, choice.Herbs);
                StartCampaign(world, attacker, region, "balanced", choiceId == "sacrifice" ? 1.45 : 1);
            }
            return;
        }
        Spend(sect, choice.Stones, choice.Herbs);
        if (e.Type == "inauguration")
        {
            sect.Policy = choiceId;
            if (choiceId == "cultivation") { sect.Doctrine++; sect.Herbs += 100; }
            if (choiceId == "prosperity") { sect.Stones += 350; sect.Stability = Math.Min(100, sect.Stability + 10); }
            if (choiceId == "expansion") { sect.Training++; for (var i = 0; i < 3; i++) WorldFactory.NewCultivator(world, sect.Id); var r = Lands(world, sect).First(); r.Population -= 3; }
        }
        else if (e.Type == "ember")
        {
            if (choiceId == "hidden") { foreach (var p in Members(world, sect)) p.Injury = 0; sect.Stability = Math.Min(100, sect.Stability + 20); }
            if (choiceId == "wander") { sect.Stones += 300; sect.Herbs += 100; }
            if (choiceId == "revenge") { sect.Training = Math.Min(10, sect.Training + 1); Boost(world, sect, 35); }
        }
        else if (e.Type.StartsWith("council", StringComparison.Ordinal)) ApplyCouncil(world, sect, e.Type, choiceId);
        Log(world, "decision", e.Title, $"掌门选择「{choice.Label}」。{choice.Description}", [sect.Id], $"支付灵石 {choice.Stones}、灵材 {choice.Herbs}");
        sect.Stability = Math.Clamp(sect.Stability, 0, 100); sect.Herbs = Math.Max(0, sect.Herbs);
    }

    private static void ApplyCouncil(GameState w, Sect s, string type, string c)
    {
        var capital = Lands(w, s).FirstOrDefault(r => r.Id == s.Capital) ?? Lands(w, s).FirstOrDefault();
        switch (type)
        {
            case "council0":
                if (c == "accept") { var p = WorldFactory.NewCultivator(w, s.Id); p.Potential = 96; p.Realm = 1; p.Role = "亲传"; if (capital is not null) capital.Population--; s.Renown += 8; }
                else if (c == "gift") { s.Stability += 8; s.Renown += 4; } else s.Stability -= 3;
                break;
            case "council1":
                if (c == "mine") { s.Stones += 350; if (capital is not null) capital.Development = Math.Min(5, capital.Development + 1); }
                else if (c == "preserve") { s.Herbs += 90; Boost(w, s, 8); } else s.Doctrine = Math.Min(8, s.Doctrine + 1);
                break;
            case "council2":
                if (c == "equal") s.Stability += 18; else if (c == "discipline") { s.Stability += 6; s.Training = Math.Min(10, s.Training + 1); } else s.Stability -= 16;
                break;
            case "council3":
                if (c == "research") { s.Doctrine = Math.Min(8, s.Doctrine + 1); s.Renown += 8; } else if (c == "sell") s.Stones += 250; else Boost(w, s, 20);
                break;
            case "council4":
                if (c == "cleanse") { s.Stability += 10; s.Herbs += 100; } else if (c == "burn") s.Herbs -= 30; else { s.Herbs -= 80; s.Stability -= 8; }
                break;
            case "council5":
                if (c == "peace" || c == "reject") { foreach (var other in w.Sects.Where(x => !x.Eliminated && x.Id != s.Id)) ChangeRelation(s, other, c == "peace" ? 12 : -8); if (c == "reject") s.Renown += 5; }
                else { s.Intel = Math.Min(5, s.Intel + 1); s.Herbs += 30; }
                break;
            case "council6":
                if (c == "support") { var p = Members(w, s).OrderByDescending(x => x.Potential).First(); p.Potential = Math.Min(100, p.Potential + 15); p.Progress = Math.Min(120, p.Progress + 45); s.Doctrine = Math.Min(8, s.Doctrine + 1); }
                else if (c == "steady") Boost(w, s, 15); else { s.Stability += 4; var p = Members(w, s).Last(); p.Loyalty = Math.Max(0, p.Loyalty - 20); }
                break;
            case "council7":
                if (c == "rescue") { s.Renown += 18; for (var i = 0; i < 2; i++) WorldFactory.NewCultivator(w, s.Id); if (capital is not null) capital.Population -= 2; foreach (var p in Members(w, s).Take(3)) p.Injury = Math.Min(90, p.Injury + 12); }
                else if (c == "array") { s.Renown += 10; if (capital is not null) capital.Fort = Math.Min(5, capital.Fort + 1); } else s.Stability -= 10;
                break;
            case "council8":
                if (c == "buy") { var p = w.Roll(25, 55); var id = Log(w, "relic", "古匣开封", "一枚「归墟道印」认宗而归。", [s.Id], $"战力 +{p}"); w.Relics.Add(new Relic { Id = w.NewId(), Owner = s.Id, Name = "归墟道印", Power = p, Effect = "气运镇宗，提升全宗战力", OriginEvent = id, FoundMonth = w.Month }); }
                else if (c == "herbs") s.Herbs += 120;
                break;
            case "council9":
                if (c == "keep") { foreach (var p in Members(w, s)) p.Injury = Math.Max(0, p.Injury - 25); s.Stability += 10; }
                else if (c == "teach") { Boost(w, s, 25); s.Renown += 5; } else { s.Stability -= 5; s.Herbs += 20; }
                break;
            case "council10":
                if (c == "attend") { s.Renown += 20; Boost(w, s, 12); } else if (c == "trade") s.Stones += 240; else Boost(w, s, 10);
                break;
            case "council11":
                if (c == "ceremony") { s.Stability += 15; foreach (var p in Members(w, s)) p.Loyalty = Math.Min(100, p.Loyalty + 10); }
                else if (c == "reward") { s.Stability += 10; s.Renown += 8; } else s.Stability += 4;
                break;
        }
    }

    private static void Boost(GameState world, Sect sect, int amount)
    {
        foreach (var p in Members(world, sect)) p.Progress = Math.Min(120, p.Progress + amount);
    }
}
