using InfiniteDao.Core.Game;
using InfiniteDao.Infrastructure.Persistence;

namespace InfiniteDao.IntegrationTests;

[TestClass]
public sealed class PortraitPersistenceRegressionTests
{
    private static readonly string[] LegacySectIds = ["qingyun", "chixia", "baicao", "tiangong", "jingshui", "xuanshuang"];

    [TestMethod]
    public async Task SaveAndLoadKeepEachAssignedPortrait()
    {
        var root = NewDirectory();
        try
        {
            var store = new GameSaveStore(root);
            var world = WorldFactory.Create("qingyun", 883, "rules", "normal", compact: true);
            var portraits = world.People.Select(person => $"{person.Id}:{person.Portrait}").ToArray();

            await store.SaveAsync(world, "one");
            var restored = await store.LoadAsync("one");

            Assert.AreEqual(2, restored.SchemaVersion);
            CollectionAssert.AreEqual(portraits, restored.People.Select(person => $"{person.Id}:{person.Portrait}").ToArray());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void LegacySixSectSaveMigratesDeterministicallyWithoutChangingWorldFactsOrRuleRandomState()
    {
        var store = new GameSaveStore("unused");
        var legacy = CreateLegacySixSectSnapshot();
        GameEngine.Validate(legacy);
        var json = System.Text.Json.JsonSerializer.Serialize(legacy);
        var regionsBefore = System.Text.Json.JsonSerializer.Serialize(legacy.Regions.Select(region => new
        {
            region.Id,
            region.Owner,
            Neighbors = region.Neighbors.Order().ToArray()
        }));
        var historyBefore = System.Text.Json.JsonSerializer.Serialize(legacy.History);
        var monthBefore = legacy.Month;
        var randomStateBefore = legacy.RandomState;
        var mapColumnsBefore = legacy.MapColumns;
        var mapRowsBefore = legacy.MapRows;
        var peopleIdsBefore = legacy.People.Select(person => person.Id).ToArray();

        var migrated = store.Parse(json);
        var repeated = store.Parse(json);

        Assert.AreEqual(2, migrated.SchemaVersion);
        Assert.HasCount(6, migrated.Sects, "旧世界只迁移原六宗，不应硬塞四个新宗。");
        CollectionAssert.AreEqual(LegacySectIds, migrated.Sects.Select(sect => sect.Id).ToArray());
        Assert.AreEqual(monthBefore, migrated.Month);
        Assert.AreEqual(randomStateBefore, migrated.RandomState);
        Assert.AreEqual(mapColumnsBefore, migrated.MapColumns);
        Assert.AreEqual(mapRowsBefore, migrated.MapRows);
        CollectionAssert.AreEqual(peopleIdsBefore, migrated.People.Select(person => person.Id).ToArray());
        Assert.AreEqual(regionsBefore, System.Text.Json.JsonSerializer.Serialize(migrated.Regions.Select(region => new
        {
            region.Id,
            region.Owner,
            Neighbors = region.Neighbors.Order().ToArray()
        })));
        Assert.AreEqual(historyBefore, System.Text.Json.JsonSerializer.Serialize(migrated.History));
        CollectionAssert.AreEqual(migrated.People.Select(person => $"{person.Id}:{person.Portrait}").ToArray(),
            repeated.People.Select(person => $"{person.Id}:{person.Portrait}").ToArray(), "相同旧存档应确定性迁移出相同立绘。");

        var usage = Enumerable.Range(0, WorldFactory.PortraitCount)
            .Select(portrait => migrated.People.Count(person => person.Alive && person.Portrait == portrait))
            .ToArray();
        Assert.IsTrue(migrated.People.All(person => person.Portrait is >= 0 and < WorldFactory.PortraitCount));
        Assert.IsTrue(usage.Any(count => count > 0) && migrated.People.Select(person => person.Portrait).Distinct().Count() > LegacySectIds.Length,
            "旧档迁移后立绘应独立于旧宗门模板编号。");
        GameEngine.Validate(migrated);
    }

    [TestMethod]
    public void ImportedSaveRejectsPortraitOutsideTheSupportedRange()
    {
        var store = new GameSaveStore("unused");
        var world = WorldFactory.Create("qingyun", 992, "rules", "normal", compact: true);
        world.People[0].Portrait = WorldFactory.PortraitCount;

        Assert.Throws<GameRuleException>(() => store.Parse(System.Text.Json.JsonSerializer.Serialize(world)));
    }

    private static GameState CreateLegacySixSectSnapshot()
    {
        var state = WorldFactory.Create("qingyun", 1255, "rules", "normal", compact: true);
        var oldIds = LegacySectIds.ToHashSet(StringComparer.Ordinal);
        var oldRegionIds = Enumerable.Range(1, 16).Select(index => $"r{index:00}").ToHashSet(StringComparer.Ordinal);

        state.SchemaVersion = 1;
        state.Sects = state.Sects.Where(sect => oldIds.Contains(sect.Id)).ToList();
        foreach (var sect in state.Sects)
        {
            sect.Relations = sect.Relations.Where(pair => oldIds.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
            sect.Truces = sect.Truces.Where(pair => oldIds.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
        }
        state.People = state.People.Where(person => oldIds.Contains(person.SectId)).ToList();
        foreach (var person in state.People)
            person.Portrait = state.Sects.Single(sect => sect.Id == person.SectId).Portrait;
        state.Regions = state.Regions.Take(16).ToList();
        foreach (var region in state.Regions)
        {
            if (!oldIds.Contains(region.Owner)) region.Owner = "";
            region.Neighbors.RemoveAll(neighbor => !oldRegionIds.Contains(neighbor));
        }
        state.MapColumns = 4;
        state.MapRows = 4;
        state.AgentMemories.RemoveAll(memory => !oldIds.Contains(memory.SectId));
        state.Decisions.RemoveAll(decision => !oldIds.Contains(decision.SectId));
        return state;
    }

    private static string NewDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "infinite-dao-portrait-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
