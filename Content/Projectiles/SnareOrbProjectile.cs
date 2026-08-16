using Terraria.Audio;
using Wildlore.Content.NPCs;

namespace Wildlore.Content.Projectiles;

/// <summary>
///     Base class for all catching orbs. Subclass it and change <see cref="CatchModifier" />
///     to create stronger variants — that is the whole extension point.
/// </summary>
public abstract class SnareOrbProjectile : ModProjectile
{
    /// <summary>Multiplier on catch chance. 1.0 = baseline orb.</summary>
    protected virtual float CatchModifier => 1f;

    /// <summary>Item that gets returned to the player when the catch fails.</summary>
    protected abstract int OrbItemType { get; }

    private BeastNPC _target;
    private int _shakeTimer;
    private int _shakesRemaining;
    private bool _resolved;

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

        // Attached: run the shake sequence, then resolve.
        Projectile.velocity = Vector2.Zero;
        Projectile.rotation = 0f;
        Projectile.tileCollide = false;

        _shakeTimer++;
        if (_shakeTimer % 40 != 0) return;

        _shakesRemaining--;
        SoundEngine.PlaySound(SoundID.Item1, Projectile.position);

        if (_shakesRemaining > 0) return;
        Resolve();
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (_target != null || target.ModNPC is not BeastNPC beast || beast.BeingCaptured) return;

        _target = beast;
        beast.Capture();
        Projectile.Center = target.Center;

        // Decide the outcome up front, then play the shakes as feedback for it.
        var success = RollCatch(beast);
        _shakesRemaining = success ? 3 : Main.rand.Next(1, 4);
        _resolved = !success;
    }

    /// <summary>
    ///     Catch chance derived from the species' catch rate and the orb's modifier.
    ///     Keep this in one place so balancing never means editing multiple orb classes.
    /// </summary>
    private bool RollCatch(BeastNPC beast)
    {
        var baseRate = beast.Schema.CatchRate / 255f;
        var levelPenalty = 1f - beast.Data.Level / (float)(Wildlore.MaxLevel * 2);
        var chance = Math.Clamp(baseRate * CatchModifier * levelPenalty, 0.02f, 0.95f);
        return Main.rand.NextFloat() < chance;
    }

    private void Resolve()
    {
        var owner = Main.player[Projectile.owner];

        if (_resolved)
        {
            // Escaped: put the creature back and return the orb.
            var type = BeastLoader.GetNPCType(_target.ID);
            if (Main.netMode != NetmodeID.MultiplayerClient && type != 0)
                NPC.NewNPC(Projectile.GetSource_FromThis(), (int)Projectile.Center.X,
                    (int)Projectile.Center.Y, type);

            Item.NewItem(Projectile.GetSource_FromThis(), Projectile.Hitbox, OrbItemType);
            Projectile.Kill();
            return;
        }

        // Caught.
        if (Projectile.owner == Main.myPlayer)
        {
            var modPlayer = owner.GetModPlayer<WildlorePlayer>();
            var data = _target.Data.ShallowCopy();

            if (modPlayer.AddToParty(data))
                Main.NewText($"{data.DisplayName} joined your party!", 120, 220, 140);
            else
                Main.NewText($"Your party is full — {data.DisplayName} was released.", 220, 180, 120);
        }

        SoundEngine.PlaySound(SoundID.Item4, Projectile.position);
        Projectile.Kill();
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
