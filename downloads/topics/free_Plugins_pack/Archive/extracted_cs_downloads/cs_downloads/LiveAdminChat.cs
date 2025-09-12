using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Rust;
using Oxide.Core;
using ConVar;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Ext.Discord.Builders;
using Oxide.Ext.Discord.Constants;
using Oxide.Ext.Discord.Entities;
using Oxide.Ext.Discord.Libraries;
using Oxide.Ext.Discord.Clients;
using Oxide.Ext.Discord.Interfaces;

namespace Oxide.Plugins
{
    [Info("Live Admin Chat", "Daemante", "1.2.3")]
    [Description("Allows players to open a live chat with admins on Discord")]

    public class LiveAdminChat : RustPlugin, IDiscordPlugin
    {
        #region Variables
        public DiscordClient Client { get; set; } = null!;
        public DiscordGuild _discordGuild { get; private set; } = null!;
        public DiscordUser _discordBot { get; private set; } = null!;
        private PluginConfig _config { get; set; } = null!;
        private readonly ChannelMessagesRequest _messageRequest = new ChannelMessagesRequest { Limit = 50 };
        private DiscordSubscriptions _subscriptions { get; set; } = null!;
        private string? serverName;
        private const string LastChatCloseTimesFilePath = "oxide/data/LiveAdminChatCooldowns.json";
        private Dictionary<string, int> _cooldownValues = new Dictionary<string, int>();

        #endregion

        #region Config
        public class PluginConfig
        {
            [JsonProperty(PropertyName = "Discord Bot Token")]
            public string ApiKey { get; set; } = string.Empty;
            
            [JsonProperty(PropertyName = "Discord Server ID")]
            public Snowflake GuildId { get; set; }

            [JsonProperty(PropertyName = "Category ID")]
            public Snowflake CategoryID;
            
            [JsonProperty(PropertyName = "Transcript Channel ID")]
            public Snowflake TranscriptChannelID { get; set; }

            [JsonProperty(PropertyName = "Mentioned Role IDs")]
            public List<string> MentionedRoleIDs { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Reply Command")]
            public string ReplyCommand = "reply";

            [JsonProperty(PropertyName = "Chat Prefix")]
            public string ChatPrefix = "ADMIN";

            [JsonProperty(PropertyName = "Steam Profile Icon")]
            public string SteamProfileIcon = "";

            [JsonProperty(PropertyName = "Default Close Reason")]
            public string DefaultCloseReason = "Closed by Admin";

            [JsonProperty(PropertyName = "Show Admin Username")]
            public Boolean ShowAdminUsername = false;

            [JsonProperty(PropertyName = "Close On Disconnect")]
            public bool CloseOnDisconnect { get; set; } = false;

            [JsonProperty(PropertyName = "Chat Command")]
            public string ChatCommand = "adminchat";

            [JsonProperty(PropertyName = "Chat Cooldown (minutes)")]
            public int ChatCooldownMinutes { get; set; } = 5;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                {
                    throw new Exception();
                }

                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file.");

            _config = new PluginConfig
            {
                ApiKey = "",
                GuildId = default,
                CategoryID = default,
                TranscriptChannelID = default,
                SteamProfileIcon = "",
                MentionedRoleIDs = new List<string>
                {
                    "1234567890", 
                    "0987654321"
                },
                ReplyCommand = "reply",
                ChatPrefix = "ADMIN",
                ShowAdminUsername = false,
                CloseOnDisconnect = false,
                DefaultCloseReason = "Closed by admin",
                ChatCommand = "adminchat",
                ChatCooldownMinutes = 5,
            };
            
            SaveConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LiveChatNotAvailable"] = "[#FFFF00]/{0} is not available yet.[/#]",
                ["LiveChatSuccess"] = "[#00C851]Your request for an admin has been sent.[/#]",
                ["LiveChatAlreadyCalled"] = "[#ff4444]Please wait, your chat request is already in progress.[/#]",
                ["LiveChatMessageLayout"] = "[#00C851]Live Admin Chat[/#] - <size=11>[#dadada]Reply by typing:[/#] [#00C851]/{1} <message>[/#]</size>\n{0}",
                ["PlayerOffline"] = "Player is currently offline; message was not sent.",
                ["PlayerConnected"] = "**{0}** has connected to the server.",
                ["PlayerDisconnected"] = "**{0}** has disconnected from the server.",
                ["ReplyNotAvailable"] = "[#FFFF00]/{0} is not available yet.[/#]",
                ["ReplyCommandUsage"] = "[#FFFF00]Usage: /{0} [message][/#]",
                ["ReplyNoLiveChatInProgress"] = "[#FFFF00]You have no live chat in progress.[/#]",
                ["ReplyWaitForAdminResponse"] = "[#ff4444]Wait until someone responds before sending another message.[/#]",
                ["ReplyMessageSent"] = "[#00C851]Your message has been sent.[/#]\n{0}: {1}",
                ["ChatClosed"] = "[#FFFF00]The live admin chat has ended.[/#]",
                ["NoPermission"] = "[#FFFF00]You do not have permission to use /{0}.[/#]",
                ["ChatOnCooldown"] = "[#FFFF00]You must wait {0} minute(s) before starting a new chat.[/#]"
            }, this);
        }

