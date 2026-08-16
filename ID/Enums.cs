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

namespace Wildlore.ID;

public enum ElementType : byte
{
    Neutral,
    Ember,
    Tide,
    Verdant,
    Storm,
    Frost,
    Stone,
    Gloom
}

public enum GrowthGroup : byte
{
    Fast,
    Medium,
    Slow
}

public enum SpawnBiome : byte
{
    Forest,
    Desert,
    Snow,
    Jungle,
    Ocean,
    Underground,
    Corruption,
    Hallow
}

/// <summary>How a species gets around. Picks which wander behaviour BeastNPC runs.</summary>
public enum MovementStyle : byte
{
    /// <summary>Walks the ground, turns at walls, hops over one-block ledges.</summary>
    Walker,

    /// <summary>Springs in arcs with a pause between each landing.</summary>
    Hopper,

    /// <summary>Free flight, ignores gravity, ranges widest.</summary>
    Flyer,

    /// <summary>Hangs in the air and drifts. Slower and eerier than a flyer.</summary>
    Drifter,

    /// <summary>Swims while submerged, flops helplessly out of water.</summary>
    Swimmer
}

/// <summary>Whether the player has merely encountered a species or actually caught one.</summary>
public enum LoreEntryStatus : byte
{
    Undiscovered,
    Seen,
    Caught
}
