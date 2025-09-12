using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using System;
using UnityEngine;



namespace Oxide.Plugins
{
    [Info("EventControlUI", "Naked", "1.0.0")]
    [Description("Plugin to manage teams, permissions, and teleportation for events in Rust via a UI.")]
    public class EventControlUI : RustPlugin
    {
        private const string PermissionManageTeams = "eventcontrolui.manage.teams";
        private const string PermissionManagePerms = "eventcontrolui.manage.perms";
        private const string PermissionManageTp = "eventcontrolui.manage.tp";
        private const string PermissionHideHud = "eventcontrolui.hidehud";
        private const string PermissionBypass = "kits.bypass";
        

        private Dictionary<string, string> playerTeams = new Dictionary<string, string>(); // Player ID -> Team

         private Dictionary<string, string> teamColors = new Dictionary<string, string>
        {
            { "TeamOne", "0.2 0.8 0.2 1.0" },
            { "TeamTwo", "0.2 0.5 0.8 1.0" },
            { "TeamThree", "0.8 0.2 0.2 1.0" },
            { "TeamFour", "0.8 0.8 0.2 1.0" }
        };

        // --- UI Tracking ---
        
       
        // Global loot flag – true means players are allowed to loot by default.
        private bool lootAllowed = true;

        // Dictionary to store per-team loot settings.
        private Dictionary<string, bool> teamLootStatus = new Dictionary<string, bool>();

        private Dictionary<string, List<string>> playerPerms = new Dictionary<string, List<string>>(); // Player ID -> Permissions
        private Dictionary<string, ulong> teamMapping = new Dictionary<string, ulong>();
        private Vector3? worldSpawnLocation = null;
        private List<string> teamNames = new List<string> { "TeamOne", "TeamTwo", "TeamThree", "TeamFour" };
        private List<string> availablePerms = new List<string> { "none" }; // Configurable

        private bool allowTeamLeave = true; // Toggle for allowing players to leave teams
        private string defaultTeam = null; // Default team players join automatically
private Harmony harmony; // Add this at the top of the class
public static EventControlUI Instance;
private bool friendlyFire = true; // Default value
 private List<string> defaultTeams = new List<string>(); // Allow multiple default teams

private void Init()
{
    Instance = this; // Set the static reference

    // Register permissions
    permission.RegisterPermission(PermissionManageTeams, this);
    permission.RegisterPermission(PermissionManagePerms, this);
    permission.RegisterPermission(PermissionManageTp, this);
    permission.RegisterPermission(PermissionHideHud, this);
    

        // Initialize Kits plugin reference
    Kits = plugins.Find("Kits"); // Ensure the plugin name matches exactly

    if (Kits == null)
    {
        PrintError("Kits plugin not found! Ensure that the Kits plugin is installed and loaded before EventControlUI.");
    }

    // Load the configuration
    LoadConfig();

    // Initialize Harmony for patching
    harmony = new Harmony("com.yourname.eventcontrolui");
    harmony.PatchAll();

    // Initialize team-specific states if not already set
    foreach (var team in teamNames)
    {
        if (!teamFreezeStatus.ContainsKey(team))
            teamFreezeStatus[team] = false;

        if (!teamRaidingStatus.ContainsKey(team))
            teamRaidingStatus[team] = raidingEnabled;

        if (!teamPvPStatus.ContainsKey(team))
            teamPvPStatus[team] = pvpEnabled;

        if (!teamGodModeStatus.ContainsKey(team))
            teamGodModeStatus[team] = godModeEnabled;

        if (!teamBuildingStatus.ContainsKey(team))
            teamBuildingStatus[team] = buildingEnabled;
    }

    // Initialize timers for Global and each team
    timers["Global"] = new TimerData
    {
        TeamName = "Global",
        RemainingTime = 0f,
        IsRunning = false,
        BuildingOnZero = false,
        RaidingOnZero = false,
        PvPOnZero = false,
        CustomText = ""
    };

    foreach (var team in teamNames)
    {
        timers[team] = new TimerData
        {
            TeamName = team,
            RemainingTime = 0f,
            IsRunning = false,
            BuildingOnZero = false,
            RaidingOnZero = false,
            PvPOnZero = false,
            CustomText = ""
        };
    }
}




protected override void LoadDefaultConfig()
{
    Config["AvailablePerms"] = new List<string> {  "atlasvanish.use",
  "bgrade.all"};
    SaveConfig();
    Config["WorldSpawnLocation"] = null;
    Config["AvailablePerms"] = availablePerms;
    Config["AllowTeamLeave"] = allowTeamLeave;
    Config["DefaultTeams"] = new List<string>();
    Config["FriendlyFire"] = true;
    Config["RaidingEnabled"] = true;
    Config["BuildingEnabled"] = true; // Default state
    Config["GodModeEnabled"] = false; // Default state
    Config["Timers"] = new Dictionary<string, object>();
    
    SaveConfig();


}

private void LoadConfig()
{
    // Load Building Enabled setting
    buildingEnabled = Config["BuildingEnabled"] != null && (bool)Config["BuildingEnabled"];
     godModeEnabled = Config["GodModeEnabled"] != null && (bool)Config["GodModeEnabled"];
    
    // Load Friendly Fire setting
    friendlyFire = Config["FriendlyFire"] != null && (bool)Config["FriendlyFire"];

    // Load Available Permissions
    if (Config["AvailablePerms"] is List<object> permsFromConfig)
    {
        availablePerms = permsFromConfig.Cast<string>().ToList();
    }
    else
    {
        availablePerms = new List<string>(); // Default empty list if not in config
    }
    // Load Allow Team Leave setting
    allowTeamLeave = Config["AllowTeamLeave"] != null && (bool)Config["AllowTeamLeave"];

    // Load Default Teams (multiple teams)
    if (Config["DefaultTeams"] is List<object> teamsFromConfig)
    {
        defaultTeams = teamsFromConfig.Cast<string>().ToList();
    }
    else
    {
        defaultTeams = new List<string>(); // Ensure it initializes even if config is missing
    }

     if (Config["WorldSpawnLocation"] != null)
    {
        worldSpawnLocation = Config["WorldSpawnLocation"] as Vector3?;
    }

    // Load Team-Specific Freeze Status
    if (Config["TeamFreezeStatus"] is Dictionary<string, object> freezeStatusConfig)
    {
        foreach (var kvp in freezeStatusConfig)
        {
            teamFreezeStatus[kvp.Key] = Convert.ToBoolean(kvp.Value);
        }
    }

    // Load Team-Specific Raiding Status
    if (Config["TeamRaidingStatus"] is Dictionary<string, object> raidingStatusConfig)
    {
        foreach (var kvp in raidingStatusConfig)
        {
            teamRaidingStatus[kvp.Key] = Convert.ToBoolean(kvp.Value);
        }
    }

    // Load Team-Specific PvP Status
    if (Config["TeamPvPStatus"] is Dictionary<string, object> pvpStatusConfig)
    {
        foreach (var kvp in pvpStatusConfig)
        {
            teamPvPStatus[kvp.Key] = Convert.ToBoolean(kvp.Value);
        }
    }

    // Load Team-Specific God Mode Status
    if (Config["TeamGodModeStatus"] is Dictionary<string, object> godModeStatusConfig)
    {
        foreach (var kvp in godModeStatusConfig)
        {
            teamGodModeStatus[kvp.Key] = Convert.ToBoolean(kvp.Value);
        }
    }

    // Load Team-Specific Building Status
    if (Config["TeamBuildingStatus"] is Dictionary<string, object> buildingStatusConfig)
    {
        foreach (var kvp in buildingStatusConfig)
        {
            teamBuildingStatus[kvp.Key] = Convert.ToBoolean(kvp.Value);
        }
    }

    // Ensure all teams have an entry
    foreach (var team in teamNames)
    {
        if (!teamFreezeStatus.ContainsKey(team)) teamFreezeStatus[team] = false;
        if (!teamRaidingStatus.ContainsKey(team)) teamRaidingStatus[team] = raidingEnabled;
        if (!teamPvPStatus.ContainsKey(team)) teamPvPStatus[team] = pvpEnabled;
        if (!teamGodModeStatus.ContainsKey(team)) teamGodModeStatus[team] = godModeEnabled;
        if (!teamBuildingStatus.ContainsKey(team)) teamBuildingStatus[team] = buildingEnabled;
    }

        if (Config["Timers"] is Dictionary<string, object> timersConfig)
    {
        foreach (var kvp in timersConfig)
        {
            string teamName = kvp.Key;
            var timerObj = kvp.Value as Dictionary<string, object>;
            if (timerObj == null) continue;

            if (!timers.ContainsKey(teamName))
                timers[teamName] = new TimerData { TeamName = teamName };

            TimerData timerData = timers[teamName];
            timerData.RemainingTime = timerObj.ContainsKey("RemainingTime") ? Convert.ToSingle(timerObj["RemainingTime"]) : 0f;
            timerData.IsRunning = timerObj.ContainsKey("IsRunning") ? Convert.ToBoolean(timerObj["IsRunning"]) : false;
            timerData.BuildingOnZero = timerObj.ContainsKey("BuildingOnZero") ? Convert.ToBoolean(timerObj["BuildingOnZero"]) : false;
            timerData.RaidingOnZero = timerObj.ContainsKey("RaidingOnZero") ? Convert.ToBoolean(timerObj["RaidingOnZero"]) : false;
            timerData.PvPOnZero = timerObj.ContainsKey("PvPOnZero") ? Convert.ToBoolean(timerObj["PvPOnZero"]) : false;
            timerData.CustomText = timerObj.ContainsKey("CustomText") ? timerObj["CustomText"].ToString() : "";

            if (timerData.IsRunning && timerData.RemainingTime > 0f)
            {
                timerData.TimerInstance = timer.Once(timerData.RemainingTime, () => OnTimerEnd(teamName));
            }
        }
    }

    SaveConfig();
}

private HashSet<ulong> uiOpenPlayers = new HashSet<ulong>();
// HashSet to track players with the Timers tab open
private HashSet<ulong> timersTabOpenPlayers = new HashSet<ulong>();

        [ChatCommand("eventui")]
        private void EventUICommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionManageTeams))
            {
                SendReply(player, "You don't have permission to use this command.");
                return;
            }

            OpenMainMenu(player, "teams");
            uiOpenPlayers.Add(player.userID);
        }

void OnPlayerDisconnected(BasePlayer player, string reason)
{
    uiOpenPlayers.Remove(player.userID);
    timersTabOpenPlayers.Remove(player.userID);
}

private void OpenMainMenu(BasePlayer player, string activeTab, string selectedPlayerId = null, string selectedTeam = null, int page = 0)
{



    // Destroy existing UI
    CuiHelper.DestroyUi(player, "EventControlUI");

    CuiElementContainer container = new CuiElementContainer();

    // Background panel
    string panelName = "EventControlUI";
    container.Add(new CuiPanel
    {
        RectTransform = { AnchorMin = "0.05 0.05", AnchorMax = "0.95 0.95" },
        Image = { Color = "0.1 0.1 0.1 0.8" }
    }, "Overlay", panelName);

    // Tabs including the new "Timers" tab
    string[] tabs = { "Teams", "Permissions", "Global", "Teleport", "Timers", "Kits" };
    float tabWidth = 0.16f; // Adjusted width to accommodate 5 tabs within the panel
    for (int i = 0; i < tabs.Length; i++)
    {
        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = $"{0.05f + i * tabWidth} 0.95", AnchorMax = $"{0.05f + (i + 1) * tabWidth - 0.02f} 1.0" },
            Button = { Command = $"eventui.tab {tabs[i].ToLower()}", Color = activeTab == tabs[i].ToLower() ? "0.8 0.8 0.8 1.0" : "0.2 0.5 0.8 1.0" },
            Text = { Text = tabs[i], FontSize = 20, Align = UnityEngine.TextAnchor.MiddleCenter }
        }, panelName);
    }

    // Main Content based on the active tab
    switch (activeTab)
    {
        case "teams":
            AddTeamTabContent(container, panelName, selectedPlayerId, selectedTeam, page);
            break;
        case "permissions":
            AddPermissionsTabContent(container, panelName, page);
            break;
        case "global":
            AddGlobalTabContent(container, panelName);
            break;
        case "teleport":
            AddTeleportTabContent(container, panelName, page);
            break;
        case "timers":
            AddTimersTabContent(container, panelName);
            break;
        case "kits":
            AddKitsTabContent(container, panelName, page);
            break;
        default:
            // Default to Teams tab if an unknown tab is specified
            AddTeamTabContent(container, panelName, selectedPlayerId, selectedTeam, page);
            break;
    }

    
    // Close button
    container.Add(new CuiButton
    {
        RectTransform = { AnchorMin = "0.85 0.05", AnchorMax = "0.95 0.1" },
        Button = { Command = "eventui.close", Color = "0.8 0.2 0.2 1.0" },
        Text = { Text = "Close", FontSize = 20, Align = UnityEngine.TextAnchor.MiddleCenter }
    }, panelName);

    // Add the constructed UI to the player
    CuiHelper.AddUi(player, container);
}


