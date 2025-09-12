// Requires: TalkingNpc
using System.Collections.Generic;
using System.Collections;
using Oxide.Core.Configuration;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;
using Oxide.Core;
using System;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("TalkingNpcVehicles", "Razor", "1.3.2")]
    [Description("Save locations for vehicel spawns")]
    public class TalkingNpcVehicles : RustPlugin
    {
        #region Init/Unload
        [PluginReference]
        private Plugin SpawnModularCar;

        public static TalkingNpc talkingNpc;

        public static TalkingNpcVehicles _;
        private List<string> vehiclestypes = new List<string>() { "modularcar", "copter", "snowmobile", "boat" };

        private void Init()
        {
            _ = this;
            talkingNpc = TalkingNpc.Instance;
        }

        private void Unload()
        {
            foreach (var Controler in UnityEngine.Object.FindObjectsOfType<AddVehicleBehavior>())
            {
                UnityEngine.Object.Destroy(Controler);
            }

            _ = null;
            talkingNpc = null;
        }
        #endregion

        #region Data Handling
        bool DataFileExists(string path)
        {
            return Interface.Oxide.DataFileSystem.ExistsDatafile($"TalkingNpc/Addons/{path}");
        }

        bool VehicleDataFileExists(string path) => DataFileExists(path);

        public bool ConvoFileExists(string name)
        {
            return Interface.Oxide.DataFileSystem.ExistsDatafile($"TalkingNpc/Conversations/{name}");
        }

        DynamicConfigFile GetDataFile(string path)
        {
            return Interface.Oxide.DataFileSystem.GetFile($"TalkingNpc/Addons/{path}");
        }

        private static void LoadData<T>(out T data, string filename) =>
            data = Interface.Oxide.DataFileSystem.ReadObject<T>($"TalkingNpc/Addons/{filename}");

        DynamicConfigFile GetVehicleDataFile(string path) => GetDataFile(path);

        bool LoadOrCreateVehicles(string filename, out VehicleInfo data)
        {
            if (VehicleDataFileExists(filename))
            {
                var file = GetVehicleDataFile(filename);
                try
                {
                    data = file.ReadObject<VehicleInfo>();
                    return true;
                }
                catch (Exception ex)
                {
                    PrintError($"Error reading data from {file.Filename}: ${ex.Message}");
                    data = new VehicleInfo();
                    return false;
                }
            }
            else
            {
                data = new VehicleInfo();
                SaveVehicles(filename, data);
                return true;
            }
        }

        void SaveVehicles(string filename, VehicleInfo data)
        {
            GetVehicleDataFile(filename).WriteObject(data);
        }

        public class VehicleInfo : Dictionary<string, SpawnInfo> { }

        public class SpawnInfo
        {
            public string prefab;
            public Vector3 position;
            public Vector3 rotation;
            public string monument;
            public int fuleAmount;
            public bool teamlock;
            public string skinID;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public CarPreset presets = null;

            public SpawnInfo(Vector3 position, Vector3 rotation, string prefab, CarPreset preset = null, string monument = "")
            {
                this.prefab = prefab;
                this.position = position;
                this.rotation = rotation;
                this.monument = monument;
                this.presets = preset;
            }
        }

        public class CarPreset
        {
            public bool CodeLock = false;
            public bool KeyLock = false;
            public int EnginePartsTier = 1;
            public int FreshWaterAmount = 0;
            public string[] Modules = null;

            public CarPreset(bool initialize = false)
            {
                Modules = new string[]
                {
                    "vehicle.1mod.cockpit.with.engine",
                    "vehicle.1mod.flatbed"
                };
            }
        }
        #endregion

        #region MonoAddVehicle
        private bool checkInfo(BasePlayer player, string npcname, string vehiceltype, int points, string atMonument, MonumentInfo monument = null)
        {
            if (talkingNpc.conversationfileExists(npcname.ToLower()))
                        {
                SendReply(player, lang.GetMessage("inUse", this));
                return false;
            }

            if (DataFileExists(npcname.ToLower()))
            {
                SendReply(player, lang.GetMessage("dataisThere", this));
                return false;
            }

            if (_.ConvoFileExists(npcname))
            {
                SendReply(player, lang.GetMessage("ConvoExists", this));
                return false;
            }
            else if (!vehiclestypes.Contains(vehiceltype))
            {
                SendReply(player, lang.GetMessage("UnknownType", this));
                return false;
            }

            player.gameObject.AddComponent<AddVehicleBehavior>().StartCreateConvo(player, npcname.ToLower(), vehiceltype, points, atMonument, monument);
            return true;
        }

        class AddVehicleBehavior : FacepunchBehaviour
        {
            private BasePlayer player { get; set; }
            private bool finished { get; set; }
            private string NpcName { get; set; }
            private int pointsNeeded { get; set; }
            private int currentPoint { get; set; }
            private bool active { get; set; }
            private float nextPressTime { get; set; }
            private DateTime autoDestroy { get; set; }
            private MonumentInfo monument { get; set; }
            private string monumentName { get; set; } = "";
            private CarPreset presets { get; set; }
            private string prefabName { get; set; }
            private VehicleInfo vInfo = null;
            private string vehiceltype { get; set; }
            private string messageVehicleName { get; set; }

            private void Awake()
            {
                autoDestroy = DateTime.Now.AddSeconds(320);
                player = GetComponent<BasePlayer>();
                finished = false;
            }

            public void StartCreateConvo(BasePlayer player, string npcname, string vehiceltype1, int points, string atMonument, MonumentInfo monum)
            {
                monument = monum;
                NpcName = npcname;
                pointsNeeded = points;
                vehiceltype = vehiceltype1;

                currentPoint = 1;

                if (monument != null)
                   monumentName = _.GetMonumentName(monument);

                if (vehiceltype1 == "modularcar")
                {
                    talkingNpc.GenerateModularCarConversation(npcname);
                    presets = new CarPreset();
                    pointsNeeded = 3;
                    messageVehicleName = "car one";
                }
                else if (vehiceltype1 == "copter")
                {
                    talkingNpc.GenerateCopterConversation(npcname);
                    pointsNeeded = 2;
                    messageVehicleName = "Minicopter";
                }
                else if (vehiceltype1 == "snowmobile")
                {
                    talkingNpc.GenerateSnowMobileConversation(npcname);
                    pointsNeeded = 2;
                    messageVehicleName = "snowmobile";
                }
                else if (vehiceltype1 == "boat")
                {
                    talkingNpc.GenerateBoatConversation(npcname);
                    pointsNeeded = 3;
                    messageVehicleName = "rowboat";
                }
                else
                {
                    Destroy(this);
                }

                string[] args = { "add", npcname.ToLower(), npcname.ToLower(), atMonument };
                talkingNpc.CommandSpawnCall(player, "talkingnpc", args);

                GameTips(player, $"{_.lang.GetMessage("goto", _)} {messageVehicleName} {_.lang.GetMessage("spawnLocation", _)}");
                active = true;
            }

            private void Update()
            {
                if (!active) return;

                if (player == null || !player.IsConnected || autoDestroy < DateTime.Now)
                {
                    Destroy(this);
                    return;
                }

                if (player.serverInput.WasJustPressed(BUTTON.RELOAD))
                {
                    float time = Time.realtimeSinceStartup;
                    if (nextPressTime < time)
                    {
                        nextPressTime = time + 0.2f;
                        setSpawnLocation(currentPoint);
                        currentPoint++;
                    }
                }
                if (currentPoint > pointsNeeded)
                {
                    finished = true;
                    Destroy(this);
                }
            }

            private void setSpawnLocation(int point)
            {
                string message = "car one";
                if (point == 1)
                    message = "car two";
                if (point == 2)
                    message = "car three";

                string savePoint = point.ToString();

                prefabName = vehiceltype;

                if (vehiceltype.Contains("copter"))
                {
                    prefabName = point == 1 ? "assets/content/vehicles/minicopter/minicopter.entity.prefab" : "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab";
                    savePoint = point == 1 ? "Minicopter" : "scraptransporthelicopter";
                    if (point == 1)
                        message = "scraptransporthelicopter";
                }
                else if (vehiceltype.Contains("boat"))
                {
                    prefabName = point == 1 ? "assets/content/vehicles/boats/rowboat/rowboat.prefab" : "assets/content/vehicles/boats/rhib/rhib.prefab";
                    savePoint = point == 1 ? "rowboat" : "rhib";
                    if (point == 2)
                    {
                        prefabName = "assets/content/vehicles/boats/tugboat/tugboat.prefab";
                        savePoint = "tugboat";
                        message = "tugboat";
                    }
                    if (point == 1)
                        message = "rhib";
                }
                else if (vehiceltype.Contains("snowmobile"))
                {
                    prefabName = point == 1 ? "assets/content/vehicles/snowmobiles/snowmobile.prefab" : "assets/content/vehicles/snowmobiles/tomahasnowmobile.prefab";
                    savePoint = point == 1 ? "snow" : "tomaha";
                    if (point == 1)
                        message = "tomaha";
                }

                var localEuler = new Vector3(0, player.transform.rotation.y, 0);
                var position = player.transform.position;

                if (monument != null)
                {
                    var localY = player.viewAngles.y - monument.transform.rotation.eulerAngles.y;
                    localEuler = new Vector3(0, (localY + 360) % 360, 0);
                    position = monument.transform.InverseTransformPoint(player.transform.position);
                }

                if (vInfo == null && !_.LoadOrCreateVehicles(NpcName, out vInfo))
                {
                    if (!_.DataFileExists(NpcName))
                    {
                        _.PrintWarning($"Creating new datafiles for addon with name {NpcName}");
                    }
                }
                        
                SpawnInfo newInfo = new SpawnInfo(position, localEuler, prefabName, presets, monumentName);
                vInfo.Add(savePoint, newInfo);
                _.SaveVehicles(NpcName, vInfo);

                GameTips(player, $"{_.lang.GetMessage("goto", _)} {message} {_.lang.GetMessage("spawnLocation", _)}"); 
            }

            private void OnDestroy()
            {
                player?.SendConsoleCommand("gametip.hidegametip");

                if (finished)
                    GameTips(player, $"{_.lang.GetMessage("finished", _)}", 2f);
                else
                    GameTips(player, $"{_.lang.GetMessage("SomthingWrong", _)} {NpcName}", 3f);
            }
        }

        public static void GameTips(BasePlayer player, string message, float destroytime = 0)
        {
            if (player != null && player.userID.IsSteamId())
            {
                player?.SendConsoleCommand("gametip.hidegametip");
                player?.SendConsoleCommand("gametip.showgametip", message);
                if (destroytime > 0)
                  _.timer.Once(destroytime, () => player?.SendConsoleCommand("gametip.hidegametip"));
            }
        }
        #endregion

        #region CommandHooks
        private object OnTalkingNpcCommand(BasePlayer player, string[] args)
        {
            string colorCode = "#FFFF00";
            CarPreset presets = null;
            float lowestDist = float.MaxValue;
            MonumentInfo closestMonument = null;

            var primaryCommand = args.Length > 0 ? args[0].ToLower() : string.Empty;
            switch (primaryCommand)
            {
                case "vehicle":
                    var SecindaryCommand = args.Length > 1 ? args[1].ToLower() : string.Empty;

                    switch (SecindaryCommand)
                    {
                        case "location":
                            foreach (var monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
                            {
                                if (GetMonumentName(monument) == null || GetMonumentName(monument).Contains("substation")) continue;
                                float dist = Vector3.Distance(player.transform.position, monument.transform.position);
                                if (dist < lowestDist)
                                {
                                    lowestDist = dist;
                                    closestMonument = monument;
                                }
                            }

                            if (closestMonument == null)
                                return true;

                            var monumentNamey = GetMonumentName(closestMonument);
                            if (string.IsNullOrEmpty(monumentNamey))
                            {
                                SendReply(player, lang.GetMessage("NoMonment", this));
                                return true;
                            }

                            var localYy = player.viewAngles.y - closestMonument.transform.rotation.eulerAngles.y;
                            var localEulery = new Vector3(0, (localYy + 360) % 360, 0);
                            var positiony = closestMonument.transform.InverseTransformPoint(player.transform.position);

                            SendReply(player, lang.GetMessage("MonumentInfo", this), monumentNamey, positiony, localEulery);
                            PrintWarning(string.Format(lang.GetMessage("MonumentInfo", this), monumentNamey, positiony, localEulery));
                            return true;

                        case "rhib":
                        case "boat":
                        case "copter":
                        case "snowmobile":
                        case "modularcar":
                            if (args.Length < 3)
                            {
                                SendReply(player, lang.GetMessage("usages", this));
                                return true;
                            }
                            
                            if (DataFileExists(args[2].ToLower()))
                            {
                                SendReply(player, lang.GetMessage("vendorexists", this), args[2].ToLower());
                                return true;
                            }
                            
                            string prefabName = SecindaryCommand;

                            if (prefabName == "modularcar")
                                presets = new CarPreset();

                            if (args.Length == 4 && args[3].ToLower() == "true")
                            {
                                foreach (var monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
                                {
                                    if (GetMonumentName(monument) == null || GetMonumentName(monument).Contains("substation")) continue;
                                    float dist = Vector3.Distance(player.transform.position, monument.transform.position);
                                    if (dist < lowestDist)
                                    {
                                        lowestDist = dist;
                                        closestMonument = monument;
                                    }
                                }

                                var monumentName = GetMonumentName(closestMonument);
                                if (string.IsNullOrEmpty(monumentName))
                                {
                                    SendReply(player, lang.GetMessage("NoMonment", this));
                                    return true; 
                                }

                                var localY = player.viewAngles.y - closestMonument.transform.rotation.eulerAngles.y;
                                var localEuler = new Vector3(0, (localY + 360) % 360, 0);

                                if (checkInfo(player, args[2].ToLower(), SecindaryCommand, 3, "true", closestMonument))
                                {

                                }
                            }
                            else
                            {
                                if (checkInfo(player, args[2].ToLower(), SecindaryCommand, 3, "false", null))
                                {
                                }
                            }
                            return true;

                        default:
                            NextTick(() =>
                            {
                                SendReply(player, lang.GetMessage("usages", this));
                            });
                            break;
                    }
                    return true;

                case "talking_npc":
                    if (player == null || args.Length < 5 || args[1].ToLower() != "vehicle" || args[2].ToLower() != "spawn") return true;

                    if (!DataFileExists(args[4].ToLower()))
                        return true;

                    VehicleInfo theData = null;
                    LoadData(out theData, args[4].ToLower());

                    if (theData == null) { PrintWarning($"{lang.GetMessage("invalidAddon", this)} {args[4].ToLower()}"); return true; }
                   
                    SpawnInfo profile = theData[args[3].ToLower()];

                    if (profile == null) { PrintWarning($"{lang.GetMessage("invalidAddonConfig", this)} {args[3].ToLower()}"); return true; }

                    if (!string.IsNullOrEmpty(profile.monument))
                    {
                        foreach (var monument in TerrainMeta.Path.Monuments.Where(m => m != null && GetMonumentName(m).ToLower().Contains(profile.monument, CompareOptions.IgnoreCase)))
                        {
                            spawnEntity(player, profile, true, monument);
                        }
                    }
                    else
                    {
                        spawnEntity(player, profile);
                    }

                    return true;

                default:
                    NextTick(() =>
                    {
                        SendReply(player, lang.GetMessage("usages", this));
                    });
                    break;
            }
            return null;
        }
        #endregion

        #region Function
        private void SpawnCars(BasePlayer player, SpawnInfo profile, Vector3 position, Quaternion rotation)
        {
            Dictionary<string, object> options = new Dictionary<string, object>
            {
                ["CodeLock"] = profile.presets.CodeLock,
                ["KeyLock"] = profile.presets.KeyLock,
                ["EnginePartsTier"] = profile.presets.EnginePartsTier,
                ["FreshWaterAmount"] = profile.presets.FreshWaterAmount,
                ["FuelAmount"] = profile.fuleAmount,
                ["Modules"] = profile.presets.Modules
            };
            SpawnModularCar?.Call("API_SpawnPreset", options, player, position, rotation);
        }

        public string GetMonumentName(MonumentInfo monument)
        {
            var gameObject = monument.gameObject;

            while (gameObject.name.StartsWith("assets/") == false && gameObject.transform.parent != null)
            {
                gameObject = gameObject.transform.parent.gameObject;
            }

            var monumentName = gameObject?.name;
            if (monumentName.Contains("monument_marker.prefab"))
                monumentName = monument.gameObject?.gameObject?.transform?.parent?.gameObject?.transform?.root?.name;

            return monumentName;
        }

        private void spawnEntity(BasePlayer player, SpawnInfo profile, bool monumentTest = false, MonumentInfo monument = null)
        {
            if (monumentTest && monument == null) { PrintWarning("Monument not found"); return; }

            StorageContainer fuelContainer = null;
            var spawnPosition = monument == null ? profile.position : monument.transform.TransformPoint(profile.position);
            var spawnRotation = monument == null ? Quaternion.Euler(profile.rotation) : monument.transform.rotation * Quaternion.Euler(profile.rotation);

            if (Vector3.Distance(player.transform.position, spawnPosition) > 50f) return;

            if (profile.prefab == "modularcar")
            {
                SpawnCars(player, profile, spawnPosition, spawnRotation);
                return;
            }
            var spawnObject = GameManager.server.CreateEntity(profile.prefab, spawnPosition, spawnRotation);
            if (spawnObject == null) { PrintWarning($"Failed to spawn {profile.prefab.ToString()}"); return; }

            spawnObject.Spawn();

            if (spawnObject is Minicopter)
            {
                if (profile.fuleAmount > 0)
                {
                    fuelContainer = (spawnObject as Minicopter).GetFuelSystem().GetFuelContainer();
                    fuelContainer?.inventory.AddItem(fuelContainer.allowedItem, profile.fuleAmount);
                }
                if (profile.teamlock)
                    (spawnObject as Minicopter).SetupOwner(player, spawnPosition, 100f);
            }
            else if (spawnObject is Snowmobile)
            {
                if (profile.fuleAmount > 0)
                {
                    fuelContainer = (spawnObject as Snowmobile).GetFuelSystem().GetFuelContainer();
                    fuelContainer?.inventory.AddItem(fuelContainer.allowedItem, profile.fuleAmount);
                }
                if (profile.teamlock)
                    (spawnObject as Snowmobile).SetupOwner(player, spawnPosition, 100f);
            }
            else if (spawnObject is MotorRowboat)
            {
                if (profile.fuleAmount > 0)
                {
                    fuelContainer = (spawnObject as MotorRowboat).GetFuelSystem().GetFuelContainer();
                    fuelContainer?.inventory.AddItem(fuelContainer.allowedItem, profile.fuleAmount);
                }
                if (profile.teamlock)
                    (spawnObject as MotorRowboat).SetupOwner(player, spawnPosition, 100f);
            }
        }
        #endregion

        #region Messages
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["inUse"] = "This npc name is in use already!",
                ["dataisThere"] = "This npc name is in use already in vehicles datafolder!",
                ["ConvoExists"] = "This conversation file exists already",
                ["UnknownType"] = "The vehicle type is unknown",
                ["SomthingWrong"] = "Somthing went wrong you must manualy delete the created datafiles",
                ["finished"] = "You have finished creating the npc and spawn points",
                ["goto"] = "Go to",
                ["spawnLocation"] = "vehicel spawn location and press the reload key!",
                ["usages"] = "Vehicle Usage:\n\n <color=#FFFF00>/talking_npc vehicle <type> <Uneek_VendorName> <true/false></color> - Adds vehice npc to location.",
                ["invalidAddon"] = "Tried to spawn invalid addon file {0}",
                ["invalidAddonConfig"] = "Tried to spawn invalid profile in addon file",
                ["NoMonment"] = "Couldn't find closest monument",
                ["vendorexists"] = "This vendor name {0} exists already",
                ["MonumentInfo"] = "Monument Info Nane:\n {0}\nPosition: {1}\nRotation: {2}"
            }, this);
        }
        #endregion
    }
}

