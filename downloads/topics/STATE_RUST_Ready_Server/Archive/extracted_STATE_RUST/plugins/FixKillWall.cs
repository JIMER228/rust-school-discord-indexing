using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Rust;

namespace Oxide.Plugins
{
    [Info("FixKillWall", "Nimant", "1.0.0")]
    class FixKillWall : RustPlugin
    {															
		
		private const int ShipSize = 200;
		
		private void OnServerInitialized() 
		{			
			var instance = SingletonComponent<ValidBounds>.Instance;
			if (instance == null)
				PrintWarning("Стена смерти не найдена, изменение её размеров невозможно.");
			else
			{
				var worldSize = World.Size;
				var wallSize = instance.worldBounds.extents.x > instance.worldBounds.extents.z ? instance.worldBounds.extents.x : instance.worldBounds.extents.z;
				
				if (worldSize + ShipSize > wallSize)
				{
					var oldSize = instance.worldBounds.extents;
					instance.worldBounds.extents = new Vector3(worldSize + ShipSize, oldSize.y, worldSize + ShipSize);					
					
					PrintWarning($"Размеры стены смерти увеличены с {oldSize} до {instance.worldBounds.extents}.");
				}
				else
					PrintWarning($"Размеры стены смерти не менялись, - нет необходимости.");
			}
		}												
		
	}
	
}	