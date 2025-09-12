using System.Collections.Generic;
using System;
using System.Reflection;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core.Configuration;
using System.Linq;
using System.Text;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Network;
using Oxide.Core;
using System.Collections;
using Oxide.Plugins;
using Oxide.Core.Plugins;


namespace Oxide.Plugins
{
    [Info("GameStores", "DeRzKiU", "1.7.2")]
    class GameStoresRUST : RustPlugin
    {
        private string Request => $"https://gamestores.ru/api/?shop_id={Config["SHOP.ID"]}&secret={Config["SECRET.KEY"]}&server={Config["SERVER.ID"]}";
        private List<Dictionary<string, object>> Stats = new List<Dictionary<string, object>>();
        private List<Dictionary<string, object>> Leaves = new List<Dictionary<string, object>>();
        private Dictionary<int, string> _OlditemIdShortnameConversions = new Dictionary<int, string>();
        private Dictionary<string, long> TakeAllBan = new Dictionary<string, long>();
        private Dictionary<string, long> Requests = new Dictionary<string, long>();
        private List<string> Log = new List<String>();


        #region [Override] Load default configurations
        protected override void LoadDefaultConfig()
        {
            Config["SHOP.ID"] = "30593";
            Config["SERVER.ID"] = "22287";
            Config["SECRET.KEY"] = "a7413e308c631d1f29230005e47a6797";
            Config["BUCKET.IMG"] = "https://i.imgur.com/J8rnXtZ.png";
            Config["ITEMS.SPLIT"] = true;
            Config["COMMAND.TOP"] = true;
            Config["UI.ENABLED"] = true;
            Config["TOP.USERS"] = true;
            Config["BUCKET.BUTTON"] = true;
        }
        #endregion

        #region [HookMethod] [Unload]
        private void Unload()
        {
            if (System.Convert.ToBoolean(Config["UI.ENABLED"]) && BasePlayer.activePlayerList.Count > 0)
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(player, "ui.store.buttonimage");
                    CuiHelper.DestroyUi(player, "ui.store.button");
                    DestroyUI(player);
                }
        }
        #endregion

        #region[Variables]
        private Dictionary<BasePlayer, int> Items = new Dictionary<BasePlayer, int>();
        private Dictionary<BasePlayer, int> Index = new Dictionary<BasePlayer, int>();
        string shopLink = string.Empty;
        #endregion

        #region [HookMethod] On server intitialized
        private void OnServerInitialized()
        {
            for (int i = 5; i >= 1; i--)
            {
                string TempOldName = i == 1 ? "GameStores.Log" : "GameStores.Log." + (i - 1).ToString();
                if (Core.Interface.Oxide.DataFileSystem.ExistsDatafile(TempOldName))
                {
                    Log = Core.Interface.Oxide.DataFileSystem.ReadObject<List<String>>(TempOldName);
                    Core.Interface.Oxide.DataFileSystem.WriteObject("GameStores.Log." + i.ToString(), Log);
                    Log = new List<String>();
                }
            }
            Core.Interface.Oxide.DataFileSystem.WriteObject("GameStores.Log", Log);
            if (Config["SECRET.KEY"].ToString().Contains("KEY"))
            {
                Debug.LogError("Plugin isn't configured");
            }
            else
            {
                if (System.Convert.ToBoolean(Config["UI.ENABLED"]))
                {
                    webrequest.EnqueueGet($"{this.Request}&info=true", (code, response) =>
                    {
                        switch (code)
                        {
                            case 0:
                                Debug.LogError("Api does not responded to a request");
                                break;
                            case 200:
                                Dictionary<string, object> Response = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, new KeyValuesConverter());
                                Dictionary<string, object> data = (Response["data"] as Dictionary<string, object>);
                                shopLink = $"{data["link"]}";
                                break;
                            case 404:
                                Debug.LogError("Response code: 404, please check your configurations");
                                break;
                        }

                    }, this);
                }
            }

