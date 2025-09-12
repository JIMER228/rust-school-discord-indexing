using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using Facepunch.Extend;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Global = Rust.Global;

namespace Oxide.Plugins
{
    [Info("SkuliSkins", "Skuli Dropek", "1.6.3")]
    public class SkuliSkins : RustPlugin
    {
        #region Cfg

        private const bool En = true;
        class DataPlayer
        {
            [JsonProperty(En ? "List of favorite skins" : "Список избранных скинов")]
            public Dictionary<string, Dictionary<ulong, ItemData.SkinInfo>> _izvSkins =
                new Dictionary<string, Dictionary<ulong, ItemData.SkinInfo>>();
            [JsonProperty(En ? "List of default skins supplied" : "Список поставленных скинов по дефолту")]
            public Dictionary<string, DefaultSkins> _defaultSkins =
                new Dictionary<string, DefaultSkins>();
            [NonSerialized]
            public BaseEntity Prefab;
        }

        class DefaultSkins
        {
            [JsonProperty(En ? "SkinID" : "СкинАйди")]
            public ulong skinId;
            [JsonProperty(En ? "Information about the skin" : "Информация о скине")]
            public ItemData.SkinInfo SkinInfo;
        }
        private Dictionary<ulong, DataPlayer> playersData = new Dictionary<ulong, DataPlayer>();
        private static ConfigData cfg { get; set; }


        private class ConfigData
        {
            internal class MarketSystem
            {
                [JsonProperty(En ? "Enabled(true = yes)" : "Включить?(true = да)")]
                public bool IsEnabled = false;
                [JsonProperty(En ? "How to pay?(Scrap, ServerRewards, Economics)" : "Чем платить?(Scrap, ServerRewards, Economics)")]
                public string WhoMarket = "Scrap";
                [JsonProperty(En ? "The price for opening the interface (If 0, then free)" : "Цена за открытие интерфейса(Если 0, то бесплатно)")]
                public int costOpen = 10;
                [JsonProperty(En ? "Price per change of one skin (If 0, then free)" : "Цена за смену одного скина(Если 0, то бесплатно)")]
                public int costChange = 10;
            }
            internal class CuiBuilderSettings
            {
                [JsonProperty(En ? "Menu color" : "Цвет менюшки")]
                public string MenuColor = "#535151";
                [JsonProperty(En ? "Text color" : "Цвет текста")]
                public string TextColor = "#beb5ac";
                [JsonProperty(En ? "The number of pits displayed when using the /skinentity command" : "Количество ячек отображаемое при использования команды /skinentity")]
                public byte EntityMenuCellsCount = 24;
                [JsonProperty(En ? "The number of pits displayed when using the /skin command" : "Количество ячек отображаемое при использования команды /skin")]
                public byte StandartMenuCellsCount = 24;
            }

            public CuiBuilderSettings MenuSettings = new CuiBuilderSettings();

            [JsonProperty(En ? "Menu commands open" : "Команды открывающие менюшку")]
            public List<string> MenuCommandsOpen = new List<string>();
            [JsonProperty(En ? "Entity Menu commands open" : "Команды для открытия скинов при наведении на предмет")]
            public List<string> EntityMenuCommandsOpen = new List<string>();
            [JsonProperty(En ? "Add a skin to the cfg automatically?(true = yes)" : "Добавить в кфг автоматически скин?(true = да)")]
            public bool IsApproved = false;
           // [JsonProperty(En ? "Vip permision" : "Пермиссион для вип игроков")]
           // public string VipPerm = "skuliskins.vip";
            [JsonProperty(En ? "Rights to use the skins system" : "Права для использование системы скинов.")]
            public string canuse = "skuliskins.use";
            [JsonProperty(En ? "Install the skin automatically on items where there is already a skin?(true = yes)" : "Устанавилвать скин автоматически на предметы где уже есть скин?(true = да)")]
            public bool SkinAuto = false;
            [JsonProperty(En ? "Rights to use default skins" : "Права для использование скинов по дефолту")]
            public string canusedefault = "skuliskins.usedefault";
            [JsonProperty(En ? "Setting up payment for opening" : "Настройка оплаты за открытие")]
            public MarketSystem Market = new MarketSystem();
            [JsonProperty(En ? "Items with these skins cannot be exchanged." : "Предметы с этими скинами нельзя будет поменять.")]
            public List<ulong> blackList = new List<ulong>();
            [JsonProperty(En ? "Rights to use unique skins" : "Права для использование уникальных скинов")]
            public Dictionary<string, List<ulong>> useSkins = new Dictionary<string, List<ulong>>();
            public static ConfigData GetNewConf()
            {
                var newConfig = new ConfigData
                {
                    MenuCommandsOpen = new List<string>() { "skin" },
                    EntityMenuCommandsOpen = new List<string>() { "skinentity" },
                    useSkins = new Dictionary<string, List<ulong>>()
                    {
                        ["skuliskins.prem"] = new List<ulong>()
                        {
                            2668297561,
                            2649480126,
                            2599664731,
                        }
                    },
                    blackList = new List<ulong>()
                    {
                        12341234,
                    }
                };
                return newConfig;
            }
        }

