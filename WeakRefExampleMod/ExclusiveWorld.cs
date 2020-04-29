using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace WeakRefExampleMod
{
	public class ExclusiveWorld : ModWorld
	{
		public static bool enteredWorld = false;

		public override void Initialize()
		{
			enteredWorld = false;
		}

		public override void PreUpdate()
		{
			//This hook also runs in the subworld if we choose to specify MyExclusiveWorld for the "Register"
		}

		public override void PostUpdate()
		{
			//This hook also runs in the subworld if we choose to specify MyExclusiveWorld for the "Register"

			if (!(SubworldManager.IsActive(SubworldManager.mySubworldID) ?? false)) return; //No point executing the code below if we aren't in the subworld we want

			if (!enteredWorld)
			{
				enteredWorld = true;
				string message = $"Hey, We successfully entered {SubworldManager.mySubworldID} and only the {Name} ModWorld will update here!";
				if (Main.netMode == NetmodeID.Server)
				{
					NetMessage.BroadcastChatMessage(NetworkText.FromLiteral(message), Color.Orange);
				}
				else
				{
					Main.NewText(message, Color.Orange);
				}
			}
		}
	}
}
