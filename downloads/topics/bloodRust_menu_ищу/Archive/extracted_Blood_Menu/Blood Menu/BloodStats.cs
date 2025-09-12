using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Newtonsoft.Json;
using System.Globalization;
using Rust;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("BloodStats", "[LimePlugin] Chibubrik", "1.0.0")]
    class BloodStats : RustPlugin
    {
        #region Вар
        [PluginReference] Plugin ImageLibrary, BloodQuests;

        string Layer = "Stat_UI";

        Dictionary<ulong, DBStat> DB = new Dictionary<ulong, DBStat>();

        Dictionary<ulong, int> PlayerPage = new Dictionary<ulong, int>();
        #endregion

        #region Класс
        public class DBStat
        {
            [JsonProperty("Ник игрока")] public string DisplayName;
            [JsonProperty("Кол-во убийств")] public float Kill;
            [JsonProperty("Кол-во смертей")] public float Death;
            [JsonProperty("Общее кол-во добытых ресурсов")] public float Gather;
            [JsonProperty("Список собранных ресурсов и их кол-во")] public Dictionary<string, float> Res;
            [JsonProperty("Список скрафченых предметов и их кол-во")] public Dictionary<string, float> Craft;
            [JsonProperty("История убийств/смертей")] public Dictionary<string, string> History;

            public DBStat()
            {
                Kill = 0;
                Death = 0;
                Gather = 0;
                Res = new Dictionary<string, float>();
                Craft = new Dictionary<string, float>();
                History = new Dictionary<string, string>();
            }
        }

        Dictionary<string, string> UserStat(BasePlayer player)
        {
            Dictionary<string, string> statUser = new Dictionary<string, string>()
            {
                ["Убийств"] = $"{DB[player.userID].Kill}",
                ["Смертей"] = $"{DB[player.userID].Death}",
                ["Соотношение"] = $"{(float)Math.Round((float)DB[player.userID].Kill / DB[player.userID].Death, 1)}",
                ["EXP Добыто"] = $"{BloodQuests?.Call("PlayerEXPQuests", (ulong)player.userID)}"
            };

            return statUser;
        }
        #endregion

        #region 
        #endregion

        #region Хуки
        void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            CreateDataBase(player);
            if (DB[player.userID].DisplayName == null)
                DB[player.userID].DisplayName = player.displayName;
            PlayerPage[player.userID] = 1;
        }

        void OnPlayerDisconnected(BasePlayer player) => SaveDataBase(player.userID);

        void Unload()
        {
            foreach (var check in DB)
                SaveDataBase(check.Key);
        }

        void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser == null || player == null || item == null) return;
            ProgressRes(player, item.info.shortname, item.amount);
            return;
        }

        void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser == null || player == null || item == null) return;
            ProgressRes(player, item.info.shortname, item.amount);
            return;
        }

        void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            if (collectible == null || player == null) return;
            foreach (var item in collectible.itemList)
                ProgressRes(player, item.itemDef.shortname, item.amount);
            return;
        }

        void OnItemCraftFinished(ItemCraftTask task, Item item, ItemCrafter crafter)
        {
            if (task == null || item == null) return;
            ProgressCraft(crafter.owner, item.info.shortname, item.amount);
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (info == null || player == null || player.IsNpc || info.InitiatorPlayer == null || info.InitiatorPlayer.IsNpc) return;

            bool isSuicide = info == null || info.damageTypes.Has(DamageType.Suicide) || info.Initiator == player;
            if (isSuicide)
            {
                DB[player.userID].History.Add($"{DB[player.userID].History.Count() + 1}<b>ВЫ</b> совершили самоубийство", "");
            }
            else if (info.InitiatorPlayer != null)
            {
                var killer = info.InitiatorPlayer;
                string weaponName = GetWeaponName(info);
                float distance = Vector3.Distance(player.transform.position, killer.transform.position);

                if (killer != player)
                {
                    if (DB.ContainsKey(killer.userID))
                    {
                        DB[killer.userID].Kill++;
                        DB[killer.userID].History.Add($"<b>ВЫ</b> убили <b>{player.displayName}</b> с помощью <b>{weaponName}</b>", $"x{distance:F1}м");
                    }
                }
                if (DB.ContainsKey(player.userID))
                {
                    DB[player.userID].Death++;
                    DB[player.userID].History.Add($"<b>ВАС</b> убил <b>{killer.displayName}</b> с помощью <b>{weaponName}</b> [{distance:F1}м]", $"x{distance:F1}м");
                }
            }
        }
        #endregion

        #region Дата
        void CreateDataBase(BasePlayer player)
        {
            var DataBase = Interface.Oxide.DataFileSystem.ReadObject<DBStat>($"{Name}/{player.userID}");

            if (!DB.ContainsKey(player.userID))
                DB.Add(player.userID, new DBStat());

            DB[player.userID] = DataBase ?? new DBStat();
        }

        void SaveDataBase(ulong userId) => Interface.Oxide.DataFileSystem.WriteObject($"{Name}/{userId}", DB[userId]);
        #endregion

        #region Прогресс
        void ProgressRes(BasePlayer player, string shortname, float amount)
        {
            if (!DB[player.userID].Res.ContainsKey(shortname))
                DB[player.userID].Res.Add(shortname, amount);
            else
                DB[player.userID].Res[shortname] += amount;

            DB[player.userID].Gather += amount;
        }

        void ProgressCraft(BasePlayer player, string shortname, float amount)
        {
            if (!DB[player.userID].Craft.ContainsKey(shortname))
                DB[player.userID].Craft.Add(shortname, amount);
            else
                DB[player.userID].Craft[shortname] += amount;
        }
        #endregion

        #region Команды
        [ConsoleCommand("stat")]
        void ConsoleStat(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player != null && args.HasArgs(1))
            {
                if (args.Args[0] == "res")
                {
                    PlayerPage[player.userID] = 1;
                    StatUI(player, "res");
                }
                if (args.Args[0] == "craft")
                {
                    PlayerPage[player.userID] = 1;
                    StatUI(player, "craft");
                }
                if (args.Args[0] == "history")
                {
                    PlayerPage[player.userID] = 1;
                    StatUI(player, "history");
                }
                if (args.Args[0] == "top")
                {
                    StatUI(player, "top");
                }
                if (args.Args[0] == "page")
                {
                    var page = int.Parse(args.Args[2]);
                    PlayerPage[player.userID] = page;
                    StatUI(player, args.Args[1], PlayerPage[player.userID]);
                }
                if (args.Args[0] == "help")
                {
                    ShowUIInforamtion(player);
                }
            }
        }
        #endregion

        #region Интерфейс
        void StatUI(BasePlayer player, string name = "res", int page = 1)
        {
            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Menu_Block", Layer);

            var colorStat = name == "res" || name == "craft" || name == "history" ? HexToCuiColor("#b2a9a3", 100) : "1 1 1 0.04";
            string colorText = name == "res" || name == "craft" || name == "history" ? $"<b><color=#45403b>Моя статистика</color></b>" : "Моя статистика";
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0 0.9", AnchorMax = $"0.496 1", OffsetMax = "0 0" },
                Button = { Color = colorStat, Command = "stat res", Material = "assets/content/ui/uibackgroundblur.mat" },
                Text = { Text = colorText, Color = HexToCuiColor("#cec5bb", 100), Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" }
            }, Layer, "Stat");

            var colorTop = name == "top" ? HexToCuiColor("#b2a9a3", 100) : "1 1 1 0.04";
            string colorTextTop = name == "top" ? $"<b><color=#45403b>Топ игроков</color></b>" : "Топ игроков";
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.504 0.9", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Button = { Color = colorTop, Command = "stat top", Material = "assets/content/ui/uibackgroundblur.mat" },
                Text = { Text = colorTextTop, Color = HexToCuiColor("#cec5bb", 100), Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" }
            }, Layer, "Top");

            if (name == "res" || name == "craft" || name == "history")
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"0 0.67", AnchorMax = $"0.175 0.887", OffsetMax = "0 0" },
                    Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
                }, Layer, "Avatar");

                container.Add(new CuiElement
                {
                    Parent = "Avatar",
                    Components = {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", player.UserIDString) },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = $"1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" },
                    }
                });

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"0.182 0.67", AnchorMax = $"1 0.887", OffsetMax = "0 0" },
                    Image = { Color = "1 1 1 0" }
                }, Layer, "Info");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"0 0.7", AnchorMax = $"1 1", OffsetMax = "0 0" },
                    Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
                }, "Info", "Name_SteamID");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0.02 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                    Text = { Text = player.displayName, Color = HexToCuiColor("#cec5bb", 100), Align = TextAnchor.MiddleLeft, FontSize = 14, Font = "robotocondensed-bold.ttf" }
                }, "Name_SteamID");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.98 1", OffsetMax = "0 0" },
                    Text = { Text = player.userID.ToString(), Color = HexToCuiColor("#7f7d7d", 100), Align = TextAnchor.MiddleRight, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "Name_SteamID");

                float width = 0.2397f, height = 0.635f, startxBox = 0f, startyBox = 0.635f - height, xmin = startxBox, ymin = startyBox;
                foreach (var check in UserStat(player))
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}" },
                        Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
                    }, "Info", "UserStat");
                    xmin += width + 0.013f;

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = $"0.06 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                        Text = { Text = $"<size=12><color=#7f7d7d>{check.Key}</color></size>\n<b>{check.Value}</b>", Color = HexToCuiColor("#cec5bb", 100), Align = TextAnchor.MiddleLeft, FontSize = 22, Font = "robotocondensed-regular.ttf" }
                    }, "UserStat");
                }

                var colorRes = name == "res" ? HexToCuiColor("#b2a9a3", 100) : "1 1 1 0.04";
                string colorTextRes = name == "res" ? $"<b><color=#45403b>Сбор ресурсов</color></b>" : "Сбор ресурсов";
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"0 0.557", AnchorMax = $"0.32 0.657", OffsetMax = "0 0" },
                    Button = { Color = colorRes, Command = "stat res", Material = "assets/content/ui/uibackgroundblur.mat" },
                    Text = { Text = colorTextRes, Color = HexToCuiColor("#cec5bb", 100), Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" }
                }, Layer, "Res");

                var colorCraft = name == "craft" ? HexToCuiColor("#b2a9a3", 100) : "1 1 1 0.04";
                string colorTextCraft = name == "craft" ? $"<b><color=#45403b>Крафты</color></b>" : "Крафты";
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"0.33 0.557", AnchorMax = $"0.67 0.657", OffsetMax = "0 0" },
                    Button = { Color = colorCraft, Command = "stat craft", Material = "assets/content/ui/uibackgroundblur.mat" },
                    Text = { Text = colorTextCraft, Color = HexToCuiColor("#cec5bb", 100), Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" }
                }, Layer, "Craft");

                var colorHistory = name == "history" ? HexToCuiColor("#b2a9a3", 100) : "1 1 1 0.04";
                string colorTextHistory = name == "history" ? $"<b><color=#45403b>Убийства/Смерти</color></b>" : "Убийства/Смерти";
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"0.68 0.557", AnchorMax = $"1 0.657", OffsetMax = "0 0" },
                    Button = { Color = colorHistory, Command = "stat history", Material = "assets/content/ui/uibackgroundblur.mat" },
                    Text = { Text = colorTextHistory, Color = HexToCuiColor("#cec5bb", 100), Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" }
                }, Layer, "History");

                CuiHelper.AddUi(player, container);

            }
            if (name == "res")
            {
                ResUI(player, page);
                PagerUI(player, name, page);
            }
            if (name == "craft")
            {
                CraftUI(player, page);
                PagerUI(player, name, page);
            }
            if (name == "history")
            {
                HistoryUI(player, page);
                PagerUI(player, name, page);
            }
            if (name == "top")
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0.82", AnchorMax = "1 0.888", OffsetMax = "0 0" },
                    Image = { Color = "1 1 1 0.1" }
                }, Layer, "Top_Block");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.015 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = $"Игрок", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", Color = HexToCuiColor("#cec5bb", 100), FontSize = 12 }
                }, "Top_Block");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.45 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = $"Убийств", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", Color = HexToCuiColor("#cec5bb", 100), FontSize = 12 }
                }, "Top_Block");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.6 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = $"Смертей", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", Color = HexToCuiColor("#cec5bb", 100), FontSize = 12 }
                }, "Top_Block");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.75 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = $"Добыто", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", Color = HexToCuiColor("#cec5bb", 100), FontSize = 12 }
                }, "Top_Block");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.95 1", OffsetMax = "0 0" },
                    Text = { Text = $"K/D", Align = TextAnchor.MiddleRight, Font = "robotocondensed-regular.ttf", Color = HexToCuiColor("#cec5bb", 100), FontSize = 12 }
                }, "Top_Block");

                float width = 1f, height = 0.088f, startxBox = 0f, startyBox = 0.82f - height, xmin = startxBox, ymin = startyBox;
                var pl = from item in DB orderby item.Value.Kill descending select item;
                var check = pl.Skip((page - 1) * 8).Take(8);
                for (int z = 0; z < 8; z++)
                {
                    var color = "1 1 1 0.02";
                    if (z % 2 == 0)
                        color = "1 1 1 0.04";

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMin = "0 0", OffsetMax = "0 0" },
                        Image = { Color = color, Material = "assets/content/ui/uibackgroundblur.mat" }
                    }, Layer, $"{z}");

                    xmin += width;
                    if (xmin + width >= 0)
                    {
                        xmin = startxBox;
                        ymin -= height;
                    }
                }
                for (int z = 0; z < check.Count(); z++)
                {
                    container.Add(new CuiElement
                    {
                        Parent = $"{z}",
                        Components = {
                            new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", check.ElementAt(z).Key.ToString()) },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = $"0.07 1", OffsetMin = "5 5", OffsetMax = "-5 -5" },
                        }
                    });

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.07 0.33", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Text = { Text = check.ElementAt(z).Value.DisplayName, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf", Color = HexToCuiColor("#cec5bb", 100), FontSize = 12 }
                    }, $"{z}");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.07 0.16", AnchorMax = "1 0.48", OffsetMax = "0 0" },
                        Text = { Text = $"{check.ElementAt(z).Key}", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", Color = HexToCuiColor("#7f7d7d", 100), FontSize = 10 }
                    }, $"{z}");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.45 0", AnchorMax = "0.515 1", OffsetMax = "0 0" },
                        Text = { Text = $"{check.ElementAt(z).Value.Kill}", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = HexToCuiColor("#cec5bb", 100), FontSize = 14 }
                    }, $"{z}");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.6 0", AnchorMax = "0.67 1", OffsetMax = "0 0" },
                        Text = { Text = $"{check.ElementAt(z).Value.Death}", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = HexToCuiColor("#cec5bb", 100), FontSize = 14 }
                    }, $"{z}");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.75 0", AnchorMax = "0.815 1", OffsetMax = "0 0" },
                        Text = { Text = $"{check.ElementAt(z).Value.Gather}", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = HexToCuiColor("#cec5bb", 100), FontSize = 14 }
                    }, $"{z}");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.885 0", AnchorMax = "0.985 1", OffsetMax = "0 0" },
                        Text = { Text = $"{(float)Math.Round((float)check.ElementAt(z).Value.Kill / check.ElementAt(z).Value.Death, 1)}", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = HexToCuiColor("#cec5bb", 100), FontSize = 14 }
                    }, $"{z}");
                }

                CuiHelper.AddUi(player, container);
                PagerUI(player, name, page);
            }
        }

        void ResUI(BasePlayer player, int page = 0)
        {
            CuiHelper.DestroyUi(player, "Res_Block");
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.545", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, "Res_Block");

            var items = from item in DB[player.userID].Res orderby item.Value descending select item;
            var check = items.Skip((page - 1) * 7).Take(7);
            float width = 1f, height = 0.113f, startxBox = 0f, startyBox = 1f - height, xmin = startxBox, ymin = startyBox;
            for (int z = 0; z < 7; z++)
            {
                var color = "1 1 1 0.02";
                if (z % 2 == 0)
                    color = "1 1 1 0.04";

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMin = "0 0", OffsetMax = "0 0" },
                    Image = { Color = color, Material = "assets/content/ui/uibackgroundblur.mat" }
                }, "Res_Block", $"{z}");

                xmin += width;
                if (xmin + width >= 0)
                {
                    xmin = startxBox;
                    ymin -= height;
                }
            }
            for (int z = 0; z < check.Count(); z++)
            {
                container.Add(new CuiElement
                {
                    Parent = $"{z}",
                    Components =
                    {
                        new CuiImageComponent { ItemId = ItemManager.FindItemDefinition(check.ElementAt(z).Key).itemid, SkinId = 0, Color = "1 1 1 1" },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0.05 1", OffsetMin = "5 5", OffsetMax = "-5 -5" },
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0.055 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                    Text = { Text = $"{ItemManager.FindDefinitionByPartialName(check.ElementAt(z).Key).displayName.english}", Color = HexToCuiColor("#cec5bb", 100), Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, $"{z}");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.985 1", OffsetMax = "0 0" },
                    Text = { Text = $"x{check.ElementAt(z).Value}", Color = HexToCuiColor("#7f7d7d", 100), Align = TextAnchor.MiddleRight, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, $"{z}");
            }

            CuiHelper.AddUi(player, container);
        }

        void CraftUI(BasePlayer player, int page = 0)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.545", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, "Craft_Block");

            var items = from item in DB[player.userID].Craft orderby item.Value descending select item;
            var check = items.Skip((page - 1) * 7).Take(7);
            float width = 1f, height = 0.113f, startxBox = 0f, startyBox = 1f - height, xmin = startxBox, ymin = startyBox;
            for (int z = 0; z < 7; z++)
            {
                var color = "1 1 1 0.02";
                if (z % 2 == 0)
                    color = "1 1 1 0.04";

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMin = "0 0", OffsetMax = "0 0" },
                    Image = { Color = color, Material = "assets/content/ui/uibackgroundblur.mat" }
                }, "Craft_Block", $"{z}");

                xmin += width;
                if (xmin + width >= 0)
                {
                    xmin = startxBox;
                    ymin -= height;
                }
            }
            for (int z = 0; z < check.Count(); z++)
            {
                container.Add(new CuiElement
                {
                    Parent = $"{z}",
                    Components =
                    {
                        new CuiImageComponent { ItemId = ItemManager.FindItemDefinition(check.ElementAt(z).Key).itemid, SkinId = 0, Color = "1 1 1 1" },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0.05 1", OffsetMin = "5 5", OffsetMax = "-5 -5" },
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0.055 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                    Text = { Text = $"{ItemManager.FindDefinitionByPartialName(check.ElementAt(z).Key).displayName.english}", Color = HexToCuiColor("#cec5bb", 100), Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, $"{z}");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.985 1", OffsetMax = "0 0" },
                    Text = { Text = $"x{check.ElementAt(z).Value}", Color = HexToCuiColor("#7f7d7d", 100), Align = TextAnchor.MiddleRight, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, $"{z}");
            }

            CuiHelper.AddUi(player, container);
        }

        void HistoryUI(BasePlayer player, int page = 0)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.545", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, "History_Block");

            var items = DB[player.userID].History.Reverse();
            var check = items.Skip((page - 1) * 7).Take(7);
            float width = 1f, height = 0.113f, startxBox = 0f, startyBox = 1f - height, xmin = startxBox, ymin = startyBox;
            for (int z = 0; z < 7; z++)
            {
                var color = "1 1 1 0.02";
                if (z % 2 == 0)
                    color = "1 1 1 0.04";

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMin = "0 0", OffsetMax = "0 0" },
                    Image = { Color = color, Material = "assets/content/ui/uibackgroundblur.mat" }
                }, "History_Block", $"{z}");

                xmin += width;
                if (xmin + width >= 0)
                {
                    xmin = startxBox;
                    ymin -= height;
                }
            }
            for (int z = 0; z < check.Count(); z++)
            {
                var text = "";
                if (check.ElementAt(z).Value == "")
                    text = Regex.Replace(check.ElementAt(z).Key, "[\\d]", "");
                else
                    text = check.ElementAt(z).Key;
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0.015 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                    Text = { Text = $"{text}", Color = HexToCuiColor("#cec5bb", 100), Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, $"{z}");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.985 1", OffsetMax = "0 0" },
                    Text = { Text = $"{check.ElementAt(z).Value}", Color = HexToCuiColor("#7f7d7d", 100), Align = TextAnchor.MiddleRight, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, $"{z}");
            }

            CuiHelper.AddUi(player, container);
        }

        void PagerUI(BasePlayer player, string name, int currentPage)
        {
            CuiHelper.DestroyUi(player, "footer");
            var container = new CuiElementContainer();
            List<string> displayedPages = GetDisplayedPages(player, name, currentPage);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.64 0.098" },
                Image = { Color = "0 0 0 0" }
            }, Layer, "footer");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.65 0", AnchorMax = $"1 0.1", OffsetMax = "0 0" },
                Button = { Color = "1 1 1 0.04", Command = "stat help", Material = "assets/content/ui/uibackgroundblur.mat" },
                Text = { Text = "Информация", Font = "robotocondensed-bold.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#cec5bb", 40) }
            }, Layer);

            float width = 0.13f, height = 1f, startxBox = 0f, startyBox = 1f - height, xmin = startxBox, ymin = startyBox;
            for (var z = 0; z < 7; z++)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMax = "0 0" },
                    Button = { Color = "1 1 1 0.01", Material = "assets/content/ui/uibackgroundblur.mat" },
                }, "footer");
                xmin += width + 0.0148f;
            }

            float x = 0f;
            for (int i = 0; i < displayedPages.Count; i++)
            {
                string page = displayedPages[i];

                if (page == "..")
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = $"{x} 0", AnchorMax = $"{x + 0.13f} 1", OffsetMax = "0 0" },
                        Text = { Text = $"..", Color = "1 1 1 0.2", Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-regular.ttf" }
                    }, "footer");
                }
                else
                {
                    int pageNum = int.Parse(page);
                    string buttonColor = pageNum == currentPage ? HexToCuiColor("#b2a9a3", 100) : "1 1 1 0.04";
                    string text = pageNum == currentPage ? $"<b><color=#45403b>{page}</color></b>" : $"{page}";

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = $"{x} 0", AnchorMax = $"{x + 0.13f} 1", OffsetMax = "0 0" },
                        Button = { Color = buttonColor, Command = $"stat page {name} {page}", Material = "assets/content/ui/uibackgroundblur.mat" },
                        Text = { Text = text, Color = "1 1 1 0.2", Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-regular.ttf" }
                    }, "footer");
                }

                x += 0.145f;
            }

            CuiHelper.AddUi(player, container);
        }

        private List<string> GetDisplayedPages(BasePlayer player, string name, int currentPage)
        {
            var result = new List<string>();
            var wItems = name == "res" ? DB[player.userID].Res.Count() : name == "craft" ? DB[player.userID].Craft.Count() : name == "history" ? DB[player.userID].History.Count() : DB.Count();
            int count = name == "top" ? 8 : 7;
            int totalPages = (int)Math.Ceiling((decimal)wItems / (decimal)count);

            if (totalPages <= 7)
            {
                for (int i = 1; i <= totalPages; i++)
                {
                    result.Add(i.ToString());
                }
                return result;
            }

            result.Add("1");

            if (currentPage <= 4)
            {
                for (int i = 2; i <= 5; i++)
                {
                    result.Add(i.ToString());
                }
                result.Add("..");
                result.Add(totalPages.ToString());
            }
            else if (currentPage >= totalPages - 3)
            {
                result.Add("..");
                for (int i = totalPages - 4; i <= totalPages - 1; i++)
                {
                    result.Add(i.ToString());
                }
                result.Add(totalPages.ToString());
            }
            else
            {
                result.Add("..");
                for (int i = currentPage - 1; i <= currentPage + 1; i++)
                {
                    result.Add(i.ToString());
                }
                result.Add("..");
                result.Add(totalPages.ToString());
            }

            return result;
        }

        void ShowUIInforamtion(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Information_UI");
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.3", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, "Overlay", "Information_UI");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0.9", Close = "Information_UI" }
            }, "Information_UI");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.3 0.3", AnchorMax = "0.7 0.7", OffsetMax = "0 0" },
                Text = { Text = "<b><size=30>СТАТИСТИКА</size></b>\n\nСтатистика отображается только за текущий вайп, а\nвместе с вайпом - будет сброшена.", Color = "1 1 1 0.2", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" }
            }, "Information_UI");

            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Хелпер
        public string HexToCuiColor(string HEX, float Alpha = 100)
        {
            if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

            var str = HEX.Trim('#');
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {Alpha / 100}";
        }

        private string GetWeaponName(HitInfo info)
        {
            if (info == null) return "Неизвестное оружие";

            if (info.Initiator is BasePlayer killer && killer.GetActiveItem() != null)
            {
                Item activeItem = killer.GetActiveItem();
                return activeItem.info.displayName.english ?? "Неизвестное оружие";
            }

            return "Неизвестное оружие";
        }
        #endregion
    }
}