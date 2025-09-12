using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using Oxide.Core;
using Facepunch;
using Network;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Remove", "Mercury", "2.34.44")]
    [Description("Allows players to remove unprotected objects with a crosshair.")]
    public class Remove : RustPlugin
    {
        #region Configuration
        private static Configuration config;

        private class Configuration
        {
            [JsonProperty("Remove mode range")]
            public float RemoveRange = 5f;

            [JsonProperty("Remove mode crosshair color (RGBA)")]
            public string CrosshairColor = "1 0 0 1";

            [JsonProperty("Crosshair size")]
            public float CrosshairSize = 10f;

            [JsonProperty("Crosshair thickness")]
            public float CrosshairThickness = 2f;

            [JsonProperty("Notification duration (seconds)")]
            public float NotificationDuration = 30f;

            [JsonProperty("Overlay background color (RGBA)")]
            public string OverlayBackgroundColor = "0 0 0 0.7";

            [JsonProperty("Overlay text color (RGBA)")]
            public string OverlayTextColor = "1 0.8 0 1";

            [JsonProperty("Overlay border color (RGBA)")]
            public string OverlayBorderColor = "1 0.8 0 0.5";

            [JsonProperty("Overlay text")]
            public string OverlayText = "Режим удаления активен\nНажмите ЛКМ для удаления";

            [JsonProperty("Overlay text size")]
            public int OverlayTextSize = 12;

            [JsonProperty("Overlay position (X Y Width Height)")]
            public string OverlayPosition = "0 0.85 0.25 0.95";

            [JsonProperty("Double activation delay (seconds)")]
            public float DoubleActivationDelay = 0.1f;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning("Error reading configuration, creating new one!");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = new Configuration();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Variables
        private Dictionary<BasePlayer, bool> removeMode = new Dictionary<BasePlayer, bool>();
        private Dictionary<BasePlayer, Item> previousItems = new Dictionary<BasePlayer, Item>();

        private const string UI_CROSSHAIR_VERTICAL = "RemoveCrosshairVertical";
        private const string UI_CROSSHAIR_HORIZONTAL = "RemoveCrosshairHorizontal";
        private const string UI_OVERLAY = "RemoveOverlay";
        #endregion

        #region Hooks
        private void Init()
        {
            cmd.AddChatCommand("remove", this, nameof(CmdRemove));
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (!removeMode.ContainsKey(player))
                removeMode[player] = false;
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!removeMode.ContainsKey(player) || !removeMode[player]) return;

            // Block item interactions via metabolism
            player.metabolism.bleeding.value = 1f;
            player.metabolism.bleeding.min = 1f;
            player.metabolism.bleeding.max = 1f;

            if (input.WasJustPressed(BUTTON.FIRE_PRIMARY))
            {
                TryRemoveEntity(player);
            }
        }

        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (removeMode.ContainsKey(player) && removeMode[player])
            {
                DisableRemoveMode(player);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (removeMode.ContainsKey(player))
            {
                DisableRemoveMode(player);
                removeMode.Remove(player);
                previousItems.Remove(player);
            }
        }
        #endregion

        #region Commands
        private void CmdRemove(BasePlayer player, string command, string[] args)
        {
            if (!removeMode.ContainsKey(player))
                removeMode[player] = false;

            if (removeMode[player])
            {
                DisableRemoveMode(player);
                return;
            }

            EnableRemoveMode(player);
            timer.Once(config.DoubleActivationDelay, () => EnableRemoveMode(player));
        }
        #endregion

        #region Core Functions
        private void EnableRemoveMode(BasePlayer player)
        {
            if (removeMode.ContainsKey(player) && removeMode[player]) return;

            removeMode[player] = true;
            ShowCrosshair(player);
            ShowOverlay(player);

            // Save current item and remove it from the player's hands
            if (player.GetActiveItem() != null)
            {
                previousItems[player] = player.GetActiveItem();
                player.GetActiveItem().RemoveFromContainer();
            }

            player.ChatMessage(GetLang("REMOVE_MODE_ENABLED", player.UserIDString));

            // Auto-disable after the specified duration
            timer.Once(config.NotificationDuration, () =>
            {
                if (removeMode.ContainsKey(player) && removeMode[player])
                {
                    player.ChatMessage(GetLang("REMOVE_MODE_DISABLED_AUTO", player.UserIDString));
                    DisableRemoveMode(player);
                }
            });
        }

        private void DisableRemoveMode(BasePlayer player)
        {
            if (!removeMode.ContainsKey(player)) return;

            removeMode[player] = false;
            HideCrosshair(player);
            HideOverlay(player);

            // Restore metabolism state
            player.metabolism.bleeding.value = 0f;
            player.metabolism.bleeding.min = 0f;
            player.metabolism.bleeding.max = 0f;

            // Restore previous item if it exists
            if (previousItems.ContainsKey(player) && previousItems[player] != null)
            {
                previousItems[player].MoveToContainer(player.inventory.containerBelt);
                previousItems.Remove(player);
            }

            player.ChatMessage(GetLang("REMOVE_MODE_DISABLED", player.UserIDString));
        }

        private void TryRemoveEntity(BasePlayer player)
        {
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, config.RemoveRange))
                return;

            BaseEntity entity = hit.GetEntity();
            if (entity == null || entity.OwnerID == 0 || entity is BasePlayer)
                return;

            BuildingPrivlidge privilege = entity.GetBuildingPrivilege();
            if (privilege != null && !privilege.IsAuthed(player))
            {
                player.ChatMessage(GetLang("REMOVE_NOT_AUTHORIZED", player.UserIDString));
                return;
            }

            entity.Kill();
            player.ChatMessage(GetLang("REMOVE_SUCCESS", player.UserIDString));
        }
        #endregion

        #region UI Functions
        private void ShowCrosshair(BasePlayer player)
        {
            var container = new CuiElementContainer();

            // Vertical line
            container.Add(new CuiElement
            {
                Name = UI_CROSSHAIR_VERTICAL,
                Parent = "Hud",
                Components =
                {
                    new CuiRawImageComponent { Color = config.CrosshairColor },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = $"{-config.CrosshairThickness / 2} {-config.CrosshairSize}",
                        OffsetMax = $"{config.CrosshairThickness / 2} {config.CrosshairSize}"
                    }
                }
            });

            // Horizontal line
            container.Add(new CuiElement
            {
                Name = UI_CROSSHAIR_HORIZONTAL,
                Parent = "Hud",
                Components =
                {
                    new CuiRawImageComponent { Color = config.CrosshairColor },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = $"{-config.CrosshairSize} {-config.CrosshairThickness / 2}",
                        OffsetMax = $"{config.CrosshairSize} {config.CrosshairThickness / 2}"
                    }
                }
            });

            CuiHelper.AddUi(player, container);
        }

        private void HideCrosshair(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UI_CROSSHAIR_VERTICAL);
            CuiHelper.DestroyUi(player, UI_CROSSHAIR_HORIZONTAL);
        }

        private void ShowOverlay(BasePlayer player)
        {
            var container = new CuiElementContainer();

            // Background overlay
            container.Add(new CuiElement
            {
                Name = UI_OVERLAY,
                Parent = "Hud",
                Components =
                {
                    new CuiImageComponent { Color = config.OverlayBackgroundColor },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = config.OverlayPosition.Split(' ')[0] + " " + config.OverlayPosition.Split(' ')[1],
                        AnchorMax = config.OverlayPosition.Split(' ')[2] + " " + config.OverlayPosition.Split(' ')[3]
                    }
                }
            });

            // Instruction text
            container.Add(new CuiElement
            {
                Name = UI_OVERLAY + "_Text",
                Parent = UI_OVERLAY,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = config.OverlayText,
                        FontSize = config.OverlayTextSize,
                        Align = TextAnchor.MiddleCenter,
                        Color = config.OverlayTextColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }
            });

            // Decorative border
            container.Add(new CuiElement
            {
                Name = UI_OVERLAY + "_Border",
                Parent = UI_OVERLAY,
                Components =
                {
                    new CuiImageComponent { Color = config.OverlayBorderColor },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = "1 1",
                        OffsetMax = "-1 -1"
                    }
                }
            });

            CuiHelper.AddUi(player, container);
        }

        private void HideOverlay(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UI_OVERLAY);
            CuiHelper.DestroyUi(player, UI_OVERLAY + "_Text");
            CuiHelper.DestroyUi(player, UI_OVERLAY + "_Border");
        }
        #endregion

        #region Localization
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["REMOVE_MODE_ENABLED"] = "Remove mode enabled. Click to remove unprotected objects.",
                ["REMOVE_MODE_DISABLED"] = "Remove mode disabled.",
                ["REMOVE_MODE_DISABLED_AUTO"] = "Remove mode automatically disabled after {0} seconds.",
                ["REMOVE_NOT_AUTHORIZED"] = "You are not authorized on the Tool Cupboard to remove this.",
                ["REMOVE_SUCCESS"] = "Object removed successfully."
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["REMOVE_MODE_ENABLED"] = "Режим удаления включен. Нажмите, чтобы удалить незащищенные объекты.",
                ["REMOVE_MODE_DISABLED"] = "Режим удаления выключен.",
                ["REMOVE_MODE_DISABLED_AUTO"] = "Режим удаления автоматически отключен через {0} секунд.",
                ["REMOVE_NOT_AUTHORIZED"] = "Вы не авторизованы на шкафу для удаления этого объекта.",
                ["REMOVE_SUCCESS"] = "Объект успешно удален."
            }, this, "ru");
        }

        private string GetLang(string key, string userId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, userId), args);
        }
        #endregion
    }
}