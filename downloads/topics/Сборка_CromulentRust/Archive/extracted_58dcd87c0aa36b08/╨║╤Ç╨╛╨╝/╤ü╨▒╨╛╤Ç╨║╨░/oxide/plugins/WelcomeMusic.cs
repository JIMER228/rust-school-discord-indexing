using Newtonsoft.Json;
using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("WelcomeMusic", "Ajie", "1.1.0")]
    [Description("Play music for players when they join the server.")]
    class WelcomeMusic : RustPlugin
    {
        private ConfigData configData;
        private Dictionary<BasePlayer, BaseEntity> BoomBoxList = new Dictionary<BasePlayer, BaseEntity>();
        private JoinData joinData;
        private string SpherePrefab = "assets/prefabs/visualization/sphere.prefab";
        private string BoomboxPrefab = "assets/prefabs/voiceaudio/boombox/boombox.deployed.prefab";
        class ConfigData
        {
            [JsonProperty("Permission Name")]
            public string PermissionName = "welcomemusic.use";
            [JsonProperty("Need Permission")]
            public bool NeedPermission = true;
            [JsonProperty("Welcome Music List")]
            public List<MusicSetting> URLs = new List<MusicSetting>();
            [JsonProperty("Only first-time join the server")]
            public bool OnlyFrist = false;
            [JsonProperty("(First-time) Music URL (Empty = none)")]
            public string URLF = "";
            [JsonProperty("(First-time) Music Duration (sec)")]
            public float DurationF = 15f;
            [JsonProperty("Music Delay (sec)")]
            public float Delay = 5f;
            [JsonProperty("Players can disable the WelcomeMusic (/wm)")]
            public bool CanDisable = false;
        }

        private class MusicSetting
        {
            [JsonProperty("Need Permission (Empty = none)")]
            public string PermissionName = "";
            [JsonProperty("Music URL")]
            public string URL = "https://yourwebsite.com/music.mp3";
            [JsonProperty("Music Duration (sec)")]
            public float Duration = 15f;
            [JsonProperty("Welcome Message (Empty = No Message)")]
            public string Message = "Welcome To Our Server, Now playing music for you~ (You can use command /wm to disable)";
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
            permission.RegisterPermission("welcomemusic.toplayer", this);
            if (configData.URLs.Count == 0)
            {
                Puts("You haven't set the Music URL List!!!");
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
                configData.URLs = new List<MusicSetting>
                {
                    new MusicSetting()
                    {
                        PermissionName = "",
                        URL = "https://github.com/blgarust/music/raw/main/WelcomeToOurServer.mp3",
                        Duration = 5.0f,
                        Message = "Welcome To Our Server, Now playing music for you~ (You can use command /wm to disable)"
                    },
                    new MusicSetting()
                    {
                        PermissionName = "welcomemusic.vip",
                        URL = "https://github.com/blgarust/music/raw/main/NeverGonnaGiveYouUp.mp3",
                        Duration = 30.0f,
                        Message = "Never Gonna Give You Up ~"
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
        void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            if (configData.NeedPermission && !permission.UserHasPermission(player.UserIDString, configData.PermissionName)) return;
            if (BoomBoxList.ContainsKey(player)) return;
            if (configData.CanDisable)
            {
                if (joinData.DisablePlayers.Contains(player.userID.Get())) return;
            }
            if (configData.URLs.Count == 0)
            {
                Puts("You haven't set the Music URL List!!!");
                return;
            }
            if (configData.OnlyFrist)
            {
                if (joinData.Players.Contains(player.userID.Get()))
                    return;
            }
            if (player.IsSleeping())
            {
                timer.Once(2f, () => OnPlayerConnected(player));
                return;
            }
            string URL = null;
            float TIME = 5f;
            string MESSAGE = null;
            if (!string.IsNullOrEmpty(configData.URLF) && !joinData.Players.Contains(player.userID.Get()))
            {
                URL = configData.URLF;
                TIME = configData.DurationF;
                joinData.Players.Add(player.userID.Get());
            }
            else
            {
                var music = GetPlayerRandomMusic(player);
                if (music != null)
                {
                    URL = music.URL;
                    TIME = music.Duration;
                    MESSAGE = music.Message;
                }
            }
            if (!string.IsNullOrEmpty(URL)) 
            {
                timer.Once(configData.Delay + 3f, () => {
                    MusicToPlayer(player, URL, TIME);
                    if (!string.IsNullOrEmpty(MESSAGE))
                        player.ChatMessage(MESSAGE);
                });
            }
            if (configData.OnlyFrist)
            {
                if (!joinData.Players.Contains(player.userID.Get()))
                    joinData.Players.Add(player.userID.Get());
            }
        }
        void OnPlayerDisconnected(BasePlayer player)
        {
            if (BoomBoxList.ContainsKey(player))
            {
                BaseEntity sph;
                if (BoomBoxList.TryGetValue(player, out sph))
                {
                    BoomBoxList.Remove(player);
                    sph.Kill();
                }
            }
        }
        MusicSetting GetPlayerRandomMusic(BasePlayer player)
        {
            MusicSetting musicSetting = null;
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
                MusicSetting randomMusic = configData.URLs
                .Where(setting => string.IsNullOrEmpty(setting.PermissionName))
                .OrderBy(_ => Guid.NewGuid())
                .FirstOrDefault();
                musicSetting = randomMusic;
            }
            return musicSetting;
        }
        [ChatCommand("testwm")]
        void TestWelcomeMusicCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                player.ChatMessage($"[WelcomeMusic] You not have permission to use this command.");
                return;
            }
            if (BoomBoxList.ContainsKey(player))
            {
                player.ChatMessage("[WelcomeMusic] Currently playing music for you, please try again later");
                return;
            }
            if (configData.OnlyFrist && joinData.Players.Contains(player.userID.Get()))
            {
                player.ChatMessage("[WelcomeMusic] You have already played the music.");
                return;
            }
            if (joinData.DisablePlayers.Contains(player.userID.Get()))
            {
                player.ChatMessage("[WelcomeMusic] You have disabled the welcome music, use command /wm to enable.");
                return;
            }
            OnPlayerConnected(player);
        }
        [ChatCommand("wm")]
        void WelcomeMusicCommand(BasePlayer player, string command, string[] args)
        {
            if (!configData.CanDisable)
            {
                player.ChatMessage($"[WelcomeMusic] The server is not allowed to disable welcome music!");
                return;
            }
            if (!joinData.DisablePlayers.Contains(player.userID.Get()))
            {
                joinData.DisablePlayers.Add(player.userID.Get());
                player.ChatMessage($"[WelcomeMusic] You have successfully disabled the welcome music!");
                return;
            }
            else
            {
                joinData.DisablePlayers.Remove(player.userID.Get());
                player.ChatMessage($"[WelcomeMusic] You have successfully enabled the welcome music!");
                return;
            }
        }
        [ChatCommand("musicto")]
        void MusicToCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "welcomemusic.toplayer"))
            {
                player.ChatMessage($"[WelcomeMusic] You not have permission to use this command.");
                return;
            }
            if (args.Length < 3)
            {
                player.ChatMessage($"[WelcomeMusic] Command Usage: /musicto <PlayerID> <MusicURL> <MusicDuration>!");
                return;
            }
            var target = BasePlayer.Find(args[0]);
            if (target == null)
            {
                player.ChatMessage("[WelcomeMusic] No player found.");
                return;
            }
            if (BoomBoxList.ContainsKey(target))
            {
                player.ChatMessage("[WelcomeMusic] Currently playing music for target player, please try again later");
                return;
            }
            if (!args[1].Contains("http"))
            {
                player.ChatMessage("[WelcomeMusic] Please enter a valid URL.");
                return;
            }
            float duration;
            if (!float.TryParse(args[2], out duration))
            {
                player.ChatMessage("[WelcomeMusic] Please enter a valid number.");
                return;
            }
            else
            {
                MusicToPlayer(target, args[1], duration);
                player.ChatMessage($"[WelcomeMusic] Now Playing for Player {target.displayName}: \nMusicURL: {args[1]}\nMusicDuration: {duration}.");
            }
        }
        [ChatCommand("musicall")]
        void MusicToAllCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "welcomemusic.toplayer"))
            {
                player.ChatMessage($"[WelcomeMusic] You not have permission to use this command.");
                return;
            }
            if (args.Length < 2)
            {
                player.ChatMessage($"[WelcomeMusic] Command Usage: /musicall <MusicURL> <MusicDuration>!");
                return;
            }
            if (!args[0].Contains("http"))
            {
                player.ChatMessage("[WelcomeMusic] Please enter a valid URL.");
                return;
            }
            float duration;
            if (!float.TryParse(args[1], out duration))
            {
                player.ChatMessage("[WelcomeMusic] Please enter a valid number.");
                return;
            }
            else
            {
                foreach (var item in BasePlayer.activePlayerList)
                {
                    MusicToPlayer(item, args[0], duration);
                }
                player.ChatMessage($"[WelcomeMusic] Now Playing for all player: \nMusicURL: {args[0]}\nMusicDuration: {duration}.");
            }
        }
        [ConsoleCommand("musicto")]
        void MusicToConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.Args?.Length != 3)
            {
                Puts("Command Usage: musicto <PlayerID> <MusicURL> <MusicDuration>");
                return;
            }

            var argplayer = arg.Player();
            if (arg.Connection != null)
                if (!argplayer.IsAdmin)
                    return;
            var target = BasePlayer.Find(arg.Args[0]);
            if (target == null)
            {
                Puts("No player found.");
                return;
            }
            if (BoomBoxList.ContainsKey(target))
            {
                Puts("Currently playing music for target player, please try again later");
                return;
            }
            if (!arg.Args[1].Contains("http"))
            {
                Puts("Please enter a valid URL.");
                return;
            }
            float duration;
            if (!float.TryParse(arg.Args[2], out duration))
            {
                Puts("Please enter a valid number.");
                return;
            }
            else
            {
                MusicToPlayer(target, arg.Args[1], duration);
                Puts($"Now Playing for Player {target.displayName}: \nMusicURL: {arg.Args[1]}\nMusicDuration: {duration}.");
            }
        }
        [ConsoleCommand("musicall")]
        void MusicToAllConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.Args?.Length != 2)
            {
                Puts("Command Usage: musicall <MusicURL> <MusicDuration>");
                return;
            }
            var argplayer = arg.Player();
            if (arg.Connection != null)
                if (!argplayer.IsAdmin)
                    return;
            if (!arg.Args[0].Contains("http"))
            {
                Puts("Please enter a valid URL.");
                return;
            }
            float duration;
            if (!float.TryParse(arg.Args[1], out duration))
            {
                Puts("Please enter a valid number.");
                return;
            }
            else
            {
                foreach (var item in BasePlayer.activePlayerList)
                {
                    MusicToPlayer(item, arg.Args[0], duration);
                }
                Puts($"Now Playing for all player: \nMusicURL: {arg.Args[0]}\nMusicDuration: {duration}.");
            }
        }
        private void MusicToPlayer(BasePlayer player, string MusicURL, float MusicDuration)
        {
            if (MusicURL == "") return;
            if (BoomBoxList.ContainsKey(player))
            {
                BoomBoxList[player].Kill();
                BoomBoxList.Remove(player);
            }
            if (configData.CanDisable)
            {
                if (joinData.DisablePlayers.Contains(player.userID.Get())) return;
            }
            SphereEntity Sphere = (SphereEntity)GameManager.server.CreateEntity(SpherePrefab, default(Vector3), default(Quaternion), true);
            Sphere.Spawn();
            BoomBoxList.Add(player, Sphere);
            foreach (var mesh in Sphere.GetComponentsInChildren<MeshCollider>())
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            DeployableBoomBox BoomBox = GameManager.server.CreateEntity(BoomboxPrefab, default(Vector3), default(Quaternion), true) as DeployableBoomBox;
            foreach (var mesh in BoomBox.GetComponentsInChildren<MeshCollider>())
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            BoomBox.SetParent(Sphere);
            BoomBox.Spawn();
            BaseCombatEntity CombatEntity = BoomBox.GetComponent<BaseCombatEntity>();
            if (CombatEntity != null)
            {
                CombatEntity.SetMaxHealth(1000000);
                CombatEntity.SetHealth(1000000);
                BoomBox.SendNetworkUpdate();
            }
            BoomBox.pickup.enabled = false;
            BoomBox.BoxController.ServerTogglePlay(false);
            BoomBox.BoxController.AssignedRadioBy = player.OwnerID;
            Sphere.LerpRadiusTo(0.01f, 1f);
            timer.Once(1f, () => {
                if (Sphere != null)
                    Sphere.SetParent(player);
                Sphere.transform.localPosition = new Vector3(0, -2f, 0f);
                BoomBox.BoxController.CurrentRadioIp = MusicURL;
                BoomBox.BoxController.baseEntity.ClientRPC<string>(null, "OnRadioIPChanged", BoomBox.BoxController.CurrentRadioIp);
                BoomBox.BoxController.ServerTogglePlay(true);
                timer.Once(MusicDuration + 1f, () =>
                {
                    try
                    {
                        if (Sphere != null)
                        {
                            Sphere.Kill();
                            BoomBoxList.Remove(player);
                        }
                    }
                    catch { }
                });
            });
            Sphere.SendNetworkUpdateImmediate();
        }
        void Unload()
        {
            SaveData();
            foreach (KeyValuePair<BasePlayer, BaseEntity> BoomBox in BoomBoxList)
            {
                if (BoomBox.Value != null)
                {
                    BoomBox.Value.Kill();
                }
            }
            BoomBoxList.Clear();
        }
    }
}