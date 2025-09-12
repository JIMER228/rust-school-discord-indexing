using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CombinedPanel", "SARO", "1.0.1")]
    public class CombinedPanel : RustPlugin
    {
        #region [References]
        [PluginReference]
        private  Plugin? ImageLibrary;
        #endregion [References]

        #region [Fields]
        private const string InfoLayer = "UI.Menu";
        private const string EventLayer = "MicroPanelBySARO";
        private  Dictionary<string, bool> Events = new()
        {
            ["heli"] = false,
            ["bradley"] = false,
            ["cargo"] = false,
        };
        private  List<ulong> closePanel = new();
        private  List<BasePlayer> activePlayers = new();
        private  float updateInterval = 5f;
        #endregion [Fields]

        #region [Configuration]
        private  Configuration _config = new();

        private sealed class Configuration
        {
            [JsonProperty("Title Text")]
            public string TitleText =
                "<color=#ff8c08>Frenetic</color> Rust  <color=#ff8c08>Max</color> 2";

            [JsonProperty("Title Position")]
            public string TitlePosition = "center";

            [JsonProperty("Logo Image")]
            public string Logo = "";

            [JsonProperty("Event Images")]
            public EventImages EventImages = new();

            [JsonProperty("Buttons")]
            public List<Button> Buttons = new();
        }

        private sealed class EventImages
        {
            [JsonProperty("Bradley Active")]
            public string BradleyActive = "https://i.imgur.com/cHISUrV.png";

            [JsonProperty("Bradley Inactive")]
            public string BradleyInactive = "https://i.imgur.com/FkFatRn.png";

            [JsonProperty("Cargo Active")]
            public string CargoActive = "https://i.imgur.com/7qOvdks.png";

            [JsonProperty("Cargo Inactive")]
            public string CargoInactive = "https://i.imgur.com/O9MzNaD.png";

            [JsonProperty("Heli Active")]
            public string HeliActive = "https://i.imgur.com/DZoqhPm.png";

            [JsonProperty("Heli Inactive")]
            public string HeliInactive = "https://i.imgur.com/kK4qgkf.png";
        }

        private sealed class Button
        {
            public required string Title { get; set; }
            public required string Command { get; set; }
        }

        private const string ColorOn = "#eb9b34ff";
        private const string ColorOff = "#808080ff";
        private const string EventOffsetMin = "15 -85";
        private const string EventOffsetMax = "225 -65";

        /// <summary>
        /// Переопределяем стандартные методы для работы с конфигом
        /// </summary>
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                {
                    throw new InvalidOperationException("Config is null");
                }

                SaveConfig();
            }
            catch
            {
                PrintError("Error loading config");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new Configuration(), true);
        }

        protected override void SaveConfig()
        {
            _config ??= CreateDefaultConfig();
            Config.WriteObject(_config, true);
        }

        private Configuration CreateDefaultConfig()
        {
            return new Configuration
            {
                TitleText = "<color=#ff8c08>Frenetic</color> Rust  <color=#ff8c08>Max</color> 2",
                TitlePosition = "center",
                Logo = "",
                Buttons = new List<Button>
                {
                    new() { Title = "МАГАЗИН", Command = "chat.say /shop" },
                    new() { Title = "МЕНЮ", Command = "chat.say /menu" },
                    new() { Title = "КИТЫ", Command = "chat.say /kit" },
                },
            };
        }

        private void OnServerInitialized(bool initial)
        {
            if (ImageLibrary == null)
            {
                PrintError("ImageLibrary not found! Plugin will not work correctly.");
                return;
            }

            _ = timer.Once(
                2f,
                () =>
                {
                    InitializeImages();
                    CheckEvents();

                    foreach (BasePlayer? player in BasePlayer.activePlayerList)
                    {
                        if (player?.IsConnected == true)
                        {
                            OnPlayerConnected(player);
                        }
                    }

                    // Запускаем таймер для регулярного обновления UI для игроков
                    _ = timer.Every(updateInterval, UpdateAllPlayers);
                }
            );

            cmd.AddChatCommand("hide1", this, nameof(CmdEventHide));
        }

        private void InitializeImages()
        {
            if (ImageLibrary == null)
            {
                return;
            }

            _ = AddImage(_config.Logo, _config.Logo, 0);
            _ = AddImage(_config.EventImages.BradleyActive, "bradley_active", 0);
            _ = AddImage(_config.EventImages.CargoActive, "cargo_active", 0);
            _ = AddImage(_config.EventImages.HeliActive, "heli_active", 0);
            _ = AddImage(_config.EventImages.BradleyInactive, "bradley_inactive", 0);
            _ = AddImage(_config.EventImages.CargoInactive, "cargo_inactive", 0);
            _ = AddImage(_config.EventImages.HeliInactive, "heli_inactive", 0);
        }

        private void CheckEvents()
        {
            Events["heli"] = false;
            Events["bradley"] = false;
            Events["cargo"] = false;

            foreach (BaseNetworkable? entity in BaseNetworkable.serverEntities)
            {
                if (entity is BradleyAPC)
                {
                    Events["bradley"] = true;
                }
                else if (entity is BaseHelicopter)
                {
                    Events["heli"] = true;
                }
                else if (entity is CargoShip)
                {
                    Events["cargo"] = true;
                }
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player?.IsConnected != true)
            {
                return;
            }

            // Добавляем игрока в список для последующих обновлений, если его там еще нет
            if (!activePlayers.Contains(player))
            {
                activePlayers.Add(player);
            }

            _ = timer.Once(
                1f,
                () =>
                {
                    try
                    {
                        if (player?.IsConnected != true)
                        {
                            return;
                        }

                        if (_config == null)
                        {
                            LoadConfig();
                        }

                        if (ImageLibrary == null)
                        {
                            return;
                        }

                        DrawInfoPanel(player);
                        DrawEventPanel(player);
                    }
                    catch (Exception ex)
                    {
                        PrintError(
                            $"Ошибка при отрисовке панелей для игрока {player?.displayName}: {ex}"
                        );
                    }
                }
            );
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null)
            {
                return;
            }

            // Удаляем игрока из списка активных игроков
            if (activePlayers.Contains(player))
            {
                _ = activePlayers.Remove(player);
            }

            if (closePanel.Contains(player.userID))
            {
                _ = closePanel.Remove(player.userID);
            }
        }

        private void EntityHandle(BaseNetworkable entity, bool spawn)
        {
            if (entity == null)
                return;

            if (entity is BaseHelicopter)
            {
                Events["heli"] = spawn;
                UpdateAllEvents("heli");
            }
            else if (entity is BradleyAPC)
            {
                Events["bradley"] = spawn;
                UpdateAllEvents("bradley");
            }
            else if (entity is CargoShip)
            {
                Events["cargo"] = spawn;
                UpdateAllEvents("cargo");
            }
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            EntityHandle(entity, true);
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            EntityHandle(entity, false);
        }
        #endregion [Configuration]

        #region [Commands]
        private void CmdEventHide(BasePlayer player, string command, string[] args)
        {
            if (player == null)
            {
                return;
            }

            if (closePanel.Contains(player.userID))
            {
                _ = closePanel.Remove(player.userID);
                DrawInfoPanel(player);
                DrawEventPanel(player);
            }
            else
            {
                closePanel.Add(player.userID);
                _ = CuiHelper.DestroyUi(player, InfoLayer);
                _ = CuiHelper.DestroyUi(player, EventLayer);
                DrawEventPanel(player);
            }
        }
        #endregion [Commands]

        #region [Drawing Methods]
        private void DrawInfoPanel(BasePlayer player)
        {
            if (
                player?.IsConnected != true
                || closePanel.Contains(player.userID)
                || _config == null
                || ImageLibrary == null
            )
            {
                return;
            }

            CuiElementContainer container = new()
            {
                {
                    new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = "10 -64",
                            OffsetMax = "250 -9.5",
                        },
                        Image = { Color = "0 0 0 0" },
                    },
                    "Overlay",
                    InfoLayer
                },
                {
                    new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 0",
                            OffsetMin = "5 25",
                            OffsetMax = "50 60",
                        },
                        Text =
                        {
                            Text = _config.TitleText,
                            Align = TextAnchor.MiddleLeft,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 18,
                        },
                    },
                    InfoLayer
                },
                {
                    new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 0",
                            OffsetMin = "5 10",
                            OffsetMax = "0 37.5",
                        },
                        Text =
                        {
                            Text =
                                $"<color=#ff8c08>ОНЛАЙН</color>: {BasePlayer.activePlayerList.Count}/{ConVar.Server.maxplayers} <color=#ff8c08>ОФЛАЙН</color>: {BasePlayer.allPlayerList.Count() - BasePlayer.activePlayerList.Count}/{ConVar.Server.maxplayers}",
                            Align = TextAnchor.MiddleLeft,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 11,
                        },
                    },
                    InfoLayer
                },
            };

            _ = CuiHelper.DestroyUi(player, InfoLayer);

            if (_config.Buttons?.Count > 0)
            {
                float xPos = 5f;
                const float width = 45f;
                const float margin = 5f;

                foreach (Button button in _config.Buttons)
                {
                    if (
                        button == null
                        || string.IsNullOrEmpty(button.Title)
                        || string.IsNullOrEmpty(button.Command)
                    )
                    {
                        continue;
                    }

                    _ = container.Add(
                        new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "0 0",
                                OffsetMin = $"{xPos} -2",
                                OffsetMax = $"{xPos + width} 17.5",
                            },
                            Text =
                            {
                                Text = button.Title,
                                Align = TextAnchor.MiddleCenter,
                                Font = "robotocondensed-bold.ttf",
                                FontSize = 10,
                            },
                            Button = { Command = button.Command, Color = "0 0 0 0" },
                        },
                        InfoLayer
                    );

                    xPos += width + margin;
                }
            }

            _ = CuiHelper.AddUi(player, container);
        }

        private void DrawEventPanel(BasePlayer player)
        {
            if (player?.IsConnected != true || _config == null || ImageLibrary == null)
            {
                return;
            }

            bool isHidden = closePanel.Contains(player.userID);
            CuiElementContainer container = new();

            if (!isHidden)
            {
                _ = container.Add(
                    new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = "0 0",
                            OffsetMax = "0 0",
                        },
                        Image = { Color = "0.882 0.882 0.882 0.8" },
                    },
                    "Overlay",
                    EventLayer
                );

                _ = container.Add(
                    new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = EventOffsetMin,
                            OffsetMax = EventOffsetMax,
                        },
                        Image = { Color = "0 0 0 0" },
                    },
                    EventLayer,
                    EventLayer + ".Events"
                );
            }

            string buttonText = isHidden ? "﹀" : "︿";
            (string AnchorMin, string AnchorMax, string OffsetMin, string OffsetMax) = isHidden
                ? (AnchorMin: "0 1", AnchorMax: "0 1", OffsetMin: "60 -20", OffsetMax: "120 -5")
                : (AnchorMin: "0 1", AnchorMax: "0 1", OffsetMin: "60 -95", OffsetMax: "120 -80");

            _ = container.Add(
                new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = AnchorMin,
                        AnchorMax = AnchorMax,
                        OffsetMin = OffsetMin,
                        OffsetMax = OffsetMax,
                    },
                    Button = { Color = "0 0 0 0", Command = "chat.say /hide1" },
                    Text =
                    {
                        Text = buttonText,
                        FontSize = 8,
                        Font = "robotocondensed-regular.ttf",
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 0.8",
                    },
                },
                "Overlay",
                "UI.ToggleButton"
            );

            if (!isHidden)
            {
                _ = container.Add(
                    new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = "60 -105",
                            OffsetMax = "120 -90",
                        },
                        Button = { Color = "0 0 0 0", Command = "chat.say /hide1" },
                        Text =
                        {
                            Text = "Скрыть",
                            FontSize = 8,
                            Font = "robotocondensed-regular.ttf",
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 0.8",
                        },
                    },
                    "Overlay",
                    "UI.ToggleButtonLabel"
                );
            }

            _ = CuiHelper.DestroyUi(player, EventLayer);
            _ = CuiHelper.DestroyUi(player, "UI.ToggleButton");
            _ = CuiHelper.DestroyUi(player, "UI.ToggleButtonLabel");
            _ = CuiHelper.AddUi(player, container);

            if (!isHidden)
            {
                UpdateEventStatus(player, "heli", Events["heli"], 0.15f);
                UpdateEventStatus(player, "bradley", Events["bradley"], 0.30f);
                UpdateEventStatus(player, "cargo", Events["cargo"], 0.45f);
            }
        }

        private void UpdateEventStatus(BasePlayer player, string type, bool isActive, float xPos)
        {
            if (player?.IsConnected != true || _config == null)
            {
                return;
            }

            CuiElementContainer container = new();
            (string activeUrl, string inactiveUrl) = GetEventUrls(type);
            if (string.IsNullOrEmpty(activeUrl) || string.IsNullOrEmpty(inactiveUrl))
            {
                return;
            }

            _ = container.Add(
                new CuiButton
                {
                    RectTransform = { AnchorMin = $"{xPos} 0", AnchorMax = $"{xPos + 0.10} 1" },
                    Button = { Color = "0 0 0 0" },
                    Text = { Text = "" },
                },
                EventLayer + ".Events",
                EventLayer + "." + type
            );

            container.Add(
                new CuiElement
                {
                    Parent = EventLayer + "." + type,
                    Name = EventLayer + "." + type + ".active",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Url = activeUrl,
                            FadeIn = 0.1f,
                            Color = isActive ? "1 1 1 1" : "1 1 1 0",
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.15 0.15",
                            AnchorMax = "0.85 0.85",
                        },
                    },
                }
            );

            container.Add(
                new CuiElement
                {
                    Parent = EventLayer + "." + type,
                    Name = EventLayer + "." + type + ".inactive",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Url = inactiveUrl,
                            FadeIn = 0.1f,
                            Color = isActive ? "0 0 0 0" : "1 1 1 1",
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.15 0.15",
                            AnchorMax = "0.85 0.85",
                        },
                    },
                }
            );

            _ = CuiHelper.DestroyUi(player, EventLayer + "." + type);
            _ = CuiHelper.AddUi(player, container);
        }

        private (string activeUrl, string inactiveUrl) GetEventUrls(string type)
        {
            return type switch
            {
                "bradley" => (
                    _config.EventImages.BradleyActive,
                    _config.EventImages.BradleyInactive
                ),
                "cargo" => (_config.EventImages.CargoActive, _config.EventImages.CargoInactive),
                "heli" => (_config.EventImages.HeliActive, _config.EventImages.HeliInactive),
                _ => ("", ""),
            };
        }

        private void UpdateAllEvents(string type)
        {
            foreach (BasePlayer? player in BasePlayer.activePlayerList)
            {
                if (player?.IsConnected == true && !closePanel.Contains(player.userID))
                {
                    bool isActive = type switch
                    {
                        "bradley" => Events["bradley"],
                        "heli" => Events["heli"],
                        "cargo" => Events["cargo"],
                        _ => false,
                    };
                    float xPos = type switch
                    {
                        "heli" => 0.15f,
                        "bradley" => 0.30f,
                        "cargo" => 0.45f,
                        _ => 0f,
                    };
                    UpdateEventStatus(player, type, isActive, xPos);
                }
            }
        }

        private void DrawInterface(BasePlayer player)
        {
            _ = new CuiElementContainer()
            {
                {
                    new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Image = { Color = "0 0 0 0.8" },
                    },
                    "Overlay",
                    "CombinedPanel"
                },
                {
                    new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.5 0.9", AnchorMax = "0.5 0.95" },
                        Text =
                        {
                            Text = _config.TitleText,
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 20,
                            Color = "1 1 1 1",
                        },
                    },
                    "CombinedPanel"
                },
            };

            // ... existing code ...
        }

        /// <summary>
        /// Добавляем метод для обновления UI для всех активных игроков
        /// </summary>
        private void UpdateAllPlayers()
        {
            // Обновляем список игроков для удаления отключившихся игроков
            List<BasePlayer> playersToRemove = new();

            foreach (BasePlayer player in activePlayers)
            {
                if (player?.IsConnected != true)
                {
                    playersToRemove.Add(player);
                    continue;
                }

                // Обновляем только информационную панель для активных игроков
                // Не обновляем панель ивентов, чтобы избежать мерцания иконок
                DrawInfoPanel(player);
            }

            // Удаляем отключившихся игроков из списка
            foreach (BasePlayer player in playersToRemove)
            {
                _ = activePlayers.Remove(player);
            }
        }
        #endregion [Drawing Methods]

        #region [Helper Methods]
        private bool AddImage(string url, string shortname, ulong skinId = 0)
        {
            try
            {
                return !string.IsNullOrEmpty(url)
                    && !string.IsNullOrEmpty(shortname)
                    && ImageLibrary?.Call("AddImage", url, shortname, skinId) is bool result
                    && result;
            }
            catch
            {
                return false;
            }
        }

        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                return "1 1 1 1";
            }

            string str = hex.Trim('#');
            if (str.Length == 6)
            {
                str += "FF";
            }

            if (str.Length != 8)
            {
                return "1 1 1 1";
            }

            try
            {
                byte r = byte.Parse(
                    str.AsSpan(0, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture
                );
                byte g = byte.Parse(
                    str.AsSpan(2, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture
                );
                byte b = byte.Parse(
                    str.AsSpan(4, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture
                );
                byte a = byte.Parse(
                    str.AsSpan(6, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture
                );
                return $"{r / 255f:F2} {g / 255f:F2} {b / 255f:F2} {a / 255f:F2}";
            }
            catch
            {
                return "1 1 1 1";
            }
        }
        #endregion [Helper Methods]
    }
}
