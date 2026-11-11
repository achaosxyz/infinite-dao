namespace InfiniteDao.Core.Game;

public static partial class GameEngine
{
    private const int LowerRealmTargetLimit = 4;

    // One major realm is a qualitative gap, not just additional aggregate power.
    public static double RealmDamageMultiplier(int attackerRealm, int defenderRealm) => attackerRealm < defenderRealm
        ? Math.Pow(.12, defenderRealm - attackerRealm) : Math.Pow(1.35, attackerRealm - defenderRealm);

    private static double CultivatorHealth(Cultivator person) => 100 * Math.Pow(2, person.Realm) * (1 - person.Injury / 140d);
    private static double CultivatorDamage(GameState world, Cultivator person, int targetRealm) =>
        22 * Math.Pow(2, person.Realm) * (.8 + person.Potential / 200d) * WorldFactory.InnateCombatMultipliers[person.InnateTrait] * RealmDamageMultiplier(person.Realm, targetRealm)
        * (ActiveRelicTemplate(world, person) is { Category: "attack" } template ? 1 + template.Bonus : 1);
    private static double SectBattleBonus(GameState world, Sect sect) => (1 + sect.Training * .09) * TraitCombatMultiplier(sect)
        * (.65 + sect.Stability / 220d) * (1 - sect.WarExhaustion * .004)
        * (1 + Math.Min(.25, world.Relics.Where(r => r.Scope == "sect" && r.Owner == sect.Id).OrderByDescending(r => r.Power).Take(3).Sum(r => r.Power) / 600d));
    private static double DefenseFormation(GameState world, Sect? defender, Region region, double defenseBonus) =>
        (1 + region.Fort * .08) * defenseBonus
        * (defender?.Id == world.PlayerId ? world.Difficulty == "story" ? 1.2 : world.Difficulty == "hard" ? .95 : 1.05 : 1);

    private static List<Cultivator> GarrisonMembers(GameState world, Sect? defender, Region region) => defender is null
        ? Enumerable.Range(0, 3 + region.Fort).Select(i => new Cultivator
            { Id = -i - 1, Name = "地方守卫" + (i + 1), Realm = i == 0 ? 1 : 0, Potential = 55 }).ToList()
        : Members(world, defender).Where(p => !IsAway(world, p.Id)).OrderByDescending(p => p.Realm).ThenBy(p => p.Id).ToList();

    private static bool CanChallengeGarrison(GameState world, Sect attacker, Region region, IReadOnlyList<Cultivator> participants)
    {
        var defender = world.Sects.SingleOrDefault(s => s.Id == region.Owner);
        var defenders = GarrisonMembers(world, defender, region);
        if (defenders.Count == 0) return true;
        // A deterministic estimate of endurance and outgoing pressure, not a preview that rolls world RNG.
        // Use the highest opposing realm conservatively; actual target selection and casualties still decide the battle.
        double Strength(IReadOnlyList<Cultivator> fighters, IReadOnlyList<Cultivator> opponents, double bonus)
        {
            var highestRealm = opponents.Max(p => p.Realm);
            var health = fighters.Sum(CultivatorHealth);
            var pressure = fighters.Sum(p => CultivatorDamage(world, p, highestRealm)
                * (p.Realm > highestRealm ? Math.Min(LowerRealmTargetLimit, opponents.Count) : 1));
            return Math.Sqrt(health * pressure * bonus);
        }
        var attackStrength = Strength(participants, defenders, SectBattleBonus(world, attacker) * (1 + attacker.Intel * .04));
        var defenseStrength = Strength(defenders, participants, (defender is null ? 1 : SectBattleBonus(world, defender)) * DefenseFormation(world, defender, region, 1));
        return attackStrength >= defenseStrength * .8;
    }

    private sealed class Combatant(Cultivator person)
    {
        public Cultivator Person { get; } = person;
        public double Maximum { get; } = CultivatorHealth(person);
        public double Health { get; set; } = CultivatorHealth(person);
        public bool Standing => Health > 0;
    }

