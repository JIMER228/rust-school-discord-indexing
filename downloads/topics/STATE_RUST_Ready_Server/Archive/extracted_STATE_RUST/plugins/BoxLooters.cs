using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
	/* Based on version v 0.3.4 */
    [Info("BoxLooters", "4seti / k1lly0u / Nimant", "1.0.1")]
    class BoxLooters : RustPlugin
    {
        #region Fields
		
        private static BoxDS boxData;        
        private static DynamicConfigFile bdata;                      
        private static bool eraseData = false;
        private static Hash<uint, BoxData> boxCache;        
		
        #endregion

        #region Oxide Hooks
		
        private void Init()
        {
            bdata = Interface.Oxide.DataFileSystem.GetFile("BoxLootersData");                        
            boxCache = new Hash<uint, BoxData>();            
            lang.RegisterMessages(messages, this);            
        }
		
        private void OnServerInitialized()
        {            
            LoadVariables();
            LoadData();
            if (eraseData)
                ClearAllData();
            else 
				RemoveOldData();
        }
		
        private void OnNewSave(string filename) => eraseData = true;        
		
        private void OnServerSave() => timer.Once(5f, SaveData);
		
        private void Unload() => SaveData();        

        private void OnLootEntity(BasePlayer looter, BaseEntity entity)
        {
            if (looter == null || entity == null || !entity.IsValid() || !IsValidType(entity)) return;

			// фиксируем только ценные контейнеры
			if (entity.PrefabName != "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab" &&
			    entity.PrefabName != "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab" &&
			    entity.PrefabName != "assets/prefabs/deployable/dropbox/dropbox.deployed.prefab" &&
			    entity.PrefabName != "assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab" &&
			    entity.PrefabName != "assets/prefabs/misc/halloween/coffin/coffinstorage.prefab") return;
			
            var time = GrabCurrentTime();
            var date = DateTime.Now.ToString("d/M @ HH:mm:ss");                        
                        
			var boxId = entity.net.ID;
			if (!boxCache.ContainsKey(boxId))
				boxCache[boxId] = new BoxData(looter, time, date, entity.transform.position);
			else 
				boxCache[boxId].AddLooter(looter, time, date);             
        }
		
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
        {
            try
            {
                if (entity == null || !entity.IsValid() || !IsValidType(entity) || entity is BasePlayer) return;
                if (hitInfo?.Initiator is BasePlayer)
                {
                    var boxId = entity.net.ID;
                    if (!boxCache.ContainsKey(boxId)) return;
                    boxCache[boxId].OnDestroyed(hitInfo.InitiatorPlayer);
                }
            }
            catch { }
        }
		
        #endregion

        #region Data Cleanup
        
		private void ClearAllData() => boxCache.Clear(); 
        
        private void RemoveOldData()
        {            
            int boxCount = 0;        
            double time = GrabCurrentTime() - (configData.RemoveHours * 3600);

            for (int i = 0; i < boxCache.Count; i++)
            {
                KeyValuePair<uint, BoxData> boxEntry = boxCache.ElementAt(i);
                if (boxEntry.Value.lastAccess < time)
                {
                    boxCache.Remove(boxEntry.Key);
                    ++boxCount;
                }
            }
        }
		
        #endregion

        #region Functions
		
        private object FindBoxFromRay(BasePlayer player)
        {
            Ray ray = new Ray(player.eyes.position, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward);
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 20))
                return null;

            var hitEnt = hit.collider.GetComponentInParent<BaseEntity>();
            if (hitEnt != null)
            {
                if (IsValidType(hitEnt))
                    return hitEnt;
            }
            return null;            
        }
		
        private void ReplyInfo(BasePlayer player, string Id, bool isPlayer = false, string additional = "")
        {
            var entId = Id;
            if (!string.IsNullOrEmpty(additional))
                entId = $"{additional} - {Id}";
                           
			if (boxCache.ContainsKey(uint.Parse(Id)))
			{
				var box = boxCache[uint.Parse(Id)];
				SendReply(player, string.Format(lang.GetMessage("BoxInfo", this, player.UserIDString), entId));

				if (!string.IsNullOrEmpty(box.killerName))
					SendReply(player, string.Format(lang.GetMessage("DetectDestr", this, player.UserIDString), box.killerName, box.killerId));

				int i = 1;
				string response1 = string.Empty;
				string response2 = string.Empty;
				foreach (var data in box.lootList.GetLooters().Reverse().Take(10))
				{
					var respString = string.Format(lang.GetMessage("DetectedLooters", this, player.UserIDString), i, data.userName, data.userId, data.firstLoot, data.lastLoot);
					if (i < 6) response1 += respString;
					else response2 += respString;
					i++;                        
				}
				SendReply(player, response1);
				SendReply(player, response2);
			}
			else SendReply(player, string.Format(lang.GetMessage("NoLooters", this, player.UserIDString), entId));		
        }
		
        #endregion

        #region Helpers
		
        private static double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;        
		
        private static float GetDistance(Vector3 init, Vector3 target) => Vector3.Distance(init, target);
		
        private static bool IsValidType(BaseEntity entity) => !entity.GetComponent<LootContainer>() && entity is StorageContainer;
		
        #endregion

        #region Commands
		
        [ChatCommand("box")]
        private void cmdBox(BasePlayer player, string command, string[] args)
        {
			if (player == null) return;
			
            if (player.net.connection.authLevel < 2 && !player.IsAdmin)
			{
				SendReply(player, "Недостаточно прав !");
				return;
			}
			
            if (args == null || args.Length == 0)
            {
                var success = FindBoxFromRay(player);                
                if (success is StorageContainer)
                    ReplyInfo(player, (success as BaseEntity).net.ID.ToString());
                else 
					SendReply(player, lang.GetMessage("Nothing", this, player.UserIDString));
				
                return;
            }
            switch (args[0].ToLower())
            {
                case "help":
                    {
                        SendReply(player, $"<color=#4F9BFF>{Title} v{Version}</color>");
                        SendReply(player, "<color=#4F9BFF>/box help</color> - Display the help menu");
                        SendReply(player, "<color=#4F9BFF>/box</color> - Retrieve information on the box you are looking at");                        
                        SendReply(player, "<color=#4F9BFF>/box id <number></color> - Retrieve information on the specified box");
                        SendReply(player, "<color=#4F9BFF>/box near <opt:radius></color> - Show nearby boxes (current and destroyed) and their ID numbers");                        
                        SendReply(player, "<color=#4F9BFF>/box clear</color> - Clears all saved data");
                        SendReply(player, "<color=#4F9BFF>/box save</color> - Saves box data");
                    }
                    return;
                case "id":
                    if (args.Length >= 2)
                    {
                        uint id;
                        if (uint.TryParse(args[1], out id))                        
                            ReplyInfo(player, id.ToString());                        
                        else SendReply(player, lang.GetMessage("NoID", this, player.UserIDString));
                        return;
                    }
                    break;
                case "near":
                    {
                        float radius = 20f;
                        if (args.Length >= 2)
                        {
                            if (!float.TryParse(args[1], out radius))
                                radius = 20f;
                        }
                        foreach(var box in boxCache)
                        {
                            if (GetDistance(player.transform.position, box.Value.GetPosition()) <= radius)
                            {
                                player.SendConsoleCommand("ddraw.text", 20f, Color.green, box.Value.GetPosition() + new Vector3(0, 1.5f, 0), $"<size=40>{box.Key}</size>");
                                player.SendConsoleCommand("ddraw.box", 20f, Color.green, box.Value.GetPosition(), 1f);
                            }
                        }
                    }
                    return;                
                case "clear":
                    boxCache.Clear();                    
                    SendReply(player, lang.GetMessage("ClearData", this, player.UserIDString));
                    return;
                case "save":
                    SaveData();
                    SendReply(player, lang.GetMessage("SavedData", this, player.UserIDString));
                    return;
                default:
                    break;
            }
            SendReply(player, lang.GetMessage("SynError", this, player.UserIDString));
        }
		
        #endregion

        #region Config        
		
        private static ConfigData configData;
		
        private class ConfigData
        {
            public int RemoveHours { get; set; }  
            public int RecordsPerContainer { get; set; }             
        }
		
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
		
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                RemoveHours = 48,
                RecordsPerContainer = 10                
            };
            SaveConfig(config);
        }
		
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion

        #region Data Management        
		
        private class BoxData
        {
            public float x, y, z;
            public string killerId, killerName;
            public LootList lootList;
            public double lastAccess;

            public BoxData() { }
            public BoxData(BasePlayer player, double time, string date, Vector3 pos)
            {
                x = pos.x;
                y = pos.y;
                z = pos.z;
                lootList = new LootList(player, date);
                lastAccess = time;
            }
            public void AddLooter(BasePlayer looter, double time, string date)
            {
                lootList.AddEntry(looter, date);
                lastAccess = time;
            }

            public void OnDestroyed(BasePlayer killer)
            {
                killerId = killer.UserIDString;
                killerName = killer.displayName;
            }
            public Vector3 GetPosition() => new Vector3(x, y, z);            
        }        
		
        private class LootList
        {
            public List<LootEntry> looters;

            public LootList() { }
            public LootList(BasePlayer player, string date)
            {
                looters = new List<LootEntry>();
                looters.Add(new LootEntry(player, date));
            }
            public void AddEntry(BasePlayer player, string date)
            {
                LootEntry lastEntry = null;
                try { lastEntry = looters.Single(x => x.userId == player.UserIDString); } catch { }                 
                if (lastEntry != null)
                {
                    looters.Remove(lastEntry);
                    lastEntry.lastLoot = date;
                }
                else
                {
                    if (looters.Count == configData.RecordsPerContainer)
                        looters.Remove(looters.ElementAt(0));
                    lastEntry = new LootEntry(player, date);
                }
                looters.Add(lastEntry);
            }
            public LootEntry[] GetLooters() => looters.ToArray();

            public class LootEntry
            {
                public string userId, userName, firstLoot, lastLoot;
                            
                public LootEntry() { }
                public LootEntry(BasePlayer player, string firstLoot)
                {
                    userId = player.UserIDString;
                    userName = player.displayName;
                    this.firstLoot = firstLoot;
                    lastLoot = firstLoot;                    
                }
            }
        }
        
        private void SaveData()
        {            
			boxData.boxes = boxCache;
			bdata.WriteObject(boxData);            
        }
		
        private void LoadData()
        {            
            try
            {
                boxData = bdata.ReadObject<BoxDS>();
                boxCache = boxData.boxes;
            }
            catch
            {
                boxData = new BoxDS();
            }            
        }
		
        private class BoxDS
        {
            public Hash<uint, BoxData> boxes = new Hash<uint, BoxData>();
        }     
        
        #endregion

        #region Localization
		
        private static Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"BoxInfo", "Список тех кто лутал [<color=#F5D400>{0}</color>]:"},            
            {"DetectedLooters", "<color=#F5D400>[{0}]</color><color=#4F9BFF>{1}</color> ({2})\nF:<color=#F80>{3}</color> L:<color=#F80>{4}</color>\n"},
            {"DetectDestr", "Уничтожен игроком: <color=#4F9BFF>{0}</color> ID:{1}"},
            {"NoLooters", "<color=#4F9BFF>Контейнер [{0}] чист!</color>"},
            {"NoLootersPlayer", "<color=#4F9BFF>Игрок [{0}] чист!</color>"},
            {"Nothing", "<color=#4F9BFF>Не найден подходящий контейнер</color>"},
            {"NoID", "<color=#4F9BFF>Укажите корректный entity ID</color>"},            
            {"SynError", "<color=#F5D400>Ошибка синтаксиса: Напишите '/box' что бы увидеть все опции</color>" },
            {"SavedData", "Данные сохранены" },
            {"ClearData", "Данные очищены" }
        };
		
        #endregion
    }
}