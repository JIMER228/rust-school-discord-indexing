using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Oxide.Core.Configuration;
using UnityEngine;
using Oxide.Core;
using Newtonsoft.Json;

namespace Oxide.Plugins
{	
    [Info("RecycleManager", "Nimant", "1.1.0")]    
    class RecycleManager : RustPlugin
    {        		
	
		#region Variables
	
		private static System.Random Rnd = new System.Random();
		private static Dictionary<string, List<ItemInfo>> GlobalIngredientList = new Dictionary<string, List<ItemInfo>>();		
		private static Dictionary<uint, Timer> Recyclers = new Dictionary<uint, Timer>();
	
        private class ItemInfo
        {
			[NonSerialized]
            public ItemDefinition itemDef;
			[NonSerialized]
			public bool isCustom;
			
			[JsonProperty(PropertyName = "Группа предмета и количество для дропа")] // Группа | Количество предметов из нее для дропа
			public string groupDrop;
			[JsonProperty(PropertyName = "Название предмета")]
			public string itemName;
			[JsonProperty(PropertyName = "Скин предмета")]
			public ulong skinID;
			[JsonProperty(PropertyName = "Минимальное рандомное количество")]
			public int minAmount;
			[JsonProperty(PropertyName = "Максимальное рандомное количество")]
			public int maxAmount;
			[JsonProperty(PropertyName = "Шаг рандома")]
			public int stepAmount;			
        }
		
		#endregion                               

		#region Init
		
		private void Init() 
		{
			LoadVariables();
			GlobalIngredientList.Clear();
		}
		
		private void OnServerInitialized()
		{						
			foreach (ItemDefinition itemInfo in ItemManager.itemList)
			{
				if (itemInfo == null) continue;
				foreach (var pair in configData.IngredientList)
				{
					var list = pair.Key.Replace(" ","").Split('|');
					if (list.Count() == 0) continue;
					var skinID_ = list.Count() <= 1 ? ulong.MaxValue : ( list[1] == "all" ? ulong.MaxValue : Convert.ToUInt64(list[1]) );
					var itemName_ = list[0];
					var key_ = $"{itemName_}|{skinID_}";										
					
					if (itemName_ == itemInfo.shortname)
					{
						var newItems = pair.Value;
						
						foreach (var item in newItems)
						{
							item.itemDef = ItemManager.FindItemDefinition(item.itemName);
							item.isCustom = true;
						}
						
						if (!GlobalIngredientList.ContainsKey(key_))
							GlobalIngredientList.Add(key_, newItems);						
					}
				}
				
				var key = $"{itemInfo.shortname}|{ulong.MaxValue}";
				if (itemInfo.Blueprint != null && itemInfo.Blueprint.ingredients?.Count > 0 && !GlobalIngredientList.ContainsKey(key))
				{
					var tmp = itemInfo.Blueprint.ingredients?.Select(y=> new ItemInfo() { itemDef = y.itemDef, groupDrop = $"{y.itemDef.shortname}|{1}", itemName = y.itemDef.shortname, skinID = 0, minAmount = (int)Math.Round(y.amount), maxAmount = (int)Math.Round(y.amount), stepAmount = 1 }).ToList();
					GlobalIngredientList.Add(key, tmp);
				}
			}			
		}		    

		#endregion
		
		#region Main Hooks
				
		private object OnRecyclerToggle(Recycler recycler, BasePlayer player)
        {   
			if (recycler == null) return null;						
			
			if (recycler.IsOn())
			{									
				if (Recyclers.ContainsKey(recycler.net.ID))
				{
					Recyclers[recycler.net.ID].Destroy();
					Recyclers.Remove(recycler.net.ID);
				}
				
				StopRecycling(recycler);				
				return false;
			}
			
			if (!HasRecyclable(recycler))
				return false;
			
			if (Recyclers.ContainsKey(recycler.net.ID) && Recyclers[recycler.net.ID] != null)
				Recyclers[recycler.net.ID].Destroy();
			
            Recyclers[recycler.net.ID] = timer.Once(5f * configData.RecycleTimeRateWait, ()=> RecycleThink(recycler));			
			StartRecycling(recycler);
		
			return true;
        }
		
		#endregion
		
		#region Main Code
		
