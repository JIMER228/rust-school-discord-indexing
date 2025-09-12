/*
                                   /\  /\  /\
     TTTTT  H   H  EEEE     K  K  |  \/  \/  |  NN   N   GGG
       T    H   H  E        K K   *----------*  N N  N  G
       T    HHHHH  EEE      KK     I  I  I  I   N  N N  G  GG
       T    H   H  E        K K    I  I  I  I   N   NN  G   G
       T    H   H  EEEE     K  K   I  I  I  I   N    N   GGG


This plugin (the software) is © copyright the_kiiiing.

You may not copy, modify, merge, publish, distribute, sublicense, or sell copies of this software without explicit consent from the_kiiiing.

DISCLAIMER:

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins.TugboatPiratesExt;
using PluginComponents.TugboatPirates;
using PluginComponents.TugboatPirates.Core;
using PluginComponents.TugboatPirates.Extensions.BaseNetworkable;
using PluginComponents.TugboatPirates.Loot;
using Rust;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using PluginComponents.TugboatPirates.Extensions.Lang;
using PluginComponents.TugboatPirates.Tools.Entity;
using System.Diagnostics.CodeAnalysis;

/*
 * 
 * VERSION HISTORY
 * 
 * V 1.1.0
 * - clean up default config
 * - add hooks for event start and end
 * - fix config spelling error
 * - fix path finding issues
 * - fix bumping noise of locked crate
 * - add config option for boat speed
 * - add support for notify
 * - make boat visible across the map by default
 * - add config option to remove npc corpses
 * - fix conflict with Loottable
 * - add config option for map markers
 * 
 * V 1.2.0
 * - prevent decay of event tugboats
 * - stop boat when it is destroyed
 * - kill npcs when boat is sinking
 * - add config option to prevent damage to boat
 * - display sinking time to players near the boat
 * - configurable sinking time after captain has been killed
 * - configurable sinking time after crate has been looted
 * - fix captain mounting issues
 * - prevent players from mounting boat
 * - fix loot conflicts with CustomLoot
 * - add config option for notify notification type
 * - add config option for crate timer
 * - support pvp zones with TruePVE
 * - fix boats remaining after event
 * 
 * V 1.2.1
 * - change chat commands to universal commands
 * - fix hook conflicts with TruePVE
 * 
 * V 1.2.2
 * - fix CanEntityTakeDamage hook conflict with raidable bases
 * - fix captain's note missing sometimes
 * 
 * V 1.2.3
 * - fix parenting issues with npcs
 * 
 * V 1.2.4
 * - fix rigidbody error message
 * 
 * V 1.2.5
 * - generate patrol path only on server boot
 * - changed movement from transform to rigidbody to fix performance issues
 * 
 * V 1.2.6
 * - add support for server rewards
 * 
 * V 1.2.7
 * - destroy junkpiles on collision
 * 
 * V 1.2.8
 * - fix bumping noise of locked crate (hopefully)
 * - remove global broadcast config option (obsolete)
 * - add better loot configuration
 * 
 * V 1.2.9
 * - fix NRE in start command caused by oxide hook issue
 * 
 * V 1.2.10
 * - fix ServerRewards hook call
 * - fix corpses and bags sliding off the boat
 *
 * V 1.2.11
 * - fix Entity is NULL but still in save list
 * - misc refactoring
 *
 * V 1.2.12
 * - misc improvements
 *
 * V 1.2.13
 * - fix for December rust update
 * 
 */

namespace Oxide.Plugins
{
    [Info(nameof(TugboatPirates), "The_Kiiiing", "1.2.13")]
    internal class TugboatPirates : BasePlugin<TugboatPirates, TugboatPirates.Configuration>
    {
        #region Fields

        protected override Color ChatColor => new Color(0, 1, 1);

        private const string PERM_ADMIN = "tugboatpirates.admin";

        private const string GUI_CONTAINER = "tugboatpirates.ui";

        // Change at your own risk
        private const float MAP_SIZE_SCALE = 0.55f;
        private const float NODE_TOLERANCE = 80f;

        [PluginReference, UsedImplicitly]
        private Plugin NpcSpawn, Notify, ServerRewards;

        private Timer nextEventTimer;
        private TugboatController currentBoat;

        private IReadOnlyList<Vector3> patrolPath;

        private readonly Dictionary<ulong, ValueTuple<string, bool>> npcProfiles = new();
        private readonly Dictionary<ulong, string> captainNoteTexts = new();

        private static BasePlayer startPlayer;

        private readonly IReadOnlyDictionary<string, Vector3> npcSpawnPoints = new Dictionary<string, Vector3>
        {
            // Back low
            ["back_right"] = new Vector3(2f, 2.69f, -8.8f),
            ["back_left"] = new Vector3(-2f, 2.69f, -8.8f),

            // Upper front
            ["upper_front_right"] = new Vector3(2f, 4.6f, 5f),
            ["upper_front_left"] = new Vector3(-2f, 4.6f, 5f),

            // Upper back
            ["upper_back_right"] = new Vector3(2.8f, 4.6f, -5f),
            ["upper_back_left"] = new Vector3(-2.8f, 4.6f, -5f),

            // Back roof
            ["roof_back"] = new Vector3(0, 7.2f, -1.3f),

            // Entrance
            ["entrance_right"] = new Vector3(2.7f, 5.7f, 1.4f),
            ["entrance_left"] = new Vector3(-2.7f, 5.7f, 1.4f),

            // Roof
            ["roof_right"] = new Vector3(1.4f, 8.6f, 3.2f),
            ["roof_left"] = new Vector3(-1.4f, 8.6f, 3.2f),

            // Front
            ["front"] = new Vector3(0f, 2f, 9.4f),

            // Sides
            ["right"] = new Vector3(3.6f, 2f, 0f),
            ["left"] = new Vector3(-3.6f, 2f, 0f),
        };

        #endregion

        #region Configuration

        public class Configuration
        {
            [JsonProperty("Time between events (minutes, set to -1 to disable scheduled events)")]
            public int eventDelay = 60;

            [JsonProperty("Event duration (seconds)")]
            public int eventDuration = 3600;

            [JsonProperty("Show toast when event starts")]
            public bool showToast = true;

            [JsonProperty("Announce event in chat")]
            public bool announceChat = true;

            [JsonProperty("Use Notify")]
            public bool notifyEnabled = false;

            [JsonProperty("Notify notification type")]
            public int notificationType = 0;

            [JsonProperty("Boat leave time before despawning (seconds, boat will return if value is too big)")]
            public int leaveTime = 90;

            [JsonProperty("Time before boat sinks after captain is killed (seconds)")]
            public int timeBeforeSinkingCaptain = 1200;

            [JsonProperty("Time before boat sinks after crate has been looted (seconds)")]
            public int timeBeforeSinkingLooted = 300;

            [JsonProperty("Time before boat is destroyed after sinking (seconds)")]
            public int destroyTime = 60;

            [JsonProperty("Create PVP zone around boat (requires TruePVE)")]
            public bool enablePvp = false;

            [JsonProperty("Zone radius (smaller than 25 not recommended)")]
            public float zoneRadius = 25f;

            [JsonProperty("Zone darkness (0 = invisible, higher value = darker)")]
            public int zoneDarkness = 0;

            [JsonProperty("Disable damage to pirate tugboat")]
            public bool disableDamage = true;

            [JsonProperty("Crate hack time (seconds, -1 for default time)")]
            public int crateTimerOverride = -1;

            [JsonProperty("Reward for completing the event (requires ServerRewards)")]
            public int serverRewardsAmount = 100;

