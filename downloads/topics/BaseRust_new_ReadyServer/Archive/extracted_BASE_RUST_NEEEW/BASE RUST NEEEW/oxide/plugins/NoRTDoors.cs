using System.Linq;

namespace Oxide.Plugins
{
	[Info("No RT Doors", "-", "0.0.1")]
	class NoRTDoors : RustPlugin
	{
		private void OnServerInitialized() 
		{
			foreach (var entity in BaseNetworkable.serverEntities.Where(p => p is Door))
			{ 
				if(entity.PrefabName.Contains("door.hinged.industrial_a") || entity.PrefabName.Contains("door.hinged.garage_a") || entity.PrefabName.Contains("door.hinged.bunker.door") || entity.PrefabName.Contains("door.hinged.security") || entity.PrefabName.Contains("door.hinged.ventity.prefab")) 
				{
					entity.Kill(); 
				}
			}
		}
	}
}
