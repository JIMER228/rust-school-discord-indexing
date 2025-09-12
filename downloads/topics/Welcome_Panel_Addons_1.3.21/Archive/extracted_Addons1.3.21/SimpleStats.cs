using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("SimpleStats", "David", "1.3.2")]
    public class SimpleStats : RustPlugin
    {

        #region [Config] 

        private Configuration config;
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = Configuration.CreateConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        class Configuration
        {
            [JsonProperty(PropertyName = "Main Settings")]
            public MainSet mainSet { get; set; }

            public class MainSet
            {
                [JsonProperty("Chat Command")]
                public string chatCommand { get; set; }

                [JsonProperty("Count Suicides")]
                public bool countSuicides { get; set; }

                [JsonProperty("Count Npc Kills")]
                public bool countNpcs { get; set; }

                [JsonProperty("Use as WelcomePanel Addon")]
                public bool isAddon { get; set; }

                [JsonProperty("Data refresh interval (seconds)")]
                public float refreshInterval { get; set; }

                [JsonProperty("Clear data on wipe.")]
                public bool wipe { get; set; }

                [JsonProperty("Require permission to open stats")]
                public bool perm { get; set; }
            }

            [JsonProperty(PropertyName = "User Interface")]
            public UiSet uiSet { get; set; }

            public class UiSet
            {
                [JsonProperty("Title Text")]
                public string title { get; set; }

                [JsonProperty("Background Color")]
                public string backgroundColor { get; set; }

                [JsonProperty("Top Panel Color")]
                public string titlePanelColor { get; set; }

                [JsonProperty("Player entry panel color 1.")]
                public string panelColor1 { get; set; }

                [JsonProperty("Player entry panel color 2.")]
                public string panelColor2 { get; set; }

                [JsonProperty("Self entry panel color")]
                public string selfColor { get; set; }
            }

            public static Configuration CreateConfig()
            {
                return new Configuration
                {
                    mainSet = new SimpleStats.Configuration.MainSet
                    {

                        chatCommand = "stats",
                        countSuicides = true,
                        countNpcs = false,
                        isAddon = false,
                        refreshInterval = 120f,
                        wipe = false,
                        perm = false,
                    },
                    uiSet = new SimpleStats.Configuration.UiSet
                    {

                        title = "<b>RUSTER.NET 10X</b> STATS",
                        backgroundColor = "0.1 0.1 0.1 0.5",
                        titlePanelColor = "0.70 0.67 0.65 0.35",
                        panelColor1 = "0.70 0.67 0.65 0.07",
                        panelColor2 = "0.70 0.67 0.65 0.13",
                        selfColor = "0.161 0.384 0.569 1",
                    },


                };
            }
        }
        #endregion

        [PluginReference]
        Plugin ImageLibrary, WelcomePanel;

        string wp_layer = "content";

        void ForceAddon()
        {
            if (config.mainSet.isAddon) return;

            config.mainSet.isAddon = true;
            CacheData();
        }

        void OnWelcomePanelPageOpen(BasePlayer player, int tab, int page, string addon)
        {   
            if(addon == null) return;

            if (addon.ToLower().Contains("stats"))
            {   
                // 4.3.4 version
                wp_layer = "wp_content";

                // 3.2 version
                if (WelcomePanel.Version.Major == 3)
                    wp_layer = "WelcomePanel_content";

                // 4.0.9 version
                if (WelcomePanel.Version.Major == 4 && WelcomePanel.Version.Minor < 3)
                    wp_layer = "content";

                ForceAddon();
                OpenStats(player);
            }
        }

        string mainLayer = "SimpleStats_main";
        private List<string> cachedPlayerPanels = new List<string>();
        string cachedBase;
        private List<ulong> playerOrderCached = new List<ulong>();

        #region Hooks

        private void OnServerInitialized()
        {
            LoadData();

            permission.RegisterPermission($"{Name}.exclude", this);

            if (config.mainSet.perm)
                permission.RegisterPermission($"{Name}.use", this);

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (permission.UserHasPermission(player.UserIDString, $"{Name}.exclude"))
                {
                    if (playerData.ContainsKey(player.userID))
                        playerData.Remove(player.userID);

                    continue;
                }

                if (!playerData.ContainsKey(player.userID))
                    playerData.Add(player.userID, new PlayerData());

                playerData[player.userID].name = player.displayName;

                //OpenStats(player);
            }

            SaveData();

            CacheData();

            timer.Every(config.mainSet.refreshInterval, () => {
                CacheData();
            });

            cmd.AddChatCommand(config.mainSet.chatCommand, this, "StatsChatCmd");
        }

        private void OnServerSave()
        {
            SaveData();
        }

        void OnNewSave()
        {
            if (config.mainSet.wipe)
            {
                LoadData();
                playerData.Clear();
                SaveData();
            }
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Name + "_background");
                CuiHelper.DestroyUi(player, mainLayer);
            }
            SaveData();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;

            if (permission.UserHasPermission(player.UserIDString, $"{Name}.exclude"))
            {
                if (playerData.ContainsKey(player.userID))
                    playerData.Remove(player.userID);

                return;
            }

            if (!playerData.ContainsKey(player.userID))
            {
                playerData.Add(player.userID, new PlayerData());
                playerData[player.userID].name = player.displayName;
            }
        }

        private void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            try
            {
                if (player == null) return;
                // admin bypass
                if (permission.UserHasPermission(player.UserIDString, $"{Name}.exclude")) return;
                
                var attacker = info.InitiatorPlayer;

                if (info == null || attacker == null) 
                {   
                    playerData[player.userID].deaths++;
                    return;
                }

                // admin bypass
                if (attacker != null && permission.UserHasPermission(attacker.UserIDString, $"{Name}.exclude")) return;
                // suicide
                if (attacker == player)
                {   
                    if (!config.mainSet.countSuicides) return;

                    playerData[player.userID].deaths++;
                    return;
                }
                if (playerData.ContainsKey(player.userID))
                    playerData[player.userID].deaths++;

                if (info.HitEntity.IsNpc && !config.mainSet.countNpcs) return;
                playerData[attacker.userID].kills++;

            }
            catch
            {   
                if (permission.UserHasPermission(player.UserIDString, $"{Name}.exclude")) return;

                if (playerData.ContainsKey(player.userID))
                    playerData[player.userID].deaths++;
            }
        }

        #endregion

        #region Commands

        [ConsoleCommand("topstats")]
        void CommandTopStats(ConsoleSystem.Arg arg)
        {
            var sb = new StringBuilder("SteamId: PlayerName | Kills | Deaths | KDR\n");
            foreach (var data in playerData.OrderByDescending(x=>x.Value.kills).Take(10))
            {
                var playerId = data.Key;
                var pd = data.Value;
                var ratio = pd.deaths == 0 ? pd.kills : (float) pd.kills / (float) pd.deaths;

                sb.Append($"{playerId}: {pd.name} | {pd.kills} | {pd.deaths} | {ratio}\n");
            }
            Interface.Oxide.LogInfo(sb.ToString());
        }
        
        [ConsoleCommand("mystats")]
        void CommandMyStats(ConsoleSystem.Arg arg)
        {
            if (arg != null && arg.HasArgs() && !string.IsNullOrEmpty(arg.Args[0]))
            {
                if (playerData.ContainsKey(ulong.Parse(arg.Args[0])))
                {
                    var pd = playerData[ulong.Parse(arg.Args[0])];
                    var ratio = pd.deaths == 0 ? pd.kills : (float) pd.kills / (float) pd.deaths;
                    Interface.Oxide.LogInfo($"SteamId: PlayerName | Kills | Deaths | KDR\n{arg.Args[0]}: {pd.name} | {pd.kills} | {pd.deaths} | {ratio}\n");
                }
            }
        }

        void StatsChatCmd(BasePlayer player, string command, string[] args)
        {
            if (command.ToLower() == config.mainSet.chatCommand && !config.mainSet.isAddon)
            {
                if (config.mainSet.perm && !permission.UserHasPermission(player.UserIDString, $"{Name}.use"))
                {
                    SendReply(player, "You don't have permission to use this command");
                    return;
                }

                OpenStats(player);
            }
        }

        [ConsoleCommand("stats_wipe")]
        private void stats_wipe(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player != null && !player.IsAdmin) return;

            playerData.Clear();
            SaveData();
            Puts("Stats wiped, reloading plugin...");
            Oxide.Core.Interface.Oxide.ReloadPlugin(Name);
            if (player != null)
                SendReply(player, "Stats wiped, reloading plugin...");
           
        }

        [ConsoleCommand("close_stats")]
        private void close_stats(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null) return;

            CuiHelper.DestroyUi(player, Name + "_background");
            CuiHelper.DestroyUi(player, mainLayer);
        }

        [ConsoleCommand("simplestats_nextpage")]
        private void simplestats_nextpage(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            var args = arg.Args;
            if (player == null) return;
            if (args.Length < 1) return;

            OpenStats(player, Convert.ToInt32(args[0]), false);
        }

        #endregion

        #region UI

        string[] anchorMax = { "1 0.92", "1 0.85", "1 0.78", "1 0.71", "1 0.64", "1 0.57", "1 0.5", "1 0.43", "1 0.36", "1 0.29", "1 0.22", "1 0.15", "1 0.08", "1 0.01" };
        string[] anchorMin = { "0 0.86", "0 0.79", "0 0.72", "0 0.65", "0 0.58", "0 0.51", "0 0.44", "0 0.37", "0 0.3", "0 0.23", "0 0.16", "0 0.09", "0 0.02", "0 -0.05" };

        private void CacheData()
        {
            cachedPlayerPanels.Clear();
            cachedBase = "";
            playerOrderCached.Clear();
            CreateBase();
            CreatePlayerPanels();
        }

        private void OpenStats(BasePlayer player, int page = 0, bool init = true)
        {
            if (init)
            {
                CuiHelper.DestroyUi(player, Name + "_background");
                CuiHelper.DestroyUi(player, mainLayer);
                CuiHelper.AddUi(player, cachedBase);
            }
            int startingIndex = 11 * page;
            int lastEntry = 0;

            if (!init)
                for (var i = 0; i < 12; i++)
                {
                    CuiHelper.DestroyUi(player, $"player_panel{i}");
                }

            for (var i = 0; i < 11; i++)
            {
                try
                {

                    if (i * page > cachedPlayerPanels.Count - 1) continue;

                    CuiHelper.AddUi(player, cachedPlayerPanels[startingIndex + i]);
                    lastEntry++;
                }
                catch
                {

                    break;
                }
            }

            var ui = new CuiElementContainer();

            if (!permission.UserHasPermission(player.UserIDString, $"{Name}.exclude"))
            {
                float fade = float.Parse($"0.{lastEntry}");
                int rank = playerOrderCached.IndexOf(player.userID) + 1;
                string color = config.uiSet.selfColor;
                float ratio = playerData[player.userID].deaths == 0 ? playerData[player.userID].kills : (float) playerData[player.userID].kills / (float) playerData[player.userID].deaths;
                
                CUIClass.CreatePanel(ref ui, $"player_panel{lastEntry}", mainLayer, color, anchorMin[lastEntry], anchorMax[lastEntry], false, fade, 0f, "assets/content/ui/uibackgroundblur.mat");
                CUIClass.CreateText(ref ui, "title_name", $"player_panel{lastEntry}", "1 1 1 0.6", $"{rank}", 15, "0.04 0", "1 1", TextAnchor.MiddleLeft, "robotocondensed-regular.ttf", fade);
                CUIClass.CreateText(ref ui, "title_name", $"player_panel{lastEntry}", "1 1 1 0.6", $"<b>{playerData[player.userID].name.Truncate(30)}</b>", 15, "0.13 0", "1 1", TextAnchor.MiddleLeft, "robotocondensed-regular.ttf", fade);
                CUIClass.CreateText(ref ui, "title_kills", $"player_panel{lastEntry}", "1 1 1 0.6", $"  <b>{playerData[player.userID].kills}</b>", 15, "0.43 0", "0.51 1", TextAnchor.MiddleCenter, "robotocondensed-regular.ttf", fade);
                CUIClass.CreateText(ref ui, "title_deaths", $"player_panel{lastEntry}", "1 1 1 0.6", $"<b>{playerData[player.userID].deaths}</b>", 15, "0.65 0", "0.73 1", TextAnchor.MiddleCenter, "robotocondensed-regular.ttf", fade);
                CUIClass.CreateText(ref ui, "title_ratio", $"player_panel{lastEntry}", "1 1 1 0.6", $"<b>{ratio:0.00}</b>", 15, "0.85 0", "0.92 1", TextAnchor.MiddleCenter, "robotocondensed-regular.ttf", fade);
            }

            if (page != 4 && startingIndex + 10 < cachedPlayerPanels.Count)
                CUIClass.CreateButton(ref ui, "simplestats_nextpage", mainLayer, "0.70 0.67 0.65 0.35", "NEXT", 13, $"0.92 {GetHorizontalPoint(anchorMin[lastEntry + 1])}", $"1 {GetHorizontalPoint(anchorMax[lastEntry + 1])}", $"simplestats_nextpage {page + 1}", "", "1 1 1 0.45", 0f, TextAnchor.MiddleCenter, "robotocondensed-bold.ttf", "assets/icons/iconmaterial.mat");

            if (page != 0)
                CUIClass.CreateButton(ref ui, "simplestats_nextprev", mainLayer, "0.70 0.67 0.65 0.35", "BACK", 13, $"0 {GetHorizontalPoint(anchorMin[lastEntry + 1])}", $"0.08 {GetHorizontalPoint(anchorMax[lastEntry + 1])}", $"simplestats_nextpage {page - 1}", "", "1 1 1 0.45", 0f, TextAnchor.MiddleCenter, "robotocondensed-bold.ttf", "assets/icons/iconmaterial.mat");

            CuiHelper.DestroyUi(player, $"simplestats_nextpage");
            CuiHelper.DestroyUi(player, $"simplestats_nextprev");
            CuiHelper.AddUi(player, ui);
        }

        private void CreateBase()
        {
            var ui = new CuiElementContainer();
            if (!config.mainSet.isAddon)
            {
                CUIClass.CreatePanel(ref ui, Name + "_background", "Overlay", config.uiSet.backgroundColor, "0 0", "1 1", true, 1f, 0f, "assets/content/ui/uibackgroundblur.mat");
                CUIClass.PullFromAssets(ref ui, "texture", Name + "_background", "0 0 0 0.96", "assets/content/ui/ui.background.transparent.radial.psd", 1f);
                CUIClass.CreateButton(ref ui, "closebtn", "texture", "0 0 0 0", "", 0, "0 0", "1 1", "close_stats", "", "0 0 0 0", 0f, TextAnchor.MiddleCenter, "", "assets/icons/iconmaterial.mat");
            }
            if (!config.mainSet.isAddon)
                CUIClass.CreatePanel(ref ui, mainLayer, "Overlay", "0.1 0.1 0.1 0", "0.5 0.5", "0.5 0.5", true, 1f, 0f, "assets/icons/iconmaterial.mat", "-425 -285", "425 200");
            else
                CUIClass.CreatePanel(ref ui, mainLayer, wp_layer, "0.1 0.1 0.1 0", "0 0", "1 1", true, 1f, 0f, "assets/icons/iconmaterial.mat", "0 0", "0 0");
            if (!config.mainSet.isAddon) CUIClass.CreateText(ref ui, "servername", mainLayer, "1 1 1 0.6", config.uiSet.title, 75, "0 1", "1 1.3", TextAnchor.LowerLeft, "robotocondensed-regular.ttf");

            CUIClass.CreatePanel(ref ui, "title_panel", mainLayer, config.uiSet.titlePanelColor, "-0.005 0.93", "1.005 1", false, 0.5f, 0f, "assets/content/ui/uibackgroundblur.mat");
            CUIClass.CreateText(ref ui, "title_name", "title_panel", "1 1 1 0.6", "<b><size=20>#</size></b>", 15, "0.04 0", "0.08 1", TextAnchor.MiddleLeft, "robotocondensed-regular.ttf");
            CUIClass.CreateText(ref ui, "title_name", "title_panel", "1 1 1 0.6", "<b>NAME</b>", 15, "0.13 0", "0.2 1", TextAnchor.MiddleLeft, "robotocondensed-regular.ttf");
            CUIClass.CreateText(ref ui, "title_kills", "title_panel", "1 1 1 0.6", "<b>KILLS</b>", 15, "0.43 0", "0.52 1", TextAnchor.MiddleCenter, "robotocondensed-regular.ttf");
            CUIClass.CreateText(ref ui, "title_deaths", "title_panel", "1 1 1 0.6", "<b>DEATHS</b>", 15, "0.65 0", "0.73 1", TextAnchor.MiddleCenter, "robotocondensed-regular.ttf");
            CUIClass.CreateText(ref ui, "title_ratio", "title_panel", "1 1 1 0.6", "<b>RATIO</b>", 15, "0.85 0", "0.91 1", TextAnchor.MiddleCenter, "robotocondensed-regular.ttf");

            cachedBase = $"{ui}";
        }

        private void CreatePlayerPanels()
        {
            var order = playerData.OrderByDescending(x => x.Value.kills).Take(50);
            var playerList = order.ToList();

            int i = 0;
            int rank = 1;
            foreach (var playerEntry in playerList)
            {
                playerOrderCached.Add(playerEntry.Key);
                if (i > 10) i = 0;
                ulong userId = playerEntry.Key;
                string color = config.uiSet.panelColor2;
                if (i % 2 == 0)
                {
                    color = config.uiSet.panelColor1;
                }

                var ui = new CuiElementContainer();

                float fade = float.Parse($"0.{i}");
                var ratio = playerData[userId].deaths == 0 ? playerData[userId].kills : (float) playerData[userId].kills / (float) playerData[userId].deaths;

                CUIClass.CreatePanel(ref ui, $"player_panel{i}", mainLayer, color, anchorMin[i], anchorMax[i], false, fade, 0f, "assets/content/ui/uibackgroundblur.mat");
                CUIClass.CreateText(ref ui, "title_name", $"player_panel{i}", "1 1 1 0.6", $"{rank}", 15, "0.04 0", "1 1", TextAnchor.MiddleLeft, "robotocondensed-regular.ttf", fade);
                CUIClass.CreateText(ref ui, "title_name", $"player_panel{i}", "1 1 1 0.6", $"<b>{playerData[userId].name.Truncate(30)}</b>", 15, "0.13 0", "1 1", TextAnchor.MiddleLeft, "robotocondensed-regular.ttf", fade);
                CUIClass.CreateText(ref ui, "title_kills", $"player_panel{i}", "1 1 1 0.6", $"<b>{playerData[userId].kills}</b>", 15, "0.43 0", "0.52 1", TextAnchor.MiddleCenter, "robotocondensed-regular.ttf", fade);
                CUIClass.CreateText(ref ui, "title_deaths", $"player_panel{i}", "1 1 1 0.6", $"<b>{playerData[userId].deaths}</b>", 15, "0.65 0", "0.73 1", TextAnchor.MiddleCenter, "robotocondensed-regular.ttf", fade);
                CUIClass.CreateText(ref ui, "title_ratio", $"player_panel{i}", "1 1 1 0.6", $"<b>{ratio:0.00}</b>", 15, "0.85 0", "0.92 1", TextAnchor.MiddleCenter, "robotocondensed-regular.ttf", fade);

                cachedPlayerPanels.Add($"{ui}");

                rank++;
                i++;
            }
        }

        private string GetHorizontalPoint(string anchor)
        {
            string[] split = anchor.Split(' ');
            return split[1];
        }

        /*
        private void CreateUi(BasePlayer player)
        {   stopWatch.Start();
            var ui = new CuiElementContainer();
            CUIClass.CreatePanel(ref ui, "background", "Overlay", "0.1 0.1 0.1 0.5", "0 0", "1 1", true, 1f , 0f, "assets/content/ui/uibackgroundblur.mat");
            CUIClass.PullFromAssets(ref ui, "texture", "background", "0 0 0 0.96", "assets/content/ui/ui.background.transparent.radial.psd", 1f);
            CUIClass.CreateButton(ref ui, "closebtn", "texture", "0 0 0 0", "", 0, "0 0", "1 1", "close_stats", "", "0 0 0 0", 0f, TextAnchor.MiddleCenter, "", "assets/icons/iconmaterial.mat");
            CUIClass.CreatePanel(ref ui, mainLayer, "Overlay", "0.1 0.1 0.1 0", "0.5 0.5", "0.5 0.5", true, 1f , 0f, "assets/icons/iconmaterial.mat", "-425 -285", "425 200");
            CUIClass.CreateText(ref ui, "servername", mainLayer, "1 1 1 0.6", "<b>RUSTER.NET 10X</b> STATS", 75, "0 1", "1 1.3", TextAnchor.LowerLeft, "robotocondensed-regular.ttf");
            
            CUIClass.CreatePanel(ref ui, "title_panel", mainLayer, "0.70 0.67 0.65 0.35", "-0.005 0.93", "1.005 1", false, 0.5f , 0f, "assets/content/ui/uibackgroundblur.mat");
                CUIClass.CreateText(ref ui, "title_name", "title_panel", "1 1 1 0.6", "<b><size=20>#</size></b>", 15, "0.04 0", "0.08 1", TextAnchor.MiddleLeft, "robotocondensed-regular.ttf");
                CUIClass.CreateText(ref ui, "title_name", "title_panel", "1 1 1 0.6", "<b>NAME</b>", 15, "0.13 0", "0.2 1", TextAnchor.MiddleLeft, "robotocondensed-regular.ttf");
                CUIClass.CreateText(ref ui, "title_kills", "title_panel", "1 1 1 0.6", "<b>KILLS</b>", 15, "0.45 0", "1 1", TextAnchor.MiddleLeft, "robotocondensed-regular.ttf");
                CUIClass.CreateText(ref ui, "title_deaths", "title_panel", "1 1 1 0.6", "<b>DEATHS</b>", 15, "0.65 0", "1 1", TextAnchor.MiddleLeft, "robotocondensed-regular.ttf");
                CUIClass.CreateText(ref ui, "title_ratio", "title_panel", "1 1 1 0.6", "<b>RATIO</b>", 15, "0.85 0", "1 1", TextAnchor.MiddleLeft, "robotocondensed-regular.ttf");
            
            var order = playerData.OrderByDescending(x => x.Value.kills).Take(50);
            var playerList = order.ToList();

            
            
           
            for (var i = 0; i < 11; i++)
            {   

                ulong userId = playerList[i].Key;
                int rank = i + 1;
                string color = "0.70 0.67 0.65 0.13";
            

                if(i % 2 == 0) {
                    color = "0.70 0.67 0.65 0.07";
                }

                if (i == 10) {
                    color = "0.161 0.384 0.569 1";
                    userId = player.userID;
                    rank = 0;

                    foreach (var entry in order)
                    {   
                        rank++;

                        if (entry.Key == player.userID)
                            break;  
                    }
                }
                
                float fade = float.Parse($"0.{i}");

                CUIClass.CreatePanel(ref ui, $"player_panel{i}", mainLayer, color, anchorMin[i], anchorMax[i], false, fade, 0f, "assets/content/ui/uibackgroundblur.mat");
                    CUIClass.CreateText(ref ui, "title_name", $"player_panel{i}", "1 1 1 0.6", $"{rank}", 15, "0.04 0", "1 1", TextAnchor.MiddleLeft, "robotocondensed-regular.ttf", fade);
                    CUIClass.CreateText(ref ui, "title_name", $"player_panel{i}", "1 1 1 0.6", $"<b>{playerData[userId].name.Truncate(30)}</b>", 15, "0.13 0", "1 1", TextAnchor.MiddleLeft, "robotocondensed-regular.ttf", fade);
                    CUIClass.CreateText(ref ui, "title_kills", $"player_panel{i}", "1 1 1 0.6", $"<b>{playerData[userId].kills}</b>", 15, "0.45 0", "0.49 1", TextAnchor.MiddleCenter, "robotocondensed-regular.ttf", fade);
                    CUIClass.CreateText(ref ui, "title_deaths", $"player_panel{i}", "1 1 1 0.6", $"<b>{playerData[userId].deaths}</b>", 15, "0.65 0", "0.71 1", TextAnchor.MiddleCenter, "robotocondensed-regular.ttf", fade);
                    CUIClass.CreateText(ref ui, "title_ratio", $"player_panel{i}", "1 1 1 0.6", $"<b>{(float)playerData[userId].kills / (float)playerData[userId].deaths:0.00}</b>", 15, "0.85 0", "0.9 1", TextAnchor.MiddleCenter, "robotocondensed-regular.ttf", fade);

            }
            

            CuiHelper.DestroyUi(player, "background");
            CuiHelper.DestroyUi(player, mainLayer);
            CuiHelper.AddUi(player, ui);
            Puts($"UI Builded in {stopWatch.Elapsed.TotalMilliseconds}ms");
        }
        */
        #endregion

        #region Cui Class

        public class CUIClass
        {

            public static void CreatePanel(ref CuiElementContainer _container, string _name, string _parent, string _color, string _anchorMin, string _anchorMax, bool _cursorOn = false, float _fadeIn = 0f, float _fadeOut = 0f, string _mat2 = "", string _OffsetMin = "", string _OffsetMax = "", bool keyboard = false)
            {
                _container.Add(new CuiPanel
                {
                    Image = { Color = _color, Material = _mat2, FadeIn = _fadeIn },
                    RectTransform = { AnchorMin = _anchorMin, AnchorMax = _anchorMax, OffsetMin = _OffsetMin, OffsetMax = _OffsetMax },
                    FadeOut = _fadeOut,
                    CursorEnabled = _cursorOn,
                    KeyboardEnabled = keyboard
                },
                _parent,
                _name);
            }

            public static void PullFromAssets(ref CuiElementContainer _container, string _name, string _parent, string _color, string _sprite, float _fadeIn = 0f, float _fadeOut = 0f, string _anchorMin = "0 0", string _anchorMax = "1 1", string _material = "assets/icons/iconmaterial.mat")
            {
                //assets/content/textures/generic/fulltransparent.tga MAT
                _container.Add(new CuiElement
                {
                    Parent = _parent,
                    Name = _name,
                    Components =
                            {
                                new CuiImageComponent { Material = _material, Sprite = _sprite, Color = _color, FadeIn = _fadeIn},
                                new CuiRectTransformComponent {AnchorMin = _anchorMin, AnchorMax = _anchorMax}
                            },
                    FadeOut = _fadeOut
                });
            }

            public static void CreateText(ref CuiElementContainer _container, string _name, string _parent, string _color, string _text, int _size, string _anchorMin, string _anchorMax, TextAnchor _align = TextAnchor.MiddleCenter, string _font = "robotocondensed-bold.ttf", float _fadeIn = 0f, float _fadeOut = 0f, string _outlineColor = "0 0 0 0", string _outlineScale = "0 0")
            {
                _container.Add(new CuiElement
                {
                    Parent = _parent,
                    Name = _name,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = _text,
                            FontSize = _size,
                            Font = _font,
                            Align = _align,
                            Color = _color,
                            FadeIn = _fadeIn,
                        },

                        new CuiOutlineComponent
                        {

                            Color = _outlineColor,
                            Distance = _outlineScale

                        },

                        new CuiRectTransformComponent
                        {
                             AnchorMin = _anchorMin,
                             AnchorMax = _anchorMax
                        }
                    },
                    FadeOut = _fadeOut
                });
            }

            public static void CreateButton(ref CuiElementContainer _container, string _name, string _parent, string _color, string _text, int _size, string _anchorMin, string _anchorMax, string _command = "", string _close = "", string _textColor = "0.843 0.816 0.78 1", float _fade = 1f, TextAnchor _align = TextAnchor.MiddleCenter, string _font = "", string _material = "assets/content/ui/uibackgroundblur-ingamemenu.mat")
            {

                _container.Add(new CuiButton
                {
                    Button = { Close = _close, Command = _command, Color = _color, Material = _material, FadeIn = _fade },
                    RectTransform = { AnchorMin = _anchorMin, AnchorMax = _anchorMax },
                    Text = { Text = _text, FontSize = _size, Align = _align, Color = _textColor, Font = _font, FadeIn = _fade }
                },
                _parent,
                _name);
            }
        }
        #endregion

        #region Data

        private void SaveData()
        {
            if (playerData != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/PlayerData", playerData);
        }

        private Dictionary<ulong, PlayerData> playerData;

        private class PlayerData
        {
            public int kills;
            public int deaths;
            public string name;
        }

        private void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/PlayerData"))
            {
                playerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>($"{Name}/PlayerData");
            }
            else
            {
                playerData = new Dictionary<ulong, PlayerData>();
                SaveData();
            }
        }


        #endregion


    }
}