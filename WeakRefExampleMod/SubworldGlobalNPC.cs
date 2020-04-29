using System.Collections.Generic;
using Terraria.ModLoader;

namespace WeakRefExampleMod
{
	public class SubworldGlobalNPC : GlobalNPC
	{
		public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
		{
			//If any subworld from our mod is loaded, disable spawns
			if (SubworldManager.AnyActive(mod) ?? false)
			{
				pool.Clear();
			}
		}
	}
}
