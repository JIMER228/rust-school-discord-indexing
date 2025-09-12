using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;

///Скачано с дискорд сервера Rust Edit [PRO+]
///discord.gg/9vyTXsJyKR

namespace Oxide.Plugins
{
    [Info("SoPaints", "TopPlugin.ru", "1.0.2")]
    public class SoPaints : RustPlugin
    {
        private ConfigData cfg { get; set; }

        private class ConfigData
        {
            [JsonProperty("Список ящиков где будет появ краска")] public List<string> _crateList = new List<string>();
            [JsonProperty("Мин кол-во выпадение")]public int MinDrop = 1;
            [JsonProperty("Макс кол-во выпадение")]public int MaxDrop = 2;
            [JsonProperty("Шанс выпадения")] public float shans = 50f;
            [JsonProperty("Скин краски")] public ulong skinId = 1916441035;
            [JsonProperty("Список скинов")] public Dictionary<string, List<ulong>> skinList = new Dictionary<string, List<ulong>>();
            
            public static ConfigData GetNewConf()
            {
                var newConfig = new ConfigData();
                newConfig._crateList = new List<string>()
                {
                    "crate_normal", "crate_elite"
                };
                newConfig.skinList = new Dictionary<string, List<ulong>>()
                {
                    ["rifle.ak"] = new List<ulong>()
                    {
                        849047662, 887494035, 1359893925, 1202410378, 1372945520, 859845460, 1259716979, 1826520371, 1750654242, 809212871, 809190373, 1435827815, 1112904406, 1385673487, 1679665505, 924020531, 1349512142, 937864743, 1196676446, 875130056, 1915393587, 1174389582, 1583542371, 1102750231, 840477492, 1306351416, 885146172, 885146172
                    },
                    ["rifle.bolt"] = new List<ulong>()
                    {
                        1852284996, 818403150, 1795984246, 1535660827, 1517933342, 1581664321, 933509449, 1687042408, 1587273896, 875259050, 972020573, 1592946955, 947954942, 1119629516, 1161165984, 840105253
                    },
                    ["rifle.semiauto"] = new List<ulong>()
                    {
                        1772028068, 828616457, 1195821858, 839302795, 1168002579, 1359059068, 1667097058, 1385736095, 1818125194, 1616628843, 1652791742, 1819195444, 1517644801, 900921542, 922119054, 1291766032,
                    },
                    ["door.double.hinged.toptier"] = new List<ulong>()
                    {
                        1911994581, 1874611109, 1925748582
                    }, 
                    ["door.hinged.toptier"] = new List<ulong>()
                    {
                        1228341388, 1206145767, 1114020299, 1402412287, 1414795168,930478674,1395469801,948938468,1557857999,869475498,1176460121,804286931,911652483,1477263064,1376526519,801889927,801937986,933057923,801831553,1605324677,839925176,809638761,807729959,1092678229,1135412861,885928673,
                    },
                    ["coffeecan.helmet"] = new List<ulong>()
                    {
                        848645884, 1797478191, 914060966, 1759479029, 1865208631,1342122459,1445131741,1121458604,1251411840,938020581,1332335200,1986043465,1349946203,955675586,1349166206,1129809202,1380023142,1740061403,1944168755,970583835,843676357,1269589560,1974807032,1154453278,1894381558,1174375607,1539575334,891592450,1438088592,1441850738,1743856800,1104118217,948491992,806212029,1151227603,1248435433,1539650632,974321420,1202978872,784910461,1388417865,1804649832,814098474,919595880,1906527802,1130589746,1400824309,1442169133,809816871,854460770
                    },
                    ["smg.2"] = new List<ulong>()
                    {
                        1329096680, 820350952, 820402694, 866745136, 1081305198,1128840196,931547202,1805101270,897099822,892212957,904964438,1597038037,1446184061,1961720552,1685722307,1198145190,1753609137,1114032911,1107572641,854914986,1523699528,822943156,970682025
                    },
                };
                return newConfig;
            }
        }

        protected override void LoadDefaultConfig()
        {
            cfg = ConfigData.GetNewConf();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(cfg);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                cfg = Config.ReadObject<ConfigData>();
            }
            catch
            { 
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }
        [ConsoleCommand("givepaints")]
        void AdminCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if(player != null && !player.IsAdmin) return;
            if (arg.Args == null || arg.Args.Length < 2)
            {
                Puts("givepaints STEAMID AMOUNT");
                if(player != null)
                    SendReply(player, "givepaints STEAMID AMOUNT");
                return;
            }
            ulong steamId;
            int amount;
            if(!int.TryParse(arg.Args[1], out amount)) return;
            if (!ulong.TryParse(arg.Args[0], out steamId))
            {
                Puts("Нужно вводить стим айди");
                if(player != null)
                    SendReply(player, "Нужно вводить стимайди");
                return;
            }
            var findPlayer = BasePlayer.FindByID(steamId);
            if (findPlayer == null)
            {
                Puts("Игрок не найден");
                if(player != null)
                    SendReply(player, "Игрок не найден");
                return; 
            }
            var item = ItemManager.CreateByName("sticks", amount, cfg.skinId);
            item.name = "Краска";
            if (!findPlayer.inventory.GiveItem(item))
                item.Drop(findPlayer.inventory.containerMain.dropPosition, findPlayer.inventory.containerMain.dropVelocity);
        }
        
        object OnLootSpawn(LootContainer container)
        {
            if (container == null) return null;
            if (!cfg._crateList.Contains(container.ShortPrefabName)) return null;
            if (Random.Range(0f, 100f) > cfg.shans) return null;
            var item = ItemManager.CreateByName("sticks", Random.Range(cfg.MinDrop, cfg.MaxDrop), cfg.skinId);
            item.name = "Краска";
            ItemContainer component1 = container.GetComponent<StorageContainer>().inventory;
            component1.itemList.Add(item);
            item.parent = component1;
            item.MarkDirty();
            
            return null;
        }
        private Item OnItemSplit(Item item, int amount)
        {
            if (amount <= 0) return null;
            if (item.skin != cfg.skinId) return null;
            item.amount -= amount;
            var newItem = ItemManager.Create(item.info, amount, item.skin);
            newItem.name = item.name;
            newItem.skin = item.skin;
            newItem.amount = amount;
            return newItem;
        } 
        private object CanCombineDroppedItem(WorldItem first, WorldItem second)
        {
            return CanStackItem(first.item, second.item);
        } 
        object CanStackItem(Item item, Item targetItem)
        {
            if (item == null || targetItem == null || item.GetOwnerPlayer() == null) return null;
            var targetPlayer = item.GetOwnerPlayer();
            List<ulong> f;
            if (targetItem.skin != cfg.skinId) return null;
            if (!cfg.skinList.TryGetValue(item.info.shortname, out f)) return null;
            var skin = f.Where(p => p != item.skin).ToList().GetRandom();
            item.skin = skin;
            targetItem.amount -= 1;
            if (targetItem.amount < 1) targetItem.RemoveFromContainer();
            var held = item.GetHeldEntity();
            if (held != null)
            {
                held.skinID = skin;
                held.SendNetworkUpdate();
            }
            targetPlayer.SendNetworkUpdate();
            Effect x = new Effect("assets/prefabs/misc/xmas/presents/effects/unwrap.prefab", targetPlayer, 0, new Vector3(), new Vector3()); 
            EffectNetwork.Send(x, targetPlayer.Connection);
            return false;
        } 
    }
}
