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

    /// <summary>Health left. 0 means fainted — the creature cannot be sent out.</summary>
    public ushort CurrentHP { get; private set; }

    public bool IsFainted => CurrentHP == 0;

    /// <summary>0–1. Drives the catch formula and the companion's health bar.</summary>
    public float HealthFraction => MaxHP == 0 ? 0f : Math.Clamp(CurrentHP / (float)MaxHP, 0f, 1f);

    /// <summary>
    ///     The element a creature attacks with. Dual-element species defend with both but
    ///     only ever strike with the first, so the matchup a player has to think about when
    ///     choosing who to send out stays a single question.
    /// </summary>
    public ElementType PrimaryElement =>
        Schema?.Elements is { Count: > 0 } elements ? elements[0] : ElementType.Neutral;

    // Combat stats grow off the same base numbers as HP but on a flatter curve, so a level
    // gap matters without a level-50 creature one-shotting everything a level-40 meets.
    public ushort Attack => Derive(Schema.Stats.Attack);
    public ushort Defense => Derive(Schema.Stats.Defense);
    public ushort Speed => Derive(Schema.Stats.Speed);

    private ushort Derive(byte baseStat) => (ushort)(5 + baseStat * Level / 25);

    /// <summary>Refills health. Used on respawn and by out-of-combat regeneration.</summary>
    public void Heal(int amount = int.MaxValue)
    {
        CurrentHP = (ushort)Math.Clamp(CurrentHP + (long)amount, 0, MaxHP);
    }

    /// <summary>
    ///     Forces health to an exact value. Used where something outside owns the number —
    ///     a wild creature's health lives on its NPC, because that is what weapons move.
    /// </summary>
    public void SetHealth(int value)
    {
        CurrentHP = (ushort)Math.Clamp(value, 0, MaxHP);
    }

    /// <summary>Applies damage. Returns true if this blow knocked the creature out.</summary>
    public bool TakeDamage(int amount)
    {
        CurrentHP = (ushort)Math.Clamp(CurrentHP - (long)amount, 0, MaxHP);
        return IsFainted;
    }

    public static BeastData Create(Player player, ushort id, byte level = 1)
    {
        var schema = Wildlore.Database.Get(id);
        var data = new BeastData
        {
            ID = id,
            Level = level,
            TotalEXP = ExperienceTable.TotalExpForLevel(level, schema.GrowthRate),
            _caughtBy = player.name,
            _caughtDate = DateTime.Now,
            IsRare = Main.rand.NextBool(Wildlore.RareChance)
        };

        data.Heal();
        return data;
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
            var before = MaxHP;
            Level++;
            levelsGained++;

            // Hand over the extra capacity the level brought, so gaining a level in a fight
            // reads as a small reward rather than as nothing at all.
            Heal(MaxHP - before);
        }
    }

    /// <summary>Returns the species ID this creature should evolve into, or 0 if it should not.</summary>
    public ushort GetQueuedEvolution()
    {
        return Wildlore.Database.GetEvolutionAtLevel(ID, Level);
    }

    public void EvolveInto(ushort id)
    {
        // Max HP jumps when the species changes, so carry the wound across as a fraction
        // rather than as a raw number. Evolving mid-fight should not be a free full heal,
        // and it should never leave the creature on more HP than it can hold.
        var fraction = HealthFraction;

        ID = id;

        CurrentHP = (ushort)Math.Max(1, (int)Math.Round(MaxHP * fraction));
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
            ["hp"] = CurrentHP,
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

        // Saves written before health existed, and saves written against a different stat
        // curve, both land here — clamping covers each of them.
        data.CurrentHP = tag.TryGet<ushort>("hp", out var hp)
            ? (ushort)Math.Min(hp, data.MaxHP)
            : data.MaxHP;

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
    public const int BitHP = 1 << 5;

    public const int AllFields = BitID | BitLevel | BitIsRare | BitNickname | BitEXP | BitHP;

    public void NetWrite(BinaryWriter writer, int fields = AllFields)
    {
        writer.Write7BitEncodedInt(fields);
        if ((fields & BitID) != 0) writer.Write7BitEncodedInt(ID);
        if ((fields & BitLevel) != 0) writer.Write(Level);
        if ((fields & BitIsRare) != 0) writer.Write(IsRare);
        if ((fields & BitNickname) != 0) writer.Write(Nickname ?? string.Empty);
        if ((fields & BitEXP) != 0) writer.Write(TotalEXP);
        if ((fields & BitHP) != 0) writer.Write7BitEncodedInt(CurrentHP);
    }

    public BeastData NetRead(BinaryReader reader)
    {
        var fields = reader.Read7BitEncodedInt();
        if ((fields & BitID) != 0) ID = (ushort)reader.Read7BitEncodedInt();
        if ((fields & BitLevel) != 0) Level = reader.ReadByte();
        if ((fields & BitIsRare) != 0) IsRare = reader.ReadBoolean();
        if ((fields & BitNickname) != 0) Nickname = reader.ReadString();
        if ((fields & BitEXP) != 0) TotalEXP = reader.ReadInt32();

        // Read after level and ID, because both feed MaxHP and the clamp depends on it.
        if ((fields & BitHP) != 0)
            CurrentHP = (ushort)Math.Clamp(reader.Read7BitEncodedInt(), 0, MaxHP);

        return this;
    }

    #endregion
}
