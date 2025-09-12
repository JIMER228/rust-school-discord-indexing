﻿﻿using System;
using System.Collections.Generic;
using System.Linq;
  using Facepunch.Extend;
  using Newtonsoft.Json;
  using Oxide.Core;
  using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
  using Rust;
  using UnityEngine;
  using UnityEngine.XR;
  using Color = UnityEngine.Color; 

namespace Oxide.Plugins
{
    [Info("Detonator", "Hougan", "0.0.1")]
    [Description("Плагин телепортации на основе детонатора")]
    public class Detonator : RustPlugin 
    {
        #region Classes

        private class DataHandler
        {
            public Dictionary<ulong, HomeManager> Managers = new Dictionary<ulong, HomeManager>();

            public Home FindHomeByFrequency(BasePlayer player, string frequency)
            {
                if (!Managers.ContainsKey(player.userID))
                    Managers.Add(player.userID, new HomeManager()); 
                
                var obj = Managers[player.userID];
                foreach (var check in obj.Homes)
                {
                    if (check.Value.Frequency == frequency)
                        return check.Value;
                }

                return null;
            }

            public void SaveData()
            {
                Interface.Oxide.DataFileSystem.WriteObject("Detonator", this); 
            } 
            
            public static DataHandler LoadData()
            {
                DataHandler data = Interface.Oxide.DataFileSystem.ReadObject<DataHandler>("Detonator");
                if (data == null) data = new DataHandler();
                
                data.Managers.ToList().ForEach(p => p.Value.LoadHome());

                return data;
            } 
        }
        
        private class HomeManager
        {
            public Dictionary<int, Home> Homes = new Dictionary<int, Home>();

            public int GetNewHomeId()
            {
                int i = 1;

                while (Homes.ContainsKey(i))
                    i++;

                return i;
            }
            public void LoadHome()
            {
                var dirty = new List<int>();
                
                foreach (var home in Homes)
                {
                    var ent = BaseEntity.saveList.FirstOrDefault(p=> p.net.ID == home.Value.NetID);
                    if (ent == null || ent.IsDestroyed || !(ent is SleepingBag))
                        dirty.Add(home.Key);

                    home.Value.Entity = ent as BaseEntity; 
                }

                foreach (var check in dirty)
                    Homes.Remove(check);
            }
        }

        private class Home
        {
            [JsonIgnore] public BaseEntity Entity;
            
            public uint NetID; 
            
            public string Frequency;
            public bool IsMain;

            public bool IsBed() => Entity.PrefabName.Contains("bed");
			public bool IsBag() => Entity.PrefabName.Contains("sleeping");
        }

        private class Configuration
        {
            [JsonProperty("Длительность телепорта на спальник/кровать")]
            public Dictionary<string, Tuple<int, int>> TeleportLength = new Dictionary<string, Tuple<int, int>>();
            [JsonProperty("Длительность перезарядки после телепортации на спальник/кровать")]
            public Dictionary<string, Tuple<int, int>> CooldownLength = new Dictionary<string, Tuple<int, int>>();
            [JsonProperty("Количество домов в зависимости от привилегии")]
            public Dictionary<string, int> HomesAmount = new Dictionary<string, int>();
            
            
            [JsonProperty("Процент поломки прибора от телепорта")]
            public float LoseCondition = 0.25f;  
            [JsonProperty("Процент дохода комарика")]
            public float Percent = 0.1f;  

            public static Configuration Generate()
            {
                return new Configuration
                {  
                    HomesAmount = new Dictionary<string, int>
                    {
                        ["Detonator.Default"] = 1,
                        ["Detonator.Vip"] = 2,
						["Detonator.VipPlus"] = 3
                    },
                    TeleportLength = new Dictionary<string, Tuple<int, int>>
                    {
                        ["Detonator.Default"] = new Tuple<int, int>(15, 15)
                    },
                    CooldownLength = new Dictionary<string, Tuple<int, int>>
                    {
                        ["Detonator.Default"] = new Tuple<int, int>(300, 360)
                    }
                };
            }
        }

        private class RFUser : MonoBehaviour
        {
            private BasePlayer Player;
            public bool IsActivated;
            public Status Status; 
            private bool IsBlocked = false;
            public int Cooldown;

            public void Awake()
            {
                Player = GetComponent<BasePlayer>();
                InvokeRepeating(nameof(DecreaseCooldown), 1f, 1f); 
            }
            
