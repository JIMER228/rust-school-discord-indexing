using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Globalization;
using Oxide.Core;
using System.IO;
using Newtonsoft.Json;
using WebSocketSharp;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Kits", "TopPlugin.ru/Sempai#3239", "2.0.0")]
    class Kits : RustPlugin
    {
        [ConsoleCommand("kit.info")]
        private void CmdR(ConsoleSystem.Arg args) => DrawInside(args.Player(), _kits.ElementAt(int.Parse(args.Args[0])), 0); 

        
        [JsonProperty("Количество предметов на строке")]
        public int ItemOnString = 7; 
        [JsonProperty("Количество строк в интерфейсе")]
        public int StringAmount = 3;
        [JsonProperty("Сторона одного предмета")]
        public int ItemSide = 100; 
        [JsonProperty("Отступ между предметами")]
        public int ItemMargin = 5;
         
        private void UI_RecountPosition(ref double xSwitch, ref double ySwitch, int count, int max)
        {
            var stringAmount  =  Math.Ceiling((double) max / ItemOnString);
            var currentString = Math.Floor((double) count  / ItemOnString);
            var lastIndex     = (stringAmount - 1) * ItemOnString;

            if (currentString + 1 == stringAmount)
            {
                var value2 = max - lastIndex;
                if (value2 < ItemOnString)
                {
                    max   =  Mathf.FloorToInt((float) value2);
                    count -= Mathf.FloorToInt((float) lastIndex); 
                }
            }
            
            var value = ItemOnString;
            if (max < ItemOnString)
                value = max;  
            
            var currentPosition = count % value;         
 
            
            var topYPosition = 0 + (float) stringAmount / 2 * ItemSide + ((float) stringAmount / 2 - 1) * ItemMargin;
            var topXPosition = 0 - (float) value / 2 * ItemSide        - ((float) value        / 2 - 1) * ItemMargin;        

            var curYPosition = topYPosition - currentString * ItemSide   - (currentString)   * ItemMargin;
            var curXPosition = topXPosition + currentPosition * ItemSide + (currentPosition) * ItemMargin;  

            xSwitch = curXPosition;
            ySwitch = curYPosition;
        }
        
        
        private static string NewLayer = "UI_KitssafsafLayer";
        private void DrawInside(BasePlayer player, Kit kit, int page)
        {
            player.SetFlag(BaseEntity.Flags.Reserved6, false);  
            
            CuiHelper.DestroyUi(player, Layer); 
            CuiHelper.DestroyUi(player, NewLayer);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                Image         = {Color     = "0 0 0 0"}
            }, "Overlay", NewLayer); 
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax    = "1 1", OffsetMax                                       = "0 0"},
                Button        = {Color     = "0 0 0 1", Material = "assets/content/ui/uibackgroundblur-notice.mat", Close = NewLayer, Command = "chat.say /kit"},
                Text          = {Text      = ""}
            }, NewLayer);
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax   = "1 1", OffsetMax = "0 0"},
                Button        = {Color     = "0 0 0 0.7", Close = Layer, Command   = "chat.say /kit"},
                Text          = {Text      = ""}
            }, NewLayer);
			container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "1 1", AnchorMax   = "1 1", OffsetMin = "-50 -50", OffsetMax = "0 0"},
                Button        = {Color     = "0 0 0 0.7", Close = Layer},
                Text          = {Text      = "X", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.7", FontSize = 16}
            }, NewLayer);
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax   = "1 1", OffsetMax = "0 0"},
                Button        = {Color     = "0 0 0 0.7", Close = Layer, Command   = "chat.say /kit"},
                Text          = {Text      = ""}
            }, NewLayer);
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax   = "1 1", OffsetMax = "0 0"},
                Button        = {Color     = "0 0 0 0.7", Close = Layer, Command   = "chat.say /kit"},
                Text          = {Text      = ""}
            }, NewLayer);
