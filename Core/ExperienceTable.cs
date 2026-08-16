using Wildlore.ID;

namespace Wildlore.Core;

/// <summary>
///     Cumulative EXP required to reach a given level, per growth group.
///     Values are precomputed once at load and cached, since these are queried every EXP gain.
/// </summary>
public static class ExperienceTable
{
    private static readonly Dictionary<GrowthGroup, int[]> Cache = new();

    public static void Build(byte maxLevel)
    {
        Cache.Clear();
        foreach (GrowthGroup group in Enum.GetValues<GrowthGroup>())
        {
            var table = new int[maxLevel + 1];
            for (var lvl = 1; lvl <= maxLevel; lvl++)
                table[lvl] = Formula(lvl, group);
            Cache[group] = table;
        }
    }

    public static void Unload()
    {
        Cache.Clear();
    }

    public static int TotalExpForLevel(byte level, GrowthGroup group)
    {
        if (level < 1) return 0;
        if (!Cache.TryGetValue(group, out var table)) return Formula(level, group);
        return level >= table.Length ? table[^1] : table[level];
    }

    private static int Formula(int level, GrowthGroup group)
    {
        var n = level;
        return group switch
        {
            GrowthGroup.Fast => (int)(4 * Math.Pow(n, 3) / 5),
            GrowthGroup.Medium => (int)Math.Pow(n, 3),
            GrowthGroup.Slow => (int)(5 * Math.Pow(n, 3) / 4),
            _ => (int)Math.Pow(n, 3)
        };
    }
}
