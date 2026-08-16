namespace Wildlore.Content.NPCs;

/// <summary>
///     Builds one <see cref="BeastNPC" /> per database entry at load time and keeps a lookup
///     from species ID to the registered NPC type, so spawning code can go straight from a
///     JSON-defined species to a real NPC without a switch statement.
/// </summary>
public static class BeastLoader
{
    private static readonly Dictionary<ushort, int> IdToNpcType = new();
    private static readonly Dictionary<ushort, BeastNPC> Instances = new();

    public static IReadOnlyDictionary<ushort, int> Registry => IdToNpcType;

    public static void Register(Mod mod, BeastDatabase database)
    {
        foreach (var (id, schema) in database.Beasts)
        {
            var npc = new BeastNPC(id, schema);
            mod.AddContent(npc);
            Instances[id] = npc;
        }
    }

    /// <summary>
    ///     Must run after content loading is finished, since NPC.Type is only assigned then.
    ///     Called from <see cref="Wildlore.PostSetupContent" />.
    /// </summary>
    public static void BuildRegistry()
    {
        foreach (var (id, instance) in Instances)
            IdToNpcType[id] = instance.Type;
    }

    public static int GetNPCType(ushort id)
    {
        return IdToNpcType.GetValueOrDefault(id, 0);
    }

    public static void Unload()
    {
        IdToNpcType.Clear();
        Instances.Clear();
    }
}