        private string GetTranslation(string key, string? id = null, params object[] args)
        {
            if (covalence != null)
            {
                return covalence.FormatText(string.Format(lang.GetMessage(key, this, id), args));
            }
            else
            {
                return string.Format(lang.GetMessage(key, this, id), args);
            }
        }

        #endregion

        #region Initialization & Setup
        private void Init()
        {
            _subscriptions = GetLibrary<DiscordSubscriptions>();
            permission.RegisterPermission("liveadminchat.use", this);
            permission.RegisterPermission("liveadminchat.bypasscooldown", this);
            AddCovalenceCommand(_config.ChatCommand, "LiveChatCommand");
            AddCovalenceCommand(_config.ReplyCommand, "ReplyCommand");

            CheckForCooldownsFile();
            CheckAndRemoveExpiredCooldowns();
        }

        private void OnServerInitialized()
        {
            if (string.IsNullOrEmpty(_config.ApiKey))
			{
				PrintWarning("Discord Bot Token is not set. Check your config and reload the plugin.");
				return;
			}
            
            try
            {
                Client.Connect(_config.ApiKey, GatewayIntents.Guilds | GatewayIntents.GuildMembers | GatewayIntents.GuildMessages | GatewayIntents.MessageContent);

            }
            catch (Exception ex)
            {
                PrintError($"Failed to connect Discord client: {ex.Message}");
            }

            serverName = ConVar.Server.hostname;
        }

        [HookMethod(DiscordExtHooks.OnDiscordClientCreated)]
        private void OnDiscordClientCreated()
        {
            if (Client == null)
            {
                PrintError("Discord client failed to initialize.");
                return;
            }
        }

