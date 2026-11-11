namespace InfiniteDao.Core.Game;

/// <summary>Fixed personal-relic content; instances store only the stable template identity.</summary>
public sealed record PersonalRelicTemplate(string Id, string Name, string Category, int Tier, double Bonus)
{
    public string EffectText => $"{new[] { "法器", "灵器", "法宝" }[Tier]} · {(Category == "attack" ? "本人伤害提高" : "本人历练遇险概率相对降低")}{Bonus:P0} · {WorldFactory.RealmNames[Tier]}起御使";
}

public static class PersonalRelics
{
    public static readonly IReadOnlyList<PersonalRelicTemplate> Templates = CreateTemplates();

    private static IReadOnlyList<PersonalRelicTemplate> CreateTemplates()
    {
        string[][] names =
        [
            ["听雨剑", "竹影短刃", "青锋飞针", "乌木短矛", "铁羽飞梭"],
            ["赤霞火印", "玄霜飞剑", "烈焰葫芦", "破阵铜锥", "流火环"],
            ["惊雷尺", "九曜剑轮", "镇岳山印", "听潮玉笛", "赤龙宝幡"],
            ["青木护符", "清心玉佩", "护脉铜铃", "玄纹龟甲", "避尘灵簪"],
            ["金莲念珠", "百炼护镜", "青岚玉伞", "星斗护盘", "流云绫"],
            ["澄心灵镜", "玄武宝盾", "护道莲灯", "星纹玉瓶", "乾元宝珠"]
        ];
        double[][] bonuses = [[0.08, 0.12, 0.16], [0.15, 0.20, 0.25]];
        return names.SelectMany((group, groupIndex) => group.Select((name, index) =>
            new PersonalRelicTemplate($"{(groupIndex < 3 ? "attack" : "protection")}_{groupIndex % 3 + 1}_{index + 1}",
                name, groupIndex < 3 ? "attack" : "protection", groupIndex % 3, bonuses[groupIndex / 3][groupIndex % 3])))
            .ToArray();
    }

    public static PersonalRelicTemplate? Find(string id) => Templates.FirstOrDefault(t => t.Id == id);

    internal static void Validate(GameState world, HashSet<int> historyIds)
    {
        if (world.Relics.Select(r => r.Id).Distinct().Count() != world.Relics.Count || world.Relics.Any(r => r.Id <= 0 || r.Id >= world.NextId))
            throw new GameRuleException("法宝身份无效或重复。");
        var usedIds = world.People.Select(p => p.Id).Concat(world.Campaigns.Select(c => c.Id)).Concat(world.Expeditions.Select(e => e.Id)).ToHashSet();
        if (world.PendingEvent is not null) usedIds.Add(world.PendingEvent.Id);
        if (usedIds.Any(id => id <= 0 || id >= world.NextId) || world.Relics.Any(r => usedIds.Contains(r.Id) || historyIds.Contains(r.Id))) throw new GameRuleException("实体身份超出分配范围或与法宝冲突。");
        var personal = world.Relics.Where(r => r.Scope == "personal").ToDictionary(r => r.Id);
        if (world.Relics.Any(r => r.Scope is not ("sect" or "personal"))) throw new GameRuleException("法宝类型无效。");
        if (world.Relics.Any(r => r.Scope == "sect" && (r.TemplateId != "" || r.HolderId != null || r.RegionId != "" || r.KeepAssigned || r.LastEventId != 0)))
            throw new GameRuleException("镇宗法宝包含个人法宝字段。");
        if (personal.Count == 0 && world.History.All(e => e.RelicChange is null)) return;
        var people = world.People.ToDictionary(p => p.Id);
        var sects = world.Sects.ToDictionary(s => s.Id);
        var regions = world.Regions.Select(r => r.Id).ToHashSet();
        if (personal.Values.Where(r => r.HolderId != null).GroupBy(r => r.HolderId).Any(g => g.Count() > 1))
            throw new GameRuleException("一人不能持有多件个人法宝。");
        var last = new Dictionary<int, Chronicle>();
        foreach (var entry in world.History.Where(e => e.RelicChange != null))
        {
            var c = entry.RelicChange!;
            if (!personal.ContainsKey(c.RelicId) || !regions.Contains(c.RegionId)
                || c.FromHolderId is int from && !people.ContainsKey(from) || c.ToHolderId is int to && !people.ContainsKey(to)
                || c.FromOwner != "" && !sects.ContainsKey(c.FromOwner) || c.ToOwner != "" && !sects.ContainsKey(c.ToOwner))
                throw new GameRuleException("法宝流转引用无效。");
            if (last.TryGetValue(c.RelicId, out var previous))
            {
                var prior = previous.RelicChange!;
                if (prior.ToHolderId != c.FromHolderId || prior.ToOwner != c.FromOwner || !entry.Causes.Contains(previous.Id))
                    throw new GameRuleException("法宝流转历史不连续。");
            }
            else if (c.FromHolderId != null || c.FromOwner != "" || personal[c.RelicId].OriginEvent != entry.Id || personal[c.RelicId].FoundMonth != entry.Month)
                throw new GameRuleException("法宝来源记录无效。");
            last[c.RelicId] = entry;
        }
        foreach (var relic in personal.Values)
        {
            var template = Find(relic.TemplateId);
            if (template is null || relic.Power != 0 || !regions.Contains(relic.RegionId) || relic.FoundMonth < 0 || relic.FoundMonth > world.Month
                || !last.TryGetValue(relic.Id, out var latest) || latest.Id != relic.LastEventId
                || latest.RelicChange!.ToHolderId != relic.HolderId || latest.RelicChange.ToOwner != relic.Owner)
                throw new GameRuleException("个人法宝状态或来源无效。");
            if (relic.HolderId is null && relic.Owner == "" && latest.RelicChange.RegionId != relic.RegionId)
                throw new GameRuleException("遗失法宝地域与流转记录不符。");
            if (relic.HolderId is int holder)
            {
                if (!people.TryGetValue(holder, out var person) || !person.Alive || person.SectId != relic.Owner || person.Realm < template.Tier
                    || relic.KeepAssigned && person.SectId != world.PlayerId) throw new GameRuleException("个人法宝持有人无效。");
            }
            else if (relic.KeepAssigned || relic.Owner != "" && (!sects.TryGetValue(relic.Owner, out var owner) || owner.Eliminated))
                throw new GameRuleException("个人法宝库存无效。");
        }
    }
}

