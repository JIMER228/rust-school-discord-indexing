using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Rust;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{	
    [Info("CraftSystem", "Nimant", "1.0.4")]
    class CraftSystem : RustPlugin
    {        
		
		#region Variables
										
		private const string InstaPerm = "craftsystem.instacraft";
		private static CraftSystem Ins = null;
		private static System.Random Rnd = new System.Random();
        private static Dictionary<string, float> SaveBlueprints = new Dictionary<string, float>();                        						      						
		private static HashSet<int> NoRealCancel = new HashSet<int>();
		private static Dictionary<ulong, TemporaryContainer> TmpConts = new Dictionary<ulong, TemporaryContainer>();
		private static Dictionary<ulong, string> PlayerLastCancelTask = new Dictionary<ulong, string>();

		private class TemporaryContainer
		{
			private string userName;
			private ulong userID;
			private Vector3 userPos;
			private Timer userTimer;
			private ItemContainer cont;
			
			public static TemporaryContainer Init(BasePlayer player)
			{
				if (!TmpConts.ContainsKey(player.userID))				
					TmpConts.Add(player.userID, new TemporaryContainer());								
				
				TmpConts[player.userID].userName = player.displayName;
				TmpConts[player.userID].userID	= player.userID;
				TmpConts[player.userID].userPos = player.transform.position;
				
				if (TmpConts[player.userID].userTimer != null)
				{
					TmpConts[player.userID].userTimer.Destroy();
					TmpConts[player.userID].userTimer = null;
				}
				
				var userID = player.userID;
				
				TmpConts[player.userID].userTimer = Ins.timer.Once(0.25f, ()=>
				{
					if (TmpConts.ContainsKey(userID))
					{
						if (TmpConts[userID].cont != null) 
							TmpConts[userID].DropBackpack();
						
						TmpConts[userID].userTimer = null;
						TmpConts[userID].cont = null;
					}
				});
				
				return TmpConts[player.userID];
			}
			
			private void DropBackpack()
			{
				var cont_ = cont;
				float dx = Rnd.Next(0,11)/10f-0.5f;
				float dy = 0.5f;
				float dz = Rnd.Next(0,11)/10f-0.5f;

				Ins.timer.Once(Rnd.Next(1,11)/10f, ()=>
				{
					var drop = ItemContainer.Drop("assets/prefabs/misc/item drop/item_drop_backpack.prefab", new Vector3(userPos.x+dx, userPos.y+dy, userPos.z+dz), Quaternion.identity, cont_);
					if (drop != null)
					{
						drop.playerName = userName;
						drop.playerSteamID = userID;
					}
				});			
			}
			
			public ItemContainer container 
			{
				get { return cont; }
				set 
				{  
					if (cont != null) DropBackpack();
					cont = value;										
				}
			}
		}
		
        #endregion
		
		#region Hooks
		
		private void Init() 
		{
			Ins = this;
			LoadVariables();
			permission.RegisterPermission(InstaPerm, this);
			
			foreach (var pair in configData.IndividualPrivCraftingRates)
				permission.RegisterPermission(pair.Key, this);
		}	

        private void OnServerInitialized()
        {						
			SaveBlueprints.Clear();
			
			var blueprintDefinitions = ItemManager.bpList;
			foreach (var bp in blueprintDefinitions)
                SaveBlueprints.Add(bp.targetItem.shortname, bp.time);
				
            foreach (var bp in blueprintDefinitions)
            {
                if (configData.IndividualItemCraftingRates.ContainsKey(bp.targetItem.displayName.english))
                    bp.time = SaveBlueprints[bp.targetItem.shortname] * configData.IndividualItemCraftingRates[bp.targetItem.displayName.english] / 100f;
                else
                    bp.time = SaveBlueprints[bp.targetItem.shortname] * configData.GlobalCraftingRate / 100f;
            }																		
        }				            

		private void OnPlayerConnected(BasePlayer player)		
		{
			if (player == null) return;
			
			if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.Once(0.11f, () => OnPlayerConnected(player));
                return;
            }
			
			player.Command("note.craft_done", new object[] { 0, 0 });			
		}
		
		private void Unload()
        {
			foreach (var bp in ItemManager.bpList)
                bp.time = SaveBlueprints[bp.targetItem.shortname];		            														
        }  	
		
		private void OnShutDown()
        {					
            foreach (var player in BasePlayer.activePlayerList.Where(x=> x != null && x.userID.IsSteamId()))                           
                CancelAllCrafting(player);
        } 		
		
		private object OnItemCraft(ItemCraftTask task, BasePlayer crafter)
        {
			if (task == null || crafter == null) return null;
			
            var itemName = task.blueprint.targetItem.displayName.english;         																																																																				
			
			// блок крафта запрещенных предметов
			if (configData.BlockedItems.Contains(itemName))
			{
				task.cancelled = true;
				SendReply(crafter, string.Format("Крафт предмета <color=yellow>{0}</color> запрещен.", itemName));	            
				
				RefundIngredients(task);
					
				task.owner.inventory.containerMain.MarkDirty();
				task.owner.inventory.containerBelt.MarkDirty();					            

				return false;
			}	

			// инстакрафт
			if (configData.GlobalCraftingRate == 0f || HasPermission(crafter.UserIDString, InstaPerm) || IsNearInstaWB(crafter))
			{								
				if (!CanTake(crafter, task.blueprint.targetItem.itemid, task.blueprint.amountToCreate*task.amount))
				{
					task.cancelled = true;
					SendReply(crafter, "В инвентаре недостаточно места. Освободите слоты.");
					
					RefundIngredients(task);
					
					task.owner.inventory.containerMain.MarkDirty();
					task.owner.inventory.containerBelt.MarkDirty();								
						
					return false;
				}	
			
				task.endTime = 1f;
				return null;
			}																			
			
			ChangeRate(task?.owner, task.taskUID);
			
			return null;						
        }        			

        private void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
			if (task == null || item == null) return;
			
			// финишируем инстакрафт
			if (configData.GlobalCraftingRate == 0f || HasPermission(task.owner.UserIDString, InstaPerm) || IsNearInstaWB(task.owner))
			{
				var crafting = task.owner.inventory.crafting;
				if (crafting.queue.Count == 0) return;																							
				
				if (!CanTake(task.owner, task.blueprint.targetItem.itemid, task.blueprint.amountToCreate * task.amount)) return;
							
				for(int ii = 0; ii < task.amount; ii++)
				{																	
					Item t = ItemManager.CreateByItemID(item.info.itemid, 1, (ulong)task.skinID);			
					t.amount = item.amount;
					if (!t.MoveToContainer(task.owner.inventory.containerMain, -1, true))						
						if (!t.MoveToContainer(task.owner.inventory.containerBelt, -1, true))
							t.Drop(task.owner.eyes.position, task.owner.eyes.BodyForward() * 2f); 
				}												
				
				task.owner.inventory.containerMain.MarkDirty();
				task.owner.inventory.containerBelt.MarkDirty();		
				
				if (!NoRealCancel.Contains(task.taskUID))
					NoRealCancel.Add(task.taskUID);
				
				crafting.CancelTask(task.taskUID, false);
				return;
			}
			
			ChangeRate(task?.owner);						
        }										
		
		private void OnItemCraftCancelled(ItemCraftTask itemCraftTask)
		{
			if (itemCraftTask == null || itemCraftTask.owner == null) return;
			
			// исключаем обработку отмены крафта вызванную инстакрафтом
			if (NoRealCancel.Contains(itemCraftTask.taskUID)) 
			{
				NoRealCancel.Remove(itemCraftTask.taskUID);
				return;
			}			
			
			// отменяем крафт игрока включая безопасное выпадение ресурсов, если игрок бежал на крафте и его убили, 
			// но количество возвращаемых предметов не влезает в инвентарь, то все не влезшие предметы будут заключены в доп рюкзаки,
			// что бы не дудосить сервер взорвавшимися предметами
			if (configData.SafeCancelCraft && itemCraftTask.takenItems != null && itemCraftTask.takenItems.Count > 0)
			{
				if (PlayerLastCancelTask.ContainsKey(itemCraftTask.owner.userID))
				{
					var taskUID = Convert.ToInt32(PlayerLastCancelTask[itemCraftTask.owner.userID].Split('|')[0]);
					var time = Convert.ToSingle(PlayerLastCancelTask[itemCraftTask.owner.userID].Split('|')[1]);
					
					// отменяем складирование ресов в рюкзак, при ручной отмене крафта и переполненом инвентаре (все будет падать на землю)
					if ((Time.realtimeSinceStartup - time) < 1f && taskUID == itemCraftTask.taskUID)					
						return;					
				}
				
				var items = itemCraftTask.takenItems.ToList();
				var player = itemCraftTask.owner;
				var userID = player.userID;				
				itemCraftTask.takenItems.Clear();
								
				var tmpCont = TemporaryContainer.Init(player);				
				
				foreach (Item takenItem in items)
				{
					if (takenItem != null && takenItem.amount > 0)
					{							
						if (takenItem.IsBlueprint() && takenItem.blueprintTargetDef == itemCraftTask.blueprint.targetItem)						
							takenItem.UseItem(itemCraftTask.numCrafted);						
						
						if (takenItem.amount <= 0) continue; 
						
						if (tmpCont.container == null)							
							if (!takenItem.MoveToContainer(player.inventory.containerMain, -1, true))
								if (!takenItem.MoveToContainer(player.inventory.containerBelt, -1, true))
									tmpCont.container = NewItemCont();
						
						if (tmpCont.container == null) continue;												
						
						if (!takenItem.MoveToContainer(tmpCont.container, -1, true))
						{
							tmpCont.container = NewItemCont();
							
							if (!takenItem.MoveToContainer(tmpCont.container, -1, true))
								takenItem.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity);												
						}	
					}
				}
			}
			
			var lastTask = itemCraftTask.owner.inventory.crafting.queue.FirstOrDefault();
			if (lastTask.taskUID == itemCraftTask.taskUID)			
				ChangeRate(itemCraftTask?.owner, 0);
		}
		
		private void OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.Connection == null || arg.cmd == null || string.IsNullOrEmpty(arg.cmd.FullName) || !(arg.cmd.FullName == "craft.canceltask")) return;
            var player = arg.Player();			
			if (player == null || arg.Args == null || arg.Args.Length != 1) return;
			
			if (!PlayerLastCancelTask.ContainsKey(player.userID))
				PlayerLastCancelTask.Add(player.userID, "");
			
			PlayerLastCancelTask[player.userID] = $"{arg.Args[0]}|{Time.realtimeSinceStartup}";
		}
		
		#endregion						
		
		#region Helpers
		
		private bool HasPermission(string steamId, string perm) => permission.UserHasPermission(steamId, perm);                						
		
        private static void CancelAllCrafting(BasePlayer player)
        {
			if (player == null) return;
			
            var crafter = player.inventory.crafting;
            foreach (var task in crafter.queue)							
                crafter.CancelTask(task.taskUID, true);				
        }        
		
        private static bool CanTake(BasePlayer player, int itemId, int needAmount)
        {
			if (player == null) return false;
			
			var info = ItemManager.FindItemDefinition(itemId);
			if (info == null) return false;
			
            var containerMain = player.inventory.containerMain;
			var containerBelt = player.inventory.containerBelt;            			
			
			var sameItemsCount = containerMain.FindItemsByItemID(itemId).Count + containerBelt.FindItemsByItemID(itemId).Count;
			var freeSpaceCount = (containerMain.capacity - containerMain.itemList.Count) + (containerBelt.capacity - containerBelt.itemList.Count);
			var usedAmount = containerMain.GetAmount(itemId, true) + containerBelt.GetAmount(itemId, true);
			
			return ((sameItemsCount + freeSpaceCount) * info.stackable - usedAmount) >= needAmount;						
        }		            
		
		private static void RefundIngredients(ItemCraftTask task)
        {
			if (task == null) return;
			
			var items = new Dictionary<int, int>();
		
			foreach (var item in task.blueprint.ingredients)
			{
				if (!items.ContainsKey(item.itemid))
					items.Add(item.itemid, Convert.ToInt32(item.amount) * task.amount);
				else
					items[item.itemid] += Convert.ToInt32(item.amount) * task.amount;
			}					
			
			foreach (var item in items)
			{										
				var info = ItemManager.FindItemDefinition(item.Key);
				if (info == null) continue;
				var stackable = info.stackable;
				if (stackable <= 0) continue;
				var value = item.Value;
				
				while (value > stackable)
				{						
					Item i = ItemManager.CreateByItemID(item.Key, stackable);
					if (!i.MoveToContainer(task.owner.inventory.containerMain)) 
						if (!i.MoveToContainer(task.owner.inventory.containerBelt)) 
							i.Drop(task.owner.eyes.position, task.owner.eyes.BodyForward() * 2f); 

					value -= stackable;
				}
				
				if (value > 0)
				{
					Item i = ItemManager.CreateByItemID(item.Key, value);
					if (!i.MoveToContainer(task.owner.inventory.containerMain)) 
						if (!i.MoveToContainer(task.owner.inventory.containerBelt)) 
							i.Drop(task.owner.eyes.position, task.owner.eyes.BodyForward() * 2f); 
				}					
			}												        
        }
		
		private void ChangeRate(BasePlayer player, int taskUID = 0)
		{
			if (player == null) return;
			
			// уменьшенный рейт по привилегии
			if (configData.IndividualPrivCraftingRates.Count > 0)
			{
				var rate = float.MaxValue;
			
				foreach (var pair in configData.IndividualPrivCraftingRates.OrderBy(x=> x.Value))
				{
					if (HasPermission(player.UserIDString, pair.Key))
					{
						rate = pair.Value;
						break;
					}
				}
				
				if (rate < configData.GlobalCraftingRate)
				{					
					timer.Once(0.1f, ()=> 
					{
						if (player != null)
							ChangeCraftTime(player.userID, taskUID, rate);
					});
				}
			}
		}
		
		private void ChangeCraftTime(ulong userID, int taskUID, float rate)
		{			
			var player = BasePlayer.FindByID(userID);			
			if (player == null) return;

			var crafter = player.inventory.crafting;
			if (crafter.queue.Where(x => !x.cancelled).Count() == 0) return;						
			
			var task = crafter.queue.FirstOrDefault(x => !x.cancelled);
			if (task == null || string.IsNullOrEmpty(task.blueprint?.targetItem?.shortname)) return;
			
			if (taskUID != 0 && taskUID != task.taskUID) return;
						
			var rss = Time.realtimeSinceStartup;
			var bpTime = GetBpTime(player, task.blueprint);
			var taskEndTime = task.endTime * (100f / configData.GlobalCraftingRate); 
			
			float endTime = ( (taskEndTime - rss) > 0f && (taskEndTime - rss) <= bpTime ) ? taskEndTime : (rss + bpTime);																	
			float value = (endTime - rss) * (rate / 100f);
			if (value > bpTime) value = bpTime;
			
			timer.Once(0.1f, ()=> task.owner.Command("note.craft_start", new object[] { task.taskUID, value, task.amount }));
			
			float diff = rss + value;
			task.endTime = diff;
		}
		
		private static float GetBpTime(BasePlayer player, ItemBlueprint bp) => GetScaledDuration(bp, player.currentCraftLevel);		
		
		// копия метода из раста, но с нашим измененным bp.time
		private static float GetScaledDuration(ItemBlueprint bp, float workbenchLevel)
		{
			float single = workbenchLevel - (float)bp.workbenchLevelRequired;			
			var bpTime = SaveBlueprints.ContainsKey(bp.targetItem.shortname) ? SaveBlueprints[bp.targetItem.shortname] : bp.time;
			
			if (single == 1f)			
				return bpTime * 0.5f;
			
			if (single < 2f)			
				return bpTime;
			
			return bpTime * 0.25f;
		}
		
		private static bool IsNearInstaWB(BasePlayer player)
		{						
			if (configData.InstaCraftNearWorkBench3 && player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench3))
				return true;						
			
			return false;
		}
		
		private static ItemContainer NewItemCont()
		{
			var cont = new ItemContainer();			
            cont.ServerInitialize(null, 36);
            cont.GiveUID();			
            return cont;
		}				
		
		#endregion
		
		#region Config        
		
        private static ConfigData configData;
		
        private class ConfigData
        {
			[JsonProperty(PropertyName = "Глобальный рейт крафта (0-100%)")]
			public float GlobalCraftingRate;
			[JsonProperty(PropertyName = "Индивидуальный рейт крафта для отдельных предметов (полное имя и 0-100%)")]
			public Dictionary<string, float> IndividualItemCraftingRates;
			[JsonProperty(PropertyName = "Индивидуальный рейт крафта для отдельных привилегий (привилегия и 0-100%)")]
			public Dictionary<string, float> IndividualPrivCraftingRates;
			[JsonProperty(PropertyName = "Разрешить инстакрафт у верстака 3го уровня")]
			public bool InstaCraftNearWorkBench3;
			[JsonProperty(PropertyName = "Заблокированные для крафта предметы (полное имя)")]
			public List<string> BlockedItems;
			[JsonProperty(PropertyName = "Разрешить безопасную отмену крафта (антиддос предметами)")]
			public bool SafeCancelCraft;
        }
		
        private void LoadVariables() => LoadConfigVariables();                    
		
        private void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
				BlockedItems = new List<string>(),
				GlobalCraftingRate = 100f,
				IndividualItemCraftingRates = new Dictionary<string, float>(),
				IndividualPrivCraftingRates = new Dictionary<string, float>() 
				{
					{ "craftsystem.rate50", 50f },
					{ "craftsystem.rate25", 25f }
				},
				InstaCraftNearWorkBench3 = false,
				SafeCancelCraft = true
            };
            SaveConfig(config);
			timer.Once(0.1f, ()=> SaveConfig(config));
        }
		
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion	
				
    }
}
