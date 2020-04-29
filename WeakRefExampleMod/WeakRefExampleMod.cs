using Terraria.ModLoader;

namespace WeakRefExampleMod
{
	public class WeakRefExampleMod : Mod
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
