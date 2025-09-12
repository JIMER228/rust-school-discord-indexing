using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MinerGame", "XAVIER", "1.0.0")]
    public class MinerGame : RustPlugin
    {
        #region Classes

        private class PlayerInfo
        {
            internal class CurrentGameInfo
            {
                public List<int> IndexOpen = new List<int>();

                public float Betting;


                public int MinesMap;
            }
            
            public float Balance = 0f;

            public CurrentGameInfo CurrentGame = null;


            public bool isSound = true;
        }

        #endregion

        #region Variables

        [PluginReference] private Plugin ImageLibrary;
        
        private const string Layer = "UI_MinerMask";

        private Dictionary<ulong, PlayerInfo> playerInfo = new Dictionary<ulong, PlayerInfo>();

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            if (Interface.GetMod().DataFileSystem.ExistsDatafile(Name))
                playerInfo = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, PlayerInfo>>(Name);
            else playerInfo = new Dictionary<ulong, PlayerInfo>();


            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
            ImageLibrary.CallHook("AddImage", "https://i.imgur.com/TGoJEGZ.png", "Sound");
            ImageLibrary.CallHook("AddImage", "https://i.imgur.com/JeIYip3.png", "MapBetting");
            ImageLibrary.CallHook("AddImage", "https://i.imgur.com/hruWs9j.png", "land.mines");
        }
        
        
        private void Unload() => Interface.GetMod().DataFileSystem.WriteObject(Name, playerInfo);


        private void OnPlayerConnected(BasePlayer player) => playerInfo.TryAdd(player.userID, new PlayerInfo());

        #endregion


        #region Commands

        #region Chat

        [ChatCommand("miner")]
        private void CmdChatMain(BasePlayer player, string command, string[] args)
        {
            if (args.Length <= 0)
            {
                StartUI(player);
                return;
            }

            if (!player.IsAdmin) return;

            switch (args[0])
            {
                case "add":
                {
                    if (args.Length <= 2)
                    {
                        player.ChatMessage("/miner add <steamid> <balance>");
                        return;
                    }

                    var target = BasePlayer.Find(args[1]);

                    if (target == null)
                    {
                        player.ChatMessage($"{args[1]} player not found.");
                        return;
                    }

                    playerInfo[target.userID].Balance += float.Parse(args[2]);
                    
                    player.ChatMessage($"{target.displayName} give to balance");
                    
                    break;
                }
                case "remove":
                {
                    if (args.Length <= 2)
                    {
                        player.ChatMessage("/miner remove <steamid> <balance>");
                        return;
                    }

                    var target = BasePlayer.Find(args[1]);

                    if (target == null)
                    {
                        player.ChatMessage($"{args[1]} player not found.");
                        return;
                    }
                    
                    playerInfo[target.userID].Balance -= float.Parse(args[2]);
                    
                    player.ChatMessage($"{target.displayName} remove to balance");

                    
                   break;
                }
            }
        }

        #endregion

        #region Console

        [ConsoleCommand("miner.game")]
        private void CmdConsoleGive(ConsoleSystem.Arg args)
        {
            if (!args.IsAdmin) return;

            if (args.HasArgs())
            {
                switch (args.Args[0])
                {
                    case "add":
                    {
                        if (args.Args.Length <= 2)
                        {
                            PrintWarning("miner.game add <steamid> <balance>");
                            return;
                        }

                        var target = BasePlayer.Find(args.Args[1]);

                        if (target == null)
                        {
                            PrintWarning($"{args.Args[1]} player not found.");
                            return;
                        }

                        playerInfo[target.userID].Balance += float.Parse(args.Args[2]);
                    
                        PrintWarning($"{target.displayName} give to balance");
                        
                        break;
                    }
                    case "remove":
                    {
                        if (args.Args.Length <= 2)
                        {
                            PrintWarning("miner.game remove <steamid> <balance>");
                            return;
                        }

                        var target = BasePlayer.Find(args.Args[1]);

                        if (target == null)
                        {
                            PrintWarning($"{args.Args[1]} player not found.");
                            return;
                        }
                    
                        playerInfo[target.userID].Balance -= float.Parse(args.Args[2]);
                    
                        PrintWarning($"{target.displayName} remove to balance");

                        
                        break;
                    }
                }
            }
        }

        [ConsoleCommand("UI_MinerGame")]
        private void CmdConsoleMain(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();

            if (args.HasArgs())
            {
                switch (args.Args[0])
                {
                    case "StartBetting":
                    {
                        int Mines = int.Parse(args.Args[1]);


                        float totalCost = float.Parse(args.Args[2]);

                        if (totalCost <= 5)
                        {
                            player.ChatMessage("Минимальная сумма ставки - 5 рублей.");
                            return;
                        }
                        
                      

                        var data = playerInfo[player.userID];

                        if (data.Balance < totalCost)
                        {
                            player.ChatMessage("У вас недостаточно средств!");
                            return;
                        }
                        
                        if (data.CurrentGame != null)
                        {
                            player.ChatMessage("Произошла внутренняя ошибка. Обратитесь к администратору.");
                            return;
                        }


                        data.Balance -= totalCost;

                        data.CurrentGame = new PlayerInfo.CurrentGameInfo
                        {
                            Betting = totalCost,
                            IndexOpen = new List<int>(),
                            MinesMap = Mines
                        };
                        
                        CuiHelper.DestroyUi(player, ".Balance");
                        
                        CuiElementContainer container = new CuiElementContainer();

                            
                        container.Add(new CuiLabel
                        {
                            Text          = {Text      = $"Ваш баланс {data.Balance:0.00} \u20bd", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleRight},
                            RectTransform = {AnchorMin = $"0.4713542 0.7777777", AnchorMax = $"0.6921875 0.811111" },
                        }, Layer, ".Balance");
                        
                        CuiHelper.AddUi(player, container);
                        
                        CuiHelper.DestroyUi(player, ".GameOver");
                        
                        SendMenu(player, totalCost, Mines);

                        ViewButtonMapping(player);
                        
                        
                        break;
                    }
                    
                    case "Mines":
                    {
                        int Mines = int.Parse(args.Args[2]);


                        float totalCost = float.Parse(args.Args[1]);

                        
                        SendMenu(player, totalCost, Mines);

                        
                        break;
                    }

                    case "GivePrize":
                    {
                        
                        
                        var data = playerInfo[player.userID];
                        
                        if (data.CurrentGame == null) return;
                        
                        float TotalWin = GetTakeCost(data.CurrentGame.Betting, data.CurrentGame.IndexOpen.Count,
                            data.CurrentGame.MinesMap);

                        data.Balance += TotalWin;

                        CuiHelper.DestroyUi(player, ".Balance");
                        
                        CuiElementContainer container = new CuiElementContainer();
                        
                        container.Add(new CuiElement
                        {
                            Parent = Layer,
                            Name = ".GameOver",
                            Components =
                            {
                                new CuiImageComponent         {Color     = HexToCuiColor("#000000", 80), },
                                new CuiRectTransformComponent {AnchorMin = "0.4729167 0.412963", AnchorMax = "0.6994792 0.5240741" }
                            }
                        });
                        
                            
                        container.Add(new CuiLabel
                        {
                            Text          = {Text      = $"<color=#3FB5DA>ВЫ ВЫЙГРАЛИ</color>\nRUB {TotalWin}", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter},
                            RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1" },
                        }, ".GameOver");

                        
                        container.Add(new CuiLabel
                        {
                            Text          = {Text      = $"Ваш баланс {data.Balance:0.00} \u20bd", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleRight},
                            RectTransform = {AnchorMin = $"0.4713542 0.7777777", AnchorMax = $"0.6921875 0.811111" },
                        }, Layer, ".Balance");

                            
                            
                        data.CurrentGame = null;
                            
                        CuiHelper.AddUi(player, container);
                            
                        SendMenu(player, 25, 3);

                        ViewButtonMapping(player);
                        
                        break;
                    }
                    
                    case "MinesButton":
                    {
                        var data = playerInfo[player.userID];
                        
                        if (data.CurrentGame == null) return;
                        
                        int Index = int.Parse(args.Args[1]);
                        data.CurrentGame.IndexOpen.Add(Index);



                        CuiElementContainer container = new CuiElementContainer();
                        

                        float Chance = config.ChanceMined[data.CurrentGame.MinesMap];

                        CuiHelper.DestroyUi(player, Layer + Index + ".ButtonMines");
                        
                        if (UnityEngine.Random.Range(1, 100) > Chance)
                        {
                            container.Add(new CuiButton
                            {
                                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"},
                                Button = { Color = HexToCuiColor("#B1FF26", 30), Sprite = "assets/content/ui/ui.circlegradient.png" },
                                Text = { Text = "", Align = TextAnchor.MiddleCenter }
                            }, Layer + Index + ".B");
                            
                            container.Add(new CuiElement
                            {
                                Parent = Layer + Index + ".B",
                                Components =
                                {
                                    new CuiRawImageComponent      {Png       = (string)ImageLibrary.Call("GetImage", $"sulfur")}, 
                                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "20 20", OffsetMax = "-20 -20"}
                                }
                            });
                            
                        if (data.CurrentGame.IndexOpen.Count >= 25 - data.CurrentGame.MinesMap)
                        {
                            container.Add(new CuiElement
                            {
                                Parent = Layer,
                                Name = ".GameOver",
                                Components =
                                {
                                    new CuiImageComponent         {Color     = HexToCuiColor("#000000", 80), },
                                    new CuiRectTransformComponent {AnchorMin = "0.4729167 0.412963", AnchorMax = "0.6994792 0.5240741" }
                                }
                            });
                            
                            float TotalWin = GetTakeCost(data.CurrentGame.Betting, data.CurrentGame.IndexOpen.Count,
                                data.CurrentGame.MinesMap);
                            
                            container.Add(new CuiLabel
                            {
                                Text          = {Text      = $"<color=#3FB5DA>ВЫ ВЫЙГРАЛИ</color>\nRUB {TotalWin}", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter},
                                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1" },
                            }, ".GameOver");


                            data.Balance += TotalWin;

                            CuiHelper.DestroyUi(player, ".Balance");
                            
                            container.Add(new CuiLabel
                            {
                                Text          = {Text      = $"Ваш баланс {data.Balance:0.00} \u20bd", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleRight},
                                RectTransform = {AnchorMin = $"0.4713542 0.7777777", AnchorMax = $"0.6921875 0.811111" },
                            }, Layer, ".Balance");

                            
                            
                            data.CurrentGame = null;
                            
                            CuiHelper.AddUi(player, container);
                            
                            SendMenu(player, 25, 3);

                            if (data.isSound)
                            {
                                Effect effect = new Effect("ASSETS/BUNDLED/PREFABS/FX/INVITE_NOTICE.PREFAB".ToLower(), player, 0, new Vector3(), new Vector3());
                                EffectNetwork.Send(effect, player.Connection);
                            }
                            
                            return;
                        }



                            CuiHelper.DestroyUi(player, ".ButtonStart");
                            
                            container.Add(new CuiButton
                            {
                                RectTransform = {AnchorMin = "0.1953119 0.525", AnchorMax = "0.3796875 0.5620429"},
                                Button        = {Color     =  HexToCuiColor("#467EDF"), Command = $"UI_MinerGame GivePrize"},
                                Text          = {Text      = $"ЗАБРАТЬ {GetTakeCost(data.CurrentGame.Betting, data.CurrentGame.IndexOpen.Count, data.CurrentGame.MinesMap):00.00} \u20bd",Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#7ED3FC")} 
                            },  Layer, ".ButtonStart");

                            CuiHelper.AddUi(player, container);

                            if (data.isSound)
                            {
                                Effect effectSendSulfur = new Effect("assets/bundled/prefabs/fx/notice/item.select.fx.prefab", player, 0, new Vector3(), new Vector3());
                                EffectNetwork.Send(effectSendSulfur, player.Connection);
                            }
                            
                        }
                        else
                        {
                            container.Add(new CuiButton
                            {
                                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"},
                                Button = { Color = HexToCuiColor("#FF7964", 30), Sprite = "assets/content/ui/ui.circlegradient.png" },
                                Text = { Text = "", Align = TextAnchor.MiddleCenter }
                            }, Layer + Index + ".B");
                            
                            container.Add(new CuiElement
                            {
                                Parent = Layer + Index + ".B",
                                Components =
                                {
                                    new CuiRawImageComponent      {Png       = (string)ImageLibrary.Call("GetImage", $"land.mines")}, 
                                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "20 20", OffsetMax = "-20 -20"}
                                }
                            });
                            
                            container.Add(new CuiElement
                            {
                                Parent = Layer,
                                Name = ".GameOver",
                                Components =
                                {
                                    new CuiImageComponent         {Color     = HexToCuiColor("#000000", 80), },
                                    new CuiRectTransformComponent {AnchorMin = "0.4729167 0.412963", AnchorMax = "0.6994792 0.5240741" }
                                }
                            });
                            
                            
                            container.Add(new CuiLabel
                            {
                                Text          = {Text      = $"<color=#FF0000>ВЫ ПРОИГРАЛИ</color>", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter},
                                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1" },
                            }, ".GameOver");


                            CuiHelper.DestroyUi(player, ".Balance");
                            
                            container.Add(new CuiLabel
                            {
                                Text          = {Text      = $"Ваш баланс {data.Balance:0.00} \u20bd", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleRight},
                                RectTransform = {AnchorMin = $"0.4713542 0.7777777", AnchorMax = $"0.6921875 0.811111" },
                            }, Layer, ".Balance");


                            CuiHelper.AddUi(player, container);
                            
                            data.CurrentGame = null;
                            
                            SendMenu(player, 25, 3);

                            if (data.isSound)
                            {
                                Effect effect = new Effect("assets/bundled/prefabs/fx/weapons/landmine/landmine_explosion.prefab", player, 0, new Vector3(), new Vector3());
                                EffectNetwork.Send(effect, player.Connection);
                            }
                            
                        }
                        
                        break;
                    }
                    
                    case "Betting":
                    {
                        int Mines = int.Parse(args.Args[1]);


                        float totalCost = float.Parse(args.Args[2]);

                        var data = playerInfo[player.userID];

                        if (totalCost > data.Balance)
                            totalCost = data.Balance;
                        
                        SendMenu(player, totalCost, Mines);
                        
                        break;
                    }
                    case "wallet":
                    {
                        CuiElementContainer container = new CuiElementContainer();
                        
                        CuiHelper.DestroyUi(player, "Notification");
                        
                        container.Add(new CuiButton
                        {
                            RectTransform = {AnchorMin = "0.1854167 0.8268501", AnchorMax = "0.7791666 0.9462963"},
                            Button        = {Color     =  HexToCuiColor("#333B4F", 100)},
                            Text          = {Text      = "К сожалению такая функция не доступна. Вывести баланс можно через наш дискорд канал, написав в тикеты и предоставить свои реквизиты и сколько нужно поставить на вывод. Пополнение баланса через донат магазин, ссылка на магазин - <b>bummerrust.ru</b> вам нужно купить товар \"Внутриигровая валюта\" и после покупки прописать на сервере команду /store и забрать ее с корзины.\nТак-же не стоит забывать, что вы можете проиграть или выиграть. Успехов в твоем пути!",Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter}
                        },  Layer, "Notification");

                        CuiHelper.AddUi(player, container);


                        var data = playerInfo[player.userID];

                        if (data.isSound)
                        {
                            Effect effect = new Effect("ASSETS/BUNDLED/PREFABS/FX/INVITE_NOTICE.PREFAB".ToLower(), player, 0, new Vector3(), new Vector3());
                            EffectNetwork.Send(effect, player.Connection);
                        }


                        timer.Once(10, () => CuiHelper.DestroyUi(player, "Notification"));

                        break;
                    }
                    case "soundSettings":
                    {
                        var data = playerInfo[player.userID];

                        data.isSound = !data.isSound;

                        CuiElementContainer container = new CuiElementContainer();

                        CuiHelper.DestroyUi(player, "Sound");
                        
                        container.Add(new CuiButton
                        {
                            RectTransform = {AnchorMin = "0.7536458 0.7777777", AnchorMax = "0.7723946 0.811111"},
                            Button        = {Color     =  HexToCuiColor("#333B4F", 100), Command = $"UI_MinerGame soundSettings"},
                            Text          = {Text      = ""}
                        },  Layer, "Sound");
            
                        container.Add(new CuiElement
                        {
                            Parent = "Sound",
                            Components =
                            {
                                new CuiRawImageComponent      {Png       = (string)ImageLibrary.Call("GetImage", $"Sound"), Color = data.isSound ? HexToCuiColor("#A3B5E4") : HexToCuiColor("#FF7964")}, 
                                new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "8 8", OffsetMax = "-8 -8"}
                            }
                        });

                        CuiHelper.AddUi(player, container);
                        
                        break;
                        
                    }
                }
            }
        }

        #endregion

        #endregion
        
        #region Functions


        private float GetTakeCost(float Cost, int IndexOpen, int IndexMines)
        {
            var totalCostIndex = Cost * config.RatesMined[IndexMines][IndexOpen];

            return totalCostIndex;
        }

        public string HexToCuiColor(string HEX, float Alpha = 100)
        {
            if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

            var str = HEX.Trim('#');
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {Alpha / 100}";
        }

        #endregion

        #region UI

        private void StartUI(BasePlayer player)
        {
            var data = playerInfo[player.userID];
            
            CuiHelper.DestroyUi(player, Layer);
            
            var container = new CuiElementContainer();
			
			
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image         = {Color     = "0 0 0 0"}
            }, "OverlayNonScaled", Layer);
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "-100 -100", AnchorMax = "100 100", OffsetMax = "0 0" },
                Button        = {Color     = "0 0 0 0", Close = Layer},
                Text          = {Text      = "" }
            },  Layer);
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent         {Color     = HexToCuiColor("#202634", 80), },
                    new CuiRectTransformComponent {AnchorMin = "0.1854167 0.7666667", AnchorMax = "0.7791666 0.8222204" }
                }
            });
            
            container.Add(new CuiLabel
            {
                Text          = {Text      = "MINES", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleLeft},
                RectTransform = {AnchorMin = $"0.1947917 0.7666667", AnchorMax = $"0.459375 0.8222204" },
            }, Layer);
            
            container.Add(new CuiLabel
            {
                Text          = {Text      = $"Ваш баланс {data.Balance:0.00} \u20bd", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleRight},
                RectTransform = {AnchorMin = $"0.4713542 0.7777777", AnchorMax = $"0.6921875 0.811111" },
            }, Layer, ".Balance");
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.6979166 0.7777777", AnchorMax = "0.7499991 0.811111"},
                Button        = {Color     =  HexToCuiColor("#333B4F", 100), Command = $"UI_MinerGame wallet"},
                Text          = {Text      = "КОШЕЛЕК",Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter}
            },  Layer);
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.7536458 0.7777777", AnchorMax = "0.7723946 0.811111"},
                Button        = {Color     =  HexToCuiColor("#333B4F", 100), Command = $"UI_MinerGame soundSettings"},
                Text          = {Text      = ""}
            },  Layer, "Sound");
            
            
            container.Add(new CuiElement
            {
                Parent = "Sound",
                Components =
                {
                    new CuiRawImageComponent      {Png       = (string)ImageLibrary.Call("GetImage", $"Sound"), Color = data.isSound ? HexToCuiColor("#A3B5E4") : HexToCuiColor("#FF7964")}, 
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "8 8", OffsetMax = "-8 -8"}
                }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent         {Color     = HexToCuiColor("#202634", 100), },
                    new CuiRectTransformComponent {AnchorMin = "0.1854167 0.2824074", AnchorMax = "0.390625 0.7629629" }
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent         {Color     = HexToCuiColor("#202634", 100), },
                    new CuiRectTransformComponent {AnchorMin = "0.1854167 0.1740741", AnchorMax = "0.390625 0.2777778" }
                }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent         {Color     = HexToCuiColor("#000000", 25), },
                    new CuiRectTransformComponent {AnchorMin = "0.1958334 0.2", AnchorMax = "0.2859375 0.2481482" }
                }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent         {Color     = HexToCuiColor("#000000", 25), },
                    new CuiRectTransformComponent {AnchorMin = "0.2890627 0.2", AnchorMax = "0.3791652 0.2481482" }
                }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent         {Color     = HexToCuiColor("#333B4F", 25), },
                    new CuiRectTransformComponent {AnchorMin = "0.1953119 0.7046297", AnchorMax = "0.3791667 0.7416672" }
                }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiRawImageComponent      {Png       = (string)ImageLibrary.Call("GetImage", $"sulfur")}, 
                    new CuiRectTransformComponent {AnchorMin = "0.3635417 0.7157407", AnchorMax = "0.373958 0.7342593"}
                }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiRawImageComponent      {Png       = (string)ImageLibrary.Call("GetImage", $"sulfur")}, 
                    new CuiRectTransformComponent {AnchorMin = "0.2057292 0.2009259", AnchorMax = "0.23125 0.2472222"}
                }
            });
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiRawImageComponent      {Png       = (string)ImageLibrary.Call("GetImage", $"land.mines")}, 
                    new CuiRectTransformComponent {AnchorMin = "0.299479 0.2009259", AnchorMax = "0.3249991 0.2472222"}
                }
            });
            

            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiRawImageComponent      {Png       = (string)ImageLibrary.Call("GetImage", $"MapBetting")}, 
                    new CuiRectTransformComponent {AnchorMin = "0.3932276 0.1740741", AnchorMax = "0.7791666 0.7629629"}
                }
            });

            container.Add(new CuiElement
            {
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent         {Color     = HexToCuiColor("#202634", 66), },
                    new CuiRectTransformComponent {AnchorMin = "0.3932276 0.1740741", AnchorMax = "0.7791666 0.7629629" }
                }
            });
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.1953119 0.5898148", AnchorMax = "0.2583333 0.6268558"},
                Button        = {Color     =  HexToCuiColor("#333B4F", 25),},
                Text          = {Text      = "",}
            },  Layer);
            
            container.Add(new CuiLabel
            {
                Text          = {Text      = "Сумма ставки", Font = "robotocondensed-regular.ttf", FontSize = 13, Align = TextAnchor.MiddleLeft},
                RectTransform = {AnchorMin = $"0.196875 0.7416667", AnchorMax = $"0.3791667 0.7629629" },
            }, Layer);
            
            
            container.Add(new CuiLabel
            {
                Text          = {Text      = "Количество мин", Font = "robotocondensed-regular.ttf", FontSize = 13, Align = TextAnchor.MiddleLeft},
                RectTransform = {AnchorMin = $"0.196875 0.6268553", AnchorMax = $"0.3791667 0.6481515" },
            }, Layer);
            

            CuiHelper.AddUi(player, container);

            SendMenu(player, 25, 3);
            ViewButtonMapping(player);
        }


        private void SendMenu(BasePlayer player, float Cost, int Mines)
        {
            CuiElementContainer container = new CuiElementContainer();
            
            var data = playerInfo[player.userID];

            CuiHelper.DestroyUi(player, ".50.0");
            
            CuiHelper.DestroyUi(player, ".1/2");
            
            CuiHelper.DestroyUi(player, ".X2");
            
            CuiHelper.DestroyUi(player, ".MAX");

            CuiHelper.DestroyUi(player, ".ButtonStart");
            
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.1953119 0.6657407", AnchorMax = "0.2390629 0.6990759"},
                Button        = {Color     =  HexToCuiColor("#333B4F", 25), Command = $"UI_MinerGame Betting {Mines} {Cost + 50}"},
                Text          = {Text      = "50.0",Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter}
            },  Layer, ".50.0");
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.2421875 0.6657407", AnchorMax = "0.2859375 0.6990759"},
                Button        = {Color     =  HexToCuiColor("#333B4F", 25), Command = Cost > 0 ? $"UI_MinerGame Betting {Mines} {Cost / 2}" : ""},
                Text          = {Text      = "1/2",Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter}
            },  Layer, ".1/2");
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.289062 0.6657407", AnchorMax = "0.3328118 0.6990759"},
                Button        = {Color     =  HexToCuiColor("#333B4F", 25), Command = Cost > 0 ? $"UI_MinerGame Betting {Mines} {Cost * 2}": ""},
                Text          = {Text      = "X2",Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter}
            },  Layer, ".X2");
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.3359363 0.6657407", AnchorMax = "0.3796861 0.6990759"},
                Button        = {Color     =  HexToCuiColor("#333B4F", 25), Command = $"UI_MinerGame Betting {Mines} {data.Balance}"},
                Text          = {Text      = "MAX",Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter}
            },  Layer, ".MAX");


            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.1953119 0.525", AnchorMax = "0.3796875 0.5620429"},
                Button        = {Color     =  data.CurrentGame != null ? HexToCuiColor("#467EDF") : Cost <= 0 ? HexToCuiColor("#264478") : HexToCuiColor("#467EDF"), Command = data.CurrentGame != null ? "UI_MinerGame GivePrize" :  Cost > 0 ? $"UI_MinerGame StartBetting {Mines} {Cost}": ""},
                Text          = {Text      = data.CurrentGame != null ? $"ЗАБРАТЬ {GetTakeCost(data.CurrentGame.Betting, data.CurrentGame.IndexOpen.Count,  data.CurrentGame.MinesMap):0.00} \u20bd" : "НАЧАТЬ ИГРУ",Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color =data.CurrentGame != null ?  HexToCuiColor("#7ED3FC") :  Cost <= 0 ? HexToCuiColor("#375FA4") : HexToCuiColor("#7ED3FC")}
            },  Layer, ".ButtonStart");


            CuiHelper.DestroyUi(player, ".SulfurCount");
            
            container.Add(new CuiLabel
            {
                Text          = {Text      = (25 - Mines).ToString(), Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#DDF2B8")},
                RectTransform = {AnchorMin = $"0.2317708 0.2009259", AnchorMax = $"0.2859372 0.2472222" },
            }, Layer,".SulfurCount");
            
            CuiHelper.DestroyUi(player, ".MinesCount");
            
            container.Add(new CuiLabel
            {
                Text          = {Text      = Mines.ToString(), Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#965A50")},
                RectTransform = {AnchorMin = $"0.3249991 0.2009259", AnchorMax = $"0.3791649 0.2472222" },
            }, Layer,".MinesCount");

            CuiHelper.DestroyUi(player, ".Input");
            
            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = ".Input",
                Components =
                {
                    new CuiInputFieldComponent    {FontSize  = 14,Align = TextAnchor.MiddleLeft, Command = $"UI_MinerGame Betting {Mines} ", CharsLimit = 50,Text = Cost.ToString(), Font = "robotocondensed-regular.ttf"},
                    new CuiRectTransformComponent {AnchorMin = "0.2015625 0.7046297", AnchorMax = "0.3536458 0.7416672"}
                }
            });
            
            CuiHelper.DestroyUi(player, ".Back");
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.1953119 0.5898148", AnchorMax = "0.2135417 0.6268558"},
                Button        = {Color     =  "0 0 0 0", Command = Mines > 1 ? $"UI_MinerGame Mines {Cost} {Mines - 1}" : ""},
                Text          = {Text      = "-",Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter}
            },  Layer, ".Back");
            
            CuiHelper.DestroyUi(player, ".Next");
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.2401042 0.5898148", AnchorMax = "0.2583337 0.6268558"},
                Button        = {Color     =  "0 0 0 0", Command = Mines < 24 ? $"UI_MinerGame Mines {Cost} {Mines + 1}" : ""},
                Text          = {Text      = "+",Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter}
            },  Layer, ".Next");
            
            CuiHelper.DestroyUi(player, ".MinesCountButton");
            
            container.Add(new CuiLabel
            {
                Text          = {Text      = Mines.ToString(), Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter},
                RectTransform = {AnchorMin = $"0.2135413 0.5898148", AnchorMax = $"0.2401042 0.6268558" },
            }, Layer,".MinesCountButton");
            
            CuiHelper.DestroyUi(player, ".2");
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.2614583 0.5898148", AnchorMax = "0.2822914 0.6268558"},
                Button        = {Color     =  HexToCuiColor("#333B4F", 25), Command = $"UI_MinerGame Mines {Cost} 2"},
                Text          = {Text      = "2",Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter}
            },  Layer, ".2");
            
            CuiHelper.DestroyUi(player, ".4");
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.2854163 0.5898148", AnchorMax = "0.3062494 0.6268558"},
                Button        = {Color     =  HexToCuiColor("#333B4F", 25), Command = $"UI_MinerGame Mines {Cost} 4"},
                Text          = {Text      = "4",Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter}
            },  Layer, ".4");
            
            CuiHelper.DestroyUi(player, ".8");
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.3098951 0.5898148", AnchorMax = "0.3307282 0.6268558"},
                Button        = {Color     =  HexToCuiColor("#333B4F", 25), Command = $"UI_MinerGame Mines {Cost} 8"},
                Text          = {Text      = "8",Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter}
            },  Layer, ".8");
            
            CuiHelper.DestroyUi(player, ".12");
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.3333322 0.5898148", AnchorMax = "0.3541653 0.6268558"},
                Button        = {Color     =  HexToCuiColor("#333B4F", 25), Command = $"UI_MinerGame Mines {Cost} 12"},
                Text          = {Text      = "12",Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter}
            },  Layer, ".12");
            
            CuiHelper.DestroyUi(player, ".24");
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.3572902 0.5898148", AnchorMax = "0.3781233 0.6268558"},
                Button        = {Color     =  HexToCuiColor("#333B4F", 25), Command = $"UI_MinerGame Mines {Cost} 24"},
                Text          = {Text      = "24",Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter}
            },  Layer, ".24");


            CuiHelper.AddUi(player, container);
        }


        private void ViewButtonMapping(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();

            var data = playerInfo[player.userID];
            
            bool isGame = data.CurrentGame != null;
            
            for (int i = 0; i < 25; i++)
            {
                CuiHelper.DestroyUi(player, Layer + i + ".B");
                
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Name   = Layer + i + ".B",
                    Components =
                    {
                        new CuiImageComponent         {FadeIn = (float) i / 25, Color     = HexToCuiColor("#202634", 70), },
                        new CuiRectTransformComponent 
                        {                            
                            AnchorMin =
                                $"{0.3963542 + i * 0.0765 - Math.Floor((float)i / 5) * 5 * 0.0765} {0.6462963 - Math.Floor((float)i / 5) * 0.117}",
                            AnchorMax =
                                $"{0.4697917 + i * 0.0765 - Math.Floor((float)i / 5) * 5 * 0.0765} {0.7574074 - Math.Floor((float)i / 5) * 0.117}",
                            
                        },
                    }
                });

                if (isGame && data.CurrentGame.IndexOpen.Contains(i))
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "12 12", OffsetMax = "-12 -12"},
                        Button = { Color = HexToCuiColor("#B1FF26", 50), Sprite = "assets/content/ui/ui.circlegradient.png" },
                        Text = { Text = "", Align = TextAnchor.MiddleCenter }
                    }, Layer + i + ".B");
                            
                    container.Add(new CuiElement
                    {
                        Parent = Layer + i + ".B",
                        Components =
                        {
                            new CuiRawImageComponent      {Png       = (string)ImageLibrary.Call("GetImage", $"sulfur")}, 
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "20 20", OffsetMax = "-20 -20"}
                        }
                    });
                }
                else
                {
                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin =
                                $"0 0",
                            AnchorMax =
                                $"1 1",
                            OffsetMax = "0 0"
                        },
                        Button =
                        {
                            Color    =  "0 0 0 0",
                            Command  =  $"UI_MinerGame MinesButton {i}", 
                        },
                        Text =
                        {
                            Text = $""
                        }
                    }, Layer + i + ".B", Layer + i + ".ButtonMines");   
                }
            }
            
            CuiHelper.AddUi(player, container);

        }

        #endregion
   
        #region Configuration

        private Configuration config;
        

        private class Configuration
        {
            [JsonProperty("Коэфциент выдаваемого X в зависимости от количество мин на карте")]
            public Dictionary<int, List<float>> RatesMined = new Dictionary<int, List<float>>();


            [JsonProperty("Шанс выпадения мины в зависимости от количество мин на карте")]
            public Dictionary<int, float> ChanceMined = new Dictionary<int, float>();

            public static Configuration GetNewConf()
            {
                return new Configuration
                {
                    RatesMined = new Dictionary<int, List<float>>
                    {
                        [1] = new List<float>
                        {
                                0.02f,
                                0.03f,
                                0.04f,
                                0.05f,
                                0.06f,
                                0.07f,
                                0.08f,
                                0.09f,
                                0.10f,
                                0.11f,
                                0.12f,
                                0.13f,
                                0.14f,
                                0.15f,
                                0.16f,
                                0.17f,
                                0.18f,
                                0.19f,
                                0.20f,
                                0.21f,
                                0.22f,
                                0.23f,
                                0.24f,
                                0.25f,
                        },
                        [2] = new List<float>
                        {
                            0.03f,
                            0.04f,
                            0.05f,
                            0.06f,
                            0.07f,
                            0.08f,
                            0.09f,
                            0.10f,
                            0.11f,
                            0.12f,
                            0.13f,
                            0.14f,
                            0.15f,
                            0.16f,
                            0.17f,
                            0.18f,
                            0.19f,
                            0.20f,
                            0.21f,
                            0.22f,
                            0.23f,
                            0.24f,
                            0.25f,
                        },
                        [3] = new List<float>
                        {
                            0.04f,
                            0.05f,
                            0.06f,
                            0.07f,
                            0.08f,
                            0.09f,
                            0.10f,
                            0.11f,
                            0.12f,
                            0.13f,
                            0.14f,
                            0.15f,
                            0.16f,
                            0.17f,
                            0.18f,
                            0.19f,
                            0.20f,
                            0.21f,
                            0.22f,
                            0.23f,
                            0.24f,
                            0.25f,
                        },
                        [4] = new List<float>
                        {
                            0.05f,
                            0.06f,
                            0.07f,
                            0.08f,
                            0.09f,
                            0.10f,
                            0.11f,
                            0.12f,
                            0.13f,
                            0.14f,
                            0.15f,
                            0.16f,
                            0.17f,
                            0.18f,
                            0.19f,
                            0.20f,
                            0.21f,
                            0.22f,
                            0.23f,
                            0.24f,
                            0.25f,
                        },
                        [5] = new List<float>
                        {
                            0.06f,
                            0.07f,
                            0.08f,
                            0.09f,
                            0.10f,
                            0.11f,
                            0.12f,
                            0.13f,
                            0.14f,
                            0.15f,
                            0.16f,
                            0.17f,
                            0.18f,
                            0.19f,
                            0.20f,
                            0.21f,
                            0.22f,
                            0.23f,
                            0.24f,
                            0.25f,
                        },
                        [6] = new List<float>
                        {
                            0.07f,
                            0.08f,
                            0.09f,
                            0.10f,
                            0.11f,
                            0.12f,
                            0.13f,
                            0.14f,
                            0.15f,
                            0.16f,
                            0.17f,
                            0.18f,
                            0.19f,
                            0.20f,
                            0.21f,
                            0.22f,
                            0.23f,
                            0.24f,
                            0.25f,
                        },
                        [7] = new List<float>
                        {
                                0.08f,
                                0.09f,
                                0.10f,
                                0.11f,
                                0.12f,
                                0.13f,
                                0.14f,
                                0.15f,
                                0.16f,
                                0.17f,
                                0.18f,
                                0.19f,
                                0.20f,
                                0.21f,
                                0.22f,
                                0.23f,
                                0.24f,
                                0.25f,
                        },
                        [8] = new List<float>
                        {
                                0.09f,
                                0.10f,
                                0.11f,
                                0.12f,
                                0.13f,
                                0.14f,
                                0.15f,
                                0.16f,
                                0.17f,
                                0.18f,
                                0.19f,
                                0.20f,
                                0.21f,
                                0.22f,
                                0.23f,
                                0.24f,
                                0.25f,
                        },
                        [9] = new List<float>
                        {
                            0.10f,
                            0.11f,
                            0.12f,
                            0.13f,
                            0.14f,
                            0.15f,
                            0.16f,
                            0.17f,
                            0.18f,
                            0.19f,
                            0.20f,
                            0.21f,
                            0.22f,
                            0.23f,
                            0.24f,
                            0.25f,
                        },
                        [10] = new List<float>
                        {
                            0.11f,
                            0.12f,
                            0.13f,
                            0.14f,
                            0.15f,
                            0.16f,
                            0.17f,
                            0.18f,
                            0.19f,
                            0.20f,
                            0.21f,
                            0.22f,
                            0.23f,
                            0.24f,
                            0.25f,
                        },
                        [11] = new List<float>
                        {
                            0.12f,
                            0.13f,
                            0.14f,
                            0.15f,
                            0.16f,
                            0.17f,
                            0.18f,
                            0.19f,
                            0.20f,
                            0.21f,
                            0.22f,
                            0.23f,
                            0.24f,
                            0.25f,
                        },
                        [12] = new List<float>
                        {
                            0.13f,
                            0.14f,
                            0.15f,
                            0.16f,
                            0.17f,
                            0.18f,
                            0.19f,
                            0.20f,
                            0.21f,
                            0.22f,
                            0.23f,
                            0.24f,
                            0.25f,
                        },
                        [13] = new List<float>
                        {
                            0.14f,
                            0.15f,
                            0.16f,
                            0.17f,
                            0.18f,
                            0.19f,
                            0.20f,
                            0.21f,
                            0.22f,
                            0.23f,
                            0.24f,
                            0.25f,
                        },
                        [14] = new List<float>
                        {
                            0.15f,
                            0.16f,
                            0.17f,
                            0.18f,
                            0.19f,
                            0.20f,
                            0.21f,
                            0.22f,
                            0.23f,
                            0.24f,
                            0.25f,
                        },
                        [15] = new List<float>
                        {
                            0.16f,
                            0.17f,
                            0.18f,
                            0.19f,
                            0.20f,
                            0.21f,
                            0.22f,
                            0.23f,
                            0.24f,
                            0.25f,
                        },
                        [16] = new List<float>
                        {
                            0.17f,
                            0.18f,
                            0.19f,
                            0.20f,
                            0.21f,
                            0.22f,
                            0.23f,
                            0.24f,
                            0.25f,
                        },
                        [17] = new List<float>
                        {
                            0.18f,
                            0.19f,
                            0.20f,
                            0.21f,
                            0.22f,
                            0.23f,
                            0.24f,
                            0.25f,
                        },
                        [18] = new List<float>
                        {
                            0.19f,
                            0.20f,
                            0.21f,
                            0.22f,
                            0.23f,
                            0.24f,
                            0.25f,
                        },
                        [19] = new List<float>
                        {
                            0.20f,
                            0.21f,
                            0.22f,
                            0.23f,
                            0.24f,
                            0.25f,
                        },
                        [20] = new List<float>
                        {
                            0.21f,
                            0.22f,
                            0.23f,
                            0.24f,
                            0.25f,
                        },
                        [21] = new List<float>
                        {
                            0.21f,
                            0.22f,
                            0.23f,
                        },
                        [22] = new List<float>
                        {
                            0.22f,
                            0.23f,
                            0.24f,
                        },
                        [23] = new List<float>
                        {
                            0.23f,
                            0.24f,
                        },
                        [24] = new List<float>
                        {
                            0f,
                            15f,
                        },
                    },
                    ChanceMined = new Dictionary<int, float>
                    {
                        [1]  = 4f,
                        [2]  = 8f,
                        [3]  = 12f,
                        [4]  = 16f,
                        [5]  = 20f,
                        [6]  = 24f,
                        [7]  = 28f,
                        [8]  = 32f,
                        [9]  = 36f,
                        [10] = 40f,
                        [11] = 44f,
                        [12] = 48f,
                        [13] = 52f,
                        [14] = 56f,
                        [15] = 60f,
                        [16] = 64f,
                        [17] = 68f,
                        [18] = 72f,
                        [19] = 76f,
                        [20] = 80f,
                        [21] = 84f,
                        [22] = 88f,
                        [23] = 92f,
                        [24] = 0f,
                    },
                    
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
            }
            catch
            {
                LoadDefaultConfig();
            }
            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConf();
        protected override void SaveConfig()        => Config.WriteObject(config);

        

        #endregion
    }
}