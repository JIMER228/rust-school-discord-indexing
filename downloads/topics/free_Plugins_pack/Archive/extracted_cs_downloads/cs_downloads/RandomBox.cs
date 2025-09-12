using UnityEngine;
using System;
using Facepunch;
using Newtonsoft.Json;
using Object = System.Object;
using Oxide.Core;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using Random = Oxide.Core.Random;
using Rust;
using System.Text;
		   		 		  						  	   		  	  			  	   		  	  			  				
namespace Oxide.Plugins
{
    [Info("RandomBox", "Mercury", "1.0.2")]
    [Description("Random box in map")]
    class RandomBox : RustPlugin
    {
        private void OnNewSave(String filename) => ParseMonuments(true);
        
        private const Single dropHeight = 200f;

        
        
        [ChatCommand("tpbox")]
        private void AdminCommandTpBox(BasePlayer player)
        {
            if (!player.IsAdmin) return;
            if (randomBox == null) return;
            if (randomBox.IsDestroyed) return;
            
            player.Teleport(randomBox.transform.position);
        }
        private readonly HashSet<String> blockedNames = new HashSet<String> { "MeshColliderBatch", "iceberg_3", "iceberg_2" };
        
        
        
                private String GetLang(in String LangKey, in String userID = null, params Object[] args)
        {
            if (args == null) 
                return lang.GetMessage(LangKey, this, userID);

            StringBuilder sb = Pool.Get<StringBuilder>();

            try
            {
                sb.AppendFormat(lang.GetMessage(LangKey, this, userID), args);
                return sb.ToString();
            }
            finally
            {
                sb.Clear();
                Pool.FreeUnmanaged(ref sb);
            }
        }
        
        
                private static Configuration config = new Configuration();

                
                
        private Single GetGroundPosition(Vector3 pos)
        {
            Single y = TerrainMeta.HeightMap.GetHeight(pos);
            if (Physics.Raycast(
                    new Vector3(pos.x, pos.y + 200f, pos.z),
                    Vector3.down,
                    out RaycastHit hit,
                    Mathf.Infinity,
                    LayerMask.GetMask("Terrain", "World", "Default", "Construction", "Deployed")) && !hit.collider.name.Contains("rock_cliff"))
            {
                return Mathf.Max(hit.point.y, y);
            }

            return y;
        }
        private const Single monumentProximityThreshold = 250f;
        private const Single colliderRadius = 1f;

        
        
