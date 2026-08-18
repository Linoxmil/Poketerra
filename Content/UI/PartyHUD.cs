using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Wildlore.Core;

namespace Wildlore.Content.UI;

/// <summary>
///     The party, drawn down the left edge: who you have, how hurt they are, how close the
///     next level is.
///     <para>
///     Deliberately not a UIState. This only ever draws — nothing here is clickable — and a
///     plain interface layer is a fraction of the machinery for the same result.
///     </para>
/// </summary>
public class PartyHUD : ModSystem
{
    private const int RowHeight = 40;
    private const int PanelWidth = 148;
    private const int Left = 16;
    private const int Top = 260;

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        var index = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
        if (index == -1) return;

        layers.Insert(index, new LegacyGameInterfaceLayer("Wildlore: Party", () =>
        {
            Draw(Main.spriteBatch);
            return true;
        }, InterfaceScaleType.UI));
    }

    private static void Draw(SpriteBatch spriteBatch)
    {
        if (Main.gameMenu || Main.LocalPlayer is not { active: true } player) return;

        var wildlore = player.GetModPlayer<WildlorePlayer>();

        var occupied = 0;
        foreach (var beast in wildlore.Party)
            if (beast != null)
                occupied++;

        // Nothing caught yet means nothing to say. An empty frame would just be clutter.
        if (occupied == 0) return;

        var pixel = TextureAssets.MagicPixel.Value;
        var source = new Rectangle(0, 0, 1, 1);
        var y = Top;

        Fill(spriteBatch, pixel, source, new Rectangle(Left - 6, Top - 8, PanelWidth, occupied * RowHeight + 14),
            new Color(12, 14, 20, 160));

        for (var slot = 0; slot < WildlorePlayer.PartySize; slot++)
        {
            var beast = wildlore.Party[slot];
            if (beast == null) continue;

            DrawRow(spriteBatch, pixel, source, beast, y, slot == wildlore.ActiveSlot);
            y += RowHeight;
        }
    }

    private static void DrawRow(SpriteBatch spriteBatch, Texture2D pixel, Rectangle source,
        BeastData beast, int y, bool active)
    {
        // The creature that is actually out gets a lit strip down its left edge, so a glance
        // is enough to know who would take the next hit.
        if (active)
            Fill(spriteBatch, pixel, source, new Rectangle(Left - 6, y - 3, 3, RowHeight - 2),
                new Color(150, 220, 255));

        DrawPortrait(spriteBatch, beast, Left, y);

        var label = $"{beast.DisplayName}  Lv.{beast.Level}";
        var colour = beast.IsRare ? new Color(255, 226, 140) : Color.White;
        spriteBatch.DrawBorderString(label, new Vector2(Left + 36, y - 4), colour, 0.72f);

        // Health, then a thinner bar underneath for progress toward the next level.
        var health = new Rectangle(Left + 36, y + 14, 88, 5);
        Fill(spriteBatch, pixel, source, health, new Color(18, 18, 22, 200));
        Fill(spriteBatch, pixel, source,
            new Rectangle(health.X + 1, health.Y + 1, (int)((health.Width - 2) * beast.HealthFraction), 3),
            Color.Lerp(new Color(198, 72, 66), new Color(120, 202, 118), beast.HealthFraction));

        var experience = new Rectangle(Left + 36, y + 22, 88, 3);
        Fill(spriteBatch, pixel, source, experience, new Color(18, 18, 22, 200));
        Fill(spriteBatch, pixel, source,
            new Rectangle(experience.X + 1, experience.Y + 1, (int)((experience.Width - 2) * LevelProgress(beast)), 1),
            new Color(120, 190, 255));
    }

    private static void DrawPortrait(SpriteBatch spriteBatch, BeastData beast, int x, int y)
    {
        var texture = ModContent.Request<Texture2D>($"Wildlore/Assets/Beasts/{beast.InternalName}").Value;
        var frameHeight = texture.Height / Wildlore.SpriteFrames;
        var frame = new Rectangle(0, 0, texture.Width, frameHeight);

        var tint = beast.IsFainted ? new Color(90, 90, 100) : Color.White;

        spriteBatch.Draw(texture, new Vector2(x, y - 4), frame, tint, 0f, Vector2.Zero, 0.85f,
            SpriteEffects.None, 0f);
    }

    /// <summary>How far this creature is through its current level, 0–1.</summary>
    private static float LevelProgress(BeastData beast)
    {
        if (beast.Level >= Wildlore.MaxLevel) return 1f;

        var current = ExperienceTable.TotalExpForLevel(beast.Level, beast.Schema.GrowthRate);
        var next = ExperienceTable.TotalExpForLevel((byte)(beast.Level + 1), beast.Schema.GrowthRate);

        if (next <= current) return 1f;

        return Math.Clamp((beast.TotalEXP - current) / (float)(next - current), 0f, 1f);
    }

    private static void Fill(SpriteBatch spriteBatch, Texture2D pixel, Rectangle source,
        Rectangle area, Color colour)
    {
        spriteBatch.Draw(pixel, area, source, colour);
    }
}
