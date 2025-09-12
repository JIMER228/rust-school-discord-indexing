using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Color = UnityEngine.Color;

namespace Oxide.Plugins
{
    [Info("WipeBlock", "Hougan / rostov114 / Xaiver", "2.0.12")]
    [Description("Блокировка предметов для вашего сервера!")]
    public class WipeBlock : RustPlugin
    {
        #region Classes
        private class Configuration
        {
            public class Interface
            {
                [JsonProperty("Сдвиг панели по вертикале (если некорректно отображается при текущих настройках)")]
                public int Margin = 0;
                [JsonProperty("Текст на первой строке")]
                public string FirstString = "БЛОКИРОВКА ПРЕДМЕТОВ";
                [JsonProperty("Текст на второй строке")]
                public string SecondString = "НАЖМИТЕ ЧТОБЫ УЗНАТЬ БОЛЬШЕ";
                [JsonProperty("Название сервера")]
                public string ServerName = "%CONFIG%";
            }

            public class Block 
            {
                [JsonProperty("Сдвиг блокировки в секундах ('920' - на 920 секунд вперёд, '-920' на 920 секунд назад)")]
                public int TimeMove = 0;
                [JsonProperty("Блокировка предметов")]
                public Dictionary<string, int> items;
                [JsonProperty("Названия категорий в интерфейсе")]
                public Dictionary<string, string> CategoriesName;
            }
            
            [JsonProperty("Настройки интерфейса плагина")]
            public Interface SInterface;

            [JsonProperty("Настройки текущей блокировки")]
            public Block SBlock;

            [JsonProperty("Включить поддержку экспериментальных функций")]
            public bool Experimental = true;

            public static Configuration GetDefaultConfiguration()
            {
                var newConfiguration = new Configuration();
                newConfiguration.SInterface = new Interface();
                newConfiguration.SBlock = new Block();
                newConfiguration.SBlock.CategoriesName = new Dictionary<string, string>
                {
                    ["Weapon"] = "ОРУЖИЯ",
                    ["Ammunition"] = "БОЕПРИПАСОВ",
                    ["Medical"] = "МЕДИЦИНЫ",
                    ["Food"] = "ЕДЫ",
                    ["Traps"] = "ЛОВУШЕК",
                    ["Tool"] = "ИНСТРУМЕНТОВ",
                    ["Construction"] = "КОНСТРУКЦИЙ",
                    ["Resources"] = "РЕСУРСОВ",
                    ["Items"] = "ПРЕДМЕТОВ",
                    ["Component"] = "КОМПОНЕНТОВ",
                    ["Misc"] = "ПРОЧЕГО",
                    ["Attire"] = "ОДЕЖДЫ"
                };
                newConfiguration.SBlock.items = new Dictionary<string, int>
                {
                    {"pistol.revolver", 3600},
                    {"pistol.python", 21600},
                    {"shotgun.pump", 28800},
                    {"smg.mp5", 36000},
                    {"pistol.m92", 36000},
                    {"rifle.m39", 86400},
                    {"lmg.m249", 93600},
                    {"rifle.lr300", 86400},
                    {"rifle.l96", 86400},
                    {"pistol.semiauto", 86400},
                    {"rifle.semiauto", 36000},
                    {"shotgun.spas12", 36000},
                    {"smg.thompson", 36000},
                    {"rifle.ak", 86400},
                    {"rifle.bolt", 86400},
                    {"smg.2", 36000},
                    {"shotgun.double", 7200},

                    {"coffeecan.helmet", 21600},
                    {"heavy.plate.helmet", 36000},
                    {"heavy.plate.jacket", 36000},
                    {"heavy.plate.pants", 36000},
                    {"metal.plate.torso", 36000},
                    {"metal.facemask", 36000},
                    {"roadsign.kilt", 21600},
                    {"roadsign.jacket", 21600},

                    {"grenade.beancan", 93600},
                    {"grenade.f1", 93600},
                    {"flamethrower", 21600},
                    {"rocket.launcher", 100800},
                    {"multiplegrenadelauncher", 86400},
                    {"explosive.satchel", 93600},
                    {"explosive.timed", 100800},
                    {"surveycharge", 93600},
                    {"ammo.grenadelauncher.he", 93600},
                    {"ammo.rifle.explosive", 93600},
                    {"ammo.rocket.basic", 93600},
                    {"ammo.rocket.fire", 93600},
                    {"ammo.rocket.hv", 93600},
                };
                
                return newConfiguration;
            }
        }

