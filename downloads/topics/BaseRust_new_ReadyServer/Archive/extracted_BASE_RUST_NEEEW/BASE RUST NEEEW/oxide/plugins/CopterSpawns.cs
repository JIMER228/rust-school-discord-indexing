using Facepunch;
using Newtonsoft.Json;
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Oxide.Plugins
{
    [Info("CopterSpawns", "OxideBro", "1.0.1")]
    public class CopterSpawns : RustPlugin
    {
        #region Configuration
        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            PrintWarning("No configuration, create a new one. Thanks for download plugins in RustPlugin.ru");
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


        public class MiniCopterSetting
        {
            [JsonProperty("Популяция миникоптеров на один квадратный километр | Minicopter population")]
            public int Population;
            [JsonProperty("Включить наполнение баков миникоптеров бензином | Enabled free fuel")]
            public bool EnabledFuelSpawn;
            [JsonProperty("Количество топлива добавляемое в бак коптеров | Fuel count")]
            public int FuelCount;
            [JsonProperty("Заблокировать инвентарь миникоптеров в случае если мы добавляем топливо | Block minicopter inventory if we add fuel")]
            public bool EnabledBlockFuel;

            [JsonProperty("[MAP]: Включить отображение миникоптеров на стандартной карте | Enabled create map marker")]
            public bool EnabledMapMarker;

            [JsonProperty("[MAP]: Радиус отметки на карте | Marker radius")]
            public float MarketRadius;

            [JsonProperty("[MAP]: Текст на отметке на карты миникоптера | Marker description")]
            public string MarkerDescription;
        }


        public class ScrapCopterSetting
        {
            [JsonProperty("Популяция скрап-коптеров на один квадратный километр | ScrapCopter population")]
            public int Population;
            [JsonProperty("Включить наполнение баков скрап-коптеров бензином | Enabled free fuel")]
            public bool EnabledFuelSpawn;
            [JsonProperty("Количество топлива добавляемое в бак скрап-коптеров | Fuel count")]
            public int FuelCount;
            [JsonProperty("Заблокировать инвентарь скрап-коптеров в случае если мы добавляем топливо | Block scrap-copter inventory if we add fuel")]
            public bool EnabledBlockFuel;

            [JsonProperty("[MAP]: Включить отображение скрап-коптеров на стандартной карте | Enabled create map marker")]
            public bool EnabledMapMarker;

            [JsonProperty("[MAP]: Радиус отметки на карте | Marker radius")]
            public float MarketRadius;

            [JsonProperty("[MAP]: Текст на отметке на карты скрап-коптеров | Marker description")]
            public string MarkerDescription;
        }



        private class PluginConfig
        {
            [JsonProperty("Настройки миникоптера | Minicopter Settings")]
            public MiniCopterSetting miniCopterSetting;


            [JsonProperty("Настройки скрап-коптера | ScrapCopter settings")]
            public ScrapCopterSetting scrapCopterSetting;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    miniCopterSetting = new MiniCopterSetting()
                    {
                        Population = 2,
                        EnabledFuelSpawn = true,
                        FuelCount = 100,
                        EnabledBlockFuel = true,
                        EnabledMapMarker = true,
                        MarketRadius = 0.3f,
                        MarkerDescription = "MiniCopter\nFree fuel"
                    },
                    scrapCopterSetting = new ScrapCopterSetting()
                    {
                        Population = 2,
                        EnabledFuelSpawn = true,
                        FuelCount = 100,
                        EnabledBlockFuel = true,
                        EnabledMapMarker = true,
                        MarketRadius = 0.3f,
                        MarkerDescription = "ScrapCopter\nFree fuel"
                    },
                };
            }
        }
        #endregion

        #region Oxide
        static CopterSpawns ins;

        private void OnServerInitialized()
        {
            ins = this;
            LoadConfig();
            MiniCopter.population = config.miniCopterSetting.Population;
            if (config.miniCopterSetting.Population > 0)
            {
                PrintWarning($"Server size map: {TerrainMeta.Size.x}, Minicopter population {MiniCopter.population}, Loaded {UnityEngine.Object.FindObjectsOfType<MiniCopter>().Count()} MiniCopters");
                StartSpawnMiniCopter();
            }

            ScrapTransportHelicopter.population = config.scrapCopterSetting.Population;

            if (config.scrapCopterSetting.Population > 0)
            {
                PrintWarning($"Server size map: {TerrainMeta.Size.x}, ScrapCopter population {MiniCopter.population}, Loaded {UnityEngine.Object.FindObjectsOfType<ScrapTransportHelicopter>().Count()} ScrapCopters");
                StartSpawnScrapCopter();
            }
        }
        void Unload()
        {
            var markers = GameObject.FindObjectsOfType<VehecleMarker>();
            foreach (var marker in markers)
            {
                if (marker != null)
                    UnityEngine.Object.Destroy(marker);
            }
            var ents = UnityEngine.Object.FindObjectsOfType<MiniCopter>();
            foreach (var marker in ents)
            {
                if (marker != null)
                    marker.Kill();
            }
        }
        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null || entity?.net?.ID == null) return;
            try
            {
                if (entity is MiniCopter) NextTick(() => StartSpawnMiniCopter());
            }
            catch (NullReferenceException) { }
        }
        #endregion

        #region Spawn
        void StartSpawnMiniCopter()
        {
            var ents = UnityEngine.Object.FindObjectsOfType<MiniCopter>();

            if (ents != null)
            {
                var entcount = ents.Count();
                var count = MiniCopter.population * TerrainMeta.Size.x / 1000 * 2;
                if (count - entcount > 0) PrintWarning($"At the moment we will create {count - entcount} minicopter");
                for (int i = 0; i < count - entcount; i++)
                {
                    Vector3 vector = GetEventPosition();
                    MiniCopter copter = GameManager.server.CreateEntity("assets/content/vehicles/minicopter/minicopter.entity.prefab", vector, new Quaternion(), true) as MiniCopter;
                    copter.enableSaving = true;
                    copter.Spawn();
                    if (config.miniCopterSetting.EnabledMapMarker) copter.gameObject.AddComponent<VehecleMarker>();
                    if (config.miniCopterSetting.EnabledFuelSpawn)
                    {
                        var b = copter.GetFuelSystem().fuelStorageInstance.Get(true).GetComponent<StorageContainer>();
                        if (b != null && b.inventory.FindItemByItemID(-946369541) == null)
                        {
                            ItemManager.CreateByName("lowgradefuel", config.miniCopterSetting.FuelCount)?.MoveToContainer(b.inventory);
                            if (config.miniCopterSetting.EnabledBlockFuel) b.inventory.SetLocked(true);
                            b.inventory.MarkDirty();
                        }
                        copter.SendNetworkUpdate();

                        Puts($"{copter.transform.position}");
                    }

                }
            }
        }


        void StartSpawnScrapCopter()
        {
            var ents = UnityEngine.Object.FindObjectsOfType<ScrapTransportHelicopter>();

            if (ents != null)
            {
                var entcount = ents.Count();
                var count = MiniCopter.population * TerrainMeta.Size.x / 1000 * 2;
                if (count - entcount > 0) PrintWarning($"At the moment we will create {count - entcount} scrap-copters");
                for (int i = 0; i < count - entcount; i++)
                {
                    Vector3 vector = GetEventPosition();
                    ScrapTransportHelicopter copter = GameManager.server.CreateEntity("assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab", vector, new Quaternion(), true) as ScrapTransportHelicopter;
                    copter.enableSaving = true;
                    copter.Spawn();
                    if (config.scrapCopterSetting.EnabledMapMarker) copter.gameObject.AddComponent<VehecleMarker>();
                    if (config.scrapCopterSetting.EnabledFuelSpawn)
                    {
                        var b = copter.GetFuelSystem().fuelStorageInstance.Get(true).GetComponent<StorageContainer>();
                        if (b != null && b.inventory.FindItemByItemID(-946369541) == null)
                        {
                            ItemManager.CreateByName("lowgradefuel", config.scrapCopterSetting.FuelCount)?.MoveToContainer(b.inventory);
                            if (config.scrapCopterSetting.EnabledBlockFuel) b.inventory.SetLocked(true);
                            b.inventory.MarkDirty();
                        }
                        copter.SendNetworkUpdate();
                    }

                }
            }
        }

        class VehecleMarker : BaseEntity
        {
            BaseEntity vehicle;
            MapMarkerGenericRadius mapmarker;
            VendingMachineMapMarker MarkerName;
            SphereCollider sphereCollider;
            CopterSpawns instance;

            void Awake()
            {
                instance = new CopterSpawns();
                vehicle = GetComponent<BaseEntity>();
                sphereCollider = gameObject.AddComponent<SphereCollider>();
                sphereCollider.gameObject.layer = (int)Layer.Reserved1;
                sphereCollider.isTrigger = true;
                sphereCollider.radius = 10f;
                SpawnMapMarkers();
            }

            public void SpawnMapMarkers()
            {
                MarkerName = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", vehicle.transform.position, Quaternion.identity, true) as VendingMachineMapMarker;
                MarkerName.markerShopName = vehicle.GetComponent<ScrapTransportHelicopter>() != null ? ins.config.scrapCopterSetting.MarkerDescription : ins.config.miniCopterSetting.MarkerDescription;
                MarkerName.Spawn();
                mapmarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", vehicle.transform.position, Quaternion.identity, true) as MapMarkerGenericRadius;
                mapmarker.Spawn();
                mapmarker.radius = vehicle.GetComponent<ScrapTransportHelicopter>() != null ? ins.config.scrapCopterSetting.MarketRadius : ins.config.miniCopterSetting.MarketRadius;
                mapmarker.alpha = 1f;
                if (vehicle.PrefabName.Contains("minicopter"))
                {
                    Color color = new Color(1.00f, 0.50f, 0.00f, 1.00f);
                    mapmarker.color1 = color;
                }

                if (vehicle.PrefabName.Contains("scrap"))
                {
                    Color color = new Color(1.00f, 0.50f, 0.00f, 1.00f);
                    mapmarker.color1 = color;
                }
                mapmarker.SendUpdate();
            }

            private void OnTriggerEnter(Collider col)
            {
                var target = col.GetComponentInParent<BasePlayer>();
                if (target != null)
                    Destroy();
            }

            void OnDestroy()
            {
                if (mapmarker != null) mapmarker.Invoke("KillMessage", 0.1f);
                if (MarkerName != null) MarkerName.Invoke("KillMessage", 0.1f);
            }

            public void Destroy()
            {
                if (mapmarker != null) mapmarker.Invoke("KillMessage", 0.1f);
                if (MarkerName != null) MarkerName.Invoke("KillMessage", 0.1f);
            }
        }

        SpawnFilter filter = new SpawnFilter();
        List<Vector3> monuments = new List<Vector3>();

        static float GetGroundPosition(Vector3 pos)
        {
            float y = TerrainMeta.HeightMap.GetHeight(pos);
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 200f, pos.z), Vector3.down, out hit, Mathf.Infinity, LayerMask.GetMask(new[] { "Terrain", "World", "Default", "Construction", "Deployed" })) && !hit.collider.name.Contains("rock_cliff"))
                return Mathf.Max(hit.point.y, y);
            return y;
        }

        public Vector3 RandomDropPosition()
        {
            var vector = Vector3.zero;
            float num = 1000f, x = TerrainMeta.Size.x / 3;

            do
            {
                vector = Vector3Ex.Range(-x, x);
            }
            while (filter.GetFactor(vector) == 0f && (num -= 1f) > 0f);
            float max = TerrainMeta.Size.x / 2;
            float height = TerrainMeta.HeightMap.GetHeight(vector);
            vector.y = height;
            return vector;
        }

        List<int> BlockedLayers = new List<int> { (int)Layer.Water, (int)Layer.Construction, (int)Layer.Trigger, (int)Layer.Prevent_Building, (int)Layer.Deployed, (int)Layer.Tree };
        static int blockedMask = LayerMask.GetMask(new[] { "Player (Server)", "Trigger", "Prevent Building" });

        public Vector3 GetSafeDropPosition(Vector3 position)
        {
            RaycastHit hit;
            position.y += 200f;

            if (Physics.Raycast(position, Vector3.down, out hit))
            {
                if (hit.collider?.gameObject == null)
                    return Vector3.zero;
                string ColName = hit.collider.name;

                if (!BlockedLayers.Contains(hit.collider.gameObject.layer) && ColName != "MeshColliderBatch" && ColName != "iceberg_3" && ColName != "iceberg_2" && !ColName.Contains("rock_cliff"))
                {
                    position.y = Mathf.Max(hit.point.y, TerrainMeta.HeightMap.GetHeight(position));
                    var colliders = Pool.GetList<Collider>();
                    Vis.Colliders(position, 1, colliders, blockedMask, QueryTriggerInteraction.Collide);
                    bool blocked = colliders.Count > 0;
                    Pool.FreeList<Collider>(ref colliders);
                    if (!blocked)
                        return position;
                }
            }
            return Vector3.zero;
        }

        public Vector3 GetEventPosition()
        {
            var eventPos = Vector3.zero;
            int maxRetries = 100;
            monuments = UnityEngine.Object.FindObjectsOfType<MonumentInfo>().Select(monument => monument.transform.position).ToList();
            do
            {
                eventPos = GetSafeDropPosition(RandomDropPosition());

                foreach (var monument in monuments)
                {
                    if (Vector3.Distance(eventPos, monument) < 150f)
                    {
                        eventPos = Vector3.zero;
                        break;
                    }
                }
            } while (eventPos == Vector3.zero && --maxRetries > 0);

            eventPos.y = GetGroundPosition(eventPos);

            if (eventPos.y < 0)
                GetEventPosition();
            return eventPos;
        }
        #endregion
    }
}