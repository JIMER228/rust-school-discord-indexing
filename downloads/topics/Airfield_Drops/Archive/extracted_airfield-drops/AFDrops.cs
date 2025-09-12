using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using System.Globalization;
using System.Linq;
using Facepunch;
using UnityEngine.AI;
using Rust;

namespace Oxide.Plugins
{
    [Info("AFDrops", "ZTL/FastBurst", "1.5.3", ResourceId = 81)]
    [Description("Cargo Plane circling airfield, then dropping crates during low-pass of runway.")]

    class AFDrops : RustPlugin
    {
        [PluginReference] Plugin PlaneCrashRandom, PlaneCrash, AdvAirstrike;

        #region Vars
        private const string PERM_ADMIN = "afdrops.admin";
        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);
        private const string CARGOPLANE_PREFAB = "assets/prefabs/npc/cargo plane/cargo_plane.prefab";
        private const string SUPPLYDROP_PREFAB = "assets/prefabs/misc/supply drop/supply_drop.prefab";
        private const string SMOKE_EFFECT = "assets/bundled/prefabs/fx/smoke_signal_full.prefab";
        private const string SIRENLIGHT_EFFECT = "assets/prefabs/io/electric/lights/sirenlightorange.prefab";
        private const string SIRENALARM_EFFECT = "assets/prefabs/deployable/playerioents/alarms/audioalarm.prefab";
        private const string MAPMARKER_PREFAB = "assets/prefabs/tools/map/genericradiusmarker.prefab";
        private const string VENDING_PREFAB = "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab";
        private const string FIRECANNON_EFFECT = "assets/prefabs/npc/m2bradley/effects/maincannonattack.prefab";
        private const string HELIEXPLOSION_EFFECT = "assets/prefabs/npc/patrol helicopter/effects/heli_explosion.prefab";
        private const string DEBRIS_EFFECT = "assets/prefabs/npc/patrol helicopter/damage_effect_debris.prefab";
        private const string HELICOPTER_PREFAB = "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab";
        private const string MLRS_PREFAB = "assets/content/vehicles/mlrs/rocket_mlrs.prefab";

        public int FlightNumber = 0;
        private bool statusPC = false;
        private bool statusPCR = false;
        private static bool debug = false;

        private static AFDrops Instance;
        private static AirfieldPlaneMono airfieldMono;

        private List<SupplyDrop> drops = new List<SupplyDrop>();
        private readonly System.Random _random = new System.Random();
        MonumentInfo monumentInfo;
        private List<MapMarkerGenericRadius> dropZoneMarkerList = new List<MapMarkerGenericRadius>();
        private List<VendingMachineMapMarker> dropVendingMarkerList = new List<VendingMachineMapMarker>();
        private MapMarkerGenericRadius mapMarker;
        private VendingMachineMapMarker vendingMarker;

        List<AF> Airfields = new List<AF>();
        #endregion        

        #region Oxide Hooks
        private void Init()
        {
            Instance = this;
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(PERM_ADMIN, this);

            if (!configData.GlobalSettings.enabled)
                return;

            lang.RegisterMessages(Messages, this);
            CheckMonuments();

            configData.GlobalSettings.RandomTimerMin = Mathf.Max(1, configData.GlobalSettings.RandomTimerMin);
            configData.GlobalSettings.RandomTimerMax = Mathf.Max(2, configData.GlobalSettings.RandomTimerMax);
            if (configData.GlobalSettings.EnableTimedEvents)
                StartTimedEvent();
        }

        // HotFix for Mounument Bradley to disallow Bradley from killing Supply Drops
        private void OnEntityTakeDamage(SupplyDrop entity, HitInfo info)
        {
            if (entity == null || info == null) return;
            if ((entity is SupplyDrop && drops.Contains(entity)) && info.Initiator is BradleyAPC)
                info.damageTypes.ScaleAll(0);
        }

        private void Unload()
        {
            if (airfieldMono != null)
                UnityEngine.Object.Destroy(airfieldMono);

            foreach (var drop in drops)
                if (!drop.IsDestroyed)
                    drop.Kill();

            WipeZoneMarkers(dropZoneMarkerList.ToList());
            WipeVendingMarkers(dropVendingMarkerList.ToList());

            statusPC = false;
            statusPCR = false;
            Instance = null;
            configData = null;

            for (int i = EventHelicopter.allHelicopters.Count - 1; i >= 0; i--)
            {
                EventHelicopter exHelicopter = EventHelicopter.allHelicopters[i];
                exHelicopter.killSilent = true;

                UnityEngine.Object.Destroy(exHelicopter);
            }
        }
        #endregion

        #region Event Checks
        AF GetAF(Vector3 loc, float rot) => new AF() { loc = loc, rot = rot };

