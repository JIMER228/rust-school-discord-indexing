using System;
using UnityEngine;
using System.Numerics;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Libraries;
using Newtonsoft.Json;
using System.Collections.Generic;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("DroneEvent", "Fruster", "1.1.4")]
    [Description("DroneEvent")]
    class DroneEvent : CovalencePlugin
    {
        [PluginReference] Plugin SimpleLootTable;
        const int layerS = ~(1 << 2 | 1 << 3 | 1 << 4 | 1 << 10 | 1 << 18 | 1 << 28 | 1 << 29);
        private float altitude = 10f;
        private Timer dronesMainTimer;
        private class DroneAI : FacepunchBehaviour
        {
            public Vector3 homePosition;
            public Vector3 targetPosition;
            public BaseEntity parent;
            public AutoTurret turret;
            public int health;
            public int radius;
            public float altitude;
            public float upVelocity;
            public float speed;
            public SpawnSettings settings;
        }

        private class SpawnSettings : FacepunchBehaviour
        {
            public Drone drone;
            public int timer;
            public int radius;
        }

        private class MonumentSettings
        {
            public string name { get; set; }
            public int flightRadius { get; set; }
            public Vector3 offset { get; set; }
            public bool use { get; set; }
        }

        private class CustomSpawnPoints
        {
            public string name { get; set; }
            public int flightRadius { get; set; }
            public Vector3 position { get; set; }
            public bool use { get; set; }
        }

        private class ItemsList
        {
            public string name { get; set; }
            public int dropChance { get; set; }
            public float minAmount { get; set; }
            public int maxAmount { get; set; }
            public ulong skinID { get; set; }
        }

        float sense;
        Drone drone;
        DroneAI droneAI;
        Rigidbody rigidbody;
        RaycastHit check;
        Vector3 offset;
        Vector3 lTargetDir;
        BaseEntity body;

        private ConfigData Configuration;
        class ConfigData
        {
            [JsonProperty("Minimum respawn time(in minutes)")]
            public int minRaspawnTime = 15;
            [JsonProperty("Maximum respawn time(in minutes)")]
            public int maxRaspawnTime = 30;
            [JsonProperty("Drone health (hits amount)")]
            public int droneHealth = 2;
            [JsonProperty("Damage dealt to a drone when a player hits it with a rocket")]
            public int rocketDamage = 5;
            [JsonProperty("Drone speed(0.5 - 1 recommended)")]
            public float droneSpeed = 0.7f;
            [JsonProperty("Grenade damage scale")]
            public float dmgScale = 1f;
            [JsonProperty("Attack range")]
            public int attackRange = 40;
            [JsonProperty("Drone aggressiveness, from 0 to 100 (the more, the more often the drone attacks)")]
            public int droneAggressiveness = 10;
            [JsonProperty("Adds a searchlight for drones")]
            public bool searchLight = false;
            [JsonProperty("Adds a turret for drones")]
            public bool turret = false;
            [JsonProperty("Turret damage scale")]
            public float turretDmgScale = 1f;
            [JsonProperty("Turret weapon short name")]
            public string turretWeaponName = "pistol.revolver";

            [JsonProperty("The drone will throw grenades at players")]
            public bool throwGrenades = true;
            [JsonProperty("Do not calculate collisions while the drone is just flying (set to true if you have problems with your server performance)")]
            public bool noCollision = false;
            [JsonProperty("Blow up a drone immediately after it runs out of health (will reduce the load on your server a little more)")]
            public bool immediatelyExp = false;
            [JsonProperty("Simple loot table name(plugin SimpleLootTable is required)")]
            public string lootTableName = "";
            [JsonProperty("Minimum number of items for a simple LootTable(plugin SimpleLootTable is required)")]
            public int tableMinItems = 1;
            [JsonProperty("Maximum number of items for a simple LootTable(plugin SimpleLootTable is required)")]
            public int tableMaxItems = 3;

            [JsonProperty("Monument settings")]
            public List<MonumentSettings> monumentSettings = new List<MonumentSettings>
            {
            new MonumentSettings { name = "gas_station", flightRadius = 15, offset = new Vector3(17, 30, 0), use = true }, new MonumentSettings { name = "supermarket", flightRadius = 15, offset = new Vector3(0, 15, 0), use = true },
            new MonumentSettings { name = "warehouse", flightRadius = 15, offset = new Vector3(-5, 15, 0), use = true }, new MonumentSettings { name = "water_treatment", flightRadius = 15, offset = new Vector3(-60, 20, 0), use = true },
            new MonumentSettings { name = "junkyard", flightRadius = 15, offset = new Vector3(0, 15, 0), use = true }, new MonumentSettings { name = "lighthouse", flightRadius = 15, offset = new Vector3(0, 35, 40), use = true },
            new MonumentSettings { name = "sphere_tank", flightRadius = 15, offset = new Vector3(60, 35, 40), use = true }, new MonumentSettings { name = "harbor_1", flightRadius = 15, offset = new Vector3( -40, 25, 20), use = true },
            new MonumentSettings { name = "harbor_2", flightRadius = 15, offset = new Vector3(-100, 20, -20), use = true }, new MonumentSettings { name = "desert_military_base", flightRadius = 15, offset = new Vector3(-15, 20, -5), use = true },
            new MonumentSettings { name = "excavator", flightRadius = 15, offset = new Vector3( 65, 20, -10), use = true }, new MonumentSettings { name = "swamp", flightRadius = 15, offset = new Vector3(-10, 30, -15), use = false },
            new MonumentSettings { name = "radtown_small", flightRadius = 15, offset = new Vector3(-5, 30, -5), use = true }, new MonumentSettings { name = "water_well", flightRadius = 15, offset = new Vector3(0, 30, 0), use = true },
            new MonumentSettings { name = "mining_quarry", flightRadius = 15, offset = new Vector3(0, 25, 0), use = false }, new MonumentSettings { name = "satellite_dish", flightRadius = 15, offset = new Vector3(-40, 25, -20), use = false },
            new MonumentSettings { name = "cave_small", flightRadius = 15, offset = new Vector3(0, 30, 0), use = false }, new MonumentSettings { name = "cave_medium", flightRadius = 15, offset = new Vector3(0, 30, 0), use = false },
            new MonumentSettings { name = "cave_large", flightRadius = 30, offset = new Vector3(0, 35, 0), use = false },new MonumentSettings { name = "airfield", flightRadius = 25, offset = new Vector3(-75, 20, 0), use = false },
            new MonumentSettings { name = "launch_site", flightRadius = 15, offset = new Vector3(0, 20, 130), use = false }, new MonumentSettings { name = "powerplant", flightRadius = 25, offset = new Vector3(-75, 20, -40), use = false },
            new MonumentSettings { name = "trainyard", flightRadius = 15, offset = new Vector3(-70, 20, 0), use = false }, new MonumentSettings { name = "arctic_research_base", flightRadius = 25, offset = new Vector3(10, 15, -25), use = true },
            new MonumentSettings { name = "ice_lake", flightRadius = 15, offset = new Vector3(0, 20, 0), use = false }, new MonumentSettings { name = "military_tunnel", flightRadius = 15, offset = new Vector3(20, 30, -15), use = false },
            new MonumentSettings { name = "power_sub_big", flightRadius = 10, offset = new Vector3(0, 20, 0), use = false }, new MonumentSettings { name = "ferry_terminal", flightRadius = 10, offset = new Vector3(40, 25, 0), use = true },
            new MonumentSettings { name = "missile_sil", flightRadius = 10, offset = new Vector3(0, 20, 0), use = false }
            };
            [JsonProperty("Custom spawn points settings")]
            public List<CustomSpawnPoints> customSpawnPoints = new List<CustomSpawnPoints>
            { new CustomSpawnPoints { name = "point1", flightRadius = 15, position = new Vector3(0, 100, 0), use = false }, new CustomSpawnPoints { name = "point2", flightRadius = 15, position = new Vector3(0, 200, 0), use = false } };

            [JsonProperty("Drop items list")]
            public List<ItemsList> dropItemsList = new List<ItemsList> { new ItemsList { name = "metal.fragments", dropChance = 100, minAmount = 50, maxAmount = 300, skinID = 0 }, new ItemsList { name = "metal.refined", dropChance = 100, minAmount = 5, maxAmount = 10, skinID = 0 } ,
            new ItemsList { name = "scrap", dropChance = 100, minAmount = 10, maxAmount = 20, skinID = 0 }, new ItemsList { name = "techparts", dropChance = 50, minAmount = 1, maxAmount = 2, skinID = 0 }};


        }
        private List<GameObject> spawnPositions = new List<GameObject>();
        private List<Drone> dronesList = new List<Drone>();

        private void OnServerInitialized()
        {
            RemoveParentMissing();
            timer.Once(5f, () => { RemoveParentMissing(); });
            LoadConfig();

            Configuration.droneSpeed *= 1.68f;
            sense = 1.2f * 1.68f;
            offset = new Vector3(0, -0.22f, 0);

            for (int i = 0; i < Configuration.monumentSettings.Count; i++)
                foreach (var monument in TerrainMeta.Path.Monuments)
                    if (monument.name.Contains(Configuration.monumentSettings[i].name))
                        if (Configuration.monumentSettings[i].use)
                        {
                            Vector3 pos = monument.transform.position + Vector3.up * Configuration.monumentSettings[i].offset.y + monument.transform.forward * Configuration.monumentSettings[i].offset.x + monument.transform.right * Configuration.monumentSettings[i].offset.z;
                            AddSpawn(pos, Configuration.monumentSettings[i].flightRadius);
                        }

            for (int i = 0; i < Configuration.customSpawnPoints.Count; i++)
                if (Configuration.customSpawnPoints[i].use)
                    AddSpawn(Configuration.customSpawnPoints[i].position, Configuration.customSpawnPoints[i].flightRadius);



            Puts(spawnPositions.Count.ToString() + " spawn points have been created on the map");

            timer.Every(1f, () =>
                 {
                     for (int i = 0; i < dronesList.Count; i++)
                     {
                         DroneAI droneAI = dronesList[i].GetComponent<DroneAI>();
                         if (Vector3.Distance(dronesList[i].transform.position, new Vector3(droneAI.homePosition.x, dronesList[i].transform.position.y, droneAI.homePosition.z)) > droneAI.radius)
                             DroneTarget(droneAI.parent as BaseEntity, droneAI);

                         foreach (BasePlayer player in BasePlayer.activePlayerList)
                             if (player.transform.position.y < dronesList[i].transform.position.y)
                                 if (Vector3.Distance(player.transform.position, dronesList[i].transform.position) < Configuration.attackRange)
                                 {
                                     float distance = Vector3.Distance(player.transform.position, dronesList[i].transform.position);
                                     Vector3 start = dronesList[i].transform.position + dronesList[i].transform.forward * 0.3f;
                                     RaycastHit check;

                                     Vector3 direction = new Vector3(player.transform.position.x - start.x, player.transform.position.y - start.y, player.transform.position.z - start.z);
                                     if (Physics.Raycast(start, direction, out check, 999, layerS))
                                     {
                                         BaseEntity entityPlayer = player.GetEntity();
                                         BaseEntity entity = check.GetEntity();
                                         if (entity == entityPlayer)
                                         {
                                             int chance = UnityEngine.Random.Range(0, 100);
                                             if (chance < Configuration.droneAggressiveness)
                                             {
                                                 if (droneAI.turret != null)
                                                     droneAI.turret.target = player;

                                                 if (Configuration.throwGrenades)
                                                 {
                                                     droneAI.targetPosition = new Vector3(player.transform.position.x, dronesList[i].transform.position.y, player.transform.position.z);
                                                     droneAI.parent.transform.LookAt(droneAI.targetPosition);
                                                     string prefab = "assets/prefabs/weapons/f1 grenade/grenade.f1.deployed.prefab";
                                                     Vector3 offset = dronesList[i].transform.forward * 0.4f + Vector3.down * 0.4f;
                                                     var gren = GameManager.server.CreateEntity(prefab, start + offset);
                                                     gren.Spawn();
                                                     gren.creatorEntity = dronesList[i];
                                                     Rigidbody rig = gren.GetComponent<Rigidbody>();
                                                     rig.velocity = Vector3.Normalize(direction) * distance / 1.8f;

                                                     TimedExplosive exp = gren as TimedExplosive;
                                                     exp.SetDamageScale(Configuration.dmgScale);

                                                     //prefab = "assets/bundled/prefabs/fx/impacts/additive/fire.prefab";
                                                     //Effect.server.Run(prefab, start + offset);
                                                 }

                                                 break;
                                             }
                                         }
                                     }


                                 }

                     }


                 });

            timer.Every(5f, () =>
            {
                for (int i = 0; i < spawnPositions.Count; i++)
                {
                    SpawnSettings settings = spawnPositions[i].GetComponent<SpawnSettings>();
                    settings.timer--;
                    if (settings.timer == 0)
                        DroneSpawn(spawnPositions[i].transform.position, settings);
                }

                for (int i = 0; i < dronesList.Count; i++)
                {
                    DroneAI droneAI = dronesList[i].GetComponent<DroneAI>();
                    DroneTarget(droneAI.parent as BaseEntity, droneAI);
                }
            });


        }

        void SaveConfig(object config) => Config.WriteObject(config, true);

        void LoadConfig()
        {
            base.Config.Settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
            Configuration = Config.ReadObject<ConfigData>();
            SaveConfig(Configuration);
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData();
            SaveConfig(config);
        }

        private void AddSpawn(Vector3 pos, int radius)
        {
            GameObject obj = new GameObject();
            obj.transform.position = pos;
            SpawnSettings settings = obj.gameObject.AddComponent<SpawnSettings>();
            settings.radius = radius;
            settings.timer = GetRespawnTime();
            spawnPositions.Add(obj);
        }

        private void DroneTarget(BaseEntity drone, DroneAI droneAI)
        {
            droneAI.targetPosition = new Vector3(UnityEngine.Random.Range(-droneAI.radius, droneAI.radius) + droneAI.homePosition.x, droneAI.homePosition.y, UnityEngine.Random.Range(-droneAI.radius, droneAI.radius) + droneAI.homePosition.z);
            droneAI.altitude = droneAI.homePosition.y + UnityEngine.Random.Range(0, altitude);
        }

        private void DronesAI()
        {
            for (int i = 0; i < dronesList.Count; i++)
            {
                drone = dronesList[i];
                droneAI = drone.GetComponent<DroneAI>();
                rigidbody = droneAI.parent.GetComponent<Rigidbody>();

                if (droneAI.health < 1 || !Configuration.noCollision)
                {
                    if (Physics.Raycast(drone.transform.position + offset, drone.transform.forward, out check, sense, layerS))
                        if (drone)
                            CheckCollider(check, drone);

                    if (Physics.Raycast(drone.transform.position + offset, Vector3.down, out check, sense, layerS))
                        if (drone)
                            CheckCollider(check, drone);
                }

                lTargetDir = new Vector3(droneAI.targetPosition.x, drone.transform.position.y + UnityEngine.Random.Range(-0.5f, 0.5f), droneAI.targetPosition.z) - drone.transform.position;
                body = droneAI.parent as BaseEntity;
                body.transform.rotation = Quaternion.RotateTowards(body.transform.rotation, Quaternion.LookRotation(lTargetDir), 20f);

                rigidbody.velocity += Vector3.up * (droneAI.altitude - drone.transform.position.y) / 3f * droneAI.upVelocity + Vector3.up * droneAI.upVelocity;
                rigidbody.velocity += drone.transform.forward * droneAI.speed;


            }

        }

        private void CheckCollider(RaycastHit check, Drone drone)
        {
            if (check.GetEntity())
                if (check.GetEntity().name == "assets/prefabs/weapons/f1 grenade/grenade.f1.deployed.prefab" || check.GetEntity().name == "tdDroneCollider")
                    return;

            DroneDead(drone);
            dronesList.Remove(drone);
            drone.Kill();
        }

        private int GetRespawnTime()
        {
            int respawnTime = (int)UnityEngine.Random.Range(Configuration.minRaspawnTime * 12, Configuration.maxRaspawnTime * 12 + 0.1f);
            return respawnTime;
        }

        private void DroneSpawn(Vector3 position, SpawnSettings settings)
        {
            string dronePrefab = "assets/prefabs/misc/marketplace/drone.delivery.prefab";
            var drone = GameManager.server.CreateEntity(dronePrefab, position) as DeliveryDrone;
            drone.Spawn();
            drone.pickup.enabled = false;
            if (drone == null)
                return;
            drone.CancelInvoke(drone.Think);
            SetupDeliveryDrone(drone);
            drone.name = "tdDeliveryDrone";

            dronePrefab = "assets/prefabs/deployable/drone/drone.deployed.prefab";
            var colliderDrone = GameManager.server.CreateEntity(dronePrefab, position) as Drone;
            colliderDrone.Spawn();
            colliderDrone.pickup.enabled = false;
            colliderDrone.SetParent(drone);
            colliderDrone.transform.localPosition = Vector3.zero;
            DroneAI droneAI = colliderDrone.gameObject.AddComponent<DroneAI>();
            Rigidbody colRigidbody = colliderDrone.GetComponent<Rigidbody>();
            colRigidbody.isKinematic = true;
            colliderDrone.name = "tdDroneCollider";
            drone.name = "tdDroneBody";
            colliderDrone._maxHealth = 999999;
            colliderDrone.health = 999999;

            droneAI.homePosition = drone.transform.position;
            droneAI.altitude = droneAI.homePosition.y + UnityEngine.Random.Range(0, 10);
            droneAI.targetPosition = new Vector3(UnityEngine.Random.Range(-droneAI.radius, droneAI.radius) + droneAI.homePosition.x, droneAI.homePosition.y, UnityEngine.Random.Range(-droneAI.radius, droneAI.radius) + droneAI.homePosition.z);
            drone.transform.LookAt(new Vector3(droneAI.targetPosition.x, drone.transform.position.y, droneAI.targetPosition.z));

            droneAI.radius = settings.radius;
            droneAI.health = Configuration.droneHealth;
            droneAI.speed = Configuration.droneSpeed;
            droneAI.upVelocity = 1.3f;
            droneAI.parent = drone;
            droneAI.settings = settings;
            dronesList.Add(colliderDrone);

            Drone dr = colliderDrone as Drone;
            dr.InitializeControl(new CameraViewerId(0, 0));
            InputMessage message = new InputMessage() { buttons = 128 };
            InputState input = new InputState() { current = message };
            dr.UserInput(input, new CameraViewerId(0, 0));
            dr.currentInput.throttle = 0;


            if (Configuration.searchLight || Configuration.turret)
                AddSphere(colliderDrone, droneAI);

            if (dronesList.Count == 1)
                dronesMainTimer = timer.Every(0.24f, () => DronesAI());


        }

        private void AddSphere(BaseEntity entity, DroneAI droneAI)
        {

            string searchLightPrefab = "assets/prefabs/deployable/search light/searchlight.deployed.prefab";
            string spherePrefab = "assets/prefabs/visualization/sphere.prefab";

            SphereEntity sphereEntity = GameManager.server.CreateEntity(spherePrefab, new Vector3(0, -100, 0), Quaternion.identity) as SphereEntity;
            sphereEntity.EnableSaving(true);
            sphereEntity.EnableGlobalBroadcast(false);
            sphereEntity.SetParent(entity);
            sphereEntity.Spawn();
            sphereEntity.currentRadius = 0.1f;
            sphereEntity.lerpRadius = 0.1f;
            sphereEntity.UpdateScale();
            sphereEntity.SendNetworkUpdateImmediate();

            timer.Once(3f, () =>
            {
                if (sphereEntity != null)
                    sphereEntity.transform.localPosition = new Vector3(0, 0.2f, 0.2f);
            });

            if (Configuration.searchLight)
            {
                SearchLight searchLight = GameManager.server.CreateEntity(searchLightPrefab, sphereEntity.transform.position) as SearchLight;
                searchLight.pickup.enabled = false;

                foreach (var mesh in searchLight.GetComponentsInChildren<Collider>())
                    UnityEngine.Object.DestroyImmediate(mesh);

                UnityEngine.Object.DestroyImmediate(searchLight.GetComponent<DestroyOnGroundMissing>());
                UnityEngine.Object.DestroyImmediate(searchLight.GetComponent<GroundWatch>());
                searchLight.Spawn();
                searchLight._maxHealth = 99999999f;
                searchLight._health = 99999999f;
                searchLight.SendNetworkUpdate();
                searchLight.SetFlag(BaseEntity.Flags.Reserved5, true);
                searchLight.SetFlag(BaseEntity.Flags.Busy, true);
                searchLight.SetFlag(IOEntity.Flag_HasPower, !searchLight.IsPowered());
                searchLight.SetParent(sphereEntity);
                searchLight.transform.localPosition = Vector3.zero;
                searchLight.transform.localRotation = Quaternion.Euler(-20, 180, 180);
            }

            if (Configuration.turret)
            {
                var turretMain = GameManager.server.CreateEntity("assets/prefabs/npc/autoturret/autoturret_deployed.prefab", Vector3.zero, new Quaternion(180, 0, 0, 0), true);
                turretMain.SetParent(sphereEntity);
                turretMain.transform.localPosition = new Vector3(0, -1.9f, 0.3f);

                foreach (var mesh in turretMain.GetComponentsInChildren<CapsuleCollider>())
                    UnityEngine.Object.DestroyImmediate(mesh);

                turretMain.Spawn();


                AutoTurret turret = turretMain.GetComponent<AutoTurret>();
                turret.sightRange = Configuration.attackRange;

                var gun = ItemManager.Create(ItemManager.FindItemDefinition(Configuration.turretWeaponName));
                gun.MoveToContainer(turret.inventory, 0);

                turret.UpdateAttachedWeapon();
                turret.CancelInvoke(turret.UpdateAttachedWeapon);

                var weapon = turret.GetAttachedWeapon();
                weapon.primaryMagazine.contents = 999999;
                weapon.damageScale = Configuration.turretDmgScale;

                turret.SetFlag(BaseEntity.Flags.Reserved8, true);
                turret.InitiateStartup();
                turret.creatorEntity = entity;

                droneAI.turret = turret;


            }


        }


        private static void SetupDeliveryDrone(DeliveryDrone deliveryDrone)
        {
            deliveryDrone.EnableSaving(false);
            deliveryDrone.EnableGlobalBroadcast(false);
            deliveryDrone.CancelInvoke(deliveryDrone.Think);
            deliveryDrone.lifestate = BaseCombatEntity.LifeState.Dead;
            if (deliveryDrone._mapMarkerInstance != null)
                deliveryDrone._mapMarkerInstance.Kill();
        }

        private void OnEntityKill(Drone entity)
        {
            if (entity.name.Contains("tdDroneCollider"))
            {
                BaseEntity drone = dronesList.Find(item => item == entity);
                if (drone)
                {
                    DroneDead(entity);
                    dronesList.Remove(entity);
                }
            }

        }

        private void DroneDead(Drone entity)
        {

            Vector3 pos = entity.transform.position;
            entity.transform.position += Vector3.down * 9999;
            
            DroneAI droneAI = entity.GetComponent<DroneAI>();

            if (droneAI)
            {
                droneAI.settings.timer = GetRespawnTime();
                NextFrame(() => { if (droneAI.parent) droneAI.parent.Kill(); });
            }

            string expPrefab = "assets/prefabs/weapons/f1 grenade/grenade.f1.deployed.prefab";

            BaseEntity exlode = GameManager.server.CreateEntity(expPrefab, pos);
            TimedExplosive exp = exlode as TimedExplosive;
            exlode.Spawn();
            exp.SetDamageScale(Configuration.dmgScale);
            exp.Explode();

            expPrefab = "assets/bundled/prefabs/fx/gas_explosion_small.prefab";
            Effect.server.Run(expPrefab, pos);

            string npcPrefab = "assets/prefabs/deployable/small stash/small_stash_deployed.prefab";
            BaseEntity storage = GameManager.server.CreateEntity(npcPrefab, pos + Vector3.up);
            storage.Spawn();

            StorageContainer inv1 = storage.GetComponent<StorageContainer>();
            inv1.inventory.capacity = 99;
            BaseCombatEntity ent = storage.GetComponent<BaseCombatEntity>();
            StorageContainer inv = storage.GetComponent<StorageContainer>();

            if (Configuration.lootTableName != "")
                SimpleLootTable?.Call("GetSetItems", inv, Configuration.lootTableName, Configuration.tableMinItems, Configuration.tableMaxItems, 1f);
            else
            {
                for (int i = 0; i < Configuration.dropItemsList.Count; i++)
                {
                    int chanсe = (int)UnityEngine.Random.Range(0, 100);
                    if (chanсe < Configuration.dropItemsList[i].dropChance)
                    {
                        ItemDefinition item = ItemManager.FindItemDefinition(Configuration.dropItemsList[i].name);
                        ulong skinID = Configuration.dropItemsList[i].skinID;

                        if (item)
                        {
                            int amount = (int)UnityEngine.Random.Range(Configuration.dropItemsList[i].minAmount, Configuration.dropItemsList[i].maxAmount + 1);
                            inv.inventory.AddItem(item, amount, skinID);
                        }
                        else
                            PrintWarning("!!! In the config file of the list of drop items, there is an error in the name of the item - " + Configuration.dropItemsList[i].name + " !!!");
                    }
                }
            }

            ent.DieInstantly();
            if (dronesList.Count == 0) RemoveMainTimer();



        }

        private void OnEntityTakeDamage(Drone drone, HitInfo hitInfo)
        {

            if (drone.name == "tdDroneCollider")
            {
                int damage = 1;

                if (hitInfo.Initiator)
                    if (hitInfo.Initiator.name == "assets/prefabs/npc/autoturret/autoturret_deployed.prefab" || hitInfo.Initiator.name == "assets/bundled/prefabs/fireball_small.prefab")
                        return;

                if (hitInfo.WeaponPrefab && hitInfo.WeaponPrefab.ToString().Contains("rocket"))
                    damage = Configuration.rocketDamage;

                DroneAI droneAI = drone.gameObject.GetComponentInChildren<DroneAI>();

                if (droneAI)
                {
                    droneAI.health = droneAI.health - damage;
                    if (hitInfo.InitiatorPlayer)
                    {
                        var str = "assets/bundled/prefabs/fx/impacts/additive/explosion.prefab";
                        Effect.server.Run(str, drone.transform.position);
                    }

                    if (droneAI.health <= 0)
                    {
                        if (Configuration.immediatelyExp)
                        {
                            DroneDead(drone);
                            dronesList.Remove(drone);
                            drone.Kill();
                            return;
                        }

                        droneAI.upVelocity = 0;

                        var str = "assets/bundled/prefabs/fireball_small.prefab";
                        BaseEntity fire = GameManager.server.CreateEntity(str, drone.transform.position);
                        fire.Spawn();

                        UnityEngine.Object.Destroy(fire.gameObject.GetComponent<Rigidbody>());
                        fire.SetParent(drone);
                        fire.transform.localPosition = new Vector3(0, 0, 0);

                    }
                }
            }
        }

        private void RemoveParentMissing()
        {
            var foundObjects = new List<BaseNetworkable>();
            var reportLines = new List<string>();

            foreach (var networkable in BaseNetworkable.serverEntities)
            {
                if (!networkable.parentEntity.IsSet())
                    continue;

                var parentEntityId = networkable.parentEntity.uid;
                string pas = parentEntityId.ToString();
                if (pas == "0")
                    continue;

                var parent = networkable.GetParentEntity();
                if (parent == null && networkable != null && !networkable.IsDestroyed)
                {
                    foundObjects.Add(networkable);
                    reportLines.Add($"{networkable.net.ID} (parent: {parentEntityId}) | {networkable.GetType()} | {networkable.PrefabName} @ {networkable.transform.position}");
                }
            }

            if (reportLines.Count == 0)
                return;


            var reportString = string.Join("\n", reportLines);

            foreach (var networkable in foundObjects)
                networkable.Kill();



        }

        public void RemoveMainTimer()
        {
            if (dronesMainTimer != null)
                dronesMainTimer.Destroy();
        }

        void Unload() { RemoveAllDrones(); }
        void OnServerShutdown() { RemoveAllDrones(); }

        private void RemoveAllDrones()
        {
            foreach (var item in dronesList)
                if (item)
                {
                    item.name = "null";
                    DroneAI droneAI = item.GetComponent<DroneAI>();
                    if (droneAI)
                        if (droneAI.parent)
                            droneAI.parent.Kill();
                }


            dronesList.Clear();
            RemoveMainTimer();
        }

        [Command("dre_addpoint")]
        private void dre_addpoint(IPlayer iplayer, string command, string[] args)
        {
            if (!iplayer.IsServer)
                if (iplayer.IsAdmin)
                {
                    var player = (BasePlayer)iplayer.Object;
                    string _name = "DefaultName";
                    int _flightRadius = 15;
                    Vector3 _position = player.transform.position;
                    bool _use = true;

                    if (args.Length > 0)
                        _name = args[0];
                    if (args.Length > 1)
                        _flightRadius = int.Parse(args[1]);
                    if (args.Length > 2)
                        _use = bool.Parse(args[2]);

                    CustomSpawnPoints point = new CustomSpawnPoints { name = _name, flightRadius = _flightRadius, position = _position, use = _use };
                    Configuration.customSpawnPoints.Add(point);
                    SaveConfig(Configuration);
                    player.ChatMessage("Spawn point was saved successfully");

                    if (point.use)
                        AddSpawn(point.position, point.flightRadius);
                    NextFrame(() => { dreshowzone(player.IPlayer); });

                }
        }

        [Command("dre_removepoint")]
        private void dre_removepoint(IPlayer iplayer, string command, string[] args)
        {
            if (!iplayer.IsServer)
                if (iplayer.IsAdmin)
                {
                    var player = (BasePlayer)iplayer.Object;
                    for (int i = 0; i < Configuration.customSpawnPoints.Count; i++)
                        if (Vector3.Distance(Configuration.customSpawnPoints[i].position, player.transform.position) < 5)
                        {
                            GameObject currentItem = spawnPositions.Find(item => item.transform.position == Configuration.customSpawnPoints[i].position);
                            spawnPositions.Remove(currentItem);

                            Configuration.customSpawnPoints.Remove(Configuration.customSpawnPoints[i]);
                            player.ChatMessage("Spawn point was successfully removed");
                            SaveConfig(Configuration);
                        }

                }
        }

        [Command("dreshowpoints")]
        private void dreshowpoints(IPlayer iplayer)
        {
            if (!iplayer.IsServer)
                if (iplayer.IsAdmin)
                {
                    var player = (BasePlayer)iplayer.Object;
                    foreach (var item in spawnPositions)
                        player.SendConsoleCommand("ddraw.text", 10f, Color.green, item.transform.position, "<size=30>+</size>");
                }
        }

        [Command("dreshowdrones")]
        private void dreshowdrones(IPlayer iplayer)
        {
            if (!iplayer.IsServer)
                if (iplayer.IsAdmin)
                {
                    var player = (BasePlayer)iplayer.Object;
                    foreach (var item in dronesList)
                        player.SendConsoleCommand("ddraw.text", 10f, Color.blue, item.transform.position, "<size=30>+</size>");
                }
        }

        [Command("dreshowzone")]
        private void dreshowzone(IPlayer iplayer)
        {
            if (!iplayer.IsServer)
                if (iplayer.IsAdmin)
                {
                    var player = (BasePlayer)iplayer.Object;
                    foreach (var item in spawnPositions)
                        if (Vector3.Distance(player.transform.position, item.transform.position) < 150)
                        {
                            int time = 30;
                            SpawnSettings settings = item.GetComponent<SpawnSettings>();
                            player.SendConsoleCommand("ddraw.text", time, Color.red, item.transform.position, "<size=30>+</size>");
                            Vector3 pos;

                            float x = settings.radius * 1.4f;
                            for (float i = 0; i < x * 2; i += 3)
                            {
                                pos = new Vector3(item.transform.position.x - x + i, item.transform.position.y, item.transform.position.z - x);
                                player.SendConsoleCommand("ddraw.text", time, Color.white, pos, "<size=30>x</size>");
                                pos = new Vector3(item.transform.position.x - x + i, item.transform.position.y, item.transform.position.z + x);
                                player.SendConsoleCommand("ddraw.text", time, Color.white, pos, "<size=30>x</size>");
                                pos = new Vector3(item.transform.position.x - x, item.transform.position.y, item.transform.position.z - x + i);
                                player.SendConsoleCommand("ddraw.text", time, Color.white, pos, "<size=30>x</size>");
                                pos = new Vector3(item.transform.position.x + x, item.transform.position.y, item.transform.position.z - x + i);
                                player.SendConsoleCommand("ddraw.text", time, Color.white, pos, "<size=30>x</size>");
                            }
                        }
                }
        }

        [Command("drerespawndrones")]
        private void drerespawndrones(IPlayer iplayer)
        {
            var player = (BasePlayer)iplayer.Object;
            if (iplayer.IsAdmin)
            {
                RemoveAllDrones();
                foreach (var item in spawnPositions)
                {
                    SpawnSettings settings = item.gameObject.GetComponent<SpawnSettings>();
                    settings.timer = 1;
                }
            }
        }

        [Command("dreremovedrones")]
        private void dreremovedrones(IPlayer iplayer)
        {
            var player = (BasePlayer)iplayer.Object;
            if (iplayer.IsAdmin)
            {
                RemoveAllDrones();
                foreach (var item in spawnPositions)
                {
                    SpawnSettings settings = item.gameObject.GetComponent<SpawnSettings>();
                    settings.timer = GetRespawnTime();
                }
            }
        }
    }
}