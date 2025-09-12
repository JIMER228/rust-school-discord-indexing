using System.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Cases", "Drop Dead", "1.0.1")]
    public class Cases : RustPlugin
    {
        [PluginReference] private Plugin ImageLibrary;
        private static Cases _ins;
        bool addday = false;
        string Layer = "Cases.Main";
        public Dictionary<ulong, int> time = new Dictionary<ulong, int>();
        public Dictionary<ulong, List<Inventory>> inventory = new Dictionary<ulong, List<Inventory>>();
        public List<int> day = new List<int>();
        public Dictionary<ulong, int> taked = new Dictionary<ulong, int>();
        public List<ulong> openedui = new List<ulong>();


        string MainIMG = "https://cdn.discordapp.com/attachments/914415456364941322/988122137250369616/case.png";
        string InventoryIMG = "https://i.imgur.com/hgjxE5o.png";

        public class Inventory
        {
            public bool command;
            public string strcommand;
            public string shortname;
            public int amount;

        }

        public class random
        {
            [JsonProperty("Шанс выпадения")]
            public int chance;
            [JsonProperty("Минимальное количество")]
            public int min;
            [JsonProperty("Максимальное количество")]
            public int max;
        }

        public class chance
        {
            [JsonProperty("Шанс выпадения")]
            public int chances;
            [JsonProperty("Картинка")]
            public string image;
        }

        public class Case
        {
            [JsonProperty("Сколько времени должен отыграть игрок для открытия кейса (в секундах)")]
            public int time = 300;

            [JsonProperty("Использовать выпадение предметов?")]
            public bool items = true;

            [JsonProperty("Использовать выдачу команды?")]
            public bool command = false;

            [JsonProperty("Команды для выполнения (%steamid% заменяется на айди игрока)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, chance> strcommands = new Dictionary<string, chance>
            {
                ["say %steamid%"] = new chance { image = "https://i.imgur.com/DXB7GRi.png", chances = 100 },
                ["example"] = new chance { image = "https://i.imgur.com/sLZm4on.png", chances = 100 },
            };

            [JsonProperty("Предметы которые могут выпасть при открытии", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, random> itemsdrop = new Dictionary<string, random>
            {
                ["sulfur"] = new random { chance = 100, min = 10, max = 50}, 
                ["metal.fragments"] = new random { chance = 50, min = 50, max = 150},
            };
        }

        private PluginConfig cfg;

        public class PluginConfig
        {
            [JsonProperty("Настройки кейсов")]
            public MainSettings settings = new MainSettings();

            public class MainSettings
            {
                [JsonProperty("День кейса, её настройки", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public Dictionary<string, Case> cases = new Dictionary<string, Case>()
                {
                    ["1"] = new Case(),
                    ["2"] = new Case(),
                    ["3"] = new Case(),
                    ["4"] = new Case(),
                    ["5"] = new Case(),
                };
            }
        }

        private void Init()
        {
            cfg = Config.ReadObject<PluginConfig>();
            Config.WriteObject(cfg);
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new PluginConfig(), true);
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Title}/Cooldown", time);
            Interface.Oxide.DataFileSystem.WriteObject($"{Title}/Day", day);
            Interface.Oxide.DataFileSystem.WriteObject($"{Title}/Taked", taked);
            Interface.Oxide.DataFileSystem.WriteObject($"{Title}/Inventory", inventory);
        }

        private void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Title}/Cooldown"))
                time = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, int>>($"{Title}/Cooldown");

            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Title}/Day"))
                day = Interface.Oxide.DataFileSystem.ReadObject<List<int>>($"{Title}/Day");

            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Title}/Taked"))
                taked = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, int>>($"{Title}/Taked");

            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Title}/Inventory"))
                inventory = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<Inventory>>>($"{Title}/Inventory");
        }

        void OnServerInitialized()
        {
            _ins = this;
            if (!plugins.Exists("ImageLibrary"))
            {
                PrintError("ImageLibrary not found. Install it and reload plugin!");
                return;
            }

            LoadData();
            if (day == null || day.Count < 1) day.Add(1);
            if (GetWipeDay() > 4) day[0] = 1;

            if (!IMGLibrary.HasImage(MainIMG, 0)) IMGLibrary.AddImage(MainIMG, MainIMG, 0);
            if (!IMGLibrary.HasImage(InventoryIMG, 0)) IMGLibrary.AddImage(InventoryIMG, InventoryIMG, 0);
            foreach (var item in cfg.settings.cases.Values) 
            {
                foreach (var cmd in item.strcommands) 
                {
                    if (!string.IsNullOrEmpty(cmd.Value.image) && !IMGLibrary.HasImage(cmd.Key, 0)) IMGLibrary.AddImage(cmd.Value.image, cmd.Key, 0);
                }
                foreach (var cmd in item.itemsdrop) 
                {
                    if (!string.IsNullOrEmpty(cmd.Key) && !IMGLibrary.HasImage(cmd.Key, 0)) IMGLibrary.AddImage("https://rustlabs.com/img/items180/" + cmd.Key + ".png", cmd.Key, 0);
                }
            }


            if (BasePlayer.activePlayerList.Count > 0) foreach (var player in BasePlayer.activePlayerList) OnPlayerConnected(player);
            InvokeHandler.Instance.InvokeRepeating(UpdateTime, 60f, 60f);
            InvokeHandler.Instance.InvokeRepeating(UpdateUI, 1f, 1f);
            timer.Every(120f, () =>
            {
                SaveData();
            });
        }

        void Unload()
        {
            InvokeHandler.Instance.CancelInvoke(UpdateTime);
            InvokeHandler.Instance.CancelInvoke(UpdateUI);
            foreach (var player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, Layer);
            SaveData();
            _ins = null;
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player));
                return;
            }
            if (!time.ContainsKey(player.userID)) time.Add(player.userID, 0);
            if (!inventory.ContainsKey(player.userID)) inventory.Add(player.userID, new List<Inventory>());
        }

        void UpdateUI()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (!time.ContainsKey(player.userID)) time.Add(player.userID, 0);
                time[player.userID]++;

                if (openedui.Contains(player.userID))
                {
                    var container = new CuiElementContainer();
                    if (GetWipeDay() == 1)
                    {
                        CuiHelper.DestroyUi(player, "1");
                        container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = "0.0539067 0.1902786", AnchorMax = "0.2632818 0.283334" },
                            Button = { Color = "0 0 0 0", Command = !HasCooldown(player, 1) && GetWipeDay() == 1 ? "TakeCase 1" : "" },
                            Text = { Color = HexToRustFormat("#747474FF"), Text = CanTake(player, 1).ToUpper(), Align = TextAnchor.MiddleCenter, FontSize = 20, Font = "RobotoCondensed-bold.ttf" }
                        }, "container", "1");
                    }
                    if (GetWipeDay() == 2)
                    {
                        CuiHelper.DestroyUi(player, "2");
                        container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = "0.2867177 0.1902786", AnchorMax = "0.4960909 0.283334" },
                            Button = { Color = "0 0 0 0", Command = !HasCooldown(player, 2) && GetWipeDay() == 2 ? "TakeCase 2" : "" },
                            Text = { Color = HexToRustFormat("#747474FF"), Text = CanTake(player, 2).ToUpper(), Align = TextAnchor.MiddleCenter, FontSize = 20, Font = "RobotoCondensed-bold.ttf" }
                        }, "container", "2");
                    }
                    if (GetWipeDay() == 3)
                    {
                        CuiHelper.DestroyUi(player, "3");
                        container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = "0.5195253 0.1902786", AnchorMax = "0.7289009 0.283334" },
                            Button = { Color = "0 0 0 0", Command = !HasCooldown(player, 3) && GetWipeDay() == 3 ? "TakeCase 3" : "" },
                            Text = { Color = HexToRustFormat("#747474FF"), Text = CanTake(player, 3).ToUpper(), Align = TextAnchor.MiddleCenter, FontSize = 20, Font = "RobotoCondensed-bold.ttf" }
                        }, "container", "3");
                    }
                    if (GetWipeDay() == 4)
                    {
                        CuiHelper.DestroyUi(player, "3");
                        container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = "0.7476482 0.1902786", AnchorMax = "0.9570214 0.283334" },
                            Button = { Color = "0 0 0 0", Command = !HasCooldown(player, 4) && GetWipeDay() == 4 ? "TakeCase 4" : "" },
                            Text = { Color = HexToRustFormat("#747474FF"), Text = CanTake(player, 4).ToUpper(), Align = TextAnchor.MiddleCenter, FontSize = 20, Font = "RobotoCondensed-bold.ttf" }
                        }, "container", "4");
                    }
                    CuiHelper.AddUi(player, container);
                }
            }
        }

        void UpdateTime()
        {
            if (DateTime.UtcNow.AddHours(3).ToString("HH:mm") == "02:00")
            {
                if (day.Count > 0)
                {
                    day[0]++;
                    //Puts("Started new day..");
                    time.Clear();
                    SaveData();
                }
            }
        }

        int GetWipeDay()
        {
            if (day == null) day.Add(1);
            return day[0];
        }

        bool HasCooldown(BasePlayer player, int Day)
        {
            if (!time.ContainsKey(player.userID)) time.Add(player.userID, 0);
            foreach (var i in cfg.settings.cases)
            {
                if (Day.ToString() != i.Key) continue;
                var cooldown = time[player.userID];
                if (cooldown >= i.Value.time) return false;
            }
            return true;
        }

        int GetCooldown(BasePlayer player, int Day)
        {
            if (!time.ContainsKey(player.userID)) time.Add(player.userID, 0);
            int amount = 0;
            foreach (var i in cfg.settings.cases)
            {
                if (Day.ToString() != i.Key) continue;
                var cooldown = time[player.userID];
                amount = i.Value.time - cooldown;
                if (amount < 0) return 0;
            }
            return amount;
        }

        string CanTake(BasePlayer player, int Day)
        {
            string text = "ОТКРЫТЬ";
            if (taked.ContainsKey(player.userID) && taked[player.userID] == Day) return "ПОЛУЧЕНО";
            if (Day != GetWipeDay()) return "НЕДОСТУПНО";
            if (HasCooldown(player, Day) == true) return TimeToString(GetCooldown(player, Day));
            return text;
        }

        [ChatCommand("case")]
        private void CaseCommand(BasePlayer player)
        {
            if (player == null) return;
            DrawMainUI(player);
        }

        [ConsoleCommand("CloseUI")]
        private void CloseUI(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null) return;
            if (openedui.Contains(player.userID)) openedui.Remove(player.userID);
            CuiHelper.DestroyUi(player, Layer);
        }

        [ConsoleCommand("OpenInventoryUI")]
        private void OpenInventoryUI(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null) return;
            if (openedui.Contains(player.userID)) openedui.Remove(player.userID);
            CuiHelper.DestroyUi(player, Layer);
            DrawInventoryUI(player);
        }

        [ConsoleCommand("TakeCase")]
        private void TakeCase(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null) return;
            if (!args.HasArgs(1)) return;

            var day = args.Args[0];
            if (HasCooldown(player, int.Parse(day))) return;
            if (taked.ContainsKey(player.userID) && taked[player.userID] >= int.Parse(day)) return;

            foreach (var capsule in cfg.settings.cases)
            {
                if (capsule.Key != day) continue;
                
                if (capsule.Value.items)
                {
                    foreach (var item in capsule.Value.itemsdrop)
                    {
                        if (UnityEngine.Random.Range(0, 100) < item.Value.chance)
                        {
                            var amount = Oxide.Core.Random.Range(item.Value.min, item.Value.max);
                            /*var newItem = ItemManager.CreateByName(item.Key, amount);
                            if (newItem == null) continue;
                            if (!player.inventory.GiveItem(newItem))
                                newItem.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity, new Quaternion());*/
                            if (inventory.ContainsKey(player.userID))
                            {
                                bool has = false;
                                foreach (var items in inventory[player.userID])
                                {
                                    if (items.shortname == item.Key) { items.amount += amount; has = true; }
                                }
                                if (!has) inventory[player.userID].Add(new Inventory { command = false, shortname = item.Key, amount = amount });
                            }
                            else inventory.Add(player.userID, new List<Inventory> { new Inventory { command = false, shortname = item.Key, amount = amount }});
                        }
                    }
                }
                if (capsule.Value.command)
                {
                    foreach (var cmd in capsule.Value.strcommands)
                    {
                        if (UnityEngine.Random.Range(0, 100) < cmd.Value.chances) 
                        {
                            if (inventory.ContainsKey(player.userID)) inventory[player.userID].Add(new Inventory { command = true, strcommand = cmd.Key });
                            else inventory.Add(player.userID, new List<Inventory> { new Inventory { command = true, strcommand = cmd.Key }});
                        }
                    }
                }
            }

            if (taked.ContainsKey(player.userID)) taked[player.userID] = int.Parse(day);
            else taked.Add(player.userID, int.Parse(day));

            var effect = new Effect("assets/prefabs/misc/xmas/presents/effects/unwrap.prefab", player, 0, Vector3.zero, Vector3.forward);
            EffectNetwork.Send(effect, player.net.connection);
        }

        void GiveApiCase(BasePlayer player, string csc)
        {
            string ccc;
            if (csc == "tank")
            {
                ccc = "5";
                TakeApi(player, ccc);
            }
            if (csc == "heli")
            {
                ccc = "6";
                TakeApi(player, ccc);
            }
            if (csc == "elite")
            {
                ccc = "7";
                TakeApi(player, ccc);
            }
            if (csc == "comp")
            {
                ccc = "8";
                TakeApi(player, ccc);
            }
            if (csc == "res")
            {
                ccc = "9";
                TakeApi(player, ccc);
            }
            return;
        }

        void TakeApi(BasePlayer player, string day)
        {
            if (player == null) return;

            foreach (var capsule in cfg.settings.cases)
            {
                if (capsule.Key != day) continue;

                if (capsule.Value.command)
                {
                    foreach (var cmd in capsule.Value.strcommands)
                    {
                        if (UnityEngine.Random.Range(0, 100) < cmd.Value.chances)
                        {
                            if (inventory.ContainsKey(player.userID)) inventory[player.userID].Add(new Inventory { command = true, strcommand = cmd.Key });
                            else inventory.Add(player.userID, new List<Inventory> { new Inventory { command = true, strcommand = cmd.Key } });
                        }
                    }
                }
            }

            var effect = new Effect("assets/prefabs/misc/xmas/presents/effects/unwrap.prefab", player, 0, Vector3.zero, Vector3.forward);
            EffectNetwork.Send(effect, player.net.connection);
        }

        [ConsoleCommand("casepage")]
        private void ChangePage(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player != null && args.HasArgs(1))
            {
                var page = int.Parse(args.Args[0]);
                if (page * 14 <= inventory[player.userID].Count)
                {
                    DrawInventoryUI(player, page);
                }
            }
        }

        [ConsoleCommand("TakeItem")]
        private void TakeItem(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null) return;
            if (!args.HasArgs(2)) return;

            var shortname = args.Args[0];
            var page = int.Parse(args.Args[1]);

            foreach (var item in inventory[player.userID])
            {
                if (item.command || item.shortname != shortname) continue;
                var newItem = ItemManager.CreateByName(item.shortname, item.amount);
                if (newItem == null) continue;
                player.GiveItem(newItem);
                //if (!player.inventory.GiveItem(newItem))
                //    newItem.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity, new Quaternion());
                break;
            }

            for (int i = 0; i < inventory[player.userID].Count; i++)
            {
                var key = inventory[player.userID][i];
                if (key.shortname != shortname) continue;
                inventory[player.userID].Remove(inventory[player.userID][i]);
                break;
            }

            DrawInventoryUI(player, page);
        }

        [ConsoleCommand("TakePerm")]
        private void TakePerm(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null) return;
            if (!args.HasArgs(2)) return;

            var perm = args.Args[0].Replace("*", " ");
            var page = int.Parse(args.Args[1]);

            foreach (var item in inventory[player.userID])
            {
                if (!item.command || item.strcommand != perm) continue;
                Server.Command(perm.Replace("%steamid%", player.UserIDString));
                break;
            }

            for (int i = 0; i < inventory[player.userID].Count; i++)
            {
                var key = inventory[player.userID][i];
                if (!key.command || key.strcommand != perm) continue;
                inventory[player.userID].Remove(inventory[player.userID][i]);
                break;
            }

            DrawInventoryUI(player, page);
        }

        void DrawMainUI(BasePlayer player)
        {
            if (!openedui.Contains(player.userID)) openedui.Add(player.userID);

            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                CursorEnabled = true,
            }, "Overlay", Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Color = "0 0 0 0", Command = "CloseUI" },
                Text = { Text = "" }
            }, Layer);

            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = "container",
                Components =
                {
                    new CuiRawImageComponent {Png = GetImage(MainIMG) },
                    new CuiRectTransformComponent {AnchorMin = "0.1666664 0.1620365", AnchorMax = "0.833333 0.8287033"}
                }
            });
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.0539067 0.1902786", AnchorMax = "0.2632818 0.283334" },
                Button = { Color = "0 0 0 0", Command = !HasCooldown(player, 1) && GetWipeDay() == 1 ? "TakeCase 1" : "" },
                Text = { Color = HexToRustFormat("#747474FF"), Text = CanTake(player, 1).ToUpper(), Align = TextAnchor.MiddleCenter, FontSize = 20, Font = "RobotoCondensed-bold.ttf" }
            }, "container", "1");
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.2867177 0.1902786", AnchorMax = "0.4960909 0.283334" },
                Button = { Color = "0 0 0 0", Command = !HasCooldown(player, 2) && GetWipeDay() == 2 ? "TakeCase 2" : "" },
                Text = { Color = HexToRustFormat("#747474FF"), Text = CanTake(player, 2).ToUpper(), Align = TextAnchor.MiddleCenter, FontSize = 20, Font = "RobotoCondensed-bold.ttf" }
            }, "container", "2");
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5195253 0.1902786", AnchorMax = "0.7289009 0.283334" },
                Button = { Color = "0 0 0 0", Command = !HasCooldown(player, 3) && GetWipeDay() == 3 ? "TakeCase 3" : "" },
                Text = { Color = HexToRustFormat("#747474FF"), Text = CanTake(player, 3).ToUpper(), Align = TextAnchor.MiddleCenter, FontSize = 20, Font = "RobotoCondensed-bold.ttf" }
            }, "container", "3");
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.7476482 0.1902786", AnchorMax = "0.9570214 0.283334" },
                Button = { Color = "0 0 0 0", Command = !HasCooldown(player, 4) && GetWipeDay() == 4 ? "TakeCase 4" : "" },
                Text = { Color = HexToRustFormat("#747474FF"), Text = CanTake(player, 4).ToUpper(), Align = TextAnchor.MiddleCenter, FontSize = 20, Font = "RobotoCondensed-bold.ttf" }
            }, "container", "4");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.05390671 0.05138971", AnchorMax = "0.2625005 0.1444452" },
                Button = { Color = "0 0 0 0", Command = "OpenInventoryUI" },
                Text = { Text = "" }
            }, "container");

            CuiHelper.AddUi(player, container);
        }
        
        void DrawInventoryUI(BasePlayer player, int page = 0)
        {
            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                CursorEnabled = true,
            }, "Overlay", Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Color = "0 0 0 0", Close = Layer },
                Text = { Text = "" }
            }, Layer);

            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = "container",
                Components =
                {
                    new CuiRawImageComponent {Png = GetImage(InventoryIMG) },
                    new CuiRectTransformComponent {AnchorMin = "0.1666664 0.1620365", AnchorMax = "0.833333 0.8287033"}
                }
            });

            const double startAnMinX = 0.06484415;
            const double startAnMaxX = 0.1703129;
            const double startAnMinY = 0.6013896;
            const double startAnMaxY = 0.8805562;
            double anMinX = startAnMinX;
            double anMaxX = startAnMaxX;
            double anMinY = startAnMinY;
            double anMaxY = startAnMaxY;

            List<Inventory> dict = inventory[player.userID].Skip(14 * page).Take(14).ToList();
            for (int i = 0; i < dict.Count; i++)
            {
                var value = dict[i];
                if (value == null) continue;

                if ((i != 0) && (i % 7 == 0))
                {
                    anMinX = startAnMinX;
                    anMaxX = startAnMaxX;
                    anMinY -= 0.4013889;
                    anMaxY -= 0.4013889;
                }

                container.Add(new CuiElement
                {
                    Parent = "container",
                    Name = i.ToString(),
                    Components =
                    {
                        new CuiImageComponent {Color = "0 0 0 0"},
                        new CuiRectTransformComponent { AnchorMin = $"{anMinX} {anMinY}", AnchorMax = $"{anMaxX} {anMaxY}" }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = i.ToString(),
                    Components =
                    {
                        new CuiRawImageComponent {Png = value.command ? GetImage(value.strcommand) : GetImage(value.shortname) },
                        new CuiRectTransformComponent {AnchorMin = "0 0.199005", AnchorMax = "1 0.8706468"}
                    }
                });
                if (!value.command)
                {
                    container.Add(new CuiElement
                    {
                        Parent = i.ToString(),
                        Components =
                        {
                            new CuiTextComponent { Color = HexToRustFormat("#949494FF"), Text = "x" + value.amount.ToString(), Align = TextAnchor.LowerRight, FontSize = 10, Font = "RobotoCondensed-bold.ttf" },
                            new CuiRectTransformComponent {AnchorMin = "0.08888899 0.2089554", AnchorMax = "0.9185183 0.5870644"}
                        }
                    });
                }
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.9850744" },
                    Button = { Color = "0 0 0 0", Command = !value.command ? "TakeItem " + value.shortname + " " + page.ToString() : $"TakePerm {value.strcommand.Replace(" ", "*")}" + " " + page.ToString() },
                    Text = { Color = HexToRustFormat("#747474FF"), Text = "ЗАБРАТЬ\n", Align = TextAnchor.LowerCenter, FontSize = 12, Font = "RobotoCondensed-bold.ttf" }
                }, i.ToString());

                anMinX += 0.12812545;
                anMaxX += 0.12812545;
            }

            container.Add(new CuiElement
            {
                Parent = "container",
                Components =
                {
                    new CuiTextComponent { Color = HexToRustFormat("#747474FF"), Text = $"{page + 1}", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0.4601567 0.05277855", AnchorMax = "0.5382817 0.1916674"}
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.4476566 0.08472304", AnchorMax = "0.4742191 0.1611119" },
                Button = { Command = $"casepage {page - 1}", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, "container");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5265617 0.08472304", AnchorMax = "0.5531241 0.1611119" },
                Button = { Command = $"casepage {page + 1}", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, "container");

            CuiHelper.AddUi(player, container);
        }

        public string TimeToString(double time)
        {
            TimeSpan elapsedTime = TimeSpan.FromSeconds(time);
            int hours = elapsedTime.Hours;
            int minutes = elapsedTime.Minutes;
            int seconds = elapsedTime.Seconds;
            int days = Mathf.FloorToInt((float)elapsedTime.TotalDays);
            string s = "";
            if (days > 0) s += $"{days} дн.";
            if (hours > 0) s += $"{hours} ч. ";
            if (minutes > 0) s += $"{minutes} мин. ";
            if (seconds >= 0) s += $"{seconds} сек.";
            else s = s.TrimEnd(' ');
            return s;
        }

        private static string HexToRustFormat(string hex)
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
            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }

        string GetImage(string name) => (string)ImageLibrary?.Call("GetImage", name);

        public static class IMGLibrary
        {
            public static bool AddImage(string url, string imageName, ulong imageId = 0, Action callback = null) => (bool)_ins.ImageLibrary.Call("AddImage", url, imageName, imageId, callback);
            public static bool AddImageData(string imageName, byte[] array, ulong imageId = 0, Action callback = null) => (bool)_ins.ImageLibrary.Call("AddImageData", imageName, array, imageId, callback);
            public static string GetImageURL(string imageName, ulong imageId = 0) => (string)_ins.ImageLibrary.Call("GetImageURL", imageName, imageId);
            public static string GetImage(string imageName, ulong imageId = 0, bool returnUrl = false) => (string)_ins.ImageLibrary.Call("GetImage", imageName, imageId, returnUrl);
            public static List<ulong> GetImageList(string name) => (List<ulong>)_ins.ImageLibrary.Call("GetImageList", name);
            public static Dictionary<string, object> GetSkinInfo(string name, ulong id) => (Dictionary<string, object>)_ins.ImageLibrary.Call("GetSkinInfo", name, id);
            public static bool HasImage(string imageName, ulong imageId) => (bool)_ins.ImageLibrary.Call("HasImage", imageName, imageId);
            public static bool IsInStorage(uint crc) => (bool)_ins.ImageLibrary.Call("IsInStorage", crc);
            public static bool IsReady() => (bool)_ins.ImageLibrary.Call("IsReady");
            public static void ImportImageList(string title, Dictionary<string, string> imageList, ulong imageId = 0, bool replace = false, Action callback = null) => _ins.ImageLibrary.Call("ImportImageList", title, imageList, imageId, replace, callback);
            public static void ImportItemList(string title, Dictionary<string, Dictionary<ulong, string>> itemList, bool replace = false, Action callback = null) => _ins.ImageLibrary.Call("ImportItemList", title, itemList, replace, callback);
            public static void ImportImageData(string title, Dictionary<string, byte[]> imageList, ulong imageId = 0, bool replace = false, Action callback = null) => _ins.ImageLibrary.Call("ImportImageData", title, imageList, imageId, replace, callback);
            public static void LoadImageList(string title, List<KeyValuePair<string, ulong>> imageList, Action callback = null) => _ins.ImageLibrary.Call("LoadImageList", title, imageList, callback);
            public static void RemoveImage(string imageName, ulong imageId) => _ins?.ImageLibrary?.Call("RemoveImage", imageName, imageId);
        }
    }
}