namespace InfiniteDao.Core.Game;

public static partial class WorldFactory
{
    public const int PortraitCount = 50;
    public const int MaximumRealm = 7;
    public static readonly string[] RealmNames = ["炼气", "筑基", "金丹", "元婴", "化神", "分神", "合体", "大乘"];
    public static readonly int[] Lifespans = [28, 36, 48, 62, 78, 94, 110, 130];
    public static readonly int[] MinimumRealmMonths = [6, 12, 24, 36, 48, 60, 72, 0];
    public static readonly string[] InnateTraitNames = ["凡体", "通灵之体", "异灵根", "天灵根", "先天道体"];
    public static readonly int[] InnateRealmLimits = [2, 3, 4, 6, 7];
    public static readonly double[] InnateCultivationMultipliers = [.8, 1, 1.4, 2, 2.8];
    public static readonly double[] InnateCombatMultipliers = [1, 1.1, 1.3, 1.6, 2];
    public static int InnateTraitFromRoll(int roll) => roll < 55 ? 0 : roll < 80 ? 1 : roll < 92 ? 2 : roll < 98 ? 3 : 4;
    public static readonly string[] Policies = ["balanced", "prosperity", "cultivation", "expansion", "diplomacy"];
    public static readonly string[] PolicyNames = ["均衡发展", "休养生息", "潜心修炼", "开疆拓土", "远交近攻"];

    public static List<Sect> Templates() =>
    [
        new() { Id = "qingyun", Name = "青云剑宗", Color = "#71d5c2", Sigil = "剑", Portrait = 0, Capital = "r06", Trait = "sword", Motto = "一剑问道，万山听风", Personality = "守诺重义，重视传承，但不惧争斗" },
        new() { Id = "chixia", Name = "赤霞宗", Color = "#f28c72", Sigil = "焰", Portrait = 1, Capital = "r04", Trait = "fire", Motto = "赤焰不灭，山河可争", Personality = "好胜果决，积极扩张，珍视高阶战力" },
        new() { Id = "baicao", Name = "百草谷", Color = "#bad77c", Sigil = "药", Portrait = 2, Capital = "r13", Trait = "herb", Motto = "草木有灵，生生不息", Personality = "温厚谨慎，善于丹药与资源积累" },
        new() { Id = "tiangong", Name = "天工门", Color = "#eac37d", Sigil = "器", Portrait = 3, Capital = "r11", Trait = "forge", Motto = "炉火锻道，百炼成锋", Personality = "务实理性，重视建设、财富与可靠盟友" },
        new() { Id = "jingshui", Name = "镜水宗", Color = "#8ebfeb", Sigil = "澜", Portrait = 4, Capital = "r14", Trait = "water", Motto = "水无常形，道有千流", Personality = "深谋远虑，擅长外交、情报与战略迂回" },
        new() { Id = "xuanshuang", Name = "玄霜门", Color = "#c4b1e7", Sigil = "霜", Portrait = 5, Capital = "r02", Trait = "ice", Motto = "千山寂雪，一念长生", Personality = "孤高坚毅，偏爱闭关与精英修士" },
        new() { Id = "leiting", Name = "雷霆阁", Color = "#9d99e3", Sigil = "雷", Portrait = 6, Capital = "r17", Trait = "thunder", Motto = "惊雷开道，正气凌霄", Personality = "刚正迅决，重视战阵与边境威慑" },
        new() { Id = "jinchan", Name = "金禅寺", Color = "#d2ab6b", Sigil = "禅", Portrait = 7, Capital = "r18", Trait = "body", Motto = "心如明镜，身镇千山", Personality = "沉稳慈悲，珍惜门人，偏重休养与持久防守" },
        new() { Id = "xingluo", Name = "星罗宫", Color = "#77b8d2", Sigil = "星", Portrait = 8, Capital = "r19", Trait = "star", Motto = "观天知变，星罗万象", Personality = "审慎求知，重道藏积累与长远修行" },
        new() { Id = "youhuang", Name = "幽篁楼", Color = "#bb8eaf", Sigil = "影", Portrait = 9, Capital = "r20", Trait = "shadow", Motto = "竹影无声，进退有度", Personality = "机敏隐忍，依情报择机行事，善于交涉" }
    ];

