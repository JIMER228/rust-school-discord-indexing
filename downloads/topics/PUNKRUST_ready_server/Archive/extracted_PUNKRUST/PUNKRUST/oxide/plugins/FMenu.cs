using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("FMenu","Netrunner","1.0")]
    public class FMenu : RustPlugin
    {
        [PluginReference] private Plugin ImageLibrary,EconomicsEvo;
        Dictionary<string,int> _itemPosition = new Dictionary<string, int>();


        #region Config

        class MenuItem
        {
            [JsonProperty("Является категорией?")]
            public bool isCategory = false;
            [JsonProperty("Текст")]
            public string Name = "КНОПКА";
            [JsonProperty("Команда")]
            public string Command = "test";
        }
        
        class CategoryItem
        {
            [JsonProperty("Текст")]
            public string Name = "";
            [JsonProperty("Команда")]
            public string Command = "";
        }

        class Category
        {
            [JsonProperty("Кнопки категории")]
            public Dictionary<string, CategoryItem> Items = new Dictionary<string, CategoryItem>();
        }

        class StaticButton
        {
            public string Name = "static";

            public string Command = "command";
        }

        class CommandSettings
        {
            [JsonProperty("Ключ категории")]
            public string Category;
            [JsonProperty("Command категории")]
            public string CatCom = "";
            [JsonProperty("Ключ кнопки")]
            public string Button;
        }
        static Configuration config = new Configuration();

        class Configuration
        {
            [JsonProperty("Задник меню")] 
            public string BackgroundImage = "https://i.ibb.co/cx8ZkQ8/Fallout-76-1.png";
            [JsonProperty("Задник кнопки")]
            public string ButtonBackGround = "https://i.ibb.co/D11nyM2/btn.png";
            [JsonProperty("Логотип")]
            public string LogoImage = "";
            [JsonProperty("Ширина логотипа")]
            public double LogoX = 100;
            [JsonProperty("Высота логотипа")]
            public double LogoY = 100;
            [JsonProperty("Ключ  первой валюты")]
            public string FirstCurKey = "";
            [JsonProperty("Ключ  второй валюты")]
            public string SecondCurKey = "";
            
            [JsonProperty("Цвет текста")]
            public string TextColor = "#f8db7d97";
            [JsonProperty("Цвет активного текста")]
            public string ActiveTextColor = "#000000";
            
            public Dictionary<string,CommandSettings> ChatCommands = new Dictionary<string, CommandSettings>();
            
            public Dictionary<string,MenuItem> MenuItems = new Dictionary<string, MenuItem>();
            public Dictionary<string,Category> Categories = new Dictionary<string, Category>();
            public List<StaticButton> StaticButtons = new List<StaticButton>();
            
            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    ChatCommands =
                    {
                      ["kit"] = new CommandSettings
                      {
                          Category = "cat2",
                          Button = "v1"
                      }  
                    },
                    MenuItems =
                    {
                        ["1"] = new MenuItem(),
                        ["2"] = new MenuItem(),
                        ["3"] = new MenuItem(),
                        ["4"] = new MenuItem(),
                    },
                    Categories =
                    {
                        ["cat1"] = new Category
                        {
                            Items = new Dictionary<string, CategoryItem>
                            {
                                ["i1"] = new CategoryItem(),
                                ["i2"] = new CategoryItem(),
                                ["i3"] = new CategoryItem(),
                                ["i4"] = new CategoryItem(),
                            }
                        }
                    },
                    StaticButtons = new List<StaticButton>
                    {
                        new StaticButton(),
                        new StaticButton(),
                        new StaticButton(),
                        new StaticButton(),
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

        List<ulong> _playersInMenu = new List<ulong>();

        #endregion

        #region Hooks

        void OnServerInitialized()
        {
            InitImages();
            RegisterChatCommands();
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (_playersInMenu.Contains(player.userID)) _playersInMenu.Remove(player.userID);
        }

        #endregion

        #region Methods

        void InitImages()
        {
            ImageLibrary.Call("AddImage", config.BackgroundImage, "bg");
            ImageLibrary.Call("AddImage", config.ButtonBackGround, "btnbg");
            ImageLibrary.Call("AddImage", config.LogoImage, "flogo");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/raG8sxW.png", "fclock");
        }

        void RegisterChatCommands()
        {
            foreach (var command in config.ChatCommands)
                cmd.AddChatCommand(command.Key,this,(player, s, arg3) =>
                {
                    ChatMenuOpen(player,command.Value.Category,command.Value.Button,command.Value.CatCom);
                });
        }
        void ChatMenuOpen(BasePlayer player, string category, string btn,string catcom)
        {
            player.SendConsoleCommand("chat.say /menu");
            player.SendConsoleCommand($"fbutton {category} 1");
            timer.Once(0.5f,() => {player.SendConsoleCommand($"navbtn {btn} {catcom}");});
            
            /*player.SendConsoleCommand("chat.say /menu");
            ClickMenuItem(player,category,true);
            NextFrame(() => {ClickNavMenu(player, btn, catcom);});*/
            
            
        }

        #endregion

        #region UI

        private string _layer = "FmenuUI";
        private string _content = "ContentUI";

        void MenuUI(BasePlayer player)
        {
            //CuiHelper.DestroyUi(player, _layer);
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Parent = "Overlay",
                Name = _layer,
                Components =
                {
                    new CuiRawImageComponent{Png = (string) ImageLibrary.Call("GetImage","bg")},
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",AnchorMax = "1 1"
                    },
                    new CuiNeedsCursorComponent()
                }
            });
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0.5",AnchorMax = "0 0.5",OffsetMin = "0 -360",OffsetMax = "270 360"},
                Image = {Color = "0 0 0 0"}
            }, _layer,"BtnFrame");

            container.Add(new CuiPanel
            {
                Image = {Color = "0.88 0.83 0.63 0.1",},
                RectTransform = {AnchorMin = "1 0",AnchorMax = "1 0",OffsetMin = "-100 45",OffsetMax = "-10 85"}
            }, _layer);
            container.Add(new CuiLabel
            {
                //OffsetMin = "-280 45",OffsetMax = "-100 85"
                //RectTransform = {AnchorMin = "0 0.05",AnchorMax = "1 0.1"},
                RectTransform ={AnchorMin = "1 0",AnchorMax = "1 0",OffsetMin = "-72 45",OffsetMax = "-10 85"},
                Text = {Text = $" {TOD_Sky.Instance.Cycle.DateTime.ToShortTimeString()}",Color = HexToRustFormat(config.TextColor),FontSize = 18,Align = TextAnchor.MiddleLeft}
            }, _layer);
            container.Add(new CuiElement
            {
                Parent = _layer,
                Components =
                {
                    new CuiRectTransformComponent{AnchorMin = "1 0",AnchorMax = "1 0",OffsetMin = "-110 45",OffsetMax = "-70 85"},
                    new CuiRawImageComponent{Color = HexToRustFormat(config.TextColor),Png = (string) ImageLibrary.Call("GetImage","fclock")}
                }
            });

            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                RectTransform = {AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = "5 -195",OffsetMax = "265 -5"}
            },"BtnFrame","LogoFrame");

            double starty = -200, y = 45;
            int pos = 0;

            foreach (var menuItem in config.MenuItems)
            {
                if (!_itemPosition.ContainsKey(menuItem.Key)) _itemPosition.Add(menuItem.Key,pos);
                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "1 1",OffsetMin = $"25 {starty-y}",OffsetMax = $"0 {starty}"},
                    Image = {Color = "0 0 0 0"}
                }, "BtnFrame", $"BtnFrame{menuItem.Key}");
                
                /*container.Add(new CuiElement
                {
                    Parent = $"BtnFrame{menuItem.Key}",
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",AnchorMax = "1 1"
                        },
                        new CuiRawImageComponent
                        {
                            Png = (string) ImageLibrary.Call("GetImage","btnbg"),
                            Color = "0 0 0 0",
                        }
                    }
                });*/
                
                container.Add(new CuiButton
                {
                    Text =
                    {
                        Text = menuItem.Value.Name, Align = TextAnchor.MiddleLeft,
                        Color =  HexToRustFormat(config.TextColor),
                        FontSize = 18
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 0",AnchorMax = "1 1",OffsetMin = $"25 0",OffsetMax = $"0 0",
                    },
                    Button =
                    {
                        Command = $"fbutton {menuItem.Key}",
                        Color = "0 0 0 0",
                    }
                }, $"BtnFrame{menuItem.Key}",$"BtnFrame{menuItem.Key}Btn");

                starty -= y+15;
                pos++;
            }
            
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 1",AnchorMax = "1 1",OffsetMin = $"25 {starty-y}",OffsetMax = $"0 {starty}"},
                Image = {Color = "0 0 0 0"}
            }, "BtnFrame", $"BtnFrameClose");
            
            container.Add(new CuiButton
            {
                Text =
                {
                    Text = "ЗАКРЫТЬ", Align = TextAnchor.MiddleLeft,
                    Color = HexToRustFormat(config.TextColor),
                    FontSize = 18
                },
                RectTransform =
                {
                    AnchorMin = "0 0",AnchorMax = "1 1",OffsetMin = $"25 0",OffsetMax = $"0 0",
                },
                Button =
                {
                    Color = "0 0 0 0",
                    Command = "closefmenu"
                }
            }, $"BtnFrameClose",$"BtnFrameCloseBtn");
            
            

            CuiHelper.AddUi(player, container);
            DrawStaticButtons(player);
            DrawLogo(player);
            UpdateCurrency(player);
        }

        void DrawActiveMenuBtn(BasePlayer player,string key)
        {

            CuiHelper.DestroyUi(player, "AcitveButton");
            double y = 45;
            double starty = -200 - (y + 15) * _itemPosition[key];
            
            
            CuiElementContainer container = new CuiElementContainer();
            
            container.Add(new CuiElement
            {
                Parent = $"BtnFrame{key}",
                Name = "AcitveButton",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",AnchorMax = "1 1"
                    },
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage","btnbg"),
                        Color = "1 1 1 1",
                    }
                }
            });
                
            container.Add(new CuiButton
            {
                Text =
                {
                    Text = config.MenuItems[key].Name, Align = TextAnchor.MiddleLeft,
                    Color = HexToRustFormat(config.ActiveTextColor),
                    FontSize = 18
                },
                RectTransform =
                {
                    AnchorMin = "0 0",AnchorMax = "1 1",OffsetMin = $"25 0",OffsetMax = $"0 0",
                },
                Button =
                {
                    Color = "0 0 0 0",
                    Close = "AcitveButton"
                }
            }, "AcitveButton");

            CuiHelper.AddUi(player, container);
        }

        void CloseMenu(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, _layer);
            _playersInMenu.Remove(player.userID);
        }

        private string _navBar = "CategoryButtons";
        void DrawCategoryMenu(BasePlayer player,string key,bool silence)
        {
            /*if (!config.Categories.ContainsKey(config.MenuItems[key].Command)) return;
            Category category = config.Categories[config.MenuItems[key].Command];*/
            Category category = config.Categories[key];
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.5 0.5",AnchorMax = "0.5 0.5",OffsetMin = "-490 280",OffsetMax = "510 310"},
                Image = {Color = "0 0 0 0.0"}
            }, _layer, _navBar);
            
            double xpos = GetStartPos(category.Items.Values.ToList());
            
            foreach (var categoryItem in category.Items)
            {
                
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0",AnchorMax = "0 1",OffsetMin = $"{xpos} 0",OffsetMax = $"{xpos+GetTextLenght(categoryItem.Value)} 0"},
                    Button = {Color = "0 0 0 0",Command = $"navbtn {categoryItem.Key} {key}"},
                    Text = {Text = $"{categoryItem.Value.Name}",Align = TextAnchor.MiddleCenter,Color = HexToRustFormat($"{config.TextColor}")}
                }, _navBar);
                xpos += GetTextLenght(categoryItem.Value);
            }
            
            CuiHelper.AddUi(player, container);
            if (!silence) ClickNavMenu(player,category.Items.First().Key,key);
        }

        void ClickNavMenu(BasePlayer player, string btnKey, string category)
        {
            DrawNavActive(player,btnKey,category);
            DrawContentLayer(player);
            player.SendConsoleCommand(config.Categories[category].Items[btnKey].Command);
        }

        void DrawNavActive(BasePlayer player, string btnKey,string category)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "NavActive");
            
            double xpos = GetStartPos(config.Categories[category].Items.Values.ToList());
            foreach (var item in config.Categories[category].Items)
            {
                
                if (item.Key == btnKey)
                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = "0 0",AnchorMax = "0 1",OffsetMin = $"{xpos} 0",OffsetMax = $"{xpos+GetTextLenght(item.Value)} 0"},
                        Button = {Color = HexToRustFormat("#5FF6FE"),Command = $"navbtn {item.Key}"},
                        Text = {Text = $"{item.Value.Name}",Align = TextAnchor.MiddleCenter,Color = HexToRustFormat($"{config.ActiveTextColor}")}
                    }, _navBar,"NavActive");

                xpos += GetTextLenght(item.Value);
            }

            CuiHelper.AddUi(player, container);
        }

        void DrawContentLayer(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, _content);
            
            CuiElementContainer container  = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                RectTransform = {AnchorMin = "0.5 0.5",AnchorMax = "0.5 0.5",OffsetMin = "-380 -280",OffsetMax = "635 280"}
            }, _layer, _content);
            CuiHelper.AddUi(player, container);
        }
