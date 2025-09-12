using System; 
using System.Text;
using System.Collections.Generic; 
using System.Linq; 
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries;
using Facepunch;
#if CARBON
using Carbon.Core;
using Carbon.Extensions;
#endif

namespace Oxide.Plugins
{
#if CARBON
    [Carbon.Components.Info("APDiscordOnline", "VORON", "0.0.6")]
#else
    [Info("APDiscordOnline", "VORON", "0.0.6")]
#endif
    [Description("Sends online players list to Discord via webhook in multiple languages with customizable embed colors")]
    public class APDiscordOnline : CovalencePlugin
    {
        private string webhookUrl = "https://discord.com/api/webhooks/your_webhook_url";
        private int updateInterval = 300; 
        private string selectedLanguage = "en"; 
        private string staticColor = "16777215"; 
        private bool useRandomColor = false;
        private bool usePlatformSync = false;
        private bool updateLastMessage = true;
        private string lastMessageId = string.Empty;
        private Timer updateTimer;
        private const bool DEBUG_MODE = false;
        private bool enableAAlertRaidIntegration = true;

#if CARBON
        [Carbon.Components.PluginReference]
#else
        [PluginReference]
#endif
        private Plugin PlatformSync;

        
        private class StoredData
        {
            public string LastMessageId { get; set; } = string.Empty;
        }

        private StoredData storedData;

        
        private class RaidStorage
        {
            public string vk;
            public string telegram;
            public ulong discord;
            public bool rustplus;
            public bool ingamerust { get; set; } = true;
        }

        private void SaveData()
        {
            if (storedData != null)
            {
                Interface.Oxide.DataFileSystem.WriteObject(Name + "/discord_data", storedData);
                DebugLog($"Saved message ID: {storedData.LastMessageId}");
            }
        }

