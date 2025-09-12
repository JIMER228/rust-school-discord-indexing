using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Quests", "Netrunner", "0.0.1")]
    internal class Quests : RustPlugin
    {
        #region Static

        private static string Layer = "QUESTS_UI";
        private static Quests _ins;
        private Dictionary<ulong, List<WorkerComponent>> Workers = new Dictionary<ulong, List<WorkerComponent>>();
        private int ImageLibraryCheck = 0;
        private Configuration _config;
        private Dictionary<ulong, Data> data;
        [PluginReference] private Plugin ImageLibrary;
        private Timer UITimer = null;
        private Timer ExpTimer = null;

        private void OpenUI(BasePlayer player) => player.SendConsoleCommand($"UI_CHANGECATEGORY WORKERS");

        private Dictionary<int, string> lvls = new Dictionary<int, string>
        {
            [1] = "первый",
            [2] = "второй",
            [3] = "третий",
            [4] = "четвертый"
        };

        private Dictionary<string, string> types = new Dictionary<string, string>
        {
            ["WORKERS"] = "НАЁМНИКИ",
            ["BOX"] = "ИНВЕНТАРЬ",
            ["MARKET"] = "СНАРЯЖЕНИЕ"
        };

        #endregion

        #region Config

        private class Configuration
        {
            [JsonProperty(PropertyName = "[Информация] Наёмники")]
            public string infoWorkers = "В основном наёмники будут приносить рецепты, чем дороже наёмник — тем лучше рецепты он может принести. Изначально для найма доступен только первый уровень наёмника, чтобы разблокировать второй — нужно нанять первого и так далее. Набор наёмников обновляется каждый месяц (exp не вайпается).";

            [JsonProperty(PropertyName = "[Информация] Фрагменты")]
            public string infoFragments = "Собранная здесь привилегия после активации будет действовать <b>7 дней</b>, точно так-же как и обычная привилегия купленная в нашем магазине <b>bloodrust.ru</b>. После нажатия кнопки <b><ЗАБРАТЬ></b> привилегия моментально активируется на этом сервере, а предмет будет выдан в инвентарь. Весь прогресс сохраняется и доступен <b>на всех</b> наших серверах.";

            [JsonProperty(PropertyName = "Список наёмников", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Worker> workers = new List<Worker>
            {
                new Worker
                {
                    displayName = "Наёмник 1",
                    lvl = 1,
                    url = "https://i.imgur.com/heqXTGg.jpg",
                    xp = 5,
                    items = new List<MItem>
                    {
                        new MItem
                        {
                            shortname = "rifle.ak",
                            amount = 5,
                            blueprint = false,
                            command = "",
                            url = "",
                            price = 1,
                            skin = 0,
                            type = "ОРУЖИЕ"
                        },
                        new MItem
                        {
                            shortname = "rifle.ak",
                            amount = 5,
                            blueprint = true,
                            command = "",
                            url = "",
                            price = 1,
                            skin = 0,
                            type = "РЕЦЕПТЫ"
                        }
                    },
                    time = 30
                },
                new Worker
                {
                    displayName = "Наёмник 2",
                    lvl = 2,
                    url = "https://i.imgur.com/heqXTGg.jpg",
                    xp = 10,
                    items = new List<MItem>(),
                    time = 2400
                },
                new Worker
                {
                    displayName = "Наёмник 3",
                    lvl = 3,
                    url = "https://i.imgur.com/heqXTGg.jpg",
                    xp = 15,
                    items = new List<MItem>(),
                    time = 2800
                },
                new Worker
                {
                    displayName = "Наёмник 4",
                    lvl = 4,
                    url = "https://i.imgur.com/heqXTGg.jpg",
                    xp = 20,
                    items = new List<MItem>(),
                    time = 3600
                }
            };

            [JsonProperty(PropertyName = "Предметы, продаваемые на рынке", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<MItem> market = new List<MItem>
            {
                new MItem
                {
                    shortname = "rifle.ak",
                    amount = 5,
                    blueprint = false,
                    command = "",
                    url = "",
                    price = 1,
                    skin = 0,
                    type = "ОРУЖИЕ"
                },
                new MItem
                {
                    shortname = "rifle.ak",
                    amount = 5,
                    blueprint = true,
                    command = "",
                    url = "",
                    price = 1,
                    skin = 0,
                    type = "РЕЦЕПТЫ"
                }
            };

            [JsonProperty(PropertyName = "Фрагменты", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<int, Part> parts = new Dictionary<int, Part>
            {
                [0] = new Part
                {
                    amoun = 0,
                    command = "Привилегия какая-то",
                    needAmount = 6,
                    shortname = "",
                    url = "https://i.imgur.com/3kGoLvo.jpg"
                },
                [1] = new Part
                {
                    amoun = 0,
                    command = "Привилегия какая-то",
                    needAmount = 6,
                    shortname = "",
                    url = "https://i.imgur.com/4QcimRp.jpg"
                }
            };

            [JsonProperty(PropertyName = "EXP за минуту игры на сервере")]
            public float minuteXP = 0.0025f;

            [JsonProperty(PropertyName = "Получение EXP за добычу ресурсов", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, float> resourcesXP = new Dictionary<string, float>();

            [JsonProperty(PropertyName = "Получение EXP за убийство (разрушение)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, float> destroyXP = new Dictionary<string, float>();

            [JsonProperty(PropertyName = "Получение EXP за лутание ящика", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, float> lootXP = new Dictionary<string, float>();

            [JsonProperty(PropertyName = "Получение EXP за постройку", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, float> buildingXP = new Dictionary<string, float>();
        }

        private class Part
        {
            [JsonProperty(PropertyName = "Сколько нужно собрать?")]
            public int needAmount;

            [JsonProperty(PropertyName = "Ссылка на картинку")]
            public string url;

            [JsonProperty(PropertyName = "Какую команду выполнит при использовании?")]
            public string command;

            [JsonProperty(PropertyName = "ShortName предмета, который выдаст")]
            public string shortname;

            [JsonProperty(PropertyName = "Кол-во предмета, которое выдаст")]
            public int amoun;
        }

        private class Worker
        {
            [JsonProperty(PropertyName = "Отображаемое имя наёмника")]
            public string displayName;

            [JsonProperty(PropertyName = "Уровень наёмника")]
            public int lvl;

            [JsonProperty(PropertyName = "Длительность вылозки(В секундах)")]
            public int time;

            [JsonProperty(PropertyName = "Стоимость найма")]
            public float xp;

            [JsonProperty(PropertyName = "URL(Ссылка на изображение наёмника)")]
            public string url;

            [JsonProperty(PropertyName = "Предметы, список предметов, которые может принести наёмник", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<MItem> items = new List<MItem>();
        }

        private class MItem
        {
            [JsonProperty(PropertyName = "shortname")]
            public string shortname;

            [JsonProperty(PropertyName = "amount")]
            public int amount;

            [JsonProperty(PropertyName = "skinid")]
            public ulong skin;

            [JsonProperty(PropertyName = "Категория предмета")]
            public string type;

            [JsonProperty(PropertyName = "Это чертёж?")]
            public bool blueprint;

            [JsonProperty(PropertyName = "Кастомная картинка")]
            public string url;

            [JsonProperty(PropertyName = "Выполняемая команда(Для магазина)")]
            public string command;

            [JsonProperty(PropertyName = "Цена выкупа/покупки")]
            public float price;
        }

        private class PItem
        {
            public string shortname;
            public int amount;
            public string type;
            public ulong skin;
            public bool blueprint;
            public string url;
            public string command;
            public bool canTake;
            public float price;
        }

        private class DWorker
        {
            public string displayName;
            public int lvl;
            public int time;
            public float xp;
            public string url;
            public List<PItem> items = new List<PItem>();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();

        #endregion

        #region Data

        private class Data
        {
            public float xp = 0;
            public List<DWorker> workers = new List<DWorker>();
            public Dictionary<int, int> workerCache = new Dictionary<int, int>();
            public List<PItem> inventory = new List<PItem>();
            public Dictionary<int, int> parts = new Dictionary<int, int>();
            public int lastLvl = 0;
        }

        private void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/data"))
                data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Data>>(
                    $"{Name}/data");
            else data = new Dictionary<ulong, Data>();
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/data", data);
        }

        private void OnServerSave()
        {
            SaveData();
        }

        private void SaveData()
        {
            if (data != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/data", data);
        }

        #endregion

        #region OxideHooks

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || data.ContainsKey(player.userID)) return;
            var d = new Data();
            foreach (var check in _config.parts)
                d.parts.Add(check.Key, 0);
            d.workers.Add(ConvertWorker(_config.workers.Where(x => x.lvl == 1).ToList().GetRandom()));
            d.workers.Add(ConvertWorker(_config.workers.Where(x => x.lvl == 2).ToList().GetRandom()));
            d.workers.Add(ConvertWorker(_config.workers.Where(x => x.lvl == 3).ToList().GetRandom()));
            d.workers.Add(ConvertWorker(_config.workers.Where(x => x.lvl == 4).ToList().GetRandom()));
            data.Add(player.userID, d);
        }

        private void OnServerInitialized()
        {
            if (!ImageLibrary)
            {
                if (ImageLibraryCheck == 3)
                {
                    PrintError("ImageLibrary not found!Unloading");
                    Interface.Oxide.UnloadPlugin(Name);
                    return;
                }

                timer.In(1, () =>
                {
                    ImageLibraryCheck++;
                    OnServerInitialized();
                });
                return;
            }

            _ins = this;
            LoadData();
            LoadImages();
            foreach (var check in BasePlayer.activePlayerList)
                OnPlayerConnected(check);
            foreach (var check in data)
            foreach (var worker in check.Value.workerCache)
                CreateWorker(check.Key, worker.Key, worker.Value);
            ExpTimer = timer.Every(60, () =>
            {
                foreach (var check in BasePlayer.activePlayerList)
                    data[check.userID].xp += _config.minuteXP;
            });
        }

        private void Unload()
        {
            foreach (var check in UnityEngine.Object.FindObjectsOfType<WorkerComponent>())
                check.Kill();
            foreach (var check in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(check, Layer + ".bg");
            ExpTimer.Destroy();
            SaveData();
            _ins = null;
        }

        #region EXP

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity != null && (entity.PrefabName.Contains("patrol") || entity.PrefabName.Contains("bradley") || entity.PrefabName.Contains("ch47")) && info?.InitiatorPlayer != null) entity._name = info.InitiatorPlayer.UserIDString ?? "UNKNOWN";
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;

            var player = info.InitiatorPlayer;
            if (player == null)
            {
                if (entity._name?.Length == 17) player = BasePlayer.FindByID(ulong.Parse(entity._name));
                if (player == null) return;
            }

            var shortName = entity.ShortPrefabName;
            if (entity is BuildingBlock)
                shortName = "tier" + (int) ((entity as BuildingBlock).grade);
            float amount;
            if (!_config.destroyXP.TryGetValue(shortName, out amount)) return;
            if (entity.ToPlayer() != null && entity.ToPlayer().userID == player.userID) return;
            player.SendConsoleCommand($"note.inv 605467368 1 \"<color=#d4d8ca>+{amount}</color><color=#b9bdaf>EXP</color>\"");
            AddEXP(player, amount);
        }


        private void OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer player)
        {
            if (player == null || card.accessLevel != cardReader.accessLevel) return;
            float amount;
            if (!_config.buildingXP.TryGetValue($"card{card.accessLevel}", out amount)) return;
            player.SendConsoleCommand($"note.inv 605467368 1 \"<color=#d4d8ca>+{amount}</color><color=#b9bdaf>EXP</color>\"");
            AddEXP(player, amount);
        }

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            var player = plan.GetOwnerPlayer();
            var ent = go.ToBaseEntity();
            if (player == null || ent == null) return;
            float amount;
            if (!_config.buildingXP.TryGetValue(ent.ShortPrefabName, out amount)) return;
            var planItem = plan?.GetItem();
            if (planItem != null)
                if (planItem.skin != 0)
                    return;
            player.SendConsoleCommand($"note.inv 605467368 1 \"<color=#d4d8ca>+{amount}</color><color=#b9bdaf>EXP</color>\"");
            AddEXP(player, amount);
        }


        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (entity.name == "LOOTED") return;
            entity.name = "LOOTED";
            float amount;
            if (!_config.lootXP.TryGetValue(entity.ShortPrefabName, out amount)) return;

            player.SendConsoleCommand($"note.inv 605467368 1 \"<color=#d4d8ca>+{amount}</color><color=#b9bdaf>EXP</color>\"");
            AddEXP(player, amount);
        }

        private void CanAffordUpgrade(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade)
        {
            if (player == null) return;
            float amount;
            if (!_config.buildingXP.TryGetValue("tier" + (int) grade, out amount)) return;
            player.SendConsoleCommand($"note.inv 605467368 1 \"<color=#d4d8ca>+{amount}</color><color=#b9bdaf>EXP</color>\"");
            AddEXP(player, amount);
        }

        private void OnCollectiblePickup(Item item, BasePlayer player, CollectibleEntity entity)
        {
            float amount;
            if (!_config.resourcesXP.TryGetValue(entity.ShortPrefabName, out amount)) return;
            player.SendConsoleCommand($"note.inv 605467368 1 \"<color=#d4d8ca>+{amount}</color><color=#b9bdaf>EXP</color>\"");
            AddEXP(player, amount);
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            var shortName = "";
            switch (item.info.shortname)
            {
                case "wood":
                    shortName = "tree";
                    break;
                case "sulfur.ore":
                    shortName = "sulfur-ore";
                    break;
                case "metal.ore":
                    shortName = "metal-ore";
                    break;
                case "stones":
                    shortName = "stones";
                    break;
            }

            float amount;
            if (!_config.resourcesXP.TryGetValue(shortName, out amount)) return;

            AddEXP(player, amount);
            player.SendConsoleCommand($"note.inv 605467368 1 \"<color=#d4d8ca>+{amount}</color><color=#b9bdaf>EXP</color>\"");
        }


        #endregion

        #endregion

        #region Commands

        [ConsoleCommand("giveexpforplayer")]
        private void cmdConsolegiveexpforplayer(ConsoleSystem.Arg arg)
        {
            if (arg?.Args == null || arg.Args.Length < 1) return;
            var id = ulong.Parse(arg.Args[0]);
            if (!data.ContainsKey(id)) return;
            data[id].xp += float.Parse(arg.Args[1]);
        }

        [ConsoleCommand("UI_CHANGECATEGORY")]
        private void cmdConsoleUI_CHANGECATEGORY(ConsoleSystem.Arg arg)
        {
            if (arg?.Args == null || arg.Args.Length < 1) return;
            var player = arg.Player();
            var type = arg.Args[0];
            ShowUIMainly(player, type);
            var length = arg.Args.Length;
            switch (type)
            {
                case "WORKERS":
                    ShowUIWork(player);
                    break;
                case "BOX":
                    ShowUIBox(player, length > 2 ? int.Parse(arg.Args[1]) : 0, length > 2 ? string.Join(" ", arg.Args.Skip(2).ToArray()) : "ВСЁ");
                    break;
                case "MARKET":
                    ShowUIMarket(player, length > 2 ? int.Parse(arg.Args[1]) : 0, length > 2 ? string.Join(" ", arg.Args.Skip(2).ToArray()) : "ВСЁ");
                    break;
                case "PARTS":
                    ShowUIParts(player);
                    break;
            }
        }

        [ConsoleCommand("givepart")]
        private void cmdConsolegivepart(ConsoleSystem.Arg arg)
        {
            if (arg?.Args == null || arg.Args.Length < 1) return;
            data[ulong.Parse(arg.Args[0])].parts[int.Parse(arg.Args[1])] += int.Parse(arg.Args[2]);
        }

        [ConsoleCommand("UI_WORKERSTART")]
        private void cmdConsoleUI_WORKERSTART(ConsoleSystem.Arg arg)
        {
            if (arg?.Args == null || arg.Args.Length < 1) return;
            var player = arg.Player();
            var d = data[player.userID];
            var work = d.workers.FirstOrDefault(x => x.lvl == int.Parse(arg.Args[0]));
            if (d.xp < work.xp) return;
            if (d.lastLvl < work.lvl) d.lastLvl = work.lvl;
            CreateWorker(player.userID, work.lvl, work.time);
            d.xp -= work.xp;
            player.SendConsoleCommand("UI_CHANGECATEGORY WORKERS");
        }

        [ConsoleCommand("UI_TAKEITEMSFROMWORKER")]
        private void cmdConsoleUI_TAKEITEMSFROMWORKER(ConsoleSystem.Arg arg)
        {
            if (arg?.Args == null || arg.Args.Length < 1) return;
            var player = arg.Player();
            var d = data[player.userID];
            var work = d.workers.FirstOrDefault(x => x.lvl == int.Parse(arg.Args[0]));
            foreach (var check in work.items)
                d.inventory.Add(check);
            work.items.Clear();
            player.SendConsoleCommand("UI_CHANGECATEGORY WORKERS");
        }

        [ConsoleCommand("UI_TAKEFROMINV")]
        private void cmdConsoleUI_TAKEFROMINV(ConsoleSystem.Arg arg)
        {
            if (arg?.Args == null || arg.Args.Length < 1) return;
            var player = arg.Player();
            var d = data[player.userID];
            var id = int.Parse(arg.Args[0]);
            if (d.inventory.Count - 1 < id) return;
            var item = d.inventory[id];
            if (!string.IsNullOrEmpty(item.shortname))
            {
                if (item.blueprint)
                {
                    var blueprint = ItemManager.CreateByItemID(-996920608, 1 ,0);
                    blueprint.blueprintTarget = ItemManager.FindItemDefinition(item.shortname).itemid;
                    player.GiveItem(blueprint);
                }
                else player.GiveItem(ItemManager.CreateByName(item.shortname, item.amount, item.skin));
            }

            Server.Command(item.command.Replace("%STEAMID%", player.UserIDString));
            d.inventory.Remove(item);
            player.SendConsoleCommand("UI_CHANGECATEGORY BOX");
        }

        [ConsoleCommand("UI_SELLFROMINV")]
        private void cmdConsoleUI_BUYFROMINV(ConsoleSystem.Arg arg)
        {
            if (arg?.Args == null || arg.Args.Length < 1) return;
            var player = arg.Player();
            var d = data[player.userID];
            var item = d.inventory[int.Parse(arg.Args[0])];
            d.xp += item.price;
            d.inventory.Remove(item);
            player.SendConsoleCommand("UI_CHANGECATEGORY BOX");
        }

        [ConsoleCommand("UI_BUYFROMMARKET")]
        private void cmdConsoleUI_BUYFROMMARKET(ConsoleSystem.Arg arg)
        {
            if (arg?.Args == null || arg.Args.Length < 1) return;
            var player = arg.Player();
            var item = _config.market[int.Parse(arg.Args[0])];
            var d = data[player.userID];
            if (d.xp < item.price) return;
            d.xp -= item.price;
            var i = ConverPItem(item);
            i.canTake = true;
            d.inventory.Add(i);
            player.SendConsoleCommand("UI_CHANGECATEGORY MARKET");
        }

        [ConsoleCommand("UI_TAKEPART")]
        private void cmdConsoleUI_TAKEPART(ConsoleSystem.Arg arg)
        {
            if (arg?.Args == null || arg.Args.Length < 1) return;
            var player = arg.Player();
            var partID = int.Parse(arg.Args[0]);
            var part = _config.parts[partID];
            if (!string.IsNullOrEmpty(part.shortname)) player.GiveItem(ItemManager.CreateByName(part.shortname, part.amoun));
            Server.Command(part.command.Replace("%STEAMID%", player.UserIDString));
            data[player.userID].parts[partID] -= part.needAmount;
            player.SendConsoleCommand("UI_CHANGECATEGORY PARTS");
        }

        #endregion

        #region Functions

        private void AddEXP(BasePlayer player, float amount)
        { 
            if (!data.ContainsKey(player.userID)) return;
            data[player.userID].xp += amount;
        }

        private static PItem ConverPItem(MItem item) => new PItem {shortname = item.shortname, amount = item.amount, blueprint = item.blueprint, command = item.command, skin = item.skin, type = item.type, url = item.url, price = item.price, canTake = false};
        private static DWorker ConvertWorker(Worker worker) => new DWorker {displayName = worker.displayName, xp = worker.xp, lvl = worker.lvl, time = worker.time, url = worker.url, items = new List<PItem>()};

        private void LoadImages()
        {
            if (!ImageLibrary.Call<bool>("HasImage", "Custom_Locker_Image_Murder")) ImageLibrary.Call("AddImage", "https://i.imgur.com/YkC5Np4.png", "Custom_Locker_Image_Murder");
            if (!ImageLibrary.Call<bool>("HasImage", "Custom_Blueprint")) ImageLibrary.Call("AddImage", "http://static.moscow.ovh/images/games/rust/icons/blueprintbase.png", "Custom_Blueprint");
            foreach (var check in _config.market)
            {
                if (ImageLibrary.Call<bool>("HasImage", check.url)) continue;
                ImageLibrary.Call("AddImage", check.url, check.url);
            }

            foreach (var check in _config.parts)
            {
                if (ImageLibrary.Call<bool>("HasImage", check.Value.url)) continue;
                ImageLibrary.Call("AddImage", check.Value.url, check.Value.url);
            }

            foreach (var check in _config.workers)
            {
                if (ImageLibrary.Call<bool>("HasImage", check.url)) continue;
                ImageLibrary.Call("AddImage", check.url, check.url);
            }
        }

        private void CreateWorker(ulong id, int lvl, int time)
        {
            var obj = new GameObject().AddComponent<WorkerComponent>();
            obj.InitWorker(id, lvl, time);
            if (Workers.ContainsKey(id)) Workers[id].Add(obj);
            else Workers.Add(id, new List<WorkerComponent> {obj});
        }

        private class WorkerComponent : FacepunchBehaviour
        {
            private ulong id;
            public int lvl;
            private DateTime end;

            public void InitWorker(ulong playerID, int workerLvl, int time)
            {
                id = playerID;
                lvl = workerLvl;
                end = DateTime.Now.AddSeconds(time);
                Invoke(GiveLoot, time);
            }

            public int GetSeconds()
            {
                var time = (int) end.Subtract(DateTime.Now).TotalSeconds;
                if (time > 0) return time;
                CancelInvoke(GiveLoot);
                GiveLoot();
                return time;
            }

            private void GiveLoot()
            {
                var items = _ins._config.workers.FirstOrDefault(x => x.lvl == lvl);
                if (items == null) return;
                var worker = _ins.data[id].workers[lvl - 1];
                for (var i = 0; i < 5; i++)
                {
                    var x = items.items.GetRandom();
                    if (x == null) continue;
                    var item = ConverPItem(x);
                    item.canTake = true;
                    worker.items.Add(item);
                }

                _ins.data[id].workerCache.Remove(lvl);
                _ins.Workers[id].Remove(this);
                Destroy(gameObject);
            }

            public void Kill()
            {
                var d = _ins.data[id].workerCache;
                if (d.ContainsKey(lvl)) d[lvl] = (int) end.Subtract(DateTime.Now).TotalSeconds;
                else d.Add(lvl, (int) end.Subtract(DateTime.Now).TotalSeconds);
                CancelInvoke(GiveLoot);
                Destroy(gameObject);
            }
        }

        #endregion

        #region UI

        private void ShowUIWork(BasePlayer player)
        {
            var d = data[player.userID];
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.312 0", AnchorMax = "1 1.03"},
                Image = {Color = "0 0 0 0"}
            }, Layer, Layer + ".R");

            container.Add(new CuiPanel()
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                Image = {Color = "0 0 0 0"}
            }, Layer + ".R", Layer + ".RP");

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -100", OffsetMax = $"-30 -30"},
                Text = {Text = "НАЁМНИКИ", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 48, Color = "0.93 0.89 0.85 1"}
            }, Layer + ".RP", Layer + ".UP");
            float margin = 0;
            foreach (var check in d.workers)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = $"0 {-105 - margin}", OffsetMax = $"0 {10 - margin}"},
                    Image = {Color = "0.815 0.776 0.741 0.2"}
                }, Layer + ".UP", Layer + ".UP" + margin / 115);

                container.Add(new CuiElement
                {
                    Parent = Layer + ".UP" + margin / 115,
                    Name = Layer + ".UP" + margin / 115 + ".Avatar",
                    Components =
                    {
                        new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", check.url)},
                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "0 1", OffsetMax = "115 0"}
                    }
                });

                if (d.lastLvl < check.lvl - 1)
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".UP" + margin / 115,
                        Name = Layer + ".UP" + margin / 115 + ".Avatar",
                        Components =
                        {
                            new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", "Custom_Locker_Image_Murder"), Color = "0 0 0 1"},
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "0 1", OffsetMax = "115 0"}
                        }
                    });
                }

                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMax = "0 15"},
                    Image = {Color = "0.105 0.117 0.094 0.9"}
                }, Layer + ".UP" + margin / 115 + ".Avatar", Layer + ".UP" + margin / 115 + ".Name");

                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                    Text = {Text = check.displayName, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 10}
                }, Layer + ".UP" + margin / 115 + ".Name");

                var timeSpan = TimeSpan.FromSeconds(check.time);
                var text = "";
                if (timeSpan.Hours > 0) text = timeSpan.Hours + " час.";
                if (timeSpan.Minutes > 0) text += " " + timeSpan.Minutes + " мин.";
                if (timeSpan.Seconds > 0) text += " " + timeSpan.Seconds + " сек.";

                var stats = $"Уровень: <b>{lvls[check.lvl]}</b>\nДлительность вылазки: <b>{text}</b>\nЦена за одну вылазку: <b>{check.xp}EXP</b>";

                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "10 0", OffsetMax = "500 -5"},
                    Text = {Text = stats, Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf", FontSize = 13, Color = "0.815 0.776 0.741"}
                }, Layer + ".UP" + margin / 115 + ".Avatar");
                float secondMargin = 0;

                for (int i = 0; i < 5; i++)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = $"{125 + secondMargin} 7.5", OffsetMax = $"{175 + secondMargin} 57.5"},
                        Image = {Color = "0.129 0.129 0.129 0.8"}
                    }, Layer + ".UP" + margin / 115, Layer + ".UP" + margin / 115 + i);

                    if (check.items.Count > 0)
                    {
                        if (check.items.Count >= i + 1)
                        {
                            var item = check.items[i];
                            if (item == null) continue;
                            if (item.blueprint)
                            {
                                container.Add(new CuiElement
                                {
                                    Parent = Layer + ".UP" + margin / 115 + i,
                                    Components =
                                    {
                                        new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", "Custom_Blueprint")},
                                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
                                    }
                                });
                            }

                            container.Add(new CuiElement
                            {
                                Parent = Layer + ".UP" + margin / 115 + i,
                                Components =
                                {
                                    new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", string.IsNullOrEmpty(item.url) ? item.shortname : item.url)},
                                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
                                }
                            });
                        }
                    }
                    else
                    {
                        container.Add(new CuiButton
                        {
                            RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"10 10", OffsetMax = $"-10 -10"},
                            Button = {Color = "0.129 0.129 0.129 0.9", Sprite = "assets/icons/picked up.png"},
                            Text = {Text = ""}
                        }, Layer + ".UP" + margin / 115 + i);
                    }

                    secondMargin += 55;
                }

                var buttonColor = "0.462 0.49 0.27";
                var buttonText = "<b>НАНЯТЬ</b>";
                var buttonCommand = $"UI_WORKERSTART {check.lvl}";
                if (d.xp < check.xp)
                {
                    buttonColor = "0.552 0.278 0.231";
                    buttonText = "НЕДОСТАТОЧНО\nОПЫТА";
                    buttonCommand = "";
                }

                if (d.lastLvl < check.lvl - 1)
                {
                    buttonColor = "0.294 0.274 0.254";
                    buttonText = "НЕ ДОСТУПНО";
                    buttonCommand = "";
                }

                if (Workers.ContainsKey(player.userID) && Workers[player.userID].Count(x => x.lvl == check.lvl) > 0)
                {
                    buttonColor = "0.462 0.49 0.27";
                    var time = Workers[player.userID].FirstOrDefault(x => check != null && x.lvl == check.lvl).GetSeconds();
                    if (time <= 0)
                    {
                        ShowUIWork(player);
                        return;
                    }
                    buttonText = $"ПОИСК\n{time}сек.";
                    buttonCommand = "";
                }

                if (check.items.Count > 0)
                {
                    buttonColor = "0.462 0.49 0.27";
                    buttonText = $"ЗАБРАТЬ\nВ ЯЩИК";
                    buttonCommand = $"UI_TAKEITEMSFROMWORKER {check.lvl}";
                }

                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-120 7.5", OffsetMax = "-7.5 57.5"},
                    Image = {Color = "0 0 0 0"}
                }, Layer + ".UP" + margin / 115, Layer + ".UP" + margin / 115 + ".B");

                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                    Image = {Color = buttonColor}
                }, Layer + ".UP" + margin / 115 + ".B");

                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                    Text = {Text = buttonText, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 14, Color = "0.815 0.776 0.741"}
                }, Layer + ".UP" + margin / 115 + ".B");

                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                    Button = {Color = "0 0 0 0", Command = buttonCommand},
                    Text = {Text = ""}
                }, Layer + ".UP" + margin / 115 + ".B", Layer + ".UP" + margin / 115 + ".B.Vis");

                margin += 125;
            }



            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"28 -100", OffsetMax = $"-30 -5"},
                Text = {Text = "ИНФОРМАЦИЯ", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 48, Color = "0.93 0.89 0.85 1"}
            }, Layer + ".H");

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "30 0", OffsetMax = "0 -60"},
                Text = {Text = _config.infoWorkers, Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf", FontSize = 13, Color = "0.67 0.63 0.596"}
            }, Layer + ".H");

            CuiHelper.AddUi(player, container);
        }

        private void ShowUIParts(BasePlayer player)
        {
            var d = data[player.userID];
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.312 0", AnchorMax = "1 1"},
                Image = {Color = "0 0 0 0"}
            }, Layer, Layer + ".R");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                Image = {Color = "0 0 0 0"}
            }, Layer + ".R", Layer + ".RP");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -140", OffsetMax = "0 0"},
                Image = {Color = "0.29411 0.27450 0.254901 0.6"}
            }, Layer + ".R", Layer + ".H");

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"28 -100", OffsetMax = $"-30 -5"},
                Text = {Text = "ИНФОРМАЦИЯ", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 48, Color = "0.93 0.89 0.85 1"}
            }, Layer + ".H");

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "30 0", OffsetMax = "0 -60"},
                Text = {Text = _config.infoFragments, Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf", FontSize = 14, Color = "0.67 0.63 0.596"}
            }, Layer + ".H");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "30 30", OffsetMax = "-30 -155"},
                Image = {Color = "1 1 1 0"}
            }, Layer + ".RP", Layer + ".RPHolder");
            var pString = 4;
            var pHeight = 208;
            var elemCount = 1f / pString;
            var elementId = 0;
            float topMargin = -13;
            foreach (var check in _config.parts)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = $"{elementId * elemCount} 1", AnchorMax = $"{(elementId + 1) * elemCount} 1", OffsetMin = $"3 {topMargin - pHeight}", OffsetMax = $"-3 {topMargin}"},
                    Image = {Color = "1 1 1 0.3"}
                }, Layer + ".RPHolder", Layer + $".RPHolder.{elementId}");

                container.Add(new CuiElement
                {
                    Parent = Layer + $".RPHolder.{elementId}",
                    Components =
                    {
                        new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", check.Value.url)},
                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"}
                    }
                });

                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                    Image = {Color = "0 0 0 0.2", Material = "assets/content/ui/uibackgroundblur.mat"}
                }, Layer + $".RPHolder.{elementId}");


                container.Add(new CuiElement
                {
                    Parent = Layer + $".RPHolder.{elementId}",
                    Components =
                    {
                        new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", check.Value.url)},
                        new CuiRectTransformComponent {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-68 -104", OffsetMax = "68 104"}
                    }
                });


                var canGet = d.parts[check.Key] >= check.Value.needAmount;

                if (!canGet)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-1 0", OffsetMax = "1 0"},
                        Image = {Color = "0 0 0 0.9"}
                    }, Layer + $".RPHolder.{elementId}");
                }

                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0.16", AnchorMax = "1 0.16", OffsetMin = "-1 0", OffsetMax = "1 20"},
                    Button = {Color = canGet ? "0 0 0 0.9" : "0 0 0 0.7"},
                    Text = {Text = $"Собрано: <b>{d.parts[check.Key]} из {check.Value.needAmount}</b>".ToUpper(), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 14, Color = canGet ? "1 1 1 0.9" : "1 1 1 0.5"}
                }, Layer + $".RPHolder.{elementId}");

                if (canGet)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0.16", OffsetMin = "-1 0", OffsetMax = "1 0"},
                        Button = {Color = "0.33 0.415 0.192 1", Command = $"UI_TAKEPART {check.Key}", Close = Layer + ".bg"},
                        Text = {Text = $"ЗАБРАТЬ".ToUpper(), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 18, Color = "0.815 0.776 0.741"}
                    }, Layer + $".RPHolder.{elementId}", Layer + $".RPHolder.{elementId}.Visual");
                }

                elementId++;
                if (elementId != 4) continue;
                elementId = 0;
                topMargin -= pHeight + 5;
            }

            CuiHelper.AddUi(player, container);
        }

        private void ShowUIMarket(BasePlayer player, int page = 0, string category = "ВСЁ")
        {
            var d = data[player.userID];
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.312 0", AnchorMax = "1 1"},
                Image = {Color = "0 0 0 0"}
            }, Layer, Layer + ".R");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                Image = {Color = "0 0 0 0"}
            }, Layer + ".R", Layer + ".RP");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "30 -70", OffsetMax = "-30 -30"},
                Image = {Color = "0 0 0 0"}
            }, Layer + ".RP", Layer + ".HRPHeader");

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                Button = {Color = "0.686 0.686 0.686 0.4"},
                Text = {Text = "РЫНОК", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "0.815 0.776 0.741", FontSize = 24}
            }, Layer + ".HRPHeader", Layer + ".HRPHeader1");

            var categories = new List<string> {"ВСЁ"};
            foreach (var cat in _config.market.Where(p => p.price > -1))
            {
                if (!categories.Contains(cat.type))
                    categories.Add(cat.type);
            }

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -25", OffsetMax = "0 0"},
                Image = {Color = "0 0 0 0"}
            }, Layer + ".HRPHeader", Layer + ".HRPCats");

            var oneItemMargin = (1 - (6) * 0.01f) / 7;

            float floatMargin = 0;
            float floatMarginVertical = -45;

            foreach (var cat in categories)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = $"{floatMargin} 0", AnchorMax = $"{floatMargin + oneItemMargin} 1", OffsetMin = $"0 {floatMarginVertical}", OffsetMax = $"0 {floatMarginVertical}"},
                    Image = {Color = category == cat ? "0.207 0.56 0.784 0.7" : "0.686 0.686 0.686 0.3"}
                }, Layer + ".HRPCats", Layer + $".HRPCats.{cat}");

                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                    Text = {Text = cat.ToUpper(), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "0.815 0.776 0.741", FontSize = 12}
                }, Layer + $".HRPCats.{cat}");

                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                    Button = {Color = "0 0 0 0", Command = $"UI_CHANGECATEGORY MARKET {page} {cat}"},
                    Text = {Text = "", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "0.815 0.776 0.741", FontSize = 14}
                }, Layer + $".HRPCats.{cat}");

                floatMargin += oneItemMargin + 0.01f;

                if (floatMargin >= 1)
                {
                    floatMargin = 0;
                    floatMarginVertical -= 30;
                }
            }

            while (categories.Count % 7 != 0)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = $"{floatMargin} 0", AnchorMax = $"{floatMargin + oneItemMargin} 1", OffsetMin = $"0 {floatMarginVertical}", OffsetMax = $"0 {floatMarginVertical}"},
                    Image = {Color = "0.686 0.686 0.686 0.2"}
                }, Layer + ".HRPCats");

                floatMargin += oneItemMargin + 0.01f;
                categories.Add("RANDOM NAME +");
            }

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"30 {20 + floatMarginVertical}", OffsetMax = $"-30 {-60 + floatMarginVertical}"},
                Image = {Color = "1 1 1 0"}
            }, Layer + ".RP", Layer + ".HRPStore");

            var itemList = _config.market.Where(p => (p.type == category || category == "ВСЁ") && p.price > -1);
            var pString = 5;
            var pHeight = 120;
            var elemCount = 1f / pString;
            var elementId = 0;
            float topMargin = 0;
            var mItems = itemList.ToList();
            foreach (var check in mItems.OrderByDescending(p => p.type).Skip(page * 5 * 4).Take(5 * 4).Select((i, t) => new {A = i, B = t}))
            {
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = $"{elementId * elemCount} 1", AnchorMax = $"{(elementId + 1) * elemCount} 1", OffsetMin = $"{0} {topMargin - pHeight}", OffsetMax = $"{(elementId == 4 ? "0" : "-5")} {topMargin}"},
                    Button = {Color = "0.815 0.776 0.741 0.15"},
                    Text = {Text = ""}
                }, Layer + ".HRPStore", Layer + $".HRPStore{check.B}");

                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = $"0 -10", OffsetMax = $"0 0"},
                    Button = {Color = "0.815 0.776 0.741 0.15"},
                    Text = {Text = ""}
                }, Layer + $".HRPStore{check.B}");

                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = $"0 -10", OffsetMax = $"0 12.5"},
                    Button = {Color = d.xp > check.A.price ? "0.294 0.356 0.18" : "0.815 0.776 0.741 0.3", Command = $"UI_BUYFROMMARKET {_config.market.IndexOf(check.A)}", Close = Layer + Layer + ".bg"},
                    Text = {Text = $"КУПИТЬ ЗА <b>{check.A.price}</b>EXP", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = d.xp > check.A.price ? "0.62 0.886 0.188" : "0.815 0.776 0.741 0.5"}
                }, Layer + $".HRPStore{check.B}", Layer + $".HRPStore{check.B}.Visuaal");

                if (check.A.blueprint)
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + $".HRPStore{check.B}",
                        Components =
                        {
                            new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", "Custom_Blueprint")},
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "15 25", OffsetMax = "-15 -10"}
                        }
                    });
                }

                container.Add(new CuiElement
                {
                    Parent = Layer + $".HRPStore{check.B}",
                    Components =
                    {
                        new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", string.IsNullOrEmpty(check.A.url) ? check.A.shortname : check.A.url), Color = "1 1 1 1"},
                        new CuiRectTransformComponent {AnchorMin = "0.5 0", AnchorMax = "0.5 1", OffsetMin = "-50 15", OffsetMax = "50 -5"}
                    }
                });


                elementId++;
                if (elementId != 5) continue;
                elementId = 0;

                topMargin -= pHeight + 15;
            }

            #region PaginationMember

            var leftActive = page > 0;
            var rightActive = (page + 1) * 5 * 4 < mItems.Count();

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = $"-120 10", OffsetMax = "-30 40"},
                Image = {Color = "0 0 0 0"}
            }, Layer + ".RP", Layer + ".Holder.PS");

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = "-1000 0", OffsetMax = "-10 0"},
                Text = {Text = "• ПРИОБРЕТЁННЫЙ ТОВАР БУДЕТ ПОМЕЩЁН В ЯЩИК НАЁМНИКОВ", Align = TextAnchor.MiddleRight, Font = "robotocondensed-regular.ttf", FontSize = 14, Color = "0.815 0.776 0.741 0.3"}
            }, Layer + ".Holder.PS");

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                Text = {Text = (page + 1).ToString(), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 16}
            }, Layer + ".Holder" + ".PS");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "0.35 1", OffsetMin = $"2 2", OffsetMax = "-2 -2"},
                Image = {Color = "0.294 0.38 0.168 0"}
            }, Layer + ".Holder" + ".PS", Layer + ".Holder" + ".PS.L");

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                Button = {Color = "0 0 0 0", Command = leftActive ? $"UI_CHANGECATEGORY MARKET {page - 1} {category}" : ""},
                Text = {Text = "<b>◄</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = leftActive ? "0.647 0.917 0.188 1" : "0.294 0.38 0.168 0.3"}
            }, Layer + ".Holder" + ".PS.L");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.65 0", AnchorMax = "1 1", OffsetMin = $"2 2", OffsetMax = "-2 -2"},
                Image = {Color = "0.294 0.38 0.168 0"}
            }, Layer + ".Holder" + ".PS", Layer + ".Holder" + ".PS.R");

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                Button = {Color = "0 0 0 0", Command = rightActive ? $"UI_CHANGECATEGORY MARKET {page + 1} {category}" : ""},
                Text = {Text = "<b>►</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.647 0.917 0.188 1" : "0.294 0.38 0.168 0.3"}
            }, Layer + ".Holder" + ".PS.R");

            #endregion

            CuiHelper.AddUi(player, container);
        }

        private void ShowUIBox(BasePlayer player, int page = 0, string category = "ВСЁ")
        {
            var d = data[player.userID];
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.312 0", AnchorMax = "1 1"},
                Image = {Color = "0 0 0 0"}
            }, Layer, Layer + ".R");

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                Image = {Color = "0 0 0 0"}
            }, Layer + ".R", Layer + ".RP");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "30 -70", OffsetMax = "-30 -30"},
                Image = {Color = "0 0 0 0"}
            }, Layer + ".RP", Layer + ".HRPHeader");

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                Button = {Color = "0.686 0.686 0.686 0.4"},
                Text = {Text = "ИНВЕНТАРЬ НАЁМНИКОВ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "0.815 0.776 0.741 1", FontSize = 24}
            }, Layer + ".HRPHeader", Layer + ".HRPHeader1");

            var categories = new List<string> {"ВСЁ"};
            foreach (var cat in d.inventory)
            {
                if (!categories.Contains(cat.type))
                    categories.Add(cat.type);
            }

            if (categories.Count > 7)
                categories.RemoveRange(7, categories.Count - 7);

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 -30", OffsetMax = "0 -5"},
                Image = {Color = "0 0 0 0"}
            }, Layer + ".HRPHeader", Layer + ".HRPCats");

            var oneItemMargin = (1 - (categories.Count - 1) * 0.01f) / categories.Count;

            float floatMargin = 0;
            foreach (var cat in categories)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = $"{floatMargin} 0", AnchorMax = $"{Math.Min(floatMargin + oneItemMargin, 1)} 1", OffsetMax = "0 0"},
                    Image = {Color = category == cat ? "0.207 0.56 0.784 0.7" : "0.686 0.686 0.686 0.3"}
                }, Layer + ".HRPCats", Layer + $".HRPCats.{cat}");

                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                    Text = {Text = cat.ToUpper(), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "0.815 0.776 0.741", FontSize = 12}
                }, Layer + $".HRPCats.{cat}");

                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                    Button = {Color = "0 0 0 0", Command = $"UI_CHANGECATEGORY BOX {page} {cat}"},
                    Text = {Text = "", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "0.815 0.776 0.741", FontSize = 14}
                }, Layer + $".HRPCats.{cat}");

                floatMargin += oneItemMargin + 0.01f;
            }

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "30 55", OffsetMax = "-30 -105"},
                Image = {Color = "1 1 1 0"}
            }, Layer + ".RP", Layer + ".HRPStore");

            var inventory = d.inventory.Where(p => p.type == category || category == "ВСЁ").ToList();
            var pString = 6;
            var pHeight = 90f;
            var elemCount = 1f / pString;
            var elementId = 0;
            float topMargin = 0;
            foreach (var check in inventory.Skip(page * 6 * 6).Take(6 * 6).Select((i, t) => new {A = i, B = t}))
            {
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = $"{elementId * elemCount} 1", AnchorMax = $"{(elementId + 1) * elemCount} 1", OffsetMin = $"{(elementId == 0 ? "0" : "2.5")} {topMargin - pHeight}", OffsetMax = $"{(elementId == 5 ? "0" : "-2.5")} {topMargin}"},
                    Button = {Color = "0.815 0.776 0.741 0.15"},
                    Text = {Text = ""}
                }, Layer + ".HRPStore", Layer + $".HRPStore{elementId}");

                if (check.A.blueprint)
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + $".HRPStore{elementId}",
                        Components =
                        {
                            new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", "Custom_Blueprint")},
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "10 10", OffsetMax = "-10 -10"}
                        }
                    });
                }

                container.Add(new CuiElement
                {
                    Parent = Layer + $".HRPStore{elementId}",
                    Components =
                    {
                        new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", string.IsNullOrEmpty(check.A.url) ? check.A.shortname : check.A.url), Color = "1 1 1 1"},
                        new CuiRectTransformComponent {AnchorMin = "0.5 0", AnchorMax = "0.5 1", OffsetMin = "-40 5", OffsetMax = "40 -5"}
                    }
                });

                container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                    Image = {Color = "1 1 1 0.2"}
                }, Layer + $".HRPStore{elementId}", Layer + $".HRPStore{elementId}.Overflow");

                if (check.A.canTake)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 27.5", OffsetMax = "0 45"},
                        Button = {Color = "0.105 0.117 0.094 0.9", Command = $"UI_TAKEFROMINV {d.inventory.IndexOf(check.A)}"},
                        Text = {Text = "Забрать", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 10}
                    }, Layer + $".HRPStore{elementId}.Overflow");
                }

                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 5", OffsetMax = "0 22.5"},
                    Button = {Color = "0.105 0.117 0.094 0.9", Command = $"UI_SELLFROMINV {d.inventory.IndexOf(check.A)}"},
                    Text = {Text = $"Вернуть за {check.A.price}exp", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 10}
                }, Layer + $".HRPStore{elementId}.Overflow");

                elementId++;
                if (elementId != 6) continue;
                elementId = 0;
                topMargin -= pHeight + 5;
            }

            #region PaginationMember

            bool leftActive = page > 0;
            bool rightActive = (page + 1) * 6 * 6 < inventory.Count;

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = $"-120 10", OffsetMax = "-30 40"},
                Image = {Color = "0 0 0 0"}
            }, Layer + ".RP", Layer + ".Holder.PS");

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                Text = {Text = (page + 1).ToString(), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 16}
            }, Layer + ".Holder" + ".PS");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "0.35 1", OffsetMin = $"2 2", OffsetMax = "-2 -2"},
                Image = {Color = "0.294 0.38 0.168 0"}
            }, Layer + ".Holder" + ".PS", Layer + ".Holder" + ".PS.L");

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                Button = {Color = "0 0 0 0", Command = leftActive ? $"UI_CHANGECATEGORY BOX {page - 1} {category}" : ""},
                Text = {Text = "<b>◄</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = leftActive ? "0.647 0.917 0.188 1" : "0.294 0.38 0.168 0.3"}
            }, Layer + ".Holder" + ".PS.L");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.65 0", AnchorMax = "1 1", OffsetMin = $"2 2", OffsetMax = "-2 -2"},
                Image = {Color = "0.294 0.38 0.168 0"}
            }, Layer + ".Holder" + ".PS", Layer + ".Holder" + ".PS.R");

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                Button = {Color = "0 0 0 0", Command = rightActive ? $"UI_CHANGECATEGORY BOX {page + 1} {category}" : ""},
                Text = {Text = "<b>►</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.647 0.917 0.188 1" : "0.294 0.38 0.168 0.3"}
            }, Layer + ".Holder" + ".PS.R");

            #endregion


            CuiHelper.AddUi(player, container);
        }

        private void ShowUIMainly(BasePlayer player, string type)
        {
            var d = data[player.userID];
            var a = 0;
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "0.845 1"},
                Image = {Color = "0.12 0.12 0.11 0.0"}
            }, "ContentUI", Layer);

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "0.212 1"},
                Image = {Color = "0.55 0.27 0.22 0.0"}
            }, Layer, Layer + ".header");

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0.88", AnchorMax = "1 1"},
                Text =
                {
                    Text = $"РЕПУТАЦИЯ: {d.xp}", Font = "robotocondensed-bold.ttf", FontSize = 28, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, Layer + ".header");

            foreach (var check in types)
            {
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = $"0 {0.564 - 0.056 * a}", AnchorMax = $"0.999 {0.62 - 0.056 * a}"},
                    Button = {Color = type == check.Key ? "0.30 0.14 0.11 1.00" : "0 0 0 0", Command = $"UI_CHANGECATEGORY {check.Key}"},
                    Text =
                    {
                        Text = check.Value + "  ", Font = "robotocondensed-bold.ttf", FontSize = 25, Align = TextAnchor.MiddleRight,
                        Color = "1 1 1 1"
                    }
                }, Layer + ".header");

                a++;
            }

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }

        #endregion
    }
}