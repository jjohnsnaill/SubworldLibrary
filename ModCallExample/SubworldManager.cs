﻿using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace ModCallExample
{
	//This class showcases how to organize your SubworldLibrary reference
	public class SubworldManager : ModSystem
	{
		//How we identify our world
		public static string mySubworldID = string.Empty; //An empty string will not cause any problems in Enter, IsActive etc. calls

		#region Helper fields and methods
		public static Mod subworldLibrary = null;

		public static bool Loaded => subworldLibrary != null;

		public static bool? Enter(string id)
		{
			if (!Loaded) return null;
			return subworldLibrary.Call("Enter", id) as bool?;
		}

		public static bool? Exit()
		{
			if (!Loaded) return null;
			return subworldLibrary.Call("Exit") as bool?;
		}

		public static bool? IsActive(string id)
		{
			if (!Loaded) return null;
			return subworldLibrary.Call("IsActive", id) as bool?;
		}

		public static bool? AnyActive(Mod mod)
		{
			if (!Loaded) return null;
			return subworldLibrary.Call("AnyActive", mod) as bool?;
		}
		#endregion

		public override void PostSetupContent()
		{
			if (ModLoader.TryGetMod("SubworldLibrary", out Mod subworldLibrary))
			{
				object result = subworldLibrary.Call(
					"Register",
					/*Mod mod*/ ModContent.GetInstance<ModCallExampleMod>(),
					/*string name*/ "MySubworld",
					/*int width*/ 600,
					/*int height*/ 400,
					/*List<GenPass> tasks*/ MySubworldGenPassList(),
					/*the following ones are optional, I've included three here (technically two but since order matters, had to pass null for the unload argument)
					/*Action load*/ (Action)LoadWorld,
					/*Action unload*/ null,
					/*ModWorld modWorld*/ ModContent.GetInstance<ExclusiveWorld>()
					);

				if (result != null && result is string id)
				{
					mySubworldID = id;
				}
			}
		}

		public override void Unload()
		{
			subworldLibrary = null;
			mySubworldID = string.Empty;
		}

		//Passed into subworldLibrary.Call()
		public static void LoadWorld()
		{
			Main.dayTime = true;
			Main.time = 27000;
		}

		//Called in subworldLibrary.Call()
		public static List<GenPass> MySubworldGenPassList()
		{
			List<GenPass> list = new List<GenPass>
			{
				//First pass
				new PassLegacy("Adjusting",
				delegate (GenerationProgress progress, GameConfiguration configuration)
				{
					progress.Message = "Adjusting world levels"; //Sets the text above the worldgen progress bar
					Main.worldSurface = Main.maxTilesY - 42; //Hides the underground layer just out of bounds
					Main.rockLayer = Main.maxTilesY; //Hides the cavern layer way out of bounds
				},
				1f),
				//Second pass
				new PassLegacy("GeneratingBorders",
				delegate (GenerationProgress progress, GameConfiguration configuration)
				{
					progress.Message = "Generating subworld borders";

					//Create three tiles for the player to stand on when he spawns
					for (int i = -1; i < 2; i++)
					{
						WorldGen.PlaceTile(Main.spawnTileX - i,  Main.spawnTileY + 2, TileID.Dirt, true, true);
					}

					//Create a wall of lihzard bricks around the world. 41, 42 and 43 are magic numbers from the game regarding world boundaries
					for (int i = 0; i < Main.maxTilesX; i++)
					{
						for (int j = 0; j < Main.maxTilesY; j++)
						{
							progress.Value = ((float)i * Main.maxTilesY + j) / (Main.maxTilesX * Main.maxTilesY);
							if (i < 42 || i >= Main.maxTilesX - 43 || j <= 41 || j >= Main.maxTilesY - 43)
							{
								WorldGen.PlaceTile(i, j, TileID.LihzahrdBrick, true, true);
							}
						}
					}
				},
				1f)
				//Add more passes here
			};

			return list;
		}
	}
}
