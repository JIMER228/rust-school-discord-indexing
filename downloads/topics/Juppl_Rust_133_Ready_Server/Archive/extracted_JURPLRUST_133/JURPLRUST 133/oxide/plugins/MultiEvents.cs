using System;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Collections;
using System.Linq;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("MultiEvents", "", "1.1.2")]

    class MultiEvents : RustPlugin
    {
        #region Fields
        [PluginReference] private readonly Plugin ImageLibrary, Duel;
        private static string Layer = "UI_MultiEvents";
        private double time;
        public Timer Main;
        public Timer DestroyTimer;
        public Timer HelicopterStrafeTarget;
        public Timer HelicopterTarget;
        public BasePlayer winner = null;
        public BasePlayer runner = null;
        public List<string> Winners = new List<string>();
        private bool hasStarted;
        public string nowEvent = null;
        private Dictionary<BasePlayer, float> PlayersTop = new Dictionary<BasePlayer, float>();
        private static MultiEvents instance;
        private static Vector3 EventPosition = new Vector3(-(float)World.Size / 2, 400, -(float)World.Size / 2);
        private static FoundationDrop cEvent = null;
        MonumentInfo beginning_mission = null;
        MonumentInfo end_mission = null;
        public Item ItemForWinner = null;
        #endregion

        #region Config
        private static ConfigData config;
        private class ConfigData
        {
            public class Interface
            {
                [JsonProperty("Цвет фона")]
                public string color;
                [JsonProperty("Offset Min")]
                public string oMin;
                [JsonProperty("Offset Max")]
                public string oMax;
            }
            public class EventSettings
            {
                [JsonProperty("Настройка интерфейса ивента")]
                public Interface ui;
                [JsonProperty("Сколько игроков требуется для начала эвента?")]
                public int MinPlayers;
                [JsonProperty("Время продолжительности эвента (в секундах)")]
                public int TimeDelay;
                [JsonProperty("Ссылка на картинку")]
                public string ImageUrl;
                [JsonProperty("Настройка приза для победителя")]
                public List<Loot> loot;
            }
            public class CollectionResourcesSettings : EventSettings
            {
            }
            public class FoundationDropSettings : EventSettings
            {
                [JsonProperty("Настройка интерфейса с информацией о количестве игроков и блоков")]
                public Interface infoUI;
                [JsonProperty("Размер арены в квадратах")]
                public int ArenaSize;
                [JsonProperty("Интвервал между удалениями блоков")]
                public float DelayDestroy;
                [JsonProperty("Время ожидания игроков с момента объявления ивента")]
                public int WaitTime;
                [JsonProperty("Интенсивность созданой радиации")]
                public float IntensityRadiation;
                [JsonProperty("Отключить стандартную радиацию на РТ (Это нужно в случае если у Вас отключена радиация, плагин включит её обратно но уберёт на РТ)")]
                public bool DisableDefaultRadiation;
                [JsonProperty("Заблокированные команды")]
                public List<string> commands;
            }
            public class Loot
            {
                [JsonProperty("Выдавать предмет?")]
                public bool itemenabled;
                [JsonProperty("Выдавать баланс?")]
                public bool cashenabled;
                [JsonProperty("Настройка предмета")]
                public List<List<Items>> items;
                [JsonProperty("Настройка баланса")]
                public Cash cash;
            }
            public class Items
            {
                [JsonProperty("Название предмета (ShortName)")]
                public string shortname;
                [JsonProperty("Минимальное количество предмета")]
                public int minrate;
                [JsonProperty("Максимальное  количество предмета")]
                public int maxrate;
                [JsonProperty("Это чертеж?")]
                public bool blueprint;
                [JsonProperty("Имя предмета (оставьте пустым для стандартного)")]
                public string displayname;
                [JsonProperty("ID Скина (0 - стандартный)")]
                public ulong skinid;
                [JsonProperty("Целостность предмета в % от 1 до 100 (0 - стандартная)")]
                public int condition;
            }
            public class Cash
            {
                [JsonProperty("Название функции для вызова")]
                public string function;
                [JsonProperty("Название плагина")]
                public string plugin;
                [JsonProperty("Количество денег")]
                public int amount;
            }

            [JsonProperty("Задержка между эвентами")]
            public int Delay;
            [JsonProperty("Эвенты доступные для игроков")]
            public List<string> EnabledEvents;
            [JsonProperty("Право на запуск ивента")]
            public string perm_admin;
            [JsonProperty("Настройка эвента СБОР РЕСУРСОВ")]
            public CollectionResourcesSettings CollectionResources;
            [JsonProperty("Настройка эвента ПАДАЮЩИЕ ПЛАТФОРМЫ")]
            public FoundationDropSettings FoundationDrop;
        }
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                Delay = 1800,
                EnabledEvents = new List<string>
                {
                    "CollectionResources", "FoundationDrop"
                },
                perm_admin = "multievents.admin",
                FoundationDrop = new ConfigData.FoundationDropSettings
                {
                    loot = new List<ConfigData.Loot>
                    {
                        new ConfigData.Loot
                        {
                            itemenabled = true,
                            cashenabled = true,
                            items = new List<List<ConfigData.Items>>
                            {
                                new List<ConfigData.Items>
                                {
                                    new ConfigData.Items
                                    {
                                        shortname = "explosive.satchel",
                                        minrate = 2,
                                        maxrate = 10,
                                        blueprint = false,
                                        displayname = "",
                                        skinid = 0,
                                        condition = 0,
                                    },
                                    new ConfigData.Items
                                    {
                                        shortname = "grenade.beancan",
                                        minrate = 5,
                                        maxrate = 25,
                                        blueprint = false,
                                        displayname = "",
                                        skinid = 0,
                                        condition = 0,
                                    }
                                }
                            },
                            cash = new ConfigData.Cash
                            {
                                function = "Deposit",
                                plugin = "Economics",
                                amount = 400,
                            }
                        },
                    },
                    MinPlayers = 4,
                    TimeDelay = 600,
                    ImageUrl = "https://i.imgur.com/B4DCBs2.png",
                    ArenaSize = 10,
                    DelayDestroy = 2f,
                    WaitTime = 60,
                    IntensityRadiation = 10f,
                    DisableDefaultRadiation = false,
                    ui = new ConfigData.Interface
                    {
                        color = "0.024 0.016 0.17 0.7",
                        oMin = "-225 -305",
                        oMax = "-5 -5"
                    },
                    infoUI = new ConfigData.Interface
                    {
                        color = "0.024 0.016 0.17 0.7",
                        oMin = "-225 -100",
                        oMax = "-5 -5"
                    },
                    commands = new List<string>
                    {
                        "bp",
                        "backpack",
                        "skin",
                        "skinbox",
                        "rec",
                        "tpa",
                        "tpr",
                        "sethome",
                        "home",
                        "kit",
                        "remove"
                    }
                },
                CollectionResources = new ConfigData.CollectionResourcesSettings
                {
                    MinPlayers = 4,
                    TimeDelay = 300,
                    ImageUrl = "https://i.imgur.com/FggJeaH.png",
                    ui = new ConfigData.Interface
                    {
                        color = "0.024 0.016 0.17 0.7",
                        oMin = "-225 -305",
                        oMax = "-5 -5"
                    },
                    loot = new List<ConfigData.Loot>
                    {
                        new ConfigData.Loot
                        {
                            itemenabled = true,
                            cashenabled = true,
                            items = new List<List<ConfigData.Items>>
                            {
                                new List<ConfigData.Items>
                                {
                                    new ConfigData.Items
                                    {
                                        shortname = "stones",
                                        minrate = 5000,
                                        maxrate = 10000,
                                        blueprint = false,
                                        displayname = "",
                                        skinid = 0,
                                        condition = 0,
                                    },
                                    new ConfigData.Items
                                    {
                                        shortname = "wood",
                                        minrate = 5000,
                                        maxrate = 10000,
                                        blueprint = false,
                                        displayname = "",
                                        skinid = 0,
                                        condition = 0,
                                    },
									new ConfigData.Items
                                    {
                                        shortname = "sulfur.ore",
                                        minrate = 500,
                                        maxrate = 5000,
                                        blueprint = false,
                                        displayname = "",
                                        skinid = 0,
                                        condition = 0,
                                    },
                                    new ConfigData.Items
                                    {
                                        shortname = "metal.ore",
                                        minrate = 5000,
                                        maxrate = 10000,
                                        blueprint = false,
                                        displayname = "",
                                        skinid = 0,
                                        condition = 0,
                                    }
                                }
                            },
                            cash = new ConfigData.Cash
                            {
                                function = "Deposit",
                                plugin = "Economics",
                                amount = 400,
                            }
                        },
                    },
                },
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();

                if (config == null)
                    LoadDefaultConfig();
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Configuration file is corrupt(or not exists), creating new one!");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Initialization
        void OnServerInitialized()
        {
            PrintWarning("  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
            PrintWarning($"     {Name} v{Version} loading");
            if (!ImageLibrary)
            {
                PrintError("   Install plugin: 'ImageLibrary'");
                Interface.Oxide.UnloadPlugin(Title);
                return;
            }
            PrintWarning($"        Plugin loaded - OK");
            PrintWarning("  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");

            timer.Every(config.Delay + Random.Range(0, 1800), () =>
            {
                StartEvent(config.EnabledEvents.GetRandom());
            });

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"EVENT.END", "Ивент {0} закончен.\nПобедил {1}."},
                {"EVENT.END.MULTI", "Ивент {0} закончен.\nПобедили {1}."},
                {"EVENT.YOUWINNER", "Вы стали победителем ивента!\nПоздравляем!"},
                {"EVENT.ErrorStarted", "Ивент уже запущен"},
                {"EVENT.Error", "Ивент запрещён к запуску!!! Проверьте правильность введённого запроса"},
                {"EVENT.Start", "Вы запустили ивент"},
                {"EVENT.NotStart", "Ивент не запущен"},
                {"EVENT.Cancel", "Вы отменили ивент"},
                {"EVENT.NotFD", "Вы ошиблись ивентом"},
                {"EVENT.Timer", "ОСТАЛОСЬ {0}"},
                {"WINNER.NOTFOUND", "ПОБЕДИТЕЛЬ НЕ НАЙДЕН"},
                {"CollectionResources", "СБОР РЕСУРСОВ"},
                {"FoundationDrop", "ПАДАЮЩИЕ ПЛАТФОРМЫ"},
                {"GET.RUNNER", "Игрок {0} подобрал ящик с заветным лутом.\n Он бежит на РТ, спешите чтобы забрать у него лут! Он отмечен у тебя на карте."},
                {"RUNNER.BYNPC", "Бегущий игрок был убит NPC\n Новым бегуном назначен {0}"},
                {"RUNNER.PLAYER", "Бегущий игрок {0} был убит\n Новым бегуном назначен {1}"},
                {"RUNNER.FORRUNNER", "Вы получили особый груз! Донесите его до цели {0} ({1})"},
                {"FOUNDROP.JOIN", "Ивент на специальной арене. Поле 10x10, которое будет постепенно обваливаться. Побеждает тот, кто последний останется на поле.\n\nДля участия напишите\n/event join\n<b><color=red>С собой ничего не брать</color></b>"},
                {"FOUNDROP.WaitTime", "<b>ДО НАЧАЛА ИВЕНТА: {0}</b>"},
                {"FOUNDROP.BlockAndPlayers", "<b>Блоков на поле: {0}\nИгроков на поле: {1}</b>"},
                {"FOUNDROP.Started", "<size=16>Мероприятие уже началось, вы <color=#538fef>не успели</color>!</size>"},
                {"FOUNDROP.Connected", "<size=16>Вы уже участник мероприятия!</size>" },
                {"FOUNDROP.Naked", "<size=16>На мероприятие можно попасть только <color=#FF9494><b>ПОЛНОСТЬЮ ГОЛЫМ</b></color></size>" },
                {"UI.Random", "<b>РАНДОМ</b>"},
                {"UI.Cancel", "<b>ОТМЕНИТЬ</b>"},
                {"UI.Start", "<b>ЗАПУСТИТЬ</b>"},
                {"UI.Back", "<b>НАЗАД</b>"},
                {"UI.Next", "<b>ДАЛЕЕ</b>"},
                {"UI.Name", "<b>ИВЕНТЫ</b>"},
                {"UI.CollectionResources", "<b>СБОР\nРЕСУРСОВ</b>"},
                {"UI.FoundationDrop", "<b>ПАДАЮЩИЕ\nПЛАТФОРМЫ</b>"},
                {"UI.Description.CollectionResources", "<b>Ваша задача собрать больше ресурсов, чем другие игроки.\nЗасчитывается стучание по камням с рудой, подбор руды с земли, срывание кустов ткани, разделывание трупов животных и людей и подбор грибов.\nСумма очков игрока равно общему количеству добытых реусрсов.\n\nВремя: 5 мин\nНаграда: рандомный предмет</b>"},
                {"UI.Description.FoundationDrop", "<b>Ивент на специальной арене.\nВсе игроки появляются на поле 10x10 из фундаментов.\nКаждые 5 секунд один из фундаментов будет проваливаться и так до того момента, пока не останется только один фундамент.\nЕсли к концу на поле осталось несколько игроков, они получают меч и на них начинает действовать радиация.\nПобеждает тот, кто последний остался на поле.\n\nВремя: ~10 минут.\nНаграда: рандомный предмет</b>"},
            }, this);

            permission.RegisterPermission(config.perm_admin, this);

            ImageLibrary.Call("AddImage", config.CollectionResources.ImageUrl, "CollectionResources");
            ImageLibrary.Call("AddImage", config.FoundationDrop.ImageUrl, "FoundationDrop");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/Okt1BMH.png", "ME_Background_image");
            ImageLibrary.Call("AddImage", "https://i.imgur.com/DpLldC8.png", "ME_Logo_image");

            Unsubscribe(nameof(OnCollectiblePickup));
            Unsubscribe(nameof(OnCropGather));
            Unsubscribe(nameof(OnDispenserBonus));
            Unsubscribe(nameof(OnDispenserGather));
            Unsubscribe(nameof(OnEntityDeath));
            Unsubscribe(nameof(OnPlayerDie));
            Unsubscribe(nameof(OnEntityKill));
            Unsubscribe(nameof(ClearTeleport));
            Unsubscribe(nameof(FoundationDrop));
            Unsubscribe(nameof(InitializeFoundation));
            Unsubscribe(nameof(DropFoundation));
            Unsubscribe(nameof(Downgrader));
            Unsubscribe(nameof(HeliPet));
            Unsubscribe(nameof(OnPlayerCommand));
            instance = this;
        }
        void Unload()
        {
            if (hasStarted)
            {
                try
                {
                    DestroyEvent(nowEvent);
                    NextTick(() =>
                    {
                        PlayersTop.Clear();
                        for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
                        {
                            var player = BasePlayer.activePlayerList[i];

                            CuiHelper.DestroyUi(player, Layer);
                            CuiHelper.DestroyUi(player, Layer + ".Notification");
                            CuiHelper.DestroyUi(player, Layer + ".FoundationDrop.Play");
                        }
                    });
                }
                catch (NullReferenceException)
                {
                    if (PlayersTop.Count > 1)
                        PlayersTop.Clear();
                    for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
                    {
                        var player = BasePlayer.activePlayerList[i];

                        CuiHelper.DestroyUi(player, Layer);
                        CuiHelper.DestroyUi(player, Layer + ".Notification");
                        CuiHelper.DestroyUi(player, Layer + ".FoundationDrop.Play");
                    }
                    UnityEngine.Object.FindObjectsOfType<BaseEntity>().Where(p => p.OwnerID == 98596 && !p.isDestroyed).ToList().ForEach(s => s?.Kill());
                    UnityEngine.Object.FindObjectsOfType<Downgrader>().ToList().ForEach(s => s?.Kill());
                    if (RadiationZones.ContainsKey(cEvent.BlockList.SelectMany(p => p).Last().GetInstanceID()))
                    {
                        UnityEngine.Object.Destroy(RadiationZones[cEvent.BlockList.SelectMany(p => p).Last().GetInstanceID()].zone);
                        RadiationZones.Remove(cEvent.BlockList.SelectMany(p => p).Last().GetInstanceID());
                    }
                }
            }
        }
        #endregion

        #region Functions
        void StartEvent(string type, BasePlayer target = null)
        {
            if (hasStarted && !string.IsNullOrEmpty(nowEvent)) DestroyEvent(nowEvent);
            nowEvent = type;
            Main?.Destroy();
            DestroyTimer?.Destroy();
            switch (type)
            {
                case "CollectionResources":
                    {
                        if (BasePlayer.activePlayerList.Count < config.CollectionResources.MinPlayers && target == null) return;
                        Subscribe(nameof(OnCollectiblePickup));
                        Subscribe(nameof(OnCropGather));
                        Subscribe(nameof(OnDispenserBonus));
                        Subscribe(nameof(OnDispenserGather));

                        for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
                            TopUI(BasePlayer.activePlayerList[i], "all", GetMessage(type, BasePlayer.activePlayerList[i].UserIDString), oMin: config.CollectionResources.ui.oMin, oMax: config.CollectionResources.ui.oMax, color: config.CollectionResources.ui.color);

                        time = config.CollectionResources.TimeDelay + GrabCurrentTime();

                        Main = timer.Repeat(1, (int)(time - GrabCurrentTime()), () =>
                        {
                            var list = PlayersTop.OrderByDescending(p => p.Value).Take(8).ToList();
                            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
                                TopUI(BasePlayer.activePlayerList[i], "refresh", list: list);
                        });

                        DestroyTimer = timer.Once(config.CollectionResources.TimeDelay, () =>
                        {
                            DestroyEvent(nowEvent);
                        });
                    }
                    break;

                case "FoundationDrop":
                    {
                        if (BasePlayer.activePlayerList.Count < config.FoundationDrop.MinPlayers && target == null) return;
                        Subscribe(nameof(ClearTeleport));
                        Subscribe(nameof(FoundationDrop));
                        Subscribe(nameof(InitializeFoundation));
                        Subscribe(nameof(DropFoundation));
                        Subscribe(nameof(Downgrader));
                        Subscribe(nameof(OnPlayerCommand));

                        time = config.FoundationDrop.WaitTime + GrabCurrentTime();
                        cEvent = new FoundationDrop
                        {
                            Started = false,
                            StartTime = time
                        };
                        ServerMgr.Instance.StartCoroutine(InitializeFoundation(config.FoundationDrop.WaitTime));
                    }
                    break;
                default:
                    break;
            }
            hasStarted = true;
        }
        void DestroyEvent(string type)
        {
            Main?.Destroy();
            DestroyTimer?.Destroy();
            var messageKEY = string.Empty;
            var winns = string.Empty;
            switch (type)
            {
                case "CollectionResources":
                    Unsubscribe(nameof(OnCollectiblePickup));
                    Unsubscribe(nameof(OnCropGather));
                    Unsubscribe(nameof(OnDispenserBonus));
                    Unsubscribe(nameof(OnDispenserGather));

                    if (PlayersTop.Count != 0)
                    {
                        var list = PlayersTop.OrderByDescending(p => p.Value).Take(config.CollectionResources.loot.Count).ToList();
                        for (int i = 0; i < list.Count; i++)
                        {
                            var winner = list[i];
                            DropItem(winner.Key, nowEvent, i);
                            Winners.Add(winner.Key.displayName);
                        }
                    }
                    break;

                case "FoundationDrop":
                    cEvent.FinishEvent();
                    cEvent = null;

                    Unsubscribe(nameof(ClearTeleport));
                    Unsubscribe(nameof(FoundationDrop));
                    Unsubscribe(nameof(InitializeFoundation));
                    Unsubscribe(nameof(DropFoundation));
                    Unsubscribe(nameof(Downgrader));
                    Unsubscribe(nameof(OnPlayerCommand));
                    break;
            }

            if (PlayersTop.Count == 0 && winner == null)
            {
                for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
                {
                    var player = BasePlayer.activePlayerList[i];
                    CuiHelper.DestroyUi(player, Layer);
                    UI_Notification(player, GetMessage("WINNER.NOTFOUND", player.UserIDString));
                }
                timer.Once(4, () =>
                {
                    for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
                        CuiHelper.DestroyUi(BasePlayer.activePlayerList[i], Layer + ".Notification");
                });
            }
            else
            {
                messageKEY = Winners.Count > 1 ? "EVENT.END.MULTI" : "EVENT.END";
                winns = Winners.Count > 1 ? string.Join(", ", Winners.ToArray()) : Winners.First();

                for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
                {
                    var player = BasePlayer.activePlayerList[i];
                    CuiHelper.DestroyUi(player, Layer);
                    CuiHelper.DestroyUi(player, Layer + ".FoundationDrop.Play");

                    UI_Notification(player, winner == player ? GetMessage("EVENT.YOUWINNER", player.UserIDString) : GetMessage(messageKEY, player.UserIDString, GetMessage(type, player.UserIDString), winns));
                }
                timer.Once(4, () =>
                {
                    PlayersTop.Clear();

                    for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
                        CuiHelper.DestroyUi(BasePlayer.activePlayerList[i], Layer + ".Notification");
                });
            }
            NextTick(() =>
            {
                if (type != "FoundationDrop")
                {
                    winner = null;
                    nowEvent = null;
                    hasStarted = false;
                }
                else
                {
                    if (winner == null)
                    {
                        nowEvent = null;
                        hasStarted = false;
                    }
                }
                runner = null;
                beginning_mission = null;
                end_mission = null;
                winns = string.Empty;
                PlayersTop.Clear();
                Winners.Clear();
                messageKEY = "EVENT.END";
                HelicopterStrafeTarget?.Destroy();
                HelicopterTarget?.Destroy();
            });
        }
        private void DropItem(BasePlayer player, string type, int temp = 0)
        {
            ConfigData.Items item = null;
            ConfigData.Cash cash = null;

            switch (type)
            {
                case "CollectionResources":
                    if (config.CollectionResources.loot[temp].itemenabled)
                    {
                        for (int i = 0; i < config.CollectionResources.loot[temp].items.Count; i++)
                        {
                            item = config.CollectionResources.loot[temp].items[i].GetRandom();
                        }
                    }
                    if (config.CollectionResources.loot[temp].cashenabled)
                        cash = config.CollectionResources.loot[temp].cash;
                    break;
                case "FoundationDrop":
                    if (config.FoundationDrop.loot[temp].itemenabled)
                    {
                        for (int i = 0; i < config.FoundationDrop.loot[temp].items.Count; i++)
                        {
                            item = config.FoundationDrop.loot[temp].items[i].GetRandom();
                        }
                    }
                    if (config.FoundationDrop.loot[temp].cashenabled)
                        cash = config.FoundationDrop.loot[temp].cash;
                    break;
                default:
                    break;
            }
            if (item != null)
            {
                int amount = Random.Range(item.minrate, item.maxrate);
                var newItem = item.blueprint ? ItemManager.CreateByName("blueprintbase") : ItemManager.CreateByName(item.shortname, amount);

                if (newItem == null)
                {
                    PrintError($"Item {item.shortname} not founbd!");
                    return;
                }
                if (item.blueprint)
                {
                    var bpItemDef = ItemManager.FindItemDefinition(item.shortname);

                    if (bpItemDef == null)
                    {
                        PrintError($"Item {item.shortname} to create a blueprint is not found!");
                        return;
                    }

                    newItem.blueprintTarget = bpItemDef.itemid;
                }
                if (!item.blueprint && item.condition != 0)
                    newItem.condition = newItem.info.condition.max / 100 * newItem.uid;
                if (item.displayname != "")
                    newItem.name = item.displayname;
                if (type == "FoundationDrop")
                {
                    ItemForWinner = newItem;
                    return;
                }
                else
                    player.GiveItem(newItem, BaseEntity.GiveItemReason.PickedUp);
            }
            if (cash != null)
            {
                var plugin = plugins.Find(cash.plugin);
                if (plugin == null)
                {
                    PrintError("ECONOMIC PLUGIN NOT FOUND");
                    return;
                }
                if (cash.plugin == "RustStore")
                {
                    plugin?.Call(cash.function, player.userID, cash.amount, new Action<string>((result) =>
                    {
                        if (result == "SUCCESS")
                        {
                            Interface.Oxide.LogDebug($"Игрок {player.displayName} получил {cash.amount} на баланс в магазине");
                            return;
                        }
                        Interface.Oxide.LogDebug($"Баланс не был изменен, ошибка: {result}");
                    }));
                }
                else if (cash.plugin == "GameStoresRUST")
                {
                    var args = cash.function.Split(' ');
                    webrequest.EnqueueGet($"https://gamestores.ru/api/?shop_id={args[0]}&secret={args[1]}&server={args[2]}&action=moneys&type=plus&steam_id={player.UserIDString}&amount={cash.amount}", (code, response) =>
                    {
                        switch (code)
                        {
                            case 0:
                                {
                                    PrintError("Api does not responded to a request");
                                    break;
                                }
                            case 200:
                                {
                                    PrintToConsole($"{player.displayName} wins {cash.amount} in award.");
                                    break;
                                }
                            case 404:
                                {
                                    PrintError($"Plese check your configuration! [404]");
                                    break;
                                }
                        }
                    }, this);
                }
                else if (cash.plugin == "Economics")
                    plugin?.Call(cash.function, player.userID, (double)cash.amount);
                else
                    plugin?.Call(cash.function, player.userID, cash.amount);
            }
        }
        double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
        void AddToDictionary(BasePlayer player, float amount)
        {
            if (!PlayersTop.ContainsKey(player))
                PlayersTop.Add(player, amount);
            else
                PlayersTop[player] += amount;
        }
        #endregion

        #region Hooks
        object OnPlayerCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Player() == null || !arg.HasArgs(0) || cEvent == null || !cEvent.PlayerConnected.ContainsKey(arg.Player().userID)) return null;
            if (config.FoundationDrop.commands.Contains(arg.Args[0].Replace("/", "")))
            {
                SendReply(arg.Player(), "Command is blocked");
                return false;
            }
            return null;
        }
        void OnEntityKill(BaseNetworkable entity)
        {
            if (!hasStarted || entity == null || entity as BaseHelicopter == null || (entity as BaseHelicopter).OwnerID != 999999999) return;
            DestroyEvent(nowEvent);
        }
        void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null || nowEvent != "FoundationDrop" || !hasStarted || player != winner || ItemForWinner == null) return;
            player.GiveItem(ItemForWinner, BaseEntity.GiveItemReason.PickedUp);
            nowEvent = null;
            hasStarted = false;
            ItemForWinner = null;
        }
        void OnPlayerDie(BasePlayer player, HitInfo info)
        {
            if (player == null || player != runner || info == null || info.InitiatorPlayer == null) return;

            if (info.InitiatorPlayer == player)
            {
                runner = BasePlayer.activePlayerList.GetRandom();
                for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
                    UI_Notification(BasePlayer.activePlayerList[i], GetMessage("RUNNER.BYNPC", BasePlayer.activePlayerList[i].UserIDString, runner.displayName));
            }
            else
            {
                runner = info.InitiatorPlayer;
                //UI_Notification(runner, GetMessage("RUNNER.FORRUNNER", runner.UserIDString, end_mission.displayPhrase.english, GetGridString(end_mission.transform.position)));
                for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
                {
                    if (BasePlayer.activePlayerList[i] != runner)
                        UI_Notification(BasePlayer.activePlayerList[i], GetMessage("RUNNER.PLAYER", BasePlayer.activePlayerList[i].UserIDString, player.displayName, runner.displayName));
                }
            }
            TopUI(runner, nowEvent);

        }
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null || info.InitiatorPlayer == null) return;

            switch (nowEvent)
            {
            }
        }
        object OnCollectiblePickup(Item item, BasePlayer player)
        {
            if (item == null || player == null)
                return null;

            AddToDictionary(player, item.amount);
            return null;
        }
        object OnCropGather(PlantEntity plant, Item item, BasePlayer player)
        {
            if (plant == null || item == null || player == null)
                return null;

            AddToDictionary(player, item.amount);
            return null;
        }
        object OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser == null || item == null || player == null)
                return null;

            AddToDictionary(player, item.amount);
            return null;
        }
        object OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (dispenser == null || entity == null || item == null || entity.ToPlayer() == null)
                return null;

            AddToDictionary(entity.ToPlayer(), item.amount);
            return null;
        }
        #endregion

        #region Interface
        void TopUI(BasePlayer player, string Type, string TopName = "", string description = "", string oMin = "", string oMax = "", string color = "", List<KeyValuePair<BasePlayer, float>> list = null)
        {
            var container = new CuiElementContainer();
            int position = 35;

            switch (Type)
            {
                case "all":

                    CuiHelper.DestroyUi(player, Layer);

                    container.Add(new CuiPanel
                    {
                        CursorEnabled = false,
                        RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = oMin, OffsetMax = oMax },
                        Image = { Color = color }
                    }, "Hud", Layer);


                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -25", OffsetMax = "0 -5" },
                        Text = { Text = $"<b>{TopName}</b>", FontSize = 16, Align = TextAnchor.MiddleCenter }
                    }, Layer);
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "10 -28", OffsetMax = "-10 -27" },
                        Image = { Color = "1 1 1 1" }
                    }, Layer, Layer + ".Line");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "24 0", OffsetMax = "0 0" },
                        Text = { Text = !string.IsNullOrEmpty(description) ? description : "", FontSize = 14, Align = TextAnchor.MiddleLeft }
                    }, Layer, Layer + ".Text");

                    CuiHelper.DestroyUi(player, Layer);
                    break;
                case "refresh":
                    CuiHelper.DestroyUi(player, Layer + ".Timer");
                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 20" },
                        Text = { Text = GetMessage("EVENT.Timer", player.UserIDString, string.Format("{0:0}", time - GrabCurrentTime())), FontSize = 12, Align = TextAnchor.MiddleCenter }
                    }, Layer, Layer + ".Timer");
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "10 0", OffsetMax = "-10 1" },
                        Image = { Color = "1 1 1 1" }
                    }, Layer + ".Timer", Layer + ".Timer.Line");

                    if (list != null)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            var check = list[i];

                            CuiHelper.DestroyUi(player, Layer + $".TopLabel.{i}");

                            container.Add(new CuiPanel
                            {
                                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"20 -{position + 20}", OffsetMax = $"-20 -{position}" },
                                Image = { Color = "0 0 0 0" }
                            }, Layer, Layer + $".TopLabel.{i}");

                            container.Add(new CuiLabel
                            {
                                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "24 0", OffsetMax = "0 0" },
                                Text = { Text = $"<b>{check.Key.displayName}</b>", FontSize = 12, Align = TextAnchor.MiddleLeft }
                            }, Layer + $".TopLabel.{i}", Layer + $".TopLabel.{i}.Text");

                            container.Add(new CuiElement
                            {
                                Name = Layer + $".TopLabel.{i}.Avatar",
                                Parent = Layer + $".TopLabel.{i}",
                                Components =
                                {
                                    new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", check.Key.UserIDString),  Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMax = "20 0" }
                                }
                            });
                            container.Add(new CuiLabel
                            {
                                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                                Text = { Text = nowEvent == "KingMountain" ? $"{check.Value.ToString("0.0")} м" : nowEvent == "CollectionResources" ? $"{check.Value} шт" : $"{check.Value}", FontSize = 12, Align = TextAnchor.MiddleRight }
                            }, Layer + $".TopLabel.{i}", Layer + $".TopLabel.{i}.Height");
                            position += 30;
                        }
                    }
                    break;
                case "FoundationDrop_Timer":
                    CuiHelper.DestroyUi(player, Layer + ".Timer");
                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 20" },
                        Text = { Text = GetMessage("FOUNDROP.WaitTime", player.UserIDString, string.Format("{0:0}", time - GrabCurrentTime())), FontSize = 12, Align = TextAnchor.MiddleCenter }
                    }, Layer, Layer + ".Timer");
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "10 0", OffsetMax = "-10 1" },
                        Image = { Color = "1 1 1 1" }
                    }, Layer + ".Timer", Layer + ".Timer.Line");
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "10 0", OffsetMax = "-10 1" },
                        Image = { Color = "1 1 1 1" }
                    }, Layer + ".Timer", Layer + ".Timer.Line");
                    break;
                case "FoundationDrop.Upd":
                    CuiHelper.DestroyUi(player, Layer + ".Text");
                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "10 0", OffsetMax = "-10 -40" },
                        Text = { Text = GetMessage("FOUNDROP.BlockAndPlayers", player.UserIDString, cEvent.BlockList.SelectMany(p => p).Count().ToString(), cEvent.PlayerConnected.Count().ToString()), FontSize = 18, Align = TextAnchor.UpperLeft }
                    }, Layer, Layer + ".Text");
                    break;

            }
            CuiHelper.AddUi(player, container);
        }
        private void UI_Notification(BasePlayer player, string message, string Name = ".Notification", string color = "0.98 0.37 0.41 0.69")
        {
            CuiHelper.DestroyUi(player, Layer + Name);
            CuiHelper.AddUi(player, new CuiElementContainer
            {
                {
                    new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -100", OffsetMax = "0 -50" },
                        Button = { Color = color },
                        Text = { FadeIn = 1f, Color = "1 1 1 1", FontSize = 18, Align = TextAnchor.MiddleCenter, Text = $"{message}" }
                    },
                    "Overlay",
                    Layer + Name
                }
            });
        }
        void Help_UI(BasePlayer player, int page)
        {
            var container = new CuiElementContainer();
            if (config.EnabledEvents.Count <= page || page < 0)
                page = 0;
            var list = config.EnabledEvents[page];
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-450 -255", OffsetMax = "450 255" },
                Image = { Color = "0.024 0.017 0.17 0.76" }
            }, "Overlay", Layer + ".Help");
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "10 10", AnchorMax = "-10 -10" },
                Text = { Text = "" },
                Button = { Color = "0 0 0 0", Close = Layer + ".Help" }
            }, Layer + ".Help");

            CuiHelper.DestroyUi(player, Layer + ".Help.Pages.Back");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "40 10", OffsetMax = "120 35" },
                Button = { Color = "0 0 0 0", Command = $"event page {page - 1}" },
                Text = { Text = GetMessage("UI.Back", player.UserIDString), FontSize = 20, Align = TextAnchor.LowerLeft, Font = "robotocondensed-bold.ttf" }
            }, Layer + ".Help", Layer + ".Help.Pages.Back");

            CuiHelper.DestroyUi(player, Layer + ".Help.Pages.Next");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-120 10", OffsetMax = "-40 35" },
                Button = { Color = "0 0 0 0", Command = $"event page {page + 1}" },
                Text = { Text = GetMessage("UI.Next", player.UserIDString), FontSize = 20, Align = TextAnchor.LowerRight, Font = "robotocondensed-bold.ttf" }
            }, Layer + ".Help", Layer + ".Help.Pages.Next");

            CuiHelper.DestroyUi(player, Layer + ".Help.Logo");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "40 -65", OffsetMax = "-40 0" },
                Text = { Text = GetMessage("UI.Name", player.UserIDString), FontSize = 38, Align = TextAnchor.MiddleCenter }
            }, Layer + ".Help", Layer + ".Help.Logo");

            CuiHelper.DestroyUi(player, Layer + ".Help.Logo.Line");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMax = "0 1" },
                Image = { Color = "1 1 1 1" }
            }, Layer + ".Help.Logo", Layer + ".Help.Logo.Line");

            CuiHelper.DestroyUi(player, Layer + ".Help.Logo.Image");

            container.Add(new CuiElement
            {
                Name = Layer + ".Help.Logo.Image",
                Parent = Layer + ".Help.Logo",
                Components =
                {
                    new CuiImageComponent { Png = (string) ImageLibrary.Call("GetImage", "ME_Logo_image"), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-142 -26", OffsetMax = "-90 26" }
                }
            });

            CuiHelper.DestroyUi(player, Layer + ".Help.Event");

            container.Add(new CuiElement
            {
                Name = Layer + ".Help.Event",
                Parent = Layer + ".Help",
                Components =
                {
                    new CuiImageComponent { Png = (string) ImageLibrary.Call("GetImage", "ME_Background_image"), Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-328 -110", OffsetMax = "-90 140" }
                }
            });

            CuiHelper.DestroyUi(player, Layer + ".Help.Event.Name");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 -186" },
                Text = { Text = GetMessage("UI." + list, player.UserIDString), Color = "0 0 0 1", FontSize = 26, Align = TextAnchor.MiddleCenter }
            }, Layer + ".Help.Event", Layer + ".Help.Event.Name");

            CuiHelper.DestroyUi(player, Layer + ".Help.Event.Image");

            container.Add(new CuiElement
            {
                Name = Layer + ".Help.Event.Image",
                Parent = Layer + ".Help.Event",
                Components =
                {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", list),  Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "13 63", OffsetMax = "-13 -15" }
                }
            });

            CuiHelper.DestroyUi(player, Layer + ".Help.Logo");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-45 -200", OffsetMax = "370 170" },
                Text = { Text = GetMessage("UI.Description." + list, player.UserIDString), FontSize = 22, Align = TextAnchor.UpperLeft }
            }, Layer + ".Help", Layer + ".Help.Logo");

            if (player.IsAdmin() || permission.UserHasPermission(player.UserIDString, config.perm_admin))
            {
                CuiHelper.DestroyUi(player, Layer + ".Help.Cmd.Random");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-50 10", OffsetMax = "50 35" },
                    Button = { Color = "0 0 0 0", Command = "event start" },
                    Text = { Text = GetMessage("UI.Random", player.UserIDString), FontSize = 20, Align = TextAnchor.LowerCenter, Color = "0.48 0.41 0.9 1", Font = "robotocondensed-bold.ttf" }
                }, Layer + ".Help", Layer + ".Help.Cmd.Random");

                CuiHelper.DestroyUi(player, Layer + ".Help.Cmd.Cancel");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-170 10", OffsetMax = "-60 35" },
                    Button = { Color = "0 0 0 0", Command = "event cancel" },
                    Text = { Text = GetMessage("UI.Cancel", player.UserIDString), FontSize = 16, Align = TextAnchor.LowerCenter, Color = "1 0.01 0.24 1", Font = "robotocondensed-bold.ttf" }
                }, Layer + ".Help", Layer + ".Help.Cmd.Cancel");

                CuiHelper.DestroyUi(player, Layer + ".Help.Cmd.Start");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "60 10", OffsetMax = "170 35" },
                    Button = { Color = "0 0 0 0", Command = $"event start {list}" },
                    Text = { Text = GetMessage("UI.Start", player.UserIDString), FontSize = 16, Align = TextAnchor.LowerCenter, Color = "0 0.6 0 1", Font = "robotocondensed-bold.ttf" }
                }, Layer + ".Help", Layer + ".Help.Cmd.Start");
            }

            CuiHelper.DestroyUi(player, Layer + ".Help");
            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Commands
        [ChatCommand("event")]
        void cmdEvent(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (args.Length == 0)
            {
                Help_UI(player, 0);
                return;
            }

            switch (args[0].ToLower())
            {
                case "start":
                    {
                        if (player.IsAdmin() || permission.UserHasPermission(player.UserIDString, config.perm_admin))
                        {
                            if (hasStarted)
                            {
                                SendReply(player, GetMessage("EVENT.ErrorStarted", player.UserIDString));
                                return;
                            }
                            else if (args.Length >= 2 && !config.EnabledEvents.Contains(args[1]))
                            {
                                SendReply(player, GetMessage("EVENT.Error", player.UserIDString));
                                return;
                            }

                            SendReply(player, GetMessage("EVENT.Start", player.UserIDString));

                            if (args.Length >= 2)
                                StartEvent(args[1], player);
                            else
                                StartEvent(config.EnabledEvents.GetRandom(), player);
                        }
                        return;
                    }
                case "cancel":
                    {
                        if (player.IsAdmin() || permission.UserHasPermission(player.UserIDString, config.perm_admin))
                        {
                            if (!hasStarted)
                            {
                                SendReply(player, GetMessage("EVENT.NotStart", player.UserIDString));
                                return;
                            }
                            if (hasStarted)
                            {
                                SendReply(player, GetMessage("EVENT.Cancel", player.UserIDString));
                                DestroyEvent(nowEvent);
                                return;
                            }
                        }
                        return;
                    }
                case "join":
                    {
                        if (!hasStarted)
                        {
                            SendReply(player, GetMessage("EVENT.NotStart", player.UserIDString));
                            return;
                        }
                        if (nowEvent != "FoundationDrop")
                        {
                            SendReply(player, GetMessage("EVENT.NotFD", player.UserIDString));
                            return;
                        }
                        cEvent?.JoinEvent(player);
                    }
                    return;
                default:
                    break;
            }
        }
        [ConsoleCommand("event")]
        private void CmdConsole(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player == null) return;
            if (!args.HasArgs(1))
            {
                player.SendConsoleCommand("chat.say /event");
                return;
            }

            switch (args.Args[0].ToLower())
            {
                case "page":
                    {
                        int page = 0;
                        if (!args.HasArgs(2) || !int.TryParse(args.Args[1], out page)) return;
                        Help_UI(player, page);
                    }
                    break;
                case "start":
                    {
                        if (player.IsAdmin() || permission.UserHasPermission(player.UserIDString, config.perm_admin))
                        {
                            if (hasStarted)
                            {
                                SendReply(player, GetMessage("EVENT.ErrorStarted", player.UserIDString));
                                return;
                            }
                            else if (!args.HasArgs(2))
                            {
                                StartEvent(config.EnabledEvents.GetRandom(), player);
                                return;
                            }
                            else if (args.Args.Length == 2)
                            {
                                if (!config.EnabledEvents.Contains(args.Args[1]))
                                {
                                    SendReply(player, GetMessage("EVENT.Error", player.UserIDString));
                                    return;
                                }
                                StartEvent(args.Args[1], player);
                            }
                            SendReply(player, GetMessage("EVENT.Start", player.UserIDString));
                        }
                        break;
                    }
                case "cancel":
                    {
                        if (player.IsAdmin() || permission.UserHasPermission(player.UserIDString, config.perm_admin))
                        {
                            if (!hasStarted)
                            {
                                SendReply(player, GetMessage("EVENT.NotStart", player.UserIDString));
                                return;
                            }
                            if (hasStarted)
                            {
                                DestroyEvent(nowEvent);
                                return;
                            }
                        }
                        break;
                    }
            }
            return;
        }
        #endregion

        #region Localization

        private string GetMessage(string messageKey, string playerID, params object[] args)
        {
            return string.Format(lang.GetMessage(messageKey, this, playerID), args);
        }
        #endregion

        #region Script
        private string GetGridString(Vector3 position)
        {
            Vector2 adjPosition = new Vector2((World.Size / 2) + position.x, (World.Size / 2) - position.z);
            return string.Format($"{NumberToString((int)(adjPosition.x / 150))}{(int)(adjPosition.y / 150)}");
        }

        private string NumberToString(int number)
        {
            bool a = number > 26;
            char c = (char)(65 + (a ? number - 26 : number));
            return a ? "A" + c : c.ToString();
        }
        private void StartSleeping(BasePlayer player)
        {
            if (player.IsSleeping())
                return;
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
            if (!BasePlayer.sleepingPlayerList.Contains(player))
                BasePlayer.sleepingPlayerList.Add(player);
            player.CancelInvoke("InventoryUpdate");
            //player.inventory.crafting.CancelAll(true);
            //player.UpdatePlayerCollider(true, false);
        }
        private static void ClearTeleport(BasePlayer player, Vector3 position)
        {
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "StartLoading");
            instance.StartSleeping(player);
            player.MovePosition(position);
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "ForcePositionTo", position);
            if (player.net?.connection != null)
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.UpdateNetworkGroup();
            //player.UpdatePlayerCollider(true, false);
            player.SendNetworkUpdateImmediate(false);
            if (player.net?.connection == null) return;
            //TODO temporary for potential rust bug
            try
            {
                player.ClearEntityQueue(null);
            }
            catch
            {
            }
            player.SendFullSnapshot();
        }
        private class FoundationDrop
        {
            public double StartTime;
            public bool Started = false;
            public bool Finished = false;
            public bool Given = false;
            public int Received = 0;

            public Timer StartTimer;
            public Timer DestroyTimer;

            public Dictionary<ulong, Vector3> PlayerConnected = new Dictionary<ulong, Vector3>();
            public List<List<BaseEntity>> BlockList = new List<List<BaseEntity>>();

            public void JoinEvent(BasePlayer player)
            {
                if (Started)
                {
                    instance.SendReply(player, instance.GetMessage("FOUNDROP.Started", player.UserIDString));
                    return;
                }
                else
                {
                    if (PlayerConnected.ContainsKey(player.userID))
                    {
                        instance.SendReply(player, instance.GetMessage("FOUNDROP.Connected", player.UserIDString));
                        return;
                    }
                    if (player.inventory.AllItems().Length != 0)
                    {
                        instance.SendReply(player, instance.GetMessage("FOUNDROP.Naked", player.UserIDString));
                        return;
                    }

                    player.inventory.Strip();
                    //player.SendNetworkUpdate();

                    PlayerConnected.Add(player.userID, player.transform.position);
                    ClearTeleport(player, EventPosition + new Vector3(0, 5, 0));
                    player.health = 100;
                    player.metabolism.hydration.value = 250;
                    player.metabolism.calories.value = 500;
                }
            }

            public void LeftEvent(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, Layer);
                player.inventory.Strip();
                player.Die();

                PlayerConnected.Remove(player.userID);
            }

            public void HandlePlayers()
            {
                if (Finished) return;
                if (PlayerConnected.Count == 1)
                {
                    instance.winner = BasePlayer.FindByID(PlayerConnected.Keys.FirstOrDefault());
                    instance.DropItem(instance.winner, instance.nowEvent);
                    instance.DestroyEvent(instance.nowEvent);
                    return;
                }
                else if (PlayerConnected.Count > 1 && BlockList.SelectMany(p => p).Count() == 1)
                {
                    var lastblock = BlockList.SelectMany(p => p).Last();
                    lastblock.gameObject.GetComponent<Downgrader>()?.Kill();
                    if (Given == false)
                        for (int i = 0; i < PlayerConnected.Keys.ToList().Count; i++)
                        {
                            var target = BasePlayer.FindByID(PlayerConnected.Keys.ToList()[i]);
                            target.GiveItem(ItemManager.CreateByName("salvaged.sword"), BaseEntity.GiveItemReason.PickedUp);
                            if (!Given) Given = true;
                        }
                    if (!instance.RadiationZones.ContainsKey(lastblock.GetInstanceID()))
                    {
                        instance.InitializeZone(lastblock.transform.position, config.FoundationDrop.IntensityRadiation, lastblock.GetInstanceID());
                    }
                }
                for (int i = 0; i < PlayerConnected.Count; i++)
                {
                    var check = PlayerConnected.Keys.ToList()[i];
                    var target = BasePlayer.FindByID(check);
                    if (target == null)
                    {
                        PlayerConnected.Remove(check);
                    }
                    else
                    {
                        if (Vector3.Distance(EventPosition, target.transform.position) > (config.FoundationDrop.ArenaSize * 5))
                        {
                            LeftEvent(target);
                        }
                        instance.TopUI(target, "FoundationDrop.Upd");
                    }
                }
                StartTimer = instance.timer.Once(1, HandlePlayers);
            }

            public void StartEvent(int startDelay)
            {
                StartTimer = instance.timer.Once(startDelay, () =>
                {
                    foreach (var check in BasePlayer.activePlayerList)
                    {
                        if (!PlayerConnected.ContainsKey(check.userID))
                            CuiHelper.DestroyUi(check, Layer);
                    }

                    BasePlayer.activePlayerList.ForEach(p => CuiHelper.DestroyUi(p, Layer));
                    for (int i = 0; i < PlayerConnected.Count; i++)
                    {
                        var player = BasePlayer.FindByID(PlayerConnected.Keys.ToList()[i]);
                        instance.TopUI(player, "all", instance.GetMessage(instance.nowEvent, player.UserIDString), oMin: config.FoundationDrop.infoUI.oMin, oMax: config.FoundationDrop.infoUI.oMax, color: config.FoundationDrop.infoUI.color);
                    }
                    List<ulong> removeKeys = new List<ulong>();
                    foreach (var check in PlayerConnected)
                    {
                        BasePlayer target = BasePlayer.FindByID(check.Key);
                        if (target != null)
                            continue;
                        else
                            removeKeys.Add(check.Key);
                    }

                    foreach (var check in removeKeys)
                        PlayerConnected.Remove(check);

                    if (PlayerConnected.Count <= 1)
                    {
                        instance.DestroyEvent(instance.nowEvent);
                        return;
                    }

                    instance.DropFoundation();
                    HandlePlayers();
                });
            }

            public void InitializeEvent(int startDelay)
            {
                Started = false;
                StartTime = instance.GrabCurrentTime() + startDelay;
                ServerMgr.Instance.StartCoroutine(instance.InitializeFoundation(startDelay));
            }

            public void FinishEvent()
            {
                Finished = true;
                Given = false;
                StartTimer?.Destroy();
                DestroyTimer?.Destroy();

                if (instance.RadiationZones.ContainsKey(BlockList.SelectMany(p => p).Last().GetInstanceID()))
                {
                    UnityEngine.Object.Destroy(instance.RadiationZones[BlockList.SelectMany(p => p).Last().GetInstanceID()].zone);
                    instance.RadiationZones.Remove(BlockList.SelectMany(p => p).Last().GetInstanceID());
                }
                for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
                {
                    CuiHelper.DestroyUi(BasePlayer.activePlayerList[i], Layer);
                    CuiHelper.DestroyUi(BasePlayer.activePlayerList[i], Layer + ".FoundationDrop.Play");
                }
                var listPredicted = BasePlayer.activePlayerList.Where(p => cEvent.PlayerConnected.ContainsKey(p.userID));

                foreach (var check in listPredicted)
                    LeftEvent(check);

                foreach (var check in BlockList.SelectMany(p => p))
                    check?.Kill();
            }
        }

        #region Radiation Control
        public class ZoneList
        {
            public RadZones zone;
        }
        private void OnServerRadiation()
        {
            var allobjects = UnityEngine.Object.FindObjectsOfType<TriggerRadiation>();
            for (int i = 0; i < allobjects.Length; i++)
            {
                UnityEngine.Object.Destroy(allobjects[i]);
            }
        }
        private Dictionary<int, ZoneList> RadiationZones = new Dictionary<int, ZoneList>();
        private static readonly int playerLayer = LayerMask.GetMask("Player (Server)");
        //private static readonly Collider[] colBuffer = Vis.colBuffer;
        private void InitializeZone(Vector3 Location, float intensity, int ZoneID)
        {
            float radius = 10f;
            if (!ConVar.Server.radiation) ConVar.Server.radiation = true;
            if (config.FoundationDrop.DisableDefaultRadiation)
                OnServerRadiation();
            var newZone = new GameObject().AddComponent<RadZones>();
            newZone.Activate(Location, radius, intensity, ZoneID);
            ZoneList listEntry = new ZoneList
            {
                zone = newZone
            };
            RadiationZones.Add(ZoneID, listEntry);
        }

        public class RadZones : MonoBehaviour
        {
            private int ID;
            private Vector3 Position;
            private float ZoneRadius;
            private float RadiationAmount;
            private List<BasePlayer> InZone;
            private void Awake()
            {
                gameObject.layer = (int)Rust.Layer.Reserved1;
                gameObject.name = "NukeZone";
                var rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
            }
            public void Activate(Vector3 pos, float radius, float amount, int ZoneID)
            {
                ID = ZoneID;
                Position = pos;
                ZoneRadius = radius;
                RadiationAmount = amount;
                gameObject.name = $"Foundation{ID}";
                transform.position = Position;
                transform.rotation = new Quaternion();
                UpdateCollider();
                gameObject.SetActive(true);
                enabled = true;
                var Rads = gameObject.GetComponent<TriggerRadiation>();
                Rads = Rads ?? gameObject.AddComponent<TriggerRadiation>();
                Rads.RadiationAmount = RadiationAmount;
                //Rads.radiationSize = ZoneRadius;
                Rads.interestLayers = playerLayer;
                Rads.enabled = true;
                if (IsInvoking("UpdateTrigger")) CancelInvoke("UpdateTrigger");
                InvokeRepeating("UpdateTrigger", 5f, 5f);
            }
            private void OnDestroy()
            {
                CancelInvoke("UpdateTrigger");
                Destroy(gameObject);
            }
            private void UpdateCollider()
            {
                var sphereCollider = gameObject.GetComponent<SphereCollider>();
                {
                    if (sphereCollider == null)
                    {
                        sphereCollider = gameObject.AddComponent<SphereCollider>();
                        sphereCollider.isTrigger = true;
                    }
                    sphereCollider.radius = ZoneRadius;
                }
            }
            /*private void UpdateTrigger()
            {
                InZone = new List<BasePlayer>();
                int entities = Physics.OverlapSphereNonAlloc(Position, ZoneRadius, colBuffer, playerLayer);
                for (var i = 0;
                i < entities;
                i++)
                {
                    var player = colBuffer[i].GetComponentInParent<BasePlayer>();
                    if (player != null) InZone.Add(player);
                }
            }*/
        }
        #endregion
        #region Utils
        private IEnumerator InitializeFoundation(int startDelay)
        {
            for (int i = -config.FoundationDrop.ArenaSize / 2; i < config.FoundationDrop.ArenaSize / 2; i++)
            {
                for (int t = -config.FoundationDrop.ArenaSize / 2; t < config.FoundationDrop.ArenaSize / 2; t++)
                {
                    cEvent.BlockList.Add(new List<BaseEntity>());
                    var newFoundation = GameManager.server.CreateEntity("assets/prefabs/building core/foundation/foundation.prefab", EventPosition + new Vector3(i * 3, 4, t * 3)) as BuildingBlock;
                    newFoundation.Spawn();
                    newFoundation.OwnerID = 98596;

                    newFoundation.SetGrade(BuildingGrade.Enum.TopTier);
                    newFoundation.SetHealthToMax();
                    cEvent.BlockList.Last().Add(newFoundation);
                    yield return i;
                }
            }
            cEvent.StartEvent(startDelay);

            for (int g = 0; g < BasePlayer.activePlayerList.Count; g++)
                TopUI(BasePlayer.activePlayerList[g], "all", GetMessage(nowEvent, BasePlayer.activePlayerList[g].UserIDString), description: GetMessage("FOUNDROP.JOIN", BasePlayer.activePlayerList[g].UserIDString), oMin: config.FoundationDrop.ui.oMin, oMax: config.FoundationDrop.ui.oMax, color: config.FoundationDrop.ui.color);

            Main = timer.Repeat(1, (int)(time - GrabCurrentTime()), () =>
            {
                for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
                {
                    TopUI(BasePlayer.activePlayerList[i], "FoundationDrop_Timer");
                }
            });
        }

        private void DropFoundation()
        {
            if (cEvent.Finished || cEvent.BlockList.SelectMany(p => p).Count() == 1) return;

            var cStack = cEvent.BlockList.GetRandom();
            if (cStack.Count == 0)
            {
                cEvent.BlockList.Remove(cStack);
                DropFoundation();
                return;
            }

            var cBlock = cStack.First();
            if (cBlock == null || cBlock.isDestroyed)
            {
                cStack.RemoveAt(0);
                DropFoundation();
                return;
            }

            cStack.RemoveAt(0);
            cBlock.gameObject.AddComponent<Downgrader>().Downgrade(cBlock);

            cEvent.DestroyTimer = timer.Once(config.FoundationDrop.DelayDestroy, DropFoundation);
        }
        #endregion

        private class Downgrader : MonoBehaviour
        {
            private BuildingGrade.Enum gradeEnum;
            private BuildingBlock block;
            public void Downgrade(BaseEntity entity)
            {
                block = entity?.GetComponent<BuildingBlock>();
                if (block == null)
                {
                    Destroy(this);
                    return;
                }

                InvokeRepeating("Downgrade", 0, config.FoundationDrop.DelayDestroy / 5);
            }
            private void Downgrade()
            {
                if (block.grade < gradeEnum)
                {
                    Destroy(this);
                    return;
                }
                if (block.grade == BuildingGrade.Enum.Twigs)
                {
                    block.Kill();
                    Destroy(this);
                    return;
                }
                block.SetGrade(block.grade - 1);
                block.transform.position -= new Vector3(0, 0.5f, 0);
                block.SendNetworkUpdate();
                block.UpdateSkin();
            }
            private void OnDestroy()
            {
                CancelInvoke();
            }
            public void Kill()
            {
                Destroy(this);
            }
        }
        private class HeliPet : MonoBehaviour
        {
            private BaseHelicopter helicopter;
            private void Awake()
            {
                helicopter = GetComponent<BaseHelicopter>();
                InvokeRepeating("Cheking", 1, 1);
            }
            private void Cheking()
            {
                if (helicopter == null)
                {
                    Destroy(this);
                    return;
                }
                var phi = helicopter.GetComponent<PatrolHelicopterAI>();
                HashSet<BasePlayer> targets = new HashSet<BasePlayer>();
                foreach (var check in phi._targetList)
                {
                    if (check != null)
                        targets.Add(check.ply);
                }
                foreach (var p in targets)
                {
                    if (phi._currentState == PatrolHelicopterAI.aiState.ORBIT)
                        instance.AddToDictionary(p, 1);
                    else if (phi._currentState == PatrolHelicopterAI.aiState.STRAFE)
                        instance.AddToDictionary(p, 2);
                }
            }
            private void OnDestroy() => CancelInvoke();
            public void Kill() => Destroy(this);
        }

        #endregion
    }
}