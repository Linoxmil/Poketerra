namespace Wildlore.ID;

/// <summary>
///     Numeric IDs for every species. These MUST match the keys in Assets/Data/BeastDB.json.
///     Never renumber an existing entry — saved worlds reference these values.
/// </summary>
public static class BeastID
{
    public const ushort Emberkit = 1;
    public const ushort Cinderfox = 2;
    public const ushort Tidepup = 3;
    public const ushort Brinehound = 4;
    public const ushort Mossling = 5;
    public const ushort Thornbeast = 6;

    public const ushort Count = 6;
}
