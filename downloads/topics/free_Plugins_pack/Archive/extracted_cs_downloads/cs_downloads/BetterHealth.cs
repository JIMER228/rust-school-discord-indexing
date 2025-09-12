//Requires: Coroutines

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Facepunch.Extend;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Better Health", "birthdates & FourTeen", "2.2.3")]
    [Description("Ability to customize the max health")]
    public class BetterHealth : RustPlugin
    {
        #region Variables
        public Dictionary<ulong, bool> PlayerData = new Dictionary<ulong, bool>();
        public Dictionary<ulong, float> PlayerLevelData = new Dictionary<ulong, float>();
        private const string PermissionUse = "betterhealth.use";
        private ConfigFile cfg;
        #endregion

        #region Helpersa
        void ChangeLvl(BasePlayer player)
        {
            PlayerLevelData[player.userID] = GetMaxHealth(player, true);
            player.modifiers.RemoveAll();
            player.modifiers.SendChangesToClient();
            timer.Once(0.5f, () => SetHealth(player, true));
        }
        /// <summary>
        ///     Get the player's max health based on config permissions
        /// </summary>
        /// <param name="player">Target player</param>
        /// <returns>The default max health or the highest max health they have permission to</returns>
        private float GetMaxHealth(BasePlayer player, bool b = false)
        {
            //filter permissions & sort
            if (b)
            {
                float maxHealth = 0.0f;
                string maxPerm = null;

                foreach (var perm in _config.Permissions)
                {
                    if (permission.UserHasPermission(player.UserIDString, $"betterhealth.{perm.Key}") && perm.Value > maxHealth)
                    {
                        maxHealth = perm.Value;
                        maxPerm = perm.Key;
                    }
                }
                return maxHealth;
            }
            if (!PlayerData.ContainsKey(player.userID.Get()))
            {
                var healthPermission = _config.Permissions.Where(p => player.IPlayer.HasPermission($"betterhealth.{p.Key}"))
                .OrderByDescending(entry => entry.Value).FirstOrDefault();
                return string.IsNullOrEmpty(healthPermission.Key) ? _config.MaxHealth : healthPermission.Value;
            }
            else
            {
                if (!PlayerLevelData.ContainsKey(player.userID.Get()))
                {
                    float maxHealth = 0.0f;
                    string maxPerm = null;

                    foreach (var perm in _config.Permissions)
                    {
                        if (permission.UserHasPermission(player.UserIDString, $"betterhealth.{perm.Key}") && perm.Value > maxHealth)
                        {
                            maxHealth = perm.Value;
                            maxPerm = perm.Key;
                        }
                    }
                    return maxHealth;
                }
                return PlayerLevelData[player.userID.Get()];
            }
        }
        [ConsoleCommand("bhealthi")]
        void CommandBHealthi(ConsoleSystem.Arg args)
        {
            if (args.Player() != null)
            {
                return;
            }

            BasePlayer player = BasePlayer.FindByID(ulong.Parse(args.Args[0]));

            if (player == null)
            {
                PrintError($"Игрок не найден!");
                return;
            }

            float maxHealth = 0.0f;
            string maxPerm = null;

            foreach (var perm in _config.Permissions)
            {
                if (permission.UserHasPermission(player.UserIDString, $"betterhealth.{perm.Key}") && perm.Value > maxHealth)
                {
                    maxHealth = perm.Value;
                    maxPerm = perm.Key;
                }
            }

            if (maxPerm != null)
            {
                string nextPerm = null;
                bool foundCurrent = false;

                foreach (var perm in _config.Permissions)
                {
                    if (foundCurrent)
                    {
                        if (maxHealth >= 100000)
                        {
                            player.ChatMessage($"Приобрести более <color=#8dba43>100000 хп</color> через магазин невозможно. Пожалуйста, свяжитесь с <color=#674ea7>создателем</color> лично\n[DS = <color=#674ea7>tima9667</color>, VK = <color=#674ea7>vk.com/gorgonarust</color>]");
                            PrintWarning($"{player} Попытка купить больше х100 урона.");
                            return;
                        }
                        nextPerm = perm.Key;
                        break;
                    }

                    if (perm.Key == maxPerm)
                    {
                        foundCurrent = true;
                    }
                }

                if (nextPerm != null)
                {
                    rust.RunServerCommand($"grantperm {player.UserIDString} betterhealth.{nextPerm} 999d");
                    Match match = Regex.Match(nextPerm, @"\d+");
                    if (int.TryParse(match.Value, out int number))
                    {
                        player.ChatMessage($"Успешно получено дополнительное здоровье {number}");
                    }
                }
                else
                {
                    PrintError("Не удалось найти следующий уровень разрешения.");
                }
            }
            else
            {
                player.ChatMessage($"Успешно получено дополнительное здоровье 200");
                rust.RunServerCommand($"grantperm {player.UserIDString} betterhealth.200 999d");
                rust.RunServerCommand($"grantperm {player.UserIDString} betterhealth.use 999d");
            }
        }
        [ChatCommand("hp")]
        void BHealthCMD(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                if (!PlayerData.ContainsKey(player.userID))
                    PlayerData.Add(player.userID, true);
                else
                    PlayerData[player.userID] = true;
                if (args.Length >= 1)
                {
                    float requestedHealth = args[0].ToFloat();
                    float maxHealth = 0.0f;
                    string maxPerm = null;
                    foreach (var perm in _config.Permissions)
                    {
                        if (permission.UserHasPermission(player.UserIDString, $"betterhealth.{perm.Key}"))
                        {
                            if (perm.Value > maxHealth)
                            {
                                maxHealth = perm.Value;
                                maxPerm = perm.Key;
                            }
                        }
                    }
                    timer.Once(0.5f, () =>
                    {
                        if (requestedHealth <= maxHealth)
                        {
                            if (PlayerLevelData.ContainsKey(player.userID))
                            {
                                PlayerLevelData[player.userID] = requestedHealth;
                            }
                            else
                            {
                                PlayerLevelData.Add(player.userID, requestedHealth);
                            }
                            player.modifiers.RemoveAll();
                            player.modifiers.SendChangesToClient();
                            timer.Once(0.5f, () => SetHealth(player, true));

                            player.ChatMessage($"Успешно: {requestedHealth}/{maxHealth}");
                        }
                        else
                        {
                            player.ChatMessage($"Недоступно. Максимальное кол-во хп: {maxHealth}");
                        }
                    });
                }
                else
                {
                    player.ChatMessage("/hp <кол-во>");
                }
            }
            else
            {
                player.ChatMessage("У вас нет разрешения использовать эту команду.");
            }
        }

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PermissionUse, this);
            LoadConfig();

            foreach (var perm in _config.Permissions.Keys.Select(p => $"betterhealth.{p}")
                .Where(perm => !permission.PermissionExists(perm, this))) permission.RegisterPermission(perm, this);
        }

        private void OnServerInitialized()
        {
            CheckAllPlayers();
            StartChecking();
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            SetHealth(player);
            NextTick(() => { player.Heal(10000000); });
        }

        /// <summary>
        ///     On consume of tea, if add booster is on, add it on to the current booster, if not, cancel
        /// </summary>
        /// <param name="item">Target item</param>
        /// <param name="action">Item action</param>
        /// <param name="player">Target player</param>
        /// <returns>Null if we should not cancel, true if we should cancel</returns>
        private object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (!action.Equals("consume") || !item.info.shortname.StartsWith("maxhealthtea")) return null;
            /*if (!_config.AddOldBooster)*/
            return true; //stop the tea from being consumed

            /*
             * Use next tick to wait for tea benefits
             * Set the health with the new booster
             */
            NextTick(() => SetHealth(player));
            return null;
        }

        #endregion

        #region Health

        /// <summary>
        ///     Check all players for health changes in a coroutine
        /// </summary>
        private void CheckAllPlayers()
        {
            const string id = "Check All Players";
            if (Coroutines.Instance.IsCoroutineRunning(id)) return;
            Coroutines.Instance.LoopListAsynchronously(this, new Action<BasePlayer>(SetHealthWrapper),
                new List<BasePlayer>(BasePlayer.allPlayerList), 0.2f, id: id, reverse: true, completePerTick: 5);
        }
        private void SetHealthWrapper(BasePlayer player)
        {
            SetHealth(player);
        }
        /// <summary>
        ///     Start a timer every 40 minutes to reset the boost timer
        /// </summary>
        private void StartChecking()
        {
            timer.Every(2400f /*40 minutes*/, CheckAllPlayers);
        }

        /// <summary>
        ///     Set the max health of a player
        /// </summary>
        /// <param name="player">Target player</param>
        private void SetHealth(BasePlayer player, bool ignoreperm = false)
        {
            if (player == null || player.modifiers == null || (!ignoreperm && !player.IPlayer.HasPermission(PermissionUse))) return;
            var startHealth = player.StartMaxHealth();
            var maxHealth = GetMaxHealth(player);
            var mh = GetMaxHealth(player, true);
            if (maxHealth > mh)
            {
                PlayerLevelData[player.userID.Get()] = mh;
                maxHealth = mh;
            }
            if (maxHealth <= 100 || maxHealth == 150) return;
            var healthMultiplier = (maxHealth - startHealth) /
                                   startHealth; //get multiplier needed (i.e 150 max health should be 0.5)

            //add old booster on to multiplier if exists & not the same booster
            float healthBooster;
            if (_config.AddOldBooster &&
                (healthBooster = player.modifiers.GetValue(Modifier.ModifierType.Max_Health, -1000f)) > -1000f &&
                Math.Abs(healthBooster - healthMultiplier) > 0.1f) healthMultiplier += healthBooster;

            player.modifiers.Add(new List<ModifierDefintion>
            {
                new ModifierDefintion
                {
                    type = Modifier.ModifierType.Max_Health,
                    value = healthMultiplier, //the equation is startHealth * (1f + modifier)
                    duration = 999999f, //don't use float.MaxValue (will kick player) (max time seems to be ~45 minutes)
                    source = Modifier.ModifierSource.Tea
                }
            });
            player.modifiers.SendChangesToClient(); //update client
        }
        private void OnUserPermissionGranted(string id, string permName)
        {
            if (permName.StartsWith("betterhealth") && permName != PermissionUse)
            {
                var player = BasePlayer.FindByID((ulong)id.ToLong());
                if (player != null)
                {
                    player.modifiers.RemoveAll();
                    player.modifiers.SendChangesToClient();
                    NextTick(() => SetHealth(player));
                    NextTick(() => NextTick(() => { player.Heal(10000000); }));
                }
            }
        }
        private void OnUserPermissionRevoked(string id, string permName)
        {
            if (permName.StartsWith("betterhealth") && permName != PermissionUse)
            {
                var player = BasePlayer.FindByID((ulong)id.ToLong());
                if (player != null)
                {
                    player.modifiers.RemoveAll();
                    player.modifiers.SendChangesToClient();
                    NextTick(() => SetHealth(player, true));
                }
            }
        }
        #endregion

        #region Configuration, Language & Data

        private ConfigFile _config;

        public class ConfigFile
        {
            [JsonProperty("Add booster if already has one?")]
            public bool AddOldBooster = true;

            [JsonProperty("Default Max Health")] public float MaxHealth = 150f;

            [JsonProperty("Max Health Permissions")]
            public Dictionary<string, float> Permissions = new Dictionary<string, float>
            {
                {"vip", 300f}
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
            if (_config == null) LoadDefaultConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = new ConfigFile();
            PrintWarning("Default configuration has been loaded.");
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }
        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Title}/PlayerData", PlayerData);
            Interface.Oxide.DataFileSystem.WriteObject($"{Title}/PlayerLevelData", PlayerLevelData);
        }
        private void LoadData()
        {
            try
            {
                PlayerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, bool>>($"{Title}/PlayerData");
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }
            if (PlayerData == null) PlayerData = new Dictionary<ulong, bool>();
            try
            {
                PlayerLevelData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, float>>($"{Title}/PlayerLevelData");
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }
            if (PlayerLevelData == null) PlayerLevelData = new Dictionary<ulong, float>();
        }

        #endregion
    }
}
//Generated with birthdates' Plugin Maker