        private void ParseMonuments(Boolean isNewSave = false)
        {
            if(!isNewSave)
                if (monuments.Count != 0)
                    return;

            monuments.Clear();
            
            Int32 countAddedMonuments = 0;
            foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
            {
                if (monument.IsSafeZone ||
                    monument.Type == MonumentType.WaterWell ||
                    monument.Type == MonumentType.Cave ||
                    monument.Bounds.size == Vector3.zero)
                {
                    continue;
                } 
                
                Vector3 posMonument = monument.transform.position;

                monuments.Add(posMonument);
                countAddedMonuments++;
            }
            
            Puts(LanguageEn ? $"Information received about {countAddedMonuments} monuments on the map" : $"Получена информация о {countAddedMonuments} монументах на карте");
            WriteData();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning(LanguageEn 
                    ? "Error reading configuration #324552 'oxide/config/{Name}', creating a new configuration!!" 
                    : $"Ошибка чтения #324552 конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }
        
        private Vector3 GetSafeDropPosition(Vector3 position)
        {
            position.y += dropHeight;

            if (!Physics.Raycast(position, Vector3.down, out RaycastHit hit))
                return Vector3.zero;

            if (hit.collider == null || hit.collider.gameObject == null)
                return Vector3.zero;

            String colliderName = hit.collider.name;
            Int32 layer = hit.collider.gameObject.layer;

            if (BlockedLayers.Contains(layer) || blockedNames.Contains(colliderName) || colliderName.Contains("rock_cliff"))
                return Vector3.zero;

            Single groundHeight = TerrainMeta.HeightMap.GetHeight(position);
            position.y = Mathf.Max(hit.point.y, groundHeight);

            if (Physics.OverlapSphere(position, colliderRadius, blockedMask, QueryTriggerInteraction.Collide).Length == 0)
                return position;

            return Vector3.zero; 
        }
        /// <summary>
        /// - Исправление после обновления и гры
        /// </summary>
        
        
        private const Boolean LanguageEn = false;
        
        private Boolean isInit = false;
        
        private List<Vector3> monuments = new List<Vector3>();
        private void WriteData() =>  Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("RandomBox", monuments);
        private class Configuration
        {
            [JsonProperty(LanguageEn ? "Loot Configuration" : "Настройка лута")]
            public LootSetting controllerLott = new();
            [JsonProperty(LanguageEn ? "IQChat: Chat Prefix" : "IQChat : Префикс в чате")]
            public String chatPrefix;
            [JsonProperty(LanguageEn ? "Box SkinID (Be Sure to Specify ID)" : "SkinID ящика (обязательно укажите ID)")]
            public UInt64 skinIdBox;
            [JsonProperty(LanguageEn ? "Use Red alarm" : "Использовать красную мигалку")]
            public Boolean useAlarm;
            [JsonProperty(LanguageEn ? "Time Interval for Box Appearance (Seconds)" : "Раз во сколько будет появляться ящик (секунды)")]
            public Int32 secondSpawn;

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    secondSpawn = 30,
                    useAlarm = false,
                    skinIdBox = 824194136,
                    chatAvatar = "0",
                    chatPrefix = "<color=#CD412B>[RandomBox]</color>\n",
                    controllerLott = new LootSetting
                    {
                        countLootInBox = new LootSetting.AmountController
                        {
                            minAmount = 3,
                            maxAmount = 6
                        },
                        itemForBox = new List<LootSetting.LootBox>
                        {
                            new LootSetting.LootBox
                            {
                                shortname = "wood",
                                skinID = 0,
                                amountLoot = new LootSetting.AmountController()
                                {
                                    minAmount = 300,
                                    maxAmount = 1000
                                }
                            },
                            new LootSetting.LootBox
                            {
                                shortname = "metal.fragments",
                                skinID = 0,
                                amountLoot = new LootSetting.AmountController()
                                {
                                    minAmount = 150,
                                    maxAmount = 500
                                }
                            },
                            new LootSetting.LootBox
                            {
                                shortname = "scrap",
                                skinID = 0,
                                amountLoot = new LootSetting.AmountController()
                                {
                                    minAmount = 30,
                                    maxAmount = 100
                                }
                            },
                            new LootSetting.LootBox
                            {
                                shortname = "xmas.present.medium",
                                skinID = 0,
                                amountLoot = new LootSetting.AmountController()
                                {
                                    minAmount = 1,
                                    maxAmount = 1
                                }
                            },
                            new LootSetting.LootBox
                            {
                                shortname = "xmas.present.large",
                                skinID = 0,
                                amountLoot = new LootSetting.AmountController()
                                {
                                    minAmount = 1,
                                    maxAmount = 1
                                }
                            },
                        }
                    }
                };
            }
            
            public class LootSetting
            {
                [JsonProperty(LanguageEn ? "List of Items (Selected Randomly)" : "Список предметов (выбираются случайно)")]
                public List<LootBox> itemForBox = new();
		   		 		  						  	   		  	  			  	   		  	  			  				
                internal class LootBox
                {
                    [JsonProperty("SkinID")] 
                    public UInt64 skinID;
                    [JsonProperty(LanguageEn ? "Loot Quantity Configuration" : "Настройка количества лута")] 
                    public AmountController amountLoot;
                    
                    public Item GetItem()
                    {
                        Item item = ItemManager.CreateByName(shortname, amountLoot.GetAmount(), skinID);
                        return item;
                    }
                    [JsonProperty("Shortname")] 
                    public String shortname;
                }

                private void ShuffleList(List<LootBox> list)
                {
                    Int32 n = list.Count;
                    for (Int32 i = 0; i < n - 1; i++)
                    {
                        Int32 j = Oxide.Core.Random.Range(i, n);
                        (list[i], list[j]) = (list[j], list[i]);
                    }
                }

                public class AmountController
                {
                    [JsonProperty(LanguageEn ? "Minimum Quantity" : "Минимальное количество")]
                    public Int32 minAmount;
                    [JsonProperty(LanguageEn ? "Maximum Quantity" : "Максимальное количество")]
                    public Int32 maxAmount;

                    public Int32 GetAmount() => Oxide.Core.Random.Range(minAmount, maxAmount);
                }
                
                public List<Item> GetRandomItems()
                {
                    List<Item> resultItems = new List<Item>();
		   		 		  						  	   		  	  			  	   		  	  			  				
                    Int32 countLoot = countLootInBox.GetAmount();

                    if (itemForBox.Count < countLoot)
                        countLoot = itemForBox.Count;

                    ShuffleList(itemForBox);

                    for (Int32 i = 0; i < countLoot; i++)
                    {
                        LootBox lootBox = itemForBox[i];
                        Item item = lootBox.GetItem();
        
                        resultItems.Add(item);
                    }

                    return resultItems;
                }
                [JsonProperty(LanguageEn ? "Loot Quantity Configuration in a Box" : "Настройка количество лута в одном ящике")] 
                public AmountController countLootInBox;
            }
            [JsonProperty(LanguageEn ? "IQChat: Chat Avatar (Use Steam64ID)" : "IQChat : Аватарка в чате (Используйте Steam64ID)")]
            public String chatAvatar;
        }
        
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<String, String>
            {
                ["CREATE_SPAWN"] = "A <color=#738D45>gift box</color> has appeared!\nHurry up and find it!\nLocation: <color=#738D45>{0}</color>",
                ["CREATE_FINDING"] = "Someone found the gifts!",
            }, this);

