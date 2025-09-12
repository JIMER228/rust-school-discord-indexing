using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Rust;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("FixDieNRE", "Nimant", "1.0.3")]
    class FixDieNRE : RustPlugin
    {															
		
		private object OnPlayerDeath(BasePlayer player, HitInfo info)
        {            					
			if (player != null && info == null)
			{
				info = new HitInfo();				
				Die(player as BaseCombatEntity, info);				
				
				return false;
			}
			
			return null;
        }
		
		private object OnEntityGroundMissing(LootContainer cont)
		{
			if (cont != null)
			{	            
				var info = new HitInfo();				
				Die(cont as BaseCombatEntity, info);	
				
				return false;
			}
			
			return null;
		}

		private void Die(BaseCombatEntity entity, HitInfo info)
		{
			if (entity == null || entity.IsDead()) return;						
			
			entity.health = 0f;
			entity.lifestate = BaseCombatEntity.LifeState.Dead;
			
			Interface.CallHook("OnEntityDeath", entity, info);
			
			using (TimeWarning timeWarning = TimeWarning.New("OnKilled", 0))			
				entity.OnKilled(info);			
		}		
		
	}
	
}	