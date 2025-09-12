using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("StatsSystem", "Molik lox", "1.0.6")]
    class StatsSystem : RustPlugin
    {
        [PluginReference] private Plugin ImageLibrary;
        
        private static StatsSystem Instance;
        private GameObject FileManagerObject;
        private FileManager m_FileManager;
        
        private bool ImageInit = false;
        
        #region Вар
        string Layer = "Stats_UI";

        Dictionary<string, DBSettings> DB = new Dictionary<string, DBSettings>();

        private Dictionary<string, string> StatsSystemImageList = new Dictionary<string, string>()
        {
            ["wood"] = "https://rustlabs.com/img/items180/wood.png",
            ["stones"] = "https://rustlabs.com/img/items180/stones.png",
            ["metal.ore"] = "https://rustlabs.com/img/items180/metal.ore.png",
            ["sulfur.ore"] = "https://rustlabs.com/img/items180/sulfur.ore.png",
            ["hq.metal.ore"] = "https://rustlabs.com/img/items180/hq.metal.ore.png",
            ["cloth"] = "https://rustlabs.com/img/items180/cloth.png",
            ["leather"] = "https://rustlabs.com/img/items180/leather.png",
            ["fat.animal"] = "https://rustlabs.com/img/items180/fat.animal.png",
            ["cratecostume"] = "https://rustlabs.com/img/items180/cratecostume.png"
        };
        
        void InitFileManager()
        {
            FileManagerObject = new GameObject("StatsSystem_FileManagerObject");
            m_FileManager = FileManagerObject.AddComponent<FileManager>();
        }

        IEnumerator LoadImages()
        {
            int i = 0;
            int lastpercent = -1;

            foreach (var name in StatsSystemImageList.Keys.ToList())
            {
                yield return m_FileManager.StartCoroutine(m_FileManager.LoadFile(name, StatsSystemImageList[name]));
                if (m_FileManager.GetPng(name) == null) yield return new WaitForSeconds(3);
                StatsSystemImageList[name] = m_FileManager.GetPng(name);
                int percent = (int) (i / (float) StatsSystemImageList.Keys.ToList().Count * 100);
                if (percent % 20 == 0 && percent != lastpercent)
                {
                    Puts($"Идёт загрузка изображений, загружено: {percent}%");
                    lastpercent = percent;
                }

                i++;
            }

            ImageInit = true;
            m_FileManager.SaveData();
            PrintWarning("Успешно загружено {0} изображения", i);
        }
        
        #endregion

        #region Класс
        public class DBSettings
        {
            public string DisplayName;
            public int Points = 0;
            public int Farm = 0;
            public int Kill = 0;
            public int Death = 0;
            public bool IsConnected;
            public int Balance;
            public Dictionary<string, int> Settings = new Dictionary<string, int>()
            {
                ["Kill"] = 0,
                ["Death"] = 0,
                ["Farm"] = 0
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
        
        [JsonProperty("ID магазина")] public string ShopID = "32665";
        [JsonProperty("Secret ключ магазина")] public string Secret = "7e96ca4803c0f38c3c7c1a83717a331e";
        [JsonProperty("Настройки бонусов")] public List<string> Bonus  = new List<string>()
        {
            "150",
            "100",
            "50",
            "25",
            "25",
        };

        #region Хуки
        void OnServerInitialized()
        {
            Instance = this;
            InitFileManager();
            ServerMgr.Instance.StartCoroutine(LoadImages());
            
            
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("StatsSystem/PlayerList"))
                DB = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, DBSettings>>("StatsSystem/PlayerList");

            foreach (var check in BasePlayer.activePlayerList)
                OnPlayerInit(check);
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (!DB.ContainsKey(player.UserIDString))
                DB.Add(player.UserIDString, new DBSettings());

            DB[player.UserIDString].DisplayName = player.displayName;
            DB[player.UserIDString].IsConnected = true;
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            DB[player.UserIDString].IsConnected = false;
            SaveDataBase();
        }

        void Unload()
        {
            UnityEngine.Object.Destroy(FileManagerObject);
            SaveDataBase();
        }

        void SaveDataBase()
        {
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("StatsSystem/PlayerList", DB);
        }

        void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser == null || player == null || item == null) return;
            if (DB[player.UserIDString].Res.ContainsKey(item.info.shortname))
            {
                DB[player.UserIDString].Res[item.info.shortname] += item.amount;
                DB[player.UserIDString].Settings["Farm"] += item.amount;
                DB[player.UserIDString].Farm += item.amount;
                return;
            }
        }

        void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser == null || player == null || item == null) return;
            if (DB[player.UserIDString].Res.ContainsKey(item.info.shortname))
            {
                DB[player.UserIDString].Res[item.info.shortname] += item.amount;
                DB[player.UserIDString].Settings["Farm"] += item.amount;
                DB[player.UserIDString].Farm += item.amount;
                DB[player.UserIDString].Points += 3;
                return;
            }
        }

        void OnCollectiblePickup(Item item, BasePlayer player)
        {
            if (item == null || player == null) return;
            if (DB[player.UserIDString].Res.ContainsKey(item.info.shortname))
            {
                DB[player.UserIDString].Res[item.info.shortname] += item.amount;
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
                    if (DB.ContainsKey(killer.UserIDString))
                    {
                        DB[killer.UserIDString].Settings["Kill"]++;
                        DB[player.UserIDString].Kill++;
                        DB[killer.UserIDString].Points += 25;
                    }
                }
                if (DB.ContainsKey(player.UserIDString))
                {
                    DB[player.UserIDString].Settings["Death"]++;
                    DB[player.UserIDString].Death++;
                    DB[player.UserIDString].Points -= 30;
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

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;
            BasePlayer player = null;

            if (info.InitiatorPlayer != null)
                player = info.InitiatorPlayer;

            if (player == null) return;

            if (entity is BradleyAPC)
            {
                player = BasePlayer.FindByID(lastDamageName);
                DB[player.UserIDString].Points += 1000;
            }

            if (entity is BaseHelicopter)
            {
                player = BasePlayer.FindByID(lastDamageName);
                DB[player.UserIDString].Points += 1000;
            }

            if (entity.ShortPrefabName.Contains("barrel"))
            {
                DB[player.UserIDString].Res["cratecostume"]++;
                DB[player.UserIDString].Points += 3;
            }
        }
        [ChatCommand("wipestats")]
        void AdminWipe(BasePlayer player)
        {
            if (!player.IsAdmin) return;
            player.ChatMessage("Вайп статы сделан!");
            int x = 0;
            foreach (var check in DB.Take(10))
            {
                //check.Value.Balance += int.Parse(config.Bonus.ElementAt(x));
                int bal = int.Parse(Bonus.ElementAt(x));
                ApiChangeGameStoresBalance(check.Key, bal);
                x++;
            }
        }

        void OnNewSave()
        {
            timer.In(60, () =>
            {
                PrintWarning("Обнаружен вайп, происходит выдача призов за топ и очистка даты!");

                foreach (var check in DB)
                {
                    check.Value.Points = 0;
                    check.Value.Farm = 0;
                    check.Value.Kill = 0;
                    check.Value.Death = 0;
                    check.Value.IsConnected = false;
                    check.Value.Settings = new Dictionary<string, int>()
                    {
                        ["Kill"] = 0,
                        ["Death"] = 0,
                        ["Farm"] = 0
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
                /*int x = 0;
                foreach (var check in DB.Take(10))
                {
                    //check.Value.Balance += int.Parse(config.Bonus.ElementAt(x));
                    int bal = int.Parse(config.Bonus.ElementAt(x));
                    ApiChangeGameStoresBalance(check.Key, bal);
                    x++;
                }*/

                SaveDataBase();
            });
        }
        #endregion

        #region Вывод коинов
        void ApiChangeGameStoresBalance(string userId, int amount)
        {
            var player = BasePlayer.FindByID(Convert.ToUInt64(userId));
            ExecuteApiRequest(new Dictionary<string, string>()
            {
                { "action", "moneys" },
                { "type", "plus" },
                { "DisplayName", player.displayName.ToUpper() },
                { "steam_id", userId },
                { "amount", amount.ToString() },
                { "mess", "Спасибо что играете у нас!"}
            });
        }

        void ExecuteApiRequest(Dictionary<string, string> args)
        {
            string url = $"https://gamestores.ru/api/?shop_id={ShopID}&secret={Secret}" + $"{string.Join("", args.Select(arg => $"&{arg.Key}={arg.Value}").ToArray())}";
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

        #region Команды
        [ChatCommand("top")]
        void ChatTop(BasePlayer player)
        {
            StatsUI(player);
        }

        [ConsoleCommand("stats")]
        void ConsoleSkip(ConsoleSystem.Arg args)
        {
            var player = args.Player();

            if (player != null && args.HasArgs(1))
            {
                if (args.Args[0] == "profile")
                {
                    ProfileUI(player, args.Args[1], int.Parse(args.Args[2]));
                }
                if (args.Args[0] == "back")
                {
                    StatsUI(player);
                }
                if (args.Args[0] == "skip")
                {
                    StatsUI(player, int.Parse(args.Args[1]));
                }
                if (args.Args[0] == "take")
                {
                    if (DB[player.UserIDString].Balance == 0)
                    {
                        SendReply(player, "Вы бомж!");
                        return;
                    }
                    
                    ApiChangeGameStoresBalance(player.UserIDString, DB[player.UserIDString].Balance);

                    SendReply(player, $"Вы успешно вывели {DB[player.UserIDString].Balance} рублей, на игровой магазин!");
                    DB[player.UserIDString].Balance -= DB[player.UserIDString].Balance;
                    CuiHelper.DestroyUi(player, "MainStats");
                }
            }
        }
        #endregion

        #region Интерфейс
        void StatsUI(BasePlayer player, int page = 0)
        {
            CuiHelper.DestroyUi(player, "MainStats");
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Overlay", "MainStats");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "MainStats");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.3 0.3", AnchorMax = "0.7 0.7", OffsetMax = "0 0" },
                Image = { Color = "0.2 0.2 0.2 1" }
            }, "MainStats", Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.23", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Image = { Color = HexToRustFormat("#EC0C0C4C") }
            }, Layer, "Top");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.95", AnchorMax = $"1 1" },
                Image = { Color = "0.5 0.5 0.5 0" }
            }, "Top");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.01 0.92", AnchorMax = $"1 1" },
                Image = { Color = "0.5 0.5 0.5 0" }
            }, "Top", "TTT");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.115 0", AnchorMax = $"0.4 1", OffsetMax = "0 0" },
                Button = { Color = "0.56 0.87 0.56 0" },
                Text = { Text = $"ИМЯ ИГРОКА", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "TTT");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.4 0", AnchorMax = $"0.5 1", OffsetMax = "0 0" },
                Button = { Color = "0.56 0.87 0.56 0" },
                Text = { Text = $"НАГРАДА", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "TTT");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.5 0", AnchorMax = $"0.6 1", OffsetMax = "0 0" },
                Button = { Color = "0.56 0.87 0.56 0" },
                Text = { Text = $"ФАРМ", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "TTT");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.6 0", AnchorMax = $"0.7 1", OffsetMax = "0 0" },
                Button = { Color = "0.56 0.87 0.56 0" },
                Text = { Text = $"ОЧКИ", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "TTT");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.7 0", AnchorMax = $"0.8 1", OffsetMax = "0 0" },
                Button = { Color = "0.56 0.87 0.56 0" },
                Text = { Text = $"К/Д", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "TTT");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.0 0.94", AnchorMax = $"0.08 1", OffsetMax = "0 0" },
                Button = { Color = "0.56 0.87 0.56 0" },
                Text = { Text = $"МЕСТО", Color = "1 1 1 1", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Top");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.837 0.02", AnchorMax = $"0.987 0.11", OffsetMax = "0 0" },
                Button = { Color = "0.56 0.00 0.00 0.7", Close = "MainStats" },
                Text = { Text = $"ЗАКРЫТЬ", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Top");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.6 0.02", AnchorMax = $"0.83 0.11", OffsetMax = "0 0" },
                Button = { Color = "0.00 0.35 0.00 1", Command = $"stats profile {player.userID} 0" },
                Text = { Text = $"МОЙ ПРОФИЛЬ", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Top");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.523 0.02", AnchorMax = $"0.593 0.11", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0.3", Command = DB.Count() > (page + 1) * 10 ? $"stats skip {page + 1}" : "" },
                Text = { Text = $">", Color = DB.Count() > (page + 1) * 10 ? "1 1 1 1" : "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Top");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.446 0.02", AnchorMax = $"0.516 0.11", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0.3", Command = "" },
                Text = { Text = $"{page + 1}", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Top");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.37 0.02", AnchorMax = $"0.44 0.11", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0.3", Command = page >= 1 ? $"stats skip {page - 1}" : "" },
                Text = { Text = $"<", Color = page >= 1 ? "1 1 1 1" : "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Top");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = $"1 0.22", OffsetMax = "0 0" },
                Image = { Color = HexToRustFormat("#EC0C0C4C") }
            }, Layer, "InfoTop");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.01 0.01", AnchorMax = $"0.99 0.99", OffsetMax = "0 0" },
                Text = { Text = "Очки даются:\nУбийство +25, добыча руды +3, разрушение бочки +3, сбитие вертолета +1000, уничтожение танка +1000\nОчки отнимаются:\nСмерть и самоубийство -30\nНаграды выдаются после вайпа на наш донат магазин!", Color = "1 1 1 0.8", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "InfoTop");

            float width = 0.98f, height = 0.063f, startxBox = 0.01f, startyBox = 0.95f - height, xmin = startxBox, ymin = startyBox, z = 0;
            var items = from item in DB orderby item.Value.Points descending select item;
            foreach (var check in items.Skip(page * 10).Take(10))
            {
                z++;
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMin = "2 1", OffsetMax = "-2 -1" },
                    Image = { Color = "0 0 0 0" }
                }, Layer, "PlayerTop");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = $"0.8 1", OffsetMax = "0 0" },
                    Image = { Color = "0.3 0.3 0.3 0.8" }
                }, "PlayerTop");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.08 1", OffsetMax = "0 0" },
                    Text = { Text = $"{z + page * 10}", Color = "1 1 1 0.8", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" }
                }, "PlayerTop");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0.115 0", AnchorMax = $"0.4 1", OffsetMax = "0 0" },
                    Text = { Text = $"{check.Value.DisplayName}", Color = "1 1 1 0.8", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "PlayerTop");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0.4 0", AnchorMax = $"0.575 1", OffsetMax = "0 0" },
                    Text = { Text = $"", Color = "1 1 1 0.8", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "PlayerTop");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0.6 0", AnchorMax = $"0.7 1", OffsetMax = "0 0" },
                    Text = { Text = $"{check.Value.Points}", Color = "1 1 1 0.8", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "PlayerTop");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0.5 0", AnchorMax = $"0.6 1", OffsetMax = "0 0" },
                    Text = { Text = $"{check.Value.Farm}", Color = "1 1 1 0.8", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "PlayerTop");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0.7 0", AnchorMax = $"0.8 1", OffsetMax = "0 0" },
                    Text = { Text = $"{check.Value.Kill}/{check.Value.Death}", Color = "1 1 1 0.8", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "PlayerTop");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"0.805 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                    Button = { Color = "0.3 0.3 0.3 0.80", Command = $"stats profile {check.Key} {z + page * 10}" },
                    Text = { Text = $"ПРОФИЛЬ", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "PlayerTop");

                xmin += width;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height;
                }
            }

            /*float width1 = 0.785f, height1 = 0.063f, startxBox1 = 0.01f, startyBox1 = 0.95f - height1, xmin1 = startxBox1, ymin1 = startyBox1;
            if (page == 0)
            {
                for (int x = 0; x < DB.Take(10).Count(); x++)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + height1 * 1}", OffsetMin = "2 1", OffsetMax = "-2 -1" },
                        Image = { Color = "0 0 0 0" }
                    }, Layer, "PlayerTop");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = $"0.5 0", AnchorMax = $"0.6 1", OffsetMax = "0 0" },
                        Text = { Text = $"{Bonus.ElementAt(x)}₽", Color = "1 1 1 0.8", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                    }, "PlayerTop");

                    xmin1 += width1;
                    if (xmin1 + width1 >= 1)
                    {
                        xmin1 = startxBox1;
                        ymin1 -= height1;
                    }
                }
            }*/

            CuiHelper.AddUi(player, container);
        }
        [ChatCommand("stats")]
        void cmdhuy(BasePlayer player)
        {
            ProfileUI(player, player.UserIDString, 0);
        }

        void ProfileUI(BasePlayer player, string SteamID, int z)
        {
            CuiHelper.DestroyUi(player, "MainStats");
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Overlay", "MainStats");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "MainStats");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.3 0.3", AnchorMax = "0.7 0.7", OffsetMax = "0 0" },
                Image = { Color = "0.2 0.2 0.2 1" }
            }, "MainStats", Layer);

            var target = DB[SteamID];
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.01 0.8", AnchorMax = $"0.99 0.99", OffsetMax = "0 0" },
                Text = { Text = $"<b><size=25>{target.DisplayName.ToUpper()}</size></b>\nЗдесь вы можете посмотреть подробно статискику об игроке!", Color = "1 1 1 0.8", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.51 0.131", AnchorMax = $"0.518 0.777", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.1" }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.682 0.02", AnchorMax = $"0.83 0.11", OffsetMax = "0 0" },
                Button = { Color = "1 1 1 0.1", Command = "stats back" },
                Text = { Text = $"НАЗАД", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.837 0.02", AnchorMax = $"0.987 0.11", OffsetMax = "0 0" },
                Button = { Color = "0.56 0.00 0.00 0.7", Close = "MainStats" },
                Text = { Text = $"ЗАКРЫТЬ", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.015 0.51", AnchorMax = $"0.21 0.777", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.1" }
            }, Layer, "Avatar");
            
            container.Add(new CuiElement
            {
                Parent = "Avatar",
                Components =
                {
                    new CuiRawImageComponent { Png = (string) plugins.Find("ImageLibrary").Call("GetImage", SteamID) },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "7 7", OffsetMax = "-7 -7" }
                }
            });

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.22 0.7", AnchorMax = $"0.5 0.777", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.1" }
            }, Layer, "Place");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.03 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Text = { Text = $"Место в топе: {z}", Color = "1 1 1 0.8", Align = TextAnchor.MiddleLeft, FontSize = 10, Font = "robotocondensed-regular.ttf" }
            }, "Place");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.22 0.603", AnchorMax = $"0.5 0.68", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.1" }
            }, Layer, "Points");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.03 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Text = { Text = $"Очков: {target.Points}", Color = "1 1 1 0.8", Align = TextAnchor.MiddleLeft, FontSize = 10, Font = "robotocondensed-regular.ttf" }
            }, "Points");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.22 0.51", AnchorMax = $"0.5 0.587", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.1" }
            }, Layer, "Status");

            var status = target.IsConnected == true ? "онлайн" : "офлайн";
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.03 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Text = { Text = $"Статус: {status}", Color = "1 1 1 0.8", Align = TextAnchor.MiddleLeft, FontSize = 10, Font = "robotocondensed-regular.ttf" }
            }, "Status");

            float width1 = 0.494f, height1 = 0.0939f, startxBox1 = 0.01f, startyBox1 = 0.5f - height1, xmin1 = startxBox1, ymin1 = startyBox1;
            foreach (var check in target.Settings)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + height1 * 1}", OffsetMin = "2 2", OffsetMax = "-2 -2" },
                    Image = { Color = "1 1 1 0.1" }
                }, Layer, "Count");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0.03 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                    Text = { Text = $"{check.Key.Replace("Kill", "УБИЙСТВ").Replace("Death", "СМЕРТЕЙ").Replace("Farm", "ФАРМ")}", Color = "1 1 1 0.8", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "Count");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.97 1", OffsetMax = "0 0" },
                    Text = { Text = $"{check.Value}", Color = "1 1 1 0.8", Align = TextAnchor.MiddleRight, FontSize = 14, Font = "robotocondensed-regular.ttf" }
                }, "Count");

                xmin1 += width1;
                if (xmin1 + width1 >= 0)
                {
                    xmin1 = startxBox1;
                    ymin1 -= height1;
                }
            }

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.014 0.133", AnchorMax = $"0.5 0.213", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.1" }
            }, Layer, "KD");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.03 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Text = { Text = $"К/Д СТАТУС", Color = "1 1 1 0.8", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "KD");

            var kd = target.Settings["Death"] == 0 ? target.Settings["Kill"] : (float)Math.Round(((float)target.Settings["Kill"]) / target.Settings["Death"], 1);
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.97 1", OffsetMax = "0 0" },
                Text = { Text = $"{kd}", Color = "1 1 1 0.8", Align = TextAnchor.MiddleRight, FontSize = 14, Font = "robotocondensed-regular.ttf" }
            }, "KD");

            float width = 0.155f, height = 0.22f, startxBox = 0.523f, startyBox = 0.785f - height, xmin = startxBox, ymin = startyBox;
            foreach (var check in target.Res)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMin = "2 2", OffsetMax = "-2 -2" },
                    Image = { Color = "1 1 1 0.1" }
                }, Layer, "Images");

                container.Add(new CuiElement
                {
                    Parent = "Images",
                    Components =
                    {
                        new CuiRawImageComponent { Png = StatsSystemImageList[check.Key] },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" }
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.97 1", OffsetMax = "0 0" },
                    Text = { Text = $"{check.Value}", Color = "1 1 1 0.8", Align = TextAnchor.LowerRight, FontSize = 14, Font = "robotocondensed-regular.ttf" }
                }, "Images");

                xmin += width;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height;
                }
            }

            CuiHelper.AddUi(player, container);
        }
        #endregion
        
        private static string HexToRustFormat(string hex)
        { 
            Color color;
            ColorUtility.TryParseHtmlString(hex, out color);
            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }

        class FileManager : MonoBehaviour
        {
            int loaded = 0;
            int needed = 0;

            Dictionary<string, FileInfo> files = new Dictionary<string, FileInfo>();
            DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetFile("StatsSystem/Images");

            private class FileInfo
            {
                public string Url;
                public string Png;
            }

            public void SaveData()
            {
                dataFile.WriteObject(files);
            }

            public void WipeData()
            {
                Interface.Oxide.DataFileSystem.WriteObject("StatsSystem/Images", new sbyte());
                Interface.Oxide.ReloadPlugin(Instance.Title);
            }

            public string GetPng(string name)
            {
                if (!files.ContainsKey(name)) return null;
                return files[name].Png;
            }

            private void Awake()
            {
                LoadData();
            }

            void LoadData()
            {
                try
                {
                    files = dataFile.ReadObject<Dictionary<string, FileInfo>>();
                }
                catch
                {
                    files = new Dictionary<string, FileInfo>();
                }
            }

            public IEnumerator LoadFile(string name, string url)
            {
                if (files.ContainsKey(name) && files[name].Url == url && !string.IsNullOrEmpty(files[name].Png)) yield break;
                files[name] = new FileInfo() {Url = url};
                needed++;

                yield return StartCoroutine(LoadImageCoroutine(name, url));
            }

            IEnumerator LoadImageCoroutine(string name, string url)
            {
                using (WWW www = new WWW(url))
                {
                    yield return www;
                    {
                        if (string.IsNullOrEmpty(www.error))
                        {
                            var entityId = CommunityEntity.ServerInstance.net.ID;
                            var crc32 = FileStorage.server.Store(www.bytes, FileStorage.Type.png, entityId).ToString();
                            files[name].Png = crc32;
                        }
                    }
                }

                loaded++;
            }
        }
    }
}