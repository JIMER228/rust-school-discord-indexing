using Facepunch;
using HarmonyLib;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Drop Bags", "Whispers88", "1.1.3")]
    [Description("Moves dropped items into bags")]
    public class DropBags : CovalencePlugin
    {
        private static string _drop;
        private static Dictionary<int, List<Item>> _itemDrops;
        private static List<DroppedItemContainer> _dropContainers;
        private static string _itemDropBackPack;
        private static int layermask;
        private static Vector3 _moveup;
        private static float areasize = 3f;
        private static bool vischeck = true;
        private static int maxdrops = 5;
        private static float overidedespawntime = 0f;
        private static bool calcdespawn = true;
        private static bool resetonloot = false;


        private void Unload()
        {
            _itemDrops = null;
            _dropContainers = null;
        }

        #region Config
        private Configuration config;
        private void Init()
        {
            areasize = config.areasize;
            vischeck = config.vischeck;
            maxdrops = config.maxdrops;
            overidedespawntime = config.overidedespawntime;
            calcdespawn = config.calcdespawn;
            resetonloot = config.resetOnLoot;

            //carbon things
            _dropContainers = new List<DroppedItemContainer>();
            _itemDrops = new Dictionary<int, List<Item>>();
            _moveup = new Vector3(0, 0.3f, 0);
            layermask = LayerMask.GetMask("Construction", "Deployed", "World");
            _itemDropBackPack = "assets/prefabs/misc/item drop/item_drop.prefab";
            _drop = "drop";
        }

        class Configuration
        {
            [JsonProperty("Area Size - How far away items can be moved into a container")]
            public int areasize = 7;

            [JsonProperty("Max drops before moving items to container")]
            public int maxdrops = 5;

            [JsonProperty("Time to wait before moving items")]
            public float waittime = 5;

            [JsonProperty("Vischeck - checks items are visable before moving to container")]
            public bool vischeck = true;

            [JsonProperty("Calculate despawntime from drop bag items")]
            public bool calcdespawn = true;

            [JsonProperty("Reset dropbag despawn time on loot")]
            public bool resetOnLoot = false;

            [JsonProperty("Override drop bag despawn time (seconds), 0 = false")]
            public float overidedespawntime = 0;

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() => config = new Configuration();
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }
                var configDict = config.ToDictionary();
                Dictionary<string, object> defaultconObjects = new Dictionary<string, object>();
                foreach (var obj in Config)
                {
                    defaultconObjects.Add(obj.Key, obj.Value);
                }
                if (configDict.Count != defaultconObjects.Count)
                {
                    Puts("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
                else
                {
                    foreach (var key in configDict.Keys)
                    {
                        if (defaultconObjects.ContainsKey(key))
                            continue;

                        Puts("Configuration appears to be outdated; updating and saving");
                        SaveConfig();
                        break;

                    }
                }
            }
            catch
            {
                Puts($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Puts($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion Config

        #region Hooks
        private void OnItemDropped(Item item, BaseEntity worldEntity)
        {
            int simplepos = Simplify(worldEntity.transform.position);

            if (!_itemDrops.TryGetValue(simplepos, out List<Item>? items))
            {
                items = Pool.Get<List<Item>>();
                _itemDrops[simplepos] = items;
            }
            items.Add(item);

            if (items.Count < config.maxdrops)
                return;

            if (ServerMgr.Instance.IsInvoking(DroppedItemsCleanup))
                return;

            ServerMgr.Instance.Invoke(DroppedItemsCleanup, config.waittime);
        }
        #endregion Hooks

        #region Core

        private static void DroppedItemsCleanup()
        {
            List<int> keysTocheck = Pool.Get<List<int>>();
            foreach (var itemDrop in _itemDrops)
            {
                for (int i = itemDrop.Value.Count - 1; i >= 0; i--)
                {
                    Item item = itemDrop.Value[i];
                    if (item?.info == null || item.parent != null || !item.IsDroppedInWorld(true))
                    {
                        itemDrop.Value.RemoveAt(i);
                    }
                }

                if (itemDrop.Value.Count < 1)
                {
                    keysTocheck.Add(itemDrop.Key);
                    continue;
                }

                if (itemDrop.Value.Count > maxdrops)
                {
                    for (int k = itemDrop.Value.Count - 1; k >= 0; k--)
                    {
                        Item item = itemDrop.Value[k];
                        if (item?.info == null)
                        {
                            itemDrop.Value.RemoveAt(k);
                            continue;
                        }
                        BaseEntity entity = item.GetWorldEntity();
                        if (entity == null)
                        {
                            itemDrop.Value.RemoveAt(k);
                            continue;
                        }
                        StoreInBag(entity, itemDrop.Key);
                        keysTocheck.Add(itemDrop.Key);
                        break;
                    }
                }
            }

            foreach (var key in keysTocheck)
            {
                if (!_itemDrops.TryGetValue(key, out var itemDrops))
                    continue;

                if (itemDrops.Count < 1)
                {
                    Pool.FreeUnmanaged<Item>(ref itemDrops);
                    _itemDrops.Remove(key);
                }
            }
            Pool.FreeUnmanaged<int>(ref keysTocheck);
        }

        private static void StoreInBag(BaseEntity positionEnt, int key)
        {
            if (!_itemDrops.TryGetValue(key, out List<Item>? itemslist) || itemslist == null)
                return;


            //check current containers
            for (int i = _dropContainers.Count - 1; i >= 0; i--)
            {
                DroppedItemContainer droppedItemContainer = _dropContainers[i];
                if (droppedItemContainer == null || droppedItemContainer.inventory.itemList.Count == 36)
                {
                    _dropContainers.RemoveAt(i);
                    continue;
                }

                //check if containers are near by
                if (Simplify(droppedItemContainer.transform.position) != Simplify(positionEnt.transform.position))
                    continue;

                Vector3 containerpos = droppedItemContainer.transform.position + _dispUp;

                for (int j = itemslist.Count - 1; j >= 0; j--)
                {
                    if (droppedItemContainer.inventory.itemList.Count >= 36)
                        break;

                    Item item = itemslist[j];

                    if (item == null || item.parent != null || item.GetWorldEntity() is not BaseEntity baseent)
                    {
                        itemslist.RemoveAt(j);
                        continue;
                    }
                    if (vischeck && !Vischeck(containerpos, baseent.transform.position))
                    {
                        continue;
                    }

                    AddItemToBackpack(droppedItemContainer, item);
                    itemslist.RemoveAt(j);
                }

                if (droppedItemContainer.inventory.itemList.Count >= 36)
                    _dropContainers.RemoveAt(i);

                CalcDespawnTime(droppedItemContainer);
                droppedItemContainer.inventory.MarkDirty();

                //if no items left return
                if (itemslist.Count < 1)
                {
                    return;
                }

            }

            Vector3 posvec = positionEnt.transform.position;
            int maxLoops = itemslist.Count;
            for (int k = 0; k < maxLoops; k++)
            {
                if (itemslist.Count < 1)
                    return;

                //create new container when none are available
                ItemContainer itemContainer = CreateContainer();

                for (int j = itemslist.Count - 1; j >= 0 && itemContainer.itemList.Count < 36; j--)
                {
                    Item item = itemslist[j];
                    if (item == null || item.parent != null || item.GetWorldEntity() is not BaseEntity baseent)
                    {
                        itemslist.RemoveAt(j);
                        continue;
                    }
                    if (vischeck && !Vischeck(posvec, baseent.transform.position))
                    {
                        continue;
                    }
                    AddItemToBackpack(itemContainer, item);
                    itemslist.RemoveAt(j);
                }
                if (itemContainer.itemList.Count < 1)
                {
                    itemContainer.Kill();
                    continue;
                }
                DroppedItemContainer backpack = SpawnItemContainer(posvec, itemContainer);

                if (itemContainer.itemList.Count >= 36)
                    _dropContainers.Remove(backpack);
            }

        }
        #endregion Core

        #region Helpers

        private static Vector3 _dispUp = Vector3.up * 0.2f;
        private static bool Vischeck(Vector3 pos, Vector3 baseent)
        {
            baseent += _dispUp;
            float dist = Vector3.Distance(baseent, pos);

            if (dist < 0.5)
                return true;

            if (!Physics.Raycast(pos, (baseent - pos).normalized, dist, layermask))
                return true;

            return false;
        }

        private static void AddItemToBackpack(DroppedItemContainer container, Item item)
        {
            if (item?.info == null)
            {
                return;
            }
            item.MoveToContainer(container.inventory);
        }

        private static void AddItemToBackpack(ItemContainer container, Item item)
        {
            if (item?.info == null)
            {
                return;
            }
            item.MoveToContainer(container);
        }

        public static int Simplify(Vector3 vector)
        {
            float x = Mathf.Round(vector.x / areasize);
            float y = Mathf.Round(vector.y / areasize);
            float z = Mathf.Round(vector.z / areasize);
            return (int)x << 22 | (int)y << 11 | (int)z;
        }

        private static ItemContainer CreateContainer()
        {
            ItemContainer itemContainer = new ItemContainer();
            itemContainer.ServerInitialize(null, 36);
            itemContainer.GiveUID();
            return itemContainer;
        }

        private static DroppedItemContainer SpawnItemContainer(Vector3 position, ItemContainer itemContainer)
        {
            DroppedItemContainer droppedItemContainer = (DroppedItemContainer)GameManager.server.CreateEntity(_itemDropBackPack, position + _dispUp);
            droppedItemContainer.Spawn();
            itemContainer.entityOwner = droppedItemContainer;
            _dropContainers.Add(droppedItemContainer);
            droppedItemContainer.OwnerID = 1234;
            droppedItemContainer.inventory = itemContainer;
            droppedItemContainer.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);
            CalcDespawnTime(droppedItemContainer);
            return droppedItemContainer;
        }

        private static void CalcDespawnTime(DroppedItemContainer droppedItemContainer)
        {
            if (overidedespawntime != 0)
            {
                droppedItemContainer.ResetRemovalTime(overidedespawntime);
            }
            else if (calcdespawn)
            {
                float itemdespawnQuick = 10f;
                foreach (Item item in droppedItemContainer.inventory.itemList)
                {
                    itemdespawnQuick = Mathf.Max(itemdespawnQuick, item.GetDespawnDuration());
                }
                droppedItemContainer.ResetRemovalTime(Mathf.Min(itemdespawnQuick, 3600f)); //making this an hour max
            }
            if (droppedItemContainer.inventory.itemList.Count < 1)
            {
                droppedItemContainer.ResetRemovalTime(5f);
            }
        }

        [HarmonyPatch(typeof(DroppedItemContainer), "OnStartBeingLooted"), AutoPatch]
        private static class DroppedItemContainer_OnStartBeingLooted_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(DroppedItemContainer __instance, BasePlayer baseEntity, ref bool __result)
            {
                if (__instance.OwnerID != 1234)
                    return true;

                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(DroppedItemContainer), "ResetRemovalTime", new Type[] { }), AutoPatch]
        private static class DroppedItemContainer_ResetRemovalTime_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(DroppedItemContainer __instance)
            {
                if (__instance.OwnerID != 1234)
                    return true;

                if (!resetonloot)
                    return false;

                __instance.CancelInvoke(new Action(__instance.RemoveMe));
                CalcDespawnTime(__instance);
                return false;
            }
        }
        #endregion Helpers
    }
}
