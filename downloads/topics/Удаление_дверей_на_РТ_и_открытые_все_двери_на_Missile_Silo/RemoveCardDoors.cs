using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Remove Card Doors", "FacepunchExpert", "1.0.4")]
    [Description("Removes keycard doors in Red Town monuments and keeps Missile Silo doors open")]
    public class RemoveCardDoors : CovalencePlugin
    {
        #region Fields
        private Configuration config;
        private Timer processingTimer;
        private readonly Dictionary<ulong, bool> managedDoors = new();
        private readonly HashSet<ulong> processedDoors = new();
        private readonly HashSet<Door> permanentlyOpenedDoors = new();
        private bool hasInitialized;
        #endregion Fields

        #region Configuration
        public class Configuration
        {
            [JsonProperty(nameof(RemoveDoorsFromMonuments))]
            public Dictionary<string, bool> RemoveDoorsFromMonuments { get; set; }

            [JsonProperty(nameof(OpenDoorsInMonuments))]
            public Dictionary<string, bool> OpenDoorsInMonuments { get; set; }

            [JsonProperty(nameof(MonumentNameIdentifiers))]
            public Dictionary<string, List<string>> MonumentNameIdentifiers { get; set; }

            [JsonProperty(nameof(DoorPrefabsToRemove))]
            public List<string> DoorPrefabsToRemove { get; set; }

            [JsonProperty(nameof(DoorPrefabsToOpen))]
            public List<string> DoorPrefabsToOpen { get; set; }

            [JsonProperty(nameof(LogActions))]
            public bool LogActions { get; set; }

            [JsonProperty(nameof(ActionInterval))]
            public float ActionInterval { get; set; }

            [JsonProperty(nameof(CardReaderSearchRadius))]
            public float CardReaderSearchRadius { get; set; }

            [JsonProperty(nameof(DebugMode))]
            public bool DebugMode { get; set; }

            [JsonProperty(nameof(InitialStartDelay))]
            public float InitialStartDelay { get; set; }

            [JsonProperty(nameof(PreventDoorSpawn))]
            public bool PreventDoorSpawn { get; set; }

            [JsonProperty(nameof(ProcessAllDoors))]
            public bool ProcessAllDoors { get; set; }

            public Configuration()
            {
                RemoveDoorsFromMonuments = new Dictionary<string, bool>
                {
                    ["RedTown"] = true,
                    ["MissileSilo"] = false,
                };

                OpenDoorsInMonuments = new Dictionary<string, bool>
                {
                    ["MissileSilo"] = true,
                    ["RedTown"] = false,
                };

                MonumentNameIdentifiers = new Dictionary<string, List<string>>
                {
                    ["RedTown"] = new List<string>
                    {
                        "radtown",
                        "red town",
                        "powerplant",
                        "power plant",
                        "train yard",
                        "trainyard",
                        "water treatment",
                        "sewer branch",
                        "satellite",
                        "airfield",
                        "dome",
                        "harbor",
                        "junkyard",
                        "gas station",
                        "supermarket",
                        "lighthouse",
                        "cabins",
                        "tunnels",
                        "military",
                        "launch site",
                        "mining outpost",
                        "arctic",
                        "excavator",
                        "underwater",
                        "labs",
                        "outpost",
                        "fishing",
                        "ranch",
                        "sphere",
                        "oil rig",
                        "abandoned",
                        "compound",
                        "camp",
                        "mining",
                        "refinery",
                        "cargo",
                        "terminal",
                    },
                    ["MissileSilo"] = new List<string> { "missile", "silo", "hatch" },
                };

                DoorPrefabsToRemove = new List<string>
                {
                    "door.hinged.security.red",
                    "door.hinged.security.blue",
                    "door.hinged.security.green",
                    "door.hinged.toptier",
                    "door.hinged.industrial.a",
                    "door.hinged.industrial.d",
                    "door.hinged.industrial.e",
                    "door.hinged.metal",
                    "door.hinged",
                    "door.double.hinged.metal",
                    "door.double.hinged.toptier",
                    "door.double.hinged.industrial.a",
                    "door.double.hinged.windowed",
                };

                DoorPrefabsToOpen = new List<string>
                {
                    "door.hinged.nms_hatch",
                    "door.hinged.silo",
                    "hatch",
                };

                LogActions = true;
                ActionInterval = 300f;
                CardReaderSearchRadius = 10f;
                DebugMode = false;
                InitialStartDelay = 5f;
                PreventDoorSpawn = true;
                ProcessAllDoors = true;
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LogError("Could not read config file, creating a new one");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        #endregion Configuration

        #region Oxide Hooks
        private void Init()
        {
            // Register permission for admin commands
            permission.RegisterPermission("removedoors.admin", this);

            // Register commands
            AddCovalenceCommand("removedoors.process", nameof(CmdProcessDoors));
            AddCovalenceCommand("removedoors.debug", nameof(CmdToggleDebug));
            AddCovalenceCommand("removedoors.listalldoors", nameof(CmdListAllDoors));

            // Register chat commands (shorter aliases)
            AddCovalenceCommand("rd.doors", nameof(CmdListAllDoors));
            AddCovalenceCommand("rd.process", nameof(CmdProcessDoors));
            AddCovalenceCommand("rd.debug", nameof(CmdToggleDebug));

            // Register simple chat commands for players
            AddCovalenceCommand("doors", nameof(ChatCommandListDoors));
            AddCovalenceCommand("rddebug", nameof(ChatCommandToggleDebug));
            AddCovalenceCommand("rdprocess", nameof(ChatCommandProcess));
        }

        private void OnServerInitialized()
        {
            // Immediate initial processing
            LogWarning("Starting initial door processing - immediate processing");
            _ = timer.Once(
                0.1f,
                () =>
                {
                    try
                    {
                        ProcessDoorsAggressively();
                        Puts("Initial door processing completed");
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error in initial door processing: {ex}");
                    }
                }
            );

            // Secondary startup processing with a delay
            LogWarning(
                $"Scheduling secondary door processing in {config.InitialStartDelay} seconds"
            );
            _ = timer.Once(
                config.InitialStartDelay,
                () =>
                {
                    try
                    {
                        ProcessDoorsAggressively();
                        hasInitialized = true;
                        Puts("Secondary door processing completed - initialization complete");

                        // Additional aggressive processing to ensure all doors are handled
                        _ = timer.Once(
                            10f,
                            () =>
                            {
                                ProcessDoorsAggressively();
                                Puts("Final door processing completed");
                            }
                        );
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error in secondary door processing: {ex}");
                    }
                }
            );

            // Start the repeating timer to process doors
            processingTimer = timer.Every(
                config.ActionInterval,
                () =>
                {
                    try
                    {
                        ProcessAllDoorsMethod();
                        MaintainPermanentlyOpenDoors();
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error in door processing: {ex}");
                    }
                }
            );
        }

        private void Unload()
        {
            // Clear timer
            processingTimer?.Destroy();
            processingTimer = null;
        }

        private object OnEntitySpawned(Door door)
        {
            // Don't process during map generation or before initialization
            if (!hasInitialized && TerrainMeta.Path?.Monuments == null)
            {
                return null;
            }

            if (door?.IsDestroyed != false)
            {
                return null;
            }

            try
            {
                // Process the door with a short delay to ensure proper monument binding
                _ = timer.Once(
                    0.1f,
                    () =>
                    {
                        // Skip if already processed
                        if (
                            door?.IsDestroyed != false
                            || processedDoors.Contains(door.net.ID.Value)
                        )
                        {
                            return;
                        }

                        // Check if door should be processed based on prefab name
                        bool shouldProcess = ShouldProcessDoorByPrefab(door, out bool shouldOpen);

                        if (shouldProcess)
                        {
                            if (shouldOpen)
                            {
                                ProcessDoorForOpening(door);
                            }
                            else
                            {
                                LogInfo(
                                    $"Removing door by prefab name: {door.PrefabName} at {door.transform.position}"
                                );
                                _ = processedDoors.Add(door.net.ID.Value);
                                door.Kill();
                            }
                            return;
                        }

                        // Get monument and type
                        MonumentInfo monument = GetMonumentInfo(door);
                        if (monument == null)
                        {
                            return;
                        }

                        string monumentType = IdentifyMonumentType(monument);
                        if (string.IsNullOrEmpty(monumentType))
                        {
                            return;
                        }

                        // Check if this door should be blocked from spawning
                        if (
                            config.PreventDoorSpawn
                            && ShouldRemoveDoor(monumentType)
                            && IsCardLocked(door)
                        )
                        {
                            if (config.LogActions || config.DebugMode)
                            {
                                LogInfo(
                                    $"PREVENTED keycard door spawn at {monumentType} monument: {door.transform.position}"
                                );
                            }
                            _ = processedDoors.Add(door.net.ID.Value);
                            door.Kill();
                            return;
                        }

                        // Otherwise, process normally
                        TryProcessDoor(door);
                    }
                );
            }
            catch (Exception ex)
            {
                LogError($"Error in OnEntitySpawned for door: {ex}");
            }

            return null;
        }

        private void OnEntitySpawned(CardReader cardReader)
        {
            // Don't process during map generation or before initialization
            if (!hasInitialized && TerrainMeta.Path?.Monuments == null)
            {
                return;
            }

            if (cardReader?.IsDestroyed != false)
            {
                return;
            }

            try
            {
                // Check nearby doors with a small delay
                _ = timer.Once(
                    0.5f,
                    () =>
                    {
                        if (cardReader?.IsDestroyed != false)
                        {
                            return;
                        }

                        // Get monument and type for this card reader
                        MonumentInfo monument = GetMonumentInfoFromPosition(
                            cardReader.transform.position
                        );
                        if (monument == null)
                        {
                            return;
                        }

                        string monumentType = IdentifyMonumentType(monument);
                        if (string.IsNullOrEmpty(monumentType))
                        {
                            return;
                        }

                        if (config.DebugMode)
                        {
                            LogInfo($"CardReader found in monument type: {monumentType}");
                        }

                        List<Door> nearbyDoors = new();
                        Vis.Entities(
                            cardReader.transform.position,
                            config.CardReaderSearchRadius,
                            nearbyDoors
                        );

                        foreach (Door door in nearbyDoors)
                        {
                            if (
                                door?.IsDestroyed != false
                                || processedDoors.Contains(door.net.ID.Value)
                            )
                            {
                                continue;
                            }

                            // Check if door should be processed based on prefab name
                            bool shouldProcess = ShouldProcessDoorByPrefab(
                                door,
                                out bool shouldOpen
                            );

                            if (shouldProcess)
                            {
                                if (shouldOpen)
                                {
                                    ProcessDoorForOpening(door);
                                }
                                else
                                {
                                    LogInfo(
                                        $"Removing door by prefab name from card reader detection: {door.PrefabName} at {door.transform.position}"
                                    );
                                    _ = processedDoors.Add(door.net.ID.Value);
                                    door.Kill();
                                }
                                continue;
                            }

                            // Process door based on monument type
                            if (ShouldRemoveDoor(monumentType))
                            {
                                _ = processedDoors.Add(door.net.ID.Value);
                                if (config.LogActions)
                                {
                                    LogInfo(
                                        $"Removing keycard door from cardreader detection at {monumentType}: {door.transform.position}"
                                    );
                                }
                                door.Kill();
                            }
                            else if (ShouldOpenDoor(monumentType))
                            {
                                ProcessDoorForOpening(door);
                            }
                        }
                    }
                );
            }
            catch (Exception ex)
            {
                LogError($"Error in OnEntitySpawned for cardReader: {ex}");
            }
        }

        private void OnTerrainInitialized()
        {
            // Process all doors again now that the terrain is fully initialized
            LogWarning("Terrain initialized - processing all doors to ensure removal");
            _ = timer.Once(
                2f,
                () =>
                {
                    try
                    {
                        ProcessDoorsAggressively();
                        Puts("Terrain initialized door processing completed");

                        // Second processing for reliability
                        _ = timer.Once(
                            5f,
                            () =>
                            {
                                ProcessDoorsAggressively();
                                Puts("Second terrain initialized door processing completed");

                                // Final processing with focus on Missile Silo
                                _ = timer.Once(
                                    5f,
                                    () =>
                                    {
                                        ProcessMissileSiloDoors();
                                        Puts("Missile Silo doors processing completed");
                                    }
                                );
                            }
                        );
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error in terrain initialization door processing: {ex}");
                    }
                }
            );
        }

        /// <summary>
        /// Special method to focus on Missile Silo doors
        /// </summary>
        private void ProcessMissileSiloDoors()
        {
            LogInfo("Processing Missile Silo doors specifically...");

            foreach (Door door in UnityEngine.Object.FindObjectsOfType<Door>())
            {
                if (door?.IsDestroyed != false)
                {
                    continue;
                }

                string prefabName = door.PrefabName ?? string.Empty;

                // Check for missile silo related names
                if (
                    prefabName.Contains("silo")
                    || prefabName.Contains("hatch")
                    || prefabName.Contains("nms")
                )
                {
                    ProcessDoorForOpening(door);
                    LogInfo(
                        $"Missile Silo door found and opened: {prefabName} at {door.transform.position}"
                    );
                    continue;
                }

                // Check if door is in Missile Silo monument
                MonumentInfo monument = GetMonumentInfo(door);
                if (monument != null)
                {
                    string monumentName = monument.name.ToLower(
                        System.Globalization.CultureInfo.CurrentCulture
                    );
                    if (monumentName.Contains("missile") || monumentName.Contains("silo"))
                    {
                        ProcessDoorForOpening(door);
                        LogInfo(
                            $"Door in Missile Silo monument opened: {prefabName} at {door.transform.position}"
                        );
                    }
                }
            }
        }
        #endregion Oxide Hooks

        #region Commands
        private void CmdProcessDoors(IPlayer player, string command, string[] args)
        {
            if (player?.HasPermission("removedoors.admin") == false)
            {
                player.Reply("You don't have permission to use this command.");
                return;
            }

            player?.Reply("Processing all doors aggressively...");
            ProcessDoorsAggressively();
            player?.Reply("Door processing complete.");
        }

        private void CmdToggleDebug(IPlayer player, string command, string[] args)
        {
            if (player?.HasPermission("removedoors.admin") == false)
            {
                player.Reply("You don't have permission to use this command.");
                return;
            }

            config.DebugMode = !config.DebugMode;
            SaveConfig();
            player?.Reply($"Debug mode is now {(config.DebugMode ? "enabled" : "disabled")}");
        }

        private void CmdListAllDoors(IPlayer player, string command, string[] args)
        {
            if (player?.HasPermission("removedoors.admin") == false)
            {
                player.Reply("You don't have permission to use this command.");
                return;
            }

            Dictionary<string, int> doorTypes = new();
            int total = 0;

            foreach (Door door in UnityEngine.Object.FindObjectsOfType<Door>())
            {
                if (door?.IsDestroyed != false)
                {
                    continue;
                }

                total++;
                string prefabName = door.PrefabName;

                if (!doorTypes.TryGetValue(prefabName, out int value))
                {
                    value = 0;
                    doorTypes[prefabName] = value;
                }

                doorTypes[prefabName] = ++value;
            }

            player?.Reply($"Found {total} doors in the world:");

            foreach (KeyValuePair<string, int> kvp in doorTypes.OrderByDescending(kv => kv.Value))
            {
                player?.Reply($"- {kvp.Key}: {kvp.Value}");
            }
        }
        #endregion Commands

        #region Core Functions
        private void ProcessDoorForOpening(Door door)
        {
            // Set permanently open
            _ = processedDoors.Add(door.net.ID.Value);
            door.SetFlag(BaseEntity.Flags.Open, true);
            door.SendNetworkUpdateImmediate();
            _ = permanentlyOpenedDoors.Add(door);

            if (config.LogActions)
            {
                LogInfo($"Permanently opened door {door.PrefabName} at {door.transform.position}");
            }
        }

        private bool ShouldProcessDoorByPrefab(Door door, out bool shouldOpen)
        {
            shouldOpen = false;

            if (door?.IsDestroyed != false)
            {
                return false;
            }

            string prefabName = door.PrefabName;

            // Check if this door should be opened
            if (config.DoorPrefabsToOpen.Contains(prefabName))
            {
                shouldOpen = true;
                return true;
            }

            // Check if this door should be removed
            return config.DoorPrefabsToRemove.Contains(prefabName);
        }

        private void ProcessDoorsAggressively()
        {
            LogInfo("Aggressively processing all doors...");

            // Reset these in case we're re-processing doors
            processedDoors.Clear();
            permanentlyOpenedDoors.Clear();

            int foundDoors = 0;
            int removedDoors = 0;
            int openedDoors = 0;

            // Process doors directly by type, ignoring monument boundaries for guaranteed removal
            foreach (Door door in UnityEngine.Object.FindObjectsOfType<Door>())
            {
                if (door?.IsDestroyed != false)
                {
                    continue;
                }

                foundDoors++;

                // Skip if already processed in this run
                if (processedDoors.Contains(door.net.ID.Value))
                {
                    continue;
                }

                string prefabName = door.PrefabName ?? string.Empty;
                LogInfo($"Processing door: {prefabName} at {door.transform.position}");

                // Immediately remove all doors with prefabs in DoorPrefabsToRemove regardless of monument
                if (config.DoorPrefabsToRemove.Contains(prefabName))
                {
                    _ = processedDoors.Add(door.net.ID.Value);
                    LogInfo(
                        $"Removing door by prefab match: {prefabName} at {door.transform.position}"
                    );
                    door.Kill();
                    removedDoors++;
                    continue;
                }

                // Immediately open all doors with prefabs in DoorPrefabsToOpen
                if (config.DoorPrefabsToOpen.Contains(prefabName))
                {
                    _ = processedDoors.Add(door.net.ID.Value);
                    door.SetFlag(BaseEntity.Flags.Open, true);
                    door.SendNetworkUpdateImmediate();
                    _ = permanentlyOpenedDoors.Add(door);
                    LogInfo(
                        $"Permanently opened door by prefab match: {prefabName} at {door.transform.position}"
                    );
                    openedDoors++;
                    continue;
                }

                // Get monument and type for additional processing if needed
                MonumentInfo monument = GetMonumentInfo(door);

                // If monument not found and debug is enabled, log it
                if (monument == null)
                {
                    if (config.DebugMode)
                    {
                        LogInfo($"Door not in any monument: {door.transform.position}");
                    }
                    continue;
                }

                string monumentType = IdentifyMonumentType(monument);
                if (string.IsNullOrEmpty(monumentType))
                {
                    if (config.DebugMode)
                    {
                        LogInfo($"Monument type not identified for: {monument.name}");
                    }
                    continue;
                }

                LogInfo($"Door found in monument type: {monumentType}");

                // Check if this is a keycard door
                bool isCardLocked = IsCardLocked(door);

                // Process door based on monument type
                _ = processedDoors.Add(door.net.ID.Value);

                if ((isCardLocked || config.ProcessAllDoors) && ShouldRemoveDoor(monumentType))
                {
                    LogInfo(
                        $"Removing door in {monumentType}: {prefabName} at {door.transform.position}"
                    );
                    door.Kill();
                    removedDoors++;
                }
                else if ((isCardLocked || config.ProcessAllDoors) && ShouldOpenDoor(monumentType))
                {
                    door.SetFlag(BaseEntity.Flags.Open, true);
                    door.SendNetworkUpdateImmediate();
                    _ = permanentlyOpenedDoors.Add(door);
                    LogInfo(
                        $"Permanently opened door in {monumentType}: {prefabName} at {door.transform.position}"
                    );
                    openedDoors++;
                }
            }

            LogWarning(
                $"Aggressive processing completed: Found {foundDoors} doors total, removed {removedDoors}, permanently opened {openedDoors}"
            );
        }

        private void TryProcessDoor(Door door)
        {
            if (door?.IsDestroyed != false)
            {
                return;
            }

            // Skip if already processed
            if (processedDoors.Contains(door.net.ID.Value))
            {
                return;
            }

            // First check if this door should be processed by prefab name
            bool shouldProcess = ShouldProcessDoorByPrefab(door, out bool shouldOpen);

            if (shouldProcess)
            {
                _ = processedDoors.Add(door.net.ID.Value);

                if (shouldOpen)
                {
                    ProcessDoorForOpening(door);
                }
                else
                {
                    if (config.LogActions)
                    {
                        LogInfo(
                            $"Removing door by prefab match: {door.PrefabName} at {door.transform.position}"
                        );
                    }
                    door.Kill();
                }
                return;
            }

            // Get the monument the door belongs to
            MonumentInfo monument = GetMonumentInfo(door);
            if (monument == null)
            {
                if (config.DebugMode)
                {
                    LogInfo($"Door not in any monument: {door.transform.position}");
                }
                return;
            }

            if (config.DebugMode)
            {
                LogInfo($"Door in monument: {monument.name}");
            }

            // Check if the door has a card reader
            if (!IsCardLocked(door) && !config.ProcessAllDoors)
            {
                if (config.DebugMode)
                {
                    LogInfo($"Door not card locked: {door.transform.position}");
                }
                return;
            }

            if (config.DebugMode)
            {
                LogInfo($"Door is card locked: {door.transform.position}");
            }

            // Get monument type
            string monumentType = IdentifyMonumentType(monument);
            if (string.IsNullOrEmpty(monumentType))
            {
                if (config.DebugMode)
                {
                    LogInfo($"Monument type not identified: {monument.name}");
                }
                return;
            }

            if (config.DebugMode)
            {
                LogInfo($"Monument type: {monumentType}");
            }

            // Process door based on monument type
            ProcessDoor(door, monumentType);
        }

        private void ProcessAllDoorsMethod()
        {
            if (config.DebugMode)
            {
                LogInfo("Processing all doors...");
            }

            int foundDoors = 0;
            // Process all doors in the game
            foreach (Door door in UnityEngine.Object.FindObjectsOfType<Door>())
            {
                foundDoors++;
                TryProcessDoor(door);
            }

            if (config.DebugMode)
            {
                LogInfo($"Processed {foundDoors} doors total");
            }
        }

        private void MaintainPermanentlyOpenDoors()
        {
            // Clean up any destroyed doors
            _ = permanentlyOpenedDoors.RemoveWhere(door => door?.IsDestroyed != false);

            // Ensure doors that should be permanently open remain open
            foreach (Door door in permanentlyOpenedDoors)
            {
                if (door?.IsDestroyed == false && !door.IsOpen())
                {
                    door.SetFlag(BaseEntity.Flags.Open, true);
                    door.SendNetworkUpdateImmediate();

                    if (config.DebugMode)
                    {
                        LogInfo(
                            $"Re-opening door that was closed: {door.PrefabName} at {door.transform.position}"
                        );
                    }
                }
            }
        }

        private void ProcessDoor(Door door, string monumentType)
        {
            // Skip if already processed
            if (processedDoors.Contains(door.net.ID.Value))
            {
                return;
            }

            // Mark as processed
            _ = processedDoors.Add(door.net.ID.Value);

            // Store initial state
            managedDoors[door.net.ID.Value] = door.IsOpen();

            // Check if we should remove this door
            if (ShouldRemoveDoor(monumentType))
            {
                // Remove door
                if (config.LogActions)
                {
                    LogInfo(
                        $"Removing keycard door at {monumentType} monument: {door.PrefabName} at {door.transform.position}"
                    );
                }
                door.Kill();
            }
            // Check if we should keep door open
            else if (ShouldOpenDoor(monumentType))
            {
                // Set the door to be permanently open
                door.SetFlag(BaseEntity.Flags.Open, true);
                door.SendNetworkUpdateImmediate();

                // Track this door for continuous monitoring
                _ = permanentlyOpenedDoors.Add(door);

                if (config.LogActions)
                {
                    LogInfo(
                        $"Permanently opened keycard door at {monumentType} monument: {door.PrefabName} at {door.transform.position}"
                    );
                }

                // Use a timer to ensure the door stays open
                _ = timer.Once(
                    1f,
                    () =>
                    {
                        if (door?.IsDestroyed == false && !door.IsOpen())
                        {
                            door.SetFlag(BaseEntity.Flags.Open, true);
                            door.SendNetworkUpdateImmediate();
                        }
                    }
                );
            }
        }

        private bool ShouldRemoveDoor(string monumentType)
        {
            return config.RemoveDoorsFromMonuments.TryGetValue(monumentType, out bool shouldRemove)
                && shouldRemove;
        }

        private bool ShouldOpenDoor(string monumentType)
        {
            return config.OpenDoorsInMonuments.TryGetValue(monumentType, out bool shouldOpen)
                && shouldOpen;
        }

        private string IdentifyMonumentType(MonumentInfo monument)
        {
            if (monument == null)
            {
                return string.Empty;
            }

            string monumentName = monument.name.ToLower(
                System.Globalization.CultureInfo.CurrentCulture
            );

            if (config.DebugMode)
            {
                LogInfo($"Checking monument: {monumentName}");
            }

            foreach (KeyValuePair<string, List<string>> entry in config.MonumentNameIdentifiers)
            {
                foreach (string identifier in entry.Value)
                {
                    if (monumentName.Contains(identifier))
                    {
                        if (config.DebugMode)
                        {
                            LogInfo(
                                $"Matched {monumentName} as {entry.Key} (matched on '{identifier}')"
                            );
                        }
                        return entry.Key;
                    }
                }
            }

            return string.Empty;
        }

        private MonumentInfo GetMonumentInfo(Door door)
        {
            if (door == null)
            {
                return null;
            }

            return GetMonumentInfoFromPosition(door.transform.position);
        }

        private MonumentInfo GetMonumentInfoFromPosition(Vector3 position)
        {
            // Try finding the monument directly
            if (TerrainMeta.Path?.Monuments != null)
            {
                foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
                {
                    if (monument?.IsInBounds(position) == true)
                    {
                        return monument;
                    }
                }
            }

            return null;
        }

        private bool IsCardLocked(Door door)
        {
            if (door == null)
            {
                return false;
            }

            // Check if door has any nearby card readers
            List<CardReader> cardReaders = new();
            Vis.Entities(door.transform.position, config.CardReaderSearchRadius, cardReaders);

            // Door is considered card-locked if there's at least one card reader nearby
            return cardReaders.Count > 0;
        }

        private void LogInfo(string message)
        {
            if (config.LogActions || config.DebugMode)
            {
                Puts(message);
            }
        }

        private void LogWarning(string message)
        {
            Puts($"[WARNING] {message}");
        }
        #endregion Core Functions

        /// <summary>
        /// Chat command handlers
        /// </summary>
        /// <param name="player"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        private void ChatCommandListDoors(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission("removedoors.admin"))
            {
                player.Reply("У вас нет прав для использования этой команды.");
                return;
            }

            Dictionary<string, int> doorTypes = new();
            int total = 0;

            foreach (Door door in UnityEngine.Object.FindObjectsOfType<Door>())
            {
                if (door?.IsDestroyed != false)
                {
                    continue;
                }

                total++;
                string prefabName = door.PrefabName;

                if (!doorTypes.TryGetValue(prefabName, out int value))
                {
                    value = 0;
                    doorTypes[prefabName] = value;
                }

                doorTypes[prefabName] = ++value;
            }

            player.Reply($"Найдено {total} дверей в мире:");

            foreach (KeyValuePair<string, int> kvp in doorTypes.OrderByDescending(kv => kv.Value))
            {
                player.Reply($"- {kvp.Key}: {kvp.Value}");
            }
        }

        private void ChatCommandToggleDebug(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission("removedoors.admin"))
            {
                player.Reply("У вас нет прав для использования этой команды.");
                return;
            }

            config.DebugMode = !config.DebugMode;
            SaveConfig();
            player.Reply($"Режим отладки теперь {(config.DebugMode ? "включен" : "выключен")}");
        }

        private void ChatCommandProcess(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission("removedoors.admin"))
            {
                player.Reply("У вас нет прав для использования этой команды.");
                return;
            }

            player.Reply("Обработка всех дверей...");
            ProcessDoorsAggressively();
            player.Reply("Обработка дверей завершена.");
        }
    }
}
