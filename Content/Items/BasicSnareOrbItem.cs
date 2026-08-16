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

public class BasicSnareOrbItem : ModItem
{
    public override string Texture => "Wildlore/Assets/Items/BasicSnareOrb";

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
        Item.value = Item.buyPrice(silver: 20);
        Item.rare = ItemRarityID.White;
        Item.shoot = ModContent.ProjectileType<BasicSnareOrb>();
        Item.shootSpeed = 9f;
        Item.damage = 0;
        Item.DamageType = DamageClass.Default;
    }

    public override void AddRecipes()
    {
        CreateRecipe(5)
            .AddIngredient(ItemID.IronBar)
            .AddIngredient(ItemID.Glass, 2)
            .AddTile(TileID.Anvils)
            .Register();

        CreateRecipe(5)
            .AddIngredient(ItemID.LeadBar)
            .AddIngredient(ItemID.Glass, 2)
            .AddTile(TileID.Anvils)
            .Register();
    }
}
