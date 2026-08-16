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

/// <summary>Whether the player has merely encountered a species or actually caught one.</summary>
public enum LoreEntryStatus : byte
{
    Undiscovered,
    Seen,
    Caught
}
