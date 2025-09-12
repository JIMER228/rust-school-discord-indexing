using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using System.Linq;
namespace Oxide.Plugins
{
    [Info("InfoPanel", "Mevent", "0.1.1")]
    public class InfoPanel : RustPlugin
    {
        #region Init
        [PluginReference] private readonly Plugin ImageLibrary;
        public static string Layer = "UI_InfoPanel";
        private List<BasePlayer> MenuUsers;

        bool IsHelI()
        {
            foreach (var check in BaseNetworkable.serverEntities)
                if (check is BaseHelicopter)
                    return true;
            return false;
        }
        bool IsAir()
        {
            foreach (var check in BaseNetworkable.serverEntities)
                if (check is CargoPlane)
                    return true;
            return false;
        }
        bool IsCargoShip()
        {
            foreach (var check in BaseNetworkable.serverEntities)
                if (check is CargoShip)
                    return true;
            return false;
        }
        bool IsBradley()
        {
            foreach (var check in BaseNetworkable.serverEntities)
                if (check is BradleyAPC)
                    return true;
            return false;
        }
        bool IsCH47()
        {
            foreach (var check in BaseNetworkable.serverEntities)
                if (check is CH47Helicopter)
                    return true;
            return false;
        }
        #endregion

        #region Config
        private static ConfigData config;

        private class ConfigData
        {
            [JsonProperty("Название сервера")]
            public string ServerName;

            [JsonProperty("Тип отображения панели (Overlay или Hud)")]
            public string LayerType;

            [JsonProperty("Цвет для панелей (фон)")]
            public string LabelColor;

            [JsonProperty("Цвет для панелей (закрытие)")]
            public string CloseColor;

            [JsonProperty("Иконка игроков")]
            public string UsersIcon;

            [JsonProperty("Иконка времени")]
            public string TimeIcon;

            //[JsonProperty("Иконка спящих")]
           // public string SleepersIcon;

            [JsonProperty("Настройка логотипа")]
            public LogoSettings logosettings;

            [JsonProperty("Настройка иконок эвентов")]
            public SettingsEvents eventsettings;

            [JsonProperty("Кнопки меню")]
            public List<Button> BTN;
        }

        private class LogoSettings
        {
            [JsonProperty(PropertyName = "Ссылка на логотип сервера")]
            public string LogoIcon;

            [JsonProperty(PropertyName = "Команда, выполняемая при нажатии на логотип сервера [ПРИМЕР] чат команда: chat.say /store  ИЛИ  консольная команда: UI_GameStoresRUST")]
            public string LogoCmd;
        }

        private class SettingsEvents
        {
            [JsonProperty(PropertyName = "Танк")]
            public EventSetting EventBradley;

            [JsonProperty(PropertyName = "Вертолёт")]
            public EventSetting EventHelicopter;

            [JsonProperty(PropertyName = "Самолёт")]
            public EventSetting EventAirdrop;

            [JsonProperty(PropertyName = "Корабль")]
            public EventSetting EventCargoship;

            [JsonProperty(PropertyName = "Чинук CH47")]
            public EventSetting EventCH47;
        }

        private class EventSetting
        {
            [JsonProperty(PropertyName = "URL картинки")]
            public string URL;

            [JsonProperty(PropertyName = "Цвет активированного эвента")]
            public string OnColor;

            [JsonProperty(PropertyName = "Цвет дизактивированного эвента")]
            public string OffColor;

            [JsonProperty(PropertyName = "Offset Min")]
            public string OffMin;

            [JsonProperty(PropertyName = "Offset Max")]
            public string OffMax;
        }

        private class Button
        {
            [JsonProperty(PropertyName = "URL картинки")]
            public string URL;

            [JsonProperty(PropertyName = "Команда для кнопки")]
            public string CMD;