        [HookMethod(DiscordExtHooks.OnDiscordGatewayReady)]
        private void OnDiscordGatewayReady(GatewayReadyEvent ready)
        {
            if (ready == null)
            {
                PrintError("Gateway is not ready.");
                return;
            }
            else
            {
                PrintWarning("Gateway is connected.");
            }

            if (Client == null)
            {
                PrintError("Discord client initialization failed.");
                return;
            }
            else
            {
                PrintWarning("Discord client has been initialized.");
            }

            if (Client?.Bot?.Application == null)
            {
                PrintError("Discord client, bot, or application is null.");
                return;
            }
            else
            {
                PrintWarning($"Bot has connected successfully.");
            }

            if (Client.Bot.Application.Flags.HasValue && !Client.Bot.Application.Flags.Value.HasFlag(ApplicationFlags.GatewayGuildMembersLimited))
            {
                PrintError($"Bot does not have 'Server Members Intent' privileges.");
                return;
            }
            else
            {
                PrintWarning($"Bot has 'Server Members Intent' privileges.");
            }

            if (Client.Bot.Application.Flags.HasValue && !Client.Bot.Application.Flags.Value.HasFlag(ApplicationFlags.GatewayMessageContentLimited))
            {
                PrintError($"Bot does not have 'Message Content Intent' privileges.");
                return;
            }
            else
            {
                PrintWarning($"Bot has 'Message Content Intent' privileges.");
            }

            if (Client.Bot.Application.Flags.HasValue && !Client.Bot.Application.Flags.Value.HasFlag(ApplicationFlags.GatewayPresenceLimited))
            {
                PrintError($"Bot does not have 'Presence Intent' privileges.");
            }
            else
            {
                PrintWarning($"Bot has 'Presence Intent' privileges.");
            }

            DiscordGuild? guild = null;
            if (ready.Guilds.Count == 1 && !_config.GuildId.IsValid())
            {
                guild = ready.Guilds.Values.FirstOrDefault();
            }
            
            if (guild == null && _config.GuildId.IsValid())
            {
                guild = ready.Guilds[_config.GuildId];
            }
            
            if (guild == null)
            {
                PrintError("Failed to find a matching guild for the Discord Server ID provided in the config.");
                return;
            }
            else
            {
                PrintWarning($"Found server: {_config.GuildId}.");
            }
            
            if (ready.Guilds == null || ready.Guilds.Count == 0)
            {
                PrintError("Your bot was not found in any discord servers. Please invite it to a server and reload the plugin.");
                return;
            }
            else
            {
                PrintWarning($"Bot was found in server: {_config.GuildId}.");
            }   
        
            _discordGuild = guild;
            _discordBot = Client.Bot.BotUser;
        
            DiscordChannel category = _discordGuild.Channels[_config.CategoryID];
            if (category == null || category.Type != ChannelType.GuildCategory)
            {
                PrintError($"Category with ID \"{_config.CategoryID}\" doesn't exist. Check your config and reload the plugin.");
                return;
            }
            else
            {
                PrintWarning($"Found category with ID: {_config.CategoryID}.");
            }

            DiscordChannel transcriptChannel = null;
            if (!string.IsNullOrEmpty(_config.TranscriptChannelID))
            {
                transcriptChannel = _discordGuild.Channels[_config.TranscriptChannelID];
            }
            else
            {
                PrintError("Transcript channel ID is not set in the config. Bot will not create transcripts.");
            }

            if (transcriptChannel == null || transcriptChannel.Type != ChannelType.GuildText)
            {
                PrintError($"Transcript channel with ID \"{_config.TranscriptChannelID}\" doesn't exist. Bot will not create transcripts.");
            }
            else
            {
                PrintWarning($"Found transcript channel with ID: {_config.TranscriptChannelID}.");
            }
        
            foreach (DiscordChannel channel in _discordGuild.Channels.Values)
            {
                if (channel.ParentId == _config.CategoryID)
                {
                    SubscribeToChannel(channel);
                }
            }
        
            PrintWarning($"Bot is ready to start chatting.");
        }

        #endregion

        #region Methods

        [HookMethod("StartAdminChat")]
        public bool StartAdminChat(string playerID)
        {
            BasePlayer? player = GetPlayerByID(playerID);
            if (player == null)
            {
                PrintError($"Player with ID {playerID} wasn't found!");
                return false;
            }
            
            string channelName = playerID;
            DiscordChannel existingChannel = _discordGuild.GetChannel(channelName);

            if (existingChannel != null)
            {
                PrintError($"Player {playerID} already has an opened chat!");
                return false;
            }

            try
            {
                var newChannel = new ChannelCreate
                {
                    Name = channelName,
                    Type = ChannelType.GuildText,
                    ParentId = _config.CategoryID,
                    DefaultAutoArchiveDuration = 1440
                };

                _discordGuild.CreateChannel(Client, newChannel).Then(channelTask =>
                {
                    DiscordChannel channel = channelTask;
                    var builder = new MessageComponentBuilder()
                        .AddLinkButton("View steam profile", $"https://steamcommunity.com/profiles/{playerID}");

                    var createMessage = new MessageCreate
                    {
                        Content = $"{string.Join(" ", _config.MentionedRoleIDs.Select(roleId => $"<@&{roleId}>"))}\n\n**{serverName}\nNew chat opened for Steam64ID: {playerID}\nYou are now chatting with player: {GetPlayerByID(playerID)?.displayName}\n\nReply with '!close' to end the chat.\nYou can supply a reason by typing anything you want after '!close'\nExample: !close Banned cheater.\n\nReply with '!cooldown' to set a cooldown period in minutes for the player.\nExample: !cooldown 60**\n\n",
                        Components = builder.Build()
                    };

                    channel.CreateMessage(Client, createMessage);
                }).Catch(exception =>
                {
                    PrintError($"Failed to create channel: {exception.Message}");
                });

                return true;
            }
            catch (Exception ex)
            {
                PrintError($"Error in StartAdminChat: {ex.Message}");
                return false;
            }
        }

