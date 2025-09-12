using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("FCaseDay", "FURY RUST", "1.0.0")]
    public class FCaseDay : RustPlugin
    {
        #region Var


        [PluginReference] private Plugin ImageLibrary, FNotification;
        private static FCaseDay _ins;
        
        public enum PrizeItem
        {
            Item = 1,
            Command = 2,
        }

        #endregion
        
        #region Data

        public class PlayerStore
        {
            public string ShortName;
            public int Amount;
            public ulong SkinID;
            public string Command;
            public string Image;
            public bool CommandOrItem;


            public int UniversalNumber;

            public void GiveItem(BasePlayer player)
            {
                if (!CommandOrItem)
                {
                    var item = ItemManager.CreateByName(ShortName, Amount, SkinID);
                    player.GiveItem(item);
                }
                else
                {
                    _ins.Server.Command(Command.Replace("%STEAMID%", player.UserIDString));
                }
            }
        }
        
        public class DataPlayer
        {
            public int TypeCase;
            public int AllOpen;
            public bool Enabled;

            public List<PlayerStore> _itemList = new List<PlayerStore>();
            
        }

        public Dictionary<ulong, DataPlayer> _playerData = new Dictionary<ulong, DataPlayer>();
        public List<string> DayWeek = new List<string>();

        #endregion
        
        #region Hooks


        void OnServerInitialized()
        {
            _ins = this;
            try
            {
                _playerData = Interface.GetMod().DataFileSystem
                    .ReadObject<Dictionary<ulong, DataPlayer>>($"{Name}/PlayerData");
                if (_playerData == null)
                    _playerData = new Dictionary<ulong, DataPlayer>();
                DayWeek = Interface.GetMod().DataFileSystem.ReadObject<List<string>>($"{Name}/DayWeek");
                if (DayWeek == null)
                    DayWeek = new List<string>();
            }
            catch
            {
                _playerData = new Dictionary<ulong, DataPlayer>();
                DayWeek = new List<string>();
            }
            
            if (ImageLibrary == null || !ImageLibrary.IsLoaded)
            {
                PrintError("IMAGE LIBRARY IS NOT INSTALLED!");
            }
            else
            {
                var imagesList = new Dictionary<string, string>();

                foreach (var imageKeys in config._caseList)
                {
                    if (!string.IsNullOrEmpty(imageKeys.Image) && !imagesList.ContainsKey(imageKeys.Image))
                    {
                        imagesList.Add(imageKeys.Image, imageKeys.Image);
                    }

                    foreach (var prizeList in imageKeys._listItem)
                    {
                        if (!string.IsNullOrEmpty(prizeList.URLImage) && !imagesList.ContainsKey(prizeList.URLImage))
                        {
                            imagesList.Add(prizeList.URLImage, prizeList.URLImage);
                        }
                        else if (!string.IsNullOrEmpty(prizeList.ShortName) && !imagesList.ContainsKey(prizeList.ShortName + 128))
                        {
                            string image = $"http://api.skyplugins.ru/api/getimage/{prizeList.ShortName}/{128}";
                            if (!string.IsNullOrEmpty(image) && !imagesList.ContainsKey(prizeList.ShortName + 128))
                            {
                                imagesList.Add(prizeList.ShortName + 128, image);
                            }
                        }
                    }
                }

                ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
            
            
            UpdateDay();

            timer.Every(60, () =>
            {
                UpdateDay();
            });
        }

        void Unload()
        {
            Interface.GetMod().DataFileSystem.WriteObject($"{Name}/PlayerData", _playerData);
            Interface.GetMod().DataFileSystem.WriteObject($"{Name}/DayWeek", DayWeek);
            _ins = null;
        }

        void OnNewSave()
        {
            if (config.WipeData)
            {
                _playerData = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, DataPlayer>>($"{Name}/PlayerData");
                DayWeek = Interface.GetMod().DataFileSystem.ReadObject<List<string>>($"{Name}/DayWeek");
                
                _playerData = new Dictionary<ulong, DataPlayer>();
                DayWeek = new List<string>();
                
                Interface.GetMod().DataFileSystem.WriteObject($"{Name}/PlayerData", _playerData);
                Interface.GetMod().DataFileSystem.WriteObject($"{Name}/DayWeek", DayWeek);
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (!_playerData.ContainsKey(player.userID))
                _playerData.Add(player.userID, new DataPlayer()
                {
                    Enabled = false,
                    TypeCase = 1,
                    AllOpen = 0,
                    _itemList = new List<PlayerStore>(),
                });

            var data = _playerData[player.userID];
            if (!data.Enabled && config.NotificationAcceptCase)
            {
                var findCase = config._caseList.FirstOrDefault(p => p.NumberCase == data.TypeCase);
                if (findCase != null)
                {
                    if (config.NotificationPlugin && plugins.Find("FNotification"))
                        FNotification?.CallHook("SendNotify", player, "Ежедневный кейс", "Вам доступен ежедневный кейс, пропишите команду /case что бы его открыть", findCase.Image,"2 2", "-2 -2",  10 );
                }   
            }
        }
        

        #endregion
        
        #region Functional
        
        public string HexToCuiColor(string HEX, float Alpha = 100)
        {
            if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

            var str = HEX.Trim('#');
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
        }


        public void UpdateDay()
        {
            string day = DateTime.Now.DayOfWeek.ToString();

            if (!DayWeek.Contains(day))
            {
                DayWeek.Add(day);
                CheckPlayersAny();
            }
            else if (DayWeek.Contains(day) && DayWeek.Count >= 7)
            {
                DayWeek.Clear();
                DayWeek.Add(day);
                CheckPlayersAny();
            }
        }


        public void CheckPlayersAny()
        {
            foreach (var keyPlayer in _playerData)
            {
                if (keyPlayer.Value.Enabled == false)
                {
                    keyPlayer.Value.AllOpen = 1;
                    keyPlayer.Value.TypeCase = 1;
                }
                keyPlayer.Value.Enabled = false;
            }
        }
        public Dictionary<string, DayOfWeek> _dayString = new Dictionary<string, DayOfWeek>()
        {  
            ["ПОНЕДЕЛЬНИК"] = DayOfWeek.Monday,
            ["ВТОРНИК"] = DayOfWeek.Tuesday,
            ["СРЕДА"] = DayOfWeek.Wednesday,
            ["ЧЕТВЕРГ"] = DayOfWeek.Thursday,
            ["ПЯТНИЦА"] = DayOfWeek.Friday, 
            ["СУББОТА"] = DayOfWeek.Saturday,
            ["ВОСКРЕСЕНЬЕ"] = DayOfWeek.Sunday
        };


        #endregion
        
        #region ChatCommand && ConsoleCommand


        [ChatCommand("case")]
        void HandlerCommandCase(BasePlayer player, string command, string[] args)
        {
            OpenUI(player);
        }


        [ConsoleCommand("UI_CaseHandler")]
        void UICaseHandler(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();

            
            
            if (args.Args[0] == "openprize")
            {
                var find = config._caseList.FirstOrDefault(p => p.NumberCase == int.Parse(args.Args[1]));
                if (find != null)
                    UIOpenPrize(player, find.NumberCase, 1);
            }
            else if (args.Args[0] == "pagePrize")
            {
                
                var find = config._caseList.FirstOrDefault(p => p.NumberCase == int.Parse(args.Args[2]));
                if (find != null)
                {
                    int page = 1;
                    if (int.TryParse(args.Args[1], out page))
                    {
                        UIOpenPrize(player, find.NumberCase, page);
                    }
                }
            }
            else if (args.Args[0] == "pageInventory")
            {
                int page = 1;
                if (int.TryParse(args.Args[1], out page))
                {
                    OpenInventory(player, page);
                }
            }
            else if (args.Args[0] == "openInventory")
            {
                CuiHelper.DestroyUi(player, Layer);
                var container = new CuiElementContainer();
                
                container.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image = { Color = "0 0 0 0.9", Material = "assets/content/ui/uibackgroundblur.mat"}
                }, "Overlay", Layer);
            
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiImageComponent { Color = "0 0 0 0.9", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                    }
                });
            
                container.Add(new CuiButton 
                {
                    RectTransform = { AnchorMin = "0.1651042 0.1666667", AnchorMax = "0.8364583 0.8361111"},
                    Button =
                    {
                        Command = $"",
                        Color = HexToCuiColor("#fffdfc", 5), Material = "assets/content/ui/uibackgroundblur.mat"
                    },
                    Text =
                    {
                        Text = "", Color = HexToCuiColor("#fffdfc"),
                        Align = TextAnchor.MiddleLeft, FontSize = 13, Font = "robotocondensed-bold.ttf"
                    },

                }, Layer);
            
                container.Add(new CuiButton 
                {
                    RectTransform = { AnchorMin = "0.756764 0.1722222", AnchorMax = "0.834375 0.2185273"},
                    Button =
                    {
                        Close = Layer,
                        Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat"
                    },
                    Text =
                    {
                        Text = "ЗАКРЫТЬ", Color = HexToCuiColor("#fcf7f6"),
                        Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-bold.ttf"
                    },

                }, Layer);

                CuiHelper.AddUi(player, container);
                OpenInventory(player, 1);
            }
            else if (args.Args[0] == "giveInventory")
            {
                var data = _playerData[player.userID];
                var findItem = data._itemList.FirstOrDefault(p => p.UniversalNumber == int.Parse(args.Args[1]));
                if (findItem != null)
                {
                    findItem.GiveItem(player);
                    data._itemList.Remove(findItem);
                    CuiHelper.DestroyUi(player, Layer + $".{int.Parse(args.Args[2])}.InventoryList");
                }
            }
            else if (args.Args[0] == "openCase")
            {
                var data = _playerData[player.userID];
                if (data.Enabled) return;
                var find = config._caseList.FirstOrDefault(p => p.NumberCase == data.TypeCase);
                if (find == null) return;

                var getItem = find._listItem.GetRandom();
                data._itemList.Add(new PlayerStore
                {
                    ShortName = getItem.ShortName,
                    Amount = getItem.Amount,
                    SkinID = getItem.SkinID,
                    Command = getItem.Command,
                    Image = getItem.URLImage,
                    CommandOrItem = getItem.PrizeType == PrizeItem.Item ? false : true,
                    UniversalNumber = UnityEngine.Random.Range(Int32.MinValue, Int32.MaxValue)
                });
                data.AllOpen++;
                data.Enabled = true;
                if (data.AllOpen >= 7)
                {
                    var lastConfig = config._caseList.FirstOrDefault(p => p.NumberCase == data.TypeCase + 1);
                    if (lastConfig != null)
                    {
                        data.AllOpen = 1;
                        data.TypeCase++;
                        OpenTypeCase(player, lastConfig, data);
                        return;
                    }
                }
                OpenTypeCase(player, find, data);
            }
        }

        #endregion
        
        #region UI


        public string Layer = "UI_CaseMask";
        
        
        public void OpenUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();
            var data = _playerData[player.userID];

            var findConfig = config._caseList.FirstOrDefault(p => p.NumberCase == data.TypeCase);
            if (findConfig == null)
            {
                player.ChatMessage("Произошла ошибка плагина #1. Отпишитесь основателю");
                return;
            }


            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0.9", Material = "assets/content/ui/uibackgroundblur.mat"}
            }, "Overlay", Layer);
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0 0.9", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                }
            });
            
            container.Add(new CuiButton 
            {
                RectTransform = { AnchorMin = "0.05468752 0.2222222", AnchorMax = "0.946875 0.7805555"},
                Button =
                {
                    Command = $"",
                    Color = HexToCuiColor("#fffdfc", 5), Material = "assets/content/ui/uibackgroundblur.mat"
                },
                Text =
                {
                    Text = "", Color = HexToCuiColor("#fffdfc"),
                    Align = TextAnchor.MiddleLeft, FontSize = 13, Font = "robotocondensed-bold.ttf"
                },

            }, Layer);
            
            container.Add(new CuiButton 
            {
                RectTransform = { AnchorMin = "0.865625 0.2277778", AnchorMax = "0.9442708 0.274074"},
                Button =
                {
                    Close = Layer,
                    Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat"
                },
                Text =
                {
                    Text = "ЗАКРЫТЬ", Color = HexToCuiColor("#fcf7f6"),
                    Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-bold.ttf"
                },

            }, Layer);
            
            
                
            container.Add(new CuiButton 
            {
                RectTransform = { AnchorMin = "0.05468749 0.787037", AnchorMax = "0.3010417 0.8333239"},
                Button =
                {
                    Color = HexToCuiColor("#95B641"), Material = "assets/content/ui/uibackgroundblur.mat"
                },
                Text =
                {
                    Text = "", Color = HexToCuiColor("#fcf7f6"),
                    Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-bold.ttf"
                },

            }, Layer);
            
            container.Add(new CuiElement() 
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent{Color = HexToCuiColor("#E8DED2"),Text = $"{config.NameFind.ToUpper()} в никнейме +50% к шансу!", Align = TextAnchor.MiddleLeft, FontSize = 16, Font = "robotocondensed-bold.ttf"},
                    new CuiRectTransformComponent{AnchorMin = "0.06874999 0.787037", AnchorMax = "0.3010417 0.8333239"},
                }
            });
            
            container.Add(new CuiButton 
            {
                RectTransform = { AnchorMin = "0.7838541 0.2277778", AnchorMax = "0.863022 0.274074"},
                Button =
                {
                    Command = "UI_CaseHandler openInventory",
                    Color = HexToCuiColor("#4094b9"), Material = "assets/content/ui/uibackgroundblur.mat"
                },
                Text =
                {
                    Text = "ИНВЕНТАРЬ", Color = HexToCuiColor("#fcf7f6"),
                    Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-bold.ttf"
                },

            }, Layer);
            
            container.Add(new CuiButton 
            {
                RectTransform = { AnchorMin = "0.07135417 0.3925925", AnchorMax = "0.9213542 0.4861111"},
                Button =
                {
                    Command = $"",
                    Color = "0 0 0 0"
                },
                Text =
                {
                    Text = $"Открывайте кейсы ежедневно в течении одной недели и получите доступ к улучшенным кейсам. Все предметы и привилегии которые Вы получите, автоматически перемещаются в инвентарь (кнопка справа снизу), вы можете забрать их в любое время. Чтобы посмотреть что можно получить в ‘Обычных ящиках’, ‘Ящиках Ветеранов’ и ‘Элитных Кейсах’, нажмите на соответствующую кнопку, после чего перелистывайте страницы с помощью кнопок ‘-” и ‘+” которые находятся слева снизу.", Color = HexToCuiColor("#E8DED2"),
                    Align = TextAnchor.MiddleLeft, FontSize = 14, Font = "robotocondensed-regular.ttf"
                },

            }, Layer);
            


            for (int i = 0; i < config._caseList.Count; i++)
            {
                CuiHelper.DestroyUi(player, Layer + $".{i}.ButtonCase");

                var configfor = config._caseList[i];

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin =
                            $"{0.2630287 + i * 0.160 - Math.Floor((double)i / 7) * 7 * 0.160} {0.2277778 - Math.Floor((double)i / 7) * 0.095}",
                        AnchorMax =
                            $"{0.4203125 + i * 0.160 - Math.Floor((double)i / 7) * 7 * 0.160} {0.274074 - Math.Floor((double)i / 7) * 0.095}",
                        OffsetMax = "0 0"
                    },
                    Button =
                    {
                        Color = HexToCuiColor(configfor.ColorCase.Color, configfor.ColorCase.Opacity),
                        Command = $"UI_CaseHandler openprize {configfor.NumberCase}",
                    },
                    Text =
                    {
                        Color = HexToCuiColor("#E8DED2"),Text = $"{configfor.DisplayName.ToUpper()}", Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-bold.ttf"
                    }
                }, Layer, Layer + $".{i}.ButtonCase");
                
            }



            CuiHelper.AddUi(player, container);
            
            OpenTypeCase(player, findConfig, data);
        }


        public void OpenTypeCase(BasePlayer player, CaseList findConfig, DataPlayer data)
        {
            var container = new CuiElementContainer();

            CuiHelper.DestroyUi(player, Layer + ".NameCase");
            
            container.Add(new CuiElement() 
            {
                Parent = Layer,
                Name = Layer + ".NameCase",
                Components =
                {
                    new CuiTextComponent{Color = HexToCuiColor("#E8DED2"),Text = $"{findConfig.DisplayName}", Align = TextAnchor.MiddleCenter, FontSize = 22, Font = "robotocondensed-bold.ttf"},
                    new CuiRectTransformComponent{AnchorMin = "0.05468752 0.7138889", AnchorMax = "0.946875 0.7777778"},
                }
            });

            foreach (var check in _dayString.Select((i, t) => new { A = i, B = t }))
            {
                int i = check.B;
                CuiHelper.DestroyUi(player, Layer + $".{i}.CaseList");
                
                
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin =
                            $"{0.07135417 + i * 0.1225 - Math.Floor((double) i / 7) * 7 * 0.1225} {0.4935185 - Math.Floor((double) i/ 7) * 0.095}",
                        AnchorMax =
                            $"{0.1911458 + i * 0.1225 - Math.Floor((double) i / 7) * 7 * 0.1225} {0.7138889 - Math.Floor((double) i / 7) * 0.095}",
                        OffsetMax = "0 0"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = DateTime.Now.DayOfWeek == check.A.Value ? "UI_CaseHandler openCase" : "",
                    },
                    Text =
                    {
                        Text = $"", Align = TextAnchor.LowerRight, Font = "robotocondensed-bold.ttf", FontSize = 15
                    }
                }, Layer, Layer + $".{i}.CaseList");
                
                
                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.09999996 0.1554622", AnchorMax = "0.9173913 0.987395"
                    },
                    Image =
                    {
                        Color = "0 0 0 0",
                    }
                },Layer + $".{i}.CaseList", Layer + $".{i}.CaseListImage");
                
                container.Add(new CuiElement
                {
                    Parent = Layer + $".{i}.CaseListImage",
                    Components =
                    {
                        new CuiRawImageComponent
                            {FadeIn = 0.3f, Png = (string)ImageLibrary.Call("GetImage", findConfig.Image)},
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0", AnchorMax = "1 1"}
                    }
                });
                if (data.Enabled == false && DateTime.Now.DayOfWeek == check.A.Value)
                    {
                        container.Add(new CuiElement() 
                        {
                            Parent = Layer + $".{i}.CaseListImage",
                            Components =
                            {
                                new CuiTextComponent{Color = HexToCuiColor("#E8DED2"),Text = $"ОТКРЫТЬ", Align = TextAnchor.MiddleCenter, FontSize = 18, Font = "robotocondensed-bold.ttf"},
                                new CuiRectTransformComponent{AnchorMin = "0 0.09595937", AnchorMax = "1 0.9494951"},
                                new CuiOutlineComponent {Color = "0 0 0 1", Distance = "0.3 0.3"},
                            }
                        });
                    }
                    else if (data.Enabled == true && DateTime.Now.DayOfWeek == check.A.Value)
                    {
                        container.Add(new CuiElement() 
                        {
                            Parent = Layer + $".{i}.CaseListImage",
                            Components =
                            {
                                new CuiTextComponent{Color = HexToCuiColor("#E8DED2"),Text = $"ОТКРЫТО", Align = TextAnchor.MiddleCenter, FontSize = 18, Font = "robotocondensed-bold.ttf"},
                                new CuiRectTransformComponent{AnchorMin = "0 0.09595937", AnchorMax = "1 0.9494951"},
                                new CuiOutlineComponent {Color = "0 0 0 1", Distance = "0.3 0.3"},
                            }
                        });
                    }


                container.Add(new CuiElement() // content
                {
                    Parent = Layer + $".{i}.CaseList",
                    Components =
                    {
                        new CuiTextComponent{Color = HexToCuiColor("#FBFBFB"),Text = check.A.Key, Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-bold.ttf"},
                        new CuiRectTransformComponent{AnchorMin = "0.09999996 0", AnchorMax = "0.9173913 0.1554622"},
                    }
                    
                });
                
            }

            
            
            CuiHelper.AddUi(player, container);
        }
        
        public void OpenInventory(BasePlayer player, int page)
        {
            var data = _playerData[player.userID];
            int pagex = page + 1;
            
            var container = new CuiElementContainer();


            CuiHelper.DestroyUi(player, Layer + ".Vpered");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.2369799 0.1722222", AnchorMax = "0.2687504 0.2185273" },
                Button =
                {
                    Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat",
                    Command = pagex > 0 && (pagex - 1) * 18 < data._itemList.Count
                        ? $"UI_CaseHandler pageInventory {page + 1}"
                        : ""
                },
                Text =
                {
                    Text = "+", Color = HexToCuiColor("#fcf7f6"),
                    Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-bold.ttf"
                }
            }, Layer, Layer + ".Vpered");
            
            CuiHelper.DestroyUi(player, Layer + ".Nazad");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.168229 0.1722222", AnchorMax = "0.2 0.2185273" },
                Button =
                {
                    Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat",
                    Command = page != 1
                        ? $"UI_CaseHandler pageInventory {page - 1}"
                        : ""
                },
                Text =
                {
                    Text = "-", Color = HexToCuiColor("#fcf7f6"),
                    Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-bold.ttf"
                }
            }, Layer, Layer + ".Nazad");
            
            CuiHelper.DestroyUi(player, Layer + ".Page");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.2026044 0.1722222", AnchorMax = "0.2343754 0.2185273" },
                Button =
                {
                    Color = HexToCuiColor("#000000", 50),
                },
                Text =
                {
                    Text = $"{page}", Color = HexToCuiColor("#fcf7f6"),
                    Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf"
                }
            }, Layer, Layer + ".Page");

            for (int i = 0; i < 18; i++)
            {
                CuiHelper.DestroyUi(player, Layer + $".{i}.InventoryList");
            }
            
            foreach (var check in data._itemList.Select((i, t) => new { A = i, B = t - (page - 1) * 18 }).Skip((page - 1) * 18).Take(18))
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin =
                            $"{0.1760416 + check.B * 0.109 - Math.Floor((float) check.B / 6) * 6 * 0.109} {0.6212963 - Math.Floor((float) check.B/ 6) * 0.195}",
                        AnchorMax =
                            $"{0.2848958 + check.B * 0.109 - Math.Floor((float) check.B / 6) * 6 * 0.109} {0.8175926 - Math.Floor((float) check.B / 6) * 0.195}",
                        OffsetMax = "0 0"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = $"UI_CaseHandler giveInventory {check.A.UniversalNumber} {check.B}",
                    },
                    Text =
                    {
                        Text = $"", Align = TextAnchor.LowerRight, Font = "robotocondensed-bold.ttf", FontSize = 15
                    }
                }, Layer, Layer + $".{check.B}.InventoryList");
                
                container.Add(new CuiElement
                {
                    Parent = Layer + $".{check.B}.InventoryList",
                    Components =
                    {
                        new CuiRawImageComponent
                            {FadeIn = 0.3f, Png = (string)ImageLibrary.Call("GetImage", string.IsNullOrEmpty(check.A.Image) == true ? check.A.ShortName  : check.A.Image)},
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0", AnchorMax = "1 1"}
                    }
                });
            }
            
            CuiHelper.AddUi(player, container);
        }


        public void UIOpenPrize(BasePlayer player, int numberCase, int page)
        {
            var find = config._caseList.FirstOrDefault(p => p.NumberCase == numberCase);
            if (find == null) return;
            
            int pagex = page + 1;
            

            var container = new CuiElementContainer();


            CuiHelper.DestroyUi(player, Layer + ".Vpered");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.1265689 0.2277778", AnchorMax = "0.1588537 0.274074" },
                Button =
                {
                    Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat",
                    Command = pagex > 0 && (pagex - 1) * 17 < find._listItem.Count
                        ? $"UI_CaseHandler pagePrize {page + 1} {find.NumberCase}"
                        : ""
                },
                Text =
                {
                    Text = "+", Color = HexToCuiColor("#fcf7f6"),
                    Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-bold.ttf"
                }
            }, Layer, Layer + ".Vpered");
            
            CuiHelper.DestroyUi(player, Layer + ".Nazad");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.05781897 0.2277778", AnchorMax = "0.09010417 0.274074" },
                Button =
                {
                    Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat",
                    Command = page != 1
                        ? $"UI_CaseHandler pagePrize {page - 1} {find.NumberCase}"
                        : ""
                },
                Text =
                {
                    Text = "-", Color = HexToCuiColor("#fcf7f6"),
                    Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-bold.ttf"
                }
            }, Layer, Layer + ".Nazad");
            
            CuiHelper.DestroyUi(player, Layer + ".Page");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.09219391 0.2277778", AnchorMax = "0.1244792 0.274074" },
                Button =
                {
                    Color = HexToCuiColor("#000000", 50),
                },
                Text =
                {
                    Text = $"{page}", Color = HexToCuiColor("#fcf7f6"),
                    Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf"
                }
            }, Layer, Layer + ".Page");

            for (int i = 0; i < 17; i++)
            {
                CuiHelper.DestroyUi(player, Layer + $".{i}.CaseListItem");
            }

            foreach (var check in find._listItem.Select((i, t) => new { A = i, B = t - (page - 1) * 17 }).Skip((page - 1) * 17).Take(17))
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin =
                            $"{0.0713606 + check.B * 0.051 - Math.Floor((double) check.B / 17) * 17 * 0.051} {0.2916667 - Math.Floor((double) check.B/ 17) * 0.095}",
                        AnchorMax =
                            $"{0.1208333 + check.B * 0.051 - Math.Floor((double) check.B / 17) * 17 * 0.051} {0.3796298 - Math.Floor((double) check.B / 17) * 0.095}",
                        OffsetMax = "0 0"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = $"",
                    },
                    Text =
                    {
                        Text = $"", Align = TextAnchor.LowerRight, Font = "robotocondensed-bold.ttf", FontSize = 15
                    }
                }, Layer, Layer + $".{check.B}.CaseListItem");
                
                container.Add(new CuiElement
                {
                    Parent = Layer + $".{check.B}.CaseListItem",
                    Components =
                    {
                        new CuiRawImageComponent
                            {FadeIn = 0.3f, Png = (string)ImageLibrary.Call("GetImage", string.IsNullOrEmpty(check.A.URLImage) == true ? check.A.ShortName + 128 : check.A.URLImage)},
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0", AnchorMax = "1 1"}
                    }
                });
                
            }
            
            CuiHelper.AddUi(player, container);
        }

        

        #endregion
        
        #region Configuration


        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            config = PluginConfig.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();

            if (config.PluginVersion < Version)
                UpdateConfigValues();

            Config.WriteObject(config, true);
        }

        private void UpdateConfigValues()
        {
            PluginConfig baseConfig = PluginConfig.DefaultConfig();
            if (config.PluginVersion < new VersionNumber(1, 0, 0))
            {
                PrintWarning("Config update detected! Updating config values...");
                PrintWarning("Config update completed!");
            }

            config.PluginVersion = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        
        public class PrizeList
        {
            [JsonProperty("ShortName")] 
            public string ShortName;

            [JsonProperty("Type ( 1 - Item, 2 - Command )")] 
            public PrizeItem PrizeType;

            [JsonProperty("Amount")]
            public int Amount;

            [JsonProperty("SkinID")] 
            public ulong SkinID;

            [JsonProperty("Command")] 
            public string Command;

            [JsonProperty("URL Image")] 
            public string URLImage;

        }

        public class ColorOption
        {
            [JsonProperty("HEX Color")] 
            public string Color;

            [JsonProperty("Opacity ( 0 - 100 )")] 
            public int Opacity;
        }
        
        public class CaseList
        {
            [JsonProperty("Название кейса")] 
            public string DisplayName;

            [JsonProperty("Картинка кейса")] 
            public string Image;

            [JsonProperty("Цвет кейса")]
            public ColorOption ColorCase;

            [JsonProperty("Номер кейса ( Упорядок кейсов по неделям. Если не понимаешь, лучше ничего не трогай и напиши сюда: vk.com/draggb )")]
            public int NumberCase;

            [JsonProperty("Предметы, которые могут выпасть из данного кейса")]
            public List<PrizeList> _listItem = new List<PrizeList>();
        }


        private class PluginConfig
        {
            [JsonProperty("Добавить поддержку FNotification ?")]
            public bool NotificationPlugin;

            [JsonProperty("Оповещать ли игрока при заходе на сервер о том, что у него есть доступный кейс ?")]
            public bool NotificationAcceptCase;

            [JsonProperty("Чистить дату при вайпе сервера?")]
            public bool WipeData;

            [JsonProperty("Приставка к нику")] 
            public string NameFind;

            [JsonProperty("Настройка кейсов")] 
            public List<CaseList> _caseList = new List<CaseList>();

            [JsonProperty("Версия конфигурации")]
            public VersionNumber PluginVersion = new VersionNumber();

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    NotificationPlugin = true,
                    NotificationAcceptCase = true,
                    
                    WipeData = true,
                    NameFind = "#FURYRUST",
                    _caseList = new List<CaseList>()
                    {
                        new CaseList()
                        {
                            DisplayName = "Обычный мешок",
                            Image = "https://i.imgur.com/3XnjTZs.png",
                            ColorCase = new ColorOption()
                            {
                                Color = "#fffdfc",
                                Opacity = 30,
                            },
                            NumberCase = 1,
                            _listItem = new List<PrizeList>()
                            {
                                new PrizeList()
                                {
                                    ShortName = "cloth",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 1500,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "sulfur",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 5000,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "explosives",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 40,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "flameturret",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 1,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "gears",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 10,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "hazmatsuit",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 1,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "hoodie",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 1,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "ammo.rifle.incendiary",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 128,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "syringe.medical",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 4,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "ammo.rifle",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 4,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "supply.signal",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 4,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "metalblade",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 4,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "metalspring",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 4,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "metalpipe",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 4,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "rifle.ak",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 1,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "rifle.bolt",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 1,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "ammo.pistol",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 1,
                                    URLImage = "",
                                    Command = ""
                                },
                            }
                        },
                        new CaseList()
                        {
                            DisplayName = "Ящик ветерана",
                            Image = "https://i.imgur.com/G8QyaOH.png",
                            ColorCase = new ColorOption()
                            {
                                Color = "#7D924D",
                                Opacity = 100,
                            },
                            NumberCase = 2,
                            _listItem = new List<PrizeList>()
                            {
                                new PrizeList()
                                {
                                    ShortName = "cloth",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 1500,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "sulfur",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 5000,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "explosives",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 40,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "flameturret",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 1,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "gears",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 10,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "hazmatsuit",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 1,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "hoodie",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 1,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "ammo.rifle.incendiary",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 128,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "syringe.medical",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 4,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "ammo.rifle",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 4,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "supply.signal",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 4,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "pistol.python",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 4,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "metalspring",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 4,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "metalpipe",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 4,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "rifle.ak",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 1,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "rifle.bolt",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 1,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "ammo.pistol",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 1,
                                    URLImage = "",
                                    Command = ""
                                },
                            }
                        },
                        new CaseList()
                        {
                            DisplayName = "Легендарный кейс",
                            Image = "https://i.imgur.com/KoH7Rgf.png",
                            ColorCase = new ColorOption()
                            {
                                Color = "#79598B",
                                Opacity = 100,
                            },
                            NumberCase = 3,
                            _listItem = new List<PrizeList>()
                            {
                                new PrizeList()
                                {
                                    ShortName = "cloth",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 1500,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "sulfur",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 5000,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "explosives",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 40,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "flameturret",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 1,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "gears",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 10,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "hazmatsuit",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 1,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "hoodie",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 1,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "ammo.rifle.incendiary",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 128,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "syringe.medical",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 4,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "ammo.rifle",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 4,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "supply.signal",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 4,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "metalblade",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 4,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "metalspring",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 4,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "metalpipe",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 4,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "rifle.ak",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 1,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "rifle.bolt",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 1,
                                    URLImage = "",
                                    Command = ""
                                },
                                new PrizeList()
                                {
                                    ShortName = "wood",
                                    PrizeType = PrizeItem.Item,
                                    SkinID = 0,
                                    Amount = 1,
                                    URLImage = "",
                                    Command = ""
                                },
                            }
                        }
                    },
                    PluginVersion = new VersionNumber(),
                };
            }
        }

        #endregion
    }
}