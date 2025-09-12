using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("caseSystem", "Chibubrik", "1.2.0")]
    class CardsSystem : RustPlugin
    {
        #region Вар
        private string Layer = "case_UI";
        private string LayerInv = "Inventory_UI";
        [PluginReference] Plugin ImageLibrary;
        private Dictionary<ulong, CardsData> dataSettings = new Dictionary<ulong, CardsData>();
        private static CardsSystem inst;
        #endregion

        #region Класс
        public class Settings
        {
            [JsonProperty("Максималное количество накапливаемых карт")] public int Count;
            [JsonProperty("Откат карт (в секундах)")] public int Time;
            [JsonProperty("Изображение карточки")] public string caseImage;
        }

        public class ItemSettings
        {
            [JsonProperty("Номер карточки")] public int ID;
            [JsonProperty("Название карточки")] public string DisplayName;
            [JsonProperty("Сколько нужно одинаковых карточек, чтобы забрать из инвентаря?")] public int Count;
            [JsonProperty("Короткое название предмета")] public string ShortName;
            [JsonProperty("Количество предмета")] public int Amount;
            [JsonProperty("Дополнительная команда")] public string Command;
            [JsonProperty("Изображение карточки")] public string Url;
        }

        public class CardsData
        {
            [JsonProperty("Доступно карт для открытия")] public int Amount;
            [JsonProperty("Откат")] public double Time;
            [JsonProperty("Список карточек игрока")] public Dictionary<string, DataSettings> Inventory = new Dictionary<string, DataSettings>();
        }

        public class DataSettings
        {
            [JsonProperty("Номер карточки")] public int ID;
            [JsonProperty("Название карточки")] public string DisplayName;
            [JsonProperty("Собранно одинаковых карточек")] public int Count;
            [JsonProperty("Короткое название предмета")] public string ShortName;
            [JsonProperty("Количество предмета")] public int Amount;
            [JsonProperty("Дополнительная команда")] public string Command;
            [JsonProperty("Изображение карточки")] public string Url;

            public Item GiveItem(BasePlayer player)
            {
                if (!string.IsNullOrEmpty(Command)) inst.Server.Command(Command.Replace("%STEAMID%", player.UserIDString));
                if (!string.IsNullOrEmpty(ShortName))
                {
                    Item item = ItemManager.CreateByPartialName(ShortName, Amount);

                    return item;
                }
                return null;
            }
        }
        #endregion

        #region Конфиг
        public Configuration config;
        public class Configuration
        {
            [JsonProperty("Основные настройки")] public Settings settings = new Settings();
            [JsonProperty("Список карточек")] public List<ItemSettings> itemSettings = new List<ItemSettings>();
            public static Configuration GetNewConfig()
            {
                return new Configuration
                {
                    settings = new Settings
                    {
                        Count = 3,
                        Time = 3600,
                        caseImage = "https://imgur.com/02foDYp.png"
                    },
                    itemSettings = new List<ItemSettings>
                    {
                        new ItemSettings
                        {
                            ID = 1,
                            DisplayName = "Куски металолома",
                            Count = 5,
                            ShortName = "scrap",
                            Amount = 100,
                            Command = null,
                            Url = "https://imgur.com/lJzMaDv.png"
                        },
                        new ItemSettings
                        {
                            ID = 2,
                            DisplayName = "Сера",
                            Count = 1,
                            ShortName = "sulfur",
                            Amount = 1000,
                            Command = null,
                            Url = "https://imgur.com/L8MfTbi.png"
                        },
                        new ItemSettings
                        {
                            ID = 3,
                            DisplayName = "Мвк",
                            Count = 1,
                            ShortName = "metal.refined",
                            Amount = 50,
                            Command = null,
                            Url = "https://imgur.com/a8UtwC8.png"
                        },
                        new ItemSettings
                        {
                            ID = 4,
                            DisplayName = "Штурмовая винтовка",
                            Count = 1,
                            ShortName = "rifle.ak",
                            Amount = 1,
                            Command = null,
                            Url = "https://imgur.com/FxrmLG2.png"
                        },
                        new ItemSettings
                        {
                            ID = 5,
                            DisplayName = "Штурмовая болтовка",
                            Count = 1,
                            ShortName = "rifle.bolt",
                            Amount = 1,
                            Command = null,
                            Url = "https://imgur.com/kO10Txh.png"
                        },
                        new ItemSettings
                        {
                            ID = 6,
                            DisplayName = "Пистолет",
                            Count = 1,
                            ShortName = "pistol.semiauto",
                            Amount = 1,
                            Command = null,
                            Url = "https://imgur.com/Tl19kLu.png"
                        },
                        new ItemSettings
                        {
                            ID = 7,
                            DisplayName = "Сигнальная граната",
                            Count = 1,
                            ShortName = "supply.signal",
                            Amount = 1,
                            Command = null,
                            Url = "https://imgur.com/zX0tvLU.png"
                        },
                        new ItemSettings
                        {
                            ID = 8,
                            DisplayName = "Взрывчатка c4",
                            Count = 1,
                            ShortName = "explosive.timed",
                            Amount = 1,
                            Command = null,
                            Url = "https://imgur.com/UeaBtiO.png"
                        },
                        new ItemSettings
                        {
                            ID = 9,
                            DisplayName = "Медведик",
                            Count = 1,
                            ShortName = "pookie.bear",
                            Amount = 1,
                            Command = null,
                            Url = "https://imgur.com/4ZSLXBu.png"
                        },
                        new ItemSettings
                        {
                            ID = 10,
                            DisplayName = "Шприц",
                            Count = 1,
                            ShortName = "syringe.medical",
                            Amount = 1,
                            Command = null,
                            Url = "https://imgur.com/F39jdLv.png"
                        },
                        new ItemSettings
                        {
                            ID = 11,
                            DisplayName = "Большая аптечка",
                            Count = 1,
                            ShortName = "largemedkit",
                            Amount = 1,
                            Command = null,
                            Url = "https://imgur.com/IY88W81.png"
                        },
                        new ItemSettings
                        {
                            ID = 12,
                            DisplayName = "Неломаемость",
                            Count = 5,
                            ShortName = null,
                            Amount = 1,
                            Command = "o.grant user %STEAMID% never",
                            Url = "https://imgur.com/tWitDZz.png"
                        },
                        new ItemSettings
                        {
                            ID = 13,
                            DisplayName = "Переработчик",
                            Count = 2,
                            ShortName = null,
                            Amount = 1,
                            Command = "recycler give %STEAMID%",
                            Url = "https://imgur.com/Ago1lps.png"
                        },
                        new ItemSettings
                        {
                            ID = 14,
                            DisplayName = "Метаболизм",
                            Count = 6,
                            ShortName = null,
                            Amount = 1,
                            Command = "o.grant user %STEAMID% met",
                            Url = "https://imgur.com/6JqzXoT.png"
                        },
                        new ItemSettings
                        {
                            ID = 15,
                            DisplayName = "Скины",
                            Count = 3,
                            ShortName = null,
                            Amount = 1,
                            Command = "o.grant user %STEAMID% skin.use",
                            Url = "https://imgur.com/ykqi6ny.png"
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
                if (config?.itemSettings == null) LoadDefaultConfig();
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
        private void OnServerInitialized()
        {
            inst = this;
            ImageLibrary.Call("AddImage", config.settings.caseImage, "Images");

            foreach (var check in config.itemSettings)
                ImageLibrary.Call("AddImage", check.Url, check.Url);
            
            foreach (var player in BasePlayer.activePlayerList.ToList())
                OnPlayerConnected(player);
        }

        private void OnPlayerConnected(BasePlayer player) => CreateDataBase(player);

        private void Unload()
        {
            foreach (var check in dataSettings)
                SaveDataBase(check.Key);
            foreach (var player in BasePlayer.activePlayerList.ToList())
            {
                CuiHelper.DestroyUi(player, Layer);
                CuiHelper.DestroyUi(player, LayerInv);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason) => SaveDataBase(player.userID);

        private DataSettings GetItem(ulong userID, string name)
        {
            if (!dataSettings.ContainsKey(userID))
                dataSettings[userID].Inventory = new Dictionary<string, DataSettings>();

            if (!dataSettings[userID].Inventory.ContainsKey(name))
                dataSettings[userID].Inventory[name] = new DataSettings();

            return dataSettings[userID].Inventory[name];
        }

        private void AddItem(BasePlayer player, ItemSettings itemSettings)
        {
            var data = GetItem(player.userID, itemSettings.DisplayName);
            data.ID = itemSettings.ID;
            data.DisplayName = itemSettings.DisplayName;
            data.Count += 1;
            data.ShortName = itemSettings.ShortName;
            data.Amount = itemSettings.Amount;
            data.Command = itemSettings.Command;
            data.Url = itemSettings.Url;
        }
        #endregion

        #region Дата
        void CreateDataBase(BasePlayer player)
        {
            var DataBase = Interface.Oxide.DataFileSystem.ReadObject<CardsData>($"CardSystem/{player.userID}");
            
            if (!dataSettings.ContainsKey(player.userID))
                dataSettings.Add(player.userID, new CardsData { Amount = config.settings.Count, Time = 0 });
             
            dataSettings[player.userID] = DataBase ?? new CardsData();
        }

        void SaveDataBase(ulong userId) => Interface.Oxide.DataFileSystem.WriteObject($"CardSystem/{userId}", dataSettings[userId]);
        #endregion

        #region Команды
        [ChatCommand("case")]
        private void Commandcase(BasePlayer player)
        {
            caseUI(player);
        }

        [ConsoleCommand("case")]
        private void Consolecase(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player != null && args.HasArgs(1))
            {
                if (args.Args[0] == "random")
                {
                    var currentTime = GetCurrentTime();
                    if (dataSettings[player.userID].Time < currentTime)
                    {
                        if (dataSettings[player.userID].Amount > 0)
                        {
                            var item = config.itemSettings.ToList().GetRandom();
                            AddItem(player, item);
                            PrizUI(player, args.Args[1], item);
                            dataSettings[player.userID].Amount -= 1;
                        }
                        if (dataSettings[player.userID].Amount == 0)
                        {
                            dataSettings[player.userID].Time = GetCurrentTime() + config.settings.Time;
                            dataSettings[player.userID].Amount = config.settings.Count;
                        }
                        Update(player);
                    }
                }
                if (args.Args[0] == "take")
                {
                    var data = GetItem(player.userID, dataSettings[player.userID].Inventory.ElementAt(int.Parse(args.Args[1])).Key);
                    var check = config.itemSettings.FirstOrDefault(p => p.ID == data.ID);
                    if (data.Count >= check.Count)
                    {
                        data.GiveItem(player)?.MoveToContainer(player.inventory.containerMain);
                        data.Count -= check.Count;
                        InventoryUI(player);
                    }
                }
                if (args.Args[0] == "inv")
                {
                    InventoryUI(player);
                }
                if (args.Args[0] == "skip")
                {
                    InventoryUI(player, int.Parse(args.Args[1]));
                }
            }
        }
        #endregion

        #region Интерфейс

        #region Основной интерфейс
        private void caseUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" },
            }, "Overlay", Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.2 0.13", AnchorMax = "0.8 0.87", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0.9" },
                Text = { Text = "" }
            }, Layer, "Layer");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0.875", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0" },
                Text = { Text = "<b><size=45>СИСТЕМА КЕЙСОВ</size></b>\nИспытай свою удачу! Открывай кейсы каждый день и получай призы!", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Layer");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.232 0.01", AnchorMax = "0.404 0.06", OffsetMax = "0 0" },
                Button = { Color = "0.71 0.88 0.36 0.6", Command = $"case inv", Close = Layer },
                Text = { Text = "ВАШ ЛУТ", Color = HexToUiColor("#FFFFFF7A"), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-bold.ttf" }
            }, "Layer");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.413 0.01", AnchorMax = "0.586 0.06", OffsetMax = "0 0" },
                Button = { Color = "0.40 0.36 0.88 0.6", Command = "chat.say /case" },
                Text = { Text = "ПЕРЕРАЗДАТЬ", Color = HexToUiColor("#FFFFFF7A"), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-bold.ttf" }
            }, "Layer");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.596 0.01", AnchorMax = "0.768 0.06", OffsetMax = "0 0" },
                Button = { Color = "0.88 0.36 0.36 0.6", Close = Layer },
                Text = { Text = "ЗАКРЫТЬ", Color = HexToUiColor("#FFFFFF7A"), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-bold.ttf" }
            }, "Layer");

            var currentTime = GetCurrentTime();
            var text = dataSettings[player.userID].Time > currentTime ? $"Подождите: {TimeExtensions.FormatTime(TimeSpan.FromSeconds(dataSettings[player.userID].Time - currentTime))}" : $"ДОСТУПНО КАРТ ДЛЯ ОТКРЫТИЯ - {dataSettings[player.userID].Amount}";
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.232 0.07", AnchorMax = "0.768 0.12", OffsetMax = "0 0" },
                Button = { Color = "1 1 1 0", Close = Layer },
                Text = { Text = text, Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-bold.ttf" }
            }, "Layer", "Amount");

            float gap = 0.01f, width = 0.172f, height = 0.365f, startxBox = 0.05f, startyBox = 0.86f - height, xmin = startxBox, ymin = startyBox;
            for (int i = 0; i < 10; i++)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = xmin + " " + ymin, AnchorMax = (xmin + width) + " " + (ymin + height * 1), OffsetMax = "0 0", OffsetMin = "0 0" },
                    Button = { Color = "1 1 1 0", Command = $"case random {i}" },
                    Text = { Text = $"" }
                }, "Layer", $"Button.{i}");

                container.Add(new CuiElement
                {
                    Name = $"Imagess.{i}",
                    Parent = $"Button.{i}",
                    FadeOut = 1.5f,
                    Components = {
                         new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "Images"), FadeIn = 1f},
                         new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                    }
                });

                xmin += width + gap;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height + gap;
                }
            }

            CuiHelper.AddUi(player, container);
        }

        #region UpdateAmount
        private void Update(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Amount");
            CuiElementContainer container = new CuiElementContainer();

            var currentTime = GetCurrentTime();
            var text = dataSettings[player.userID].Time > currentTime ? $"Подождите: {TimeExtensions.FormatTime(TimeSpan.FromSeconds(dataSettings[player.userID].Time - currentTime))}" : $"ДОСТУПНО КАРТ ДЛЯ ОТКРЫТИЯ - {dataSettings[player.userID].Amount}";
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.232 0.07", AnchorMax = "0.768 0.12", OffsetMax = "0 0" },
                Button = { Color = "1 1 1 0", Close = Layer },
                Text = { Text = text, Color = HexToUiColor("#FFFFFF5A"), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-bold.ttf" }
            }, "Layer", "Amount");
            CuiHelper.AddUi(player, container);
        }
        #endregion
        #endregion

        #region Приз
        private void PrizUI(BasePlayer player, string z, ItemSettings itemSettings)
        {
            CuiHelper.DestroyUi(player, $"Imagess.{z}");
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "1 1 1 0" },
                Text = { Text = "" }
            }, $"Button.{z}", "Layers");

            container.Add(new CuiElement
            {
                Parent = "Layers",
                Components =
                {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", itemSettings.Url), FadeIn = 2f},
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                }
            });

            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Инфентарь
        private void InventoryUI(BasePlayer player, int page = 0)
        {
            CuiHelper.DestroyUi(player, LayerInv);
            CuiElementContainer container = new CuiElementContainer();
            float gap = 0.01f, width = 0.1095f, height = 0.26f, startxBox = 0.027f, startyBox = 0.87f - height, xmin = startxBox, ymin = startyBox;

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" },
            }, "Overlay", LayerInv);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "-2 -2", AnchorMax = "2 2", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Close = LayerInv },
                Text = { Text = "" }
            }, LayerInv);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.2 0.13", AnchorMax = "0.8 0.87", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0.9" },
                Text = { Text = "" }
            }, LayerInv, "Inventory");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0.875", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0" },
                Text = { Text = "ВАШИ СОБРАННЫЕ КЕЙСЫ", Align = TextAnchor.MiddleCenter, FontSize = 45, Font = "robotocondensed-bold.ttf" }
            }, "Inventory");

            if (page != 0)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.45 0.01", AnchorMax = "0.495 0.06", OffsetMax = "0 0" },
                    Button = { Color = "1 1 1 0.1", Command = $"case skip {page - 1}" },
                    Text = { Text = "<", Align = TextAnchor.MiddleCenter, FontSize = 23, Font = "robotocondensed-bold.ttf" }
                }, "Inventory");
            }
            if ((float)dataSettings[player.userID].Inventory.Count > (page + 1) * 24)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.505 0.01", AnchorMax = "0.55 0.06", OffsetMax = "0 0" },
                    Button = { Color = "1 1 1 0.1", Command = $"case skip {page + 1}" },
                    Text = { Text = ">", Align = TextAnchor.MiddleCenter, FontSize = 23, Font = "robotocondensed-bold.ttf" }
                }, "Inventory");
            }

            var list = dataSettings[player.userID].Inventory.Skip(page * 24).Take(24);
            for (int i = 0; i < list.Count(); i++)
            {
                var data = GetItem(player.userID, list.ElementAt(i).Key);
                var check = config.itemSettings.FirstOrDefault(p => p.ID == data.ID);

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = xmin + " " + ymin, AnchorMax = (xmin + width) + " " + (ymin + height * 1), OffsetMax = "0 0", OffsetMin = "0 0" },
                    Button = { Color = "1 1 1 0" },
                    Text = { Text = "" }
                }, "Inventory", $"Inv.{i}");

                var color = data.Count >= check.Count ? "1 1 1 1" : "1 1 1 0.5";
                container.Add(new CuiElement
                {
                    Name = $"ImageItem.{i}",
                    Parent = $"Inv.{i}",
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", data.Url), Color = color},
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.04 0", AnchorMax = "0.96 1", OffsetMax = "0 0" },
                    Button = { Color = "1 1 1 0", Command = $"case take {i}" },
                    Text = { Text = $"\nСобрано: {data.Count} из {check.Count}", Align = TextAnchor.UpperCenter, FontSize = 10, Font = "robotocondensed-regular.ttf" }
                }, $"Inv.{i}");

                xmin += width + gap;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height + gap;
                }
            }

            CuiHelper.AddUi(player, container);
        }
        #endregion

        #endregion

        #region Хелпер
        private static string HexToUiColor(string hex)
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

            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }

        private double GetCurrentTime() => new TimeSpan(DateTime.UtcNow.Ticks).TotalSeconds;

        private static class TimeExtensions
        {
            public static string FormatTime(TimeSpan time)
            {
                string result = string.Empty;
                if (time.Days != 0)
                    result += $"{Format(time.Days, "дней", "дня", "день")} ";

                if (time.Hours != 0)
                    result += $"{Format(time.Hours, "часов", "часа", "час")} ";

                if (time.Minutes != 0)
                    result += $"{Format(time.Minutes, "минут", "минуты", "минута")} ";

                if (time.Seconds != 0)
                    result += $"{Format(time.Seconds, "секунд", "секунды", "секунда")} ";

                return result;
            }

            private static string Format(int units, string form1, string form2, string form3)
            {
                var tmp = units % 10;

                if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9)
                    return $"{units} {form1}";

                if (tmp >= 2 && tmp <= 4)
                    return $"{units} {form2}";

                return $"{units} {form3}";
            }
        }
        #endregion
    }
}