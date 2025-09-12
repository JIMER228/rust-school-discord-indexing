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
    [Info("FExchanger", "Sempai", "2.0.0")]
    public class FExchanger : RustPlugin
    {
        #region Var

        [PluginReference] private Plugin ImageLibrary;
        public Timer anyCheckDay;
        public string Layer = "UI_FExchangerMask";




        public Dictionary<ulong, Dictionary<int, long>> _playerData = new Dictionary<ulong, Dictionary<int, long>>();

        #endregion
        
        #region Hooks

        void OnServerInitialized()
        {
            PrintWarning("\n-----------------------------\n " +" Author - " +" VK -  " +" Forum -  " +" Discord - " +"-----------------------------");  
            try
            {
                _playerData = Interface.GetMod().DataFileSystem
                    .ReadObject<Dictionary<ulong, Dictionary<int, long>>>($"{Name}/PlayerLimit");
                if (_playerData == null)
                    _playerData = new Dictionary<ulong, Dictionary<int, long>>();
                
            }
            catch
            {
                _playerData = new Dictionary<ulong, Dictionary<int, long>>();
            }
            
            if (ImageLibrary == null || !ImageLibrary.IsLoaded)
            {
                PrintError("IMAGE LIBRARY IS NOT INSTALLED!");
            }
            else
            {
                var imagesList = new Dictionary<string, string>();

                foreach (var imageKeys in config._EList)
                {
                    if (!string.IsNullOrEmpty(imageKeys._GiveExchanger.ImageGive) && !imagesList.ContainsKey(imageKeys._GiveExchanger.ImageGive))
                    {
                        imagesList.Add(imageKeys._GiveExchanger.ImageGive, imageKeys._GiveExchanger.ImageGive);
                    }
                    if (!string.IsNullOrEmpty(imageKeys._ItemExchanger.ImageExchanger) && !imagesList.ContainsKey(imageKeys._ItemExchanger.ImageExchanger))
                    {
                        imagesList.Add(imageKeys._ItemExchanger.ImageExchanger, imageKeys._ItemExchanger.ImageExchanger);
                    }
                }

                ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
            }
            

            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }

            anyCheckDay = timer.Every(30, () => InvokeUpdateDayAny());
        }


        void Unload()
        {
            if (anyCheckDay != null)
                anyCheckDay.Destroy();
            
            Interface.GetMod().DataFileSystem.WriteObject($"{Name}/PlayerLimit", _playerData);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (!_playerData.ContainsKey(player.userID))
                _playerData.Add(player.userID, new Dictionary<int, long>());
        }
        
        object CanLootEntity(BasePlayer player, ShopFront container)
        {
            if (player == null || container == null)
            {
                return null;
            }

            if (container.skinID == 100)
            {
                StartUI(player);
                NextTick(player.EndLooting);
            }
            return null;
        }

        #endregion

        #region Functional
        
        
        public int GetAmount(IEnumerable<Item> itemList, string shortname, ulong skinId)
        {
            int num = 0;
            foreach (Item obj in itemList)
            {
                if (obj.info.shortname == shortname && obj.skin == skinId)
                    num += obj.amount;
            }

            return num;
        }

        private int Take(IEnumerable<Item> itemList, string shortname, ulong skinId, int iAmount)
        {
            int num1 = 0;
            if (iAmount == 0)
                return 0;

            var list = Facepunch.Pool.GetList<Item>();
            foreach (var obj in itemList)
            {
                if (obj.info.shortname == shortname && obj.skin == skinId)
                {
                    int num2 = iAmount - num1;
                    if (num2 > 0)
                    {
                        if (obj.amount > num2)
                        {
                            obj.MarkDirty();
                            obj.amount -= num2;
                            num1 += num2;

                            break;
                        }

                        if (obj.amount <= num2)
                        {
                            num1 += obj.amount;
                            list.Add(obj);
                        }

                        if (num1 == iAmount)
                            break;
                    }
                }
            }

            foreach (var obj in list)
                obj.Remove();
            Facepunch.Pool.FreeList<Item>(ref list);
            return num1;
        }
        


        public void InvokeUpdateDayAny()
        {
            if (config.TimeToClear == DateTime.Now.ToString("t"))
            {
                foreach (var key in _playerData)
                {
                    key.Value?.Clear();
                }
                if (anyCheckDay != null)
                    anyCheckDay.Destroy();

                anyCheckDay = timer.Once(120, () =>
                {
                    anyCheckDay = timer.Every(30, InvokeUpdateDayAny);
                });
            }
        }
        
        public string HexToCuiColor(string HEX, float Alpha = 100)
        {
            if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

            var str = HEX.Trim('#');
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {Alpha / 100}";
        }
        

        #endregion


        #region Chat && Console Command


        [ConsoleCommand("FExchangerHandler")]
        void FExchangerHandler(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();

            if (args.Args[0] == "obmen")
            {
                var find = config._EList.FirstOrDefault(p => p.UniversalKey == int.Parse(args.Args[1]));
                if (find == null) return;
                var data = _playerData[player.userID];
                if (find.Limit > 0)
                {
                    if (data.ContainsKey(find.UniversalKey) && data[find.UniversalKey] >= find.Limit)
                    {
                        player.ChatMessage("Вы уже использовали лимит обмена на сегодня, приходи завтра!");
                        return;
                    }
                    
                    if (!data.ContainsKey(find.UniversalKey))
                        data.Add(find.UniversalKey, 0);


                    long amountLimit = data[find.UniversalKey];

                    if (find.AmountGive + amountLimit >= find.Limit)
                    {
                        player.ChatMessage("Вы уже использовали лимит обмена на сегодня, приходи завтра!");
                        return;
                    }
                    
                    
                    int amount = GetAmount(player.inventory.AllItems(), find._ItemExchanger.ShortNameExchanger,
                        find._ItemExchanger.SkinIDExchanger);

                    if (amount < find.AmountExchanger)
                    {
                        player.ChatMessage("У вас недостаточно ресурсов для обмена!");
                        return;
                    }


                    Take(player.inventory.AllItems(), find._ItemExchanger.ShortNameExchanger,
                        find._ItemExchanger.SkinIDExchanger, find.AmountExchanger);
                    


              
                    
                    var item = ItemManager.CreateByName(find._GiveExchanger.ShortNameGive,
                        find.AmountGive, find._GiveExchanger.SkinIDGive);
                    if (!string.IsNullOrEmpty(find._GiveExchanger.DisplayNameGive))
                        item.name = find._GiveExchanger.DisplayNameGive;
                    
                    player.GiveItem(item);

                    if (data.ContainsKey(find.UniversalKey))
                        data[find.UniversalKey] += find.AmountGive;


                    if (find.Limit > 0)
                    {
                        int number = int.Parse(args.Args[2]);
                        var container = new CuiElementContainer();
                        CuiHelper.DestroyUi(player, Layer + $".{number}.ExchangerListUpdate");
                        
                        string text = data.ContainsKey(find.UniversalKey) ? $"ЛИМИТ: {data[find.UniversalKey]}/{find.Limit}" : $"ЛИМИТ: 0/{find.Limit}";
                    
                        container.Add(new CuiElement()
                        {
                            Parent = Layer + $".{number}.ExchangerList",
                            Name = Layer + $".{number}.ExchangerListUpdate",
                            Components =
                            {
                                new CuiTextComponent{Color = HexToCuiColor("#E4C680"),Text = text, Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf"},
                                new CuiRectTransformComponent{AnchorMin = "0.3355263 0", AnchorMax = "0.6644735 0.4838714"},
                            }
                        });

                        CuiHelper.AddUi(player, container);
                    }
                }
                else
                {
                    
                    
                    int amount = GetAmount(player.inventory.AllItems(), find._ItemExchanger.ShortNameExchanger,
                        find._ItemExchanger.SkinIDExchanger);

                    int getAmount = amount / find.AmountExchanger;


                    if (getAmount <= 0)
                    {
                        player.ChatMessage("У вас недостаточно ресурсов для обмена!");
                        return;
                    }

                    Take(player.inventory.AllItems(), find._ItemExchanger.ShortNameExchanger,
                        find._ItemExchanger.SkinIDExchanger, getAmount * find.AmountExchanger);
                    
                    int amountToGive = find.AmountGive * getAmount;
                    
                    var item = ItemManager.CreateByName(find._GiveExchanger.ShortNameGive,
                        amountToGive, find._GiveExchanger.SkinIDGive);
                    
                    if (!string.IsNullOrEmpty(find._GiveExchanger.DisplayNameGive))
                        item.name = find._GiveExchanger.DisplayNameGive;
                    
                    player.GiveItem(item);
                }
                
            }
        }

        #endregion

        #region UI



        public void StartUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);

            var container = new CuiElementContainer();
            
             #region Panel | Color Panel

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

            #endregion

            #region Panel Main

            
            container.Add(new CuiButton 
            {
                RectTransform = { AnchorMin = "0.3052083 0.1879629", AnchorMax = "0.6958333 0.812963"},
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

            #endregion

            #region Text

            
            container.Add(new CuiElement() 
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent{Color = HexToCuiColor("#E4C680"),Text = $"Сброс лимитов в {config.TimeToClear} по МСК", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf"},
                    new CuiRectTransformComponent{AnchorMin = "0.3052083 0.1935185", AnchorMax = "0.6958333 0.237963"},
                }
            });
            

            #endregion
            
            #region Close

            container.Add(new CuiButton 
            {
                RectTransform = { AnchorMin = "0.6145834 0.1935185", AnchorMax = "0.6927084 0.237963"},
                Button =
                {
                    Close = Layer,
                    Color = HexToCuiColor("#fffdfc", 10), Material = "assets/content/ui/uibackgroundblur.mat"
                },
                Text =
                {
                    Text = "ЗАКРЫТЬ", Color = HexToCuiColor("#fcf7f6"),
                    Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-bold.ttf"
                },

            }, Layer);

            #endregion

            #region Text

            container.Add(new CuiElement() 
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent{Color = HexToCuiColor("#e7dfd4"),Text = $"Обмен предметов", Align = TextAnchor.MiddleCenter, FontSize = 22, Font = "robotocondensed-bold.ttf"},
                    new CuiRectTransformComponent{AnchorMin = "0.3052083 0.753697", AnchorMax = "0.6958333 0.79814"},
                }
            });

            #endregion

            CuiHelper.AddUi(player, container);

            OpenExchangerList(player);
        }

        public void OpenExchangerList(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();


            var data = _playerData[player.userID];

            for (int i = 0; i < 5; i++)
            {
                CuiHelper.DestroyUi(player, Layer + $".{i}.ExchangerList");
            }

            foreach (var check in config._EList.Select((i, t) => new { A = i, B = t }).Take(5))
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin =
                            $"{0.3817708 + check.B * 0.370 - Math.Floor((double) check.B / 1) * 1 * 0.370} {0.6574074 - Math.Floor((double) check.B / 1) * 0.095}",
                        AnchorMax =
                            $"{0.6192709 + check.B * 0.370 - Math.Floor((double) check.B / 1) * 1 * 0.370} {0.7435185 - Math.Floor((double) check.B / 1) * 0.095}",
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
                }, Layer, Layer + $".{check.B}.ExchangerList");
                
                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "0.2039474 1"
                    },
                    Image =
                    {
                        Color = HexToCuiColor("#000000", 50),
                    }
                },Layer + $".{check.B}.ExchangerList", $"{check.B}.ImageExchanger");
                
                container.Add(new CuiElement
                {
                    Parent = $"{check.B}.ImageExchanger",
                    Components =
                    {
                        new CuiRawImageComponent
                            {FadeIn = 0.3f, Png = (string)ImageLibrary.Call("GetImage", string.IsNullOrEmpty(check.A._ItemExchanger.ImageExchanger) ? check.A._ItemExchanger.ShortNameExchanger : check.A._ItemExchanger.ImageExchanger)},
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "2 2", OffsetMax = "-2 -2"}
                    }
                });
                
                container.Add(new CuiElement()
                {
                    Parent = $"{check.B}.ImageExchanger",
                    Components =
                    {
                        new CuiTextComponent{Color = HexToCuiColor("#e7dfd4"),Text = $"x{check.A.AmountExchanger}", Align = TextAnchor.LowerRight, FontSize = 14, Font = "robotocondensed-bold.ttf"},
                        new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 2", OffsetMax = "-3 0"},
                    }
                });

                if (check.A.Limit > 0)
                {
                    string text = data.ContainsKey(check.A.UniversalKey) ? $"ЛИМИТ: {data[check.A.UniversalKey]}/{check.A.Limit}" : $"ЛИМИТ: 0/{check.A.Limit}";
                    
                    container.Add(new CuiElement()
                    {
                        Parent = Layer + $".{check.B}.ExchangerList",
                        Name = Layer + $".{check.B}.ExchangerListUpdate",
                        Components =
                        {
                            new CuiTextComponent{Color = HexToCuiColor("#E4C680"),Text = text, Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf"},
                            new CuiRectTransformComponent{AnchorMin = "0.3355263 0", AnchorMax = "0.6644735 0.4838714"},
                        }
                    });
                }
                
                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.7960506 0", AnchorMax = "1 1"
                    },
                    Image =
                    {
                        Color = HexToCuiColor("#000000", 50),
                    }
                },Layer + $".{check.B}.ExchangerList", $"{check.B}.ImageGive");
                
                container.Add(new CuiElement
                {
                    Parent = $"{check.B}.ImageGive",
                    Components =
                    {
                        new CuiRawImageComponent
                            {FadeIn = 0.3f, Png = (string)ImageLibrary.Call("GetImage", string.IsNullOrEmpty(check.A._GiveExchanger.ImageGive) ? check.A._GiveExchanger.ShortNameGive : check.A._GiveExchanger.ImageGive)},
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "2 2", OffsetMax = "-2 -2"}
                    }
                });
                
                container.Add(new CuiElement()
                {
                    Parent = $"{check.B}.ImageGive",
                    Components =
                    {
                        new CuiTextComponent{Color = HexToCuiColor("#e7dfd4"),Text = $"x{check.A.AmountGive}", Align = TextAnchor.LowerRight, FontSize = 14, Font = "robotocondensed-bold.ttf"},
                        new CuiRectTransformComponent{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 2", OffsetMax = "-3 0"},
                    }
                });


                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.3355263 0.4838715", AnchorMax = "0.6644735 1"},
                    Button =
                    {
                        Color = HexToCuiColor("#000000", 40),
                        Command = $"FExchangerHandler obmen {check.A.UniversalKey} {check.B}"
                    },
                    Text =
                    {
                        Text = "ОБМЕНЯТЬ", Color = HexToCuiColor($"#e7dfd4"),
                        Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-bold.ttf"
                    }
                }, Layer + $".{check.B}.ExchangerList");
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


        public class ItemExchanger
        {
            [JsonProperty("ShortName Обмениваемого предмета")] 
            public string ShortNameExchanger;

            [JsonProperty("SkinID Обмениваемого предмета")]
            public ulong SkinIDExchanger;

            [JsonProperty("Картинка обмениваемого предмета ( оставье пустую строчку если вы хотите использовать картинку от ShortName )")]
            public string ImageExchanger;
        }

        public class GiveExchanger
        {
            [JsonProperty("ShortName Получаемого предмета")]
            public string ShortNameGive;

            [JsonProperty("SkinID Получаемого предмета")]
            public ulong SkinIDGive;

            [JsonProperty("Дисплейное имя получаемого предмета")]
            public string DisplayNameGive;

            [JsonProperty("Картинка получаемого предмета ( оставье пустую строчку если вы хотите использовать картинку от ShortName )")]
            public string ImageGive;
        }
        
        
        public class ExchangerSettings
        {
            [JsonProperty("Настройка обмениваемого предмета")]
            public ItemExchanger _ItemExchanger = new ItemExchanger();

            [JsonProperty("Настройка получаемого предмета при обмене")]
            public GiveExchanger _GiveExchanger = new GiveExchanger();

            [JsonProperty("Нужное количество обмениваемого предмета")]
            public int AmountExchanger;

            [JsonProperty("Количество получаемого предмета")]
            public int AmountGive;

            [JsonProperty("Лимит ( оставьте 0 если вы хотите выключить лимит на данный обмен )")]
            public int Limit;

            [JsonProperty("Универсальный ключ ( Указывайте абсолютное любое число, главное что бы оно не совпадало с другими )")]
            public int UniversalKey;
        }


        private class PluginConfig
        {
            [JsonProperty("Во сколько делать сброс лимитов")]
            public string TimeToClear;

            [JsonProperty("Настройка обменника")] 
            public List<ExchangerSettings> _EList = new List<ExchangerSettings>();

            [JsonProperty("Версия конфигурации")]
            public VersionNumber PluginVersion = new VersionNumber();

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    TimeToClear = "04:00",
                    _EList = new List<ExchangerSettings>()
                    {
                        new ExchangerSettings()
                        {
                            _ItemExchanger = new ItemExchanger()
                            {
                                ShortNameExchanger = "corn",
                                SkinIDExchanger = 0,
                                ImageExchanger = ""
                            },
                            _GiveExchanger = new GiveExchanger()
                            {
                                ShortNameGive = "bleach",
                                SkinIDGive = 2916405375,
                                DisplayNameGive = "Урановая руда",
                                ImageGive = "https://i.imgur.com/YOe3Z5j.png",
                            },
                            AmountExchanger = 250,
                            AmountGive = 3,
                            Limit = 5000,
                            UniversalKey = 1,
                        },
                        new ExchangerSettings()
                        {
                            _ItemExchanger = new ItemExchanger()
                            {
                                ShortNameExchanger = "fish.troutsmall",
                                SkinIDExchanger = 0,
                                ImageExchanger = ""
                            },
                            _GiveExchanger = new GiveExchanger()
                            {
                                ShortNameGive = "bleach",
                                SkinIDGive = 2916405375,
                                DisplayNameGive = "Урановая руда",
                                ImageGive = "https://i.imgur.com/YOe3Z5j.png",
                            },
                            AmountExchanger = 25,
                            AmountGive = 3,
                            Limit = 5000,
                            UniversalKey = 2,
                        },
                        new ExchangerSettings()
                        {
                            _ItemExchanger = new ItemExchanger()
                            {
                                ShortNameExchanger = "fish.troutsmall",
                                SkinIDExchanger = 0,
                                ImageExchanger = ""
                            },
                            _GiveExchanger = new GiveExchanger()
                            {
                                ShortNameGive = "sulfur",
                                SkinIDGive = 0,
                                DisplayNameGive = "",
                                ImageGive = "",
                            },
                            AmountExchanger = 250,
                            AmountGive = 10000,
                            Limit = 500,
                            UniversalKey = 3,
                        },
                        new ExchangerSettings()
                        {
                            _ItemExchanger = new ItemExchanger()
                            {
                                ShortNameExchanger = "fish.troutsmall",
                                SkinIDExchanger = 0,
                                ImageExchanger = ""
                            },
                            _GiveExchanger = new GiveExchanger()
                            {
                                ShortNameGive = "sulfur",
                                SkinIDGive = 0,
                                DisplayNameGive = "",
                                ImageGive = "",
                            },
                            AmountExchanger = 250,
                            AmountGive = 10000,
                            Limit = 250,
                            UniversalKey = 4,
                        },
                        new ExchangerSettings()
                        {
                            _ItemExchanger = new ItemExchanger()
                            {
                                ShortNameExchanger = "fish.troutsmall",
                                SkinIDExchanger = 0,
                                ImageExchanger = ""
                            },
                            _GiveExchanger = new GiveExchanger()
                            {
                                ShortNameGive = "sulfur",
                                SkinIDGive = 0,
                                DisplayNameGive = "",
                                ImageGive = "",
                            },
                            AmountExchanger = 250,
                            AmountGive = 10000,
                            Limit = 500,
                            UniversalKey = 5,
                        },
                    },
                    
                    
                    PluginVersion = new VersionNumber(),
                };
            }
        }

        #endregion
    }
}