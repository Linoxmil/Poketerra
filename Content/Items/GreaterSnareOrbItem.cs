using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Wildlore.Content.Projectiles;
using Wildlore.Core;

namespace Wildlore.Content.Items;

/// <summary>
///     Second-tier orb. Catches roughly twice as reliably as the starter, and costs a gold or
///     platinum bar to say so.
/// </summary>
public class GreaterSnareOrbItem : ModItem
{
    public override string Texture => "Wildlore/Assets/Items/GreaterSnareOrb";

    public override void SetDefaults()
    {
        Item.width = 14;
        Item.height = 14;
        Item.maxStack = 999;
        Item.consumable = true;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.useTime = 20;
        Item.useAnimation = 20;
        Item.noUseGraphic = true;
        Item.noMelee = true;
        Item.autoReuse = false;
        Item.value = Item.buyPrice(gold: 1);
        Item.rare = ItemRarityID.Green;
        Item.shoot = ModContent.ProjectileType<GreaterSnareOrb>();
        Item.shootSpeed = 9f;
        Item.damage = 0;
        Item.DamageType = DamageClass.Default;
    }

    public override void AddRecipes()
    {
        CreateRecipe(5)
            .AddIngredient(ItemID.GoldBar)
            .AddIngredient(ItemID.Glass, 2)
            .AddIngredient<BasicSnareOrbItem>(5)
            .AddTile(TileID.Anvils)
            .Register();

        CreateRecipe(5)
            .AddIngredient(ItemID.PlatinumBar)
            .AddIngredient(ItemID.Glass, 2)
            .AddIngredient<BasicSnareOrbItem>(5)
            .AddTile(TileID.Anvils)
            .Register();
    }
}
