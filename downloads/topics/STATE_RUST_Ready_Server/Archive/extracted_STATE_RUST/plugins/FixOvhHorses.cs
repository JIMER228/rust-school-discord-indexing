using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using UnityEngine;
using Facepunch;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("FixOvhHorses", "Nimant", "1.0.4")]
    class FixOvhHorses : RustPlugin
    {
		
		#region Variables
		
		private static FixOvhHorses ins = null;
		
		#endregion
		
		#region Hooks
		
		private void Init() 
		{
			ins = this;
			LoadVariables();
		}		
		
		private void OnServerInitialized() 
		{			
			foreach(var horse in BaseNetworkable.serverEntities.OfType<RidableHorse>().ToList())			
				horse.gameObject.AddComponent<HorseController>();
		}
		
		private void OnEntitySpawned(BaseNetworkable entity) 
		{								
			var horse = entity as RidableHorse;
			if (horse == null) return;															
			horse.gameObject.AddComponent<HorseController>();									
		}
		
		private void Unload()
		{
			var objects = UnityEngine.Object.FindObjectsOfType<HorseController>();
            if (objects != null)
                foreach (var gameObj in objects)
                    UnityEngine.Object.Destroy(gameObj);
		}
		
		#endregion
		
		#region Mono
		
		private class HorseController : MonoBehaviour
        {
			private FieldInfo nextEatTime = typeof(BaseRidableAnimal).GetField("nextEatTime", BindingFlags.NonPublic | BindingFlags.Instance);
			private FieldInfo lastEatTime = typeof(BaseRidableAnimal).GetField("lastEatTime", BindingFlags.NonPublic | BindingFlags.Instance);
            private RidableHorse horse;
			private float nextEatTimeNew;
			private float lastCheck;

            private void Awake()
            {
                horse = GetComponent<RidableHorse>();
				try { nextEatTime.SetValue(horse, float.MaxValue); }
				catch 
				{
					ins.PrintWarning("Ошибка '1' создания контроллера лошади!");
					GameObject.Destroy(this);
				}
            }
			
			private void OnDestroy()
            {
				if (horse != null && !horse.IsDestroyed)
				{
					try	{ nextEatTime.SetValue(horse, float.MinValue); } catch {}
				}
				
                GameObject.Destroy(this);
            } 
			
			private void FixedUpdate()
            {
				if (Time.realtimeSinceStartup - lastCheck < 0.1f) 
					return;
				
				lastCheck = Time.realtimeSinceStartup;
				
				if (horse == null)
				{
					Destroy(this);
					return;
				}
				
				if (horse.currentRunState == RidableHorse.RunState.stopped || horse.currentRunState == RidableHorse.RunState.walk)
					EatNearbyFood();
			}
			
			private void EatNearbyFood()
			{
				if (Time.time < nextEatTimeNew) return;
				
				var single = horse.StaminaCoreFraction();				
				if (single >= 1f) return;
				
				if (horse.IsHitched())
				{
					Item foodItem = horse.currentHitch.GetFoodItem();
					if (foodItem != null && foodItem.amount > 0)
					{
						ItemModConsumable component = foodItem.info.GetComponent<ItemModConsumable>();
						if (component)
						{
							horse.ReplenishFromFood(component);
							foodItem.UseItem(1);
							nextEatTimeNew = Time.time + UnityEngine.Random.Range(2f, 3f) + Mathf.InverseLerp(0.5f, 1f, single) * 4f;
							return;
						}
					}
				}
				
				List<BaseEntity> list = Pool.GetList<BaseEntity>();
				Vis.Entities<BaseEntity>(horse.transform.position + (horse.transform.forward * 1.5f), 2f, list);
				//list.Sort((BaseEntity a, BaseEntity b) => b is DroppedItem.CompareTo(a is DroppedItem));
				
				foreach (BaseEntity baseEntity in list)
				{
					if (IsExcludeFood(baseEntity) || baseEntity.ShortPrefabName.Contains("saddletest") || baseEntity.ShortPrefabName.Contains("testridablehorse")) continue;										
					
					DroppedItem droppedItem = baseEntity as DroppedItem;
					if (droppedItem && droppedItem.item != null && droppedItem.item.info.category == ItemCategory.Food)
					{
						ItemModConsumable component = droppedItem.item.info.GetComponent<ItemModConsumable>();
						if (component)
						{
							horse.ClientRPC(null, "Eat");
							
							try
							{
								lastEatTime.SetValue(horse, Time.time);							
							}
							catch 
							{
								Pool.FreeList<BaseEntity>(ref list);
								ins.PrintWarning("Ошибка '2' создания контроллера лошади!");
								GameObject.Destroy(this);
								return;
							}
							
							float ifType = component.GetIfType(MetabolismAttribute.Type.Calories);
							float ifType1 = component.GetIfType(MetabolismAttribute.Type.Hydration);
							float single1 = component.GetIfType(MetabolismAttribute.Type.Health) + component.GetIfType(MetabolismAttribute.Type.HealthOverTime);
							horse.ReplenishStaminaCore(ifType, ifType1);
							horse.Heal(single1 * 2f);
							droppedItem.item.UseItem(1);
							if (droppedItem.item.amount > 0) break;
							
							droppedItem.Kill(BaseNetworkable.DestroyMode.None);
							Pool.FreeList<BaseEntity>(ref list);
							nextEatTimeNew = Time.time + UnityEngine.Random.Range(2f, 3f) + Mathf.InverseLerp(0.5f, 1f, single) * 4f;
							return;
						}
					}
					
					CollectibleEntity collectibleEntity = baseEntity as CollectibleEntity;
					if (!collectibleEntity || !collectibleEntity.IsFood())
					{						
						var plantEntity = baseEntity as GrowableEntity;
						if (!plantEntity || !plantEntity.CanPick())	continue;																
						plantEntity.PickFruit(null);
						Pool.FreeList<BaseEntity>(ref list);
						return;
					}
					else
					{
						collectibleEntity.DoPickup(null);
						Pool.FreeList<BaseEntity>(ref list);
						return;
					}
				}
				
				Pool.FreeList<BaseEntity>(ref list);
				nextEatTimeNew = Time.time + UnityEngine.Random.Range(2f, 3f) + Mathf.InverseLerp(0.5f, 1f, single) * 4f;
			}
			
			private bool IsExcludeFood(BaseEntity baseEntity)
			{
				if (baseEntity == null) return true;
				
				foreach (var excl in configData.ExcludeEntities)				
					if (baseEntity.ShortPrefabName.Contains(excl))
						return true;
					
				return false;					
			}
			
        }
		
		#endregion
		
		#region Config
		
        private static ConfigData configData;
		
        private class ConfigData
        {            
			[JsonProperty(PropertyName = "Список еды, которую не должна срывать лошадь")]
			public List<string> ExcludeEntities;
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                ExcludeEntities = new List<string>()
				{
					"mushroom",
					"hemp"
				}
            };
            SaveConfig(configData);
			timer.Once(0.1f, ()=> SaveConfig(configData));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
		
    }	
	
}