using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;
using System.Globalization;
using Rust;

namespace Oxide.Plugins
{
    [Info("RaceTrack", "k1lly0u", "0.1.97")]
    [Description("A standalone racing event for cars, boats, mincopters and horses")]
    class RaceTrack : RustPlugin
    {
        #region Fields
        private Hash<string, TrackData> trackData;
        private Hash<string, ModularCarData> carData;
        private RestoreData restoreData;
        private List<uint> spawnedEntities;
        private DynamicConfigFile entitydata, trackdata, cardata, restorationData;

        [PluginReference] Plugin CarCommander, BoatCommander, HeliCommander, Economics, ServerRewards;

        private static RaceTrack ins;
        private static bool disableSpectate;

        private RaceManager manager;
        private Timer autoTimer;

        private int lastEventIndex = 0;

        private Hash<string, TrackData> raceTracks = new Hash<string, TrackData>();
        private Hash<ulong, TrackData> trackCreator = new Hash<ulong, TrackData>();

        private CuiElementContainer scoreContainer;

        private LightType lightType;

        private const string SEDAN_PREFAB = "assets/content/vehicles/sedan_a/sedantest.entity.prefab";
        private const string ROWBOAT_PREFAB = "assets/content/vehicles/boats/rowboat/rowboat.prefab";
        private const string RHIB_PREFAB = "assets/content/vehicles/boats/rhib/rhib.prefab";
        private const string MINICOPTER_PREFAB = "assets/content/vehicles/minicopter/minicopter.entity.prefab";
        private const string HORSE_PREFAB = "assets/rust.ai/nextai/testridablehorse.prefab";
        private const string BOOGIEBOARD_PREFAB = "assets/prefabs/misc/summer_dlc/boogie_board/boogieboard.deployed.prefab";
        private const string INNER_TUBE_PREFAB = "assets/prefabs/misc/summer_dlc/inner_tube/innertube.deployed.prefab";
        private const string KAYAK_PREFAB = "assets/content/vehicles/boats/kayak/kayak.prefab";
        private const string KAYAK_PADDLE_ITEM = "paddle";

        private const string LANTERN_PREFAB = "assets/prefabs/deployable/lantern/lantern.deployed.prefab";
        private const string FLASHER_PREFAB = "assets/prefabs/deployable/playerioents/lights/flasherlight/electric.flasherlight.deployed.prefab";
        private const string SIREN_PREFAB = "assets/prefabs/deployable/playerioents/lights/sirenlight/electric.sirenlight.deployed.prefab";
        private const string WALL_PREFAB = "assets/prefabs/building core/wall.low/wall.low.prefab";

        private enum RaceMode { Laps, Sprint }

        private enum RaceType { Car, Boat, RHIB, Minicopter, Horse, Kayak, BoogieBoard, InnerTube }
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            permission.RegisterPermission("racetrack.admin", this);
            permission.RegisterPermission("racetrack.play", this);

            lang.RegisterMessages(Messages, this);

            entitydata = Interface.Oxide.DataFileSystem.GetFile("RaceTrack/entity_data");
            trackdata = Interface.Oxide.DataFileSystem.GetFile("RaceTrack/track_data");
            cardata = Interface.Oxide.DataFileSystem.GetFile("RaceTrack/car_data");
            restorationData = Interface.Oxide.DataFileSystem.GetFile("RaceTrack/restoration_data");
            trackdata.Settings.Converters = new JsonConverter[] { new UnityVector3Converter() };
            restorationData.Settings.Converters = new JsonConverter[] { new UnityVector3Converter() };
        }

        private void OnServerInitialized()
        {
            ins = this;

            disableSpectate = configData.DisableSpectate;
            if (disableSpectate)
            {
                //Unsubscribe(nameof(OnPlayerInput));
                //Unsubscribe(nameof(OnPlayerTick));
                Unsubscribe(nameof(CanSpectateTarget));
            }

            if (!configData.Racers.DisableMetabolism)
                Unsubscribe(nameof(OnRunPlayerMetabolism));

            lightType = ParseType<LightType>(configData.Checkpoints.LightType);

            LoadData();
            CleanupEntities();
            StartEventTimer();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsSleeping() || player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.Once(1, () => OnPlayerConnected(player));
                return;
            }