            lang.RegisterMessages(new Dictionary<String, String>
            {
                ["CREATE_SPAWN"] = "Появилась <color=#738D45>коробка с подарками</color>!\nСкорее найди ее!\nМестоположение: <color=#738D45>{0}</color>",
                ["CREATE_FINDING"] = "Кто-то нашел подарки!",
            }, this, "ru");
            
            PrintWarning(LanguageEn 
                ? "Language file loaded successfully." 
                : "Языковой файл загружен успешно");
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        
        
        private void ReadData() => monuments = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<List<Vector3>>("RandomBox");
                
                
        [PluginReference] Plugin IQChat;

        private Vector3 GetEventPosition()
        {
            for (Int32 i = 0; i < maxRetries; i++)
            {
                Vector3 eventPos = GetSafeDropPosition(RandomDropPosition());

                Boolean tooCloseToMonument = false;
                foreach (Vector3 monument in monuments)
                    if (Vector3.Distance(eventPos, monument) < monumentProximityThreshold)
                    {
                        tooCloseToMonument = true;
                        break;
                    }

                if (tooCloseToMonument)
                    continue;

                eventPos.y = GetGroundPosition(eventPos);
                if (eventPos.y >= 0)
                    return eventPos; // Valid event position
            }

            return Vector3.zero;
        }
		   		 		  						  	   		  	  			  	   		  	  			  				
        private const String prefabAlarm = "assets/prefabs/deployable/playerioents/lights/sirenlight/electric.sirenlight.deployed.prefab";
        private Timer timerStart = null;
        private const String effectBox = "assets/prefabs/misc/casino/slotmachine/effects/payout_jackpot.prefab";
        private StorageContainer randomBox = null;
        