/*
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-415 -265", OffsetMax = "415 -260"},
                Image         = {Color     = "1 1 1 0.8"}
            }, NewLayer);
             
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax  = "0.5 0.5", OffsetMin = "-500 -125", OffsetMax = "-450 100" },
                Button        = { Color     = "0 0 0 0", Material = "", Command = $"kitsshow " + page }, 
                Text          = { Text      = "<", Color          = "1 1 1 1", Align = TextAnchor.MiddleRight, Font = "robotocondensed-regular.ttf", FontSize = 80 }
            }, NewLayer);
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "450 -125", OffsetMax = "500 100" },
                Button        = { Color     = "0 0 0 0", Material  = "", Command          = $"kitsshow " + page }, 
                Text          = { Text      = ">", Color           = "1 1 1 0.2", Align     = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 80 }
            }, NewLayer);*/
            
            container.Add(new CuiButton 
            {
                RectTransform = { AnchorMin = "0.5 0.9", AnchorMax    = "0.5 0.9", OffsetMin   = "-200 -25", OffsetMax         = "200 25" },
                Button        = { Color     = "0 0 0 0", Material = "", Close          = Layer, Command                = $"kitsshow " + page },
                Text          = { Text      = kit.Name.ToUpper(), Color      = "1 1 1 0.6", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 32 }
            }, NewLayer);
            
            double xSwitch = 0;
            double ySwitch = 0;

            var list = kit.Items.ToList();
            
            for (int i = 0; i < list.Count; i++)
            {
                var item = list.ElementAtOrDefault(i); 
                UI_RecountPosition(ref xSwitch, ref ySwitch, i, list.Count); 
                    
                container.Add(new CuiButton 
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{xSwitch} {ySwitch - ItemSide}", OffsetMax = $"{xSwitch + ItemSide} {ySwitch}" },
                    Button        = { Color     = "1 1 1 0.2", Command   = $""},
                    Text          = { Text      = "" } 
                }, NewLayer, NewLayer + $".BlockPanel.{i}");

                container.Add(new CuiElement
                {
                    Parent = NewLayer + $".BlockPanel.{i}",
                    Components =
                    { 
                        new CuiRawImageComponent {Png            = (string) plugins.Find("ImageLibrary").Call("GetImage", item.ShortName, item.SkinID) },
                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax         = "1 1", OffsetMin            = "0 5", OffsetMax                        = "-5 0"},
                    Text          = {Text      = "x" + item.Amount, Align = TextAnchor.LowerRight, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "1 1 1 0.5"}
                }, NewLayer + $".BlockPanel.{i}");
            }
            
            /*container.Add(new CuiElement
            {
                Parent = NewLayer,
                Components = 
                {
                    new CuiRawImageComponent {Png            = (string) ImageLibrary.Call("GetImage", kit.Key + "I")},
                    new CuiRectTransformComponent {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-432 -243", OffsetMax = "432 243"}
                },
            });*/

            CuiHelper.AddUi(player, container);
        } 
        
        private static string Layer = "UI_NewKitsLayer";
        private void UI_DrawInterfaces(BasePlayer player, int page = 0, int pageperm = 0)
        {
            List<Kit> defaultKits = _kits.Where(p => p.Permission== "kits.default" && p.Hide == false).ToList();
            List<Kit> premiumKits = _kits.Where(p => p.Permission != "kits.default"  && p.Hide == false).ToList(); 

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.DestroyUi(player, NewLayer);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel()
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                Image         = {Color     = "0 0 0 0"}
            }, "Overlay", Layer);
 
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax    = "1 1", OffsetMax                                       = "0 0"},
                Button        = {Color     = "0 0 0 1", Material = "assets/content/ui/uibackgroundblur-notice.mat", Close = Layer, Command = "closemenu"},
                Text          = {Text      = ""}
            }, Layer);
			container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "1 1", AnchorMax   = "1 1", OffsetMin = "-50 -50", OffsetMax = "0 0"},
                Button        = {Color     = "0 0 0 0.7", Close = Layer},
                Text          = {Text      = "X", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.7", FontSize = 16}
            }, Layer);
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax    = "1 1", OffsetMax                                       = "0 0"},
                Button        = {Color     = "0 0 0 0.7", Close = Layer, Command = "closemenu"},
                Text          = {Text      = ""}
            }, Layer);
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax   = "1 1", OffsetMax = "0 0"},
                Button        = {Color     = "0 0 0 0.7", Close = Layer, Command   = "closemenu"},
                Text          = {Text      = ""}
            }, Layer);
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax   = "1 1", OffsetMax = "0 0"},
                Button        = {Color     = "0 0 0 0.7", Close = Layer, Command   = "closemenu"},
                Text          = {Text      = ""}
            }, Layer);
 

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0.875", AnchorMax         = "1 1", OffsetMax              = "0 0"},
                Text          = {Text      = $"БЕСПЛАТНЫЕ НАБОРЫ", Align = TextAnchor.LowerCenter, Font = "robotocondensed-regular.ttf", FontSize = 24, Color = "1 1 1 0.6" }
            }, Layer);
 
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0.525", AnchorMax         = "1 1", OffsetMax             = "0 0"},
                Text          = {Text      = $"ПЛАТНЫЕ НАБОРЫ", Align = TextAnchor.LowerCenter, Font = "robotocondensed-regular.ttf", FontSize = 24, Color = "1 1 1 0.6" }
            }, Layer);

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0.15", AnchorMax     = "1 1", OffsetMax             = "0 0"},
                Text          = {Text      = $"ДЛЯ ДОСТУПА К ПЛАТНЫМ НАБОРАМ КУПИТЕ ПРИВИЛЕГИЮ\n" +
                        $"<b>https://тест вайп у всех привы</b>", Align = TextAnchor.LowerCenter, Font = "robotocondensed-regular.ttf", FontSize = 18, Color = "1 1 1 0.4" }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                Button        = {Color     = "0 0 0 0", Close = Layer },
                Text          = {Text      = ""}
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.06 0.6", AnchorMax  = "0.11 0.855", OffsetMax = "0 0" },
                Button        = { Color     = "0 0 0 0", Material = "", Command = $"kit skipdef {page - 1} {pageperm}" }, 
                Text          = { Text      = "<", Color          = "1 1 1 1", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 50 }
            }, Layer);
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.85 0.6", AnchorMax = "0.9 0.855", OffsetMax = "0 0" },
                Button        = { Color     = "0 0 0 0", Material  = "", Command          = $"kit skipdef {page + 1} {pageperm}" }, 
                Text          = { Text      = ">", Color           = "1 1 1 1", Align     = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 50 }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.06 0.25", AnchorMax  = "0.11 0.5", OffsetMax = "0 0" },
                Button        = { Color     = "0 0 0 0", Material = "", Command = $"kit skipperm {page} {pageperm - 1}" }, 
                Text          = { Text      = "<", Color          = "1 1 1 1", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 50 }
            }, Layer);
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.85 0.25", AnchorMax = "0.9 0.5", OffsetMax = "0 0" },
                Button        = { Color     = "0 0 0 0", Material  = "", Command          = $"kit skipperm {page} {pageperm + 1}" }, 
                Text          = { Text      = ">", Color           = "1 1 1 1", Align     = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 50 }
            }, Layer);

            float leftPosition = defaultKits.Skip(page * 6).Take(6).Count() / 2f * -150 - (defaultKits.Count - 1) / 2f * 10;  
            foreach (var check in defaultKits.Skip(page * 6).Take(6))
            {
                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0.5 0.75", AnchorMax = "0.5 0.75", OffsetMin = $"{leftPosition} -75", OffsetMax = $"{leftPosition + 150} 75" },
                    Image         = {Color     = "1 1 1 0.1"}
                }, Layer, Layer + check.Name);

                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 -32.5", OffsetMax = "0 -2.5"},
                    Image         = {Color     = "1 1 1 0.15"}
                }, Layer + check.Name, check.Name + ".S");

                var res = CanGiveKit(player, check.Name);
                if (!res.IsNullOrEmpty())
                {
                    float cooldown = 0;
                    if (float.TryParse(res, out cooldown))
                    { 
                        container.Add(new CuiPanel
                        {
                            RectTransform = {AnchorMin = "0 0", AnchorMax = $"{Math.Max((float) cooldown / check.Cooldown, 0.1f)} 1", OffsetMin = "2 2", OffsetMax = "-2 -2"},
                            Image         = {Color     = "1 0.6 0 0.5" }
                        }, check.Name + ".S");
                    
                        container.Add(new CuiLabel
                        {
                            RectTransform = {AnchorMin = "0 0", AnchorMax = $"1 1", OffsetMin = "2 2", OffsetMax = "-2 -2" },
                            Text = { Text = TimeSpan.FromSeconds(cooldown).ToShortString(), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "0 0 0 0.8", FontSize = 16 }
                        }, check.Name + ".S");
                    }
                    else 
                    { 
                        container.Add(new CuiButton
                        {
                            RectTransform = {AnchorMin = "0 0", AnchorMax = "0.8 1", OffsetMin = "2 2", OffsetMax = "-2 -2"},
                            Button        = {Color     = "1 0.4 0.4 0.8" },
                            Text          = { Text     = res, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.5", FontSize = 16 }
                        }, check.Name + ".S");
                        
                        container.Add(new CuiButton
                        {
                            RectTransform = {AnchorMin = "0.8 0", AnchorMax = "1 1", OffsetMin = "2 2", OffsetMax = "-2 -2"},
                            Button        = {Color     = "1 1 1 0.2", Command = $"kit.info {_kits.IndexOf(check)}" },
                            Text          = { Text     = "?", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.7", FontSize = 16 }
                        }, check.Name + ".S");
                    }
                }
                else
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "0.8 1", OffsetMin = "2 2", OffsetMax = "-2 -2"},
                        Button        = {Color     = "0.5 0.9 0.5 0.5", Close = Layer, Command = $"kit {check.Name}"},
                            Text          = { Text     = "ПОЛУЧИТЬ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "0 0 0 0.8", FontSize = 16 }
                    }, check.Name + ".S");

                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = "0.8 0", AnchorMax   = "1 1", OffsetMin = "2 2", OffsetMax = "-2 -2"},
                        Button        = {Color     = "1 1 1 0.2", Command = $"kit.info {_kits.IndexOf(check)}" },
                        Text          = { Text     = "?", Align           = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.7", FontSize = 16 }
                    }, check.Name + ".S");
                }
 
                container.Add(new CuiElement
                {
                    Parent = Layer + check.Name,
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) plugins.Find("ImageLibrary").Call("GetImage", check.Name) },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
                    }
                });

                leftPosition += 155;
            }
 
            leftPosition = premiumKits.Skip(pageperm * 6).Take(6).Count() / 2f * -150 - (premiumKits.Count - 1) / 2f * 10;   
            foreach (var check in premiumKits.Skip(pageperm * 6).Take(6)) 
            {
                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0.5 0.4", AnchorMax = "0.5 0.4", OffsetMin = $"{leftPosition} -75", OffsetMax = $"{leftPosition + 150} 75" },
                    Image         = {Color     = "1 1 1 0.1"}
                }, Layer, Layer + check.Name);

                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 -32.5", OffsetMax = "0 -2.5"},
                    Image         = {Color     = "1 1 1 0.15"}
                }, Layer + check.Name, check.Name + ".S");

                var res = CanGiveKit(player, check.Name);
                if (!res.IsNullOrEmpty())
                {
                    float cooldown = 0;
                    if (float.TryParse(res, out cooldown))
                    { 
                        container.Add(new CuiPanel
                        {
                            RectTransform = {AnchorMin = "0 0", AnchorMax = $"{Math.Max((float) cooldown / check.Cooldown, 0.1f)} 1", OffsetMin = "2 2", OffsetMax = "-2 -2"},
                            Image         = {Color     = "1 0.4 0.4 0.8" }
                        }, check.Name + ".S");
                        
                        container.Add(new CuiLabel
                        {
                            RectTransform = {AnchorMin = "0 0", AnchorMax = $"1 1", OffsetMin = "2 2", OffsetMax = "-2 -2" },
                            Text = { Text = TimeSpan.FromSeconds(cooldown).ToShortString(), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.5", FontSize = 16 }
                        }, check.Name + ".S"); 
                    }
                    else
                    {
                        container.Add(new CuiButton
                        {
                            RectTransform = {AnchorMin = "0 0", AnchorMax = "0.8 1", OffsetMin = "2 2", OffsetMax = "-2 -2"},
                            Button        = {Color     = "1 0.4 0.4 0.8" },
                            Text          = { Text     = res, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.5", FontSize = 16 }
                        }, check.Name + ".S");
                        
                        container.Add(new CuiButton
                        {
                            RectTransform = {AnchorMin = "0.8 0", AnchorMax   = "1 1", OffsetMin = "2 2", OffsetMax = "-2 -2"},
                            Button        = {Color     = "1 1 1 0.2", Command = $"kit.info {_kits.IndexOf(check)}" },
                            Text          = { Text     = "?", Align           = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.7", FontSize = 16 }
                        }, check.Name + ".S");
                    }
                } 
                else
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "0.8 1", OffsetMin = "2 2", OffsetMax = "-2 -2"},
                        Button        = {Color     = "0.5 1 0.5 0.5", Command = $"kit {check.Name}"},
                        Text          = { Text     = "ПОЛУЧИТЬ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.4", FontSize = 16 }
                    }, check.Name + ".S");
                     
                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = "0.8 0", AnchorMax   = "1 1", OffsetMin = "2 2", OffsetMax = "-2 -2"},
                        Button        = {Color     = "1 1 1 0.2", Command = $"kit.info {_kits.IndexOf(check)}" },
                        Text          = { Text     = "?", Align           = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.7", FontSize = 16 }
                    }, check.Name + ".S");
                }
 
                container.Add(new CuiElement
                {
                    Parent = Layer + check.Name,
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) plugins.Find("ImageLibrary").Call("GetImage", check.Name) },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
                    }
                });
  
                leftPosition += 155; 
            } 

            CuiHelper.AddUi(player, container);
        }
        
        
        private PluginConfig _config;
        private ImagesCache _imagesCache = new ImagesCache();
        private List<Kit> _kits;
        private Dictionary<ulong, Dictionary<string, KitData>> _kitsData;
        private Dictionary<BasePlayer, List<string>> _kitsGUI = new Dictionary<BasePlayer, List<string>>();

        #region Classes

        class PluginConfig
        {
            public Position Position { get; set; }
            public Position CloseButtonPosition { get; set; }
            public Position CloseButtonPositionNext1 { get; set; }
            public Position CloseButtonPositionNext2 { get; set; }
            public Position CloseButtonPositionNext3 { get; set; }
            public string CloseButtonColor { get; set; }
            public string MainBackgroundColor { get; set; }

            public string DefaultKitImage { get; set; }
            public float MarginTop { get; set; }
            public float MarginBottom { get; set; }
            public float MarginBetween { get; set; }
            public float KitWidth { get; set; }
            public string DisableMaskColor { get; set; }
            public string KitBackgroundColor { get; set; }
            public List<string> CustomAutoKits;
            public ImageConfig Image { get; set; }
            public LabelConfig Label { get; set; }
            public LabelConfig Amount { get; set; }
            public LabelConfig Time { get; set; }

            public static PluginConfig CreateDefault()
            {
                return new PluginConfig
                {
                    CustomAutoKits = new List<string>()
                    {
                        "autokit1",
                        "autokit2"
                    },
                    MainBackgroundColor = "#00000000",
                    Position = new Position
                    {
                        AnchorMin = "0 0.35",
                        AnchorMax = "1 0.65"
                    },
                    CloseButtonPosition = new Position
                    {
                        AnchorMin = "0.95 0.8",
                        AnchorMax = "0.997 0.98"
                    },
                    CloseButtonPositionNext1 = new Position
                    {
                        AnchorMin = "0.95 0.6",
                        AnchorMax = "0.997 0.78"
                    },
                    CloseButtonPositionNext2 = new Position
                    {
                        AnchorMin = "0.95 0.4",
                        AnchorMax = "0.997 0.58"
                    },
                    CloseButtonPositionNext3 = new Position
                    {
                        AnchorMin = "0.95 0.1",
                        AnchorMax = "0.997 0.38"
                    },
                    CloseButtonColor = "0 0 0 0.70",

                    DefaultKitImage = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "Kits" + Path.DirectorySeparatorChar + "home.png",
                    KitWidth = 0.12f,
                    MarginTop = 0.04f,
                    MarginBottom = 0.03f,
                    DisableMaskColor = "#000000DD",
                    MarginBetween = 0.01f,
                    KitBackgroundColor = "#00000000",

                    Image = new ImageConfig
                    {
                        Color = "#FFFFFFFF",
                        Position = new Position
                        {
                            AnchorMin = "0.05 0.15",
                            AnchorMax = "0.95 0.95"
                        },
                        Png = ""
                    },
                    Label = new LabelConfig
                    {
                        Position = new Position
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 0.15"
                        },
                        FontSize = 14,
                        ForegroundColor = "#FFFFFFFF",
                        BackgroundColor = "#00000000",
                        TextAnchor = TextAnchor.MiddleCenter
                    },
                    Amount = new LabelConfig
                    {
                        Position = new Position
                        {
                            AnchorMin = "0.05 0.85",
                            AnchorMax = "0.95 0.95"
                        },
                        FontSize = 14,
                        ForegroundColor = "#FFFFFFFF",
                        BackgroundColor = "#00000000",
                        TextAnchor = TextAnchor.MiddleCenter
                    },
                    Time = new LabelConfig
                    {
                        Position = new Position
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        },
                        FontSize = 14,
                        ForegroundColor = "#FFFFFFFF",
                        BackgroundColor = "#00000000",
                        TextAnchor = TextAnchor.MiddleCenter
                    }
                };
            }

            public class LabelConfig
            {
                public Position Position { get; set; }
                public string ForegroundColor { get; set; }
                public string BackgroundColor { get; set; }
                public int FontSize { get; set; }
                public TextAnchor TextAnchor { get; set; }
            }

            public class ImageConfig
            {
                public Position Position { get; set; }
                public string Color { get; set; }
                public string Png { get; set; }
            }
        }

        public class Kit
        {
            public string Name { get; set; }
            public string DisplayName { get; set; }
            public int Amount { get; set; }
            public double Cooldown { get; set; }
            public bool Hide { get; set; }
            public string Inside { get; set; }
            public string Permission { get; set; }
            public List<KitItem> Items { get; set; }

            public string Png { get; set; }
        }

        public class KitItem
        {
            public string ShortName { get; set; }
            public int Amount { get; set; }
            public int Blueprint { get; set; }
            public ulong SkinID { get; set; }
            public string Container { get; set; }
            public float Condition { get; set; }
            public Weapon Weapon { get; set; }
            public List<ItemContent> Content { get; set; }

        }
        public class Weapon
        {
            public string ammoType { get; set; }
            public int ammoAmount { get; set; }
        }
        public class ItemContent
        {
            public string ShortName { get; set; }
            public float Condition { get; set; }
            public int Amount { get; set; }
        }

        public class KitData
        {
            public int Amount { get; set; }
            public double Cooldown { get; set; }
        }

        public class Position
        {
            public string AnchorMin { get; set; }
            public string AnchorMax { get; set; }
        }

        public class ImagesCache : MonoBehaviour
        {
            private Dictionary<string, string> _images = new Dictionary<string, string>();

            public void Add(string name, string url)
            {
                if (_images.ContainsKey(name))
                    return;

                using (var www = new WWW(url))
                {
                    if (www.error != null)
                    {
                        print(string.Format("Image loading fail! Error: {0}", www.error));
                    }
                    else
                    {
                        if (www.bytes == null || www.bytes.Count() == 0)
                        {
                            Interface.Oxide.LogError($"Failed to add image for {name}. File address possibly invalide\n {url}");
                            return;
                        }

                        _images[name] = FileStorage.server.Store(www.bytes, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
                    }
                }
            }
            public string Get(string name)
            {
                if (_images.ContainsKey(name))
                    return _images[name];

                return string.Empty;
            }
        }

        #endregion

        #region Oxide hooks

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            Config.WriteObject(PluginConfig.CreateDefault(), true);
            PrintWarning("Благодарим за приобритение плагина на сайте TopPlugin.ru. Если вы приобрели этот плагин на другом ресурсе знайте - это лишает вас гарантированных обновлений!");
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            foreach (var kits in _config.CustomAutoKits)
            {
                if (_kits.Exists(x => x.Name == kits))
                {
                    var kit1 = _kits.First(x => x.Name == kits);
                    if (permission.UserHasPermission(player.UserIDString, kit1.Permission))
                    {
                        player.inventory.Strip();
                        GiveItems(player, kit1);
                        return;
                    }
                }
            }
            if (_kits.Exists(x => x.Name == "autokit"))
            {
                player.inventory.Strip();
                var kit = _kits.First(x => x.Name == "autokit");
                GiveItems(player, kit);
            }

        }
        private void SaveKits()
        {
            Interface.Oxide.DataFileSystem.WriteObject("Kits", _kits);
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("Kits_Data", _kitsData);
        }

        void OnServerSave()
        {
            SaveData();
        }


        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Kit Was Removed"] = "<color=#008B8B>[Сервер]:</color> Kit {kitname} was removed",
                ["Kit Doesn't Exist"] = "<color=#008B8B>[Сервер]:</color> This kit doesn't exist",
                ["Not Found Player"] = "<color=#008B8B>[Сервер]:</color> Player not found",
                ["To Many Player"] = "<color=#008B8B>[Сервер]:</color> Found multipy players",
                ["Permission Denied"] = "<color=#008B8B>[Сервер]:</color> Access denied",
                ["Limite Denied"] = "<color=#008B8B>[Сервер]:</color> Useage limite reached",
                ["Cooldown Denied"] = "<color=#008B8B>[Сервер]:</color> You will be able to use this kit after {time}",
                ["Reset"] = "<color=#008B8B>[Сервер]:</color> Kits data wiped",
                ["Kit Already Exist"] = "<color=#008B8B>[Сервер]:</color> Kit with the same name already exist",
                ["Kit Created"] = "<color=#008B8B>[Сервер]:</color> You have created a new kit - {name}",
                ["Kit Extradited"] = "<color=#008B8B>[Сервер]:</color> You have claimed kit - {kitname}",
                ["Kit Cloned"] = "<color=#008B8B>[Сервер]:</color> You inventory was copyed to the kit",
                ["UI Amount"] = "Timeleft: {amount}",
                ["Help"] = "/kit name|add|clone|remove|list|reset",
                ["Help Add"] = "/kit add <kitname>",
                ["Help Clone"] = "/kit clone <kitname>",
                ["Help Remove"] = "/kit remove <kitname>",
                ["Help Give"] = "/kit give <kitname> <playerName|steamID>",
                ["Give Succes"] = "You have successfully given the player {0} a set {1}",
                ["No Space"] = "Can't redeem kit. Not enought space"
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Kit Was Removed"] = "<color=#008B8B>[Сервер]:</color> {kitname} был удалён",
                ["Kit Doesn't Exist"] = "<color=#008B8B>[Сервер]:</color> Этого комплекта не существует",
                ["Not Found Player"] = "<color=#008B8B>[Сервер]:</color> Игрок не найден",
                ["To Many Player"] = "<color=#008B8B>[Сервер]:</color> Найдено несколько игроков",
                ["Permission Denied"] = "<color=#008B8B>[Сервер]:</color> У вас нет полномочий использовать этот комплект",
                ["Limite Denied"] = "<color=#008B8B>[Сервер]:</color> Вы уже использовали этот комплект максимальное количество раз",
                ["Cooldown Denied"] = "<color=#008B8B>[Сервер]:</color> Вы сможете использовать этот комплект через {time}",
                ["Reset"] = "<color=#008B8B>[Сервер]:</color> Вы обнулили все данные о использовании комплектов игроков",
                ["Kit Already Exist"] = "<color=#008B8B>[Сервер]:</color> Этот набор уже существует",
                ["Kit Created"] = "<color=#008B8B>[Сервер]:</color> Вы создали новый набор - {name}",
                ["Kit Extradited"] = "<color=#008B8B>[Сервер]:</color> Вы получили комплект {kitname}",
                ["Kit Cloned"] = "<color=#008B8B>[Сервер]:</color> Предметы были скопированы из инвентаря в набор",
                ["UI Amount"] = "Осталось: {amount}",
                ["Help"] = "/kit name|add|clone|remove|list|reset",
                ["Help Add"] = "/kit add <kitname>",
                ["Help Clone"] = "/kit clone <kitname>",
                ["Help Remove"] = "/kit remove <kitname>",
                ["Help Give"] = "/kit give <kitname> <playerName|steamID>",
                ["Give Succes"] = "Вы успешно выдали игрок {0} набор {1}",
                ["No Space"] = "Невозможно получить кит,недостаточно места в инвентаре и слотах одежды"
            }, this, "ru");
        }

        private void Loaded()
        {
            _config = Config.ReadObject<PluginConfig>();
            _kits = Interface.Oxide.DataFileSystem.ReadObject<List<Kit>>("Kits");
            _kitsData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string, KitData>>>("Kits_Data");

            LoadImages();

            LoadMessages();
        }

        [PluginReference] Plugin ImageLibrary;

        private void LoadImages()
        {
            _kits.SelectMany(p => p.Items).Where(p => p.SkinID != 0).ToList().ForEach(p =>
            {
                ImageLibrary.Call("AddImage", $"https://i.imgur.com/0nIHe6Y.png", p.ShortName, p.SkinID);
            });
            _kits.Where(p => !p.Inside.IsNullOrEmpty()).ToList().ForEach(p =>
            {
                ImageLibrary.Call("AddImage", p.Inside, p.Name + ".I");
            });
            _kits.Where(p => !p.Png.IsNullOrEmpty()).ToList().ForEach(p => {

                ImageLibrary.Call("AddImage", p.Png, p.Name);
            });
        }

        private void Unload()
        {
            SaveData();

            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
            }
        } 

        private void OnServerInitialized()
        {
            foreach (var kit in _kits)
            {
                _imagesCache.Add(kit.Name, Interface.Oxide.DataDirectory
                    + Path.DirectorySeparatorChar + "Kits" + Path.DirectorySeparatorChar + kit.Png);
                 
                if (!permission.PermissionExists(kit.Permission))
                    permission.RegisterPermission(kit.Permission, this);
            } 

            timer.Repeat(1, 0, RefreshCooldownKitsUI);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            _kitsGUI.Remove(player);
        }

        #endregion

        #region Commands

        [ConsoleCommand("kit")]
        private void CommandConsoleKit(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
                return;

            var player = arg.Player();

            if (!arg.HasArgs())
                return;

            var value = arg.Args[0].ToLower();

            if (value == "ui")
            {
                TriggerUI(player);
                return;
            }

            if (value == "next1")
            {
                TriggerUI(player);
                TriggerUI1(player);
                return;
            }

            if (value == "next2")
            {
                TriggerUI(player);
                TriggerUI2(player);
                return;
            }

            if (value == "next3")
            {
                TriggerUI(player);
                TriggerUI3(player);
                return;
            }

            if (value == "skipdef")
            {
                UI_DrawInterfaces(player, int.Parse(arg.Args[1]), int.Parse(arg.Args[2]));
                return;
            }

            if (value == "skipperm")
            {
                UI_DrawInterfaces(player, int.Parse(arg.Args[1]), int.Parse(arg.Args[2]));
                return;
            }


            GiveKit(player, value);
            //UI_DrawInterfaces(player); 

            return;
        }

        [ChatCommand("kit")]
        private void CommandChatKit(BasePlayer player, string command, string[] args)
        {
            if (player == null)
                return;

            if (args.Length == 0)
            {
                UI_DrawInterfaces(player);
                return;
            }

            if (!player.IsAdmin)
            {
                GiveKit(player, args[0].ToLower());
                return;
            }

            switch (args[0].ToLower())
            {
                case "help":
                    SendReply(player, GetMsg("Help", player));
                    return;
                case "add":
                    if (args.Length < 2)
                        SendReply(player, GetMsg("Help Add", player));
                    else
                        KitCommandAdd(player, args[1].ToLower());
                    return;
                case "clone":
                    if (args.Length < 2)
                        SendReply(player, GetMsg("Help Clone", player));
                    else
                        KitCommandClone(player, args[1].ToLower());
                    return;
                case "remove":
                    if (args.Length < 2)
                        SendReply(player, GetMsg("Help Remove", player));
                    else
                        KitCommandRemove(player, args[1].ToLower());
                    return;
                case "list":
                    KitCommandList(player);
                    return;
                case "reset":
                    KitCommandReset(player);
                    return;
                case "give":
                    if (args.Length < 3)
                    {
                        SendReply(player, GetMsg("Help Give", player));
                    }
                    else
                    {
                        var foundPlayer = FindPlayer(player, args[1].ToLower());
                        if (foundPlayer == null)
                            return;
                        SendReply(player, GetMsg("Give Succes", player), foundPlayer.displayName, args[2]);
                        KitCommandGive(player, foundPlayer, args[2].ToLower());
                    }
                    return;
                default:
                    GiveKit(player, args[0].ToLower());
                    return;
            }
        }

        #endregion

        #region Kits
		
		
        private bool GiveKitEvent(BasePlayer player, string kitName)
        {
            if (string.IsNullOrEmpty(kitName))
                return false; 
			
			PrintError(kitName); 
            
            if (!_kits.Exists(x => x.Name == kitName))
            {
                SendReply(player, GetMsg("Kit Doesn't Exist", player));
                return false;
            }
            
            var kit = _kits.First(x => x.Name == kitName);
  
            GiveItems(player, kit);
            
            return true;
        } 
        
        private string CanGiveKit(BasePlayer player, string kitname)
        {
            if (string.IsNullOrEmpty(kitname)) 
                return "ERROR";

            if (Interface.Oxide.CallHook("canRedeemKit", player) != null)
            {
                return "ERROR";
            }
            if (!_kits.Exists(x => x.Name == kitname))
            {
                return "ERROR";
            }

            var kit = _kits.First(x => x.Name == kitname);

            if (!string.IsNullOrEmpty(kit.Permission) && !permission.UserHasPermission(player.UserIDString, kit.Permission))
            {
                return "НЕТ ПРАВ";
            }

            var playerData = GetPlayerData(player.userID, kitname);

            if (kit.Amount > 0 && playerData.Amount >= kit.Amount)
            {
                return "ЗАКОНЧИЛОСЬ";
            }

            if (kit.Cooldown > 0)
            {
                var currentTime = GetCurrentTime();
                if (playerData.Cooldown > currentTime)
                {
                    return (playerData.Cooldown - currentTime).ToString();
                }
            }

            //foreach (var item in kit.Items)
            //    player.GiveItem(ItemManager.CreateByName(item.ShortName, item.Amount, item.Skin));
            int beltcount = kit.Items.Where(i => i.Container == "belt").Count();
            int wearcount = kit.Items.Where(i => i.Container == "wear").Count();
            int maincount = kit.Items.Where(i => i.Container == "main").Count();
            int totalcount = beltcount + wearcount + maincount;
            if ((player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count) < beltcount || (player.inventory.containerWear.capacity - player.inventory.containerWear.itemList.Count) < wearcount || (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count) < maincount)
                if (totalcount > (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count))
                {
                    return "НЕТ МЕСТА";
                } 

            return "";
        }

        private bool GiveKit(BasePlayer player, string kitname)
        {
            if (string.IsNullOrEmpty(kitname))
                return false;

            if (Interface.Oxide.CallHook("canRedeemKit", player) != null)
            {
                return false;
            }
            if (!_kits.Exists(x => x.Name == kitname))
            {
                SendReply(player, GetMsg("Kit Doesn't Exist", player));
                return false;
            }

            var kit = _kits.First(x => x.Name == kitname);

            if (!string.IsNullOrEmpty(kit.Permission) && !permission.UserHasPermission(player.UserIDString, kit.Permission))
            {
                SendReply(player, GetMsg("Permission Denied", player));
                return false;
            }

            var playerData = GetPlayerData(player.userID, kitname);

            if (kit.Amount > 0 && playerData.Amount >= kit.Amount)
            {
                SendReply(player, GetMsg("Limite Denied", player));
                return false; 
            }

            if (kit.Cooldown > 0)
            {
                var currentTime = GetCurrentTime();
                if (playerData.Cooldown > currentTime)
                {
                    SendReply(player, GetMsg("Cooldown Denied", player).Replace("{time}", TimeExtensions.FormatTime(TimeSpan.FromSeconds(playerData.Cooldown - currentTime))));
                    return false;
                }
            }

            //foreach (var item in kit.Items)
            //    player.GiveItem(ItemManager.CreateByName(item.ShortName, item.Amount, item.Skin));
            int beltcount = kit.Items.Where(i => i.Container == "belt").Count();
            int wearcount = kit.Items.Where(i => i.Container == "wear").Count();
            int maincount = kit.Items.Where(i => i.Container == "main").Count();
            int totalcount = beltcount + wearcount + maincount;
            if ((player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count) < beltcount || (player.inventory.containerWear.capacity - player.inventory.containerWear.itemList.Count) < wearcount || (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count) < maincount)
                if (totalcount > (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count))
                {
                    player.ChatMessage(GetMsg("No Space", player));
                    return false;
                }
            GiveItems(player, kit);

            if (kit.Amount > 0)
                playerData.Amount += 1;

            if (kit.Cooldown > 0)
                playerData.Cooldown = GetCurrentTime() + kit.Cooldown;

            SendReply(player, GetMsg("Kit Extradited", player).Replace("{kitname}", kit.DisplayName));
            return true;
        }

        private void KitCommandAdd(BasePlayer player, string kitname)
        {
            if (_kits.Exists(x => x.Name == kitname))
            {
                SendReply(player, GetMsg("Kit Already Exist", player));
                return;
            }

            _kits.Add(new Kit
            {
                Name = kitname,
                DisplayName = kitname,
                Cooldown = 600,
                Hide = true,
                Permission = "kits.default",
                Amount = 0,
                Png = _config.DefaultKitImage,
                Items = GetPlayerItems(player)
            });
            permission.RegisterPermission($"kits.default", this);
            SendReply(player, GetMsg("Kit Created", player).Replace("{name}", kitname));

            SaveKits();
            SaveData();
        }

        private void KitCommandClone(BasePlayer player, string kitname)
        {
            if (!_kits.Exists(x => x.Name == kitname))
            {
                SendReply(player, GetMsg("Kit Doesn't Exist", player));
                return;
            }

            _kits.First(x => x.Name == kitname).Items = GetPlayerItems(player);

            SendReply(player, GetMsg("Kit Cloned", player).Replace("{name}", kitname));

            SaveKits();
        }

        private void KitCommandRemove(BasePlayer player, string kitname)
        {
            if (_kits.RemoveAll(x => x.Name == kitname) <= 0)
            {
                SendReply(player, GetMsg("Kit Doesn't Exist", player));
                return;
            }

            SendReply(player, GetMsg("Kit Was Removed", player).Replace("{kitname}", kitname));

            SaveKits();
        }

        private void KitCommandList(BasePlayer player)
        {
            foreach (var kit in _kits)
                SendReply(player, $"{kit.Name} - {kit.DisplayName}");
        }

        private void KitCommandReset(BasePlayer player)
        {
            _kitsData.Clear();

            SendReply(player, GetMsg("Reset", player));
        }

        private void KitCommandGive(BasePlayer player, BasePlayer foundPlayer, string kitname)
        {
            var reply = 1;
            if (reply == 0) { }
            if (!_kits.Exists(x => x.Name == reply.ToString())) { }

            if (!_kits.Exists(x => x.Name == kitname))
            {
                SendReply(player, GetMsg("Kit Doesn't Exist", player));
                return;
            }

            GiveItems(foundPlayer, _kits.First(x => x.Name == kitname));
        }
        private void GiveItems(BasePlayer player, Kit kit)
        {
            foreach (var kitem in kit.Items)
            {
                GiveItem(player,
                    BuildItem(kitem.ShortName, kitem.Amount, kitem.SkinID, kitem.Condition, kitem.Blueprint,
                        kitem.Weapon, kitem.Content),
                    kitem.Container == "belt" ? player.inventory.containerBelt :
                    kitem.Container == "wear" ? player.inventory.containerWear : player.inventory.containerMain);
            }
        }
        private void GiveItem(BasePlayer player, Item item, ItemContainer cont = null)
        {
            if (item == null) return;
            var inv = player.inventory;

            var moved = item.MoveToContainer(cont) || item.MoveToContainer(inv.containerMain);
            if (!moved)
            {
                if (cont == inv.containerBelt)
                    moved = item.MoveToContainer(inv.containerWear);
                if (cont == inv.containerWear)
                    moved = item.MoveToContainer(inv.containerBelt);
            }

            if (!moved)
                item.Drop(player.GetCenter(),player.GetDropVelocity());
        }
        private Item BuildItem(string ShortName, int Amount, ulong SkinID, float Condition, int blueprintTarget, Weapon weapon, List<ItemContent> Content)
        {
            Item item = ItemManager.CreateByName(ShortName, Amount > 1 ? Amount : 1, SkinID);
            item.condition = Condition;

            if (blueprintTarget != 0)
                item.blueprintTarget = blueprintTarget;

            if (weapon != null)
            {
                (item.GetHeldEntity() as BaseProjectile).primaryMagazine.contents = weapon.ammoAmount;
                (item.GetHeldEntity() as BaseProjectile).primaryMagazine.ammoType = ItemManager.FindItemDefinition(weapon.ammoType);
            }
            if (Content != null)
            {
                foreach (var cont in Content)
                {
                    Item new_cont = ItemManager.CreateByName(cont.ShortName, cont.Amount);
                    new_cont.condition = cont.Condition;
                    new_cont.MoveToContainer(item.contents);
                }
            }
            return item;
        }
        #endregion

        #region UI

        private void TriggerUI(BasePlayer player)
        {
            if (_kitsGUI.ContainsKey(player))
                DestroyUI(player);
            else
                InitilizeUI(player);
        }

        private void TriggerUI2(BasePlayer player)
        {
            InitilizeUI2(player);
        }

        private void TriggerUI3(BasePlayer player)
        {
            InitilizeUI3(player);
        }

        private void TriggerUI1(BasePlayer player)
        {
            InitilizeUI(player);
        }

        private void InitilizeUI(BasePlayer player)
        {
            var kits = GetKitsForPlayer(player).ToList();
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = _config.Position.AnchorMin, AnchorMax = _config.Position.AnchorMax },
                CursorEnabled = true
            }, name: "ui.kits");
            container.Add(new CuiButton
            {
                Button = { Command = "kit ui", Color = _config.CloseButtonColor },
                RectTransform = { AnchorMin = _config.CloseButtonPosition.AnchorMin, AnchorMax = _config.CloseButtonPosition.AnchorMax },
                Text = { Text = "", FontSize = 20, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter }
            }, "ui.kits");
            foreach (var kit in kits)
            {
                if (kits.Count > 7)
                {
                    container.Add(new CuiButton
                    {
                        Button = { Command = "kit next2", Color = "0 0 0 0.15" },
                        RectTransform = { AnchorMin = "0.965 0.3", AnchorMax = "0.997 0.7" },
                        Text = { Text = ">", FontSize = 20, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter }
                    }, "ui.kits");
                }
            }

            InitilizeKitsUI(ref container, player);

            CuiHelper.AddUi(player, container);
        }
        private void InitilizeUI2(BasePlayer player)
        {
            var kits = GetKitsForPlayer(player).ToList();
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = HexToRustFormat(_config.MainBackgroundColor) },
                RectTransform = { AnchorMin = _config.Position.AnchorMin, AnchorMax = _config.Position.AnchorMax },
                CursorEnabled = true
            }, name: "ui.kits");
            container.Add(new CuiButton
            {
                Button = { Command = "kit ui", Color = _config.CloseButtonColor },
                RectTransform = { AnchorMin = _config.CloseButtonPosition.AnchorMin, AnchorMax = _config.CloseButtonPosition.AnchorMax },
                Text = { Text = "", FontSize = 20, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter }
            }, "ui.kits");

            foreach (var kit in kits)
            {
                if (kits.Count > 14)
                {
                    container.Add(new CuiButton
                    {
                        Button = { Command = "kit next1", Color = "0 0 0 0.15" },
                        RectTransform = { AnchorMin = "0.002 0.3", AnchorMax = "0.035 0.7" },
                        Text = { Text = "<", FontSize = 20, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter }
                    }, "ui.kits");

                    container.Add(new CuiButton
                    {
                        Button = { Command = "kit next3", Color = "0 0 0 0.15" },
                        RectTransform = { AnchorMin = "0.965 0.3", AnchorMax = "0.997 0.7" },
                        Text = { Text = ">", FontSize = 20, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter }
                    }, "ui.kits");
                }
                else if (kits.Count > 7)
                {
                    container.Add(new CuiButton
                    {
                        Button = { Command = "kit next1", Color = "0 0 0 0.15" },
                        RectTransform = { AnchorMin = "0.002 0.3", AnchorMax = "0.035 0.7" },
                        Text = { Text = "<", FontSize = 20, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter }
                    }, "ui.kits");

                }
            }


            if (kits.Count < 7)
            {
                InitilizeKitsUI(ref container, player);
            }
            else
            {
                InitilizeKitsUINext(ref container, player);
            }

            CuiHelper.AddUi(player, container);
        }

        private void InitilizeUI3(BasePlayer player)
        {
            var kits = GetKitsForPlayer(player).ToList();
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = HexToRustFormat(_config.MainBackgroundColor) },
                RectTransform = { AnchorMin = _config.Position.AnchorMin, AnchorMax = _config.Position.AnchorMax },
                CursorEnabled = true
            }, name: "ui.kits");
            container.Add(new CuiButton
            {
                Button = { Command = "kit ui", Color = _config.CloseButtonColor },
                RectTransform = { AnchorMin = _config.CloseButtonPosition.AnchorMin, AnchorMax = _config.CloseButtonPosition.AnchorMax },
                Text = { Text = "", FontSize = 20, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter }
            }, "ui.kits");

            foreach (var kit in kits)
            {
                if (kits.Count > 14)
                {
                    container.Add(new CuiButton
                    {
                        Button = { Command = "kit next2", Color = "0 0 0 0.15" },
                        RectTransform = { AnchorMin = "0.002 0.3", AnchorMax = "0.035 0.7" },
                        Text = { Text = "<", FontSize = 20, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter }
                    }, "ui.kits");


                }
                else if (kits.Count > 7)
                {
                    container.Add(new CuiButton
                    {
                        Button = { Command = "kit next1", Color = _config.CloseButtonColor },
                        RectTransform = { AnchorMin = _config.CloseButtonPositionNext1.AnchorMin, AnchorMax = _config.CloseButtonPositionNext1.AnchorMax },
                        Text = { Text = "1", FontSize = 20, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter }
                    }, "ui.kits");
                    container.Add(new CuiButton
                    {
                        Button = { Command = "kit next2", Color = _config.CloseButtonColor },
                        RectTransform = { AnchorMin = _config.CloseButtonPositionNext2.AnchorMin, AnchorMax = _config.CloseButtonPositionNext2.AnchorMax },
                        Text = { Text = "2", FontSize = 20, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter }
                    }, "ui.kits");
                }
            }


            if (kits.Count < 14)
            {
                InitilizeKitsUINext(ref container, player);
            }
            else if (kits.Count < 7)
            {
                InitilizeKitsUI(ref container, player);
            }
            else
            {
                InitilizeKitsUI2(ref container, player);
            }

            CuiHelper.AddUi(player, container);
        }

        private void DestroyUI(BasePlayer player)
        {
            if (!_kitsGUI.ContainsKey(player))
                return;

            foreach (var kitname in _kitsGUI[player])
            {
                CuiHelper.DestroyUi(player, $"ui.kits.{kitname}.time");
                CuiHelper.DestroyUi(player, $"ui.kits.{kitname}.mask");
                CuiHelper.DestroyUi(player, $"ui.kits.{kitname}.button");
                CuiHelper.DestroyUi(player, $"ui.kits.{kitname}.amount");
                CuiHelper.DestroyUi(player, $"ui.kits.{kitname}");
            }
            CuiHelper.DestroyUi(player, "ui.kits");

            _kitsGUI.Remove(player);
        }

        private void RefreshCooldownKitsUI()
        {
            var currentTime = GetCurrentTime();
            foreach (var playerGUIData in _kitsGUI)
            {
                var container = new CuiElementContainer();
                if (!_kitsData.ContainsKey(playerGUIData.Key.userID)) continue;
                var playerKitsData = _kitsData[playerGUIData.Key.userID];
                foreach (var kitname in playerGUIData.Value)
                {
                    var playerKitData = playerKitsData[kitname];
                    if (playerKitData.Cooldown > 0)
                    {
                        CuiHelper.DestroyUi(playerGUIData.Key, $"ui.kits.{kitname}.time");
                        if (playerKitData.Cooldown < currentTime)
                        {
                            CuiHelper.DestroyUi(playerGUIData.Key, $"ui.kits.{kitname}.mask");
                            InitilizeButtonUI(ref container, kitname);
                        }
                        else
                        {
                            InitilizeCooldownLabelUI(ref container, kitname, TimeSpan.FromSeconds(playerKitData.Cooldown - currentTime));
                        }
                    }
                }
                CuiHelper.AddUi(playerGUIData.Key, container);
            }
        }

        private void InitilizeKitsUI(ref CuiElementContainer container, BasePlayer player)
        {
            _kitsGUI[player] = new List<string>();
            var currentTime = GetCurrentTime();
            var kits = GetKitsForPlayer(player).Take((int)(1.0f / (_config.KitWidth + _config.MarginBetween))).ToList();
            var pos = 0.490f - (kits.Count * _config.KitWidth + (kits.Count - 1) * _config.MarginBetween) / 2;

            foreach (var kit in kits)
            {
                _kitsGUI[player].Add(kit.Name);

                var playerData = GetPlayerData(player.userID, kit.Name);

                // Kit panel
                container.Add(new CuiPanel
                {
                    Image = { Color = "0 0 0 0" },
                    RectTransform = { AnchorMin = $"{pos} {_config.MarginBottom}", AnchorMax = $"{pos + _config.KitWidth} {1.0f - _config.MarginTop}" }
                }, "ui.kits", $"ui.kits.{kit.Name}");

                pos += _config.KitWidth + _config.MarginBetween;

                InitilizeNameLabelUI(ref container, kit.Name, kit.DisplayName);

                InitilizeKitImageUI(ref container, kit.Name);

                if (kit.Amount > 0)
                {
                    InitilizeAmountLabelUI(ref container, kit.Name, GetMsg("UI Amount", player).Replace("{amount}", (kit.Amount - playerData.Amount).ToString()));
                }

                if (kit.Cooldown > 0 && (playerData.Cooldown > currentTime))
                {
                    InitilizeMaskUI(ref container, kit.Name);
                    InitilizeCooldownLabelUI(ref container, kit.Name, TimeSpan.FromSeconds(playerData.Cooldown - currentTime));
                }
                else
                {
                    InitilizeButtonUI(ref container, kit.Name);
                }
            }
        }

        private void InitilizeKitsUINext(ref CuiElementContainer container, BasePlayer player)
        {
            _kitsGUI[player] = new List<string>();
            var currentTime = GetCurrentTime();
            var kits = GetKitsForPlayer(player).Take((int)(1.0f / (_config.KitWidth + _config.MarginBetween)) + 7).ToList();
            var pos = 0.525f - ((kits.Count - 7) * _config.KitWidth + (kits.Count - 1) * _config.MarginBetween) / 2;
            var kitsAmount = kits.Skip(7);
            foreach (var kit in kitsAmount)
            {
                _kitsGUI[player].Add(kit.Name);

                var playerData = GetPlayerData(player.userID, kit.Name);

                // Kit panel
                container.Add(new CuiPanel
                {
                    Image = { Color = "0 0 0 0" },
                    RectTransform = { AnchorMin = $"{pos} {_config.MarginBottom}", AnchorMax = $"{pos + _config.KitWidth} {1.0f - _config.MarginTop}" }
                }, "ui.kits", $"ui.kits.{kit.Name}");

                pos += _config.KitWidth + _config.MarginBetween;

                InitilizeNameLabelUI(ref container, kit.Name, kit.DisplayName);

                InitilizeKitImageUI(ref container, kit.Name);

                if (kit.Amount > 0)
                {
                    InitilizeAmountLabelUI(ref container, kit.Name, GetMsg("UI Amount", player).Replace("{amount}", (kit.Amount - playerData.Amount).ToString()));
                }

                if (kit.Cooldown > 0 && (playerData.Cooldown > currentTime))
                {
                    InitilizeMaskUI(ref container, kit.Name);
                    InitilizeCooldownLabelUI(ref container, kit.Name, TimeSpan.FromSeconds(playerData.Cooldown - currentTime));
                }
                else
                {
                    InitilizeButtonUI(ref container, kit.Name);
                }
            }
        }

        private void InitilizeKitsUI2(ref CuiElementContainer container, BasePlayer player)
        {
            _kitsGUI[player] = new List<string>();
            var currentTime = GetCurrentTime();
            var kits = GetKitsForPlayer(player).ToList();
            var pos = 0.56f - ((kits.Count - 14) * _config.KitWidth + (kits.Count - 1) * _config.MarginBetween) / 2;
            var kitsAmount = kits.Skip(14);
            foreach (var kit in kitsAmount)
            {
                _kitsGUI[player].Add(kit.Name);

                var playerData = GetPlayerData(player.userID, kit.Name);

                // Kit panel
                container.Add(new CuiPanel
                {
                    Image = { Color = "0 0 0 0" },
                    RectTransform = { AnchorMin = $"{pos} {_config.MarginBottom}", AnchorMax = $"{pos + _config.KitWidth} {1.0f - _config.MarginTop}" }
                }, "ui.kits", $"ui.kits.{kit.Name}");

                pos += _config.KitWidth + _config.MarginBetween;

                InitilizeNameLabelUI(ref container, kit.Name, kit.DisplayName);

                InitilizeKitImageUI(ref container, kit.Name);

                if (kit.Amount > 0)
                {
                    InitilizeAmountLabelUI(ref container, kit.Name, GetMsg("UI Amount", player).Replace("{amount}", (kit.Amount - playerData.Amount).ToString()));
                }

                if (kit.Cooldown > 0 && (playerData.Cooldown > currentTime))
                {
                    InitilizeMaskUI(ref container, kit.Name);
                    InitilizeCooldownLabelUI(ref container, kit.Name, TimeSpan.FromSeconds(playerData.Cooldown - currentTime));
                }
                else
                {
                    InitilizeButtonUI(ref container, kit.Name);
                }
            }
        }

        private void InitilizeKitImageUI(ref CuiElementContainer container, string kitname)
        {
            string image = _imagesCache.Get(kitname);
            CuiRawImageComponent imageComp = new CuiRawImageComponent
            {
                Sprite = "assets/content/textures/generic/fulltransparent.tga",
                Color = HexToRustFormat(_config.Image.Color)
            };
            if (image != string.Empty)
            {
                imageComp.Png = image;
            }
            container.Add(new CuiElement
            {
                Parent = $"ui.kits.{kitname}",
                Components =
                {
                    imageComp,
                    new CuiRectTransformComponent {AnchorMin = _config.Image.Position.AnchorMin, AnchorMax = _config.Image.Position.AnchorMax }
                }
            });
        }

        private void InitilizeNameLabelUI(ref CuiElementContainer container, string kitname, string text)
        {
            var name = container.Add(new CuiPanel
            {
                Image = { Color = HexToRustFormat(_config.Label.BackgroundColor) },
                RectTransform = { AnchorMin = _config.Label.Position.AnchorMin, AnchorMax = _config.Label.Position.AnchorMax }
            }, $"ui.kits.{kitname}");

            container.Add(new CuiLabel
            {
                Text = { Color = HexToRustFormat(_config.Label.ForegroundColor), FontSize = _config.Label.FontSize, Align = _config.Label.TextAnchor, Text = text }
            }, name);
        }

        private void InitilizeMaskUI(ref CuiElementContainer container, string kitname)
        {
            var name = container.Add(new CuiPanel
            {
                Image = { Color = HexToRustFormat(_config.DisableMaskColor) }
            }, $"ui.kits.{kitname}", $"ui.kits.{kitname}.mask");
        }

        private void InitilizeAmountLabelUI(ref CuiElementContainer container, string kitname, string text)
        {
            container.Add(new CuiPanel
            {
                Image = { Color = HexToRustFormat(_config.Amount.BackgroundColor) },
                RectTransform = { AnchorMin = _config.Amount.Position.AnchorMin, AnchorMax = _config.Amount.Position.AnchorMax }
            }, $"ui.kits.{kitname}", $"ui.kits.{kitname}.amount");

            container.Add(new CuiLabel
            {
                Text = { Color = HexToRustFormat(_config.Amount.ForegroundColor), FontSize = _config.Amount.FontSize, Align = _config.Amount.TextAnchor, Text = text }
            }, $"ui.kits.{kitname}.amount");
        }

        private void InitilizeButtonUI(ref CuiElementContainer container, string kitname)
        {
            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = $"kit {kitname}" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = "" }
            }, $"ui.kits.{kitname}", $"ui.kits.{kitname}.button");
        }

        private void InitilizeCooldownLabelUI(ref CuiElementContainer container, string kitname, TimeSpan time)
        {
            container.Add(new CuiPanel
            {
                Image = { Color = HexToRustFormat(_config.Time.BackgroundColor) },
                RectTransform = { AnchorMin = _config.Time.Position.AnchorMin, AnchorMax = _config.Time.Position.AnchorMax }
            }, $"ui.kits.{kitname}", $"ui.kits.{kitname}.time");

            container.Add(new CuiLabel
            {
                Text = { Color = HexToRustFormat(_config.Time.ForegroundColor), FontSize = _config.Time.FontSize, Align = _config.Time.TextAnchor, Text = TimeExtensions.FormatShortTime(time) }
            }, $"ui.kits.{kitname}.time");
        }

        #endregion

        #region Helpers 

        private KitData GetPlayerData(ulong userID, string name)
        {
            if (!_kitsData.ContainsKey(userID))
                _kitsData[userID] = new Dictionary<string, KitData>();

            if (!_kitsData[userID].ContainsKey(name))
                _kitsData[userID][name] = new KitData();

            return _kitsData[userID][name];
        }

        private List<KitItem> GetPlayerItems(BasePlayer player)
        {
            List<KitItem> kititems = new List<KitItem>();
            foreach (Item item in player.inventory.containerWear.itemList)
            {
                if (item != null)
                {
                    var iteminfo = ItemToKit(item, "wear");
                    kititems.Add(iteminfo);
                }
            }
            foreach (Item item in player.inventory.containerMain.itemList)
            {
                if (item != null)
                {
                    var iteminfo = ItemToKit(item, "main");
                    kititems.Add(iteminfo);
                }
            }
            foreach (Item item in player.inventory.containerBelt.itemList)
            {
                if (item != null)
                {
                    var iteminfo = ItemToKit(item, "belt");
                    kititems.Add(iteminfo);
                }
            }
            return kititems;
        }

        string GetMsg(string key, BasePlayer player = null) => lang.GetMessage(key, this, player == null ? null : player.UserIDString);

        private KitItem ItemToKit(Item item, string container)
        {
            KitItem kitem = new KitItem();
            kitem.Amount = item.amount;
            kitem.Container = container;
            kitem.SkinID = item.skin;
            kitem.Blueprint = item.blueprintTarget;
            kitem.ShortName = item.info.shortname;
            kitem.Condition = item.condition;
            kitem.Weapon = null;
            kitem.Content = null;
            if (item.info.category == ItemCategory.Weapon)
            {
                BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon != null)
                {
                    kitem.Weapon = new Weapon();
                    kitem.Weapon.ammoType = weapon.primaryMagazine.ammoType.shortname;
                    kitem.Weapon.ammoAmount = weapon.primaryMagazine.contents;
                }
            }
            if (item.contents != null)
            {
                kitem.Content = new List<ItemContent>();
                foreach (var cont in item.contents.itemList)
                {
                    kitem.Content.Add(new ItemContent()
                    {
                        Amount = cont.amount,
                        Condition = cont.condition,
                        ShortName = cont.info.shortname
                    });
                }
            }
            return kitem;
        }

        private List<Kit> GetKitsForPlayer(BasePlayer player)
        {
            return _kits.Where(kit => kit.Hide == false && (string.IsNullOrEmpty(kit.Permission) || permission.UserHasPermission(player.UserIDString, kit.Permission)) && (kit.Amount == 0 || (kit.Amount > 0 && GetPlayerData(player.userID, kit.Name).Amount < kit.Amount))).ToList();
        }

        private BasePlayer FindPlayer(BasePlayer player, string nameOrID)
        {
            ulong id;
            if (ulong.TryParse(nameOrID, out id) && nameOrID.StartsWith("7656119") && nameOrID.Length == 17)
            {
                var findedPlayer = BasePlayer.FindByID(id);
                if (findedPlayer == null || !findedPlayer.IsConnected)
                {
                    SendReply(player, GetMsg("Not Found Player", player));
                    return null;
                }

                return findedPlayer;
            }

            var foundPlayers = BasePlayer.activePlayerList.Where(x => x.displayName.ToLower().Contains(nameOrID.ToLower()));

            if (foundPlayers.Count() == 0)
            {
                SendReply(player, GetMsg("Not Found Player", player));
                return null;
            }

            if (foundPlayers.Count() > 1)
            {
                SendReply(player, GetMsg("To Many Player", player));
                return null;
            }

            return foundPlayers.First();
        }

        private double GetCurrentTime() => new TimeSpan(DateTime.UtcNow.Ticks).TotalSeconds;

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
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            Color color = new Color32(r, g, b, a);

            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }

        private static class TimeExtensions
        {
            public static string FormatShortTime(TimeSpan time)
            {
                string result = string.Empty;
                if (time.Days != 0)
                    result += $"{time.Days} д. ";

                if (time.Hours != 0)
                    result += $"{time.Hours} ч. ";

                if (time.Minutes != 0)
                    result += $"{time.Minutes} м. ";

                if (time.Seconds != 0)
                    result += $"{time.Seconds} с. ";

                return result;
            }

            public static string FormatTime(TimeSpan time)
            {
                string result = string.Empty;
                if (time.Days != 0)
                    result += $"{Format(time.Days, "дней", "дня", "день")} ";

                if (time.Hours != 0)
                    result += $"{Format(time.Hours, "часов", "часа", "час")} ";

                if (time.Minutes != 0)
                    result += $"{Format(time.Minutes, "минут", "минуты", "минута")} ";

                if (time.Seconds != 0)
                    result += $"{Format(time.Seconds, "секунд", "секунды", "секунда")} ";

                return result;
            }

            private static string Format(int units, string form1, string form2, string form3)
            {
                var tmp = units % 10;

                if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9)
                    return $"{units} {form1}";

                if (tmp >= 2 && tmp <= 4)
                    return $"{units} {form2}";

                return $"{units} {form3}";
            }
        }

        #endregion
    }
}