        class ItemData
        {
            public class SkinInfo
            {
                [JsonProperty(En ? "Enabled skin?(true = yes)" : "Включить скин?(true = да)")]
                public bool IsEnabled = true;
                [JsonProperty(En ? "Is this skin from the developers of rust or take it in a workshop?" : "Этот скин есть от разработчиков раста или принять в воркшопе??(true = да)")]
                public bool IsApproved = true;
                [JsonProperty(En ? "Name skin" : "Название скина")]
                public string MarketName = "Warhead LR300";
                [JsonProperty(En ? "Url" : "Сыссылка")]
                public string Url = "";

            }
            public Dictionary<ulong, SkinInfo> _skinInfos = new Dictionary<ulong, SkinInfo>();
        }
        protected override void LoadDefaultConfig()
        {
            cfg = ConfigData.GetNewConf();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(cfg);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                cfg = Config.ReadObject<ConfigData>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        #endregion

        #region Interface
        private static string Layer = "UiSkuliSkins";
        private static string Overlay = "Overlay";
        private static string Regular = "robotocondensed-regular.ttf";
       // private static string Sharp = "assets/content/ui/ui.background.tile.psd";


        private void LoadIzbranoeUI(BasePlayer player, string weapon, uint itemId, int page)
        {
            DataPlayer weapons;
            if (!playersData.TryGetValue(player.userID, out weapons)) return;
            CuiHelper.DestroyUi(player, Layer + "-Izbranoe");
            var cont = new CuiElementContainer();
            cont.Add(new CuiPanel()
            {
                Image =
                {
                    Color = "0 0 0 0"
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0",
                    AnchorMax = "0.5 0",
                    OffsetMin = $"{201} {111}",
                    OffsetMax = $"{571} {231}"
                }
            }, Overlay, Layer + "-Izbranoe");

            if (!weapons._izvSkins.ContainsKey(weapon)) return;
            var tSkinsList = weapons._izvSkins[weapon];
            foreach (var keyValuePair in cfg.useSkins.Where(keyValuePair => !permission.UserHasPermission(player.UserIDString, keyValuePair.Key)))
            {
                keyValuePair.Value.ForEach(p =>
                {
                    if (tSkinsList.ContainsKey(p))
                    {
                        tSkinsList.Remove(p);
                    }
                });
            }
            for (int i = 0; i < 12; i++)
            {
                if (tSkinsList.Count - 1 >= i)
                {
                    cont.Add(new CuiElement()
                    {
                        Parent = Layer + "-Izbranoe",
                        Components =
                        {
                            new CuiRawImageComponent()
                            {
                                Png = GetImage("SKULISKINS1"+ weapon +"_" + tSkinsList.ToList()[i].Key)
                            },
                            new CuiRectTransformComponent()
                            {
                                AnchorMin =
                                    $"0 0.5",
                                AnchorMax =
                                    $"0 0.5",
                                OffsetMin =
                                    $"{2 + i * 62 - Math.Floor((double) i / 6) * 6 * 62} {5 - Math.Floor((double) i / 6) * 62}",
                                OffsetMax =
                                    $"{52 + i * 62 - Math.Floor((double) i / 6) * 6 * 62} {55 - Math.Floor((double) i / 6) * 62}"
                            }
                        }
                    });
                    cont.Add(new CuiButton()
                    {
                        Button = {
                            Color = "1 1 1 0",
                            Command = $"uiskinmenu setskin {itemId} {tSkinsList.ToList()[i].Key}"
                        },
                        Text =
                        {
                            Text = ""
                        },
                        RectTransform =
                        {
                            AnchorMin =
                                $"0 0.5",
                            AnchorMax =
                                $"0 0.5",
                            OffsetMin =
                                $"{2 + i * 62 - Math.Floor((double) i / 6) * 6 * 62} {5- Math.Floor((double) i / 6) * 62}",
                            OffsetMax =
                                $"{52 + i * 62 - Math.Floor((double) i / 6) * 6 * 62} {55 - Math.Floor((double) i / 6) * 62}"
                        }
                    }, Layer + "-Izbranoe", Layer + "-Izbranoe" + i);
                    cont.Add(new CuiButton()
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = $"{-5} {-10}",
                            OffsetMax = $"{10} {5}"
                        },
                        Button =
                        {
                            Command = $"uiskinmenu setizbranoe {weapon} {tSkinsList.ToList()[i].Key} {page} {itemId}",
                            Color = HexToRustFormat("#dead39"),
                            Sprite = "assets/icons/favourite_servers.png",
                        },
                        Text = { Text = "" }
                    }, Layer + "-Izbranoe" + i);
                    if (permission.UserHasPermission(player.UserIDString, cfg.canusedefault))
                    {
                        if (playersData.TryGetValue(player.userID, out weapons) && weapons._defaultSkins.ContainsKey(weapon) && weapons._defaultSkins[weapon].skinId == tSkinsList.ToList()[i].Key)
                        {
                            cont.Add(new CuiButton()
                            {
                                RectTransform =
                                {
                                    AnchorMin = "1 0",
                                    AnchorMax = "1 0",
                                    OffsetMin = $"{-10} {0}",
                                    OffsetMax = $"{0} {10}"
                                },
                                Button =
                                {
                                    Command = $"uiskinmenu setdefault {weapon} {tSkinsList.ToList()[i].Key} {page} {itemId}",
                                    Color = HexToRustFormat("#369f47"),
                                    Sprite = "assets/icons/power.png",
                                },
                                Text = { Text = "" }
                            }, Layer + "-Izbranoe" + i);
                        }
                        else
                        {
                            cont.Add(new CuiButton()
                            {
                                RectTransform =
                                {
                                    AnchorMin = "1 0",
                                    AnchorMax = "1 0",
                                    OffsetMin = $"{-10} {0}",
                                    OffsetMax = $"{0} {10}"
                                },
                                Button =
                                {
                                    Command = $"uiskinmenu setdefault {weapon} {tSkinsList.ToList()[i].Key} {page} {itemId}",
                                    Color = HexToRustFormat("#fb4f3b"),
                                    Sprite = "assets/icons/power.png",
                                },
                                Text = { Text = "" }
                            }, Layer + "-Izbranoe" + i);
                        }
                    }
                }
            }
            CuiHelper.AddUi(player, cont);
        }
        private void LoadWearUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer + "-Wear");
            DataPlayer dataPlayer;
            if (this.playersData.TryGetValue(player.userID, out dataPlayer) && dataPlayer.Prefab != null) return;
            var cont = new CuiElementContainer
            {
                {
                    new CuiPanel()
                    {
                        Image =
                {
                    Color = "0 0 0 0"
                },
                        RectTransform =
                {
                    AnchorMin = "0.5 0",
                    AnchorMax = "0.5 0",
                    OffsetMin = $"{-588} {115}",
                    OffsetMax = $"{-215} {169}"
                }
                    },
                    Overlay,
                    Layer + "-Wear"
                }
            };

            for (int i = 0; i < player.inventory.containerWear.capacity; i++)
            {
                var item = player.inventory.containerWear.GetSlot(i);
                if (item == null)
                    continue;

                if (!cfg.blackList.Contains(item.skin) &&
                    _loadData.ContainsKey(item.info.shortname))
                {
                    cont.Add(new CuiButton()
                    {
                        Button = {
                            Color = "0.65 0.65 0.65 0",
                            Command = $"uiskinmenu weaponSelect {item.info.shortname} {item.uid}"
                        },
                        Text =
                        {
                            Text = ""
                        },
                        RectTransform =
                        {
                            AnchorMin =
                                $"0 0",
                            AnchorMax =
                                $"0 0",
                            OffsetMin = $"{0 + i * 54 - Math.Floor((double) i / 7) * 7 * 54} {0}",
                            OffsetMax = $"{50 + i * 54 - Math.Floor((double) i / 7) * 7 * 54} {50}"
                        }
                    }, Layer + "-Wear");
                }
            }
            CuiHelper.AddUi(player, cont);
        }
        private void LoadMainUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer + "-Main");
            DataPlayer dataPlayer;
            if (this.playersData.TryGetValue(player.userID, out dataPlayer) && dataPlayer.Prefab != null) return;
            var cont = new CuiElementContainer();
            cont.Add(new CuiPanel()
            {
                Image =
                {
                    Color = "0 0 0 0"
                },
                RectTransform =
                {
                    AnchorMin = $"{0.5} {0}",
                    AnchorMax = $"{0.5} {0}",
                    OffsetMin = $"{-200 } {85}",
                    OffsetMax = $"{180} {337}"
                }
            }, Overlay, Layer + "-Main");
            for (int i = 0; i < player.inventory.containerMain.capacity; i++)
            {
                var item = player.inventory.containerMain.GetSlot(i);
                if (item == null)
                {
                    continue;
                }
                if (!cfg.blackList.Contains(item.skin) &&
                    _loadData.ContainsKey(item.info.shortname))
                {
                    cont.Add(new CuiButton()
                    {
                        Button = {
                            Color = "0.65 0.65 0.65 0",
                            Command = $"uiskinmenu weaponSelect {item.info.shortname} {item.uid}"

                        },
                        Text =
                        {
                            Text = ""
                        },
                        RectTransform =
                        {
                            AnchorMin =
                                $"0 1",
                            AnchorMax =
                                $"0 1",
                            OffsetMin =
                                $"{0 + i * 64- Math.Floor((double) i / 6) * 6 * 64} {-60 - Math.Floor((double) i / 6) * 63}",
                            OffsetMax =
                                $"{60 + i * 64 - Math.Floor((double) i / 6) * 6 * 64} {0 - Math.Floor((double) i / 6) * 63}"
                        }
                    }, Layer + "-Main");
                }
            }
            CuiHelper.AddUi(player, cont);
        }
        private void LoadBeltUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer + "-Belt");
            DataPlayer dataPlayer;
            if (this.playersData.TryGetValue(player.userID, out dataPlayer) && dataPlayer.Prefab != null) return;
            var cont = new CuiElementContainer();
            cont.Add(new CuiPanel()
            {
                Image =
                {
                    Color = "0 0 0 0"
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0",
                    AnchorMax = "0.5 0",
                    OffsetMin = $"{-200} {17}",
                    OffsetMax = $"{180} {79}"
                }
            }, Overlay, Layer + "-Belt");
            for (int i = 0; i < 6; i++)
            {
                var item = player.inventory.containerBelt.GetSlot(i);
                if (item == null)
                    continue;

                if (!cfg.blackList.Contains(item.skin) &&
                    _loadData.ContainsKey(item.info.shortname))
                {
                    cont.Add(new CuiButton()
                    {
                        Button =
                        {
                            Color = "0.65 0.65 0.65 0",
                            Command = $"uiskinmenu weaponSelect {item.info.shortname} {item.uid}"
                        },
                        Text =
                        {
                            Text = ""
                        },
                        RectTransform =
                        {
                            AnchorMin =
                                $"0 0",
                            AnchorMax =
                                $"0 0",
                            OffsetMin = $"{0 + i * 64 - Math.Floor((double) i / 6) * 6 * 64} {0}",
                            OffsetMax = $"{60+ i * 64 - Math.Floor((double) i / 6) * 6 * 64} {60}"
                        }
                    }, Layer + "-Belt");
                }
            }
            CuiHelper.AddUi(player, cont);
        }
        [ConsoleCommand("uiskinmenu")]
        void ConsoleCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            Effect Sound1 = new Effect("assets/bundled/prefabs/fx/notice/loot.drag.grab.fx.prefab", player, 0, new Vector3(), new Vector3());
            string weapon;
            ulong skinId;
            int page;
            int itemId;
            DataPlayer playerData;
            switch (arg.Args[0])
            {
                case "page":
                    EffectNetwork.Send(Sound1, player.Connection);
                    LoadSkinsUI(player, arg.Args[1], arg.Args[2].ToInt(), uint.Parse(arg.Args[3]));
                    LoadIzbranoeUI(player, arg.Args[1], uint.Parse(arg.Args[3]), arg.Args[2].ToInt());
                    break;
                case "weaponSelect":
                    EffectNetwork.Send(Sound1, player.Connection);
                    LoadSkinsUI(player, arg.Args[1], 1, uint.Parse(arg.Args[2]));
                    LoadIzbranoeUI(player, arg.Args[1], uint.Parse(arg.Args[2]), 1);
                    break;
                case "setizbranoe":
                    EffectNetwork.Send(Sound1, player.Connection);
                    if (!this.playersData.TryGetValue(player.userID, out playerData)) return;
                    weapon = arg.Args[1];
                    skinId = ulong.Parse(arg.Args[2]);
                    page = arg.Args[3].ToInt();
                    itemId = arg.Args[4].ToInt();
                    if (playerData._izvSkins.ContainsKey(weapon) && playerData._izvSkins[weapon].ContainsKey(skinId))
                    {
                        playerData._izvSkins[weapon].Remove(skinId);
                    }
                    else if (playerData._izvSkins.ContainsKey(weapon))
                    {
                        playerData._izvSkins[weapon].Add(skinId, _loadData[weapon][skinId]);
                    }
                    else
                    {
                        playerData._izvSkins.Add(weapon, new Dictionary<ulong, ItemData.SkinInfo>()
                        {
                            [skinId] = _loadData[weapon][skinId]
                        });
                    }
                    LoadSkinsUI(player, weapon, page, (uint)itemId);
                    LoadIzbranoeUI(player, weapon, (uint)itemId, page);
                    break;
                case "setdefault":
                    EffectNetwork.Send(Sound1, player.Connection);
                    if (!this.playersData.TryGetValue(player.userID, out playerData)) return;
                    weapon = arg.Args[1];
                    skinId = ulong.Parse(arg.Args[2]);
                    page = arg.Args[3].ToInt();
                    itemId = arg.Args[4].ToInt();
                    if (playerData._defaultSkins.ContainsKey(weapon) && playerData._defaultSkins[weapon].skinId == skinId)
                    {
                        playerData._defaultSkins.Remove(weapon);
                    }
                    else if (playerData._defaultSkins.ContainsKey(weapon))
                    {
                        playerData._defaultSkins[weapon].skinId = skinId;
                        playerData._defaultSkins[weapon].SkinInfo = _loadData[weapon][skinId];
                    }
                    else
                    {
                        playerData._defaultSkins.Add(weapon, new DefaultSkins()
                        {
                            skinId = skinId,
                            SkinInfo = _loadData[weapon][skinId]
                        });
                    }
                    LoadSkinsUI(player, weapon, page, (uint)itemId);
                    LoadIzbranoeUI(player, weapon, (uint)itemId, page);
                    break;
                case "setskin":
                    skinId = ulong.Parse(arg.Args[2]);

                    EffectNetwork.Send(Sound1, player.Connection);
                    if (cfg.Market.IsEnabled && !permission.UserHasPermission(player.UserIDString, cfg.canuse))
                    {
                        if (ChangeSkin(player) == false)
                        {
                            return;
                        }
                    }
                    if (!this.playersData.TryGetValue(player.userID, out playerData)) return;
                    if (playerData.Prefab == null)
                    {
                        var uid = arg.Args[1];
                        var item = player.inventory.FindItemUID(uint.Parse(uid));
                        item.skin = skinId;
                        var hend = item.GetHeldEntity();
                        if (hend != null)
                        {
                            hend.skinID = skinId;
                            hend.SendNetworkUpdate();
                        }
                        item.MarkDirty();
                        player.SendNetworkUpdate();
                    }
                    else
                    {
                        DefaultSkins skinData;

                        if (playerData.Prefab is BaseVehicle)
                        {
                            var vehicle = playerData.Prefab as BaseVehicle;
                            var shortname = vehicle.ShortPrefabName;
                            if (!playerData._defaultSkins.TryGetValue(shortname, out skinData)) return;

                            if (skinId == vehicle.skinID || skinId == 0) return;

                            BaseVehicle transport = GameManager.server.CreateEntity($"assets/content/vehicles/snowmobiles/{shortname}.prefab", vehicle.transform.position, vehicle.transform.rotation) as BaseVehicle;
                            transport.health = vehicle.health;
                            transport.skinID = skinId;

                            vehicle.Kill();
                            transport.Spawn();
                            Effect.server.Run("assets/prefabs/deployable/repair bench/effects/skinchange_spraypaint.prefab", transport.transform.localPosition);
                        }
                        else if (playerData.Prefab.OwnerID == player.userID || player.currentTeam != 0 && player.Team.members.Contains(playerData.Prefab.OwnerID))
                        {
                            if (shortnamesEntity.ContainsKey(playerData.Prefab.ShortPrefabName))
                            {
                                if (skinId == playerData.Prefab.skinID || skinId == 0) return;

                                playerData.Prefab.skinID = skinId;
                                playerData.Prefab.SendNetworkUpdate();
                                Effect.server.Run("assets/prefabs/deployable/repair bench/effects/skinchange_spraypaint.prefab", playerData.Prefab.transform.localPosition);
                            }
                        }
                    }
                    break;
                case "":

                    break;
                case "close":
                    if (this.playersData.TryGetValue(player.userID, out playerData))
                        playerData.Prefab = null;
                    CuiHelper.DestroyUi(player, Layer);
                    CuiHelper.DestroyUi(player, Layer + "-Wear");
                    CuiHelper.DestroyUi(player, Layer + "-Main");
                    CuiHelper.DestroyUi(player, Layer + "-Belt");
                    CuiHelper.DestroyUi(player, Layer + "-Izbranoe");
                    CuiHelper.DestroyUi(player, Layer + "-Search");
                    CuiHelper.DestroyUi(player, Layer + "-LableIzb");
                    openSkins.Remove(player.userID);
                    EffectNetwork.Send(Sound1, player.Connection);
                    player.EndLooting();

                    break;
            }
        }
        private void LoadSkinsUI(BasePlayer player, string weapon, int page, uint itemId)
        {
            if (!_loadData.ContainsKey(weapon)) return;
            DataPlayer dataPlayer;

            if (!playersData.TryGetValue(player.userID, out dataPlayer)) return;

            bool havePrefab = dataPlayer.Prefab == null;

            int skinsCount = havePrefab ? 
                cfg.MenuSettings.StandartMenuCellsCount : 
                cfg.MenuSettings.EntityMenuCellsCount;
            int pageXCount = havePrefab ? skinsCount / 3 : 4;
            int pageYCount = havePrefab ? 3 : skinsCount / 4;
            CuiHelper.DestroyUi(player, Layer + "-Search");
            var container = new CuiElementContainer
            {
                {
                    new CuiPanel()
                    {
                        Image =
                        {
                            ImageType = Image.Type.Filled,
                            Png = "assets/standard assets/effects/imageeffects/textures/noise.png",
                            //Sprite = Sharp,
                            Color = HexToRustFormat($"#535151{(havePrefab ? "2C" : "00")}"),//
                            Material = "assets/icons/greyout.mat"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = $"{-400} {-185}",
                            OffsetMax = $"{-400 + 90 * pageXCount} {115}"
                        }
                    },
                    Layer,
                    Layer + "-Search"
                },
                {
                    new CuiPanel()
                    {
                        Image =
                        {
                            ImageType = Image.Type.Filled,
                            Png = "assets/standard assets/effects/imageeffects/textures/noise.png",
                           // Sprite = Sharp,
                            Color = HexToRustFormat("#5351512C"),
                            Material = "assets/icons/greyout.mat"
                        },
                        RectTransform =
                        {
                            AnchorMin = "1 0.5",
                            AnchorMax = $"{1} 0.5",//  + float.Parse($"0.{skinsCount}") / 2
                            OffsetMin = $"{15} 0", // * (skinsCount == 12 ? 8 : 4)  + 90 * pageCount + 20} {-150}
                            OffsetMax = $"{15} 0" //  + 90 * pageCount + 20} {150}
                        }
                    },
                    Layer + "-Search",
                    Layer + "-Page"
                }
            };
            if (0 < _loadData[weapon].Count - skinsCount * page)
            {
                container.Add(new CuiButton()
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = $"{-10} {5}",
                        OffsetMax = $"{10} {143}"
                    },
                    Button =
                    {
                        Command = $"uiskinmenu page {weapon} {page + 1} {itemId}",
                        Color = HexToRustFormat("#5c80ba")
                    },
                    Text =
                    {
                        Text = "»", Align = TextAnchor.MiddleCenter,
                        FontSize = (int) (20 )
                    }
                }, Layer + "-Page");
            }
            else
            {
                container.Add(new CuiButton()
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = $"{-10} {5}",
                        OffsetMax = $"{10} {143}"
                    },
                    Button =
                    {
                        Command = $"",
                        Color = HexToRustFormat("#5c80bac3")
                    },
                    Text =
                    {
                        Text = "»", Align = TextAnchor.MiddleCenter,
                        FontSize = (int) (20),
                        Color = "0.65 0.65 0.65 0.65"
                    }
                }, Layer + "-Page");
            }
            if (page > 1)
            {
                container.Add(new CuiButton()
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = $"{-10} {-137}",
                        OffsetMax = $"{10} {-5}"
                    },
                    Button =
                    {
                        Command = $"uiskinmenu page {weapon} {page - 1} {itemId}",
                        Color = HexToRustFormat("#5c80ba")
                    },
                    Text =
                    {
                        Text = "«", Align = TextAnchor.MiddleCenter, FontSize = (int) (20 )
                    }
                }, Layer + "-Page");
            }
            else
            {
                container.Add(new CuiButton()
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = $"{-10} {-137}",
                        OffsetMax = $"{10} {-5}"
                    },
                    Button =
                    {
                        Command = $"",
                        Color = HexToRustFormat("#5c80bac3")
                    },
                    Text =
                    {
                        Text = "«", Align = TextAnchor.MiddleCenter, FontSize = (int) (20 ),
                        Color = "0.65 0.65 0.65 0.65"
                    }
                }, Layer + "-Page");
            }
            //CuiHelper.AddUi(player, container);
            //return;
            var tSkinsList = _loadData[weapon].ToDictionary(keyValuePair => keyValuePair.Key, keyValuePair => keyValuePair.Value);
            if (cfg.useSkins.Count >= 1)
                foreach (var keyValuePair in cfg.useSkins.Where(keyValuePair =>
                    !permission.UserHasPermission(player.UserIDString, keyValuePair.Key)))
                {
                    keyValuePair.Value.ForEach(p =>
                    {
                        if (tSkinsList.ContainsKey(p))
                        {
                            tSkinsList.Remove(p);
                        }
                    });
                }

            foreach (var skinItem in tSkinsList.
                Where(p => p.Value.IsEnabled).
                Select((i, t) => new { A = i, B = t - (page - 1) * skinsCount }). //
                Skip((page - 1) * skinsCount). //skinsCount
                Take(skinsCount)) //skinsCount
            {
                container.Add(new CuiElement()
                {
                    Parent = Layer + "-Search",
                    Name = Layer + "-Search" + ".Player" + skinItem.B,
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = "0.35 0.35 0.35 0.65"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"0 0.5",
                            AnchorMax = $"0 0.5",
                            OffsetMin = $"{4 + skinItem.B * 90 - Math.Floor((double) skinItem.B / pageXCount) * pageXCount * 90} {60 - Math.Floor((double) skinItem.B / pageXCount) * 98}",
                            OffsetMax = $"{90 + skinItem.B * 90 - Math.Floor((double) skinItem.B / pageXCount) * pageXCount * 90} {143 - Math.Floor((double) skinItem.B / pageXCount) * 98}"
                        }
                    }
                });
                
                container.Add(new CuiElement()
                {
                    Parent = Layer + "-Search" + ".Player" + skinItem.B,
                    Components =
                    {
                        new CuiRawImageComponent()
                        {
                            Png = GetImage("SKULISKINS1"+ weapon +"_" + skinItem.A.Key)
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = $"{-30} {-30}",
                            OffsetMax = $"{30} {30}"
                        }
                    }
                });
                container.Add(new CuiElement()
                {
                    Parent = Layer + "-Search" + ".Player" + skinItem.B,
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Text = skinItem.A.Value.MarketName, Align = TextAnchor.MiddleCenter,
                            Font = Regular,
                            FontSize = 9
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.5 0",
                            AnchorMax = "0.5 0",
                            OffsetMin = $"{-40} {-15}",
                            OffsetMax = $"{40} {0}"
                        }
                    }
                });
                container.Add(new CuiButton()
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = $"{-30} {-30}",
                        OffsetMax = $"{30} {30}"
                    },
                    Button =
                    {
                        Command = $"uiskinmenu setskin {itemId} {skinItem.A.Key}",
                        Color = "0 0 0 0"
                    },
                    Text = { Text = "" }
                }, Layer + "-Search" + ".Player" + skinItem.B);

                if (dataPlayer._izvSkins.ContainsKey(weapon) &&
                    dataPlayer._izvSkins[weapon].ContainsKey(skinItem.A.Key))
                {
                    container.Add(new CuiButton()
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = $"{1} {-18}",
                            OffsetMax = $"{18} {-1}"
                        },
                        Button =
                        {
                            Command = $"uiskinmenu setizbranoe {weapon} {skinItem.A.Key} {page} {itemId}",
                            Color = HexToRustFormat("#dead39"),
                            Sprite = "assets/icons/favourite_servers.png",
                        },
                        Text = { Text = "" }
                    }, Layer + "-Search" + ".Player" + skinItem.B);
                }
                else
                {
                    container.Add(new CuiButton()
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = $"{1} {-18}",
                            OffsetMax = $"{18} {-1}"
                        },
                        Button =
                        {
                            Command = $"uiskinmenu setizbranoe {weapon} {skinItem.A.Key} {page} {itemId}",
                            Color = "1 1 1 1",
                            Sprite = "assets/icons/favourite_servers.png",
                        },
                        Text = { Text = "" }
                    }, Layer + "-Search" + ".Player" + skinItem.B);
                }

                if (permission.UserHasPermission(player.UserIDString, cfg.canusedefault))
                {
                    if (playersData.TryGetValue(player.userID, out dataPlayer) && dataPlayer._defaultSkins.ContainsKey(weapon) && dataPlayer._defaultSkins[weapon].skinId == skinItem.A.Key)
                    {
                        container.Add(new CuiButton()
                        {
                            RectTransform =
                            {
                                AnchorMin = "1 1",
                                AnchorMax = "1 1",
                                OffsetMin = $"{-16} {-16}",
                                OffsetMax = $"{-5} {-5}"
                            },
                            Button =
                            {
                                Command = $"uiskinmenu setdefault {weapon} {skinItem.A.Key} {page} {itemId}",
                                Color = HexToRustFormat("#369f47"),
                                Sprite = "assets/icons/power.png",
                            },
                            Text = { Text = "" }
                        }, Layer + "-Search" + ".Player" + skinItem.B);
                    }
                    else
                    {
                        container.Add(new CuiButton()
                        {
                            RectTransform =
                            {
                                AnchorMin = "1 1",
                                AnchorMax = "1 1",
                                OffsetMin = $"{-16} {-16}",
                                OffsetMax = $"{-5} {-5}"
                            },
                            Button =
                            {
                                Command = $"uiskinmenu setdefault {weapon} {skinItem.A.Key} {page} {itemId}",
                                Color = HexToRustFormat("#fb4f3b"),
                                Sprite = "assets/icons/power.png",
                            },
                            Text = { Text = "" }
                        }, Layer + "-Search" + ".Player" + skinItem.B);
                    }
                }
            }
            CuiHelper.AddUi(player, container);

        }
        #endregion

        #region Hooks

        bool ChangeSkin(BasePlayer player)
        {
            if (player == null || player.IsDead()) return false;
            if (cfg.Market.costChange <= 0) return true;
            switch (cfg.Market.WhoMarket)
            {
                case "Scrap":
                    {
                        var scrap = player.inventory.AllItems().FirstOrDefault(p =>
                            p.info.shortname == "scrap" && p.skin == 0 && p.amount >= cfg.Market.costChange);
                        if (scrap == null)
                        {
                            SendReply(player, lang.GetMessage("NEEDMORESCRAP", this, player.UserIDString));
                            return false;
                        }
                        if (scrap.amount == cfg.Market.costChange)
                        {
                            scrap.RemoveFromContainer();
                            scrap.RemoveFromWorld();
                        }
                        else
                        {
                            scrap.amount = (int)(scrap.amount - cfg.Market.costChange);
                            scrap.MarkDirty();
                        }

                        return true;
                    }
                case "ServerRewards":
                    {
                        var checkPoint = ServerRewards?.Call("CheckPoints", player.userID);
                        if (checkPoint == null)
                        {
                            return false;
                        }

                        if ((int)checkPoint < cfg.Market.costChange)
                        {
                            SendReply(player, lang.GetMessage("NEEDMOREMONEY", this, player.UserIDString));
                            return false;
                        }
                        else
                        {
                            ServerRewards?.Call("TakePoints", player.userID, cfg.Market.costChange);
                        }
                        return true;
                    }
                case "Economics":
                    {
                        double checkPoint = (double)Economics?.Call("Balance", player.userID);
                        if (checkPoint <= 0)
                        {
                            return false;
                        }
                        if (checkPoint < cfg.Market.costChange)
                        {
                            SendReply(player, lang.GetMessage("NEEDMOREMONEY", this, player.UserIDString));
                            return false;
                        }
                        else
                        {
                            Economics?.Call("Withdraw", player.userID, cfg.Market.costChange);
                        }
                        return true;
                    }
                default:
                    return false;
            }
        }
        bool OpenPay(BasePlayer player)
        {
            if (player == null || player.IsDead()) return false;
            if (cfg.Market.costOpen <= 0) return true;
            switch (cfg.Market.WhoMarket)
            {
                case "Scrap":
                    {
                        var scrap = player.inventory.AllItems().FirstOrDefault(p =>
                            p.info.shortname == "scrap" && p.skin == 0 && p.amount >= cfg.Market.costOpen);
                        if (scrap == null)
                        {
                            SendReply(player, lang.GetMessage("NEEDMORESCRAP", this, player.UserIDString));
                            return false;
                        }
                        if (scrap.amount == cfg.Market.costOpen)
                        {
                            scrap.RemoveFromContainer();
                            scrap.RemoveFromWorld();
                        }
                        else
                        {
                            scrap.amount = (int)(scrap.amount - cfg.Market.costOpen);
                            scrap.MarkDirty();
                        }

                        return true;
                    }
                case "ServerRewards":
                    {
                        var checkPoint = ServerRewards?.Call("CheckPoints", player.userID);
                        if (checkPoint == null)
                        {
                            return false;
                        }

                        if ((int)checkPoint < cfg.Market.costOpen)
                        {
                            SendReply(player, lang.GetMessage("NEEDMOREMONEY", this, player.UserIDString));
                            return false;
                        }
                        else
                        {
                            ServerRewards?.Call("TakePoints", player.userID, cfg.Market.costOpen);
                        }
                        return true;
                    }
                case "Economics":
                    {
                        double checkPoint = (double)Economics?.Call("Balance", player.userID);
                        if (checkPoint <= 0)
                        {
                            return false;
                        }
                        if (checkPoint < cfg.Market.costOpen)
                        {
                            SendReply(player, lang.GetMessage("NEEDMOREMONEY", this, player.UserIDString));
                            return false;
                        }
                        else
                        {
                            Economics?.Call("Withdraw", player.userID, cfg.Market.costOpen);
                        }
                        return true;
                    }
                default:
                    return false;
            }
        }
        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (container == null || item == null || !_loadData.ContainsKey(item.info.shortname)) return;
            if (item.skin != 0 && !cfg.SkinAuto) return;
            if (item.skin != 0 && cfg.blackList.Contains(item.skin)) return;
            var player = item.GetOwnerPlayer();
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, cfg.canusedefault)) return;
            if (container != player.inventory.containerMain && container != player.inventory.containerBelt && container != player.inventory.containerWear) return;
            DataPlayer playerData;
            if (!this.playersData.TryGetValue(player.userID, out playerData)) return;
            DefaultSkins skinData;
            if (!playerData._defaultSkins.TryGetValue(item.info.shortname, out skinData)) return;
            item.skin = skinData.skinId;
            var held = item.GetHeldEntity();
            if (held == null) return;
            held.skinID = skinData.skinId;
            held.SendNetworkUpdate();
        }
        private void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            if (task.skinID != 0 && !cfg.SkinAuto) return;
            if (task.skinID != 0 && cfg.blackList.Contains(ulong.Parse(task.skinID.ToString()))) return;
            if (!permission.UserHasPermission(task.owner.UserIDString, cfg.canusedefault)) return;
            DataPlayer playerData;
            if (!this.playersData.TryGetValue(task.owner.userID, out playerData)) return;
            DefaultSkins skinData;
            if (!playerData._defaultSkins.TryGetValue(item.info.shortname, out skinData)) return;
            item.skin = skinData.skinId;
            var held = item.GetHeldEntity();
            if (held == null) return;
            held.skinID = skinData.skinId;
            held.SendNetworkUpdate();
        }
        private object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (openSkins.ContainsKey(player.userID)) return false;
            return null;
        }

        private Dictionary<string, Dictionary<ulong, ItemData.SkinInfo>> _loadData = new Dictionary<string, Dictionary<ulong, ItemData.SkinInfo>>();
        private void LoadDataSkins()
        {
            foreach (var cfgWeaponSkin in Interface.Oxide.DataFileSystem.GetFiles("SkuliSkins/Skins/"))
            {
                var text = cfgWeaponSkin.Remove(0, cfgWeaponSkin.IndexOf("/Skins/") + 7);
                var text2 = text.Remove(text.IndexOf(".json"), text.Length - text.IndexOf(".json"));
                var skins = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, ItemData.SkinInfo>>($"SkuliSkins/Skins/{text2}");
                _loadData.Add(text2, skins);
                foreach (var keyValuePair in skins)
                {
                    //if (ImageLibrary.Call<bool>("HasImage", $"SKULISKINS1{text2}_{keyValuePair.Key}", 0ul)) continue;
                    if (keyValuePair.Value.Url == "" || keyValuePair.Value.Url == null)
                        skinIdsImageSave.Add(keyValuePair.Key, text2);
                    skinIdsRust.Add(keyValuePair.Key, text2);
                }
            }
        }
        void LoadApprovedSkin()
        {
            var i = 0;
            foreach (var approvedSkinInfo in Rust.Workshop.Approved.All)
            {
                if (approvedSkinInfo.Value == null || approvedSkinInfo.Value.Skinnable == null || approvedSkinInfo.Value.Marketable == false) continue;
                var item = approvedSkinInfo.Value.Skinnable.ItemName;
                if (item.Contains("lr300")) item = "rifle.lr300";
                if (_loadData.ContainsKey(item) && _loadData[item].ContainsKey(approvedSkinInfo.Value.WorkshopdId)) continue;
                if (_loadData.ContainsKey(item))
                {
                    _loadData[item].Add(approvedSkinInfo.Value.WorkshopdId, new ItemData.SkinInfo()
                    {
                        IsApproved = true,
                        MarketName = approvedSkinInfo.Value.Name
                    });
                    Interface.Oxide.DataFileSystem.WriteObject($"SkuliSkins/Skins/{item}", _loadData[item]);
                }
                else
                {
                    _loadData.Add(item, new Dictionary<ulong, ItemData.SkinInfo>()
                    {
                        [approvedSkinInfo.Value.WorkshopdId] = new ItemData.SkinInfo()
                        {
                            IsApproved = true,
                            MarketName = approvedSkinInfo.Value.Name
                        }
                    });
                    Interface.Oxide.DataFileSystem.WriteObject($"SkuliSkins/Skins/{item}", _loadData[item]);
                }
                i++;
                if (skinIdsRust.ContainsKey(approvedSkinInfo.Value.WorkshopdId)) continue;
                skinIdsRust.Add(approvedSkinInfo.Value.WorkshopdId, item);
            }
        }


        private void OnServerInitialized()
        {
            UpdateWorkshopShortName();
            permission.RegisterPermission(cfg.canuse, this);
            permission.RegisterPermission(cfg.canusedefault, this);
           // permission.RegisterPermission(cfg.VipPerm, this);
            permission.RegisterPermission("skuliskins.admin", this);
            foreach (var keyValuePair in cfg.useSkins)
            {
                permission.RegisterPermission(keyValuePair.Key, this);
            }

            foreach (var command in cfg.MenuCommandsOpen)
                cmd.AddChatCommand(command, this, StartUI);
            foreach (var command in cfg.EntityMenuCommandsOpen)
                cmd.AddChatCommand(command, this, SkinItemCMD);
            playersData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, DataPlayer>>("SkuliSkins/PlayerData");
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile("SkuliSkins/Skins/rifle.lr300"))
            {
                var newData = new Dictionary<ulong, ItemData.SkinInfo>();
                newData.Add(2540239609, new ItemData.SkinInfo()
                {
                    IsApproved = true,
                    MarketName = "Bombing LR"
                });
                Interface.Oxide.DataFileSystem.WriteObject("SkuliSkins/Skins/rifle.lr300", newData);
            }
            LoadDataSkins();
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(basePlayer);
            }
            if (cfg.IsApproved)
            {
                NextTick(() =>
                {
                    LoadSkinDefines();
                    LoadApprovedSkin();
                });
            }
            timer.Once(0.5f, () =>
            {
                if (skinIdsRust.Count <= 0) return;
                start = Global.Runner.StartCoroutine(LoadSkin());
            });
        }

        void LoadSkinDefines()
        {
            foreach (var itemDefinition in ItemManager.GetItemDefinitions())
            {
                        
                if (itemDefinition == null || itemDefinition.skins2 == null || itemDefinition.skins2.Length == 0) continue;

                foreach (var playerItemDefinition in itemDefinition.skins2)
                {
                    if (playerItemDefinition == null || playerItemDefinition.WorkshopId <= 0 || itemDefinition.shortname == null) continue;
                    if (_loadData.ContainsKey(itemDefinition.shortname) && _loadData[itemDefinition.shortname].ContainsKey(playerItemDefinition.WorkshopId)) continue;
                    if (_loadData.ContainsKey(itemDefinition.shortname))
                    {
                        _loadData[itemDefinition.shortname].Add(playerItemDefinition.WorkshopId,
                            new ItemData.SkinInfo()
                            {
                                IsApproved = true,
                                MarketName = playerItemDefinition.Name,
                                Url = playerItemDefinition.IconUrl
                            });
                        Interface.Oxide.DataFileSystem.WriteObject($"SkuliSkins/Skins/{itemDefinition.shortname}",
                        _loadData[itemDefinition.shortname]);
                    }
                    else
                    {
                        _loadData.Add(itemDefinition.shortname, new Dictionary<ulong, ItemData.SkinInfo>()
                        {
                            [playerItemDefinition.WorkshopId] = new ItemData.SkinInfo()
                            {
                                IsApproved = true,
                                MarketName = playerItemDefinition.Name,
                                Url = playerItemDefinition.IconUrl
                            }
                        });
                        Interface.Oxide.DataFileSystem.WriteObject($"SkuliSkins/Skins/{itemDefinition.shortname}",
                            _loadData[itemDefinition.shortname]);
                    }
                    skinIdsRust.Add(playerItemDefinition.WorkshopId, itemDefinition.shortname);
                }
                var prefab = itemDefinition.GetComponent<ItemModDeployable>()?.entityPrefab?.resourcePath;

                if (string.IsNullOrEmpty(prefab)) continue;

                var shortPrefabName = Core.Utility.GetFileNameWithoutExtension(prefab);
                if (!string.IsNullOrEmpty(shortPrefabName) && !shortnamesEntity.ContainsKey(shortPrefabName))
                    shortnamesEntity.Add(shortPrefabName, itemDefinition.shortname);
            }
        }
        private void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject("SkuliSkins/PlayerData", playersData);
            foreach (var keyValuePair in _loadData)
            {
                Interface.Oxide.DataFileSystem.WriteObject($"SkuliSkins/Skins/{keyValuePair.Key}", keyValuePair.Value);
            }
            if (start != null) Global.Runner.StopCoroutine(start);
            foreach (var basePlayer in BasePlayer.activePlayerList.Where(p => openSkins.ContainsKey(p.userID)))
            {
                basePlayer.EndLooting();
                CuiHelper.DestroyUi(basePlayer, Layer);
                CuiHelper.DestroyUi(basePlayer, Layer + "-Wear");
                CuiHelper.DestroyUi(basePlayer, Layer + "-Main");
                CuiHelper.DestroyUi(basePlayer, Layer + "-Belt");
                CuiHelper.DestroyUi(basePlayer, Layer + "-Izbranoe");
                CuiHelper.DestroyUi(basePlayer, Layer + "-Search");
                CuiHelper.DestroyUi(basePlayer, Layer + "-LableIzb");
            }
        }

        #region ConsoleCommand

        void AddSkins(BasePlayer player, ulong skinId)
        {
            var weapon = String.Empty;
            var name = String.Empty;
            webrequest.Enqueue($"https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/", $"&itemcount=1&publishedfileids[0]={skinId}",
                (code, res) =>
                {
                    PublishedFileQueryResponse query =
                        JsonConvert.DeserializeObject<PublishedFileQueryResponse>(res, errorHandling);
                    if (query != null && query.response != null && query.response.publishedfiledetails.Length > 0)
                    {
                        foreach (var publishedFileQueryDetail in query.response.publishedfiledetails)
                        {
                            foreach (var tag in publishedFileQueryDetail.tags)
                            {
                                string adjTag = tag.tag.ToLower().Replace("skin", "").Replace(" ", "").Replace("-", "").Replace(".item", "");
                                if (_workshopsnames.ContainsKey(adjTag))
                                {
                                    weapon = _workshopsnames[adjTag];
                                    name = publishedFileQueryDetail.title;
                                    if (!_loadData.ContainsKey(weapon))
                                    {
                                        _loadData.Add(weapon, new Dictionary<ulong, ItemData.SkinInfo>()
                                        {
                                            [skinId] = new ItemData.SkinInfo()
                                            {
                                                IsApproved = false,
                                                IsEnabled = true,
                                                MarketName = name
                                            },
                                        });

                                        PrintWarning($"Add skin(SkuliSkins/Skins/{weapon}.json): {skinId}");
                                        if (player != null) PrintToConsole(player, $"[SkuliSkins] Add skin(Open the file and edit:SkuliSkins/Skins/{weapon}.json): {skinId}");
                                        _loadData[weapon][skinId].Url = publishedFileQueryDetail.preview_url;
                                        Interface.Oxide.DataFileSystem.WriteObject($"SkuliSkins/Skins/{weapon}", _loadData[weapon]);
                                    }
                                    else if (!_loadData[weapon].ContainsKey(skinId))
                                    {
                                        _loadData[weapon].Add(skinId, new ItemData.SkinInfo()
                                        {
                                            IsApproved = false,
                                            IsEnabled = true,
                                            MarketName = name
                                        });
                                        PrintWarning($"Add skin(SkuliSkins/Skins/{weapon}.json): {skinId}");
                                        if (player != null)
                                            PrintToConsole(player, $"[SkuliSkins] Add skin(Open the file and edit:SkuliSkins/Skins/{weapon}.json): {skinId}");
                                        _loadData[weapon][skinId].Url = publishedFileQueryDetail.preview_url;
                                        Interface.Oxide.DataFileSystem.WriteObject($"SkuliSkins/Skins/{weapon}", _loadData[weapon]);
                                    }
                                    return;
                                }
                            }
                        }
                    }
                }, this, RequestMethod.POST);
        }
        private JsonSerializerSettings errorHandling = new JsonSerializerSettings { Error = (se, ev) => { ev.ErrorContext.Handled = true; } };

        [ConsoleCommand("skuliskins")]
        private void SkinsConsole(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !permission.UserHasPermission(arg.Player().UserIDString, "skuliskins.admin")) return;
            if (arg.Args == null || arg.Args.Length < 2) return;
            var type = arg.Args[0];
            ulong skinId;
            if (!ulong.TryParse(arg.Args[1], out skinId)) return;
            string weapon;
            switch (type)
            {
                case "add":
                    AddSkins(arg.Player(), skinId);
                    break;
                case "addcollection":
                    webrequest.Enqueue($"https://api.steampowered.com/ISteamRemoteStorage/GetCollectionDetails/v1/", $"&collectioncount=1&publishedfileids[0]={skinId}",
                        (code, res) =>
                        {
                            CollectionQueryResponse collectionQuery = JsonConvert.DeserializeObject<CollectionQueryResponse>(res);
                            foreach (var responseCollectiondetail in collectionQuery.response.collectiondetails)
                            {
                                foreach (var responseCollectiondetailChild in responseCollectiondetail.children)
                                {
                                    rust.RunServerCommand($"skuliskins add {responseCollectiondetailChild.publishedfileid}");
                                }
                            }
                        }, this, RequestMethod.POST);
                    break;
                case "remove":
                    if (arg.Args.Length < 3)
                    {
                        PrintWarning($"skuliskins {type} weapon {skinId}");
                        if (arg.Player() != null) PrintToConsole(arg.Player(), $"skuliskins {type} weapon {skinId} NAMESKIN");
                        return;
                    }
                    weapon = arg.Args[2];
                    if (_loadData.ContainsKey(weapon) && _loadData[weapon].ContainsKey(skinId))
                    {
                        _loadData[weapon].Remove(skinId);
                        PrintWarning($"Remove skin: {weapon}_{skinId}");
                        if (arg.Player() != null)
                            PrintToConsole(arg.Player(), $"[SkuliSkins] Remove skin: {weapon}_{skinId}");
                    }
                    break;
            }
        }

        #endregion

        private void SkinItemCMD(BasePlayer player, string command, string[] args)
        {
            //if (!permission.UserHasPermission(player.UserIDString, cfg.VipPerm)) return;
            if (!cfg.Market.IsEnabled)
            {
                if (!permission.UserHasPermission(player.UserIDString, cfg.canuse)) // cfg.canuse
                {
                    SendReply(player, lang.GetMessage("NOPERMUSE", this, player.UserIDString));
                    return;
                }
            }
            else
            {
                if (!permission.UserHasPermission(player.UserIDString, cfg.canuse))
                {
                    if (!OpenPay(player)) return;
                }
            }
            DataPlayer playerData;
            if (!this.playersData.TryGetValue(player.userID, out playerData)) return;
            RaycastHit rhit;

            if (!Physics.Raycast(player.eyes.HeadRay(), out rhit, 3f, LayerMask.GetMask("Deployed", "Construction", "Prevent Building"))) return;
            var entity = rhit.GetEntity();

            if (entity == null) return;

            string shortName = "";

            if (entity is BaseVehicle)
            {
                var vehicle = entity as BaseVehicle;
                shortName = vehicle.ShortPrefabName;
            }
            else if (entity.OwnerID == player.userID || player.currentTeam != 0 && player.Team.members.Contains(entity.OwnerID))
            {
                if (shortnamesEntity.ContainsKey(entity.ShortPrefabName))
                    shortName = shortnamesEntity[entity.ShortPrefabName];
            }
            else
            {
                return;
            }
            if (openSkins.ContainsKey(player.userID)) return;
            if (!_workshopsnames.ContainsValue(shortName)) return;
            ItemContainer container = new ItemContainer();
            openSkins.Add(player.userID, container);

            playerData.Prefab = entity;
            StartInterface(player);
            player.SendConsoleCommand($"uiskinmenu", new object[] { "weaponSelect", shortName, 00000000000.ToString() });
        }
        private void StartUI(BasePlayer player, string command, string[] args)
        {
            if (!cfg.Market.IsEnabled)
            {
                if (!permission.UserHasPermission(player.UserIDString, cfg.canuse))
                {
                    SendReply(player, lang.GetMessage("NOPERMUSE", this, player.UserIDString));
                    return;
                }
            }
            else
            {
                if (!permission.UserHasPermission(player.UserIDString, cfg.canuse))
                {
                    if (!OpenPay(player)) return;
                }
            }
            CuiHelper.DestroyUi(player, Layer);
            player.EndLooting();
            if (openSkins.ContainsKey(player.userID)) return;
            timer.Once(0.5f, () => { StartLoot(player); });
        }
        private object CanLootPlayer(BasePlayer looted, BasePlayer looter)
        {
            if (looter == null) return null;
            if (openSkins.ContainsKey(looter.userID))
            {
                return true;
            }
            return null;
        }
        private void StartLoot(BasePlayer player)
        {
            if (openSkins.ContainsKey(player.userID)) return;
            ItemContainer container = new ItemContainer();
            container.entityOwner = player;
            container.isServer = true;
            container.allowedContents = ItemContainer.ContentsType.Generic;
            container.GiveUID();
            openSkins.Add(player.userID, container);
            container.capacity = 12;
            container.playerOwner = player;
            PlayerLootContainer(player, container);
            StartInterface(player);
        }
        private void StartInterface(BasePlayer player)
        {
            DataPlayer dataPlayer;
            if (!playersData.TryGetValue(player.userID, out dataPlayer)) return;
            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.DestroyUi(player, Layer + "-Wear");
            CuiHelper.DestroyUi(player, Layer + "-Main");
            CuiHelper.DestroyUi(player, Layer + "-Belt");
            CuiHelper.DestroyUi(player, Layer + "-Izbranoe");
            CuiHelper.DestroyUi(player, Layer + "-Search");
            CuiHelper.DestroyUi(player, Layer + "-LableIzb");
            var cont = new CuiElementContainer();
            CuiPanel Fon = new CuiPanel()
            {
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = $"{-225} {-325}", OffsetMax = $"{615} {-1}" },
                CursorEnabled = true,
                Image = { Color = "0 0 0 0" }
            };
            cont.Add(Fon, Overlay, Layer);
            cont.Add(new CuiPanel()
            {
                Image =
                {
                    Color = HexToRustFormat(cfg.MenuSettings.MenuColor),
                   // Material = Sharp,
                    ImageType = Image.Type.Tiled
                },
                RectTransform =
                {
                    AnchorMin = "0 1",
                    AnchorMax = "0 1",
                    OffsetMin = $"{7} {-47}",
                    OffsetMax = $"{427} {5}"
                }
            }, Layer, Layer + "Lable");
            cont.Add(new CuiElement()
            {
                Parent = Layer + "Lable",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text =  lang.GetMessage("LABLEMENUTEXT", this, player.UserIDString),
                        Align = TextAnchor.MiddleCenter,
                        FontSize = (int) (24 ),
                        Color = HexToRustFormat(cfg.MenuSettings.TextColor)
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 0.5",
                        AnchorMax = "0 0.5",
                        OffsetMin = $"{4} {-30}",
                        OffsetMax = $"{427} {25}"
                    }
                }
            });
            cont.Add(new CuiElement()
            {
                Parent = Layer,
                Name = Layer + "-Search",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = lang.GetMessage("INFOCLICKSKIN", this, player.UserIDString),
                        Color = "1 1 1 1",
                        FontSize = (int) (12 ),
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = $"{-400} {-185}",
                        OffsetMax = $"{-35} {115}"
                    }
                }
            });
            CuiButton destroyUi = new CuiButton()
            {
                RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = $"{5} {-15}", OffsetMax = $"{35} {15}" },
                Button = { Color = "0.75 0.75 0.75 0.65", Command = "uiskinmenu close", Sprite = "assets/icons/close.png" },
                Text = { Text = "", Align = TextAnchor.MiddleCenter }
            };
            cont.Add(destroyUi, Layer + "Lable");
            cont.Add(new CuiPanel()
            {
                Image =
                {
                    Color = HexToRustFormat(cfg.MenuSettings.MenuColor),
                   //Material = Sharp,
                    ImageType = Image.Type.Tiled
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0",
                    AnchorMax = "0.5 0",
                    OffsetMin = $"{190} {235}",
                    OffsetMax = $"{575} {290}"
                },

            }, Overlay, Layer + "-LableIzb");
            cont.Add(new CuiElement()
            {
                Parent = Layer + "-LableIzb",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = lang.GetMessage("FAVSKINSLABLE", this, player.UserIDString),
                        Align = TextAnchor.MiddleCenter,
                        FontSize = (int) (24 ),
                        Color = HexToRustFormat(cfg.MenuSettings.TextColor)
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 0.5",
                        AnchorMax = "0 0.5",
                        OffsetMin = $"{20} {-25}",
                        OffsetMax = $"{350} {25}"
                    }
                }
            });

            cont.Add(new CuiPanel()
            {
                Image =
                {
                    Color = "0 0 0 0"
                },
                RectTransform =
                {
                    AnchorMin = "0.5 0",
                    AnchorMax = "0.5 0",
                    OffsetMin = $"{201} {111}",
                    OffsetMax = $"{571} {231}"
                }
            }, Overlay, Layer + "-Izbranoe");
            CuiHelper.AddUi(player, cont);
            LoadWearUI(player);
            LoadMainUI(player);
            LoadBeltUI(player);
        }
        private Dictionary<ulong, ItemContainer> openSkins = new Dictionary<ulong, ItemContainer>();
        private static void PlayerLootContainer(BasePlayer player, ItemContainer container)
        {
            player.inventory.loot.Clear();
            player.inventory.loot.PositionChecks = false;
            player.inventory.loot.entitySource = container.entityOwner ?? player;
            player.inventory.loot.itemSource = null;
            player.inventory.loot.MarkDirty();
            player.inventory.loot.AddContainer(container);
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", "generic_resizable");
        }
        private void OnPlayerLootEnd(PlayerLoot inventory)
        {
            if (inventory == null) return;
            var player = inventory.GetComponent<BasePlayer>();
            if (player == null || !openSkins.ContainsKey(player.userID)) return;
            var cont = openSkins[player.userID];
            var itemc = inventory.containers.Find(p => p.uid == cont.uid);
            if (itemc == null) return;
            itemc.Clear();
            openSkins.Remove(player.userID);
            itemc.Kill();
            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.DestroyUi(player, Layer + "-Izbranoe");
            CuiHelper.DestroyUi(player, Layer + "-LableIzb");
            CuiHelper.DestroyUi(player, Layer + "-Wear");
            CuiHelper.DestroyUi(player, Layer + "-Main");
            CuiHelper.DestroyUi(player, Layer + "-Belt");

        }

        void OnPlayerConnected(BasePlayer player)
        {
            DataPlayer playerDatas;
            if (!playersData.TryGetValue(player.userID, out playerDatas)) playersData.Add(player.userID, new DataPlayer());
        }
        #endregion

        #region LoadSkins
        private IEnumerator LoadSkin()
        {
            Puts($"Loads skins: " + skinIdsRust.Count);
            foreach (var keyValuePair in skinIdsImageSave)
            {
                webrequest.Enqueue($"https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/", $"&itemcount=1&publishedfileids[0]={keyValuePair.Key}",
                (code, res) =>
                {
                    PublishedFileQueryResponse query =
                        JsonConvert.DeserializeObject<PublishedFileQueryResponse>(res, errorHandling);

                    if (query != null && query.response != null && query.response.publishedfiledetails.Length > 0)
                    {
                        if (_loadData.ContainsKey(keyValuePair.Value))
                        {
                            _loadData[keyValuePair.Value][keyValuePair.Key].Url = query.response.publishedfiledetails.First().preview_url;
                        }
                    }
                }, this, RequestMethod.POST);
                if (string.IsNullOrEmpty(_loadData[keyValuePair.Value][keyValuePair.Key].Url))
                    continue;

                yield return CoroutineEx.waitForSeconds(WebRequests.Timeout);
            }
            int index = 0;
            foreach (var skindID in skinIdsRust)
            {
                index++;
                if (ImageLibrary.Call<bool>("HasImage", $"SKULISKINS1{skindID.Value}_{skindID.Key}")) continue;
                if (index % 50 == 0)
                {
                    PrintWarning($"Processing approved skins: {(float)index / (float)skinIdsRust.Count * 100}/100%");
                }
                //AddImage(, $"SKULISKINS1{skindID.Value}_{skindID.Key}");

                UnityWebRequest www = UnityWebRequestTexture.GetTexture(_loadData[skindID.Value][skindID.Key].Url);
                yield return www.SendWebRequest();

                if (www.isNetworkError || www.isHttpError)
                {
                    PrintWarning(string.Format("Image download error! Error: {0}, Image name: {1}", www.error, $"SKULISKINS1{skindID.Value}_{skindID.Key}"));
                    www.Dispose();
                    continue;
                }

                Texture2D texture = DownloadHandlerTexture.GetContent(www);
                if (texture != null)
                {
                    System.Drawing.Image x = (System.Drawing.Bitmap)((new System.Drawing.ImageConverter()).ConvertFrom(texture.EncodeToPNG()));
                    AddImage($"SKULISKINS1{skindID.Value}_{skindID.Key}", ResizeImg(x, 130, 130));
                    UnityEngine.Object.DestroyImmediate(texture);
                }
                www.Dispose();
                yield return CoroutineEx.waitForSeconds(0.001f);
            }
            Puts($"Skins loaded");
        }

        public byte[] ResizeImg(System.Drawing.Image b, int nWidth, int nHeight)
        {
            //Image
            System.Drawing.Image result = new System.Drawing.Bitmap(nWidth, nHeight);
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage((System.Drawing.Image)result))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(b, 0, 0, nWidth, nHeight);
                g.Dispose();
            }
            return ConverterDemo(result);
        }
        public static byte[] ConverterDemo(System.Drawing.Image x)
        {
            System.Drawing.ImageConverter _imageConverter = new System.Drawing.ImageConverter();
            byte[] xByte = (byte[])_imageConverter.ConvertTo(x, typeof(byte[]));
            return xByte;
        }
        Texture2D Resize(Texture2D texture2D, int targetX, int targetY)
        {
            RenderTexture rt = new RenderTexture(targetX, targetY, 24);
            RenderTexture.active = rt;
            Graphics.Blit(texture2D, rt);
            Texture2D result = new Texture2D(targetX, targetY);
            result.ReadPixels(new Rect(0, 0, targetX, targetY), 0, 0);
            result.Apply();
            RenderTexture.active = null;
            return result;
        }

        private Coroutine start;
        Dictionary<ulong, string> skinIdsRust = new Dictionary<ulong, string>();
        Dictionary<ulong, string> skinIdsImageSave = new Dictionary<ulong, string>();
        private readonly Dictionary<string, string> shortnamesEntity = new Dictionary<string, string>();
        #endregion

        #region lang
        protected override void LoadDefaultMessages()
        {
            var ru = new Dictionary<string, string>();
            foreach (var rus in new Dictionary<string, string>()
            {
                ["NOPERMUSE"] = "У вас нет прав на использование команды!",
                ["NEEDMORESCRAP"] = "Не достаточно скрапа в инвентаре!",
                ["NEEDMOREMONEY"] = "Не достаточно средств на счету.",
                ["LABLEMENUTEXT"] = "МЕНЮ СКИНОВ",
                ["INFOCLICKSKIN"] = "Нажмите на предмет,\nна который нужно установить скин",
                ["FAVSKINSLABLE"] = "ИЗБРАННЫЕ СКИНЫ",
                ["ADAPTINTERFACELABLE"] = "АДАПТИРОВАТЬ\n<size=10>РАЗМЕР ИНТЕРФЕЙСА СКИНОВ</size>",
            }) ru.Add(rus.Key, rus.Value);
            var en = new Dictionary<string, string>();
            foreach (var ens in new Dictionary<string, string>()
            {
                ["NOPERMUSE"] = "You don't have the rights to use the command!",
                ["NEEDMORESCRAP"] = "Not enough scrap in the inventory!",
                ["NEEDMOREMONEY"] = "Insufficient funds in the account.",
                ["LABLEMENUTEXT"] = "Menu Skin by Kh",
                ["INFOCLICKSKIN"] = "Haz clic en el item en el que quieras instalar el skin",
                ["FAVSKINSLABLE"] = "SKINS FAVORITAS BY KH",
                ["ADAPTINTERFACELABLE"] = "ADAPT\n<size=10>EL TAMAÑO DE LA INTERFAZ DE LAS skins</size>",
            }) en.Add(ens.Key, ens.Value);
            lang.RegisterMessages(ru, this, "ru");
            lang.RegisterMessages(en, this, "en");
        }
        #endregion

        #region Help

        [PluginReference] private Plugin ImageLibrary, ServerRewards, Economics;

        public string GetImage(string shortname, ulong skin = 0) =>
            (string)ImageLibrary.Call("GetImage", shortname, skin);

        public bool AddImage(string shortname, byte[] bytes, ulong skin = 0) =>
            (bool)ImageLibrary.Call("AddImageData", shortname, bytes, skin);
        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFFFF";
            var str = hex.Trim('#');
            if (str.Length == 6) str += "FF";
            if (str.Length != 8)
            {
                throw new Exception(hex);
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);
            Color color = new Color32(r, g, b, a);
            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }

        #endregion

        #region JSON Response Classes
        public class PublishedFileQueryResponse
        {
            public FileResponse response { get; set; }
        }

        public class FileResponse
        {
            public int result { get; set; }
            public int resultcount { get; set; }
            public PublishedFileQueryDetail[] publishedfiledetails { get; set; }
        }

        public class PublishedFileQueryDetail
        {
            public string publishedfileid { get; set; }
            public int result { get; set; }
            public string creator { get; set; }
            public int creator_app_id { get; set; }
            public int consumer_app_id { get; set; }
            public string filename { get; set; }
            public int file_size { get; set; }
            public string preview_url { get; set; }
            public string hcontent_preview { get; set; }
            public string title { get; set; }
            public string description { get; set; }
            public int time_created { get; set; }
            public int time_updated { get; set; }
            public int visibility { get; set; }
            public int banned { get; set; }
            public string ban_reason { get; set; }
            public int subscriptions { get; set; }
            public int favorited { get; set; }
            public int lifetime_subscriptions { get; set; }
            public int lifetime_favorited { get; set; }
            public int views { get; set; }
            public Tag[] tags { get; set; }

            public class Tag
            {
                public string tag { get; set; }
            }
        }
        public class CollectionQueryResponse
        {
            public CollectionResponse response { get; set; }
        }

        public class CollectionResponse
        {
            public int result { get; set; }
            public int resultcount { get; set; }
            public CollectionDetails[] collectiondetails { get; set; }
        }

        public class CollectionDetails
        {
            public string publishedfileid { get; set; }
            public int result { get; set; }
            public CollectionChild[] children { get; set; }
        }

        public class CollectionChild
        {
            public string publishedfileid { get; set; }
            public int sortorder { get; set; }
            public int filetype { get; set; }
        }

        #endregion

        #region WokshopNames
        private Dictionary<string, string> _workshopsnames = new Dictionary<string, string>()
        {
            {"ak47", "rifle.ak" },
            {"lr300", "rifle.lr300" },
            {"lr300.item", "rifle.lr300" },
            {"m39", "rifle.m39" },
            {"l96", "rifle.l96" },
            {"longtshirt", "tshirt.long" },
            {"cap", "hat.cap" },
            {"beenie", "hat.beenie" },
            {"boonie", "hat.boonie" },
            {"balaclava", "mask.balaclava" },
            {"pipeshotgun", "shotgun.waterpipe" },
            {"woodstorage", "box.wooden" },
            {"bearrug", "rug.bear" },
            {"boltrifle", "rifle.bolt" },
            {"bandana", "mask.bandana" },
            {"hideshirt", "attire.hide.vest" },
            {"snowjacket", "jacket.snow" },
            {"buckethat", "bucket.helmet" },
            {"semiautopistol", "pistol.semiauto" },
            {"roadsignvest", "roadsign.jacket" },
            {"roadsignpants", "roadsign.kilt" },
            {"burlappants", "burlap.trousers" },
            {"collaredshirt", "shirt.collared" },
            {"mp5", "smg.mp5" },
            {"sword", "salvaged.sword" },
            {"workboots", "shoes.boots" },
            {"vagabondjacket", "jacket" },
            {"hideshoes", "attire.hide.boots" },
            {"deerskullmask", "deer.skull.mask" },
            {"minerhat", "hat.miner" },
            {"burlapgloves", "burlap.gloves" },
            {"burlap.gloves", "burlap.gloves"},
            {"leather.gloves", "burlap.gloves"},
            {"python", "pistol.python" },
            {"woodendoubledoor", "door.double.hinged.wood" }
        };
        private void UpdateWorkshopShortName()
        {
            foreach (var itemDefinition in ItemManager.itemList)
            {
                if (itemDefinition.shortname == "ammo.snowballgun") continue;
                var name = itemDefinition.displayName.english.ToLower().Replace("skin", "").Replace(" ", "").Replace("-", "");
                if (!_workshopsnames.ContainsKey(name))
                    _workshopsnames.Add(name, itemDefinition.shortname);
                if (!_workshopsnames.ContainsKey(itemDefinition.shortname))
                    _workshopsnames.Add(itemDefinition.shortname, itemDefinition.shortname);
                if (!_workshopsnames.ContainsKey(itemDefinition.shortname.Replace(".", "")))
                    _workshopsnames.Add(itemDefinition.shortname.Replace(".", ""), itemDefinition.shortname);
            }
        }
        #endregion
    }
}