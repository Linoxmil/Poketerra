using ReLogic.Content;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Localization;
using Wildlore.Content.NPCs;
using Wildlore.ID;

namespace Wildlore.Content.Projectiles;

/// <summary>
///     The summoned companion. One projectile per player, reading its appearance from the
///     party slot stored in <c>Projectile.ai[0]</c> so a single class covers every species.
///     <para>
///     This is also where fights happen. Wildlore has no separate battle screen: the companion
///     picks its own targets, lunges at them, and takes hits back, all in the world the player
///     is standing in.
///     </para>
/// </summary>
public class BeastPet : ModProjectile
{
    private const float AggroRange = 320f;
    private const float LeashRange = 700f;
    private const int LungeTicks = 16;
    private const float LungeSpeed = 13f;
    private const int RegenInterval = 90;
    private const int OutOfCombatTicks = 300;
    private const int EvolveFlashTicks = 60;

    /// <summary>Below this fraction a wild creature is left alone — it is worn down enough to catch.</summary>
    private const float SpareThreshold = 0.2f;

    private Asset<Texture2D> _texture;
    private ushort _textureFor;
    private BeastData _data;

    private int _attackCooldown;
    private int _lungeTimer;
    private int _outOfCombat;
    private int _regenTimer;
    private int _evolveFlash;
    private float _lastMultiplier = 1f;

    public override string Texture => "Wildlore/Assets/Beasts/Placeholder";

    private int Slot => (int)Projectile.ai[0];

    /// <summary>The creature this companion is. Null until the owner's party has been read.</summary>
    public BeastData Companion => _data;

    public override void SetStaticDefaults()
    {
        // Animate() and PreDraw both read the frame count back off this, so setting it is
        // what makes a multi-frame species sheet draw one frame instead of the whole strip.
        Main.projFrames[Type] = Wildlore.SpriteFrames;
    }

    public override void SetDefaults()
    {
        Projectile.width = 32;
        Projectile.height = 32;

        Projectile.friendly = true;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 2;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
        Projectile.netImportant = true;

        // Damage is zero except during a lunge, so the companion never hurts anything just by
        // drifting into it. One hit per target per lunge.
        Projectile.damage = 0;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = LungeTicks + 4;
    }

    public override bool? CanCutTiles() => false;

    public override void AI()
    {
        var owner = Main.player[Projectile.owner];
        var modPlayer = owner.GetModPlayer<WildlorePlayer>();

        // Despawn if the owner died, recalled, or the slot emptied.
        if (!owner.active || owner.dead || Slot < 0 || Slot >= WildlorePlayer.PartySize ||
            modPlayer.Party[Slot] == null)
        {
            Projectile.Kill();
            return;
        }

        _data = modPlayer.Party[Slot];

        if (_data.IsFainted)
        {
            Faint(owner, modPlayer);
            return;
        }

        Projectile.timeLeft = 2; // Keep alive as long as the conditions above hold.

        if (_evolveFlash > 0) EvolveEffect();

        if (_attackCooldown > 0) _attackCooldown--;
        _outOfCombat++;

        if (_lungeTimer > 0)
        {
            _lungeTimer--;
            if (_lungeTimer == 0) EndLunge();
        }
        else
        {
            Follow(owner);

            // Target selection and every consequence of a fight — damage, EXP, evolution —
            // run on the owner's own machine, because that is the only place the party data
            // actually exists. Live party sync is still on the to-do list.
            if (Projectile.owner == Main.myPlayer) AcquireTarget(owner);

            Regenerate();
        }

        if (_data.IsRare) Lighting.AddLight(Projectile.Center, 0.4f, 0.36f, 0.16f);

        Animate();
    }

    // -- Movement ------------------------------------------------------------------

    private void Follow(Player owner)
    {
        var target = owner.Center + new Vector2(owner.direction * -32f, -16f);
        var toTarget = target - Projectile.Center;
        var distance = toTarget.Length();

        // Teleport if the player got too far ahead (through a portal, mount, etc).
        if (distance > 1200f)
        {
            Projectile.Center = owner.Center;
            Projectile.velocity = Vector2.Zero;
            return;
        }

        if (distance > 48f)
        {
            var speed = MathHelper.Clamp(distance / 24f, 2f, 12f);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity,
                toTarget.SafeNormalize(Vector2.Zero) * speed, 0.12f);
        }
        else
        {
            Projectile.velocity *= 0.9f;
            Projectile.velocity.Y += 0.15f; // Settle onto the ground rather than hovering.
        }