        [HookMethod("StopAdminChat")]
        public void StopAdminChat(string playerID, string reason = null)
        {
            DiscordChannel channel = _discordGuild.GetChannel(playerID);
            if (channel == null)
                return;

            DeleteChannel(channel, reason);
        }

        private bool IsLiveChatChannel(DiscordChannel channel)
        {
            return channel.ParentId == _config.CategoryID && (!_config.TranscriptChannelID.IsValid() || channel.Id != _config.TranscriptChannelID);
        }

        private void DeleteChannel(DiscordChannel channel, string reason)
        {
            if (channel == null)
            {
                PrintError("Channel is null. Unable to delete the channel.");
                return;
            }

            if (!IsLiveChatChannel(channel))
            {
                return;
            }
            
            if (string.IsNullOrEmpty(reason))
            {
                reason = _config.DefaultCloseReason;
            }

            if (_discordGuild == null)
            {
                PrintError("Discord guild is not initialized");
                return;
            }
            
            channel.CreateMessage(Client, "Closing the chat in 5 seconds...")
                .Then(messageTask =>
                {
                    SendTranscript(channel, reason);
                    timer.Once(5f, () =>
                    {
                        channel.Delete(Client).Then(deleteTask =>
                        {
                            PrintToConsole($"Successfully deleted channel '{channel.Name}'");
                            SendMessageToPlayerID(channel.Name, $"The chat has been closed by an admin. \nTicket ID: {channel.Name} \nReason: {reason}");
                            string playerID = channel.Name;
                            UpdateLastChatCloseTime(playerID);
                        }).Catch(exception =>
                        {
                            PrintError($"Failed to delete channel '{channel.Name}': {exception.Message}");
                        });
                    });
                }).Catch(exception =>
                {
                    PrintError($"Failed to send closing message: {exception.Message}");
                });
        }

        private void SubscribeToChannel(DiscordChannel channel, string reason = null)
        {
            string channelName = channel.Name;
            if (channelName.Length <= 2)
            {
                PrintError($"Invalid channel name: {channelName}");
                return;
            }

            if (channel == null)
            {
                PrintError("Cannot subscribe to null channel");
                return;
            }

            if (_subscriptions == null)
            {
                PrintError("Discord subscriptions not initialized");
                return;
            }

            _subscriptions.AddChannelSubscription(Client, channel.Id, message =>
            {
                if (message.Author.Id == _discordBot.Id)
                {
                    return;
                }
                
                if (message.Content.StartsWith("!close"))
                {
                    string reason = message.Content.Length > 6 ? message.Content.Substring(7).Trim() : null;
                    string playerID = channel.Name;
                    StopAdminChat(playerID, reason);
                    return;
                }

                if (message.Content.StartsWith("!cooldown"))
                {
                    string[] parts = message.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1)
                    {
                        string cooldownMinutes = parts[1];
                        string playerID = channel.Name;
                        SetCooldown(playerID, cooldownMinutes);
                        var createMessage = new MessageCreate
                        {
                            Content = $"Cooldown of {cooldownMinutes} minutes has been set for player {playerID}."
                        };
                    
                        channel.CreateMessage(Client, createMessage);
                        return;
                    }
                }

                string messageContent = _config.ChatPrefix + ": " + "";
                if (_config.ShowAdminUsername) {
                    messageContent += "[#c9c9c9]" + message.Author.Username + ": [/#]";
                }
                
                messageContent += message.Content;
                // message.CreateReaction(Client, "✅");

                if (!SendMessageToPlayerID(channelName, GetTranslation("LiveChatMessageLayout", channelName, messageContent, _config.ReplyCommand))) {
                    channel.CreateMessage(Client, new MessageCreate
                    {
                        Content = GetTranslation("PlayerOffline")
                    });
                }
            });
        }

