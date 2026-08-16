using ReLogic.Content;

namespace Wildlore.Content.Projectiles;

/// <summary>
///     The summoned companion. One projectile per player, reading its appearance from the
///     party slot stored in <c>Projectile.ai[0]</c> so a single class covers every species.
/// </summary>
public class BeastPet : ModProjectile
{
    private Asset<Texture2D> _texture;
    private BeastData _data;

    public override string Texture => "Wildlore/Assets/Beasts/Placeholder";

    private int Slot => (int)Projectile.ai[0];

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
    }

    public override void AI()
    {
        var owner = Main.player[Projectile.owner];
        var modPlayer = owner.GetModPlayer<WildlorePlayer>();

        // Despawn if the owner died, recalled, or the slot emptied.
        if (owner.dead || Slot < 0 || Slot >= WildlorePlayer.PartySize || modPlayer.Party[Slot] == null)
        {
            Projectile.Kill();
            return;
        }

        Projectile.timeLeft = 2; // Keep alive as long as the conditions above hold.
        _data ??= modPlayer.Party[Slot];

        Follow(owner);
        Animate();
    }

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
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget.SafeNormalize(Vector2.Zero) * speed, 0.12f);
        }
        else
        {
            Projectile.velocity *= 0.9f;
            Projectile.velocity.Y += 0.15f; // Settle onto the ground rather than hovering.
        }

        if (Math.Abs(Projectile.velocity.X) > 0.2f)
            Projectile.spriteDirection = Projectile.velocity.X > 0 ? 1 : -1;
    }

    private void Animate()
    {
        Projectile.frameCounter++;
        if (Projectile.frameCounter < 8) return;
        Projectile.frameCounter = 0;
        Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Projectile.type];
    }

    public override bool PreDraw(ref Color lightColor)
    {
        if (_data == null) return false;

        _texture ??= ModContent.Request<Texture2D>($"Wildlore/Assets/Beasts/{_data.InternalName}");

        var frameHeight = _texture.Value.Height / Main.projFrames[Projectile.type];
        var frame = new Rectangle(0, frameHeight * Projectile.frame, _texture.Value.Width, frameHeight);
        var effects = Projectile.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

        Main.EntitySpriteDraw(_texture.Value,
            Projectile.Center - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY),
            frame, lightColor, Projectile.rotation,
            frame.Size() / 2f, Projectile.scale, effects);

        return false;
    }
}