            if (restoreData.HasRestoreData(player.userID))
                restoreData.RestorePlayer(player);
        }

        private void OnPlayerRespawned(BasePlayer player) => OnPlayerConnected(player);

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (manager != null)
                manager.PlayerDisconnected(player);
            else
            {
                RaceDriver raceDriver = player.GetComponent<RaceDriver>();
                if (raceDriver != null)
                {                   
                    raceDriver.DismountPlayer();
                    NextTick(() =>
                    {
                        if (raceDriver != null)
                            UnityEngine.Object.Destroy(raceDriver);
                    });                    
                }
            }
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {            
            RaceDriver raceDriver = player.GetComponent<RaceDriver>();
            if (raceDriver != null)
            {
                if (!player.isMounted && raceDriver.Vehicle != null && raceDriver.Vehicle.entity != null)
                    raceDriver.MountPlayer(raceDriver.Vehicle);
            }
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null)
                return;

            if (entity.GetComponent<RaceDriver>() != null)            
                NullifyDamage(ref info);

            if (entity.GetComponent<RaceVehicle>() != null)
                NullifyDamage(ref info);

            BaseVehicleModule module = entity as BaseVehicleModule;
            if (module != null && module.Vehicle?.GetComponent<RaceVehicle>())
                NullifyDamage(ref info);

            CheckPoint.Marker marker = entity.GetComponent<CheckPoint.Marker>();
            if (marker != null && manager != null && manager.Status != EventStatus.Finished)
                NullifyDamage(ref info);
        }

        private object OnRunPlayerMetabolism(PlayerMetabolism metabolism, BasePlayer owner, float delta)
        {
            if (owner.GetComponent<RaceDriver>())
                return true;
            return null;
        }

        private object CanMountEntity(BasePlayer player, BaseMountable mountable)
        {
            if (mountable.GetComponent<RaceCar>())            
                return false;            
            return null;
        }

        private object CanDismountEntity(BasePlayer player, BaseMountable mountable)
        {
            RaceDriver raceDriver = player.GetComponent<RaceDriver>();
            if (raceDriver != null && !raceDriver.HasFinished)            
                return false;            
            return null;
        }

        private object OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            if (player != null && player.GetComponent<RaceDriver>())
            {
                if (configData.CommandBlacklist.Any(x => x.StartsWith("/") ? x.Substring(1).ToLower() == command : x.ToLower() == command))
                {
                    SendReply(player, msg("blacklistcmd", player.UserIDString));
                    return false;
                }
            }
            return null;
        }

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            
            if (player != null && player.GetComponent<RaceDriver>())
            {
                if (configData.CommandBlacklist.Any(x => arg.cmd.FullName.Contains(x.ToLower())) || arg.cmd.FullName == "vehicle.swapseats")
                {
                    SendReply(player, msg("blacklistcmd", player.UserIDString));
                    return false;
                }
            }

            return null;
        }

        private object CanSpectateTarget(BasePlayer player, string name)
        {
            RaceDriver raceDriver = player.GetComponent<RaceDriver>();
            if (raceDriver != null && raceDriver.Player.IsSpectating())
            {
                raceDriver.UpdateSpectateTarget(1);
                return false;
            }

            return null;
        }

        private void Unload()
        {
            SaveRestoreData();

            if (autoTimer != null)
                autoTimer.Destroy();

            RaceDriver[] raceDrivers = UnityEngine.Object.FindObjectsOfType<RaceDriver>();
            if (raceDrivers != null)
            {
                foreach(RaceDriver raceDriver in raceDrivers)                                    
                    UnityEngine.Object.Destroy(raceDriver);                
            }

            RaceVehicle[] raceVehicles = UnityEngine.Object.FindObjectsOfType<RaceVehicle>();
            if (raceVehicles != null)
            {
                foreach(RaceVehicle raceVehicle in raceVehicles)               
                    UnityEngine.Object.Destroy(raceVehicle);
            }

            CheckPoint[] checkPoints = UnityEngine.Object.FindObjectsOfType<CheckPoint>();
            if (checkPoints != null)
            {
                foreach(CheckPoint checkPoint in checkPoints)                
                    UnityEngine.Object.Destroy(checkPoint); 
            }

            CheckPoint.Marker[] markers = UnityEngine.Object.FindObjectsOfType<CheckPoint.Marker>();
            if (markers != null)
            {
                foreach (CheckPoint.Marker marker in markers)
                    UnityEngine.Object.Destroy(marker);
            }

            UnityEngine.Object.Destroy(manager);

            ins = null;
        }
        #endregion

        #region Functions        
        private void CleanupEntities()
        {            
            if (spawnedEntities.Count > 0)
            {
                PrintWarning("Finding and destroying left over entities");
                BaseNetworkable[] objects = BaseNetworkable.serverEntities.ToArray();
                if (objects != null)
                {
                    foreach(BaseNetworkable obj in BaseNetworkable.serverEntities)
                    {
                        if (obj != null && !obj.IsDestroyed)
                        {
                            if (spawnedEntities.Contains(obj.net.ID))
                                obj.Kill(BaseNetworkable.DestroyMode.None);
                        }
                    }
                }
                spawnedEntities.Clear();
                SaveEntityData();
                PrintWarning("Cleanup completed");
            }            
        }   

        private string FormatTime(double time)
        {
            TimeSpan dateDifference = TimeSpan.FromSeconds((float)time);
            int days = dateDifference.Days;
            int hours = dateDifference.Hours;
            hours += (days * 24);
            int mins = dateDifference.Minutes;
            int secs = dateDifference.Seconds;
            if (hours > 0)
                return string.Format("{0:00}:{1:00}:{2:00}", hours, mins, secs);
            else return string.Format("{0:00}:{1:00}", mins, secs);
        }

        private T ParseType<T>(string type)
        {
            try
            {
                return (T)Enum.Parse(typeof(T), type, true);
            }
            catch
            {
                return default(T);
            }
        }

        private static string EntityFromRaceType(RaceType raceType)
        {
            switch (raceType)
            {
                case RaceType.Car:
                    return SEDAN_PREFAB;
                case RaceType.Boat:
                    return ROWBOAT_PREFAB;
                case RaceType.RHIB:
                    return RHIB_PREFAB;
                case RaceType.Minicopter:
                    return MINICOPTER_PREFAB;
                case RaceType.Horse:
                    return HORSE_PREFAB;
                case RaceType.Kayak:
                    return KAYAK_PREFAB;
                case RaceType.BoogieBoard:
                    return BOOGIEBOARD_PREFAB;
                case RaceType.InnerTube:
                    return INNER_TUBE_PREFAB;
                default:
                    return string.Empty;
            }
        }

        private static Type VehicleComponentFromRaceType(RaceType raceType)
        {
            switch (raceType)
            {
                case RaceType.Car:
                    return typeof(RaceCar);
                case RaceType.Boat:
                case RaceType.RHIB:
                case RaceType.Kayak:
                case RaceType.BoogieBoard:
                case RaceType.InnerTube:
                    return typeof(RaceBoat);
                case RaceType.Minicopter:
                    return typeof(RaceHelicopter);
                case RaceType.Horse:
                    return typeof(RaceHorse);
                default:
                    return null;
            }
        }

        private void NullifyDamage(ref HitInfo info)
        {
            info.damageTypes = new DamageTypeList();
            info.HitEntity = null;
            info.HitMaterial = 0;
            info.PointStart = Vector3.zero;
        }

        private void StartEventTimer()
        {
            if (configData.Automation.Enabled)
            {
                if (trackData.Count == 0)
                {
                    PrintError("You have no race tracks set up. Unable to automate events");
                    return;
                }

                autoTimer = timer.In(configData.Timers.Interval, () =>
                {
                    if (manager == null)
                        manager = new GameObject().AddComponent<RaceManager>();

                    if (manager.Status != EventStatus.Finished)
                        return;

                    if (configData.Automation.Random)
                    {
                        var data = trackData.ElementAt(UnityEngine.Random.Range(0, trackData.Count - 1));
                        manager.SetTrackData(data.Key, data.Value);
                        manager.OpenEvent();
                    }
                    else
                    {
                        if (configData.Automation.Order.Count < 1)
                        {
                            PrintError("You have no race tracks set in your config. Unable to automate events by order");
                            return;
                        }
                        if (!trackData.ContainsKey(configData.Automation.Order[lastEventIndex]))
                        {
                            PrintError($"Unable to find a race track with the name : {configData.Automation.Order[lastEventIndex]}");
                            return;
                        }

                        var data = trackData[configData.Automation.Order[lastEventIndex]];
                        manager.SetTrackData(configData.Automation.Order[lastEventIndex], data);

                        lastEventIndex++;

                        if (lastEventIndex >= configData.Automation.Order.Count)
                            lastEventIndex = 0;
                        manager.OpenEvent();
                    }
                });
            }
        }

        private void BroadcastEvent()
        {
            if (!configData.Entrance.Enabled)
                BroadcastToChat(string.Format(msg("eventopen1"), manager.RaceType));
            else
            {
                if (configData.Entrance.ServerRewards)
                    BroadcastToChat(string.Format(msg("eventopenfee1"), configData.Entrance.Amount, msg("serverrewards"), manager.RaceType));
                if (configData.Entrance.Economics)
                    BroadcastToChat(string.Format(msg("eventopenfee1"), configData.Entrance.Amount, msg("economics"), manager.RaceType)); if (configData.Entrance.Scrap)
                if (configData.Entrance.Scrap)
                    BroadcastToChat(string.Format(msg("eventopenfee1"), configData.Entrance.Amount, msg("scrap"), manager.RaceType));
            }

            if (configData.Rewards.Enabled)
            {
                string rewards = configData.Rewards.ServerRewards ? msg("serverrewards") : configData.Rewards.Scrap ? msg("scrap") : msg("economics");
                if (configData.Rewards.Podium)
                    BroadcastToChat(string.Format(msg("eventprizepodium"), configData.Rewards.Prize1, rewards, configData.Rewards.Prize2, configData.Rewards.Prize3));
                else BroadcastToChat(string.Format(msg("eventprize"), configData.Rewards.Prize1, rewards));
            }
        }

        private void OnVehicleUnderwater(BasicCar car)
        {
            RaceCar raceCar = car.GetComponent<RaceCar>();
            if (raceCar != null)
            {
                raceCar.ResetVehicle();
                ins.CarCommander.Call("ToggleController", car, true);
            }
        }

        private static void BroadcastToChat(string message)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                player.SendConsoleCommand("chat.add", new object[] { 0, 0, message });
        }
        #endregion

        #region Component
        enum EventStatus { Open, Loading, Started, Finishing, Finished }

        private class RaceManager : MonoBehaviour
        {
            private List<BasePlayer> joiners;

            private List<RaceDriver> raceDrivers;
            private List<RaceVehicle> raceVehicles;
            private List<CheckPoint> checkPoints;
            internal List<RaceDriver> spectateTargets;

            private RaceScores[] winners;
            private int playersFinished;
            private int totalRacers;

            private string trackName;
            private TrackData trackData;
            private EventStatus status;

            private int maxRacers;
            private int countdown = 10;
            private int timerTick;
            private double startTime;
            private double endTime;

            private bool hasStarted;
            private bool isEnding;
            private bool isStarting;
            private bool isRacing;

            private bool hasLaunchedFirework;

            #region Properties
            public EventStatus Status
            {
                get
                {
                    return status;
                }
            }

            public bool HasTrackSet
            {
                get
                {
                    return trackData != null;
                }
            }

            public int RacerCount
            {
                get
                {
                    return raceDrivers.Count;
                }
            }

            public int CheckpointCount
            {
                get
                {
                    return trackData.checkPoints.Count;
                }
            }

            public int JoinerCount
            {
                get
                {
                    return joiners.Count;
                }
            }

            public string TrackName
            {
                get
                {
                    return trackName;
                }
            }

            public RaceMode RaceMode
            {
                get
                {
                    return trackData.raceMode;
                }
            }

            public RaceType RaceType
            {
                get
                {
                    return trackData.raceType;
                }
            }

            public TrackData TrackData
            {
                get
                {
                    return trackData;
                }
            }

            public int Laps
            {
                get
                {
                    return trackData.laps;
                }
            }

            public double StartTime
            {
                get
                {
                    return startTime;
                }
            }

            public double CurrentTime
            {
                get
                {
                    return DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
                }
            }

            public bool CanReset
            {
                get
                {
                    return isRacing;
                }
            }
            #endregion

            private void Awake()
            {
                joiners = Facepunch.Pool.GetList<BasePlayer>();
                raceDrivers = Facepunch.Pool.GetList<RaceDriver>();
                raceVehicles = Facepunch.Pool.GetList<RaceVehicle>();
                checkPoints = Facepunch.Pool.GetList<CheckPoint>();
                spectateTargets = Facepunch.Pool.GetList<RaceDriver>();

                countdown = ins.configData.Timers.Countdown;

                status = EventStatus.Finished;
                enabled = false;
            }

            private void OnDestroy()
            {
                DestroyEvent();
                ins.StartEventTimer();
            }

            #region Event Management
            public void SetTrackData(string trackName, TrackData trackData)
            {
                this.trackName = trackName;
                this.trackData = trackData;

                maxRacers = trackData.gridPoints.Count;
            }

            public void OpenEvent()
            {
                if (ins.autoTimer != null)
                    ins.autoTimer.Destroy();

                isStarting = false;
                isEnding = false;
                hasStarted = false;
                isRacing = false;

                status = EventStatus.Loading;

                ins.scoreContainer = null;

                ServerMgr.Instance.StartCoroutine(CreateCheckPoints());
            }

            public void StartEvent()
            {
                isStarting = false;
                hasStarted = true;

                status = EventStatus.Started;
                
                InvokeHandler.CancelInvoke(this, StartEvent);
                InvokeHandler.CancelInvoke(this, EndEvent);

                foreach (BasePlayer player in joiners)
                    SpawnPlayer(player);

                joiners.Clear();

                ins.DisplayInformation(raceDrivers, trackName, trackData);
                Countdown();

                winners = new RaceScores[raceDrivers.Count];
                playersFinished = 0;
                totalRacers = raceDrivers.Count;
            }

            public void EndEvent()
            {
                InvokeHandler.CancelInvoke(this, Countdown);
                InvokeHandler.CancelInvoke(this, CalculateScores);

                raceDrivers.ForEach((RaceDriver driver) => CuiHelper.DestroyUi(driver.Player, UITime));

                if (status == EventStatus.Open)
                {
                    status = EventStatus.Finished;
                    ins.IssueRefunds(joiners);
                    joiners.Clear();
                    BroadcastToChat(msg("cancelled"));
                    Destroy(this);
                }
                else FinishRace();                
            }

            public void JoinEvent(BasePlayer player)
            {
                if (status != EventStatus.Open)
                {
                    player.ChatMessage(status == EventStatus.Finished ? msg("noevent", player.UserIDString) : msg("nojoin", player.UserIDString));
                    return;
                }

                if (joiners.Contains(player))
                {
                    player.ChatMessage(msg("inevent", player.UserIDString));
                    return;
                }

                if (joiners.Count >= maxRacers)
                {
                    player.ChatMessage(msg("eventfull", player.UserIDString));
                    return;
                }

                joiners.Add(player);
                player.ChatMessage(msg("joinevent", player.UserIDString));
                BroadcastToChat(string.Format(msg("joinevent.player"), player.displayName));

                if (joiners.Count >= trackData.minPlayers && !isStarting)
                {
                    isStarting = true;
                    InvokeHandler.CancelInvoke(this, EndEvent);
                    BroadcastToChat(string.Format(msg("minplayers.reached"), ins.configData.Timers.StartTime));
                    InvokeHandler.Invoke(this, StartEvent, ins.configData.Timers.StartTime);
                }
            }

            public void LeaveEvent(BasePlayer player, bool hasFinished)
            {
                if (status == EventStatus.Open)
                {
                    if (joiners.Contains(player))
                    {
                        ins.IssueRefund(player);

                        joiners.Remove(player);
                        if (joiners.Count < trackData.minPlayers)
                        {
                            isStarting = false;
                            InvokeHandler.CancelInvoke(this, EndEvent);
                            InvokeHandler.CancelInvoke(this, StartEvent);
                            InvokeHandler.Invoke(this, EndEvent, ins.configData.Timers.CloseTime);

                            BroadcastToChat(string.Format(msg("leftevent.wait"), player.displayName));
                        }
                        else BroadcastToChat(string.Format(msg("leftevent"), player.displayName));
                    }
                }
                else if (status == EventStatus.Started)
                {
                    RaceDriver raceDriver = player.GetComponent<RaceDriver>();
                    if (raceDriver != null)
                    {
                        raceDrivers.Remove(raceDriver);
                        spectateTargets.Remove(raceDriver);

                        if (!disableSpectate)
                        {
                            raceDrivers.ForEach((RaceDriver spectatorDriver) =>
                            {
                                if (spectatorDriver.Player.IsSpectating())
                                {
                                    if (raceDrivers.Count == 0)
                                        spectatorDriver.FinishSpectating();
                                    else
                                    {
                                        if (raceDriver.SpectateTarget == raceDriver)
                                            raceDriver.UpdateSpectateTarget();
                                    }
                                }
                            });
                        }

                        winners[raceDrivers.Count] = new RaceScores(raceDriver, CurrentTime - StartTime, raceDriver.LapNumber - 1, raceDriver.GetDistanceTravelled());
                        winners[raceDrivers.Count].hasFinished = false;

                        Destroy(raceDriver);
                        if (!hasFinished)
                            BroadcastToChat(string.Format(msg("leftevent"), player.displayName));

                        totalRacers -= 1;
                        if (raceDrivers.Count == 0)
                            EndEvent();
                    }
                }
                else player.ChatMessage(msg("noevent", player.UserIDString));
            }       

            private void DestroyEvent()
            {
                isStarting = false;
                isEnding = false;
                isRacing = false;

                joiners.Clear();

                for (int i = raceDrivers.Count - 1; i >= 0; i--)
                {
                    RaceDriver raceDriver = raceDrivers[i];
                    Destroy(raceDriver);                    
                }
                raceDrivers.Clear();

                for (int i = raceVehicles.Count - 1; i >= 0; i--)
                {
                    RaceVehicle raceVehicle = raceVehicles[i];
                    BaseMountable baseMountable = raceVehicle.entity;
                    Destroy(raceVehicle);
                }
                raceVehicles.Clear();

                for (int i = checkPoints.Count - 1; i >= 0; i--)
                {
                    CheckPoint checkPoint = checkPoints[i];
                    Destroy(checkPoint);
                }
                checkPoints.Clear();

                Facepunch.Pool.FreeList(ref joiners);
                Facepunch.Pool.FreeList(ref raceDrivers);
                Facepunch.Pool.FreeList(ref spectateTargets);
                Facepunch.Pool.FreeList(ref raceVehicles);
                Facepunch.Pool.FreeList(ref checkPoints);

                CancelInvoke();
                InvokeHandler.CancelInvoke(this, OpenEvent);
                InvokeHandler.CancelInvoke(this, StartEvent);
                InvokeHandler.CancelInvoke(this, EndEvent);
                InvokeHandler.CancelInvoke(this, CalculateScores);
                InvokeHandler.CancelInvoke(this, Countdown);
                InvokeHandler.CancelInvoke(this, DestroyCountdown);
                InvokeHandler.CancelInvoke(this, FinishRace);                
            }
           
            private IEnumerator DestroyCheckpoints()
            {
                for (int i = checkPoints.Count - 1; i >= 0; i--)
                {
                    CheckPoint checkPoint = checkPoints[i];
                    Destroy(checkPoint);
                    yield return new WaitForSeconds(0.5f);
                }
                checkPoints.Clear();

                Destroy(this, 15f);
            }

            #endregion

            #region Player Management
            private void SpawnPlayer(BasePlayer player)
            {
                if (player == null)
                    return;

                if (player.isMounted)
                {
                    player.GetMounted().DismountPlayer(player, true);
                    player.DismountObject();
                }

                ins.restoreData.AddData(player);

                player.inventory.Strip();

                ins.NextTick(() =>
                {
                    player.inventory.containerWear.SetFlag(ItemContainer.Flag.IsLocked, true);

                    string[] clothing = ins.configData.Racers.Clothing.GetRandom();
                    for (int i = 0; i < clothing.Length; i++)
                    {
                        Item item = ItemManager.CreateByName(clothing[i]);
                        if (item != null)
                        {
                            if (!item.MoveToContainer(player.inventory.containerWear))
                                item.Remove(0f);
                        }
                    }
                });

                player.metabolism.Reset();
                player.health = 100f;

                RaceDriver raceDriver = player.gameObject.AddComponent<RaceDriver>();
                raceDrivers.Add(raceDriver);
                spectateTargets.Add(raceDriver);

                int gridId = raceDrivers.Count - 1;
                                             
                TrackData.PointInfo gridPoint = trackData.gridPoints.ElementAt(gridId);

                BaseEntity baseEntity = CreateEntity(RaceType, gridPoint.position, gridPoint.rotation, trackData.useCommander, trackData.UseModularCars);
                RaceVehicle raceVehicle = baseEntity.gameObject.AddComponent(VehicleComponentFromRaceType(RaceType)) as RaceVehicle;

                ins.spawnedEntities.Add(baseEntity.net.ID);

                raceVehicles.Add(raceVehicle);

                if (RaceType == RaceType.BoogieBoard || RaceType == RaceType.InnerTube || RaceType == RaceType.Kayak)
                    baseEntity.GetComponent<Rigidbody>().isKinematic = true;

                if (RaceType == RaceType.Horse)
                    (baseEntity as RidableHorse).SetBreed(trackData.horseBreed);               
                                
                raceVehicle.Driver = raceDriver;
                raceVehicle.Manager = this;
                raceVehicle.gridId = gridId;

                raceDriver.Vehicle = raceVehicle;
                raceDriver.Manager = this;

                if (player.isMounted)
                {
                    player.GetMounted()?.DismountPlayer(player);
                    ins.NextTick(() => ins.MovePosition(player, raceVehicle.transform.position + raceVehicle.transform.up, true));
                    return;
                }
                else ins.MovePosition(player, raceVehicle.transform.position + raceVehicle.transform.up, true);                   
            }

            private BaseEntity CreateEntity(RaceType raceType, Vector3 position, float rotation, bool useCommander, bool useModularCars)
            {
                if (useModularCars)                
                    return SpawnModularCar(position, rotation);

                if (raceType == RaceType.Car && ins.CarCommander && useCommander)
                    return SpawnCarCommander(position, rotation);

                if (raceType == RaceType.Boat && ins.BoatCommander && useCommander)
                    return SpawnBoatCommander(position, rotation);

                if (raceType == RaceType.Minicopter && ins.HeliCommander && useCommander)
                    return SpawnHeliCommander(position, rotation);

                string prefab = EntityFromRaceType(raceType);
                
                BaseEntity baseEntity = GameManager.server.CreateEntity(prefab, position + (Vector3.up * 0.2f), Quaternion.Euler(0, rotation, 0));
                baseEntity.enableSaving = false;
                baseEntity.Spawn();
                baseEntity.enabled = false;

                return baseEntity;
            }

            private BaseEntity SpawnCarCommander(Vector3 position, float rotation)
            {
                BaseEntity baseEntity = ins.CarCommander.Call("SpawnAtLocation", position + (Vector3.up * 0.2f), Quaternion.Euler(0, rotation, 0), false, true) as BasicCar;
                ins.CarCommander.Call("ToggleController", baseEntity as BasicCar, false);
                return baseEntity;
            }

            private BaseEntity SpawnBoatCommander(Vector3 position, float rotation)
            {
                BaseEntity baseEntity = ins.BoatCommander.Call("SpawnAtLocation", position + (Vector3.up * 0.2f), Quaternion.Euler(0, rotation, 0), false, true, true, false, false, false, RaceType == RaceType.RHIB) as MotorRowboat;
                ins.BoatCommander.Call("ToggleController", baseEntity as MotorRowboat, false);
                return baseEntity;
            }

            private BaseEntity SpawnHeliCommander(Vector3 position, float rotation)
            {
                BaseEntity baseEntity = ins.HeliCommander.Call("SpawnAtLocation", position + (Vector3.up * 0.2f), Quaternion.Euler(0, rotation, 0), "Mini", false, 0UL, true) as MiniCopter;
                ins.HeliCommander.Call("ToggleController", baseEntity, false);
                return baseEntity;
            }

            private BaseEntity SpawnModularCar(Vector3 position, float rotation)
            {
                ModularCarData modularCarData = ins.carData[TrackData.modularCarProfiles.GetRandom()];
                ModularCar modularCar = modularCarData.LoadVehicle(position + (Vector3.up * 0.2f), Quaternion.Euler(0, rotation, 0));
                modularCar.SetFlag(BaseEntity.Flags.Reserved1, true);
                modularCar.SetFlag(BaseEntity.Flags.On, false);
                modularCar.rigidBody.isKinematic = true;
                modularCar.enabled = false;

                return modularCar;
            }

            public void PlayerDisconnected(BasePlayer player)
            {
                if (joiners.Contains(player))
                {
                    joiners.Remove(player);
                    if (joiners.Count < trackData.minPlayers)
                    {
                        isStarting = false;
                        InvokeHandler.CancelInvoke(this, EndEvent);
                        InvokeHandler.CancelInvoke(this, StartEvent);
                        InvokeHandler.Invoke(this, EndEvent, ins.configData.Timers.CloseTime);

                        BroadcastToChat(string.Format(msg("leftevent.wait"), player.displayName));
                    }
                    else BroadcastToChat(string.Format(msg("leftevent"), player.displayName));
                }

                RaceDriver raceDriver = player.GetComponent<RaceDriver>();
                if (raceDriver != null)
                {
                    raceDrivers.Remove(raceDriver);
                    spectateTargets.Remove(raceDriver);

                    if (!disableSpectate)
                    {
                        raceDrivers.ForEach((RaceDriver spectatorDriver) =>
                        {
                            if (spectatorDriver.Player.IsSpectating())
                            {
                                if (raceDrivers.Count == 0)
                                    spectatorDriver.FinishSpectating();
                                else
                                {
                                    if (raceDriver.SpectateTarget == raceDriver)
                                        raceDriver.UpdateSpectateTarget();
                                }
                            }
                        });
                    }
                    
                    winners[raceDrivers.Count] = new RaceScores(raceDriver, CurrentTime - StartTime, raceDriver.LapNumber - 1, raceDriver.GetDistanceTravelled());
                    winners[raceDrivers.Count].hasFinished = false;

                    raceDriver.DismountPlayer();
                    Destroy(raceDriver);

                    totalRacers -= 1;

                    if (raceDrivers.Count == 0)
                        EndEvent();
                }
            } 

            public TrackData.PointInfo GetLastCheckpoint(int number)
            {
                if (number < 0)
                    number = 0;
                if (number > trackData.checkPoints.Count - 1)
                    number = trackData.checkPoints.Count - 1;
                return trackData.checkPoints[number];
            }
            #endregion

            #region Checkpoints           
            private IEnumerator CreateCheckPoints()
            {
                print("[RaceTrack] Creating race checkpoints, please wait!");
                enabled = true;
                for (int i = 0; i < trackData.checkPoints.Count; i++)
                {
                    print($"[RaceTrack] Building Checkpoint {i + 1}/{trackData.checkPoints.Count}");
                    TrackData.PointInfo pointInfo = trackData.checkPoints[i];

                    CheckPoint checkPoint = new GameObject().AddComponent<CheckPoint>();
                    checkPoint.transform.position = pointInfo.position;
                    checkPoint.transform.rotation = Quaternion.Euler(0, pointInfo.rotation, 0);
                    checkPoint.SetCheckpointData(i, pointInfo.size);
                    checkPoints.Add(checkPoint);

                    yield return new WaitWhile(()=> checkPoint.IsBuilding);
                    yield return new WaitForSeconds(0.5f);
                }

                print("[RaceTrack] All checkpoints have been loaded. Race is now open!");
                ins.SaveEntityData();
                countdown = Mathf.Max(11, ins.configData.Timers.Countdown);
                status = EventStatus.Open;
                ins.BroadcastEvent();
                InvokeHandler.Invoke(this, EndEvent, ins.configData.Timers.CloseTime);
                enabled = false;
            }           
            #endregion

            #region Pre-race Setup
            private void Countdown()
            {
                --countdown;
                ins.UpdateCountdown(raceDrivers, countdown > 0 ? string.Format(msg("countdown.count"), countdown) : msg("countdown.go"));
                if (countdown > 0)
                    InvokeHandler.Invoke(this, Countdown, 1f);
                else
                {
                    startTime = CurrentTime;
                    EnableAllVehicles();
                    InvokeHandler.Invoke(this, DestroyCountdown, 1f);

                    if (trackData.maxTime > 0)
                    {
                        foreach(RaceDriver raceDriver in raceDrivers)
                        {
                            raceDriver.Player.ChatMessage(string.Format(msg("eventtimer", raceDriver.Player.UserIDString), ins.FormatTime(trackData.maxTime)));
                        }
                        endTime = UnityEngine.Time.realtimeSinceStartup + trackData.maxTime;
                        InvokeHandler.Invoke(this, FinishRace, trackData.maxTime);
                    }
                }
            }

            private void EnableAllVehicles()
            {
                foreach(RaceDriver raceDriver in raceDrivers)
                {
                    RaceVehicle raceVehicle = raceDriver.Vehicle;

                    if (RaceType == RaceType.Car)
                    {
                        if (TrackData.UseModularCars)
                        {
                            raceVehicle.entity.SetFlag(BaseEntity.Flags.Reserved1, false);
                            raceVehicle.entity.SetFlag(BaseEntity.Flags.On, true);
                            (raceVehicle.entity as ModularCar).rigidBody.isKinematic = false;
                            raceVehicle.entity.enabled = true;
                        }
                        else
                        {
                            if (ins.CarCommander && TrackData.useCommander)
                                ins.CarCommander.Call("ToggleController", raceVehicle.entity as BasicCar, true);
                            else raceVehicle.entity.GetComponent<BasicCar>().enabled = true;
                        }
                    }
                    else if (RaceType == RaceType.Boat || RaceType == RaceType.RHIB)
                    {
                        if (ins.BoatCommander && TrackData.useCommander)
                            ins.BoatCommander.Call("ToggleController", raceVehicle.entity as MotorRowboat, true);
                        else raceVehicle.entity.GetComponent<MotorRowboat>().enabled = true;

                        raceVehicle.entity.GetComponent<MotorRowboat>().EngineToggle(true);
                    }
                    else if (RaceType == RaceType.Kayak || RaceType == RaceType.BoogieBoard || RaceType == RaceType.InnerTube)
                    {
                        raceVehicle.entity.GetComponent<Rigidbody>().isKinematic = false;
                    }                   
                    else if (RaceType == RaceType.Horse)
                    {
                        raceVehicle.entity.GetComponent<BaseRidableAnimal>().enabled = true;
                    }
                    else
                    {
                        if (ins.HeliCommander && TrackData.useCommander)
                            ins.HeliCommander.Call("ToggleController", raceVehicle.entity as MiniCopter, true);
                        else raceVehicle.entity.GetComponent<MiniCopter>().enabled = true;

                        (raceVehicle as RaceHelicopter).OnRaceStarted();

                        raceVehicle.entity.GetComponent<MiniCopter>().EngineStartup();
                    }

                    raceDriver.StartLap();
                }
                isRacing = true;
            }

            private void DestroyCountdown()
            {
                foreach (var raceDriver in raceDrivers)
                {
                    raceDriver.DestroyUI(UIInfo);
                    raceDriver.DestroyUI(UITime);

                    if (trackData.raceMode == RaceMode.Laps)
                        ins.UpdateLapCounter(raceDriver, true, null);
                    else ins.UpdateLapCounter(raceDriver, false, null);
                }               
            }
            #endregion

            #region Finished Racers
            public void FinishedRace(RaceDriver raceDriver)
            {
                spectateTargets.Remove(raceDriver);
                
                if (!hasLaunchedFirework && ins.configData.Checkpoints.LaunchFirework)
                {
                    MortarFirework firework = GameManager.server.CreateEntity(ins.configData.Checkpoints.FireworkPrefab, raceDriver.transform.position + (Vector3.up * 3f)) as MortarFirework;
                    if (firework != null)
                    {
                        firework.enableSaving = false;
                        firework.Spawn();
                        firework.Ignite();
                    }
                    hasLaunchedFirework = true;
                }

                winners[playersFinished] = new RaceScores(raceDriver, trackData);
                playersFinished++;

                if (!disableSpectate)
                {
                    raceDrivers.ForEach((RaceDriver spectatorDriver) =>
                    {
                        if (spectatorDriver.Player.IsSpectating())
                        {
                            if (playersFinished >= totalRacers)
                                spectatorDriver.FinishSpectating();
                            else
                            {
                                if (spectatorDriver.SpectateTarget == raceDriver)
                                    spectatorDriver.UpdateSpectateTarget();
                            }
                        }
                    });
                }
                
                raceDriver.DismountPlayer();                
                    
                if (!isEnding)
                {
                    isEnding = true;

                    int finishTime = ins.configData.Timers.EndTime;
                    if (trackData.maxTime > 0)
                    {
                        double forcedFinishTime = endTime - UnityEngine.Time.realtimeSinceStartup;
                        if (forcedFinishTime < finishTime)
                            finishTime = (int)forcedFinishTime;
                    }

                    foreach(RaceDriver driver in raceDrivers)                    
                        driver.Player.ChatMessage(string.Format(msg("win.end", driver.Player.UserIDString), raceDriver.Player.displayName, finishTime));

                    timerTick = ins.configData.Timers.EndTime;
                    TimerTick();                    
                }

                if (playersFinished >= totalRacers)
                {
                    InvokeHandler.CancelInvoke(this, FinishRace);
                    FinishRace();
                }
                else
                {
                    if (disableSpectate)                    
                        LeaveEvent(raceDriver.Player, true);                    
                    else ins.NextTick(raceDriver.BeginSpectating);                    
                }

                ins.NextTick(() => Destroy(raceDriver.Vehicle));
            }

            private void TimerTick()
            {
                timerTick -= 1;

                if (timerTick == 30 || timerTick == 10)
                {
                    int finishTime = timerTick;
                    if (trackData.maxTime > 0)
                    {
                        double forcedFinishTime = endTime - UnityEngine.Time.realtimeSinceStartup;
                        if (forcedFinishTime < finishTime)
                            finishTime = (int)forcedFinishTime;
                    }

                    foreach (RaceDriver driver in raceDrivers)
                        driver.Player.ChatMessage(string.Format(msg("win.countdown", driver.Player.UserIDString), finishTime));
                }
                if (timerTick > 0)
                    InvokeHandler.Invoke(this, TimerTick, 1f);

                if (timerTick == 0)
                    FinishRace();                
            }   
            
            public void FinishRace()
            {
                InvokeHandler.CancelInvoke(this, TimerTick);                

                status = EventStatus.Finishing;
                foreach(RaceDriver raceDriver in raceDrivers)
                {
                    if (raceDriver.Player.IsSpectating())
                        raceDriver.FinishSpectating();

                    raceDriver.DestroyAllUI();                   
                }
                ins.NextTick(DismountDrivers);
            }

            private void DismountDrivers()
            {
                foreach (RaceDriver raceDriver in raceDrivers)                
                    raceDriver.DismountPlayer();                   
                
                ins.NextTick(CalculateScores);
            }

            public void UpdateLapCounter(RaceDriver raceDriver)
            {
                ins.UpdateLapCounter(raceDriver, RaceMode == RaceMode.Laps, null);

                foreach (RaceDriver spectator in raceDrivers)                
                {
                    if (spectator.SpectateTarget == raceDriver)
                        ins.UpdateLapCounter(raceDriver, RaceMode == RaceMode.Laps, spectator);
                }
            }

            public void UpdateLapTime(RaceDriver raceDriver)
            {
                ins.UpdateLapTime(raceDriver, raceDriver.LastLapTime, null);

                foreach (RaceDriver spectator in raceDrivers)
                {
                    if (spectator.SpectateTarget == raceDriver)
                        ins.UpdateLapTime(raceDriver, raceDriver.LastLapTime, spectator);
                }
            }
            #endregion

            #region Scores
            public void CalculateScores()
            {
                foreach (RaceDriver raceDriver in raceDrivers.Where(x => !x.HasFinished).OrderByDescending(x => x.GetDistanceTravelled()))
                {
                    winners[playersFinished] = new RaceScores(raceDriver, CurrentTime - StartTime, raceDriver.LapNumber - 1, raceDriver.GetDistanceTravelled());
                    playersFinished++;
                }

                string podiumStr = string.Empty;
                for (int i = 0; i < 3; i++)
                {
                    if (winners.Length >= i + 1 && winners[i] != null)
                        podiumStr += string.Format(msg("win.podium"), i == 0 ? "\n1st" : i == 1 ? "\n2nd" : "\n3rd", ins.StripTags(winners[i].displayName));
                }

                BroadcastToChat(string.Format(msg("win.winners"), podiumStr));
                BroadcastToChat(msg("win.viewscores"));

                ins.CreateScoreboard(TrackName, winners);

                foreach (RaceDriver raceDriver in raceDrivers)
                    raceDriver.AddUI(ins.scoreContainer, UIScoreboard);

                IssueRewards(winners);
                ServerMgr.Instance.StartCoroutine(DestroyCheckpoints());                
            }
           
            private void IssueRewards(RaceScores[] raceScores)
            {
                int amount = raceScores.Length < 3 ? raceScores.Length : 3;

                for (int i = 0; i < amount; i++)
                {
                    if (raceScores[i] == null)
                        continue;

                    if (raceScores[i].hasFinished)
                        ins.IssueReward(raceScores[i].playerId, i + 1);
                }              
            }

            public class RaceScores
            {
                public ulong playerId;
                public string displayName;

                public double raceTime;
                public float laps;
                public float distance;
                public bool hasFinished;

                public RaceScores() { }
                public RaceScores(RaceDriver raceDriver, TrackData trackData)
                {
                    playerId = raceDriver.Player.userID;
                    displayName = raceDriver?.Player?.displayName ?? "Unknown";

                    if (raceDriver.HasFinished)
                    {
                        distance = trackData.raceMode == RaceMode.Laps ? trackData.totalDistance * trackData.laps : trackData.totalDistance;
                        raceTime = raceDriver.FinishTime;
                        laps = trackData.laps;
                        hasFinished = true;
                    }                    
                }

                public RaceScores(RaceDriver raceDriver, double raceTime, float laps, float distance)
                {
                    playerId = raceDriver.Player.userID;
                    displayName = raceDriver.Player?.displayName ?? "Unknown";

                    this.distance = distance;
                    this.laps = laps;
                    this.raceTime = raceTime;
                }                
            }
            #endregion
        }

        private class RaceDriver : MonoBehaviour
        {
            private BasePlayer player;
            private RaceVehicle raceVehicle;
            private RaceManager raceManager;

            private int lapNumber;
            private int nextPoint;

            private bool hasFinished;

            private double lapStart;
            private double finishTime;
            private double lastLapTime;
            private double totalRaceTime;
            private float cachedDistance = 0;

            private double lastMissedMsg;

            private int spectateIndex = 0;
            private RaceDriver spectateTarget = null;

            private List<string> uiPanels = new List<string>();

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                enabled = false;
                lapNumber = 1;
                nextPoint = 1;
            }

            private void OnDestroy()
            {
                if (player != null && player.IsConnected)
                {
                    DestroyAllUI();

                    if (player.isMounted)
                        DismountPlayer();

                    player.SetParent(null, false, true);
                    ins.restoreData.RestorePlayer(player);
                }
                Destroy(raceVehicle);
            }

            #region Components
            public BasePlayer Player
            {
                get
                {
                    return player;
                }
            }

            public RaceDriver SpectateTarget
            {
                get
                {
                    return spectateTarget;
                }
            }

            public RaceVehicle Vehicle
            {
                get
                {
                    return raceVehicle;
                }
                set
                {
                    raceVehicle = value;
                }
            }

            public RaceManager Manager
            {
                set
                {
                    raceManager = value;
                }
            }
            #endregion

            #region Laps
            public int NextCheckpoint
            {
                get
                {
                    return nextPoint;
                }
                set
                {
                    nextPoint = value;
                }
            }

            public int LapNumber
            {
                get
                {
                    return lapNumber;
                }
                set
                {
                    lapNumber = value;
                }
            }

            public double LapStart
            {
                get
                {
                    return lapStart;
                }
            }

            public double FinishTime
            {
                get
                {
                    return finishTime;
                }
                set
                {
                    finishTime = value;
                    hasFinished = true;
                }
            }

            public double LastLapTime
            {
                get
                {
                    return lastLapTime;
                }
            }

            public double TotalRaceTime
            {
                get
                {
                    return totalRaceTime;
                }
            }

            public bool HasFinished
            {
                get
                {
                    return hasFinished;
                }
            }

            public void StartLap() => lapStart = raceManager.CurrentTime;

            public void FinishedLap(bool checkPoint = false)
            {
                double currentTime = raceManager.CurrentTime;
                double time = currentTime - lapStart;
                lastLapTime = time;
                totalRaceTime += time;
                lapStart = currentTime;
            }

            public void MissedCheckpoint()
            {
                if (lastMissedMsg < raceManager.CurrentTime)
                {
                    player.ChatMessage(msg("missedcp", player.UserIDString));
                    lastMissedMsg = raceManager.CurrentTime + 3;
                }
            }

            public float GetDistanceTravelled()
            {
                if (cachedDistance != 0)
                    return cachedDistance;

                float distance = 0;

                if (raceManager.RaceMode == RaceMode.Laps)
                    distance += (raceManager.TrackData.totalDistance * (LapNumber - 1));

                for (int i = 1; i < NextCheckpoint; i++)
                    distance += Vector3.Distance(raceManager.TrackData.checkPoints[i - 1].position, raceManager.TrackData.checkPoints[i].position);

                if (NextCheckpoint > 0 && player.transform != null)
                    distance += Vector3.Distance(player.transform.position, raceManager.TrackData.checkPoints[NextCheckpoint - 1].position);

                cachedDistance = distance;
                return distance;
            }
            #endregion

            #region Mounting
            public void MountPlayer(RaceVehicle raceVehicle)
            {
                raceVehicle.entity._name = player.displayName;
                player.EnsureDismounted();

                if (raceManager.RaceType == RaceType.Car && ins.CarCommander && raceManager.TrackData.useCommander)
                {
                    ins.CarCommander.Call("MountPlayerTo", player, raceVehicle.entity as BasicCar);
                    return;
                }
                else if ((raceManager.RaceType == RaceType.Boat || raceManager.RaceType == RaceType.RHIB) && ins.BoatCommander && raceManager.TrackData.useCommander)
                {
                    ins.BoatCommander.Call("MountPlayerTo", player, raceVehicle.entity as MotorRowboat);
                    return;
                }               
                else if (raceManager.RaceType == RaceType.Minicopter && ins.HeliCommander && raceManager.TrackData.useCommander)
                {
                    ins.HeliCommander.Call("MountPlayerTo", player, raceVehicle.entity as MiniCopter, false);
                    return;
                }
                else if (raceManager.RaceType == RaceType.Kayak)
                {
                    ItemManager.CreateByName(KAYAK_PADDLE_ITEM).MoveToContainer(player.inventory.containerBelt);
                }

                if (raceVehicle.entity is BaseVehicle)                
                    (raceVehicle.entity as BaseVehicle).mountPoints[0].mountable.MountPlayer(player);                
                else raceVehicle.entity.MountPlayer(player);
            }

            public void DismountPlayer()
            {
                if (player == null || raceVehicle == null || raceVehicle.entity == null)
                    return;

                if (raceVehicle is RaceCar)
                {
                    if (!raceManager.TrackData.UseModularCars && ins.CarCommander && raceManager.TrackData.useCommander)
                        ins.CarCommander.Call("EjectAllPlayers", raceVehicle.entity as BasicCar);
                    else raceVehicle.entity.DismountAllPlayers();
                }
                else if (raceVehicle is RaceBoat)
                {
                    if (ins.BoatCommander && raceManager.TrackData.useCommander)
                        ins.BoatCommander.Call("EjectAllPlayers", raceVehicle.entity as MotorRowboat);
                    else raceVehicle.entity.DismountAllPlayers();
                }
                else if (raceVehicle is RaceHorse)
                    raceVehicle.entity.DismountAllPlayers();
                else
                {
                    if (ins.HeliCommander && raceManager.TrackData.useCommander)
                        ins.HeliCommander.Call("EjectAllPlayers", raceVehicle.entity as MiniCopter);
                    else raceVehicle.entity.DismountAllPlayers();
                }
            }
            #endregion

            #region Spectating  
            public void BeginSpectating()
            {
                if (player.IsSpectating())
                    return;

                DestroyAllUI();

                player.StartSpectating();
                player.ChatMessage(msg("spectatecycle", player.UserIDString));        
                UpdateSpectateTarget();
            }

            public void FinishSpectating()
            {
                if (!player.IsSpectating())
                    return;

                player.SetParent(null, false, false);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);
                player.gameObject.SetLayerRecursive(17);
            }

            public void SetSpectateTarget(RaceDriver raceDriver)
            {                
                ins.UpdateLapCounter(spectateTarget, raceManager.RaceMode == RaceMode.Laps, this);
                player.ChatMessage($"Spectating: {raceDriver.player.displayName}");

                player.SendEntitySnapshot(raceDriver.Player);
                player.gameObject.Identity();
                player.SetParent(raceDriver.Player, false, false);                       
            }

            public void UpdateSpectateTarget(int index = 0)
            {                
                int newIndex = spectateIndex += index;

                if (newIndex > raceManager.spectateTargets.Count - 1)
                    newIndex = 0;
                else if (newIndex < 0)
                    newIndex = raceManager.spectateTargets.Count - 1;

                if (raceManager.spectateTargets[newIndex] == spectateTarget)
                    return;

                spectateIndex = newIndex;
                spectateTarget = raceManager.spectateTargets[spectateIndex];
                SetSpectateTarget(spectateTarget);
            }
            #endregion

            #region UI
            public void AddUI(CuiElementContainer container, string panel, float destroyIn = 0)
            {
                DestroyUI(panel);

                uiPanels.Add(panel);
                CuiHelper.AddUi(player, container);

                if (destroyIn > 0)
                    InvokeHandler.Invoke(this, ()=> DestroyUI(panel), destroyIn);
            }

            public void DestroyUI(string panel)
            {
                if (uiPanels.Contains(panel))
                    uiPanels.Remove(panel);
                CuiHelper.DestroyUi(player, panel);
            }

            public void DestroyAllUI()
            {
                foreach (var panel in uiPanels)
                    CuiHelper.DestroyUi(player, panel);
                CuiHelper.DestroyUi(player, UIInfo);
                CuiHelper.DestroyUi(player, UITime);
                CuiHelper.DestroyUi(player, UILaps);
                CuiHelper.DestroyUi(player, UILapTime);
                CuiHelper.DestroyUi(player, UIScoreboard);              
            }
            #endregion
        }

        private class RaceCar : RaceVehicle
        {
            public override void Awake()
            {
                base.Awake();

                InvokeHandler.InvokeRepeating(this, CheckUpsideDown, 3, 3);
            }

            public override void OnDestroy()
            {
                InvokeHandler.CancelInvoke(this, CheckUpsideDown);
                base.OnDestroy();
            }           
          
            private void CheckUpsideDown()
            {
                if (Vector3.Dot(entity.transform.up, Vector3.down) > 0)
                {
                    raceDriver.Player.ChatMessage(msg("reset_vehicle", raceDriver.Player.UserIDString));
                }
            }  
            
        }

        private class RaceHelicopter : RaceVehicle
        {
            private StorageContainer container;
            private MiniCopter minicopter;

            public override void Awake()
            {
                base.Awake();
                enabled = true;
                minicopter = entity.GetComponent<MiniCopter>();
                InitializeFuel();
                InvokeHandler.InvokeRepeating(this, CheckUpsideDown, 3, 3);
            }

            private void Update()
            {
                minicopter.ApplyWheelForce(minicopter.frontWheel, 0, 1, 0f);
                minicopter.ApplyWheelForce(minicopter.leftWheel, 0, 1, 0f);
                minicopter.ApplyWheelForce(minicopter.rightWheel, 0, 1, 0f);
            }

            public override void OnDestroy()
            {
                InvokeHandler.CancelInvoke(this, CheckUpsideDown);
                base.OnDestroy();
            }

            public void OnRaceStarted() => enabled = false;

            private void InitializeFuel()
            {
                StorageContainer container = minicopter.GetFuelSystem().GetFuelContainer();
                Item item = ItemManager.CreateByItemID(-946369541, 1000);
                item.MoveToContainer(container.inventory, -1, true);
                container.SetFlag(BaseEntity.Flags.Locked, true);

                minicopter.fuelPerSec = 0f;
            }

            private void CheckUpsideDown()
            {                
                if (minicopter.Waterlogged())
                {
                    raceDriver.Player.ChatMessage(msg("reset_vehicle", raceDriver.Player.UserIDString));
                }
            }
        }

        private class RaceBoat : RaceVehicle
        {
            private StorageContainer container;

            public override void Awake()
            {                
                base.Awake();

                if (entity is MotorRowboat)
                    InitializeFuel((entity as MotorRowboat).fuelSystem.GetFuelContainer());

                InvokeHandler.InvokeRepeating(this, CheckStuck, 3, 3);
            }

            public override void OnDestroy()
            {
                InvokeHandler.CancelInvoke(this, CheckStuck);
                base.OnDestroy();
            }

            private void InitializeFuel(StorageContainer container)
            {                
                Item item = ItemManager.CreateByItemID(-946369541, 1000);
                item.MoveToContainer(container.inventory, -1, true);
                container.SetFlag(BaseEntity.Flags.Locked, true);
            }

            private void CheckStuck()
            {
                if ((TerrainMeta.WaterMap.GetHeight(entity.transform.position) - TerrainMeta.HeightMap.GetHeight(entity.transform.position) >= 0) && !IsFlipped())                
                    return;

                raceDriver.Player.ChatMessage(msg("reset_vehicle", raceDriver.Player.UserIDString));
            }

            private bool IsFlipped() => Vector3.Dot(Vector3.up, entity.transform.up) <= 0f;
        }

        private class RaceHorse : RaceVehicle
        {
            private BaseRidableAnimal horse;

            public override void Awake()
            {
                base.Awake();
                horse = entity.GetComponent<BaseRidableAnimal>();
                RefreshHorse();
            }

            public override void OnDestroy()
            {
                base.OnDestroy();
            }  
            
            private void RefreshHorse()
            {
                horse.ReplenishStaminaCore(1500, 1500);
                horse.ReplenishStamina(1500);
                horse.Heal(horse.MaxHealth());
                horse.DoNetworkUpdate();
            }
        }

        private class RaceVehicle : MonoBehaviour
        {
            public BaseMountable entity;
            internal RaceDriver raceDriver;
            internal RaceManager raceManager;
            private int lastCheckpoint;
            public int gridId;

            private bool hasFinished;

            public virtual void Awake()
            {
                entity = GetComponent<BaseMountable>();
                enabled = false;
            }

            public virtual void OnDestroy()
            {
                if (entity != null && !entity.IsDestroyed)
                {
                    if (entity is ModularCar)
                    {
                        ModularCar m = entity as ModularCar;
                        for (int i = 0; i < m.AttachedModuleEntities.Count; i++)
                        {
                            VehicleModuleStorage storage = m.AttachedModuleEntities[i] as VehicleModuleStorage;
                            if (storage != null)
                            {
                                ItemContainer container = storage.GetContainer()?.inventory;
                                if (container != null)
                                {
                                    for (int y = container.itemList.Count - 1; y >= 0; y--)
                                    {
                                        Item item = container.itemList[y];
                                        item.RemoveFromContainer();
                                        item.Remove();
                                    }
                                }
                            }
                        }                        
                    }

                    entity.Kill();
                }
            }

            #region Components
            public RaceDriver Driver
            {
                get
                {
                    return raceDriver;
                }
                set
                {
                    raceDriver = value;
                }
            }

            public RaceManager Manager
            {
                set
                {
                    raceManager = value;
                }
            }
            #endregion

            #region Checkpoints
            public void HitCheckPoint(int number)
            {
                if (raceDriver.HasFinished)
                    return;

                if (raceDriver.NextCheckpoint != number)
                {
                    if (raceManager.RaceMode == RaceMode.Laps && raceDriver.LapNumber == 1 && number == raceManager.CheckpointCount)
                        return;

                    if (raceDriver.NextCheckpoint - 1 == number)
                        return;

                    if (lastCheckpoint == number)
                        return;

                    if (raceDriver.NextCheckpoint < number)
                        raceDriver.MissedCheckpoint();
                    return;
                }

                if (raceManager.RaceMode == RaceMode.Laps)
                {
                    if (number == raceManager.CheckpointCount)
                    {
                        raceDriver.FinishedLap(false);
                        if (raceDriver.LapNumber == raceManager.Laps && !hasFinished)
                        {
                            hasFinished = true;
                            raceDriver.DestroyAllUI();
                            raceDriver.FinishTime = raceManager.CurrentTime - raceManager.StartTime;
                            raceManager.FinishedRace(raceDriver);
                        }
                        else
                        {
                            raceDriver.LapNumber += 1;
                            raceDriver.NextCheckpoint = 1;

                            raceManager.UpdateLapCounter(raceDriver);
                            raceManager.UpdateLapTime(raceDriver);
                        }
                    }
                    else raceDriver.NextCheckpoint += 1;
                }
                else
                {
                    raceDriver.FinishedLap(true);
                    if (number == raceManager.CheckpointCount && !hasFinished)
                    {
                        hasFinished = true;
                        raceDriver.DestroyAllUI();
                        raceDriver.FinishTime = raceManager.CurrentTime - raceManager.StartTime;
                        raceManager.FinishedRace(raceDriver);
                    }
                    else
                    {
                        raceDriver.NextCheckpoint += 1;
                        raceManager.UpdateLapCounter(raceDriver);
                    }
                }

                lastCheckpoint = number;
            }
            #endregion

            public void ResetVehicle()
            {
                Rigidbody rb = entity.GetComponent<Rigidbody>();
                if (rb != null)
                    rb.velocity = Vector3.zero;     
                
                TrackData.PointInfo pointInfo = raceDriver.NextCheckpoint == 1 ? raceManager.TrackData.gridPoints.ElementAt(gridId) : raceManager.GetLastCheckpoint(raceDriver.NextCheckpoint == 0 ? raceManager.CheckpointCount - 2 : raceDriver.NextCheckpoint - 2);
                entity.transform.position = pointInfo.position;
                entity.transform.rotation = Quaternion.Euler(0, pointInfo.rotation, 0);

                if (entity is MiniCopter)
                {
                    entity.SetFlag(BaseEntity.Flags.On, true, false, true);
                    entity.SetFlag(BaseEntity.Flags.Reserved4, false, false, true);
                }

                entity.SendNetworkUpdate();
            }           
        }

        private class CheckPoint : MonoBehaviour
        {
            private List<Marker> markers;
            private int number;

            public bool IsBuilding { get; private set; }

            private void Awake()
            {
                markers = new List<Marker>();
                enabled = false;
            }

            private void OnTriggerEnter(Collider col)
            {
                RaceDriver raceDriver = col.GetComponentInChildren<RaceDriver>();
                if (raceDriver != null)
                    raceDriver.Vehicle.HitCheckPoint(number);                              
            }
            private void OnTriggerExit(Collider col)
            {
                RaceDriver raceDriver = col.GetComponentInChildren<RaceDriver>();
                if (raceDriver != null)
                    raceDriver.Vehicle.HitCheckPoint(number);
            }

            private void OnDestroy()
            {
                foreach (Marker marker in markers)
                    Destroy(marker);
            }

            public int Number
            {
                get
                {
                    return number;
                }
            }            

            public void SetCheckpointData(int number, float radius)
            {
                this.number = number + 1;
                
                var collider = gameObject.AddComponent<BoxCollider>();
                collider.transform.position = transform.position;
                collider.transform.rotation = transform.rotation;
                collider.gameObject.layer = (int)Layer.Reserved1;
                collider.size = new Vector3(radius * 2, radius * 2, 4f);
                collider.isTrigger = true;

                ServerMgr.Instance.StartCoroutine(SpawnMarkers(radius));
            }    
            
            private IEnumerator SpawnMarkers(float radius)
            {
                IsBuilding = true;

                int objectCount = (int)(2 * Math.PI * radius) / 3;

                Vector3 lastPos = Vector3.zero;
                int count = ins.manager.RaceType == RaceType.Minicopter ? objectCount : (objectCount / 2) + 1;
                int i = 0;
                while (i <= count)
                {                    
                    float angle = i * Mathf.PI * 2 / objectCount;
                    Vector3 position = transform.position + (transform.rotation * (new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius));
                    
                    BuildingBlock block = GameManager.server.CreateEntity(WALL_PREFAB, position) as BuildingBlock;
                    block.enableSaving = false;
                    block.Spawn();

                    block.transform.LookAt(transform, transform.right);
                    block.transform.rotation = block.transform.rotation * (i == 0 ? Quaternion.Euler(270, 90, 0) : Quaternion.Euler(90, 90, 0));
                    block.grounded = true;

                    block.SetGrade(ins.ParseType<BuildingGrade.Enum>(ins.configData.Checkpoints.Grade));
                    block.health = block.MaxHealth();

                    markers.Add(block.gameObject.AddComponent<Marker>());
                    ins.spawnedEntities.Add(block.net.ID);

                    if (ins.configData.Checkpoints.Lights)
                    {
                        if (i < count)
                        {
                            if (ins.lightType == LightType.Lantern)
                            {
                                BaseOven oven = GameManager.server.CreateEntity(LANTERN_PREFAB, position, Quaternion.LookRotation(lastPos - position, -transform.up)) as BaseOven;
                                oven.enableSaving = false;
                                oven.Spawn();
                                
                                oven.GetComponent<DestroyOnGroundMissing>().enabled = false;
                                oven.GetComponent<GroundWatch>().enabled = false;

                                markers.Add(oven.gameObject.AddComponent<LightMarker>());
                                ins.spawnedEntities.Add(oven.net.ID);
                            }
                            else
                            {
                                IOEntity ioEntity = GameManager.server.CreateEntity(ins.lightType == LightType.Flasher ? FLASHER_PREFAB : SIREN_PREFAB, position, Quaternion.LookRotation(lastPos - position, -transform.up)) as IOEntity;
                                ioEntity.enableSaving = false;
                                ioEntity.Spawn();

                                ioEntity.SetFlag(BaseEntity.Flags.Reserved8, true);
                                markers.Add(ioEntity.gameObject.AddComponent<Marker>());
                                ins.spawnedEntities.Add(ioEntity.net.ID);
                            }
                           
                        }
                    }
                    lastPos = block.transform.position;
                    i++;
                    yield return new WaitForEndOfFrame();
                    yield return new WaitForEndOfFrame();
                    yield return new WaitForEndOfFrame();
                    yield return new WaitForEndOfFrame();
                    yield return new WaitForEndOfFrame();
                }

                IsBuilding = false;
            }
            
            public class Marker : MonoBehaviour
            {
                internal BaseEntity entity;                

                public virtual void Awake()
                {
                    entity = GetComponent<BaseEntity>();
                    enabled = false;                    
                }

                public virtual void OnDestroy()
                {
                    if (entity != null && !entity.IsDestroyed)
                        entity.Kill();
                }                      
            }  
            
            public class LightMarker : Marker
            {
                private bool isOn = false;

                public override void Awake()
                {
                    base.Awake();
                    InvokeHandler.InvokeRepeating(this, ToggleLight, 0.75f, 0.75f);
                }
                public override void OnDestroy()
                {
                    InvokeHandler.CancelInvoke(this, ToggleLight);
                    base.OnDestroy();
                }

                private void ToggleLight()
                {
                    isOn = !isOn;
                    entity.SetFlag(BaseEntity.Flags.On, isOn);
                }
            } 
        }
        #endregion

        #region Rewards
        private void IssueReward(ulong playerId, int position)
        {
            if (!configData.Rewards.Enabled)
                return;

            if (!configData.Rewards.Podium && position > 1)
                return;

            int amount = position == 1 ? configData.Rewards.Prize1 : position == 2 ? configData.Rewards.Prize2 : configData.Rewards.Prize3;

            if (configData.Rewards.ServerRewards)
                ServerRewards?.Call("AddPoints", playerId, amount);

            if (configData.Rewards.Economics)
                Economics?.Call("Deposit", playerId.ToString(), (double)amount);

            if (configData.Rewards.Scrap)
            {
                if (disableSpectate)
                {
                    BasePlayer player = BasePlayer.FindByID(playerId);
                    if (player != null)
                        player.GiveItem(ItemManager.CreateByItemID(-932201673, amount), BaseEntity.GiveItemReason.PickedUp);
                }
                else restoreData.AddPrizeToData(playerId, -932201673, amount);
            }
        }

        private void IssueRefunds(List<BasePlayer> players)
        {           
            foreach (BasePlayer player in players)            
                IssueRefund(player);            
        }

        private void IssueRefund(BasePlayer player)
        {
            if (!configData.Entrance.Enabled)
                return;

            int amount = configData.Entrance.Amount;

            if (configData.Entrance.ServerRewards)
                ServerRewards?.Call("AddPoints", player.userID, amount);

            if (configData.Entrance.Economics)
                Economics?.Call("Deposit", player.UserIDString, (double)amount);

            if (configData.Entrance.Scrap)
                player.GiveItem(ItemManager.CreateByItemID(-932201673, amount), BaseEntity.GiveItemReason.PickedUp);
        }
        #endregion

        #region Teleportation
        private void MovePosition(BasePlayer player, Vector3 destination, bool sleep)
        {
            if (sleep)
            {
                if (player.net?.connection != null)
                    player.ClientRPCPlayer(null, player, "StartLoading");
                StartSleeping(player);
                player.MovePosition(destination);
                if (player.net?.connection != null)
                    player.ClientRPCPlayer(null, player, "ForcePositionTo", destination);
                if (player.net?.connection != null)
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
                player.UpdateNetworkGroup();
                player.SendNetworkUpdateImmediate(false);
                if (player.net?.connection == null) return;
                try { player.ClearEntityQueue(null); } catch { }
                player.SendFullSnapshot();
            }
            else
            {
                player.MovePosition(destination);
                player.ClientRPCPlayer(null, player, "ForcePositionTo", destination);
                player.SendNetworkUpdateImmediate();
                try { player.ClearEntityQueue(null); } catch { }
            }
        }
        private void StartSleeping(BasePlayer player)
        {
            if (player.IsSleeping())
                return;
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
            if (!BasePlayer.sleepingPlayerList.Contains(player))
                BasePlayer.sleepingPlayerList.Add(player);
            player.CancelInvoke("InventoryUpdate");
        }
        #endregion

        #region UI 
        const string UIInfo = "TrackUI_Info";
        const string UITime = "TrackUI_Time";
        const string UILaps = "TrackUI_Laps";
        const string UILapTime = "TrackUI_LapTime";
        const string UIScoreboard = "TrackUI_Scoreboard";
            
        public static class UI
        {
            static public CuiElementContainer ElementContainer(string panelName, string color, UI4 dimensions, bool useCursor = false, string parent = "Overlay")
            {
                var NewElement = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax()},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panelName.ToString()
                    }
                };
                return NewElement;
            }           
            static public void Panel(ref CuiElementContainer container, string panel, string color, UI4 dimensions, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() },
                    CursorEnabled = cursor
                },
                panel.ToString());
            }
            static public void Label(ref CuiElementContainer container, string panel, string text, int size, UI4 dimensions, TextAnchor align = TextAnchor.MiddleCenter, bool altFont = false)
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, Text = text, Font = altFont ? "droidsansmono.ttf" : "robotocondensed-regular.ttf" },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() }
                },
                panel.ToString());

            }
            static public void Button(ref CuiElementContainer container, string panel, string color, string text, int size, UI4 dimensions, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 0f },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel.ToString());
            }
            static public void OutlineLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string distance, UI4 dimensions, TextAnchor align = TextAnchor.MiddleCenter, string parent = "Overlay")
            {
                CuiElement textElement = new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel.ToString(),
                    FadeOut = 0.2f,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = text,
                            FontSize = size,
                            Align = align,
                            FadeIn = 0.2f
                        },
                        new CuiOutlineComponent
                        {
                            Distance = distance,
                            Color = color
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = dimensions.GetMin(),
                            AnchorMax = dimensions.GetMax()
                        }
                    }
                };
                container.Add(textElement);
            }           
            
            public static string Color(string hexColor, float alpha)
            {
                if (hexColor.StartsWith("#"))
                    hexColor = hexColor.Substring(1);
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }
        public class UI4
        {
            public float xMin, yMin, xMax, yMax;
            public UI4(float xMin, float yMin, float xMax, float yMax)
            {
                this.xMin = xMin;
                this.yMin = yMin;
                this.xMax = xMax;
                this.yMax = yMax;
            }
            public string GetMin() => $"{xMin} {yMin}";
            public string GetMax() => $"{xMax} {yMax}";
        }
        #endregion

        #region UI Display
        private void DisplayInformation(List<RaceDriver> drivers, string trackName, TrackData data)
        {
            CuiElementContainer container = UI.ElementContainer(UIInfo, "0 0 0 0", new UI4(0.3f, 0.7f, 0.7f, 1f), false);

            UI.OutlineLabel(ref container, UIInfo, "0 0 0 1", trackName, 40, "2 2", new UI4(0, 0.5f, 1, 1), TextAnchor.LowerCenter);
            UI.OutlineLabel(ref container, UIInfo, "0 0 0 1", data.raceMode == RaceMode.Laps ? string.Format(msg("ui.info.laps"), data.laps, data.checkPoints.Count) : string.Format(msg("ui.info.sprint"), data.checkPoints.Count), 20, "1 1", new UI4(0, 0.25f, 1, 0.5f), TextAnchor.MiddleLeft);
            UI.OutlineLabel(ref container, UIInfo, "0 0 0 1", string.Format(msg("ui.info.distance"), data.raceMode == RaceMode.Laps ? Math.Round(data.totalDistance * data.laps, 1) : Math.Round(data.totalDistance, 1)), 20, "1 1", new UI4(0, 0, 1, 0.5f), TextAnchor.MiddleRight);

            foreach (var raceDriver in drivers)            
                raceDriver.AddUI(container, UIInfo);            
        }

        private void UpdateCountdown(List<RaceDriver> drivers, string time)
        {
            CuiElementContainer container = UI.ElementContainer(UITime, "0 0 0 0", new UI4(0.3f, 0.3f, 0.7f, 0.7f), false);
            UI.OutlineLabel(ref container, UITime, "", time, 100, "2 2", new UI4(0, 0, 1, 1));
            foreach (var raceDriver in drivers)            
                raceDriver.AddUI(container, UITime);            
        }

        private void UpdateLapCounter(RaceDriver raceDriver, bool isLaps, RaceDriver spectator)
        {
            ConfigData.UIOptions.UICounter opt = configData.UI.Counter;
            if (opt.Enabled)
            {
                if (spectator == null)
                    spectator = raceDriver;

                CuiElementContainer container = UI.ElementContainer(UILaps, UI.Color(opt.Color1, opt.Color1A), new UI4(opt.Xmin, opt.YMin, opt.XMax, opt.YMax), false);
                UI.Label(ref container, UILaps, isLaps ? string.Format(msg("ui.lap", spectator.Player.UserIDString), opt.Color2, raceDriver.LapNumber, manager.Laps) : string.Format(msg("ui.checkpoint", spectator.Player.UserIDString), opt.Color2, raceDriver.NextCheckpoint - 1, manager.CheckpointCount), 12, new UI4(0.03f, 0, 1, 1), TextAnchor.MiddleLeft, true);

                spectator.AddUI(container, UILaps);
            }
        }

        private void UpdateLapTime(RaceDriver raceDriver, double currentTime, RaceDriver spectator)
        {
            ConfigData.UIOptions.UICounter opt = configData.UI.Times;
            if (opt.Enabled)
            {
                if (spectator == null)
                    spectator = raceDriver;

                CuiElementContainer container = UI.ElementContainer(UILapTime, UI.Color(opt.Color1, opt.Color1A), new UI4(opt.Xmin, opt.YMin, opt.XMax, opt.YMax), false);
                UI.Label(ref container, UILapTime, string.Format(msg("ui.laptime", spectator.Player.UserIDString), opt.Color2, FormatTime(currentTime)), 12, new UI4(0.03f, 0, 1, 1), TextAnchor.MiddleLeft, true);

                spectator.AddUI(container, UILapTime, 5f);
            }
        }
        #endregion        

        #region UI Scoreboard
        private void CreateScoreboard(string trackName, RaceManager.RaceScores[] raceScores)
        {
            if (raceScores == null || raceScores.Length == 0)
                return;

            scoreContainer = UI.ElementContainer(UIScoreboard, UI.Color("#2b2b2b", 1f), new UI4(0, 0, 1, 1), true);

            UI.Label(ref scoreContainer, UIScoreboard, trackName, 30, new UI4(0.3f, 0.85f, 0.7f, 0.95f));
            UI.Button(ref scoreContainer, UIScoreboard, UI.Color("#393939", 1f), msg("close"), 15, new UI4(0.01f, 0.95f, 0.1f, 0.99f), "trackuileave");

            UI.Panel(ref scoreContainer, UIScoreboard, UI.Color("#d85540", 1f), new UI4(0.2f, 0.8f, 0.23f, 0.84f));

            UI.Panel(ref scoreContainer, UIScoreboard, UI.Color("#393939", 1f), new UI4(0.233f, 0.8f, 0.527f, 0.84f));
            UI.Label(ref scoreContainer, UIScoreboard, msg("player"), 15, new UI4(0.243f, 0.8f, 0.517f, 0.84f), TextAnchor.MiddleLeft);

            UI.Panel(ref scoreContainer, UIScoreboard, UI.Color("#393939", 1f), new UI4(0.53f, 0.8f, 0.66f, 0.84f));
            UI.Label(ref scoreContainer, UIScoreboard, msg("distance"), 15, new UI4(0.53f, 0.8f, 0.66f, 0.84f));

            UI.Panel(ref scoreContainer, UIScoreboard, UI.Color("#393939", 1f), new UI4(0.663f, 0.8f, 0.8f, 0.84f));
            UI.Label(ref scoreContainer, UIScoreboard, msg("time"), 15, new UI4(0.663f, 0.8f, 0.8f, 0.84f));

            UI.Panel(ref scoreContainer, UIScoreboard, UI.Color("#d85540", 1f), new UI4(0.2f, 0.7955f, 0.8f, 0.7995f));

            for (int i = 0; i < raceScores.Take(16).Count(); i++)
            {
                if (raceScores[i] == null)
                    continue;

                float yMin = 0.8f - ((i + 1) * 0.045f);
                float yMax = yMin + 0.04f;
                UI.Panel(ref scoreContainer, UIScoreboard, UI.Color("#545554", 1f), new UI4(0.2f, yMin, 0.23f, yMax));
                UI.Label(ref scoreContainer, UIScoreboard, (i + 1).ToString(), 15, new UI4(0.2f, yMin, 0.23f, yMax));

                UI.Panel(ref scoreContainer, UIScoreboard, UI.Color("#545554", 1f), new UI4(0.233f, yMin, 0.527f, yMax));
                UI.Label(ref scoreContainer, UIScoreboard, $"{StripTags(raceScores[i].displayName)}", 15, new UI4(0.243f, yMin, 0.517f, yMax), TextAnchor.MiddleLeft);

                UI.Panel(ref scoreContainer, UIScoreboard, UI.Color("#545554", 1f), new UI4(0.53f, yMin, 0.66f, yMax));
                UI.Label(ref scoreContainer, UIScoreboard, $"{Math.Round(raceScores[i].distance, 1)} M", 15, new UI4(0.54f, yMin, 0.65f, yMax), TextAnchor.MiddleRight);

                UI.Panel(ref scoreContainer, UIScoreboard, UI.Color("#545554", 1f), new UI4(0.663f, yMin, 0.8f, yMax));
                UI.Label(ref scoreContainer, UIScoreboard, !raceScores[i].hasFinished ? "DNF" : FormatTime(raceScores[i].raceTime), 15, new UI4(0.663f, yMin, 0.79f, yMax), TextAnchor.MiddleRight);
            }
        }

        private string StripTags(string str)
        {
            if (str.StartsWith("[") && str.Contains("]") && str.Length > str.IndexOf("]"))
                str = str.Substring(str.IndexOf("]") + 1).Trim();

            if (str.StartsWith("[") && str.Contains("]") && str.Length > str.IndexOf("]"))
                StripTags(str);

            return str;
        }

        [ConsoleCommand("trackuileave")]
        void ccmdLeaveEvent(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            RaceDriver raceDriver = player.GetComponent<RaceDriver>();
            if (raceDriver != null)
            {
                if (manager != null && (manager.Status == EventStatus.Open || manager.Status == EventStatus.Started))
                {
                    manager.LeaveEvent(player, false);
                    return;
                }
                raceDriver.DestroyAllUI();
                UnityEngine.Object.Destroy(raceDriver);
            }
            else CuiHelper.DestroyUi(player, UIScoreboard);
        }
        #endregion

        #region Commands  
        [ChatCommand("reset")]
        void cmdReset(BasePlayer player, string command, string[] args)
        {
            if (manager == null)
                return;

            if (manager.Status != EventStatus.Started)
                return;

            RaceDriver raceDriver = player.GetComponent<RaceDriver>();
            if (raceDriver != null)
            {
                if (raceDriver.Vehicle != null)
                    raceDriver.Vehicle.ResetVehicle();
            }
        }

        [ChatCommand("race")]
        void cmdRace(BasePlayer player, string command, string[] args)
        {
            bool isAdmin = permission.UserHasPermission(player.UserIDString, "racetrack.admin");

            if (args.Length == 0)
            {                
                string message = $"<color=#ce422b>RaceTrack</color><color=#D3D3D3> v</color><color=#ce422b>{Version}</color>"
                        + "\n<color=#ce422b>/race join</color><color=#D3D3D3> - Join an open race event</color>"
                        + "\n<color=#ce422b>/race leave</color><color=#D3D3D3> - Leave the event</color>";
                
                if (isAdmin)
                {
                    message += "\n<color=#ce422b>/race open</color><color=#D3D3D3> - Open a race event</color>"
                        + "\n<color=#ce422b>/race start</color><color=#D3D3D3> - Start a race event</color>"
                        + "\n<color=#ce422b>/race stop</color><color=#D3D3D3> - Stop the current event</color>"
                        + "\n<color=#ce422b>/race select <trackname></color><color=#D3D3D3> - Set the next race track</color>";                    
                }

                SendReply(player, message);
                return;
            }

            if (manager == null)
                manager = new GameObject().AddComponent<RaceManager>();

            switch (args[0].ToLower())
            {
                case "join":
                    if (!permission.UserHasPermission(player.UserIDString, "racetrack.play"))
                    {
                        SendReply(player, msg("nopermission", player.UserIDString));
                        return;
                    }
                    if (configData.Entrance.Enabled)
                    {
                        if (configData.Entrance.Economics)
                        {
                            double amount = (double)Economics?.Call("Balance", player.UserIDString);
                            if (amount < configData.Entrance.Amount || !(bool)Economics?.Call("Withdraw", player.UserIDString, (double)configData.Entrance.Amount))
                            {
                                SendReply(player, string.Format(msg("eventfee", player.UserIDString), configData.Entrance.Amount, msg("economics", player.UserIDString)));
                                return;
                            }                            
                        }
                        if (configData.Entrance.ServerRewards)
                        {
                            int amount = (int)ServerRewards?.Call("CheckPoints", player.userID);
                            if (amount < configData.Entrance.Amount || !(bool)ServerRewards?.Call("TakePoints", player.userID, configData.Entrance.Amount))
                            {
                                SendReply(player, string.Format(msg("eventfee", player.UserIDString), configData.Entrance.Amount, msg("serverrewards", player.UserIDString)));
                                return;
                            }
                        }
                        if (configData.Entrance.Scrap)
                        {
                            int amount = player.inventory.GetAmount(-932201673);
                            if (amount <= configData.Entrance.Amount)
                            {
                                player.inventory.Take(null, -932201673, configData.Entrance.Amount);
                                SendReply(player, string.Format(msg("eventfee", player.UserIDString), configData.Entrance.Amount, msg("scrap", player.UserIDString)));
                                return;
                            }
                        }
                    }
                    manager.JoinEvent(player);
                    return;
                case "leave":
                    manager.LeaveEvent(player, false);
                    return;
                case "open":
                    if (!isAdmin)
                        return;

                    if (manager.Status != EventStatus.Finished)
                    {
                        SendReply(player, string.Format("<color=#D3D3D3>There is already an event {0}</color>", manager.Status));
                        return;
                    }
                   
                    if (!manager.HasTrackSet)
                    {
                        SendReply(player, "<color=#D3D3D3>You need to set a track before opening an event</color>");
                        return;
                    }

                    if (manager.TrackData.raceType == RaceType.Car)
                    {
                        if (manager.TrackData.modularCarProfiles != null && manager.TrackData.modularCarProfiles.Count > 0)
                        {
                            for (int i = 0; i < manager.TrackData.modularCarProfiles.Count; i++)
                            {
                                if (!carData.ContainsKey(manager.TrackData.modularCarProfiles[i]))
                                {
                                    SendReply(player, $"<color=#D3D3D3>The selected track is setup to use a modular vehicle profile that does not exist. You must either remove the profile from your track data, or create a new profile with the same name ({manager.TrackData.modularCarProfiles[i]}). Unable to open race!</color>");
                                    return;
                                }
                            }                            
                        }
                    }

                    manager.OpenEvent();
                    SendReply(player, "<color=#D3D3D3>Creating checkpoints, please wait!</color>");
                    return;
                case "start":
                    if (!isAdmin)
                        return;

                    if (manager.Status != EventStatus.Open)
                    {
                        SendReply(player, "<color=#D3D3D3>You must open the event before starting it</color>");
                        return;
                    }

                    if (manager.JoinerCount < 1)
                    {
                        SendReply(player, "<color=#D3D3D3>You can not start the event if there are no players</color>");
                        return;
                    }

                    manager.StartEvent();
                    return;
                case "stop":
                    if (!isAdmin)
                        return;
                    if (manager.Status == EventStatus.Loading)
                    {
                        SendReply(player, "<color=#D3D3D3>You must wait until the event has finished loading</color>");
                        return;
                    }
                    if (manager.Status == EventStatus.Finishing)
                    {
                        SendReply(player, "<color=#D3D3D3>The last event is finishing up</color>");
                        return;
                    }
                    if (manager.Status == EventStatus.Finished)
                    {
                        SendReply(player, "<color=#D3D3D3>There is no event in progress</color>");
                        return;
                    }
                    else manager.EndEvent();
                    return;
                case "select":
                    if (!isAdmin)
                        return;

                    if (args.Length != 2)
                    {
                        SendReply(player, "<color=#D3D3D3>You must enter a track name</color>");
                        return;
                    }

                    if (!raceTracks.ContainsKey(args[1]))
                    {
                        SendReply(player, string.Format("<color=#D3D3D3>Unable to find a track with the name {0}</color>", args[1]));
                        return;
                    }

                    if (manager.Status != EventStatus.Finished)
                    {
                        SendReply(player, manager.Status.ToString());
                        SendReply(player, "<color=#D3D3D3>You can not set the track when an event is open or in progress</color>");
                        return;
                    }

                    manager.SetTrackData(args[1], raceTracks[args[1]]);
                    SendReply(player, string.Format("<color=#D3D3D3>You have set the track to {0}</color>", args[1]));
                    return;

                default:
                    SendReply(player, "<color=#D3D3D3>Invalid Syntax. Type \"/race\" for help</color>");
                    return;
            }
        }

        //[ChatCommand("tr")]
        //void cmdtetRace(BasePlayer player, string command, string[] args)
        //{
        //    if (manager == null)
        //        manager = new GameObject().AddComponent<RaceManager>();

        //    manager.SetTrackData("car2", raceTracks["car2"]);

        //    manager.OpenEvent();

        //    timer.In(5f, () =>
        //    {
        //        manager.JoinEvent(player);

        //        //for (int i = 0; i < 3; i++)
        //        //{
        //        //    BasePlayer npc = GameManager.server.CreateEntity(player.PrefabName, player.transform.position, player.transform.rotation) as BasePlayer;
        //        //    npc.enableSaving = false;
        //        //    npc.displayName = $"Bot {i}";
        //        //    npc.userID = player.userID + (ulong)UnityEngine.Random.Range(1000, 9000);
        //        //    npc.UserIDString = npc.userID.ToString();
        //        //    npc.Spawn();

        //        //    manager.JoinEvent(npc);

        //        //    timer.In(2f, npc.EndSleeping);
        //        //}

        //        manager.StartEvent();
        //    });
        //}

        [ChatCommand("track")]
        void cmdTrack(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "racetrack.admin"))
                return;

            if (args.Length == 0)
            {
                string message = $"<color=#ce422b>RaceTrack</color><color=#D3D3D3> v</color><color=#ce422b>{Version}</color><color=#D3D3D3> - Track Creator</color>"
                        + "\n<color=#ce422b>/track new</color><color=#D3D3D3> - Begin creating a new track</color>"
                        + "\n<color=#ce422b>/track cancel</color><color=#D3D3D3> - Cancel current track creation</color>"
                        + "\n<color=#ce422b>/track players <number></color><color=#D3D3D3> - Set the minimum players</color>"
                        + "\n<color=#ce422b>/track mode <laps/sprint></color><color=#D3D3D3> - Set the race mode</color>"
                        + "\n<color=#ce422b>/track type <car/boat/rhib/minicopter/horse/kayak/boogieboard/innertube></color><color=#D3D3D3> - Set the race type</color>"
                        + "\n<color=#ce422b>/track laps <number></color><color=#D3D3D3> - Set the amount of laps</color>"
                        + "\n<color=#ce422b>/track time <amount></color><color=#D3D3D3> - Set a time limit to how long the race can last (in seconds)</color>"
                        + "\n<color=#ce422b>/track save <name></color><color=#D3D3D3> - Save the current track</color>"
                        + "\n<color=#ce422b>/track edit <name></color><color=#D3D3D3> - Edit an existing track</color>"
                        + "\n<color=#ce422b>/track list</color><color=#D3D3D3> - List all race tracks</color>"
                        + "\n<color=#ce422b>/track delete <name></color><color=#D3D3D3> - Delete the track with the specified name</color>";

                string message2 = "<color=#ce422b>/track vehicleprofile add <profile name></color><color=#D3D3D3> - Adds the specified vehicle profile to the track</color>"
                        + "\n<color=#ce422b>/track vehicleprofile remove <profile name></color><color=#D3D3D3> - Removes the specified vehicle profile from the track</color>"
                        + "\n<color=#ce422b>/track vehicleprofile list</color><color=#D3D3D3> - Lists vehicle profiles associated with the track</color>"
                        + "\n<color=#ce422b>/track breed <0-9></color><color=#D3D3D3> - Set the horse breed, breed ID's are from 0 - 9</color>";

                string message3 = "<color=#ce422b>/track gp add</color><color=#D3D3D3> - Add a track grid point</color>"
                        + "\n<color=#ce422b>/track gp remove <number></color><color=#D3D3D3> - Remove a track grid point</color>"
                        + "\n<color=#ce422b>/track gp move <number></color><color=#D3D3D3> - Move a track grid point to your position</color>"
                        + "\n<color=#ce422b>/track gp show</color><color=#D3D3D3> - Show all track grid points</color>"
                        + "\n<color=#ce422b>/track cp add <radius></color><color=#D3D3D3> - Add a track checkpoint</color>"
                        + "\n<color=#ce422b>/track cp remove <number></color><color=#D3D3D3> - Remove a track checkpoint</color>"
                        + "\n<color=#ce422b>/track cp move <number></color><color=#D3D3D3> - Move a track check point to your position</color>"
                        + "\n<color=#ce422b>/track cp size <number> <radius></color><color=#D3D3D3> - Adjust the size of the specified track checkpoint</color>";

                string message4 = "<color=#ce422b>/track cp show</color><color=#D3D3D3> - Show all track checkpoints</color>"
                        + "<color=#ce422b>/track commander <true/false></color><color=#D3D3D3> - Use Boat/Car/HeliCommander for this race</color>";

                SendReply(player, message);
                SendReply(player, message2);
                SendReply(player, message3);
                SendReply(player, message4);
                return;
            }

            switch (args[0].ToLower())
            {
                case "new":
                    trackCreator[player.userID] = new TrackData();
                    SendReply(player, "<color=#D3D3D3>You are now creating a new race track</color>");
                    return;
                case "list":
                    string tracks = "<color=#ce422b>Race Tracks:</color>";
                    foreach (var track in raceTracks)
                        tracks += $"\n<color=#D3D3D3>{track.Key}</color>";
                    SendReply(player, tracks);
                    return;
                case "edit":
                    if (args.Length < 2)
                    {
                        SendReply(player, "<color=#D3D3D3>You must specify a track name</color>");
                        return;
                    }

                    if (!raceTracks.ContainsKey(args[1]))
                    {
                        SendReply(player, "<color=#D3D3D3>Unable to find a track with that name</color>");
                        return;
                    }

                    trackCreator[player.userID] = raceTracks[args[1]];
                    SendReply(player, string.Format("<color=#D3D3D3>You are now editing the race track named {0}</color>", args[1]));
                    return;
                case "delete":
                    if (args.Length < 2)
                    {
                        SendReply(player, "<color=#D3D3D3>You must specify a track name</color>");
                        return;
                    }

                    if (!raceTracks.ContainsKey(args[1]))
                    {
                        SendReply(player, "<color=#D3D3D3>Unable to find a track with that name</color>");
                        return;
                    }

                    if (manager != null && manager.TrackName == args[1])
                    {
                        SendReply(player, "<color=#D3D3D3>You can't delete a track when it is in use</color>");
                        return;
                    }

                    raceTracks.Remove(args[1]);
                    SaveTrackData();
                    SendReply(player, string.Format("<color=#D3D3D3>You have deleted the track named {0}</color>", args[1]));
                    return;
                default:
                    break;
            }           

            if (!trackCreator.ContainsKey(player.userID))
            {
                SendReply(player, "<color=#D3D3D3>You must either create a new track, or edit an existing one</color>");
                return;
            }

            switch (args[0].ToLower())
            {               
                case "cancel":
                    if (trackCreator.ContainsKey(player.userID))
                    {
                        trackCreator.Remove(player.userID);
                        SendReply(player, "<color=#D3D3D3>You have cancelled track creation</color>");
                    }
                    else SendReply(player, "<color=#D3D3D3>You are not currently creating a race track</color>");
                    return;
                case "save":
                    if (trackCreator.ContainsKey(player.userID))
                    {
                        if (args.Length < 2)
                        {
                            SendReply(player, "<color=#D3D3D3>You must specify a track name</color>");
                            return;
                        }

                        if(trackCreator[player.userID].gridPoints.Count == 0)
                        {
                            SendReply(player, "<color=#D3D3D3>You must create grid points for racers to start on</color>");
                            return;
                        }

                        if (trackCreator[player.userID].checkPoints.Count < 2)
                        {
                            SendReply(player, "<color=#D3D3D3>You must create atleast 2 check points for racers to follow</color>");
                            return;
                        }

                        if (trackCreator[player.userID].laps == 0 && trackCreator[player.userID].raceMode == RaceMode.Laps)
                        {
                            SendReply(player, "<color=#D3D3D3>You must specify the amount of laps in this race</color>");
                            return;
                        }

                        if (trackCreator[player.userID].minPlayers == 0)
                        {
                            SendReply(player, "<color=#D3D3D3>You must specify the minimum amount of players for this race</color>");
                            return;
                        }

                        float totalDistance = 0;
                        for (int i = 1; i < trackCreator[player.userID].checkPoints.Count; i++)                        
                            totalDistance += Vector3.Distance(trackCreator[player.userID].checkPoints[i - 1].position, trackCreator[player.userID].checkPoints[i].position);                        

                        trackCreator[player.userID].totalDistance = totalDistance;
                                                
                        raceTracks[string.Join(" ", args).Substring(5)] = trackCreator[player.userID];
                        SaveTrackData();
                        trackCreator.Remove(player.userID);
                        SendReply(player, string.Format("<color=#D3D3D3>You have successfully saved a race track named {0}</color>", string.Join(" ", args).Substring(5)));
                    }
                    else SendReply(player, "<color=#D3D3D3>You are not currently creating a race track</color>");
                    return;                
                case "laps":
                    if (args.Length < 2)
                    {
                        SendReply(player, "<color=#D3D3D3>You must enter a number of laps</color>");
                        return;
                    }

                    int laps = 0;
                    if (!int.TryParse(args[1], out laps))
                    {
                        SendReply(player, "<color=#D3D3D3>You must enter a number of laps</color>");
                        return;
                    }

                    trackCreator[player.userID].laps = laps;
                    SendReply(player, string.Format("<color=#D3D3D3>You have set the number of laps to {0}</color>", laps));
                    return;
                case "time":
                    if (args.Length < 2)
                    {
                        SendReply(player, "<color=#D3D3D3>You must enter a amount of time in seconds</color>");
                        return;
                    }

                    int time = 0;
                    if (!int.TryParse(args[1], out time))
                    {
                        SendReply(player, "<color=#D3D3D3>You must enter a amount of time in seconds</color>");
                        return;
                    }

                    trackCreator[player.userID].maxTime = time;
                    SendReply(player, string.Format("<color=#D3D3D3>You have set the race time limit to {0} seconds</color>", time));
                    return;
                case "players":
                    if (args.Length < 2)
                    {
                        SendReply(player, "<color=#D3D3D3>You must enter a number</color>");
                        return;
                    }

                    int minPlayers = 0;
                    if (!int.TryParse(args[1], out minPlayers))
                    {
                        SendReply(player, "<color=#D3D3D3>You must enter a number</color>");
                        return;
                    }

                    trackCreator[player.userID].minPlayers = minPlayers;
                    SendReply(player, string.Format("<color=#D3D3D3>You have set the minimum players to {0}</color>", minPlayers));
                    return;
                case "mode":
                    if (args.Length < 2)
                    {
                        SendReply(player, "<color=#D3D3D3>You must enter a race mode (laps or sprint)</color>");
                        return;
                    }

                    trackCreator[player.userID].raceMode = ParseType<RaceMode>(args[1]);
                    SendReply(player, string.Format("<color=#D3D3D3>You have set the race mode to {0}</color>", trackCreator[player.userID].raceMode));
                    return;
                case "type":
                    if (args.Length < 2)
                    {
                        SendReply(player, "<color=#D3D3D3>You must enter a type of race (car/boat/rhib/minicopter/horse/kayak/boogieboard/innertube)</color>");
                        return;
                    }

                    trackCreator[player.userID].raceType = ParseType<RaceType>(args[1]);
                    SendReply(player, string.Format("<color=#D3D3D3>You have set the race type to {0}</color>", trackCreator[player.userID].raceType));
                    return;
                case "vehicleprofile":
                    if (args.Length < 2)
                    {
                        SendReply(player, "<color=#D3D3D3>Invalid syntax!</color>");
                        return;
                    }

                    switch (args[1].ToLower())
                    {
                        case "add":
                            {
                                if (args.Length < 3)
                                {
                                    SendReply(player, "<color=#D3D3D3>You must enter vehicle profile name</color>");
                                    return;
                                }

                                if (!carData.ContainsKey(args[2]))
                                {
                                    SendReply(player, "<color=#D3D3D3>The vehicle profile name you have entered does not exist</color>");
                                    return;
                                }

                                if (trackCreator[player.userID].modularCarProfiles == null)
                                    trackCreator[player.userID].modularCarProfiles = new List<string>();

                                trackCreator[player.userID].modularCarProfiles.Add(args[2]);
                                SendReply(player, string.Format("<color=#D3D3D3>You have added the vehicle profile {0} to the track</color>", args[2]));
                            }
                            return;
                        case "remove":
                            {
                                if (args.Length < 3)
                                {
                                    SendReply(player, "<color=#D3D3D3>You must enter vehicle profile name</color>");
                                    return;
                                }

                                if (trackCreator[player.userID].modularCarProfiles == null)
                                {
                                    SendReply(player, "<color=#D3D3D3>The track does not have any vehicle profiles assigned</color>");
                                    return;
                                }

                                if (!trackCreator[player.userID].modularCarProfiles.Contains(args[2]))
                                {
                                    SendReply(player, "<color=#D3D3D3>The track does not contain the specified vehicle profile</color>");
                                    return;
                                }
                                                                
                                trackCreator[player.userID].modularCarProfiles.Remove(args[2]);
                                if (trackCreator[player.userID].modularCarProfiles.Count == 0)
                                    trackCreator[player.userID].modularCarProfiles = null;

                                SendReply(player, string.Format("<color=#D3D3D3>You have removed the vehicle profile {0} from the track</color>", args[2]));
                            }
                            return;
                        case "list":
                            {
                                if (trackCreator[player.userID].modularCarProfiles == null)
                                {
                                    SendReply(player, "<color=#D3D3D3>The track does not have any vehicle profiles assigned</color>");
                                    return;
                                }

                                SendReply(player, $"<color=#D3D3D3>Vehicle profiles currently assigned;</color> {trackCreator[player.userID].modularCarProfiles.ToSentence()}");
                            }
                            return;
                        default:
                            break;
                    }
                    
                    return;
                case "breed":
                    if (args.Length < 2)
                    {
                        SendReply(player, "<color=#D3D3D3>You must enter a breed ID (from 0 - 9)</color>");
                        return;
                    }

                    int breed = 0;
                    if (!int.TryParse(args[1], out breed))
                    {
                        SendReply(player, "<color=#D3D3D3>You must enter a breed ID (from 0 - 9)</color>");
                        return;
                    }

                    breed = Mathf.Clamp(breed, 0, 9);

                    trackCreator[player.userID].horseBreed = breed;
                    SendReply(player, string.Format("<color=#D3D3D3>You have set the horse breed to {0}</color>", breed));
                    return;
                case "commander":                    
                    bool useCommander;
                    if (args.Length < 2 || !bool.TryParse(args[1], out useCommander))
                    {
                        SendReply(player, "<color=#D3D3D3>You must enter either true or false</color>");
                        return;
                    }
                    trackCreator[player.userID].useCommander = useCommander;
                    SendReply(player, string.Format("<color=#D3D3D3>You have set this race to {0} Boat/Car/HeliCommander</color>", useCommander ? "use" : "not use"));
                    return;
                case "cp":
                    if (args.Length < 2)
                    {
                        SendReply(player, "<color=#D3D3D3>Invalid Syntax. Type \"/track\" for help</color>");
                        return;
                    }

                    switch (args[1].ToLower())
                    {
                        case "add":
                            if (args.Length < 3)
                            {
                                SendReply(player, "<color=#D3D3D3>You must enter a checkpoint diameter</color>");
                                return;
                            }

                            float size = 0f;
                            if (!float.TryParse(args[2], out size))
                            {
                                SendReply(player, "<color=#D3D3D3>You must enter a checkpoint diameter</color>");
                                return;
                            }

                            trackCreator[player.userID].checkPoints.Add(new TrackData.PointInfo(player.transform.position, player?.eyes?.rotation.eulerAngles.y ?? 0, size));
                            ShowPoint(player, player.transform.position, $"Checkpoint {trackCreator[player.userID].checkPoints.Count}", size);
                            SendReply(player, string.Format("<color=#D3D3D3>You have added checkpoint number {0}</color>", trackCreator[player.userID].checkPoints.Count));
                            return;
                        case "remove":
                            if (args.Length < 3)
                            {
                                SendReply(player, "<color=#D3D3D3>You must enter a check point number</color>");
                                return;
                            }
                            int number = 0;
                            if (!int.TryParse(args[2], out number))
                            {
                                SendReply(player, "<color=#D3D3D3>You must enter a check point number</color>");
                                return;
                            }

                            if (number > trackCreator[player.userID].checkPoints.Count)
                            {
                                SendReply(player, string.Format("<color=#D3D3D3>This track only has {0} check points</color>", trackCreator[player.userID].checkPoints.Count));
                                return;
                            }

                            trackCreator[player.userID].checkPoints.RemoveAt(number - 1);
                            SendReply(player, string.Format("<color=#D3D3D3>You have successfully removed check point #{0}</color>", number));
                            return;
                        case "move":
                            if (args.Length < 3)
                            {
                                SendReply(player, "<color=#D3D3D3>You must enter a check point number</color>");
                                return;
                            }
                            int moveNumber = 0;
                            if (!int.TryParse(args[2], out moveNumber))
                            {
                                SendReply(player, "<color=#D3D3D3>You must enter a check point number</color>");
                                return;
                            }

                            if (moveNumber > trackCreator[player.userID].checkPoints.Count)
                            {
                                SendReply(player, string.Format("<color=#D3D3D3>This track only has {0} check points</color>", trackCreator[player.userID].checkPoints.Count));
                                return;
                            }

                            trackCreator[player.userID].checkPoints[moveNumber - 1] = new TrackData.PointInfo(player.transform.position, player?.eyes?.rotation.eulerAngles.y ?? 0, trackCreator[player.userID].checkPoints[moveNumber - 1].size);
                            ShowPoint(player, player.transform.position, $"Checkpoint {moveNumber}", trackCreator[player.userID].checkPoints[moveNumber - 1].size, player?.eyes?.rotation.eulerAngles.y ?? 0);
                            SendReply(player, string.Format("<color=#D3D3D3>You have successfully moved grid point #{0}</color>", moveNumber));
                            return;
                        case "size":
                            if (args.Length < 4)
                            {
                                SendReply(player, "<color=#D3D3D3>You must enter a check point number and new radius</color>");
                                return;
                            }

                            int sizeNumber = 0;
                            if (!int.TryParse(args[2], out sizeNumber))
                            {
                                SendReply(player, "<color=#D3D3D3>You must enter a check point number</color>");
                                return;
                            }

                            float newSize = 0;
                            if (!float.TryParse(args[3], out newSize))
                            {
                                SendReply(player, "<color=#D3D3D3>You must enter a checkpoint diameter</color>");
                                return;
                            }

                            if (sizeNumber > trackCreator[player.userID].checkPoints.Count)
                            {
                                SendReply(player, string.Format("<color=#D3D3D3>This track only has {0} check points</color>", trackCreator[player.userID].checkPoints.Count));
                                return;
                            }

                            trackCreator[player.userID].checkPoints[sizeNumber - 1].size = newSize;

                            ShowPoint(player, trackCreator[player.userID].checkPoints[sizeNumber - 1].position, $"Checkpoint {sizeNumber - 1}", newSize, trackCreator[player.userID].checkPoints[sizeNumber - 1].rotation);
                            SendReply(player, string.Format("<color=#D3D3D3>You have successfully changed the size of grid point #{0}</color>", sizeNumber));
                            return;
                        case "show":
                            float displayTime = 10;
                            if (args.Length > 2)
                                float.TryParse(args[2], out displayTime);

                            int i = 1;
                            foreach (var checkPoint in trackCreator[player.userID].checkPoints)
                            {
                                ShowPoint(player, checkPoint.position + Vector3.up, $"Checkpoint {i}", checkPoint.size, checkPoint.rotation, displayTime);
                                i++;
                            }
                            return;
                    }
                    return;
                case "gp":
                    if (args.Length < 2)
                    {
                        SendReply(player, "<color=#D3D3D3>Invalid Syntax. Type \"/track\" for help</color>");
                        return;
                    }

                    switch (args[1].ToLower())
                    {
                        case "add":
                            trackCreator[player.userID].gridPoints.Add(new TrackData.PointInfo(player.transform.position, player?.eyes?.rotation.eulerAngles.y ?? 0));
                            ShowPoint(player, player.transform.position, $"Gridpoint {trackCreator[player.userID].gridPoints.Count}", 0, player?.eyes?.rotation.eulerAngles.y ?? 0);
                            SendReply(player, string.Format("<color=#D3D3D3>You have added gridpoint number {0}</color>", trackCreator[player.userID].gridPoints.Count));
                            return;
                        case "remove":
                            if (args.Length < 3)
                            {
                                SendReply(player, "<color=#D3D3D3>You must enter a grid point number</color>");
                                return;
                            }
                            int number = 0;
                            if (!int.TryParse(args[2], out number))
                            {
                                SendReply(player, "<color=#D3D3D3>You must enter a grid point number</color>");
                                return;
                            }

                            if (number > trackCreator[player.userID].gridPoints.Count)
                            {
                                SendReply(player, string.Format("<color=#D3D3D3>This track only has {0} grid points</color>", trackCreator[player.userID].gridPoints.Count));
                                return;
                            }

                            trackCreator[player.userID].gridPoints.RemoveAt(number - 1);
                            SendReply(player, string.Format("<color=#D3D3D3>You have successfully removed grid point #{0}</color>", number));
                            return;
                        case "move":
                            if (args.Length < 3)
                            {
                                SendReply(player, "<color=#D3D3D3>You must enter a grid point number</color>");
                                return;
                            }
                            int moveNumber = 0;
                            if (!int.TryParse(args[2], out moveNumber))
                            {
                                SendReply(player, "<color=#D3D3D3>You must enter a grid point number</color>");
                                return;
                            }

                            if (moveNumber > trackCreator[player.userID].gridPoints.Count)
                            {
                                SendReply(player, string.Format("<color=#D3D3D3>This track only has {0} grid points</color>", trackCreator[player.userID].gridPoints.Count));
                                return;
                            }

                            trackCreator[player.userID].gridPoints[moveNumber - 1] = new TrackData.PointInfo(player.transform.position, player?.eyes?.rotation.eulerAngles.y ?? 0);
                            ShowPoint(player, player.transform.position, $"Gridpoint {moveNumber}", 0, player?.eyes?.rotation.eulerAngles.y ?? 0);
                            SendReply(player, string.Format("<color=#D3D3D3>You have successfully moved grid point #{0}</color>", moveNumber));
                            return;
                        case "show": 
                            float displayTime = 10;
                            if (args.Length > 2)
                                float.TryParse(args[2], out displayTime);

                            int i = 1;
                            foreach (var gridPoint in trackCreator[player.userID].gridPoints)
                            {
                                ShowPoint(player, gridPoint.position + Vector3.up, $"Gridpoint {i}", 0, gridPoint.rotation, displayTime);
                                i++;
                            }
                            return;
                    }
                    return;
                default:
                    SendReply(player, "<color=#D3D3D3>Invalid Syntax. Type \"/track\" for help</color>");
                    return;
            }
        }

        [ChatCommand("racescores")]
        void cmdRaceScores(BasePlayer player, string command, string[] args)
        {
            if (scoreContainer == null)
            {
                SendReply(player, msg("scoreboard.nonesaved", player.UserIDString));
                return;
            }

            CuiHelper.AddUi(player, scoreContainer);
        }

        [ChatCommand("racevehicle")]
        void cmdSaveCar(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "racetrack.admin"))
                return;

            if (args.Length == 0)
            {
                SendReply(player, "<color=#ce422b>/racevehicle list</color> - Lists all vehicle profiles currently available");
                SendReply(player, "<color=#ce422b>/racevehicle remove \"profile name\"</color> - Lists all vehicle profiles currently available");
                SendReply(player, "<color=#ce422b>/racevehicle save \"profile name\" <tier></color> - Saves the modular car you are looking at to use in races. Tier is the quality of components that will be placed in the vehicle");
                return;
            }

            switch (args[0].ToLower())
            {
                case "list":
                    if (carData.Count == 0)
                        SendReply(player, "<color=#D3D3D3>No vehicles currently saved!</color>");
                    else SendReply(player, $"<color=#D3D3D3>Vehicles:</color> {carData.Select(x => x.Key).ToSentence()}");
                    return;
                case "remove":
                    if (args.Length == 2)
                    {
                        if (carData.ContainsKey(args[1]))
                        {
                            carData.Remove(args[1]);
                            SaveCarData();
                            SendReply(player, $"<color=#D3D3D3>You have removed the vehicle profile with the name <color=#ce422b>{args[1]}</color></color>");
                        }
                        else SendReply(player, $"<color=#D3D3D3>There is no vehicle profile with the name <color=#ce422b>{args[1]}</color></color>");
                    }
                    else goto default;
                    return;
                case "save":
                    if (args.Length == 3)
                    {
                        ModularCar modularCar = FindVehicle(player);
                        if (modularCar == null)
                        {
                            SendReply(player, "<color=#D3D3D3>No modular vehicle found!</color>");
                            return;
                        }

                        int tier;
                        if (!int.TryParse(args[2], out tier))
                        {
                            SendReply(player, "<color=#D3D3D3>Invalid tier entered! It must be a number between <color=#ce422b>1 and 3</color></color>");
                            return;
                        }

                        carData[args[1]] = new ModularCarData(modularCar, Mathf.Clamp(tier, 1, 3));
                        SaveCarData();
                        SendReply(player, $"<color=#D3D3D3>Vehicle successfully saved as <color=#ce422b>{args[1]}</color></color>");
                    }
                    else goto default;
                    return;
                default:
                    SendReply(player, "<color=#D3D3D3>Invalid syntax!</color>");
                    break;
            }
            
        }

        private ModularCar FindVehicle(BasePlayer player)
        {
            Ray ray = new Ray(player.eyes.position, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward);
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 10f))
                return null;

            return hit.collider.GetComponentInParent<ModularCar>();            
        }

        [ConsoleCommand("race")]
        void ccmdRace(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
                return;

            if (arg.Args.Length == 0)
            {
                string message = "\nrace open - Open a race event"
                        + "\nrace start - Start a race event"
                        + "\nrace stop - Stop the current event"
                        + "\nrace select <trackname> - Set the next race track";                

                SendReply(arg, message);
                return;
            }

            if (manager == null)
                manager = new GameObject().AddComponent<RaceManager>();

            switch (arg.Args[0].ToLower())
            {                
                case "open":
                    if (manager.Status != EventStatus.Finished)
                    {
                        SendReply(arg, string.Format("There is already an event {0}", manager.Status));
                        return;
                    }

                    if (!manager.HasTrackSet)
                    {
                        SendReply(arg, "You need to set a track before opening an event");
                        return;
                    }

                    if (manager.TrackData.raceType == RaceType.Car)
                    {
                        if (manager.TrackData.modularCarProfiles != null && manager.TrackData.modularCarProfiles.Count > 0)
                        {
                            for (int i = 0; i < manager.TrackData.modularCarProfiles.Count; i++)
                            {
                                if (!carData.ContainsKey(manager.TrackData.modularCarProfiles[i]))
                                {
                                    SendReply(arg, $"The selected track is setup to use a modular vehicle profile that does not exist. You must either remove the profile from your track data, or create a new profile with the same name ({manager.TrackData.modularCarProfiles[i]}). Unable to open race!");
                                    return;
                                }
                            }
                           
                        }
                    }

                    SendReply(arg, "Creating checkpoints, please wait!");
                    manager.OpenEvent();
                    return;
                case "start":                   
                    if (manager.Status != EventStatus.Open)
                    {
                        SendReply(arg, "You must open the event before starting it");
                        return;
                    }

                    if (manager.JoinerCount < 1)
                    {
                        SendReply(arg, "You can not start the event if there are no players");
                        return;
                    }

                    manager.StartEvent();
                    return;
                case "stop":
                    if (manager.Status == EventStatus.Loading)
                    {
                        SendReply(arg, "You must wait until the event has finished loading");
                        return;
                    }
                    if (manager.Status == EventStatus.Finishing)
                    {
                        SendReply(arg, "The last event is finishing up");
                        return;
                    }
                    if (manager.Status == EventStatus.Finished)
                    {
                        SendReply(arg, "There is no event in progress");
                        return;
                    }
                    else manager.EndEvent();
                    return;
                case "select":
                    if (arg.Args.Length != 2)
                    {
                        SendReply(arg, "You must enter a track name");
                        return;
                    }

                    if (!raceTracks.ContainsKey(arg.Args[1]))
                    {
                        SendReply(arg, string.Format("Unable to find a track with the name {0}", arg.Args[1]));
                        return;
                    }

                    if (manager.Status != EventStatus.Finished)
                    {
                        SendReply(arg, manager.Status.ToString());
                        SendReply(arg, "You can not set the track when an event is open or in progress");
                        return;
                    }

                    manager.SetTrackData(arg.Args[1], raceTracks[arg.Args[1]]);
                    SendReply(arg, string.Format("You have set the track to {0}", arg.Args[1]));
                    return;

                default:
                    SendReply(arg, "Invalid Syntax. Type \"race\" for help");
                    return;
            }
        }

        private void ShowPoint(BasePlayer player, Vector3 point, string text, float radius = 0, float rotation = 0, float time = 10)
        {
            player.SendConsoleCommand("ddraw.text", time, Color.green, point + new Vector3(0, 1.5f, 0), $"<size=40>{text}</size>");
            player.SendConsoleCommand("ddraw.box", time, Color.green, point, 1f);

            if (radius > 0)
                player.SendConsoleCommand("ddraw.sphere", time, Color.blue, point, radius);

            if (rotation != 0)            
                player.SendConsoleCommand("ddraw.arrow", time, Color.blue, point, point + (Quaternion.Euler(0, rotation, 0) * (Vector3.forward * 3)), 1f);            
        }
        #endregion

        #region Config    
        enum LightType { Lantern, Flasher, Siren }
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Event Automation")]
            public EventAutomation Automation { get; set; }

            [JsonProperty(PropertyName = "Event Timers")]
            public EventTimers Timers { get; set; }

            [JsonProperty(PropertyName = "Checkpoint Options")]
            public CheckpointOptions Checkpoints { get; set; }

            [JsonProperty(PropertyName = "Reward Options")]
            public RewardOptions Rewards { get; set; }

            [JsonProperty(PropertyName = "Event Entry Options")]
            public EntryOptions Entrance { get; set; }

            [JsonProperty(PropertyName = "Racer Options")]
            public RacerOptions Racers { get; set; }

            [JsonProperty(PropertyName = "UI Options")]
            public UIOptions UI { get; set; }

            [JsonProperty(PropertyName = "Blacklisted commands for event players")]
            public string[] CommandBlacklist { get; set; }

            [JsonProperty(PropertyName = "Disable spectate mode for finished racers")]
            public bool DisableSpectate { get; set; }
           
            public class EventAutomation
            {
                [JsonProperty(PropertyName = "Enable automated events")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Randomise automated events")]
                public bool Random { get; set; }

                [JsonProperty(PropertyName = "Event order for non-randomised events")]
                public List<string> Order { get; set; }
            }

            public class EventTimers
            {
                [JsonProperty(PropertyName = "Time to close the event if minimum players has not been reached (seconds)")]
                public int CloseTime { get; set; }

                [JsonProperty(PropertyName = "Time to start the event when minimum players reached (seconds)")]
                public int StartTime { get; set; }

                [JsonProperty(PropertyName = "Time to finish racing after the first player has won (seconds)")]
                public int EndTime { get; set; }

                [JsonProperty(PropertyName = "Interval between automated events (seconds)")]
                public int Interval { get; set; }

                [JsonProperty(PropertyName = "Time from when racers are teleported to their race car until the race starts (seconds)")]
                public int Countdown { get; set; }
            }

            public class CheckpointOptions
            {
                [JsonProperty(PropertyName = "Spawn lights around checkpoint markers")]
                public bool Lights { get; set; }

                [JsonProperty(PropertyName = "Light type (Lantern, Flasher, Siren)")]
                public string LightType { get; set; }

                [JsonProperty(PropertyName = "Checkpoint marker block grade (Twigs, Wood, Stone, Metal, TopTier)")]
                public string Grade { get; set; }

                [JsonProperty(PropertyName = "Launch a firework when player crosses the finish line")]
                public bool LaunchFirework { get; set; }

                [JsonProperty(PropertyName = "Firework prefab path")]
                public string FireworkPrefab { get; set; }
            }

            public class EntryOptions
            {
                [JsonProperty(PropertyName = "Enable entrance fee")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Collect entrance fee from ServerRewards money")]
                public bool ServerRewards { get; set; }

                [JsonProperty(PropertyName = "Collect entrance fee from Economics money")]
                public bool Economics { get; set; }

                [JsonProperty(PropertyName = "Collect entrance fee using Scrap")]
                public bool Scrap { get; set; }

                [JsonProperty(PropertyName = "Fee amount")]
                public int Amount { get; set; }
            }

            public class RewardOptions
            {
                [JsonProperty(PropertyName = "Enable reward system")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Give prizes to all podium winners")]
                public bool Podium { get; set; }

                [JsonProperty(PropertyName = "Use ServerRewards as prize money")]
                public bool ServerRewards { get; set; }

                [JsonProperty(PropertyName = "Use Economics as prize money")]
                public bool Economics { get; set; }

                [JsonProperty(PropertyName = "Use Scrap as prize money")]
                public bool Scrap { get; set; }

                [JsonProperty(PropertyName = "Prize money for first place")]
                public int Prize1 { get; set; }

                [JsonProperty(PropertyName = "Prize money for second place")]
                public int Prize2 { get; set; }

                [JsonProperty(PropertyName = "Prize money for third place")]
                public int Prize3 { get; set; }
            }

            public class RacerOptions
            {
                [JsonProperty(PropertyName = "Disable metabolism while racing")]
                public bool DisableMetabolism { get; set; }

                [JsonProperty(PropertyName = "Racers clothing (chosen at random)")]
                public List<string[]> Clothing { get; set; } 
            }

            public class UIOptions
            {
                [JsonProperty(PropertyName = "Lap/Checkpoint counter")]
                public UICounter Counter { get; set; }

                [JsonProperty(PropertyName = "Lap/Checkpoint times")]
                public UICounter Times { get; set; }

                public class UICounter
                {
                    [JsonProperty(PropertyName = "Display to player")]
                    public bool Enabled { get; set; }

                    [JsonProperty(PropertyName = "Position - X minimum")]
                    public float Xmin { get; set; }

                    [JsonProperty(PropertyName = "Position - X maximum")]
                    public float XMax { get; set; }

                    [JsonProperty(PropertyName = "Position - Y minimum")]
                    public float YMin { get; set; }

                    [JsonProperty(PropertyName = "Position - Y maximum")]
                    public float YMax { get; set; }

                    [JsonProperty(PropertyName = "Background color (hex)")]
                    public string Color1 { get; set; }

                    [JsonProperty(PropertyName = "Background alpha")]
                    public float Color1A { get; set; }

                    [JsonProperty(PropertyName = "Status color (hex)")]
                    public string Color2 { get; set; }
                }
            }

            public Oxide.Core.VersionNumber Version;
        }

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                Automation = new ConfigData.EventAutomation
                {
                    Enabled = true,
                    Random = true,
                    Order = new List<string>()
                },
                Checkpoints = new ConfigData.CheckpointOptions
                {
                    Grade = "Metal",
                    Lights = true,
                    LightType = "Siren",
                    LaunchFirework = false,
                    FireworkPrefab = "assets/prefabs/deployable/fireworks/mortarchampagne.prefab",
                },
                CommandBlacklist = new string[] { "s", "tp", "tpr", "tpa", "home" },
                DisableSpectate = false,
                Entrance = new ConfigData.EntryOptions
                {
                    Amount = 100,
                    Economics = false,
                    ServerRewards = true,
                    Enabled = false,
                    Scrap = false,
                },
                Timers = new ConfigData.EventTimers
                {
                    CloseTime = 240,
                    StartTime = 45,
                    EndTime = 60,
                    Interval = 1800,
                    Countdown = 10
                },
                Racers = new ConfigData.RacerOptions
                {
                    Clothing = new List<string[]>
                    {
                        new string[] { "hazmatsuit" },
                        new string[] { "shoes.boots", "tshirt", "pants", "deer.skull.mask" }
                    },
                    DisableMetabolism = false
                },
                Rewards = new ConfigData.RewardOptions
                {
                    Economics = false,
                    Enabled = true,
                    Podium = true,
                    Prize1 = 250,
                    Prize2 = 125,
                    Prize3 = 50,
                    Scrap = false,
                    ServerRewards = true
                },
                UI = new ConfigData.UIOptions
                {
                    Counter = new ConfigData.UIOptions.UICounter
                    {
                        Color1 = "#F2F2F2",
                        Color1A = 0.05f,
                        Color2 = "#ce422b",
                        Enabled = true,
                        Xmin = 0.69f,
                        XMax = 0.83f,
                        YMin = 0.14f,
                        YMax = 0.175f
                    },
                    Times = new ConfigData.UIOptions.UICounter
                    {
                        Color1 = "#F2F2F2",
                        Color1A = 0.05f,
                        Color2 = "#ce422b",
                        Enabled = true,
                        Xmin = 0.69f,
                        XMax = 0.83f,
                        YMin = 0.18f,
                        YMax = 0.215f
                    }
                },
                Version = Version
            };
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

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(0, 1, 78))
            {
                configData.Timers.Countdown = baseConfig.Timers.Countdown;
            }

            if (configData.Version < new VersionNumber(0, 1, 80))
                configData.Checkpoints.LightType = baseConfig.Checkpoints.LightType;

            if (configData.Version < new VersionNumber(0, 1, 88))
            {
                configData.Checkpoints.FireworkPrefab = baseConfig.Checkpoints.FireworkPrefab;
                configData.Checkpoints.LaunchFirework = false;
            }

            if (configData.Version < new VersionNumber(0, 1, 92))
                configData.Racers = baseConfig.Racers;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion

        #region Data Management
        private void SaveTrackData()
        {
            trackData = raceTracks;
            trackdata.WriteObject(trackData);
        }

        private void SaveCarData()
        {
            cardata.WriteObject(carData);
        }

        private void SaveRestoreData() => restorationData.WriteObject(restoreData);

        private void SaveEntityData() => entitydata.WriteObject(spawnedEntities);

        private void LoadData()
        {
            try
            {
                trackData = trackdata.ReadObject<Hash<string, TrackData>>();
                raceTracks = trackData;
            }
            catch
            {
                trackData = new Hash<string, TrackData>();
            }

            try
            {
                carData = cardata.ReadObject<Hash<string, ModularCarData>>();
            }
            catch
            {
                carData = new Hash<string, ModularCarData>();
            }

            try
            {
                restoreData = restorationData.ReadObject<RestoreData>();
                if (restoreData.restoreData == null)
                    restoreData.restoreData = new Hash<ulong, RestoreData.PlayerData>();

            }
            catch
            {
                restoreData = new RestoreData();
            }
           
            try
            {
                spawnedEntities = entitydata.ReadObject<List<uint>>();
            }
            catch
            {
                spawnedEntities = new List<uint>();
            }
        }

        private class ModularCarData
        {
            public string chassis;

            public int tier = 2;

            public List<ModuleItem> moduleContainer;

            public ModularCarData() { }

            public ModularCarData(ModularCar modularCar, int tier)
            {
                chassis = modularCar.PrefabName;
                this.tier = tier;

                moduleContainer = new List<ModuleItem>();
                for (int i = 0; i < modularCar.Inventory.ModuleContainer.itemList.Count; i++)
                {
                    moduleContainer.Add(new ModuleItem(modularCar.Inventory.ModuleContainer.itemList[i]));
                }
            }

            public ModularCar LoadVehicle(Vector3 position, Quaternion rotation)
            {
                ModularCar modularCar = GameManager.server.CreateEntity(chassis, position, rotation) as ModularCar;
                modularCar.enableSaving = false;

                modularCar.spawnSettings.useSpawnSettings = false;

                modularCar.Spawn();

                for (int i = 0; i < moduleContainer.Count; i++)
                {
                    moduleContainer[i].Create(modularCar.Inventory.ModuleContainer);
                }

                modularCar.Invoke(()=> modularCar.AdminFixUp(tier), 1f);
                return modularCar;
            }

            public class ModuleItem
            {
                public int itemID;

                public int slot;

                public ModuleItem() { }

                public ModuleItem(Item item)
                {
                    itemID = item.info.itemid;
                    slot = item.position;
                }

                public void Create(ItemContainer target)
                {
                    Item item = ItemManager.CreateByItemID(itemID);
                    item.MoveToContainer(target, slot);
                }
            }
        }

        private class TrackData
        {
            public int laps, minPlayers, maxTime, horseBreed;
            public float totalDistance;
            public bool useCommander = false;
            public List<string> modularCarProfiles;

            public RaceMode raceMode;
            public RaceType raceType;

            public List<PointInfo> checkPoints;
            public List<PointInfo> gridPoints;

            public bool UseModularCars { get { return raceType == RaceType.Car && (modularCarProfiles?.Count ?? 0) > 0; } }

            public TrackData()
            {
                checkPoints = new List<PointInfo>();
                gridPoints = new List<PointInfo>();
            }
            
            public class PointInfo
            {
                public Vector3 position;
                public float rotation, size;

                public PointInfo() { }
                public PointInfo(Vector3 position, float rotation)
                {
                    this.position = position;
                    this.rotation = rotation;
                }
                public PointInfo(Vector3 position, float rotation, float size)
                {
                    this.position = position;
                    this.rotation = rotation;
                    this.size = size;
                }
            }            
        }

        public class RestoreData
        {
            public Hash<ulong, PlayerData> restoreData = new Hash<ulong, PlayerData>();

            public void AddData(BasePlayer player)
            {
                restoreData[player.userID] = new PlayerData(player);
            }

            public void AddPrizeToData(ulong playerId, int itemId, int amount)
            {
                PlayerData playerData;
                if (restoreData.TryGetValue(playerId, out playerData))
                {
                    ItemData[] items = new ItemData[playerData.containerMain.Length + 1];
                    for (int i = 0; i < playerData.containerMain.Length; i++)
                    {
                        items[i] = playerData.containerMain[i];
                    }
                    items[playerData.containerMain.Length] = new ItemData() { ammo = 0, ammotype = string.Empty, amount = amount, condition = 100, contents = new ItemData[0], instanceData = null, itemid = itemId, position = -1, skin = 0UL };
                    playerData.containerMain = items;
                }
            }

            public void RemoveData(ulong playerId)
            {
                if (HasRestoreData(playerId))
                    restoreData.Remove(playerId);
            }

            public bool HasRestoreData(ulong playerId) => restoreData.ContainsKey(playerId);

            public void RestorePlayer(BasePlayer player)
            {
                PlayerData playerData;
                if (restoreData.TryGetValue(player.userID, out playerData))
                {
                    player.inventory.containerWear.SetFlag(ItemContainer.Flag.IsLocked, false);

                    player.inventory.Strip();

                    player.metabolism.Reset();

                    if (player.IsSleeping() || player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
                    {
                        ins.timer.Once(1, () => RestorePlayer(player));
                        return;
                    }

                    ins.NextTick(() =>
                    {
                        playerData.SetStats(player);
                        ins.MovePosition(player, playerData.position, true);
                        RestoreAllItems(player, playerData);
                    });
                }
            }

            private void RestoreAllItems(BasePlayer player, PlayerData playerData)
            {
                if (player == null || !player.IsConnected)
                    return;

                if (RestoreItems(player, playerData.containerBelt, "belt") && RestoreItems(player, playerData.containerWear, "wear") && RestoreItems(player, playerData.containerMain, "main"))
                    RemoveData(player.userID);
            }

            private bool RestoreItems(BasePlayer player, ItemData[] itemData, string type)
            {
                ItemContainer container = type == "belt" ? player.inventory.containerBelt : type == "wear" ? player.inventory.containerWear : player.inventory.containerMain;

                for (int i = 0; i < itemData.Length; i++)
                {
                    ItemData data = itemData[i];
                    if (data.amount < 1)
                        continue;

                    Item item = CreateItem(data);
                    item.position = data.position;
                    item.SetParent(container);
                }
                return true;
            }

            private Item CreateItem(ItemData itemData)
            {
                Item item = ItemManager.CreateByItemID(itemData.itemid, itemData.amount, itemData.skin);
                item.condition = itemData.condition;
                item.maxCondition = itemData.maxCondition;                

                if (itemData.frequency > 0)
                {
                    ItemModRFListener rfListener = item.info.GetComponentInChildren<ItemModRFListener>();
                    if (rfListener != null)
                    {                       
                        PagerEntity pagerEntity = BaseNetworkable.serverEntities.Find(item.instanceData.subEntity) as PagerEntity;
                        if (pagerEntity != null)
                        {
                            pagerEntity.ChangeFrequency(itemData.frequency);
                            item.MarkDirty();
                        }             
                    }
                }

                if (itemData.instanceData?.IsValid() ?? false)
                    itemData.instanceData.Restore(item);

                BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon != null)
                {
                    if (!string.IsNullOrEmpty(itemData.ammotype))
                        weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(itemData.ammotype);
                    weapon.primaryMagazine.contents = itemData.ammo;
                }

                FlameThrower flameThrower = item.GetHeldEntity() as FlameThrower;
                if (flameThrower != null)
                    flameThrower.ammo = itemData.ammo;

                if (itemData.contents != null)
                {
                    foreach (ItemData contentData in itemData.contents)
                    {
                        Item newContent = ItemManager.CreateByItemID(contentData.itemid, contentData.amount);
                        if (newContent != null)
                        {
                            newContent.condition = contentData.condition;
                            newContent.MoveToContainer(item.contents);
                        }
                    }
                }
                return item;
            }

            public class PlayerData
            {
                public float[] stats;
                public Vector3 position;
                public ItemData[] containerMain;
                public ItemData[] containerWear;
                public ItemData[] containerBelt;

                public PlayerData() { }

                public PlayerData(BasePlayer player)
                {
                    stats = GetStats(player);
                    position = player.transform.position;

                    containerBelt = GetItems(player.inventory.containerBelt).ToArray();
                    containerMain = GetItems(player.inventory.containerMain).ToArray();
                    containerWear = GetItems(player.inventory.containerWear).ToArray();
                }

                private IEnumerable<ItemData> GetItems(ItemContainer container)
                {
                    return container.itemList.Select(item => new ItemData
                    {
                        itemid = item.info.itemid,
                        amount = item.amount,
                        ammo = item.GetHeldEntity() is BaseProjectile ? (item.GetHeldEntity() as BaseProjectile).primaryMagazine.contents : item.GetHeldEntity() is FlameThrower ? (item.GetHeldEntity() as FlameThrower).ammo : 0,
                        ammotype = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.ammoType.shortname ?? null,
                        position = item.position,
                        skin = item.skin,
                        condition = item.condition,
                        maxCondition = item.maxCondition,
                        frequency = ItemModAssociatedEntity<PagerEntity>.GetAssociatedEntity(item)?.GetFrequency() ?? -1,
                        instanceData = new ItemData.InstanceData(item),
                        contents = item.contents?.itemList.Select(item1 => new ItemData
                        {
                            itemid = item1.info.itemid,
                            amount = item1.amount,
                            condition = item1.condition
                        }).ToArray()
                    });
                }

                private float[] GetStats(BasePlayer player) => new float[] { player.health, player.metabolism.hydration.value, player.metabolism.calories.value };

                public void SetStats(BasePlayer player)
                {
                    player.health = stats[0];
                    player.metabolism.hydration.value = stats[1];
                    player.metabolism.calories.value = stats[2];
                    player.metabolism.SendChangesToClient();
                }                
            }

            public class ItemData
            {
                public int itemid;
                public ulong skin;
                public int amount;
                public float condition;
                public float maxCondition;
                public int ammo;
                public string ammotype;
                public int position;
                public int frequency;
                public InstanceData instanceData;
                public ItemData[] contents;

                public class InstanceData
                {
                    public int dataInt;
                    public int blueprintTarget;
                    public int blueprintAmount;
                    public uint subEntity;

                    public InstanceData() { }
                    public InstanceData(Item item)
                    {
                        if (item.instanceData == null)
                            return;

                        dataInt = item.instanceData.dataInt;
                        blueprintAmount = item.instanceData.blueprintAmount;
                        blueprintTarget = item.instanceData.blueprintTarget;
                    }

                    public void Restore(Item item)
                    {
                        if (item.instanceData == null)
                            item.instanceData = new ProtoBuf.Item.InstanceData();

                        item.instanceData.ShouldPool = false;

                        item.instanceData.blueprintAmount = blueprintAmount;
                        item.instanceData.blueprintTarget = blueprintTarget;
                        item.instanceData.dataInt = dataInt;

                        item.MarkDirty();
                    }

                    public bool IsValid()
                    {
                        return dataInt != 0 || blueprintAmount != 0 || blueprintTarget != 0;
                    }
                }
            }
        }

        private class UnityVector3Converter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var vector = (Vector3)value;
                writer.WriteValue($"{vector.x} {vector.y} {vector.z}");
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String)
                {
                    var values = reader.Value.ToString().Trim().Split(' ');
                    return new Vector3(Convert.ToSingle(values[0]), Convert.ToSingle(values[1]), Convert.ToSingle(values[2]));
                }
                var o = JObject.Load(reader);
                return new Vector3(Convert.ToSingle(o["x"]), Convert.ToSingle(o["y"]), Convert.ToSingle(o["z"]));
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Vector3);
            }
        }
        #endregion

        #region Localization
        private static string msg(string key, string playerId = null) => ins.lang.GetMessage(key, ins, playerId);

        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["ui.lap"] = "LAP : <color={0}>{1} / {2}</color>",
            ["ui.checkpoint"] = "CHECKPOINT : <color={0}>{1} / {2}</color>",
            ["ui.laptime"] = "LAP TIME : <color={0}>{1}</color>",
            ["leave"] = "Leave",
            ["close"] = "Close",
            ["player"] = "Player",
            ["distance"] = "Distance",
            ["time"] = "Time",
            ["ui.info.laps"] = "<color=#D3D3D3>Lap Race  |  </color><color=#ce422b>{0} laps</color><color=#D3D3D3>  |  </color><color=#ce422b>{1} checkpoints</color>",
            ["ui.info.sprint"] = "<color=#D3D3D3>Sprint Race  |  </color><color=#ce422b>{0} checkpoints</color>",
            ["ui.info.distance"] = "<color=#ce422b>{0} M</color><color=#D3D3D3> total distance</color>",
            ["missedcp"] = "<color=#D3D3D3>You have missed a checkpoint. </color><color=#ce422b>Turn around!</color>",
            ["win.end"] = "<color=#ce422b>{0}</color> <color=#D3D3D3>has won the race! You have</color><color=#ce422b> {1} seconds</color> <color=#D3D3D3>to make it to the finish line!</color>",
            ["win.winners"] = "<color=#ce422b>The race is over!</color> <color=#D3D3D3>The winners are:</color> {0}",
            ["win.podium"] = "\n<color=#ce422b>{0}</color> <color=#D3D3D3>{1}</color>",
            ["win.viewscores"] = "Type <color=#ce422b>/racescores</color> <color=#D3D3D3>to view the scoreboard!</color>",
            ["win.countdown"] = "<color=#D3D3D3>You have</color><color=#ce422b> {0} seconds</color> <color=#D3D3D3>to make it to the finish line!</color>",
            ["scoreboard.nonesaved"] = "<color=#D3D3D3>There is no scoreboard currently available</color>",
            ["countdown.count"] = "<color=#B20000>{0}</color>",
            ["countdown.go"] = "<color=#00CD00>GO!</color>",
            ["noevent"] = "<color=#D3D3D3>There is no event in progress</color>",
            ["leftevent"] = "<color=#ce422b>{0} </color><color=#D3D3D3>has left the event</color>",
            ["leftevent.wait"] = "<color=#ce422b>{0} </color><color=#D3D3D3>has left the event. Waiting for more players to start</color>",
            ["minplayers.reached"] = "<color=#D3D3D3>Minimum players has been reached. The event will start in</color> <color=#ce422b>{0} seconds</color>",
            ["joinevent"] = "<color=#ce422b>You have joined the race</color>",
            ["joinevent.player"] = "<color=#ce422b>{0}</color> <color=#D3D3D3>has joined the race</color>",
            ["eventfull"] = "<color=#D3D3D3>This event already has the maximum amount of contestents</color>",
            ["inevent"] = "<color=#D3D3D3>You are already in this event</color>",
            ["noevent"] = "<color=#D3D3D3>There is no event in progress</color>",
            ["nojoin"] = "<color=#D3D3D3>You can't join an event that is in progress</color>",
            ["cancelled"] = "<color=#ce422b>The event has been cancelled</color>",
            ["eventopen1"] = "<color=#D3D3D3>A {0} race event has been opened! You can join by typing</color> <color=#ce422b>/race join</color>",
            ["eventopenfee1"] = "<color=#D3D3D3>A {2} race event has been opened! Entrance to this race will cost you </color> <color=#ce422b>{0} {1}</color> <color=#D3D3D3>.\nYou can join by typing</color> <color=#ce422b>/race join</color>",
            ["eventprize"] = "<color=#D3D3D3>The prize for winning this event is</color><color=#ce422b> {0} {1}</color>",
            ["eventprizepodium"] = "<color=#D3D3D3>The prizes for this event are:</color>\n<color=#ce422b>First Place: {0} {1}\nSecond Place: {2} {1}\nThird Place: {3} {1}</color>",
            ["reset_vehicle"] = "<color=#D3D3D3>Type <color=#ce422b>/reset</color> <color=#D3D3D3>to reset your vehicle back at the last checkpoint</color></color>",
            ["eventfee"] = "<color=#D3D3D3>The fee to enter this event is </color><color=#ce422b>{0} {1}</color><color=#D3D3D3>. You do not have enough to enter</color>",
            ["economics"] = "coins",
            ["serverrewards"] = "RP",
            ["scrap"] = "Scrap",
            ["blacklistcmd"] = "<color=#939393>You can not run that command while you are racing!</color>",
            ["nopermission"] = "<color=#939393>You do not have permission to enter the race</color>",
            ["eventtimer"] = "<color=#939393>You have <color=#ce422b>{0}</color> to finish the race!</color>",
            ["spectatecycle"] = "<color=#D3D3D3>Press <color=#ce422b>JUMP</color> to cycle spectate targets</color>",
        };       
        #endregion
    }
}
