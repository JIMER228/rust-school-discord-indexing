using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Oxide.Core.Plugins;
using WebSocketSharp;

namespace Oxide.Plugins
{
    [Info("BHelp", "Hougan", "0.0.1")]
    public class BHelp : RustPlugin
    {
        #region Classess

        private class Info
        {
            public class ImagePage
            {
                [JsonProperty("Заголовок (если необходимо)")]
                public string Header;
                [JsonProperty("Изображение")]
                public string Image;
            }
            
            [JsonProperty("Отображаемое название")]
            public string DisplayName;
            [JsonProperty("Главное изображение")]
            public string ImageURL;

            [JsonProperty("Страницы с помощью")]
            public List<ImagePage> Pages = new List<ImagePage>();
        }

        private class Configuration
        {
            [JsonProperty("Возможная информация")]
            public List<Info> Infos = new List<Info>();
            [JsonProperty("Команда для открытия интерфейса")]
            public string ExecuteCommand;
            [JsonProperty("Затемнять задний фон")]
            public bool EnableShadow;

            public static Configuration Generate()
            {
                return new Configuration
                {
                    EnableShadow = true,
                    ExecuteCommand = "help",
                    Infos = new List<Info> 
                    {
						new Info
                        {
                            DisplayName = "Телепорт",
                            ImageURL = "https://i.imgur.com/QpBdQeL.png",
                            
                            Pages = new List<Info.ImagePage>
                            {
                                new Info.ImagePage
                                {
                                    Header = "ЗАГОЛОВОК №1",
                                    Image = "https://i.imgur.com/Nt41Xpc.png"
                                },
								new Info.ImagePage
                                {
                                    Header = "ЗАГОЛОВОК №1",
                                    Image = "https://i.imgur.com/dPAVPXl.png"
                                },
                            }
                        },
                        new Info
                        {
                            DisplayName = "Волшебный саженец",
                            ImageURL    = "https://i.imgur.com/GQ7KY1Q.png",
                            
                            Pages = new List<Info.ImagePage>
                            {
                                new Info.ImagePage
                                {
                                    Header = "ЗАГОЛОВОК №1",
                                    Image  = "https://i.imgur.com/y6TeGl1.png"
                                },
                            }
                        },
                        new Info
                        {
                            DisplayName = "Золотой череп",
                            ImageURL    = "https://i.imgur.com/WlD4vtO.png",
                            
                            Pages = new List<Info.ImagePage>
                            {
                                new Info.ImagePage
                                {
                                    Header = "ЗАГОЛОВОК №1",
                                    Image  = "https://i.imgur.com/sTAXEAz.png"
                                },
                            }
                        },
                        new Info
                        {
                            DisplayName = "Кристалл",
                            ImageURL    = "https://i.imgur.com/WlD4vtO.png",
                            
                            Pages = new List<Info.ImagePage>
                            {
                                new Info.ImagePage
                                {
                                    Header = "ЗАГОЛОВОК №1",
                                    Image  = "https://i.imgur.com/uEBhHF9.png"
                                },
                            }
                        },
                        new Info
                        {
                            DisplayName = "Золотая рыбка",
                            ImageURL    = "https://i.imgur.com/WlD4vtO.png",
                            
                            Pages = new List<Info.ImagePage>
                            {
                                new Info.ImagePage
                                {
                                    Header = "ЗАГОЛОВОК №1",
                                    Image  = "https://i.imgur.com/AkGDtIu.png"
                                },
                            }
                        },
						new Info
                        {
                            DisplayName = "⃤",
                            ImageURL    = "https://i.imgur.com/WlD4vtO.png",
                            
                            Pages = new List<Info.ImagePage>
                            {
                                new Info.ImagePage
                                {
                                    Header = "ЗАГОЛОВО1",
                                    Image  = "https://i.imgur.com/PsJGb3H.png"
                                },
                            }
                        },
						new Info
                        {
                            DisplayName = "Правила",
                            ImageURL    = "https://i.imgur.com/WlD4vtO.png",
                            
                            Pages = new List<Info.ImagePage>
                            {
                                new Info.ImagePage
                                {
                                    Header = "ЗАГОЛОВОК №1",
                                    Image  = "https://i.imgur.com/eQvLjjL.png"
                                },
								new Info.ImagePage
                                {
                                    Header = "ЗАГОЛОВОК №1",
                                    Image  = "https://i.imgur.com/JO0uYJr.png"
                                },
                            }
                        },
						new Info
                        {
                            DisplayName = "Бонусный кит",
                            ImageURL    = "https://i.imgur.com/WlD4vtO.png",
                            
                            Pages = new List<Info.ImagePage>
                            {
                                new Info.ImagePage
                                {
                                    Header = "ЗАГОЛОВОК №1",
                                    Image  = "https://i.imgur.com/6moyBNz.png"
                                },
                            }
                        },
						new Info
                        {
                            DisplayName = "Наши соц.сети",
                            ImageURL = "https://i.imgur.com/QpBdQeL.png",
                            
                            Pages = new List<Info.ImagePage>
                            {
                                new Info.ImagePage
                                {
                                    Header = "ЗАГОЛОВОК №1",
                                    Image = "https://i.imgur.com/DBsU19I.png"
                                },
                            }
                        },
                    }
                };
            }
        }
        
