using System.Collections.Generic;
using UnityEngine;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("TPMenuSystem", "/https://blackplugin.ru/", "11.0.1")]
    class TPMenuSystem : RustPlugin
    {
        #region Вар
        [PluginReference] Plugin TPShop, TPMiningFarm, TPBaraxolka, Economics, ImageLibrary, TPRulesSystem, TPWipeBlock, TPKits, TPCases, TPStatsSystem, TPTeleportation, TPReportSystem, TPSkinMenu, TPBattlePass, GameStoresRUST, TPLotterySystem, SkinDrop, TPWipeSchedule, VKBot, TPChat, TPSkillSystem;

        public string Layer = "Menu_UI";

        Dictionary<ulong, bool> hidden = new Dictionary<ulong, bool>();
        Dictionary<ulong, string> activeButton = new Dictionary<ulong, string>();

        public class CommandSettings {
            [JsonProperty("Название отображаемое в меню")] public string DisplayName;
            [JsonProperty("Выполняемая команда в меню")] public string Command;
            [JsonProperty("Текст")] public string Text;
        }

        public class Settings {
            [JsonProperty("Название отображаемое в меню")] public string DisplayName;
            [JsonProperty("Выполняемая команда в меню")] public string Command;
            [JsonProperty("Изображение которое будет отображаться на кнопке")] public string Url;
        }
        #endregion

        #region Конфиг
        Configuration config;
        class Configuration 
        {
            [JsonProperty("Изображение беннера в окне с информацией")] public string BannerURL = "https://";
            [JsonProperty("Первый заголовок в окне с информацией")] public string Title1 = "ДОБРО ПОЖАЛОВАТЬ НА KAMCHATKA";
            [JsonProperty("Первый текст с информацией")] public string Text1 = "Текст заполнитель - это текст, который имеет некоторые характеристики реального письменного текста, но является случайным набором слов или сгенерирован иным образом. Его можно использовать для отображения образца шрифтов, создание текста для тестирования или обхода.";
            [JsonProperty("Заголовок донат магазина в окне с информацией")] public string ShopTitle = "ДОНАТ МАГАЗИН";
            [JsonProperty("Ссылка донат магазина в окне с информацией")] public string ShopText = "RUST.GOVNOSTORE.COM";
            [JsonProperty("QRCode изображение донат магазина в окне с информацией")] public string ShopQR = "https://";
            [JsonProperty("Заголовок дискорда в окне с информацией")] public string DSTitle = "НАШ ДИСКОРД";
            [JsonProperty("Ссылка на группу в дискорде в окне с информацией")] public string DSText = "RUST.GOVNOSTORE.COM";
            [JsonProperty("QRCode изображение дискорда в окне с информацией")] public string DSQR = "https://";
            [JsonProperty("Заголовок вк в окне с информацией")] public string VKTitle = "ДОНАТ МАГАЗИН";
            [JsonProperty("Ссылка вк в окне с информацией")] public string VKText = "RUST.GOVNOSTORE.COM";
            [JsonProperty("QRCode изображение вк в окне с информацией")] public string VKQR = "https://";
            [JsonProperty("Кнопки в инфо")] public List<CommandSettings> command;
            [JsonProperty("Настройки навигации меню")] public List<Settings> settings;
            public static Configuration GetNewConfig() 
            {
                return new Configuration
                {
                    command = new List<CommandSettings>() {
                        new CommandSettings {
                            DisplayName = "Команды",
                            Command = "command2",
                            Text = "<color=#ffa987><b>КОМАНДЫ СЕРВЕРА:</b></color>\n\n<color=orange>/duel</color> - вызвать на дуэль\n<color=orange>/BPKits</color> - доступные наборы\n<color=orange>/map</color> - карта сервера\n<color=orange>/lottery</color> - ежедневная лотерея\n<color=orange>/remove</color> - удаление построек\n<color=orange>/up</color> - улучшение построек\n<color=orange>/store</color> - корзина магазина\n<color=orange>/skin</color> - изменить скин предмета\n<color=orange>/tpmenu</color> - система телепортации\n<color=orange>/vk</color> - уведомления о рейдах и бонус\n<color=orange>/trade</color> - обмен вещами\n<color=orange>/chat</color> - Чат система\n<color=orange>/wipe</color> - расписание вайпов\n<color=orange>/block</color> - блок предметов\n<color=orange>/top</color> - система статистики\n<color=orange>/friend</color> - система друзей\n<color=orange>/report</color> - жалобы на игроков\n<color=orange>/info</color> - информация о сервере\n<color=orange>/rates</color> - узнать свои рейты\n\n☑ Наша группа - <color=#ffa987><b>vk.com/kamgamer</b></color>      ☑ Наш магазин - <color=#ffa987><b>kamchatka.gamestores.app</b></color>"
                        },
                        new CommandSettings {
                            DisplayName = "Бинды сервера",
                            Command = "command3",
                            Text = "<color=#ffa987><b>БИНДЫ СЕРВЕРА: Команды ввводить в консоль (F1)</b></color>\n\n\n<color=orange>bind m chat.say /map</color> - открытие карты на М (англ)\n<color=orange>bind x chat.say /menu</color> - открывает на клавишу X меню сервера\n<color=orange>bind z menu.tp</color> - открытие меню тп на клавишу Z (англ)\n<color=orange>bind c menu.friend</color> - открытие меню тп к друзьям на клавишу С (англ)\n<color=orange>bind v menu.friendset</color> - открытие меню настройки друзей на клавишу V (англ)\n<color=orange>bind u menu.up</color> - открытие меню апгрейда на клавишу U (англ)\n<color=orange>bind t menu.trade</color> - открытие меню трейда на клавишу T (англ)\n\n☑ Наша группа - <color=#ffa987><b>vk.com/kamgamer</b></color>      ☑ Наш магазин - <color=#ffa987><b>kamchatka.shop</b></color>"
                        },
                        new CommandSettings {
                            DisplayName = "Уникальность",
                            Command = "command4",
                            Text = "<color=#ffa987><b>Уникальность:</b></color>\n\n<color=orange>КАРЬЕРЫ</color> - На карте <color=orange>4</color> карьера, они добывают <color=orange>ресурсы</color> в <color=orange>переработанном виде</color>, но их нужно захватить и удерживать.\n<color=orange>ТЕЛЕПОРТ В ГОРОД БАНДИТОВ И НПС</color> - Пропишите команду <color=orange>/tpmenu</color> в нём телепорт в <color=orange>город бандитов</color> и <color=orange>город нпс</color>.\n<color=orange>ДУЭЛИ СО СТАВКАМИ</color> -  вызвать на дуэль любого игрока и сделать ставку в виде ресурсов командой <color=orange>/duel</color>.\n<color=orange>СТАТИСТИКА С НАГРАДАМИ</color> - Займите <color=orange>1</color> место в топе и выиграйте деньги на баланс который указан.\n<color=orange>ДЕНЬГИ НА БАЛАНС</color> - Заходя на сервер с правой стороны будет <color=orange>UI</color> уведомление о бонусе в виде денег на баланс магазина.\n<color=orange>GIV УВЕДОМЛЕНИЕ</color> - Прописав команду <color=orange>/vk</color> вы сможете получить бонус и подключить уведомление о рейде.\n\n<color=orange>БЕСПЛАТНЫЕ СКИНЫ</color> - Прописав команду <color=orange>/skin</color>  выбирать любой скин на любой предмет бесплатно.\n\n☑ Наша группа - <color=#ffa987><b>vk.com/kamgamer</b></color>      ☑ Наш магазин - <color=#ffa987><b>kamchatka.shop</b></color>"
                        }
                    },
                    settings = new List<Settings>()
                    {
                        new Settings {
                            DisplayName = "Правила",
                            Command = "rules",
                            Url = "https://i.ibb.co/2hCcKvw/RRJLrbU.png"
                        },
                        new Settings {
                            DisplayName = "Скилы",
                            Command = "skill",
                            Url = "https://i.ibb.co/2hCcKvw/RRJLrbU.png"
                        },
                        new Settings {
                            DisplayName = "Вайп блок",
                            Command = "block",
                            Url = "https://i.ibb.co/2hCcKvw/RRJLrbU.png"
                        },
                        new Settings {
                            DisplayName = "Наборы",
                            Command = "kit",
                            Url = "https://i.ibb.co/2hCcKvw/RRJLrbU.png"
                        },
                        new Settings {
                            DisplayName = "Кейсы",
                            Command = "case1",
                            Url = "https://i.ibb.co/2hCcKvw/RRJLrbU.png"
                        },
                        new Settings {
                            DisplayName = "Статистика",
                            Command = "stat",
                            Url = "https://i.ibb.co/2hCcKvw/RRJLrbU.png"
                        },
                        new Settings {
                            DisplayName = "Телепортация",
                            Command = "teleport",
                            Url = "https://i.ibb.co/2hCcKvw/RRJLrbU.png"
                        },
                        new Settings {
                            DisplayName = "Репорты",
                            Command = "report",
                            Url = "https://i.ibb.co/2hCcKvw/RRJLrbU.png"
                        },
                        new Settings {
                            DisplayName = "Скины",
                            Command = "skin",
                            Url = "https://i.ibb.co/2hCcKvw/RRJLrbU.png"
                        },
                        new Settings {
                            DisplayName = "Лотерея",
                            Command = "lot",
                            Url = "https://i.ibb.co/2hCcKvw/RRJLrbU.png"
                        },
                        new Settings {
                            DisplayName = "Скин дроп",
                            Command = "drop",
                            Url = "https://i.ibb.co/2hCcKvw/RRJLrbU.png"
                        },
                        new Settings {
                            DisplayName = "Вайп",
                            Command = "wipe",
                            Url = "https://i.ibb.co/2hCcKvw/RRJLrbU.png"
                        },
                        new Settings {
                            DisplayName = "Привязка",
                            Command = "bot",
                            Url = "https://i.ibb.co/2hCcKvw/RRJLrbU.png"
                        },
                        new Settings {
                            DisplayName = "Чат",
                            Command = "chat",
                            Url = "https://i.ibb.co/2hCcKvw/RRJLrbU.png"
                        },
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
                if (config?.settings == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Ошибка чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfig();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Хуки
        Dictionary<string, string> imageMenu = new Dictionary<string, string>() {
            ["backgroundhidden"] = "https://i.ibb.co/Pmn3TH1/TwnHrDb.png",
            ["backgroundshow"] = "https://i.ibb.co/tbxy3PD/tYRqjVa.png",
            ["backgroundbutton"] = "https://i.ibb.co/TYsp1zP/W7Q62vH.png",
            ["backgroundtextbutton"] = "https://i.ibb.co/3vQ2LKR/tmpHyNX.png",
            ["hiddenarrow"] = "https://i.ibb.co/Vq2YkLX/TnSlWn9.png",
            ["showarrow"] = "https://i.ibb.co/xjsFNY2/r8ur8Us.png",
            ["textshow"] = "https://i.ibb.co/nc01LHz/IsEKNVQ.png",
            ["activeButton"] = "https://i.ibb.co/LCyw9XC/wgQO01k.png",
            ["foninfo"] = "https://gspics.org/images/2024/07/01/0zFw2y.png",
            ["commandinfo"] = "https://i.ibb.co/JkZx93S/2ZPHP9q.png",
        };
        void OnServerInitialized()
        {
            foreach (var check in imageMenu) {
                ImageLibrary.Call("AddImage", check.Value, check.Value);
            }
            foreach (var check in config.settings)
                ImageLibrary.Call("AddImage", check.Url, check.Url);

            ImageLibrary.Call("AddImage", config.BannerURL, "banner");
            ImageLibrary.Call("AddImage", config.ShopQR, "shopqr");
            ImageLibrary.Call("AddImage", config.DSQR, "dsqr");
            ImageLibrary.Call("AddImage", config.VKQR, "vkqr");

            foreach (var check in BasePlayer.activePlayerList) 
                OnPlayerConnected(check);
        }

        void OnPlayerConnected(BasePlayer player) {
            if (!hidden.ContainsKey(player.userID))
                hidden[player.userID] = false;

            if (!activeButton.ContainsKey(player.userID))
                activeButton[player.userID] = "info";
        }
        #endregion

        #region Команды
        [ChatCommand("menu")]
        void ChatMenu(BasePlayer player) => MenuUI(player);

        [ChatCommand("info")]
        void ChatInfo(BasePlayer player) => MenuUI(player, "info");

        [ChatCommand("skill")]
        void ChatSkill(BasePlayer player) => MenuUI(player, "skill");

        [ChatCommand("block")]
        void ChatBlock(BasePlayer player) => MenuUI(player, "block");

        [ChatCommand("kits")]
        void ChatKit(BasePlayer player) => MenuUI(player, "kit");
                
        [ChatCommand("case")]
        void ChatCase(BasePlayer player) => MenuUI(player, "case");

        [ChatCommand("stat")]
        void ChatStat(BasePlayer player) => MenuUI(player, "stat");

        [ChatCommand("tpmenu")]
        void ChatTeleport(BasePlayer player) => MenuUI(player, "teleport");

        [ChatCommand("report")]
        void ChatReport(BasePlayer player) => MenuUI(player, "report");

        [ChatCommand("skin")]
        void ChatSkin(BasePlayer player) => MenuUI(player, "skin");
        
        [ChatCommand("lot")]
        void ChatLot(BasePlayer player) => MenuUI(player, "lot");

        [ChatCommand("tpbaraxolka")]
        void Chattpbaraxolka(BasePlayer player) => MenuUI(player, "tpbaraxolka");

        [ChatCommand("shop")]
        void Chattpshop(BasePlayer player) => MenuUI(player, "tpshop");

        [ChatCommand("mainingfarm")]    
        void ChatMainingFarm(BasePlayer player) => MenuUI(player, "mainingfarm");

        [ChatCommand("drop")]
        void ChatDrop(BasePlayer player) => MenuUI(player, "drop");

        [ChatCommand("wipe")]
        void ChatWipe(BasePlayer player) => MenuUI(player, "wipe");

        [ChatCommand("bot")]
        void ChatBot(BasePlayer player) => MenuUI(player, "bot");

        [ChatCommand("chat")]
        void ChatChat(BasePlayer player) => MenuUI(player, "chat");

        [ChatCommand("pass")]
        void ChatPass(BasePlayer player) => MenuUI(player, "pass");

        [ConsoleCommand("command")]
        void ConsoleCommand(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            UI(player, "command", args.Args[0]);
        }

        [ConsoleCommand("hidden_menu")]
        void Hidden(ConsoleSystem.Arg args) {
            var player = args.Player();
            var hide = hidden[player.userID] == true ? false : true;
            hidden[player.userID] = hide;
            ButtonUI(player);
            MenuUI(player);
        }

        [ConsoleCommand("menu")]
        void ConsoleMenu(ConsoleSystem.Arg args) {
            var player = args.Player();
            activeButton[player.userID] = args.Args[0];
            ButtonUI(player);
            UI(player, args.Args[0]);
        }
        #endregion

        #region Интерфейс
        void MenuUI(BasePlayer player, string name = "")
        {
            if (name != "")
                activeButton[player.userID] = name;
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0", Material = "assets/content/ui/uibackgroundblur.mat", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" }
            }, "Overlay", Layer);

            var anchormin = hidden[player.userID] == true ? "0.233" : "0.283";
            var anchormax = hidden[player.userID] == true ? "0.8" : "0.85";
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"{anchormin} 0.2", AnchorMax = $"{anchormax} 0.8", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.9" }
            }, Layer, ".Mains");

            CuiHelper.AddUi(player, container);
            ButtonUI(player);

            var command = name != "" ? name : activeButton[player.userID];
            UI(player, command);
        }

        void UI(BasePlayer player, string name, string command = "") {
            CuiHelper.DestroyUi(player, "lay" + ".Main");
            CuiHelper.DestroyUi(player, "lay" + ".Command");
            CuiHelper.DestroyUi(player, "Rules_UI" + ".Main");
            CuiHelper.DestroyUi(player, "MainStats" + ".Main");
            CuiHelper.DestroyUi(player, "ui.kits" + ".Main");
            CuiHelper.DestroyUi(player, "TPMENULAYER");
            CuiHelper.DestroyUi(player, "TPMENULAYER1");
            CuiHelper.DestroyUi(player, "TPMENULAYER2");
            CuiHelper.DestroyUi(player, ".SGUI");
            CuiHelper.DestroyUi(player, "UI_IQCHAT_CONTEXT");
            
            if (name == "info") {
                InfoUI(player);
            }
            if (name == "tpshop") {
                TPShop?.Call("DeleteUserInShow", player);
                TPShop?.Call("TPShopUI", player);
            }
            if (name == "rules") {
                TPRulesSystem?.Call("RulesUI", player);
            }
            if (name == "block") {
                TPWipeBlock?.Call("BlockUi", player);
            }
            if (name == "kit") {
                TPKits?.Call("InitilizeUI", player);
            }
            if (name == "case") {
                TPCases?.Call("OpenMenuCases", player);
            }
            if (name == "stat") {
                TPStatsSystem?.Call("PlayerTopInfo", player);
            }
            if (name == "teleport") {
                TPTeleportation?.Call("DDrawMenu", player);
            }
            if (name == "report") {
                TPReportSystem?.Call("ReportUI", player);
            }
            if (name == "tpbaraxolka") {
                TPBaraxolka?.Call("DrawNPCUI", player);
            }
            if (name == "skin") {
                TPSkinMenu?.Call("GUI", player);
            }
            if (name == "pass") {
                TPBattlePass?.Call("ShowUIMain", player, 0);
            }
            if (name == "store") {
                GameStoresRUST?.Call("InitializeStore", player, 0);
            }
            if (name == "lot") {
                TPLotterySystem?.Call("LotteryUI", player, 0);
            }
            if (name == "drop") {
                SkinDrop?.Call("SkinDropUI", player);
            }
            if (name =="wipe") {
                TPWipeSchedule?.Call("BuildUI", player);
            }
            if (name =="bot") {
                VKBot?.Call("StartVKBotMainGUI", player);
            }
            if (name =="chat") {
                TPChat?.Call("ChatCommandOpenedUI", player);
            }
            if (name =="skill") {
                TPSkillSystem?.Call("UI_DrawResearch", player);
            }
            if (name =="mainingfarm") {
                TPMiningFarm?.Call("chatMiningFarm", player);
            }
            if (name == "command") {
                InfoUI(player);
                CommandUI(player, command);
            }
        }
       private void UpdateUIBalance(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "PlayerBalance");

            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Name = "PlayerBalance",
                Parent = Layer + ".Main",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $"{(double)Economics?.Call("Balance", player.UserIDString)}",
                        FontSize = 11,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1",
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.90",
                        AnchorMax = "1 0.93",
                        //OffsetMin = offsetmin,
                        //OffsetMax = offsetmax
                    },

                }
            });

            CuiHelper.AddUi(player, container);
        }
        void ButtonUI(BasePlayer player) {
            CuiHelper.DestroyUi(player, Layer + ".Main");
            var container = new CuiElementContainer();

            var imagehidden = hidden[player.userID] == true ? imageMenu["backgroundhidden"] : imageMenu["backgroundshow"];
            var anchorhiddenmin = hidden[player.userID] == true ? "0.19" : "0.1466";
            var anchorhiddenmax = hidden[player.userID] == true ? "0.22" : "0.27";
              container.Add(new CuiElement
            {
                Name = Layer+".ImgAvater",
                Parent =  Layer,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        //Color = "1 1 1 1",
                        Png = (string) ImageLibrary.Call("GetImage", player.UserIDString),
                    },

                    new CuiRectTransformComponent
                    {
                       /* AnchorMin = "0 0",
                        AnchorMax = "0.036 0.069",*/
                        AnchorMin = "0.152 0.74",
                        AnchorMax = "0.189 0.809",
                    }
                }
            });
            container.Add(new CuiElement
            {
                Name = Layer + ".Main",
                Parent = Layer,
                Components =
                {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", imagehidden) },
                    new CuiRectTransformComponent { AnchorMin = $"{anchorhiddenmin} 0.19", AnchorMax = $"{anchorhiddenmax} 0.814", OffsetMax = "0 0" }
                }
            });
        
            container.Add(new CuiElement
            {
                Name = "PlayerName",
                Parent = Layer + ".Main",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $"{player.displayName}",
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1",
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.4 0.94",
                        AnchorMax = "1 0.97",
                        //OffsetMin = offsetmin,
                        //OffsetMax = offsetmax
                    },

                }
            });
            container.Add(new CuiElement
            {
                Name = "PlayerBalance",
                Parent = Layer + ".Main",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $"{(double)Economics?.Call("Balance", player.UserIDString)}",
                        FontSize = 11,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1",
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.90",
                        AnchorMax = "1 0.93",
                        //OffsetMin = offsetmin,
                        //OffsetMax = offsetmax
                    },

                }
            });
            /*var imagebgarrow = hidden[player.userID] == true ? imageMenu["backgroundbutton"] : imageMenu["textshow"];
            var anchorbgarrowmin = hidden[player.userID] == true ? "0.15" : "0.04";
            var anchorbgarrowmax = hidden[player.userID] == true ? "0.85" : "0.96";
            container.Add(new CuiElement
            {
                Name = "Hidden",
                Parent = Layer + ".Main",
                Components =
                {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", imagebgarrow) },
                    new CuiRectTransformComponent { AnchorMin = $"{anchorbgarrowmin} 0.015", AnchorMax = $"{anchorbgarrowmax} 0.075", OffsetMax = "0 0" }
                }
            });

            var imagearrow = hidden[player.userID] == true ? imageMenu["hiddenarrow"] : imageMenu["showarrow"];
            var anchorarrow = hidden[player.userID] == true ? "1" : "0.18";
            container.Add(new CuiElement
            {
                Parent = "Hidden",
                Components =
                {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", imagearrow)},
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = $"{anchorarrow} 1", OffsetMin = "9.2 9.2", OffsetMax = "-9.2 -9.2" }
                }
            });

            if (hidden[player.userID] == false) {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.19 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0" },
                    Text = { Text = "Свернуть", Color = "1 1 1 0.4", FontSize = 12, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf" }
                }, "Hidden");
            }

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Command = "hidden_menu", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, "Hidden");*/

            var anchorbutton = hidden[player.userID] == true ? 1f : 0.228f;
            float width = anchorbutton, height = 0.0660f, startxBox = 0.005f, startyBox = 0.19f - height, xmin = startxBox, ymin = startyBox;
            if(!hidden[player.userID])
            {
                startyBox = 0.86f - height;
                ymin = startyBox;
            }
            foreach (var check in config.settings)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = xmin + " " + ymin, AnchorMax = (xmin + width) + " " + (ymin + height * 1)},
                    Button = { Color = "1 1 1 0", Command = $"menu {check.Command}" },
                    Text = { Text = "" }
                }, Layer + ".Main", "Button");

                container.Add(new CuiElement
                {
                    Name = "ButtonImage",
                    Parent = "Button",
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", imageMenu["backgroundbutton"]) },
                        new CuiRectTransformComponent { AnchorMin = "0.15 0.1", AnchorMax = "0.85 0.9" }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = "ButtonImage",
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", check.Url) },
                        new CuiRectTransformComponent { AnchorMin = "0.3 0.3", AnchorMax = "0.7 0.7"}
                    }
                });

                var color = activeButton[player.userID] == check.Command ? "1 1 1 1" : "0 0 0 0";
                container.Add(new CuiElement
                {
                    Parent = "ButtonImage",
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", imageMenu["activeButton"]), Color = color },
                        new CuiRectTransformComponent { AnchorMin = "-0.15 0.2", AnchorMax = "-0.06 0.8"}
                    }
                });

                if (hidden[player.userID] == false) {
                    container.Add(new CuiElement
                    {
                        Name = "Button" + "Text",
                        Parent = "Button",
                        Components =
                        {
                            new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", imageMenu["backgroundtextbutton"]) },
                            new CuiRectTransformComponent { AnchorMin = "0.95 0.1", AnchorMax = "4.15 0.85", OffsetMax = "0 0", OffsetMin = "0 0" }
                        }
                    });

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.08 0", AnchorMax = "1 1" },
                        Button = { Color = "0 0 0 0", Command = $"menu {check.Command}" },
                        Text = { Text = check.DisplayName, Color = "1 1 1 0.4", FontSize = 12, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf" }
                    }, "Button" + "Text");
                }

                xmin += width;
                if (xmin + width >= 0)
                {
                    xmin = startxBox;
                    ymin -= height-0.005f;
                }
            }

            CuiHelper.AddUi(player, container);
        }

        void InfoUI(BasePlayer player) {
            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Name = "lay" + ".Main",
                Parent = ".Mains",
                Components = 
                {
                    new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", imageMenu["foninfo"]) },
                    new CuiRectTransformComponent { AnchorMin = "-0.315 -0.27", AnchorMax = "1.3 1.275", OffsetMax = "0 0" },
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.8 0.804", AnchorMax = "0.817 0.832" },
                Button = { Close = "Menu_UI", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, "lay" + ".Main");

            container.Add(new CuiElement
            {
                Parent = "lay" + ".Main",
                Components = 
                {
                    new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "banner") },
                    new CuiRectTransformComponent { AnchorMin = "0.244 0.615", AnchorMax = "0.76 0.79", OffsetMax = "0 0" },
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.406 0.554", AnchorMax = "0.5967 0.592", OffsetMax = "0 0" },
                Text = { Text = config.Title1, Color = "1 1 1 0.4", FontSize = 13, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }
            }, "lay" + ".Main");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.25 0.38", AnchorMax = "0.755 0.54", OffsetMax = "0 0" },
                Text = { Text = config.Text1, Color = "1 1 1 0.3", FontSize = 12, Align = TextAnchor.UpperCenter, Font = "robotocondensed-regular.ttf" }
            }, "lay" + ".Main");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.32 0.26", AnchorMax = "0.383 0.277", OffsetMax = "0 0" },
                Text = { Text = config.ShopTitle, Color = "1 1 1 0.4", FontSize = 8, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }
            }, "lay" + ".Main");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.31 0.19", AnchorMax = "0.4 0.245", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0" }
            }, "lay" + ".Main", "ShopText");

            container.Add(new CuiElement
            {
                Parent = "ShopText",
                Components =
                {
                    new CuiInputFieldComponent { Text = config.ShopText, Color = "1 1 1 0.3", Align = TextAnchor.UpperCenter, FontSize = 10, Font = "robotocondensed-bold.ttf"},
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiElement
            {
                Parent = "lay" + ".Main",
                Components = 
                {
                    new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "shopqr") },
                    new CuiRectTransformComponent { AnchorMin = "0.257 0.2", AnchorMax = "0.301 0.274", OffsetMax = "0 0" },
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.48 0.26", AnchorMax = "0.57 0.277", OffsetMax = "0 0" },
                Text = { Text = config.DSTitle, Color = "1 1 1 0.4", FontSize = 8, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }
            }, "lay" + ".Main");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.485 0.19", AnchorMax = "0.57 0.245", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0" }
            }, "lay" + ".Main", "DSText");

            container.Add(new CuiElement
            {
                Parent = "DSText",
                Components =
                {
                    new CuiInputFieldComponent { Text = config.DSText, Color = "1 1 1 0.3", Align = TextAnchor.UpperCenter, FontSize = 10, Font = "robotocondensed-bold.ttf"},
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiElement
            {
                Parent = "lay" + ".Main",
                Components = 
                {
                    new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "dsqr") },
                    new CuiRectTransformComponent { AnchorMin = "0.43 0.2", AnchorMax = "0.475 0.274", OffsetMax = "0 0" },
                }
            });

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.655 0.26", AnchorMax = "0.745 0.277", OffsetMax = "0 0" },
                Text = { Text = config.VKTitle, Color = "1 1 1 0.4", FontSize = 8, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }
            }, "lay" + ".Main");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.655 0.19", AnchorMax = "0.75 0.245", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0" }
            }, "lay" + ".Main", "VKText");

            container.Add(new CuiElement
            {
                Parent = "VKText",
                Components =
                {
                    new CuiInputFieldComponent { Text = config.VKText, Color = "1 1 1 0.3", Align = TextAnchor.UpperCenter, FontSize = 10, Font = "robotocondensed-bold.ttf"},
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiElement
            {
                Parent = "lay" + ".Main",
                Components = 
                {
                    new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "vkqr") },
                    new CuiRectTransformComponent { AnchorMin = "0.604 0.2", AnchorMax = "0.6485 0.274", OffsetMax = "0 0" },
                }
            });

            float width = 0.1f, height = 0.04f, startxBox = 0.344f, startyBox = 0.36f - height, xmin = startxBox, ymin = startyBox;
            foreach (var check in config.command)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = xmin + " " + ymin, AnchorMax = (xmin + width) + " " + (ymin + height * 1), OffsetMax = "0 0" },
                    Button = { Color = "1 1 1 0", Command = $"command {check.Command}" },
                    Text = { Text = "" }
                }, "lay" + ".Main", "Text");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = check.DisplayName.ToUpper(), Color = "1 1 1 0.7", FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }
                }, "Text");

                xmin += width + 0.009f;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height;
                }
            }

            CuiHelper.AddUi(player, container);
        }
      
        void CommandUI(BasePlayer player, string command)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Name = "lay" + ".Command",
                Parent = ".Mains",
                Components = 
                {
                    new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", imageMenu["commandinfo"]) },
                    new CuiRectTransformComponent { AnchorMin = "0.05 0.01", AnchorMax = "0.93 0.95", OffsetMax = "0 0" },
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.96 0.94", AnchorMax = "1 1" },
                Button = { Close = "lay" + ".Command", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, "lay" + ".Command");

            foreach (var check in config.command)
            {
                if (check.Command == command)
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.05 0", AnchorMax = "0.95 0.92", OffsetMax = "0 0" },
                        Text = { Text = check.Text, Color = "1 1 1 0.7", FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }
                    }, "lay" + ".Command");
                }
            }

            CuiHelper.AddUi(player, container);
        }
        #endregion 
    }
}