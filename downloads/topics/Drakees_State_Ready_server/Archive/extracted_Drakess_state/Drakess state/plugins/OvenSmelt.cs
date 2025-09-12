using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;
using Newtonsoft.Json;
using ItemR = Item;
using Random = System.Random;

namespace Oxide.Plugins
{
    [Info("OvenSmelt", "Nimant", "1.1.1")]    
    class OvenSmelt : RustPlugin
    {										
		
		#region Hooks
		
		private void OnServerInitialized() 
		{ 		
			var ovens = new HashSet<BaseOven>(BaseNetworkable.serverEntities.OfType<BaseOven>().Where(r=> configData.SmeltObjects.ContainsKey(r.ShortPrefabName) && r.IsOn()));
			
			foreach(var oven in ovens)			
				StartCooking(oven);					
		}
		
		private void Init() => LoadVariables();			
		
		private void Unload()
        {
			var ovens = new HashSet<BaseOven>(BaseNetworkable.serverEntities.OfType<BaseOven>().Where(r=> configData.SmeltObjects.ContainsKey(r.ShortPrefabName)));
			
			foreach(var oven in ovens)			
				RestoreDefaultCooking(oven);							
		}	
		
		private object OnOvenToggle(BaseOven oven, BasePlayer player)
		{
			if (oven == null) return null;			
			if (!configData.SmeltObjects.ContainsKey(oven.ShortPrefabName)) return null;
			
			bool state = oven.IsOn();												
			
			if (!state)
				StartCooking(oven);
			else
				StopCooking(oven);				
			
			return false;
		}								
		
		#endregion
		
		#region Common
		
		private void StartCooking(BaseOven oven)
		{					
			OvenTimer otimer = oven.GetComponent<OvenTimer>();
			if (otimer != null)
			{				
				otimer.StopCooking();
				GameObject.Destroy(otimer);
			}				
			OvenTimer otimer2 = oven.gameObject.AddComponent<OvenTimer>();
		}
		
		private void StopCooking(BaseOven oven)
		{			
			if (oven.GetComponent<OvenTimer>()) 			
			{
				OvenTimer otimer = oven.GetComponent<OvenTimer>();
				otimer.StopCooking();
				GameObject.Destroy(otimer);
			}	
		}								
		
		private void RestoreDefaultCooking(BaseOven oven)
		{			
			if (oven.GetComponent<OvenTimer>()) 			
			{
				OvenTimer otimer = oven.GetComponent<OvenTimer>();
				otimer.RestoreDefaultCooking();
				GameObject.Destroy(otimer);
			}	
		}
		
		#endregion
		
		#region Oven Timer Class
		
		private class OvenTimer : MonoBehaviour
        {		
			private BaseOven oven;			
			private Dictionary<Item, float> itemCycle;
			private Random Rnd;
		
			private void Awake()
            {
				itemCycle = new Dictionary<Item, float>();
                oven = GetComponent<BaseOven>();         								
				Rnd = new Random();
				if (FindBurnable() == null) return;												
				oven.inventory.temperature = getCookingTemperature();			
				oven.UpdateAttachmentTemperature();			
				oven.CancelInvoke("Cook");			
				this.InvokeRepeating("NewCook", 0.5f, 0.5f);									
				oven.SetFlag(BaseEntity.Flags.On, true, false);																
            }
			