    public static string TraitDescription(string trait) => trait switch
    {
        "sword" => "剑修战力 +12%", "fire" => "战力 +8%", "herb" => "灵材产出 +40%",
        "forge" => "每月额外 15 灵石", "water" => "外交与情报先机", "ice" => "修炼每月额外 +2",
        "thunder" => "战力 +10%", "body" => "山门疗伤每月额外 +2", "star" => "修炼额外 +1 · 开局道藏 +1",
        "shadow" => "开局情报三级", _ => "道统绵延"
    };

    public static GameState Create(string playerId, int seed, string mode, string difficulty, bool compact = false)
    {
        var templates = Templates();
        if (!templates.Any(s => s.Id == playerId)) throw new GameRuleException("请选择一个有效宗门。");
        if (mode is not ("llm" or "rules")) throw new GameRuleException("请选择智能世界或离线世界。");
        if (difficulty is not ("story" or "normal" or "hard")) throw new GameRuleException("难度无效。");
        var state = new GameState { PlayerId = playerId, Seed = seed, RandomState = unchecked((uint)seed) | 1, Mode = mode, Difficulty = difficulty, Sects = templates, AutoStewardship = !compact };
        if (compact)
        {
        state.MapRows = 5;
        string[] names = ["霜岭雪原", "寒潭", "北境矿口", "赤霞山", "松风谷", "青云山", "落霞隘", "赤岩丘", "万药泽", "云河渡", "铸剑岭", "黑石矿", "长生谷", "镜湖", "浮萍洲", "古战场", "雷鸣崖", "金莲山", "观星台", "幽篁谷"];
        string[] owners = ["xuanshuang", "xuanshuang", "chixia", "chixia", "qingyun", "qingyun", "", "chixia", "baicao", "", "tiangong", "tiangong", "baicao", "jingshui", "jingshui", "", "leiting", "jinchan", "xingluo", "youhuang"];
        int[] pops = [2000, 3000, 2000, 4000, 6000, 5000, 2000, 3000, 5000, 8000, 3000, 2000, 6000, 5000, 4000, 1000, 4000, 4500, 3500, 4000];
        int[] mines = [2, 1, 5, 2, 1, 2, 1, 4, 0, 2, 2, 5, 0, 1, 1, 3, 3, 2, 2, 1];
        int[] herbs = [1, 3, 1, 2, 4, 3, 1, 1, 6, 3, 2, 0, 5, 4, 4, 1, 2, 4, 3, 4];
        string[] terrains = ["雪原", "寒潭", "灵矿", "灵山", "幽谷", "灵山", "关隘", "丘陵", "药泽", "渡口", "灵山", "矿脉", "药谷", "湖泊", "水洲", "遗迹", "灵山", "灵山", "灵山", "幽谷"];
        for (var i = 0; i < names.Length; i++)
        {
            var region = new Region { Id = $"r{i + 1:00}", Index = i, Name = names[i], Owner = owners[i], Population = pops[i], Mine = mines[i], HerbYield = herbs[i], Spirit = 2 + herbs[i] / 2, Terrain = terrains[i], Description = $"{names[i]}的{terrains[i]}孕育着灵气与机缘。灵矿产出 {mines[i]} 阶，灵材恢复 {herbs[i]} 阶。" };
            for (var j = 0; j < names.Length; j++)
                if (Math.Abs(i / 4 - j / 4) + Math.Abs(i % 4 - j % 4) == 1) region.Neighbors.Add($"r{j + 1:00}");
            state.Regions.Add(region);
        }
        }
        else PopulateVastRegions(state);
        string[] leaders = ["沈青衡", "厉长焰", "顾清芷", "岳百炼", "江映月", "苏凝霜", "裴惊雷", "释明照", "谢观星", "宁竹隐"];
        foreach (var sect in state.Sects)
        {
            sect.Policy = sect.Trait switch { "fire" or "thunder" => "expansion", "ice" or "star" => "cultivation", "body" => "prosperity", "shadow" => "diplomacy", _ => "balanced" };
            foreach (var other in state.Sects.Where(s => s.Id != sect.Id)) sect.Relations[other.Id] = 0;
            var count = sect.Id == playerId ? 16 : compact ? 13 : 24;
            if (!compact && sect.Id != playerId) { sect.Training = 2; sect.Doctrine = 2; sect.Stones = 950; }
            if (sect.Trait == "star") sect.Doctrine++;
            if (sect.Trait == "shadow") sect.Intel = 3;
            for (var n = 0; n < count; n++)
            {
                var person = NewCultivator(state, sect.Id);
                person.Realm = n == 0 ? 2 : n < 4 ? 1 : 0;
                person.RealmMonths = n == 0 ? state.Roll(48, 120) : n < 4 ? state.Roll(12, 36) : state.Roll(0, 8);
                person.AgeMonths = n == 0 ? state.Roll(34, 42) * 12 : n < 4 ? state.Roll(25, 32) * 12 : state.Roll(18, 24) * 12;
                if (n == 0)
                {
                    person.Name = leaders[sect.Portrait]; person.Role = "掌门"; person.Potential = 80 + state.Roll(0, 15);
                    person.InnateTrait = Math.Max(1, person.InnateTrait);
                    person.Biography = $"承继{sect.Name}道统，立誓守护门下弟子。"; sect.LeaderId = person.Id;
                }
                else if (n < 4) person.Role = "长老";
            }
        }
        state.Sects[0].Relations["chixia"] = -25; state.Sects[1].Relations["qingyun"] = -25;
        var waterSect = state.Sects.Single(s => s.Trait == "water");
        waterSect.Intel = 2;
        foreach (var other in state.Sects.Where(s => s.Id != waterSect.Id)) GameEngine.ChangeRelation(waterSect, other, 10);
        state.Player.Stones = difficulty == "story" ? 1300 : difficulty == "hard" ? 650 : 950;
        state.Player.Herbs = difficulty == "story" ? 350 : 240;
        GameEngine.Log(state, "founding", "青岚历启", $"十宗并立，山河未定。你接过{state.Player.Name}的宗门印信。终有一日，天下道统将归于一门。", [playerId]);
        state.PendingEvent = new GameEvent
        {
            Id = state.NewId(), Type = "inauguration", Title = "新掌门，第一道宗令", Eyebrow = "开宗之议",
            Text = "山门钟声响起，长老与弟子齐聚殿前。你将以何种道路，引领宗门走过这场漫长的兴亡？此选择决定起始优势，发展策略此后仍可随时调整。",
            Choices =
            [
                new() { Id = "cultivation", Label = "以道为本", Description = "传功堂等级 +1，获得 100 灵材；潜心修炼。" },
                new() { Id = "prosperity", Label = "先富山门", Description = "获得 350 灵石，宗门安定 +10；休养生息。" },
                new() { Id = "expansion", Label = "执剑开疆", Description = "新增 3 名弟子，军阵等级 +1；开疆拓土。" }
            ]
        };
        return state;
    }

