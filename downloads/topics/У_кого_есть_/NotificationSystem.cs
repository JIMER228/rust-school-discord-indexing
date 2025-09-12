using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("NotificationSystem", "YourName", "1.0.0")]
    public class NotificationSystem : RustPlugin
    {
        private const string UIParent = "Overlay";
        private const float DisplayTime = 3.5f;
        private Dictionary<ulong, List<string>> activeNotifications = new();
        #region Configuration
        private Configuration config;
        private class Configuration
        {
            [JsonProperty("Maximum concurrent notifications")]
            public int MaxNotifications { get; set; } = 5;
            [JsonProperty("Notification spacing (pixels)")]
            public float NotificationSpacing { get; set; } = 5f;
            [JsonProperty("Base position from top")]
            public float BasePositionFromTop { get; set; } = 250f;
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        #endregion

        #region Oxide Hooks
        private void OnServerInitialized()
        {
            permission.RegisterPermission("notificationsystem.use", this);
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyAllNotifications(player);
            }
        }
        #endregion

        #region API Methods
        [HookMethod("ShowNotification")]
        public void ShowNotification(BasePlayer player, string message, string type = "default")
        {
            if (player == null || !player.IsConnected) return;

            var notificationData = new NotificationData
            {
                Message = message,
                Type = type,
                Icon = GetIconForType(type),
                Color = GetColorForType(type)
            };

            CreateNotification(player, notificationData);
        }

        private string GetIconForType(string type)
        {
            return type.ToLower() switch
            {
                "raid" => "assets/icons/raid.png",
                "gift" => "assets/icons/gift.png",
                "warning" => "assets/icons/warning.png",
                _ => "assets/icons/notification.png"
            };
        }

        private string GetColorForType(string type)
        {
            return type.ToLower() switch
            {
                "raid" => "1 0.1 0.1 1",
                "gift" => "0.1 1 0.1 1",
                "warning" => "1 0.92 0.016 1",
                _ => "1 1 1 1"
            };
        }
        #endregion

        #region UI Implementation
        private class NotificationData
        {
            public string Message { get; set; }
            public string Type { get; set; }
            public string Icon { get; set; }
            public string Color { get; set; }
        }

        private void CreateNotification(BasePlayer player, NotificationData data)
        {
            if (!activeNotifications.ContainsKey(player.userID))
            {
                activeNotifications[player.userID] = new List<string>();
            }

            // Remove excess notifications
            while (activeNotifications[player.userID].Count >= config.MaxNotifications)
            {
                var oldestId = activeNotifications[player.userID][0];
                DestroyNotification(player, oldestId);
                activeNotifications[player.userID].RemoveAt(0);
            }

            string notificationId = $"notification_{player.userID}_{DateTime.Now.Ticks}";
            var container = new CuiElementContainer();
            // Main notification panel
            container.Add(new CuiElement
            {
                Parent = UIParent,
                Name = notificationId,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"0.5 {1 - ((activeNotifications[player.userID].Count + 1) * 0.05f)}",
                        AnchorMax = $"0.5 {1 - (activeNotifications[player.userID].Count * 0.05f)}",
                        OffsetMin = "-150 0",
                        OffsetMax = "150 0"
                    },
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0.8",
                        Material = "assets/content/ui/uibackgroundblur.mat"
                    }
                }
            });

            // Icon
            container.Add(new CuiElement
            {
                Parent = notificationId,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Url = data.Icon,
                        Color = data.Color
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0.2 1"
                    }
                }
            });

            // Message
            container.Add(new CuiElement
            {
                Parent = notificationId,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = data.Message,
                        FontSize = 14,
                        Align = TextAnchor.MiddleLeft,
                        Color = data.Color
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.22 0",
                        AnchorMax = "0.98 1"
                    }
                }
            });

            CuiHelper.AddUi(player, container);
            activeNotifications[player.userID].Add(notificationId);

            timer.Once(DisplayTime, () => DestroyNotification(player, notificationId));
        }

        private void DestroyNotification(BasePlayer player, string notificationId)
        {
            if (player == null || !player.IsConnected) return;
            CuiHelper.DestroyUi(player, notificationId);
            if (activeNotifications.ContainsKey(player.userID))
            {
                activeNotifications[player.userID].Remove(notificationId);
            }
        }

        private void DestroyAllNotifications(BasePlayer player)
        {
            if (!activeNotifications.ContainsKey(player.userID)) return;

            foreach (var notificationId in activeNotifications[player.userID])
            {
                CuiHelper.DestroyUi(player, notificationId);
            }
            activeNotifications.Remove(player.userID);
        }
        #endregion

        #region Example Usage
        [ChatCommand("testnotify")]
        private void TestNotificationCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "notificationsystem.use"))
                return;

            string type = args.Length > 0 ? args[0].ToLower() : "default";
            string message = args.Length > 1 ? string.Join(" ", args.Skip(1)) : "Test notification!";
            ShowNotification(player, message, type);
        }
        #endregion
    }
}