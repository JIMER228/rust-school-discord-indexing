using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins;
using UnityEngine;
using UnityEngine.XR;

namespace Oxide.Plugins
{
    [Info("Quest", "Hougan", "1.0.0")]
    public class Quest : RustPlugin
    {
        #region Classes

        private class DResult
        {
            [JsonProperty("code")]
            public int Code;
            [JsonProperty("result")] 
            public object Result;

            public static DResult GenerateFromAnswer(string input, string id, string type)
            {
                DResult obj = null;
                try
                {
                    obj = JsonConvert.DeserializeObject<DResult>(input);
                }
                catch(Exception exception)
                {
                    _.LogToFile("QuestErrors", input, _); 
                    Interface.Oxide.LogWarning($"WEB> Critical error in getting result from server [{id}][{type}]");
                }

                return obj; 
            }
        }

        private class DPart 
        {
            [JsonProperty("id")] 
            public int Id;
            [JsonProperty("name")]
            public string Name; 

            [JsonProperty("url")]
            public string Url;
            [JsonProperty("smallUrl")]
            public string SmallUrl;
            [JsonProperty("amount")]
            public int Amount;

            [JsonProperty("command")]
            public string Command;
            [JsonProperty("item")] 
            public string Item;
  
            public string GetImage() => (string) _.ImageLibrary.Call("GetImage", $"PartsID.{Id}.Avatar");
            public string GetSmallImage() => (string) _.ImageLibrary.Call("GetImage", $"PartsID.{Id}.SmallAvatar");

            public static void LoadParts()
            {
                _.webrequest.Enqueue($"{Settings.WebURL}/api/parts/fetch?pass=ABCBLOO1ss1009g", "", (i, s) =>
                { 
                    DResult result = DResult.GenerateFromAnswer(s, "0", "parts");
                    if (result == null)
                    {
                        return;
                    }
                    
                    HashSet<DPart> info = JsonConvert.DeserializeObject<HashSet<DPart>>(result.Result.ToString());
                    Parts = info;

                    if (!_.ImageLibrary) 
                    {  
                        _.PrintError($"C#> Failed to load images [ERROR]"); 
                        return; 
                    }
                    _.PrintWarning($"WEB> Loaded {info.Count} parts [OK]");
                    
                    foreach (var part in info)
                    { 
                        _.ImageLibrary.Call("AddImage", part.Url, $"PartsID.{part.Id}.Avatar");
                        _.ImageLibrary.Call("AddImage", part.SmallUrl, $"PartsID.{part.Id}.SmallAvatar");
                    }
                }, _);
            }
        }
        
        private class DPlayer
        {
            [JsonProperty("id")]
            public int Id;
            [JsonProperty("displayName")]
            public string DisplayName;
            [JsonProperty("userId")] 
            public ulong UserID;

            [JsonProperty("exp")]
            public float Exp;
            [JsonProperty("level")]  
            public float Level;
            [JsonProperty("free")]
            public bool Free;

            [JsonProperty("players@items")]
            public List<DItemCache> InventoryCaches = new List<DItemCache>();
            [JsonIgnore] 
            public HashSet<DItem> Inventory = new HashSet<DItem>();
            
            [JsonProperty("players@blockeditems")]
            public List<DItem> Blocked = new List<DItem>();
            [JsonProperty("murders")]
            public List<DMurder> Murders = new List<DMurder>(); 
            [JsonProperty("quest")]
            public DQuest Quest = new DQuest();
            
            public void DebugInformation()
            {
                Interface.Oxide.LogWarning($"ID: {Id}");
                Interface.Oxide.LogWarning($"Name: {DisplayName}");
                Interface.Oxide.LogWarning($"UserID: {UserID}"); 
                Interface.Oxide.LogWarning($"");
                Interface.Oxide.LogWarning($"EXP: {Exp}");
                Interface.Oxide.LogWarning($"Level: {Level}");
                Interface.Oxide.LogWarning($"");
                Interface.Oxide.LogWarning($"Inventory count: {Inventory.Count}");
                Interface.Oxide.LogWarning($"Murders count: {Murders.Count}");
                Interface.Oxide.LogWarning($"Active quest: {Quest != null}");
            }

            public void RefreshIDs()
            {
                foreach (var check in Inventory)
                {
                    check.OutID = CuiHelper.GetGuid();
                }
            }

            public void FinishQuest(Action<bool> callback, bool manual = false)
            {
                if (Quest == null || !Quest.IsEnd()) return;
                
                _.webrequest.Enqueue($"{Settings.WebURL}/api/players/finishQuest/{UserID}/{manual}?pass=ABCBLOO1ss1009g", "", (i, s) =>
                {
                    UpdateAfterUpdate(s);
                    
                    callback(true);
                }, _);
            }

            public void GenerateQuest(DMurder murder, Action<bool> callback)
            {
                if (Quest != null)
                {  
                    callback(false); 
                    return;  
                }  
 
                var player = BasePlayer.Find(UserID.ToString());

                float length = Settings.TaskPrice[murder.Level].Length;
                if (murder.Level == 4)
                {
                    length = Mathf.FloorToInt(Oxide.Core.Random.Range(600, 7200));
                }				
                
                _.webrequest.Enqueue($"{Settings.WebURL}/api/players/setQuest/{UserID}/{murder.Level}/{length}?pass=ABCBLOO1ss1009g", "", (i, s) =>
                {
                    UpdateAfterUpdate(s);   
                    player.GetComponent<WPlayer>().SendExpUpdate(); 
                    
                    callback(true);
                }, _);
            }
            public static void GetFromPlayer(BasePlayer player, Action<DPlayer> callback)
            {
                GetFromPlayer(player.displayName, player.userID, callback);
            }
            public static void GetFromPlayer(string displayName, ulong userId, Action<DPlayer> callback)
            {
                _.webrequest.Enqueue($"{Settings.WebURL}/api/players/fetch/{userId}/FIX?pass=ABCBLOO1ss1009g", "", (i, s) =>
                {
                    try
                    {
                        var result = DResult.GenerateFromAnswer(s, userId.ToString(), "fetch player");
                        if (result == null)
                        {
                            return; 
                        } 

                        var info = DPlayer.GenerateFromAnswer(result.Result as JObject);
                        if (info == null)  
                        {
                            return;  
                        } 
                     
                        callback(info);
                    }
                    catch
                    { 
                    
                    }
                }, _);    
            }

            public void UpdateAfterUpdate(string s) 
            {
                var result = DResult.GenerateFromAnswer(s, UserID.ToString(), "update after update");
                if (result == null)
                {
                    return;
                }

                var info = DPlayer.GenerateFromAnswer(result.Result as JObject);
                if (info == null) 
                {
                    return; 
                }
                
                _.UpdatePlayer(BasePlayer.Find(UserID.ToString()), info);  
            }
            public static DPlayer GenerateFromAnswer(JObject obj)
            {
                DPlayer result = null;
                
                try
                {
                    result = JsonConvert.DeserializeObject<DPlayer>(obj.ToString());
                    result.Murders.ForEach(p => p.LoadImage()); 
                }
                catch
                {
                }

                return result;  
            }
        }

        private class WPlayer : MonoBehaviour
        {
            public BasePlayer Player;

            public float Exp;
            public int PlayTime;
            public Vector3 LastPosition;
            
            public HashSet<ulong> KillTarget = new HashSet<ulong>();
            
            public void Awake()
            {
                Player = GetComponent<BasePlayer>();
                LastPosition = Player.transform.position;
                
                InvokeRepeating(nameof(ScanPosition), 0f, 5f);
                InvokeRepeating(nameof(ClearTargetList), 0f, 1200f);
            }

            public void ClearTargetList()
            {
                KillTarget.Clear();
            }

            public void ScanPosition()
            {
                if (!Player.IsConnected)
                {
                    Destroy(this);
                    return;
                }
                
                if (Vector3.Distance(Player.transform.position, LastPosition) < 0.5f) return;
                LastPosition = Player.transform.position;

                PlayTime += 5;
                if (PlayTime == 60)
                {
                    PlayTime = 0;
                    
                    List<string> names = new List<string> { "bloodrust", "blood-rust", "blood rust" };
                    if (names.Any(p => Player.displayName.ToLower().Contains(p.ToLower())))
                    {
                        AddExp(Settings.ExpForMinute * 2f, "За одну минуту игры на сервере"); 
                    }
                    else
                    {
                        AddExp(Settings.ExpForMinute, "За одну минуту игры на сервере"); 
                    }
                    
                }
            }

            public bool AddExp(float amount, string mess, ulong playerId = 0)
            {
                if (IsInvoking(nameof(SendExpUpdate)))
                    CancelInvoke(nameof(SendExpUpdate));

                if (playerId != 0)
                {
                    if (KillTarget.Contains(playerId))
                    {
                        //Player.ChatMessage("Вы его уже убивали!");
                        return false;
                    }

                    KillTarget.Add(playerId);
                }
                
                Exp += amount;
                if (Settings.DebugErrors)
                    Player.ChatMessage($"Add {amount}exp for {mess}");
                
                Invoke(nameof(SendExpUpdate), 5f);
                return true;
            }

            public void SendExpUpdate() 
            {
                try
                {
                    _.webrequest.Enqueue($"{Settings.WebURL}/api/players/addExp/{Player.userID}/{Exp}?pass=ABCBLOO1ss1009g", "", (i, s) =>
                    {
                        try
                        {
                            float value = 0;
                            if (Player == null || !float.TryParse(s, out value))
                            {
                                return;
                            }
                            Exp = 0;
                     
                            Base[Player].Exp = value; 
                            UpdateExpCounter(Player, ExpAction.Minus, Base[Player].Exp);
                        }
                        catch
                        {
                        }
                    }, _);
                }
                catch
                {
                    
                }
            }
        }
        
        private class DQuest
        {
            [JsonProperty("id")]
            public int Id; 
            [JsonProperty("timeStart")]
            public double TimeStart;
            [JsonProperty("timeEnd")]
            public double TimeEnd; 

            [JsonProperty("items")]
            public List<DItem> Items = new List<DItem>();
            [JsonProperty("murder")] 
            public DMurder Murder = new DMurder(); 

            public bool IsEnd() => Time() > TimeEnd  / 1000;
            public double TimeLeft() => IsEnd() ? 0 : TimeSpan.FromSeconds(TimeEnd / 1000 - Time()).TotalSeconds;

            public string TimeLeftText()
            {
                if (IsEnd())
                {
                    return "КОНЕЦ";
                } 

                string text = "<b>";
                var timeSpan = TimeSpan.FromSeconds(TimeEnd / 1000 - Time());
 
                if (timeSpan.Hours > 0) return $"<b>{timeSpan.Hours} ЧАС</b>";
                if (timeSpan.Minutes > 0) return $"<b>{timeSpan.Minutes} МИН</b>";
                if (timeSpan.Seconds > 0) return $"<b>{timeSpan.Seconds} СЕК</b>";

                return "<b>0 СЕК</b>";
            }
        }

        private class DMurder
        {
            [JsonProperty("id")]
            public int Id;
            [JsonProperty("name")]
            public string Name;
            [JsonProperty("url")]
            public string Url;
            [JsonProperty("level")]
            public int Level;

            [JsonIgnore] public string ImageID;
            
            public void LoadImage()
            {
                ImageID = (string) _.ImageLibrary.Call("GetImage", $"MurderID.{Id}.Avatar");
            }

            public static void LoadImages()
            {
                _.webrequest.Enqueue($"{Settings.WebURL}/api/murders/fetch?pass=ABCBLOO1ss1009g", "", (i, s) =>
                {
                    DResult result = DResult.GenerateFromAnswer(s, "UNKNOWN", "murders fetch");
                    if (result == null)
                    {
                        return;
                    }

                    var info = JsonConvert.DeserializeObject<List<DMurder>>(result.Result.ToString());

                    if (!_.ImageLibrary) 
                    { 
                        _.PrintError($"C#> Failed to load images [ERROR]"); 
                        return; 
                    }
                    _.PrintWarning($"WEB> Loaded {info.Count} murders [OK]");
                    
                    foreach (var murder in info)
                    {
                        _.ImageLibrary.Call("AddImage", murder.Url, $"MurderID.{murder.Id}.Avatar");
                    }
                }, _);
            }
        }

