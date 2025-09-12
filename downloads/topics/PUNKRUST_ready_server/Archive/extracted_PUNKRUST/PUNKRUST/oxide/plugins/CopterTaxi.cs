using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Rust;
using System.Linq;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using Oxide.Core;
using ProtoBuf;

namespace Oxide.Plugins
{
    [Info("CopterTaxi", "Qbis", "1.2.1")]
    public class CopterTaxi : RustPlugin
    {
        #region [Vars]
        [PluginReference] Plugin ImageLibrary, Economics, IQEconomic, ServerRewards;
        private static CopterTaxi plugin;

        private List<ScrapTransportHelicopter> Copters = new List<ScrapTransportHelicopter>();
        private Dictionary<ulong, int> playersLastCall = new Dictionary<ulong, int>();
        private List<Vector3> SpawnsComp = new List<Vector3>()
        {
            new Vector3(13.2f, 11.1f, 3.0f),
            new Vector3(17.2f, 0.3f, -29.7f),
            new Vector3(-45.7f, 0.3f, -70.7f),
            new Vector3(50.7f, 0.3f, 57.7f)
        };

        private List<Vector3> SpawnsBandit = new List<Vector3>()
        {
            new Vector3(45.2f, 15.1f, -20.0f),
            new Vector3(-15.2f, 5.1f, 5.0f),
            new Vector3(55.2f, 11.1f, 18.0f),
            new Vector3(13.2f, 11.1f, 63.0f)
        };
        #endregion

        #region [Config]
        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            config = PluginConfig.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();

            if (config.PluginVersion < Version)
                UpdateConfigValues();

            Config.WriteObject(config, true);
        }

