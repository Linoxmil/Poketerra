using Terraria.ModLoader.IO;
using Wildlore.ID;

namespace Wildlore.Core;

/// <summary>
///     One individual creature owned by a player. Distinct from <see cref="BeastDatabase.BeastSchema" />,
///     which is the shared, immutable species definition.
/// </summary>
public class BeastData
{
    private const ushort Version = 0;

    private ushort _id;
    private string _caughtBy;
    private DateTime? _caughtDate;

    public byte Level = 1;
    public bool IsRare;
    public string Nickname;

    public ushort ID
    {
        get => _id;
        set
        {
            _id = value;
            Schema = Wildlore.Database.Get(value);
        }
    }

    /// <summary>Cached species definition. Refreshed automatically whenever <see cref="ID" /> changes.</summary>
    public BeastDatabase.BeastSchema Schema { get; private set; }

    public string InternalName => Schema.Identifier;

    public string LocalizedName => BeastDatabase.GetLocalizedNameDirect(Schema);

    public string DisplayName => string.IsNullOrEmpty(Nickname) ? LocalizedName : Nickname;

    public int TotalEXP { get; private set; }

    /// <summary>
    ///     Effective health at the current level.
    ///     A flat floor keeps a level-1 creature from being one-shot, the linear term makes
    ///     every level felt regardless of species, and the base-stat term is what separates a
    ///     Brinehound from an Emberkit. Tuned so a capped creature lands in the low hundreds,
    ///     which is the range a mid-game Terraria player is used to reading.
    /// </summary>
    public ushort MaxHP => (ushort)(15 + Level * 2 + Schema.Stats.HP * Level / 40);

    public static BeastData Create(Player player, ushort id, byte level = 1)
    {
        var schema = Wildlore.Database.Get(id);
        return new BeastData
        {
            ID = id,
            Level = level,
            TotalEXP = ExperienceTable.TotalExpForLevel(level, schema.GrowthRate),
            _caughtBy = player.name,
            _caughtDate = DateTime.Now,
            IsRare = Main.rand.NextBool(Wildlore.RareChance)
        };
    }

    public void GainExperience(int amount, out int levelsGained)
    {
        levelsGained = 0;
        if (Level >= Wildlore.MaxLevel) return;

        TotalEXP = Math.Clamp(TotalEXP + amount, 0,
            ExperienceTable.TotalExpForLevel(Wildlore.MaxLevel, Schema.GrowthRate));

        while (Level < Wildlore.MaxLevel &&
               TotalEXP >= ExperienceTable.TotalExpForLevel((byte)(Level + 1), Schema.GrowthRate))
        {
            Level++;
            levelsGained++;
        }
    }

    /// <summary>Returns the species ID this creature should evolve into, or 0 if it should not.</summary>
    public ushort GetQueuedEvolution()
    {
        return Wildlore.Database.GetEvolutionAtLevel(ID, Level);
    }

    public void EvolveInto(ushort id)
    {
        ID = id;
    }

    public BeastData ShallowCopy()
    {
        return (BeastData)MemberwiseClone();
    }

    #region Save / Load

    public TagCompound SerializeData()
    {
        var tag = new TagCompound
        {
            ["id"] = ID,
            ["lvl"] = Level,
            ["exp"] = TotalEXP,
            ["by"] = _caughtBy ?? string.Empty,
            ["version"] = Version
        };
        if (IsRare) tag["rare"] = true;
        if (!string.IsNullOrEmpty(Nickname)) tag["nick"] = Nickname;
        if (_caughtDate.HasValue) tag["caught"] = _caughtDate.Value.ToBinary();
        return tag;
    }

    public static BeastData Load(TagCompound tag)
    {
        var data = new BeastData
        {
            ID = (ushort)tag.GetShort("id"),
            Level = tag.GetByte("lvl"),
            _caughtBy = tag.GetString("by")
        };
        if (tag.TryGet<bool>("rare", out var rare)) data.IsRare = rare;
        if (tag.TryGet<string>("nick", out var nick)) data.Nickname = nick;
        if (tag.TryGet<long>("caught", out var caught)) data._caughtDate = DateTime.FromBinary(caught);
        data.TotalEXP = tag.TryGet<int>("exp", out var exp)
            ? exp
            : ExperienceTable.TotalExpForLevel(data.Level, data.Schema.GrowthRate);
        return data;
    }

    #endregion

    #region Network Sync

    // Bitmask so we only send fields that actually changed, instead of the whole object every tick.
    public const int BitID = 1 << 0;
    public const int BitLevel = 1 << 1;
    public const int BitIsRare = 1 << 2;
    public const int BitNickname = 1 << 3;
    public const int BitEXP = 1 << 4;

    public const int AllFields = BitID | BitLevel | BitIsRare | BitNickname | BitEXP;

    public void NetWrite(BinaryWriter writer, int fields = AllFields)
    {
        writer.Write7BitEncodedInt(fields);
        if ((fields & BitID) != 0) writer.Write7BitEncodedInt(ID);
        if ((fields & BitLevel) != 0) writer.Write(Level);
        if ((fields & BitIsRare) != 0) writer.Write(IsRare);
        if ((fields & BitNickname) != 0) writer.Write(Nickname ?? string.Empty);
        if ((fields & BitEXP) != 0) writer.Write(TotalEXP);
    }

    public BeastData NetRead(BinaryReader reader)
    {
        var fields = reader.Read7BitEncodedInt();
        if ((fields & BitID) != 0) ID = (ushort)reader.Read7BitEncodedInt();
        if ((fields & BitLevel) != 0) Level = reader.ReadByte();
        if ((fields & BitIsRare) != 0) IsRare = reader.ReadBoolean();
        if ((fields & BitNickname) != 0) Nickname = reader.ReadString();
        if ((fields & BitEXP) != 0) TotalEXP = reader.ReadInt32();
        return this;
    }

    #endregion
}
