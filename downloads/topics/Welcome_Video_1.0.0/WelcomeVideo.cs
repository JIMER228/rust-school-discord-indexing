using Newtonsoft.Json;
using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("WelcomeVideo", "AjiE", "1.0.0")]
    class WelcomeVideo : RustPlugin
    {
        private ConfigData configData;
        private JoinData joinData;
        class ConfigData
        {
            [JsonProperty("Permission Name")]
            public string PermissionName = "WelcomeVideo.use";
            [JsonProperty("Need Permission")]
            public bool NeedPermission = true;
            [JsonProperty("Welcome Video List")]
            public List<VideoSetting> URLs = new List<VideoSetting>();
            [JsonProperty("Only first-time join the server")]
            public bool OnlyFrist = false;
            [JsonProperty("Players can disable the WelcomeVideo (/wv)")]
            public bool CanDisable = false;
        }
        private class VideoSetting
        {
            [JsonProperty("Need Permission (Empty = none)")]
            public string PermissionName = "";
            [JsonProperty("Video URL")]
            public string URL = "https://yourwebsite.com/video.mp4";
            [JsonProperty("Welcome Message (Empty = No Message)")]
            public string Message = "Welcome To Our Server, Now playing video for you~ (You can use command /wv to disable)";
        }
        private bool LoadConfigVariables()
        {
            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
                return false;
            }
            SaveConfig(configData);
            return true;
        }
        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            configData = new ConfigData();
            GetDefaultConfig();
            SaveConfig(configData);
        }

        void GetDefaultConfig()
        {
            if (configData.URLs.Count <= 0 || configData.URLs == null)
            {
                configData.URLs = new List<VideoSetting>
                {
                    new VideoSetting()
                    {
                        PermissionName = "",
                        URL = "https://github.com/blgarust/music/raw/main/4_1.mp4",
                        Message = "Welcome To Our Server, Now playing video for you~ (You can use command /wv to disable)"
                    },
                    new VideoSetting()
                    {
                        PermissionName = "WelcomeVideo.vip",
                        URL = "https://yourwebsite.com/video.mp4",
                        Message = "Welcome To Our Server, Now playing video for you~ (You can use command /wv to disable)"
                    }
                };
            }
        }

        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        public class JoinData
        {
            [JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ulong> Players = new List<ulong>();
            [JsonProperty(PropertyName = "Disable Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ulong> DisablePlayers = new List<ulong>();
        }
        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, joinData);
        }
        void Init()
        {
            if (!LoadConfigVariables())
            {
                Puts("Config file issue detected. Please delete file, or check syntax and fix.");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }
            if (!string.IsNullOrEmpty(configData.PermissionName))
                permission.RegisterPermission(configData.PermissionName, this);
            if (configData.URLs.Count == 0)
            {
                Puts("You haven't set the Video URL List!!!");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }
            joinData = Interface.Oxide.DataFileSystem.ReadObject<JoinData>(Name);
            foreach (var music in configData.URLs)
            {
                Register(music.PermissionName);
            }
        }
        public void Register(String Permissions)
        {
            if (!String.IsNullOrWhiteSpace(Permissions))
            {
                if (!permission.PermissionExists(Permissions, this))
                    permission.RegisterPermission(Permissions, this);
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            if (configData.NeedPermission && !permission.UserHasPermission(player.UserIDString, configData.PermissionName)) return;
            if (configData.CanDisable)
            {
                if (joinData.DisablePlayers.Contains(player.userID)) return;
            }
            if (configData.URLs.Count == 0)
            {
                Puts("You haven't set the Video URL List!!!");
                return;
            }
            if (configData.OnlyFrist)
            {
                if (joinData.Players.Contains(player.userID))
                    return;
            }
            if (player.IsSleeping())
            {
                timer.Once(2f, () => OnPlayerConnected(player));
                return;
            }
            string URL = null;
            string MESSAGE = null;
            var music = GetPlayerRandomVideo(player);
            if (music != null)
            {
                URL = music.URL;
                MESSAGE = music.Message;
            }
            PlayVideo(player, URL);
            if (!string.IsNullOrEmpty(MESSAGE))
            {
                player.ChatMessage(MESSAGE);
            }
            if (configData.OnlyFrist)
            {
                if (!joinData.Players.Contains(player.userID))
                    joinData.Players.Add(player.userID);
            }
        }
        VideoSetting GetPlayerRandomVideo(BasePlayer player)
        {
            VideoSetting musicSetting = null;
            foreach (var music in configData.URLs)
            {
                if (!string.IsNullOrEmpty(music.PermissionName))
                {
                    if (permission.UserHasPermission(player.UserIDString, music.PermissionName))
                    {
                        musicSetting = music;
                        break;
                    }
                }
            }
            if (musicSetting == null)
            {
                VideoSetting randomMusic = configData.URLs
                .Where(setting => string.IsNullOrEmpty(setting.PermissionName))
                .OrderBy(_ => Guid.NewGuid())
                .FirstOrDefault();
                musicSetting = randomMusic;
            }
            return musicSetting;
        }
        void PlayVideo(BasePlayer player, string url)
        {
            player.Command("client.playvideo", url);
        }
        [ChatCommand("testwv")]
        void TestWelcomeVideoCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                player.ChatMessage($"[WelcomeVideo] You not have permission to use this command.");
                return;
            }
            if (configData.OnlyFrist && joinData.Players.Contains(player.userID))
            {
                player.ChatMessage("[WelcomeVideo] Currently playing video for you, please try again later");
                return;
            }
            if (joinData.DisablePlayers.Contains(player.userID))
            {
                player.ChatMessage("[WelcomeVideo] You have disabled the welcome video, use command /wv to enable.");
                return;
            }
            OnPlayerConnected(player);
        }
        [ChatCommand("wv")]
        void WelcomeVideoCommand(BasePlayer player, string command, string[] args)
        {
            if (!configData.CanDisable)
            {
                player.ChatMessage($"[WelcomeVideo] The server is not allowed to disable welcome video!");
                return;
            }
            if (!joinData.DisablePlayers.Contains(player.userID))
            {
                joinData.DisablePlayers.Add(player.userID);
                player.ChatMessage($"[WelcomeVideo] You have successfully disabled the welcome video!");
                return;
            }
            else
            {
                joinData.DisablePlayers.Remove(player.userID);
                player.ChatMessage($"[WelcomeVideo] You have successfully enabled the welcome video!");
                return;
            }
        }

    }
}