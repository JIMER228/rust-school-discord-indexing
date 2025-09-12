using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;
using static Oxide.Plugins.RocketFire;

namespace Oxide.Plugins
{
    [Info("Rocket Fire", "birthdates", "1.0.4")]
    [Description("Ability to fire x amount of rockets at once")]
    public class RocketFire : RustPlugin
    {
        #region Variables
        private const string Perm = "rocketfire.use";
        private Dictionary<ulong, float> cooldown = new Dictionary<ulong, float>();
        #endregion

        #region Hooks
        private void Init()
        {
            permission.RegisterPermission(Perm, this);
            foreach (var kvp in _config.permissions)
            {
                var permissionName = kvp.Key;
                permission.RegisterPermission(permissionName, this);
            }
            LoadConfig();
        }
        private void SetCooldown(BasePlayer player, float cd)
        {
            if (player == null) return;
            ulong userid = player.userID;
            cooldown[userid] = Time.time + cd;
            timer.Once(cd, () => cooldown.Remove(userid));
        }
        private float GetGlobalCooldown(BasePlayer player)
        {
            if (player == null) return 0f;
            float cd;
            if (!cooldown.TryGetValue(player.userID.Get(), out cd))
            {
                return 0f;
            }

            return cd - Time.time;
        }
        [ConsoleCommand("fr")]
        private void FireRocketsConsoleCMD(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
            {
                player.ChatMessage(lang.GetMessage("PlayerOnlyCommand", this, player.UserIDString));
                return;
            }
            FRCommand(player, arg.Args);
        }

        [ChatCommand("fr")]
        private void FireRocketsCMD(BasePlayer player, string command, string[] args) => FRCommand(player, args);