        if (Math.Abs(Projectile.velocity.X) > 0.2f)
            Projectile.spriteDirection = Projectile.velocity.X > 0 ? 1 : -1;
    }

    // -- Fighting ------------------------------------------------------------------

    private void AcquireTarget(Player owner)
    {
        if (_attackCooldown > 0 || _data == null) return;

        NPC best = null;
        var bestDistance = AggroRange;

        foreach (var npc in Main.ActiveNPCs)
        {
            if (!IsValidTarget(npc, owner)) continue;

            var distance = Vector2.Distance(Projectile.Center, npc.Center);
            if (distance > bestDistance) continue;

            best = npc;
            bestDistance = distance;
        }

        if (best != null) BeginLunge(best);
    }

    private static bool IsValidTarget(NPC npc, Player owner)
    {
        if (npc.dontTakeDamage || npc.immortal) return false;

        if (npc.ModNPC is BeastNPC beast)
        {
            // A companion softens wild creatures up only while its owner is actually holding
            // an orb — otherwise walking through a meadow would start six fights. And it stops
            // once the target is worn down, so it cannot finish off the creature you came for.
            return !beast.BeingCaptured
                   && npc.life > npc.lifeMax * SpareThreshold
                   && HoldingOrb(owner);
        }

        return !npc.friendly && npc.damage > 0 && npc.WithinRange(owner.Center, LeashRange);
    }

    private static bool HoldingOrb(Player owner)
    {
        var held = owner.HeldItem;
        if (held == null || held.IsAir || held.shoot <= ProjectileID.None) return false;

        return ModContent.GetModProjectile(held.shoot) is SnareOrbProjectile;
    }

    private void BeginLunge(NPC target)
    {
        _lungeTimer = LungeTicks;
        _outOfCombat = 0;

        // Faster creatures come back around sooner. Speed is the stat that decides how often
        // you act, rather than a second damage number.
        _attackCooldown = Math.Clamp(70 - _data.Speed / 3, 26, 70);

        _lastMultiplier = ElementChart.Multiplier(_data.PrimaryElement, Combat.ElementsOf(target));
        Projectile.damage = Combat.Damage(_data.Attack, target.defense, _lastMultiplier);

        // Against a wild creature the blow is capped short of the spare threshold. A
        // companion softens the thing you came for; it never takes it off the board, however
        // lopsided the type matchup is. That is also why catching, not killing, pays the EXP.
        if (target.ModNPC is BeastNPC)
        {
            var floor = (int)(target.lifeMax * SpareThreshold);
            Projectile.damage = Math.Clamp(Projectile.damage, 1, Math.Max(1, target.life - floor));
        }

        Projectile.tileCollide = false;
        Projectile.velocity = Projectile.DirectionTo(target.Center) * LungeSpeed;
        Projectile.spriteDirection = Projectile.velocity.X > 0 ? 1 : -1;

        SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.35f }, Projectile.Center);

        for (var i = 0; i < 6; i++)
        {
            var dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                DustID.Smoke, 0f, 0f, 150, default, 0.7f);
            dust.velocity = -Projectile.velocity * 0.15f;
            dust.noGravity = true;
        }
    }

    private void EndLunge()
    {
        Projectile.damage = 0;
        Projectile.tileCollide = true;
        Projectile.velocity *= 0.3f;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        Combat.AnnounceEffectiveness(target.Hitbox, _lastMultiplier);

        for (var i = 0; i < 8; i++)
        {
            var dust = Dust.NewDustDirect(target.position, target.width, target.height,
                DustID.SilverCoin, 0f, 0f, 120, default, 0.8f);
            dust.noGravity = true;
        }

        if (Projectile.owner != Main.myPlayer || _data == null) return;
        if (target.active && target.life > 0) return;

        AwardExperience(target);
    }

    private void AwardExperience(NPC target)
    {
        _data.GainExperience(Combat.ExperienceFrom(target), out var levels);
        if (levels <= 0) return;

        CombatText.NewText(Projectile.Hitbox, new Color(160, 230, 255),
            Language.GetTextValue("Mods.Wildlore.Combat.LevelUp", _data.DisplayName, _data.Level));
        SoundEngine.PlaySound(SoundID.Item4, Projectile.Center);

        var evolution = _data.GetQueuedEvolution();
        if (evolution != 0) Evolve(evolution);
    }

    private void Evolve(ushort into)
    {
        var before = _data.DisplayName;
        _data.EvolveInto(into);

        // The sheet is cached per species, so it has to be dropped here or the creature keeps
        // wearing its old body after changing species.
        _texture = null;
        _textureFor = 0;

        _evolveFlash = EvolveFlashTicks;
        SoundEngine.PlaySound(SoundID.Item25, Projectile.Center);

        Main.NewText(Language.GetTextValue("Mods.Wildlore.Combat.Evolved", before, _data.DisplayName),
            200, 230, 160);
    }

    private void EvolveEffect()
    {
        _evolveFlash--;

        var progress = _evolveFlash / (float)EvolveFlashTicks;
        Lighting.AddLight(Projectile.Center, 0.9f * progress, 0.85f * progress, 0.6f * progress);

        for (var i = 0; i < 3; i++)
        {
            var offset = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * (8f + 40f * progress);
            var dust = Dust.NewDustPerfect(Projectile.Center + offset, DustID.TreasureSparkle,
                -offset.SafeNormalize(Vector2.Zero) * 2f, 100, default, 1.1f);
            dust.noGravity = true;
        }
    }

    /// <summary>Called by a wild creature hitting back. Runs only on the owner's machine.</summary>
    public void TakeHit(int damage, float multiplier)
    {
        if (_data == null) return;

        _outOfCombat = 0;
        _regenTimer = 0;

        Combat.AnnounceEffectiveness(Projectile.Hitbox, multiplier);
        CombatText.NewText(Projectile.Hitbox, CombatText.DamagedFriendly, damage);

        Projectile.velocity.Y -= 2.5f;
        SoundEngine.PlaySound(SoundID.NPCHit1, Projectile.Center);

        _data.TakeDamage(damage);
    }

    private void Regenerate()
    {
        if (Projectile.owner != Main.myPlayer || _data == null) return;
        if (_outOfCombat < OutOfCombatTicks || _data.HealthFraction >= 1f)
        {
            _regenTimer = 0;
            return;
        }

        // Out of a fight a creature knits itself back together on its own. Slow enough that
        // walking away from a hard fight still costs time, fast enough that there is no
        // healing chore between them.
        if (++_regenTimer < RegenInterval) return;

        _regenTimer = 0;
        _data.Heal(Math.Max(1, _data.MaxHP / 20));
    }

    private void Faint(Player owner, WildlorePlayer modPlayer)
    {
        if (Projectile.owner == Main.myPlayer)
        {
            Main.NewText(Language.GetTextValue("Mods.Wildlore.Combat.Fainted", _data.DisplayName),
                220, 140, 140);

            modPlayer.SetActive(-1);
        }

        for (var i = 0; i < 14; i++)
        {
            var dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                DustID.Smoke, 0f, -1f, 120, default, 1.1f);
            dust.noGravity = true;
        }

        SoundEngine.PlaySound(SoundID.NPCDeath1, Projectile.Center);
        Projectile.Kill();
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        // A companion never dies on terrain; it just stops.
        Projectile.velocity = Vector2.Zero;
        return false;
    }

    // -- Drawing -------------------------------------------------------------------

    private void Animate()
    {
        Projectile.frameCounter++;

        var interval = _lungeTimer > 0 ? 4 : 8;
        if (Projectile.frameCounter < interval) return;

        Projectile.frameCounter = 0;
        Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Projectile.type];
    }

    public override bool PreDraw(ref Color lightColor)
    {
        if (_data == null) return false;

        if (_texture == null || _textureFor != _data.ID)
        {
            _texture = ModContent.Request<Texture2D>($"Wildlore/Assets/Beasts/{_data.InternalName}");
            _textureFor = _data.ID;
        }

        var frameHeight = _texture.Value.Height / Main.projFrames[Projectile.type];
        var frame = new Rectangle(0, frameHeight * Projectile.frame, _texture.Value.Width, frameHeight);
        var effects = Projectile.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

        var draw = lightColor;
        if (_data.IsRare) draw = Color.Lerp(draw, Color.White, 0.35f);
        if (_evolveFlash > 0) draw = Color.Lerp(draw, Color.White, _evolveFlash / (float)EvolveFlashTicks);

        Main.EntitySpriteDraw(_texture.Value,
            Projectile.Center - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY),
            frame, draw, Projectile.rotation,
            frame.Size() / 2f, Projectile.scale, effects);

        return false;
    }

    public override void PostDraw(Color lightColor)
    {
        // A health bar only while it matters. An untouched companion carries no clutter.
        if (_data == null || _data.HealthFraction >= 1f) return;

        var pixel = TextureAssets.MagicPixel.Value;
        var source = new Rectangle(0, 0, 1, 1);
        var origin = Projectile.Center - Main.screenPosition + new Vector2(-15f, -26f);

        Main.EntitySpriteDraw(pixel, origin, source, new Color(18, 18, 22, 190), 0f,
            Vector2.Zero, new Vector2(30f, 5f), SpriteEffects.None);

        var fill = Color.Lerp(new Color(198, 72, 66), new Color(120, 202, 118), _data.HealthFraction);

        Main.EntitySpriteDraw(pixel, origin + new Vector2(1f, 1f), source, fill, 0f,
            Vector2.Zero, new Vector2(28f * _data.HealthFraction, 3f), SpriteEffects.None);
    }
}
