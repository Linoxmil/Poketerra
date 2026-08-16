using Wildlore.ID;

namespace Wildlore.Content.NPCs;

/// <summary>
///     Injects creatures into Terraria's spawn pool based on the player's current biome.
///     Vanilla builds a weighted pool each spawn attempt; adding entries here is the
///     supported way to make modded NPCs appear naturally.
/// </summary>
public class BeastSpawnSystem : ModSystem
{
    /// <summary>Global multiplier applied to every creature's spawn weight. Tune this first when balancing.</summary>
    private const float GlobalSpawnScale = 0.08f;

    public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
    {
        if (spawnInfo.PlayerInTown || spawnInfo.Invasion) return;

        var biome = ResolveBiome(spawnInfo);

        foreach (var (id, schema) in Wildlore.Database.Beasts)
        {
            if (schema.Biome != biome) continue;
            var type = BeastLoader.GetNPCType(id);
            if (type == 0) continue;
            pool[type] = schema.SpawnWeight * GlobalSpawnScale;
        }
    }

    private static SpawnBiome ResolveBiome(NPCSpawnInfo info)
    {
        var player = info.Player;

        // Order matters: check the most specific biomes first.
        if (player.ZoneCorrupt || player.ZoneCrimson) return SpawnBiome.Corruption;
        if (player.ZoneHallow) return SpawnBiome.Hallow;
        if (player.ZoneSnow) return SpawnBiome.Snow;
        if (player.ZoneJungle) return SpawnBiome.Jungle;
        if (player.ZoneDesert) return SpawnBiome.Desert;
        if (player.ZoneBeach) return SpawnBiome.Ocean;
        if (info.SpawnTileY > Main.worldSurface) return SpawnBiome.Underground;
        return SpawnBiome.Forest;
    }
}
