namespace InfiniteDao.Core.Game;

public static partial class GameEngine
{
    public static bool IsMobilized(GameState world, Sect sect) => world.Campaigns.Any(c => c.Attacker == sect.Id);
    private static (int Duration, int Stones, int Herbs) CampaignCosts(GameState world, Sect sect, Region target)
    {
        var capital = world.Regions.FirstOrDefault(r => r.Id == sect.Capital) ?? target;
        var distance = Math.Abs(capital.Index % world.MapColumns - target.Index % world.MapColumns) + Math.Abs(capital.Index / world.MapColumns - target.Index / world.MapColumns);
        return (MinimumBattleMonths(world, target) + distance / 6, 40 + target.Fort * 10 + distance * 2, 8 + target.Fort * 2);
    }
    public static int MinimumBattleMonths(GameState world, Region target)
    {
        var defended = target.Owner != "";
        return (defended ? 36 + target.Fort * 9 : 12 + target.Fort * 2) + (target.Terrain is "关隘" or "雪原" or "湖泊" or "水洲" ? 6 : 0);
    }
    public static int DefensePower(GameState world, Region region)
    {
        var owner = world.Sects.SingleOrDefault(s => s.Id == region.Owner);
        if (owner is null) return 45 + region.Fort * 15 + region.Population / 500;
        var divided = 1 + world.Campaigns.Count(c => c.Defender == owner.Id) * .18;
        return (int)(Power(world, owner) * (.52 + region.Fort * .12) * (IsMobilized(world, owner) ? .65 : 1) / divided);
    }

    private static List<Cultivator> CampaignMembers(GameState world, Sect sect)
    {
        var available = Members(world, sect).Where(p => !IsAway(world, p.Id) && p.Injury < 60)
            .OrderByDescending(p => p.Realm).ThenByDescending(p => p.Potential).ThenBy(p => p.Id).ToList();
        return available.Count < 2 ? [] : available.Take(Math.Max(1, available.Count * 7 / 10)).ToList();
    }

    private static void StartCampaign(GameState world, Sect attacker, Region target, string tactic, double defenseBonus = 1)
    {
        if (IsMobilized(world, attacker)) throw new GameRuleException("宗门主力已在远征，需待战役结束后再开新战线。");
        if (world.Campaigns.Any(c => c.Target == target.Id)) throw new GameRuleException("该地已有战役进行，不能重复派遣同一主力。");
        var participants = CampaignMembers(world, attacker).Select(p => p.Id).ToList();
        if (participants.Count == 0) throw new GameRuleException("可调动门人不足，需等历练归山或养伤。");
        Spend(attacker, 130, 20);
        var cost = CampaignCosts(world, attacker, target);
        var origin = Log(world, "campaign", $"{attacker.Name}远征{target.Name}", $"主力集结、行军与围攻，预计{cost.Duration}个月。每月消耗灵石{cost.Stones}、灵材{cost.Herbs}。期间不能开启第二场远征，战果尚未结算。", target.Owner == "" ? [attacker.Id] : [attacker.Id, target.Owner], "备军灵石 -130；灵材 -20");
        foreach (var id in participants)
            if (HeldRelic(world, id) is { } relic) relic.RegionId = target.Id;
        world.Campaigns.Add(new Campaign { Id = world.NewId(), Attacker = attacker.Id, Defender = target.Owner, Target = target.Id, Tactic = tactic, StartedMonth = world.Month, Duration = cost.Duration, MonthsRemaining = cost.Duration, MonthlyStones = cost.Stones, MonthlyHerbs = cost.Herbs, DefenseBonus = defenseBonus, OriginEvent = origin, Participants = participants });
    }

    private static void AdvanceCampaigns(GameState world)
    {
        foreach (var campaign in world.Campaigns.OrderBy(c => c.Target).ThenBy(c => c.Id).ToList())
        {
            var attacker = world.Sects.Single(s => s.Id == campaign.Attacker);
            var target = world.Regions.Single(r => r.Id == campaign.Target);
            var defender = world.Sects.SingleOrDefault(s => s.Id == campaign.Defender);
            if (attacker.Eliminated || attacker.Exiled || target.Owner != campaign.Defender || !Border(world, attacker, target) || defender is not null && AtPeace(world, attacker, defender))
            {
                world.Campaigns.Remove(campaign);
                Log(world, "campaign", "远征中止", $"{attacker.Name}撤回{target.Name}远征军：战局、边界或盟约已经改变。", [attacker.Id], "已耗补给不退还", [campaign.OriginEvent]);
                ReturnCampaignRelics(world, campaign, attacker);
                continue;
            }
            if (attacker.Stones >= campaign.MonthlyStones && attacker.Herbs >= campaign.MonthlyHerbs) Spend(attacker, campaign.MonthlyStones, campaign.MonthlyHerbs);
            else { campaign.Supply -= .2; attacker.Stability = Math.Max(0, attacker.Stability - 4); }
            attacker.WarExhaustion = Math.Min(100, attacker.WarExhaustion + 2);
            if (campaign.Supply < .5)
            {
                world.Campaigns.Remove(campaign);
                Log(world, "campaign", "粮道断绝，撤军归山", $"{attacker.Name}连续缺乏补给，无法继续围攻{target.Name}。战果没有凭空产生。", [attacker.Id], "远征失败；控制权未变", [campaign.OriginEvent]);
                ReturnCampaignRelics(world, campaign, attacker);
                continue;
            }
            campaign.MonthsRemaining--;
            if (campaign.MonthsRemaining > 0) continue;
            world.Campaigns.Remove(campaign);
            Battle(world, attacker, target, campaign.Tactic, campaign.DefenseBonus, true, campaign.OriginEvent, campaign.Supply, campaign.Participants);
            ReturnCampaignRelics(world, campaign, attacker);
            attacker.WarExhaustion = Math.Min(100, attacker.WarExhaustion + 8);
        }
    }

    private static void ReturnCampaignRelics(GameState world, Campaign campaign, Sect sect)
    {
        if (sect.Eliminated || sect.Exiled || sect.Capital == "") return;
        foreach (var id in campaign.Participants)
            if (HeldRelic(world, id) is { } relic && relic.Owner == sect.Id) relic.RegionId = sect.Capital;
    }
}