            webrequest.EnqueueGet($"https://gamestores.ru/OldRustItemsIDs.json", (code, response) =>
            {
                switch (code)
                {
                    case 0:
                        Debug.LogError("Api does not responded to a request");
                        break;
                    case 200:
                        Debug.LogWarning("Old IDs of Items successfully loaded");
                        _OlditemIdShortnameConversions = JsonConvert.DeserializeObject<Dictionary<int, string>>(response, new KeyValuesConverter());
                        break;
                    case 404:
                        Debug.LogError("Response code: 404, please check your configurations");
                        break;
                }

            }, this);
        }
        #endregion

        #region[HookMethod] OnPlayerSleepEnded
        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (System.Convert.ToBoolean(Config["UI.ENABLED"]))
            {
                if (System.Convert.ToBoolean(Config["BUCKET.BUTTON"]))
                {
                    string Image = Config["BUCKET.IMG"].ToString();

                    CuiElementContainer UI = new CuiElementContainer();
                    UI.Add(new CuiElement()
                    {
                        Parent = "Hud",
                        Name = "ui.store.buttonimage",
                        Components =
                    {
                        new CuiRawImageComponent
                        {
                            Sprite = "assets/content/textures/generic/fulltransparent.tga",
                            Url = Image
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.01 0.948",
                            AnchorMax = "0.035 0.990"
                        }
                    }
                    });
                    UI.Add(new CuiButton()
                    {
                        Button =
                    {
                        Command = $"ui.store {player.userID}",
                        Color = "0 0 0 0"
                    },
                        RectTransform =
                    {
                        AnchorMin = "0.01 0.95",
                        AnchorMax = "0.042 0.99"
                    },
                        Text =
                    {
                        Text = ""
                    }
                    }, "Hud", "ui.store.button");
                    CuiHelper.DestroyUi(player, "ui.store.buttonimage");
                    CuiHelper.DestroyUi(player, "ui.store.button");
                    CuiHelper.AddUi(player, UI);
                }
            }
        }
        #endregion 

        #region[Method] Executing - WebRequest callback handler
        private void Executing(BasePlayer Player, string response, int code)
        {
            switch (code)
            {
                case 0:
                    Debug.LogError("Api does not responded to a request");
                    Player.ChatMessage("Корзина недоступна. Попробуйте позже");
                    break;
                case 200:
                    Dictionary<string, object> Response = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, new KeyValuesConverter());
                    if (Response != null && response != null && response != "null")
                    {
                        CuiElementContainer UI = new CuiElementContainer();
                        switch (System.Convert.ToInt32(Response["code"]))
                        {
                            case 100:
                                List<object> data = Response["data"] as List<object>;
                                DestroyUI(Player);

                                if (System.Convert.ToBoolean(Config["UI.ENABLED"]))
                                {

                                    #region[BackGround] Close
                                    UI.Add(new CuiButton
                                    {
                                        Button =
                                    {
                                        Command = $"ui.destroy {Player.userID}",
                                        Color = "1 0.1 0.1 0"
                                    },
                                        RectTransform =
                                    {
                                        AnchorMin = "0.0 0.0",
                                        AnchorMax = "0.999 0.999"
                                    },
                                        Text =
                                    {
                                        Color = "0.9 0.9 0.9 1",
                                        Text = "",
                                        FontSize = 25,
                                        Align = TextAnchor.MiddleCenter
                                    }
                                    }, "Hud", "ui.close.background");
                                    #endregion 

                                    #region[Panel] Parent
                                    UI.Add(new CuiPanel()
                                    {
                                        Image =
                                    {
                                        Color = "0 0 0 0.95"
                                    },
                                        RectTransform =
                                    {
                                        AnchorMin = "0.20 0.18",
                                        AnchorMax = "0.80 0.95"
                                    },
                                        CursorEnabled = true
                                    }, "Hud", "ui.store");
                                    #endregion

                                    #region[Panel] Items
                                    int index = 0;
                                    Items.Add(Player, data.Count);
                                    Index.Add(Player, data.Count > 14 ? 14 : data.Count);
                                    for (int r = 0; r < 4; r++)
                                    {
                                        for (int i = 0; i < (r > 1 ? 2 : 5); i++)
                                        {
                                            #region[Panel] Backgroud
                                            UI.Add(new CuiPanel()
                                            {
                                                Image =
                                            {
                                                Color = "0.1 0.1 0.1 1"
                                            },
                                                RectTransform =
                                            {
                                                AnchorMin = $"{0.04f + (0.19 * i)} {0.75f - (r * 0.23f)}",
                                                AnchorMax = $"{0.19f + ((0.19f * i) - (i == 4 ? 0f : 0f))} {0.95f - (r * 0.23f)}"
                                            }

                                            }, "ui.store", $"ui.background{index}");
                                            #endregion

                                            if (index < data.Count)
                                            {
                                                Dictionary<string, object> itemdata = data[index] as Dictionary<string, object>;

                                                int ItemID = System.Convert.ToInt32(itemdata["item_id"]);
                                                int Amount = System.Convert.ToInt32(itemdata["amount"]);
                                                string Image = $"{itemdata["img"]}";


                                                #region[Element] BpBlock
                                                if (itemdata["type"].ToString() == "bp")
                                                {
                                                    UI.Add(new CuiElement()
                                                    {
                                                        Name = $"ui.bp{index}",
                                                        Parent = $"ui.background{index}",
                                                        Components =
                                                    {

                                                        new CuiRawImageComponent
                                                        {
                                                            Sprite = "assets/content/textures/generic/fulltransparent.tga",
                                                            Url = "https://gamestores.ru/img/games/rust/blueprintbase.png"
                                                        },
                                                        new CuiRectTransformComponent
                                                        {
                                                            AnchorMin = $"0.10 0.10",
                                                            AnchorMax = $"0.90 0.90"
                                                        },
                                                        new CuiOutlineComponent
                                                        {
                                                            Distance = "1.0 1.0",
                                                            Color = "0.0 0.0 0.0 1.0"
                                                        }
                                                    }
                                                    });
                                                }
                                                #endregion

                                                #region[Element] ImgBlock                                                                                      
                                                UI.Add(new CuiElement()
                                                {
                                                    Name = $"ui.block{index}",
                                                    Parent = $"ui.background{index}",
                                                    Components =
                                                {

                                                    new CuiRawImageComponent
                                                    {
                                                        Sprite = "assets/content/textures/generic/fulltransparent.tga",
                                                        Url = Image
                                                    },
                                                    new CuiRectTransformComponent
                                                    {
                                                        AnchorMin = $"0.10 0.10",
                                                        AnchorMax = $"0.90 0.90"
                                                    },
                                                    new CuiOutlineComponent
                                                    {
                                                        Distance = "1.0 1.0",
                                                        Color = "0.0 0.0 0.0 0.0"
                                                    }
                                                }
                                                });
                                                #endregion

                                                #region[Label] Amount
                                                UI.Add(new CuiLabel()
                                                {
                                                    RectTransform =
                                                {
                                                    AnchorMin = $"0.0 0.0",
                                                    AnchorMax = $"1.0 0.90"
                                                },
                                                    Text =
                                                {
                                                    Text = $"{Amount} шт. ",
                                                    FontSize = 14,
                                                    Align = TextAnchor.LowerRight,
                                                    Color = "1 1 1 1"
                                                }
                                                }, $"ui.background{index}", $"ui.amount{index}");
                                                #endregion

                                                #region[ItemName] Product
                                                UI.Add(new CuiLabel()
                                                {
                                                    RectTransform =
                                                {
                                                    AnchorMin = $"0.05 0.01",
                                                    AnchorMax = $"0.99 0.99"
                                                },
                                                    Text =
                                                {
                                                    Text = $"{itemdata["name"]}",
                                                    FontSize = 14,
                                                    Align = TextAnchor.UpperLeft,
                                                    Color = "1 1 1 1"
                                                }
                                                }, $"ui.background{index}", $"ui.product{index}");
                                                #endregion

                                                #region[Button] Take
                                                UI.Add(new CuiButton
                                                {
                                                    Button =
                                                {
                                                    Command = $"ui.gives {Player.userID} {index} {itemdata["id"]}",
                                                    Color = "0 0 0 0"
                                                },
                                                    RectTransform =
                                                {
                                                    AnchorMin = $"0.0 0.0",
                                                    AnchorMax = $"1.0 1.0"
                                                },
                                                    Text =
                                                {
                                                    Text = ""
                                                }
                                                }, $"ui.background{index}", $"ui.command.take{index}");
                                                #endregion

                                            }
                                            index++;
                                        }
                                    }
                                    #endregion

                                    #region[Button] Close
                                    UI.Add(new CuiButton
                                    {
                                        Button =
                                    {
                                        Command = $"ui.destroy {Player.userID}",
                                        Color = "0.1 0.1 0.1 0"
                                    },
                                        RectTransform =
                                    {
                                        AnchorMin = "0.960 0.94",
                                        AnchorMax = "0.999 0.998"
                                    },
                                        Text =
                                    {
                                        Color = "0.9 0.9 0.9 1",
                                        Text = "X",
                                        FontSize = 25,
                                        Align = TextAnchor.MiddleCenter
                                    }
                                    }, "ui.store", "ui.close");
                                    #endregion

                                    #region[Button] Back
                                    UI.Add(new CuiButton
                                    {
                                        Button =
                                    {
                                        Command = $"ui.back {Player.UserIDString}",
                                        Color = "0.1 0.1 0.1 1"
                                    },
                                        RectTransform =
                                    {
                                        AnchorMin = $"0.42 0.39",
                                        AnchorMax = $"0.65 0.49"
                                    },
                                        Text =
                                    {
                                        Text = "Назад",
                                        Color = "1 1 1 1",
                                        Align = TextAnchor.MiddleCenter,
                                        FontSize = 20
                                    }
                                    }, "ui.store", $"ui.back");
                                    #endregion

                                    #region[Button] Next
                                    UI.Add(new CuiButton
                                    {
                                        Button =
                                    {
                                        Command = $"ui.next {Player.UserIDString}",
                                        Color = "0.1 0.1 0.1 1"
                                    },
                                        RectTransform =
                                    {
                                        AnchorMin = $"0.70 0.39",
                                        AnchorMax = $"0.95 0.49"
                                    },
                                        Text =
                                    {
                                        Text = "Вперёд",
                                        Color = "1 1 1 1",
                                        Align = TextAnchor.MiddleCenter,
                                        FontSize = 20
                                    }
                                    }, "ui.store", $"ui.next");
                                    #endregion

                                    #region[Button] TakeAll
                                    UI.Add(new CuiButton
                                    {
                                        Button =
                                    {
                                        Command = $"ui.takeall {Player.UserIDString}",
                                        Color = "0.1 0.1 0.1 1"
                                    },
                                        RectTransform =
                                    {
                                        AnchorMin = $"0.42 0.25",
                                        AnchorMax = $"0.95 0.35"
                                    },
                                        Text =
                                    {
                                        Text = "Забрать всё",
                                        Color = "1 1 1 1",
                                        Align = TextAnchor.MiddleCenter,
                                        FontSize = 20
                                    }
                                    }, "ui.store", $"ui.command.takeall");
                                    #endregion

                                    #region[Label] shopLink
                                    UI.Add(new CuiLabel()
                                    {
                                        RectTransform =
                                    {
                                        AnchorMin = $"0.42 0.15",
                                        AnchorMax = $"0.95 0.25"
                                    },
                                        Text =
                                    {
                                        Text = "Магазин",
                                        Align = TextAnchor.MiddleCenter,
                                        Color = "1 1 1 1",
                                        FontSize = 40
                                    }
                                    }, "ui.store", "ui.link");
                                    #endregion

                                    #region[Label] shopLink
                                    UI.Add(new CuiLabel()
                                    {
                                        RectTransform =
                                    {
                                        AnchorMin = $"0.42 0.05",
                                        AnchorMax = $"0.95 0.15"
                                    },
                                        Text =
                                    {
                                        Text = shopLink,
                                        Align = TextAnchor.MiddleCenter,
                                        Color = "1 1 1 1",
                                        FontSize = 35
                                    }
                                    }, "ui.store", "ui.link");
                                    #endregion

                                    CuiHelper.AddUi(Player, UI);
                                    return;
                                }

                                #region [UI OFF] Give Items If UI Off
                                foreach (object pair in data)
                                {
                                    Dictionary<string, object> iteminfo = pair as Dictionary<string, object>;

                                    if (iteminfo.ContainsKey("command"))
                                    {
                                        string command = iteminfo["command"].ToString().ToLower().Replace('\n', '|').Replace("%steamid%", Player.UserIDString).Replace("%username%", Player.displayName);
                                        String[] CommandArray = command.Split('|');
                                        foreach (var substring in CommandArray)
                                        {
                                            ConsoleSystem.Run.Server.Normal(substring);
                                            //ConsoleSystem.Run(ConsoleSystem.Option.Server, substring);
                                        }
                                        Player.ChatMessage($"Получен товар из магазина: <color=lime>\"{iteminfo["name"]}\"</color>");
                                        SendResult(new Dictionary<string, string>() { { "gived", "true" }, { "id", $"{iteminfo["id"]}" } });
                                        break;
                                    }

                                    if (iteminfo["type"].ToString() == "bp")
                                    {
                                        Item item = CreateByItemID(-1887162396);
                                        item.blueprintTarget = GetNewItemID(System.Convert.ToInt32(iteminfo["item_id"]));

                                        if (!Player.inventory.containerMain.IsFull() || !Player.inventory.containerBelt.IsFull())
                                        {
                                            SendGived(new Dictionary<string, string>() { { "gived", "true" }, { "id", $"{iteminfo["id"]}" } }, Player);


                                            Player.inventory.GiveItem(item, Player.inventory.containerMain);
                                            Player.ChatMessage($"Получен рецепт из магазина: <color=lime>\"{iteminfo["name"]}\"</color> в количестве <color=lime>{iteminfo["amount"]}</color> шт.");

                                        }
                                        else
                                            Player.ChatMessage($"В инвентаре недостаточно места для получения <color=lime>\"{iteminfo["name"]}\"</color>");

                                        break;
                                    }

                                    int ItemID = System.Convert.ToInt32(iteminfo["item_id"]);
                                    int Amount = System.Convert.ToInt32(iteminfo["amount"]);
                                    Item Item = CreateByItemID(ItemID, Amount);

                                    if (CanTake(Player, Item) >= Amount)
                                    {
                                        if (System.Convert.ToBoolean(Config["ITEMS.SPLIT"]))
                                        {
                                            List<Item> Items = SplitItem(Item);

                                            foreach (Item item in Items)
                                            {
                                                Player.inventory.GiveItem(CreateByItemID(Item.info.itemid, item.amount), Player.inventory.containerMain);
                                            }
                                        }
                                        else
                                        {
                                            Player.inventory.GiveItem(Item, Player.inventory.containerMain);
                                        }

                                        Player.ChatMessage($"Получен товар из магазина: <color=lime>\"{iteminfo["name"]}\"</color> в количестве <color=lime>{Amount}</color> шт.");
                                        SendGived(new Dictionary<string, string>() { { "gived", "true" }, { "id", $"{iteminfo["id"]}" } }, Player);
                                    }
                                    else
                                        Player.ChatMessage($"В инвентаре недостаточно места для получения <color=lime>\"{iteminfo["name"]}\"</color>");

                                }
                                #endregion
                                break;
                            case 104:
                                if (System.Convert.ToBoolean(Config["UI.ENABLED"]))
                                {
                                    #region[BackGround] Close
                                    UI.Add(new CuiButton
                                    {
                                        Button =
                                    {
                                        Command = $"ui.destroy {Player.userID}",
                                        Color = "1 0.1 0.1 0"
                                    },
                                        RectTransform =
                                    {
                                        AnchorMin = "0.0 0.0",
                                        AnchorMax = "0.999 0.999"
                                    },
                                        Text =
                                    {
                                        Color = "0.9 0.9 0.9 1",
                                        Text = "",
                                        FontSize = 25,
                                        Align = TextAnchor.MiddleCenter
                                    }
                                    }, "Hud", "ui.close.background");
                                    #endregion

                                    #region[Panel] Parent

                                    UI.Add(new CuiPanel()
                                    {
                                        Image =
                                    {
                                        Color = "0 0 0 0.95"
                                    },
                                        RectTransform =
                                    {
                                        AnchorMin = "0.20 0.20",
                                        AnchorMax = "0.80 0.85"
                                    },
                                        CursorEnabled = true
                                    }, "Hud", "ui.store");
                                    #endregion

                                    UI.Add(new CuiLabel()
                                    {
                                        RectTransform =
                                    {
                                        AnchorMin = "0.01 0.01",
                                        AnchorMax = "0.99 0.50"
                                    },
                                        Text =
                                    {
                                        Text = "Ваша корзина пуста!",
                                        Align = TextAnchor.UpperCenter,
                                        Color = "1 1 1 1",
                                        FontSize = 25
                                    }
                                    }, "ui.store", "ui.noitems");

                                    UI.Add(new CuiLabel()
                                    {
                                        RectTransform =
                                    {
                                        AnchorMin = "0.01 0.01",
                                        AnchorMax = "0.98 0.07"
                                    },
                                        Text =
                                    {
                                        Text = shopLink,
                                        Align = TextAnchor.UpperRight,
                                        Color = "1 1 1 1",
                                        FontSize = 25
                                    }
                                    }, "ui.store", "ui.link");

                                    #region[Button] Close
                                    UI.Add(new CuiButton
                                    {
                                        Button =
                                    {
                                        Command = $"ui.destroy {Player.userID}",
                                        Color = "0.1 0.1 0.1 0"
                                    },
                                        RectTransform =
                                    {
                                        AnchorMin = "0.960 0.94",
                                        AnchorMax = "0.999 0.998"
                                    },
                                        Text =
                                    {
                                        Color = "0.9 0.9 0.9 1",
                                        Text = "X",
                                        FontSize = 25,
                                        Align = TextAnchor.MiddleCenter
                                    }
                                    }, "ui.store", "ui.close");
                                    #endregion

                                    CuiHelper.AddUi(Player, UI);
                                    return;
                                }
                                Player.ChatMessage($"Ваша корзина пуста!");
                                break;
                        }
                    }
                    else
                        Debug.LogError("Api does not responded to a request");
                    break;
                case 404:
                    Debug.LogError("Response code: 404, please check your configurations");
                    break;
            }
        }
        #endregion

        #region [Method] SendResult - Send WebRequest result
        private void SendResult(Dictionary<string, string> Args) => SendRequest(Args);
        #endregion

        #region[Method] SendRequest - Send request to GameStore API
        private void SendRequest(Dictionary<string, string> Args, BasePlayer Player = null, bool exec = true)
        {
            string Request = $"{this.Request}&{string.Join("&", Args.Select(x => x.Key + "=" + x.Value).ToArray())}";
            webrequest.EnqueueGet(Request, (code, res) => { if (Player != null && exec) Executing(Player, res, code); }, this);
        }
        #endregion        

        #region[Method] SendGived - Send request about givint item to GameStore API
        private void SendGived(Dictionary<string, string> Args, BasePlayer Player = null)
        {
            string Request = $"{this.Request}&{string.Join("&", Args.Select(x => x.Key + "=" + x.Value).ToArray())}";
            webrequest.EnqueueGet(Request, (code, res) => { if (Player != null) TestRequestSent(Player, res, code, Args); }, this);
        }
        #endregion      

        #region[Method] TestRequestSent - Check send request
        private void TestRequestSent(BasePlayer Player, string response, int code, Dictionary<string, string> Args)
        {
            if (code == 200)
            {
                Dictionary<string, object> Resp = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, new KeyValuesConverter());
                if (Resp["result"].ToString() != "success")
                {
                    Debug.LogError("Api do not responded to request. Trying again (Player received items but it was not recorded)");
                    SendGived(Args, Player);
                }
            }
            else
            {
                Debug.LogError("Api do not responded to request. Trying again (Player received items but it was not recorded)");
                SendGived(Args, Player);
            }
        }
        #endregion

        #region[Method] cmdTakeItem - Using by UI.TakeAll
        private void cmdTakeItem(List<string> Args)
        {
            BasePlayer player = BasePlayer.FindByID(System.Convert.ToUInt64(Args[0]));
            if (Requests.ContainsKey(player.UserIDString + Args[2].ToString()))
            {
                if (Requests[player.UserIDString + Args[2].ToString()] + 10 > GetTimestampNow())
                {
                    player.ChatMessage($"Дождитесь завершения предыдущего запроса");
                    return;
                }
                else
                {
                    Requests.Remove(player.UserIDString + Args[2].ToString());
                }
            }

            Requests.Add(player.UserIDString + Args[2].ToString(), GetTimestampNow());
            webrequest.EnqueueGet($"{Request}&item=true&steam_id={player.UserIDString}&id={Args[2]}", (code, response) =>
            {
                switch (code)
                {
                    case 0:
                        Debug.LogError("Api does not responded to a request");
                        break;
                    case 200:
                        Dictionary<string, object> Response = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, new KeyValuesConverter());
                        if (!Response.ContainsKey("data"))
                        {
                            if (Requests.ContainsKey(player.UserIDString + Args[2].ToString()))
                                Requests.Remove(player.UserIDString + Args[2].ToString());
                            return;
                        }
                        Dictionary<string, object> data = Response["data"] as Dictionary<string, object>; ;
                        if (data["type"].ToString() == "item")
                        {
                            Item Item = CreateByItemID(System.Convert.ToInt32(data["item_id"]), System.Convert.ToInt32(data["amount"]));

                            if ((System.Convert.ToBoolean(Config["ITEMS.SPLIT"]) && CanTake(player, Item) >= Item.amount) || (!System.Convert.ToBoolean(Config["ITEMS.SPLIT"]) && (!player.inventory.containerMain.IsFull() || !player.inventory.containerBelt.IsFull())))
                            {
                                SendGived(new Dictionary<string, string>() { { "gived", "true" }, { "id", $"{Args[2]}" } }, player);
                                CuiHelper.DestroyUi(player, $"ui.block{Args[1]}");
                                CuiHelper.DestroyUi(player, $"ui.amount{Args[1]}");
                                CuiHelper.DestroyUi(player, $"ui.product{Args[1]}");
                                CuiHelper.DestroyUi(player, $"ui.command.take{Args[1]}");

                                if (System.Convert.ToBoolean(Config["ITEMS.SPLIT"]))
                                {
                                    List<Item> Items = SplitItem(Item);

                                    foreach (Item item in Items)
                                    {
                                        player.inventory.GiveItem(CreateByItemID(Item.info.itemid, item.amount), player.inventory.containerMain);
                                        player.ChatMessage($"Получен предмет из магазина: <color=lime>\"{data["name"]}\"</color> в количестве <color=lime>{Item.amount}</color> шт.");

                                    }
                                }
                                else
                                {
                                    player.inventory.GiveItem(Item, player.inventory.containerMain);
                                    player.ChatMessage($"Получен предмет из магазина: <color=lime>\"{data["name"]}\"</color> в количестве <color=lime>{data["amount"]}</color> шт.");
                                }
                                Log.Add($"|{player.UserIDString}|{DateTime.Now.ToString()}|{data["name"]}({data["amount"]})|{data["item_id"]}|");

                                if (Index[player] < 14)
                                    Index[player] -= 1;
                                Items[player] -= 1;

                                if (Items[player] < 1)
                                {
                                    Items.Remove(player);
                                    Index.Remove(player);
                                }
                            }
                            else
                            {
                                player.ChatMessage($"В инвентаре недостаточно места для получения <color=lime>\"{Item.info.displayName.english}\"</color>");
                            }
                        }
                        else if (data["type"].ToString() == "command")
                        {
                            string command = data["command"].ToString().Replace('\n', '|').ToLower().Trim('\"').Replace("%steamid%", player.UserIDString).Replace("%username%", player.displayName);
                            String[] CommandArray = command.Split('|');
                            foreach (var substring in CommandArray)
                            {
								ConsoleSystem.Run.Server.Normal(substring);
                             //   ConsoleSystem.Run(ConsoleSystem.Option.Server, substring);
                                Log.Add($"|{player.UserIDString}|{DateTime.Now.ToString()}|{data["name"]}({data["amount"]})|{substring}|");
                            }

                            player.ChatMessage($"Получен предмет из магазина: <color=lime>\"{data["name"]}\"</color>");
                            CuiHelper.DestroyUi(player, $"ui.block{Args[1]}");
                            CuiHelper.DestroyUi(player, $"ui.amount{Args[1]}");
                            CuiHelper.DestroyUi(player, $"ui.product{Args[1]}");
                            CuiHelper.DestroyUi(player, $"ui.command.take{Args[1]}");
                            SendGived(new Dictionary<string, string>() { { "gived", "true" }, { "id", $"{Args[2]}" } }, player);

                            if (Index[player] < 14)
                                Index[player] -= 1;
                            Items[player] -= 1;

                            if (Items[player] < 1)
                            {
                                Items.Remove(player);
                                Index.Remove(player);
                            }

                        }
                        SaveLog();
                        break;
                    case 404:
                        Debug.LogError("Response code: 404, please check your configurations");
                        break;
                    default:
                        Debug.LogError("Api does not responded to a request");
                        break;
                }
                Requests.Remove(player.UserIDString + Args[2].ToString());
            }, this);

        }
        #endregion

        #region[Method] DestroyUI
        private void DestroyUI(BasePlayer Player)
        {
            for (int i = 0; i < 15; i++)
            {
                CuiHelper.DestroyUi(Player, $"ui.background{i}");
                CuiHelper.DestroyUi(Player, $"ui.amount{i}");
                CuiHelper.DestroyUi(Player, $"ui.product{i}");
                CuiHelper.DestroyUi(Player, $"ui.command.take{i}");
            }

            Items.Remove(Player);
            Index.Remove(Player);
            CuiHelper.DestroyUi(Player, "ui.close");
            CuiHelper.DestroyUi(Player, "ui.close.background");
            CuiHelper.DestroyUi(Player, "ui.command.takeall");
            CuiHelper.DestroyUi(Player, "ui.back");
            CuiHelper.DestroyUi(Player, "ui.next");
            CuiHelper.DestroyUi(Player, "ui.store");
        }
        #endregion

        #region[ChatCommand] /store
        [ChatCommand("store")]
        private void cmdStore(BasePlayer Player, string command, string[] args)
        {
            SendRequest(new Dictionary<string, string>() { { "items", "true" }, { "steam_id", $"{Player.UserIDString}" } }, Player);
        }
        #endregion

        #region[ConsoleCommand] /store
        [ConsoleCommand("ui.store")]
        private void cmdUiStore(ConsoleSystem.Arg Args)
        {
            BasePlayer Player = BasePlayer.FindByID(System.Convert.ToUInt64(Args.Args[0]));
            DestroyUI(Player);
            SendRequest(new Dictionary<string, string>() { { "items", "true" }, { "steam_id", $"{Player.UserIDString}" } }, Player);
        }
        #endregion

        #region[ConsoleCommand] ui.takeall
        [ConsoleCommand("ui.takeall")]
        private void cmdTakeAll(ConsoleSystem.Arg Args)
        {
            BasePlayer Player = BasePlayer.FindByID(System.Convert.ToUInt64(Args.Args[0]));

            if (TakeAllBan.ContainsKey(Player.UserIDString.ToString()))
            {
                if (TakeAllBan[Player.UserIDString.ToString()] + 5 > GetTimestampNow())
                {
                    Player.ChatMessage($"Эту функцию можно использовать один раз в 5 сек.");
                    return;
                }
                else
                {
                    TakeAllBan.Remove(Player.UserIDString);
                }
            }

            TakeAllBan.Add(Player.UserIDString, GetTimestampNow());

            if (Player != null)
            {
                webrequest.EnqueueGet($"{Request}&items=true&steam_id={Player.UserIDString}", (code, response) =>
                {
                    switch (code)
                    {
                        case 0:
                            Debug.LogError("Api does not responded to a request");
                            break;
                        case 200:
                            Dictionary<string, object> Response = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, new KeyValuesConverter());
                            if (!Response.ContainsKey("data"))
                                return;
                            List<object> data = Response["data"] as List<object>;
                            if (data == null || Response == null)
                                return;

                            if (data.Count() > 14)
                            {
                                Player.ChatMessage($"Вы не можете забрать больше 14 предметов за раз.");
                                return;
                            }

                            if (data.Count() < 1)
                            {
                                return;
                            }
                            int i = data.Count() - 1;
                            foreach (object pair in data)
                            {
                                Dictionary<string, object> iteminfo = pair as Dictionary<string, object>;
                                List<string> Arguments = new List<string>() { { $"{Args.Args[0]}" }, { $"{i}" }, { $"{iteminfo["id"]}" } };
                                cmdTakeItem(Arguments);
                                i--;
                            }
                            if (Items[Player] < 1)
                            {
                                Items.Remove(Player);
                                Index.Remove(Player);
                            }
                            break;
                        case 404:
                            Debug.LogError("Response code: 404, please check your configurations");
                            break;
                    }
                }, this);
            }
        }
        #endregion

        #region[ConsoleCommand] ui.gives
        [ConsoleCommand("ui.gives")]
        private void cmdDestroyItem(ConsoleSystem.Arg Args)
        {
            BasePlayer player = BasePlayer.FindByID(System.Convert.ToUInt64(Args.Args[0]));

            if (Requests.ContainsKey(player.UserIDString + Args.Args[2].ToString()))
            {
                if (Requests[player.UserIDString + Args.Args[2].ToString()] + 10 > GetTimestampNow())
                {
                    player.ChatMessage($"Дождитесь завершения предыдущего запроса");
                    return;
                }
                else
                {
                    Requests.Remove(player.UserIDString + Args.Args[2].ToString());
                }
            }

            Requests.Add(player.UserIDString + Args.Args[2].ToString(), GetTimestampNow());
            webrequest.EnqueueGet($"{Request}&item=true&steam_id={player.UserIDString}&id={Args.Args[2]}", (code, response) =>
            {
                switch (code)
                {
                    case 0:
                        Debug.LogError("Api does not responded to a request");
                        break;
                    case 200:
                        Dictionary<string, object> Response = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, new KeyValuesConverter());
                        if (!Response.ContainsKey("data"))
                        {
                            player.ChatMessage($"Предмет не найден");
                            CuiHelper.DestroyUi(player, $"ui.block{Args.Args[1]}");
                            CuiHelper.DestroyUi(player, $"ui.amount{Args.Args[1]}");
                            CuiHelper.DestroyUi(player, $"ui.product{Args.Args[1]}");
                            CuiHelper.DestroyUi(player, $"ui.command.take{Args.Args[1]}");
                            Requests.Remove(player.UserIDString + Args.Args[2].ToString());
                            return;
                        }
                        Dictionary<string, object> data = Response["data"] as Dictionary<string, object>;
                        if (data["type"].ToString() == "item")
                        {
                            Item Item = CreateByItemID(System.Convert.ToInt32(data["item_id"]), System.Convert.ToInt32(data["amount"]));

                            if ((System.Convert.ToBoolean(Config["ITEMS.SPLIT"]) && CanTake(player, Item) >= Item.amount) || (!System.Convert.ToBoolean(Config["ITEMS.SPLIT"]) && (!player.inventory.containerMain.IsFull() || !player.inventory.containerBelt.IsFull())))
                            {

                                SendGived(new Dictionary<string, string>() { { "gived", "true" }, { "id", $"{Args.Args[2]}" } }, player);
                                CuiHelper.DestroyUi(player, $"ui.block{Args.Args[1]}");
                                CuiHelper.DestroyUi(player, $"ui.amount{Args.Args[1]}");
                                CuiHelper.DestroyUi(player, $"ui.product{Args.Args[1]}");
                                CuiHelper.DestroyUi(player, $"ui.command.take{Args.Args[1]}");

                                if (System.Convert.ToBoolean(Config["ITEMS.SPLIT"]))
                                {
                                    List<Item> Items = SplitItem(Item);

                                    foreach (Item item in Items)
                                    {
                                        player.inventory.GiveItem(CreateByItemID(Item.info.itemid, item.amount), player.inventory.containerMain);
                                        player.ChatMessage($"Получен предмет из магазина: <color=lime>\"{data["name"]}\"</color> в количестве <color=lime>{Item.amount}</color> шт.");
                                    }
                                }
                                else
                                {
                                    player.inventory.GiveItem(Item, player.inventory.containerMain);
                                    player.ChatMessage($"Получен предмет из магазина: <color=lime>\"{data["name"]}\"</color> в количестве <color=lime>{data["amount"]}</color> шт.");
                                }
                                Log.Add($"|{player.UserIDString}|{DateTime.Now.ToString()}|{data["name"]}({data["amount"]})|{data["item_id"]}|");
                                if (Index[player] < 14)
                                    Index[player] -= 1;
                                Items[player] -= 1;

                                if (Items[player] < 1)
                                {
                                    Items.Remove(player);
                                    Index.Remove(player);
                                }
                            }
                            else
                                player.ChatMessage($"В инвентаре недостаточно места для получения <color=lime>\"{Item.info.displayName.english}\"</color>");
                        }
                        else if (data["type"].ToString() == "command")
                        {
                            string command = data["command"].ToString().Replace('\n', '|').ToLower().Replace("%steamid%", player.UserIDString).Replace("%username%", player.displayName);
                            String[] CommandArray = command.Split('|');
                            foreach (var substring in CommandArray)
                            {
                                ConsoleSystem.Run.Server.Normal(substring);
                             //   ConsoleSystem.Run(ConsoleSystem.Option.Server, substring);
                                Log.Add($"|{player.UserIDString}|{DateTime.Now.ToString()}|{data["name"]}({data["amount"]})|{substring}|");
                            }

                            player.ChatMessage($"Получен предмет из магазина: <color=lime>\"{data["name"]}\"</color>");
                            CuiHelper.DestroyUi(player, $"ui.block{Args.Args[1]}");
                            CuiHelper.DestroyUi(player, $"ui.amount{Args.Args[1]}");
                            CuiHelper.DestroyUi(player, $"ui.product{Args.Args[1]}");
                            CuiHelper.DestroyUi(player, $"ui.command.take{Args.Args[1]}");
                            SendGived(new Dictionary<string, string>() { { "gived", "true" }, { "id", $"{Args.Args[2]}" } }, player);

                            if (Index[player] < 14)
                                Index[player] -= 1;
                            Items[player] -= 1;

                            if (Items[player] < 1)
                            {
                                Items.Remove(player);
                                Index.Remove(player);
                            }
                        }
                        else if (data["type"].ToString() == "bp")
                        {
                            Item item = CreateByItemID(-996920608);
                            item.blueprintTarget = GetNewItemID(System.Convert.ToInt32(data["item_id"]));

                            if (!player.inventory.containerMain.IsFull() || !player.inventory.containerBelt.IsFull())
                            {
                                SendGived(new Dictionary<string, string>() { { "gived", "true" }, { "id", $"{Args.Args[2]}" } }, player);
                                CuiHelper.DestroyUi(player, $"ui.block{Args.Args[1]}");
                                CuiHelper.DestroyUi(player, $"ui.bp{Args.Args[1]}");
                                CuiHelper.DestroyUi(player, $"ui.amount{Args.Args[1]}");
                                CuiHelper.DestroyUi(player, $"ui.product{Args.Args[1]}");
                                CuiHelper.DestroyUi(player, $"ui.command.take{Args.Args[1]}");


                                player.inventory.GiveItem(item, player.inventory.containerMain);
                                player.ChatMessage($"Получен рецепт из магазина: <color=lime>\"{data["name"]}\"</color> в количестве <color=lime>{data["amount"]}</color> шт.");

                            }
                            else
                                player.ChatMessage($"В инвентаре недостаточно места для получения <color=lime>\"{data["name"]}\"</color>");
                        }
                        SaveLog();
                        break;
                    case 404:
                        Debug.LogError("Response code: 404, please check your configurations");
                        break;
                }
                Requests.Remove(player.UserIDString + Args.Args[2].ToString());
            }, this);
        }
        #endregion

        #region[ConsoleCommand] ui.destroy
        [ConsoleCommand("ui.destroy")]
        private void cmdUi(ConsoleSystem.Arg Args)
        {
            BasePlayer Player = BasePlayer.FindByID(System.Convert.ToUInt64(Args.Args[0]));
            DestroyUI(Player);
        }
        #endregion

        #region[ConsoleCommand] ui.back
        [ConsoleCommand("ui.back")]
        private void cmdUiBack(ConsoleSystem.Arg Args)
        {
            BasePlayer Player = BasePlayer.FindByID(System.Convert.ToUInt64(Args.Args[0]));
            webrequest.EnqueueGet($"{Request}&items=true&steam_id={Player.UserIDString}", (code, response) =>
            {
                switch (code)
                {
                    case 0:
                        Debug.LogError("Api does not responded to a request");
                        break;
                    case 200:
                        Dictionary<string, object> Response = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, new KeyValuesConverter());
                        if (!Response.ContainsKey("data"))
                            return;
                        List<object> data = Response["data"] as List<object>;
                        List<object> items = new List<object>();
                        if (data == null || Response == null)
                            return;
                        if (Index[Player] <= 14)
                        {
                            return;
                        }
                        else
                        {

                            int Page = System.Convert.ToInt32(System.Math.Ceiling((System.Convert.ToSingle(Index[Player]) / 14f)));
                            Index[Player] = (Page - 1) * 14;

                            for (int i = 0; i != 14; i++)
                            {
                                CuiHelper.DestroyUi(Player, $"ui.block{i}");
                                CuiHelper.DestroyUi(Player, $"ui.amount{i}");
                                CuiHelper.DestroyUi(Player, $"ui.product{i}");
                                CuiHelper.DestroyUi(Player, $"ui.command.take{i}");
                                Index[Player] -= 1;
                            }

                            for (int i = 0; i != 14 && i < data.Count; i++)
                            {
                                items.Add(data[Index[Player] + i]);
                            }

                            if (Index[Player] < 1)
                                Index[Player] = 0;

                            CuiElementContainer UI = new CuiElementContainer();
                            for (int index = 0; index != 14 && index < items.Count; index++)
                            {
                                Dictionary<string, object> itemdata = items[index] as Dictionary<string, object>;

                                int ItemID = System.Convert.ToInt32(itemdata["item_id"]);
                                int Amount = System.Convert.ToInt32(itemdata["amount"]);
                                string Image = $"{itemdata["img"]}";

                                #region[Element] ImgBlock                                                                                      
                                UI.Add(new CuiElement()
                                {
                                    Name = $"ui.block{index}",
                                    Parent = $"ui.background{index}",
                                    Components =
                                {
                                    new CuiRawImageComponent
                                    {
                                        Sprite = "assets/content/textures/generic/fulltransparent.tga",
                                        Url = Image
                                    },
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = $"0.10 0.10",
                                        AnchorMax = $"0.90 0.90"
                                    },
                                    new CuiOutlineComponent
                                    {
                                        Distance = "1.0 1.0",
                                        Color = "0.0 0.0 0.0 1.0"
                                    }
                                }
                                });
                                #endregion

                                #region[Label] Amount
                                UI.Add(new CuiLabel()
                                {
                                    RectTransform =
                                {
                                    AnchorMin = $"0.0 0.0",
                                    AnchorMax = $"1.0 0.90"
                                },
                                    Text =
                                {
                                    Text = $"{Amount} шт. ",
                                    FontSize = 14,
                                    Align = TextAnchor.LowerRight,
                                    Color = "1 1 1 1"
                                }
                                }, $"ui.background{index}", $"ui.amount{index}");
                                #endregion

                                #region[ItemName] Product
                                UI.Add(new CuiLabel()
                                {
                                    RectTransform =
                                {
                                    AnchorMin = $"0.05 0.01",
                                    AnchorMax = $"0.99 0.99"
                                },
                                    Text =
                                {
                                    Text = $"{itemdata["name"]}",
                                    FontSize = 14,
                                    Align = TextAnchor.UpperLeft,
                                    Color = "1 1 1 1"
                                }
                                }, $"ui.background{index}", $"ui.product{index}");
                                #endregion

                                #region[Button] Take
                                UI.Add(new CuiButton
                                {
                                    Button =
                                {
                                    Command = $"ui.gives {Player.userID} {index} {itemdata["id"]}",
                                    Color = "0 0 0 0"
                                },
                                    RectTransform =
                                {
                                    AnchorMin = $"0.0 0.0",
                                    AnchorMax = $"1.0 1.0"
                                },
                                    Text =
                                {
                                    Text = ""
                                }
                                }, $"ui.background{index}", $"ui.command.take{index}");
                                #endregion

                                Index[Player] += 1;
                            }
                            CuiHelper.AddUi(Player, UI);
                        }
                        break;
                    case 404:
                        Debug.LogError("Response code: 404, please check your configurations");
                        break;
                }
            }, this);
        }
        #endregion

        #region[ConsoleCommand] ui.next
        [ConsoleCommand("ui.next")]
        private void cmdUiNext(ConsoleSystem.Arg Args)
        {
            BasePlayer Player = BasePlayer.FindByID(System.Convert.ToUInt64(Args.Args[0]));
            webrequest.EnqueueGet($"{Request}&items=true&steam_id={Player.UserIDString}", (code, response) =>
            {
                switch (code)
                {
                    case 0:
                        Debug.LogError("Api does not responded to a request");
                        break;
                    case 200:
                        Dictionary<string, object> Response = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, new KeyValuesConverter());
                        if (!Response.ContainsKey("data"))
                            return;
                        List<object> data = Response["data"] as List<object>;
                        if (data == null || Response == null)
                            return;

                        if (data.Count <= Index[Player])
                        {
                            return;
                        }
                        else
                        {
                            for (int i = 0; i != Index[Player]; i++)
                            {
                                data.RemoveAt(0);
                                CuiHelper.DestroyUi(Player, $"ui.block{i}");
                                CuiHelper.DestroyUi(Player, $"ui.amount{i}");
                                CuiHelper.DestroyUi(Player, $"ui.product{i}");
                                CuiHelper.DestroyUi(Player, $"ui.command.take{i}");
                            }
                            CuiElementContainer UI = new CuiElementContainer();
                            for (int index = 0; index != 14 && index < data.Count; index++)
                            {
                                Dictionary<string, object> itemdata = data[index] as Dictionary<string, object>;

                                int ItemID = System.Convert.ToInt32(itemdata["item_id"]);
                                int Amount = System.Convert.ToInt32(itemdata["amount"]);
                                string Image = $"{itemdata["img"]}";

                                #region[Element] ImgBlock                                                                                      
                                UI.Add(new CuiElement()
                                {
                                    Name = $"ui.block{index}",
                                    Parent = $"ui.background{index}",
                                    Components =
                                    {
                                        new CuiRawImageComponent
                                        {
                                            Sprite = "assets/content/textures/generic/fulltransparent.tga",
                                            Url = Image
                                        },
                                        new CuiRectTransformComponent
                                        {
                                            AnchorMin = $"0.10 0.10",
                                            AnchorMax = $"0.90 0.90"
                                        },
                                        new CuiOutlineComponent
                                        {
                                            Distance = "1.0 1.0",
                                            Color = "0.0 0.0 0.0 1.0"
                                        }
                                    }
                                });
                                #endregion

                                #region[Label] Amount
                                UI.Add(new CuiLabel()
                                {
                                    RectTransform =
                                    {
                                        AnchorMin = $"0.0 0.0",
                                        AnchorMax = $"1.0 0.90"
                                    },
                                    Text =
                                    {
                                        Text = $"{Amount} шт. ",
                                        FontSize = 14,
                                        Align = TextAnchor.LowerRight,
                                        Color = "1 1 1 1"
                                    }
                                }, $"ui.background{index}", $"ui.amount{index}");
                                #endregion

                                #region[ItemName] Product
                                UI.Add(new CuiLabel()
                                {
                                    RectTransform =
                                    {
                                        AnchorMin = $"0.05 0.01",
                                        AnchorMax = $"0.99 0.99"
                                    },
                                    Text =
                                    {
                                        Text = $"{itemdata["name"]}",
                                        FontSize = 14,
                                        Align = TextAnchor.UpperLeft,
                                        Color = "1 1 1 1"
                                    }
                                }, $"ui.background{index}", $"ui.product{index}");
                                #endregion

                                #region[Button] Take
                                UI.Add(new CuiButton
                                {
                                    Button =
                                    {
                                        Command = $"ui.gives {Player.userID} {index} {itemdata["id"]}",
                                        Color = "0 0 0 0"
                                    },
                                    RectTransform =
                                    {
                                        AnchorMin = $"0.0 0.0",
                                        AnchorMax = $"1.0 1.0"
                                    },
                                    Text =
                                    {
                                        Text = ""
                                    }
                                }, $"ui.background{index}", $"ui.command.take{index}");
                                #endregion

                                Index[Player] += 1;
                            }
                            CuiHelper.AddUi(Player, UI);
                        }
                        break;
                    case 404:
                        Debug.LogError("Response code: 404, please check your configurations");
                        break;
                }
            }, this);
        }
        #endregion

        #region[Helper] CanTake/SplitItem
        private int CanTake(BasePlayer Player, Item Item)
        {
            ItemContainer Container = Player.inventory.containerMain;
            int ItemID = Item.info.itemid;

            if (Item == null || (Item.MaxStackable() == 1 && (Container.IsFull() || (Container.capacity - Container.itemList.Count) < Item.amount)))
                return 0;
            else if (Item.MaxStackable() == 1 && !Container.IsFull())
                return 1 * Item.amount;

            return ((Container.FindItemsByItemID(ItemID).Count + (Container.capacity - Container.itemList.Count)) * Item.MaxStackable() - Container.GetAmount(ItemID, true));

        }
        private List<Item> SplitItem(Item Item)
        {
            List<Item> Items = new List<Item>() { Item };
            int MaxStackable = Item.MaxStackable();
            if (Item.amount > MaxStackable)
                for (int Amount = Items[0].amount; Items[0].amount > MaxStackable; Items[0].amount -= MaxStackable)
                    Items.Add(CreateByItemID(Item.info.itemid, MaxStackable));

            return Items;
        }
        #endregion

        #region[Helper] GetTimestampNow
        private long GetTimestampNow()
        {
            return System.Convert.ToInt32((DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds);
        }
        #endregion

        #region[Helper] DebugMessages
        public class Debug
        {
            public static void LogWarning(object message) => UnityEngine.Debug.LogWarning(CreateLog(message));
            public static void LogError(object message) => UnityEngine.Debug.LogError(CreateLog(message));
            private static string CreateLog(object message) => $"[{DateTime.Now.TimeOfDay.ToString().Split('.')[0]}] [GameStores]: {message}";
        }

        #endregion

        #region[Helper] SaveLog
        private void SaveLog()
        {
            Core.Interface.Oxide.DataFileSystem.WriteObject("GameStores.Log", Log);
        }

        #endregion

        #region[Helper] CreateByItemID
        private Item CreateByItemID(int itemID, int amount = 1)
        {
            string shortName = "";
            if (_OlditemIdShortnameConversions.TryGetValue(itemID, out shortName))
            {
                return ItemManager.CreateByName(shortName, amount);
            }
            else
            {
                return ItemManager.CreateByItemID(itemID, amount);
            }
        }
        #endregion

        #region[Helper] GetNewItemID
        private int GetNewItemID(int oldItemID)
        {
            string shortName = "";
            if (_OlditemIdShortnameConversions.TryGetValue(oldItemID, out shortName))
            {
                var itemDef = ItemManager.FindItemDefinition(shortName);
                if (itemDef != null)
                    return itemDef.itemid;
                else
                    return oldItemID;
            }
            else
                return oldItemID;
        }
        #endregion

        #region[Statistic] Methods for Top Players


        #region[HookMethod] OnEntityDeath
        [HookMethod("OnEntityDeath")]
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null || info.Initiator == null)
                return;

            if (System.Convert.ToBoolean(Config["TOP.USERS"]))
            {
                BaseEntity initiator = info.Initiator;

                if (entity as BasePlayer == null && initiator as BasePlayer == null)
                    return;

                Dictionary<string, object> args = new Dictionary<string, object>();

                if (initiator as BasePlayer != null)
                {
                    args["player_id"] = initiator.ToPlayer().UserIDString;
                }
                else if (initiator.PrefabName.Contains("agents"))
                {
                    args["player_id"] = "1";
                }

                if (entity as BasePlayer != null)
                {
                    args["victim_id"] = entity.ToPlayer().UserIDString;
                    args["type"] = entity.ToPlayer().IsSleeping() ? "sleeper" : "kill";
                }
                else if (entity.PrefabName.Contains("agents"))
                {
                    args["victim_id"] = "1";
                    args["type"] = "kill";
                }

                //Debug.LogWarning(entity.PrefabName);
                args["time"] = System.Convert.ToInt32((DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds).ToString();

                if (args.Count == 4)
                {
                    Stats.Add(args);
                }

            }

            if (Stats.Count >= 10)
                SendKillsInfo();
            //Debug.LogWarning(JsonConvert.SerializeObject(Stats));
        }
        #endregion

        #region[HookMethod] OnPlayerDisconnected
        [HookMethod("OnPlayerDisconnected")]
        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (System.Convert.ToBoolean(Config["TOP.USERS"]))
            {
                if (Config["SERVER.ID"].ToString() == "0")
                {
                    Debug.LogWarning("Need set SERVER.ID in configurations to send info for top players");
                }
                else
                {
                    Dictionary<string, object> args = new Dictionary<string, object>();

                    args["player_id"] = player.UserIDString;
                    args["played"] = player.net.connection.GetSecondsConnected().ToString();
                    args["username"] = player.displayName;
                    args["time"] = System.Convert.ToInt32((DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds).ToString();

                    Leaves.Add(args);

                    if (Leaves.Count >= 10)
                        SendLeavesInfo();
                }
            }
        }
        #endregion

        #region[HookMethod] OnServerSave
        [HookMethod("OnServerSave")]
        private void OnServerSave()
        {
            SendKillsInfo();
            SendLeavesInfo();
        }
        #endregion

        #region[HookMethod] SendKillsInfo
        private void SendKillsInfo()
        {
            if (System.Convert.ToBoolean(Config["TOP.USERS"]))
            {
                if (Config["SERVER.ID"].ToString() == "0")
                {
                    Debug.LogWarning("Need set SERVER.ID in configurations to send info for top players");
                }
                else
                {
                    for (int i = 0; i < (int)(Stats.Count / 20) + 1; i++)
                    {
                        if (Stats.Count > 0)
                        {
                            List<Dictionary<string, object>> Temp = new List<Dictionary<string, object>>();
                            int range = Stats.Count > 20 ? 20 : Stats.Count;
                            Temp = Stats.GetRange(0, range);
                            Stats.RemoveRange(0, range);

                            string request = $"{Request}&json=true&data={JsonConvert.SerializeObject(Temp)}";

                            //Debug.LogWarning(request);
                            webrequest.EnqueueGet(request, (code, res) =>
                            {
                                switch (code)
                                {
                                    case 0:
                                        Debug.LogError("Api does not responded to a request");
                                        break;
                                    case 200:
                                        break;
                                    case 404:
                                        Debug.LogError("Response code: 404, please check your configurations");
                                        break;
                                }
                            }, this);
                        }

                    }
                    Stats.Clear();
                }
            }
        }
        #endregion

        #region[HookMethod] SendLeavesInfo
        private void SendLeavesInfo()
        {
            if (System.Convert.ToBoolean(Config["TOP.USERS"]))
            {
                if (Config["SERVER.ID"].ToString() == "0")
                {
                    Debug.LogWarning("Need set SERVER.ID in configurations to send info for top players");
                }
                else
                {
                    for (int i = 0; i < (int)(Leaves.Count / 20) + 1; i++)
                    {
                        if (Leaves.Count > 0)
                        {
                            List<Dictionary<string, object>> Temp = new List<Dictionary<string, object>>();
                            int range = Leaves.Count > 20 ? 20 : Leaves.Count;
                            Temp = Leaves.GetRange(0, range);
                            Leaves.RemoveRange(0, range);


                            string request = $"{Request}&action=leaves&type=json&data={JsonConvert.SerializeObject(Temp)}";

                            //Debug.LogWarning(request);
                            webrequest.EnqueueGet(request, (code, res) =>
                            {
                                switch (code)
                                {
                                    case 0:
                                        Debug.LogError("Api does not responded to a request");
                                        break;
                                    case 200:
                                        break;
                                    case 404:
                                        Debug.LogError("Response code: 404, please check your configurations");
                                        break;
                                }
                            }, this);
                        }

                    }
                    Leaves.Clear();
                }
            }
        }
        #endregion

        #region[ConsoleCommand] gs.send
        [ConsoleCommand("gs.send")]
        private void sendInfo(ConsoleSystem.Arg Args)
        {
            switch (Args.Args[0])
            {
                case "kills":
                    SendKillsInfo();
                    Debug.LogWarning($"Sended info about kills");
                    break;
                case "leave":
                    SendLeavesInfo();
                    Debug.LogWarning($"Sended info about leaves");
                    break;
                default:
                    Debug.LogWarning("Command not found");
                    break;
            }
        }
        #endregion

        #region[ChatCommand] /gstop
        [ChatCommand("gstop")]
        private void cmdTop(BasePlayer player, string command, string[] args)
        {
            if (System.Convert.ToBoolean(Config["COMMAND.TOP"]))
            {
                string request = $"{Request}&top=true&steam_id={player.UserIDString}";
                webrequest.EnqueueGet(request, (code, res) =>
                {
                    switch (code)
                    {
                        case 0:
                            Debug.LogError("Api does not responded to a request");
                            break;
                        case 200:
                            List<object> data = JsonConvert.DeserializeObject<Dictionary<string, object>>(res, new KeyValuesConverter())["data"] as List<object>;
                            // Debug.LogWarning(res);
                            if (data.Count > 0)
                            {
                                player.ChatMessage($"Топ игроков: ");
                            }
                            else
                            {
                                player.ChatMessage($"Топ игроков пуст");
                            }
                            foreach (object user in data)
                            {
                                Dictionary<string, object> info = user as Dictionary<string, object>;
                                if (!System.Convert.ToBoolean(String.Compare(info["steam_id"].ToString(), player.UserIDString)))
                                {
                                    if (System.Convert.ToInt32(info["position"].ToString()) > 6)
                                        player.ChatMessage($"...");
                                    player.ChatMessage($"#{info["position"]} <color=lime>{info["username"]}</color> : Очков: {info["points"]}, Убийств: {info["kill"]}, Смертей: {info["death"]}");
                                }
                                else
                                {
                                    player.ChatMessage($"#{info["position"]} {info["username"]} : Очков: {info["points"]}, Убийств: {info["kill"]}, Смертей: {info["death"]}");
                                }

                            }
                            break;
                        case 404:
                            Debug.LogError("Response code: 404, please check your configurations");
                            break;
                    }
                }, this);
            }
            else { }
        }
        #endregion

        #endregion

    }
}