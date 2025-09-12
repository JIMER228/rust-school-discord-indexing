using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("FixOilRigBoats", "Nimant", "1.0.2")]
    class FixOilRigBoats : RustPlugin
    {            		
		
		#region Variables				
		
		private static List<Vector3> OilRigPositions = new List<Vector3>();
		
		#endregion
		
		#region Hooks
		
		private void Init() => LoadVariables();
		
		private void OnServerInitialized() 
		{
			OilRigPositions = TerrainMeta?.Path?.Monuments?.Where(x=> x.name.ToLower().Contains("oilrig")).Select(x=> new Vector3(x.transform.position.x, 0f, x.transform.position.z)).ToList();
			
			if (OilRigPositions?.Count > 0)
				timer.Once(10f, CheckBoatsToDecay);
			else
				Unsubscribe(nameof(OnEntityTakeDamage));
		}
		
		private void OnEntityTakeDamage(BaseBoat entity, HitInfo info)
        {
            if (OilRigPositions?.Count == 0 || entity == null || info == null) return;

			var damage = info.damageTypes.GetMajorityDamageType();			
			if (damage != Rust.DamageType.Decay) return;
			
			// отменяем дамаг от гниения в зоне нефтевышек
			if (IsPointNearOilRig(entity.transform.position))
				info.damageTypes.ScaleAll(0);						
        }
		
		#endregion
		
		#region Helpers
		
		private static bool IsPointNearOilRig(Vector3 entityPos)
		{
			foreach (var pos in OilRigPositions)
				if (Vector3.Distance(pos, entityPos) <= configData.DecayRadius)
					return true;
				
			return false;	
		}
		
		private void CheckBoatsToDecay()
		{
			var boats = BaseNetworkable.serverEntities.OfType<BaseBoat>().Where(x=> x != null && IsPointNearOilRig(x.transform.position)).ToList();
			
			if (boats.Count > 0)
				CommunityEntity.ServerInstance.StartCoroutine(DoDecayBoats(boats));
			
			timer.Once(60f, CheckBoatsToDecay);
		}
		
		private IEnumerator DoDecayBoats(List<BaseBoat> boats)
		{
			foreach (var boat in boats)
			{
				if (boat == null || boat.IsDestroyed) continue;
				boat.Hurt(boat.MaxHealth() / configData.TotalTimeDecay, Rust.DamageType.Generic, boat, false);
				yield return new WaitForSeconds(0.05f);
			}
		}
		
		#endregion
		
		#region Config        				
		
        private static ConfigData configData;
		
        private class ConfigData
        {            
			[JsonProperty(PropertyName = "Время до полного сгнивания лодок (минуты)")]
			public int TotalTimeDecay;
			[JsonProperty(PropertyName = "Радиус обнаружения лодок возле нефтевышек")]
			public float DecayRadius;
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                TotalTimeDecay = 200,
				DecayRadius = 50f
            };
            SaveConfig(configData);
			timer.Once(0.1f, ()=> SaveConfig(configData));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
    }	
	
}