public static partial class GameEngine
{
    public static Relic? HeldRelic(GameState world, int personId) =>
        world.Relics.FirstOrDefault(r => r.Scope == "personal" && r.HolderId == personId);

    public static PersonalRelicTemplate? ActiveRelicTemplate(GameState world, Cultivator person)
    {
        var relic = HeldRelic(world, person.Id);
        var template = relic is null ? null : PersonalRelics.Find(relic.TemplateId);
        return person.Alive && relic?.Owner == person.SectId && template is not null && person.Realm >= template.Tier ? template : null;
    }

    private static void MoveRelic(GameState world, Relic relic, string owner, int? holder, string region, string title, int cause = 0)
    {
        var oldHolder = relic.HolderId;
        var oldOwner = relic.Owner;
        var causes = new[] { relic.LastEventId, cause }.Where(id => id != 0).Distinct().ToArray();
        var sects = new[] { oldOwner, owner }.Where(id => id != "").Distinct().ToArray();
        var id = Log(world, "relic", title, $"{relic.Name}于{world.Regions.Single(r => r.Id == region).Name}流转。", sects, causes: causes);
        world.History[^1].RelicChange = new RelicChange { RelicId = relic.Id, FromHolderId = oldHolder, ToHolderId = holder, FromOwner = oldOwner, ToOwner = owner, RegionId = region };
        relic.Owner = owner; relic.HolderId = holder; relic.RegionId = region; relic.KeepAssigned = false; relic.LastEventId = id;
    }

    private static void AssignPersonalRelic(GameState world, GameCommand command)
    {
        var person = Members(world, world.Player).SingleOrDefault(p => p.Id.ToString() == command.Target)
            ?? throw new GameRuleException("只能安排本宗在世门人。");
        var current = HeldRelic(world, person.Id);
        if (command.Type == "relic_auto")
        {
            if (current is null || !current.KeepAssigned) throw new GameRuleException("此人没有需恢复托管的法宝。");
            current.KeepAssigned = false;
            Log(world, "decision", "法宝交由执事", $"{person.Name}的随身法宝恢复托管。", [world.PlayerId]);
            if (!world.Player.Exiled && !IsAway(world, person.Id)) AssignInventory(world, world.Player);
            return;
        }
        if (!person.Focused || world.Player.Exiled || IsAway(world, person.Id)) throw new GameRuleException("只能向已归山的重点人物赐宝。");
        var relic = world.Relics.SingleOrDefault(r => r.Id.ToString() == command.Option && r.Scope == "personal" && r.Owner == world.PlayerId)
            ?? throw new GameRuleException("只能调配本宗个人法宝。");
        var template = PersonalRelics.Find(relic.TemplateId)!;
        if (person.Realm < template.Tier || relic.HolderId is int source && IsAway(world, source)) throw new GameRuleException("境界不足或原持有人尚未归山。");
        if (current != relic)
        {
            if (current is not null) MoveRelic(world, current, world.PlayerId, null, world.Player.Capital, "旧物归库");
            MoveRelic(world, relic, world.PlayerId, person.Id, world.Player.Capital, "宗门赐宝");
        }
        relic.KeepAssigned = true;
        Log(world, "decision", "保留所赐法宝", $"{person.Name}保留{relic.Name}，不自动换新。", [world.PlayerId]);
        AssignInventory(world, world.Player);
    }