            [JsonProperty(PropertyName = "Заголовок кнопки")]
            public string Title;
        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                ServerName = "RUSTPLUGIN.RU",
                LayerType = "Overlay",
                LabelColor = "#A7A7A725",
                CloseColor = "#FF00003B",
                UsersIcon = "https://i.imgur.com/MUkpWFA.png",
                TimeIcon = "https://i.imgur.com/c5AW7sO.png",
                //SleepersIcon = "https://i.imgur.com/UvLItA7.png",
                logosettings = new LogoSettings
                {
                    LogoIcon = "https://i.imgur.com/UFmy9HT.png",
                    LogoCmd = "chat.say /store"
                },
                eventsettings = new SettingsEvents
                {
                    EventHelicopter = new EventSetting
                    {
                        URL = "https://i.imgur.com/Y0rVkt8.png",
                        OnColor = "#0CF204FF",
                        OffColor = "#FFFFFFFF",
                        OffMin = "43 -40",
                        OffMax = "59 -24",
                    },
                    EventAirdrop = new EventSetting
                    {
                        URL = "https://i.imgur.com/GcQKlg2.png",
                        OnColor = "#0CF204FF",
                        OffColor = "#FFFFFFFF",
                        OffMin = "62 -40",
                        OffMax = "78 -24",
                    },
                    EventCargoship = new EventSetting
                    {
                        URL = "https://i.imgur.com/3jigtJS.png",
                        OnColor = "#0CF204FF",
                        OffColor = "#FFFFFFFF",
                        OffMin = "81 -40",
                        OffMax = "97 -24",
                    },
                    EventBradley = new EventSetting
                    {
                        URL = "https://i.imgur.com/6Vtl3NG.png",
                        OnColor = "#0CF204FF",
                        OffColor = "#FFFFFFFF",
                        OffMin = "100 -40",
                        OffMax = "116 -24",
                    },
                    EventCH47 = new EventSetting
                    {
                        URL = "https://i.imgur.com/6U5ww9g.png",
                        OnColor = "#0CF204FF",
                        OffColor = "#FFFFFFFF",
                        OffMin = "119 -40",
                        OffMax = "135 -24",
                    },
                },
                BTN = new List<Button>
                {
                    new Button
                    {
                        URL = "https://i.imgur.com/WeHYCni.png",
                        CMD = "chat.say /store",
                        Title = "МАГАЗИН",
                    },
                    new Button
                    {
                        URL = "https://i.imgur.com/buPPBW9.png",
                        CMD = "chat.say /menu",
                        Title = "МЕНЮ",
                    },
                    new Button
                    {
                        URL = "https://i.imgur.com/oFhPHky.png",
                        CMD = "chat.say /map",
                        Title = "КАРТА",
                    },
                }
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();

                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintError("Configuration file is corrupt(or not exists), creating new one!");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        #endregion

