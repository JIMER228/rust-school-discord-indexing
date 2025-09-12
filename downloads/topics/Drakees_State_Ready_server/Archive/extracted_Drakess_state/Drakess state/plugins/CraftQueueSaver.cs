using Facepunch;
using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Oxide.Plugins
{	
	/* Based on version 1.1.0 by Jake_Rich */
    [Info("CraftQueueSaver", "Jake_Rich|Nimant", "1.0.3")]
    public class CraftQueueSaver : RustPlugin
    {                
		
		#region Variables
				
		private static List<ulong> AwakeLoadCraft = new List<ulong>();
		
		private static bool NeedSave = false;
		
		#endregion
	
		#region Hooks        

		private void Init() 
		{
			LoadVariables();
			LoadData();
		}
		
        private void OnServerInitialized()
        {
			RunSaver();
			
            foreach(var player in BasePlayer.activePlayerList)            
                LoadQueue(player);
        }

        private void Unload() => OnNewSave();

		private void OnNewSave()
        {
            CraftQueueDatabase.queueData.Clear();
            SaveData();
        }
		
        private void OnPlayerConnected(BasePlayer player) 
		{
			if (player == null) return;
			
			if (!AwakeLoadCraft.Contains(player.userID))
				AwakeLoadCraft.Add(player.userID);						
		}
		
		private void OnPlayerSleepEnded(BasePlayer player)
		{
			if (player == null) return;
			
			if (AwakeLoadCraft.Contains(player.userID))
			{
				LoadQueue(player);
				AwakeLoadCraft.Remove(player.userID);
			}
		}

        private void OnItemCraft(ItemCraftTask task, BasePlayer crafter) => SaveQueue(crafter); 		
		
		private void OnItemCraftFinished(ItemCraftTask task, Item item) => SaveQueue(task?.owner);		
		
		private void OnItemCraftCancelled(ItemCraftTask task) => SaveQueue(task?.owner);

        #endregion
		
		#region Saver
		
		private void RunSaver()
		{
			if (NeedSave) 
			{
				SaveData();
				NeedSave = false;				
			}			
			
			timer.Once(configData.SaveTime, RunSaver);
		}
		
		#endregion

        #region Classes

        private class CraftQueueDatabase
        {
            public static Dictionary<ulong, List<SerializedItemCraftTask>> queueData = new Dictionary<ulong, List<SerializedItemCraftTask>>();

			private static List<SerializedItemCraftTask> GetQueue(BasePlayer player)
			{
				if (player != null)
					return player.inventory.crafting.queue.Where(x=> x != null && !x.cancelled && x.amount > 0).Select(x => new SerializedItemCraftTask(x)).ToList();
				
				return new List<SerializedItemCraftTask>();
			}
			
            public static void SaveQueue(BasePlayer player)
            {
				if (player == null) return;
                queueData[player.userID] = GetQueue(player);
            }

            public static bool LoadQueue(BasePlayer player)
            {
				if (player == null) return false;
				
                List<SerializedItemCraftTask> data;
                if (!queueData.TryGetValue(player.userID, out data))                
                    return false;
                
                foreach(var item in data)                
                    item.Deserialize(player);
                
                queueData.Remove(player.userID);
                return true;
            }
        }        

        private class SerializedItemCraftTask
        {
            private List<Item> TakenItems;

            public int amount { get; set; }
            public int skinDefinition { get; set; }
            public int itemID { get; set; }
            public bool fromTempBlueprint { get; set; }

            public List<string> itemBytes { get; set; }

            public SerializedItemCraftTask(ItemCraftTask craftTask)
            {
				if (craftTask != null && !craftTask.cancelled && craftTask.amount > 0)
				{
					amount = craftTask.amount;
					skinDefinition = craftTask.skinID;
					itemID = craftTask.blueprint.targetItem.itemid;
					TakenItems = craftTask.takenItems;
				}
				
                SerializeItems();
            }

            public SerializedItemCraftTask() {}

            private void SerializeItems() => itemBytes = TakenItems?.Select(x => System.Convert.ToBase64String(x.Save().ToProtoBytes())).ToList();            

            public void Deserialize(BasePlayer player)
            {
				if (player == null) return;
				
                player.inventory.crafting.taskUID++;
                ItemCraftTask craftTask = Pool.Get<ItemCraftTask>();
                craftTask.blueprint = ItemManager.bpList.FirstOrDefault(x => x.targetItem.itemid == itemID);
                if (craftTask.blueprint == null) return;
                
                if (TakenItems == null || TakenItems.Count == 0) DeserializeItems();				
				if (TakenItems == null || TakenItems.Count == 0) return;
                
                craftTask.takenItems = TakenItems;
                craftTask.endTime = 0f;
                craftTask.taskUID = player.inventory.crafting.taskUID++;
                craftTask.owner = player;
                craftTask.amount = amount;
                craftTask.skinID = skinDefinition;
				
                if (fromTempBlueprint)
                {
                    var bpItem = ItemManager.CreateByName("blueprint");
                    bpItem.blueprintTarget = itemID;
                    craftTask.takenItems.Add(bpItem);
                    craftTask.conditionScale = 0.5f;
                }
				
                object obj = Interface.CallHook("OnItemCraft", new object[] { craftTask, player, fromTempBlueprint });
                if (obj is bool) return;
                
                player.inventory.crafting.queue.AddLast(craftTask);
				
                if (craftTask.owner != null)                
                    craftTask.owner.Command("note.craft_add", new object[] { craftTask.taskUID, craftTask.blueprint.targetItem.itemid, amount, craftTask.skinID });                
            }

            private void DeserializeItems() => TakenItems = itemBytes?.Select(x => ItemManager.Load(ProtoBuf.Item.Deserialize(System.Convert.FromBase64String(x)), null, true)).ToList();
        }        

        #endregion       

		#region Queue				

        private void SaveQueue(BasePlayer player)
        {
			if (player == null) return;			           

            CraftQueueDatabase.SaveQueue(player);
            NeedSave = true;
        }

        private void LoadQueue(BasePlayer player)
        {
			if (player == null) return;
			
            if (CraftQueueDatabase.LoadQueue(player))
				NeedSave = true;
        }

		#endregion
		
		#region Config
		
        private static ConfigData configData;
		
        private class ConfigData
        {            
			[JsonProperty(PropertyName = "Частота сохранения данных (секунды)")]
			public float SaveTime;
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                SaveTime = 5f
            };
            SaveConfig(configData);
			timer.Once(0.1f, ()=> SaveConfig(configData));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
		
		#region Data				
		
		private void LoadData() => CraftQueueDatabase.queueData = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, List<SerializedItemCraftTask>>>("CraftQueueSaverData");					
		
		private void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("CraftQueueSaverData", CraftQueueDatabase.queueData);
		
		#endregion
		
    }
}