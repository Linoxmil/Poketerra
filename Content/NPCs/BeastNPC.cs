using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Wildlore.Content.Projectiles;
using Wildlore.Core;
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
    /// <summary>Ticks a creature keeps fighting back after it was last struck.</summary>
    private const int AggroMemory = 480;

    /// <summary>Ticks between its own blows.</summary>
    private const int StrikeInterval = 70;

    private const float StrikeRange = 96f;

    /// <summary>How close the player has to be for the name and level to appear.</summary>
    private const float LabelRange = 340f;

    private Asset<Texture2D> _texture;
    private int _hoverTimer;

    private int _aggroProjectile = -1;
    private int _aggroTimer;
    private int _strikeCooldown;

    private int _behaviourTimer;
    private int _hopCooldown;
    private bool _moving;

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

    public override void SetDefaults()
    {
        NPC.width = 32;
        NPC.height = 32;

        // A placeholder until OnSpawn rolls the individual and rescales it. Anything spawned
        // without going through OnSpawn still gets a survivable creature rather than a 0 HP one.
        NPC.lifeMax = 60;
        NPC.damage = 0;
        NPC.defense = 0;
        NPC.HitSound = SoundID.NPCHit1;
        NPC.DeathSound = SoundID.NPCDeath1;
        NPC.value = 0f;
        NPC.knockBackResist = 0.75f;
        NPC.npcSlots = 0.2f;
        NPC.friendly = true;

        // aiStyle 0 means no vanilla AI at all. Borrowing the bunny's left these creatures
        // standing still, because that AI is written around conditions a Wildlore creature
        // never meets. Everything below Wander() is ours, gravity included — Terraria still
        // does the tile collision and fills in collideX/collideY.
        NPC.aiStyle = 0;
    }

    /// <summary>
    ///     Walked frames are driven here rather than by an AnimationType: borrowing a vanilla
    ///     NPC's animation would index frames against that NPC's sheet, which runs straight off
    ///     the end of a four-frame one.
    /// </summary>
    public override void FindFrame(int frameHeight)
    {
        NPC.frame.Height = frameHeight;

        NPC.frameCounter += Math.Abs(NPC.velocity.X) > 0.1f ? 0.18 : 0.07;
        if (NPC.frameCounter >= Wildlore.SpriteFrames) NPC.frameCounter = 0;

        NPC.frame.Y = (int)NPC.frameCounter * frameHeight;
    }

    public override void OnSpawn(IEntitySource source)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient) return;

        var nearest = Player.FindClosest(NPC.Center, NPC.width, NPC.height);
        Data = BeastData.Create(Main.player[nearest], ID, (byte)Main.rand.Next(2, 8));

        NPC.direction = Main.rand.NextBool() ? -1 : 1;

        ApplyStats();
        NPC.netUpdate = true;
    }

    private void ApplyStats()
    {
        if (Data == null) return;

        NPC.lifeMax = Data.MaxHP;
        NPC.life = Math.Max(1, (int)Data.CurrentHP);
        NPC.defense = Data.Defense / 3;
        NPC.scale = Data.IsRare ? 1.15f : 1f;
    }

    public override void AI()
    {
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

        // NPC.life is what weapons and the companion actually move, so it is the source of
        // truth while the creature is wild. The catch roll reads the mirrored value.
        if (Data != null && Data.CurrentHP != NPC.life) Data.SetHealth(NPC.life);

        if (_aggroTimer > 0)
        {
            _aggroTimer--;
            Retaliate();
        }

        Wander();

        if (Data is { IsRare: true }) RareSparkle();
    }

    // -- Wandering -----------------------------------------------------------------

    /// <summary>
    ///     Idle life. A creature picks between standing still and moving off in some direction,
    ///     and switches every few seconds, so a meadow reads as inhabited rather than staged.
    ///     Once something has hurt it, it runs from the nearest player instead.
    /// </summary>
    private void Wander()
    {
        var frightened = _aggroTimer > 0 || NPC.life * 2 < NPC.lifeMax;

        if (--_behaviourTimer <= 0) ChooseBehaviour(frightened);

        if (frightened)
        {
            var player = Main.player[Player.FindClosest(NPC.Center, NPC.width, NPC.height)];
            if (player.active && NPC.WithinRange(player.Center, 460f))
                NPC.direction = NPC.Center.X < player.Center.X ? -1 : 1;
        }

        var speed = WanderSpeed() * (frightened ? 1.8f : 1f);

        switch (Schema.Movement)
        {
            case MovementStyle.Hopper: Hop(speed); break;
            case MovementStyle.Flyer: Drift(speed, 1.6f); break;
            case MovementStyle.Drifter: Drift(speed * 0.6f, 0.8f); break;
            case MovementStyle.Swimmer: Swim(speed); break;
            default: Walk(speed); break;
        }

        NPC.spriteDirection = NPC.direction;
    }

    private void ChooseBehaviour(bool frightened)
    {
        if (frightened)
        {
            _moving = true;
            _behaviourTimer = 60;
            return;
        }

        _moving = Main.rand.NextBool();
        _behaviourTimer = _moving ? Main.rand.Next(90, 240) : Main.rand.Next(45, 150);

        if (_moving) NPC.direction = Main.rand.NextBool() ? -1 : 1;
    }

    /// <summary>Movement speed off the Speed stat, so a fast species also looks fast idling.</summary>
    private float WanderSpeed()
    {
        var stat = Data?.Speed ?? 10;

        return 0.8f + Math.Clamp(stat / 30f, 0f, 2.4f);
    }

    private void ApplyGravity(float strength = 0.3f)
    {
        NPC.noGravity = false;
        NPC.velocity.Y += strength;
        if (NPC.velocity.Y > 10f) NPC.velocity.Y = 10f;
    }

    private bool Grounded => NPC.velocity.Y == 0f;

    private void Walk(float speed)
    {
        ApplyGravity();

        NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, _moving ? speed * NPC.direction : 0f, 0.07f);

        if (!NPC.collideX) return;

        // A wall is either something to step over or something to turn away from.
        NPC.direction *= -1;
        if (Grounded) NPC.velocity.Y = -4.6f;
    }

    private void Hop(float speed)
    {
        ApplyGravity();

        if (_hopCooldown > 0) _hopCooldown--;

        if (Grounded)
        {
            NPC.velocity.X *= 0.8f;

            if (_moving && _hopCooldown == 0)
            {
                NPC.velocity = new Vector2(NPC.direction * speed * 1.5f, -5.2f);
                _hopCooldown = Main.rand.Next(25, 55);
            }
        }

        if (NPC.collideX) NPC.direction *= -1;
    }

    private void Drift(float speed, float bob)
    {
        NPC.noGravity = true;

        // Offset by whoAmI so a group of them never bobs in lockstep.
        var sway = (float)Math.Sin(Main.GameUpdateCount * 0.03f + NPC.whoAmI) * bob;

        NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, _moving ? speed * NPC.direction : 0f, 0.04f);
        NPC.velocity.Y = MathHelper.Lerp(NPC.velocity.Y, sway, 0.05f);

        if (NPC.collideX) NPC.direction *= -1;
        if (NPC.collideY) NPC.velocity.Y *= -0.5f;
    }

    private void Swim(float speed)
    {
        var submerged = Collision.WetCollision(NPC.position, NPC.width, NPC.height);

        if (!submerged)
        {
            // Beached. Flop until gravity puts it back where it belongs.
            ApplyGravity(0.35f);
            NPC.velocity.X *= 0.94f;
            return;
        }

        NPC.noGravity = true;

        var sway = (float)Math.Sin(Main.GameUpdateCount * 0.02f + NPC.whoAmI) * 1.2f;

        NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, _moving ? speed * NPC.direction : 0f, 0.05f);
        NPC.velocity.Y = MathHelper.Lerp(NPC.velocity.Y, sway, 0.05f);

        if (NPC.collideX) NPC.direction *= -1;
    }

    /// <summary>
    ///     Wild creatures are passive until something hits them, then they hit back at whatever
    ///     companion is standing in front of them. There is no separate battle screen — the
    ///     fight happens where the creature lives.
    /// </summary>
    private void Retaliate()
    {
        if (_strikeCooldown > 0)
        {
            _strikeCooldown--;
            return;
        }

        if (_aggroProjectile < 0 || _aggroProjectile >= Main.maxProjectiles) return;

        var projectile = Main.projectile[_aggroProjectile];
        if (!projectile.active || projectile.ModProjectile is not BeastPet pet) return;

        // Party state lives only on its owner's machine, so that is where the blow lands.
        if (projectile.owner != Main.myPlayer || pet.Companion == null) return;
        if (!NPC.WithinRange(projectile.Center, StrikeRange)) return;

        var attacker = Data?.PrimaryElement ?? ElementType.Neutral;
        var multiplier = ElementChart.Multiplier(attacker, pet.Companion.Schema.Elements);
        var damage = Combat.Damage(Data?.Attack ?? 5, pet.Companion.Defense, multiplier);

        pet.TakeHit(damage, multiplier);

        NPC.velocity += NPC.DirectionTo(projectile.Center) * 2.5f;
        _strikeCooldown = StrikeInterval;
    }

    public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone)
    {
        if (projectile.ModProjectile is not BeastPet) return;

        _aggroProjectile = projectile.whoAmI;
        _aggroTimer = AggroMemory;
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

    // Only Snare Orbs and companions may interact — creatures are not targets for the
    // player's own weapons.
    public override bool? CanBeHitByProjectile(Projectile projectile)
    {
        return projectile.ModProjectile is SnareOrbProjectile or BeastPet;
    }

    public override bool? CanBeHitByItem(Player player, Item item)
    {
        return false;
    }

    public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position)
    {
        // Hidden until something has actually hurt it. Once a fight starts the bar is the
        // only way to judge whether the creature is worn down enough to throw an orb.
        return NPC.life >= NPC.lifeMax ? false : null;
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        Data.NetWrite(writer, BeastData.BitLevel | BeastData.BitIsRare | BeastData.BitHP);
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

    /// <summary>
    ///     Name and level float over a creature the player is standing near. Without this the
    ///     whole levelling system is invisible until something is already caught.
    /// </summary>
    public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (Data == null || NPC.IsABestiaryIconDummy || BeingCaptured) return;

        var local = Main.LocalPlayer;
        if (!local.active || !NPC.WithinRange(local.Center, LabelRange)) return;

        var label = $"{DisplayName.Value}  Lv.{Data.Level}";
        const float scale = 0.75f;

        var width = FontAssets.MouseText.Value.MeasureString(label).X * scale;
        var position = NPC.Top - screenPos + new Vector2(-width / 2f, -24f);
        var colour = Data.IsRare ? new Color(255, 226, 140) : Color.White;

        spriteBatch.DrawBorderString(label, position, colour, scale);
    }
}