            public void TryStart()
            {
                Status = new Status();
                if (!Status.CheckDetonator(Player)) return;
                if (Player.IsSwimming()) return;
                if (Player.metabolism.bleeding.value > 0)
                {
                    Player.ChatMessage($"<color=#c43d2a>Кровотечение</color> замедляет телепортацию!");
                }

                if ((bool) _.RaidBlock.Call("IsInRaid", Player))
                {
                    Player.ChatMessage($"Телепортация при <color=#e6533d>рейд блоке</color> невозможна!");
                }   
                
                Status.CheckFrequency(Player); 
                
                InvokeRepeating(nameof(ControlUpdate), 0, 0.05f);
                InvokeRepeating(nameof(CheckCupboard), 0f, 0.25f); 
                InvokeRepeating(nameof(CheckBlocked), 0f, 1f);
            }

            public void CheckBlocked()
            {
                if (Cooldown > 0) return;
                
                IsBlocked = (bool) _.RaidBlock.Call("IsInRaid", Player);
                //if (!IsBlocked && Status.Home != null)
                //    IsBlocked = (bool) _.RaidBlock.Call("IsInRaid", Status.Home.Entity.transform.position);

                if (IsBlocked) Status.Color = "1 0.5 0.5 0.5"; 
            }
   
            public void CheckCupboard()
            {
                if (IsBlocked || Cooldown > 0) return;
                 
                Status.Color = Status.CheckCupboard(Player) ? "0.5 1 0.5 0.5" : "1 0.5 0.5 0.5";
                    
                if (Player.IsSwimming())
                {
                    ManualStop();
                    return;
                }
            }
            
            public void ManualStop()
            {
                IsActivated = false;
                CuiHelper.DestroyUi(Player, Layer);
                
                CancelInvoke(nameof(ControlUpdate));
                CancelInvoke(nameof(CheckCupboard)); 
                CancelInvoke(nameof(CheckBlocked)); 
            }

            public void DecreaseCooldown()
            {
                if (Cooldown > 0)
                {
                    Cooldown--;   
                }
            }
 
