namespace InfiniteDao.Core.Game;

public static partial class WorldFactory
{
    private static void PopulateVastRegions(GameState world)
    {
        world.MapColumns = 16; world.MapRows = 12;
        string[] provinces = ["北苍", "寒洲", "赤陵", "东荒", "青岚", "云川", "玄泽", "西陲", "长生", "镜天", "南溟", "炎海"];
        string[] places = ["望月岭", "寒潭", "灵矿口", "栖霞山", "松风谷", "观星台", "落霞隘", "赤岩丘", "万药泽", "云河渡", "铸剑岭", "黑石矿", "长生谷", "镜湖", "浮萍洲", "古战场"];
        string[] terrains = ["雪原", "寒潭", "灵矿", "灵山", "幽谷", "灵山", "关隘", "丘陵", "药泽", "渡口", "灵山", "矿脉", "药谷", "湖泊", "水洲", "遗迹"];
        for (var y = 0; y < world.MapRows; y++)
        for (var x = 0; x < world.MapColumns; x++)
        {
            var local = y % 4 * 4 + x % 4;
            var province = y / 4 * 4 + x / 4;
            var index = y * world.MapColumns + x;
            var region = new Region { Id = $"r{index + 1:00}", Index = index, Name = provinces[province] + "·" + places[local], Terrain = terrains[local], Population = world.Roll(1800, 9000), Mine = world.Roll(1, 5), HerbYield = world.Roll(1, 5), Spirit = world.Roll(2, 5), Fort = local is 6 or 15 ? 2 : 1 };
            foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                if (x + dx >= 0 && x + dx < world.MapColumns && y + dy >= 0 && y + dy < world.MapRows)
                    region.Neighbors.Add($"r{(y + dy) * world.MapColumns + x + dx + 1:00}");
            region.Description = $"{provinces[province]}境内的{region.Terrain}，灵脉 {region.Spirit} 阶。行军须经接壤地域，占地与攻山历时数年，并逐月消耗补给。";
            world.Regions.Add(region);
        }
        (int X, int Y)[] seats = [(2, 5), (13, 2), (2, 10), (13, 8), (8, 10), (4, 1), (9, 1), (6, 6), (10, 5), (1, 2)];
        for (var i = 0; i < world.Sects.Count; i++)
        {
            var sect = world.Sects[i]; var (x, y) = seats[i];
            var capital = world.Regions[y * world.MapColumns + x]; sect.Capital = capital.Id;
            foreach (var region in world.Regions.Where(r => r.Id == capital.Id || capital.Neighbors.Contains(r.Id))) region.Owner = sect.Id;
            capital.Name = sect.Id switch { "qingyun" => "青岚·青云山", "chixia" => "赤陵·赤霞山", "baicao" => "长生·百草谷", "tiangong" => "炎海·天工岭", "jingshui" => "南溟·镜水山", "xuanshuang" => "寒洲·玄霜峰", "leiting" => "东荒·雷鸣崖", "jinchan" => "云川·金莲山", "xingluo" => "玄泽·观星台", _ => "北苍·幽篁谷" };
            capital.Fort = 3; capital.Development = 2;
        }
    }
}
