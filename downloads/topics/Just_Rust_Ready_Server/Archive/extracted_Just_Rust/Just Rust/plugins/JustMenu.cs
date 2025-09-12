using System;
using UnityEngine;
using Newtonsoft.Json;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using Oxide.Core;
using ru = Oxide.Game.Rust;

namespace Oxide.Plugins
{
    [Info("JustMenu", "fermens", "0.1.12")]
    class JustMenu : RustPlugin
    {
        #region КОНФИГ
        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            config = PluginConfig.DefaultConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        class setbutton
        {
            [JsonProperty("Название")]
            public string name;

            [JsonProperty("Команда")]
            public string command;

            [JsonProperty("Размер текста")]
            public string font;

            [JsonProperty("Цвет текста")]
            public string color;

            [JsonProperty("Закрывать меню после нажатия?")]
            public bool close;
        }

        class setbuttontp
        {
            [JsonProperty("Название")]
            public string name;

            [JsonProperty("Команда")]
            public string command;

            [JsonProperty("Размер текста")]
            public string font;

            [JsonProperty("Цвет текста")]
            public string color;

            [JsonProperty("Тип кнопки")]
            public BUTTON bUTTON;

        }

        class upremove
        {
            [JsonProperty("Консольная команда")]
            public string consolecommand;

            [JsonProperty("Настройка кнопок")]
            public setbutton[] menu;

            [JsonProperty("Закрывать меню после нажатия?")]
            public bool close;
        }

        class mainset
        {
            [JsonProperty("Консольная команда")]
            public string consolecommand;

            [JsonProperty("Настройка кнопок")]
            public setbuttontp[] menu;

            [JsonProperty("Закрывать меню после нажатия?")]
            public bool close;
        }

        class button
        {
            [JsonProperty("AnchorMin")]
            public string start;

            [JsonProperty("AnchorMax")]
            public string end;

            [JsonProperty("Картинка")]
            public string image;
        }

        enum BUTTON { sethome, homes, outpost, bandit, tppending, tpr, upgrade, ffon, ffoff, turretsoff, turretson, codelockson, codelocksoff, create, tradefriend, trade, gesture }

        class tpmenu
        {
            [JsonProperty("Консольная команда - Основная")]
            public string consolecommandtp;

            [JsonProperty("Консольная команда - Друзья")]
            public string consolecommandfriend;

            [JsonProperty("Настройка кнопок")]
            public setbuttontp[] menu;

            [JsonProperty("Закрывать меню после нажатия?")]
            public bool close;
        }

        class friend
        {
            [JsonProperty("Консольная команда")]
            public string consolecommand;

            [JsonProperty("Настройка кнопок")]
            public setbuttontp[] menu;
        }

        class logo
        {
            [JsonProperty("Включить?")]
            public bool enable;

            [JsonProperty("Начало координат")]
            public string start;

            [JsonProperty("OffsetMin")]
            public string min;

            [JsonProperty("OffsetMax")]
            public string max;

            [JsonProperty("Картинка")]
            public string image;

            [JsonProperty("Цвет и прозрачность")]
            public string color;

            [JsonProperty("Команда")]
            public string command;
        }

        private class PluginConfig
        {
            [JsonProperty("Кнопки")]
            public button[] listbuttons;

            [JsonProperty("Лого - кнопка")]
            public logo logo;

            [JsonProperty("Центральная кнопка")]
            public button button;

            [JsonProperty("Основное меню")]
            public setbutton[] menu;

            [JsonProperty("Апгрейд/ремув меню")]
            public upremove upremove;

            [JsonProperty("Трейд меню")]
            public mainset trade;

            [JsonProperty("Меню жестов")]
            public mainset gesture;

            [JsonProperty("Друзья меню")]
            public friend friend;

            [JsonProperty("Чат команда")]
            public string chatcommand;

            [JsonProperty("Консольная команда")]
            public string consolecommand;

