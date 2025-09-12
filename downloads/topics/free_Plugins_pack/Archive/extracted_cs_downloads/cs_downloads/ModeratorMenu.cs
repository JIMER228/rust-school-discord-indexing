using System.Collections.Generic;
using System.IO;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ModeratorMenu", "Crunchy", "2.2.2")]
    class ModeratorMenu : RustPlugin
    {
        private string serverName = "Moderator Panel";
        private Dictionary<ulong, string> playerSearchInput = new Dictionary<ulong, string>(); // Store search input per player
        private const string PermissionUseMenu = "moderatormenu.use"; // Updated permission string

        private void Init()
        {
            permission.RegisterPermission(PermissionUseMenu, this);
        }

        [ChatCommand("menu")]
        private void OpenMenu(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionUseMenu))
            {
                SendReply(player, "You do not have permission to use this command.");
                return;
            }

            ShowModeratorUI(player);
        }

        private void ShowModeratorUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "MenuSystems_background"); // Clear any existing UI

            var ui = new CuiElementContainer();

            // Main Background Panel
            Create.Panel(ref ui, "MenuSystems_background", "Overlay", "0 0 0 0.85", "0 0", "1 1", true);

            // Title Bar
            Create.Panel(ref ui, "MenuSystems_titlebar", "MenuSystems_background", "0.15 0.15 0.15 0.95", "0 0.95", "1 1");
            Create.Text(ref ui, "MenuSystems_title", "MenuSystems_titlebar", "1 0.7 0 1", serverName, 32, "0.02 0", "0.3 1", TextAnchor.MiddleLeft);

            // Close Button
            Create.Button(ref ui, "MenuSystems_close", "MenuSystems_titlebar", "0.9 0.1 0.1 1", "X", 20, "0.95 0.05", "0.99 0.9", "menusystems.close", "1 1 1 1");

            // Add Button centered in the title bar, slightly shortened
            Create.Button(ref ui, "MenuSystems_add", "MenuSystems_titlebar", "1 0.5 0 1", "Add", 20, "0.4 0.1", "0.6 0.9", "search.mod", "1 1 1 1");

            // Search Bar Panel
            Create.Panel(ref ui, "MenuSystems_searchBar", "MenuSystems_background", "0.1 0.1 0.1 0.9", "0.4 0.91", "0.6 0.948");

            // Input Field for Steam ID
            ui.Add(new CuiElement
            {
                Name = "ModeratorMenu_SearchInput",
                Parent = "MenuSystems_searchBar",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Align = TextAnchor.MiddleLeft,
                        FontSize = 14,
                        Command = "moderatormenu.input",
                        IsPassword = false,
                        Text = "",
                        Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.02 0.1",
                        AnchorMax = "0.98 0.9"
                    }
                }
            });

            // Populate list with moderators and owners from users.cfg
            ListAllModerators(ref ui, player);

            // Display UI to player
            CuiHelper.AddUi(player, ui);
        }

        [ConsoleCommand("menusystems.close")]
        private void CloseMenu(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player != null)
                CuiHelper.DestroyUi(player, "MenuSystems_background");
        }

        [ConsoleCommand("moderatormenu.input")]
        private void CaptureSearchInput(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player == null || args.Args == null || args.Args.Length < 1) return;

            string input = args.GetString(0);

            if (playerSearchInput.ContainsKey(player.userID))
                playerSearchInput[player.userID] = input;
            else
                playerSearchInput.Add(player.userID, input);
        }

        [ConsoleCommand("search.mod")]
        private void AddModerator(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player == null || !playerSearchInput.ContainsKey(player.userID))
            {
                SendReply(player, "No Steam ID entered.");
                return;
            }

            string steamID = playerSearchInput[player.userID];

            // Run commands to add the user as a moderator and admin group
            ConsoleSystem.Run(ConsoleSystem.Option.Server, $"moderatorid {steamID}");
            ConsoleSystem.Run(ConsoleSystem.Option.Server, $"adminrestrictions.addadmintogroup admin {steamID}");
            Puts($"Added moderator with Steam ID: {steamID} and assigned to admin group.");

            // Clear the entered Steam ID after adding
            playerSearchInput.Remove(player.userID);

            // Refresh UI
            ShowModeratorUI(player);
        }

        [ConsoleCommand("menusystems.remove")]
