using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("BloodPass", "[LimePlugin] Chibubrik", "1.0.0")]
    class BloodPass : RustPlugin
    {
        #region Вар
        string Layer = "Pass_UI";

        [PluginReference] Plugin ImageLibrary;

        Dictionary<ulong, DataBase> DB;
        #endregion

        #region Класс
        public class Settings
        {
            [JsonProperty("Основное название")] public string DisplayName;
            [JsonProperty("Название сервера")] public string ServerName;
        }

        public class PassSettings
        {
            [JsonProperty("Уровень")] public int Level;
            [JsonProperty("Сколько максимум игрок может получить вещей из списка?")] public int Count;
            [JsonProperty("Список заданий")] public List<MainSettings> mains;
            [JsonProperty("Список наград")] public List<ItemsList> items;
        }

        public class MainSettings
        {
            [JsonProperty("Название задания")] public string DisplayName;
            [JsonProperty("Информация о задании")] public string Description;
            [JsonProperty("Короткое название задачи")] public string ShortName;
            [JsonProperty("Количество предмета")] public int Amount;
        }

        public class ItemsList
        {
            [JsonProperty("Название предмета или услуги")] public string DisplayName;
            [JsonProperty("Короткое название предмета")] public string ShortName;
            [JsonProperty("Шанс выпадения предмета")] public int DropChance;
            [JsonProperty("Дополнительная команда")] public string Command;
            [JsonProperty("Изображение")] public string Url;
            [JsonProperty("Минимальное количество при выпадени")] public int AmountMin;
            [JsonProperty("Максимальное Количество при выпадени")] public int AmountMax;
        }

        public class DataBase
        {
            [JsonProperty("Уровень игрока")] public int Level = 1;
            [JsonProperty("Список выполняемых заданий")] public Dictionary<string, PlayerProgress> Progress = new Dictionary<string, PlayerProgress>();
        }

        public class PlayerProgress
        {
            [JsonProperty("Выполнено ли задание")] public bool Enable;
            [JsonProperty("Количество")] public int Amount;
        }
        #endregion

        #region Конфиг
        public Configuration config;
        public class Configuration
        {
            [JsonProperty("Основные настройки")] public Settings settings = new Settings();
            [JsonProperty("Список заданий и наград")] public List<PassSettings> passSettings;
            public static Configuration GetNewConfig()
            {
                return new Configuration
                {
                    settings = new Settings
                    {
                        DisplayName = "SERVERNAME PASS",
                        ServerName = "SERVERNAME"
                    },
                    passSettings = new List<PassSettings>
                    {
                        new PassSettings
                        {
                            Level = 1,
                            Count = 3,
                            mains = new List<MainSettings>
                            {
                                new MainSettings
                                {
                                    DisplayName = "Листики",
                                    Description = "Соберите ткань",
                                    ShortName = "cloth",
                                    Amount = 500,
                                },
                                new MainSettings
                                {
                                    DisplayName = "Царь горы",
                                    Description = "Убейте игроков",
                                    ShortName = "player",
                                    Amount = 5,
                                },
                                new MainSettings
                                {
                                    DisplayName = "Добыть дерево",
                                    Description = "Добыть 1000 дерева",
                                    ShortName = "wood",
                                    Amount = 1000,
                                },
                                new MainSettings
                                {
                                    DisplayName = "Добыть камень",
                                    Description = "Добыть 2000 камня",
                                    ShortName = "stones",
                                    Amount = 2000,
                                },
                                new MainSettings
                                {
                                    DisplayName = "Пиздюки",
                                    Description = "Убейте npc",
                                    ShortName = "scientistnpc_junkpile_pistol",
                                    Amount = 2,
                                },
                                new MainSettings
                                {
                                    DisplayName = "Открыватель",
                                    Description = "Открыть двери синей картой",
                                    ShortName = "card2",
                                    Amount = 1,
                                }
                            },
                            items = new List<ItemsList>
                            {
                                new ItemsList
                                {
                                    DisplayName = "Дерево",
                                    ShortName = "wood",
                                    Command = null,
                                    Url = null,
                                    AmountMin = 1,
                                    AmountMax = 1000,
                                    DropChance = 70
                                },
                                new ItemsList
                                {
                                    DisplayName = "Камень",
                                    ShortName = "stones",
                                    Command = null,
                                    Url = null,
                                    AmountMin = 1000,
                                    AmountMax = 5000,
                                    DropChance = 100
                                },
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
                if (config?.passSettings == null) LoadDefaultConfig();
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
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/PlayerList"))
                DB = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, DataBase>>($"{Name}/PlayerList");
            else
                DB = new Dictionary<ulong, DataBase>();

            foreach (var check in config.passSettings)
            {
                foreach (var item in check.items)
                    ImageLibrary.Call("AddImage", item.Url, item.Url);
            }

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (!DB.ContainsKey(player.userID))
                DB.Add(player.userID, new DataBase());
        }

        PlayerProgress GetDataBase(ulong userID, string name)
        {
            if (!DB.ContainsKey(userID))
                DB[userID].Progress = new Dictionary<string, PlayerProgress>();

            if (!DB[userID].Progress.ContainsKey(name))
                DB[userID].Progress[name] = new PlayerProgress();

            return DB[userID].Progress[name];
        }

        void Progress(BasePlayer player, string ShortName, int Count)
        {
            foreach (var check in config.passSettings)
            {
                var name = check.mains.FirstOrDefault(x => x.ShortName == ShortName);
                if (name != null)
                {
                    var database = GetDataBase(player.userID, name.ShortName);
                    if (DB[player.userID].Level == check.Level)
                    {
                        if (!database.Enable)
                        {
                            database.Amount += Count;
                            if (database.Amount >= name.Amount)
                            {
                                player.SendConsoleCommand($"note.inv 915408809 +1 \"Задание выполнено\"");
                                database.Enable = true;
                                database.Amount = name.Amount;
                                return;
                            }
                            player.SendConsoleCommand($"note.inv 605467368 +{Count} \"Задание\"");
                        }
                    }
                }
            }
        }

        void OnDispenserBonus(ResourceDispenser dispenser, BaseEntity entity, Item item) => OnDispenserGather(dispenser, entity, item);
        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            BasePlayer player = entity.ToPlayer();
            Progress(player, item.info.shortname, item.amount);
        }

        void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            foreach (var item in collectible.itemList)
            {
                Progress(player, item.itemDef.shortname, (int)item.amount);
            }
        }

        void OnItemCraftFinished(ItemCraftTask task, Item item, ItemCrafter craft)
        {
            Progress(craft.owner, item.info.shortname, item.amount);
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

            if (info.InitiatorPlayer != null) player = info.InitiatorPlayer;
            else if (entity is BradleyAPC || entity is BaseHelicopter) player = BasePlayer.FindByID(lastDamageName);

            if (player == null) return;
            if (entity.ToPlayer() != null && entity as BasePlayer == player) return;
            Progress(player, entity?.ShortPrefabName, 1);
        }

        void OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer player)
        {
            if (player == null || card.accessLevel != cardReader.accessLevel) return;
            Puts($"card{card.accessLevel}");
            Progress(player, $"card{card.accessLevel}", 1);
        }

        void SaveDataBase() => Interface.Oxide.DataFileSystem.WriteObject($"{Name}/PlayerList", DB);

        void OnPlayerDisconnected(BasePlayer player, string reason) => SaveDataBase();

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, Layer);

            SaveDataBase();
        }
        #endregion

        #region Команды
        [ConsoleCommand("pass")]
        private void CmdCase(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player != null && args.HasArgs(1))
            {
                if (args.Args[0] == "take")
                {
                    bool enable = true;
                    foreach (var check in config.passSettings)
                    {
                        foreach (var pass in check.mains)
                        {
                            if (DB[player.userID].Level == check.Level)
                            {
                                var data = GetDataBase(player.userID, pass.ShortName);
                                if (data.Amount >= pass.Amount) continue;
                                enable = false;
                            }
                        }
                    }
                    if (!enable)
                    {
                        SendReply(player, "Вы еще не выполнили все задания");
                        return;
                    }
                    foreach (var check in config.passSettings)
                    {
                        int count = 0;
                        foreach (var item in check.items)
                        {
                            if (UnityEngine.Random.Range(0, 100) > item.DropChance) continue;
                            int Amount = Core.Random.Range(item.AmountMin, item.AmountMax);
                            if (DB[player.userID].Level == check.Level)
                            {
                                if (count >= check.Count) break;
                                if (!string.IsNullOrEmpty(item.Command))
                                {
                                    Server.Command(item.Command.Replace("%STEAMID%", player.UserIDString));
                                    SendReply(player, $"Вы получили услугу: <color=#ee3e61>{item.DisplayName}</color>");
                                }
                                if (!string.IsNullOrEmpty(item.ShortName))
                                {
                                    player.inventory.GiveItem(ItemManager.CreateByName(item.ShortName, Amount));
                                    SendReply(player, $"Вы получили: <color=#ee3e61>{item.DisplayName}</color>\nВ размере: <color=#ee3e61>{Amount}</color>");
                                }
                                count++;
                            }
                            DB[player.userID].Progress.Clear();

                        }
                        DB[player.userID].Level += 1;
                        CuiHelper.DestroyUi(player, "Menu_UI");
                    }
                }
            }
        }
        #endregion

        #region Интерфейс
        void PassUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" },
            }, "Menu_Block", Layer);

            foreach (var check in config.passSettings)
            {
                if (DB[player.userID].Level == check.Level)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 0.935", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Image = { Color = HexToCuiColor("#d1b283", 10), Material = "assets/content/ui/uibackgroundblur.mat" }
                    }, Layer, ".Level");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.015 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Text = { Text = $"Ваш уровень батлпасса: <b>{DB[player.userID].Level}</b> lvl", Color = HexToCuiColor("#d1b283", 100), Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                    }, ".Level");

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 0.515", AnchorMax = "1 0.581", OffsetMax = "0 0" },
                        Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
                    }, Layer, ".Priz");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.015 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Text = { Text = $"Список возможных наград, за выполнение всех заданий!", Color = "1 1 1 0.2", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                    }, ".Priz");

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "0.639 0.1", OffsetMax = "0 0" },
                        Image = { Color = HexToCuiColor("#d1b283", 10), Material = "assets/content/ui/uibackgroundblur.mat" }
                    }, Layer, ".Info");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.015 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Text = { Text = $"<b><size=14>ИНФОРМАЦИЯ</size></b>\nВыполняй все задания и получай награду!", Color = HexToCuiColor("#d1b283", 100), Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                    }, ".Info");

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.501", OffsetMax = "0 0" },
                        Image = { Color = "0 0 0 0" }
                    }, Layer, "ItemList");

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.65 0", AnchorMax = "1 0.1", OffsetMax = "0 0" },
                        Button = { Color = HexToCuiColor("#84b4dd", 10), Material = "assets/content/ui/uibackgroundblur.mat", Command = $"pass take" },
                        Text = { Text = $"получить\n<b><size=14>НАГРАДУ</size></b>", Color = HexToCuiColor("#84b4dd", 100), Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "robotocondensed-regular.ttf", FadeIn = 0.5f }
                    }, Layer);

                    float width1 = 0.1585f, height1 = 0.375f, startxBox1 = 0f, startyBox1 = 1f - height1, xmin1 = startxBox1, ymin1 = startyBox1;
                    foreach (var i in Enumerable.Range(0, 12))
                    {
                        var item = check.items.ElementAtOrDefault(i);
                        if (item != null)
                        {
                            container.Add(new CuiPanel
                            {
                                RectTransform = { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + height1 * 1}", OffsetMax = "0 0" },
                                Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
                            }, "ItemList", "Set");

                            if (item.Url != null)
                            {
                                container.Add(new CuiElement
                                {
                                    Parent = "Set",
                                    Components =
                                        {
                                            new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", item.Url) },
                                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "10 10", OffsetMax = "-10 -10" }
                                        }
                                });
                            }
                            else
                            {
                                container.Add(new CuiElement
                                {
                                    Parent = "Set",
                                    Components =
                                        {
                                            new CuiImageComponent { ItemId = ItemManager.FindItemDefinition(item.ShortName).itemid, SkinId = 0 },
                                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "10 10", OffsetMax = "-10 -10" }
                                        }
                                });
                            }

                            var textAmount = item.AmountMin != item.AmountMax ? $"~{item.AmountMax / 2}" : $"{item.AmountMax}";
                            container.Add(new CuiLabel
                            {
                                RectTransform = { AnchorMin = "0 0.01", AnchorMax = "0.96 1", OffsetMax = "0 0" },
                                Text = { Text = $"{textAmount}x", Color = "1 1 1 0.2", Align = TextAnchor.LowerRight, FontSize = 12, Font = "robotocondensed-regular.ttf", FadeIn = 0.5f }
                            }, "Set");
                        }
                        else
                        {
                            container.Add(new CuiPanel
                            {
                                RectTransform = { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + height1 * 1}", OffsetMax = "0 0" },
                                Image = { Color = "1 1 1 0.015", Material = "assets/content/ui/uibackgroundblur.mat" }
                            }, "ItemList", "Set");
                        }
                        xmin1 += width1 + 0.0095f;
                        if (xmin1 + width1 >= 1)
                        {
                            xmin1 = startxBox1;
                            ymin1 -= height1 + 0.023f;
                        }
                    }

                    float width = 0.4945f, height = 0.1015f, startxBox = 0f, startyBox = 0.923f - height, xmin = startxBox, ymin = startyBox;
                    foreach (var i in Enumerable.Range(0, 6))
                    {
                        var item = check.mains.ElementAtOrDefault(i);
                        if (item != null)
                        {
                            var database = GetDataBase(player.userID, item.ShortName);
                            container.Add(new CuiPanel
                            {
                                RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMax = "0 0" },
                                Image = { Color = "1 1 1 0.02", Material = "assets/content/ui/uibackgroundblur.mat" }
                            }, Layer, "Task");

                            container.Add(new CuiLabel
                            {
                                RectTransform = { AnchorMin = "0.03 0", AnchorMax = "0.7 1", OffsetMax = "0 0" },
                                Text = { Text = $"<color=#cec5bb><b>{item.DisplayName}</b></color>\n<size=10>{item.Description}</size>", Color = "1 1 1 0.2", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf", FadeIn = 0.5f }
                            }, "Task");

                            var text = database.Amount >= item.Amount ? "выполнено" : $"{database.Amount}/{item.Amount}";
                            container.Add(new CuiLabel
                            {
                                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.96 1", OffsetMax = "0 0" },
                                Text = { Text = $"{text} ", Color = "1 1 1 0.2", Align = TextAnchor.MiddleRight, FontSize = 12, Font = "robotocondensed-regular.ttf", FadeIn = 0.5f }
                            }, "Task");

                            container.Add(new CuiPanel
                            {
                                RectTransform = { AnchorMin = "0.975 0.08", AnchorMax = "0.99 0.92", OffsetMax = "0 0" },
                                Image = { Color = "1 1 1 0.12" }
                            }, "Task", "Progress");

                            container.Add(new CuiPanel
                            {
                                RectTransform = { AnchorMin = "0 0", AnchorMax = $"1 {(float)database.Amount / item.Amount}", OffsetMax = "0 0" },
                                Image = { Color = HexToCuiColor("#bbc47e", 40), Material = "assets/content/ui/uibackgroundblur.mat" }
                            }, "Progress");
                        }
                        else
                        {
                            container.Add(new CuiPanel
                            {
                                RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMax = "0 0" },
                                Image = { Color = "1 1 1 0.015", Material = "assets/content/ui/uibackgroundblur.mat" }
                            }, Layer);
                        }
                        xmin += width + 0.01f;
                        if (xmin + width >= 1)
                        {
                            xmin = startxBox;
                            ymin -= height + 0.012f;
                        }
                    }
                }
            }
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
        #endregion
    }
}