        private void CheckMonuments()
        {
            bool hasAirfield = false;
            foreach (var monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            {
                var monumentName = GetMonumentName(monument);
                if (monumentName == null || !monumentName.Contains("airfield")) continue;
                hasAirfield = true;
                monumentInfo = monument;
                var go = monumentInfo.gameObject.transform;
                var loc = go.position;
                var rot = go.eulerAngles.y;
                hasAirfield = true;
                Airfields.Add(GetAF(loc, rot));
                PrintWarning("AirField was found on this map.");
            }
            if (!hasAirfield)
            {
                PrintWarning("No AirField found on your map PLUGIN UNLOADED.");
                covalence.Server.Command("o.unload AirFieldEvent");
                return;
            }
        }

        private void StartTimedEvent()
        {
            float nextEvent = UnityEngine.Random.Range(configData.GlobalSettings.RandomTimerMin, configData.GlobalSettings.RandomTimerMax + 1);
            timer.Repeat(nextEvent, 0, () =>
            {
                if (airfieldMono != null)
                    Puts("There is already an active Airfield Drops event.");
                else
                {
                    if (CallAFDrop())
                        Puts("Attempting to start Airfield Drops event.");
                }
            });
        }
        #endregion

        #region API Event Running Checks
        private bool IsAFPlane(CargoPlane cargoplane) => isAFPlane(cargoplane);

        private bool isAFPlane(CargoPlane cargoplane)
        {
            if (airfieldMono != null)
            {
                cargoplane.GetComponent<AirfieldPlaneMono>();
                return true;
            }
            else
                return false;
        }
        #endregion

        #region Functions
        public string GetMonumentName(MonumentInfo monument)
        {
            var gameObject = monument.gameObject;

            while (gameObject.name.StartsWith("assets/") == false && gameObject.transform.parent != null)
            {
                gameObject = gameObject.transform.parent.gameObject;
            }

            return gameObject?.name;
        }

        private bool CallAFDrop()
        {
            if (!configData.GlobalSettings.enabled)
            {
                Puts("Plugin is disabled in config.");
                return false;
            }
            if (Airfields.Count == 0)
            {
                PrintWarning("No Airfield found on this map.");
                return false;
            }

            if (BasePlayer.activePlayerList.Count < configData.GlobalSettings.MinRequiredPlayers)
            {
                Puts("Skipping Airfield Drops  event : Not enough players.");
                return false;
            }
            FlightNumber = UnityEngine.Random.Range(3000, 9999);
            AF af = Airfields[0];
            var entity = (CargoPlane)GameManager.server.CreateEntity(CARGOPLANE_PREFAB, new Vector3(), new Quaternion(), true);
            if (configData.GlobalSettings.CheckPlugins)
            {
                if (PlaneCrashRandom && (bool)PlaneCrashRandom.Call("IsRandomCrashPlane", entity))
                {
                    PrintWarning("Currently there is an active Random Plane Crash event. Restarting Event countdown.");
                    StartTimedEvent();
                    statusPCR = true;
                    return false;
                }
                else if (PlaneCrash && (bool)PlaneCrash.Call("IsCrashPlane", entity))
                {
                    PrintWarning("Currently there is an active Plane Crash event. Restarting Event countdown.");
                    StartTimedEvent();
                    statusPC = true;
                    return false;
                }
                else
                    PrintWarning("There is no active plane crash events.");
            }

            // Wipe Map Markers and Drops before next event.
            WipeZoneMarkers(Instance.dropZoneMarkerList.ToList());
            WipeVendingMarkers(Instance.dropVendingMarkerList.ToList());
            foreach (var drop in drops)
                if (!drop.IsDestroyed)
                    drop.Kill();

            for (int i = EventHelicopter.allHelicopters.Count - 1; i >= 0; i--)
            {
                EventHelicopter exHelicopter = EventHelicopter.allHelicopters[i];
                exHelicopter.killSilent = true;

                UnityEngine.Object.Destroy(exHelicopter);
            }

            // Start New Event Entities
            entity.Spawn();
            airfieldMono = entity.gameObject.AddComponent<AirfieldPlaneMono>();
            UnityEngine.Object.Destroy(entity.GetComponent<SaveRestore>());
            SpawnMapMarker();

            return true;
        }

        private static void RunEffect(string name, BaseEntity entity = null, Vector3 position = new Vector3(), Vector3 offset = new Vector3())
        {
            if (entity != null)
                Effect.server.Run(name, entity, 0, offset, position, null, true);
            else Effect.server.Run(name, position, Vector3.up, null, true);
        }

        private void DropMRLS(Vector3 pos)
        {
            int MLRSAmount = configData.MissleSettings.MRSLAmount;
            if (MLRSAmount == 0) return;
            float RandomRange = configData.MissleSettings.mrlsRadius;
            Instance.timer.Repeat(1.8f, MLRSAmount, () =>
            {
                BaseEntity AirfieldMLRS = GameManager.server.CreateEntity(MLRS_PREFAB, pos + new Vector3(UnityEngine.Random.Range(-(RandomRange), (RandomRange)), 450, UnityEngine.Random.Range(-(RandomRange), (RandomRange))), new Quaternion(), true);
                AirfieldMLRS.Spawn();
                AirfieldMLRS.skinID = 29051978;
            });
        }
        #endregion

        #region Map Markers
        private void SpawnMapMarker()
        {
            if (!configData.MapMarkerSettings.EnableMapMarker)
                return;

            AF af = Airfields[0];

            mapMarker = GameManager.server.CreateEntity(MAPMARKER_PREFAB, af.loc) as MapMarkerGenericRadius;
            mapMarker.Spawn();
            mapMarker.radius = configData.MapMarkerSettings.DropZoneRadius;
            mapMarker.alpha = configData.MapMarkerSettings.DropZoneAlpha;
            mapMarker.color1 = Color.red;
            mapMarker.SendUpdate();

            vendingMarker = GameManager.server.CreateEntity(VENDING_PREFAB, af.loc) as VendingMachineMapMarker;
            vendingMarker.markerShopName = configData.MapMarkerSettings.EventName;
            vendingMarker.Spawn();
            vendingMarker.SendNetworkUpdate();

            dropZoneMarkerList.Add(mapMarker);
            dropVendingMarkerList.Add(vendingMarker);
        }

        private void WipeZoneMarkers(List<MapMarkerGenericRadius> markers)
        {
            foreach (var mapMarker in markers)
            {
                if (mapMarker == null || mapMarker.IsDestroyed)
                {
                    continue;
                }

                mapMarker.Kill();
                mapMarker.SendUpdate();
            }

            markers.Clear();
        }
        private void WipeVendingMarkers(List<VendingMachineMapMarker> markers)
        {
            foreach (var mapMarker in markers)
            {
                if (mapMarker == null || mapMarker.IsDestroyed)
                {
                    continue;
                }

                mapMarker.Kill();
                mapMarker.SendNetworkUpdate();
            }

            markers.Clear();
        }
        #endregion

        #region AFDropPlane Class
        public class AirfieldPlaneMono : FacepunchBehaviour
        {
            List<bool> dropped = new List<bool> { false, false, false, false, false };
            Vector3 ZeroY(Vector3 input) => new Vector3(input.x, 0, input.z);
            AF Airfield = Instance.Airfields[0];
            Vector3 posRef = Vector3.zero;
            public CargoPlane plane;
            public bool started;
            Vector3 targetDir;
            int turn = 1, circles = 0;
            bool CircleBlocked = false;
            bool exitSent = false;
            int speedmult = 10;

            public List<TargetInfo> targets = new List<TargetInfo>();

            public class TargetInfo
            {
                public bool Direct = false, Started = false;
                public Vector3 TargetPos = new Vector3();
                public Vector3 AimTarget = new Vector3();
                public Vector3 PivotPoint;
                public GameObject Trans = new GameObject();
                public Vector3 Adjust => new Vector3(pitch, yaw, roll);
                public float rotatemulti, roll = 0, pitch = 0, yaw = 0;
            }

            #region Flight Movements
            public void Awake()
            {
                targets.Add(new TargetInfo() { TargetPos = RelativePos(new Vector3(-configData.PlaneSettings.CircleRadius, configData.PlaneSettings.Height, 0)), Direct = true, rotatemulti = 40 });
                targets.Add(new TargetInfo() { TargetPos = RelativePos(new Vector3(-configData.PlaneSettings.CircleRadius, configData.PlaneSettings.Height, 0)), Direct = true, rotatemulti = 40 });
                targets.Add(new TargetInfo() { TargetPos = RelativePos(new Vector3(-configData.PlaneSettings.CircleRadius, configData.PlaneSettings.Height, 0)), Direct = true, rotatemulti = 40 });
                // Start Landing Pattern
                targets.Add(new TargetInfo() { TargetPos = RelativePos(new Vector3(-2175, configData.PlaneSettings.Height, -221)), Direct = true, rotatemulti = 1.1f });
                targets.Add(new TargetInfo() { TargetPos = RelativePos(new Vector3(-1897, 100, 225)), Direct = true, rotatemulti = 1.1f });
                targets.Add(new TargetInfo() { TargetPos = RelativePos(new Vector3(-1460, 70, 225)), Direct = true, rotatemulti = 1.1f });
                // End Turns Begin Runway Approach & Centering
                targets.Add(new TargetInfo() { TargetPos = RelativePos(new Vector3(-716, 55, 100)), Direct = true, rotatemulti = 20 });
                targets.Add(new TargetInfo() { TargetPos = RelativePos(new Vector3(-316, 40, 40)), Direct = true, rotatemulti = 20 });
                // Start Runway run
                targets.Add(new TargetInfo() { TargetPos = RelativePos(new Vector3(-94, -5, 5)), Direct = true, rotatemulti = 20 });
                // End Runway Run
                targets.Add(new TargetInfo() { TargetPos = RelativePos(new Vector3(120, -5, -40)), Direct = true, rotatemulti = 1 });
                // Start Take off
                targets.Add(new TargetInfo() { TargetPos = RelativePos(new Vector3(230, 60, -65)), Direct = true, rotatemulti = 1 });
                targets.Add(new TargetInfo() { TargetPos = RelativePos(new Vector3(562, 200, -302)), Direct = true, rotatemulti = 1 });
                // Leave Map
                targets.Add(new TargetInfo() { TargetPos = RelativePos(new Vector3(2850, 500, -4020)), Direct = true, rotatemulti = 1 });

                plane = GetComponentInParent<CargoPlane>();
                plane.enabled = false;
                plane.transform.position = RelativePos(new Vector3(-265.5f, configData.PlaneSettings.Height, 4333.5f));
                UpdateDirectionaAndRotation();
                started = true;
                SendChatMessage("Notification.Inbound", Instance.FlightNumber.ToString());

                if (configData.PlaneSettings.SmokeTrail)
                {
                    RunEffect(SMOKE_EFFECT, plane, new Vector3(), Vector3.up * 3);
                    RunEffect(SMOKE_EFFECT, plane, new Vector3(), Vector3.up * 3);
                }
            }

            public void FixedUpdate()
            {
                if (!started)
                    return;
                if (turn == 2 && circles < 6)
                {
                    DoCircle();
                    UpdateDirectionaAndRotation();
                    return;
                }

                if (Vector3.Distance(targets[turn].TargetPos, this.transform.position) < 40f)
                {
                    turn++;
                    if (targets.Count > turn)
                        targets[turn].roll = targets[turn - 1].roll;
                }
                if (targets.Count == turn)
                {
                    plane.Kill();
                    return;
                }

                else if (targets[turn].Direct)
                    GotoDirect();
                else
                    GotoIndirect();
                UpdateDirectionaAndRotation();
            }

            public void OnDestroy()
            {
                if (plane?.IsDestroyed == false)
                    plane.Kill();
                Instance.WipeZoneMarkers(Instance.dropZoneMarkerList.ToList());
                Instance.WipeVendingMarkers(Instance.dropVendingMarkerList.ToList());
            }
            #endregion

            #region Movements 
            public void DoCircle()
            {
                plane.transform.RotateAround(Airfield.loc, Vector3.down, configData.PlaneSettings.PlaneSpeed * 0.8f * Time.deltaTime);
                if (!CircleBlocked && Vector3.Distance(transform.position, targets[turn].TargetPos) < 30)
                {
                    CircleBlocked = true;
                    Instance.timer.Once(1f, () => CircleBlocked = false);
                    circles++;
                    string msgs = circles == 1 ? "Notification.FirstCircle" : circles == 2 ? "Notification.SecondCircle" : circles == 3 ? "Notification.ThirdCircle" : circles == 4 ? "Notification.FourthCircle" : circles == 5 ? "Notification.FifthCircle" : circles == 6 ? "Notification.ApproachBound" : "";
                    if (circles < 7)
                        SendChatMessage(msgs, Instance.FlightNumber.ToString());
                }
            }

            public void GotoDirect()
            {
                #region Areas to Drop Supply Drops
                if (turn > 6)
                {
                    if (!dropped[2] && Vector3.Distance(MakeRealPos(transform.position), new Vector3(-88.4f, 0, -6.4f)) < 20)
                        if (configData.SupplyDropSettings.NumberOfDroppedCrates > 2)
                        {
                            DoCrateDrop(2);
                            if (configData.GlobalSettings.HeliChance == 100 || UnityEngine.Random.Range(0, 100) < configData.GlobalSettings.HeliChance)
                            {
                                Instance.SpawnHeli();
                                Instance.timer.Once(0.5f, () => SendChatMessage("Notification.Heli", Instance.FlightNumber.ToString()));
                            }

                            if (Instance.AdvAirstrike && configData.AdvAirstrikeSettings.enableAdvAirstrike)
                            {
                                string[] msg = configData.AdvAirstrikeSettings.StrikeTypes;
                                string command = configData.AdvAirstrikeSettings.AdvAirstrikeCommand;
                                Instance.Server.Command($"{command} {msg.GetRandom()} {Airfield.loc.x} {Airfield.loc.z}");
                            }
                            SendChatMessage("Notification.LandingMessage", Instance.FlightNumber.ToString());

                            if (configData.MissleSettings.enabled)
                                Instance.timer.Once(2.5f, () => Instance.DropMRLS(Airfield.loc));
                        }
                    if (!dropped[3] && Vector3.Distance(MakeRealPos(transform.position), new Vector3(-45.3f, 0, -6.9f)) < 20)
                        if (configData.SupplyDropSettings.NumberOfDroppedCrates > 3)
                            DoCrateDrop(3);
                    if (!dropped[0] && Vector3.Distance(MakeRealPos(transform.position), new Vector3(1.5f, 0, -6.6f)) < 20)
                        if (configData.SupplyDropSettings.NumberOfDroppedCrates > 0)
                            DoCrateDrop(0);
                    if (!dropped[4] && Vector3.Distance(MakeRealPos(transform.position), new Vector3(54.0f, 0, -6.4f)) < 20)
                        if (configData.SupplyDropSettings.NumberOfDroppedCrates > 4)
                            DoCrateDrop(4);
                    if (!dropped[1] && Vector3.Distance(MakeRealPos(transform.position), new Vector3(99.6f, 0, -6.8f)) < 20)
                        if (configData.SupplyDropSettings.NumberOfDroppedCrates > 1)
                            DoCrateDrop(1);
                }
                #endregion

                if (turn == 10 && !exitSent)
                {
                    exitSent = true;
                    SendChatMessage("Notification.Outbound", Instance.FlightNumber.ToString());
                }
                if (!targets[turn].Started)
                {
                    if (turn == 1)
                        targets[turn].Trans.transform.position = targets[2].TargetPos;
                    else
                        targets[turn].Trans.transform.position = transform.position + (transform.forward * Vector3.Distance(targets[turn].TargetPos, transform.position));

                    targets[turn].Started = true;
                }
                if (Vector3.Distance(targets[turn].Trans.transform.position, targets[turn].TargetPos) > 10f)
                {
                    targets[turn].Trans.transform.position = Vector3.MoveTowards(targets[turn].Trans.transform.position, targets[turn].TargetPos, Time.deltaTime * (configData.PlaneSettings.PlaneSpeed * targets[turn].rotatemulti) * 10);
                    targets[turn].AimTarget = targets[turn].Trans.transform.position;
                }
                else
                    targets[turn].AimTarget = targets[turn].TargetPos;
                speedmult = turn == 7 ? 9 : turn == 8 ? 7 : turn == 9 ? 8 : 10;
                transform.position = Vector3.MoveTowards(transform.position, targets[turn].AimTarget, Time.deltaTime * configData.PlaneSettings.PlaneSpeed * speedmult);
            }

            public void GotoIndirect()
            {
                if (!targets[turn].Started)
                {
                    targets[turn].Trans.transform.position = transform.position + (transform.position - targets[turn].TargetPos);
                    targets[turn].Trans.transform.position = new Vector3(targets[turn].Trans.transform.position.x, targets[turn].TargetPos.y, targets[turn].Trans.transform.position.z);
                    targets[turn].PivotPoint = transform.position;
                    targets[turn].PivotPoint.y = targets[turn].TargetPos.y;
                    targets[turn].AimTarget = targets[turn].Trans.transform.position;
                    targets[turn].Started = true;
                }
                if (Vector3.Distance(targets[turn].Trans.transform.position, targets[turn].TargetPos) > 10f)
                {
                    targets[turn].Trans.transform.RotateAround(targets[turn].PivotPoint, Vector3.up, configData.PlaneSettings.PlaneSpeed / targets[turn].rotatemulti);
                    targets[turn].AimTarget = targets[turn].Trans.transform.position;
                }
                else
                    targets[turn].AimTarget = targets[turn].TargetPos;

                transform.position = Vector3.MoveTowards(transform.position, targets[turn].AimTarget, Time.deltaTime * configData.PlaneSettings.PlaneSpeed * 10); //move plane towards that moving target
            }

            public void UpdateDirectionaAndRotation()
            {
                targetDir = (turn < 3 && turn > 1) ? Airfield.loc - plane.transform.position : targets[turn].TargetPos - plane.transform.position;
                float angle = Vector3.SignedAngle(targetDir, plane.transform.position - posRef, Vector3.up);

                if (angle > 10 && targets[turn].roll > -55)
                    targets[turn].roll += -1.2f;
                else if (angle < -10 && targets[turn].roll < 55)
                    targets[turn].roll += 1.2f;
                else
                {
                    if (plane.transform.rotation.eulerAngles.z > 1 && plane.transform.rotation.eulerAngles.z < 180)
                        targets[turn].roll += 1.2f;
                    else if (plane.transform.rotation.eulerAngles.z > 180 && plane.transform.rotation.eulerAngles.z < 359)
                        targets[turn].roll += -1.2f;
                    else
                    {
                        targets[turn].roll = 0f;
                        plane.transform.rotation = Quaternion.Euler(plane.transform.rotation.eulerAngles.x, plane.transform.rotation.eulerAngles.y, 0);
                    }
                }

                if (ZeroY(plane.transform.position) != ZeroY(posRef))
                    plane.transform.rotation = Quaternion.LookRotation(ZeroY(plane.transform.position) - ZeroY(posRef));

                posRef = plane.transform.position;
                plane.transform.rotation = Quaternion.Euler(plane.transform.rotation.eulerAngles - targets[turn].Adjust);
            }

            Vector3 RelativePos(Vector3 pos)
            {
                var newTrans = new GameObject().transform;
                newTrans.transform.position = Instance.Airfields[0].loc + pos;
                newTrans.transform.RotateAround(Instance.Airfields[0].loc, Vector3.down, -Instance.Airfields[0].rot + 11);
                return newTrans.transform.position;
            }

            Vector3 MakeRealPos(Vector3 pos)
            {
                var newTrans = new GameObject().transform;
                newTrans.transform.position = pos - Instance.Airfields[0].loc;
                newTrans.transform.RotateAround(new Vector3(0, 0, 0), Vector3.down, Instance.Airfields[0].rot);
                return new Vector3(newTrans.transform.position.x, 0, newTrans.transform.position.z);
            }
            #endregion

            private void DoCrateDrop(int d)
            {
                dropped[d] = true;
                SpawnCrates();
            }

            #region Spawn Drops and Effects
            private static void CreateSirenLights(BaseEntity entity)
            {
                var SirenLight = GameManager.server.CreateEntity(SIRENLIGHT_EFFECT, default(Vector3), default(Quaternion), true);
                SirenLight.gameObject.Identity();
                SirenLight.SetParent(entity as LootContainer, "parachute_attach");
                SirenLight.Spawn();
                SirenLight.SetFlag(BaseEntity.Flags.Reserved8, true);
            }

            private static void CreateSirenAlarms(BaseEntity entity)
            {
                var SirenAlarm = GameManager.server.CreateEntity(SIRENALARM_EFFECT, new Vector3(0f, 0f, 0f), default(Quaternion), true);
                SirenAlarm.gameObject.Identity();
                SirenAlarm.SetParent(entity);
                SirenAlarm.Spawn();
                SirenAlarm.SetFlag(BaseEntity.Flags.Reserved8, true);
            }

            private static void CreateDropEffects(BaseEntity entity)
            {
                if (entity == null) return;

                if (configData.SupplyDropSettings.SupplyDropEffects.enableSmoke)
                    Effect.server.Run(SMOKE_EFFECT, entity, 0, Vector3.zero, Vector3.zero, null, true);

                // Create Siren Alams on Supply Drops
                if (configData.SupplyDropSettings.SupplyDropEffects.useSirenAlarmOnDrop && configData.SupplyDropSettings.SupplyDropEffects.useSirenAlarmAtNightOnly && TOD_Sky.Instance.IsNight)
                    CreateSirenAlarms(entity);
                else if (configData.SupplyDropSettings.SupplyDropEffects.useSirenAlarmAtNightOnly && TOD_Sky.Instance.IsNight)
                    CreateSirenAlarms(entity);
                else if (configData.SupplyDropSettings.SupplyDropEffects.useSirenAlarmOnDrop)
                    CreateSirenAlarms(entity);
                else
                    return;

                // Create Spinning Siren Lights on Supply Drops
                if (configData.SupplyDropSettings.SupplyDropEffects.useSirenLightOnDrop && configData.SupplyDropSettings.SupplyDropEffects.useSirenLightAtNightOnly && TOD_Sky.Instance.IsNight)
                    CreateSirenLights(entity);
                else if (configData.SupplyDropSettings.SupplyDropEffects.useSirenLightAtNightOnly && TOD_Sky.Instance.IsNight)
                    CreateSirenLights(entity);
                else if (configData.SupplyDropSettings.SupplyDropEffects.useSirenLightOnDrop)
                    CreateSirenLights(entity);
                else
                    return;
            }

            private void SpawnCrates()
            {
                SupplyDrop drop = GameManager.server.CreateEntity(SUPPLYDROP_PREFAB, transform.position + Vector3.up * configData.SupplyDropSettings.SupplyDropHeight, new Quaternion(), true) as SupplyDrop;
                drop.Spawn();
                Rigidbody rigidbody = drop.GetComponent<Rigidbody>();
                rigidbody.drag = 0f;
                rigidbody.mass = 1000;
                rigidbody.AddForce((plane.transform.forward + (plane.transform.right * UnityEngine.Random.Range(-10f, 10f))) * 25);
                drop.transform.rotation = Quaternion.LookRotation(drop.transform.position - new Vector3(0, UnityEngine.Random.Range(0, 180), 0));

                int slots = 36;
                drop.inventory.capacity = 36;
                drop.panelName = "generic";
                drop.inventory.ServerInitialize(null, slots);
                drop.inventory.MarkDirty();

                if (configData.SupplyDropSettings.RemoveChutes)
                    drop.RemoveParachute();

                Instance.drops.Add(drop);

                if (configData.SupplyDropSettings.enableEffects)
                    CreateDropEffects(drop);

                Instance.timer.Once(configData.SupplyDropSettings.DespawnTimerMinutes * 60, () =>
                {
                    if (drop != null)
                        drop.Kill();
                });
                Interface.CallHook("OnLootSpawn", drop);
            }


            #endregion
        }
        #endregion

        #region Event Heli Controller
        private void SpawnHeli()
        {
            PatrolHelicopter helicopter = GameManager.server.CreateEntity(HELICOPTER_PREFAB) as PatrolHelicopter;
            helicopter.enableSaving = false;
            helicopter.Spawn();

            //NextTick(() =>
            //{
            //    if (helicopter == null)
            //    {
            //        PrintError("[Plugin Conflict] Another plugin on your server has destroyed the helicopter when it spawned");
            //        return;
            //    }

            helicopter.transform.position = RandomPointOnWorldBounds();
            helicopter.myAI.State_Patrol_Enter();
            helicopter.gameObject.AddComponent<EventHelicopter>();
            //});
        }

        private Vector3 RandomPointOnWorldBounds()
        {
            Vector3 position;
            position = new Vector3(150, 0, 150);
            position.y = TerrainMeta.HeightMap.GetHeight(position) + 150f;
            return position;
        }

        private static void NullifyDamage(HitInfo info)
        {
            info.damageTypes = new DamageTypeList();
            info.HitEntity = null;
            info.HitMaterial = 0;
            info.PointStart = Vector3.zero;
        }

        private class EventHelicopter : FacepunchBehaviour
        {
            internal static List<EventHelicopter> allHelicopters = new List<EventHelicopter>();
            protected PatrolHelicopter Helicopter { get; private set; }
            protected PatrolHelicopterAI HeliAI { get; private set; }

            private bool enemiesInVicinity = false;
            internal bool hasValidLocation = false;
            private float nextValidationTime;
            public DateTime born = DateTime.UtcNow;
            public bool killSilent = false;
            private bool isDying = false;
            private float actualHealth;
            internal bool retired = false;
            public bool bored = false;
            internal bool destroyKill = true;
            AF Airfield = Instance.Airfields[0];

            private void Start()
            {
                Helicopter = GetComponent<PatrolHelicopter>();
                HeliAI = GetComponent<PatrolHelicopterAI>();
                actualHealth = Helicopter.health;
                FindTargetLocation();
                InvokeRepeating("UpdatePos", 0f, 5f);
                InvokeHandler.Invoke(this, ResetStateToPatrol, 90f);
                allHelicopters.Add(this);
            }

            private void UpdatePos()
            {
                var timeAlive = DateTime.UtcNow - born;
                if (timeAlive.Minutes > configData.GlobalSettings.RetireMinutes - 1)
                {
                    if (!retired)
                    {
                        Retire();
                        return;
                    }
                }

                if (!bored)
                {
                    HeliAI.SetTargetDestination(Airfield.loc + new Vector3(0.0f, 50f, 0.0f));
                    if (Vector2.Distance(transform.position, Airfield.loc) < 10f)
                    {
                        HeliAI.State_Orbit_Enter(5f);
                        HeliAI.State_Orbit_Think(5f);
                        HeliAI.State_Patrol_Enter();
                        HeliAI.State_Patrol_Think(60f);
                        HeliAI.maxSpeed = 25f;
                    }
                    else
                    {
                        HeliAI.State_Move_Enter(Airfield.loc + new Vector3(0.0f, 30f, 0.0f));
                        HeliAI.maxSpeed = 70f;
                    }
                }
            }

            private void FindEnemiesInVicinity()
            {
                AIUpdate();
                enemiesInVicinity = false;
                List<BasePlayer> players = Pool.GetList<BasePlayer>();
                Vis.Entities(Airfield.loc, 30f, players);
                players.RemoveAll(x => x.IsNpc);

                if (players.Count > 0)
                {
                    enemiesInVicinity = true;
                    HeliAI._targetList.Clear();
                    players.ForEach(x => HeliAI._targetList.Add(new PatrolHelicopterAI.targetinfo(x, x)));
                }

                Pool.FreeList(ref players);
            }

            private void OnDestroy()
            {
                CancelInvoke("UpdatePos");
                allHelicopters.Remove(this);

                if (Helicopter != null && !Helicopter.IsDestroyed)
                    Helicopter.Kill();

                if (destroyKill && Helicopter != null && !Helicopter.IsDestroyed)
                    Helicopter.Kill();
            }

            public void Kill()
            {
                Destroy(gameObject);
            }

            #region Death and Damage
            internal void OnDamage(HitInfo info)
            {
                actualHealth -= info.damageTypes.Total();

                if (actualHealth < 0f)
                {
                    //NullifyDamage(info);
                    //OnDeath(info);

                    if (info.damageTypes.GetMajorityDamageType() == DamageType.Explosion)
                        DropLoot();

                    if (!isDying)
                        CrashToDeath();
                }
            }

            internal void OnDeath(HitInfo info)
            {
                if (isDying)
                    return;

                isDying = true;

                if (configData.LootBoxes.DropHeliLoot)
                    DropLoot();

                Interface.CallHook("OnEntityDeath", Helicopter);

                Helicopter.DieInstantly();
            }

            internal void CrashToDeath()
            {
                isDying = true;
                HeliAI.CriticalDamage();
            }
            #endregion

            #region Loot Drop
            private void DropLoot()
            {
                if (configData.LootBoxes.Amount <= 0)
                    return;

                for (int i = 0; i < configData.LootBoxes.Amount; i++)
                {
                    LootContainer container = GameManager.server.CreateEntity(Helicopter.crateToDrop.resourcePath, transform.position) as LootContainer;
                    container.enableSaving = false;
                    container.Spawn();

                    Vector3 velocity = UnityEngine.Random.onUnitSphere;
                    velocity.y = 1;

                    Rigidbody rb = container.gameObject.AddComponent<Rigidbody>();
                    rb.useGravity = true;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    rb.mass = 2f;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.angularVelocity = Vector3Ex.Range(-1.75f, 1.75f);
                    rb.drag = 0.5f * (rb.mass / 5f);
                    rb.angularDrag = 0.2f * (rb.mass / 5f);

                    rb.AddForce(velocity * 5f, ForceMode.Impulse);

                    FireBall fireBall = GameManager.server.CreateEntity(Helicopter.fireBall.resourcePath) as FireBall;
                    if (fireBall)
                    {
                        fireBall.SetParent(container, false, false);
                        fireBall.Spawn();
                        fireBall.GetComponent<Rigidbody>().isKinematic = true;
                        fireBall.GetComponent<Collider>().enabled = false;
                    }
                    container.SendMessage("SetLockingEnt", fireBall.gameObject, SendMessageOptions.DontRequireReceiver);

                    if (configData.LootBoxes.UseCustomLoot)
                    {
                        ClearContainer(container.inventory);
                        PopulateLoot(container.inventory, configData.LootBoxes.RandomItems);
                    }
                }
            }

            private void ClearContainer(ItemContainer container)
            {
                if (container == null || container.itemList == null)
                    return;

                while (container.itemList.Count > 0)
                {
                    Item item = container.itemList[0];
                    item.RemoveFromContainer();
                    item.Remove(0f);
                }
            }

            private void PopulateLoot(ItemContainer container, ConfigData.LootContainer loot)
            {
                if (container == null || loot == null)
                    return;

                ClearContainer(container);

                int amount = UnityEngine.Random.Range(loot.Minimum, loot.Maximum);

                List<ConfigData.LootItem> list = Pool.GetList<ConfigData.LootItem>();
                list.AddRange(loot.Items);

                int itemCount = 0;
                while (itemCount < amount)
                {
                    int totalWeight = list.Sum((ConfigData.LootItem x) => Mathf.Max(1, x.Weight));
                    int random = UnityEngine.Random.Range(0, totalWeight);

                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        ConfigData.LootItem lootItem = list[i];

                        totalWeight -= Mathf.Max(1, lootItem.Weight);

                        if (random >= totalWeight)
                        {
                            list.Remove(lootItem);

                            Item item = ItemManager.CreateByName(lootItem.Name, UnityEngine.Random.Range(lootItem.Minimum, lootItem.Maximum), lootItem.Skin);
                            item?.MoveToContainer(container);

                            itemCount++;
                            break;
                        }
                    }

                    if (list.Count == 0)
                        list.AddRange(loot.Items);
                }

                Pool.FreeList(ref list);
            }
            #endregion

            internal void ResetStateToPatrol()
            {
                InvokeHandler.Invoke(this, ResetState, 1f);
            }

            private void ResetState()
            {
                enabled = true;
                HeliAI.enabled = true;
                HeliAI.ClearAimTarget();
                FindEnemiesInVicinity();
                HeliAI._currentState = PatrolHelicopterAI.aiState.PATROL;
                HeliAI.maxSpeed = 30f;
                hasValidLocation = false;
                //SendChatMessage("Resuming normal patrol");
                bored = true;
                destroyKill = false;
            }

            private void AIUpdate()
            {
                HeliAI.MoveToDestination();
                HeliAI.UpdateRotation();
                HeliAI.UpdateSpotlight();
                HeliAI.AIThink();
                HeliAI.DoMachineGuns();
            }

            private void Retire()
            {
                CancelInvoke("UpdatePos");
                HeliAI._targetList.Clear();
                bored = true;
                HeliAI.Retire();
                HeliAI.maxSpeed = 100f;
                retired = true;
                SendChatMessage(msg("Notification.Helicopter.Retire"));
            }

            private void FindTargetLocation()
            {
                if (Airfield.loc != null)
                {
                    hasValidLocation = true;
                }
                hasValidLocation = false;
            }
        }
        #endregion