            public void ControlUpdate() 
            { 
                Status.Length += 0.05f; 
                
                if (!Player.serverInput.IsDown(BUTTON.FIRE_PRIMARY) && !Player.serverInput.WasJustPressed(BUTTON.FIRE_PRIMARY))
                {
                    ManualStop();
                    return;
                }

                if (Status.Length >= Status.MaxLength && Cooldown == 0)
                {
                    ManualStop();
                    TryTeleport();
                    return;
                }
                
                if (!IsActivated)
                {
                    if (Math.Abs(Status.Length - 0.4f) < 0.1f)
                    {
                        CuiHelper.DestroyUi(Player, Layer);
                        CuiElementContainer initialContainer = new CuiElementContainer();

                        initialContainer.Add(new CuiPanel
                        {
                            RectTransform = {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-50 -5", OffsetMax = "50 5"},
                            Image         = {Color     = "1 1 1 0.2"} 
                        }, "Overlay", Layer);

                        CuiHelper.AddUi(Player, initialContainer);
                        IsActivated   = true;
                        Status.Length = 0f;  
                    }

                    return;
                }

                if (Cooldown > 0)
                {
                    CuiHelper.DestroyUi(Player, Layer + ".Update");
                    CuiHelper.DestroyUi(Player, Layer + ".Text");
                    CuiElementContainer container = new CuiElementContainer();

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = $"1 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                        Image         = { Color     = "1 0.5 0.5 0.5" }
                    }, Layer, Layer + ".Update");
                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = $"1 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                        Text         = { Text = $"ПЕРЕЗАРЯДКА", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 8, Color = "1 1 1 0.8" }
                    }, Layer + ".Update");
  
                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax                                                  = $"0 1", OffsetMin            = "-300 -1", OffsetMax                     = "-2 0"},
                        Text          = { Text      = $"{Cooldown} СЕК.", Align = TextAnchor.MiddleRight, Font = "robotocondensed-regular.ttf", FontSize = 9, Color = "1 1 1 0.8" }
                    }, Layer, Layer + ".Text");  

                    CuiHelper.AddUi(Player, container); 
                }
                else
                {
                    CuiHelper.DestroyUi(Player, Layer + ".Update");
                    CuiHelper.DestroyUi(Player, Layer + ".Text");
                    CuiElementContainer container = new CuiElementContainer();

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = $"1 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                        Image         = { Color     = "1 0.5 0.5 0" }
                    }, Layer, Layer + ".Update");
                    
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = $"{(float) Status.Length / Status.MaxLength} 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                        Image         = { Color     = Status.Color }
                    }, Layer + ".Update");
                    
                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax       = $"1 1", OffsetMin             = "0 0", OffsetMax                        = "0 0"},
                        Text          = { Text      = $"ТЕЛЕПОРТАЦИЯ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 8, Color = "1 1 1 0.8" }
                    }, Layer + ".Update");
  
                    container.Add(new CuiLabel 
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax                                                  = $"0 1", OffsetMin            = "-20 -1", OffsetMax                     = "-2 0"},
                        Text          = { Text      = $"{((Status.Length / Status.MaxLength) * 100):F0}%", Align = TextAnchor.MiddleRight, Font = "robotocondensed-regular.ttf", FontSize = 9, Color = "1 1 1 0.8" }
                    }, Layer, Layer + ".Text");  
                    
                    CuiHelper.AddUi(Player, container);
                }
            }

            public void TryTeleport()
            {
                if (!Status.CheckCupboard(Player)) return;
                if (IsBlocked) return;
                if (!Status.CheckDetonator(Player)) return;
                if (Player.IsSwimming()) return;

                if (Player.HasParent())
                {
                    Player.ChatMessage("Вы <color=#eb7d6a>не можете</color> телепортироваться с водной мусорки!");
                    return;
                }
                
                Status.CheckFrequency(Player);

                if (Status.Home != null)
                {
                    Cooldown = GetMinimalCooldown(Player, Status.Home.IsBed()); 
                    ClearTeleport(Player, Status.Home.Entity.transform.position + new Vector3(0, 0.5f, 0));
                }
            }
        } 

        private class Status
        {
            public float Length; 
            public Home Home;
            public Item Detonator;
            public string Color = "1 1 1 0.2";
            public int MaxLength;

            public bool CheckDetonator(BasePlayer player)
            { 
                Detonator = player.GetActiveItem();
                
                return Detonator != null && !Detonator.isBroken && Detonator.info.shortname == "rf.detonator";
            }
            public bool CheckCupboard(BasePlayer player) => !player.IsBuildingBlocked() && Home != null;
            public void CheckFrequency(BasePlayer player)
            {
                var freq = Detonator.GetHeldEntity()?.GetComponent<global::Detonator>().frequency ?? -1;
                
                Home         = Handler.FindHomeByFrequency(player, freq.ToString());
                Color = Home == null ? "1 0.5 0.5 0.5" : "0.5 1 0.5 0.5";
                MaxLength = Home == null ? 5 : GetMinimalTeleport(player, Home.IsBed());
            }
        }
        
        #endregion

        #region Variables

        [PluginReference] private Plugin RaidBlock;
        private static Detonator _;
        private static DataHandler Handler;
        private static string Layer = "UI_DetonatorPressLayer";
        private static Configuration Settings;
        private static Hash<ulong, RFUser> PressStatuses = new Hash<ulong, RFUser>();

        #endregion

        #region API

        private int GetFrequencyForPlayer(BasePlayer player)
        {
            if (!Handler.Managers.ContainsKey(player.userID)) return 0; 
            
            var info = Handler.Managers[player.userID];
            
            var item = info.Homes.FirstOrDefault(p => p.Value.IsMain);
            if (item.Value == null) return 1;

            return int.Parse(item.Value.Frequency);
        }

        #endregion

        #region Commands

        [ConsoleCommand("UI_DetonatorHandler")]
        private void CmdConsoleHandler(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!player || !arg.HasArgs(1)) return;

            switch (arg.Args[0].ToLower())
            {
                case "sethome":
                {
                    uint index = uint.Parse(arg.Args[1]);
                    var obj = Handler.Managers[player.userID];

                    var entity = BaseNetworkable.serverEntities.Find(index) as BaseEntity;
                    if (entity == null || entity.IsDestroyed) return;
                    
                    
                    if (Handler.Managers[player.userID].Homes.Any(p =>
                        Vector3.Distance(p.Value.Entity.transform.position, entity.transform.position) < 30))
                    {
                        player.ChatMessage("В этом доме уже есть дом!");
                        return;
                    }
                    
                    string frequency = Oxide.Core.Random.Range(10, 54).ToString();
                    while (obj.Homes.Any(p => p.Value.Frequency == frequency))
                        frequency = Oxide.Core.Random.Range(10, 54).ToString();

                    var slObj = entity.GetComponent<SleepingBag>();
                    slObj.niceName = $"HOME: {frequency}Hz";
                    slObj._name = $"HOME: {frequency}Hz";
                    slObj.SendNetworkUpdate(); 
            
                    obj.Homes.Add(obj.GetNewHomeId(), new Home
                    {  
                        Entity = entity,
                        Frequency = frequency, 
                        IsMain = obj.Homes.Count == 0 || obj.Homes.All(p => !p.Value.IsMain),
                        NetID = entity.net.ID
                    });

                    var item = player.inventory.AllItems().FirstOrDefault(p => p.info.shortname.Contains("detonator"));
                    if (item != null)
                    {
                        item.GetHeldEntity().GetComponent<global::Detonator>().frequency = int.Parse(frequency);
                        item.GetHeldEntity().SendNetworkUpdate();
                         
                        player.inventory.SendUpdatedInventory(PlayerInventory.Type.Belt, player.inventory.containerBelt);
                    }
                    
                    player.ChatMessage($"Вы <color=#accc7a>успешно</color> установили точку телепортации!\n<size=10>Чтобы телепортироваться возьмите <color=#D3D3D3>пульт</color> в руку и зажмите ЛКМ.</size>");
                    break;
                }
                case "remove":
                {
                    int index = int.Parse(arg.Args[1]);

                    if (!Handler.Managers[player.userID].Homes.ContainsKey(index)) return;
 
                    bool shouldMain = Handler.Managers[player.userID].Homes[index].IsMain;
                     
                    Handler.Managers[player.userID].Homes[index].Entity.GetComponent<SleepingBag>().niceName = "REMOVED";
                    Handler.Managers[player.userID].Homes[index].Entity.GetComponent<SleepingBag>()._name = "REMOVED";
                    Handler.Managers[player.userID].Homes[index].Entity.GetComponent<SleepingBag>().SendNetworkUpdate();
                     
                    Handler.Managers[player.userID].Homes.Remove(index);

                    if (Handler.Managers[player.userID].Homes.Count > 0 && shouldMain)
                        Handler.Managers[player.userID].Homes.FirstOrDefault().Value.IsMain = true;
                    
                    InitializeInterface(player);
                    break;
                }
                case "main":
                {
                    int index = int.Parse(arg.Args[1]);

                    if (!Handler.Managers[player.userID].Homes.ContainsKey(index)) return;

                    foreach (var check in Handler.Managers[player.userID].Homes)
                        check.Value.IsMain = false;
                    
                    Handler.Managers[player.userID].Homes[index].IsMain = true;
                    
                    var item = player.inventory.AllItems().FirstOrDefault(p => p.info.shortname.Contains("detonator"));
                    if (item == null) return;

                    if (item.GetHeldEntity() == null) return;
                    
                    item.GetHeldEntity().GetComponent<global::Detonator>().frequency = GetFrequencyForPlayer(player); 
                    
                    InitializeInterface(player); 
                    break;
                }
            }
        }

        [ChatCommand("tpa")]
        private void CmdChatTpa(BasePlayer player, string command, string[] args) => ReplyWithHelp(player);
        [ChatCommand("tpr")] 
        private void CmdChatTpr(BasePlayer player, string command, string[] args) => ReplyWithHelp(player);
        [ChatCommand("home")]
        private void CmdChatHome(BasePlayer player, string command, string[] args) => ReplyWithHelp(player);
        [ChatCommand("sethome")]
        private void CmdChatSetHome(BasePlayer player, string command, string[] args) => ReplyWithHelp(player);

        private void ReplyWithHelp(BasePlayer player) => player.ChatMessage($"О нашей системе телепортации вы можете узнать в меню, перейдя в раздел <<color=#accc7a>информация</color>>.");

        #endregion

        #region Initialization

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
        private void OnServerInitialized()  
        {
            _ = this;

            Handler = DataHandler.LoadData();
            
            plugins.Find("ImageLibrary").Call("AddImage", "https://i.imgur.com/v2iJZcT.png", "SleepingIcon");
            plugins.Find("ImageLibrary").Call("AddImage", "https://i.imgur.com/GlOFGgh.png", "BedIcon");
            
            Settings.TeleportLength.ToList().ForEach(p => permission.RegisterPermission(p.Key, this));
            Settings.CooldownLength.ToList().ForEach(p => permission.RegisterPermission(p.Key, this));
            Settings.HomesAmount.ToList().ForEach(p => permission.RegisterPermission(p.Key, this));
            
            timer.Once(1, () => { BasePlayer.activePlayerList.ToList().ForEach(OnPlayerConnected); });
            timer.Every(30, Handler.SaveData);
        }
		
		private void OnPlayerRespawn(BasePlayer player)
        {
            if (!Handler.Managers.ContainsKey(player.userID))
            {
                return;
            }

            Handler.Managers[player.userID].LoadHome();
        }

        private void Unload()
        {  
            Handler.SaveData(); 
            DestroyAll<RFUser>(); 
        }
 
        #endregion

        #region Hooks  
        
        private object CanAssignBed(BasePlayer player, SleepingBag bag, ulong targetPlayerId)
        {
            if (bag.niceName.Contains("HOME"))
            {
                player.ChatMessage($"Вы <color=#eb7d6a>не можете</color> передать свою точку телепортации!");
                return false;
            }
            
            return null;
        }
        
        private object CanRenameBed(BasePlayer player, SleepingBag bed, string bedName)
        {
            if (bed.niceName.Contains("HOME")) return false;
            
            return null;
        }

        private void CanPickupEntity(BasePlayer player, BaseEntity entity)
        {
            if (entity is SleepingBag)
            {
                if (Handler.Managers.ContainsKey(entity.OwnerID))
                {
                    var home = Handler.Managers[entity.OwnerID].Homes.FirstOrDefault(p => p.Value.NetID == entity.net.ID);
                    if (home.Value == null) return;

                    Handler.Managers[entity.OwnerID].Homes.Remove(home.Key);
                }
            }
        }
        
        private bool? OnRecycleItem(Recycler recycler, Item item)
        {
            if (item.info.shortname.Contains("detonator"))
            {
                recycler.MoveItemToOutput(item);
                return false;
            }

            return null;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is SleepingBag)
            {
                if (Handler.Managers.ContainsKey(entity.OwnerID))
                {
                    var home = Handler.Managers[entity.OwnerID].Homes.FirstOrDefault(p => p.Value.NetID == entity.net.ID);
                    if (home.Value == null) return;

                    Handler.Managers[entity.OwnerID].Homes.Remove(home.Key);
                }
            }
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (!(entity is BasePlayer)) return;

            var obj = entity.GetComponent<RFUser>();
            if (obj == null) return;

            if (obj.IsActivated)
            {
                float onePercent = obj.Status.MaxLength / 100f;

                if (info.damageTypes.GetMajorityDamageType() == DamageType.Bleeding)
                {
                    obj.Status.Length -= onePercent * info.damageTypes.Total() * 20;
                }
                else
                {
                    obj.Status.Length -= onePercent * info.damageTypes.Total();
                }
                if (obj.Status.Length < 0) obj.Status.Length = 0; 
            }
        } 

        private void OnEntityBuilt(Planner planner, GameObject gameobject)
        {
            if (planner == null || gameobject == null) return;
            
            var        player = planner.GetOwnerPlayer();
            BaseEntity entity = gameobject.ToBaseEntity();
            if (entity == null) return; 

            if (!entity.PrefabName.Contains("sleeping") && !entity.PrefabName.Contains("bed") && !entity.PrefabName.Contains("beachtowel")) return;

            var maxAmount = GetMaximumHomes(player, true);
            var obj = Handler.Managers[player.userID];

            List<BuildingBlock> blocks = new List<BuildingBlock>();
            Vis.Entities(entity.transform.position, entity.PrefabName.Contains("sleeping") ? 0.1f : 0.3f, blocks);

            if (blocks.Count == 0) return;

            if (Handler.Managers[player.userID].Homes.Any(p =>
                Vector3.Distance(p.Value.Entity.transform.position, entity.transform.position) < 30))
            {
                player.ChatMessage("В этом доме уже есть ваша точка телепортации!");
                return;
            }

            
            Handler.Managers[player.userID].LoadHome();
            if (obj.Homes.Count >= maxAmount)
            {
                player.ChatMessage($"У вас нет <color=#BDECB6>свободного слота</color> для установки ещё одной точки телепортации!");
                return;
            }
            try
            {
                Interface.Oxide.CallHook("AddNotification", player,
					$"СДЕЛАТЬ {(entity.PrefabName.Contains("sleeping") ? "СПАЛЬНИК" : (entity.PrefabName.Contains("beachtowel") ? "ПОЛОТЕНЦЕ" : "КРОВАТЬ"))} СЕТХОМОМ?",
                    $"НАЖМИТЕ ЧТОБЫ СОХРАНИТЬ ТОЧКУ ТЕЛЕПОРТАЦИИ", "1 1 1 0.5", 15, "", "",
                    $"UI_DetonatorHandler sethome {entity.net.ID}");


                Effect effect = new Effect("ASSETS/BUNDLED/PREFABS/FX/INVITE_NOTICE.PREFAB".ToLower(), player, 0,
                    new Vector3(), new Vector3());
                EffectNetwork.Send(effect, player.Connection);
            }
            catch
            {
                PrintError("TRY FIX ONENTITYBUILT FIX FAILED ON TRY CATCH");
            }
           
            /*string frequency = Oxide.Core.Random.Range(1000, 9999).ToString();
            while (obj.Homes.Any(p => p.Value.Frequency == frequency))
                frequency = Oxide.Core.Random.Range(1000, 9999).ToString();

            var slObj = entity.GetComponent<SleepingBag>();
            slObj.niceName = $"HOME: {frequency}Hz";
            slObj._name = $"HOME: {frequency}Hz";
            slObj.SendNetworkUpdate();
            
            obj.Homes.Add(obj.GetNewHomeId(), new Home
            {  
                Entity = entity,
                Frequency = frequency, 
                IsMain = obj.Homes.Count == 0 || obj.Homes.All(p => !p.Value.IsMain),
                NetID = entity.net.ID
            });*/
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (!Handler.Managers.ContainsKey(player.userID))
                Handler.Managers.Add(player.userID, new HomeManager());

            if (!PressStatuses.ContainsKey(player.userID))
            {
                PressStatuses.Add(player.userID, player.gameObject.AddComponent<RFUser>());
            }
            else
            {
                var obj = player.GetComponent<RFUser>();
                if (obj == null)
                {
                    obj = player.gameObject.AddComponent<RFUser>();
                }
                PressStatuses[player.userID] = obj;
            }
        }
        
        
        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (oldItem?.info.shortname == "rf.detonator")
            {
                PressStatuses[player.userID].ManualStop();
            }
        }
        
        private void OnPlayerInput(BasePlayer player, InputState state)
        {
            player.serverInput = state;
            
            if (state.WasJustPressed(BUTTON.FIRE_PRIMARY))
            {
                if (!PressStatuses.ContainsKey(player.userID)) return;
                
                PressStatuses[player.userID].TryStart();
                return; 
            }
        }
       
        #endregion

        #region Commands

        [ChatCommand("tpSecret")] 
        private void CmdTPCommandSecret(BasePlayer player, string command, string[] args) => InitializeInterface(player);

        #endregion

        #region Interface

        
        private string SettingsLayer = "UI_SettingsLayer";
        private void InitializeInterface(BasePlayer player)
        {
            var settings = Handler.Managers[player.userID];
            
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, SettingsLayer + ".RP");
        
            container.Add(new CuiPanel()
            { 
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"},  
                Image         = {Color = "0 0 0 0" }
            }, SettingsLayer + ".R", SettingsLayer + ".RP");
            
            CuiHelper.DestroyUi(player, SettingsLayer + ".RPP");
            container.Add(new CuiPanel()
            { 
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"},  
                Image         = {Color = "0 0 0 0" }
            }, SettingsLayer + ".RP", SettingsLayer + ".RPP");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 210" },
                Image = { Color = "0.29411 0.27450 0.254901 0.6" }
            }, SettingsLayer + ".R", SettingsLayer + ".Info");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -100", OffsetMax = $"-30 -5"} ,
                Text = { Text = "ИНФОРМАЦИЯ", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 48, Color = "0.93 0.89 0.85 1"}
            }, SettingsLayer + ".Info");

            string helpText = "При установке спальника/кровати на фундаменте - вам будет предложено сделать этот спальник/кровать точкой для телепортации. После установки точки - спальник будет назван рандомной частотой (частота основной точки телепортации будет <b>автоматически</b> установленна в каждом выдаваемом при спавне детонаторе)." +

                              "\n\nВы всегда можете поменять основную точку телепортации, нажав кнопку\n«<b>СДЕЛАТЬ ОСНОВНЫМ</b>» в этом меню." +

                              "\n\nУдалить точку для телепортации можно через экран смерти (удалив нужный спальник/кровать) или нажав «<b>УДАЛИТЬ</b>» в этом меню.";
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "30 0", OffsetMax = "-30 -60" },
                Text = { Text = helpText, Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "0.67 0.63 0.596"}
            }, SettingsLayer + ".Info");
            
             
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"30 -130", OffsetMax = $"500 -80"} , 
                Text = { Text = $"ДОСТУПНО ТОЧЕК ДОМА:  <b>{(GetMaximumHomes(player, true) - settings.Homes.Count)}</b>", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 20, Color = "0.81 0.77 0.74 0.6"}
            }, SettingsLayer + ".RPP");

            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"28 -100", OffsetMax = $"600 -30"} ,
                Text = { Text = "ТОЧКИ ТЕЛЕПОРТАЦИИ", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 48, Color = "0.93 0.89 0.85 1"}
            }, SettingsLayer + ".RPP", SettingsLayer + ".UP");

            settings.LoadHome();
            float margin = 0;
            for (int i = 0; i < 3; i++)
            {
                var cHome = settings.Homes.ElementAtOrDefault(i);
                string color = "";
                
                if (cHome.Value == null)
                {
                    color = "0.81 0.77 0.74 0.15";
                }
                else if (i + 1 > GetMaximumHomes(player, false))
                {
                    color = "0.81 0.77 0.74 0.15";
                }
                else
                {
                    if (cHome.Value.IsMain)
                    {
                        color = "0.46 0.49 0.27 0.5";
                    }
                    else
                    {
                        color = "0.81 0.77 0.74 0.25";
                    }
                }
                
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = $"0 {-90 - margin}", OffsetMax = $"0 {-10 - margin}" },
                    Image = { Color = color }
                }, SettingsLayer + ".UP", SettingsLayer + ".UP" + i);

