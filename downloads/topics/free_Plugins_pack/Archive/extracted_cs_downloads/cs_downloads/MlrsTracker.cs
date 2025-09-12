using Newtonsoft.Json;
using Oxide.Core.Libraries;
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Oxide.Plugins
{
    [Info("MLRS Tracker", "Rogder Dodger", "1.2.0")]
    [Description("Tracks Firing of MLRS ")]
    internal class MlrsTracker : RustPlugin
    {
        #region Configuration
        private const string DefaultWebhookURL = "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
        private Configuration _config;
        private class Configuration
        {
            [JsonProperty(PropertyName = "Log To Console")]
            public bool LogToConsole = true;

            [JsonProperty(PropertyName = "Log To Discord")]
            public bool LogToDiscord = true;

            [JsonProperty(PropertyName = "Discord Webhook URL")]
            public string DiscordHookUrl = DefaultWebhookURL;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
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
            PrintWarning("A new configuration file is being generated.");
            _config = new Configuration
            {
                LogToConsole = true,
                LogToDiscord = true,
                DiscordHookUrl = DefaultWebhookURL,
            };
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion

        #region hooks

        private void Init()
        {
            var cfg = Config.ReadObject<Configuration>();

            if (cfg.DiscordHookUrl == DefaultWebhookURL)
            {
                PrintWarning($"Please set the discord webhook in the configuration file and reload the plugin");
            }
        }


        void OnMlrsFired(MLRS mlrs, BasePlayer player)
        {
            var msg = $"{player.displayName}/{player.userID} Fired MLRS Rockets At {GetGrid(mlrs.TrueHitPos)} " + "```teleportpos " + Math.Round(mlrs.TrueHitPos.x, 2) + "," + Math.Round(mlrs.TrueHitPos.y, 2) + "," + Math.Round(mlrs.TrueHitPos.z, 2) + "```";
            if (_config.LogToConsole)
            {
                Puts(msg);
            }
            if (_config.LogToDiscord)
            {
                SendToDiscord(msg);
            }
        }

        #endregion

        #region Discord

        void SendToDiscord(string message)
        {
            var contentBody = new WebHookContentBody
            {
                Content = message
            };
            webrequest.Enqueue(_config.DiscordHookUrl, JsonConvert.SerializeObject(contentBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }),
            (headerCode, headerResult) =>
            {
                if (headerCode >= 200 && headerCode <= 204)
                {
                }
                else
                {
                    Puts($"Failed to send to discord: {headerCode} - {headerResult}");
                }
            }, this, RequestMethod.POST,
                    new Dictionary<string, string> { { "Content-Type", "application/json" } });
        }

        private class WebHookContentBody
        {
            [JsonProperty(PropertyName = "content")]
            public string Content;
        }

        #endregion

        #region methods

        private string GetGrid(Vector3 pos)
        {
            char letter = 'A';
            var x = Mathf.Floor((pos.x + (ConVar.Server.worldsize / 2)) / 146.3f) % 26;
            var count = Mathf.Floor(Mathf.Floor((pos.x + (ConVar.Server.worldsize / 2)) / 146.3f) / 26);
            var z = (Mathf.Floor(ConVar.Server.worldsize / 146.3f)) - Mathf.Floor((pos.z + (ConVar.Server.worldsize / 2)) / 146.3f);
            letter = (char)(letter + x);
            var secondLetter = count <= 0 ? string.Empty : ((char)('A' + (count - 1))).ToString();
            return $"{secondLetter}{letter}{z}";
        }

        #endregion

    }
}
