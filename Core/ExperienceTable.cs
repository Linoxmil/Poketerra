using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
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

    /// <summary>
    ///     Cumulative EXP to reach <paramref name="level" />.
    ///     Each curve is a cubic plus a quadratic term: the cubic sets how punishing the late
    ///     levels are, the quadratic front-loads enough cost that level 2-5 are not instant.
    ///     All three are strictly increasing across the whole 1..MaxLevel range.
    /// </summary>
    private static int Formula(int level, GrowthGroup group)
    {
        double n = level;
        var cube = n * n * n;
        var square = n * n;

        return group switch
        {
            GrowthGroup.Fast => (int)(0.6 * cube + 8 * square),
            GrowthGroup.Medium => (int)(0.9 * cube + 12 * square),
            GrowthGroup.Slow => (int)(1.4 * cube + 16 * square),
            _ => (int)(0.9 * cube + 12 * square)
        };
    }
}
