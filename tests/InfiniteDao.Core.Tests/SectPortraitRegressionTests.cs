using InfiniteDao.Core.Game;

namespace InfiniteDao.Core.Tests;

[TestClass]
public sealed class SectPortraitRegressionTests
{
    private static readonly string[] LegacySectIds = ["qingyun", "chixia", "baicao", "tiangong", "jingshui", "xuanshuang"];
    private static readonly string[] NewSectIds = ["leiting", "jinchan", "xingluo", "youhuang"];

    [TestMethod]
    public void TenSectsAreSelectableAndCompactWorldPreservesOldRegionLinksAndDiplomacy()
    {
        var templates = WorldFactory.Templates();
        CollectionAssert.AreEqual(LegacySectIds.Concat(NewSectIds).ToArray(), templates.Select(sect => sect.Id).ToArray());

        var standard = WorldFactory.Create("leiting", 731, "rules", "normal");
        Assert.AreEqual(2, standard.SchemaVersion);
        Assert.HasCount(10, standard.Sects);
        Assert.HasCount(192, standard.Regions);
        Assert.AreEqual(16, standard.MapColumns);
        Assert.AreEqual(12, standard.MapRows);
        GameEngine.Validate(standard);

        foreach (var template in templates)
        {
            var compact = WorldFactory.Create(template.Id, 731, "rules", "normal", compact: true);
            Assert.AreEqual(2, compact.SchemaVersion);
            Assert.AreEqual(template.Id, compact.PlayerId);
            Assert.HasCount(10, compact.Sects);
            Assert.HasCount(20, compact.Regions);
            Assert.AreEqual(4, compact.MapColumns);
            Assert.AreEqual(5, compact.MapRows);
            Assert.HasCount(10, compact.Sects.Select(sect => sect.Capital).Distinct());
            Assert.IsTrue(compact.Sects.All(sect => compact.Regions.Single(region => region.Id == sect.Capital).Owner == sect.Id),
                "每宗都应保有独立山门。");

            for (var index = 0; index < 16; index++)
            {
                var x = index % 4;
                var y = index / 4;
                var expectedOldNeighbors = new List<string>();
                if (x > 0) expectedOldNeighbors.Add($"r{index:00}");
                if (x < 3) expectedOldNeighbors.Add($"r{index + 2:00}");
                if (y > 0) expectedOldNeighbors.Add($"r{index - 3:00}");
                if (y < 3) expectedOldNeighbors.Add($"r{index + 5:00}");
                Assert.IsTrue(expectedOldNeighbors.All(compact.Regions[index].Neighbors.Contains),
                    $"紧凑地图的旧地域 {compact.Regions[index].Id} 应保留原有相邻关系。");
            }

            foreach (var sect in compact.Sects)
            foreach (var other in compact.Sects.Where(other => other.Id != sect.Id))
            {
                Assert.IsTrue(sect.Relations.ContainsKey(other.Id), $"{sect.Id} 缺少与 {other.Id} 的外交关系。");
                Assert.IsTrue(other.Relations.ContainsKey(sect.Id), $"{other.Id} 缺少与 {sect.Id} 的反向外交关系。");
            }

            var leiting = compact.Sects.Single(sect => sect.Id == "leiting");
            var jinchan = compact.Sects.Single(sect => sect.Id == "jinchan");
            var relationBefore = leiting.Relations[jinchan.Id];
            GameEngine.ChangeRelation(leiting, jinchan, 3);
            Assert.AreEqual(relationBefore + 3, leiting.Relations[jinchan.Id]);
            Assert.AreEqual(leiting.Relations[jinchan.Id], jinchan.Relations[leiting.Id]);
            GameEngine.Validate(compact);
        }
    }

