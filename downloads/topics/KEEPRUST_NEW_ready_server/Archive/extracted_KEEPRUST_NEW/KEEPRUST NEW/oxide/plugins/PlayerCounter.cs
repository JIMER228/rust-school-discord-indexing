using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core.Plugins;
using Oxide.Ext.Discord;
using Oxide.Ext.Discord.Constants;
using Oxide.Ext.Discord.Attributes;
using Oxide.Ext.Discord.Entities.Activities;
using Oxide.Ext.Discord.Entities.Gatway.Commands;
using Oxide.Ext.Discord.Entities.Gatway.Events;
using System;
using System.Collections.Generic;
using Oxide.Ext.Discord.Logging;

namespace Oxide.Plugins
{
    [Info("PlayerCounter", "Farkas", "3.0")]
    class PlayerCounter : RustPlugin
    {
        [DiscordClient]
        private DiscordClient _client;

        private ConfigData _configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Bot token")]
            public string token = "";
            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty(PropertyName = "Bot's activity type: (Game, Listening, Watching, Streaming, Competing)")]
            public ActivityType activitytype = ActivityType.Game;
            [JsonProperty(PropertyName = "Presence update interval (in seconds)")]
            public float interval = 30;
        }

        private bool LoadConfigVariables()
        {
            try
            {
                _configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
                return false;
            }
            SaveConfig(_configData);
            return true;
        }

        void Init()
        {
            if (!LoadConfigVariables())
            {
                Puts("Config file issue detected. Please delete file, or check syntax and fix.");
                return;
            }
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            _configData = new ConfigData();
            SaveConfig(_configData);
        }

        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }

        [HookMethod(DiscordExtHooks.OnDiscordGatewayReady)]
        private void OnDiscordGatewayReady(GatewayReadyEvent ready)
        {
            if (ready.Guilds.Count == 0)
            {
                PrintError("Please invite the bot into your discord server and reload the plugin.");
                _client.Disconnect();
                return;
            }

            Puts("Your discord bot connected succesfully.");
        }

        void OnServerInitialized(bool initial)
        {
            if (string.IsNullOrEmpty(_configData.token))
            {
                return;
            }

            _client.Connect(new DiscordSettings
            {
                ApiToken = _configData.token,
                Intents = Ext.Discord.Entities.Gatway.GatewayIntents.Guilds,
                //LogLevel = DiscordLogLevel.Verbose //remove comment for debugging
            });

            UpdateStatus();
            timer.Every(_configData.interval, () => UpdateStatus());
        }

        private void UpdateStatus()
        {
            string description = $"{BasePlayer.activePlayerList.Count}/{ConVar.Server.maxplayers}";

            if (ServerMgr.Instance.connectionQueue.joining.Count != 0)
            {
                description += $" ({ServerMgr.Instance.connectionQueue.joining.Count})";
            }

            if (ServerMgr.Instance.connectionQueue.queue.Count != 0)
            {
                description += $" - {ServerMgr.Instance.connectionQueue.queue.Count} queue";
            }

            _client.Bot.UpdateStatus(new UpdatePresenceCommand
            {
                Activities = new List<DiscordActivity>
                {
                    new DiscordActivity
                    {
                        Type = _configData.activitytype,
                        Name = description,
                    }
                }
            });
        }
    }
}