        #region Commands
        [ConsoleCommand("afdrops")]
        private void afdconsole(ConsoleSystem.Arg arg)
        {
            if (arg?.Args == null || arg.Args.Length == 0)
            {
                Puts("Usage for Airfield Drops Event");
                Puts("afdrops start - Starts Airfield Drops Event manually");
                Puts("afdrops stop - Stops and kills Airfield Drop Event manually");
                return;
            }
            if (arg.Player() == null)
            {
                if (arg.Args[0] == "start")
                {
                    if (airfieldMono != null)
                        Puts("There is already an active Airfield Drops event.");
                    else
                    {
                        Puts("Attempting to start Airfield Drops event.");

                        if (CallAFDrop())
                            Puts("Starting Airfield Drops event.");
                    }
                }
                if (arg.Args[0] == "stop")
                {
                    if (airfieldMono == null)
                        Puts("There is no active Airfield Drops event.");
                    else
                    {
                        airfieldMono.plane.Kill();
                        foreach (var drop in drops)
                            if (!drop.IsDestroyed)
                                drop.Kill();
                        for (int i = EventHelicopter.allHelicopters.Count - 1; i >= 0; i--)
                        {
                            EventHelicopter exHelicopter = EventHelicopter.allHelicopters[i];
                            exHelicopter.killSilent = true;

                            UnityEngine.Object.Destroy(exHelicopter);
                        }
                        Puts("Airfield Drops event was stopped.");
                    }
                }
            }
            else
                afdchat(arg.Player(), "afdrops", arg.Args);
        }

