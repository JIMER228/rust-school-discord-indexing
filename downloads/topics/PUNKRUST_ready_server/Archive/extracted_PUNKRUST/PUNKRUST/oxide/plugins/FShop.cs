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
    [Info("FShop","Netrunner","1.0")]
    public class FShop : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ImageLibrary,EconomicsEvo,FMenu,Broadcast;
        
        List<ShopItem>_allItems = new List<ShopItem>();

        #endregion

        #region Config

        class ShopCategory
        {
            [JsonProperty("Название категории")]
            public string Name;
            [JsonProperty("Предметы")]
            public List<ShopItem> Items;
        }

        class ShopItem
        {
            [JsonProperty("Ключ предмета")]
            public string Key;
            [JsonProperty("Отображаемое имя")]
            public string DisplayName;
            [JsonProperty("Описание")]
            public string Description;
            [JsonProperty("Стоимость")]
            public int Price;
            [JsonProperty("Количество")]
            public int Amount;
            [JsonProperty("Shortname или команда")]
            public string Shortname;
            [JsonProperty("Картинка")]
            public string Image;
            [JsonProperty("Id скина")]
            public ulong SkinId;
            [JsonProperty("Является командой?")]
            public bool IsCommand;
            
        }
        
        static Configuration config = new Configuration();

        class Configuration
        {
            [JsonProperty("Ключ валюты")] 
            public string Currency;

            [JsonProperty("Настройка Магазина")] public Dictionary<string,ShopCategory> ShopItems = new Dictionary<string, ShopCategory>();
            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    Currency = "rp",
                    ShopItems =new Dictionary<string, ShopCategory>
                    {
                        ["items"] = new ShopCategory
                        {
                            Name = "Items",
                            Items = new List<ShopItem>
                            {
                                new ShopItem
                                {
                                    Key = "sulfur",
                                    Amount = 100,
                                    Price = 10,
                                    Description = "Сера",
                                    DisplayName = "Сера",
                                    IsCommand = false,
                                    Shortname = "sulfur",
                                    SkinId = 0,
                                    Image = ""
                                },
                                new ShopItem
                                {
                                    Key = "command",
                                    Amount = 1,
                                    Price = 1,
                                    Description = "Coin",
                                    DisplayName = "Coin",
                                    IsCommand = true,
                                    Shortname = "ebalanceservadd userid 5 rp",
                                    SkinId = 0,
                                    Image = ""
                                }
                            }
                        }
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

        #region Hooks

        void OnServerInitialized()
        {
            InithopItems();
            
        }

        #endregion

        #region Methods

        void InithopItems()
        {
            foreach (var category in config.ShopItems)
            foreach (var item in category.Value.Items)
            {
                _allItems.Add(item);
                if (item.IsCommand || item.SkinId != 0)
                    ImageLibrary.Call("AddImage", item.Image, item.Image);
            }
            ImageLibrary.Call("AddImage", "https://i.imgur.com/WdFT3Vw.png", "buybtn");
        }

        void GiveItem(BasePlayer player,ShopItem shopItem)
        {
            if (shopItem.IsCommand)
            {
                Server.Command(shopItem.Shortname.Replace("userid",player.UserIDString));
            }
            else
            {
                Item item = ItemManager.CreateByPartialName(shopItem.Shortname, shopItem.Amount, shopItem.SkinId);
                if (!item.MoveToContainer(player.inventory.containerMain))
                {
                    item.Drop(player.transform.position, Vector3.forward);
                }
            }

            var call = EconomicsEvo.Call("RemoveBalanceByID", player.userID, config.Currency,shopItem.Price);
            FMenu.Call("UpdateCurrency", player);
            NoticeBuyer(player, true);
            
        }
        

        #endregion

        #region UI

        void NoticeBuyer(BasePlayer player, bool good)
        {
            string text = good ? "Покупка совершена" : "Не хватает средств";
            Broadcast.Call("GetPlayerNotice", player, "БАРМЕН",text,"infoicn","assets/bundled/prefabs/fx/invite_notice.prefab");
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = {Color = good? HexToRustFormat("#52854d"):HexToRustFormat("#854d4d")},
                RectTransform = {AnchorMin = "0.5 0.1",AnchorMax = "0.5 0.1",OffsetMin = "-50 -15",OffsetMax = "50 15"}
            }, _layer,"BuyNotice");
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0",AnchorMax = "1 1"},
                Text = {Text = good? "Покупка совершена":"Не хватает средств",Align = TextAnchor.MiddleCenter,FontSize = 21}
            }, "BuyNotice");
            CuiHelper.AddUi(player, container);
            timer.Once(4f, () =>
            {
                CuiHelper.DestroyUi(player, "BuyNotice");
            });
        }
        private string _layer = "ShopUI";
        void ShopUI(BasePlayer player,string category, int page = 0)
        {
            CuiHelper.DestroyUi(player, _layer);
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0.5 0.5",AnchorMax = "0.5 0.5",OffsetMin = "-500 -270",OffsetMax = "500 270"},
                Image = {Color = "0 0 0 0"}
            }, "ContentUI", _layer);
            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                RectTransform = {AnchorMin = "0 1",AnchorMax = "1 1",OffsetMin = "0 -40",OffsetMax = "0 0"}
            }, _layer, "ShopCategories");
            double catx = 0, catLenght = 120;
            foreach (var categoryItem in config.ShopItems)
            {
                container.Add(new CuiButton
                {
                    Button = {Color = category==categoryItem.Key? HexToRustFormat("5FF6FE"): HexToRustFormat("#952F29"),Command = $"shop {categoryItem.Key} 0"},
                    RectTransform = {AnchorMin = "0 0",AnchorMax = "0 1",OffsetMin = $"{catx} 0",OffsetMax = $"{catx+catLenght} 0"},
                    Text = {Text = categoryItem.Value.Name,Color = category==categoryItem.Key?HexToRustFormat("#962E2C"):HexToRustFormat("FB6F69"),Align = TextAnchor.MiddleCenter,FontSize = 16}
                }, "ShopCategories");
                catx += catLenght + 5;
            }

            double itemx = 10, itemlenght = 90, itemheight = 110, itemy = -155;
            foreach (var shopItem in config.ShopItems[category].Items.Skip(page*38).Take(38))
            {
                container.Add(new CuiPanel
                {
                    Image = {Color = "0 0 0 0.6"},
                    RectTransform = {AnchorMin = "0 1",AnchorMax = "0 1",OffsetMin = $"{itemx} {itemy}",OffsetMax = $"{itemx+itemlenght} {itemy+itemheight}"},
                }, _layer,$"ShopItem{shopItem.Key}");
                container.Add(new CuiElement
                {
                    Parent = $"ShopItem{shopItem.Key}",
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = $"0 20",OffsetMax = "90 110"
                        },
                        new CuiRawImageComponent
                        {
                            Png = shopItem.IsCommand || shopItem.SkinId != 0 ? (string) ImageLibrary.Call("GetImage",shopItem.Image) : (string) ImageLibrary.Call("GetImage",shopItem.Shortname)
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = $"ShopItem{shopItem.Key}",
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "0 0",OffsetMax = "65 20"
                        },
                        new CuiRawImageComponent
                        {
                            Png = (string) ImageLibrary.Call("GetImage","buybtn")
                        }
                    }
                });
                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "0 0",OffsetMax = "90 20"},
                    Text = {Text = $"{shopItem.Price}",Align = TextAnchor.MiddleRight,Color = HexToRustFormat("f8db7d97")}
                }, $"ShopItem{shopItem.Key}");
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0",AnchorMax = "0 0",OffsetMin = "2 0",OffsetMax = "90 20"},
                    Button = {Color = "0 0 0 0",Command = $"shopbuy {shopItem.Key}"},
                    Text = {Align = TextAnchor.MiddleLeft,Text = "Купить",Color = "0 0 0 0.9"}
                }, $"ShopItem{shopItem.Key}");
                
                itemx += itemlenght + 10;
                if (itemx>= 1000)
                {
                    itemx = 10;
                    itemy -= itemheight + 20;
                }
            }

            /*if (config.ShopItems[category].Items.Count > (page+1)*38)
            {
                container.Add(new CuiButton
                {
                    Text = {Text = "->",Color = HexToRustFormat("f8db7d97"),Align = TextAnchor.MiddleCenter,FontSize = 21},
                    Button = {Command = $"shop {category} {page+1}",Color = "0 0 0 0.3"},
                    RectTransform = {AnchorMin = "1 0",AnchorMax = "1 0",OffsetMin = "-55 0",OffsetMax = "-5 25"}
                }, _layer);
                
            }

            if (page>0)
            {
                container.Add(new CuiButton
                {
                    Text = {Text = "<-",Align = TextAnchor.MiddleCenter,FontSize = 21,Color = HexToRustFormat("f8db7d97")},
                    Button = {Command = $"shop {category} {page-1}",Color = "0 0 0 0.3"},
                    RectTransform = {AnchorMin = "1 0",AnchorMax = "1 0",OffsetMin = "-110 0",OffsetMax = "-60 25"}
                }, _layer);
            }
            */
            
            if (config.ShopItems[category].Items.Count > (page+1)*38)
            {
                container.Add(new CuiButton
                {
                    Text = {Text = "ВПЕРЕД",Color = HexToRustFormat("f8db7d97"),Align = TextAnchor.MiddleCenter,FontSize = 21},
                    Button = {Command = $"shop {category} {page+1}",Color = "0 0 0 0.6"},
                    RectTransform = {AnchorMin = "1 0",AnchorMax = "1 0",OffsetMin = "-100 0",OffsetMax = "-5 25"}
                }, _layer);
                
            }

            if (page>0)
            {
                container.Add(new CuiButton
                {
                    Text = {Text = "НАЗАД",Align = TextAnchor.MiddleCenter,FontSize = 21,Color = HexToRustFormat("f8db7d97")},
                    Button = {Command = $"shop {category} {page-1}",Color = "0 0 0 0.6"},
                    RectTransform = {AnchorMin = "1 0",AnchorMax = "1 0",OffsetMin = "-200 0",OffsetMax = "-105 25"}
                }, _layer);
            }
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Commands

        [ConsoleCommand("shopbuy")]
        void ShopBuyCommand(ConsoleSystem.Arg arg)
        {
            if(arg.Player() == null || !arg.HasArgs()) return;
            BasePlayer player = arg.Player();
            ShopItem shopItem = _allItems.Find(p => p.Key == arg.Args[0]);
            if (shopItem == null)
            {
                PrintError("No Item");
                return;
            }

            if ((double)EconomicsEvo.Call("GetBalance",player.userID,config.Currency)<shopItem.Price)
            {
                NoticeBuyer(player, false);
                PrintWarning("No Money");
                return;
            }
            PrintWarning($"{player.userID} {config.Currency} {shopItem.Price}");
            GiveItem(player,shopItem);
        }

        [ConsoleCommand("shop")]
        void OpenShop(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null) return;

            BasePlayer player = arg.Player();
            if (!arg.HasArgs(2))
                ShopUI(player,config.ShopItems.First().Key);
            else
                ShopUI(player,arg.Args[0],arg.Args[1].ToInt());
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