        #endregion
        
        #region Variables

        [PluginReference] 
        private Plugin ImageLibrary, Duels, Battles;
        private Configuration settings = null;

        private List<string> Gradients = new List<string> { "518eef","5CAD4F","5DAC4E","5EAB4E","5FAA4E","60A94E","61A84E","62A74E","63A64E","64A54E","65A44E","66A34E","67A24E","68A14E","69A04E","6A9F4E","6B9E4E","6C9D4E","6D9C4E","6E9B4E","6F9A4E","71994E","72984E","73974E","74964E","75954E","76944D","77934D","78924D","79914D","7A904D","7B8F4D","7C8E4D","7D8D4D","7E8C4D","7F8B4D","808A4D","81894D","82884D","83874D","84864D","86854D","87844D","88834D","89824D","8A814D","8B804D","8C7F4D","8D7E4D","8E7D4D","8F7C4D","907B4C","917A4C","92794C","93784C","94774C","95764C","96754C","97744C","98734C","99724C","9B714C","9C704C","9D6F4C","9E6E4C","9F6D4C","A06C4C","A16B4C","A26A4C","A3694C","A4684C","A5674C","A6664C","A7654C","A8644C","A9634C","AA624B","AB614B","AC604B","AD5F4B","AE5E4B","B05D4B","B15C4B","B25B4B","B35A4B","B4594B","B5584B","B6574B","B7564B","B8554B","B9544B","BA534B","BB524B","BC514B","BD504B","BE4F4B","BF4E4B","C04D4B","C14C4B","C24B4B","C44B4B" };
        
        private string Layer = "UI_920InstanceBlock";
        private string LayerBlock = "UI_920Block";
        private string LayerInfoBlock = "UI_920InfoBlock"; 

        private string IgnorePermission = "wipeblock.ignore";

        private Dictionary<ulong, int> UITimer = new Dictionary<ulong, int>();

        private Coroutine UpdateAction;
        #endregion

        #region Initialization
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                settings = Config.ReadObject<Configuration>();
                if (settings?.SBlock == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Ошибка чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => settings = Configuration.GetDefaultConfiguration();
        protected override void SaveConfig() => Config.WriteObject(settings);

        private void OnServerInitialized()
        {
            foreach (var item in settings.SBlock.items)
            {
                if (!ImageLibrary.Call<bool>("HasImage", item.Key))
                {
                    ImageLibrary.Call("AddImage", ImageLibrary.Call<string>("GetImageURL", item.Key), item.Key);
                }
            }
            permission.RegisterPermission(IgnorePermission, this);
            
            
            if (ImageLibrary == null || !ImageLibrary.IsLoaded)
            {
                PrintError("IMAGE LIBRARY IS NOT INSTALLED!");
            }
            else
            {
                var imagesList = new Dictionary<string, string>();
                
                
                if (!imagesList.ContainsKey("https://i.imgur.com/qwPt5ie.png")) // main
                    imagesList.Add("https://i.imgur.com/qwPt5ie.png", "https://i.imgur.com/qwPt5ie.png");
                


                ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
            }
        }

        private void Unload()
        {
            if (UpdateAction != null)
                ServerMgr.Instance.StopCoroutine(UpdateAction);

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                player.SetFlag(BaseEntity.Flags.Reserved3, false);

                CuiHelper.DestroyUi(player, Layer);
                CuiHelper.DestroyUi(player, LayerBlock);
                CuiHelper.DestroyUi(player, LayerInfoBlock);
            }
        }
        #endregion

        #region Hooks
        private object CanWearItem(PlayerInventory inventory, Item item)
        {
            var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
            var isBlocked = IsBlocked(item.info) > 0 ? false : (bool?) null;
            
            if (isBlocked == false)
            {
                if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                    return null;

                if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                    return null;

                DrawInstanceBlock(player, item);
            }