            [JsonProperty("Boat configuration", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public BoatConfig boatConfig = new BoatConfig
            {
                captainNpcProfile = "captain",
                npcSpawnProfiles = new Dictionary<string, string>
                {
                    ["back_right"] = "pirate_lr",
                    ["back_left"] = "pirate_lr",

                    ["upper_front_right"] = "pirate_lr",
                    ["upper_front_left"] = "pirate_lr",

                    ["upper_back_right"] = "pirate_lr",
                    ["upper_back_left"] = "pirate_lr",

                    ["roof_back"] = "pirate_lr",

                    ["entrance_right"] = "pirate_lr",
                    ["entrance_left"] = "pirate_lr",

                    ["roof_right"] = "pirate_lr",
                    ["roof_left"] = "pirate_lr",

                    ["front"] = "pirate_lr",

                    ["right"] = "pirate_mp5",
                    ["left"] = "pirate_mp5",
                },
                interior = new List<InteriorObject>
                {
                    new InteriorObject
                    {
                        prefabPath = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab",
                        //prefabPath = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab",
                        localPosition = new Vector3(0, 2f, 4.2f),
                        rotationY = 180f,
                        lootProfile = "",
                        skinId = 1394363785
                    },
                    new InteriorObject
                    {
                        prefabPath = "assets/prefabs/deployable/research table/researchtable_deployed.prefab",
                        localPosition = new Vector3(2.1f, 2f, -4f),
                        rotationY = -90f
                    },
                    new InteriorObject
                    {
                        prefabPath = "assets/prefabs/deployable/sofa/sofa.deployed.prefab",
                        localPosition = new Vector3(-1.9f, 2, -2.2f),
                        rotationY = 90f
                    },
                    new InteriorObject
                    {
                        prefabPath = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab",
                        localPosition = new Vector3(-1.9f, 2, -4.2f),
                        rotationY = 90f,
                        lootProfile = "crate_2",
                        skinId = 811157743ul
                    },
                    new InteriorObject
                    {
                        prefabPath = "assets/prefabs/deployable/bed/bed_deployed.prefab",
                        localPosition = new Vector3(2f, 2f, -1.8f),
                        rotationY = -90f
                    },
                    new InteriorObject
                    {
                        prefabPath = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                        localPosition = new Vector3(-1.9f, 2f, 1f),
                        rotationY = 90f,
                        lootProfile = "crate_2"
                    },
                    new InteriorObject
                    {
                        prefabPath = "assets/prefabs/deployable/table/table.deployed.prefab",
                        localPosition = new Vector3(0.75f, 2f, 0.62f),
                        rotationY = 225f
                    },
                    new InteriorObject
                    {
                        prefabPath = "assets/bundled/prefabs/radtown/crate_basic.prefab",
                        localPosition = new Vector3(0.3f, 2f, 0.92f),
                        rotationY = 200f,
                        lootProfile = "crate_2"
                    },
                    new InteriorObject
                    {
                        prefabPath = "assets/prefabs/deployable/secretlab chair/secretlabchair.deployed.prefab",
                        localPosition = new Vector3(1.8f, 2f, 1.9f),
                        rotationY = 210f
                    },
                    new InteriorObject
                    {
                        prefabPath = "assets/prefabs/building/door.hinged/door.hinged.toptier.prefab",
                        localPosition = new Vector3(0.00f, 2.03f, -5.34f),
                        rotationY = 270f,
                        codeLocked = true,
                        skinId = 850289896
                    },
                    new InteriorObject
                    {
                        prefabPath = "assets/prefabs/misc/permstore/factorydoor/door.hinged.industrial.d.prefab",
                        localPosition = new Vector3(2.00f, 5.75f, 1.39f),
                        rotationY = 180f,
                    },
                    new InteriorObject
                    {
                        prefabPath = "assets/prefabs/misc/permstore/factorydoor/door.hinged.industrial.d.prefab",
                        localPosition = new Vector3(-2.00f, 5.75f, 1.39f),
                        rotationY = 0f,
                    },
                    new InteriorObject
                    {
                        prefabPath = "assets/content/structures/excavator/prefabs/diesel_collectable.prefab",
                        localPosition = new Vector3(-1.8f, 2f, 3f),
                        rotationY = 110f,
                    }
                }
            };

            [JsonProperty("Npc profiles", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, NpcConfig> npcProfiles = new Dictionary<string, NpcConfig>
            {
                ["pirate_lr"] = new NpcConfig
                {
                    name = "Pirate",
                    health = 200f,
                    enableRadio = true,
                    senseRange = 50f,
                    memoryDuration = 60f,
                    visionCone = 135f,
                    damageScale = 1f,

                    clothing = new List<LootItem> {
                        new LootItem { shortName = "hat.boonie", skinId = 965553937ul },
                        new LootItem { shortName = "hoodie", skinId = 2984978438ul },
                        new LootItem { shortName = "pants", skinId = 2984977257ul },
                        new LootItem { shortName = "attire.hide.boots", skinId = 861468674ul },
                    },
                    belt = new List<LootItem>
                    {
                        new LootItem{ shortName = "rifle.lr300" }
                    },

                    kit = string.Empty,
                    lootProfile = "pirate"
                },

                ["pirate_mp5"] = new NpcConfig
                {
                    name = "Pirate",
                    health = 150f,
                    enableRadio = true,
                    senseRange = 50f,
                    memoryDuration = 60f,
                    visionCone = 135f,
                    damageScale = 1f,

                    clothing = new List<LootItem> {
                        new LootItem { shortName = "hat.boonie", skinId = 965553937ul },
                        new LootItem { shortName = "hoodie", skinId = 2984978438ul },
                        new LootItem { shortName = "pants", skinId = 2984977257ul },
                        new LootItem { shortName = "attire.hide.boots", skinId = 861468674ul },
                    },
                    belt = new List<LootItem>
                    {
                        new LootItem{ shortName = "smg.mp5" }
                    },

                    kit = string.Empty,
                    lootProfile = "pirate"
                },

                ["captain"] = new NpcConfig
                {
                    name = "Captain",
                    health = 100f,
                    enableRadio = false,
                    senseRange = 0f,
                    memoryDuration = 0f,
                    visionCone = 0f,
                    damageScale = 1f,

                    clothing = new List<LootItem> {
                        new LootItem { shortName = "hat.boonie", skinId = 965553937ul },
                        new LootItem { shortName = "tshirt", skinId = 811762477ul },
                        new LootItem { shortName = "pants.shorts", skinId = 849256923ul },
                        new LootItem { shortName = "attire.hide.boots", skinId = 861468674ul },
                    },
                    belt = new List<LootItem>
                    {
                        new LootItem { shortName = "mace.baseballbat" }
                    },

                    kit = string.Empty,
                    lootProfile = "pirate"
                }
            };

            [JsonProperty("Loot profiles", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, LootManager.LootTable> lootProfiles = new()
            {
                ["crate_main"] = new LootManager.LootTable
                {
                    enabled = true,
                    minItems = 4,
                    maxItems = 8,
                    items = new List<LootManager.LootItem>
                    {
                        new("scrap", 8, 20, 1f),
                        new("metal.refined", 10, 25, 0.7f),

                        new("lmg.m249", 1, 1, 0.05f),
                        new("rifle.ak.diver", 1, 1, 0.1f),
                        new("rifle.bolt", 1, 1, 0.1f),
                        new("smg.mp5", 1, 1, 0.2f),
                        new("smg.thompson", 1, 1, 0.2f),

                        new("ammo.shotgun", 4, 8, 0.2f),
                        new("ammo.shotgun.fire", 4, 8, 0.2f),
                        new("ammo.shotgun.slug", 4, 8, 0.2f),
                        new("ammo.pistol", 15, 30, 0.2f),
                        new("ammo.pistol.hv", 15, 30, 0.2f),
                        new("ammo.pistol.fire", 15, 30, 0.2f),
                        new("ammo.rifle", 12, 24, 0.2f),
                        new("ammo.rifle.hv", 12, 24, 0.2f),
                        new("ammo.rifle.incendiary", 12, 24, 0.2f),
                    },
                },
                ["crate_2"] = new LootManager.LootTable
                {
                    enabled = true,
                    minItems = 4,
                    maxItems = 8,
                    items = new List<LootManager.LootItem>
                    {
                        new("scrap", 2, 20, 1f),
                        new("metal.refined", 4, 8, 0.5f),
                        new("gears", 1, 3, 0.2f),
                        new("sewingkit", 1, 3, 0.2f),
                        new("rope", 1, 3, 0.2f),
                        new("sheetmetal", 1, 2, 0.2f),

                        new("grenade.molotov", 1, 2, 0.1f),
                        new("grenade.f1", 1, 4, 0.1f),
                        new("telephone", 1, 1, 0.1f),
                        new("multiplegrenadelauncher", 1, 1, 0.1f),
                    },
                },
                ["pirate"] = new LootManager.LootTable
                {
                    enabled = true,
                    minItems = 2,
                    maxItems = 5,
                    items = new List<LootManager.LootItem>
                    {
                        new("scrap", 2, 6, 1f),
                        new("bottle.vodka", 1, 1, 0.7f),
                        new("pistol.eoka", 1, 1, 0.2f),
                        new("ammo.handmade.shell", 5, 10, 0.2f),
                        new("rope", 1, 3, 0.3f),
                        new("sewingkit", 1, 2, 0.3f),
                    },
                }
            };

            [JsonProperty("Priate quotes (included in captain's note)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> pirateQuotes = new List<string>
            {
                "If rum can’t fix it, ye are not using enough rum.",
                "But… why is the rum gone?",
                "Be who you arrrr...",
                "All for rum and rum for all!",
                "Land was created to provide a place for boats to visit.",
                "If ye can read this ye be stupid."
            };

            public void RemoveUnsupportedItems()
            {
                boatConfig.interior.RemoveAll(x => x.prefabPath == "assets/bundled/prefabs/radtown/oil_barrel.prefab" || x.prefabPath == "assets/prefabs/deployable/waterpurifier/waterpurifier.deployed.prefab");
            }
        }

        [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
        [SuppressMessage("ReSharper", "ConvertToConstant.Global")]
        public class BoatConfig
        {
            [JsonProperty("Npc profile for captain (must be a valid profile)")]
            public string captainNpcProfile;

            [JsonProperty("Boat speed multiplier")]
            public float speedMultiplier = 1f;

            [JsonProperty("Enable map marker")]
            public bool useMapMarker = true;

            [JsonProperty("Map marker color (hex format)")]
            public string markerColor = "#2480FB";

            [JsonProperty("Map marker name")]
            public string markerName = "Pirate Ship";

            [JsonProperty("Npc spawn locations and profiles", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, string> npcSpawnProfiles = new Dictionary<string, string>();

            [JsonProperty("Interior objects (crates, decoration, etc.)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<InteriorObject> interior = new List<InteriorObject>();
        }

        public class InteriorObject
        {
            [JsonProperty("Prefab path")]
            public string prefabPath;
            [JsonProperty("Rotation")]
            public float rotationY;
            [JsonProperty("Position on boat")]
            public Vector3 localPosition;
            [JsonProperty("Skin id")]
            public ulong skinId;
            [JsonProperty("Loot profile (only for crates, leave empty for default loot)", NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string lootProfile = string.Empty;
            [JsonProperty("Add code lock (only for doors)", NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool? codeLocked = null;
        }

        public class NpcConfig
        {
            public string name = "Pirate";
            public float health = 150f;
            public bool enableRadio = true;

            public float senseRange = 50f;
            public float visionCone = 135f;
            public float damageScale = 1f;
            public float memoryDuration = 60f;

            public bool removeCorpseAfterDeath = false;

            public string lootProfile = string.Empty;

            public string kit = string.Empty;

            [JsonProperty("Clothing items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<LootItem> clothing = new();
            [JsonProperty("Belt items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<LootItem> belt = new();
        }

        public class LootItem
        {
            public string shortName;
            public int amount = 1;
            public ulong skinId;
        }

        #endregion

        #region Commands

        void CmdStart(IPlayer iplayer)
        {
            if (!permission.UserHasPermission(iplayer.Id, PERM_ADMIN))
            {
                iplayer.Reply("You don't have permission to do that");
                return;
            }

            StartEvent(iplayer.Object as BasePlayer, true);
        }

        void CmdStop(IPlayer iplayer)
        {
            if (!permission.UserHasPermission(iplayer.Id, PERM_ADMIN))
            {
                iplayer.Reply("You don't have permission to do that");
                return;
            }

            EndEvent(true);
            iplayer.Reply("The event has ended");
        }

        #endregion

        #region Hooks

        protected override void Init()
        {
            base.Init();

            permission.RegisterPermission(PERM_ADMIN, this);

            AddCovalenceCommand("tugboatstart", nameof(CmdStart));
            AddCovalenceCommand("tugboatstop", nameof(CmdStop));

            Unsubscribe();
        }

        protected override void OnServerInitialized(bool initial)
        {
            base.OnServerInitialized(initial);

            patrolPath = TugboatPathFinder.GeneratePatrolPath(initial);

            if (NpcSpawn == null)
            {
                LogError("NpcSpawn is required to spawn NPCs. You can download it here: https://codefling.com/extensions/npc-spawn");
            }

            ScheduleEvent(0.5f);
        }

        protected override void Unload()
        {
            currentBoat?.Destroy();
            nextEventTimer?.Destroy();

            base.Unload();
        }

        #region Event Hooks

        [Hook] void OnCrateHack(HackableLockedCrate crate)
        {
            if (Config.crateTimerOverride >= 0 && currentBoat?.crates.Contains(crate) == true)
            {
                crate.hackSeconds = HackableLockedCrate.requiredHackSeconds - Config.crateTimerOverride;
            }
        }

        [Hook] object CanMountEntity(BasePlayer player, BaseMountable entity)
        {
            if (!player.IsNpc && entity.GetParentEntity()?.net.ID == currentBoat?.Boat.net.ID)
            {
                return false;
            }

            return null;
        }

        [Hook] void OnLootEntityEnd(BasePlayer player, HackableLockedCrate crate)
        {
            currentBoat?.OnCrateLooted(crate, player);
        }

        [Hook] object OnEntityTakeDamage(Tugboat boat, HitInfo info)
        {
            if (Config.disableDamage && boat.net.ID == currentBoat?.Boat.net.ID)
            {
                return true;
            }

            return null;
        }

        [Hook] void OnCorpsePopulate(ScientistNPC npc, NPCPlayerCorpse corpse)
        {
            if (npc == null || corpse == null)
            {
                return;
            }

            var npcId = npc.userID;
            if (npcProfiles.TryGetValue(npcId, out var profile))
            {
                string captainNoteText = captainNoteTexts.GetValueOrDefault(npcId, null);
                var lootTable = GetLootProfile(profile.Item1, captainNoteText);
                if (lootTable != null)
                {
                    Instance.NextFrame(() =>
                    {
                        if (!corpse.IsValid())
                        {
                            LogError($"Failed to populate NPC corpse ({npc.displayName}) - invalid corpse");
                            return;
                        }

                        LootManager.FillWithLoot(corpse.containers.First(), lootTable);

                        // Drop bag
                        if (profile.Item2 && !corpse.IsDestroyed)
                        {
                            corpse.Kill();
                        }
                    });
                }

                //npcProfiles.Remove(npcId);
                captainNoteTexts.Remove(npcId);
            }
        }

        [Hook] void OnEntitySpawned(NPCPlayerCorpse corpse)
        {
            if (corpse != null && npcProfiles.ContainsKey(corpse.playerSteamID))
            {
                UnityEngine.Object.Destroy(corpse.GetComponent<Rigidbody>());
                LogDebug("Destroy corpse rb");
            }
        }

        [Hook] void OnEntitySpawned(DroppedItemContainer bag)
        {
            NextFrame(() =>
            {
                if (bag != null && npcProfiles.ContainsKey(bag.playerSteamID))
                {
                    UnityEngine.Object.Destroy(bag.GetComponent<Rigidbody>());
                    bag.transform.position = bag.transform.position.WithY(bag.transform.position.y - 1);
                    LogDebug("Destroy bag rb");
                }
            });
        }

        // Loottable
        [Hook] object OnCorpsePopulate(LootableCorpse corpse)
        {
            if (corpse != null && npcProfiles.ContainsKey(corpse.playerSteamID))
            {
                LogDebug("prevent populate corpse (Loottable)");
                return false;
            }

            return null;
        }

        // CustomLoot
        [Hook] object OnCustomLootNPC(ulong netId)
        {
            var npc = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as BasePlayer;
            if (!npc.IsNullOrDestroyed() && npcProfiles.ContainsKey(npc!.userID))
            {
                LogDebug("prevent populate corpse (Custom Loot)");
                return false;
            }

            return null;
        }

        // TruePVE
        [Hook] object CanEntityTakeDamage(BasePlayer target, HitInfo info)
        {
            if (!Config.enablePvp || currentBoat == null)
            {
                return null;
            }

            BasePlayer attacker = info?.InitiatorPlayer;
            if (attacker?.IsNpc == false && target?.IsNpc == false)
            {
                return (currentBoat.Zone.Players.Contains(attacker) && currentBoat.Zone.Players.Contains(target)) ? true : null;
            }

            return null;
        }

        #endregion

        [Hook] void OnTugboatPiratesEnded()
        {
            Unsubscribe();
            ScheduleEvent();
        }

#if DEBUG
        [Hook] object OnHammerHit(BasePlayer player, HitInfo info)
        {
            var entity = info.HitEntity;
            var parent = entity?.GetParentEntity();
            if (entity != null && parent != null)
            {
                LogDebug($"Local pos {entity.transform.localPosition} local rot {entity.transform.localEulerAngles.y:N0}");
                LogDebug($"Calculated local pos {entity.transform.position - parent.transform.position}");
                LogDebug($"new InteriorObject\r\n                        {{\r\n                            prefabPath = \"{entity.PrefabName}\",\r\n                            localPosition = new Vector3({entity.transform.localPosition.x:N2}f, {entity.transform.localPosition.y:N2}f, {entity.transform.localPosition.z:N2}f),\r\n                            rotationY = {entity.transform.localEulerAngles.y:N0}f,\r\n                            lootProfile = \"crate_main\"\r\n                        }},");
            }

            return null;
        }
#endif

        private void Subscribe()
        {
            LogDebug("Subscribe");

            if (Config.disableDamage)
            {
                Subscribe(nameof(OnEntityTakeDamage));
            }
            if (Config.crateTimerOverride >= 0)
            {
                Subscribe(nameof(OnCrateHack));
            }
            if (Config.enablePvp)
            {
                Subscribe(nameof(CanEntityTakeDamage));
            }

            Subscribe(nameof(OnLootEntityEnd));
            Subscribe(nameof(CanMountEntity));
            Subscribe(nameof(CanMountEntity));
            Subscribe(nameof(OnCorpsePopulate));
            Subscribe(nameof(OnCustomLootNPC));
        }

        private void Unsubscribe()
        {
            LogDebug("Unsubscribe");

            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(OnCrateHack));
            Unsubscribe(nameof(OnLootEntityEnd));
            Unsubscribe(nameof(CanMountEntity));
            Unsubscribe(nameof(CanMountEntity));
            Unsubscribe(nameof(OnCorpsePopulate));
            Unsubscribe(nameof(OnCustomLootNPC));
            Unsubscribe(nameof(CanEntityTakeDamage));
        }

        #endregion

        #region Path Finder

        private class TugboatPathFinder
        {
            private int targetNodeIndex = -1;
            private readonly float sqrVisitDistance;

            private int skip;

            private readonly IReadOnlyList<Vector3> nodes;

            private bool leave;
            private Vector3 finalDestination;

            public TugboatPathFinder(IReadOnlyList<Vector3> path, float visitDistance)
            {
                sqrVisitDistance = visitDistance * visitDistance;
                nodes = path;
                skip = 1;
            }

            public Vector3 GetCurrentNode()
            {
                return nodes[targetNodeIndex];
            }

            public Vector3 GetNextNode()
            {
                int idx = targetNodeIndex + skip;
                if (idx >= nodes.Count)
                {
                    idx = 0;
                }

                return nodes[idx];
            }

            public Vector3 GetRandomStartPosition(float height = 0)
            {
                float outside = TerrainMeta.Size.x * MAP_SIZE_SCALE;

                float x = UnityEngine.Random.Range(-outside, outside);
                float y = UnityEngine.Random.Range(-outside, outside);

                if (UnityEngine.Random.Range(0, 2) == 1)
                {
                    x = outside * Mathf.Sign(x);
                }
                else
                {
                    y = outside * Mathf.Sign(y);
                }

                return new Vector3(x, height, y);
            }

            public void SetFinalDestination()
            {
                finalDestination = GetDespawnPosition(GetCurrentNode());
                leave = true;
            }

            public Vector3 GetCurrentTarget(Vector3 currentPosition)
            {
                if (leave)
                {
                    return finalDestination;
                }

                if (targetNodeIndex < 0)
                {
                    targetNodeIndex = GetClosestNode(currentPosition);
                }

                Vector3 currentTarget = GetCurrentNode();

                if ((currentPosition - currentTarget).sqrMagnitude < sqrVisitDistance)
                {
                    targetNodeIndex += skip;
                }

                ValidateNodeIndex();
                

                return GetCurrentNode();
            }

            public void Reverse()
            {
                skip *= -1;
                targetNodeIndex += skip;
                ValidateNodeIndex();
            }

            private void ValidateNodeIndex()
            {
                if (targetNodeIndex >= nodes.Count)
                {
                    targetNodeIndex = 0;
                }
                else if (targetNodeIndex < 0)
                {
                    targetNodeIndex = nodes.Count - 1;
                }
            }

            private Vector3 GetDespawnPosition(Vector3 lastNode, float height = 0)
            {
                lastNode.y = 0;
                lastNode.Normalize();

                lastNode *= TerrainMeta.Size.x;

                lastNode.y = height;

                return lastNode;
            }

            private int GetClosestNode(Vector3 position)
            {
                int result = 0;
                float num = float.PositiveInfinity;
                for (int i = 0; i < nodes.Count; i++)
                {
                    Vector3 b = nodes[i];
                    float num2 = Vector3.Distance(position, b);
                    if (num2 < num)
                    {
                        result = i;
                        num = num2;
                    }
                }

                return result;
            }

            public static List<Vector3> GeneratePatrolPath(bool forceGenerate = false)
            {
                const string dataFileName = "TugboatPiratesPatrolPath";

                var data = Interface.Oxide.DataFileSystem.ReadObject<PathData>(dataFileName);
                if (!data.IsValid || forceGenerate)
                {
                    var sw = new Stopwatch();
                    LogWarning("Generating tugboat patrol path (expect some lag)");
                    sw.Start();
                    data.path = BaseBoat.GenerateOceanPatrolPath(NODE_TOLERANCE * 1.1f);
                    sw.Stop();
                    Log($"Patrol path generated in {sw.Elapsed.TotalSeconds:N2}s");

                    Interface.Oxide.DataFileSystem.WriteObject(dataFileName, data);
                }

                return data.path;
            }

            private class PathData
            {
                [JsonIgnore]
                public bool IsValid => !path.IsNullOrEmpty();
                public List<Vector3> path;
            }
        }

        #endregion

        #region Functions

        private void ScheduleEvent(float delayMpl = 1f)
        {
            if (Config.eventDelay < 1)
            {
                return;
            }

            Log($"Schedule next event in {(Config.eventDelay * delayMpl):N0} min");

            if (nextEventTimer != null && !nextEventTimer.Destroyed)
            {
                nextEventTimer.Destroy();
            }

            nextEventTimer = timer.In(Config.eventDelay * 60 * delayMpl, () => StartEvent(null));
        }

        private void StartEvent(BasePlayer player, bool force = false)
        {
            if (!EndEvent(force))
            {
                player?.ChatMessage("Failed to start event, another event is still running");
                Log("Failed to start event, another event is still running");
                return;
            }

            var pathFinder = new TugboatPathFinder(patrolPath, NODE_TOLERANCE);

#if DEBUG
            startPlayer = player;
            Vector3 startPosition = startPlayer?.transform.position ?? pathFinder.GetRandomStartPosition();
#else
            Vector3 startPosition = pathFinder.GetRandomStartPosition();
#endif

            Subscribe();

            var rotation = Quaternion.LookRotation(startPosition - pathFinder.GetCurrentTarget(startPosition)) * Quaternion.Euler(Vector3.up * 180);
            var boat = GameManager.server.CreateEntity("assets/content/vehicles/boats/tugboat/tugboat.prefab", startPosition, rotation) as Tugboat;
            if (boat == null)
            {
                LogError("Failed to spawn boat - boat is null");
                Unsubscribe();
                return;
            }

            boat.Spawn();

            boat.EnableGlobalBroadcast(true);
            boat.EnableSaving(false);

            // Prevent OnEntityTakeDamage hook conflict with NpcSpawn
            boat.skinID = 14922524;

            currentBoat = boat.gameObject.AddComponent<TugboatController>();
            currentBoat.PathFinder = pathFinder;
            currentBoat.serverRewardsAmount = Config.serverRewardsAmount;
            currentBoat.Spawn(Config.boatConfig);
        }

        private bool EndEvent(bool force)
        {
            startPlayer = null;

            if (currentBoat == null)
            {
                return true;
            }

            if (currentBoat.State == EventState.ENDED || force)
            {
                currentBoat?.Destroy();
                return true;
            }

            return false;
        }

        #endregion

        #region Boat Controller

        public enum EventState {
            PREPARING = 1,
            RUNNING = 2,
            CAPTAIN_DEAD = 4,
            LOOTED = 8,
            LEAVING = 16,
            ENDED = 32,
        }

        private class TugboatController : FacepunchBehaviour
        {
            public EventState State { get; private set; }

            private VendingMachineMapMarker vendingMarker;
            private MapMarkerGenericRadius colorMarker;

            private readonly HashSet<ulong> npcs = new();

            private readonly List<BaseEntity> entities = new();
            public readonly HashSet<HackableLockedCrate> crates = new();

            public Tugboat Boat { get; private set; }
            private BasePlayer captain;

            public TugboatPathFinder PathFinder { get; set; }

            public float speedMultiplier;
            public bool useMarker;
            public string markerColor;
            public string markerName;
            public float serverRewardsAmount;

            private bool boatDestroyed;
            private bool captainDead;

            private Vector3 target;

            private string lockCode;

            private int timeRemaining;

            public Zone Zone { get; private set; }

            private bool calledEnd;

            #region Mono

            void Awake()
            {
                enabled = false;
                State = EventState.PREPARING;

                Boat = GetComponent<Tugboat>();
                if (Boat == null)
                {
                    LogError("Tugboat is null");
                    Destroy(this);
                    return;
                }

                Boat.GetTriggerParent().ParentNPCPlayers = true;

                #if DEBUG
                InvokeRepeating(DebugPath, 1f, 1f);
                #endif

                Boat.CancelInvoke(Boat.BoatDecay);

                CreateZone();
            }

            void Update()
            {
                if (Boat.IsDying && !boatDestroyed)
                {
                    LogDebug("ship destroyed");
                    boatDestroyed = true;
                    if (Config.announceChat)
                    {
                        BroadcastLang("ship_destroyed");
                    }

                    StartSinking();
                }

                if (captain != null && captain.IsDead() && !captainDead && !boatDestroyed)
                {
                    LogDebug("captain is dead");
                    captainDead = true;
                    OnCaptainKilled();
                }
            }

            void FixedUpdate()
            {
                if (!boatDestroyed && !Boat.EngineOn())
                {
                    Boat.SetFlag(BaseEntity.Flags.On, true);
                    Boat.SetFlag(BaseEntity.Flags.Reserved1, true);
                }

                if (!captainDead && !boatDestroyed)
                {
                    UpdateMovement();
                }
            }

            void OnDestroy()
            {
                try
                {
                    EndEvent();

                    StopAllCoroutines();
                    CancelInvoke();

                    Boat.DismountAllPlayers();

                    Zone.Destroy();

                    EntityTools.Kill(entities);

                    if (!Boat.IsDestroyed)
                    {
                        Boat.Kill();
                    }
                }
                catch(Exception ex)
                {
                    LogError("Error in OnDestroy " + ex.Message);
                }
            }

            void NetworkUpdate()
            {
                foreach(var ent in crates)
                {
                    ent.SendNetworkUpdate();
                }
            }

            public void Destroy()
            {
                if (Boat?.IsDestroyed == false)
                {
                    Boat.Kill(BaseNetworkable.DestroyMode.Gib);
                }
                else
                {
                    Destroy(this);
                }
            }

            void StartSinking()
            {
                LogDebug("start sinking");
                EndEvent();

                if (!Boat.IsDying)
                {
                    Boat.SetFlag(BaseEntity.Flags.Broken, b: true);
                    Boat.repair.enabled = false;
                    boatDestroyed = true;
                }

                CancelInvoke(WaterCheck);
                Invoke(WaterCheck, 10f);

                CancelInvoke(Destroy);
                Invoke(Destroy, Config.destroyTime);
            }

            void WaterCheck()
            {
                LogDebug("Water check");

                Boat.SetFlag(BaseEntity.Flags.Reserved1, false);

                foreach (var ent in entities.ToArray())
                {
                    var npc = ent as ScientistNPC;
                    if (npc != null)
                    {
                        LogDebug($"Water factor {npc.WaterFactor():N2}");
                        if (npc.WaterFactor() > 0.8f)
                        {
                            npc.Invoke(() => npc.Die(), UnityEngine.Random.Range(0.1f, 2f));
                            entities.Remove(ent);
                        }
                    }
                }
            }

            #endregion

            #region Event

            private void OnCaptainKilled()
            {
                if (Config.announceChat)
                {
                    BroadcastLang("captain_killed", Config.timeBeforeSinkingCaptain.ToString());
                }
                
                State = EventState.CAPTAIN_DEAD;
                timeRemaining = Config.timeBeforeSinkingCaptain;
            }

            public void OnCrateLooted(HackableLockedCrate crate, BasePlayer looter)
            {
                if (State == EventState.LOOTED)
                {
                    return;
                }

                crates.Remove(crate);

                if (crates.Count < 1)
                {
                    if (Config.announceChat)
                    {
                        BroadcastLang("ship_looted", Config.timeBeforeSinkingLooted.ToString());
                    }

                    if (looter != null && Instance?.ServerRewards != null)
                    {
                        Instance.ServerRewards.Call("AddPoints", looter.userID.Get(), serverRewardsAmount);
                        ChatMessageLang(looter, "event_reward", serverRewardsAmount.ToString());
                    }

                    State = EventState.LOOTED;
                    timeRemaining = Config.timeBeforeSinkingLooted;
                }
            }

            private void StartEvent()
            {
                State = EventState.RUNNING;
                enabled = true;

                timeRemaining = Config.eventDuration;

                StartCoroutine(EventUpdateCoro());

                LogDebug("OnTugboatPiratesStarted");
                Interface.CallHook("OnTugboatPiratesStarted");
            }

            private void EndEvent()
            {
                if (calledEnd)
                {
                    return;
                }
                calledEnd = true;

                State = EventState.ENDED;

                foreach (var player in Zone.Players)
                {
                    DestroyGui(player);
                }

                LogDebug("OnTugboatPiratesEnded");
                Interface.CallHook("OnTugboatPiratesEnded");
            }

            private IEnumerator EventUpdateCoro()
            {
                bool announcedStart = false;
                bool announcedLeave5m = false;
                bool announcedLeave = false;

                while (timeRemaining > 0 && State != EventState.ENDED)
                {
                    if (State == EventState.RUNNING)
                    {
                        if (!announcedStart)
                        {
                            announcedStart = true;
                            if (Config.announceChat)
                            {
                                BroadcastLang("ship_start", MapHelper.PositionToString(PathFinder.GetCurrentNode()));
                            }
                            if (Config.notifyEnabled)
                            {
                                BroadcastNotify("ship_start", MapHelper.PositionToString(PathFinder.GetCurrentNode()));
                            }
                            if (Config.showToast)
                            {
                                ShowToastLang("ship_start_toast");
                            }
                        }

                        if (!announcedLeave5m && timeRemaining <= 300 + Config.leaveTime)
                        {
                            announcedLeave5m = true;
                            if (Config.announceChat)
                            {
                                BroadcastLang("ship_leave_5m");
                            }
                            if (Config.notifyEnabled)
                            {
                                BroadcastNotify("ship_leave_5m");
                            }
                        }

                        if (!announcedLeave && timeRemaining <= Config.leaveTime)
                        {
                            announcedLeave = true;
                            if (Config.announceChat)
                            {
                                BroadcastLang("ship_leave");
                            }
                            if (Config.notifyEnabled)
                            {
                                BroadcastNotify("ship_leave");
                            }

                            State = EventState.LEAVING;
                            speedMultiplier *= 1.6f;
                            PathFinder.SetFinalDestination();
                        }
                    }

                    UpdateGui();

                    yield return new WaitForSecondsRealtime(1f);
                    timeRemaining--;
                }

                if (State != EventState.ENDED)
                {
                    StartSinking();
                }
            }

            #endregion

            #region Coroutines

            public void Spawn(BoatConfig config)
            {
                if (PathFinder == null)
                {
                    LogError("Cannot spawn tugboat - path finder is null");
                    Destroy();
                    return;
                }

                Log($"Spawning event at {Boat.transform.position}");

                lockCode = UnityEngine.Random.Range(1111, 10000).ToString();

                StartCoroutine(SpawnCoro(config));
            }

            private IEnumerator SpawnCoro(BoatConfig config)
            {
                speedMultiplier = config.speedMultiplier * 0.9f;

                useMarker = config.useMapMarker;
                markerColor = config.markerColor;
                markerName = config.markerName;
                CreateMapMarker();
                yield return CoroutineEx.waitForEndOfFrame;

                SpawnCaptain(GetNpcConfig(config.captainNpcProfile));
                yield return CoroutineEx.waitForEndOfFrame;

                SpawnNpcs(config.npcSpawnProfiles.Select(x => GetSpawnConfig(x.Value, x.Key)));
                yield return CoroutineEx.waitForEndOfFrame;

                foreach (var obj in config.interior)
                {
                    var lootProfile = GetLootProfile(obj.lootProfile);
                    SpawnInteriorObject(obj, lootProfile);
                    yield return CoroutineEx.waitForEndOfFrame;
                }

                yield return CoroutineEx.waitForSeconds(2);

                StartEvent();
            }

            #endregion

            #region Movement

            void UpdateMovement()
            {
                target = PathFinder.GetCurrentTarget(Boat.transform.position);

                var desiredForward = target - Boat.transform.position;
                Boat.rigidBody.AddForceAtPosition(desiredForward.normalized * (Boat.engineThrust * speedMultiplier), Boat.thrustPoint.position, ForceMode.Force);

                var desiredRotation = Quaternion.LookRotation(desiredForward, Vector3.up);
                Boat.rigidBody.MoveRotation(Quaternion.RotateTowards(Boat.rigidBody.rotation, desiredRotation, 15f * speedMultiplier * Time.fixedDeltaTime));
            }

            void DebugPath()
            {
                if (startPlayer != null && startPlayer.IsConnected)
                {
                    DrawSphere(startPlayer, target, NODE_TOLERANCE, Color.red, 1f);
                    DrawSphere(startPlayer, PathFinder.GetNextNode(), NODE_TOLERANCE, Color.red, 1f);
                }
            }

            private void OnCollisionEnter(Collision collision)
            {
                if (Boat == null || Boat.IsDestroyed)
                {
                    return;
                }

                var entity = collision.GetEntity();
                if (entity == Boat)
                {
                    return;
                }

                if (entity != null && !entity.IsDestroyed)
                {
                    //Dprint($"Collision with {entity.ShortPrefabName}");
                    if (entity.PrefabName.Contains("junkpile_water"))
                    {
                        entity.Kill();
                    }
                }
            }

            #endregion

            #region Interior

            public void SpawnInteriorObject(InteriorObject obj, LootManager.LootTable lootConfig)
            {
                var entity = GameManager.server.CreateEntity(obj.prefabPath);
                if (entity == null)
                {
                    LogError($"Failed to spawn prefab '{obj.prefabPath}' - prefab does not exist");
                    return;
                }
                entity.enableSaving = false;
                
                if (entity is HackableLockedCrate crate)
                {
                    crate.SendMessage("SetWasDropped", SendMessageOptions.DontRequireReceiver);
                    crates.Add(crate);
                }

                entity.skinID = obj.skinId;
                entity.Spawn();

                entity.enableSaving = false;

                // Remove da fuckin bumping noise
                // Who needs physics anyways
                Destroy(entity.GetComponent<Rigidbody>());
                
                entity.SetParent(Boat);
                entity.transform.localPosition = obj.localPosition;
                entity.transform.localEulerAngles = Vector3.up * obj.rotationY;

                entity.SendNetworkUpdate();

                if (entity is StorageContainer container && lootConfig != null)
                {
                    LootManager.FillWithLoot(container, lootConfig);
                }

                if (entity is Door && obj.codeLocked == true)
                {
                    var codeLock = EntityTools.CreateEntity<CodeLock>("assets/prefabs/locks/keypad/lock.code.prefab");
                    codeLock.gameObject.Identity();
                    codeLock.SetParent(entity, entity.GetSlotAnchorName(BaseEntity.Slot.Lock));
                    codeLock.Spawn();
                    codeLock.code = lockCode;
                    codeLock.hasCode = true;
                    entity.SetSlot(BaseEntity.Slot.Lock, codeLock);
                    codeLock.SetFlag(BaseEntity.Flags.Locked, true);
                }

                if (entity is BaseOven)
                {
                    entity.SetFlag(BaseEntity.Flags.On, true);
                }

                entities.Add(entity);
            }

            #endregion

            #region NPC

            public void SpawnCaptain(NpcConfig config)
            {
                if (config == null)
                {
                    LogError("Failed to spawn captain - npc profile is null. Make sure the captain npc profile in the config is valid");
                    Destroy();
                    return;
                }

                var sb = new StringBuilder();
                sb.AppendLine(String.Join(string.Empty, Enumerable.Repeat("+", 36)));
                sb.AppendLine();
                sb.AppendLine(Config.pirateQuotes.GetRandom());
                sb.AppendLine();
                sb.AppendLine(String.Join(string.Empty, Enumerable.Repeat(" ", 24)) + lockCode);
                sb.AppendLine(String.Join(string.Empty, Enumerable.Repeat("+", 36)));

                config.health = 10f;
                captain = CreateNpc(config, new Vector3(0, 5.9f, 2.1f), true, sb.ToString());

                if (captain == null)
                {
                    LogError("Failed to spawn captain");
                    return;
                }

                foreach(var item in config.belt)
                {
                    var def = ItemManager.FindItemDefinition(item.shortName);
                    if (def == null)
                    {
                        continue;
                    }

                    captain.inventory.containerMain.AddItem(def, item.amount, item.skinId);
                }

                Invoke(() => { Boat.AttemptMount(captain, false); }, 1f);
            }

            public void SpawnNpcs(IEnumerable<Tuple<NpcConfig, Vector3>> spawnProfiles)
            {
                foreach(var profile in spawnProfiles)
                {
                    if (profile == null)
                    {
                        continue;
                    }

                    CreateNpc(profile.Item1, profile.Item2);
                }
            }

            private ScientistNPC CreateNpc(NpcConfig config, Vector3 localPosition, bool idleOnly = false, string captainNoteText = null)
            {
                if (Instance.NpcSpawn == null)
                {
                    LogError("Failed to spawn npc - NpcSpawn is not loaded");
                    return null;
                }

                NpcSpawnConfig npcConfig = new NpcSpawnConfig
                {
                    Name = config.name,
                    WearItems = config.clothing.Select(x => new NpcSpawnNpcWear { ShortName = x.shortName, SkinID = x.skinId }),
                    BeltItems = idleOnly ? new NpcSpawnNpcBelt[0] : config.belt.Select(x => new NpcSpawnNpcBelt { ShortName = x.shortName, Amount = x.amount, SkinID = x.skinId, Ammo = string.Empty, Mods = new string[0] }),
                    Kit = config.kit,
                    Health = config.health,
                    RoamRange = 0,
                    ChaseRange = 0,
                    SenseRange = config.senseRange,
                    ListenRange = config.senseRange / 2f,
                    AttackRangeMultiplier = 1f,
                    VisionCone = config.visionCone,
                    DamageScale = config.damageScale,
                    TurretDamageScale = 1f,
                    AimConeScale = 1f,
                    DisableRadio = !config.enableRadio,
                    CanRunAwayWater = true,
                    CanSleep = false,
                    Speed = 0,
                    AreaMask = 1,
                    AgentTypeID = -1372625422,
                    HomePosition = string.Empty,
                    MemoryDuration = config.memoryDuration,
                    States = idleOnly ? new HashSet<string> { "IdleState" } : new HashSet<string> { "IdleState", "CombatStationaryState" }
                };

                var scientist = Instance.NpcSpawn?.Call("SpawnNpc", Boat.transform.position, JObject.FromObject(npcConfig)) as ScientistNPC;
                if (scientist == null)
                {
                    LogError("Failed to spawn npc - scientist is null");
                    return null;
                }

                scientist.SetParent(Boat);
                scientist.transform.localPosition = localPosition;

                entities.Add(scientist);
                npcs.Add(scientist.userID);

                Instance.RegisterNpc(scientist, config.lootProfile, config.removeCorpseAfterDeath, captainNoteText);

                return scientist;
            }

            #endregion

            #region Map marker

            public void CreateMapMarker()
            {
                if (!useMarker)
                {
                    return;
                }

                var color = new Color(36f / 255f, 128f / 255f, 251f / 255f);
                if (!ColorUtility.TryParseHtmlString(markerColor, out color))
                {
                    LogWarning($"Failed to parse map marker color '{color}' - make sure it is a valid hex color");
                }
                color.a = 0.6f;

                vendingMarker = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", Boat.transform.position).GetComponent<VendingMachineMapMarker>();
                vendingMarker.markerShopName = markerName;
                vendingMarker.enableSaving = false;
                vendingMarker.Spawn();

                colorMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab").GetComponent<MapMarkerGenericRadius>();
                colorMarker.color1 = color;
                colorMarker.color2 = colorMarker.color1;
                colorMarker.radius = 0.2f;
                colorMarker.alpha = 1f;
                colorMarker.enableSaving = false;
                colorMarker.SetParent(vendingMarker);
                colorMarker.Spawn();

                entities.Add(vendingMarker);
                entities.Add(colorMarker);

                InvokeRepeating(UpdateMapMarker, 2f, 2f);
            }

            void UpdateMapMarker()
            {
                var pos = Boat.transform.position;
                vendingMarker.transform.position = pos;
                colorMarker.transform.position = pos;

                vendingMarker.SendNetworkUpdate();
                colorMarker.SendNetworkUpdate();
                colorMarker.SendUpdate();
            }

            #endregion

            #region Zone

            private void CreateZone()
            {
                Zone = Zone.Create(Boat, Config.zoneRadius, Config.zoneDarkness, Vector3.up * 5);

                Zone.OnPlayerLeave += pl => DestroyGui(pl);
            }

            private readonly CuiElementContainer container = new CuiElementContainer();
            private readonly CuiElement panel = new CuiElement
            {
                Name = GUI_CONTAINER,
                Parent = "Hud",
                FadeOut = 0,
                DestroyUi = GUI_CONTAINER,
                Components =
                {
                    new CuiImageComponent { Color = "0 0 0 0" },
                    new CuiRectTransformComponent { AnchorMin = "0 0.88", AnchorMax = "1 1"}
                }
            };
            
            private void UpdateGui()
            {
                string key = State == EventState.RUNNING ? "timer_leave" : "timer_sink";
                int remaining = State == EventState.RUNNING ? timeRemaining - Config.destroyTime : timeRemaining;

                int minutes = Mathf.FloorToInt(remaining / 60f);
                int seconds = remaining % 60;

                foreach (var player in Zone.Players)
                {
                    container.Add(panel);
                    container.Add(new CuiElement
                    {
                        Parent = GUI_CONTAINER,
                        Components =
                        {
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                            new CuiTextComponent { Color = "1 1 1 1", FadeIn = 0f, Text = String.Format(GetMessage(key, player), minutes, seconds), FontSize = 24, Align = TextAnchor.LowerCenter, Font = "robotocondensed-bold.ttf" },
                            new CuiOutlineComponent { Distance = "1 1", Color = "0 0 0 1" }
                        }
                    });

                    CuiHelper.AddUi(player, container);

                    container.Clear();
                }
            }

            private void DestroyGui(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, GUI_CONTAINER);
            }

            #endregion
        }

        #endregion

        #region Zone

        private class Zone : MonoBehaviour
        {
            public static Zone Create(BaseEntity parent, float radius, int sphereCount, Vector3 localPosition = default(Vector3))
            {
                var go = new GameObject();
                go.layer = (int)Layer.Reserved1;

                var zone = go.AddComponent<Zone>();
                zone.Setup(localPosition, radius, sphereCount, parent);

                return zone;
            }

            public static Zone Create(Vector3 position, float radius, int sphereCount)
            {
                var go = new GameObject();
                go.layer = (int)Layer.Reserved1;

                var zone = go.AddComponent<Zone>();
                zone.Setup(position, radius, sphereCount);

                return zone;
            }

            private SphereCollider collider;
            private readonly List<BaseEntity> spheres = new List<BaseEntity>();

            public HashSet<BasePlayer> Players { get; private set; } = new HashSet<BasePlayer>();

            public event Action<BasePlayer> OnPlayerEnter;
            public event Action<BasePlayer> OnPlayerLeave;

            void OnTriggerEnter(Collider other)
            {
                var player = other.ToBaseEntity() as BasePlayer;
                if (player != null && !player.IsNpc)
                {
                    Players.Add(player);
                    OnPlayerEnter?.Invoke(player);
                }
            }

            void OnTriggerExit(Collider other)
            {
                var player = other.ToBaseEntity() as BasePlayer;
                if (player != null && !player.IsNpc)
                {
                    Players.Remove(player);
                    OnPlayerLeave?.Invoke(player);
                }
            }

            void Setup(Vector3 position, float radius, int sphereCount, BaseEntity parent = null)
            {
                if (parent != null)
                {
                    gameObject.transform.SetParent(parent.transform, false);
                    gameObject.transform.localPosition = position;
                }
                else
                {
                    gameObject.transform.position = position;
                }

                collider = gameObject.AddComponent<SphereCollider>();
                collider.isTrigger = true;
                collider.radius = radius;

                for (int i = 0; i < sphereCount; i++)
                {
                    var sphere = (SphereEntity)GameManager.server.CreateEntity("assets/prefabs/visualization/sphere.prefab", collider.transform.position);
                    sphere.currentRadius = radius * 2;
                    sphere.lerpSpeed = 0f;
                    sphere.enableSaving = false;
                    sphere.Spawn();

                    if (parent != null)
                    {
                        sphere.SetParent(parent, true);
                        sphere.transform.localPosition = position;

                        sphere.SendNetworkUpdate();
                    }
                    
                    spheres.Add(sphere);
                }
            }

            void OnDestroy()
            {
                foreach (var sphere in spheres)
                {
                    if (!sphere.IsDestroyed)
                    {
                        sphere.Kill();
                    }
                }

                Players.Clear();
            }

            public void Destroy()
            {
                Destroy(gameObject);
            }
        }

        #endregion

        #region NpcSpawn

        internal class NpcSpawnNpcBelt { public string ShortName; public int Amount; public ulong SkinID; public IEnumerable<string> Mods; public string Ammo; }

        internal class NpcSpawnNpcWear { public string ShortName; public ulong SkinID; }

        internal class NpcSpawnConfig
        {
            public string Name { get; set; }
            public IEnumerable<NpcSpawnNpcWear> WearItems { get; set; }
            public IEnumerable<NpcSpawnNpcBelt> BeltItems { get; set; }
            public string Kit { get; set; }
            public float Health { get; set; }
            public float RoamRange { get; set; }
            public float ChaseRange { get; set; }
            public float SenseRange { get; set; }
            public float ListenRange { get; set; }
            public float AttackRangeMultiplier { get; set; }
            public bool CheckVisionCone { get; set; }
            public float VisionCone { get; set; }
            public float DamageScale { get; set; }
            public float TurretDamageScale { get; set; }
            public float AimConeScale { get; set; }
            public bool DisableRadio { get; set; }
            public bool CanRunAwayWater { get; set; }
            public bool CanSleep { get; set; }
            public float Speed { get; set; }
            public int AreaMask { get; set; }
            public int AgentTypeID { get; set; }
            public string HomePosition { get; set; }
            public float MemoryDuration { get; set; }
            public HashSet<string> States { get; set; }
        }

        #endregion

        #region Lang

        private static string GetMessage(string key, BasePlayer player)
        {
            return Instance.lang.GetMessage(key, player);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["captain_killed"] = "The captain has been killed. Hurry up, the boat will start sinking in {0} seconds",
                ["ship_looted"] = "The pirate ship has been looted and will start sinking in {0} seconds",
                ["ship_destroyed"] = "The pirate ship has been destroyed and is sinking",

                ["timer_leave"] = "The ship will leave in {0}m {1}s",
                ["timer_sink"] = "The ship will sink in {0}m {1}s",

                ["ship_start"] = "Pirates are on their way to patrol the seas. They have been spotted near {0}",
                ["ship_start_toast"] = "Pirate ship inbound",
                ["ship_leave_5m"] = "Pirates will leave the seas in 5 minutes",
                ["ship_leave"] = "The Pirates have left the seas",

                ["event_reward"] = "You received RP x{0} for completing the event"
            }, this);
        }

        #endregion

        #region Profiles

        private void RegisterNpc(ScientistNPC npc, string profile, bool removeCorpse, string captainNoteText = null)
        {
            if (String.IsNullOrEmpty(profile))
            {
                return;
            }

            npcProfiles[npc.userID] = new ValueTuple<string, bool>(profile, removeCorpse);
            if (!String.IsNullOrEmpty(captainNoteText))
            {
                captainNoteTexts[npc.userID] = captainNoteText;
            }
        }

        private static LootManager.LootTable GetLootProfile(string profile, string captainNoteText = null)
        {
            if (Config.lootProfiles.TryGetValue(profile, out var lootTable))
            {
                if (!String.IsNullOrEmpty(captainNoteText))
                {
                    lootTable = lootTable.Copy();
                    lootTable.items.Insert(0, new LootManager.LootItem("note", 1, 1, 1f) { text = captainNoteText });
                }

                return lootTable;
            }

            if (!String.IsNullOrEmpty(profile))
            {
                LogError($"Loot profile '{profile}' not found. Check your config!");
            }

            return null;
        }

        private static NpcConfig GetNpcConfig(string profile)
        {
            if (Config.npcProfiles.TryGetValue(profile, out var npcConfig))
            {
                return npcConfig;
            }

            LogError($"NPC profile '{profile}' not found. Check your config!");
            return null;
        }

        private static Tuple<NpcConfig, Vector3> GetSpawnConfig(string profile, string spawnPoint)
        {
            NpcConfig npcConfig = GetNpcConfig(profile);
            if (npcConfig == null)
            {
                return null;
            }

            if (Instance.npcSpawnPoints.TryGetValue(spawnPoint, out var spawnLocation))
            {
                return new Tuple<NpcConfig, Vector3>(npcConfig, spawnLocation);
            }

            LogError($"NPC spawn point '{spawnPoint}' does not exist. Check your config!");
            return null;
        }

        #endregion

        #region Helpers

        public static void DrawSphere(BasePlayer player, Vector3 pos, float radius, Color color, float duration)
        {
            player.SendConsoleCommand("ddraw.sphere", duration, color, pos, radius);
        }

        private static void BroadcastNotify(string key, params string[] args)
        {
            if (Instance.Notify == null)
            {
                LogWarning("Failed to send notification. Notify is not installed");
                return;
            }

            foreach(var player in BasePlayer.activePlayerList)
            {
                string message = Instance.lang.GetMessage(key, player);
                Instance.Notify.Call("SendNotify", player, Config.notificationType, String.Format(message, args));
            }
        }

        private static void BroadcastLang(string key, params string[] args)
        {
            foreach(var player in BasePlayer.activePlayerList)
            {
                ChatMessageLang(player, key, args);
            }
        }

        private static void Broadcast(string message)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                Instance?.ChatMessage(player, message);
            }
        }

        private static void ChatMessageLang(BasePlayer player, string key, params object[] args)
        {
            var message = Instance.lang.GetMessage(key, player);
            player?.ChatMessage("<color=#00ffff>[Tugboat Event]</color> " + String.Format(message, args));
        }

        private static void ShowToastLang(string key, params object[] args)
        {
            foreach(var player in BasePlayer.activePlayerList)
            {
                string message = GetMessage(key, player);
                player.SendConsoleCommand("gametip.showtoast", 1, String.Format(message, args));
            }
        }

        #endregion
    }
}

namespace Oxide.Plugins.TugboatPiratesExt
{
    public static class Extensions
    {
        public static TriggerParent GetTriggerParent(this Tugboat tugboat)
        {
            var field = typeof(Tugboat).GetField("parentTrigger", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new FieldAccessException("Field Tugboat.parentTrigger not found");
            }

            return (TriggerParent)field.GetValue(tugboat);
        }
    }
}
namespace PluginComponents.TugboatPirates{using JetBrains.Annotations;using Oxide.Plugins;using System;[AttributeUsage(AttributeTargets.Field,AllowMultiple=false),MeansImplicitUse]public sealed class PermAttribute:Attribute{}[AttributeUsage(AttributeTargets.Method,AllowMultiple=false),MeansImplicitUse]public sealed class UniversalCommandAttribute:Attribute{public UniversalCommandAttribute(string name){Name=name;}public string Name{get;set;}public string Permission{get;set;}}[AttributeUsage(AttributeTargets.Method),MeansImplicitUse]public sealed class HookAttribute:Attribute{}[AttributeUsage(AttributeTargets.Method,Inherited=false)]public sealed class DebugAttribute:Attribute{}public class MinMaxInt{public int min;public int max;public MinMaxInt(){}public MinMaxInt(int value):this(value,value){}public MinMaxInt(int min,int max){this.min=min;this.max=max;}public int Random(){return UnityEngine.Random.Range(min,max+1);}}}namespace PluginComponents.TugboatPirates.Core{using Oxide.Core.Plugins;using Oxide.Core;using Oxide.Plugins;using Newtonsoft.Json;using System.IO;using UnityEngine;using System;using System.Diagnostics;using System.Collections.Generic;using System.Linq;using Facepunch.Extend;using System.Reflection;using PluginComponents.TugboatPirates;public abstract class BasePlugin<TPlugin,TConfig>:BasePlugin<TPlugin>where TConfig:class,new()where TPlugin:RustPlugin{protected new static TConfig Config{get;private set;}private string ConfigPath=>Path.Combine(Interface.Oxide.ConfigDirectory,$"{Name}.json");protected override void LoadConfig()=>ReadConfig();protected override void SaveConfig()=>WriteConfig();protected override void LoadDefaultConfig()=>Config=new TConfig();private void ReadConfig(){if(File.Exists(ConfigPath)){Config=JsonConvert.DeserializeObject<TConfig>(File.ReadAllText(ConfigPath));if(Config==null){LogError("[CONFIG] Your configuration file contains an error. Using default configuration values.");LoadDefaultConfig();}}else{LoadDefaultConfig();}WriteConfig();}private void WriteConfig(){var directoryName=Utility.GetDirectoryName(ConfigPath);if(directoryName!=null&&!Directory.Exists(directoryName)){Directory.CreateDirectory(directoryName);}if(Config!=null){string text=JsonConvert.SerializeObject(Config,Formatting.Indented);File.WriteAllText(ConfigPath,text);}else{LogError("[CONFIG] Saving failed - config is null");}}}public abstract class BasePlugin<TPlugin>:BasePlugin where TPlugin:RustPlugin{public new static TPlugin Instance{get;private set;}protected static string DataFolder=>Path.Combine(Interface.Oxide.DataDirectory,typeof(TPlugin).Name);protected override void Init(){base.Init();Instance=this as TPlugin;}protected override void Unload(){Instance=null;base.Unload();}}public abstract class BasePlugin:RustPlugin{public const int OSI_DELAY=5;public const bool CARBONARA=
#if CARBON
true;
#else
false;
#endif
public const bool DEBUG=
#if DEBUG
true;
#else
false;
#endif
public static BasePlayer DebugPlayer=>DEBUG?BasePlayer.activePlayerList.FirstOrDefault(x=>!x.IsNpc):null;public static string PluginName=>Instance?.Name??"NULL";public static BasePlugin Instance{get;private set;}protected virtual UnityEngine.Color ChatColor=>default;protected virtual string ChatPrefix=>ChatColor!=default?$"<color=#{ColorUtility.ToHtmlStringRGB(ChatColor)}>[{Title}]</color>":$"[{Title}]";[HookMethod("Init")]protected virtual void Init(){Instance=this;foreach(var field in GetType().GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)){if(field.IsLiteral&&!field.IsInitOnly&&field.FieldType==typeof(string)&&field.HasAttribute(typeof(PermAttribute))){if(field.GetValue(null)is string perm){LogDebug($"Auto-registered permission '{perm}'");permission.RegisterPermission(perm,this);}}}foreach(var method in GetType().GetMethods(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)){if(method.GetCustomAttributes(typeof(UniversalCommandAttribute),true).FirstOrDefault()is UniversalCommandAttribute attribute){var commandName=attribute.Name??method.Name.ToLower().Replace("cmd",string.Empty);if(attribute.Permission!=null){LogDebug($"Auto-registered command '{commandName}' with permission '{attribute.Permission??"<null>"}'");}else{LogDebug($"Auto-registered command '{commandName}'");}AddUniversalCommand(commandName,method.Name,attribute.Permission);}}}[HookMethod("Unload")]protected virtual void Unload(){Instance=null;}[HookMethod("OnServerInitialized")]protected virtual void OnServerInitialized(bool initial){if(!CARBONARA){OnServerInitialized();}timer.In(OSI_DELAY,OnServerInitializedDelayed);}
#if CARBON
[HookMethod("OnServerInitialized")]
#endif
protected virtual void OnServerInitialized(){}protected virtual void OnServerInitializedDelayed(){}public static void Log(string s){if(Instance!=null){Interface.Oxide.LogInfo($"[{Instance.Title}] {s}");}}[Conditional("DEBUG")]public static void LogDebug(string s){if(DEBUG&&Instance!=null){if(CARBONARA){LogWarning("[DEBUG] "+s);}else{Interface.Oxide.LogDebug($"[{Instance.Title}] {s}");}}}public static void LogWarning(string s){if(Instance!=null){Interface.Oxide.LogWarning($"[{Instance.Title}] {s}");}}public static void LogError(string s){if(Instance!=null){Interface.Oxide.LogError($"[{Instance.Title}] {s}");}}private Dictionary<string,CommandCallback>uiCallbacks;private string uiCommandBase;private void PrepareCommandHandler(){if(uiCallbacks==null){uiCallbacks=new();uiCommandBase=$"{Title.ToLower()}.uicmd";cmd.AddConsoleCommand(uiCommandBase,this,HandleCommand);}}private bool HandleCommand(ConsoleSystem.Arg arg){var cmd=arg.GetString(0);if(uiCallbacks.TryGetValue(cmd,out var callback)){var player=arg.Player();try{callback.ButtonCallback?.Invoke(player);callback.InputCallback?.Invoke(player,string.Join(' ',arg.Args?.Skip(1)??Enumerable.Empty<string>()));}catch(Exception ex){PrintError($"Failed to run UI command {cmd}: {ex}");}}return false;}public string CreateUiCommand(string guid,Action<BasePlayer>callback,bool singleUse){PrepareCommandHandler();uiCallbacks.Add(guid,new CommandCallback(callback,singleUse));return$"{uiCommandBase} {guid}";}public string CreateUiCommand(string guid,Action<BasePlayer,string>callback,bool singleUse){PrepareCommandHandler();uiCallbacks.Add(guid,new CommandCallback(callback,singleUse));return$"{uiCommandBase} {guid}";}private readonly struct CommandCallback{public readonly bool SingleUse;public readonly Action<BasePlayer>ButtonCallback;public readonly Action<BasePlayer,string>InputCallback;public CommandCallback(Action<BasePlayer>buttonCallback,bool singleUse){ButtonCallback=buttonCallback;InputCallback=null;SingleUse=singleUse;}public CommandCallback(Action<BasePlayer,string>inputCallback,bool singleUse){ButtonCallback=null;InputCallback=inputCallback;SingleUse=singleUse;}}public void ChatMessage(BasePlayer player,string message){if(player){player.SendConsoleCommand("chat.add",2,0,$"{ChatPrefix} {message}");}}}}namespace PluginComponents.TugboatPirates.Extensions.BaseNetworkable{using PluginComponents.TugboatPirates;using PluginComponents.TugboatPirates.Extensions;public static class BaseNetworkableEx{public static bool IsNullOrDestroyed(this global::BaseNetworkable baseNetworkable){return!baseNetworkable||baseNetworkable.IsDestroyed;}}}namespace PluginComponents.TugboatPirates.Loot{using Newtonsoft.Json;using System;using System.Collections.Generic;using System.Linq;using UnityEngine;using PluginComponents.TugboatPirates;public static class LootManager{public static void FillWithLoot(StorageContainer container,IEnumerable<LootItem>lootTable)=>FillWithLoot(container.inventory,lootTable);public static void FillWithLoot(ItemContainer container,IEnumerable<LootItem>lootTable){ClearContainer(container);int amt=0;foreach(var itm in lootTable){if(UnityEngine.Random.Range(0f,1f)<=itm.chance){var item=itm.CreateItem();if(item==null){continue;}if(!item.MoveToContainer(container)){item.Remove();}amt++;}if(amt>=container.capacity){break;}}}public static void FillWithLoot(StorageContainer container,LootTable lootTable)=>FillWithLoot(container.inventory,lootTable);public static void FillWithLoot(ItemContainer container,LootTable lootTable){if(!lootTable.Enabled){return;}if(lootTable.minItems<=0||lootTable.maxItems<=0){FillWithLoot(container,lootTable.items);return;}ClearContainer(container);const int max_retries=50;int itemAmount=0;var targetItemAmount=UnityEngine.Random.Range(lootTable.minItems,lootTable.maxItems+1);targetItemAmount=Mathf.Min(targetItemAmount,lootTable.items.Count);var included=new HashSet<string>();container.capacity=targetItemAmount;for(int i=0;(i<max_retries&&itemAmount<targetItemAmount);i++){foreach(var itm in lootTable.items){if(!included.Contains(itm.shortname)&&UnityEngine.Random.Range(0f,1f)<=itm.chance){var item=itm.CreateItem();if(item==null){continue;}if(!item.MoveToContainer(container)){item.Remove();}included.Add(itm.shortname);itemAmount++;}}}}private static void ClearContainer(ItemContainer container){container.Clear();ItemManager.DoRemoves();}public class LootTable{[JsonIgnore]public bool Enabled=>enabled&&items.Count>0;[JsonProperty("Enabled")]public bool enabled;[JsonProperty("Minimum items",DefaultValueHandling=DefaultValueHandling.Ignore)]public int minItems;[JsonProperty("Maximum items",DefaultValueHandling=DefaultValueHandling.Ignore)]public int maxItems;[JsonProperty("Item list",ObjectCreationHandling=ObjectCreationHandling.Replace)]public List<LootItem>items=new();public LootTable Copy(){return new LootTable{enabled=enabled,minItems=minItems,maxItems=maxItems,items=items.ToList(),};}}public class LootItem{[JsonProperty("Short name")]public string shortname;[JsonProperty("Min amount")]public int min;[JsonProperty("Max amount")]public int max;[JsonProperty("Chance (1 = 100%)")]public float chance;[JsonProperty("Skin id")]public ulong skin=0;[JsonProperty("Custom name")]public string customName=string.Empty;[JsonProperty("Text",DefaultValueHandling=DefaultValueHandling.Ignore)]public string text;[JsonIgnore]public ItemDefinition ItemDefinition=>ItemManager.FindItemDefinition(shortname);public LootItem(){shortname="scrap";min=5;max=10;chance=1f;skin=0;}public LootItem(string shortname,int min,int max,float chance){this.shortname=shortname;this.min=min;this.max=max;this.chance=chance;}public LootItem(string shortname,int min,int max,float chance,ulong skin){this.shortname=shortname;this.min=min;this.max=max;this.chance=chance;this.skin=skin;}public Item CreateItem(){if(ItemDefinition==null||ItemDefinition.itemid==-996920608){return null;}var itm=ItemManager.Create(ItemDefinition,UnityEngine.Random.Range(min,max+1),skin);itm?.OnVirginSpawn();if(customName!=null&&customName.Length>0){itm.name=customName;}if(text!=null&&text.Length>0){itm.text=text;}return itm;}public override int GetHashCode(){return HashCode.Combine(shortname,skin);}}}}namespace PluginComponents.TugboatPirates.Extensions.Lang{using PluginComponents.TugboatPirates.Core;using System.Collections.Generic;using PluginComponents.TugboatPirates;using PluginComponents.TugboatPirates.Extensions;public static class LangEx{public static string GetMessage(this Oxide.Core.Libraries.Lang lang,string key,BasePlayer player)=>GetMessage(lang,key,player.userID.Get());public static string GetMessage(this Oxide.Core.Libraries.Lang lang,string key,ulong playerId){return lang.GetMessage(key,BasePlugin.Instance,playerId.ToString());}public static string GetMessage(this Oxide.Core.Libraries.Lang lang,string key,BasePlayer player,params object[]args)=>GetMessage(lang,key,player.userID,args);public static string GetMessage(this Oxide.Core.Libraries.Lang lang,string key,ulong playerId,params object[]args){var msg=lang.GetMessage(key,BasePlugin.Instance,playerId.ToString());return string.Format(msg,args);}public static void SendMessage(this Oxide.Core.Libraries.Lang lang,string key,BasePlayer player){var msg=GetMessage(lang,key,player.userID);BasePlugin.Instance.ChatMessage(player,msg);}public static void SendMessage(this Oxide.Core.Libraries.Lang lang,string key,BasePlayer player,params object[]args){var msg=GetMessage(lang,key,player.userID,args);BasePlugin.Instance.ChatMessage(player,msg);}public static void BroadcastMessage(this Oxide.Core.Libraries.Lang lang,string key)=>BroadcastMessage(lang,key,BasePlayer.activePlayerList);public static void BroadcastMessage(this Oxide.Core.Libraries.Lang lang,string key,IEnumerable<BasePlayer>players){foreach(var player in players){var msg=GetMessage(lang,key,player.userID);BasePlugin.Instance.ChatMessage(player,msg);}}public static void BroadcastMessage(this Oxide.Core.Libraries.Lang lang,string key,params object[]args)=>BroadcastMessage(lang,key,BasePlayer.activePlayerList,args);public static void BroadcastMessage(this Oxide.Core.Libraries.Lang lang,string key,IEnumerable<BasePlayer>players,params object[]args){foreach(var player in players){var msg=GetMessage(lang,key,player.userID,args);BasePlugin.Instance.ChatMessage(player,msg);}}public static string GetLanguage(this Oxide.Core.Libraries.Lang lang,BasePlayer player)=>GetLanguage(lang,player.userID);public static string GetLanguage(this Oxide.Core.Libraries.Lang lang,ulong userId){return lang.GetLanguage(userId.ToString());}}}namespace PluginComponents.TugboatPirates.Tools.Entity{using Facepunch;using JetBrains.Annotations;using PluginComponents.TugboatPirates.Extensions.BaseNetworkable;using System;using System.Collections.Generic;using System.Reflection;using UnityEngine;using PluginComponents.TugboatPirates;using PluginComponents.TugboatPirates.Tools;public static class EntityTools{public static TCustom CreateCustomEntity<TEnt,TCustom>(string prefab,Vector3 position=default,Quaternion rotation=default)where TEnt:BaseEntity where TCustom:TEnt{var entity=CreateEntity<TEnt>(prefab,position,rotation,false,false);var customEntity=entity.gameObject.AddComponent<TCustom>();var fields=typeof(TEnt).GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);foreach(var field in fields){field.SetValue(customEntity,field.GetValue(entity));}UnityEngine.Object.DestroyImmediate(entity,true);customEntity.gameObject.AwakeFromInstantiate();return customEntity;}public static T CreateEntity<T>(string prefab,bool save=false)where T:BaseEntity=>CreateEntity<T>(prefab,default,Quaternion.identity,save,true);public static T CreateEntity<T>(string prefab,Vector3 position,bool save=false)where T:BaseEntity=>CreateEntity<T>(prefab,position,Quaternion.identity,save,true);public static T CreateEntity<T>(string prefab,Vector3 position,Vector3 rotation,bool save=false)where T:BaseEntity=>CreateEntity<T>(prefab,position,Quaternion.Euler(rotation),save,true);public static T CreateEntity<T>(string prefab,Vector3 position,Quaternion rotation,bool save=false)where T:BaseEntity=>CreateEntity<T>(prefab,position,rotation,save,true);private static T CreateEntity<T>(string prefab,Vector3 position,Quaternion rotation,bool save,bool active)where T:BaseEntity{var ent=GameManager.server.CreateEntity(prefab,position,rotation,active);if(ent is not T entity){UnityEngine.Object.Destroy(ent);throw new InvalidCastException($"Failed to create entity of type '{typeof(T).Name}' from '{prefab}'");}ent.enableSaving=save;return entity;}public static void KillSafe<T>([ItemCanBeNull]IEnumerable<T>entities)where T:BaseEntity{var list=Pool.Get<List<T>>();list.AddRange(entities);Kill(list);Pool.FreeUnmanaged(ref list);}public static void Kill<T>([ItemCanBeNull]IEnumerable<T>entities)where T:BaseEntity{foreach(var entity in entities){Kill(entity);}}public static void Kill([CanBeNull]BaseEntity entity){if(entity is not null&&!entity.IsDestroyed){entity.Kill();}}}}