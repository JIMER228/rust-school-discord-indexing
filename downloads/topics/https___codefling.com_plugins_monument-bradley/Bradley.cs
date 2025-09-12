using System.Collections.Generic;
using System.Collections;
using Oxide.Core.Configuration;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
using Oxide.Core;
using System;
using Rust;
using static Oxide.Plugins.BradleyEx.BradleyEx;

namespace Oxide.Plugins
{
    [Info("Bradley", "Razor", "1.5.4")]
    [Description("Spawn Bradly at other monuments")]
    public class Bradley : RustPlugin
    {
        #region Vars
        private bool debug = false;
        PathEntity pcdData;
        private DynamicConfigFile PCDDATA;
        public static Bradley _instance;
        private BUTTON Main;
        public Dictionary<BradleyAPC, MonumentName> BradleySaveData = new Dictionary<BradleyAPC, MonumentName>();
        public Dictionary<BradleyAPC, string> BradleyCustomSaveData = new Dictionary<BradleyAPC, string>();
        public Dictionary<BradleyAPC, string> BradleyCustomSaveDataRoad = new Dictionary<BradleyAPC, string>();
        public List<PathMaker> ControllerPlayer = new List<PathMaker>();
        static System.Random random = new System.Random();
        private readonly Dictionary<ulong, Timer> timers = new Dictionary<ulong, Timer>();
        private const string theAdmin = "bradley.admin";
        public Dictionary<ulong, DateTime> sleepers = new Dictionary<ulong, DateTime>();
        public const string bradPrefab = "assets/prefabs/npc/m2bradley/bradleyapc.prefab";
        #endregion

