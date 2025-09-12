using Network;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("DroneTaxi", "Cameron", "1.0.15")]
    [Description("Get a taxi from one point of the map to another ")]
    public class DroneTaxi : CovalencePlugin
    {
        private static int _groundLayer = LayerMask.GetMask("Terrain", "Default", "Construction", "Deployed", "Construction Trigger", "Trigger", "Invisible");
        public static Oxide.Core.Libraries.Timer customTimer = Interface.Oxide.GetLibrary<Oxide.Core.Libraries.Timer>();
        public static int flatFeeX = 10;
        public static int varableFeeX = 1;
        public static int flatFeeXL = 50;
        public static int varableFeeXL = 5;
        public static int flatFeeLUX = 100;
        public static int varableFeeLUX = 10;
        public static float coolDown = 60;
        public static string command = "taxi";
        public static bool ignoreBuildingBlocked = false;
        Dictionary<BasePlayer, DateTime> lastCommand = new Dictionary<BasePlayer, DateTime>();

        Dictionary<BaseMountable, Taxi> activeStrikes = new Dictionary<BaseMountable, Taxi>();

        private void Init()
        {
            permission.RegisterPermission("DroneTaxi.X", this);
            permission.RegisterPermission("DroneTaxi.XL", this);
            permission.RegisterPermission("DroneTaxi.LUX", this);

            flatFeeX = int.Parse(Config["flatFeeX"].ToString());
            varableFeeX = int.Parse(Config["varableFeeX"].ToString());
            flatFeeXL = int.Parse(Config["flatFeeXL"].ToString());
            varableFeeXL = int.Parse(Config["varableFeeXL"].ToString());
            flatFeeLUX = int.Parse(Config["flatFeeLUX"].ToString());
            varableFeeLUX = int.Parse(Config["varableFeeLUX"].ToString());
            coolDown = int.Parse(Config["coolDown"].ToString());

            if (Config["command"] == null)
            {
                Config["command"] = "taxi";
                SaveConfig();
            }
            if (Config["ignoreBuildingBlocked"] == null)
            {
                Config["ignoreBuildingBlocked"] = false;
                SaveConfig();
            }

            command = Config["command"].ToString();
            ignoreBuildingBlocked = bool.Parse(Config["ignoreBuildingBlocked"].ToString());
            AddUniversalCommand(command, "AddStrike");
        }
        protected override void LoadDefaultConfig()
        {
            LogWarning("Creating a new configuration file for DroneTaxi");
            Config["flatFeeX"] = 10;
            Config["varableFeeX"] = 1;
            Config["flatFeeXL"] = 50;
            Config["varableFeeXL"] = 5;
            Config["flatFeeLUX"] = 100;
            Config["varableFeeLUX"] = 10;
            Config["coolDown"] = 30;
            Config["command"] = "taxi";
            Config["ignoreBuildingBlocked"] = false;
        }
        void OnEntityMounted(BaseMountable entity, BasePlayer player)
        {
            if (activeStrikes.ContainsKey(entity))
            {
                Taxi taxi = activeStrikes[entity];
                player.ChatMessage($"[Drone Taxi]<color=#FFBF00> Welcome on board </color>{player.displayName}<color=#FFBF00>!\nIt costs </color>{taxi.InitalPrice}<color=#FFBF00> scrap to set your destination plus </color>{taxi.PricePerMin}<color=#FFBF00> scrap per </color>seconds<color=#FFBF00> of flight to use the drone taxi!\n<color=#FA5E5E>\n<b>You will be ejected if you can not afford your fair!</color>\n\n <color=#FFBF00>To set your destination open your map and </color>right click<color=#FFBF00> where you want to go.\nIll try my best to avoid all obstacles but you may want to change your desitination to help</color>");
                PlayEffect("assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab", player);
            }
        }
        

        [Command("closetaxi")]
        private void CloseTaxiPannel(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            CuiHelper.DestroyUi(player, "TaxiPanel");
        }
        void OnEntityDismounted(BaseMountable entity, BasePlayer player)
        {
            if (activeStrikes.ContainsKey(entity))
            {
                //player.transform.position = entity.transform.position;//stop people from teleporting to the sky
                player.ChatMessage($"[Drone Taxi]<color=#FFBF00> Thanks for riding! Come back any time</color>");
                activeStrikes[entity].KillDrone();
                PlayEffect("assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab", player);
            }
        }
        object OnMapMarkerAdd(BasePlayer player, MapNote note)
        {
            if (player.isMounted)
            {
                BaseMountable mount = player.GetMounted();
                
                if (mount != null && activeStrikes.ContainsKey(mount))
                {
                    BuildingPrivlidge priv = GetBuildingPrivilege(note);
                    if (priv != null && !priv.IsAuthed(player))
                    {
                        player.ChatMessage($"Drone Taxi]<color=#FFBF00> Your target area is </color>invalid!<color=#FFBF00> I can not drop you at building blocked areas!");
                        PlayEffect("assets/prefabs/npc/autoturret/effects/targetacquired.prefab", player);
                        return null;
                    }
                    RaycastHit hitInfo;
                    if (Physics.Raycast(note.worldPosition + (Vector3.up * 450), Vector3.down, out hitInfo, 500))
                    {
                        
                        if (ignoreBuildingBlocked && hitInfo.collider != null && hitInfo.collider.name == "prevent_building_sphere" || hitInfo.collider.name == "ZoneManager")
                        {
                            Physics.Raycast(new Vector3(note.worldPosition.x, hitInfo.point.y - 5.0f, note.worldPosition.z), Vector3.down, out hitInfo, 500);
                           
                        }
                        if(hitInfo.point.y - TerrainMeta.HeightMap.GetHeight(note.worldPosition) > 10)
                        {
                            player.ChatMessage($"[Drone Taxi]<color=#FFBF00> We do not have permission from facepunch to opperate over monuments. Please select another spot near the monument");
                            PlayEffect("assets/prefabs/npc/autoturret/effects/targetacquired.prefab", player);
                            return null;
                        }
                        
                        
                    }                    
                    
                    Taxi strike = activeStrikes[mount];
                    strike.SetTargetPoint(note.worldPosition,player);
                    
                }
            }
            

            return null;
        }
        public BuildingPrivlidge GetBuildingPrivilege(MapNote obb)
        {
            BuildingBlock buildingBlock1 = (BuildingBlock)null;
            BuildingPrivlidge buildingPrivlidge = (BuildingPrivlidge)null;
            System.Collections.Generic.List<BuildingBlock> list = Facepunch.Pool.GetList<BuildingBlock>();
            Vis.Entities<BuildingBlock>(obb.worldPosition, 16f, list, 2097152);
            for (int index = 0; index < list.Count; ++index)
            {
                BuildingBlock buildingBlock2 = list[index];
                if (buildingBlock2.IsOlderThan((BaseEntity)buildingBlock1) && (double)Vector3.Distance(obb.worldPosition, buildingBlock2.transform.position) <= 16.0)
                {
                    BuildingManager.Building building = buildingBlock2.GetBuilding();
                    if (building != null)
                    {
                        BuildingPrivlidge buildingPrivilege = building.GetDominatingBuildingPrivilege();
                        if (!((UnityEngine.Object)buildingPrivilege == (UnityEngine.Object)null))
                        {
                            buildingBlock1 = buildingBlock2;
                            buildingPrivlidge = buildingPrivilege;
                        }
                    }
                }
            }
            Facepunch.Pool.FreeList<BuildingBlock>(ref list);
            return buildingPrivlidge;
        }
        private void AddStrike(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            
            CuiHelper.DestroyUi(player, "TaxiPanel");
            BuildingPrivlidge priv = player.GetBuildingPrivilege();
            if(priv != null && !priv.IsAuthed(player))
            {
                player.ChatMessage($"[Drone Taxi]<color=#FFBF00> Sorry you can not call drone taxis in </color>building blocked<color=#FFBF00> areas!");
                PlayEffect("assets/prefabs/npc/autoturret/effects/targetacquired.prefab", player);
                return;
                
            }
            if (!player.IsOutside())
            {
                player.ChatMessage($"[Drone Taxi]<color=#FFBF00> Sorry you cant call for drones </color>inside buildings!");
                PlayEffect("assets/prefabs/npc/autoturret/effects/targetacquired.prefab", player);
                return;
            }

            var rotatedVector = player.GetNetworkRotation() * Vector3.forward;
            Vector3 newEnd = player.transform.position + (rotatedVector.normalized * 4);
            RaycastHit hitInfo;
            
            if (Physics.Raycast(newEnd + (Vector3.up * 450), Vector3.down, out hitInfo, 500))
            {
                if (hitInfo.point.y - TerrainMeta.HeightMap.GetHeight(newEnd) > 10 && hitInfo.collider.name != "ZoneManager")
                {
                    player.ChatMessage($"[Drone Taxi]<color=#FFBF00> Sorry cant call a drone taxi in this location! We do not have permission from facepunch to opperate over monuments</color>");
                    PlayEffect("assets/prefabs/npc/autoturret/effects/targetacquired.prefab", player);
                    return;
                }
                
            }
            if (lastCommand.ContainsKey(player))
            {
                DateTime lastMessage = lastCommand[player];
                if (lastMessage + TimeSpan.FromSeconds(coolDown) > DateTime.UtcNow)
                {
                    player.ChatMessage($"[Drone Taxi]<color=#FFBF00> You recently requested a taxi! Please try again shortly</color>");
                    return;
                }
                
            }
            int result;
            if (args.Length <= 0 || !int.TryParse(args[0], out result))
            {
                MakeUi(player);
                return;
            }
            
            if (result < 0 || result > 2) { player.ChatMessage($"[Drone Taxi]<color=#FFBF00> That doesnt look like a drone we have in stock</color>"); return; }

            if((result == 0 && !iplayer.HasPermission("DroneTaxi.X")) || (result == 1 && !iplayer.HasPermission("DroneTaxi.XL")) || (result == 2 && !iplayer.HasPermission("DroneTaxi.LUX")))
            {
                player.ChatMessage($"[Drone Taxi]<color=#FFBF00> You dont have permission for this type of Drone Taxi</color>");
                return;
            }
            Taxi t = new Taxi(newEnd, player.GetNetworkRotation(), result);
            activeStrikes.Add(t.seat,t);
            lastCommand.Remove(player);
            lastCommand.Add(player, DateTime.UtcNow);
            player.ChatMessage($"[Drone Taxi]<color=#FFBF00> Taxi inbound!</color>");
            PlayEffect("assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab", player);
        }
        private static void PlayEffect(string effectPrefab,BasePlayer player)
        {
            Effect effect = new Effect();
            effect.Init(Effect.Type.Generic, player.transform.position, Vector3.zero);
            effect.pooledString = effectPrefab;
            EffectNetwork.Send(effect, player.net.connection);
        }
        public class Taxi
        {
            float heightAboveGround = 40;
            Vector3 direction;
            Drone drone;
            int pricePerMin;
            int initalPrice;

            List<BaseMountable> seats = new List<BaseMountable>();
            public BaseMountable seat {get; private set;}
            public int PricePerMin { get { return pricePerMin; } }
            public int InitalPrice { get { return initalPrice; } }

            Oxide.Core.Libraries.Timer.TimerInstance hieghtTimer;
            Oxide.Core.Libraries.Timer.TimerInstance afkTimer;
            public Taxi(Vector3 spawnPoint, Quaternion rot, int type = 1)
            {
                afkTimer = customTimer.Once(300.0f, () =>
                 {
                     DestroyTaxi();
                 });
                
                if (type == 0)
                {
                    Vector3 ground = GetGround(spawnPoint);
                    UnityEngine.Debug.Log(ground);
                    float goundY = ground.y;
                    Vector3 intialSpawnPoint = new Vector3(spawnPoint.x, ground.y + 150, spawnPoint.z);
                    Vector3 pickUpPoint = new Vector3(spawnPoint.x, ground.y + 0.5f, spawnPoint.z);
                    drone = GameManager.server.CreateEntity("assets/prefabs/deployable/drone/drone.deployed.prefab", intialSpawnPoint, new Quaternion(), true) as Drone;
                    //drone.targetPosition = endPoint;
                    //drone.altitudeAcceleration = 50;
                    //drone.movementAcceleration = 5f;
                    drone.targetPosition = pickUpPoint;
                    drone.Spawn();
                    drone.pickup.enabled = false;
                    drone.EnableGlobalBroadcast(true);
                    drone.altitudeAcceleration = 50;
                    drone.EnableSaving(false);
                    drone.pickup.enabled = false;
                    drone.enableGrounding = false;
                    drone.collisionDisableTime = 0;
                    //drone.movementAcceleration = 20f;
                    
                    seat = GameManager.server.CreateEntity("assets/prefabs/deployable/chair/chair.deployed.prefab", new Vector3() + (Vector3.up / 10), new Quaternion()) as BaseMountable;

                    seat.Spawn();


                    //seat.GetComponent<Collider>().convex = true;
                    seat.SetParent((BaseEntity)drone);
                    seat.EnableGlobalBroadcast(true);
                    //seat.needsVehicleTick = true;
                    seat.EnableSaving(false);
                    seat.pickup.enabled = false;
                    seats.Add(seat);
                    pricePerMin = varableFeeX;
                    initalPrice = flatFeeX;

                    //seat.canWieldItems = true;
                }
                else if(type == 2)
                {
                    
                    Vector3 ground = GetGround(spawnPoint);
                    float goundY = ground.y;
                    Vector3 intialSpawnPoint = new Vector3(spawnPoint.x, ground.y + 150, spawnPoint.z);
                    Vector3 pickUpPoint = new Vector3(spawnPoint.x, ground.y + 0.5f, spawnPoint.z);
                    drone = GameManager.server.CreateEntity("assets/prefabs/deployable/drone/drone.deployed.prefab", intialSpawnPoint, rot, true) as Drone;

                    //drone.targetPosition = endPoint;
                    //drone.altitudeAcceleration = 50;
                    //drone.movementAcceleration = 2.5f;
                    drone.targetPosition = pickUpPoint;
                    drone.Spawn();
                    drone.pickup.enabled = false;
                    drone.EnableGlobalBroadcast(true);
                    drone.altitudeAcceleration = 50;
                    drone.EnableSaving(false);
                    drone.pickup.enabled = false;
                    drone.collisionDisableTime = 0;
                    //drone.movementAcceleration = 20f;
                    seat = GameManager.server.CreateEntity("assets/prefabs/misc/summer_dlc/beach_chair/beachchair.deployed.prefab", new Vector3(0,0.1f,0), new Quaternion()) as BaseMountable;
                    seat.Spawn();
                    //seat.GetComponent<MeshCollider>().convex = true;
                   
                    seat.SetParent((BaseEntity)drone);
                    
                    seat.EnableGlobalBroadcast(true);
                    //seat.needsVehicleTick = true;
                    seat.EnableSaving(false);
                    seat.pickup.enabled = false;
                    seats.Add(seat);
                    
                    BaseCombatEntity t = GameManager.server.CreateEntity("assets/prefabs/deployable/signs/sign.medium.wood.prefab", new Vector3(0.5f, 0.2f, 0), Quaternion.Euler(90, 0, 90)) as BaseCombatEntity;
                    t.Spawn();
                    
                    t.SetParent((BaseEntity)drone);
                    t.EnableGlobalBroadcast(true);
                    t.EnableSaving(false);
                    t.pickup.enabled = false;
                    pricePerMin = varableFeeLUX;
                    initalPrice = flatFeeLUX;


                }
                else
                {
                    Vector3 ground = GetGround(spawnPoint);
                    
                    float goundY = ground.y;
                    Vector3 intialSpawnPoint = new Vector3(spawnPoint.x, ground.y + 150, spawnPoint.z);
                    Vector3 pickUpPoint = new Vector3(spawnPoint.x, ground.y + 0.5f, spawnPoint.z);
                    drone = GameManager.server.CreateEntity("assets/prefabs/deployable/drone/drone.deployed.prefab", intialSpawnPoint, rot, true) as Drone;

                    //drone.targetPosition = endPoint;
                    //drone.altitudeAcceleration = 50;
                    //drone.movementAcceleration = 2.5f;
                    drone.targetPosition = pickUpPoint;
                    drone.Spawn();
                    drone.pickup.enabled = false;
                    drone.EnableGlobalBroadcast(true);
                    drone.altitudeAcceleration = 50;
                    drone.EnableSaving(false);
                    drone.pickup.enabled = false;
                    drone.collisionDisableTime = 0;
                    //drone.movementAcceleration = 20f;
                    seat = GameManager.server.CreateEntity("assets/prefabs/deployable/chair/chair.deployed.prefab", new Vector3() + (Vector3.up / 10), new Quaternion()) as BaseMountable;
                    seat.Spawn();
                    //seat.GetComponent<MeshCollider>().convex = true;
                    //UnityEngine.Debug.Log("jere");
                    seat.SetParent((BaseEntity)drone);
                    seat.EnableGlobalBroadcast(true);
                    //seat.needsVehicleTick = true;
                    seat.EnableSaving(false);
                    seat.pickup.enabled = false;
                    seats.Add(seat);
                    BaseCombatEntity t = GameManager.server.CreateEntity("assets/prefabs/deployable/signs/sign.medium.wood.prefab", new Vector3(0,0.25f,-0.5f), Quaternion.Euler(90,0,0)) as BaseCombatEntity;
                    t.Spawn();
                    //t.GetComponent<MeshCollider>().convex = true;

                    t.SetParent((BaseEntity)drone);
                    t.EnableGlobalBroadcast(true);
                    t.EnableSaving(false);
                    t.pickup.enabled = false;
                    BaseMountable seat2 = GameManager.server.CreateEntity("assets/prefabs/deployable/chair/chair.deployed.prefab", new Vector3(0.8f,0.1f,0), new Quaternion()) as BaseMountable;
                    seat2.Spawn();
                    //seat2.GetComponent<MeshCollider>().convex = true;

                    seat2.SetParent((BaseEntity)drone);
                    seat2.EnableGlobalBroadcast(true);
                    //seat2.needsVehicleTick = true;
                    seat2.EnableSaving(false);
                    seat2.pickup.enabled = false;
                    seats.Add(seat2);
                    BaseMountable seat3 = GameManager.server.CreateEntity("assets/prefabs/deployable/chair/chair.deployed.prefab", new Vector3(-0.8f, 0.1f,0), new Quaternion()) as BaseMountable;
                    seat3.Spawn();
                    //seat3.GetComponent<MeshCollider>().convex = true;

                    seat3.SetParent((BaseEntity)drone);
                    seat3.EnableGlobalBroadcast(true);
                    //seat3.needsVehicleTick = true;
                    seat3.EnableSaving(false);
                    seat3.pickup.enabled = false;
                    seats.Add(seat3);
                    //t.isMobile = true;
                    pricePerMin = varableFeeXL;
                    initalPrice = flatFeeXL;
                }
                
            }
            
            public void SetTargetPoint(Vector3 target, BasePlayer player)
            {
               
                if (!CannAffordAndCharge(player, InitalPrice)) { player.ChatMessage($"[Drone Taxi]<color=#FFBF00> Sorry you dont have enough</color> scrap<color=#FFBF00> to set your destination! Scrap required </color>{InitalPrice}"); PlayEffect("assets/prefabs/npc/autoturret/effects/targetacquired.prefab", player); return; }
                float targetHeight = GetGround(target).y + 0.5f;
                drone.targetPosition = new Vector3(target.x, targetHeight, target.z);
                direction = drone.targetPosition.Value - drone.transform.position;
                drone.altitudeAcceleration = 10;
                HeightCheck();
                if (afkTimer != null && !afkTimer.Destroyed)
                    afkTimer.Destroy();
            }
            private bool CannAffordAndCharge(BasePlayer player,int amount)
            {
                if (amount - player.inventory.GetAmount(-932201673) < 1)
                {
                    List<Item> items = new List<Item>();
                    
                    player.inventory.Take(items, -932201673, amount);
                    player.Command("note.inv", (object)-932201673, (object)(float)((double)amount * -1.0));
                    return true;
                }
                
                return false;
            }
            private Vector3 GetGround(Vector3 pos)
            {
                RaycastHit hitInfo;
                pos += new Vector3(0, 100, 0);

                if (Physics.Raycast(pos, Vector3.down, out hitInfo, 500, _groundLayer))
                    return hitInfo.point;

                return new Vector3();
            }
            private Vector3 GetBellow(Vector3 pos)
            {
                RaycastHit hitInfo;
                if (Physics.Raycast(pos, Vector3.down, out hitInfo, 250)) {
                    if ((ignoreBuildingBlocked && hitInfo.collider != null && hitInfo.collider.name == "prevent_building_sphere") || hitInfo.collider.name == "ZoneManager")
                    {
                        Physics.Raycast(hitInfo.point - (Vector3.up * 5), Vector3.down, out hitInfo, 500, _groundLayer);

                    }
                    return hitInfo.point;
                }

                return new Vector3();
            }
            private void HeightCheck()
            {
                if(hieghtTimer != null && !hieghtTimer.Destroyed)
                    hieghtTimer.Destroy();
                int count = 0;
                hieghtTimer = customTimer.Repeat(0.25f, 0, () =>
                {
                    if (drone == null) { hieghtTimer.Destroy(); return; }
                    HeightUpdate();
                    CheckEndZone();
                    if(count % 4 == 0)
                    {
                        BasePlayer player = seat.GetMounted();
                        if (player != null)
                        {
                            if (!CannAffordAndCharge(player, PricePerMin))
                            {
                                player.ChatMessage("[Drone Taxi]<color=#FFBF00> You dont have enough </color>scrap<color=#FFBF00> to complete the ride! Sorry no free rides!");
                                DestroyTaxi();
                                PlayEffect("assets/prefabs/locks/keypad/effects/lock.code.lock.prefab", player);
                            }
                            


                        }      
                        
                    }
                    foreach (var seat in seats)
                    {
                        if (seat == null) continue;
                        BasePlayer player = seat.GetMounted();
                        if (player != null)
                        {
                            player.transform.position = seat.transform.position;
                        }
                    }

                    count++;
                   
                
                });
            }
            private void CheckEndZone()
            {
                if (Vector3.Distance(drone.transform.position, drone.targetPosition.Value) < 5)
                {
                    hieghtTimer.Destroy();
                    
                    drone.targetPosition = GetBellow(drone.transform.position - (Vector3.up * 3));
                    
                    if (drone.targetPosition.Value.y < WaterLevel.GetWaterDepth(drone.targetPosition.Value,true, true))
                    {
                        drone.targetPosition = new Vector3(drone.targetPosition.Value.x,WaterLevel.GetWaterDepth(drone.targetPosition.Value,true, true) + 2.0f, drone.targetPosition.Value.z);
                    }

                    BasePlayer player = seat.GetMounted();
                    if (player != null)
                    {
                        player.ChatMessage("[Drone Taxi]<color=#FFBF00> You have arrived at your destination!</color>");
                        PlayEffect("assets/bundled/prefabs/fx/item_unlock.prefab", player);
                    }
                }
            }
            private void DestroyTaxi()
            {
                
                if (hieghtTimer != null && !hieghtTimer.Destroyed)
                {
                    hieghtTimer.Destroy();
                }
                if (drone != null && !drone.IsDestroyed)
                {
                    drone.Kill();
                    Effect.server.Run("assets/prefabs/misc/easter/painted eggs/effects/eggpickup.prefab", drone.transform.position, Vector3.up, broadcast: true);
                }
                if (afkTimer != null && !afkTimer.Destroyed)
                    afkTimer.Destroy();
            }
            public void HeightUpdate()
            {
                Vector3 newEnd = drone.transform.position + (direction.normalized * 4) + (Vector3.up * 20);
                float height = 5;

                height = GetGround(newEnd).y;
                float h2 = FindHieghtBuildingBlock();
                if (height < h2)
                    height = h2;
                //height = TerrainMeta.HeightMap.GetHeight(newEnd);

                if (height < 5)
                    height = 5;
                drone.targetPosition = new Vector3(drone.targetPosition.Value.x, height + heightAboveGround, drone.targetPosition.Value.z);
                BasePlayer player = seat.GetMounted();
                if (player != null)
                {
                    
                }
                else
                {
                    DestroyTaxi();
                }
            }
            public void KillDrone()
            {
                if (drone != null) drone.Kill();
            }

            private float FindHieghtBuildingBlock()
            {
                OBB obb = drone.WorldSpaceBounds();
                System.Collections.Generic.List<BuildingBlock> list = Facepunch.Pool.GetList<BuildingBlock>();
                Vis.Entities<BuildingBlock>(obb.position, 16f + obb.extents.magnitude, list, 2097152);
                float height = 0;
                for (int index = 0; index < list.Count; ++index)
                {

                    BuildingBlock buildingBlock2 = list[index];
                    if (height < buildingBlock2.transform.position.y)
                        height = buildingBlock2.transform.position.y;
                }
                Facepunch.Pool.FreeList<BuildingBlock>(ref list);
                if (height < drone.transform.position.y)
                {
                   
                    return 0;
                }
                return height;
            }
        }
        private void MakeUi(BasePlayer player)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.1792453 0.1792453 0.1792453 0.509804" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
            }, "Overlay", "TaxiPanel");

            container.Add(new CuiElement
            {
                Name = "BleedingOutText",
                Parent = "TaxiPanel",
                Components = {
                    new CuiTextComponent { Text = "Drone Taxi", Font = "robotocondensed-bold.ttf", FontSize = 30, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 1", Distance = "1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.4 0.8", AnchorMax = "0.6 0.95"}
                }
            });
            container.Add(new CuiElement
            {
                Parent = "TaxiPanel",
                Components = {
                    new CuiTextComponent { Text = "You will be ejected if you cant afford your fee!", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "0.98 0.36 0.36 1" },
                    new CuiOutlineComponent { Color = "0 0 0 1", Distance = "1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.3 0.15", AnchorMax = "0.7 0.3"}
                }
            });
            container.Add(new CuiButton
            {
                Button = { Color = "0.98 0.36 0.36 1",Command = "closetaxi" },
                Text = { Text = "CLOSE", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", },
                RectTransform = { AnchorMin = "0.45 0.1", AnchorMax = "0.55 0.15" }
            }, "TaxiPanel", "RespawnButton");
            #region buttons
            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 1" },
                Text = { Text = "Drone Lux", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.7 0.3", AnchorMax = "0.9 0.35" }
            }, "TaxiPanel", "RespawnButton");
            #endregion



            #region x
            container.Add(new CuiPanel
            {
                Image = { Color = "0.17 0.17 0.17 1" },
                RectTransform = { AnchorMin = "0.1 0.3", AnchorMax = "0.3 0.8" },
            },
            "TaxiPanel");
            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.1 0.72", AnchorMax = "0.3 0.7225" },
            },
            "TaxiPanel");

            container.Add(new CuiButton
            {
                
                Button = { Color = "1 1 1 1",Command = $"{command} 0" },
                Text = { Text = "Order Drone X", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.1 0.3", AnchorMax = "0.3 0.35" }
            }, "TaxiPanel", "RespawnButton");
            container.Add(new CuiElement
            {
                Parent = "TaxiPanel",
                Components = {
                    new CuiTextComponent { Text = $"Affordable Rides For One\n\n\n\nBase Fee: {flatFeeX} scrap\n\nCost Per Second: {varableFeeX}\n\nSeats 1 person", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.1 0.4", AnchorMax = "0.3 0.7"}
                }
            });
            container.Add(new CuiElement
            {
                Name = "BleedingOutText",
                Parent = "TaxiPanel",
                Components = {
                    new CuiTextComponent { Text = "Drone X", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.1 0.7", AnchorMax = "0.3 0.8"}
                }
            });
            #endregion
            #region XL
            container.Add(new CuiPanel
            {
                Image = { Color = "0.17 0.17 0.17 1" },
                RectTransform = { AnchorMin = "0.4 0.3", AnchorMax = "0.6 0.8" },
            },
            "TaxiPanel");
            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.4 0.72", AnchorMax = "0.6 0.7225" },
            },
            "TaxiPanel");

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 1", Command = $"{command} 1" },
                Text = { Text = "Order Drone XL", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.4 0.3", AnchorMax = "0.6 0.35" }
            }, "TaxiPanel", "RespawnButton");
            container.Add(new CuiElement
            {
                Parent = "TaxiPanel",
                Components = {
                    new CuiTextComponent { Text = $"Large Drones For More Capacity\n\n\n\nBase Fee: {flatFeeXL} scrap\n\nCost Per Second: {varableFeeXL}\n\nSeats 3 person's", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.42 0.4", AnchorMax = "0.58 0.7"}
                }
            });
            container.Add(new CuiElement
            {
                Parent = "TaxiPanel",
                Components = {
                    new CuiTextComponent { Text = "Drone XL", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.4 0.7", AnchorMax = "0.6 0.8"}
                }
            });
            #endregion
            container.Add(new CuiPanel
            {
                Image = { Color = "0.17 0.17 0.17 1" },
                RectTransform = { AnchorMin = "0.7 0.3", AnchorMax = "0.9 0.8" },
            },
            "TaxiPanel");
            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.7 0.72", AnchorMax = "0.9 0.7225" },
            },
            "TaxiPanel");

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 1", Command = $"{command} 2" },
                Text = { Text = "Order Drone LUX", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.7 0.3", AnchorMax = "0.9 0.35" }
            }, "TaxiPanel", "RespawnButton");
            container.Add(new CuiElement
            {
                Parent = "TaxiPanel",
                Components = {
                    new CuiTextComponent { Text = $"Luxary Drones For The Wealthy\n\n\n\nBase Fee: {flatFeeLUX} scrap\n\nCost Per Second: {varableFeeLUX}\n\nSeats 1 person's", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.7 0.4", AnchorMax = "0.9 0.7"}
                }
            });
            container.Add(new CuiElement
            {
                Parent = "TaxiPanel",
                Components = {
                    new CuiTextComponent { Text = "Drone LUX", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.7 0.7", AnchorMax = "0.9 0.8"}
                }
            });
            CuiHelper.DestroyUi(player, "TaxiPanel");
            CuiHelper.AddUi(player, container);
        }
     
    }
}