        private void UpdateConfigValues()
        {
            PluginConfig baseConfig = PluginConfig.DefaultConfig();
            if (config.PluginVersion < Version)
            {
                PrintWarning("Plugin update detected! Checking config values...");
                if (Version == new VersionNumber(1, 0, 5))
                {
                    config.taxi.copterFly = 80f;
                }

                if (Version == new VersionNumber(1, 2, 0))
                {
                    config.taxi.monument = "compound";
                }

                config.PluginVersion = Version;
                PrintWarning("Config checked completed!");
            }
            config.PluginVersion = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private class PluginConfig
        {
            [JsonProperty("Настройки такси")]
            public TaxiSettings taxi;

            [JsonProperty("Настройки стоимости такси")]
            public PriceSettings price;

            [JsonProperty("Настройка UI")]
            public UiSettings ui;

            [JsonProperty("Версия конфига")]
            public VersionNumber PluginVersion = new VersionNumber();

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    taxi = new TaxiSettings()
                    {
                        name = "Виталя",
                        clothes = new Dictionary<string, ulong>
                        {
                            ["hat.cap"] = 2275597860,
                            ["tshirt"] = 2442749397,
                            ["pants"] = 2346785436,
                            ["shoes.boots"] = 1427198029
                        },
                        copterCount = 4,
                        copterSpeed = 2f,
                        copterFly = 80f,
                        copterSpeedUp = 0.2f,
                        copterSpeedLow = 0.5f,
                        copterCallAgain = 600,
                        copterWaitPay = 300,
                        chatIcon = 76561198976168730,
                        monument = "compound",
                        permmission = "coptertaxi.use",
                        discount = new Dictionary<string, int>()
                        {
                            ["coptertaxi.discount10"] = 10,
                            ["coptertaxi.discount20"] = 20,
                            ["coptertaxi.discount30"] = 30
                        }
                    },
                    price = new PriceSettings()
                    {
                        type = "Item",
                        Cost = 100,
                        shortname = "scrap",
                        skinID = 0,
                        url = "https://i.imgur.com/jBaVKHu.png"
                    },
                    ui = new UiSettings()
                    {
                        colorBG = "0 0 0 0.3",
                        colorLines = "1.00 0.64 0.28 1.00",
                        colorButtonPay = "0.09 0.39 0.14 0.85",
                        colorButtonReturn = "0.39 0.09 0.14 0.85",
                        colorButtonCall = "1.00 0.75 0.50 0.5",
                        photoUrl = "https://i.imgur.com/tgSooQl.png"
                    },
                    PluginVersion = new VersionNumber()

                };
            }
        }

        public class TaxiSettings
        {
            [JsonProperty("Имя для таксиста")]
            public string name;

            [JsonProperty("Одежда таксиста")]
            public Dictionary<string, ulong> clothes;

            [JsonProperty("Сколько создавать такси (от 1 до 4)")]
            public int copterCount;

            [JsonProperty("Высота полета такси (от 80 до 150) (менять с осторожностью!)")]
            public float copterFly;

            [JsonProperty("Скорость полета такси (менять с осторожностью!)")]
            public float copterSpeed;

            [JsonProperty("Скорость ускорения полета такси (менять с осторожностью!)")]
            public float copterSpeedUp;

            [JsonProperty("Скорость замедления полета такси (менять с осторожностью!)")]
            public float copterSpeedLow;

            [JsonProperty("Сколько ждет таксист для назначения маршрута игроком (в секундах)")]
            public int copterWaitPay;

            [JsonProperty("Как часто игрок может вызывать такси(в секундах)")]
            public int copterCallAgain;

            [JsonProperty("Привилегия для использования такси")]
            public string permmission;

            [JsonProperty("Привилегии для скидки")]
            public Dictionary<string, int> discount;

            [JsonProperty("Аватар для оповещения в чате (steamID)")]
            public ulong chatIcon;

            [JsonProperty("Выбор города для спавна такси (compound, bandit_town)")]
            public string monument;
        }

        public class PriceSettings
        {
            [JsonProperty("Тип оплаты для такси Item, CustomItem, Economics, IQEconomic, ServerRewards")]
            public string type;

            [JsonProperty("Стоимость 1км")]
            public int Cost;

            [JsonProperty("ShortName предмета (для Item, CustomItem)")]
            public string shortname;

            [JsonProperty("SkinID предмета (для CustomItem)")]
            public ulong skinID;

            [JsonProperty("Ссылка на картинку предмета/валюты  (кроме Item)")]
            public string url;
        }

        public class UiSettings
        {
            [JsonProperty("Цвет фона")]
            public string colorBG;

            [JsonProperty("Цвет обводки")]
            public string colorLines;

            [JsonProperty("Ссылка на логотип")]
            public string photoUrl;

            [JsonProperty("Цвет кнопки 'Вызвать'")]
            public string colorButtonCall;

            [JsonProperty("Цвет кнопки 'Оплатить'")]
            public string colorButtonPay;

            [JsonProperty("Цвет кнопки 'Отказаться'")]
            public string colorButtonReturn;
        }
        #endregion

        #region Localization⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AlreadyCall"] = "You have already been allocated a taxi!",
                ["Cant"] = "There is an obstacle for landing in this place, choose another point!",
                ["Near"] = "To fly too close, and you can walk!",
                ["NoBuild1"] = "A taxi will soon sit in this place, construction is prohibited at times!",
                ["NoBuild2"] = "Building next to a taxi is prohibited!",
                ["NoPerm"] = "You don't have access to call a taxi!",
                ["Distance"] = "Remaining: {0}km",
                ["Marker"] = "Waiting for a marker: {0}m. {1}s.",
                ["Nofunds"] = "You don't have enough funds",
                ["Say1"] = "<color=orange>{0}</color>: Hello, put a point on the map where to take you!",
                ["Say2"] = "<color=orange>{0}</color>: Bye!",
                ["Say3"] = "<color=orange>{0}</color>: Hello, I'm here! I ask you to board.",
                ["Say4"] = "<color=orange>{0}</color>: I warm up the engine and fly to you!",
                ["Say5"] = "<color=orange>{0}</color>: Fasten your seat belts, we take off!",
                ["UI_Header"] = "Vitala-Taxi LLC",
                ["UI_Text"] = "Tired of wasting resources? So call Vital!\nWe will deliver with comfort and a solid fart!",
                ["UI_Cost"] = "for 1 km | Discount {0}%",
                ["UI_Cost2"] = "flight cost | Distance {0} km | Discount {1}%",
                ["UI_Call"] = "A taxi call will be available in {0}h. {1}m. {2}s.",
                ["UI_NoCall"] = "There are no available taxis at the moment, please try again later",
                ["UI_Call2"] = "Call",
                ["UI_Pay"] = "Pay",
                ["UI_Close"] = "Close",
                ["UI_Refuse"] = "Refuse",
                ["UI_Free"] = "Free"
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AlreadyCall"] = "Вам уже выделено такси!",
                ["Cant"] = "В этом месте есть помехи для посадки, выберите другую точку!",
                ["Near"] = "Слишком близко лететь, можно и пешочком!",
                ["NoBuild1"] = "В этом месте скоро сядет такси, постройка времена запрещена!",
                ["NoBuild2"] = "Постройка рядом с такси запрещена!",
                ["NoPerm"] = "У вас нет доступа для вызова такси!",
                ["Distance"] = "Осталось: {0}км",
                ["Marker"] = "Ожидание маркера: {0}м. {1}с.",
                ["Nofunds"] = "У вас недостаточно средств!",
                ["Say1"] = "<color=orange>{0}</color>: Здравствуйте, поставьте точку на карте, куда Вас подвезти!",
                ["Say2"] = "<color=orange>{0}</color>: До свидания!",
                ["Say3"] = "<color=orange>{0}</color>: Здравствуйте, я на месте! Прошу Вас на борт.",
                ["Say4"] = "<color=orange>{0}</color>: Прогреваю двигатель и вылетаю к Вам!",
                ["Say5"] = "<color=orange>{0}</color>: Пристегнуть ремни, мы взлетаем!",
                ["UI_Header"] = "ООО Виталя-Такси",
                ["UI_Text"] = "Устал просирать ресурсы? Так вызови Виталю!\nДоставим с комфортом и цельным пуканом!",
                ["UI_Cost"] = "за 1 км | Скидка {0}%",
                ["UI_Cost2"] = "стоимость перелета | Расстояние {0}км | Скидка {1}%",
                ["UI_Call"] = "Вызов такси будет доступен через {0}ч. {1}м. {2}c.",
                ["UI_NoCall"] = "В данный момент нет свободных такси, попробуйте позже.",
                ["UI_Call2"] = "Вызвать",
                ["UI_Pay"] = "Оплатить",
                ["UI_Close"] = "Закрыть",
                ["UI_Refuse"] = "Отказаться",
                ["UI_Free"] = "Бесплатно"
            }, this, "ru");
        }
        string GetMsg(string key, BasePlayer player = null) => lang.GetMessage(key, this, player?.UserIDString);
        string GetMsg(string key) => lang.GetMessage(key, this);
        #endregion

        #region [Func]
        private void SpawnHelicopters()
        {
            var monument = TerrainMeta.Path.Monuments.Where(m => m.name.Contains(config.taxi.monument)).FirstOrDefault();
            if(monument == null)
            {
                PrintError($"Monument {config.taxi.monument} not found!");
                return;
            }

            List<Vector3> Spawns = new List<Vector3>();
            if (config.taxi.monument == "bandit_town")
                Spawns = SpawnsBandit;
            else
                Spawns = SpawnsComp;

            int i = 0;
            foreach (var spawn in Spawns)
            {
                if (i >= config.taxi.copterCount)
                    return;

                var copter = GameManager.server.CreateEntity("assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab", monument.transform.position + monument.transform.rotation * spawn) as ScrapTransportHelicopter;
                copter.enableSaving = false;
                copter.Spawn();
                BasePlayer bot = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", copter.transform.position + new Vector3(-5f, 5f, 0f)).ToPlayer();
                bot.enableSaving = false;
                bot.Spawn();
                bot.displayName = config.taxi.name;
                foreach (var cloth in config.taxi.clothes)
                {
                    var item = ItemManager.CreateByName(cloth.Key, 1, cloth.Value);
                    item.MoveToContainer(bot.inventory.containerWear);
                }
                BaseMountable seat;
                bot.MountObject(seat = copter.GetIdealMountPoint(bot.eyes.position, bot.transform.position, bot));
                seat.AttemptMount(bot);
                var AI = copter.gameObject.AddComponent<AIcopter>();
                AI.SetCopterWaitOrderPos(monument.transform.position + monument.transform.rotation * spawn);
                AI.SetTaxiDriver(bot);
                Copters.Add(copter);

                i++;
            }
        }

        private bool IsPlayerOnHeli(BasePlayer player)
        {
            if (player.isMounted)
            {
                var heli = player.GetMountedVehicle() as ScrapTransportHelicopter;
                if (heli == null)
                    return false;

                if (Copters.Contains(heli))
                    return true;
            }

            var ParentHeli = player.GetComponentInParent<ScrapTransportHelicopter>();
            if (ParentHeli != null)
                if (Copters.Contains(ParentHeli))
                    return true;

            return false;
        }

        private bool IsPlayerDriver(BasePlayer player)
        {
            if (player.isMounted)
            {
                var heli = player.GetMountedVehicle() as ScrapTransportHelicopter;
                if (heli == null)
                    return false;

                if (Copters.Contains(heli))
                {
                    var AI = heli.GetComponent<AIcopter>();
                    if (AI == null)
                        return false;

                    if (AI.IsTaxiDriver(player))
                        return true;
                }
            }
            return false;
        }

        private bool ValidPosition(Vector3 randomPos, BasePlayer player = null)
        {
            if (WaterLevel.Test(randomPos + new Vector3(0, 1.3f, 0))) return false;

            var colliders = new List<Collider>();
            Vis.Colliders(randomPos, 8f, colliders);
            if (colliders.Where(col => col.name.ToLower().Contains("prevent") && col.name.ToLower().Contains("building")).Count() > 0) return false;


            var entities = new List<BaseEntity>();
            Vis.Entities(randomPos, 8f, entities);
            if (entities.Where(ent => ent is BaseVehicle || ent is CargoShip || ent is BaseHelicopter || ent is BradleyAPC || ent is BuildingBlock).Count() > 0) return false;

            Vis.Entities(randomPos, 4f, entities);
            if (entities.Where(ent => ent.PrefabName.Contains("resource")).Count() > 0) return false;



            var cupboards = new List<BuildingPrivlidge>();
            Vis.Entities(randomPos, 30f, cupboards);
            if (player == null)
            {
                if (cupboards.Count > 0)
                    return false;
            }
            else
            {
                if (cupboards.Count <= 0)
                    return true;

                foreach (var cup in cupboards)
                    if (cup.IsAuthed(player))
                        return true;

                return false;
            }

            return true;
        }

        private bool IsAlreadyClient(BasePlayer player, AIcopter c)
        {
            foreach (var copter in Copters)
            {
                var AI = copter.GetComponent<AIcopter>();
                if (AI == null)
                    continue;

                if (AI.GetTaxiClient() == player)
                {
                    if (AI == c)
                        return false;
                    else
                        return true;
                }
            }
            return false;
        }

        private bool IsAlreadyClient(BasePlayer player)
        {
            foreach (var copter in Copters)
            {
                var AI = copter.GetComponent<AIcopter>();
                if (AI == null)
                    continue;

                if (AI.GetTaxiClient() == player)
                    return true;
            }

            return false;

        }

        private int GetCost(float distance) => Convert.ToInt32(Math.Round(distance / 1000 * config.price.Cost));

        private bool HasPlayerItems(BasePlayer player, string shortname, int amount, ulong skinid = 0)
        {
            var playerHas = 0;
            foreach(var item in player.inventory.containerMain.itemList)
            {
                if (item.info.shortname == shortname && item.skin == skinid)
                    playerHas += item.amount;

                if (playerHas >= amount)
                    return true;
            }

            foreach (var item in player.inventory.containerBelt.itemList)
            {
                if (item.info.shortname == shortname && item.skin == skinid)
                    playerHas += item.amount;

                if (playerHas >= amount)
                    return true;
            }

            return false;
        }

        private void RemovePlayerItems(BasePlayer player, string shortname, int amount, ulong skinid = 0)
        {
            int count = amount;
            for (int i = 0; i < 24; i++)
            {
                var item = player.inventory.containerMain.GetSlot(i);
                if (item == null)
                    continue;

                if (item.info.shortname == shortname && item.skin == skinid)
                {
                    if (item.amount > count)
                    {
                        item.UseItem(count);
                        count = 0;
                    }
                    else if (item.amount < count)
                    {
                        count -= item.amount;
                        item.UseItem(item.amount);
                    }
                    else
                    {
                        item.UseItem(item.amount);
                        count = 0;
                    }

                    if (count == 0)
                        return;
                }
            }

            for (int i = 0; i < 6; i++)
            {
                var item = player.inventory.containerBelt.GetSlot(i);
                if (item == null)
                    continue;

                if (item.info.shortname == shortname && item.skin == skinid)
                {
                    if (item.amount > count)
                    {
                        item.UseItem(count);
                        count = 0;
                    }
                    else if (item.amount < count)
                    {
                        count -= item.amount;
                        item.UseItem(item.amount);
                    }
                    else
                    {
                        item.UseItem(item.amount);
                        count = 0;
                    }

                    if (count == 0)
                        return;
                }
            }
        }

        private string PayTaxi(BasePlayer player, int amount)
        {

            if (amount <= 0)
                return "paid";


            switch(config.price.type)
            {
                case "Economics":
                    return (bool)Economics?.Call("Withdraw", player.userID, Convert.ToDouble(amount)) ? "paid" : GetMsg("Nofunds", player);

                case "IQEconomic":
                    if ((bool)IQEconomic.Call("API_IS_REMOVED_BALANCE", player.userID, amount) == false)
                        return GetMsg("Nofunds", player);

                    IQEconomic.Call("API_REMOVE_BALANCE", player.userID, amount);
                    return "paid";


                case "ServerRewards":
                    return ServerRewards?.Call("TakePoints", player.userID, amount) == null ? GetMsg("Nofunds", player) : "paid";

                case "Item":
                    if (!HasPlayerItems(player, config.price.shortname, amount))
                        return GetMsg("Nofunds", player);

                    RemovePlayerItems(player, config.price.shortname, amount);
                    return "paid";

                case "CustomItem":
                    if (!HasPlayerItems(player, config.price.shortname, amount, config.price.skinID))
                        return GetMsg("Nofunds", player);

                    RemovePlayerItems(player, config.price.shortname, amount, config.price.skinID);
                    return "paid";
            }

            return "Ошибка #100, сообщите администратору";
        }

        private int GetPlayerDiscount(BasePlayer player)
        {
            var discount = 0;
            foreach(var dis in config.taxi.discount.OrderByDescending(d => d.Value))
            {
                if(permission.UserHasPermission(player.UserIDString, dis.Key))
                {
                    discount = dis.Value;
                    break;
                }
            }
                
            return discount; 
        }

        private void DisMountAllPlayers(Vector3 checkPos)
        {
            var players = new List<BasePlayer>();
            Vis.Entities(checkPos, 8f, players);
            float r = 0f;
            foreach(var player in players)
            {
                if (!player.IsConnected || !player.IsAlive())
                    continue;

                if(IsPlayerOnHeli(player))
                {
                    if (player.isMounted)
                    {
                        player.GetMounted().DismountPlayer(player);
                    }

                    player.Teleport(checkPos + new Vector3(-3f, 3f, r));
                    r += 1f;
                }
            }

        }
        #endregion

        #region [Oxide]
        private void OnServerInitialized()
        {
            SpawnHelicopters();
            permission.RegisterPermission(config.taxi.permmission, this);
            foreach (var perm in config.taxi.discount)
                permission.RegisterPermission(perm.Key, this);

            ImageLibrary?.Call("AddImage", config.ui.photoUrl, "taxiPhoto");

            if (config.price.type != "Item")
                ImageLibrary?.Call("AddImage", config.price.url, config.price.type + "_" + config.price.skinID);
        }

        private void Init()
        {
            plugin = this;
        }

        private void Unload()
        {
            plugin = null;
            foreach (var copter in Copters)
            {
                if (copter != null)
                {
                    var AI = copter.gameObject.GetComponent<AIcopter>();
                    AI.KillTaxiDriver();
                    UnityEngine.Object.Destroy(AI);
                    copter.Kill();
                }
            }
        }

        private object OnSamSiteTarget(SamSite entity, BaseCombatEntity target)
        {
            if (entity == null || target == null)
                return null;

            var heli = target as ScrapTransportHelicopter;
            if (!heli)
                return null;

            if (!Copters.Contains(heli))
                return null;

            return false;
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null)
                return null;

            if (entity as ScrapTransportHelicopter)
            {
                if (Copters.Contains(entity as ScrapTransportHelicopter))
                    return false;

                return null;
            }

            var attacker = info?.InitiatorPlayer;
            var victim = entity?.ToPlayer();

            if (victim == null)
                return null;

            if (attacker == null)
            {
                if (IsPlayerOnHeli(victim))
                    return false;

                return null;
            }

            if (IsPlayerOnHeli(attacker) || IsPlayerOnHeli(victim))
                return false;

            return null;
        }

        private object CanMountEntity(BasePlayer player, BaseMountable entity)
        {
            if (entity == null || player == null)
                return null;

            var copter = entity.VehicleParent() as ScrapTransportHelicopter;
            if (copter == null)
                return null;

            if (!Copters.Contains(copter))
                return null;

            var AI = copter.GetComponent<AIcopter>();
            if (AI == null)
                return null;

            if (IsAlreadyClient(player, AI))
            {
                CreatePayCopterTaxiUI(player, "", 0, GetMsg("AlreadyCall", player));
                return false;
            }

            if (AI.IsBusy())
                return null;

            var client = AI.GetTaxiClient();
            if (client == null)
            {
                AI.SetStateWaitMarker();
                AI.SetNewClient(player);
                player.SendConsoleCommand("chat.add", new object[] { 0, config.taxi.chatIcon, String.Format(GetMsg("Say1", player), config.taxi.name)});
                return null;
            }

            if (player.userID == AI.GetTaxiClient().userID && AI.CanSetMarker())
                player.SendConsoleCommand("chat.add", new object[] { 0, config.taxi.chatIcon, String.Format(GetMsg("Say1", player), config.taxi.name) });

            return null;
        }

        private void OnEntityDismounted(BaseMountable entity, BasePlayer player)
        {
            var copter = entity.VehicleParent() as ScrapTransportHelicopter;
            if (copter == null)
                return;

            if (!Copters.Contains(copter))
                return;

            var AI = copter.GetComponent<AIcopter>();
            if (AI == null)
                return;

            if (Vector3.Distance(copter.transform.position, AI.GetCopterWaitOrderPos()) < 10f)
            {
                AI.SetStateWaitOrder();
                AI.SetNewClient(null);
                player.SendConsoleCommand("chat.add", new object[] { 0, config.taxi.chatIcon, String.Format(GetMsg("Say2", player), config.taxi.name) });
                return;
            }
        }

        private void OnMapMarkerAdded(BasePlayer player, MapNote note)
        {
            if (!player.isMounted)
                return;

            var copter = player.GetMountedVehicle() as ScrapTransportHelicopter;
            if (copter == null)
                return;

            if (!Copters.Contains(copter))
                return;

            var AI = copter.GetComponent<AIcopter>();
            if (AI == null)
                return;

            if (!AI.CanSetMarker())
                return;

            var client = AI.GetTaxiClient();
            if (client == null)
                return;

            if (client.userID != player.userID)
                return;

            RaycastHit hitInfo;
            Physics.Raycast(note.worldPosition + new Vector3(0f, 120f, 0f), Vector3.down, out hitInfo, 500f, Layers.Solid);


            if (!ValidPosition(hitInfo.point, player))
            {
                CreatePayCopterTaxiUI(player, "", 0, GetMsg("Cant", player));
                return;
            }

            if (Vector3.Distance(copter.transform.position, hitInfo.point) < 50f)
            {
                CreatePayCopterTaxiUI(player, "", 0, GetMsg("Near", player));
                return;
            }

            var metre = Vector3.Distance(hitInfo.point, AI.transform.position - new Vector3(0, AI.GetFlyHeight(), 0));
            var cost = Convert.ToInt32(Math.Round(metre / 1000 * config.price.Cost));
            var disc = GetPlayerDiscount(player);
            if (disc < 100)
                cost = cost - (cost / 100 * GetPlayerDiscount(player));
            else
                cost = 0;

            CreatePayCopterTaxiUI(player, (metre / 1000).ToString("0.00"), cost);
            AI.SetMarkerPos(hitInfo.point);
        }

        private object CanBuild(Planner plan, Construction prefab)
        {
            if (plan == null)
                return null;

            var player = plan?.GetOwnerPlayer();
            if (player == null)
                return null;

            foreach(var copter in Copters)
            {
                //Add
                if (copter == null)
                    continue;

                var AI = copter.GetComponent<AIcopter>();
                if (AI == null)
                    continue;

                if (!AI.IsBusy())
                    continue;

                if (AI.GoToPlayer())
                {

                    if (Vector3.Distance(player.transform.position, AI.GetCopterPlayerPos()) < 15f)
                    {
                        player.ChatMessage(GetMsg("NoBuild1", player));
                        return false;
                    }
                }
                else if (AI.GoToMarker())
                {
                    if (Vector3.Distance(player.transform.position, AI.GetCopterMarkerPos()) < 15f)
                    {
                        player.ChatMessage(GetMsg("NoBuild1", player));
                        return false;
                    }
                }
                else if(AI.WaitMarker())
                {
                    if (Vector3.Distance(player.transform.position, AI.transform.position) < 15f)
                    {
                        player.ChatMessage(GetMsg("NoBuild2", player));
                        return false;
                    }
                }


            }
            return null;
        }

        private object OnTurretTarget(AutoTurret turret, BaseCombatEntity entity)
        {
            if (turret == null || entity == null) return null;
            if (entity.ToPlayer() == null) return null;

            if (IsPlayerDriver(entity.ToPlayer())) return false;

            return null;
        }
        #endregion

        #region [Command]
        [ChatCommand("taxi")]
        private void cmd_CallTaxi(BasePlayer player, string c, string[] a)
        {
            if (!permission.UserHasPermission(player.UserIDString, config.taxi.permmission))
            {
                player.ChatMessage(GetMsg("NoPerm", player));
                return;
            }
            CreateCallCopterTaxiUI(player);
        }

        [ChatCommand("tt")]
        void casdas(BasePlayer player, string c, string[] a)
        {
            var entities = new List<BaseEntity>();
            Vis.Entities(player.transform.position, 1f, entities);
            foreach (var ent in entities)
                player.ChatMessage(ent.PrefabName);
        }
        #endregion

        #region [AI]
        public class AIcopter : MonoBehaviour
        {
            private ScrapTransportHelicopter copter;
            private EntityFuelSystem fuelSystem;
            private VehicleEngineController<MiniCopter> engineController;
            private BasePlayer client;
            private BasePlayer driver;
            private Vector3 WaitOrderPos;
            private Vector3 PlayerPos;
            private Vector3 MarkerPos;
            private Timer speedTimer;

            private float speed;
            private bool Up;
            private bool Down;
            private bool Rotate;
            private bool Stop;
            private bool Engine;
            private bool LowSpeed;
            private bool SearhedLet;
            private bool Let;
            private FlyStates State;
            private float rotationSpeed;
            private float FlyHeight;
            private float WaitSecondsMarker;
            private int time;
            private int wait;
            private RaycastHit hitInfo;
            private RaycastHit RayLine;
            private Vector3 move;
            private int HeightTime;

            private int testtime = 0;

            private enum FlyStates
            {
                FlyToPlayer = 1,
                FlyToStart = 2,
                FlyToMarker = 3,
                WaitOrder = 4,
                WaitMarker = 5
            }

            private void Awake()
            {
                copter = gameObject.GetComponent<ScrapTransportHelicopter>();
                fuelSystem = copter.GetFuelSystem();
                engineController = copter.engineController;
            }

            private void Start()
            {
                Up = false;
                Down = false;
                Rotate = false;
                Stop = true;
                Engine = false;
                LowSpeed = true;
                SearhedLet = false;

                State = FlyStates.WaitOrder;
                speed = plugin.config.taxi.copterSpeed;
                rotationSpeed = 0.5f;
                FlyHeight = plugin.config.taxi.copterFly;
                if (FlyHeight < 80)
                {
                    FlyHeight = 80;
                    plugin.PrintError("Taxi FlyHeight cant be lower 80");
                }
                WaitSecondsMarker = plugin.config.taxi.copterWaitPay;
            }

            private void FixedUpdate()
            {
                if (Engine)
                {
                    fuelSystem.TryUseFuel(0f, copter.fuelPerSec);
                    engineController.FinishStartingEngine();
                }

                if (Up || Down)
                    speed = 0.5f;

                switch (State)
                {

                    #region [FlyToPlayer]
                    case FlyStates.FlyToPlayer:

                        if (Stop)
                            return;

                        if (Up == true)
                        {
                            MoveObj(new Vector3(WaitOrderPos.x, FlyHeight, WaitOrderPos.z));

                            if (Rotate)
                                RotateObj(new Vector3(PlayerPos.x, transform.position.y, PlayerPos.z));

                            if (Vector3.Distance(transform.position, new Vector3(WaitOrderPos.x, FlyHeight, WaitOrderPos.z)) < 5f)
                            {
                                Up = false;
                                Rotate = false;
                                LowSpeed = true;
                                move = PlayerPos;
                                move.y = FlyHeight;
                                StartCoroutine(Hover(2f));
                                ChangeSpeed(1);
                            }
                            return;
                        }

                        if (Down == true)
                        {
                            if (Vector3.Distance(transform.position, PlayerPos) < 15f)
                                speed = 0.3f;

                            if (Vector3.Distance(transform.position, PlayerPos) < 10f)
                                speed = 0.2f;

                            MoveObj(PlayerPos);
                            if (Vector3.Distance(transform.position, PlayerPos) < 1.5f)
                            {
                                Stop = true;
                                Engine = false;
                                State = FlyStates.WaitMarker;
                                wait = Facepunch.Math.Epoch.Current;
                                StartCoroutine(WaitMarker(WaitSecondsMarker));
                                Down = false;
                                client.SendConsoleCommand("chat.add", new object[] { 0, plugin.config.taxi.chatIcon, String.Format(plugin.GetMsg("Say3", client), plugin.config.taxi.name) });
                            }
                            return;
                        }

                        if (client != null)
                        {
                            if (Facepunch.Math.Epoch.Current - time >= 2)
                            {
                                SetPlayerFlag(client, BasePlayer.PlayerFlags.IsAdmin, true);
                                client.SendConsoleCommand("ddraw.text", 2.01f, Color.white, PlayerPos + new Vector3(0, 2f, 0), String.Format(plugin.GetMsg("Distance", client), (Vector3.Distance(transform.position - new Vector3(0, FlyHeight, 0), PlayerPos) / 1000).ToString("0.00")));
                                SetPlayerFlag(client, BasePlayer.PlayerFlags.IsAdmin, false);
                                time = Facepunch.Math.Epoch.Current;
                            }
                        }

                        if (Vector3.Distance(transform.position, PlayerPos + new Vector3(0, FlyHeight, 0)) < 100f)
                            if (LowSpeed)
                                ChangeSpeed(2);

                        if (Vector3.Distance(transform.position, move) < 10f)
                        {
                            StartCoroutine(Hover(2f));
                            Down = true;
                            MoveObj(move);
                        }
                        else
                            MoveObj(move);
                        break;

                    #endregion

                    #region [FlyToStart]
                    case FlyStates.FlyToStart:
                        if (Stop)
                            return;

                        if (Up == true)
                        {
                            MoveObj(PlayerPos + new Vector3(0f, FlyHeight, 0f));

                            if (Rotate)
                                RotateObj(new Vector3(WaitOrderPos.x, transform.position.y, WaitOrderPos.z));

                            if (Vector3.Distance(transform.position, PlayerPos + new Vector3(0f, FlyHeight, 0f)) < 5f)
                            {
                                Up = false;
                                Rotate = false;
                                LowSpeed = true;
                                move = WaitOrderPos;
                                move.y = FlyHeight;
                                StartCoroutine(Hover(2f));
                                ChangeSpeed(1);
                            }
                            return;
                        }

                        if (Down == true)
                        {
                            if (Vector3.Distance(transform.position, WaitOrderPos) < 15f)
                                speed = 0.3f;
                            if (Vector3.Distance(transform.position, WaitOrderPos) < 10f)
                                speed = 0.2f;
                            MoveObj(WaitOrderPos);
                            if (Vector3.Distance(transform.position, WaitOrderPos) < 0.8f)
                            {
                                Stop = true;
                                Engine = false;
                                State = FlyStates.WaitOrder;
                                client = null;
                                MarkerPos = Vector3.zero;
                                Down = false;
                            }
                            return;
                        }

                        if (Vector3.Distance(transform.position, WaitOrderPos + new Vector3(0, FlyHeight, 0)) < 100f)
                            if (LowSpeed)
                                ChangeSpeed(2);

                        if (Vector3.Distance(transform.position, move) < 10f)
                        {
                            StartCoroutine(Hover(2f));
                            Down = true;
                            MoveObj(move);
                        }
                        else
                            MoveObj(move);
                        break;
                    #endregion

                    #region [FlyToMarker]
                    case FlyStates.FlyToMarker:
                        if (Stop)
                            return;

                        if (Up == true)
                        {
                            MoveObj(PlayerPos + new Vector3(0f, FlyHeight, 0f));

                            if (Rotate)
                                RotateObj(new Vector3(MarkerPos.x, transform.position.y, MarkerPos.z));

                            if (Vector3.Distance(transform.position, PlayerPos + new Vector3(0f, FlyHeight, 0f)) < 5f)
                            {
                                Up = false;
                                Rotate = false;
                                LowSpeed = true;
                                move = MarkerPos;
                                move.y = FlyHeight;
                                StartCoroutine(Hover(2f));
                                ChangeSpeed(1);
                            }
                            return;
                        }

                        if (Down == true)
                        {
                            if (Vector3.Distance(transform.position, MarkerPos) < 15f)
                                speed = 0.3f;
                            if (Vector3.Distance(transform.position, MarkerPos) < 10f)
                                speed = 0.2f;
                            MoveObj(MarkerPos);
                            if (Vector3.Distance(transform.position, MarkerPos) < 1.5f)
                            {
                                StartCoroutine(WaitOut(15f));
                                Stop = true;
                                Engine = false;
                                Down = false;
                            }
                            return;
                        }

                        if (Facepunch.Math.Epoch.Current - time >= 2)
                        {
                            if (client != null)
                            {
                                SetPlayerFlag(client, BasePlayer.PlayerFlags.IsAdmin, true);
                                client.SendConsoleCommand("ddraw.text", 2.01f, Color.white, MarkerPos + new Vector3(0, 3f, 0), String.Format(plugin.GetMsg("Distance", client), (Vector3.Distance(MarkerPos, transform.position - new Vector3(0, FlyHeight, 0)) / 1000).ToString("0.00")));
                                SetPlayerFlag(client, BasePlayer.PlayerFlags.IsAdmin, false);
                                time = Facepunch.Math.Epoch.Current;
                            }
                        }

                        if (Vector3.Distance(transform.position, MarkerPos + new Vector3(0, FlyHeight, 0)) < 100f)
                            if (LowSpeed)
                                ChangeSpeed(2);


                        if (Vector3.Distance(transform.position, move) < 10f)
                        {
                            StartCoroutine(Hover(2f));
                            Down = true;
                            MoveObj(move);
                        }
                        else
                            MoveObj(move);
                        break;
                    #endregion

                    #region [WaitMarker]
                    case FlyStates.WaitMarker:
                        if (client != null)
                        {
                            if (Vector3.Distance(transform.position, WaitOrderPos) > 10f)
                            {
                                if (Facepunch.Math.Epoch.Current - time >= 2)
                                {
                                    var wtime = TimeSpan.FromSeconds(wait + plugin.config.taxi.copterWaitPay - Facepunch.Math.Epoch.Current);

                                    SetPlayerFlag(client, BasePlayer.PlayerFlags.IsAdmin, true);
                                    client.SendConsoleCommand("ddraw.text", 2.01f, Color.white, transform.position + new Vector3(0, 4f, 0), String.Format(plugin.GetMsg("Marker", client), wtime.Minutes, wtime.Seconds));
                                    SetPlayerFlag(client, BasePlayer.PlayerFlags.IsAdmin, false);
                                    time = Facepunch.Math.Epoch.Current;
                                }
                            }
                        }
                        break;
                        #endregion
                }
            }

            private void MoveObj(Vector3 move)
            {
                if (transform.position.y < FlyHeight - 5f && SearhedLet && !Let)
                {
                    transform.position = Vector3.MoveTowards(transform.position, new Vector3(transform.position.x, FlyHeight, transform.position.z), 1f);
                }
                else
                {
                    transform.position = Vector3.MoveTowards(transform.position, move, speed);
                }
            }

            private void RotateObj(Vector3 look)
            {
                Quaternion targetRotation = Quaternion.LookRotation(look - transform.position);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            private void ChangeSpeed(int action)
            {

                speedTimer?.Destroy();

                if (action == 1)
                {
                    SearhedLet = true;
                    InvokeRepeating("CheckLet", 0.5f, 0.5f);

                    speedTimer = plugin.timer.Every(1f, () => {
                        if (speed >= plugin.config.taxi.copterSpeed + plugin.config.taxi.copterSpeedUp)
                        {
                            speed = plugin.config.taxi.copterSpeed;
                            speedTimer.Destroy();
                            return;
                        }

                        speed += plugin.config.taxi.copterSpeedUp;
                    });
                }
                else if (action == 2)
                {
                    LowSpeed = false;
                    SearhedLet = false;
                    speedTimer = plugin.timer.Every(2f, () => {
                        if (speed <= 1f)
                        {
                            speed = 1f;
                            speedTimer.Destroy();
                            return;
                        }
                        speed -= plugin.config.taxi.copterSpeedLow;
                    });
                }
            }

            private void CheckLet()
            {
                if (!SearhedLet)
                {
                    CancelInvoke("CheckLet");
                    return;
                }

                Vector3 pos = new Vector3(transform.position.x, FlyHeight - 5f, transform.position.z);
                Vector3 direct = pos + transform.rotation * new Vector3(0, 0, 100f);

                if (Physics.Linecast(pos, direct, out RayLine, Layers.Solid))
                {
                    if (RayLine.distance < 10f)
                        return;

                    Let = true;
                    FlyHeight += 15;
                    move.y = FlyHeight;
                }
                else
                {
                    Let = false;
                }
            }

            #region [IEnumerator]
            IEnumerator GoToPoint(FlyStates state)
            {
                Engine = true;
                FlyHeight = plugin.config.taxi.copterFly;
                yield return new WaitForSeconds(7f);
                Stop = false;
                Rotate = true;
                Up = true;
                if (state == FlyStates.FlyToStart || state == FlyStates.FlyToMarker)
                    PlayerPos = transform.position;

                State = state;

                yield return new WaitForSeconds(1f);
                if (state == FlyStates.FlyToStart)
                    plugin.DisMountAllPlayers(transform.position);
                yield return null;
            }

            IEnumerator WaitMarker(float wait)
            {
                yield return new WaitForSeconds(wait);
                if (State == FlyStates.WaitMarker)
                {
                    PlayerPos = transform.position;
                    State = FlyStates.FlyToStart;
                    StartCoroutine(GoToPoint(FlyStates.FlyToStart));
                }
                yield return null;
            }

            IEnumerator WaitOut(float wait)
            {
                yield return new WaitForSeconds(wait);
                PlayerPos = transform.position;
                MarkerPos = Vector3.zero;
                client = null;
                StartCoroutine(GoToPoint(FlyStates.FlyToStart));
                yield return null;
            }

            IEnumerator Hover(float time)
            {
                Stop = true;
                yield return new WaitForSeconds(time);
                Stop = false;

                yield return null;
            }
            #endregion

            #region [Func]
            public void SetCopterWaitOrderPos(Vector3 pos) => WaitOrderPos = pos;
            public Vector3 GetCopterWaitOrderPos() => WaitOrderPos;

            //Add
            public Vector3 GetCopterPlayerPos()
            {
                if (PlayerPos == null)
                    return Vector3.zero;

                return PlayerPos;
            }
            public Vector3 GetCopterMarkerPos() => MarkerPos;
            public void SetTaxiDriver(BasePlayer bot) => driver = bot;
            public bool IsTaxiDriver(BasePlayer bot) => driver == bot;
            public BasePlayer GetTaxiClient() => client;
            public void KillTaxiDriver() => driver.Kill();
            public void SetStateWaitMarker() => State = FlyStates.WaitMarker;
            public void SetStateWaitOrder() => State = FlyStates.WaitOrder;
            public void SetNewClient(BasePlayer player) => client = player;
            public float GetFlyHeight() => FlyHeight;
            public void SetMarkerPos(Vector3 pos) => MarkerPos = pos;
            public bool IsBusy()
            {
                if (State == FlyStates.WaitOrder)
                    return false;

                return true;
            }

            public bool CanSetMarker()
            {
                if (State == FlyStates.WaitMarker)
                    return true;

                return false;
            }

            public bool GoToPlayer()
            {
                if (State == FlyStates.FlyToPlayer)
                    return true;

                return false;
            }

            public bool GoToMarker()
            {
                if (State == FlyStates.FlyToMarker)
                    return true;

                return false;
            }

            public bool WaitMarker()
            {
                if (State == FlyStates.WaitMarker)
                    return true;

                return false;
            }

            public void SetCopterClient(BasePlayer player)
            {
                client = player;
                PlayerPos = player.transform.position;
                State = FlyStates.FlyToPlayer;
                StartCoroutine(GoToPoint(FlyStates.FlyToPlayer));
            }

            public void GoMarkerPosition()
            {
                PlayerPos = transform.position;
                StartCoroutine(GoToPoint(FlyStates.FlyToMarker));
                State = FlyStates.FlyToMarker;
            }

            void SetPlayerFlag(BasePlayer player, BasePlayer.PlayerFlags f, bool b)
            {
                if (plugin.permission.UserHasGroup(player.UserIDString, "admin")) return;

                if (b)
                {
                    if (player.HasPlayerFlag(f)) return;
                    player.playerFlags |= f;
                }
                else
                {
                    if (!player.HasPlayerFlag(f)) return;
                    player.playerFlags &= ~f;
                }
                player.SendNetworkUpdateImmediate(false);
            }
            #endregion
        }
        #endregion

        #region [UI]
        private void CreateCallCopterTaxiUI(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiElement { Parent = "Overlay", Name = "TaxiMain", Components = { new CuiImageComponent { Color = "0 0 0 0", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }, new CuiNeedsCursorComponent() } });
            UI.CreateButton(ref container, "TaxiMain", "0 0 0 0", "", 0, "0 0", "1 1", "UI_CLOSE_TAXI");
            UI.CreatePanelBlur(ref container, "TaxiPanel", "TaxiMain", config.ui.colorBG, "0.20 0.15", "0.80 0.90");

            //Lines
            UI.CreatePanel(ref container, "TaxiLine", "TaxiPanel", $"{config.ui.colorLines}", "0 0", "0.001 1");
            UI.CreatePanel(ref container, "TaxiLine", "TaxiPanel", $"{config.ui.colorLines}", "0 0", "1 0.001");
            UI.CreatePanel(ref container, "TaxiLine", "TaxiPanel", $"{config.ui.colorLines}", "0.999 0", "1 1");
            UI.CreatePanel(ref container, "TaxiLine", "TaxiPanel", $"{config.ui.colorLines}", "0 0.999", "1 1");

            UI.CreateTextOutLine(ref container, "TaxiPanel", GetMsg("UI_Header", player), "1 1 1 0.9", $"0 0.9", $"1 1", TextAnchor.MiddleCenter, 28);
            CreateImage(ref container, "TaxiHeader", "TaxiPanel", "1 1 1 1", "taxiPhoto", "0.2408962 0.5882353", "0.8039216 0.8970588");
            UI.CreateTextOutLine(ref container, "TaxiPanel", GetMsg("UI_Text", player), "1 1 1 0.9", $"0.06 0.4", $"1 0.56", TextAnchor.MiddleCenter, 22);


            UI.CreatePanel(ref container, "TaxiPrice", "TaxiPanel", "0 0 0 0", "0 0.252451", "1 0.3897059");

            if(config.price.type == "Item")
                CreateImage(ref container, "TaxiIcon", "TaxiPrice", "1 1 1 1", config.price.shortname, "0.4238683 0.09999967", "0.5249794 0.9400001");
            else
                CreateImage(ref container, "TaxiIcon", "TaxiPrice", "1 1 1 1", config.price.type + "_" + config.price.skinID, "0.4238683 0.09999967", "0.5249794 0.9400001");

            UI.CreateTextOutLine(ref container, "TaxiIcon", $"x{config.price.Cost}", "1 1 1 0.9", $"0 0", $"1 1", TextAnchor.LowerRight, 18);

            UI.CreateTextOutLine(ref container, "TaxiPrice", String.Format(GetMsg("UI_Cost", player), GetPlayerDiscount(player)), "1 1 1 0.9", $"0.5404664 0.09999967", $"0.9794239 0.9400001", TextAnchor.MiddleLeft, 18);

            int lastcall;
            playersLastCall.TryGetValue(player.userID, out lastcall);
            if (Facepunch.Math.Epoch.Current - lastcall < config.taxi.copterCallAgain)
            {
                var time = TimeSpan.FromSeconds(lastcall + config.taxi.copterCallAgain - Facepunch.Math.Epoch.Current);
                UI.CreateTextOutLine(ref container, "TaxiPanel", String.Format(GetMsg("UI_Call", player), time.Hours, time.Minutes, time.Seconds), "1 1 1 0.9", "0.009602189 0.0401606", "0.9903979 0.1506023", TextAnchor.MiddleCenter, 20);
            }
            else
                UI.CreateButton(ref container, "TaxiPanel", config.ui.colorButtonCall, GetMsg("UI_Call2", player), 24, "0.2866939 0.05220878", "0.7421124 0.1506023", "UI_CALL_TAXI", TextAnchor.MiddleCenter, "callbutton");

            UI.CreateButton(ref container, "TaxiPanel", "0 0 0 0", "✘", 28, "0.9350427 0.9369025", "0.9982907 0.998088", "UI_CLOSE_TAXI");


            CuiHelper.DestroyUi(player, "TaxiMain");
            CuiHelper.AddUi(player, container);
        }

        private void CreatePayCopterTaxiUI(BasePlayer player, string km, int cost, string text = "")
        {
            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiElement { Parent = "Overlay", Name = "TaxiMain", Components = { new CuiImageComponent { Color = "0 0 0 0", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }, new CuiNeedsCursorComponent() } });
            UI.CreateButton(ref container, "TaxiMain", "0 0 0 0", "", 0, "0 0", "1 1", "UI_CLOSE_TAXI");
            UI.CreatePanelBlur(ref container, "TaxiPanel", "TaxiMain", config.ui.colorBG, "0.2515625 0.4652777", "0.6929688 0.6319445");

            //Lines
            UI.CreatePanel(ref container, "TaxiLine", "TaxiPanel", $"{config.ui.colorLines}", "0 0", "0.001 1");
            UI.CreatePanel(ref container, "TaxiLine", "TaxiPanel", $"{config.ui.colorLines}", "0 0", "1 0.001");
            UI.CreatePanel(ref container, "TaxiLine", "TaxiPanel", $"{config.ui.colorLines}", "0.999 0", "1 1");
            UI.CreatePanel(ref container, "TaxiLine", "TaxiPanel", $"{config.ui.colorLines}", "0 0.999", "1 1");

            if (text == "")
            {

                UI.CreatePanel(ref container, "TaxiCost", "TaxiPanel", $"0 0 0 0", "0 0.35", "1 0.9");


                UI.CreateTextOutLine(ref container, "TaxiCost", String.Format(GetMsg("UI_Cost2", player), km, GetPlayerDiscount(player)), "1 1 1 0.9", $"0.18 0", $"1 1", TextAnchor.MiddleLeft, 18);
                if (config.price.type == "Item")
                    CreateImage(ref container, "TaxiIcon", "TaxiCost", "1 1 1 1", config.price.shortname, "0.0511505 0.07500029", "0.1628318 0.9333332");
                else
                    CreateImage(ref container, "TaxiIcon", "TaxiCost", "1 1 1 1", config.price.type + "_" + config.price.skinID, "0.1411505 0.07500029", "0.2528318 0.9333332");

                if(cost > 0)
                    UI.CreateTextOutLine(ref container, "TaxiIcon", $"x{cost}", "1 1 1 0.9", $"0 0", $"1 1", TextAnchor.LowerRight, 16);
                else
                    UI.CreateTextOutLine(ref container, "TaxiIcon", GetMsg("UI_Free", player), "1 1 1 0.9", $"0 0", $"1 1", TextAnchor.LowerRight, 12);

                UI.CreateButton(ref container, "TaxiPanel", config.ui.colorButtonPay, GetMsg("UI_Pay", player), 18, "0.2144448 0.08266856", "0.5806452 0.3372779", $"UI_PAY_TAXI {cost}");
                UI.CreateButton(ref container, "TaxiPanel", config.ui.colorButtonReturn, GetMsg("UI_Refuse", player), 18, "0.6079932 0.08266856", "0.9741937 0.3372779", "UI_CLOSE_TAXI");
            }
            else
            {

                UI.CreateTextOutLine(ref container, "TaxiPanel", text, "1 1 1 0.9", $"0.03 0", $"0.97 1", TextAnchor.MiddleCenter, 18);
                UI.CreateButton(ref container, "TaxiPanel", config.ui.colorButtonReturn, GetMsg("UI_Close", player), 18, "0.6079932 0.08266856", "0.9741937 0.3372779", "UI_CLOSE_TAXI");
            }

            CuiHelper.DestroyUi(player, "TaxiMain");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("UI_CLOSE_TAXI")]
        private void cmd_UI_CLOSE_TAXI(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            CuiHelper.DestroyUi(player, "TaxiMain");
        }

        [ConsoleCommand("UI_CALL_TAXI")]
        private void cmd_UI_CALL_TAXI(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            bool canCall = false;
            AIcopter callCopter = null;

            if (!playersLastCall.ContainsKey(player.userID))
                playersLastCall.Add(player.userID, 0);

            foreach(var copter in Copters)
            {
                var AI = copter.GetComponent<AIcopter>();
                if (AI == null)
                    return;

                if (!AI.IsBusy())
                {
                    canCall = true;
                    callCopter = AI;
                    break;
                }
            }

            if(!canCall || callCopter == null)
            {
                CuiElementContainer container = new CuiElementContainer();
                UI.CreateTextOutLine(ref container, "TaxiPanel", GetMsg("UI_NoCall", player), "1 1 1 0.9", "0.009602189 0.0401606", "0.9903979 0.1506023", TextAnchor.MiddleCenter, 16);
                CuiHelper.DestroyUi(player, "callbutton");
                CuiHelper.AddUi(player, container);
                return;
            }

            if(Facepunch.Math.Epoch.Current - playersLastCall[player.userID] < config.taxi.copterCallAgain)
            {
                CuiElementContainer container = new CuiElementContainer();
                int lastcall;
                playersLastCall.TryGetValue(player.userID, out lastcall);
                var time = TimeSpan.FromSeconds(lastcall + config.taxi.copterCallAgain - Facepunch.Math.Epoch.Current);
                UI.CreateTextOutLine(ref container, "TaxiPanel", String.Format(GetMsg("UI_Call", player), time.Hours, time.Minutes, time.Seconds), "1 1 1 0.9", "0.009602189 0.0401606", "0.9903979 0.1506023", TextAnchor.MiddleCenter, 16);
                CuiHelper.DestroyUi(player, "callbutton");
                CuiHelper.AddUi(player, container);
                return;
            }

            if(!ValidPosition(player.transform.position, player))
            {
                CuiElementContainer container = new CuiElementContainer();
                UI.CreateButton(ref container, "TaxiPanel", config.ui.colorButtonCall, GetMsg("Cant", player), 12, "0.2866939 0.05220878", "0.7421124 0.1506023", "UI_CALL_TAXI", TextAnchor.MiddleCenter, "callbutton");
                CuiHelper.DestroyUi(player, "callbutton");
                CuiHelper.AddUi(player, container);
                return;
            }

            if(player.IsSwimming() || !player.IsOnGround())
            {
                CuiElementContainer container = new CuiElementContainer();
                UI.CreateButton(ref container, "TaxiPanel", config.ui.colorButtonCall, GetMsg("Cant", player), 12, "0.2866939 0.05220878", "0.7421124 0.1506023", "UI_CALL_TAXI", TextAnchor.MiddleCenter, "callbutton");
                CuiHelper.DestroyUi(player, "callbutton");
                CuiHelper.AddUi(player, container);
                return;
            }

            if(IsAlreadyClient(player))
            {
                CuiElementContainer container = new CuiElementContainer();
                UI.CreateTextOutLine(ref container, "TaxiPanel", GetMsg("AlreadyCall", player), "1 1 1 0.9", "0.009602189 0.0401606", "0.9903979 0.1506023", TextAnchor.MiddleCenter, 16);
                CuiHelper.DestroyUi(player, "callbutton");
                CuiHelper.AddUi(player, container);
                return;
            }

            playersLastCall[player.userID] = Facepunch.Math.Epoch.Current;
            callCopter.SetCopterClient(player);
            player.SendConsoleCommand("chat.add", new object[] { 0, config.taxi.chatIcon, String.Format(GetMsg("Say4", player), config.taxi.name) });
            CuiHelper.DestroyUi(player, "TaxiMain");
        }

        [ConsoleCommand("UI_PAY_TAXI")]
        private void cmd_UI_PAY_TAXI(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;


            var result = PayTaxi(player, Convert.ToInt32(arg.Args[0]));
            if(result != "paid")
            {
                CreatePayCopterTaxiUI(player, "", 0, result);
                return;
            }

            foreach (var copter in Copters)
            {
                var AI = copter.GetComponent<AIcopter>();
                if (AI == null)
                    return;

                if (AI.GetTaxiClient() != player)
                    continue;

                AI.GoMarkerPosition();
                player.SendConsoleCommand("chat.add", new object[] { 0, config.taxi.chatIcon, String.Format(GetMsg("Say5", player), config.taxi.name)});
                CuiHelper.DestroyUi(player, "TaxiMain");
                return;
            }

        }

        #region [UI generator]
        public class UI
        {

            public static void CreateButton(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter, string name = "button", float FadeIn = 0f)
            {

                container.Add(new CuiButton
                {

                    Button = { Color = color, Command = command, FadeIn = FadeIn },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }

                },
                panel, name);
            }

            public static void CreatePanel(ref CuiElementContainer container, string name, string parent, string color, string aMin, string aMax, float Fadeout = 0f, float Fadein = 0f)
            {

                container.Add(new CuiElement
                {
                    Name = name,
                    Parent = parent,
                    Components =
                {
                    new CuiImageComponent { Color = color, FadeIn = Fadein },
                    new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax}
                },
                    FadeOut = Fadeout
                });
            }

            public static void CreatePanelBlur(ref CuiElementContainer container, string name, string parent, string color, string aMin, string aMax, float Fadeout = 0f, float Fadein = 0f)
            {
                container.Add(new CuiPanel()
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Image = { Color = color, Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat", FadeIn = Fadein },
                    FadeOut = Fadeout
                }, parent, name);
            }
            public static void CreateText(ref CuiElementContainer container, string parent, string text, string color, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleLeft, int size = 14, string name = "name", float Fadein = 0f)
            {
                container.Add(new CuiElement
                {
                    Parent = parent,
                    Name = name,
                    Components =
                {
                    new CuiTextComponent(){ Color = color, Text = text, FontSize = size, Align = align, FadeIn = Fadein },
                    new CuiRectTransformComponent{ AnchorMin =  aMin ,AnchorMax = aMax }
                }
                });
            }

            public static void CreateTextOutLine(ref CuiElementContainer container, string parent, string text, string color, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleLeft, int size = 14, string name = "name", float Fadein = 0f)
            {
                container.Add(new CuiElement
                {
                    Parent = parent,
                    Name = name,
                    Components =
                {
                    new CuiTextComponent(){ Color = color, Text = text, FontSize = size, Align = align, FadeIn = Fadein },
                    new CuiRectTransformComponent{ AnchorMin =  aMin ,AnchorMax = aMax },
                    new CuiOutlineComponent{ Color = "0 0 0 1" }
                }
                });
            }
        }

        public void CreateImage(ref CuiElementContainer container, string name, string panel, string color, string image, string aMin, string aMax, float Fadeout = 0f, float Fadein = 0f, ulong skin = 0)
        {
            container.Add(new CuiElement
            {
                Name = name,
                Parent = panel,
                Components =
                {
                    new CuiRawImageComponent { Color = color, Png = (string)ImageLibrary.Call("GetImage", image, skin), FadeIn = Fadein },
                    new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax },

                },
                FadeOut = Fadeout
            });
        }
        #endregion
        #endregion
    }
}