[ConsoleCommand("team.page")]
private void HandleTeamPageCommand(ConsoleSystem.Arg args)
{
    BasePlayer player = args.Connection?.player as BasePlayer;
    if (player == null) return;

    int page = args.Args.Length > 0 ? int.Parse(args.Args[0]) : 0;
    OpenMainMenu(player, "teams", null, null, page);
}


        private void AddTeamTabContent(CuiElementContainer container, string parent, string selectedPlayerId = null, string selectedTeam = null, int page = 0)
{
    // --- Allow Leave Toggle ---
    container.Add(new CuiLabel {
        RectTransform = { AnchorMin = "0.1 0.88", AnchorMax = "0.3 0.93" },
        Text = { Text = "Allow Leave", FontSize = 24, Align = TextAnchor.MiddleLeft }
    }, parent);
    container.Add(new CuiButton {
        RectTransform = { AnchorMin = "0.3 0.88", AnchorMax = "0.4 0.93" },
        Button = { Command = "team.toggleleave", Color = allowTeamLeave ? "0.2 0.8 0.2 1.0" : "0.8 0.2 0.2 1.0" },
        Text = { Text = allowTeamLeave ? "True" : "False", FontSize = 24, Align = TextAnchor.MiddleCenter }
    }, parent);

    // --- Friendly Fire Toggle ---
    container.Add(new CuiLabel {
        RectTransform = { AnchorMin = "0.5 0.88", AnchorMax = "0.7 0.93" },
        Text = { Text = "Friendly Fire", FontSize = 24, Align = TextAnchor.MiddleLeft }
    }, parent);
    container.Add(new CuiButton {
        RectTransform = { AnchorMin = "0.7 0.88", AnchorMax = "0.8 0.93" },
        Button = { Command = "team.togglefriendlyfire", Color = friendlyFire ? "0.2 0.8 0.2 1.0" : "0.8 0.2 0.2 1.0" },
        Text = { Text = friendlyFire ? "True" : "False", FontSize = 24, Align = TextAnchor.MiddleCenter }
    }, parent);

    // --- Default Teams Row ---
    container.Add(new CuiLabel {
        RectTransform = { AnchorMin = "0.1 0.82", AnchorMax = "0.3 0.87" },
        Text = { Text = "Default Teams", FontSize = 24, Align = TextAnchor.MiddleLeft }
    }, parent);

    int index = 0;
    foreach (var teamName in teamNames)
    {
        container.Add(new CuiButton {
            RectTransform = { AnchorMin = $"{0.35f + index * 0.12f} 0.82", AnchorMax = $"{0.45f + index * 0.12f} 0.87" },
            Button = { Command = $"team.toggleddefault {teamName}", Color = defaultTeams.Contains(teamName) ? "0.2 0.8 0.2 1.0" : "0.8 0.2 0.2 1.0" },
            Text = { Text = teamName, FontSize = 24, Align = TextAnchor.MiddleCenter }
        }, parent);
        index++;
    }

    // --- Player Grid (Team Assignment) ---
    int playersPerPage = 6 * 10; // 60 players per page
    var players = BasePlayer.activePlayerList.Skip(page * playersPerPage).Take(playersPerPage).ToList();

    // Adjust grid starting position to be below the default teams row.
    float gridStartX = 0.1f;
    float gridStartY = 0.75f;
    float buttonWidth = 0.13f;
    float buttonHeight = 0.06f;
    float hSpacing = 0.02f;
    float vSpacing = 0.01f;
    int cols = 6;

    for (int i = 0; i < players.Count; i++)
    {
        int col = i % cols;
        int row = i / cols;
        float xMin = gridStartX + col * (buttonWidth + hSpacing);
        float xMax = xMin + buttonWidth;
        float yMax = gridStartY - row * (buttonHeight + vSpacing);
        float yMin = yMax - buttonHeight;
        BasePlayer p = players[i];
        string playerId = p.UserIDString;
        // Set button color based on assigned team (gray if none)
        string boxColor = "0.5 0.5 0.5 1.0";
        if (playerTeams.ContainsKey(playerId))
        {
            string team = playerTeams[playerId];
            if (teamColors.ContainsKey(team))
                boxColor = teamColors[team];
        }
        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = $"{xMin} {yMin}", AnchorMax = $"{xMax} {yMax}" },
            Button = { Command = $"team.openselect {playerId} {page}", Color = boxColor },
            Text = { Text = p.displayName, FontSize = 14, Align = TextAnchor.MiddleCenter }
        }, parent);
    }

    // --- Pagination Arrows ---
    int totalPlayers = BasePlayer.activePlayerList.Count;
    int totalPages = (int)Math.Ceiling((double)totalPlayers / playersPerPage);
    if (totalPages > 1)
    {
        if (page > 0)
        {
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.1 0.05", AnchorMax = "0.2 0.1" },
                Button = { Command = $"team.paginate {page - 1}", Color = "0.2 0.5 0.8 1.0" },
                Text = { Text = "<< Previous", FontSize = 20, Align = TextAnchor.MiddleCenter }
            }, parent);
        }
        if (page < totalPages - 1)
        {
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.8 0.05", AnchorMax = "0.9 0.1" },
                Button = { Command = $"team.paginate {page + 1}", Color = "0.2 0.5 0.8 1.0" },
                Text = { Text = "Next >>", FontSize = 20, Align = TextAnchor.MiddleCenter }
            }, parent);
        }
    }
}


        private void OpenTeamSelectionMenu(BasePlayer admin, string targetPlayerId, int page)
        {
            // Destroy any existing team selection UI
            CuiHelper.DestroyUi(admin, "TeamSelectionUI");

            var container = new CuiElementContainer();
            // Create a centered panel for team selection
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.3 0.3", AnchorMax = "0.7 0.7" },
                Image = { Color = "0.1 0.1 0.1 0.9" }
            }, "Overlay", "TeamSelectionUI");

            // Title label
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.32 0.65", AnchorMax = "0.68 0.75" },
                Text = { Text = "Select a Team", FontSize = 24, Align = TextAnchor.MiddleCenter }
            }, "TeamSelectionUI");

            // Show one button per team from teamNames.
            int teamCount = teamNames.Count;
            float buttonWidth = 0.15f;
            float spacing = 0.05f;
            float totalWidth = teamCount * buttonWidth + (teamCount - 1) * spacing;
            float startX = 0.5f - totalWidth / 2;
            for (int i = 0; i < teamCount; i++)
            {
                string teamName = teamNames[i];
                float xMin = startX + i * (buttonWidth + spacing);
                float xMax = xMin + buttonWidth;
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{xMin} 0.45", AnchorMax = $"{xMax} 0.55" },
                    Button = { Command = $"team.assignplayer {targetPlayerId} {teamName} {page}", Color = teamColors.ContainsKey(teamName) ? teamColors[teamName] : "0.2 0.2 0.2 1.0" },
                    Text = { Text = teamName, FontSize = 18, Align = TextAnchor.MiddleCenter }
                }, "TeamSelectionUI");
            }

            // A close button to cancel team selection and return to the teams tab.
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.45 0.35", AnchorMax = "0.55 0.4" },
                Button = { Command = $"team.closeSelect {page}", Color = "0.8 0.2 0.2 1.0" },
                Text = { Text = "Close", FontSize = 18, Align = TextAnchor.MiddleCenter }
            }, "TeamSelectionUI");

            CuiHelper.AddUi(admin, container);
        }


        // Pagination command for teams tab.
        [ConsoleCommand("team.paginate")]
        private void HandleTeamPagination(ConsoleSystem.Arg args)
        {
            BasePlayer admin = args.Connection?.player as BasePlayer;
            if (admin == null) return;
            int page = 0;
            if (args.Args != null && args.Args.Length > 0)
                int.TryParse(args.Args[0], out page);
            OpenMainMenu(admin, "teams");
        }

        // Command triggered when a player name button is clicked.
        // It opens the team selection menu for that player.
        [ConsoleCommand("team.openselect")]
        private void OpenTeamSelectCommand(ConsoleSystem.Arg args)
        {
            BasePlayer admin = args.Connection?.player as BasePlayer;
            if (admin == null) return;
            if (args.Args == null || args.Args.Length < 2) return;
            string targetPlayerId = args.Args[0];
            int page = 0;
            int.TryParse(args.Args[1], out page);
            OpenTeamSelectionMenu(admin, targetPlayerId, page);
        }

        // Command to close the team selection menu and return to the teams tab.
        [ConsoleCommand("team.closeSelect")]
        private void CloseTeamSelectCommand(ConsoleSystem.Arg args)
        {
            BasePlayer admin = args.Connection?.player as BasePlayer;
            if (admin == null) return;
            int page = 0;
            if (args.Args != null && args.Args.Length > 0)
                int.TryParse(args.Args[0], out page);
            CuiHelper.DestroyUi(admin, "TeamSelectionUI");
            OpenMainMenu(admin, "teams");
        }

        // Command that is run when a team button is clicked in the team selection menu.
        // It assigns the target player to the chosen team.
[ConsoleCommand("team.assignplayer")]
private void AssignPlayerToTeamCommand(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin == null) return;
    if (args.Args == null || args.Args.Length < 3) return;
    
    string targetPlayerId = args.Args[0];
    string newTeamName = args.Args[1];
    int page = 0;
    int.TryParse(args.Args[2], out page);

    // Validate team name.
    if (!teamNames.Contains(newTeamName))
    {
        SendReply(admin, $"Team {newTeamName} does not exist.");
        return;
    }

    // Update our internal dictionary.
    if (playerTeams.ContainsKey(targetPlayerId))
        playerTeams[targetPlayerId] = newTeamName;
    else
        playerTeams.Add(targetPlayerId, newTeamName);

    // Find the target player.
    var targetPlayer = BasePlayer.FindByID(ulong.Parse(targetPlayerId));
    if (targetPlayer == null)
    {
        SendReply(admin, "Player not found.");
        return;
    }

    // If the player is already in a team, remove them from it.
    if (targetPlayer.currentTeam != 0)
    {
        var oldTeam = RelationshipManager.ServerInstance.FindTeam(targetPlayer.currentTeam);
        if (oldTeam != null)
        {
            oldTeam.RemovePlayer(targetPlayer.userID);
            UpdateTeamMembers(oldTeam);
        }
    }

    // Find or create the new team.
    RelationshipManager.PlayerTeam newTeam;
    if (!teamMapping.ContainsKey(newTeamName))
    {
        newTeam = RelationshipManager.ServerInstance.CreateTeam();
        newTeam.teamName = newTeamName;
        teamMapping[newTeamName] = newTeam.teamID;
    }
    else
    {
        newTeam = RelationshipManager.ServerInstance.FindTeam(teamMapping[newTeamName]);
    }

    // Add the player to the new team.
    newTeam.AddPlayer(targetPlayer);
    targetPlayer.currentTeam = newTeam.teamID;
    UpdateTeamMembers(newTeam);

    // Notify the player.
    targetPlayer.ChatMessage($"You have been added to the team: {newTeamName}");

    // Close the team selection UI and refresh the Teams tab.
    CuiHelper.DestroyUi(admin, "TeamSelectionUI");
    OpenMainMenu(admin, "teams", page: page);
}




[ConsoleCommand("team.togglefriendlyfire")]
private void ToggleFriendlyFire(ConsoleSystem.Arg args)
{
    BasePlayer player = args.Connection?.player as BasePlayer;
    if (player == null) return;

    // Toggle the friendly fire setting
    friendlyFire = !friendlyFire;
    Config["FriendlyFire"] = friendlyFire;
    SaveConfig();

    // Notify the admin
    player.ChatMessage($"Friendly Fire is now {(friendlyFire ? "Enabled" : "Disabled")}");

    // Refresh the UI
    OpenMainMenu(player, "teams");
}


[ConsoleCommand("team.toggleddefault")]
private void ToggleDefaultTeam(ConsoleSystem.Arg args)
{
    BasePlayer player = args.Connection?.player as BasePlayer;
    if (player == null || args.Args.Length < 1) return;

    string teamName = args.Args[0];

    if (defaultTeams.Contains(teamName))
        defaultTeams.Remove(teamName);
    else
        defaultTeams.Add(teamName);

    Config["DefaultTeams"] = defaultTeams;
    SaveConfig();

    player.ChatMessage($"Default teams updated: {string.Join(", ", defaultTeams)}");
    OpenMainMenu(player, "teams");
}





void OnPlayerConnected(BasePlayer player)
{
    // Check if the player is already assigned to a team
    if (playerTeams.ContainsKey(player.UserIDString))
    {
        string team = playerTeams[player.UserIDString];
        player.ChatMessage($"Welcome back! You are in team {team}.");
        return;
    }

    // Handle players who are excluded from auto-team assignment
    if (defaultTeams == null || defaultTeams.Count == 0)
    {
        player.ChatMessage("You are not assigned to any team.");
        return;
    }

    // Find the least populated default team
    string assignedTeam = defaultTeams.OrderBy(t =>
    {
        // Ensure the team exists in the teamMapping dictionary
        if (!teamMapping.ContainsKey(t))
        {
            CreateTeamIfNotExists(t);
        }

        return BasePlayer.activePlayerList.Count(p => playerTeams.ContainsKey(p.UserIDString) && playerTeams[p.UserIDString] == t);
    }).First();

    // Assign the player to the selected team
    playerTeams[player.UserIDString] = assignedTeam;

    var newTeam = RelationshipManager.ServerInstance.FindTeam(teamMapping[assignedTeam]);
    if (newTeam == null)
    {
        newTeam = CreateTeamIfNotExists(assignedTeam);
    }

    newTeam.AddPlayer(player);
    player.currentTeam = newTeam.teamID;

    player.ChatMessage($"You have been added to the default team: {assignedTeam}.");
}

// Helper Method to Create a Team If It Doesn't Exist
private RelationshipManager.PlayerTeam CreateTeamIfNotExists(string teamName)
{
    if (!teamMapping.ContainsKey(teamName))
    {
        var newTeam = RelationshipManager.ServerInstance.CreateTeam();
        newTeam.teamName = teamName;
        teamMapping[teamName] = newTeam.teamID;
        return newTeam;
    }

    return RelationshipManager.ServerInstance.FindTeam(teamMapping[teamName]);
}





private void ShowLeaveTeamBlockUI(BasePlayer player)
{
    // Destroy any existing blocking UI
    CuiHelper.DestroyUi(player, "LeaveTeamBlockUI");

    // Only show the UI if leaving teams is not allowed
    if (allowTeamLeave) return;

    // Create a blocking UI
    var container = new CuiElementContainer();
    string panelName = "LeaveTeamBlockUI";

    container.Add(new CuiPanel
    {
        RectTransform = { AnchorMin = "0.15 0.05", AnchorMax = "0.3 .12" }, // Position over the in-game Leave Team button
        Image = { Color = "0 0 0 0" } // Semi-transparent black
    }, "Overlay", panelName);



    CuiHelper.AddUi(player, container);
}

private void RemoveLeaveTeamBlockUI(BasePlayer player)
{
    CuiHelper.DestroyUi(player, "LeaveTeamBlockUI");
}






[ConsoleCommand("team.toggleleave")]
private void ToggleAllowLeave(ConsoleSystem.Arg args)
{
    BasePlayer player = args.Connection?.player as BasePlayer;
    if (player == null) return;

    allowTeamLeave = !allowTeamLeave; // Toggle the value
    Config["AllowTeamLeave"] = allowTeamLeave;
    SaveConfig();

    // Notify the admin about the current status
    player.ChatMessage($"Allow Leave is now {(allowTeamLeave ? "enabled" : "disabled")}.");

    // Update the UI for all active players
    foreach (var activePlayer in BasePlayer.activePlayerList)
    {
        if (allowTeamLeave)
        {
            RemoveLeaveTeamBlockUI(activePlayer);
        }
        else
        {
            ShowLeaveTeamBlockUI(activePlayer);
        }
    }

    OpenMainMenu(player, "teams");
}




[ConsoleCommand("team.setdefault")]
private void SetDefaultTeam(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin == null || args.Args.Length < 1) return;

    string teamName = args.Args[0];

    // Validate the team name
    if (!teamNames.Contains(teamName)) return;

    // Update the default team
    defaultTeam = teamName;

    // Save the default team in the configuration
    Config["DefaultTeam"] = defaultTeam;
    SaveConfig();

    // Refresh the UI for the admin
    OpenMainMenu(admin, "teams");
}


[ConsoleCommand("team.assign")]
private void AssignPlayerToTeam(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin == null || args.Args.Length < 2) return;

    string playerId = args.Args[0];
    string newTeamName = args.Args[1];

    // Validate the team name
    if (!teamNames.Contains(newTeamName)) return;

    // Find the target player
    var targetPlayer = BasePlayer.activePlayerList.FirstOrDefault(p => p.UserIDString == playerId);
    if (targetPlayer == null) return;

    // Update the player's team (admins bypass allowTeamLeave)
    if (playerTeams.ContainsKey(playerId))
    {
        playerTeams[playerId] = newTeamName;
    }
    else
    {
        playerTeams.Add(playerId, newTeamName);
    }

    // Check if the player is already in a team
    if (targetPlayer.currentTeam != 0)
    {
        var oldTeam = RelationshipManager.ServerInstance.FindTeam(targetPlayer.currentTeam);
        if (oldTeam != null)
        {
            oldTeam.RemovePlayer(targetPlayer.userID);
            UpdateTeamMembers(oldTeam);
        }
    }

    // Find or create the new team
    RelationshipManager.PlayerTeam newTeam;
    if (!teamMapping.ContainsKey(newTeamName))
    {
        newTeam = RelationshipManager.ServerInstance.CreateTeam();
        newTeam.teamName = newTeamName;
        teamMapping[newTeamName] = newTeam.teamID;
    }
    else
    {
        newTeam = RelationshipManager.ServerInstance.FindTeam(teamMapping[newTeamName]);
    }

    // Add the player to the new team
    newTeam.AddPlayer(targetPlayer);
    targetPlayer.currentTeam = newTeam.teamID;
    UpdateTeamMembers(newTeam);

    // Notify the player
    targetPlayer.ChatMessage($"You have been added to the team: {newTeamName}");

    // Refresh the UI for the admin
    OpenMainMenu(admin, "teams");
}







private void UpdateTeamMembers(RelationshipManager.PlayerTeam team)
{
    foreach (var memberId in team.members)
    {
        var member = BasePlayer.FindByID(memberId);
        if (member != null)
        {
            member.SendNetworkUpdateImmediate();
            SendTeamInfo(member, team);
        }
    }
}

private void SendTeamInfo(BasePlayer player, RelationshipManager.PlayerTeam team)
{
    var members = new List<object>();
    foreach (var memberId in team.members)
    {
        var member = BasePlayer.FindByID(memberId);
        if (member != null)
        {
            members.Add(new
            {
                userid = memberId,
                username = member.displayName,
                online = true,
                avatar = string.Empty // You can add avatar URL logic if needed
            });
        }
    }

    var teamInfo = new
    {
        teamID = team.teamID,
        teamName = team.teamName,
        members
    };

    player.ClientRPCPlayer(null, player, "CLIENT_ReceiveTeamInfo", teamInfo);
}





private void AddPermissionsTabContent(CuiElementContainer container, string parent, int currentPage = 0)
{
    const float buttonHeight = 0.05f;
    const float fullButtonWidth = 0.15f;  // Full width for individual player buttons
    const float halfButtonWidth = fullButtonWidth / 2;  // Half width for Grant/Revoke buttons
    const float spacingBetweenPairs = 0.02f;  // Space between revoke -> next grant
    const float rowSpacing = 0.06f;
    const float startY = 0.85f;
    const int rowsPerPage = 10;

    var entries = new List<string>();
    entries.AddRange(teamNames);  // Add teams
    entries.AddRange(BasePlayer.activePlayerList.Select(p => p.UserIDString));  // Add players

    int totalPages = (int)Math.Ceiling((double)entries.Count / rowsPerPage);
    int startRow = currentPage * rowsPerPage;

    // GLOBAL ROW (Handles all players)
    float yMinGlobal = startY;
    float yMaxGlobal = yMinGlobal + buttonHeight;

    container.Add(new CuiLabel
    {
        RectTransform = { AnchorMin = $"0.1 {yMinGlobal}", AnchorMax = $"0.3 {yMaxGlobal}" },
        Text = { Text = "Global", FontSize = 20, Align = UnityEngine.TextAnchor.MiddleLeft }
    }, parent);

    for (int j = 0; j < availablePerms.Count; j++)
    {
        float xStart = 0.35f + j * (fullButtonWidth + spacingBetweenPairs);

        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = $"{xStart} {yMinGlobal}", AnchorMax = $"{xStart + halfButtonWidth} {yMaxGlobal}" },
            Button = { Command = $"perm.globalgrant {availablePerms[j]}", Color = "0.2 0.8 0.2 1.0" },
            Text = { Text = $"Grant", FontSize = 14, Align = UnityEngine.TextAnchor.MiddleCenter }
        }, parent);

        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = $"{xStart + halfButtonWidth} {yMinGlobal}", AnchorMax = $"{xStart + fullButtonWidth} {yMaxGlobal}" },
            Button = { Command = $"perm.globalrevoke {availablePerms[j]}", Color = "0.8 0.2 0.2 1.0" },
            Text = { Text = $"Revoke", FontSize = 14, Align = UnityEngine.TextAnchor.MiddleCenter }
        }, parent);
    }

    // TEAM AND PLAYER ROWS
    for (int i = 0; i < rowsPerPage; i++)
    {
        int index = startRow + i;
        if (index >= entries.Count) break;

        string entry = entries[index];
        float yMin = startY - (i + 1) * rowSpacing;
        float yMax = yMin + buttonHeight;

        if (teamNames.Contains(entry))
        {
            // Team row with Grant/Revoke buttons
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.1 {yMin}", AnchorMax = $"0.3 {yMax}" },
                Text = { Text = entry, FontSize = 20, Align = UnityEngine.TextAnchor.MiddleLeft }
            }, parent);

            for (int j = 0; j < availablePerms.Count; j++)
            {
                float xStart = 0.35f + j * (fullButtonWidth + spacingBetweenPairs);

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{xStart} {yMin}", AnchorMax = $"{xStart + halfButtonWidth} {yMax}" },
                    Button = { Command = $"perm.teamgrant {entry} {availablePerms[j]}", Color = "0.2 0.8 0.2 1.0" },
                    Text = { Text = $"Grant", FontSize = 14, Align = UnityEngine.TextAnchor.MiddleCenter }
                }, parent);

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{xStart + halfButtonWidth} {yMin}", AnchorMax = $"{xStart + fullButtonWidth} {yMax}" },
                    Button = { Command = $"perm.teamrevoke {entry} {availablePerms[j]}", Color = "0.8 0.2 0.2 1.0" },
                    Text = { Text = $"Revoke", FontSize = 14, Align = UnityEngine.TextAnchor.MiddleCenter }
                }, parent);
            }
        }
        else
        {
            // Individual player row with full-width permission buttons
            string displayName = BasePlayer.FindByID(ulong.Parse(entry))?.displayName ?? "Unknown Player";

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.1 {yMin}", AnchorMax = $"0.3 {yMax}" },
                Text = { Text = displayName, FontSize = 20, Align = UnityEngine.TextAnchor.MiddleLeft }
            }, parent);

            for (int j = 0; j < availablePerms.Count; j++)
            {
                float xStart = 0.35f + j * (fullButtonWidth + spacingBetweenPairs);
                bool hasPerm = permission.UserHasPermission(entry, availablePerms[j]);

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{xStart} {yMin}", AnchorMax = $"{xStart + fullButtonWidth} {yMax}" },
                    Button = { Command = $"perm.toggle {entry} {availablePerms[j]}", Color = hasPerm ? "0.2 0.8 0.2 1.0" : "0.8 0.2 0.2 1.0" },
                    Text = { Text = availablePerms[j], FontSize = 18, Align = UnityEngine.TextAnchor.MiddleCenter }
                }, parent);
            }
        }
    }
}



