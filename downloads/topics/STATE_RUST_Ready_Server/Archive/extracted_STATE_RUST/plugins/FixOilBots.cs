using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("FixOilBots", "Nimant", "1.0.6")]
    class FixOilBots : RustPlugin
    {            		
		
		private static float LastCall;
		
		//private void OnServerInitialized() => SeekAndDestroy("При загрузке");
		
		private void OnEntitySpawned(BaseEntity entity)
		{
			if (entity is HumanNPC && Time.realtimeSinceStartup - LastCall > 1f)
			{
				timer.Once(1f, ()=> SeekAndDestroy("При спавне"));				
				LastCall = Time.realtimeSinceStartup;
			}
		}        
		
		private void OnServerSave() => SeekAndDestroy("При сохранении");
		
		private void SeekAndDestroy(string reason)
		{
			if (!IsNavEnabled()) return; // не работаем при выключенном навмеше
			
			var npcs = BaseNetworkable.serverEntities.OfType<HumanNPC>().ToList();						
			int count = 0;
			
			foreach(var bot in npcs.Where(x=> x != null && !x.IsDestroyed && !x.isMounted && x.NavAgent?.isOnNavMesh == false))
			{												
				bot.Kill();
				count++;
			}
			
			if (count > 0)							
				PrintWarning($"{reason} найдено и уничтожено багованных ботов - {count} шт.");								
		}
		
		private bool IsNavEnabled()
		{
			try { return Rust.Ai.AiManager.nav_disable == true ? false : true; } catch {}			
			return false;
		}
		
    }	
	
}