using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("TPMenu", "SARO", "1.0.0")]
    public class TPMenu : RustPlugin
    {
        private readonly PluginConfig config;
        private const string MainLayer = "tpmenu";
        private const float CACHE_TIMEOUT = 5f;
        private const float BUTTON_HEIGHT = 0.04f;
        private const float BUTTON_SPACING = 0.005f;
        private const float BUTTON_WIDTH = 0.075f;
        private const float PANEL_BG_COLOR = 0.12f;
        private const float BUTTON_BG_COLOR = 0.19f;
        private const float OUTLINE_COLOR = 0.30f;
        private const float TEXT_COLOR = 0.8f;
        private const int FONT_SIZE = 12;
        private const string FONT_NAME = "robotocondensed-regular.ttf";
        private const string BOLD_FONT_NAME = "robotocondensed-bold.ttf";
        private const float BASE_FONT_SIZE = 12f;
        private const float MIN_INTERFACE_SCALE = 0.6f;
        private const float MAX_INTERFACE_SCALE = 1.0f;
        private const float MAX_FONT_SIZE = 24f;

        private const int BUTTONS_PER_PAGE = 5;
        private const float SCROLL_UP_Y = 0.715f;
        private const float SCROLL_DOWN_Y = 0.515f;

        private const int TITLE_FONT_SIZE = 20;
        private const int SMALL_FONT_SIZE = 10;

        [PluginReference]
        private readonly Plugin? ImageLibrary;

        [PluginReference]
        private readonly Plugin? Friends;

        [PluginReference]
        private readonly Plugin? MutualPermission;

        [PluginReference]
        private readonly Plugin? NTeleportation;

        [PluginReference]
        private readonly Plugin? Teleport;

        [PluginReference]
        private readonly Plugin? Teleportation;

        [PluginReference]
        private readonly Plugin? HomesGUI;

        [PluginReference]
        private readonly Plugin? Home;

        [PluginReference]
        private readonly Plugin? IQTeleportation;

        private readonly Dictionary<string, string> imageCache;
        private readonly Dictionary<ulong, Dictionary<string, Vector3>> homesCache;
        private readonly Dictionary<ulong, List<ulong>> friendsCache;
        private readonly Dictionary<ulong, DateTime> lastHomesUpdate;
        private readonly Dictionary<ulong, DateTime> lastFriendsUpdate;
        private readonly Dictionary<ulong, bool> PlayersList;
        private readonly Dictionary<ulong, int> menuPageByUser;
        private readonly Dictionary<ulong, bool> actionsInProgress;

        private const float LEFT_PANEL_X = 0.377f;
        private const float CENTER_PANEL_X = 0.453f;
        private const float RIGHT_PANEL_X = 0.547f;
        private const float PANEL_WIDTH = 0.075f;
        private const float TOP_PANEL_Y = 0.915f;
        private const float PANEL_HEIGHT = 0.15f;

        private const float BASE_BUTTON_HEIGHT = 0.04f;
        private const float BASE_BUTTON_SPACING = 0.005f;
        private const float BASE_BUTTON_WIDTH = 0.075f;

        public TPMenu()
        {
            config = new PluginConfig();
            imageCache = new Dictionary<string, string>();
            homesCache = new Dictionary<ulong, Dictionary<string, Vector3>>();
            friendsCache = new Dictionary<ulong, List<ulong>>();
            lastHomesUpdate = new Dictionary<ulong, DateTime>();
            lastFriendsUpdate = new Dictionary<ulong, DateTime>();
            PlayersList = new Dictionary<ulong, bool>();
            menuPageByUser = new Dictionary<ulong, int>();
            actionsInProgress = new Dictionary<ulong, bool>();

            ImageLibrary = plugins.Find("ImageLibrary");
            Friends = plugins.Find("Friends");
            MutualPermission = plugins.Find("MutualPermission");
            NTeleportation = plugins.Find("NTeleportation");
            Teleport = plugins.Find("Teleport");
            Teleportation = plugins.Find("Teleportation");
            HomesGUI = plugins.Find("HomesGUI");
            Home = plugins.Find("Home");
            IQTeleportation = plugins.Find("IQTeleportation");
        }

        private void Loaded()
        {
            LoadData();
            permission.RegisterPermission("tpmenu.use", this);
            LoadConfig();

            cmd.RemoveConsoleCommand("tpmenu", this);
            cmd.AddConsoleCommand("tpmenu", this, nameof(cmdOpentpmenuConsole));

            if (config?.MainSettings?.Commands != null)
            {
                foreach (string command in config.MainSettings.Commands)
                {
                    cmd.AddChatCommand(command, this, nameof(cmdOpentpmenu));
                }
            }
        }

        [HookMethod("OnPluginLoaded")]
        private void OnPluginLoaded(Plugin plugin)
        {
            // Method intentionally left empty.
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Создание новой конфигурации по умолчанию...");

            if (config == null)
            {
                return;
            }

            config.MainSettings = new MainSettings
            {
                Commands = new List<string> { "menu", "tpmenu" }
                    .Distinct()
                    .ToList(),
                ShowOnConnect = false,
                ShowOnlyFirstConnect = false,
            };

            config.Homes = new ButtonConfig
            {
                Title = "ДОМА",
                ButtonColor = "0.19 0.19 0.21 0.75",
                TextColor = "1 1 1 1",
                ButtonText = new Dictionary<string, string> { { "ru", "ДОМА" } },
            };

            config.Friends = new ButtonConfig
            {
                Title = "ДРУЗЬЯ",
                ButtonColor = "0.19 0.19 0.21 0.75",
                TextColor = "1 1 1 1",
                ButtonText = new Dictionary<string, string> { { "ru", "ДРУЗЬЯ" } },
            };

            config.Home = new HomeSettings();
            config.FriendsSettings = new FriendSettings();
        }

        private void LoadConfig()
        {
            try
            {
                PluginConfig loadedConfig = null;
                if (Config.Exists())
                {
                    loadedConfig = Config.ReadObject<PluginConfig>();
                }

                if (loadedConfig != null)
                {
                    if (loadedConfig.MainSettings != null)
                    {
                        config.MainSettings = loadedConfig.MainSettings;
                        if (config.MainSettings.Commands != null)
                        {
                            config.MainSettings.Commands = config
                                .MainSettings.Commands.Distinct()
                                .ToList();
                        }
                        if (config.MainSettings.ButtonSettings?.Buttons != null)
                        {
                            config.MainSettings.ButtonSettings.Buttons = config
                                .MainSettings.ButtonSettings.Buttons.GroupBy(b => new
                                {
                                    b.Command,
                                    b.Text,
                                })
                                .Select(g => g.First())
                                .ToList();
                        }
                    }
                    if (loadedConfig.Homes != null)
                    {
                        config.Homes = loadedConfig.Homes;
                    }
                    if (loadedConfig.Friends != null)
                    {
                        config.Friends = loadedConfig.Friends;
                    }
                    if (loadedConfig.Home != null)
                    {
                        config.Home = loadedConfig.Home;
                    }
                    if (loadedConfig.FriendsSettings != null)
                    {
                        config.FriendsSettings = loadedConfig.FriendsSettings;
                    }
                    if (loadedConfig.ConfigVersion != default)
                    {
                        config.ConfigVersion = loadedConfig.ConfigVersion;
                    }
                }
                else
                {
                    LoadDefaultConfig();
                    SaveConfig();
                }

                config.MainSettings ??= new MainSettings();
                config.MainSettings.ButtonSettings ??= new ButtonSettings();

                if (config.MainSettings.Commands == null || config.MainSettings.Commands.Count == 0)
                {
                    config.MainSettings.Commands = new List<string> { "menu", "tpmenu" };
                }
                else
                {
                    config.MainSettings.Commands = config.MainSettings.Commands.Distinct().ToList();
                }

                if (
                    config.MainSettings.ButtonSettings.Buttons == null
                    || config.MainSettings.ButtonSettings.Buttons.Count == 0
                )
                {
                    config.MainSettings.ButtonSettings.Buttons = new List<MenuButton>
                    {
                        new() { Command = "chat.say /block", Text = "БЛОКИРОВКА" },
                        new() { Command = "chat.say /help", Text = "ПОМОЩЬ" },
                    };
                }
                else
                {
                    config.MainSettings.ButtonSettings.Buttons = config
                        .MainSettings.ButtonSettings.Buttons.GroupBy(b => new { b.Command, b.Text })
                        .Select(g => g.First())
                        .ToList();
                }

                SaveConfig();
            }
            catch (Exception ex)
            {
                PrintError($"Error in LoadConfig: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void UpdateConfigValues()
        {
            if (config.ConfigVersion < PluginConfig.DefaultConfig().ConfigVersion)
            {
                PrintWarning("Updating old config version...");
                PluginConfig baseConfig = PluginConfig.DefaultConfig();
                config.ConfigVersion = baseConfig.ConfigVersion;
                SaveConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config, true);
        }

        private void LoadData()
        {
            try
            {
                PlayersList.Clear();
                Dictionary<ulong, bool> data = Interface.Oxide.DataFileSystem.ReadObject<
                    Dictionary<ulong, bool>
                >("tpmenu_Players");
                if (data != null)
                {
                    foreach (KeyValuePair<ulong, bool> kvp in data)
                    {
                        PlayersList[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error loading data: {ex.Message}");
                PlayersList.Clear();
            }
        }

        private void SaveData()
        {
            try
            {
                Interface.Oxide.DataFileSystem.WriteObject("tpmenu_Players", PlayersList);
            }
            catch (Exception ex)
            {
                PrintError($"Error saving data: {ex.Message}");
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (!config.MainSettings.ShowOnConnect)
            {
                return;
            }

            if (config.MainSettings.ShowOnlyFirstConnect)
            {
                if (!PlayersList.ContainsKey(player.userID))
                {
                    PlayersList[player.userID] = true;
                    SaveData();
                    _ = timer.Once(2f, () => CreateMenu(player));
                }
            }
            else
            {
                _ = timer.Once(2f, () => CreateMenu(player));
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            _ = homesCache.Remove(player.userID);
            _ = friendsCache.Remove(player.userID);
            _ = lastHomesUpdate.Remove(player.userID);
            _ = lastFriendsUpdate.Remove(player.userID);
            _ = actionsInProgress.Remove(player.userID);
        }

        private Dictionary<string, Vector3> GetHomes(ulong playerid)
        {
            try
            {
                if (
                    homesCache.TryGetValue(playerid, out Dictionary<string, Vector3>? cachedHomes)
                    && lastHomesUpdate.TryGetValue(playerid, out DateTime lastUpdate)
                    && (DateTime.Now - lastUpdate).TotalSeconds < CACHE_TIMEOUT
                )
                {
                    return cachedHomes;
                }

                BasePlayer player = BasePlayer.FindByID(playerid);
                if (player == null)
                {
                    return new Dictionary<string, Vector3>();
                }

                Dictionary<string, Vector3> homes = new();

                Plugin IQTeleportation = plugins.Find("IQTeleportation");
                if (IQTeleportation != null)
                {
                    try
                    {
                        object homeResult = IQTeleportation.Call("GetHomes", playerid);
                        if (homeResult is Dictionary<string, Vector3> iqHomes)
                        {
                            foreach (KeyValuePair<string, Vector3> home in iqHomes)
                            {
                                homes[home.Key] = home.Value;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        PrintError($"Ошибка при получении домов из IQTeleportation: {ex.Message}");
                    }
                }

                if (NTeleportation != null)
                {
                    if (
                        NTeleportation?.Call("API_GetHomes", player)
                        is Dictionary<string, Vector3> ntHomes
                    )
                    {
                        foreach (KeyValuePair<string, Vector3> home in ntHomes)
                        {
                            homes[home.Key] = home.Value;
                        }
                    }
                }

                if (Teleport != null)
                {
                    if (
                        Teleport?.Call("ApiGetHomes", playerid)
                        is Dictionary<string, Vector3> tpHomes
                    )
                    {
                        foreach (KeyValuePair<string, Vector3> home in tpHomes)
                        {
                            homes[home.Key] = home.Value;
                        }
                    }
                }

                if (Teleportation != null)
                {
                    if (
                        Teleportation?.Call("GetHomes", playerid)
                        is Dictionary<string, Vector3> telHomes
                    )
                    {
                        foreach (KeyValuePair<string, Vector3> home in telHomes)
                        {
                            homes[home.Key] = home.Value;
                        }
                    }
                }

                if (HomesGUI != null)
                {
                    if (
                        HomesGUI?.Call("GetPlayerHomes", playerid.ToString())
                        is Dictionary<string, Vector3> guiHomes
                    )
                    {
                        foreach (KeyValuePair<string, Vector3> home in guiHomes)
                        {
                            homes[home.Key] = home.Value;
                        }
                    }
                }

                homesCache[playerid] = homes;
                lastHomesUpdate[playerid] = DateTime.Now;
                return homes;
            }
            catch (Exception ex)
            {
                PrintError($"Error in GetHomes: {ex.Message}");
                return new Dictionary<string, Vector3>();
            }
        }

        private List<ulong> GetFriends(ulong playerid)
        {
            try
            {
                if (
                    friendsCache.TryGetValue(playerid, out List<ulong>? cachedFriends)
                    && lastFriendsUpdate.TryGetValue(playerid, out DateTime lastUpdate)
                    && (DateTime.Now - lastUpdate).TotalSeconds < CACHE_TIMEOUT
                )
                {
                    return cachedFriends;
                }

                List<ulong> result = new();

                if (Friends != null && Friends?.Call("GetFriends", playerid) is ulong[] friends)
                {
                    result.AddRange(friends);
                }

                friendsCache[playerid] = result;
                lastFriendsUpdate[playerid] = DateTime.Now;
                return result;
            }
            catch (Exception ex)
            {
                PrintError($"Error in GetFriends: {ex.Message}");
                return new List<ulong>();
            }
        }

        private bool IsPlayerOnline(ulong friendId)
        {
            BasePlayer player = BasePlayer.FindByID(friendId);
            return player?.IsConnected == true;
        }

        private string GetFriendName(ulong friendId)
        {
            try
            {
                BasePlayer player = BasePlayer.FindByID(friendId);
                if (player != null)
                {
                    return player.displayName.Length > 10
                        ? player.displayName.Substring(0, 7) + "*"
                        : player.displayName;
                }

                IPlayer offlinePlayer = covalence.Players.FindPlayerById(friendId.ToString());
                if (offlinePlayer != null)
                {
                    string name = offlinePlayer.Name;
                    return name.Length > 10 ? name.Substring(0, 7) + "*" : name;
                }

                if (Friends != null)
                {
                    if (
                        Friends?.Call("GetFriendData", friendId)
                            is Dictionary<string, object> friendData
                        && friendData.TryGetValue("name", out object? value)
                    )
                    {
                        string name = value.ToString();
                        return name.Length > 10 ? name.Substring(0, 7) + "*" : name;
                    }
                }

                return friendId.ToString();
            }
            catch (Exception ex)
            {
                PrintError($"Error in GetFriendName: {ex.Message}");
                return friendId.ToString();
            }
        }

        private bool IsTeamLeader(BasePlayer player)
        {
            RelationshipManager.PlayerTeam team =
                RelationshipManager.ServerInstance.FindPlayersTeam(player.userID);
            return team?.teamLeader == player.userID;
        }

        private string GetGridString(Vector3 position)
        {
            Vector2 adjPosition = new((World.Size / 2) + position.x, (World.Size / 2) - position.z);
            return $"{NumberToString((int)(adjPosition.x / 150))}{(int)(adjPosition.y / 150)}";
        }

        private string NumberToString(int number)
        {
            bool a = number > 26;
            char c = (char)(65 + (a ? number - 26 : number));
            return a ? "A" + c : c.ToString();
        }

        private int GetFreeHomesCount(BasePlayer player)
        {
            try
            {
                const int defaultMaxHomes = 5;

                if (config?.Home != null)
                {
                    int count = config.Home.MaxHomes;
                    if (count <= 0)
                    {
                        count = defaultMaxHomes;
                    }

                    if (config.Home.PermissionsHomes?.Count > 0)
                    {
                        foreach (KeyValuePair<string, int> perm in config.Home.PermissionsHomes)
                        {
                            if (permission.UserHasPermission(player.UserIDString, perm.Key))
                            {
                                count = perm.Value;
                                break;
                            }
                        }
                    }

                    return count;
                }
                else if (config?.Homes != null)
                {
                    int count = config.Homes.MaxHomes;
                    if (count <= 0)
                    {
                        count = defaultMaxHomes;
                    }

                    if (config.Homes.PermissionsHomes?.Count > 0)
                    {
                        foreach (KeyValuePair<string, int> perm in config.Homes.PermissionsHomes)
                        {
                            if (permission.UserHasPermission(player.UserIDString, perm.Key))
                            {
                                count = perm.Value;
                                break;
                            }
                        }
                    }

                    return count;
                }

                PrintWarning(
                    "Не удалось найти настройки для домов в конфигурации, используем значение по умолчанию"
                );
                return defaultMaxHomes;
            }
            catch (Exception ex)
            {
                PrintError($"Error in GetFreeHomesCount: {ex.Message}");
                return 5;
            }
        }

        private int GetCurrentPage(BasePlayer player)
        {
            List<KeyValuePair<string, Vector3>> homes = GetHomes(player.userID).ToList();
            return (int)Math.Ceiling(homes.Count / 5.0f);
        }

        private string GetImage(string url)
        {
            if (ImageLibrary == null || string.IsNullOrEmpty(url))
            {
                return string.Empty;
            }

            return imageCache.TryGetValue(url, out string cachedImage)
                ? cachedImage
                : (imageCache[url] = (string)ImageLibrary.Call("GetImage", url) ?? string.Empty);
        }

        private void cmdOpentpmenu(BasePlayer player, string command, string[] args)
        {
            try
            {
                if (!permission.UserHasPermission(player.UserIDString, "tpmenu.use"))
                {
                    player.ChatMessage("У вас нет разрешения на использование этой команды");
                    return;
                }

                DestroyUI(player);
                CreateMenu(player);
            }
            catch (Exception ex)
            {
                PrintError($"Error in cmdOpentpmenu: {ex.Message}\n{ex.StackTrace}");
            }
        }

        [ConsoleCommand("tpmenu")]
        private void cmdOpentpmenuConsole(ConsoleSystem.Arg arg)
        {
            try
            {
                BasePlayer player = arg.Player();
                if (player == null)
                {
                    return;
                }

                if (!permission.UserHasPermission(player.UserIDString, "tpmenu.use"))
                {
                    player.ChatMessage("У вас нет разрешения на использование этой команды");
                    return;
                }

                DestroyUI(player);
                CreateMenu(player);
            }
            catch (Exception ex)
            {
                PrintError($"Error in cmdOpentpmenuConsole: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void DestroyAllButtons(BasePlayer player)
        {
            for (int i = 0; i < 600; i++)
            {
                _ = CuiHelper.DestroyUi(player, $"{MainLayer}.ButtonOutline.{i}");
            }

            for (int i = 0; i < 10; i++)
            {
                _ = CuiHelper.DestroyUi(player, $"HomeRemove_{i}");
            }

            _ = CuiHelper.DestroyUi(player, "PrevPageButton");
            _ = CuiHelper.DestroyUi(player, "PageIndicator");
            _ = CuiHelper.DestroyUi(player, "NextPageButton");
        }

        [ConsoleCommand("tpmenu_UI")]
        private void cmdtpmenuCommands(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null)
            {
                return;
            }

            switch (args.GetString(0).ToLower(System.Globalization.CultureInfo.CurrentCulture))
            {
                case "close":
                    DestroyUI(player);
                    break;
                case "home":
                    if (args.Args.Length > 1)
                    {
                        string homeName = args.GetString(1);
                        player.Command("chat.say", $"/home {homeName}");
                        _ = timer.Once(0.1f, () => CreateMenu(player));
                    }
                    break;
                case "removehome":
                    if (args.Args.Length > 1)
                    {
                        if (IsActionInProgress(player.userID))
                        {
                            player.ChatMessage("Дождитесь завершения предыдущего действия");
                            return;
                        }

                        SetActionInProgress(player.userID, true);
                        string homeName = args.GetString(1);
                        player.Command("chat.say", $"/removehome {homeName}");

                        _ = homesCache.Remove(player.userID);
                        _ = lastHomesUpdate.Remove(player.userID);

                        _ = timer.Once(
                            0.1f,
                            () =>
                            {
                                CreateMenu(player);
                                SetActionInProgress(player.userID, false);
                            }
                        );
                    }
                    break;
                case "switchpage":
                    if (args.Args.Length > 1 && int.TryParse(args.GetString(1), out int page))
                    {
                        if (IsActionInProgress(player.userID))
                        {
                            return;
                        }

                        SetActionInProgress(player.userID, true);

                        try
                        {
                            Dictionary<string, Vector3> homes = GetHomes(player.userID);
                            int totalPages = (int)Math.Ceiling(homes.Count / 5.0f);
                            if (totalPages == 0)
                            {
                                totalPages = 1;
                            }

                            if (page < 1)
                            {
                                page = 1;
                            }

                            if (page > totalPages)
                            {
                                page = totalPages;
                            }

                            DestroyUI(player);
                            CreateMenu(player, page);
                        }
                        finally
                        {
                            SetActionInProgress(player.userID, false);
                        }
                    }
                    break;
                case "scroll":
                    if (args.Args.Length > 1)
                    {
                        DestroyUI(player);
                        CreateMenu(player);
                    }
                    break;
                case "sethome":
                    if (args.Args.Length > 1)
                    {
                        if (IsActionInProgress(player.userID))
                        {
                            player.ChatMessage("Дождитесь завершения предыдущего действия");
                            return;
                        }

                        SetActionInProgress(player.userID, true);
                        string gridPos = args.GetString(1);
                        string homeName = gridPos.Length > 7 ? gridPos.Substring(0, 7) : gridPos;

                        player.Command("chat.say", $"/sethome {homeName}");

                        _ = homesCache.Remove(player.userID);
                        _ = lastHomesUpdate.Remove(player.userID);

                        _ = timer.Once(
                            0.1f,
                            () =>
                            {
                                CreateMenu(player);
                                SetActionInProgress(player.userID, false);
                            }
                        );
                    }
                    break;
                case "friend":
                    if (
                        args.Args.Length > 1
                        && ulong.TryParse(args.GetString(1), out ulong friendId)
                    )
                    {
                        DestroyUI(player);
                        player.Command("chat.say", $"/tpr {friendId}");
                    }
                    break;
                case "addfriend":
                    DestroyUI(player);
                    if (Friends != null)
                    {
                        _ = Friends.Call("ShowFriends", player);

                        ResetCache(player.userID);
                    }
                    else
                    {
                        player.ChatMessage("Плагин Friends не найден!");
                    }
                    break;
                case "removefriend":
                    if (
                        args.Player() != null
                        && ulong.TryParse(args.GetString(1), out ulong friendToRemove)
                    )
                    {
                        _ = Friends.Call("fremove", friendToRemove);
                        ResetCache(args.Player().userID);
                        ResetCache(friendToRemove);
                        UpdateFriendsPanel(args.Player());
                    }
                    break;
            }
        }

        private void UpdateHomePanel(BasePlayer player, int page)
        {
            try
            {
                if (config.Home == null)
                {
                    PrintError("Home settings are null! Initializing default settings.");
                    config.Home = new HomeSettings();
                    SaveConfig();
                }

                CuiElementContainer container = new();

                const string homePanel = MainLayer + ".Home";

                _ = container.Add(
                    new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Text =
                        {
                            Text = $"<color=#ff8c08>{config.Home.Title}</color>",
                            FontSize = 20,
                            Align = TextAnchor.MiddleCenter,
                            Font = BOLD_FONT_NAME,
                        },
                    },
                    homePanel + ".Title"
                );

                List<KeyValuePair<string, Vector3>> homes = GetHomes(player.userID).ToList();
                int maxHomes = GetFreeHomesCount(player);

                int totalPages = (int)Math.Ceiling(homes.Count / 5.0f);
                if (totalPages == 0)
                {
                    totalPages = 1;
                }

                if (page > totalPages)
                {
                    page = totalPages;
                }

                List<KeyValuePair<string, Vector3>> pageHomes = homes
                    .Skip((page - 1) * 5)
                    .Take(5)
                    .ToList();

                const float startButtonY = 0.635f;
                const float homeButtonX = 0.377f;

                for (int i = 0; i < Math.Min(pageHomes.Count, 5); i++)
                {
                    KeyValuePair<string, Vector3> home = pageHomes[i];
                    float buttonY = startButtonY - (i * (BUTTON_HEIGHT + BUTTON_SPACING));

                    ButtonConfig buttonConfig = new()
                    {
                        Command = $"tpmenu_UI home {home.Key}",
                        ButtonColor = "0.19 0.19 0.21 0.75",
                        TextColor = "1 1 1 1",
                        ButtonText = new Dictionary<string, string>
                        {
                            { "ru", $"<color=#ff8c08>{home.Key}</color>" },
                        },
                    };

                    CreateButton(container, buttonConfig, homeButtonX, buttonY, 100 + i, player);

                    string removeButtonId = $"HomeRemove_{i}";
                    _ = container.Add(
                        new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin =
                                    $"{homeButtonX + BUTTON_WIDTH - 0.015f} {buttonY + 0.005f}",
                                AnchorMax =
                                    $"{homeButtonX + BUTTON_WIDTH - 0.002f} {buttonY + BUTTON_HEIGHT - 0.005f}",
                            },
                            Button =
                            {
                                Color = "0.8 0 0 0.7",
                                Command = $"tpmenu_UI removehome {home.Key}",
                            },
                            Text =
                            {
                                Text = "×",
                                FontSize = FONT_SIZE + 4,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 1",
                                Font = BOLD_FONT_NAME,
                            },
                        },
                        MainLayer,
                        removeButtonId
                    );
                }

                int buttonCount = Math.Min(pageHomes.Count, 5);

                bool showNavigationButtons = homes.Count > 5;

                if (page == 1 && homes.Count > 5)
                {
                    const float buttonY = startButtonY - (5 * (BUTTON_HEIGHT + BUTTON_SPACING));
                    const float smallButtonWidth = BUTTON_WIDTH / 4;

                    const float textWidth = BUTTON_WIDTH - (2 * smallButtonWidth);
                    const float textX = homeButtonX + smallButtonWidth;
                    _ = container.Add(
                        new CuiLabel
                        {
                            RectTransform =
                            {
                                AnchorMin = $"{textX} {buttonY}",
                                AnchorMax = $"{textX + textWidth} {buttonY + BUTTON_HEIGHT}",
                            },
                            Text =
                            {
                                Text = "<color=#FFFFFF>1/2</color>",
                                FontSize = FONT_SIZE,
                                Align = TextAnchor.MiddleCenter,
                                Font = BOLD_FONT_NAME,
                            },
                        },
                        MainLayer,
                        "PageIndicator"
                    );

                    const float rightButtonX = textX + textWidth;
                    _ = container.Add(
                        new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = $"{rightButtonX} {buttonY}",
                                AnchorMax =
                                    $"{rightButtonX + smallButtonWidth} {buttonY + BUTTON_HEIGHT}",
                            },
                            Button =
                            {
                                Color = "0.19 0.19 0.21 0.75",
                                Command = "tpmenu_UI switchpage 2",
                            },
                            Text =
                            {
                                Text = "►",
                                FontSize = FONT_SIZE,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 0.8",
                                Font = BOLD_FONT_NAME,
                            },
                        },
                        MainLayer,
                        "NextPageButton"
                    );
                }
                else if (page > 1)
                {
                    const float buttonY = startButtonY - (5 * (BUTTON_HEIGHT + BUTTON_SPACING));
                    const float smallButtonWidth = BUTTON_WIDTH / 4;

                    _ = container.Add(
                        new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = $"{homeButtonX} {buttonY}",
                                AnchorMax =
                                    $"{homeButtonX + smallButtonWidth} {buttonY + BUTTON_HEIGHT}",
                            },
                            Button =
                            {
                                Color = "0.19 0.19 0.21 0.75",
                                Command = "tpmenu_UI switchpage 1",
                            },
                            Text =
                            {
                                Text = "◄",
                                FontSize = FONT_SIZE,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 0.8",
                                Font = BOLD_FONT_NAME,
                            },
                        },
                        MainLayer,
                        "PrevPageButton"
                    );

                    const float textWidth = BUTTON_WIDTH - (2 * smallButtonWidth);
                    const float textX = homeButtonX + smallButtonWidth;
                    _ = container.Add(
                        new CuiLabel
                        {
                            RectTransform =
                            {
                                AnchorMin = $"{textX} {buttonY}",
                                AnchorMax = $"{textX + textWidth} {buttonY + BUTTON_HEIGHT}",
                            },
                            Text =
                            {
                                Text = "<color=#FFFFFF>2/2</color>",
                                FontSize = FONT_SIZE,
                                Align = TextAnchor.MiddleCenter,
                                Font = BOLD_FONT_NAME,
                            },
                        },
                        MainLayer,
                        "PageIndicator"
                    );
                }

                if (homes.Count < maxHomes)
                {
                    if (page == 1 && homes.Count <= 5)
                    {
                        float buttonY =
                            startButtonY - (buttonCount * (BUTTON_HEIGHT + BUTTON_SPACING));

                        ButtonConfig addButtonConfig = new()
                        {
                            Command =
                                $"tpmenu_UI sethome {GetGridString(player.transform.position)}",
                            ButtonColor = "0.19 0.19 0.21 0.75",
                            TextColor = "1 1 1 1",
                            ButtonText = new Dictionary<string, string>
                            {
                                { "ru", "<color=#00FF00>ДОБАВИТЬ</color>" },
                            },
                        };

                        CreateButton(container, addButtonConfig, homeButtonX, buttonY, 200, player);
                    }
                    else if (page > 1 && pageHomes.Count < 5)
                    {
                        float buttonY =
                            startButtonY - (pageHomes.Count * (BUTTON_HEIGHT + BUTTON_SPACING));

                        ButtonConfig addButtonConfig = new()
                        {
                            Command =
                                $"tpmenu_UI sethome {GetGridString(player.transform.position)}",
                            ButtonColor = "0.19 0.19 0.21 1",
                            TextColor = "1 1 1 1",
                            ButtonText = new Dictionary<string, string>
                            {
                                { "ru", "<color=#00FF00>ДОБАВИТЬ</color>" },
                            },
                        };

                        CreateButton(container, addButtonConfig, homeButtonX, buttonY, 200, player);
                    }
                }

                _ = container.Add(
                    new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0.3", AnchorMax = "1 0.35" },
                        Text =
                        {
                            Text = "<color=#FFFFFF>АВТОЗАКРЫТИЕ</color>",
                            FontSize = 16,
                            Align = TextAnchor.MiddleCenter,
                            Font = BOLD_FONT_NAME,
                        },
                    },
                    homePanel
                );

                const float autodoorButtonY =
                    startButtonY - (6 * (BUTTON_HEIGHT + BUTTON_SPACING)) - 0.02f;

                ButtonConfig autodoorButtonConfig = new()
                {
                    Command = "chat.say /ad",
                    ButtonColor = "0.19 0.19 0.21 0.75",
                    TextColor = "1 1 1 1",
                    ButtonText = new Dictionary<string, string>
                    {
                        { "ru", "<color=#FFFFFF>АВТОЗАКРЫТИЕ</color>" },
                    },
                };

                CreateButton(
                    container,
                    autodoorButtonConfig,
                    homeButtonX,
                    autodoorButtonY,
                    250,
                    player
                );

                _ = CuiHelper.AddUi(player, container);
            }
            catch (Exception ex)
            {
                PrintError($"Error in UpdateHomePanel: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void UpdateFriendsPanel(BasePlayer player)
        {
            try
            {
                _ = CuiHelper.DestroyUi(player, "FriendsPanel");

                DestroyAllButtons(player);

                CuiElementContainer container = new();
                CreateFriendsPanel(container, player);
                _ = CuiHelper.AddUi(player, container);
            }
            catch (Exception ex)
            {
                PrintError($"Error in UpdateFriendsPanel: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void DestroyUI(BasePlayer player)
        {
            try
            {
                _ = CuiHelper.DestroyUi(player, MainLayer);
            }
            catch (Exception ex)
            {
                PrintError($"Error in DestroyUI: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void Unload()
        {
            foreach (BasePlayer? player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
            }
        }

        private void CreateMenu(BasePlayer player, int page = 1)
        {
            try
            {
                DestroyUI(player);
                CuiElementContainer container = new();

                _ = container.Add(
                    new CuiPanel
                    {
                        CursorEnabled = true,
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Image = { Color = "0 0 0 0" },
                    },
                    "Overlay",
                    MainLayer
                );

                _ = container.Add(
                    new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Button = { Color = "0 0 0 0", Command = "tpmenu_UI close" },
                        Text = { Text = "" },
                    },
                    MainLayer,
                    MainLayer + ".Background"
                );

                CreatePanels(container, player, TITLE_FONT_SIZE);

                CreateButtons(container, player, page);

                _ = CuiHelper.AddUi(player, container);
            }
            catch (Exception ex)
            {
                PrintError($"Error in CreateMenu: {ex.Message}");
            }
        }

        private void CreatePanels(
            CuiElementContainer container,
            BasePlayer player,
            int titleFontSize
        )
        {
            CreatePanel(
                container,
                "TeleportPanel",
                $"{LEFT_PANEL_X} {TOP_PANEL_Y - PANEL_HEIGHT}",
                $"{LEFT_PANEL_X + PANEL_WIDTH} {TOP_PANEL_Y}",
                "<color=#ff8c08>ТЕЛЕПОРТ</color>",
                titleFontSize
            );

            CreatePanel(
                container,
                "MenuPanel",
                $"{CENTER_PANEL_X} {TOP_PANEL_Y - PANEL_HEIGHT}",
                $"{CENTER_PANEL_X + PANEL_WIDTH} {TOP_PANEL_Y}",
                "",
                titleFontSize
            );

            _ = container.Add(
                new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = $"{CENTER_PANEL_X} {TOP_PANEL_Y - PANEL_HEIGHT}",
                        AnchorMax = $"{RIGHT_PANEL_X} {TOP_PANEL_Y}",
                    },
                    Image = { Color = "0 0 0 0" },
                },
                MainLayer,
                "MenuTitlePanel"
            );

            _ = container.Add(
                new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text =
                    {
                        Text = "<color=#ff8c08>МЕНЮ</color>",
                        FontSize = titleFontSize,
                        Align = TextAnchor.MiddleCenter,
                        Font = BOLD_FONT_NAME,
                    },
                },
                "MenuTitlePanel"
            );

            CreatePanel(
                container,
                "TradePanel",
                $"{RIGHT_PANEL_X} {TOP_PANEL_Y - PANEL_HEIGHT}",
                $"{RIGHT_PANEL_X + PANEL_WIDTH} {TOP_PANEL_Y}",
                "<color=#ff8c08>ОБМЕН</color>",
                titleFontSize
            );

            CreateTeleportButtons(container, player);
            CreateTradeButtons(container, player);

            _ = container.Add(
                new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = $"{CENTER_PANEL_X - 0.02} 0.37",
                        AnchorMax = $"{RIGHT_PANEL_X + 0.02} 0.41",
                    },
                    Text =
                    {
                        Text = "<color=#ff8c08>Frenetic</color> Rust",
                        FontSize = titleFontSize,
                        Align = TextAnchor.MiddleCenter,
                        Font = BOLD_FONT_NAME,
                    },
                },
                MainLayer
            );

            _ = container.Add(
                new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = $"{CENTER_PANEL_X - 0.02} 0.34",
                        AnchorMax = $"{RIGHT_PANEL_X + 0.02} 0.38",
                    },
                    Text =
                    {
                        Text = "<color=#ff8c08>Max</color> 2",
                        FontSize = titleFontSize,
                        Align = TextAnchor.MiddleCenter,
                        Font = BOLD_FONT_NAME,
                    },
                },
                MainLayer
            );
        }

        private float GetInterfaceScale(BasePlayer player)
        {
            return player == null
                ? MIN_INTERFACE_SCALE
                : (MIN_INTERFACE_SCALE + MAX_INTERFACE_SCALE) / 2;
        }

        private int CalculateFontSize(float interfaceScale)
        {
            return Mathf.RoundToInt(BASE_FONT_SIZE * interfaceScale);
        }

        private void CreateTeleportButtons(CuiElementContainer container, BasePlayer player)
        {
            CreateButtonWithOutline(
                container,
                "AcceptTeleport",
                "0.377 0.775",
                "0.452 0.815",
                "ПРИНЯТЬ",
                "chat.say /tpa",
                player
            );
            CreateButtonWithOutline(
                container,
                "CancelTeleport",
                "0.377 0.73",
                "0.452 0.77",
                "ОТМЕНИТЬ",
                "chat.say /tpc",
                player
            );
        }

        private void CreateTradeButtons(CuiElementContainer container, BasePlayer player)
        {
            CreateButtonWithOutline(
                container,
                "AcceptTrade",
                "0.547 0.775",
                "0.622 0.815",
                "ПРИНЯТЬ",
                "trade.accept",
                player
            );
            CreateButtonWithOutline(
                container,
                "CancelTrade",
                "0.547 0.73",
                "0.622 0.77",
                "ОТМЕНИТЬ",
                "trade.cancel",
                player
            );
        }

        private void CreateButtonWithOutline(
            CuiElementContainer container,
            string name,
            string min,
            string max,
            string text,
            string command,
            BasePlayer player
        )
        {
            float interfaceScale = GetInterfaceScale(player);
            int fontSize = CalculateFontSize(interfaceScale);

            string outlineName = MainLayer + $".{name}Outline";
            _ = container.Add(
                new CuiPanel
                {
                    RectTransform = { AnchorMin = min, AnchorMax = max },
                    Image = { Color = "0.09 0.09 0.11 0" },
                },
                MainLayer,
                outlineName
            );

            CreateButtonOutline(container, outlineName);

            _ = container.Add(
                new CuiButton
                {
                    RectTransform = { AnchorMin = min, AnchorMax = max },
                    Button = { Color = "0.19 0.19 0.21 0.75", Command = command },
                    Text =
                    {
                        Text = text,
                        FontSize = fontSize,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1",
                        Font = BOLD_FONT_NAME,
                    },
                },
                MainLayer
            );
        }

        private void CreateButtons(CuiElementContainer container, BasePlayer player, int page)
        {
            if (config?.MainSettings?.ButtonSettings?.Buttons == null)
            {
                return;
            }

            float interfaceScale = GetInterfaceScale(player);
            float buttonHeight = GetScaledButtonHeight(interfaceScale);
            float buttonSpacing = GetScaledButtonSpacing(interfaceScale);
            _ = GetScaledButtonWidth(interfaceScale);
            _ = CalculateFontSize(interfaceScale);

            _ = container.Add(
                new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.453 0.410", AnchorMax = "0.546 0.815" },
                    Image = { Color = "0.19 0.19 0.21 0" },
                },
                MainLayer,
                "ButtonPanel"
            );

            float totalContentHeight =
                config.MainSettings.ButtonSettings.Buttons.Count * (buttonHeight + buttonSpacing);
            float offsetMin = -Math.Max(totalContentHeight * 1000, 750);

            container.Add(
                new CuiElement
                {
                    Parent = "ButtonPanel",
                    Name = "ScrollRect",
                    Components =
                    {
                        new CuiScrollViewComponent
                        {
                            Horizontal = false,
                            Vertical = true,
                            MovementType = UnityEngine.UI.ScrollRect.MovementType.Elastic,
                            ScrollSensitivity = 24,
                            Inertia = true,
                            DecelerationRate = 0.24f,
                            ContentTransform = new CuiRectTransform
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "1 1",
                                OffsetMin = $"0 {offsetMin}",
                                OffsetMax = "0 0",
                            },
                        },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                        new CuiImageComponent { Color = "0 0 0 0" },
                    },
                }
            );

            int visibleButtonCount = Math.Min(config.MainSettings.ButtonSettings.Buttons.Count, 10);

            for (int i = 0; i < visibleButtonCount; i++)
            {
                MenuButton button = config.MainSettings.ButtonSettings.Buttons[i];
                float offsetY = i * (buttonHeight + buttonSpacing) * 1000;

                string buttonPanelName = $"ScrollRect.Button_{i}";
                CreateScrollButton(container, button, offsetY, buttonPanelName, player);
            }

            if (page > 0)
            {
                CreateHomePanel(container, player, page);
            }

            CreateFriendsPanel(container, player);
        }

        private void CreateScrollButton(
            CuiElementContainer container,
            MenuButton button,
            float offsetY,
            string buttonPanelName,
            BasePlayer player
        )
        {
            float interfaceScale = GetInterfaceScale(player);
            float buttonHeight = GetScaledButtonHeight(interfaceScale) * 1000;
            int fontSize = CalculateFontSize(interfaceScale);

            _ = container.Add(
                new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.07 1",
                        AnchorMax = "0.93 1",
                        OffsetMin = $"5 {-offsetY - buttonHeight}",
                        OffsetMax = $"-5 {-offsetY}",
                    },
                    Image = { Color = config.MainSettings.ButtonSettings.ButtonColor },
                },
                "ScrollRect",
                buttonPanelName
            );

            string outlineColor = $"1 0.55 0.03 {OUTLINE_COLOR}";

            _ = container.Add(
                new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0.98", AnchorMax = "1 1" },
                    Image = { Color = outlineColor },
                },
                buttonPanelName
            );

            _ = container.Add(
                new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.02" },
                    Image = { Color = outlineColor },
                },
                buttonPanelName
            );

            _ = container.Add(
                new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.02 1" },
                    Image = { Color = outlineColor },
                },
                buttonPanelName
            );

            _ = container.Add(
                new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.98 0", AnchorMax = "1 1" },
                    Image = { Color = outlineColor },
                },
                buttonPanelName
            );

            _ = container.Add(
                new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Color = "0 0 0 0", Command = button.Command },
                    Text =
                    {
                        Text = button.Text,
                        FontSize = fontSize,
                        Align = TextAnchor.MiddleCenter,
                        Color = config.MainSettings.ButtonSettings.TextColor,
                        Font = BOLD_FONT_NAME,
                    },
                },
                buttonPanelName
            );
        }

        private void CreateButtonOutline(CuiElementContainer container, string outlinePanelName)
        {
            string outlineColor = $"1 0.55 0.03 {OUTLINE_COLOR}";
            _ = container.Add(
                new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0.98", AnchorMax = "1 1" },
                    Image = { Color = outlineColor },
                },
                outlinePanelName
            );

            _ = container.Add(
                new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.02" },
                    Image = { Color = outlineColor },
                },
                outlinePanelName
            );

            _ = container.Add(
                new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.02 1" },
                    Image = { Color = outlineColor },
                },
                outlinePanelName
            );

            _ = container.Add(
                new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.98 0", AnchorMax = "1 1" },
                    Image = { Color = outlineColor },
                },
                outlinePanelName
            );
        }

        private void CreateHomePanel(CuiElementContainer container, BasePlayer player, int page)
        {
            try
            {
                if (config.Home == null)
                {
                    PrintError("Home settings are null! Initializing default settings.");
                    config.Home = new HomeSettings();
                    SaveConfig();
                }

                const string homePanel = MainLayer + ".Home";
                _ = container.Add(
                    new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0.377 0.515", AnchorMax = "0.452 0.715" },
                        Image = { Color = "0.12 0.12 0.12 0" },
                    },
                    MainLayer,
                    homePanel
                );

                _ = container.Add(
                    new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0.377 0.675", AnchorMax = "0.452 0.725" },
                        Image = { Color = "0.12 0.12 0.12 0" },
                    },
                    MainLayer,
                    homePanel + ".Title"
                );

                _ = container.Add(
                    new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Text =
                        {
                            Text = $"<color=#ff8c08>{config.Home.Title}</color>",
                            FontSize = 20,
                            Align = TextAnchor.MiddleCenter,
                            Font = BOLD_FONT_NAME,
                        },
                    },
                    homePanel + ".Title"
                );

                List<KeyValuePair<string, Vector3>> homes = GetHomes(player.userID).ToList();
                int maxHomes = GetFreeHomesCount(player);

                int totalPages = (int)Math.Ceiling(homes.Count / 5.0f);
                if (totalPages == 0)
                {
                    totalPages = 1;
                }

                if (page > totalPages)
                {
                    page = totalPages;
                }

                List<KeyValuePair<string, Vector3>> pageHomes = homes
                    .Skip((page - 1) * 5)
                    .Take(5)
                    .ToList();

                const float startButtonY = 0.635f;
                const float homeButtonX = 0.377f;

                for (int i = 0; i < Math.Min(pageHomes.Count, 5); i++)
                {
                    KeyValuePair<string, Vector3> home = pageHomes[i];
                    float buttonY = startButtonY - (i * (BUTTON_HEIGHT + BUTTON_SPACING));

                    ButtonConfig buttonConfig = new()
                    {
                        Command = $"tpmenu_UI home {home.Key}",
                        ButtonColor = "0.19 0.19 0.21 0.75",
                        TextColor = "1 1 1 1",
                        ButtonText = new Dictionary<string, string>
                        {
                            { "ru", $"<color=#ff8c08>{home.Key}</color>" },
                        },
                    };

                    CreateButton(container, buttonConfig, homeButtonX, buttonY, 100 + i, player);

                    string removeButtonId = $"HomeRemove_{i}";
                    _ = container.Add(
                        new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin =
                                    $"{homeButtonX + BUTTON_WIDTH - 0.015f} {buttonY + 0.005f}",
                                AnchorMax =
                                    $"{homeButtonX + BUTTON_WIDTH - 0.002f} {buttonY + BUTTON_HEIGHT - 0.005f}",
                            },
                            Button =
                            {
                                Color = "0.8 0 0 0.7",
                                Command = $"tpmenu_UI removehome {home.Key}",
                            },
                            Text =
                            {
                                Text = "×",
                                FontSize = FONT_SIZE + 4,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 1",
                                Font = BOLD_FONT_NAME,
                            },
                        },
                        MainLayer,
                        removeButtonId
                    );
                }

                int buttonCount = Math.Min(pageHomes.Count, 5);

                bool showNavigationButtons = homes.Count > 5;

                if (page == 1 && homes.Count > 5)
                {
                    const float buttonY = startButtonY - (5 * (BUTTON_HEIGHT + BUTTON_SPACING));
                    const float smallButtonWidth = BUTTON_WIDTH / 4;

                    const float textWidth = BUTTON_WIDTH - (2 * smallButtonWidth);
                    const float textX = homeButtonX + smallButtonWidth;
                    _ = container.Add(
                        new CuiLabel
                        {
                            RectTransform =
                            {
                                AnchorMin = $"{textX} {buttonY}",
                                AnchorMax = $"{textX + textWidth} {buttonY + BUTTON_HEIGHT}",
                            },
                            Text =
                            {
                                Text = "<color=#FFFFFF>1/2</color>",
                                FontSize = FONT_SIZE,
                                Align = TextAnchor.MiddleCenter,
                                Font = BOLD_FONT_NAME,
                            },
                        },
                        MainLayer,
                        "PageIndicator"
                    );

                    const float rightButtonX = textX + textWidth;
                    _ = container.Add(
                        new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = $"{rightButtonX} {buttonY}",
                                AnchorMax =
                                    $"{rightButtonX + smallButtonWidth} {buttonY + BUTTON_HEIGHT}",
                            },
                            Button =
                            {
                                Color = "0.19 0.19 0.21 0.75",
                                Command = "tpmenu_UI switchpage 2",
                            },
                            Text =
                            {
                                Text = "►",
                                FontSize = FONT_SIZE,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 0.8",
                                Font = BOLD_FONT_NAME,
                            },
                        },
                        MainLayer,
                        "NextPageButton"
                    );
                }
                else if (page > 1)
                {
                    const float buttonY = startButtonY - (5 * (BUTTON_HEIGHT + BUTTON_SPACING));
                    const float smallButtonWidth = BUTTON_WIDTH / 4;

                    _ = container.Add(
                        new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = $"{homeButtonX} {buttonY}",
                                AnchorMax =
                                    $"{homeButtonX + smallButtonWidth} {buttonY + BUTTON_HEIGHT}",
                            },
                            Button =
                            {
                                Color = "0.19 0.19 0.21 0.75",
                                Command = "tpmenu_UI switchpage 1",
                            },
                            Text =
                            {
                                Text = "◄",
                                FontSize = FONT_SIZE,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 0.8",
                                Font = BOLD_FONT_NAME,
                            },
                        },
                        MainLayer,
                        "PrevPageButton"
                    );

                    const float textWidth = BUTTON_WIDTH - (2 * smallButtonWidth);
                    const float textX = homeButtonX + smallButtonWidth;
                    _ = container.Add(
                        new CuiLabel
                        {
                            RectTransform =
                            {
                                AnchorMin = $"{textX} {buttonY}",
                                AnchorMax = $"{textX + textWidth} {buttonY + BUTTON_HEIGHT}",
                            },
                            Text =
                            {
                                Text = "<color=#FFFFFF>2/2</color>",
                                FontSize = FONT_SIZE,
                                Align = TextAnchor.MiddleCenter,
                                Font = BOLD_FONT_NAME,
                            },
                        },
                        MainLayer,
                        "PageIndicator"
                    );
                }

                if (homes.Count < maxHomes)
                {
                    if (page == 1 && homes.Count <= 5)
                    {
                        float buttonY =
                            startButtonY - (buttonCount * (BUTTON_HEIGHT + BUTTON_SPACING));

                        ButtonConfig addButtonConfig = new()
                        {
                            Command =
                                $"tpmenu_UI sethome {GetGridString(player.transform.position)}",
                            ButtonColor = "0.19 0.19 0.21 0.75",
                            TextColor = "1 1 1 1",
                            ButtonText = new Dictionary<string, string>
                            {
                                { "ru", "<color=#00FF00>ДОБАВИТЬ</color>" },
                            },
                        };

                        CreateButton(container, addButtonConfig, homeButtonX, buttonY, 200, player);
                    }
                    else if (page > 1 && pageHomes.Count < 5)
                    {
                        float buttonY =
                            startButtonY - (pageHomes.Count * (BUTTON_HEIGHT + BUTTON_SPACING));

                        ButtonConfig addButtonConfig = new()
                        {
                            Command =
                                $"tpmenu_UI sethome {GetGridString(player.transform.position)}",
                            ButtonColor = "0.19 0.19 0.21 1",
                            TextColor = "1 1 1 1",
                            ButtonText = new Dictionary<string, string>
                            {
                                { "ru", "<color=#00FF00>ДОБАВИТЬ</color>" },
                            },
                        };

                        CreateButton(container, addButtonConfig, homeButtonX, buttonY, 200, player);
                    }
                }

                _ = container.Add(
                    new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0.3", AnchorMax = "1 0.35" },
                        Text =
                        {
                            Text = "<color=#FFFFFF>АВТОЗАКРЫТИЕ</color>",
                            FontSize = 16,
                            Align = TextAnchor.MiddleCenter,
                            Font = BOLD_FONT_NAME,
                        },
                    },
                    homePanel
                );

                const float autodoorButtonY =
                    startButtonY - (6 * (BUTTON_HEIGHT + BUTTON_SPACING)) - 0.02f;

                ButtonConfig autodoorButtonConfig = new()
                {
                    Command = "chat.say /ad",
                    ButtonColor = "0.19 0.19 0.21 0.75",
                    TextColor = "1 1 1 1",
                    ButtonText = new Dictionary<string, string>
                    {
                        { "ru", "<color=#FFFFFF>АВТОЗАКРЫТИЕ</color>" },
                    },
                };

                CreateButton(
                    container,
                    autodoorButtonConfig,
                    homeButtonX,
                    autodoorButtonY,
                    250,
                    player
                );
            }
            catch (Exception ex)
            {
                PrintError($"Error in CreateHomePanel: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void CreateFriendsPanel(CuiElementContainer container, BasePlayer player)
        {
            try
            {
                List<ulong> friends = GetFriends(player.userID);
                List<ulong> onlineFriends = friends.Where(IsPlayerOnline).ToList();
                List<ulong> offlineFriends = friends.Where(f => !IsPlayerOnline(f)).ToList();

                _ = container.Add(
                    new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0.547 0.515", AnchorMax = "0.622 0.715" },
                        Image = { Color = "0.12 0.12 0.12 0" },
                    },
                    MainLayer,
                    "FriendsPanel"
                );

                _ = container.Add(
                    new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 0.85", AnchorMax = "1 1" },
                        Image = { Color = "0.12 0.12 0.12 0" },
                    },
                    "FriendsPanel",
                    "FriendsPanel.Header"
                );

                _ = container.Add(
                    new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Text =
                        {
                            Text = "<color=#ff8c08>ДРУЗЬЯ</color>",
                            FontSize = 20,
                            Align = TextAnchor.MiddleCenter,
                            Font = BOLD_FONT_NAME,
                        },
                    },
                    "FriendsPanel.Header"
                );

                const float startButtonY = 0.635f;
                const float friendsButtonX = 0.547f;

                ButtonConfig addFriendButtonConfig = new()
                {
                    Command = "chat.say /fmenu",
                    ButtonColor = "0.19 0.19 0.21 0.75",
                    TextColor = "1 1 1 1",
                    ButtonText = new Dictionary<string, string>
                    {
                        { "ru", "<color=#00FF00>ДОБАВИТЬ</color>" },
                    },
                };

                CreateButton(
                    container,
                    addFriendButtonConfig,
                    friendsButtonX,
                    startButtonY,
                    300,
                    player
                );

                const int maxDisplayedFriends = 5;
                int displayedOnlineFriends = Math.Min(onlineFriends.Count, maxDisplayedFriends);
                int remainingSlots = maxDisplayedFriends - displayedOnlineFriends;
                int displayedOfflineFriends = Math.Min(offlineFriends.Count, remainingSlots);

                for (int i = 0; i < displayedOnlineFriends; i++)
                {
                    ulong friendId = onlineFriends[i];
                    string friendName = GetFriendName(friendId);
                    float buttonY = startButtonY - ((i + 1) * (BUTTON_HEIGHT + BUTTON_SPACING));

                    ButtonConfig friendButtonConfig = new()
                    {
                        Command = $"tpmenu_UI friend {friendId}",
                        ButtonColor = "0.19 0.19 0.21 0.75",
                        TextColor = "1 1 1 1",
                        ButtonText = new Dictionary<string, string>
                        {
                            { "ru", $"<color=#00FF00>{friendName}</color>" },
                        },
                    };

                    CreateButton(
                        container,
                        friendButtonConfig,
                        friendsButtonX,
                        buttonY,
                        400 + i,
                        player
                    );

                    _ = container.Add(
                        new CuiLabel
                        {
                            RectTransform =
                            {
                                AnchorMin = $"0.552 {buttonY}",
                                AnchorMax = $"0.562 {buttonY + BUTTON_HEIGHT}",
                            },
                            Text =
                            {
                                Text = "<color=#38C738>●</color>",
                                FontSize = 12,
                                Align = TextAnchor.MiddleLeft,
                                Font = BOLD_FONT_NAME,
                            },
                        },
                        MainLayer
                    );

                    string removeButtonId = $"FriendRemove_{i}";
                    _ = container.Add(
                        new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin =
                                    $"{friendsButtonX + BUTTON_WIDTH - 0.015f} {buttonY + 0.005f}",
                                AnchorMax =
                                    $"{friendsButtonX + BUTTON_WIDTH - 0.002f} {buttonY + BUTTON_HEIGHT - 0.005f}",
                            },
                            Button = { Color = "0.8 0 0 0.7", Command = $"fremove {friendId}" },
                            Text =
                            {
                                Text = "×",
                                FontSize = FONT_SIZE + 4,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 1",
                                Font = BOLD_FONT_NAME,
                            },
                        },
                        MainLayer,
                        removeButtonId
                    );

                    string tradeButtonId = $"FriendTrade_{i}";
                    _ = container.Add(
                        new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin =
                                    $"{friendsButtonX + BUTTON_WIDTH - 0.028f} {buttonY + 0.005f}",
                                AnchorMax =
                                    $"{friendsButtonX + BUTTON_WIDTH - 0.015f} {buttonY + BUTTON_HEIGHT - 0.005f}",
                            },
                            Button = { Color = "0.0 0.5 0.0 0.7", Command = $"trade {friendId}" },
                            Text =
                            {
                                Text = "⇄",
                                FontSize = FONT_SIZE + 2,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 1",
                                Font = BOLD_FONT_NAME,
                            },
                        },
                        MainLayer,
                        tradeButtonId
                    );
                }

                for (int i = 0; i < displayedOfflineFriends; i++)
                {
                    ulong friendId = offlineFriends[i];
                    string friendName = GetFriendName(friendId);
                    float buttonY =
                        startButtonY
                        - ((i + displayedOnlineFriends + 1) * (BUTTON_HEIGHT + BUTTON_SPACING));

                    ButtonConfig friendButtonConfig = new()
                    {
                        Command = $"tpmenu_UI friend {friendId}",
                        ButtonColor = "0.19 0.19 0.21 0.75",
                        TextColor = "1 1 1 1",
                        ButtonText = new Dictionary<string, string>
                        {
                            { "ru", $"<color=#FF0000>{friendName}</color>" },
                        },
                    };

                    CreateButton(
                        container,
                        friendButtonConfig,
                        friendsButtonX,
                        buttonY,
                        500 + i,
                        player
                    );

                    _ = container.Add(
                        new CuiLabel
                        {
                            RectTransform =
                            {
                                AnchorMin = $"0.552 {buttonY}",
                                AnchorMax = $"0.562 {buttonY + BUTTON_HEIGHT}",
                            },
                            Text =
                            {
                                Text = "<color=#C73838>●</color>",
                                FontSize = 12,
                                Align = TextAnchor.MiddleLeft,
                                Font = BOLD_FONT_NAME,
                            },
                        },
                        MainLayer
                    );

                    string removeButtonId = $"FriendRemove_offline_{i}";
                    _ = container.Add(
                        new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin =
                                    $"{friendsButtonX + BUTTON_WIDTH - 0.015f} {buttonY + 0.005f}",
                                AnchorMax =
                                    $"{friendsButtonX + BUTTON_WIDTH - 0.002f} {buttonY + BUTTON_HEIGHT - 0.005f}",
                            },
                            Button = { Color = "0.8 0 0 0.7", Command = $"fremove {friendId}" },
                            Text =
                            {
                                Text = "×",
                                FontSize = FONT_SIZE + 4,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 1",
                                Font = BOLD_FONT_NAME,
                            },
                        },
                        MainLayer,
                        removeButtonId
                    );

                    string tradeButtonId = $"FriendTrade_offline_{i}";
                    _ = container.Add(
                        new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin =
                                    $"{friendsButtonX + BUTTON_WIDTH - 0.028f} {buttonY + 0.005f}",
                                AnchorMax =
                                    $"{friendsButtonX + BUTTON_WIDTH - 0.015f} {buttonY + BUTTON_HEIGHT - 0.005f}",
                            },
                            Button = { Color = "0.0 0.5 0.0 0.7", Command = $"trade {friendId}" },
                            Text =
                            {
                                Text = "⇄",
                                FontSize = FONT_SIZE + 2,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 1",
                                Font = BOLD_FONT_NAME,
                            },
                        },
                        MainLayer,
                        tradeButtonId
                    );
                }

                _ = container.Add(
                    new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0.3", AnchorMax = "1 0.35" },
                        Text =
                        {
                            Text = "<color=#FFFFFF>АВТОПРИНЯТИЕ</color>",
                            FontSize = 16,
                            Align = TextAnchor.MiddleCenter,
                            Font = BOLD_FONT_NAME,
                        },
                    },
                    "FriendsPanel"
                );

                const float autoAcceptButtonY =
                    startButtonY - (6 * (BUTTON_HEIGHT + BUTTON_SPACING)) - 0.02f;

                ButtonConfig autoAcceptButtonConfig = new()
                {
                    Command = "chat.say /atp",
                    ButtonColor = "0.19 0.19 0.21 0.75",
                    TextColor = "1 1 1 1",
                    ButtonText = new Dictionary<string, string>
                    {
                        { "ru", "<color=#FFFFFF>АВТОПРИНЯТИЕ</color>" },
                    },
                };

                CreateButton(
                    container,
                    autoAcceptButtonConfig,
                    friendsButtonX,
                    autoAcceptButtonY,
                    550,
                    player
                );
            }
            catch (Exception ex)
            {
                PrintError($"Error in CreateFriendsPanel: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void ResetCache(ulong userId)
        {
            _ = homesCache.Remove(userId);
            _ = lastHomesUpdate.Remove(userId);
            _ = friendsCache.Remove(userId);
            _ = lastFriendsUpdate.Remove(userId);

            RelationshipManager.PlayerTeam team =
                RelationshipManager.ServerInstance.FindPlayersTeam(userId);
            if (team != null)
            {
                foreach (ulong teamMember in team.members)
                {
                    _ = friendsCache.Remove(teamMember);
                    _ = lastFriendsUpdate.Remove(teamMember);
                }
            }
        }

        private void OnChatCommand(BasePlayer player, string command, string[] args)
        {
            try
            {
                if (command == "f" && args.Length >= 2 && args[0] == "remove")
                {
                    _ = timer.Once(
                        0.5f,
                        () =>
                        {
                            DestroyUI(player);
                            CreateMenu(player);
                        }
                    );
                    return;
                }

                if (!permission.UserHasPermission(player.UserIDString, "tpmenu.use"))
                {
                    player.ChatMessage("У вас нет разрешения на использование этой команды");
                    return;
                }

                DestroyUI(player);
                CreateMenu(player);
            }
            catch (Exception ex)
            {
                PrintError($"Error in OnChatCommand: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void CreateButton(
            CuiElementContainer container,
            ButtonConfig button,
            float startX,
            float currentY,
            int index,
            BasePlayer player
        )
        {
            string outlinePanelName = $"{MainLayer}.ButtonOutline.{index}";
            _ = container.Add(
                new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = $"{startX} {currentY}",
                        AnchorMax = $"{startX + BUTTON_WIDTH} {currentY + BUTTON_HEIGHT}",
                    },
                    Image = { Color = "0.09 0.09 0.11 0" },
                },
                MainLayer,
                outlinePanelName
            );

            CreateButtonOutline(container, outlinePanelName);

            _ = container.Add(
                new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = $"{startX} {currentY}",
                        AnchorMax = $"{startX + BUTTON_WIDTH} {currentY + BUTTON_HEIGHT}",
                    },
                    Button = { Color = button.ButtonColor, Command = button.Command },
                    Text =
                    {
                        Text = button.ButtonText["ru"],
                        FontSize = FONT_SIZE,
                        Align = TextAnchor.MiddleCenter,
                        Color = button.TextColor,
                        Font = BOLD_FONT_NAME,
                    },
                },
                MainLayer
            );
        }

        private void CreateButton(
            CuiElementContainer container,
            MenuButton button,
            string buttonPanelName,
            BasePlayer player
        )
        {
            float interfaceScale = GetInterfaceScale(player);
            int fontSize = CalculateFontSize(interfaceScale);

            _ = container.Add(
                new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text =
                    {
                        Text = button.Text,
                        FontSize = fontSize,
                        Align = TextAnchor.MiddleCenter,
                        Color = config.MainSettings.ButtonSettings.TextColor,
                        Font = BOLD_FONT_NAME,
                    },
                },
                buttonPanelName,
                $"{buttonPanelName}.Text"
            );

            _ = container.Add(
                new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Color = "0 0 0 0", Command = button.Command },
                    Text = { Text = "" },
                },
                buttonPanelName,
                $"{buttonPanelName}.Button"
            );
        }

        private void CreatePanel(
            CuiElementContainer container,
            string name,
            string min,
            string max,
            string text,
            int fontSize
        )
        {
            _ = container.Add(
                new CuiPanel
                {
                    RectTransform = { AnchorMin = min, AnchorMax = max },
                    Image = { Color = "0.12 0.12 0.12 0" },
                },
                MainLayer,
                name
            );

            _ = container.Add(
                new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text =
                    {
                        Text = text,
                        FontSize = fontSize,
                        Align = TextAnchor.MiddleCenter,
                        Font = BOLD_FONT_NAME,
                    },
                },
                name
            );
        }

        private sealed class MainSettings
        {
            [JsonProperty("Показывать при подключении")]
            public bool ShowOnConnect;

            [JsonProperty("Показывать только при первом подключении")]
            public bool ShowOnlyFirstConnect;

            private List<string> commands;

            [JsonProperty("Команды для открытия меню")]
            public List<string> Commands
            {
                get => commands ??= new List<string> { "menu", "tpmenu" };
                set =>
                    commands = value?.Distinct().ToList() ?? new List<string> { "menu", "tpmenu" };
            }

            [JsonProperty("Настройки основных кнопок")]
            public ButtonSettings ButtonSettings = new();
        }

        private sealed class ButtonSettings
        {
            [JsonProperty("Цвет кнопки")]
            public string ButtonColor = "0.19 0.19 0.21 0.75";

            [JsonProperty("Цвет текста")]
            public string TextColor = "1 1 1 1";

            [JsonProperty("Список кнопок")]
            public List<MenuButton> Buttons = new()
            {
                new() { Command = "chat.say /block", Text = "БЛОКИРОВКА" },
                new() { Command = "chat.say /help", Text = "ПОМОЩЬ" },
            };
        }

        private sealed class MenuButton
        {
            [JsonProperty("Чат Команда")]
            public string Command { get; set; }

            [JsonProperty("Текст кнопки")]
            public string Text { get; set; }
        }

        private sealed class ButtonConfig
        {
            [JsonProperty("Заголовок")]
            public string Title { get; set; }

            [JsonProperty("Команда")]
            public string Command { get; set; }

            [JsonProperty("Цвет кнопки")]
            public string ButtonColor { get; set; }

            [JsonProperty("Цвет текста")]
            public string TextColor { get; set; }

            [JsonProperty("Текст на кнопке")]
            public Dictionary<string, string> ButtonText { get; set; }

            [JsonProperty("Максимальное количество точек домов")]
            public int MaxHomes { get; set; } = 10;

            [JsonProperty("Список привилегий по количеству home (Привилегия: количество)")]
            public Dictionary<string, int> PermissionsHomes { get; set; } =
                new Dictionary<string, int> { { "vip", 10 }, { "premium", 15 } };
        }

        private sealed class PluginConfig
        {
            [JsonProperty("Основные настройки")]
            public MainSettings MainSettings = new();

            [JsonProperty("Настройки домов")]
            public ButtonConfig Homes = new();

            [JsonProperty("Настройки друзей")]
            public ButtonConfig Friends = new();

            [JsonProperty("Настройки старых домов")]
            public HomeSettings Home = new();

            [JsonProperty("Настройки старых друзей")]
            public FriendSettings FriendsSettings = new();

            [JsonProperty("Версия конфигурации")]
            public VersionNumber ConfigVersion = new(1, 1, 4);

            [JsonIgnore]
            public string Title = "Главное меню";

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig
                {
                    MainSettings = new MainSettings(),
                    Homes = new ButtonConfig
                    {
                        Title = "ДОМА",
                        ButtonColor = "0.19 0.19 0.21 0.75",
                        TextColor = "1 1 1 1",
                        ButtonText = new Dictionary<string, string> { { "ru", "ДОМА" } },
                    },
                    Friends = new ButtonConfig
                    {
                        Title = "ДРУЗЬЯ",
                        ButtonColor = "0.19 0.19 0.21 0.75",
                        TextColor = "1 1 1 1",
                        ButtonText = new Dictionary<string, string> { { "ru", "ДРУЗЬЯ" } },
                    },
                    Home = new HomeSettings(),
                    FriendsSettings = new FriendSettings(),
                    Title = "Главное меню",
                    ConfigVersion = new VersionNumber(1, 1, 4),
                };
            }
        }

        private sealed class HomeSettings
        {
            [JsonProperty("Заголовок блока домов игрока")]
            public string Title = "ДОМА";

            [JsonProperty("Цвет фона блока")]
            public string Color = "0.12 0.12 0.12 1";

            [JsonProperty("Максимальное количество точек домов")]
            public int MaxHomes = 5;

            [JsonProperty("Список привилегий по количеству home (Привилегия: количество)")]
            public Dictionary<string, int> PermissionsHomes = new()
            {
                { "vip", 10 },
                { "premium", 15 },
            };
        }

        private sealed class FriendSettings
        {
            [JsonProperty("Заголовок блока друзей игрока")]
            public string Title = "ДРУЗЬЯ";

            [JsonProperty("Цвет фона блока")]
            public string Color = "0.12 0.12 0.12 1";

            [JsonProperty("Лимит друзей")]
            public int FriendsLimit = 2;
        }

        private bool IsUIVisible(BasePlayer player, string panelName)
        {
            return player?.IsConnected == true
                && player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot);
        }

        private bool IsActionInProgress(ulong userId)
        {
            return actionsInProgress.TryGetValue(userId, out bool inProgress) && inProgress;
        }

        private void SetActionInProgress(ulong userId, bool inProgress)
        {
            actionsInProgress[userId] = inProgress;
        }

        private float GetScaledButtonHeight(float interfaceScale)
        {
            return BASE_BUTTON_HEIGHT * interfaceScale;
        }

        private float GetScaledButtonWidth(float interfaceScale)
        {
            return BASE_BUTTON_WIDTH * interfaceScale;
        }

        private float GetScaledButtonSpacing(float interfaceScale)
        {
            return BASE_BUTTON_SPACING * interfaceScale;
        }
    }
}