        private BasePlayer? GetPlayerByID(string ID)
        {
            try {
                return BasePlayer.FindByID(Convert.ToUInt64(ID));
            } catch {
                return null;
            }
        }

        private bool SendMessageToPlayerID(string playerID, string message)
        {
            if (string.IsNullOrEmpty(playerID))
            {
                PrintError("PlayerID cannot be null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(message))
            {
                PrintError("Message cannot be null or empty");
                return false;
            }
            
            try
            {
                if (!ulong.TryParse(playerID, out ulong steamId))
                {
                    PrintError($"Invalid player ID format: {playerID}");
                    return false;
                }

                BasePlayer player = BasePlayer.FindByID(steamId);
                if (player == null)
                    return false;
            
                player.Command("chat.add", (int)Chat.ChatChannel.Server, _config.SteamProfileIcon, message);
                return true;
            }
            catch (Exception ex)
            {
                PrintError($"Error in SendMessageToPlayerID: {ex.Message}");
                return false;
            }
        }

        private void SendTranscript(DiscordChannel channel, string reason)
        {
            if (channel == null)
            {
                PrintError("Cannot send transcript: channel is null");
                return;
            }

            if (!_config.TranscriptChannelID.IsValid())
            {
                PrintError("Invalid transcript channel ID in configuration");
                return;
            }

            try
            {
                var transcriptChannel = _discordGuild.GetChannel(_config.TranscriptChannelID);

                channel.GetMessages(Client, new ChannelMessagesRequest { Limit = 100 })
                    .Then(messages =>
                    {
                        if (messages == null || !messages.Any())
                            return;

                        var messageList = messages.ToList();
                        messageList.Reverse();
                        
                        var transcriptBuilder = new System.Text.StringBuilder();
                        transcriptBuilder.AppendLine($"{serverName}");
                        transcriptBuilder.AppendLine($"Transcript for ticket: {channel.Name}");
                        transcriptBuilder.AppendLine($"Reason for closing: {reason}");
                        transcriptBuilder.AppendLine("```");

                        for (int i = 1; i < messageList.Count; i++)
                        {
                            var msg = messageList[i];
                            transcriptBuilder.AppendLine($"{msg.Author.Username}: {msg.Content}");
                        }

                        transcriptBuilder.AppendLine("```");

                        var embed = new DiscordEmbed
                        {
                            Title = $"Live Admin Chat",
                            Description = transcriptBuilder.ToString(),
                            Color = DiscordColor.Blue,
                            Timestamp = DateTime.UtcNow
                        };

                        var createMessage = new MessageCreate
                        {
                            Embeds = new List<DiscordEmbed> { embed }
                        };

                        transcriptChannel.CreateMessage(Client, createMessage)
                            .Then(message =>
                            {
                                PrintToConsole($"Successfully created transcript for channel '{channel.Name}'");
                            })
                            .Catch(exception =>
                            {
                                PrintError($"Failed to create transcript message: {exception.Message}");
                            });

                        return;
                    })
                    .Catch(exception =>
                    {
                        PrintError($"Failed to get messages for transcript: {exception.Message}");
                    });
            }
            catch (Exception ex)
            {
                PrintError($"Error in SendTranscript: {ex.Message}");
            }
        }

        private void SetCooldown(string playerID, string cooldownMinutes)
        {
            if (string.IsNullOrEmpty(cooldownMinutes.Trim()))
            {
                PrintError($"Invalid cooldown value: {cooldownMinutes.Trim()}");
                return;
            }
        
            if (!int.TryParse(cooldownMinutes.Trim(), out int cooldownMinutesInt))
            {
                PrintError($"Invalid cooldown value: {cooldownMinutes.Trim()}");
                return;
            }
        
            DiscordChannel channel = _discordGuild.GetChannel(playerID);
        
            if (channel == null)
            {
                PrintError($"Channel with ID '{playerID}' doesn't exist.");
                return;
            }
        
            _cooldownValues[playerID] = cooldownMinutesInt;
            UpdateLastChatCloseTime(playerID);
        }
        
        private void CheckForCooldownsFile()
        {
            try
            {
                if (!File.Exists(LastChatCloseTimesFilePath))
                {

                    File.WriteAllText(LastChatCloseTimesFilePath, "{}");
                    PrintWarning($"Created {LastChatCloseTimesFilePath} as it did not exist.");
                }
            }
            catch (Exception ex)
            {
                PrintError($"Failed to create {LastChatCloseTimesFilePath}: {ex.Message}");
            }
        }
        
        private Dictionary<string, DateTime> LoadLastChatCloseTimes()
        {
            try
            {
                if (File.Exists(LastChatCloseTimesFilePath))
                {
                    var json = File.ReadAllText(LastChatCloseTimesFilePath);
                    return JsonConvert.DeserializeObject<Dictionary<string, DateTime>>(json) ?? new Dictionary<string, DateTime>();
                }
            }
            catch (Exception ex)
            {
                PrintError($"Failed to load last chat close times: {ex.Message}");
            }
            return new Dictionary<string, DateTime>();
        }

        private void SaveLastChatCloseTimes(Dictionary<string, DateTime> lastChatCloseTimes)
        {
            try
            {
                var json = JsonConvert.SerializeObject(lastChatCloseTimes, Formatting.Indented, new JsonSerializerSettings
                {
                    DateFormatString = "yyyy-MM-dd HH:mm"
                });
                File.WriteAllText(LastChatCloseTimesFilePath, json);
            }
            catch (Exception ex)
            {
                PrintError($"Failed to save last chat close times: {ex.Message}");
            }
        }

        private void UpdateLastChatCloseTime(string playerID)
        {
            if (!File.Exists(LastChatCloseTimesFilePath))
            {
                PrintError($"Cooldowns file not found at: {LastChatCloseTimesFilePath}");
                return;
            }
            
            var lastChatCloseTimes = LoadLastChatCloseTimes();
            if (_cooldownValues.TryGetValue(playerID, out int cooldownMinutes))
            {
                lastChatCloseTimes[playerID] = DateTime.Now.AddMinutes(cooldownMinutes);
            }
            else if (!lastChatCloseTimes.ContainsKey(playerID))
            {
                lastChatCloseTimes[playerID] = DateTime.Now.AddMinutes(_config.ChatCooldownMinutes);
            }
            SaveLastChatCloseTimes(lastChatCloseTimes);
        }

        private void CheckAndRemoveExpiredCooldowns()
        {
            if (!File.Exists(LastChatCloseTimesFilePath))
            {
                PrintError($"Cooldowns file not found at: {LastChatCloseTimesFilePath}");
                return;
            }
            
            var lastChatCloseTimes = LoadLastChatCloseTimes();
            var expiredEntries = lastChatCloseTimes.Where(entry => entry.Value < DateTime.Now).ToList();

            foreach (var expiredEntry in expiredEntries)
            {
                lastChatCloseTimes.Remove(expiredEntry.Key);
            }

            SaveLastChatCloseTimes(lastChatCloseTimes);
        }

        [ConsoleCommand("liveadminchat.setcooldown")]
        private void ConsoleCommandSetCooldown(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length != 2)
            {
                Puts("Usage: liveadminchat.setcooldown <steam64id> <cooldown_minutes>");
                return;
            }

            string steam64id = arg.Args[0];
            if (!ulong.TryParse(steam64id, out _))
            {
                Puts($"Invalid Steam64ID: {steam64id}");
                return;
            }

            if (!int.TryParse(arg.Args[1], out int cooldownMinutes) || cooldownMinutes <= 0)
            {
                Puts($"Invalid cooldown value: {arg.Args[1]}. Please provide a positive integer in minutes.");
                return;
            }

            string playerID = steam64id;
            _cooldownValues[playerID] = cooldownMinutes;
            var lastChatCloseTimes = LoadLastChatCloseTimes();
            lastChatCloseTimes[playerID] = DateTime.Now.AddMinutes(cooldownMinutes);
            SaveLastChatCloseTimes(lastChatCloseTimes);

            Puts($"Cooldown of {cooldownMinutes} minutes has been set for player: {steam64id}.");
        }

        [ConsoleCommand("liveadminchat.removecooldown")]
        private void ConsoleCommandRemoveCooldown(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length != 1)
            {
                Puts("Usage: liveadminchat.removecooldown <steam64id>");
                return;
            }

            string steam64id = arg.Args[0];
            if (!ulong.TryParse(steam64id, out _))
            {
                Puts($"Invalid Steam64ID: {steam64id}");
                return;
            }

            string playerID = steam64id;
            _cooldownValues.Remove(playerID);
            var lastChatCloseTimes = LoadLastChatCloseTimes();
            bool removed = lastChatCloseTimes.Remove(playerID);
            SaveLastChatCloseTimes(lastChatCloseTimes);

            if (removed)
            {
                Puts($"Cooldown has been removed for player: {steam64id}.");
            }
            else
            {
                Puts($"No cooldown was set for player {steam64id}.");
            }
        }

