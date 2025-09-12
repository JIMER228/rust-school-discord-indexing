using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Facepunch.Extend;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PermView","Baks","1.1")]
    public class PermView: RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ImageLibrary,TimedPermissions;
        
        private string back = "https://i.ibb.co/GxnS2x1/111.png";

        #endregion

        #region Config

        class PermSetting
        {
            [JsonProperty("Имя")]
            public string Name;
            [JsonProperty("Картинка")]
            public string Image;
        }
        
        static Configuration config = new Configuration();

        class Configuration
        {
            [JsonProperty("Настройка")] 
            public Dictionary<string, PermSetting> Settings;
            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    Settings = new Dictionary<string, PermSetting>{["vip"] = new PermSetting
                    {
                        Image = "",
                        Name = "ВИП"
                    }}
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

        private string _fileName = "PermView/Players";

        Dictionary<ulong,Dictionary<string,DateTime>>_PlayerPermissions = new Dictionary<ulong, Dictionary<string, DateTime>>();

        void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(_fileName, _PlayerPermissions);

        void ReadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(_fileName))
            {
                _PlayerPermissions =
                    Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string, DateTime>>>(
                        _fileName);
            }
        }

        #endregion

        #region Hooks

        void OnServerInitialized()
        {
            foreach (var setting in config.Settings) ImageLibrary.Call("AddImage", setting.Value.Image, setting.Value.Image);
            ImageLibrary.Call("AddImage", back, back);
            ReadData();
            foreach (var player in BasePlayer.activePlayerList) OnPlayerConnected(player);
            SaveData();
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (!_PlayerPermissions.ContainsKey(player.userID)) _PlayerPermissions.Add(player.userID,new Dictionary<string, DateTime>());
        }

        void OnTimedPermissionGranted(string Id, string permission, TimeSpan duration)
        {
            if (config.Settings.ContainsKey(permission))
            {
                ulong playerID = UInt64.Parse(Id);    
                _PlayerPermissions[playerID].Add(permission,DateTime.Now+duration);
                SaveData();
            }
            
        }

        void OnTimedGroupAdded(string Id, string group, TimeSpan duration)
        {
            if (config.Settings.ContainsKey(group))
            {
                ulong playerID = UInt64.Parse(Id);    
                _PlayerPermissions[playerID].Add(group,DateTime.Now+duration);
                SaveData();
                PrintWarning($"До конца {group} Осталось {duration} ({DateTime.Now+duration}");
            }
        }

        void OnTimedGroupExtended(string Id, string group, TimeSpan duration)
        {
            ulong playerID = UInt64.Parse(Id);
            if (config.Settings.ContainsKey(group) && _PlayerPermissions[playerID].ContainsKey(group))
            {
                _PlayerPermissions[playerID].Remove(group);
                SaveData();
            }
        }

        void OnTimedPermissionExtended(string Id, string permission, TimeSpan duration)
        {
            ulong playerID = UInt64.Parse(Id);
            if (config.Settings.ContainsKey(permission) && _PlayerPermissions[playerID].ContainsKey(permission))
            {
                _PlayerPermissions[playerID].Remove(permission);
                SaveData();
            }
        }
        
        

        #endregion

        #region Methods
        
        string FormatTimeSpan(TimeSpan timeSpan)
        {
            //PrintToChat($"{timeSpan} {timeSpan.Days} {timeSpan.Hours} {timeSpan.Minutes}");
            string text = "";
            PrintWarning(timeSpan.Days.ToString());
            text += $"Дней:{timeSpan.Days}\n";
            text += $"Часов:{timeSpan.Hours}\n";
            text += $"Минут:{timeSpan.Minutes}\n";
            //text += $"Секунд:{timeSpan.Seconds}\n";
            /*if (timeSpan.Days > 0)
            {
                text += $"Дней:{timeSpan.Days}\n";
            }
            if (timeSpan.Hours > 0)
            {
                text += $"Часов:{timeSpan.Hours}\n";
            }
            if (timeSpan.Minutes > 0)
            {
                text += $"Минут:{timeSpan.Minutes}\n";
            }
            if (timeSpan.Seconds > 0)
            {
                text += $"Секунд:{timeSpan.Seconds}\n";
            }*/

            return text;
        }

        #endregion

        #region UI

        
        private string _layer = "PermUI";

        void OpenUI(BasePlayer player, int page = 0)
        {
            CuiElementContainer container = new CuiElementContainer();
            if (!_PlayerPermissions.ContainsKey(player.userID)) _PlayerPermissions.Add(player.userID,new Dictionary<string, DateTime>());

            
            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                RectTransform = {AnchorMin = "0 0",AnchorMax = "1 1"}
            }, "ContentUI", _layer);

            double panelLenght = 500;
            double startx = 0, starty = -3;
            double panelHeight = 120;
            double imgLenght = 118;

            Dictionary<string,DateTime> playerPerms = _PlayerPermissions[player.userID];
            foreach (var perm in playerPerms)
                if (perm.Value<DateTime.UtcNow) playerPerms.Remove(perm.Key);
            if (playerPerms == null || playerPerms.Count<1)
            {
                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 0",AnchorMax = "1 1"},
                    Text = {Text = "У вас нет активных привилегий",Align = TextAnchor.MiddleCenter,Color = "1 1 1 0.98",FontSize = 32},
                }, _layer);
                container.Add(new CuiElement
                {
                    Parent = _layer,
                    Components =
                    {
                        new CuiRectTransformComponent{AnchorMin = "0 0",AnchorMax = "1 1"},
                        new CuiTextComponent{Text = "У вас нет активных привилегий",Align = TextAnchor.MiddleCenter,Color = "1 1 1 0.98",FontSize = 32},
                        new CuiOutlineComponent{Color = "0 0 0 1",Distance = "3 3",UseGraphicAlpha = true}
                    }
                });
            }
            else
            {
                int x = 0;
                int i = 0;
                foreach (var perm in playerPerms.Skip(page*8).Take(8))
                {
                    container.Add(new CuiPanel
                    {
                        Image = {Color = "0 0 0 0"},
                        RectTransform = {AnchorMin = "0 1",AnchorMax = "0 1", OffsetMin = $"{startx} {starty-panelHeight}",OffsetMax = $"{startx+panelLenght} {starty}"}
                    }, _layer, $"PermPanel{i}");
                    
                    container.Add(new CuiElement
                    {
                        Parent = $"PermPanel{i}",
                        Components =
                        {
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",AnchorMax = "1 1"
                            },
                            new CuiRawImageComponent{Png = (string) ImageLibrary.Call("GetImage",back)}
                        }
                    });


                    container.Add(new CuiElement
                    {
                        Parent = $"PermPanel{i}",
                        Components =
                        {
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = $"2 2",OffsetMax = $"{imgLenght} {imgLenght}"
                            },
                            new CuiRawImageComponent{Png = (string) ImageLibrary.Call("GetImage",config.Settings[perm.Key].Image)}
                        }
                    });

                    /*container.Add(new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = "80 -40",OffsetMax = $"{500-imgLenght} -2"},
                        Text = {Text = $"{config.Settings[perm.Key].Name}",Align = TextAnchor.UpperCenter,Color = "1 1 1 0.9",FontSize = 21}
                    }, $"PermPanel{i}");*/
                    container.Add(new CuiElement
                    {
                        Parent = $"PermPanel{i}",
                        Components =
                        {
                            new CuiRectTransformComponent{AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = "80 -40",OffsetMax = $"{500-imgLenght} -2"},
                            new CuiTextComponent{Text = $"{config.Settings[perm.Key].Name}",Align = TextAnchor.UpperCenter,Color = "1 1 1 0.9",FontSize = 21},
                            new CuiOutlineComponent{Color = "0 0 0 1",Distance = "2 2",UseGraphicAlpha = true}
                        }
                    });
                    /*container.Add(new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = $"80 {-panelHeight+10}",OffsetMax = $"{500-imgLenght} -20"},
                        Text = {Text = $"До конца привилегии \n{FormatTimeSpan(TimeSpan.FromMinutes( perm.Value.TimeOfDay.TotalMinutes))}",Align = TextAnchor.LowerLeft,Color = "1 1 1 0.9",FontSize = 18}
                    }, $"PermPanel{i}","PermText");*/
                    container.Add(new CuiElement
                    {
                        Parent = $"PermPanel{i}",
                        Components =
                        {
                            new CuiRectTransformComponent{AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = $"125 {-panelHeight+10}",OffsetMax = $"{500-imgLenght} -20"},
                            new CuiTextComponent{Text = $"До конца привилегии \n{FormatTimeSpan(TimeSpan.FromSeconds( perm.Value.TimeOfDay.TotalSeconds))}",Align = TextAnchor.LowerLeft,Color = "1 1 1 0.9",FontSize = 18},
                            new CuiOutlineComponent{Color = "0 0 0 1",Distance = "2 2",UseGraphicAlpha = true}
                        }
                    });
                    i++;
                    starty -= panelHeight + 5;
                    x++;
                    if (x == 4)
                    {
                        starty = -3;
                        startx += panelLenght+5;
                    }
                }

                if (playerPerms.Count>(page+1)*8)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "120 -50",OffsetMax = "200 -10"},
                        Button = {Close = _layer,Color = "0.27 0.27 0.27 0.9",Command = $"viewperms {page+1}"},
                        Text = {Text = "Вперед",Align = TextAnchor.MiddleCenter}
                    }, _layer);
                }
                if (page>0)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "20 -50",OffsetMax = "100 -10"},
                        Button = {Close = _layer,Color = "0.27 0.27 0.27 0.9",Command = $"viewperms {page-1}"},
                        Text = {Text = "Назад",Align = TextAnchor.MiddleCenter}
                    }, _layer);
                }
            }

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Command

        [ConsoleCommand("viewperms")]
        void OpenPlayerUI(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs())
            {
                OpenUI(arg.Player());
            }
            else
            {
                OpenUI(arg.Player(),arg.Args[0].ToInt());
            }
        }

        [ChatCommand("pclear")]
        void PermCommand(BasePlayer player, string command, string[] args)
        {
            _PlayerPermissions.Remove(player.userID);
            SaveData();
            OnServerInitialized();
        }

        #endregion
    }
}