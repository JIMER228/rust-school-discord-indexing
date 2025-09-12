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
    [Info("Shop", "Sparkless", "0.0.3")]
    public class Shop : RustPlugin
    {
        [PluginReference] private Plugin Economics, CEconomics, ImageLibrary;
        
        
        public class ItemConfig
        {
            [JsonProperty("ШортНейм предмета || ShortName Item")]
            public string ShortName;

            [JsonProperty("Комманда || Commands")] 
            public string Command;

            [JsonProperty("Количество предмета || Amount item")]
            public int Amount;

            [JsonProperty("СкинИД Предмета || SkinID item")]
            public ulong SkinID;

            [JsonProperty("Название предмета || Name item")]
            public string NameItem;

            [JsonProperty("Картинка предмета || Image Item")]
            public string Image;

            [JsonProperty("Цена предмета || Price Item")]
            public float Price;

            [JsonProperty("Будет ли являться данный предмет чертежом? ||  Will be the subject of a blueprint?")]
            public bool IsBluePrint;

            [JsonProperty("Команда или же предмет || Command or Item")]
            public bool ItemOrCommand;

            internal void GiveItem(BasePlayer player)
            {
                if (!ItemOrCommand)
                {
                    if (!IsBluePrint)
                    {
                        var item = ItemManager.CreateByName(ShortName, Amount, SkinID);
                        if (item == null) return;
                        if (NameItem != null)
                        {
                            item.name = NameItem;
                        }
                        player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);   
                    }
                    else
                    {
                        var item = ItemManager.CreateByName("blueprintbase");
                        var items = ItemManager.FindItemDefinition(ShortName);
                        item.blueprintTarget = items.itemid;
                        player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
                    }
                }
                else
                {
                    Instance.Server.Command(Command.Replace("{steamid}", player.UserIDString), Array.Empty<object>());
                }
            }
        }


        public class ShopSettings
        {
            [JsonProperty("Названии категории || Name Category")] 
            public string NameCategory;

            [JsonProperty("Указывать ли, что данная категория - новинка? || Should I indicate that this category is new?")]
            public bool NoveltyCategory;

            [JsonProperty("Предметы в данной категории || Item's category")]
            public List<ItemConfig> ListItem;
        }

        public class ConfigData
        {
            [JsonProperty("Настройка категорий || Settings Category")] 
            public List<ShopSettings> ListCategory;

            [JsonProperty("CEconomics или же Economics ? || CEconomics or Economics?")]
            public bool ChoiceEconomics;

            [JsonProperty("Название валюты || Name Currencies")] 
            public string NameCurrency;

            public static ConfigData GetNewCong()
            {
                ConfigData newConfig = new ConfigData();
                newConfig.ChoiceEconomics = true;
                newConfig.NameCurrency = "G";
                newConfig.ListCategory = new List<ShopSettings>()
                {
                    new ShopSettings()
                    {
                        NameCategory = "Оружие",
                        NoveltyCategory = true,
                        ListItem = new List<ItemConfig>()
                        {
                            new ItemConfig()
                            {
                                ShortName = "rifle.ak",
                                Command = "",
                                Amount = 1,
                                SkinID = 0,
                                NameItem = "Автомат калашникова",
                                Image = "",
                                Price = 100,
                                IsBluePrint = false,
                                ItemOrCommand = false,
                            },
                            new ItemConfig()
                            {
                                ShortName = "pistol.semiauto",
                                Command = "o.grant user {steamid} vip.use",
                                IsBluePrint = false,
                                Amount = 1,
                                SkinID = 0,
                                NameItem = "Пешка",
                                Image = "",
                                Price = 25,
                                ItemOrCommand = true
                            }
                        }
                    }
                };
                return newConfig;
            }
        }


        private ConfigData _config;
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<ConfigData>();
                if (_config?.ListCategory == null) LoadDefaultConfig();
            }
            catch
            {
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => _config = ConfigData.GetNewCong();
        protected override void SaveConfig() => Config.WriteObject(_config);

        private static Shop Instance;


        void OnServerInitialized()
        {
            Instance = this;
            foreach (var variable in _config.ListCategory)
            {
                foreach (var key in variable.ListItem)
                {
                    ImageLibrary.Call("AddImage", key.Image, key.Image);
                }
            }
            PrintWarning($"|-----------------------------------|\n|          Author: SPARKLESS     |\n|          VK: vk.com/draggb         |\n|          Discord: Sparkless#7640      |\n|          Email: romansparkless@gmail.com      |\n|-----------------------------------|\nIf you want to order a plugin from me, I am waiting for you in discord.");
            PrintWarning("Благодарим за покупку плагина на сайте RustPlugin.ru. Если вы передадите этот плагин сторонним лицам знайте - это лишает вас гарантированных обновлений!");
            PrintWarning("Плагин был успешно загружен!");
        }
        
        private static string HexToCuiColor(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                hex = "#FFFFFFFF";
            }

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

        public string Layer = "UI_CupLayerShop";
        
        [ChatCommand("shop")]
        void OpenShop(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                Image = {Color = HexToCuiColor("#000000E6")},
            }, "Overlay", Layer);
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "-100 -100", AnchorMax = "100 100", OffsetMax = "0 0"},
                Button = {Color = "0 0 0 0", Close = Layer},
                Text = {Text = ""}
            }, Layer);
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.2223959 0.807409", AnchorMax = "0.7770834 0.8842608"},
                Button = {Color = "0.4156863 0.4156863 0.4156863 0.3686275", FadeIn = 0.1f},
                Text =
                {
                    Text = $"МАГАЗИН", FontSize = 29, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#bdb9b6"),
                    Font = "robotocondensed-bold.ttf"
                }
            }, Layer);
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent {Color = "0.227451 0.2156863 0.1921569 0.8235294"},
                    new CuiRectTransformComponent {AnchorMin = "0.2223959 0.1916667", AnchorMax = "0.7770834 0.802778"}
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent {Color = "0.4156863 0.4156863 0.4156863 0.3686275"},
                    new CuiRectTransformComponent {AnchorMin = "0.2223959 0.1101941", AnchorMax = "0.7770834 0.1870485"}
                }
            });
            CuiHelper.AddUi(player, container);
            OpenListShop(player, _config.ListCategory.FirstOrDefault());
        }

        void OpenListShop(BasePlayer player, ShopSettings settings, int page = 1)
        {
            CuiElementContainer container = new CuiElementContainer();
            
            for (int i = 0; i < 15; i++)
            {
                CuiHelper.DestroyUi(player, Layer + $".{i}.ShopCategory");
                CuiHelper.DestroyUi(player, Layer + $".{i}.ShopItem");
            }
            foreach (var check in _config.ListCategory.Select((i, t) => new {A = i, B = t}))
            { 
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin =
                            $"{0.2328125 + check.B * 0.090 - Math.Floor((double) check.B / 100) * 100 * 0.090} {0.1231483 - Math.Floor((double) check.B / 100) * 0.155}",
                        AnchorMax =
                            $"{0.3125 + check.B * 0.090 - Math.Floor((double) check.B / 100) * 100 * 0.090} {0.1759259 - Math.Floor((double) check.B / 100) * 0.155}",
                        OffsetMax = "0 0"
                    },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Command = $""
                    },
                    Text =
                    {
                        Text = $"", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf",
                        FontSize = 25, Color = HexToCuiColor("#d5cfcd")
                    }
                }, Layer, Layer + $".{check.B}.ShopCategory");

                if (check.A == settings)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.006536633 0.03508535", AnchorMax = "0.9869279 0.6315781" },
                        Button = {Color = HexToCuiColor("#6A6A6A86"), Command = $"shop.console pagecategory {check.A.NameCategory}", FadeIn = 0.1f},
                        Text = { Text = check.A.NameCategory, FontSize = 14, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                    }, Layer + $".{check.B}.ShopCategory", Layer + ".TextShopCategory");
                }
                else
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = "0.006536633 0.03508535", AnchorMax = "0.9869279 0.6315781"},
                        Button = {Color = "0.4156863 0.4156863 0.4156863 0.3686275", Command = $"shop.console pagecategory {check.A.NameCategory}", FadeIn = 0.1f},
                        Text =
                        {
                            Text = check.A.NameCategory, FontSize = 14, Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf"
                        }
                    }, Layer + $".{check.B}.ShopCategory", Layer + ".TextShopCategory");
                }

                if (check.A.NoveltyCategory)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = "0.490196 0.6315781", AnchorMax = "0.9803921 0.9649129"},
                        Button = {Color = "0 0 0 0", FadeIn = 0.1f},
                        Text =
                        {
                            Text = "Новинка!", Color = "0.5921569 0.4745098 0.2313726 1", FontSize = 10, Align = TextAnchor.MiddleRight,
                            Font = "robotocondensed-regular.ttf"
                        }
                    }, Layer + $".{check.B}.ShopCategory", Layer + ".TextShopCategory");
                }
            }
            CuiHelper.DestroyUi(player, Layer + ".Back");
            container.Add(new CuiButton
            {
                RectTransform =
                    {AnchorMin = "0.222403 0.262963", AnchorMax = "0.2432292 0.7333337", OffsetMax = "0 0"},
                Button =
                {
                    FadeIn = 0f, Color = "0.4156863 0.4156863 0.4156863 0.3686275",
                    Command = $"shop.console {page - 1} {settings.NameCategory}"
                },
                Text =
                {
                    Text = "◄", Align = TextAnchor.MiddleCenter, Color = "0.7137255 0.7137255 0.7058824 1",
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 30
                }
            }, Layer, Layer + ".Back");
            CuiHelper.DestroyUi(player, Layer + ".Run");
            container.Add(new CuiButton
            {
                RectTransform =
                    {AnchorMin = "0.7562498 0.262963", AnchorMax = "0.7770754 0.7333337", OffsetMax = "0 0"},
                Button =
                {
                    FadeIn = 0f, Color = "0.4156863 0.4156863 0.4156863 0.3686275",
                    Command = $"shop.console {page + 1} {settings.NameCategory}"
                },
                Text =
                {
                    Text = "►", Align = TextAnchor.MiddleCenter, Color = "0.7137255 0.7137255 0.7058824 1",
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 30
                }
            }, Layer, Layer + ".Run");
            foreach (var check in settings.ListItem.Select((i, t) => new {A = i, B = t - (page - 1) * 15}).Skip((page - 1) * 15).Take(15))
            {
                container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin =
                                $"{0.2765697 + check.B * 0.0920 - Math.Floor((double) check.B / 5) * 5 * 0.0920} {0.5861111 - Math.Floor((double) check.B / 5) * 0.16}",
                            AnchorMax =
                                $"{0.3583333 + check.B * 0.0920 - Math.Floor((double) check.B / 5) * 5 * 0.0920} {0.7333337 - Math.Floor((double) check.B / 5) * 0.16}",
                            OffsetMax = "0 0"
                        },
                        Button =
                        {
                            Color = HexToCuiColor("#00000070"),
                            Command = check.A.ItemOrCommand ? $"shop.console buy {settings.NameCategory} {check.A.Command}" :  $"shop.console buy {settings.NameCategory} {check.A.ShortName}"
                        },
                        Text =
                        {
                            Text = $"", Align = TextAnchor.UpperCenter, Font = "robotocondensed-bold.ttf", FontSize = 15
                        }
                    }, Layer, Layer + $".{check.B}.ShopItem");
                if (string.IsNullOrEmpty(check.A.Image))
                {
                    container.Add(new CuiElement
                    {
                        FadeOut = 0.3f,
                        Parent = Layer + $".{check.B}.ShopItem",
                        Name = Layer + $".{check.B}.Img",
                        Components =
                        {
                            new CuiRawImageComponent
                                {FadeIn = 0.3f, Png = (string) ImageLibrary?.Call("GetImage",  check.A.ShortName)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 1", OffsetMax = "-5 -1"}
                        }
                    });   
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        FadeOut = 0.3f,
                        Parent = Layer + $".{check.B}.ShopItem",
                        Name = Layer + $".{check.B}.Img",
                        Components =
                        {
                            new CuiRawImageComponent
                                {FadeIn = 0.3f, Png = (string) ImageLibrary?.Call("GetImage",  check.A.Image)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 1", OffsetMax = "-5 -1"}
                        }
                    });   
                }
                container.Add(new CuiElement
                {
                    FadeOut = 0.3f,
                    Parent = Layer + $".{check.B}.ShopItem",
                    Name = Layer + $".{check.B}.Txt",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = $"x{check.A.Amount}", Align = TextAnchor.LowerRight,
                            Font = "robotocondensed-regular.ttf", FontSize = 10, Color = HexToCuiColor("#A7A19EFF")
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 1", OffsetMax = "-5 -1"}
                    }
                });
                container.Add(new CuiElement
                {
                    FadeOut = 0.3f,
                    Parent = Layer + $".{check.B}.ShopItem",
                    Name = Layer + $".{check.B}.Txt1",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text =  $"{check.A.Price} {_config.NameCurrency}", Align = TextAnchor.LowerLeft,
                            Font = "robotocondensed-regular.ttf", FontSize = 10,Color = HexToCuiColor("#A7A19EFF")
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 1", OffsetMax = "-5 -1"}
                    }
                });
            }
            CuiHelper.AddUi(player, container);
        }


        object GetBalance(BasePlayer player)
        {
            if (_config.ChoiceEconomics)
            {
                float balance = (float) CEconomics?.Call("GetBalance", player.userID);
                return balance;
            }
            if (!_config.ChoiceEconomics)
            {
                double balance = (double) Economics?.Call("Balance", player.userID);
                return balance;
            }

            return null;
        }


        [ConsoleCommand("shop.console")]
        void ShopConsole(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            int page = 1;
            if (int.TryParse(args.Args[0], out page))
            {
                var find = _config.ListCategory.FirstOrDefault(p => p.NameCategory == string.Join(" ", args.Args.Skip(1).ToArray()));
                if (find != null && page > 0 && (page - 1) * 15 <= find.ListItem.Count)
                {
                    OpenListShop(player, find, page);   
                }
            }
            else if (args.Args[0] == "pagecategory")
            {
                var find = _config.ListCategory.FirstOrDefault(p => p.NameCategory == string.Join(" ", args.Args.Skip(1).ToArray()));
                if (find != null)
                {
                    OpenListShop(player, find, 1);
                }
            }
            else if (args.Args[0] == "buy")
            {
                var find = _config.ListCategory.FirstOrDefault(p => p.NameCategory == args.Args[1]);
                if (find == null) return;
                var findshortname = find.ListItem.FirstOrDefault(p => p.ShortName == string.Join(" ", args.Args.Skip(2).ToArray()) || p.Command == string.Join(" ", args.Args.Skip(2).ToArray()));
                if (findshortname == null) return;
                if (_config.ChoiceEconomics)
                {
                    if ( GetBalance(player) is float &&  (float) GetBalance(player) < findshortname.Price)
                    {
                        player.ChatMessage($"У вас недостаточно монет для покупки данного предмета!");
                        return;  
                    }
                }
                else
                {
                    if ( GetBalance(player) is double && (double) GetBalance(player) < findshortname.Price)
                    {
                        player.ChatMessage($"У вас недостаточно монет для покупки данного предмета!");
                        return;  
                    }
                }
                findshortname.GiveItem(player);
                if (_config.ChoiceEconomics)
                {
                    CEconomics?.Call("RemoveBalance", player.userID,  findshortname.Price);
                }
                else
                {
                    Economics.Call("Withdraw", player.userID.ToString(), (double) findshortname.Price);
                }
                player.ChatMessage($"Вы успешно купили <color=#00FF00> {findshortname.NameItem}</color>.\n\n<color=#00FF00>Спасибо за покупку!</color>");
                Effect.server.Run("assets/prefabs/deployable/vendingmachine/effects/vending-machine-purchase-human.prefab", player.transform.position);
            }
        }
        
    }
}