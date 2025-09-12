using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Oxide.Plugins {
    [Info("NPC", "Egor Blagov", "1.5.4")]
    [Description("Spawns and configures enemy NPCs")]
    class NPC : RustPlugin {
        static NPC Instance;
        const float HeightToRaycast = 250;
        const float RaycastDistance = 500;
        const float RaycastDiffThreshold = 0.4f;
        const int SpawnTrials = 150;
        const string perm = "npc.adm";
        private const float SupplyRadius = 30.0f;

        enum NPCType { Murderer, Scarecrow, Scientist, Military };
        static Dictionary<NPCType, string> prefabs = new Dictionary<NPCType, string> {
            [NPCType.Murderer] = "assets/prefabs/npc/murderer/murderer.prefab",
            [NPCType.Scarecrow] = "assets/prefabs/npc/scarecrow/scarecrow.prefab",
            [NPCType.Scientist] = "assets/prefabs/npc/scientist/scientist.prefab",
            [NPCType.Military] = "assets/prefabs/npc/scientist/htn/scientist_full_mp5.prefab"
        };
        static Dictionary<string, string> lootPrefabs = new Dictionary<string, string> {
            ["crate_elite"] = "assets/bundled/prefabs/radtown/crate_elite.prefab",
            ["crate_normal"] = "assets/bundled/prefabs/radtown/crate_normal.prefab",
            ["crate_normal_2"] = "assets/bundled/prefabs/radtown/crate_normal_2.prefab",
            ["crate_basic"] = "assets/bundled/prefabs/radtown/crate_basic.prefab",
            ["crate_tools"] = "assets/bundled/prefabs/radtown/crate_tools.prefab",
            ["supply_drop"] = "assets/prefabs/misc/supply drop/supply_drop.prefab",
            ["bradley_crate"] = "assets/prefabs/npc/m2bradley/bradley_crate.prefab",
            ["heli_crate"] = "assets/prefabs/npc/patrol helicopter/heli_crate.prefab",
            ["loot_component_test"] = "assets/bundled/prefabs/radtown/loot_component_test.prefab",
            ["presentdrop"] = "assets/prefabs/misc/xmas/sleigh/presentdrop.prefab"
        };
        private Dictionary<uint, BaseCombatEntity> spawnedNpcs = new Dictionary<uint, BaseCombatEntity>();
        private Dictionary<uint, NonUserSupplyDrop> nonUserSupplyDrops = new Dictionary<uint, NonUserSupplyDrop>();
        private List<ExpectedCorpse> expectedCorpses = new List<ExpectedCorpse>();
        private List<GameObject> availableMonuments = new List<GameObject>();
        private LootGenerator lootGenerator = new LootGenerator();
        private List<Vector3> supplyDrops = new List<Vector3>();
        class NonUserSupplyDrop {
            public List<NPCController> npcs = new List<NPCController>();
        }
        class UserSupplyDrop {
            static private int nextId = 1;
            public int id = nextId++;
            public DateTime SupplySignalMaxStamp;

            public uint SupplySignalID = 0;
            public uint SupplyDropID = 0;

            public bool EqualDrop(SupplyDrop drop) {
                return drop.net.ID == SupplyDropID;
            }

            [JsonIgnore]
            public CargoPlane CargoPlane;

            [JsonIgnore]
            public bool IsInvalid => SupplySignalMaxStamp < DateTime.Now && CargoPlane == null && SupplyDropID == 0;

        }
        class StoredData {
            public List<UserSupplyDrop> userSupplyDrops = new List<UserSupplyDrop>();
            public bool DropIsHandled(SupplyDrop supplyDrop) {
                if (userSupplyDrops.Find(u => u.EqualDrop(supplyDrop)) != null) {
                    return true;
                }

                foreach (var drop in userSupplyDrops) {
                    if (drop.CargoPlane != null && drop.CargoPlane.transform.position == supplyDrop.transform.position) {
                        drop.SupplyDropID = supplyDrop.net.ID;
                        return true;
                    }
                }

                return false;
            }

            public void HandleSupplySignal(SupplySignal supplySignal) {
                userSupplyDrops.Add(new UserSupplyDrop {
                    SupplySignalID = supplySignal.net.ID,
                    SupplySignalMaxStamp = DateTime.Now.AddSeconds(supplySignal.timerAmountMax + 4.0f)
                });
            }

            public void HandleCargoPlane(CargoPlane cargoPlane) {
                userSupplyDrops.RemoveAll(dr => dr.IsInvalid);
                UserSupplyDrop nonCalledDrop = null;
                foreach (var drop in userSupplyDrops) {
                    if (drop.CargoPlane != null || drop.SupplyDropID != 0) {
                        continue;
                    }
                    nonCalledDrop = drop;
                    break;
                }
                    
                if (nonCalledDrop == null) {
                    return;
                }

                nonCalledDrop.CargoPlane = cargoPlane;
            }

            public void OnKillSupplyDrop(SupplyDrop supplyDrop) {
                userSupplyDrops.RemoveAll(dr => dr.EqualDrop(supplyDrop));
            }

            public void CleanupSupplyDrops() {
                List<SupplyDrop> actualDrops = Facepunch.Pool.GetList<SupplyDrop>();
                foreach (var ent in BaseNetworkable.serverEntities) {
                    if (ent is SupplyDrop) {
                        actualDrops.Add(ent as SupplyDrop);
                    }
                }

                userSupplyDrops.RemoveAll(dr => actualDrops.FindIndex(actualDrop => dr.EqualDrop(actualDrop)) == -1);
                Facepunch.Pool.FreeList(ref actualDrops);
            }
        }

        private StoredData storedData;
        #region LOOT HANDLING
        class LootGenerator {
            private Dictionary<string, LootSpawner> spawners = new Dictionary<string, LootSpawner>();
            public LootSpawner get(string name) {
                if (!lootPrefabs.ContainsKey(name)) {
                    Instance.PrintError($"Unknown crate type: {name}");
                    return null;
                }

                if (!spawners.ContainsKey(name)) {
                    spawners[name] = new LootSpawner(lootPrefabs[name]);
                }

                if (!spawners[name].isContainerOk) {
                    spawners[name].Cleanup();
                    spawners[name] = new LootSpawner(lootPrefabs[name]);
                }

                return spawners[name];
            }

            public void Cleanup() {
                foreach (var spawner in spawners.Values) {
                    spawner.Cleanup();
                }
                spawners.Clear();
                spawners = null;
            }
        }
        class LootSpawner {
            private string fullPrefabName;
            private LootContainer lootContainer;
            public bool isContainerOk {
                get {
                    return this.lootContainer != null && !this.lootContainer.IsDestroyed;
                }
            }

            public LootSpawner(string prefabName) {
                this.fullPrefabName = prefabName;
                this.lootContainer = GameManager.server.CreateEntity(this.fullPrefabName, new Vector3(), new Quaternion(), true) as LootContainer;
                this.lootContainer.Spawn();
                this.lootContainer.CancelInvoke(new Action(this.lootContainer.SpawnLoot));
            }

            public void Cleanup() {
                if (this.isContainerOk) {
                    this.lootContainer.Kill();
                }
            }

            public void SpawnLoot() {
                if (Instance.config.LootUpdatedByThirdPartyPlugins) {
                    this.lootContainer.SpawnLoot();
                } else {
                    this.lootContainer.inventory.Clear();
                    ItemManager.DoRemoves();
                    this.lootContainer.PopulateLoot();
                }
            }

            public ItemContainer inventory {
                get {
                    return this.lootContainer.inventory;
                }
            }
        }
        #endregion
        class ExpectedCorpse {
            public ulong id;
            public NPCPreset preset;
        }
        #region NPC DESCRIPTION
        class NPCItemDefinition {
            public string shortname;
            public ulong[] skin = { 0 };

            public virtual Item createItem() {
                ulong data2Inf_unknwn = 0;
                if (this.skin.Length > 0) {
                    data2Inf_unknwn = this.skin.GetRandom();
                }

                return ItemManager.CreateByName(shortname, 1, data2Inf_unknwn);
            }
        }

        class NPCItemSpendableDefinition : NPCItemDefinition {
            public int amount = 1;

            public override Item createItem() {
                var item = base.createItem();
                item.amount = amount;
                return item;
            }
        }

        class NPCExtraLootDefinition {
            public class Item {
                public string shortname;
                public uint skinId;
                public int minAmount;
                public int maxAmount;
                public float probability;
            }

            public Item[] items;

            public List<global::Item> GetItems() {
                List<global::Item> result = new List<global::Item>();
                foreach (var item in items) {
                    if (getRandom(item.probability)) {
                        result.Add(ItemManager.CreateByName(item.shortname, UnityEngine.Random.Range(item.minAmount, item.maxAmount + 1), item.skinId));
                    }
                }
                return result;
            }
        }

        class NPCLootDefinition {
            public string[] lootCrates = { };
            public uint crateItemsMin = 1;
            public uint crateItemsMax = 10;
            public NPCExtraLootDefinition extra;
            public string[] excludeList;
            public void spawnItemsToContainer(ItemContainer container) {
                clearContainer(container);

                if (lootCrates.Length == 0 && extra == null) {
                    return;
                }

                uint min = Math.Min(crateItemsMax, crateItemsMin);
                uint max = Math.Max(crateItemsMax, crateItemsMin);
                uint spawnedItems = 0;

                for (int k = 0; k < max; k++) { // not giving infinite cycle just in case
                    for (int i = 0; i < lootCrates.Length; i++) {
                        var spawner = Instance.lootGenerator.get(lootCrates[i]);
                        spawner.SpawnLoot();
                        while (spawner.inventory.itemList.Count > 0) {
                            var item = spawner.inventory.itemList[0];
                            if (excludeList != null && excludeList.Contains(item.info.shortname)) {
                                item.RemoveFromContainer();
                                item.Remove();
                                continue;
                            }
                            item.MoveToContainer(container);
                            spawnedItems++;
                            if (spawnedItems >= max) {
                                break;
                            }
                        }

                        if (spawnedItems >= max) {
                            break;
                        }
                    }

                    if (spawnedItems >= min) {
                        break;
                    }
                }

                if (extra != null) {
                    var extraItems = extra.GetItems();
                    foreach (var item in extraItems) {
                        item.MoveToContainer(container);
                    }
                }
            }
        }

        class NPCPreset {
            public string name;
            [JsonConverter(typeof(StringEnumConverter))]
            public NPCType type;
            public string displayName;

            public float accuracy = 1.0f;
            public float receivingDamageMultiplier = 1.0f;
            public float dealingDamageMultiplier = 1.0f;
            public float roamingRange = 40f;

            public NPCItemDefinition[] wear;
            public NPCItemSpendableDefinition[] belt;
            public NPCLootDefinition loot;

            [JsonIgnore]
            public string prefab {
                get {
                    return prefabs[this.type];
                }
            }

            public void InitPlayer(BaseCombatEntity npcEnt) {
                BasePlayer npcPlayer = npcEnt as BasePlayer;
                if (npcPlayer == null) {
                    throw new Exception("Unable to init player");
                }

                clearContainer(npcPlayer.inventory.containerBelt);
                clearContainer(npcPlayer.inventory.containerMain);
                clearContainer(npcPlayer.inventory.containerWear);
                npcPlayer.inventory.ServerUpdate(0f);

                wear.ToList().ForEach(npcItemDefintion => {
                    npcItemDefintion.createItem().MoveToContainer(npcPlayer.inventory.containerWear);
                });

                belt.ToList().ForEach(NPCItemDefinition => {
                    NPCItemDefinition.createItem().MoveToContainer(npcPlayer.inventory.containerBelt);
                });

                npcPlayer.displayName = displayName;

                //if (npcPlayer is NPCPlayerApex) {
                //    var npc = npcPlayer as NPCPlayerApex;
                //    npc.RadioEffect = new GameObjectRef();
                //}

                //if (npcPlayer is HTNPlayer) {
                //    var npc = npcPlayer as HTNPlayer;
                //    npc.AiDefinition.StopVoices(npc);
                //}
            }

            public NPCController spawn(Vector3 point, bool shouldRespawn=true) {
                BaseCombatEntity entity = InstantiateEntity(prefab, point);

                InitPlayer(entity);

                var npc = entity.gameObject.AddComponent<NPCController>();
                npc.SetInfo(name, point, roamingRange, shouldRespawn);
                Instance.spawnedNpcs.Add(entity.net.ID, entity);
                return npc;
            }

            private BaseCombatEntity InstantiateEntity(string prefab, Vector3 position) {
                var gameObject = Instantiate.GameObject(GameManager.server.FindPrefab(prefab), position, new Quaternion());
                gameObject.name = prefab;

                SceneManager.MoveGameObjectToScene(gameObject, Rust.Server.EntityScene);
                UnityEngine.Object.Destroy(gameObject.GetComponent<Spawnable>());

                if (!gameObject.activeSelf)
                    gameObject.SetActive(true);

                BaseCombatEntity component = gameObject.GetComponent<BaseCombatEntity>();
                component.enableSaving = false;
                component.Spawn();
                component.InitializeHealth(component.StartHealth(), component.StartMaxHealth());

                return component;
            }
        }
        public abstract class NPCSpawnRule {
            public int minCount = 1;
            public int maxCount = 4;
            public abstract void spawn();
            protected bool IsInTextures(Vector3 point) {
                var raycastStart = new Vector3(
                    point.x,
                    HeightToRaycast,
                    point.z
                );

                RaycastHit hitInfo;
                if (Physics.Raycast(raycastStart, Vector3.down, out hitInfo, RaycastDistance, Layers.Solid)) {

                    var res = Mathf.Abs(hitInfo.point.y - point.y) > RaycastDiffThreshold;
                    return res;
                }
                return true;
            }

            protected virtual int GetCount() {
                if (Instance.config.scaleNpcFromRulesToMaxCount) {
                    var minScaled = Mathf.RoundToInt(Instance.config.spawnCountFactor * (float)minCount);
                    var maxScaled = Mathf.RoundToInt(Instance.config.spawnCountFactor * (float)maxCount);
                    return UnityEngine.Random.Range(minScaled, maxScaled + 1);
                }

                return UnityEngine.Random.Range(minCount, maxCount + 1);
            }

            protected bool FindValidSpawnPoint(out Vector3 result) {
                for (int i = 0; i < SpawnTrials; i++) {
                    Vector3 randomPos = this.GetSpawnPos();

                    RaycastHit hitInfo;
                    if (Physics.Raycast(randomPos, Vector3.down, out hitInfo, RaycastDistance, Layers.Solid)) {
                        randomPos.y = hitInfo.point.y;
                    } else {
                        continue;
                    }

                    if (!this.PreNavmeshCheck(randomPos)) {
                        continue;
                    }

                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(randomPos, out hit, 50, 1)) {

                        if (hit.position.y - TerrainMeta.HeightMap.GetHeight(hit.position) > 3) {
                            continue;
                        }

                        if(WaterLevel.Test(hit.position))
                        {
                            continue;
                        }
                        if (IsInTextures(hit.position)) {
                            continue;
                        }


                        result = hit.position;
                        return true;
                    }
                }
                
                result = Vector3.zero;
                return false;
            }

            protected abstract bool PreNavmeshCheck(Vector3 randomPos);
            protected abstract Vector3 GetSpawnPos();
        }
        class NPCMonumentSpawnRule : NPCSpawnRule {
            public string monumentName;
            public string npcPresetName;

            public float minSpread = 0;
            public float maxSpread = 10;
            private List<Vector3> monumentPositions;
            private Vector3 currentMonumentPos;
            private bool inited = false;
            public override void spawn() {
                if (!inited) {
                    inited = true;
                    var monuments = Instance.availableMonuments.FindAll(g => g.name.Contains(this.monumentName));
                    if (monuments.Count == 0) {
                        return;
                    }
                    monumentPositions = monuments.Select(x => x.transform.position).ToList();
                }

                if (monumentPositions.Count == 0) {
                    return;
                }
                foreach (var monumentPosition in monumentPositions) {
                    currentMonumentPos = monumentPosition;
                    Vector3 point;
                    var count = this.GetCount();
                    int beforeSpawn = Instance.spawnedNpcs.Count;
                    for (int i = 0; i < count; i++) {
                        if (FindValidSpawnPoint(out point)) {
                            Instance.config.getNPCPreset(npcPresetName).spawn(point);
                            if (Instance.spawnedNpcs.Count() >= Instance.config.maxNpcs) {
                                return;
                            }
                        }
                    }
                    int spawnedCount = Instance.spawnedNpcs.Count - beforeSpawn;
                    if (count != spawnedCount) {
                        Instance.PrintWarning($"Unable to spawn {count} at {monumentName}, result is {spawnedCount}");
                    }
                }
                currentMonumentPos = new Vector3();
            }

            protected override Vector3 GetSpawnPos() {
                float angle = UnityEngine.Random.Range(0, Mathf.PI * 2);
                float radius = UnityEngine.Random.Range(minSpread, maxSpread);
                float x = radius * Mathf.Cos(angle);
                float y = radius * Mathf.Sin(angle);
                return currentMonumentPos + new Vector3(x, HeightToRaycast, y);
            }

            protected override bool PreNavmeshCheck(Vector3 randomPos) {
                return true;
            }
        }
        class NPCBiomeSpawnRule : NPCSpawnRule {
            [JsonConverter(typeof(StringEnumConverter))]
            public TerrainBiome.Enum biomeName;
            public string npcPresetName;

            public override void spawn() {
                Vector3 point;
                var count = this.GetCount();
                for (int i = 0; i < count; i++) {
                    if (FindValidSpawnPoint(out point)) {
                        Instance.config.getNPCPreset(npcPresetName).spawn(point);
                        if (Instance.spawnedNpcs.Count() >= Instance.config.maxNpcs) {
                            return;
                        }
                    }
                }
            }

            protected override Vector3 GetSpawnPos() {
                return new Vector3(
                    UnityEngine.Random.Range(-TerrainMeta.Size.x / 2, TerrainMeta.Size.x / 2),
                    HeightToRaycast,
                    UnityEngine.Random.Range(-TerrainMeta.Size.z / 2, TerrainMeta.Size.z / 2)
                );
            }

            protected override bool PreNavmeshCheck(Vector3 randomPos) {
                return (TerrainBiome.Enum)TerrainMeta.BiomeMap.GetBiomeMaxType(randomPos) == this.biomeName;
            }
        }
        class NPCAirdropSpawnRule : NPCSpawnRule {
            public string npcPresetName;
            public float minSpread = 0;
            public float maxSpread = 10;

            private SupplyDrop currentDrop;
            private NonUserSupplyDrop supplyDropInfo;
            public void SetDrop(SupplyDrop drop) {
                this.currentDrop = drop;
            }

            public override void spawn() {
                if (this.currentDrop == null) {
                    return;
                }

                Vector3 point;
                var count = this.GetCount();
                for (int i = 0; i < count; i++) {
                    if (FindValidSpawnPoint(out point)) {
                        supplyDropInfo.npcs.Add(Instance.config.getNPCPreset(npcPresetName).spawn(point, false));
                    }
                }
                this.currentDrop = null;
            }

            protected override Vector3 GetSpawnPos() {
                float angle = UnityEngine.Random.Range(0, Mathf.PI * 2);
                float radius = UnityEngine.Random.Range(minSpread, maxSpread);
                float x = radius * Mathf.Cos(angle);
                float y = radius * Mathf.Sin(angle);
                return this.currentDrop.transform.position.XZ() + new Vector3(x, HeightToRaycast, y);
            }

            protected override bool PreNavmeshCheck(Vector3 randomPos) {
                return true;
            }

            protected override int GetCount() {
                var res = UnityEngine.Random.Range(minCount, maxCount+1);
                return res;
            }

            public void SetSupplyDropInfo(NonUserSupplyDrop info) {
                supplyDropInfo = info;
            }
        }
        #endregion
        #region CONFIG
        private PluginConfig config;
        class PluginConfig {
            #region READONLY
            public string[] monumentNamesReadonly {
                get {
                    return new string[] {
                        "lighthouse", "powerplant_1", "military_tunnel_1", "harbor_1", "harbor_2", "airfield_1",
                        "trainyard_1", "water_treatment_plant_1", "warehouse", "satellite_dish", "sphere_tank",
                        "radtown_small_3", "launch_site_1", "gas_station_1", "supermarket_1", "mining_quarry_c",
                        "mining_quarry_a", "mining_quarry_b", "junkyard_1"
                    };
                }
            }

            public string[] biomeNamesReadonly {
                get {
                    return new string[] {
                        TerrainBiome.Enum.Arctic.ToString(), TerrainBiome.Enum.Arid.ToString(), TerrainBiome.Enum.Temperate.ToString(), TerrainBiome.Enum.Tundra.ToString()
                    };
                }
            }

            public string[] crateNamesReadonly {
                get {
                    return lootPrefabs.Keys.ToArray();
                }
            }

            public string[] npcTypesReadonly {
                get {
                    return new string[] {
                        NPCType.Murderer.ToString(),
                        NPCType.Scarecrow.ToString(),
                        NPCType.Scientist.ToString()
                    };
                }
            }
            #endregion
            [JsonIgnore]
            public List<NPCSpawnRule> AllRules {
                get {
                    return this.biomeSpawnRules.Cast<NPCSpawnRule>().Concat(this.spawnRules).ToList();
                }
            }

            #region COMMON PARAMS
            public bool CanBeAttackedByNPCs = true;
            public bool CanBeAttackedByTurrets = true;
            public bool CanBeAttackedByHeli = true;
            public bool CanBeAttackedByBradley = true;
            public bool CanBeAttackedByAnimals = true;

            public bool LootUpdatedByThirdPartyPlugins = true;

            public int respawnPeriodSeconds = 300;
            public int maxNpcs = 100;
            public bool scaleNpcFromRulesToMaxCount = true;
            public int aidropZombiesRemoveDelaySeconds = 120;
            #endregion

            public NPCPreset[] npcPresets = {
                new NPCPreset {
                    name = "zombieCarrion",
                    type = NPCType.Scarecrow,
                    displayName = "Zombie Carrion",
                    accuracy = 1.0f,
                    receivingDamageMultiplier = 2.0f,
                    dealingDamageMultiplier = 0.5f,
                    wear = new NPCItemDefinition[] {
                        new NPCItemDefinition {
                            shortname = "burlap.shoes",
                            skin = new ulong[] { 820952520 }
                        },
                        new NPCItemDefinition {
                            shortname = "burlap.shirt"
                        }
                    },

                    belt = new NPCItemSpendableDefinition[] {
                        new NPCItemSpendableDefinition {
                            shortname = "spear.stone"
                        }
                    },

                    loot = new NPCLootDefinition {
                        crateItemsMin = 1,
                        crateItemsMax = 3,
                        lootCrates = new string[] { "crate_normal_2" }
                    }
                }
            };

            public NPCPreset getNPCPreset(string presetName) {
                return this.npcPresets.ToList().Find(np => np.name.Equals(presetName));
            }

            public NPCMonumentSpawnRule[] spawnRules = {
                new NPCMonumentSpawnRule {
                    monumentName = "sphere_tank",
                    minCount = 3,
                    maxCount = 10,
                    minSpread = 0f,
                    maxSpread = 25.0f,
                    npcPresetName = "zombieCarrion"
                }
            };

            public NPCBiomeSpawnRule[] biomeSpawnRules = {
                new NPCBiomeSpawnRule {
                    biomeName = TerrainBiome.Enum.Arctic,
                    minCount = 10,
                    maxCount = 20,
                    npcPresetName = "zombieCarrion",
                }
            };

            public NPCAirdropSpawnRule[] airdropSpawnRules = {
                new NPCAirdropSpawnRule {
                    maxCount = 10,
                    minCount = 5,
                    maxSpread = 7,
                    minSpread = 2,
                    npcPresetName = "zombieCarrion"
                }
            };

            [JsonIgnore]
            public float spawnCountFactor = 1.0f;

            public void Init() {
                validate();
                initMonuments();
                initSpawnScaleFactor();
                InitSpawns();
                Instance.Puts($"Successfully spawned {Instance.spawnedNpcs.Count} NPCs");
            }

            private void validate() {
                bool valid = true;
                if (npcPresets.Select(preset => preset.name).Distinct().Count() != npcPresets.Length) {
                    Instance.PrintError("There are duplicating NPC preset names, check the config!");
                    valid = false;
                }

                foreach (var preset in npcPresets) {
                    foreach (var nid in preset.wear) {
                        if (ItemManager.itemList.FindIndex(id => id.shortname == nid.shortname) == -1) {
                            Instance.PrintError($"NPC preset {preset.name}: shortname {nid.shortname} doesn't exist");
                            valid = false;
                        }
                    }

                    foreach (var nid in preset.belt) {
                        if (ItemManager.itemList.FindIndex(id => id.shortname == nid.shortname) == -1) {
                            Instance.PrintError($"NPC preset {preset.name}: shortname {nid.shortname} doesn't exist");
                            valid = false;
                        }
                    }

                    foreach (var crateName in preset.loot.lootCrates) {
                        if (!crateNamesReadonly.Contains(crateName)) {
                            Instance.PrintError($"NPC preset {preset.name}: crate {crateName} doesn't exist");
                            valid = false;
                        }
                    }
                }

                foreach (var rule in spawnRules) {
                    if (npcPresets.ToList().FindIndex(pr => pr.name.Equals(rule.npcPresetName)) == -1) {
                        Instance.PrintError($"Unknown NPC preset {rule.npcPresetName}, check the config!");
                        valid = false;
                    }

                    if (!monumentNamesReadonly.Contains(rule.monumentName)) {
                        Instance.PrintError($"Unknown monument name {rule.monumentName}, check the config!");
                        valid = false;
                    }
                }

                foreach (var rule in biomeSpawnRules) {
                    if (npcPresets.ToList().FindIndex(pr => pr.name.Equals(rule.npcPresetName)) == -1) {
                        Instance.PrintError($"Unknown NPC preset {rule.npcPresetName}, check the config!");
                        valid = false;
                    }

                    if (!biomeNamesReadonly.Contains(rule.biomeName.ToString())) {
                        Instance.PrintError($"Unknown biome name {rule.biomeName.ToString()}, check the config!");
                        valid = false;
                    }
                }

                foreach (var rule in airdropSpawnRules) {
                    if (npcPresets.ToList().FindIndex(pr => pr.name.Equals(rule.npcPresetName)) == -1) {
                        Instance.PrintError($"Unknown NPC preset {rule.npcPresetName}, check the config!");
                        valid = false;
                    }
                }

                if (!valid) {
                    throw new Exception("Config is invalid check the console");
                }
            }
            public void InitSpawns() {
                foreach (var rule in AllRules) {
                    rule.spawn();
                    
                    if (Instance.spawnedNpcs.Count >= maxNpcs) {
                        Instance.PrintWarning($"NPC limit reached {maxNpcs}");
                        return;
                    }
                }
            }
            private void initSpawnScaleFactor() {
                int maxFromRules = 0;
                foreach (var monument in Instance.availableMonuments) {
                    var monumentName = monumentNamesReadonly.ToList().Find(name => monument.name.Contains(name));
                    if (monumentName != null) {
                        foreach (var rule in spawnRules) {
                            if (rule.monumentName.Equals(monumentName)) {
                                maxFromRules += rule.maxCount;
                            }
                        }
                    }
                }

                foreach (var rule in biomeSpawnRules) {
                    maxFromRules += rule.maxCount;
                }

                if (maxFromRules == 0) {
                    maxFromRules = 1;
                }
                Instance.Puts($"max zombies from rules: {maxFromRules}");
                Instance.Puts($"max from config: {maxNpcs}");
                if (scaleNpcFromRulesToMaxCount) { 
                    spawnCountFactor = (float)maxNpcs / (float)maxFromRules;
                    Instance.Puts($"resulting factor: {spawnCountFactor}");
                } else {
                    Instance.Puts($"No scaling, will try to spawn {maxFromRules} NPCs");
                }
            }
            private void initMonuments() {
                var allobjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                foreach (var gobject in allobjects) {
                    if (gobject.name.Contains("autospawn/monument")) {
                        if (gobject.transform.position == new Vector3(0, 0, 0)) {
                            continue;
                        }
                        Instance.availableMonuments.Add(gobject);
                    }
                }

                Instance.Puts($"Found {Instance.availableMonuments.Count} monuments");
            }
        }
        #endregion
        private static bool getRandom(float probability = 0.5f) {
            return UnityEngine.Random.Range(0f, 1f) < probability;
        }
        private static void clearContainer(ItemContainer container) {
            while (container.itemList.Count > 0) {
                var item = container.itemList[0];
                item.RemoveFromContainer();
                item.Remove(0f);
            }
        }

        public class NPCController : MonoBehaviour {
            public BasePlayer npcPlayer;
            public string presetName;
            public float roamingRange;
            public Vector3 homePos;
            public bool naturalDeath = false;
            public bool handledKill;
            public bool shouldRespawn;

            private void Awake() {
                npcPlayer = GetComponent<BasePlayer>();
                enabled = false;
            }

            private void OnDestroy() {
                InvokeHandler.CancelInvoke(this, CheckLocation);
                if (npcPlayer != null && npcPlayer.IsValid() && !npcPlayer.IsDestroyed && !naturalDeath) {
                    npcPlayer.Kill();
                }

                if (!this.handledKill) {
                    Instance.PrintError("Unhandled kill of NPC. Maybe it's another plugin tryies to remove NPCs");
                }
            }

            public void SetInfo(string presetName, Vector3 homePos, float roamingRange, bool shouldRespawn) {
                this.presetName = presetName;
                this.homePos = homePos;
                this.roamingRange = roamingRange;
                this.shouldRespawn = shouldRespawn;
                InvokeHandler.InvokeRepeating(this, CheckLocation, 1f, 20f);
            }

            private void CheckLocation() {
                if (Vector3.Distance(npcPlayer.transform.position, homePos) > roamingRange) {
                    //((npcPlayer as HTNPlayer)?.AiDomain as MurdererDomain)?.SetDestination(homePos);
                    var comp = npcPlayer.gameObject.GetComponent<ScientistBrain>();
                    if (comp)
                    {
                        comp.Navigator.SetDestination(homePos);
                        return;
                    }

                    var scare = npcPlayer.gameObject.GetComponent<ScarecrowBrain>();
                    if (scare)
                    {
                        scare.Navigator.SetDestination(homePos);
                        return;
                    }
                    (npcPlayer as NPCPlayer)?.SetDestination(homePos);
                }
            }
        }

        protected override void LoadDefaultConfig() {
            Config.WriteObject(new PluginConfig(), true);
        }

        #region Hooks

        object OnNpcTarget(BaseNpc npc, BaseEntity target) {
            if (!this.config.CanBeAttackedByAnimals) {
                if (target?.net?.ID != null && this.spawnedNpcs.ContainsKey(target.net.ID)) {
                    return false;
                }
            }

            return null;
        }

        //TODO: FIX CAN TARGET
        //object OnNpcPlayerTarget(NPCPlayerApex npcPlayer, BaseEntity target) {
        //    if (!this.config.CanBeAttackedByNPCs) {
        //        if (target?.net?.ID != null && this.spawnedNpcs.ContainsKey(target.net.ID)) {
        //            return false;
        //        }
        //    }

        //    return null;
        //}

        object CanBradleyApcTarget(BradleyAPC apc, BaseEntity target) {
            if (!this.config.CanBeAttackedByBradley) {
                if (target?.net?.ID != null && this.spawnedNpcs.ContainsKey(target.net.ID)) {
                    return false;
                }
            }

            return null;
        }

        object CanHelicopterTarget(PatrolHelicopterAI heli, BasePlayer target) {
            if (!this.config.CanBeAttackedByHeli) {
                if (target?.net?.ID != null && this.spawnedNpcs.ContainsKey(target.net.ID)) {
                    return false;
                }
            }

            return null;
        }

        object CanBeTargeted(BaseCombatEntity target, MonoBehaviour turret) {
            if (!this.config.CanBeAttackedByTurrets) {
                if (target?.net?.ID != null && this.spawnedNpcs.ContainsKey(target.net.ID)) {
                    return false;
                }
            }

            return null;
        }

        private void Init() {
            permission.RegisterPermission(perm, this);
            Instance = this;
            config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(config);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
        }

        private void OnServerInitialized() {
            config.Init();
            timer.Once(1.0f, storedData.CleanupSupplyDrops);
        }

        private void OnNewSave() {
            storedData = new StoredData();
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }

        private void OnServerSave() {
            if (storedData != null) {
                Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
            }
        }
        private void OnEntityKill(BaseEntity entity) {
            if (entity is SupplyDrop) {
                storedData.OnKillSupplyDrop(entity as SupplyDrop);

                if (nonUserSupplyDrops.ContainsKey(entity.net.ID)) {
                    uint id = entity.net.ID;
                    timer.Once(config.aidropZombiesRemoveDelaySeconds, () => {
                        foreach (var npcController in nonUserSupplyDrops[id].npcs) {
                            npcController.handledKill = true;
                            UnityEngine.Object.Destroy(npcController);
                        }

                        nonUserSupplyDrops.Remove(id);
                    });
                }
            }
        }
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info) {
            if (entity?.net?.ID == null)
                return;

            if (spawnedNpcs.ContainsKey(entity.net.ID)) {
                NPCController npcPlayer = entity.GetComponent<NPCController>();
                if (npcPlayer != null) {
                    npcPlayer.naturalDeath = true;
                    var preset = config.getNPCPreset(npcPlayer.presetName);

                    expectedCorpses.Add(new ExpectedCorpse {
                        id = npcPlayer.npcPlayer.userID,
                        preset = preset
                    });
                    if (npcPlayer.shouldRespawn) {
                        timer.In(config.respawnPeriodSeconds, () => {
                            preset.spawn(npcPlayer.homePos);
                        });
                    }

                    npcPlayer.handledKill = true;
                    UnityEngine.Object.Destroy(npcPlayer);
                }

                spawnedNpcs.Remove(entity.net.ID);
            }
        }

        private void Unload() {
            foreach (var npc in spawnedNpcs) {
                if (npc.Value != null) {
                    var controller = npc.Value.GetComponent<NPCController>();
                    if (controller) {
                        controller.handledKill = true;
                        UnityEngine.Object.Destroy(controller);
                    }
                }
            }

            var npcs = UnityEngine.Object.FindObjectsOfType<NPCController>();
            if (npcs != null) {
                foreach (var gameObj in npcs) {
                    gameObj.handledKill = true;
                    UnityEngine.Object.Destroy(gameObj);
                }
            }

            lootGenerator.Cleanup();
            spawnedNpcs.Clear();
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }

        private void OnEntityTakeDamage(BaseCombatEntity victimEntity, HitInfo hitInfo) {
            if (victimEntity?.net?.ID != null && spawnedNpcs.ContainsKey(victimEntity.net.ID)) {
                NPCController npcPlayer = victimEntity.GetComponent<NPCController>();
                if (npcPlayer != null) {
                    hitInfo.damageTypes.ScaleAll(config.getNPCPreset(npcPlayer.presetName).receivingDamageMultiplier);

                    if (hitInfo.Initiator != null && hitInfo.Initiator is BradleyAPC) {
                        hitInfo.damageTypes.ScaleAll(0);
                    }
                }
            }

            if (hitInfo.Initiator?.net?.ID != null && spawnedNpcs.ContainsKey(hitInfo.Initiator.net.ID)) {
                NPCController npcPlayer = hitInfo.Initiator.GetComponent<NPCController>();
                var preset = config.getNPCPreset(npcPlayer.presetName);
                if (npcPlayer != null) {
                    hitInfo.damageTypes.ScaleAll(preset.dealingDamageMultiplier);
                }

                if (getRandom(1 - preset.accuracy)) { // accuracy emulation
                    hitInfo.damageTypes = new DamageTypeList();
                    hitInfo.HitMaterial = 0;
                    hitInfo.PointStart = Vector3.zero;
                }
            }
        }

        void OnEntitySpawned(BaseEntity entity) {
            if (entity is LootableCorpse) {
                LootableCorpse corpse = entity as LootableCorpse;

                var corpseID = corpse.playerSteamID;
                var expectedCorpse = expectedCorpses.Find(ex => ex.id == corpseID);
                if (expectedCorpse == null)
                    return;
                var preset = expectedCorpse.preset;
                expectedCorpses.Remove(expectedCorpse);

                NextTick(() => {
                    if (corpse != null && corpse.IsValid()) {
                        preset.loot.spawnItemsToContainer(corpse.containers[0]);
                    }
                });

            } else if (entity is SupplySignal) {
                storedData.HandleSupplySignal(entity as SupplySignal);
            } else if (entity is CargoPlane) {
                storedData.HandleCargoPlane(entity as CargoPlane);
            } else if (entity is SupplyDrop) {
                if (storedData.DropIsHandled(entity as SupplyDrop)) {
                    return;
                }

                nonUserSupplyDrops.Add(entity.net.ID, new NonUserSupplyDrop());
                foreach (var rule in this.config.airdropSpawnRules) {
                    rule.SetDrop(entity as SupplyDrop);
                    rule.SetSupplyDropInfo(nonUserSupplyDrops[entity.net.ID]);
                    rule.spawn();
                }
            }
        }

        #endregion
        [ChatCommand("npc_killall")]
        private void KillAllCommand(BasePlayer player, string command, string[] args) {
            if (!permission.UserHasPermission(player.UserIDString, perm)) {
                SendReply(player, "У вас нет привилегии для выполнения этой команды");
                return;
            }

            int count = spawnedNpcs.Count;
            foreach (var npc in spawnedNpcs) {
                var controller = npc.Value.GetComponent<NPCController>();
                if (controller) {
                    controller.handledKill = true;
                    UnityEngine.Object.Destroy(controller);
                }
            }
            spawnedNpcs.Clear();
            SendReply(player, $"{count} NPC удалено");
        }
        [ChatCommand("npc_count")]
        private void SpawnCount(BasePlayer player, string command, string[] args) {
            if (!permission.UserHasPermission(player.UserIDString, perm)) {
                SendReply(player, "У вас нет привилегии для выполнения этой команды");
                return;
            }
            SendReply(player, $"на карте {this.spawnedNpcs.Count} NPC");
        }

        [ChatCommand("npc_spawn")]
        private void SpawnCommand(BasePlayer player, string command, string[] args) {
            if (!permission.UserHasPermission(player.UserIDString, perm)) {
                SendReply(player, "У вас нет привилегии для выполнения этой команды");
                return;
            }

           if (args.Length == 1) {
                var presetName = args[0];
                var preset = this.config.getNPCPreset(presetName);
                if (preset == null) {
                    SendReply(player, $"Пресет {presetName} не найден");
                    return;
                }

                try {
                    var point = getSpawnPoint(player);
                    preset.spawn(point, false);
                    SendReply(player, $"Spawned new {preset.displayName}");
                } catch (Exception ex) {
                    SendReply(player, $"Unable to spawn NPC: {ex.Message}");
                }
           } else {
                int count = spawnedNpcs.Count; 
                foreach (var npc in spawnedNpcs) {
                    var controller = npc.Value.GetComponent<NPCController>();
                    if (controller) {
                        controller.handledKill = true;
                        UnityEngine.Object.Destroy(controller);
                    }
                }
                spawnedNpcs.Clear();
                SendReply(player, $"Удалено {count} NPC");

                config.InitSpawns();
                SendReply(player, $"{this.spawnedNpcs.Count} NPC добавлено");
            }
        }

        private Vector3 getSpawnPoint(BasePlayer player) {
            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, 100.0f, Layers.Solid)) {
                return hit.point;
            }

            throw new Exception("Unable to raycast pos: curvative exceed");            
        }
    }
}