            [JsonProperty("Меню телепортации")]
            public tpmenu tpmenu;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    listbuttons = new button[]
                    {
                        new button{ start = "0.5286459 0.3694446", end = "0.5807291 0.4620369", image = "https://i.imgur.com/XWm6FAW.png" },
                        new button{ start = "0.55625 0.4972222", end = "0.6083333 0.5898148", image = "https://i.imgur.com/ZV7ytpq.png" },
                        new button{ start = "0.5109376 0.611111", end = "0.5630208 0.7037036", image = "https://i.imgur.com/BaFfFlD.png" },
                        new button{ start = "0.4333334 0.6287036", end = "0.4854167 0.7212963", image = "https://i.imgur.com/gphhXuo.png" },
                        new button{ start = "0.3697916 0.5444443", end = "0.421875 0.6370369", image = "https://i.imgur.com/iI12jj9.png"},
                        new button{ start = "0.353125 0.4055555", end = "0.4052083 0.4981481", image = "https://i.imgur.com/8wGQ6HQ.png" },
                        new button{ start = "0.3864584 0.2768519", end = "0.4385417 0.3694444", image = "https://i.imgur.com/CNwKeTL.png" },
                        new button{ start = "0.4552084 0.2055556", end = "0.5072916 0.2981481", image = "https://i.imgur.com/S9sQgVc.png" },
                        new button{ start = "0.534896 0.2138889", end = "0.5869792 0.3064815", image = "https://i.imgur.com/eeA5McP.png" },
                        new button{ start = "0.6010416 0.2944445", end = "0.653125 0.387037", image = "https://i.imgur.com/uqOhJeg.png" },
                        new button{ start = "0.6348957 0.4231482", end = "0.6869791 0.5157408", image = "https://i.imgur.com/lVaRHxv.png" },
                        new button{ start = "0.6296875 0.5648148", end = "0.6817709 0.6574074", image = "https://i.imgur.com/IwJplHJ.png" },
                        new button{ start = "0.5875 0.6870371", end = "0.6395833 0.7796296", image = "https://i.imgur.com/9I1cniw.png" },
                        new button{ start = "0.5197917 0.7620371", end = "0.571875 0.8546297", image = "https://i.imgur.com/SJPKdDm.png" },
                        new button{ start = "0.4395832 0.7768519", end = "0.4916668 0.8694444", image = "https://i.imgur.com/qXwmqtf.png" },
                        new button{ start = "0.3635416 0.7296295", end = "0.415625 0.8222221", image = "https://i.imgur.com/zjFGw94.png" },
                        new button{ start = "0.3057292 0.6305556", end = "0.3578125 0.7231481", image = "https://i.imgur.com/P8llNjU.png" },
                        new button{ start = "0.2744792 0.4981483", end = "0.3265625 0.5907407", image = "https://i.imgur.com/aeCAh7f.png" },
                        new button{ start = "0.2755208 0.3555556", end = "0.3276042 0.4481482", image = "https://i.imgur.com/wAchd8S.png" },
                        new button{ start = "0.3067708 0.2240741", end = "0.3588542 0.3166666", image = "https://i.imgur.com/QvF9teW.png" }
                        ///https://i.imgur.com/1Hlgy2B.png
                        ///https://i.imgur.com/VlV1KcA.png
                    },
                    menu = new setbutton[]
                    {
                        new setbutton{ close = false, color = "1 1 1 1", font = "12", command = "chat.say /report", name = "ПОЖАЛОВАТЬСЯ" },
                        new setbutton{ close = true, color = "1 1 1 1", font = "12", command = "menu.tp", name = "Телепортация" },
                        new setbutton{ close = false, color = "1 1 1 1", font = "12", command = "menu.friend", name = "Друзья" },
                        new setbutton{ close = false, color = "1 1 1 1", font = "12", command = "menu.friendset", name = "Друзья настройки" },
                        new setbutton{ close = false, color = "1 1 1 1", font = "12", command = "chat.say /report", name = "TEST4" },
                        new setbutton{ close = false, color = "1 1 1 1", font = "12", command = "chat.say /report", name = "TEST5" },
                        new setbutton{ close = false, color = "1 1 1 1", font = "12", command = "chat.say /report", name = "TEST6" },
                        new setbutton{ close = false, color = "1 1 1 1", font = "12", command = "chat.say /report", name = "TEST7" },
                        new setbutton{ close = false, color = "1 1 1 1", font = "12", command = "chat.say /report", name = "TEST8" },
                        new setbutton{ close = false, color = "1 1 1 1", font = "12", command = "chat.say /report", name = "TEST9" },
                        new setbutton{ close = false, color = "1 1 1 1", font = "12", command = "chat.say /report", name = "TEST10" },
                        new setbutton{ close = false, color = "1 1 1 1", font = "12", command = "chat.say /report", name = "TEST11" },
                        new setbutton{ close = false, color = "1 1 1 1", font = "12", command = "chat.say /report", name = "TEST12" },
                        new setbutton{ close = false, color = "1 1 1 1", font = "12", command = "chat.say /report", name = "TEST13" },
                        new setbutton{ close = false, color = "1 1 1 1", font = "12", command = "chat.say /report", name = "TEST14" },
                        new setbutton{ close = false, color = "1 1 1 1", font = "12", command = "chat.say /report", name = "TEST15" },
                        new setbutton{ close = false, color = "1 1 1 1", font = "12", command = "chat.say /report", name = "TEST16" },
                        new setbutton{ close = false, color = "1 1 1 1", font = "12", command = "chat.say /report", name = "TEST17" },
                        new setbutton{ close = false, color = "1 1 1 1", font = "12", command = "chat.say /report", name = "TEST18" },
                        new setbutton{ close = true, color = "1 0 1 1", font = "14", command = "chat.say /kit", name = "Кусь" }
                    },
                    button = new button { image = "https://i.imgur.com/s7cAwTE.png", start = "0.4666667 0.4407405", end = "0.5333334 0.5592592" },
                    chatcommand = "menu",
                    consolecommand = "menu.open",
                    tpmenu = new tpmenu
                    {
                        consolecommandtp = "menu.tp",
                        consolecommandfriend = "menu.friend",
                        menu = new setbuttontp[]
                        {
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "sethome {0}", name = "СОХРАНИТЬ\nТОЧКУ ДОМА", bUTTON = BUTTON.sethome },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "tpa", name = "ПРИНЯТЬ\nТЕЛЕПОРТ", bUTTON = BUTTON.tppending },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "tpc", name = "ОКЛОНИТЬ\nТЕЛЕПОРТ", bUTTON = BUTTON.tppending },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "home {0}", name = "ТП ДОМОЙ\n{1}", bUTTON = BUTTON.homes },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "tpr {0}", name = "ТП К ДРУГУ\n{1}", bUTTON = BUTTON.tpr },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "chat.say /bandit", name = "ГОРОД БАНДИТОВ", bUTTON = BUTTON.bandit },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "chat.say /outpost", name = "МИРНЫЙ ГОРОД", bUTTON = BUTTON.outpost }
                        },
                        close = true
                    },
                    upremove = new upremove
                    {
                        consolecommand = "menu.remove",
                        menu = new setbutton[]
                        {
                            new setbutton { color = "1 1 1 1", font = "10", command = "upgrade.off", name = "Выключить улучшение" },
                            new setbutton { color = "1 1 1 1", font = "10", command = "upgrade.use 1", name = "Улучшение\n до дерева" },
                            new setbutton { color = "1 1 1 1", font = "10", command = "upgrade.use 2", name = "Улучшение\n до камня" },
                            new setbutton { color = "1 1 1 1", font = "10", command = "upgrade.use 3", name = "Улучшение\n до метала" },
                            new setbutton { color = "1 1 1 1", font = "10", command = "upgrade.use 4", name = "Улучшение\n до мвк" }
                        },
                        close = true
                    },
                    trade = new mainset
                    {
                        consolecommand = "menu.trade",
                        menu = new setbuttontp[]
                        {
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "trade yes", name = "Принять обмен", bUTTON = BUTTON.trade },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "trade no", name = "Отказаться\nот обмена", bUTTON = BUTTON.trade },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "trade {0}", name = "Обмен с\n{1}", bUTTON = BUTTON.tradefriend }
                        },
                        close = true
                    },
                    gesture = new mainset
                    {
                        consolecommand = "menu.gesture",
                        menu = new setbuttontp[]
                        {
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "gesture wave", name = "Пустить волну", bUTTON = BUTTON.gesture },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "gesture victory", name = "Победный жест", bUTTON = BUTTON.gesture },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "gesture shrug", name = "Пожимание плечами", bUTTON = BUTTON.gesture },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "gesture thumbsup", name = "Пальцы вверх", bUTTON = BUTTON.gesture },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "gesture chicken", name = "Изобразить курицу", bUTTON = BUTTON.gesture },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "gesture hurry", name = "Подгонять", bUTTON = BUTTON.gesture },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "gesture whoa", name = "Погоди", bUTTON = BUTTON.gesture },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "gesture hattip", name = "Знак уважения", bUTTON = BUTTON.gesture },
                        },
                        close = true
                    },
                    logo = new logo
                    {
                        enable = true,
                        start = "0 1",
                        color = "1 1 1 0.9",
                        command = "menu.open",
                        min = "10 -60",
                        max = "60 -10",
                        image = "https://i.imgur.com/s7cAwTE.png"
                    },
                    friend = new friend
                    {
                        consolecommand = "menu.friendset",
                        menu = new setbuttontp[]
                        {
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "friend turret", name = "Включить авторизацию в турелях", bUTTON = BUTTON.turretson },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "friend turret", name = "Выключить авторизацию в турелях", bUTTON = BUTTON.turretsoff },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "friend codelock", name = "Включить авторизацию в замках", bUTTON = BUTTON.codelockson },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "friend codelock", name = "Выключить авторизацию в замках", bUTTON = BUTTON.codelocksoff },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "friend ff", name = "Включить урон по друзьям", bUTTON = BUTTON.ffon },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "friend ff", name = "Выключить урон по друзьям", bUTTON = BUTTON.ffoff },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "relationshipmanager.trycreateteam", name = "Создать команду", bUTTON = BUTTON.create }
                        }
                    }
                };
            }
        }

        #endregion

        #region ЗАГРУЖАЕМ КАРТИНКИ
        [PluginReference] private Plugin ImageLibrary;
        public string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary.Call("GetImage", shortname, skin);
        public bool HasImage(string imageName, ulong imageId) => (bool)ImageLibrary.Call("HasImage", imageName, imageId);
        public bool Download(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);
        public void AddImage(string url, string shortname)
        {
            if (!HasImage(shortname, 0))
            {
                //  Debug.Log(shortname);
                Download(url, shortname);
                timer.Once(1f, () => AddImage(url, shortname));
            }
            else
            {
                downloaded++;
                // Debug.Log(downloaded.ToString() + "/" + needdownloads);
                if (downloaded >= needdownloads)
                {
                    string buttons = "";
                    for (int i = 0; i < config.menu.Length; i++)
                    {
                        if (i >= config.listbuttons.Length) break;
                        button button = config.listbuttons[i];
                        setbutton setbutton = config.menu[i];
                        buttons += Button.Replace("{max}", button.end).Replace("{com}", $"menu.click {i}").Replace("{text}", setbutton.name).Replace("{color}", setbutton.color).Replace("{font}", setbutton.font).Replace("{min}", button.start).Replace("{png}", GetImage(button.image));

                    }
                    LOGO = "[{\"name\":\"LOGOBUTTON\",\"parent\":\"Hud.Menu\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"{command}\",\"color\":\"1 1 1 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"{start}\",\"anchormax\":\"{start}\",\"offsetmin\":\"{min}\",\"offsetmax\":\"{max}\"}]},{\"name\":\"IMAGELOGO\",\"parent\":\"LOGOBUTTON\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"sprite\":\"assets/content/textures/generic/fulltransparent.tga\",\"color\":\"{color}\",\"png\":\"{png}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmax\":\"0 0\"}]}]".Replace("{start}", config.logo.start).Replace("{png}", GetImage(config.logo.image)).Replace("{max}", config.logo.max).Replace("{min}", config.logo.min).Replace("{command}", config.logo.command).Replace("{color}", config.logo.color);
                    ROFL = "[{\"name\":\"JustRust\",\"parent\":\"Overlay\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0 0 0 0.9430604\",\"fadeIn\":0.5},{\"type\":\"NeedsCursor\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmax\":\"0 0\"}]},{\"name\":\"EXITBUT\",\"parent\":\"JustRust\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"menu.close\",\"color\":\"1 1 1 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"main\",\"parent\":\"JustRust\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"menu.close\",\"color\":\"1 1 1 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"{min}\",\"anchormax\":\"{max}\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"image\",\"parent\":\"main\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"sprite\":\"assets/content/textures/generic/fulltransparent.tga\",\"png\":\"{main}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmax\":\"0 0\"}]}{buttons}]".Replace("{max}", config.button.end).Replace("{min}", config.button.start).Replace("{main}", GetImage(config.button.image)).Replace("{buttons}", buttons);
                    if (!string.IsNullOrEmpty(config.chatcommand)) Interface.Oxide.GetLibrary<ru.Libraries.Command>(null).AddChatCommand(config.chatcommand, this, "cmdmenu");
                    if (!string.IsNullOrEmpty(config.consolecommand)) Interface.Oxide.GetLibrary<ru.Libraries.Command>(null).AddConsoleCommand(config.consolecommand, this, "cmdConsolemenu");
                    if (!string.IsNullOrEmpty(config.tpmenu.consolecommandtp)) Interface.Oxide.GetLibrary<ru.Libraries.Command>(null).AddConsoleCommand(config.tpmenu.consolecommandtp, this, "MENUTP");
                    if (!string.IsNullOrEmpty(config.tpmenu.consolecommandfriend)) Interface.Oxide.GetLibrary<ru.Libraries.Command>(null).AddConsoleCommand(config.tpmenu.consolecommandfriend, this, "MENUTPFRIEND");
                    if (!string.IsNullOrEmpty(config.upremove.consolecommand)) Interface.Oxide.GetLibrary<ru.Libraries.Command>(null).AddConsoleCommand(config.upremove.consolecommand, this, "MENUOPENUP");
                    if (!string.IsNullOrEmpty(config.friend.consolecommand)) Interface.Oxide.GetLibrary<ru.Libraries.Command>(null).AddConsoleCommand(config.friend.consolecommand, this, "MENUFRIEND");
                    if (!string.IsNullOrEmpty(config.trade.consolecommand)) Interface.Oxide.GetLibrary<ru.Libraries.Command>(null).AddConsoleCommand(config.trade.consolecommand, this, "MENUTRADE");
                    if (!string.IsNullOrEmpty(config.gesture.consolecommand)) Interface.Oxide.GetLibrary<ru.Libraries.Command>(null).AddConsoleCommand(config.gesture.consolecommand, this, "MENUGESTURE");

                    if (config.logo.enable)
                    {
                        Subscribe("OnPlayerConnected");
                        CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = Network.Net.sv.connections }, null, "DestroyUI", "LOGOBUTTON");
                        CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = Network.Net.sv.connections }, null, "AddUI", LOGO);
                    }
                    Debug.Log("Меню готово к работе");
                }
            }
        }
        #endregion

        #region КЭШ
        [PluginReference] Plugin NTeleportation, RemoveUpgradeV2, Trade, Friendsbyfermens;
        static string LOGO = "";
        const string Button = ",{\"name\":\"elem\",\"parent\":\"JustRust\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"{com}\",\"color\":\"1 1 1 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"{min}\",\"anchormax\":\"{max}\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"image\",\"parent\":\"elem\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"sprite\":\"assets/content/textures/generic/fulltransparent.tga\",\"png\":\"{png}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmax\":\"0 0\"}]},{\"name\":\"text\",\"parent\":\"elem\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text}\",\"fontSize\":{font},\"align\":\"MiddleCenter\",\"color\":\"{color}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"5 0\",\"offsetmax\":\"-5 0\"}]}";
        const string TPMENU = "[{\"name\":\"JustRust\",\"parent\":\"Overlay\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0 0 0 0.9430604\"},{\"type\":\"NeedsCursor\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmax\":\"0 0\"}]},{\"name\":\"EXITBUT\",\"parent\":\"JustRust\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"menu.close\",\"color\":\"1 1 1 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"main\",\"parent\":\"JustRust\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"command\":\"menu.open\",\"color\":\"1 1 1 0\"},{\"type\":\"RectTransform\",\"anchormin\":\"{min}\",\"anchormax\":\"{max}\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"image\",\"parent\":\"main\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"sprite\":\"assets/content/textures/generic/fulltransparent.tga\",\"png\":\"{main}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmax\":\"0 0\"}]}{buttons}]";
        static string ROFL = "";
        static int downloaded;
        static int needdownloads;

        Dictionary<BasePlayer, menuopen> cooldown = new Dictionary<BasePlayer, menuopen>();

        enum LASTPAGE { main, tp, up, trade, friend, gesture };
        class menuopen
        {
            public DateTime dateTime;
            public bool open;
            public LASTPAGE lastpage;
        }
        #endregion

        #region ОКСИД ХУКИ
        private void Init()
        {
            Unsubscribe("OnPlayerConnected");
        }

        private void OnServerInitialized()
        {
            Debug.Log(DateTime.Now.Hour.ToString());

            if (ImageLibrary == null)
            {
                Debug.LogWarning("УСТАНОВИТЕ ImageLibrary!");
                return;
            }


            //PatcH to 0.0.8
            if (config.gesture == null)
            {
                config.gesture = new mainset
                {
                    consolecommand = "menu.gesture",
                    menu = new setbuttontp[]
                        {
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "gesture wave", name = "Пустить волну", bUTTON = BUTTON.gesture },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "gesture victory", name = "Победный жест", bUTTON = BUTTON.gesture },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "gesture shrug", name = "Пожимание плечами", bUTTON = BUTTON.gesture },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "gesture thumbsup", name = "Пальцы вверх", bUTTON = BUTTON.gesture },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "gesture chicken", name = "Изобразить курицу", bUTTON = BUTTON.gesture },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "gesture hurry", name = "Подгонять", bUTTON = BUTTON.gesture },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "gesture whoa", name = "Погоди", bUTTON = BUTTON.gesture },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "gesture hattip", name = "Знак уважения", bUTTON = BUTTON.gesture },
                        },
                    close = true
                };
                config.trade = new mainset
                {
                    consolecommand = "menu.trade",
                    menu = new setbuttontp[]
                            {
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "trade yes", name = "Принять обмен", bUTTON = BUTTON.trade },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "trade no", name = "Отказаться\nот обмена", bUTTON = BUTTON.trade },
                            new setbuttontp { color = "1 1 1 1", font = "10", command = "trade {0}", name = "Обмен с\n{1}", bUTTON = BUTTON.tradefriend }
                            },
                    close = true
                };
                SaveConfig();
            }

            CreateSpawnGrid();

            Debug.Log("Меню - проверяем наличие картинок...");
            downloaded = 0;
            needdownloads = 4 + config.listbuttons.Count();
            AddImage(config.button.image, config.button.image);
            AddImage(config.logo.image, config.logo.image);
            AddImage("http://i.imgur.com/sZepiWv.png", "NONE");
            AddImage("http://i.imgur.com/lydxb0u.png", "LOADING");
            for (int i = 0; i < config.listbuttons.Length; i++)
            {
                AddImage(config.listbuttons[i].image, config.listbuttons[i].image);
            }
        }

        private void Unload()
        {
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connections = Network.Net.sv.connections }, null, "DestroyUI", "JustRust");
            cooldown.Clear();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (!player.IsConnected) return;

            if (player.IsReceivingSnapshot)
            {
                timer.Once(2, () => OnPlayerConnected(player));
                return;
            }

            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "LOGOBUTTON");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", LOGO);
        }
        #endregion

        #region КОМАНДЫ
        private void cmdmenu(BasePlayer player, string command, string[] args)
        {
            MENUOPEN(player);
        }

        private void cmdConsolemenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            MENUOPEN(player);
        }

        private void MENUCLOSE(BasePlayer player)
        {
            menuopen menuopen;
            if (cooldown.TryGetValue(player, out menuopen) && menuopen.open) menuopen.open = false;
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "JustRust");
        }

        [ConsoleCommand("menu.close")]
        void cmdConsolemenuclose(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            MENUCLOSE(player);
        }

        bool CHECKPOINT(BasePlayer player, LASTPAGE lASTPAGE)
        {
            menuopen menuopen;
            if (cooldown.TryGetValue(player, out menuopen))
            {
                if (menuopen.dateTime > DateTime.Now && menuopen.lastpage == lASTPAGE && menuopen.lastpage != LASTPAGE.friend)
                {
                    player.ChatMessage("Не так часто!");
                    return false;
                }

                if (menuopen.open && menuopen.lastpage == lASTPAGE && menuopen.lastpage != LASTPAGE.friend)
                {
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "JustRust");
                    menuopen.open = false;
                    return false;
                }

                menuopen.lastpage = lASTPAGE;
                menuopen.open = true;
                menuopen.dateTime = DateTime.Now.AddSeconds(0.3f);
            }
            else cooldown.Add(player, new menuopen { open = true, dateTime = DateTime.Now.AddSeconds(0.3f), lastpage = lASTPAGE });
            return true;
        }

        private void MENUOPEN(BasePlayer player)
        {
            if (!CHECKPOINT(player, LASTPAGE.main)) return;

            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "JustRust");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", ROFL);
        }

        private void MENUGESTURE(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (!CHECKPOINT(player, LASTPAGE.gesture)) return;

            string buttons = "";
            int i = 0;
            foreach (var gesture in config.gesture.menu)
            {
                button button = config.listbuttons[i];
                ADDBUTTON(ref buttons, button.end, button.start, $"menugesture.click {gesture.command}", gesture.name, gesture.color, gesture.font, GetImage(button.image));
                i++;
            }

            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "JustRust");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", TPMENU.Replace("{max}", config.button.end).Replace("{min}", config.button.start).Replace("{main}", GetImage(config.button.image)).Replace("{buttons}", buttons));
        }

        private void MENUFRIEND(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (!CHECKPOINT(player, LASTPAGE.friend)) return;

            bool IsCodelock = Friendsbyfermens.Call<bool>("IsCodelock", player.userID);
            bool IsTurret = Friendsbyfermens.Call<bool>("IsTurret", player.userID);
            bool IsPvp = Friendsbyfermens.Call<bool>("IsPvp", player.userID);
            string buttons = "";
            int i = 0;
            foreach (var friend in config.friend.menu)
            {
                if (friend.bUTTON == BUTTON.create && player.Team == null || friend.bUTTON == BUTTON.codelockson && !IsCodelock || friend.bUTTON == BUTTON.codelocksoff && IsCodelock || friend.bUTTON == BUTTON.ffon && IsPvp || friend.bUTTON == BUTTON.ffoff && !IsPvp || friend.bUTTON == BUTTON.turretsoff && IsTurret || friend.bUTTON == BUTTON.turretson && !IsTurret)
                {
                    button button = config.listbuttons[i];
                    ADDBUTTON(ref buttons, button.end, button.start, $"menufriend.click {friend.command}", friend.name, friend.color, friend.font, GetImage(button.image));
                    i++;
                }
            }

            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "JustRust");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", TPMENU.Replace("{max}", config.button.end).Replace("{min}", config.button.start).Replace("{main}", GetImage(config.button.image)).Replace("{buttons}", buttons));
        }

        private void MENUTRADE(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (!CHECKPOINT(player, LASTPAGE.trade)) return;

            bool IsTrade = Trade.Call<bool>("PlayerGetActiveTrade", player);
            int i = 0;
            string buttons = "";
            foreach (var trade in config.trade.menu)
            {
                if (trade.bUTTON == BUTTON.trade && IsTrade)
                {
                    button button = config.listbuttons[i];
                    ADDBUTTON(ref buttons, button.end, button.start, $"menutrade.click {trade.command}", trade.name, trade.color, trade.font, GetImage(button.image));
                    i++;
                }
                else if (trade.bUTTON == BUTTON.tradefriend && player.Team != null && player.Team.members.Count > 1)
                {
                    foreach (var z in player.Team.members)
                    {
                        if (i >= config.listbuttons.Length) break;
                        if (z == player.userID) continue;
                        BasePlayer target = BasePlayer.FindByID(z);
                        if (target == null || !target.IsConnected) continue;
                        button button = config.listbuttons[i];
                        ADDBUTTON(ref buttons, button.end, button.start, $"menutrade.click {trade.command.Replace("{0}", target.UserIDString)}", trade.name.Replace("{1}", target.displayName), trade.color, trade.font, GetImage(button.image));
                        i++;
                    }
                }
            }
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "JustRust");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", TPMENU.Replace("{max}", config.button.end).Replace("{min}", config.button.start).Replace("{main}", GetImage(config.button.image)).Replace("{buttons}", buttons));
        }

        private void MENUOPENUP(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (!CHECKPOINT(player, LASTPAGE.up)) return;

            bool IsUpgrade = RemoveUpgradeV2.Call<bool>("IsUpgrade", player);

            string buttons = "";
            int i = 0;
            string grid = GetNameGrid(player.transform.position);
            foreach (var up in config.upremove.menu)
            {
                if (up.command == "upgrade.off" && !IsUpgrade) continue;
                if (i >= config.listbuttons.Length) break;
                button button = config.listbuttons[i];
                ADDBUTTON(ref buttons, button.end, button.start, $"menuup.click {up.command.Replace("{0}", grid)}", up.name, up.color, up.font, GetImage(button.image));
                i++;
            }

            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "JustRust");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", TPMENU.Replace("{max}", config.button.end).Replace("{min}", config.button.start).Replace("{main}", GetImage(config.button.image)).Replace("{buttons}", buttons));
        }

        private void MENUTP(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (!CHECKPOINT(player, LASTPAGE.tp)) return;

            bool API_HavePendingRequest = NTeleportation.Call<bool>("API_HavePendingRequest", player);
            bool API_HaveAvailableHomes = NTeleportation.Call<bool>("API_HaveAvailableHomes", player);
            List<string> API_GetHomes = NTeleportation.Call<List<string>>("API_GetHomes", player);

            string buttons = "";
            int i = 0;
            string grid = GetNameGrid(player.transform.position);
            foreach (var tp in config.tpmenu.menu)
            {
                if (tp.bUTTON == BUTTON.sethome && API_HaveAvailableHomes || tp.bUTTON == BUTTON.tppending && API_HavePendingRequest && tp.command != "tpa" || tp.bUTTON == BUTTON.outpost || tp.bUTTON == BUTTON.bandit)
                {
                    if (i >= config.listbuttons.Length) break;
                    button button = config.listbuttons[i];
                    ADDBUTTON(ref buttons, button.end, button.start, $"menutp.click {tp.command.Replace("{0}", grid)}", tp.name, tp.color, tp.font, GetImage(button.image));
                    i++;
                }
                else if (tp.bUTTON == BUTTON.homes && API_GetHomes.Count > 0)
                {
                    foreach (var z in API_GetHomes)
                    {
                        if (i >= config.listbuttons.Length) break;
                        button button = config.listbuttons[i];
                        ADDBUTTON(ref buttons, button.end, button.start, $"menutp.click {tp.command.Replace("{0}", z)}", tp.name.Replace("{1}", z), tp.color, tp.font, GetImage(button.image));
                        i++;
                    }
                }
            }

            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "JustRust");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", TPMENU.Replace("{max}", config.button.end).Replace("{min}", config.button.start).Replace("{main}", GetImage(config.button.image)).Replace("{buttons}", buttons));
        }

        void ADDBUTTON(ref string buttons, string end, string start, string command, string name, string color, string font, string image)
        {
            buttons += Button.Replace("{max}", end).Replace("{com}", command).Replace("{text}", name).Replace("{color}", color).Replace("{font}", font).Replace("{min}", start).Replace("{png}", image);
        }

        [ConsoleCommand("menu.click")]
        void cmdConsolemenuclick(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs()) return;
            var player = arg.Player();
            if (player == null) return;
            int number;
            if (!int.TryParse(arg.Args[0], out number) || number >= config.menu.Length) return;
            player.Command(config.menu[number].command);
            if (config.menu[number].close) MENUCLOSE(player);
        }

        [ConsoleCommand("menutp.click")]
        void cmdConsoletpmenuclick(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs()) return;
            var player = arg.Player();
            if (player == null) return;
            player.Command(string.Join(" ", arg.Args.ToArray()));
            if (config.tpmenu.close) MENUCLOSE(player);
        }

        [ConsoleCommand("menuup.click")]
        void cmdConsolemenuupclick(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs()) return;
            var player = arg.Player();
            if (player == null) return;
            player.Command(string.Join(" ", arg.Args.ToArray()));
            if (config.upremove.close) MENUCLOSE(player);
        }

        [ConsoleCommand("menutrade.click")]
        void cmdConsolemetradepclick(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs()) return;
            var player = arg.Player();
            if (player == null) return;
            player.Command(string.Join(" ", arg.Args.ToArray()));
            if (config.trade.close) MENUCLOSE(player);
        }

        [ConsoleCommand("menugesture.click")]
        void cmdConsolemenugesturepclick(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs()) return;
            var player = arg.Player();
            if (player == null) return;
            player.Command(string.Join(" ", arg.Args.ToArray()));
            if (config.gesture.close) MENUCLOSE(player);
        }

        [ConsoleCommand("menufriend.click")]
        void cmdConsolemefriendk(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs()) return;
            var player = arg.Player();
            if (player == null) return;
            player.Command(string.Join(" ", arg.Args.ToArray()));
            player.Command(config.friend.consolecommand);
        }

        private void MENUTPFRIEND(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (!CHECKPOINT(player, LASTPAGE.tp)) return;

            bool API_HavePendingRequest = NTeleportation.Call<bool>("API_HavePendingRequest", player);

            string buttons = "";
            int i = 0;
            string grid = GetNameGrid(player.transform.position);
            foreach (var tp in config.tpmenu.menu)
            {
                if (tp.bUTTON == BUTTON.tppending && API_HavePendingRequest)
                {
                    if (i >= config.listbuttons.Length) break;
                    button button = config.listbuttons[i];
                    ADDBUTTON(ref buttons, button.end, button.start, $"menutp.click {tp.command.Replace("{0}", grid)}", tp.name, tp.color, tp.font, GetImage(button.image));
                    i++;
                }
                else if (tp.bUTTON == BUTTON.tpr && player.Team != null && player.Team.members.Count > 1)
                {
                    foreach (var z in player.Team.members)
                    {
                        if (i >= config.listbuttons.Length) break;
                        if (z == player.userID) continue;
                        BasePlayer target = BasePlayer.FindByID(z);
                        if (target == null || !target.IsConnected) continue;
                        button button = config.listbuttons[i];
                        ADDBUTTON(ref buttons, button.end, button.start, $"menutp.click {tp.command.Replace("{0}", target.UserIDString)}", tp.name.Replace("{1}", target.displayName), tp.color, tp.font, GetImage(button.image));
                        i++;
                    }
                }
            }

            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "DestroyUI", "JustRust");
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", TPMENU.Replace("{max}", config.button.end).Replace("{min}", config.button.start).Replace("{main}", GetImage(config.button.image)).Replace("{buttons}", buttons));
        }
        #endregion

        #region GRID
        Dictionary<string, Vector3> Grids = new Dictionary<string, Vector3>();
        const float calgon = 0.0066666666666667f;
        void CreateSpawnGrid()
        {
            var worldSize = (ConVar.Server.worldsize);
            float offset = worldSize / 2;
            var gridWidth = (calgon * worldSize);
            float step = worldSize / gridWidth;

            string start = "";

            char letter = 'A';
            int number = 0;

            for (float zz = offset; zz > -offset; zz -= step)
            {
                for (float xx = -offset; xx < offset; xx += step)
                {
                    Grids.Add($"{start}{letter}{number}", new Vector3(xx - 55f, 0, zz + 55f));
                    if (letter.ToString().ToUpper() == "Z")
                    {
                        start = "A";
                        letter = 'A';
                    }
                    else
                    {
                        letter = (char)(((int)letter) + 1);
                    }


                }
                number++;
                start = "";
                letter = 'A';
            }
        }

        private string GetNameGrid(Vector3 pos)
        {
            return Grids.Where(x => x.Value.x < pos.x && x.Value.x + 150f > pos.x && x.Value.z > pos.z && x.Value.z - 150f < pos.z).FirstOrDefault().Key;
        }
        #endregion
    }
}