//                string parent = SettingsLayer + ".UP" + i;
//                container.Add(new CuiElement
//                {
//                    Parent = parent,
//                    Components =
//                    {
//                        new CuiRawImageComponent {Png = (string) plugins.Find("ImageLibrary").Call("GetImage", cHome.Value == null || !cHome.Value.IsBed() ? "SleepingIcon" : "BedIcon"), Color = "1 1 1 0.3"},
//                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "25 0", OffsetMax = "223 62" }
//                    }
//                });

                string headerText = "ДОМ НЕ УСТАНОВЛЕН";
                if (cHome.Value != null) headerText = $"ЧАСТОТА: {cHome.Value.Frequency} GHz";
                if (i + 1 > GetMaximumHomes(player, false) && cHome.Value == null) headerText = "ДОПОЛНИТЕЛЬНАЯ ТОЧКА";
                
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "15 0", OffsetMax = "0 -10" },
                    Text = { Text = headerText, Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 30, Color = !(i + 1 > GetMaximumHomes(player, false)) ? "0.81 0.77 0.74 1" : "0.81 0.77 0.74 1" }
                }, SettingsLayer + ".UP" + i);
                
                /*
                 *
                 * 
            
            container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "30 -290", OffsetMax = "-40 -115" },
                    Image = { Color = "0.337 0.25 0.16 1" }
                }, SettingsLayer + ".RPP", SettingsLayer + ".Info");
            
            container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "-10 -14", OffsetMax = "14 10" },
                    Button = { Color = "0.93 0.89 0.85 0.7", Sprite = "assets/icons/connection.png" },
                    Text = { Text = "" }
                }, SettingsLayer + ".Info");

            string text =
                "При установке спальника/кровати в доме где вы авторизованы в шкафу - вам будет предложено сделать этот спальник/кровать точкой для телепортации. После установки точки - спальник будет назван рандомной частотой (частота первой точки телепортации будет <b>автоматически</b> установленна в каждом выдаваемом при спавне детонаторе)." +

            "\n\nВы всегда можете поменять основную точку телепортации, нажав кнопку\n«<b>СДЕЛАТЬ ОСНОВНЫМ</b>» в этом меню." +

                "\n\nУдалить точку для телепортации можно через экран смерти (удалив нужный спальник/кровать) или нажав «<b>УДАЛИТЬ</b>» в этом меню.";
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"15 0", OffsetMax = $"-10 -10"} , 
                Text = { Text = text, Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf", FontSize = 14, Color = "0.81 0.77 0.74 1"}
            }, SettingsLayer + ".Info");
                 */

                string subText = "УСТАНОВИТЕ СПАЛЬНИК ИЛИ КРОВАТЬ";
                if (cHome.Value != null) subText = "ТИП: " + (cHome.Value.IsBed() ? "КРОВАТЬ" : (cHome.Value.IsBag() ? "СПАЛЬНИК" : "ПОЛОТЕНЦЕ")) + $" — <b>{GridReference(cHome.Value.Entity.transform.position)}</b>";
                if (i + 1 > GetMaximumHomes(player, false) && cHome.Value == null) subText = "ТОЛЬКО ДЛЯ ПРИВИЛЕГИЙ";
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "15 15", OffsetMax = "0 25" },
                    Text = { Text = subText, Align = TextAnchor.LowerLeft, Font = "robotocondensed-regular.ttf", FontSize = 18, Color = !(i + 1 > GetMaximumHomes(player, false)) ? "0.81 0.77 0.74 1" : "0.81 0.77 0.74 1" }
                }, SettingsLayer + ".UP" + i);
                
                if (i + 1 > GetMaximumHomes(player, false) && cHome.Value == null)
                {

                }
                else if (cHome.Value != null)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "-95 0", OffsetMax = "0 0" },
                        Button = { Color = "0.55 0.27 0.23 1", Material = "", Command = $"UI_DetonatorHandler remove {cHome.Key}" }, 
                        Text = { Text = "УДАЛИТЬ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.93 0.89 0.85 1" }
                    }, SettingsLayer + ".UP" + i);
                    
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "-215 0", OffsetMax = "-95 0" },
                        Button = { Color = cHome.Value.IsMain ? "0.52 0.50 0.47 1" : "0.46 0.49 0.27 1", Material = "", Command = $"UI_DetonatorHandler main {cHome.Key}"},  
                        Text = { Text = cHome.Value.IsMain ? "ОСНОВНОЙ\nДОМ" : "СДЕЛАТЬ\nОСНОВНЫМ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.93 0.89 0.85 1" }
                    }, SettingsLayer + ".UP" + i);
                } 

                margin += 90; 
            }
            //PrintError("123");
            
            CuiHelper.AddUi(player, container);
        }
        
        private static string GridReference(Vector3 pos)
        {
            int worldSize = ConVar.Server.worldsize;
            const float scale = 150f;
            float x = pos.x + worldSize/2f;
            float z = pos.z + worldSize/2f;
            var lat = (int)(x / scale);
            var latChar = (char)('A' + lat);
            var lon = (int)(worldSize/scale - z/scale);
            return $"{latChar}{lon}";
        }

        #endregion

        #region Methods

        private static int GetMaximumHomes(BasePlayer player, bool isBed)
        {
            int maxHomes = 1;
            
            foreach (var check in Settings.HomesAmount.Where(p =>
                _.permission.UserHasPermission(player.UserIDString, p.Key)))
            {
                if (check.Value > maxHomes)
                    maxHomes = check.Value;
            }

            return maxHomes;
        }

        private static int GetMinimalTeleport(BasePlayer player, bool isBed)
        {
            Tuple<int, int> tuple = new Tuple<int,int>(30, 300);
            
            foreach (var check in Settings.TeleportLength.Where(p =>
                _.permission.UserHasPermission(player.UserIDString, p.Key)))
            {
                if (check.Value.Item1 < tuple.Item1)
                    tuple = check.Value;
            }

            return isBed ? tuple.Item1 : tuple.Item2;
        }

        private static int GetMinimalCooldown(BasePlayer player, bool isBed)
        {
            Tuple<int, int> tuple = new Tuple<int,int>(300, 360);
            
            foreach (var check in Settings.CooldownLength.Where(p =>
                _.permission.UserHasPermission(player.UserIDString, p.Key)))
            { 
                if (check.Value.Item1 < tuple.Item1)
                    tuple = check.Value;
            }

            return isBed ? tuple.Item1 : tuple.Item2;
        }

        private static void StartTeleport(BasePlayer player, Home home)
        {
            if (home == null) return;
            
            ClearTeleport(player, home.Entity.transform.position + new Vector3(0, 0.3f, 0)); 
        }
        
        private static void ClearTeleport(BasePlayer player, Vector3 position, bool last = false)
        {
            if (player.GetMounted() != null)
                player.EnsureDismounted();
             
            player.transform.SetParent(null); 
            player.SwitchParent(null); 
            
            if (player.net?.connection != null)
            {
                player.ClientRPCPlayer(null, player, "StartLoading");
            }
            
            //player.StartSleeping();
            player.MovePosition(position);
            
            if (player.net?.connection != null)
            {
                player.ClientRPCPlayer(null, player, "ForcePositionTo", position);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            }
            
            player.UpdateNetworkGroup();
            player.SendNetworkUpdateImmediate(false);
            
            if (player.net?.connection == null)
            {
                return;
            }
            
            player.SendFullSnapshot(); 
        }
        
        private void DestroyAll<T>() 
        {
            var objects = UnityEngine.Object.FindObjectsOfType(typeof(T));
            objects?.ToList().ForEach(UnityEngine.Object.Destroy); 
        }

        private static void SendMarker(BasePlayer player, Vector3 position, string text)
        {
            player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true); 
                    
            player.SendEntityUpdate();   
            player.SendConsoleCommand("ddraw.text", 1f, Color.white, position, text);
                    
            if (player.Connection.authLevel < 2)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);  
                player.SendConsoleCommand("camspeed 0");
            }
                    
            player.SendEntityUpdate();
        }

        #endregion
    }
}