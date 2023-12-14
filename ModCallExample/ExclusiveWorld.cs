using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ModCallExample
{
	public class ExclusiveWorld : ModSystem
	{
		public static bool enteredWorld = false;

		public override void OnWorldLoad()/* tModPorter Suggestion: Also override OnWorldUnload, and mirror your worldgen-sensitive data initialization in PreWorldGen */
		{
			enteredWorld = false;
		}

		public override void PreUpdateWorld()
		{
			//This hook also runs in the subworld if we choose to specify ExclusiveWorld for the "Register"
		}

		public override void PostUpdateWorld()
		{
			//This hook also runs in the subworld if we choose to specify ExclusiveWorld for the "Register"

			if (!(SubworldManager.IsActive(SubworldManager.mySubworldID) ?? false)) return; //No point executing the code below if we aren't in the subworld we want

			if (!enteredWorld)
			{
				enteredWorld = true;
				string message = $"Hey, We successfully entered '{SubworldManager.mySubworldID}' and only the '{Name}' will update here!";
				if (Main.netMode == NetmodeID.Server)
				{
					ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(message), Color.Orange);
				}
				else
				{
					Main.NewText(message, Color.Orange);
				}
			}
		}
	}
}