        [ConsoleCommand("liveadminchat.listcooldowns")]
        private void ConsoleCommandListCooldowns(ConsoleSystem.Arg arg)
        {
            var lastChatCloseTimes = LoadLastChatCloseTimes();
            
            if (lastChatCloseTimes.Count == 0)
            {
                Puts("No active cooldowns found.");
                return;
            }

            Puts("Current active cooldowns:");
            Puts("------------------------");
            foreach (var cooldown in lastChatCloseTimes)
            {
                TimeSpan remainingTime = cooldown.Value - DateTime.Now;
                if (remainingTime.TotalMinutes > 0)
                {
                    Puts($"Steam64ID: {cooldown.Key}");
                    Puts($"Expires: {cooldown.Value:yyyy-MM-dd HH:mm:ss}");
                    Puts($"Remaining: {Math.Ceiling(remainingTime.TotalMinutes)} minutes");
                    Puts("------------------------");
                }
            }
        }

        #endregion

        #region Events

        [HookMethod(DiscordExtHooks.OnDiscordGuildChannelCreated)]
        private void OnDiscordGuildChannelCreated(DiscordChannel channel)
        {
            if (channel.ParentId == _config.CategoryID)
                SubscribeToChannel(channel);
        }

        [HookMethod(DiscordExtHooks.OnDiscordGuildChannelDeleted)]
        private void OnDiscordGuildChannelDeleted(DiscordChannel channel)
        {
            if (channel.ParentId == _config.CategoryID) {
                string channelName = channel.Name;
                if (channelName.Length != 17)
                    return;

                SendMessageToPlayerID(channelName, GetTranslation("ChatClosed", channelName));
            }
        }

