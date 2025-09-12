using System;
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
    [Info("BloodQuests", "[LimePlugin] Chibubrik", "5.0.0")]
    internal class BloodQuests : RustPlugin
    {
        #region Static

        private static string Layer = "Quests_UI";
        private static BloodQuests _ins;
        private Dictionary<ulong, List<WorkerComponent>> Workers = new Dictionary<ulong, List<WorkerComponent>>();
        Dictionary<ulong, string> PlayerLayer = new Dictionary<ulong, string>();
        Dictionary<ulong, int> MarketPage = new Dictionary<ulong, int>();
        Dictionary<ulong, int> PlayerPage = new Dictionary<ulong, int>();
        private int ImageLibraryCheck = 0;
        private Configuration _config;
        private Dictionary<ulong, Data> data;
        [PluginReference] private Plugin ImageLibrary, BloodMenu;
        private Timer UITimer = null;
        private Timer ExpTimer = null;

        void OpenUI(BasePlayer player)
        {
            ShowUIMainly(player, "WORKERS");
            PlayerLayer[player.userID] = "WORKERS";
            NextTick(() => ShowUIWork(player));
        }

        private Dictionary<int, string> lvls = new Dictionary<int, string>
        {
            [1] = "первый",
            [2] = "второй",
            [3] = "третий",
            [4] = "четвертый"
        };

        private Dictionary<string, string> types = new Dictionary<string, string>
        {
            ["WORKERS"] = "Наёмники",
            ["MARKET"] = "Рынок",
            ["BOX"] = "Инвентарь",
            ["PARTS"] = "Сбор фрагментов"
        };

        #endregion

        #region Config

        private class Configuration
        {
            [JsonProperty(PropertyName = "[Информация] Наёмники")]
            public string infoWorkers = "В основном наёмники будут приносить рецепты, чем дороже наёмник — тем лучше рецепты он может принести. Изначально для найма доступен только первый уровень наёмника, чтобы разблокировать второй — нужно нанять первого и так далее. Набор наёмников обновляется каждый месяц (exp не вайпается).";

            [JsonProperty(PropertyName = "[Информация] Фрагменты")]
            public string infoFragments = "Соберите необходимое число фрагментов чтобы получить привилегию/предмет. Собранная здесь\nпривилегия будет действовать 6 дней, точно так-же как и обычная привилегия приобретённая на\nсайте, а так-же услуги входящие в собранную привилегию ничем не отличаются от купленной на\nсайте. После того как Вы нажмёте на кнопку - привилегия будет активирована на этом сервере, а\nпредмет (например ''Переработчик'') - будет  выдан в игровой инвентарь.";

            [JsonProperty(PropertyName = "Список наёмников", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Worker> workers = new List<Worker>
            {
                new Worker
                {
                    displayName = "Ежедневный",
                    lvl = 1,
                    url = "https://i.postimg.cc/gLvZzFTM/image-1.png",
                    xp = 5,
                    itemCount = 2,
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
                            skin = 0
                        },
                        new MItem
                        {
                            shortname = "rifle.ak",
                            amount = 5,
                            blueprint = true,
                            command = "",
                            url = "",
                            price = 1,
                            skin = 0
                        }
                    },
                    time = 30
                },
                new Worker
                {
                    displayName = "Летчик Стас",
                    lvl = 2,
                    url = "https://i.postimg.cc/RNLH2mfh/image2.png",
                    xp = 10,
                    itemCount = 3,
                    items = new List<MItem>(),
                    time = 2400
                },
                new Worker
                {
                    displayName = "Машинист Джо",
                    lvl = 3,
                    url = "https://i.postimg.cc/Hj554XkQ/image3.png",
                    xp = 15,
                    itemCount = 4,
                    items = new List<MItem>(),
                    time = 2800
                },
                new Worker
                {
                    displayName = "NPC Учёный",
                    lvl = 4,
                    url = "https://i.postimg.cc/w7pNWM1n/image.png",
                    xp = 20,
                    itemCount = 5,
                    items = new List<MItem>(),
                    time = 3600
                }
            };

            [JsonProperty(PropertyName = "Предметы, продаваемые на рынке", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<MItem> market = new List<MItem>
            {
                new MItem
                {
                    displayName = "Калашик",
                    shortname = "rifle.ak",
                    amount = 5,
                    blueprint = false,
                    command = "",
                    url = "",
                    price = 1,
                    skin = 0
                },
                new MItem
                {
                    displayName = "Калашик",
                    shortname = "rifle.ak",
                    amount = 5,
                    blueprint = true,
                    command = "",
                    url = "",
                    price = 1,
                    skin = 0
                }
            };

            [JsonProperty(PropertyName = "Фрагменты", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<int, Part> parts = new Dictionary<int, Part>
            {
                [0] = new Part
                {
                    amoun = 0,
                    command = "Привилегия какая-то",
                    needAmount = 5,
                    shortname = "",
                    url = "https://i.imgur.com/3kGoLvo.jpg"
                },
                [1] = new Part
                {
                    amoun = 0,
                    command = "Привилегия какая-то",
                    needAmount = 5,
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
            [JsonProperty(PropertyName = "Название привилегии или предмета")]
            public string displayName;
            [JsonProperty(PropertyName = "Сколько нужно собрать? (минимум 3 - максимум 5) или все пойдет по бороде")]
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

            [JsonProperty(PropertyName = "Кол-во предметов, которое может добыть наемник")]
            public int itemCount;

            [JsonProperty(PropertyName = "Откат после вылазки")]
            public double Cooldown;

            [JsonProperty(PropertyName = "Предметы, список предметов, которые может принести наёмник", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<MItem> items = new List<MItem>();
        }

        private class MItem
        {
            [JsonProperty(PropertyName = "displayName")]
            public string displayName;
            [JsonProperty(PropertyName = "shortname")]
            public string shortname;

            [JsonProperty(PropertyName = "amount")]
            public int amount;

            [JsonProperty(PropertyName = "skinid")]
            public ulong skin;

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
            public int itemCount;
            public double Cooldown;
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

        void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;

            PlayerLayer[player.userID] = "";
            MarketPage[player.userID] = 1;
            PlayerPage[player.userID] = 1;

            if (!data.ContainsKey(player.userID))
            {
                var d = new Data();

                
                if (_config?.parts != null)
                {
                    foreach (var check in _config.parts)
                        d.parts.Add(check.Key, 0);
                }

                
                if (_config?.workers != null)
                {
                    var worker1 = _config.workers.Where(x => x.lvl == 1).ToList();
                    var worker2 = _config.workers.Where(x => x.lvl == 2).ToList();
                    var worker3 = _config.workers.Where(x => x.lvl == 3).ToList();
                    var worker4 = _config.workers.Where(x => x.lvl == 4).ToList();

                    if (worker1.Any()) d.workers.Add(ConvertWorker(worker1.GetRandom()));
                    if (worker2.Any()) d.workers.Add(ConvertWorker(worker2.GetRandom()));
                    if (worker3.Any()) d.workers.Add(ConvertWorker(worker3.GetRandom()));
                    if (worker4.Any()) d.workers.Add(ConvertWorker(worker4.GetRandom()));
                }

                data.Add(player.userID, d);
            }
        }

        void OnServerInitialized()
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
                shortName = "tier" + (int)((entity as BuildingBlock).grade);
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
            if (!_config.buildingXP.TryGetValue("tier" + (int)grade, out amount)) return;
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

        [ConsoleCommand("givepart")]
        private void cmdConsolegivepart(ConsoleSystem.Arg arg)
        {
            if (arg?.Args == null || arg.Args.Length < 1) return;
            data[ulong.Parse(arg.Args[0])].parts[int.Parse(arg.Args[1])] += int.Parse(arg.Args[2]);
        }

        [ConsoleCommand("quest")]
        void ConsoleQuest(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            ShowUIMainly(player, args.Args[0]);
            ShowUIWork(player);
        }

        [ConsoleCommand("UI_CHANGECATEGORY")]
        void cmdConsoleUI_CHANGECATEGORY(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (types.ContainsKey(args.Args[0]))
            {
                if (PlayerLayer[player.userID] == args.Args[0]) return;
                PlayerLayer[player.userID] = args.Args[0];
                ShowUIMainly(player, args.Args[0]);
            }
            if (player != null && args.HasArgs(1))
            {
                if (args.Args[0] == "WORKERS")
                {
                    ShowUIWork(player);
                }
                if (args.Args[0] == "WORKERSTART")
                {
                    var d = data[player.userID];
                    var work = d.workers.FirstOrDefault(x => x.lvl == int.Parse(args.Args[1]));
                    if (d.xp < work.xp) return;
                    if (d.lastLvl < work.lvl) d.lastLvl = work.lvl;
                    CreateWorker(player.userID, work.lvl, work.time);
                    d.xp -= work.xp;
                    ShowNotify(player, $"Вы успешно наняли наемника {work.displayName}");
                    ShowUIWork(player);
                    BloodMenu?.Call("ProfileEXPUpdate", player);
                    Effect y = new Effect("assets/bundled/prefabs/fx/notice/loot.drag.grab.fx.prefab", player, 0, new Vector3(), new Vector3());
					EffectNetwork.Send(y, player.Connection);
                }
                if (args.Args[0] == "TAKEITEMSFROMWORKER")
                {
                    var d = data[player.userID];
                    var work = d.workers.FirstOrDefault(x => x.lvl == int.Parse(args.Args[1]));
                    var cfg = _config.workers.FirstOrDefault(z => z.lvl == int.Parse(args.Args[1]));
                    foreach (var check in work.items) 
                        d.inventory.Add(check);
                    work.items.Clear();
                    work.Cooldown = CurrentTime() + cfg.Cooldown;
                    ShowNotify(player, $"Вы успешно получили лут с наемника");
                    ShowUIWork(player);
                }
                if (args.Args[0] == "MARKET")
                {
                    ShowUIMarket(player, MarketPage[player.userID]);
                }
                if (args.Args[0] == "BUYFROMMARKET")
                {
                    var item = _config.market[int.Parse(args.Args[1])];
                    var d = data[player.userID];
                    if (d.xp < item.price) return;
                    d.xp -= item.price;
                    var i = ConverPItem(item);
                    i.canTake = true;
                    d.inventory.Add(i);
                    ShowNotify(player, $"Вы успешно преобрели товар {item.displayName}");
                    BloodMenu?.Call("ProfileEXPUpdate", player);
                }
                if (args.Args[0] == "BOX")
                {
                    ShowUIBox(player, PlayerPage[player.userID]);
                }
                if (args.Args[0] == "PARTS")
                {
                    ShowUIParts(player);
                }
                if (args.Args[0] == "UI_TAKEPART")
                {
                    var partID = int.Parse(args.Args[1]);
                    var part = _config.parts[partID];
                    if (!string.IsNullOrEmpty(part.shortname))
                    {
                        player.GiveItem(ItemManager.CreateByName(part.shortname, part.amoun));
                        ShowNotify(player, $"Вы успешно получили предмет {part.displayName}");
                    }
                    if (!string.IsNullOrEmpty(part.command))
                    {
                        Server.Command(part.command.Replace("%STEAMID%", player.UserIDString));
                        ShowNotify(player, $"Вы успешно получили {part.displayName}");
                    }
                    data[player.userID].parts[partID] -= part.needAmount;
                    ShowUIParts(player);
                }
                if (args.Args[0] == "HELP")
                {
                    ShowUIInforamtion(player, args.Args[1]);
                }
                if (args.Args[0] == "PAGE")
                {
                    var page = int.Parse(args.Args[2]);
                    if (args.Args[1] == "MARKET")
                    {
                        if (MarketPage[player.userID] == page) return;
                        MarketPage[player.userID] = page;
                        ShowUIMarket(player, MarketPage[player.userID]);
                    }
                    else
                    {
                        if (PlayerPage[player.userID] == page) return;
                        PlayerPage[player.userID] = page;
                        ShowUIBox(player, PlayerPage[player.userID]);
                    }
                }
                if (args.Args[0] == "SELLGIVE")
                {
                    ShowSellGive(player, int.Parse(args.Args[1]), int.Parse(args.Args[2]), float.Parse(args.Args[3]), bool.Parse(args.Args[4]));
                }
                if (args.Args[0] == "TAKEFROMINV")
                {
                    var d = data[player.userID];
                    var id = int.Parse(args.Args[1]);
                    if (d.inventory.Count - 1 < id) return;
                    var item = d.inventory[id];
                    if (!string.IsNullOrEmpty(item.shortname))
                    {
                        if (item.blueprint)
                        {
                            var blueprint = ItemManager.CreateByItemID(-996920608, 1, 0);
                            blueprint.blueprintTarget = ItemManager.FindItemDefinition(item.shortname).itemid;
                            player.GiveItem(blueprint);
                            ShowNotify(player, $"Вы успешно получили принт {item.shortname}");
                        }
                        else
                        {
                            player.GiveItem(ItemManager.CreateByName(item.shortname, item.amount, item.skin));
                            ShowNotify(player, $"Вы успешно получили предмет {item.shortname}");
                        }
                    }

                    if (!string.IsNullOrEmpty(item.command))
                    {
                        Server.Command(item.command.Replace("%STEAMID%", player.UserIDString));
                        ShowNotify(player, $"Вы успешно получили услугу");
                    }
                    d.inventory.Remove(item);
                    ShowUIBox(player, PlayerPage[player.userID]);
                }
                if (args.Args[0] == "SELLFROMINV")
                {
                    var d = data[player.userID];
                    var item = d.inventory[int.Parse(args.Args[1])];
                    d.xp += item.price;
                    d.inventory.Remove(item);
                    ShowNotify(player, $"Вы успешно обменяли предмет на EXP");
                    ShowUIBox(player, PlayerPage[player.userID]);
                    BloodMenu?.Call("ProfileEXPUpdate", player);
                }
            }
        }
        #endregion

        #region Functions

        private void AddEXP(BasePlayer player, float amount)
        {
            if (!data.ContainsKey(player.userID)) return;
            data[player.userID].xp += amount;
        }

        private static PItem ConverPItem(MItem item) => new PItem { shortname = item.shortname, amount = item.amount, blueprint = item.blueprint, command = item.command, skin = item.skin, url = item.url, price = item.price, canTake = false };
        private static DWorker ConvertWorker(Worker worker) => new DWorker { displayName = worker.displayName, xp = worker.xp, lvl = worker.lvl, time = worker.time, url = worker.url, itemCount = worker.itemCount, Cooldown = 0, items = new List<PItem>() };

        private void LoadImages()
        {
            if (!ImageLibrary.Call<bool>("HasImage", "Custom_Locker_Image_Murder")) ImageLibrary.Call("AddImage", "https://i.imgur.com/YkC5Np4.png", "Custom_Locker_Image_Murder");
            if (!ImageLibrary.Call<bool>("HasImage", "Custom_Blueprint")) ImageLibrary.Call("AddImage", "http://static.moscow.ovh/images/games/rust/icons/blueprintbase.png", "Custom_Blueprint");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/PvAhjYX.png", "PvAhjYX");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/wGos4aX.png", "wGos4aX");
            foreach (var check in _config.workers)
            {
                if (ImageLibrary.Call<bool>("HasImage", check.url)) continue;
                ImageLibrary.Call("AddImage", check.url, check.url);
            }

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
        }

        private void CreateWorker(ulong id, int lvl, int time)
        {
            var obj = new GameObject().AddComponent<WorkerComponent>();
            obj.InitWorker(id, lvl, time);
            if (Workers.ContainsKey(id)) Workers[id].Add(obj);
            else Workers.Add(id, new List<WorkerComponent> { obj });
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
                var time = (int)end.Subtract(DateTime.Now).TotalSeconds;
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
                for (var i = 0; i < items.itemCount; i++)
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
                if (d.ContainsKey(lvl)) d[lvl] = (int)end.Subtract(DateTime.Now).TotalSeconds;
                else d.Add(lvl, (int)end.Subtract(DateTime.Now).TotalSeconds);
                CancelInvoke(GiveLoot);
                Destroy(gameObject);
            }
        }

        #endregion

        #region UI

        private void ShowUIWork(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer + ".RP");
            var d = data[player.userID];
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1.05 0.9", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, Layer + ".RP");

            float margin = 0;
            float width = 0.469f, height = 0.491f, startxBox = 0f, startyBox = 1f - height, xmin = startxBox, ymin = startyBox;
            foreach (var check in d.workers)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMax = "0 0" },
                    Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
                }, Layer + ".RP", Layer + ".UP");
                xmin += width + 0.014f;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height + 0.0167f;
                }

                container.Add(new CuiElement
                {
                    Parent = Layer + ".UP",
                    Components =
                    {
                        new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", check.url)},
                        new CuiRectTransformComponent {AnchorMin = "0.03 0.53", AnchorMax = "0.355 0.97", OffsetMax = "0 0"}
                    }
                });

                if (d.lastLvl < check.lvl - 1)
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".UP",
                        Components =
                        {
                            new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", "Custom_Locker_Image_Murder"), Color = "0 0 0 1"},
                            new CuiRectTransformComponent {AnchorMin = "0.03 0.53", AnchorMax = "0.355 0.97", OffsetMax = "0 0"}
                        }
                    });
                }

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.37 0.89", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Image = { Color = "0 0 0 0" }
                }, Layer + ".UP", Layer + ".UP" + ".Name");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = "Наёмник", Color = "1 1 1 0.2", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 10 }
                }, Layer + ".UP" + ".Name");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = check.displayName + "    ", Align = TextAnchor.MiddleRight, Font = "robotocondensed-bold.ttf", FontSize = 10 }
                }, Layer + ".UP" + ".Name");

                var timeSpan = TimeSpan.FromSeconds(check.time);
                var text = "";
                if (timeSpan.Hours > 0) text = timeSpan.Hours + " ч.";
                if (timeSpan.Minutes > 0) text += " " + timeSpan.Minutes + " м.";
                if (timeSpan.Seconds > 0) text += " " + timeSpan.Seconds + " с.";

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.37 0.79", AnchorMax = "1 0.89", OffsetMax = "0 0" },
                    Image = { Color = "0 0 0 0" }
                }, Layer + ".UP", Layer + ".UP" + ".Level");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = "Уровень", Color = "1 1 1 0.2", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 10 }
                }, Layer + ".UP" + ".Level");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = lvls[check.lvl] + "    ", Align = TextAnchor.MiddleRight, Font = "robotocondensed-bold.ttf", FontSize = 10 }
                }, Layer + ".UP" + ".Level");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.37 0.71", AnchorMax = "1 0.79", OffsetMax = "0 0" },
                    Image = { Color = "0 0 0 0" }
                }, Layer + ".UP", Layer + ".UP" + ".Time");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = "Длительность вылазки", Color = "1 1 1 0.2", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 10 }
                }, Layer + ".UP" + ".Time");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = text + "    ", Align = TextAnchor.MiddleRight, Font = "robotocondensed-bold.ttf", FontSize = 10 }
                }, Layer + ".UP" + ".Time");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.37 0.6", AnchorMax = "1 0.71", OffsetMax = "0 0" },
                    Image = { Color = "0 0 0 0" }
                }, Layer + ".UP", Layer + ".UP" + ".Price");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = "Цена найма", Color = "1 1 1 0.2", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 10 }
                }, Layer + ".UP" + ".Price");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = check.xp + " EXP   ", Align = TextAnchor.MiddleRight, Color = HexToCuiColor("#cdb083", 100), Font = "robotocondensed-bold.ttf", FontSize = 10 }
                }, Layer + ".UP" + ".Price");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.37 0.51", AnchorMax = "1 0.6", OffsetMax = "0 0" },
                    Image = { Color = "0 0 0 0" }
                }, Layer + ".UP", Layer + ".UP" + ".Count");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = "Макс. кол-во предметов", Color = "1 1 1 0.2", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 10 }
                }, Layer + ".UP" + ".Count");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = $"{check.itemCount}   ", Align = TextAnchor.MiddleRight, Color = HexToCuiColor("#cdb083", 100), Font = "robotocondensed-bold.ttf", FontSize = 10 }
                }, Layer + ".UP" + ".Count");

                float secondMargin = 0;

                float width1 = 0.17f, height1 = 0.233f, startxBox1 = 0.03f, startyBox1 = 0.49f - height1, xmin1 = startxBox1, ymin1 = startyBox1;
                for (int i = 0; i < 5; i++)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + height1 * 1}", OffsetMax = "0 0" },
                        Image = { Color = "1 1 1 0.07", Material = "assets/content/ui/uibackgroundblur.mat" }
                    }, Layer + ".UP", Layer + ".UP" + i);
                    xmin1 += width1 + 0.024f;

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
                                    Parent = Layer + ".UP" + i,
                                    Components =
                                    {
                                        new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", "Custom_Blueprint")},
                                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
                                    }
                                });
                            }
                            if (string.IsNullOrEmpty(item.url))
                            {
                                container.Add(new CuiElement
                                {
                                    Parent = Layer + ".UP" + i,
                                    Components =
                                    {
                                        new CuiImageComponent { ItemId = ItemManager.FindItemDefinition(item.shortname).itemid, SkinId = 0 },
                                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
                                    }
                                });
                            }
                            else
                            {
                                container.Add(new CuiElement
                                {
                                    Parent = Layer + ".UP" + i,
                                    Components =
                                    {
                                        new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", item.url)},
                                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
                                    }
                                });
                            }
                        }
                    }
                    else
                    {
                        container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"10 10", OffsetMax = $"-10 -10" },
                            Button = { Color = "0.129 0.129 0.129 0.9", Sprite = "assets/icons/picked up.png" },
                            Text = { Text = "" }
                        }, Layer + ".UP" + i);
                    }

                    if (i >= check.itemCount)
                    {
                        container.Add(new CuiElement
                        {
                            Parent = Layer + ".UP" + i,
                            Components =
                            {
                                new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", "Custom_Locker_Image_Murder"), Color = "1 1 1 0.5"},
                                new CuiRectTransformComponent {AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0"}
                            }
                        });
                    }

                    secondMargin += 55;
                }

                var buttonColor = HexToCuiColor("#bbc47e", 40);
                var textColor = HexToCuiColor("#bbc47e", 100);
                var buttonText = "Нанять";
                var buttonCommand = $"UI_CHANGECATEGORY WORKERSTART {check.lvl}";

                var db = d.workers.FirstOrDefault(Z => Z.lvl == check.lvl);
                if (db.Cooldown > CurrentTime()) {
                    buttonColor = "1 1 1 0.07";
                    textColor = "1 1 1 0.4";
                    buttonText = $"Откат {FormatShortTime(TimeSpan.FromSeconds(db.Cooldown - CurrentTime()))}";
                    buttonCommand = "";
                }

                if (d.xp < check.xp)
                {
                    buttonColor = "1 1 1 0.07";
                    textColor = "1 1 1 0.4";
                    buttonText = "Не хватает EXP";
                    buttonCommand = "";
                }

                if (d.lastLvl < check.lvl - 1)
                {
                    buttonColor = "1 1 1 0.07";
                    textColor = "1 1 1 0.4";
                    buttonText = "Недоступно";
                    buttonCommand = "";
                }

                if (Workers.ContainsKey(player.userID) && Workers[player.userID].Count(x => x.lvl == check.lvl) > 0)
                {
                    buttonColor = HexToCuiColor("#84b4dd", 40);
                    textColor = HexToCuiColor("#84b4dd", 100);
                    var time = Workers[player.userID].FirstOrDefault(x => check != null && x.lvl == check.lvl).GetSeconds();
                    if (time <= 0)
                    {
                        ShowUIWork(player);
                        return;
                    }
                    buttonText = $"Поиск {FormatShortTime(TimeSpan.FromSeconds(time))}";
                    buttonCommand = "";
                }

                if (check.items.Count > 0)
                {
                    buttonColor = HexToCuiColor("#84b4dd", 40);
                    textColor = HexToCuiColor("#84b4dd", 100);
                    buttonText = $"Забрать в ящик";
                    buttonCommand = $"UI_CHANGECATEGORY TAKEITEMSFROMWORKER {check.lvl}";
                }

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.03 0.04", AnchorMax = "0.976 0.23", OffsetMax = "0 0" },
                    Image = { Color = "0 0 0 0" }
                }, Layer + ".UP", Layer + ".UP" + ".B");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Image = { Color = buttonColor, Material = "assets/content/ui/uibackgroundblur.mat" }
                }, Layer + ".UP" + ".B");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = buttonText, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 14, Color = textColor }
                }, Layer + ".UP" + ".B");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0", Command = buttonCommand },
                    Text = { Text = "" }
                }, Layer + ".UP" + ".B", Layer + ".UP" + ".B.Vis");

                margin += 125;
            }

            CuiHelper.AddUi(player, container);
        }

        private void ShowUIParts(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer + ".R");
            var d = data[player.userID];
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.9", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, Layer + ".R");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer + ".R", Layer + ".RP");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.3", OffsetMax = "0 0" },
                Image = { Color = HexToCuiColor("#d1b283", 40) }
            }, Layer + ".R", Layer + ".H");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.03 0.61", AnchorMax = "1 1", OffsetMax = $"0 0" },
                Text = { Text = "Информация о фрагментах", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = HexToCuiColor("#d1b283", 100) }
            }, Layer + ".H");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.03 0", AnchorMax = "1 0.68", OffsetMax = "0 0" },
                Text = { Text = _config.infoFragments, Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = HexToCuiColor("#d1b283", 80) }
            }, Layer + ".H");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.32", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer + ".RP", Layer + ".RPHolder");

            float width = 0.4958f, height = 0.237f, startxBox = 0f, startyBox = 1f - height, xmin = startxBox, ymin = startyBox;
            for (int z = 0; z < 8; z++)
            {
                var check = _config.parts.FirstOrDefault(x => x.Key == z);
                var color = check.Key == z ? "1 1 1 0" : "1 1 1 0.04";
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMax = "0 0" },
                    Image = { Color = color, Material = "assets/content/ui/uibackgroundblur.mat" }
                }, Layer + ".RPHolder", $".Parts{z}");
                xmin += width + 0.008f;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height + 0.0167f;
                }
            }

            foreach (var check in _config.parts)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"0.255 1", OffsetMax = "0 0" },
                    Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
                }, $".Parts{check.Key}", ".Image");

                var canGet = d.parts[check.Key] >= check.Value.needAmount;
                container.Add(new CuiElement
                {
                    Parent = $".Image",
                    Components =
                    {
                        new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", check.Value.url), Color = canGet ? "1 1 1 1" : "1 1 1 0.4"},
                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"}
                    }
                });

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.27 0.15", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
                }, $".Parts{check.Key}", ".Text");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.04 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = $"<color=#cec5bb><b><size=13>{check.Value.displayName}</size></b></color>\nфрагментов собрано: <b>{d.parts[check.Key]}/{check.Value.needAmount}</b>", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 10, Color = "1 1 1 0.4" }
                }, ".Text");

                var sizes = check.Value.needAmount == 3 ? 0.238f : check.Value.needAmount == 4 ? 0.1765f : 0.1396f;
                float width1 = sizes, height1 = 0.1f, startxBox1 = 0.27f, startyBox1 = 0.1f - height1, xmin1 = startxBox1, ymin1 = startyBox1;
                for (int i = 1; i <= check.Value.needAmount; i++)
                {
                    var partCompletedColor = d.parts[check.Key] >= i ? HexToCuiColor("#bbc47e", 40) : "1 1 1 0.04";
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + height1 * 1}", OffsetMax = "0 0" },
                        Image = { Color = partCompletedColor, Material = "assets/content/ui/uibackgroundblur.mat" }
                    }, $".Parts{check.Key}");
                    xmin1 += width1 + 0.008f;
                }

                if (canGet)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.78 0.25", AnchorMax = "0.93 0.75", OffsetMax = "0 0" },
                        Button = { Color = HexToCuiColor("#bbc47e", 40), Command = $"UI_CHANGECATEGORY UI_TAKEPART {check.Key}", Material = "assets/content/ui/uibackgroundblur.mat" },
                        Text = { Text = $"", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 14, Color = HexToCuiColor("#bbc47e", 100) }
                    }, $".Text", ".CompletedImage");

                    container.Add(new CuiElement
                    {
                        Parent = ".CompletedImage",
                        Components =
                        {
                            new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", "wGos4aX"), Color = HexToCuiColor("#bbc47e", 100)},
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5"}
                        }
                    });
                }
            }

            CuiHelper.AddUi(player, container);
        }

        private void ShowUIMarket(BasePlayer player, int page)
        {
            CuiHelper.DestroyUi(player, Layer + ".R");

           
            if (!data.ContainsKey(player.userID))
            {
                PrintError($"Player {player.displayName} not found in data");
                return;
            }

            var d = data[player.userID];
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1.05 0.9" },
                Image = { Color = "0 0 0 0" }
            }, Layer, Layer + ".R");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.65 0", AnchorMax = $"0.947 0.113", OffsetMax = "0 0" },
                Button = { Color = "1 1 1 0.04", Command = "UI_CHANGECATEGORY HELP MARKET", Material = "assets/content/ui/uibackgroundblur.mat" },
                Text = { Text = "Информация", Font = "robotocondensed-bold.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#cec5bb", 40) }
            }, Layer + ".R");

            float width = 0.1804f, height = 0.2793f, startxBox = 0f, startyBox = 1f - height, xmin = startxBox, ymin = startyBox;
            for (var z = 0; z < 15; z++)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMax = "0 0" },
                    Button = { Color = "1 1 1 0.01", Material = "assets/content/ui/uibackgroundblur.mat" },
                }, Layer + ".R", $".Market.{z}");
                xmin += width + 0.012f;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height + 0.0167f;
                }
            }

            
            if (_config?.market == null)
            {
                PrintError("Market config is null");
                CuiHelper.AddUi(player, container);
                return;
            }

            var mItems = _config.market.Where(item => item != null).ToList(); 

            if (mItems.Count == 0)
            {
                PrintWarning("No market items found");
                CuiHelper.AddUi(player, container);
                return;
            }

            var pagedItems = mItems.Skip((page - 1) * 15).Take(15).ToList();

            for (int i = 0; i < pagedItems.Count; i++)
            {
                var check = pagedItems[i];
                if (check == null) continue; 

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                    Button = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" },
                    Text = { Text = "" }
                }, $".Market.{i}", Layer + $".HRPStore{i}");

                
                string displayName = check.displayName ?? "Неизвестный предмет";

                if (displayName.Length > 13)
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.05 0", AnchorMax = "1 0.25", OffsetMax = "0 0" },
                        Text = { Text = displayName.Substring(0, 13) + "...", Color = "1 1 1 0.2", Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf", FontSize = 10 }
                    }, Layer + $".HRPStore{i}");
                }
                else
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.05 0", AnchorMax = "1 0.25", OffsetMax = "0 0" },
                        Text = { Text = displayName, Color = "1 1 1 0.2", Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf", FontSize = 10 }
                    }, Layer + $".HRPStore{i}");
                }

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.05 0", AnchorMax = "1 0.135", OffsetMax = "0 0" },
                    Text = { Text = $"{check.price} EXP", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 10 }
                }, Layer + $".HRPStore{i}");

                if (check.blueprint)
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + $".HRPStore{i}",
                        Components =
                {
                    new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", "Custom_Blueprint")},
                    new CuiRectTransformComponent {AnchorMin = "0 0.1", AnchorMax = "1 1", OffsetMin = "20 20", OffsetMax = "-20 -20"}
                }
                    });
                }

                if (string.IsNullOrEmpty(check.url))
                {
                    
                    if (!string.IsNullOrEmpty(check.shortname))
                    {
                        var itemDef = ItemManager.FindItemDefinition(check.shortname);
                        if (itemDef != null)
                        {
                            container.Add(new CuiElement
                            {
                                Parent = Layer + $".HRPStore{i}",
                                Components =
                        {
                            new CuiImageComponent { ItemId = itemDef.itemid, SkinId = 0 },
                            new CuiRectTransformComponent {AnchorMin = "0 0.1", AnchorMax = "1 1", OffsetMin = "15 15", OffsetMax = "-15 -15"}
                        }
                            });
                        }
                    }
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + $".HRPStore{i}",
                        Components =
                {
                    new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", check.url), Color = "1 1 1 1"},
                    new CuiRectTransformComponent {AnchorMin = "0 0.1", AnchorMax = "1 1", OffsetMin = "15 15", OffsetMax = "-15 -15"}
                }
                    });
                }

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.73 0.05", AnchorMax = "0.95 0.25", OffsetMax = "0 0" },
                    Button = { Color = "1 1 1 0.1", Material = "assets/content/ui/uibackgroundblur.mat", Command = $"UI_CHANGECATEGORY BUYFROMMARKET {_config.market.IndexOf(check)}" },
                    Text = { Text = "" }
                }, Layer + $".HRPStore{i}", Layer + $".HRPStore{i}.Image");

                container.Add(new CuiElement
                {
                    Parent = Layer + $".HRPStore{i}.Image",
                    Components =
            {
                new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", "PvAhjYX"), Color = "1 1 1 0.4"},
                new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "4 4", OffsetMax = "-4 -4"}
            }
                });
            }

            CuiHelper.AddUi(player, container);
            PagerUI(player, "MARKET", page);
        }

        private void ShowUIBox(BasePlayer player, int page)
        {
            CuiHelper.DestroyUi(player, Layer + ".R");
            var db = data[player.userID];
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1.05 0.9" },
                Image = { Color = "0 0 0 0" }
            }, Layer, Layer + ".R");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.65 0", AnchorMax = $"0.947 0.113", OffsetMax = "0 0" },
                Button = { Color = "1 1 1 0.04", Command = "UI_CHANGECATEGORY HELP BOX", Material = "assets/content/ui/uibackgroundblur.mat" },
                Text = { Text = "Информация", Font = "robotocondensed-bold.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#cec5bb", 40) }
            }, Layer + ".R");

            var elementId = 0;
            float width = 0.128f, height = 0.205f, startxBox = 0f, startyBox = 1f - height, xmin = startxBox, ymin = startyBox;
            for (var z = 0; z < 28; z++)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMax = "0 0" },
                    Button = { Color = "1 1 1 0.01", Material = "assets/content/ui/uibackgroundblur.mat" },
                }, Layer + ".R", $".Box{z}");
                xmin += width + 0.0085f;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height + 0.0167f;
                }
            }

            foreach (var check in db.inventory.Skip((page - 1) * 28).Take(28).Select((i, t) => new { A = i, B = t }))
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                    Button = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" },
                    Text = { Text = "" }
                }, $".Box{elementId}", Layer + $".HRPStore{elementId}");

                if (check.A.blueprint)
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + $".HRPStore{elementId}",
                        Components =
                        {
                            new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", "Custom_Blueprint")},
                            new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "20 20", OffsetMax = "-20 -20"}
                        }
                    });
                }

                if (string.IsNullOrEmpty(check.A.url))
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + $".HRPStore{elementId}",
                        Components =
                        {
                            new CuiImageComponent { ItemId = ItemManager.FindItemDefinition(check.A.shortname).itemid, SkinId = 0, Color = "1 1 1 1" },
                            new CuiRectTransformComponent {AnchorMin = "0 0.12", AnchorMax = "1 0.88", OffsetMin = "10 0", OffsetMax = "-10 0"}
                        }
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Parent = Layer + $".HRPStore{elementId}",
                        Components =
                        {
                            new CuiRawImageComponent {Png = ImageLibrary.Call<string>("GetImage", check.A.url), Color = "1 1 1 1"},
                            new CuiRectTransformComponent {AnchorMin = "0 0.12", AnchorMax = "1 0.88", OffsetMin = "10 0", OffsetMax = "-10 0"}
                        }
                    });
                }

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0 0.04", AnchorMax = $"0.96 1", OffsetMax = "0 0" },
                    Text = { Text = $"x{check.A.amount}", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.LowerRight, Color = HexToCuiColor("#cec5bb", 40) }
                }, Layer + $".HRPStore{elementId}");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0", Command = $"UI_CHANGECATEGORY SELLGIVE {elementId} {db.inventory.IndexOf(check.A)} {check.A.price} {check.A.canTake}" },
                    Text = { Text = "" }
                }, Layer + $".HRPStore{elementId}");

                elementId++;
            }
            CuiHelper.AddUi(player, container);
            PagerUI(player, "BOX", page);
        }

        void ShowSellGive(BasePlayer player, int id, int item, float price, bool take)
        {
            CuiHelper.DestroyUi(player, $"GiveSell");
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" },
            }, Layer + $".HRPStore{id}", "GiveSell");

            if (take)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0.55", AnchorMax = "1 0.75", OffsetMax = "0 0" },
                    Button = { Color = "0.105 0.117 0.094 0.9", Command = $"UI_CHANGECATEGORY TAKEFROMINV {item}" },
                    Text = { Text = "Забрать", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 10 }
                }, "GiveSell");
            }

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0.3", AnchorMax = "1 0.5", OffsetMax = "0 0" },
                Button = { Color = "0.105 0.117 0.094 0.9", Command = $"UI_CHANGECATEGORY SELLFROMINV {item}" },
                Text = { Text = $"Вернуть за {price}exp", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 10 }
            }, "GiveSell");

            CuiHelper.AddUi(player, container);
        }

        void ShowNotify(BasePlayer player, string text)
        {
            CuiHelper.DestroyUi(player, Layer + ".Notify");
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "-0.393 1.016", AnchorMax = "0.998 1.07" },
                Image = { Color = HexToCuiColor("#bbc47e", 20) }
            }, Layer, Layer + ".Notify");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = text, Color = HexToCuiColor("#bbc47e", 100), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, Layer + ".Notify");

            CuiHelper.AddUi(player, container);

            timer.In(6f, () => CuiHelper.DestroyUi(player, Layer + ".Notify"));
        }

        void PagerUI(BasePlayer player, string name, int currentPage)
        {
            CuiHelper.DestroyUi(player, "footer");
            var container = new CuiElementContainer();
            List<string> displayedPages = GetDisplayedPages(player, name, currentPage);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.64 0.113" },
                Image = { Color = "0 0 0 0" }
            }, Layer + ".R", "footer");

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
                        Button = { Color = buttonColor, Command = $"UI_CHANGECATEGORY PAGE {name} {page}", Material = "assets/content/ui/uibackgroundblur.mat" },
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
            var db = data[player.userID];
            var wItems = name == "MARKET" ? _config.market.Count() : db.inventory.Count();
            var count = name == "MARKET" ? 15 : 28;
            int totalPages = (int)Math.Ceiling((decimal)wItems / count);

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

        void ShowUIInforamtion(BasePlayer player, string name)
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
                Button = { Color = "0 0 0 0.9", Close = "Information_UI" },
                Text = { Text = string.Empty }
            }, "Information_UI");

            string text = name == "MARKET" ? "<b><size=30>Рынок</size></b>\n\nЗдесь можно купить фрагменты привилегий/предметов из раздела\n''сбор фрагментов'', иногда префиксы перед ником и транспорт. Если\nВам, например, не хватает парочки фрагментов для того чтобы\nсобрать привилегию - Вы всегда можете приобрести недостающие\nфрагменты тут.\n\nПриобременные здесь предметы попадают в раздел ''инвентарь'', где\nбудут хранится пока Вы их не заберёте." : "<b><size=30>Инвентарь</size></b>\n\nВсе найденные наёмниками рецепты и купленные на рынке предметы\nбудут хранится здесь, отсюда вы сможете их забирать в игровой\nинвентарь или продавать за определённое количество EXP.\n\nЭтот инвентарь не вайпается.";
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.3 0.3", AnchorMax = "0.7 0.7", OffsetMax = "0 0" },
                Text = { Text = text, Color = "1 1 1 0.2", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" }
            }, "Information_UI");

            CuiHelper.AddUi(player, container);
        }

        void ShowUIMainly(BasePlayer player, string type)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Menu_Block", Layer);

            float width = 0.2433f, height = 0.08f, startxBox = 0f, startyBox = 1f - height, xmin = startxBox, ymin = startyBox;
            foreach (var check in types)
            {
                string buttonColor = type == check.Key ? HexToCuiColor("#b2a9a3", 100) : "1 1 1 0.04";
                string text = type == check.Key ? $"<b><color=#45403b>{check.Value}</color></b>" : check.Value;
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMax = "0 0" },
                    Button = { Color = buttonColor, Command = $"UI_CHANGECATEGORY {check.Key}", Material = "assets/content/ui/uibackgroundblur.mat" },
                    Text = { Text = text, Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.2", FontSize = 14, Align = TextAnchor.MiddleCenter, }
                }, Layer);
                xmin += width + 0.009f;
            }

            CuiHelper.DestroyUi(player, Layer);
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

        string PlayerEXPQuests(ulong userID)
        {
            return data[userID].xp.ToString("0.0000");
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

        private double CurrentTime() => DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        #endregion
    }
}