using Terraria.ModLoader.IO;
using Wildlore.Content.Projectiles;
using Wildlore.ID;

namespace Wildlore.Core;

/// <summary>
///     Per-player state: the active party, which creature is currently out, and the discovery log.
/// </summary>
public class WildlorePlayer : ModPlayer
{
    public const int PartySize = 6;

    public readonly BeastData[] Party = new BeastData[PartySize];
    public readonly Dictionary<ushort, LoreEntryStatus> Lore = new();

    /// <summary>Index into <see cref="Party" /> of the creature currently summoned, or -1 if none.</summary>
    public int ActiveSlot { get; private set; } = -1;

    public static WildlorePlayer LocalPlayer => Main.LocalPlayer.GetModPlayer<WildlorePlayer>();

    public int NextFreeSlot()
    {
        for (var i = 0; i < PartySize; i++)
            if (Party[i] == null)
                return i;
        return -1;
    }

    /// <summary>Adds a creature to the party. Returns false if the party is full.</summary>
    public bool AddToParty(BeastData data)
    {
        var slot = NextFreeSlot();
        if (slot == -1) return false;
        Party[slot] = data;
        UpdateLore(data.ID, LoreEntryStatus.Caught);
        return true;
    }

    public void UpdateLore(ushort id, LoreEntryStatus status)
    {
        // Never downgrade an entry: Caught outranks Seen.
        if (Lore.TryGetValue(id, out var existing) && existing >= status) return;
        Lore[id] = status;
    }

    /// <summary>Summons the creature in the given slot as a following pet. Pass -1 to recall.</summary>
    public void SetActive(int slot)
    {
        if (slot >= PartySize) return;

        // Despawn any existing companion belonging to this player.
        foreach (var proj in Main.ActiveProjectiles)
            if (proj.owner == Player.whoAmI && proj.ModProjectile is BeastPet)
                proj.Kill();

        ActiveSlot = slot;
        if (slot < 0 || Party[slot] == null)
        {
            ActiveSlot = -1;
            return;
        }

        if (Main.myPlayer != Player.whoAmI) return;

        Projectile.NewProjectile(Player.GetSource_Misc("WildloreSummon"), Player.Center,
            Vector2.Zero, ModContent.ProjectileType<BeastPet>(), 0, 0, Player.whoAmI, slot);
    }

    public override void SaveData(TagCompound tag)
    {
        var partyTags = new List<TagCompound>();
        for (var i = 0; i < PartySize; i++)
        {
            if (Party[i] == null) continue;
            var entry = Party[i].SerializeData();
            entry["slot"] = i;
            partyTags.Add(entry);
        }

        if (partyTags.Count > 0) tag["party"] = partyTags;

        if (Lore.Count > 0)
        {
            tag["loreIds"] = Lore.Keys.Select(k => (int)k).ToList();
            tag["loreStatus"] = Lore.Values.Select(v => (byte)v).ToList();
        }
    }

    public override void LoadData(TagCompound tag)
    {
        if (tag.TryGet<List<TagCompound>>("party", out var partyTags))
            foreach (var entry in partyTags)
            {
                var slot = entry.GetInt("slot");
                if (slot < 0 || slot >= PartySize) continue;
                Party[slot] = BeastData.Load(entry);
            }

        if (!tag.TryGet<List<int>>("loreIds", out var ids) ||
            !tag.TryGet<List<byte>>("loreStatus", out var statuses)) return;

        for (var i = 0; i < ids.Count && i < statuses.Count; i++)
            Lore[(ushort)ids[i]] = (LoreEntryStatus)statuses[i];
    }
}
