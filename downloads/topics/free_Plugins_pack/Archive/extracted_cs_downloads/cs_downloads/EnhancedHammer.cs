using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Enhanced Hammer", "Iv Misticos", "2.0.7")]
    [Description("Upgrade your buildings easily with a hammer")]
    public class EnhancedHammer : CovalencePlugin
    {
        #region Variables

        private static EnhancedHammer _ins;

        private List<BasePlayer> _activePlayers = new List<BasePlayer>();

        private readonly int _maskConstruction = LayerMask.GetMask("Construction");

        private const string PermissionUse = "enhancedhammer.use";
        private const string PermissionFree = "enhancedhammer.free";
        private const string PermissionDowngrade = "enhancedhammer.downgrade";
        private const string PermissionGradeHit = "enhancedhammer.grade.hit";
        private const string PermissionGradeClick = "enhancedhammer.grade.click";
        private const string PermissionGradeBuild = "enhancedhammer.grade.build";

        #endregion

        #region Configuration

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] Commands =
            {
                "eh",
                "hammer",
                "up",
                "grade",
                "upgrade"
            };

            [JsonProperty(PropertyName = "Distance From Entity")]
            public float Distance = 5.0f;

            [JsonProperty(PropertyName = "Allow Auto Grade")]
            public bool AutoGrade = true;

            [JsonProperty(PropertyName = "Send Downgrade Disabled Message")]
            public bool DowngradeMessage = true;

            [JsonProperty(PropertyName = "Cancel Default Hammer Hit Behavior")]
            public bool CancelHammerHitDefaultBehavior = true;

            [JsonProperty(PropertyName = "Default Preferences")]
            public PluginData.Preference Preferences = new PluginData.Preference();
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

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();

        #endregion

        #region Work with Data

        private PluginData _data;

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _data);

        private void LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_data == null) _data = new PluginData();
        }

        private class PluginData
        {
            [JsonProperty(PropertyName = "Player Preferences")]
            public Dictionary<string, Preference> Preferences = new Dictionary<string, Preference>();

            [Serializable]
            public class Preference
            {
                [NonSerialized]
                public BasePlayer Player = null;

                [NonSerialized]
                public BuildingGrade.Enum Grade = BuildingGrade.Enum.None;

                [NonSerialized]
                public float GradeStartTime = 0;

                public float DisableIn = 30f;

                public bool AutoGrade = true;

                #region Helpers

                public bool IsEnabled() => Player?.net?.connection != null && !Mathf.Approximately(GradeStartTime, 0) &&
                                           Grade != BuildingGrade.Enum.None;

                public void SetGrade(BuildingGrade.Enum grade)
                {
                    if ((int) grade > (int) BuildingGrade.Enum.TopTier || (int) grade < (int) BuildingGrade.Enum.None)
                        return;

                    if (Grade != grade)
                    {
                        Grade = grade;
                        Player.IPlayer.Message(GetMsg("Grade: Changed", Player.IPlayer.Id)
                            .Replace("{grade}", Grade.ToString()));

                        if (Grade == BuildingGrade.Enum.None && _ins._activePlayers.Contains(Player))
                        {
                            _ins._activePlayers.Remove(Player);
                        }
                        else if (!_ins._activePlayers.Contains(Player))
                        {
                            _ins._activePlayers.Add(Player);
                        }

                        if (_ins._activePlayers.Count == 0)
                        {
                            _ins.Unsubscribe(nameof(_ins.OnPlayerInput));
                        }
                        else
                        {
                            _ins.Subscribe(nameof(_ins.OnPlayerInput));
                        }
                    }

                    UpdateGradeTimer();
                }

                private void UpdateGradeTimer()
                {
                    if (Grade == BuildingGrade.Enum.None)
                        GradeStartTime = 0;
                    else
                        GradeStartTime = Time.realtimeSinceStartup;
                    
                    EnhancedController.Instance.RefreshAvailabilityTime();
                }

                public void DoGrade(BuildingBlock block, bool suppressMessages)
                {
                    UpdateGradeTimer();

                    if (Grade == block.grade)
                        return;

                    if ((int) Grade < (int) block.grade && !Player.IPlayer.HasPermission(PermissionDowngrade))
                    {
                        if (!suppressMessages && _ins._config.DowngradeMessage)
                        {
                            Player.IPlayer.Message(GetMsg("Grade: No Downgrade", Player.IPlayer.Id));
                        }

                        return;
                    }

                    if (Player.IsBuildingBlocked(block.transform.position, block.transform.rotation, block.bounds))
                    {
                        if (!suppressMessages)
                        {
                            Player.IPlayer.Message(GetMsg("Grade: Building Blocked", Player.IPlayer.Id));
                        }

                        return;
                    }

                    if ((int) Grade < 0 || (int) Grade >= block.blockDefinition.grades.Length)
                    {
                        // Invalid grade
                        SetGrade(BuildingGrade.Enum.None);
                        return;
                    }

                    if (block.SecondsSinceAttacked <= 30f)
                    {
                        if (!suppressMessages)
                        {
                            Player.IPlayer.Message(GetMsg("Grade: Recently Damaged", Player.IPlayer.Id));
                        }

                        return;
                    }

                    if (!Player.IPlayer.HasPermission(PermissionFree))
                    {
                        foreach (var item in block.blockDefinition.grades[(int) Grade].costToBuild)
                        {
                            if (Player.inventory.GetAmount(item.itemid) >= item.amount)
                                continue;

                            if (!suppressMessages)
                            {
                                Player.IPlayer.Message(GetMsg("Grade: Insufficient Resources", Player.IPlayer.Id));
                            }

                            return;
                        }

                        var items = Pool.GetList<Item>();
                        foreach (var itemAmount in block.blockDefinition.grades[(int) Grade].costToBuild)
                        {
                            Player.inventory.Take(items, itemAmount.itemid, (int) itemAmount.amount);
                            Player.Command("note.inv", itemAmount.itemid, itemAmount.amount * -1f);
                        }

                        foreach (var item in items)
                        {
                            item.Remove();
                        }

                        ItemManager.DoRemoves();
                        Pool.FreeList(ref items);
                    }

                    block.SetGrade(Grade);
                    block.SetHealthToMax();
                    block.StartBeingRotatable();
                    block.SendNetworkUpdate();
                    block.UpdateSkin();
                    block.ResetUpkeepTime();
                    block.GetBuilding()?.Dirty();

                    var prefab = "assets/bundled/prefabs/fx/build/promote_" + Grade.ToString().ToLower() + ".prefab";
                    if (!StringPool.toNumber.ContainsKey(prefab)) // Ignore if it does not exist so that it does not spam client and/or server side
                        return;

                    Effect.server.Run(prefab, block, 0u, Vector3.zero, Vector3.zero);
                }

                public static Preference Get(string id)
                {
                    Preference preference;
                    if (!_ins._data.Preferences.TryGetValue(id, out preference))
                        _ins._data.Preferences[id] = preference = DeepCopy(_ins._config.Preferences);

                    return preference;
                }

                #endregion
            }
        }

        #endregion

        #region Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Command: No Permission", "You do not have enough permissions for that (enhancedhammer.use)"},
                {"Command: Player Only", "You must be an in-game player to use this command"},
                {"Command: Invalid Grade", "You have entered an invalid grade"},
                {"Command: Timeout", "Current timeout: {timeout}"},
                {"Command: Auto Grade: Enabled", "You have enabled automatic toggle for grading"},
                {"Command: Auto Grade: Disabled", "You have disabled automatic toggle for grading"},
                {"Command: Auto Grade: Force Disabled", "Auto Grade is disabled on this server"},
                {"Command: Syntax", "Grade command syntax:\n" +
                                    "(grade) - Set current grade to a specific value\n" +
                                    "timeout (Time in seconds) - Disable grading in X seconds\n" +
                                    "autograde (True/False) - Toggle automatic grading toggle"},
                {"Grade: Changed", "Current grade: {grade}"},
                {"Grade: No Downgrade", "Downgrading is not allowed on this server"},
                {"Grade: Building Blocked", "You cannot build there"},
                {"Grade: Insufficient Resources", "You cannot afford this upgrade"},
                {"Grade: Recently Damaged", "This entity was recently damaged"}
            }, this);
        }

        private void Init()
        {
            _ins = this;

            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionFree, this);
            permission.RegisterPermission(PermissionDowngrade, this);
            permission.RegisterPermission(PermissionGradeHit, this);
            permission.RegisterPermission(PermissionGradeClick, this);
            permission.RegisterPermission(PermissionGradeBuild, this);
            
            if (!_config.AutoGrade)
                Unsubscribe(nameof(OnStructureUpgrade));
            
            LoadData();

            AddCovalenceCommand(_config.Commands, nameof(CommandGrade));
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerDisconnected(player);
            
            UnityEngine.Object.DestroyImmediate(EnhancedController.Instance);
            
            SaveData();

            _ins = null;
        }

        private void OnServerInitialized()
        {
            new GameObject().AddComponent<EnhancedController>();

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
            
            SaveData();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            PluginData.Preference.Get(player.UserIDString).Player = player;
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            PluginData.Preference.Get(player.UserIDString).SetGrade(BuildingGrade.Enum.None);
        }

        private object OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (!CanUse(player.IPlayer, GradingType.Hit))
                return null;

            var prefs = PluginData.Preference.Get(player.UserIDString);
            if (!prefs.IsEnabled())
                return null;

            var block = info.HitEntity as BuildingBlock;
            if (block == null)
                return null;

            prefs.DoGrade(block, false);
            return _config.CancelHammerHitDefaultBehavior ? false : (object) null;
        }

        private void OnEntityBuilt(HeldEntity planner, GameObject gameObject)
        {
            var player = planner.GetOwnerPlayer();
            if (!CanUse(player.IPlayer, GradingType.Build))
                return;

            var prefs = PluginData.Preference.Get(player.UserIDString);
            if (!prefs.IsEnabled())
                return;
            
            var block = gameObject.ToBaseEntity() as BuildingBlock;
            if (block == null)
                return;
            
            prefs.DoGrade(block, false);
        }

        private object OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (!CanUse(player.IPlayer, GradingType.Use))
                return null;

            var prefs = PluginData.Preference.Get(player.UserIDString);
            if (!prefs.AutoGrade)
                return null;
            
            if (!prefs.IsEnabled() || grade != prefs.Grade)
            {
                // Enable and update grade when disabled or grade is different
                prefs.SetGrade(grade);
            }

            if (prefs.IsEnabled())
            {
                prefs.DoGrade(block, false);
                return true;
            }

            return null;
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!_activePlayers.Contains(player) || !input.IsDown(BUTTON.FIRE_PRIMARY) ||
                !CanUse(player.IPlayer, GradingType.Click) || !(player.GetHeldEntity() is Hammer))
                return;

            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, _ins._config.Distance, _ins._maskConstruction))
                return;

            var block = hit.GetEntity() as BuildingBlock;
            if (block == null)
                return;

            PluginData.Preference.Get(player.UserIDString).DoGrade(block, true);
        }

        private void OnServerSave() => SaveData();

        #endregion
        
        #region Commands

        private void CommandGrade(IPlayer player, string command, string[] args)
        {
            if (!CanUse(player, GradingType.Use))
            {
                player.Reply(GetMsg("Command: No Permission", player.Id));
                return;
            }

            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null)
            {
                player.Reply(GetMsg("Command: Player Only", player.Id));
                return;
            }

            var prefs = PluginData.Preference.Get(player.Id);

            switch (args.Length)
            {
                case 1:
                {
                    BuildingGrade.Enum grade;
                    if (!Enum.TryParse(args[0], true, out grade))
                    {
                        player.Reply(GetMsg("Command: Invalid Grade", player.Id));
                        return;
                    }

                    prefs.SetGrade(grade);
                    return;
                }

                case 2:
                {
                    switch (args[0].ToLower())
                    {
                        case "t":
                        case "timeout":
                        {
                            float timeout;
                            if (!float.TryParse(args[1], out timeout))
                                goto syntax;

                            prefs.DisableIn = timeout;
                            player.Reply(GetMsg("Command: Timeout", player.Id).Replace("{timeout}",
                                timeout.ToString(CultureInfo.CurrentCulture)));
                            
                            return;
                        }

                        case "ag":
                        case "autograde":
                        {
                            if (!_config.AutoGrade)
                            {
                                player.Reply(GetMsg("Command: Auto Grade: Force Disabled", player.Id));
                                break;
                            }
                            
                            bool autograde;
                            if (!bool.TryParse(args[1], out autograde))
                                goto syntax;

                            prefs.AutoGrade = autograde;
                            player.Reply(GetMsg("Command: Auto Grade: " + (prefs.AutoGrade ? "Enabled" : "Disabled"),
                                player.Id));

                            return;
                        }

                        default:
                        {
                            goto syntax;
                        }
                    }

                    return;
                }

                default:
                {
                    goto syntax;
                }
            }

            syntax:
            player.Reply(GetMsg("Command: Syntax", player.Id));
        }
        
        #endregion

        #region Controller

        private class EnhancedController : SingletonComponent<EnhancedController>
        {
            private void UpdateAvailability()
            {
                var nextDisableIn = float.NaN;
                var currentTime = Time.realtimeSinceStartup;
                for (var i = _ins._activePlayers.Count - 1; i >= 0; i--)
                {
                    var player = _ins._activePlayers[i];
                    var prefs = PluginData.Preference.Get(player.UserIDString);
                    var disableIn = prefs.GradeStartTime + prefs.DisableIn - currentTime;
                    if (disableIn > 0 && (float.IsNaN(nextDisableIn) || nextDisableIn > disableIn))
                        nextDisableIn = disableIn;

                    if (!Mathf.Approximately(disableIn, 0) && disableIn > 0)
                        continue;

                    prefs.SetGrade(BuildingGrade.Enum.None);
                }

                if (float.IsNaN(nextDisableIn))
                    return;
                
                RefreshAvailabilityTime(nextDisableIn);
            }

            private void RefreshAvailabilityTime(float nextTime)
            {
                if (IsInvoking(UpdateAvailability))
                    CancelInvoke(UpdateAvailability);
                
                Invoke(UpdateAvailability, nextTime);
            }

            public void RefreshAvailabilityTime()
            {
                var nextDisableIn = float.NaN;
                var currentTime = Time.realtimeSinceStartup;
                for (var i = 0; i < _ins._activePlayers.Count; i++)
                {
                    var player = _ins._activePlayers[i];
                    var prefs = PluginData.Preference.Get(player.UserIDString);
                    var disableIn = prefs.GradeStartTime + prefs.DisableIn - currentTime;
                    if (disableIn > 0 && (float.IsNaN(nextDisableIn) || nextDisableIn > disableIn))
                        nextDisableIn = disableIn;
                }

                RefreshAvailabilityTime(nextDisableIn);
            }
        }

        #endregion

        #region Helpers

        private enum GradingType : byte
        {
            Use,
            Hit,
            Click,
            Build
        }

        private static bool CanUse(IPlayer player, GradingType type)
        {
            if (!player.HasPermission(PermissionUse))
                return false;

            switch (type)
            {
                case GradingType.Hit:
                {
                    return player.HasPermission(PermissionGradeHit);
                }

                case GradingType.Click:
                {
                    return player.HasPermission(PermissionGradeClick);
                }

                case GradingType.Build:
                {
                    return player.HasPermission(PermissionGradeBuild);
                }

                default:
                {
                    return true;
                }
            }
        }

        private static string GetMsg(string key, string userId = null) => _ins.lang.GetMessage(key, _ins, userId);

        private static T DeepCopy<T>(T other)
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();

                formatter.Serialize(ms, other);
                ms.Position = 0;

                return (T) formatter.Deserialize(ms);
            }
        }

        #endregion
    }
}