using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;
using UnityEngine.Networking;

namespace Oxide.Plugins
{
    [Info("RTM", "AhigaO#4485", "1.3.4")]
    internal class RTM : RustPlugin
    {
        #region Static

        private const string Layer = "UI_RTM";
        private Data _data;
        private Configuration _config;
        private bool CanRaidNow = false;
        private bool Update = true;
        private Timer DiscordTimer;
        private List<ulong> AlertCooldown = new List<ulong>();
        private List<ulong> ClosedUI = new List<ulong>();
        private Dictionary<ulong, DateTime> LeaveTime = new Dictionary<ulong, DateTime>();
        private string[] DaysOfWeek = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
        private List<string> UIPositions = new List<string> { "UPPER_LEFT", "UPPER_CENTER", "UPPER_RIGHT", "LOWER_CENTER", "LOWER_RIGHT", "LOWER_CENTER_LITE" };
        private List<string> ZoneManagerSettings = new List<string> { "DISABLED", "TRUE", "FALSE" };

        #region Image

        [PluginReference] private Plugin ImageLibrary, RaidableBases, TruePVE, ZoneManager;
        private int ILCheck = 0;
        private Dictionary<string, string> Images = new Dictionary<string, string>();

        private void AddImage(string url)
        {
            if (!ImageLibrary.Call<bool>("HasImage", url)) ImageLibrary.Call("AddImage", url, url);
            timer.In(1f, () => { Images.Add(url, ImageLibrary.Call<string>("GetImage", url)); });
        }

        private string GetImage(string url)
        {
            return Images[url];
        }

        private void LoadImages()
        {
            AddImage("https://i.imgur.com/dvmwW5c.png");
            AddImage("https://i.imgur.com/afJY7cT.png");
            AddImage("https://i.imgur.com/pO6eVOK.png");
            AddImage("https://i.imgur.com/nPqbl6m.png");
            AddImage("https://i.imgur.com/Qo14QIG.png");
            AddImage("https://i.imgur.com/mwbQmBO.png");
        }

        #endregion

        #region Classes

        private class Data
        {
            public Dictionary<string, DateTime> NPP = new Dictionary<string, DateTime>();
        }

        private class RaidBlockTime
        {
            [JsonProperty(PropertyName = "Start Raid Block(Hours)")]
            public int StartHours;

            [JsonProperty(PropertyName = "Start Raid Block(Minutes)")]
            public int StartMinutes;

            [JsonProperty(PropertyName = "Stop Raid Block(Hours)")]
            public int StopHours;

            [JsonProperty(PropertyName = "Stop Raid Block(Minutes)")]
            public int StopMinutes;
        }

        private class RaidBlockDay
        {
            [JsonProperty(PropertyName = "Year")] public int Year;

            [JsonProperty(PropertyName = "Month")] public int Month;

            [JsonProperty(PropertyName = "Day")] public int Day;

            public static RaidBlockDay GetFromString(string input)
            {
                var result = input.Split('/');
                return new RaidBlockDay { Month = int.Parse(result[0]), Day = int.Parse(result[1]), Year = int.Parse(result[2]) };
            }

            public override string ToString() => $"{Month}/{Day}/{Year}";
        }

        private class Configuration
        {
            [JsonProperty(PropertyName = "Command for open Raid Time Managment UI")]
            public string Command = "rtm";

            [JsonProperty(PropertyName = "Timezone(Difference between UTC and your time)")]
            public int TimeZone = 60;

            [JsonProperty(PropertyName = "Time format for UI(True - 24H format, False - 12H format)")]
            public bool TimeFormat = false;

            [JsonProperty(PropertyName = "Display UI")]
            public bool DisplayUI = true;

            [JsonProperty(PropertyName = "Enable Alerts")]
            public bool Alerts = true;
            
            [JsonProperty(PropertyName = "Discord webhook [empty = not use]")]
            public string DiscordWebHook = "";  
            
            [JsonProperty(PropertyName = "Time before the start of the raid in minutes when to send a discord notification")]
            public int DiscordMessageTime = 120;

            [JsonProperty(PropertyName = "Allow players to close the UI")]
            public bool AllowToCloseUI = true;

            [JsonProperty(PropertyName = "[RaidableBases] Allow raiding RB bases in SafeTime")]
            public bool RaidRBInSafeTime = true;

            [JsonProperty(PropertyName = "[TruePVE] Safe Time RuleSet")]
            public string SafeTimeRuleSet = "default";

            [JsonProperty(PropertyName = "[TruePVE] Raid Time RuleSet")]
            public string RaidTimeRuleSet = "default";

            [JsonProperty(PropertyName = "[ZoneManager] The plugin will work on entities in the zones")]
            public string ZoneManagerSettings = "DISABLED";

