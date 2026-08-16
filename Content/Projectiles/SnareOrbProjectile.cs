using Terraria.Audio;
using Wildlore.Content.NPCs;

namespace Wildlore.Content.Projectiles;

/// <summary>
///     Base class for all catching orbs. Subclass it and change <see cref="CatchModifier" />
///     to create stronger variants — that is the whole extension point.
/// </summary>
public abstract class SnareOrbProjectile : ModProjectile
{
    /// <summary>
    ///     Ticks the snare takes to close once it has latched onto a creature.
    ///     <see cref="BeastNPC" />'s shrink is paced to finish at the same moment, so the two
    ///     halves of the effect have to be retuned together.
    /// </summary>
    private const int CinchTicks = 42;

    /// <summary>Multiplier on catch chance. 1.0 = baseline orb.</summary>
    protected virtual float CatchModifier => 1f;

    /// <summary>Item that gets returned to the player when the catch fails.</summary>
    protected abstract int OrbItemType { get; }

    private BeastNPC _target;

    /// <summary>
    ///     The snared creature's identity, copied at the moment of contact.
    ///     <see cref="BeastNPC" /> starts shrinking straight away and is gone from the world
    ///     well before the snare finishes closing, so its data cannot be read back later.
    /// </summary>
    private BeastData _snared;

    private int _cinchTimer;
    private bool _willEscape;

    public override void SetDefaults()
    {
        Projectile.width = 14;
        Projectile.height = 14;
        Projectile.friendly = true;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 600;
        Projectile.aiStyle = 0;
    }

    public override void AI()
    {
        if (_target == null)
        {
            // Free flight: arc toward wherever it was thrown.
            Projectile.velocity.Y += 0.2f;
            Projectile.rotation += Projectile.velocity.X * 0.05f;
            return;
        }

        // Latched on: hold position and run the cinch to completion.
        Projectile.velocity = Vector2.Zero;
        Projectile.rotation = 0f;
        Projectile.tileCollide = false;

        _cinchTimer++;
        CinchEffect();

        if (_cinchTimer < CinchTicks) return;
        Resolve();
    }

    /// <summary>
    ///     A ring of light drawing inward onto the orb, tightening as the snare closes, with
    ///     a hum that climbs in pitch alongside it. One continuous action the player can read
    ///     the progress of, rather than a sequence of discrete beats.
    /// </summary>
    private void CinchEffect()
    {
        var progress = _cinchTimer / (float)CinchTicks;
        var radius = MathHelper.Lerp(44f, 4f, progress);

        for (var i = 0; i < 2; i++)
        {
            var offset = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * radius;
            var dust = Dust.NewDustPerfect(Projectile.Center + offset, DustID.TreasureSparkle,
                -offset.SafeNormalize(Vector2.Zero) * 1.6f, 120, default, 0.9f);
            dust.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.35f, 0.4f, 0.22f);

        if (_cinchTimer % 18 != 0) return;
        SoundEngine.PlaySound(SoundID.Item25 with { Pitch = -0.5f + progress }, Projectile.Center);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (_target != null || target.ModNPC is not BeastNPC beast || beast.BeingCaptured) return;

        _target = beast;
        _snared = beast.Data?.ShallowCopy();

        beast.Capture();
        Projectile.Center = target.Center;
        Projectile.netUpdate = true;

        // The outcome is decided the moment the snare latches on. The cinch is feedback for
        // a result that already exists, not a roll running in real time.
        _willEscape = !RollCatch(beast);
    }

    /// <summary>
    ///     Catch chance derived from the species' catch rate and the orb's modifier.
    ///     Keep this in one place so balancing never means editing multiple orb classes.
    /// </summary>
    private bool RollCatch(BeastNPC beast)
    {
        if (beast.Data == null) return false;

        var baseRate = beast.Schema.CatchRate / 255f;
        var levelPenalty = 1f - beast.Data.Level / (float)(Wildlore.MaxLevel * 2);

        // A creature at full strength throws the snare off; a worn-down one barely resists.
        // This is the whole reason to send a companion in first.
        var wear = 1f - 0.55f * beast.Data.HealthFraction;

        var chance = Math.Clamp(baseRate * CatchModifier * levelPenalty * wear, 0.02f, 0.95f);
        return Main.rand.NextFloat() < chance;
    }