        #region Localization
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["nope"] = "<color=#ce422b>You lack the perms to use this command!</color>",
                ["PathAddNew"] = "<color=#ce422b>You Added path marker {0} with speed {1}</color>",
                ["PauseAddTime"] = "<color=#ce422b>{1} second pause on path marker {0}</color>",
                ["SpeedAddTime"] = "<color=#ce422b>Speed to next path is now {1} set on path marker {0}</color>",
                ["Active"] = "<color=#ce422b>The controler is now activated.</color>",
                ["IsActive"] = "<color=#ce422b>The controller is already active...</color>",
                ["deactive"] = "<color=#ce422b>The controller is now deactivated.</color>",
                ["usagecommand"] = "<color=#ce422b>/bradley <activate, deactivate, new, edit, move, delete></color>",
                ["Newusage"] = "<color=#ce422b>/bradley new <SomeName></color>",
                ["NotActive"] = "<color=#ce422b>The controller is not active.</color>",
                ["New"] = "<color=#ce422b>Creating a new path.. Use the default USE key to set path markers</color>",
                ["isthere"] = "<color=#ce422b>This path name already exists... Try editing it instead..</color>",
                ["editusage"] = "<color=#ce422b>/bradley edit <pathName></color>",
                ["Editing"] = "<color=#ce422b>You are now editing path {0}....</color>",
                ["isnotthere"] = "<color=#ce422b>Path {0} does not exist....</color>",
                ["moveusage"] = "<color=#ce422b>/bradley move <PathName> <pathNumber></color>",
                ["PathMoved"] = "<color=#ce422b>You just moved the path to your location...</color>",
                ["NoPath"] = "<color=#ce422b>That path number does not exist...</color>",
                ["NoPathname"] = "<color=#ce422b>That path name does not exist...</color>",
                ["deleteusage"] = "<color=#ce422b>/bradley delete <PathName></color>",
                ["delete"] = "<color=#ce422b>You just deleted path {0}...</color>",
                ["newHealth"] = "<color=#ce422b>Health changed from {0} to {1}</color>",
            }, this);
        }

        private void RegisterPermissions()
        {
            permission.RegisterPermission(theAdmin, this);
        }
        #endregion

        #region Config 
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Monuments to add bradley to")]
            public Settings settings { get; set; }

            [JsonProperty(PropertyName = "Roads to add bradley to")]
            public SettingsRing settingsRing { get; set; }

            public class Settings
            {
                public string Button { get; set; }
                public Dictionary<MonumentName, bradleyMonumentInfo> MonumentWithBradley { get; set; }
            }

            public class SettingsRing
            {
                public List<roadRing> RingRoadSettings { get; set; }
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
                settings = new ConfigData.Settings
                {
                    Button = "USE",
                    MonumentWithBradley = new Dictionary<MonumentName, bradleyMonumentInfo>()
                    {
                       { MonumentName.Airfield, new bradleyMonumentInfo() },
                       { MonumentName.WaterTreatment, new bradleyMonumentInfo() },
                       { MonumentName.Trainyard, new bradleyMonumentInfo() },
                       { MonumentName.Powerplant, new bradleyMonumentInfo() },
                       { MonumentName.Harbor_B, new bradleyMonumentInfo() },
                       { MonumentName.Harbor_A, new bradleyMonumentInfo() },
                       { MonumentName.Junkyard, new bradleyMonumentInfo() },
                       { MonumentName.Dome, new bradleyMonumentInfo() }
                    }
                },

                settingsRing = new ConfigData.SettingsRing
                {
                    RingRoadSettings = new List<roadRing>()
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(1, 0, 1))
                configData = baseConfig;

            if (configData.Version < new VersionNumber(1, 3, 0))
                configData.settings.MonumentWithBradley.Add(MonumentName.Harbor_A, new bradleyMonumentInfo());

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion

        #region The Button
        private BUTTON ConvertToButton(string button)
        {
            try
            {
                return (BUTTON)Enum.Parse(typeof(BUTTON), button);
            }
            catch (Exception)
            {
                return BUTTON.USE;
            }
        }
        #endregion

        #region Oxide Hooks
        private void Init()
        {
            if (configData.settingsRing.RingRoadSettings.Count <= 0)
            {
                configData.settingsRing.RingRoadSettings.Add(new roadRing
                {
                    RingRoadEnabled = true,
                    RespawnMin = 30,
                    Can_Kill_NPC = false,
                    IgnoreSleepers = false,
                    moveForceMax_250_To_2000 = 650f,
                    Health = 1000.0f,
                    CanStop = true,
                    FireRocketAtPlayerBase = false,
                    RocketPrefab = "assets/prefabs/ammo/rocket/rocket_smoke.prefab",
                    maxCratesToSpawn = 3
                }); ;
                SaveConfig();
            }
            else if (configData.settingsRing.RingRoadSettings[0].RocketPrefab == null)
            {
                configData.settingsRing.RingRoadSettings[0].RocketPrefab = "assets/prefabs/ammo/rocket/rocket_smoke.prefab";
                SaveConfig();
            }
            _instance = this;
            PCDDATA = Interface.Oxide.DataFileSystem.GetFile(Name + "/CustomPaths");
            LoadData();
        }

        private void OnServerInitialized()
        {
            timer.Once(4f, () =>
            {
                getpath("");

                if (configData.settingsRing.RingRoadSettings[0].RingRoadEnabled)
                    spawnroad();
                Main = ConvertToButton(configData.settings.Button);
                RegisterPermissions();

                foreach (var custom in pcdData.pEntity.ToList())
                {
                    GetCustomPath(custom.Key);
                }
            });
        }

        void Unload()
        {
            foreach (var Brad in BradleySaveData)
            {
                if (Brad.Key != null && !Brad.Key.IsDestroyed)
                    Brad.Key.Kill();
            }

            foreach (var BradCustom in BradleyCustomSaveData)
            {
                if (BradCustom.Key != null && !BradCustom.Key.IsDestroyed)
                    BradCustom.Key.Kill();
            }

            foreach (var BradCustomRoad in BradleyCustomSaveDataRoad)
            {
                if (BradCustomRoad.Key != null && !BradCustomRoad.Key.IsDestroyed)
                    BradCustomRoad.Key.Kill();

            }
            BradleySaveData.Clear();
            BradleyCustomSaveData.Clear();
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (timers.ContainsKey(player.userID))
                    timers[player.userID].Destroy();
                if (timers.ContainsKey(player.userID))
                    timers.Remove(player.userID);
            }

            foreach (var insp in ControllerPlayer)
            {
                insp.OnDestroy();
            }
        }

        private object CanEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
			if (info == null || info.Initiator == null || !(info.Initiator is BradleyAPC)) return null;
			if (entity != null && entity is BuildingBlock && info.Initiator is BradleyAPC)
				return true;

            return null;
        }

        private object OnEntityTakeDamage(NPCPlayer player, HitInfo info)
        {
            if (info == null || info.Initiator == null || !(info.Initiator is BradleyAPC) || player == null) return null;
            BradleyAPC apc = info.Initiator as BradleyAPC;
            if (apc == null) return null;

            if (BradleySaveData.ContainsKey(apc) || BradleyCustomSaveData.ContainsKey(apc) || BradleyCustomSaveDataRoad.ContainsKey(apc))
            {
                info.damageTypes = new DamageTypeList();
                return true;
            }
            return null;
        }

        object CanBradleyApcTarget(BradleyAPC apc, BaseEntity entity)
        {
            if (BradleySaveData.ContainsKey(apc) || BradleyCustomSaveData.ContainsKey(apc) || BradleyCustomSaveDataRoad.ContainsKey(apc))
            {
                if (BradleyCustomSaveDataRoad.ContainsKey(apc))
                {
                    if (entity is BasePlayer && configData.settingsRing.RingRoadSettings[0].IgnoreSleepers && (entity as BasePlayer).IsSleeping())
                    {
                        return false;
                    }

                    if (!configData.settingsRing.RingRoadSettings[0].Can_Kill_NPC)
                        if (entity.IsNpc || entity is BaseNpc) return false;
                }
                if (BradleyCustomSaveData.ContainsKey(apc))
                {
                    if (pcdData.pEntity.ContainsKey(BradleyCustomSaveData[apc]) && !pcdData.pEntity[BradleyCustomSaveData[apc]].Can_Kill_Npc)
                        if (entity.IsNpc || entity is BaseNpc) return false;
                }

                else if (BradleySaveData.ContainsKey(apc) && !configData.settings.MonumentWithBradley[BradleySaveData[apc]].Can_Kill_NPC)
                    if (entity.IsNpc || entity is BaseNpc) return false;

                if (entity is BasePlayer)
                {
                    if ((entity as BasePlayer).IsSleeping() && sleepers.ContainsKey((entity as BasePlayer).userID) && sleepers[(entity as BasePlayer).userID] < DateTime.Now) return false;
                    if ((entity as BasePlayer).userID.IsSteamId())
                        return null;
                    else if (BradleySaveData.ContainsKey(apc) && !configData.settings.MonumentWithBradley[BradleySaveData[apc]].Can_Kill_NPC) return false;
                }
            }
            return null;
        }

        void OnEntityDeath(BradleyAPC bradly, HitInfo info)
        {
            if (BradleySaveData.ContainsKey(bradly))
            {
                MonumentName name = BradleySaveData[bradly];
                if (!configData.settings.MonumentWithBradley.ContainsKey(name))
                    return;

                int time = configData.settings.MonumentWithBradley[name].RespawnMinutes * 60;

                timer.Once(time, () =>
                {
                    getpath(name.ToString().ToLower());
                });
                BradleySaveData.Remove(bradly);
            }
            else if (BradleyCustomSaveDataRoad.ContainsKey(bradly))
            {
                int time1 = configData.settingsRing.RingRoadSettings[0].RespawnMin * 60;
                timer.Once(time1, () =>
                {
                    spawnroad();
                });
                BradleyCustomSaveDataRoad.Remove(bradly);
            }
            else if (BradleyCustomSaveData.ContainsKey(bradly))
            {
                string name1 = BradleyCustomSaveData[bradly];
                int time1 = pcdData.pEntity[name1].RespawnMinutes * 60;
                if (time1 <= 0) time1 = 30 * 60;
                timer.Once(time1, () =>
                {
                    GetCustomPath(name1);
                });
                BradleyCustomSaveData.Remove(bradly);
            }
        }

        #endregion

        #region Classes & Data
        public class bradleyMonumentInfo
        {
            public bool spawn = false;
            public float Health = 1000;
            public int RespawnMinutes = 30;
            public bool Can_Kill_NPC = false;
            public int maxCratesToSpawn = 3;
            public int ScientistSpawnCount = 3;
        }

        public class roadRing
        {
            public bool RingRoadEnabled;
            public int RespawnMin;
            public bool Can_Kill_NPC;
            public bool IgnoreSleepers;
            public float moveForceMax_250_To_2000;
            public float Health;
            public bool CanStop;
            public bool FireRocketAtPlayerBase;
            public bool DestroyBuildingBlocks;
            public string RocketPrefab;
            public int maxCratesToSpawn;
            public int ScientistSpawnCount = 3;
        }

        class PathEntity
        {
            public Dictionary<string, Paths> pEntity = new Dictionary<string, Paths>();
        }

        class Paths
        {
            public float startHealth;
            public int RespawnMinutes;
            public bool FollowPathBack;
            public bool Can_Kill_Npc;
            public int maxCratesToSpawn = 3;
            public int ScientistSpawnCount = 3;
            public Dictionary<int, pauseInfo> Path = new Dictionary<int, pauseInfo>();
        }

        class pauseInfo
        {
            public Vector3 pos;
            public int pause;
            public float speed;
        }

        void LoadData()
        {
            try
            {
                pcdData = Interface.Oxide.DataFileSystem.ReadObject<PathEntity>(Name + "/CustomPaths");
            }
            catch
            {
                PrintWarning("Couldn't load path data. Making new path data.");
                pcdData = new PathEntity();
            }
        }

        void SaveData(string filename = "ld")
        {
            PCDDATA.WriteObject(pcdData);
        }

        private void OnBradleyApcInitialize(BradleyAPC bradley)
        {
            if (BradleyCustomSaveData.ContainsKey(bradley))
            {
                //bradley._maxHealth = configs.options.startHealth;
                //bradley.health = bradley._maxHealth;
            }
        }
        #endregion

        #region Fucntions
        internal static Vector3[] FindPathRoad()
        {
            PathList list = null;
            Vector3[] points = null;

            foreach (var pathFind in TerrainMeta.Path.Roads)
            {
                if (pathFind.Path.GetStartPoint() == pathFind.Path.GetEndPoint())
                {
                    list = pathFind;
                    break;
                }
            }

            if (list != null)
            {
                points = list.Path.Points;
                if (points.Length > 0)
                {
                    return points;
                }
            }
            _instance.PrintWarning("RoadRing Path Not Found");
            return null;
        }

        internal static bool isPlayersBuilding(BaseEntity entity, ulong playerID)
        {
            BuildingPrivlidge theBlock = entity?.GetBuildingPrivilege();
            if (theBlock != null && theBlock.IsAuthed(playerID) || theBlock != null && theBlock.OwnerID == playerID)
                return true;
            return false;
        }

        public void spawnroad()
        {
            Vector3 position = Vector3.zero;
            BradleyAPC bradly = GameManager.server.CreateEntity(bradPrefab, position) as BradleyAPC;
            bradly.enableSaving = false;           
            bradly.maxCratesToSpawn = configData.settingsRing.RingRoadSettings[0].maxCratesToSpawn;
            bradly.ScientistSpawnCount = configData.settingsRing.RingRoadSettings[0].ScientistSpawnCount;
            BradlyAI Brad = bradly.gameObject.AddComponent<BradlyAI>();
            Brad.bradly = bradly;
            Brad.RoadRing = true;
            bradly.skinID = 8675309;
            bradly.Spawn();
            BradleyCustomSaveDataRoad.Add(bradly, "new");
            //Brad.Custom = true;
        }

        public void GetCustomPath(string name)
        {
            foreach (var custom in pcdData.pEntity.ToList())
            {
                List<Vector3> Path = new List<Vector3>();
                Path.Clear();
                if (custom.Key.ToLower() == name)
                {
                    if (pcdData.pEntity[custom.Key].Path.Count <= 0)
                    {
                        PrintError($"No path for bradley with datafile ({custom.Key})");
                        return;
                    }
                    Vector3 position = pcdData.pEntity[custom.Key].Path[0].pos;
                    BradleyAPC bradly = GameManager.server.CreateEntity(bradPrefab, position) as BradleyAPC;
                    bradly.enableSaving = false;
                    bradly.skinID = 8675309;
                    bradly.Spawn();

                    PrintWarning($"Spawned bradley at {custom.Key} {bradly.transform.position}");

                    bradly.maxCratesToSpawn = pcdData.pEntity[custom.Key].maxCratesToSpawn;
                    bradly.ScientistSpawnCount = pcdData.pEntity[custom.Key].ScientistSpawnCount;
                    bradly.ClearPath();
                    foreach (var customPath in pcdData.pEntity[custom.Key].Path.ToList())
                    {
                        Path.Add(customPath.Value.pos);
                    }
                    bradly.currentPath = Path;
                    bradly.pathLooping = true;
                    BradleyCustomSaveData.Add(bradly, name);
                    bradly.SendNetworkUpdateImmediate();
                    BradlyAI Brad = bradly.gameObject.AddComponent<BradlyAI>();
                    Brad.bradly = bradly;
                    Brad.Custom = true;
                    Brad.MonumentName = name;
                    float health = bradly._maxHealth;
                    if (pcdData.pEntity[custom.Key].startHealth != null && pcdData.pEntity[custom.Key].startHealth >= 1)
                    {
                        health = pcdData.pEntity[custom.Key].startHealth;
                        bradly._maxHealth = health;
                        bradly.health = bradly._maxHealth;
                    }
                }
            }

        }

        public void getpath(string name)
        {
            foreach (var monument in TerrainMeta.Path.Monuments)
            {
                BradleyAPC bradly = null;
                List<Vector3> Path = new List<Vector3>();
                Path.Clear();
                bool canSpawn = false;
                var monumentName = monument.GetMonumentName();
                float health = 0;
                Vector3 position = Vector3.zero;

                if (debug && configData.settings.MonumentWithBradley.ContainsKey(monumentName))
                    PrintWarning($"Is Enabled {monumentName.ToString()} = {configData.settings.MonumentWithBradley[monumentName].spawn}");
                
                if (!configData.settings.MonumentWithBradley.ContainsKey(monumentName) || !configData.settings.MonumentWithBradley[monumentName].spawn)
                    continue;

                if (string.IsNullOrEmpty(name))
                    canSpawn = true;
                else if (name == monumentName.ToString().ToLower())
                    canSpawn = true;

                if (canSpawn && monumentName.ToString().ToLower() == "airfield")
                {
                    position = monument.transform.TransformPoint(new Vector3(-86.86411f, 0.3000298f, 38.17391f));
                }
                else if (canSpawn && monumentName.ToString().ToLower() == "watertreatment")
                {
                    position = monument.transform.TransformPoint(new Vector3(96.74454f, 0.1126404f, 31.32247f));
                }
                else if (canSpawn && monumentName.ToString().ToLower() == "trainyard")
                {
                    position = monument.transform.TransformPoint(new Vector3(66.8533f, 0.1389904f, -2.566289f));
                }
                else if (canSpawn && monumentName.ToString().ToLower() == "junkyard")
                {
                    position = monument.transform.TransformPoint(new Vector3(31.64271f, 0.119297f, 55.50159f));
                }
                else if (canSpawn && monumentName.ToString().ToLower() == "dome")
                {
                    position = monument.transform.TransformPoint(new Vector3(-44.06296f, 5.761227f, -12.12094f));
                }
                else if (canSpawn && monumentName.ToString().ToLower() == "powerplant")
                {
                    position = monument.transform.TransformPoint(new Vector3(18.3474f, 0.1845f, 67.7239f));
                }
                else if (canSpawn && monumentName.ToString().ToLower() == "harbor_b")
                {
                    position = monument.transform.TransformPoint(new Vector3(22.69171f, 3.886088f, -84.9832f));
                }
                else if (canSpawn && monumentName.ToString().ToLower() == "harbor_a")
                {
                    position = monument.transform.TransformPoint(new Vector3(100.5f, 5.1f, -21.75f));
                }

                if (position != Vector3.zero)
                {
                    bradly = GameManager.server.CreateEntity(bradPrefab, position) as BradleyAPC;
                    bradly.enableSaving = false;
                    bradly.skinID = 8675309;
                    bradly.Spawn();
                    PrintWarning($"Spawned bradley at {monumentName} {position}");

                    bradly.maxCratesToSpawn = configData.settings.MonumentWithBradley[monumentName].maxCratesToSpawn;
                    bradly.ScientistSpawnCount = configData.settings.MonumentWithBradley[monumentName].ScientistSpawnCount;
                    BradleySaveData.Add(bradly, monumentName);
                    bradly.ClearPath();
                    bradly.currentPath = Path;
                    bradly.pathLooping = true;
                    bradly.SendNetworkUpdateImmediate();
                    BradlyAI Brad = bradly.gameObject.AddComponent<BradlyAI>();
                    Brad.bradly = bradly;
                    Brad.monument = monument;
                    Brad.MonumentName = monumentName.ToString().ToLower();

                    if (configData.settings.MonumentWithBradley.ContainsKey(monumentName) && configData.settings.MonumentWithBradley[monumentName].Health > 0)
                    {
                        bradly._maxHealth = configData.settings.MonumentWithBradley[monumentName].Health;
                        bradly.health = bradly._maxHealth;
                    }
                }

                
            }
        }

        #endregion

        #region Chat Commands
        [ChatCommand("bradley")]
        private void thePath(BasePlayer player, string cmd, string[] args)
        {

            if (!permission.UserHasPermission(player.userID.ToString(), theAdmin))
            {
                SendReply(player, lang.GetMessage("nope", this, player.UserIDString));
                return;
            }
            if (args.Length < 1)
            {
                SendReply(player, string.Format(lang.GetMessage("usagecommand", this, player.UserIDString)));
                return;
            }

            switch (args[0].ToLower())
            {
                case "activate":

                    PathMaker Checking = player.GetComponent<PathMaker>();
                    if (Checking == null)
                    {
                        PathMaker maker = player.gameObject.AddComponent<PathMaker>();
                        ControllerPlayer.Add(maker);
                        SendReply(player, string.Format(lang.GetMessage("Active", this, player.UserIDString)));
                    }
                    else SendReply(player, string.Format(lang.GetMessage("IsActive", this, player.UserIDString)));

                    return;

                case "new":

                    if (args.Length < 2)
                    {
                        SendReply(player, string.Format(lang.GetMessage("Newusage", this, player.UserIDString)));
                        return;
                    }

                    PathMaker Controller = player.GetComponent<PathMaker>();
                    if (Controller == null)
                    {
                        SendReply(player, string.Format(lang.GetMessage("NotActive", this, player.UserIDString)));
                        return;
                    }

                    if (!pcdData.pEntity.ContainsKey(args[1].ToLower()))
                    {
                        pcdData.pEntity.Add(args[1].ToLower(), new Paths());
                        SaveData();
                        Controller.MonumentName = args[1].ToLower();
                        Controller.active = true;
                        SendReply(player, string.Format(lang.GetMessage("New", this, player.UserIDString)));
                    }
                    else
                    {
                        SendReply(player, string.Format(lang.GetMessage("isthere", this, player.UserIDString)));
                        return;
                    }

                    return;

                case "deactivate":

                    PathMaker deactive = player.GetComponent<PathMaker>();
                    if (deactive != null)
                    {
                        deactive.active = false;
                        deactive.OnDestroy();
                        ControllerPlayer.Remove(deactive);
                        SendReply(player, string.Format(lang.GetMessage("deactive", this, player.UserIDString)));


                    }

                    return;

                case "edit":

                    if (args.Length < 2)
                    {
                        SendReply(player, string.Format(lang.GetMessage("editusage", this, player.UserIDString)));
                        return;
                    }

                    PathMaker Controlleredit = player.GetComponent<PathMaker>();
                    if (Controlleredit == null)
                    {
                        SendReply(player, string.Format(lang.GetMessage("NotActive", this, player.UserIDString)));
                        return;
                    }

                    if (pcdData.pEntity.ContainsKey(args[1].ToLower()))
                    {
                        Controlleredit.MonumentName = args[1].ToLower();
                        Controlleredit.update = true;
                        Controlleredit.active = true;
                        SendReply(player, string.Format(lang.GetMessage("Editing", this, player.UserIDString), args[1].ToLower()));
                    }
                    else
                    {
                        SendReply(player, string.Format(lang.GetMessage("isnotthere", this, player.UserIDString), args[1].ToLower()));
                        return;
                    }

                    return;

                case "move":

                    if (args.Length < 3)
                    {
                        SendReply(player, string.Format(lang.GetMessage("moveusage", this, player.UserIDString)));
                        return;
                    }

                    PathMaker ControllerMove = player.GetComponent<PathMaker>();
                    if (ControllerMove == null)
                    {
                        SendReply(player, string.Format(lang.GetMessage("NotActive", this, player.UserIDString)));
                        return;
                    }
                    var ids = default(int);
                    if (!int.TryParse(args[2], out ids))
                    {
                        SendReply(player, string.Format(lang.GetMessage("moveusage", this, player.UserIDString)));
                        return;
                    }

                    if (pcdData.pEntity.ContainsKey(args[1].ToLower()))
                    {
                        if (pcdData.pEntity[args[1].ToLower()].Path.ContainsKey(ids))
                        {
                            pcdData.pEntity[args[1].ToLower()].Path[ids].pos = player.transform.position;
                            SaveData();
                            SendReply(player, string.Format(lang.GetMessage("PathMoved", this, player.UserIDString)));
                            return;
                        }

                        SendReply(player, string.Format(lang.GetMessage("NoPath", this, player.UserIDString)));
                    }
                    else
                    {
                        SendReply(player, string.Format(lang.GetMessage("NoPathname", this, player.UserIDString)));
                        return;
                    }

                    return;

                case "delete":

                    if (args.Length < 2)
                    {
                        SendReply(player, string.Format(lang.GetMessage("deleteusage", this, player.UserIDString)));
                        return;
                    }
                    if (pcdData.pEntity.ContainsKey(args[1].ToLower()))
                    {
                        pcdData.pEntity.Remove(args[1].ToLower());
                        SaveData();
                        foreach (var BradCustom in BradleyCustomSaveData.ToList())
                        {
                            if (BradCustom.Value == args[1].ToLower())
                            {
                                BradleyCustomSaveData.Remove(BradCustom.Key);
                                BradCustom.Key?.Kill();
                            }
                        }

                        SendReply(player, string.Format(lang.GetMessage("delete", this, player.UserIDString), args[1].ToLower()));
                    }
                    else SendReply(player, string.Format(lang.GetMessage("NoPathname", this, player.UserIDString)));

                    return;

                case "health":

                    if (args.Length < 3)
                    {
                        SendReply(player, string.Format(lang.GetMessage("healthusage", this, player.UserIDString)));
                        return;
                    }

                    if (pcdData.pEntity.ContainsKey(args[1].ToLower()))
                    {
                        var health = default(float);
                        if (!float.TryParse(args[2], out health))
                        {
                            SendReply(player, string.Format(lang.GetMessage("healthusage", this, player.UserIDString)));
                            return;
                        }
                        float oldhealth = 0;
                        if (pcdData.pEntity[args[1].ToLower()].startHealth != null)
                            oldhealth = pcdData.pEntity[args[1].ToLower()].startHealth;

                        pcdData.pEntity[args[1].ToLower()].startHealth = health;
                        SendReply(player, string.Format(lang.GetMessage("newHealth", this, player.UserIDString), oldhealth, health.ToString()));
                        SaveData();
                        return;
                    }
                    else
                    {
                        SendReply(player, string.Format(lang.GetMessage("isnotthere", this, player.UserIDString), args[1].ToLower()));
                        return;
                    }

                    SendReply(player, string.Format(lang.GetMessage("healthusage", this, player.UserIDString)));
                    return;

                default:
                    break;

            }
        }
        #endregion

        #region Path Maker Controller
        public class PathMaker : MonoBehaviour
        {
            public BasePlayer player;
            public string MonumentName = "";
            public bool active;
            public float nextPressTime;
            public int total;
            public bool update;
            public Vector3 stop = Vector3.zero;
            public float speedSet = 650.0f;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
            }

            public void OnDestroy()
            {
                if (_instance.timers.ContainsKey(player.userID))
                    _instance.timers[player.userID].Destroy();
                if (_instance.timers.ContainsKey(player.userID))
                    _instance.timers.Remove(player.userID);

                if (_instance.pcdData.pEntity.ContainsKey(MonumentName))
                {
                    if (_instance.pcdData.pEntity[MonumentName].Path.Count <= 0)
                        _instance.pcdData.pEntity.Remove(MonumentName);
                    _instance.SaveData();
                }
                Destroy(this);
            }

            private void Update()
            {
                if (MonumentName == "" || !_instance.pcdData.pEntity.ContainsKey(MonumentName)) return;
                if (update)
                {
                    update = false;
                    total = _instance.pcdData.pEntity[MonumentName].Path.Count + 1;
                }
                if (player == null || !player.IsConnected)
                {
                    Destroy(this);
                    return;
                }
                if (!active) return;
                if (player.serverInput.WasJustPressed(BUTTON.RELOAD))
                {
                    float time = Time.realtimeSinceStartup;
                    if (nextPressTime < time)
                    {
                        nextPressTime = time + 0.2f;
                        int newpos = total - 1;
                        if (_instance.pcdData.pEntity[MonumentName].Path.ContainsKey(newpos))
                        {
                            if (_instance.pcdData.pEntity[MonumentName].Path[newpos].speed >= 2000f)
                            {
                                speedSet = 650.0f;
                                _instance.pcdData.pEntity[MonumentName].Path[newpos].speed = speedSet;
                            }
                            else
                            {
                                _instance.pcdData.pEntity[MonumentName].Path[newpos].speed += 50.0f;
                                speedSet = _instance.pcdData.pEntity[MonumentName].Path[newpos].speed;
                            }

                            _instance.SendReply(player, _instance.lang.GetMessage("SpeedAddTime", _instance, player.UserIDString), newpos.ToString(), _instance.pcdData.pEntity[MonumentName].Path[newpos].speed);

                        }
                    }
                }

                if (player.serverInput.WasJustPressed(_instance.Main))
                {
                    float time = Time.realtimeSinceStartup;
                    if (nextPressTime < time)
                    {
                        nextPressTime = time + 0.2f;
                        if (Vector3.Distance(stop, player.transform.position) <= 0.01)
                        {
                            int newpos = total - 1;
                            if (_instance.pcdData.pEntity[MonumentName].Path.ContainsKey(newpos))
                            {
                                if (_instance.pcdData.pEntity[MonumentName].Path[newpos].pause >= 60)
                                {
                                    _instance.pcdData.pEntity[MonumentName].Path[newpos].pause = 0;
                                }
                                else _instance.pcdData.pEntity[MonumentName].Path[newpos].pause += 10;
                                _instance.SendReply(player, _instance.lang.GetMessage("PauseAddTime", _instance, player.UserIDString), newpos.ToString(), _instance.pcdData.pEntity[MonumentName].Path[newpos].pause);
                            }
                        }
                        else
                        {
                            stop = player.transform.position;
                            _instance.pcdData.pEntity[MonumentName].Path.Add(total, new pauseInfo());
                            _instance.pcdData.pEntity[MonumentName].Path[total].pos = player.transform.position;
                            _instance.pcdData.pEntity[MonumentName].Path[total].speed = speedSet;
                            _instance.SendReply(player, _instance.lang.GetMessage("PathAddNew", _instance, player.UserIDString), total.ToString(), speedSet.ToString());
                            total++;

                        }
                        _instance.SaveData();
                    }

                }

                if (_instance.timers.ContainsKey(player.userID)) return;
                if (!_instance.pcdData.pEntity.ContainsKey(MonumentName)) return;
                if (_instance.pcdData.pEntity[MonumentName].Path.Count <= 0) return;

                _instance.timers[player.userID] = _instance.timer.Every(2.0f, () =>
                {
                    foreach (var output in _instance.pcdData.pEntity[MonumentName].Path.ToList())
                    {
                        DrawUi("<size=40>" + output.Key.ToString() + "</size> Speed " + output.Value.speed.ToString(), output.Value.pos);
                    }
                });
            }

            void DrawUi(string format, Vector3 pos)
            {
                if (player == null || player.IsDead() || player.IsSleeping() || !player.IsConnected) return;
                if (Vector3.Distance(player.transform.position, pos) > 100) return;
                if (player.IsAdmin)
                {
                    player.SendConsoleCommand("ddraw.text", 2.0f, "#ffffff", pos + new Vector3(0, 0.5f, 0), format);
                }
                else
                {
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                    player.SendNetworkUpdateImmediate();
                    player.SendConsoleCommand("ddraw.text", 2.0f, "#ffffff", pos + new Vector3(0, 0.5f, 0), format);
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                    player.SendNetworkUpdateImmediate();
                }
            }
        }
        #endregion

        #region BradlyAI Controller
        public class BradlyAI : MonoBehaviour
        {
            public BradleyAPC bradly;
            public BasePlayer player;
            public MonumentInfo monument;
            public string MonumentName;
            public List<Vector3> Path = new List<Vector3>();
            private Coroutine QueuedRoutine;
            public bool stopped;
            public float lastSeenTime = 0f;
            public int currentPathIndex;
            public bool Custom;
            public bool RoadRing;
            public DateTime Active;
            private Vector3 aimVector = Vector3.forward;
            private Vector3 aimVectorTop = Vector3.forward;
            private int oldpath = 0;
            private bool IsAiming;
            private bool reverse;
            private bool isPausePos;
            private DateTime pauseTime = DateTime.Now;
            private DateTime NextpauseTime = DateTime.Now;
            private int pauseIndex;
            private bool okGo;
            private float moveForce = 650.0f;
            private float rocketTime { get; set; }
            private Dictionary<int, float> speed = new Dictionary<int, float>();
            private Dictionary<int, int> stopPause = new Dictionary<int, int>();
            private bool trainInTheWay { get; set; }
            private TrainCar trainCar { get; set; }
            private MagnetCrane magnetcrane { get; set; }
            public int ScientistSpawnCount { get; set; }
            private HitInfo hitinfoTrain { get; set; }
            private HitInfo hitinfoOther { get; set; }

            private void Awake()
            {
                bradly = GetComponent<BradleyAPC>();
                Active = DateTime.Now.AddSeconds(2);
                hitinfoTrain = new HitInfo();
                hitinfoTrain.damageTypes.Add(Rust.DamageType.Explosion, 0.001f);
                hitinfoOther = new HitInfo();
                hitinfoOther.damageTypes.Add(Rust.DamageType.Explosion, 350f);
                bradly.ScientistSpawnCount = ScientistSpawnCount;

                _instance.NextTick(() =>
                {
                    if (_instance == null)
                    {
                        return;
                    }
                    if (monument != null && _instance.configData.settings.MonumentWithBradley.ContainsKey(monument.GetMonumentName()) && _instance.configData.settings.MonumentWithBradley[monument.GetMonumentName()].Health > 0)
                    {
                        bradly._maxHealth = _instance.configData.settings.MonumentWithBradley[monument.GetMonumentName()].Health;
                        bradly.ScientistSpawnCount = _instance.configData.settings.MonumentWithBradley[monument.GetMonumentName()].ScientistSpawnCount;
                        bradly.setNumberOfScientistsToSpawn(_instance.configData.settings.MonumentWithBradley[monument.GetMonumentName()].ScientistSpawnCount);
                        bradly.health = bradly._maxHealth;
                        bradly.SendNetworkUpdateImmediate();
                        if (bradly.ScientistSpawns.Count > _instance.configData.settings.MonumentWithBradley[monument.GetMonumentName()].ScientistSpawnCount)
                        {
                            for (int i = 0; i < 20; i++)
                            {
                                if (bradly.ScientistSpawns.Count != 0 && bradly.ScientistSpawns.Count != _instance.configData.settings.MonumentWithBradley[monument.GetMonumentName()].ScientistSpawnCount)
                                    bradly.ScientistSpawns.RemoveAt((int)UnityEngine.Random.Range(0, bradly.ScientistSpawns.Count - 1));
                            }
                        }
                    }
                    bool usGood = MakePath(RoadRing);

                    if (RoadRing)
                    {
                        bradly.ScientistSpawnCount = _instance.configData.settingsRing.RingRoadSettings[0].ScientistSpawnCount;
                        bradly.setNumberOfScientistsToSpawn(_instance.configData.settingsRing.RingRoadSettings[0].ScientistSpawnCount);
                        if (usGood)
                            _instance.PrintWarning($"Spawned Bradley on ring road {bradly.transform.position}");

                        if (bradly.ScientistSpawns.Count > _instance.configData.settingsRing.RingRoadSettings[0].ScientistSpawnCount)
                        {
                            for (int i = 0; i < 20; i++)
                            {
                                if (bradly.ScientistSpawns.Count != 0 && bradly.ScientistSpawns.Count != _instance.configData.settingsRing.RingRoadSettings[0].ScientistSpawnCount)
                                    bradly.ScientistSpawns.RemoveAt((int)UnityEngine.Random.Range(0, bradly.ScientistSpawns.Count - 1));
                            }
                        }
                    }
                });
                moveForce = 650f;
                bradly.moveForceMax = moveForce;
                rocketTime = Time.time + 10; 
            }

            void Relocate()
            {
                if (isPausePos)
                {
                    if (pauseTime <= DateTime.Now)
                    {
                        NextpauseTime = DateTime.Now.AddSeconds(2);
                        isPausePos = false;
                    }
                }
                else if (bradly.targetList.Count <= 0 && !isPausePos)
                {
                    aimWeaponAt();
                    StartMoving();
                    CancelInvoke("Relocate");
                    return;
                }

                else if (!isPausePos && bradly.targetList.Count >= 1)
                {
                    foreach (var target in bradly.targetList.ToList())
                    {
                        if (target.entity is BasePlayer && (target.entity as BasePlayer).IsSleeping() && !_instance.sleepers.ContainsKey((target.entity as BasePlayer).userID)) _instance.sleepers.Add((target.entity as BasePlayer).userID, DateTime.Now.AddSeconds(10));

                        if (UnityEngine.Time.time > target.lastSeenTime + 10)
                        {
                            bradly.targetList.Remove(target);
                        }
                    }
                    if (_instance.sleepers.Count > 0)
                        foreach (var sleeper in _instance.sleepers.ToList())
                        {
                            if (sleeper.Value.AddSeconds(180) < DateTime.Now)
                                _instance.sleepers.Remove(sleeper.Key);
                        }
                }
            }

            private int isStuck { get; set; }
            private float nextStuckTime { get; set; }
            private int stuckNode { get; set; }
            private float nextStuckjump = 0;

            private bool IsStuck()
            {
                
                nextStuckTime = Time.time + 1f;
                if (stopped || trainInTheWay) return false;

                if (stuckNode != bradly.currentPathIndex)
                {
                    isStuck = 0;
                }

                TimeSince timeReverse = bradly.GetTimeSinceStuckReverseStart();

                if ((double)(float)timeReverse < 3.0)
                {
                    stuckNode = bradly.currentPathIndex;
                    isStuck++;
                }

                if (isStuck >= 5 && nextStuckjump < Time.time)
                {
                    isStuck = 0;
                    nextStuckjump = Time.time + 5f;
                    stuckNode = -1;
                    int jumpPosition = bradly.currentPathIndex + 1;
                    if (jumpPosition > Path.Count - 1)
                        jumpPosition = 0;
                    bradly.currentPathIndex = jumpPosition;
                    bradly.transform.position = Path[jumpPosition];                  
                    return true;                  
                }
                return false;
            }

            public static bool IsOdd(int value)
            {
                return value % 2 != 0;
            }

            private bool MakePath(bool start = false)
            {
                Path.Clear();
                if (RoadRing)
                {
                    int number = 0;
                    Vector3[] newPath = FindPathRoad();
                    if (newPath == null || newPath.Length <= 0)
                    {
                        _instance.PrintWarning("No path for bradley road ring.");
                        bradly?.Kill();
                        return false;
                    }
                    foreach (Vector3 pos in newPath)
                    {
                        if (IsOdd(number))
                            Path.Add(pos);
                        number++;
                    }
                    if (start)
                    {
                        bradly.transform.position = Path[0];
                        bradly._maxHealth = _instance.configData.settingsRing.RingRoadSettings[0].Health;
                        bradly.health = bradly._maxHealth;
                        bradly.SendNetworkUpdateImmediate();
                    }
                    moveForce = _instance.configData.settingsRing.RingRoadSettings[0].moveForceMax_250_To_2000;
                    bradly.moveForceMax = moveForce;
                }
                else if (!Custom)
                {
                    var monumentName = monument.GetMonumentName();
                    if (MonumentName.ToLower() == "airfield")
                    {
                        Path.Add(monument.transform.TransformPoint(new Vector3(-86.86411f, 0.3000298f, 38.17391f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-89.63014f, 0.232235f, 18.8835f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-136.7101f, 0.300005f, -9.763386f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-95.21716f, 0.309103f, -8.810207f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-70.59235f, 0.3139439f, -39.47253f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-44.59235f, 0.3139439f, -39.47253f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-36.59235f, 0.3139439f, -57.73223f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-02.59235f, 0.3139439f, -57.73223f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(6.59235f, 0.3139439f, -39.47253f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(16.50055f, 0.3000298f, -39.47253f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(145.2082f, 0.3000278f, -39.47253f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(136.1699f, 0.3000011f, -8.187582f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(45.1699f, 0.3000011f, -8.187582f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(45.1699f, 0.3000011f, -8.187582f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(7.1699f, 0.3000011f, -8.187582f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(7.1699f, 0.3000011f, -8.187582f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-30.1699f, 0.3000011f, -8.187582f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-83.80389f, 0.3000183f, -7.108672f)));

                        if (stopPause.Count <= 0)
                        {
                            stopPause.Add(6, 10);
                            stopPause.Add(7, 10);
                            stopPause.Add(12, 10);
                            stopPause.Add(14, 10);
                            stopPause.Add(16, 10);
                        }

                        if (speed.Count <= 0)
                        {
                            speed.Add(3, 1500);
                            speed.Add(4, moveForce);
                            speed.Add(5, 1500);
                            speed.Add(6, moveForce);
                            speed.Add(7, 1500);
                            speed.Add(8, moveForce);
                            speed.Add(13, 1500);
                            speed.Add(14, moveForce);
                            speed.Add(15, 1500);
                            speed.Add(16, moveForce);
                        }
                    }
                    else if (MonumentName.ToLower() == "watertreatment")
                    {
                        Path.Add(monument.transform.TransformPoint(new Vector3(94.29491f, 0.1128616f, 31.32852f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(59.64446f, 0.1277847f, 30.30154f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(56.24982f, 0.1277924f, -10.11833f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(48.02157f, 0.3232574f, -5.359991f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(29.88624f, 0.3232574f, -5.401128f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(16.17002f, 0.1277924f, -14.53429f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(15.51263f, 0.1725616f, -98.54493f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(14.13537f, 0.131218f, -103.6932f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(14.25436f, 0.1428223f, -125.0455f)));
                        // new run
                        Path.Add(monument.transform.TransformPoint(new Vector3(17.48942f, 0.1553135f, -127.6867f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(17.58115f, 0.1319599f, -135.9689f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(16.07823f, 0.1386414f, -139.0313f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(16.13775f, 0.1387196f, -149.4363f))); // stop
                        Path.Add(monument.transform.TransformPoint(new Vector3(15.89532f, 0.1387672f, -140.2674f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(16.92456f, 0.1389065f, -138.7454f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(17.26203f, 0.1633282f, -125.6138f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(12.80357f, 0.1161442f, -124.4518f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-15.16409f, 0.2930737f, -124.7532f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-23.35952f, 0.3228512f, -133.7242f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-23.71822f, 0.3228512f, -137.2873f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-24.12018f, 0.230772f, -148.1148f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-27.14117f, 0.1367722f, -153.928f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-28.73289f, 0.1808319f, -155.9729f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-28.64502f, 0.1783485f, -150.3157f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-50.82085f, 0.2452316f, -150.588f))); // stop
                        Path.Add(monument.transform.TransformPoint(new Vector3(-28.78485f, 0.1734924f, -151.1725f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-24.818f, 0.1062603f, -148.7883f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-24.1909f, 0.3209896f, -130.5209f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-28.58832f, 0.1382751f, -126.4554f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-29.1837f, 0.1364422f, -124.9082f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-28.95499f, 0.1374493f, -112.0597f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-23.63842f, 0.3219528f, -108.2334f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-21.19233f, 0.3233871f, -106.9218f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-21.42602f, 0.3234196f, -99.87251f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-28.33028f, 0.179081f, -91.65036f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-27.05217f, 0.1277695f, -51.71019f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-30.70649f, 0.2450867f, -44.54843f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-27.38387f, 0.2450867f, -12.36733f))); //stop
                        Path.Add(monument.transform.TransformPoint(new Vector3(-28.05143f, 0.2450867f, -31.84668f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-28.75095f, 0.2450867f, -38.19529f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-23.99733f, 0.2450867f, -40.62247f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-13.95524f, 0.2450867f, -40.16006f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-8.651077f, 0.2450867f, -42.85468f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-5.075838f, 0.2450867f, -42.55705f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(2.948938f, 0.1774673f, -52.35186f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(14.87062f, 0.1354713f, -52.56411f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(15.83446f, 0.1339912f, -80.81995f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-15.94862f, 0.2911549f, -80.81995f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-22.03658f, 0.2292805f, -84.72041f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-23.7125f, 0.09775543f, -85.63631f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-23.9451f, 0.3169003f, -99.03419f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-19.68123f, 0.2866096f, -105.5159f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-16.02742f, 0.2866096f, -109.7258f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-8.19521f, 0.245121f, -111.4237f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-7.104496f, 0.2450905f, -113.8903f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-6.842197f, 0.291832f, -124.7866f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(15.83446f, 0.1589012f, -124.7866f))); //stop
                        // back to beginning run
                        Path.Add(monument.transform.TransformPoint(new Vector3(15.83446f, 0.1354904f, -51.28057f))); //stop
                        Path.Add(monument.transform.TransformPoint(new Vector3(18.23803f, 0.1435318f, -11.47866f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(28.99718f, 0.32341f, -5.803827f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(36.89473f, 0.32341f, -5.317663f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(45.9107f, 0.32341f, -5.478407f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(55.67981f, 0.138588f, -9.346466f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(60.17113f, 0.1277924f, 31.09496f)));
                        if (stopPause.Count <= 0)
                        {
                            stopPause.Add(12, 5);
                            stopPause.Add(24, 5);
                            stopPause.Add(37, 5);
                            stopPause.Add(56, 5);
                            stopPause.Add(57, 10);
                        }
                    }
                    else if (MonumentName.ToLower() == "trainyard")
                    {
                        Path.Add(monument.transform.TransformPoint(new Vector3(63.5246f, 0.1390724f, -2.361706f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(56.42427f, 0.5505486f, -2.610112f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(49.06216f, 0.1278152f, -2.730916f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(36.04762f, 0.1277733f, -14.61397f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(22.71599f, 0.4301624f, -14.65888f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(12.71599f, 0.4301624f, -14.65888f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(12.71599f, 0.4301624f, -22.75f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(12.71599f, 0.4301624f, 18.65888f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(12.71599f, 0.4301624f, -14.65888f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(22.17083f, 0.4301834f, -18.29986f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(22.071f, 0.2607594f, -74.77444f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-6.612503f, 0.07893753f, -74.26021f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-26.75501f, 0.6402607f, -80.75694f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-38.51782f, 4.229902f, -70.49458f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-38.7187f, 9.010956f, -50.28886f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-22.02893f, 8.849794f, -44.6155f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-37.12535f, 8.552464f, -59.16274f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-41.50461f, 3.016088f, -77.94614f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-90.57095f, 0.1897659f, -82.46899f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-92.10056f, 0.2049656f, -78.96725f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-95.58481f, 0.1891155f, -61.59507f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-95.72913f, 0.1374187f, 25.48425f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-80.54308f, 0.2582016f, 30.88243f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-70.39217f, 0.268959f, 30.91283f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-67.64632f, 0.3241653f, 23.03827f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-42.10621f, 0.1277714f, 22.77687f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-44.33346f, 0.1354752f, -12.30531f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-44.875f, 0.1354733f, -12.78514f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-38.44003f, 0.1354733f, -14.60317f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-26.39468f, 0.5508099f, -14.41892f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-22.00624f, 0.3346806f, -13.28942f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-7.971876f, 0.4302311f, -13.13248f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-7.803906f, 0.4349918f, -11.78872f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-7.665912f, 0.2566185f, 18.28899f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-7.639902f, 0.2566204f, 19.79658f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-7.571397f, 0.4325447f, -11.40362f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-10.86426f, 0.3731861f, -12.52769f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-11.19731f, 0.3731842f, -21.71595f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-13.17627f, 0.2508755f, -24.58857f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-11.75299f, 0.3715134f, -40.08435f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-7.949722f, 0.2644825f, -45.03418f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-8.406029f, 0.1107674f, -74.54991f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(22.071f, 0.2607594f, -74.77444f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(76.471f, 0.2607594f, -74.77444f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(80.34207f, 0.1386051f, -62.47713f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(80.34207f, 0.1386051f, -32.47713f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(75.48219f, 0.1386051f, -30.47713f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(75.48219f, 0.1531754f, -16.59138f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(75.48219f, 0.1531754f, -10.59138f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(70.48219f, 0.1531754f, -6.59138f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(65.5246f, 0.1390724f, -2.361706f)));
                        if (stopPause.Count <= 0)
                        {
                            stopPause.Add(5, 10);
                            stopPause.Add(7, 10);
                            stopPause.Add(15, 10);
                            stopPause.Add(33, 10);
                            stopPause.Add(44, 10);
                        }
                    }
                    else if (MonumentName.ToLower() == "powerplant")
                    {
                        Path.Add(monument.transform.TransformPoint(new Vector3(18.3474f, 0.1845f, 67.7239f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(18.3474f, 0.1379f, 40.8174f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(67.4247f, 0.1841f, 40.7106f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(4.3920f, 0.1386f, 40.7106f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(1.0467f, 0.1387f, 0.0888f)));
                        // new turns
                        Path.Add(monument.transform.TransformPoint(new Vector3(1.0467f, 0.1387f, -40.6115f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-3.486853f, 0.1437454f, -46.22466f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(1.4433f, 0.1439438f, -58.2209f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(4.888628f, 0.1386147f, -61.37122f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(4.818565f, 0.1386185f, -66.17683f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(3.268604f, 0.1386185f, -69.41037f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(3.188866f, 0.1478806f, -93.83349f))); // stop
                        Path.Add(monument.transform.TransformPoint(new Vector3(3.268604f, 0.1386185f, -69.41037f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(4.818565f, 0.1386185f, -66.17683f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(4.888628f, 0.1386147f, -61.37122f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(2.306936f, 0.1388206f, -58.06636f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(0.1141834f, 0.1416016f, -51.98983f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-5.912672f, 0.1420479f, -44.5731f))); // stop
                        Path.Add(monument.transform.TransformPoint(new Vector3(-63.45444f, 0.4307175f, -44.5731f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-67.69406f, 0.1487732f, -48.21448f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-96.1015f, 0.1400871f, -48.37261f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-101.4971f, 0.1410484f, -55.39466f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-101.9085f, 0.1695023f, -87.38583f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-93.16472f, 0.2691994f, -91.74126f))); // stop
                        Path.Add(monument.transform.TransformPoint(new Vector3(-86.16797f, 0.291317f, -96.1535f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-70.84471f, 0.2453156f, -95.97448f)));
                        //train track run
                        Path.Add(monument.transform.TransformPoint(new Vector3(-67.88322f, 0.2785683f, -91.99197f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-63.5f, 0.2785683f, -75.57696f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-62.5f, 0.2891006f, -71.01109f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-62.5f, 0.2861099f, -32.41165f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-69.40817f, 0.2822723f, -24.51225f))); // stop
                        Path.Add(monument.transform.TransformPoint(new Vector3(-70.3849f, 0.02630234f, -21.54729f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-67.93692f, -0.005096436f, -16.0f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-65.0f, 0.2841568f, -13.47328f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-65.0f, 0.2841568f, -12.63905f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-65.0f, 0.289772f, 12.21361f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-65.0f, 0.2921486f, 63.87698f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-61.65328f, 0.2816162f, 79.35797f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-56.89554f, 0.2819405f, 86.6915f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-44.58654f, 0.2890816f, 92.63689f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-32.74379f, 0.2893257f, 99.9867f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-17.30183f, 0.2893257f, 105.0f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-13.5f, 0.2893257f, 105.5f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(30.5f, 0.2914619f, 105.5f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(50.9375f, 0.2914619f, 97.90883f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(66.14213f, 0.2914619f, 80.71275f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(70.75f, 0.2914619f, 63.87612f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(70.75f, 0.4217339f, 39.54501f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(70.75f, 0.3008728f, -23.25602f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(70.75f, 0.4081383f, 41.53177f)));
                        // end track run
                        Path.Add(monument.transform.TransformPoint(new Vector3(18.3474f, 0.1379f, 40.8174f))); // stop
                        //end new turns
                        if (stopPause.Count <= 0)
                        {
                            stopPause.Add(11, 5);
                            stopPause.Add(17, 10);
                            stopPause.Add(23, 5);
                            stopPause.Add(30, 10);
                            stopPause.Add(50, 5);
                        }
                    }
                    else if (MonumentName.ToLower() == "junkyard")
                    {
                        Path.Add(monument.transform.TransformPoint(new Vector3(31.64271f, 0.119297f, 55.50159f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(30.1172f, 0.1151161f, 43.21851f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(25.58159f, 0.1340065f, 28.51339f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(35.29071f, 0.1052132f, 13.11929f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(31.27332f, 0.105154f, 3.809889f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(5.171313f, 0.1437817f, -8.949705f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-12.49451f, 0.1359272f, -8.788202f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-21.68601f, 0.1078663f, 1.292976f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-35.50058f, 0.1359749f, 5.063232f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-48.99272f, 0.1249943f, 8.290569f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-70.85337f, 0.142767f, 7.698631f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-74.03624f, 0.142767f, -8.442984f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-68.34813f, 0.142767f, 9.776157f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-66.93368f, 0.1088715f, 27.51042f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-69.58651f, 0.1100197f, 38.80193f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-67.13332f, 0.1198921f, 27.3404f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-68.19159f, 0.1249943f, 11.59162f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-48.56188f, 0.1341705f, 8.602455f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-35.6937f, 0.1250458f, 5.305647f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-21.67464f, 0.1221581f, 2.937718f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(11.81205f, 0.1249943f, 17.17112f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(24.80643f, 0.03949165f, 22.38815f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(37.58057f, 0.1080112f, 14.21169f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(43.43172f, 0.1325397f, 16.00401f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(49.61998f, 0.136549f, 19.51055f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(58.41704f, 0.3128242f, 9.551607f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(65.72428f, 0.1110191f, -2.887486f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(70.65662f, 0.1447468f, -26.57395f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(60.33736f, 0.1057472f, -39.18386f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(49.28505f, 0.1183395f, -53.45815f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(34.67465f, 0.1068268f, -61.51337f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(50.05286f, 0.1156616f, -52.80519f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(65.3894f, 0.1193409f, -34.05927f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(72.40601f, 0.1249962f, -26.24762f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(65.20512f, 0.1195145f, -3.002785f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(57.44494f, 0.3677597f, 9.418499f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(51.22025f, 0.1249943f, 17.74174f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(37.17601f, 0.108139f, 15.05862f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(26.86843f, 0.1447201f, 25.442f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(30.42674f, 0.1125221f, 43.93211f)));
                    }
                    else if (MonumentName.ToLower() == "dome")
                    {
                        Path.Add(monument.transform.TransformPoint(new Vector3(-44.06296f, 5.761227f, -12.12094f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-39.51987f, 5.835545f, 7.776884f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-36.0826f, 5.805714f, 19.81175f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-5.549446f, 5.817455f, 46.85023f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(26.90167f, 5.681538f, 54.92204f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(40.37264f, 5.681538f, 52.25163f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(48.66075f, 5.681538f, 44.65501f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(43.03573f, 5.681553f, 15.22724f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(36.59595f, 5.681507f, -3.170134f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(33.6379f, 5.681507f, -10.14482f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(19.88553f, 5.681507f, -31.55441f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-15.66899f, 5.683926f, -39.57835f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-29.22861f, 5.683926f, -40.31653f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-39.74881f, 5.683926f, -32.86559f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-43.75142f, 5.760841f, -12.5981f)));
                    }
                    else if (MonumentName.ToLower() == "harbor_b")
                    {
                        Path.Add(monument.transform.TransformPoint(new Vector3(22.69171f, 3.886088f, -84.9832f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-34.13817f, 3.885469f, -85.58311f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-82.81777f, 3.899666f, -85.27451f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-83.25726f, 3.882581f, -53.53805f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-81.72797f, 3.890851f, -28.78954f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-15.85363f, 3.888145f, -25.4362f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(6.648301f, 3.878627f, -23.37169f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(41.06578f, 3.89161f, -22.74822f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(40.19106f, 3.877769f, 14.55536f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-30.9287f, 4.002068f, 16.40429f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-67.09302f, 3.88547f, 16.75063f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-64.94119f, 3.881475f, 35.45695f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-64.26638f, 3.88025f, 91.83291f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-26.85582f, 4.000001f, 99.94082f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-24.60423f, 4.000001f, 71.51466f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(45.19585f, 4.000001f, 69.42707f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-24.41446f, 4.000001f, 71.30032f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-27.07535f, 4.000001f, 95.90489f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-65.07816f, 3.882022f, 93.40216f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-64.13502f, 3.877976f, 50.48643f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-64.4117f, 3.892355f, 19.06527f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-66.65001f, 3.896023f, -24.14629f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-80.14777f, 3.896023f, -27.12628f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-83.31219f, 3.882886f, -51.19764f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-82.76659f, 3.899487f, -84.80103f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-1.820816f, 3.902136f, -85.77778f)));
                        if (stopPause.Count <= 0)
                        {
                            stopPause.Add(12, 5);
                            stopPause.Add(23, 5);
                            stopPause.Add(32, 10);
                            stopPause.Add(39, 5);
                            stopPause.Add(48, 5);
                        }
                    }
                    else if (MonumentName.ToLower() == "harbor_a")
                    {
                        Path.Add(monument.transform.TransformPoint(new Vector3(100.5458f, 5.137896f, -21.7406f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(57.48274f, 5.148497f, -21.7406f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(57.34441f, 5.091216f, -55.93035f)));  // stop
                        Path.Add(monument.transform.TransformPoint(new Vector3(57.59581f, 5.135468f, -21.7406f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(30.89552f, 5.132325f, -21.7406f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(25.99373f, 5.132325f, -24.61702f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(24.48319f, 5.138023f, -31.04927f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(18.74314f, 5.138023f, -36.69394f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-12.91411f, 5.157865f, -36.64078f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-45.35143f, 5.137881f, -36.76052f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-54.72924f, 5.25f, -26.22145f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-51.83475f, 5.249999f, -25.45465f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-51.80507f, 5.249999f, 8.364723f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-51.39455f, 5.249999f, -25.65089f))); // stop
                        Path.Add(monument.transform.TransformPoint(new Vector3(-63.31296f, 5.139017f, -36.62488f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-98.159f, 5.146594f, -36.74872f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-103.2626f, 5.135469f, -40.40769f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-104.3916f, 5.135469f, -43.80708f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-105.6828f, 5.098644f, -52.17129f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-104.8878f, 5.110994f, -59.16515f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-99.53786f, 4.986493f, -66.03759f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-60.44549f, 5.109105f, -66.03759f))); // stop
                        Path.Add(monument.transform.TransformPoint(new Vector3(-38.67418f, 5.103838f, -66.03759f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-33.38956f, 5.285808f, -69.97565f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-29.30282f, 5.28581f, -71.23842f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-16.86336f, 5.28581f, -75.50622f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(-11.87201f, 5.28581f, -76.12173f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(0.8109016f, 5.288602f, -75.18558f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(11.23451f, 5.288602f, -71.63471f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(22.01541f, 5.288602f, -64.14096f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(29.35221f, 5.288602f, -54.96849f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(34.42397f, 5.288602f, -44.3777f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(36.47923f, 5.288602f, -33.42015f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(36.74368f, 5.424547f, -21.7406f))); // stop
                        Path.Add(monument.transform.TransformPoint(new Vector3(45.6758f, 5.120514f, -21.7406f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(45.6758f, 5.250004f, 11.5046f))); // stop
                        Path.Add(monument.transform.TransformPoint(new Vector3(45.84613f, 5.115921f, -21.7406f)));
                        Path.Add(monument.transform.TransformPoint(new Vector3(91.77109f, 5.138303f, -21.7406f)));
                        if (stopPause.Count <= 0)
                        {
                            stopPause.Add(2, 5);
                            stopPause.Add(13, 5);
                            stopPause.Add(21, 10);
                            stopPause.Add(33, 5);
                            stopPause.Add(35, 5);
                        }
                    }
                }
                else if (Custom && _instance.pcdData.pEntity != null)
                {
                    Path.Clear();
                    stopPause.Clear();
                    speed.Clear();
                    foreach (var loc in _instance.pcdData.pEntity[MonumentName].Path.ToList())
                    {
                        Path.Add(loc.Value.pos);
                        if (loc.Value.pause != 0)
                        {
                            if (!stopPause.ContainsKey(loc.Key))
                                stopPause.Add(loc.Key, loc.Value.pause);
                        }
                        if (loc.Value.speed != 0f)
                        {
                            if (!speed.ContainsKey(loc.Key))
                                speed.Add(loc.Key, loc.Value.speed);
                        }
                    }
                    if (reverse)
                    {
                        Path.Reverse();
                        stopPause.Clear();
                        speed.Clear();
                        foreach (var loc in _instance.pcdData.pEntity[MonumentName].Path.ToList())
                        {
                            var index = Path.FindIndex(x => x == loc.Value.pos);
                            if (index == null || index < 0) continue;
                            if (loc.Value.pause != 0)
                            {
                                if (!stopPause.ContainsKey(index))
                                    stopPause.Add(index, loc.Value.pause);
                            }
                            if (loc.Value.speed != 0f)
                            {
                                if (_instance.pcdData.pEntity[MonumentName].Path.ContainsKey(index - 1))
                                    if (!speed.ContainsKey(index - 1))
                                        speed.Add(index - 1, loc.Value.speed);
                            }
                        }
                    }
                }
                okGo = true;
                bradly.currentPath = Path;
                bradly.currentPathIndex = 0;
                bradly.pathLooping = true;
                bradly.SendNetworkUpdateImmediate();
                return true;
            }

            private void StopMoving()
            {
                currentPathIndex = bradly.currentPathIndex;
                bradly.pathLooping = false;
                bradly.ClearPath();
            }

            void StartMoving()
            {
                if (Path.Count <= 0) MakePath();
                bradly.currentPath = Path;
                bradly.currentPathIndex = currentPathIndex;
                bradly.pathLooping = true;
                bradly.moveForceMax = moveForce;
                stopped = false;
                bradly.DoHealing();
            }

            private IEnumerator updateAim()
            {
                while (!stopped)
                {
                    aimWeaponAt();
                    yield return new WaitForSeconds(4);
                }

                if (QueuedRoutine != null && stopped)
                    InvokeHandler.Instance.StopCoroutine(QueuedRoutine);
                QueuedRoutine = null;
            }

            private float nextForceTime { get; set; }
            private int totalfired { get; set; }

            private void Update()
            {
                if (bradly == null || !okGo) return;

                if (trainInTheWay && bradly.targetList.Count <= 0)
                {
                    if (rocketTime < Time.time)
                    {
                        bradly.nextFireTime = UnityEngine.Time.time;
                        totalfired++;
                        aimWeaponAtTrain();
                        if (totalfired < 3)
                            rocketTime = Time.time + 0.2f;
                        else
                        {
                            rocketTime = Time.time + 5.0f;
                            totalfired = 0;
                        }

                        if (trainCar != null)
                        {
                            bradly.FireGunTest();
                            trainCar.Hurt(hitinfoTrain);
                        }
                        else if (magnetcrane != null)
                        {
                            bradly.FireGunTest();
                            magnetcrane.Hurt(hitinfoOther);
                        }
                        else
                        {
                            trainInTheWay = false;
                            pauseTime = DateTime.Now;
                            StartMoving();
                        }
                    }
                    return;
                }

                if (nextStuckTime < Time.time)
                    IsStuck();

                if (colContainer.Count > 0 && nextForceTime < Time.time)
                {
                    nextForceTime = Time.time + 2f;
                    foreach (Rigidbody rBody in colContainer.ToList())
                    {
                        if (rBody == null)
                            colContainer.Remove(rBody);
                        else
                        {
                            rBody.AddForce(transform.right * 50, ForceMode.Acceleration);
                            rBody.AddForce(transform.forward * 50, ForceMode.Acceleration);
                        }
                    }
                }

                // Pause
                if (stopPause.Count > 0 && !isPausePos && stopPause.ContainsKey(bradly.currentPathIndex) && NextpauseTime <= DateTime.Now)
                {
                    if (Vector3.Distance(bradly.transform.position, bradly.currentPath[bradly.currentPathIndex]) <= (double)bradly.stoppingDist)
                    {
                        pauseTime = DateTime.Now.AddSeconds(stopPause[bradly.currentPathIndex]);
                        pauseIndex = bradly.currentPathIndex;
                        isPausePos = true;
                        stopped = true;
                        StopMoving();
                        float delay = random.Next(1, 2);
                        InvokeRepeating("Relocate", delay, delay);
                        return;
                    }
                }

                //Speed
                if (speed.Count > 0 && speed.ContainsKey(bradly.currentPathIndex - 1) && moveForce != speed[bradly.currentPathIndex - 1])
                {
                    if (speed[bradly.currentPathIndex - 1] != bradly.moveForceMax && Vector3.Distance(bradly.transform.position, bradly.currentPath[bradly.currentPathIndex - 1]) <= (double)bradly.stoppingDist)
                    {
                        moveForce = speed[bradly.currentPathIndex - 1];
                        bradly.moveForceMax = speed[bradly.currentPathIndex - 1];
                    }

                }
                if (!Custom && !RoadRing && monument == null) return;

                if (bradly.patrolPath != null) bradly.patrolPath = null;

                if (!stopped && bradly.targetList.Count >= 1)
                {
                    if (RoadRing && !_instance.configData.settingsRing.RingRoadSettings[0].CanStop)
                    {

                    }
                    else
                    {
                        stopped = true;
                        StopMoving();
                        float delay = random.Next(2, 6);
                        InvokeRepeating("Relocate", delay, delay);
                        return;
                    }
                }
                //Ajust Aim
                if (QueuedRoutine == null && !stopped)
                {
                    QueuedRoutine = InvokeHandler.Instance.StartCoroutine(updateAim());
                }

                if (RoadRing && bradly.targetList.Count > 0 && _instance.configData.settingsRing.RingRoadSettings[0].FireRocketAtPlayerBase && rocketTime < Time.time)
                {
                    shouldFireRocket();
                }

                //Fix if loses path
                if (!stopped && Active < DateTime.Now && bradly.currentPath != Path && bradly.targetList.Count <= 0)
                {
                    StartMoving();
                }
                if (Custom)
                {
                    //.Reverse()
                    if (_instance.pcdData.pEntity[MonumentName].FollowPathBack && bradly.currentPath.Count >= 1)
                    {
                        float distance = Vector3.Distance(bradly.currentPath[bradly.currentPath.Count - 1], bradly.transform.position);
                        if (Path.Count > 0 && distance <= 5.0f)
                        {
                            if (reverse) reverse = false;
                            else reverse = true;
                            MakePath(RoadRing);
                        }
                    }
                }
            }

            private void shouldFireRocket()
            {
                rocketTime = Time.time + 20;
                foreach (var target in bradly.targetList.ToList())
                {
                    if (target.lastSeenTime + 5 < Time.time || bradly.SecondsSinceAttacked >= 60) continue;
                    if (target.entity is BasePlayer)
                    {
                        Vector3 offset = new Vector3(-107.3504f, 12.1489f, -107.7641f);
                        BasePlayer targetPlayer = target.entity as BasePlayer;
                        if (targetPlayer == null || targetPlayer.IsDead() || targetPlayer.IsSleeping() || offset == Vector3.zero) continue;
                        if (isPlayersBuilding(targetPlayer, targetPlayer.userID))
                        {
                            _instance.timer.Once(2.5f, () => { FireRocket(); });
                            break;
                        }
                    }
                }
            }

            public void FireRocket()
            {
                if (bradly == null) return;
                Vector3 launchPos = bradly.CannonMuzzle.position + bradly.CannonMuzzle.forward * 1;
                // launchPos.y = launchPos.y + 0.5f;
                Vector3 forward = bradly.CannonMuzzle.position + bradly.CannonMuzzle.forward * 20;
                Vector3 newTarget = Quaternion.Euler(0, 0, 0) * forward;

                BaseEntity rocket = GameManager.server.CreateEntity(_instance.configData.settingsRing.RingRoadSettings[0].RocketPrefab, launchPos, new Quaternion(), true);
                if (rocket == null) return;
                TimedExplosive rocketExplosion = rocket.GetComponent<TimedExplosive>();
                ServerProjectile rocketProjectile = rocket.GetComponent<ServerProjectile>();
                rocket.creatorEntity = bradly;
                rocketProjectile.speed = 30;
                rocketProjectile.gravityModifier = 0;
                rocketExplosion.timerAmountMin = 60;
                rocketExplosion.timerAmountMax = 60;

                Vector3 newDirection = (newTarget - launchPos);
                rocketProjectile.InitializeVelocity(newDirection * 2);
                rocket.Spawn();
            }

            //SetAim
            void aimWeaponAt()
            {
                bradly.desiredAimVector = bradly.topTurretAimVector;
                bradly.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                IsAiming = false;
            }

            void aimWeaponAtTrain()
            {
                Vector3 positonAim = Vector3.zero;
                if (trainCar != null)
                    positonAim = trainCar.transform.position;
                else if (magnetcrane != null)
                        positonAim = magnetcrane.transform.position;
                if (positonAim == Vector3.zero)
                    return;
                bradly.desiredAimVector = positonAim - bradly.transform.position;
                bradly.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                IsAiming = false;
            }

            private List<Rigidbody> colContainer = new List<Rigidbody>();
            private void OnCollisionEnter(Collision collision)
            {
                GameObject gObject = collision.gameObject;
                if (gObject == null) return;

                LootContainer loot = gObject.GetComponentInParent<LootContainer>();
                if (loot != null)
                {
                    if (loot is HackableLockedCrate || loot is SupplyDrop)
                    {
                        Rigidbody rBody = loot.GetComponent<Rigidbody>();
                        if (rBody != null)
                        {
                            if (!colContainer.Contains(rBody))
                                colContainer.Add(rBody);
                        }
                        return;
                    }
                    loot.Die(new HitInfo(bradly, loot, DamageType.Blunt, 200f));
                    return;
                }

                ResourceEntity resource = gObject.GetComponentInParent<ResourceEntity>();
                DroppedItemContainer containers = gObject.GetComponentInParent<DroppedItemContainer>();
                ModularCar car = gObject.GetComponentInParent<ModularCar>();
                TrainCar FoundTrainCar = gObject.GetComponentInParent<TrainCar>();
                MagnetCrane FoundMagnetCrane = gObject.GetComponentInParent<MagnetCrane>();
                DroppedItemContainer backpack = gObject.GetComponentInParent<DroppedItemContainer>();

                if (FoundMagnetCrane != null)
                {
                    magnetcrane = FoundMagnetCrane;
                    trainInTheWay = true;
                    rocketTime = Time.time + 5;
                    aimWeaponAtTrain();
                    //_instance.Puts("train enter " + FoundTrainCar.GetType().ToString());
                    pauseTime = DateTime.Now.AddMinutes(1800);
                    pauseIndex = bradly.currentPathIndex;
                    isPausePos = true;
                    stopped = true;
                    StopMoving();
                    bradly.moveForceMax = 0;
                    float delay = random.Next(1, 2);
                    return;
                }
                else if (FoundTrainCar != null)
                {
                    trainCar = FoundTrainCar;
                    trainInTheWay = true;
                    rocketTime = Time.time + 5;
                    aimWeaponAtTrain();
                    //_instance.Puts("train enter " + FoundTrainCar.GetType().ToString());
                    pauseTime = DateTime.Now.AddMinutes(1800);
                    pauseIndex = bradly.currentPathIndex;
                    isPausePos = true;
                    stopped = true;
                    StopMoving();
                    bradly.moveForceMax = 0;
                    float delay = random.Next(1, 2);
                    return;
                }
                else if (car != null)
                {
                    Rigidbody rBody = car.GetComponent<Rigidbody>();
                    if (rBody != null)
                    {
                        if (rBody.IsSleeping())
                            rBody.WakeUp();
                        var nullHit = new HitInfo(bradly, car, Rust.DamageType.Explosion, 100, car.transform.position);
                        nullHit.damageTypes.Add(Rust.DamageType.AntiVehicle, 50f);
                        car.Hurt(nullHit);
                        //rBody.AddExplosionForce(Mathf.Min(10 * 650f, 150000f), nullHit.HitPositionWorld, 1f, 2.5f);
                    }
                    return;
                }
                else if (resource != null)
                {
                    resource.Kill(BaseNetworkable.DestroyMode.None);
                    return;
                }
                else if (containers != null)
                {
                    if (containers != null && !containers.IsDestroyed)
                        DropUtil.DropItems(containers.inventory, containers.transform.position);
                    if (containers != null && !containers.IsDestroyed)
                        containers.Kill(BaseNetworkable.DestroyMode.None);
                    return;
                }
                else if (backpack != null && backpack.ShortPrefabName == "item_drop_backpack")
                {
                    if (backpack != null && !backpack.IsDestroyed)
                        DropUtil.DropItems(backpack.inventory, backpack.transform.position);
                    if (backpack != null && !backpack.IsDestroyed)
                        backpack.Kill(BaseNetworkable.DestroyMode.None);
                    return;
                }
                else if (RoadRing && _instance.configData.settingsRing.RingRoadSettings[0].DestroyBuildingBlocks)
                {
                    BuildingBlock block = gObject.GetComponentInParent<BuildingBlock>();
                    if (block != null)
                    {
                        block.Kill(BaseNetworkable.DestroyMode.None);
                        return;
                    }
                }
            }

            private void OnCollisionExit(Collision collision)
            {
                GameObject gObject = collision.gameObject;
                if (gObject == null) return;

                LootContainer loot = gObject.GetComponentInParent<LootContainer>();
                TrainCar trainCar = gObject.GetComponentInParent<TrainCar>();

                if (loot != null)
                {
                    Rigidbody rBody = loot.GetComponent<Rigidbody>();
                    if (rBody != null)
                    {
                        if (colContainer.Contains(rBody))
                            colContainer.Remove(rBody);
                    }
                }

                if (trainCar != null)
                {
                    trainCar = null;
                    pauseTime = DateTime.Now;
                    trainInTheWay = false;
                    StartMoving();
                    return;
                }

                if (magnetcrane != null)
                {
                    magnetcrane = null;
                    pauseTime = DateTime.Now;
                    trainInTheWay = false;
                    StartMoving();
                    return;
                }
            }
        }
        #endregion        
    }

    #region BradleyEx Class
    namespace BradleyEx
    {
        public static class BradleyEx
        {
            public enum MonumentName
            {
                Unknown = 0,
                Lighthouse,
                MiningOutpost,
                Dome,
                SatelliteDish,
                SewerBranch,
                Powerplant,
                Trainyard,
                Airfield,
                MilitaryTunnel,
                WaterTreatment,
                SulfurQuarry,
                StoneQuarry,
                HqmQuarry,
                GasStation,
                Supermarket,
                LaunchSite,
                Outpost,
                BanditCamp,
                Harbor_A,
                Harbor_B,
                Junkyard,
                OilRig_Small,
                OilRig_Large,
            }

            private static Dictionary<string, MonumentName> MonumentToName = new Dictionary<string, MonumentName>()
            {
                { "assets/bundled/prefabs/autospawn/monument/small/warehouse.prefab", MonumentName.MiningOutpost },
                { "assets/bundled/prefabs/autospawn/monument/lighthouse/lighthouse.prefab", MonumentName.Lighthouse },
                { "assets/bundled/prefabs/autospawn/monument/small/satellite_dish.prefab", MonumentName.SatelliteDish },
                { "assets/bundled/prefabs/autospawn/monument/small/sphere_tank.prefab", MonumentName.Dome },
                { "assets/bundled/prefabs/autospawn/monument/harbor/harbor_1.prefab", MonumentName.Harbor_A },
                { "assets/bundled/prefabs/autospawn/monument/harbor/harbor_2.prefab", MonumentName.Harbor_B },
                { "assets/bundled/prefabs/autospawn/monument/large/airfield_1.prefab", MonumentName.Airfield },
                { "assets/bundled/prefabs/autospawn/monument/medium/junkyard_1.prefab", MonumentName.Junkyard },
                { "assets/bundled/prefabs/autospawn/monument/large/launch_site_1.prefab", MonumentName.LaunchSite },
                { "assets/bundled/prefabs/autospawn/monument/large/military_tunnel_1.prefab", MonumentName.MilitaryTunnel },
                { "assets/bundled/prefabs/autospawn/monument/large/powerplant_1.prefab", MonumentName.Powerplant },
                { "assets/bundled/prefabs/autospawn/monument/large/trainyard_1.prefab", MonumentName.Trainyard },
                { "assets/bundled/prefabs/autospawn/monument/railside/trainyard_1.prefab", MonumentName.Trainyard },
                { "assets/bundled/prefabs/autospawn/monument/large/water_treatment_plant_1.prefab", MonumentName.WaterTreatment },
                { "assets/bundled/prefabs/autospawn/monument/medium/bandit_town.prefab", MonumentName.BanditCamp },
                { "assets/bundled/prefabs/autospawn/monument/medium/compound.prefab", MonumentName.Outpost },
                { "assets/bundled/prefabs/autospawn/monument/medium/radtown_small_3.prefab", MonumentName.SewerBranch },
                { "assets/bundled/prefabs/autospawn/monument/small/gas_station_1.prefab", MonumentName.GasStation },
                { "assets/bundled/prefabs/autospawn/monument/small/mining_quarry_a.prefab", MonumentName.SulfurQuarry },
                { "assets/bundled/prefabs/autospawn/monument/small/mining_quarry_b.prefab", MonumentName.StoneQuarry },
                { "assets/bundled/prefabs/autospawn/monument/small/mining_quarry_c.prefab", MonumentName.HqmQuarry },
                { "assets/bundled/prefabs/autospawn/monument/offshore/oilrig_2.prefab", MonumentName.OilRig_Small },
                { "assets/bundled/prefabs/autospawn/monument/offshore/oilrig_1.prefab", MonumentName.OilRig_Large },
            };

            public static MonumentName GetMonumentName(this MonumentInfo monument)
            {
                MonumentName name;

                var gameObject = monument.gameObject;

                while (gameObject.name.StartsWith("assets/") == false && gameObject.transform.parent != null)
                {
                    gameObject = gameObject.transform.parent.gameObject;
                }

                MonumentToName.TryGetValue(gameObject.name, out name);

                return name;
            }

            private static FieldInfo TimeSinceStuckReverseStartField = typeof(BradleyAPC).GetField("timeSinceStuckReverseStart", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            public static TimeSince GetTimeSinceStuckReverseStart(this BradleyAPC bradley)
            {
                return (TimeSince)TimeSinceStuckReverseStartField.GetValue(bradley);
            }

            private static FieldInfo NumberOfScientistsToSpawnMax = typeof(BradleyAPC).GetField("numberOfScientistsToSpawn", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            public static void setNumberOfScientistsToSpawn(this BradleyAPC bradley, int value)
            {
                NumberOfScientistsToSpawnMax.SetValue(bradley, value);
            }
        }
    }
    #endregion
}