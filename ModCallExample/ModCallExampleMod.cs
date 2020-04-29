/* ###### DISCLAIMER ######
 * This is an example showcasing 'Mod.Call' functionality. If you strong reference SubworldLibrary, you won't need this at all
 * ###### DISCLAIMER ######
 */
using Terraria.ModLoader;

namespace ModCallExample
{
	public class ModCallExampleMod : Mod
	{
		public override void PostSetupContent()
		{
			SubworldManager.Load();
		}

		public override void Unload()
		{
			SubworldManager.Unload();
		}
	}
}
