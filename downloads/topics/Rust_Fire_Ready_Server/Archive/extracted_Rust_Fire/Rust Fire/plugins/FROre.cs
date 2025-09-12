using System.Collections.Generic;
using Newtonsoft.Json;
using Random = Oxide.Core.Random;
using System.Linq;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("FROre", "fermens", "0.1.0")]
    [Description("^^")]
    public class FROre : RustPlugin
    {
        #region -0-
        private static PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            config = PluginConfig.DefaultConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        class RADIATION
        {
            [JsonProperty("Радиус")]
            public float radius;

            [JsonProperty("Количество")]
            public float amount;
        }

        class XITEM
        {
            [JsonProperty("Префаб")]
            public string prefabname;

            [JsonProperty("Название")]
            public string name;

            [JsonProperty("Вероятность выпадения [0.0-1.0]")]
            public float shance;

            [JsonProperty("Количество [минимальное]")]
            public int minamount;

            [JsonProperty("Количество [максимальное]")]
            public int maxamount;

            [JsonProperty("Скин")]
            public ulong skin;
        }

        class ACTION
        {
            [JsonProperty("Действия")]
            public List<string> act;

            [JsonProperty("Действие выполенено определенными предметами [prefab: skinid]")]
            public Dictionary<string, ulong> uNITEMS;

            [JsonProperty("Вероятность срабатывания [0.0-1.0]")]
            public float shance;

            [JsonProperty("В получаемых предметах может выпасть только один предмет?")]
            public bool drop;

            [JsonProperty("Радиация")]
            public RADIATION rADIATION;

            [JsonProperty("Получаемые предметы")]
            public List<XITEM> xITEM;

            [JsonProperty("Выполняемая команда")]
            public string command;
        }

        private class PluginConfig
        {
            [JsonProperty("Список")]
            public List<ACTION> actions;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    actions = new List<ACTION>
                    {
                        new ACTION{ act = new List<string> { { "crush" } }, command = null, shance = 1f, uNITEMS = new Dictionary<string, ulong>{ { "skull.human", 1687324896 } }, xITEM = new List<XITEM> { new XITEM{ name = "Кринжатий", prefabname = "sulfur.ore", skin = 2078469880, minamount = 1, maxamount = 3 } }, rADIATION = new RADIATION{ amount= 10, radius = 10} },
                        new ACTION{ act = new List<string> { { "gatherore" }, {"sulfur-ore"}, { "metal-ore" } }, command = null, shance = 0.75f, uNITEMS = new Dictionary<string, ulong>{ { "pickaxe", 0 }, { "stone.pickaxe", 0 } }, xITEM = new List<XITEM> { new XITEM{ name = "Дедпул", prefabname = "skull.human", skin = 1687324896, minamount = 1, maxamount = 1 } }, rADIATION = new RADIATION{ amount= 10, radius = 10} },
                        new ACTION{ act = new List<string> { { "recycle" } }, command = null, rADIATION = new RADIATION{ amount = 5f, radius = 7f }, shance = 0.75f, uNITEMS = new Dictionary<string, ulong>{ { "can.beans.empty", 0 } }, xITEM = new List<XITEM> { new XITEM{ name = "БипБуп", prefabname = "glue", skin = 1926772293, minamount = 1, maxamount = 2 }, new XITEM { name = "БапБип", prefabname = "glue", skin = 1926660666, minamount = 1, maxamount = 4 } } }
                    }
                };
            }
        }
        #endregion

        #region -1-
        private bool dispencer = true;
        private bool bonus = true;
        private bool recycle = true;
        private bool kill = true;
        private bool action = true;

        private void OnInit()
        {
            if (dispencer)
            {
                Unsubscribe(nameof(OnDispenserGather));
                dispencer = false;
            }
            if (bonus)
            {
                Unsubscribe(nameof(OnDispenserBonus));
                bonus = false;
            }
            if (recycle)
            {
                Unsubscribe(nameof(OnRecycleItem));
                recycle = false;
            }
            if (kill)
            {
                Unsubscribe(nameof(OnEntityDeath));
                kill = false;
            }
            if (action)
            {
                Unsubscribe(nameof(OnItemAction));
                action = false;
            }
        }

        private void OnServerInitialized()
        {
            _unicalitems.Clear();
            _gatherore.Clear();
            _gathertree.Clear();
            _gatherflash.Clear();

            _bounsore.Clear();
            _bounstree.Clear();
            _bounsflash.Clear();
            _recycle.Clear();
            _kill.Clear();
            _melt.Clear();
            _crush.Clear();

            bool Xdispencer = false;
            bool Xbonus = false;
            bool Xrecycle = false;
            bool Xkill = false;
            bool Xaction = false;

            foreach (var z in config.actions)
            {
                if (z.rADIATION == null)
                {
                    z.rADIATION = new RADIATION { amount = 0f, radius = 0f };
                }
                if (z.act.Contains("gatherore"))
                {
                    _gatherore.Add(CreateDispencer(z.act, z.command, z.xITEM, z.shance, z.uNITEMS, z.rADIATION, z.drop));
                    Xdispencer = true;
                }
                if (z.act.Contains("gathertree"))
                {
                    _gathertree.Add(CreateDispencer(z.act, z.command, z.xITEM, z.shance, z.uNITEMS, z.rADIATION, z.drop));
                    Xdispencer = true;
                }
                if (z.act.Contains("gatherflash"))
                {
                    _gatherflash.Add(CreateDispencer(z.act, z.command, z.xITEM, z.shance, z.uNITEMS, z.rADIATION, z.drop));
                    Xdispencer = true;
                }

                if (z.act.Contains("bounsore"))
                {
                    _bounsore.Add(CreateDispencer(z.act, z.command, z.xITEM, z.shance, z.uNITEMS, z.rADIATION, z.drop));
                    Xbonus = true;
                }
                if (z.act.Contains("bounstree"))
                {
                    _bounstree.Add(CreateDispencer(z.act, z.command, z.xITEM, z.shance, z.uNITEMS, z.rADIATION, z.drop));
                    Xbonus = true;
                }
                if (z.act.Contains("bounsflash"))
                {
                    _bounsflash.Add(CreateDispencer(z.act, z.command, z.xITEM, z.shance, z.uNITEMS, z.rADIATION, z.drop));
                    Xbonus = true;
                }

                if (z.act.Contains("recycle"))
                {
                    _recycle.Add(CreateDispencer(z.act, z.command, z.xITEM, z.shance, z.uNITEMS, z.rADIATION, z.drop));
                    Xrecycle = true;
                }

                if (z.act.Contains("kill"))
                {
                    _kill.Add(CreateDispencer(z.act, z.command, z.xITEM, z.shance, z.uNITEMS, z.rADIATION, z.drop));
                    Xkill = true;
                }

                if (z.act.Contains("crush"))
                {
                    _crush.Add(CreateDispencer(z.act, z.command, z.xITEM, z.shance, z.uNITEMS, z.rADIATION, z.drop));
                    Xaction = true;
                }

                if (z.act.Contains("melt"))
                {
                    _melt.Add(CreateDispencer(z.act, z.command, z.xITEM, z.shance, z.uNITEMS, z.rADIATION, z.drop));
                }

                if (z.xITEM.Count > 0)
                {
                    foreach (var x in z.xITEM)
                    {
                        if (x.shance == 0f) x.shance = 1f;
                        if (x.skin != 0 && !_unicalitems.Contains(x.skin)) _unicalitems.Add(x.skin);
                    }
                }
            }
            if (Xdispencer && !dispencer)
            {
                Unsubscribe(nameof(OnDispenserGather));
                dispencer = true;
            }
            if (Xbonus && !bonus)
            {
                Unsubscribe(nameof(OnDispenserBonus));
                bonus = true;
            }
            if (Xrecycle && !recycle)
            {
                Unsubscribe(nameof(OnRecycleItem));
                recycle = true;
            }
            if (Xkill && !recycle)
            {
                Subscribe(nameof(OnEntityDeath));
                kill = true;
            }
            if (Xaction && !action)
            {
                Subscribe(nameof(OnItemAction));
                action = true;
            }
        }

        private void Unload()
        {

        }
        #endregion

        #region -2-
        List<ulong> _unicalitems = new List<ulong>();
        List<DISPENCER> _gatherore = new List<DISPENCER>();
        List<DISPENCER> _gathertree = new List<DISPENCER>();
        List<DISPENCER> _gatherflash = new List<DISPENCER>();

        List<DISPENCER> _bounsore = new List<DISPENCER>();
        List<DISPENCER> _bounstree = new List<DISPENCER>();
        List<DISPENCER> _bounsflash = new List<DISPENCER>();

        List<DISPENCER> _recycle = new List<DISPENCER>();

        List<DISPENCER> _kill = new List<DISPENCER>();

        List<DISPENCER> _melt = new List<DISPENCER>();

        List<DISPENCER> _crush = new List<DISPENCER>();

        class DISPENCER
        {
            public Dictionary<string, ulong> uNITEMS;
            public bool skip;
            public bool drop;
            public List<string> act;
            public float shance;
            public RADIATION rADIATION;
            public string command;
            public List<XITEM> iTEM;
        }

        private object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (action == "crush")
            {
                bool fal = false;
                string activeitem = item.info.shortname;
                ulong activeskin = item.skin;

                foreach (var z in _crush)
                {
                    if (z.uNITEMS.Count > 0)
                    {
                        if (!z.uNITEMS.Any(x => x.Key == activeitem && x.Value == activeskin)) continue;
                    }

                    float rand = Random.Range(0f, 1f);
                    if (rand > z.shance) continue;
                    fal = true;
                    if (!string.IsNullOrEmpty(z.command)) Server.Command(z.command.Replace("{steamid}", player.UserIDString));

                    if (z.rADIATION.amount > 0f)
                    {
                        List<BasePlayer> basePlayers = new List<BasePlayer>();
                        Vis.Entities<BasePlayer>(player.transform.position, z.rADIATION.radius, basePlayers);
                        foreach (var x in basePlayers)
                        {
                            x.UpdateRadiation(z.rADIATION.amount);
                        }
                    }

                    float rand3 = Random.Range(0f, 1f);

                    if (z.iTEM.Count > 0)
                    {
                        foreach (var x in z.iTEM)
                        {
                            float rand2 = Random.Range(0f, 1f);
                            if (!z.drop && rand2 > x.shance || z.drop && rand3 > x.shance) continue;
                            int amount = x.minamount != x.maxamount ? Random.Range(x.minamount, x.maxamount + 1) : x.minamount;
                            Item ditem = CreateItem(x.prefabname, x.name, x.skin, amount);
                            player.GiveItem(ditem);
                            if (z.drop) break;
                        }
                    }
                }

                if (fal)
                {
                    item.amount -= 1;
                    if (item.amount <= 0) item.Remove(0f);
                    return false;
                }
            }

            return null;
        }


        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            BasePlayer player = entity.ToPlayer();
            if (player == null) return;
            if (dispenser.gatherType == ResourceDispenser.GatherType.Ore) Gathering(dispenser._baseEntity.ShortPrefabName, player, _gatherore);
            else if (dispenser.gatherType == ResourceDispenser.GatherType.Tree) Gathering(dispenser._baseEntity.ShortPrefabName, player, _gathertree);
            else Gathering(dispenser._baseEntity.ShortPrefabName, player, _gatherflash);
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser.gatherType == ResourceDispenser.GatherType.Ore) Gathering(dispenser._baseEntity.ShortPrefabName, player, _bounsore);
            else if (dispenser.gatherType == ResourceDispenser.GatherType.Tree) Gathering(dispenser._baseEntity.ShortPrefabName, player, _bounstree);
            else Gathering(dispenser._baseEntity.ShortPrefabName, player, _bounsflash);
        }

        private object OnRecycleItem(Recycler recycler, Item item)
        {
            string shortname = item.info.shortname;
            ulong skin = item.skin;
            foreach (var z in _recycle)
            {
                if (z.uNITEMS.Count > 0)
                {
                    if (!z.uNITEMS.Any(x => x.Key == shortname && x.Value == skin)) continue;
                }

                item.amount -= 1;

                if (item.amount <= 0) item.Remove(0f);
                else item.MarkDirty();

                float rand = Random.Range(0f, 1f);
                if (rand > z.shance) return true;

                if (z.rADIATION.amount > 0f)
                {
                    List<BasePlayer> basePlayers = new List<BasePlayer>();
                    Vis.Entities<BasePlayer>(recycler.transform.position, z.rADIATION.radius, basePlayers);
                    foreach (var x in basePlayers)
                    {
                        x.UpdateRadiation(z.rADIATION.amount);
                    }
                }

                if (z.iTEM.Count > 0)
                {
                    float rand3 = Random.Range(0f, 1f);

                    foreach (var x in z.iTEM)
                    {
                        float rand2 = Random.Range(0f, 1f);
                        if (!z.drop && rand2 > x.shance || z.drop && rand3 > x.shance) continue;
                        int amount = x.minamount != x.maxamount ? Random.Range(x.minamount, x.maxamount + 1) : x.minamount;
                        Item ditem = CreateItem(x.prefabname, x.name, x.skin, amount);
                        recycler.MoveItemToOutput(ditem);
                        if (z.drop) break;
                    }
                }
                return true;
            }
            return null;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;
            else if (info.Initiator is BasePlayer)
            {
                Gathering(entity.PrefabName, info.InitiatorPlayer, _kill, entity.transform.position);
            }
        }

        private void Gathering(string shortname, BasePlayer player, List<DISPENCER> dISPENCERs, Vector3 vector3 = default(Vector3))
        {
            Item active = player.GetActiveItem();
            string activeitem = active != null ? active.info.shortname : "null";
            ulong activeskin = active != null ? active.skin : 0UL;
            foreach (var z in dISPENCERs)
            {
                if (z.skip || z.act.Contains(shortname))
                {
                    if (z.uNITEMS.Count > 0)
                    {
                        if (!z.uNITEMS.Any(x => x.Key == activeitem && x.Value == activeskin)) continue;
                    }

                    float rand = Random.Range(0f, 1f);
                    if (rand > z.shance) continue;
                    if (!string.IsNullOrEmpty(z.command)) Server.Command(z.command.Replace("{steamid}", player.UserIDString));

                    if (z.rADIATION.amount > 0f)
                    {
                        List<BasePlayer> basePlayers = new List<BasePlayer>();
                        Vis.Entities<BasePlayer>(player.transform.position, z.rADIATION.radius, basePlayers);
                        foreach (var x in basePlayers)
                        {
                            x.UpdateRadiation(z.rADIATION.amount);
                        }
                    }

                    if (z.iTEM.Count > 0)
                    {
                        float rand3 = Random.Range(0f, 1f);
                        foreach (var x in z.iTEM)
                        {
                            float rand2 = Random.Range(0f, 1f);
                            if (!z.drop && rand2 > x.shance || z.drop && rand3 > x.shance) continue;
                            int amount = x.minamount != x.maxamount ? Random.Range(x.minamount, x.maxamount + 1) : x.minamount;
                            Item ditem = CreateItem(x.prefabname, x.name, x.skin, amount);
                            if (vector3 == default(Vector3))
                            {
                                player.GiveItem(ditem);
                            }
                            else
                            {
                                ItemContainer container2 = new ItemContainer();
                                container2.Insert(ditem);
                                DropUtil.DropItems(container2, vector3);
                            }
                            if (z.drop) break;
                        }
                    }
                }
            }
        }
        #endregion

        #region -3-
        class CREAP
        {
            public string prefabname;
            public string name;
            public ulong skin;
            public int amount;
        }

        private List<Item> GetMelt(string shortname, ulong skin)
        {
            List<Item> cREAPs = new List<Item>();
            foreach (var z in _melt)
            {
                if (z.uNITEMS.Count == 0 || !z.uNITEMS.Any(x => x.Key == shortname && x.Value == skin)) continue;

                float rand = Random.Range(0f, 1f);
                if (rand > z.shance || z.iTEM.Count == 0) continue;

                float rand3 = Random.Range(0f, 1f);

                foreach (var x in z.iTEM)
                {
                    float rand2 = Random.Range(0f, 1f);
                    if (!z.drop && rand2 > x.shance || z.drop && rand3 > x.shance) continue;
                    int amount = x.minamount != x.maxamount ? Random.Range(x.minamount, x.maxamount + 1) : x.minamount;
                    var item2 = ItemManager.CreateByName(x.prefabname, amount);
                    item2.name = x.name;
                    item2.skin = x.skin;
                    if (z.rADIATION.amount > 0f) item2.text = $"{z.rADIATION.amount} {z.rADIATION.radius}";
                    cREAPs.Add(item2);
                    if (z.drop) break;
                }
            }
            return cREAPs;
        }

        private Item CreateItem(string prefab, string name, ulong skin, int amount)
        {
            Item item = ItemManager.CreateByName(prefab);
            item.name = name;
            item.skin = skin;
            item.amount = amount;
            item.MarkDirty();
            return item;
        }

        private DISPENCER CreateDispencer(List<string> act, string command, List<XITEM> xITEM, float shance, Dictionary<string, ulong> uNITEMS, RADIATION rADIATION, bool Drop)
        {
            List<XITEM> xRITEM = new List<XITEM>();
            xRITEM.AddRange(xITEM);

            if (Drop)
            {

                float total = xRITEM.Sum(x => x.shance);
                float last = 0f;
                int count = xRITEM.Count;
                foreach (var z in xRITEM)
                {
                    count--;
                    float result = count == 0 ? 1 : z.shance / total + last;
                    last = result;
                    z.shance = result;
                }
            }

            return new DISPENCER { act = act, skip = act.Contains("all"), command = command, iTEM = xRITEM, shance = shance, uNITEMS = uNITEMS, rADIATION = rADIATION, drop = Drop };
        }
        #endregion

        #region -4-
        object CanStackItem(Item item, Item targetItem)
        {
            if (item.skin == targetItem.skin) return null;
            if (_unicalitems.Contains(item.skin) || _unicalitems.Contains(targetItem.skin)) return false;
            return null;
        }

        object CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
        {
            if (item.item.skin == targetItem.item.skin) return null;
            if (_unicalitems.Contains(item.item.skin) || _unicalitems.Contains(targetItem.item.skin)) return false;
            return null;
        }

        private Item OnItemSplit(Item item, int amount)
        {
            if (_unicalitems.Contains(item.skin))
            {
                Item newitem = ItemManager.Create(item.info);
                newitem.name = item.name;
                newitem.skin = item.skin;
                newitem.amount = amount;

                item.amount -= amount;
                item.MarkDirty();

                return newitem;
            }
            return null;
        }
        #endregion
    }
}
