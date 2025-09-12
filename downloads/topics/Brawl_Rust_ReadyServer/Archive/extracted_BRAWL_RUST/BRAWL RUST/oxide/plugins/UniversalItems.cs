using System.Text;
using System;
using Newtonsoft.Json;
using System.Linq;
using Oxide.Game.Rust.Cui;
using Oxide.Core;
using UnityEngine;
using System.Collections.Generic;
using WebSocketSharp;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("UniversalItems", "DezLife", "1.9.9")]
    [Description("Универсальные предметы")]
    public class UniversalItems : RustPlugin
    {

                private object CanBeRecycled(Item item, Recycler recycler)
        {
            if (item == null)
                return false;
            var cfg = config.ItemsSetings[Type.Переработать];
            if (item.info.shortname == cfg.ShortName && item.skin == cfg.SkinIDI)
                return true;
            return null;
        }

        private static System.Random random = new System.Random();
        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!furnacec)
                return;
            if (entity == null) return;
            if (entity is BaseOven && !(entity is BaseFuelLightSource))
            {
                BaseOven baseOven = entity as BaseOven;
                if (baseOven == null) return;
                FurnaceBurn fBurn = new FurnaceBurn();
                fBurn.OvenTogle(baseOven);
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        bool furnacec = false;

        private void Unload()
        {
            foreach (BasePlayer item in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(item, IconLayer);
                CuiHelper.DestroyUi(item, MAINMENU);
            }
        }
        private static string HexToRustFormat(string hex)
        {
            Color color;
            ColorUtility.TryParseHtmlString(hex, out color);
            sb.Clear();
            return sb.AppendFormat("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a).ToString();
        }

        private class Configuration
        {

            [JsonProperty("Настройки уникальных предметов")]
            public Dictionary<Type, UItems> ItemsSetings = new Dictionary<Type, UItems>();
            [JsonProperty("Настройки")]
            public Setings setings = new Setings();
            public class UItems
            {
                public int GetItemAmount(BasePlayer player) => player.inventory.GetAmount(GetItemId());

                public int GetItemId() => ItemManager.FindItemDefinition(ShortName).itemid;

                public Item Copy(int amount = 1)
                {
                    Item x = ItemManager.CreateByPartialName(ShortName, amount);
                    x.skin = SkinIDI;
                    x.name = Name;
                    return x;
                }
                [JsonProperty("Предметы которые будут выпадать")]
                public List<ItemsDrop> ItemsDrop;
                [JsonProperty("Список ящиков/бочек в которых будет появлятся предмет и шанс")]
                public Dictionary<string, int> CrateDrop;
                [JsonProperty("Шорт нейм предмета")]
                public string ShortName;
                [JsonProperty("ID скина для предмета")]
                public ulong SkinIDI;

                public Item CreateItem(int amount)
                {
                    Item item = ItemManager.CreateByPartialName(ShortName, amount);
                    item.name = Name;
                    item.skin = SkinIDI;
                    return item;
                }
                [JsonProperty("Названия предмета")]
                public string Name;
                [JsonProperty("Описания предмета")]
                public string Descriptions;
            }
            public class ItemsDrop
            {
                [JsonProperty("Шорт нейм предмета")]
                public string Shortname;
                [JsonProperty("ID скина для предмета")]
                public ulong SkinID;
                [JsonProperty("Минимальное количество при выпадени")]
                public int MinimalAmount = 0;
                [JsonProperty("Максимальное количество при выпадении")]
                public int MaximumAmount = 0;
            }

            public class Setings
            {
                [JsonProperty("Включить иконку с информацией о предметах в углу экрана ?")]
                public bool GuiInfo = true;
                [JsonProperty("Включить радиацию при переплавки")]
                public bool Radiation = true;
                [JsonProperty("Количевство радиации каторое будет даваться каждый тик")]
                public float Radiations = 10f;
                [JsonProperty("Радиус радиации в метрах (От печки)")]
                public float RadiationsRadius = 15f;
                [JsonProperty("Текст который будет написан в гуи если радиация при переплавке включена")]
                public string RadiationGui = "<color=#ffca29>ОСТОРОЖНО!</color>\nПри переплавке этого предмета могут распространяться частицы радиации";
                [JsonProperty("Титл в гуи")]
                public string TitleGui = "МЕНЮ УНИКАЛЬНЫХ ПРЕДМЕТОВ";
                [JsonProperty("Включить иконку с кнопкой крафта")] public bool ButonOpenMenu = true;
                [JsonProperty("Картинка для кнопки")] public string PngForButton = "https://i.imgur.com/ffy28FG.png";
                [JsonProperty("OffsetMin кнопки")] public string Ofssemin = "3 -50";
                [JsonProperty("OffsetMax кнопки")] public string Ofsetmax = "50 -3";
                [JsonProperty("Команда для открытия меню")] public string CommandOpen = "Uitem";
            }
        }
		   		 		  						  	   		  	 				  	 				   					  		 	
        private void OnPlayerConnected(BasePlayer player)
        {
            if (config.setings.ButonOpenMenu)
                InitializeIconButton(player);
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                    LoadDefaultConfig();
                ValidateConfig();
                SaveConfig();
            }
            catch (JsonException ex)
            {
                Debug.LogException(ex);
                LoadDefaultConfig();
            }
        }

        
        
        public class FurnaceBurn
        {
            BaseOven oven;
            StorageContainer storageContainer;
            Timer timer;

            public void OvenTogle(BaseOven oven)
            {
                this.oven = oven;
                storageContainer = oven.GetComponent<StorageContainer>();
                timertick();
            }
		   		 		  						  	   		  	 				  	 				   					  		 	
            void timertick()
            {
                if (timer == null)
                {
                    timer = instance.timer.Once(5f, CheckRadOres);
                }
                else
                {
                    timer.Destroy();
                    timer = instance.timer.Once(5f, CheckRadOres);
                }
            }

            void CheckRadOres()
            {
                if (oven == null)
                {
                    timer.Destroy();
                    return;
                }
                if (oven.IsOn())
                {
                    foreach (var item in storageContainer.inventory.itemList)
                    {
                        if (instance.config.ItemsSetings[Type.Переплавить].SkinIDI == item.skin)
                        {
                            var cfg = instance.config.ItemsSetings[Type.Переплавить];

                            instance.NextTick(() =>
                            {
                                if (instance.config.setings.Radiation)
                                {
                                    List<BasePlayer> players = new List<BasePlayer>();
                                    Vis.Entities<BasePlayer>(oven.transform.position, instance.config.setings.RadiationsRadius, players);
                                    players.ForEach(p => p.metabolism.radiation_poison.value += instance.config.setings.Radiations);
                                }

                                if (item.amount > 1) item.amount--;
                                else item.RemoveFromContainer();

                                int RandomItem = UnityEngine.Random.Range(0, cfg.ItemsDrop.Count);
                                Item newItem = ItemManager.CreateByName(cfg.ItemsDrop[RandomItem].Shortname, UnityEngine.Random.Range(cfg.ItemsDrop[RandomItem].MinimalAmount, cfg.ItemsDrop[RandomItem].MaximumAmount), cfg.ItemsDrop[RandomItem].SkinID);
                                if (!newItem.MoveToContainer(storageContainer.inventory))
                                    newItem.Drop(oven.transform.position, Vector3.up);
                            });
                        }
                    }
                }
                timertick();
            }
        }
        private void GUI_MAIN(BasePlayer player, Type type)
        {
            var Gui = new CuiElementContainer();

            if (type == Type.None)
            {
                CuiHelper.DestroyUi(player, MAINMENU);
                CuiHelper.DestroyUi(player, "ElementInfo");

                Gui.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image = { Color = "0 0 0 0.8", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
                }, "Overlay", MAINMENU);

                Gui.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "-100 -100", AnchorMax = "100 100" },
                    Button = { Close = MAINMENU, Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, MAINMENU);

                Gui.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.8796875 0.9425926", AnchorMax = "0.9947916 0.995372280" },
                    Button = { Close = MAINMENU, Color = "0 0 0 0" },
                    Text = { Text = lang.GetMessage("CLOSE_GUI", this, player.UserIDString), FontSize = 24, Align = TextAnchor.MiddleCenter }
                }, MAINMENU);
		   		 		  						  	   		  	 				  	 				   					  		 	
                Gui.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.0989586 0.2009263", AnchorMax = "0.8859376 0.942593" },
                    Image = { Color = "0 0 0 0" }
                }, MAINMENU, "main");

                Gui.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.433208", OffsetMax = "0 0" },
                    Text = { Text = lang.GetMessage("INFO_GUI", this, player.UserIDString), FontSize = 23, Font = "RobotoCondensed-Regular.ttf", Align = TextAnchor.MiddleCenter }
                }, "main");

                Gui.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0.8589257", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = lang.GetMessage("TITLE_GUI", this, player.UserIDString), FontSize = 23, Font = "RobotoCondensed-Regular.ttf", Align = TextAnchor.MiddleCenter }
                }, "main");

                int x = 0;
		   		 		  						  	   		  	 				  	 				   					  		 	
                foreach (var element in config.ItemsSetings)
                {
                    Gui.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{0.08934465 + (x * 0.33)} 0.3770282", AnchorMax = $"{0.3011248 + (x * 0.33)} 0.7765288" },
                        Image = { Color = HexToRustFormat("#5A5A5A71") }
                    }, "main", "elements");

                    Gui.Add(new CuiElement
                    {
                        Parent = "elements",
                        Components = {
                    new CuiRawImageComponent
                    {
                        Png = (string)ImageLibrary.Call("GetImage", element.Value.ShortName, element.Value.SkinIDI), Color = "1 1 1 1",
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.016949 0.01910812",
                        AnchorMax = "0.983051 0.9840761"
                    },
                    }
                    });
                    int en = Convert.ToInt32(element.Key);
                    Gui.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Button = { Color = "0 0 0 0", Command = $"GoToInfo {en}" },
                        Text = { Text = "", Color = "0 0 0 0" }
                    }, "elements");
                    x++;
                }
            }
		   		 		  						  	   		  	 				  	 				   					  		 	
            Type types = type == Type.Переплавить ? Type.Переплавить : type == Type.Переработать ? Type.Переработать : type == Type.Потрошить ? Type.Потрошить : Type.None;
            if (types != Type.None)
            {
                var cfg = config.ItemsSetings[types];

                CuiHelper.DestroyUi(player, "main");

                Gui.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.0989586 0.2009263", AnchorMax = "0.8859376 0.942593" },
                    Image = { Color = "0 0 0 0" }
                }, MAINMENU, "ElementInfo");

                Gui.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0.8589257", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = cfg.Name, FontSize = 25, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
                }, "ElementInfo");

                Gui.Add(new CuiElement
                {
                    Parent = "ElementInfo",
                    Components = {
                    new CuiRawImageComponent
                    {
                        Png = (string)ImageLibrary.Call("GetImage", cfg.ShortName, cfg.SkinIDI), Color = "1 1 1 1",
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.4003971 0.4519346",
                        AnchorMax = "0.5989414 0.8264664"
                    },
                    }
                });

                Gui.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.4157298" },
                    Text = { Text = cfg.Descriptions, FontSize = 21, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
                }, "ElementInfo");

                if (types == Type.Переплавить && config.setings.Radiation == true)
                {
                    Gui.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.1410731" },
                        Text = { Text = config.setings.RadiationGui.ToUpper(), FontSize = 19, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
                    }, "ElementInfo");
                }

                Gui.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0.9425926", AnchorMax = "0.1052083 1" },
                    Button = { Command = "GoToInfo 0", Color = "0 0 0 0" },
                    Text = { Text = lang.GetMessage("BACK_GUI", this, player.UserIDString), FontSize = 24, Align = TextAnchor.MiddleCenter }
                }, MAINMENU);
            }

            CuiHelper.AddUi(player, Gui);

        }

        private void ValidateConfig()
        {
            if (config.ItemsSetings.Count == 0)
            {
                config.ItemsSetings = new Dictionary<Type, Configuration.UItems>
                {
                    [Type.Переработать] = new Configuration.UItems
                    {
                        Name = "ЗОЛОТАЯ ПОДКОВА УДАЧИ",
                        Descriptions = "С ЭТОЙ ПОДКОВОЙ ВАМ УЛЫБНЕТСЯ УДАЧА! ПЕРЕРАБОТАЙТЕ ЕЕ И ПОЛУЧИТЕ ЧТО-ТО ВЗАМЕН",
                        ShortName = "sticks",
                        SkinIDI = 1742207203,
                        ItemsDrop = new List<Configuration.ItemsDrop>
                       {
                            new Configuration.ItemsDrop{Shortname = "smg.2", SkinID = 0, MinimalAmount = 1, MaximumAmount = 1 },
                            new Configuration.ItemsDrop{Shortname = "grenade.smoke", SkinID = 0, MinimalAmount = 1, MaximumAmount = 5 },
                            new Configuration.ItemsDrop{Shortname = "weapon.mod.silencer", SkinID = 0, MinimalAmount = 1, MaximumAmount = 1 },
                            new Configuration.ItemsDrop{Shortname = "rifle.l96", SkinID = 0, MinimalAmount = 1, MaximumAmount = 1 }
                       },
                        CrateDrop = new Dictionary<string, int>
                        {
                            ["loot-barrel-1"] = 30,
                            ["bradley_crate"] = 50,
                            ["crate_elite"] = 70,
                        }
                    },
                    [Type.Потрошить] = new Configuration.UItems
                    {
                        Name = "ЗОЛОТОЙ ЧЕРЕП",
                        Descriptions = "ЗОЛОТОЙ ЧЕРЕП ЛУЧШАЯ ДОБЫЧА МАРОДЕРА! ПОТРОШИ ЭТОТ ЧЕРЕП ЧТОБЫ ЗАБРАТЬ ИЗ НЕГО ЧТО-ТО!",
                        ShortName = "skull.human",
                        SkinIDI = 1683645276,
                        ItemsDrop = new List<Configuration.ItemsDrop>
                       {
                            new Configuration.ItemsDrop{Shortname = "jackhammer", SkinID = 0, MinimalAmount = 1, MaximumAmount = 1 },
                            new Configuration.ItemsDrop{Shortname = "explosive.timed", SkinID = 0, MinimalAmount = 1, MaximumAmount = 2 },
                            new Configuration.ItemsDrop{Shortname = "supply.signal", SkinID = 0, MinimalAmount = 1, MaximumAmount = 1 },
                            new Configuration.ItemsDrop{Shortname = "flare", SkinID = 0, MinimalAmount = 1, MaximumAmount = 15 },
                       },
                        CrateDrop = new Dictionary<string, int>
                        {
                            ["loot-barrel-1"] = 30,
                            ["crate_tools"] = 50,
                            ["heli_crate"] = 70,
                        }
                    },
                    [Type.Переплавить] = new Configuration.UItems
                    {
                        Name = "АРТЕФАКТ",
                        Descriptions = "НЕОБЫЧНЫЙ АРТЕФАКТ,ТАКИХ НЕТ НИГДЕ! ПРИ ПЕРЕПЛАВКИ ПРЕВРАЩАЕТСЯ В КАКОЙ-ТО ПРЕДМЕТ!",
                        ShortName = "glue",
                        SkinIDI = 1714466074,
                        ItemsDrop = new List<Configuration.ItemsDrop>
                       {
                            new Configuration.ItemsDrop{Shortname = "sulfur", SkinID = 0, MinimalAmount = 200, MaximumAmount = 300 },
                            new Configuration.ItemsDrop{Shortname = "metal.refined", SkinID = 0, MinimalAmount = 10, MaximumAmount = 100 },
                            new Configuration.ItemsDrop{Shortname = "stones", SkinID = 0, MinimalAmount = 1000, MaximumAmount = 5000 },
                            new Configuration.ItemsDrop{Shortname = "scrap", SkinID = 0, MinimalAmount = 100, MaximumAmount = 300 },
                       },
                        CrateDrop = new Dictionary<string, int>
                        {
                            ["loot-barrel-1"] = 30,
                            ["crate_underwater_basic"] = 50,
                            ["supply_drop"] = 70,
                        }
                    },
                };
            }
        }
        private object CanRecycle(Recycler recycler, Item item)
        {
            var cfg = config.ItemsSetings[Type.Переработать];
            if (item.info.shortname == cfg.ShortName && item.skin == cfg.SkinIDI)
                return true;
            return null;
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }

        private void InitializeIconButton(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, IconLayer);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Parent = "Overlay",
                Name = IconLayer,
                Components =
                {
                      new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", config.setings.CommandOpen) },
                      new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = config.setings.Ofssemin, OffsetMax = config.setings.Ofsetmax}
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = $"chat.say /{config.setings.CommandOpen}" },
                Text = { Text = "" }
            }, IconLayer);

            CuiHelper.AddUi(player, container);
        }
        [PluginReference] private Plugin ImageLibrary;
        
        void ChatInfoMenu(BasePlayer player)
        {
            GUI_MAIN(player, 0);
        }

        [ConsoleCommand("GoToInfo")]
        void CommandOpenInfo(ConsoleSystem.Arg arg)
        {
            int enums = Convert.ToInt32(arg.Args[0]);
            Type types = (Type)enums;
            GUI_MAIN(arg.Player(), types);
        }
        public static UniversalItems instance;
        public static StringBuilder sb = new StringBuilder();

                private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TITLE_GUI"] = "UNIQUE ITEMS MENU",
                ["INFO_GUI"] = "TO FIND OUT DETAILED INFORMATION ABOUT THE SUBJECT INTERESTING IN YOU, CLICK ON IT :)",
                ["CLOSE_GUI"] = "CLOSE",
                ["BACK_GUI"] = "BACK",

            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TITLE_GUI"] = "МЕНЮ УНИКАЛЬНЫХ ПРЕДМЕТОВ",
                ["INFO_GUI"] = "ЧТО БЫ УЗНАТЬ ПОДРОБНУЮ ИНФОРМАЦИЮ О ИНТЕРЕСУЮЩЕМ ВАС ПРЕДМЕТЕ, НАЖМИТЕ НА НЕГО :)",
                ["CLOSE_GUI"] = "ЗАКРЫТЬ",
                ["BACK_GUI"] = "НАЗАД",

            }, this, "ru");
        }
        public static string IconLayer = "MAIN_MENU_ICON";
        private object OnItemAction(Item item, string action, BasePlayer player)
        {
            var cfg = config.ItemsSetings[Type.Потрошить];
            if (action == "crush" && item.skin == cfg.SkinIDI)
            {
                int RandomItem = random.Next(cfg.ItemsDrop.Count);
                Item itemS = ItemManager.CreateByName(cfg.ItemsDrop[RandomItem].Shortname, random.Next(cfg.ItemsDrop[RandomItem].MinimalAmount, cfg.ItemsDrop[RandomItem].MaximumAmount), cfg.ItemsDrop[RandomItem].SkinID);
                player.GiveItem(itemS, BaseEntity.GiveItemReason.PickedUp);
                ItemRemovalThink(item, player, 1);
                return false;
            }
            return null;
        }
        
        public static string MAINMENU = "MAIN_MENU";
      
        private void OnLootSpawn(LootContainer container)
        {
            if (container == null)
                return;
            int RandomItem = random.Next(config.ItemsSetings.Count);
            var cfg = config.ItemsSetings.ElementAt(RandomItem).Value;

            foreach (var crate in cfg.CrateDrop)
            {
                if (container.PrefabName.Contains(crate.Key))
                { 
                    if (random.Next(0, 100) >= (100 - crate.Value))
                    {
                        InvokeHandler.Instance.Invoke(() =>
                        {
                            if (container.inventory.capacity <= container.inventory.itemList.Count)
                            {
                                container.inventory.capacity = container.inventory.itemList.Count + 1;
                            }
                            var item = cfg.Copy();
                            item?.MoveToContainer(container.inventory);
                        }, 0.21f);
                    }
                }
            }
        }
        

        private static void ItemRemovalThink(Item item, BasePlayer player, int itemsToTake)
        {
            if (item.amount == itemsToTake)
            {
                item.RemoveFromContainer();
                item.Remove();
            }
            else
            {
                item.amount = item.amount - itemsToTake;
                player.inventory.SendSnapshot();
            }
        }
        
        
        [ChatCommand("Give.Utest")]
        private void CmdChatDebugGoldfSpawn(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            foreach (var Items in config.ItemsSetings)
            {
                var item = Items.Value.CreateItem(10);
                item.MoveToContainer(player.inventory.containerMain);
            }
        }
        
                private Configuration config;
		   		 		  						  	   		  	 				  	 				   					  		 	
        private object OnRecycleItem(Recycler recycler, Item item)
        {
            var cfg = config.ItemsSetings[Type.Переработать];

            if (item.info.shortname == cfg.ShortName && item.skin == cfg.SkinIDI)
            {
                item.UseItem(1);
                int RandomItem = random.Next(cfg.ItemsDrop.Count);
                recycler.MoveItemToOutput(ItemManager.CreateByName(cfg.ItemsDrop[RandomItem].Shortname, random.Next(cfg.ItemsDrop[RandomItem].MinimalAmount, cfg.ItemsDrop[RandomItem].MaximumAmount), cfg.ItemsDrop[RandomItem].SkinID));
                return true;
            }
            return null;
        }

        private void OnServerInitialized()
        {
            instance = this;
            furnacec = true;

            if (!ImageLibrary)
            {
                NextTick(() => {
                    PrintError("Не найден ImageLibrary, плагин не будет работать!");
                    Interface.Oxide.UnloadPlugin(Name);
                });
                return;
            }

            cmd.AddChatCommand(config.setings.CommandOpen, this, nameof(ChatInfoMenu));
            if (config.setings.ButonOpenMenu)
                ImageLibrary.Call("AddImage", config.setings.PngForButton, config.setings.CommandOpen);

            foreach (var cfg in config.ItemsSetings.Values)
            {
                if (cfg.ShortName.IsNullOrEmpty())
                {
                    ImageLibrary.Call("AddImage", $"http://api.skyplugins.ru/api/getimage/{cfg.ShortName}/512", cfg.ShortName + 128, cfg.SkinIDI);
                }
                if (cfg.SkinIDI != 0)
                {
                    ImageLibrary.Call("AddImage", $"http://api.skyplugins.ru/api/getskin/{cfg.SkinIDI}/512", cfg.ShortName, cfg.SkinIDI);
                }
            }
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);

            var baseOvens = BaseNetworkable.serverEntities.Where(x => x as BaseOven);
            foreach (var item in baseOvens)
            {
                if (!(item is BaseFuelLightSource))
                {
                    OnEntitySpawned(item);
                }
            }
        }
        public enum Type
        {
            None = 0,
            Переплавить = 1,
            Переработать = 2,
            Потрошить = 3
        }   
            }
}
