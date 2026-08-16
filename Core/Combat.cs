using Terraria.Localization;
using Wildlore.Content.NPCs;
using Wildlore.ID;

namespace Wildlore.Core;

/// <summary>
///     Shared damage maths for creature fights, so the companion striking a wild creature and
///     a wild creature striking back run through exactly the same numbers.
/// </summary>
public static class Combat
{
    /// <summary>
    ///     Defence shaves half its value off the blow, and a floor keeps a heavily armoured
    ///     target from being immune. Deliberately flat: a fight should take several exchanges
    ///     at every level, because the point of one is usually to weaken something enough to
    ///     catch it rather than to end it.
    /// </summary>
    public static int Damage(int attack, int defence, float elementMultiplier)
    {
        var raw = Math.Max(attack * 0.3f, attack - defence * 0.5f);
        var rolled = raw * elementMultiplier * Main.rand.NextFloat(0.9f, 1.1f);

        return Math.Max(1, (int)rolled);
    }

    /// <summary>
    ///     The element to defend with. Wildlore creatures carry their own; vanilla enemies get
    ///     one assigned from a short list of the obvious cases, then fall back to Neutral.
    ///     A partial list is deliberate — guessing wrong on an obscure enemy is worse than
    ///     treating it as unaligned.
    /// </summary>
    public static IReadOnlyList<ElementType> ElementsOf(NPC npc)
    {
        if (npc.ModNPC is BeastNPC { Data: not null } beast)
            return beast.Schema.Elements;

        return [VanillaElement(npc)];
    }

    private static ElementType VanillaElement(NPC npc) => npc.type switch
    {
        NPCID.FireImp or NPCID.Hellbat or NPCID.LavaSlime or NPCID.Demon or NPCID.VoodooDemon
            => ElementType.Ember,

        NPCID.IceSlime or NPCID.SnowFlinx or NPCID.IceBat or NPCID.ZombieEskimo
            => ElementType.Frost,

        NPCID.EaterofSouls or NPCID.Crimera or NPCID.BloodCrawler or NPCID.FaceMonster
            => ElementType.Gloom,

        NPCID.ManEater or NPCID.JungleSlime or NPCID.Hornet or NPCID.Snatcher
            => ElementType.Verdant,

        NPCID.Shark or NPCID.PinkJellyfish or NPCID.BlueJellyfish or NPCID.Crab or NPCID.Squid
            => ElementType.Tide,

        NPCID.GraniteGolem or NPCID.RockGolem or NPCID.DiggerHead
            => ElementType.Stone,

        NPCID.Harpy
            => ElementType.Storm,

        _ => ElementType.Neutral
    };

    /// <summary>
    ///     EXP handed over for defeating something.
    ///     A wild Wildlore creature pays out its species' own yield scaled by its level; a
    ///     vanilla enemy is valued from what it actually took to kill. Both land in the same
    ///     rough range so neither becomes the only sensible way to train.
    /// </summary>
    public static int ExperienceFrom(NPC npc)
    {
        if (npc.ModNPC is BeastNPC { Data: not null } beast)
            return ExperienceFromBeast(beast.Data);

        return Math.Max(1, npc.lifeMax / 3 + npc.defense * 3 + 10);
    }

    /// <summary>
    ///     What a wild creature is worth. Paid out on a successful catch rather than on a
    ///     kill, because a companion is not allowed to finish one off.
    /// </summary>
    public static int ExperienceFromBeast(BeastData beast)
    {
        return Math.Max(1, beast.Schema.BaseExp * beast.Level / 2);
    }

    /// <summary>
    ///     Puts the effectiveness call-out on screen. Even trades say nothing — the text is
    ///     there to flag the two cases worth reacting to, not to narrate every hit.
    /// </summary>
    public static void AnnounceEffectiveness(Rectangle where, float multiplier)
    {
        var key = ElementChart.EffectivenessKey(multiplier);
        if (key == null) return;

        var colour = multiplier > 1f ? new Color(255, 210, 120) : new Color(150, 160, 180);

        CombatText.NewText(where, colour, Language.GetTextValue($"Mods.Wildlore.Combat.{key}"));
    }
}