        #endregion

        #region Variables

        [PluginReference] private Plugin ImageLibrary;
        private static Configuration Settings = Configuration.Generate(); 

        #endregion

        #region Initialization
        
        /*protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                Settings = Config.ReadObject<Configuration>();
            }
            catch
            { 
                PrintWarning($"Ошибка чтения конфигурации 'oxide/config/{Name}'!");
                return;
            }
            
            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => Settings = Configuration.Generate();
        protected override void SaveConfig()        => Config.WriteObject(Settings);*/

        private void OnServerInitialized()
        {
            if (!ImageLibrary)
            {
                PrintError($"ImageLibrary is not loaded!");
                return;
            }

            cmd.AddChatCommand(Settings.ExecuteCommand, this, nameof(InitializeMenuCommand)); 
            foreach (var check in Settings.Infos)
            {
                int index = Settings.Infos.IndexOf(check);

                ImageLibrary.Call("AddImage", check.ImageURL, $"BHelp.{index}.Header");
                foreach (var picture in check.Pages)
                {
                    int newIndex = check.Pages.IndexOf(picture);
                    
                    ImageLibrary.Call("AddImage", picture.Image, $"BHelp.{index}.{newIndex}");
                }
            }
        }

        #endregion

        #region Commands

        [ConsoleCommand("UI_BHelp_Handler")]
        private void CmdConsoleHandler(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null || !args.HasArgs(1)) return;

            switch (args.Args[0].ToLower())
            {
                case "page":
                {
                    int page = -1;
                    if (!int.TryParse(args.Args[1], out page)) return;

                    //InitializeMenu(player, page);
                    break;
                }
                case "open":
                {
                    int index = -1;
                    if (!int.TryParse(args.Args[1], out index)) return;

                    int newPage = 0;
                    if (args.HasArgs(3))
                        int.TryParse(args.Args[2], out newPage);
                    
                    var el = Settings.Infos.ElementAtOrDefault(index);
                    if (el == null || el.DisplayName.Length < 2) return;
                    
                    InitializeMenu(player, el, newPage);
                    break;
                }
            }
        }

        private void InitializeMenuCommand(BasePlayer player) => InitializeMenu(player, Settings.Infos.FirstOrDefault(), 0);

        #endregion

        #region Interface

