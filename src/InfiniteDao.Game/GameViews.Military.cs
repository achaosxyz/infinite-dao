using Godot;
using InfiniteDao.Core.Game;
using static InfiniteDao.Game.NativeTheme;

namespace InfiniteDao.Game;

public partial class GameClient
{
    private void ShowCampaigns()
    {
        paused = true; var state = world!; var view = Stack(14);
        view.AddChild(Text("每宗只能派出一支远征军。逐月支付补给，持续断供会撤退；战疲削弱战力，留守休养才能恢复。结盟会在下一月停止双方战线。", 14, Muted, true));
        var rows = Stack();
        foreach (var campaign in state.Campaigns.OrderBy(c => c.Attacker != state.PlayerId).ThenBy(c => c.Id))
        {
            var attacker = state.Sects.Single(s => s.Id == campaign.Attacker); var region = state.Regions.Single(r => r.Id == campaign.Target);
            var card = Stack(10); card.AddChild(Heading($"{attacker.Name} → {region.Name}", 22, new Color(attacker.Color)));
            card.AddChild(Text($"已行军围城 {campaign.Duration - campaign.MonthsRemaining} / {campaign.Duration} 月 · 预计余 {campaign.MonthsRemaining} 月\n月需 {campaign.MonthlyStones} 灵石 / {campaign.MonthlyHerbs} 灵材 · 补给 {campaign.Supply:P0} · 战疲 {attacker.WarExhaustion}%", 14, Paper, true));
            var actions = Row(); actions.AddChild(Button("追溯出征令", "campaign-origin-" + campaign.Id, () => ShowChronicle(campaign.OriginEvent)));
            card.AddChild(Text($"实际参战门人 {campaign.Participants.Count} · 在外门人不重复部署；留守者负责护山。", 12, Muted, true));
            card.AddChild(actions); rows.AddChild(Card(card));
        }
        if (state.Campaigns.Count == 0) rows.AddChild(Text("眼下没有战役。执事依据战略与储备决定远征，掌门只定发展方向。", color: Muted, wrap: true));
        foreach (var battle in state.BattleReports.TakeLast(12).Reverse())
        {
            var region = state.Regions.Single(r => r.Id == battle.RegionId);
            rows.AddChild(Button($"{Date(battle.Month)} · {region.Name} · {(battle.AttackerWon ? "破阵" : "守住")} · {battle.Rounds}轮交锋", "battle-report-" + battle.EventId, () => ShowBattleReport(battle)));
        }
        var scroll = Scroll(rows); scroll.CustomMinimumSize = new Vector2(730, 350); view.AddChild(scroll); ShowOverlay("天下军情", view);
    }

    private void ShowBattleReport(BattleReport battle)
    {
        var content = Stack(12);
        content.AddChild(Text($"护山阵倍率 {battle.FormationBonus:F2} · {battle.Rounds}轮\n进攻方陨落{battle.AttackerDeaths}人；守方陨落{battle.DefenderDeaths}人。低一大境界伤害只有12%，每差一境递乘；总战力不能替代对局。", 15, Gold, true));
        var records = Stack(6);
        foreach (var exchange in battle.Exchanges.Take(60))
            records.AddChild(Text($"{exchange.AttackerName}（{WorldFactory.RealmNames[exchange.AttackerRealm]}）→ {exchange.DefenderName}（{WorldFactory.RealmNames[exchange.DefenderRealm]}）\n{exchange.Hits}次出手 · 累计伤害{exchange.Damage:F1} · 境界倍率{exchange.RealmMultiplier:F3}{(exchange.RelicName == "" ? "" : $" · 当场御使{exchange.RelicName}（{new[] { "法器", "灵器", "法宝" }[exchange.RelicTier]}，{(exchange.RelicCategory == "attack" ? $"伤害 +{exchange.RelicBonus:P0}" : "护身，不增加战斗伤害")}）")}", 13, exchange.RealmMultiplier < 1 ? Muted : Paper, true));
        var scroll = Scroll(records); scroll.CustomMinimumSize = new Vector2(700, 380); content.AddChild(scroll);
        content.AddChild(Button("追溯此战因果", "battle-cause", () => ShowChronicle(battle.EventId)));
        ShowOverlay("战役对局 · 门人交锋", content);
    }
}