        #region Hooks
        void OnServerInitialized()
        {
            if (!ImageLibrary)
            {
                PrintError("Please setup ImageLibrary plugin!");
                Interface.Oxide.UnloadPlugin(Title);
                return;
            }
            {
                MenuUsers = new List<BasePlayer>();

                for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
                    InitializeUI(BasePlayer.activePlayerList[i]);

                for (int i = 0; i < config.BTN.Count; i++)
                    ImageLibrary.Call("AddImage", config.BTN[i].URL, config.BTN[i].URL);

                //Логотип
                ImageLibrary.Call("AddImage", config.logosettings.LogoIcon, config.logosettings.LogoIcon);
                //Онлайн
                ImageLibrary.Call("AddImage", config.UsersIcon, config.UsersIcon);
                //Время
                ImageLibrary.Call("AddImage", config.TimeIcon, config.TimeIcon);
                //Слиперы
                //ImageLibrary.Call("AddImage", config.SleepersIcon, config.SleepersIcon);
                //ЭВЕНТЫ
                ImageLibrary.Call("AddImage", config.eventsettings.EventAirdrop.URL, config.eventsettings.EventAirdrop.URL);
                ImageLibrary.Call("AddImage", config.eventsettings.EventBradley.URL, config.eventsettings.EventBradley.URL);
                ImageLibrary.Call("AddImage", config.eventsettings.EventCargoship.URL, config.eventsettings.EventCargoship.URL);
                ImageLibrary.Call("AddImage", config.eventsettings.EventHelicopter.URL, config.eventsettings.EventHelicopter.URL);
                ImageLibrary.Call("AddImage", config.eventsettings.EventCH47.URL, config.eventsettings.EventCH47.URL);

                timer.Every(5f, () => BasePlayer.activePlayerList.ToList().ForEach(player => RefreshUI(player, "time")));
            }
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null || entity.net == null) return;
            try
            {
                if (entity is CargoPlane)
                    BasePlayer.activePlayerList.ToList().ForEach(player => RefreshUI(player, "air"));
                if (entity is BradleyAPC)
                    BasePlayer.activePlayerList.ToList().ForEach(player => RefreshUI(player, "bradley"));
                if (entity is BaseHelicopter)
                    BasePlayer.activePlayerList.ToList().ForEach(player => RefreshUI(player, "heli"));
                if (entity is CargoShip)
                    BasePlayer.activePlayerList.ToList().ForEach(player => RefreshUI(player, "cargo"));
                if (entity is CH47Helicopter)
                    BasePlayer.activePlayerList.ToList().ForEach(player => RefreshUI(player, "ch47"));
            }
            catch (NullReferenceException)
            {

            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot || player.IsSleeping())
            {
                NextTick(() =>
                {
                    OnPlayerConnected(player);
                });
                return;
            }