        private static string Layer = "UI_BHelp.Help";
        private void InitializeMenu(BasePlayer player, Info info, int page)
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer container = new CuiElementContainer(); 

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1.43 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                Image         = {Color     = $"0 0 0 0" }
            }, "UI_RustMenu_Internal", Layer);

            container.Add(new CuiPanel()
            {  
                CursorEnabled = true,
                RectTransform = {AnchorMin = "-0.04 0", AnchorMax = "0.18 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                Image         = {Color = "0.549 0.270 0.215 0.7", Material = "" }
            }, Layer, Layer + ".C");
            container.Add(new CuiPanel()
                { 
                    CursorEnabled = true,
                    RectTransform = {AnchorMin = "0 -0.05", AnchorMax = "1 0.5", OffsetMin = "0 0", OffsetMax = "0 0"},
                    Image         = {Color = "0 0 0 0.9", Sprite = "assets/content/ui/ui.gradient.up.psd"} 
                }, Layer + ".C");
                                     
            container.Add(new CuiPanel()
                { 
                    CursorEnabled = true,
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0.3", OffsetMin = "0 0", OffsetMax = "0 0"},
                    Image         = {Color = "0 0 0 0.2", Sprite = "assets/content/ui/ui.background.transparent.radial.psd"}
                }, Layer + ".C");

            
            container.Add(new CuiPanel()
            { 
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0.18 0", AnchorMax = "0.665 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                Image         = {Color = "0.117 0.121 0.109 0.95" }
            }, Layer, Layer + ".R");

            var list = Settings.Infos.ToList();
            float topPosition = (list.Count() / 2f * 40 + (list.Count() - 1) / 2f * 5);
            foreach (var vip in list) 
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0.5", AnchorMax = "1 0.51", OffsetMin = $"0 {topPosition - 40}", OffsetMax = $"0 {topPosition}" },
                    Button = { Color = info == vip ? "0.149 0.145 0.137 0.8" : "0 0 0 0", Command = $"UI_BHelp_Handler open {list.ToList().IndexOf(vip)} 0"},
                    Text = { Text = "", Align = TextAnchor.MiddleRight, Font = "robotocondensed-bold.ttf", FontSize = 14 }
                }, Layer + ".C", Layer + vip.DisplayName);
                
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"0 0", OffsetMax = $"-20 0" },
                    Text = { Text = vip.DisplayName.ToUpper(), Align = TextAnchor.MiddleRight, Font = "robotocondensed-bold.ttf", FontSize = 24, Color = "0.929 0.882 0.847 1"}
                }, Layer + vip.DisplayName);
                
                topPosition -= 40 + 5;
            }

            container.Add(new CuiElement
            {
                Parent = Layer + ".R",
                Components =
                {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", $"BHelp.{Settings.Infos.IndexOf(info)}.{page}") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-280 -299", OffsetMax = "280 359"}
                }
            });
            
            #region PaginationMember

            string leftCommand = $"UI_BHelp_Handler open {Settings.Infos.IndexOf(info)} {page-1}"; 
            string rightCommand = $"UI_BHelp_Handler open {Settings.Infos.IndexOf(info)} {page+1}";
            bool leftActive = page > 0;
            bool rightActive = page + 1 < info.Pages.Count;
 
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = $"-80 10", OffsetMax = "80 40" },
                Image = { Color = "0 0 0 0" } 
            }, Layer + ".R", Layer + ".PS");
			
			container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = (page + 1).ToString(), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 16, Color = "0.8 0.768 0.741 1" }
            }, Layer + ".PS"); 
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.45 1", OffsetMin = $"0 0", OffsetMax = "-0 -0" },
                Image = { Color = leftActive ? "0.294 0.38 0.168 0" : "0.294 0.38 0.168 0" }
            }, Layer + ".PS", Layer + ".PS.L");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = leftActive ? leftCommand : "" },
                Text = { Text = "<b>◄</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = leftActive ? "0.647 0.917 0.188 1" : "0.294 0.38 0.168 0.3" }
            }, Layer + ".PS.L");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.55 0", AnchorMax = "1 1", OffsetMin = $"0 0", OffsetMax = "-0 -0" },
                Image = { Color = rightActive ? "0.294 0.38 0.168 0" : "0.294 0.38 0.168 0" }
            }, Layer + ".PS", Layer + ".PS.R");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = rightActive ? rightCommand : "" },
                Text = { Text = "<b>►</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.647 0.917 0.188 1" : "0.294 0.38 0.168 0.3" }
            }, Layer + ".PS.R");

            #endregion

            CuiHelper.AddUi(player, container);
        }

        private void InitializeInfo(BasePlayer player, Info info, int page, int subPage)
        {
            CuiElementContainer container = new CuiElementContainer(); 
            CuiHelper.DestroyUi(player, Layer); 
             
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-1000 -1000", OffsetMax = "1000 1000"},
                Image         = {Color     = $"0 0 0 {(Settings.EnableShadow ? "0.7" : "0")}" }
            }, "Overlay2", Layer);

            container.Add(new CuiButton
            { 
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                Button        = {Color = "0 0 0 0.9", Material = "assets/content/ui/uibackgroundblur-notice.mat", Close = Layer, Command = $"UI_BHelp_Handler page {page}" },
                Text          = {Text      = ""} 
            }, Layer);

            float currentAnchor = 0.25f;
            float width = 0.5f / info.Pages.Count;
            foreach (var check in info.Pages.Select((i,t) => new { A = i, B = t}))
            {
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = $"{currentAnchor} 0.5", AnchorMax = $"{currentAnchor + width} 0.5", OffsetMin = "5 -265", OffsetMax = "-5 -260"},
                    Button        = {Color     = subPage == check.B ? "1 1 1 0.8" : "1 1 1 0.4", Command = $"UI_BHelp_Handler open {Settings.Infos.IndexOf(info)} {page} {check.B}" }, 
                    Text          = {Text      = ""}  
                }, Layer);

                currentAnchor += width; 
            }

            var cPage = info.Pages.ElementAtOrDefault(subPage);
            if (cPage == null) return;
            
            if (!cPage.Header.IsNullOrEmpty()) 
            {
                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 0.5", AnchorMax   = "1 0.5", OffsetMin = "0 250", OffsetMax              = "0 2900"},
                    Text          = {Text      = cPage.Header, Align = TextAnchor.LowerCenter, Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.8", FontSize = 32 }
                }, Layer);
            }
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-200 -50", OffsetMax = "0 0" },
                Button        = { Color     = "0 0 0 0", Material  = "", Close = Layer, Command = $"UI_BHelp_Handler page {page}" },
                Text          = { Text      = "НАЗАД", Color           = "1 1 1 0.6", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 32 }
            }, Layer);
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-500 -125", OffsetMax = "-435 125" },
                Button        = { Color     = "0 0 0 0", Material = "", Command         = subPage > 0 ? $"UI_BHelp_Handler open {Settings.Infos.IndexOf(info)} {page} {subPage -1}" : "" },
                Text          = { Text      = "<b><</b>", Color          = subPage > 0 ? "1 1 1 1" : "1 1 1 0.2", Align = TextAnchor.MiddleRight, Font = "robotocondensed-regular.ttf", FontSize = 80 }
            }, Layer);
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "435 -125", OffsetMax = "500 125" },
                Button        = { Color     = "0 0 0 0", Material = "", Command         = (subPage + 1) < info.Pages.Count ? $"UI_BHelp_Handler open {Settings.Infos.IndexOf(info)} {page} {subPage + 1}" : "" },
                Text          = { Text      = "<b>></b>", Color          = (subPage + 1) < info.Pages.Count ? "1 1 1 1" : "1 1 1 0.2", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 80 }
            }, Layer);
            
            container.Add(new CuiElement 
            {
                Parent = Layer,
                Components =
                {
                    new CuiRawImageComponent() {Png          = (string) ImageLibrary.Call("GetImage", $"BHelp.{Settings.Infos.IndexOf(info)}.{subPage}")},
                    new CuiRectTransformComponent {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-432 -243", OffsetMax = "432 243"} 
                }
            });
            
            CuiHelper.AddUi(player, container);
        }

        #endregion
    }
}