//{AnchorMin = "0.5 0.5",AnchorMax = "0.5 0.5",OffsetMin = "-270 -280",OffsetMax = "810 280"}
        void ClickMenuItem(BasePlayer player, string key,bool silence = false)
        {
            MenuItem menuItem = config.MenuItems[key];
            CuiHelper.DestroyUi(player, _navBar);
            DrawActiveMenuBtn(player,key);
            DrawContentLayer(player);
            if (menuItem.isCategory)
            {
                
                  DrawCategoryMenu(player,menuItem.Command,silence);
            }
            else
            {
                player.SendConsoleCommand(menuItem.Command);
            }
        }
        
        

        void DrawStaticButtons(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.5 0.5",AnchorMax = "0.5 0.5",OffsetMin = "-380 -310",OffsetMax = "635 -280"},
                Image = {Color = "0 0 0 0"}
            }, _layer, "StaticBtnUI");
            
            double pos = 540;
            double lenght = 0;
            foreach (var button in config.StaticButtons)
            {
                lenght += button.Name.ToCharArray().Length*16;
            }
            pos -= lenght / 1.5 + 8;
            foreach (var button in config.StaticButtons)
            {
                container.Add(new CuiButton
                {
                    Button = {Close = _layer,Command = $"static {button.Command}",Color = "0 0 0 0.3"},
                    Text = {Text = button.Name,FontSize = 16,Color = HexToRustFormat(config.TextColor),Align = TextAnchor.MiddleCenter},
                    RectTransform = {AnchorMin = "0 0",AnchorMax = "0 1",OffsetMin = $"{pos} 0",OffsetMax = $"{pos+button.Name.ToCharArray().Length*16} 0"}
                },"StaticBtnUI");
                pos += button.Name.ToCharArray().Length * 16;
            }

            CuiHelper.AddUi(player, container);

        }

        void UpdateCurrency(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Balance");
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0.88 0.83 0.63 0.1",
                },
                RectTransform = {AnchorMin = "1 0",AnchorMax = "1 0",OffsetMin = "-280 45",OffsetMax = "-100 85"}
            }, _layer,"Balance");
            container.Add(new CuiElement
            {
                Parent = "Balance",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",AnchorMax = "0 1",OffsetMin = "0 0",OffsetMax = "40 0"
                    },
                    new CuiRawImageComponent
                    {
                        Png = (string)ImageLibrary.Call("GetImage",config.FirstCurKey)
                    }
                }
            });
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0",AnchorMax = "0.5 1",OffsetMin = "40 0",OffsetMax = "0 0"},
                Text = {Text = $"{EconomicsEvo.Call("GetBalance",player.userID,config.FirstCurKey)}",FontSize = 18,Color = HexToRustFormat(config.TextColor),Align = TextAnchor.MiddleLeft}
            }, "Balance");
            container.Add(new CuiElement
            {
                Parent = "Balance",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",AnchorMax = "0 1",OffsetMin = "90 0",OffsetMax = "130 0"
                    },
                    new CuiRawImageComponent
                    {
                        Png = (string)ImageLibrary.Call("GetImage",config.SecondCurKey)
                    }
                }
            });
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.5 0",AnchorMax = "1 1",OffsetMin = "40 0",OffsetMax = "0 0"},
                Text = {Text = $"{EconomicsEvo.Call("GetBalance",player.userID,config.SecondCurKey)}",FontSize = 18,Color = HexToRustFormat(config.TextColor),Align = TextAnchor.MiddleLeft}
            }, "Balance");
            
            CuiHelper.AddUi(player, container);
        }

        void DrawLogo(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Parent = "LogoFrame",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",AnchorMax = "0.5 0.5",OffsetMin = $"-{config.LogoX/2} -{config.LogoY/2}",OffsetMax = $"{config.LogoX/2} {config.LogoY/2}"
                    },
                    new CuiRawImageComponent{Png = (string) ImageLibrary.Call("GetImage","flogo")}
                }
            });
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Commands

        [ConsoleCommand("static")]
        void RunStaticBtnCommand(ConsoleSystem.Arg arg)
        {
            CloseMenu(arg.Player());
            arg.Player().SendConsoleCommand(arg.FullString);
        }
        
        [ChatCommand("menu")]
        void MenuCommandRun(BasePlayer player, string command, string[] args)
        {
            if (_playersInMenu.Contains(player.userID)) return;

            MenuUI(player);
            _playersInMenu.Add(player.userID);
        }

        [ConsoleCommand("fbutton")]
        void BtnPressed(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
            {
                if (arg.HasArgs(2))
                    ClickMenuItem(arg.Player(), arg.Args[0], true);
                else
                    ClickMenuItem(arg.Player(), arg.Args[0]);
            }
        }

        [ConsoleCommand("navbtn")]
        void NavBtnPressed(ConsoleSystem.Arg arg)
        {
            ClickNavMenu(arg.Player(),arg.Args[0],arg.Args[1]);
        }
        
        [ConsoleCommand("closefmenu")]
        void PlayerCloseMenu(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null) CloseMenu(arg.Player());
        }

        #endregion

        #region Helper

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
        
        double GetStartPos(List<CategoryItem> categoryItems)
        {
            double pos = 670;
            double lenght = 0;
            foreach (var categoryItem in categoryItems) lenght += GetTextLenght(categoryItem);
            return pos - (lenght / 1.5 + 8);

        }

        double GetTextLenght(CategoryItem categoryItem)
        {
            return categoryItem.Name.ToCharArray().Length*12;
        }

        #endregion
    }
}