            InitializeUI(player);
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                RefreshUI(BasePlayer.activePlayerList[i], "online");
                //RefreshUI(BasePlayer.activePlayerList[i], "sleepers");
            }
        }
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            NextTick(() =>
            {
                for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
                {
                    RefreshUI(BasePlayer.activePlayerList[i], "online");
                   // RefreshUI(BasePlayer.activePlayerList[i], "sleepers");
                }
            });
        }
        private void Unload()
        {
            BasePlayer.activePlayerList.ToList().ForEach(p => CuiHelper.DestroyUi(p, Layer));
        }
        #endregion

        #region Commands
        [ChatCommand("menu")]
        private void CmdChatMenu(BasePlayer player)
        {
            if (MenuUsers.Contains(player))
            {
                CuiHelper.DestroyUi(player, Layer + ".Menu.Opened");
                MenuUsers.Remove(player);
            }
            else
            {
                ButtonsUI(player);
                MenuUsers.Add(player);
            }
            return;
        }
        #endregion

        #region Interface
        private void InitializeUI(BasePlayer player)
        {
            if (player == null) return;
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1" },
                Image = { Color = "0 0 0 0" }
            }, config.LayerType, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 -40", OffsetMax = "40 -5" },
                Button = { Color = HexToCuiColor(config.LabelColor), Command = config.logosettings.LogoCmd },
                Text = { Text = "" }
            }, Layer, Layer + ".Logo");
            container.Add(new CuiElement
            {
                Name = Layer + ".Logo.Icon",
                Parent = Layer + ".Logo",
                Components =
                                                {
                    new CuiRawImageComponent { FadeIn = 1.0f, Color = "1 1 1 1", Png = (string) ImageLibrary.Call("GetImage", $"{config.logosettings.LogoIcon}") },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "2 2", OffsetMax = "-2 -2" },
                                                }
            });

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "45 -20", OffsetMax = "160 -5" },
                Image = { Color = HexToCuiColor(config.LabelColor) }
            }, Layer, Layer + ".ServerName");
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { FadeIn = 1f, Color = "1 1 1 1", Text = $"<b>{config.ServerName}</b>", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 11 }
            }, Layer + ".ServerName");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 -60", OffsetMax = "40 -45" },
                Button = { Color = HexToCuiColor(config.LabelColor), Command = "chat.say /menu" },
                Text = { Color = "1 1 1 1", Text = "/MENU", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 10 }
            }, Layer, Layer + ".Menu");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = config.eventsettings.EventHelicopter.OffMin, OffsetMax = config.eventsettings.EventHelicopter.OffMax },
                Image = { Color = HexToCuiColor(config.LabelColor) }
            }, Layer, Layer + ".Helicopter");
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = config.eventsettings.EventAirdrop.OffMin, OffsetMax = config.eventsettings.EventAirdrop.OffMax },
                Image = { Color = HexToCuiColor(config.LabelColor) }
            }, Layer, Layer + ".Air");
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = config.eventsettings.EventCargoship.OffMin, OffsetMax = config.eventsettings.EventCargoship.OffMax },
                Image = { Color = HexToCuiColor(config.LabelColor) }
            }, Layer, Layer + ".Cargo");
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = config.eventsettings.EventBradley.OffMin, OffsetMax = config.eventsettings.EventBradley.OffMax },
                Image = { Color = HexToCuiColor(config.LabelColor) }
            }, Layer, Layer + ".Bradley");
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = config.eventsettings.EventCH47.OffMin, OffsetMax = config.eventsettings.EventCH47.OffMax },
                Image = { Color = HexToCuiColor(config.LabelColor) }
            }, Layer, Layer + ".CH47");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "45 -40", OffsetMax = "100 -25" },
                Image = { Color = HexToCuiColor(config.LabelColor) }
            }, Layer, Layer + ".Online.Label");
            container.Add(new CuiElement
            {
                Name = Layer + ".Online.Icon",
                Parent = Layer + ".Online.Label",
                Components =
                {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = (string) ImageLibrary.Call("GetImage", $"{config.UsersIcon}") },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = "1 1", OffsetMax = "15 -1" },
                }
            });

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "105 -40", OffsetMax = "160 -25" },
                Image = { Color = HexToCuiColor(config.LabelColor) }
            }, Layer, Layer + ".Time.Label");
            container.Add(new CuiElement
            {
                Name = Layer + ".Time.Icon",
                Parent = Layer + ".Time.Label",
                Components =
                {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = (string) ImageLibrary.Call("GetImage", $"{config.TimeIcon}") },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = "1 1", OffsetMax = "15 -1" },
                }
            });
            /*
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "186 -40", OffsetMax = "231 -24" },
                Image = { Color = HexToCuiColor(config.LabelColor) }
            }, Layer, Layer + ".Sleepers.Label");
            container.Add(new CuiElement
            {
                Name = Layer + ".Sleepers.Icon",
                Parent = Layer + ".Sleepers.Label",
                Components =
                {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = (string) ImageLibrary.Call("GetImage", $"{config.SleepersIcon}") },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = "1 1", OffsetMax = "13 -1" },
                }
            });*/
            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);

            RefreshUI(player, "all");
        }
        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null) return;
            try
            {
                var tag = entity is CargoPlane ? "air" : entity is BradleyAPC ? "bradley" : entity is BaseHelicopter ? "heli" : entity is CargoShip ? "cargo" : entity is CH47Helicopter ? "ch47" : "";
                NextTick(() => BasePlayer.activePlayerList.ToList().ForEach(p => RefreshUI(p, tag)));
            }
            catch (NullReferenceException)
            {

            }
        }
        private void ButtonsUI(BasePlayer player)
        {
            CuiElementContainer ButtonsContainer = new CuiElementContainer();
            double ySwitch = -65;

            ButtonsContainer.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0" }
            }, Layer, Layer + ".Menu.Opened");
            /*
            ButtonsContainer.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "43 -55", OffsetMax = "53 -43" },
                Image = { Color = HexToCuiColor(config.CloseColor) }
            }, Layer + ".Menu.Opened", Layer + ".Menu.Opened.Close");
            ButtonsContainer.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Color = "0 0 0 0", Command = "chat.say /menu" },
                Text = { Text = "X", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 9 }
            }, Layer + ".Menu.Opened.Close");*/

            for (int i = 0; i < config.BTN.Count; i++)
            {
                ButtonsContainer.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"5 {ySwitch - 20}", OffsetMax = $"140 {ySwitch}" },
                    Image = { Color = HexToCuiColor(config.LabelColor) }
                }, Layer + ".Menu.Opened", Layer + $".Menu.Opened.{config.BTN[i].URL}");
                ySwitch -= 23;

                ButtonsContainer.Add(new CuiElement
                {
                    Parent = Layer + $".Menu.Opened.{config.BTN[i].URL}",
                    Components =
                                                {
                                                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", $"{config.BTN[i].URL}") },
                                                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = "3 1", OffsetMax = "21 -1" },
                                                }
                });

                ButtonsContainer.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "22 0" },
                    Text = { Text = $"<b>{config.BTN[i].Title}</b>", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 12 }
                }, Layer + $".Menu.Opened.{config.BTN[i].URL}");

                ButtonsContainer.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Color = "0 0 0 0", Command = $"{config.BTN[i].CMD}" },
                    Text = { Text = "" }
                }, Layer + $".Menu.Opened.{config.BTN[i].URL}");
            }
            CuiHelper.DestroyUi(player, Layer + ".Menu.Opened");
            CuiHelper.AddUi(player, ButtonsContainer);
        }
        private void RefreshUI(BasePlayer player, string Type)
        {
            if (player.IsReceivingSnapshot || player.IsSleeping())
                return;

            CuiElementContainer RefreshContainer = new CuiElementContainer();
            switch (Type)
            {
                case "online":
                    CuiHelper.DestroyUi(player, Layer + ".Refresh.Online");
                    RefreshContainer.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "13 1", OffsetMax = "0 -1" },
                        Text = { Text = $"{BasePlayer.activePlayerList.Count}/{ConVar.Server.maxplayers}", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 12 }
                    }, Layer + ".Online.Label", Layer + ".Refresh.Online");
                    break;
                case "time":
                    CuiHelper.DestroyUi(player, Layer + ".Refresh.Time");
                    RefreshContainer.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "13 1", OffsetMax = "0 -1" },
                        Text = { Text = $"{TOD_Sky.Instance.Cycle.DateTime.ToString("HH:mm")}", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 12 }
                    }, Layer + ".Time.Label", Layer + ".Refresh.Time");
                    break;
                    /*
                case "sleepers":
                    CuiHelper.DestroyUi(player, Layer + ".Refresh.Sleepers");
                    RefreshContainer.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "13 1", OffsetMax = "0 -1" },
                        Text = { Text = $"{BasePlayer.sleepingPlayerList.Count}", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 11 }
                    }, Layer + ".Sleepers.Label", Layer + ".Refresh.Sleepers");
                    break;*/
                case "heli":
                    CuiHelper.DestroyUi(player, Layer + ".Events.Helicopter");

                    RefreshContainer.Add(new CuiElement
                    {
                        Name = Layer + ".Events.Helicopter",
                        Parent = Layer + ".Helicopter",
                        Components =
                        {
                            new CuiRawImageComponent { FadeIn = 1.0f, Color = IsHelI() ? HexToCuiColor(config.eventsettings.EventHelicopter.OnColor) : HexToCuiColor(config.eventsettings.EventHelicopter.OffColor), Png = (string) ImageLibrary.Call("GetImage", $"{config.eventsettings.EventHelicopter.URL}") },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "1 1", OffsetMax = "-1 -1" },
                        }
                    });
                    break;
                case "air":
                    CuiHelper.DestroyUi(player, Layer + ".Events.Air");

                    RefreshContainer.Add(new CuiElement
                    {
                        Name = Layer + ".Events.Air",
                        Parent = Layer + ".Air",
                        Components =
                                                {
                                                    new CuiRawImageComponent { FadeIn = 1.0f, Color = IsAir() ? HexToCuiColor(config.eventsettings.EventAirdrop.OnColor) : HexToCuiColor(config.eventsettings.EventAirdrop.OffColor), Png = (string) ImageLibrary.Call("GetImage", $"{config.eventsettings.EventAirdrop.URL}") },
                                                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "1 1", OffsetMax = "-1 -1" },
                                                }
                    });
                    break;
                case "cargo":
                    CuiHelper.DestroyUi(player, Layer + ".Events.Cargo");
                    RefreshContainer.Add(new CuiElement
                    {
                        Name = Layer + ".Events.Cargo",
                        Parent = Layer + ".Cargo",
                        Components =
                                                {
                                                    new CuiRawImageComponent { FadeIn = 1.0f, Color = IsCargoShip() ? HexToCuiColor(config.eventsettings.EventCargoship.OnColor) : HexToCuiColor(config.eventsettings.EventCargoship.OffColor), Png = (string) ImageLibrary.Call("GetImage", $"{config.eventsettings.EventCargoship.URL}") },
                                                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "1 1", OffsetMax = "-1 -1" },
                                                }
                    });
                    break;
                case "bradley":
                    CuiHelper.DestroyUi(player, Layer + ".Events.Bradley");
                    RefreshContainer.Add(new CuiElement
                    {
                        Name = Layer + ".Events.Bradley",
                        Parent = Layer + ".Bradley",
                        Components =
                                                {
                                                    new CuiRawImageComponent { FadeIn = 1.0f, Color = IsBradley() ? HexToCuiColor(config.eventsettings.EventBradley.OnColor) : HexToCuiColor(config.eventsettings.EventBradley.OffColor), Png = (string) ImageLibrary.Call("GetImage", $"{config.eventsettings.EventBradley.URL}") },
                                                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "1 1", OffsetMax = "-1 -1" },
                                                }
                    });
                    break;
                case "ch47":
                    CuiHelper.DestroyUi(player, Layer + ".Events.CH47");
                    RefreshContainer.Add(new CuiElement
                    {
                        Name = Layer + ".Events.CH47",
                        Parent = Layer + ".CH47",
                        Components =
                                                {
                                                    new CuiRawImageComponent { FadeIn = 1.0f, Color = IsCH47() ? HexToCuiColor(config.eventsettings.EventCH47.OnColor) : HexToCuiColor(config.eventsettings.EventCH47.OffColor), Png = (string) ImageLibrary.Call("GetImage", $"{config.eventsettings.EventCH47.URL}") },
                                                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "1 1", OffsetMax = "-1 -1" },
                                                }
                    });
                    break;

                case "all":
                    CuiHelper.DestroyUi(player, Layer + ".Refresh.Online");
                    CuiHelper.DestroyUi(player, Layer + ".Refresh.Time");
                    CuiHelper.DestroyUi(player, Layer + ".Events.Helicopter");
                    CuiHelper.DestroyUi(player, Layer + ".Events.Air");
                    CuiHelper.DestroyUi(player, Layer + ".Events.Cargo");
                    CuiHelper.DestroyUi(player, Layer + ".Events.Bradley");
                    CuiHelper.DestroyUi(player, Layer + ".Events.CH47");
                    //CuiHelper.DestroyUi(player, Layer + ".Refresh.Sleepers");
                    /*
                    RefreshContainer.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "13 1", OffsetMax = "0 -1" },
                        Text = { Text = $"{BasePlayer.sleepingPlayerList.Count}", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 11 }
                    }, Layer + ".Sleepers.Label", Layer + ".Refresh.Sleepers");
                    */
                    RefreshContainer.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "13 1", OffsetMax = "0 -1" },
                        Text = { Text = $"{BasePlayer.activePlayerList.Count}/{ConVar.Server.maxplayers}", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 12 }
                    }, Layer + ".Online.Label", Layer + ".Refresh.Online");

                    RefreshContainer.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "13 1", OffsetMax = "0 -1" },
                        Text = { Text = $"{TOD_Sky.Instance.Cycle.DateTime.ToString("HH:mm")}", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 12 }
                    }, Layer + ".Time.Label", Layer + ".Refresh.Time");

                    RefreshContainer.Add(new CuiElement
                    {
                        Name = Layer + ".Events.Helicopter",
                        Parent = Layer + ".Helicopter",
                        Components =
                        {
                            new CuiRawImageComponent { FadeIn = 1.0f, Color = IsHelI() ? HexToCuiColor(config.eventsettings.EventHelicopter.OnColor) : HexToCuiColor(config.eventsettings.EventHelicopter.OffColor), Png = (string) ImageLibrary.Call("GetImage", $"{config.eventsettings.EventHelicopter.URL}") },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "1 1", OffsetMax = "-1 -1" },
                        }
                    });
                    RefreshContainer.Add(new CuiElement
                    {
                        Name = Layer + ".Events.Air",
                        Parent = Layer + ".Air",
                        Components =
                                                {
                                                    new CuiRawImageComponent { FadeIn = 1.0f, Color = IsAir() ? HexToCuiColor(config.eventsettings.EventAirdrop.OnColor) : HexToCuiColor(config.eventsettings.EventAirdrop.OffColor), Png = (string) ImageLibrary.Call("GetImage", $"{config.eventsettings.EventAirdrop.URL}") },
                                                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "1 1", OffsetMax = "-1 -1" },
                                                }
                    });
                    RefreshContainer.Add(new CuiElement
                    {
                        Name = Layer + ".Events.Cargo",
                        Parent = Layer + ".Cargo",
                        Components =
                                                {
                                                    new CuiRawImageComponent { FadeIn = 1.0f, Color = IsCargoShip() ? HexToCuiColor(config.eventsettings.EventCargoship.OnColor) : HexToCuiColor(config.eventsettings.EventCargoship.OffColor), Png = (string) ImageLibrary.Call("GetImage", $"{config.eventsettings.EventCargoship.URL}") },
                                                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "1 1", OffsetMax = "-1 -1" },
                                                }
                    });
                    RefreshContainer.Add(new CuiElement
                    {
                        Name = Layer + ".Events.Bradley",
                        Parent = Layer + ".Bradley",
                        Components =
                                                {
                                                    new CuiRawImageComponent { FadeIn = 1.0f, Color = IsBradley() ? HexToCuiColor(config.eventsettings.EventBradley.OnColor) : HexToCuiColor(config.eventsettings.EventBradley.OffColor), Png = (string) ImageLibrary.Call("GetImage", $"{config.eventsettings.EventBradley.URL}") },
                                                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "1 1", OffsetMax = "-1 -1" },
                                                }
                    });
                    RefreshContainer.Add(new CuiElement
                    {
                        Name = Layer + ".Events.CH47",
                        Parent = Layer + ".CH47",
                        Components =
                                                {
                                                    new CuiRawImageComponent { FadeIn = 1.0f, Color = IsCH47() ? HexToCuiColor(config.eventsettings.EventCH47.OnColor) : HexToCuiColor(config.eventsettings.EventCH47.OffColor), Png = (string) ImageLibrary.Call("GetImage", $"{config.eventsettings.EventCH47.URL}") },
                                                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "1 1", OffsetMax = "-1 -1" },
                                                }
                    });

                    break;
            }
            CuiHelper.AddUi(player, RefreshContainer);
        }
        #endregion

        #region Helpers
        private static string HexToCuiColor(string hex)
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

            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }
        #endregion
    }
}