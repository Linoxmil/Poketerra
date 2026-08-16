using Wildlore.Content.NPCs;
using Wildlore.Core;

namespace Wildlore;

public class Wildlore : Mod
{
    /// <summary>Highest level any creature can reach.</summary>
    public const byte MaxLevel = 50;

    /// <summary>1-in-N chance of a creature spawning in its rare colour variant.</summary>
    public const int RareChance = 2048;

    /// <summary>
    ///     Animation frames in every creature sheet, stacked vertically.
    ///     Every sheet in Assets/Beasts is authored to this, so the NPC and the companion
    ///     can share one number instead of each guessing at the texture height.
    /// </summary>
    public const int SpriteFrames = 2;

    public static Wildlore Instance => ModContent.GetInstance<Wildlore>();

    public static BeastDatabase Database { get; private set; }

    /// <summary>
    ///     Sends out the next creature in the party, or recalls the current one.
    ///     Until the party UI exists this is the only way to get a companion into the world.
    /// </summary>
    public static ModKeybind CycleCompanionKeybind { get; private set; }

    public override void Load()
    {
        CycleCompanionKeybind = KeybindLoader.RegisterKeybind(this, "CycleCompanion", "N");

        // The species database must exist before any content is registered,
        // since the NPC classes are constructed from its entries.
        using var stream = GetFileStream("Assets/Data/BeastDB.json");
        Database = BeastDatabase.Parse(stream);

        ExperienceTable.Build(MaxLevel);
        BeastLoader.Register(this, Database);

        Logger.Info($"Loaded {Database.Beasts.Count} species.");
    }

    public override void PostSetupContent()
    {
        // NPC types are only assigned once loading completes.
        BeastLoader.BuildRegistry();
    }

    public override void Unload()
    {
        BeastLoader.Unload();
        ExperienceTable.Unload();
        Database = null;
        CycleCompanionKeybind = null;
    }
}