		private static bool HasRecyclable(Recycler recycler)
		{			
			for (int i = 0; i < 6; i++)
			{
				var slot = recycler.inventory.GetSlot(i);
				if (slot != null)
				{
					var needSkip = false;
					
					foreach (var item in configData.InputBlacklistItems) 
					{
						var list = item.Replace(" ","").Split('|');
						if (list.Count() == 0) continue;
						var skinID_ = list.Count() <= 1 ? ulong.MaxValue : ( list[1] == "all" ? ulong.MaxValue : Convert.ToUInt64(list[1]) );
						var itemName_ = list[0];
						var key_ = $"{itemName_}|{skinID_}";
						
						if (slot.info.shortname == itemName_ && skinID_ == ulong.MaxValue || slot.info.shortname == itemName_ && skinID_ == slot.skin)
						{
							// черный список входных предметов распространяется только на дефолтные предметы
							if (!GlobalIngredientList.ContainsKey(key_) || GlobalIngredientList.ContainsKey(key_) && GlobalIngredientList[key_].FirstOrDefault().isCustom == false)
							{
								needSkip = true;
								break;
							}
						}
					}
					
					if (needSkip) continue;
										
					foreach (var pair in GlobalIngredientList)
					{
						var list = pair.Key.Split('|');
						var skinID_ = Convert.ToUInt64(list[1]);
						var itemName_ = list[0];						
						
						if (slot.info.shortname == itemName_ && skinID_ == ulong.MaxValue || slot.info.shortname == itemName_ && skinID_ == slot.skin)
						{
							if (pair.Value.Where(x=> x.isCustom || !configData.OutputBlacklistItems.Contains(x.itemName)).Count() > 0)
								return true;
						}
					}
				}
			}
			
			return false;
		}				

