using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch.Extend;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Kits", "Netrunner", "1.0.0")]
    public class Kits : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ImageLibrary, Broadcast;

        #endregion

        #region Config

        enum ItemPosition
        {
            Main,
            Belt,
            Wear
        }

        class KitItem
        {
            [JsonProperty("Короткое название предмета")]
            public string Shortname;

            [JsonProperty("Количество")] public int Amount;

            [JsonProperty("Является чертежом?(true-да/false-нет")]
            public bool IsBlueprint = false;

            [JsonProperty("SkinID")] public ulong SkinId = 0;

            [JsonProperty("Позиция предмета(0 - инвентарь,1 - пояс, 2 - одежда)")]
            public ItemPosition Position = ItemPosition.Main;


        }

        class KitData
        {
            [JsonProperty("Ключ набора")] public string Key;

            [JsonProperty("Отоброжаемое имя набора")]
            public string DisplayName;

            [JsonProperty("Максимальное количество использований(0 для безлимитных наборов")]
            public int MaxUse = 0;

            [JsonProperty("Является автокитом?")] public bool IsAuto = false;

            [JsonProperty("Приоритет автокита должен быть равен 0 или больше 0")]
            public int AutoPriority = -1;

            [JsonProperty("Привилегия для доступа к набору")]
            public string Permission = "kits.default";

            [JsonProperty("Список предметов набора")]
            public List<KitItem> Items = new List<KitItem>();

            [JsonProperty("Время перезарядки(В секундах)")]
            public double Cooldown = 60;

            [JsonProperty("Ссылка на изображение")]
            public string ImageUrl = "https://i.ibb.co/phk84y0/Logo-New2.png";
        }

        static Configuration config = new Configuration();

        class Configuration
        {
            public List<KitData> FreeKits = new List<KitData>();
            public List<KitData> PaidKits = new List<KitData>();

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    FreeKits = new List<KitData>
                    {
                        new KitData
                        {
                            Key = "start",
                            Cooldown = 100,
                            DisplayName = "Start",
                            IsAuto = false,
                            MaxUse = 0,
                            Permission = "kits.default",
                            ImageUrl = "https://i.ibb.co/Rg3vjvt/StartKit.png",
                            Items = new List<KitItem>
                            {
                                new KitItem
                                {
                                    Shortname = "wood",
                                    Amount = 100
                                },
                                new KitItem
                                {
                                    Shortname = "wood",
                                    Amount = 100
                                },
                                new KitItem
                                {
                                    Shortname = "pickaxe",
                                    Amount = 1,
                                    Position = ItemPosition.Belt
                                },
                                new KitItem
                                {
                                    Shortname = "hatchet",
                                    Amount = 1,
                                    Position = ItemPosition.Belt
                                },
                            }
                        },
                        new KitData
                        {
                            Key = "hunt",
                            Cooldown = 100,
                            DisplayName = "Hunter",
                            IsAuto = false,
                            MaxUse = 0,
                            Permission = "kits.default",
                            ImageUrl = "https://i.ibb.co/hW1mKKD/hunter.png",
                            Items = new List<KitItem>
                            {
                                new KitItem
                                {
                                    Shortname = "arrow.wooden",
                                    Amount = 100
                                },
                                new KitItem
                                {
                                    Shortname = "knife.bone",
                                    Amount = 1,
                                    Position = ItemPosition.Belt
                                },
                                new KitItem
                                {
                                    Shortname = "wolfmeat.cooked",
                                    Amount = 5
                                },
                                new KitItem
                                {
                                    Shortname = "bow.hunting",
                                    Amount = 100,
                                    Position = ItemPosition.Belt
                                },
                            }
                        },
                    },
                    PaidKits = new List<KitData>
                    {
                        new KitData
                        {
                            Key = "vip",
                            Cooldown = 36000,
                            DisplayName = "VIP",
                            IsAuto = false,
                            Permission = "kits.vip",
                            ImageUrl = "https://i.ibb.co/SXBbdwc/VipImg.png",
                            MaxUse = 2,
                            Items = new List<KitItem>
                            {
                                new KitItem
                                {
                                    Shortname = "pistol.semiauto",
                                    Amount = 1,
                                    IsBlueprint = false,
                                    Position = ItemPosition.Belt,
                                    SkinId = 0
                                },

                                new KitItem
                                {
                                    Shortname = "syringe.medical",
                                    Amount = 5,
                                    IsBlueprint = false,
                                    Position = ItemPosition.Belt,
                                    SkinId = 0
                                },
                                new KitItem
                                {
                                    Shortname = "coffeecan.helmet",
                                    Amount = 1,
                                    IsBlueprint = false,
                                    Position = ItemPosition.Wear,
                                    SkinId = 0
                                },
                                new KitItem
                                {
                                    Shortname = "ammo.pistol",
                                    Amount = 50,
                                    IsBlueprint = false,
                                    Position = ItemPosition.Main,
                                    SkinId = 0
                                },
                            }
                        }
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) PrintWarning("NULL");
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintError($"Не удалось найти конфигурацию 'oxide/config/{Name}', Создание конфига!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region data

        Dictionary<ulong, List<PlayerKit>> _playerData = new Dictionary<ulong, List<PlayerKit>>();

        Dictionary<ulong, KitData> _autoKits = new Dictionary<ulong, KitData>();

        #region Data Methods

        void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("Kits/PlayerData", _playerData);

        void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("Kits/PlayerData"))
            {
                _playerData =
                    Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<PlayerKit>>>("Kits/PlayerData");
            }
        }

        #endregion

        class PlayerKit
        {
            public string KitKey;

            public double Time;

            public int Uses;
        }

        void InitPlayer(BasePlayer player)
        {
            if (!_playerData.ContainsKey(player.userID))
            {
                _playerData.Add(player.userID, new List<PlayerKit>());
                foreach (var kitData in config.FreeKits)
                {
                    _playerData[player.userID].Add(GetPlayerDataFromKit(kitData));
                }

                foreach (var kitData in config.PaidKits)
                {
                    _playerData[player.userID].Add(GetPlayerDataFromKit(kitData));
                }
            }
            else
            {
                foreach (var kitData in config.PaidKits)
                {
                    if (!_playerData[player.userID].Exists(data => data.KitKey == kitData.Key))
                    {
                        _playerData[player.userID].Add(GetPlayerDataFromKit(kitData));
                    }
                }

                foreach (var kitData in config.FreeKits)
                {
                    if (!_playerData[player.userID].Exists(data => data.KitKey == kitData.Key))
                    {
                        _playerData[player.userID].Add(GetPlayerDataFromKit(kitData));
                    }
                }
            }
        }

        void InitAutoKit(BasePlayer player)
        {
            KitData kit = new KitData
            {
                Cooldown = 0,
                DisplayName = "",
                ImageUrl = "",
                IsAuto = true,
                AutoPriority = -1,
                Key = "nullauto",
                Permission = "kits.default",
                MaxUse = 0,
                Items = new List<KitItem>
                {
                    new KitItem
                    {
                        Shortname = "torch", Amount = 1, IsBlueprint = false, Position = ItemPosition.Belt, SkinId = 0
                    },
                    new KitItem
                    {
                        Shortname = "rock", Amount = 1, IsBlueprint = false, Position = ItemPosition.Belt, SkinId = 0
                    }
                }
            };
            foreach (var kitData in autoList)
            {
                if (permission.UserHasPermission(player.UserIDString, kitData.Permission) &&
                    kit.AutoPriority < kitData.AutoPriority)
                {
                    kit = kitData;
                }
            }

            if (!_autoKits.ContainsKey(player.userID))
            {
                _autoKits.Add(player.userID, kit);
            }
        }

        PlayerKit GetPlayerDataFromKit(KitData kitData)
        {
            PlayerKit playerKit = new PlayerKit
                {KitKey = kitData.Key, Time = CurTime(), Uses = kitData.MaxUse > 0 ? kitData.MaxUse : -1};
            return playerKit;
        }

        void ClearData()
        {
            _playerData = new Dictionary<ulong, List<PlayerKit>>();
            SaveData();
        }

        #endregion

        #region hooks

        void OnPlayerRespawned(BasePlayer player)
        {
            player.inventory.Strip();
            if (_autoKits.ContainsKey(player.userID))
            {
                ClaimAutoKit(player,_autoKits[player.userID]);
            }
            else
            {
                InitAutoKit(player);
                timer.Once(0.1f, () => ClaimAutoKit(player, _autoKits[player.userID]));
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            InitPlayer(player);
        }

        List<KitData> kitList = new List<KitData>();
        List<KitData> freeList = new List<KitData>();
        List<KitData> paidList = new List<KitData>();
        List<KitData> autoList = new List<KitData>();

        void OnServerInitialized()
        {
            ImageLibrary.Call("AddImage", "https://lc-crb.ru/CyberPunk/fMDPD7s.png", "kitback");
            permission.RegisterPermission("kits.default", this);
            foreach (var kitData in config.PaidKits)
            {
                if (!kitData.IsAuto) paidList.Add(kitData);
                else autoList.Add(kitData);
                kitList.Add(kitData);
                permission.RegisterPermission(kitData.Permission, this);
                foreach (var item in kitData.Items)
                {
                    ImageLibrary.Call("AddImage", $"https://rustlabs.com/img/items180/{item.Shortname}.png",
                        item.Shortname);
                }

                ImageLibrary.Call("AddImage", kitData.ImageUrl, kitData.ImageUrl);
            }

            if (!permission.GroupHasPermission("default", "kits.default"))
            {
                permission.GrantGroupPermission("default", "kits.default", this);
            }

            foreach (var kitData in config.FreeKits)
            {
                kitList.Add(kitData);
                if (!kitData.IsAuto) freeList.Add(kitData);
                else autoList.Add(kitData);
                foreach (var item in kitData.Items)
                {
                    ImageLibrary.Call("AddImage", $"https://rustlabs.com/img/items180/{item.Shortname}.png",
                        item.Shortname);
                }

                ImageLibrary.Call("AddImage", kitData.ImageUrl, kitData.ImageUrl);
            }

            LoadData();
            foreach (var player in BasePlayer.activePlayerList) InitPlayer(player);
        }


        void Unload()
        {
            SaveData();
        }

        void OnNewSave(string filename) => ClearData();

        #endregion

        #region methods

        #region Kit

        void ClaimAutoKit(BasePlayer player, KitData kit)
        {
            foreach (var kitItem in kit.Items)
            {
                if (kitItem.IsBlueprint)
                {
                    Item item = ItemManager.CreateByPartialName(kitItem.Shortname);
                    Item create = ItemManager.CreateByItemID(-996920608);

                    var info = ItemManager.FindItemDefinition(item.info.shortname);
                    create.blueprintTarget = info.itemid;
                    if (kitItem.Position == ItemPosition.Belt)
                    {
                        create.MoveToContainer(player.inventory.containerBelt);
                    }
                    if (kitItem.Position == ItemPosition.Main)
                    {
                        create.MoveToContainer(player.inventory.containerMain);
                    }
                    if (kitItem.Position == ItemPosition.Wear)
                    {
                        create.MoveToContainer(player.inventory.containerWear);
                    }
                }
                else
                {
                    Item item = ItemManager.CreateByPartialName(kitItem.Shortname,kitItem.Amount,kitItem.SkinId);
                    
                    if (kitItem.Position == ItemPosition.Belt)
                    {
                        item.MoveToContainer(player.inventory.containerBelt);
                    }
                    if (kitItem.Position == ItemPosition.Main)
                    {
                        item.MoveToContainer(player.inventory.containerMain);
                    }
                    if (kitItem.Position == ItemPosition.Wear)
                    {
                        item.MoveToContainer(player.inventory.containerWear);
                    }
                }
            }
        }

        bool CanClaimKit(BasePlayer player, KitData kit)
        {
            string key = kit.Key;
            PlayerKit playerKit = _playerData[player.userID].Find(data => data.KitKey == key);
            if (!permission.UserHasPermission(player.UserIDString,kit.Permission)| playerKit.Uses == 0 || playerKit.Time>CurTime())
                return false;
            int playerMain = player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count;
            int playerBelt = player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count;
            int playerWear = player.inventory.containerWear.capacity - player.inventory.containerWear.itemList.Count;
            int kitMain = 0, kitBelt = 0,kitWear = 0;
            foreach (var kitItem in kit.Items)
            {
                if (kitItem.Position == ItemPosition.Belt) kitBelt++;
                if (kitItem.Position == ItemPosition.Main) kitMain++;
                if (kitItem.Position == ItemPosition.Wear) kitWear++;
            }

            if (kitBelt>playerBelt)
            {
                return false;
            }
            
            if (kitMain>playerMain)
            {
                return false;
            }
            if (kitWear>playerWear)
            {
                return false;
            }
            return true;
        }

        bool CheckPlayerClaim(BasePlayer player, KitData kit)
        {
            string key = kit.Key;
            PlayerKit playerKit = _playerData[player.userID].Find(data => data.KitKey == key);
            if (!permission.UserHasPermission(player.UserIDString,kit.Permission))
            {
                ReasonUI(player,$"Требуется привилегия",kit.DisplayName);
            }

            if (playerKit.Uses == 0)
            {
                ReasonUI(player,$"Закончился. Будет доступен в следующем вайпе",kit.DisplayName);
            }

            if (playerKit.Time>CurTime())
            {
                ReasonUI(player,$"Перезаряжается",kit.DisplayName);
            }
            if (!permission.UserHasPermission(player.UserIDString,kit.Permission)| playerKit.Uses == 0 || playerKit.Time>CurTime())
                return false;
            int playerMain = player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count;
            int playerBelt = player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count;
            int playerWear = player.inventory.containerWear.capacity - player.inventory.containerWear.itemList.Count;
            int kitMain = 0, kitBelt = 0,kitWear = 0;
            foreach (var kitItem in kit.Items)
            {
                if (kitItem.Position == ItemPosition.Belt) kitBelt++;
                if (kitItem.Position == ItemPosition.Main) kitMain++;
                if (kitItem.Position == ItemPosition.Wear) kitWear++;
            }

            if (kitBelt>playerBelt)
            {
                ReasonUI(player,$"Не хватает {kitBelt-playerBelt} места в поясе",kit.DisplayName);
                return false;
            }
            
            if (kitMain>playerMain)
            {
                ReasonUI(player,$"Не хватает {kitBelt-playerBelt} места в инвентаре",kit.DisplayName);
                return false;
            }
            if (kitWear>playerWear)
            {
                ReasonUI(player,$"Не хватает {kitBelt-playerBelt} места для одежды",kit.DisplayName);
                return false;
            }
            return true;
        }

        void ClaimKit(BasePlayer player, KitData kit)
        {
            CheckPlayerClaim(player, kit);
            foreach (var kitItem in kit.Items)
            {
                if (kitItem.IsBlueprint)
                {
                    Item item = ItemManager.CreateByPartialName(kitItem.Shortname);
                    Item create = ItemManager.CreateByItemID(-996920608);

                    var info = ItemManager.FindItemDefinition(item.info.shortname);
                    create.blueprintTarget = info.itemid;
                    if (kitItem.Position == ItemPosition.Belt)
                    {
                        create.MoveToContainer(player.inventory.containerBelt);
                    }
                    if (kitItem.Position == ItemPosition.Main)
                    {
                        create.MoveToContainer(player.inventory.containerMain);
                    }
                    if (kitItem.Position == ItemPosition.Wear)
                    {
                        create.MoveToContainer(player.inventory.containerWear);
                    }
                }
                else
                {
                    Item item = ItemManager.CreateByPartialName(kitItem.Shortname,kitItem.Amount,kitItem.SkinId);
                    
                    if (kitItem.Position == ItemPosition.Belt)
                    {
                        item.MoveToContainer(player.inventory.containerBelt);
                    }
                    if (kitItem.Position == ItemPosition.Main)
                    {
                        item.MoveToContainer(player.inventory.containerMain);
                    }
                    if (kitItem.Position == ItemPosition.Wear)
                    {
                        item.MoveToContainer(player.inventory.containerWear);
                    }
                }
            }
            PlayerKit playerKit = _playerData[player.userID].Find(data => data.KitKey == kit.Key);
            playerKit.Time = CurTime() + kit.Cooldown;
            if (playerKit.Uses > 0)
                playerKit.Uses--;
            SaveData();
        }

        #endregion
        
        #region UI

        private string _reasonLayer = "ReasonUI";
        void ReasonUI(BasePlayer player, string reason,string name)
        {
            Broadcast.Call("GetPlayerNotice", player, $"Набор {name}", reason,"","",3f);
            /*CuiHelper.DestroyUi(player,_reasonLayer);
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.45 0",AnchorMax = "0.55 0",OffsetMin = "0 -50",OffsetMax = "0 -5"},
                Image = {Color = "0 0 0 0.3",FadeIn = 0.05f,Material = "assets/content/ui/uibackgroundblur.mat"},
                FadeOut = 0.05f
            }, "ContentUI", _reasonLayer);
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMax = "1 1",AnchorMin = "0 0"},
                Text = {Text = reason,Align = TextAnchor.MiddleCenter,FadeIn = 0.1f,Color = "1 1 1 0.9",FontSize = 18},
                FadeOut = 0.1f
            }, _reasonLayer,"ReasonText");
            
            CuiHelper.AddUi(player, container);
            timer.Once(2f, () => CuiHelper.DestroyUi(player, _reasonLayer));*/
        }

        private string _layer = "Kits.UI";
        string _mainLayer = "Kits.Main";
        private string greyout = "assets/icons/greyout.mat";
        private string blur = "assets/content/ui/uibackgroundblur.mat";
        void OpenUI(BasePlayer player, int freePage = 0,int paidPage = 0)
        {
            CuiHelper.DestroyUi(player, _layer);
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = {Color = "0 0 0 0"},
                RectTransform = {AnchorMin = "0 0 ",AnchorMax = "1 1"}
            }, "ContentUI", _layer);
            
            
            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},    
                RectTransform = {AnchorMin = "0.1 0.15",AnchorMax = "0.85 0.9"}
            }, _layer,_mainLayer);
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.9",AnchorMax = "1 1",OffsetMin = "220 10",OffsetMax = "-220 -10"},
                Image = {Color = "0.59 0.18 0.18 0.5"}
            }, _mainLayer,"FreeName");
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.4",AnchorMax = "1 0.5",OffsetMin = "220 10",OffsetMax = "-220 -10"},
                Image = {Color = "0.59 0.18 0.18 0.5"}
            }, _mainLayer,"PaidName");
            container.Add(new CuiPanel
            {
                Image = {Color = "0.59 0.18 0.18 0.5"},
                RectTransform = {AnchorMin = "0 1",AnchorMax = "1 1",OffsetMin = "0 12",OffsetMax = "0 15"}
            }, "PaidName");
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0",AnchorMax = "1 1"},
                Text = {Text = "БАЗОВОЕ СНАРЯЖЕНИЕ",Align = TextAnchor.MiddleCenter,Font = "robotocondensed-bold.ttf",Color = "1 1 1 0.9",FontSize = 18}
            }, "FreeName");
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0",AnchorMax = "1 1"},
                Text = {Text = "ЭЛИТНОЕ СНАРЯЖЕНИЕ",Align = TextAnchor.MiddleCenter,Font = "robotocondensed-bold.ttf",Color = "1 1 1 0.9",FontSize = 18}
            }, "PaidName");
            /*container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "1 1",AnchorMax = "1 1",OffsetMin = "-27 -27",OffsetMax = "-2 -2"},
                Image = {Color = "0.8 0.28 0.2 1",Material = greyout}
            }, _mainLayer, "Close");
            container.Add(new CuiButton
            {
                Button = {Close = _layer,Color = "1 1 1 0.9",Sprite = "assets/icons/close.png"},
                Text = {Text = ""}
            }, "Close");*/
            if (freeList.Count>4*(freePage+1))
            {
                container.Add(new CuiPanel
                {
                    Image = {Color = "0.82 0.78 0.59 0.0"},
                    RectTransform = {AnchorMin = "0.97 0.55",AnchorMax = "0.99 0.9"}
                },_mainLayer,"free_Forward");
                container.Add(new CuiLabel
                {
                    Text = {Text = ">",Align = TextAnchor.MiddleCenter}
                },"free_Forward");
                container.Add(new CuiButton
                {
                    Text = {Text = ">", Align = TextAnchor.MiddleCenter},
                    RectTransform = {AnchorMin = "0 0",AnchorMax = "1 1"},
                    Button = {Color = "0 0 0 0",Command = $"kit {freePage+1} {paidPage}"}
                }, "free_Forward");
            }

            if (freePage>0)
            {
                container.Add(new CuiPanel
                {
                    Image = {Color = "0.4 0.45 0.27 0"},
                    RectTransform = {AnchorMin = "0.03 0.55",AnchorMax = "0.05 0.9"}
                }, _mainLayer, "free_Back");
                container.Add(new CuiButton
                {
                    Text = {Text = "<", Align = TextAnchor.MiddleCenter},
                    RectTransform = {AnchorMin = "0 0",AnchorMax = "1 1"},
                    Button = {Color = "0 0 0 0",Command = $"kit {freePage-1}  {paidPage}"}
                }, "free_Back");
            }
            if (paidList.Count>4*(paidPage+1))
            {
                container.Add(new CuiPanel
                {
                    Image = {Color = "0.4 0.45 0.27 0"},
                    RectTransform = {AnchorMin = "0.97 0.55",AnchorMax = "0.99 0.9"}
                },_mainLayer,"paid_Forward");
                container.Add(new CuiButton
                {
                    Text = {Text = ">", Align = TextAnchor.MiddleCenter},
                    RectTransform = {AnchorMin = "0 0",AnchorMax = "1 1"},
                    Button = {Color = "0 0 0 0",Command = $"kit {freePage} {paidPage+1}"}
                }, "paid_Forward");
            }
            if (paidPage>0)
            {
                container.Add(new CuiPanel
                {
                    Image = {Color = "0.4 0.45 0.27 0"},
                    RectTransform = {AnchorMin = "0.03 0.05",AnchorMax = "0.05 0.4"}
                }, _mainLayer, "paid_Back");
                container.Add(new CuiButton
                {
                    Text = {Text = "<", Align = TextAnchor.MiddleCenter},
                    RectTransform = {AnchorMin = "0 0",AnchorMax = "1 1"},
                    Button = {Color = "0 0 0 0",Command = $"kit {freePage}  {paidPage-1}"}
                }, "paid_Back");
            }
            double xmin = 0.0625;
            int index = 0;
            foreach (var kitData in freeList.Skip(freePage*4).Take(4))
            {
                double time = CurTime();
                PlayerKit playerKit = _playerData[player.userID].Find(data => data.KitKey == kitData.Key);
                bool allow = CanClaimKit(player, kitData);
                container.Add(new CuiPanel
                {
                    Image = {Color = "0.88 0.83 0.63 0.0"},
                    RectTransform = {AnchorMin = $"{xmin} 0.55",AnchorMax = $"{xmin+0.2} 0.9"}
                }, _mainLayer,_mainLayer+$"FKit{index}");
                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 0.45",AnchorMax = "1 0.57"},
                    Text = {Text = kitData.DisplayName,Align = TextAnchor.MiddleCenter,Color = "1 1 1 0.9",FontSize = 20}
                }, _mainLayer + $"FKit{index}");
                container.Add(new CuiElement
                {
                    Parent = _mainLayer + $"FKit{index}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = (string) ImageLibrary.Call("GetImage","kitback")
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",AnchorMax = "1 1"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = _mainLayer + $"FKit{index}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = (string) ImageLibrary.Call("GetImage",kitData.ImageUrl)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.3 0.48",AnchorMax = "0.7 0.88"
                        }
                    }
                });

                container.Add(new CuiPanel
                {
                    Image = {Color = "0.98 0.45 0.42 0.7",Material = greyout},
                    RectTransform = {AnchorMin = "0.83 0.03",AnchorMax = "0.98 0.17"}
                }, _mainLayer + $"FKit{index}", $"FKit{index}.info");
                container.Add(new CuiButton
                {
                    Button = {Close = _mainLayer,Color = "1 1 1 0.6",Command = $"kit.view {kitData.Key}",Sprite = "assets/icons/warning.png"},
                    Text = {Text = "",Color = "1 1 1 0.8",Align = TextAnchor.MiddleCenter},
                    RectTransform = {AnchorMin = "0.15 0.15",AnchorMax = "0.85 0.85"}
                }, $"FKit{index}.info");
                /*if (playerKit.Uses>0)
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0.05 0.17",AnchorMax = "0.95 0.3"},
                        Text = {Align = TextAnchor.MiddleCenter,Text = $"  {playerKit.Uses}"}
                    }, _mainLayer + $"FKit{index}");
                }
                if (playerKit.Uses==0)
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0.05 0.17",AnchorMax = "0.95 0.3"},
                        Text = {Align = TextAnchor.MiddleCenter,Text = $"<color=#843535>Закончился</color>"}
                    }, _mainLayer + $"FKit{index}");
                }
                if (playerKit.Uses<0)
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0.05 0.17",AnchorMax = "0.95 0.3"},
                        Text = {Align = TextAnchor.MiddleCenter,Text = $"НЕОГРАНИЧЕННО", Color = "0.88 0.83 0.63 0.8"}
                    }, _mainLayer + $"FKit{index}");
                }*/

                if (playerKit.Time>CurTime())
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0.05 0.27",AnchorMax = "0.95 0.5"},
                        Text = {Align = TextAnchor.MiddleCenter,Text = $"{FormatShortTime(TimeSpan.FromSeconds(playerKit.Time - time))}"}
                    }, _mainLayer + $"FKit{index}");
                }
                else
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0.05 0.27",AnchorMax = "0.95 0.5"},
                        Text = {Align = TextAnchor.MiddleCenter,Text = $"ГОТОВО",FontSize = 18}
                    }, _mainLayer + $"FKit{index}");
                }
                
                
                if (allow)
                {
                    container.Add(new CuiButton
                    {
                        Button = {Close = "MenuUI",Color = "0.37 0.96 1 0.9",Command = $"kit.take {kitData.Key}",Material = greyout},
                        Text = {Text = "ПОЛУЧИТЬ",Color = "0 0 0 0.95",Align = TextAnchor.MiddleCenter},
                        RectTransform = {AnchorMin = "0.05 0.03",AnchorMax = "0.81 0.17"}
                    }, _mainLayer + $"FKit{index}");
                    
                }
                else
                {
                    
                    container.Add(new CuiButton
                    {
                        Button = {Color = "0.59 0.18 0.18 0.8",Command = $"kit.take {kitData.Key}",Material = greyout},
                        Text = {Text = "НЕДОСТУПЕН",Color = "0.98 0.44 0.41 1",Align = TextAnchor.MiddleCenter},
                        RectTransform = {AnchorMin = "0.05 0.03",AnchorMax = "0.81 0.17"}
                    }, _mainLayer + $"FKit{index}");
                }
                xmin += 0.225;
                index++;
            }

            #region paid Kits

            xmin = 0.0625;
            index = 0;
            foreach (var kitData in paidList.Skip(paidPage*4).Take(4))
            {
                double time = CurTime();
                PlayerKit playerKit = _playerData[player.userID].Find(data => data.KitKey == kitData.Key);
                bool allow = CanClaimKit(player, kitData);
                container.Add(new CuiPanel
                {
                    Image = {Color = "0.88 0.83 0.63 0.0"},
                    RectTransform = {AnchorMin = $"{xmin} 0.05",AnchorMax = $"{xmin+0.2} 0.4"}
                }, _mainLayer,_mainLayer+$"PKit{index}");
                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 0.45",AnchorMax = "1 0.57"},
                    Text = {Text = kitData.DisplayName,Align = TextAnchor.MiddleCenter,Color = "1 1 1 0.9",FontSize = 20}
                }, _mainLayer + $"PKit{index}");
                container.Add(new CuiElement
                {
                    Parent = _mainLayer + $"PKit{index}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = (string) ImageLibrary.Call("GetImage","kitback")
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",AnchorMax = "1 1"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = _mainLayer + $"PKit{index}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = (string) ImageLibrary.Call("GetImage",kitData.ImageUrl)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.3 0.48",AnchorMax = "0.7 0.88"
                        }
                    }
                });

                container.Add(new CuiPanel
                {
                    Image = {Color = "0.98 0.45 0.42 0.7",Material = greyout},
                    RectTransform = {AnchorMin = "0.83 0.03",AnchorMax = "0.98 0.17"}
                }, _mainLayer + $"PKit{index}", $"PKit{index}.info");
                container.Add(new CuiButton
                {
                    Button = {Close = _mainLayer,Color = "1 1 1 0.6",Command = $"kit.view {kitData.Key}",Sprite = "assets/icons/warning.png"},
                    Text = {Text = "",Color = "1 1 1 0.8",Align = TextAnchor.MiddleCenter},
                    RectTransform = {AnchorMin = "0.15 0.15",AnchorMax = "0.85 0.85"}
                }, $"PKit{index}.info");
                /*if (playerKit.Uses>0)
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0.05 0.17",AnchorMax = "0.95 0.3"},
                        Text = {Align = TextAnchor.MiddleCenter,Text = $"  {playerKit.Uses}"}
                    }, _mainLayer + $"PKit{index}");
                }
                if (playerKit.Uses==0)
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0.05 0.17",AnchorMax = "0.95 0.3"},
                        Text = {Align = TextAnchor.MiddleCenter,Text = $"<color=#843535>Закончился</color>"}
                    }, _mainLayer + $"PKit{index}");
                }
                if (playerKit.Uses<0)
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0.05 0.17",AnchorMax = "0.95 0.3"},
                        Text = {Align = TextAnchor.MiddleCenter,Text = $"НЕОГРАНИЧЕННО", Color = "0.88 0.83 0.63 0.8"}
                    }, _mainLayer + $"PKit{index}");
                }*/

                if (playerKit.Time>CurTime())
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0.05 0.27",AnchorMax = "0.95 0.5"},
                        Text = {Align = TextAnchor.MiddleCenter,Text = $"{FormatShortTime(TimeSpan.FromSeconds(playerKit.Time - time))}"}
                    }, _mainLayer + $"PKit{index}");
                }
                else
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0.05 0.27",AnchorMax = "0.95 0.5"},
                        Text = {Align = TextAnchor.MiddleCenter,Text = $"ГОТОВО",FontSize = 18}
                    }, _mainLayer + $"PKit{index}");
                }
                
                
                if (allow)
                {
                    container.Add(new CuiButton
                    {
                        Button = {Close = "MenuUI",Color = "0.88 0.83 0.63 0.9",Command = $"kit.take {kitData.Key}",Material = greyout},
                        Text = {Text = "ПОЛУЧИТЬ",Color = "0.47 0 0 0.95",Align = TextAnchor.MiddleCenter},
                        RectTransform = {AnchorMin = "0.05 0.03",AnchorMax = "0.81 0.17"}
                    }, _mainLayer + $"PKit{index}");
                    
                }
                else
                {
                    
                    container.Add(new CuiButton
                    {
                        Button = {Color = "0.59 0.18 0.18 0.8",Command = $"kit.take {kitData.Key}",Material = greyout},
                        Text = {Text = "НЕДОСТУПЕН",Color = "0.98 0.44 0.41 1",Align = TextAnchor.MiddleCenter},
                        RectTransform = {AnchorMin = "0.05 0.03",AnchorMax = "0.81 0.17"}
                    }, _mainLayer + $"PKit{index}");
                }
                xmin += 0.225;
                index++;
            }

            #endregion
            xmin = 0.025;
            CuiHelper.AddUi(player, container);
        }

        private string _invLayer = "KitInvLayer";
        void ShowKitInventory(BasePlayer player, KitData kitData)
        {
            CuiHelper.DestroyUi(player, _invLayer);
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                RectTransform = {AnchorMin = "0.2 0.1",AnchorMax = "0.75 0.9"}
            }, _layer,_invLayer);
            container.Add(new CuiPanel
            {
                Image = {Color = "1 1 1 0.2"},
                RectTransform = {AnchorMin = "0.1 0.6",AnchorMax = "0.3 0.85"}
            },_invLayer,"KitLogo");
            container.Add(new CuiElement
            {
                Parent = "KitLogo",
                Components =
                {
                    
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage",kitData.ImageUrl)
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",AnchorMax = "1 1"
                    }
                }
            });
            container.Add(new CuiLabel
            {
                Text = {Text = $"Название: {kitData.DisplayName}",Align = TextAnchor.MiddleLeft,FontSize = 18, Font = "robotocondensed-bold.ttf"},
                RectTransform = {AnchorMin = "0.1 0.55",AnchorMax = "0.38 0.6"}
            },_invLayer);
            container.Add(new CuiLabel
            {
                Text = {Text = $"Перезарядка: {FormatShortTime(TimeSpan.FromSeconds(kitData.Cooldown))}",Align = TextAnchor.MiddleLeft,FontSize = 18, Font = "robotocondensed-bold.ttf"},
                RectTransform = {AnchorMin = "0.1 0.45",AnchorMax = "0.38 0.55"}
            },_invLayer);
            /*if (kitData.MaxUse>0)
            {
                container.Add(new CuiLabel
                {
                    Text = {Text = $"Макс. количесто: {kitData.MaxUse}",Align = TextAnchor.MiddleLeft,FontSize = 18, Font = "robotocondensed-bold.ttf"},
                    RectTransform = {AnchorMin = "0.1 0.35",AnchorMax = "0.38 0.45"}
                },_invLayer);
            }
            else
            {
                container.Add(new CuiLabel
                {
                    Text = {Text = $"Макс. количесто: Неограниченно",Align = TextAnchor.MiddleLeft,FontSize = 18, Font = "robotocondensed-bold.ttf"},
                    RectTransform = {AnchorMin = "0.1 0.35",AnchorMax = "0.45 0.45"}
                },_invLayer);
            }*/
            
            /*container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0",AnchorMax = "1 1"},
                Button = {Close = _invLayer,Color = "0 0 0 0"},
                Text = {Text = ""}
            }, _invLayer);*/
            
            container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                    Image = { Color = "0 0 0 0" }
                }, _invLayer, "Items");
                

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"0.1 0", AnchorMax = $"0.34 0.06", OffsetMax = "0 0" },
                    Button = { Color = "0.59 0.18 0.18 0.9", Command = "kit" },
                    Text = { Text = "НАЗАД", Align = TextAnchor.MiddleCenter,Color = "0.0 0 0 0.95",FontSize = 20, Font = "robotocondensed-bold.ttf" }
                }, _invLayer, "Back");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"0.503 0.92", AnchorMax = $"0.97 0.99", OffsetMax = "0 0" },
                    Image = { Color = "0 0 0 0.0" }
                }, _invLayer, "Inventory");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                    Text = { Text = "ИНВЕНТАРЬ", Align = TextAnchor.LowerLeft, FontSize = 20, Font = "robotocondensed-bold.ttf",Color = "1 1 1 0.9" }
                }, "Inventory");

                float width = 0.0782f, height = 0.09f, startxBox = 0.503f, startyBox = 0.915f - height, xmin = startxBox, ymin = startyBox;
                for (int z = 0; z < 24; z++)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMin = "2 2", OffsetMax = "-2 -2" },
                        Button = { Color = "1 1 1 0.05", Command = $"",Material = greyout},
                        Text = { Text = $""}
                    }, "Items");

                    xmin += width;
                    if (xmin + width>= 1)
                    {
                        xmin = startxBox;
                        ymin -= height;
                    }
                }

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"0.505 0.45", AnchorMax = $"0.97 0.52", OffsetMax = "0 0" },
                    Image = { Color = "0 0 0 0.0" }
                }, _invLayer, "Clothing");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                    Text = { Text = "ОДЕЖДА", Align = TextAnchor.LowerLeft, FontSize = 20, Font = "robotocondensed-bold.ttf",Color = "1 1 1 0.9" }
                }, "Clothing");

                float width1 = 0.0782f, height1 = 0.09f, startxBox1 = 0.503f, startyBox1 = 0.445f - height1, xmin1 = startxBox1, ymin1 = startyBox1;
                for (int z = 0; z < 6; z++)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + height1 * 1}", OffsetMin = "2 2", OffsetMax = "-2 -2" },
                        Button = { Color = "1 1 1 0.05", Command = $"",Material = greyout },
                        Text = { Text = $"", }
                    }, "Items");

                    xmin1 += width1;
                    if (xmin1 + width1>= 1)
                    {
                        xmin1 = startxBox1;
                        ymin1 -= height1;
                    }
                }
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{startxBox1} {startyBox1-0.09}", AnchorMax = $"{startxBox1 + width1} {startyBox1 + height1 * 1-0.09}", OffsetMin = "2 2", OffsetMax = "-2 -2" },
                    Button = { Color = "1 1 1 0.05", Command = $"",Material = greyout  },
                    Text = { Text = $"",}
                }, "Items");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"0.505 {0.238-width1-0.01}", AnchorMax = $"0.97 {0.322 - width1-0.01}", OffsetMax = "0 0" },
                    Image = { Color = "1 1 1 0.0" }
                }, _invLayer, "HotBar");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                    Text = { Text = "ПОЯС", Align = TextAnchor.LowerLeft, FontSize = 20, Font = "robotocondensed-bold.ttf",Color = "1 1 1 0.9"}
                }, "HotBar");

                float width2 = 0.0782f, height2 = 0.09f, startxBox2 = 0.503f, startyBox2 = 0.235f - height2, xmin2 = startxBox2, ymin2 = startyBox2;
                for (int z = 0; z < 6; z++)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = $"{xmin2} {ymin2-width1-0.01}", AnchorMax = $"{xmin2 + width2} {ymin2 + height2 * 1-width1-0.01}", OffsetMin = "2 2", OffsetMax = "-2 -2" },
                        Button = { Color = "1 1 1 0.05", Command = $"",Material = greyout },
                        Text = { Text = $""}
                    }, "Items");

                    xmin2 += width2;
                    if (xmin2 + width2>= 1)
                    {
                        xmin2 = startxBox2;
                        ymin2 -= height2;
                    }
                }

                float width3 = 0.0782f, height3 = 0.09f, startxBox3 = 0.503f, startyBox3 = 0.915f - height3, xmin3 = startxBox3, ymin3 = startyBox3;
                float width4 = 0.0782f, height4 = 0.09f, startxBox4 = 0.503f, startyBox4 = 0.445f - height4, xmin4 = startxBox4, ymin4 = startyBox4;
                float width5 = 0.0782f, height5 = 0.09f, startxBox5 = 0.503f, startyBox5 = 0.235f - height5, xmin5 = startxBox5, ymin5 = startyBox5;
                foreach (var item in kitData.Items)
                {
                    if (item.Position == ItemPosition.Main)
                    {
                        container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = $"{xmin3} {ymin3}", AnchorMax = $"{xmin3 + width3} {ymin3 + height3 * 1}", OffsetMin = "2 2", OffsetMax = "-2 -2" },
                            Button = { Color = "0 0 0 0", Command = $"" },
                            Text = { Text = $"x{item.Amount} ", Align = TextAnchor.LowerRight, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                        }, "Items", "Item");

                        container.Add(new CuiElement
                        {
                            Parent = "Item",
                            Components =
                            {
                                new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", item.Shortname), FadeIn = 0.5f},
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "8 8", OffsetMax = "-8 -8" }
                            }
                        });

                        xmin3 += width3;
                        if (xmin3 + width3  >= 1)
                        {
                            xmin3 = startxBox3;
                            ymin3 -= height3;
                        }
                    }
                    if (item.Position == ItemPosition.Wear)
                    {
                        container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = $"{xmin4} {ymin4}", AnchorMax = $"{xmin4 + width4} {ymin4 + height4 * 1}", OffsetMin = "2 2", OffsetMax = "-2 -2" },
                            Button = { Color = "0 0 0 0", Command = $"" },
                            Text = { Text = $"x{item.Amount} ", Align = TextAnchor.LowerRight, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                        }, "Items", "Item");
                        
                        container.Add(new CuiElement
                        {
                            Parent = "Item",
                            Components =
                            {
                                new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", item.Shortname), FadeIn = 0.5f },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "8 8", OffsetMax = "-8 -8" }
                            }
                        });

                        xmin4 += width4;
                        if (xmin4 + width4>= 1)
                        {
                            xmin4 = startxBox1;
                            ymin4 -= height1;
                        }
                    }
                    if (item.Position == ItemPosition.Belt)
                    {
                        container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = $"{xmin5} {ymin5-width1-0.01}", AnchorMax = $"{xmin5 + width5} {ymin5 + height5 * 1-width1-0.01}", OffsetMin = "2 2", OffsetMax = "-2 -2" },
                            Button = { Color = "0 0 0 0", Command = $"" },
                            Text = { Text = $"x{item.Amount} ", Align = TextAnchor.LowerRight, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                        }, "Items", "Item");

                        container.Add(new CuiElement
                        {
                            Parent = "Item",
                            Components =
                            {
                                new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", item.Shortname), FadeIn = 0.5f },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "8 8", OffsetMax = "-8 -8" }
                            }
                        });

                        xmin5 += width5;
                    }
                }

                CuiHelper.AddUi(player, container);
        }
        
        #endregion
        
        

        #endregion

        #region commnds

        [ConsoleCommand("kit")]
        void OpenUIConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
            {
                return;
            }

            if (!arg.HasArgs())
            {
                OpenUI(arg.Player());
            }
            else
            {
                OpenUI(arg.Player(),arg.Args[0].ToInt());
            }
        }

        [ChatCommand("kit")]
        void OpenUICommand(BasePlayer player, string command,string[] args)
        {
            if (args.Length<1)
                OpenUI(player);
            else
                OpenUI(player,args[0].ToInt());
        }

        [ConsoleCommand("kit.view")]
        void ViewKitInventory(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null || !arg.HasArgs(1)) return;
            BasePlayer player = arg.Player();
            KitData data = kitList.FirstOrDefault(p => p.Key == arg.Args[0]);
            ShowKitInventory(player,data);
        }

        [ConsoleCommand("kit.take")]
        void TakeKitCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.IsClientside && !arg.HasArgs(1))
                return;
            KitData kit = kitList.Find(data => data.Key == arg.Args[0]);
            if (kit == null)
            {
                PrintWarning($"Kit with key {arg.Args[0]} not exists");
                return;
            }

            BasePlayer player = arg.Player();
            if (player == null) return;
            if (CheckPlayerClaim(player, kit)) ClaimKit(player,kit);
        }

        

        #endregion


        #region Admin

        enum KitSetting
        {
            Name,
            Cooldown,
            Amount,
            Free,
            Permission,
            Auto,
            AutoPriority,
            Img
        }
        private KitData tempKit = new KitData();
        Dictionary<ulong,KitSetting> AdminAddingKit = new Dictionary<ulong,KitSetting>();
            
        [ChatCommand("addkit")]
        void AdminAddKit(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            foreach (var item in player.inventory.containerMain.itemList)
            {
                if (item.info.itemid != -996920608)
                {
                    tempKit.Items.Add(new KitItem
                    {
                        Shortname = item.info.shortname,
                        Amount = item.amount,
                        IsBlueprint = false,
                        SkinId = item.skin,
                        Position = ItemPosition.Main
                    });
                }
                else
                {
                    tempKit.Items.Add(new KitItem
                    {
                        Shortname = item.blueprintTargetDef.shortname,
                        Amount = item.amount,
                        IsBlueprint = true,
                        SkinId = item.skin,
                        Position = ItemPosition.Main
                    });
                }
            }
            foreach (var item in player.inventory.containerBelt.itemList)
            {
                if (item.info.itemid != -996920608)
                {
                    tempKit.Items.Add(new KitItem
                    {
                        Shortname = item.info.shortname,
                        Amount = item.amount,
                        IsBlueprint = false,
                        SkinId = item.skin,
                        Position = ItemPosition.Belt
                    });
                }
                else
                {
                    tempKit.Items.Add(new KitItem
                    {
                        Shortname = item.blueprintTargetDef.shortname,
                        Amount = item.amount,
                        IsBlueprint = true,
                        SkinId = item.skin,
                        Position = ItemPosition.Belt
                    });
                }
            }
            foreach (var item in player.inventory.containerWear.itemList)
            {
                    tempKit.Items.Add(new KitItem
                    {
                        Shortname = item.info.shortname,
                        Amount = item.amount,
                        IsBlueprint = false,
                        SkinId = item.skin,
                        Position = ItemPosition.Wear
                    });
            }

            tempKit.Key = $"kit{UnityEngine.Random.Range(0,99999)}";
            AdminAddingKit.Add(player.userID,KitSetting.Name);
            SendReply(player,"Введите название набора: /k название");
        }

        [ChatCommand("k")]
        void SetSettingsForKit(BasePlayer player,string command,string[] args)
        {
            if (!player.IsAdmin || !AdminAddingKit.ContainsKey(player.userID))
            {
                
                return;
            }

            switch (AdminAddingKit[player.userID])
            {
                case KitSetting.Name:
                    tempKit.DisplayName = String.Join(" ",args);
                    AdminAddingKit[player.userID] = KitSetting.Amount;
                    SendReply(player,"Введите максимальное количество использований набора(0 если безлимитно): /k 1");
                    break;
                case KitSetting.Amount:
                    if (args != null)
                    {
                        tempKit.MaxUse = args[0].ToInt();
                        AdminAddingKit[player.userID] = KitSetting.Cooldown;
                        SendReply(player,"Введите количество секунд перезарядки: /k 3600");
                    }
                    break;
                case KitSetting.Cooldown:
                    if (args!=null)
                    {
                        tempKit.Cooldown = args[0].ToInt();
                        AdminAddingKit[player.userID] = KitSetting.Auto;
                        SendReply(player,"Набор является автокитом: /k да");
                    }
                    break;
                case KitSetting.Auto:
                    if (args!=null)
                    {
                        if (args[0] == "1" || args[0] == "true" || args[0] == "да")
                        {
                            tempKit.IsAuto = true;
                            AdminAddingKit[player.userID] = KitSetting.AutoPriority;
                            SendReply(player,"Введите приоритет автокита(значение должно быть больше 0): /k 3");
                        }
                        else
                        {
                            tempKit.IsAuto = false;
                            AdminAddingKit[player.userID] = KitSetting.Free;
                            SendReply(player,"Набор является бесплатным: /k да");
                            
                        }
                    }
                    break;
                case KitSetting.AutoPriority:
                    if (args!=null)
                    {
                        tempKit.AutoPriority = args[0].ToInt();
                        AdminAddingKit[player.userID] = KitSetting.Free;
                        SendReply(player,"Набор является бесплатным: /k да");
                    }
                    break;
                case KitSetting.Free:
                    if (args!=null)
                    {
                        if (args[0] == "1" || args[0] == "true" || args[0] == "да")
                        {
                            tempKit.Permission = "kits.default";
                            AdminAddingKit[player.userID] = KitSetting.Img;
                            SendReply(player,"Введите ссылку на изображение: /k url");
                        }
                        else
                        {
                            AdminAddingKit[player.userID] = KitSetting.Permission;
                            SendReply(player,"Введите привилегию для доступа к набору(префикс kits. добавляется автоматически): /k premium");
                        }

                        
                    }
                    break;
                case KitSetting.Permission:
                    if (args!=null)
                    {
                        tempKit.Permission = "kits."+args[0];
                        AdminAddingKit[player.userID] = KitSetting.Img;
                        SendReply(player,"Введите ссылку на изображение: /k url");
                    }
                    break;
                case KitSetting.Img:
                    if (args!=null)
                    {
                        tempKit.ImageUrl = args[0];
                        SendReply(player,$"Набор:\n" +
                                         $"    Название:{tempKit.DisplayName}\n" +
                                         $"    Перезарядка:{tempKit.Cooldown} секунд\n" +
                                         $"    Маскимально использований:{tempKit.MaxUse}\n" +
                                         $"    Является автокитов:{tempKit.IsAuto} \n " +
                                         $"    Приоритет автокита:{tempKit.AutoPriority}\n" +
                                         $"    Ссылка на изображение:{tempKit.ImageUrl}\n" +
                                         $"    Привилегия для доступа:{tempKit.Permission}\n" +
                                         $"\n");
                        
                    }

                    if (tempKit.Permission != "kits.default")
                    {
                        config.PaidKits.Add(tempKit);
                        SaveConfig();
                        SendReply(player,"Набор сохранен в платные");
                    }
                    else
                    {
                        config.FreeKits.Add(tempKit);
                        SaveConfig();
                        SendReply(player,"Набор сохранен в бесплатные");
                    }

                    AdminAddingKit.Remove(player.userID);
                    break;
            }
            
        }

        #endregion

        #region Helper

        public static string FormatShortTime(TimeSpan time)
        {
            string result = string.Empty;
            result = $"{time.Hours.ToString("00")}:";
            result += $"{time.Minutes.ToString("00")}:";
            result += $"{time.Seconds.ToString("00")}";
            return result;
        }

        double CurTime() => new TimeSpan(DateTime.UtcNow.Ticks).TotalSeconds;

        #endregion
    }
}