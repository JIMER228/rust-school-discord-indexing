/*
                                   /\  /\  /\
     TTTTT  H   H  EEEE     K  K  |  \/  \/  |  NN   N   GGG
       T    H   H  E        K K   *----------*  N N  N  G
       T    HHHHH  EEE      KK     I  I  I  I   N  N N  G  GG
       T    H   H  E        K K    I  I  I  I   N   NN  G   G
       T    H   H  EEEE     K  K   I  I  I  I   N    N   GGG


This plugin (the software) is © Copyright the_kiiiing.

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

using \u0048\u0061\u0072\u006D\u006F\u006E\u0079Lib;
using JetBrains.Annotations;
using Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Plugins.HalloweenExtensions;
using PluginComponents.Halloween;
using PluginComponents.Halloween.Core;
using PluginComponents.Halloween.Extensions.BaseNetworkable;
using PluginComponents.Halloween.Extensions.Enumerable;
using PluginComponents.Halloween.External.NpcSpawn;
using PluginComponents.Halloween.Loot;
using PluginComponents.Halloween.LoottableApi;
using PluginComponents.Halloween.MapMarker;
using PluginComponents.Halloween.SpawnPoint;
using PluginComponents.Halloween.Tools.Entity;
using ProtoBuf;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using UnityEngine;
using VLB;
using Net = Network.Net;
using Pool = Facepunch.Pool;
using Random = UnityEngine.Random;
using Time = UnityEngine.Time;

/*
 *  CHANGELOG
 * 
 *  V 1.0.0
 *  - added check for empty player list in EventManager.MaybeRunSoundEffect and MaybeRunLightningEffect
 *  - removed unnecessary usings
 *  
 *  V 1.0.1
 *  - fixed record permission not registered
 *  - added map markers for graveyards
 *  
 *  V 1.0.2
 *  - fixed recording not working
 *  
 *  V 1.0.3
 *  - destroy decorations on plugin unload
 *  - replace custom zombies with default scarecrows until ai is fixed
 *  
 *  V 1.0.4
 *  - junk pile decorations no longer cause lag or crash the server when loading/unloading the plugin
 *  - fix null reference exception when graveyard despawns
 *  - added skin support for grave yard loot
 *  
 *  V 1.0.5
 *  - add support for carbon
 *  - fix error with OverrideCorpseName
 *  - networking update
 *  
 *  V 1.0.6
 *  - patch networking for May 04 update
 *  
 *  V 1.0.7
 *  - fix config value enableJumpscares ignored
 *  
 *  V 1.1.0
 *  - ignore NPCs created with NpcSpawn
 *  - add limit for junk pile decorations
 *  - add health config for zombies
 *  - remove junk pile decorations when unloading
 *  
 *  V 1.1.1
 *  - actually ignore NPCs created with NpcSpawn
 *  - improve graveyard performance
 *  - graveyard spawn time can now be configured
 *
 *  V 1.2.0
 *  - major rewrite
 *  - switch to BasePlugin
 *  - remove MarkerApi requirement
 *  - fix graveyard spawn issues
 *  - change zombies to NpcSpawn npcs
 *  - loottable integration
 * 
 *  V 1.2.1
 *  - fix NRE in MaybeRunLightningEffect
 *
 *  V 1.2.2
 *  - removed some legacy code
 *  - add BuildingManager debugging
 *
 *  V 1.2.3
 *  - add loot config for graveyard zombies
 *  - fix BuildingManager server exception
 *  - fix junk pile decorations not properly destroyed on unload
 *  - fix infinite loop in LootManager.FillWithLoot that could lead to sever crash
 *  - misc refactoring
 *
 * V 1.2.4
 * - fix bug in LootManager
 *
 * V 1.2.5
 * - fix graveyard spawn points
 * - add config option for grave yard fire
 *
 * V 1.3.0
 * - rework sound system
 * - rework jumpscare system
 * - add random jumpscares
 * - add config option for chat commands
 * - fix graveyards spawning too close to each other
 * - fix graveyards spawning near safe zones
 *
 * V 1.3.1
 * - fix small bug in jumpscare command
 * - fix bug with fog not appearing
 *
 * V 1.3.2
 * - update for Loot Api
 *
 * V 1.3.3
 * - fix for rust update
 * 
 */

namespace Oxide.Plugins
{
    [Info(nameof(Halloween), "The_Kiiiing", "1.3.3")]
    [Description("Happy Halloween")]
    public class Halloween : BasePlugin<Halloween, Halloween.Configuration>
    {
        #region Fields

        private const ulong DECORATION_SKIN = 385UL;
        
        private const string PRESET_BOX = "gy_box";
        private const string PRESET_ZOMBIE = "gy_zombie";
        
        [Perm]
        private const string PERM_EDIT = "halloween.edit";
        [Perm]
        private const string PERM_RECORD = "halloween.record";
        [Perm]
        private const string PERM_SCARE = "halloween.scare";

        private const float SPAWN_AREA_RADIUS = 10f;
        private const int SPAWN_POINT_RESOLUTION = 10;

        private int junkPileDecorationCount;

        private ulong configEditor;
        private GraveyardConfig graveYardConfigDraft;
        private JunkPileConfig junkPileConfigDraft;

        private static List<byte[]> record;
        private static BasePlayer recordPlayer;

        private Dictionary<char, JunkPileConfig> junkPileConfigs;
        
        private EventManager _eventManager;

        [PluginReference, UsedImplicitly]
        private readonly Plugin NpcSpawn;

        private readonly Dictionary<ulong, NpcConfig> npcProfiles = new();

        private readonly Dictionary<NetworkableId, List<BaseEntity>> junkPileDecorations = new();

        private static readonly List<BaseJumpscarePlayer> _jumpscareNpcs = new();
        
        #endregion

        #region Hooks

        #region General

        protected override void Init()
        {
            base.Init();
            
            SoundManager.LoadBuiltinSounds();

            LoadJunkPileConfigs();
            
            AddCovalenceCommand(Config.CmdGyName, nameof(CmdGy));
            AddCovalenceCommand(Config.CmdJpName, nameof(CmdJp));
            AddCovalenceCommand(Config.CmdRecName, nameof(CmdRec));
            AddCovalenceCommand(Config.CmdScareName, nameof(CmdScare));

            \u0048\u0061\u0072\u006D\u006F\u006E\u0079Instance.Patch(AccessTools.Method(typeof(JunkPile), "SinkAndDestroy"), AccessTools.Method(typeof(JunkPile_SinkAndDestroy), nameof(JunkPile_SinkAndDestroy.Prefix)));
        }

        protected override void OnServerInitializedDelayed()
        {
            base.OnServerInitializedDelayed();
            
            AddAllDecorations();

            _eventManager = new GameObject().AddComponent<EventManager>();
            _eventManager.Init();
            
            UpdateNpcClothing();
            
            LoottableApi.ClearPresets(Instance);
            LoottableApi.CreatePreset(Instance, PRESET_BOX, "Graveyard Crate", "crate_normal");
            LoottableApi.CreatePreset(Instance, PRESET_ZOMBIE, "Graveyard Zombie", "npc_scarecrow");
        }

        protected override void Unload()
        {
            EntityTools.KillSafe(_jumpscareNpcs);

            RemoveAllDecorations();

            if (_eventManager)
            {
                UnityEngine.Object.Destroy(_eventManager.gameObject);
            }
            
            SoundManager.Unload();
            
            base.Unload();
        }

        //[ConsoleCommand("deco")]
        //void CmdDecoration(ConsoleSystem.Arg arg)
        //{
        //    arg.ReplyWith($"Current decoration count: {junkPileDecorationCount}");
        //}

        #endregion

        #region Junkpile / Graveyard editing

        [Hook] object OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERM_EDIT) || configEditor != player.userID || info.HitEntity == null)
            {
                return null;
            }
            
            if (junkPileConfigDraft != null)
            {
                junkPileConfigDraft.AddEntity(info.HitEntity);
                player.ChatMessage($"Added {info.HitEntity.ShortPrefabName} to the current junkpile");
            }
            else if (graveYardConfigDraft != null)
            {
                graveYardConfigDraft.AddObject(info.HitEntity);
                if (info.HitEntity.PrefabName == "assets/prefabs/misc/xmas/snowman/snowman.deployed.prefab")
                {
                    player.ChatMessage($"Added a zombie spawn point to the current graveyard");
                }
                else
                {
                    player.ChatMessage($"Added {info.HitEntity.ShortPrefabName} to the current graveyard");
                }
            }
            else
            {
                player.ChatMessage("You have to create a new graveyard config first or start editing a junk pile");
            }

            return false;
        }

        #endregion

        #region Junk pile decoration

        [Hook] void OnEntitySpawned(JunkPile junkPile)
        {
            if (Config.enableJunkPileDecoration && junkPile.IsValid() && junkPileDecorationCount < Config.maxJunkPileDecorationsTotal && !junkPile.ShortPrefabName.Contains("water"))
            {
                // Make sure there are no decorations left
                RemoveDecorations(junkPile);
                
                AddDecorations(junkPile);
            }
        }

        [Hook] void OnEntityKill(JunkPile junkPile)
        {
            RemoveDecorations(junkPile);
        }
        
        #if DEBUG
        
        [ConsoleCommand("sink"), UsedImplicitly]
        void CmdSink(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (Physics.Raycast(player.eyes.HeadRay(), out var hit, 3f))
            {
                if (hit.GetEntity() is JunkPile junkPile)
                {
                    junkPile.SinkAndDestroy();
                    arg.ReplyWith("Sink da junk pile");
                }
            }
        }
        
        #endif

        #endregion

        #region NPC Clothing

        [Hook] void OnEntitySpawned(NPCPlayer npc)
        {
            NextFrame(() => UpdateNpcClothing(npc));
        }

        #endregion

        #region Recording

        [Hook] object OnPlayerVoice(BasePlayer player, byte[] data)
        {
            if (recordPlayer != null && player == recordPlayer)
            {
                record.Add(data);
            }

            return null;
        }

        #endregion

        #region Jumpscare

        [Hook] void OnLootEntity(BasePlayer player, LootContainer container)
        {
            if (Config.lootJumpscares.Enabled && (Random.Range(0f, 100f) < Config.lootJumpscares.ChancePercent || DEBUG))
            {
                var pd = PlayerData.Of(player);
                if (DateTime.UtcNow.Subtract(pd.lastCrateJumpscare).TotalMinutes > Config.lootJumpscares.DelayMinutes)
                {
                    pd.lastCrateJumpscare = DateTime.UtcNow;
                    
                    var sound = SoundManager.GetSound(Config.lootJumpscares.Sounds.GetRandomOrDefault());
                    if (sound == null)
                    {
                        LogError($"Failed to get sound for loot jumpscare - no sounds configured");
                        return;
                    }
                    
                    BaseJumpscarePlayer.Create(player, Config.lootJumpscares.NpcName, sound, false, container.transform.position, 1f);
                }
            }
        }

        #endregion
        
        #region Graveyard
        
        [Hook] void OnCorpsePopulate(ScientistNPC npc, NPCPlayerCorpse corpse)
        {
            if (npc == null || corpse == null)
            {
                return;
            }

            if (npcProfiles.Remove(npc.userID, out var profile) && profile.lootTable.enabled)
            {
                Instance.NextFrame(() =>
                {
                    if (!corpse.IsValid())
                    {
                        LogError($"Failed to populate NPC corpse ({npc.displayName}) - invalid corpse");
                        return;
                    }

                    LootManager.FillWithLoot(corpse.containers[0], profile.lootTable);

                    // Drop bag
                    if (profile.removeCorpse && !corpse.IsDestroyed)
                    {
                        corpse.Kill();
                    }
                });
            }
        }

        private void OnGraveyardDestroyed(Graveyard graveyard)
        {
            _eventManager?.OnGraveyardDestroyed(graveyard);
        }
        
        #endregion
        
        #endregion

        #region Commands

        // Jumpscare
        void CmdScare(IPlayer iPlayer, string command, string[] args)
        {
            if (!permission.UserHasPermission(iPlayer.Id, PERM_SCARE))
            {
                iPlayer.Reply("No permission");
                return;
            }
            
            BasePlayer target = null;
            if (args.Length == 1)
            {
                target = BasePlayer.Find(args[0]);
            }
            else
            {
                target = iPlayer.Object as BasePlayer;
            }
            
            if (target == null)
            {
                iPlayer.Reply("Target player not found");
                return;
            }

            RunJumpscare(target);
            iPlayer.Reply($"Jumpscare npc spawned for player {target.displayName}");
        }

        // Grave yard
        private void CmdGy(IPlayer iPlayer, string command, string[] args)
        {
            if (iPlayer.Object is not BasePlayer player)
            {
                return;
            }
            
            if (!permission.UserHasPermission(player.UserIDString, PERM_EDIT))
            {
                player.ChatMessage("You don't have permission to do that");
                return;
            }

            if (args.Length == 1 && args[0] == "new")
            {
                configEditor = player.userID;
                graveYardConfigDraft = new GraveyardConfig(player.transform.position);
                player.ChatMessage($"Created graveyard at {player.transform.position}. Hit objects with a hammer to add them to your graveyard");
            }
            else if (args.Length == 2 && args[0] == "save")
            {
                if (graveYardConfigDraft == null)
                {
                    player.ChatMessage("Nothing to save, create a graveyard first");
                    return;
                }

                ConfigManager.SaveGraveyardConfig(graveYardConfigDraft, args[1]);
                player.ChatMessage($"Saved config as {args[1]}.json");

                graveYardConfigDraft = null;
                configEditor = 0UL;
            }
            else
            {
                player.ChatMessage("Invalid syntax! Usage:\n" +
                    $"{Config.CmdGyName} new - create a new graveyard\n" +
                    $"{Config.CmdGyName} save <name> - save the current graveyard");
            }
        }

        // Junk pile
        private void CmdJp(IPlayer iPlayer, string command, string[] args)
        {
            if (iPlayer.Object is not BasePlayer player)
            {
                return;
            }
            
            var error = $"Invalid syntax. Usage:\n{Config.CmdJpName} <type> to edit the config of a certain type\n{Config.CmdJpName} save to save the current config";

            if (!permission.UserHasPermission(player.UserIDString, PERM_EDIT))
            {
                player.ChatMessage("You don't have permission to do that");
            }
            else if (args.Length != 1)
            {
                player.ChatMessage(error);
            }
            else if (args[0] == "save")
            {
                if (junkPileConfigDraft == null)
                {
                    player.ChatMessage("Nothing to save, edit a junkpile first");
                    return;
                }
                string name = $"pile_{junkPileConfigDraft.type}";
                ConfigManager.SaveJunkPileConfig(junkPileConfigDraft, name);
                junkPileConfigDraft.junkPile?.Kill();
                player.ChatMessage($"Saved junk pile config as {name}.json");

                junkPileConfigDraft = null;
                configEditor = 0UL;
            }
            else if ("abcdefghij".Contains(args[0]) && args[0].Length == 1)
            {
                configEditor = player.userID;

                char type = args[0].First();
                var jp = EntityTools.CreateEntity<JunkPile>($"assets/prefabs/misc/junkpile/junkpile_{type}.prefab", player.transform.position);
                jp.Spawn();

                junkPileConfigDraft = new JunkPileConfig(jp);
            }
            else
            {
                player.ChatMessage(error);
            }
        }

        // Recording
        private void CmdRec(IPlayer iPlayer, string command, string[] args)
        {
            if (iPlayer.Object is not BasePlayer player)
            {
                return;
            }
            
            if (!permission.UserHasPermission(player.UserIDString, PERM_RECORD))
            {
                player.ChatMessage("You don't have permission to do that");
                return;
            }

            if (args.Length == 0)
            {
                // Start recording
                if (recordPlayer == null)
                {
                    player.ChatMessage("Start recording");
                    record = new List<byte[]>();
                    recordPlayer = player;
                }
                // Stop recording
                else
                {
                    player.ChatMessage($"Stop recording, use '{Config.CmdRecName} test' to preview your recording. Use '{Config.CmdRecName} save <name>' to it.");
                    recordPlayer = null;
                }
            }
            else if (args.Length == 1 && args[0] == "test")
            {
                if (record.IsNullOrEmpty())
                {
                    player.ChatMessage("Failed to play sound: recoding is empty");
                    return;
                }

                var sound = ProtoSound.Create(record);
                BaseJumpscarePlayer.Create(player, "Jumpscare Test", sound, false, player.transform.position + Vector3.forward);
            }
            else if (args.Length == 2 && args[0] == "save")
            {
                if (record.IsNullOrEmpty())
                {
                    player.ChatMessage("Nothing to save, start a recording first");
                    return;
                }

                SoundManager.SaveSound(args[1], record);
                player.ChatMessage($"Saved recording with name '{args[1]}'");
            }
            else if (DEBUG && args.Length == 2 && args[0] == "savexml")
            {
                if (record.IsNullOrEmpty())
                {
                    player.ChatMessage("Nothing to save, start a recording first");
                    return;
                }
                
                SoundManager.SaveSoundLegacy(args[1], record);
                player.ChatMessage($"Saved recording with name '{args[1]}'");
            }
            else
            {
                player.ChatMessage($"Invalid syntax! Usage:\n{Config.CmdRecName} - Start/stop recording\n{Config.CmdRecName} test - Play recording\n{Config.CmdRecName} save <name> - Save recording");
            }
        }

        #endregion

        #region NPC Clothing

        private void UpdateNpcClothing()
        {
            foreach (var npc in BaseNetworkable.serverEntities.OfType<NPCPlayer>())
            {
                UpdateNpcClothing(npc);
            }
        }

        private void UpdateNpcClothing(NPCPlayer npc)
        {
            if (npc == null || npc.IsDestroyed || npc.IsDead() || npc.inventory?.containerWear == null || Config.ignoreNpcSkins.Contains(npc.skinID)) return;

            string name = npc.ShortPrefabName.Replace(".prefab", string.Empty);

            if (Config.npcClothing.TryGetValue(name, out var newClothes))
            {
                npc.inventory.containerWear.Clear();
                ItemManager.DoRemoves();

                foreach (var cloth in newClothes)
                {
                    Item itm = ItemManager.CreateByName(cloth.shortname, 1, cloth.skin);
                    if (itm == null) continue;
                    if (!itm.MoveToContainer(npc.inventory.containerWear))
                    {
                        itm.Remove();
                        LogDebug($"Failed to move item {cloth.shortname} to {npc.ShortPrefabName}");
                    }
                } 
            }
        }

        #endregion

        #region Junkpile Decoration

        private void AddDecorations(JunkPile junkPile)
        {
            var key = junkPile.ShortPrefabName.Last();
            if (junkPileConfigs.TryGetValue(key, out var cfg))
            {
                var entities = Pool.Get<List<BaseEntity>>();
                cfg.Apply(junkPile, entities);
                junkPileDecorationCount += entities.Count;
                junkPileDecorations.Add(junkPile.net.ID, entities);
            }
        }
        
        private void RemoveDecorations(JunkPile junkPile)
        {
            if (junkPile.IsValid() && junkPileDecorations.Remove(junkPile.net.ID, out var entities))
            {
                junkPileDecorationCount -= entities.Count;
                EntityTools.Kill(entities);
                Pool.FreeUnmanaged(ref entities);
            }
        }

        class JunkPile_SinkAndDestroy
        {
            public static void Prefix(JunkPile __instance)
            {
                Instance?.RemoveDecorations(__instance);
            }
        }

        private void RemoveAllDecorations()
        {
            EntityTools.KillSafe(junkPileDecorations.Values.SelectMany(x => x));
            junkPileDecorations.Clear();
            Log($"Cleared {junkPileDecorationCount} junk pile decoration entities");
        }
        
        private void AddAllDecorations()
        {
            if (Config.enableJunkPileDecoration)
            {
                ServerMgr.Instance.StartCoroutine(AddAllDecorationsCoro());
            }
        }

        private IEnumerator AddAllDecorationsCoro()
        {
            yield return CoroutineEx.waitForFixedUpdate;

            var sw = new Stopwatch();
            sw.Start();

            var junkPiles = BaseNetworkable.serverEntities.OfType<JunkPile>().ToArray();
            foreach (var jp in junkPiles)
            {
                OnEntitySpawned(jp);
                yield return CoroutineEx.waitForEndOfFrame;
            }

            sw.Stop();
            Log($"Decorated {junkPiles.Length} junk piles in {sw.Elapsed.TotalSeconds:N2} seconds with {junkPileDecorationCount} total entities");
        }

        internal class JunkPileConfig
        {
            [JsonIgnore]
            public bool Default => decorations.IsNullOrEmpty();

            [JsonIgnore]
            public JunkPile junkPile;

            public bool water;
            public char type;

            public List<JunkPileDecoration> decorations;

            public JunkPileConfig() { }

            [JsonConstructor]
            public JunkPileConfig(bool water, char type, List<JunkPileDecoration> decorations)
            {
                this.water = water;
                this.type = type;
                this.decorations = decorations;
            }

            public JunkPileConfig(JunkPile junkPile)
            {
                this.junkPile = junkPile;

                type = junkPile.ShortPrefabName.Last();
                decorations = new List<JunkPileDecoration>();
                water = false;
            }

            public void AddEntity(BaseEntity ent)
            {
                decorations.Add(new JunkPileDecoration
                {
                    prefabName = ent.PrefabName,
                    rotation = ent.transform.eulerAngles - junkPile.transform.eulerAngles,
                    position = ent.transform.position - junkPile.transform.position
                });

                ent.Kill();
            }

            public void Apply(JunkPile junkPile, List<BaseEntity> entities)
            {
                foreach(var decoration in decorations.GetRandom(Config.maxJunkPileDecorations))
                {
                    AddDecoration(decoration);
                }
                
                void AddDecoration(JunkPileDecoration decoration)
                {
                    var worldPos = junkPile.transform.position + (junkPile.transform.rotation * decoration.position);
                    var localRot = junkPile.transform.rotation * Quaternion.Euler(decoration.rotation);
                    var ent = EntityTools.CreateEntity<BaseEntity>(decoration.prefabName, worldPos, localRot);

                    UnityEngine.Object.DestroyImmediate(ent.GetComponent<DestroyOnGroundMissing>());
                    UnityEngine.Object.DestroyImmediate(ent.GetComponent<GroundWatch>());

                    ent.skinID = DECORATION_SKIN;
                    // No set parent, causes server exception probably
                    //ent.SetParent(junkPile, worldPositionStays: true);
                    ent.Spawn();
                    ent.SetFlag(BaseEntity.Flags.On, true);
                    entities.Add(ent);
                }
            }

            public class JunkPileDecoration
            {
                public Vector3 position;
                public Vector3 rotation;

                public string prefabName;
            }
        }

        private void LoadJunkPileConfigs()
        {
            junkPileConfigs = new Dictionary<char, JunkPileConfig>();

            foreach(char type in "abcdefghij")
            {
                string name = $"pile_{type}";
                var cfg = ConfigManager.LoadJunkPileConfig(name);
                if (cfg == null || cfg.Default)
                {
                    if (!Config.enableJunkPileDecoration)
                    {
                        continue;
                    }
                    cfg = ConfigManager.LoadDefaultJunkPileConfig(type);
                }

                junkPileConfigs.Add(type, cfg);
            }
        }

        #endregion

        #region Event Manager

        internal class EventManager : FacepunchBehaviour
        {
            private const float TICK_TIME = 10f;
            
            private static DateTime GameTime => Instance.covalence.Server.Time;
            
            private static readonly ConsoleSystem.Option cmdOptions = new() { IsServer = true, IsUnrestricted = true };
            
            private readonly List<Graveyard> _graveyards = new();

            private bool _wasNight;

            private Coroutine _fogCoro;

            private SpawnPointManager _spawnPointManager;

            public void Init()
            {
                LogDebug($"Night time {Config.graveyardSpawnTime.start} - {Config.graveyardSpawnTime.end}");

                _spawnPointManager = new SpawnPointManager(SPAWN_POINT_RESOLUTION);
                _spawnPointManager.Configure(x =>
                {
                    x.CustomValidator = IsNotNearOtherGraveyard;
                    x.MinDistanceToBuildings = 40f;
                    x.MinDistanceToSafeZones = 100f;
                    x.ValidHeight = new Range<float>
                    {
                        Min = ConVar.Env.oceanlevel // No graveyard below water level
                    };
                
                    x.BlockTopology(SpawnPointManager.TP_DEFAULT);
                    
                    if (!Config.allowGraveyardOnRoad)
                    {
                        x.BlockTopology(SpawnPointManager.TP_ROAD);
                    }
                    
                });
                
                StartCoroutine(_spawnPointManager.CacheSpawnPoints());
                
                InvokeRepeating(DoTick, 0, TICK_TIME);
            }

            void OnDestroy()
            {
                DespawnGraveyards(true);
                _graveyards.Clear();
            }

            public void OnGraveyardDestroyed(Graveyard graveyard)
            {
                _graveyards.Remove(graveyard);
            }

            private void DoTick()
            {
                if (IsNightTime())
                {
                    if (Config.enableLightningEffects)
                    {
                        MaybeRunLightningEffect();
                    }

                    if (Config.enableSoundEffects)
                    {
                        MaybeRunSoundEffect();
                    }

                    if (Config.enableFoggyNights && !_wasNight)
                    {
                        FadeFog(1f);
                    }

                    if (Config.enableGraveYards && _graveyards.Count < Config.graveyardPopulation)
                    {
                        SpawnGraveyard();
                    }

                    if (Config.randomJumpscares.Enabled)
                    {
                        MaybeRunJumpscare();
                    }

                    _wasNight = true;
                }
                else
                {
                    if (_wasNight)
                    {
                        if (Config.enableFoggyNights)
                        {
                            FadeFog(0f);
                        }

                        DespawnGraveyards();
                    }

                    _wasNight = false;
                }
            }
            private void MaybeRunJumpscare()
            {
                if (BasePlayer.activePlayerList.Count < 1 || (GameTime.Hour < 21 && GameTime.Hour > 4))
                {
                    return;
                }

                foreach (var victim in BasePlayer.activePlayerList.Where(x => !x.IsNpc))
                {
                    if (Random.Range(0f, 100f) < Config.randomJumpscares.ChancePercent)
                    {
                        var victimData = PlayerData.Of(victim);
                        if (DateTime.UtcNow.Subtract(victimData.lastRandomJumpscare).TotalMinutes > Config.randomJumpscares.DelayMinutes
                            && !(victim.InSafeZone() || victim.IsNearBase() || victim.IsHeadUnderwater()))
                        {
                            victimData.lastRandomJumpscare = DateTime.UtcNow;
                            RunJumpscare(victim);
                        }
                    }
                }
            }

            public void SpawnGraveyard()
            {
                if (!_spawnPointManager.Initialized)
                {
                    LogWarning("Failed to spawn graveyard - spawn points still initializing");
                    return;
                }

                if (!_spawnPointManager.TryGetRandomSpawnPoint(out var point))
                {
                    LogWarning("Failed to spawn graveyard - attempt limit exceeded, consider decreasing the graveyard population in the config");
                }
                
                var gc = Config.RandomGraveyardConfig;
                if (!gc.IsValid)
                {
                    LogError("Failed to spawn graveyard - invalid configuration file");
                }
                else
                {
                    var gy = Graveyard.Spawn(point, gc);
                    _graveyards.Add(gy);
                    Log($"Graveyard spawned at {point}");
                }
            }

            private void DespawnGraveyards(bool force = false)
            {
                var graveyards = Pool.Get<List<Graveyard>>();
                graveyards.AddRange(_graveyards);
                
                foreach (var graveyard in graveyards)
                {
                    if (force || !Config.enableGraveYardFire)
                    {
                        graveyard.Destroy();
                    }
                    else
                    {
                        var players = Pool.Get<List<BasePlayer>>();
                        Vis.Entities(graveyard.Position, 100f, players);
                        players.RemoveAll(x => x.IsNpc);

                        if (players.Count > 0)
                        {
                            graveyard.Despawn();
                        }
                        else
                        {
                            graveyard.Destroy();
                        }

                        Pool.FreeUnmanaged(ref players);
                    }
                }
                
                Pool.FreeUnmanaged(ref graveyards);
            }

            #region Effects / Fog

            private void MaybeRunLightningEffect()
            {
                if (BasePlayer.activePlayerList.Count > 0 && Random.Range(0, 3) == 0)
                {
                    var player = BasePlayer.activePlayerList[Random.Range(0, BasePlayer.activePlayerList.Count)];
                    RunEffect("assets/content/effects/weather/pfx_lightning_strong.prefab", player.transform.position);
                }
            }
            
            private void MaybeRunSoundEffect()
            {
                if (BasePlayer.activePlayerList.Count < 1 || (GameTime.Hour < 21 && GameTime.Hour > 4))
                {
                    return;
                }

                for (int i = 0; i < 5; i++)
                {
                    var victim = BasePlayer.activePlayerList.GetRandom();
                    if (victim.InSafeZone() || victim.IsNearBase() || victim.IsHeadUnderwater())
                    {
                        continue;
                    }

                    var victimData = PlayerData.Of(victim);
                    if (DateTime.UtcNow.Subtract(victimData.lastSoundEffect).TotalMinutes < Config.minSoundEffectDelay)
                    {
                        continue;
                    }

                    Invoke(() =>
                    {
                        var pos = victim.RandomPositionAround(10f, 2f);
                        RunEffect(Config.RandomSoundEffect, pos);
                        victimData.lastSoundEffect = DateTime.UtcNow;
                    }, 3f);
                    break;
                }
            }

            
            private void FadeFog(float target, float time = 30f, float stepSize = 0.02f)
            {
                if (_fogCoro != null)
                {
                    Log("Stop fog");
                    ServerMgr.Instance.StopCoroutine(_fogCoro);
                }

                Log($"Fade {(target > 0.5f ? "in" : "out")} fog");
                _fogCoro = ServerMgr.Instance.StartCoroutine(FadeFogCoro(target, time, stepSize));
            }

            private static IEnumerator FadeFogCoro(float target, float time, float stepSize)
            {
                var value = ConVar.Weather.fog;
                if (target < value)
                {
                    stepSize *= -1;
                }

                int steps = Mathf.CeilToInt(Mathf.Abs((target - value) / stepSize));
                float stepTime = time / steps;

                bool b = false;
                for(int step = 0; step < steps; step++)
                {
                    value += stepSize;
                    if (value > 1f || value < 0f)
                    {
                        value = Mathf.Clamp(value, 0f, 1f);
                        b = true;
                    }

                    ConVar.Weather.fog = value;

                    if (b)
                    {
                        break;
                    }
                    
                    yield return CoroutineEx.waitForSeconds(stepTime);
                }
            }
            
            #endregion
            
            private bool IsNotNearOtherGraveyard(Vector3 pos)
            {
                foreach (var gy in _graveyards)
                {
                    if (Vector3.Distance(pos, gy.Position) - SPAWN_AREA_RADIUS * 2 < Config.minGraveyardDist)
                    {
                        return false;
                    }
                }

                return true;
            }
            
            private static bool IsNightTime()
            {
                if (Config.graveyardSpawnTime.start > Config.graveyardSpawnTime.end)
                {
                    return GameTime.Hour > Config.graveyardSpawnTime.start || GameTime.Hour < Config.graveyardSpawnTime.end;
                }
                else
                {
                    return GameTime.Hour > Config.graveyardSpawnTime.start && GameTime.Hour < Config.graveyardSpawnTime.end;
                }
            }
        }

        #endregion

        #region Player Data

        internal class PlayerData
        {
            private static readonly Dictionary<ulong, PlayerData> data = new Dictionary<ulong, PlayerData>();

            public ulong Id { get; }

            public DateTime lastSoundEffect;
            public DateTime lastCrateJumpscare;
            public DateTime lastRandomJumpscare;

            private PlayerData(ulong id)
            {
                Id = id;
                lastSoundEffect = DateTime.MinValue;
                lastCrateJumpscare = DateTime.MinValue;
                lastRandomJumpscare = DateTime.MinValue;
            }

            public static PlayerData Of(BasePlayer player) => Of(player.userID);

            public static PlayerData Of(ulong id)
            {
                if (data.TryGetValue(id, out var pd))
                {
                    return pd;
                }

                pd = new PlayerData(id);
                data.Add(id, pd);
                return pd;
            }
        }

        #endregion
        
        #region Graveyard

        internal class Graveyard : FacepunchBehaviour
        {
            public Vector3 Position => transform.position;
            public bool IsDespawning { get; private set; }
            
            public readonly List<BaseEntity> Objects = new();
            public readonly List<ScientistNPC> Zombies = new();

            private CustomMapMarker _marker;
            
            private void OnDestroy()
            {
                _marker?.Destroy();
                
                foreach(var obj in Objects)
                {
                    if (!obj.IsNullOrDestroyed())
                    {
                        obj.Kill(IsDespawning ? BaseNetworkable.DestroyMode.Gib : BaseNetworkable.DestroyMode.None);
                    }
                }
                
                foreach(var zombie in Zombies)
                {
                    Instance?.npcProfiles.Remove(zombie.userID);
                    
                    if (IsDespawning && zombie.IsAlive())
                    {
                        zombie.Die();
                    }
                    else if (!IsDespawning && !zombie.IsDestroyed)
                    {
                        zombie.Kill();
                    }
                }
                
                Instance?.OnGraveyardDestroyed(this);
            }

            public void Destroy() => Destroy(this);

            private void Start()
            {
                if (Config.enableGraveyardMapMarker)
                {
                    CreateMarker();
                }
            }

            private void CreateMarker()
            {
                if (!ColorUtility.TryParseHtmlString(Config.mapMarkerSettings.color, out var color))
                {
                    LogError($"Invalid map marker color '{Config.mapMarkerSettings.color}'. Color must be in hex format");
                }
                
                _marker = CustomMapMarker.Create(Position, Config.mapMarkerSettings.radius, color);
                _marker.SetText(Config.mapMarkerSettings.name);
            }

            public void Despawn()
            {
                LogDebug("Despawn graveyard");
                
                _marker?.Destroy();
                IsDespawning = true;

                foreach(var obj in Objects)
                {
                    if (obj.IsNullOrDestroyed())
                    {
                        continue;
                    }

                    if (obj is StorageContainer container)
                    {
                        container.inventory?.Clear();
                    }

                    var fb = EntityTools.CreateEntity<FireBall>("assets/bundled/prefabs/fireball.prefab", obj.transform.position);
                    fb.damagePerSecond = 5f;
                    fb.lifeTimeMin = Config.graveyardDespawnTime;
                    fb.lifeTimeMax = Config.graveyardDespawnTime + 10f;
                    fb.Spawn();
                }

                Invoke(Destroy, Config.graveyardDespawnTime);
            }

            public static Graveyard Spawn(Vector3 position, GraveyardConfig config)
            {
                var gy = new GameObject().AddComponent<Graveyard>();
                gy.transform.position = position;

                foreach (var obj in config.objects)
                {
                    var ent = SpawnObject(obj, position);
                    if (ent is StorageContainer container && !LoottableApi.AssignPreset(Instance, PRESET_BOX, container))
                    {
                        LootManager.FillWithLoot(container, Config.graveyardLootTable);
                    }
                    gy.Objects.Add(ent);
                }

                foreach (var spawnPoint in config.zombieSpawns)
                {
                    var pos = position + spawnPoint;
                    pos.y = TerrainMeta.HeightMap.GetHeight(pos) + 0.5f;
                    var zombie = SpawnZombie(pos);
                    if (zombie != null)
                    {
                        gy.Zombies.Add(zombie);
                    }
                }

                return gy;

                static BaseEntity SpawnObject(GraveyardConfig.GraveyardObject obj, Vector3 pos)
                {
                    pos += obj.Offset;
                    pos.y = TerrainMeta.HeightMap.GetHeight(pos);

                    var norm = TerrainMeta.HeightMap.GetNormal(pos);
                    float x = Mathf.Atan(norm.x) * Mathf.Rad2Deg;
                    float z = Mathf.Atan(norm.z) * Mathf.Rad2Deg;

                    var rot = Quaternion.Euler(x, 90f, z);
                    if (obj.Rotation != default)
                    {
                        rot *= Quaternion.Euler(Vector3.up * (obj.Rotation.y + 90f));
                    }

                    var ent = EntityTools.CreateEntity<BaseEntity>(obj.PrefabName, pos, rot);
                    ent.Spawn();
                    
                    //LogDebug($"Graveyard object {obj.PrefabName} spawned at {pos}");
                    
                    if (ent is FogMachine fm)
                    {
                        fm.fuelPerSec = 0f;
                        fm.SetFlag(FogMachine.Flag_HasJuice, true);
                        fm.SetFlag(FogMachine.FogFieldOn, true);
                        fm.SetFlag(FogMachine.Emitting, true);
                    }
                    else if (ent is BaseOven || ent is Candle)
                    {
                        ent.SetFlag(BaseEntity.Flags.On, true);
                    }

                    return ent;
                }

                static ScientistNPC SpawnZombie(Vector3 position)
                {
                    if (Instance.NpcSpawn == null)
                    {
                        LogError("Failed to spawn npc - NpcSpawn is not loaded");
                        return null;
                    }

                    var config = Config.graveyardNpcConfig;

                    var npcConfig = new NpcSpawnConfig
                    {
                        Name = config.name,
                        WearItems = config.clothing.Select(x => new NpcSpawnNpcWear { ShortName = x.shortName, SkinID = x.skinId }),
                        BeltItems = config.belt.Select(x => new NpcSpawnNpcBelt { ShortName = x.shortName, Amount = x.amount, SkinID = x.skinId, Ammo = String.Empty, Mods = Array.Empty<string>() }),
                        Kit = config.kit,
                        Health = config.health,
                        RoamRange = config.roamRange,
                        ChaseRange = config.chaseRange,
                        SenseRange = config.senseRange,
                        ListenRange = config.senseRange / 2f,
                        AttackRangeMultiplier = config.attackRangeMultiplier,
                        VisionCone = config.visionCone,
                        DamageScale = config.damageScale,
                        TurretDamageScale = 1f,
                        AimConeScale = 1f,
                        DisableRadio = true,
                        CanRunAwayWater = true,
                        CanSleep = false,
                        Speed = 6f,
                        AreaMask = 1,
                        AgentTypeID = -1372625422,
                        HomePosition = position.ToString(),
                        MemoryDuration = config.memoryDuration,
                        States = new HashSet<string> { NpcSpawnStates.ROAM, NpcSpawnStates.CHASE, NpcSpawnStates.COMBAT }
                    };

                    var scientist = Instance.NpcSpawn?.Call("SpawnNpc", position, JObject.FromObject(npcConfig)) as ScientistNPC;
                    if (scientist == null)
                    {
                        LogError("Failed to spawn npc - scientist is null");
                        return null;
                    }
                    
                    LoottableApi.AssignPreset(Instance, PRESET_ZOMBIE, scientist);

                    if (config.lootTable.enabled)
                    {
                        Instance?.npcProfiles.Add(scientist.userID, config);
                    }

                    return scientist;
                }
            }
        }

        public class GraveyardConfig
        {
            [JsonIgnore]
            public bool IsValid => (objects?.Count ?? 0) > 0;
            [JsonIgnore]
            private readonly Vector3 center;
            
            public List<GraveyardObject> objects;
            public List<Vector3> zombieSpawns;

            [JsonConstructor]
            public GraveyardConfig() { }

            public GraveyardConfig(Vector3 worldPos)
            {
                center = worldPos;
                objects = new List<GraveyardObject>();
                zombieSpawns = new List<Vector3>();
            }

            public void AddObject(BaseEntity entity)
            {
                var offset = center - entity.transform.position;

                if (entity.ShortPrefabName == "snowman.deployed")
                {
                    AddZombie(offset);
                    entity.Kill();
                    return;
                }

                var o = new GraveyardObject(entity.PrefabName, offset, entity.transform.eulerAngles);
                objects.Add(o);
                entity.Kill();
            }

            public void AddZombie(Vector3 offset)
            {
                zombieSpawns.Add(offset);
            }

            public record GraveyardObject(
                [JsonProperty("prefabName")] string PrefabName, 
                [JsonProperty("offset")] Vector3 Offset,
                [JsonProperty("rotation")] Vector3 Rotation
            );
        }

        #endregion
        
        #region Jumpscare v2

        private static void RunJumpscare(BasePlayer target)
        {
            var sound = SoundManager.GetSound(Config.randomJumpscares.Sounds.GetRandomOrDefault());
            if (sound == null)
            {
                LogError("Failed to get sound for random jumpscare - no sounds configured");
                return;
            }
            
            BaseJumpscarePlayer.Create(target, Config.randomJumpscares.NpcName, sound, true);
        }
        
        class BaseJumpscarePlayer : BasePlayer
        {
            public float Speed { get; set; } = 7f;
            public float DistanceToTarget { get; set; } = 2f;
            public BasePlayer TargetPlayer { get; set; }
            public bool NeedsAttention { get; set; }
            
            [CanBeNull] 
            public ISound Sound { get; set; }

            private bool isPlayingSound;
            
            public override void ServerInit()
            {
                userID = (ulong)Random.Range(0, 10000000);
                UserIDString = userID.ToString();
                //displayName = UserIDString;
                bots.Add(this);
                
                base.ServerInit();
                
                _jumpscareNpcs.Add(this);
                
                RemovePlayerRigidbody();
                DisablePlayerCollider();
                
                SetupClothing();
                
                if (NeedsAttention)
                {
                    InvokeRandomized(RandomEffect, 1f, 5f, 2f);
                    InvokeRandomized(FaceCheck, 2f, 2f, 1f);
                }
                else
                {
                    PlaySound();
                }
                
                Invoke(KillIfNotPlaying, 60f);
            }

            public override void DestroyShared()
            {
                base.DestroyShared();

                _jumpscareNpcs.Remove(this);
            }

            public override void OnDied(HitInfo info)
            {
                base.OnDied(info);
                
                Effect.server.Run("assets/prefabs/clothes/halloween.scarecrow/effects/soul_release_effect.prefab", transform.position, Vector3.forward, broadcast: true);
            }

            public override BaseCorpse CreateCorpse(PlayerFlags flagsOnDeath, Vector3 posOnDeath, Quaternion rotOnDeath, List<TriggerBase> triggersOnDeath, bool forceServerSide = false) => null;
            public override bool EligibleForWounding(HitInfo info) => false;

            public override float StartMaxHealth() => StartHealth();
            public override float StartHealth() => 1f;
            
            #region Movement
            
            void FixedUpdate()
            {
                if (TargetPlayer == null)
                {
                    return;
                }
                
                OverrideViewAngles(Quaternion.LookRotation(TargetPlayer.transform.position - transform.position).eulerAngles);
                
                var targetPos = TargetPlayer.transform.position;
                if (!IsAtTarget(targetPos))
                {
                    var forward = (targetPos - transform.position).normalized;
                    forward *= (Speed * Time.fixedDeltaTime);
               
                    transform.position += forward;
                    transform.hasChanged = true;
                }
                
                SendNetworkUpdate();
                
                bool IsAtTarget(Vector3 pos)
                {
                    return Mathf.Abs(pos.x - transform.position.x) < DistanceToTarget && Mathf.Abs(pos.y - transform.position.y) < DistanceToTarget && Mathf.Abs(pos.z - transform.position.z) < DistanceToTarget;
                }
            }
            
            #endregion

            private void SetupClothing()
            {
                if (NeedsAttention)
                {
                    inventory.containerWear.AddItem(ItemManager.FindItemDefinition("burlap.headwrap"), 1, 3339379263);
                    inventory.containerWear.AddItem(ItemManager.FindItemDefinition("attire.hide.poncho"), 1, 3339380266);
                    inventory.containerWear.AddItem(ItemManager.FindItemDefinition("gloweyes"), 1);
                    inventory.containerWear.AddItem(ItemManager.FindItemDefinition("hoodie"), 1, 883710255);
                    inventory.containerWear.AddItem(ItemManager.FindItemDefinition("pants"), 1, 883709785); 
                    inventory.containerWear.AddItem(ItemManager.FindItemDefinition("tactical.gloves"), 1);
                    inventory.containerWear.AddItem(ItemManager.FindItemDefinition("shoes.boots"), 1, 883709405);
                }
                else
                {
                    inventory.containerWear.AddItem(ItemManager.FindItemDefinition("ghostsheet"), 1);
                    inventory.containerWear.AddItem(ItemManager.FindItemDefinition("gloweyes"), 1);
                    inventory.containerWear.AddItem(ItemManager.FindItemDefinition("pants"), 1, 883709785);
                    inventory.containerWear.AddItem(ItemManager.FindItemDefinition("tactical.gloves"), 1);
                    inventory.containerWear.AddItem(ItemManager.FindItemDefinition("shoes.boots"), 1, 883709405);
                }
            }
            
            private void FaceCheck()
            {
                if (TargetPlayer == null || TargetPlayer.IsDead())
                {
                    DieInstantly();
                    return;
                }
                
                var a = Mathf.Abs(TargetPlayer.viewAngles.y - viewAngles.y - 180);
                if (a > 180)
                {
                    a = Mathf.Abs(360 - a);
                }
                
                if (a < 45)
                {
                    CancelInvoke(RandomEffect);
                    CancelInvoke(FaceCheck);
                    PlaySound();
                }
            }

            private void RandomEffect()
            {
                var effect = Config.randomJumpscares.Effects.GetRandomOrDefault();
                if (effect != null)
                {
                    Effect.server.Run(effect, transform.position, broadcast: true);
                }
            }

            private void KillIfNotPlaying()
            {
                if (!isPlayingSound)
                {
                    DieInstantly();
                }
            }

            #region Sound
            
            private void PlaySound()
            {
                StartCoroutine(SoundCoro());
            }

            private IEnumerator SoundCoro()
            {
                yield return CoroutineEx.waitForEndOfFrame;
                
                if (Sound != null && Sound.IsValid)
                {
                    isPlayingSound = true;
                    
                    var soundQueue = Sound.GetChunks();
                    var timeTaken = 0f;
                    var totalTime = soundQueue.Count * 0.13f + 0.2f;

                    while (soundQueue.TryDequeue(out var nextChunk))
                    {
                        BroadcastSoundData(net.ID, nextChunk);
                        timeTaken += 0.07f;
                        yield return CoroutineEx.waitForSeconds(0.07f);
                    }

                    if (timeTaken < totalTime)
                    {
                        yield return new WaitForSeconds(totalTime - timeTaken);
                    }

                    isPlayingSound = false;
                }
                else
                {
                    LogError($"{nameof(BaseJumpscarePlayer)}: Sound is null");
                }

                yield return CoroutineEx.waitForSeconds(2);
                
                KillIfNotPlaying();
                
                void BroadcastSoundData(NetworkableId netId, byte[] data)
                {
                    using var netWrite = Net.sv.StartWrite();
                    netWrite.PacketID(Message.Type.VoiceData);
                    netWrite.EntityID(netId);
                    netWrite.BytesWithSize(data);
                    netWrite.Send(new SendInfo(GetConnectionsWithin(transform.position, 100f)));
                }
            }
            
            #endregion
            
            public static void Create(BasePlayer target, string npcName, [CanBeNull] ISound sound, bool needsAttention = true, Vector3 spawnPos = default, float distanceToTarget = 2f)
            {
                if (spawnPos == default)
                {
                    spawnPos = target.transform.position + target.eyes.BodyForward() * -2f;
                    spawnPos.y = target.transform.position.y + 0.2f;
                }
                
                var ent = EntityTools.CreateCustomEntity<BasePlayer, BaseJumpscarePlayer>("assets/prefabs/player/player.prefab", spawnPos);
                ent.NeedsAttention = needsAttention;
                ent.Sound = sound;
                ent.TargetPlayer = target;
                ent.DistanceToTarget = distanceToTarget;
                ent.displayName = npcName;
                ent.Spawn();
            }
        }
        
        #endregion

        #region Helpers

        private static void RunEffect(string effect, BaseEntity entity, Vector3? localPos = null)
        {
            if (entity == null)
            {
                return;
            }

            Effect.server.Run(effect, entity, 0, localPos ?? Vector3.zero, Vector3.back, null, true);
        }

        private static void RunEffect(string effect, Vector3 position)
        {
            Effect.server.Run(effect, position, broadcast: true);
        }

        #endregion

        #region SoundManager

        private static class SoundManager
        {
            #region Built-In sounds

            private const string LAUGH = """<?xml version="1.0" encoding="utf-16"?><LegacySound xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"><chunks><base64Binary>E6g7PQEAEAELwF0G9QEqAAAAaAvrUmpyoBrrTSufV5OeeMC1pD09SdlG9qIoslUuKTStbzCufwa6kZQXKgABAGgJlIU6bW41erqnFD3JJnVE81Rm1cBa8Il2iKDsW+zf3eskMBA2f/lMvCoAAgBoCZSF2fVc9B1yKW4X57rvCJgRMqIWxHdG/Y5fmQHS8SG0KfN+lY2PRGcqAAMAaAmUhS0nPp8aR1aTri5vGvJUhND6T/++qLOmRD0z+gbbftmIcKzqae0eOgAEAGiABFpSd0N0cpcWvmhTv9+8l1wyFwYUlhGzCrIWgpYqmuCd15iAe+5KSQJBnnbE89SpkpQ+SgwNKHVQAAUAaImRYi8gqqFzfS5yUDhJWJAqd/z+meJ/hbfbww0rapBpjzvhOnA+sJUKTMPke0mBwWSBtxZTRb1Ue4JFNldetp/a5WKZbDqOVcJngZKLxnRNAAYAaImLlW3fiE8SOFScXGSZf7z6zI0Nf5z1hwuWVnUeAz63+/wJpqYYs4wgPXcq2EW1gu5+hzdVGWZjS76BN53KO4BYGEBmpZikPNc6YipWAAcAaIlSCi2rveXukVcJkFz+IA98rgKiVJ1k0CQhsR6bOxfFmDUeeZ4ySPR1SvUiMc4M5BPKvI7JdWAIVD6Zhl+YI2GKnx5m1f7gjpSM6/jkq1NT9z6P1LaKw7y7</base64Binary><base64Binary>E6g7PQEAEAELwF0GXwBbAAgAaJ10hzPJGXvOfSUJRYIbNqVFTTUU5oEaC3Q/LMwb+oikEEaCf7BBHavgLPNDdXupLeHMUnHCT8YTDUeCkvliCkS46shfgBsOHARk6SQAg7UQL1wLcI0m/NdFCAvAXQb+AFsACQBonGR4tsuv61xfVcXAe2QQsH4PPckqftM2DP5zemLlv1FooZF0oq1v7CYLfEjNYio73Iv2C/CujrAlzsTCxFXPTiNP37+7kW2AXgzjzZ7LpMu/ruM26iSFG+q2UQAKAGiJlnqCAAz5sVZ5KLE0Q3ayAFxDscO40THVSeOuVa9bz8eZw7yn8P5c7uLj2DOpNSdKnRAGgF7l8EzTn2WPZlMXIrzBvkHkweZNB6cASB4NrEYACwBoiTB1MEmsZoRfVf/GIfIH/XM30TrErbZa68WdnKEkYzShFlqMUBdJMvwXmEnvF9nud0U8zzvRUKA+Su7EM0d0s5fZXZzBC8BdBp8ATQAMAGiJJSM3czJ+svdULhZXHgDxmvrOWwOA94//FOal/jXObjZAt/pi2H/ftPjdC0SX8Qz6CTcoHf4eW6A9jH+I9wcvaI+bWSVjwz/hbZH2SgANAGiIyDGwsPNRQ8tFwpk3xiuHMWTJeT8o4q9GiH6enQgFFIbPjustyR4oAve2PI4xMMZrdppXY0ZrOs2tmnnK9MMtciR7m8OK1IPwgGojLQ==</base64Binary><base64Binary>E6g7PQEAEAELwF0GFgFaAA4AaIjXyaRhV9HePAdfhgF0fcd9gIl2wM5Um4iHx8W6iTr3R5NY/8jYjEayurWCqLyLxoO+q99RKZ/Be7xQJojSVnOlKiYt9ZmG2Lf8e8zo61kdU5iecsx44im1WgAPAGiMKLTvjJaCXZTeTDbhzayxERx7WuGYYHNgB01bP3fOiDrn4cCnwtSN+PAlA2MlcTngpIpx/yjHGL47qlz/ymXC8/BDC4Slib7C6KRhkW+H3tKG1lbQQu0Fa1YAEABojhH7N/6ESonjKjP/3E2pDV8IXnC3zCS+fonUT78vUsqD5wg+34MlsHKQQf2dgwEdVPEcDknhnB6Roop2sIxfOooKukd+gEwBgbeRL+1rowgf8vjLFgvAXQYOAV0AEQBooNdnK1pncvw1eebfIZVuXq1fsV4rCiBIbrEKVZvjcKaeshRUHGDRD0ylSvMurjvN9NodgKwsJ6yoJc+go9q1TljQNMy3S1QTHbxWCh+zRSygOyAVMRTtwrfg4VRUABIAaIsxHXRuqD5QWsNWE0mbWkUxkg68qafwm03XimeksM4Qv3thQEhS1LyBo6lbCnhEDMEdQOu4PQw/baj7RqjC4kjhYGUqkD1eCX5WxPzpsqrd446EUQATAGiJhgqmnV3leXFa4uFmlsSFoVGkFm1k2lAYPdsMYEqL3fnYJJWQNQ5v3z2O9jZZioi3gDGC2LLuJRUZjkKIkce/oUZE4MsIx91tBnMQ0xllAk+R/rs=</base64Binary><base64Binary>E6g7PQEAEAELwF0G9wBGABQAaC8qmbfjxbLPFMYPjgXhJRjjyHeGhKRDFhmFkBNyvLbtTT1yJawOAslhvDiLQz6LQ3ztu7vyfMocjNa2Gi6khc6aV1Nl2EoAFQBoLbriDXuDTzCDINpafzupZaghSo7Jg+T0/qWXjJVHPIoejsmqUShVjkykDehViwYE2bzugUtRTe35gE5z2akOX/9ewLIDru8MQFsAFgBoiVY+aacOLa9BOMv+6n2NJhyqwrsyNJbx3k98b8y44LuA+/05X75EF6Bylw3HMt+9+TgMUPKevHjSQhs7DCptf6LypdYhXI2yIlKqck6FF7jIviOp9rcR29nXC8BdBsoAXQAXAGii7wmdlclU4RATDMbn11no8u1EOQlPGbit2rHM9g2XoGqbKMsyumbQrQ4DVLjHzK7N+YNNOmuR35CkbDjRFDe1GxTnjtjgZZRxagI1bB/YH5vjLeLnsVyWhhebCWUAGABopD16oR//4rfZQ90Ghq5Yq3EO544FUng6ca7AZpuQLlktOfsazpA3UAfRHXtX/AETgDkPgiuXFE8TIYNyAerMxCGXdBmZiRtM5aiSrglRxaptEoGgCNggGdR5uRl4n8aIiET+ldCs/9c=</base64Binary><base64Binary>E6g7PQEAEAELwF0GXgBaABkAaKXgJU4ILJSdpVL8Lo71Exz7XXiNGfiTLGdu60qNpENp3qeCSwdQsErAn1odbYYjps0fDQCxQYXHoeT5JT7uhQ/ysxAxrZtErVq+rbiVdzljpT7wXv9Cfd4QC8BdBkABVQAaAGi947iaolW/ILwK5q0+AphGazm/u1E929BKbC1IgjemBh+k7Dvv8R5QhCnY67Hxy03dDtRirCSY5TKHl4CPVy024xzSl2KyHjKTetv+u++WAZN2Dx5OABsAaIn+ioHqurybDWY0qY/YEpME2m4tsJmyFU2p2DPSY6Hb69A472R10Yn91LeUDBryNDQyWQsTkkUgj0HDL6b/SGE+V8nbd75+Wj512llISAAcAGgzJ7g1qTKtPwRAQdX8ILdYgHJqztueun7mDASvp9dD8aMF+dq+8luBwREXF3pj2oIsQSsrU6uSiTMo7Evzxy0ZF51Az+ySbkUAHQBoMof6FmbmNI06TfGFssIrhrkkZgFsQeBGzPwgmiwf9lyrUydOdUgc8aQPrQv0HZXVmiE/nX/zknroS+9WlICrEXWdralBG1Uu</base64Binary><base64Binary>E6g7PQEAEAELwF0G9wBDAB4AaC8HO/WzGDTXcW5dnLpiMWeu/Nr3bzI21Jt95dVilZHNe0mJRgoJmClUrLog0QdWjytPZjmC+sLAQ19f2UuKf+w+oE0AHwBoLXXxdOKUE2h0RbqJG1MS1qlVlwkqF5LHxoA8Ei63EjICD1KtOdblC9TwxpDEFgxbx8Ni6P7E6QIEjZ/P6NfqDJg+uP3k4SJgtnKGHlsAIABoicF2edkf/RoHqr1PQNXP4KPd5xQdQJA3atjOBmF5hoK8akgLhMcjLiRkG+9t93+9ODw6HD9JUy2dNqOGmQ/xtlS22Ga3WNBgvv+dzEZvVqLxBqAQMXdQB6vQC8BdBmUAYQAhAGiiecNJzHuvW2mA8byOrIZK6mllRbqDYb3WSJy+I9yLP2LooJLQPtSjvuPWMvAAloZK5neMH9Dih0+RP64npODgugAEyT/jQD/R4HqRYaMkGe+XO2qGOJu4arXCM8HVdksLwF0GCQFdACIAaKJoMEhuy2l7b0fqXVdBXN7YvszFCoLKyNU8Vqn1r/kY6y0+VpULijoLovOwg9/o1FaGvc9NUOT/K3EbDsFpmpu4BQcfgN2Gu01AZjDqV/8GV3FER6tjDloOrITeVgAjAGifhrZ23HbJBY+wJkYoq1ejTlrPcSEL9QGFdLiL5ukGceUeBrjal/P/TqDL1ahz54hLf6q1SwWKeDTDpi4YbX4GguTy/RbJW390UKsrFkbw7vR0eeXoSgAkAGgyzJEOzv+0Pu251NozUNnX6bQLQirtWFNNOaPF++ge0nHI12nIFY4ioEnv36sLFhzE9pPsxkbGKuNNdu898qvpn/Vy44ddxptq/VEtLg==</base64Binary><base64Binary>E6g7PQEAEAELwF0GkABEACUAaC9ee181OjBGCtERjec3s6JbrkC8yL0h6MIJ1BjzCPV7vQrTcZ0wC/UFw33kKyr1cUBX+BLHJ4B1F/IsNxn9Xyjnw+5EACYAaC9f0WkopQ2dXNEE+8tXXHFF0e9WohgA4cATTjEk+YIVTGatjO2/FO7/NPSMHg+PMPj/rLEuiUASjeZgfmCUETqYSE8LwF0GCgFJACcAaC9d/PIq05Vwoz29KTGLEWluuBxjHKNTa2PPx6oLG5JZq9cRF6JSEJIG8BN5siA9fGQ1PuV865Xj4ek6UYi8okf4e/AUE65wglkAKABoiwsNewC4jTYweayf2SpI5gBzeGmG5haHKuQFDAWsjvCL6lnYKvubRn6y7kyw8FIc309p/tIV7bXA7o1hca0ivxU8MKgQsamK8KujXac9LQ5veqYpIa0Ya1wAKQBojTR/sYzo3U3TB3FeUkpdgNo+MgUs2zNcW32VHM3zNN4LXJL8xTezWw9lgn3ZfBIGik5xESrFbhuFh+eq0RWwavT3AOq0xXKGq4HQhZLHGvGgrDyC2o09O0dJh1nw5HY=</base64Binary><base64Binary>E6g7PQEAEAELwF0GvQBbACoAaI2n9qt4ZE05kZ2Iq8e6kJk6KqrHP47jaT3yrm00Vra8Fw4hAUkuLvNq07oVgnk5qTsRNL431cU64xP4i49CwrB07wv5HYKSia9JgWQjXzbLE0jMNiZE30diMVoAKwBopOfiQlXhP9wVrsuZCfY1wxKmBpDWsC6S9bwkmd/DakX61F5nelCv8/V1zoutl3E+fvr2FK7K5k+rDpndwlFBZi9EDlbx5RDeuXRk6UAMmGH6pwIkCm1rMpoLwF0GCgFeACwAaKOc40XkU10GYJjmN0lcnF9tcY6aNKa8j3adNTi9XPPTrT6q8Dgkiuvz5HipMD8FjtkcUCMtzI/8q25bY/zyRwlWR3RYByziy3BsCNoxhlQSrEhoIMBysaXfm3HBhlUALQBogmwQu/LZ9CDOWAniNbIVJwiQfP/use7lr51GfcAinliRGsJyrkSaR9kjVehgVKj6rD0s02zpY+zrAOzJKDFGCJ2CQSwpN17g/V5Ji/d7P66o8FY0SwAuAGiCYRe/iABP3JENd2GIA9bak+O31jKVvzpM+zVtuQ513uVDoS3ldbCGh+b3E+u5vJlwtMCPQgrO0OhBfq/3Rlz3JqZOew4SQtiL9AvAXQbwAEsALwBoL17SAlDuoKivSzduFwojUKnFenaiCgex6raszs8KOeVqx1ZeegElqK+LmOwHDTx2KyLEuC76jEzqjS/5pwqbX+kb5mir+wGXRK9FADAAaC9f0ra4QnG//X5UpkoPR+QEy/tXjZX7n5V6/FMhuYuMBeT4Fru/DzZabtB7oQKHnENDxD86kAzHb4EgBMeEOq2FFb+cVAAxAGiJP6pFzX32VggpD8P9+7h50SA4DdYNWLAEJMYflQ2eojdS5EH3VGPMnfH/jGDX8AT912179oZ9nNx9ifsLj/SnBF2upJ4amMufAoFGkFjg3hgIWv76Xqg=</base64Binary><base64Binary>E6g7PQEAEAELwF0GYABcADIAaJynYWn4c7KmyvcQkG0s8HSH6wbYJfI2fERj2XeF1O+bmzHykmMmVQshYwd/KUJ80VSsngvnAdHyh492V5mIeQ8hfS+4JDlK6d5CGwNozrMY02tXSl71pddFViALwF0GeAFiADMAaKFxzaVDSZUur4kd0igv4xmZhF9cfTxJdhV8P6/echIzPwG3PvBcxBQFIrPGMevVZJP1uM+aDQ3vjx/1oc/d5/L78Ode7Sh3rCOWo9zn9xc75NXShUk4tMjx8Yh2y6WHIPhhADQAaKKJYVj4sUfgcESjnRdf3UlKtempfOMRj5VuJf0hI3Rn1uhupDgl/GYi+fvQr/FQZp/95M8UDGNpcCFhDXF8w1D/9ScNfmDetlDOg228lNb6SfZ9PT+QHAb8NJkUDIHtHFoANQBooh/MQetLE2Dr/uc/Ksy4GpNqjQmSoS/Ggabai9t7RjPnjpZBxUFo8QlyELyJoi/NO7Xw4aOXxFalZDXgX6nmOPer7jB57cdcBOXoKIgk4AYIYWgvySzgfDZLADYAaIvCmWb35Ux1qd+mjIj6eyRzuEe5+/JsJtwwb6TfWV6f2WeLOaB3eQ3rvyhPsUXQJG9MHZTwZhQezEtVHxlQqbQGNNYXb+9jkla0D6ohrA==</base64Binary><base64Binary>E6g7PQEAEAELwF0GSABEADcAaDURUW+ooHKxKLay3MtXa7PWgonfsBBW8TnSpEXIef9fpowpbDwssRERXX2K/d42X93h7fnDwuVqcUX9R9PbRQ3ludsLwF0G/ABAADgAaDRobBicpI0RFiXfM9uk0bPMbfNZ/VIy7h1UyIWNyYHkFIW8ReQy28cv/UWIFYbxhSWu5VvDOOa/UxT6EdJ9NlYAOQBoiT/Je9fiw6MsOP0k2cgMmUR7mXuaMWU56PUW2xatRS4N8wICdftgMbMHHdj28xwdeImPtPfUqUpV9M5P+RvSm5wQs+YAJCSp8+e/douDECE9FThADVoAOgBoiw2aa4gV2fxsofZ9rocTP1+SGGj0dzI1AuOgQFlbTJb2eoyR41yRONsbVlmNFN2C1qYZFBzyC8f17fu6xrBLkkO6Wk5GFI/Rk1DiOQGm2Epd+Mg6VQh9lRgLwF0GJwFdADsAaKFG2vaTyvEnWowHD+LeYA5H13HaNojvp8+zKKg/nqT1fht52Kc8oKzg1LLfTOOj4YEalUg8/cqfJ6gwsI6RQ+OC1jK5egxCSjqF82/CyZUrV5XTkO7D+CbslsVQYQA8AGihsooOFjMlVwxJDBLCrmVjf2rlsiUqRc4KMpqqQfB0v7Zh+fX6H4PZ/c+WQEhAnuvBCwB+gRlb0m0k7d/tZjk7u6oOq1RxJMC1Vs/wRqjcqR7kEPQedzW4FHvMdtqEfsldAD0AaKPNVH9eZ7zQ5CYDbYsga5t4PMr8yT6a/TZDrKGQXWs9/bfNYwIca6ct85wVId3HwjGDX8BtsgRWFZs3+a4R9Jke1H+HL9AQeldFSGhTnvPDGRNuvXr9uGRUNzGyJgglIw==</base64Binary><base64Binary>E6g7PQEAEAELwF0GswBXAD4AaKOekBe4kv2vDyNYlMcV/v1eubKZ9DxA4dinWzvGPZzODVeW8OQAh6xXn8Z8rMgGPSdtBciuFPU9Tb2UwD6/1pek3tSjelFObZ1jMBU+cpB7anJytrv7VAA/AGiiRAUdn1KzOTql22WQC+rlMApv/dK8va6sv15ZVvoCSs5WteJU++fRipAJofE66GgBuMx/wmw/W4sbjZyctiBICALyEoirUpLu5f9M6PHKjx8iNgvAXQbLADsAQABoNvhaETSfu6Yp7OT6ciL/q1EltO9RE01gb7GP/qNeG3mPgsmNVdFVaBV2QkM1CBk6VEUQvPDSBifDEz8AQQBoMOiHCywM4JtJ2jKxvuQxdjgcko/Z5bWwRxjkAtKYd8H+wluyTy1B3cC5/vW8Ftver68iShX4brNXHD+UeT1FAEIAaC9e5NOH9yiYMaAmZuhdJ93Zj5CoXMFwu8P2OVxIZP7wcT6hjdXcHN1QEI/NxqDg3uUBp3udG5tK3tTMKI7hQCOCdsY6C6TT1A==</base64Binary><base64Binary>E6g7PQEAEAELwF0GvABTAEMAaIjazB0dwW5ctQdXKE/0sSSwZrlZtwFvzSX197y9AtPWBiDcNsLdFK3KnuIvae3GJ8iPFBssI3ewzaejMgycjQjW0rfAcprmDhbG93RSulEfouxhAEQAaJ8q7kumx4bnoxn2UalU9EO15fY/k0/gpr45Q0sNWUVxNenQv1D4GZlLD67LGqsMkkweW849R9XcsPkYUrwegBOLq4IhW4hcH8gwLj9u/qmE/xqiUPtMUJrTbFslSOUAdwvAXQYPAVkARQBooTyUfh7Sn5sawxZEjQclz2yl3wD9MonmMMQu41Otfh1CkaLpRpRyWqRTmXUwsxDxWwlSCWKqH+0WLd4CLRcgbSFMuO81n1S+d4IwBiFJ+AmSod70lsc7yVcARgBook2nZ0K0neKej2LGUo/tzfYO+kGDiufKhv+C1KlN6X8BIHmwR1M3u0xo6HIbsXIj5KfkyjXratOgSUz8Lm/I0j7a0bqDORkO8rcELzDCfsttrsu4fy9TAEcAaJ+HSg2zobOlK++IbEYN72v8woty0t7pwS4Zy/g0Ir+cjd9/ToAsMg8AOO8PpyvFUEBhvPhNAaGQ9gh43XmrArlWNmH+qJzyLevRcXqYqwUq4W8LwF0G6QBUAEgAaJyJTbODzWX69rcSvUJLrkTyxYOWx07ctgc8vqBZo4/fcrUd2hdHOhJ+w063KBFBgn3gfcOgLYL8MceMhXRCtSD5uu2Oq5afg0L43XXIYsUP7lmeRwBJAGiKUTl6XI833wJt1NDakvcCxfXxLkXrbVR+qygslNwWSgUHLj63hnE2X5U4+cMCWbUuF/ZVvpth8ZU6xVzUeGfEdYGsuqgAQgBKAGgxEWzOnA6xmvBymBsJCs3pKvI+NV7Pjq7csqKun2C5g5wT60Bdi39pPmKyzeLq6E2SsGt4DTKJaUcVU3LfJWF4Rxm7V5c=</base64Binary><base64Binary>E6g7PQEAEAELwF0GWgBWAEsAaIETw6K6DJbYSdWL8T6v25UxH0LKYhVq6+AJMdx1cTtyernY/WmFj/fNr9P4FRF3CULFZsBQGXFogKZ/Wpg4/pUS9/BLZIQApOsalbjwLXhi9PtipXwLwF0GggFgAEwAaJ8gHa6d2M6fnZNhgK8fjy/7rwek8NUfhfTUo+jTNPQU+x835vbvpeDvTP4CrIQ5XfV7653d0r17dmtoJGiPdC6/SjivA8HU/rl0ciyuZ/OYMsJvk1547At8zZQVsc3GXwBNAGihVgmaZHO4H3nn6IEHXkkxBqB7qFmmLUacaSwrVo7QCtK5f8AtF/mR8TkJ68wIuM/bmgBnXqo11R6J/mehn0GNtFfuU45lV+B+EJHA4Lw8ZgqvGDrrEZdc29Ofe7xtVwBOAGiivCyhHCYJ40Mh4gkK5thUVTycboFEV1Af8mRZgGvmRcAJyiHTFEEreMVukEkcdTdoDKdSxJJjsI5N0AnuDLyD2h1M5+M/bcD8IpmygrXvANDF6VCW/1wATwBoo+kQbEXsCu+vMMIgWtQ321a0U+wJUDSPT0WjvuGKzYtlN4QlDRyIRam/Dr1RS+X0AV10tM6nJ1Ag8mA1T0mpycP7xPuC5H+0DIQ6aYNc3w7TbG1hwWeEQ90kzyL0SYk=</base64Binary><base64Binary>E6g7PQEAEAELwF0GVwBTAFAAaKTm8Bdy9fLM72zJLbxypxUwaQ0fQcI4x6lOWEEHRa9JsM6AE0DlpqtxZVfHbGQb2ndkv4J/itbtEgdPPzlcGSTOhbo7OcDZ0pRWmxmGhrk+fS0LwF0G7gBRAFEAaKC4yTlV/5MeA3kP6jL+87gVaKQqkLLCm20uNXeC+lw8NTiZfNxnkIYlL4a4Ho1PuRHUlOkSoJFymDJrRn0sDxCWnA8p9QSzA8J8D8MO3ha2UgBSAGiclgAwrY11P2rDkRy6smgrRocT8c9Pr9jMy8KdeVeog5OKCXprdVwvdftA4+bvT0ahmQhDZe8OB6znV6MnYDRLL39saVqpA4mMTssR1ifnp2I/AFMAaDS7QUw6MW1WSzaXiQEv8yaUJRsYftpNimQ8KzjYy5L4Z4WEzHQKk0GfQrYmhj+43ireX2vVvdgn/5cLU7XX4ZxpSw==</base64Binary><base64Binary>E6g7PQEAEAELwF0GkAA9AFQAaDLSTEOh9+L+lF6iNShdPITGPUt0TEdIwJXbgt3fAXIeU+n2CsPmYFBRMj2zEej3KERY8S2fRpygWk/EkEsAVQBoMV1468yPhtOy4faBgKmKPsnOuVWk6OYDZVdptW9f2PtgEHDhTLI3OBeOjZLeO0TFuaqf19amgbtzrEZh3iB6OJ88+1E5fjr7JtsLwF0G2wBIAFYAaDNzlczZnN81tdzJz+4kMcltMpr4XQOXZnvZue4gSieH0lZnv9hNIITJjakiqbLaYzsKPM6W/7u9WDIOKYHfVLWyKnyUeHd0RwBXAGgzMVIs4jN2hrOHdRLlDi4T3wxk7/6Zap5/muNL1IbWloohlgx53fMitBFeSEDaYo8gRs9xqEQ+lnUNxX6zg4ZD1FktF64BQABYAGgyjLCFraSJL+g7Wu6K7MlDVAAURYYorIesOOJdPeWvGHy9ViVtsVwKrR0jb6awbbdwm43ZqToAcJDDioNeB9avi+IC</base64Binary><base64Binary>E6g7PQEAEAELwF0GSQBFAFkAaDDkMn2oMoBl6wcsiVHYEfNAKAJZa1PmXuj6mYImUgSvmpYaDZzjj29ZTxpsDbi5tI+8kwqLDgHp/BWFwZ+5bHTb7JppC8BdBp4ARABaAGgtdTfmdb8zTmaHcPwSR72pNSDTH+gpP7gGvN5K74jkg8Ji4GFFK8yBoraR5MBstWdccQ7CRJZDcl1R43xVaBJUme8bSABbAGgtdiBvPfO0ZrmERihaLd9HX5o72MchjRE2AgIZmeor3FWWHWf+ny7Pnd03ZIrmiZIUsh2/3Joh/Pky2QTjuk4UTrnlLuNJ6wEAXABoAQBdAGiGaQoC</base64Binary><base64Binary>E6g7PQEAEAELwF0GIAFPAF4AaIjNr54KMT92DsCnviRopC84hUItzrG8oVb50rN0VVIn5PdJXOQyks9eVLlNufV3xKeLtFaPxh3LaEDWui6YnXn3oNNfknu9hFxmZPHu6mIAXwBom1Om2mzJzvw8E1Qnt6PMU9KMwAu8gBhooZ/p4C/hX+Cd0Ix1qbWAqdEUA/TJb5542eSi1ZSzs0V0J39IcYg5fbEuX2Zz5OjGJJmy/5fdqe8j9Uv4hSGG+60LrpnB/qSgmGMAYABonG2457LcobZ/u00eAuIoIk35cYDLYerIzBl5iUoZMTE7xlozn+GXWyL8uKTqr+zxdC4zU6Xpk0qpBHR4+8bITreN0a1xrl4V0jzblrfyI5E845IQ0Q8O7CTykAFqlh3oFqYLwF0G7gBZAGEAaJwIf2nzQ/FqfYN1Z0Wg3NLvPwXqf5A5dbgmGzC3m6cR2dYSCYZyA3YCHzsrD16HNzxvz871fra9Lwz02sCHBYJUfeAQY89y7eWoFncYGbcZb8WnWvD2xPRIAGIAaIl0fncreyiQ158QiEetuNfHBc374tYg/UNgp7Y4k7Gpme1Z7f2SEMfBB2OW5cDbLkbZeq/CM2dJOfgbtEEf03oiMujh6h3wQQBjAGgtbXVVeZ8kg6whuFZuJdZkI77SVi6Ho5wlfwjJMX+QkHsss/8AoYuiwEg3OcLvS4LvWpISCQXRBJLj8emWGNAXC8BdBksARwBkAGgs2eQfSMoHpnd3qpnnCjKDJR+Dc1Bqhu0G/to1PkZ4wQa5v5nA1D3lijgGZwCpzF5Maoklkd73QQXLWVvfYSzz/I8XyZ4DK9gG+A==</base64Binary><base64Binary>E6g7PQEAEAELwF0GrgBQAGUAaCujIe2tU3RSbTG63+YWx90FplpvJlsppxuD4upyNb4bd07/0QoBEAFqmf/BQmOtqOIT86w7dpKC4h6LGtmtv8+0x8vlXpB0CEBMm21hh8FWAGYAaIjaub1GZ0Tc7os38+YW4YztCf9FP5ulsk5e3BxsJAgR1z53cYQKf/h4FxW1BJZrFGSsiOldEMtwGFPezg8mKHqcYbBwGRvkNSuc3LhgnZpZsUQGa3ELwF0GIgFfAGcAaIsSS9BFv+akndEbeAjFy9B9SJ3e5VPEJDo8fcy2uQEaWCkBafOWCQOCMZEjSO7ujHSRaEbJWIFFnWKULUxG+TkJfYW5i7R7OBiT3/2T/Ky5FKPzksd4sW3CFVl93VBaAGgAaI03wBLHZxFjXvqrlinzvLQP/tgDYsWBb5wRSA/VxUJsra2r9bz9CT59hY5HrEL1PrLiSlF0EbEo0KqfUovD2LkUEgK+S9wQqFDKk1axeDctquFqaNRzdzZiXQBpAGiOsfxXbmB2G1BeI6bQMsgAUSrB8YXlEFzAwO0QVweJ1Bhmu1iMcW/o89A6fhrdnOO/Eij7umevhmsv9Rv09XRBiglfiB8Egd2kfpH+oI2aLTwmYB7eH0yNFa4YpjsZRGQ=</base64Binary><base64Binary>E6g7PQEAEAELwF0GHgFaAGoAaI+86pMZoiP3HPH+1T6esfhCz4K9ebUuuZEoYohqp+tzPw8Ex/xqeFhY09gJSmXdPwPzcE9u7BhU9vVHajVaa/8UryxkEjpUt4kj8nnhufzHvYzafwSOpPXsXQBrAGiQTwQZJygTGjb5uKwTlR+nwCIuMgJ8oQXOOS3/CJr1NFnm8iT+YY3aS5kO4kbTFkmYKzQBV9+7rHS5uI0fSroqDODkZRD0X+MzsyLpr4IVjUlOZ0Ba5sfbpNUGn1sAbABokGmq532lgezavLoaD1/jOfx5iwT27UjL9lxHyRTTaCWf3AVy2whholfzvdIO8/YTdkx3+XblXUY274P2s3wU8hoH+l74m05+BO08E9frRx/IUuZIQWb0OKIvC8BdBhIBWwBtAGiQ+QrSB8TYbuezag9CiVt0TK9Zool51Ryad8DCY4/cz/VQnYAo/qJ8613rsyHDeIrRBayUiV4M0UIYXP+98isOf30BaE3t47qZpJYJkI+qVWxxmcmKX8i9FXZbAG4AaJFti6KCVyvt0w5qFWa8GaptqK2UKwC+/ZKxhBY9oGGjs0PiF5wOHqTL5nXybWd63x+H+8x/3TeJYvGxjy9bm/sLZPE8c7N0odSUdO70TviOiq1afvHKJXaNGVAAbwBokXcOxl5yXGee+4uCw+/dCvO9YFdMaS3mmwisdP8xetC1p+ERJ/w5txdPQcO+mmoRUukR9znb0ijQHFT/ugCIzviGfcg1oCIH6UevP42wy2Y5t/w=</base64Binary><base64Binary>E6g7PQEAEAELwF0GqwBSAHAAaJFr3UuInYC10hGD69nPsSQjJLR/VtgPmK2PmcV84W8g5fyU4IcjGE7oMWvkX6Y4P7ICm283X5o7JLENdPx8ArVpfZZbDdDg3nWPaOVfhONiB1EAcQBokWvdRbKln2QrOFh+SWKfXVf5KNe/DeMtYBttGNoK+uYK7v0OwTDuedHx7PpNiHDMznsjdJnwkA1M3lD/lRzlStTObG3/hVVd3rcLHUa45TsLwF0GAgFPAHIAaJDcfbxs4G+F6kRF9VqkgFvQODu/mPngOhFyk8/nFNRjIK/Zclr0SlyaLLagXcSGCG54FmgZBKNOw3GnGhJyF3mzEkX/nx0yEIuyzrGBM1IAcwBokLbmWs9jzMLHK1TiGfUYMBEStwjbqG2L7djyNr+/4EitJ4cVJ8B1YEHB2J+wJS8e9YWcajoF52eKpOlgimfJUZ3U1ekk3KDNYhArx5xJQC9MVQB0AGiQJyNVjtse1CR2e70abDyiN9aOiRrpEtVuLYCtBMGLtIPlbE5wqi8Ulh5d1WF4zRIuH/B0UmFiYTILZKqKdum7t95N7/axrOFz34K6qbI0P3oeoKULwF0GsgBYAHUAaI/LizG19ob4nPQOitRdSuI2D0Rw2aVJwbV9yzjsSKcLiNHWSHG7lu1/9Mqe2uzLlVeWKVKlWsaEvHPhAk0brtT4bgnUroNl4hDY2QLh8byTT7Rmv7IKcVIAdgBoj7Z60SIp6DY3nnESIpB6v4RsC3HnGokdzObfDsjNkIoL8DFpn6hplmygFEESD0sLRFy9/8xeEsH8HMD4lijr/2wXCh3zz/DM+8EM3QH8Z915fyjwnQ==</base64Binary><base64Binary>E6g7PQEAEAELwF0G9wBNAHcAaI8EDKqi/+FOmOqtQ8UEp+iQb8ZLe5o5oJCuxcTiJPHKJhBjEsw5iHGIBZdHkd/acDoDgIfIchq5pSoD4nqjgt3FyKrQ64D+L9FJhadOAHgAaI1jxGeGhCsgPOC6P9ZZGodYuInC3j7UhIOj/osl2om+J+B0HrQPJxhf0QybWtG6bGod73hv8Ds/d4CyyF/i7Zmn6L6qFM0IMffKua5rUAB5AGiB6VZ+R//mgEkrXYv5AY/N1SVIX8XShSdLB4R4dG9Hgv6vJnSyieTv/atpJqRuj9RvMYk7dUq4ofyEyYO3eLqiSh0GQroMtd7wlYR1C4GyC8BdBlIATgB6AGiLT4b9bdhzfLm0g7mqK6ByJ0FEG9HlpFT/+Kn7ca8ei2TL81otUiaTSp/lZTZ9xWcONN878wPAmDVIdM/zd5dzVj6eO41ymtLcMXjtsRVQvds=</base64Binary><base64Binary>E6g7PQEAEAELwF0GJQFeAHsAaKA2WzTi4FmW4cvgxjyCAF5Juob0WUQeiebDx66hqPoJTmlKGPGxUn5ATLezTfrhaVT69YykamOHi40K0TuP376wJf7NmELdlURjHAtXhFwvsjyxCeMjJ3QVfZ7HXF0AfABoorou94tqedy+XdxibBWZcowNm2mqwjnuSINjgQItHKxg3Fmf4NrjmfnsKMQ58jKec0iwL8I428uEJFmtD29EIp8o86chzUtw+n2ZpSppDGtO2f81DozZek/Jcm9eAH0AaKWKbm2coPNNPDJIzA7AvcENoDdnZrSbCwWhyYfEGUDSZImMCWEk8s8REQRoBe94FAyvnWitMdtLOoSNeqXtid2rxiLQ8jlpJWb0FZl2LbxodHl01ZjdzM9HZjxHGwvAXQauAFcAfgBop0aRY3EGeolhR9/A8n8atgxmPubRwbJUVvJIeRDLQ5z8IQEfGqBuFlIXdKWIGtrcKYRnMy2S30mPjZML3QM0QpAEL8NNXbwb8+guOoezD0FvAnw2a4BPAH8AaI6l4J/UhTmPzMTctc25SYfE2EwZJguaSmSXWoc6bdvBZqDxKol5ep6iC37KcaC+aVM1lHkCibevQ5nx49vRR/WAegGTElzQhVtDsMpEZHzSIDk=</base64Binary><base64Binary>E6g7PQEAEAELwF0G8QBOAIAAaI6hMUllRQbvaKPH/6EHffx0PuGUtRvl4SmJf1Vy6RNZkbLCltbc8jqwDpNxQJHdQ2vV3MC7YUhEoc5qgCIY4AWaf6iS6mBa3QFOQ0blSwCBAGiOEFRfV9KL7VFqIGvLKesEVyZXRLq4HL+SU3oDIB9mCxGgIfad1JDZt4kVPx1QhGBnvl7yWJQRQAK0EvB3zwiIC+TE+XIOxISFLUwAggBojN1V7dBFfFE3t0OMcz9v3SLBZagw79Yclo4aCT3kY79vY05Z+hypBx8s9uGfa+Ve4gAJWhcMEcEUY/1nQwMVegRxWK+d+j0+zHTgC8BdBrAAVgCDAGiMfg57aXchcEPoHX7SShnNrrvF9DwRyCp7sCHHO3jDEGUxoQA6A7VWsEWN40H80s6bIMmj34Wa4nDw9JxWpA6olLmsSpcrHnObQWoc9xkZDjjme2qdUgCEAGiMlQr8cuA74U0cmdQ+2XugIA+Z5/Zcf5WxBcJEn2v98Ab67iDjhO4I3J3Ar1P3G+775d3vCP+wZykfOthdDnLiSX9xeX0LPK5HdX02CJEGZsp8ON7U</base64Binary><base64Binary>E6g7PQEAEAELwF0GBwFSAIUAaI2vPPPSTM6cY0lFZK2rLhS13Vi8FQ+rSeBVAgkTna8tffIf/c9p0AEuHq6tu40ksmJLKzAVCvp2L9geZzMn12JMD2Jd1NpTuDdhj7ym5AiPjVQAhgBoksf9onOLXBLJGoBca0fVS3QFWN74ZExhCb6Vveee0V+LbNVLQV6fetqjNT4VeERLJ5AwiCUNU/uKCVUwNb6rLW6vGYa2IZfZ+j0voGoR1gqhseFVAIcAaLMxl0GAQjzcVpYut8KPjwZSnMYqdeRIwuayX5kwvSJCeCjE4G/7STdFIhjfQYXzFEB1XBND5cAZLdsQIh/tkYWXy2hB3Vpf83EBRNS5ZZx5PYOVTAvAXQZZAFUAiABorg5jQhrkG6KzGUzyOWfBkm/s8xsggi0HrDGWUUCfIKOLtCGrYXU/ryxmdwaNk49nysjOUxNmv8hrvCwjDjNyR/ANoP/Cu7g8T8dkROvmOrf2VF8cptZCzQ==</base64Binary><base64Binary>E6g7PQEAEAELwF0G5wBPAIkAaKX27IRAWAEP0uCFZo4PaQgRUdvFneArbKbt6uVzvfuOCFjbkpm/Lm1rGCauhjzuLKpadba6f7r6XDL4fmuunBzaF5BX3G6z0BsCDPbM50oAigBojWNdyxPG5Wy5u89XMqcS063IXEzwUwxZ+0abffKDFzV16X/tcMoW+HD7eaSU72ixgKvPOfhydvUHamTjQASNEOmwuekSKm5D10IAiwBoN9d71TPwa1OwoeH9qnsP7sxd9COLUBnIexHcoQEmJaGzDALwayjVZCKa3tkGbusnIh3XEwaU38eFvdbNE6bj3a0LwF0GZAFCAIwAaDaKMotGvIVCqtF+5Xju0H+Bu3dByoxJMt+khWMtaQJJcY+l9hE3Ku/D1jwqfy7pAIbe56hPZwiu9R1vTHdRKXnFWACNAGiMLQV/GWbCaUst3jvgUFZ2LdV7XjaDLKanGbKp3O0aGlfjo2dP+pabO/wsZWsT7e+r2NtHcHJn6MUB2p34qUeq5CiTujeV34mVFu75AKkqJ04oLtWZUI5fAI4AaKnXAd9uJ+MPyrnvwDLRvFuTTwXevPapCagwV3+dN3XY+9KZg78BfG02JNH3+LY1r7ne8KMuyuoHQrggA71mci9fh3g/DX5GocBYCb7t3JWSABXQMpoRrfANwg/eTN9bAI8AaLBGYL1dS+DkYNbVHwJR+p/gkny3PbuYEYNVC7LMoItGJs92dlFZmN89vrU4MSW0vD87f7TeHjV+p+Zit+qKiNGLCyToxyCGuh+xkWQyqu69AROVEKXtW2D6y71tsq0=</base64Binary><base64Binary>E6g7PQEAEAELwF0GXQBZAJAAaLA0w2iXmB5uev7ubHM8e9j7T+SGYvxP6AkKziLJUeiK5iOH1Qk1NGWjdkHFPtKLkwZ6+BNhzwk4LaqYiPfl3fnSGkXXWUi7aNUzHqE+rXoPdSiyE7y3pdcLwF0GAAFWAJEAaK4PAY3TDbldliFpAgfd0EOO0vIl77hAvStBRx779v+LCv+UW6SHwul7RdyuzfhTJjVBhVt3nOMpZlvXUEdlrUKStSv+uGhVpPM1JamlkdVX8oNPlJFSAJIAaKj+9IU84hvTEzYWoaEqArxS3GN2DXzkxX/CC6epVzBCCggP+CtG1iczFzrsyj1m3nn2YrO8UGA3Lco8aSpfFKJXn3Q32Qr0UbYI5rQiTwuI+EwAkwBojnfx3k8EaMO5ZTdsPhEwdCD+BBNhHUHJTWtpjUctgip6xNaowpIG45MW4308cm06DXzJQ0kCiqMgQcklnFJJZersRrkYpXahbEh5C8BdBo8AQQCUAGg3KY0Lnp9qVH0w+GqZhbl2xVTtoL7G9awUmauhZ3C7axBTsyjz61317f7OjQE6LrJo7Y3cnsAjK4XXhUwGUK1JRgCVAGg2e3RtLWKUZAyrQlukveeYVTKY5EpXw4W8P+5/su0Hvh9ObAC3KK+17u+23/hpIrPBM1v6+ye12V5rd+dU+0aiwR4kIpu1caLa</base64Binary><base64Binary>E6g7PQEAEAELwF0GDAFSAJYAaIr4RWBf8VFvrcG9n9V4vSb2tGYeJ8hQxETfRN/b8CGrCA+9L4DhLtiR+jbiU7AJX+Uk5qVrHLJo2ozRAnQOTXhf7uf7y75dphibgwqC45dsRFkAlwBokjQhi99K5pLHqiYq1VDGheRpFmidtdC3r8pEle8qUo5KUdbdc9aJ5nrYUdLTS33ZRdZTpHYSBsIahwTK2REfA5rJgmPcpQY9uKYuTDd3gexMcTl5M6QnB1UAmABok574Py4s+46hZKzBQWKrOCPPN+E0UicevC7G8f+1IEzlcgIPNn0X+iikaCDVdk7Puxr6SGIPG1MVJrDfNbclNf10Q1Xut9HZLf4wgVq+qWx1pRi5C8BdBvkAUwCZAGiToSwD3wJVXUcUaAdVyH9U6TUAM0IINX9bpnTbMHDstYY6BL4sL2Iq3Z/FjC3IGdElZhfSaewY8i+OL6CKLrDHrqlHXeeuHENKpDJxzXtuXvsBUACaAGiSWRONoFV+mgk6dqH8xFLhQt8IK0mMPmjcJplFe5H35Skk9i5DdYuoHaUzZsJr33g7NTXSDmg3uUFWOKq/My3VpZeu6rnfdnU0h7xRwW5VSgCbAGiOccYfT1s8PfIAXm6BuW2Kb6vT1USjorqbrri8gpgVjTDmDWFwyLd/ZbiIMWPxqfiYhsHiqkMjTefR5bzzkMnycr6kTVJHOFkmw2FGYg==</base64Binary><base64Binary>E6g7PQEAEAELwF0GmwBCAJwAaDfcsqhzD5z9Ge46v5x9spRvpMm8hdeVvYAwzsfSFAq3ImQ1HBpt/JHsT3TBFtVhQdzXooEIHVDtNc8NKntJ3Yz7UQCdAGiK+DT27DnUKHfv3TORW1dwnvgY1/H3KTpAzJ5Kf6KvIfIw9WmXyUaWayP/mXxyUEb3YY5qzyzUO3laQAFgsjBUtgAB51zhvqW6FXPFIoz0uwvAXQYdAVcAngBoguJ8k+s7Od6PLjORXWZu4HqCTAv5GenhyAVzYstNVDG65/VZ+r7l+gBV+Vr8jI7hIiDNKjN0YqOtQ5ctgxi+FuFD+pWKxbjJFmb6Mt128KdKY6BcAL1jAJ8AaLRMC0o6lvpIWlcagNfNEkRMOx0ymv7lHF4f/8z3HMm/Dg3TR0Gk5iCKTxdUnzlPBA49RC6O2vo0C6YJNM/hnl5lMYCxaTf8KXLiesPUUS6z+HdZwIGOoJX7fK0gwcKTXzbAVwCgAGi17BSfedUIO2rYQHRjgMy9VaTIW21Izjf7TtHuiLJL1m1wEh/M0xUA4eHdd9CdKWcgICup/IUEORvyWi7epLL47Ip1IK4LEjZBAST0OD2HN7HfK8aGU/OJjAg=</base64Binary><base64Binary>E6g7PQEAEAELwF0GWABUAKEAaLXWkm5ywgZecNURxuoBpzHFSej25cN9TcNgSGLgGFsXUmo73joQ2JzItGCLWEXn81e+uRoKkjKYeizwevqQr8YB3tMF/pBZaxSyqAvySwlyYoH6C8BdBu4AUgCiAGizJAxcmLM2dX+HNV3HwfSE6jl+yrlxY8KFihN3IkGNPQLCKpRvPtKNAPh7wgcntY8CVhLqVHUDuBGLOgMg0nQRZLRBVvcA6a+zLnhDWasqkz1NAKMAaI+mvRxCq4XnmQvBrbTKbKYS0m6fbsn8Ot9WhKaAQtDn1vcZMFXdWUU6PQtUEdikmvMGbsEYXLZVeFcMALSVonoJnrP4hr6zbu0LGF9DAKQAaDpJTH66dr02zTwH8782SRausXrOiwGk2SIxsInj/xXy3/wg2tsyKEC5Vrug7qRjSjaiDWLnvRYEozacw4DqNs6+MyVLsE8=</base64Binary><base64Binary>E6g7PQEAEAELwF0GbQFCAKUAaDfaQksPrxMyJQHcGjM4ZyDiElu/5pITq4rsh3So9Gy4cTzeWHh1EnyHGVi7M5J2NL+SOq5SC2/l3chwzyjEnwS5YgCmAGifPpkDrGuQKnXLYWxgWj6p9ZB819RGUx2WkFw6uWgCRttd61lgr3LUccX5NXX7n0R3E05/D5sbQpPQ5lPKBUeKFui/38nw+LQrDwOWaqOpLaQp76UVu04egVkQ5BzM3rmrWwCnAGiyUW87uAax58zHkSDrq7W9qvmYTv4XNyl+irI5+XRiLIWNu33farJuEOhm6k6a5mUDssJV4vPz+iEGuXDo+z69DMrqwYqvIe9KbbBu/aNTrSLVcfeYHpuOWGBeAKgAaLO9eJU1ee9IOctMzPJWT2qIcD62/fj0zNyN/CThJjLhX5XMPGE+IaltJ2kMA3g0Xg9N6wiKzCU/CF1W1uOqOzjTcnyvtHS7v2sc429AURYoscWW1ewEmRMnMHFuwgvAXQZfAFsAqQBos5bjea+ebIurcWCdYWGScvr/JlcRJF6HHDJwF1dMHRVnmoDwBWLvJHyI+iXMeiJQ+vGVhK8x5TPpOan6gymVienlGjnvdmZZM95Aps/ZQQxP6ZgCfhYjk8RaJ3ZRag==</base64Binary><base64Binary>E6g7PQEAEAELwF0G8ABQAKoAaK4OUlFns5zGKtu+LVPy+mRTDIgEEy7DVrOeAlP8s4NPPK83Yh9wK0PLDOmbcvuWcLu3y0zArhz8FjquiwIQiAtJ/i5hsnfIR5D9xqaPxM5PAKsAaKXw5lzQl7BlXdPihrFir26MKs3Hw/eE0YKFQz9++hb1OgflJdkpiyvCQyt8HJZmHNqlIGpqvKyPKiUPuoRZ3xD4yf9SUf3qHdLJjt3+6kUArABoOWoA0Y+e9MpK4RDp0SILcC+TSRQihREs7Z7XqaCixtxHtkDaiKTMo/G2uZYuVgP+z4IpyJ+/5Ay6N08rk8Cibg9xW9ALwF0GmABCAK0AaDdMcBBUswtR+VeBkmqlEikHIaPWZTo0sbUZNeGyJ+m8HnthZvZdpv4qiVnM+sLYk5o1fumkHloNORszwq5ylTEQTgCuAGiBiDPY5l0cu/4wBujvwytbJ7ogw36+ibXAayqsjfBrmZYsIZztiLSONpTjinHaq5WmZJGwzVrB8LS6rLIw9dj+59AnxEzRTuKCXwNLeTqhARM=</base64Binary><base64Binary>E6g7PQEAEAELwF0GDAFUAK8AaJEdVGFx4MC64QvkyfjdoEcsdSdWkmEY3dW04NZCBe3h7a5GmF9mDxile93Esyj8zPqwlDZkBEn+oZagSxqxAhRSytmZSiaO9f8C6sQuu9QSqe//UgCwAGiTTqIkVF2XAogeS2zzYyThaLfI/q2jBgIRk46I1M9iIjvqT5Aj0ehxxacPJvmltRumvXxIBBZb04zJWArgiEmOmGtLXPVskzNAv4+ZjOTLmplaALEAaLO0VsgaZH5T23MTjb39j3F1Ry5uJzgw4PZENPiv5D7dmK4Ja+yYSA6zxEFTBGD4ryP2WIK70/seCwPV06nbxRjHcR/Ez4pjW3yDQpi/LTuq6w4hajT6OxA9C8BdBvcAVgCyAGizD86K2CxBVt8lPdUsiDvfwQ91Y/3mhIdOpkUY7PvbqpnkYmHp4Y/jqOirRlJw88BMsnLJJICPGQdjdN90UwW/3q1WIVVWELmYt9rK4n1k/eH32WIPTgCzAGiPi7LW09RojMPP5x1bcCh3kpCrLnn2soSejm//wGYMJC9MDKHEujvXYjIXDrUsY7KpgTourw1PT0jnuEy7nDdytHtHDBrnVH3FOSkGy0cAtABoOMWtGZsNu9f25zvlOr9RE0ixiYtFER+PrO8QIHEaJMMlkuFyAHxlcgFik9fuGwWjOKODYLmBnICM7ABSNtrImg2q19aVOgvAXQaaAD4AtQBoNwxcmCkubnAwrW8eM3l+PTdNwsTaJhvt9qBy5DzSMh78TLAxcnKxqg4Kqi4hLyhORNG8Ojg5k3r9Ka6T/VQAtgBogZvGzGLSyEoCELoP5qNxdSQ7FalExzzuTfMeynfJAz0m2KE8BY5n4SuynvILi8Ho8JMrfzQChQ83pNCCe8Be6u+JbsjRl1QwkWWt36f9xA/RYpT+o/o7</base64Binary><base64Binary>E6g7PQEAEAELwF0GBQFSALcAaJFEnvBiNDVGbbqH4cOyMdpwT5stFFg+sxAV/bmM+cYLcxvK+YKGl3cXQALVpddtrLKU36WVSzOUYl29dYLbGxlflDD8VHLycMBl4PtUOKrO+1kAuABokI5tZzHzkY4ECIBBLnIpjwYlj3eToL1d2MBoRBuG58tcOyvp1ztiQpyvk5DxK7Zfh16zNgjEUSAb5vWZwoHEZYaxON8TCq/5WsiNuXxTCpVuxhaJCGDND04AuQBokyZ4bAz+i4PTIn6IJW5uy3O7kS9MRvc1WGEsSeatjEPj2fp17jaSS6TnkIkaeUeNgtjJz29UpuIsjN6AAfVKV0yWIimCNJzz1X+y50ELwF0GVgBSALoAaJMUfu4pU6TxpToCAZr2K4ieKhN5QjglV3/BR8yxzkvBSxn2V1WJCoR6ycEO4AY/s7l+Fw9uVyUxr3PrFJY8VPMN92dCXY8EP9On0jDF4f43v5i5E9k=</base64Binary><base64Binary>E6g7PQEAEAELwF0G8gBXALsAaKxbpReVUXIUEPOitVWvOEkyUe8/J9PnxIBm+ZoE5wMIHjvj9YFoLTk3UYHEeLqbLet+KqKo0aPkTYRlSV7ArkmEOEsOOaYIA0LbU4yMjiD+N/Px8xipSwC8AGiN5CfP0XbzZN0PnBQvIBpzy0xErAM/Ql+N4FiBmsFr5a0aNlPB/e099OYTGX3HkIWDgc+GFs5WYjf/9mi5mtE0aIlN+TXjXBdc4EQAvQBoN9gUsztqCkMF8tJFAuORTRxRFV5X5g1lvhvg417EpvKtflGr7UgsFHQktjbsh6B+WSrJ4WJBYfJgeYIby8BEGeCvjQvAXQZmAUwAvgBoinfVH4s2FCYMsVYxmdZJKr9noWm1l7YWNZBgkjs1CtUqzkvduRVsLvUTp6pK00m3s83DoVgdc8kIOwfSbEOHMQy+okCIQ4KBvxF/XAC/AGiCqQO6oYXPVvJfPPsOEUQBJs3YDoL6e2QgCycscembFQ5GKo6FHyuIOncsJewetpGIMOrCuC0CBA88kukpVjlWKpGj0moUDGj1Ld23wu40v53uDInh8yqtBhDhVgDAAGiQhVT6nEbyqaW0L6unRS3nvSBKtCO5gg8upsMZ8/GdNp/lKAd/KYuuNTaZ1FDrnQxnkUJ/54dUxbZ8OyzNRZQzZOixQA7KZYWVEVyMehmzzQ/wMoqZWADBAGiDxfK3lkDe76SMawdoGM6yU7QXloHKi8DcaPjwqaQuOQmBpN623usl1FjA/YkOHwmAnQE4ZUpIGz02sl3b4nl2PaPvGYE9wdlY9vHrXwB7j7Tfq1DWTFCEOfYh</base64Binary><base64Binary>E6g7PQEAEAELwF0GVQBRAMIAaJJ2/J/07/mbhrMgI6qtctpfykB7IyIVfqljmYmmOwG3gr2KN1hfvD1qi7wDagxX1DrjYDp+PY+zJ3NRYhEue1gGIO1iaEQ0nFqstdoWviPrC8BdBugATgDDAGiQGx4dak5dS1SKoeZfsQHEJt3f/L9eRiSDkZ+K+0NA02I3Z6XLxeZkq86vMXipv8kgVJMLFdptTAh3MBjPtOuMFCDy1wuSZMZElzHVDUoAxABojNfCzck+l9yjRAU5ESNZkpfkJd0YLaeBl4H+PPk+Ij23stKqujoD+QyoMdwrpASGdtmmYtwI6+oyDOAZbr2Ejn7IvceSdQd7Y0QAxQBoNlkayPO1Gh7qOd0b/L8TOz/pcmnffumfWOUwK2F48xMV20+T2z1IdN0a1+FjUPbw52LcOrJmOph/lbsADSUZp4D093pAS5A=</base64Binary><base64Binary>E6g7PQEAEAELwF0GowBGAMYAaDMpHpBw5v8ZeOw2zM9rvcjP6IXQez+s2SRAClqcuzF8BwU37VpN2sEMPFMRH1pLdLCPYol0TJNGciUmaQcVin7vGC0viFUAxwBogVkuqNN5b2XXXLJaYO8KH62kMMRnMNu9j3izC9/TMaM0j2EU8D1HxggyKSCqYErXZwx0yXwlxA+VnFtfA8bWcjtJ0fnOg1hfUO+0LVqIwL1prAP3C8BdBg4BVwDIAGiD5To8O+TtUbmUZ8asb4sBIFbdM7bSOcjKWxWBgx5MGEjOEeDYUE6wztgL0snCGVhS384wMxw8BrqhshSVFzRTLDdgZVo/H2KKF5JJLqWE1nOdV/KQAVoAyQBog/QfyhW1mjgnFoBsJWabxWwXIpjiIOj5D3l12GLdc1d/q6+J9NIzIJOD68Dtrcb/EPLhPlqN9MC+57UZsht6CWq6S2gUqjfCUbJSeQvzXpLuwI3AiVGNX6NRAMoAaJJj5hytZke9d4IIVpFBIP/v2x5ufYn9qP+UMSn+eRX34nsvaN6v3Cd2ZPW+ioIDqBMkXUK4V+dv9JxX5mnJtNVGdsUHTHV6eM1vrFW5UQHCfyIVhQ==</base64Binary><base64Binary>E6g7PQEAEAELwF0GoABNAMsAaJA0Qymw/eZR+aMZs4FTqycPbImBu20C7IrBGE94R15c7MPc3SCW1pYltWXhujEh+JK2obLNhzPTCjsshpoCd0/xL+IgM4lNtG2XhYpLAMwAaIzRh+A8lYNpDApjhp9PQoVttNW+Qt6ORYL2m4HLCd1sydeYHn1gqmwa0pT2mygxi/gijcWQl2KzIeeND2GWrv9EQHX4OWCYBJ7TC8BdBvMARgDNAGg0exF2jiIaBD6YlBfRyJsxPOBp+ngV0nL4TKQHU+IcAxgggm9BCD2NHwaeKORohfMuHHXLatLkEY4aHR5XTEPtyvZX2wVMAM4AaDPNNbxiSMPNuPfcgGMz8ymuxxdoDx3ixsxwklpeEF8OJRQU1sOzB+1jjuO8/YwpWXHSEPNQgwaRPJXLY3J2TLZmf5jnO9TJZidI3FUAzwBogrVcj0YM7SSeam6Sh4KCA80KVsqbNjW6637HrJMCBtaQC8QSguSOWFs7Gm1pi85ih46dK2UypvV2KzjYYZ94hCPWHRSf5+ogUcjiCjWHIgZBLRALC8BdBgcBVgDQAGiRQw7wCxPC4/mF8LyNE7YRA2AgvrvWAgx8lZnWBC2xnWKQc9Bn+eB2XPDr4hGPj/iZ1AQFV2DbX49+R5leVo0cdMrQV9ozVr2NbuH3EDPmNbAI+V5HUADRAGiShRVs1JDyjREk1s/OEY+SrUDNZslaIUSkKECiB/2KMF0VFK0IFUObJL36caF+h1GM4WhOkkWtL7jcPzkNwr/iG7foolPX+B8+c6dNFUeMVQDSAGiSdvuRPtWFRgENsoq205BDsrm5YYPg/S5mjHHF7waly0MGZS/XSOm3FTKPGrK4mIwBS+sIg0bhF/0t3OIR+tQxM4TLydJETJ6qVixd6Q8I5DZIcmcgdefw</base64Binary><base64Binary>E6g7PQEAEAELwF0GTwBLANMAaI75xynOd15OjF3cmJ0YdXNzQIY6E1IrayYx80kXzeUWwf7+arCJE4DHq6KfBXTxibmwHS0moJoNiYyBPyEmozX24O2x5LF8+aRjC8BdBu8AQgDUAGg4xJmFdZnLGQ3SDAssUPo+DtpuIgG/hLQQreq+JOH9Hde9UxZXU9Vc+q33YDKnec9UbRCs3sjcG2++TEYqBy9wTE0A1QBoNla0DlDwb2IuVBW7wE/z92AW0DLeICJ1TUp+qEgOPu+FPMN40uGN+ac0Zxwmc6VHXMHO0DtIiY47NiKW00Z2YKT+/UiSBjm9tI9xBVQA1gBoio5rNd982XJXEBa7JiUZAIacgbZL8uSvVlgiruoDh9oztBm6K5EJXuJ1xxD4QgboM+tprhIft57C9/wqpoyefTZbpGYrGGG7CT4hHl2qnBdeF/6UGq0a</base64Binary><base64Binary>E6g7PQEAEAELwF0GvABaANcAaI3C/GsFyjWuvdUnSA2mg6+67bWW9VRiJCvtlOpFxY6qvTEeptvCDs7p2/S+IKDfgtb5fnRHjoYLu0bIKxZ/kBG1IBhkWs/++Zmeiaj+Tfh45Vr2jsCoZ7o9WgDYAGiqIZnVLzHwHP09WW6vECa/VedSOiznfTiadbiJkRlPIhVteqZTCw0UFtQ7kysbza3wOa0xCi32PLKRRTvfP4J5TxDcO8++LO4c1wq87aArcB+PxvRnq8TAiwvAXQYCAV8A2QBorFxZBFYvnGEDG2veaO/L5ui4620kvAJ1Q+CJRpDbA8P+1a5UpsutgPpG9+mIyKyzPH6ys7Toy9/xzjmbjhXhR6F7JPb4MLKqAKFL7wdHTVcxvRFupLNOtOUU7beptFcA2gBopfEsxoMpc0+QNQn6E0RAHoIhSidV1ouhDfq1Go365y3rHIkWLzSl3zkIBkj3nxQI7c4r5l8QtTIqRcTx0qNEQsJgj7y6rZTEB5x1VAgHE8Knc4ncVIFAANsAaDl0Ue8D8QyEvDG5AjFjolUfY5523aRb7sQQdIoPG2sq68OgU6BE+Sakg34hUl9OgDbT/ix4hFv275HBf+lC6AvAXQaSAEIA3ABoNjvNUrcN860Zhi1NsMb8J7TtktEdKzWLo1TfPpDsvBh1sV0IbV7v6ExgYRixjojoMDXzX5ZiW92WgECkYi7dLo5IAN0AaDCpp3nt42b4jr+8hVPdn6Tmzs5el8UqcPtoq/Y0QaeomKE2fOsAhMYejPjfp7AdmX4Di5o9BWYq7JQZnLI3xuvUKhIONHcRIMU2hg==</base64Binary><base64Binary>E6g7PQEAEAELwF0GIQFZAN4AaIlWP+WVBjADw27oFlRvyfn5kPItg+neXmLughHRiKfTT/tqOg/HbXet317FgSbVHzGUmH6/pE53XILykOElymulaWSODiy+BAlUjCcWz0/sG5loQk+zBdRdAN8AaKPxt41i27w695/IJiS10UCVf+hFcc/UvPwThE0NgiQdvt2mNta2L5U4jKN1Uh/MhYbLIhdZe6U+KLqvX6yekqse9EwqdKjMAKO42tYFM5pXwZjFVwKNYGt1Sq7sXwDgAGipq/N1qcCk+URxn5bOEQGcO5OTi8qCok3csacGJs2LtJGzSRtdnO96Nw8fHm4KtCA0Lxzf8mMNiEJMoXipBBYKdBM38yiGio/bmWhxOOCq6QiTOmAiIQGVHS6M/OwLC8BdBlwAWADhAGiso1wb/EsyMpZLk3k8qYrqzDVQM+/Ygv9uflDQ8aN1TsT4yXQQkHDlp32m5Sm83gfXv+/a1ckKI9LKZOVfhAkh4g4J1fo/FovghTKZxOVojva4wWsGIw7cq54F</base64Binary><base64Binary>E6g7PQEAEAELwF0GLgFWAOIAaKSXPQf7iNg8T+1NcFcYk0cfuyVuqgQAHl5Gk09CP7r4d1tgi+ci7BqaeZns5+O5Jo7KQ+i+hk+2s+Pe6sqQFZPr5kmcF3wKwV6eFOHH040BbUJ6/11GAOMAaDfUjOvSqtRDaus5ACYp1PXT8D23NsYAKwOeo4JnGIxtPiwJ0h/tRaUOLzC/yk47JI+nZjfbbwDz848QagpWnl2oI+hQZEEA5ABoNL+6LuWPKjTbyfNE8D7U6hNFuKqns1riS9Sx6aOmMODmY39Ltw4ChP6X9pZjUlHX0vgF3r1EAvARY20a9g4Q7kEA5QBoMpIzLSku0evrAxzUKU+vy0cCtwJuTX3Lh1HsNc+uMWl6E8zHBgp+wqbc2wVGJZolntjC41b7xWUm0mPbXRaxDgvAXQbyAEMA5gBoL3nv381bNGHwScQ0RDur9PnoWFEOZ8YYgyoGJOhJiUWPJ26LYeU7zt+lB1N4mDdVYiyBDO9pAAzoZgo68bAH0EN+SwDnAGgzxdk2Ryb60yNrCa3+AHLARZruCWoUboUFWwgbqqU1ACgp/yMFss4fgl6L0yBtDJmU/SFwYIPySedYnMGrgacmKEkwELm7ldfZUlgA6ABoi5hM69pFdYySf8DcQ6Rp+MCyybKPUnxxiojRKj2fTGE0lwPjY789Y8ytpxil8G76rmr6PymaVEIe0X6uljtIRQzDx4Bc6vSVLMXl1zzK+2hvmQSHjjQT71g/og==</base64Binary><base64Binary>E6g7PQEAEAELwF0GVwBTAOkAaI3DTTcZDI8dpGJOIj8pvTAzglpy/hDuCp5twsGfhoPthWw031jGDcMLPimSCd2eLcrPlPKa2ybTDczqjVAvFbVx8W56ikIGjYqM8RnEwOURlpoLwF0GBwFVAOoAaJCAeHisVyR5rDzjJjLOKsqPDybDZCijFuHa8eM5WgNtmFe88Wx3QicIwzAlToxeFptlwfnTXZ9jXy3tp+HE/2dAd9G7cEA5nBIMFx6UifB1Coand1MA6wBokO2GUGTA1Z0xfg2gSzgelkUUVCWABxRBjuiKe8jsuJh4RCx0hvUFN/8OiYQTQwEGSNtteS8STD25qJreVgnLX0yq3Q1aP+ErW2d4FjzHcGyJcFMA7ABokW3GOoLuS58uZYDIkI71efFO0WYlwAv31VzrYR/35em6ERdiboEQlkbhTEO8yjaYnyb1SL6iCIfVJAxc7uVoC85UNUbSsQpVen8BtR7jAkBG7JEqCoY=</base64Binary><base64Binary>E6g7PQEAEAELwF0GrwBTAO0AaJGfqKqOzNq1eDDMZ3kz/rXf4g/EZZhpUDthXFlYpQkEnrRF/uYjrMBU5IqJ1iPDtCXQQFbYLbtMzWYE+ge4q9gSgVNNhhMdxY2votWxsWTHexdUAO4AaJH9J4xszM+Y26UEYpe6s8ziV41zIhrfelJnXeq3Smss/M8xm1e7ra4oJx8WPi+Jm9UfcZHXrdyErYA//QN8zm/7iHSlzslwXaUML3sBY/7LRgUQC8BdBgEBVgDvAGiRijLGhaecufY/V4HOeoPrg8xNT1OMSZaSpCMU2X/Prz/EKJ25QyK8KI7TcxcQp5hdzHBn/uW238KgNg4fIrFzCcGuu7rgxJyFB1iDifLDNGCX/DyVTwDwAGiR/S51iyaYVPBPCDe7VVnzDEESbjpatnSPAyFA+s0KnSO8bR6ugQo5/PLxMx5H9khrolJybrYD6lfn6kGz3pElFgxQAm9NJrkO8wOlq6RQAPEAaJH9KPfde2/4yUW15Cfo41vKDtYoBueOkNq0SZut8u53sbzTQimpEPrPyfTYqTwjHatg+K/odV3SgPz0BGP23x1jAMBhn5DLM67iNJ37VtALwF0G/ABLAPIAaJFtxuGux4LKga6pjzz0FRMF/2h3RRGpEgjIVwVQtukMHUNoJrCVBamnuB+6M4J438XTUsfkx4PhyQRN0AEnATf2E2XKYp+Wj6tNUQDzAGiQ3ImZR93qR5o7CzLMd+YZrcwbDXWiNY2ev4QO14qGbJqtgCefSKsqW6n4UE2vvBQcUePsQAXVlY8oAuYe6C4xkUAgX0UEsh8531VEW+K4AlQA9ABokNyVlhakz2RGDZRL0yqlo0Cd5pqU+HrUW5bsBJjcK0cTpg+FtmzZlMIEhvBWKjGUjcmtlfPMIAU27nt+O+GRivJcWkGQxp3ZC4eMFZ2+YTS9zQw1EV9v</base64Binary><base64Binary>E6g7PQEAEAELwF0GsgBYAPUAaJDclSFLsC0xYh9v/6fm9jLZuM/pu7UGTOdfrgU4m8Oo/R0msWoZia1Im57FYyNnCJ/1Qox6pq98EXVLcQANGzp4oLmS07/NrSAOStOCsflVZFJsq1V6F1IA9gBokRisg6ZBBgPVfCk4we5azBY3RGnzXgAByUm0F4SatnxMOfNmmQNODqXc5uAElgStA2/CauzpnnhfTiRe1DO/WsQE5VkpmMueKyyFit2GJhsQC8BdBvsAVQD3AGiRgDjD1yHiT9b90FmrPjOGTUWwvsTuI0eYDdEhA7JfTXATCj1C3WvpO2xdtq6aI2i5SwcH3GmRrPea9kCI87tFfOoBwVvlTpTB60cb/Z8AYlBcJTFOAPgAaJJXJqchYAPzt2Toc14rqsaGiccLgYsWxfJgIHYVnidaS+Cp0N9bKbMagLzMlHDZinuzXgX1SW+eRQ4vfAWPAd6KH6F+W2zo4TDFlAJyTAD5AGiPiL7drK+VfbQwhQ2O276KEdlN2SEDQ7qAYFARaNMTHA1fWk6BSkElctPtCNDvALl7zbiBB/WYDraZF7DMlqEJ60aPDkxHDabLHhwBUvBw</base64Binary><base64Binary>E6g7PQEAEAELwF0GSwBHAPoAaDc4JrTTNHi9XerwYMFHrdxoI4k94kKZrfIVhDjZ++2P4PLU+ziGw4GO3pcDzVZXz0s30jjUaOhFrY5TYBJItOXb+s4UsDILwF0GcwFSAPsAaIF0Yb1hKFC3hE2qO57s5GUNUG4GSRkHE60eY5xs/nQXuw2ZoiCSgfOG0fIAthwINwyukneW8uatlWCfjlF/VOp9rPQVeirgnvlGWecxiPCM31cA/ABok1AoSlPOpmeNUh2V6VdNbbvIe3a7x7Y630iEQdpi0Xh/qyamO4U3GqwfKU3Zg0wOoOW4TUFrgcDIaT3LwdT7z44qqKjiQRgTmdBWCNUDHXn+yylsb09eAP0AaJSM6SaxRdRGNH/1V0olUV7btyStKyLOuiYn7lTA241plVRGRnmndkuOXhN4Z3CcPQK1DKGPIOiAc61onQblmpVH1CqvAWvLXp1dQSVu3Gq9jucFsCRr4rSzVAmVm1wA/gBouPidKiLUqfKlWr4S2MWI24Z8W8vwtIbVkGooXl7/57F0LPonBSPSWfIOkgYbjwp+r4EcQ81e6EmxOn/VlW9fahI6cD4izNSHJ+NxiMPBZ6SEb0XZU1xsKTVaoRdSKyU=</base64Binary><base64Binary>E6g7PQEAEAELwF0GWQBVAP8AaLbLoqH69YFkDGDfOkZ3fwoRG9/640cSD5IRmMJnI54Deqpgkcn8c02UlA/efgTRgjQOv5s0GCtcK61saugFamqn4cvs0H/dXSTaQMT/9V/lz88TogvAXQYIAVgAAAFoteSSF6g1d0eaM1FkFLEKiqKUIviBFqJUVvgFOGlPWUzE2e+0QgdcM+1mVeodyVIUoPAWfx1NIMD3LkpKH9CZlROZEBQsj6OMKPGJt99PTsbuWb7DHbpRUQABAWiUCGqsO8p6IopI8R+IhRu/WiAQzhSumwS2lAGGzsrbU/BZisMSzb6XWQr4JTfD11o8xU9T1tAWLFLUGfA7LM1g5hZjnl8U0ipGDS+kj8XUNFMAAgFolC+vK1vU43JOABVKZsyLBId439cIQ3wVpD9uo+IMBxeLF3t2j1gXfut7H1MG81AoOq7SAdG1Tn3iA5jO0pSPKvOzWUkkOghbiQU1EO5pC948acS7MfQ=</base64Binary><base64Binary>E6g7PQEAEAELwF0GAAFRAAMBaJRnAEyB55DjgDvSLq8oqRwYB33kdKGvHMhXnOaOnAEiGD6Ylpdggt1V4UTMe0z542JWxgEfOiQj1v4UxXrGEPuFHTZwUDou4fpMmwJxTPZqVAAEAWiUEdn2WO+bMrGyJ4hvzkxzCdSsZ5c6thElEymtm5j8I2AHnu8vqBifbMNVOuuanjpxKsbvHXPc6tYKVcZqgT2QMMSdWAQt/tYIPmUTH6bjGmV9S08ABQFolAikILa/Qj4Jpz/gIzEnH3YxqOOy5FKSRmeh0p+gxXaIZ3/wwW75O3Xwy39BQU5tth1X7WfzefgJ+ICQXXzT9urccYAOeIFlSC41bdiVC8BdBq0AUAAGAWiUDvA+473332MyQk7p1P07p7QfhmGjsWkYYCUF0rMFx//6J31eLVIW/7rz9Xv4/Kh24cYwiJK0HOe75rJwagAwqVriWhfXMkcXz+ZVmGMpVQAHAWiVDP66NpFq10GDLTaQl+OvA9XTfOVnEZeRNAaOVLBW6exS+7Z+FzNyjFz9AgO2rF9qlk/BjRzoqDCOlMlFAIrvJDGbJnubUzmbWb2FJ9SXvIYStfzx16BW</base64Binary><base64Binary>E6g7PQEAEAELwF0GDAFXAAgBaLsNLDzqYTmFjS/FxwHkoei1xSpUwrNSE9wHZrh1o6/nn1z0lDoo8a4yyW5btTpKYbUBb084IzGU7sjCmtURF8hpWMiO2MvrhM6AphUv+KplMNTlgkurVQAJAWi4U5NJzitrCvWyjma7C4JfraV7SC+1f5LNryPjtM3mCv6uzrQHL1HUcLv9yRgekIbYxaQlUe4nnmVG1m6sXrEW9KAPl0cosAAXj+t7X1MNt5pyMWlUAAoBaLTF0g/CtlYtrOM9rD9VnV3xAByku1qfIXHZonycSPeVVMSO2sKNIXNI7eTLpfHLcpYB1qPIXXyQz44ItjLXgXbrq+XpK1ODQp7vhi7xfeWCbR9tC8BdBvIASwALAWiR/egplTiPenHG0LbBRTc0XkDDGMwf2Oa/AUpXvb1MvbJzd72APAV/ISD5lFHHaROBIGDxxa9lqyDkgMFCOU7l4v6ZOqWeZiz0u1AADAFokMkaD9smt6cJB92anYs7ooBPpiW03lGkhi2cyfWtCPD0hAh2ZOh+hWHB1KTQqmT6hxobHLwIsLarwqs3RIA86ubW4TDZGYJ9rHrUF6m5QksADQFoj63KCkFrdYSUUm5y0pwjZjROyKseq3zQYseieQuffKbcKlNefrmfD+Lf5SCR6tWWRP0n6CepRL52vh2mYDIreBpaco+mETrihe8LwF0GswBWAA4BaI9xiLNT83LV26OUJ5K11E3+EaCqXwHc3B/jFAveP77jKDpzHjsriLAVq/k4CtAtI54VIkOMyyuL+cI45tmi4pVPQtQMsEXlt1b1t/VO0LEBrDw+HFpVAA8BaJOcOPQHdIXI1OXm6EYC9hXk+OAfjKmd/yQW/N7UKZTYyQbARCkp5y65aJcajuDMZbgsDp2se1MBC0WD/YHpPh+ERsw90ahBPKGVxVLL1E4b1um5j2ga3pU=</base64Binary><base64Binary>E6g7PQEAEAELwF0GDQFVABABaLPRjcdnbh6T2YGF8C0vV2HlrDSkJzYLRj1jJa7b/gFFgtFPw0L9EBKp8wB3gh3NnDNs+DqTnstf48J6Wsofc9zmPqiJxfi2DuIq4jRKKRhLSiDCclQAEQFos2EbXU7tk9JUpfgUZlAy+WLHoQb40+umfhoL3hwXOfp3J86yhf1ER/yJ9f28nDD/H+XdN2c74kZYnBEzWnALmzywGiwFV4TQBk+huShHy9iczzRYABIBaK4QzQ2aL7JridSS5cgqnvAteIdptgfWtF0JONceBoZWvLcx4knPpvl7OEq6BOxbW5NHWgswSPh+3p/E6sPwRWIj64RsKcmxoka12r8/BQjHEw5ITQ8vxwvAXQZcAFgAEwFoq1Obb/GFuovkYhIius3MRBT0k0TaJENviJiw0vhDbX6BgUTlZqGoJmufhMySaLUs4IC+V+WKbVqPJiDvzFHr57hwIXIeRKJa6ZhSy+4p50D4yDmwLGqF6LSzvg==</base64Binary><base64Binary>E6g7PQEAEAELwF0GlgBMABQBaJAmB+W5qaj/+WnJa2cjWovn06/34XYhx4fh3iuHJFsxtQQ0DUOh7D8qNFt/frmyfWd/PcMhuFqCHcN8us52SMKQfiNgb1zUyyUEPUIAFQFoOxTfzHvZnS5SisdPLeU5TdCwL6OZLWbtdVVHoRf8GVmmA72QpQU973PETUa40Tht2ukAcmcbO4bDfnXqXHdK91ILwF0G7ABBABYBaDf1WHfQpbOXmIIjwb3EjR9s6tfJN3f32PBEEyYmZVPLhb48lY41YUR72RJ86pILef71QBGfxt3pS3WndLH4j8ZNABcBaDaZ3ozqsWGZt5Eo9M1wFk2fNEDojtg3/7MXrOr1cUS2bi09k4N9a3xk/hVz5OkSb27lvx3M8V1osVlG1XPSxjDIZeS3JTmLwl/FPfpSABgBaIuC/C8JFvA/120DeErV+XTRvL7DGXOZACa7cOhjc+0031cal/RD/UWplYVtM8/x+1j6HF1bmH2mHb7Fa3BJYcfD4F2DM/S6Yp3/7IYlKBo8HIo5VZI=</base64Binary><base64Binary>E6g7PQEAEAELwF0GEQFdABkBaKvtMTBS8HRFojPV8bmugpQinSEq/nnWNNdgdfPOG80rWypHHPdqJf9eXeIopFvh0WLCdbQHMgWBhrU6XnB6pWKWotZK5UvkzXpXa4bE67DqcBqs/ZyYkRfrt/ZWVwAaAWiwMejt+16B/8qnbUvDlpbePwvvtg3pDTxTwn5qSrgUGs07S/BcVxnVqlZpux9harfYyB/gv1vaC4RuW1FQ/mG7YkkIqwh0/QITd2fIi/gtYVe2VCkv8VEAGwForp+Pc04YWhfGc/+pQJuoGAAgGQVloRx26lPsYiKzHApMrvNYp6Ka3/palFnwOmRMATQ6ozAdyMKSWHvIFnAF9BbQGgodGQeA/WXxEDyIuMcLwF0GsgBUABwBaKzv6hGRI1C+Qw+VbqgNoLnHEWIO3jZjs7ykdHMylHXXWux5LxGE+k9A50jWWx1kjKHPNwvWNgmIiFzWzZYf8r9q9ZXYlAfKGQK6sKu2mA99syPsVgAdAWissCpknPrSo0tSPL0MguZAKqc+td6ElvPu3D+ZWqhu+H8P+mKA/xA3vFx6ZZqeqMuEsRE/sG11sRXiSml3L53QJXtMLdnxsFh5/P8qr6DZ5ju11/wu+4hy4w==</base64Binary><base64Binary>E6g7PQEAEAELwF0G9ABUAB4BaKeKDYYZ/hSf9mSREJtpiHdW3C8LbKPhKGmcWZm748F49cdEgCv+koZyjKZXB/TtBhy9O+rBwRcar73Ydv6kZaVTovLtpOPZb4kvhfxe8fZDZwCRUgAfAWikpoDTGj8qh09DiJja2A5qRhz7N7p60NBhv1XfKbTDd/DvX7jLsGMBkmOEfwniV2rDmEHvzjUa6IHXScPDOQvoivx96oLnIBRIA9nNzCnqNAhCACABaDcHHjlec3K8/kE9lpqLLHF9SxyUhiJJZUydZdVGl4EYRMrSNJqYG98rUySyJv9Q6i5aKlBpkBI340KLG4zZXnDlC8BdBkYAQgAhAWg0+a2MMZDoRd1JUXhOONsyqyrXRpahSSHhmtaSNKyDr3YulD9PYEZqmceQiHvggNyJXTMz/aBNnwu6omGLasWs4NlW6zc=</base64Binary><base64Binary>E6g7PQEAEAELwF0GVgFDACIBaDTd4i24GRaXTqAD4L+V5wkM0MtSDd/LNlJhhIaGenOsqJIgge26LHixarDy70snGNLRHk7dwSYvVsROXk7WQIiBZ0gAIwFoM3jK9mEYaMojEcg57gl+ripklRKKaY5T2sWsO9sfrzBBZ4cwSjnpZrN0oKWxsztZzsAU9DSatsiytZqC+jw/YXXZLs03dJpgACQBaKRaXIgAUQsXVrsy7opHZvD4lcEuBGv6soiCKKChI2Zy9yGvpk9BzAarv7QW9p9qhof5XUCjeKo5X/VCPg6MIWgBVu0gVdhq8hhp+5/3YhShscGKDYKJuVv5dR33LO9tWwAlAWirVAzLwSyD0Tv1vMxaTz/R8MepiV0et/aAlKIxltghbTgziamqVEeCM72Fri6AL+eUBIasOOylccmuvT64jBfwgBNd+luQp+id7Y0F2MZiaRtZmUjeOt+G5aALwF0GHAFaACYBaKtEz5Er65HkobZhDBDhZBfY3Uru9pTztPprV5O1V6eRvmokkuyJe1R2qE9Q7ASj7yaCxQ7cRVo6bmKRYZM6CLtfOtgHvX1WV0Uo4YTtGLOAKkbMng5GJkuuXgAnAWiquqdbkezTmkh04spC8mmLMr8sPAPdhOBBrnZy0K1PD3eLGY9tchYgjnLJeH3I3MNbzwyo9TT6adwbvstg04oZ52p4xrSNhV/usQ+m48Hnv4za30OKibvGeisa8mNYACgBaKmZhsVKzNHJOsczccUUcHXxVZ3Fa5yKN+tWsk+t2dqzqDtH3los3PuzUh6LcMAleW4cXbYuh/OBqgdcQ9Cilcc9uPZOr8YVErx4KbWth4JKbxgD2+LzxXzp1ZI=</base64Binary><base64Binary>E6g7PQEAEAELwF0G9ABRACkBaKmZHwDBMytoPqajrXzVQjDnyV1gGslYkkEPgAsIfELlO8plFG7fhb6fLaq2fc13PQ63GuxpO6Z864XyQaIRX67chD2yokFsKvdQduC8k1UbUwAqAWinQPFzUrgAzQKIM1zEFpiNpdnEoqddsp0/P/q3WJX8GV4Tpyd1c03RYIC3n98ozsMMavfTzb7RHxpX910vaVtiZVL+eilz9BSFclinEv4d5iXoRAArAWg5cuunSTHv/PYXLb8gO1l7xYhI3KI9QgSMPzn9gaV80MBuIXHS4QrFjeGNdkFor1nypzwMYXzDF9d4tm95pgSL0QNRC8BdBkYAQgAsAWg0UlfIqs8sdl5GYU3U89kSU1razBjFXgel2bWDVhjh4JFc6KKhjZxoPUNTKGuelJpCa1D/TiX1eON1c46QS//xjgvAXQaVAEMALQFoMUDof3Qxxanqk+p82oS3ZTZK5ZVfkTCbIQbjj4FQiN/bhG9p++cYTvlAUYFTmQRMHOf0T7BW6/MciTaMrHwCYiGnSgAuAWgvdNaNwnt/FVPAkW+xU30Bg1dhm1mAD8AC8EzLda5h+VH6DHmJuw0ifjgXPIJEekpeScEljSjhuViGiBBiA5v+owsAsv+qTdaPuaNMpA==</base64Binary><base64Binary>E6g7PQEAEAELwF0GDQFNAC8BaDFdr+KfbKOGhabF+edqCkQ4sL5YwhQ350Ne4Pw59Z5Aa/TyHlEanNa07RKgU2aTNCGEJPEQzAd7LSurn7nPfXjRSgtei0be1p3q5x5UADABaIoQHLGQwBinb5LhDfHl/h3QM+ErrpkjDFkU0FeoI+IYd+62jrt8OZ8/IpploCMmH8LYfl9c1IcXN2S3b1U6k7VVy43VW2cScz2m4cnrGGmBdvCJYAAxAWinaiKFxxBGyLYzGVFU8U+wJ3Wm6tZxqfeCL8gTHLQ/Efh+kBnTNn2TY8d0F817F5w3caddLfVE7IGidL5e79+te4DVQegnkQPzFp5tlJR+rQQJK2AzZnhMEh7Xsz1UaAvAXQYMAVgAMgFosAvBSRjW3t2YR5NPbVnvD+BWjSkfLpWeuozPUP7Wb9Dd8v4a2cVozVXB38nzGef6ASbwVzD6acTqpc6uLl/CJ4bfXqJ9JYuHLWbiHd/OOgH/kcr7EQpxVAAzAWiuY2L8U3MmlJ9VdWurqEU+E5U9XRBWULqoWEi6GSEKyHecTE9qwdE8w1ULJPR4A+pmZdGSBdRk8I6GBPLQ+xjnlmqlme8MtDiRNGEte7pwM4iXElQANAForGaqd7z0JXhPg4EUkK2sRT0urCqinV0EtZwPpDczVYKLqyPRi3p34yM6OhHdutDDzn8vEm1Y/GzKipD0siA/MLl8lVof8vsnZvPa9nKtWx4Juj2EuW01</base64Binary><base64Binary>E6g7PQEAEAELwF0GBgFVADUBaKeLauVAjKxG+xK81qHIQkhDGmlPMkFVLq1uy8orM9J4q5jg2feOjZTDjtl0nXYnkZxxlwCxsEyWOlm/ajNalFf/jjIhSiYR4y16tljisZRKo5XqjVgANgFopNwPfxp0RgrRT7MxC26HJ1Goz00E+AMNWVOpn/0xckN5M/1soJgGkyvwO3EPfa6K4BO7+aHCIH8fovw2pIFB/EWmdALjovAFPBjSzZ1Bct2y/r/fYPmVTQA3AWiMTUXLzojatkMFg20Douv/viiRMnoqCHoabN1nbV5eXOWkIM4xiq0eymHRYwxQaQNvLe0G8So6JIVEJz+3MOHvQF5jaWLQKaUAUugCC8BdBpAAQQA4AWg0aBFhCZhLZeapol2rRZ7CkK3KFmiTrdM+HlcPHfRb08zf+IZQzr0A/Hl/XJd2Gs0BHYdzTf/ggbc7CeX/IfNfRwA5AWguBoO1kf0qibIQyvA7dBkYPMu0iQ6VSo3SZT2lLLmxWAxlKzOtZPojEFGXNM5EY9y3cAFyNbLcoNtb9mj5gHUryRLfELb47oh5jw==</base64Binary><base64Binary>E6g7PQEAEAELwF0GSABEADoBaC9BSrldFjZld5bmmt5hsgxA3iAIkHwdIQiRQyAWMz6N3xHwY5jY5Lr0uQEUXdokNTI2368zywzJ4t8KPfUVM/LQNNgLwF0GRQFCADsBaC8/cccHmRqgtVxtLcCPD7GNSAsJ8kPTyODPozn4QznTaXXcVrjOmJvRCz4izdx+HKLSUgxVy+tU1YgNQUlkUu2/RAA8AWgvgYhTyJDStEOs2lbyCMlbK63uWAsxPXM1yFxTzjPXVQl5l87V0WDqyhLB5CwH84XTMqplKfmAtuTr9zUkZaVIwPVIWQA9AWiNv0Y++nn0M159eqaipI3q1RdleZwE7RQUGvBwM3GwYfim6XT2wiS3D45vu0TDIVshoIMXJ0a8aML69XlfgDCfGBDIe/AYEmWcaW1wzlwwMZQqXAr3fSs1VgA+AWiO2Dvc2dw55xylqSpmZlQKTOF0DTmZT+a92ZHc8fZ31WgzF49QPN1hlcJ4tqp+JWnOPD16OrzcS3veepIzvf/TqgxU2uCGUaDIuKDWAiiAerGrpg8CyM6Atg==</base64Binary><base64Binary>E6g7PQEAEAELwF0GGQFTAD8BaI/zL56C8em9gUhzxZ3dCqZHjUMWUl9+wrhdl0acdwGvZx5Y/g7vzJgLq7St4V2JAyLSbRScQbe2HIs0UjOAz2u4ej5xbKoUiXHWgQnx8FLQsoReAEABaKmHYEr/b11azlG0yvhRPGK6FpAChph4IR1bFGvK4+JRGtOI5/jOJrRkOCpj3WquDFdi3CzrR8ORG6YWO1W5DBDCyJgR/fINXBN6FgA3mFvsYLSyfPtkVOoWpYoFGlwAQQFoqV1vtft0Sz+qm+FareFIsekyuQrt7IdNlXlD8lJoJVsNoNKIOm23QSIwBjTtKEO6Qlo0ZRk6ZZYdmgpeCNgjvKLpsDyPOAfvnkl58QrtDqWYIGwjDYV0PimQ6wvAXQZbAFcAQgFopNP2FKU4xG/YZjxQ4bO5asMPdYBW55mU4mQolm9e6u687x/9Ql6puEdvadT0bmz4XC95Sr4SXM+D4qpEI42t/WA58gSs4XIxZ3/eYXpmh5vEyPvQQksF6O6a</base64Binary><base64Binary>E6g7PQEAEAELwF0G1QBFAEMBaDfJKqQX8Ljx5wk11ri3H3eWpzPwiVgowDyDyNZUX29u2Uq7ekLDAJZcgSF0p465CWbgUFpWNaPYZFLW3gYaMHOtqsYOQgBEAWgy0Bhr98o/XJ0cS5mWXIu85LdtCFK26l4y2pEoeT9H6QvUUrCXJHEP7qtIeuDEyuvIrOAVQYvrSXXfL0VWEG/9WkIARQFoMJ7iHZbfhfd52JhWumR1TYZADwfssv1QVYdFmkKL3wPzGStYOf9w0eiP5Q5M+4V6q+rRE/CYi4FY7USQxwLRrMsLwF0GqABLAEYBaCsB/VWukkecXvdx5jJWAc5VSaHtPtY7sQKwyY9+ZBV4IElfmGU8JNNjh7HH+djKAt32ktW+um1b2SU3bdRSzx7I51Xs8+kvtojNVQBHAWiBez6Oy41ZnSar6wjvON/5/Sj2viF54/hRlw+xEWbbbpGE9tLJlDXCXZprlYeRXPkNPO8HrXd7PLj94Cg5AGQ0iyM9xzg6TvovtfpJEaP/2HSK6I9urK4Z</base64Binary><base64Binary>E6g7PQEAEAELwF0GDgFRAEgBaI6TifgKjuLQOaO9ECmWr3pMDPBQObDnnYHYogK+kIXbWXipBTeg6Jmwz2k35mHGaekUNEcQlnlUJyk7DP2b/WzRLB5oBxGe7jH/29WBYVgaWwBJAWiOTcLV+F0Q/GmBLKGVEYZQlsdepp0VXG3vX4mRpSccnkGPVqjxB5i46m/d6wojHVsBOtfMkAH4pxbwnBYYkLk3123nMI98L/46Vs11LCOV38jrxpaB/BBL1ylWAEoBaJDsjqpNvJG9R/eKyh9KYcBtNhS8D3OlPfy4BVQTeHt5/PzOhFcs1lqMPmAxfjiDKDd55yqDwPoLC/BgXi6ngLGAaVO+TokipmAf7yhqKS4m/tajDDMLwF0G/QBVAEsBaKdDSEHdncb5gC7M9mvvDOIIYNXZetv6/oRVKr7UKU0cNIIccW/t2vdWwxm+Ybm5OCUQVKlxSaoHMF1OyLgoawcZsIqyzu7dt2zEcHjjQ7+8HJjK8VMATAFovg6QQAiANi+bBuu6OtcXVNjYupvozS+gJPGM57kUE9nCYZrQWIkVkWGKxvpCkTPF2w2lNC8tyGc7e6sRhtl1+lYpdZ6gxdd3sMUnOWj5penXlUkATQFoMouCb1rJRQ5hPJlLyruNml8ADX8zoSxDz0tPhY0+dZYr8EgvOm75sNXjQcQgf2LqCS26Plp90fvq3pOmwmc4CwSCMkRbzoaTC8BdBpAARgBOAWgxQJOzbEevAaQtSyi+TQhNI8mXKksrAj1U8Xstxrocutwgu/xWYJfKE7vvf1v1yM1vvY36rJOHp8ndZkRI4D2lmywPoo5CAE8BaDEqEQDKXRWCloWZ0aJyoQYXQ5/hP7mt4oSpvSGk6gdZhUZjcNQ51CdIGm97H6k0jnpCq5gkRH3/vFTnAcDgdOiXc0MCAQ==</base64Binary><base64Binary>E6g7PQEAEAELwF0G+gBPAFABaIGlojwyCmuH/Ez/vTz3uNi19sO1zh31O7FgC5WCT+EWxuTfavIuiwovhqm7+Au3EXBMQwhN4ZeZo5ugbH+Tr9k/XC1q4HnBZMjPWT5uMkgAUQFoL3Ui+OnebAnHzmXWjMMfgoASJfVg9Rnuf26MHZ6eIL9wh2X2RL8kkJqNqWFH7IBwAqpEGFI3kkZijE1aaMZCpIXEgIMcWuJXAFIBaIFzrt4nsW8zz/q9yvlEXmwZ43c75mWI7jYM8k3nYFuSJbPit0l90qbtYZLS8RkNIK/25LrMeA8gceaR6KGdRlHq7spcvOUMVRE2b8vDnTS8EwoaSkV8C8BdBmYAYgBTAWikPiAvpS0XXoWgB2ZmZZOohwJcOlho0NtsujYvLTSdNH5ibXSTueBB/6Mk410BSX13JfgdUuHOrZSfa7CdRfHt9t5TzKhYLmhHMgYXL6qyOZQjdESsiF09uu2oyA+fU0hfNaZg1Q==</base64Binary><base64Binary>E6g7PQEAEAELwF0GQgFTAFQBaJBq0riVhNMKZ513vcXM0KAMcBJqCLnCUcIoq250sVysDet+XHlXOC2AbhCkk1oLi+LUWccWo0C2ushhiHC70QCQOIcqQPMZHeWcwrg/RsLbvxhPAFUBaJAh78T0/kf76YVGhne2ZJ4ZFA/ordNzBq2N34SAJcVxJ0ZI0/ZKLcRSHKIXnmuzeuDbjZfX8kIcAfTRKqBRGmYClqWHvPz5/WwBhd+M3E0AVgFojNitVHmxpwyet9OPMQlBtOGALYJDoKqXNPFcC9xMYaRM+1RG/jB/WkD0XqnZlQs4kjr2pr8DPCEVne+3NhxcpeV73oX5N5Pt7NKprEMAVwFoNjKR7yl8v1ONvOjXIyXaKtKxGI08YYv1JgVNfkv4OVgVmUr/3wh8n1wOmFYA+FqfhkLeJymCQsJ54LLpI+Nf9EVsC8BdBlgAVABYAWiI10MoweWKcapmXComCkcV0JUbRs6npCh+qA9y5EPHFAeJw1Z4y0ofs8MIALFMY3XA3335ySjFKavmEe5AD9/NwropOo/KLhc4pEhktWgvZ/zNIfyYKiY=</base64Binary><base64Binary>E6g7PQEAEAELwF0G+gBSAFkBaIEwdDIC0XckWniHy5StdU+N7TuAfOFyBGin8HJD/u6bQJxElMeUjtfTNvDs5U8gLqaPCPbIqHM3zzZuN73Oi0fT6z9mUpbw0ELW+wLVDq6TDk8AWgFoid+G8tFhZm6xcv3Ehlek/NdGuop1Bx6NbVAfLb+JOCw+/xaeCvMzkv6awkgxuamy/uQGm9awoglvoWxv9toPNfFZNd7x9xVnBfrzNKsvTQBbAWiJHgF7SgGDytvyUaYk12eHPbURYZNmaBkMn+bDGTIJxNDraPGCi1EkH5Jg74bRM/Meo0vDpIXzkcqajUKh1MjCRIytZSbNw4wDCAKZC8BdBqQASgBcAWguAll3OEnJY5S/1gxd3ItbvOH0cceElPIWcoBjkhQDdFN95/CbqP1tMqRiHu2XQgIY4+8++ouNcrEQrHDRTsEpHo7IJZ8HC44JUgBdAWiA0EYMyZzCW+xPZlC5T6u+wcYAZc/DWpJhh6pdIvKKdlQboWZE92SU/8CVWoAwwNQgly7MGfl9swOIpUriGJsYRbbQBv8TREykPEN1ZUD9QQknV85r</base64Binary><base64Binary>E6g7PQEAEAELwF0GCAFfAF4BaI3Lccwp6GaoLdmI5pXCJzJiBFBV3Kj0/3z0K6eniDfvLoSOBBDjUc2uTjVF1bC9AeCDD+ncPoMqU+tK466l9giASZ1/OfPhcJ5Px+uD82RKtWSh5myMhNOA7eKq8dVPAF8BaJAhgDDWgBdXKP/OMIoQUo7/4ouJ0iuvEBzbAgrepIGcGnNCrCQEHe4eTZmfgb/lts+qc+u7UttiZIp8hxFbRS5olIDJPyoh1u844EOIzk4AYAFoi7uojzhRTXyaLfcEM9uXU/I++lroZmMW9HUSEplsMV5Hxnc/gClFrB9awOUrd/uSX+FxuFCFlaszmo2Xzc9NHWwkTQeP8y6chY7wStYLwF0G3gBDAGEBaDCkjk+6gqMUNWX9YipYXJoQwmRApo+/60DAjd7o3Bu3rwAQ8qM4qi0An1aFe/Ed1KF6ZwNesCPodDhRx0wpNLVc6UUAYgFoLpfwEV/owz9YBN8R3MAQuSeo61Ri1qsxByF0xIhDPz3vNW+qlwWVJu1oJngc+eWaptwaVrTOS7ygx41MiZ9M09gROs5KAGMBaCtmtTe82fDOooyXoIRufmY+Ss+Hl1PLyjgTx84l4aiXs/WJ+vnbsPkzeJo6TuNFj5W5RUgTLSR4immI4lEKGuCBoChWhG/7GZwLwF0G/QBNAGQBaCr2f22nlz1OBeBKAjynYy+CE1/jSsqjcLcpcqDTLBK6lrTUjWz2iYKgYN2OsMDg2N1l5UI8XInL+rMU+gIxTH/JggsrROn9q8MAn+FSAGUBaCrqX83e4TnfanNTTGyBub3GLMV1P+eGUrJIJ1Ylpho6K9jqkRruDQ5VhsYoZeMjvzaK4RK4GToXcReTtflh7sXCZ0lrNSKilP5Kjw+yl2OINVIAZgFoK2a5irqEqomno1lopSH2Vmn1SgzMAg00CEV1ACnaomN8/jkgezw8130FgJsVfFnKQZMh7/+1D/xqvxACMG+MaPJ2jYcG8DYkqjgJqXYTZSzm7r+SXg==</base64Binary><base64Binary>E6g7PQEAEAELwF0GsgBLAGcBaCtvuG7qAFoFVXZWvKtiKDz2rBCgvfgxkmennpklTS6ljbSkQVgKoI3YPIfDv6D94jKeM2Z7v3C1YWqXu+B5nx42b5EqFSAydzICXwBoAWiA0x5M91iXp2Gzhuq4hXyC/5KCeGi197Gd0mASiY8B0iokfVTCbTHPxOA7sccq9ha1MsYTpkcrMCKHXZ4VE5MNUiDAFWlp6W7KdQj4KctIUeUXZrLZI+LFmZOM9ux8C8BdBl4AWgBpAWiPbo4WY1N8s3SWASeccXqmDWDWPTuX2EhwlEmx0LSdZOfHnxNM+vryoecBEx3YikH3FDED794OUWeLmIpkkGCERc1XqsK3m+7JBlqozHdceWn/u9/4ECCcwf00L18=</base64Binary><base64Binary>E6g7PQEAEAELwF0G8ABTAGoBaI+LsJbIstK5pTxmCuvLBqM8/hzI4JBu4fm+0sn2iLzpyJccwNeIuK1Jd1vkxu2eXOQMmsnqqXYV6DBdpEk1Jojnlq+pnd0w+NYfZQiD26H1wU9NAGsBaIqtsW5tticC4tuOrTWDpXqsYTB2SQQNOh4w5VIXHzLmcE1Xs3BtWk6RH94nLCWzpXosDzeOleaI5tcMivQixlqRV9YQYoD1uN65rBBEAGwBaDEMGsiZLEI6NKYyR2/Xk0l2juVXKOUlFKMBhVxdF8yxOli4FKQxTW57UxAlOcEXe+t9wx8ydaGxLJaPebUZPbeFwqoLwF0GPQFBAG0BaC8/ccCU2nnYJzMPs1avozCw3aTy+mP47yqtK8kcaH7VD9NbqT9K7Id0IrzDSsAAc5YinAMs5X/EOnVPb/Gf9VNSAG4BaIjFvcwQrqeQ/tNqLDa7CMcikpd128xfBm2tpzS/6OBcmtobIs/kjTC31/0M1fwvJ+R5xzOJDLejNvZm7lujdNvAU6U+6dBX68Fqg9O2Vf+f91EAbwFoiOPSSTmomSsKho1QW3rvDuwS82AtKdHi0pRl1HXIh1Z8ZyBWk4+ED03XD1z6RiX08mGrJBxx/LGVa9E9dnB7SEt9HZopx1aY/KyVufj3pDtJAHABaCuNpFSj3jnIWzM3oZu6tL1DD1Rjzou+uxwSgq/XZ4hnTsIYp1QVCHhAQAneapmdthG2jS1/c8XrpYLP+ZACJRlFMokudRCCkBgF4cQ=</base64Binary><base64Binary>E6g7PQEAEAELwF0GTQBJAHEBaCmXG7333IsiO0DJgGBzCzAkVZBjcsflN3oxHDM8lPz8801LYOda97SdRk83bECjhw9dT4XbmRdjQTeP8OuZCXf1T5S3JY0gmAvAXQYLAVcAcgFoiCE6orh5s4b2ch5xiMe2Su1EekEkL5Ng43jfiV2Nf+nKi1+SMfkd0YI3dhWHD1BjxKwWI6qVhFysbxaC09a8MMd2kyOtRXNA0BbS+DFEqKgdbuPCcRZXAHMBaJDIMw6fWTV4TfPzEC2DU7QnywGOZiTFmKM5hsQnXNVZEa2Yh/JXbD0Tqq0RkJl2arkjiD+lyWbWULRc2ND0q5PEKzvPfZeXcQ1ruU9IyMBn0t1gU7LDUQB0AWiMRmzUM5UJ1YeP/kXdy3BdsCFcZQgHK7Q3qndAfwiRt7jApIe1BRQK/FwDWdJ3IRW1mJVpa/W6JjYA5G4LLpSzYghk+6IjjXe0HThJOeaRrfSXToc=</base64Binary><base64Binary>E6g7PQEAEAELwF0GngBLAHUBaDDnZLs1mScxREjPmUZYpPb5MXehGD3Y8UcQCV1GzhA8pGPFEvdAnpQG9lavNrZsBs/vMMJ8qPg3S+5Hc0FuC42UAKotX448xpvOSwB2AWgtb12HnDpdkxNHbIi0VAW/52f+GQ2/ZpiiBZFTs0meF/rZcOKF2UoSGMbZqPA4V6Nsz4jldFG4UGWbH/P29pU0gVR96z8jYFmqGwvAXQbbAEgAdwFoLVavgwxn8Dtj3PDtb4OHNaeWcQBAzSmz4X8y8vOv/v+tN+dNMUYXx3dYQC+oLfxmugWDU/oywZgVliEgeetXCmjRshzmuSBHAHgBaCrp3kGT3Kk1LAatUzrhQxH/EaBynjYwu72eZsEKciBzSWcLbcZlzLYFqXqdY8ca1SzbN4mifDTnNbDT9Glwml4UqaIx9FdAAHkBaCjMNXddZr+c7c4KLkPBrazxFPKlTiPp7wQmI8whoyRvLKU4lwVnlHOXzHfGUuD8m4z14tfhlD0+nriace5fusHFeEs=</base64Binary><base64Binary>E6g7PQEAEAELwF0GmQA0AHoBaAsapAsshCeOh2N4/wcghceOivkXG/6BTJtytfBv397nqKHKB6SVR2oXL+e4YqdFGap7ki4AewFoClfOV5VRFcPeTgfJs7NmD3mSx6coXVvhwiULqbjc6Be+HhRVlJgphSxJGYyWKwB8AWgJlHfdERNu4iNj4+rkDFdjeStWvEv9u34Ni8zVvC4wbPdboQySKUqTyTALwF0GMwAqAH0BaAmTtQGtszWuodhmLC3hGGE0bDpEA2f0FNm3Xf9+KUnLrt1b5WFR6avoAQB+AWgLwF0GDwABAH8BaAEAgAFoAQCBAWjYPeQP</base64Binary><base64Binary>E6g7PQEAEAELwF0GBQABAIIBaAvAXQYPAAEAgwFoAQCEAWgBAIUBaPv1ZU8=</base64Binary><base64Binary>E6g7PQEAEAELwF0GCgABAIYBaAEAhwFoC8BdBhYAAQCIAWgBAIkBaAEAigFoAQCLAWj//zyDKg0=</base64Binary><base64Binary>E6g7PQEAEAELwF0A7gJmaHKl</base64Binary></chunks></LegacySound>""";
            private const string SCREAM = """<?xml version="1.0" encoding="utf-16"?><LegacySound xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"><chunks><base64Binary>E6g7PQEAEAELwF0GMgI7AAAAaAxvp8Pfo/B+ef9fvDBEVaC1VnM0OXqORsKDoqsvYCpMJ5TMrvGZ7Hf4gKgccvQIETCEh9h+oTDZgsVAAAEAaA05/9dIvGeWAM/Vxt6B5XgQ/ueRQ2yRzW9b5pd6ukhoMJ17ZWgYK1JU+fGKK6QvlHywt9Ij7tMEQ/beCdbV4D0AAgBoDTWJJROwUH44zmfQQ+B9OY4dPdO0kl4JFHJyK9H7swBxIVdz3jVwN40dJ4ihVHSlj5uWi8m5lnDywUmWQwADAGgNOf6BnUtD2b1JztF/P/NwmPfTQ2mjdxxlw11Jlakjn1MuddeRBwVbxDVDtkYXN8LjCpB1umxUU+0fausKPa90YZdBAAQAaA05/oJlIDF2m1Jb7ZljOvKpTMo0kVzamvquDb68pB2X0cXg2CsMOYyq+KYVn81eUeHD0ZfZW0tcIgReQ98gxhhHAAUAaA1Vp33170h0vRBZo7x9gt8Sx9PcDnED8osq6GrBdlgYTac4jcdotQOLQzqQ7ncIWXMaIfUyTpyLg0A/LjiWPJy5d3yl61RBAAYAaA821vI/GoZEd0CtoZQKUpvzXSvcYVeSvEJ6CMPHRgUlQ9a7X3AenOSe4hNMo2RAXgDvrUAMWH9hvBMlZ71YqWhOAAcAaIAL6Ud1QSGwye+6+gt6UQzCOJ6T0+Lq7AS3BKG5EHNadWipHVZL07jIMeL2z/2rWDv66hCgI5gjGPQh5Nc+Vkh9B2tj86wMnvXUEZNaCRiI5A==</base64Binary><base64Binary>E6g7PQEAEAELwF0GXABYAAgAaJHLMLdmeHc9j8LCg+6Y4hakX5jg0GnNofoqny3qzZVzXaNEnx6dZe75OFhMvRLay/08apCMdDZrZO/twm8QbXH1grgy+wQrNX6oWBgoUSJh3rq6NclgAQvAXQYhAVoACQBokPeG85/KSDplnxdkM/0mnjLeVDlgTL0eMvJz5izQU0L8ZgE1FUfmiuPhYzVRtcp3ynJgJVxANgMBE5k/R3K1hRBFnlpOGeQLDxj4TU32TXY1k8Oyg/63OYpcAAoAaI4p4kVwk8bvMk/v77day/l1ocTSeKGfa8m8o35V/f9yHV57lWV3qzxN7ZnVyUmT05Ha6L52QHSNo5zOBAAbus4d3MyPhWFDKf6VOuhyuezwalQxaNBuwhS9frRfAAsAaI9nmipPoUSXKpd+w/xCPsxCn1KJO+GVnIWwPtuRqVEGxt2JLG47Ed9QNzNqOsQde3dITgzASyhAm32JVIw8XURQyepDHTSv5roR/FgA4c9QIi4xaMyS+bqaVSWbiggLwF0GJAFbAAwAaJXkOK7lZ2UZDD40DuN7xsAdr/F1MoF9851qLosBpfRVRBQE0pKdOTluL7dTAP0OAbXO+ypG1gXt8rFM8/paPvH2HL4vtuZNfO5crfGudU5SgDLLPemFBh6Lil4ADQBoule7IG56vfOi0f75suhGLqe3kUrUI/IoaHSX8h6qkF+XE+1GXKeJABQ7rdg5yIQzKH8C/0a/hOSGbVLv4svsUGDVdksD5OpyoqKiSAMBhV9aAZWJkVFrjhYaj4EXXwAOAGi5p8aJ9mUSj7wu5ylgSPL8gvqYZY18mWKGUNYUbrjY1DGSgY0BgEcu8e1kM3WejJwZHlczEDd0Mv5mWV+Yzf7+ieTVd5W9jhLmTGSmGO/AIXiroVgAW1WgRWQBGOeydyn5pQ==</base64Binary><base64Binary>E6g7PQEAEAELwF0GtgBcAA8AaL+rxVQFsPyFsZoJ1ljXs/L0H9jgFdggHuLJAX5OoCpE8Mv5egKzId5ipbZ9FXkuzI8SxW/s3Pov/BO8wLcOUpkzt4OnUwwRVh52qF7VrA5dRTx/pfDqQXXHV1JSABAAaL+t4JvbrsoaO/o9XXk6kVaeVe/o9gKkAXzkldZSaxUi6dKvEBSxzpLtBuEoxk0R7VVSVgThGNDnV2jUgwmPnd9rNO6jPL6qX/dHpzmLrvKRygvAXQYmAVcAEQBov61YkOY9lFfY8R1SJ2C9aHSEuoBDj/yItI4Mv/YgOsYSRzHoDYdkbLqA84/qvDOn7jbce2a9FfS0B0fmMpowPVCkJWNSof1ql7WsneVSpH1BuRB2Q79gABIAaL+tUjdpnx61QaMA429BVVkDzKDsK0LSB71aBgubeqf00YOXOIqAFMnWuIwbBy6cznddT6Ayp7LZloZ6C/b6+pBHRKA8+9LIEIk7ONEGztWpqrGGQXduffw2LoFXc/sMYwATAGi/riceaAXqpJzZqlLGKLVRhnwbkPKv43bKclogd83EIdmMAJfQ0NrrYWY2ukGfZkRSOc/AXKNZzIBzZNQIGIoHNBan73Qvok5EZVISQcY6QH25su8RZ6S335fW0n0bhFh7kjBmC7U=</base64Binary><base64Binary>E6g7PQEAEAELwF0GsgBVABQAaJXjjeTjqIxTFl9LgH/a57OWIfbrn8kEw/WP0nhcx9HUu59HO0d7/fzLsqJbsBZC3wz6ifsUviVeSjto/+MafRpO9ao9T95JgYTOpJ35qe7GtYFuuFUAFQBoljDS3Zmwmu8XgAnp/YvwkltraVTcBBqEi8baE+51nn+vs9XhX0gc0IdMFNSrIfnfQ8fDp2H/ZOEwhoEhYTFgCHO/tMf/pb7/ypLTxtLXdkH3Ah+KC8BdBv0AUAAWAGiWM9KIIYH9yyl3IBJyOk0BF2LTWcWHjquV3eykfjepQowcvURwXtFzDTcklU/X954dGYrLCfldKYM/FpTbDXubdh51oRbOifCOJjrB+n06TwAXAGiWLDxvmorwVwqCMk9pNVf0AX23jLv09IV/bfedoeyFk5upgHc0jmJHcRDEPR+QvA+Oy5DZcvXtk3Wvrjr9ZVAOhMZkjN6ytc2YiCj5NodSABgAaJYz0oim8TVJbkUDZfY0Stsuue85qNabHlkvNlJYGe6moElZB0xxekrLir+EMFwPHciNrpWbEx8y0cPuNOaY08MN3aCxFWvrfzTZ96bG2uMJyp3v4NM=</base64Binary><base64Binary>E6g7PQEAEAELwF0GWQBVABkAaJYz0oixckNXRY+Tp35qdRmvZve2ThCVkFSaTgp6Do8Gf+tkscG4CVGLWLMdJ0pDG7JJW1VRwD1brxfSen5ZVS25gWAFNeJANxshKpMe7b85QWFEygvAXQZpAVMAGgBoli2K89+Bq+qm9GwOR1OLUqdMCwMPFHIzqf0QWTRFmjnpvpcAt9bjrZn8k/m3AYEYpTPKvHd0dJjhYDsLbK5BDNOEV+FWbsDtM/DSF7C+VmlJCVQAGwBoljPSiCGCdDhgLcp1SW1N7T7qp/eQVAbYbpzATh/+VcVuUmA2xVrEBRsoOpSKzNYZMzS59g3wXRIBrqnQr+cEZtL8TbWwaQG2EWCmBZKIykMi1zlTABwAaJYtivPfgawucUe5QEahMJ8oS4TP7twzjt26WNn2BtCNtKv8SuHL0iXc/tCRZ0tddNGForBK2FbyFc7QmJdLFn1DlZezxCMzS60CMJb9/tBQzAhfAB0AaJYn9X1BFBlIp9ssudtDd1UtRlh1j2mT4AbLu2ig1DBQudKQbMirdcLAaaximxvzV58/DJLNelxhmO0NuJCYRS6nL658EyvVhh9GGxJGOFJQ2WbjqMhS4PCJ+Ifk99MLwF0GBwFVAB4AaJZQiRe/69S50tC+CZpd3iVvYHstHXAFcZVbrDxGel89Sg0wXbU/7oiddXUyOwJtehzvUDQcOg4s6j0Gii1LECryGq5GdgFOY5zWPZ7yfchujQjJsFMAHwBolpzPaNdnNiYvJdG58FKGNEzyh3HDdHSGBnS6zt2qFk1QAfhmIX/Gfys4LUOrlMkGlR2hDoecVViMb7EL8/2ihGibFjDoIqwkLWeMIFETAgSogVMAIABolr1+q2hOz467jVCMMlMWUvO7sRvgfPlnOKKLbrIOrKaex1afOJQqaK1tNmGPBgqfwhLGzd7lMCAPeMj/iJ2jXiHHWb4m0jz04a2zGsxcr+xfg9V1d8U=</base64Binary><base64Binary>E6g7PQEAEAELwF0GVABQACEAaJbrZkooRgrIbA01ML/cDJNEbpu/GU9k3lL8p3T4Lq0hqz6HMUfYHx6ccRjkp83W1C/mR38TJKwq+iuS+JrNkbjzv/E/VH5dALRfcvTmZ/oLwF0GAAFMACIAaJbrZkolDekXP3rTpjo7UBkABXkUUcqZ8Oig74TpWZfP0AHL4FnNtlCjyPy7rnMuR6cc5HbD89seyuzld4aVuHYSFoSaggZX00ydiVQAIwBollBQUKUJiPi2PCSoj5dGU06P55/KP+Yk5lcRyNakyMfZYTL3eMF9qzoUCdhNJ7SVs+CBHCafp0fcTgtOtlAlJavfmOZjy0wkF/jPUonnzIeJMklUACQAaJYOtb/y4Hz7gnwssXgekf+QUyd5/QTYJnozlVHlKZF0p41eJkqjX4enRstk0SEj8TzxazMAekw8qGvspO7cY0+zMpjTquuu7I4gg/ro0VHn/puAONeADg==</base64Binary><base64Binary>E6g7PQEAEAELwF0GDQFXACUAaJbn0KguXtwhBhFeCXJIWSMlrskclXz7PqqFa74qsBH+Y4VHPCCNon5VY1iMmmFPmoHS6RypwFtpvmsGKIWGLE7g08+H1U9rFkjY/UD7GYH21cNAJ8ZCWAAmAGiW59CbsH7v5FkadQzhITopVCZT7TWvTnb5lAtCqQ2oGDtzt5nm1Lam0sHUHNVDw5H2KSYn5XxYMv7sSRLeRrx2fK5sbZaE/+ze7/7RR8kMeFVZMOWsdJlSACcAaJcJhKCQOSIaiWJyIol7kg1j2MargE52YZsNB6AWgaS5Mvgk93aosAtg9Vu2t5s7kc+UPvybj2R1C7ZK/Zrm/yuZVi+lShhuogzuOJTm/XiQzAvAXQauAFIAKABolxiMFbe/YsVSAc14rFyz3xLPv8tO0//Py9m9yI9Qyoa4Fg23lo67cSj8Y+aD1sjjFFJpD65o1sVRutWOAQFVj6TQ5yYDLiFnBnqZtS450JVNVAApAGiXGIwVt2uiUxLYERzIQRDcQUuO/DcXcHRI+v5oxy/WI/qHsMGexaYeFitDG2FYHzbBaZ5zPredNzCT2ueYF7vwbyvySktuMCuV7t8Abf/XGKtmVHhQA0E=</base64Binary><base64Binary>E6g7PQEAEAELwF0GpwBOACoAaJcOfoKykc2Y1IKzMxN/prHkZbHWTMqMhHBnRD83E0Kn8YPEiqi4Y/J7xekEAtEa62GG4tqLaGcEMOCdxJzqyRQZHXRJ+Nagy5o18ZRXUQArAGiW3l0QTkVf6U50KsHincK/QewU1hwA9G5AMe7xghh1cWKW4NfLwIkLa+/JSWF8uf+tqUAZvljqXU6KUtidTaZnDTjHuKaoKFccZ5tEkXBrzQvAXQYWAV0ALABoluycFTdSJ/nkaAUbPWUxJ7L9L0QQYCswxI34ORokqIwDomCZOM8V4EoM56CrGB7gqwO2+MBnxpkwmG7ANgrplxYftPMJIuXGhblHwJx83dVWkr7KzsijG4MFjlJXAC0AaJbn8QfrHIjjnlDpHOOyTCg+CddaWMy7G0oBkBHeW1pv3ynVby9vNeZ5mW6ZKHJ4WOHJ7jT5pfwooP0AwLsGr7lfOEUpl1m5b76TuQV69yzAqtJ93MzRVgAuAGiW52UQmNzivnraqgZssy9pCHd1KBb8g4hyRLoyQHBL+6FOKzqEMBkB0+P2rgrmFfmo3JGfNj/5KWfCgfNk0Th0vOhOxmPZTQ1IkZWRrOYWd6uyNq37GPrT7g==</base64Binary><base64Binary>E6g7PQEAEAELwF0GVABQAC8AaJbiZtZjeQ++br2IJdW3Rql64poGCs6qAp/tNJ7XzpY5C6Hr70zoipStI6lRb6Ene7PQMjPKlBlwO/QWN3stUBC+Gqj7rcJbLfIn3iiYlGALwF0GAAFRADAAaJZ8/5Hmgo1glgnRYD8zxhWcgkYoJz6H3ohb0AZ437twJeDMAFHRenAlIbjLsCG9mdC7HYn3bXCGd3+i6FXkw9kXncxsl/RttpR7C+bzYsqPUwAxAGiWq1rooz4gW4Gx2U3ZEpXFY97aTgLJQMQcz2bvZPAQf38j91MkfieNoj3S3AywMjKcWFUoBoeXUzfw6doANK8rRiWuh5LAPSm6n9xYf1ZhaVfVUAAyAGiW6sAbXkxInWxKZ41PBYIzw55wJyJQowL6VNVZ+u1Gz9EgRnu5YtDgWordSSZP9izgiTNfzL8Yq+gYluN3DGS5TW3UxmG1UtkerbC/dxIqC8BdBlQBTwAzAGiW59Cqa4rf7wrxB3BXfx/BRR8QjlZ1LP7H6m12JeBwv8jPT0CH7LBNAiuXkE6bJQwEEF6vMa8QlFCaokPiWTJbvuLurkvo02TvojH4qwRRADQAaJZxN2F09gc4ZtgoY6T/mu1mKN5JeGZPROXO9+fSGZNDcOzx6JBXJl2/+MJo81ppLXDlNMEVyZseaQH4PLB7qBhbglLs1wVqEmVq8RqlkMrVVAA1AGiW8DDJd2QHzhfWkov0yF4HhYlz1LQBZ6VG0Kekr1jwpQRvmxACx4wLYRA8zbI5yAiYNSidLf5m0SuXTBIsf9hznXrrNWiW1PLCIFOY1kLZOXApnVAANgBolveNThjJdFnF3G3fYPnqyttOq7oWbQo2HUqoikKYA00YfGU0FZUuB6uBVWyHDThoyJj1OaaompbH9lU9aTylT266JBdlmTV2ZYI5bklZCR88bnc=</base64Binary><base64Binary>E6g7PQEAEAELwF0G/ABQADcAaIVoAdSkJdrXLzTTsCWHNF9MMOYPn36BVv2sGRdwPKLStcP6DyZpuwZc43ACC5v0W3X0JXg8yzNHDgGjBgk58SGgvAzYKayd/pkiqbVPWxdPADgAaJbny7hZiCw6iSus7fWCdQV0ltqrf2S5jozL3QLGZ85Ff5mYNUTxI+CZML8L8eaFhNJsdHv1GrlfVui6vp/gMUahsFL0cevoZc543mSn2lEAOQBohXrsnMkHC+qG17h2pZ0EMSvQjBgFLDbxiHclf2ENrDZrZFRMjD09ElUlj9645lxOvzrVJrKa7D36xNfRAzNccDWDoUqAhTLipYI9gvK5bWILwF0GVgBSADoAaJbmpAs1DPfFo7dUFzqfl8XiNbUxd0bWpYkl1p4U1cUiDfJqLalFZmnJtJFgmcvuc/9+D8JiKZZ3ApRwsRc9X5ySUzpFikFr0cpMlG4RjbbdWRSJgg4=</base64Binary><base64Binary>E6g7PQEAEAELwF0GtQBYADsAaJaUiCDm6pbhD7Q5w0M6KHl5BQ6TG0enHM8+Lwia6aeUtvwoOQ/cjel0cQSowrmUaRJXEMPXCwqMbxKmdKafTmiI+EG2F4SVIo1G/2hq2myDjV4muVGdAFUAPABolvgfd3iMMbT4+IlFr+sCWg31oOwdn3e1azDkmM+YvMEVytzETT/G4uVGjx/A4RWO2oJwkFgVdabz5mcZUMJF+AhNZPBOMrizU1QeXrrDbEZK4CiTC8BdBgwBUAA9AGiXQzvDa3zCyPnFaVSg04NtsC4izX9OonOVbpmOv6ua4mWwMPZXUmO3VcI8NG7m7/7gq82+LOf1qYqGJDzINHRAXfeVDZ/mBIPFJ0f92R8TVQA+AGi/6J60w6ZbOhCqpLORl+28/xJeFaLUG6OIrSok2y/cUBmuhRD6pleswljYh/OKK+b+OxjBEqY7CYvcSohHi9qzeLD+nDNEueTAShrYIrjSabOZy5RbAD8AaL/pelcdxMfbr5TUraUnCrb6SYyRSh/pTBJFllispAsUeOuLJqA5r4pEkhv6/1bLbC8cD+lVmbRWqJkLs9lyQFUV0Z39oGQcKwcHGIBccTsJiMARWo5Oayh8G9EZgeA=</base64Binary><base64Binary>E6g7PQEAEAELwF0GDQFVAEAAaL/rip+lLwURSEd5HLyh3vIicBH6bxgwG4OZkRoRt4FkfjMSEkXrqBm7G5IiO5rVuOYJz6piPEi9vyGSvUCA3HUU4cPX9RcC7V3Bcvlpfa5Mk9zze1QAQQBovEdFVjm4spRLbgAX3fqeWBv/gL+r39z23smUtkgjiRGGRmeHjIiD7VuJd7t1XD/iSpt28O0/Y9jnpg+Vi58Mc51XGxgnYbhV5yhTTS6/wT8HRhtYAEIAaL/oWxD1Mvi7S7X6XLnlRG6dtDwsFYs0u1LDm/VO00rVQiaRierFvm+iFbEwkiyvn1jQ4Ia0vT1m0/xhhsKVCmVOXIJ5Jm1LSzJT0cRkPTM9pCdKv3B0kwvAXQayAF0AQwBov+jLwMGEwPLrBCThkve+DUBjq0B+GanmDT/YM4xzzBDqZ57yYhBBFr44RxreoAy1U3BaHZSCMrp/WWvmKn5wEf+qIcu2VNAKtfSEaxjatItwEirAktmBQ7GJBmBNAEQAaJc/pcHHr4k1IJc8HYqEfr2uA3cVW17cfT1ASp4g1O3YUTDmBnX5GDgl/1065R54zo/MEoMzN4thdjTutFRjyKahfEX//VUxm3xErVJtVueh</base64Binary><base64Binary>E6g7PQEAEAELwF0G7gBIAEUAaJdEeA/umg8GNP18qt8TEpgligm0PsPVSaewoEMB2K0ak0HUymI8Wj6dC38STUms4n/MdddoE1wiHhqGkOBcxEQMaUcLX04RTABGAGiXP6vXIl7nsJfRuX5wdTT4Lo8jxAkZBKi/ro6f9uKEjbrkUMHuM0gY7MGVuPNjGa2jf9hPa4bDyIn/2Pdo0d6smPNpbH5i9a4ZGeBOAEcAaJc7Xq2IbxvTeoz0J26wFqjIItDi+FoMxsll1pjI13lA1BjZjVrULk4hGeb8A+bPLfmXAEiKlHKRr3+RU6lqF0eLbD/wK5iAy4IwDIRJC8BdBlQAUABIAGiXF/PEWrDZ/fpOEWB0frIqm7M18tekF9Inh9Ln/4HmFTGYxEvy8pstmN7+dwDkFvMMrIgVngdEGTONfeNKUioF2uPvsth7GFWPAQxFjRsiC8BdBu4ASwBJAGiXDfCD/+spGGw1H45lmItpMTnG+yno7hgMTBiJv5p9DI98eu9uorML1z4nNR68pIYTHrav/gK1ogVSOMSNoN6zp7xYs5W7up0X0kkASgBolj0LvIBftfgWvympgR+hbs4ONYb9U2197wKa7MmGqL/MOQQkKnJibbKg1MwihCEx4wF5tql/t26o7NdAYT7UV1PEdcfTrb2nTgBLAGiWLzU1iR0JAAKC8NY7VxYVktxuy4QW9BDYjt88ug7CEwteAgpRLmDN36nSka63A3tfrjlgWlcDuc85Lh1zq9tglqaMi8chWMkUxZce0A+h2gM=</base64Binary><base64Binary>E6g7PQEAEAELwF0GWgFTAEwAaJYn6+9t7255f6wuxHMafDcMNNC90qM9k5qRWEQ23/3BNn4jb9vhVVJht73QEQVnNT5Qe2PpEHy0GMKNFSAch06GDxB5ni6moQoDZxKuE6K3/1lZAE0AaJadRH/vv74Kyvd4E5DGJwu0aog8uFb0n+XKvIfv73mz06KFgh92bt6F1rQLNt4CbtmqnurqrdIHytXFxJ+m5rtaCjwg2grxqCQIW89fWKbPBrvrXkE9jSJOAE4AaJa8vurieUKCoN0UWrFSAhqfrsObAZuArsmdJaiA/YSR5TirGS7kYrGi7tupeZJW1zRezwzAavQ4XzSNQLU3KyA8ZISqMPVvwOVhZNXIUABPAGiFfJFVP/t0izxgs/I/i4L4HggHzvgYBtNKVb54Eu2z41pCqjZ/tt9/UFmAwIu/qwVba/dKpwN5EYx9EEyjCZKbhUHaaXLJ/iqmj8BWpZviC8BdBgYBVABQAGiFe5szteE4gW0d6y3HOVlQhASlLsAMfmWmxjF4qL+4iOA9dosli/PRsnLUnKFBw4TMRiL6V4Abmxh8WLqI5fXEutD+7fbJavjWoVDNxNcHw0IzV1AAUQBol0MThhXOEpVaeKnf8RMWMRTce0FkMYHNwz/qi6fH9s/Iowv6SkisCeiFinSF1b/PG0ABio/GFXDFVa/F9llRvLrOSV76MmqBPWL9aIbvElYAUgBovCYxvsAvxNFbi5iNTEEhhBd0d69jDoFUVdjBQRHv7g5JR11i0KE+AOiaKgaRsa/MLxYxdL/kQ2WP6xACbXkxUMfITGEi1LuJf5LMm6Lscje4HGdBWydhEIM=</base64Binary><base64Binary>E6g7PQEAEAELwF0GWgBWAFMAaL/rKxIP3Zcg7wNQhcz2uvLINSgdXFY1HPmgTk0mD6QdJrYo1t7mcwcAoegRvk1UY6+Cfu9PSKXK8alkvBLwK+ejQGMcE9nqAVt6oA2CpQV7Q/cPIokLwF0GAwFSAFQAaL/rKwDnbbHuOrtdSQlAnfMaIF/buZG65cJQXA31RXmpuYHYm3+xyue4Cq3zzWnBXrIRK3qok17CfzfY+k5xVsTwZ5CvxXutzPvUdfy9tBKTUVEAVQBolz+pMFo2LE83ARnsTKKC7IU+xQ+NdrwTXvF8o8PLkbyt1GV2o6Hw6jvmNPXo0zDEqVZfTLPK2X2rYrCzd8uatHHdtEfJR83oltes5sdOhlpUAFYAaJdIZgyvmpMOl7GfkuylYa3jbeQEOs8lYqZd66i2WN+tNXzPnTPLdGpRmtqvHIkzoNEdPoXkhlw1Mj7TZn7VSbyTUuKoMzISy0zILnUG7yRfy/SKfvPO0Q==</base64Binary><base64Binary>E6g7PQEAEAELwF0GpgBPAFcAaJdgDEnLm6kel1jcqsMT6PDbmirrfepUQleerXL0gJ6lMGF/WgIYg5ALrerXAueZ3jGUtA/dvGa7nRBmEtNT6g8+ZyyN5uh6tHM7nzscRk8AWABolzpuWqb4X2KkVx7uKBFpqjbHSvRXVKVfh0arLn3vD4SJX6bbYY9iWETKBcSksJkuaUk6SW4Uh36ou+Q4JjJNtE3BtWMQXpH81+9rCQ/RC8BdBvkAUgBZAGiXHOx5sg3f95INTaRkzv9qlR11j6VtH9EBBGrbcaO5CPKQdZSUfuR/C0+YMf5aIeY2M49K6X8RW9ZZWNg/BSOglVNH0EZ9lwFHmKWniwmlBVtPAFoAaJdDE/+qsOsdwXwkH6NRIHZ9s5Au6IZ3iszJENfrkz1zcG4d0DyiPl6+kHJsPkwhcZkFTJnwIXNiNYAY3PwQmE8lUN8qNQ7VhGi3Ac2Bk0wAWwBol0KIyZuRYqGwLLpbdKSeDh3VOeOqTKYutU4WsLeA97V8tJ0KN9MIA6IH7n40qw9LMRJvjOgCC/DLbVrJZBmoe0P/qP4rYhZdnUcShG0aLA==</base64Binary><base64Binary>E6g7PQEAEAELwF0GrQBRAFwAaJcTqqROP1tiuoqz/wgSVNjnkOBzgXA52fuCpfcExyH5xizQFAgVru8bPNtlTaUe3ZnGIWq/JfcK8BQ4k9yKaTMjZ98Qe1KEMJsngKrVZfNhVABdAGiXJEHekTszvbr20qAuVWvWbhu5rhECnMRk5pehMtGHGaCr7hJZfsDOmjRfDHuCIfK60XJaSxBtj7qB2vIXAvk6qUnoDl+Dyg7b8ZCXzJ6a02+KUwvAXQZPAEsAXgBol0RmkLzaGkWQE4C/qHViEN774+GYDF6MtpaVSRi/41b/G9d+Ic5EqdFo9wNwSv32+zBbtwmk+KLAoxQZslVGKjel3+vKQBDfK9NPt1hD</base64Binary><base64Binary>E6g7PQEAEAELwF0G9QBRAF8AaJcj2xy1uSBM9h/3g5SfuVFB2G5RFWP3ZfjVW5ZBsILLVVaUe3U3lgQ67lpxCciRBG9Ty8iPtAT1B+iyTqYLtwCGTEhIv89/SH+Nh7MnodPaSQBgAGiXQp7BnGHfIboRZhINOxH+rqpNVuCsec0SnYckQO0IpJoOGsDtBRqytIxGTjjWJnDl3nPsPfmS/ZyeqKNwZSOPXs69H1IHhJJPAGEAaJdERzo7MX7xFgk4aGpcUVG7KoPeqGnO6M0/956QXcgNZyxNhOIMd1XSFvuNCMe6HILTqaCnRqw/KMVwc0U7XlKu9i4ly9T90X/+dn/gsQvAXQbxAEwAYgBol0R4invScaJ9/O1QWen09BSMknvq039Yb5e4jY290Lo5LU+INae+nlAse1xU6C8T19vLZbZnGoVxrQ5jlsn0Iq8Ei+5JGXQm15jLTQBjAGiXP6kwdZ7Tyrgt3KPXJRhFzAnVtxYfES8IGYOYpnXuVQml0XPcoZqcJ/Fb0U2Qq2h6BYtcrzFhkc+iJfZ8C6I1iBpj+VkyOXtgQi9JTABkAGiXCYSEMRdlue5gW5C+BXzJVK4ieg5igntarc1LFPtWOdPzQpTzMjzeVJw4Y69L5CumZe9KF8gL80ZYYLnC1+GWIQE+LGpChoux6Nse9GeQ</base64Binary><base64Binary>E6g7PQEAEAELwF0GpABPAGUAaJcYiIp70YSpaYh49FCskuJSZsZjuTNSlNTrsxnkQ5eCI6f5p8YCy+ApmNaAAfr1IXsGvx+rVzetguYlp9GEgz69VoOvB24EK6BGQHnC4U0AZgBolxcj/6valXC+loPuqGwEb+MrtyLuJFQAB0zDUcpYOuj3U7lctK/5gAWGbT1cIuXrhzfVYHcAYl6AlYt7YBD++CVeoPIWsZK17LvCWwvAXQYFAVIAZwBolxdKRZWW8sROzjs7OSZRBDYltm0VPIRh9X2R2St0TNZDEEq2detMQAQRemABxyJHxnrXcg4uqiGWJ8Vu1BUlhDcpzycINatRd8AMbVkSU6iaVwBoAGiXGU8tH07V48VqSWQ2qp1lsr3hG8Q05hweRZyfazyhcYRGv9BbJOsFNZMqgUfxgWjArqhRF9vi+TUmlmBtYjjpbJoI3AW2GjexDVh+v7ARz3lV9fwEU1AAaQBolz+WeEe6fwxjadeY25L5njTtT9c2cZLQu5XJlKVGUAT+TqVFy/MUW0LAHRJriDI3QR4+fIitpQ9vWBHNxKkaFugKVriW2s0mAtqqtCAaivLsoTU=</base64Binary><base64Binary>E6g7PQEAEAELwF0GnQBJAGoAaJc/qTB8Ct/XYBotFkGtaYWQyDNanMtW+51oO6Bk//iupw2/iFX3SlePXkVjgXpWtF8hx+bUjWQoBBhfmVfH9Q1gQKWcp1ssIUwAawBolz+lwYSzbIbCSAQgp1I/sHvYMY/ywX66jp++A6u5JfLcsfFM4PtGJN+5yRJHLex+uQI1xk+e/I4aH/yJqboJGaXrUfZOWVOmnPhRC8BdBu8ASwBsAGiXT2n7rGd1/zcsjzutrWhap5/VI5RyjiG5yiTDWqhl2crk4a5GUkzt0SfQznjK0EeFmWhluRTAFZtBRCV9iuW/O0ylvVPgaz07E08AbQBol0RxkVvybyEDwyHgBOhvSKCMbjC+1jhpbi+2h7S7YA7CpRKmmysQAJUQqHszRXbHB0hiZMKqLrVQHBc5Eka2tf/ow1USEewcA6xGf5yASQBuAGiFmREnDwByajLsxXoMEYInWEzeePPnUIZsJNmPa4lQAmA88BrPP17P5UVCXdF+xypzBDAM6Ph7k2fD9DoTHOsGUqnfG4z93xqxSEen</base64Binary><base64Binary>E6g7PQEAEAELwF0G/ABLAG8AaJc/qTB72qzapSaiLTe9RR781N6T/c4TWl0GIiM53rkg+KJAImn4yAYBsH818x4OnBf4Bi8ZeJo79uVvPxahsoRS/xvoWVC5SF2jTgBwAGi8Ig+2YukVcPDM8rgFuxR60bWT5xq2BLgN3vM3ZZfvcuNTLva5wAj4yide9IBd/Btycec8tXJNQM/9HIa6gXJsDqOIkbWV4bJ7z+uZk1cAcQBov+epkcocFkC+AlQkkJPpckm+c/FS5VlawxF/EWo3Uk4eIhCTrLaF0eXD1weHpeoUXRtozKqBtK6QZbL8QWGiogXXUpQlCuBoPyQs+sQFGsOUH2H+4cQLwF0GAwFTAHIAaLv8VyGclVbPElbTMet+5DmQ7Sni8WZrVB+/tv42F4CX7IIJ0uumOOgpSHVlp/IgmbxaAFP/K5OlTYrUBh5uiFjSQ4ObSkFHBS24mUw7xtG+4NlSAHMAaLv5j+w2qj2BI3f/oqYiKiivxzHXd8DHw1tfIYv9Y83ms2da5i007REpCsg48gPFnt63VWCR8XVKjlk4ozHeNgemf8FPcFmwSDFp3Bv62WFcm1IAdABou/4SBaWhKhdM0DXLlRIkm7c3tVVV3lj9RhiiAq7hQiGM28lenhJVPrCQvGn3udbLOjNe2SYqsxSw9Z7wUm/EAyBlQVnwjsiw9BmrEgvAFhbaC8BdBqUATwB1AGi8IgOQk1wzIvb/a664jbQO9mIxDYWokMluXsno0Eq3K+U3Agmo6rlgO9ZYOM+813SCh7YKH/o2nlT32fDsm764cyF3HAK5bBa+hcq8wpxOAHYAaLwIyVTFbLtl8kP75WmDWYtIfJ60CkV10FWjHgc7GM5iTE34WtO7pZK4rddfoELQURPMgkmVy5K70KrjVatkqOmcFqzT38jZpK9wx/JBCw/xDw==</base64Binary><base64Binary>E6g7PQEAEAELwF0GAQFYAHcAaL/nwHe25SZ29Wz+OQ3VOfHZ0qbKMlDXB30AGg3nFa49KGV7CbBOcnCWOyxzbMnxqe3XtkJtMtoIYoTQipWoxCA1XnTTJfGGB3RRPwXGEw6JTnv1fPfm2U0AeABolz+YVPsg1s9mqzLIJVper90vIgrscyxpdwlgkWO4gckxGsofo1g4jJ08gMJaZ39RuIr5wn/L56WsK2mh0S2om+v5sWwJBatEdEZe41AAeQBovCIPtpN7Nl9LC6NkHSXGr5bVK21ATBrVRbQuzXHctSmOlKtyL3cDi7hTduyj1OEQyZMStDfljlU7p5pwY30POnB/83fTmnDeT2wIGJDCIQvAXQZQAEwAegBovCIDkJNbpsvblYuhHJE62BdCU76hnOOMnImYy0dNZqvj3DSsLYAaBOqbb3dulsODKPhMqTPtjYTnJBI4O1gXfVYqDgdCb6weQTLjR09S6Q==</base64Binary><base64Binary>E6g7PQEAEAELwF0G7wBKAHsAaD+ex5Dtb8Y6teqFicDt0kKpEpkfCgV79IcCAheq4HAe0cQpBLM/LpM2jHNCnFLH++hx0Vi70jjPG4GRQrOaFQ1moJj7ie8j3oROAHwAaJc/ppuxweyCF638NsZnd6mNNfDXi4zrdC6J1cOTTExd2r5rgy5ykx/e4uNnGqrqXIgP1PChhrS1fjzGKklAy/fgQanPIlieAX+o9MIZSwB9AGi8GRsAo/PhW86yXztO5TIYot8m64ZfPKYvC7JKf8OLrHJKc0hmzEtNd7XoWFiLYrorAKc/yn9C/B/pTYPZApILcX5bHdr5GQgnogvAXQarAFMAfgBov+WTYssHorY02E3suomk+sFd9BXV9pXNGm8I2ecoC4OjZ8v7Z6ka+tCsp7DRqsZ4L13XeLeVzDxq/Cyc892dsSpl60MdAvgJeBXC1bSDCEjzClAAfwBov+Xq9Krw0h/ilRpxF8kU56JC/b68caHzzIiHbCha2H1+4JcEIz/PxkoM8paPGwePy8XBXoq+vHFHANzwK71Q1nZcTA5xc7wWS784aJ/eGNI4Adw=</base64Binary><base64Binary>E6g7PQEAEAELwF0G/wBQAIAAaLv3JYisV0rjEthfndI+NTHE6gR0xA1AAqTvB+tYrrWX3hc9U93/p2Hfzp7wdIdczBZkOVcnrUDTqIB4+drVC6jtSrR5ky2Yco6v79QvuWtXAIEAaL/cwHZIgemuhgOcjET+gJC/xgO1z1A7GItDxlOIjmMObpCF4RZhaxhAxDMYC2sCZUNnrSwsHMuRs7/8FXJbsIyMQtUBCPpVjqCLKObVBX1SXUd5rH3UTACCAGiWeaYktM0ltq3XxMR4/J6m5n3pXPTvIWkQrfhHRzavnQZO/T4z0+i+i0tr2IKh7YzoCSft9RuWawUmqMS7DDvgAqcC4+v78vf7gpkLwF0GowBPAIMAaJY1sNHi0cJxfYxsL91EONy7gyWBfilol66ndSPWRMd+9Kfgur4hJeLTLEFBOhyg6C55FMGp134/oY2ZoAYLk8OcoswKxkHCY2R1SJvVYUwAhABolnsohf1zWpQA0ivf+5VChkc2xBowndvsDbzKfbe1ysBRrS8E35u5vAjCrhC9GwxOwhRSFIcZQnAunaWXIRoY6fP4BOqsMnZbPPfTC8BdBv4ATgCFAGiWoH7oATaOlR+B9Jxl4gOAn+MWqxlo9tdh+2Qo/BkXR7CmEU/8o3/SZyN2v2BUXCibrzmtOclK3iPD1/7UpQPUm0+Ah6shkk6QEOT0a1AAhgBouoX/8f5FpkVaPIcDqBVAw1LI7yVbgpTPxGQ+Pft2bxjBsyXEMxjVxrFSadzCgcDq71SmJBTW0e18HkrsTjuX7Jq+LfiUw2qN/DsckRJBW1QAhwBov9FHrAq/FyTIj2ChniJBIaiz/ImUkOldYcwWhDKVLTBjVGzniXYMxWTunqk9bwH65iv8Dq13bHZGS6taekkPE6StXiHBn5v6ddiIB3a1dKvKmFFJFwx/</base64Binary><base64Binary>E6g7PQEAEAELwF0GBwFOAIgAaLp+zX2WqGXhH6bpfz3cWNY2V/Lbupog9RDZc6rKIyfzsoJRdKgiqkw3c4xgSsX/ZF1tGJgZUIP9rP4vOnshgKuBrsf8NqY+kgwTDjxLVwCJAGi6huervv+EyXAug8k1PBnGkltO1wMiNldH44M3gpebm92M7XGWyY/WjhdARg4ByDWGs5+PmNoviTxP1qMZ4ZCbzbHTqi4EMzBCCws6dMl9eejtihSYVVYAigBou3ZTBqntu9RFXIwxtinhjgf69sqckhL1qxKRTho5KPPHSxNx/R2ALQ4ICsmLc+0GtUp4lwCta7G4sr2+E3MK0mwt7NU0DHKaZ5bwVbtnD9zpZlBknAvAXQZdAFkAiwBou7E0cpKvgmJ+DWRKuwOs48nlc/M9h67nmIqn7nPJf8+Z1Y9qg5ggpGMtSXh0U+nC3ulBj/uMgPosyPsh09vuxKUuq0iabgEOksWv+PYa+rgErXhxdOx5IsgyWRk=</base64Binary><base64Binary>E6g7PQEAEAELwF0GWAFTAIwAaLv9+9iuX06HTq727F0AiniXZUIXAdJoqS1MY/YiP7xC2YSnK12ysHe6rSaTAsoP+z0m3RbwRrY8Ex3SBm8VNY+2JPzlsvWrLPLs68LGSgsGmyJTAI0AaLv4Y/ffpB/Kr2Yj6rnco0W7lFT8SA8v09dDn2QrqKL3eJ/UgkezHPbuN0BdBuWlGmob4STosvVfMXzNxAsBCtVc1B04IEr3rC/vVuM53xKsrgtRAI4AaLwijKPHamg6JfcLtl22blrFWkQewt+B/8QZ+BnsrpVmTU9SzdZUnhom+arZ/UKivivTaFx/ah9Yln3ME108tElJWG/5hD3YEPuYBA5QaylaUQCPAGi8IoyxBqus/sMHn/ZnQQPivRu6KPvwWad3ZW+qJM0gSCkkRWP2g1mhxYZILdXB4T4q+D444u9IZ0mwKwZeJEhmi48BaGGBBJPRPu4r/xoFUwvAXQZZAFUAkABov+hjeVvbvs3zwNLmXUx8XRNYAQ+IS4ShvPuth6uu1Ec1bnxFNoAXiNF3GNNZioHRRtePtvq+TGGAU+XB44oJ1FzDX5Ore+G7iBZukRaffzpR/8Mch9YPiQ==</base64Binary><base64Binary>E6g7PQEAEAELwF0GBwFSAJEAaLwijKPH3182HkEYhw+z+G4fzg4ydZn4Pj4LhVIhTziEaXQhO+LPj6wmtcLNJRO1tKxi7Sn7jZKjXmdiLEPty5nGasgL/QcrpPs0fF/HFy92mVUAkgBovB3Cc+KcRCC/GIMgri/vL4kvusS/khTD9WZ/VEYAWfSM2STAjCoGDUu81qhKA8IMwu4GE4X4g5kOdSHiXG4SNu19NYyJHOufelvkXMq++cFVsNLYVACTAGi8Ioz77rhWnk0pegk/8vwnC6EuIDoH1EjLxZuXHQttiPuInkBB7n3Dzd/KfZIXHXI7qjbKTfmGDv+17iDthxCSJufXmJSwWB2KX4i4NQm8/A+tmwvAXQYHAVIAlABovCKM++6CJKx8Vo7gZgm93c+U5NzwEvNrLt8HuDqNGUWhMlXlgzMkOCI0N/S2f0Ny7IEldpWzI4Wl3U5GYY7QJUNp2vBtoDr6w3ihvo/He3XwVgCVAGi8Ioyf5q4AvR0NfB766mXbp1FLsuZRdn89fC9VTc7/3CDzL9rBozyoBxvvHA8l21i5+nLxXFPnLykhWcSSNHn0aauXE0i9qB/ppjDDU7ywA7yGtEEaUwCWAGi8Ig16n2Ajc12xbofE33+uyW2sMpKiU+o76ObNxj93uFJA+vIn6K6pEo+MzeYPilEFKQI41FOYElQAqTpx/FeAJ0NEd7TL6zSRKCEfxcRgofs6OLhq3g==</base64Binary><base64Binary>E6g7PQEAEAELwF0GqQBUAJcAaLwijLFaXLAIUsBoWIkEX83dJ4zhxwVmfB/j5vwAj2qUYNReNiSa0AH0P1ebEtM7dkHczGn9Dnr44HstLgeLSHseVcrbx/Enev+ZyScJMrmxZoYoTQCYAGi8IKepeen3Rtqz0E1fSB7MUhhEU0/7MkBZnFeYvy+G7wOJ7gpCKgMRMMC+6vT+TswSXsUn0/jJNlhWerxcg02ybQX89MCvHGjtVlM8C8BdBg0BWgCZAGi/5YrbOGlDOaEzDPBGp0P352wzfoGWT3Alh4FcWQrqYTieJdZVh8WsSygVyg3BCsVh3CvE2SJ5XvgY9MEMY81Y7gzAeTiptZAd4lvUoNrdUZ1N0xUIo5pdgFYAmgBou/hxKYSO4rHprux36JiGXRRan9pXBG2B6t/OAbhvwQHQpZkIvGCsGeD+nKu+ULvgeFJBcqySpSZKUE6Po/yCrYNDDhOBfOUnLKRaTrdBeoyly61i1FEAmwBovCCoRskWDIRyRaa3H6bb2oYdM8cEkQ006O1V2MtS5D0fdw/tE6uN0nPKir/1+/Vg8BJ19w2j65vCX53ykfvE2ERKV+B4SuiAjWh/sYim4blAbrFb</base64Binary><base64Binary>E6g7PQEAEAELwF0GpwBPAJwAaJbngM17SnRAZ6beL57NP7CZHrvY3MvzC7bK/TaDAym/U6ms0BkVdoR6Ud2II2LFyG4tXj9YGZbSZifAN9cT0jqpxow0kSPXPggPisI/zFAAnQBohX0pZBMi8ZdhzSHQfeuP6xvdtKd5UFRPrwFd7tnUY4lUkMD5oFVCmhtTg8tnCsWT6l0m9AevsuLFVdy07VQlSwOoUk7WC5rJjnHX/+N/WwvAXQb1AEsAngBoluyU9SCo166XFUwEvWcePekYlLSjqGfU3ib2r/FON8cGrTYgAdvO/MCdA6KlYm9SJYKklBmV70eF6OVPbxIDXSB72xajFee4HcpSAJ8AaJcjfyc3lpob2wBMFFO9wPfqzcHLcEq8QZGqdFskrbDHqbCqnewxuzicypPUYiTyvu3xTmmI7ckOLX/nHxMHdqJuy3XTPDL2hHQQrS+cgX6UREwAoABol0BAf10t2IHuqtsJD+bWMq2WIJV7TnooMvU3JPA9QaRW/qlK1BuaLvdqANVeD4lm0ZzP7lF3pKENDqSqI1A0NilrTDDNHWqor1sIC8BdBlAATAChAGiXP6kMi6jrXjqKDSKSMEzB9ugZP0VLMIUUj6Vs/9Ff/2qCfyFHzAHyItNqbGM2DLy0y/O2Y3Ha3UhmciZWtY8Y0qvOsmvYoM5J99nnidr4</base64Binary><base64Binary>E6g7PQEAEAELwF0G7wBNAKIAaJc9bLhoHZiYhjLd7Xo0UCEgpSJIsJoN1XOTQNof/88eb34Yc+Pv3rwdNbLiKwsx7s7wH8CdHE7DTXHHB8E7RQj5cQtXtkQuaXgC1sdLAKMAaJcUUKZK0vWOwnEx9NIMgAF3bD/cOWGR7AFvsHrVFlUwtpLp0LKcOSmo1lOyaCGim9rcuHtnl2zR7NGy/cZR4CPQwBb6uSj9fw7BSwCkAGiW3WaQFuXPVzap3B+G0ODv3urWUGJmNr9/vK9txdm9yAARFNTLPvcYUo3HcUMt8mrfs+omEEjuSTp8oENTGdA2smFimBQwf9iotwvAXQYaAU8ApQBoluHfMqeCVmBhZ+w6sN+MGBg95b55w94RDjeOs4c4b9wYTOf4HN04Wl2vcVBLZTfJXRXLXJcxZmztERsLLSPbTQUx5Gfp2rkusEZaN89NOQCmAGiExcARyZDzcZ5o6CkvVNSkRiOdBw/VOpcdRB00aPIv/vdlKJZ1fTL33s/f/KQWdjB/FNQlhlmAy0QApwBoOI9RCGiCPd+Od+yjEskJaVZMod8eCJouPnNSSIrQuiq4IspbE0rlHy4pXnrNjgm1a+qj5R3lZBmbJZpEkeL0HbPeYD4AqABogEzHcMszpBUhzPfMlKRj8scTJZzfPnnC6phdeLdKDr/WLHcB2B/PRW2jmJ8HOQxW56k11zmYugbWWumdLdv9D28=</base64Binary><base64Binary>E6g7PQEAEAELwF0GSwBHAKkAaIQwv8Q0ceokEBbPTYB4ktUexp07MN9Nzq2WmEM/1JF0nYXwv4X+aPPXwV77urWAgDXDQXnmdJK1ce9FMLtVVE3BIfm3OaULwF0G9gBQAKoAaIRJ+HS5VbXfbVdNqUfQqbqwZX1+n2JR3cYmUJNrg7F/wRjraq+wtzLMPiydFETMsnA94d431tpBXb+aE7oOsmmF8kFL9eEuohB8f4ee6/BLAKsAaJQOtnSZXMiu/YIbkHTInR/QC1xrIsinVKl6ZXm+Kd47pl/OmnltES5XEY+UPC4CBjYEOP0iKBtAzg4kU6ppeoeSODE3QGiNN/b2TwCsAGiEZo6HUrnAuQ43zi1MBLKV9N1tarhAABFB4au4U8wQ1pSOwjaW8foGBR093YbB0T7hA6bgFkOO4Ki+bLhdLOjrUCZGm8W0/2xKW6pShr0LwF0GnABJAK0AaISPHHefX+qF8VxLTHAt/u2csd4mJoJLd2XR/ViI679bO6i4rxziDVJnT98dCv6NSUtNlT8euGUh2qzwIGKXAmo/3b127nqfUUsArgBohBoE2Ccb3+r4LlzoOtKPy/xObnp7GkYKZM+eBolHyfvCgBPSrJNawZJqf1LNihtJXZCQ+yALGVz6ZpPeg+zNwykRLp2RAl/LiTmfO9gP</base64Binary><base64Binary>E6g7PQEAEAELwF0G0QA9AK8AaIQC/3oCvwF5LC3OHH/is2NyIObu0CLO1ErG22OBTL9tAAUSpfgBJsFOTUAnJ4+r2WS8SRVQlEfITeCf9jMAsABog6MXEYbrPc8Lye8KtZQ7wJHPTofpqTChZD8NPJDxggkVqUVZR9WrfFWNU/CFXR+gTGRVALEAaINuXxkVjdQCxKYM3xrkkQJkBsXrbrQA6nQZLN/sE/QIrqhv6nHrm2925/Zqo+3jT0xxoPJWzLwVaR/FwtkmkTID5ZGOyPh8JhDFgBgid4q0BgRCqgvAXQa4AFUAsgBot7z8BzDeWu8lOYq21mZiEwvymvX1bxzdOzFey4KB1w31HG2G8PfO6Lf4Ryw7aBAIPK8JqCWMM5K3P6mt2GSJuKgtZL5DFKNdWFIU/AGToy4hBLz5WwCzAGi3vRcKvaBQXCqXh8Q60PI6szpjeMF1cbMPq7LOaA04mQDZs/iSBFg136QIdhW8q+zi0yawnPTvKVbxnBmiCB42RUh6ef8wJv0ouhhNPLpdV5Dnhvsnkc4raG0tNhKb</base64Binary><base64Binary>E6g7PQEAEAELwF0G+ABTALQAaLiaIsLTSFyfHLf2hT8kzyOFsE7bbP5XBPYFcgdyWEpn0jh974VFxLQ1rMh9UVJSK7nEoQPH7IIlYcBC+zndkRLlk3EaEL4BKhY9aE8GVkTZVy1QALUAaLfRA576Wi2ggMqgEHhmcqLDsggqEw3kqITaKtVg5QYfDPuVBkx/aJU9kkQC/9S0AAVeSBXPgFJWacp+2Xiit6shADru9MwDORXvyk+gl1xJALYAaLfFHC/jEu0VY72XQP2VoE9wT+0kQlFu2cNMZtHRj81/rD0s+oQKteIhX42sBLbwl75cYe6SNn9YtFpDg9Zq4EzpjlPR0DB5EwvAXQZPAEsAtwBot7lqdZ1aerN0h0RcMLxOhGNWqeQxRm+ASmvVWvlKBXCcWUAyoGuSlUrAqWEkFSW/lr1Co9evFd7c+dbx/6gzOrUSomFatIbuK5cLwF0GFQFaALgAaLe9PCrxfU1gHdsReMsd9JRg+oOtcZFzTVxtp7IWkJFoq7J7B/+ZUnp175iBbhr0FqAbwBW9+DYmcOpWneUSYUrMd2zgyuiWfZTDSJoPCm3RGF+b5yfvNsqDWAC5AGi367+yWqzUUUgkhcNsjgs7l5TLDmPAtlKWsgvUNGWf8Iu3q0j5DXuztkRAyL4nN0bLx9oaWAcf+TWI+cJXld/wVNwIg882TSTXl2z3LxhCE2PbSANEnSNXALoAaLfRBY8uVSfUa8bLEjh7tSQcgzFPyGKP2wJCDYpowEZQY8LuopfjhY+nu39SxMIs6rpByulyy+HwB7LmoZYRtojiKbaqSd+h2Hwsh8MPxWLiYx1KEcufs+1A1Q==</base64Binary><base64Binary>E6g7PQEAEAELwF0GGgFcALsAaLe9FsczecAIrWoDVP3YdwJRcDd4uAYoSNa5P7REAKOhYr1oUobb6T5NV67j9qlO1D/BPucoC4MXFUDjOaJ54ShOD/FgeGxWDZB7oseyBmPxL7C8miR/+HtV1SVaALwAaLmBxeWZf8xGVfz5+9yBQOXjkbZVhc/vDyRT+SJGIuMWwjRTmwsnU6F/E3XVyBaaNPQW5+oSJMBEdqWROPBOVWwzo54j/5OFL6OJcC5C8/1TUbwpy+nFJKtdWAC9AGi4Fu5pMlFu5NnXsjQ07WNgc7YFkHNWt8XS35f2J70hi70svsatMACyE7iFx2xit/CZ+8/4qD+HIbxdMoAHYDTo8CG3yI42S/MuUCls/yN6yoxkg+icIg0LwF0GXgFNAL4AaLlC+7nYPcjNhbTgL3YN93UaUiaKcN3eVz+E46bHDUBHeit2GuXtl4WRtasgK86bcre4N0vQ3T/ORbeqQeePvGICpk6R2WAb+ELkwDtUAL8AaLmGcnKptQklCpxFyk+RZuQjOQlnaJeKUIgeTqDnTLwIOY96N3tZzEy+0t2N0rbJbFS9fv6Vm7iZM/rkiwpk1YxCMn+d95qFEwRPBOTUmM0bxX4UWgDAAGi4wYAxYVoliXY/3E++LhTe3MH0x3fuuBAvcagWQ2XbScrT+nTgriBeejc3comjke9Qf3DALqhw8JvsMWnWVTKFCXeFOox88gEAKflKCxnlHoeQPgNawyMRnFMAwQBouVd2Oj0IuVyRbGc3qTPjtWmri8rzE32grJZNoJRyNS/P6LmXLqNWwmShDHR7XXJj5Dkqt+Dc5Eht4WOX0J/JkAZwR9MCrtySGWUElV23ehI2FXp5nQ4=</base64Binary><base64Binary>E6g7PQEAEAELwF0GVQBRAMIAaLlw/dCOeNTnCfygytkje8leDqB8Jhlasmvp9KMV/mIfp/FtdwAAo7yc952YWTpVKljwEfZ/G+ljwzBFvMAQZwFCdD4a8yR4kM/ulpucT0IbC8BdBgsBWQDDAGi5i2l+31tML7DSr0/voHjiP/0pU7sjpdmHj9V5+okc4JhfyGC3EAuAGwH3L6nTr9WAGUaOHKWQ80ponDcFC6FiNlrS8/XaMOBcryp0HSNnMQu4iPtrQFWdUgDEAGi53Dedbasb2kdzt1qbfOAR57FvetPE/rmuM6+bZovTy9cd6NqoSYMp1y/RZqXSt8hU3vIAnWySkN9vbPvZDmI4S1fgp70L1BoHvXA6yS6eCJRUAMUAaLjBc14jTX4djZ8EnAZ1RDE5yUlc3hNMOSgMwuK/SlMJsiS9HzB4CJFeteZPjUttZez2PgR2HAFXO0hjd5+bdgg0aKZY+IaBViAXJICrdSUdKnvk71O9gg==</base64Binary><base64Binary>E6g7PQEAEAELwF0GrQBUAMYAaLmLa3BvtA5eBOt5S8FN8ejnQ8HPGXW/2QtTskwcQWiWZ4Dv24HuoOsZmKkded09cN4X6zPMW+BxhgGL1QANLlwy25OvQY9F6O+4x8jGRltKarmVUQDHAGi5pYc2kwlR0JuzzBOmOOrZH4gkZXdhseefgt9TTj8RfBaqGE8vcK9U9y17O0W54ihkeu9Rbq2gHJHCHmRpg2CZ6LnEtxGpo4GX1vY70z/JHQvAXQb8AE4AyABouYHJlODMcpmFwkNJIkSz3QFshE3xpEye8x4e4JBQBd2wkzM/gM+iIr+skidzvLTgJr9kgp+jPeqTmqsM5BClisoacls2IK85UQ2RkRVXAMkAaLikLed00JB1Wgln9w5Q0ZtvR0IZxvy8KM4iT+fAHumfc3iVAAxC1s/hZnflI49UdLoz3D9NfCmWqkpYvs0QPgF/VGC8DRY7etGFYPH7cR4pZJvLbJSbSwDKAGiWLSkyXZNr/raBA7IF/GhD7WeI2zFOHaCj0hiHupFjBqflI9R5D7BXN7rxyMYk+xKUjypUSBkPWUf7U2HsV7AS7n7Vrp34xOnZVlFVQhM=</base64Binary><base64Binary>E6g7PQEAEAELwF0GmwBLAMsAaJYn/fCyM7MMBhVBte5F9tVJO3zt6QBl6fqBdwknU6V1/ZZYuEo0hxrpbIrCr6Xs5vUkXzTfWQqCZ6+Fsb1dRCLrClmVhS9zuvzbSADMAGiWLSj/80nFpVTn29Lu+wUfdI1X/k2flksA7Ibz/r9JlCC3N9JlWT2gX3dKn8c1wo/3JyWUYjbCrph8C4NHXghXO3v9+mxBZAvAXQb3AE0AzQBolilb5B+aKb1sNWMTakDz/ti1OJo5JAhGV6N2GzzO0I7rX7LP6T6GWFcWri+ujOWVVDcYVfB4Ck+8KpPvcQzv96mr+cOWsIdGeOPqFlQAzgBouYtWa2tcUZP5FqGjRI0UCkRw7/EIP7VbZ9FDFzyYwDBEY+eqwo5Nu/wHR3MWUOHQPZNTslDSw/EPDluV9I81+C5xMyRInjlZOh8J/y3bhcVNFV1KAM8AaLikK162RxKyP33cm2L6IzZqlrwAs0GNeu65KuRM/Qgfm0+4VzOLGQDDiCS9pXjuHdHkDe/+YrLmdmYZz01tBZKBfTZLY2XtQJ0QcbcI</base64Binary><base64Binary>E6g7PQEAEAELwF0GXgBaANAAaLike8vKfXl0Iv/3TKdkubEu2yM+grgxVy0f0eDr4pwcE0yx3RjoovP5aPEW27/tBNL8tZHvgDdrQ2NHIJYygDxINJPTlVdf/9zpVtDdy5/zdfhzDP8p0nqmC8BdBhYBXQDRAGi5i73Jq2oK3C2fSvkL9eehV7zJv+wAGFIZS8xkqktaMVQsEoWbcXNYeuFHYguCLqIB7aaFm3I/hHm+oGIeeF9uByr/GHbaa9V2TGTtwRlDZEHqj//YpEPY5iOVlFgA0gBouaA2WLWRqXeDcFTW1RE1PdT7CSgt5rya+TPQNT7b1YhuQTv22R2BNNzg642bWTJDjdv7Tm3HDtnkpplbmAKT1IIF08qqdRRxHS5QQ7vxT2Ba6pMzSrLuVQDTAGi5n2uCOhfeM5m1vvXTbfPkpeuyRNrjzuLVaqWC48B6SapW8t/mERoxvUO4+WCvfrpJad4rtKd5fOOz8d4nHrp7ec5ubbELj4H5KZmp0w8cLlpC1p8LwF0GzQBBANQAaLkzQHLHWUaImAZjVCgjBc/c5fH8Q581C2dnaRRP54L1g6dBaDLojivCA1GjoXQFVGH5ZvP76pkjcM7O3N6nEiwnANUAaIJhBbL1c82xcePGVO/V+gbpUBnYmzZoGdi3ezMvgRVLw/Ge3Zl3WQDWAGiEwEJ5QJNOMj0i2Nm1M1HACq2/coa58PLXI3UTvEtRF7uhPupWJA0nG0fKD5tShRzqpvxo955JH4o2NF/Z9MHF8z6Syp0ChKpAYD/jOKsMYMgTNs8LLoyBE6KxKw==</base64Binary><base64Binary>E6g7PQEAEAELwF0GcgFVANcAaJXA+oo9B1tLNWP5vIJkyPm9//YJmgU5a4gajXyLlYC9Faiy2E3v9vyBMYU6I5XLIC17s+qD4m7VGvXEAeXCczk48M/yIfhBnHcMFWWtKIF4SDFmQVQA2ABouG7FYL1KlPrPm/qCnWsJuuX5lGRFzSHNX9g6fuW9h1QHckoYJBvHS/0m/YuIZ7byCcU0V5/U8P3BlBLNGVatEzf+eEhk/Rg9IlL+BtFDfsPncchfANkAaLfx7UUZV5x3MzHS/4+pPpT2mRwcKx0sHYHCvmpNORITc7qliZPrZLCoULV/IA+pt4MvIFa60pSd0sMoMPAmL0PyupplEiFpwwOAs7eeWS7trpL6YoFVz68q16PhhUdaANoAaLt1xF6xXADlmWWzFtr4qWncZxOBIPp8F3gvx1N84mhhGN0znXrXzWN1xKMTLiFfyGZSb+nNk7A/yghofyy9zz+Y30gZxq6jX+ayY7eyFXRoF98gluoBV/QvC8BdBloAVgDbAGi7WSfTHBrQHl/wU1GJIla5rWyzPegKmw2hmTB+j+ZBdGfwppatWsOvsdJGedo2M9QKMv4mv2yRVcOv3/jan9lMUNkbIwG5tSXcf6MrlPuMK+/G0nJxmTqhFQ==</base64Binary><base64Binary>E6g7PQEAEAELwF0GvgBXANwAaLtAcjiOInPZL0wU3whJ2F6KSvUmkvPJyXIZKa9X2I4aGiagMgXRpTef0GrpkTiuWmz1jSAGwygCLvEqW14CBzYo/YfqYGl32kjYfbpgz1GTBD2st5dQXwDdAGi6oxSRAihVwQNmMhTV/4oTLD6/WnijTNYWjWZ51YNdR0k0QsLy9rzoWowJQ+/RG9apqFnRg0A5GUjZOP08wcebM8QadpMW2vx4EVkbxyOJccDGLyL+suk0PanW7mbGC8BdBhwBWQDeAGi7+OzfZ4+Euc6HoQ2sWDcsnOMEwUAnz2gXlDmHmtD7v+/co8B5CbFhL9cl+eEervSSzhiaIlnEkxOGDj1r6HF3ehFtfq3QOHALAFOXvEy1Z5ZdD5FvsAmTVQDfAGi79EkMGtagwULM9cK3KhkGD6BJhuNEdYeY893Wq+JPaFK+vhyJt15dyofFXLMG1lcLC19vGUtpBgWzbRzrg5lWHdBpdbm5/9/aTRBvdrY/yohb2XpiAOAAaLt20qRMpFcCIqaELIYF4TeUgh1iL12XENU1HtH5NCtaG9YaU8MkFiV4XxztHZ/MAIyigcL35tC+LaWC+lir0UQPsN8Xp7h1kXvhxEG/XIdEkees5ua2AFn8HqpwKuO0ggn6kNlU</base64Binary><base64Binary>E6g7PQEAEAELwF0GWQBVAOEAaJcUUCn8rUpUEOo/5yqijSCCdBIlsxQKkGJnAfYrokn+6LKP23Y9RfNaz66VRcGt3NDGf3dIpRYgjoxecz9HrvEr3isYL4wOSkT5M3HdlMkt4SCYSQvAXQZKAUsA4gBolxPAoBthnGrcUSgwk3+ty3ckYGautEkoy+vziSGnhusVJaHEZz72oJV1ZHw4pZ+EzlH9/Lgy+UJ6KO2cc7RVdlPh4z9Kto/uVwZNAOMAaJbsnD3Xn8F5Ab/aGVTFviZLDvj3rDVukI90B4Qzuu+W6/EC+uVb9Hs+t8OWi+ZaNqeDOLWsMGE7zf+M0X5Y6GeP1OXLzo3d8FbB9ApRAOQAaJbsnD3XutlP8iVhvAA9xBXOZdY1amkoaQPka4hWMGR3xNG/Q0LpkU+W7m7YUUSiBUqA/ozBzNHw8psvgBVQn1x1p382luxj6rZyHvnGBnTRUQDlAGiXOecnx7ei47boUKIm9iZ9CBzZWNdM+PBr4wYXROzV/qRbXeCXbf1LiESCfoBSFEOB6aX3Wcpda+Iv5xKoQ9d7RrUOAnNWzCBU3NtuDY1qGg2dIeI=</base64Binary><base64Binary>E6g7PQEAEAELwF0GAAFSAOYAaJbrZh2+/bs1J74YWacEglpcnmurtqKFiM7zSU0MZV8RFAyS7s6s1Fxjv2Ss/45GfkQgeoILjJaT3c74vy8x8XYmPeNIkPAFxXRWcQJ9SQscOVAA5wBolxdWHdMVC9nnGqoO6QCBYLha4MuIm6rgIz7PD6mdXJoBI0v6oTZGPASRdarluXmKLYvRMTA1wV6wLlWbgG6RskwWC+pNCtrq1rExkb/xFFIA6ABolw8msWcI97LboaKSTrA2LsEGoURKjQ9KiWcPljiYGq1wXTrnEq093cawDkSjz3x5qm98C5dAoynXGwmqEAeJKd60qQS3mtj0XsU5lkolU+vIC8BdBlsAVwDpAGiW6zdsz7CzXyrPCCnesutkty4XJeGFkSOf1TDZq5vpeIBGFR5wa2+pGL9t/7/rj0GCXhK/iedW3owcSIqghqe+7Zeo/ENoixIutNtdVglUldQsFLg5jAPFZhE=</base64Binary><base64Binary>E6g7PQEAEAELwF0GDAFSAOoAaJbrN2zPMuusGnftZsBkrbFU5sZ2szbRhlnjgF04H104puG/WSSbEMHcFrXLPnTBfsFZ+wOSQiOtB7MS/9D/gHzhm3nyB9cv8zz/lb+c3S4t2lUA6wBolutmJVdV+mOp7umy+rb2W2tHbTOV3iv43rAJbG+n4Q7VV1tkYm2mw5ElgUDfXpHx4Xh/oUGs0nIdGU9UoI0ZFCLLoSzAfkJTJ6qfgJMApsqbfsbQWQDsAGiW6zdvuBN/Bl/LClp3sj5n+TamyotFNiNgzfex1InTZZC/CrRhZhA1pVV7DdmHV8bKqBYowjbqov7WUQQgkuq68c3RIjW54a7jXhHjKAMshqVWRWlz3E6aC8BdBrUAVgDtAGiW6zdvuri7tro8TsBgtcG+AG+F3a9aGLNvnznDu2A25KzQuYkJfvtX+d294vTBr5+lipdCjXiV5RSy+DYPvWmInzdIx2DCV6Tsn/FUU0PNhxUrSUVDVwDuAGiW7HtfNG5nlsLHzX9JRDQqKY9lBYRHvNbQvHgKcZzAb7Eii9mfAaF+7RoXUT88c4PFSeBG1V3O2zGAtcKbTXNfcvcWfOcoo0dU9FdjasyUD+hHwrmx0QvAXQb5AFIA7wBolutmHd6gwSe9F4A1nT+KA0arG9gdPC35f2AY92+7tqUIXek6vTqMbhi6Bw88xVS3MLRrXRJ6qXYHVeB/8YbxOsPd5wQXeWUtox3EbjPPXeyZUgDwAGiW62Yd3gCd906t/JPqlyT7NTm4x59dktbfIvjyQvQupFKlKgcdWeRvJW9f0aiqPE/H8H/hbH7LMDQ8cdRB0UcGKyQcGAkHoJhQK2Rh0xb/bBJJAPEAaJbrZh2zXw1UagVt5CcHVya6pDNMFTjrf5BUWu7dxe/6tShFyzodIN8cWzTgjHppoqwPXzdArlid/wFoDxecTdvTux1YfKC7EcpOVMU=</base64Binary><base64Binary>E6g7PQEAEAELwF0G8wBMAPIAaJbr9omc1M+vbogjYkwqYl/iBgP9D7H1n3nRxSFowFsx7QCcsVNbxfNx4K2KLghJbvZGN+QH5a48bzBionYCQ62JEWYDD+4MzXdDCUwA8wBoluycDlAirEfuecvyD8yV9fUZWeq2T4M80qHj6m9egDYlXbQeKwUIcZRgda2D5W1MHCo0vohLHzQDucdhFXK6JY4LZa3THLwkrAqYTwD0AGiWoIEwYi7Nk9OL3zks6etGuwfW7BwOJmHYhLoox6EDASYdiwpKjixmRC1/XMT9/qNaDcXLAeKU60VEIUwFkJIHq329NYOrMFjAyf463NELwF0GqQBPAPUAaJbrZhFvokI7RjV91KdOccQXFWBLWmn7zboKFQ6Aa67mhz21TFw6g9uwQgfvgIHkkWr+pD9N4unhWdPwoJf3lbeyFRQBQblutCQIAoDL2VIA9gBolutmEWf6ayriiRiuwDDslzICwBn1kBbsc4HCW2JKTsQRW0dADXKymdGJpbSNj979KS75QYKIsztDzbWfV+h8+7exZEy1NvWtbqbkMyXjCtrBmzlf2Q==</base64Binary><base64Binary>E6g7PQEAEAELwF0GAwFTAPcAaJbsp0fHrys3euT7+Y3uIYa5+wj2e2Xp5BV570oNjaqVIrOyvLj4DnHXiw3QESIRJIPPdmg1e+fq9ewW8UP7VZdr2o+K8YlsN8XUfgmPGQVa+YpRAPgAaJbn0IXQeOEKU28ds1U42iq/SazTrqwPfRUhrgcFpObxdoy4CR2ZKuc0BM90ZVRdUTn5jXYjYskPjTH9IOR84rFoabi3tjGAVOjTbv8es73QUwD5AGiW7Jv/oWgzKTfWxpv0KlD8dP+9/8864Ua8UCF0LUbuuWZpYu0RSD8s91FkEexA/EPvFM71ae8LjEND75sN8wPJ+yiVf7B42lZAvcqGRgXGmrgSC8BdBu4ATAD6AGiXF1YhWyFIIYoEO+q7kxWpIbGFO38le5nzHKTAri5kHIMMFqfKSrvQxx6k6Y7HrpYuytI4X5btKTGHKSEEeDMCXj6ec9q3AI1sB3dHAPsAaJcSVJe55MQT/1FdA3SKSWU524zBFlBmVZhQX+H+SHOcKHD9J1bfrnvRyD+3hOUL+UFthjk+l+QWQEGkAOESc17v1cJcatdPAPwAaJbrZhFb1lQ0HW4OEbko0iO0Al/oytgQLSq86nKJv6CwFsbAB5V+G1Lbtu6jjJjAr0hYiFpKRfCKRwlHzVDdVcv9d5NU6F2Uv940RIaTDNgzECA=</base64Binary><base64Binary>E6g7PQEAEAELwF0GpgBMAP0AaJbn0IWpUO6ffkKdtEelQBHrl8E82+Vu+Ocu8EH1VNcayMvq90QK6rVxA0y7BAYItHCaTIQP9g4ngAEA3lbTR2LliTellFHI7+CF/lIA/gBoluNat0nB3ANG1xsJJJOTllGMRf9LbCRrsFZgn61TUZO6fO12FyRaUXv3KMInncl1QVaUPIgbzxbhVi0ofHuDPFjfc5pvyryWPdHKTrFJfhZPC8BdBv8AVgD/AGiWqoLw56DvJPwtxIPr6nfRE8AQ25YeHAo0f+IvrHseCtEYcu9X5H/JGbESk6gcUGcq5OWKUmqGElr1xn57TT6drWLUmyo0qPZgMaR9Onuk7EMxtSWMTwAAAWiW7Kc6zqHBwGI2B7SUxjbwqtWNIkMUGGbJKcSIWAsjfgS9Tbp9Q+SZeujPrI8t9A9TvFuBtVin4MDkfpYFy8flV6SGV8fPNKe9qOL8o3BOAAEBaJbrZgp2CINT+zBixjCHSN9HmQRuhXNtZ0o6AJ3zAs15bD4JBM8v1/DEOVoKLuoBlCfD1lDwZzNKEeLJrnQiRV54gZ07O5jPSqcEJVOoO7Q2Sw==</base64Binary><base64Binary>E6g7PQEAEAELwF0GUQBNAAIBaJbn0IHKqSc7c86RuhceuDbHE4OlUayERUc3g9XyHFbi7AidgBOnj5pC9FckY9oh3N4VAbGNuBFm7BELxPNxXcH2LximzU3NcxkKgm8LwF0GsQBWAAMBaJbig6+iO3uunwo8chyJu3l0gJycLqL1h3V1zz6Sd/SgPPUsz1HNXA4P5NSjpn4C9/jPF7L+X54M6MM4HKT9LK7zyzEpFqLuf7kllsoaDKH2HN/MFgNTAAQBaJaU5xp66u1SN6zpNBtvd45RTcu0+kkar6cocCL61+kGDx7Ldhq3wfJu386H6Qqxw0askBpQkzYZb/RTRlavr3G7bJz6EbJ2QAgLV8qSWHf09IELwF0GDwFRAAUBaJa75ynwz6STJiT2oJ3QU/rXMrjQ39II3IC7aT11YOvXbSEcn7uSZ92AjOVhGHCcezx1ItnTw2uV3b3DGeekq5UyNck8wyr7x5uAjE+7lBc4VwAGAWi77nGi79VAv2uju+cDS9ywVaQLvBdG99wvrk5oLAmKBUkiIL6oeaFrQqY8P+8MpJP2lAJ6VokSLW23Yqu13z9DCezs4OurDpK88FwCPaRDhWdaLeMt0lsABwFou2bl3hshqse8H+58rzs1z1Nk4xv0JtDKwR6+P1zNH+DjAYyK3GV26dM3j9lh09pkrDrEibYWmvsNupY1WkTvoNXYIxjDA1jM95QCBVAqbaufr/l9PgcwTJFJNmDuMQ==</base64Binary><base64Binary>E6g7PQEAEAELwF0GGwFZAAgBaLtdyOf09IyDSYAOlqtqKmQ+hd68bgJP5jzr0F/uMmGMilEjhEW15ilvdIQa7ueKmQsmYC+FbRjpTbXAPi0Cx2a1UXJsCGngXDsTRFMaeM2iaHMYq/Uvg0JbAAkBaLuKOeaIdTl3i/cs2yu0lVkmnYgtA5jUJ4H1dduLLQUTcbeHfHVFQAxDC6IW2xHzKWIwLMdAeAb6qtwh9GWW8kzCni+8CHo9VrGqQn+k0ceoMH86gk9gV5fJ0FsACgFou21TkX6Alrn1SKVm91gCKUdbRbMC/wjqkGRfGBTMwzFZd2dSJhSbi+QTVfBnG4xEJ6XjG363TdgWdXx4lFC6DP0qhPJz0vzbbbxQvTLegS9e558/x+VEVKiBC8BdBgYBUwALAWi/3iA/ENxuymjTC30sjYYo4xhjSeHlS/qEtnkbfq5AeJ6lyIYVNTbJo3adhH8jCqmb0xcG8cqSYRFrLfDox9fzS2dnX7pbAC+9p6ei7b7OBdy4UwAMAWi7+ZDGuqgA1p9OlmNEgvCT22cHueadCIeC4KmNoGH/0tOujilT3ptqezr3dAKWOKRcSHhyGX+cicIWL/M1hlFuAtt7+HPThHzNLyZAQd8KUil+VAANAWi7+LwecGQ/3GIjla+Rd4NvTJakMSZZta0nrXNb3TlRf8eJTNLvZ+NJnn4ZUen2gQ6to70rJ6O1QBUxPJfosodjM0RrsCd3XALM6CQv0YZD5ROcGPg+Guw=</base64Binary><base64Binary>E6g7PQEAEAELwF0GrgBVAA4BaLv4vBI/7TDbKCsa7RBXRr7RrhwidWsalboCmPXO/4uGr9PhSZPvQMeogz7VJuiRISUdTrRJQ7kmi65XrnnXhPPiBr08oA3eCAuDcuS2st3rC5NKOVEADwFou/i79uZAIYwMdf5uCEseWg2uaUmcV1YuKMl9ZaudyOti/UQLlxyJLtU4HoUwsfR95OSmArkehmxxce3PW8tMD+clSdLdx1nKLrlPplbwiPkLwF0GBQFMABABaLv5TwThBDwW2irlMmGhOK+CGgJRLUkp3TjLQ360BZPcVlhmG8JRHa2EB6JCYLIB48wzrI+2tFKbmhE0DW42TplWLjjdVo31jBlzCFUAEQFou/cezgd+Tqh6sQPK5rsbqXq6vhikud0oinsI7S8LzOSl9ptUCVE2IjQWuzBVAfRIxywhEH0iOeGOuDW8e+DeSYeJCzx70SD9BYl6069JFpn6xJcBWAASAWi7dxSf23bGD/5RPtg/gxCXyZ1IMXJ0SaFNCfrtP/kerdWPCJOWCZknyq2lTuOT9mfLsyqcjrsIZZ5XNDN55yxEhLqUaueuP4H38O8g6z12WRghUElK04DrU3rP</base64Binary><base64Binary>E6g7PQEAEAELwF0GVQBRABMBaLv0FwzVmXzvfwiZHfbc9enCC0R8ZNy0youaTkpltPEh+tjquVySGWHwjzf66LcUXibIvr+6nS6c59mWPlusyJ6T2LHtHkllyrazHGaYnakQC8BdBmQBVgAUAWi7WkCo5ki+eqst/J6VZTl5vQ4LWcoDMXjtnQOVazT4cKpeCh5E3m8KflUBxoi58C1ORVGqE9uc4rl8uB4KaLxvbwpCqEZ8i4SZzP3ySbNSggC4MKD5UQAVAWi7bgkkVMJkMCQV7qhtcas1xOFYICcQslzNRMUiu2t4Jm1/nm4LX0YHngjoXPKPwQUVXXx3L2CvxY3Ltrmf6OSZ7Zn2g7yJoblDStaXbz64NlcAFgFou/js734H2jejaTCNgtWW1PS436K01pkQu91wHsIqXBEdiZpASbWoEQTxCha5dZg7f19QItspBwaR1FXS3K+XuGrnfTkobWx5rhPGdMaKB9kKIe1eM79WABcBaLuKUuphDnYGrIzeroHVvpP0beZvsmwJRfHV2/v4jHCWWkCnGA9OsXk9luRkhwBacnIWxSzVi/zGdi/Zj/qIM6gIts9+M7QihdNGJBWFCo/SWz8W04huAnKn</base64Binary><base64Binary>E6g7PQEAEAELwF0GVgBSABgBaLv4b165uxoXxHPfhG41UutbG2B6ao0JudnRsJyKud57UaJjiLPWDFmY0jS8G/RNOziEn9kqTvLhHDeCccKolLAqfrtJn1Q1Sy6M0ZGCcht06gvAXQYKAVsAGQFou/4SYPZD6l4PZuZj5pI/2amd/kNJIfmgkUwnn0tqKQ16J5dReiQeaAu6bew0D0C8Xn2ffbJQ+Z/B1T2q4tZKpwKR9bDoiyR3yyhspUNZ8zx99C7/DgInJQxDUQAaAWi8Ig9KtUYgukt945FclrHU7sqxV2p08UYTCQ+qEGpU+uXqm0e9Xue+ziPskr+EoAFShyP4vdlnmcYfdtA0F+yDKTRY9x5Fg/ya6dK4otMub1IAGwFovBfrZIGuXCB/mG5baIx5crgZiI8mL73wnas09poTbJF9BiVg4qraQHmR3xtI4OKzhTOZL+0gsMVlD2HNrKhhvFHbPSmWQbEayLfG2IFfy/hvzdsi3w==</base64Binary><base64Binary>E6g7PQEAEAELwF0GCAFXABwBaLv4b1V6RUkwX+wbWHwFPDYAJ4dHuioQWMj6D2v0VUSXBbKY3hBkgF4xYN+cmp8myRbQGkB3u1O3bslTVc5QmvINUyXUMn9vCP9YFBWcz9S8+tvEbLmBVgAdAWi8AZVrDfulW2S4PBXM40oq2KPCD9AjI7p1PqRX+aSV5bLhaalG7MOPeXziSk2CIa7UCkja9Nhx4nfvU39jE62umxdff+zmRsHFKqi4PyDzELU4+SxxTwAeAWi7+OojpOxjSQeG2ofegOgVwhCjGbA2hlXz1gOO9b6EfPySP6avRQ7pEUzLXKXjJoCbJbDeowZOhiEsQ7Is26n9dVCXNYTCdPNxW4cMQW8LwF0GqQBWAB8BaLv+GvQBWdjflNy5B53bvL9whRa/WKgc+ZVefN0y1/G3D6zLSdvjk/Fula3313NWW7tYWnF9hJgewdXZYpSCdPJiHE/ZYB/v0gS2WUjkOMO83K8Jwj5LACABaLwh3cIfnqMS+k/d2I/X23/q0YAakOPOAxSL4zqKSMorXOH+y/0K2TNX3EvzrXWipMjgZMJ0l3iDXT8ve9w26P9DyICKrJSKkBQDC8BdBvsAUwAhAWi7+Oxy/zi1clAJJKchXVg/bh85BbRwJBhjcgZ0GcKoUaKj/cN8LvsMSGZ54j4yNYvF3ejY0M15nfNM43PT0gyuRxq2X32z2K+PY/yxb5FZJg9vUgAiAWi7+OXIxh5q7tS1UcdwBPJlsdIrrGNXNVQ+Sf3+3YBk1H0fSFdnmInKE05sIZp+/Y8id8m69LrkrghPI+GT687oGVY+18sMdQ65EpzPMTll7K9KACMBaLwihkZwemkOYYgMZKsbuaPV+kuTwDhqcPL3KPfsek+r3+9vdDBnfdsI9Yyep/tFMn6zr7nHhWWwOyhHjafaPwp+Q/os8rDfSsD3h1GH</base64Binary><base64Binary>E6g7PQEAEAELwF0GqQBVACQBaLwihkZwdKHkxlDhuUXIm2mrG6i9mCV37tFBXQa4mNAd0UdFWARIjUJDK8hNugRmxUalkagLrDbyBwCdj6pYYmR8u8HgWB1pf2eG9wQD6hBZ/WgUwUwAJQFovCDEBiIINwhmiIELY4L5lZPJ/jxRQG3upvyNlTfazmav+Gy0xzGC6FgBFgb+ZjMaBrXzGWQx849wrcaio8FCRQZAZW+IqzBiNBUAC8BdBv4AVQAmAWi8AlLu7+blJEwaBaSKU/l74aOvlO8tIhToQMIXFsSM+adanamW5/n4/x8CABoHCy8PjIo9Uii7NdsymFSuRbXRIKdwaEuMJd7ASx/qr4DCt2ET8S9GACcBaLwdwqKGVTeV8xad4lu/nhOtX9/n5md/P8/bu9WjnlLPolCXhSUzpbCBle1903fp/HkjHYpIIxp6hpA5x0r4HFIdqLKRQFcAKAFou/x8oNhK3x+hDLlzPqq5E5zVx05WHuYcVYFGKyYjNeShZ1hcZlx+V8fjN7yzieL3mjjyju4hBZK+tuPFyeBZAuydy5MRFY6j1jIpkNuqdFRqK8B3Q4Q5AFF/</base64Binary><base64Binary>E6g7PQEAEAELwF0GTwBLACkBaLv3JbKE+1gTsPU9lMWPLaxTPGU4GAyNk6u8CJJf79F6EMlgQJ4QlkDhYPG0TvaWdMc8Hu4sHzDF63An6tQEsLibp0i5iblgH4E+C8BdBgUBVgAqAWi7r1ARUoxZpXiSDuYfjIcQN/yUfXJwN9ecM7XtfIDUJ8eVUZ7pADHoZHP3555BOrdKiyG+jcwBbHJuGtx2Aa45ESilRYoZk0UP4E0iObs9hVmvx6HoVAArAWi8Ioo81yiTicf8z8yITI03B0h4v0XEaE2zBSVRPe/hMCyGP8oQNFshK70YHQFUie6ofHTyiOZnDX/rKHcQkFkP/7k7k0uGAc73pbeC3RFWxfWpQE8ALAFovB2mty6Cyg7a38fvHqqhEtConCXKhC6najxlEFTCPGakfSNsQEta54/hCBj5NYNVH5zJeyfI44aV8JT5ODxOuRSk4GFIdAZEnW4e1d7LsZMg/g==</base64Binary><base64Binary>E6g7PQEAEAELwF0GXAFTAC0BaLv+EmxaPDNAr6E4A9jXIu916dsSfwjcs7DZFMyH9uu4E1KtCtTekFLjJbMF6usyUufIrX0RQ+B2Sbg8IrrS/nTFxhQyhjr+hhWz0q9dTEx67i9WAC4BaLwmHSK1DY0mkKFpUU0kDjpUFOhZj4mRWnAr9Obevbvr+00Mn50dJMip1HmoY6Cxq16IIHJnr4pv8KXcsjpYx1h1POf16NCp6Y5FwMSe+ejwo8SeyXFWAC8BaLwYEMFalI1SZnv8XlK5uEoZfoEgn41r0Z1bbt53UEwaOa/+iA/ixjd6V/1q9n3DSS3Z/lAJ93PQmnRl20Y7chnh5Ns9cMzpkIPMPs3x2H2EpOlSaiZNADABaLv4ckNfyECnaAKNbN3Lf0DuBU3rGeRg+df3toZEF9g0bLdISlEWy8/Xp5YqQsV380ulVrn8BEPBnJknuYohKc9+va5QNhsEG2VIFHYLwF0GWQBVADEBaLv0SDAjCR3GK/wiEe3nsJDBOn4e2hkGdDcOTz8yIUYRFbsG9MKksD8+Qf1NXPykeeaMjsxGJPHFQgVUp9GD0eDtGra6Z9RJPWHP4/dM0kefpRn35ue+h68=</base64Binary><base64Binary>E6g7PQEAEAELwF0G+gBSADIBaLu2+d057BlKL0k4Eakt3877wB/4+D2nIpFZ01K6KuclneNyxGPveaTSRU1q4nen1CT37yexCZbPPuZjXnoTTtzoOKa4m8vMzmS6XtfQ+7UUyVMAMwFovAGUVZVDv6Z/XGYEJSEzMAE1T4QKjG+NzEuMZbOvkZaTESM7Atv1EY8yNbrm8EjBjpWKOIyxnJRejVDfd/J5LdA6gYKZbsx26n9bqeK8yQ2NPkkANAFovCKNrxRoXzKKruJhqJRzGx0iiEi3DU6KhUgtGSZUtj9bs8cvGcDrmYgSIPEy2982GwBlNahA1mrWuoM7CvmcvGeS8Wltm2ZBC8BdBqcATAA1AWi8HVegX1ven4sBlC0fJlRs5XKh680Iwpr+Dgl/XGVtiT88NdcF3U0d4kiV0uX+FKikc7QHNncg1aON350cPE8VBKx8gSWd6oGvvB5TADYBaLuKULUOCh1D+G9gNniQK5ZGvowogjk3DaIFeZUDeqNkcK+22TWkrMgf5w/7JCVaBEN+nNT5fPdAvQHfenFhwa1/zs5C/fcN1QpzxOjE2bP8JuTOyIue</base64Binary><base64Binary>E6g7PQEAEAELwF0GAgFYADcBaLwH9bliy62CIPGE6HkO2mi9hzM2hsvtXraK2l/n7T2EAjayoMiHH4TVXMaixSce/gmpgBYMmno5boMvKmatHizsXaJnNlasb3rh05BN4W9d3HzlZ1KWgE8AOAFovAGViHrQAJS+pVWOlyxGN3dAzt42YMtmxGB1037VhLHc+1IN1LdyiPGe2xT1zXcvWZLFCuTiSAvMCH+Pi8sazLY17eri6yykcBOJN/83TwA5AWi8IMTfmuLixTZtX2XUpsGXCMnMg+qe5+vnPzz5oHPX4HIxno98BaVq5NvduYbQJF869fjWdoFE3BHDc5GLFfBcZ9XX+QeXY8Bf/KUJMt8LwF0GSwBHADoBaLv0IfYGSKJWsvSFqlDX3xzaWX9nn2Q+LnoPKFAHpxujrYZrDbPXEpdc5f6Qq4ly8jkv06UnAy948l6Su54LRF2xaNb/7b8LwF0GSQFPADsBaLv47a8WiMg73nXIy4WGKCFGcG0OAH1jNoT+KGaC4yuoJXBE1vYyXkjNQ6lhPNEbvbXDwA7miylveRA4mxncXDUCaq8dpQtk5I8UF0w2L0sAPAFou/juBiPk5TwqL8FM2gsTzny3B3nBkyAzQlCX6RDgmLLMhU0xm1h4Y7ofnEVmPnqa2g9u/giZkwgV9I7R4v0ahLa957vjJB81FudOAD0BaLvuxiW/AhCYxu6yvwgGM33acOnjwKYPykXMy0Sff6unouTyNVLkadNMmwJELmYFUDYPD5ujaawRMWRy1mNa8MIoua1xQGSCgJYFd1/mUQA+AWi7r1O0XtlNDMsitrSq1kojWenCrojZgfNn2m9v7Butm8BYRxySaaPppEQUcqwubkM4x6c0hX7jVzZywpJd17iQ94BCTaT+LxtaOLlSML+oMGLL84o=</base64Binary><base64Binary>E6g7PQEAEAELwF0GUQBNAD8BaLv4zRxWWP8GX3GmyjtiQ2Aa9fWoVv/FrRDsaIISASjiPWKDt1dS0Kot0FiV03aBcasvKPiwDyujBbu7IFhv/bAA/tnszBOMMaHzx38LwF0GBQFYAEABaLv46dyxshRZcROj15ahYbu/ak81rMho/A04YP5zVnRsQ5zZf+ZF1eTDFD8hokHUWt0Y5osgKSW6saBrC6EQY2+UnA2fzk7+u23T8Zk5PlC7FvyTPpIJOFAAQQFou/jq9AUgtTqn8W4EQinYT+dRXKd8RSjC0MeQhFhvpeHwt3xWwmLzP+QV8POSDOgZusR+zrbR4zuQCbM7J4yWLh50tWbM23EO3wP9s3iMgVEAQgFou/joxgkVeVJAXjoSaTXXoVDTHox3ZQ2oS2NZGW5HlG8jOAXeqIFMANG1rZMtSRVlwQUQxyg2n3oZcvsQBkII2rzzytCIARX2BUywG+/2Rrjy5i+a</base64Binary><base64Binary>E6g7PQEAEAELwF0GAAFWAEMBaLv46eSEmEW+ywPawrUGPPWPvFgO4w3wb4qFKEjXGFUwuJ4OJe44QOZjHv5ysdx1a1rX6a/UA9Y82/kabeqlA6hJW5cABrwxzJujO/AtPg54pc68nrlPAEQBaLv46MYHffs+cDQy4Z+RDp3fPXtmuTsIZMLbjhOCAzeYdRmSXnRwJljx9cvB6Xz6DpyUe/FXOQmtUlBZz0L9He3FN2ivXat0NocV606wLk8ARQFou/jp5H7FyGc4quuizLbDhqwGrJ1vO+YLu4j4/kIXTi4uYW88oEw2RVbWxVyqykEU3sZTURIYYbuPKaLp+UtqffWM2tM3Y9OjwkimHJP+C8BdBqQATgBGAWi79CIPZSiNNw+ZQHvLs5UrqMiYJ7o5Abpq7jmHtEw0tYj5kxHjY9IRlp0o4KxSqtqwznPT4o4ld8nJmyWJF7AIQ39rAANlLeSMrHMSQE4ARwFouyuO/Bit5DQCrn7/hf48eTWy9d6WLHgS+BbuOzh6GBshGJ2geSkECtjCFhV8h22xrRM4HC8hjciFssG1FlYK0u5PjHYfQMYE6DVPl2WSSPdq</base64Binary><base64Binary>E6g7PQEAEAELwF0GAQFSAEgBaLv6nwCceG2ugZE6ufSoH6whS9g5/Htyjf+CZhM8bf86RLbszWw1IdUM0vZcaj9nQyXa/F2+OqDf/n5fmMDk3U6TKl2wpBE5Feh1imnSt6oPEFIASQFou/h1QDEQpLwbJPrblL73SDTdVbKRL028v+vVOle2K/rUO97Rp3+bmXxskRzbLUaebF074KpbRO3/2qhaoK8i1sSXmtb8lcHTxOFUC4aUeezhUQBKAWi7+O2vFqZJu8IIBhKLaJOv70uy/GJHaSCoFL8pJ+3vM9KC5fXa8j/0qAl+ZgXVldrs/TFOhRoFMoq4WYCe/MV3xnUjopbglPNALT0S9UR3/wvAXQazAFgASwFou/zHU3VE7X0YC9IwYZD2z49ZiSJUgxCKqOPSLAOFEmF/dVXOV/NRp61SZ+Qt6QR1AFuYvny8hHo0ir+P/5iVCprGipEkFrEvyMWL/EcL7qnJRKXTSX8wUwBMAWi8AYxB7XXG+X3FyabRKOuYV9ONNgdvw9zUlEdA6m9TJg0cY0nnoqxOixbQWEzmZpJZVpcpMZtdjOcT6wNsXFYnQiOnluQyr/dt+FKzfMuOD3kSMEWeOw==</base64Binary><base64Binary>E6g7PQEAEAELwF0GAAFSAE0BaLv47hqrl/YCjSCMIvafks2BWVVDAMHF1ETDRRV34CAZ1Ttabp+LzhfYN0sNPX8TU1d4cArRsHSddZ9oeUc7ms/D1qxgUfJvireCpbDuG86HP1QATgFou/jq868OJosgzAT+MtkxFvRoZwqLbNGDfWsd2ngc+NRYiRfZiw+qE57X1rHhbBwjZnRiD5ipY8Fu3+I6DZhnXdSjl34DQhyBC8Wg4xESxdqCV8BOAE8BaLwijhqsZo8SmHYODzEyVkIsFV06qOaOK13poMq2Kyazfh2hsVsm7XBaJngmfeUdNLhGpz6gOA76r1T/M6kKkJoKRpqk9yZIgCOwzsTuC8BdBvkAUQBQAWi8Io4fT43vi6X/VYP2wEQ60Ol/Rn4s6H5eFOZ/fESQdqG2hvNiDAoB4gv/PnFeRP789hO3+56bJqfgsSH8vFCllYWQBGA91qhZYC2YLjNmr0kAUQFovCJtCiuWFXbwztmeoeJgoVoQjmekK1TSGH8b9CyV4M4KEZd4qcApMJpaL6qZfhq3Ho7pb9V0WNua3a2DUJz7SQZCcKBO40DYUwBSAWi8JIhpksThkeikM1c3obElheYY0A2Eq27FPWR9kti9K3dDGJnbG0jwJhMaZvENwKDFAQe5K4YYVsxF7WQlns/1cUFFLRPSSSXNDS4I6H7CXPJnh8m4yQ==</base64Binary><base64Binary>E6g7PQEAEAELwF0G/gBQAFMBaLwijh0tGQziZC7btL3lbxcqqAvjp8R+pLlTlRKDhQPhrnk++I/+sQ1LwnPjIVIIzpl/QkpaqQ3FgeaTh1e99iH7aI8H7q+ySmvBrR3Mdv5XAFQBaLv47a8WiHDgnyenpb9NJnwvxbA4zMibKymd41oEZP4yEpBLdAT2DTNTR1MFPVKJj9p4zJaumz+hQCKiggNSwnB/GNaERVTFs5rxL5r4OdeksgNb4XeuSwBVAWi8IonACBe1jFf3geUI7ukR2inHhlwQAcDqc7q6iMS3RR1Nkfgpl8nEggAUszpPZZHgeAHkEi69TwW+y2OVD0SNPP1bTH2cbrT4rwvAXQapAFUAVgFovCKOGqqVSkmD3UhvYtZOtZ8wL+ejjo5Ki3ZiVTLDQTEmL9WD93zvj9m0RlfGg5zHi7TgeLwK6nkjmdFuEoB6d4C0dunn/gkISnLcKijQggEa0+TCTABXAWi8IhVAML7bR4pAoeiLl8baKCHhXhJnwXLXOoZda2uw15l60kQlBVXcDLofnMGWBLJ7SAis/Q9/KSofXb+CsZaNZAPBi/SIaxVLY6/hF+/8</base64Binary><base64Binary>E6g7PQEAEAELwF0G9wBLAFgBaLwemISNZPAcFxIIWo6KeS9j95Abo9lhHtl0bEF8C59UzpVck8U27az1b+W6HnkosyJ0qeBQIYfNTc77e9mKFHWqdjfDNx+K+0jvUABZAWi7+O2vF1Dx2nT3iuNt/TLEjSjhSSYlgbN8Ym19TXgcq5A410b9HvAbk0IY8vxQZHre6uxaQ1XLlPpq+xXA6tudzYRr4gZ/z7Ope3NDFbTFUABaAWi8IonkevJXiFaH0tp8BneDESSBF4/xsrOGgI0aF1sj0mhx3shN9C0XyucnnY54MxgBq0X3l1d1vbPb23IIjAsSOCTZ++3ty9FWRDGW/adxC8BdBlcAUwBbAWi7+Oxopl6JGI/dv/7eHiZ72OPvOv3UrkxTWeJekVhASa8jKsTBPxjgssSl0VaTS1zUosqs5eGnpzyVZcj5ST0cFiPtnFhH1RlLUmG1I6gb5Cn3OzBELA==</base64Binary><base64Binary>E6g7PQEAEAELwF0GAgFYAFwBaLwBgmFdGMnScIAivlnQrApBTXLvzJi+6srT57VxJW9J/yPdiVpG2bEHLH8lrqZ9B8YOnOT80RInQvAZ6tSJSkEbMnx4CeCNkSE6DSeE/aeQrC3W9OeMh00AXQFovCIT44C+kOaKaQZQ0OyiG1IlAb0gVxR/DKZeIRxw/upsMRX2Hef2ch/T382hm/blrvZ9Bk2t4DAojY9nrjA9H8wB7fQcT+As03tRt1EAXgFovBgRjEnPCz+3tEfm22CmrBOoMGwvcKigAKTwBXoiqT8bQRsiMWwkqyfqKog3dTfB6oYu8ma23or6s7Uuz4hSAzCn08bjbxklYH/9xnFV+McLwF0G6wBVAF8BaLv46vQFHExSeisU8wBqsj8j7DNtCDG+Pn3wd0URsRlcF2GgHKK/dp+t/alB9mg+ibFOZ8gDnWxJxvkzYww1vinZ8vdm0vvrQRT7HwG8Y1uACP+M+TIAYAFohZKAADZMm12CJZjdL8KIOm0KSkG78wCg7p8JKPlD3mbORV5q5WmDDGKcjZvxtMUB/yUAYQFoBjIABv42XYsu4VdJ5aBmEiyRDqD0YAkM/TaxHR0pmxd4a+X4LwBiAWgqqYYCWrkaICKNvehVWZQ50dRaaKloHdF3wt3mp3mev83OfPj4x1/5jpVYFfwoC8BdBjwAOABjAWgSXS9dHIyOR+lAwvxmwpo86bCIW8BR7XCaZZMjJ8kN9f+RXO3qnyJBmi44algdA8WPamRPPzApjmWInQ==</base64Binary><base64Binary>E6g7PQEAEAELwF0GfAA7AGQBaBIbNmMhDvDPkYd/QDhCm63SL5hWHSaIHIo4rlX5zkGsP0SmX2R5HxX4Tk5q9CYTPYAd+Jvm0E6HVR45AGUBaBJdCdAwF9V6yFAwIANUqx3v2xjogvmpccj54TRc8H9Q9Ecr6TdY6f+nCimp8I99QZELCzR2knxgC8BdBr8AOwBmAWgSXZhVdIap5inZaalKTR7J8HB8jO+D8fdozMMNY2WyXHzs+mFD9+G1rTiq4DOq4AeZ8KF4teFjrO9XOQBnAWgR9tb6u0tSURHyvYCTvAoe0fNXSgwUekvxa5uzLr79j1mE41iR6+epGrK1S2NkrXhBeFgEcvMiFz8AaAFoDvNY4C67josA8YtTMJ/8RW09Vbiig6kHpxQzqznpX3FPg2mVzsawUL3mkkIrJDN+DHpMF52w1TougbX5BZe6NHQ0</base64Binary><base64Binary>E6g7PQEAEAELwF0GQQA9AGkBaA8pLf/ISNukNkXeY/MmFLIY9IKg9pzPSVnUejfyIpJSlzvcjhltWPtbl0gWgAODyTeoa7YLxdgaGhL93gvAXQYPAAEAagFoAQBrAWgBAGwBaLcN8lM=</base64Binary><base64Binary>E6g7PQEAEAELwF0GFAABAG0BaAEAbgFoAQBvAWgBAHABaAvAXQYFAAEAcQFoQfM8pw==</base64Binary><base64Binary>E6g7PQEAEAELwF0GDwABAHIBaAEAcwFoAQB0AWgLwF0GFgABAHUBaAEAdgFoAQB3AWgBAHgBaP//C8BdAO4CW/H8Rw==</base64Binary></chunks></LegacySound>""";

            #endregion

            private static readonly Dictionary<string, ISound> _soundDict = new();
            
            public static void LoadBuiltinSounds()
            {
                var laugh = DeserializeXml<LegacySound>(LAUGH);
                _soundDict.Add("_laugh", laugh);
                
                var scream = DeserializeXml<LegacySound>(SCREAM);
                _soundDict.Add("_scream", scream);
            }

            public static void Unload() => _soundDict.Clear();

            [CanBeNull]
            public static ISound GetSound([CanBeNull] string name)
            {
                if (name == null)
                {
                    return null;
                }
                
                if (_soundDict.TryGetValue(name, out var sound))
                {
                    return sound;
                }

                var dir = GetSoundDirectory();
                var protoSoundFile = Path.Combine(dir, $"{name}.pb");
                var jsonSoundFile = Path.Combine(dir, $"{name}.json");
                if (File.Exists(protoSoundFile))
                {
                    using var stream = File.OpenRead(protoSoundFile);
                    sound = Serializer.Deserialize<ProtoSound>(stream);
                }
                else if (File.Exists(jsonSoundFile))
                {
                    var json = File.ReadAllText(jsonSoundFile);
                    sound = JsonConvert.DeserializeObject<LegacySound>(json);
                    if (sound != null && sound.IsValid)
                    {
                        sound.OnDeserialize();
                    }
                }
                
                // Sound may still be null - but we add it anyway to avoid IO every time
                _soundDict.Add(name, sound);
                return sound;
            }

            public static void SaveSound(string name, IEnumerable<byte[]> chunks)
            {
                var sound = ProtoSound.Create(chunks);
                
                var filePath = Path.Combine(GetSoundDirectory(), $"{name}.pb");
                using var stream = File.Open(filePath, FileMode.Create);
                Serializer.Serialize(stream, sound);
            }

            public static void SaveSoundLegacy(string name, IEnumerable<byte[]> chunks)
            {
                var sound = new LegacySound
                {
                    chunks = chunks.ToList()
                };
                sound.OnSerialize();
                
                var filePath = Path.Combine(GetSoundDirectory(), $"{name}.xml");
                File.WriteAllText(filePath, SerializeXml(sound));
            }
            
            private static T DeserializeXml<T>(string serialized)
            {
                var xmlSerializer = new XmlSerializer(typeof(T));
                using var textReader = new StringReader(serialized);
                return (T)xmlSerializer.Deserialize(textReader);
            }
            
            private static string SerializeXml<T>(T toSerialize)
            {
                var xmlSerializer = new XmlSerializer(toSerialize.GetType());
                using var textWriter = new StringWriter();
                xmlSerializer.Serialize(textWriter, toSerialize);
                return textWriter.ToString();
            }

            private static string GetSoundDirectory()
            {
                var dir = Path.Combine(DataFolder, "sounds");
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                return dir;
            }
        }
        
        [Serializable]
        public class LegacySound : ISound
        {
            [JsonIgnore]
            public bool IsValid => (chunks != null && chunks.Any()) || (sequence != null && sequence.Any());
            
            [JsonIgnore]
            public List<byte[]> chunks = new List<byte[]>();

            // Causes conflict with json serialization
            //[NonSerialized]
            [JsonProperty]
            private List<string> sequence;

            // [JsonIgnore]
            // public float Runtime => chunks.Count * 0.15f + 0.2f;

            public Queue<byte[]> GetChunks()
            {
                return new Queue<byte[]>(chunks);
            }

            public void OnSerialize()
            {
                sequence = chunks.Select(Convert.ToBase64String).ToList();         
            }

            public void OnDeserialize()
            {
                if (sequence == null)
                {
                    LogError("Failed to deserialize sequence");
                    return;
                }
                chunks = sequence.Select(Convert.FromBase64String).ToList();
            }
        }

        [ProtoContract]
        public class ProtoSound : ISound
        {
            [ProtoMember(1)]
            public byte[][] Chunks { get; set; }

            [ProtoIgnore]
            public bool IsValid => Chunks != null && Chunks.Length > 0;
            
            public Queue<byte[]> GetChunks()
            {
                return new Queue<byte[]>(Chunks);
            }
            
            public void OnSerialize() { }
            public void OnDeserialize() { }
            
            public static ProtoSound Create(IEnumerable<byte[]> chunks)
            {
                return new ProtoSound
                {
                    Chunks = chunks.ToArray()
                };
            }
        }

        private interface ISound
        {
            bool IsValid { get; }
            
            Queue<byte[]> GetChunks();

            void OnSerialize();
            void OnDeserialize();
        }

        #endregion

        #region ConfigManager

        private static class ConfigManager
        {
            #region Default config

            private const string defaultGraveyardJson = """{"objects":[{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":-0.8553467,"y":0.00003385544,"z":0.7173157},"rotation":{"x":0,"y":253.48851,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":-1.93078613,"y":-0.0458238125,"z":-1.09896851},"rotation":{"x":-2.26783627e-7,"y":345.866241,"z":1.72357643},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/coffin/coffinstorage.prefab","offset":{"x":-2.31542969,"y":0.0000309944153,"z":0.3977356},"rotation":{"x":0.0000134974653,"y":74.74707,"z":0.00000293165431},"lockRotation":false},{"prefabName":"assets/content/props/fog machine/fogmachine.prefab","offset":{"x":-1.63818359,"y":0.000018119812,"z":0.264099121},"rotation":{"x":0.000009659346,"y":350.9592,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":-1.3203125,"y":0.00004196167,"z":0.0230102539},"rotation":{"x":0.000009659347,"y":118.081993,"z":-4.0711095e-13},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":-1.53686523,"y":0.0000176429749,"z":0.7641907},"rotation":{"x":0.000009659346,"y":71.37329,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":-1.74523926,"y":-0.0000212192535,"z":1.44848633},"rotation":{"x":0.000009659346,"y":37.4867859,"z":-2.035555e-13},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":-2.76989746,"y":-0.0451231,"z":1.7975769},"rotation":{"x":-0.00000208107326,"y":165.265121,"z":357.357758},"lockRotation":false},{"prefabName":"assets/content/props/fog machine/fogmachine.prefab","offset":{"x":-2.78955078,"y":0.0000138282776,"z":-0.0179748535},"rotation":{"x":0.000009659346,"y":174.513641,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":-3.20617676,"y":0.00002861023,"z":0.23526001},"rotation":{"x":0.00000578243862,"y":253.217438,"z":-0.00000288016145},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":-3.30786133,"y":0.0000197887421,"z":0.9121399},"rotation":{"x":0.000009659348,"y":290.342346,"z":-8.142219e-13},"lockRotation":false},{"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab","offset":{"x":-4.003418,"y":0.013563633,"z":-0.8026428},"rotation":{"x":359.974731,"y":91.21362,"z":1.19329548},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.stone.deployed.prefab","offset":{"x":-4.04455566,"y":0.0054636,"z":-0.0390930176},"rotation":{"x":1.15657592,"y":75.695015,"z":359.7051},"lockRotation":false},{"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab","offset":{"x":-4.404175,"y":0.0129747391,"z":0.5748596},"rotation":{"x":1.09568751,"y":66.63109,"z":359.5266},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":-3.744751,"y":-0.101410389,"z":0.00305175781},"rotation":{"x":-0.00000170754731,"y":74.3531952,"z":0.426511317},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":-2.98291016,"y":0.009921074,"z":-0.6281738},"rotation":{"x":359.054474,"y":217.613937,"z":359.271545},"lockRotation":false},{"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab","offset":{"x":-2.49047852,"y":0.0035803318,"z":-1.68170166},"rotation":{"x":359.71347,"y":346.1115,"z":358.841339},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.stone.deployed.prefab","offset":{"x":-1.87524414,"y":0.0000109672546,"z":-1.40307617},"rotation":{"x":0.000009659346,"y":348.3937,"z":-1.01777751e-13},"lockRotation":false},{"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab","offset":{"x":-1.18969727,"y":0.0000612735748,"z":-1.3638916},"rotation":{"x":0.000009659347,"y":359.86673,"z":-7.951387e-16},"lockRotation":false},{"prefabName":"assets/bundled/prefabs/autospawn/collectable/stone/halloween/halloween-sulfur-collectible.prefab","offset":{"x":-3.89379883,"y":-0.00000596046448,"z":2.40826416},"rotation":{"x":0.00000503659066,"y":213.664276,"z":-0.000001398522},"lockRotation":false},{"prefabName":"assets/bundled/prefabs/autospawn/collectable/stone/halloween/halloween-bone-collectable.prefab","offset":{"x":0.245117188,"y":-0.0000102519989,"z":3.31918335},"rotation":{"x":0.000009659347,"y":143.6002,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/scarecrow/scarecrow.deployed.prefab","offset":{"x":1.92895508,"y":0.0000193119049,"z":1.85293579},"rotation":{"x":0.0000135316968,"y":253.39859,"z":0.00000288628667},"lockRotation":false},{"prefabName":"assets/bundled/prefabs/autospawn/collectable/stone/halloween/halloween-sulfur-collectible.prefab","offset":{"x":2.01086426,"y":0.0000238418579,"z":-1.81207275},"rotation":{"x":0.000009659348,"y":49.27787,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.wood.deployed.prefab","offset":{"x":3.49707031,"y":0.0000195503235,"z":-4.09390259},"rotation":{"x":0.000009659347,"y":155.07312,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":3.60107422,"y":0.00004005432,"z":-3.70605469},"rotation":{"x":0.000009659348,"y":157.772079,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/scarecrow/scarecrow.deployed.prefab","offset":{"x":1.56689453,"y":0.0106554031,"z":-4.83428955},"rotation":{"x":0.3889132,"y":160.986588,"z":1.1284349},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":1.11401367,"y":0.0200433731,"z":-5.34158325},"rotation":{"x":0.3796192,"y":161.457626,"z":1.13159478},"lockRotation":false},{"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab","offset":{"x":-0.376708984,"y":0.0305254459,"z":-5.677124},"rotation":{"x":0.000009659347,"y":169.202057,"z":0},"lockRotation":false},{"prefabName":"assets/bundled/prefabs/autospawn/collectable/stone/halloween/halloween-metal-collectable.prefab","offset":{"x":-4.102051,"y":0.0305883884,"z":-6.44747925},"rotation":{"x":0.0000144774713,"y":7.92584133,"z":3.33783e-7},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.wood.deployed.prefab","offset":{"x":-6.54797363,"y":0.0382885933,"z":-7.848175},"rotation":{"x":359.139374,"y":223.861343,"z":359.173},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":-6.201782,"y":0.0343384743,"z":-7.66012573},"rotation":{"x":359.0704,"y":218.848541,"z":359.251343},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.wood.deployed.prefab","offset":{"x":-8.175415,"y":0.0305464268,"z":-6.28564453},"rotation":{"x":0.000009659348,"y":238.86351,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":-7.87548828,"y":0.030559063,"z":-6.204193},"rotation":{"x":0.000005039364,"y":213.890854,"z":-0.00000140766},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.wood.deployed.prefab","offset":{"x":-9.025879,"y":0.0305149555,"z":-4.60971069},"rotation":{"x":0.00000564686161,"y":247.63829,"z":-0.000002688067},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/cursed_cauldron/cursedcauldron.deployed.prefab","offset":{"x":-9.71106,"y":0.0305495262,"z":-2.1401062},"rotation":{"x":0.00000585138832,"y":255.918121,"z":-0.00000297072347},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.wood.deployed.prefab","offset":{"x":-9.650513,"y":0.0305356979,"z":-0.34161377},"rotation":{"x":0.000009659347,"y":285.843384,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":-8.85144,"y":0.0298864841,"z":1.34243774},"rotation":{"x":0.884414,"y":317.810638,"z":359.198456},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.wood.deployed.prefab","offset":{"x":-9.012695,"y":0.0231897831,"z":1.664093},"rotation":{"x":0.495797247,"y":294.540344,"z":358.914276},"lockRotation":false},{"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab","offset":{"x":-8.130859,"y":0.00003528595,"z":3.07324219},"rotation":{"x":0.000009659347,"y":316.4879,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":-7.312622,"y":0.000011920929,"z":3.32696533},"rotation":{"x":0.000009659346,"y":343.757,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/coffin/coffinstorage.prefab","offset":{"x":-6.791992,"y":0.00002861023,"z":4.225708},"rotation":{"x":0.000005015632,"y":328.09787,"z":0.00000132727212},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":-6.73181152,"y":0.0000343322754,"z":3.46838379},"rotation":{"x":0.000009659347,"y":343.083862,"z":-1.01777751e-13},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":-6.340088,"y":0.0000257492065,"z":3.7354126},"rotation":{"x":0.000005134751,"y":319.052551,"z":0.000001689313},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":-5.87280273,"y":0.0000171661377,"z":4.141571},"rotation":{"x":0.0000142967519,"y":327.5576,"z":-0.00000134915183},"lockRotation":false},{"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab","offset":{"x":-5.29748535,"y":4.76837158e-7,"z":5.39541626},"rotation":{"x":0.000009659347,"y":272.748047,"z":-4.07111e-13},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.wood.deployed.prefab","offset":{"x":0.4880371,"y":0.0000276565552,"z":6.9331665},"rotation":{"x":0.000009659347,"y":354.0187,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":0.364501953,"y":0.0000138282776,"z":6.557312},"rotation":{"x":0.000009659348,"y":0.9036973,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab","offset":{"x":1.927002,"y":-0.00000548362732,"z":7.2578125},"rotation":{"x":0.0000144783935,"y":352.396942,"z":-3.20209949e-7},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":3.276245,"y":0.0000228881836,"z":6.8921814},"rotation":{"x":0.00000535524,"y":53.95629,"z":-0.00000219098388},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.wood.deployed.prefab","offset":{"x":3.4831543,"y":0.0000462532043,"z":7.202057},"rotation":{"x":0.0000144862634,"y":3.87219954,"z":1.63169986e-7},"lockRotation":false},{"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab","offset":{"x":4.561157,"y":0.00003695488,"z":7.17922974},"rotation":{"x":0.000004925867,"y":22.9088554,"z":-9.591173e-7},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.wood.deployed.prefab","offset":{"x":5.96459961,"y":0.0000357627869,"z":6.42126465},"rotation":{"x":0.000009659347,"y":36.94886,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":5.647461,"y":0.00003671646,"z":6.23403931},"rotation":{"x":0.00000598202359,"y":80.82408,"z":-0.00000313097962},"lockRotation":false},{"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab","offset":{"x":6.880249,"y":0.00000381469727,"z":5.48220825},"rotation":{"x":0.00001388053,"y":58.14395,"z":0.00000234677623},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":7.55810547,"y":0.0000176429749,"z":4.130249},"rotation":{"x":0.000009659347,"y":110.928383,"z":-4.07111e-13},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":6.984741,"y":0.0000298023224,"z":4.13027954},"rotation":{"x":0.00001385274,"y":120.513214,"z":-0.00000239608062},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":6.49389648,"y":-0.00000524520874,"z":3.97286987},"rotation":{"x":0.000009659348,"y":140.901154,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":6.05322266,"y":0.0000324249268,"z":3.82070923},"rotation":{"x":0.0000142799872,"y":146.162628,"z":-0.00000140550412},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":5.61193848,"y":-0.00000524520874,"z":3.60189819},"rotation":{"x":0.000009659348,"y":144.003357,"z":-2.035555e-13},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":5.18286133,"y":0.0000259876251,"z":3.330902},"rotation":{"x":0.000009659347,"y":121.593117,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":5.208374,"y":0.0000243186951,"z":2.91519165},"rotation":{"x":0.000009659347,"y":104.583847,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":5.319214,"y":0.00003504753,"z":2.56253052},"rotation":{"x":0.000009659348,"y":70.69874,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":5.61560059,"y":0.00003528595,"z":2.38934326},"rotation":{"x":0.000009659347,"y":37.08588,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":5.987671,"y":0.00006341934,"z":2.44271851},"rotation":{"x":0.0000144888718,"y":0.899703562,"z":3.791928e-8},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":6.373169,"y":0.000011920929,"z":2.5411377},"rotation":{"x":0.000009659347,"y":357.262939,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":6.774414,"y":0.00003528595,"z":2.61859131},"rotation":{"x":0.000009659348,"y":0.905399859,"z":6.36110859e-15},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":7.164795,"y":0.00006151199,"z":2.7288208},"rotation":{"x":0.000004856384,"y":347.942627,"z":5.072427e-7},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":7.58764648,"y":0.0000205039978,"z":2.819641},"rotation":{"x":0.00000485757573,"y":347.676361,"z":5.184022e-7},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":7.89831543,"y":0.0000123977661,"z":3.20263672},"rotation":{"x":0.000009659347,"y":98.2373,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.wood.deployed.prefab","offset":{"x":7.977295,"y":0.0000262260437,"z":3.77929688},"rotation":{"x":0.0000135554019,"y":72.45221,"z":0.00000285420765},"lockRotation":false},{"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab","offset":{"x":8.397583,"y":0.0000174045563,"z":0.692474365},"rotation":{"x":0.000013469813,"y":104.178719,"z":-0.000002967504},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.wood.deployed.prefab","offset":{"x":7.81713867,"y":0.0000243186951,"z":-0.366394043},"rotation":{"x":0.000009659348,"y":123.619446,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":7.83618164,"y":0.0000329017639,"z":0.107452393},"rotation":{"x":0.000009659346,"y":112.5486,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab","offset":{"x":7.230957,"y":0.00006151199,"z":-1.695282},"rotation":{"x":0.000009659346,"y":136.982117,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":6.140381,"y":0.0000305175781,"z":-2.18276978},"rotation":{"x":0.0000142347826,"y":142.653244,"z":-0.00000154632994},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.wood.deployed.prefab","offset":{"x":6.119995,"y":0.00000715255737,"z":-2.5241394},"rotation":{"x":0.0000051158,"y":140.358,"z":0.00000163765912},"lockRotation":false},{"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab","offset":{"x":4.89343262,"y":0.0000329017639,"z":-3.50061035},"rotation":{"x":0.000009659347,"y":141.03392,"z":-2.035555e-13},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/coffin/coffinstorage.prefab","offset":{"x":6.60339355,"y":0.0000171661377,"z":3.27584839},"rotation":{"x":-0.00000280505355,"y":341.013062,"z":-0.00000572775434},"lockRotation":false},{"prefabName":"assets/bundled/prefabs/autospawn/collectable/stone/halloween/halloween-bone-collectable.prefab","offset":{"x":-6.4161377,"y":0.0305466652,"z":-2.40640259},"rotation":{"x":0.000009659347,"y":273.603027,"z":4.07111e-13},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.wood.deployed.prefab","offset":{"x":-4.21459961,"y":0.0305576324,"z":-8.918182},"rotation":{"x":0.000004881133,"y":196.742813,"z":-7.031485e-7},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":-3.89953613,"y":0.0305500031,"z":-8.745026},"rotation":{"x":0.0000049658247,"y":207.273727,"z":-0.00000113868},"lockRotation":false},{"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab","offset":{"x":-2.74401855,"y":0.030520916,"z":-9.084991},"rotation":{"x":0.000009659346,"y":181.758743,"z":-1.27222189e-14},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.wood.deployed.prefab","offset":{"x":-1.50805664,"y":0.0305073261,"z":-8.972748},"rotation":{"x":0.000009659347,"y":179.865738,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":-1.07348633,"y":0.0305213928,"z":-9.120209},"rotation":{"x":0.00000487239959,"y":164.746262,"z":6.41000042e-7},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/scarecrow/scarecrow.deployed.prefab","offset":{"x":0.9310303,"y":0.0305259228,"z":-7.01425171},"rotation":{"x":0.000009659347,"y":54.00304,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/scarecrow/scarecrow.deployed.prefab","offset":{"x":4.62109375,"y":0.0305294991,"z":-6.99713135},"rotation":{"x":0.000005708021,"y":70.20288,"z":-0.00000277718686},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":4.2734375,"y":0.0305650234,"z":-7.12896729},"rotation":{"x":0.000009659347,"y":72.45397,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":0.6031494,"y":0.03057909,"z":-7.20101929},"rotation":{"x":0.000009659348,"y":49.63679,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/scarecrow/scarecrow.deployed.prefab","offset":{"x":7.974121,"y":0.0000348091125,"z":-3.52142334},"rotation":{"x":0.000009659347,"y":316.1278,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":8.279175,"y":0.0000448226929,"z":-3.75708},"rotation":{"x":0.0000140564653,"y":311.131836,"z":-0.00000199777514},"lockRotation":false},{"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab","offset":{"x":9.257568,"y":0.0305199623,"z":-6.436249},"rotation":{"x":0.0000137802126,"y":242.868622,"z":0.00000251877464},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":0.2869873,"y":-0.0000152587891,"z":8.215576},"rotation":{"x":0.0000142549352,"y":215.823227,"z":0.00000148536321},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":-0.717773438,"y":0.0000283718109,"z":8.790527},"rotation":{"x":0.000009659347,"y":235.938766,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab","offset":{"x":-1.892334,"y":0.0000123977661,"z":10.9476318},"rotation":{"x":0.000009659347,"y":171.1817,"z":5.08888755e-14},"lockRotation":false},{"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab","offset":{"x":-4.88806152,"y":0.000036239624,"z":10.1401978},"rotation":{"x":0.00000965934851,"y":169.292831,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/scarecrow/scarecrow.deployed.prefab","offset":{"x":-5.46105957,"y":0.0000247955322,"z":7.216217},"rotation":{"x":0.0000138165351,"y":118.803932,"z":-0.00000245836077},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":-5.63134766,"y":0.000027179718,"z":7.74789429},"rotation":{"x":0.000009659348,"y":117.858971,"z":-4.0711095e-13},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab","offset":{"x":-5.92797852,"y":0.000039100647,"z":7.303406},"rotation":{"x":0.0000143921279,"y":157.007584,"z":-9.625688e-7},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":-1.40625,"y":-0.162520885,"z":8.794281},"rotation":{"x":-0.000001947671,"y":256.5592,"z":0.4252314},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.stone.deployed.prefab","offset":{"x":-0.387817383,"y":-0.0000143051147,"z":8.206146},"rotation":{"x":0.0000051388306,"y":221.223267,"z":-0.0000017001986},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":0.3894043,"y":-0.150010824,"z":7.61203},"rotation":{"x":0.00000261468176,"y":169.649551,"z":359.090729},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":-4.351074,"y":0.000008583069,"z":7.97094727},"rotation":{"x":0,"y":255.19931,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":-5.40551758,"y":-0.03238702,"z":5.98376465},"rotation":{"x":-4.935879e-7,"y":339.519165,"z":1.22325051},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":3.38317871,"y":-0.119962215,"z":7.843567},"rotation":{"x":-0.00000246606351,"y":1.63430548,"z":0.231166527},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":6.11914063,"y":-0.08985424,"z":6.989502},"rotation":{"x":1.60082564e-7,"y":33.40346,"z":0.926589251},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":8.091675,"y":-0.0597145557,"z":4.828766},"rotation":{"x":-0.00000255465079,"y":62.1244354,"z":0.2169731},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":8.907593,"y":-0.0826499462,"z":1.97076416},"rotation":{"x":-0.00000186762986,"y":85.81539,"z":358.9176},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":8.3671875,"y":-0.0564644337,"z":-0.7786255},"rotation":{"x":-3.20165128e-7,"y":116.755585,"z":2.11445427},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":6.50109863,"y":-0.00000524520874,"z":-3.11712646},"rotation":{"x":0,"y":140.898315,"z":0},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":3.91711426,"y":-0.0619373322,"z":-4.7211},"rotation":{"x":0.00000277476443,"y":155.139114,"z":357.6848},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":1.182373,"y":-0.130412817,"z":-5.793579},"rotation":{"x":-0.000004552348,"y":161.857651,"z":359.649078},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":7.30078125,"y":-0.0278611183,"z":-7.332245},"rotation":{"x":-3.8019607e-7,"y":156.096756,"z":359.608582},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":4.489624,"y":-0.0522985458,"z":-8.375458},"rotation":{"x":0.00000170754731,"y":163.121811,"z":359.4593},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":1.57775879,"y":-0.08722329,"z":-9.112946},"rotation":{"x":-0.000003882002,"y":168.3862,"z":359.2098},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":-1.3449707,"y":-0.1053493,"z":-9.664429},"rotation":{"x":0.00000149243635,"y":170.230438,"z":0.0919348449},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":-4.2890625,"y":-0.0949764252,"z":-9.631653},"rotation":{"x":-0.00000274474883,"y":191.1251,"z":0.305086553},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":-6.952881,"y":-0.05514598,"z":-8.477814},"rotation":{"x":-2.13443414e-7,"y":216.092438,"z":1.22994649},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":-8.899414,"y":-0.0307371616,"z":-6.240326},"rotation":{"x":-0.00000148076367,"y":62.0881157,"z":0.310998529},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":-10.2220459,"y":-0.0301301479,"z":-3.569519},"rotation":{"x":-0.00000130734088,"y":65.22035,"z":359.66568},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":-10.5438232,"y":-0.0444667339,"z":-0.70614624},"rotation":{"x":-0.00000178758864,"y":101.76313,"z":0.8757367},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":-9.62207,"y":-0.08525491,"z":2.12442017},"rotation":{"x":-0.00000146742343,"y":114.127533,"z":0.685146034},"lockRotation":false},{"prefabName":"assets/prefabs/misc/halloween/graveyard_fence/graveyardfence.prefab","offset":{"x":-7.913208,"y":-0.08369398,"z":4.45916748},"rotation":{"x":-0.00000229451666,"y":138.291153,"z":359.2544},"lockRotation":false}],"zombieSpawns":[{"x":3.56323242,"y":0.0000157356262,"z":0.8786621},{"x":-5.48376465,"y":0.03541088,"z":-8.360443},{"x":2.74731445,"y":0.000008583069,"z":4.94171143},{"x":-6.681885,"y":0.0298466682,"z":1.0118103},{"x":9.051147,"y":0.0000112056732,"z":-1.24377441},{"x":8.039673,"y":0.030531168,"z":-7.77059937},{"x":2.90625,"y":0.0000011920929,"z":9.123322},{"x":-11.2504883,"y":0.03055787,"z":2.32556152}]}""";

            private const string defaultPileA = """{"water":false,"type":"a","decorations":[{"position":{"x":-0.3137207,"y":0.07618427,"z":1.76635742},"rotation":{"x":8.633948,"y":348.5868,"z":352.117523},"prefabName":"assets/prefabs/misc/halloween/scarecrow/scarecrow.deployed.prefab"},{"position":{"x":2.16723633,"y":0.4474535,"z":0.6627197},"rotation":{"x":359.92627,"y":64.3647842,"z":0.09918409},"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab"},{"position":{"x":2.31634521,"y":-0.08292389,"z":-0.599731445},"rotation":{"x":33.5820045,"y":123.808105,"z":1.91495693},"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab"},{"position":{"x":0.215148926,"y":0.047917366,"z":-1.21911621},"rotation":{"x":11.7777462,"y":121.560829,"z":5.13143539},"prefabName":"assets/prefabs/misc/halloween/skull spikes/skins/skullspikes.pumpkin.deployed.prefab"},{"position":{"x":-2.09436035,"y":0.969965,"z":-1.01977539},"rotation":{"x":357.004333,"y":310.936,"z":358.672729},"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab"},{"position":{"x":-1.77233887,"y":0.9240303,"z":0.243896484},"rotation":{"x":1.05366111,"y":310.389984,"z":357.341461},"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab"},{"position":{"x":-3.22277832,"y":-0.0966711044,"z":0.6883545},"rotation":{"x":18.8892479,"y":274.9687,"z":0.801842153},"prefabName":"assets/prefabs/misc/halloween/skull spikes/skullspikes.deployed.prefab"},{"position":{"x":1.19012451,"y":0.921124458,"z":0.4260254},"rotation":{"x":355.769073,"y":13.640192,"z":7.882624},"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab"}]}""";
            private const string defaultPileB = """{"water":false,"type":"b","decorations":[{"position":{"x":1.70526123,"y":0.657169342,"z":-0.3482666},"rotation":{"x":3.66692066,"y":45.239872,"z":359.421936},"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab"},{"position":{"x":1.03851318,"y":0.444253922,"z":0.321411133},"rotation":{"x":356.732971,"y":69.5262756,"z":351.716339},"prefabName":"assets/prefabs/misc/halloween/skull spikes/skins/skullspikes.pumpkin.deployed.prefab"},{"position":{"x":1.24316406,"y":1.47918034,"z":-1.24523926},"rotation":{"x":3.63404274,"y":118.195511,"z":1.34748018},"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab"},{"position":{"x":-0.162902832,"y":0.6707716,"z":-1.45739746},"rotation":{"x":2.73558259,"y":226.719452,"z":359.314972},"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.stone.deployed.prefab"},{"position":{"x":-0.3890381,"y":0.6543369,"z":-1.73657227},"rotation":{"x":2.64143,"y":220.27066,"z":359.012177},"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab"},{"position":{"x":-1.71502686,"y":1.80590725,"z":1.52404785},"rotation":{"x":2.06557536,"y":302.36853,"z":0.0515213721},"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab"},{"position":{"x":-0.043762207,"y":2.71414185,"z":0.5855713},"rotation":{"x":2.47662663,"y":245.977,"z":357.702942},"prefabName":"assets/prefabs/misc/halloween/scarecrow/scarecrow.deployed.prefab"},{"position":{"x":-0.207275391,"y":0.3951664,"z":1.30871582},"rotation":{"x":17.0786419,"y":37.8700829,"z":359.844635},"prefabName":"assets/prefabs/misc/halloween/skull spikes/skins/skullspikes.candles.deployed.prefab"}]}""";
            private const string defaultPileC = """{"water":false,"type":"c","decorations":[{"position":{"x":-2.62475586,"y":0.00137043,"z":0.842041},"rotation":{"x":12.7946091,"y":245.9385,"z":359.6421},"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.wood.deployed.prefab"},{"position":{"x":-2.32946777,"y":0.04228115,"z":0.7397461},"rotation":{"x":8.791769,"y":238.815186,"z":343.980164},"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab"},{"position":{"x":-2.18518066,"y":0.0165586472,"z":1.53979492},"rotation":{"x":12.0330009,"y":352.262817,"z":359.653473},"prefabName":"assets/prefabs/misc/halloween/skull spikes/skins/skullspikes.candles.deployed.prefab"},{"position":{"x":2.302185,"y":0.262998581,"z":-0.0574951172},"rotation":{"x":2.327561,"y":56.05398,"z":351.194977},"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.wood.deployed.prefab"},{"position":{"x":2.17059326,"y":0.226635933,"z":-0.5501709},"rotation":{"x":8.138159,"y":104.522346,"z":355.9028},"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab"},{"position":{"x":1.9864502,"y":0.170712471,"z":-1.07226563},"rotation":{"x":11.0216227,"y":147.694275,"z":0.6736235},"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.happy.prefab"}]}""";
            private const string defaultPileD = """{"water":false,"type":"d","decorations":[{"position":{"x":0.0285644531,"y":0.358247757,"z":0.09123427},"rotation":{"x":8.805238,"y":61.410183,"z":358.456},"prefabName":"assets/prefabs/misc/halloween/scarecrow/scarecrow.deployed.prefab"},{"position":{"x":-1.30078506,"y":1.1594429,"z":0.31005007},"rotation":{"x":6.90095234,"y":320.784851,"z":2.80424118},"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab"},{"position":{"x":-0.9705839,"y":0.3494873,"z":-0.5886281},"rotation":{"x":2.29222226,"y":227.973434,"z":357.077484},"prefabName":"assets/prefabs/misc/halloween/skull spikes/skins/skullspikes.candles.deployed.prefab"},{"position":{"x":-0.423654556,"y":1.3590641,"z":-1.03887677},"rotation":{"x":16.56447,"y":186.047516,"z":358.079865},"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab"}]}""";
            private const string defaultPileE = """{"water":false,"type":"e","decorations":[{"position":{"x":-0.880441666,"y":0.3054161,"z":-0.510950565},"rotation":{"x":8.062617,"y":181.602509,"z":359.341736},"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab"},{"position":{"x":0.14836216,"y":0.219125748,"z":-0.711302757},"rotation":{"x":10.9138241,"y":186.765915,"z":3.49875069},"prefabName":"assets/prefabs/misc/halloween/skull spikes/skins/skullspikes.pumpkin.deployed.prefab"},{"position":{"x":1.12559128,"y":0.04369545,"z":-1.0759573},"rotation":{"x":9.839871,"y":147.496063,"z":2.802296},"prefabName":"assets/prefabs/misc/halloween/skull spikes/skins/skullspikes.candles.deployed.prefab"},{"position":{"x":-0.207486629,"y":0.3676567,"z":0.5052052},"rotation":{"x":8.809003,"y":61.5525436,"z":358.477783},"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.wood.deployed.prefab"},{"position":{"x":-0.220489025,"y":0.363843918,"z":0.2639863},"rotation":{"x":357.911438,"y":2.60934162,"z":4.608762},"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab"},{"position":{"x":-1.87372541,"y":0.178339,"z":0.112344384},"rotation":{"x":7.311178,"y":342.339264,"z":9.504964},"prefabName":"assets/prefabs/misc/halloween/scarecrow/scarecrow.deployed.prefab"}]}""";
            private const string defaultPileF = """{"water":false,"type":"f","decorations":[{"position":{"x":0.534318,"y":0.3499527,"z":-0.136197329},"rotation":{"x":2.8943615,"y":77.101944,"z":350.1714},"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.happy.prefab"},{"position":{"x":-0.02256012,"y":0.381908417,"z":-0.224438429},"rotation":{"x":10.4285574,"y":167.622925,"z":358.481171},"prefabName":"assets/prefabs/misc/halloween/skull spikes/skins/skullspikes.candles.deployed.prefab"},{"position":{"x":-0.4614811,"y":0.332000732,"z":-0.524418354},"rotation":{"x":10.0835495,"y":193.014069,"z":3.07504082},"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.stone.deployed.prefab"},{"position":{"x":-0.8663378,"y":1.149147,"z":0.345117569},"rotation":{"x":0.220404059,"y":357.818634,"z":5.76643467},"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.happy.prefab"},{"position":{"x":-0.85182,"y":0.311922073,"z":-0.7939303},"rotation":{"x":7.179059,"y":218.972946,"z":2.759523},"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab"}]}""";
            private const string defaultPileG = """{"water":false,"type":"g","decorations":[{"position":{"x":2.41292381,"y":0.0312099457,"z":-0.4514251},"rotation":{"x":12.524889,"y":44.73879,"z":358.4596},"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.stone.deployed.prefab"},{"position":{"x":2.74277687,"y":-0.0467987061,"z":-0.339167148},"rotation":{"x":16.221487,"y":52.6186562,"z":1.1618154},"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab"},{"position":{"x":2.4266758,"y":-0.0391616821,"z":0.0383346975},"rotation":{"x":21.1232319,"y":94.32902,"z":356.2568},"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab"},{"position":{"x":-0.6190529,"y":0.165113449,"z":3.02720618},"rotation":{"x":10.6358738,"y":32.8656425,"z":353.318939},"prefabName":"assets/prefabs/misc/halloween/skull spikes/skins/skullspikes.candles.deployed.prefab"},{"position":{"x":-3.32156849,"y":0.562101364,"z":2.68483472},"rotation":{"x":10.2825747,"y":305.8667,"z":358.545441},"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab"},{"position":{"x":-1.60050583,"y":1.761982,"z":1.691622},"rotation":{"x":1.79971945,"y":295.518219,"z":0.4375894},"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab"},{"position":{"x":-1.32880592,"y":1.7817955,"z":0.8196764},"rotation":{"x":0.908187449,"y":309.915222,"z":3.01421046},"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.happy.prefab"},{"position":{"x":-0.519062042,"y":0.0963726044,"z":-1.56937647},"rotation":{"x":2.43805337,"y":230.662064,"z":2.25142884},"prefabName":"assets/prefabs/misc/halloween/skull spikes/skins/skullspikes.pumpkin.deployed.prefab"},{"position":{"x":2.459959,"y":-0.00315094,"z":-2.193352},"rotation":{"x":12.3491373,"y":149.7389,"z":3.96454453},"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.happy.prefab"}]}""";
            private const string defaultPileH = """{"water":false,"type":"h","decorations":[{"position":{"x":2.17673683,"y":0.06470299,"z":0.9743713},"rotation":{"x":16.0321026,"y":72.01199,"z":0.291995734},"prefabName":"assets/prefabs/misc/halloween/skull spikes/skins/skullspikes.pumpkin.deployed.prefab"},{"position":{"x":-0.65765667,"y":0.298780441,"z":-0.4392893},"rotation":{"x":2.80704618,"y":261.441,"z":353.882446},"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab"},{"position":{"x":-0.6726122,"y":0.494907379,"z":0.6405169},"rotation":{"x":359.1595,"y":129.4761,"z":3.0738256},"prefabName":"assets/prefabs/misc/halloween/candles/largecandleset.prefab"},{"position":{"x":0.6185255,"y":0.4272461,"z":1.4041698},"rotation":{"x":3.122857,"y":17.2293186,"z":359.632782},"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab"},{"position":{"x":-0.151872635,"y":0.435071945,"z":1.59286582},"rotation":{"x":2.70594287,"y":55.4333534,"z":1.63678491},"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab"},{"position":{"x":-0.0608139038,"y":0.5733471,"z":1.11835241},"rotation":{"x":3.00600338,"y":42.3453,"z":0.981819749},"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.stone.deployed.prefab"},{"position":{"x":-1.06064892,"y":0.497777939,"z":1.97149622},"rotation":{"x":0.921317637,"y":312.207,"z":355.969421},"prefabName":"assets/prefabs/misc/halloween/scarecrow/scarecrow.deployed.prefab"},{"position":{"x":-1.060811,"y":0.812721252,"z":3.267889},"rotation":{"x":1.84406829,"y":11.4453039,"z":0.9495676},"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab"},{"position":{"x":-2.532467,"y":0.06676674,"z":1.65496087},"rotation":{"x":15.9878931,"y":271.838257,"z":2.97801018},"prefabName":"assets/prefabs/misc/halloween/skull spikes/skins/skullspikes.candles.deployed.prefab"}]}""";
            private const string defaultPileI = """{"water":false,"type":"i","decorations":[{"position":{"x":0.843569756,"y":0.232639313,"z":-0.0219564736},"rotation":{"x":17.204752,"y":97.18578,"z":359.584167},"prefabName":"assets/prefabs/misc/halloween/skull spikes/skins/skullspikes.candles.deployed.prefab"},{"position":{"x":-0.7554674,"y":0.308132172,"z":-1.08500361},"rotation":{"x":358.752533,"y":197.523438,"z":4.229652},"prefabName":"assets/prefabs/misc/halloween/deployablegravestone/gravestone.stone.deployed.prefab"},{"position":{"x":-1.016882,"y":0.284563065,"z":-1.33527243},"rotation":{"x":17.6642418,"y":213.236008,"z":0.29085654},"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab"},{"position":{"x":-1.41609192,"y":0.6662159,"z":1.39465773},"rotation":{"x":350.23172,"y":199.4962,"z":358.2971},"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab"},{"position":{"x":-0.1942091,"y":0.368310928,"z":1.32470012},"rotation":{"x":354.6352,"y":32.1155319,"z":3.89119935},"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab"}]}""";
            private const string defaultPileJ = """{"water":false,"type":"j","decorations":[{"position":{"x":-2.27161217,"y":0.213348389,"z":2.50333166},"rotation":{"x":12.5190935,"y":291.13385,"z":11.6333733},"prefabName":"assets/prefabs/misc/halloween/scarecrow/scarecrow.deployed.prefab"},{"position":{"x":-1.9931612,"y":0.110141754,"z":1.04218352},"rotation":{"x":16.143652,"y":226.251129,"z":358.4025},"prefabName":"assets/prefabs/misc/halloween/skull spikes/skins/skullspikes.pumpkin.deployed.prefab"},{"position":{"x":-0.1964016,"y":1.11312866,"z":2.300363},"rotation":{"x":0.4604976,"y":47.63449,"z":358.015442},"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab"},{"position":{"x":-0.499943733,"y":0.590990067,"z":2.80501318},"rotation":{"x":5.03090143,"y":84.74667,"z":0.08528825},"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab"},{"position":{"x":2.492528,"y":0.383829117,"z":-0.469989657},"rotation":{"x":1.12197554,"y":90.6007,"z":0.7751849},"prefabName":"assets/prefabs/deployable/jack o lantern/jackolantern.happy.prefab"},{"position":{"x":2.53445721,"y":0.375169754,"z":0.117781758},"rotation":{"x":1.363693,"y":56.08642,"z":0.00310338545},"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab"},{"position":{"x":2.07875776,"y":0.5685806,"z":0.0953382254},"rotation":{"x":1.16840744,"y":24.9100132,"z":359.296753},"prefabName":"assets/prefabs/misc/halloween/candles/smallcandleset.prefab"},{"position":{"x":-0.339736938,"y":0.2002449,"z":-1.47314847},"rotation":{"x":12.9163933,"y":169.723,"z":3.7546823},"prefabName":"assets/prefabs/misc/halloween/skull spikes/skins/skullspikes.candles.deployed.prefab"}]}""";

            #endregion

            public static void SaveGraveyardConfig(GraveyardConfig config, string name)
            {
                Interface.Oxide.DataFileSystem.WriteObject("Halloween/graveyards/" + name, config);
            }

            public static GraveyardConfig LoadGraveyardConfig(string name)
            {
                if (name == "_default")
                {
                    return JsonConvert.DeserializeObject<GraveyardConfig>(defaultGraveyardJson);
                }

                var cfg = Interface.Oxide.DataFileSystem.ReadObject<GraveyardConfig>("Halloween/graveyards/" + name);
                return cfg;
            }

            public static void SaveJunkPileConfig(JunkPileConfig config, string name)
            {
                Interface.Oxide.DataFileSystem.WriteObject("Halloween/junkpiles/" + name, config);
            }

            public static JunkPileConfig LoadJunkPileConfig(string name)
            {
                var cfg = Interface.Oxide.DataFileSystem.ReadObject<JunkPileConfig>("Halloween/junkpiles/" + name);
                return cfg;
            }

            public static JunkPileConfig LoadDefaultJunkPileConfig(char type)
            {
                string json = string.Empty;
                switch (type)
                {
                    case 'a':
                        json = defaultPileA;
                        break;
                    case 'b':
                        json = defaultPileB;
                        break;
                    case 'c':
                        json = defaultPileC;
                        break;
                    case 'd':
                        json = defaultPileD;
                        break;
                    case 'e':
                        json = defaultPileE;
                        break;
                    case 'f':
                        json = defaultPileF;
                        break;
                    case 'g':
                        json = defaultPileG;
                        break;
                    case 'h':
                        json = defaultPileH;
                        break;
                    case 'i':
                        json = defaultPileI;
                        break;
                    case 'j':
                        json = defaultPileJ;
                        break;
                    default:
                        break;
                }
                return JsonConvert.DeserializeObject<JunkPileConfig>(json);
            }
        }

        #endregion

        #region Configuration

        [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
        [SuppressMessage("ReSharper", "ConvertToConstant.Global")]
        public class Configuration
        {
            [JsonProperty("Enable lightnings at night")]
            public bool enableLightningEffects = true;

            [JsonProperty("Enable fog at night")]
            public bool enableFoggyNights = true;

            [JsonProperty("Enable junk pile decorations")]
            public bool enableJunkPileDecoration = true;

            [JsonProperty("Max decorations per junk pile")]
            public int maxJunkPileDecorations = 2;

            [JsonProperty("Max total junk pile decorations (set lower to reduce performance impact)")]
            public int maxJunkPileDecorationsTotal = 2000;

            [JsonProperty("Enable grave yards")]
            public bool enableGraveYards = true;

            [JsonProperty("Enable fire when grave yards despawn (impacts performance)")]
            public bool enableGraveYardFire = false;

            [JsonProperty("Show graveyards on the map (requires https://codefling.com/plugins/marker-api)")]
            public bool enableGraveyardMapMarker = true;

            [JsonProperty("Map marker settings", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public MapMarkerSettings mapMarkerSettings = new();

            [JsonProperty("Spawn graveyards during this time")]
            public StartEndTime graveyardSpawnTime = new(19, 7);

            [JsonProperty("Grave yard zombie health")]
            public float graveyardZombieHealth = 120f;

            [JsonProperty("Grave yard population at night")]
            public int graveyardPopulation = 20;

            [JsonProperty("Allow grave yards on roads")]
            public bool allowGraveyardOnRoad = true;

            [JsonProperty("Grave yard despawn time (seconds)")]
            public float graveyardDespawnTime = 40f;

            [JsonProperty("Minimum distance between grave yards")]
            public float minGraveyardDist = 50f;

            [JsonProperty("Grave yard configurations to spawn", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> graveyardConfigs = new List<string>
            {
                "_default"
            };

            [JsonProperty("Grave yard loot table", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<LootManager.LootItem> graveyardLootTable = new List<LootManager.LootItem>
            {
                new("scrap", 5, 10, 1f),
                new("corn", 2, 8, 0.8f),
                new("potato", 1, 2, 0.2f),
                new("skull", 1, 1, 0.5f),
                new("knife.butcher", 1, 1, 0.4f),
                new("pitchfork", 1, 1, 0.3f),
                new("halloween.lootbag.small", 1, 3, 0.7f),
                new("halloween.lootbag.medium", 1, 2, 0.3f),
                new("halloween.lootbag.large", 1, 1, 0.05f),
                new("sulfur", 50, 150, 0.3f),
                new("cloth", 5, 40, 0.3f),
                new("frankensteintable", 1, 1, 0.1f),
                new("frankensteins.monster.03.head", 1, 1, 0.15f),
                new("frankensteins.monster.03.legs", 1, 1, 0.15f),
                new("frankensteins.monster.03.torso", 1, 1, 0.15f),
                new("frankensteins.monster.02.head", 1, 1, 0.2f),
                new("frankensteins.monster.02.legs", 1, 1, 0.2f),
                new("frankensteins.monster.02.torso", 1, 1, 0.2f),
                new("frankensteins.monster.01.head", 1, 1, 0.3f),
                new("frankensteins.monster.01.legs", 1, 1, 0.3f),
                new("frankensteins.monster.01.torso", 1, 1, 0.3f),
            };
            
            [JsonProperty("Grave yard zombie configuration")]
            public NpcConfig graveyardNpcConfig = new();

            [JsonProperty("Enable sound effects at night")]
            public bool enableSoundEffects = true;

            [JsonProperty("Minimum time between sound effects (per player, in minutes)")]
            public float minSoundEffectDelay = 5f;

            [JsonProperty("List of sound effects", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> soundEffects = new List<string>
            {
                "assets/bundled/prefabs/fx/player/beartrap_scream.prefab",
                "assets/bundled/prefabs/fx/player/howl.prefab"
            };

            [JsonProperty("Custom NPC Clothing", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, ClothingItem[]> npcClothing = new Dictionary<string, ClothingItem[]>
            {
                ["scientistnpc_junkpile_pistol"] = ClothingItem.Single("halloween.mummysuit", 0UL),
                ["scientistnpc_heavy"] = new[]
                {
                    new ClothingItem("tshirt.long", 2461910209),
                    new ClothingItem("metal.facemask", 1171198416),
                    new ClothingItem("pants", 2461910644),
                    new ClothingItem("gloweyes", 0),
                    new ClothingItem("shoes.boots", 2461911046),
                    new ClothingItem("tactical.gloves", 0),
                },
                ["scientistnpc_oilrig"] = new[]
                {
                    new ClothingItem("burlap.headwrap", 2857176433),
                    new ClothingItem("burlap.shirt", 2857177309),
                    new ClothingItem("burlap.trousers", 2857176980),
                    new ClothingItem("shoes.boots", 916448999),
                    new ClothingItem("tactical.gloves", 0),
                    new ClothingItem("gloweyes", 0),
                },
                ["stables_shopkeeper"] = new[]
                {
                    new ClothingItem("pumpkin", 0),
                    new ClothingItem("gloweyes", 0),
                },
                ["bandit_conversationalist"] = new[]
                {
                    new ClothingItem("pumpkin", 0),
                    new ClothingItem("gloweyes", 0),
                },
            };

            [JsonProperty("Custom NPC clothing ignored NPC skins")]
            public HashSet<ulong> ignoreNpcSkins = new HashSet<ulong>
            {
                11162132011012
            };

            [JsonProperty("Loot jumpscare configuration", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public JumpscareConfig lootJumpscares = new()
            {
                Enabled = true,
                ChancePercent = 5f,
                DelayMinutes = 30,
                NpcName = "Ghost",
                Sounds = new List<string>()
                {
                    "_laugh",
                    "_scream"
                }
            };
            
            [JsonProperty("Random jumpscare configuration", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public JumpscareConfig randomJumpscares = new()
            {
                Enabled = true,
                ChancePercent = 5f,
                DelayMinutes = 30,
                NpcName = "Ghost",
                Sounds = new List<string>
                {
                    "_laugh",
                    "_scream"
                },
                Effects = new List<string>
                {
                    "assets/prefabs/deployable/reactive target/effects/snd_knockdown.prefab",
                    "assets/prefabs/clothes/halloween.scarecrow/effects/soul_release_effect.prefab",
                    "assets/bundled/prefabs/fx/item_break.prefab",
                    "assets/prefabs/building/door.hinged/effects/door-wood-knock.prefab",
                    "assets/prefabs/weapons/cleaver big/effects/hit.prefab",
                    "assets/prefabs/weapons/sword big/effects/hit.prefab",
                    "assets/bundled/prefabs/fx/player/gutshot_scream.prefab"
                }
            };

            // [JsonProperty("Jumpscare chance (%)")]
            // public float jumpscareChance = 5f;
            //
            // [JsonProperty("Minimum time between jumpscares (per player, in minutes)")]
            // public float minJumpscareDelay = 30f;
            //
            // [JsonProperty("Jumpscare sounds", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            // public List<string> jumpscareSounds = new List<string>
            // {
            //     "_laugh",
            //     "_scream"
            // };

            [JsonProperty("Recording command name")]
            public string CmdRecName { get; set; } = "rec";
            
            [JsonProperty("Grave yard command name")]
            public string CmdGyName { get; set; } = "gy";
            
            [JsonProperty("Junk pile decoration command name")]
            public string CmdJpName { get; set; } = "jp";
            
            [JsonProperty("Jumpscare command name")]
            public string CmdScareName { get; set; } = "jumpscare";

            [JsonIgnore] // This is very bad style I know
            public string RandomSoundEffect => soundEffects.IsNullOrEmpty() ? "assets/bundled/prefabs/fx/player/beartrap_scream.prefab" : soundEffects.GetRandom();

            [JsonIgnore] // And also this one
            public GraveyardConfig RandomGraveyardConfig => ConfigManager.LoadGraveyardConfig(graveyardConfigs.IsNullOrEmpty() ? "_default" : graveyardConfigs.GetRandom());
        }
        
        public class NpcConfig
        {
            [JsonProperty("Npc name")]
            public string name = "Zombie";
            [JsonProperty("Health")]
            public float health = 150f;

            [JsonProperty("Attack range multiplier")]
            public float attackRangeMultiplier = 1;
            [JsonProperty("Sense range (m)")]
            public float senseRange = 50f;
            [JsonProperty("Vision cone (degrees)")]
            public float visionCone = 135f;
            [JsonProperty("Damage scale (1 = 100%)")]
            public float damageScale = 1f;
            [JsonProperty("Memory duration (seconds)")]
            public float memoryDuration = 60f;
            [JsonProperty("Roam range (m)")]
            public float roamRange = 30f;
            [JsonProperty("Chase range (m)")]
            public float chaseRange = 50f;

            [JsonProperty("Remove corpse on death and drop bag")]
            public bool removeCorpse = false;

            [JsonProperty("Kit (requires Kits plugin)")]
            public string kit = string.Empty;

            [JsonProperty("Clothing items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Item> clothing = new List<Item>
            {
                new Item { shortName = "scarecrow.suit" }
            };
            [JsonProperty("Belt items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Item> belt = new()
            {
                new Item { shortName = "pitchfork" }
            };

            [JsonProperty("Custom loot configuration")]
            public LootManager.LootTable lootTable = new()
            {
                enabled = false,
                minItems = 1,
                maxItems = 5,
                items = new List<LootManager.LootItem>()
                {
                    new("scrap", 5, 10, 1f)
                }
            };

            public class Item
            {
                public string shortName;
                public int amount = 1;
                public ulong skinId;
            }
        }

        public class ClothingItem
        {
            [JsonProperty("Item shortname")]
            public string shortname;
            [JsonProperty("Skin id")]
            public ulong skin;

            public ClothingItem(string shortname, ulong skin)
            {
                this.shortname = shortname;
                this.skin = skin;
            }

            public static ClothingItem[] Single(string shortname, ulong skin)
            {
                return new ClothingItem[] { new ClothingItem(shortname, skin) };
            }
        }

        public class MapMarkerSettings
        {
            [JsonProperty("Name")]
            public string name = "A Graveyard";

            [JsonProperty("Radius")]
            public float radius = 0.2f;

            [JsonProperty("Color (hex format)")]
            public string color = "#FFFF00";
        }

        public struct StartEndTime
        {
            public int start;
            public int end;

            public StartEndTime(int start, int end)
            {
                this.start = start;
                this.end = end;
            }
        }

        public class JumpscareConfig
        {
            [JsonProperty("Enabled")]
            public bool Enabled { get; set; }
            
            [JsonProperty("Jumpscare NPC name")]
            public string NpcName { get; set; }
            
            [JsonProperty("Jumpscare chance (%)")]
            public float ChancePercent { get; set; }
            
            [JsonProperty("Minimum time between jumpscares (per player, in minutes)")]
            public int DelayMinutes { get; set; }

            [JsonProperty("Jumpscare sounds - played when the player is looking at the npc")]
            public List<string> Sounds { get; set; }

            [JsonProperty("Attention sound effects - played when the player is not looking at the npc", NullValueHandling = NullValueHandling.Ignore)]
            public List<string> Effects { get; set; }
        }
        
        #endregion
    }

    namespace HalloweenExtensions
    {
        internal static class HalloweenEx
        {
            public static float AngleTo(this Vector3 origin, Vector3 target)
            {
                Vector3 norm = (target - origin);
                float a = norm.z < 0 ? 180f : 0f;
                return Mathf.Atan(norm.x / norm.z) * Mathf.Rad2Deg + a;
            }

            public static Vector3 RandomPositionAround(this BaseEntity entity, float radius, float minDistance = 0f)
            {
                float distance = UnityEngine.Random.Range(minDistance, radius);
                float angle = UnityEngine.Random.Range(0f, 359f);

                var delta = Quaternion.Euler(0, angle, 0) * (Vector3.forward * distance);
                return entity.transform.position + delta;
            }

            public static bool IsNearBase(this BasePlayer player)
            {
                var bp = player.GetBuildingPrivilege();
                return bp?.IsAuthed(player) ?? false;
            }
        }
    }
}

namespace PluginComponents.Halloween{using JetBrains.Annotations;using Oxide.Plugins;using System;[AttributeUsage(AttributeTargets.Field,AllowMultiple=false),MeansImplicitUse]public sealed class PermAttribute:Attribute{}[AttributeUsage(AttributeTargets.Method,AllowMultiple=false),MeansImplicitUse]public sealed class UniversalCommandAttribute:Attribute{public UniversalCommandAttribute(string name){Name=name;}public string Name{get;set;}public string Permission{get;set;}}[AttributeUsage(AttributeTargets.Method),MeansImplicitUse]public sealed class HookAttribute:Attribute{}[AttributeUsage(AttributeTargets.Method,Inherited=false)]public sealed class DebugAttribute:Attribute{}[AttributeUsage(AttributeTargets.Method)]public sealed class DefaultReturnAttribute:Attribute{public DefaultReturnAttribute(object value){}}public class MinMaxInt{public int min;public int max;public MinMaxInt(){}public MinMaxInt(int value):this(value,value){}public MinMaxInt(int min,int max){this.min=min;this.max=max;}public int Random(){return UnityEngine.Random.Range(min,max+1);}}}namespace PluginComponents.Halloween.Core{using Oxide.Core.Plugins;using Oxide.Core;using Oxide.Plugins;using Newtonsoft.Json;using System.IO;using UnityEngine;using System;using System.Diagnostics;using System.Collections.Generic;using System.Linq;using Facepunch.Extend;using System.Reflection;using PluginComponents.Halloween;public abstract class BasePlugin<TPlugin,TConfig>:BasePlugin<TPlugin>where TConfig:class,new()where TPlugin:RustPlugin{protected new static TConfig Config{get;private set;}private string ConfigPath=>Path.Combine(Interface.Oxide.ConfigDirectory,$"{Name}.json");protected override void LoadConfig()=>ReadConfig();protected override void SaveConfig()=>WriteConfig();protected override void LoadDefaultConfig()=>Config=new TConfig();private void ReadConfig(){if(File.Exists(ConfigPath)){Config=JsonConvert.DeserializeObject<TConfig>(File.ReadAllText(ConfigPath));if(Config==null){LogError("[CONFIG] Your configuration file contains an error. Using default configuration values.");LoadDefaultConfig();}}else{LoadDefaultConfig();}WriteConfig();}private void WriteConfig(){var directoryName=Utility.GetDirectoryName(ConfigPath);if(directoryName!=null&&!Directory.Exists(directoryName)){Directory.CreateDirectory(directoryName);}if(Config!=null){string text=JsonConvert.SerializeObject(Config,Formatting.Indented);File.WriteAllText(ConfigPath,text);}else{LogError("[CONFIG] Saving failed - config is null");}}}public abstract class BasePlugin<TPlugin>:BasePlugin where TPlugin:RustPlugin{public new static TPlugin Instance{get;private set;}protected static string DataFolder=>Path.Combine(Interface.Oxide.DataDirectory,typeof(TPlugin).Name);protected override void Init(){base.Init();Instance=this as TPlugin;}protected override void Unload(){Instance=null;base.Unload();}}public abstract class BasePlugin:RustPlugin{public const int OSI_DELAY=5;public const bool CARBONARA=
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
protected virtual void OnServerInitialized(){}protected virtual void OnServerInitializedDelayed(){}public static void Log(string s){if(Instance!=null){Interface.Oxide.LogInfo($"[{Instance.Title}] {s}");}}[Conditional("DEBUG")]public static void LogDebug(string s){if(DEBUG&&Instance!=null){if(CARBONARA){LogWarning("[DEBUG] "+s);}else{Interface.Oxide.LogDebug($"[{Instance.Title}] {s}");}}}public static void LogWarning(string s){if(Instance!=null){Interface.Oxide.LogWarning($"[{Instance.Title}] {s}");}}public static void LogError(string s){if(Instance!=null){Interface.Oxide.LogError($"[{Instance.Title}] {s}");}}private Dictionary<string,CommandCallback>uiCallbacks;private string uiCommandBase;private void PrepareCommandHandler(){if(uiCallbacks==null){uiCallbacks=new();uiCommandBase=$"{Title.ToLower()}.uicmd";cmd.AddConsoleCommand(uiCommandBase,this,HandleCommand);}}private bool HandleCommand(ConsoleSystem.Arg arg){var cmd=arg.GetString(0);if(uiCallbacks.TryGetValue(cmd,out var callback)){var player=arg.Player();try{callback.ButtonCallback?.Invoke(player);callback.InputCallback?.Invoke(player,string.Join(' ',arg.Args?.Skip(1)??Enumerable.Empty<string>()));}catch(Exception ex){PrintError($"Failed to run UI command {cmd}: {ex}");}}return false;}public string CreateUiCommand(string guid,Action<BasePlayer>callback,bool singleUse){PrepareCommandHandler();uiCallbacks.Add(guid,new CommandCallback(callback,singleUse));return$"{uiCommandBase} {guid}";}public string CreateUiCommand(string guid,Action<BasePlayer,string>callback,bool singleUse){PrepareCommandHandler();uiCallbacks.Add(guid,new CommandCallback(callback,singleUse));return$"{uiCommandBase} {guid}";}private readonly struct CommandCallback{public readonly bool SingleUse;public readonly Action<BasePlayer>ButtonCallback;public readonly Action<BasePlayer,string>InputCallback;public CommandCallback(Action<BasePlayer>buttonCallback,bool singleUse){ButtonCallback=buttonCallback;InputCallback=null;SingleUse=singleUse;}public CommandCallback(Action<BasePlayer,string>inputCallback,bool singleUse){ButtonCallback=null;InputCallback=inputCallback;SingleUse=singleUse;}}public void ChatMessage(BasePlayer player,string message){if(player){player.SendConsoleCommand("chat.add",2,0,$"{ChatPrefix} {message}");}}}}namespace PluginComponents.Halloween.Extensions.BaseNetworkable{using PluginComponents.Halloween;using PluginComponents.Halloween.Extensions;public static class BaseNetworkableEx{public static bool IsNullOrDestroyed(this global::BaseNetworkable baseNetworkable){return!baseNetworkable||baseNetworkable.IsDestroyed;}}}namespace PluginComponents.Halloween.Extensions.Enumerable{using System;using System.Collections.Generic;using System.Linq;using PluginComponents.Halloween;using PluginComponents.Halloween.Extensions;public static class EnumerableExtensions{public static IEnumerable<T>GetRandom<T>(this IList<T>list,int count){if(list.Count<1){throw new InvalidOperationException("Can not take random element from empty list");}if(list.Count<=count){for(int i=0;i<list.Count;i++){yield return list[i];}yield break;}int returned=0;for(int i=0;i<list.Count;i++){float chance=(float)(count-returned)/(list.Count-i);if(UnityEngine.Random.Range(0f,1f)<=chance){returned++;yield return list[i];}}}public static T GetRandom<T>(this IList<T>list){if(list.Count<1){throw new InvalidOperationException("Can not take random element from empty list");}return list[UnityEngine.Random.Range(0,list.Count)];}public static T GetRandomOrDefault<T>(this IList<T>list){if(list.Count<1){return default(T);}return list[UnityEngine.Random.Range(0,list.Count)];}public static T GetRandom<T>(this IEnumerable<T>list){if(!list.Any()){throw new InvalidOperationException("Can not take random element from empty sequence");}return list.ElementAt(UnityEngine.Random.Range(0,list.Count()));}public static T GetRandomOrDefault<T>(this IEnumerable<T>list){if(!list.Any()){return default(T);}return list.ElementAt(UnityEngine.Random.Range(0,list.Count()));}}}namespace PluginComponents.Halloween.External.NpcSpawn{using System.Collections.Generic;using PluginComponents.Halloween;using PluginComponents.Halloween.External;public static class NpcSpawnStates{public const string ROAM="RoamState";public const string CHASE="ChaseState";public const string COMBAT="CombatState";public const string IDLE="IdleState";public const string COMBAT_STATIONARY="CombatStationaryState";public const string RAID="RaidState";public const string RAID_MELEE="RaidStateMelee";public const string SLEDGE="SledgeState";public const string BLAZER="BlazerState";}public class NpcSpawnNpcBelt{public string ShortName;public int Amount;public ulong SkinID;public IEnumerable<string>Mods;public string Ammo;}public class NpcSpawnNpcWear{public string ShortName;public ulong SkinID;}public class NpcSpawnConfig{public string Name{get;set;}public IEnumerable<NpcSpawnNpcWear>WearItems{get;set;}public IEnumerable<NpcSpawnNpcBelt>BeltItems{get;set;}public string Kit{get;set;}public float Health{get;set;}public float RoamRange{get;set;}public float ChaseRange{get;set;}public float SenseRange{get;set;}public float ListenRange{get;set;}public float AttackRangeMultiplier{get;set;}public bool CheckVisionCone{get;set;}public float VisionCone{get;set;}public float DamageScale{get;set;}public float TurretDamageScale{get;set;}public float AimConeScale{get;set;}public bool DisableRadio{get;set;}public bool CanRunAwayWater{get;set;}public bool CanSleep{get;set;}public float Speed{get;set;}public int AreaMask{get;set;}public int AgentTypeID{get;set;}public string HomePosition{get;set;}public float MemoryDuration{get;set;}public HashSet<string>States{get;set;}}}namespace PluginComponents.Halloween.Loot{using Newtonsoft.Json;using System;using System.Collections.Generic;using System.Linq;using UnityEngine;using PluginComponents.Halloween;public static class LootManager{public static void FillWithLoot(StorageContainer container,IEnumerable<LootItem>lootTable)=>FillWithLoot(container.inventory,lootTable);public static void FillWithLoot(ItemContainer container,IEnumerable<LootItem>lootTable){ClearContainer(container);int amt=0;foreach(var itm in lootTable){if(UnityEngine.Random.Range(0f,1f)<=itm.chance){var item=itm.CreateItem();if(item==null){continue;}if(!item.MoveToContainer(container)){item.Remove();}amt++;}if(amt>=container.capacity){break;}}}public static void FillWithLoot(StorageContainer container,LootTable lootTable)=>FillWithLoot(container.inventory,lootTable);public static void FillWithLoot(ItemContainer container,LootTable lootTable){if(!lootTable.Enabled){return;}if(lootTable.minItems<=0||lootTable.maxItems<=0){FillWithLoot(container,lootTable.items);return;}ClearContainer(container);const int max_retries=50;int itemAmount=0;var targetItemAmount=UnityEngine.Random.Range(lootTable.minItems,lootTable.maxItems+1);targetItemAmount=Mathf.Min(targetItemAmount,lootTable.items.Count);var included=new HashSet<string>();container.capacity=targetItemAmount;for(int i=0;(i<max_retries&&itemAmount<targetItemAmount);i++){foreach(var itm in lootTable.items){if(!included.Contains(itm.shortname)&&UnityEngine.Random.Range(0f,1f)<=itm.chance){var item=itm.CreateItem();if(item==null){continue;}if(!item.MoveToContainer(container)){item.Remove();}included.Add(itm.shortname);itemAmount++;}}}}private static void ClearContainer(ItemContainer container){container.Clear();ItemManager.DoRemoves();}public class LootTable{[JsonIgnore]public bool Enabled=>enabled&&items.Count>0;[JsonProperty("Enabled")]public bool enabled;[JsonProperty("Minimum items",DefaultValueHandling=DefaultValueHandling.Ignore)]public int minItems;[JsonProperty("Maximum items",DefaultValueHandling=DefaultValueHandling.Ignore)]public int maxItems;[JsonProperty("Item list",ObjectCreationHandling=ObjectCreationHandling.Replace)]public List<LootItem>items=new();public LootTable Copy(){return new LootTable{enabled=enabled,minItems=minItems,maxItems=maxItems,items=items.ToList(),};}}public class LootItem{[JsonProperty("Short name")]public string shortname;[JsonProperty("Min amount")]public int min;[JsonProperty("Max amount")]public int max;[JsonProperty("Chance (1 = 100%)")]public float chance;[JsonProperty("Skin id")]public ulong skin=0;[JsonProperty("Custom name")]public string customName=string.Empty;[JsonProperty("Text",DefaultValueHandling=DefaultValueHandling.Ignore)]public string text;[JsonIgnore]public ItemDefinition ItemDefinition=>ItemManager.FindItemDefinition(shortname);public LootItem(){shortname="scrap";min=5;max=10;chance=1f;skin=0;}public LootItem(string shortname,int min,int max,float chance){this.shortname=shortname;this.min=min;this.max=max;this.chance=chance;}public LootItem(string shortname,int min,int max,float chance,ulong skin){this.shortname=shortname;this.min=min;this.max=max;this.chance=chance;this.skin=skin;}public Item CreateItem(){if(ItemDefinition==null||ItemDefinition.itemid==-996920608){return null;}var itm=ItemManager.Create(ItemDefinition,UnityEngine.Random.Range(min,max+1),skin);itm?.OnVirginSpawn();if(customName!=null&&customName.Length>0){itm.name=customName;}if(text!=null&&text.Length>0){itm.text=text;}return itm;}public override int GetHashCode(){return HashCode.Combine(shortname,skin);}}}}namespace PluginComponents.Halloween.LoottableApi{using JetBrains.Annotations;using Oxide.Core.Plugins;using UnityEngine;using PluginComponents.Halloween;public static class LoottableApi{private static Plugin PluginInstance=>Oxide.Core.Interface.Oxide.RootPluginManager.GetPlugin("Loottable");public static void ClearPresets(Plugin plugin){PluginInstance?.Call("ClearPresets",plugin);}public static void CreatePresetCategory(Plugin plugin,string displayName){PluginInstance?.Call("CreatePresetCategory",plugin,displayName);}public static void CreatePreset(Plugin plugin,string key,string displayName,string iconOrUrl,bool isNpc=false){PluginInstance?.Call("CreatePreset",plugin,key,displayName,iconOrUrl,isNpc);}public static bool AssignPreset(Plugin plugin,string key,ItemContainer container){return PluginInstance?.Call<bool>("AssignPreset",plugin,key,container)??false;}public static bool AssignPreset(Plugin plugin,string key,ScientistNPC npc){return PluginInstance?.Call<bool>("AssignPreset",plugin,key,npc)??false;}public static bool AssignPreset(Plugin plugin,string key,StorageContainer container){return PluginInstance?.Call<bool>("AssignPreset",plugin,key,container)??false;}public static void ClearCustomItems(Plugin plugin){PluginInstance?.Call("ClearCustomItems",plugin);}public static void AddCustomItem(Plugin plugin,int itemId,ulong skinId,string customName=null,bool persistent=false){PluginInstance?.Call("AddCustomItem",plugin,itemId,skinId,customName,persistent);}}}namespace PluginComponents.Halloween.MapMarker{using Oxide.Core;using PluginComponents.Halloween.Core;using PluginComponents.Halloween.Extensions.BaseNetworkable;using System;using System.Collections.Generic;using System.Linq;using System.Text;using UnityEngine;using PluginComponents.Halloween;public class CustomMapMarker{public Vector3 Position{get=>vendingMarker.transform.position;set=>vendingMarker.transform.position=value;}private VendingMachineMapMarker vendingMarker;private MapMarkerGenericRadius colorMarker;private MapMarkerGenericRadius[]paintMarkers;private readonly string[]lines;private bool isParented;private CustomMapMarker(int lines){if(lines<1){throw new ArgumentException("Marker line count must be positive",nameof(lines));}this.lines=new string[lines];}private void Spawn(Vector3 position,Color color,float radius,BaseEntity parent){isParented=parent!=null;vendingMarker=GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab",position).GetComponent<VendingMachineMapMarker>();vendingMarker.enableSaving=false;vendingMarker.Spawn();if(isParented){vendingMarker.SetParent(parent);}vendingMarker.SendNetworkUpdate();colorMarker=CreateColorMarker(color,radius,vendingMarker);}private static MapMarkerGenericRadius CreateColorMarker(Color color,float radius,VendingMachineMapMarker markerParent,Vector3 offset=default){var colorMarker=GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab",offset).GetComponent<MapMarkerGenericRadius>();colorMarker.color1=color;colorMarker.color2=colorMarker.color1;colorMarker.radius=radius*(4000f/World.Size);colorMarker.alpha=color.a;colorMarker.enableSaving=false;colorMarker.SetParent(markerParent);colorMarker.Spawn();colorMarker.SendNetworkUpdate();colorMarker.SendUpdate();return colorMarker;}public void SetText(string text,bool networkUpdate=true)=>SetText(0,text,networkUpdate);public void SetText(int line,string text,bool networkUpdate=true){lines[line]=text.Trim();vendingMarker.markerShopName=String.Join('\n',lines);if(networkUpdate){SendNetworkUpdate();}}public void SendNetworkUpdate(bool fullUpdate=false){vendingMarker.SendNetworkUpdate();if(isParented||fullUpdate){colorMarker.SendNetworkUpdate();colorMarker.SendUpdate();if(paintMarkers!=null){foreach(var marker in paintMarkers){marker.SendNetworkUpdate();marker.SendUpdate();}}}}public void Destroy(){if(paintMarkers!=null){foreach(var marker in paintMarkers){if(!marker.IsNullOrDestroyed()){marker.Kill();}}}if(!colorMarker.IsNullOrDestroyed()){colorMarker.Kill();}if(!vendingMarker.IsNullOrDestroyed()){vendingMarker.Kill();}}public void Paint(Color color,Func<IEnumerable<Vector2>>dots,float dotSize){if(paintMarkers!=null){throw new InvalidOperationException("Marker can only be painted once");}var markerList=new List<MapMarkerGenericRadius>();foreach(var dot in dots.Invoke()){var marker=CreateColorMarker(color,dotSize,vendingMarker,dot.XZ3D());markerList.Add(marker);}paintMarkers=markerList.ToArray();}public static CustomMapMarker Create(Vector3 position,float radius,Color color,int lines=1)=>Create(null,position,radius,color,lines);public static CustomMapMarker Create(BaseEntity parent,Vector3 localPosition,float radius,Color color,int lines=1){var marker=new CustomMapMarker(lines);marker.Spawn(localPosition,color,radius,parent);marker.SendNetworkUpdate();return marker;}}}namespace PluginComponents.Halloween.SpawnPoint{using System;using System.Collections.Generic;using UnityEngine;using PluginComponents.Halloween.Core;using Facepunch;using JetBrains.Annotations;using Oxide.Core;using Rust;using System.Collections;using System.Diagnostics.CodeAnalysis;using PluginComponents.Halloween;[SuppressMessage("ReSharper","BitwiseOperatorOnEnumWithoutFlags")]public class SpawnPointManager{public const int DEFAULT_BATCH_SIZE=100_000;public const TerrainTopology.Enum TP_WATER=TerrainTopology.Enum.Ocean|TerrainTopology.Enum.Oceanside|TerrainTopology.Enum.Lake|TerrainTopology.Enum.Lakeside|TerrainTopology.Enum.River|TerrainTopology.Enum.Riverside|TerrainTopology.Enum.Swamp|TerrainTopology.Enum.Offshore;public const TerrainTopology.Enum TP_ROAD=TerrainTopology.Enum.Road|TerrainTopology.Enum.Roadside|TerrainTopology.Enum.Rail|TerrainTopology.Enum.Railside;public const TerrainTopology.Enum TP_BUILDING=TerrainTopology.Enum.Building|TerrainTopology.Enum.Monument;public const TerrainTopology.Enum TP_ROCK=TerrainTopology.Enum.Hilltop|TerrainTopology.Enum.Mountain|TerrainTopology.Enum.Cliff|TerrainTopology.Enum.Cliffside|TerrainTopology.Enum.Clutter|TerrainTopology.Enum.Decor;public const TerrainTopology.Enum TP_DEFAULT=TP_ROCK|TP_WATER|TP_BUILDING;public bool Initialized{get;private set;}public int GridSize{get;set;}public readonly SpawnPointConstraints Constraints=new();private readonly List<Vector3>spawnPoints=new();public SpawnPointManager(int gridSize,float overrideTopologyRadius=-1){GridSize=gridSize;Constraints.TopologyRadius=overrideTopologyRadius>0?overrideTopologyRadius:gridSize;}public void Configure(Action<SpawnPointConstraints>configure){configure.Invoke(Constraints);}public bool TryGetRandomSpawnPoint(out Vector3 point,int attempts=20,[CanBeNull]Func<Vector3,bool>customValidator=null){point=default;if(!Initialized){BasePlugin.LogError($"Failed to get spawn point - not initialized");return false;}for(int attempt=0;attempt<attempts;attempt++){point=spawnPoints.GetRandom();if(IsValidSpawnPoint(point)){if(customValidator==null){return true;}if(customValidator.Invoke(point)){return true;}}}BasePlugin.LogWarning($"Failed to get spawn point - attempt limit exceeded");return false;}public IEnumerator CacheSpawnPoints(int batchSize=DEFAULT_BATCH_SIZE){var ws=Mathf.RoundToInt(World.Size/2f);int count=0,vcount=0,tcount=(ws*2/GridSize)*(ws*2/GridSize);for(int x=-ws;x<ws;x+=GridSize){for(int z=-ws;z<ws;z+=GridSize){if(batchSize>0&&count%batchSize==0){yield return CoroutineEx.waitForEndOfFrame;}if(batchSize>0&&count%(batchSize*2)==0){BasePlugin.Log($"Finding spawn points {((float)count/tcount*100f):N0}% ({count} / {tcount})");}var point=new Vector3(x,0,z);var height=TerrainMeta.HeightMap.GetHeight(point);point.y=height;if(IsValidSpawnPoint(point)){vcount++;spawnPoints.Add(point);}count++;}}BasePlugin.LogDebug($"Found {vcount:N0} valid spawn points of {tcount:N0}");BasePlugin.Log("done");Initialized=true;}private bool IsValidSpawnPoint(Vector3 point){float height=TerrainMeta.HeightMap.GetHeight(point);if(!Constraints.ValidHeight.IsInRange(height)){return false;}var topology=(TerrainTopology.Enum)TerrainMeta.TopologyMap.GetTopology(point,Constraints.TopologyRadius);if((topology&Constraints.BlockedTopology)>0){return false;}if(Constraints.MinDistanceToBuildings>0){var list=Pool.Get<List<BaseEntity>>();Vis.Entities(point,Constraints.MinDistanceToBuildings,list,Layers.Mask.Construction|Layers.Mask.Deployed);list.RemoveAll(x=>!x||x.IsDestroyed);list.RemoveAll(x=>x.IsNpc||!x.OwnerID.IsSteamId());var fail=list.Count>0;Pool.FreeUnmanaged(ref list);if(fail){return false;}}if(Constraints.MinDistanceToSafeZones>0){var list=Pool.Get<List<TriggerBase>>();GamePhysics.OverlapSphere(point,Constraints.MinDistanceToSafeZones,list,262144,QueryTriggerInteraction.Collide);var fail=false;foreach(var trigger in list){if(trigger!=null&&trigger.GetComponent<TriggerSafeZone>()){fail=true;break;}}Pool.FreeUnmanaged(ref list);if(fail){return false;}}if(Constraints.CustomValidator!=null&&!Constraints.CustomValidator.Invoke(point)){return false;}return true;}private static BuildingPrivlidge GetBuildingPrivilege(Vector3 position,float radius){var obb=new OBB(position,Quaternion.identity,new Bounds(Vector3.zero,new Vector3(radius*2f,8f,radius*2f)));BuildingBlock other=null;BuildingPrivlidge result=null;List<BuildingBlock>buildingBlocks=Pool.Get<List<BuildingBlock>>();Vis.Entities(obb.position,16f+obb.extents.magnitude,buildingBlocks,2097152);for(int i=0;i<buildingBlocks.Count;i++){BuildingBlock buildingBlock=buildingBlocks[i];if(!buildingBlock.IsOlderThan(other)||obb.Distance(buildingBlock.WorldSpaceBounds())>16f){continue;}BuildingManager.Building building=buildingBlock.GetBuilding();if(building!=null){BuildingPrivlidge dominatingBuildingPrivilege=building.GetDominatingBuildingPrivilege();if(!(dominatingBuildingPrivilege==null)){other=buildingBlock;result=dominatingBuildingPrivilege;}}}Pool.FreeUnmanaged(ref buildingBlocks);return result;}public class SpawnPointConstraints{public TerrainTopology.Enum BlockedTopology{get;set;}public float TopologyRadius{get;set;}public float MinDistanceToBuildings{get;set;}public float MinDistanceToSafeZones{get;set;}public Range<float>ValidHeight{get;set;}public Func<Vector3,bool>CustomValidator;public void BlockTopology(params TerrainTopology.Enum[]topology){foreach(var layer in topology){BlockedTopology|=layer;}}}}public struct Range<T>where T:IComparable<T>{private T min;public T Min{get=>MinIsSet?min:default;set{min=value;MinIsSet=true;}}public bool MinIsSet{get;private set;}private T max;public T Max{get=>MaxIsSet?max:default;set{max=value;MaxIsSet=true;}}public bool MaxIsSet{get;private set;}public bool IsInRange(T value){if(MinIsSet&&value.CompareTo(min)<0){return false;}if(MaxIsSet&&value.CompareTo(max)>0){return false;}return true;}}}namespace PluginComponents.Halloween.Tools.Entity{using Facepunch;using JetBrains.Annotations;using PluginComponents.Halloween.Extensions.BaseNetworkable;using System;using System.Collections.Generic;using System.Reflection;using UnityEngine;using PluginComponents.Halloween;using PluginComponents.Halloween.Tools;public static class EntityTools{public static TCustom CreateCustomEntity<TEnt,TCustom>(string prefab,Vector3 position=default,Quaternion rotation=default)where TEnt:BaseEntity where TCustom:TEnt{var entity=CreateEntity<TEnt>(prefab,position,rotation,false,false);var customEntity=entity.gameObject.AddComponent<TCustom>();var fields=typeof(TEnt).GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);foreach(var field in fields){field.SetValue(customEntity,field.GetValue(entity));}UnityEngine.Object.DestroyImmediate(entity,true);customEntity.gameObject.AwakeFromInstantiate();return customEntity;}public static T CreateEntity<T>(string prefab,bool save=false)where T:BaseEntity=>CreateEntity<T>(prefab,default,Quaternion.identity,save,true);public static T CreateEntity<T>(string prefab,Vector3 position,bool save=false)where T:BaseEntity=>CreateEntity<T>(prefab,position,Quaternion.identity,save,true);public static T CreateEntity<T>(string prefab,Vector3 position,Vector3 rotation,bool save=false)where T:BaseEntity=>CreateEntity<T>(prefab,position,Quaternion.Euler(rotation),save,true);public static T CreateEntity<T>(string prefab,Vector3 position,Quaternion rotation,bool save=false)where T:BaseEntity=>CreateEntity<T>(prefab,position,rotation,save,true);private static T CreateEntity<T>(string prefab,Vector3 position,Quaternion rotation,bool save,bool active)where T:BaseEntity{var ent=GameManager.server.CreateEntity(prefab,position,rotation,active);if(ent is not T entity){UnityEngine.Object.Destroy(ent);throw new InvalidCastException($"Failed to create entity of type '{typeof(T).Name}' from '{prefab}'");}ent.enableSaving=save;return entity;}public static void KillSafe<T>([ItemCanBeNull]IEnumerable<T>entities)where T:BaseEntity{var list=Pool.Get<List<BaseEntity>>();list.AddRange(entities);Kill(list);Pool.FreeUnmanaged(ref list);}public static void Kill<T>([ItemCanBeNull]IEnumerable<T>entities)where T:BaseEntity{foreach(var entity in entities){Kill(entity);}}public static void Kill([CanBeNull]BaseEntity entity){if(entity is not null&&!entity.IsDestroyed){entity.Kill();}}}}