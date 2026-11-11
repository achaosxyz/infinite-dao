using System.Text.Json;
using System.Text.Json.Nodes;
using InfiniteDao.Core.Game;

namespace InfiniteDao.Agents.Providers;

public sealed partial class SectAgentLoop
{
    private static readonly JsonArray Tools = JsonNode.Parse("""
    [
      {
        "name": "get_situation",
        "strict": true,
        "description": "只读查询当前宗门完整门人情况、邻境对手、进行中战役或相关历史。只在已有观察不足时使用；历史可用beforeEvent向前翻页。不能改变世界。",
        "input_schema": {
          "type": "object", "additionalProperties": false,
          "properties": {"section": {"type": "string", "enum": ["self", "neighbors", "campaigns", "history"]}, "beforeEvent": {"type": "integer", "description":"非负历史事件编号，只查询更早记录"}},
          "required": ["section"]
        }
      },
      {
        "name": "submit_strategy",
        "strict": true,
        "description": "最终提交可持续执行的宗门计划。只选合法战略和可达目标；留空target表示暂不出征。计划沿用至重大变化或复盘，不按月反复改。规则拒绝时根据反馈修正。",
        "input_schema": {
          "type": "object", "additionalProperties": false,
          "properties": {
            "policy": {"type": "string", "enum": ["balanced", "prosperity", "cultivation", "expansion", "diplomacy"]},
            "target": {"type": "string"}, "goal": {"type": "string", "description":"1至500字符的长期目标"},
            "reason": {"type": "string", "description":"1至300字符的公开决策理由，不是隐藏思考"},
            "stages": {"type": "array", "minItems": 1, "description":"1至8个阶段，每项1至600字符；只有policy与target交给规则执行", "items": {"type": "string"}},
            "reconsiderWhen": {"type": "string", "description":"500字符以内的重新议政条件"},
            "reviewAfterMonths": {"type": "integer", "description":"12至1200个月；运行配置可要求更早复盘"}
          },
          "required": ["policy", "target", "goal", "reason", "stages", "reconsiderWhen", "reviewAfterMonths"]
        }
      }
    ]
    """)!.AsArray();

    private static string Query(GameState world, Sect sect, string name, JsonElement input)
    {
        if (name != "get_situation") throw new GameRuleException("此Agent仅能查询局势并提交宗门战略，不能调用其他工具。");
        var section = input.GetProperty("section").GetString();
        object data = section switch
        {
            "self" => new { sect, members = GameEngine.Members(world, sect).Select(p => new { p.Id, p.Name, p.Role, realm = WorldFactory.RealmNames[p.Realm], p.AgeMonths, p.Potential, p.Injury, p.Focused, p.GuardianId, away = GameEngine.IsAway(world, p.Id), relic = GameEngine.HeldRelic(world, p.Id) is { } relic ? new { relic.Name, minimumRealm = WorldFactory.RealmNames[PersonalRelics.Find(relic.TemplateId)!.Tier], effect = PersonalRelics.Find(relic.TemplateId)!.EffectText } : null }),
                relicRules = "法器、灵器、法宝分别从炼气、筑基、金丹起用；御敌最高增加本人16%伤害，护身最高相对降低本人25%历练风险，库存不生效。",
                income = GameEngine.Income(world, sect), upkeep = GameEngine.Upkeep(world, sect), herbIncome = GameEngine.HerbIncome(world, sect), reservedStones = GameEngine.ReservedStones(world, sect), reservedHerbs = GameEngine.ReservedHerbs(world, sect) },
            "neighbors" => world.Regions.Where(r => r.Owner != sect.Id && GameEngine.Border(world, sect, r)).OrderBy(r => r.Id).Select(r => new { r.Id, r.Name, r.Owner, r.Fort, r.Terrain, r.Neighbors, defenseEstimate = GameEngine.DefensePower(world, r), minimumBattleMonths = GameEngine.MinimumBattleMonths(world, r),
                realmComposition = world.People.Where(p => p.Alive && p.SectId == r.Owner).GroupBy(p => p.Realm).Select(g => new { realm = WorldFactory.RealmNames[g.Key], count = g.Count(), available = g.Count(p => !GameEngine.IsAway(world, p.Id)) }) }),
            "campaigns" => world.Campaigns.Where(c => c.Attacker == sect.Id || c.Defender == sect.Id).ToArray(),
            "history" => History(world, sect, input),
            _ => throw new GameRuleException("未知查询分类，只允许self/neighbors/campaigns/history。")
        };
        return JsonSerializer.Serialize(data, JsonOptions);
    }

    private static object History(GameState world, Sect sect, JsonElement input)
    {
        var before = input.TryGetProperty("beforeEvent", out var value) ? value.GetInt32() : int.MaxValue;
        var events = world.History.Where(e => e.Sects.Contains(sect.Id) && e.Id < before).OrderByDescending(e => e.Id).Take(30).Reverse().ToArray();
        return new { events, nextBeforeEvent = events.FirstOrDefault()?.Id };
    }
}
