using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Wildlore.Core;
using Wildlore.ID;

namespace Wildlore.Content.NPCs;

/// <summary>
///     Injects creatures into Terraria's spawn pool based on the player's current biome.
///     Vanilla builds a weighted pool each spawn attempt; adding entries here is the
///     supported way to make modded NPCs appear naturally.
///     <para>
///     A GlobalNPC despite the name: EditSpawnPool hangs off GlobalNPC, not ModSystem.
///     </para>
/// </summary>
public class BeastSpawnSystem : GlobalNPC
{
    /// <summary>
    ///     Roughly what share of nearby spawns should be Wildlore creatures, before the world
    ///     size is taken into account. Vanilla's own entry sits at weight 1, so 0.30 means
    ///     roughly one creature for every three ordinary spawns. Tune this first when balancing.
    /// </summary>
    public static float GlobalSpawnScale = 0.30f;

    /// <summary>
    ///     How much more often the game reaches into the spawn pool while a player is out in
    ///     the world.
    ///     <para>
    ///     Weighting the pool only decides what comes out of it — on a quiet daytime surface
    ///     Terraria barely reaches into it at all, so weight alone leaves a meadow empty.
    ///     This raises vanilla spawns slightly too; set it to 0 to leave Terraria's own pacing
    ///     completely untouched.
    ///     </para>
    /// </summary>
    public static float SpawnRateBoost = 0.35f;

    /// <summary>
    ///     Creatures allowed alive near one player at once, before the world size scales it.
    ///     Without a ceiling a generous spawn scale eats the NPC budget and vanilla stops
    ///     spawning anything at all.
    /// </summary>
    public static int NearbyCap = 8;

    private const float NearbyRange = 2200f;

    /// <summary>
    ///     A large world carries a lot more creatures than a small one, and noticeably more
    ///     around the player too. Squaring the ratio makes the difference something you feel
    ///     rather than something you could only measure. Clamped at 1.5 so creatures stay a
    ///     part of the world rather than becoming most of what spawns in it.
    /// </summary>
    public static float WorldPopulation =>
        Math.Clamp(Main.maxTilesX / 6400f * (Main.maxTilesX / 6400f), 0.5f, 1.5f);

    public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
    {
        if (spawnInfo.PlayerInTown || spawnInfo.Invasion) return;
        if (CountNearby(spawnInfo.Player) >= NearbyCap * WorldPopulation) return;

        var biome = ResolveBiome(spawnInfo);

        // Total the matching weights first. A species' weight then only decides its share of
        // the biome, so a biome that happens to hold four species is not twice as busy as one
        // that holds two — that used to fall out of the numbers by accident.
        var total = 0f;
        foreach (var (_, schema) in Wildlore.Database.Beasts)
            if (schema.Biome == biome)
                total += schema.SpawnWeight;

        if (total <= 0f) return;

        var share = GlobalSpawnScale * WorldPopulation;

        foreach (var (id, schema) in Wildlore.Database.Beasts)
        {
            if (schema.Biome != biome) continue;
            var type = BeastLoader.GetNPCType(id);
            if (type == 0) continue;
            pool[type] = schema.SpawnWeight / total * share;
        }
    }

    public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
    {
        if (SpawnRateBoost <= 0f) return;

        // spawnRate is a one-in-N chance, so dividing it makes spawns more frequent.
        var boost = 1f + SpawnRateBoost * WorldPopulation;

        spawnRate = Math.Max(1, (int)(spawnRate / boost));
        maxSpawns = (int)(maxSpawns * (1f + (boost - 1f) * 0.5f));
    }

    private static int CountNearby(Player player)
    {
        var count = 0;

        foreach (var npc in Main.ActiveNPCs)
            if (npc.ModNPC is BeastNPC && npc.WithinRange(player.Center, NearbyRange))
                count++;

        return count;
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