        private class DItemCache
        {
            public int      id        { get; set; }
            public DateTime createdAt { get; set; }
            public DateTime updatedAt { get; set; }
            public int      playerId  { get; set; }
            public int      itemId    { get; set; }
        }

        private class DItem
        {
            [JsonProperty("id")]
            public int Id;
            [JsonProperty("shortName")] 
            public string ShortName;
            [JsonProperty("skinId")] 
            public ulong SkinID; 
            [JsonProperty("amount")]
            public int Amount;
            [JsonProperty("exp")] 
            public float Exp; 
            [JsonProperty("expBuy")]
            public float ExpBuy;
            [JsonProperty("levelQuest")]
            public int LevelQuest; 
            [JsonProperty("category")]
            public string Category; 
            [JsonProperty("isPart")] 
            public string IsPart;
            [JsonProperty("isBp")]  
            public bool IsBlueprint;
            [JsonProperty("url")] 
            public string Url;
            [JsonProperty("shouldBlock")] 
            public bool ShouldBlock;
 
            [JsonIgnore] public string OutID = "UNKNOWN";

            public static DItem FromCache(DItemCache cache)
            {
                var rItem = Market.FirstOrDefault(p => p.Id == cache.itemId);
                if (rItem == null)
                {
                    _.PrintError($"Unknown item {cache.itemId} -> {cache.playerId} [{cache.id}]!");
                    return Market.FirstOrDefault();
                }
                return (DItem) rItem.Clone(); 
            } 
            
            public string GetImage()
            {
                if (ShortName.StartsWith("command: "))
                { 
                    return (string) _.ImageLibrary.Call("GetImage", "M.Custom." + Id);
                }
                else if (IsPart.Length < 2)
                {
                    return (string) _.ImageLibrary.Call("GetImage", ShortName);
                }
                else
                {
                    return (string) Parts.FirstOrDefault(p => p.Name == IsPart).GetSmallImage();
                }
            }

            /// <summary>
            /// Отображает процесс произведения процесса (что!?)
            /// </summary>
            private void ShowWaitingResult(BasePlayer player, bool success)
            {
                CuiElementContainer container = new CuiElementContainer();
                CuiHelper.DestroyUi(player, InitialLayer + $".HRPStore{OutID}.Wait");
                                
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "15 15", OffsetMax = "-15 -15"},
                    Button = {Color = "1 1 1 0.5", Sprite = success ? "assets/icons/vote_up.png" : "assets/icons/vote_up.png" },
                    Text = {Text = ""}
                }, InitialLayer + $".HRPStore{OutID}", InitialLayer + $".HRPStore{OutID}.Wait"); 
                                
