namespace InfiniteDao.Core.Game;

public static partial class GameEngine
{
    private static bool NeedsCultivation(Cultivator person) => CanAdvanceRealm(person) && person.Progress < 120;
    public static int ReservedHerbs(GameState world, Sect sect)
    {
        var campaign = world.Campaigns.FirstOrDefault(c => c.Attacker == sect.Id);
        if (campaign is not null) return campaign.MonthsRemaining * campaign.MonthlyHerbs;
        if (!world.AutoStewardship) return 0;
        if (sect.Policy == "prosperity" || sect.Policy == "cultivation" || sect.Policy == "diplomacy") return 100;
        var target = world.Regions.FirstOrDefault(r => r.Id == sect.Target && r.Owner != sect.Id);
        if (target is null) return 180;
        var cost = CampaignCosts(world, sect, target); return 20 + cost.Duration * cost.Herbs;
    }

    public static int ReservedStones(GameState world, Sect sect)
    {
        var reserve = Upkeep(world, sect) * 6 + 300;
        var campaign = world.Campaigns.FirstOrDefault(c => c.Attacker == sect.Id);
        if (campaign is not null) return reserve + campaign.MonthsRemaining * campaign.MonthlyStones;
        if (sect.Policy is not ("expansion" or "balanced")) return reserve;
        var target = world.Regions.FirstOrDefault(r => r.Id == sect.Target && r.Owner != sect.Id);
        if (target is null) return reserve;
        var cost = CampaignCosts(world, sect, target); return reserve + 130 + cost.Duration * cost.Stones;
    }

    private static void ProvisionSect(GameState world, Sect sect)
    {
        var members = Members(world, sect);
        var cultivation = CultivationReserve(world, sect);
        var monthly = HerbUpkeep(world, sect) + members.Where(NeedsCultivation).Sum(p => 1 + p.Realm + (p.Focused ? 3 + p.Realm * 2 : 0));
        var desired = monthly * 6 + ReservedHerbs(world, sect) + cultivation.Herbs + 120;
        var surplusStones = Math.Max(0, sect.Stones - ReservedStones(world, sect) - cultivation.Stones);
        // The market converts actual accumulated wealth; no free material or silent deficit subsidy.
        var purchased = Math.Min(Math.Max(0, desired - sect.Herbs), Math.Min(surplusStones / 4, Math.Max(50, HerbIncome(world, sect) * 3)));
        if (purchased <= 0) return;
        Spend(sect, purchased * 4); sect.Herbs += purchased;
        if (world.Month % 12 == 0)
            Log(world, "economy", "执事采办丹材", $"{sect.Name}从坊市购入{purchased}份丹材，优先保障供养与军需。", [sect.Id], $"灵石 -{purchased * 4}；灵材 +{purchased}");
    }
}