    [TestMethod]
    public void PortraitPoolIsBalancedAndSameSeedReproducesAssignments()
    {
        var world = WorldFactory.Create("qingyun", 8821, "rules", "normal", compact: true);
        var repeated = WorldFactory.Create("qingyun", 8821, "rules", "normal", compact: true);

        Assert.IsTrue(world.People.All(person => person.Portrait is >= 0 and < WorldFactory.PortraitCount));
        Assert.AreEqual(WorldFactory.PortraitCount, world.People.Take(WorldFactory.PortraitCount)
            .Select(person => person.Portrait).Distinct().Count(), "立绘池尚有未使用编号时，新人物应避免复用已有立绘。");
        var usage = Enumerable.Range(0, WorldFactory.PortraitCount)
            .Select(portrait => world.People.Count(person => person.Alive && person.Portrait == portrait))
            .ToArray();
        Assert.HasCount(50, usage);
        Assert.IsLessThanOrEqualTo(1, usage.Max() - usage.Min(), "按当前全世界最少使用数分配后，初始使用次数应均衡。");
        CollectionAssert.AreEqual(world.People.Select(person => person.Portrait).ToArray(),
            repeated.People.Select(person => person.Portrait).ToArray(), "同一种子应复现人物立绘分配。");
    }

    [TestMethod]
    public void NewlyCreatedCultivatorUsesOneOfTheCurrentlyLeastUsedPortraits()
    {
        var world = WorldFactory.Create("qingyun", 991, "rules", "normal", compact: true);
        foreach (var person in world.People) person.Portrait = 7;
        var usageBefore = Enumerable.Range(0, WorldFactory.PortraitCount)
            .Select(portrait => world.People.Count(person => person.Alive && person.Portrait == portrait))
            .ToArray();

        var created = WorldFactory.NewCultivator(world, world.PlayerId);

        Assert.IsTrue(created.Portrait is >= 0 and < WorldFactory.PortraitCount);
        Assert.AreEqual(usageBefore.Min(), usageBefore[created.Portrait]);
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void SuccessionAndWorldCopyPreserveAssignedPortraits()
    {
        var world = WorldFactory.Create("qingyun", 192, "rules", "normal", compact: true);
        world.PendingEvent = null;
        world.NextEventMonth = int.MaxValue;
        var sect = world.Player;
        var leader = world.People.Single(person => person.Id == sect.LeaderId);
        leader.Realm = 0;
        leader.AgeMonths = WorldFactory.Lifespans[0] * 12 - 1;
        leader.Portrait = 48;
        leader.Loyalty = 0;
        foreach (var person in GameEngine.Members(world, sect).Where(person => person.Id != leader.Id))
        {
            person.Realm = 0;
            person.AgeMonths = 30 * 12;
            person.Loyalty = 1;
            person.Progress = 0;
        }
        var successor = GameEngine.Members(world, sect).First(person => person.Id != leader.Id);
        successor.Loyalty = 100;
        successor.Portrait = 49;

        var copy = world.Copy();
        CollectionAssert.AreEqual(world.People.Select(person => $"{person.Id}:{person.Portrait}").ToArray(),
            copy.People.Select(person => $"{person.Id}:{person.Portrait}").ToArray());
        copy.People.Single(person => person.Id == successor.Id).Portrait = 0;
        Assert.AreEqual(49, world.People.Single(person => person.Id == successor.Id).Portrait,
            "复制世界中调整立绘不得修改原世界。");

        GameEngine.AdvanceMonth(world);

        Assert.IsFalse(leader.Alive);
        Assert.AreEqual(successor.Id, sect.LeaderId);
        Assert.AreEqual("掌门", successor.Role);
        Assert.AreEqual(49, successor.Portrait, "继任掌门应保留已分配的人物立绘。");
        GameEngine.Validate(world);
    }

    [TestMethod]
    public void CoreValidationRejectsPortraitsOutsideTheFiftyEntryPool()
    {
        var world = WorldFactory.Create("qingyun", 237, "rules", "normal", compact: true);
        world.People[0].Portrait = -1;

        Assert.Throws<GameRuleException>(() => GameEngine.Validate(world));
    }
}