        [ChatCommand("afdrops")]
        private void afdchat(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player.UserIDString, PERM_ADMIN))
            {
                SendReply(player, "You do not have permission to use this command.");
                return;
            }
            if (args == null)
                return;
            if (args.Length == 0)
            {
                var helpmsg = new StringBuilder();
                helpmsg.Append("<size=22><color=green>Airfield Drops</color></size> by: ZTL\n");
                helpmsg.Append("<color=orange>/afdrops start</color> - Starts Airfield Drops Event manually\n");
                helpmsg.Append("<color=orange>/afdrops stop</color> - Stops and kills Airfield Drops Event manually\n");
                SendReply(player, helpmsg.ToString().TrimEnd());
                return;
            }
            if (args.Length == 1)
            {
                if (args[0] == "start")
                {
                    if (airfieldMono != null)
                        SendReply(player, "There is already an active Airfield Drops event.");
                    else
                    {
                        SendReply(player, "Attempting to start Airfield Drops event");

                        if (CallAFDrop())
                            SendReply(player, "Starting Airfield Drops event");
                        else
                        {
                            if (statusPC && PlaneCrash)
                            {
                                SendReply(player, "Currently there is an active Plane Crash event. Canceling Event");
                                statusPC = false;
                            }
                            if (statusPCR && PlaneCrashRandom)
                            {
                                SendReply(player, "Currently there is an active Random Plane Crash event. Canceling Event");
                                statusPCR = false;
                            }
                        }
                    }
                }
                if (args[0] == "stop")
                {
                    if (airfieldMono == null)
                        SendReply(player, "There is no active Airfield Drops event.");
                    else
                    {
                        airfieldMono.plane.Kill();
                        foreach (var drop in drops)
                            if (!drop.IsDestroyed)
                                drop.Kill();
                        for (int i = EventHelicopter.allHelicopters.Count - 1; i >= 0; i--)
                        {
                            EventHelicopter exHelicopter = EventHelicopter.allHelicopters[i];
                            exHelicopter.killSilent = true;

                            UnityEngine.Object.Destroy(exHelicopter);
                        }
                        SendReply(player, "Airfield Drops event was stopped.");
                    }
                }
            }
        }
        #endregion

        #region Class
        public class AF
        {
            public Vector3 loc;
            public float rot;
        }
        #endregion

        #region Config
        private static ConfigData configData;
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Global Event Settings")]
            public GlobalOptions GlobalSettings { get; set; }
            [JsonProperty(PropertyName = "Event Map Marker Settings")]
            public EventOptions MapMarkerSettings { get; set; }
            [JsonProperty(PropertyName = "Cargo Plane Settings")]
            public PlaneOptions PlaneSettings { get; set; }
            [JsonProperty(PropertyName = "MRLS Missle Settings")]
            public MRLSOptions MissleSettings { get; set; }
            [JsonProperty(PropertyName = "Supply Drop Settings")]
            public SupplyDropOptions SupplyDropSettings { get; set; }
            [JsonProperty(PropertyName = "Advance Airstrike Plugin Options")]
            public AdvAirstrikeOptions AdvAirstrikeSettings { get; set; }
            [JsonProperty(PropertyName = "Loot Container Options For Patrol Heli")]
            public Loot LootBoxes { get; set; }


            public class GlobalOptions
            {
                [JsonProperty(PropertyName = "Enable Airfield Air Drops Event")]
                public bool enabled { get; set; }
                [JsonProperty(PropertyName = "Enable random autospawn Airfield Drop Events on random timer")]
                public bool EnableTimedEvents { get; set; }
                [JsonProperty(PropertyName = "Minimum required players to start events")]
                public int MinRequiredPlayers { get; set; }
                [JsonProperty(PropertyName = "Minimum time between autospawn Airfield Air Drop Event (seconds)")]
                public int RandomTimerMin { get; set; }
                [JsonProperty(PropertyName = "Maximum time between autospawn Airfield Air Drop Event (seconds)")]
                public int RandomTimerMax { get; set; }
                [JsonProperty(PropertyName = "Check Plane Crash plugin?")]
                public bool CheckPlugins { get; set; }
                [JsonProperty(PropertyName = "Chance of Patrol Helicopter to spawn and protect the box once unlocked (x / 100)")]
                public float HeliChance { get; set; }
                [JsonProperty(PropertyName = "Retire Heli after call (minutes)")]
                public int RetireMinutes { get; set; }

            }

            public class MRLSOptions
            {
                [JsonProperty(PropertyName = "Enable MRLS Missle Drops at Event")]
                public bool enabled { get; set; }
                [JsonProperty(PropertyName = "Number of MRLS Missles to launch at Event")]
                public int MRSLAmount { get; set; }
                [JsonProperty(PropertyName = "Radius for MRLS Missiles at Event")]
                public float mrlsRadius { get; set; }
            }

            public class EventOptions
            {

                [JsonProperty(PropertyName = "Enable Custom Map Marker for Airfield Drop Events")]
                public bool EnableMapMarker { get; set; }
                [JsonProperty(PropertyName = "Event Zone Map Marker Name")]
                public string EventName { get; set; }
                [JsonProperty(PropertyName = "Event Zone Radius")]
                public float DropZoneRadius { get; set; }
                [JsonProperty(PropertyName = "Event Zone Alpha Shading")]
                public float DropZoneAlpha { get; set; }
            }

            public class PlaneOptions
            {
                [JsonProperty(PropertyName = "Plane speed for event")]
                public float PlaneSpeed { get; set; }
                [JsonProperty(PropertyName = "Radius for which the cargo plane should circle the airfield)")]
                public int CircleRadius { get; set; }
                [JsonProperty(PropertyName = "Plane hieght for circling the airfield and island")]
                public int Height { get; set; }
                [JsonProperty(PropertyName = "Show smoke trail behind plane")]
                public bool SmokeTrail { get; set; }
            }

            public class SupplyDropOptions
            {
                [JsonProperty(PropertyName = "Number of supply drops to spawn on runway (Max is 5)")]
                public int NumberOfDroppedCrates { get; set; }
                [JsonProperty(PropertyName = "Despawn time for supply drops on runway (minutes)")]
                public int DespawnTimerMinutes { get; set; }
                [JsonProperty(PropertyName = "Enable Supply Drop effects")]
                public bool enableEffects { get; set; }
                [JsonProperty(PropertyName = "Supply Drop height drop from plane (default is set to 6)")]
                public int SupplyDropHeight { get; set; }
                [JsonProperty(PropertyName = "Remove Parachutes from Supply Drops at event (default set to true)")]
                public bool RemoveChutes { get; set; }
                [JsonProperty(PropertyName = "Supply Drop Effects Settings")]
                public SupplyEffectOptions SupplyDropEffects { get; set; }

                public class SupplyEffectOptions
                {
                    [JsonProperty(PropertyName = "Enable Smoke on Supply Drops")]
                    public bool enableSmoke { get; set; }
                    [JsonProperty(PropertyName = "Enable Spinning Light on Supply Drops")]
                    public bool useSirenLightOnDrop { get; set; }
                    [JsonProperty(PropertyName = "Enable Spinning Light on Supply Drops at night only")]
                    public bool useSirenLightAtNightOnly { get; set; }
                    [JsonProperty(PropertyName = "Enable Siren Alarm on Supply Drops")]
                    public bool useSirenAlarmOnDrop { get; set; }
                    [JsonProperty(PropertyName = "Enable Siren Alarm on Supply Drops at night only")]
                    public bool useSirenAlarmAtNightOnly { get; set; }

                }
            }

            public class AdvAirstrikeOptions
            {
                [JsonProperty(PropertyName = "Enable Advanced Airstrike plugin calls")]
                public bool enableAdvAirstrike { get; set; }
                [JsonProperty(PropertyName = "Advanced Airstrike command call")]
                public string AdvAirstrikeCommand { get; set; }
                [JsonProperty(PropertyName = "Type of Advanced Airstrikes available")]
                public string[] StrikeTypes { get; set; }
            }

            public class Loot
            {
                [JsonProperty(PropertyName = "Enable Patrol Helicopter loot when destroyed")]
                public bool DropHeliLoot { get; set; }
                [JsonProperty(PropertyName = "Amount of loot boxes to drop when Patrol Helicopter is destroyed")]
                public int Amount { get; set; }
                [JsonProperty(PropertyName = "Replace default loot with custom loot?")]
                public bool UseCustomLoot { get; set; }
                [JsonProperty(PropertyName = "Loot container items")]
                public LootContainer RandomItems { get; set; }
            }

            public class LootContainer
            {
                [JsonProperty(PropertyName = "Minimum amount of items")]
                public int Minimum { get; set; }
                [JsonProperty(PropertyName = "Maximum amount of items")]
                public int Maximum { get; set; }
                [JsonProperty(PropertyName = "Items")]
                public LootItem[] Items { get; set; }
            }

            public class LootItem
            {
                [JsonProperty(PropertyName = "Item shortname")]
                public string Name { get; set; }
                [JsonProperty(PropertyName = "Item skin ID")]
                public ulong Skin { get; set; }
                [JsonProperty(PropertyName = "Minimum amount of item")]
                public int Minimum { get; set; }
                [JsonProperty(PropertyName = "Maximum amount of item")]
                public int Maximum { get; set; }
                [JsonProperty(PropertyName = "Item weight (a larger number has more chance of being selected)")]
                public int Weight { get; set; } = 1;
                [JsonProperty(PropertyName = "Item Display Name")]
                public string itemDisplayName { get; set; }
            }

            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                GlobalSettings = new ConfigData.GlobalOptions
                {
                    enabled = true,
                    EnableTimedEvents = true,
                    MinRequiredPlayers = 1,
                    RandomTimerMin = 10800,
                    RandomTimerMax = 18000,
                    CheckPlugins = true,
                    HeliChance = 65f,
                    RetireMinutes = 9,
                },
                MapMarkerSettings = new ConfigData.EventOptions
                {
                    EnableMapMarker = true,
                    EventName = "Airfield Drop Event",
                    DropZoneRadius = 1f,
                    DropZoneAlpha = 0.4f
                },
                SupplyDropSettings = new ConfigData.SupplyDropOptions
                {
                    NumberOfDroppedCrates = 5,
                    DespawnTimerMinutes = 30,
                    SupplyDropHeight = 6,
                    RemoveChutes = true,
                    enableEffects = true,
                    SupplyDropEffects = new ConfigData.SupplyDropOptions.SupplyEffectOptions
                    {
                        enableSmoke = true,
                        useSirenLightOnDrop = true,
                        useSirenLightAtNightOnly = false,
                        useSirenAlarmOnDrop = true,
                        useSirenAlarmAtNightOnly = false
                    }
                },
                PlaneSettings = new ConfigData.PlaneOptions
                {
                    PlaneSpeed = 5.5f,
                    CircleRadius = 800,
                    Height = 300,
                    SmokeTrail = true
                },
                MissleSettings = new ConfigData.MRLSOptions
                {
                    enabled = true,
                    MRSLAmount = 20,
                    mrlsRadius = 50f
                },
                AdvAirstrikeSettings = new ConfigData.AdvAirstrikeOptions
                {
                    enableAdvAirstrike = true,
                    AdvAirstrikeCommand = "strike",
                    StrikeTypes = new string[] { "spectre", "squad", "strike", "napalm" },
                },

                LootBoxes = new ConfigData.Loot
                {
                    DropHeliLoot = true,
                    Amount = 4,
                    UseCustomLoot = false,
                    RandomItems = new ConfigData.LootContainer
                    {
                        Minimum = 3,
                        Maximum = 10,
                        Items = new ConfigData.LootItem[]
                        {
                            new ConfigData.LootItem {Name = "apple", Skin = 0, Maximum = 6, Minimum = 2, Weight = 1, itemDisplayName = "Apple" },
                            new ConfigData.LootItem {Name = "bearmeat.cooked", Skin = 0, Maximum = 4, Minimum = 2, Weight = 1, itemDisplayName = "Cooked Bear Meat" },
                            new ConfigData.LootItem {Name = "blueberries", Skin = 0, Maximum = 8, Minimum = 4, Weight = 1, itemDisplayName = "Blueberries" },
                            new ConfigData.LootItem {Name = "corn", Skin = 0, Maximum = 8, Minimum = 4, Weight = 1, itemDisplayName = "Corn" },
                            new ConfigData.LootItem {Name = "fish.raw", Skin = 0, Maximum = 4, Minimum = 2, Weight = 1, itemDisplayName = "Raw Fish" },
                            new ConfigData.LootItem {Name = "granolabar", Skin = 0, Maximum = 4, Minimum = 1, Weight = 1, itemDisplayName = "Granola Bar" },
                            new ConfigData.LootItem {Name = "meat.pork.cooked", Skin = 0, Maximum = 8, Minimum = 4, Weight = 1, itemDisplayName = "Cooked Pork" },
                            new ConfigData.LootItem {Name = "syringe.medical", Skin = 0, Maximum = 6, Minimum = 2, Weight = 1, itemDisplayName = "Medical Syringe" },
                            new ConfigData.LootItem {Name = "largemedkit", Skin = 0, Maximum = 2, Minimum = 1, Weight = 1, itemDisplayName = "Large Medkit" },
                            new ConfigData.LootItem {Name = "bandage", Skin = 0, Maximum = 4, Minimum = 1, Weight = 1, itemDisplayName = "Bandage" },
                            new ConfigData.LootItem {Name = "antiradpills", Skin = 0, Maximum = 3, Minimum = 1, Weight = 1, itemDisplayName = "Anti-Radiation Pills" },
                            new ConfigData.LootItem {Name = "ammo.rifle", Skin = 0, Maximum = 100, Minimum = 10, Weight = 1, itemDisplayName = "5.56 Rifle Ammo" },
                            new ConfigData.LootItem {Name = "ammo.pistol", Skin = 0, Maximum = 100, Minimum = 10, Weight = 1, itemDisplayName = "Pistol Bullet" },
                            new ConfigData.LootItem {Name = "ammo.rocket.basic", Skin = 0, Maximum = 10, Minimum = 1, Weight = 1, itemDisplayName = "Rocket" },
                            new ConfigData.LootItem {Name = "ammo.shotgun.slug", Skin = 0, Maximum = 20, Minimum = 10, Weight = 1, itemDisplayName = "12 Gauge Slug" },
                            new ConfigData.LootItem {Name = "pistol.m92",Skin = 0,  Maximum = 1, Minimum = 1, Weight = 1, itemDisplayName = "M92 Pistol" },
                            new ConfigData.LootItem {Name = "rifle.l96", Skin = 0, Maximum = 1, Minimum = 1, Weight = 1, itemDisplayName = "L96 Rifle" },
                            new ConfigData.LootItem {Name = "rifle.lr300", Skin = 0, Maximum = 1, Minimum = 1, Weight = 1, itemDisplayName = "LR-300 Assault Rifle" },
                            new ConfigData.LootItem {Name = "rifle.ak", Skin = 0, Maximum = 1, Minimum = 1, Weight = 1, itemDisplayName = "Assault Rifle" },
                            new ConfigData.LootItem {Name = "rifle.bolt", Skin = 0, Maximum = 1, Minimum = 1, Weight = 1, itemDisplayName = "Bolt Action Rifle" },
                            new ConfigData.LootItem {Name = "rocket.launcher", Skin = 0, Maximum = 1, Minimum = 1, Weight = 1, itemDisplayName = "Rocket Launcher" },
                            new ConfigData.LootItem {Name = "pistol.revolver", Skin = 0, Maximum = 1, Minimum = 1, Weight = 1, itemDisplayName = "Revolver" }
                        }
                    }
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new Core.VersionNumber(1, 1, 9))
                configData = baseConfig;

            if (configData.Version < new Core.VersionNumber(1, 2, 3))
            {
                configData.SupplyDropSettings.RemoveChutes = true;
                configData.SupplyDropSettings.SupplyDropHeight = 6;
            }

            if (configData.Version < new Core.VersionNumber(1, 2, 6))
                configData.PlaneSettings.SmokeTrail = true;

            if (configData.Version < new Core.VersionNumber(1, 4, 0))
                configData = baseConfig;

            if (configData.Version < new Core.VersionNumber(1, 4, 7))
                configData.AdvAirstrikeSettings = baseConfig.AdvAirstrikeSettings;

            if (configData.Version < new Core.VersionNumber(1, 4, 8))
            {
                configData.LootBoxes = baseConfig.LootBoxes;
                configData.GlobalSettings.HeliChance = 65f;
                configData.GlobalSettings.RetireMinutes = 9;
            }

            if (configData.Version < new Core.VersionNumber(1, 5, 0))
                configData.MissleSettings = baseConfig.MissleSettings;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion

        #region Localization
        private static void SendChatMessage(string key, params object[] args)
        {
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                BasePlayer player = BasePlayer.activePlayerList[i];
                player.ChatMessage(args != null ? string.Format(msg(key, player.UserIDString), args) : msg(key, player.UserIDString));
            }
        }

        private static string msg(string key, string playerId = null) => Instance.lang.GetMessage(key, Instance, playerId);

        private Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["Notification.Heli"] = "<size=12><color=red>ALERT WARNING</color>: Be advised, <color=#C4FF00>ZTL Cargo Flight {0}</color> has just called in a <color=orange> Patrol Helicopter</color> for Close Air Support at the Airfield as it was taking off!</size>",
            ["Notification.Inbound"] = "<color=yellow><size=12>ZTL Cargo Flight {0} reported being overweight, they will be circling the Airfield for a landing attempt, lots of possble components and loot onboard!</size></color>",
            ["Notification.FirstCircle"] = "<color=yellow><size=12>ZTL Cargo Flight {0} spotted circling the Airfield to drop excess Supply Drops on the runway, lots of possible Scrap and components!</size></color>",
            ["Notification.SecondCircle"] = "<color=yellow><size=12>ZTL Cargo Flight {0} is currently circling the Airfield and to see if it is safe to attempt a landing to drop excess Supply Drops.</size></color>",
            ["Notification.ThirdCircle"] = "<color=yellow><size=12>ZTL Cargo Flight {0} is radioing the Air Control Tower located at the Airfield that it is going to make a couple more passes before attempting to make a landing!</size></color>",
            ["Notification.FourthCircle"] = "<color=yellow><size=12>ZTL Cargo Flight {0} is going to attempt to land at Airfield, to drop excess Supply Drops filled with components. Be sure to be there to protect the loot!</size></color>",
            ["Notification.FifthCircle"] = "<color=yellow><size=12>ZTL Cargo Flight {0} is on its final turn and going into its flight pattern to make its final approach. They will be attempting a landing shortly at the Airfield.</size></color>",
            ["Notification.ApproachBound"] = "<color=yellow><size=12>ZTL Cargo Flight {0} is now moving its flight pattern into position and making its approach to land at the Airfield.</size></color>",
            ["Notification.LandingMessage"] = "<color=yellow><size=12>ZTL Cargo Flight {0} has been spotted landing at the Airfield dropping its cargo right now! Get to the Supply Drops now and get the loot!</size></color>",
            ["Notification.Outbound"] = "<color=yellow><size=12>ZTL Cargo Flight {0} spotted leaving the Airfield, and it just dropped excess Supply Drops on the runway, filled with components and possible Scrap!</size></color>",
            ["Error.NoPermission"] = "You do not have permission to use this command.",
            ["Notification.Helicopter.Retire"] = "<color=#ffff00><size=12>The Patrol Helicopter has stopped its patrol for the Airfield and has decided to head back to HQ for debriefing.</size></color>",
        };
        #endregion
    }
}