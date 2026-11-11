using System.Text.Json;

namespace InfiniteDao.Core.Game;

public sealed class GameState
{
    public int SchemaVersion { get; set; } = 2;
    public int Seed { get; set; }
    public uint RandomState { get; set; }
    public long Revision { get; set; }
    public int Month { get; set; }
    public string PlayerId { get; set; } = "qingyun";
    public string Difficulty { get; set; } = "normal";
    public string Mode { get; set; } = "llm";
    public string Status { get; set; } = "playing";
    public int CommandsLeft { get; set; } = 2;
    public int Rebirths { get; set; }
    public int NextEventMonth { get; set; } = 24;
    public int NextId { get; set; } = 1000;
    public int ModelRequests { get; set; }
    public int ModelContextChecks { get; set; }
    public int MapColumns { get; set; } = 4;
    public int MapRows { get; set; } = 4;
    public bool AutoStewardship { get; set; } = true;
    public long ModelTokens { get; set; }
    public string LastCouncil { get; set; } = "诸宗静候风起。";
    public string AgentStatus { get; set; } = "";
    public List<Sect> Sects { get; set; } = [];
    public List<Region> Regions { get; set; } = [];
    public List<Cultivator> People { get; set; } = [];
    public List<Chronicle> History { get; set; } = [];
    public List<Relic> Relics { get; set; } = [];
    public GameEvent? PendingEvent { get; set; }
    public List<AgentDecision> Decisions { get; set; } = [];
    public List<Campaign> Campaigns { get; set; } = [];
    public List<Expedition> Expeditions { get; set; } = [];
    public List<BattleReport> BattleReports { get; set; } = [];
    public List<SectAgentMemory> AgentMemories { get; set; } = [];

    public GameState Copy() => JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(this))!;
    public double Random()
    {
        var x = RandomState == 0 ? 0x9E3779B9u : RandomState;
        x ^= x << 13; x ^= x >> 17; x ^= x << 5;
        RandomState = x;
        return x / 4294967296d;
    }
    public int Roll(int min, int max) => min + (int)(Random() * (max - min + 1));
    public int NewId() => NextId++;
    public Sect Player => Sects.Single(s => s.Id == PlayerId);
}

public sealed class Sect
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
    public string Sigil { get; set; } = "";
    public string Motto { get; set; } = "";
    public string Trait { get; set; } = "";
    public string Personality { get; set; } = "";
    public int Portrait { get; set; }
    public string Capital { get; set; } = "";
    public int LeaderId { get; set; }
    public int Stones { get; set; } = 600;
    public int Herbs { get; set; } = 160;
    public int Renown { get; set; } = 20;
    public int Stability { get; set; } = 80;
    public int Doctrine { get; set; } = 1;
    public int Training { get; set; } = 1;
    public int Intel { get; set; }
    public int WarExhaustion { get; set; }
    public string Policy { get; set; } = "balanced";
    public string Target { get; set; } = "";
    public string Intent { get; set; } = "整顿山门，积蓄实力。";
    public bool Eliminated { get; set; }
    public bool Exiled { get; set; }
    public int ExileSince { get; set; }
    public Dictionary<string, int> Relations { get; set; } = [];
    public Dictionary<string, int> Truces { get; set; } = [];
}

public sealed class Region
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Index { get; set; }
    public string Owner { get; set; } = "";
    public string Terrain { get; set; } = "";
    public string Description { get; set; } = "";
    public int Population { get; set; }
    public int Mine { get; set; }
    public int Spirit { get; set; }
    public int HerbYield { get; set; }
    public int Development { get; set; } = 1;
    public int Fort { get; set; } = 1;
    public List<string> Neighbors { get; set; } = [];
}

public sealed class Cultivator
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string SectId { get; set; } = "";
    public int Portrait { get; set; }
    public int AgeMonths { get; set; }
    public int Realm { get; set; }
    public int Potential { get; set; }
    public int Loyalty { get; set; } = 80;
    public int Injury { get; set; }
    public int Progress { get; set; }
    public int RealmMonths { get; set; }
    public bool Focused { get; set; }
    public int? GuardianId { get; set; }
    public int NextJourneyMonth { get; set; } = 12;
    public bool Alive { get; set; } = true;
    public string Temperament { get; set; } = "沉稳";
    public string Role { get; set; } = "弟子";
    public string Technique { get; set; } = "引气诀";
    public int Kills { get; set; }
    public string Biography { get; set; } = "";
}

public sealed class Chronicle
{
    public int Id { get; set; }
    public int Month { get; set; }
    public string Kind { get; set; } = "world";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public List<string> Sects { get; set; } = [];
    public List<int> Causes { get; set; } = [];
    public string Delta { get; set; } = "";
}

public sealed class Relic
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Effect { get; set; } = "";
    public int Power { get; set; }
    public int FoundMonth { get; set; }
    public int OriginEvent { get; set; }
}

public sealed class GameEvent
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string Eyebrow { get; set; } = "宗门议事";
    public string Actor { get; set; } = "";
    public string Target { get; set; } = "";
    public string NarrativeSource { get; set; } = "rules";
    public List<EventChoice> Choices { get; set; } = [];
}

public sealed class EventChoice
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    public string Flavor { get; set; } = "";
    public int Stones { get; set; }
    public int Herbs { get; set; }
}

public sealed class AgentDecision
{
    public int Month { get; set; }
    public string SectId { get; set; } = "";
    public string Policy { get; set; } = "balanced";
    public string Target { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Source { get; set; } = "rules";
}

public sealed record GameCommand(string Type, string Target = "", string Option = "");
public sealed class GameRuleException(string message) : Exception(message);

public sealed class Campaign
{
    public int Id { get; set; }
    public string Attacker { get; set; } = "";
    public string Defender { get; set; } = "";
    public string Target { get; set; } = "";
    public string Tactic { get; set; } = "balanced";
    public int StartedMonth { get; set; }
    public int MonthsRemaining { get; set; }
    public int Duration { get; set; }
    public int MonthlyStones { get; set; }
    public int MonthlyHerbs { get; set; }
    public double Supply { get; set; } = 1;
    public double DefenseBonus { get; set; } = 1;
    public int OriginEvent { get; set; }
    public List<int> Participants { get; set; } = [];
}

public sealed class Expedition
{
    public int Id { get; set; }
    public int DiscipleId { get; set; }
    public int? GuardianId { get; set; }
    public string SectId { get; set; } = "";
    public string RegionId { get; set; } = "";
    public int MonthsRemaining { get; set; }
    public int OriginEvent { get; set; }
}

public sealed class BattleReport
{
    public int EventId { get; set; }
    public int Month { get; set; }
    public string RegionId { get; set; } = "";
    public string Attacker { get; set; } = "";
    public string Defender { get; set; } = "";
    public bool AttackerWon { get; set; }
    public int Rounds { get; set; }
    public double FormationBonus { get; set; }
    public int AttackerDeaths { get; set; }
    public int DefenderDeaths { get; set; }
    public List<DuelExchange> Exchanges { get; set; } = [];
}

public sealed class DuelExchange
{
    public int AttackerId { get; set; }
    public int DefenderId { get; set; }
    public string AttackerName { get; set; } = "";
    public string DefenderName { get; set; } = "";
    public int AttackerRealm { get; set; }
    public int DefenderRealm { get; set; }
    public int Hits { get; set; }
    public double Damage { get; set; }
    public double RealmMultiplier { get; set; }
}
