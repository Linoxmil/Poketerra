using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Wildlore.ID;

namespace Wildlore.Core;

/// <summary>
///     Loads and exposes the static species data for every creature in the mod.
///     Backed by a single JSON file so that new species can be added without touching C#.
/// </summary>
public class BeastDatabase
{
    [JsonProperty("beasts")]
    public ReadOnlyDictionary<ushort, BeastSchema> Beasts { get; private set; }

    public static BeastDatabase Parse(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return JsonConvert.DeserializeObject<BeastDatabase>(reader.ReadToEnd());
    }

    public BeastSchema Get(ushort id)
    {
        return Beasts.GetValueOrDefault(id);
    }

    /// <summary>
    ///     Resolves the display name from the localization files, so species names are translatable.
    ///     Key: <c>Mods.Wildlore.Beasts.{Identifier}.DisplayName</c>
    /// </summary>
    public static LocalizedText GetLocalizedName(BeastSchema schema)
    {
        return Language.GetText($"Mods.Wildlore.Beasts.{schema.Identifier}.DisplayName");
    }

    public static string GetLocalizedNameDirect(BeastSchema schema)
    {
        return Language.GetTextValue($"Mods.Wildlore.Beasts.{schema.Identifier}.DisplayName");
    }

    public static string GetLoreDirect(BeastSchema schema)
    {
        return Language.GetTextValue($"Mods.Wildlore.Beasts.{schema.Identifier}.Lore");
    }

    /// <summary>
    ///     Returns the species this creature should become at the given level, or 0 if none.
    /// </summary>
    public ushort GetEvolutionAtLevel(ushort id, byte level)
    {
        var schema = Get(id);
        if (schema?.Evolution == null) return 0;
        return schema.Evolution.AtLevel <= level && schema.Evolution.ID != id
            ? schema.Evolution.ID
            : (ushort)0;
    }

    public class BeastSchema
    {
        /// <summary>Internal, non-localized name. Must match the sprite filename in Assets/Beasts/.</summary>
        [JsonProperty("name")]
        public string Identifier { get; private set; }

        [JsonProperty("elements")]
        public List<ElementType> Elements { get; private set; } = [];

        /// <summary>Higher = easier to catch. Range 1-255.</summary>
        [JsonProperty("catchRate")]
        public byte CatchRate { get; private set; } = 45;

        [JsonProperty("baseExp")]
        public ushort BaseExp { get; private set; } = 45;

        [JsonProperty("growthRate")]
        public GrowthGroup GrowthRate { get; private set; } = GrowthGroup.Medium;

        [JsonProperty("biome")]
        public SpawnBiome Biome { get; private set; } = SpawnBiome.Forest;

        /// <summary>Relative spawn weight within its biome. 1.0 = common.</summary>
        [JsonProperty("spawnWeight")]
        public float SpawnWeight { get; private set; } = 1f;

        [JsonProperty("movement")]
        public MovementStyle Movement { get; private set; } = MovementStyle.Walker;

        [JsonProperty("stats")]
        public StatsSchema Stats { get; private set; } = new();

        [JsonProperty("evolution")]
        public EvolutionSchema Evolution { get; private set; }
    }

    public class StatsSchema
    {
        [JsonProperty("hp")] public byte HP { get; set; } = 45;
        [JsonProperty("attack")] public byte Attack { get; set; } = 45;
        [JsonProperty("defense")] public byte Defense { get; set; } = 45;
        [JsonProperty("speed")] public byte Speed { get; set; } = 45;
    }

    public class EvolutionSchema
    {
        [JsonProperty("id")] public ushort ID { get; set; }
        [JsonProperty("atLevel")] public byte AtLevel { get; set; }
    }
}
