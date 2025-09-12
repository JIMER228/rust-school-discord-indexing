using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;


namespace Oxide.Plugins
{
    /* Плагин был разыгран в RUST EDIT PRO https://discord.gg/8CWJadmA5w */  [Info("TPStatsSystem", "https://discord.gg/8CWJadmA5w", "1.0.7‌﻿‌​‍‍")]
    class TPStatsSystem : RustPlugin
    {
        #region Вар
        string Layer = "Stats_UI";

        [PluginReference] Plugin ImageLibrary, RustStore;

        Dictionary<ulong, DBSettings> DB = new Dictionary<ulong, DBSettings>();

        public string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary?.Call("GetImage", shortname, skin);
        #endregion

        #region Класс
        public class DBSettings
        {
            public string DisplayName;
            public int Points = 0;
            public bool IsConnected;
            public int Balance;
            public Dictionary<string, int> Settings = new Dictionary<string, int>()
            {
                ["Kill"] = 0,
                ["Death"] = 0,
                ["Time"] = 0
            };
            public Dictionary<string, int> Res = new Dictionary<string, int>()
            {
                ["wood"] = 0,
                ["stones"] = 0,
                ["metal.ore"] = 0,
                ["sulfur.ore"] = 0,
                ["hq.metal.ore"] = 0,
                ["cloth"] = 0,
                ["leather"] = 0,
                ["fat.animal"] = 0,
                ["cratecostume"] = 0
            };
        }
        #endregion

        #region Конфиг
        Configuration config;
        class Configuration
        {
            [JsonProperty("ID магазина")] public string ShopID = "";
            [JsonProperty("Secret ключ магазина")] public string Secret = "";
            [JsonProperty("Настройки бонусов")] public List<string> Bonus;
            public static Configuration GetNewConfig()
            {
                return new Configuration
                {
                    Bonus = new List<string>()
                    {
                        "10000",
                        "9000",
                        "8000",
                        "7000",
                        "6000",
                        "5000",
                        "4000",
                        "3000",
                        "2000",
                        "1000"
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
                if (config?.Bonus == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Ошибка чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfig();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Хуки
        void OnServerInitialized()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("StatsSystem/PlayerList"))
                DB = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, DBSettings>>("StatsSystem/PlayerList");

            ImageLibrary.Call("AddImage", $"https://imgur.com/P72zCAu.png", "P72zCAu");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/SE8GPHK.png", "avatarLine");
            ImageLibrary.Call("AddImage", "https://imgur.com/yEONWCm.png", "Skip");
            ImageLibrary.Call("AddImage", "https://imgur.com/uF1fOzF.png", "SkipBack");

            foreach (var check in ResImage)
                ImageLibrary.Call("AddImage", $"https://rustlabs.com/img/items180/{check}.png", check);

            foreach (var check in BasePlayer.activePlayerList)
                OnPlayerConnected(check);

            timer.Every(60f, PlayTime);
        }

        void PlayTime()
        {
            foreach (var check in BasePlayer.activePlayerList)
                DB[check.userID].Settings["Time"] += 1;
        }

        void OnPlayerConnected(BasePlayer player)
        {
            SteamAvatarAdd(player.UserIDString);
            if (!DB.ContainsKey(player.userID))
                DB.Add(player.userID, new DBSettings());

            DB[player.userID].DisplayName = player.displayName;
            DB[player.userID].IsConnected = true;
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            DB[player.userID].IsConnected = false;
            SaveDataBase();
        }

        void Unload()
        {
            SaveDataBase();
        }

        void SaveDataBase()
        {
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("StatsSystem/PlayerList", DB);
        }

        void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser == null || player == null || item == null) return;
            if (DB[player.userID].Res.ContainsKey(item.info.shortname))
            {
                DB[player.userID].Res[item.info.shortname] += item.amount;
                return;
            }
        }

        void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser == null || player == null || item == null) return;
            if (DB[player.userID].Res.ContainsKey(item.info.shortname))
            {
                DB[player.userID].Res[item.info.shortname] += item.amount;
                DB[player.userID].Points += 7;
                return;
            }
        }

        void OnCollectiblePickup(Item item, BasePlayer player)
        {
            if (item == null || player == null) return;
            if (DB[player.userID].Res.ContainsKey(item.info.shortname))
            {
                DB[player.userID].Res[item.info.shortname] += item.amount;
                return;
            }
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (info == null || player == null || player.IsNpc || info.InitiatorPlayer == null || info.InitiatorPlayer.IsNpc) return;

            if (info.InitiatorPlayer != null)
            {
                var killer = info.InitiatorPlayer;

                if (killer != player)
                {
                    if (DB.ContainsKey(killer.userID))
                    {
                        DB[killer.userID].Settings["Kill"]++;
                        DB[killer.userID].Points += 100;
                    }
                }
                if (DB.ContainsKey(player.userID))
                {
                    DB[player.userID].Settings["Death"]++;
                    DB[player.userID].Points -= 25;
                }
            }
        }