			private void ProcessSmeltCycle()
			{							
				var inv = oven.inventory;				
				var template = configData.SmeltObjects[oven.ShortPrefabName];
				
				if (!configData.SmeltTemplates.ContainsKey(template)) return;
				
				var sobjList = configData.SmeltTemplates[template];								
				
				for(int ii=0;ii<inv.itemList.Count;ii++)					
				{
					var item = inv.itemList[ii];
					if (item == null || !item.IsValid()) continue;					
					
					var sobj = sobjList.FirstOrDefault(r=> r.sourceName == item.info.shortname && !r.isFuel && r.enabled);					
					if (sobj == null) continue;					
										
					if (!itemCycle.ContainsKey(item))
						itemCycle.Add(item, 0);					
					
					/*if (!item.IsCooking())
						itemCycle[item] = 0;*/
					
					if (!item.IsCooking() && template == "cook")
						item.SetFlag(ItemR.Flag.Cooking, true);
															
					var timeReady = sobj.timeSmelt * 2f * 1f/sobj.speedRate;				
					itemCycle[item] += sobj.smeltRate;
					
					if (itemCycle[item] >= timeReady)
					{						
						int rem, amount = Math.DivRem((int)Math.Round(itemCycle[item] * 100), (int)Math.Round(timeReady * 100f), out rem);
						itemCycle[item] = rem / 100f;	
												
						RemoveRawItems(item, amount * sobj.sourceAmount);
						
						var targetAmount = GetRandomAmount(sobj.targetAmount);
						AddSmeltedItems(sobj.targetName, amount * targetAmount);
					}																		
				}
				
				foreach(var item in itemCycle.Where(x=> !inv.itemList.Contains(x.Key)).Select(x=> x.Key).ToList())				
				{
					itemCycle.Remove(item);
					item.SetFlag(ItemR.Flag.Cooking, false);					
					item.MarkDirty();
				}
			}

			private void ProcessFuelCycle(Item fuel)
			{	
				if (fuel == null || !fuel.IsValid())
				{
					StopCooking();
					Destroy(this, 0.1f);					
					return;
				}
				
				var inv = oven.inventory;				
				var template = configData.SmeltObjects[oven.ShortPrefabName];
				
				if (!configData.SmeltTemplates.ContainsKey(template)) return;																
				
				var sobj = configData.SmeltTemplates[template].FirstOrDefault(r=> r.sourceName == fuel.info.shortname && r.isFuel && r.enabled);
				
				if (!itemCycle.ContainsKey(fuel))					
					itemCycle.Add(fuel, 0);										
				
				//int timeReady = (int)Math.Ceiling(10f * (200f / getCookingTemperature()) * 2f);								
				var timeReady = sobj.timeSmelt * 2f * 1f/sobj.speedRate;				
				itemCycle[fuel] += sobj.smeltRate;
				
				if (itemCycle[fuel] >= timeReady)
				{					
					int rem, amount = Math.DivRem((int)Math.Round(itemCycle[fuel] * 100f), (int)Math.Round(timeReady * 100f), out rem);
					itemCycle[fuel] = rem / 100f;	
																					
					object[] objArray = new object[] { oven, fuel, null };
					Interface.CallHook("OnConsumeFuel", objArray);					
															
					RemoveRawItems(fuel, amount * sobj.sourceAmount);
								
					var targetAmount = GetRandomAmount(sobj.targetAmount);
					AddSmeltedItems(sobj.targetName, amount * targetAmount);
				}				
			}
			
			private void RemoveRawItems(Item item, int amount)
			{
				if (amount <= 0) return;
				
				if (item.amount <= amount)				
				{	
					itemCycle.Remove(item);
					item.Remove(0f);
				}
				else
				{
					item.amount -= amount;
					item.MarkDirty();
				}					
			}						
			
			private void AddSmeltedItems(string itemname, int amount)
			{				
				if (string.IsNullOrEmpty(itemname)) return;
				if (amount <= 0) return;
										
				var inv = oven.inventory;						
				Item item = ItemManager.CreateByPartialName(itemname, amount);
				if (!item.MoveToContainer(inv, -1, true))
				{					
					item.Drop(inv.dropPosition, inv.dropVelocity, new Quaternion());
					StopCooking();
					Destroy(this, 0.1f);					
					return;
				}
			}
			
			private int GetRandomAmount(List<int> amounts) => amounts[Rnd.Next(0, amounts.Count)];			
			