private void RemoveUser(ConsoleSystem.Arg args)
{
    var player = args.Player();
    if (args.Args == null || args.Args.Length < 1)
    {
        SendReply(player, "Invalid command usage.");
        return;
    }

    string userID = args.Args[0];
    if (!ulong.TryParse(userID, out ulong steamID))
    {
        SendReply(player, "Invalid Steam ID.");
        return;
    }

    var moderators = GetModeratorsFromConfig();
    if (moderators.TryGetValue(userID, out var userData))
    {
        string role = userData.role;
        if (role == "Moderator")
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server, $"removemoderator {userID}");
            ConsoleSystem.Run(ConsoleSystem.Option.Server, $"adminrestrictions.removeadminfromgroup admin {userID}");
            Puts($"Removed moderator with Steam ID: {userID} and updated admin group");
        }
        else if (role == "Owner")
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server, $"removeowner {userID}");
            ConsoleSystem.Run(ConsoleSystem.Option.Server, $"adminrestrictions.removeadminfromgroup admin {userID}");
            Puts($"Removed owner with Steam ID: {userID} and updated admin group");
        }

        // Refresh the UI to reflect the changes immediately
        if (player != null)
        {
            ShowModeratorUI(player);
        }
    }
    else
    {
        SendReply(player, "User not found in configuration.");
    }
}


        private void ListAllModerators(ref CuiElementContainer ui, BasePlayer player)
        {
            var moderators = GetModeratorsFromConfig();
            float panelWidth = 0.12f;
            float panelHeight = 0.06f;
            float textSize = 10;
            float xStart = 0.05f;
            float yStart = 0.8f;
            int columns = 5;
            int count = 0;

            foreach (var moderator in moderators)
            {
                string panelName = $"moderator_card_{moderator.Key}";
                string buttonName = $"remove_button_{moderator.Key}";

                string displayName = moderator.Value.displayName ?? "Unknown";
                float xMin = xStart + (count % columns) * (panelWidth + 0.02f);
                float yMin = yStart - (count / columns) * (panelHeight + 0.02f);

                Create.Panel(ref ui, panelName, "MenuSystems_background", "0.2 0.2 0.2 1", $"{xMin} {yMin}", $"{xMin + panelWidth} {yMin + panelHeight}");
                Create.Text(ref ui, $"{panelName}_name", panelName, "1 1 1 1", displayName, (int)textSize, "0.05 0.5", "0.95 1", TextAnchor.MiddleLeft);
                Create.Text(ref ui, $"{panelName}_id", panelName, "1 1 1 1", $"ID: {moderator.Key}", (int)textSize, "0.05 0", "0.95 0.5", TextAnchor.MiddleLeft);

                Create.Button(ref ui, buttonName, panelName, "0.9 0.5 0 1", "Remove", (int)(textSize - 2), "0.75 0.15", "0.95 0.85", $"menusystems.remove {moderator.Key}", "1 1 1 1");

                count++;
            }
        }

        private Dictionary<string, (string role, string displayName)> GetModeratorsFromConfig()
        {
            var moderators = new Dictionary<string, (string role, string displayName)>();
            string configPath = $"{ConVar.Server.rootFolder}/cfg/users.cfg";

            if (!File.Exists(configPath))
            {
                Puts("users.cfg not found.");
                return moderators;
            }

            foreach (var line in File.ReadAllLines(configPath))
            {
                if (line.StartsWith("moderatorid") || line.StartsWith("ownerid"))
                {
                    string[] parts = line.Split(' ');
                    if (parts.Length >= 2)
                    {
                        string userID = parts[1];
                        string role = line.StartsWith("moderatorid") ? "Moderator" : "Owner";
                        string displayName = covalence.Players.FindPlayerById(userID)?.Name ?? "Unknown";

                        if (!moderators.ContainsKey(userID))
                        {
                            moderators.Add(userID, (role, displayName));
                        }
                    }
                }
            }
            return moderators;
        }

        public static class Create
        {
            public static void Panel(ref CuiElementContainer ui, string name, string parent, string color, string aMin, string aMax, bool cursor = false)
            {
                ui.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    CursorEnabled = cursor
                }, parent, name);
            }

            public static void Text(ref CuiElementContainer ui, string name, string parent, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                ui.Add(new CuiLabel
                {
                    Text = { Text = text, FontSize = size, Align = align, Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                }, parent, name);
            }

            public static void Button(ref CuiElementContainer ui, string name, string parent, string color, string text, int size, string aMin, string aMax, string command, string textColor)
            {
                ui.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 0.1f },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = TextAnchor.MiddleCenter, Color = textColor }
                }, parent, name);
            }
        }
    }
}
