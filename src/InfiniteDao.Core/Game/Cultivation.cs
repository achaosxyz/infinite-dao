namespace InfiniteDao.Core.Game;

public static partial class GameEngine
{
    private static readonly int[] CultivatorStoneUpkeep = [1, 3, 8, 20, 50, 125, 300, 700];
    private static readonly int[] CultivatorHerbUpkeep = [0, 1, 2, 5, 12, 28, 65, 150];
    public static int MaximumAgeMonths(Cultivator person) => WorldFactory.Lifespans[person.Realm] * 12;
    public static int HerbUpkeep(GameState world, Sect sect) => Members(world, sect).Sum(p => CultivatorHerbUpkeep[p.Realm]);
    public static double FoundationMultiplier(Sect sect) => 1 + Math.Min(10, sect.Foundation) * .05;
    public static bool CanAdvanceRealm(Cultivator person) => person.Realm < WorldFactory.InnateRealmLimits[person.InnateTrait];
    public static (int Stones, int Herbs) BreakthroughCost(Cultivator person, bool prepared)
    {
        var scale = 1 << person.Realm;
        return ((prepared ? 120 : 80) * scale, (prepared ? 50 : 35) * scale);
    }
    public static (int Stones, int Herbs) DoctrineUpgradeCost(Sect sect) => (240 * (1 << sect.Doctrine), 30 * (1 << sect.Doctrine));
    public static (int Stones, int Herbs) TrainingUpgradeCost(Sect sect) => (100 * (1 << sect.Training), 10 * (1 << sect.Training));
    private static Cultivator? PriorityBreakthrough(GameState world, Sect sect) => Members(world, sect)
        .Where(p => CanAdvanceRealm(p) && p.Progress >= 70 && p.RealmMonths >= WorldFactory.MinimumRealmMonths[p.Realm]
            && p.AgeMonths + 1 < MaximumAgeMonths(p) && p.Injury < 30 && !IsAway(world, p.Id))
        .OrderByDescending(p => p.Focused).ThenByDescending(p => p.InnateTrait).ThenByDescending(p => p.Realm).ThenBy(p => p.Id).FirstOrDefault();
    private static (int Stones, int Herbs) CultivationReserve(GameState world, Sect sect) => PriorityBreakthrough(world, sect) is { } person
        ? BreakthroughCost(person, false) : (0, 0);
    public static (int Stones, int Herbs) RetentionCost(GameState world, Sect sect, Cultivator person)
    {
        var years = Math.Max(0, person.RealmMonths - 120) / 12;
        var multiplier = 1 + Math.Min(100, years) * .1;
        return ((int)Math.Ceiling(Math.Max(3000, Income(world, sect) * 4) * multiplier),
            (int)Math.Ceiling(Math.Max(800, HerbIncome(world, sect) * 4) * multiplier));
    }

    private static bool ResolveAscension(GameState world, Sect? sect, Cultivator person)
    {
        if (person.Realm != WorldFactory.MaximumRealm || person.RealmMonths < 120) return false;
        if (sect is not null && person.RetainInWorld)
        {
            var cost = RetentionCost(world, sect, person);
            // Retention cannot consume the funds already needed for ordinary upkeep or the expedition supply.
            if (sect.Stones >= cost.Stones + ReservedStones(world, sect) && sect.Herbs >= cost.Herbs + ReservedHerbs(world, sect))
            {
                Spend(sect, cost.Stones, cost.Herbs);
                if (world.Month % 12 == 0)
                    Log(world, "cultivation", "逆天留世", $"{sect.Name}供给{person.Name}遮天灵阵，暂缓飞升；下月仍需供给，费用逐年增加。", [sect.Id], $"灵石 -{cost.Stones}；灵材 -{cost.Herbs}");
                return false;
            }
        }
        person.Alive = false; person.Ascended = true; person.RetainInWorld = false;
        if (sect is not null) sect.Foundation++;
        var origin = Log(world, "ascension", $"{person.Name}飞升", $"{person.Name}大乘满十年，顺应天道飞升。" + (sect is null ? "仙踪留在人间史书。" : $"为{sect.Name}留下道统底蕴；全宗生产与修炼加成现为{FoundationMultiplier(sect) - 1:P0}（上限50%）。"),
            sect is null ? [] : [sect.Id], sect is null ? "离开人间" : "底蕴 +1；离开人间");
        person.Biography += $" 青岚历{world.Month / 12 + 1}年飞升，留下道统。";
        if (HeldRelic(world, person.Id) is { } relic)
            MoveRelic(world, relic, sect?.Id ?? "", null, sect?.Capital ?? relic.RegionId, sect is null ? "飞升遗物失落" : "飞升传宝归库", origin);
        foreach (var disciple in world.People.Where(p => p.GuardianId == person.Id)) disciple.GuardianId = null;
        return true;
    }

    private static GameEvent? AscensionCouncil(GameState world)
    {
        var person = Members(world, world.Player).Where(p => p.Realm == WorldFactory.MaximumRealm && p.RealmMonths >= 108 && !p.AscensionWarned)
            .OrderByDescending(p => p.RealmMonths).ThenByDescending(p => p.Focused).ThenBy(p => p.Id).FirstOrDefault();
        if (person is null) return null;
        person.AscensionWarned = true;
        var cost = RetentionCost(world, world.Player, person);
        return new GameEvent
        {
            Id = world.NewId(), Type = "ascension", Title = $"{person.Name}将叩天门", Eyebrow = "飞升之议 · 世界已暂停", Actor = world.PlayerId, Target = person.Id.ToString(),
            Text = $"{person.Name}大乘已满{person.RealmMonths}个月，满120个月将自动飞升。顺应天道可为宗门留下底蕴；强留则持续消耗巨额资源，且不能延长寿元。",
            Choices = [
                new() { Id = "ascend", Label = "顺应天道，传承道统", Description = "到期自动飞升，底蕴 +1，生产与修炼 +5%（上限50%）；随身法宝留宗，不再需要供养。" },
                new() { Id = "retain", Label = "为此人设遮天阵", Description = $"现在只确定留世意愿；到期后预估每月{cost.Stones}灵石、{cost.Herbs}灵材，逐年加价。须同时保住常例供养与军需，缺资自动飞升，可随时在人物详情取消。" }
            ]
        };
    }

}
