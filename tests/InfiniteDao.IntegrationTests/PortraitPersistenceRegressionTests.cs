using InfiniteDao.Core.Game;
using InfiniteDao.Infrastructure.Persistence;

namespace InfiniteDao.IntegrationTests;

[TestClass]
public sealed class PortraitPersistenceRegressionTests
{
    [TestMethod]
    public async Task SaveAndLoadKeepEachAssignedPortrait()
    {
        var root = NewDirectory();
        try
        {
            var store = new GameSaveStore(root);
            var world = WorldFactory.Create("qingyun", 883, "rules", "normal", compact: true);
            world.Player.Foundation = 2;
            world.People[0].Realm = WorldFactory.MaximumRealm;
            world.People[0].InnateTrait = 4;
            world.People[0].RetainInWorld = true;
            world.People[1].Realm = WorldFactory.MaximumRealm;
            world.People[1].InnateTrait = 4;
            world.People[1].Alive = false;
            world.People[1].Ascended = true;
            var portraits = world.People.Select(person => $"{person.Id}:{person.Portrait}").ToArray();

            await store.SaveAsync(world, "one");
            var restored = await store.LoadAsync("one");

            Assert.AreEqual(4, restored.SchemaVersion);
            CollectionAssert.AreEqual(portraits, restored.People.Select(person => $"{person.Id}:{person.Portrait}").ToArray());
            Assert.AreEqual(2, restored.Player.Foundation);
            Assert.AreEqual(4, restored.People[0].InnateTrait);
            Assert.IsTrue(restored.People[0].RetainInWorld);
            Assert.IsTrue(restored.People[1].Ascended);
            Assert.IsFalse(restored.People[1].Alive);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public async Task UnsupportedSaveVersionsAreRejectedWithoutRewritingFiles(int version)
    {
        var root = NewDirectory();
        try
        {
            var store = new GameSaveStore(root);
            var world = WorldFactory.Create("qingyun", 993, "rules", "normal", compact: true);
            world.SchemaVersion = version;
            var json = System.Text.Json.JsonSerializer.Serialize(world);
            var path = Path.Combine(root, "one.json");
            await File.WriteAllTextAsync(path, json);
            var error = await Assert.ThrowsAsync<GameRuleException>(() => store.LoadAsync("one"));
            StringAssert.Contains(error.Message, "版本");
            Assert.AreEqual(json, await File.ReadAllTextAsync(path));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void ImportedSaveRejectsPortraitOutsideTheSupportedRange()
    {
        var store = new GameSaveStore("unused");
        var world = WorldFactory.Create("qingyun", 992, "rules", "normal", compact: true);
        world.People[0].Portrait = WorldFactory.PortraitCount;

        Assert.Throws<GameRuleException>(() => store.Parse(System.Text.Json.JsonSerializer.Serialize(world)));
    }

    [TestMethod]
    public async Task PersonalRelicAndManualRetentionSurviveSaveLoadAndCopy()
    {
        var root = NewDirectory();
        try
        {
            var store = new GameSaveStore(root);
            var world = WorldFactory.Create("qingyun", 994, "rules", "normal", compact: true);
            var holder = GameEngine.Members(world, world.Player).First();
            var relic = new Relic { Id = world.NewId(), Scope = "personal", TemplateId = "attack_1_1", Name = "传承听雨剑", Owner = world.PlayerId, HolderId = holder.Id, RegionId = world.Player.Capital, FoundMonth = world.Month, KeepAssigned = true };
            world.Relics.Add(relic);
            var origin = GameEngine.Log(world, "relic_discovery", "得宝", "得宝", [world.PlayerId]);
            world.History[^1].RelicChange = new RelicChange { RelicId = relic.Id, ToOwner = world.PlayerId, ToHolderId = holder.Id, RegionId = relic.RegionId };
            relic.OriginEvent = relic.LastEventId = origin;
            var battleEvent = GameEngine.Log(world, "war", "战报验证", "验证历史快照", [world.PlayerId]);
            world.BattleReports.Add(new BattleReport { EventId = battleEvent, RegionId = world.Player.Capital, FormationBonus = 1,
                Exchanges = [new DuelExchange { AttackerId = holder.Id, RelicName = relic.Name, RelicCategory = "attack", RelicBonus = .08, RealmMultiplier = 1 }] });
            relic.Name = "战后新名";
            await store.SaveAsync(world, "one");
            var restored = await store.LoadAsync("one");
            var copied = restored.Copy();
            var json = await File.ReadAllTextAsync(Path.Combine(root, "one.json"));
            var injected = json.Replace("\"templateId\":\"attack_1_1\"", "\"attackBonus\":999,\"templateId\":\"attack_1_1\"");
            Assert.AreNotEqual(json, injected);
            Assert.Throws<GameRuleException>(() => store.Parse(injected), "个人法宝不可注入自由数值字段");
            Assert.Throws<GameRuleException>(() => store.Parse(json.Insert(1, "\"Relics\":[],")), "大小写不同的法宝集合不能覆盖校验");
            foreach (var candidate in new[] { restored, copied })
            {
                var actual = candidate.Relics.Single(r => r.Id == relic.Id);
                Assert.AreEqual(holder.Id, actual.HolderId);
                Assert.IsTrue(actual.KeepAssigned);
                Assert.AreEqual("attack_1_1", actual.TemplateId);
                Assert.AreEqual("战后新名", actual.Name);
                Assert.AreEqual("传承听雨剑", candidate.BattleReports.Single().Exchanges.Single().RelicName);
                Assert.AreEqual(origin, actual.OriginEvent);
                Assert.AreEqual(relic.Id, candidate.History.Single(e => e.Id == origin).RelicChange!.RelicId);
                GameEngine.Validate(candidate);
            }
        }
        finally { Directory.Delete(root, true); }
    }

    private static string NewDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "infinite-dao-portrait-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
