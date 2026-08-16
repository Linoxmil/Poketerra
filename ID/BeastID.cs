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

/// <summary>
///     Numeric IDs for every species. These MUST match the keys in Assets/Data/BeastDB.json.
///     Never renumber an existing entry — saved worlds reference these values.
/// </summary>
public static class BeastID
{
    // Starter lines.
    public const ushort Emberkit = 1;
    public const ushort Cinderfox = 2;
    public const ushort Tidepup = 3;
    public const ushort Brinehound = 4;
    public const ushort Mossling = 5;
    public const ushort Thornbeast = 6;

    // Snow.
    public const ushort Frostnib = 7;
    public const ushort Rimehorn = 8;

    // Surface wind.
    public const ushort Gustling = 9;
    public const ushort Skyrend = 10;

    // Underground.
    public const ushort Pebblit = 11;
    public const ushort Cragmaw = 12;

    // Corruption.
    public const ushort Duskmoth = 13;
    public const ushort Nightmaw = 14;

    // Ocean.
    public const ushort Glimmerfin = 15;
    public const ushort Stormray = 16;

    // Hallow.
    public const ushort Sunmite = 17;
    public const ushort Prismhart = 18;

    // Desert.
    public const ushort Dunewisp = 19;
    public const ushort Sandwraith = 20;

    // Standalone species — no evolution, stronger than a base form from the start.
    public const ushort Emberdrift = 21;
    public const ushort Snowlurk = 22;
    public const ushort Bloomstag = 23;
    public const ushort Tidewarden = 24;

    public const ushort Count = 24;
}
