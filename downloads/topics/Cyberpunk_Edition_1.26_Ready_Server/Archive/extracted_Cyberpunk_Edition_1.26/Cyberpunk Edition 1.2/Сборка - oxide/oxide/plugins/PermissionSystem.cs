using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PermissionSystem","Netrunner","1.0")]
    public class PermissionSystem : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ImageLibrary;
        private static PermissionSystem _;
        private PermisssionController PermissionManager = new PermisssionController();

        #endregion

        #region Config

        class ViewSetting
        {
            public string DisplayName = "Permission";

            public string Description = "";

            public string Image = "";
        }
        
        static Configuration config = new Configuration();

        class Configuration
        {
            [JsonProperty("Настройка отображаемых привилегий и групп")] 
            public Dictionary<string, ViewSetting> ViewablePermission;
            
            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    ViewablePermission = new Dictionary<string, ViewSetting>
                    {
                        ["uchat.vip"] = new ViewSetting()
                    }
                    
                };
            }
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) PrintWarning("NULL");
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintError($"Не удалось найти конфигурацию 'oxide/config/{Name}', Создание конфига!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }
        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Data
        Dictionary<ulong,PermissionData> _datebase = new Dictionary<ulong, PermissionData>();

        void InitPlayerData(BasePlayer player)
        {
            if (!_datebase.ContainsKey(player.userID))
            {
                _datebase.Add(player.userID,new PermissionData());
                _datebase[player.userID].GetPermanent(player.UserIDString);
                PrintWarning($"Player {player} Inited");
                PrintWarning($"Player {_datebase[player.userID].Permanent.Count} Inited");
            }
            
        }

        class PermissionData
        {
            public Dictionary<string,DateTime>PermissionsTime = new Dictionary<string, DateTime>();
            
            public Dictionary<string,DateTime>GroupsTime = new Dictionary<string, DateTime>();
            
            public List<string>Expired = new List<string>();
            
            public List<string>Permanent = new List<string>();

            public void RefreshTime(string userid)
            {
                RefreshPermisisonsTime(userid);
                RefreashGroupsTime(userid);
            }
            public void GetPermanent(string userid)
            {
                foreach (var permission in _.permission.GetUserPermissions(userid))
                {
                    if (!PermissionsTime.ContainsKey(permission))
                    {
                        Permanent.Add(permission);
                    }
                    Permanent.AddRange(_.permission.GetGroups());
                }
                foreach (var group in _.permission.GetUserGroups(userid))
                {
                    if (!GroupsTime.ContainsKey(group))
                    {
                        Permanent.Add(group);
                    }
                }
            }

            #region Permissions

            void RefreshPermisisonsTime(string userid)
            {
                foreach (var permissionTime in PermissionsTime.ToList())
                {
                    _.PrintWarning($"{permissionTime.Value} {DateTime.UtcNow} {permissionTime.Value<DateTime.UtcNow}");
                    if (permissionTime.Value<DateTime.UtcNow)
                    {
                        RevokePermission(userid,permissionTime.Key);
                    }
                }
                
            }

            public void AddPermission(string userid, string perm, TimeSpan expireTime)
            {
                if (!PermissionsTime.ContainsKey(perm))
                {
                    PermissionsTime.Add(perm,DateTime.UtcNow.Add(expireTime));
                    _.permission.GrantUserPermission(userid,perm,null);
                }
                else
                {
                    PermissionsTime[perm].Add(expireTime);
                }
            }

            public void ReducePermissionTime(string userid, string perm, TimeSpan expireTime)
            {
                if (!PermissionsTime.ContainsKey(perm))
                {
                    return;
                }
                else
                {
                    PermissionsTime[perm].Add(-expireTime);
                    RefreshPermisisonsTime(userid);
                }
            }

            public void RevokePermission(string userid, string perm)
            {
                if (PermissionsTime.ContainsKey(perm))
                {
                    _.permission.RevokeUserPermission(userid,perm);
                    PermissionsTime.Remove(perm);
                    _.PrintWarning($"{perm} revoked");
                }
                
            }

            

            #endregion

            #region Groups

            void RefreashGroupsTime(string userid)
            {
                foreach (var groupTime in GroupsTime.ToList())
                {
                    if (groupTime.Value<DateTime.UtcNow)
                    {
                        RevokeGroup(userid,groupTime.Key);
                    }
                    
                }
            }
            
            public void AddGroup(string userid, string group, TimeSpan expireTime)
            {
                if (!GroupsTime.ContainsKey(group))
                {
                    GroupsTime.Add(group,DateTime.UtcNow.Add(expireTime));
                    _.permission.GrantGroupPermission(userid,group,null);
                }
                else
                {
                    GroupsTime[group].Add(expireTime);
                }
            }

            public void ReduceGroupTime(string userid, string group, TimeSpan expireTime)
            {
                if (!GroupsTime.ContainsKey(group))
                {
                    return;
                }
                else
                {
                    GroupsTime[group].Add(-expireTime);
                    RefreashGroupsTime(userid);
                }
            }

            public void RevokeGroup(string userid, string group)
            {
                if (GroupsTime.ContainsKey(group))
                {
                    _.permission.RemoveUserGroup(userid,group);
                    GroupsTime.Remove(group);
                }
                
            }

            #endregion
        }

        void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(Name)) _datebase = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PermissionData>>(Name);
        }

        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name,_datebase);
        }

        #region PermissionInterface

        

        class PermisssionController
        {
            public void GivePermission(ulong userid, string perm, string time)
            {
                if (!_._datebase.ContainsKey(userid))
                {
                    _.PrintWarning("Player not Found");
                    return;
                }
                _.PrintWarning($"Trying give {perm} to {userid}, for {time}");
                _._datebase[userid].AddPermission(userid.ToString(),perm,_.FormatTimeFromString(time));
            }

            void RevokePermission(ulong userid, string perm)
            {
                if (!_._datebase.ContainsKey(userid))
                {
                    _.PrintWarning("Player not Found");
                    return;
                }
                _._datebase[userid].RevokePermission(userid.ToString(),perm);
            }

            public void GiveGroup(ulong userid, string perm, string time)
            {
                if (!_._datebase.ContainsKey(userid))
                {
                    _.PrintWarning("Player not Found");
                    return;
                }
                _._datebase[userid].AddGroup(userid.ToString(),perm,_.FormatTimeFromString(time));
            }

            void RevokeGroup(ulong userid, string perm)
            {
                if (!_._datebase.ContainsKey(userid))
                {
                    _.PrintWarning("Player not Found");
                    return;
                }
                _._datebase[userid].RevokeGroup(userid.ToString(),perm);
            }

            void CheckTime()
            {
                foreach (var playerdata in _._datebase.ToList()) playerdata.Value.RefreshTime(playerdata.Key.ToString());
            }
            public void StartCheckTime()
            {
                InvokeHandler.Instance.InvokeRepeating(CheckTime,3,1);
            }
        }

        #endregion

        #endregion

        #region Hooks

        void OnServerInitialized()
        {
            _ = this;
            foreach (var player in BasePlayer.activePlayerList)
            {
                InitPlayerData(player);
            }
            InitImages();
            PermissionManager.StartCheckTime();
            
        }

        void OnPlayerConnected(BasePlayer player)
        {
            InitPlayerData(player);
        }

        #endregion

        #region Methods

        void InitImages()
        {
            foreach (var perm in config.ViewablePermission) ImageLibrary.Call("AddImage", perm.Value.Image, perm.Key);
        }
        
        private Regex _timeSpanPattern =
            new Regex(
                @"(?:(?<days>\d{1,3})d)?(?:(?<hours>\d{1,3})h)?(?:(?<minutes>\d{1,3})m)?(?:(?<seconds>\d{1,3})s)?",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        TimeSpan FormatTimeFromString(string text)
        {
            var match = _timeSpanPattern.Match(text);
            if (!match.Success) return new TimeSpan();

            if (!match.Groups[0].Value.Equals(text)) return new TimeSpan();
            
            Group daysGroup = match.Groups["days"];
            Group hoursGroup = match.Groups["hours"];
            Group minutesGroup = match.Groups["minutes"];
            Group secondsGroup = match.Groups["seconds"];

            int days = daysGroup.Success
                ? int.Parse(daysGroup.Value)
                : 0;
            int hours = hoursGroup.Success
                ? int.Parse(hoursGroup.Value)
                : 0;
            int minutes = minutesGroup.Success
                ? int.Parse(minutesGroup.Value)
                : 0;
            int seconds = secondsGroup.Success
                ? int.Parse(secondsGroup.Value)
                : 0;

            TimeSpan time = new TimeSpan(days, hours, minutes, seconds);
            if (days + hours + minutes + seconds == 0) return new TimeSpan();
            return time;
        }

        #endregion

        #region UI

        private string _layer = "PermissionUI";

        class ViewablePerm
        {
            public string Name;

            public string Time;
           
        }

        string BuildTimeText(DateTime time)
        {
            
            TimeSpan timeSpan = new TimeSpan(time.Ticks-DateTime.UtcNow.Ticks);
            string text = "";
            if (timeSpan.Days>4)
            {
                text += $"{timeSpan.Days} Дней ";
            }
            if (timeSpan.Days>4&&timeSpan.Days<5)
            {
                text += $"{timeSpan.Days} Дня ";
            }
            if (timeSpan.Days== 1)
            {
                text += $"1 День ";
            }

            if (timeSpan.Hours>4)
            {
                text += $"{timeSpan.Hours} Часов ";
            }
            if (timeSpan.Hours>4&&timeSpan.Hours<5)
            {
                text += $"{timeSpan.Hours} Часа ";
            }
            if (timeSpan.Hours== 1)
            {
                text += $"1 Чам ";
            }
            if (timeSpan.Minutes>4)
            {
                text += $"{timeSpan.Hours} Минут ";
            }
            if (timeSpan.Minutes>4&&timeSpan.Minutes<5)
            {
                text += $"{timeSpan.Minutes} Минуты ";
            }
            if (timeSpan.Minutes== 1)
            {
                text += $"1 Минута ";
            }

            PrintWarning(text);
            return text;
        }
        Dictionary<string, ViewablePerm> GetPlayerViewablePerms(BasePlayer player)
        {
            Dictionary<string, ViewablePerm> viewablePerms = new Dictionary<string, ViewablePerm>();

            foreach (var perm in _datebase[player.userID].PermissionsTime)
            {
                if (config.ViewablePermission.ContainsKey(perm.Key))
                {
                    viewablePerms.Add(perm.Key,new ViewablePerm
                    {
                        Name = config.ViewablePermission[perm.Key].DisplayName,
                        Time = BuildTimeText(perm.Value)
                    });
                }
            }
            
            foreach (var perm in _datebase[player.userID].GroupsTime)
            {
                if (config.ViewablePermission.ContainsKey(perm.Key))
                {
                    viewablePerms.Add(perm.Key,new ViewablePerm
                    {
                        Name = config.ViewablePermission[perm.Key].DisplayName,
                        Time = BuildTimeText(perm.Value)
                    });
                }
            }

            return viewablePerms;
        }

        void MainUI(BasePlayer player, int page = 0)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                RectTransform = {AnchorMin = "0.5 0.5",AnchorMax = "0.5 0.5",OffsetMin = "-215 -190",OffsetMax = "215 190"}
            }, "ContentUI", _layer);


            Dictionary<string,ViewablePerm> playerPerms = GetPlayerViewablePerms(player);

            double height = 185, xmargin = 5, lenght = 100, ymargin = 10,xpos = 0,ypos = -height;
            int count = 1;
            foreach (var uiperm in playerPerms.Skip(page*8).Take(8))
            {
                container.Add(new CuiPanel
                {
                    Image = {Color = "0 0 0 0"},
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = $"{xpos} {ypos}",OffsetMax = $"{xpos+lenght} {ypos+height}"}
                }, _layer,uiperm.Key);
                container.Add(new CuiElement
                {
                    Parent = uiperm.Key,
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = "5 -95",OffsetMax = "90 -5"
                        },
                        new CuiRawImageComponent
                        {
                            Png = (string) ImageLibrary.Call("GetImage",uiperm.Key)
                        }
                    }
                });
                container.Add(new CuiLabel
                {
                    Text = {Text = uiperm.Value.Name,FontSize = 21,Align = TextAnchor.MiddleCenter},
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "1 1",OffsetMin = "0 -130",OffsetMax = "0 -95"}
                }, uiperm.Key);
                container.Add(new CuiLabel
                {
                    Text = {Text = $"Истекает через:\n{uiperm.Value.Time}",FontSize = 14,Align = TextAnchor.MiddleCenter},
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "1 1",OffsetMin = "0 -180",OffsetMax = "0 -130"}
                }, uiperm.Key);

                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "1 1",OffsetMin = "0 -200",OffsetMax = "0 -180"},
                    Text = {Text = "подробнее",Align = TextAnchor.MiddleCenter,FontSize = 16},
                    Button = {Color = "0 0 0 0"}
                }, uiperm.Key);
                xpos += lenght + xmargin;
                if (count>3)
                {
                    ypos -= height + ymargin;
                    xpos = 0;
                }
            }

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Commands

        [ChatCommand("ptt")]
        void TestCommand(BasePlayer player, string command, string[] args)
        {
            SendReply(player,"Permission:");
            foreach (var perm in _datebase[player.userID].PermissionsTime)
            {
                SendReply(player,$"{perm.Key} {new TimeSpan(perm.Value.Ticks-DateTime.UtcNow.Ticks)}");
            }
            SendReply(player,"Group:");
            foreach (var perm in _datebase[player.userID].GroupsTime)
            {
                SendReply(player,$"{perm.Key} {new TimeSpan(perm.Value.Ticks-DateTime.UtcNow.Ticks)}");
            }

            /*SendReply(player,"Permanent:");
            foreach (var perm in _datebase[player.userID].Permanent)
            {
                SendReply(player,perm);
            }*/
            
        }

        [ConsoleCommand("permsui")]
        void OpenPermUI(ConsoleSystem.Arg arg)
        {
            MainUI(arg.Player());
        }

        [ConsoleCommand("pmgivepermission")]
        void GivePermissionCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs(2))
            {
                return;
            }

            PrintWarning(arg.Args[0]);
            PrintWarning(arg.Args[1]);
            PrintWarning(arg.Args[2]);
            if (arg.Args.Length>2) PermissionManager.GivePermission(Convert.ToUInt64(arg.Args[0]),arg.Args[1],arg.Args[2]);
            else
                permission.GrantUserPermission(arg.Args[0],arg.Args[1],null);
        }
        [ConsoleCommand("pmgivegroup")]
        void GiveGroupCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs(2))
            {
                return;
            }
            
            

            if (arg.Args.Length>2) PermissionManager.GiveGroup(Convert.ToUInt64(arg.Args[0]),arg.Args[1],arg.Args[2]);
            else
                permission.GrantGroupPermission(arg.Args[0],arg.Args[1],null);
        }

        [ConsoleCommand("apm")]
        void TestGiveCmd(ConsoleSystem.Arg arg)
        {
            PermissionManager.GivePermission(76561198088771133, "vehileshop.admin", "1h2m3s");
        }
        
        

        #endregion
    }
}