private const string BypassPermission = "eventcontrolui.bypass";

[ConsoleCommand("perm.globalgrant")]
private void GrantGlobalPermission(ConsoleSystem.Arg args)
{
    if (args.Connection?.player is not BasePlayer admin || args.Args.Length < 1)
    {
        
        return;
    }

    string perm = args.Args[0].Trim();
    int grantedCount = 0;

    foreach (var player in BasePlayer.activePlayerList)
    {
        if (permission.UserHasPermission(player.UserIDString, BypassPermission))
        {
            SendReply(admin, $"Skipping {player.displayName} (bypass enabled)");
            continue;
        }

        permission.GrantUserPermission(player.UserIDString, perm, null);

        if (permission.UserHasPermission(player.UserIDString, perm))
        {
            grantedCount++;
        }
        else
        {
            Puts($"[DEBUG] Failed to grant {perm} to {player.displayName}");
        }
    }

    SendReply(admin, $"Granted {perm} to {grantedCount} players (excluding bypassed).");
    OpenMainMenu(admin, "permissions");
}

[ConsoleCommand("perm.globalrevoke")]
private void RevokeGlobalPermission(ConsoleSystem.Arg args)
{
    if (args.Connection?.player is not BasePlayer admin || args.Args.Length < 1)
    {
       
        return;
    }

    string perm = args.Args[0].Trim();
    int revokedCount = 0;

    foreach (var player in BasePlayer.activePlayerList)
    {
        if (permission.UserHasPermission(player.UserIDString, BypassPermission))
        {
            SendReply(admin, $"Skipping {player.displayName} (bypass enabled)");
            continue;
        }

        permission.RevokeUserPermission(player.UserIDString, perm);

        if (!permission.UserHasPermission(player.UserIDString, perm))
        {
            revokedCount++;
        }
        else
        {
            Puts($"[DEBUG] Failed to revoke {perm} from {player.displayName}");
        }
    }

    SendReply(admin, $"Revoked {perm} from {revokedCount} players (excluding bypassed).");
    OpenMainMenu(admin, "permissions");
}


private Dictionary<string, TimerGroup> timerGroups = new Dictionary<string, TimerGroup>();

public class TimerGroup
{
    public string GroupName { get; set; }
    public float RemainingTime { get; set; }
    public bool IsRunning { get; set; }
    public bool IsUIVisible { get; set; }
    public bool RaidingOnZero { get; set; }
    public bool BuildingOnZero { get; set; }
    public bool PvPOnZero { get; set; }

    public TimerGroup(string groupName)
    {
        GroupName = groupName;
        RemainingTime = 0f;
        IsRunning = false;
        IsUIVisible = true;
        RaidingOnZero = false;
        BuildingOnZero = false;
        PvPOnZero = false;
    }
}

private void AddGlobalTabContent(CuiElementContainer container, string parent)
{
    // Row 1: Set World Spawn, Heal All, Kill All
    float row1YMin = 0.85f;
    float row1YMax = 0.9f;
    float buttonWidth = 0.25f;
    float buttonHeight = 0.05f;
    float spacing = 0.02f;

    // Set World Spawn Button
    container.Add(new CuiButton
    {
        RectTransform = { AnchorMin = "0.1 0.85", AnchorMax = $"{0.1 + buttonWidth} {row1YMax}" },
        Button = { Command = "global.setworldspawn", Color = "0.2 0.5 0.8 1.0" },
        Text = { Text = "Set World Spawn", FontSize = 20, Align = TextAnchor.MiddleCenter }
    }, parent);

    // Heal All Button
    container.Add(new CuiButton
    {
        RectTransform = { AnchorMin = $"{0.1 + buttonWidth + spacing} 0.85", AnchorMax = $"{0.1 + 2 * buttonWidth + spacing} {row1YMax}" },
        Button = { Command = "global.healall", Color = "0.2 0.8 0.2 1.0" },
        Text = { Text = "Heal All", FontSize = 20, Align = TextAnchor.MiddleCenter }
    }, parent);

    // Kill All Button
    container.Add(new CuiButton
    {
        RectTransform = { AnchorMin = $"{0.1 + 2 * buttonWidth + 2 * spacing} 0.85", AnchorMax = $"{0.1 + 3 * buttonWidth + 2 * spacing} {row1YMax}" },
        Button = { Command = "global.killall", Color = "0.8 0.2 0.2 1.0" },
        Text = { Text = "Kill All", FontSize = 20, Align = TextAnchor.MiddleCenter }
    }, parent);

    // Define the actions for each row
    List<string> actionButtons = new List<string> { "Freeze", "Raiding", "PvP", "God Mode", "Building", "Loot" };
List<string> actionCommands = new List<string> { "freeze", "toggle.raiding", "toggle.pvp", "toggle.godmode", "toggle.building", "toggle.loot" };



    // Row 2: Global actions
    float currentY = row1YMin - 0.1f;
    AddActionRow(container, parent, "Global", actionButtons, actionCommands, 0.1f, currentY);

    // Rows 3-6: Team actions
    for (int i = 0; i < teamNames.Count; i++)
    {
        currentY -= 0.07f;
        AddActionRow(container, parent, teamNames[i], actionButtons, actionCommands, 0.1f, currentY, isTeam: true);
    }
}

private void AddActionRow(CuiElementContainer container, string parent, string rowLabel, List<string> actions, List<string> commands, float xStart, float y, bool isTeam = false)
{
    // Row Label
    container.Add(new CuiLabel
    {
        RectTransform = { AnchorMin = $"{xStart} {y + 0.04f}", AnchorMax = $"{xStart + 0.1f} {y + 0.09f}" },
        Text = { Text = $"[{rowLabel}]", FontSize = 20, Align = TextAnchor.MiddleLeft }
    }, parent);

    // Action Buttons
    float buttonWidth = 0.1f;
    float buttonHeight = 0.05f;
    float spacing = 0.02f;
    float currentX = xStart + 0.12f;

    for (int i = 0; i < actions.Count; i++)
    {
        string action = actions[i];
        string command = commands[i];

        // Determine the current state of the action
        bool isEnabled = false;
switch (action)
{
    case "Freeze":
         // Set isEnabled for Freeze, e.g.:
         isEnabled = isTeam ? (teamFreezeStatus.ContainsKey(rowLabel) && teamFreezeStatus[rowLabel])
                            : globalFreezeEnabled;
         break;  // Ensure break is present here
    case "Loot":
         isEnabled = isTeam ? (teamLootStatus.ContainsKey(rowLabel) && teamLootStatus[rowLabel])
                            : lootAllowed;
         break;
            case "Raiding":
                isEnabled = isTeam ? teamRaidingStatus.ContainsKey(rowLabel) && teamRaidingStatus[rowLabel]
                                   : raidingEnabled;
                break;
            case "PvP":
                isEnabled = isTeam ? teamPvPStatus.ContainsKey(rowLabel) && teamPvPStatus[rowLabel]
                                   : pvpEnabled;
                break;
            case "God Mode":
                isEnabled = isTeam ? teamGodModeStatus.ContainsKey(rowLabel) && teamGodModeStatus[rowLabel]
                                   : godModeEnabled;
                break;
            case "Building":
                isEnabled = isTeam ? teamBuildingStatus.ContainsKey(rowLabel) && teamBuildingStatus[rowLabel]
                                   : buildingEnabled;
                break;
            default:
                break;
        }

        // Set button color based on state
        string color = isEnabled ? "0.2 0.8 0.2 1.0" : "0.8 0.2 0.2 1.0"; // Green for on, Red for off

        // Define the command based on whether it's Global or Team
        string fullCommand = "";
        switch (action)
        {
            case "Freeze":
                fullCommand = isTeam ? $"global.freeze.team {rowLabel}" : $"global.freeze";
                break;
            case "Raiding":
                fullCommand = isTeam ? $"global.toggle.raiding.team {rowLabel}" : $"global.toggle.raiding";
                break;
            case "PvP":
                fullCommand = isTeam ? $"global.toggle.pvp.team {rowLabel}" : $"global.toggle.pvp";
                break;
            case "God Mode":
                fullCommand = isTeam ? $"global.toggle.godmode.team {rowLabel}" : $"global.toggle.godmode";
                break;
            case "Building":
                fullCommand = isTeam ? $"global.toggle.building.team {rowLabel}" : $"global.toggle.building";
                break;
            default:
                fullCommand = $"global.{command.ToLower()}";
                break;
        }

        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = $"{currentX} {y}", AnchorMax = $"{currentX + buttonWidth} {y + buttonHeight}" },
            Button = { Command = fullCommand, Color = color },
            Text = { Text = action, FontSize = 18, Align = TextAnchor.MiddleCenter }
        }, parent);

        currentX += buttonWidth + spacing;
    }
}

// Global Permission Flag
private bool globalPermEnabled;

// Global Settings
private bool globalFreezeEnabled;
private bool globalBuildingEnabled;
private bool globalRaidingEnabled;
private bool globalPvPEnabled;

// Global feature states
private bool raidingEnabled = true;
private bool pvpEnabled = true;
private bool godModeEnabled = false;
private bool buildingEnabled = true;

// Team-specific feature states
private Dictionary<string, bool> teamFreezeStatus = new Dictionary<string, bool>();
private Dictionary<string, bool> teamRaidingStatus = new Dictionary<string, bool>();
private Dictionary<string, bool> teamPvPStatus = new Dictionary<string, bool>();
private Dictionary<string, bool> teamGodModeStatus = new Dictionary<string, bool>();
private Dictionary<string, bool> teamBuildingStatus = new Dictionary<string, bool>();

private void LoadConfigValues()
{
    // Load global permissions
    globalPermEnabled = Convert.ToBoolean(Config["GlobalPermEnabled"] ?? false);
    globalFreezeEnabled = Convert.ToBoolean(Config["GlobalFreezeEnabled"] ?? false);
    globalBuildingEnabled = Convert.ToBoolean(Config["GlobalBuildingEnabled"] ?? true);
    globalRaidingEnabled = Convert.ToBoolean(Config["GlobalRaidingEnabled"] ?? true);
    globalPvPEnabled = Convert.ToBoolean(Config["GlobalPvPEnabled"] ?? true);

    // Load team-specific settings
    teamFreezeStatus = Config["TeamFreezeStatus"] as Dictionary<string, bool> ?? new Dictionary<string, bool>();
    teamBuildingStatus = Config["TeamBuildingStatus"] as Dictionary<string, bool> ?? new Dictionary<string, bool>();
    teamRaidingStatus = Config["TeamRaidingStatus"] as Dictionary<string, bool> ?? new Dictionary<string, bool>();
    teamPvPStatus = Config["TeamPvPStatus"] as Dictionary<string, bool> ?? new Dictionary<string, bool>();
}


[ConsoleCommand("global.toggle.loot")]
private void ToggleGlobalLoot(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin == null || !permission.UserHasPermission(admin.UserIDString, PermissionManagePerms))
    {
         SendReply(admin, "You don't have permission to execute this command.");
         return;
    }
    lootAllowed = !lootAllowed;
    Config["LootAllowed"] = lootAllowed;
    SaveConfig();

    foreach (var player in BasePlayer.activePlayerList)
    {
         player.ChatMessage($"Looting has been {(lootAllowed ? "enabled" : "disabled")} globally by an administrator.");
    }
    SendReply(admin, $"Global loot is now {(lootAllowed ? "enabled" : "disabled")}.");
    OpenMainMenu(admin, "global");
}

[ConsoleCommand("global.toggle.loot.team")]
private void ToggleTeamLoot(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin == null || args.Args.Length < 1 || !permission.UserHasPermission(admin.UserIDString, PermissionManagePerms))
    {
         SendReply(admin, "You don't have permission to execute this command.");
         return;
    }
    string teamName = args.Args[0];

    if (!teamNames.Contains(teamName))
    {
         admin.ChatMessage($"Team {teamName} does not exist.");
         return;
    }

    bool current = true;
    if (teamLootStatus.ContainsKey(teamName))
         current = teamLootStatus[teamName];
    // Toggle the team loot setting.
    teamLootStatus[teamName] = !current;
    Config["TeamLootStatus"] = teamLootStatus;
    SaveConfig();

    foreach (var kvp in playerTeams.Where(kvp => kvp.Value == teamName))
    {
         var player = BasePlayer.FindByID(ulong.Parse(kvp.Key));
         if (player != null)
         {
              player.ChatMessage($"Looting has been {(teamLootStatus[teamName] ? "enabled" : "disabled")} for your team by an administrator.");
         }
    }
    admin.ChatMessage($"Looting is now {(teamLootStatus[teamName] ? "enabled" : "disabled")} for team {teamName}.");
    OpenMainMenu(admin, "global");
}

private bool IsLootAllowed(BasePlayer player)
{
    // First, check global loot setting.
    if (!lootAllowed)
         return false;
    // If the player belongs to a team, check team-specific loot.
    if (playerTeams.TryGetValue(player.UserIDString, out string team))
    {
         if (teamLootStatus.TryGetValue(team, out bool teamAllowed))
              return teamAllowed;
    }
    return true;
}

object CanLootEntity(BasePlayer player, DroppedItemContainer container)
{
    if (!IsLootAllowed(player))
    {
         // Optionally notify or log:
         Puts($"Loot blocked for player {player.displayName} on DroppedItemContainer.");
         return true; // Return non-null to block looting.
    }
    return null;
}

object CanLootEntity(BasePlayer player, LootableCorpse corpse)
{
    if (!IsLootAllowed(player))
    {
         Puts($"Loot blocked for player {player.displayName} on LootableCorpse.");
         return true;
    }
    return null;
}

object CanLootEntity(BasePlayer player, ResourceContainer container)
{
    if (!IsLootAllowed(player))
    {
         Puts($"Loot blocked for player {player.displayName} on ResourceContainer.");
         return true;
    }
    return null;
}

object CanLootEntity(BasePlayer player, BaseRidableAnimal animal)
{
    if (!IsLootAllowed(player))
    {
         Puts($"Loot blocked for player {player.displayName} on BaseRidableAnimal.");
         return true;
    }
    return null;
}

object CanLootEntity(BasePlayer player, StorageContainer container)
{
    if (!IsLootAllowed(player))
    {
         Puts($"Loot blocked for player {player.displayName} on StorageContainer.");
         return true;
    }
    return null;
}


[ConsoleCommand("global.toggle.raiding.team")]
private void ToggleRaidingTeam(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin == null || args.Args.Length < 1 || !permission.UserHasPermission(admin.UserIDString, PermissionManagePerms))
    {
        SendReply(admin, "You don't have permission to execute this command.");
        return;
    }

    string teamName = args.Args[0];

    if (!teamNames.Contains(teamName))
    {
        admin.ChatMessage($"Team {teamName} does not exist.");
        return;
    }

    teamRaidingStatus[teamName] = !teamRaidingStatus[teamName];
    Config["TeamRaidingStatus"] = teamRaidingStatus;
    SaveConfig();

    foreach (var kvp in playerTeams.Where(kvp => kvp.Value == teamName))
    {
        var player = BasePlayer.FindByID(ulong.Parse(kvp.Key));
        if (player != null)
        {
            player.ChatMessage($"Raiding has been {(teamRaidingStatus[teamName] ? "enabled" : "disabled")} for your team by an administrator.");
        }
    }

    admin.ChatMessage($"Raiding is now {(teamRaidingStatus[teamName] ? "enabled" : "disabled")} for team {teamName}.");
    OpenMainMenu(admin, "global");
}