        private void OnServerInitialized()
        {
            if (config.skinIdBox == 0)
            {
                PrintWarning(LanguageEn 
                    ? "You have not set the SkinID - this is a mandatory field." 
                    : "У вас не установлен SkinID - это обязательный пункт");
                NextTick(() =>
                {
                    Interface.Oxide.UnloadPlugin(Name);
                });
            }
            
            ParseMonuments();
            
            StartEvent();
            isInit = true;
        }
        private const String prefabNameBox = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";
        private void SendChat(String Message, BasePlayer player)
        {
            if (IQChat)
                 IQChat?.Call("API_ALERT_PLAYER", player, Message, config.chatPrefix, config.chatAvatar);
            else player.SendConsoleCommand("chat.add", ConVar.Chat.ChatChannel.Global, 0, Message);
        }
        
        
        
        
        private void StartEvent()
        {
            if (!isInit)
            {
                timerStart = timer.Once(config.secondSpawn, StartEvent);
                return;
            }
            
            if (timerStart is { Destroyed: false })
            {
                timerStart.Destroy();
                timerStart = null;
            }
            
            Vector3 positionEvent = GetEventPosition();
            if (positionEvent == Vector3.zero)
            {
                PrintError(LanguageEn 
                    ? "Failed to start the event, could not detect positions." 
                    : "Не удалось запустить мероприятие, не смогли обнаружить позиции");

                return;
            }

            String gMapKey = MapHelper.PositionToString(positionEvent);

            if (randomBox != null)
            {
                randomBox.inventory.Clear();
                randomBox.Kill();
                randomBox = null;
            }
            
            randomBox = (StorageContainer)GameManager.server.CreateEntity(prefabNameBox, positionEvent) as StorageContainer;
            randomBox.enableSaving = true;
            randomBox.skinID = config.skinIdBox;
            randomBox.Spawn();

            if (config.useAlarm)
            {
                BaseEntity AlarmEntity = GameManager.server.CreateEntity(prefabAlarm, randomBox.transform.position);
                AlarmEntity.Spawn();
                AlarmEntity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                AlarmEntity.UpdateNetworkGroup();
                AlarmEntity.transform.localPosition = new Vector3(0, 0.4f, 0f);
                AlarmEntity.SetParent(randomBox);
                AlarmEntity.SetFlag(BaseEntity.Flags.Reserved8, true);
                AlarmEntity.SetFlag(BaseEntity.Flags.On, true);
            }

            List<Item> itemList = Pool.GetList<Item>();
            itemList = config.controllerLott.GetRandomItems();

            foreach (Item item in itemList)
                item.MoveToContainer(randomBox.inventory);
            
            Pool.FreeList(ref itemList);
            
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                SendChat(GetLang("CREATE_SPAWN", player.UserIDString, gMapKey), player);

            timerStart = timer.Once(config.secondSpawn, StartEvent);
        }
        private readonly Int32 blockedMask = LayerMask.GetMask(new[] { "Player (Server)", "Trigger", "Prevent Building" });
        
        private SpawnFilter filter = new SpawnFilter();
        
        private const Int32 maxRetries = 100;
        
        private Object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (container == null || player == null) return null;
            if (container.skinID != config.skinIdBox) return null;
            
            for (Int32 i = 0; i < container.inventory.itemList.Count; i++)
            {
                Item itemInBox = container.inventory.itemList[i];
                Single randomForce = Random.Range(1.5f, 3.0f);
                itemInBox.DropAndTossUpwards(container.transform.position, randomForce);
            }

            Effect effect = new Effect(effectBox, player, 0, new Vector3(), new Vector3());
            EffectNetwork.Send(effect, player.Connection);
            
            container.Kill();
            
            foreach (BasePlayer p in BasePlayer.activePlayerList)
                SendChat(String.Format(lang.GetMessage("CREATE_FINDING", this, p.UserIDString)), p);

            randomBox = null;

            return false;
        }

        
        
        private void Init() => ReadData();

        private Vector3 RandomDropPosition()
        {
            Vector3 vector = Vector3.zero;
            Single num = 1000f;
            Single x = TerrainMeta.Size.x / 3;

            do
            { vector = Vector3Ex.Range(-x, x); }
            while (filter.GetFactor(vector) == 0f && (num -= 1f) > 0f);

            Single height = TerrainMeta.HeightMap.GetHeight(vector);
            vector.y = height;
            return vector;
        }
        private readonly List<Int32> BlockedLayers = new List<Int32> { (Int32)Layer.Water, (Int32)Layer.Construction, (Int32)Layer.Trigger, (Int32)Layer.Prevent_Building, (Int32)Layer.Deployed, (Int32)Layer.Tree };

        private void Unload()
        {
            if (timerStart is { Destroyed: false })
            {
                timerStart.Destroy();
                timerStart = null;
            }
            
            if (randomBox != null)
            {
                randomBox.inventory.Clear();
                randomBox.Kill();
                randomBox = null;
            }
            
            WriteData();
        }
        protected override void SaveConfig() => Config.WriteObject(config);
            }
}
