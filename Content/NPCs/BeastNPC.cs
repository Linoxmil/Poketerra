using ReLogic.Content;
using Terraria.DataStructures;
using Terraria.Localization;
using Wildlore.Content.Projectiles;
using Wildlore.ID;

namespace Wildlore.Content.NPCs;

/// <summary>
///     A single ModNPC class instantiated once per species at load time.
///     <c>[Autoload(false)]</c> stops tModLoader from registering it automatically —
///     <see cref="BeastLoader" /> constructs one instance per database entry instead.
///     This is what lets you add species by editing JSON rather than writing a class each time.
/// </summary>
[Autoload(false)]
public class BeastNPC(ushort id, BeastDatabase.BeastSchema schema) : ModNPC
{
    private Asset<Texture2D> _texture;
    private int _hoverTimer;

    protected override bool CloneNewInstances => true;

    public override string Name { get; } = schema.Identifier + "NPC";

    public override string Texture { get; } = "Wildlore/Assets/Beasts/" + schema.Identifier;

    public override LocalizedText DisplayName => BeastDatabase.GetLocalizedName(Schema);

    public ushort ID { get; } = id;

    public BeastDatabase.BeastSchema Schema { get; } = schema;

    /// <summary>Rolled on spawn by the server, then synced to clients via ExtraAI.</summary>
    public BeastData Data { get; set; }

    /// <summary>True once a Snare Orb has connected and the creature is being pulled in.</summary>
    public bool BeingCaptured { get; private set; }

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = Wildlore.SpriteFrames;

        // Hidden from the vanilla bestiary; the mod ships its own discovery log.
        NPCID.Sets.NPCBestiaryDrawOffset.Add(NPC.type, new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true });
    }

    /// <summary>
    ///     Walked frames are driven here rather than by an AnimationType: borrowing a vanilla
    ///     NPC's animation would index frames against that NPC's sheet, which runs straight off
    ///     the end of a two-frame one.
    /// </summary>
    public override void FindFrame(int frameHeight)
    {
        NPC.frame.Height = frameHeight;

        NPC.frameCounter += Math.Abs(NPC.velocity.X) > 0.1f ? 0.15 : 0.05;
        if (NPC.frameCounter >= Wildlore.SpriteFrames) NPC.frameCounter = 0;

        NPC.frame.Y = (int)NPC.frameCounter * frameHeight;
    }

    public override void SetDefaults()
    {
        NPC.width = 32;
        NPC.height = 32;
        NPC.lifeMax = 250;
        NPC.damage = 0;
        NPC.defense = 0;
        NPC.HitSound = SoundID.NPCHit1;
        NPC.value = 0f;
        NPC.knockBackResist = 0.75f;
        NPC.npcSlots = 0.2f;
        NPC.friendly = true;
        NPC.aiStyle = NPCAIStyleID.Passive;
        AIType = NPCID.Bunny;
    }

    public override void OnSpawn(IEntitySource source)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient) return;

        var nearest = Player.FindClosest(NPC.Center, NPC.width, NPC.height);
        Data = BeastData.Create(Main.player[nearest], ID, (byte)Main.rand.Next(2, 8));
        NPC.netUpdate = true;
    }

    public override void AI()
    {
        if (NPC.life < NPC.lifeMax) NPC.life = NPC.lifeMax;

        if (BeingCaptured)
        {
            // Paced to run out at the same moment the orb's snare finishes closing, so the
            // creature is not already gone while the ring is still contracting on it.
            NPC.scale *= 0.91f;
            NPC.alpha = Math.Min(255, NPC.alpha + 6);
            if (NPC.scale < 0.02f)
            {
                NPC.active = false;
                NPC.netUpdate = true;
            }

            return;
        }

        if (Data is { IsRare: true }) RareSparkle();
    }

    private void RareSparkle()
    {
        Lighting.AddLight(NPC.Center, 0.5f, 0.45f, 0.2f);
        if (!Main.rand.NextBool(12)) return;
        var dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.TreasureSparkle);
        dust.velocity = NPC.velocity;
        dust.noGravity = true;
    }

    /// <summary>Called by the Snare Orb once a catch succeeds. Plays the shrink-and-vanish effect.</summary>
    public void Capture()
    {
        if (BeingCaptured) return;
        BeingCaptured = true;
        NPC.noGravity = true;
        NPC.velocity = Vector2.Zero;
        NPC.netUpdate = true;
    }

    // Only Snare Orbs may interact — creatures are not combat targets.
    public override bool? CanBeHitByProjectile(Projectile projectile)
    {
        return projectile.ModProjectile is SnareOrbProjectile;
    }

    public override bool CanBeHitByItem(Player player, Item item)
    {
        return false;
    }

    public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position)
    {
        return false;
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        Data.NetWrite(writer, BeastData.BitLevel | BeastData.BitIsRare);
        writer.Write(BeingCaptured);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        Data ??= new BeastData { ID = ID, Level = 1 };
        Data.NetRead(reader);
        BeingCaptured = reader.ReadBoolean();
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        _texture ??= ModContent.Request<Texture2D>(Texture);

        // Register as Seen after the cursor rests on the creature for ~1 second.
        if (!NPC.IsABestiaryIconDummy && _hoverTimer != -1 && Main.myPlayer < Main.maxPlayers)
        {
            var box = NPC.Hitbox;
            box.Offset((int)-screenPos.X, (int)-screenPos.Y);
            _hoverTimer = box.Contains(Main.MouseScreen.ToPoint()) ? _hoverTimer + 1 : 0;
            if (_hoverTimer >= 60)
            {
                WildlorePlayer.LocalPlayer.UpdateLore(ID, LoreEntryStatus.Seen);
                _hoverTimer = -1;
            }
        }

        var effects = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        var origin = NPC.frame.Size() / 2f;
        spriteBatch.Draw(_texture.Value, NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY),
            NPC.frame, drawColor * (NPC.alpha / 255f == 1 ? 1 : 1 - NPC.alpha / 255f),
            NPC.rotation, origin, NPC.scale, effects, 0f);

        return false;
    }
}
