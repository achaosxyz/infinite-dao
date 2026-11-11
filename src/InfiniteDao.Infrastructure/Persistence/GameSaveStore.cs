using System.Text.Json;
using InfiniteDao.Core.Game;

namespace InfiniteDao.Infrastructure.Persistence;

public sealed class GameSaveStore(string directory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = false, AllowDuplicateProperties = false };
    private static readonly string[] Slots = ["auto", "one", "two", "three"];
    public const int MaximumSaveCharacters = 64_000_000;

    private string GetPath(string slot)
    {
        if (!Slots.Contains(slot)) throw new GameRuleException("存档位置无效。");
        return Path.Combine(directory, slot + ".json");
    }

    public async Task SaveAsync(GameState state, string slot)
    {
        GameEngine.Validate(state);
        var json = JsonSerializer.Serialize(state, Json);
        if (json.Length > MaximumSaveCharacters) throw new GameRuleException("宗门史超过存档容量，请先导出历史；原自动档仍保留。");
        var path = GetPath(slot);
        Directory.CreateDirectory(directory);
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, json);
        File.Move(temporary, path, true);
    }

    public async Task<GameState> LoadAsync(string slot)
    {
        var path = GetPath(slot);
        if (!File.Exists(path)) throw new GameRuleException("此位置尚无存档。");
        return Parse(await File.ReadAllTextAsync(path));
    }

    public GameState Parse(string json)
    {
        if (json.Length > MaximumSaveCharacters) throw new GameRuleException("存档过大，无法读取。");
        try
        {
            var state = JsonSerializer.Deserialize<GameState>(json, Json) ?? throw new JsonException();
            GameEngine.Validate(state);
            var templates = WorldFactory.Templates();
            if (state.Sects.Count == 6) templates = templates.Take(6).ToList();
            var templateIds = templates.Select(s => s.Id).Order();
            if (!templateIds.SequenceEqual(state.Sects.Select(s => s.Id).Order())) throw new GameRuleException("存档宗门标识与本游戏不匹配。");
            foreach (var template in templates)
            {
                var sect = state.Sects.Single(s => s.Id == template.Id);
                if (sect.Name != template.Name || sect.Color != template.Color || sect.Sigil != template.Sigil || sect.Portrait != template.Portrait || sect.Trait != template.Trait || sect.Motto != template.Motto || sect.Personality != template.Personality)
                    throw new GameRuleException("存档中的宗门定义与本游戏不匹配。");
            }
            // These labels also appear in SVG and select controls. Imported saves must
            // not turn a local game's content boundary into markup/script injection.
            if (state.Regions.Any(r => r.Index < 0 || r.Index >= state.Regions.Count || !SafeLabel(r.Name) || !SafeLabel(r.Terrain) || r.Id != $"r{r.Index + 1:00}")
                || state.People.Any(p => !SafeLabel(p.Role) || !SafeLabel(p.Temperament))
                || state.Sects.Any(s => !WorldFactory.Policies.Contains(s.Policy)))
                throw new GameRuleException("存档包含无效的地图或人物标记。");
            if (state.Mode is not ("llm" or "rules") || state.Status is not ("playing" or "victory") || state.Difficulty is not ("normal" or "story" or "hard") || state.CommandsLeft is < 0 or > 2 || state.NextId <= state.History.Select(e => e.Id).Concat(state.People.Select(p => p.Id)).DefaultIfEmpty(0).Max()) throw new GameRuleException("存档元数据无效。");
            if (state.SchemaVersion == 1)
            {
                // Upgrade appearance only; never advance rule RNG or rewrite the old world's factions and land.
                foreach (var person in state.People)
                    person.Portrait = (int)((unchecked((uint)person.Id * 2654435761u) ^ unchecked((uint)state.Seed)) % WorldFactory.PortraitCount);
                state.SchemaVersion = 2;
            }
            return state;
        }
        catch (Exception error) when (error is JsonException or NullReferenceException or InvalidOperationException or ArgumentException)
        { throw new GameRuleException("存档损坏或格式不兼容；原有世界未被替换。"); }
    }

    private static bool SafeLabel(string value) => value.Length is > 0 and < 100 && !value.Any(c => c is '<' or '>' or '&' or '"' or '\'' || char.IsControl(c));

    public async Task<List<SaveSummary>> ListAsync()
    {
        List<SaveSummary> summaries = [];
        foreach (var slot in Slots)
        {
            var path = GetPath(slot);
            if (!File.Exists(path)) continue;
            try
            {
                var world = await LoadAsync(slot);
                summaries.Add(new SaveSummary(slot, world.Player.Name, world.Month, world.Status, File.GetLastWriteTimeUtc(path)));
            }
            catch (Exception error) when (error is GameRuleException or IOException) { summaries.Add(new SaveSummary(slot, "存档无法读取", 0, "invalid", File.GetLastWriteTimeUtc(path))); }
        }
        return summaries;
    }
}

public sealed record SaveSummary(string Slot, string Sect, int Month, string Status, DateTime UpdatedUtc);