			private void NewCook()
			{
				if (oven == null || oven.IsDestroyed)
				{
					Destroy(this, 0.1f);					
					return;
				}

				if (!configData.SmeltObjects.ContainsKey(oven.ShortPrefabName))
				{
					StopCooking();		
					Destroy(this, 0.1f);					
					return;
				}
				
				Item item = FindBurnable();
				if (item == null)
				{
					StopCooking();		
					Destroy(this, 0.1f);					
					return;
				}																		
				
				DisableLeftBurnable(item);
				
				if (!item.HasFlag(ItemR.Flag.OnFire))
				{
					item.SetFlag(ItemR.Flag.OnFire, true);
					item.MarkDirty();
				}				
				
				ProcessFuelCycle(item);
				ProcessSmeltCycle();
			}
			
			private void DisableLeftBurnable(Item fuel)
			{
				foreach (Item item in oven.inventory.itemList)
				{
					if (item == fuel) continue;
					
					if (item.HasFlag(ItemR.Flag.OnFire))
					{
						item.SetFlag(ItemR.Flag.OnFire, false);
						item.MarkDirty();
					}
				}					
			}	
			
			private Item FindBurnable()
			{
				if (!configData.SmeltObjects.ContainsKey(oven.ShortPrefabName)) return null;
				
				var template = configData.SmeltObjects[oven.ShortPrefabName];
				
				if (!configData.SmeltTemplates.ContainsKey(template)) return null;
				
				foreach(var fuel in configData.SmeltTemplates[template].Where(x=> x.isFuel && x.enabled))
				{
					if (fuel == null) continue;
									
					var item = oven.inventory.itemList.FirstOrDefault(r=> r != null && r.IsValid() && r.info.shortname.Equals(fuel.sourceName));
					
					if (item != null) return item;
				}
				
				return null;
			}
			
			public void StopCooking()
			{
				oven.UpdateAttachmentTemperature();
				if (oven.inventory != null)
				{
					oven.inventory.temperature = 15f;
					foreach (Item item in oven.inventory.itemList)
					{
						if (item.HasFlag(ItemR.Flag.Cooking))
							item.SetFlag(ItemR.Flag.Cooking, false);
							
						if (!item.HasFlag(ItemR.Flag.OnFire))						
							continue;
						
						item.SetFlag(ItemR.Flag.OnFire, false);
						item.MarkDirty();
					}
				}
				
				this.CancelInvoke("NewCook");															
				oven.SetFlag(BaseEntity.Flags.On, false, false);
			}
			
			public void RestoreDefaultCooking()
			{
				this.CancelInvoke("NewCook");
				if (oven.IsOn()) oven.InvokeRepeating("Cook", 0.5f, 0.5f);						
			}						
			
			private float getCookingTemperature()
			{			
				switch (oven.temperature)
				{
					case BaseOven.TemperatureType.Warming:					
						return 50f;					
					case BaseOven.TemperatureType.Cooking:					
						return 200f;					
					case BaseOven.TemperatureType.Smelting:					
						return 1000f;					
					case BaseOven.TemperatureType.Fractioning:					
						return 1500f;					
				}
				return 15f;			
			}			
		}	      
		
		#endregion		
		
		#region Config        
		
        private static ConfigData configData;
		
        private class ConfigData
        {
			[JsonProperty(PropertyName = "Шаблоны плавки")]
			public Dictionary<string, List<SmeltObjectInfo>> SmeltTemplates;
			[JsonProperty(PropertyName = "Связь контейнера плавки и шаблона")]
            public Dictionary<string, string> SmeltObjects { get; set; }
        }
		