    private static BattleReport ResolveCultivatorBattle(GameState world, Sect attacker, Region region, string tactic,
        double defenseBonus, double supply, IReadOnlyList<int>? deployed)
    {
        var defender = world.Sects.SingleOrDefault(s => s.Id == region.Owner);
        var attacking = Members(world, attacker).Where(p => deployed is null ? !IsAway(world, p.Id) : deployed.Contains(p.Id))
            .OrderByDescending(p => p.Realm).ThenBy(p => p.Id).Select(p => new Combatant(p)).ToList();
        var defending = GarrisonMembers(world, defender, region).Select(p => new Combatant(p)).ToList();
        var formation = DefenseFormation(world, defender, region, defenseBonus);
        var attackBonus = SectBattleBonus(world, attacker) * supply * (1 + attacker.Intel * .04) * (tactic == "fierce" ? 1.25 : tactic == "careful" ? .9 : 1);
        var defendBonus = (defender is null ? 1 : SectBattleBonus(world, defender)) * formation;
        var report = new BattleReport { Month = world.Month, RegionId = region.Id, Attacker = attacker.Id, Defender = defender?.Id ?? "", FormationBonus = formation };
        var exchanges = new Dictionary<(int, int), DuelExchange>();
        void Strike(List<Combatant> side, List<Combatant> enemies, double bonus, List<(Combatant Target, double Damage)> strikes)
        {
            var targets = enemies.Where(c => c.Standing).OrderByDescending(c => c.Person.Realm).ThenBy(c => c.Person.Id).ToArray();
            if (targets.Length == 0) return;
            var index = 0;
            foreach (var source in side.Where(c => c.Standing))
            {
                var start = index++ % targets.Length;
                var targetLimit = source.Person.Realm > targets[start].Person.Realm ? LowerRealmTargetLimit : 1;
                for (int offset = 0, hits = 0; offset < targets.Length && hits < targetLimit; offset++)
                {
                    var target = targets[(start + offset) % targets.Length];
                    if (offset > 0 && target.Person.Realm >= source.Person.Realm) continue;
                    hits++;
                    var multiplier = RealmDamageMultiplier(source.Person.Realm, target.Person.Realm);
                    var damage = CultivatorDamage(world, source.Person, target.Person.Realm) * bonus * (.92 + world.Random() * .16);
                    strikes.Add((target, damage));
                    var key = (source.Person.Id, target.Person.Id);
                    if (!exchanges.TryGetValue(key, out var exchange))
                    {
                        var template = ActiveRelicTemplate(world, source.Person);
                        var relic = template is null ? null : HeldRelic(world, source.Person.Id);
                        exchange = new DuelExchange { AttackerId = key.Item1, DefenderId = key.Item2, AttackerName = source.Person.Name,
                            DefenderName = target.Person.Name, AttackerRealm = source.Person.Realm, DefenderRealm = target.Person.Realm, RealmMultiplier = multiplier,
                            RelicName = template is null ? "" : string.IsNullOrWhiteSpace(relic?.Name) ? "无名法宝" : relic.Name,
                            RelicCategory = template?.Category ?? "", RelicTier = template?.Tier ?? 0, RelicBonus = template?.Bonus ?? 0 };
                        exchanges.Add(key, exchange);
                    }
                    exchange.Hits++; exchange.Damage += damage;
                }
            }
        }
        for (var round = 1; round <= 48 && attacking.Any(c => c.Standing) && defending.Any(c => c.Standing); round++)
        {
            var strikes = new List<(Combatant Target, double Damage)>();
            Strike(attacking, defending, attackBonus, strikes); Strike(defending, attacking, defendBonus, strikes);
            foreach (var (target, damage) in strikes) target.Health = Math.Max(0, target.Health - damage);
            report.Rounds = round;
        }
        // A stalemate does not transfer a defended mountain. Occupation requires breaking the garrison.
        report.AttackerWon = attacking.Any(c => c.Standing) && !defending.Any(c => c.Standing);
        int ApplyLosses(List<Combatant> side, Sect? sect, bool lost)
        {
            if (sect is null) return 0;
            var deaths = 0;
            var safe = sect.Id == world.PlayerId ? Math.Max(0, Members(world, sect).Count - 3) : int.MaxValue;
            foreach (var c in side)
            {
                var fraction = 1 - c.Health / Math.Max(1, c.Maximum);
                if (!c.Standing && deaths < safe && world.Random() < (lost ? .42 : .18) * (tactic == "fierce" ? 1.4 : tactic == "careful" ? .55 : 1))
                {
                    c.Person.Alive = false; c.Person.RetainInWorld = false; deaths++;
                    c.Person.Biography += $" 青岚历{world.Month / 12 + 1}年于{region.Name}战役陨落。";
                    var killer = exchanges.Values.Where(e => e.DefenderId == c.Person.Id).OrderByDescending(e => e.Damage).FirstOrDefault();
                    var victor = world.People.SingleOrDefault(p => p.Id == killer?.AttackerId);
                    if (victor is not null) victor.Kills++;
                }
                else c.Person.Injury = Math.Min(95, c.Person.Injury + (int)(fraction * 65));
            }
            return deaths;
        }
        report.AttackerDeaths = ApplyLosses(attacking, attacker, !report.AttackerWon);
        report.DefenderDeaths = ApplyLosses(defending, defender, report.AttackerWon);
        report.Exchanges = exchanges.Values.OrderByDescending(e => e.Damage).Take(160).ToList();
        return report;
    }
}