        private void LoadData()
        {
            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name + "/discord_data") ?? new StoredData();
                lastMessageId = storedData.LastMessageId;
                DebugLog($"Loaded message ID: {lastMessageId}");
            }
            catch (Exception ex)
            {
                PrintError($"Error loading data: {ex.Message}");
                storedData = new StoredData();
            }
        }

        private readonly Dictionary<string, Dictionary<string, string>> messages = new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string>
            {
                ["NoPlayers"] = "No players online to send.",
                ["PlayerList"] = "**Online Players List:**\n\nNick: Steam64ID: Discord:",
                ["Error"] = "Failed to send message to Discord (Code: {0}): {1}",
                ["PlayerFormat"] = "{0} - {1} - {2}",
                ["NotLinked"] = "Not linked"
            },
            ["ru"] = new Dictionary<string, string>
            {
                ["NoPlayers"] = "Нет игроков онлайн для отправки.",
                ["PlayerList"] = "**Список игроков онлайн:**\n\nНик: Steam64ID: Discord:",
                ["Error"] = "Не удалось отправить сообщение в Discord (Код: {0}): {1}",
                ["PlayerFormat"] = "{0} - {1} - {2}",
                ["NotLinked"] = "Не привязан"
            },
            ["uk"] = new Dictionary<string, string>
            {
                ["NoPlayers"] = "Немає гравців онлайн для відправки.",
                ["PlayerList"] = "**Список гравців онлайн:**\n\nНік: Steam64ID: Discord:",
                ["Error"] = "Не вдалося надіслати повідомлення в Discord (Код: {0}): {1}",
                ["PlayerFormat"] = "{0} - {1} - {2}",
                ["NotLinked"] = "Не прив'язаний"
            },
            ["pl"] = new Dictionary<string, string>
            {
                ["NoPlayers"] = "Brak graczy online do wysłania.",
                ["PlayerList"] = "**Lista graczy online:**\n\nNick: Steam64ID: Discord:",
                ["Error"] = "Nie udało się wysłać wiadomości do Discord (Kod: {0}): {1}",
                ["PlayerFormat"] = "{0} - {1} - {2}",
                ["NotLinked"] = "Nie połączony"
            },
            ["de"] = new Dictionary<string, string>
            {
                ["NoPlayers"] = "Keine Spieler online zum Senden.",
                ["PlayerList"] = "**Liste der Online-Spieler:**\n\nName: Steam64ID: Discord:",
                ["Error"] = "Nachricht konnte nicht an Discord gesendet werden (Code: {0}): {1}",
                ["PlayerFormat"] = "{0} - {1} - {2}",
                ["NotLinked"] = "Nicht verknüpft"
            }
        };

        protected override void LoadDefaultConfig()
        {
            Config["WebhookUrl"] = webhookUrl;
            Config["UpdateInterval"] = updateInterval;
            Config["Language"] = selectedLanguage; 
            Config["EmbedColor"] = staticColor; 
            Config["UseRandomColor"] = useRandomColor;
            Config["UsePlatformSync"] = usePlatformSync;
            Config["UpdateLastMessage"] = updateLastMessage;
            Config["EnableAAlertRaidIntegration"] = enableAAlertRaidIntegration;
            SaveConfig();
        }

        private void Init()
        {
            LoadConfig();
            LoadData();
        }

        private void OnServerInitialized()
        {
            LoadConfig();
            StartUpdateTimer();
        }

        private void OnServerSave() => SaveData();

        private void Unload()
        {
            SaveData();
            StopUpdateTimer();
        }

        private void LoadConfig()
        {
            webhookUrl = Config["WebhookUrl"]?.ToString() ?? webhookUrl;
            updateInterval = Convert.ToInt32(Config["UpdateInterval"] ?? updateInterval);
            selectedLanguage = Config["Language"]?.ToString() ?? selectedLanguage;
            staticColor = Config["EmbedColor"]?.ToString() ?? staticColor;
            useRandomColor = Convert.ToBoolean(Config["UseRandomColor"] ?? useRandomColor);
            usePlatformSync = Convert.ToBoolean(Config["UsePlatformSync"] ?? usePlatformSync);
            updateLastMessage = Convert.ToBoolean(Config["UpdateLastMessage"] ?? updateLastMessage);
            enableAAlertRaidIntegration = Convert.ToBoolean(Config["EnableAAlertRaidIntegration"] ?? true);

            if (!messages.ContainsKey(selectedLanguage))
            {
                PrintWarning($"Language '{selectedLanguage}' not supported. Defaulting to 'en'.");
                selectedLanguage = "en";
            }
        }

        private void StartUpdateTimer()
        {
            StopUpdateTimer();
#if CARBON
            updateTimer = Carbon.Components.Timer.Every(updateInterval, SendOnlinePlayersToDiscord);
#else
            updateTimer = timer.Every(updateInterval, SendOnlinePlayersToDiscord);
#endif
        }

        private void StopUpdateTimer()
        {
            if (updateTimer != null)
            {
#if CARBON
                updateTimer.Destroy();
#else
                updateTimer.Destroy();
#endif
                updateTimer = null;
            }
        }

        private string FormatDiscordMention(ulong discordId)
        {
            return $"<@{discordId}>";
        }

        private string GetDiscordInfo(string steamId)
        {
            if (!enableAAlertRaidIntegration)
                return GetMessage("NotLinked");
            if (usePlatformSync && PlatformSync != null)
                return null;
            try
            {
                var storage = Interface.Oxide.DataFileSystem.ReadObject<RaidStorage>("AAlertRaid/" + steamId);
                if (storage != null && storage.discord != 0UL)
                    return FormatDiscordMention(storage.discord);
            }
            catch { }
            return GetMessage("NotLinked");
        }

        private void SendOnlinePlayersToDiscord()
        {
#if CARBON
            var players = BasePlayer.ActivePlayerList;
#else
            var players = BasePlayer.activePlayerList;
#endif
            if (players == null || players.Count == 0)
            {
                Puts(GetMessage("NoPlayers"));
                return;
            }

            var stringList = Facepunch.Pool.Get<List<string>>();
            try
            {
                stringList.Add(GetMessage("PlayerList"));
                foreach (var player in players)
                {
                    if (player == null) continue;
                    string discordInfo = GetMessage("NotLinked");
                    string steamId = string.Empty;
                    string displayName = string.Empty;
#if CARBON
                    steamId = player.UserID.ToString();
                    displayName = player.DisplayName;
#else
                    steamId = player.userID.ToString();
                    displayName = player.displayName;
#endif
                    if (usePlatformSync && PlatformSync != null)
                    {
                        try
                        {
#if CARBON
                            var linkDetails = PlatformSync?.Call("GetPlayerLinkDetails", player.UserID);
#else
                            var linkDetails = PlatformSync?.Call("GetPlayerLinkDetails", player.userID);
#endif
                            if (linkDetails != null)
                            {
                                var linkedData = linkDetails as Dictionary<string, object>;
                                if (linkedData != null && (bool)linkedData["linked"])
                                {
                                    discordInfo = linkedData["discord_name"]?.ToString() ?? GetMessage("NotLinked");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            PrintError($"Error getting Discord info: {ex.Message}");
                        }
                    }
                    else
                    {
                        discordInfo = GetDiscordInfo(steamId);
                    }
                    stringList.Add(string.Format(GetMessage("PlayerFormat"), displayName, steamId, discordInfo));
                }
                var message = string.Join("\n", stringList);
                SendDiscordMessage(message);
            }
            catch (Exception ex)
            {
                PrintError($"Error in SendOnlinePlayersToDiscord: {ex.Message}");
            }
            finally
            {
                stringList.Clear();
                Facepunch.Pool.FreeList(ref stringList);
            }
        }

        private void SendDiscordMessage(string message)
        {
            var embedColor = useRandomColor ? GetRandomColor() : staticColor;
            var fields = new List<Dictionary<string, object>>();
            DebugLog($"Preparing message with color: {embedColor}");
#if CARBON
            var players = BasePlayer.ActivePlayerList;
#else
            var players = BasePlayer.activePlayerList;
#endif
            DebugLog($"Found {players?.Count ?? 0} players");
            foreach (var player in players)
            {
                if (player == null) continue;
                string discordInfo = GetMessage("NotLinked");
                string steamId = string.Empty;
                string displayName = string.Empty;
#if CARBON
                steamId = player.UserID.ToString();
                displayName = player.DisplayName;
#else
                steamId = player.userID.ToString();
                displayName = player.displayName;
#endif
                if (usePlatformSync && PlatformSync != null)
                {
                    try
                    {
#if CARBON
                        var linkDetails = PlatformSync?.Call("GetPlayerLinkDetails", player.UserID);
#else
                        var linkDetails = PlatformSync?.Call("GetPlayerLinkDetails", player.userID);
#endif
                        DebugLog($"PlatformSync details for {displayName}: {(linkDetails != null ? "received" : "null")}");
                        if (linkDetails != null)
                        {
                            var linkedData = linkDetails as Dictionary<string, object>;
                            if (linkedData != null && (bool)linkedData["linked"])
                            {
                                discordInfo = linkedData["discord_name"]?.ToString() ?? GetMessage("NotLinked");
                                DebugLog($"Discord info for {displayName}: {discordInfo}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        PrintError($"Error getting Discord info: {ex.Message}");
                        DebugLog($"PlatformSync error for {displayName}: {ex.Message}");
                    }
                }
                else
                {
                    var raidDiscord = GetDiscordInfo(steamId);
                    if (!string.IsNullOrEmpty(raidDiscord) && raidDiscord != GetMessage("NotLinked"))
                        discordInfo = raidDiscord;
                }
                fields.Add(new Dictionary<string, object>
                {
                    ["name"] = fields.Count == 0 ? "Name:" : " ",
                    ["value"] = $"[{displayName}](https://steamcommunity.com/profiles/{steamId})",
                    ["inline"] = true
                });
                fields.Add(new Dictionary<string, object>
                {
                    ["name"] = fields.Count == 1 ? "SteamID:" : " ",
                    ["value"] = steamId,
                    ["inline"] = true
                });
                fields.Add(new Dictionary<string, object>
                {
                    ["name"] = fields.Count == 2 ? "Discord:" : " ",
                    ["value"] = discordInfo,
                    ["inline"] = true
                });
            }

            string jsonPayload = $@"
            {{
                ""embeds"": [
                    {{
                        ""color"": {embedColor},
                        ""author"": {{
                            ""name"": ""Online Players List:""
                        }},
                        ""fields"": {Newtonsoft.Json.JsonConvert.SerializeObject(fields)}
                    }}
                ]
            }}";

            DebugLog("Prepared JSON payload");

            if (updateLastMessage && !string.IsNullOrEmpty(lastMessageId))
            {
                try
                {
                    var parts = webhookUrl.Split('/');
                    if (parts.Length >= 7)
                    {
                        var webhookId = parts[5];
                        var webhookToken = parts[6];
                        var url = $"https://discord.com/api/webhooks/{webhookId}/{webhookToken}/messages/{lastMessageId}";
                        
                        DebugLog($"Updating message with ID: {lastMessageId}");
                        
                        webrequest.Enqueue(
                            url,
                            jsonPayload,
                            (code, response) =>
                            {
                                DebugLog($"Update response - Code: {code}, Response: {response}");
                                if (code == 404)
                                {
                                    DebugLog("Message not found or was deleted, sending new message");
                                    lastMessageId = string.Empty;
                                    storedData.LastMessageId = string.Empty;
                                    SaveData();
                                    SendNewMessage(jsonPayload);
                                }
                                else if (code != 200)
                                {
                                    PrintError($"Discord API error (Code: {code}): {response}");
                                    
                                    SendNewMessage(jsonPayload);
                                }
                            },
                            this,
                            RequestMethod.PATCH,
                            new Dictionary<string, string> { ["Content-Type"] = "application/json" }
                        );
                        return;
                    }
                }
                catch (Exception ex)
                {
                    PrintError($"Error preparing webhook URL: {ex.Message}");
                    DebugLog($"Webhook URL error: {ex.Message}");
                    lastMessageId = string.Empty;
                    storedData.LastMessageId = string.Empty;
                    SaveData();
                }
            }

            SendNewMessage(jsonPayload);
        }

        private void SendNewMessage(string jsonPayload)
        {
            DebugLog("Sending new message");
            
            var parts = webhookUrl.Split('/');
            if (parts.Length >= 7)
            {
                var webhookId = parts[5];
                var webhookToken = parts[6];
                var url = $"https://discord.com/api/webhooks/{webhookId}/{webhookToken}?wait=1";

                webrequest.Enqueue(
                    url,
                    jsonPayload,
                    (code, response) =>
                    {
                        DebugLog($"New message response - Code: {code}, Response: {response}");
                        if (code != 204 && code != 200)
                        {
                            PrintError($"Discord API error (Code: {code}): {response}");
                            return;
                        }
                        
                        if (!string.IsNullOrEmpty(response))
                        {
                            try
                            {
                                var responseData = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
                                if (responseData != null && responseData.ContainsKey("id"))
                                {
                                    lastMessageId = responseData["id"].ToString();
                                    storedData.LastMessageId = lastMessageId;
                                    SaveData();
                                    DebugLog($"Successfully saved new message ID: {lastMessageId}");
                                }
                                else
                                {
                                    PrintError("Failed to get message ID from Discord response");
                                    DebugLog($"Response data: {response}");
                                }
                            }
                            catch (Exception ex)
                            {
                                PrintError($"Error parsing Discord response: {ex.Message}");
                                DebugLog($"Response parsing error: {ex.Message}");
                                DebugLog($"Raw response: {response}");
                            }
                        }
                    },
                    this,
                    RequestMethod.POST,
                    new Dictionary<string, string> { ["Content-Type"] = "application/json" }
                );
            }
            else
            {
                PrintError("Invalid webhook URL format");
                DebugLog($"Webhook URL parts count: {parts.Length}");
            }
        }

        private string GetMessage(string key)
        {
            if (messages[selectedLanguage].TryGetValue(key, out var value))
            {
                return value;
            }

            return messages["en"].TryGetValue(key, out var fallback) ? fallback : "Message not found.";
        }

        private string EscapeJson(string text)
        {
            return text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
        }

        private string GetRandomColor()
        {
            System.Random random = new System.Random();
            return random.Next(0, 16777215).ToString(); 
        }

        private void DebugLog(string message)
        {
            if (DEBUG_MODE)
            {
                Puts($"[Debug] {message}");
            }
        }
    }
} 