		private void RecycleThink(Recycler recycler)
	    {
			if (!recycler.IsOn()) return;
			
			var flag = false;
			var recEff = recycler.recycleEfficiency;
			for (int slot1 = 0; slot1 < 6; ++slot1)
			{
			    var slot2 = recycler.inventory.GetSlot(slot1);
			    if (slot2 != null)
			    {
					bool allowItem = false, needSkip = false;
					
					foreach (var item in configData.InputBlacklistItems)
					{
						var list = item.Replace(" ","").Split('|');
						if (list.Count() == 0) continue;
						var skinID_ = list.Count() <= 1 ? ulong.MaxValue : ( list[1] == "all" ? ulong.MaxValue : Convert.ToUInt64(list[1]) );
						var itemName_ = list[0];
						var key_ = $"{itemName_}|{skinID_}";												
						
						if (slot2.info.shortname == itemName_ && skinID_ == ulong.MaxValue || slot2.info.shortname == itemName_ && skinID_ == slot2.skin)
						{
							// черный список входных предметов распространяется только на дефолтные предметы
							if (!GlobalIngredientList.ContainsKey(key_) || GlobalIngredientList.ContainsKey(key_) && GlobalIngredientList[key_].FirstOrDefault().isCustom == false)
							{
								needSkip = true;
								break;
							}
						}
					}
					if (needSkip) continue;
					
					foreach (var pair in GlobalIngredientList)
					{
						var list = pair.Key.Split('|');
						var skinID_ = Convert.ToUInt64(list[1]);
						var itemName_ = list[0];						
						
						if (slot2.info.shortname == itemName_ && skinID_ == ulong.MaxValue || slot2.info.shortname == itemName_ && skinID_ == slot2.skin)
						{
							if (pair.Value.Where(x=> x.isCustom || !configData.OutputBlacklistItems.Contains(x.itemName)).Count() > 0)
							{
								allowItem = true;
								break;
							}
						}
					}
					if (!allowItem) continue;	
					
				    if (Interface.CallHook("OnRecycleItem", (object) recycler, (object) slot2) != null)
					{
						if (HasRecyclable(recycler))
							return;
						
						StopRecycling(recycler);
						return;
					}
													
					if (slot2.hasCondition)
						recEff = Mathf.Clamp01(recEff * Mathf.Clamp(slot2.conditionNormalized * slot2.maxConditionNormalized, 0.1f, 1f));
					
					int amountToConsume = 1;
					if (slot2.amount > 1)
						amountToConsume = Mathf.CeilToInt(Mathf.Min((float) slot2.amount, (float) slot2.info.stackable * (configData.MaxItemsPerRecycle / 100f)));
					
					// формируем ключ
					var key = $"{slot2.info.shortname}|{ulong.MaxValue}";	
					foreach (var pair in GlobalIngredientList)
					{
						var list = pair.Key.Split('|');
						var skinID_ = Convert.ToUInt64(list[1]);
						var itemName_ = list[0];						
						
						if (slot2.info.shortname == itemName_ && skinID_ == slot2.skin)
						{
							key = $"{slot2.info.shortname}|{skinID_}";
							break;
						}
					}
					
					var value = GlobalIngredientList[key];
					
					// изменяем количество скрапа для стандартных предметов, если требуется (а кастомные должны обрабатыватся вручную в конфиге)
					if (!value.FirstOrDefault().isCustom)
					{
						var multiScrap = configData.MultiRate;  
				
						if (configData.MultiRateIndividual.ContainsKey("scrap"))
							multiScrap = Convert.ToSingle(configData.MultiRateIndividual["scrap"]);	
						
						if (slot2.info.Blueprint?.scrapFromRecycle > 0 && !configData.OutputBlacklistItems.Contains("scrap"))
							recycler.MoveItemToOutput(ItemManager.CreateByName("scrap", Mathf.CeilToInt(slot2.info.Blueprint.scrapFromRecycle * amountToConsume * multiScrap), 0UL));
					}
					
					var listResult = new List<ItemInfo>();
					
					foreach (var item_ in value.Select(x=> x.groupDrop.Replace(" ","")).Distinct())
					{
						var group = item_.Split('|')[0];
						var count = Convert.ToInt32(item_.Split('|')[1]);						
						var items = value.Where(x=> x.groupDrop.Replace(" ","").Split('|')[0] == group).ToList();
						
						foreach (var itemRandom in items.OrderBy(x=> Rnd.Next()))
						{
							if (count <= 0) break;
							listResult.Add(itemRandom);
							count--;
						}
					}					
					
					//if (listResult.Count == 0) continue;
					
					slot2.UseItem(amountToConsume);
					
					using (var enumerator = listResult.GetEnumerator())
					{
						while (enumerator.MoveNext())
						{															
							var current = enumerator.Current;
							var amount = Rnd.Next((int)Math.Truncate(1f * current.minAmount / current.stepAmount), (int)Math.Truncate(1f * current.maxAmount / current.stepAmount) + 1) * current.stepAmount;
							if (amount <= 0) continue;
							
							// блочим запрещенные дефолтные предметы к выдаче, кроме кастомных переписанных предметов
							if (!current.isCustom && configData.OutputBlacklistItems.Contains(current.itemDef.shortname)) continue;
							
							var multi = 1f;
							
							// изменяем рейт только для дефолтных предметов
							if (!current.isCustom)
							{
								multi = configData.MultiRate;  
				
								if (configData.MultiRateIndividual.ContainsKey(current.itemDef.shortname))
									multi = Convert.ToSingle(configData.MultiRateIndividual[current.itemDef.shortname]);	
							}
							
							if (!current.isCustom && !(current.itemDef.shortname == "scrap") || current.isCustom) 
							{
								float correctAmount = !current.isCustom ? amount / (float) (slot2.info.Blueprint?.amountToCreate ?? 1f) : amount;
								int finalAmount = 0;																		
								
								if (correctAmount <= 1f)
								{
									for (int index = 0; index < amountToConsume; ++index)									
										if (UnityEngine.Random.Range(0f, 1f) <= (!current.isCustom ? recEff : 1f))  
											finalAmount++;
																			
									finalAmount = (int)Math.Round(multi * finalAmount);
								}
								else
									finalAmount = Mathf.CeilToInt(Mathf.CeilToInt(Mathf.Clamp(correctAmount * (!current.isCustom ? recEff : 1f), 0f, correctAmount) * amountToConsume) * multi);
								
								if (finalAmount > 0)
								{
									int iterations = Mathf.CeilToInt((float) finalAmount / (float) current.itemDef.stackable);
									
									for (int index = 0; index < iterations; ++index)
									{											
										int iAmount = finalAmount <= current.itemDef.stackable ? finalAmount : current.itemDef.stackable;
										
										if (!recycler.MoveItemToOutput(ItemManager.Create(current.itemDef, iAmount, current.skinID)))
											flag = true;
										
										finalAmount -= iAmount;
										
										if (finalAmount <= 0) break; 
									}
								}
							}
						}
						
						break;
					}				
				}
			}
			
			if (!flag && HasRecyclable(recycler) && recycler.IsOn())
			{	
				timer.Once(5f * configData.RecycleTimeRateWait, ()=> RecycleThink(recycler));
				return;
			} 
		  
			StopRecycling(recycler);
		}
		
		private static void StartRecycling(Recycler recycler)
		{
			if (recycler.IsOn()) return;
						
			Effect.server.Run(recycler.startSound.resourcePath, recycler, 0, Vector3.zero, Vector3.zero, null, false);
			recycler.SetFlag(BaseEntity.Flags.On, true, false);
			recycler.SendNetworkUpdateImmediate(false);
		}
		