                CuiHelper.AddUi(player, container); 
            }
            
            /// <summary>
            /// Отображает процесс произведения процесса (что!?)
            /// </summary>
            private void WaitForResponseOnItem(BasePlayer player)
            {
                CuiElementContainer container = new CuiElementContainer();
                CuiHelper.DestroyUi(player, InitialLayer + $".HRPStore{OutID}.Overflow");
                
                container.Add(new CuiElement 
                {
                    Parent = InitialLayer + $".HRPStore{OutID}", 
                    Components =
                    {
                        new CuiRawImageComponent { Png = WhiteLockerImage, Color = "1 1 1 0.3"},
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "15 15", OffsetMax = "-15 -15"},
                    Button = {Color = "1 1 1 0.5", Sprite = "assets/icons/refresh.png"},
                    Text = {Text = ""}
                }, InitialLayer + $".HRPStore{OutID}", InitialLayer + $".HRPStore{OutID}.Wait"); 
                                
                CuiHelper.AddUi(player, container); 
            }
 
            public void Buy(BasePlayer player, DPlayer dPlayer)
            { 
                _.webrequest.Enqueue($"{Settings.WebURL}/api/players/buyItem/{dPlayer.UserID}/{Id}?pass=ABCBLOO1ss1009g", "",  (i, s) =>
                {
                    DResult result = DResult.GenerateFromAnswer(s, dPlayer.UserID.ToString(), "but item");
                    if (result == null)
                    {
                        return;
                    }
                    if (result.Result is Boolean) 
                    { 
                        dPlayer.Exp -= ExpBuy; 

                        UpdateExpCounter(player, ExpAction.Minus, dPlayer.Exp);
                        
                        dPlayer.Inventory.Add((DItem) Clone());
                        if (ShouldBlock)
                        {
                            dPlayer.Blocked.Add((DItem) Clone());
                        } 
                        _.InitializeShop(player, dPlayer); 
                        
                        Effect effect = new Effect("assets/prefabs/deployable/vendingmachine/effects/vending-machine-purchase-human.prefab", player, 0, new Vector3(), new Vector3());
                        EffectNetwork.Send(effect, player.Connection);
                    } 
                    else
                    {
                        //LogError($"C#> Не удалось купить {ShortName} [{Id}] игрком {dPlayer.UserID}. Причина: '{result.Result}'"); 
                    }
                }, _);
            }
            
            public void RefundItem(BasePlayer player, DPlayer dPlayer)
            { 
                WaitForResponseOnItem(player); 
                
                _.webrequest.Enqueue($"{Settings.WebURL}/api/players/removeItem/{dPlayer.UserID}/{Id}/{Exp}?pass=ABCBLOO1ss1009g", "",  (i, s) =>
                { 
                    DResult result = DResult.GenerateFromAnswer(s, dPlayer.UserID.ToString(), "refund item");
                    if (result == null)
                    {
                        return;
                    }
                    if (result.Result is Boolean)
                    {
                        dPlayer.Exp += Exp;
                        
                        ShowWaitingResult(player, (bool) result.Result);
                        UpdateExpCounter(player, ExpAction.Plus, dPlayer.Exp);
                        
                        dPlayer.Inventory.Remove(this);
                    }
                    else
                    {
                        LogError($"C#> Не удалось вернуть {ShortName} [{Id}] игрком {dPlayer.UserID}. Причина: '{result.Result}'"); 
                    }
                }, _);
            }
            
            public void ProcessItem(BasePlayer player, DPlayer dPlayer) 
            {
                WaitForResponseOnItem(player); 
                
                dPlayer.Inventory.Remove(this);
				_.webrequest.Enqueue($"{Settings.WebURL}/api/players/removeItem/{dPlayer.UserID}/{Id}/false?pass=ABCBLOO1ss1009g", "",  (i, s) =>
                {
                    DResult result = DResult.GenerateFromAnswer(s, dPlayer.UserID.ToString(), "process item");
                    if (result == null)
                    {
                        return;
                    }
                    if (result.Result is Boolean)
                    {
                        ShowWaitingResult(player, (bool) result.Result);
                        
                        Item item = null; 
    
                        Effect effect = new Effect("assets/bundled/prefabs/fx/notice/stack.world.fx.prefab", player, 0, new Vector3(), new Vector3());
                        EffectNetwork.Send(effect, player.Connection);
                        if (ShortName != "") 
                        {
                            if (ShortName.StartsWith("command: ")) 
                            {
                                string cmd = ShortName.Replace("command: ", "").Replace("%STEAMID%", player.UserIDString);
                                _.Server.Command(cmd);

                                if (cmd.StartsWith("grant"))
                                {
                                    _.webrequest.Enqueue($"http://api2.hougan.space/blood/rcon/{cmd}/2281337ABC/all?pass=ABCBLOO1ss1009g", "",
                                        (i1, s1) => { }, _);  
                                }
                            }
                            else
                            {
                                if (IsBlueprint)
                                {
                                    item = ItemManager.CreateByItemID(-996920608, 1, 0);
                                    item.blueprintTarget = ItemManager.FindItemDefinition(ShortName).itemid;
                                }
                                else 
                                {
                                    item = ItemManager.CreateByName(ShortName, Amount, SkinID);
                                }
                                if (!item.MoveToContainer(player.inventory.containerMain)) 
                                    item.Drop(player.transform.position, Vector3.zero);
                            
                                player.SendConsoleCommand($"note.inv {item.info.itemid} {item.amount}");
                            } 
                        }
                        
                    }
                    else
                    {
                        LogError($"C#> Не удалось получить {ShortName} [{Id}] игрком {dPlayer.UserID}. Причина: '{result.Result}'"); 
                    }
                }, _);
            }

            public object Clone()
            {
                return new DItem
                {
                    Amount = Amount,
                    Category = Category,
                    Exp = Exp,    
                    Id = Id,
                    ExpBuy = ExpBuy,
                    IsPart = IsPart,
                    IsBlueprint = IsBlueprint,
                    LevelQuest = LevelQuest,
                    ShortName = ShortName, 
                    OutID = CuiHelper.GetGuid(),
                    SkinID = SkinID
                };
            }
        }

        public class LevelSettings
        {
            [JsonProperty("Стоимость наёмника в EXP")]
            public int ExpCost;
            [JsonProperty("Длительность поиска (сек.)")]
            public int Length;

            [JsonProperty("Минимальное количество предметов")]
            public int MinimalItems;
            [JsonProperty("Максимальное количество предметов")]
            public int MaximumItems;
        } 

        private class Configuration
        {
            public bool DebugErrors = true;  
            public string WebURL = "http://mercenaries.bloodrust.ru/";
            
            [JsonProperty("Настройки уровней для поиска наёмников")]
            public Dictionary<int, LevelSettings> TaskPrice = new Dictionary<int, LevelSettings>();
            
            [JsonProperty("Получение EXP за добычу ресурсов")]
            public Hash<string, float> ResourceExp = new Hash<string, float>();
            [JsonProperty("Получение EXP за посадку ресурсов")]
            public Hash<string, float> PlantExp = new Hash<string, float>();
            [JsonProperty("Получение EXP за убийство (разрушения)")]
            public Hash<string, float> KillExp = new Hash<string, float>(); 
            [JsonProperty("Получение EXP за открытие ящиков")]
            public Hash<string, float> LootExp = new Hash<string, float>();
            [JsonProperty("Получение EXP за постройки")]
            public Hash<string, float> BuildExp = new Hash<string, float>();

            [JsonProperty("EXP за минуту игры на сервере")]
            public float ExpForMinute = 0.5f;

            public static Configuration Generate()
            {
                return new Configuration
                {
                    DebugErrors = true,
                    
                    ResourceExp = new Hash<string, float>
                    {
                        ["sulfur-ore"] = 0.05f,
                        ["tree"] = 10f
                    },
                    PlantExp = new Hash<string, float>
                    {
                         
                    },
                    BuildExp = new Hash<string, float>
                    {
                        ["tier1"] = 1f,
                        ["tier2"] = 1f,
                        ["tier3"] = 1f,
                        ["tier4"] = 1f, 
                    },
                    KillExp = new Hash<string, float>
                    {
                        ["player"] = 1f,
                        ["tier1"] = 1f,
                        ["tier2"] = 1f,
                        ["tier3"] = 1f,
                        ["tier4"] = 1f, 
                    },
                    LootExp = new Hash<string, float>
                    {
                        
                    },
                    ExpForMinute = 0.5f,
                    
                    TaskPrice = new Dictionary<int, LevelSettings> 
                    {
                        [1] = new LevelSettings
                        { 
                            ExpCost = 10,
                            Length = 1000, 
                            
                            MinimalItems = 1,  
                            MaximumItems = 5 
                        },
                        [2] = new LevelSettings
                        {
                            ExpCost = 20,
                            Length = 1, 
                            
                            MinimalItems = 1,
                            MaximumItems = 5 
                        },
                        [3] = new LevelSettings
                        {
                            ExpCost = 30,
                            Length = 1,  
                            
                            MinimalItems = 1,
                            MaximumItems = 5
                        },
                        [4] = new LevelSettings
                        {
                            ExpCost = 40,
                            Length = 1,
                             
                            MinimalItems = 1,
                            MaximumItems = 5
                        } 
                    } 
                };
            }
        }

        #endregion

        #region Variables

        private static bool CheckEngine = false;
        private static Hash<ulong, float> CheckData = new Hash<ulong, float>();

        private static bool Initialized = false;
        private static Quest _;

        private enum ExpAction
        {
            None,
            Plus,
            Minus
        }  

        [PluginReference] 
        private Plugin ImageLibrary;

        private static string BlueprintImage;
        private static string LockerImage; 
        private static string WhiteLockerImage; 
        private static string HandlerCommand = "UI_Quest_FuryController";
        
        private static HashSet<ulong> Joined = new HashSet<ulong>();
        private static HashSet<DPart> Parts = new HashSet<DPart>();
        private static HashSet<DItem> Market = new HashSet<DItem>();
        private static Configuration Settings;
        private static Hash<BasePlayer, DPlayer> Base = new Hash<BasePlayer, DPlayer>();
        private static Hash<BasePlayer, WPlayer> Workers = new Hash<BasePlayer, WPlayer>();
        private static Hash<BaseEntity, ulong> Tankers = new Hash<BaseEntity, ulong>();
        private static Hash<ulong, double> SyncCooldowns = new Hash<ulong, double>();
        
        private static Dictionary<string, string> Sections = new Dictionary<string, string>
        {
            ["FURY"] = "НАЁМНИКИ",
            ["STORE"] = "ЯЩИК НАЁМНИКОВ",
            ["SHOP"] = "РЫНОК",
            ["PARTS"] = "СБОР ФРАГМЕНТОВ",
        };

        #endregion

        #region Hooks
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                Settings = Config.ReadObject<Configuration>();
            }
            catch
            {
                PrintWarning($"Error reading config, creating one new config!");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => Settings = Configuration.Generate();
        protected override void SaveConfig()        => Config.WriteObject(Settings);

        private void FetchBaseSync(Action callback)
        {
            DPlayer.GetFromPlayer("TEST_BORDER", 2281337, dPlayer =>
            {
                PrintWarning("WEB> Testing account found [OK]");

                _.timer.Once(1, DPart.LoadParts);
                _.timer.Once(2, DMurder.LoadImages);
                _.timer.Once(3, FetchMurders);
                _.timer.Once(4, LoadImages);
                _.timer.Once(6, FetchMarket);

                _.timer.Once(30, callback);
            }); 
        }

        private IEnumerator LoadGlobalPlayers()
        {
            foreach (var check in BasePlayer.activePlayerList)
            {
                int index = BasePlayer.activePlayerList.ToList().IndexOf(check);
                
                if (index % 5 == 0)
                    PrintWarning($"WEB> Loaded {(float) index / BasePlayer.activePlayerList.Count}% loaded [OK]");
                OnPlayerConnected(check);
                yield return new WaitForSeconds(0.25f);
            }

            Initialized = true;
            yield return 0;
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/CheckEngine", CheckData);
        }
        
        private void OnServerInitialized()
        {
            _ = this;

            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/CheckEngine"))
                CheckData = Interface.Oxide.DataFileSystem.ReadObject<Hash<ulong, float>>($"{Name}/CheckEngine");

            timer.Every(60, SaveData);
            
            if (CheckEngine)
            {
                PrintError("Аварийный режим, вам на разборочку");
                return;
            }
             
            timer.Once(1, () =>
            {
                FetchBaseSync(() =>
                {
                    timer.Every(5, () =>
                    {
                        foreach (var check in Base)
                        {
                            if (check.Value.Quest != null && check.Value.Quest.TimeEnd / 1000 < Time())
                            {
                                if (check.Value.Quest.Murder.Level != 4)
                                {
                                    Interface.Oxide.CallHook("AddNotification", check.Key, $"{check.Value.Quest.Murder.Name.ToUpper()} ВЕРНУЛСЯ", $"Нажмите чтобы посмотреть найденные предметы", "1 1 1 0.5", 10, "", "", "chat.say /fury");
                                }
                                else
                                {
                                    Interface.Oxide.CallHook("AddNotification", check.Key, $"{check.Value.Quest.Murder.Name.ToUpper()} ОТКРЫТ", $"Нажмите чтобы посмотреть полученные предметы", "1 1 1 0.5", 10, "", "", "chat.say /fury");
                                } 
                            }
                        }
                    });
                    timer.Every(5, () =>
                    {
                        if (Market.Count == 0)
                        {
                            FetchBaseSync(null);
                        }
                    });
                    PrintError("Загрузка успешно окончена!");
                });
            });
            
        }
 
        private void OnPlayerConnected(BasePlayer player)
        {
            FetchPlayer(player);
        }
        private void OnPlayerDisconnected(BasePlayer player, string reason) => DestroyPlayer(player);

        private void Unload() => BasePlayer.activePlayerList.ToList().ToList().ToList().ForEach(DestroyPlayer);

        
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if ((entity.PrefabName.Contains("patrol") || entity.PrefabName.Contains("bradley") || entity.PrefabName.Contains("ch47")) && info?.InitiatorPlayer != null && Base.ContainsKey(info.InitiatorPlayer))
            {
                entity._name = info.InitiatorPlayer.UserIDString ?? "UNKNOWN";
            }
        }
        
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;
            
            var player = info.InitiatorPlayer;
            if (player == null || !Base.ContainsKey(player))
            {
                if (entity._name?.Length == 17)
                {
                    player = BasePlayer.Find(entity._name);
                } 
                
                if (player == null || !Base.ContainsKey(player))
                    return;
            }

            string shortName = entity.ShortPrefabName;
            if (entity is BuildingBlock)
                shortName = "tier" + (int) ((entity as BuildingBlock).grade);
            
            
            var amount = 0f;
            if (!Settings.KillExp.TryGetValue(shortName, out amount))
            {
                if (Settings.DebugErrors) {
                    Server.Command($"echo {shortName}");
                    player.ChatMessage("Неизвестный тип [ED]: " + shortName);
                }  
                return; 
            }
            
            if ((entity is BasePlayer && ((BasePlayer) entity).userID == player.userID)) return;
            
            player.SendConsoleCommand($"note.inv 605467368 1 \"<color=#d4d8ca>+{amount}</color><color=#b9bdaf>EXP</color>\"");
            AddEXP(player, amount, "Убийство " + shortName, entity.GetComponent<BasePlayer>() != null  ? ((BasePlayer) entity).userID : 0); 
        }
        
         
        private void OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer player)
        {
            if (player == null || card.accessLevel != cardReader.accessLevel) return;  
            
            var amount = 0f;
            if (!Settings.BuildExp.TryGetValue($"card{card.accessLevel}", out amount))
            {
                //Server.Command($"echo card{card.accessLevel}");
                if (Settings.DebugErrors)
                {  
                    player.ChatMessage("Неизвестный тип [EB]: " + $"echo card{card.accessLevel}");
                }
                return;
            }
            
            player.SendConsoleCommand($"note.inv 605467368 1 \"<color=#d4d8ca>+{amount}</color><color=#b9bdaf>EXP</color>\"");
            AddEXP(player, amount, "Свайп " + $"echo card{card.accessLevel}"); 
        }
        
        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            var player = plan.GetOwnerPlayer();
            var ent = go.ToBaseEntity();
            
            if (player == null || ent == null) return;
            
            string shortName = go.ToBaseEntity().ShortPrefabName;
            
            var amount = 0f;
            if (!Settings.BuildExp.TryGetValue(shortName, out amount))
            { 
                if (Settings.DebugErrors)
                {
                    Server.Command($"echo {shortName}");
                    player.ChatMessage("Неизвестный тип [EB]: " + shortName);
                }

                return;
            }

            var planItem = plan?.GetItem();
            if (planItem != null)
            {
                if (planItem.skin != 0)
                {
                    return;
                }
            }
            
            player.SendConsoleCommand($"note.inv 605467368 1 \"<color=#d4d8ca>+{amount}</color><color=#b9bdaf>EXP</color>\"");
            AddEXP(player, amount, "Постройка " + shortName); 
        }
        
         
        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (entity.name == "LOOTED") return;
            entity.name = "LOOTED";
            
            string shortName = entity.ShortPrefabName;
            
            var amount = 0f;
            if (!Settings.LootExp.TryGetValue(shortName, out amount))
            {
                if (Settings.DebugErrors)
                { 
                    Server.Command($"echo {shortName}");
                    player.ChatMessage("Неизвестный тип [EB]: " + shortName);
                }
                return;
            }
            
            player.SendConsoleCommand($"note.inv 605467368 1 \"<color=#d4d8ca>+{amount}</color><color=#b9bdaf>EXP</color>\"");
            AddEXP(player, amount, "Постройка " + shortName);
        }
        
        private void CanAffordUpgrade(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade)
        {
            if (player == null) return;
            string shortName = "tier" + (int) grade;
            
            var amount = 0f;
            if (!Settings.BuildExp.TryGetValue(shortName, out amount))
            {
                if (Settings.DebugErrors)
                {
                    Server.Command($"echo {shortName}"); 
                    player.ChatMessage("Неизвестный тип [AU]: " + shortName);
                    
                }

                return;
            }

            player.SendConsoleCommand($"note.inv 605467368 1 \"<color=#d4d8ca>+{amount}</color><color=#b9bdaf>EXP</color>\"");
            AddEXP(player, amount, "Улучшение " + shortName); 
        }
        
        private void OnCollectiblePickup(Item item, BasePlayer player, CollectibleEntity entity)
        {
            string shortName = entity.GetComponent<BaseEntity>().ShortPrefabName;
            
            var amount = 0f;
            if (!Settings.ResourceExp.TryGetValue(shortName, out amount))
            {
                if (Settings.DebugErrors)
                {
                    Server.Command($"echo {shortName}");
                    player.ChatMessage("Неизвестный тип [CP]: " + shortName);
                }

                return;
            }

            if (AddEXP(player, amount, "[CP] Добыча " + shortName, entity.net.ID))
            {
                
                item.name = item.info.displayName.english +  $" <size=10><color=#d4d8ca>+{amount}</color><color=#b9bdaf>EXP</color></size>";
                NextTick(() =>
                {
                    item.name = "";
                });
            }
            
        }
         
        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            string shortName = dispenser.GetComponent<BaseEntity>().ShortPrefabName;
            
            if (item.info.shortname == "wood") shortName = "tree"; 
            if (item.info.shortname == "sulfur.ore") shortName = "sulfur-ore"; 
            if (item.info.shortname == "metal.ore") shortName = "metal-ore"; 
            if (item.info.shortname == "stones") shortName = "stones";
            
            var amount = 0f;
            if (!Settings.ResourceExp.TryGetValue(shortName, out amount))
            {
                if (Settings.DebugErrors)
                {
                    Server.Command($"echo {shortName}");
                    player.ChatMessage("Неизвестный тип [DB]: " + shortName);
                }

                return;
            }
            
            if (AddEXP(player, amount, "[DP]  Добыча " + shortName, dispenser.GetComponent<BaseEntity>().net.ID))
            {
                player.SendConsoleCommand($"note.inv 605467368 1 \"<color=#d4d8ca>+{amount}</color><color=#b9bdaf>EXP</color>\"");
            } 
            return;
        }
        
        private void OnGrowableGather(GrowableEntity plant, Item item, BasePlayer player)
        {
            string shortName = plant.ShortPrefabName; 
            
            var amount = 0f;
            if (!Settings.ResourceExp.TryGetValue(shortName, out amount))
            {
                if (Settings.DebugErrors)
                {
                    Server.Command($"echo {shortName}");
                    player.ChatMessage("Неизвестный тип [CG]: " + shortName);
                }

                return;
            }
            
            item.name = item.info.displayName.english +  $" <size=10><color=#d4d8ca>+{amount}</color><color=#b9bdaf>EXP</color></size>";
            NextTick(() =>
            {
                item.name = "";
            });
            AddEXP(player, amount, "[CG] Добыча " + shortName); 
        }

        #endregion

        #region Debug

        [ConsoleCommand("exp_merge")]
        private void CmdConsole(ConsoleSystem.Arg args)
        {
            if (args.Player() != null) return;
            
            
            foreach (var check in CheckData)
            {
                webrequest.Enqueue($"{Settings.WebURL}/api/players/addExp/{check.Key}/{check.Value}?pass=ABCBLOO1ss1009g", "", (i, s) =>
                {
                }, _);
            }

            PrintError($"Merged {CheckData.Count} pl., total: {CheckData.Sum(p => p.Value).ToString("F2")} EXP");
            CheckData.Clear();
        }
       
        [ChatCommand("update.items")]
        private void CmdChat(BasePlayer player)
        {
            if (!player.IsAdmin) return;
            ServerMgr.Instance.StartCoroutine(LoadingQueue());
        }
        
        private IEnumerator LoadingQueue() 
        {
            var list = ItemManager.itemList.Where(p => p.Blueprint != null && p.Blueprint.isResearchable && p.Blueprint.workbenchLevelRequired > 0 && p.Blueprint.userCraftable);
            
            foreach (var check in list) 
            {
                PrintError($"Loaded ok: {check.shortname}"); 
                webrequest.Enqueue($"{Settings.WebURL}api/items/add/{check.shortname}/0/{check.stackable}/{check.stackable}/{check.Blueprint.workbenchLevelRequired}/caban/{check.category.ToString()}?pass=ABCBLOO1ss1009g", "", (i, s) => {}, this);
                yield return new WaitForSeconds(0.2f);  
            } 
        }
        
        #region EXP Money

        [ChatCommand("exp.none")]
        private void CmdChatExpNone(BasePlayer player)
        {
            Item item = ItemManager.CreateByPartialName("scraptransportheli.repair");
            item.MoveToContainer(player.inventory.containerBelt);
            UpdateExpCounter(player, ExpAction.None, 1488);
        }

        [ChatCommand("exp.plus")]
        private void CmdChatExpPlus(BasePlayer player) => UpdateExpCounter(player, ExpAction.Plus, 1488);

        [ChatCommand("exp.minus")]
        private void CmdChatExpMinus(BasePlayer player) => UpdateExpCounter(player, ExpAction.Minus, 1488);

        #endregion

        #endregion

        #region Methods

        private bool AddEXP(BasePlayer player, float amount, string mess = "", ulong targetId = 0)
        {
            //player.SendConsoleCommand($"note.inv 605467368 1 \"<color=#83c786>+{amount}EXP</color>\"");
            
            
            if (CheckEngine)
            {
                if (!CheckData.ContainsKey(player.userID))
                    CheckData.Add(player.userID, 0f);

                CheckData[player.userID] += amount;
                return false;
            }

            var obj = player.GetComponent<WPlayer>();
            if (obj == null)
            {
                return false;
            }
            
            return obj.AddExp(amount, mess, targetId);
        }

        private float GetExp(BasePlayer player)
        {
            if (CheckEngine)
            {
                if (!CheckData.ContainsKey(player.userID)) return 0f;
                var info = CheckData[player.userID];

                float left = info - Mathf.FloorToInt(info);
                return left < 0 ? 0f : left; 
            }
            
            if (!Base.ContainsKey(player)) return 0f;
            var infos = Base[player];

            float lefts = infos.Exp - Mathf.FloorToInt(infos.Exp);
            return lefts < 0 ? 0f : lefts; 
        }
        
        private float GetCurrentExp(BasePlayer player)
        {
            if (CheckEngine)
            {
                if (!CheckData.ContainsKey(player.userID)) return 0f;
                var info = CheckData[player.userID];

                float left = info - Mathf.FloorToInt(info);
                return left < 0 ? 0f : left; 
            }
            
            if (!Base.ContainsKey(player)) return 0f;
            var infos = Base[player];

            return infos.Exp; 
        }

        /// <summary> 
        /// Сохраняет логи и выводит ошибку если включён DEBUG
        /// </summary>
        private static void LogError(string text)
        {
            //_.PrintError(text);
            
            if (Settings.DebugErrors) _.LogToFile(_.Name, text, _); 
        }
        
        /// <summary>
        /// Обновляет текущий счётчик EXP по середине экрана
        /// </summary>
        /// <param name="action">Тип, если стоит plus/minus - происходит анимация изменения</param>
        private static void UpdateExpCounter(BasePlayer player, ExpAction action, float newValue)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, InitialLayer + ".EXP");
             
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"20 -100", OffsetMax = $"-0 -15"} ,
                Text = { Text = $"<b>{newValue}</b><color=#865f5a>EXP</color>", Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf", FontSize = 32, Color = "0.93 0.89 0.85 1"}
            }, InitialLayer + ".C", InitialLayer + ".EXP");

            if (action != ExpAction.None)
            {
                CuiHelper.DestroyUi(player, InitialLayer + ".EXP.Animation");
                container.Add(new CuiLabel 
                {
                    FadeOut = 2f, 
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"5 -100", OffsetMax = $"-0 -20"} ,
                    Text = {Text = action == ExpAction.Plus ? "⇡" : "⇣", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 28, Color = action == ExpAction.Plus ? "0.462 0.79 0.27" : "0.95 0.278 0.231" } 
                }, InitialLayer + ".C", InitialLayer + ".EXP.Animation"); 
            }

            CuiHelper.AddUi(player, container);

            if (action != ExpAction.None) CuiHelper.DestroyUi(player, InitialLayer + ".EXP.Animation");
        }

        private void LoadImages()
        { 
            ImageLibrary.Call("AddImage", "https://i.imgur.com/YkC5Np4.png", "Custom_Locker_Image_Murder"); 
            ImageLibrary.Call("AddImage", "https://i.imgur.com/BQx3bap.png", "Custom_WhiteLocker_Image_Murder"); 
            ImageLibrary.Call("AddImage", "http://static.moscow.ovh/images/games/rust/icons/blueprintbase.png", "Custom_Blueprint");
            
            FetchImages();
        }
 
        private void FetchImages()
        {
            if (!((bool) ImageLibrary.Call("HasImage", "Custom_Locker_Image_Murder")))
            {
                PrintError("C#> Images loaded failed [ERR]");
                timer.Once(1f, FetchImages);
                return;
            }
            if (!((bool) ImageLibrary.Call("HasImage", "Custom_Blueprint")))
            {
                PrintError("C#> Images loaded failed [ERR]");
                timer.Once(1f, FetchImages);
                return;
            }

            BlueprintImage = (string) ImageLibrary.Call("GetImage", "Custom_Blueprint");
            LockerImage = (string) ImageLibrary.Call("GetImage", "Custom_Locker_Image_Murder");
            WhiteLockerImage = (string) ImageLibrary.Call("GetImage", "Custom_WhiteLocker_Image_Murder"); 
            PrintWarning($"C#> Loaded correct images [OK]"); 
        }
        
        private void FetchMurders() 
        { 
            var fetchConditions = 0;
                
            foreach (var check in Settings.TaskPrice)
                fetchConditions += check.Key;
                
            if (fetchConditions != 10) PrintError($"C#> Wrong settings for murders");
            else PrintWarning($"C#> Loaded correct murders [OK]");
        }

        private void FetchMarket()
        {
            webrequest.Enqueue($"{Settings.WebURL}/api/market/fetch?pass=ABCBLOO1ss1009g", "", (i, s) =>
            {
                DResult result = DResult.GenerateFromAnswer(s, "UNKNOWN", "fetch market");
                if (result == null) return; 
 
                try
                {
                    Market = JsonConvert.DeserializeObject<HashSet<DItem>>(result.Result.ToString());
                    
                    foreach (var check in Market.Where(p => p.Url.Length > 5))
                    { 
                        ImageLibrary.Call("AddImage", check.Url, $"M.Custom.{check.Id}");
                    }
                    
                    PrintWarning($"WEB> Market loaded {Market.Count} items [OK]");

                    PrintWarning($"WEB> Start loading players {BasePlayer.activePlayerList.Count}");
                    ServerMgr.Instance.StartCoroutine(LoadGlobalPlayers());
                }  
                catch     
                {
                    if (Market.Count == 0)
                    { 
                        PrintError($"WEB> Market failed load [ERROR | {Market.Count}]"); 
                        timer.Once(1, FetchMarket);  
                    }
                }
            }, this);
        }

        private void UpdatePlayer(BasePlayer player, DPlayer dPlayer)
        {
            if (!Base.ContainsKey(player)) 
                Base.Add(player, null);
 
            dPlayer.Inventory.Clear();
            dPlayer.InventoryCaches.ForEach(p => dPlayer.Inventory.Add(DItem.FromCache(p)));
            //PrintError($"{player.userID}. Cache: {dPlayer.InventoryCaches.Count} | Inv: {dPlayer.Inventory.Count}");
            
            Base[player] = dPlayer;
        }
        
        private void FetchPlayer(BasePlayer player)
        {
            if (CheckEngine)
            {
                if (!CheckData.ContainsKey(player.userID))
                    CheckData.Add(player.userID, 0);
                
                return;
            }
            
            DPlayer.GetFromPlayer(player, dPlayer =>
            {
                UpdatePlayer(player, dPlayer);

                if (!Workers.ContainsKey(player)) 
                    Workers.Add(player, player.gameObject.AddComponent<WPlayer>());

                if (/*!*/Joined.Contains(player.userID))
                {
                    Joined.Add(player.userID);
                    player.ChatMessage("Спасибо что зашли ебать, лови 500 ехп в рыло");
                    
                    AddEXP(player, 500, "Jopa"); 
                }
            });
        }

        private void DestroyPlayer(BasePlayer player)
        {
            if (CheckEngine) return;
            
            var obj = player.GetComponent<WPlayer>();
            if (obj != null) UnityEngine.Object.Destroy(obj); 
            
            Base.Remove(player);
            Workers.Remove(player);
        }

        #endregion

        #region Commands

        [ConsoleCommand("UI_Quest_FuryController")]
        private void CmdConsoleHandler(ConsoleSystem.Arg arg)
        {
            if (!Initialized) return;
            
            var player = arg.Player();
            if (player == null || !arg.HasArgs(1)) return;

            if (!Base.ContainsKey(player))
            {
                FetchPlayer(player);
                return;
            }
			
			if (player.IsDead())
            {
                player.ChatMessage("Использование наёмников во время смерти ограничено.");
				return;
            }

            var info = Base[player];
            switch (arg.Args[0].ToLower())
            {
                case "sync":
                {
                    if (!SyncCooldowns.ContainsKey(player.userID))
                        SyncCooldowns.Add(player.userID, 0);
 
                    if (SyncCooldowns[player.userID] > Time() && !player.IsAdmin) return;
                    
                    SyncCooldowns[player.userID] =  Time() + 60f; 
                    
                    
                    CuiHelper.DestroyUi(player, InitialLayer);
                    player.ChatMessage("Подождите, происходит <color=orange>синхронизация</color> с БД!");
                    
                    FetchPlayer(player);
                    break;
                }
                case "shop":
                {
                    if (arg.HasArgs(2))
                    {
                        switch (arg.Args[1].ToLower())
                        {
                            case "page":
                            {
                                int page = 0;
                                if (!int.TryParse(arg.Args[2], out page)) return;

                                InitializeShop(player, info, arg.Args[3], page);
                                break;
                            }
                            case "buy":
                            {
                                int index = 0;
                                if (!int.TryParse(arg.Args[2], out index)) return;

                                var item = Market.FirstOrDefault(p => p.Id == index);
                                if (item == null) return;

                                item.Buy(player, info);
                                break;
                            }
                        }
                    }
                    
                    
                    if (!arg.HasArgs(3))
                    {
                        InitializeLayers(player, "SHOP");
                        InitializeShop(player, info, arg.HasArgs(2) ? arg.Args[1] : "ВСЁ");  
                        return;
                    }
                    break;
                }
                case "parts":
                {if (arg.HasArgs(2))
                    {
                        switch (arg.Args[1].ToLower()) 
                        {
                            case "get":
                            {
                                int id = 0;
                                if (!int.TryParse(arg.Args[2], out id)) return;

                                var part = Parts.FirstOrDefault(p => p.Id == id);
                                int count = info.Inventory.Count(p => p.IsPart == part.Name);

                                if (count < part.Amount) return;
                                
                                //Effect.server.Run("assets/prefabs/misc/xmas/presents/effects/unwrap.prefab", player.transform.position);

                                Effect effect = new Effect("assets/bundled/prefabs/fx/notice/stack.world.fx.prefab", player, 0, new Vector3(), new Vector3());
                                EffectNetwork.Send(effect, player.Connection);
                                 
                                webrequest.Enqueue($"{Settings.WebURL}/api/parts/get/{player.userID}/{part.Id}?pass=ABCBLOO1ss1009g", "", (g, s) =>
                                { 
                                    DResult result = DResult.GenerateFromAnswer(s, player.UserIDString, "get parts");
                                    
                                    if (result == null)
                                    {
                                        return;
                                    }

                                    if (result.Result is Boolean && (bool) result.Result)
                                    { 
                                        for (int i = 0; i < part.Amount; i++)
                                        {
                                            info.Inventory.Remove(info.Inventory.FirstOrDefault(p => p.IsPart == part.Name));
                                        }
                                        
                                        if (part.Command.Length > 1)
                                        {
                                            Server.Command(part.Command.Replace("%STEAMID%", player.UserIDString));
                                        }

                                        if (part.Item.Length > 1)
                                        {
                                            Item item = ItemManager.CreateByName(part.Item, 1);
                                    
                                            if (!item.MoveToContainer(player.inventory.containerMain)) 
                                                item.Drop(player.transform.position, Vector3.zero);
                            
                                            player.SendConsoleCommand($"note.inv {item.info.itemid} {item.amount}");
                                        }
                                        
                                        InitializeParts(player, info); 
                                    }
                                }, this);
                                break;
                            }
                        }
                    }
                    
                    if (!arg.HasArgs(3))
                    {
                        InitializeLayers(player, "PARTS");
                        InitializeParts(player, info);  
                        return;
                    }
                    break;
                }
                case "store":
                {
                    if (arg.HasArgs(2))
                    {
                        switch (arg.Args[1].ToLower()) 
                        {
                            case "page":
                            {
                                int page = 0;
                                if (!int.TryParse(arg.Args[2], out page)) return;
                                
                                InitializeInventory(player, info, arg.Args[3], "STORE", page);  
                                break;
                            }
                            case "refund": 
                            {
                                string itemIndex = arg.Args[2];
                                
                                var item = info.Inventory.FirstOrDefault(p => p.OutID == itemIndex);
                                if (item == null) return; 
                                
                                
                                Effect effect = new Effect("assets/bundled/prefabs/fx/repairbench/itemrepair.prefab", player, 0, new Vector3(), new Vector3());
                                EffectNetwork.Send(effect, player.Connection);
                                item.RefundItem(player, info);
                                break;
                            } 
                            case "take":
                            {
                                string itemIndex = arg.Args[2];
                                
                                var item = info.Inventory.FirstOrDefault(p => p.OutID == itemIndex);
                                if (item == null) return; 
                                
                                item.ProcessItem(player, info); 
                                break;
                            } 
                            case "show": 
                            {
                                string itemIndex = arg.Args[2];
                                
                                var item = info.Inventory.FirstOrDefault(p => p.OutID == itemIndex);
                                if (item == null) return; 

                                CuiHelper.DestroyUi(player, InitialLayer + $".HRPStore{item.OutID}.Overflow");
                                CuiElementContainer container = new CuiElementContainer();
                                
                                //PrintWarning("OK");
                                container.Add(new CuiPanel
                                {
                                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                                    Image = { Color = "1 1 1 0.2" }
                                }, InitialLayer + $".HRPStore{item.OutID}", InitialLayer + $".HRPStore{item.OutID}.Overflow");
                                
                                container.Add(new CuiButton
                                {
                                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                                    Button = { Color = "0 0 0 0", Close = InitialLayer + $".HRPStore{item.OutID}.Overflow" },
                                    Text = { Text = "" }
                                }, InitialLayer + $".HRPStore{item.OutID}.Overflow");
 
                                container.Add(new CuiButton 
                                {
                                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 5", OffsetMax = "0 22.5"},
                                    Button = { Color = "0.105 0.117 0.094 0.9", Command = HandlerCommand + $" store refund {itemIndex}"}, 
                                    Text = { Text = $"Вернуть за {item.Exp}exp", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 10 }
                                }, InitialLayer + $".HRPStore{item.OutID}.Overflow");

                                if (item.IsPart.Length < 2)
                                {
                                    container.Add(new CuiButton
                                    {
                                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 27.5", OffsetMax = "0 45"},
                                        Button = {Color = "0.105 0.117 0.094 0.9", Command = HandlerCommand + $" store take {itemIndex}"}, 
                                        Text = { Text = $"Забрать", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 10 }
                                    }, InitialLayer + $".HRPStore{item.OutID}.Overflow");
                                }

                                CuiHelper.AddUi(player, container); 
                                break;
                            }
                        }
                    }
                    
                    if (!arg.HasArgs(3))
                    {
                        InitializeLayers(player, "STORE");
                        InitializeInventory(player, info, arg.HasArgs(2) ? arg.Args[1] : "ВСЁ"); 
                        return;
                    }
                    
                    break;
                }
                case "fury":
                {
                    if (!arg.HasArgs(2)) 
                    {
                        InitializeLayers(player,  "FURY");
                        InitializeMurders(player, Base[player]); 
                        return;
                    }

                    switch (arg.Args[1].ToLower())
                    {
                        case "create":
                        { 
                            if (!arg.HasArgs(3)) return;

                            int level = -1;
                            if (!int.TryParse(arg.Args[2], out level)) return;

                            var murder = info.Murders.FirstOrDefault(p => p.Level == level);
                            if (murder == null) return;
                            
                            info.GenerateQuest(murder, b =>
                            {
                                if (!b) return;

                                if (murder.Level == 4)
                                {
                                    Effect effect = new Effect("assets/bundled/prefabs/fx/gestures/cameratakescreenshot.prefab", player, 0, new Vector3(), new Vector3());
                                    EffectNetwork.Send(effect, player.Connection);
                                }
                                else
                                {
                                    Effect effect = new Effect("assets/prefabs/npc/scientist/sound/chatter.prefab", player, 0, new Vector3(), new Vector3());
                                    EffectNetwork.Send(effect, player.Connection);
                                }
                                InitializeMurders(player, Base[player]); 
                            });
                            break;
                        } 
                        case "finish":
                        {
                            info.FinishQuest(b =>
                            {
                                if (!b || player == null) return;
                                
                                InitializeMurders(player, Base[player]); 
                            });
                            break;
                        }
                    }
                    
                    break;
                }
            }
        }

        [ConsoleCommand("base_sync")]
        private void CmdConsoleSync(ConsoleSystem.Arg args)
        {
            if (args.Player() != null) return;
            
            FetchBaseSync(null); 
        }

        [ConsoleCommand("free_destroy")]
        private void CmdConsoleFreeDestroy(ConsoleSystem.Arg args)
        {
            if (args.Player() != null) return; 
            
            webrequest.Enqueue($"{Settings.WebURL}/api/free/2281337sasfggadsg235264rgfsgfdsgfjdslkghbfjlsh43j5h64j32h56jk4l326jlk34b26lj43?pass=ABCBLOO1ss1009g", "", (i, s) => { FetchBaseSync(null); }, this);
        }
 
        [ChatCommand("furySecret")] 
        private void CmdChatFurySecret(BasePlayer player)
        {
            if (!Initialized) return;

            if (CheckEngine)
            {
                player.ChatMessage($"Мы в аварийном режиме, у вас: {CheckData[player.userID].ToString("F2")} EXP");
                return;
            }
            
            InitializeLayers(player, Sections.FirstOrDefault().Key);
            InitializeMurders(player, Base[player]); 
        }
 
        [ChatCommand("fury")]
        private void CmdChatFury(BasePlayer player)
        {
            if (!Initialized) return; 

            if (CheckEngine)
            {
                player.ChatMessage($"Мы в аварийном режиме, у вас: {CheckData[player.userID].ToString("F2")} EXP");
                return;
            }
            
            player.SendConsoleCommand("chat.say /menuSecret");  
            player.SendConsoleCommand("UI_RM_Handler choose 2");
            
            //InitializeLayers(player, Sections.FirstOrDefault().Key);
            //InitializeMurders(player, Base[player]); 
        }

        #endregion

        #region Interfaces

        private static string InitialLayer = "UI_QuestLayer";

        private void InitializeInventory(BasePlayer player, DPlayer dPlayer, string category = "ВСЁ", string mode = "STORE", int page = 0)
        {
            CuiElementContainer container = new CuiElementContainer(); 
            CuiHelper.DestroyUi(player, InitialLayer + ".RP");
            
            container.Add(new CuiPanel()
            { 
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"},  
                Image         = {Color = "0 0 0 0" }
            }, InitialLayer + ".R", InitialLayer + ".RP");
            
            

            container.Add(new CuiPanel
            {
                RectTransform =  {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "30 -70", OffsetMax = "-30 -30"},
                Image = {Color = "0 0 0 0"}
            }, InitialLayer + ".RP", InitialLayer + ".HRPHeader");

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                Button = {Color = mode == "STORE" ? "0.686 0.686 0.686 0.4" : "0.686 0.686 0.686 0.25" },
                Text = { Text = "ЯЩИК НАЁМНИКОВ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = mode == "STORE" ? "0.815 0.776 0.741" : "0.815 0.776 0.741 0.5", FontSize = 24 }
            }, InitialLayer + ".HRPHeader", InitialLayer + ".HRPHeader1");

            
            UpdateExpCounter(player, ExpAction.None, dPlayer.Exp);

            var categories = new List<string> { "ВСЁ" };
            foreach (var cat in dPlayer.Inventory)
            {
                if (!categories.Contains(cat.Category))
                    categories.Add(cat.Category);
            }
            
            if (categories.Count > 7)
                categories.RemoveRange(7, categories.Count - 7);
            
            container.Add(new CuiPanel
            {
                RectTransform =  {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 -30", OffsetMax = "0 -5"},
                Image = {Color = "0 0 0 0"}
            }, InitialLayer + ".HRPHeader", InitialLayer + ".HRPCats");

            var oneItemMargin = (1 - (categories.Count - 1) * 0.01f) / categories.Count;
            
            float floatMargin = 0;
            foreach (var cat in categories)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{floatMargin} 0", AnchorMax = $"{Math.Min(floatMargin + oneItemMargin, 1)} 1", OffsetMax = "0 0"},
                    Image = { Color = category == cat ? "0.207 0.56 0.784 0.7" :  "0.686 0.686 0.686 0.3" }
                }, InitialLayer + ".HRPCats", InitialLayer + $".HRPCats.{cat}");
                
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = cat.ToUpper(), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "0.815 0.776 0.741", FontSize = 12 }
                }, InitialLayer + $".HRPCats.{cat}");
                
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0", Command = HandlerCommand + $" store {cat}" },
                    Text = { Text = "", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "0.815 0.776 0.741", FontSize = 14 }
                }, InitialLayer + $".HRPCats.{cat}");

                floatMargin += oneItemMargin + 0.01f;
            }
            
            container.Add(new CuiPanel
            {
                RectTransform =  {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "30 55", OffsetMax = "-30 -105"},
                Image = {Color = "1 1 1 0"}
            }, InitialLayer + ".RP", InitialLayer + ".HRPStore");

            dPlayer.RefreshIDs();

            var inventory = dPlayer.Inventory.Where(p => p.Category == category || category == "ВСЁ").ToList();
           

            int pString = 6;
            float pHeight = 90f;

            float elemCount = 1f / pString;

            int elementId = 0;
            float topMargin = 0;
            foreach (var check in inventory.Skip(page * 6 * 6).Take(6 * 6).Select((i, t) => new { A = i, B = t }))
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{elementId * elemCount} 1", AnchorMax = $"{(elementId + 1) * elemCount} 1", OffsetMin = $"{(elementId == 0 ? "0" : "2.5")} {topMargin - pHeight}", OffsetMax = $"{(elementId == 5 ? "0" : "-2.5")} {topMargin}" },
                    Button = { Color = "0.815 0.776 0.741 0.15", Command = HandlerCommand + $" store show {check.A.OutID}" },
                    Text = { Text = "" }
                }, InitialLayer + ".HRPStore", InitialLayer + $".HRPStore{check.A.OutID}");

                if (check.A.IsBlueprint)
                {
                    container.Add(new CuiElement
                    {
                        Parent = InitialLayer + $".HRPStore{check.A.OutID}",
                        Components =  
                        { 
                            new CuiRawImageComponent { Png = BlueprintImage }, 
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "10 10", OffsetMax = "-10 -10" }
                        } 
                    });
                }
                container.Add(new CuiElement
                {
                    Parent = InitialLayer + $".HRPStore{check.A.OutID}",
                    Components = 
                    {
                        new CuiRawImageComponent {Png = check.A.GetImage(), Color = "1 1 1 1"},
                        new CuiRectTransformComponent {AnchorMin = "0.5 0", AnchorMax = "0.5 1", OffsetMin = "-40 5", OffsetMax = "40 -5"}
                    }
                });

                elementId++;
                if (elementId == 6)
                {
                    elementId = 0;

                    topMargin -= pHeight + 5;
                }
            }
            
            #region PaginationMember

            string leftCommand = $"{HandlerCommand} store page {page-1} {category}"; 
            string rightCommand = $"{HandlerCommand} store page {page+1} {category}";
            bool leftActive = page > 0;
            bool rightActive = (page + 1 ) * 6 * 6 < inventory.Count;
 
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = $"-120 10", OffsetMax = "-30 40" },
                Image = { Color = "0 0 0 0" }
            }, InitialLayer + ".RP", InitialLayer + ".Holder.PS");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = "-1000 0", OffsetMax = "-10 0" },
                Text = { Text = "• ЯЩИК НАЁМНИКОВ СИНХРОНИЗИРОВАН МЕЖДУ ВСЕМИ СЕРВЕРАМИ", Align = TextAnchor.MiddleRight, Font = "robotocondensed-regular.ttf", FontSize =  14, Color = "0.815 0.776 0.741 0.3" }
            }, InitialLayer + ".Holder.PS");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = (page + 1).ToString(), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 16 }
            }, InitialLayer + ".Holder" + ".PS"); 
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.35 1", OffsetMin = $"2 2", OffsetMax = "-2 -2" },
                Image = { Color = leftActive ? "0.294 0.38 0.168 0" : "0.294 0.38 0.168 0" }
            }, InitialLayer + ".Holder" + ".PS", InitialLayer + ".Holder" + ".PS.L");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = leftActive ? leftCommand : "" },
                Text = { Text = "<b>◄</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = leftActive ? "0.647 0.917 0.188 1" : "0.294 0.38 0.168 0.3" }
            }, InitialLayer + ".Holder" + ".PS.L");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.65 0", AnchorMax = "1 1", OffsetMin = $"2 2", OffsetMax = "-2 -2" },
                Image = { Color = rightActive ? "0.294 0.38 0.168 0" : "0.294 0.38 0.168 0" }
            }, InitialLayer + ".Holder" + ".PS", InitialLayer + ".Holder" + ".PS.R");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = rightActive ? rightCommand : "" },
                Text = { Text = "<b>►</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.647 0.917 0.188 1" : "0.294 0.38 0.168 0.3" }
            }, InitialLayer + ".Holder" + ".PS.R");

            #endregion
            

            CuiHelper.AddUi(player, container);
        }
        
        private void InitializeShop(BasePlayer player, DPlayer dPlayer, string category = "ВСЁ", int page = 0)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, InitialLayer + ".RP");
            
            container.Add(new CuiPanel()
            { 
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"},  
                Image         = {Color = "0 0 0 0" }
            }, InitialLayer + ".R", InitialLayer + ".RP");

            container.Add(new CuiPanel
            {
                RectTransform =  {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "30 -70", OffsetMax = "-30 -30"},
                Image = {Color = "0 0 0 0"}
            }, InitialLayer + ".RP", InitialLayer + ".HRPHeader");

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                Button = {Color = "0.686 0.686 0.686 0.4" },
                Text = { Text = "РЫНОК", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "0.815 0.776 0.741", FontSize = 24 }
            }, InitialLayer + ".HRPHeader", InitialLayer + ".HRPHeader1");
            
            UpdateExpCounter(player, ExpAction.None, dPlayer.Exp);

            var categories = new List<string> { "ВСЁ" };
            foreach (var cat in Market.Where(p => p.ExpBuy > -1))  
            {
                if (!categories.Contains(cat.Category))
                    categories.Add(cat.Category);
            } 
            
            container.Add(new CuiPanel
            {
                RectTransform =  {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -25", OffsetMax = "0 0"},
                Image = {Color = "0 0 0 0"}
            }, InitialLayer + ".HRPHeader", InitialLayer + ".HRPCats");

            var oneItemMargin = (1 - (6) * 0.01f) / 7;
            
            float floatMargin = 0;
            float floatMarginVertical = -45;
            
            foreach (var cat in categories)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{floatMargin} 0", AnchorMax = $"{floatMargin + oneItemMargin} 1", OffsetMin = $"0 {floatMarginVertical}", OffsetMax = $"0 {floatMarginVertical}" },
                    Image = { Color = category == cat ? "0.207 0.56 0.784 0.7" :  "0.686 0.686 0.686 0.3" }
                }, InitialLayer + ".HRPCats", InitialLayer + $".HRPCats.{cat}");
                
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = cat.ToUpper(), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "0.815 0.776 0.741", FontSize = 12 }
                }, InitialLayer + $".HRPCats.{cat}");
                
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0", Command = HandlerCommand + $" shop {cat}" },
                    Text = { Text = "", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "0.815 0.776 0.741", FontSize = 14 }
                }, InitialLayer + $".HRPCats.{cat}");

                floatMargin += oneItemMargin + 0.01f;

                if (floatMargin >= 1)
                {
                    floatMargin = 0;
                    floatMarginVertical -= 30;
                }
            }

            while (categories.Count % 7 != 0)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{floatMargin} 0", AnchorMax = $"{floatMargin + oneItemMargin} 1", OffsetMin = $"0 {floatMarginVertical}", OffsetMax = $"0 {floatMarginVertical}" },
                    Image = { Color = "0.686 0.686 0.686 0.2" }
                }, InitialLayer + ".HRPCats" );

                floatMargin += oneItemMargin + 0.01f; 
                categories.Add("RANDOM NAME +");
            }
            
            container.Add(new CuiPanel 
            {
                RectTransform =  {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"30 {20 + floatMarginVertical}", OffsetMax = $"-30 {-60 + floatMarginVertical}"},
                Image = {Color = "1 1 1 0"}
            }, InitialLayer + ".RP", InitialLayer + ".HRPStore");
 
            var itemList = Market.Where(p => (p.Category == category || category == "ВСЁ") && p.ExpBuy > -1); 
            
            int pString = 5;
            float pHeight = 120;

            float elemCount = 1f / pString;

            int elementId = 0;
            float topMargin = 0;
            foreach (var check in itemList.OrderByDescending(p => p.Category).Skip(page * 5 * 4).Take(5 * 4).Select((i, t) => new { A = i, B = t }))
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{elementId * elemCount} 1", AnchorMax = $"{(elementId + 1) * elemCount} 1", OffsetMin = $"{0} {topMargin - pHeight}", OffsetMax = $"{(elementId == 4 ? "0" : "-5")} {topMargin}" },
                    Button = { Color = "0.815 0.776 0.741 0.15" },
                    Text = { Text = "" } 
                }, InitialLayer + ".HRPStore", InitialLayer + $".HRPStore{check.B}");
                
                container.Add(new CuiButton 
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = $"0 -10", OffsetMax = $"0 0" },
                    Button = { Color = "0.815 0.776 0.741 0.15" },
                    Text = { Text = "" }     
                }, InitialLayer + $".HRPStore{check.B}");
   
                if (dPlayer.Blocked.All(p => p.Id != check.A.Id))
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = $"0 -10", OffsetMax = $"0 12.5" },
                        Button = { Color = dPlayer.Exp > check.A.ExpBuy ? "0.294 0.356 0.18" : "0.815 0.776 0.741 0.3" },
                        Text = { Text = $"КУПИТЬ ЗА <b>{check.A.ExpBuy}</b>EXP", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = dPlayer.Exp > check.A.ExpBuy ? "0.62 0.886 0.188" : "0.815 0.776 0.741 0.5"}  
                    }, InitialLayer + $".HRPStore{check.B}");
                    
                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax                                                                     = "1 0", OffsetMin = $"0 -10", OffsetMax = $"0 12.5" },
                        Button        = { Color    = dPlayer.Exp > check.A.ExpBuy ? "0.294 0.356 0.18" : "0.815 0.776 0.741 0.3", Command = HandlerCommand + $" shop buy {check.A.Id}", Close = InitialLayer + $".HRPStore{check.B}.Visuaal" },
                        Text          = { Text     = $"КУПИТЬ ЗА <b>{check.A.ExpBuy}</b>EXP", Font                                        = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = dPlayer.Exp > check.A.ExpBuy ? "0.62 0.886 0.188" : "0.815 0.776 0.741 0.5"}  
                    }, InitialLayer + $".HRPStore{check.B}", InitialLayer + $".HRPStore{check.B}.Visuaal");
                }
                else
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = $"0 -10", OffsetMax = $"0 12.5" },
                        Button = { Color = "0.815 0.776 0.741 0.3", Command = HandlerCommand + $" shop buy {check.A.Id}" },
                        Text = { Text = $"УЖЕ КУПЛЕНО", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.815 0.776 0.741 0.5"}  
                    }, InitialLayer + $".HRPStore{check.B}");
                }
 
                if (check.A.IsBlueprint)
                {
                    container.Add(new CuiElement
                    {
                        Parent = InitialLayer + $".HRPStore{check.B}",
                        Components = 
                        {
                            new CuiRawImageComponent { Png = BlueprintImage }, 
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "15 25", OffsetMax = "-15 -10" }
                        } 
                    });
                }
                
                container.Add(new CuiElement
                {
                    Parent = InitialLayer + $".HRPStore{check.B}",
                    Components =
                    {
                        new CuiRawImageComponent {Png = check.A.GetImage(), Color = "1 1 1 1"},
                        new CuiRectTransformComponent {AnchorMin = "0.5 0", AnchorMax = "0.5 1", OffsetMin = "-50 15", OffsetMax = "50 -5"}
                    } 
                });


                elementId++;
                if (elementId == 5)
                {
                    elementId = 0;

                    topMargin -= pHeight + 15;
                }
            }
            
            #region PaginationMember

            string leftCommand = $"{HandlerCommand} shop page {page-1} {category}"; 
            string rightCommand = $"{HandlerCommand} shop page {page+1} {category}";
            bool leftActive = page > 0;
            bool rightActive = (page + 1 ) * 5 * 4 < itemList.Count();
 
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = $"-120 10", OffsetMax = "-30 40" },
                Image = { Color = "0 0 0 0" }
            }, InitialLayer + ".RP", InitialLayer + ".Holder.PS");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = "-1000 0", OffsetMax = "-10 0" },
                Text = { Text = "• ПРИОБРЕТЁННЫЙ ТОВАР БУДЕТ ПОМЕЩЁН В ЯЩИК НАЁМНИКОВ", Align = TextAnchor.MiddleRight, Font = "robotocondensed-regular.ttf", FontSize =  14, Color = "0.815 0.776 0.741 0.3" }
            }, InitialLayer + ".Holder.PS");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = (page + 1).ToString(), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 16 }
            }, InitialLayer + ".Holder" + ".PS"); 
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.35 1", OffsetMin = $"2 2", OffsetMax = "-2 -2" },
                Image = { Color = leftActive ? "0.294 0.38 0.168 0" : "0.294 0.38 0.168 0" }
            }, InitialLayer + ".Holder" + ".PS", InitialLayer + ".Holder" + ".PS.L");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = leftActive ? leftCommand : "" },
                Text = { Text = "<b>◄</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = leftActive ? "0.647 0.917 0.188 1" : "0.294 0.38 0.168 0.3" }
            }, InitialLayer + ".Holder" + ".PS.L");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.65 0", AnchorMax = "1 1", OffsetMin = $"2 2", OffsetMax = "-2 -2" },
                Image = { Color = rightActive ? "0.294 0.38 0.168 0" : "0.294 0.38 0.168 0" }
            }, InitialLayer + ".Holder" + ".PS", InitialLayer + ".Holder" + ".PS.R");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = rightActive ? rightCommand : "" },
                Text = { Text = "<b>►</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.647 0.917 0.188 1" : "0.294 0.38 0.168 0.3" }
            }, InitialLayer + ".Holder" + ".PS.R");

            #endregion

            CuiHelper.AddUi(player, container);
        }

        private IEnumerator Speed(BasePlayer player) 
        {
            CuiHelper.DestroyUi(player, "LayerInstance");
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                Image = {Color = "1 1 1 0"}
            }, "Overlay", "LayerInstance");
            
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "-0 -0"},
                Image = {Color = "0 0 0 1"}
            }, "LayerInstance", "InstanceStory");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = "Directed by:\n<b>РАЗРАБОТЧИК</b> ➫ Владимир Хуганов\n<b>UI/UX ДИЗАЙНЕР</b> ➫ Денис Хаскарёв", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 28 }
            }, "InstanceStory");

            float step = 700;
            float delay = 0.0000000000001f;
            
            container.Add(new CuiPanel
            {
                FadeOut = 1f,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "-0 -0"},
                Image = {Color = "1 1 1 1"}
            }, "LayerInstance", "InstanceStoryRemove");
            
            CuiHelper.AddUi(player, container);
            container.Clear();

            CuiHelper.DestroyUi(player, "InstanceStoryRemove");
            yield return new WaitForSeconds(1f);
            yield return new WaitForSeconds(1f);
            for (var i = 0; i < step; i++)
            {
                container.Add(new CuiPanel 
                {
                    RectTransform = { AnchorMin = $"0 {i * (1 / step)}", AnchorMax = $"1 {(i + 1) * (1 / step)}", OffsetMax = "0 0" },
                    Image = { Color = "1 1 1 1"}
                }, "InstanceStory");
                
                container.Add(new CuiPanel 
                {
                    RectTransform = { AnchorMin = $"0 {(step - i) * (1 / step)}", AnchorMax = $"1 {(step - i + 1) * (1 / step)}", OffsetMax = "0 0" },
                    Image = { Color = "1 1 1 1"}
                }, "InstanceStory");

                CuiHelper.AddUi(player, container);
                container.Clear();
             
                //player.SendConsoleCommand("echo 1");
                yield return new WaitForSeconds(delay);
            }


            yield return new WaitForSeconds(1f);

            CuiHelper.DestroyUi(player, "LayerInstance");
        }
        
        [ChatCommand("r.developer")]
        private void CmdChatSafffds(BasePlayer player)
        {
            ServerMgr.Instance.StartCoroutine(Speed(player));
        }
        
        
        private void InitializeParts(BasePlayer player, DPlayer dPlayer)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, InitialLayer + ".RP");
            
            container.Add(new CuiPanel()
            { 
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"},  
                Image         = {Color = "0 0 0 0" }
            }, InitialLayer + ".R", InitialLayer + ".RP");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -140", OffsetMax = "0 0" },
                Image = { Color = "0.29411 0.27450 0.254901 0.6" }
            }, InitialLayer + ".R", InitialLayer + ".H");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"28 -100", OffsetMax = $"-30 -5"} ,
                Text = { Text = "ИНФОРМАЦИЯ", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 48, Color = "0.93 0.89 0.85 1"}
            }, InitialLayer + ".H");

            string helpText = "Собранная здесь привилегия после активации будет действовать <b>7 дней</b>, точно так-же как и обычная привилегия купленная в нашем магазине <b>bloodrust.ru</b>. После нажатия кнопки <b><ЗАБРАТЬ></b> привилегия моментально активируется на этом сервере, а предмет будет выдан в инвентарь. Весь прогресс сохраняется и доступен <b>на всех</b> наших серверах.";
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "30 0", OffsetMax = "0 -60" },
                Text = { Text = helpText, Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf", FontSize = 14, Color = "0.67 0.63 0.596"}
            }, InitialLayer + ".H");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "30 30", OffsetMax = "-30 -155"},
                Image = {Color = "1 1 1 0"}
            }, InitialLayer + ".RP", InitialLayer + ".RPHolder");

            int pString = 4;
            float pHeight = 208;

            float elemCount = 1f / pString;

            int elementId = 0;
            float topMargin = -13;
            foreach (var check in Parts.OrderBy(p => p.Id))
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{elementId * elemCount} 1", AnchorMax = $"{(elementId + 1) * elemCount} 1", OffsetMin = $"3 {topMargin - pHeight}", OffsetMax = $"-3 {topMargin}" },
                    Image = { Color = "1 1 1 0.3" }
                }, InitialLayer + ".RPHolder", InitialLayer + $".RPHolder.{elementId}");

                container.Add(new CuiElement
                {
                    Parent = InitialLayer + $".RPHolder.{elementId}",
                    Components =
                    {
                        new CuiRawImageComponent {Png = check.GetImage()},
                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"}
                    }
                });
                
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Image = { Color = "0 0 0 0.2", Material = "assets/content/ui/uibackgroundblur.mat" } 
                }, InitialLayer + $".RPHolder.{elementId}");
                

                container.Add(new CuiElement
                {
                    Parent = InitialLayer + $".RPHolder.{elementId}",
                    Components =
                    {
                        new CuiRawImageComponent {Png = check.GetImage()},
                        new CuiRectTransformComponent {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-68 -104", OffsetMax = "68 104"}
                    }
                });
                

                int count = dPlayer.Inventory.Count(p => p.IsPart == check.Name);
                bool canGet = count >= check.Amount;
                 
                if (!canGet)
                {
                    container.Add(new CuiPanel  
                    { 
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-1 0", OffsetMax = "1 0" },
                        Image = { Color = "0 0 0 0.9" }    
                    }, InitialLayer + $".RPHolder.{elementId}");
                }
                
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0.16", AnchorMax = "1 0.16", OffsetMin = "-1 0", OffsetMax = "1 20"},
                    Button = {Color = canGet ? "0 0 0 0.9" : "0 0 0 0.7"},  
                    Text = { Text = $"Собрано: <b>{count} из {check.Amount}</b>".ToUpper(), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 14, Color = canGet ? "1 1 1 0.9" : "1 1 1 0.5" }
                }, InitialLayer + $".RPHolder.{elementId}"); 
                
                if (canGet)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0.16", OffsetMin = "-1 0", OffsetMax = "1 0"},
                        Button = {Color = "0.33 0.415 0.192 1" },   
                        Text = { Text = $"ЗАБРАТЬ".ToUpper(), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 18, Color = "0.815 0.776 0.741" }
                    }, InitialLayer + $".RPHolder.{elementId}");  
                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax              = "1 0.16", OffsetMin = "-1 0", OffsetMax = "1 0"},
                        Button        = {Color     = "0.33 0.415 0.192 1", Command = $"{HandlerCommand} parts get {check.Id}", Close = InitialLayer + $".RPHolder.{elementId}.Visual" },   
                        Text          = { Text     = $"ЗАБРАТЬ".ToUpper(), Align   = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 18, Color = "0.815 0.776 0.741" }
                    }, InitialLayer + $".RPHolder.{elementId}", InitialLayer + $".RPHolder.{elementId}.Visual");   
                }

                elementId++;
                if (elementId == 4)
                {
                    elementId = 0;

                    topMargin -= pHeight + 5;
                }
            }

            CuiHelper.AddUi(player, container);
            
        }

        private void InitializeMurders(BasePlayer player, DPlayer dPlayer)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, InitialLayer + ".RP");
            
            container.Add(new CuiPanel()
            { 
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"},  
                Image         = {Color = "0 0 0 0" }
            }, InitialLayer + ".R", InitialLayer + ".RP");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -100", OffsetMax = $"-30 -30"} ,
                Text = { Text = "НАЁМНИКИ", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 48, Color = "0.93 0.89 0.85 1"}
            }, InitialLayer + ".RP", InitialLayer + ".UP"); 
            
            UpdateExpCounter(player, ExpAction.None, dPlayer.Exp);

            float margin = 0;
            foreach (var check in dPlayer.Murders.OrderBy(p => p.Level))
            {
                if (check.Level == 4 && !dPlayer.Free)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = $"0 {-105 - margin}", OffsetMax = $"0 {10 - margin}" },
                        Image         = { Color     = "0.545 0.274 0.231 0.6", Material = "" }   
                    }, InitialLayer + ".UP", InitialLayer + ".UP" + margin / 115);
                
                    /*container.Add(new CuiElement
                    {
                        Parent = InitialLayer + ".UP" + margin / 115,
                        Components =
                        {
                            new CuiRawImageComponent { Png            = LockerImage, Color = "1 1 1 0.1"},
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                        }
                    });*/
                }
                else
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = $"0 {-105 - margin}", OffsetMax = $"0 {10 - margin}" },
                        Image         = { Color     = "0.815 0.776 0.741 0.2" }
                    }, InitialLayer + ".UP", InitialLayer + ".UP" + margin / 115);
                }
                
                container.Add(new CuiElement
                {
                    Parent = InitialLayer + ".UP" + margin / 115,
                    Name = InitialLayer + ".UP" + margin / 115 + ".Avatar",
                    Components =
                    {
                        new CuiRawImageComponent { Png = check.ImageID },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMax = "115 0" }
                    }
                });

                if (check.Level > dPlayer.Level && check.Level != 4)
                {
                    container.Add(new CuiElement 
                    {
                        Parent = InitialLayer + ".UP" + margin / 115,
                        Name = InitialLayer + ".UP" + margin / 115 + ".Avatar",
                        Components =
                        {
                            new CuiRawImageComponent { Png = LockerImage, Color = "0 0 0 1"},
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMax = "115 0" }
                        }
                    });
                }

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMax = "0 15" },
                    Image = { Color = "0.105 0.117 0.094 0.9" }
                }, InitialLayer + ".UP" + margin / 115 + ".Avatar", InitialLayer + ".UP" + margin / 115 + ".Name");
                
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = check.Name, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 10 }
                }, InitialLayer + ".UP" + margin / 115 + ".Name");

                var timeSpan = TimeSpan.FromSeconds(Settings.TaskPrice[check.Level].Length);
                var text = "";
                if (timeSpan.Hours > 0) text = timeSpan.Hours + " час.";
                if (timeSpan.Minutes > 0) text += " " + timeSpan.Minutes + " мин.";
                if (timeSpan.Seconds > 0) text += " " + timeSpan.Seconds + " сек.";
				
				if (check.Level == 4)
                {
                    text = "от 10 до 120 минут";
                }
 
                string stats = $"Уровень: <b>{GetNameFromLevel(check.Level)}</b>\nДлительность вылазки: <b>{text}</b>\nЦена за одну вылазку: <b>{Settings.TaskPrice[check.Level].ExpCost}EXP</b>";
                if (check.Level == 4)
                    stats = $"{(check.Level == 4 ? "<b>Ежедневный бонус</b>" : $"Уровень: <b>{GetNameFromLevel(check.Level)}</b>")}\nДлительность открытия: <b>{text}</b>\n{(!dPlayer.Free ? "Бесплатное открытие <b>раз в день</b>" : $"Следущее открытие будет доступно после <b>3:30</b> по МСК")}";
                
                container.Add(new CuiLabel
                { 
                    RectTransform = { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "10 0", OffsetMax = "500 -5" },
                    Text = { Text = stats, Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf", FontSize = 13, Color = "0.815 0.776 0.741"}
                }, InitialLayer + ".UP" + margin / 115 + ".Avatar");

                double fullLength = 0;
                double itemAmount = 0;

                double visibleItems = 0;
                double oneItemTime = 0;
                if (dPlayer.Quest != null)
                {
                    fullLength = (dPlayer.Quest.TimeEnd / 1000 - dPlayer.Quest.TimeStart / 1000);
                    itemAmount = dPlayer.Quest.Items.Count;

                    visibleItems = Math.Min(itemAmount + 1, 5);
                    oneItemTime = fullLength / visibleItems;
                }
                
                float secondMargin = 0;
                for (int i = 0; i < 5; i++)
                {
                    container.Add(new CuiPanel
                    { 
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = $"{125 + secondMargin} 7.5", OffsetMax = $"{175 + secondMargin} 57.5" },
                        Image = { Color = "0.129 0.129 0.129 0.8" }
                    }, InitialLayer + ".UP" + margin / 115, InitialLayer + ".UP" + margin / 115 + i);

                    if (dPlayer.Quest != null && dPlayer.Quest.Murder.Id == check.Id)
                    {
                        var thisItemFindTime = dPlayer.Quest.TimeStart / 1000 + oneItemTime * (i + 1);
                        var prevItemFindTime = thisItemFindTime - oneItemTime;
                        
                        if (thisItemFindTime < Time() || (thisItemFindTime > dPlayer.Quest.TimeEnd / 1000))
                        {
                            var item = dPlayer.Quest.Items.ElementAtOrDefault(i);
                            if (item != null)
                            {
                                if (item.IsBlueprint)
                                {
                                    container.Add(new CuiElement
                                    {
                                        Parent = InitialLayer + ".UP" + margin / 115 + i,
                                        Components = 
                                        {
                                            new CuiRawImageComponent { Png = BlueprintImage }, 
                                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" }
                                        } 
                                    });
                                }
                                container.Add(new CuiElement 
                                {  
                                    Parent = InitialLayer + ".UP" + margin / 115 + i,
                                    Components = 
                                    {
                                        new CuiRawImageComponent { Png = item.GetImage() },
                                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" }
                                    }
                                });
                            }
                            else if (Time() > dPlayer.Quest.TimeEnd / 1000)
                            {
                                container.Add(new CuiLabel
                                { 
                                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"0 0", OffsetMax = $"0 0" },
                                    Text = { Text = "НЕ\nНАЙДЕН", Color = "0.129 0.129 0.129 1", FontSize= 11, Align = TextAnchor.MiddleCenter}
                                }, InitialLayer + ".UP" + margin / 115 + i);
                            }
                            else
                            {
                                container.Add(new CuiButton
                                { 
                                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"10 10", OffsetMax = $"-10 -10" },
                                    Button = { Color = "0.129 0.129 0.129 0.95", Sprite = "assets/icons/picked up.png" },
                                    Text = { Text = "" }
                                }, InitialLayer + ".UP" + margin / 115 + i);
                            }
                        }
                        else 
                        {
                            container.Add(new CuiButton
                            { 
                                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"10 10", OffsetMax = $"-10 -10" },
                                Button = { Color = "0.129 0.129 0.129 0.95", Sprite = Time() > prevItemFindTime  ? "assets/icons/stopwatch.png" : "assets/icons/picked up.png" },
                                Text = { Text = "" }
                            }, InitialLayer + ".UP" + margin / 115 + i);

                            var endFindTime = dPlayer.Quest.TimeStart / 1000 + oneItemTime * (i + 1);
                            var lookingTime = endFindTime - Time(); 
                            
                            container.Add(new CuiPanel
                            {
                                RectTransform = { AnchorMin = "0 0", AnchorMax = $"1 {Math.Min(1 - lookingTime / oneItemTime, 1)}", OffsetMax = "0 0" },
                                Image = { Color = "0.762 0.79 0.57 0.2"}
                            }, InitialLayer + ".UP" + margin / 115 + i);
                        }
                    }
                    else
                    {
                        container.Add(new CuiButton
                        { 
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"10 10", OffsetMax = $"-10 -10" },
                            Button = { Color = "0.129 0.129 0.129 0.9", Sprite = "assets/icons/picked up.png" },
                            Text = { Text = "" }
                        }, InitialLayer + ".UP" + margin / 115 + i); 
                    }

                    secondMargin += 55;
                }

                string buttonColor = "0.462 0.49 0.27";
                string buttonText = check.Level == 4 ? "<b>ОТКРЫТЬ</b>" : "<b>НАНЯТЬ</b>";
                string buttonCommand = $"{HandlerCommand} fury create {check.Level}"; 

                if (dPlayer.Exp < Settings.TaskPrice[check.Level].ExpCost)
                {
                    buttonColor = "0.552 0.278 0.231";
                    buttonText = "НЕДОСТАТОЧНО\nОПЫТА";
                    buttonCommand = $""; 
                }
                if ((check.Level > dPlayer.Level || dPlayer.Quest != null) && (check.Level != 4 || dPlayer.Free))
                {
                    buttonColor = "0.294 0.274 0.254";
                    buttonText = "НЕ ДОСТУПНО";
                    buttonCommand = $"";
                } 
 
                if (dPlayer.Quest != null && dPlayer.Quest.Murder.Id == check.Id)
                {
                    buttonColor = "0.462 0.49 0.27";
                    buttonText = !dPlayer.Quest.IsEnd() ? $"ПОИСК\n{dPlayer.Quest.TimeLeftText()}" : "ЗАБРАТЬ\nВ ЯЩИК"; 
                    buttonCommand = $"{HandlerCommand} fury finish"; 
                } 
                if (dPlayer.Quest == null && check.Level == 4 && dPlayer.Free)
                {
                    buttonColor = "0.294 0.274 0.254";
                    buttonText = "НЕ ДОСТУПНО";
                    buttonCommand = $"";   
                }
                
                container.Add(new CuiPanel
                {  
                    RectTransform = { AnchorMin = "1 0", AnchorMax      = "1 0", OffsetMin = $"-120 7.5", OffsetMax = $"-7.5 57.5" },
                    Image = { Color = "0 0 0 0" }
                }, InitialLayer + ".UP" + margin / 115, InitialLayer + ".UP" + margin / 115 + ".B");
                
                container.Add(new CuiPanel
                {  
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Image         = { Color     = buttonColor }
                }, InitialLayer + ".UP" + margin / 115 + ".B");
                
                
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax  = "1 1", OffsetMax              = "0 0" },
                    Text          = { Text      = buttonText, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 14, Color = "0.815 0.776 0.741" }
                }, InitialLayer + ".UP" + margin / 115 + ".B");
                  
                container.Add(new CuiButton
                {  
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0", Material = "", Close = InitialLayer + ".UP" + margin / 115 + ".B.Vis", Command = buttonCommand},
                    Text = { Text = "" } 
                }, InitialLayer + ".UP" + margin / 115 + ".B", InitialLayer + ".UP" + margin / 115 + ".B.Vis");
                
                margin += 125; 
            }
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMax = "0 125" },
                Image = { Color = "0.29411 0.27450 0.254901 1" }
            }, InitialLayer + ".R", InitialLayer + ".H");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"28 -100", OffsetMax = $"-30 -5"} ,
                Text = { Text = "ИНФОРМАЦИЯ", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 48, Color = "0.93 0.89 0.85 1"}
            }, InitialLayer + ".H");

            string helpText = "В основном наёмники будут приносить рецепты, чем дороже наёмник — тем лучше рецепты он может принести. Изначально для найма доступен только первый уровень наёмника, чтобы разблокировать второй — нужно нанять первого и так далее. Набор наёмников обновляется каждый месяц (exp не вайпается).";
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "30 0", OffsetMax = "0 -60" },
                Text = { Text = helpText, Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf", FontSize = 13, Color = "0.67 0.63 0.596"}
            }, InitialLayer + ".H");

            CuiHelper.AddUi(player, container);
        }
        
        private void InitializeLayers(BasePlayer player, string currentSection)
        {
            CuiHelper.DestroyUi(player, InitialLayer);
            CuiElementContainer container = new CuiElementContainer();
            
            container.Add(new CuiPanel() 
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1.43 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                Image = {Color = "0 0 0 0"}
            }, "UI_RustMenu_Internal", InitialLayer);
                    
            container.Add(new CuiPanel()
            { 
                CursorEnabled = true,
                RectTransform = {AnchorMin = "-0.04 0", AnchorMax = "0.18 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                Image         = {Color = "0.549 0.270 0.215 0.7", Material = "" }
            }, InitialLayer, InitialLayer + ".C");
            
            container.Add(new CuiPanel()
            { 
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 -0.05", AnchorMax = "1 0.5", OffsetMin = "0 0", OffsetMax = "0 0"},
                Image         = {Color = "0 0 0 0.9", Sprite = "assets/content/ui/ui.gradient.up.psd"}
            }, InitialLayer + ".C");
                                     
            container.Add(new CuiPanel()
            { 
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0.3", OffsetMin = "0 0", OffsetMax = "0 0"},
                Image         = {Color = "0 0 0 0.2", Sprite = "assets/content/ui/ui.background.transparent.radial.psd"}
            }, InitialLayer + ".C");
                 
            

            if (!SyncCooldowns.ContainsKey(player.userID) || SyncCooldowns[player.userID] < Time() || player.IsAdmin )
            {
                
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "5 20", OffsetMax = "-5 120" },
                    Button         = {Color = "1 1 1 0", Command = HandlerCommand + " sync" },
                    Text = { Text = "<b><size=22>НАЖМИ СЮДА</size></b>\nДЛЯ СИНХРОНИЗАЦИИ С БД\n<size=8>(В СЛУЧАЕ ЕСЛИ ВОЗНИКЛИ ПРОБЛЕМЫ)</size>", Color = "0.929 0.882 0.847 0.05", Align = TextAnchor.LowerCenter, Font = "robotocondensed-regular.ttf", FontSize =  14}
                }, InitialLayer + ".C");
            }     
            
            float topPosition = (Sections.Count / 2f * 40 + (Sections.Count - 1) / 2f * 5);
            foreach (var vip in Sections) 
            { 
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0.5", AnchorMax = "1 0.51", OffsetMin = $"0 {topPosition - 40}", OffsetMax = $"0 {topPosition}" },
                    Button = { Color = currentSection == vip.Key ? "0.149 0.145 0.137 0.8" : "0 0 0 0", Command = $"{HandlerCommand} {vip.Key}"},
                    Text = { Text = "", Align = TextAnchor.MiddleRight, Font = "robotocondensed-bold.ttf", FontSize = 14 }
                }, InitialLayer + ".C", InitialLayer + vip.Value); 
                
                container.Add(new CuiLabel 
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"0 0", OffsetMax = $"-20 0" },
                    Text = { Text = vip.Value.ToUpper().Replace("_", " "), Align = TextAnchor.MiddleRight, Font = "robotocondensed-bold.ttf", FontSize = 24, Color = "0.929 0.882 0.847 1"}
                }, InitialLayer + vip.Value);
                 
                topPosition -= 40 + 5;
            }
                
            container.Add(new CuiPanel()
            { 
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0.18 0", AnchorMax = "0.665 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                Image         = {Color = "0.117 0.121 0.109 0.95" } 
            }, InitialLayer, InitialLayer + ".R");
                
                

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Utils

        private static string GetNameFromLevel(int index)
        {
            switch (index)
            {
                case 1: return "первый";
                case 2: return "второй";
                case 3: return "третий";
                case 4: return "четвертый";
            }

            return "неизвестный";
        }

        private static double Time() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

        #endregion
    }
}