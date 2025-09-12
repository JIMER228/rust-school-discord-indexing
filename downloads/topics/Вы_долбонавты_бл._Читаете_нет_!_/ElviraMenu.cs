using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Elvira Menu", "Cursor", "1.0.0")]
    public class ElviraMenu : RustPlugin
    {
        #region Fields
        private const string PERMISSION_USE = "elviramenu.use";
        private Dictionary<ulong, string> uiPlayers = new Dictionary<ulong, string>();
        private string MAIN_PANEL_NAME = "ElviraMenu_MainPanel";
        #endregion

        #region Oxide Hooks
        private void Init()
        {
            permission.RegisterPermission(PERMISSION_USE, this);
            cmd.AddChatCommand("menu", this, "CmdMenuOpen");
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            DestroyUI(player);
        }
        #endregion

        #region Commands
        private void CmdMenuOpen(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                player.ChatMessage("У вас нет прав для использования этой команды");
                return;
            }

            if (uiPlayers.ContainsKey(player.userID))
            {
                DestroyUI(player);
                return;
            }

            CreateUI(player);
        }
        #endregion

        #region UI Creation
        private void CreateUI(BasePlayer player)
        {
            DestroyUI(player);

            var container = new CuiElementContainer();
            
            // Main panel
            container.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.9" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.2 1", OffsetMin = "10 10", OffsetMax = "10 -10" },
                CursorEnabled = true
            }, "Overlay", MAIN_PANEL_NAME);

            // Profile section
            container.Add(new CuiPanel
            {
                Image = { Color = "0.15 0.15 0.15 0.95" },
                RectTransform = { AnchorMin = "0 0.92", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
            }, MAIN_PANEL_NAME, MAIN_PANEL_NAME + ".ProfilePanel");

            // Avatar image
            container.Add(new CuiElement
            {
                Parent = MAIN_PANEL_NAME + ".ProfilePanel",
                Components =
                {
                    new CuiRawImageComponent { Url = $"https://steamcdn-a.akamaihd.net/steamcommunity/public/images/avatars/43/43e26a888bdd6b546e7384424c62aa42b3c0fdf6.jpg", Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0.2 1", OffsetMin = "5 5", OffsetMax = "-5 -5" }
                }
            });

            // Username
            container.Add(new CuiLabel
            {
                Text = { Text = "Elvira#QuickRust", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.2 0.5", AnchorMax = "1 1", OffsetMin = "5 0", OffsetMax = "0 0" }
            }, MAIN_PANEL_NAME + ".ProfilePanel");

            // XP
            container.Add(new CuiLabel
            {
                Text = { Text = "0.3245XP", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.7 0.7 0.7 1" },
                RectTransform = { AnchorMin = "0.2 0", AnchorMax = "1 0.5", OffsetMin = "5 0", OffsetMax = "0 0" }
            }, MAIN_PANEL_NAME + ".ProfilePanel");

            // Menu items with icons
            MenuItemData[] menuItems = new MenuItemData[]
            {
                new MenuItemData { Name = "Информация", IconChar = "ℹ" }, // Information
                new MenuItemData { Name = "Корзина", IconChar = "🛒" }, // Basket
                new MenuItemData { Name = "Календарь", IconChar = "📅" }, // Calendar
                new MenuItemData { Name = "Точки дома", IconChar = "🏠" }, // Home points
                new MenuItemData { Name = "Наёмники", IconChar = "👥" }, // Mercenaries
                new MenuItemData { Name = "Статистика", IconChar = "📊" }, // Statistics
                new MenuItemData { Name = "Вайп-блок", IconChar = "🔄" }, // Wipe block
                new MenuItemData { Name = "Навыки", IconChar = "⚒" }, // Skills
                new MenuItemData { Name = "Жалобы", IconChar = "⚠" }, // Complaints
                new MenuItemData { Name = "Настройки", IconChar = "⚙" } // Settings
            };

            for (int i = 0; i < menuItems.Length; i++)
            {
                string buttonName = MAIN_PANEL_NAME + $".Button.{i}";
                
                // Button panel
                container.Add(new CuiPanel
                {
                    Image = { Color = "0.20 0.20 0.20 0.95" },
                    RectTransform = { AnchorMin = $"0 {0.92 - ((i + 1) * 0.055)}", AnchorMax = $"1 {0.92 - (i * 0.055)}" }
                }, MAIN_PANEL_NAME, buttonName);

                // Button text
                container.Add(new CuiLabel
                {
                    Text = { Text = menuItems[i].Name, FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.2 0", AnchorMax = "1 1", OffsetMin = "5 0", OffsetMax = "0 0" }
                }, buttonName);

                // Icon
                container.Add(new CuiLabel
                {
                    Text = { Text = menuItems[i].IconChar, FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.7 0.7 0.7 1" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.2 1" }
                }, buttonName);

                // Button functionality
                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = $"elviramenu.action {i}" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text = { Text = "" }
                }, buttonName);
            }

            // Close button (hidden but functional)
            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = "elviramenu.close" },
                RectTransform = { AnchorMin = "0.9 0.01", AnchorMax = "0.99 0.05" },
                Text = { Text = "", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, MAIN_PANEL_NAME);

            CuiHelper.AddUi(player, container);
            uiPlayers[player.userID] = MAIN_PANEL_NAME;
        }

        private void DestroyUI(BasePlayer player)
        {
            if (uiPlayers.TryGetValue(player.userID, out string uiName))
            {
                CuiHelper.DestroyUi(player, uiName);
                uiPlayers.Remove(player.userID);
            }
        }
        #endregion

        #region Helper Classes
        private class MenuItemData
        {
            public string Name { get; set; }
            public string IconChar { get; set; }
        }
        #endregion

        #region Commands Handling
        [ConsoleCommand("elviramenu.close")]
        private void ConsoleMenuClose(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player == null) return;
            
            DestroyUI(player);
        }

        [ConsoleCommand("elviramenu.action")]
        private void ConsoleMenuAction(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player == null) return;
            
            int actionId = arg.GetInt(0, -1);
            if (actionId == -1) return;
            
            // Implement menu actions here
            switch (actionId)
            {
                case 0: // Information
                    player.ChatMessage("Вы открыли раздел Информация");
                    break;
                case 1: // Basket
                    player.ChatMessage("Вы открыли раздел Корзина");
                    break;
                case 2: // Calendar
                    player.ChatMessage("Вы открыли раздел Календарь");
                    break;
                case 3: // Home points
                    player.ChatMessage("Вы открыли раздел Точки дома");
                    break;
                case 4: // Mercenaries
                    player.ChatMessage("Вы открыли раздел Наёмники");
                    break;
                case 5: // Statistics
                    player.ChatMessage("Вы открыли раздел Статистика");
                    break;
                case 6: // Wipe block
                    player.ChatMessage("Вы открыли раздел Вайп-блок");
                    break;
                case 7: // Skills
                    player.ChatMessage("Вы открыли раздел Навыки");
                    break;
                case 8: // Complaints
                    player.ChatMessage("Вы открыли раздел Жалобы");
                    break;
                case 9: // Settings
                    player.ChatMessage("Вы открыли раздел Настройки");
                    break;
                default:
                    break;
            }
        }
        #endregion
    }
} 