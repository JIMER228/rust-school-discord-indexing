/*
 * Copyright (c) 2023 Bazz3l
 *
 * Bank Heist cannot be copied, edited and/or (re)distributed without the express permission of Bazz3l.
 * Discord bazz3l
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 *
 */

// Reference: Unity.AI.Navigation

using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Linq;
using System;
using Oxide.Plugins.BankHeistExtensionMethods;
using Oxide.Core.Plugins;
using Oxide.Core;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Network;
using ProtoBuf;
using Rust;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Oxide.Plugins
{
    [Info("Bank Heist", "Bazz3l", "1.5.21")]
    [Description("")]
    internal class BankHeist : RustPlugin
    {
        [PluginReference] Plugin NpcSpawn, PersonalVaultDoor;

        #region Fields
        
        private const int RAYCAST_LAYERS = Layers.Solid | Layers.Deploy;
        
        private const string WORKER_PERM = "bankheist.worker";
        private const string MANAGE_PERM = "bankheist.manage";
        
        private readonly HashSet<BaseEntity> _tempSpawned = new();
        private readonly HashSet<ulong> _players = new();
        private readonly object _falseObj = false;
        private readonly object _trueObj = true;
        private ConfigData _configData;
        private Coroutine _coroutine;

        private static BankHeist _instance;

        #endregion
        
        #region Local
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { LangEntry.NoPermission, "You don't have permission to use this command." },
                { LangEntry.PrefabsToSpawn, "Prefab list:\n{0}" },
                { LangEntry.InvalidArgs, "Invalid arguments:" },
                { LangEntry.InvalidArgument, "Invalid argument: {0} must be a {1}" },
                { LangEntry.InvalidCmd, "Invalid command: /{0} {1} {2}" },
                { LangEntry.InvalidLookPoint, "Invalid look point." },
                { LangEntry.EntityNotFound, "No entity found at the look position." },
                { LangEntry.MonumentNotFound, "No monument found by that name." },
                { LangEntry.SpawnEntryNotFound,  "No spawn entry found." },
                
                { LangEntry.BankAlreadyExists, "A bank already exists by that name." },
                { LangEntry.BankNotFound, "No bank found by that name." },
                { LangEntry.BankCreated, "Bank created successfully, if you wish to parent the event to a specific monument do so now and reload the plugin." },
                { LangEntry.BankRemoved, "Bank removed successfully!" },
                { LangEntry.BankUpdated, "Bank updated successfully!" },
                { LangEntry.BankParent, "Bank parent updated successfully, please reload the plugin." },
                { LangEntry.BankNotSpawned, "No bank spawned by that name." },
                { LangEntry.BankAttacked, "<color=#ca094c>WARNING!</color>!\n<color=#2ec8d0>{0}</color> is being attacked please send help!" },
                
                { LangEntry.ProfileAlreadyExists, "A profile already exists by that name." },
                { LangEntry.ProfileNotFound, "No profile found by that name." },
                { LangEntry.ProfileCreated, "Profile created successfully!" },
                { LangEntry.ProfileRemoved, "Profile removed successfully!" },
                { LangEntry.ProfileUpdated, "Profile updated successfully!" },
                
                { LangEntry.DebugInvalidSyntax, "<color=#2ec8d0>Debug</color>: invalid syntax, /bhm debug <bank-name> <door|loot|guard|info|everything>." },
                { LangEntry.DebugToggled, "<color=#2ec8d0>Debug</color>: has now been enabled for (<color=#2ec8d0>30</color>) seconds." },
                { LangEntry.DebugInvalidOption, "<color=#2ec8d0>Debug</color>: invalid option specified." },
                
                { LangEntry.EliminateGuards, "You must first eliminate all guards." },
                { LangEntry.OnUnauthorizedAccess, "You don't have access to this door, blowup the door to gain access." },
                { LangEntry.OnCratesLooted, "<color=#2ec8d0>{0}</color> has been robbed of all loot." },
                { LangEntry.OnGuardDeath, "<color=#2ec8d0>{0}</color>: security guard down <color=#2ec8d0>{1}</color>/<color=#2ec8d0>{2}</color> remaining!" },
                { LangEntry.OnDoorDeath, "<color=#2ec8d0>{0}</color>: security door breached!" },
                { LangEntry.OnEnterZone, "You have entered <color=#2ec8d0>{0}</color>" },
                { LangEntry.OnLeaveZone, "You have left <color=#2ec8d0>{0}</color>" },
                { LangEntry.OnCommandBlocked, "You cannot use this command from this location!" },
                { LangEntry.OnTeleportBlocked, "You cannot teleport from this location!" },
                { LangEntry.OnWorkerPreventLoot, "You cannot loot this container." },
                { LangEntry.OnBankReset, "<color=#2ec8d0>{0}</color> is now available." },
            }, this);
        }
        
        private static class LangEntry
        {
            public static readonly string NoPermission = "NoPermission";
            public static readonly string PrefabsToSpawn = "PrefabsToSpawn";
            public static readonly string InvalidArgs = "InvalidArgs";
            public static readonly string InvalidArgument = "InvalidArgument";
            public static readonly string InvalidCmd = "InvalidCmd";
            public static readonly string InvalidLookPoint = "InvalidLookPoint";
            public static readonly string MonumentNotFound = "MonumentNotFound";
            public static readonly string EntityNotFound = "EntityNotFound";
            public static readonly string SpawnEntryNotFound = "SpawnEntryNotFound";
            
            public static readonly string BankAlreadyExists = "BankAlreadyExists";
            public static readonly string BankNotFound = "BankNotFound";
            public static readonly string BankCreated = "BankCreated";
            public static readonly string BankRemoved = "BankRemoved";
            public static readonly string BankUpdated = "BankUpdated";
            public static readonly string BankParent = "BankParent";
            public static readonly string BankAttacked = "BankAttacked";
            public static readonly string BankNotSpawned = "BankNotSpawned";
            
            public static readonly string ProfileAlreadyExists = "ProfileAlreadyExists";
            public static readonly string ProfileNotFound = "ProfileNotFound";
            public static readonly string ProfileCreated = "ProfileCreated";
            public static readonly string ProfileRemoved = "ProfileRemoved";
            public static readonly string ProfileUpdated = "ProfileUpdated";
            
            public static readonly string DebugInvalidSyntax = "DebugInvalidSyntax";
            public static readonly string DebugInvalidOption = "DebugInvalidOption";
            public static readonly string DebugToggled = "DebugToggled";
            
            public static readonly string EliminateGuards = "EliminateGuards";
            public static readonly string OnUnauthorizedAccess = "OnUnauthorizedAccess";
            public static readonly string OnCratesLooted = "OnCratesLooted";
            public static readonly string OnGuardDeath = "OnGuardDeath";
            public static readonly string OnDoorDeath = "OnDoorDeath";
            public static readonly string OnEnterZone = "OnEnterZone";
            public static readonly string OnLeaveZone = "OnLeaveZone";
            public static readonly string OnCommandBlocked = "OnCommandBlocked";
            public static readonly string OnTeleportBlocked = "OnTeleportBlocked";
            public static readonly string OnWorkerPreventLoot = "OnWorkerPreventLoot";
            public static readonly string OnBankReset = "OnBankBankReset";
        }
        
        private string GetMessage(string langEntry, string userID = null, params object[] args)
        {
            return args?.Length > 0 ? string.Format(lang.GetMessage(langEntry, this, userID), args) : lang.GetMessage(langEntry, this, userID);
        }

        private void MessagePlayer(BasePlayer player, string langEntry, params object[] args)
        {
            if (player == null || !player.IsConnected) return;
            player.ChatMessage(GetMessage(langEntry, player.UserIDString, args));
        }
        
        private void MessagePlayers(string langEntry, params object[] args)
        {
            ConsoleNetwork.BroadcastToAllClients("chat.add", 2, 0, GetMessage(langEntry, null, args));
        }
        
        #endregion

        #region Config
        
        protected override void LoadDefaultConfig() => _configData = ConfigData.DefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _configData = Config.ReadObject<ConfigData>();
                if (_configData == null) throw new Exception();
                
                if (_configData.SpawnablePrefabs == null)
                {
                    _configData.SpawnablePrefabs = ConfigData.DefaultConfig().SpawnablePrefabs;
                    SaveConfig();
                }
            }
            catch (Exception e)
            {
                PrintWarning("Loaded default config: {0}", e.Message);
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_configData, true);

        private class ConfigData
        {
            [JsonProperty("enable notifications")] public bool EnableNotifications;
            [JsonProperty("enable visible bubble")] public bool EnableVisibleBubble;
            [JsonProperty("enable visible marker")] public bool EnableVisibleMarker;
            [JsonProperty("enable alarm trigger")] public bool EnableAlarmTrigger;
            [JsonProperty("enable eliminate guards")] public bool MustEliminateGuards;
            [JsonProperty("enable zone messages")] public bool EnableZoneMessages;
            [JsonProperty("prevent worker looting bank")] public bool PreventWorkerLootingBank;
            [JsonProperty("prevent teleport from bank")] public bool PreventTeleportFromBank;
            [JsonProperty("blocked zone commands")] public List<string> BlockedCommands;
            [JsonProperty("spawnable prefabs")] public Dictionary<string, string> SpawnablePrefabs;

            public static ConfigData DefaultConfig()
            {
                return new ConfigData
                {
                    EnableNotifications = true,
                    EnableVisibleBubble = true,
                    EnableVisibleMarker = true,
                    EnableAlarmTrigger = true,
                    EnableZoneMessages = true,
                    MustEliminateGuards = true,
                    PreventTeleportFromBank = false,
                    BlockedCommands = new List<string>(),
                    SpawnablePrefabs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "garage-door", "assets/prefabs/building/wall.frame.garagedoor/wall.frame.garagedoor.prefab" },
                        { "vault-door", "assets/bundled/prefabs/modding/asset_store/bankheist_package/bankheist_vol03/prefabs/door.vault.static.prefab" },
                        { "toptier-door", "assets/prefabs/building/door.hinged/door.hinged.toptier.prefab" },
                        { "toptier-d-door", "assets/prefabs/building/door.double.hinged/door.double.hinged.toptier.prefab" },
                        { "hackable-crate", "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab" },
                        { "elite-crate", "assets/bundled/prefabs/radtown/crate_elite.prefab" },
                        { "crate-normal", "assets/bundled/prefabs/radtown/crate_normal.prefab" },
                        { "crate-normal_2", "assets/bundled/prefabs/radtown/crate_normal_2.prefab" }
                    }
                };
            }
        }

        #endregion

        #region Bank Storage

        private class BaseStorage<T> where T : BaseStorage<T>, new()
        {
            public static T Data;
            public static string Filename;
            
            public static void LoadData(string filename)
            {
                Filename = filename;
                
                try
                {
                    Data = Interface.Oxide.DataFileSystem.ReadObject<T>(Filename);
                    if (Data == null) throw new Exception();
                }
                catch
                {
                    Interface.Oxide.LogDebug("Failed to load {0}; loaded default data.", Filename);
                    Data = new T();
                }
            }
            
            public static void SaveData()
            {
                if (Data == null) 
                    return;
                
                Interface.Oxide.DataFileSystem.WriteObject(Filename, Data);
            }

            public static void OnUnload()
            {
                Data = null;
                Filename = null;
            }
        }

        private class BankStorage : BaseStorage<BankStorage>
        {
            public List<BankEntry> BankEntries = new();
            
            public bool GetBankByName(string displayName, out BankEntry foundEntry)
            {
                for (int i = 0; i < BankEntries.Count; i++)
                {
                    BankEntry bankEntry = BankEntries[i];
                    if (bankEntry != null && bankEntry.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase))
                    {
                        foundEntry = bankEntry;
                        return true;
                    }
                }
                
                foundEntry = null;
                return false;
            }            
        }

        private class BankEntry
        {
            [JsonProperty("bank display name")] public string DisplayName;
            [JsonProperty("parent monument name")] public string MonumentParentName;
            [JsonProperty("time between resets")] public float TimeBetweenResets;
            [JsonProperty("alarm disable when all crates are looted")] public bool AlarmClear;
            [JsonProperty("alarm timeout seconds")] public float AlarmTimeoutSeconds;
            
            [JsonProperty("spawn position")] public Vector3 SpawnPosition;
            [JsonProperty("spawn rotation")] public Vector3 SpawnRotation;
            
            [JsonProperty("player detection origin")] public Vector3 PlayerDetectionOrigin;
            [JsonProperty("player detection radius")] public float PlayerDetectionRadius;
            
            [JsonProperty("map marker origin")] public Vector3 MapMarkerOrigin;
            [JsonProperty("map marker radius")] public float MapMarkerRadius;
            [JsonProperty("map marker opacity")] public float MapMarkerOpacity;
            [JsonProperty("map marker ready color")] public string MapMarkerReadyColor;
            [JsonProperty("map marker reset color")] public string MapMarkerResetColor;
            [JsonProperty("map marker outline color")] public string MapMarkerOuterColor;
            
            [JsonProperty("loot spawn group entries")] public List<SpawnEntry> LootSpawnGroupEntries;
            [JsonProperty("door spawn group entries")] public List<SpawnEntry> DoorSpawnGroupEntries;
            [JsonProperty("npcs spawn group entries")] public List<SpawnEntry> NpcSpawnGroupEntries;

            public BankEntry() {}

            public BankEntry(string displayName, Transform transform)
            {
                DisplayName = displayName;
                TimeBetweenResets = 1800f;
                
                SpawnPosition = transform.position;
                SpawnRotation = new Vector3(0.0f, transform.rotation.eulerAngles.y, 0.0f);
                
                PlayerDetectionOrigin = Vector3.zero;
                PlayerDetectionRadius = 100f;
                
                MapMarkerOrigin = Vector3.zero;
                MapMarkerRadius = 100f;
                MapMarkerOpacity = 0.6f;
                MapMarkerReadyColor = "#2eff74";
                MapMarkerResetColor = "#ff2e2e";
                MapMarkerOuterColor = "#ffffff";

                LootSpawnGroupEntries = new List<SpawnEntry>();
                DoorSpawnGroupEntries = new List<SpawnEntry>();
                NpcSpawnGroupEntries = new List<SpawnEntry>();
            }

            public IEnumerator SpawnBankInstance()
            {
                Vector3 spawnPosition = SpawnPosition;
                Vector3 spawnRotation = SpawnRotation;
                
                if (!string.IsNullOrEmpty(MonumentParentName))
                {
                    Transform transform = MonumentCache.FindMonumentByName(MonumentParentName)?.GetTransform();;
                    if (transform == null)
                    {
                        Interface.Oxide.LogDebug("Failed to perform setup: missing parent transform for ({0}) assigned to ({1}).", MonumentParentName, DisplayName);
                        yield break;
                    }

                    spawnPosition = transform.position;
                    spawnRotation = transform.rotation.eulerAngles;
                }
                
                if (LootSpawnGroupEntries == null || 
                    DoorSpawnGroupEntries == null || 
                    NpcSpawnGroupEntries == null)
                {
                    Interface.Oxide.LogDebug("Failed to perform setup: missing spawn group found in data file for ({0}).", DisplayName);
                    yield break;
                }
                
                // Create bank component
                BankHeistInstance component = CustomUtils.CreateObjectWithComponent<BankHeistInstance>(spawnPosition, Quaternion.Euler(spawnRotation), "BANK_CONTROLLER_NAME");
                
                // Cache instance
                BankHeistInstance.CacheHeistInstance(component);
                
                // General settings
                component.requiresReset = true;
                component.displayName = DisplayName;
                component.monumentName = MonumentParentName;
                component.timeBetweenResets = TimeBetweenResets;
                component.alarmTime = AlarmTimeoutSeconds;
                component.clearAlarm = AlarmClear;
                
                // Player detection origin and radius
                component.playerDetectionOrigin = component.transform.TransformPoint(PlayerDetectionOrigin);
                component.playerDetectionRadius = PlayerDetectionRadius / 2;
                
                // Player detection sphere collider
                SphereCollider sphereCollider = component.gameObject.AddComponent<SphereCollider>();
                sphereCollider.isTrigger = true;
                sphereCollider.radius = PlayerDetectionRadius / 2;
                sphereCollider.center = PlayerDetectionOrigin;
                
                // Map marker origin and radius
                component.markerOrigin = component.transform.TransformPoint(MapMarkerOrigin);
                component.markerReadyColor = ColorUtility.TryParseHtmlString(MapMarkerReadyColor, out Color readyColor) ? readyColor : Color.green;
                component.markerResetColor = ColorUtility.TryParseHtmlString(MapMarkerResetColor, out Color resetColor) ? resetColor : Color.red;
                component.markerOuterColor = ColorUtility.TryParseHtmlString(MapMarkerOuterColor, out Color outerColor) ? outerColor : Color.white;
                component.markerRadius = MapMarkerRadius / 2;
                component.markerOpacity = MapMarkerOpacity;
                
                // Loot spawn group
                component.lootSpawnGroup = component.gameObject.AddComponent<LootSpawnGroup>();
                component.lootSpawnGroup.spawnPointEntries.AddRange(LootSpawnGroupEntries ?? new List<SpawnEntry>());
                component.lootSpawnGroup.bankHeistInstance = component;

                // Door spawn group
                component.doorSpawnGroup = component.gameObject.AddComponent<DoorSpawnGroup>();
                component.doorSpawnGroup.spawnPointEntries.AddRange(DoorSpawnGroupEntries ?? new List<SpawnEntry>());
                component.doorSpawnGroup.bankHeistInstance = component;

                // Guard spawn group
                component.guardSpawnGroup = component.gameObject.AddComponent<GuardSpawnGroup>();
                component.guardSpawnGroup.spawnPointEntries.AddRange(NpcSpawnGroupEntries ?? new List<SpawnEntry>());
                component.guardSpawnGroup.bankHeistInstance = component;
                
                // Initialize event
                yield return component.Initialize();
            }
        }

        private class SpawnEntry
        {
            public int Id;
            public string Prefab;
            public string Profile;
            public Vector3 Position;
            public Vector3 Rotation;

            public SpawnEntry() { }

            public SpawnEntry(int id, string prefab, string profile, Vector3 position, Vector3 rotation, ulong skinID = 0UL)
            {
                Id = id;
                Prefab = prefab;
                Profile = profile;
                Position = position;
                Rotation = rotation;
            }

            public T CreateEntity<T>(Transform transform) where T : BaseEntity
            {
                Vector3 position = transform.TransformPoint(Position);
                Vector3 rotation = Rotation;
                rotation.y = transform.rotation.eulerAngles.y + rotation.y;
                return (T)GameManager.server.CreateEntity(Prefab, position, Quaternion.Euler(rotation), false);
            }
            
            public T CreateGuard<T>(Transform transform, GuardProfileEntry profileEntry) where T : BaseEntity
            {
                Vector3 position = transform.TransformPoint(Position);
                
                if (profileEntry.Parsed == null)
                    profileEntry.CacheConfig();
                
                return (T)_instance?.NpcSpawn?.Call("SpawnNpc", position, profileEntry.Parsed);
            }
        }
        
        #endregion
        
        #region Guard Profile Storage
        
        private class GuardProfileStorage : BaseStorage<GuardProfileStorage>
        {
            public Dictionary<string, GuardProfileEntry> ProfileEntries = new(StringComparer.OrdinalIgnoreCase);
            
            public bool GetProfileByName(string profileName, out GuardProfileEntry profileEntry)
            {
                if (string.IsNullOrEmpty(profileName))
                {
                    profileEntry = null;
                    return false;
                }
                
                return ProfileEntries.TryGetValue(profileName, out profileEntry);
            }
            
            public static GuardProfileEntry DefaultProfile()
            {
                return new GuardProfileEntry
                {
                    Name = "Bank Guard",
                    WearItems = new List<GuardProfileEntry.WearEntry>
                    {
                        new()
                        {
                            ShortName = "hazmatsuit_scientist_peacekeeper",
                            SkinID = 0UL
                        }
                    },
                    BeltItems = new List<GuardProfileEntry.BeltEntry>
                    {
                        new()
                        {
                            ShortName = "smg.mp5",
                            Amount = 1,
                            SkinID = 0UL,
                            Ammo = null,
                            Mods = new List<string>()
                        },
                        new()
                        {
                            ShortName = "syringe.medical",
                            Amount = 10,
                            SkinID = 0UL,
                            Ammo = null,
                            Mods = new List<string>()
                        },
                    },
                    Kit = "",
                    Health = 250f,
                    RoamRange = 5f,
                    ChaseRange = 25f,
                    SenseRange = 75f,
                    ListenRange = 75f / 2,
                    AttackRangeMultiplier = 8f,
                    CheckVisionCone = false,
                    VisionCone = 180f,
                    DamageScale = 1f,
                    TurretDamageScale = 0.25f,
                    AimConeScale = 0.35f,
                    DisableRadio = false,
                    CanRunAwayWater = true,
                    CanSleep = false,
                    Speed = 8.5f,
                    HomePosition = string.Empty,
                    MemoryDuration = 30f,
                    Stationary = false,
                    UseUnderGround = false
                };
            }
        }
        
        private class GuardProfileEntry
        {
            public string Name;
            public List<WearEntry> WearItems;
            public List<BeltEntry> BeltItems;
            public string Kit;
            public float Health;
            public float RoamRange;
            public float ChaseRange;
            public float SenseRange;
            public float ListenRange;
            public float AttackRangeMultiplier;
            public bool CheckVisionCone;
            public float VisionCone;
            public bool HostileTargetsOnly;
            public float DamageScale;
            public float TurretDamageScale;
            public float AimConeScale;
            public bool DisableRadio;
            public bool CanRunAwayWater;
            public bool CanSleep;
            public float SleepDistance;
            public float Speed;
            public int AreaMask;
            public int AgentTypeID;
            [JsonIgnore]
            public string HomePosition;
            public float MemoryDuration;
            public bool Stationary;
            public bool UseUnderGround;
            public HashSet<string> States;
            
            [JsonIgnore]
            public JObject Parsed;

            public class BeltEntry
            {
                public string ShortName; 
                public int Amount; 
                public ulong SkinID; 
                public List<string> Mods; 
                public string Ammo;
                
                public static BeltEntry SerializeItem(Item item)
                {
                    BeltEntry wearEntry = new BeltEntry
                    {
                        ShortName = item.info.shortname,
                        SkinID = item.skin,
                        Amount = item.amount,
                        Mods = new List<string>()
                    };
                    
                    if (item.GetHeldEntity() is BaseProjectile projectile && projectile.primaryMagazine?.ammoType != null)
                        wearEntry.Ammo = projectile.primaryMagazine.ammoType.shortname;

                    if (item.contents?.itemList == null) 
                        return wearEntry;
                    
                    foreach (Item itemContent in item.contents.itemList)
                        wearEntry.Mods.Add(itemContent.info.shortname);
                    
                    return wearEntry;
                }
            }

            public class WearEntry
            {
                public string ShortName; 
                public ulong SkinID;

                public static WearEntry SerializeItem(Item item)
                {
                    return new WearEntry
                    {
                        ShortName = item.info.shortname,
                        SkinID = item.skin
                    };
                }
            }
            
            public void CacheConfig()
            {
                Parsed = new JObject
                {
                    ["Name"] = Name,
                    ["WearItems"] = new JArray { WearItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["SkinID"] = x.SkinID }) },
                    ["BeltItems"] = new JArray { BeltItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["Amount"] = x.Amount, ["SkinID"] = x.SkinID, ["Mods"] = new JArray { x.Mods }, ["Ammo"] = x.Ammo }) },
                    ["Kit"] = Kit,
                    ["Health"] = Health,
                    ["RoamRange"] = RoamRange,
                    ["ChaseRange"] = ChaseRange,
                    ["SenseRange"] = SenseRange,
                    ["ListenRange"] = SenseRange / 2f,
                    ["AttackRangeMultiplier"] = AttackRangeMultiplier,
                    ["CheckVisionCone"] = CheckVisionCone,
                    ["VisionCone"] = VisionCone,
                    ["HostileTargetsOnly"] = HostileTargetsOnly,
                    ["DamageScale"] = DamageScale,
                    ["TurretDamageScale"] = TurretDamageScale,
                    ["AimConeScale"] = AimConeScale,
                    ["DisableRadio"] = DisableRadio,
                    ["CanRunAwayWater"] = CanRunAwayWater,
                    ["CanSleep"] = CanSleep,
                    ["SleepDistance"] = SleepDistance,
                    ["Speed"] = Speed,
                    ["AreaMask"] = !UseUnderGround ? 1 : 25,
                    ["AgentTypeID"] = !UseUnderGround ? -1372625422 : 0,
                    ["HomePosition"] = string.Empty,
                    ["MemoryDuration"] = MemoryDuration,
                    ["States"] = new JArray
                    { 
                        Stationary 
                            ? new HashSet<string> { "IdleState", "CombatStationaryState" }
                            : new HashSet<string> { "RoamState", "ChaseState", "CombatState", "RaidState" }
                    }
                };
            }
        }

        #endregion
        
        #region Loot Profile Storage

        private class LootProfileStorage : BaseStorage<LootProfileStorage>
        {
            public Dictionary<string, LootProfileEntry> ProfileEntries = new(StringComparer.OrdinalIgnoreCase);
            
            public bool GetProfileByName(string profileName, out LootProfileEntry foundEntry)
            {
                if (string.IsNullOrEmpty(profileName))
                {
                    foundEntry = null;
                    return false;
                }
                
                return ProfileEntries.TryGetValue(profileName, out foundEntry);
            }
            
            public static LootProfileEntry DefaultProfile()
            {
                return new LootProfileEntry
                {
                    MinItems = 1,
                    MaxItems = 1,
                    SkinId = 0UL,
                    HackableSeconds = 300f,
                    LootItems = new List<LootProfileEntry.LootItem>()
                };
            }
        }
        
        private class LootProfileEntry
        {
            public int MinItems = 1;
            public int MaxItems = 1;
            public ulong SkinId = 0UL;
            public float HackableSeconds = 300f;
            public List<LootItem> LootItems = new();
            
            public class LootItem
            {
                public string Shortname;
                public string ItemName = string.Empty;
                public ulong SkinID;
                public int MinAmount = 1;
                public int MaxAmount = 1;
                public bool IsBlueprint;
                
                [JsonIgnore]
                private int _itemId = -1;

                [JsonIgnore]
                public int ItemID
                {
                    get
                    {
                        if (_itemId == -1)
                            _itemId = ItemManager.FindItemDefinition(Shortname)?.itemid ?? -1;
                        
                        return _itemId;
                    }
                }
                
                public LootItem() { }

                public LootItem(Item item)
                {
                    Shortname = item.info.shortname;
                    ItemName = item.name;
                    SkinID = item.skin;
                    MinAmount = item.amount;
                    MaxAmount = item.amount;
                    IsBlueprint = item.blueprintTarget != 0;
                }

                public void CreateItem(LootContainer container)
                {
                    if (ItemID == -1)
                    {
                        _instance?.Puts($"[BankHeist] - Failed to find ItemDefinition for {Shortname}!");
                        return;
                    }

                    Item item;
                    
                    if (IsBlueprint)
                    {
                        item = ItemManager.CreateByItemID(-996920608, UnityEngine.Random.Range(MinAmount, MaxAmount), SkinID);
                        item.blueprintTarget = ItemID;
                    }
                    else
                    {
                        item = ItemManager.CreateByItemID(ItemID, UnityEngine.Random.Range(MinAmount, MaxAmount), SkinID);
                        item.name = ItemName;
                    }

                    if (!item.MoveToContainer(container.inventory))
                        item.Remove();
                }
            }

            public void GenerateContainerItems(ref List<LootItem> items, int maxItemCount)
            {
                for (int i = 0; i < 100; i++)
                {
                    LootItem lootItem = LootItems[UnityEngine.Random.Range(0, LootItems.Count)];
                    if (!items.Contains(lootItem))
                        items.Add(lootItem);
                    
                    if (items.Count >= maxItemCount)
                        break;
                }
            }

            public void PopulateContainer(LootContainer container, int itemCount)
            {
                if (LootItems == null || LootItems.Count == 0)
                    return;
                
                List<LootItem> items = Facepunch.Pool.Get<List<LootItem>>();

                try
                {
                    container.inventory.SafeClear();
                    GenerateContainerItems(ref items, Mathf.Clamp(itemCount, 1, 24));
                    container.inventory.capacity = items.Count;

                    foreach (LootItem lootItem in items)
                        lootItem.CreateItem(container);
                }
                catch (Exception e)
                { 
                    //
                }
                
                Facepunch.Pool.FreeUnmanaged<LootItem>(ref items);
            }
        }

        #endregion
        
        #region Door Profile Storage

        private class DoorProfileStorage : BaseStorage<DoorProfileStorage>
        {
            public Dictionary<string, DoorProfileEntry> ProfileEntries = new(StringComparer.OrdinalIgnoreCase);
            
            public bool GetProfileByName(string profileName, out DoorProfileEntry foundEntry)
            {
                if (string.IsNullOrEmpty(profileName))
                {
                    foundEntry = null;
                    return false;
                }
                
                return ProfileEntries.TryGetValue(profileName, out foundEntry);
            }
            
            public static DoorProfileEntry DefaultProfile()
            {
                return new DoorProfileEntry
                {
                    Health = 1000f,
                    SkinId = 0UL,
                };
            }
        }
        
        private class DoorProfileEntry
        {
            public float Health;
            public ulong SkinId;
        }

        #endregion

        #region Oxide Hooks

        private void OnServerInitialized()
        {
            cmd.AddChatCommand("blp", this, nameof(LootProfileCommand));
            cmd.AddChatCommand("bdp", this, nameof(DoorProfileCommand));
            cmd.AddChatCommand("bgp", this, nameof(GuardProfileCommand));
            cmd.AddChatCommand("bse", this, nameof(SpawnEntityCommand));
            cmd.AddChatCommand("bhm", this, nameof(BankManageCommand));
            
            EntitiesCache.Initialize();
            MonumentCache.Initialize();
            
            PerformSetupRoutine();
            
            if (_configData.BlockedCommands.Count > 0)
                _hooks.Add("OnServerCommand");
            
            SubscribeToHooks(true);
        }

        private void Init()
        {
            permission.RegisterPermission(WORKER_PERM, this);
            permission.RegisterPermission(MANAGE_PERM, this);
            
            _instance = this;
            
            GuardProfileStorage.LoadData("BankHeist/GuardProfileStorage");
            LootProfileStorage.LoadData("BankHeist/LootProfileStorage");
            DoorProfileStorage.LoadData("BankHeist/DoorProfileStorage");
            BankStorage.LoadData("BankHeist/BankStorage");
            
            SubscribeToHooks();
        }

        private void Unload()
        {
            DestroySetupRoutine();
            
            BankHeistInstance.OnUnload();
            MonumentCache.OnUnload();
            EntitiesCache.OnUnload();
            
            BankStorage.OnUnload();
            GuardProfileStorage.OnUnload();
            LootProfileStorage.OnUnload();
            DoorProfileStorage.OnUnload();
        }
        
        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player != null)
            {
                foreach (string blockedCommand in _configData.BlockedCommands)
                {
                    if (arg.FullString.EndsWith(blockedCommand, StringComparison.OrdinalIgnoreCase))
                    {
                        MessagePlayer(player, LangEntry.OnCommandBlocked);
                        return true;
                    }
                }                
            }

            return null;
        }
        
        private void OnEntityTakeDamage(ScientistNPC npc, HitInfo info)
        {
            if (npc == null || info?.InitiatorPlayer == null || !info.InitiatorPlayer.IsPlayer()) 
                return;
            
            EntitiesCache.GetBankByEntity(npc.net.ID)?.OnBankAttacked(info.InitiatorPlayer);
        }
        
        private void OnEntityTakeDamage(Door door, HitInfo info)
        {
            if (door == null || door.isSecurityDoor || info?.InitiatorPlayer == null || !info.InitiatorPlayer.IsPlayer())
                return;
            
            if (!info.IsMajorityDamage(DamageType.Explosion)) 
                return;
            
            EntitiesCache.GetBankByEntity(door.net.ID)?.OnBankAttacked(info.InitiatorPlayer);
        }
        
        private void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || !player.IsPlayer())
                return;
            
            _players.Remove(player.userID);
        }
        
        private void OnEntityDeath(ScientistNPC npc, HitInfo info)
        {
            if (npc == null) 
                return;
            
            EntitiesCache.GetBankByEntity(npc.net.ID)?.OnEntityDeath(npc, info?.InitiatorPlayer);
        }
        
        private void OnEntityKill(ScientistNPC npc)
        {
            if (npc == null) 
                return;
            
            OnEntityDeath(npc, null);
        }

        private void OnEntityDeath(Door door, HitInfo info) 
        {
            if (door == null)
                return;
            
            EntitiesCache.GetBankByEntity(door.net.ID)?.OnEntityDeath(door, info?.InitiatorPlayer);
        }

        private void OnEntityKill(Door door)
        {
            if (door == null) 
                return;
            
            OnEntityDeath(door, null);
        }

        private void OnEntityKill(LootContainer container)
        {
            if (container == null) 
                return;
            
            EntitiesCache.GetBankByEntity(container.net.ID)?.OnEntityDeath(container);
        }

        private void OnLootEntityEnd(BasePlayer player, LootContainer container)
        {
            if (container == null) 
                return;
            
            EntitiesCache.GetBankByEntity(container.net.ID)?.OnLootEntityEnd(player, container);
        }

        private object CanLootEntity(BasePlayer player, LootContainer container)
        {
            return EntitiesCache.GetBankByEntity(container.net.ID)
                ?.CanLootEntity(player);
        }

        private object CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            return EntitiesCache.GetBankByEntity(crate.net.ID)
                ?.CanHackCrate(player, crate);
        }

        private bool? CanUseLockedEntity(BasePlayer player, CodeLock codeLock)
        {
            if (codeLock.GetParentEntity() is Door entity)
            {
                BankHeistInstance instance = EntitiesCache.GetBankByEntity(entity.net.ID);
                if (instance != null && instance.CanUseLockedEntity(player, codeLock)) 
                    return true;
                
                return null;
            }
            
            return null;
        }
        
        #endregion
        
        #region Bank Heist Instance

        private void PerformSetupRoutine()
        {
            if (_coroutine != null) return;
            _coroutine = ServerMgr.Instance.StartCoroutine(SpawnBankInstances());
        }

        private void DestroySetupRoutine()
        {
            if (_coroutine != null) 
                ServerMgr.Instance.StopCoroutine(_coroutine);
            
            _coroutine = null;
        }

        private IEnumerator SpawnBankInstances()
        {
            yield return CoroutineEx.waitForSeconds(10f);
            
            Puts("Initializing banks...");
            
            Stopwatch stopwatch = Stopwatch.StartNew();
            
            foreach (BankEntry bankEntry in BankStorage.Data.BankEntries)
                yield return bankEntry.SpawnBankInstance();
            
            stopwatch.Stop();

            Puts($"Finished spawning banks: {stopwatch.ElapsedMilliseconds}ms.");
            
            _coroutine = null;
        }

        private class BankHeistInstance : FacepunchBehaviour
        {
            public static readonly string DebugText = "<size=24>Bank Information</size>\n----------------------------------\n<size=16>Display Name: {0}\nMonument Parent Name: {1}\nTime Between Resets: {2}\nCrates: {3} | Doors: {4} | Guards: {5}</size>";
            public static List<BankHeistInstance> BankHeistInstances = new();
            
            private List<SphereEntity> _sphereInstances = new();
            private MapMarkerGenericRadius _markerInstance;
            private IOEntity _alarmInstance;
            
            public GuardSpawnGroup guardSpawnGroup;
            public LootSpawnGroup lootSpawnGroup;
            public DoorSpawnGroup doorSpawnGroup;
            
            public string displayName;
            public string monumentName;
            
            public Vector3 playerDetectionOrigin;
            public float playerDetectionRadius;
            
            public Vector3 markerOrigin;
            public Color markerReadyColor;
            public Color markerResetColor;
            public Color markerOuterColor;
            public float markerRadius;
            public float markerOpacity;
            
            public float timeBetweenResets = 3600.0f;
            public float resetTickTime = 10.0f;
            public float resetTimeElapsed;
            
            public float alarmTime;
            public bool clearAlarm;
            
            public bool requiresReset;
            public bool lootedEvery;
            
            public float lastCooldownTime = 60f;
            public float lastAttackedTime;
            public float SecondsSinceAttacked => lastAttackedTime - Time.realtimeSinceStartup;
            
            public static void OnUnload()
            {
                if (Rust.Application.isQuitting)
                    return;

                for (int i = BankHeistInstances.Count - 1; i >= 0; i--)
                    BankHeistInstances[i]?.Dispose();
                
                BankHeistInstances.Clear();
            }
            
            public static bool FindBankByName(string displayName, out BankHeistInstance heistInstance)
            {
                heistInstance = BankHeistInstances.Find(x => x.displayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));
                return heistInstance != null;
            }
            
            public static void CacheHeistInstance(BankHeistInstance bankHeistInstance) => BankHeistInstances.Add(bankHeistInstance);
            
            public static void RemoveHeistInstance(BankHeistInstance bankHeistInstance)
            {
                BankHeistInstances.Remove(bankHeistInstance);
                UnityEngine.GameObject.Destroy(bankHeistInstance.gameObject);
            }

            #region Unity

            private void OnTriggerEnter(Collider other)
            {
                BasePlayer player = other?.GetComponentInParent<BasePlayer>();
                if (player == null || !player.userID.IsSteamId()) 
                    return;
                
                _instance._players.Add(player.userID);
                
                if (_instance._configData.EnableZoneMessages)
                    _instance.MessagePlayer(player, LangEntry.OnEnterZone, displayName);
            }

            private void OnTriggerExit(Collider other)
            {
                BasePlayer player = other?.GetComponentInParent<BasePlayer>();
                if (player == null || !player.userID.IsSteamId()) 
                    return;
                
                _instance._players.Remove(player.userID);
                
                if (_instance._configData.EnableZoneMessages)
                    _instance?.MessagePlayer(player, LangEntry.OnLeaveZone, displayName);
            }

            #endregion

            #region Setup / Destroy

            public IEnumerator Initialize()
            {
                CreateSpheres();
                CreateMarkers();
                CreateAlarm();
                
                ResetTimer();
                ResetEvent();
                DebugInfo(DebugMode.Info);
                
                yield return CoroutineEx.waitForEndOfFrame;
            }
            
            public void Dispose()
            {
                StopAllCoroutines();
                CancelInvoke();
                
                DestroySpheres();
                DestroyMarkers();
                DestroyAlarm();
                DestroySpawnGroups();
                
                BankHeistInstance.RemoveHeistInstance(this);
            }

            #endregion
            
            #region Event Management

            private void ResetTick()
            {
                bool hasNearbyPlayers = ActiveNearbyPlayers();
                if (!hasNearbyPlayers)
                    resetTimeElapsed += resetTickTime;
                
                if (resetTimeElapsed <= timeBetweenResets) 
                    return;

                if (hasNearbyPlayers)
                    return;

                resetTimeElapsed = 0.0f;
                ResetEvent();
            }

            public void ResetTimer()
            {
                resetTimeElapsed = 0.0f;
                CancelInvoke(ResetTick);
                InvokeRandomized(ResetTick, UnityEngine.Random.Range(0.0f, 1f), resetTickTime, 0.5f);
            }

            public void ResetEvent()
            {
                if (!requiresReset) 
                    return;
                
                requiresReset = false;
                lootedEvery = false;
                
                RemoveNearbySleepers();
                StopAlarm();
                UpdateMarker();
                RefillSpawnGroups();
                
                _instance?.MessagePlayers(LangEntry.OnBankReset, displayName);
                
                Interface.Oxide.CallHook("OnBankHeistReset", displayName);
            }

            private bool HasGuardsRemaining()
            {
                return guardSpawnGroup != null && guardSpawnGroup.spawnInstances.Count > 0;
            }

            private bool ActiveNearbyPlayers()
            {
                if (BasePlayer.activePlayerList != null && 
                    BasePlayer.activePlayerList.Count > 0)
                {
                    for (int i = BasePlayer.activePlayerList.Count - 1; i >= 0; --i)
                    {
                        BasePlayer player = BasePlayer.activePlayerList[i];
                        if (player.IsValid() && player.IsWithinRadius(playerDetectionOrigin, playerDetectionRadius) && !player.HasPermission(WORKER_PERM))
                            return true;
                    }
                }

                return false;
            }

            private void RemoveNearbySleepers()
            {
                if (BasePlayer.sleepingPlayerList == null || 
                    BasePlayer.sleepingPlayerList.Count == 0) 
                    return;
                
                for (int i = BasePlayer.sleepingPlayerList.Count - 1; i >= 0; --i)
                {
                    BasePlayer player = BasePlayer.sleepingPlayerList[i];
                    if (player.IsValid() && player.IsWithinRadius(playerDetectionOrigin, playerDetectionRadius) && !player.HasPermission(WORKER_PERM))
                        player.Hurt(1000f, DamageType.Suicide, player, false);
                }
            }

            #endregion

            #region Spawn Groups

            private void RefillSpawnGroups()
            {
                lootSpawnGroup.Clear();
                doorSpawnGroup.Clear();
                guardSpawnGroup.Clear();
                
                StartCoroutine(UpdateSpawnGroups());
            }

            private void DestroySpawnGroups()
            {
                StopCoroutine(UpdateSpawnGroups());
                
                lootSpawnGroup.Clear();
                doorSpawnGroup.Clear();
                guardSpawnGroup.Clear();
                
                if (lootSpawnGroup.gameObject != null)
                    GameObject.Destroy(lootSpawnGroup.gameObject);
                if (doorSpawnGroup.gameObject != null)
                    GameObject.Destroy(doorSpawnGroup.gameObject);
                if (guardSpawnGroup.gameObject != null)
                    GameObject.Destroy(guardSpawnGroup.gameObject);
            }
            
            private IEnumerator UpdateSpawnGroups()
            {
                yield return CoroutineEx.waitForSeconds(2.5f);
                
                yield return lootSpawnGroup.Spawn();
                yield return doorSpawnGroup.Spawn();
                yield return guardSpawnGroup.Spawn();
            }

            #endregion

            #region Spheres

            private void CreateSpheres()
            {
                if (!_instance._configData.EnableVisibleBubble) 
                    return;
                
                for (int i = 0; i < 5; i++)
                {
                    SphereEntity sphere = (SphereEntity)GameManager.server.CreateEntity("assets/prefabs/visualization/sphere.prefab", playerDetectionOrigin);
                    sphere.EnableSaving(false);
                    sphere.currentRadius = 1f;
                    sphere.Spawn();
                    sphere.LerpRadiusTo(playerDetectionRadius * 2, playerDetectionRadius * 0.75f);
                    
                    _sphereInstances.Add(sphere);
                }
            }

            private void DestroySpheres()
            {
                for (int i = _sphereInstances.Count - 1; i >= 0; i--)
                {
                    BaseEntity entity = _sphereInstances[i];
                    if (entity != null && !entity.IsDestroyed)
                        entity.Kill();
                }
                
                _sphereInstances.Clear();
            }

            #endregion

            #region Markers
            
            private void CreateMarkers()
            {
                if (!_instance._configData.EnableVisibleMarker) 
                    return;
                
                _markerInstance = (MapMarkerGenericRadius)GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", markerOrigin, Quaternion.identity);
                _markerInstance.EnableSaving(false);
                _markerInstance.color1 = markerReadyColor;
                _markerInstance.color2 = markerOuterColor;
                _markerInstance.radius = markerRadius / 145f;
                _markerInstance.alpha = markerOpacity;
                _markerInstance.Spawn();
                
                InvokeRepeating(UpdateMarker, 1f, 60f);
            }

            private void DestroyMarkers()
            {
                if (_markerInstance == null || _markerInstance.IsDestroyed) 
                    return;
                
                _markerInstance.Kill();
                _markerInstance = null;
            }

            private void UpdateMarker()
            {
                if (_markerInstance == null || _markerInstance.IsDestroyed) 
                    return;
                
                _markerInstance.color1 = requiresReset ? markerResetColor : markerReadyColor;
                _markerInstance.SendUpdate();
            }

            #endregion

            #region Alarm
            
            private void CreateAlarm()
            {
                if (!_instance._configData.EnableAlarmTrigger) 
                    return;
                
                _alarmInstance = (IOEntity)GameManager.server.CreateEntity("assets/prefabs/io/electric/other/alarmsound.prefab", transform.position, Quaternion.identity);
                _alarmInstance.EnableSaving(false);
                _alarmInstance.Spawn();
            }

            private void DestroyAlarm()
            {
                if (_alarmInstance == null || _alarmInstance.IsDestroyed) 
                    return;
                
                _alarmInstance.Kill();
                _alarmInstance = null;
            }

            private void StartAlarm()
            {
                if (!_alarmInstance.IsValid() || _alarmInstance.HasFlag(BaseEntity.Flags.Reserved8)) 
                    return;
                
                _alarmInstance.SetFlag(BaseEntity.Flags.Reserved8, true);
                
                if (alarmTime == 0.0f) 
                    return;
                
                _alarmInstance.Invoke(new Action(StopAlarm), alarmTime);
            }
            
            private void StopAlarm()
            {
                if (!_alarmInstance.IsValid() || !_alarmInstance.HasFlag(BaseEntity.Flags.Reserved8)) 
                    return;
                
                _alarmInstance.SetFlag(BaseEntity.Flags.Reserved8, false);
                _alarmInstance.CancelInvoke(new Action(StopAlarm));
            }

            private void CheckAlarm()
            {
                if (!_instance._configData.EnableNotifications) 
                    return;
                
                if (!(SecondsSinceAttacked <= 0.0f))
                    return;
                
                StartAlarm();
                
                _instance.MessagePlayers(LangEntry.BankAttacked, displayName);
            }

            #endregion
            
            #region Oxide Hooks
            
            public void OnBankAttacked(BasePlayer player)
            {
                resetTimeElapsed = 0.0f;

                if (!requiresReset)
                {
                    requiresReset = true;
                    UpdateMarker();
                }
                
                CheckAlarm();
                lastAttackedTime = Time.realtimeSinceStartup + lastCooldownTime;
            }

            public void OnEntityDeath(BaseEntity entity, BasePlayer player = null)
            {
                EntitiesCache.RemoveEntity(entity.net.ID);

                if (!_instance._configData.EnableNotifications) 
                    return;
                
                switch (entity)
                {
                    case ScientistNPC _:
                        _instance.MessagePlayer(player, LangEntry.OnGuardDeath, displayName, (guardSpawnGroup.SpawnPointInstanceTotal - 1), guardSpawnGroup.SpawnPointEntriesTotal);
                        break;
                    case Door _:
                        _instance.MessagePlayer(player, LangEntry.OnDoorDeath, displayName);
                        break;
                }
            }

            public void OnLootEntityEnd(BasePlayer player, LootContainer container)
            {
                if (lootedEvery || lootSpawnGroup.SpawnPointInstanceTotal > 1)
                    return;
                
                lootedEvery = true;
                
                if (clearAlarm) 
                    StopAlarm();
                
                _instance.MessagePlayer(player, LangEntry.OnCratesLooted, displayName);
            }
            
            public object CanHackCrate(BasePlayer player, HackableLockedCrate crate)
            {
                if (_instance._configData.PreventWorkerLootingBank && player.HasPermission(WORKER_PERM))
                {
                    _instance.MessagePlayer(player, LangEntry.OnWorkerPreventLoot);
                    return true;
                }
                
                if (_instance._configData.MustEliminateGuards && HasGuardsRemaining())
                {
                    _instance.MessagePlayer(player, LangEntry.EliminateGuards);
                    return true;                    
                }
                
                return null;
            }

            public object CanLootEntity(BasePlayer player)
            {
                if (_instance._configData.PreventWorkerLootingBank && player.HasPermission(WORKER_PERM))
                {
                    _instance.MessagePlayer(player, LangEntry.OnWorkerPreventLoot);
                    return true;
                }
                
                if (_instance._configData.MustEliminateGuards && HasGuardsRemaining())
                {
                    _instance.MessagePlayer(player, LangEntry.EliminateGuards);
                    return true;
                }
                
                return null;
            }
            
            public bool CanUseLockedEntity(BasePlayer player, CodeLock codeLock)
            {
                if (player.HasPermission(WORKER_PERM))
                    return true;
                
                _instance.MessagePlayer(player, LangEntry.OnUnauthorizedAccess);
                Effect.server.Run(codeLock.effectDenied.resourcePath, codeLock, 0U, Vector3.zero, Vector3.forward);
                return false;
            }

            #endregion

            #region Debug Information

            public void DebugInfo(DebugMode mode, float seconds = 30f)
            {
                List<Connection> connections = Facepunch.Pool.Get<List<Connection>>();
                
                try
                {
                    connections.AddRange(Net.sv.connections
                        .Where(x => x.active && x.authLevel != 0));
                    
                    if (mode.HasFlag(DebugMode.Info))
                    {
                        string debugMessage = string.Format(BankHeistInstance.DebugText, displayName, (monumentName ?? "N/A"), timeBetweenResets, lootSpawnGroup.SpawnPointEntriesTotal, doorSpawnGroup.SpawnPointEntriesTotal, guardSpawnGroup.SpawnPointEntriesTotal);
                        
                        CustomUtils.DrawText(connections, transform.position + (Vector3.up * 100f), debugMessage, Color.white, seconds);
                        CustomUtils.DrawSphere(connections, transform.position, 1f, Color.red, seconds);
                        CustomUtils.DrawSphere(connections, playerDetectionOrigin, playerDetectionRadius, Color.magenta, seconds);
                        CustomUtils.DrawSphere(connections, markerOrigin, markerRadius, Color.yellow, seconds);
                        
                        debugMessage = null;
                    }
                    
                    if (mode.HasFlag(DebugMode.Loot))
                        lootSpawnGroup.DisplayInfo(connections, Color.cyan);
                    if (mode.HasFlag(DebugMode.Door))
                        doorSpawnGroup.DisplayInfo(connections, Color.yellow);
                    if (mode.HasFlag(DebugMode.Guard))
                        guardSpawnGroup.DisplayInfo(connections, Color.magenta);
                }
                catch (Exception e)
                {
                    //
                }
                
                Facepunch.Pool.FreeUnmanaged<Connection>(ref connections);
            }

            #endregion
        }
        
        [Flags]
        private enum DebugMode
        {
            Guard = 0,
            Loot = 1,
            Door = 2,
            Info = 3,
            Everything = Info | Guard | Loot | Door,
        }

        #endregion
        
        #region Spawn Group
        
        private class GuardSpawnGroup : BankSpawnGroup
        {
            public override IEnumerator Spawn()
            {
                yield return CoroutineEx.waitForSeconds(0.25f);
                
                if (spawnPointEntries == null || spawnPointEntries.Count == 0)
                    yield break;

                if (_instance == null || !_instance.NpcSpawn.IsReady())
                {
                    Interface.Oxide.LogDebug("Missing dependency [NpcSpawn] this can be found over at codefling.com thanks to KpucTaJl");
                    yield break;
                }
                
                foreach (SpawnEntry spawnEntry in spawnPointEntries)
                {
                    if (!GuardProfileStorage.Data.GetProfileByName(spawnEntry.Profile, out GuardProfileEntry profileEntry) || profileEntry == null)
                        profileEntry = GuardProfileStorage.DefaultProfile();
                    
                    ScientistNPC scientist =  spawnEntry.CreateGuard<ScientistNPC>(transform, profileEntry);
                    if (scientist == null || scientist.IsDestroyed) 
                        continue;
                    
                    EntitiesCache.CacheEntity(scientist.net.ID, bankHeistInstance);
                    
                    BankSpawnPointInstance spawnPointInstance = scientist.gameObject.AddComponent<BankSpawnPointInstance>();
                    spawnPointInstance.spawnPointUser = this;
                    spawnPointInstance.Notify();
                    
                    yield return CoroutineEx.waitForEndOfFrame;
                }
            }
        }

        private class DoorSpawnGroup : BankSpawnGroup
        {
            public override IEnumerator Spawn()
            {
                yield return CoroutineEx.waitForSeconds(0.25f);
                
                if (spawnPointEntries == null || spawnPointEntries.Count == 0)
                    yield break;
                
                foreach (SpawnEntry spawnEntry in spawnPointEntries)
                {
                    if (!DoorProfileStorage.Data.GetProfileByName(spawnEntry.Profile, out DoorProfileEntry profileEntry) || profileEntry == null)
                        profileEntry = DoorProfileStorage.DefaultProfile();
                    
                    Door door = spawnEntry.CreateEntity<Door>(transform);
                    door.gameObject.AwakeFromInstantiate();
                    door.EnableSaving(false);
                    
                    CustomUtils.DestroyComponents(door);
                    CustomUtils.DestroyMeshCollider(door);

                    if (profileEntry.Health != 0.0f)
                    {
                        door.startHealth = profileEntry.Health;
                        door._maxHealth = profileEntry.Health;
                    }
                    
                    door.isSecurityDoor = false;
                    door.pickup.enabled = false;
                    door.repair.enabled = false;
                    door.canTakeKnocker = false;
                    door.canTakeCloser = false;
                    door.canNpcOpen = false;
                    door.grounded = true;
                    door.decay = null;
                    door.Spawn();
                    door.InitializeHealth(door.MaxHealth(), door.MaxHealth());
                    
                    EntitiesCache.CacheEntity(door.net.ID, bankHeistInstance);
                    
                    BankSpawnPointInstance spawnPointInstance = door.gameObject.AddComponent<BankSpawnPointInstance>();
                    spawnPointInstance.spawnPointUser = this;
                    spawnPointInstance.Notify();
                    
                    DoorSpawnGroup.SetupDoorSettings(door, profileEntry);
                    DoorSpawnGroup.SetupDoorObstacle(door);
                    
                    yield return CoroutineEx.waitForEndOfFrame;
                }
            }
            
            public override void Clear()
            {
                for (int i = spawnInstances.Count - 1; i >= 0; --i)
                {
                    BaseEntity entity = spawnInstances[i]?.gameObject?.ToBaseEntity();
                    if (entity != null && !entity.IsDestroyed)
                    {
                        entity.GetSlot(BaseEntity.Slot.Lock)?.Kill();
                        entity.Kill();
                    }
                }
                
                spawnInstances.Clear();
            }

            private static void SetupDoorSettings(Door door, DoorProfileEntry profileEntry)
            {
                CodeLock codeLock;
                
                if (_instance.PersonalVaultDoor == null || (CodeLock)door.GetSlot(BaseEntity.Slot.Lock) == null)
                {
                    codeLock = (CodeLock)GameManager.server.CreateEntity("assets/prefabs/locks/keypad/lock.code.prefab");
                    codeLock.EnableSaving(false);
                    codeLock.gameObject.Identity();
                    codeLock.SetParent(door, door.GetSlotAnchorName(BaseEntity.Slot.Lock));
                    codeLock.Spawn();
                    
                    door.SetSlot(BaseEntity.Slot.Lock, codeLock);
                }
                else
                {
                    codeLock = (CodeLock)door.GetSlot(BaseEntity.Slot.Lock);
                }

                if (codeLock != null)
                {
                    codeLock.code = UnityEngine.Random.Range(1000, 9999).ToString();
                    codeLock.hasCode = true;
                    codeLock.SetFlag(BaseEntity.Flags.Locked, true);
                }
                
                door.skinID = profileEntry.SkinId;
                door.SendNetworkUpdate();
            }

            private static void SetupDoorObstacle(Door door)
            {
                NavMeshObstacle obstacle = door.GetComponent<NavMeshObstacle>() ?? door.gameObject.AddComponent<NavMeshObstacle>();
                obstacle.carving = true;
                obstacle.center = Vector3.zero;
                obstacle.size = Vector3.one * 2.5f;
                obstacle.shape = NavMeshObstacleShape.Box;
                
                door.NavMeshVolumeHumanoids = door.GetComponent<NavMeshModifierVolume>();
                door.NavMeshVolumeHumanoids.size = Vector3.one + Vector3.up + Vector3.forward;
            }
        }
        
        private class LootSpawnGroup : BankSpawnGroup
        {
            public override IEnumerator Spawn()
            {
                yield return CoroutineEx.waitForSeconds(0.25f);

                if (spawnPointEntries == null || spawnPointEntries.Count == 0) 
                    yield break;
                
                foreach (SpawnEntry spawnPoint in spawnPointEntries)
                {
                    if (!LootProfileStorage.Data.GetProfileByName(spawnPoint.Profile, out LootProfileEntry profileEntry) || profileEntry == null)
                        profileEntry = LootProfileStorage.DefaultProfile();
                    
                    LootContainer container = spawnPoint.CreateEntity<LootContainer>(transform);
                    container.gameObject.AwakeFromInstantiate();
                    container.EnableSaving(false);
                    container.minSecondsBetweenRefresh = 0.0f;
                    container.maxSecondsBetweenRefresh = 0.0f;
                    container.decay = null;
                    container.Spawn();
                    
                    EntitiesCache.CacheEntity(container.net.ID, bankHeistInstance);
                    
                    BankSpawnPointInstance spawnPointInstance = container.gameObject.AddComponent<BankSpawnPointInstance>();
                    spawnPointInstance.spawnPointUser = this;
                    spawnPointInstance.Notify();
                    
                    LootSpawnGroup.PostSpawn(container, profileEntry);
                    
                    yield return CoroutineEx.waitForEndOfFrame;
                }
            }

            private static void PostSpawn(LootContainer container, LootProfileEntry profileEntry)
            {
                if (container is HackableLockedCrate lockedCrate)
                {
                    lockedCrate.shouldDecay = false;
                    lockedCrate.inventory.onItemAddedRemoved = null;
                    lockedCrate.CancelInvoke(new Action(lockedCrate.RefreshDecay));
                    lockedCrate.CancelInvoke(new Action(lockedCrate.DelayedDestroy));

                    if (profileEntry.HackableSeconds == 0.0f)
                        lockedCrate.hackSeconds = HackableLockedCrate.requiredHackSeconds;
                    else 
                        lockedCrate.hackSeconds = HackableLockedCrate.requiredHackSeconds - profileEntry.HackableSeconds;
                }
                
                container.skinID = profileEntry.SkinId;
                container.Invoke(() => profileEntry.PopulateContainer(container, UnityEngine.Random.Range(profileEntry.MinItems, profileEntry.MaxItems)), 2.5f);
            }
        }

        private abstract class BankSpawnGroup : FacepunchBehaviour, IBankSpawnGroup, IBankSpawnPointUser
        {
            public List<BankSpawnPointInstance> spawnInstances = new();
            public List<SpawnEntry> spawnPointEntries = new();
            public BankHeistInstance bankHeistInstance;
            public int SpawnPointInstanceTotal => spawnInstances.Count;
            public int SpawnPointEntriesTotal => spawnPointEntries.Count;

            public virtual IEnumerator Spawn()
            {
                yield return null;
            }

            public virtual void Clear()
            {
                for (int i = spawnInstances.Count - 1; i >= 0; --i)
                {
                    BaseEntity entity = spawnInstances[i]?.gameObject?.ToBaseEntity();
                    if (entity != null && !entity.IsDestroyed)
                        entity.Kill();
                }
                
                spawnInstances.Clear();
            }

            public virtual void DisplayInfo(List<Connection> connections, Color color)
            {
                for (var i = 0; i < spawnPointEntries.Count; i++)
                {
                    SpawnEntry spawnEntry = spawnPointEntries[i];
                    if (spawnEntry == null)
                        continue;
                    
                    Vector3 position = transform.TransformPoint(spawnEntry.Position);
                    Vector3 rotation = transform.TransformDirection(spawnEntry.Rotation);
                    CustomUtils.DrawText(connections, position, $"ID: {spawnEntry.Id}\n{System.IO.Path.GetFileNameWithoutExtension(spawnEntry.Prefab ?? "guard")} | {position} | {rotation}", color, 30f);
                    CustomUtils.DrawSphere(connections, position, 1f, color, 30f);
                }
            }

            public virtual void ObjectSpawned(BankSpawnPointInstance instance) => spawnInstances.Add(instance);

            public virtual void ObjectRetired(BankSpawnPointInstance instance) => spawnInstances.Remove(instance);
        }

        private class BankSpawnPointInstance : MonoBehaviour
        {
            public IBankSpawnPointUser spawnPointUser;

            public void Notify()
            {
                if (!spawnPointUser.IsUnityNull<IBankSpawnPointUser>())
                    spawnPointUser.ObjectSpawned(this);
            }

            public void Retire()
            {
                if (!spawnPointUser.IsUnityNull<IBankSpawnPointUser>())
                    spawnPointUser.ObjectRetired(this);
            }

            protected void OnDestroy()
            {
                if (Rust.Application.isQuitting) 
                    return;
                
                Retire();
            }
        }

        private interface IBankSpawnPointUser
        {
            void ObjectSpawned(BankSpawnPointInstance instance);
            void ObjectRetired(BankSpawnPointInstance instance);
        }
        
        private interface IBankSpawnGroup
        {
            IEnumerator Spawn();
            void Clear();
            void DisplayInfo(List<Connection> connections, Color color);
        }

        #endregion
        
        #region Monument Lookup
        
        private static class MonumentCache
        {
            public static readonly List<MonumentEntry> MonumentEntries = new();
            
            public static MonumentEntry FindMonumentByName(string displayName)
            {
                return MonumentEntries.Find(x => x.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));
            }
            
            public static void Initialize()
            {
                foreach (MonumentInfo monumentInfo in TerrainMeta.Path.Monuments)
                {
                    if (monumentInfo == null || monumentInfo.IsSafeZone) 
                        continue;
                    
                    string displayName = System.IO.Path.GetFileNameWithoutExtension(monumentInfo.name);
                    if (string.IsNullOrEmpty(displayName)) 
                        continue;
                    
                    MonumentEntries.Add(new MonumentEntry(displayName, monumentInfo.transform, null));
                }
                
                List<string> bankNames = Facepunch.Pool.Get<List<string>>();

                try
                {
                    foreach (PrefabData prefabData in World.Serialization.world.prefabs)
                    {
                        if (prefabData.category != "IGNORE_MONUMENT" && prefabData.category != "Decor")
                        {
                            string[] prefabName = prefabData.category.Split(':');
                            if (prefabName.Length < 2)
                                continue;
                    
                            string displayName = prefabName[1]?.Replace("\\", string.Empty);
                            if (string.IsNullOrEmpty(displayName) || !(displayName.StartsWith("BankHeist", StringComparison.OrdinalIgnoreCase) || displayName.StartsWith("BankFloor", StringComparison.OrdinalIgnoreCase)))
                                continue;
                        
                            if (bankNames.Contains(displayName))
                                continue;
                            
                            bankNames.Add(displayName);
                    
                            GameObject gameObject = new GameObject();
                            gameObject.transform.SetPositionAndRotation(prefabData.position, Quaternion.Euler(prefabData.rotation));
                            gameObject.AddComponent<BankHeistCustomMonument>();
                            
                            MonumentEntries.Add(new MonumentEntry(displayName, gameObject.transform, gameObject));
                        }
                    }
                    
                    Interface.Oxide.LogDebug("Banks monuments found: {0}", bankNames.Count);
                }
                catch (Exception e)
                {
                    //
                }
                
                Facepunch.Pool.FreeUnmanaged<string>(ref bankNames);
            }
            
            public static void OnUnload()
            {
                foreach (MonumentEntry monumentEntry in new List<MonumentEntry>(MonumentEntries))
                    monumentEntry.Dispose();
                
                MonumentEntries.Clear();
            }

            public static void Display()
            {
                List<Connection> connections = Facepunch.Pool.Get<List<Connection>>();

                try
                {
                    connections.AddRange(Net.sv.connections.Where(x => x.connected && x.authLevel != 0));
                    
                    foreach (MonumentEntry monumentEntry in MonumentEntries)
                        CustomUtils.DrawText(connections, monumentEntry.GetTransform().position, monumentEntry.DisplayName, Color.magenta, 30f);
                }
                catch (Exception e)
                {
                    //
                }
                
                Facepunch.Pool.FreeUnmanaged<Connection>(ref connections);
            }
        }

        private class MonumentEntry : IDisposable
        {
            public string DisplayName;
            public Transform Transform;
            public GameObject CustomObject;
            
            public MonumentEntry(string displayName, Transform transform, GameObject customObject = null)
            {
                DisplayName = displayName;
                Transform =  transform;
                CustomObject = customObject;
            }

            public Transform GetTransform() => Transform;
            
            public void Dispose()
            {
                if (CustomObject != null)
                    UnityEngine.Object.Destroy(CustomObject);

                DisplayName = null;
                Transform = null;
                CustomObject = null;
            }
        }

        private class BankHeistCustomMonument : MonoBehaviour { }

        #endregion

        #region Entities Lookup

        private static class EntitiesCache
        {
            public static Dictionary<NetworkableId, BankHeistInstance> Entities = new();

            public static void Initialize() { }
            
            public static void OnUnload() => Entities.Clear();
            
            public static BankHeistInstance GetBankByEntity(NetworkableId entity)
            {
                if (!entity.IsValid) 
                    return null;
                
                return Entities.TryGetValue(entity, out BankHeistInstance heistInstance) ? heistInstance : null;
            }
            
            public static void CacheEntity(NetworkableId networkableId, BankHeistInstance heistInstance)
            {
                if (!networkableId.IsValid) 
                    return;
                
                Entities.Add(networkableId, heistInstance);
            }

            public static void RemoveEntity(NetworkableId networkableId) => Entities.Remove(networkableId);
        }

        #endregion
        
        #region Command Methods

        private void CreateBankEvent(BasePlayer player, string[] args)
        {
            if (args.Length < 1)
            {
                MessagePlayer(player, LangEntry.InvalidCmd, "bhm", "create", "<bank-name>");
                return;
            }

            string bankName = string.Join(" ", args);
            if (BankStorage.Data.GetBankByName(bankName, out BankEntry bankEntry))
            {
                MessagePlayer(player, LangEntry.BankAlreadyExists);
                return;
            }
            
            bankEntry = new BankEntry(bankName, player.transform);
            
            CommunityEntity.ServerInstance.StartCoroutine(bankEntry.SpawnBankInstance());
            
            BankStorage.Data.BankEntries.Add(bankEntry);
            BankStorage.SaveData();

            MessagePlayer(player, LangEntry.BankCreated);
        }

        private void RemoveBankEvent(BasePlayer player, string[] args)
        {
            if (args.Length < 1)
            {
                MessagePlayer(player, LangEntry.InvalidCmd, "bhm", "remove", "<bank-name>");
                return;
            }

            string bankName = string.Join(" ", args);
            if (!BankStorage.Data.GetBankByName(bankName, out BankEntry bankEntry))
            {
                MessagePlayer(player, LangEntry.BankNotFound);
                return;
            }
            
            if (BankHeistInstance.FindBankByName(bankName, out BankHeistInstance heistInstance))
                heistInstance.Dispose();
            
            BankStorage.Data.BankEntries.Remove(bankEntry);
            BankStorage.SaveData();

            MessagePlayer(player, LangEntry.BankRemoved);
        }

        private void CreateBankEntity(BasePlayer player, string option, string[] args)
        {
            if (args.Length < 1)
            {
                MessagePlayer(player, LangEntry.InvalidArgs, "<guard|door|loot>", "create <bank-name>");
                return;
            }

            string bankName = args[0];
            if (!BankHeistInstance.FindBankByName(bankName, out BankHeistInstance heistInstance))
            {
                MessagePlayer(player, LangEntry.BankNotSpawned);
                return;
            }
            
            if (!BankStorage.Data.GetBankByName(bankName, out BankEntry bankEntry))
            {
                MessagePlayer(player, LangEntry.BankNotFound);
                return;
            }
            
            Vector3 position;
            Vector3 rotation;

            switch(option)
            {
                case "guard":
                    position = heistInstance.transform.InverseTransformPoint(player.transform.position);
                    rotation = player.transform.rotation.eulerAngles - heistInstance.transform.rotation.eulerAngles;
                    
                    bankEntry.NpcSpawnGroupEntries.Add(new SpawnEntry(UnityEngine.Random.Range(0000, 9999), null, "default", position, rotation, 0UL));
                    BankStorage.SaveData();
                    break;
                case "loot":
                    if (!CustomUtils.GetLookEntity<LootContainer>(player, 10f, RAYCAST_LAYERS, out BaseEntity entity))
                    {
                        MessagePlayer(player, LangEntry.EntityNotFound);
                        return;
                    }
                    
                    position = heistInstance.transform.InverseTransformPoint(entity.transform.position);
                    rotation = entity.transform.rotation.eulerAngles - heistInstance.transform.rotation.eulerAngles;
                    bankEntry.LootSpawnGroupEntries.Add(new SpawnEntry(UnityEngine.Random.Range(0000, 9999), entity.PrefabName, null, position, rotation, entity.skinID));
                    BankStorage.SaveData();
                    break;
                case "door":
                    if (!CustomUtils.GetLookEntity<Door>(player, 10f, RAYCAST_LAYERS, out entity))
                    {
                        MessagePlayer(player, LangEntry.EntityNotFound);
                        return;
                    }
                    
                    position = heistInstance.transform.InverseTransformPoint(entity.transform.position);
                    rotation = entity.transform.rotation.eulerAngles - heistInstance.transform.rotation.eulerAngles;
                    
                    bankEntry.DoorSpawnGroupEntries.Add(new SpawnEntry(UnityEngine.Random.Range(0000, 9999), entity.PrefabName, null, position, rotation, entity.skinID));
                    BankStorage.SaveData();
                    break;
                default:
                    MessagePlayer(player, LangEntry.InvalidArgs, "<guard|door|loot>", "create <bank-name>");
                    return;
            }

            MessagePlayer(player, LangEntry.BankUpdated);
        }

        private void RemoveBankEntity(BasePlayer player, string option, string[] args)
        {
            if (args.Length < 2)
            {
                MessagePlayer(player, LangEntry.InvalidArgument, "<guard|door|loot>", "remove <bank-name> <number>");
                return;
            }
            
            if (!BankStorage.Data.GetBankByName(args[0], out BankEntry bankEntry))
            {
                MessagePlayer(player, LangEntry.BankNotFound);
                return;
            }
            
            int sid = -1;
            
            SpawnEntry spawnGroupEntry = (SpawnEntry)null;

            switch(option)
            {
                case "guard":
                    if (args[1].Equals("clear", StringComparison.OrdinalIgnoreCase))
                    {
                        bankEntry.NpcSpawnGroupEntries.Clear();
                        BankStorage.SaveData();
                        return;
                    }
                    
                    if (!int.TryParse(args[1], out sid))
                    {
                        MessagePlayer(player, LangEntry.InvalidArgument, "<number>", "number");
                        return;
                    }
                    
                    spawnGroupEntry = bankEntry.NpcSpawnGroupEntries.Find(x => x.Id == sid);
                    
                    if (spawnGroupEntry == null)
                    {
                        MessagePlayer(player, LangEntry.SpawnEntryNotFound);
                        return;
                    }

                    bankEntry.NpcSpawnGroupEntries.Remove(spawnGroupEntry);
                    BankStorage.SaveData();
                    break;
                case "loot":
                    if (args[1].Equals("clear", StringComparison.OrdinalIgnoreCase))
                    {
                        bankEntry.LootSpawnGroupEntries.Clear();
                        BankStorage.SaveData();
                        return;
                    }
                    
                    if (!int.TryParse(args[1], out sid))
                    {
                        MessagePlayer(player, LangEntry.InvalidArgument, "<number>", "number");
                        return;
                    }
                    
                    spawnGroupEntry = bankEntry.LootSpawnGroupEntries.Find(x => x.Id == sid);
                    
                    if (spawnGroupEntry == null)
                    {
                        MessagePlayer(player, LangEntry.SpawnEntryNotFound);
                        return;
                    }

                    bankEntry.LootSpawnGroupEntries.Remove(spawnGroupEntry);
                    BankStorage.SaveData();
                    break;
                case "door":
                    if (args[1].Equals("clear", StringComparison.OrdinalIgnoreCase))
                    {
                        bankEntry.DoorSpawnGroupEntries.Clear();
                        BankStorage.SaveData();
                        return;
                    }
                    
                    if (!int.TryParse(args[1], out sid))
                    {
                        MessagePlayer(player, LangEntry.InvalidArgument, "<number>", "number");
                        return;
                    }
                    
                    spawnGroupEntry = bankEntry.DoorSpawnGroupEntries.Find(x => x.Id == sid);
                    
                    if (spawnGroupEntry == null)
                    {
                        MessagePlayer(player, LangEntry.SpawnEntryNotFound);
                        return;
                    }

                    bankEntry.DoorSpawnGroupEntries.Remove(spawnGroupEntry);
                    BankStorage.SaveData();
                    break;
                default:
                    MessagePlayer(player, LangEntry.InvalidArgument, "<guard|door|loot>", "remove <bank-name> <number>");
                    return;
            }
            
            MessagePlayer(player, LangEntry.BankUpdated);
        }

        private void ProfileBankEntity(BasePlayer player, string option, string[] args)
        {
            if (args.Length < 3)
            {
                MessagePlayer(player, LangEntry.InvalidArgument, "<guard|loot|door>", "profile <bank-name> <number> <profile-name>");
                return;
            }
            
            if (!BankStorage.Data.GetBankByName(args[0], out BankEntry bankEntry))
            {
                MessagePlayer(player, LangEntry.BankNotFound);
                return;
            }
            
            if (!int.TryParse(args[1], out int sid))
            {
                MessagePlayer(player, LangEntry.InvalidArgument, "<number>", "number");
                return;
            }

            SpawnEntry spawnPointEntry = (SpawnEntry)null;

            switch(option)
            {
                case "guard":
                    spawnPointEntry = bankEntry.NpcSpawnGroupEntries.Find(x => x.Id == sid);
                    
                    if (spawnPointEntry == null)
                    {
                        MessagePlayer(player, LangEntry.SpawnEntryNotFound);
                        return;
                    }
                    
                    if (string.IsNullOrEmpty(args[2]) || !GuardProfileStorage.Data.GetProfileByName(args[2], out GuardProfileEntry guardProfileEntry))
                    {
                        MessagePlayer(player, LangEntry.ProfileNotFound);
                        return;
                    }
                    
                    spawnPointEntry.Profile = args[2];
                    BankStorage.SaveData();
                    break;
                case "loot":
                    spawnPointEntry = bankEntry.LootSpawnGroupEntries.Find(x => x.Id == sid);
                    
                    if (spawnPointEntry == null)
                    {
                        MessagePlayer(player, LangEntry.SpawnEntryNotFound);
                        return;
                    }
                    
                    if (string.IsNullOrEmpty(args[2]) || !LootProfileStorage.Data.GetProfileByName(args[2], out LootProfileEntry lootProfileEntry))
                    {
                        MessagePlayer(player, LangEntry.ProfileNotFound);
                        return;
                    }

                    spawnPointEntry.Profile = args[2];
                    BankStorage.SaveData();
                    break;
                case "door":
                    spawnPointEntry = bankEntry.DoorSpawnGroupEntries.Find(x => x.Id == sid);
                    
                    if (spawnPointEntry == null)
                    {
                        MessagePlayer(player, LangEntry.SpawnEntryNotFound);
                        return;
                    }

                    if (string.IsNullOrEmpty(args[2]) || !DoorProfileStorage.Data.GetProfileByName(args[2], out DoorProfileEntry doorProfileEntry))
                    {
                        MessagePlayer(player, LangEntry.ProfileNotFound);
                        return;
                    }

                    spawnPointEntry.Profile = args[2];
                    BankStorage.SaveData();
                    break;
                default:
                    MessagePlayer(player, LangEntry.InvalidArgument, "<guard|loot>", "profile <bank-name> <number> <profile-name>");
                    return;
            }
            
            MessagePlayer(player, LangEntry.BankUpdated);
        }

        private void UpdateMonumentParent(BasePlayer player, string[] args)
        {
            if (args.Length < 2)
            {
                MessagePlayer(player, LangEntry.InvalidCmd, "bhm", "parent", "<bank-name> <monument-name>");
                return;
            }
            
            if (!BankStorage.Data.GetBankByName(args[0], out BankEntry bankEntry))
            {
                MessagePlayer(player, LangEntry.BankNotFound);
                return;
            }

            MonumentEntry monumentEntry = MonumentCache.FindMonumentByName(args[1]);
            if (monumentEntry == null)
            {
                MessagePlayer(player, LangEntry.MonumentNotFound);
                return;
            }
            
            bankEntry.MonumentParentName = monumentEntry.DisplayName;
            BankStorage.SaveData();

            MessagePlayer(player, LangEntry.BankUpdated);
        }

        private void UpdatePlayerDetectionRadius(BasePlayer player, string[] args)
        {
            if (args.Length < 2)
            {
                MessagePlayer(player, LangEntry.InvalidCmd, "bhm", "pdr", "<bank-name> <radius>");
                return;
            }
            
            if (!BankStorage.Data.GetBankByName(args[0], out BankEntry bankEntry))
            {
                MessagePlayer(player, LangEntry.BankNotFound);
                return;
            }
            
            if (!float.TryParse(args[1], out float radius) || radius <= 0.0f)
            {
                MessagePlayer(player, LangEntry.InvalidArgument, "<radius>", "number");
                return;
            }

            bankEntry.PlayerDetectionRadius = radius;
            BankStorage.SaveData();
            
            MessagePlayer(player, LangEntry.BankUpdated);
        }

        private void UpdateMapMarkerRadius(BasePlayer player, string[] args)
        {
            if (args.Length < 2)
            {
                MessagePlayer(player, LangEntry.InvalidCmd, "bhm", "mmr", "<bank-name> <radius>");
                return;
            }
            
            if (!BankStorage.Data.GetBankByName(args[0], out BankEntry bankEntry))
            {
                MessagePlayer(player, LangEntry.BankNotFound);
                return;
            }
            
            if (!float.TryParse(args[1], out float radius) || radius <= 0.0f)
            {
                MessagePlayer(player, LangEntry.InvalidArgument, "<radius>", "number");
                return;
            }

            bankEntry.MapMarkerRadius = radius;
            BankStorage.SaveData();
            
            MessagePlayer(player, LangEntry.BankUpdated);
        }
        
        private void UpdateTimeBetweenResets(BasePlayer player, string[] args)
        {
            if (args.Length < 2)
            {
                MessagePlayer(player, LangEntry.InvalidCmd, "bhm", "tbr", "<bank-name> <seconds>");
                return;
            }
            
            if (!BankStorage.Data.GetBankByName(args[0], out BankEntry bankEntry))
            {
                MessagePlayer(player, LangEntry.BankNotFound);
                return;
            }
            
            if (!float.TryParse(args[1], out float seconds) || seconds <= 0.0f)
            {
                MessagePlayer(player, LangEntry.InvalidArgument, "<seconds>", "number");
                return;
            }
            
            bankEntry.TimeBetweenResets = Mathf.Clamp(seconds, 60f, float.PositiveInfinity);
            BankStorage.SaveData();
            
            MessagePlayer(player, LangEntry.BankUpdated);
        }

        private void UpdateAlarmTimeout(BasePlayer player, string[] args)
        {
            if (args.Length < 2)
            {
                MessagePlayer(player, LangEntry.InvalidCmd, "bhm", "ato", "<bank-name> <seconds>");
                return;
            }
            
            if (!BankStorage.Data.GetBankByName(args[0], out BankEntry bankEntry))
            {
                MessagePlayer(player, LangEntry.BankNotFound);
                return;
            }
            
            if (!float.TryParse(args[1], out float seconds) || seconds < 0.0f)
            {
                MessagePlayer(player, LangEntry.InvalidArgument, "<seconds>", "number");
                return;
            }
            
            bankEntry.AlarmTimeoutSeconds = Mathf.Clamp(seconds, 0f, float.PositiveInfinity);
            BankStorage.SaveData();
            
            MessagePlayer(player, LangEntry.BankUpdated);
        }
        
        private static void DisplayHelpText(BasePlayer player)
        {
            StringBuilder sb = new StringBuilder();
            
            try
            {
                sb.Clear();
                sb.AppendLine("Invalid arguments:")
                    .AppendLine("<color=#2ec8d0>/bse</color> <type>, spawn prefab entity of type for setup")
                    .AppendLine("<color=#2ec8d0>/blp</color> create|remove|update <profile-name>, manage bank loot profiles")
                    .AppendLine("<color=#2ec8d0>/bgp</color> create|remove|update|stationary <profile-name>, manage bank guard profiles")
                    .AppendLine("<color=#2ec8d0>/bdp</color> create|remove|update <profile-name>, manage bank door profiles")
                    .AppendLine("<color=#2ec8d0>/bhm</color> create|remove <bank-name>, create or remove a bank")
                    .AppendLine("<color=#2ec8d0>/bhm</color> parent <bank-name> <monument-name>, setup a parent monument for the bank")
                    .AppendLine("<color=#2ec8d0>/bhm</color> <guard|loot|door> profile <bank-name> <loot|door|guard-id> <profile-name>, setup profile for a door or loot to use");
                
                player.ChatMessage(sb.ToString());
                
                sb.Clear();
                sb.AppendLine("<color=#2ec8d0>/bhm</color> <guard|loot|door> remove <bank-name> <loot/guard-id>, remove entity from a specified bank")
                    .AppendLine("<color=#2ec8d0>/bhm</color> <guard|loot|door> create <bank-name>, create entity for the specified bank")
                    .AppendLine("<color=#2ec8d0>/bhm</color> mmr <bank-name> <radius>, map marker radius")
                    .AppendLine("<color=#2ec8d0>/bhm</color> pdr <bank-name> <radius>, player detection radius")
                    .AppendLine("<color=#2ec8d0>/bhm</color> tbr <bank-name> <seconds>, seconds before bank will reset")
                    .AppendLine("<color=#2ec8d0>/bhm</color> ato <bank-name> <seconds>, seconds before alarm will disable 0 = until bank reset");
                
                player.ChatMessage(sb.ToString());
                
                sb.Clear();
                sb.AppendLine("<color=#2ec8d0>/bhm</color> debug <bank-name>, enable debug information")
                    .AppendLine("<color=#2ec8d0>/bhm</color> monuments");
            
                player.ChatMessage(sb.ToString());
            }
            catch(Exception e)
            {
                //
            }
            
            sb.Clear();
            sb = null;
        }

        #endregion
        
        #region Chat Commands

        private void GuardProfileCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(MANAGE_PERM))
            {
                MessagePlayer(player, LangEntry.NoPermission);
                return;
            }
            
            if (args.Length < 2)
            {
                MessagePlayer(player, LangEntry.InvalidCmd, command, "<create|remove|update|stationary>", "<profile-name>");
                return;
            }
            
            GuardProfileEntry profileEntry = null;
            string optionName = args[0];
            string profileName = args[1];

            if (optionName.Equals("create", StringComparison.OrdinalIgnoreCase))
            {
                if (GuardProfileStorage.Data.GetProfileByName(profileName, out profileEntry))
                {
                    MessagePlayer(player, LangEntry.ProfileAlreadyExists);
                    return;
                }
                    
                GuardProfileStorage.Data.ProfileEntries.Add(profileName, GuardProfileStorage.DefaultProfile());
                GuardProfileStorage.SaveData();
                MessagePlayer(player, LangEntry.ProfileCreated);
                return;
            }
            
            if (optionName.Equals("remove", StringComparison.OrdinalIgnoreCase))
            {
                if (!GuardProfileStorage.Data.GetProfileByName(profileName, out profileEntry))
                {
                    MessagePlayer(player, LangEntry.ProfileNotFound);
                    return;
                }

                GuardProfileStorage.Data.ProfileEntries.Remove(profileName);
                GuardProfileStorage.SaveData();
                MessagePlayer(player, LangEntry.ProfileRemoved);
                return;
            }
            
            if (optionName.Equals("update", StringComparison.OrdinalIgnoreCase))
            {
                if (!GuardProfileStorage.Data.GetProfileByName(profileName, out profileEntry))
                {
                    MessagePlayer(player, LangEntry.ProfileNotFound);
                    return;
                }

                if (player.inventory.containerBelt != null)
                {
                    profileEntry.BeltItems.Clear();
                    foreach (Item item in player.inventory.containerBelt.itemList)
                        profileEntry.BeltItems.Add(GuardProfileEntry.BeltEntry.SerializeItem(item));
                }

                if (player.inventory.containerWear != null)
                {
                    profileEntry.WearItems.Clear();
                    foreach (Item item in player.inventory.containerWear.itemList)
                        profileEntry.WearItems.Add(GuardProfileEntry.WearEntry.SerializeItem(item));
                }
                
                profileEntry.CacheConfig();
                GuardProfileStorage.SaveData();
                MessagePlayer(player, LangEntry.ProfileUpdated);
                return;
            }

            if (optionName.Equals("stationary", StringComparison.OrdinalIgnoreCase))
            {
                if (!GuardProfileStorage.Data.GetProfileByName(profileName, out profileEntry))
                {
                    MessagePlayer(player, LangEntry.ProfileNotFound);
                    return;
                }

                if (args.Length < 3)
                {
                    MessagePlayer(player, LangEntry.InvalidCmd, command, "<create|remove|update|stationary <true|false>>", "<profile-name>");
                    return;
                }

                if (!bool.TryParse(args[2], out bool stationary))
                {
                    MessagePlayer(player, LangEntry.InvalidCmd, command, "<create|remove|update|stationary <true|false>>", "<profile-name>");
                    return;
                }
                
                profileEntry.Stationary = stationary;
                profileEntry.CacheConfig();
                GuardProfileStorage.SaveData();
                MessagePlayer(player, LangEntry.ProfileUpdated);
                return;
            }
            
            DisplayHelpText(player);
        }
        
        private void LootProfileCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(MANAGE_PERM))
            {
                MessagePlayer(player, LangEntry.NoPermission);
                return;
            }
            
            if (args.Length < 2)
            {
                MessagePlayer(player, LangEntry.InvalidCmd, command, "<create|remove|update>", "<profile-name>");
                return;
            }
            
            LootProfileEntry profileEntry = null;
            string optionName = args[0];
            string profileName = args[1];

            if (optionName.Equals("create", StringComparison.OrdinalIgnoreCase))
            {
                if (LootProfileStorage.Data.GetProfileByName(profileName, out profileEntry))
                {
                    MessagePlayer(player, LangEntry.ProfileAlreadyExists);
                    return;
                }
                    
                LootProfileStorage.Data.ProfileEntries.Add(profileName, LootProfileStorage.DefaultProfile());
                LootProfileStorage.SaveData();
                MessagePlayer(player, LangEntry.ProfileCreated);
                return;
            }
            
            if (optionName.Equals("remove", StringComparison.OrdinalIgnoreCase))
            {
                if (!LootProfileStorage.Data.GetProfileByName(profileName, out profileEntry))
                {
                    MessagePlayer(player, LangEntry.ProfileNotFound);
                    return;
                }

                LootProfileStorage.Data.ProfileEntries.Remove(profileName);
                LootProfileStorage.SaveData();
                MessagePlayer(player, LangEntry.ProfileRemoved);
                return;
            }
            
            if (optionName.Equals("update", StringComparison.OrdinalIgnoreCase))
            {
                if (!LootProfileStorage.Data.GetProfileByName(profileName, out profileEntry))
                {
                    MessagePlayer(player, LangEntry.ProfileNotFound);
                    return;
                }
                    
                profileEntry.LootItems.Clear();
                if (player.inventory.containerWear != null)
                {
                    foreach (Item item in player.inventory.containerWear.itemList)
                        profileEntry.LootItems.Add(new LootProfileEntry.LootItem(item));
                }
                    
                if (player.inventory.containerBelt != null)
                {
                    foreach (Item item in player.inventory.containerBelt.itemList)
                        profileEntry.LootItems.Add(new LootProfileEntry.LootItem(item));
                }

                if (player.inventory.containerMain != null)
                {
                    foreach (Item item in player.inventory.containerMain.itemList)
                        profileEntry.LootItems.Add(new LootProfileEntry.LootItem(item));
                }

                LootProfileStorage.SaveData();
                MessagePlayer(player, LangEntry.ProfileUpdated);
                return;
            }
            
            DisplayHelpText(player);
        }

        private void DoorProfileCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(MANAGE_PERM))
            {
                MessagePlayer(player, LangEntry.NoPermission);
                return;
            }
            
            if (args.Length < 2)
            {
                MessagePlayer(player, LangEntry.InvalidCmd, command, "<create|remove|update>", "<profile-name>");
                return;
            }
            
            DoorProfileEntry profileEntry = null;
            string optionName = args[0];
            string profileName = args[1];

            if (optionName.Equals("create", StringComparison.OrdinalIgnoreCase))
            {
                if (DoorProfileStorage.Data.GetProfileByName(profileName, out profileEntry))
                {
                    MessagePlayer(player, LangEntry.ProfileAlreadyExists);
                    return;
                }
                
                DoorProfileStorage.Data.ProfileEntries[profileName] = DoorProfileStorage.DefaultProfile();
                DoorProfileStorage.SaveData();
                MessagePlayer(player, LangEntry.ProfileCreated);
                return;
            }
            
            if (optionName.Equals("remove", StringComparison.OrdinalIgnoreCase))
            {
                if (!DoorProfileStorage.Data.GetProfileByName(profileName, out profileEntry))
                {
                    MessagePlayer(player, LangEntry.ProfileNotFound);
                    return;
                }

                DoorProfileStorage.Data.ProfileEntries.Remove(profileName);
                DoorProfileStorage.SaveData();
                MessagePlayer(player, LangEntry.ProfileRemoved);
                return;
            }
            
            if (optionName.Equals("update", StringComparison.OrdinalIgnoreCase))
            {
                if (!DoorProfileStorage.Data.GetProfileByName(profileName, out profileEntry))
                {
                    MessagePlayer(player, LangEntry.ProfileNotFound);
                    return;
                }

                if (args.Length < 4)
                {
                    MessagePlayer(player, LangEntry.InvalidCmd, command, "<update>", "<profile-name> <health|skin> <value>");
                    return;
                }

                switch (args[2].ToLower())
                {
                    case "health":
                        profileEntry.Health = float.Parse(args[3]);
                        break; 
                    case "skin": 
                        profileEntry.SkinId = ulong.Parse(args[3]);
                        break;
                    default:
                        DisplayHelpText(player);
                        return;
                }
                
                DoorProfileStorage.SaveData();
                MessagePlayer(player, LangEntry.ProfileUpdated);
                return;
            }
            
            DisplayHelpText(player);
        }
        
        private void BankManageCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(MANAGE_PERM))
            {
                MessagePlayer(player, LangEntry.NoPermission);
                return;
            }

            if (args.Length == 0)
            {
                DisplayHelpText(player);
                return;
            }

            string option = args[0].ToLower();
            switch (option)
            {
                case "create":
                    CreateBankEvent(player, args.Skip(1).ToArray());
                    break;
                case "remove":
                    RemoveBankEvent(player, args.Skip(1).ToArray());
                    break;
                case "parent":
                    UpdateMonumentParent(player, args.Skip(1).ToArray());
                    break;
                case "guard":
                case "door":
                case "loot":
                {
                    if (args.Length < 3)
                    {
                        MessagePlayer(player, LangEntry.InvalidArgument, "<guard|door|loot>", "<remove|update> <bank-name> <number>");
                        MessagePlayer(player, LangEntry.InvalidArgument, "<guard|door|loot>", "create <bank-name>");
                        MessagePlayer(player, LangEntry.InvalidArgument, "<guard|loot>", "profile <bank-name> <profile-name>");
                        return;
                    }

                    string option2 = args[1].ToLower();
                    if (option2.Equals("create"))
                    {
                        CreateBankEntity(player, option, args.Skip(2).ToArray());
                        return;
                    }

                    if (option2.Equals("remove"))
                    {
                        RemoveBankEntity(player, option, args.Skip(2).ToArray());
                        return;
                    }

                    if (option2.Equals("profile"))
                    {
                        ProfileBankEntity(player, option, args.Skip(2).ToArray());
                        return;
                    }

                    MessagePlayer(player, LangEntry.InvalidArgument, "<guard|door|loot>", "<remove|update> <bank-name> <number>");
                    MessagePlayer(player, LangEntry.InvalidArgument, "<guard|door|loot>", "create <bank-name>");
                    MessagePlayer(player, LangEntry.InvalidArgument, "<guard|loot>", "profile <bank-name> <profile-name>");
                    break;
                }
                case "mmr":
                    UpdateMapMarkerRadius(player, args.Skip(1).ToArray());
                    break;
                case "pdr":
                    UpdatePlayerDetectionRadius(player, args.Skip(1).ToArray());
                    break;
                case "tbr":
                    UpdateTimeBetweenResets(player, args.Skip(1).ToArray());
                    break;
                case "ato":
                    UpdateAlarmTimeout(player, args.Skip(1).ToArray());
                    break;
                case "debug":
                    if (args.Length < 3)
                    {
                        MessagePlayer(player, LangEntry.DebugInvalidSyntax);
                        return;
                    }
                    
                    if (!BankHeistInstance.FindBankByName(args[1], out BankHeistInstance heistInstance))
                    {
                        MessagePlayer(player, LangEntry.BankNotFound);
                        return;
                    }

                    if (!Enum.TryParse<DebugMode>(args[2], true, out DebugMode mode))
                    {
                        MessagePlayer(player, LangEntry.DebugInvalidOption);
                        return;
                    }
                    
                    heistInstance.DebugInfo(mode);
                    MessagePlayer(player, LangEntry.DebugToggled);   
                    break;
                case "monuments":
                    MonumentCache.Display();
                    break;
                default:
                    DisplayHelpText(player);
                    break;
            }
        }

        private void SpawnEntityCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(MANAGE_PERM))
            {
                MessagePlayer(player, LangEntry.NoPermission);
                return;
            }

            if (args.Length < 1)
            {
                MessagePlayer(player, LangEntry.InvalidCmd, command, "<type>", "");
                MessagePlayer(player, LangEntry.PrefabsToSpawn, string.Join("\n", _configData.SpawnablePrefabs.Keys));
                return;
            }

            if (!_configData.SpawnablePrefabs.TryGetValue(args[0], out string prefabPath))
            {
                MessagePlayer(player, LangEntry.PrefabsToSpawn, string.Join("\n", _configData.SpawnablePrefabs.Keys));
                return;
            }
            
            if (!CustomUtils.GetLookPoint(player, 50f, out Vector3 position))
            {
                MessagePlayer(player, LangEntry.InvalidCmd, command, "<type>", "");
                MessagePlayer(player, LangEntry.InvalidLookPoint);
                return;
            }
            
            BaseEntity entity = GameManager.server.CreateEntity(prefabPath, position, Quaternion.identity);
            if (entity is Door door)
            {
                door.transform.rotation = Quaternion.Euler((prefabPath.Contains("vault") ? 180f : 0f), player.GetNetworkRotation().eulerAngles.y + 270f, 0f);
                door.transform.position = door.transform.position + Vector3.up * 2.5f;
                door.pickup.enabled = false;
                door.repair.enabled = false;
                door.canTakeKnocker = false;
                door.canTakeCloser = false;
                door.grounded = true;
                door.decay = null;
                
                CustomUtils.DestroyComponents(entity);
            }
            else
            {
                entity.transform.rotation = Quaternion.Euler(0f, player.GetNetworkRotation().eulerAngles.y + 180f, 0f);
            }
            
            entity.Spawn();
            entity.SendNetworkUpdateImmediate();
        }

        #endregion
        
        #region Hook Subscriber
        
        private readonly List<string> _hooks = new()
        {
            "OnEntityTakeDamage",
            "OnEntityDeath",
            "OnEntityKill"
        };

        private void SubscribeToHooks(bool shouldSub = false)
        {
            if (shouldSub)
            {
                foreach (string hookName in _hooks)
                    Subscribe(hookName);
                
                return;
            }
            
            foreach (string hookName in _hooks)
                Unsubscribe(hookName);
        }

        #endregion

        #region External Hooks
        
        /*
         * NPCSpawn: prevent npcs targeting bank workers
         */
        private object OnCustomNpcTarget(BaseEntity attacker, BasePlayer target)
        {
            if (attacker == null || EntitiesCache.GetBankByEntity(attacker.net.ID) == null)
                return null;
            
            if (target != null && permission != null)
                return !permission.UserHasPermission(target.UserIDString, WORKER_PERM);
            
            return null;
        }

        /*
         * AlphaLoot: prevent overriding loot tables for guard corpses
         */
        private object CanPopulateLoot(LootableCorpse corpse)
        {
            return corpse != null && EntitiesCache.GetBankByEntity(corpse.net.ID) != null ? _trueObj : null;
        }

        /*
         * AlphaLoot: prevent overriding loot tables for loot containers
         */
        private object CanPopulateLoot(LootContainer container)
        {
            return container != null && EntitiesCache.GetBankByEntity(container.net.ID) != null ? _trueObj : null;
        }
        
        /*
         * NTeleportation: prevent players teleporting out of Bank Heists
         */
        private object CanTeleport(BasePlayer player, Vector3 position)
        {
            if (_configData.PreventTeleportFromBank && _players.Contains(player.userID))
                return GetMessage(LangEntry.OnTeleportBlocked, player.UserIDString);                
            
            return null;
        }
        
        /*
         * TruePVE: prevent TruePVE overriding damage
         */
        private object CanEntityTakeDamage(ScientistNPC npc, HitInfo info)
        {
            return npc != null && EntitiesCache.GetBankByEntity(npc.net.ID) != null ? _trueObj : null;
        }
        
        /*
         * TruePVE: prevent TruePVE overriding damage
         */
        private object CanEntityTakeDamage(Door door, HitInfo info)
        {
            return door != null && EntitiesCache.GetBankByEntity(door.net.ID) != null ? _trueObj : null;
        }
        
        #endregion

        #region API Hooks
        
        private bool IsBankHeistEntity(BaseEntity entity)
        {
            return EntitiesCache.GetBankByEntity(entity.net.ID) != null;
        }

        private string GetBankHeistName(BaseEntity entity)
        {
            return EntitiesCache.GetBankByEntity(entity.net.ID)?.displayName;
        }

        #endregion
    }
}

