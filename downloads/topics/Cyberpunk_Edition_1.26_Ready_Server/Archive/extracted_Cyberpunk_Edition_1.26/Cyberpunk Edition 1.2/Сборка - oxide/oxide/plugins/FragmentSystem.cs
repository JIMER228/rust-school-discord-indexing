using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Facepunch.Extend;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("FragmentSystem","Netrunner","1.0")]
    public class FragmentSystem : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ImageLibrary,Broadcast;
        Dictionary<string,Fragment> _allFragments = new Dictionary<string, Fragment>();
        List<uint> _openedContainers = new List<uint>();

        #endregion

        #region Config

        class Fragment
        {
            [JsonProperty("Название фрагмента")] 
            public string DisplayName;
            [JsonProperty("Картинка фрагмента")] 
            public string Image;
        }

        

        class FragmentItem
        {
            [JsonProperty("Треюуемые фрагменты")]
            public Dictionary<string,int> requiredFragments = new Dictionary<string, int>();
            [JsonProperty("Название привилегии")] 
            public string DisplayName;
            [JsonProperty("Описание привилегии")] 
            public string Description;
            [JsonProperty("Картинка привилегии")] 
            public string Image;
            [JsonProperty("Команда привилегии")] 
            public string Command;
        }
        
        
        static Configuration config = new Configuration();

        class Configuration
        {
            [JsonProperty("Шанс выпадения фрагмента")]
            public int DropChance = 50;
            [JsonProperty("Настройка фрагментов")]
            public Dictionary<string,Fragment> Fragments = new Dictionary<string, Fragment>();
            [JsonProperty("Настройка предметов")] 
            public Dictionary<string,FragmentItem> Settings;

            
            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    Fragments = new Dictionary<string, Fragment>
                    {
                        
                        ["p0"] = new Fragment
                        {
                            DisplayName = "Fragment",
                            Image = "https://rustlabs.com/img/items180/gunpowder.png"
                        },
                        ["p1"] = new Fragment
                        {
                            DisplayName = "Fragment",
                            Image = "https://rustlabs.com/img/items180/gunpowder.png"
                        },
                        ["p2"] = new Fragment
                        {
                            DisplayName = "Fragment",
                            Image = "https://rustlabs.com/img/items180/gunpowder.png"
                        },
                        ["p3"] = new Fragment
                        {
                            DisplayName = "Fragment",
                            Image = "https://rustlabs.com/img/items180/gunpowder.png"
                        },
                    },
                    Settings = new Dictionary<string,FragmentItem>
                    {
                        ["f0"] = new FragmentItem
                        {
                            DisplayName = "Сера",
                            Description = "",
                            Image = "https://rustlabs.com/img/items180/sulfur.png",
                            Command = "give userid sulfur 100",
                            requiredFragments = new Dictionary<string, int>
                            {
                                ["p0"] = 4,
                                ["p1"] = 3,
                                ["p2"] = 2,
                                ["p3"] = 2
                            }
                        },
                        ["f1"] = new FragmentItem
                        {
                            DisplayName = "Сера",
                            Description = "",
                            Image = "https://rustlabs.com/img/items180/sulfur.png",
                            Command = "give userid sulfur 100",
                            requiredFragments = new Dictionary<string, int>
                            {
                                ["p0"] = 4,
                                ["p1"] = 3,
                                ["p2"] = 2,
                                ["p3"] = 2
                            }
                        },
                        ["f2"] = new FragmentItem
                        {
                            DisplayName = "Сера",
                            Description = "",
                            Image = "https://rustlabs.com/img/items180/sulfur.png",
                            Command = "give userid sulfur 100",
                            requiredFragments = new Dictionary<string, int>
                            {
                                ["p0"] = 4,
                                ["p1"] = 3,
                                ["p2"] = 2,
                                ["p3"] = 2
                            }
                        },
                        ["f3"] = new FragmentItem
                        {
                            DisplayName = "Сера",
                            Description = "",
                            Image = "https://rustlabs.com/img/items180/sulfur.png",
                            Command = "give userid sulfur 100",
                            requiredFragments = new Dictionary<string, int>
                            {["p0"] = 4,
                                ["p1"] = 3,
                                ["p2"] = 2,
                                ["p3"] = 2
                            }
                        },
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

        
        
        Dictionary<ulong,Dictionary<string,int>> _playerFragments = new Dictionary<ulong, Dictionary<string,int>>();
        
        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name,_playerFragments);
        }

        void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(Name))
            {
                _playerFragments = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string,int>>>(Name);
            }
        }

        #endregion

        #region Hooks

        void OnServerInitialized()
        {
            InitFragmentsAndImages();
            LoadData();
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (!_playerFragments.ContainsKey(player.userID))
                    _playerFragments.Add(player.userID, new Dictionary<string, int>());
            }
        }

        void Unload()
        {
            SaveData();
        }
        
        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (entity is LootContainer)
                if (!_openedContainers.Contains(entity.net.ID))
                {
                    if (UnityEngine.Random.Range(0, 99) < config.DropChance)
                        GiveFragment(player,
                            config.Fragments.Keys.ToList().GetRandom());
                    _openedContainers.Add(entity.net.ID);
                }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (!_playerFragments.ContainsKey(player.userID))
                _playerFragments.Add(player.userID, new Dictionary<string, int>());
        }

        #endregion

        #region Methods

        void InitFragmentsAndImages()
        {
            ImageLibrary.Call("AddImage", "https://i.imgur.com/6I2JERw.png","fragsmall");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/8s1jLEv.png","fragfull");
            foreach (var item in config.Settings)
            {
                ImageLibrary.Call("AddImage", item.Value.Image, item.Key);
                foreach (var fragment in item.Value.requiredFragments)
                {
                    
                    /*if (!_allFragments.ContainsKey(fragment.Key)) _allFragments.Add(fragment.Key,fragment.Value);*/
                }
            }

            foreach (var fragment in config.Fragments) ImageLibrary.Call("AddImage", fragment.Value.Image, fragment.Key);
        }

        bool HasFragment(ulong playerId, string key)
        {
            if (_playerFragments[playerId].ContainsKey(key)) return true;

            return false;
        }

        bool CanBuildItem(ulong playerId, string key)
        {
            foreach (var fragment in config.Settings[key].requiredFragments)
            {
                if (!HasFragment(playerId, fragment.Key))
                {
                    return  false;
                }

                if (_playerFragments[playerId][fragment.Key]<fragment.Value)
                {
                    return false;
                }
            }
            PrintWarning("CanCraft");
            return true;
        }

        void BuildItem(ulong playerId, string key)
        {
            foreach (var fragment in config.Settings[key].requiredFragments)
                if (_playerFragments[playerId][fragment.Key] == fragment.Value)
                    _playerFragments[playerId].Remove(fragment.Key);
                else
                    _playerFragments[playerId][fragment.Key]-=fragment.Value;

            Server.Command(config.Settings[key].Command.Replace("userid",playerId.ToString()));
            PrintError(config.Settings[key].Command.Replace("userid",playerId.ToString()));
            CraftFragmentUI(BasePlayer.FindByID(playerId),key);
        }

        void GiveFragment(BasePlayer player,string fragKey)
        {
            Fragment fragment = config.Fragments[fragKey];
            Broadcast.Call("GetPlayerNotice", player, "Найден фрагмент","Поздравляю вы нашли <color=#de9400>фрагмент</color> !!!",fragment.Image,"assets/bundled/prefabs/fx/invite_notice.prefab");
            if (!_playerFragments[player.userID].ContainsKey(fragKey))
                _playerFragments[player.userID].Add(fragKey,1);
            else
                _playerFragments[player.userID][fragKey]++;
            SendReply(player,$"Вы получили {fragment.DisplayName}{fragKey}");
        }

        #endregion

        #region UI

        private string _layer = "FragmentLayer";
        void FragmentUI(BasePlayer player,int page = 0)
        {
            CuiHelper.DestroyUi(player, _layer);
            PrintWarning($"Fragment {page}");
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0.5 0.5",AnchorMax = "0.5 0.5",OffsetMin = "-500 -270",OffsetMax = "500 270"}
            }, "ContentUI", _layer);
            container.Add(new CuiPanel
            {
                Image = {Color = HexToRustFormat("FF5B53")},
                RectTransform = {AnchorMin = "0 0",AnchorMax = "0 1",OffsetMin = "599 0",OffsetMax = "601 0"}
            }, _layer);

            double xpadding = 10,ypadding = 10,lenght = 600-xpadding*2,height = 122.2,startx = xpadding+30,starty = -ypadding-height;

            /*for (int i = 0; i < 4; i++)
            {
                container.Add(new CuiPanel
                {
                    Image = {Color = HexToRustFormat("FF5B5397")},
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = $"{startx} {starty}",OffsetMax = $"{startx+lenght} {starty+height}"}
                }, _layer);
                PrintWarning($"x:{startx} {startx+lenght} y:{starty} {starty+height}");
                starty -= ypadding + height;
            }*/
            container.Add(new CuiButton
            {
                Button = {Color = page>0?HexToRustFormat("#FF5B53"):HexToRustFormat("#FF5B53b7"),Command = page>0?$"fragments {page-1}":$"fragments {page}"},
                RectTransform = {AnchorMin = "0 0.5",AnchorMax = "0 1",OffsetMin = "10 0",OffsetMax = "25 0"},
                Text = {Text = "↑",Align = TextAnchor.MiddleCenter}
            }, _layer);
            container.Add(new CuiButton
            {
                Button = {Color = config.Settings.Count>(page+1)*4?HexToRustFormat("#FF5B53"):HexToRustFormat("#FF5B53b7"),Command = config.Settings.Count>(page +1)*4?$"fragments {page+1}":$"fragments {page}"},
                RectTransform = {AnchorMin = "0 0",AnchorMax = "0 0.5",OffsetMin = "10 0",OffsetMax = "25 0"},
                Text = {Text = "↓",Align = TextAnchor.MiddleCenter}
            }, _layer);
            foreach (var CompleteItem in config.Settings.Skip(page*4).Take(4))
            {
                container.Add(new CuiPanel
                {
                    Image = {Color = "0 0 0 0"},
                    //Image = {Color = HexToRustFormat("#FF5B53")},
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = $"{startx} {starty}",OffsetMax = $"{startx+lenght-30} {starty+height}"}
                }, _layer,$"{_layer}{CompleteItem.Key}");
                container.Add(new CuiElement
                {
                    Parent = $"{_layer}{CompleteItem.Key}",
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",AnchorMax = "1 1"
                        },
                        new CuiRawImageComponent
                        {
                            Png = (string) ImageLibrary.Call("GetImage","fragsmall")
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = $"{_layer}{CompleteItem.Key}",
                    Name = $"{CompleteItem.Key}Image",
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0.5",AnchorMax = "0 0.5",OffsetMin = "10 -61.1",OffsetMax = "132.2 61.1"
                        },
                        new CuiRawImageComponent
                        {
                            Png = (string) ImageLibrary.Call("GetImage",CompleteItem.Key)
                        }
                    }
                });
                
                
                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "0.8 1",OffsetMin = "132.2 -30"},
                    Text = {Text = CompleteItem.Value.DisplayName,Align = TextAnchor.MiddleCenter,FontSize = 18,Color = HexToRustFormat("#FF5B53e6")}
                }, $"{_layer}{CompleteItem.Key}");
                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 0",AnchorMax = "0.8 1",OffsetMin = "132.2 3",OffsetMax = "0 -28"},
                    Text = {Text = CompleteItem.Value.Description,Align = TextAnchor.MiddleLeft,FontSize = 14,Color = HexToRustFormat("#FF5B53e6")}
                }, $"{_layer}{CompleteItem.Key}");

                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "1 0",AnchorMax = "1 1",OffsetMin = "-105 20",OffsetMax = "-5 -20"},
                    Button =
                    {
                        Color = HexToRustFormat("#FF5B53e6"), Command = $"choosefragment {CompleteItem.Key}"
                    },
                    Text = {Color = HexToRustFormat("#000000"),Text = "ПОДРОБНЕЕ",FontSize = 18,Align = TextAnchor.MiddleCenter},
                }, $"{_layer}{CompleteItem.Key}");

                starty -= ypadding + height;
            }

            CuiHelper.AddUi(player, container);
        }

        private string _fragLayer = "FragLayer";
        void CraftFragmentUI(BasePlayer player, string fragKey)
        {
            CuiHelper.DestroyUi(player, _fragLayer);
            if (!config.Settings.ContainsKey(fragKey)) return;
            FragmentItem item = config.Settings[fragKey];
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = {Color = HexToRustFormat("#FF5B5399")},
                RectTransform = {AnchorMin = "0 0",AnchorMax = "0 1",OffsetMin = "610 5",OffsetMax = "990 -5"}
            }, _layer, _fragLayer);
            
            container.Add(new CuiElement
            {
                Parent = _fragLayer,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",AnchorMax = "1 1"
                    },
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage","fragfull")
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = _fragLayer,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = $"5 -80",OffsetMax = "80 -5"
                    },
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage",fragKey)
                    }
                }
            });
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 1",AnchorMax = "1 1",OffsetMin = $"80 -35",OffsetMax = "-5 -5"},
                Text = {Text = item.DisplayName,Align = TextAnchor.MiddleCenter,FontSize = 21,Color = "0 0 0 1"}
            }, _fragLayer);
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 1",AnchorMax = "1 1",OffsetMin = $"80 -115",OffsetMax = "-5 -35"},
                Text = {Text = item.Description,Align = TextAnchor.MiddleCenter,FontSize = 18,Color = "0 0 0 1"}
            }, _fragLayer);
            double starty = -205, height = 70, padding = 10; 
            foreach (var fragmentkey in item.requiredFragments)
            {
                
                var fragment = config.Fragments.First(p => Equals(p.Key, fragmentkey.Key));
                /*container.Add(new CuiElement
                {
                    Parent = _fragLayer,
                    Name = $"{_fragLayer}{fragment.Key}",
                    Components =
                    {
                        new CuiRectTransformComponent{AnchorMin = "0 0",AnchorMax = "1 0",OffsetMin = $"0 {starty}",OffsetMax = $"0 {starty+height}"},
                        new CuiOutlineComponent
                        {
                            Color = HasFragment(player.userID,fragment.Key)? HexToRustFormat("fcc200"):HexToRustFormat("d9bc5d")
                        }
                    }
                });*/
                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "1 1",OffsetMin = $"0 {starty}",OffsetMax = $"0 {starty+height}"},
                    Image = {Color = "0 0 0 0"}
                }, _fragLayer, $"{_fragLayer}{fragment.Key}");
                container.Add(new CuiElement
                {
                    Parent = $"{_fragLayer}{fragment.Key}",
                    Components =
                    {
                        new CuiRectTransformComponent{AnchorMin = "0 0.5",AnchorMax = "0 0.5",OffsetMin = $"5 -{height/2}",OffsetMax = $"{height+5} {height/2}"},
                        new CuiRawImageComponent{Png = (string) ImageLibrary.Call("GetImage",fragment.Key)}
                    }
                });
                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "1 1",OffsetMin = $"{height+5} -25",OffsetMax = "0 0"},
                    Text = {Text = fragment.Value.DisplayName,Color = "0 0 0 1"}
                }, $"{_fragLayer}{fragment.Key}");
                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 0",AnchorMax = "1 1",OffsetMin = $"{height+5} 0",OffsetMax = "0 -25"},
                    Text = {Text = HasFragment(player.userID,fragment.Key)?$"{_playerFragments[player.userID][fragment.Key]}/{fragmentkey.Value}": $"0/{fragmentkey.Value}",Color = "0 0 0 1"}
                },$"{_fragLayer}{fragment.Key}");
                starty -= height + padding;
            }

            container.Add(new CuiButton
            {
                Button = {Color = HexToRustFormat("#00F2FF99"),Command = $"fragmentcraft {fragKey}"},//00F2FF
                Text = {Text = "СОБРАТЬ",Color = HexToRustFormat("#000000"),FontSize = 18,Align = TextAnchor.MiddleCenter},
                RectTransform = {AnchorMin = "0 0",AnchorMax = "1 0",OffsetMin = "5 5",OffsetMax = "-5 40"}
            }, _fragLayer);

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Commands

        [ConsoleCommand("fragments")]
        void OpenUICmd(ConsoleSystem.Arg arg)
        {
            if (arg.HasArgs())
            {
                FragmentUI(arg.Player(),arg.Args[0].ToInt());
            }
            else
            {
                FragmentUI(arg.Player());
            }
            
        }

        [ConsoleCommand("choosefragment")]
        void PlayerChooseFragment(ConsoleSystem.Arg arg)
        {
            CraftFragmentUI(arg.Player(),arg.Args[0]);
        }

        [ConsoleCommand("fragmentcraft")]
        void PlayerTryCraftFragment(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (CanBuildItem(player.userID,arg.Args[0]))
            {
                BuildItem(player.userID,arg.Args[0]);
            }
        }

        [ChatCommand("fgive")]
        void GiveFragmentsToAdmin(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin)
            {
                if (config.Settings.ContainsKey(args[0]))
                {
                    foreach (var fragment in config.Settings[args[0]].requiredFragments)
                    {
                        GiveFragment(player,fragment.Key);
                    }
                }
                else
                {
                    SendReply(player,$"Not Contains {args[0]}");
                }
            }
        }

        #endregion
        
        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                hex = "#FFFFFFFF";
            }

            var str = hex.Trim('#');

            if (str.Length == 6)
                str += "FF";

            if (str.Length != 8)
            {
                throw new Exception(hex);
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            Color color = new Color32(r, g, b, a);

            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }
    }
}