		private class SmeltObjectInfo
		{	
			[JsonProperty(PropertyName = "Включено")]
			public bool enabled;
			[JsonProperty(PropertyName = "Название сырья")]
			public string sourceName;
			[JsonProperty(PropertyName = "Название переплавленного продукта")]
			public string targetName;	
			[JsonProperty(PropertyName = "Количество сырья обрабатываемого за раз")]
			public int sourceAmount;
			[JsonProperty(PropertyName = "Количество переплавленного продукта за раз")]
			public List<int> targetAmount;			
			[JsonProperty(PropertyName = "Стандартное время плавки (в секундах)")]
			public float timeSmelt;
			[JsonProperty(PropertyName = "Рейт времени плавки продукта")]
			public float speedRate;
			[JsonProperty(PropertyName = "Рейт переплавленного продукта")]
			public float smeltRate;
			[JsonProperty(PropertyName = "Данное сырье топливо")]
			public bool isFuel;			
		}			
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();       
		
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                SmeltTemplates = new Dictionary<string, List<SmeltObjectInfo>>()
				{
					{"cook", new List<SmeltObjectInfo>() 
							 {
								new SmeltObjectInfo() 
								{
									enabled = true,
									sourceName = "wood",
									targetName = "charcoal",
									sourceAmount = 1,
									targetAmount = new List<int>() {0, 1, 1},									
									timeSmelt = 10f,
									speedRate = 1f,
									smeltRate = 1f,
									isFuel = true
								},
								new SmeltObjectInfo() 
								{
									enabled = true,
									sourceName = "can.beans.empty",
									targetName = "metal.fragments",
									sourceAmount = 1,
									targetAmount = new List<int>() {15},									
									timeSmelt = 30f,
									speedRate = 1f,
									smeltRate = 1f,
									isFuel = false
								},
								new SmeltObjectInfo() 
								{
									enabled = true,
									sourceName = "can.tuna.empty",
									targetName = "metal.fragments",
									sourceAmount = 1,
									targetAmount = new List<int>() {10},									
									timeSmelt = 30f,
									speedRate = 1f,
									smeltRate = 1f,
									isFuel = false
								},
								new SmeltObjectInfo() 
								{
									enabled = true,
									sourceName = "bearmeat",
									targetName = "bearmeat.cooked",
									sourceAmount = 1,
									targetAmount = new List<int>() {1},								
									timeSmelt = 30f,
									speedRate = 1f,
									smeltRate = 1f,
									isFuel = false
								},
								new SmeltObjectInfo() 
								{
									enabled = true,
									sourceName = "meat.boar",
									targetName = "meat.pork.cooked",
									sourceAmount = 1,
									targetAmount = new List<int>() {1},									
									timeSmelt = 30f,
									speedRate = 1f,
									smeltRate = 1f,
									isFuel = false
								},
								new SmeltObjectInfo() 
								{
									enabled = true,
									sourceName = "chicken.raw",
									targetName = "chicken.cooked",
									sourceAmount = 1,
									targetAmount = new List<int>() {1},
									timeSmelt = 30f,
									speedRate = 1f,
									smeltRate = 1f,
									isFuel = false
								},
								new SmeltObjectInfo() 
								{
									enabled = true,
									sourceName = "fish.raw",
									targetName = "fish.cooked",
									sourceAmount = 1,
									targetAmount = new List<int>() {1},									
									timeSmelt = 30f,
									speedRate = 1f,
									smeltRate = 1f,
									isFuel = false
								},
								new SmeltObjectInfo() 
								{
									enabled = true,
									sourceName = "humanmeat.raw",
									targetName = "humanmeat.cooked",
									sourceAmount = 1,
									targetAmount = new List<int>() {1},									
									timeSmelt = 30f,
									speedRate = 1f,
									smeltRate = 1f,
									isFuel = false
								},
								new SmeltObjectInfo() 
								{
									enabled = true,
									sourceName = "wolfmeat.raw",
									targetName = "wolfmeat.cooked",
									sourceAmount = 1,
									targetAmount = new List<int>() {1},								
									timeSmelt = 30f,
									speedRate = 1f,
									smeltRate = 1f,
									isFuel = false
								},								
								new SmeltObjectInfo() 
								{
									enabled = true,
									sourceName = "horsemeat.raw",
									targetName = "horsemeat.cooked",
									sourceAmount = 1,
									targetAmount = new List<int>() {1},								
									timeSmelt = 30f,
									speedRate = 1f,
									smeltRate = 1f,
									isFuel = false
								},
								new SmeltObjectInfo() 
								{
									enabled = true,
									sourceName = "deermeat.raw",
									targetName = "deermeat.cooked",
									sourceAmount = 1,
									targetAmount = new List<int>() {1},								
									timeSmelt = 30f,
									speedRate = 1f,
									smeltRate = 1f,
									isFuel = false
								},
								
								new SmeltObjectInfo() 
								{
									enabled = true,
									sourceName = "bearmeat.cooked",
									targetName = "bearmeat.burned",
									sourceAmount = 1,
									targetAmount = new List<int>() {1},									
									timeSmelt = 60f,
									speedRate = 1f,
									smeltRate = 1f,
									isFuel = false
								},
								new SmeltObjectInfo() 
								{
									enabled = true,
									sourceName = "meat.pork.cooked",
									targetName = "meat.pork.burned",
									sourceAmount = 1,
									targetAmount = new List<int>() {1},									
									timeSmelt = 60f,
									speedRate = 1f,
									smeltRate = 1f,
									isFuel = false
								},
								new SmeltObjectInfo() 
								{
									enabled = true,
									sourceName = "chicken.cooked",
									targetName = "chicken.burned",
									sourceAmount = 1,
									targetAmount = new List<int>() {1},									
									timeSmelt = 60f,
									speedRate = 1f,
									smeltRate = 1f,
									isFuel = false
								},								
								new SmeltObjectInfo() 
								{
									enabled = true,
									sourceName = "wolfmeat.cooked",
									targetName = "wolfmeat.burned",
									sourceAmount = 1,
									targetAmount = new List<int>() {1},									
									timeSmelt = 60f,
									speedRate = 1f,
									smeltRate = 1f,
									isFuel = false
								},
								new SmeltObjectInfo() 
								{
									enabled = true,
									sourceName = "horsemeat.cooked",
									targetName = "horsemeat.burned",
									sourceAmount = 1,
									targetAmount = new List<int>() {1},								
									timeSmelt = 60f,
									speedRate = 1f,
									smeltRate = 1f,
									isFuel = false
								},								
								new SmeltObjectInfo() 
								{
									enabled = true,
									sourceName = "deermeat.cooked",
									targetName = "deermeat.burned",
									sourceAmount = 1,
									targetAmount = new List<int>() {1},								
									timeSmelt = 60f,
									speedRate = 1f,
									smeltRate = 1f,
									isFuel = false
								},															
								new SmeltObjectInfo() 
								{
									enabled = false,
									sourceName = "fish.cooked",
									targetName = "chicken.burned",    // improvisation
									sourceAmount = 1,
									targetAmount = new List<int>() {1},									
									timeSmelt = 60f,
									speedRate = 1f,
									smeltRate = 1f,
									isFuel = false
								},
								new SmeltObjectInfo() 
								{
									enabled = false,
									sourceName = "humanmeat.cooked",
									targetName = "humanmeat.spoiled", // improvisation
									sourceAmount = 1,
									targetAmount = new List<int>() {1},									
									timeSmelt = 60f,
									speedRate = 1f,
									smeltRate = 1f,
									isFuel = false
								}
							 }  
					},					
					{"furnace", new List<SmeltObjectInfo>() 
								{
									new SmeltObjectInfo() 
									{
										enabled = true,
										sourceName = "wood",
										targetName = "charcoal",
										sourceAmount = 1,
										targetAmount = new List<int>() {0, 1, 1},
										timeSmelt = 2f,
										speedRate = 1f,
										smeltRate = 1f,
										isFuel = true
									},
									new SmeltObjectInfo() 
									{
										enabled = true,
										sourceName = "hq.metal.ore",
										targetName = "metal.refined",
										sourceAmount = 1,
										targetAmount = new List<int>() {1},									    
										timeSmelt = 20f,
										speedRate = 1f,
										smeltRate = 1f,
										isFuel = false
									},
									new SmeltObjectInfo() 
									{
										enabled = true,
										sourceName = "metal.ore",
										targetName = "metal.fragments",
										sourceAmount = 1,
										targetAmount = new List<int>() {1},									   
										timeSmelt = 10f,
										speedRate = 1f,
										smeltRate = 1f,
										isFuel = false
									},
									new SmeltObjectInfo() 
									{
										enabled = true,
										sourceName = "sulfur.ore",
										targetName = "sulfur",
										sourceAmount = 1,
										targetAmount = new List<int>() {1},									    
										timeSmelt = 5f,
										speedRate = 1f,
										smeltRate = 1f,
										isFuel = false
									}
								}
					},																			
					{"refinery", new List<SmeltObjectInfo>() 
								 {
									new SmeltObjectInfo() 
									{
										enabled = true,
										sourceName = "wood",
										targetName = "charcoal",
										sourceAmount = 1,
										targetAmount = new List<int>() {0, 1, 1},									    
										timeSmelt = 1.5f,
										speedRate = 1f,
										smeltRate = 1f,
										isFuel = true
									},
									new SmeltObjectInfo() 
									{
										enabled = true,
										sourceName = "crude.oil",
										targetName = "lowgradefuel",
										sourceAmount = 1,
										targetAmount = new List<int>() {3},									    
										timeSmelt = 10f,
										speedRate = 1f,
										smeltRate = 1f,
										isFuel = false
									}															
								 }
					},						
					{"lamp", new List<SmeltObjectInfo>() 
							 {
								new SmeltObjectInfo() 
								{
									enabled = true,
									sourceName = "lowgradefuel",
									targetName = "",
									sourceAmount = 1,
									targetAmount = new List<int>() {0},								
									timeSmelt = 600f,
									speedRate = 1f,
									smeltRate = 1f,
									isFuel = true
								}
							 }
					},													
					{"lamp.hat", new List<SmeltObjectInfo>() 
								 {
									new SmeltObjectInfo() 
									{
										enabled = true,
										sourceName = "lowgradefuel",
										targetName = "",
										sourceAmount = 1,
										targetAmount = new List<int>() {0},									
										timeSmelt = 60f,
										speedRate = 1f,
										smeltRate = 1f,
										isFuel = true
									}
								 } 
					},
				    {"fogmachine", new List<SmeltObjectInfo>() 
								 {
									new SmeltObjectInfo() 
									{
										enabled = true,
										sourceName = "lowgradefuel",
										targetName = "",
										sourceAmount = 1,
										targetAmount = new List<int>() {0},										
										timeSmelt = 5f,
										speedRate = 1f,
										smeltRate = 1f,
										isFuel = true
									}
								 } 
					},
					{"searchlight", new List<SmeltObjectInfo>() 
									{
										new SmeltObjectInfo() 
										{
											enabled = true,
											sourceName = "lowgradefuel",
											targetName = "",
											sourceAmount = 1,
											targetAmount = new List<int>() {0},
											timeSmelt = 20f,
											speedRate = 1f,
											smeltRate = 1f,
											isFuel = true
										}
									} 
					}
				},
				
				SmeltObjects = new Dictionary<string, string>()
				{
					{ "campfire" , "cook" },
					{ "skull_fire_pit" , "cook" },
					{ "cursedcauldron.deployed" , "cook" },
					{ "bbq.deployed" , "cook" },
					{ "hobobarrel_static" , "cook" },
					
					{ "furnace" , "furnace" },
					{ "furnace.large" , "furnace" },
					{ "furnace_static" , "furnace" },					
					
					{ "refinery_small_deployed" , "refinery" },
					{ "small_refinery_static" , "refinery" },
					
					{ "ceilinglight.deployed" , "lamp" },
					{ "lantern.deployed" , "lamp" },					
					{ "tunalight.deployed" , "lamp" },
					{ "jackolantern.angry" , "lamp" },
					{ "jackolantern.happy" , "lamp" },
					
					{ "fogmachine" , "fogmachine" },
					
					{ "searchlight.deployed" , "searchlight" },
					
					{ "hat.miner" , "lamp.hat" },
					{ "hat.candle" , "lamp.hat" }
				}
            };
            SaveConfig(configData);
			timer.Once(0.5f, ()=>SaveConfig(configData));
        }
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
		
    }
}