[ConsoleCommand("global.healall")]
private void HealAll(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin == null || !permission.UserHasPermission(admin.UserIDString, PermissionManagePerms))
    {
        SendReply(admin, "You don't have permission to execute this command.");
        return;
    }

    foreach (var player in BasePlayer.activePlayerList)
    {
        if (player != null && player.IsConnected)
        {
            // Restore health
            player.health = player.MaxHealth();

            // Restore metabolism stats with null checks
            if (player.metabolism != null)
            {
                if (player.metabolism.bleeding != null)
                    player.metabolism.bleeding.value = 0;

                if (player.metabolism.calories != null)
                    player.metabolism.calories.value = player.metabolism.calories.max;

                if (player.metabolism.hydration != null)
                    player.metabolism.hydration.value = player.metabolism.hydration.max;

                if (player.metabolism.wetness != null)
                    player.metabolism.wetness.value = player.metabolism.wetness.max;
            }

            // Send immediate network update to reflect changes
            player.SendNetworkUpdateImmediate();

            // Notify the player
            player.ChatMessage("You have been healed by an administrator.");
        }
    }

    // Notify the admin
    admin.ChatMessage("All players have been healed.");
}



[ConsoleCommand("global.killall")]
private void KillAll(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin == null || !permission.UserHasPermission(admin.UserIDString, PermissionManagePerms))
    {
        SendReply(admin, "You don't have permission to execute this command.");
        return;
    }

    foreach (var player in BasePlayer.activePlayerList)
    {
        if (player != null && player.IsConnected)
        {
            player.Hurt(player.MaxHealth() * 2f); // Ensure the player dies
            player.ChatMessage("You have been killed by an administrator.");
        }
    }

    // Notify the admin
    admin.ChatMessage("All players have been killed.");
}



        private void AddTeleportTabContent(CuiElementContainer container, string parent, int page = 0)
{
    const int playersPerPage = 25;
    const float buttonHeight = 0.05f; // Height for all buttons
    const float buttonWidth = 0.15f; // Adjusted width for smaller buttons

    // Teleport All Button
    container.Add(new CuiButton
    {
        RectTransform = { AnchorMin = "0.1 0.85", AnchorMax = $"{0.1 + buttonWidth} 0.9" },
        Button = { Command = "teleport.all", Color = "0.2 0.5 0.8 1.0" },
        Text = { Text = "Teleport All", FontSize = 24, Align = UnityEngine.TextAnchor.MiddleCenter }
    }, parent);

    // Teleport Teams Buttons
    int teamIndex = 0;
    for (int i = 0; i < teamNames.Count; i++)
    {
        float xMin = 0.1f + (teamIndex % 5) * (buttonWidth + 0.02f); // Place up to 5 buttons per row
        float xMax = xMin + buttonWidth;
        float yMin = 0.75f - (teamIndex / 5) * (buttonHeight + 0.02f); // Move to next row if necessary
        float yMax = yMin + buttonHeight;

        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = $"{xMin} {yMin}", AnchorMax = $"{xMax} {yMax}" },
            Button = { Command = $"teleport.team {teamNames[i]}", Color = "0.2 0.5 0.8 1.0" },
            Text = { Text = $"Teleport {teamNames[i]}", FontSize = 20, Align = UnityEngine.TextAnchor.MiddleCenter }
        }, parent);

        teamIndex++;
    }

    // Player Teleport Buttons (Paginated)
    var players = BasePlayer.activePlayerList.Skip(page * playersPerPage).Take(playersPerPage).ToList();
    int playerIndex = 0;
    foreach (var player in players)
    {
        float xMin = 0.1f + (playerIndex % 3) * (buttonWidth + 0.02f); // Place up to 3 buttons per row
        float xMax = xMin + buttonWidth;
        float yMin = 0.6f - (playerIndex / 3) * (buttonHeight + 0.02f); // Move to next row if necessary
        float yMax = yMin + buttonHeight;

        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = $"{xMin} {yMin}", AnchorMax = $"{xMax} {yMax}" },
            Button = { Command = $"teleport.player {player.UserIDString}", Color = "0.2 0.5 0.8 1.0" },
            Text = { Text = player.displayName, FontSize = 18, Align = UnityEngine.TextAnchor.MiddleCenter }
        }, parent);

        playerIndex++;
    }

    // Pagination Arrows
    if (page > 0)
    {
        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = "0.1 0.05", AnchorMax = "0.2 0.1" },
            Button = { Command = $"teleport.page {page - 1}", Color = "0.2 0.5 0.8 1.0" },
            Text = { Text = "<< Previous", FontSize = 20, Align = UnityEngine.TextAnchor.MiddleCenter }
        }, parent);
    }

    if (BasePlayer.activePlayerList.Count > (page + 1) * playersPerPage)
    {
        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = "0.8 0.05", AnchorMax = "0.9 0.1" },
            Button = { Command = $"teleport.page {page + 1}", Color = "0.2 0.5 0.8 1.0" },
            Text = { Text = "Next >>", FontSize = 20, Align = UnityEngine.TextAnchor.MiddleCenter }
        }, parent);
    }
}



[ConsoleCommand("global.toggle.raiding")]
private void ToggleRaiding(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin == null || !permission.UserHasPermission(admin.UserIDString, PermissionManagePerms))
    {
        SendReply(admin, "You don't have permission to execute this command.");
        return;
    }

    raidingEnabled = !raidingEnabled;
    Config["RaidingEnabled"] = raidingEnabled;
    SaveConfig();

    foreach (var player in BasePlayer.activePlayerList)
    {
        if (raidingEnabled)
            player.ChatMessage("Raiding has been enabled globally by an administrator.");
        else
            player.ChatMessage("Raiding has been disabled globally by an administrator.");
    }

    admin.ChatMessage($"Raiding is now {(raidingEnabled ? "enabled" : "disabled")} globally.");
    OpenMainMenu(admin, "global");
}


[ConsoleCommand("perm.page")]
private void HandlePermissionPageCommand(ConsoleSystem.Arg args)
{
    BasePlayer player = args.Connection?.player as BasePlayer;
    if (player == null) return;

    int page = args.Args.Length > 0 ? int.Parse(args.Args[0]) : 0;
    OpenMainMenu(player, "permissions", null, null, page);

}



void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
{
    if (info == null) return;

    // Disable raiding damage
    if (!raidingEnabled && entity is BuildingBlock)
    {
        info.damageTypes.Clear();
        info.HitEntity = null;
        info.HitMaterial = 0;
        return;
    }

   
     // Apply God Mode logic
    if (entity is BasePlayer player)
    {
        // God Mode logic
        if (godModeEnabled && godModePlayers.Contains(player.UserIDString))
        {
            info.damageTypes.Clear();
            info.DoHitEffects = false;
            info.HitEntity = null;
            return; // Prevent further damage processing
        }
    }

    // Disable PvP damage
    if (!pvpEnabled && entity is BasePlayer victim && info?.InitiatorPlayer is BasePlayer)
    {
        info.damageTypes.Clear();
        info.DoHitEffects = false;
        info.HitEntity = null;
        return;
    }

    

    // Check for friendly fire
    if (entity is BasePlayer teammateVictim && info?.InitiatorPlayer is BasePlayer teammateAttacker)
    {
        if (!friendlyFire && teammateVictim.currentTeam != 0 && teammateVictim.currentTeam == teammateAttacker.currentTeam)
        {
            info.damageTypes.Clear();
            info.DoHitEffects = false;
            info.HitEntity = null;
            return;
        }
    }
}




[ConsoleCommand("perm.toggle")]
private void TogglePermission(ConsoleSystem.Arg args)
{
    if (args.Connection?.player is not BasePlayer admin || args.Args.Length < 2)
    {
        Puts("Invalid command usage. Correct format: perm.toggle <playerId> <perm>");
        return;
    }

    string targetId = args.Args[0].Trim();
    string perm = args.Args[1].Trim();

    // Ensure valid player ID format
    if (!ulong.TryParse(targetId, out _))
    {
        SendReply(admin, "Invalid player ID format.");
        return;
    }

    // Check if permission exists in the config
    if (!availablePerms.Contains(perm))
    {
        SendReply(admin, $"Permission {perm} is not available.");
        return;
    }

    // Toggle permission and update the UI
    if (permission.UserHasPermission(targetId, perm))
    {
        permission.RevokeUserPermission(targetId, perm);
        SendReply(admin, $"Permission {perm} removed from {targetId}.");
    }
    else
    {
        permission.GrantUserPermission(targetId, perm, null);

        if (permission.UserHasPermission(targetId, perm))
        {
            SendReply(admin, $"Permission {perm} successfully granted to {targetId}.");
        }
        else
        {
            SendReply(admin, $"Failed to grant permission {perm} to {targetId}.");
        }
    }

    // Refresh the UI by reopening the permissions tab
    OpenMainMenu(admin, "permissions");
}


[ConsoleCommand("perm.teamgrant")]
private void GrantTeamPermission(ConsoleSystem.Arg args)
{
    if (args.Connection?.player is not BasePlayer admin || args.Args.Length < 2)
    {
      
        return;
    }

    string teamName = args.Args[0].Trim();
    string perm = args.Args[1].Trim();

    if (!teamNames.Contains(teamName))
    {
        SendReply(admin, $"Team {teamName} does not exist.");
        return;
    }

    int grantedCount = 0;

    foreach (var kvp in playerTeams.Where(kvp => kvp.Value == teamName))
    {
        string playerId = kvp.Key.Trim();
        
        // Validate player ID format
        if (!ulong.TryParse(playerId, out _))
        {
            Puts($"[ERROR] Invalid player ID format: {playerId}");
            continue;
        }

        // Grant permission via Oxide API
        permission.GrantUserPermission(playerId, perm, null);

        // Verify permission grant
        if (permission.UserHasPermission(playerId, perm))
        {
            grantedCount++;
        }
        else
        {
            Puts($"[DEBUG] Failed to grant {perm} to {playerId}");
        }
    }

    SendReply(admin, $"Granted permission {perm} to {grantedCount} members of {teamName}.");
    OpenMainMenu(admin, "permissions");
}



[ConsoleCommand("perm.teamrevoke")]
private void RevokeTeamPermission(ConsoleSystem.Arg args)
{
    if (args.Connection?.player is not BasePlayer admin || args.Args.Length < 2)
    {
       
        return;
    }

    string teamName = args.Args[0].Trim();
    string perm = args.Args[1].Trim();

    if (!teamNames.Contains(teamName))
    {
        SendReply(admin, $"Team {teamName} does not exist.");
        return;
    }

    int revokedCount = 0;
    foreach (var kvp in playerTeams.Where(kvp => kvp.Value == teamName))
    {
        string playerId = kvp.Key;

        // Revoke permission via Oxide API
        permission.RevokeUserPermission(playerId, perm);
        
        // Verify if permission was successfully revoked
        if (!permission.UserHasPermission(playerId, perm))
        {
            revokedCount++;
        }
        else
        {
            Puts($"[DEBUG] Failed to revoke {perm} from {playerId}");
        }
    }

    SendReply(admin, $"Revoked permission {perm} from {revokedCount} members of {teamName}.");
    OpenMainMenu(admin, "permissions");
}



[ConsoleCommand("global.toggle.pvp")]
private void TogglePvP(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin == null || !permission.UserHasPermission(admin.UserIDString, PermissionManagePerms))
    {
        SendReply(admin, "You don't have permission to execute this command.");
        return;
    }

    pvpEnabled = !pvpEnabled;
    Config["PvPEnabled"] = pvpEnabled;
    SaveConfig();

    foreach (var player in BasePlayer.activePlayerList)
    {
        if (pvpEnabled)
            player.ChatMessage("PvP has been enabled globally by an administrator.");
        else
            player.ChatMessage("PvP has been disabled globally by an administrator.");
    }

    admin.ChatMessage($"PvP is now {(pvpEnabled ? "enabled" : "disabled")} globally.");
    OpenMainMenu(admin, "global");
}

[ConsoleCommand("global.toggle.pvp.team")]
private void TogglePvPTeam(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin == null || args.Args.Length < 1 || !permission.UserHasPermission(admin.UserIDString, PermissionManagePerms))
    {
        SendReply(admin, "You don't have permission to execute this command.");
        return;
    }

    string teamName = args.Args[0];

    if (!teamNames.Contains(teamName))
    {
        admin.ChatMessage($"Team {teamName} does not exist.");
        return;
    }

    teamPvPStatus[teamName] = !teamPvPStatus[teamName];
    Config["TeamPvPStatus"] = teamPvPStatus;
    SaveConfig();

    foreach (var kvp in playerTeams.Where(kvp => kvp.Value == teamName))
    {
        var player = BasePlayer.FindByID(ulong.Parse(kvp.Key));
        if (player != null)
        {
            player.ChatMessage($"PvP has been {(teamPvPStatus[teamName] ? "enabled" : "disabled")} for your team by an administrator.");
        }
    }

    admin.ChatMessage($"PvP is now {(teamPvPStatus[teamName] ? "enabled" : "disabled")} for team {teamName}.");
    OpenMainMenu(admin, "global");
}

private HashSet<string> frozenTeams = new HashSet<string>();

private HashSet<string> godModePlayers = new HashSet<string>(); // Players in God Mode

[ConsoleCommand("global.toggle.godmode")]
private void ToggleGodMode(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin == null || !permission.UserHasPermission(admin.UserIDString, PermissionManagePerms))
    {
        SendReply(admin, "You don't have permission to execute this command.");
        return;
    }

    godModeEnabled = !godModeEnabled;
    Config["GodModeEnabled"] = godModeEnabled;
    SaveConfig();

    foreach (var player in BasePlayer.activePlayerList)
    {
        if (player != null)
        {
            // Implement God Mode logic here
            if (godModeEnabled)
            {
                godModePlayers.Add(player.UserIDString);
                player.ChatMessage("God Mode has been enabled globally by an administrator.");
            }
            else
            {
                godModePlayers.Remove(player.UserIDString);
                player.ChatMessage("God Mode has been disabled globally by an administrator.");
            }
        }
    }

    admin.ChatMessage($"God Mode is now {(godModeEnabled ? "enabled" : "disabled")} globally.");
    OpenMainMenu(admin, "global");
}

[ConsoleCommand("global.toggle.godmode.team")]
private void ToggleGodModeTeam(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin == null || args.Args.Length < 1 || !permission.UserHasPermission(admin.UserIDString, PermissionManagePerms))
    {
        SendReply(admin, "You don't have permission to execute this command.");
        return;
    }

    string teamName = args.Args[0];

    if (!teamNames.Contains(teamName))
    {
        admin.ChatMessage($"Team {teamName} does not exist.");
        return;
    }

    teamGodModeStatus[teamName] = !teamGodModeStatus[teamName];
    Config["TeamGodModeStatus"] = teamGodModeStatus;
    SaveConfig();

    foreach (var kvp in playerTeams.Where(kvp => kvp.Value == teamName))
    {
        var player = BasePlayer.FindByID(ulong.Parse(kvp.Key));
        if (player != null)
        {
            if (teamGodModeStatus[teamName])
            {
                godModePlayers.Add(player.UserIDString);
                player.ChatMessage("God Mode has been enabled for your team by an administrator.");
            }
            else
            {
                godModePlayers.Remove(player.UserIDString);
                player.ChatMessage("God Mode has been disabled for your team by an administrator.");
            }
        }
    }

    admin.ChatMessage($"God Mode is now {(teamGodModeStatus[teamName] ? "enabled" : "disabled")} for team {teamName}.");
    OpenMainMenu(admin, "global");
}



[ConsoleCommand("godmode.enable")]
private void EnableGodMode(ConsoleSystem.Arg args)
{
    if (args.Args.Length < 1)
    {
        args.ReplyWith("Usage: godmode.enable <playerID>");
        return;
    }

    string playerId = args.Args[0];
    var player = BasePlayer.FindByID(ulong.Parse(playerId));
    if (player == null)
    {
        args.ReplyWith($"Player with ID {playerId} not found.");
        return;
    }

    godModePlayers.Add(playerId);
    player.ChatMessage("God Mode has been enabled for you.");
}

[ConsoleCommand("godmode.disable")]
private void DisableGodMode(ConsoleSystem.Arg args)
{
    if (args.Args.Length < 1)
    {
        args.ReplyWith("Usage: godmode.disable <playerID>");
        return;
    }

    string playerId = args.Args[0];
    var player = BasePlayer.FindByID(ulong.Parse(playerId));
    if (player == null)
    {
        args.ReplyWith($"Player with ID {playerId} not found.");
        return;
    }

    godModePlayers.Remove(playerId);
    player.ChatMessage("God Mode has been disabled for you.");
}



[ConsoleCommand("global.freezeall")]
private void FreezeAll(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin != null)
    {
        admin.SendConsoleCommand("chat.say", "/freezeall");
        admin.ChatMessage("You executed /freezeall command.");
    }
}