    private static void AssignInventory(GameState world, Sect sect)
    {
        if (!world.Relics.Any(r => r.Scope == "personal" && r.Owner == sect.Id && r.HolderId is null)) return;
        var candidates = Members(world, sect).Where(p => !IsAway(world, p.Id))
            .OrderByDescending(p => p.Focused).ThenByDescending(p => p.Realm).ThenByDescending(p => p.Potential).ThenBy(p => p.Id).ToList();
        var held = world.Relics.Where(r => r.Scope == "personal" && r.HolderId is not null).ToDictionary(r => r.HolderId!.Value);
        // Lower-tier displaced items are considered later in the same pass.
        for (var tier = 2; tier >= 0; tier--)
        {
            var inventory = world.Relics.Where(r => r.Scope == "personal" && r.Owner == sect.Id && r.HolderId == null
                && PersonalRelics.Find(r.TemplateId)!.Tier == tier).OrderBy(r => r.Id).ToList();
            foreach (var relic in inventory)
            {
                var template = PersonalRelics.Find(relic.TemplateId)!;
                var person = candidates.FirstOrDefault(p => p.Realm >= template.Tier &&
                    (!held.TryGetValue(p.Id, out var current) || !current.KeepAssigned &&
                        PersonalRelics.Find(current.TemplateId) is { } oldTemplate && oldTemplate.Category == template.Category && oldTemplate.Tier < template.Tier));
                if (person is null) continue;
                held.TryGetValue(person.Id, out var old);
                if (old is not null) MoveRelic(world, old, sect.Id, null, sect.Capital, "旧物归库");
                MoveRelic(world, relic, sect.Id, person.Id, sect.Capital, old is null ? "法宝传承" : "同类换新");
                held[person.Id] = relic;
            }
        }
    }

    private static void JourneyRelicReturn(GameState world, Sect sect, Cultivator person, Region region, int journeyEvent)
    {
        var lost = world.Relics.Where(r => r.Scope == "personal" && r.Owner == "" && r.HolderId is null && r.RegionId == region.Id)
            .OrderBy(r => r.Id).FirstOrDefault();
        if (lost is not null)
        {
            MoveRelic(world, lost, sect.Id, null, region.Id, "历练重获", journeyEvent);
            lost.RegionId = sect.Capital;
        }
        AssignInventory(world, sect);
        if (lost is not null) return;
        var held = HeldRelic(world, person.Id);
        var old = held is null ? null : PersonalRelics.Find(held.TemplateId);
        var tier = Math.Min(2, person.Realm);
        if (held?.KeepAssigned == true || old is not null && tier <= old.Tier) return;
        var first = !world.History.Any(e => e.Kind == "relic_discovery" && e.RelicChange?.ToOwner == sect.Id);
        if (!first && world.Random() >= .1) return;
        var category = old?.Category ?? (world.Roll(0, 1) == 0 ? "attack" : "protection");
        var candidates = PersonalRelics.Templates.Where(t => t.Category == category && t.Tier == tier).ToArray();
        var template = candidates[world.Roll(0, candidates.Length - 1)];
        var relic = new Relic { Id = world.NewId(), Name = template.Name, Scope = "personal", TemplateId = template.Id,
            RegionId = region.Id, FoundMonth = world.Month };
        world.Relics.Add(relic);
        if (held is not null) MoveRelic(world, held, sect.Id, null, sect.Capital, "旧物归库", journeyEvent);
        MoveRelic(world, relic, sect.Id, person.Id, region.Id, "历练得宝", journeyEvent);
        world.History[^1].Kind = "relic_discovery";
        relic.OriginEvent = relic.LastEventId;
        relic.RegionId = sect.Capital;
        AssignInventory(world, sect);
    }
}