        private void FRCommand(BasePlayer player, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, Perm) && !player.IsAdmin)
            {
                SendReply(player, lang.GetMessage("NoPermission", this, player.UserIDString));
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, lang.GetMessage("InvalidArgs", this, player.UserIDString));
            }
            else
            {
                string maxPermission = null;
                int maxPermissionNumber = -1;

                foreach (var kvp in _config.permissions)
                {
                    var permissionName = kvp.Key;
                    var permissionSettings = kvp.Value;

                    if (permission.UserHasPermission(player.UserIDString, permissionName))
                    {
                        var match = Regex.Match(permissionName, @"\d+$");
                        if (match.Success)
                        {
                            int permissionNumber = int.Parse(match.Value);
                            if (permissionNumber > maxPermissionNumber)
                            {
                                maxPermission = permissionName;
                                maxPermissionNumber = permissionNumber;
                            }
                        }
                    }
                }

                int amount;
                if (!int.TryParse(args[0], out amount))
                {
                    SendReply(player, lang.GetMessage("InvalidNumber", this, player.UserIDString));
                    return;
                }

                if (maxPermission != null)
                {
                    float cd = GetGlobalCooldown(player);
                    if (cd != 0)
                    {
                        player.ChatMessage($"Вы уже использовали эту команду. Используйте её снова через <color=#FF9740>{cd.ToString("0")}</color> секунд.");
                        return;
                    }

                    var permissionSettings = _config.permissions[maxPermission];

                    if (amount > permissionSettings.MaxRockets)
                    {
                        SendReply(player, string.Format(lang.GetMessage("TooManyRockets", this, player.UserIDString), permissionSettings.MaxRockets));
                        return;
                    }
                    if (!int.TryParse(args[0], out amount))
                    {
                        SendReply(player, lang.GetMessage("InvalidNumber", this, player.UserIDString));
                    }
                    else
                    {

                        if (amount > _config.maxRockets)
                        {
                            SendReply(player, string.Format(lang.GetMessage("TooManyRockets", this, player.UserIDString), _config.maxRockets));
                            return;
                        }
                        if (!StringPool.toString.ContainsValue(
                            $"assets/prefabs/ammo/rocket/{_config.rocketType}.prefab"))
                        {
                            SendReply(player, lang.GetMessage("InvalidPrefab", this, player.UserIDString));
                            return;
                        }

                        var pos = player.eyes.position;
                        var forward = player.eyes.HeadForward();
                        var rot = player.transform.rotation;
                        var aim = player.serverInput.current.aimAngles;
                        var staticPos = _config.staticRockets ? pos + forward : default(Vector3);
                        timer.Repeat(_config.delay, amount, delegate
                        {

                            var rocket = GameManager.server.CreateEntity($"assets/prefabs/ammo/rocket/{_config.rocketType}.prefab",
                               _config.staticRockets ? staticPos : player.eyes.position + player.eyes.HeadForward(), _config.staticRockets ? rot : player.transform.rotation);
                            if (rocket == null) return;
                            var proj = rocket.GetComponent<ServerProjectile>();
                            if (proj == null) return;
                            proj.InitializeVelocity(Quaternion.Euler(_config.staticRockets ? aim : player.serverInput.current.aimAngles) * rocket.transform.forward * _config.velocity);

                            rocket.Spawn();
                        });
                        SendReply(player, string.Format(lang.GetMessage("RocketsFired", this, player.UserIDString), amount));
                        SetCooldown(player, permissionSettings.CoolDown);
                    }
                }
                else
                {
                    if (amount > _config.maxRockets)
                    {
                        SendReply(player, string.Format(lang.GetMessage("TooManyRockets", this, player.UserIDString), _config.maxRockets));
                        return;
                    }
                }
                if (!int.TryParse(args[0], out amount))
                {
                    SendReply(player, lang.GetMessage("InvalidNumber", this, player.UserIDString));
                }
                else
                {

                    if (amount > _config.maxRockets)
                    {
                        SendReply(player, string.Format(lang.GetMessage("TooManyRockets", this, player.UserIDString), _config.maxRockets));
                        return;
                    }
                    if (!StringPool.toString.ContainsValue(
                        $"assets/prefabs/ammo/rocket/{_config.rocketType}.prefab"))
                    {
                        SendReply(player, lang.GetMessage("InvalidPrefab", this, player.UserIDString));
                        return;
                    }

                    var pos = player.eyes.position;
                    var forward = player.eyes.HeadForward();
                    var rot = player.transform.rotation;
                    var aim = player.serverInput.current.aimAngles;
                    var staticPos = _config.staticRockets ? pos + forward : default(Vector3);
                    timer.Repeat(_config.delay, amount, delegate
                    {

                        var rocket = GameManager.server.CreateEntity($"assets/prefabs/ammo/rocket/{_config.rocketType}.prefab",
                           _config.staticRockets ? staticPos : player.eyes.position + player.eyes.HeadForward(), _config.staticRockets ? rot : player.transform.rotation);
                        if (rocket == null) return;
                        var proj = rocket.GetComponent<ServerProjectile>();
                        if (proj == null) return;
                        proj.InitializeVelocity(Quaternion.Euler(_config.staticRockets ? aim : player.serverInput.current.aimAngles) * rocket.transform.forward * _config.velocity);

                        rocket.Spawn();
                    });
                    SendReply(player, string.Format(lang.GetMessage("RocketsFired", this, player.UserIDString), amount));
                }
            }
        }

        #endregion

        #region Configuration & Language
        public ConfigFile _config;

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"TooManyRockets", "You have attempted to fire too many rockets, you can only fire {0} at once!"},
                {"RocketsFired", "You have launched {0} rockets"},
                {"NoPermission", "You have no permission!"},
                {"InvalidArgs", "/fr <amount of rockets to fire>"},
                {"InvalidNumber", "That is not a valid number!"},
                {"InvalidPrefab", "Please fix the rocket type in the configuration file, that is not a valid rocket type!"},
                {"PlayerOnlyCommand", "This player is only accesible by players."}
            }, this);
        }

        public class ConfigFile
        {
            [JsonProperty("Max amount of rockets to fire at once")]
            public int maxRockets;
            [JsonProperty("Delay in between multiple rocket shots (e.g /fr 10)")]
            public float delay;
            [JsonProperty("Rockets fire at the same position if you move when you shoot multiple (e.g /fr 10)")]
            public bool staticRockets;
            [JsonProperty("The rocket velocity")]
            public float velocity;
            [JsonProperty("The rocket type")]
            public string rocketType;
            [JsonProperty("Permission settings")]
            public Dictionary<string, PermissionSettings> permissions;
            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile()
                {
                    maxRockets = 100,
                    delay = 0.2f,
                    staticRockets = false,
                    velocity = 22,
                    rocketType = "rocket_basic",
                    permissions = new Dictionary<string, PermissionSettings>()
                    {
                        ["rocketfire.perm1"] = new PermissionSettings() { MaxRockets = 5, CoolDown = 15 }
                    }
                };
            }
            
        }
        public class PermissionSettings
        {
            [JsonProperty("Максимум ракет")]
            public int MaxRockets { get; set; }
            [JsonProperty("Кд на команнду (сек)")]
            public float CoolDown { get; set; }
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
            if (_config == null)
            {
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = ConfigFile.DefaultConfig();
            PrintWarning("Default configuration has been loaded.");
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }
        #endregion
    }
}
//Generated with birthdates' Plugin Maker