        [HookMethod("OnPlayerConnected")]
        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            
            DiscordChannel? channel = _discordGuild?.GetChannel(player.UserIDString);
            if (channel != null)
            {
                try
                {
                    channel.CreateMessage(Client, new MessageCreate
                    {
                        Content = GetTranslation("PlayerConnected", player.UserIDString, player.displayName)
                    });
                }
                catch (Exception ex)
                {
                    PrintError($"Failed to send player connection message: {ex.Message}");
                }
            }
        }

        [HookMethod("OnPlayerDisconnected")]
        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null) return;

            DiscordChannel? channel = _discordGuild?.GetChannel(player.UserIDString);
            if (channel != null)
            {
                try
                {
                    channel.CreateMessage(Client, new MessageCreate
                    {
                        Content = GetTranslation("PlayerDisconnected", player.UserIDString, player.displayName)
                    });
                }
                catch (Exception ex)
                {
                    PrintError($"Failed to send player disconnection message: {ex.Message}");
                }
            }

            if (_config.CloseOnDisconnect)
            {
                StopAdminChat(player.UserIDString);
            }
        }

        #endregion

        #region Commands

        [ChatCommand("adminchat")]
        private void LiveChatCommand(IPlayer player, string command, string[] args)
        {
            if (Client == null)
            {
                player.Reply(GetTranslation("LiveChatNotAvailable", player.Id));
                PrintError("Discord client is null");
                return;
            }

            if (!Client.IsConnected())
            {
                player.Reply(GetTranslation("LiveChatNotAvailable", player.Id));
                PrintError("Discord client is not connected");
                return;
            }

            if (!player.HasPermission("liveadminchat.use"))
            {
                player.Reply(GetTranslation("NoPermission", player.Id));
                PrintWarning("Player does not have permission.");
                return;
            }

            if (_discordGuild == null)
            {
                player.Reply(GetTranslation("LiveChatNotAvailable", player.Id));
                PrintError("Discord guild is null.");
                return;
            }

            bool hasBypassPermission = player.HasPermission("liveadminchat.bypasscooldown");
            if (!hasBypassPermission)
            {
                var lastChatCloseTimes = LoadLastChatCloseTimes();
                if (lastChatCloseTimes.TryGetValue(player.Id, out DateTime cooldownEndTime))
                {
                    if (cooldownEndTime > DateTime.Now)
                    {
                        int remainingMinutes = (int)Math.Ceiling((cooldownEndTime - DateTime.Now).TotalMinutes);
                        SendMessageToPlayerID(player.Id, GetTranslation("ChatOnCooldown", player.Id, remainingMinutes));
                        return;
                    }
                }
            }

            try 
            {
                bool chatStarted = StartAdminChat(player.Id);
                if (!chatStarted)
                {
                    SendMessageToPlayerID(
                        player.Id,
                        GetTranslation("LiveChatAlreadyCalled", player.Id)
                    );
                }
                else
                {
                    SendMessageToPlayerID(
                        player.Id,
                        GetTranslation("LiveChatSuccess", player.Id)
                    );
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error in LiveChatCommand: {ex.Message}");
                player.Reply("An error occurred while processing your request.");
            }
        }

        private void ReplyCommand(IPlayer player, string command, string[] args)
        {
            if (_discordGuild == null) {
                player.Reply(GetTranslation("ReplyNotAvailable", player.Id, _config.ReplyCommand));
                return;
            }

            if (args.Length < 1) {
                player.Reply(GetTranslation("ReplyCommandUsage", player.Id, _config.ReplyCommand));
                return;
            }
            
            DiscordChannel replyChannel = _discordGuild.GetChannel(player.Id);
            string sentMessage = string.Join(" ", args);

            if (replyChannel == null) {
                SendMessageToPlayerID(player.Id, GetTranslation("ReplyNoLiveChatInProgress", player.Id));
                return;
            }

            replyChannel.GetMessages(Client, _messageRequest).Then(messagesTask =>
            {
                var messages = messagesTask;
                if (messages.Count < 2)
                {
                    SendMessageToPlayerID(player.Id, GetTranslation("ReplyWaitForAdminResponse", player.Id));
                    return;
                }
            
                DateTime now = DateTime.Now;
                replyChannel.CreateMessage(Client, new MessageCreate
                {
                    Content = $"{player.Name}: {sentMessage}"
                });
                SendMessageToPlayerID(player.Id, GetTranslation("ReplyMessageSent", player.Id, player.Name, sentMessage));
            }).Catch(exception =>
            {
                PrintError($"Failed to get messages: {exception.Message}");
            });
        }

        #endregion
    }
}