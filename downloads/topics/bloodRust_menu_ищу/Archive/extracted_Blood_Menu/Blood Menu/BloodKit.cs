using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using UnityEngine;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("BloodKit", "[LimePlugin] Chibubrik", "1.0.0")]
    class BloodKit : RustPlugin
    {
        #region Вар
        private string Layer = "Kit_UI";

        [PluginReference] Plugin ImageLibrary;

        Dictionary<ulong, Data> Settings = new Dictionary<ulong, Data>();

        List<BasePlayer> KitOpen = new List<BasePlayer>();
        #endregion

        #region Класс
        public class KitSet
        {
            [JsonProperty("Описание набора")] public string Description;
            [JsonProperty("Картинка набора (url)")] public string Url;
            [JsonProperty("Настройки наборов")] public List<KitSettings> settings;
        }

        public class KitSettings 
        {
            [JsonProperty("Название набора")] public string Name;
            [JsonProperty("Формат названия набора")] public string DisplayName;
            [JsonProperty("Название кулдаун набора")] public double Cooldown;
            [JsonProperty("Предметы набора")] public List<ItemSettings> Items;
        }

        public class ItemSettings
        {
            [JsonProperty("Название предмета")] public string ShortName;
            [JsonProperty("Количество предмета")] public int Amount;
            [JsonProperty("Скин предмета")] public ulong SkinID;
            [JsonProperty("Место в инвентаре")] public string Container;
            [JsonProperty("Шанс выпадения")] public double Chance;
            [JsonProperty("Чертеж?")] public int Blueprint;
            [JsonProperty("Прочность")] public float Condition;
            [JsonProperty("Патроны")] public Weapon Weapon;
            [JsonProperty("Содержимое предмета")] public List<ItemContent>  Content;
        }

        public class Weapon
        {
            [JsonProperty("Тип патрона")] public string AmmoType;
            [JsonProperty("Количество патронов")] public int AmmoAmount;
        }

        public class ItemContent
        {
            [JsonProperty("Название предмета")] public string ShortName;
            [JsonProperty("Прочность")] public float Condition;
            [JsonProperty("Количество предмета")] public int Amount;
        }

        public class Data
        {
            [JsonProperty("Список наборов и их кулдаун")] public Dictionary<string, KitData> SettingsData = new Dictionary<string, KitData>();
        }

        public class KitData
        {
            [JsonProperty("Кулдаун набора")] public double Cooldown;
        }
        #endregion

        #region Конфиг
        Configuration config;
        class Configuration 
        {
            [JsonProperty("Список категорий и их наборы")] public Dictionary<string, KitSet> set = new Dictionary<string, KitSet>();
            public static Configuration GetNewConfig() 
            {
                return new Configuration();
            }
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.set == null) LoadDefaultConfig();
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
            foreach (var check in config.set) 
                ImageLibrary.Call("AddImage", check.Value.Url, check.Value.Url);
            foreach (var check in config.set) 
                permission.RegisterPermission(check.Key, this);


            foreach(BasePlayer check in BasePlayer.activePlayerList)
                OnPlayerConnected(check);
        }

        void OnPlayerConnected(BasePlayer player) => CreateDataBase(player); 

        void OnPlayerDisconnected(BasePlayer player, string reason) => SaveDataBase(player.userID);

        void Unload() 
        {
            foreach(var check in Settings)
                SaveDataBase(check.Key);

            foreach(BasePlayer check in BasePlayer.activePlayerList)
                DestroyUi(check);
        }
        #endregion

        #region Методы
        KitData GetDataBase(ulong userID, string name)
        {
            if (!Settings.ContainsKey(userID))
                Settings[userID].SettingsData = new Dictionary<string, KitData>();

            if (!Settings[userID].SettingsData.ContainsKey(name))
                Settings[userID].SettingsData[name] = new KitData();

            return Settings[userID].SettingsData[name];
        }

        public static string FormatShortTime(TimeSpan time)
        {
            string result = string.Empty;
            if (time.Days != 0)
                result = $"{time.Days.ToString("0")}д ";
            if (time.Hours != 0)
                result += $"{time.Hours.ToString("0")}ч ";
            if (time.Minutes != 0)
                result += $"{time.Minutes.ToString("0")}м ";
            if (time.Seconds != 0)
                result += $"{time.Seconds.ToString("0")}с";
            return result;
        }

        void DestroyUi(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            KitOpen.Remove(player);
        }
        
        double CurTime() => new TimeSpan(DateTime.UtcNow.Ticks).TotalSeconds;
        #endregion

        #region Дата
        void CreateDataBase(BasePlayer player)
        {
            var DataBase = Interface.Oxide.DataFileSystem.ReadObject<Data>($"BloodKit/{player.userID}");
            
            if (!Settings.ContainsKey(player.userID))
                Settings.Add(player.userID, new Data());
             
            Settings[player.userID] = DataBase ?? new Data();
        }

        void SaveDataBase(ulong userId) => Interface.Oxide.DataFileSystem.WriteObject($"BloodKit/{userId}", Settings[userId]);
        #endregion

        #region Создание наборов
        void CreateKit(BasePlayer player, string perm, string name)
        {
            var check = $"bloodkit.{perm}";
            if (!config.set.ContainsKey(check)) {
                config.set.Add(check, new KitSet() {
                    Description = "Категория",
                    Url = "https://i.imgur.com/SSNjAsF.png",
                    settings = new List<KitSettings>()
                });
            }
            if (config.set.Values.Any(p => p.settings.Exists(z => z.Name == name)))
            {
                player.ConsoleMessage($"Набор {name} уже существует!");
                return;
            }
            if (config.set[check].settings.Count >= 3) {
                player.ConsoleMessage($"Вы уже добавили в категорию {check} максимальное количество наборов!");
                return;
            }
            config.set[check].settings.Add(new KitSettings() {
                Name = name,
                DisplayName = name,
                Cooldown = 600,
                Items = GetItems(player)
            });
            permission.RegisterPermission(check, this);
            SaveConfig();
            player.ConsoleMessage($"Вы успешно создали набор {name}");
        }

        List<ItemSettings> GetItems(BasePlayer player)
        {
            List<ItemSettings> kititems = new List<ItemSettings>();
            foreach (Item item in player.inventory.containerWear.itemList)
            {
                if (item != null)
                {
                    var iteminfo = CreateItem(item, "Одежда");
                    kititems.Add(iteminfo);
                }
            }
            foreach (Item item in player.inventory.containerMain.itemList)
            {
                if (item != null)
                {
                    var iteminfo = CreateItem(item, "Рюкзак");
                    kititems.Add(iteminfo);
                }
            }
            foreach (Item item in player.inventory.containerBelt.itemList)
            {
                if (item != null)
                {
                    var iteminfo = CreateItem(item, "Пояс");
                    kititems.Add(iteminfo);
                }
            }
            return kititems;
        }

        ItemSettings CreateItem(Item item, string container)
        {
            ItemSettings items = new ItemSettings();
            items.ShortName = item.info.shortname;
            items.Amount = item.amount;
            items.SkinID = item.skin;
            items.Container = container;
            items.Chance = 100;
            items.Blueprint = item.blueprintTarget;
            items.Condition = item.condition;
            items.Weapon = null;
            items.Content = null;
            if (item.info.category == ItemCategory.Weapon)
            {
                BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon != null)
                {
                    items.Weapon = new Weapon();
                    items.Weapon.AmmoType = weapon.primaryMagazine.ammoType.shortname;
                    items.Weapon.AmmoAmount = weapon.primaryMagazine.contents;
                }
            }
            if (item.contents != null)
            {
                items.Content = new List<ItemContent>();
                foreach (var cont in item.contents.itemList)
                {
                    items.Content.Add(new ItemContent()
                    {
                        Amount = cont.amount,
                        Condition = cont.condition,
                        ShortName = cont.info.shortname
                    }
                    );
                }
            }
            return items;
        }
        #endregion

        #region Выдача наборов
        Item GetItem(string ShortName, int Amount, ulong SkinID, int Blueprint, float Condition, Weapon weapon, List<ItemContent> Content)
        {
            Item item = ItemManager.CreateByName(ShortName, Amount, SkinID);
            if (Blueprint != 0)
                item.blueprintTarget = Blueprint;
            item.condition = Condition;
            if (weapon != null)
            {
                (item.GetHeldEntity() as BaseProjectile).primaryMagazine.contents = weapon.AmmoAmount;
                (item.GetHeldEntity() as BaseProjectile).primaryMagazine.ammoType = ItemManager.FindItemDefinition(weapon.AmmoType);
            }
            if (Content != null)
            {
                foreach (var cont in Content)
                {
                    Item new_cont = ItemManager.CreateByName(cont.ShortName, cont.Amount);
                    new_cont.condition = cont.Condition;
                    new_cont.MoveToContainer(item.contents);
                }
            }

            return item;
        }

        void SetItem(BasePlayer player, Item item, string container)
        {
            if (item == null) return;
            var cont = container == "Пояс" ? player.inventory.containerBelt : container == "Одежда" ? player.inventory.containerWear : player.inventory.containerMain;
           
            var moved = item.MoveToContainer(cont) || item.MoveToContainer(player.inventory.containerMain);
            if (!moved) {
                if (player.inventory.containerBelt == cont) 
                    moved = item.MoveToContainer(player.inventory.containerWear);
                if (player.inventory.containerWear == cont) 
                    moved = item.MoveToContainer(player.inventory.containerBelt);
            }

            if (!moved) 
                item.Drop(player.GetCenter(), player.GetDropVelocity());
        }
        #endregion

        #region Команды
        [ChatCommand("kit")]
        void ChatKit(BasePlayer player, string command, string[] args)
        {
            if (KitOpen.Contains(player))
            {
                DestroyUi(player);
                return;
            }
            else
            {
                KitUI(player);
                return;
            }
        }

        [ConsoleCommand("kit")]
        void ConsoleKit(ConsoleSystem.Arg args)
        {
            var Time = CurTime();
            var player = args.Player();
            if (player != null && args.HasArgs(1))
            {
                if (args.Args[0] == "new")
                {
                    if (!player.IsAdmin) return;
                    if (args.Args.Length < 3)
                    {
                        player.ConsoleMessage("Используйте: kit new [название пермишена] [название набора]");
                        return;
                    }
                    CreateKit(player, args.Args[1], args.Args[2]);
                }
                if (args.Args[0] == "cremove")
                {
                    if (!player.IsAdmin) return;
                    if (args.Args.Length < 2)
                    {
                        player.ConsoleMessage("Используйте: kit cremove [название пермишена]");
                        return;
                    }
                    if (config.set.ContainsKey($"bloodkit.{args.Args[1]}")) {
                        config.set.Remove($"bloodkit.{args.Args[1]}");
                        player.ConsoleMessage($"Вы успешно удали всю категорию пермишена {args.Args[1]}!");
                        SaveConfig();
                    }
                } 
                if (args.Args[0] == "remove")
                {
                    if (!player.IsAdmin) return;
                    if (args.Args.Length < 2)
                    {
                        player.ConsoleMessage("Используйте: kit remove [название набора]");
                        return;
                    }
                    if (!config.set.Values.Any(z => z.settings.RemoveAll(z => z.Name == args.Args[1]) <= 0)) {
                        config.set.Remove($"bloodkit.{args.Args[1]}");
                        player.ConsoleMessage($"Набор {args.Args[1]} успешно удален!");
                        SaveConfig();
                    }
                    SaveConfig();
                }                
                if (args.Args[0] == "ui")
                {
                    DestroyUi(player);
                }
                if (args.Args[0] == "info")
                {
                    KitInfoUI(player, args.Args[1], args.Args[2]);
                }
                if (args.Args[0] == "take")
                {
                    var perm = config.set.FirstOrDefault(z => z.Key == args.Args[1]);
                    var check = perm.Value.settings.FirstOrDefault(z => z.Name == args.Args[2]);
                    if (!permission.UserHasPermission(player.UserIDString, perm.Key))
                    {
                        player.ConsoleMessage($"<size=12>Набор <color=#ee3e61>{check.DisplayName}</color> недоступен!</size>");
                        return;
                    }
                    if (player.inventory.containerMain.itemList.Count >= 24)
                    {
                        player.SendConsoleCommand($"note.inv 605467368 -1 \"Недостаточно места\"");
                        return;
                    }
                    var db = GetDataBase(player.userID, check.Name);
                    if (check.Cooldown > 0) 
                    {
                        if (db.Cooldown > Time)
                        {
                            player.SendConsoleCommand("<size=12>Ахахахахахах, а вот и хуй тебе\nЖди кулдаун сука!</size>");
                            return;
                        }
                        db.Cooldown = Time + check.Cooldown;
                    }
                    foreach (var item in check.Items) {
                        if (UnityEngine.Random.Range(0, 100) < item.Chance) {
                            SetItem(player, GetItem(item.ShortName, item.Amount, item.SkinID, item.Blueprint, item.Condition, item.Weapon, item.Content), item.Container);
                        }
                    }
                    DestroyUi(player);
                    player.SendConsoleCommand($"note.inv 605467368 1 \"НАБОР ПОЛУЧЕН\"");
                    Effect x = new Effect("assets/bundled/prefabs/fx/notice/stack.world.fx.prefab", player, 0, new Vector3(), new Vector3());
                    EffectNetwork.Send(x, player.Connection);
                }
            }
        }
        #endregion

        #region Интерфейс
        void KitUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            KitOpen.Add(player);
            var container = new CuiElementContainer();
            int ItemCount = config.set.Where(z => (string.IsNullOrEmpty(z.Key) || permission.UserHasPermission(player.UserIDString, z.Key))).ToList().Count(), CountItem = 0, Count = 3;
            float Position = 0.5f, Width = 0.176f, Height = 0.212f, Margin = 0f, MinHeight = ItemCount > 3 ? 0.5f : 0.4f;
            var set = config.set.Where(z => (string.IsNullOrEmpty(z.Key) || permission.UserHasPermission(player.UserIDString, z.Key))).ToList();

            if (ItemCount >= Count) Position = 0.5f - Count / 2f * Width - (Count - 1) / 2f * Margin;
            else Position = 0.502f - ItemCount / 2f * Width - (ItemCount - 1) / 2f * Margin;
            ItemCount -= Count;

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.9" },
            }, "Overlay", Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0.23", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, Layer);
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Sprite = "assets/content/ui/ui.background.transparent.radial.psd", Color = HexToCuiColor("#363636", 60) }
            }, Layer);

            if (config.set.Count() == 0) {
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = $"<b><size=60><color=#cec5bb>Наборы</color></size></b>\nНа данный момент, Вы не настроили ни одного набора.\nЧтобы закрыть меню наборов, щелкните по свободному месту!", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.2", FontSize = 14 }
                }, Layer);
            }

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = $"kit ui" },
                Text = { Text = "" }
            }, Layer);

            foreach (var check in set) {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{Position} {MinHeight}", AnchorMax = $"{Position + Width} {MinHeight + Height}", OffsetMin = "4 4", OffsetMax = "-4 -4" },
                    Image = { Color = "1 1 1 0", Material = "assets/content/ui/uibackgroundblur.mat" }
                }, Layer, Layer + ".Set");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0.63", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
                }, Layer + ".Set", "Name");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.06 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = $"<b><size=14><color=#cec5bb>Набор китов</color></size></b>\n", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.2", FontSize = 10 }
                }, "Name");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.06 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = $"\n{check.Value.Description.ToLower()}", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", Color = HexToCuiColor("#a2a3a5", 50), FontSize = 11 }
                }, "Name");

                    container.Add(new CuiElement
                    {
                        Parent = "Name",
                        Components = 
                        {
                            new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", check.Value.Url), Color = "1 1 1 0.6" },
                            new CuiRectTransformComponent { AnchorMin = "0.6 0", AnchorMax = $"0.9 1", OffsetMax = "0 0" },
                        }
                    });

                CountItem += 1;
                if (CountItem % Count == 0)
                {
                    if (ItemCount > Count)
                    {
                        Position = 0.5f - Count / 2f * Width - (Count - 1) / 2f * Margin;
                        ItemCount -= Count;
                    }
                    else
                    {
                        Position = 0.5f - ItemCount / 2f * Width - (ItemCount - 1) / 2f * Margin;
                    }
                    MinHeight -= ((Margin * 2) + Height + 0.005f);
                }
                else
                {
                    Position += (Width + Margin);
                }

                float width = 1f, height = 0.2f, startxBox = 0f, startyBox = 0.613f - height, xmin = startxBox, ymin = startyBox;
                foreach (var i in Enumerable.Range(0, 3)) {
                    var item = check.Value.settings.ElementAtOrDefault(i);
                    if (item != null) {
                        var db = GetDataBase(player.userID, item.Name);
                        var Time = CurTime();

                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMax = "0 0" },
                            Image = { Color = "0 0 0 0" }
                        }, Layer + ".Set", ".Kit");

                        if (db.Cooldown > 0 && (db.Cooldown > Time)) {
                            container.Add(new CuiPanel
                            {
                                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.846 1", OffsetMax = "0 0" },
                                Image = { Color = "1 1 1 0.015", Material = "assets/content/ui/uibackgroundblur.mat" }
                            }, ".Kit");

                            container.Add(new CuiLabel
                            {
                                RectTransform = { AnchorMin = "0.03 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                                Text = { Text = item.DisplayName, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", Color = HexToCuiColor("#a2a3a5", 70), FontSize = 11 }
                            }, ".Kit");

                            container.Add(new CuiLabel
                            {
                                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.82 1", OffsetMax = "0 0" },
                                Text = { Text = $"{FormatShortTime(TimeSpan.FromSeconds(db.Cooldown - Time))}", Align = TextAnchor.MiddleRight, Font = "robotocondensed-regular.ttf", Color = HexToCuiColor("#a2a3a5", 70), FontSize = 11 }
                            }, ".Kit");
                        }
                        else {
                            container.Add(new CuiPanel
                            {
                                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.693 1", OffsetMax = "0 0" },
                                Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
                            }, ".Kit");

                            container.Add(new CuiPanel
                            {
                                RectTransform = { AnchorMin = "0.707 0", AnchorMax = "0.846 1", OffsetMax = "0 0" },
                                Image = { Color = HexToCuiColor("#bbc47e", 10), Material = "assets/content/ui/uibackgroundblur.mat" },
                            }, ".Kit", ".ImageTake");

                            container.Add(new CuiElement
                            {
                                Parent = ".ImageTake",
                                Components = 
                                {
                                    new CuiImageComponent { Sprite = "assets/icons/picked up.png", Color = HexToCuiColor("#bbc47e", 100) },
                                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = $"1 1", OffsetMin = "7 6.5", OffsetMax = "-7 -6.5" },
                                }
                            });

                            container.Add(new CuiLabel
                            {
                                RectTransform = { AnchorMin = "0.03 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                                Text = { Text = item.DisplayName, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf", Color = HexToCuiColor("#cec5bb", 100), FontSize = 12 }
                            }, ".Kit");

                            container.Add(new CuiButton
                            {
                                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                                Button = { Color = "0 0 0 0", Command = $"kit take {check.Key} {item.Name}" },
                                Text = { Text = "" }
                            }, ".Kit");
                        }

                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0.861 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                            Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" },
                        }, ".Kit", ".ImageInfo");

                        container.Add(new CuiElement
                        {
                            Parent = ".ImageInfo",
                            Components = 
                            {
                                new CuiImageComponent { Sprite = "assets/icons/info.png", Color = "1 1 1 0.5" },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = $"1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" },
                            }
                        });

                        container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                            Button = { Color = "0 0 0 0", Command = $"kit info {check.Key} {item.Name}" },
                            Text = { Text = "" }
                        }, ".ImageInfo");
                    }
                    else {
                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMax = "0 0" },
                            Image = { Color = "1 1 1 0.015", Material = "assets/content/ui/uibackgroundblur.mat" }
                        }, Layer + ".Set");
                    }
                    xmin += width;
                    if (xmin + width >= 0)
                    {
                        xmin = startxBox;
                        ymin -= height + 0.016f;
                    }
                }
            }

            CuiHelper.AddUi(player, container);
        }

        void KitInfoUI(BasePlayer player, string perm, string name) {
            CuiHelper.DestroyUi(player, ".Kit_Info");
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.9" },
            }, Layer, ".Kit_Info");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0.23", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, ".Kit_Info");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Sprite = "assets/content/ui/ui.background.transparent.radial.psd", Color = HexToCuiColor("#363636", 30) }
            }, ".Kit_Info");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Close = ".Kit_Info" },
                Text = { Text = "" }
            }, ".Kit_Info");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.31 0.183", AnchorMax = "0.69 0.8171", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, ".Kit_Info", ".Info");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.09 0.938", AnchorMax = "0.772 1", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.015", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, ".Info", ".Title");

            var check = config.set.FirstOrDefault(z => z.Key == perm).Value;
            var item = check.settings.FirstOrDefault(z => z.Name == name);
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.028 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = $"{check.Description} / <color=#cec5bb><b>{item.DisplayName}</b></color>", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", Color = HexToCuiColor("#a2a3a5", 70), FontSize = 12 }
            }, ".Title");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.966 1", OffsetMax = "0 0" },
                Text = { Text = $"откат {FormatShortTime(TimeSpan.FromSeconds(item.Cooldown))}", Align = TextAnchor.MiddleRight, Font = "robotocondensed-regular.ttf", Color = HexToCuiColor("#a2a3a5", 70), FontSize = 11 }
            }, ".Title");

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.784 0.938", AnchorMax = "0.91 1", OffsetMax = "0 0"},
                Button = {Color = HexToCuiColor("#e0947a", 10), Close = ".Kit_Info", Material = "assets/content/ui/uibackgroundblur.mat" },
                Text = {Text = "Закрыть", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = HexToCuiColor("#e0947a", 100), FontSize = 12 }
            }, ".Info");

            var itemMain = item.Items.FindAll(z => z.Container == "Рюкзак");
            float width = 0.1262f, height = 0.13f, startxBox = 0.09f, startyBox = 0.925f - height, xmin = startxBox, ymin = startyBox;
            foreach (var i in Enumerable.Range(0, 24)) {
                var items = itemMain.ElementAtOrDefault(i);
                if (items != null) {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMax = "0 0" },
                        Image = { Color = "0 0 0 0" }
                    }, ".Info", ".Main");

                    if (items.Chance < 100) {
                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                            Image = { Color = "1 1 1 0.015", Material = "assets/content/ui/uibackgroundblur.mat" }
                        }, ".Main");

                        container.Add(new CuiButton
                        {
                            RectTransform = {AnchorMin = "0 0.75", AnchorMax = "0.4 1", OffsetMax = "0 0"},
                            Button = {Color = HexToCuiColor("#d1b283", 10), Close = ".Kit_Info", Material = "assets/content/ui/uibackgroundblur.mat" },
                            Text = {Text = $"{items.Chance}%", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = HexToCuiColor("#d1b283", 100), FontSize = 9 }
                        }, ".Main");
                    }
                    else {
                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                            Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
                        }, ".Main");
                    }

                    container.Add(new CuiElement
                    {
                        Parent = ".Main", 
                        Components =
                        {
                            new CuiImageComponent { ItemId = ItemManager.FindItemDefinition(items.ShortName).itemid, SkinId = items.SkinID },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" },
                        }
                    });

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0.003", AnchorMax = "0.94 0.28", OffsetMax = "0 0" },
                        Text = { Text = $"x{items.Amount}", Align = TextAnchor.MiddleRight, Font = "robotocondensed-regular.ttf", Color = HexToCuiColor("#a2a3a5", 70), FontSize = 10 }
                    }, ".Main");
                }
                else {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMax = "0 0" },
                        Image = { Color = "1 1 1 0.015", Material = "assets/content/ui/uibackgroundblur.mat" }
                    }, ".Info");
                }
                xmin += width + 0.0125f;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height + 0.0143f;
                }
            }

            var itemWear = item.Items.FindAll(z => z.Container == "Одежда");
            float width1 = 0.1262f, height1 = 0.13f, startxBox1 = 0.02f, startyBox1 = 0.35f - height1, xmin1 = startxBox1, ymin1 = startyBox1;
            foreach (var i in Enumerable.Range(0, 7)) {
                var items = itemWear.ElementAtOrDefault(i);
                if (items != null) {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + height1 * 1}", OffsetMax = "0 0" },
                        Image = { Color = "0 0 0 0" }
                    }, ".Info", ".Wear");

                    if (items.Chance < 100) {
                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                            Image = { Color = "1 1 1 0.015", Material = "assets/content/ui/uibackgroundblur.mat" }
                        }, ".Wear");

                        container.Add(new CuiButton
                        {
                            RectTransform = {AnchorMin = "0 0.75", AnchorMax = "0.4 1", OffsetMax = "0 0"},
                            Button = {Color = HexToCuiColor("#d1b283", 10), Close = ".Kit_Info", Material = "assets/content/ui/uibackgroundblur.mat" },
                            Text = {Text = $"{items.Chance}%", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = HexToCuiColor("#d1b283", 100), FontSize = 9 }
                        }, ".Wear");
                    }
                    else {
                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                            Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
                        }, ".Wear");
                    }

                    container.Add(new CuiElement
                    {
                        Parent = ".Wear", 
                        Components =
                        {
                            new CuiImageComponent { ItemId = ItemManager.FindItemDefinition(items.ShortName).itemid, SkinId = items.SkinID },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" },
                        }
                    });

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0.003", AnchorMax = "0.94 0.28", OffsetMax = "0 0" },
                        Text = { Text = $"x{items.Amount}", Align = TextAnchor.MiddleRight, Font = "robotocondensed-regular.ttf", Color = HexToCuiColor("#a2a3a5", 70), FontSize = 10 }
                    }, ".Wear");
                }
                else {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + height1 * 1}", OffsetMax = "0 0" },
                        Image = { Color = "1 1 1 0.015", Material = "assets/content/ui/uibackgroundblur.mat" }
                    }, ".Info");
                }
                xmin1 += width1 + 0.0125f;
            }

            var itemBelt = item.Items.FindAll(z => z.Container == "Пояс");
            float width2 = 0.1262f, height2 = 0.13f, startxBox2 = 0.09f, startyBox2 = 0.206f - height2, xmin2 = startxBox2, ymin2 = startyBox2;
            foreach (var i in Enumerable.Range(0, 6)) {
                var items = itemBelt.ElementAtOrDefault(i);
                if (items != null) {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{xmin2} {ymin2}", AnchorMax = $"{xmin2 + width2} {ymin2 + height2 * 1}", OffsetMax = "0 0" },
                        Image = { Color = "0 0 0 0" }
                    }, ".Info", ".Belt");

                    if (items.Chance < 100) {
                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                            Image = { Color = "1 1 1 0.015", Material = "assets/content/ui/uibackgroundblur.mat" }
                        }, ".Belt");

                        container.Add(new CuiButton
                        {
                            RectTransform = {AnchorMin = "0 0.75", AnchorMax = "0.4 1", OffsetMax = "0 0"},
                            Button = {Color = HexToCuiColor("#d1b283", 10), Close = ".Kit_Info", Material = "assets/content/ui/uibackgroundblur.mat" },
                            Text = {Text = $"{items.Chance}%", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = HexToCuiColor("#d1b283", 100), FontSize = 9 }
                        }, ".Belt");
                    }
                    else {
                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                            Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
                        }, ".Belt");
                    }

                    container.Add(new CuiElement
                    {
                        Parent = ".Belt", 
                        Components =
                        {
                            new CuiImageComponent { ItemId = ItemManager.FindItemDefinition(items.ShortName).itemid, SkinId = items.SkinID },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" },
                        }
                    });

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0.003", AnchorMax = "0.94 0.28", OffsetMax = "0 0" },
                        Text = { Text = $"x{items.Amount}", Align = TextAnchor.MiddleRight, Font = "robotocondensed-regular.ttf", Color = HexToCuiColor("#a2a3a5", 70), FontSize = 10 }
                    }, ".Belt");
                }
                else {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{xmin2} {ymin2}", AnchorMax = $"{xmin2 + width2} {ymin2 + height2 * 1}", OffsetMax = "0 0" },
                        Image = { Color = "1 1 1 0.015", Material = "assets/content/ui/uibackgroundblur.mat" }
                    }, ".Info");
                }
                xmin2 += width2 + 0.0125f;
            }

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.09 0", AnchorMax = "0.91 0.062", OffsetMax = "0 0"},
                Button = {Color = HexToCuiColor("#d1b283", 10), Close = ".Kit_Info", Material = "assets/content/ui/uibackgroundblur.mat" },
                Text = {Text = $"<b>Внимание!</b> Предметы с процентом выпадают только с определённым шансом.", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = HexToCuiColor("#d1b283", 100), FontSize = 11 }
            }, ".Info");


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