[ConsoleCommand("global.freeze")]
private void ToggleGlobalFreeze(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin == null || !permission.UserHasPermission(admin.UserIDString, PermissionManagePerms))
    {
        SendReply(admin, "You don't have permission to execute this command.");
        return;
    }

    if (frozenTeams.Contains("Global"))
    {
        // Unfreeze all players
        foreach (var player in BasePlayer.activePlayerList)
        {
            if (player != null)
            {
                rust.RunServerCommand($"unfreeze {player.UserIDString}");
                player.ChatMessage("You have been unfrozen by an administrator.");
            }
        }
        frozenTeams.Remove("Global");
        Config["FrozenTeams"] = frozenTeams;
        SaveConfig();
        admin.ChatMessage("All players have been unfrozen globally.");
    }
    else
    {
        // Freeze all players
        foreach (var player in BasePlayer.activePlayerList)
        {
            if (player != null)
            {
                rust.RunServerCommand($"freeze {player.UserIDString}");
                player.ChatMessage("You have been frozen by an administrator.");
            }
        }
        frozenTeams.Add("Global");
        Config["FrozenTeams"] = frozenTeams;
        SaveConfig();
        admin.ChatMessage("All players have been frozen globally.");
    }

    OpenMainMenu(admin, "global");
}


[ConsoleCommand("global.freeze.team")]
private void ToggleFreezeTeam(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin == null || args.Args.Length < 1 || !permission.UserHasPermission(admin.UserIDString, PermissionManagePerms))
    {
        SendReply(admin, "You don't have permission to execute this command.");
        return;
    }

    string teamName = args.Args[0];

    if (!teamNames.Contains(teamName))
    {
        admin.ChatMessage($"Team {teamName} does not exist.");
        return;
    }

    if (teamFreezeStatus[teamName])
    {
        // Unfreeze all players in the team
        foreach (var kvp in playerTeams.Where(kvp => kvp.Value == teamName))
        {
            var player = BasePlayer.FindByID(ulong.Parse(kvp.Key));
            if (player != null)
            {
                rust.RunServerCommand($"unfreeze {player.UserIDString}");
                player.ChatMessage("You have been unfrozen by an administrator.");
            }
        }
        teamFreezeStatus[teamName] = false;
        Config["TeamFreezeStatus"] = teamFreezeStatus;
        SaveConfig();
        admin.ChatMessage($"All players in team {teamName} have been unfrozen.");
    }
    else
    {
        // Freeze all players in the team
        foreach (var kvp in playerTeams.Where(kvp => kvp.Value == teamName))
        {
            var player = BasePlayer.FindByID(ulong.Parse(kvp.Key));
            if (player != null)
            {
                rust.RunServerCommand($"freeze {player.UserIDString}");
                player.ChatMessage("You have been frozen by an administrator.");
            }
        }
        teamFreezeStatus[teamName] = true;
        Config["TeamFreezeStatus"] = teamFreezeStatus;
        SaveConfig();
        admin.ChatMessage($"All players in team {teamName} have been frozen.");
    }

    OpenMainMenu(admin, "global");
}




[ConsoleCommand("global.toggle.building")]
private void ToggleBuilding(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin == null) return;

    buildingEnabled = !buildingEnabled;
    Config["BuildingEnabled"] = buildingEnabled;
    SaveConfig();

    admin.ChatMessage($"Building is now {(buildingEnabled ? "enabled" : "disabled")}.");
    OpenMainMenu(admin, "global");
}

// Hook to control building
object CanBuild(Planner planner, Construction prefab)
{
    if (!buildingEnabled)
    {
        BasePlayer player = planner?.GetOwnerPlayer();
        if (player != null)
        {
            player.ChatMessage("Building is currently disabled.");
        }
        return false;
    }

    return null; // Allow building if enabled
}


[ConsoleCommand("global.unfreezeall")]
private void UnfreezeAll(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin != null)
    {
        admin.SendConsoleCommand("chat.say", "/unfreezeall");
        admin.ChatMessage("You executed /unfreezeall command.");
    }
}

[ConsoleCommand("teleport.all")]
private void TeleportAll(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin == null) return;

    foreach (var player in BasePlayer.activePlayerList)
    {
        if (player != admin)
        {
            player.Teleport(admin.transform.position);
            player.ChatMessage($"You have been teleported to {admin.displayName}.");
        }
    }

    admin.ChatMessage("All players have been teleported to you.");
}

[ConsoleCommand("teleport.team")]
private void TeleportTeam(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin == null || args.Args.Length < 1) return;

    string teamName = args.Args[0];

    foreach (var kvp in playerTeams)
    {
        if (kvp.Value == teamName)
        {
            var player = BasePlayer.FindByID(ulong.Parse(kvp.Key));
            if (player != null && player != admin)
            {
                player.Teleport(admin.transform.position);
                player.ChatMessage($"You have been teleported to {admin.displayName}.");
            }
        }
    }

    admin.ChatMessage($"All players in team {teamName} have been teleported to you.");
}

[ConsoleCommand("global.setworldspawn")]
private void SetWorldSpawn(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin == null) return;

    // Set the world spawn location to the admin's current position
    worldSpawnLocation = admin.transform.position;
    Config["WorldSpawnLocation"] = worldSpawnLocation;
    SaveConfig();

    admin.ChatMessage($"WorldSpawn has been set to your current location: {worldSpawnLocation}");
    OpenMainMenu(admin, "global");
}


[ConsoleCommand("teleport.player")]
private void TeleportPlayer(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin == null || args.Args.Length < 1) return;

    var player = BasePlayer.FindByID(ulong.Parse(args.Args[0]));
    if (player != null && player != admin)
    {
        player.Teleport(admin.transform.position);
        player.ChatMessage($"You have been teleported to {admin.displayName}.");
        admin.ChatMessage($"Player {player.displayName} has been teleported to you.");
    }
}

[ConsoleCommand("teleport.page")]
private void HandleTeleportPagination(ConsoleSystem.Arg args)
{
    BasePlayer player = args.Connection?.player as BasePlayer;
    if (player == null || args.Args.Length < 1) return;

    if (!int.TryParse(args.Args[0], out int page)) page = 0;
    OpenMainMenu(player, "teleport", null, null, page);
}


[ConsoleCommand("eventui.tab")]
private void HandleTabCommand(ConsoleSystem.Arg args)
{
    BasePlayer player = args.Connection?.player as BasePlayer;
    if (player == null) return;

    string tab = args.Args.Length > 0 ? args.Args[0] : "teams";
    OpenMainMenu(player, tab);
}

[ConsoleCommand("eventui.close")]
private void HandleCloseCommand(ConsoleSystem.Arg args)
{
    BasePlayer player = args.Connection?.player as BasePlayer;
    if (player == null) return;

    // Existing code to close the UI
    CuiHelper.DestroyUi(player, "EventControlUI");
    uiOpenPlayers.Remove(player.userID); // Existing tracking

    // Remove the player from the Timers tracking if they have it open
    timersTabOpenPlayers.Remove(player.userID);
}






void OnPlayerRespawned(BasePlayer player)
{
    if (!worldSpawnLocation.HasValue)
        return;

    // Check for a sleeping bag or bed at the player's current position
    bool hasValidSpawn = IsSleepingBagOrBedUnderPlayer(player.transform.position);

    // If no valid spawn found, teleport to world spawn
    if (!hasValidSpawn)
    {
        Vector3 safePosition = worldSpawnLocation.Value + Vector3.up * 1f; // Slightly above ground
        player.Teleport(safePosition);

        // Prevent fall damage
        NextTick(() =>
        {
            player.metabolism.bleeding.value = 0; // Reset bleeding
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, false); // Prevent wounded state
            player.Hurt(0f); // Reset any residual damage
        });

        player.ChatMessage("You have been teleported to the server's WorldSpawn.");
    }
}


private bool IsSleepingBagOrBedUnderPlayer(Vector3 position)
{
    foreach (var entity in BaseNetworkable.serverEntities)
    {
        if (entity is SleepingBag sleepingBag)
        {
            // Check if the sleeping bag or bed is within a small radius under the player
            if (Vector3.Distance(sleepingBag.transform.position, position) < 2f) // Adjust radius as needed
            {
                return true; // Found a valid spawn
            }
        }
    }
    return false; // No valid sleeping bag or bed found
}




object CanExecuteCommand(ConsoleSystem.Arg arg)
{
    if (arg?.Connection?.player is BasePlayer player && arg.cmd.FullName.StartsWith("inventory.give"))
    {
        if (!playerPerms.ContainsKey(player.UserIDString) || !playerPerms[player.UserIDString].Contains("itemspawn"))
        {
            player.ChatMessage("You do not have permission to spawn items.");
            return false;
        }
    }
    return null;
}

object CanUseUI(BasePlayer player, string panelName)
{
    if (panelName == "DevConsole.Items" && (!playerPerms.ContainsKey(player.UserIDString) || !playerPerms[player.UserIDString].Contains("itemtab")))
    {
        player.ChatMessage("You do not have permission to use the item tab.");
        return false;
    }
    return null;
}

// Time Data below

private Dictionary<string, TimerData> timers = new Dictionary<string, TimerData>();

public class TimerData
{
    public string TeamName { get; set; } // "Global", "TeamOne", etc.
    public float RemainingTime { get; set; } // In seconds
    public Timer TimerInstance { get; set; }
    public bool IsRunning { get; set; }
    public bool BuildingOnZero { get; set; }
    public bool RaidingOnZero { get; set; }
    public bool PvPOnZero { get; set; }
    public string CustomText { get; set; }
}

// Layout variables
private float startY = 0.85f; // Starting Y position for headers
private float rowHeight = 0.06f; // Height for each row
private float columnWidthTeam = 0.1f; // Width for Team column
private float columnWidthTimer = 0.1f; // Width for Timer column
private float columnWidthButton = 0.08f; // Width for Start/Stop, Clear, Set buttons
private float columnWidthFeature = 0.08f; // Width for Building, Raiding, PvP buttons
private float spacingX = 0.02f; // Horizontal spacing between columns

// Dictionary to store label names for timers

private Dictionary<string, string> timerLabelNames = new Dictionary<string, string>();
// Tracks admins who are setting timers and their corresponding teams
private Dictionary<ulong, string> pendingTimerSets = new Dictionary<ulong, string>();


// At the top of your plugin, add two dictionaries to track panel/label names:
private Dictionary<string, string> timerPanelNames = new Dictionary<string, string>();


private void AddTimersTabContent(CuiElementContainer container, string parent)
{

     var orderedTeams = new List<string> { "Global" };
     orderedTeams.AddRange(teamNames); 
    // Header row positions
    float currentHeaderY = startY;   // e.g. 0.85f
    float rowH = rowHeight;         // e.g. 0.06f
    float xStart = 0.05f;

    // (A) CREATE HEADER LABELS
    container.Add(new CuiLabel
    {
        RectTransform = { AnchorMin = $"{xStart} {currentHeaderY}", AnchorMax = $"{xStart + columnWidthTeam} {currentHeaderY + 0.04f}" },
        Text = { Text = "Team", FontSize = 16, Align = TextAnchor.MiddleCenter }
    }, parent);

    container.Add(new CuiLabel
    {
        RectTransform = {
            AnchorMin = $"{xStart + columnWidthTeam + spacingX} {currentHeaderY}",
            AnchorMax = $"{xStart + columnWidthTeam + spacingX + columnWidthTimer} {currentHeaderY + 0.04f}"
        },
        Text = { Text = "Timer", FontSize = 16, Align = TextAnchor.MiddleCenter }
    }, parent);

    container.Add(new CuiLabel
    {
        RectTransform = {
            AnchorMin = $"{xStart + columnWidthTeam + spacingX + columnWidthTimer + spacingX} {currentHeaderY}",
            AnchorMax = $"{xStart + columnWidthTeam + spacingX + columnWidthTimer + spacingX + columnWidthButton} {currentHeaderY + 0.04f}"
        },
        Text = { Text = "Start/Stop", FontSize = 16, Align = TextAnchor.MiddleCenter }
    }, parent);

    container.Add(new CuiLabel
    {
        RectTransform = {
            AnchorMin = $"{xStart + columnWidthTeam + spacingX + columnWidthTimer + spacingX + columnWidthButton + spacingX} {currentHeaderY}",
            AnchorMax = $"{xStart + columnWidthTeam + spacingX + columnWidthTimer + spacingX + 2*columnWidthButton + spacingX} {currentHeaderY + 0.04f}"
        },
        Text = { Text = "Clear", FontSize = 16, Align = TextAnchor.MiddleCenter }
    }, parent);

    // "Input" column header
    container.Add(new CuiLabel
    {
        RectTransform = {
            AnchorMin = $"{xStart + columnWidthTeam + spacingX + columnWidthTimer + 2*spacingX + 2*columnWidthButton} {currentHeaderY}",
            AnchorMax = $"{xStart + columnWidthTeam + spacingX + columnWidthTimer + 2*spacingX + 3*columnWidthButton} {currentHeaderY + 0.04f}"
        },
        Text = { Text = "Input", FontSize = 16, Align = TextAnchor.MiddleCenter }
    }, parent);

    // "Set" column header
    container.Add(new CuiLabel
    {
        RectTransform = {
            AnchorMin = $"{xStart + columnWidthTeam + spacingX + columnWidthTimer + 3*spacingX + 3*columnWidthButton} {currentHeaderY}",
            AnchorMax = $"{xStart + columnWidthTeam + spacingX + columnWidthTimer + 3*spacingX + 4*columnWidthButton} {currentHeaderY + 0.04f}"
        },
        Text = { Text = "Set", FontSize = 16, Align = TextAnchor.MiddleCenter }
    }, parent);

    // Next columns: Building, Raiding, PvP
    container.Add(new CuiLabel
    {
        RectTransform = {
            AnchorMin = $"{xStart + columnWidthTeam + spacingX + columnWidthTimer + 4*spacingX + 4*columnWidthButton} {currentHeaderY}",
            AnchorMax = $"{xStart + columnWidthTeam + spacingX + columnWidthTimer + 4*spacingX + 4*columnWidthButton + columnWidthFeature} {currentHeaderY + 0.04f}"
        },
        Text = { Text = "Building", FontSize = 16, Align = TextAnchor.MiddleCenter }
    }, parent);

    container.Add(new CuiLabel
    {
        RectTransform = {
            AnchorMin = $"{xStart + columnWidthTeam + spacingX + columnWidthTimer + 5*spacingX + 4*columnWidthButton + columnWidthFeature} {currentHeaderY}",
            AnchorMax = $"{xStart + columnWidthTeam + spacingX + columnWidthTimer + 5*spacingX + 4*columnWidthButton + 2*columnWidthFeature} {currentHeaderY + 0.04f}"
        },
        Text = { Text = "Raiding", FontSize = 16, Align = TextAnchor.MiddleCenter }
    }, parent);

    container.Add(new CuiLabel
    {
        RectTransform = {
            AnchorMin = $"{xStart + columnWidthTeam + spacingX + columnWidthTimer + 6*spacingX + 4*columnWidthButton + 2*columnWidthFeature} {currentHeaderY}",
            AnchorMax = $"{xStart + columnWidthTeam + spacingX + columnWidthTimer + 6*spacingX + 4*columnWidthButton + 3*columnWidthFeature} {currentHeaderY + 0.04f}"
        },
        Text = { Text = "PvP", FontSize = 16, Align = TextAnchor.MiddleCenter }
    }, parent);

    // Move down for rows
    float currentY = currentHeaderY - rowH;

    // (B) CREATE A ROW PER TIMER (Global + each team in order)
    foreach (var teamName in orderedTeams)
    {
        if (!timers.ContainsKey(teamName))
            continue; // Skip if no timer data exists for the team

        var timerEntry = timers[teamName];
        // 1) TEAM LABEL
        container.Add(new CuiLabel
        {
            RectTransform = {
                AnchorMin = $"{xStart} {currentY}",
                AnchorMax = $"{xStart + columnWidthTeam} {currentY + 0.04f}"
            },
            Text = {
                Text = teamName,
                FontSize = 14,
                Align = TextAnchor.MiddleCenter
            }
        }, parent);

        // 2) Instead of placing the timer label directly, we create a small child panel
        //    so that we can easily partial-update only that label later.

        // Name for the panel (store it so we can re-use it later if needed)
        string timerPanelName = $"TimerPanel_{teamName}";
        timerPanelNames[teamName] = timerPanelName;

        // Add a panel that occupies the same area your timer label used to
        container.Add(new CuiPanel
        {
            RectTransform = {
                AnchorMin = $"{xStart + columnWidthTeam + spacingX} {currentY}",
                AnchorMax = $"{xStart + columnWidthTeam + spacingX + columnWidthTimer} {currentY + 0.04f}"
            },
            Image = { Color = "0 0 0 0" } // transparent
        }, parent, timerPanelName);

        // Now place the *label* inside that panel
        string labelName = $"TimerLabel_{teamName}";
        timerLabelNames[teamName] = labelName;

        // Format the initial time
        string timeDisplay = FormatTime(timerEntry.RemainingTime);

        container.Add(new CuiLabel
        {
            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
            Text = {
                Text = timeDisplay,
                FontSize = 14,
                Align = TextAnchor.MiddleCenter
            }
        }, timerPanelName, labelName);

        // 3) START/STOP BUTTON
        bool isRunning = timerEntry.IsRunning;
        string startStopCmd = isRunning ? $"timers.stop {teamName}" : $"timers.start {teamName}";
        string startStopText = isRunning ? "Stop" : "Start";
        string startStopColor = isRunning ? "0.8 0.2 0.2 1.0" : "0.2 0.8 0.2 1.0";
        
        container.Add(new CuiButton
        {
            RectTransform = {
                AnchorMin = $"{xStart + columnWidthTeam + spacingX + columnWidthTimer + spacingX} {currentY}",
                AnchorMax = $"{xStart + columnWidthTeam + spacingX + columnWidthTimer + spacingX + columnWidthButton} {currentY + 0.04f}"
            },
            Button = { Command = startStopCmd, Color = startStopColor },
            Text   = { Text = startStopText, FontSize = 12, Align = TextAnchor.MiddleCenter }
        }, parent);

        // 4) CLEAR BUTTON
        string clearCmd = $"timers.clear {teamName}";
        container.Add(new CuiButton
        {
            RectTransform = {
                AnchorMin = $"{xStart + columnWidthTeam + spacingX + columnWidthTimer + spacingX + columnWidthButton + spacingX} {currentY}",
                AnchorMax = $"{xStart + columnWidthTeam + spacingX + columnWidthTimer + spacingX + 2*columnWidthButton + spacingX} {currentY + 0.04f}"
            },
            Button = { Command = clearCmd, Color = "0.8 0.2 0.2 1.0" },
            Text   = { Text = "Clear", FontSize = 12, Align = TextAnchor.MiddleCenter }
        }, parent);

        // 5) INPUT FIELD
        float inputMinX = xStart + columnWidthTeam + spacingX + columnWidthTimer + 2*spacingX + 2*columnWidthButton;
        float inputMaxX = inputMinX + columnWidthButton;

        // If you want to pass typed text in quotes, do: "... \"{text}\""
        container.Add(new CuiElement
        {
            Name = $"TimerInput_{teamName}",
            Parent = parent,
            Components =
            {
                new CuiInputFieldComponent
                {
                    Command = $"timers.input.save {teamName}", 
                    FontSize = 12,
                    Align = TextAnchor.MiddleCenter,
                    Text = timerEntry.CustomText ?? "",
                    Color = "1 1 1 1"
                },
                new CuiRectTransformComponent
                {
                    AnchorMin = $"{inputMinX} {currentY}",
                    AnchorMax = $"{inputMaxX} {currentY + 0.04f}"
                }
            }
        });

        // 6) SET BUTTON
        float setMinX = inputMaxX + spacingX;
        float setMaxX = setMinX + columnWidthButton;

        container.Add(new CuiButton
        {
            RectTransform = {
                AnchorMin = $"{setMinX} {currentY}",
                AnchorMax = $"{setMaxX} {currentY + 0.04f}"
            },
            Button = { Command = $"timers.input.apply {teamName}", Color = "0.2 0.5 0.8 1.0" },
            Text   = { Text = "Set", FontSize = 12, Align = TextAnchor.MiddleCenter }
        }, parent);

        // 7) BUILDING Toggle
        float buildMinX = setMaxX + spacingX;
        float buildMaxX = buildMinX + columnWidthFeature;
        string buildingCmd = $"timers.togglefeature {teamName} Building";
        string buildingColor = timerEntry.BuildingOnZero ? "0.2 0.8 0.2 1.0" : "0.8 0.2 0.2 1.0";

        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = $"{buildMinX} {currentY}", AnchorMax = $"{buildMaxX} {currentY + 0.04f}" },
            Button = { Command = buildingCmd, Color = buildingColor },
            Text   = { Text = "Building", FontSize = 12, Align = TextAnchor.MiddleCenter }
        }, parent);

        // 8) RAIDING Toggle
        float raidMinX = buildMaxX + spacingX;
        float raidMaxX = raidMinX + columnWidthFeature;
        string raidingCmd = $"timers.togglefeature {teamName} Raiding";
        string raidingColor = timerEntry.RaidingOnZero ? "0.2 0.8 0.2 1.0" : "0.8 0.2 0.2 1.0";

        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = $"{raidMinX} {currentY}", AnchorMax = $"{raidMaxX} {currentY + 0.04f}" },
            Button = { Command = raidingCmd, Color = raidingColor },
            Text   = { Text = "Raiding", FontSize = 12, Align = TextAnchor.MiddleCenter }
        }, parent);

        // 9) PVP Toggle
        float pvpMinX = raidMaxX + spacingX;
        float pvpMaxX = pvpMinX + columnWidthFeature;
        string pvpCmd = $"timers.togglefeature {teamName} PvP";
        string pvpColor = timerEntry.PvPOnZero ? "0.2 0.8 0.2 1.0" : "0.8 0.2 0.2 1.0";

        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = $"{pvpMinX} {currentY}", AnchorMax = $"{pvpMaxX} {currentY + 0.04f}" },
            Button = { Command = pvpCmd, Color = pvpColor },
            Text   = { Text = "PvP", FontSize = 12, Align = TextAnchor.MiddleCenter }
        }, parent);

        // Move Y down to next row
        currentY -= rowH;
    }
}


