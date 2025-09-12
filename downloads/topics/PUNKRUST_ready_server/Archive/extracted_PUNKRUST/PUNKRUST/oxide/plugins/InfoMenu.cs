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
    [Info("InfoMenu","Baks","2.3")]
    public class InfoMenu : RustPlugin
    {
        #region var

        [PluginReference] private Plugin ImageLibrary,BMenu;

        
        private string _layer = "Info_Layer";
        private string _parent = "ContentUI";
        
        
        #endregion
        
        #region config

        static Configuration config = new Configuration();

        class Configuration
        {
            [JsonProperty("Навигационное меню")] public List<MenuItem> Menus;
            
            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    Menus = new List<MenuItem>
                    {
                       new MenuItem
                       {
                           Key = "social",
                           Pages = new Dictionary<int, Page>
                           {
                               [1] = new Page
                               {
                                   Blocks = new List<Block>
                                   {
                                       new Block
                                       {
                                           Text = "fd;lakhjfihwbqakjnasfdlkjaslfnjlsdkjfbjhbdkqhnjenfkjabkjdbflkjqnAWKJEBGKAKDNSKJFGNKANDHJSBDFKJAKJNgnskjdfnjkfnaksnajksn",
                                           Img = false,
                                           Alignment = TextAnchor.MiddleCenter,
                                           MinX = 5,
                                           MinY = 300,
                                           MaxX = 100,
                                           MaxY = 100,
                                           priority = 0
                                       },
                                       new Block
                                       {
                                           Text = "https://i.ibb.co/StT43Cf/zzz.png",
                                           Img = true,
                                           MinX = 120,
                                           MinY = 300,
                                           MaxX = 300,
                                           MaxY = 100,
                                           priority = 1
                                       }
                                   }
                               }
                           }
                       },
                       new MenuItem
                       {
                           Key = "blueprint",
                           Pages = new Dictionary<int, Page>
                           {
                               [1] = new Page
                               {
                                   Blocks = new List<Block>
                                   {
                                       new Block
                                       {
                                           Text = "fd;lakhjfihwbqakjnasfdlkjaslfnjlsdkjfbjhbdkqhnjenfkjabkjdbflkjqnAWKJEBGKAKDNSKJFGNKANDHJSBDFKJAKJNgnskjdfnjkfnaksnajksn",
                                           Img = false,
                                           Alignment = TextAnchor.MiddleCenter,
                                           MinX = 5,
                                           MinY = 300,
                                           MaxX = 100,
                                           MaxY = 100,
                                           priority = 0
                                       },
                                       new Block
                                       {
                                           Text = "https://i.ibb.co/StT43Cf/zzz.png",
                                           Img = true,
                                           MinX = 120,
                                           MinY = 300,
                                           MaxX = 300,
                                           MaxY = 100,
                                           priority = 1
                                       }
                                   }
                               }
                           }
                       },
                       new MenuItem
                       {
                           Key = "wiki",
                           Pages = new Dictionary<int, Page>
                           {
                               [1] = new Page
                               {
                                   Blocks = new List<Block>
                                   {
                                       new Block
                                       {
                                           Text = "fd;lakhjfihwbqakjnasfdlkjaslfnjlsdkjfbjhbdkqhnjenfkjabkjdbflkjqnAWKJEBGKAKDNSKJFGNKANDHJSBDFKJAKJNgnskjdfnjkfnaksnajksn",
                                           Img = false,
                                           Alignment = TextAnchor.MiddleCenter,
                                           MinX = 5,
                                           MinY = 300,
                                           MaxX = 100,
                                           MaxY = 100,
                                           priority = 0
                                       },
                                       new Block
                                       {
                                           Text = "https://i.ibb.co/StT43Cf/zzz.png",
                                           Img = true,
                                           MinX = 120,
                                           MinY = 300,
                                           MaxX = 300,
                                           MaxY = 100,
                                           priority = 1
                                       }
                                   }
                               }
                           }
                       },
                       new MenuItem
                       {
                           Key = "server",
                           Pages = new Dictionary<int, Page>
                           {
                               [1] = new Page
                               {
                                   Blocks = new List<Block>
                                   {
                                       new Block
                                       {
                                           Text = "fd;lakhjfihwbqakjnasfdlkjaslfnjlsdkjfbjhbdkqhnjenfkjabkjdbflkjqnAWKJEBGKAKDNSKJFGNKANDHJSBDFKJAKJNgnskjdfnjkfnaksnajksn",
                                           Img = false,
                                           Alignment = TextAnchor.MiddleCenter,
                                           MinX = 5,
                                           MinY = 300,
                                           MaxX = 100,
                                           MaxY = 100,
                                           priority = 0
                                       },
                                       new Block
                                       {
                                           Text = "https://i.ibb.co/StT43Cf/zzz.png",
                                           Img = true,
                                           MinX = 120,
                                           MinY = 300,
                                           MaxX = 300,
                                           MaxY = 100,
                                           priority = 1
                                       }
                                   }
                               }
                           }
                       },
                       new MenuItem
                       {
                           Pages = new Dictionary<int, Page>
                           {
                               [1] = new Page
                               {
                                   Blocks = new List<Block>
                                   {
                                       new Block
                                       {
                                           Text = "fd;lakhjfihwbqakjnasfdlkjaslfnjlsdkjfbjhbdkqhnjenfkjabkjdbflkjqnAWKJEBGKAKDNSKJFGNKANDHJSBDFKJAKJNgnskjdfnjkfnaksnajksn",
                                           Img = false,
                                           Alignment = TextAnchor.MiddleCenter,
                                           MinX = 5,
                                           MinY = 300,
                                           MaxX = 100,
                                           MaxY = 100,
                                           priority = 0
                                       },
                                       new Block
                                       {
                                           Text = "https://i.ibb.co/StT43Cf/zzz.png",
                                           Img = true,
                                           MinX = 120,
                                           MinY = 300,
                                           MaxX = 300,
                                           MaxY = 100,
                                           priority = 1
                                       }
                                   }
                               }
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

        class MenuItem
        {
            public string Key;
            public Dictionary<int, Page> Pages;
        }

        class Block
        {
            [JsonProperty("Приоритет")]
            public int priority = 0;
            [JsonProperty("Текст")]
            public string Text;
            [JsonProperty("Картинка?(true/false)")]
            public bool Img;
            [JsonProperty("MinX")]
            public double MinX;
            [JsonProperty("MinY")]
            public double MinY = 920;
            [JsonProperty("MaxX")]
            public double MaxX;
            [JsonProperty("MaxY")]
            public double MaxY = 920;
            [JsonProperty("Цвет")] 
            public string color = "#FFFFFF";
            [JsonProperty("Размер шрифта")]
            public int FonSize = 18;
            [JsonProperty("Выравнивание текста")] 
            public TextAnchor Alignment = TextAnchor.MiddleCenter;

        }

        class Page
        {
            public List<Block> Blocks;
        }

        #endregion

        #region commands

        [ChatCommand("info")]
        void OpenInfo(BasePlayer player)
        {
            ContentUI(player);
        }

        [ConsoleCommand("infopage")]
        void InfoCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null) return;
            BasePlayer player = arg.Player();
            PageUI(player,arg.Args[0],arg.Args[1].ToInt());
        }
        
        

        #endregion

        #region UI
        class ButtonData
        {
            public string button;
            public string text;
            public double widht;
            public double index;
            public double padding;
        }
        Dictionary<string,ButtonData>_button = new Dictionary<string, ButtonData>();
        
        void PageUI(BasePlayer player,string key,int page = 1)
        {
            CuiHelper.DestroyUi(player, _layer);
            PrintWarning($"{key} {page}");
            var container = new CuiElementContainer();
            
            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                RectTransform = {AnchorMin = "0 0",AnchorMax = "1 1"}
            }, _parent,_layer);
            MenuItem menu = config.Menus.Find(p => p.Key == key);
            /*if (menu == null)
            {
                PrintWarning($"null item {key} {page}");
            }*/
            if (menu == null) return;
            int minIndex = menu.Pages.Keys.Min();
            int maxIndex = menu.Pages.Keys.Max();
            //PrintWarning($"{minIndex} : {maxIndex} - {page}");

            Block[] blockArray = menu.Pages[page].Blocks.ToArray();
            //SortToArray(ref blockArray,menu.Pages[page].Blocks);
            
            foreach (var block in blockArray)
            {
                
                if (block.Img)
                {
                    container.Add(new CuiElement
                    {
                        Parent = _layer,
                        Name = _layer+".Block",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = (string) ImageLibrary.Call("GetImage",block.Text)
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "0 0",OffsetMin = $"{block.MinX} {block.MinY}",OffsetMax = $"{block.MaxX} {block.MaxY}"
                            }
                        }
                    });
                    
                }
                else
                {
                    container.Add(new CuiLabel
                    {
                        Text = {Text = block.Text,Align = block.Alignment,Color = HexToCuiColor(block.color),FontSize = block.FonSize},
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "0 0",OffsetMin = $"{block.MinX} {block.MinY}",OffsetMax = $"{block.MaxX} {block.MaxY}"}
                    }, _layer, _layer + ".Block");
                }
                
            }
            

            /*if (page>minIndex)
            {
                container.Add(new CuiButton
                {
                    Button = {Close = _layer,Color = HexToCuiColor("#630606f4"),Command = $"{key} {page-1}"},
                    Text = {Text = "<",Align = TextAnchor.MiddleCenter},
                    RectTransform = {AnchorMin = "0 0",AnchorMax = "0.15 0.06"}
                }, _layer, _layer + ".Back");
            }
            if (page<maxIndex)
            {
                container.Add(new CuiButton
                {
                    Button = {Close = _layer,Color = HexToCuiColor("#630606f4"),Command = $"{key} {page+1}"},
                    Text = {Text = ">",Align = TextAnchor.MiddleCenter},
                    RectTransform = {AnchorMin = "0.85 0",AnchorMax = "1 0.06"}
                }, _layer, _layer + ".Forward");
            }*/
            if (page<maxIndex)
            {
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "190 10",OffsetMax = "550 80"},
                    Button = { Color = "0.88 0.83 0.63 0.9", Command = $"infopage {key} {page+1}",Material = "assets/icons/greyout.mat" },
                    Text = { Text = "<b>ВПЕРЁД</b>", Align = TextAnchor.MiddleCenter, FontSize = 21, Font = "robotocondensed-regular.ttf",Color = HexToCuiColor("#000000") }
                },  _layer, _layer + ".Forward");
            }

            if (page>minIndex)
            {
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "190 10",OffsetMax = "550 80"},
                    Button = { Color = "0.88 0.83 0.63 0.9", Command = $"infopage {key} {page-1}",Material = "assets/icons/greyout.mat" },
                    Text = { Text = "<b>НАЗАД</b>", Align = TextAnchor.MiddleCenter, FontSize = 21, Font = "robotocondensed-regular.ttf",Color = HexToCuiColor("#000000") }
                },  _layer, _layer + ".Back");
            }
            //PrintWarning($"item {key} {page}");
            //PrintWarning($"Key:{key} Page:{page} Blocks:{menu.Pages[page].Blocks.Count}");
            CuiHelper.AddUi(player, container);

        }

        void ContentUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, _parent);
            CuiHelper.DestroyUi(player, _layer);
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                RectTransform = {AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "345 15", OffsetMax = "1265 660"},

            }, "Main_UI", _parent);
            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                RectTransform = {AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "0 0", OffsetMax = "325 620"}
            }, _parent, _layer);
            
            container.Add(new CuiElement
            {
                Parent = _parent,
                Name = _parent + ".Image",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = (string) ImageLibrary.Call("GetImage", "clan"),

                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1"
                    },
                }
            });
            CuiHelper.AddUi(player, container);

        }
        
        #endregion

        #region hooks
        
        void OnServerInitialized()
        {
            foreach (var menuItem in config.Menus)
            //PrintWarning($"Key:{menuItem.Key} Pages:{menuItem.Pages.Count} Blocks:{menuItem.Pages[1].Blocks.Count}");
            foreach (var pages in menuItem.Pages.Values)
            foreach (var block in pages.Blocks)
                if (block.Img)
                    ImageLibrary.Call("AddImage", block.Text, block.Text);
            
        }

        #endregion

        #region helper

        void SortToArray(ref Block[] array,List<Block> list)
        {
            int length = array.Length;
            Block[] buffer = new Block[length];
           
            
            int index = 0;
            while (list.Count>0)
            {
                int min = GetMinIndex(list);
                foreach (var block in list)
                {
                    if (block.priority==min)
                    {
                        buffer[index] = block;
                        index++;
                        list.Remove(block);
                    }
                }
            }

            array = buffer;
        }

        

        int GetMinIndex(List<Block> array)
        {
            int min = array.GetRandom().priority;
            foreach (var block in array)
            {
                if (min>block.priority)
                {
                    min = block.priority;
                }
            }

            return min;
        }
        
        
        private static string HexToCuiColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFFFF";
            var str = hex.Trim('#');
            if (str.Length == 6) str += "FF";
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
            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }

        #endregion
    }
}