            [JsonProperty(PropertyName = "[ZoneManager] Zone ID List", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> ZoneIDs = new List<string>();

            [JsonProperty(PropertyName = "UI Position")]
            public string UIPosition = "UPPER_LEFT";

            [JsonProperty(PropertyName = "NPP amount")]
            public int NPP = 0;

            [JsonProperty(PropertyName = "Block only offline raids")]
            public bool BlockOnlyOffline = false;

            [JsonProperty(PropertyName = "Allow bots raids in any time")]
            public bool BotsRaids = false;

            [JsonProperty(PropertyName = "Offline defence activation time")]
            public int OfflineDefenceTimer = 3600;

            [JsonProperty(PropertyName = "Add safe days after wipe")]
            public int AdditionalSafeHours = 1;

            [JsonProperty(PropertyName = "Commands to be executed when raid time starts", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> CommandOnRaidStarted = new List<string>();
            
            [JsonProperty(PropertyName = "Commands to be executed when safe time starts", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> CommandOnSafeStarted = new List<string>();

            [JsonProperty(PropertyName = "Forbidden actions during the blocking of the raid")]
            public Dictionary<string, bool> RaidBlockSettings = new Dictionary<string, bool>
            {
                ["Default Raid By Player"] = true,
                ["Damage Own Buildings"] = false,
                ["Raid By Patrol Helicopter"] = true,
                ["Raid By MLRS"] = true,
                ["Using Ladders In Building Block"] = true,
                ["Damage By Fire"] = true,
                ["Build Ceiling In Building Block"] = true,
                ["Build Turrets In 2x Building Block Radius"] = true,
                ["Break down twig buildings"] = false,
            };

            [JsonProperty(PropertyName = "List of prefabs that can always be damaged", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> AlwaysDamaged = new List<string>();

            [JsonProperty(PropertyName = "Manage raids by day of the week (Used by default)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, List<RaidBlockTime>> BlockByWeekDay = new Dictionary<string, List<RaidBlockTime>>
            {
                ["Monday"] = new List<RaidBlockTime>
                {
                    new RaidBlockTime
                    {
                        StartHours = 18,
                        StartMinutes = 0,
                        StopHours = 22,
                        StopMinutes = 0
                    }
                },
                ["Tuesday"] = new List<RaidBlockTime>
                {
                    new RaidBlockTime
                    {
                        StartHours = 18,
                        StartMinutes = 0,
                        StopHours = 22,
                        StopMinutes = 0
                    }
                },
                ["Wednesday"] = new List<RaidBlockTime>
                {
                    new RaidBlockTime
                    {
                        StartHours = 18,
                        StartMinutes = 0,
                        StopHours = 22,
                        StopMinutes = 0
                    }
                },
                ["Thursday"] = new List<RaidBlockTime>
                {
                    new RaidBlockTime
                    {
                        StartHours = 18,
                        StartMinutes = 0,
                        StopHours = 22,
                        StopMinutes = 0
                    }
                },
                ["Friday"] = new List<RaidBlockTime>
                {
                    new RaidBlockTime
                    {
                        StartHours = 18,
                        StartMinutes = 0,
                        StopHours = 22,
                        StopMinutes = 0
                    }
                },
                ["Saturday"] = new List<RaidBlockTime>
                {
                    new RaidBlockTime
                    {
                        StartHours = 10,
                        StartMinutes = 0,
                        StopHours = 22,
                        StopMinutes = 0
                    }
                },
                ["Sunday"] = new List<RaidBlockTime>
                {
                    new RaidBlockTime
                    {
                        StartHours = 8,
                        StartMinutes = 0,
                        StopHours = 22,
                        StopMinutes = 0
                    }
                }
            };

            [JsonProperty(PropertyName = "Raid management for specific days(Takes precedence over raid management by day of the week)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, List<RaidBlockTime>> BlockByDay = new Dictionary<string, List<RaidBlockTime>>();
        }

        #endregion

        #endregion

        #region Config

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
        
        #region Data

        private void LoadData() => _data = Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/data") ? Interface.Oxide.DataFileSystem.ReadObject<Data>($"{Name}/data") : new Data();
        private void OnServerSave() => SaveData();

        private void SaveData()
        {
            if (_data != null) Interface.Oxide.DataFileSystem.WriteObject($"{Name}/data", _data);
        }
        
        #endregion

        #region OxideHooks

        private void OnRaidableBaseStarted(Vector3 raidPos, int mode, bool allowPVP, string id, float spawnTime, float despawnTime, float loadTime, ulong ownerId, BasePlayer owner, List<BasePlayer> raiders, List<BasePlayer> intruders, List<BaseEntity> entities)
        {
            foreach (var check in entities)
            {
                if (check == null) continue;
                check._name = "ITRBBASE";
            }
        }

        private void OnEntityTakeDamage(DecayEntity entity, HitInfo info)
        {
            if (entity == null || info == null || entity._name == "ITRBBASE" || _config.AlwaysDamaged.Contains(entity.PrefabName) || !entity.OwnerID.IsSteamId()) return;
            var player = info.InitiatorPlayer;
            if ((CanRaidNow || player != null && _config.BotsRaids && !player.userID.IsSteamId()) && !PlayerHasNPP(entity.OwnerID))
                return;

            if (!InCorrectZone(entity) || CanRaidPlayer(entity)) return;

            if (player != null)
            {
                var team = player.Team;
                var tc = entity.GetBuildingPrivilege();
                if ((tc != null && tc.IsAuthed(player)) || (player.userID == entity.OwnerID || (team != null && team.members.Contains(entity.OwnerID))))
                {
                    if (_config.RaidBlockSettings["Damage Own Buildings"])
                    {
                        ShowUIAlert(player, GetMsg(player?.UserIDString, "UI_CANDAMAGEOWN"));
                        info.damageTypes.ScaleAll(0);
                        return;
                    }

                    return;
                }

                if (entity is BuildingBlock)
                {
                    if ((entity as BuildingBlock).grade == BuildingGrade.Enum.Twigs)
                    {
                        if (!_config.RaidBlockSettings["Break down twig buildings"]) return;
                        info?.damageTypes?.ScaleAll(0);
                        return;
                    }
                }

                if (_config.RaidBlockSettings["Default Raid By Player"])
                {
                    info.damageTypes.ScaleAll(0);
                    ShowUIAlert(player, GetMsg(player?.UserIDString, "UI_CANDAMAGE"));
                }

                return;
            }

            if (_config.RaidBlockSettings["Raid By Patrol Helicopter"] && InitiatorIsHelicopter(info))
            {
                info.damageTypes.ScaleAll(0);
                return;
            }

            if (_config.RaidBlockSettings["Raid By MLRS"] && info.WeaponPrefab != null && info.WeaponPrefab.ShortPrefabName == "rocket_mlrs")
            {
                info.damageTypes.ScaleAll(0);
                return;
            }

            if (_config.RaidBlockSettings["Damage By Fire"] && info.damageTypes.Has(DamageType.Heat)) info.damageTypes.Scale(DamageType.Heat, 0);
        }

        private bool PlayerHasNPP(ulong entityOwnerID, bool checkTeam = true)
        {
            if (_config.NPP == 0)
                return false;
            
            DateTime time;
            if (!_data.NPP.TryGetValue(entityOwnerID.ToString(), out time))
                return false;
            
            if (time.Subtract(DateTime.Now).TotalSeconds < 0)
                return false;

            if (!checkTeam)
                return true;
            
            var team = RelationshipManager.ServerInstance.FindPlayersTeam(entityOwnerID);
            if (team == null)
                return true;

            foreach (var check in team.members)
                if (!PlayerHasNPP(check, false))
                    return false;

            return true;
        }

        private object CanBuild(Planner bplan, Construction prefab, Construction.Target target)
        {
            var player = bplan?.GetOwnerPlayer();
            if (player == null || !InCorrectZone(player)) return null;
            var tc = player.GetBuildingPrivilege();
            if (tc == null || tc.IsAuthed(player)) return null;
            switch (prefab.fullName)
            {
                case "assets/prefabs/building/ladder.wall.wood/ladder.wooden.wall.prefab":
                    if (!_config.RaidBlockSettings["Using Ladders In Building Block"]) return null;
                    ShowUIAlert(player, GetMsg(player?.UserIDString, "UI_LADDERBLOCK"));
                    return false;
                case "assets/prefabs/building core/floor/floor.prefab":
                case "assets/prefabs/building core/floor.triangle/floor.triangle.prefab":
                case "assets/prefabs/building core/floor.frame/floor.frame.prefab":
                case "assets/prefabs/building core/floor.triangle.frame/floor.triangle.frame.prefab":
                    if (!_config.RaidBlockSettings["Build Ceiling In Building Block"]) return null;
                    ShowUIAlert(player, GetMsg(player?.UserIDString, "UI_CEILINGBLOCK"));
                    return false;
                case "assets/prefabs/npc/autoturret/autoturret_deployed.prefab":
                    if (!_config.RaidBlockSettings["Build Turrets In 2x Building Block Radius"]) return null;
                    var tcs = Facepunch.Pool.GetList<BuildingPrivlidge>();
                    Vis.Entities(player.transform.position, 36, tcs);
                    foreach (var check in tcs)
                    {
                        if (check.IsAuthed(player)) continue;
                        ShowUIAlert(player, GetMsg(player?.UserIDString, "UI_TURRETBLOCK"));
                        return false;
                    }

                    return null;
                default:
                    return null;
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || !_config.DisplayUI) return;
            ShowHudUI(player);
            if (_config.NPP == 0)
                return;
            
            if (!_data.NPP.ContainsKey(player?.UserIDString))
                _data.NPP.Add(player?.UserIDString, DateTime.Now.AddHours(_config.NPP));
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null) return;
            LeaveTime.TryAdd(player.userID, DateTime.Now);
        }

        private void OnServerInitialized()
        {
            if (!ImageLibrary)
            {
                if (ILCheck == 3)
                {
                    PrintError("ImageLibrary not found!Unloading");
                    Interface.Oxide.UnloadPlugin(Name);
                    return;
                }

                timer.In(1, () =>
                {
                    ILCheck++;
                    OnServerInitialized();
                });
                return;
            }

            LoadData();
            ToggleOnHooks();
            LoadImages();
            ClearDateCache();
            CheckForStartSafeTime();
            cmd.AddChatCommand(_config.Command, this, nameof(cmdChatRTM));
            timer.In(1.5f, OnServerInitializedLate);
        }

        private void OnServerInitializedLate()
        {
            foreach (var check in BasePlayer.activePlayerList) OnPlayerConnected(check);
            ServerMgr.Instance.StartCoroutine(CheckTimer());
        }

        private void Unload()
        {
            Update = false;
            ToggleOffHooks();
            foreach (var check in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(check, Layer + ".bg");
                CuiHelper.DestroyUi(check, Layer + ".alert");
                CuiHelper.DestroyUi(check, Layer + ".HUD");
            }
        }

        #endregion

        #region Commands

        private void cmdChatRTM(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (args.Length == 0)
            {
                ShowUIBackGround(player);
                ShowRTMInfo(player, Time.Year, Time.Month, player.IsAdmin);
                return;
            }

            if (args.Length == 1 && args[0] == "hud")
            {
                if (ClosedUI.Contains(player.userID))
                {
                    ClosedUI.Remove(player.userID);
                    ShowHudUI(player);
                    return;
                }

                if (!_config.AllowToCloseUI) return;
                SendMessage(player, "CM_CLOSE_HUD");
                ClosedUI.Add(player.userID);
                CuiHelper.DestroyUi(player, Layer + ".HUD");
            }
        }

        [ConsoleCommand("UI_RTM")]
        private void cmdConsoleUI_RTM(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null || !arg.HasArgs()) return;
            int time;
            switch (arg.GetString(0))
            {
                case "OPENMONTH":
                    ShowRTMInfo(player, arg.GetInt(1), arg.GetInt(2), player.IsAdmin);
                    return;
                case "SHOWDAYINFO":
                    ShowUIDayInfo(player, arg.GetInt(1), arg.GetInt(2), arg.GetInt(3));
                    return;
                case "CLOSE":
                    if (!_config.AllowToCloseUI) return;
                    SendMessage(player, "CM_CLOSE_HUD");
                    ClosedUI.Add(player.userID);
                    CuiHelper.DestroyUi(player, Layer + ".HUD");
                    break;
                case "OPENSETTINGS":
                    ShowUISettings(player);
                    return;
                case "COMMAND":
                    if (!arg.HasArgs(2)) return;
                    _config.Command = arg.GetString(1);
                    player.ChatMessage("You need to reload plugin after this change");
                    break;
                case "TIMEZONE":
                    if (!arg.HasArgs(2)) return;
                    _config.TimeZone = arg.GetInt(1);
                    player.ChatMessage("You need to reload plugin after this change");
                    break;
                case "ASD":
                    if (!arg.HasArgs(2)) return;
                    _config.AdditionalSafeHours = arg.GetInt(1);
                    player.ChatMessage("You need to reload plugin after this change");
                    break;
                case "NPP":
                    if (!arg.HasArgs(2)) return;
                    _config.NPP = arg.GetInt(1);
                    player.ChatMessage("You need to reload plugin after this change");
                    break;
                case "TIMEFORMAT":
                    _config.TimeFormat = !_config.TimeFormat;
                    break;
                case "DISPLAYUI":
                    _config.DisplayUI = !_config.DisplayUI;
                    if (_config.DisplayUI)
                        foreach (var check in BasePlayer.activePlayerList)
                            OnPlayerConnected(check);
                    else
                        foreach (var check in BasePlayer.activePlayerList)
                            CuiHelper.DestroyUi(check, Layer + ".HUD");
                    break;
                case "RBAR":
                    _config.RaidRBInSafeTime = !_config.RaidRBInSafeTime;
                    break;
                case "ACUIBP":
                    _config.AllowToCloseUI = !_config.AllowToCloseUI;
                    break;
                case "BOF":
                    _config.BlockOnlyOffline = !_config.BlockOnlyOffline;
                    break;
                case "ENABLEALERTS":
                    _config.Alerts = !_config.Alerts;
                    break;
                case "SETTINGS":
                    var type = string.Join(" ", arg.Args.Skip(1));
                    _config.RaidBlockSettings[type] = !_config.RaidBlockSettings[type];
                    break;
                case "UITYPE":
                    var currUIPos = UIPositions.IndexOf(UIPositions.FirstOrDefault(x => x == _config.UIPosition));
                    _config.UIPosition = UIPositions[currUIPos >= UIPositions.Count - 1 ? 0 : currUIPos + 1];
                    foreach (var check in BasePlayer.activePlayerList) OnPlayerConnected(check);
                    break;
                case "ZONEMANAGER":
                    var currState = ZoneManagerSettings.IndexOf(ZoneManagerSettings.FirstOrDefault(x => x == _config.ZoneManagerSettings));
                    _config.ZoneManagerSettings = ZoneManagerSettings[currState >= ZoneManagerSettings.Count - 1 ? 0 : currState + 1];
                    foreach (var check in BasePlayer.activePlayerList) OnPlayerConnected(check);
                    break;
                case "ADDDAY":
                    if (_config.BlockByDay.ContainsKey($"{arg.GetString(2)}/{arg.GetString(3)}/{arg.GetString(1)}")) return;
                    _config.BlockByDay.Add($"{arg.GetString(2)}/{arg.GetString(3)}/{arg.GetString(1)}", new List<RaidBlockTime> { new RaidBlockTime { StartHours = 18, StartMinutes = 0, StopHours = 24, StopMinutes = 0 } });
                    break;
                case "OPENADDDAYS":
                    ShowRTMInfoAddDays(player, arg.GetInt(1), arg.GetInt(2));
                    return;
                case "OPENNEXTPAGE":
                    ShowUISettings(player, arg.GetString(1), arg.GetInt(2));
                    return;
                case "ADDNEWRAIDTIME":
                    if (_config.BlockByWeekDay.ContainsKey(arg.GetString(1)))
                    {
                        _config.BlockByWeekDay[arg.GetString(1)].Add(new RaidBlockTime { StartHours = 0, StartMinutes = 0, StopHours = 0, StopMinutes = 0 });
                        ShowUISettings(player, arg.GetString(1), arg.GetInt(2));
                        return;
                    }

                    _config.BlockByDay[arg.GetString(1)].Add(new RaidBlockTime { StartHours = 0, StartMinutes = 0, StopHours = 0, StopMinutes = 0 });
                    ShowUISettings(player, arg.GetString(1), arg.GetInt(2));
                    return;
                case "SSH":
                    if (!arg.HasArgs(4)) return;
                    time = arg.GetInt(3);
                    if (time > 24) time = 24;
                    if (time < 0) time = 0;
                    _config.BlockByWeekDay[arg.GetString(1)][arg.GetInt(2)].StartHours = time;
                    ShowUISettings(player, arg.GetString(1), arg.GetInt(2));
                    SaveConfig();
                    return;
                case "SSM":
                    if (!arg.HasArgs(4)) return;
                    time = arg.GetInt(3);
                    if (time > 59) time = 59;
                    if (time < 0) time = 0;
                    _config.BlockByWeekDay[arg.GetString(1)][arg.GetInt(2)].StartMinutes = time;
                    ShowUISettings(player, arg.GetString(1), arg.GetInt(2));
                    SaveConfig();
                    return;
                case "SEH":
                    if (!arg.HasArgs(4)) return;
                    time = arg.GetInt(3);
                    if (time > 24) time = 24;
                    if (time < 0) time = 0;
                    _config.BlockByWeekDay[arg.GetString(1)][arg.GetInt(2)].StopHours = time;
                    ShowUISettings(player, arg.GetString(1), arg.GetInt(2));
                    SaveConfig();
                    return;
                case "SEM":
                    if (!arg.HasArgs(4)) return;
                    time = arg.GetInt(3);
                    if (time > 59) time = 59;
                    if (time < 0) time = 0;
                    _config.BlockByWeekDay[arg.GetString(1)][arg.GetInt(2)].StopMinutes = time;
                    ShowUISettings(player, arg.GetString(1), arg.GetInt(2));
                    SaveConfig();
                    return;
                case "DSSH":
                    if (!arg.HasArgs(6)) return;
                    var dssh = _config.BlockByDay.FirstOrDefault(x => x.Key == $"{arg.GetString(2)}/{arg.GetString(3)}/{arg.GetString(1)}");
                    if (dssh.Value == null) return;
                    time = arg.GetInt(5);
                    if (time > 24) time = 24;
                    if (time < 0) time = 0;
                    dssh.Value[arg.GetInt(4)].StartHours = time;
                    ShowUISettings(player, $"{arg.GetString(2)}/{arg.GetString(3)}/{arg.GetString(1)}", arg.GetInt(4));
                    SaveConfig();
                    return;
                case "DSSM":
                    if (!arg.HasArgs(6)) return;
                    var dssm = _config.BlockByDay.FirstOrDefault(x => x.Key == $"{arg.GetString(2)}/{arg.GetString(3)}/{arg.GetString(1)}");
                    if (dssm.Value == null) return;
                    time = arg.GetInt(5);
                    if (time > 59) time = 59;
                    if (time < 0) time = 0;
                    dssm.Value[arg.GetInt(4)].StartMinutes = time;
                    ShowUISettings(player, $"{arg.GetString(2)}/{arg.GetString(3)}/{arg.GetString(1)}", arg.GetInt(4));
                    SaveConfig();
                    return;
                case "DSEH":
                    if (!arg.HasArgs(6)) return;
                    var dseh = _config.BlockByDay.FirstOrDefault(x => x.Key == $"{arg.GetString(2)}/{arg.GetString(3)}/{arg.GetString(1)}");
                    if (dseh.Value == null) return;
                    time = arg.GetInt(5);
                    if (time > 24) time = 24;
                    if (time < 0) time = 0;
                    dseh.Value[arg.GetInt(4)].StopHours = time;
                    ShowUISettings(player, $"{arg.GetString(2)}/{arg.GetString(3)}/{arg.GetString(1)}", arg.GetInt(4));
                    SaveConfig();
                    return;
                case "DSEM":
                    if (!arg.HasArgs(6)) return;
                    var dsem = _config.BlockByDay.FirstOrDefault(x => x.Key == $"{arg.GetString(2)}/{arg.GetString(3)}/{arg.GetString(1)}");
                    if (dsem.Value == null) return;
                    time = arg.GetInt(5);
                    if (time > 59) time = 59;
                    if (time < 0) time = 0;
                    dsem.Value[arg.GetInt(4)].StopMinutes = time;
                    ShowUISettings(player, $"{arg.GetString(2)}/{arg.GetString(3)}/{arg.GetString(1)}", arg.GetInt(4));
                    SaveConfig();
                    return;
                case "REMOVEDAYPAGE":
                    var page = arg.GetInt(2);
                    if (_config.BlockByWeekDay.ContainsKey(arg.GetString(1)))
                    {
                        _config.BlockByWeekDay[arg.GetString(1)].RemoveAt(page);
                        ShowUISettings(player, arg.GetString(1), page);
                        return;
                    }

                    _config.BlockByDay[arg.GetString(1)].RemoveAt(page);
                    ShowUISettings(player, arg.GetString(1), page);
                    return;
                case "REMOVEDAY":
                    _config.BlockByDay.Remove(_config.BlockByDay.FirstOrDefault(x => x.Key == $"{arg.GetString(2)}/{arg.GetString(3)}/{arg.GetString(1)}").Key);
                    break;
                case "TRUEPVESAFE":
                    _config.SafeTimeRuleSet = arg.GetString(1);
                    break;
                case "TRUEPVERAID":
                    _config.RaidTimeRuleSet = arg.GetString(1);
                    break;
            }

            ShowUISettings(player);
            SaveConfig();
        }

        [ConsoleCommand("UI_RTMCLOSEALERT")]
        private void cmdConsoleUI_RTMCLOSEALERT(ConsoleSystem.Arg arg)
        {
            if (arg?.Player() == null) return;
            DestroyAlert(arg.Player());
        }

        #endregion

        #region Functions

        private void CheckForStartSafeTime()
        {
            if (_config.AdditionalSafeHours == 0 || DateTime.UtcNow.Subtract(SaveRestore.SaveCreatedTime.Date).TotalSeconds > _config.AdditionalSafeHours * 86400) return;
            SetStartSafeTime(SaveRestore.SaveCreatedTime);
        }

        private void SetStartSafeTime(DateTime wipeTime)
        {
            var additionDays = _config.AdditionalSafeHours;
            var settings = _config.BlockByDay;
            for (int i = 0; i < additionDays + 1; i++)
            {
                if (i == 0)
                {
                    if (settings.ContainsKey($"{wipeTime.Month}/{wipeTime.Day}/{wipeTime.Year}")) settings[$"{wipeTime.Month}/{wipeTime.Day}/{wipeTime.Year}"] = new List<RaidBlockTime> { new RaidBlockTime { StartHours = 0, StartMinutes = 0, StopHours = wipeTime.Hour, StopMinutes = wipeTime.Minute } };
                    else settings.Add($"{wipeTime.Month}/{wipeTime.Day}/{wipeTime.Year}", new List<RaidBlockTime> { new RaidBlockTime { StartHours = 0, StartMinutes = 0, StopHours = wipeTime.Hour, StopMinutes = wipeTime.Minute } });
                    continue;
                }

                if (i == additionDays)
                {
                    wipeTime = wipeTime.AddDays(1);
                    if (settings.ContainsKey($"{wipeTime.Month}/{wipeTime.Day}/{wipeTime.Year}")) settings[$"{wipeTime.Month}/{wipeTime.Day}/{wipeTime.Year}"] = new List<RaidBlockTime> { new RaidBlockTime { StartHours = wipeTime.Hour, StartMinutes = wipeTime.Minute, StopHours = 24, StopMinutes = 0 } };
                    else settings.Add($"{wipeTime.Month}/{wipeTime.Day}/{wipeTime.Year}", new List<RaidBlockTime> { new RaidBlockTime { StartHours = wipeTime.Hour, StartMinutes = wipeTime.Minute, StopHours = 24, StopMinutes = 0 } });
                    continue;
                }

                wipeTime = wipeTime.AddDays(1);
                if (settings.ContainsKey($"{wipeTime.Month}/{wipeTime.Day}/{wipeTime.Year}")) settings[$"{wipeTime.Month}/{wipeTime.Day}/{wipeTime.Year}"] = new List<RaidBlockTime> { new RaidBlockTime { StartHours = 0, StartMinutes = 0, StopHours = 0, StopMinutes = 0 } };
                else settings.Add($"{wipeTime.Month}/{wipeTime.Day}/{wipeTime.Year}", new List<RaidBlockTime> { new RaidBlockTime { StartHours = 0, StartMinutes = 0, StopHours = 0, StopMinutes = 0 } });
            }

            SaveConfig();
        }

        private void ShowHudUI(BasePlayer player)
        {
            switch (_config.UIPosition)
            {
                case "UPPER_LEFT":
                    ShowUIUpperLeftBG(player);
                    break;
                case "UPPER_RIGHT":
                    ShowUIUpperRightBG(player);
                    break;
                case "LOWER_RIGHT":
                    ShowUILowerRightBG(player);
                    break;
                case "UPPER_CENTER":
                    ShowUIUpperCenterBG(player);
                    break;
                case "LOWER_CENTER":
                    ShowUILowerCenterBG(player);
                    break;
                case "LOWER_CENTER_LITE":
                    ShowUILowerCenterLiteBG(player);
                    break;
            }
        }

        private void UpdateHudUI()
        {
            switch (_config.UIPosition)
            {
                case "UPPER_LEFT":
                    ShowUIUpperLeftChange();
                    break;
                case "UPPER_RIGHT":
                    ShowUIUpperRightChange();
                    break;
                case "LOWER_RIGHT":
                    ShowUILowerRightChange();
                    break;
                case "UPPER_CENTER":
                    ShowUIUpperCenterChange();
                    break;
                case "LOWER_CENTER":
                    ShowUILowerCenterChange();
                    break;
                case "LOWER_CENTER_LITE":
                    ShowUILowerCenterLiteChange();
                    break;
            }
        }

        private void SendDiscordMessage()
        {
            var currentDay = Time;
            var time = _config.BlockByDay.FirstOrDefault(x => x.Key == $"{currentDay.Month}/{currentDay.Day}/{currentDay.Year}").Value ?? _config.BlockByWeekDay[new DateTime(currentDay.Year, currentDay.Month, currentDay.Day).DayOfWeek.ToString()];
            var raidTime = GetNearesTime(time);
            
            var field = new WWWForm();
            field.AddField("content", $"The raid will start in {_config.DiscordMessageTime} minutes, and will last {raidTime.StopHours * 60 - raidTime.StartHours * 60 + raidTime.StopMinutes - raidTime.StartMinutes} minutes [{(_config.BlockOnlyOffline ? "Offline" : "Online")} raids]");
            UnityWebRequest.Post(_config.DiscordWebHook, field).SendWebRequest();
        }

        private IEnumerator CheckTimer()
        {
            while (Update)
            {
                if (_config.DisplayUI) UpdateHudUI();
                if (IsRaidTime())
                {
                    if (CanRaidNow)
                    {
                        yield return new WaitForSeconds(5f);
                        continue;
                    }

                    CanRaidNow = true;
                    ToggleOffHooks();
                    PrintWarning("Raid time has begun!");
                    foreach (var check in _config.CommandOnRaidStarted)
                        Server.Command(check);
                    if (TruePVE != null) TruePVE.Call("ResetRules", _config.RaidTimeRuleSet);
                    foreach (var player in BasePlayer.activePlayerList) ShowUIAlert(player, GetMsg(player?.UserIDString, "UI_START_RAID_TIME"));
                }
                else
                {
                    if (DiscordTimer == null && !string.IsNullOrEmpty(_config.DiscordWebHook))
                    {
                        var currentDay = Time;
                        var time = _config.BlockByDay.FirstOrDefault(x => x.Key == $"{currentDay.Month}/{currentDay.Day}/{currentDay.Year}").Value ?? _config.BlockByWeekDay[new DateTime(currentDay.Year, currentDay.Month, currentDay.Day).DayOfWeek.ToString()];
                        if (!IsSafeDay(time))
                        { 
                            var raidTime = GetNearesTime(time);
                            var startTimer = (raidTime.StartHours - currentDay.Hour) * 60 * 60 + (raidTime.StartMinutes - currentDay.Minute) * 60 - _config.DiscordMessageTime * 60;
                            if (startTimer < 0)
                                DiscordTimer = timer.Once(1f, null);
                            else
                                DiscordTimer = timer.Once((raidTime.StartHours - currentDay.Hour) * 60 * 60 + (raidTime.StartMinutes - currentDay.Minute) * 60 - _config.DiscordMessageTime * 60, SendDiscordMessage);
                        }
                    }

                    if (!CanRaidNow)
                    {
                        yield return new WaitForSeconds(5f);
                        continue;
                    }

                    CanRaidNow = false;
                    ToggleOnHooks();
                    PrintWarning("The raid time is over.");
                    foreach (var check in _config.CommandOnSafeStarted)
                        Server.Command(check);
                    if (TruePVE != null) TruePVE.Call("ResetRules", _config.SafeTimeRuleSet);
                    foreach (var player in BasePlayer.activePlayerList) ShowUIAlert(player, GetMsg(player?.UserIDString, "UI_STOP_RAID_TIME"));
                    DiscordTimer = null;
                }

                yield return new WaitForSeconds(5f);
            }
        }

        private RaidBlockTime GetNearesTime(List<RaidBlockTime> times)
        {
            if (times.Count == 1) return times[0];
            var currentTime = Time;
            var currentMinutes = currentTime.Hour * 60 + currentTime.Minute;
            RaidBlockTime raidBlockTime = null;

            foreach (var check in times)
            {
                var startMinutes = check.StartHours * 60 + check.StartMinutes;
                var stopMinutes = check.StopHours * 60 + check.StopMinutes;
                if (startMinutes > currentMinutes || stopMinutes < currentMinutes) continue;
                if (raidBlockTime != null && raidBlockTime.StartHours * 60 + raidBlockTime.StartMinutes < startMinutes) continue;
                raidBlockTime = check;
            }

            return raidBlockTime ?? times[times.Count - 1];
        }

        private bool IsSafeDay(List<RaidBlockTime> times)
        {
             foreach (var check in times)
                if (check.StartHours != check.StopHours || check.StartMinutes != check.StopMinutes)
                    return false;
            return true;
        }

        private string GetCorrectDate(int year, int month, int day)
        {
            var date = string.Empty;
            date += month >= 10 ? $"{month}/" : $"0{month}/";
            date += day >= 10 ? $"{day}/" : $"0{day}/";
            date += $"{year}";
            return date;
        }

        private int GetStartPosOfDay(DayOfWeek day)
        {
            switch (day)
            {
                case DayOfWeek.Sunday:
                    return 590;
                case DayOfWeek.Monday:
                    return 140;
                case DayOfWeek.Tuesday:
                    return 215;
                case DayOfWeek.Wednesday:
                    return 290;
                case DayOfWeek.Thursday:
                    return 365;
                case DayOfWeek.Friday:
                    return 440;
                case DayOfWeek.Saturday:
                    return 515;
                default:
                    return 140;
            }
        }

        private bool IsRaidTime()
        {
            var RaidBlockDay = GetCurrentDayBlock();
            return RaidBlockDay != null ? IsRaidTime(_config.BlockByDay[RaidBlockDay.ToString()]) : IsRaidTime(_config.BlockByWeekDay[Time.DayOfWeek.ToString()]);
        }

        private bool CanRaidPlayer(BaseEntity entity)
        {
            if (!_config.BlockOnlyOffline) 
                return false;
            
            var owner = entity.OwnerID;
            var team = RelationshipManager.ServerInstance.FindPlayersTeam(owner);
            if (team == null) return IsOnlineRecently(owner);
            foreach (var check in team.members)
                if (IsOnlineRecently(check))
                    return true;

            return false;
        }

        private bool IsOnlineRecently(ulong id)
        {
            var player = BasePlayer.FindByID(id);
            if (player != null) 
                return true;
            DateTime leaveTime;
            if (!LeaveTime.TryGetValue(id, out leaveTime)) 
                return false;
            if (DateTime.Now.Subtract(leaveTime).TotalSeconds < _config.OfflineDefenceTimer) 
                return true;
            return false;
        }

        private bool IsRaidTime(List<RaidBlockTime> times)
        {
            var currentTime = Time.Hour * 60 + Time.Minute;
            foreach (var time in times)
                if (time.StartHours * 60 + time.StartMinutes <= currentTime && time.StopHours * 60 + time.StopMinutes > currentTime)
                    return true;
            return false;
        }

        private RaidBlockDay GetCurrentDayBlock()
        {
            var time = Time;
            foreach (var check in _config.BlockByDay)
            {
                var key = RaidBlockDay.GetFromString(check.Key);
                if (key.Year != time.Year || key.Month != time.Month || key.Day != time.Day) continue;
                return key;
            }

            return null;
        }

        private void ToggleOnHooks()
        {
            Subscribe(nameof(CanBuild));
        }

        private void ToggleOffHooks()
        {
            Unsubscribe(nameof(CanBuild));
        }

        private bool InitiatorIsHelicopter(HitInfo hitInfo)
        {
            if (hitInfo.Initiator is BaseHelicopter || (hitInfo.Initiator != null && (hitInfo.Initiator.ShortPrefabName.Equals("oilfireballsmall") || hitInfo.Initiator.ShortPrefabName.Equals("napalm")))) return true;
            return hitInfo.WeaponPrefab != null && (hitInfo.WeaponPrefab.ShortPrefabName.Equals("rocket_heli") || hitInfo.WeaponPrefab.ShortPrefabName.Equals("rocket_heli_napalm"));
        }

        private void ClearDateCache()
        {
            var currentDate = Time;
            var blockByDay = _config.BlockByDay;
            foreach (var check in _config.BlockByDay.ToArray())
            {
                var key = RaidBlockDay.GetFromString(check.Key);
                if (key.Year < currentDate.Year)
                {
                    blockByDay.Remove(check.Key);
                    continue;
                }

                if (key.Year > currentDate.Year) continue;
                if (key.Month < currentDate.Month)
                {
                    blockByDay.Remove(check.Key);
                    continue;
                }

                if (key.Month > currentDate.Month || key.Day >= currentDate.Day) continue;
                blockByDay.Remove(check.Key);
            }
        }

        private string TimeString(string userID)
        {
            var time = Time;
            if (_config.TimeFormat) return time.ToShortTimeString();
            return time.Hour > 12 ? $"{time.Hour - 12}:{time.Minute} {GetMsg(userID, "UI_PM")}" : $"{time.ToShortTimeString()} {GetMsg(userID, "UI_AM")}";
        }

        private string TimeString(int hours, int minutes, string userID)
        {
            var time = string.Empty;
            time += hours >= 10 ? $"{hours}:" : $"0{hours}:";
            time += minutes >= 10 ? $"{minutes}" : $"0{minutes}";
            if (_config.TimeFormat) return time;

            return hours > 12 ? minutes >= 10 ? $"{hours - 12}:{minutes} {GetMsg(userID, "UI_PM")}" : $"{hours - 12}:0{minutes} {GetMsg(userID, "UI_PM")}" : $"{time} {GetMsg(userID, "UI_AM")}";
        }

        private DateTime Time => DateTime.UtcNow.AddMinutes(_config.TimeZone);

        private bool InCorrectZone(BaseEntity entity)
        {
            if (_config.ZoneManagerSettings == "DISABLED" || ZoneManager == null) return true;
            if (_config.ZoneManagerSettings == "TRUE")
            {
                foreach (var check in _config.ZoneIDs)
                    if (ZoneManager.Call<bool>("IsEntityInZone", check, entity))
                        return true;

                return false;
            }

            foreach (var check in _config.ZoneIDs)
                if (ZoneManager.Call<bool>("IsEntityInZone", check, entity))
                    return false;

            return true;
        }

        #endregion

        #region UI

        #region LowerCenterLite

        private void ShowUILowerCenterLiteChange(BasePlayer player = null)
        {
            var container = new CuiElementContainer();
            var currentDay = Time;
            var time = _config.BlockByDay.FirstOrDefault(x => x.Key == $"{currentDay.Month}/{currentDay.Day}/{currentDay.Year}").Value ?? _config.BlockByWeekDay[new DateTime(currentDay.Year, currentDay.Month, currentDay.Day).DayOfWeek.ToString()];
            var currentPos = (currentDay.Hour * 60 + currentDay.Minute) / 1440f;

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.31 0.03", AnchorMax = "0.89 0.07" },
                Image = { Color = "0.2 0.6 0.2 1" }
            }, Layer + ".HUD", Layer + ".timeLine");

            foreach (var check in time)
            {
                var startPos = (check.StartHours * 60 + check.StartMinutes) / 1440f;
                var stopPos = (check.StopHours * 60 + check.StopMinutes) / 1440f;

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{startPos} 0", AnchorMax = $"{stopPos} 0.78" },
                    Image = { Color = "0.6 0.2 0.2 1" }
                }, Layer + ".timeLine");
            }

            container.Add(new CuiElement
            {
                Parent = Layer + ".timeLine",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{currentPos} 0", AnchorMax = $"{currentPos} 0.9",
                        OffsetMin = "-1 -2", OffsetMax = "1 2"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0 0 0 1",
                        Distance = "-0.5 -0.5"
                    }
                }
            });

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.155 0", AnchorMax = "0.33 0.1" },
                Image = { Color = "0 0 0 0" }
            }, Layer + ".HUD", Layer + ".raideTimeStatus");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.15 0.4", AnchorMax = "0.18 0.6" },
                Image = { Color = CanRaidNow ? "1 0 0 1" : "0 1 0 1", Sprite = "assets/icons/isbroken.png" }
            }, Layer + ".raideTimeStatus");