// Holds edits before they're actually saved/applied.
private Dictionary<string, TimerEdits> pendingTimerEdits = new Dictionary<string, TimerEdits>();

// Simple class to store the user's typed timer value (or any other fields).
private class TimerEdits
{
    public string TempTimeString = "";
}


[ConsoleCommand("timers.input.save")]
private void SaveTimerInput(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    if (player == null) return;

    // We expect Arg[0] to be the teamName
    // Arg[1..end] = typed text
    if (arg.Args == null || arg.Args.Length < 2)
    {
        // Means user typed nothing
        Puts("No typed text provided.");
        return;
    }

    // e.g. "Global"
    string teamName = arg.Args[0];

    // Re-join all leftover pieces as one string
    // If user typed "10:00 test", Arg[1] = "10:00", Arg[2] = "test". We join => "10:00 test"
    string typedText = string.Join(" ", arg.Args.Skip(1));

    if (!timers.ContainsKey(teamName))
    {
        Puts($"No timer found for team '{teamName}'.");
        return;
    }

    // Store the typed text in .CustomText, just like your other plugin does with TempEdits
    timers[teamName].CustomText = typedText;

    Puts($"[EventControlUI] Saved input for '{teamName}': {typedText}");
}




[ConsoleCommand("timers.input.apply")]
private void ApplyTimerInput(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    if (player == null) return;

    if (arg.Args == null || arg.Args.Length < 1)
    {
        Puts("Usage: timers.input.apply <teamName>");
        return;
    }

    string teamName = arg.Args[0];
    if (!timers.ContainsKey(teamName))
    {
        Puts($"No timer found for team '{teamName}'.");
        return;
    }

    string savedText = timers[teamName].CustomText;
    if (string.IsNullOrEmpty(savedText))
    {
        Puts($"No input text was saved for '{teamName}'.");
        return;
    }

    // Now parse
    if (!TryParseTime(savedText, out float seconds))
    {
        Puts($"Invalid time '{savedText}' for team '{teamName}'.");
        return;
    }

    timers[teamName].RemainingTime = seconds;
    timers[teamName].CustomText = string.Empty;
    UpdateTimersUI(); // or rebuild UI

    Puts($"Timer for '{teamName}' set to {FormatTime(seconds)}.");
}




[ConsoleCommand("timers.input")]
private void HandleTimerInput(ConsoleSystem.Arg args)
{
    var player = args.Player();
    if (player == null || args.Args == null || args.Args.Length < 2)
    {
        Puts("Invalid arguments. Usage: timers.input <teamName> <time>");
        return;
    }

    string teamName = args.Args[0];
    string timeInput = args.Args[1]; // Captures user-entered text

    if (!timers.ContainsKey(teamName))
    {
        Puts($"Team '{teamName}' not found.");
        return;
    }

    if (!TryParseTime(timeInput, out float seconds))
    {
        Puts($"Invalid time format: {timeInput}. Use MM:SS.");
        return;
    }

    timers[teamName].RemainingTime = seconds;
    UpdateTimersUI();
    Puts($"Timer for '{teamName}' set to {FormatTime(seconds)}.");
}


private void UpdateTimersUI()
{
    foreach (var timerEntry in timers.Values)
    {
        if (!timerLabelNames.ContainsKey(timerEntry.TeamName))
            continue; // Skip if label name not stored

        string labelName = timerLabelNames[timerEntry.TeamName];
        string newText = $"{FormatTime(timerEntry.RemainingTime)}";

        // Create a container to update the specific label
        var container = new CuiElementContainer();

        container.Add(new CuiLabel
        {
            Text = {
                Text = newText,
                FontSize = 14,
                Align = TextAnchor.MiddleCenter,
                Color = "1 1 1 1"
            },
            RectTransform = {
                AnchorMin = $"{0.1f + columnWidthTeam + spacingX + columnWidthTimer + 0.05f} {startY - rowHeight * (teamNames.IndexOf(timerEntry.TeamName) + 1) + 0.02f}",
                AnchorMax = $"{0.1f + columnWidthTeam + spacingX + columnWidthTimer + 0.15f} {startY - rowHeight * (teamNames.IndexOf(timerEntry.TeamName) + 1) + 0.07f}"
            }
        }, "EventControlUI", labelName); // Use the panel name

        // Update the label for all players
        foreach (var kvp in playerTeams)
        {
            if (kvp.Value == timerEntry.TeamName)
            {
                var pl = BasePlayer.FindByID(ulong.Parse(kvp.Key));
                if (pl != null)
                {
                    CuiHelper.DestroyUi(pl, labelName);
                    CuiHelper.AddUi(pl, container);
                }
            }
        }
    }
}


// Helper method to format time in MM:SS
private string FormatTime(float totalSeconds)
{
    if (totalSeconds <= 0)
        return "00:00";
    int minutes = Mathf.FloorToInt(totalSeconds / 60F);
    int seconds = Mathf.FloorToInt(totalSeconds - minutes * 60);
    return string.Format("{0:00}:{1:00}", minutes, seconds);
}

private void UpdateTeamHudTimer(string teamName)
{
    // Define the panel name for the team's HUD
    string panelName = $"TeamTimerHUD_{teamName}";

    TimerData tData = timers[teamName];
    
    // If the timer is not running or has no remaining time, remove the HUD
    if (!tData.IsRunning || tData.RemainingTime <= 0f)
    {
        foreach (var kvp in playerTeams)
        {
            if (kvp.Value == teamName)
            {
                var pl = BasePlayer.FindByID(ulong.Parse(kvp.Key));
                if (pl != null)
                    CuiHelper.DestroyUi(pl, panelName);
            }
        }
        return;
    }

    // Define HUD position (top-left corner)
    string anchorMin = "0.01 0.85"; // 1% from left, 85% from bottom
    string anchorMax = "0.15 0.90"; // 15% width, 5% height

    // Update the HUD for all players in the team
    foreach (var kvp in playerTeams)
    {
        if (kvp.Value == teamName)
        {
            var pl = BasePlayer.FindByID(ulong.Parse(kvp.Key));
            if (pl == null) continue;

            // Destroy old HUD panel if it exists
            CuiHelper.DestroyUi(pl, panelName);

            // Create a new HUD panel
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax },
                Image = { Color = "0 0 0 0.7" } // Semi-transparent black
            }, "Overlay", panelName);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = {
                    Text = $"<color=#FFFF00>{teamName}</color>\n{FormatTime(tData.RemainingTime)}",
                    FontSize = 14,
                    Align = TextAnchor.MiddleCenter
                }
            }, panelName);

            CuiHelper.AddUi(pl, container);
        }
    }
}


// Add these console commands within your EventControlUI class

[ConsoleCommand("timers.start")]
private void ToggleTimer(ConsoleSystem.Arg args)
{
    // Usage: timers.start <teamName>
    if (args.Args.Length < 1) return;

    string teamName = args.Args[0];
    if (!timers.ContainsKey(teamName)) return;

    TimerData timerData = timers[teamName];

    // Toggling logic
    if (timerData.IsRunning)
    {
        // It's currently running => pause it
        timerData.IsRunning = false;
        // No need to set RemainingTime = 0, we keep leftover time
        // If you previously used TimerData.TimerInstance, destroy it:
        timerData.TimerInstance?.Destroy();
        timerData.TimerInstance = null;

        SendReply(args, $"Timer for {teamName} has been **paused** at {FormatTime(timerData.RemainingTime)}.");
    }
    else
    {
        // It's currently paused => resume it
        if (timerData.RemainingTime <= 0)
        {
            SendReply(args, $"Timer for {teamName} is at 0. Set a time before starting.");
            return;
        }

        timerData.IsRunning = true;
        // Don’t schedule timer.Once() now because we have the
        // 1-second repeating loop in OnServerInitialized.

        SendReply(args, $"Timer for {teamName} **started** with {FormatTime(timerData.RemainingTime)} remaining.");
    }

    // Save, update UI, etc.
    Config["Timers"] = timers;
    SaveConfig();
    UpdateTimersUI(); // refresh label display

    // Update the appropriate upper right HUD
    if (teamName != "Global")
    {
        UpdateGlobalUpperRightHudTimer();
    }

        BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin != null)
    {
        OpenMainMenu(admin, "timers");
    }
}




[ConsoleCommand("timers.stop")]
private void StopTimer(ConsoleSystem.Arg args)
{
    if (args.Args.Length < 1) return;

    string teamName = args.Args[0];
    if (!timers.ContainsKey(teamName)) return;

    TimerData timerData = timers[teamName];

    if (!timerData.IsRunning)
    {
        SendReply(args, $"Timer for {teamName} is not running.");
        return;
    }

    timerData.IsRunning = false;
    timerData.TimerInstance?.Destroy();
    timerData.TimerInstance = null;
    Config["Timers"] = timers;
    SaveConfig();

    SendReply(args, $"Timer for {teamName} has been stopped.");

    // Remove the upper right HUD
    if (teamName != "Global")
    {
        string upperRightPanelName = $"UpperRightTeamTimerHUD_{teamName}";
        foreach (var kvp in playerTeams)
        {
            if (kvp.Value == teamName)
            {
                var pl = BasePlayer.FindByID(ulong.Parse(kvp.Key));
                if (pl != null)
                    CuiHelper.DestroyUi(pl, upperRightPanelName);
            }
        }
    }
    else
    {
        // Remove the Global upper right HUD
        foreach (var player in BasePlayer.activePlayerList)
        {
            CuiHelper.DestroyUi(player, GlobalUpperRightPanelName);
        }
    }

    // Refresh UI
    RefreshAllTimersUI();

        BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin != null)
    {
        OpenMainMenu(admin, "timers");
    }
}



[ConsoleCommand("timers.clear")]
private void ClearTimer(ConsoleSystem.Arg args)
{
    if (args.Args.Length < 1) return;
    string teamName = args.Args[0];
    if (!timers.ContainsKey(teamName)) return;

    TimerData timerData = timers[teamName];

    // Stop any running coroutine or timer
    if (timerData.IsRunning)
    {
        timerData.TimerInstance?.Destroy();
    }

    timerData.IsRunning = false;
    timerData.RemainingTime = 0f;
    timerData.CustomText = "";
    Config["Timers"] = timers;
    SaveConfig();

    SendReply(args, $"Timer for {teamName} has been cleared.");

    // **Remove** the top-left HUD for everyone in that team
    string panelName = $"TeamTimerHUD_{teamName}";
    foreach (var kvp in playerTeams)
    {
        if (kvp.Value == teamName)
        {
            var pl = BasePlayer.FindByID(ulong.Parse(kvp.Key));
            if (pl != null)
                CuiHelper.DestroyUi(pl, panelName);
        }
    }

    RefreshAllTimersUI();

        BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin != null)
    {
        OpenMainMenu(admin, "timers");
    }
}


[ConsoleCommand("timers.set")]
private void SetTimer(ConsoleSystem.Arg args)
{
    // Log the command trigger for debugging
    Puts($"Command triggered with args: {args.FullString ?? "No arguments provided"}");

    // Validate arguments
    if (args.Args == null || args.Args.Length < 2)
    {
        Puts("Invalid arguments. Usage: timers.set <teamName> <time>");
        return;
    }

    // Extract arguments
    string teamName = args.Args[0];
    string timeInput = args.Args[1]; // Expecting input in MM:SS format

    // Check if the team exists
    if (!timers.ContainsKey(teamName))
    {
        Puts($"Team '{teamName}' not found.");
        return;
    }

    // Parse the time input
    if (!TryParseTime(timeInput, out float seconds))
    {
        Puts($"Invalid time format: {timeInput}. Use MM:SS.");
        return;
    }

    // Update the timer
    timers[teamName].RemainingTime = seconds;

    // Refresh the UI to reflect the changes
    UpdateTimersUI();

    // Update the appropriate upper right HUD
    if (teamName != "Global")
    {
        UpdateGlobalUpperRightHudTimer();
    }

    // Log success
    Puts($"Timer for '{teamName}' set to {FormatTime(seconds)}.");
}




