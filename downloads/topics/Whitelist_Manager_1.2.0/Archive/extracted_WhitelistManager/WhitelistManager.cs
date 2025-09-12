/*
This plugin (the software) is © copyright Von.

You may not copy, modify, merge, publish, distribute, sublicense, or sell copies of this software without explicit consent from the_kiiiing.

DISCLAIMER:

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using System.Collections.Generic;
using System;
using Network;
using UnityEngine;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using System.IO;
using System.Net;
using System.Threading;
using Oxide.Core.Libraries;

namespace Oxide.Plugins
{
    [Info("WhitelistManager", "Von", "1.2.0")]
    [Description("Manages server whitelist with admin commands, UI and RCON support")]
    class WhitelistManager : RustPlugin
    {
        private HashSet<ulong> whitelistedPlayers = new HashSet<ulong>();
        private HashSet<BasePlayer> playersWithUIOpen = new HashSet<BasePlayer>();
        private const string permissionUse = "whitelistmanager.use";
        private const string permissionWhitelisted = "whitelistmanager.whitelisted";
        private const string importFileName = "whitelist_import";
        private Configuration config;
        private string cmd_whitelist;
        private string currentPage = "MAIN";
        private string searchQuery = "";
        private int currentListPage = 0;
        private const int ITEMS_PER_PAGE = 10;
        private string feedbackMessage = "";
        private Dictionary<ulong, string> playerNames = new Dictionary<ulong, string>();
        private Timer backupTimer;
        private const string BACKUP_DATE_FORMAT = "yyyy-MM-dd_HH-mm-ss";
        private int currentBackupPage = 0;
        private string tempBackupName = "";

        class Configuration
        {
            [JsonProperty("Auto Whitelist Admins")]
            public bool AutoWhitelistAdmins = true;

            [JsonProperty("Auto Whitelist Group")]
            public bool AutoWhitelistGroup = false;

            [JsonProperty("Auto Whitelisted Group Name")]
            public string WhitelistedGroupName = "whitelisted";

            [JsonProperty("Not Whitelisted Message")]
            public string NotWhitelistedMessage = "You are not whitelisted on this server";

            [JsonProperty("Use Steam API")]
            public bool UseSteamAPI = false;

            [JsonProperty("Steam API Key")]
            public string SteamAPIKey = "";

            [JsonProperty("Discord Integration")]
            public DiscordSettings Discord = new DiscordSettings();

            public class DiscordSettings
            {
                [JsonProperty("Use Discord Integration")]
                public bool UseDiscordIntegration = false;

                [JsonProperty("Discord Webhook URL")]
                public string WebhookUrl = "";

                [JsonProperty("Notify Automatic Backups")]
                public bool NotifyAutomaticBackups = true;
            }

            [JsonProperty("Chat Commands")]
            public Dictionary<string, string> ChatCommands = new Dictionary<string, string>
            {
                {"whitelist", "Opens whitelist UI"},
                {"/whitelist backup", "Create a named backup"},
                {"/whitelist restore", "Restore a backup"}
            };

            [JsonProperty("RCON Commands")]
            public Dictionary<string, string> RconCommands = new Dictionary<string, string>
            {
                {"wl.add", "Add player to whitelist"},
                {"wl.remove", "Remove player from whitelist"},
                {"wl.import", "Import steamIDs from file"},
                {"wl.backup", "Create a named backup"},
                {"wl.restore", "Restore a backup"}
            };

            [JsonProperty("Enable Whitelist Backup")]
            public bool EnableBackup = true;

            [JsonProperty("Backup Interval (Seconds)")]
            public int BackupIntervalSeconds = 3600;

            [JsonProperty("Maximum Backup Files")]
            public int MaxBackupFiles = 5;

            [JsonProperty("Backup Directory")]
            public string BackupDirectory = "whitelist_backups";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new JsonException();
                Puts("[Config] Configuration file loaded successfully");
            }
            catch
            {
                PrintWarning("[Config] Configuration file is corrupt, creating new one with default values");
                LoadDefaultConfig();
            }
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

        private void SendWelcomeMessage()
        {
            var message = new System.Text.StringBuilder();
            message.AppendLine("**🎮 Whitelist Manager Discord Integration Enabled! 🎮**");
            message.AppendLine("\nThis channel will receive notifications for the following actions:");
            message.AppendLine("• When a player is added to the whitelist");
            message.AppendLine("• When a player is removed from the whitelist");
            message.AppendLine("• When a whitelist import is performed");
            message.AppendLine("• When a backup is created or restored");
            message.AppendLine("\n*Note: This is a one-way notification system. Commands cannot be executed through Discord.*");
            
            SendDiscordMessage(message.ToString());
        }

        private void SendDiscordMessage(string message)
        {
            if (!config.Discord.UseDiscordIntegration || string.IsNullOrEmpty(config.Discord.WebhookUrl))
                return;

            var payload = new Dictionary<string, string>
            {
                ["content"] = message
            };

            webrequest.Enqueue(config.Discord.WebhookUrl, JsonConvert.SerializeObject(payload), 
                (code, response) => {
                    if (code != 200 && code != 204)
                    {
                        PrintWarning($"Failed to send Discord message. Code: {code}, Response: {response}");
                    }
                }, this, RequestMethod.POST, new Dictionary<string, string>
                {
                    ["Content-Type"] = "application/json"
                });
        }

        void Init()
        {
            permission.RegisterPermission(permissionUse, this);
            permission.RegisterPermission(permissionWhitelisted, this);
            
            foreach (var cmd in config.ChatCommands.Keys)
            {
                cmd_whitelist = cmd;
                AddCovalenceCommand(cmd, nameof(WhitelistCommand));
            }
            
            try {
                LoadWhitelist();
                Puts("[Data] Whitelist data file loaded successfully");
            }
            catch (Exception ex) {
                PrintWarning($"[Data] Failed to load whitelist data: {ex.Message}");
            }

            if (config.Discord.UseDiscordIntegration && !string.IsNullOrEmpty(config.Discord.WebhookUrl))
            {
                timer.Once(5f, () => SendWelcomeMessage());
            }

            string importFilePath = Path.Combine(Interface.Oxide.DataDirectory, "whitelist_import_example.json");
            if (!File.Exists(importFilePath))
            {
                try {
                    var jsonContent = @"
{
    ""steamids"": [
        ""76561198012345678"",
        ""76561198087654321"",
        ""76561198011223344"",
        ""76561198055667788"",
        ""76561198099887766""
    ]
}";
                    File.WriteAllText(importFilePath, jsonContent);
                    Puts($"[Files] Created example whitelist_import_example.json file in oxide/data directory");
                }
                catch (Exception ex) {
                    PrintWarning($"[Files] Failed to create example import file: {ex.Message}");
                }
            }

            InitializeBackup();
            
            if (config.EnableBackup)
            {
                string backupPath = Path.Combine(Interface.Oxide.DataDirectory, config.BackupDirectory);
                if (!Directory.Exists(backupPath) || !Directory.GetFiles(backupPath, "whitelist_backup_*.json").Any())
                {
                    Puts("[Backup] No existing backups found, creating initial backup...");
                    CreateBackup();
                }
            }

            Puts("[Startup] WhitelistManager initialized successfully");
        }

        void LoadWhitelist()
        {
            try
            {
                var data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, object>>("WhitelistManager");
                whitelistedPlayers = new HashSet<ulong>();
                playerNames = new Dictionary<ulong, string>();
                
                if (data != null && data.ContainsKey("players"))
                {
                    var players = JsonConvert.DeserializeObject<Dictionary<ulong, string>>(data["players"].ToString());
                    foreach (var pair in players)
                    {
                        whitelistedPlayers.Add(pair.Key);
                        playerNames[pair.Key] = pair.Value;
                    }
                }
            }
            catch
            {
                try
                {
                    var oldData = Interface.Oxide.DataFileSystem.ReadObject<HashSet<ulong>>("WhitelistManager");
                    if (oldData != null)
                    {
                        whitelistedPlayers = oldData;
                        playerNames = new Dictionary<ulong, string>();
                        
                        foreach (var steamId in whitelistedPlayers)
                        {
                            playerNames[steamId] = "Unknown";
                        }
                        
                        SaveWhitelist();
                    }
                }
                catch (Exception ex)
                {
                    whitelistedPlayers = new HashSet<ulong>();
                    playerNames = new Dictionary<ulong, string>();
                    PrintWarning($"[Data] Failed to load whitelist data: {ex.Message}");
                }
            }
        }

        void SaveWhitelist()
        {
            var data = new Dictionary<string, object>
            {
                ["players"] = playerNames
            };
            
            Interface.Oxide.DataFileSystem.WriteObject("WhitelistManager", data);
            
            var players = playersWithUIOpen.ToList();
            foreach (var player in players)
            {
                if (player != null && !player.IsDestroyed)
                {
                    if (currentPage == "MAIN")
                        ShowMainUI(player);
                    else
                        ShowListUI(player);
                }
                else
                {
                    playersWithUIOpen.Remove(player);
                }
            }
        }

        private bool IsValidSteamId(string steamId)
        {
            return steamId.Length == 17 && steamId.All(char.IsDigit);
        }

        private void ImportSteamIDs(bool isRcon, IPlayer player = null)
        {
            string filePath = Path.Combine(Interface.Oxide.DataDirectory, $"{importFileName}.json");
            if (!File.Exists(filePath))
            {
                string msg = $"Import file not found at 'oxide/data/{importFileName}.json'";
                if (isRcon)
                    Puts(msg);
                else
                    feedbackMessage = msg;
                return;
            }

            try
            {
                string jsonContent = File.ReadAllText(filePath);
                var importData = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(jsonContent);

                if (!importData.ContainsKey("steamids"))
                {
                    string msg = $"Import file '{importFileName}.json' is missing 'steamids' array";
                    if (isRcon)
                        Puts(msg);
                    else
                        feedbackMessage = msg;
                    return;
                }

                int addedCount = 0;
                List<int> invalidLines = new List<int>();
                List<int> duplicateLines = new List<int>();
                var steamIds = importData["steamids"];
                List<string> results = new List<string>();
                
                for (int i = 0; i < steamIds.Count; i++)
                {
                    string steamId = steamIds[i];
                    if (!IsValidSteamId(steamId))
                    {
                        results.Add($"{steamId} - Invalid SteamID format (Line: {i + 1})");
                        invalidLines.Add(i + 1);
                        continue;
                    }

                    ulong steamIdUlong = ulong.Parse(steamId);
                    if (whitelistedPlayers.Contains(steamIdUlong))
                    {
                        results.Add($"{steamId} - Already whitelisted (Line: {i + 1})");
                        duplicateLines.Add(i + 1);
                        continue;
                    }

                    whitelistedPlayers.Add(steamIdUlong);
                    playerNames[steamIdUlong] = "Unknown";
                    UpdatePlayerName(steamIdUlong);
                    results.Add($"{steamId} - Successfully added");
                    addedCount++;
                }

                string summary = $"Import Summary:\n" +
                               $"• {addedCount} SteamIDs added successfully\n" +
                               $"• {invalidLines.Count} Invalid SteamIDs (Line: {string.Join(",", invalidLines)})\n" +
                               $"• {duplicateLines.Count} Already whitelisted (Line: {string.Join(",", duplicateLines)})";

                string executedBy = isRcon ? "RCON" : player?.Name ?? "Unknown";
                SendDiscordMessage($"**Whitelist Import Completed**\n{summary}\n• Executed by: {executedBy}");

                if (isRcon)
                {
                    Puts(string.Join("\n", results));
                    Puts("\n" + summary);
                }
                else
                    feedbackMessage = summary;

                if (addedCount > 0)
                {
                    SaveWhitelist();
                }
            }
            catch (Exception ex)
            {
                string msg = $"Failed to import whitelist from '{importFileName}.json': {ex.Message}";
                if (isRcon)
                    Puts(msg);
                else
                    feedbackMessage = msg;
            }
        }

        object CanUserLogin(string name, string id, string ipAddress)
        {
            var steamId = ulong.Parse(id);
            var player = covalence.Players.FindPlayer(id);
            
            if (player != null)
            {
                if (config.AutoWhitelistAdmins && player.HasPermission(permissionUse))
                {
                    if (!whitelistedPlayers.Contains(steamId))
                    {
                        whitelistedPlayers.Add(steamId);
                        SaveWhitelist();
                    }
                    return null;
                }

                if (player.HasPermission(permissionWhitelisted))
                {
                    return null;
                }
            }

            if (config.AutoWhitelistGroup && permission.UserHasGroup(id, config.WhitelistedGroupName))
            {
                return null;
            }

            if (!whitelistedPlayers.Contains(steamId))
            {
                return config.NotWhitelistedMessage;
            }
            
            return null;
        }

        [Command("whitelist")]
        private void WhitelistCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permissionUse))
            {
                player.Reply("You don't have permission to use this command");
                return;
            }

            if (args.Length == 0)
            {
                var basePlayer = player.Object as BasePlayer;
                if (basePlayer != null)
                {
                    currentPage = "MAIN";
                    ShowMainUI(basePlayer);
                    basePlayer.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                    basePlayer.SendNetworkUpdateImmediate();
                }
                return;
            }

            if (args[0] == "backup" && args.Length == 2)
            {
                CreateNamedBackup(args[1], false, player);
                return;
            }

            if (args[0] == "restore" && args.Length == 2)
            {
                RestoreBackup(args[1], false, player.Object as BasePlayer);
                return;
            }

            player.Reply("Available commands:\n/whitelist - Open UI\n/whitelist backup <name> - Create backup\n/whitelist restore <filename> - Restore backup");
        }

        private void ShowMainUI(BasePlayer player)
        {
            if (!playersWithUIOpen.Contains(player))
            {
                CuiHelper.DestroyUi(player, "WhitelistUI");
                CuiHelper.DestroyUi(player, "WhitelistCursor");
                
                playersWithUIOpen.Add(player);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                player.SendNetworkUpdateImmediate();
                
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image = { Color = "0 0 0 0" },
                    CursorEnabled = true
                }, "Overlay", "WhitelistCursor");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400 -250", OffsetMax = "400 250" },
                    Image = { Color = "0.1 0.1 0.1 0.98" }
                }, "WhitelistCursor", "WhitelistUI");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0.9", AnchorMax = "1 1" },
                    Image = { Color = "0.8 0.8 0.8 0.2" }
                }, "WhitelistUI", "Header");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text = { Text = "WHITELIST MANAGER V1.2", FontSize = 20, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
                }, "Header");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.92 0.92", AnchorMax = "0.98 0.98" },
                    Button = { Command = "whitelistui.close", Color = "0.8 0.2 0.2 0.8" },
                    Text = { Text = "✕", FontSize = 20, Align = TextAnchor.MiddleCenter }
                }, "WhitelistUI");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.82 0.92", AnchorMax = "0.90 0.98" },
                    Button = { Command = "whitelistui.showbackups", Color = "0.2 0.6 0.8 0.8" },
                    Text = { Text = "BACKUPS", FontSize = 16, Align = TextAnchor.MiddleCenter }
                }, "WhitelistUI");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.72 0.92", AnchorMax = "0.80 0.98" },
                    Button = { Command = "whitelistui.showlist", Color = "0.2 0.6 0.8 0.8" },
                    Text = { Text = "LIST", FontSize = 16, Align = TextAnchor.MiddleCenter }
                }, "WhitelistUI");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.1 0.8", AnchorMax = "0.5 0.88" },
                    Image = { Color = "0.15 0.15 0.15 0.8" }
                }, "WhitelistUI", "InputBox");

                container.Add(new CuiElement
                {
                    Parent = "InputBox",
                    Components = {
                        new CuiInputFieldComponent
                        {
                            Command = "whitelistui.setinput",
                            FontSize = 14,
                            CharsLimit = 20,
                            Align = TextAnchor.MiddleLeft,
                            Text = inputSteamId
                        },
                        new CuiRectTransformComponent { AnchorMin = "0.02 0", AnchorMax = "0.98 1" }
                    }
                });

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.52 0.8", AnchorMax = "0.7 0.88" },
                    Image = { Color = "0.2 0.8 0.2 0.8" }
                }, "WhitelistUI", "AddBox");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = "whitelistui.add", Color = "0 0 0 0" },
                    Text = { Text = "ADD", FontSize = 14, Align = TextAnchor.MiddleCenter }
                }, "AddBox");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.72 0.8", AnchorMax = "0.9 0.88" },
                    Image = { Color = "0.2 0.6 0.8 0.8" }
                }, "WhitelistUI", "ImportBox");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = "whitelistui.import", Color = "0 0 0 0" },
                    Text = { Text = "IMPORT", FontSize = 14, Align = TextAnchor.MiddleCenter }
                }, "ImportBox");

                if (!string.IsNullOrEmpty(feedbackMessage))
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.1 0.2", AnchorMax = "0.9 0.78" },
                        Text = { Text = feedbackMessage, FontSize = 14, Align = TextAnchor.UpperLeft }
                    }, "WhitelistUI");
                }

                CuiHelper.AddUi(player, container);
            }
            else
            {
                CuiHelper.DestroyUi(player, "WhitelistUI");
                
                CuiElementContainer container = new CuiElementContainer();
                
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400 -250", OffsetMax = "400 250" },
                    Image = { Color = "0.1 0.1 0.1 0.98" }
                }, "WhitelistCursor", "WhitelistUI");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0.9", AnchorMax = "1 1" },
                    Image = { Color = "0.8 0.8 0.8 0.2" }
                }, "WhitelistUI", "Header");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text = { Text = "WHITELIST MANAGER V1.2", FontSize = 20, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
                }, "Header");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.92 0.92", AnchorMax = "0.98 0.98" },
                    Button = { Command = "whitelistui.close", Color = "0.8 0.2 0.2 0.8" },
                    Text = { Text = "✕", FontSize = 20, Align = TextAnchor.MiddleCenter }
                }, "WhitelistUI");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.82 0.92", AnchorMax = "0.90 0.98" },
                    Button = { Command = "whitelistui.showbackups", Color = "0.2 0.6 0.8 0.8" },
                    Text = { Text = "BACKUPS", FontSize = 16, Align = TextAnchor.MiddleCenter }
                }, "WhitelistUI");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.72 0.92", AnchorMax = "0.80 0.98" },
                    Button = { Command = "whitelistui.showlist", Color = "0.2 0.6 0.8 0.8" },
                    Text = { Text = "LIST", FontSize = 16, Align = TextAnchor.MiddleCenter }
                }, "WhitelistUI");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.1 0.8", AnchorMax = "0.5 0.88" },
                    Image = { Color = "0.15 0.15 0.15 0.8" }
                }, "WhitelistUI", "InputBox");

                container.Add(new CuiElement
                {
                    Parent = "InputBox",
                    Components = {
                        new CuiInputFieldComponent
                        {
                            Command = "whitelistui.setinput",
                            FontSize = 14,
                            CharsLimit = 20,
                            Align = TextAnchor.MiddleLeft,
                            Text = inputSteamId
                        },
                        new CuiRectTransformComponent { AnchorMin = "0.02 0", AnchorMax = "0.98 1" }
                    }
                });

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.52 0.8", AnchorMax = "0.7 0.88" },
                    Image = { Color = "0.2 0.8 0.2 0.8" }
                }, "WhitelistUI", "AddBox");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = "whitelistui.add", Color = "0 0 0 0" },
                    Text = { Text = "ADD", FontSize = 14, Align = TextAnchor.MiddleCenter }
                }, "AddBox");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.72 0.8", AnchorMax = "0.9 0.88" },
                    Image = { Color = "0.2 0.6 0.8 0.8" }
                }, "WhitelistUI", "ImportBox");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = "whitelistui.import", Color = "0 0 0 0" },
                    Text = { Text = "IMPORT", FontSize = 14, Align = TextAnchor.MiddleCenter }
                }, "ImportBox");

                if (!string.IsNullOrEmpty(feedbackMessage))
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.1 0.2", AnchorMax = "0.9 0.78" },
                        Text = { Text = feedbackMessage, FontSize = 14, Align = TextAnchor.UpperLeft }
                    }, "WhitelistUI");
                }

                CuiHelper.AddUi(player, container);
            }
        }

        private void ShowListUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "WhitelistUI");
            CuiHelper.DestroyUi(player, "WhitelistCursor");
            
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0" },
                CursorEnabled = true
            }, "Overlay", "WhitelistCursor");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400 -250", OffsetMax = "400 250" },
                Image = { Color = "0.1 0.1 0.1 0.98" }
            }, "WhitelistCursor", "WhitelistUI");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.9", AnchorMax = "1 1" },
                Image = { Color = "0.8 0.8 0.8 0.2" }
            }, "WhitelistUI", "Header");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = "WHITELIST LIST", FontSize = 20, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
            }, "Header");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.92 0.92", AnchorMax = "0.98 0.98" },
                Button = { Command = "whitelistui.close", Color = "0.8 0.2 0.2 0.8" },
                Text = { Text = "✕", FontSize = 20, Align = TextAnchor.MiddleCenter }
            }, "WhitelistUI");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.82 0.92", AnchorMax = "0.90 0.98" },
                Button = { Command = "whitelistui.showmain", Color = "0.2 0.6 0.8 0.8" },
                Text = { Text = "BACK", FontSize = 16, Align = TextAnchor.MiddleCenter }
            }, "WhitelistUI");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.1 0.8", AnchorMax = "0.7 0.88" },
                Image = { Color = "0.15 0.15 0.15 0.8" }
            }, "WhitelistUI", "SearchBox");

            container.Add(new CuiElement
            {
                Parent = "SearchBox",
                Components = {
                    new CuiInputFieldComponent
                    {
                        Command = "whitelistui.setsearch",
                        FontSize = 14,
                        CharsLimit = 20,
                        Align = TextAnchor.MiddleLeft,
                        Text = searchQuery,
                        NeedsKeyboard = true
                    },
                    new CuiRectTransformComponent { AnchorMin = "0.02 0", AnchorMax = "0.98 1" }
                }
            });

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.72 0.8", AnchorMax = "0.9 0.88" },
                Image = { Color = "0.2 0.6 0.8 0.8" }
            }, "WhitelistUI", "SearchButtonBox");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Command = "whitelistui.search", Color = "0 0 0 0" },
                Text = { Text = "SEARCH", FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, "SearchButtonBox");

            var filteredList = whitelistedPlayers.Where(steamId => 
            {
                if (string.IsNullOrEmpty(searchQuery)) return true;
                var playerInfo = covalence.Players.FindPlayerById(steamId.ToString());
                return steamId.ToString().Contains(searchQuery) || 
                       (playerInfo?.Name?.ToLower().Contains(searchQuery.ToLower()) ?? false);
            }).ToList();

            int totalPages = (int)Math.Ceiling(filteredList.Count / (float)ITEMS_PER_PAGE);
            var pageItems = filteredList.Skip(currentListPage * ITEMS_PER_PAGE).Take(ITEMS_PER_PAGE);

            float startY = 0.70f;
            float itemHeight = 0.05f;
            float spacing = 0.01f;

            foreach (var steamId in pageItems)
            {
                string panelName = $"Entry_{steamId}";
                float currentY = startY - (itemHeight + spacing) * (pageItems.ToList().IndexOf(steamId));

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"0.1 {currentY}", AnchorMax = $"0.9 {currentY + itemHeight}" },
                    Image = { Color = "0.2 0.2 0.2 0.8" }
                }, "WhitelistUI", panelName);

                string playerName = GetPlayerName(steamId);
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.02 0", AnchorMax = "0.7 1" },
                    Text = { Text = $"{playerName} ({steamId})", FontSize = 14, Align = TextAnchor.MiddleLeft }
                }, panelName);

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.75 0.2", AnchorMax = "0.95 0.8" },
                    Button = { Command = $"whitelistui.remove {steamId}", Color = "0.8 0.2 0.2 0.8" },
                    Text = { Text = "REMOVE", FontSize = 12, Align = TextAnchor.MiddleCenter }
                }, panelName);
            }

            CuiHelper.AddUi(player, container);
        }

        private string inputSteamId = string.Empty;

        [ConsoleCommand("whitelistui.setinput")]
        private void SetInputCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            inputSteamId = arg.GetString(0);
        }

        [ConsoleCommand("whitelistui.setsearch")]
        private void SetSearchCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            searchQuery = arg.GetString(0, "");
        }

        [ConsoleCommand("whitelistui.search")]
        private void SearchCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            ShowListUI(player);
        }

        [ConsoleCommand("whitelistui.prevpage")]
        private void PrevPageCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (currentListPage > 0)
            {
                currentListPage--;
                ShowListUI(player);
            }
        }

        [ConsoleCommand("whitelistui.nextpage")]
        private void NextPageCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            currentListPage++;
            ShowListUI(player);
        }

        [ConsoleCommand("whitelistui.showlist")]
        private void ShowListCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            currentPage = "LIST";
            searchQuery = "";
            currentListPage = 0;
            ShowListUI(player);
        }

        [ConsoleCommand("whitelistui.showmain")]
        private void ShowMainCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            currentPage = "MAIN";
            ShowMainUI(player);
        }

        [ConsoleCommand("whitelistui.add")]
        private void AddCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            if (!permission.UserHasPermission(player.UserIDString, permissionUse))
                return;

            if (!IsValidSteamId(inputSteamId))
            {
                feedbackMessage = "Invalid SteamID";
                ShowMainUI(player);
                return;
            }

            ulong steamId = ulong.Parse(inputSteamId);
            if (whitelistedPlayers.Contains(steamId))
            {
                feedbackMessage = "This SteamID is already whitelisted";
                ShowMainUI(player);
                return;
            }

            whitelistedPlayers.Add(steamId);
            playerNames[steamId] = "Unknown";
            UpdatePlayerName(steamId);
            SaveWhitelist();
            
            var playerName = GetPlayerName(steamId);
            SendDiscordMessage($"**Player Added to Whitelist**\n• Player: {playerName}\n• SteamID: {steamId}\n• Added by: {player.displayName}");
            
            feedbackMessage = $"Added {steamId} to whitelist";
            ShowMainUI(player);
        }

        [ConsoleCommand("whitelistui.import")]
        private void ImportUICommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            if (!permission.UserHasPermission(player.UserIDString, permissionUse))
                return;

            ImportSteamIDs(false, player.IPlayer);
            ShowMainUI(player);
        }

        private void DestroyAllUI(BasePlayer player)
        {
            if (player == null) return;
            
            CuiHelper.DestroyUi(player, "WhitelistUI");
            
            if (playersWithUIOpen.Contains(player))
            {
                CuiHelper.DestroyUi(player, "WhitelistCursor");
                playersWithUIOpen.Remove(player);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                player.SendNetworkUpdateImmediate();
            }
        }

        [ConsoleCommand("whitelistui.close")]
        private void CloseUICommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            DestroyAllUI(player);
        }

        [ConsoleCommand("whitelistui.remove")]
        private void RemoveCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            if (!permission.UserHasPermission(player.UserIDString, permissionUse))
                return;

            ulong steamId = arg.GetULong(0);
            string playerName = GetPlayerName(steamId);
            
            if (whitelistedPlayers.Remove(steamId))
            {
                playerNames.Remove(steamId);
                SaveWhitelist();
                
                SendDiscordMessage($"**Player Removed from Whitelist**\n• Player: {playerName}\n• SteamID: {steamId}\n• Removed by: {player.displayName}");
                
                ShowListUI(player);
            }
        }

        [ConsoleCommand("wl.add")]
        private void RconWhitelistAdd(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length != 1)
            {
                Puts("Usage: wl.add <steamid>");
                return;
            }

            if (!IsValidSteamId(arg.Args[0]))
            {
                Puts("Invalid SteamID");
                return;
            }

            ulong steamId = ulong.Parse(arg.Args[0]);
            if (whitelistedPlayers.Contains(steamId))
            {
                Puts("This SteamID is already whitelisted");
                return;
            }

            whitelistedPlayers.Add(steamId);
            playerNames[steamId] = "Unknown";
            UpdatePlayerName(steamId);
            SaveWhitelist();

            var playerName = GetPlayerName(steamId);
            SendDiscordMessage($"**Player Added to Whitelist**\n• Player: {playerName}\n• SteamID: {steamId}\n• Added by: RCON");

            Puts($"SteamID {steamId} has been added to the whitelist");
        }

        [ConsoleCommand("wl.remove")]
        private void RconWhitelistRemove(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length != 1)
            {
                Puts("Usage: wl.remove <steamid>");
                return;
            }

            if (!IsValidSteamId(arg.Args[0]))
            {
                Puts("Invalid SteamID");
                return;
            }

            ulong steamId = ulong.Parse(arg.Args[0]);
            string playerName = GetPlayerName(steamId);
            if (whitelistedPlayers.Remove(steamId))
            {
                playerNames.Remove(steamId);
                SaveWhitelist();

                SendDiscordMessage($"**Player Removed from Whitelist**\n• Player: {playerName}\n• SteamID: {steamId}\n• Removed by: RCON");

                Puts($"SteamID {steamId} has been removed from the whitelist");
            }
            else
            {
                Puts($"SteamID {steamId} was not found in the whitelist");
            }
        }

        [ConsoleCommand("wl.list")]
        private void RconWhitelistList(ConsoleSystem.Arg arg)
        {
            var list = new List<string>();
            foreach (var steamId in whitelistedPlayers)
            {
                var player = covalence.Players.FindPlayerById(steamId.ToString());
                string playerName = player?.Name ?? "Sconosciuto";
                list.Add($"{steamId} - {playerName}");
            }

            if (list.Count == 0)
            {
                Puts("Nessun giocatore nella whitelist");
                return;
            }

            Puts(string.Join("\n", list));
        }

        [ConsoleCommand("wl.import")]
        private void RconImportCommand(ConsoleSystem.Arg arg)
        {
            ImportSteamIDs(true);
        }

        private string GetPlayerNameFromSteamID(ulong steamId)
        {
            if (!config.UseSteamAPI || string.IsNullOrEmpty(config.SteamAPIKey))
                return "Unknown";

            try
            {
                string url = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={config.SteamAPIKey}&steamids={steamId}";
                webrequest.EnqueueGet(url, (code, response) =>
                {
                    if (code != 200 || string.IsNullOrEmpty(response))
                        return;
                    
                    var data = JsonConvert.DeserializeObject<SteamApiResponse>(response);
                    if (data?.Response?.Players?.Count > 0)
                    {
                        string playerName = data.Response.Players[0].PersonaName;
                        var players = playersWithUIOpen.ToList();
                        foreach (var player in players)
                        {
                            if (player != null && !player.IsDestroyed)
                            {
                                if (currentPage == "LIST")
                                    ShowListUI(player);
                            }
                        }
                    }
                }, this);
            }
            catch
            {
                return "Unknown";
            }
            return "Loading...";
        }

        private class SteamApiResponse
        {
            public ResponseData Response { get; set; }
        }

        private class ResponseData
        {
            public List<PlayerData> Players { get; set; }
        }

        private class PlayerData
        {
            [JsonProperty("personaname")]
            public string PersonaName { get; set; }
        }

        private void UpdatePlayerName(ulong steamId)
        {
            if (!config.UseSteamAPI || string.IsNullOrEmpty(config.SteamAPIKey))
                return;

            try
            {
                string url = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={config.SteamAPIKey}&steamids={steamId}";
                webrequest.EnqueueGet(url, (code, response) =>
                {
                    if (code != 200 || string.IsNullOrEmpty(response))
                        return;
                    
                    var data = JsonConvert.DeserializeObject<SteamApiResponse>(response);
                    if (data?.Response?.Players?.Count > 0)
                    {
                        string playerName = data.Response.Players[0].PersonaName;
                        playerNames[steamId] = playerName;
                        SaveWhitelist();
                    }
                }, this);
            }
            catch
            {
            }
        }

        private string GetPlayerName(ulong steamId)
        {
            var playerInfo = covalence.Players.FindPlayerById(steamId.ToString());
            if (playerInfo != null && !string.IsNullOrEmpty(playerInfo.Name))
            {
                if (playerNames.ContainsKey(steamId) && playerNames[steamId] != playerInfo.Name)
                {
                    playerNames[steamId] = playerInfo.Name;
                    SaveWhitelist();
                }
                return playerInfo.Name;
            }

            return playerNames.ContainsKey(steamId) ? playerNames[steamId] : "Unknown";
        }

        private void InitializeBackup()
        {
            if (!config.EnableBackup) return;

            string backupPath = Path.Combine(Interface.Oxide.DataDirectory, config.BackupDirectory);
            if (!Directory.Exists(backupPath))
            {
                Directory.CreateDirectory(backupPath);
            }

            backupTimer?.Destroy();
            backupTimer = timer.Every(config.BackupIntervalSeconds, CreateBackup);
        }

        private void CreateBackup()
        {
            if (!config.EnableBackup) return;

            string backupPath = Path.Combine(Interface.Oxide.DataDirectory, config.BackupDirectory);
            if (!Directory.Exists(backupPath))
            {
                Directory.CreateDirectory(backupPath);
            }

            string timestamp = DateTime.Now.ToString(BACKUP_DATE_FORMAT);
            string backupFile = Path.Combine(backupPath, $"whitelist_backup_auto_{timestamp}.json");

            var backupData = new Dictionary<string, object>
            {
                ["players"] = playerNames,
                ["timestamp"] = DateTime.Now.ToString("O"),
                ["playerCount"] = whitelistedPlayers.Count,
                ["type"] = "auto"
            };

            File.WriteAllText(backupFile, JsonConvert.SerializeObject(backupData, Formatting.Indented));

            if (config.Discord.NotifyAutomaticBackups)
            {
                SendDiscordMessage($"**Automatic Backup Created**\n• Filename: whitelist_backup_auto_{timestamp}.json\n• Players: {whitelistedPlayers.Count}");
            }

            var autoBackupFiles = Directory.GetFiles(backupPath, "whitelist_backup_auto_*.json")
                                       .OrderByDescending(f => f)
                                       .ToList();

            if (autoBackupFiles.Count > config.MaxBackupFiles)
            {
                for (int i = config.MaxBackupFiles; i < autoBackupFiles.Count; i++)
                {
                    try
                    {
                        File.Delete(autoBackupFiles[i]);
                    }
                    catch (Exception ex)
                    {
                        PrintWarning($"Failed to delete old backup {autoBackupFiles[i]}: {ex.Message}");
                    }
                }
            }
        }

        [ConsoleCommand("wl.backup")]
        private void RconBackupCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length != 1)
            {
                Puts("Usage: wl.backup <backup_name>");
                return;
            }

            CreateNamedBackup(arg.GetString(0), true);
        }

        private void CreateNamedBackup(string backupName, bool isRcon, IPlayer player = null)
        {
            if (!config.EnableBackup)
            {
                string msg = "Backup system is disabled in configuration";
                if (isRcon)
                    Puts(msg);
                else
                    player.Reply(msg);
                return;
            }

            try
            {
                string backupPath = Path.Combine(Interface.Oxide.DataDirectory, config.BackupDirectory);
                if (!Directory.Exists(backupPath))
                {
                    Directory.CreateDirectory(backupPath);
                }

                string timestamp = DateTime.Now.ToString(BACKUP_DATE_FORMAT);
                string backupFile = Path.Combine(backupPath, $"whitelist_backup_manual_{backupName}_{timestamp}.json");

                var backupData = new Dictionary<string, object>
                {
                    ["players"] = playerNames,
                    ["timestamp"] = DateTime.Now.ToString("O"),
                    ["playerCount"] = whitelistedPlayers.Count,
                    ["name"] = backupName,
                    ["type"] = "manual"
                };

                File.WriteAllText(backupFile, JsonConvert.SerializeObject(backupData, Formatting.Indented));

                string createdBy = isRcon ? "RCON" : (player?.Name ?? "Unknown");
                SendDiscordMessage($"**Manual Backup Created**\n• Name: {backupName}\n• Filename: whitelist_backup_manual_{backupName}_{timestamp}.json\n• Players: {whitelistedPlayers.Count}\n• Created by: {createdBy}");

                string successMsg = $"Created backup: {Path.GetFileName(backupFile)}";
                if (isRcon)
                    Puts(successMsg);
                else
                    player.Reply(successMsg);
            }
            catch (Exception ex)
            {
                string errorMsg = $"Failed to create backup: {ex.Message}";
                if (isRcon)
                    PrintWarning(errorMsg);
                else
                    player.Reply(errorMsg);
            }
        }

        [ConsoleCommand("wl.listbackups")]
        private void RconListBackupsCommand(ConsoleSystem.Arg arg)
        {
            if (!config.EnableBackup)
            {
                Puts("Backup system is disabled in configuration");
                return;
            }

            string backupPath = Path.Combine(Interface.Oxide.DataDirectory, config.BackupDirectory);
            if (!Directory.Exists(backupPath))
            {
                Puts("No backups found");
                return;
            }

            var backupFiles = Directory.GetFiles(backupPath, "whitelist_backup_*.json")
                                      .OrderByDescending(f => f)
                                      .ToList();

            if (backupFiles.Count == 0)
            {
                Puts("No backups found");
                return;
            }

            Puts("Available backups:");
            foreach (var file in backupFiles)
            {
                try
                {
                    var content = File.ReadAllText(file);
                    var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(content);
                    Puts($"- {Path.GetFileName(file)} ({data["playerCount"]} players, created: {data["timestamp"]})");
                }
                catch
                {
                    Puts($"- {Path.GetFileName(file)} (corrupted or invalid format)");
                }
            }
        }

        [ConsoleCommand("wl.restore")]
        private void RconRestoreCommand(ConsoleSystem.Arg arg)
        {
            if (!config.EnableBackup)
            {
                Puts("Backup system is disabled in configuration");
                return;
            }

            if (arg.Args == null || arg.Args.Length != 1)
            {
                Puts("Usage: wl.restore <backup_filename>");
                return;
            }

            string backupFileName = arg.GetString(0);
            RestoreBackup(backupFileName, true);
        }

        private void RestoreBackup(string backupFileName, bool isRcon = false, BasePlayer player = null)
        {
            try
            {
                string backupPath = Path.Combine(Interface.Oxide.DataDirectory, config.BackupDirectory);
                string backupFile = Path.Combine(backupPath, backupFileName);

                if (!File.Exists(backupFile))
                {
                    string msg = $"Backup file not found: {backupFileName}";
                    if (isRcon)
                        Puts(msg);
                    else
                        feedbackMessage = msg;
                    return;
                }

                var content = File.ReadAllText(backupFile);
                var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(content);
                
                if (!data.ContainsKey("players"))
                {
                    string msg = "Invalid backup file format";
                    if (isRcon)
                        Puts(msg);
                    else
                        feedbackMessage = msg;
                    return;
                }

                var restoredPlayers = JsonConvert.DeserializeObject<Dictionary<ulong, string>>(data["players"].ToString());
                playerNames = restoredPlayers;
                whitelistedPlayers = new HashSet<ulong>(restoredPlayers.Keys);
                SaveWhitelist();

                string restoredBy = isRcon ? "RCON" : (player?.displayName ?? "Unknown");
                SendDiscordMessage($"**Backup Restored**\n• Filename: {backupFileName}\n• Players: {whitelistedPlayers.Count}\n• Restored by: {restoredBy}");

                string successMsg = $"Successfully restored whitelist from backup: {backupFileName}";
                if (isRcon)
                    Puts(successMsg);
                else
                {
                    feedbackMessage = successMsg;
                    ShowBackupUI(player);
                }
            }
            catch (Exception ex)
            {
                string errorMsg = $"Failed to restore backup: {ex.Message}";
                if (isRcon)
                    Puts(errorMsg);
                else
                    feedbackMessage = errorMsg;
            }
        }

        [ConsoleCommand("whitelistui.showbackups")]
        private void ShowBackupsCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            currentPage = "BACKUP";
            ShowBackupUI(player);
        }

        [ConsoleCommand("whitelistui.confirmrestore")]
        private void ConfirmRestoreCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            CuiHelper.DestroyUi(player, "ConfirmUI");
            
            string backupFile = string.Join(" ", arg.Args);
            RestoreBackup(backupFile, false, player);
            
            ShowSuccessPopup(player, "Backup restored successfully");
        }

        [ConsoleCommand("whitelistui.cancelrestore")]
        private void CancelRestoreCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            CuiHelper.DestroyUi(player, "ConfirmUI");
            ShowBackupUI(player);
        }

        private void ShowSuccessPopup(BasePlayer player, string message)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.3 0.45", AnchorMax = "0.7 0.55" },
                Image = { Color = "0.1 0.8 0.1 0.95" }
            }, "Overlay", "SuccessUI");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = message, FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, "SuccessUI");

            CuiHelper.AddUi(player, container);
            
            timer.Once(2f, () =>
            {
                if (player != null && !player.IsDestroyed)
                {
                    CuiHelper.DestroyUi(player, "SuccessUI");
                }
            });
        }

        [ConsoleCommand("whitelistui.askconfirm")]
        private void AskConfirmCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            string backupFile = string.Join(" ", arg.Args);
            ShowConfirmUI(player, backupFile);
        }

        private void ShowConfirmUI(BasePlayer player, string backupFile)
        {
            CuiHelper.DestroyUi(player, "ConfirmUI");
            
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.3 0.35", AnchorMax = "0.7 0.65" },
                Image = { Color = "0.1 0.1 0.1 0.98" }
            }, "Overlay", "ConfirmUI");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.1 0.6", AnchorMax = "0.9 0.9" },
                Text = { Text = "Are you sure you want to restore this backup?\nThis will overwrite the current whitelist!", FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, "ConfirmUI");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.1 0.2", AnchorMax = "0.45 0.35" },
                Button = { Command = $"whitelistui.confirmrestore {backupFile}", Color = "0.8 0.2 0.2 0.8" },
                Text = { Text = "YES", FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, "ConfirmUI");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.55 0.2", AnchorMax = "0.9 0.35" },
                Button = { Command = "whitelistui.cancelrestore", Color = "0.2 0.6 0.8 0.8" },
                Text = { Text = "NO", FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, "ConfirmUI");

            CuiHelper.AddUi(player, container);
        }

        private void ShowBackupUI(BasePlayer player)
        {
            if (!playersWithUIOpen.Contains(player))
            {
                CuiHelper.DestroyUi(player, "WhitelistUI");
                CuiHelper.DestroyUi(player, "WhitelistCursor");
                
                playersWithUIOpen.Add(player);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                player.SendNetworkUpdateImmediate();
                
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image = { Color = "0 0 0 0" },
                    CursorEnabled = true
                }, "Overlay", "WhitelistCursor");

                CreateBackupUI(container);
                CuiHelper.AddUi(player, container);
            }
            else
            {
                CuiHelper.DestroyUi(player, "WhitelistUI");
                CuiElementContainer container = new CuiElementContainer();
                CreateBackupUI(container);
                CuiHelper.AddUi(player, container);
            }
        }

        private void CreateBackupUI(CuiElementContainer container)
        {
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400 -250", OffsetMax = "400 250" },
                Image = { Color = "0.1 0.1 0.1 0.98" }
            }, "WhitelistCursor", "WhitelistUI");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.9", AnchorMax = "1 1" },
                Image = { Color = "0.8 0.8 0.8 0.2" }
            }, "WhitelistUI", "Header");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = "WHITELIST BACKUPS", FontSize = 20, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
            }, "Header");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.92 0.92", AnchorMax = "0.98 0.98" },
                Button = { Command = "whitelistui.close", Color = "0.8 0.2 0.2 0.8" },
                Text = { Text = "✕", FontSize = 20, Align = TextAnchor.MiddleCenter }
            }, "WhitelistUI");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.82 0.92", AnchorMax = "0.90 0.98" },
                Button = { Command = "whitelistui.showmain", Color = "0.2 0.6 0.8 0.8" },
                Text = { Text = "BACK", FontSize = 16, Align = TextAnchor.MiddleCenter }
            }, "WhitelistUI");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.1 0.85", AnchorMax = "0.9 0.89" },
                Image = { Color = "0.15 0.15 0.15 0.8" }
            }, "WhitelistUI", "ColumnHeaders");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.02 0", AnchorMax = "0.2 1" },
                Text = { Text = "NAME", FontSize = 12, Align = TextAnchor.MiddleLeft }
            }, "ColumnHeaders");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.22 0", AnchorMax = "0.45 1" },
                Text = { Text = "DATE & TIME", FontSize = 12, Align = TextAnchor.MiddleLeft }
            }, "ColumnHeaders");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.47 0", AnchorMax = "0.7 1" },
                Text = { Text = "PLAYERS COUNT", FontSize = 12, Align = TextAnchor.MiddleLeft }
            }, "ColumnHeaders");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.75 0", AnchorMax = "0.95 1" },
                Text = { Text = "ACTIONS", FontSize = 12, Align = TextAnchor.MiddleCenter }
            }, "ColumnHeaders");

            string backupPath = Path.Combine(Interface.Oxide.DataDirectory, config.BackupDirectory);
            if (!Directory.Exists(backupPath))
            {
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.1 0.4", AnchorMax = "0.9 0.6" },
                    Text = { Text = "No backups available", FontSize = 16, Align = TextAnchor.MiddleCenter }
                }, "WhitelistUI");
                return;
            }

            var backupFiles = Directory.GetFiles(backupPath, "whitelist_backup_*.json")
                .Select(f => new
                {
                    FilePath = f,
                    FileInfo = new FileInfo(f)
                })
                .OrderByDescending(f => f.FileInfo.LastWriteTime)
                .Select(f => f.FilePath)
                .ToList();

            if (backupFiles.Count == 0)
            {
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.1 0.4", AnchorMax = "0.9 0.6" },
                    Text = { Text = "No backups available", FontSize = 16, Align = TextAnchor.MiddleCenter }
                }, "WhitelistUI");
                return;
            }

            const int BACKUPS_PER_PAGE = 10;
            int totalPages = (int)Math.Ceiling(backupFiles.Count / (float)BACKUPS_PER_PAGE);
            
            if (currentBackupPage >= totalPages)
                currentBackupPage = totalPages - 1;
            
            var pageBackups = backupFiles
                .Skip(currentBackupPage * BACKUPS_PER_PAGE)
                .Take(BACKUPS_PER_PAGE)
                .ToList();

            float startY = 0.79f;
            float itemHeight = 0.055f;
            float spacing = 0.015f;

            foreach (var file in pageBackups)
            {
                try
                {
                    var content = File.ReadAllText(file);
                    var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(content);
                    string fileName = Path.GetFileName(file);
                    
                    DateTime timestamp = DateTime.Parse(data["timestamp"].ToString());
                    string formattedDate = timestamp.ToString("dd/MM/yyyy HH:mm:ss");
                    
                    float currentY = startY - (itemHeight + spacing) * pageBackups.IndexOf(file);

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"0.1 {currentY}", AnchorMax = $"0.9 {currentY + itemHeight}" },
                        Image = { Color = "0.2 0.2 0.2 0.8" }
                    }, "WhitelistUI", $"Backup_{fileName}");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.02 0", AnchorMax = "0.2 1" },
                        Text = { Text = data.ContainsKey("name") ? data["name"].ToString() : "Auto", FontSize = 12, Align = TextAnchor.MiddleLeft }
                    }, $"Backup_{fileName}");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.22 0", AnchorMax = "0.45 1" },
                        Text = { Text = formattedDate, FontSize = 12, Align = TextAnchor.MiddleLeft }
                    }, $"Backup_{fileName}");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.47 0", AnchorMax = "0.7 1" },
                        Text = { Text = $"Players: {data["playerCount"]}", FontSize = 12, Align = TextAnchor.MiddleLeft }
                    }, $"Backup_{fileName}");

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0.75 0.2", AnchorMax = "0.95 0.8" },
                        Image = { Color = "0 0 0 0" }
                    }, $"Backup_{fileName}", "ButtonContainer");

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "0.48 1" },
                        Button = { Command = $"whitelistui.askconfirm {fileName}", Color = "0.2 0.6 0.8 0.8" },
                        Text = { Text = "RESTORE", FontSize = 12, Align = TextAnchor.MiddleCenter }
                    }, "ButtonContainer");

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.52 0", AnchorMax = "1 1" },
                        Button = { Command = $"whitelistui.askdelete {fileName}", Color = "0.8 0.2 0.2 0.8" },
                        Text = { Text = "DELETE", FontSize = 12, Align = TextAnchor.MiddleCenter }
                    }, "ButtonContainer");
                }
                catch
                {
                }
            }

            if (totalPages > 1)
            {
                if (currentBackupPage > 0)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.1 0.05", AnchorMax = "0.2 0.1" },
                        Button = { Command = "whitelistui.prevbackuppage", Color = "0.2 0.6 0.8 0.8" },
                        Text = { Text = "◄", FontSize = 14, Align = TextAnchor.MiddleCenter }
                    }, "WhitelistUI");
                }

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.4 0.05", AnchorMax = "0.6 0.1" },
                    Text = { Text = $"Page {currentBackupPage + 1}/{totalPages}", FontSize = 14, Align = TextAnchor.MiddleCenter }
                }, "WhitelistUI");

                if (currentBackupPage < totalPages - 1)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.8 0.05", AnchorMax = "0.9 0.1" },
                        Button = { Command = "whitelistui.nextbackuppage", Color = "0.2 0.6 0.8 0.8" },
                        Text = { Text = "►", FontSize = 14, Align = TextAnchor.MiddleCenter }
                    }, "WhitelistUI");
                }
            }

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.72 0.92", AnchorMax = "0.80 0.98" },
                Button = { Command = "whitelistui.createbackup", Color = "0.2 0.8 0.2 0.8" },
                Text = { Text = "CREATE", FontSize = 16, Align = TextAnchor.MiddleCenter }
            }, "WhitelistUI");
        }

        [ConsoleCommand("whitelistui.prevbackuppage")]
        private void PrevBackupPageCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (currentBackupPage > 0)
            {
                currentBackupPage--;
                ShowBackupUI(player);
            }
        }

        [ConsoleCommand("whitelistui.nextbackuppage")]
        private void NextBackupPageCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            currentBackupPage++;
            ShowBackupUI(player);
        }

        [Command("wl.backup")]
        private void BackupCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permissionUse))
            {
                player.Reply("You don't have permission to use this command");
                return;
            }

            if (args.Length != 1)
            {
                player.Reply("Usage: wl.backup <backup_name>");
                return;
            }

            CreateNamedBackup(args[0], false, player);
        }

        [ConsoleCommand("whitelistui.createbackup")]
        private void CreateBackupUICommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            ShowCreateBackupUI(player);
        }

        [ConsoleCommand("whitelistui.confirmbackup")]
        private void ConfirmBackupCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            if (string.IsNullOrEmpty(tempBackupName))
            {
                ShowSuccessPopup(player, "Please enter a backup name");
                return;
            }
            
            CuiHelper.DestroyUi(player, "CreateBackupUI");
            CreateNamedBackup(tempBackupName, false, player.IPlayer);
            tempBackupName = "";
            ShowSuccessPopup(player, "Backup created successfully");
            timer.Once(2f, () => 
            {
                CuiHelper.DestroyUi(player, "SuccessPopup");
                ShowBackupUI(player);
            });
        }

        [ConsoleCommand("whitelistui.cancelbackup")]
        private void CancelBackupCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            CuiHelper.DestroyUi(player, "CreateBackupUI");
        }

        private void ShowCreateBackupUI(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.3 0.35", AnchorMax = "0.7 0.65" },
                Image = { Color = "0.1 0.1 0.1 0.98" }
            }, "Overlay", "CreateBackupUI");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.1 0.6", AnchorMax = "0.9 0.9" },
                Text = { Text = "Enter backup name:", FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, "CreateBackupUI");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.1 0.4", AnchorMax = "0.9 0.55" },
                Image = { Color = "0.2 0.2 0.2 0.8" }
            }, "CreateBackupUI", "BackupNameInput");

            var inputField = new CuiElement
            {
                Parent = "BackupNameInput",
                Components = {
                    new CuiInputFieldComponent
                    {
                        Command = "whitelistui.setbackupname",
                        FontSize = 14,
                        CharsLimit = 30,
                        Align = TextAnchor.MiddleLeft
                    },
                    new CuiRectTransformComponent { AnchorMin = "0.02 0", AnchorMax = "0.98 1" }
                }
            };
            container.Add(inputField);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.1 0.2", AnchorMax = "0.45 0.35" },
                Button = { Command = "whitelistui.confirmbackup", Color = "0.2 0.8 0.2 0.8" },
                Text = { Text = "CONFIRM", FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, "CreateBackupUI");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.55 0.2", AnchorMax = "0.9 0.35" },
                Button = { Command = "whitelistui.cancelbackup", Color = "0.8 0.2 0.2 0.8" },
                Text = { Text = "CANCEL", FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, "CreateBackupUI");

            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("whitelistui.setbackupname")]
        private void SetBackupNameCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            tempBackupName = string.Join(" ", arg.Args);
        }

        [ConsoleCommand("whitelistui.deletebackup")]
        private void DeleteBackupCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            string fileName = string.Join(" ", arg.Args);
            string backupPath = Path.Combine(Interface.Oxide.DataDirectory, config.BackupDirectory, fileName);
            
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
                ShowSuccessPopup(player, "Backup deleted successfully");
                ShowBackupUI(player);
            }
        }

        [ConsoleCommand("whitelistui.askdelete")]
        private void AskDeleteCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            string fileName = string.Join(" ", arg.Args);
            ShowDeleteConfirmUI(player, fileName);
        }

        [ConsoleCommand("whitelistui.confirmdelete")]
        private void ConfirmDeleteCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            string backupFile = arg.GetString(0);
            try
            {
                File.Delete(Path.Combine(Interface.Oxide.DataDirectory, config.BackupDirectory, backupFile));
                feedbackMessage = $"Backup {backupFile} deleted successfully";
            }
            catch (Exception ex)
            {
                feedbackMessage = $"Failed to delete backup: {ex.Message}";
            }

            CuiHelper.DestroyUi(player, "ConfirmUI");
            ShowBackupUI(player);
        }

        [ConsoleCommand("whitelistui.canceldelete")]
        private void CancelDeleteCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            CuiHelper.DestroyUi(player, "ConfirmUI");
            ShowBackupUI(player);
        }

        private void ShowDeleteConfirmUI(BasePlayer player, string fileName)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.3 0.35", AnchorMax = "0.7 0.65" },
                Image = { Color = "0.1 0.1 0.1 0.98" }
            }, "Overlay", "ConfirmUI");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.1 0.6", AnchorMax = "0.9 0.9" },
                Text = { Text = "Are you sure you want to delete this backup?", FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, "ConfirmUI");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.1 0.2", AnchorMax = "0.45 0.35" },
                Button = { Command = $"whitelistui.confirmdelete {fileName}", Color = "0.8 0.2 0.2 0.8" },
                Text = { Text = "DELETE", FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, "ConfirmUI");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.55 0.2", AnchorMax = "0.9 0.35" },
                Button = { Command = "whitelistui.canceldelete", Color = "0.2 0.6 0.8 0.8" },
                Text = { Text = "CANCEL", FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, "ConfirmUI");

            CuiHelper.AddUi(player, container);
        }
    }
}