        public ulong lastDamageName;
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BradleyAPC && info.Initiator is BasePlayer)
                lastDamageName = info.Initiator.ToPlayer().userID;
            if (entity is BaseHelicopter && info.Initiator is BasePlayer)
                lastDamageName = info.Initiator.ToPlayer().userID;
        }

        object OnHelicopterKilled(CH47HelicopterAIController heli)
        {
            Puts("OnHelicopterKilled works!");
            return null;
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;
            BasePlayer player = null;
            if (info.InitiatorPlayer != null)
                player = info.InitiatorPlayer;

            if (player == null)
            {
                player = BasePlayer.FindByID(lastDamageName);
                if (entity is BaseHelicopter)
                {
                    DB[player.userID].Points += 1500;
                }
            }
            else
            {
                if (entity is BradleyAPC)
                {
                    DB[player.userID].Points += 750;
                }
                if (entity.ShortPrefabName.Contains("barrel"))
                {
                    DB[player.userID].Res["cratecostume"]++;
                    DB[player.userID].Points += 2;
                }
            }
        }

        void OnNewSave()
        {
            timer.In(60, () =>
            {
                PrintWarning("Обнаружен вайп, происходит выдача призов за топ и очистка даты!");

                int xx = 0;
                foreach (var check in  DB.OrderByDescending(x => x.Value.Points))
                {
                    if (config.Bonus.Count > xx)
                    {
                        check.Value.Balance += int.Parse(config.Bonus.ElementAt(xx));
                        xx++;
                        continue;
                    }
                    break;
                }

                foreach (var check in DB)
                {
                    check.Value.Points = 0;
                    check.Value.IsConnected = false;
                    check.Value.Settings = new Dictionary<string, int>()
                    {
                        ["Kill"] = 0,
                        ["Death"] = 0,
                        ["Time"] = 0
                    };
                    check.Value.Res = new Dictionary<string, int>()
                    {
                        ["wood"] = 0,
                        ["stones"] = 0,
                        ["metal.ore"] = 0,
                        ["sulfur.ore"] = 0,
                        ["hq.metal.ore"] = 0,
                        ["cloth"] = 0,
                        ["leather"] = 0,
                        ["fat.animal"] = 0,
                        ["cratecostume"] = 0
                    };
                }

                SaveDataBase();
            });
        }
        #endregion

        #region Вывод коинов
        void ApiChangeGameStoresBalance(ulong userId, int amount)
        {
            var player = BasePlayer.FindByID(userId);
            ExecuteApiRequest(new Dictionary<string, string>()
            {
                { "action", "moneys" },
                { "type", "plus" },
                { "DisplayName", player.displayName.ToUpper() },
                { "steam_id", userId.ToString() },
                { "amount", amount.ToString() },
                { "mess", "Спасибо что играете у нас!"}
            });
        }

        void APIChangeUserBalance(ulong steam, int balanceChange)
        {
            if (RustStore)
            {
                plugins.Find("RustStore").CallHook("APIChangeUserBalance", steam, balanceChange, new Action<string>((result) =>
                {
                    if (result == "SUCCESS")
                    {
                        LogToFile("LogMoscow", $"СтимID: {steam}\nУспешно получил {balanceChange} рублей на игровой счет!\n", this);
                        PrintWarning($"Игрок {steam} успешно получил {balanceChange} рублей");
                    }
                    else
                    {
                        PrintError($"Ошибка пополнения баланса для {steam}!");
                        PrintError($"Причина: {result}");
                        LogToFile("logError", $"Баланс игрока {steam} не был изменен, ошибка: {result}", this);
                    }
                }));
            }
        }

        void ExecuteApiRequest(Dictionary<string, string> args)
        {
            string url = $"https://gamestores.ru/api/?shop_id={config.ShopID}&secret={config.Secret}" + $"{string.Join("", args.Select(arg => $"&{arg.Key}={arg.Value}").ToArray())}";
            LogToFile("LogGS", $"Ник: {args["DisplayName"]}\nСтимID: {args["steam_id"]}\nУспешно получил {args["amount"]} рублей на игровой счет!\n", this);
            webrequest.EnqueueGet(url, (i, s) =>
            {
                if (i != 200)
                {
                    PrintError($"Ошибка соединения с сайтом!");
                }
                else
                {
                    JObject jObject = JObject.Parse(s);
                    if (jObject["result"].ToString() == "fail")
                    {
                        PrintError($"Ошибка пополнения баланса для {args["steam_id"]}!");
                        PrintError($"Причина: {jObject["message"].ToString()}");
                        LogToFile("logError", $"Баланс игрока {args["steam_id"]} не был изменен, ошибка: {jObject["message"].ToString()}", this);
                    }
                    else
                    {
                        PrintWarning($"Игрок {args["steam_id"]} успешно получил {args["amount"]} рублей");
                    }
                }
            }, this);
        }
        #endregion

        #region Картинки ресурсов
        List<string> ResImage = new List<string>()
        {
            "wood",
            "stones",
            "metal.ore",
            "sulfur.ore",
            "hq.metal.ore",
            "cloth",
            "leather",
            "fat.animal",
            "cratecostume"
        };
        #endregion

        #region Команды
        [ChatCommand("top")]
        void ChatTop(BasePlayer player) => StatsUI(player);

        [ChatCommand("stats")]
        void cmdProfileUis(BasePlayer player) => StatsUI(player);

        [ConsoleCommand("stats")]
        void ConsoleSkip(ConsoleSystem.Arg args)
        {
            var player = args.Player();

            if (player != null && args.HasArgs(1))
            {
                if (args.Args[0] == "profile")
                {
                    ProfileUI(player, ulong.Parse(args.Args[1]), int.Parse(args.Args[2]));
                }
                if (args.Args[0] == "skip")
                {
                    ListUI(player, int.Parse(args.Args[1]));
                }
                if (args.Args[0] == "take")
                {
                    if (DB[player.userID].Balance == 0)
                    {
                        SendReply(player, "Ваш баланс на данный момент пуст!");
                        return;
                    }
                    if (string.IsNullOrEmpty(config.Secret)) APIChangeUserBalance(player.userID, DB[player.userID].Balance);
                    else ApiChangeGameStoresBalance(player.userID, DB[player.userID].Balance);

                    SendReply(player, $"Вы успешно вывели {DB[player.userID].Balance} рублей, на игровой магазин!");
                    DB[player.userID].Balance -= DB[player.userID].Balance;
                    CuiHelper.DestroyUi(player, "MainStats");
                }
            }
        }
        #endregion

        #region Интерфейс
        private void StatsUI(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Name = "MainStats" + ".Main",
                Parent = ".Mains",
                Components = 
                {
                    new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "P72zCAu"), Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "-0.315 -0.27", AnchorMax = "1.3 1.275", OffsetMax = "0 0" },
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.8 0.804", AnchorMax = "0.817 0.832" },
                Button = { Close = "Menu_UI", Color = "0 0 0 0" },
                Text = { Text = "" }
            }, "MainStats" + ".Main");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.45 0.682", AnchorMax = "0.807 0.72" },
                Image = { Color = "1 1 1 0" }
            }, "MainStats" + ".Main", "MainStats" + ".Main" + ".Text");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04 0", AnchorMax = "1 1" },
                Text = { Text = $"#", Color = "1 1 1 0.85", Align = TextAnchor.MiddleLeft, FontSize = 14, Font = "robotocondensed-bold.ttf" }
            }, "MainStats" + ".Main" + ".Text");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.14 0", AnchorMax = "1 1" },
                Text = { Text = $"ИМЯ ИГРОКА", Color = "1 1 1 0.85", Align = TextAnchor.MiddleLeft, FontSize = 13, Font = "robotocondensed-bold.ttf" }
            }, "MainStats" + ".Main" + ".Text");
    
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.51 0", AnchorMax = "1 1" },
                Text = { Text = $"НАГРАДА", Color = "1 1 1 0.85", Align = TextAnchor.MiddleLeft, FontSize = 13, Font = "robotocondensed-bold.ttf" }
            }, "MainStats" + ".Main" + ".Text");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.88 0", AnchorMax = "1 1" },
                Text = { Text = $"ОЧКИ", Color = "1 1 1 0.85", Align = TextAnchor.MiddleLeft, FontSize = 13, Font = "robotocondensed-bold.ttf" }
            }, "MainStats" + ".Main" + ".Text");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.195 0.188", AnchorMax = "0.438 0.377" },
                Image = { Color = "1 1 1 0" }
            }, "MainStats" + ".Main", "MainStats" + ".Main" + ".TextPoint");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.045 0.11", AnchorMax = "1 1" },
                Text = { Text = $"<color=White>Очки даются:</color>\nУбийство +100, добыча +7, разрушение бочки +2, сбитие\nвертолета +1500,уничтожение\nтанка +750\n\n<color=White>Очки отнимаются:</color>\nСмерть и самоубийство -25\nНаграды выдаются после вайпа на сервере!", Color = "1 1 1 0.45", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-bold.ttf" }
            }, "MainStats" + ".Main" + ".TextPoint");

            CuiHelper.DestroyUi(player, "MainStats");
            CuiHelper.AddUi(player, container);
            ProfileUI(player, player.userID, 0);
            ListUI(player, 0);
        }
        
        private void ListUI(BasePlayer player, int page)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.45 0.17", AnchorMax = "0.807 0.675" },
                Image = { Color = "1 1 1 0" }
            }, "MainStats" + ".Main", "MainStats" + ".Main" + ".List");

            for (int y = 0; y < 9; y++)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"0 {0.915 - y * 0.099}", AnchorMax = $"1 {0.984 - y * 0.099}" },
                    Image = { Color = "1 1 1 0" }
                }, "MainStats" + ".Main" + ".List", "MainStats" + ".Main" + ".List" + $".TopLine{y}");
            }

            int i = 0;
            var items = from item in DB orderby item.Value.Points descending select item;
            foreach (var check in items.Skip(page * 9).Take(9))
            {
                container.Add(new CuiLabel
                {
                    Text = { Text = $"{i + (1 + (page * 9))}", Color = "1 1 1 1", FontSize = 12, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft },
                    RectTransform = { AnchorMin = "0.04 0", AnchorMax = "1 1" },
                }, "MainStats" + ".Main" + ".List" + $".TopLine{i}");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.14 0", AnchorMax = "1 1" },
                    Text = { Text = $"{check.Value.DisplayName}", Color = "1 1 1 0.85", Align = TextAnchor.MiddleLeft, FontSize = 13, Font = "robotocondensed-bold.ttf" }
                }, "MainStats" + ".Main" + ".List" + $".TopLine{i}");
    
                if (config.Bonus.Count > i + (page * 9))
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.51 0", AnchorMax = "1 1" },
                        Text = { Text = $"{config.Bonus.ElementAt(i + (page * 9))}", Color = "1 1 1 0.85", Align = TextAnchor.MiddleLeft, FontSize = 13, Font = "robotocondensed-bold.ttf" }
                    }, "MainStats" + ".Main" + ".List" + $".TopLine{i}");
                }

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.88 0", AnchorMax = "1 1" },
                    Text = { Text = $"{check.Value.Points}", Color = "1 1 1 0.85", Align = TextAnchor.MiddleLeft, FontSize = 13, Font = "robotocondensed-bold.ttf" }
                }, "MainStats" + ".Main" + ".List" + $".TopLine{i}");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0", Command = $"stats profile {check.Key} {i + 1 + (page * 9)}" },
                    Text = { Text = $"", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "MainStats" + ".Main" + ".List" + $".TopLine{i}");
                i++;
            }

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.505 0.03", AnchorMax = $"0.7 0.1" },
                Button = { Color = "1 1 1 0", Command = DB.Count() > (page + 1) * 10 ? $"stats skip {page + 1}" : "" },
                Text = { Text = $"", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-bold.ttf" }
            }, "MainStats" + ".Main" + ".List", "Skips");

            container.Add(new CuiElement
            {
                Parent = "Skips",
                Components = 
                {
                    new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "Skip"), Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.3 0.03", AnchorMax = $"0.495 0.1" },
                Button = { Color = "1 1 1 0", Command = page > 0 ? $"stats skip {page - 1}" : "" },
                Text = { Text = $"", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-bold.ttf" }
            }, "MainStats" + ".Main" + ".List", "SkipsBack");

            container.Add(new CuiElement
            {
                Parent = "SkipsBack",
                Components = 
                {
                    new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "SkipBack"), Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                }
            });

            CuiHelper.DestroyUi(player, "MainStats" + ".Main" + ".List");
            CuiHelper.AddUi(player, container);
        }

        private void ProfileUI(BasePlayer player, ulong SteamID, int z)
        {
            var data = DB[SteamID];
            if (data == null) return;

            var kd = data.Settings["Death"] == 0 ? data.Settings["Kill"] : (float)Math.Round((float)data.Settings["Kill"] / data.Settings["Death"], 1);
            var status = data.IsConnected == true ? "ОНЛАЙН" : "ОФЛАЙН";

            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.195 0.38", AnchorMax = "0.438 0.72" },
                Image = { Color = "1 1 1 0" }
            }, "MainStats" + ".Main", "MainStats" + ".Main" + ".Profile");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.035 0.51", AnchorMax = "0.38 0.945" },
                Image = { Color = "1 1 1 0" }
            }, "MainStats" + ".Main" + ".Profile", "MainStats" + ".Main" + ".Profile" + ".Avatar");

            container.Add(new CuiElement
            {
                Parent = "MainStats" + ".Main" + ".Profile" + ".Avatar",
                Components =
                {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", SteamID.ToString()) },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "4 4", OffsetMax = "-4 -4" }
                }
            });

            container.Add(new CuiElement
            {
                Parent = "MainStats" + ".Main" + ".Profile" + ".Avatar",
                Components =
                {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "avatarLine") },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            for (int i = 0; i < 7; i++)
            {
                string Name = i == 0 ? "МЕСТО В ТОПЕ" : i == 1 ? "ОЧКОВ" : i == 2 ? "СТАТУС" : i == 3 ? "УБИЙСТВ" : i == 4 ? "СМЕРТЕЙ" : i == 5 ? "АКТИВНОСТЬ" : "К/Д";
                string Description = i == 0 ? $"{z}" : i == 1 ? $"{data.Points}" : i == 2 ? $"{status}" : i == 3 ? $"{data.Settings["Kill"]}" : i == 4 ? $"{data.Settings["Death"]}" : i == 5 ? $"{data.Settings["Time"]}" : $"{kd}";

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"0.435 {0.895 - (i * 0.06)}", AnchorMax = $"0.95 {0.94 - (i * 0.06)}" },
                    Image = { Color = "1 1 1 0" }
                }, "MainStats" + ".Main" + ".Profile", "MainStats" + ".Main" + ".Profile" + $".Line{i}");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0.005 0", AnchorMax = $"0.95 1" },
                    Text = { Text = $"{Name}", Color = "1 1 1 0.45", Align = TextAnchor.MiddleLeft, FontSize = 10, Font = "robotocondensed-regular.ttf" }
                }, "MainStats" + ".Main" + ".Profile" + $".Line{i}");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.99 1" },
                    Text = { Text = $"{Description}", Color = "1 1 1 0.85", Align = TextAnchor.MiddleRight, FontSize = 10, Font = "robotocondensed-regular.ttf" }
                }, "MainStats" + ".Main" + ".Profile" + $".Line{i}");
            }

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.025 0.065", AnchorMax = "0.38 0.135" },
                Image = { Color = "1 1 1 0" }
            }, "MainStats" + ".Main" + ".Profile", "MainStats" + ".Main" + ".Profile" + ".Balance");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.07 0", AnchorMax = "1 1" },
                Text = { Text = $"{(player.userID == SteamID ? $"Ваш баланс: {data.Balance}" : "Не тот профиль")}", Color = "1 1 1 0.45", Align = TextAnchor.MiddleLeft, FontSize = 11, Font = "robotocondensed-bold.ttf" }
            }, "MainStats" + ".Main" + ".Profile" + ".Balance");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.41 0.065", AnchorMax = "0.667 0.135" },
                Button = { Color = "1 1 1 0", Command = $"{(player.userID == SteamID ? "stats take" : "")}" },
                Text = { Text = $"", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "MainStats" + ".Main" + ".Profile");

            for (int x = 0; x < data.Res.Count; x++)
            {
                var Resourse = data.Res.ToList()[x];
                var Text = Resourse.Value >= 1000 ? $"{(float)Math.Round((float)Resourse.Value / 1000, 1)}к" : $"{Resourse.Value}";

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"{0.025 + (x * 0.1075)} 0.22", AnchorMax = $"{0.115 + (x * 0.1075)} 0.3" },
                    Text = { Text = $"{Text}", Color = "1 1 1 0.45", Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "robotocondensed-regular.ttf" }
                }, "MainStats" + ".Main" + ".Profile", "MainStats" + ".Main" + ".Profile" + $".Res{x}");
            }

            CuiHelper.DestroyUi(player, "MainStats" + ".Main" + ".Profile");
            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Подгрузка аватарок
        void SteamAvatarAdd(string userid)
        {
            string url = "http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key=B23DC0D84302CF828713C73F35A30006&" + "steamids=" + userid;
            webrequest.Enqueue(url, null, (code, response) =>
            {
                if (code == 200)
                {
                    string Avatar = (string)JObject.Parse(response)["response"]?["players"]?[0]?["avatarfull"];
                    ImageLibrary.Call("AddImage", Avatar, userid);
                }
            }, this);
        }
        #endregion
    }
}