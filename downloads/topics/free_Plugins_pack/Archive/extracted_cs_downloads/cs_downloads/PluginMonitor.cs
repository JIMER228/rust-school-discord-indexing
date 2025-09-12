using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine.Networking;
using System.Collections;

namespace Oxide.Plugins
{
    [Info("PluginMonitor", "Mentol", "1.0.4")]
    [Description("Monitors plugins status and reports to Discord webhook")]
    public class PluginMonitor : CovalencePlugin
    {
        private Configuration _config;
        private Timer _checkTimer;
        private string _lastMessageId = string.Empty;
        private bool _isUpdating = false;

        #region Configuration
        private class Configuration
        {
            [JsonProperty("Discord Webhook URL")]
            public string WebhookUrl { get; set; } = "https://discord.com/api/webhooks/your/webhook/url";

            [JsonProperty("Check Interval (minutes)")]
            public int CheckIntervalMinutes { get; set; } = 5;

            [JsonProperty("Embed Color (hex)")]
            public string EmbedColor { get; set; } = "#00ff00";

            [JsonProperty("Server Name")]
            public string ServerName { get; set; } = "Rust Server";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                
                if (!_config.WebhookUrl.StartsWith("https://discord.com/api/webhooks/"))
                {
                    LogError("Invalid Discord webhook URL in config! Please update it.");
                    _config.WebhookUrl = "https://discord.com/api/webhooks/your/webhook/url";
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);
        #endregion

        #region Oxide Hooks
        private void Init()
        {
            LoadConfig();
            if (_config.WebhookUrl == "https://discord.com/api/webhooks/your/webhook/url")
            {
                LogError("Please configure your Discord webhook URL in the config file!");
                return;
            }
            StartMonitoring();
        }

        private void Unload()
        {
            _checkTimer?.Destroy();
        }
        #endregion

        #region Plugin Logic
        private void StartMonitoring()
        {
            _checkTimer?.Destroy();
            _checkTimer = timer.Every(_config.CheckIntervalMinutes * 60f, () =>
            {
                if (!_isUpdating)
                {
                    CheckPluginsStatus();
                }
            });
            
            CheckPluginsStatus();
        }

        private void CheckPluginsStatus()
        {
            if (_isUpdating) return;
            _isUpdating = true;

            var pluginStatuses = new Dictionary<string, bool>();
            var totalPlugins = 0;
            var workingPlugins = 0;
            
            foreach (var plugin in plugins.GetAll())
            {
                if (plugin == null) continue;
                totalPlugins++;
                
                try
                {
                    bool isWorking = IsPluginWorking(plugin);
                    if (isWorking) workingPlugins++;
                    pluginStatuses.Add(plugin.Name, isWorking);
                }
                catch (Exception ex)
                {
                    pluginStatuses.Add(plugin.Name, false);
                    LogError($"Error checking plugin {plugin.Name}: {ex.Message}");
                }
            }

            var fields = new List<object>
            {
                new
                {
                    name = "Status Summary",
                    value = $"Working: {workingPlugins}/{totalPlugins}\nLast Updated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
                    inline = false
                },
                new
                {
                    name = "Server Info",
                    value = $"Server Name: {_config.ServerName}\nCheck Interval: {_config.CheckIntervalMinutes} minutes",
                    inline = false
                }
            };

            UpdateDiscordMessage(pluginStatuses, fields);
        }

        private bool IsPluginWorking(Plugin plugin)
        {
            if (plugin == null) return false;
            
            try
            {
                if (string.IsNullOrEmpty(plugin.Name)) return false;
                if (!plugin.IsLoaded) return false;
                if (plugin.Object == null) return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void UpdateDiscordMessage(Dictionary<string, bool> pluginStatuses, List<object> fields)
        {
            var embed = new
            {
                title = $"Plugin Monitor - {_config.ServerName}",
                description = FormatPluginStatus(pluginStatuses),
                color = int.Parse(_config.EmbedColor.TrimStart('#'), System.Globalization.NumberStyles.HexNumber),
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                fields = fields,
                footer = new
                {
                    text = $"Last Updated • {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"
                }
            };

            var payload = new
            {
                embeds = new[] { embed }
            };

            string jsonPayload = JsonConvert.SerializeObject(payload);
            ServerMgr.Instance.StartCoroutine(SendWebhookCoroutine(jsonPayload));
        }

        private IEnumerator SendWebhookCoroutine(string jsonPayload)
        {
            string url;
            string method;
            
            if (string.IsNullOrEmpty(_lastMessageId))
            {
                url = _config.WebhookUrl;
                method = "POST";
            }
            else
            {
                // Правильный формат URL для обновления сообщения
                var webhookParts = _config.WebhookUrl.Split('/');
                var webhookId = webhookParts[webhookParts.Length - 2];
                var webhookToken = webhookParts[webhookParts.Length - 1];
                url = $"https://discord.com/api/webhooks/{webhookId}/{webhookToken}/messages/{_lastMessageId}";
                method = "PATCH";
            }

            using (var request = new UnityWebRequest(url, method))
            {
                byte[] jsonToSend = new UTF8Encoding().GetBytes(jsonPayload);
                request.uploadHandler = new UploadHandlerRaw(jsonToSend);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                HandleWebhookResponse(request);
            }

            _isUpdating = false;
        }

        private void HandleWebhookResponse(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.Success)
            {
                if (string.IsNullOrEmpty(_lastMessageId) && !string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    try
                    {
                        var response = JsonConvert.DeserializeObject<Dictionary<string, object>>(request.downloadHandler.text);
                        if (response != null && response.ContainsKey("id"))
                        {
                            _lastMessageId = response["id"].ToString();
                            Puts($"Successfully created Discord message with ID: {_lastMessageId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"Failed to parse Discord response: {ex.Message}");
                        LogError($"Response content: {request.downloadHandler.text}");
                    }
                }
                else
                {
                    Puts("Successfully updated Discord message");
                }
            }
            else
            {
                LogError($"Failed to send Discord message: {request.error}");
                _lastMessageId = string.Empty;
            }
        }

        private string FormatPluginStatus(Dictionary<string, bool> pluginStatuses)
        {
            var sb = new StringBuilder();
            sb.AppendLine("```diff");
            
            foreach (var status in pluginStatuses.OrderBy(x => x.Key))
            {
                if (status.Value)
                    sb.AppendLine($"+ {status.Key}: Working");
                else
                    sb.AppendLine($"- {status.Key}: Not Working");
            }
            
            sb.AppendLine("```");
            return sb.ToString();
        }
        #endregion

        #region Commands
        [ChatCommand("pluginmonitor")]
        private void PluginMonitorCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) 
            {
                player.ChatMessage("You don't have permission to use this command.");
                return;
            }
            
            if (args.Length > 0 && args[0].ToLower() == "check")
            {
                CheckPluginsStatus();
                player.ChatMessage("Manually triggered plugin status check.");
                return;
            }
            
            player.ChatMessage("Usage: /pluginmonitor check - Manually check plugin status");
        }
        #endregion
    }
}