		private static void StopRecycling(Recycler recycler)
		{
			if (!recycler.IsOn()) return;
			
			Effect.server.Run(recycler.stopSound.resourcePath, recycler, 0, Vector3.zero, Vector3.zero, null, false);
			recycler.SetFlag(BaseEntity.Flags.On, false, false);
			recycler.SendNetworkUpdateImmediate(false);
		}

		#endregion
		
		#region API
		
		// можно вызывать в любой момент (на старте сервера, после старта, главное что бы плагин был активен)
		// skinID = 0 - для дефолтного скина, X - для скинового, ulong.MaxValue - для всех
		// outputItems : Key = itemName|skinID, Value = minAmount|maxAmount|stepAmount|group|count
		private void API_AddRecycleItemInfo(string itemName, ulong skinID, Dictionary<string, string> outputItems)
		{
			var key = $"{itemName}|{skinID}";
			
			if (!GlobalIngredientList.ContainsKey(key))
				GlobalIngredientList.Add(key, new List<ItemInfo>());
			
			GlobalIngredientList[key].Clear();
			
			foreach (var pair in outputItems)
			{
				try
				{
					var listKey = pair.Key.Replace(" ","").Split('|');
					var skinID_ = Convert.ToUInt64(listKey[1]);
					var itemName_ = listKey[0];
					
					var listValue = pair.Value.Replace(" ","").Split('|');
					var minAmount_ = Convert.ToInt32(listValue[0]);
					var maxAmount_ = Convert.ToInt32(listValue[1]);
					var stepAmount_ = Convert.ToInt32(listValue[2]);
					var group_ = listValue[3];
					var count_ = Convert.ToInt32(listValue[4]);
					
					var tmp = new ItemInfo() 
					{ 
						itemDef = ItemManager.FindItemDefinition(itemName_), 
						itemName = itemName_, 
						skinID = skinID_, 
						minAmount = minAmount_, 
						maxAmount = maxAmount_, 
						stepAmount = stepAmount_,
						groupDrop = $"{group_}|{count_}",
						isCustom = true
					};
					
					GlobalIngredientList[key].Add(tmp);				
				}
				catch 
				{
					PrintWarning($"Ошибка парсинга строки: '{pair.Key}:{pair.Value}'");
				}
			}
		}
		
		#endregion
		
        #region Config        				
		
        private static ConfigData configData;
		
        private class ConfigData
        {            
			[JsonProperty(PropertyName = "Рейт длительности переработки одного предмета (1 = дефолтные 5 секунд)")]
			public float RecycleTimeRateWait;
			[JsonProperty(PropertyName = "Максимальная порция предметов перерабатываемых за раз (10% = дефолтный процент от максимального стака)")]
			public int MaxItemsPerRecycle;
			[JsonProperty(PropertyName = "Общий рейт ресурсов и компонентов на выходе, исключая кастомный список (1 = дефолт)")]
			public float MultiRate;
			[JsonProperty(PropertyName = "Частный рейт ресурса или компонента на выходе, исключая кастомный список")]
			public Dictionary<string, float> MultiRateIndividual;
			[JsonProperty(PropertyName = "Список запрещенных предметов к переработке, исключая кастомный список (имя предмета | 0 - деф скин, X - определённый, all - все)")]
			public List<string> InputBlacklistItems;						
			[JsonProperty(PropertyName = "Список запрещенных предметов на выходе переработчика, исключая кастомный список (имя предмета)")]
			public List<string> OutputBlacklistItems;						
			[JsonProperty(PropertyName = "Кастомный список предметов для переработки (имя предмета | 0 - деф скин, X - определённый, all - все)")]
			public Dictionary<string, List<ItemInfo>> IngredientList = new Dictionary<string, List<ItemInfo>>();
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();
		
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {                
				RecycleTimeRateWait = 1f,
				MaxItemsPerRecycle = 10,
				MultiRate = 1f,
				MultiRateIndividual = new Dictionary<string, float>(),
				InputBlacklistItems = new List<string>()
				{
					"rock | 2084257363"
				},
				OutputBlacklistItems = new List<string>(),
				IngredientList = new Dictionary<string, List<ItemInfo>>()
				{
					{ "coal | 0", new List<ItemInfo>(){ new ItemInfo() { groupDrop = "A|1", itemName = "charcoal", skinID = 0, minAmount = 20, maxAmount = 20, stepAmount = 1 } } }
				}
            };
            SaveConfig(config);
			timer.Once(0.1f, ()=> SaveConfig(config));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion				        
        
    }
}