namespace Oxide.Plugins.BankHeistExtensionMethods
{
    internal static class CustomUtils
    {
        public static T CreateObjectWithComponent<T>(Vector3 position, Quaternion rotation, string name) where T : MonoBehaviour
        {
            GameObject gameObject = new GameObject();
            gameObject.layer = (int)Layer.Reserved1;
            gameObject.transform.SetPositionAndRotation(position, rotation);
            return gameObject.AddComponent<T>();
        }

        public static void DestroyComponents(BaseEntity entity)
        {
            UnityEngine.Object.DestroyImmediate(entity.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.DestroyImmediate(entity.GetComponent<GroundWatch>());
        }
        
        public static void DestroyMeshCollider(BaseEntity entity)
        {
            foreach (MeshCollider meshCollider in entity.GetComponentsInChildren<MeshCollider>())
                UnityEngine.Object.DestroyImmediate(meshCollider);
        }
        
        public static bool GetLookEntity<T>(BasePlayer player, float distance, int layers, out BaseEntity entity) where T : BaseEntity
        {
            if (!Physics.Raycast(player.eyes.HeadRay(), out RaycastHit hit, distance, layers, QueryTriggerInteraction.Ignore))
            {
                entity = null;
                return false;
            }

            if (hit.GetEntity() is T found)
            {
                entity = found;
                return true;
            }

            entity = null;
            return false;
        }
        
        public static bool GetLookPoint(BasePlayer player, float distance, out Vector3 position)
        {
            if (Physics.Raycast(player.eyes.position, player.eyes.HeadForward(), out RaycastHit hit, distance, -1, QueryTriggerInteraction.Ignore))
            {
                position = hit.point;
                return true;
            }

            position = Vector3.zero;
            return false;
        }
        
        public static void DrawSphere(List<Connection> connections, Vector3 origin, float radius, Color color, float duration)
        {
            if (!(connections?.Count > 0)) return;
            ConsoleNetwork.SendClientCommand(connections, "ddraw.sphere", duration, color, origin, radius);
        }
        
        public static void DrawText(List<Connection> connections, Vector3 origin, string text, Color color, float duration)
        {
            if (!(connections?.Count > 0)) return;
            ConsoleNetwork.SendClientCommand(connections, "ddraw.text", duration, color, origin, text);
        }
    }
    
    internal static class ExtensionMethods
    {
        public static bool HasPermission(this BasePlayer player, string permName)
        {
            return player != null && player.IPlayer.HasPermission(permName);
        }
        
        public static bool IsReady(this Plugin plugin) => plugin != null && plugin.IsLoaded;

        public static bool IsPlayer(this BasePlayer player) => player != null && player.userID.IsSteamId();
        
        public static bool IsWithinRadius(this BaseEntity entity, Vector3 position, float radius) => Vector3.Distance(entity.transform.position, position) <= radius;

        public static bool IsMajorityDamage(this HitInfo hitInfo, DamageType damageType) => hitInfo?.damageTypes?.GetMajorityDamageType() == damageType;
        
        public static void SafeClear(this ItemContainer container)
        {
            for (int i = container.itemList.Count - 1; i >= 0; i--)
            {
                Item item = container.itemList[i];
                item.RemoveFromContainer();
                item.Remove();
            }
        }
    }
}
