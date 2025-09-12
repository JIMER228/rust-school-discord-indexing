using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("OreBonus", "DezLife", "3.0.9")]
    [Description("Добавляет новую руду в камни")]
    public class OreBonus : RustPlugin
    {
        public static OreBonus instance;
        public Dictionary<ulong, string> ToOres = new Dictionary<ulong, string>();
        bool furnacec = false;

        #region CFG
        public class CustomItem
        {
            [JsonProperty("Отображаемое имя")]
            public string DisplayName;
            [JsonProperty("Название предмета который он будет заменять")]
            public string ReplaceShortName;
            [JsonProperty("Что он получит после переплавки")]
            public string Shortnames;
            [JsonProperty("Количевство радиации каторое будет даваться каждый тик")]
            public float Radiations;
            [JsonProperty("Радиус радиации в метрах (От печки)")]
            public float RadiationsRadius;
            [JsonProperty("Минимальное выпадения переплавленой руды в печках")]
            public int mincount;
            [JsonProperty("Максимальное выпадения переплавленой руды в печках")]
            public int maxcount;

            [JsonProperty("Шанс выпадения")]
            public int DropChance;
            [JsonProperty("Максимальное количевство выпадения радиактивной руды")]
            public int DropAmount;

            [JsonProperty("Скин ID предмета")]
            public ulong ReplaceID;

            public int GetItemId() => ItemManager.FindItemDefinition(ReplaceShortName).itemid;
            public int GetItemAmount(BasePlayer player) => player.inventory.GetAmount(GetItemId());

            public Item Copy(int amount)
            {
                Item x = ItemManager.CreateByPartialName(ReplaceShortName, amount);
                x.skin = ReplaceID;
                x.name = DisplayName;

                return x;
            }

            public void CreateItem(BasePlayer player, int amount)
            {
                Item x = ItemManager.CreateByPartialName(ReplaceShortName, amount);
                x.skin = ReplaceID;
                x.name = DisplayName;

                if (player != null)
                {
                    if (player.inventory.containerMain.itemList.Count < 24)
                        x.MoveToContainer(player.inventory.containerMain);
                    else
                        x.Drop(player.transform.position, Vector3.zero);
                    return;
                }
            }
        }



        private class Configuration
        {
            [JsonProperty("Настройка руды")]
            public Dictionary<string, CustomItem> CustomItems = new Dictionary<string, CustomItem>
            {
                ["sulfur.ore"] = new CustomItem
                {
                    DisplayName = "Радиактивная сера",
                    ReplaceShortName = "glue",
                    Shortnames = "sulfur",
                    DropChance = 7,
                    DropAmount = 2,
                    ReplaceID = 1681986132,
                    Radiations = 10f,
                    RadiationsRadius = 15f,
                    mincount = 100,
                    maxcount = 1000,
                },
                ["metal.ore"] = new CustomItem
                {
                    DisplayName = "Радиактивный металл",
                    ReplaceShortName = "ducttape",
                    Shortnames = "metal.fragments",
                    DropChance = 11,
                    DropAmount = 3,
                    ReplaceID = 1681987157,
                    Radiations = 10f,
                    RadiationsRadius = 10f,
                    mincount = 300,
                    maxcount = 1500,
                },
                ["stones"] = new CustomItem
                {
                    DisplayName = "Радиактивный камень",
                    ReplaceShortName = "bleach",
                    Shortnames = "stones",
                    DropChance = 14,
                    DropAmount = 4,
                    ReplaceID = 1681987635,
                    Radiations = 10f,
                    RadiationsRadius = 10f,
                    mincount = 500,
                    maxcount = 2500,
                },
            };
        }

        private static Configuration Settings = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                Settings = Config.ReadObject<Configuration>();
                if (Settings?.CustomItems == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning("Ошибка #331 чтения конфигурации 'oxide/config/', создаём новую конфигурацию!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => Settings = new Configuration();
        protected override void SaveConfig() => Config.WriteObject(Settings);
        #endregion

        #region Hooks

        [ChatCommand("ore.give")]
        void CmdChatDebugOreSpawn(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;

            foreach (var check in Settings.CustomItems)
            {
                var item = check.Value.Copy(100);
                item.MoveToContainer(player.inventory.containerMain);
            }
        }
        void OnServerInitialized()
        {
            instance = this;
            furnacec = true;
            foreach (var skkinid in Settings.CustomItems)
            {
                ToOres.Add(skkinid.Value.ReplaceID, skkinid.Key);
            }

            List<BaseOven> baseOvens = UnityEngine.Object.FindObjectsOfType<BaseOven>().ToList();
            baseOvens.ForEach(baseOven =>
            {
                if (!(baseOven is BaseFuelLightSource))
                {
                    OnEntitySpawned(baseOven);
                }
            });
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!furnacec) return;
            if (entity == null) return;
            if (entity is BaseOven && !(entity is BaseFuelLightSource))
            {
                BaseOven baseOven = entity as BaseOven;
                if (baseOven == null) return;
                FurnaceBurn fBurn = new FurnaceBurn();
            }
        }
        object OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (player == null) return null;
            if (Settings.CustomItems.ContainsKey(item.info.shortname))
            {
                var items = Settings.CustomItems["sulfur.ore"];
                bool goodChance = UnityEngine.Random.Range(0, 100) >= (100 - items.DropChance);
                if (goodChance)
                {
                    Item RadSulfur = ItemManager.CreateByName(items.ReplaceShortName, UnityEngine.Random.Range(1, items.DropAmount), items.ReplaceID);
                    RadSulfur.name = items.DisplayName;
                    player?.GiveItem(RadSulfur);
                }
            }
            return null;
        }



        object CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
        {
            if (item.GetItem().skin != targetItem.GetItem().skin) return false;
            return null;
        }

        object CanStackItem(Item item, Item targetItem)
        {
            if (item.skin != targetItem.skin) return false;
            return null;
        }

        #endregion

        #region Metod
        public class FurnaceBurn
        {
            BaseOven oven;
            StorageContainer storageContainer;
            Timer timer;

            void timertick()
            {
                if (timer == null)
                {
                    timer = instance.timer.Once(5f, CheckRadOres);
                }
                else
                {
                    timer.Destroy();
                    timer = instance.timer.Once(5f, CheckRadOres);
                }
            }

            void CheckRadOres()
            {
                if (oven == null)
                {
                    timer.Destroy();
                    return;
                }
                if (oven.IsOn())
                {
                    foreach (var item in storageContainer.inventory.itemList)
                    {
                        if (instance.ToOres.ContainsKey(item.skin))
                        {
                            instance.NextTick(() =>
                            {
                                List<BasePlayer> players = new List<BasePlayer>();
                                Vis.Entities<BasePlayer>(oven.transform.position, Settings.CustomItems[instance.ToOres[item.skin]].RadiationsRadius, players);
                                players.ForEach(p => p.metabolism.radiation_poison.value += Settings.CustomItems[instance.ToOres[item.skin]].Radiations);

                                if (item.amount > 1) item.amount--;
                                else item.RemoveFromContainer();

                                Item newItem = ItemManager.CreateByName(Settings.CustomItems[instance.ToOres[item.skin]].Shortnames, UnityEngine.Random.Range(Settings.CustomItems[instance.ToOres[item.skin]].mincount, Settings.CustomItems[instance.ToOres[item.skin]].maxcount));
                                if (!newItem.MoveToContainer(storageContainer.inventory))
                                {
                                    newItem.Drop(oven.transform.position, Vector3.up);
                                }
                            });
                        }
                    }
                }
                timertick();
            }
        }
        #endregion
    }
}
