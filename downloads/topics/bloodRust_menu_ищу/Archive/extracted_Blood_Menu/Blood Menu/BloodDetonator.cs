using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;
using Color = UnityEngine.Color;
using Random = UnityEngine.Random;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("BloodDetonator", "[LimePlugin] Chibubrik", "1.0.0")]
    [Description("Плагин телепортации на основе детонатора")]
    public class BloodDetonator : RustPlugin
    {
        #region Classes
        Dictionary<ulong, double> Cooldown = new Dictionary<ulong, double>();

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
                    var ent = BaseEntity.saveList.FirstOrDefault(p => p.net.ID.Value == home.Value.NetID);
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

            public ulong NetID;

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


            [JsonProperty("Выдавать детонатор при возрождении?")]
            public bool givePlayerDetonator = true;
            [JsonProperty("Кулдаун на получение детонатора")]
            public double CooldownGive = 60;
            [JsonProperty("Настройка пейджера по привилегиям")]
            public List<SettingsPermissionPager> ListPager = new List<SettingsPermissionPager>();

            public static Configuration Generate()
            {
                return new Configuration
                {
                    HomesAmount = new Dictionary<string, int>
                    {
                        ["blooddetonator.Default"] = 1,
                        ["blooddetonator.Vip"] = 2,
                        ["blooddetonator.VipPlus"] = 3
                    },
                    TeleportLength = new Dictionary<string, Tuple<int, int>>
                    {
                        ["blooddetonator.Default"] = new Tuple<int, int>(15, 15)
                    },
                    CooldownLength = new Dictionary<string, Tuple<int, int>>
                    {
                        ["blooddetonator.Default"] = new Tuple<int, int>(300, 360)
                    },
                    ListPager = new List<SettingsPermissionPager>
                    {
                        new SettingsPermissionPager
                        {
                            Permission = "blooddetonator.pager.default",
                            TP = 15,
                            KD = 300,
                            GivePager = false
                        },
                        new SettingsPermissionPager
                        {
                            Permission = "blooddetonator.pager.vip",
                            TP = 5,
                            KD = 100,
                            GivePager = true
                        }
                    }
                };
            }
        }

        public class SettingsPermissionPager
        {
            [JsonProperty("Привилегия")]
            public string Permission;
            [JsonProperty("Длительность телепортации")]
            public int TP;
            [JsonProperty("КД после телепортации")]
            public int KD;
            [JsonProperty("Выдавать пейджер при возрождении?")]
            public bool GivePager;
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
                if (Player == null) return;
                if (!Status.CheckDetonator(Player)) return;
                if (Player.IsSwimming()) return;
                if (Player.metabolism.bleeding.value > 0)
                {
                    Player.ChatMessage($"<color=#c43d2a>Кровотечение</color> замедляет телепортацию!");
                }

                /*if ((bool) _.RaidBlock.Call("IsInRaid", Player))
                {
                    Player.ChatMessage($"Телепортация при <color=#e6533d>рейд блоке</color> невозможна!");
                }*/

                Status.CheckFrequency(Player);
                //Log($"test TryStart: {Status?.pagerPlayer?.displayName}");
                InvokeRepeating(nameof(ControlUpdate), 0, 0.05f);
                InvokeRepeating(nameof(CheckCupboard), 0f, 0.25f);
                //InvokeRepeating(nameof(CheckBlocked), 0f, 1f);
            }

            public void CheckBlocked()
            {
                if (Cooldown > 0) return;

                IsBlocked = (bool)_.RaidBlock.Call("IsInRaid", Player);
                //if (!IsBlocked && Status.Home != null)
                //    IsBlocked = (bool) _.RaidBlock.Call("IsInRaid", Status.Home.Entity.transform.position);

                if (IsBlocked) Status.Color = "1 0.5 0.5 0.5";
            }

            public void CheckCupboard()
            {
                if (IsBlocked || Cooldown > 0) return;

                Status.Color = Status.CheckCupboard(Player) ? Status.Home != null ? "0.5 1 0.5 0.5" : Status.pagerPlayer != null ? "0.1 0.2 1 0.5" : "1 0.5 0.5 0.5" : "1 0.5 0.5 0.5";

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
                //CancelInvoke(nameof(CheckBlocked)); 
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
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-50 -5", OffsetMax = "50 5" },
                            Image = { Color = "1 1 1 0.2" }
                        }, "Overlay", Layer);

                        CuiHelper.AddUi(Player, initialContainer);
                        IsActivated = true;
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
                        RectTransform = { AnchorMin = "0 0", AnchorMax = $"1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                        Image = { Color = "1 0.5 0.5 0.5" }
                    }, Layer, Layer + ".Update");
                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = $"1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                        Text = { Text = $"ПЕРЕЗАРЯДКА", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 8, Color = "1 1 1 0.8" }
                    }, Layer + ".Update");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = $"0 1", OffsetMin = "-300 -1", OffsetMax = "-2 0" },
                        Text = { Text = $"{Cooldown} СЕК.", Align = TextAnchor.MiddleRight, Font = "robotocondensed-regular.ttf", FontSize = 9, Color = "1 1 1 0.8" }
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
                        RectTransform = { AnchorMin = "0 0", AnchorMax = $"1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                        Image = { Color = "1 0.5 0.5 0" }
                    }, Layer, Layer + ".Update");

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = $"{(float)Status.Length / Status.MaxLength} 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                        Image = { Color = Status.Color }
                    }, Layer + ".Update");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = $"1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                        Text = { Text = $"ТЕЛЕПОРТАЦИЯ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 8, Color = "1 1 1 0.8" }
                    }, Layer + ".Update");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = $"0 1", OffsetMin = "-20 -1", OffsetMax = "-2 0" },
                        Text = { Text = $"{((Status.Length / Status.MaxLength) * 100):F0}%", Align = TextAnchor.MiddleRight, Font = "robotocondensed-regular.ttf", FontSize = 9, Color = "1 1 1 0.8" }
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

                //Log($"test TryTeleport: {Status?.pagerPlayer?.displayName}");
                if (Status.Home != null)
                {
                    Cooldown = GetMinimalCooldown(Player, Status.Home.IsBed());
                    ClearTeleport(Player, Status.Home.Entity.transform.position + new Vector3(0, 0.5f, 0));
                    return;
                }
                if (Status.pagerPlayer != null)
                {
                    Cooldown = _.GetSetPager(Player.UserIDString).KD;
                    ClearTeleport(Player, Status.pagerPlayer.transform.position + new Vector3(1f, 0.5f, 1f));
                    return;
                }
            }
        }

        private class Status
        {
            public float Length;
            public Home Home;
            public BasePlayer pagerPlayer;
            public Item Detonator;
            public string Color = "1 1 1 0.2";
            public int MaxLength;

            public bool CheckDetonator(BasePlayer player)
            {
                Detonator = player.GetActiveItem();

                return Detonator != null && !Detonator.isBroken && Detonator.info.shortname == "rf.detonator";
            }
            public bool CheckCupboard(BasePlayer player) => !player.IsBuildingBlocked() && (Home != null || pagerPlayer != null);
            public void CheckFrequency(BasePlayer player)
            {
                var freq = Detonator.GetHeldEntity()?.GetComponent<global::Detonator>().frequency ?? -1;

                Home = Handler.FindHomeByFrequency(player, freq.ToString());
                if (Home == null && _.HasSetPager(player.UserIDString)) CheckPager(player, freq);
                Color = Home != null ? "0.5 1 0.5 0.5" : pagerPlayer != null ? "0.1 0.2 1 0.5" : "1 0.5 0.5 0.5";
                MaxLength = Home != null ? GetMinimalTeleport(player, Home.IsBed()) : pagerPlayer != null ? _.GetSetPager(player.UserIDString).TP : 5;
            }
            void CheckPager(BasePlayer player, int freq)
            {
                pagerPlayer = GetPlayerListen(player, freq);
            }
            internal BasePlayer GetPlayerListen(BasePlayer _player, int freq)
            {
                List<IRFObject> ListenList = new List<IRFObject>(RFManager.GetListenerSet(freq));
                if (ListenList == null || ListenList.Count == 0)
                {
                    Log("Test GetPlayerListen: не найдены пейджеры");
                    return null;
                }
                var obj = ListenList?.FindAll(x => x is PagerEntity
                                                   && ((PagerEntity)x).GetParentEntity() != null
                                                   && ((PagerEntity)x).GetParentEntity() is BasePlayer
                                                   && ((BasePlayer)((PagerEntity)x).GetParentEntity()) != _player
                                                   && _.HasSetPager(((BasePlayer)((PagerEntity)x).GetParentEntity()).UserIDString)
                                                   && ((BasePlayer)((PagerEntity)x).GetParentEntity()).GetActiveItem()?.info?.itemid != null
                                                   && ((BasePlayer)((PagerEntity)x).GetParentEntity()).GetActiveItem().info.itemid == -566907190
                                                   );
                if (obj == null || obj.Count == 0) return null;
                // Log($"Test GetPlayerListen: тип - {obj[0].GetType()}");
                var pl = obj?.FirstOrDefault(x => (((PagerEntity)x)?.GetParentEntity()) is BasePlayer);
                if (pl == null)
                {
                    Log("Test GetPlayerListen: не найдены игроки");
                    return null;
                }
                var player = ((PagerEntity)pl).GetParentEntity() as BasePlayer;
                // Log($"Test GetPlayerListen: игрок {player.displayName}");
                return player;
            }
        }

        #endregion

        #region Variables

        [PluginReference] private Plugin RaidBlock;
        private static BloodDetonator _;
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
                        ulong index = ulong.Parse(arg.Args[1]);
                        var obj = Handler.Managers[player.userID];

                        var entity = BaseNetworkable.serverEntities.Find(new NetworkableId(index)) as BaseEntity;
                        if (entity == null || entity.IsDestroyed) return;
                        string frequency = Oxide.Core.Random.Range(1000, 9999).ToString();
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
                            NetID = entity.net.ID.Value
                        });
                        player.SendConsoleCommand("detonator_give");

                        player.SendConsoleCommand("gametip.showtoast_translated", 2, "", $"<size=12>Вы успешно установили точку телепортации!</size>\n<color=#82c6f580><size=10>Чтобы телепортироваться возьмите пульт в руку и зажмите ЛКМ.</size></color>");
                        CuiHelper.DestroyUi(player, Layer + ".Notify");
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

                        var item = player.inventory.FindItemByItemID(ItemManager.FindItemDefinition("rf.detonator").itemid);
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

        private void ReplyWithHelp(BasePlayer player) => player.ChatMessage($"О нашей системе телепортации вы можете узнать в меню, перейдя в раздел <<color=#accc7a>НАСТРОЙКИ</color>>.");

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
        protected override void SaveConfig() => Config.WriteObject(Settings);
        private void OnServerInitialized()
        {
            _ = this;

            Handler = DataHandler.LoadData();

            Settings.TeleportLength.ToList().ForEach(p => permission.RegisterPermission(p.Key, this));
            Settings.CooldownLength.ToList().ForEach(p => permission.RegisterPermission(p.Key, this));
            Settings.HomesAmount.ToList().ForEach(p => permission.RegisterPermission(p.Key, this));
            Settings.ListPager.ForEach(p => permission.RegisterPermission(p.Permission, this));
            Subscribe("OnPlayerRespawned");
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
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, SettingsLayer + ".R");
            }
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
                    var home = Handler.Managers[entity.OwnerID].Homes.FirstOrDefault(p => p.Value.NetID == entity.net.ID.Value);
                    if (home.Value == null) return;

                    Handler.Managers[entity.OwnerID].Homes.Remove(home.Key);
                }
            }
        }

        private bool? OnRecycleItem(Recycler recycler, Item item)
        {
            if (item.info.shortname.Contains("rf.detonator"))
            {
                recycler.MoveItemToOutput(item);
                return false;
            }
            if (item.info.shortname.Contains("rf_pager"))
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
                    var home = Handler.Managers[entity.OwnerID].Homes.FirstOrDefault(p => p.Value.NetID == entity.net.ID.Value);
                    if (home.Value == null) return;

                    Handler.Managers[entity.OwnerID].Homes.Remove(home.Key);
                }
            }
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            if ((BaseEntity)entity is SleepingBag)
            {
                if (Handler.Managers.ContainsKey(((BaseEntity)entity).OwnerID))
                {
                    var home = Handler.Managers[((BaseEntity)entity).OwnerID].Homes.FirstOrDefault(p => p.Value.NetID == entity.net.ID.Value);
                    if (home.Value == null) return;

                    Handler.Managers[((BaseEntity)entity).OwnerID].Homes.Remove(home.Key);
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

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null) return;
            if (Handler.Managers[player.userID].Homes.Count() > 0)
            {
                if (HasSetPager(player.UserIDString))
                {
                    if (GetSetPager(player.UserIDString).GivePager)
                    {
                        Item pager = ItemManager.CreateByItemID(-566907190);
                        if (pager != null)
                        {
                            try { pager.info.GetComponent<PagerEntity>().ChangeFrequency(Random.Range(1, 9999)); } catch { }
                            player.GiveItem(pager);
                        }
                    }
                }
                if (Settings.givePlayerDetonator)
                {
                    int i = GetFrequencyForPlayer(player);
                    Item det = ItemManager.CreateByItemID(596469572);

                    if (det == null)
                    {
                        Puts($@"Детонатор НЕ выдан игроку: {player.displayName}
                Отсутствует детонатор.");
                        return;
                    }
                    if (det.GetHeldEntity() == null)
                    {
                        Puts($@"Детонатор НЕ выдан игроку: {player.displayName}
                det.GetHeldEntity() == null");
                        return;
                    }
                    det.GetHeldEntity().GetComponent<global::Detonator>().frequency = i;
                    player.GiveItem(det);
                }
            }
            return;
        }

        private void OnEntityBuilt(Planner planner, GameObject gameobject)
        {
            if (planner == null || gameobject == null) return;

            var player = planner.GetOwnerPlayer();
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
                player.SendConsoleCommand("gametip.showtoast_translated", 2, "", $"<size=12>Точка телепортации!</size>\n<color=#82c6f580><size=10>В этом доме уже есть ваша точка телепортации.</size></color>");
                return;
            }


            Handler.Managers[player.userID].LoadHome();
            if (obj.Homes.Count >= maxAmount)
            {
                player.SendConsoleCommand("gametip.showtoast_translated", 2, "", $"<size=12>Точка телепортации!</size>\n<color=#82c6f580><size=10>У вас нет свободного слота для установки ещё одной точки телепортации.</size></color>");
                return;
            }
            try
            {
                ShowNotify(player, $"{(entity.PrefabName.Contains("sleeping") ? "спальник" : (entity.PrefabName.Contains("beachtowel") ? "полотенце" : "кровать"))}", entity.net.ID);
                Effect effect = new Effect("ASSETS/BUNDLED/PREFABS/FX/INVITE_NOTICE.PREFAB".ToLower(), player, 0, new Vector3(), new Vector3());
                EffectNetwork.Send(effect, player.Connection);
            }
            catch
            {
                PrintError("TRY FIX ONENTITYBUILT FIX FAILED ON TRY CATCH");
            }
        }

        void ShowNotify(BasePlayer player, string text, NetworkableId id)
        {
            CuiHelper.DestroyUi(player, Layer + ".Notify");
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "5 -79", OffsetMax = "275 -39" },
                Image = { Color = "0 0 0 0" }
            }, "Overlay", Layer + ".Notify");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.1", AnchorMax = "0.12 0.9", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, Layer + ".Notify");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.1", AnchorMax = "0.12 0.9", OffsetMax = "0 0" },
                Image = { Color = HexToCuiColor("#bbc47e", 30), Sprite = "assets/content/ui/ui.background.transparent.radial.psd" }
            }, Layer + ".Notify", ".NotifyImage");

            container.Add(new CuiElement
            {
                Parent = ".NotifyImage",
                Components =
                {
                    new CuiImageComponent { Sprite = "assets/icons/picked up.png", Color = HexToCuiColor("#bbc47e", 100) },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "9 9", OffsetMax = "-9 -9" },
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.15 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = $"<b><size=12><color=white>Сделать {text} точкой телепортации?</color></size></b>\nнажмите на уведомление чтобы подтвердить", Color = "1 1 1 0.4", Align = TextAnchor.MiddleLeft, FontSize = 10, Font = "robotocondensed-regular.ttf" }
            }, Layer + ".Notify");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.15 0", AnchorMax = "1 1" },
                Button = { Color = "0 0 0 0", Command = $"UI_DetonatorHandler sethome {id}" },
                Text = { Text = "" }
            }, Layer + ".Notify");

            CuiHelper.AddUi(player, container);

            timer.In(6f, () => CuiHelper.DestroyUi(player, Layer + ".Notify"));
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

            Cooldown[player.userID] = 0;
        }


        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (oldItem?.info.shortname == "rf.detonator")
            {
                PressStatuses[player.userID].ManualStop();
            }
            if (newItem == null || player == null || !player.userID.IsSteamId()) return;
            if (newItem.info.shortname == "rf_pager")
            {
                if (HasSetPager(player.UserIDString))
                    player.ChatMessage("Вы взяли пейджер.Сообщите свою частоту человеку,чтобы тот мог телепортироваться!");
                else
                    player.ChatMessage("недостаточно прав для телепорта!");
            }
        }

        private void OnPlayerInput(BasePlayer player, InputState state)
        {
            if (state.WasJustPressed(BUTTON.FIRE_PRIMARY))
            {
                if (!PressStatuses.ContainsKey(player.userID)) return;
                PressStatuses[player.userID].TryStart();
                return;
            }
        }

        #endregion

        #region Commands
        [ConsoleCommand("detonator_give")]
        void ConsoleGive(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (Cooldown[player.userID] > CurrentTime())
            {
                Effect effect = new Effect("assets/bundled/prefabs/fx/notice/item.select.fx.prefab", player, 0, new Vector3(), new Vector3());
                EffectNetwork.Send(effect, player.Connection);
                player.Command("gametip.showtoast", 1, $"Подождите {TimeSpan.FromSeconds(Cooldown[player.userID] - CurrentTime()).TotalSeconds}с для следующего получения детонатора!");
                return;
            }
            int i = GetFrequencyForPlayer(player);
            Item det = ItemManager.CreateByItemID(596469572);

            if (det == null)
            {
                Puts($@"Детонатор НЕ выдан игроку: {player.displayName}
                Отсутствует детонатор.");
                return;
            }
            if (det.GetHeldEntity() == null)
            {
                Puts($@"Детонатор НЕ выдан игроку: {player.displayName}
                det.GetHeldEntity() == null");
                return;
            }
            det.GetHeldEntity().GetComponent<global::Detonator>().frequency = i;
            player.GiveItem(det);
            Cooldown[player.userID] = CurrentTime() + Settings.CooldownGive;
        }
        #endregion

        #region Interface

        private void UnInitializeInterface(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, SettingsLayer + ".R");
        }

        private string SettingsLayer = "UI_SettingsLayer";

        Dictionary<string, string> InfoTP = new Dictionary<string, string>()
        {
            ["Установка"] = "Установите спальник/кровать на фундамент в своём\nдоме, учтите, рядом не должно быть уже\nустановленных ваших точек телепортации.",
            ["Подтверждение установки"] = "В левом верхнем углу после установки появится\nуведомление, нажмите по нему (не по логотипу)\nчтобы подтвердить установку точки.",
            ["Использование"] = "После успешной установки точки телепортации в\nинвентарь будет выдан дистанционный пульт с\nустановленной частотой, для телепортации\nнеобходимо взять его в руки и зажать ЛКМ. Этот\nпульт будет выдаваться при каждом спавне если у\nВас установлена точка телепортации.",
            ["Управление установленными точками"] = "Управлять точками телепортации можно через меню\nв котором Вы сейчас находитесь.",
            ["В случае утери пульта"] = "Ничего страшного в том что Вы потеряли или\nвыкинули пульт - нет, его можно будет получить в\nэтом разделе, нажав на кнопку:",
            ["Дополнительные точки телепортации"] = "На данный момент дополнительные точки\nтелепортации открываются только если у вас\nактивная привилегия.",
        };

        private void InitializeInterface(BasePlayer player)
        {
            var settings = Handler.Managers[player.userID];

            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, SettingsLayer + ".R");

            container.Add(new CuiPanel()
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Menu_Block", SettingsLayer + ".R");

            container.Add(new CuiPanel()
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, SettingsLayer + ".R", SettingsLayer + ".RP");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.5 0.38", OffsetMax = "0 0" },
                Image = { Color = HexToCuiColor("#d1b283", 25) }
            }, SettingsLayer + ".R", SettingsLayer + ".Info");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04 0.75", AnchorMax = "1 1", OffsetMax = $"0 0" },
                Text = { Text = "Частота точек телепортации", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = HexToCuiColor("#d1b283", 100) }
            }, SettingsLayer + ".Info");

            string helpText = "Если Вы используете только одну точку\nтелепортации, то обращать внимание на её\nчастоту вообще не нужно. Но чтобы\nиспользовать несколько точек, придётся\nменять частоту пульта. Это можно делать\nвручную или просто сделав нужную точку\nосновной (тогда пульт в инвентаре\nавтоматически выставит нужную частоту).";

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04 0", AnchorMax = "1 0.76", OffsetMax = "0 0" },
                Text = { Text = helpText, Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = HexToCuiColor("#d1b283", 80) }
            }, SettingsLayer + ".Info");

            settings.LoadHome();
            float width = 0.5f, height = 0.19f, startxBox = 0f, startyBox = 1f - height, xmin = startxBox, ymin = startyBox;
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
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMax = "0 0" },
                    Image = { Color = "1 1 1 0.015", Material = "assets/content/ui/uibackgroundblur.mat" }
                }, SettingsLayer + ".R", SettingsLayer + ".UP" + i);
                xmin += width;
                if (xmin + width >= 0)
                {
                    xmin = startxBox;
                    ymin -= height + 0.0167f;
                }

                if (cHome.Value == null)
                {

                }

                string parent = SettingsLayer + ".UP" + i;

                if (cHome.Value != null)
                {

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
                    }, SettingsLayer + ".UP" + i);

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.04 0.3", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Text = { Text = $"<size=18><b>ЧАСТОТА: {cHome.Value.Frequency} GHz</b></size>\n" + (cHome.Value.IsBed() ? "кровать" : (cHome.Value.IsBag() ? "спальник" : "полотенце")) + $" - в квадрате {GridReference(cHome.Value.Entity.transform.position)}", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = !(i + 1 > GetMaximumHomes(player, false)) ? "0.81 0.77 0.74 1" : "0.81 0.77 0.74 1" }
                    }, SettingsLayer + ".UP" + i);

                    container.Add(new CuiElement
                    {
                        Parent = parent,
                        Components =
                        {
                            new CuiImageComponent {ItemId = cHome.Value.IsBed() ? ItemManager.FindItemDefinition("bed").itemid : cHome.Value.IsBag() ? ItemManager.FindItemDefinition("sleepingbag").itemid : ItemManager.FindItemDefinition("beachtowel").itemid, Color = "1 1 1 0.8"},
                            new CuiRectTransformComponent { AnchorMin = "0.8 0.45", AnchorMax = "0.98 0.95", OffsetMin = "4 4", OffsetMax = "-4 -4" }
                        }
                    });
                }
                else if (i + 1 > GetMaximumHomes(player, false) && cHome.Value == null)
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.2 0", AnchorMax = "0.8 1", OffsetMax = "0 0" },
                        Text = { Text = $"<size=16><b>Дополнительная точка</b></size>\nтолько для привилегий", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "1 1 1 0.2" }
                    }, SettingsLayer + ".UP" + i);
                }
                else
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Text = { Text = $"<size=16><b>Точка не установлена</b></size>\nустановите спальник или кровать", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "1 1 1 0.2" }
                    }, SettingsLayer + ".UP" + i);
                }

                if (i + 1 > GetMaximumHomes(player, false) && cHome.Value == null)
                {

                }
                else if (cHome.Value != null)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.73 0", AnchorMax = "1 0.3", OffsetMax = "0 0" },
                        Button = { Color = HexToCuiColor("#efb399", 35), Command = $"UI_DetonatorHandler remove {cHome.Key}" },
                        Text = { Text = "Удалить", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 14, Color = HexToCuiColor("#efb399", 100) }
                    }, SettingsLayer + ".UP" + i);

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "0.725 0.3", OffsetMax = "0 0" },
                        Button = { Color = cHome.Value.IsMain ? "0 0 0 0.4" : HexToCuiColor("#bbc47e", 35), Command = $"UI_DetonatorHandler main {cHome.Key}" },
                        Text = { Text = cHome.Value.IsMain ? "Основной" : "<b>Сделать основным</b>", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = cHome.Value.IsMain ? "1 1 1 0.2" : HexToCuiColor("#bbc47e", 100) }
                    }, SettingsLayer + ".UP" + i);
                }
            }
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.53 0.13", AnchorMax = "0.535 1", OffsetMax = "0 0" },
                Image = { Color = HexToCuiColor("#b2a9a3", 15), Material = "assets/content/ui/uibackgroundblur.mat" }
            }, SettingsLayer + ".RP");
            float width1 = 0.5f, height1 = 0f, startxBox1 = 0.51f, startyBox1 = 1f - height1, xmin1 = startxBox1, ymin1 = 0f;
            for (int z = 0; z < InfoTP.Count(); z++)
            {
                if (z == 0)
                {
                    height1 = 0.14f;
                    ymin1 = 1f - height1;
                }
                else if (z == 1)
                    height1 = 0.14f;
                else if (z == 2)
                    height1 = 0.22f;
                else if (z == 3)
                    height1 = 0.12f;
                else if (z == 4)
                    height1 = 0.21f;
                else if (z == 5)
                    height1 = 0.14f;

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + height1 * 1}", OffsetMax = "0 0" },
                    Image = { Color = "0 0 0 0" }
                }, SettingsLayer + ".R", SettingsLayer + ".INFO" + z);
                xmin1 += width1;
                if (xmin1 + width1 >= 0)
                {
                    xmin1 = startxBox1;
                    ymin1 -= height1 + z == 0 ? 0.2f : z == 1 ? 0.225f : z == 2 ? 0.125f : z == 3 ? 0.215f : z == 4 ? 0.145f : 0.145f;
                }

                var anchHeight = z == 0 ? 0.63 : z == 1 ? 0.63 : z == 2 ? 0.76 : z == 3 ? 0.56 : z == 4 ? 0.75 : 0.63;
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"0 {anchHeight}", AnchorMax = "0.09 1", OffsetMax = "0 0" },
                    Image = { Color = HexToCuiColor("#b2a9a3", 100), Material = "assets/content/ui/uibackgroundblur.mat" }
                }, SettingsLayer + ".INFO" + z, ".Count");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = $"<color=#45403b>{z + 1}</color>", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.93 0.89 0.85 1" }
                }, ".Count");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0.12 {anchHeight}", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = InfoTP.ElementAt(z).Key, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf", FontSize = 12, Color = HexToCuiColor("#cec5bb", 100) }
                }, SettingsLayer + ".INFO" + z);

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.12 0", AnchorMax = $"1 {anchHeight}", OffsetMax = "0 0" },
                    Text = { Text = InfoTP.ElementAt(z).Value, Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf", FontSize = 10, Color = "1 1 1 0.4" }
                }, SettingsLayer + ".INFO" + z);

                if (z == 4)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.12 0", AnchorMax = "0.5 0.3", OffsetMax = "0 0" },
                        Button = { Color = settings.Homes.Count() > 0 ? HexToCuiColor("#84b4dd", 30) : "1 1 1 0.2", Command = settings.Homes.Count() > 0 ? "detonator_give" : "" },
                        Text = { Text = settings.Homes.Count() > 0 ? "Получить пульт" : "Недоступно", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = settings.Homes.Count() > 0 ? HexToCuiColor("#84b4dd", 100) : "1 1 1 0.2" }
                    }, SettingsLayer + ".INFO" + z);
                }
            }

            CuiHelper.AddUi(player, container);
        }

        private static string GridReference(Vector3 pos)
        {
            int worldSize = ConVar.Server.worldsize;
            const float scale = 150f;
            float x = pos.x + worldSize / 2f;
            float z = pos.z + worldSize / 2f;
            var lat = (int)(x / scale);
            var latChar = (char)('A' + lat);
            var lon = (int)(worldSize / scale - z / scale);
            return $"{latChar}{lon}";
        }

        #endregion

        #region Methods

        public static void Log(string text, string prefix = "Detonator")
        {
            Interface.Oxide.LogInfo($"[{prefix}] {text}");
        }

        public SettingsPermissionPager GetSetPager(string userId)
        {
            return new SettingsPermissionPager
            {
                TP = Settings.ListPager.Min(x => x.TP),
                KD = Settings.ListPager.Min(x => x.KD),
                GivePager = Settings.ListPager.Exists(x => x.GivePager),
            };
        }

        public bool HasSetPager(string userId) => Settings.ListPager.Exists(x => permission.UserHasPermission(userId, x.Permission));

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
            Tuple<int, int> tuple = new Tuple<int, int>(30, 300);

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
            Tuple<int, int> tuple = new Tuple<int, int>(300, 360);

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

            player.StartSleeping();
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

        #region Хелпер
        public string HexToCuiColor(string HEX, float Alpha = 100)
        {
            if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

            var str = HEX.Trim('#');
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {Alpha / 100}";
        }

        private double CurrentTime() => DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        #endregion
    }
}