private bool TryParseTime(string input, out float seconds)
{
    seconds = 0f;
    if (string.IsNullOrWhiteSpace(input)) return false;

    // If input has a colon, parse "MM:SS"
    if (input.Contains(":"))
    {
        string[] parts = input.Split(':');
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], out int mins) || !int.TryParse(parts[1], out int secs)) return false;
        if (mins < 0 || secs < 0 || secs >= 60) return false;

        seconds = mins * 60 + secs;
        return true;
    }
    else
    {
        // Try pure integer seconds
        if (!int.TryParse(input, out int totalSecs)) return false;
        if (totalSecs < 0) return false;

        seconds = totalSecs;
        return true;
    }
}







[ConsoleCommand("timers.enter")]
private void EnterTimer(ConsoleSystem.Arg args)
{
    if (args.Args == null || args.Args.Length < 2)
    {
        Puts("Invalid arguments. Usage: timers.enter <teamName> <time>");
        return;
    }

    string teamName = args.Args[0];
    string timeInput = args.Args[1]; // User's entered text is passed as the second argument

    if (!timers.ContainsKey(teamName))
    {
        Puts($"Team '{teamName}' not found.");
        return;
    }

    if (!TryParseTime(timeInput, out float seconds))
    {
        Puts($"Invalid time format: {timeInput}. Use MM:SS.");
        return;
    }

    // Update the timer
    timers[teamName].RemainingTime = seconds;
    UpdateTimersUI();
    Puts($"Timer for '{teamName}' set to {FormatTime(seconds)}.");
}



[ConsoleCommand("timers.togglefeature")]
private void ToggleTimerFeature(ConsoleSystem.Arg args)
{
    if (args.Args.Length < 2) return;

    string teamName = args.Args[0];
    string feature = args.Args[1].ToLower();

    if (!timers.ContainsKey(teamName)) return;

    TimerData timerData = timers[teamName];
    switch (feature)
    {
        case "building":
            timerData.BuildingOnZero = !timerData.BuildingOnZero;
            SendReply(args, $"Building feature on zero for {teamName} is now {(timerData.BuildingOnZero ? "Enabled" : "Disabled")}.");
            break;
        case "raiding":
            timerData.RaidingOnZero = !timerData.RaidingOnZero;
            SendReply(args, $"Raiding feature on zero for {teamName} is now {(timerData.RaidingOnZero ? "Enabled" : "Disabled")}.");
            break;
        case "pvp":
            timerData.PvPOnZero = !timerData.PvPOnZero;
            SendReply(args, $"PvP feature on zero for {teamName} is now {(timerData.PvPOnZero ? "Enabled" : "Disabled")}.");
            break;
        default:
            SendReply(args, "Unknown feature.");
            return;
    }

    Config["Timers"] = timers;
    SaveConfig();
    RefreshAllTimersUI();

        BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin != null)
    {
        OpenMainMenu(admin, "timers");
    }
}

private void OnServerInitialized()
{
    timer.Every(1f, () =>
    {
        foreach (var kvp in timers)
        {
            var tData = kvp.Value;
            if (tData.IsRunning && tData.RemainingTime > 0f)
            {
                tData.RemainingTime--;
                if (tData.RemainingTime <= 0f)
                {
                    OnTimerEnd(kvp.Key);
                }
            }
        }

        // Update normal UI labels (if open)
        UpdateAllTimerLabels();

        // Update the small top-left HUD for each team
        foreach (var kvp in timers)
        {
            if (kvp.Key != "Global")
            {
                UpdateTeamHudTimer(kvp.Key);
            }
        }

        // Update the Global upper right HUD separately
        UpdateGlobalUpperRightHudTimer();
    });
}


private void OnTimerTick(string teamName)
{
    if (!timers.ContainsKey(teamName)) return;

    TimerData tData = timers[teamName];
    if (tData.IsRunning && tData.RemainingTime > 0f)
    {
        tData.RemainingTime--;
        if (tData.RemainingTime <= 0f)
        {
            OnTimerEnd(teamName);
        }

        // Update both UI labels
        UpdateTimersUI();
    }
}

private void UpdateAllTimerLabels()
{
    // Update labels for every active player
    foreach (var player in BasePlayer.activePlayerList)
    {
        // Define the ordered list including "Global"
        var orderedTeams = new List<string> { "Global" };
        orderedTeams.AddRange(teamNames);

        foreach (var teamName in orderedTeams)
        {
            if (!timers.ContainsKey(teamName))
                continue;

            if (!timerLabelNames.ContainsKey(teamName))
                continue;

            string labelName = timerLabelNames[teamName];
            string panelName = timerPanelNames[teamName];
            string newText = FormatTime(timers[teamName].RemainingTime);



            // Create a container for the updated label
            var container = new CuiElementContainer();

            container.Add(new CuiLabel
            {
                Text = {
                    Text = newText,
                    FontSize = 14,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, panelName, labelName);

            // Update the label for the player
            CuiHelper.DestroyUi(player, labelName);
            CuiHelper.AddUi(player, container);
        }
    }
}



// Now for each player, refresh each team's label
private void UpdateTimerLabelsForPlayer(BasePlayer player)
{
    
    // Ensure the player and UI are valid
    if (player == null) return;

    
    foreach (var kvp in timers)
    {
        string teamName = kvp.Key;

        if (!timerLabelNames.ContainsKey(teamName))
            continue; // No label was created for this team

        // Retrieve the label name
        string labelName = timerLabelNames[teamName];
        float remaining = kvp.Value.RemainingTime;

        // Create a container for the updated label
        var container = new CuiElementContainer();

        container.Add(new CuiLabel
        {
            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }, // Anchored to fill the parent panel
            Text = {
                Text = FormatTime(remaining),
                FontSize = 14,
                Align = TextAnchor.MiddleCenter
            }
        }, timerPanelNames[teamName], labelName); // Parent is the TimerPanel

        // Destroy the old label
        CuiHelper.DestroyUi(player, labelName);

        // Add the new updated label
        CuiHelper.AddUi(player, container);
    }
}

private float xStart = 0.1f;

// Dictionary to store upper right HUD panel names for timers
private Dictionary<string, string> upperRightTimerPanelNames = new Dictionary<string, string>();
private const string GlobalUpperRightPanelName = "UpperRightGlobalTimerHUD";

private void UpdateGlobalUpperRightHudTimer()
{
    // Define the panel name for the Global upper right HUD
    string panelName = GlobalUpperRightPanelName;
    
    TimerData tData = timers["Global"];
    
    // If the Global timer is not running or has no remaining time, remove the HUD
    if (!tData.IsRunning || tData.RemainingTime <= 0f)
    {
        foreach (var player in BasePlayer.activePlayerList)
        {
            CuiHelper.DestroyUi(player, panelName);
        }
        return;
    }
    
    // Define HUD position (upper right corner)
    string anchorMin = "0.85 0.85"; // 85% from left, 85% from bottom
    string anchorMax = "0.99 0.90"; // 14% width, 5% height
    
    // Update the HUD for all players who do NOT have the Hide HUD permission
    foreach (var player in BasePlayer.activePlayerList)
    {
        if (player == null) continue;

        // **Check if the player has the Hide HUD permission**
        if (permission.UserHasPermission(player.UserIDString, PermissionHideHud))
            continue; // Skip adding HUD for this player

        // Destroy old HUD panel if it exists
        CuiHelper.DestroyUi(player, panelName);
        
        // Create a new HUD panel
        var container = new CuiElementContainer();
        
        container.Add(new CuiPanel
        {
            RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax },
            Image = { Color = "0 0 0 0.7" } // Semi-transparent black
        }, "Overlay", panelName);
        
        container.Add(new CuiLabel
        {
            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
            Text = {
                Text = $"<color=#FFFF00>Global</color>\n{FormatTime(tData.RemainingTime)}",
                FontSize = 14,
                Align = TextAnchor.MiddleCenter
            }
        }, panelName);
        
        CuiHelper.AddUi(player, container);
    }
}




private void UpdateTimerLabels(BasePlayer player)
{
    foreach (var kvp in timers)
    {
        string teamName = kvp.Key;
        TimerData tData = kvp.Value;

        if (!timerLabelNames.ContainsKey(teamName)) 
            continue;

        string labelName = timerLabelNames[teamName];

        // Destroy the old label
        CuiHelper.DestroyUi(player, labelName);

        // Build a new label with the same parent as before, e.g. "EventControlUI"
        var container = new CuiElementContainer();

        string timeText = FormatTime(tData.RemainingTime);
    
        container.Add(new CuiLabel
        {
            Text = { Text = timeText, FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
            RectTransform = {
                // same position as in AddTimersTabContent
                AnchorMin = $"{xStart + columnWidthTeam + spacingX} 0.1f ",
                AnchorMax = $"{xStart + columnWidthTeam + spacingX + columnWidthTimer} {0.1f + 0.04f}"
            }
        }, "EventControlUI", labelName);

        CuiHelper.AddUi(player, container);
    }
}


private void RefreshAllTimersUI()
{
    foreach (var player in BasePlayer.activePlayerList)
    {
        if (timersTabOpenPlayers.Contains(player.userID))
        {
            OpenMainMenu(player, "timers");
        }
    }
}

private void Unload()
{
    foreach (var player in BasePlayer.activePlayerList)
    {
        // Destroy the main UI
        CuiHelper.DestroyUi(player, "EventControlUI");
        CuiHelper.DestroyUi(player, "TeamSelectionUI");
        // Destroy team-specific upper right HUDs
        foreach (var teamName in teamNames)
        {
            string panelName = $"UpperRightTeamTimerHUD_{teamName}";
            CuiHelper.DestroyUi(player, panelName);
        }

        // Destroy the Global upper right HUD
        CuiHelper.DestroyUi(player, GlobalUpperRightPanelName);
    }

    // Clear the HashSets
    uiOpenPlayers.Clear();
    timersTabOpenPlayers.Clear();
}

private void OnTimerEnd(string teamName)
{
    if (!timers.ContainsKey(teamName)) return;

    TimerData timerData = timers[teamName];
    timerData.IsRunning = false;
    timerData.TimerInstance = null;

    if (teamName == "Global")
    {
        // Remove the Global upper right HUD for all players
        foreach (var player in BasePlayer.activePlayerList)
        {
            CuiHelper.DestroyUi(player, GlobalUpperRightPanelName);
        }
    }
    else
    {
        // Remove the team-specific upper right HUD
        string upperRightPanelName = $"UpperRightTeamTimerHUD_{teamName}";
        foreach (var kvp in playerTeams)
        {
            if (kvp.Value == teamName)
            {
                var pl = BasePlayer.FindByID(ulong.Parse(kvp.Key));
                if (pl != null)
                    CuiHelper.DestroyUi(pl, upperRightPanelName);
            }
        }

        // Remove the top-left HUD
        string panelName = $"TeamTimerHUD_{teamName}";
        foreach (var kvp in playerTeams)
        {
            if (kvp.Value == teamName)
            {
                var pl = BasePlayer.FindByID(ulong.Parse(kvp.Key));
                if (pl != null)
                    CuiHelper.DestroyUi(pl, panelName);
            }
        }
    }
    // 1) If buildingOnZero is true => toggle building for this team
    if (timerData.BuildingOnZero)
    {
        if (teamName.Equals("Global", StringComparison.OrdinalIgnoreCase))
        {
            // Flip the global buildingEnabled
            buildingEnabled = !buildingEnabled;
            SendGlobalMessage($"Building has been {(buildingEnabled ? "enabled" : "disabled")} globally by timer end!");
        }
        else
        {
            // Flip the team-based building status
            teamBuildingStatus[teamName] = !teamBuildingStatus[teamName];
            NotifyTeam(teamName, $"Building has been {(teamBuildingStatus[teamName] ? "enabled" : "disabled")} for {teamName} by timer end!");
        }
    }

    // 2) If raidingOnZero => toggle raiding for this team
    if (timerData.RaidingOnZero)
    {
        if (teamName.Equals("Global", StringComparison.OrdinalIgnoreCase))
        {
            raidingEnabled = !raidingEnabled;
            SendGlobalMessage($"Raiding has been {(raidingEnabled ? "enabled" : "disabled")} globally by timer end!");
        }
        else
        {
            teamRaidingStatus[teamName] = !teamRaidingStatus[teamName];
            NotifyTeam(teamName, $"Raiding has been {(teamRaidingStatus[teamName] ? "enabled" : "disabled")} for {teamName} by timer end!");
        }
    }

    // 3) If PvPOnZero => toggle pvp for this team
    if (timerData.PvPOnZero)
    {
        if (teamName.Equals("Global", StringComparison.OrdinalIgnoreCase))
        {
            pvpEnabled = !pvpEnabled;
            SendGlobalMessage($"PvP has been {(pvpEnabled ? "enabled" : "disabled")} globally by timer end!");
        }
        else
        {
            teamPvPStatus[teamName] = !teamPvPStatus[teamName];
            NotifyTeam(teamName, $"PvP has been {(teamPvPStatus[teamName] ? "enabled" : "disabled")} for {teamName} by timer end!");
        }
    }

    // e.g. you can do GodMode toggles similarly if you want
    // ...

    // Refresh UI
    RefreshAllTimersUI();
}

// Internal methods to toggle features without UI feedback
private void ToggleRaidingTeamInternal(string teamName)
{
    if (!teamRaidingStatus.ContainsKey(teamName)) return;

    teamRaidingStatus[teamName] = !teamRaidingStatus[teamName];
    Config["TeamRaidingStatus"] = teamRaidingStatus;
    SaveConfig();

    foreach (var kvp in playerTeams.Where(kvp => kvp.Value == teamName))
    {
        var player = BasePlayer.FindByID(ulong.Parse(kvp.Key));
        if (player != null)
        {
            player.ChatMessage($"Raiding has been {(teamRaidingStatus[teamName] ? "enabled" : "disabled")} for your team by a timer.");
        }
    }
}

private void TogglePvPTeamInternal(string teamName)
{
    if (!teamPvPStatus.ContainsKey(teamName)) return;

    teamPvPStatus[teamName] = !teamPvPStatus[teamName];
    Config["TeamPvPStatus"] = teamPvPStatus;
    SaveConfig();

    foreach (var kvp in playerTeams.Where(kvp => kvp.Value == teamName))
    {
        var player = BasePlayer.FindByID(ulong.Parse(kvp.Key));
        if (player != null)
        {
            player.ChatMessage($"PvP has been {(teamPvPStatus[teamName] ? "enabled" : "disabled")} for your team by a timer.");
        }
    }
}

private void SendGlobalMessage(string message)
{
    foreach (var player in BasePlayer.activePlayerList)
    {
        player.ChatMessage(message);
    }
}

private void NotifyTeam(string teamName, string message)
{
    foreach (var kvp in playerTeams.Where(kvp => kvp.Value == teamName))
    {
        var player = BasePlayer.FindByID(ulong.Parse(kvp.Key));
        if (player != null)
        {
            player.ChatMessage(message);
        }
    }
}

private Plugin Kits;

// Method to retrieve kit names from the Kits plugin
private void GetKitNames(List<string> list)
{
    if (Kits == null)
    {
        PrintWarning("Kits plugin not found. Cannot retrieve kit names.");
        return;
    }

    // Call the GetKitNames method from the Kits plugin
    Kits.Call("GetKitNames", list);
}

// Method to give a kit to a player via the Kits plugin
private object GiveKit(BasePlayer player, string kitName)
{
    if (Kits == null)
    {
        PrintWarning("Kits plugin not found. Cannot give kits.");
        return "Kits plugin not found.";
    }

    // Call the GiveKit method from the Kits plugin
    // Expected to return true on success or a string error message on failure
    return Kits.Call("GiveKit", player, kitName);
}


private void AddKitsTabContent(CuiElementContainer container, string parent, int page = 0)
{
    // Retrieve all kit names from the Kits plugin
    var allKits = new List<string>();
    GetKitNames(allKits); // Populates allKits with kit names

    // Define Kits Per Page
    const int KitsPerPage = 4;
    int totalPages = (int)Math.Ceiling((double)allKits.Count / KitsPerPage);
    page = Mathf.Clamp(page, 0, Math.Max(totalPages - 1, 0)); // Ensure page is within valid range

    // Header Label
    container.Add(new CuiLabel
    {
        RectTransform = { AnchorMin = "0.1 0.95", AnchorMax = "0.3 0.98" },
        Text = { Text = "<color=#FFD479><b>Kits Control</b></color>", FontSize = 24, Align = TextAnchor.MiddleLeft }
    }, parent);

    /*
       Layout:
       - Global Row
       - Team Rows (one per team)
       - Pagination Controls
    */

    // Starting positions
    float startY = 0.80f;
    float rowHeight = 0.05f;
    float xStart = 0.1f;
    float xSpacing = 0.02f;
    float buttonWidth = 0.10f;
    float currentY = startY;

    // --- Global Row ---
    string globalTeamName = "Global"; // Define a consistent name for the global row

    // Team Label (Global)
    container.Add(new CuiLabel
    {
        RectTransform = {
            AnchorMin = $"{xStart} {currentY}",
            AnchorMax = $"{xStart + 0.15f} {currentY + rowHeight}"
        },
        Text = { Text = globalTeamName, FontSize = 16, Align = TextAnchor.MiddleLeft }
    }, parent);

    // WipeInv Button (Global)
    container.Add(new CuiButton
    {
        RectTransform = {
            AnchorMin = $"{xStart + 0.17f} {currentY}",
            AnchorMax = $"{xStart + 0.17f + buttonWidth} {currentY + rowHeight}"
        },
        Button = {
            Command = $"kits.wipeinvteam \"{globalTeamName}\"",
            Color = "0.8 0.2 0.2 1.0"
        },
        Text = {
            Text = "WipeInv",
            FontSize = 14,
            Align = TextAnchor.MiddleCenter
        }
    }, parent);

    // **CopyInv Button (Global)**
    container.Add(new CuiButton
    {
        RectTransform = {
            AnchorMin = $"{xStart + 0.17f + buttonWidth + xSpacing} {currentY}",
            AnchorMax = $"{xStart + 0.17f + 2 * buttonWidth + xSpacing} {currentY + rowHeight}"
        },
        Button = {
            Command = $"kits.copyinvteam \"{globalTeamName}\"",
            Color = "0.8 0.2 0.2 1.0" // A distinct color for differentiation
        },
        Text = {
            Text = "CopyInv",
            FontSize = 14,
            Align = TextAnchor.MiddleCenter
        }
    }, parent);

    // Kit Buttons (Global) with Pagination
    int globalStartKitIndex = page * KitsPerPage;
    var globalKitsToDisplay = allKits.Skip(globalStartKitIndex).Take(KitsPerPage).ToList();

    float globalKitX = xStart + 0.17f + 2 * buttonWidth + 2 * xSpacing; // Starting X for the first kit button

    foreach (var kitName in globalKitsToDisplay)
    {
        // Handle kit names with spaces by enclosing them in quotes
        string escapedKitName = kitName.Replace("\"", "\\\""); // Escape any existing quotes

        container.Add(new CuiButton
        {
            RectTransform = {
                AnchorMin = $"{globalKitX} {currentY}",
                AnchorMax = $"{globalKitX + buttonWidth} {currentY + rowHeight}"
            },
            Button = {
                Command = $"kits.giveteam \"{globalTeamName}\" \"{escapedKitName}\"",
                Color = "0.2 0.5 0.8 1.0"
            },
            Text = {
                Text = kitName,
                FontSize = 14,
                Align = TextAnchor.MiddleCenter
            }
        }, parent);

        globalKitX += (buttonWidth + xSpacing);
    }

    // Move down to the next row after Global
    currentY -= (rowHeight + 0.05f);

    // --- Team Rows ---
    foreach (var teamName in teamNames) // Ensure 'teamNames' is defined and contains all team names
    {
        // Team Label
        container.Add(new CuiLabel
        {
            RectTransform = {
                AnchorMin = $"{xStart} {currentY}",
                AnchorMax = $"{xStart + 0.15f} {currentY + rowHeight}"
            },
            Text = { Text = teamName, FontSize = 16, Align = TextAnchor.MiddleLeft }
        }, parent);

        // WipeInv Button (Team)
        container.Add(new CuiButton
        {
            RectTransform = {
                AnchorMin = $"{xStart + 0.17f} {currentY}",
                AnchorMax = $"{xStart + 0.17f + buttonWidth} {currentY + rowHeight}"
            },
            Button = {
                Command = $"kits.wipeinvteam \"{teamName}\"",
                Color = "0.8 0.2 0.2 1.0"
            },
            Text = {
                Text = "WipeInv",
                FontSize = 14,
                Align = TextAnchor.MiddleCenter
            }
        }, parent);

        // **CopyInv Button (Team)**
        container.Add(new CuiButton
        {
            RectTransform = {
                AnchorMin = $"{xStart + 0.17f + buttonWidth + xSpacing} {currentY}",
                AnchorMax = $"{xStart + 0.17f + 2 * buttonWidth + xSpacing} {currentY + rowHeight}"
            },
            Button = {
                Command = $"kits.copyinvteam \"{teamName}\"",
                Color = "0.8 0.2 0.2 1.0" // A distinct color for differentiation
            },
            Text = {
                Text = "CopyInv",
                FontSize = 14,
                Align = TextAnchor.MiddleCenter
            }
        }, parent);

        // Kit Buttons with Pagination
        int startKitIndex = page * KitsPerPage;
        var kitsToDisplay = allKits.Skip(startKitIndex).Take(KitsPerPage).ToList();

        float kitX = xStart + 0.17f + 2 * buttonWidth + 2 * xSpacing; // Starting X for the first kit button

        foreach (var kitName in kitsToDisplay)
        {
            // Handle kit names with spaces by enclosing them in quotes
            string escapedKitName = kitName.Replace("\"", "\\\""); // Escape any existing quotes

            container.Add(new CuiButton
            {
                RectTransform = {
                    AnchorMin = $"{kitX} {currentY}",
                    AnchorMax = $"{kitX + buttonWidth} {currentY + rowHeight}"
                },
                Button = {
                    Command = $"kits.giveteam \"{teamName}\" \"{escapedKitName}\"",
                    Color = "0.2 0.5 0.8 1.0"
                },
                Text = {
                    Text = kitName,
                    FontSize = 14,
                    Align = TextAnchor.MiddleCenter
                }
            }, parent);

            kitX += (buttonWidth + xSpacing);
        }

        // Move down to the next row
        currentY -= (rowHeight + 0.02f);

        // If currentY is too low, stop adding more teams
        if (currentY < 0.1f)
            break; // Adjust as needed for UI space
    }

    // --- Pagination Controls ---
    // Positioning at the bottom center
    float paginationY = 0.10f; // Increased Y position for better visibility
    float paginationXStart = 0.45f;
    float paginationButtonWidth = 0.05f;
    float paginationSpacing = 0.02f;

    // Previous Button
    if (page > 0)
    {
        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = $"{paginationXStart - (paginationButtonWidth + paginationSpacing)} {paginationY}", AnchorMax = $"{paginationXStart} {paginationY + 0.05f}" },
            Button = { Command = $"kits.kitspage {page - 1}", Color = "0.2 0.5 0.8 1.0" },
            Text = { Text = "<<", FontSize = 18, Align = TextAnchor.MiddleCenter }
        }, parent);
    }

    // Next Button
    if (page < totalPages - 1)
    {
        container.Add(new CuiButton
        {
            RectTransform = { AnchorMin = $"{paginationXStart + 0.05f} {paginationY}", AnchorMax = $"{paginationXStart + 0.05f + paginationButtonWidth} {paginationY + 0.05f}" },
            Button = { Command = $"kits.kitspage {page + 1}", Color = "0.2 0.5 0.8 1.0" },
            Text = { Text = ">>", FontSize = 18, Align = TextAnchor.MiddleCenter }
        }, parent);
    }
}