    public static Cultivator NewCultivator(GameState state, string sectId)
    {
        string[] surnames = ["林", "陆", "叶", "温", "沈", "顾", "谢", "苏", "陈", "萧", "柳", "宁", "白", "方", "宋", "江"];
        string[] given = ["知微", "云舟", "听澜", "望舒", "长风", "砚秋", "见山", "若尘", "清和", "明霁", "栖梧", "临渊", "无忧", "照影", "闻笛", "行止"];
        string[] temperaments = ["沉稳", "果敢", "仁厚", "执着", "机敏", "孤傲"];
        var sect = state.Sects.Single(s => s.Id == sectId);
        var person = new Cultivator { Id = state.NewId(), Name = surnames[state.Roll(0, 15)] + given[state.Roll(0, 15)], SectId = sectId, AgeMonths = state.Roll(16, 28) * 12, Potential = state.Roll(40, 95), Temperament = temperaments[state.Roll(0, 5)], Portrait = ChoosePortrait(state), Technique = sect.Trait switch { "fire" => "赤霞焚天诀", "herb" => "青木长生经", "forge" => "百炼金身诀", "water" => "镜水无相经", "ice" => "玄霜寂照录", "thunder" => "九霄御雷经", "body" => "金莲不动经", "star" => "周天观星诀", "shadow" => "幽篁无影录", _ => "青云御剑诀" }, Biography = "初入仙途，前路未定。" };
        person.AgeMonths = Math.Min(person.AgeMonths, 22 * 12);
        person.InnateTrait = InnateTraitFromRoll(state.Roll(0, 99));
        state.People.Add(person);
        return person;
    }

    private static int ChoosePortrait(GameState state)
    {
        var usage = new int[PortraitCount];
        foreach (var person in state.People.Where(p => p.Alive))
            if (person.Portrait >= 0 && person.Portrait < PortraitCount) usage[person.Portrait]++;
        var minimum = usage.Min();
        var available = Enumerable.Range(0, PortraitCount).Where(i => usage[i] == minimum).ToArray();
        return available[state.Roll(0, available.Length - 1)];
    }
}