            if (player == null)
            {
                foreach (var check in BasePlayer.activePlayerList)
                {
                    if (ClosedUI.Contains(check.userID)) continue;

                    var container1 = new CuiElementContainer();
                    
                    container1.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.225 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                        Text = { Text = CanRaidNow ? GetMsg(check.UserIDString, "UI_RAID_TIME") : GetMsg(check.UserIDString, "UI_SAFE_TIME"), FontSize = 9, Font = "robotocondensed-regular.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft }
                    }, Layer + ".raideTimeStatus");
                    
                    CuiHelper.DestroyUi(check, Layer + ".time");
                    CuiHelper.DestroyUi(check, Layer + ".timeLine");
                    CuiHelper.DestroyUi(check, Layer + ".raideTimeStatus");
                    CuiHelper.DestroyUi(check, Layer + ".dayTime");
                    CuiHelper.AddUi(check, container);
                    CuiHelper.AddUi(check, container1);
                }

                return;
            }
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.225 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = CanRaidNow ? GetMsg(player.UserIDString, "UI_RAID_TIME") : GetMsg(player.UserIDString, "UI_SAFE_TIME"), FontSize = 9, Font = "robotocondensed-regular.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft }
            }, Layer + ".raideTimeStatus");

            CuiHelper.DestroyUi(player, Layer + ".time");
            CuiHelper.DestroyUi(player, Layer + ".timeLine");
            CuiHelper.DestroyUi(player, Layer + ".raideTimeStatus");
            CuiHelper.DestroyUi(player, Layer + ".dayTime");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUILowerCenterLiteBG(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.344 0.003", AnchorMax = "0.64 0.168" },
                Image = { Color = "0 0 0 0" }
            }, "Hud", Layer + ".HUD");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.1" },
                Image = { Color = "0 0 0 0.85" }
            }, Layer + ".HUD");

            container.Add(new CuiElement
            {
                Parent = Layer + ".HUD",
                Components =
                {
                    new CuiRawImageComponent { Png = GetImage("https://i.imgur.com/Qo14QIG.png") },
                    new CuiRectTransformComponent { AnchorMin = "0.15 0.015", AnchorMax = "0.17 0.085" }
                }
            });

            CuiHelper.DestroyUi(player, Layer + ".HUD");
            CuiHelper.AddUi(player, container);

            ShowUILowerCenterLiteChange(player);
        }

        #endregion

        #region LowerCenter

        private void ShowUILowerCenterChange(BasePlayer player = null)
        {
            var container = new CuiElementContainer();
            var currentDay = Time;
            var time = _config.BlockByDay.FirstOrDefault(x => x.Key == $"{currentDay.Month}/{currentDay.Day}/{currentDay.Year}").Value ?? _config.BlockByWeekDay[new DateTime(currentDay.Year, currentDay.Month, currentDay.Day).DayOfWeek.ToString()];
            var currentPos = (currentDay.Hour * 60 + currentDay.Minute) / 1440f;

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.31 0.03", AnchorMax = "0.89 0.07" },
                Image = { Color = "0.2 0.6 0.2 1" }
            }, Layer + ".HUD", Layer + ".timeLine");

            if (IsSafeDay(time))
            {
                container.Add(new CuiElement
                {
                    Parent = Layer + ".HUD",
                    Name = Layer + ".time",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"SAFE DAY <color=#a1a1a1>{GetCorrectDate(currentDay.Year, currentDay.Month, currentDay.Day)}</color>", FontSize = 14, Font = "robotocondensed-regular.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.1 0.725", AnchorMax = "0.9 0.858"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = "-0.5 -0.5"
                        }
                    }
                });
            }
            else
            {

                foreach (var check in time)
                {
                    var startPos = (check.StartHours * 60 + check.StartMinutes) / 1440f;
                    var stopPos = (check.StopHours * 60 + check.StopMinutes) / 1440f;

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{startPos} 0", AnchorMax = $"{stopPos} 0.78" },
                        Image = { Color = "0.6 0.2 0.2 1" }
                    }, Layer + ".timeLine");
                }

                var nearesTime = GetNearesTime(time);

                container.Add(new CuiElement
                {
                    Parent = Layer + ".HUD",
                    Name = Layer + ".time",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"{TimeString(nearesTime.StartHours, nearesTime.StartMinutes, player?.UserIDString)}·{TimeString(nearesTime.StopHours, nearesTime.StopMinutes, player?.UserIDString)} <color=#a1a1a1>{GetCorrectDate(currentDay.Year, currentDay.Month, currentDay.Day)}</color>", FontSize = 14, Font = "robotocondensed-regular.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.1 0.725", AnchorMax = "0.9 0.858"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = "-0.5 -0.5"
                        }
                    }
                });
            }

            container.Add(new CuiElement
            {
                Parent = Layer + ".timeLine",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{currentPos} 0", AnchorMax = $"{currentPos} 0.9",
                        OffsetMin = "-1 -2", OffsetMax = "1 2"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0 0 0 1",
                        Distance = "-0.5 -0.5"
                    }
                }
            });

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.155 0", AnchorMax = "0.33 0.1" },
                Image = { Color = "0 0 0 0" }
            }, Layer + ".HUD", Layer + ".raideTimeStatus");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.15 0.4", AnchorMax = "0.18 0.6" },
                Image = { Color = CanRaidNow ? "1 0 0 1" : "0 1 0 1", Sprite = "assets/icons/isbroken.png" }
            }, Layer + ".raideTimeStatus");

            var uiTime = TimeString(currentDay.Hour, currentDay.Minute, player?.UserIDString);

            if (player == null)
            {
                foreach (var check in BasePlayer.activePlayerList)
                {
                    if (ClosedUI.Contains(check.userID)) continue;

                    var container1 = new CuiElementContainer();
                    
                    container1.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.225 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                        Text = { Text = CanRaidNow ? GetMsg(check.UserIDString, "UI_RAID_TIME") : GetMsg(check.UserIDString, "UI_SAFE_TIME"), FontSize = 9, Font = "robotocondensed-regular.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft }
                    }, Layer + ".raideTimeStatus");

                    container1.Add(new CuiElement
                    {
                        Parent = Layer + ".HUD",
                        Name = Layer + ".dayTime",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = $"{GetMsg(check.UserIDString, $"UI_{currentDay.DayOfWeek.ToString()}")} <color=#a1a1a1>{uiTime}</color>", FontSize = 14, Font = "robotocondensed-bold.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.1 0.865", AnchorMax = "0.9 1"
                            },
                            new CuiOutlineComponent
                            {
                                Color = "0 0 0 1",
                                Distance = "-0.5 -0.5"
                            }
                        }
                    });
                    
                    CuiHelper.DestroyUi(check, Layer + ".time");
                    CuiHelper.DestroyUi(check, Layer + ".timeLine");
                    CuiHelper.DestroyUi(check, Layer + ".raideTimeStatus");
                    CuiHelper.DestroyUi(check, Layer + ".dayTime");
                    CuiHelper.AddUi(check, container);
                    CuiHelper.AddUi(check, container1);
                }

                return;
            }
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.225 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = CanRaidNow ? GetMsg(player.UserIDString, "UI_RAID_TIME") : GetMsg(player.UserIDString, "UI_SAFE_TIME"), FontSize = 9, Font = "robotocondensed-regular.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft }
            }, Layer + ".raideTimeStatus");

            container.Add(new CuiElement
            {
                Parent = Layer + ".HUD",
                Name = Layer + ".dayTime",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $"{GetMsg(player.UserIDString, $"UI_{currentDay.DayOfWeek.ToString()}")} <color=#a1a1a1>{uiTime}</color>", FontSize = 14, Font = "robotocondensed-bold.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.1 0.865", AnchorMax = "0.9 1"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0 0 0 1",
                        Distance = "-0.5 -0.5"
                    }
                }
            });

            CuiHelper.DestroyUi(player, Layer + ".time");
            CuiHelper.DestroyUi(player, Layer + ".timeLine");
            CuiHelper.DestroyUi(player, Layer + ".raideTimeStatus");
            CuiHelper.DestroyUi(player, Layer + ".dayTime");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUILowerCenterBG(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.344 0.003", AnchorMax = "0.64 0.168" },
                Image = { Color = "0 0 0 0" }
            }, "Hud", Layer + ".HUD");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.1" },
                Image = { Color = "0 0 0 0.85" }
            }, Layer + ".HUD");

            container.Add(new CuiElement
            {
                Parent = Layer + ".HUD",
                Components =
                {
                    new CuiRawImageComponent { Png = GetImage("https://i.imgur.com/Qo14QIG.png") },
                    new CuiRectTransformComponent { AnchorMin = "0.15 0.015", AnchorMax = "0.17 0.085" }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.45 0.6", AnchorMax = "0.55 0.75" },
                Text = { Text = "˅", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.63 0.63 0.63 1.00" },
                Button = { Color = "0 0 0 0", Command = "UI_RTM CLOSE" }
            }, Layer + ".HUD");

            CuiHelper.DestroyUi(player, Layer + ".HUD");
            CuiHelper.AddUi(player, container);

            ShowUILowerCenterChange(player);
        }

        #endregion

        #region UpperCenter

        private void ShowUIUpperCenterChange(BasePlayer player = null)
        {
            var container = new CuiElementContainer();
            var currentDay = Time;
            var time = _config.BlockByDay.FirstOrDefault(x => x.Key == $"{currentDay.Month}/{currentDay.Day}/{currentDay.Year}").Value ?? _config.BlockByWeekDay[new DateTime(currentDay.Year, currentDay.Month, currentDay.Day).DayOfWeek.ToString()];
            var currentPos = (currentDay.Hour * 60 + currentDay.Minute) / 1440f;

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.31 0.28", AnchorMax = "0.8 0.34" },
                Image = { Color = "0.2 0.6 0.2 1" }
            }, Layer + ".HUD", Layer + ".timeLine");

            if (IsSafeDay(time))
            {
                container.Add(new CuiElement
                {
                    Parent = Layer + ".HUD",
                    Name = Layer + ".time",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"SAFE DAY <color=#a1a1a1>{GetCorrectDate(currentDay.Year, currentDay.Month, currentDay.Day)}</color>", FontSize = 15, Font = "robotocondensed-regular.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.1 0.425", AnchorMax = "0.9 0.725"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = "-0.5 -0.5"
                        }
                    }
                });
            }
            else
            {
                foreach (var check in time)
                {
                    var startPos = (check.StartHours * 60 + check.StartMinutes) / 1440f;
                    var stopPos = (check.StopHours * 60 + check.StopMinutes) / 1440f;

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{startPos} 0", AnchorMax = $"{stopPos} 0.78" },
                        Image = { Color = "0.6 0.2 0.2 1" }
                    }, Layer + ".timeLine");
                }

                var nearestTime = GetNearesTime(time);

                container.Add(new CuiElement
                {
                    Parent = Layer + ".HUD",
                    Name = Layer + ".time",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"{TimeString(nearestTime.StartHours, nearestTime.StartMinutes, player?.UserIDString)}·{TimeString(nearestTime.StopHours, nearestTime.StopMinutes, player?.UserIDString)} <color=#a1a1a1>{GetCorrectDate(currentDay.Year, currentDay.Month, currentDay.Day)}</color>", FontSize = 15, Font = "robotocondensed-regular.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.1 0.425", AnchorMax = "0.9 0.725"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = "-0.5 -0.5"
                        }
                    }
                });
            }

            container.Add(new CuiElement
            {
                Parent = Layer + ".timeLine",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{currentPos} 0", AnchorMax = $"{currentPos} 0.9",
                        OffsetMin = "-1 -2", OffsetMax = "1 2"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0 0 0 1",
                        Distance = "-0.5 -0.5"
                    }
                }
            });

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.12 0.212", AnchorMax = "0.31 0.41" },
                Image = { Color = "0 0 0 0" }
            }, Layer + ".HUD", Layer + ".raideTimeStatus");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.15 0.4", AnchorMax = "0.18 0.6" },
                Image = { Color = CanRaidNow ? "1 0 0 1" : "0 1 0 1", Sprite = "assets/icons/isbroken.png" }
            }, Layer + ".raideTimeStatus");

            var uiTime = TimeString(currentDay.Hour, currentDay.Minute, player?.UserIDString);

            if (player == null)
            {
                foreach (var check in BasePlayer.activePlayerList)
                {
                    if (ClosedUI.Contains(check.userID)) continue;

                    var container1 = new CuiElementContainer();
                    
                    container1.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.225 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                        Text = { Text = CanRaidNow ? GetMsg(check.UserIDString, "UI_RAID_TIME") : GetMsg(check.UserIDString, "UI_SAFE_TIME"), FontSize = 9, Font = "robotocondensed-regular.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft }
                    }, Layer + ".raideTimeStatus");

                    container1.Add(new CuiElement
                    {
                        Parent = Layer + ".HUD",
                        Name = Layer + ".dayTime",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = $"{GetMsg(check.UserIDString, $"UI_{currentDay.DayOfWeek.ToString()}")} <color=#a1a1a1>{uiTime}</color>", FontSize = 15, Font = "robotocondensed-bold.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.1 0.725", AnchorMax = "0.9 1"
                            },
                            new CuiOutlineComponent
                            {
                                Color = "0 0 0 1",
                                Distance = "-0.5 -0.5"
                            }
                        }
                    });
                    
                    CuiHelper.DestroyUi(check, Layer + ".time");
                    CuiHelper.DestroyUi(check, Layer + ".timeLine");
                    CuiHelper.DestroyUi(check, Layer + ".raideTimeStatus");
                    CuiHelper.DestroyUi(check, Layer + ".dayTime");
                    CuiHelper.AddUi(check, container);
                    CuiHelper.AddUi(check, container1);
                }

                return;
            }
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.225 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = CanRaidNow ? GetMsg(player.UserIDString, "UI_RAID_TIME") : GetMsg(player.UserIDString, "UI_SAFE_TIME"), FontSize = 9, Font = "robotocondensed-regular.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft }
            }, Layer + ".raideTimeStatus");

            container.Add(new CuiElement
            {
                Parent = Layer + ".HUD",
                Name = Layer + ".dayTime",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $"{GetMsg(player.UserIDString, $"UI_{currentDay.DayOfWeek.ToString()}")} <color=#a1a1a1>{uiTime}</color>", FontSize = 15, Font = "robotocondensed-bold.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.1 0.725", AnchorMax = "0.9 1"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0 0 0 1",
                        Distance = "-0.5 -0.5"
                    }
                }
            });

            CuiHelper.DestroyUi(player, Layer + ".time");
            CuiHelper.DestroyUi(player, Layer + ".timeLine");
            CuiHelper.DestroyUi(player, Layer + ".raideTimeStatus");
            CuiHelper.DestroyUi(player, Layer + ".dayTime");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIUpperCenterBG(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.36 0.84", AnchorMax = "0.64 0.925" },
                Image = { Color = "0 0 0 0" }
            }, "Overlay", Layer + ".HUD");

            container.Add(new CuiElement
            {
                Parent = Layer + ".HUD",
                Components =
                {
                    new CuiRawImageComponent { Png = GetImage("https://i.imgur.com/mwbQmBO.png") },
                    new CuiRectTransformComponent { AnchorMin = "0 0.16", AnchorMax = "1 0.43" }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".HUD",
                Components =
                {
                    new CuiRawImageComponent { Png = GetImage("https://i.imgur.com/Qo14QIG.png") },
                    new CuiRectTransformComponent { AnchorMin = "0.115 0.25", AnchorMax = "0.135 0.36" }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.45 0", AnchorMax = "0.55 0.2" },
                Text = { Text = "˅", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.63 0.63 0.63 1.00" },
                Button = { Color = "0 0 0 0", Command = "UI_RTM CLOSE" }
            }, Layer + ".HUD");

            CuiHelper.DestroyUi(player, Layer + ".HUD");
            CuiHelper.AddUi(player, container);

            ShowUIUpperCenterChange(player);
        }

        #endregion

        #region LowerRight

        private void ShowUILowerRightChange(BasePlayer player = null)
        {
            var container = new CuiElementContainer();
            var currentDay = Time;
            var time = _config.BlockByDay.FirstOrDefault(x => x.Key == $"{currentDay.Month}/{currentDay.Day}/{currentDay.Year}").Value ?? _config.BlockByWeekDay[new DateTime(currentDay.Year, currentDay.Month, currentDay.Day).DayOfWeek.ToString()];

            var currentPos = (currentDay.Hour * 60 + currentDay.Minute) / 1440f;

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.023 0.117", AnchorMax = "0.843 0.182" },
                Image = { Color = "0.2 0.6 0.2 1" }
            }, Layer + ".HUD", Layer + ".timeLine");

            if (IsSafeDay(time))
            {
                container.Add(new CuiElement
                {
                    Parent = Layer + ".HUD",
                    Name = Layer + ".time",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"SAFE DAY <color=#a1a1a1>{GetCorrectDate(currentDay.Year, currentDay.Month, currentDay.Day)}</color>", FontSize = 15, Font = "robotocondensed-regular.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.023 0.22", AnchorMax = "0.839 0.43"
                        },
                    }
                });
            }
            else
            {
                foreach (var check in time)
                {
                    var startPos = (check.StartHours * 60 + check.StartMinutes) / 1440f;
                    var stopPos = (check.StopHours * 60 + check.StopMinutes) / 1440f;

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{startPos} 0", AnchorMax = $"{stopPos} 0.9" },
                        Image = { Color = "0.6 0.2 0.2 1" }
                    }, Layer + ".timeLine");
                }

                var nearestTime = GetNearesTime(time);

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.023 0.22", AnchorMax = "0.839 0.43" },
                    Text = { Text = $"{TimeString(nearestTime.StartHours, nearestTime.StartMinutes, player?.UserIDString)}·{TimeString(nearestTime.StopHours, nearestTime.StopMinutes, player?.UserIDString)} <color=#a1a1a1>{GetCorrectDate(currentDay.Year, currentDay.Month, currentDay.Day)}</color>", FontSize = 15, Font = "robotocondensed-regular.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft }
                }, Layer + ".HUD", Layer + ".time");
            }

            container.Add(new CuiElement
            {
                Parent = Layer + ".timeLine",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{currentPos} 0", AnchorMax = $"{currentPos} 0.9",
                        OffsetMin = "-1 -2", OffsetMax = "1 2"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0 0 0 1",
                        Distance = "-0.5 -0.5"
                    }
                }
            });

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.023 0.4", AnchorMax = "0.839 0.7" },
                Image = { Color = "0 0 0 0" }
            }, Layer + ".HUD", Layer + ".raideTimeStatus");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.055 0.4", AnchorMax = "0.075 0.6" },
                Image = { Color = CanRaidNow ? "1 0 0 1" : "0 1 0 1", Sprite = "assets/icons/isbroken.png" }
            }, Layer + ".raideTimeStatus");

            var uiTime = TimeString(currentDay.Hour, currentDay.Minute, player?.UserIDString);

            if (player == null)
            {
                foreach (var check in BasePlayer.activePlayerList)
                {
                    if (ClosedUI.Contains(check.userID)) continue;

                    var container1 = new CuiElementContainer();
                    
                    container1.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.1 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                        Text = { Text = CanRaidNow ? GetMsg(check.UserIDString, "UI_RAID_TIME") : GetMsg(check.UserIDString, "UI_SAFE_TIME"), FontSize = 9, Font = "robotocondensed-regular.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft }
                    }, Layer + ".raideTimeStatus");

                    container1.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.023 0.64", AnchorMax = "0.839 1" },
                        Text = { Text = $"{GetMsg(check.UserIDString, $"UI_{currentDay.DayOfWeek.ToString()}")} <color=#a1a1a1>{uiTime}</color>", FontSize = 16, Font = "robotocondensed-bold.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft }
                    }, Layer + ".HUD", Layer + ".dayTime");
                    
                    CuiHelper.DestroyUi(check, Layer + ".time");
                    CuiHelper.DestroyUi(check, Layer + ".timeLine");
                    CuiHelper.DestroyUi(check, Layer + ".raideTimeStatus");
                    CuiHelper.DestroyUi(check, Layer + ".dayTime");
                    CuiHelper.AddUi(check, container);
                    CuiHelper.AddUi(check, container1);
                }

                return;
            }
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.1 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = CanRaidNow ? GetMsg(player.UserIDString, "UI_RAID_TIME") : GetMsg(player.UserIDString, "UI_SAFE_TIME"), FontSize = 9, Font = "robotocondensed-regular.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft }
            }, Layer + ".raideTimeStatus");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.023 0.64", AnchorMax = "0.839 1" },
                Text = { Text = $"{GetMsg(player.UserIDString, $"UI_{currentDay.DayOfWeek.ToString()}")} <color=#a1a1a1>{uiTime}</color>", FontSize = 16, Font = "robotocondensed-bold.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft }
            }, Layer + ".HUD", Layer + ".dayTime");

            CuiHelper.DestroyUi(player, Layer + ".time");
            CuiHelper.DestroyUi(player, Layer + ".timeLine");
            CuiHelper.DestroyUi(player, Layer + ".raideTimeStatus");
            CuiHelper.DestroyUi(player, Layer + ".dayTime");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUILowerRightBG(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.665 0.022", AnchorMax = "0.833 0.135" },
                Image = { Color = "0 0 0 0.95" }
            }, "Overlay", Layer + ".HUD");

            container.Add(new CuiElement
            {
                Parent = Layer + ".HUD",
                Components =
                {
                    new CuiRawImageComponent { Png = GetImage("https://i.imgur.com/pO6eVOK.png"), Color = "1 1 1 0.05" },
                    new CuiRectTransformComponent { AnchorMin = "0.839 0", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".HUD",
                Components =
                {
                    new CuiRawImageComponent { Png = GetImage("https://i.imgur.com/Qo14QIG.png") },
                    new CuiRectTransformComponent { AnchorMin = "0.02 0.5", AnchorMax = "0.055 0.61" }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.93 0.4", AnchorMax = "0.98 0.63" },
                Text = { Text = ">", Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.63 0.63 0.63 1.00" },
                Button = { Color = "0 0 0 0", Command = "UI_RTM CLOSE" }
            }, Layer + ".HUD");

            CuiHelper.DestroyUi(player, Layer + ".HUD");
            CuiHelper.AddUi(player, container);

            ShowUILowerRightChange(player);
        }

        #endregion

        #region UpperRight

        private void ShowUIUpperRightChange(BasePlayer player = null)
        {
            var container = new CuiElementContainer();
            var currentDay = Time;
            var time = _config.BlockByDay.FirstOrDefault(x => x.Key == $"{currentDay.Month}/{currentDay.Day}/{currentDay.Year}").Value ?? _config.BlockByWeekDay[new DateTime(currentDay.Year, currentDay.Month, currentDay.Day).DayOfWeek.ToString()];

            var currentPos = (currentDay.Hour * 60 + currentDay.Minute) / 1440f;

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.161 0.117", AnchorMax = "0.977 0.182" },
                Image = { Color = "0.2 0.6 0.2 1" }
            }, Layer + ".HUD", Layer + ".timeLine");

            if (IsSafeDay(time))
            {
                container.Add(new CuiElement
                {
                    Parent = Layer + ".HUD",
                    Name = Layer + ".time",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"<color=#a1a1a1>{GetCorrectDate(currentDay.Year, currentDay.Month, currentDay.Day)}</color> SAFE DAY", FontSize = 15, Font = "robotocondensed-regular.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleRight
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.161 0.22", AnchorMax = "0.977 0.43"
                        },
                    }
                });
            }
            else
            {
                foreach (var check in time)
                {
                    var startPos = (check.StartHours * 60 + check.StartMinutes) / 1440f;
                    var stopPos = (check.StopHours * 60 + check.StopMinutes) / 1440f;

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{startPos} 0", AnchorMax = $"{stopPos} 0.9" },
                        Image = { Color = "0.6 0.2 0.2 1" }
                    }, Layer + ".timeLine");
                }

                var nearestTime = GetNearesTime(time);

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.161 0.22", AnchorMax = "0.977 0.43" },
                    Text = { Text = $"<color=#a1a1a1>{GetCorrectDate(currentDay.Year, currentDay.Month, currentDay.Day)}</color> {TimeString(nearestTime.StartHours, nearestTime.StartMinutes, player?.UserIDString)}·{TimeString(nearestTime.StopHours, nearestTime.StopMinutes, player?.UserIDString)}", FontSize = 15, Font = "robotocondensed-regular.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleRight }
                }, Layer + ".HUD", Layer + ".time");
            }

            container.Add(new CuiElement
            {
                Parent = Layer + ".timeLine",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{currentPos} 0", AnchorMax = $"{currentPos} 0.9",
                        OffsetMin = "-1 -2", OffsetMax = "1 2"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0 0 0 1",
                        Distance = "-0.5 -0.5"
                    }
                }
            });

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.161 0.4", AnchorMax = "0.977 0.7" },
                Image = { Color = "0 0 0 0" }
            }, Layer + ".HUD", Layer + ".raideTimeStatus");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.915 0.4", AnchorMax = "0.935 0.6" },
                Image = { Color = CanRaidNow ? "1 0 0 1" : "0 1 0 1", Sprite = "assets/icons/isbroken.png" }
            }, Layer + ".raideTimeStatus");

            var uiTime = TimeString(currentDay.Hour, currentDay.Minute, player?.UserIDString);

            if (player == null)
            {
                foreach (var check in BasePlayer.activePlayerList)
                {
                    if (ClosedUI.Contains(check.userID)) continue;

                    var container1 = new CuiElementContainer();
                    
                    container1.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "0.905 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                        Text = { Text = CanRaidNow ? GetMsg(check.UserIDString, "UI_RAID_TIME") : GetMsg(check.UserIDString, "UI_SAFE_TIME"), FontSize = 11, Font = "robotocondensed-regular.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleRight }
                    }, Layer + ".raideTimeStatus");

                    container1.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.161 0.64", AnchorMax = "0.977 1" },
                        Text = { Text = $"<color=#a1a1a1>{uiTime}</color> {GetMsg(check.UserIDString, $"UI_{currentDay.DayOfWeek.ToString()}")}", FontSize = 20, Font = "robotocondensed-bold.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleRight }
                    }, Layer + ".HUD", Layer + ".dayTime");
                    
                    CuiHelper.DestroyUi(check, Layer + ".time");
                    CuiHelper.DestroyUi(check, Layer + ".timeLine");
                    CuiHelper.DestroyUi(check, Layer + ".raideTimeStatus");
                    CuiHelper.DestroyUi(check, Layer + ".dayTime");
                    CuiHelper.AddUi(check, container);
                    CuiHelper.AddUi(check, container1);
                }

                return;
            }
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.905 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = CanRaidNow ? GetMsg(player.UserIDString, "UI_RAID_TIME") : GetMsg(player.UserIDString, "UI_SAFE_TIME"), FontSize = 11, Font = "robotocondensed-regular.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleRight }
            }, Layer + ".raideTimeStatus");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.161 0.64", AnchorMax = "0.977 1" },
                Text = { Text = $"<color=#a1a1a1>{uiTime}</color> {GetMsg(player.UserIDString, $"UI_{currentDay.DayOfWeek.ToString()}")}", FontSize = 20, Font = "robotocondensed-bold.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleRight }
            }, Layer + ".HUD", Layer + ".dayTime");

            CuiHelper.DestroyUi(player, Layer + ".time");
            CuiHelper.DestroyUi(player, Layer + ".timeLine");
            CuiHelper.DestroyUi(player, Layer + ".raideTimeStatus");
            CuiHelper.DestroyUi(player, Layer + ".dayTime");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIUpperRightBG(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.791 0.885", AnchorMax = "0.995 0.992" },
                Image = { Color = "0 0 0 0.95" }
            }, "Overlay", Layer + ".HUD");

            container.Add(new CuiElement
            {
                Parent = Layer + ".HUD",
                Components =
                {
                    new CuiRawImageComponent { Png = GetImage("https://i.imgur.com/nPqbl6m.png"), Color = "1 1 1 0.05" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0.161 1" }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".HUD",
                Components =
                {
                    new CuiRawImageComponent { Png = GetImage("https://i.imgur.com/Qo14QIG.png") },
                    new CuiRectTransformComponent { AnchorMin = "0.945 0.5", AnchorMax = "0.975 0.615" }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.02 0.4", AnchorMax = "0.07 0.63" },
                Text = { Text = ">", Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.63 0.63 0.63 1.00" },
                Button = { Color = "0 0 0 0", Command = "UI_RTM CLOSE" }
            }, Layer + ".HUD");

            CuiHelper.DestroyUi(player, Layer + ".HUD");
            CuiHelper.AddUi(player, container);

            ShowUIUpperRightChange(player);
        }

        #endregion

        #region UpperLeft

        private void ShowUIUpperLeftChange(BasePlayer player = null)
        {
            var container = new CuiElementContainer();
            var currentDay = Time;
            var time = _config.BlockByDay.FirstOrDefault(x => x.Key == $"{currentDay.Month}/{currentDay.Day}/{currentDay.Year}").Value ?? _config.BlockByWeekDay[new DateTime(currentDay.Year, currentDay.Month, currentDay.Day).DayOfWeek.ToString()];

            var currentPos = (currentDay.Hour * 60 + currentDay.Minute) / 1440f;
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.023 0.117", AnchorMax = "0.843 0.182" },
                Image = { Color = "0.2 0.6 0.2 1" }
            }, Layer + ".HUD", Layer + ".timeLine");

            if (IsSafeDay(time))
            {
                container.Add(new CuiElement
                {
                    Parent = Layer + ".HUD",
                    Name = Layer + ".time",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"SAFE DAY <color=#a1a1a1>{GetCorrectDate(currentDay.Year, currentDay.Month, currentDay.Day)}</color>", FontSize = 15, Font = "robotocondensed-regular.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.023 0.22", AnchorMax = "0.839 0.43"
                        },
                    }
                });
            }
            else
            {

                foreach (var check in time)
                {
                    var startPos = (check.StartHours * 60 + check.StartMinutes) / 1440f;
                    var stopPos = (check.StopHours * 60 + check.StopMinutes) / 1440f;

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{startPos} 0", AnchorMax = $"{stopPos} 0.9" },
                        Image = { Color = "0.6 0.2 0.2 1" }
                    }, Layer + ".timeLine");
                }

                var nearestTime = GetNearesTime(time);

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.023 0.22", AnchorMax = "0.839 0.43" },
                    Text = { Text = $"{TimeString(nearestTime.StartHours, nearestTime.StartMinutes, player?.UserIDString)}·{TimeString(nearestTime.StopHours, nearestTime.StopMinutes, player?.UserIDString)} <color=#a1a1a1>{GetCorrectDate(currentDay.Year, currentDay.Month, currentDay.Day)}</color>", FontSize = 15, Font = "robotocondensed-regular.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft }
                }, Layer + ".HUD", Layer + ".time");
            }

            container.Add(new CuiElement
            {
                Parent = Layer + ".timeLine",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{currentPos} 0", AnchorMax = $"{currentPos} 0.9",
                        OffsetMin = "-1 -2", OffsetMax = "1 2"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0 0 0 1",
                        Distance = "-0.5 -0.5"
                    }
                }
            });

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.023 0.4", AnchorMax = "0.839 0.7" },
                Image = { Color = "0 0 0 0" }
            }, Layer + ".HUD", Layer + ".raideTimeStatus");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.055 0.4", AnchorMax = "0.075 0.6" },
                Image = { Color = CanRaidNow ? "1 0 0 1" : "0 1 0 1", Sprite = "assets/icons/isbroken.png" }
            }, Layer + ".raideTimeStatus");

            var uiTime = TimeString(currentDay.Hour, currentDay.Minute, player?.UserIDString);
            
            if (player == null)
            {
                foreach (var check in BasePlayer.activePlayerList)
                {
                    if (ClosedUI.Contains(check.userID)) continue;

                    var container1 = new CuiElementContainer();
                    
                    container1.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.1 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                        Text = { Text = CanRaidNow ? GetMsg(check.UserIDString, "UI_RAID_TIME") : GetMsg(check.UserIDString, "UI_SAFE_TIME"), FontSize = 11, Font = "robotocondensed-regular.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft }
                    }, Layer + ".raideTimeStatus");

                    container1.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.023 0.64", AnchorMax = "0.839 1" },
                        Text = { Text = $"{GetMsg(check.UserIDString, $"UI_{currentDay.DayOfWeek.ToString()}")} <color=#a1a1a1>{uiTime}</color>", FontSize = 20, Font = "robotocondensed-bold.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft }
                    }, Layer + ".HUD", Layer + ".dayTime");
                    
                    CuiHelper.DestroyUi(check, Layer + ".time");
                    CuiHelper.DestroyUi(check, Layer + ".timeLine");
                    CuiHelper.DestroyUi(check, Layer + ".raideTimeStatus");
                    CuiHelper.DestroyUi(check, Layer + ".dayTime");
                    CuiHelper.AddUi(check, container);
                    CuiHelper.AddUi(check, container1);
                }

                return;
            }
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.1 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = CanRaidNow ? GetMsg(player.UserIDString, "UI_RAID_TIME") : GetMsg(player.UserIDString, "UI_SAFE_TIME"), FontSize = 11, Font = "robotocondensed-regular.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft }
            }, Layer + ".raideTimeStatus");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.023 0.64", AnchorMax = "0.839 1" },
                Text = { Text = $"{GetMsg(player.UserIDString, $"UI_{currentDay.DayOfWeek.ToString()}")} <color=#a1a1a1>{uiTime}</color>", FontSize = 20, Font = "robotocondensed-bold.ttf", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft }
            }, Layer + ".HUD", Layer + ".dayTime");

            CuiHelper.DestroyUi(player, Layer + ".time");
            CuiHelper.DestroyUi(player, Layer + ".timeLine");
            CuiHelper.DestroyUi(player, Layer + ".raideTimeStatus");
            CuiHelper.DestroyUi(player, Layer + ".dayTime");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIUpperLeftBG(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.005 0.885", AnchorMax = "0.209 0.992" },
                Image = { Color = "0 0 0 0.95" }
            }, "Overlay", Layer + ".HUD");

            container.Add(new CuiElement
            {
                Parent = Layer + ".HUD",
                Components =
                {
                    new CuiRawImageComponent { Png = GetImage("https://i.imgur.com/pO6eVOK.png"), Color = "1 1 1 0.05" },
                    new CuiRectTransformComponent { AnchorMin = "0.839 0", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".HUD",
                Components =
                {
                    new CuiRawImageComponent { Png = GetImage("https://i.imgur.com/Qo14QIG.png") },
                    new CuiRectTransformComponent { AnchorMin = "0.025 0.5", AnchorMax = "0.055 0.615" }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.93 0.4", AnchorMax = "0.98 0.63" },
                Text = { Text = "<", Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.63 0.63 0.63 1.00" },
                Button = { Color = "0 0 0 0", Command = "UI_RTM CLOSE" }
            }, Layer + ".HUD");

            CuiHelper.DestroyUi(player, Layer + ".HUD");
            CuiHelper.AddUi(player, container);
            ShowUIUpperLeftChange(player);
        }

        #endregion

        #region SettingsUI

        private void ShowUISettings(BasePlayer player, string redactingPage = "", int page = 0)
        {
            ClearDateCache();
            var container = new CuiElementContainer();
            var y = -80;

            container.Add(new CuiElement
            {
                Parent = Layer + ".bg",
                Name = Layer,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.2 0.2 0.2 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                        OffsetMin = "-400 -350", OffsetMax = "400 350"
                    }
                }
            });
            Outline(container, Layer, "0.15 0.15 0.15 1", "2");

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "RAID TIME MANAGEMENT",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 35,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = "0 -50", OffsetMax = "800 0"
                    }
                }
            });

            var time = Time;
            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "0 1",
                    OffsetMin = "8 -33", OffsetMax = "33 -8"
                },
                Button =
                {
                    Color = "1 1 1 1",
                    Command = $"UI_RTM OPENMONTH {time.Year} {time.Month}",
                    Sprite = "assets/icons/gear.png"
                },
                Text =
                {
                    Text = ""
                }
            }, Layer);

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "PLUGIN SETTINGS",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 20,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = "0 -80", OffsetMax = "400 -40"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "RAID TIME SETTINGS",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 20,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = "400 -80", OffsetMax = "800 -40"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "Command for open Raid Time Managment UI:",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleLeft
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"10 {y - 20}", OffsetMax = $"350 {y}"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.25 0.25 0.25 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"335 {y - 20}", OffsetMax = $"375 {y}"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0 0 0 1",
                        Distance = "-0.5 -0.5"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = _config.Command,
                        Color = "0.8 0.8 0.8 0.6",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"335 {y - 20}", OffsetMax = $"375 {y}"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Command = "UI_RTM COMMAND",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter,
                        CharsLimit = 5
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"335 {y - 20}", OffsetMax = $"375 {y}"
                    }
                }
            });

            y -= 25;

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "Timezone(Difference between UTC and your time M):",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleLeft
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"10 {y - 20}", OffsetMax = $"350 {y}"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.25 0.25 0.25 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"335 {y - 20}", OffsetMax = $"375 {y}"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0 0 0 1",
                        Distance = "-0.5 -0.5"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = _config.TimeZone.ToString(),
                        Color = "0.8 0.8 0.8 0.6",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"335 {y - 20}", OffsetMax = $"375 {y}"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Command = "UI_RTM TIMEZONE",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter,
                        CharsLimit = 5
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"335 {y - 20}", OffsetMax = $"375 {y}"
                    }
                }
            });

            y -= 25;

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "Additional safe days after wipe:",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleLeft
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"10 {y - 20}", OffsetMax = $"350 {y}"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.25 0.25 0.25 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"335 {y - 20}", OffsetMax = $"375 {y}"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0 0 0 1",
                        Distance = "-0.5 -0.5"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = _config.AdditionalSafeHours.ToString(),
                        Color = "0.8 0.8 0.8 0.6",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"335 {y - 20}", OffsetMax = $"375 {y}"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Command = "UI_RTM ASD",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter,
                        CharsLimit = 5
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"335 {y - 20}", OffsetMax = $"375 {y}"
                    }
                }
            });

            y -= 25;

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "New Player Protection duration:",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleLeft
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"10 {y - 20}", OffsetMax = $"350 {y}"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.25 0.25 0.25 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"335 {y - 20}", OffsetMax = $"375 {y}"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0 0 0 1",
                        Distance = "-0.5 -0.5"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = _config.NPP.ToString(),
                        Color = "0.8 0.8 0.8 0.6",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"335 {y - 20}", OffsetMax = $"375 {y}"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Command = "UI_RTM NPP",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter,
                        CharsLimit = 5
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"335 {y - 20}", OffsetMax = $"375 {y}"
                    }
                }
            });
            
            y -= 25;

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "Time format UI(True - 24 format False - 12 format):",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleLeft
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"10 {y - 20}", OffsetMax = $"350 {y}"
                    }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "0 1",
                    OffsetMin = $"335 {y - 20}", OffsetMax = $"375 {y}"
                },
                Button =
                {
                    Color = "0 0 0 0",
                    Command = "UI_RTM TIMEFORMAT"
                },
                Text =
                {
                    Text = _config.TimeFormat.ToString(),
                    Color = _config.TimeFormat ? "0.2 0.6 0.2 1" : "0.6 0.2 0.2 1",
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 15,
                    Align = TextAnchor.MiddleRight
                }
            }, Layer);

            y -= 25;

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "Display UI:",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleLeft
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"10 {y - 20}", OffsetMax = $"350 {y}"
                    }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "0 1",
                    OffsetMin = $"335 {y - 20}", OffsetMax = $"375 {y}"
                },
                Button =
                {
                    Color = "0 0 0 0",
                    Command = "UI_RTM DISPLAYUI"
                },
                Text =
                {
                    Text = _config.DisplayUI.ToString(),
                    Color = _config.DisplayUI ? "0.2 0.6 0.2 1" : "0.6 0.2 0.2 1",
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 15,
                    Align = TextAnchor.MiddleRight
                }
            }, Layer);

            y -= 25;

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "Enable Alerts:",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleLeft
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"10 {y - 20}", OffsetMax = $"350 {y}"
                    }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "0 1",
                    OffsetMin = $"335 {y - 20}", OffsetMax = $"375 {y}"
                },
                Button =
                {
                    Color = "0 0 0 0",
                    Command = "UI_RTM ENABLEALERTS"
                },
                Text =
                {
                    Text = _config.Alerts.ToString(),
                    Color = _config.Alerts ? "0.2 0.6 0.2 1" : "0.6 0.2 0.2 1",
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 15,
                    Align = TextAnchor.MiddleRight
                }
            }, Layer);

            y -= 25;

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "Allow players to close the UI:",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleLeft
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"10 {y - 20}", OffsetMax = $"350 {y}"
                    }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "0 1",
                    OffsetMin = $"335 {y - 20}", OffsetMax = $"375 {y}"
                },
                Button =
                {
                    Color = "0 0 0 0",
                    Command = "UI_RTM ACUIBP"
                },
                Text =
                {
                    Text = _config.AllowToCloseUI.ToString(),
                    Color = _config.AllowToCloseUI ? "0.2 0.6 0.2 1" : "0.6 0.2 0.2 1",
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 15,
                    Align = TextAnchor.MiddleRight
                }
            }, Layer);

            y -= 25;

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "Plugin work only for offline raids:",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleLeft
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"10 {y - 20}", OffsetMax = $"350 {y}"
                    }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "0 1",
                    OffsetMin = $"335 {y - 20}", OffsetMax = $"375 {y}"
                },
                Button =
                {
                    Color = "0 0 0 0",
                    Command = "UI_RTM BOF"
                },
                Text =
                {
                    Text = _config.BlockOnlyOffline.ToString(),
                    Color = _config.BlockOnlyOffline ? "0.2 0.6 0.2 1" : "0.6 0.2 0.2 1",
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 15,
                    Align = TextAnchor.MiddleRight
                }
            }, Layer);

            y -= 25;

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "UI Type:",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleLeft
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"10 {y - 20}", OffsetMax = $"350 {y}"
                    }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "0 1",
                    OffsetMin = $"220 {y - 20}", OffsetMax = $"375 {y}"
                },
                Button =
                {
                    Color = "0 0 0 0",
                    Command = $"UI_RTM UITYPE"
                },
                Text =
                {
                    Text = _config.UIPosition,
                    Color = "0.2 0.6 0.2 1",
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 15,
                    Align = TextAnchor.MiddleRight
                }
            }, Layer);

            y -= 25;

            if (RaidableBases != null)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "[RaidableBases] Allow raiding RB bases in SafeTime:",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"10 {y - 20}", OffsetMax = $"350 {y}"
                        }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"335 {y - 20}", OffsetMax = $"375 {y}"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = "UI_RTM RBAR"
                    },
                    Text =
                    {
                        Text = _config.RaidRBInSafeTime.ToString(),
                        Color = _config.RaidRBInSafeTime ? "0.2 0.6 0.2 1" : "0.6 0.2 0.2 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleRight
                    }
                }, Layer);

                y -= 25;
            }

            if (TruePVE != null)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "[TruePVE] Safe Time RuleSet:",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"10 {y - 20}", OffsetMax = $"350 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.25 0.25 0.25 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"300 {y - 20}", OffsetMax = $"375 {y}"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = "-0.5 -0.5"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = _config.SafeTimeRuleSet,
                            Color = "0.8 0.8 0.8 0.6",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"300 {y - 20}", OffsetMax = $"375 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Command = "UI_RTM TRUEPVESAFE",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter,
                            CharsLimit = 5
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"300 {y - 20}", OffsetMax = $"375 {y}"
                        }
                    }
                });

                y -= 25;

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "[TruePVE] Raid Time RuleSet:",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"10 {y - 20}", OffsetMax = $"350 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.25 0.25 0.25 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"300 {y - 20}", OffsetMax = $"375 {y}"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = "-0.5 -0.5"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = _config.RaidTimeRuleSet,
                            Color = "0.8 0.8 0.8 0.6",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"300 {y - 20}", OffsetMax = $"375 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Command = "UI_RTM TRUEPVERAID",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter,
                            CharsLimit = 5
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"300 {y - 20}", OffsetMax = $"375 {y}"
                        }
                    }
                });

                y -= 25;
            }

            if (ZoneManager != null)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "[ZM] The plugin will work on entities in the zones:",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"10 {y - 20}", OffsetMax = $"350 {y}"
                        }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"220 {y - 20}", OffsetMax = $"375 {y}"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = "UI_RTM ZONEMANAGER"
                    },
                    Text =
                    {
                        Text = _config.ZoneManagerSettings,
                        Color = "0.2 0.6 0.2 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleRight
                    }
                }, Layer);

                y -= 25;

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "[ZM] Zone IDs for Zone Manager(Entering in configuration file)",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"10 {y - 20}", OffsetMax = $"385 {y}"
                        }
                    }
                });

                y -= 25;
            }

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "BLOCKED ACTIONS DURING <color=green>SAFE TIME</color>",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 16,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"10 {y - 20}", OffsetMax = $"350 {y}"
                    }
                }
            });

            y -= 25;

            foreach (var check in _config.RaidBlockSettings)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "[BLOCK]" + check.Key + ":",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"10 {y - 20}", OffsetMax = $"350 {y}"
                        }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"335 {y - 20}", OffsetMax = $"375 {y}"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = $"UI_RTM SETTINGS {check.Key}"
                    },
                    Text =
                    {
                        Text = check.Value.ToString(),
                        Color = check.Value ? "0.2 0.6 0.2 1" : "0.6 0.2 0.2 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleRight
                    }
                }, Layer);

                y -= 25;
            }

            y = -80;

            foreach (var check in _config.BlockByWeekDay)
            {
                var isItDay = check.Key == redactingPage;
                var block = isItDay && check.Value.Count - 1 >= page ? check.Value[page] : check.Value[0];

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = check.Key + ":",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"425 {y - 20}", OffsetMax = $"800 {y}"
                        }
                    }
                });

                if (isItDay && page >= check.Value.Count)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"505 {y - 20}", OffsetMax = $"800 {y}"
                        },
                        Button =
                        {
                            Color = "0 0 0 0",
                            Command = $"UI_RTM ADDNEWRAIDTIME {check.Key} {page}"
                        },
                        Text =
                        {
                            Text = "+",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        }
                    }, Layer);

                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"770 {y - 20}", OffsetMax = $"785 {y}"
                        },
                        Button =
                        {
                            Color = "0 0 0 0",
                            Command = $"UI_RTM OPENNEXTPAGE {check.Key} {page - 1}"
                        },
                        Text =
                        {
                            Text = "<",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        }
                    }, Layer);

                    y -= 25;
                    continue;
                }

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "from",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"505 {y - 20}", OffsetMax = $"800 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.25 0.25 0.25 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"540 {y - 20}", OffsetMax = $"580 {y}"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = "-0.5 -0.5"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = block.StartHours + "H",
                            Color = "0.8 0.8 0.8 0.6",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"540 {y - 20}", OffsetMax = $"580 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Command = $"UI_RTM SSH {check.Key} {page}",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter,
                            CharsLimit = 5
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"540 {y - 20}", OffsetMax = $"580 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = ":",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"580 {y - 20}", OffsetMax = $"600 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.25 0.25 0.25 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"600 {y - 20}", OffsetMax = $"640 {y}"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = "-0.5 -0.5"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = block.StartMinutes + "M",
                            Color = "0.8 0.8 0.8 0.6",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"600 {y - 20}", OffsetMax = $"640 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Command = $"UI_RTM SSM {check.Key} {page}",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter,
                            CharsLimit = 5
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"600 {y - 20}", OffsetMax = $"640 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "to",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"650 {y - 20}", OffsetMax = $"800 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.25 0.25 0.25 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"670 {y - 20}", OffsetMax = $"710 {y}"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = "-0.5 -0.5"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = block.StopHours + "H",
                            Color = "0.8 0.8 0.8 0.6",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"670 {y - 20}", OffsetMax = $"710 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Command = $"UI_RTM SEH {check.Key} {page}",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter,
                            CharsLimit = 5
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"670 {y - 20}", OffsetMax = $"710 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = ":",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"710 {y - 20}", OffsetMax = $"730 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.25 0.25 0.25 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"730 {y - 20}", OffsetMax = $"770 {y}"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = "-0.5 -0.5"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = block.StopMinutes + "M",
                            Color = "0.8 0.8 0.8 0.6",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"730 {y - 20}", OffsetMax = $"770 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Command = $"UI_RTM SEM {check.Key} {page}",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter,
                            CharsLimit = 5
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"730 {y - 20}", OffsetMax = $"770 {y}"
                        }
                    }
                });

                if (isItDay && page > 0)
                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"770 {y - 20}", OffsetMax = $"785 {y}"
                        },
                        Button =
                        {
                            Color = "0 0 0 0",
                            Command = $"UI_RTM OPENNEXTPAGE {check.Key} {page - 1}"
                        },
                        Text =
                        {
                            Text = "<",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        }
                    }, Layer);

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"785 {y - 20}", OffsetMax = $"800 {y}"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = isItDay ? $"UI_RTM OPENNEXTPAGE {check.Key} {page + 1}" : $"UI_RTM OPENNEXTPAGE {check.Key} 1"
                    },
                    Text =
                    {
                        Text = ">",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter
                    }
                }, Layer);

                if (check.Value.Count > 1)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"400 {y - 20}", OffsetMax = $"420 {y}"
                        },
                        Button =
                        {
                            Color = "0.6 0.2 0.2 1",
                            Command = isItDay ? $"UI_RTM REMOVEDAYPAGE {check.Key} {page}" : $"UI_RTM REMOVEDAYPAGE {check.Key} 0",
                            Sprite = "assets/icons/close.png"
                        },
                        Text =
                        {
                            Text = ""
                        }
                    }, Layer);
                }

                y -= 25;
            }

            foreach (var check in _config.BlockByDay)
            {
                var key = RaidBlockDay.GetFromString(check.Key);
                var isItDay = check.Key == redactingPage;
                var block = isItDay && check.Value.Count - 1 >= page ? check.Value[page] : check.Value[0];

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetCorrectDate(key.Year, key.Month, key.Day),
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"425 {y - 20}", OffsetMax = $"800 {y}"
                        }
                    }
                });

                if (isItDay && page >= check.Value.Count)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"505 {y - 20}", OffsetMax = $"800 {y}"
                        },
                        Button =
                        {
                            Color = "0 0 0 0",
                            Command = $"UI_RTM ADDNEWRAIDTIME {check.Key} {page}"
                        },
                        Text =
                        {
                            Text = "+",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        }
                    }, Layer);

                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"770 {y - 20}", OffsetMax = $"785 {y}"
                        },
                        Button =
                        {
                            Color = "0 0 0 0",
                            Command = $"UI_RTM OPENNEXTPAGE {check.Key} {page - 1}"
                        },
                        Text =
                        {
                            Text = "<",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        }
                    }, Layer);

                    y -= 25;
                    continue;
                }

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "from",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"505 {y - 20}", OffsetMax = $"800 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.25 0.25 0.25 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"540 {y - 20}", OffsetMax = $"580 {y}"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = "-0.5 -0.5"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = block.StartHours + "H",
                            Color = "0.8 0.8 0.8 0.6",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"540 {y - 20}", OffsetMax = $"580 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Command = $"UI_RTM DSSH {key.Year} {key.Month} {key.Day} {page}",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter,
                            CharsLimit = 5
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"540 {y - 20}", OffsetMax = $"580 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = ":",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"580 {y - 20}", OffsetMax = $"600 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.25 0.25 0.25 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"600 {y - 20}", OffsetMax = $"640 {y}"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = "-0.5 -0.5"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = block.StartMinutes + "M",
                            Color = "0.8 0.8 0.8 0.6",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"600 {y - 20}", OffsetMax = $"640 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Command = $"UI_RTM DSSM {key.Year} {key.Month} {key.Day} {page}",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter,
                            CharsLimit = 5
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"600 {y - 20}", OffsetMax = $"640 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "to",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleLeft
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"650 {y - 20}", OffsetMax = $"800 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.25 0.25 0.25 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"670 {y - 20}", OffsetMax = $"710 {y}"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = "-0.5 -0.5"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = block.StopHours + "H",
                            Color = "0.8 0.8 0.8 0.6",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"670 {y - 20}", OffsetMax = $"710 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Command = $"UI_RTM DSEH {key.Year} {key.Month} {key.Day} {page}",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter,
                            CharsLimit = 5
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"670 {y - 20}", OffsetMax = $"710 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = ":",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"710 {y - 20}", OffsetMax = $"730 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.25 0.25 0.25 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"730 {y - 20}", OffsetMax = $"770 {y}"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = "-0.5 -0.5"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = block.StopMinutes + "M",
                            Color = "0.8 0.8 0.8 0.6",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"730 {y - 20}", OffsetMax = $"770 {y}"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Command = $"UI_RTM DSEM {key.Year} {key.Month} {key.Day} {page}",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter,
                            CharsLimit = 5
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"730 {y - 20}", OffsetMax = $"770 {y}"
                        }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"400 {y - 20}", OffsetMax = $"420 {y}"
                    },
                    Button =
                    {
                        Color = "0.6 0.2 0.2 1",
                        Command = check.Value.Count == 1 ? $"UI_RTM REMOVEDAY {key.Year} {key.Month} {key.Day}" : isItDay ? $"UI_RTM REMOVEDAYPAGE {check.Key} {page}" : $"UI_RTM REMOVEDAYPAGE {check.Key} 0",
                        Sprite = "assets/icons/close.png"
                    },
                    Text =
                    {
                        Text = ""
                    }
                }, Layer);

                if (isItDay && page > 0)
                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"770 {y - 20}", OffsetMax = $"785 {y}"
                        },
                        Button =
                        {
                            Color = "0 0 0 0",
                            Command = $"UI_RTM OPENNEXTPAGE {check.Key} {page - 1}"
                        },
                        Text =
                        {
                            Text = "<",
                            Color = "1 1 1 1",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        }
                    }, Layer);

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"785 {y - 20}", OffsetMax = $"800 {y}"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = isItDay ? $"UI_RTM OPENNEXTPAGE {check.Key} {page + 1}" : $"UI_RTM OPENNEXTPAGE {check.Key} 1"
                    },
                    Text =
                    {
                        Text = ">",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter
                    }
                }, Layer);

                y -= 25;
            }

            if (_config.BlockByDay.Count < 15)
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"425 {y - 40}", OffsetMax = $"800 {y}"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = $"UI_RTM OPENADDDAYS {time.Year} {time.Month}"
                    },
                    Text =
                    {
                        Text = "+",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 35,
                        Align = TextAnchor.MiddleCenter
                    }
                }, Layer);
            }

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }

        private void ShowRTMInfoAddDays(BasePlayer player, int year, int month)
        {
            var container = new CuiElementContainer();
            var startTime = Time;
            var time = startTime;
            if (time.Year != year) time = time.AddYears(year - time.Year);
            if (time.Month != month) time = time.AddMonths(month - time.Month);
            var amountOfDays = DateTime.DaysInMonth(year, month);
            var isTNDays = amountOfDays >= 29;
            int x = 140;
            int y = -120;

            container.Add(new CuiElement
            {
                Parent = Layer + ".bg",
                Name = Layer,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.2 0.2 0.2 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                        OffsetMin = "-400 -350", OffsetMax = "400 350"
                    }
                }
            });
            Outline(container, Layer, "0.15 0.15 0.15 1", "2");

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetMsg(player?.UserIDString, "UI_RAIDTIMESCHEDULE"),
                        Color = "1 1 1 1",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 35,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = "0 -50", OffsetMax = "800 0"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = time.ToString("MMMMMMMMMMMM"),
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 30,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = "0 -90", OffsetMax = "800 -50"
                    }
                }
            });

            for (var i = 0; i < 7; i++)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player?.UserIDString, $"UI_{DaysOfWeek[i]}"),
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{x} -115", OffsetMax = $"{x + 70} -95"
                        }
                    }
                });

                x += 75;
            }

            x = GetStartPosOfDay(new DateTime(year, month, 1).DayOfWeek);

            for (var i = 1; i < amountOfDays + 1; i++)
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"{x} {y - 70}", OffsetMax = $"{x + 70} {y}"
                    },
                    Button =
                    {
                        Color = "0.3 0.3 0.3 1",
                        FadeIn = 0.05f * i,
                        Command = $"UI_RTM ADDDAY {year} {month} {i}"
                    },
                    Text =
                    {
                        Text = $"{i}",
                        Color = "0.8 0.8 0.8 1",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 20,
                        Align = TextAnchor.MiddleCenter,
                        FadeIn = 0.05f * i,
                    }
                }, Layer, Layer + ".DayButton" + i);
                Outline(container, Layer + ".DayButton" + i, "0 0 0 1", "1.5");

                x += 75;
                if (x < 630) continue;
                x = 140;
                y -= 75;
            }

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = time.Year.ToString(),
                        Color = "1 1 1 1",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 25,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = "500 -90", OffsetMax = "770 -50"
                    }
                }
            });

            if (startTime.Year < year || startTime.Month < month)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiRawImageComponent { Png = GetImage("https://i.imgur.com/dvmwW5c.png") },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = isTNDays ? "75 -330" : "75 -293", OffsetMax = isTNDays ? "125 -280" : "125 -243"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0.1 0.1 0.1 1",
                            Distance = "3 2"
                        }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = isTNDays ? "75 -330" : "75 -293", OffsetMax = isTNDays ? "125 -280" : "125 -243"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = month <= 1 ? $"UI_RTM OPENADDDAYS {year - 1} 12" : $"UI_RTM OPENADDDAYS {year} {month - 1}"
                    },
                    Text =
                    {
                        Text = ""
                    }
                }, Layer);
            }

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiRawImageComponent { Png = GetImage("https://i.imgur.com/afJY7cT.png") },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 1", AnchorMax = "1 1",
                        OffsetMin = isTNDays ? "-125 -330" : "-125 -293", OffsetMax = isTNDays ? "-75 -280" : "-75 -243"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0.1 0.1 0.1 1",
                        Distance = "3 2"
                    }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = isTNDays ? "-125 -330" : "-125 -293", OffsetMax = isTNDays ? "-75 -280" : "-75 -243"
                },
                Button =
                {
                    Color = "0 0 0 0",
                    Command = month >= 12 ? $"UI_RTM OPENADDDAYS {year + 1} 1" : $"UI_RTM OPENADDDAYS {year} {month + 1}"
                },
                Text =
                {
                    Text = ""
                }
            }, Layer);

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "Select the day you want to set the raid time for",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 20,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "1 0.15"
                    }
                }
            });

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIDayInfo(BasePlayer player, int year, int month, int day)
        {
            var container = new CuiElementContainer();
            var time = _config.BlockByDay.FirstOrDefault(x => x.Key == $"{month}/{day}/{year}").Value ?? _config.BlockByWeekDay[new DateTime(year, month, day).DayOfWeek.ToString()];

            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".DayInfo",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "0 0",
                        OffsetMin = "130 20", OffsetMax = "670 130"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".DayInfo",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetCorrectDate(year, month, day),
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 20,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = "-270 -35", OffsetMax = "270 0"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".DayInfo",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.2 0.6 0.2 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "0 0",
                        OffsetMin = "40 15", OffsetMax = "500 20"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0 0 0 1",
                        Distance = "-0.5 -0.5"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".DayInfo",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "0 0",
                        OffsetMin = "39 13", OffsetMax = "41 22"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0 0 0 1",
                        Distance = "-0.5 -0.5"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".DayInfo",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "1 1 1 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "0 0",
                        OffsetMin = "499 13", OffsetMax = "501 22"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0 0 0 1",
                        Distance = "-0.5 -0.5"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".DayInfo",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = TimeString(0, 0, player?.UserIDString),
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "0 0",
                        OffsetMin = "0 20", OffsetMax = "80 55"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".DayInfo",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = TimeString(24, 0, player?.UserIDString),
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 15,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "0 0",
                        OffsetMin = "460 20", OffsetMax = "540 55"
                    }
                }
            });

            if (IsSafeDay(time))
            {
                container.Add(new CuiElement
                {
                    Parent = Layer + ".DayInfo",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "SAFE DAY",
                            Color = "0.2 0.6 0.2 1",
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 25,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "0 0",
                            OffsetMin = "0 30", OffsetMax = "540 80"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = "-0.5 -0.5"
                        }
                    }
                });
            }
            else
            {

                container.Add(new CuiElement
                {
                    Parent = Layer + ".DayInfo",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.2 0.6 0.2 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "0 0",
                            OffsetMin = "140 52", OffsetMax = "160 57"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = "-0.5 -0.5"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer + ".DayInfo",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player?.UserIDString, "UI_SAFE_TIME").ToUpper(),
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "0 0",
                            OffsetMin = "150 30", OffsetMax = "250 80"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer + ".DayInfo",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.6 0.2 0.2 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "0 0",
                            OffsetMin = "290 52", OffsetMax = "310 57"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0 0 0 1",
                            Distance = "-0.5 -0.5"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer + ".DayInfo",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player?.UserIDString, "UI_RAID_TIME"),
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "0 0",
                            OffsetMin = "310 30", OffsetMax = "390 80"
                        }
                    }
                });

                foreach (var check in time)
                {
                    var startPos = (check.StartHours * 60 + check.StartMinutes) / 1440f * 460 + 40;
                    var stopPos = (check.StopHours * 60 + check.StopMinutes) / 1440f * 460 + 40;

                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".DayInfo",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = TimeString(check.StartHours, check.StartMinutes, player?.UserIDString),
                                Color = "1 1 1 1",
                                Font = "robotocondensed-regular.ttf",
                                FontSize = 15,
                                Align = TextAnchor.MiddleCenter
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "0 0",
                                OffsetMin = $"{startPos - 40} -20", OffsetMax = $"{startPos + 40} 15"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".DayInfo",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = TimeString(check.StopHours, check.StopMinutes, player?.UserIDString),
                                Color = "1 1 1 1",
                                Font = "robotocondensed-regular.ttf",
                                FontSize = 15,
                                Align = TextAnchor.MiddleCenter
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "0 0",
                                OffsetMin = $"{stopPos - 40} -20", OffsetMax = $"{stopPos + 40} 15"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".DayInfo",
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = "0.6 0.2 0.2 1"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "0 0",
                                OffsetMin = $"{startPos} 15", OffsetMax = $"{stopPos} 20"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".DayInfo",
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = "1 1 1 1"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "0 0",
                                OffsetMin = $"{startPos - 1} 13", OffsetMax = $"{startPos + 1} 22"
                            },
                            new CuiOutlineComponent
                            {
                                Color = "0 0 0 1",
                                Distance = "-0.5 -0.5"
                            }
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".DayInfo",
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = "1 1 1 1"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "0 0",
                                OffsetMin = $"{stopPos - 1} 13", OffsetMax = $"{stopPos + 1} 22"
                            },
                            new CuiOutlineComponent
                            {
                                Color = "0 0 0 1",
                                Distance = "-0.5 -0.5"
                            }
                        }
                    });
                }
            }

            CuiHelper.DestroyUi(player, Layer + ".DayInfo");
            CuiHelper.AddUi(player, container);
        }

        #endregion

        private void ShowRTMInfo(BasePlayer player, int year, int month, bool isAdmin = false)
        {
            var container = new CuiElementContainer();
            var startTime = Time;
            var time = startTime;
            if (time.Year != year) time = time.AddYears(year - time.Year);
            if (time.Month != month) time = time.AddMonths(month - time.Month);
            var amountOfDays = DateTime.DaysInMonth(year, month);
            var isTNDays = amountOfDays >= 29;
            int x = 140;
            int y = -120;

            container.Add(new CuiElement
            {
                Parent = Layer + ".bg",
                Name = Layer,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.2 0.2 0.2 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                        OffsetMin = "-400 -350", OffsetMax = "400 350"
                    }
                }
            });
            Outline(container, Layer, "0.15 0.15 0.15 1", "2");

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetMsg(player?.UserIDString, "UI_RAIDTIMESCHEDULE"),
                        Color = "1 1 1 1",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 35,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = "0 -50", OffsetMax = "800 0"
                    }
                }
            });

            if (isAdmin)
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = "8 -33", OffsetMax = "33 -8"
                    },
                    Button =
                    {
                        Color = "1 1 1 1",
                        Command = "UI_RTM OPENSETTINGS",
                        Sprite = "assets/icons/gear.png"
                    },
                    Text =
                    {
                        Text = ""
                    }
                }, Layer);
            }

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = GetMsg(player?.UserIDString, $"UI_{time.ToString("MMMMMMMMMMMM")}"),
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 30,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = "0 -90", OffsetMax = "800 -50"
                    }
                }
            });

            for (var i = 0; i < 7; i++)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetMsg(player?.UserIDString, $"UI_{DaysOfWeek[i]}"),
                            Color = "1 1 1 1",
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{x} -115", OffsetMax = $"{x + 70} -95"
                        }
                    }
                });

                x += 75;
            }

            x = GetStartPosOfDay(new DateTime(year, month, 1).DayOfWeek);

            for (var i = 1; i < amountOfDays + 1; i++)
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = $"{x} {y - 70}", OffsetMax = $"{x + 70} {y}"
                    },
                    Button =
                    {
                        Color = "0.3 0.3 0.3 1",
                        FadeIn = 0.05f * i,
                        Command = $"UI_RTM SHOWDAYINFO {year} {month} {i}"
                    },
                    Text =
                    {
                        Text = $"{i}",
                        Color = "0.8 0.8 0.8 1",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 20,
                        Align = TextAnchor.MiddleCenter,
                        FadeIn = 0.05f * i,
                    }
                }, Layer, Layer + ".DayButton" + i);
                Outline(container, Layer + ".DayButton" + i, "0 0 0 1", "1.5");

                x += 75;
                if (x < 630) continue;
                x = 140;
                y -= 75;
            }

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = time.Year.ToString(),
                        Color = "1 1 1 1",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 25,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = "500 -90", OffsetMax = "770 -50"
                    }
                }
            });

            if (startTime.Year < year || startTime.Month < month)
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiRawImageComponent { Png = GetImage("https://i.imgur.com/dvmwW5c.png") },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = isTNDays ? "75 -330" : "75 -293", OffsetMax = isTNDays ? "125 -280" : "125 -243"
                        },
                        new CuiOutlineComponent
                        {
                            Color = "0.1 0.1 0.1 1",
                            Distance = "3 2"
                        }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = isTNDays ? "75 -330" : "75 -293", OffsetMax = isTNDays ? "125 -280" : "125 -243"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = month <= 1 ? $"UI_RTM OPENMONTH {year - 1} 12" : $"UI_RTM OPENMONTH {year} {month - 1}"
                    },
                    Text =
                    {
                        Text = ""
                    }
                }, Layer);
            }

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiRawImageComponent { Png = GetImage("https://i.imgur.com/afJY7cT.png") },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 1", AnchorMax = "1 1",
                        OffsetMin = isTNDays ? "-125 -330" : "-125 -293", OffsetMax = isTNDays ? "-75 -280" : "-75 -243"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0.1 0.1 0.1 1",
                        Distance = "3 2"
                    }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = isTNDays ? "-125 -330" : "-125 -293", OffsetMax = isTNDays ? "-75 -280" : "-75 -243"
                },
                Button =
                {
                    Color = "0 0 0 0",
                    Command = month >= 12 ? $"UI_RTM OPENMONTH {year + 1} 1" : $"UI_RTM OPENMONTH {year} {month + 1}"
                },
                Text =
                {
                    Text = ""
                }
            }, Layer);

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIBackGround(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Parent = "Overlay",
                Name = Layer + ".bg",
                Components =
                {
                    new CuiNeedsKeyboardComponent(),
                    new CuiNeedsCursorComponent(),
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0.8",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer + ".bg",
                Name = Layer + ".closeBTN",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.2 0.2 0.2 0.9",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 1", AnchorMax = "1 1",
                        OffsetMin = "-80 -80", OffsetMax = "-10 -10"
                    }
                }
            });

            Outline(container, Layer + ".closeBTN", "0.5 0.5 0.5 1", "2.5");

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = "-70 -70", OffsetMax = "-20 -20"
                },
                Button =
                {
                    Color = "0.5 0.5 0.5 1",
                    Close = Layer + ".bg",
                    Sprite = "assets/icons/close.png"
                },
                Text =
                {
                    Text = ""
                }
            }, Layer + ".bg");

            CuiHelper.DestroyUi(player, Layer + ".bg");
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIAlert(BasePlayer player, string text)
        {
            if (!_config.Alerts || AlertCooldown.Contains(player.userID)) return;
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-150 -150", OffsetMax = "150 -35" },
                Image = { Color = "0.2 0.2 0.2 1", FadeIn = 2 },
                FadeOut = 2f
            }, "Overlay", Layer + ".alert");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -25", OffsetMax = "300 0" },
                Image = { Color = "0.9 0.2 0.2 1", FadeIn = 2 },
                FadeOut = 2f
            }, Layer + ".alert", Layer + ".alert1");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "108 -23", OffsetMax = "129 -2" },
                Image = { Color = "1 1 1 1", FadeIn = 2, Sprite = "assets/icons/warning.png" },
                FadeOut = 2f
            }, Layer + ".alert", Layer + ".alert2");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "20 -25", OffsetMax = "300 0" },
                Text =
                {
                    Text = GetMsg(player?.UserIDString, "UI_ALERT"), Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1", FadeIn = 2
                },
                FadeOut = 2f
            }, Layer + ".alert", Layer + ".alert3");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-24 -24", OffsetMax = "-1 -1" },
                Button = { Color = "1 1 1 1", Command = "UI_RTMCLOSEALERT", Sprite = "assets/icons/vote_down.png", FadeIn = 2f },
                Text = { Text = "" },
                FadeOut = 2f
            }, Layer + ".alert", Layer + ".alert4");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -115", OffsetMax = "300 -25" },
                Text =
                {
                    Text = text, Font = "robotocondensed-regular.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1", FadeIn = 2
                },
                FadeOut = 2f
            }, Layer + ".alert", Layer + ".alert5");

            DestroyAlert(player);
            CuiHelper.AddUi(player, container);
            AlertCooldown.Add(player.userID);
            timer.In(5, () => DestroyAlert(player));
        }

        private void DestroyAlert(BasePlayer player)
        {
            if (player == null) return;
            CuiHelper.DestroyUi(player, Layer + ".alert1");
            CuiHelper.DestroyUi(player, Layer + ".alert2");
            CuiHelper.DestroyUi(player, Layer + ".alert3");
            CuiHelper.DestroyUi(player, Layer + ".alert4");
            CuiHelper.DestroyUi(player, Layer + ".alert5");
            CuiHelper.DestroyUi(player, Layer + ".alert");
            if (AlertCooldown.Contains(player.userID)) AlertCooldown.Remove(player.userID);
        }

        private void Outline(CuiElementContainer container, string parent, string color = "1 1 1 1", string size = "1")
        {
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = $"0 0", OffsetMax = $"0 {size}" },
                Image = { Color = color }
            }, parent);
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{size}", OffsetMax = $"0 0" },
                Image = { Color = color }
            }, parent);
            container.Add(new CuiPanel
            {
                RectTransform =
                    { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = $"0 {size}", OffsetMax = $"{size} -{size}" },
                Image = { Color = color }
            }, parent);
            container.Add(new CuiPanel
            {
                RectTransform =
                    { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = $"-{size} {size}", OffsetMax = $"0 -{size}" },
                Image = { Color = color }
            }, parent);
        }

        #endregion

        #region Language

        private void SendMessage(BasePlayer player, string msg, params object[] args) => Player.Message(player, GetMsg(player?.UserIDString, msg, args));

        private string GetMsg(string player, string msg, params object[] args) =>
            string.Format(lang.GetMessage(msg, this, player), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UI_ALERT"] = "ALERT",
                ["UI_TURRETBLOCK"] = "You cannot place turrets in double TC radius during safe time",
                ["UI_LADDERBLOCK"] = "You may not place ladders in a Building Block during safe times.",
                ["UI_CEILINGBLOCK"] = "You cannot place ceilings in the Building Block during safe times.",
                ["UI_CANDAMAGE"] = "You cannot damage other people's buildings during safe times",
                ["UI_CANDAMAGEOWN"] = "You cannot damage own buildings during safe times",
                ["UI_START_RAID_TIME"] = "Raid time has begun! All restrictions removed",
                ["UI_STOP_RAID_TIME"] = "The raid time is over. Restrictions are in effect!",
                ["CM_CLOSE_HUD"] = "You closed the RTM HUD, to open it again use the /rtm hud command",
                ["UI_RAIDTIMESCHEDULE"] = "RAID TIME SCHEDULE",
                ["UI_SAFE_TIME"] = "Safe Time",
                ["UI_RAID_TIME"] = "Raid Time",
                ["UI_Monday"] = "Monday",
                ["UI_Tuesday"] = "Tuesday",
                ["UI_Wednesday"] = "Wednesday",
                ["UI_Thursday"] = "Thursday",
                ["UI_Friday"] = "Friday",
                ["UI_Saturday"] = "Saturday",
                ["UI_Sunday"] = "Sunday",
                ["UI_AM"] = "AM",
                ["UI_PM"] = "PM",
                ["UI_January"] = "January",
                ["UI_February"] = "February",
                ["UI_March"] = "March",
                ["UI_April"] = "April",
                ["UI_May"] = "May",
                ["UI_June"] = "June",
                ["UI_July"] = "July",
                ["UI_August"] = "August",
                ["UI_September"] = "September",
                ["UI_October"] = "October",
                ["UI_November"] = "November",
                ["UI_December"] = "December",
            }, this);

        }

        #endregion
    }
}