[ConsoleCommand("kits.copyinvteam")]
private void CmdCopyInvTeam(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin == null)
    {
        PrintWarning("CmdCopyInvTeam called by non-player.");
        return;
    }

    // Permission check
    if (!permission.UserHasPermission(admin.UserIDString, PermissionManagePerms))
    {
        SendReply(admin, "You don't have permission to execute this command.");
        return;
    }

    if (args.Args == null || args.Args.Length < 1)
    {
        SendReply(admin, "Usage: kits.copyinvteam <teamName>");
        return;
    }

    // Strip surrounding quotes from teamName
    string teamName = args.Args[0].Trim('\"');

    // Handle "Global" as a special case
    bool isGlobal = teamName.Equals("Global", StringComparison.OrdinalIgnoreCase);

    // Validate team name
    if (!isGlobal && !teamNames.Contains(teamName))
    {
        SendReply(admin, $"[Kits] Team \"{teamName}\" does not exist.");
        return;
    }

    // Get the admin's inventory items
    var adminInventory = admin.inventory.containerMain.itemList.ToList(); // Main inventory
    var adminBelt = admin.inventory.containerBelt.itemList.ToList(); // Belt inventory
    var adminWear = admin.inventory.containerWear.itemList.ToList(); // Wear inventory
 

    // Combine all inventory items
    var allAdminItems = new List<Item>();
    allAdminItems.AddRange(adminInventory);
    allAdminItems.AddRange(adminBelt);
    allAdminItems.AddRange(adminWear);
   

    if (allAdminItems.Count == 0)
    {
        SendReply(admin, "Your inventory is empty. Nothing to copy.");
        return;
    }

    // Find target players
    var targetPlayers = new List<BasePlayer>();

    if (isGlobal)
    {
        targetPlayers = BasePlayer.activePlayerList.Where(p => p != admin).ToList();
    }
    else
    {
        targetPlayers = playerTeams
            .Where(kvp => kvp.Value.Equals(teamName, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => BasePlayer.FindByID(ulong.Parse(kvp.Key)))
            .Where(p => p != null && p != admin)
            .ToList();
    }

    if (targetPlayers.Count == 0)
    {
        SendReply(admin, $"No other players found in team \"{teamName}\" to copy inventory to.");
        return;
    }

    int successCount = 0;
    int failCount = 0;

    foreach (var target in targetPlayers)
    {
        bool allItemsAdded = true;

        foreach (var item in allAdminItems)
        {
            // Clone the item to avoid reference issues
            var clonedItem = ItemManager.CreateByItemID(item.info.itemid, item.amount, item.skin);

            if (clonedItem == null)
            {
                PrintWarning($"Failed to clone item ID {item.info.itemid}.");
                allItemsAdded = false;
                continue;
            }

            // Attempt to move the cloned item to the target's inventory
            if (!target.inventory.GiveItem(clonedItem))
            {
                PrintWarning($"Failed to give item ID {item.info.itemid} to player {target.displayName}.");
                allItemsAdded = false;
                // Destroy the cloned item to prevent duplication
                clonedItem.Remove();
            }
        }

        if (allItemsAdded)
        {
            target.ChatMessage($"[Kits] Your inventory has been updated with a copy from {admin.displayName}.");
            successCount++;
        }
        else
        {
            target.ChatMessage($"[Kits] Some items from {admin.displayName}'s inventory could not be copied.");
            failCount++;
        }
    }

    SendReply(admin, $"[Kits] Copied inventory to {successCount} player(s) in team \"{teamName}\". Failed to copy to {failCount} player(s).");
}




[ConsoleCommand("kits.kitspage")]
private void CmdKitsPage(ConsoleSystem.Arg args)
{
    BasePlayer player = args.Connection?.player as BasePlayer;
    if (player == null)
    {
        PrintWarning("CmdKitsPage called by non-player.");
        return;
    }

    PrintToConsole($"CmdKitsPage called by {player.displayName} with args: {string.Join(", ", args.Args)}");

    if (args.Args == null || args.Args.Length < 1)
    {
        SendReply(player, "Usage: kits.kitspage <pageNumber>");
        return;
    }

    if (!int.TryParse(args.Args[0], out int page))
    {
        SendReply(player, "Invalid page number.");
        return;
    }

    OpenMainMenu(player, "kits", null, null, page);
}

[ConsoleCommand("kits.wipeinvteam")]
private void CmdWipeInvTeam(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin == null)
    {
        PrintWarning("CmdWipeInvTeam called by non-player.");
        return;
    }

    PrintToConsole($"CmdWipeInvTeam called by {admin.displayName} with args: {string.Join(", ", args.Args)}");

    // Permission check
    if (!permission.UserHasPermission(admin.UserIDString, PermissionManagePerms))
    {
        SendReply(admin, "You don't have permission to execute this command.");
        return;
    }

    if (args.Args == null || args.Args.Length < 1)
    {
        SendReply(admin, "Usage: kits.wipeinvteam <teamName>");
        return;
    }

    // Strip surrounding quotes from teamName
    string teamName = args.Args[0].Trim('\"');

    // Handle "Global" as a special case
    if (teamName.Equals("Global", StringComparison.OrdinalIgnoreCase))
    {
        // Wipe inventory for all connected players
        int wipedCount = 0;
        foreach (var player in BasePlayer.activePlayerList)
        {
            // Check for bypass permission
            if (permission.UserHasPermission(player.UserIDString, "kits.bypass"))
            {
                continue; // Skip players with bypass permission
            }

            player.inventory.Strip(); // Remove all items
            player.ChatMessage($"[Kits] Your inventory has been wiped by an administrator for the Global team.");
            wipedCount++;
        }

        SendReply(admin, $"[Kits] Wiped inventory for {wipedCount} player(s) globally.");
        return;
    }

    // Validate team name
    if (!teamNames.Contains(teamName))
    {
        SendReply(admin, $"[Kits] Team \"{teamName}\" does not exist.");
        return;
    }

    // Wipe inventory for each player in the team
    int teamWipedCount = 0;
    foreach (var kvp in playerTeams)
    {
        if (kvp.Value.Equals(teamName, StringComparison.OrdinalIgnoreCase))
        {
            if (ulong.TryParse(kvp.Key, out ulong playerId))
            {
                var targetPlayer = BasePlayer.FindByID(playerId);
                if (targetPlayer != null && targetPlayer.IsConnected)
                {
                    // Check for bypass permission
                    if (permission.UserHasPermission(targetPlayer.UserIDString, "kits.bypass"))
                    {
                        continue; // Skip players with bypass permission
                    }

                    targetPlayer.inventory.Strip(); // Remove all items
                    targetPlayer.ChatMessage($"[Kits] Your inventory has been wiped by an administrator for team \"{teamName}\".");
                    teamWipedCount++;
                }
            }
            else
            {
                PrintWarning($"Invalid UserIDString '{kvp.Key}' in playerTeams dictionary.");
            }
        }
    }

    SendReply(admin, $"[Kits] Wiped inventory for {teamWipedCount} player(s) in team \"{teamName}\".");
}



[ConsoleCommand("kits.giveteam")]
private void CmdGiveKitTeam(ConsoleSystem.Arg args)
{
    BasePlayer admin = args.Connection?.player as BasePlayer;
    if (admin == null)
    {
        PrintWarning("CmdGiveKitTeam called by non-player.");
        return;
    }

    PrintToConsole($"CmdGiveKitTeam called by {admin.displayName} with args: {string.Join(", ", args.Args)}");

    // Permission check
    if (!permission.UserHasPermission(admin.UserIDString, PermissionManagePerms))
    {
        SendReply(admin, "You don't have permission to execute this command.");
        return;
    }

    if (args.Args == null || args.Args.Length < 2)
    {
        SendReply(admin, "Usage: kits.giveteam <teamName> <kitName>");
        return;
    }

    // Strip surrounding quotes from teamName and kitName
    string teamName = args.Args[0].Trim('\"');
    string kitName = string.Join(" ", args.Args.Skip(1)).Trim('\"');

    // Handle "Global" as a special case
    if (teamName.Equals("Global", StringComparison.OrdinalIgnoreCase))
    {
        // Assign kit to all connected players
        int successCount = 0;
        int failCount = 0;

        foreach (var player in BasePlayer.activePlayerList)
        {
            // Check for bypass permission
            if (permission.UserHasPermission(player.UserIDString, "kits.bypass"))
            {
                continue; // Skip players with bypass permission
            }

            object result = GiveKit(player, kitName);
            if (result is bool success && success)
            {
                player.ChatMessage($"[Kits] You have been given the kit \"{kitName}\" by an administrator.");
                successCount++;
            }
            else if (result is string errorMsg)
            {
                player.ChatMessage($"[Kits] Failed to receive kit \"{kitName}\": {errorMsg}");
                failCount++;
            }
            else
            {
                player.ChatMessage($"[Kits] An unknown error occurred while giving kit \"{kitName}\".");
                failCount++;
            }
        }

        SendReply(admin, $"[Kits] Assigned kit \"{kitName}\" to {successCount} player(s) globally. Failed: {failCount}.");
        return;
    }

    // Validate team name
    if (!teamNames.Contains(teamName))
    {
        SendReply(admin, $"[Kits] Team \"{teamName}\" does not exist.");
        return;
    }

    // Validate kit name
    var allKits = new List<string>();
    GetKitNames(allKits);
    if (!allKits.Contains(kitName))
    {
        SendReply(admin, $"[Kits] Kit \"{kitName}\" does not exist.");
        return;
    }

    // Give the kit to each player in the team
    int teamSuccessCount = 0;
    int teamFailCount = 0;

    foreach (var kvp in playerTeams)
    {
        if (kvp.Value.Equals(teamName, StringComparison.OrdinalIgnoreCase))
        {
            if (ulong.TryParse(kvp.Key, out ulong playerId))
            {
                var targetPlayer = BasePlayer.FindByID(playerId);
                if (targetPlayer != null && targetPlayer.IsConnected)
                {
                    // Check for bypass permission
                    if (permission.UserHasPermission(targetPlayer.UserIDString, "kits.bypass"))
                    {
                        continue; // Skip players with bypass permission
                    }

                    object result = GiveKit(targetPlayer, kitName);
                    if (result is bool success && success)
                    {
                        targetPlayer.ChatMessage($"[Kits] You have been given the kit \"{kitName}\" by an administrator.");
                        teamSuccessCount++;
                    }
                    else if (result is string errorMsg)
                    {
                        targetPlayer.ChatMessage($"[Kits] Failed to receive kit \"{kitName}\": {errorMsg}");
                        teamFailCount++;
                    }
                    else
                    {
                        targetPlayer.ChatMessage($"[Kits] An unknown error occurred while giving kit \"{kitName}\".");
                        teamFailCount++;
                    }
                }
            }
            else
            {
                PrintWarning($"Invalid UserIDString '{kvp.Key}' in playerTeams dictionary.");
            }
        }
    }

    SendReply(admin, $"[Kits] Assigned kit \"{kitName}\" to {teamSuccessCount} player(s) in team \"{teamName}\". Failed: {teamFailCount}.");
}









    }
}