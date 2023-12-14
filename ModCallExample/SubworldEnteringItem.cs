using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModCallExample
{
	public class SubworldEnteringItem : ModItem
	{
		public override string Texture => "Terraria/Images/Item_" + ItemID.Extractinator;

		public override void SetDefaults()
		{
			Item.maxStack = 1;
			Item.width = 34;
			Item.height = 38;
			Item.rare = 12;
			Item.useStyle = 4;
			Item.useTime = 30;
			Item.useAnimation = 30;
			Item.UseSound = SoundID.Item1;
		}

		public override bool? UseItem(Player player)
		{
			//Enter should be called on exactly one side, which here is the player
			if (Main.myPlayer == player.whoAmI)
			{
				bool result = SubworldManager.Enter(SubworldManager.mySubworldID) ?? false;

				if (!result)
				{
					//If some issue occured, inform why (can't know exactly obviously, might need to check logs)
					string message;
					if (!SubworldManager.Loaded)
					{
						message = "SubworldLibrary Mod is required to be enabled for this item to work!";
					}
					else
					{
						message = $"Unable to enter {SubworldManager.mySubworldID}!";
					}

					Main.NewText(message, Color.Orange);
				}

				return result;
			}
			return true;
		}

		public override void AddRecipes()
		{
			Recipe recipe = CreateRecipe();
			recipe.AddIngredient(ItemID.DirtBlock, 1);
			recipe.Register();
		}
	}
}