    private void Resolve()
    {
        if (_willEscape)
        {
            ReleaseSnared();
            Item.NewItem(Projectile.GetSource_FromThis(), Projectile.Hitbox, OrbItemType);
            SoundEngine.PlaySound(SoundID.Item16, Projectile.position);
            Projectile.Kill();
            return;
        }

        if (Projectile.owner == Main.myPlayer && _snared != null)
        {
            var modPlayer = Main.player[Projectile.owner].GetModPlayer<WildlorePlayer>();

            // Read the companion before the catch lands, so a first catch into an empty
            // party does not credit the creature that was just caught.
            var trainer = modPlayer.Active;

            if (modPlayer.AddToParty(_snared))
            {
                Main.NewText($"{_snared.DisplayName} joined your party!", 120, 220, 140);
                if (trainer != null) AwardCatchExperience(trainer, _snared);
            }
            else
            {
                Main.NewText($"Your party is full — {_snared.DisplayName} was released.", 220, 180, 120);
            }
        }

        SoundEngine.PlaySound(SoundID.Item4, Projectile.position);
        Projectile.Kill();
    }

    /// <summary>
    ///     A successful catch is what pays the companion, since it is not allowed to land a
    ///     killing blow on a wild creature. Levelling comes from working the field, not from
    ///     clearing it.
    /// </summary>
    private static void AwardCatchExperience(BeastData trainer, BeastData caught)
    {
        trainer.GainExperience(Combat.ExperienceFromBeast(caught), out var levels);
        if (levels <= 0) return;

        Main.NewText(
            Language.GetTextValue("Mods.Wildlore.Combat.LevelUp", trainer.DisplayName, trainer.Level),
            160, 230, 255);

        var evolution = trainer.GetQueuedEvolution();
        if (evolution == 0) return;

        // BeastPet keys its cached sheet off the species ID, so the companion picks up its
        // new body on the next frame without being told.
        var before = trainer.DisplayName;
        trainer.EvolveInto(evolution);

        Main.NewText(
            Language.GetTextValue("Mods.Wildlore.Combat.Evolved", before, trainer.DisplayName),
            200, 230, 160);
    }

    /// <summary>
    ///     Puts an escaped creature back into the world as the same individual it was.
    ///     Spawning a plain new NPC would re-roll its level and rare flag, so a rare that got
    ///     away would come back as something else entirely.
    /// </summary>
    private void ReleaseSnared()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || _snared == null) return;

        var type = BeastLoader.GetNPCType(_snared.ID);
        if (type == 0) return;

        var index = NPC.NewNPC(Projectile.GetSource_FromThis(), (int)Projectile.Center.X,
            (int)Projectile.Center.Y, type);

        if (index < 0 || index >= Main.maxNPCs) return;

        if (Main.npc[index].ModNPC is BeastNPC restored) restored.Data = _snared;
        Main.npc[index].netUpdate = true;
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        if (_target != null) return false;

        // Bounce once, then drop back as an item so orbs are never lost to a bad throw.
        Projectile.velocity.X = oldVelocity.X * -0.4f;
        Projectile.velocity.Y = oldVelocity.Y * -0.4f;

        if (Math.Abs(Projectile.velocity.Y) >= 1f) return false;
        Item.NewItem(Projectile.GetSource_FromThis(), Projectile.Hitbox, OrbItemType);
        Projectile.Kill();
        return false;
    }
}

/// <summary>The starter orb. Craftable, weakest catch rate.</summary>
public class BasicSnareOrb : SnareOrbProjectile
{
    protected override float CatchModifier => 1f;
    protected override int OrbItemType => ModContent.ItemType<Items.BasicSnareOrbItem>();
    public override string Texture => "Wildlore/Assets/Items/BasicSnareOrb";
}