            return isBlocked;
        }

        private object CanEquipItem(PlayerInventory inventory, Item item)
        {
            var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
            if (player == null)
                return null;
            
            var isBlocked = IsBlocked(item.info) > 0 ? false : (bool?) null;
            if (isBlocked == false)
            {
                if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                    return null;

                if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                    return null;

                DrawInstanceBlock(player, item);
            }

            return isBlocked;
        }

        private object OnReloadWeapon(BasePlayer player, BaseProjectile projectile)
        {
            if (player is NPCPlayer)
                return null;

            if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                return null;
            
            if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
                return null;

            var isBlocked = IsBlocked(projectile.primaryMagazine.ammoType) > 0 ? false : (bool?) null;
            if (isBlocked == false && !playerOnDuel(player))
            {
                List<Item> list = player.inventory.FindItemIDs(projectile.primaryMagazine.ammoType.itemid).ToList<Item>();
                if (list.Count == 0)
                {
                    List<Item> list2 = new List<Item>();
                    player.inventory.FindAmmo(list2, projectile.primaryMagazine.definition.ammoTypes);
                    if (list2.Count > 0)
                    {
                        isBlocked = IsBlocked(list2[0].info) > 0 ? false : (bool?) null;
                    }
                }

                if (isBlocked == false)
                {
                    SendReply(player, $"Вы <color=#81B67A>не можете</color> использовать этот тип боеприпасов!");
                }

                return isBlocked;
            }

            return null;
        }
        
        private object OnReloadMagazine(BasePlayer player, BaseProjectile projectile)
        {
            if (player is NPCPlayer)
                return null;

            if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                return null;

            NextTick(() =>
            {
                var isBlocked = IsBlocked(projectile.primaryMagazine.ammoType) > 0 ? false : (bool?) null;
                if (isBlocked == false)
                {
                    player.GiveItem(ItemManager.CreateByItemID(projectile.primaryMagazine.ammoType.itemid, projectile.primaryMagazine.contents, 0UL), BaseEntity.GiveItemReason.Generic);
                    projectile.primaryMagazine.contents = 0;
                    projectile.GetItem().LoseCondition(projectile.GetItem().maxCondition);
                    projectile.SendNetworkUpdate();
                    player.SendNetworkUpdate();

                    PrintError($"[{DateTime.Now.ToShortTimeString()}] {player} пытался взломать систему блокировки!");
                    SendReply(player, $"<color=#81B67A>Хорошая</color> попытка, правда ваше оружие теперь сломано!");
                }
            });

            return null;
        }

        private void OnPlayerConnected(BasePlayer player, bool first = true)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player, first));
                return;
            }

            if (settings.Experimental)
            {
                if (first)
                {
                    foreach (var item in settings.SBlock.items)
                        SendFilePng(player, item.Key);
                }
            }
            
        }

        private object CanMoveItem(Item item, PlayerInventory inventory, uint targetContainer)
        {
            if (inventory == null || item == null)
                return null;

            BasePlayer player = inventory.GetComponent<BasePlayer>();
            if (player == null)
                return null;

            if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                return null;

            ItemContainer container = inventory.FindContainer(targetContainer);
            if (container == null || container.entityOwner == null)
                return null;

            if (container.entityOwner is AutoTurret)
            {
                var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?) null;
                if (isBlocked == false)
                {
                    DrawInstanceBlock(player, item);
                    return true;
                }
            }

            return null;
        }

        private object CanAcceptItem(ItemContainer container, Item item)
        {
            if (container == null || item == null || container.entityOwner == null)
                return null;

            if (container.entityOwner is AutoTurret)
            {
                BasePlayer player = item.GetOwnerPlayer();
                if (player == null)
                    return null;

                if (permission.UserHasPermission(player.UserIDString, IgnorePermission))
                    return null;

                var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?) null;
                if (isBlocked == false)
                {
                    DrawInstanceBlock(player, item);
                    return ItemContainer.CanAcceptResult.CannotAcceptRightNow;
                }
            }

            return null;
        }
        #endregion

        #region GUI

        [ConsoleCommand("blockmove")]
        private void cmdConsoleMoveblock(ConsoleSystem.Arg args)
        {
            if (args.Player() != null)
                return;

            if (!args.HasArgs(1))
            {
                PrintWarning($"Введите количество секунд для перемещения!");
                return;
            }

            int newTime;
            if (!int.TryParse(args.Args[0], out newTime))
            {
                PrintWarning("Вы ввели не число!");
                return;
            }

            settings.SBlock.TimeMove += newTime;
            SaveConfig();
            PrintWarning("Время блокировки успешно изменено!");
        }
        
        public Dictionary<ulong, double> DataKD = new Dictionary<ulong, double>();


        [ConsoleCommand("open.blocktab")]
        void OpenWipeBlock(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            CuiHelper.DestroyUi(player, "StaticMenu");
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiElement 
            {
                Parent = "UI_MenuLayer",
                Name = "StaticMenu",
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0 0"},
                    new CuiRectTransformComponent {AnchorMin = "0.125 0.08333331", AnchorMax = "0.8755208 0.8324074"}
                }
            });
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0.8850433", AnchorMax = "1 0.8862795", OffsetMax = "0 0" },
                Button = { Color = HexToCuiColor("#FBFBFF", 20)},
                Text = { Text = "",Font = "robotocondensed-regular.ttf", FontSize = 23, Color = HexToCuiColor("#BFBFBF"), Align = TextAnchor.MiddleCenter }
            }, "StaticMenu");

            CuiHelper.AddUi(player, container);
            OpenMenuInfo(player, "");
        }

        void OpenMenuInfo(BasePlayer player, string button)
        {
            List<string> BlockedFill = new List<string>();
            ListBlocked(BlockedFill);
            CuiElementContainer container = new CuiElementContainer();

            for (int i = 0; i < BlockedFill.Count; i++)
            {
                CuiHelper.DestroyUi(player, "StaticMenu" + $".{i}.ListButton");
            }

            foreach (var check in BlockedFill.Select((i, t) => new { A = i, B = t }))
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin =
                            $"{0.04510755 + check.B * 0.0700 - Math.Floor((double)check.B / 9) * 9 * 0.0700} {0.8850433 - Math.Floor((double)check.B / 9) * 0.305}",
                        AnchorMax =
                            $"{0.1235253 + check.B * 0.0700 - Math.Floor((double)check.B / 9) * 9 * 0.0700} {0.998764 - Math.Floor((double)check.B / 9) * 0.305}",
                        OffsetMax = "0 0"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = button == check.A ? "" : $"block.tab select {check.A}"
                    },
                    Text =
                    {
                        Color = button == check.A ? HexToCuiColor("#FBFBFF") : HexToCuiColor($"#FBFBFF", 50),
                        Text = check.A, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf",
                        FontSize = 12
                    }
                }, "StaticMenu", "StaticMenu" + $".{check.B}.ListButton");

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 0.01086855"
                    },
                    Button =
                    {
                        Color = button == check.A ? HexToCuiColor("#FBFBFF") : HexToCuiColor($"#BFBFBF", 0),
                    },
                    Text =
                    {
                        Text = $"", Align = TextAnchor.LowerRight, Font = "robotocondensed-bold.ttf", FontSize = 15
                    }
                }, "StaticMenu" + $".{check.B}.ListButton");
            }

            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("block.tab")]
        void BlockSelect(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();

            if (args.Args[0] == "select")
            {
                Dictionary<string, int> ListItems = new Dictionary<string, int>();
                ListCategory(args.Args[1], ListItems);
                
                OpenSelectCategory(player, ListItems, 1, args.Args[1]);
            }
            else if (args.Args[0] == "page")
            {
                int page = 1;
                if (int.TryParse(args.Args[1], out page))
                {
                    var nameBlock = args.Args[2];
                    Dictionary<string, int> ListItems = new Dictionary<string, int>();
                    ListCategory(nameBlock, ListItems);
                    OpenSelectCategory(player, ListItems, page, nameBlock);
                }
            }
        }

        void OpenSelectCategory(BasePlayer player, Dictionary<string, int> listItems, int page, string nameBlock)
        {
            CuiElementContainer container = new CuiElementContainer();

            CuiHelper.DestroyUi(player, "StaticMenu" + ".back");
            CuiHelper.DestroyUi(player, "StaticMenu" + ".next");


            for (int i = 0; i < 12; i++)
            {
                CuiHelper.DestroyUi(player, "StaticMenu" + $".{i}.ListItem");
            }

            int pagex = page + 1;



            container.Add(new CuiElement
            {
                Parent = "StaticMenu",
                Name = "StaticMenu" + ".next",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string)ImageLibrary?.Call("GetImage", "https://i.imgur.com/P5rDCQP.png"),
                        Color = pagex > 0 && (pagex - 1) * 12 < listItems.Count
                            ? HexToCuiColor("")
                            : HexToCuiColor("#FFFFFF", 10)
                    },
                    new CuiRectTransformComponent
                        { AnchorMin = "0.9104796 0.05933254", AnchorMax = "0.9437898 0.118665" },
                }
            });
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button =
                {
                    Color = "0 0 0 0",
                    Command = pagex > 0 && (pagex - 1) * 12 < listItems.Count
                        ? $"block.tab page {page + 1} {nameBlock}"
                        : ""
                },
                Text =
                {
                    Text = "", Font = "robotocondensed-regular.ttf",
                    FontSize = 20, Align = TextAnchor.MiddleCenter
                }
            }, "StaticMenu" + ".next");
            container.Add(new CuiElement
            {
                Parent = "StaticMenu",
                Name = "StaticMenu" + ".back",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string)ImageLibrary?.Call("GetImage", "https://i.imgur.com/b4TUg0Z.png"),
                        Color = page != 1 ? HexToCuiColor("") : HexToCuiColor("#FFFFFF", 10)
                    },
                    new CuiRectTransformComponent
                        { AnchorMin = "0.8716169 0.05933254", AnchorMax = "0.9049271 0.118665" },
                }
            });
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button =
                {
                    Color = "0 0 0 0",
                    Command = page != 1
                        ? $"block.tab page {page - 1} {nameBlock}"
                        : ""
                },
                Text =
                {
                    Text = "", Font = "robotocondensed-regular.ttf",
                    FontSize = 20, Align = TextAnchor.MiddleCenter
                }
            }, "StaticMenu" + ".back");

            foreach (var check in listItems.Select((i, t) => new { A = i, B = t - (page - 1) * 12 })
                         .Skip((page - 1) * 12).Take(12))
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin =
                            $"{0.05621097 + check.B * 0.150 - Math.Floor((double)check.B / 6) * 6 * 0.150} {0.5562423 - Math.Floor((double)check.B / 6) * 0.350}",
                        AnchorMax =
                            $"{0.1950035 + check.B * 0.150 - Math.Floor((double)check.B / 6) * 6 * 0.150} {0.8257107 - Math.Floor((double)check.B / 6) * 0.350}",
                        OffsetMax = "0 0"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = $"",
                    },
                    Text =
                    {
                        Text = "", Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf",
                        FontSize = 14, Color = HexToCuiColor($"#FBFBFF", 40),
                    }
                }, "StaticMenu", "StaticMenu" + $".{check.B}.ListItem");

                container.Add(new CuiElement
                {
                    Parent = "StaticMenu" + $".{check.B}.ListItem",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = (string)ImageLibrary?.Call("GetImage", "https://i.imgur.com/qwPt5ie.png")
                        },
                        new CuiRectTransformComponent
                            { AnchorMin = "0 0", AnchorMax = "1 1" },
                    }
                });
                container.Add(new CuiElement
                {
                    FadeOut = 0.3f,
                    Parent = "StaticMenu" + $".{check.B}.ListItem",
                    Components =
                    {
                        new CuiRawImageComponent
                            { FadeIn = 0.3f, Png = (string)ImageLibrary?.Call("GetImage", check.A.Key) },
                        new CuiRectTransformComponent
                            { AnchorMin = "0.26 0.5", AnchorMax = "0.745 0.9403672" }
                    }
                });
                
                string text = BlockTimeGui(check.A.Key) ? $"<color=#FBFBFF>{FormatShortTime(TimeSpan.FromSeconds((int) IsBlocked(check.A.Key)))}</color>" : "<color=#309E1E>Доступно</color>";
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0.05963308", AnchorMax = "1 0.4266056"},
                    Button = {Color = "0 0 0 0", FadeIn = 0.1f},
                    Text =
                    {
                        Text = $"{ItemManager.FindItemDefinition(check.A.Key).displayName.english}\n{text}",
                        FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf"
                    }
                }, "StaticMenu" + $".{check.B}.ListItem");
            }

            CuiHelper.AddUi(player, container);

        }
        
        public static string FormatShortTime(TimeSpan time)
        {
            string result = string.Empty;
            if (time.Days != 0)
                result += $"{time.Days} Д. ";

            if (time.Hours != 0)
                result += $"{time.Hours} Ч. ";

            if (time.Minutes != 0)
                result += $"{time.Minutes} М. ";

            if (time.Seconds != 0)
                result += $"{time.Seconds} С. ";

            return result;
        }


        public string HexToCuiColor(string HEX, float Alpha = 100)
        {
            if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

            var str = HEX.Trim('#');
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
        }
        
        


        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                hex = "#FFFFFFFF";
            }

            var str = hex.Trim('#');

            if (str.Length == 6)
                str += "FF";

            if (str.Length != 8)
            {
                throw new Exception(hex);
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            Color color = new Color32(r, g, b, a);

            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }

        private void DrawInstanceBlock(BasePlayer player, Item item)
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer container = new CuiElementContainer();
            string inputText = "Предмет {name} временно заблокирован,\nподождите {1}".Replace("{name}", item.info.displayName.english).Replace("{1}", $"{Convert.ToInt32(Math.Floor(TimeSpan.FromSeconds(IsBlocked(item.info)).TotalHours))} час {TimeSpan.FromSeconds(IsBlocked(item.info)).Minutes} минут.");
            
            container.Add(new CuiPanel
            {
                FadeOut = 1f,
                Image = { FadeIn = 1f, Color = "0.1 0.1 0.1 0" },
                RectTransform = { AnchorMin = "0.35 0.75", AnchorMax = "0.62 0.95" },
                CursorEnabled = false
            }, "Overlay", Layer);
            
            container.Add(new CuiElement
            {
                FadeOut = 1f,
                Parent = Layer,
                Name = Layer + ".Hide",
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0 0" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".Hide",
                Name = Layer + ".Destroy1",
                FadeOut = 1f,
                Components =
                {
                    new CuiImageComponent { Color = "0.4 0.4 0.4 0.7"},
                    new CuiRectTransformComponent { AnchorMin = "0 0.62", AnchorMax = "1.1 0.85" }
                }
                
            });
            container.Add(new CuiLabel
            {
                FadeOut = 1f,
                Text = {FadeIn = 1f, Color = "0.9 0.9 0.9 1", Text = "ПРЕДМЕТ ЗАБЛОКИРОВАН", FontSize = 22, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, Layer + ".Destroy1", Layer + ".Destroy5");
            container.Add(new CuiButton
            {
                FadeOut = 1f,
                RectTransform = { AnchorMin = "0 0.29", AnchorMax = "1.1 0.61" },
                Button = {FadeIn = 1f, Color = "0.3 0.3 0.3 0.5" },
                Text = { Text = "" }
            }, Layer + ".Hide", Layer + ".Destroy2");
            container.Add(new CuiLabel
            {
                FadeOut = 1f,
                Text = {FadeIn = 1f, Text = inputText, FontSize = 16, Align = TextAnchor.MiddleLeft, Color = "0.85 0.85 0.85 1" , Font = "robotocondensed-regular.ttf"},
                RectTransform = { AnchorMin = "0.04 0", AnchorMax = "10 0.9" }
            }, Layer + ".Hide", Layer + ".Destroy3");
            CuiHelper.AddUi(player, container);

            timer.Once(3f, () =>
            {
                CuiHelper.DestroyUi(player, Layer + ".Destroy1");
                CuiHelper.DestroyUi(player, Layer + ".Destroy2");
                CuiHelper.DestroyUi(player, Layer + ".Destroy3");
                CuiHelper.DestroyUi(player, Layer + ".Destroy5");
                timer.Once(1, () => CuiHelper.DestroyUi(player, Layer));
            });
        }

        #endregion

        #region Functions
        private double IsBlocked(string shortname)
        {
            if (!settings.SBlock.items.ContainsKey(shortname))
                return 0;
            var blockTime = settings.SBlock.items.FirstOrDefault(p => p.Key == shortname).Value;
            var lefTime = (UnBlockTime(blockTime)) - CurrentTime();
            return lefTime > 0 ? lefTime : 0;
        }
        
        private double UnBlockTime(int amount) => SaveRestore.SaveCreatedTime.ToUniversalTime().Subtract(epoch).TotalSeconds + amount;
        private double IsBlocked(ItemDefinition itemDefinition) => IsBlocked(itemDefinition.shortname);

        private bool BlockTimeGui(string shortName)
        {
            var blockLeft = UnBlockTime(settings.SBlock.items[shortName]) - CurrentTime();
            if (blockLeft > 0)
            {
                return true;
            }

            return false;
        }
        

        private void ListCategory(string Category, Dictionary<string, int> Items)
        {
            var category = settings.SBlock.CategoriesName.FirstOrDefault(p => p.Value == Category);
            foreach (var value in settings.SBlock.items)
            {
                ItemDefinition definition = ItemManager.FindItemDefinition(value.Key);
                if (definition.category.ToString() == category.Key)
                {
                    Items.Add(value.Key, value.Value);
                }
            }
        }

        private void ListBlocked(List<string> fillDictionary)
        {
            foreach (var category in settings.SBlock.items)
            {
                ItemDefinition definition = ItemManager.FindItemDefinition(category.Key);
                string catName = settings.SBlock.CategoriesName[definition.category.ToString()];
                if (!fillDictionary.Contains(catName))
                    fillDictionary.Add(catName);
            }
        }
        
        

        
        #endregion

        #region Utils
        static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static double CurrentTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }
        
        public static string ToShortString(TimeSpan timeSpan)
        {
            int i = 0;
            string resultText = "";
            if (timeSpan.Days > 0)
            {
                resultText += timeSpan.Days + " День";
                i++;
            }
            if (timeSpan.Hours > 0 && i < 2)
            {
                if (resultText.Length != 0)
                    resultText += " ";
                resultText += timeSpan.Days + " Час";
                i++;
            }
            if (timeSpan.Minutes > 0 && i < 2)
            {
                if (resultText.Length != 0)
                    resultText += " ";
                resultText += timeSpan.Days + " Мин.";
                i++;
            }
            if (timeSpan.Seconds > 0 && i < 2)
            {
                if (resultText.Length != 0)
                    resultText += " ";
                resultText += timeSpan.Days + " Сек.";
                i++;
            }

            return resultText;
        }
        
        private void GetConfig<T>(string menu, string key, ref T varObject)
        {
            if (Config[menu, key] != null)
            {
                varObject = Config.ConvertValue<T>(Config[menu, key]);
            }
            else
            {
                Config[menu, key] = varObject;
            }
        }

        private bool playerOnDuel(BasePlayer player)
        {
            if (Duels != null)
                if (Duels.Call<bool>("inDuel", player))
                    return true;

            if (Battles != null)
                if (Battles.Call<bool>("IsPlayerOnBattle", player.userID))
                    return true;

            return false;
        }

        private void SendFilePng(BasePlayer player, string imageName, ulong imageId = 0)
        {
            if (!ImageLibrary.Call<bool>("HasImage", imageName, imageId))
                return;

            uint crc = ImageLibrary.Call<uint>("GetImage", imageName, imageId);
            byte[] array = FileStorage.server.Get(crc, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);

            if (array == null)
                return;

            CommunityEntity.ServerInstance.ClientRPCEx<uint, uint, byte[]>(new Network.SendInfo(player.net.connection)
            {
                channel = 2,
                method = Network.SendMethod.Reliable
            }, null, "CL_ReceiveFilePng", crc, (uint)array.Length, array);
        }
        #endregion

        #region Coroutines
        
        #endregion
    }
}