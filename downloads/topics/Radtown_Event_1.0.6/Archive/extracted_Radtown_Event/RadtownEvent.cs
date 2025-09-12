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

using Facepunch;
using \u0048\u0061\u0072\u006D\u006F\u006E\u0079Lib;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using PluginComponents.RadtownEvent;
using PluginComponents.RadtownEvent.Core;
using PluginComponents.RadtownEvent.Cui;
using PluginComponents.RadtownEvent.Cui.Style;
using PluginComponents.RadtownEvent.Event;
using PluginComponents.RadtownEvent.Extensions.BaseNetworkable;
using PluginComponents.RadtownEvent.Extensions.Command;
using PluginComponents.RadtownEvent.Extensions.Reflection;
using PluginComponents.RadtownEvent.Tools.Entity;
using PluginComponents.RadtownEvent.Zone;
using PluginComponents.RadtownEvent.Extensions.Lang;
using PluginComponents.RadtownEvent.External.Notify;
using PluginComponents.RadtownEvent.Loot;
using PluginComponents.RadtownEvent.LoottableApi.Static;
using PluginComponents.RadtownEvent.MapMarker;
using Rust;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/*
 * VERSION HISTORY
 *
 * V 1.0.1
 * - give rugs more health
 * - fix crate hack time bound to ConVar
 * - fix Failed to set value of field RadContainer.dressingVariant
 * - fix waves not spawning correctly
 * - add ui indicator for waves
 * - add despawn timer if event is not started by player
 * - adjust default config
 *
 * V 1.0.2
 * - fix bug in LootManager
 *
 * V 1.0.3
 * - add support for TruePVE
 * - add dynamic hook subscriptions to improve performance
 * - add automatically destroy drones that get stuck
 * - add loot to drones
 * - support Loottable plugin
 * - fix FormatException with outdated lang files
 * - fix UI not displayed correctly on large UI scale
 *
 * V 1.0.4
 * - add map marker
 * - add config option for chat prefix
 * - fix conflict with LootDefender
 *
 * V 1.0.5
 * - prevent players from damaging container
 *
 * V 1.0.6
 * - fix for December rust update
 * - add support for Notify
 * 
 */

namespace Oxide.Plugins
{
    [Info(nameof(RadtownEvent), "The_Kiiiing", "1.0.6")]
    internal class RadtownEvent : BasePlugin<RadtownEvent, RadtownEvent.Configuration>
    {
        [Perm]
        private const string PERM_ADMIN = "radtownevent.admin";

        private static readonly Dictionary<HackableLockedCrate, int> _hackSecondsOverride = new();
       
        private RadtownEventController _eventController;

        private static MonumentInfo _radtownMonument;

        protected override Color ChatColor => new(1f, 0.83f, 0);

        protected override string ChatPrefix => Config.OverrideChatPrefix ?? base.ChatPrefix;

        #region Hooks
        
        protected override void Init()
        {
            base.Init();

            Unsubscribe();

            \u0048\u0061\u0072\u006D\u006F\u006E\u0079Instance.Patch(AccessTools.Method(typeof(HackableLockedCrate), "HackProgress"), AccessTools.Method(typeof(HackableLockedCrate_HackProgress), nameof(HackableLockedCrate_HackProgress.Prefix)));
        }

        protected override void OnServerInitialized()
        {
            base.OnServerInitialized();

            _radtownMonument = TerrainMeta.Path.Monuments.Find(x => x.name == "assets/bundled/prefabs/autospawn/monument/roadside/radtown_1.prefab");
            if (_radtownMonument == null)
            {
                LogError("This plugin requires the radtown monument to work. Please install a map that contains the radtown monument to use this plugin");
                NextTick(() => Interface.Oxide.UnloadPlugin(nameof(RadtownEvent)));
                return;
            }
            
            _eventController = EventController.Create<RadtownEventController>();
            _eventController.EventDelaySeconds = Config.EventDelayMinutes * 60;
            _eventController.StartEventLoop();
        }

        protected override void OnServerInitializedDelayed()
        {
            base.OnServerInitializedDelayed();

            if (!NotifyExtensions.NotifyAvailable)
            {
                LogError("Notify is not installed! No notifications will be displayed. To get rid of this warning disable Notify in the configuration");
            }
            
            LoottableApi.ClearPresets(this);
            LoottableApi.CreatePreset(this,"c_locked", "Container Locked Crate", "crate_hackable");
            LoottableApi.CreatePreset(this,"drone", "Attack Drone", "https://www.corrosionhour.com/wp-content/uploads/2021/06/rust-drone.png");
        }

        protected override void Unload()
        {
            _eventController?.Destroy();

            base.Unload();
        }

        [Hook] void OnCrateHack(HackableLockedCrate crate)
        {
            _eventController?.OnCrateHacked(crate);
        }
        
        [Hook] void OnCrateHackEnd(HackableLockedCrate crate)
        {
            _eventController?.OnCrateFullyHacked(crate);
        }

        private void OnAttackDroneKilled(AttackDrone attackDrone)
        {
            _eventController?.OnAttackDroneKilled(attackDrone);
        }
        
        private void OnRadContainerDestroyed(RadContainer container)
        {
            _eventController?.OnRadContainerDestroyed(container);
        }

        private void OnEventZoneDestroyed()
        {
            LogDebug("Event Zone Destroyed");
            _eventController.EndEvent();
        }
        
        // TruePVE
        [Hook] object CanEntityTakeDamage(Drone target, HitInfo info)
        {
            if (_eventController.EventZone && _eventController.EventZone.Drones.Contains(target))
            {
                return true;
            }

            return null;
        }

        // LootDefender
        [Hook] object OnLootLockedEntity(BasePlayer player, HackableLockedCrate crate)
        {
            if (_eventController.EventZone?.Crate == crate)
            {
                return true;
            }

            return null;
        }

        private void Subscribe()
        {
            LogDebug("Subscribe");
            
            Subscribe(nameof(OnCrateHack));
            Subscribe(nameof(OnCrateHackEnd));
            Subscribe(nameof(CanEntityTakeDamage));
        }
        
        private void Unsubscribe()
        {
            LogDebug("Unsubscribe");
            
            Unsubscribe(nameof(OnCrateHack));
            Unsubscribe(nameof(OnCrateHackEnd));
            Unsubscribe(nameof(CanEntityTakeDamage));
        }
        
        
        #endregion
        
        #region Event Controller

        class RadtownEventController : EventController
        {
            public EventZone EventZone { get; private set; }

            private CH47HelicopterAIController _cargoHeli;
            private CH47LandingZone _heliLandingZone;

            protected override void OnEventStart()
            {
                Instance.Subscribe();
                
                Log("Radtown event started");
                OnContainerDelivered();
            }

            protected override void OnEventEnd()
            {
                if (EventZone)
                {
                    EventZone.Destroy();
                }
                EventZone = null;
                
                if (_heliLandingZone != null)
                {
                    Destroy(_heliLandingZone.gameObject);
                    _heliLandingZone = null;
                }

                EntityTools.Kill(_cargoHeli);
                
                Instance.Unsubscribe();
            }

            private void OnContainerDelivered()
            {
                Interface.CallHook("OnRadtownEventContainerDelivered");
                BroadcastLang("container_delivered", MapHelper.PositionToString(_radtownMonument.transform.position));
                EventZone = Zone.Create<EventZone>(_radtownMonument.transform.position, EventZone.RADIUS, Config.EventZoneVisible ? 5 : 0);
            }

            public void OnCrateHacked(HackableLockedCrate crate)
            {
                if (EventZone != null && EventZone.Crate == crate)
                {
                    EventZone.OnCrateHacked();
                }
            }
            
            public void OnCrateFullyHacked(HackableLockedCrate crate)
            {
                if (EventZone != null && EventZone.Crate == crate)
                {
                    EventZone.OnCrateFullyHacked();
                }
            }

            public void OnAttackDroneKilled(AttackDrone drone)
            {
                EventZone?.OnDroneKilled(drone);
            }
            
            public void OnRadContainerDestroyed(RadContainer container)
            {
                if (EventZone != null && container == EventZone.Container)
                {
                    EventZone?.OnContainerDestroyed();
                }
            }

            // private void SpawnContainerHeli()
            // {
            //     var spawnPoint = DebugPlayer?.transform.position ?? TerrainMeta.RandomPointOffshore();
            //     _cargoHeli = EntityTools.CreateCustomEntity<CH47HelicopterAIController, CH47ContainerHelicopter>("assets/prefabs/npc/ch47/ch47scientists.entity.prefab", spawnPoint);
            //     _cargoHeli.SetLandingTarget(_radtownMonument.transform.position + new Vector3(0, 10, 0));
            //     _cargoHeli.Spawn();
            //
            //     if (_heliLandingZone != null)
            //     {
            //         Destroy(_heliLandingZone.gameObject);
            //     }
            //
            //     // _heliLandingZone = new GameObject().AddComponent<CH47LandingZone>();
            //     // _heliLandingZone.transform.position = _radtownMonument.transform.position;
            // }
        }

        #endregion
        
        #region Event Zone
        
        class EventZone : Zone
        {
            enum State
            {
                WAITING,
                RUNNING,
                EXPLODED,
                FINISHED
            }
            
            public const float RADIUS = 55f;

            public readonly HashSet<AttackDrone> Drones = new();
            private readonly Dictionary<BasePlayer, UiRef> uiRefs = new();

            public HackableLockedCrate Crate { get; private set; }

            public RadContainer Container { get; private set; }

            private BaseEntity smokeEnt;

            private State state;
            
            private float nextWaveStartTime;
            private int nextWaveIdx;
            private DroneWaveConfiguration currentWaveConfig;

            private int kamikazeDronesToSpawn;
            private int attackDronesToSpawn;

            private float endTime;

            private CustomMapMarker _marker;

            private void Awake()
            {
                endTime = Time.time + Config.IdleDespawnMinutes * 60;
                SpawnEntities();
                StartCoroutine(MainCoro());
            }

            public void OnCrateHacked()
            {
                UpdateState(State.RUNNING);
                _hackSecondsOverride.Add(Crate, Config.CrateHackSeconds);
            }
            

            public void OnCrateFullyHacked()
            {
                UpdateState(State.FINISHED);
            }

            public void OnContainerDestroyed()
            {
                UpdateState(State.EXPLODED);
            }
            
            public void OnDroneKilled(AttackDrone drone)
            {
                Drones.Remove(drone);
            }

            private void SpawnEntities()
            {
                Container = EntityTools.CreateCustomEntity<CargoShipContainer, RadContainer>("assets/content/props/shipping_containers/harbor_dynamic_container.prefab", _radtownMonument.transform.position,
                    Quaternion.LookRotation(_radtownMonument.transform.TransformDirection(Quaternion.Euler(0, 30, 0) * Vector3.forward)));
                Container.InitializeHealth(Config.ContainerHealth);
                Container.Spawn();

                var rug1 = EntityTools.CreateEntity<BaseCombatEntity>("assets/prefabs/deployable/rug/rug.deployed.prefab", new Vector3(0, 1.5f, -1.5f), new Vector3(0, -90, 90));
                rug1.skinID = 3342091882;
                rug1.SetParent(Container);
                rug1.Spawn();
                rug1.InitializeHealth(100_000, 100_000);
                
                var rug2 = EntityTools.CreateEntity<BaseCombatEntity>("assets/prefabs/deployable/rug/rug.deployed.prefab", new Vector3(0, 1.5f, 1.5f), new Vector3(0, 90, 90));
                rug2.skinID = 3342091882;
                rug2.SetParent(Container);
                rug2.Spawn();
                rug2.InitializeHealth(100_000, 100_000);

                Crate = EntityTools.CreateEntity<HackableLockedCrate>("assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab", Container.transform.TransformPoint(new Vector3(0, 0.1f, -0.5f)),
                    Container.transform.eulerAngles);
                Crate.Spawn();

                if (!LoottableApi.AssignPreset(Instance, Crate, "c_locked") && Config.LockedCrateLootTable.enabled)
                {
                    LootManager.FillWithLoot(Crate, Config.LockedCrateLootTable);
                }
                
                _marker = CustomMapMarker.Create(_radtownMonument.transform.position, 0.3f, new Color(1f, 0.83f, 0f), 2);
                _marker.SetText("Radtown Event");
                _marker.Paint(Color.black, DrawMarker, 0.009f);
            }

            private IEnumerator MainCoro()
            {
                while (endTime - Time.time > 0)
                {
                    if (state == State.RUNNING && Time.time > nextWaveStartTime)
                    {
                        // Can spawn drone
                        if (currentWaveConfig != null && Drones.Count < currentWaveConfig.MaxConcurrentDrones)
                        {
                            var r = UnityEngine.Random.Range(0, 2);
                            if (r == 1 && kamikazeDronesToSpawn > 0)
                            {
                                SpawnDrone(currentWaveConfig.KamikazeDroneConfiguration);
                                kamikazeDronesToSpawn--;
                            }
                            else if (r == 0 && attackDronesToSpawn > 0)
                            {
                                SpawnDrone(currentWaveConfig.AttackDroneConfiguration);
                                attackDronesToSpawn--;
                            }
                        }
                        
                        // No more drones to spawn, next wave
                        if (Drones.Count == 0 && kamikazeDronesToSpawn < 1 && attackDronesToSpawn < 1)
                        {
                            // No more waves
                            if (!StartWave())
                            {
                                LogDebug("No more waves to spawn");
                                UpdateState(State.FINISHED);
                            }
                        }
                    }
                    
                    CalculateRemaining(endTime - Time.time, out var minutes, out var seconds);
                    _marker.SetText(1, $"{minutes}m {seconds}s");
                    
                    UpdateHealthBar();
                    yield return CoroutineEx.waitForSeconds(1);
                }

                LogDebug("Destroy from MainCoro");
                Destroy();
            }

            private void OnStateChanged()
            {
                if (state == State.RUNNING)
                {
                    Interface.CallHook("OnRadtownEventStart");
                    endTime = Time.time + Config.CrateHackSeconds + Config.DespawnSecondsFinished;
                    BroadcastLang("container_hacked");
                }
                if (state == State.FINISHED)
                {
                    Interface.CallHook("OnRadtownEventEnd", true);
                    endTime = Time.time + Config.DespawnSecondsFinished;
                    
                    foreach (var drone in Drones.ToArray())
                    {
                        if (!drone.IsNullOrDestroyed())
                        {
                            drone.DieInstantly();
                        }
                    }
                    
                    BroadcastLang("container_defended", Config.DespawnSecondsFinished.ToString());
                    if (!Crate.IsFullyHacked())
                    {
                        // Force finish crate
                        Crate.hackSeconds = Single.PositiveInfinity;
                    }
                }
                if (state == State.EXPLODED)
                {
                    Interface.CallHook("OnRadtownEventEnd", false);
                    BroadcastLang("container_exploded");
                    StopAllCoroutines();
                    DestroyAllHealthBars();
                    
                    _marker?.Destroy();
                    _marker = null;

                    StartCoroutine(ExplodeCoro());
                }
            }

            private bool StartWave()
            {
                LogDebug($"Spawn wave {nextWaveIdx}");
                if (nextWaveIdx >= Config.Waves.Count)
                {
                    return false;
                }

                currentWaveConfig = Config.Waves[nextWaveIdx++];
                LogDebug($"Attack {currentWaveConfig.AttackDroneConfiguration.DroneCount} Kamikaze {currentWaveConfig.KamikazeDroneConfiguration.DroneCount}");
                nextWaveStartTime = Time.time + currentWaveConfig.PreparationTime;
                kamikazeDronesToSpawn = currentWaveConfig.KamikazeDroneConfiguration.DroneCount;
                attackDronesToSpawn = currentWaveConfig.AttackDroneConfiguration.DroneCount;
                return true;
            }

            private void SpawnDrone(IDroneConfig config)
            {
                var pos = transform.position + Quaternion.Euler(0, UnityEngine.Random.Range(0, 360), 0) * Vector3.forward * (RADIUS * UnityEngine.Random.Range(0.9f, 1.2f));
                pos.y = TerrainMeta.HeightMap.GetHeight(pos) + UnityEngine.Random.Range(18, 30);

                var drone = EntityTools.CreateCustomEntity<Drone, AttackDrone>("assets/prefabs/deployable/drone/drone.deployed.prefab", pos);
                drone.Init(Container, config);
                drone.Spawn();
                Drones.Add(drone);
            }

            private IEnumerator ExplodeCoro()
            {
                var playerList = Pool.Get<List<BasePlayer>>();
                smokeEnt = EntityTools.CreateEntity<BaseEntity>("assets/prefabs/visualization/sphere.prefab", transform.position);
                smokeEnt.Spawn();
                
                EntityTools.Kill(Crate);
                EntityTools.Kill(Container);
                EntityTools.KillSafe(Drones);
                
                Effect.server.Run("assets/prefabs/npc/m2bradley/effects/bradley_explosion.prefab", transform.position);

                yield return CoroutineEx.waitForSeconds(0.5f);

                // TODO different smoke effect
                var r = UnityEngine.Random.insideUnitSphere;
                foreach (var x in new[] {-10, -5, 0, 5, 10})
                {
                    foreach (var z in new[] {-10, -5, 0, 5, 10})
                    {
                        LogDebug($"smoke {x} {z}");
                        Effect.server.Run("assets/bundled/prefabs/fx/smoke_cover_full.prefab", smokeEnt, 0, new Vector3(x, 0, z) + r);
                    }
                    yield return CoroutineEx.waitForSeconds(0.1f);
                }
                
                playerList.AddRange(Players);
                foreach (var player in playerList)
                {
                    player.ApplyRadiation(100);
                }
                playerList.Clear();
                
                for (int i = 0; i < 40; i++)
                {
                    Effect.server.Run("assets/content/effects/explosions/explosion large.prefab", transform.position + Vector3.up * (i + 1));
                    yield return CoroutineEx.waitForSeconds(0.1f);
                }

                for (int i = 0; i < 60; i++)
                {
                    playerList.AddRange(Players);
                    foreach (var player in playerList)
                    {
                        player.ApplyRadiation(100);
                    }
                    playerList.Clear();
                    yield return CoroutineEx.waitForSeconds(1);
                }
                
                LogDebug("Destroy from ExplodeCoro");
                Destroy();
            }
            
            #region GUI

            private void UpdateHealthBar()
            {
                var list = Pool.Get<List<BasePlayer>>();
                list.AddRange(Players);
                foreach (var player in list)
                {
                    DrawHealthBar(player);
                }
                Pool.FreeUnmanaged(ref list);
            }
            
            private readonly UiColor barBg = new Color(0.48f, 0.39f, 0);
            private readonly UiColor barFg = new(1f, 0.83f, 0);
            private readonly LabelStyle _healthLabelStyle = new()
            {
                Font = UiFont.robotoBold,
                FontSize = 12,
                TextAnchor = TextAnchor.MiddleCenter,
                Outline = UiColor.Black,
                TextColor = UiColor.White
            };
            private readonly LabelStyle _healthTextStyle = new()
            {
                Font = UiFont.robotoBold,
                FontSize = 20,
                TextAnchor = TextAnchor.MiddleCenter,
                Outline = UiColor.Black,
                TextColor = UiColor.White
            };
            
            private readonly StringBuilder textBuilder = new();

            private void DrawHealthBar(BasePlayer player)
            {
                if (Instance == null)
                {
                    return;
                }
                
                textBuilder.Clear();
                
                if (state == State.WAITING)
                {
                    CalculateRemaining(endTime - Time.time, out var minutes, out var seconds);
                    textBuilder.Append(Instance.lang.GetMessage("zone_text_start", player));
                    textBuilder.Append('\n');
                    textBuilder.Append(Instance.lang.GetMessage("time_remaining", player, minutes, seconds));
                }
                else if (state == State.RUNNING)
                {
                    CalculateRemaining(Config.CrateHackSeconds - Crate.hackSeconds, out var minutes, out var seconds);
                    textBuilder.Append(Instance.lang.GetMessage("time_remaining", player, minutes, seconds));
                    textBuilder.Append('\n');
                    var timeToNextWave = nextWaveStartTime - Time.time;
                    if (timeToNextWave > 0)
                    {
                        CalculateRemaining(timeToNextWave, out var minutesToNextWave, out var secondsToNextWave);
                        textBuilder.Append(Instance.lang.GetMessage("next_wave_2", player, minutesToNextWave, secondsToNextWave));
                    }
                    else
                    {
                        textBuilder.Append(Instance.lang.GetMessage("waves_remaining", player, currentWaveConfig.WaveNumber, Config.Waves.Count));
                        textBuilder.Append('\n');
                        textBuilder.Append(Instance.lang.GetMessage("drones_remaining_2", player, Drones.Count));
                    }
                }
                else if (state == State.FINISHED)
                {
                    CalculateRemaining(endTime - Time.time, out var minutes, out var seconds);
                    textBuilder.Append(Instance.lang.GetMessage("time_remaining", player, minutes, seconds));
                }
                else
                {
                    LogError($"Calling UpdateUi in state {state}");
                    return;
                }

                // if (DEBUG)
                // {
                //     textBuilder.Append($" [ds {endTime - Time.time}s]");
                // }

                using var cui = Cui.Create(Instance, (0.3f, 0.7f, 0.82f, 0.96f), UiColor.Transparent, UiParentLayer.OverlayNonScaled);

                cui.AddLabel((0.1f, 0.9f, 0.25f, 1), textBuilder.ToString(), _healthTextStyle);

                if (state != State.FINISHED)
                {
                    using (cui.CreateContainer((0, 1, 0, 0.25f), barBg))
                    {
                        cui.AddBox(Anchor.Relative(0, Mathf.Clamp01(Container.Health() / Container.MaxHealth()), 0, 1, 3, -3, 3, -3), barFg);
                        cui.AddLabel(Anchor.Fill, $"{Container.Health():N0}/{Container.MaxHealth():N0}", _healthLabelStyle);
                    }
                }

                uiRefs.TryGetValue(player, out var uiRef);
                cui.Send(player, ref uiRef);
                uiRefs[player] = uiRef;
            }
            
            private static void CalculateRemaining(float secondsRemaining, out int minutes, out int seconds)
            {
                minutes = Mathf.FloorToInt(secondsRemaining / 60);
                seconds = Mathf.FloorToInt(secondsRemaining % 60);
            }

            private void DestroyHealthBar(BasePlayer player)
            {
                if (uiRefs.Remove(player, out var uiRef))
                {
                    Cui.Destroy(player, uiRef);
                }
            }
            
            private void DestroyAllHealthBars()
            {
                var list = Pool.Get<List<BasePlayer>>();
                list.AddRange(uiRefs.Keys);
                foreach (var player in list)
                {
                    DestroyHealthBar(player);
                }
                Pool.FreeUnmanaged(ref list);
            }

            protected override void PlayerLeave(BasePlayer player)
            {
                base.PlayerLeave(player);

                DestroyHealthBar(player);
            }

            #endregion

            private void UpdateState(State newState)
            {
                if (state == newState)
                {
                    return;
                }

                state = newState;
                OnStateChanged();
            }

            protected override void OnDestroy()
            {
                StopAllCoroutines();

                EntityTools.KillSafe(Drones);
                EntityTools.Kill(Container);
                EntityTools.Kill(Crate);
                EntityTools.Kill(smokeEnt);

                DestroyAllHealthBars();
                
                _marker?.Destroy();
                
                base.OnDestroy();
                
                Instance?.OnEventZoneDestroyed();
            }
            
            private static IEnumerable<Vector2> DrawMarker()
            {
                const int dot_size = 2;
                
                for (int r = 40; r > 14; r -= dot_size)
                {
                    var length = r * 1.1f;
                    var dotCount = Mathf.RoundToInt(length / dot_size);
                    var angleOffset = 60f / dotCount;
                    for (int c = 0; c < dotCount; c++)
                    {
                        for (int i = 1; i <= 3; i++)
                        {
                            var angleRad = ((i * 120 + angleOffset * c) % 360) * Mathf.Deg2Rad;
                            yield return new Vector2(r * Mathf.Cos(angleRad), r * Mathf.Sin(angleRad));
                        }
                    }
                }
            }
        }
        
        #endregion
        
        #region Mono

        class RadContainer : CargoShipContainer
        {
            public bool Destroyed { get; private set; }

            private float health;
            private float maxHealth;

            public override void PreInitShared()
            {
                base.PreInitShared();
                this.SetField<CargoShipContainer>("dressingVariant", 0);
            }

            public override void ServerInit()
            {
                base.ServerInit();
                gameObject.layer = (int)Layer.Vehicle_World;
            }

            public void InitializeHealth(float maxHealth)
            {
                this.maxHealth = maxHealth;
                health = maxHealth;
            }

            public override float Health()
            {
                return health;
            }
            
            public override float MaxHealth()
            {
                return maxHealth;
            }

            public override void OnAttacked(HitInfo info)
            {
                if (info == null || info.InitiatorPlayer != null) 
                {
                    return;
                }
                
                base.OnAttacked(info);
            
                info.damageTypes.Scale(DamageType.Arrow, ConVar.Server.arrowdamage);
                info.damageTypes.Scale(DamageType.Bullet, ConVar.Server.bulletdamage);
                info.damageTypes.Scale(DamageType.Slash, ConVar.Server.meleedamage);
                info.damageTypes.Scale(DamageType.Blunt, ConVar.Server.meleedamage);
                info.damageTypes.Scale(DamageType.Stab, ConVar.Server.meleedamage);
                info.damageTypes.Scale(DamageType.Bleeding, ConVar.Server.bleedingdamage);
            
                health -= info.damageTypes.Total();
            
                if (health <= 0)
                {
                    health = 0;
                    OnDestroyed();
                }
            }

            public void OnDestroyed()
            {
                Destroyed = true;
                Instance?.OnRadContainerDestroyed(this);
            }
        }

        class AttackDrone : Drone
        {
            public override bool CanAcceptInput => false;
            
            private const float THINK_TIME = 3f;

            private const float EXPLOSIVE_DELAY = 5f;
            private float lastExplosiveTime;

            private Rigidbody rigidbody;

            private bool isExploding;
            private RFTimedExplosive c4;

            public bool IsKamikaze { get; set; }

            private float damage;

            private bool hasTarget;
            
            private Vector3 target;
            private float lastTargetTime = -1;

            private Vector3 lastPosition;
            private int lastPositionUpdate;

            public override void ServerInit()
            {
                base.ServerInit();

                InvokeRandomized(Think, 2f, THINK_TIME, 2f);

                targetPosition = transform.position;
                
                movementAcceleration = altitudeAcceleration = IsKamikaze ? 8f : 5f;
                
                rigidbody = GetComponent<Rigidbody>();

                if (IsKamikaze)
                {
                    altitudeAcceleration = 20f;
                    SetupC4();
                }
            }

            public void Init(BaseEntity target, IDroneConfig config)
            {
                this.target = target.transform.position;
                damage = config.Damage;
                IsKamikaze = config.IsKamikaze;
                InitializeHealth(config.Health, config.Health);
            }

            void Think()
            {
                if (Vector3.SqrMagnitude(transform.position - lastPosition) < 1)
                {
                    lastPositionUpdate++;
                }
                else
                {
                    lastPositionUpdate = 0;
                }

                if (lastPositionUpdate > 4)
                {
                    LogDebug("Kill because stuck");
                    DieInstantly();
                    return;
                }
                
                if (!hasTarget)
                {
                    targetPosition = GetTargetHoverPosition();
                    hasTarget = true;
                    return;
                }
                
                var atTarget = targetPosition.HasValue && (targetPosition.Value - transform.position).sqrMagnitude < 1;
                if (IsKamikaze)
                {
                    if (atTarget)
                    {
                        LogDebug("set boom target");
                        movementSpeedOverride = altitudeSpeedOverride = 30;
                        targetPosition = target;
                    }
                }
                else
                {
                    if (atTarget)
                    {
                        if (lastTargetTime < 0)
                        {
                            lastTargetTime = Time.time;
                        }
                        else if (Time.time - lastTargetTime > 8)
                        {
                            targetPosition = GetTargetHoverPosition();
                            lastTargetTime = -1;
                        }
                        if ((Time.time - lastExplosiveTime) > EXPLOSIVE_DELAY)
                        {
                            DropExplosive();
                        }
                    }
                }

                lastPosition = transform.position;

                // if (DEBUG)
                // {
                //     DebugPlayer?.DrawArrow(transform.position, transform.position + rayDirection * rayLength, SPHERECAST_RADIUS, Color.green, THINK_TIME);
                //     DebugPlayer?.DrawSphere(targetPosition ?? default, SPHERECAST_RADIUS, Color.green, THINK_TIME);
                // }
            }

            private Vector3 GetTargetHoverPosition()
            {
                var rand = UnityEngine.Random.insideUnitCircle * 4;
                var pos = target + new Vector3(rand.x, 0, rand.y);
                pos.y = TerrainMeta.HeightMap.GetHeight(pos) + UnityEngine.Random.Range(8f, 20f);
                return pos;
            }

            public void DropExplosive()
            {
                var velocity = (target - transform.position).normalized * 2f;

                var explosive = (TimedExplosive)GameManager.server.CreateEntity("assets/prefabs/ammo/rocket/rocket_basic.prefab", transform.position - Vector3.up * 0.5f, Quaternion.LookRotation(Vector3.down));
                var projectile = explosive.GetComponent<ServerProjectile>();
                if (projectile != null)
                {
                    projectile.InitializeVelocity(velocity * 9.81f);
                }

                explosive.SetDamageScale(damage / 350f);
                explosive.SetCreatorEntity(this);
                explosive.Spawn();

                if (rigidbody != null)
                {
                    rigidbody.AddForce(-velocity * 3f, ForceMode.Impulse);
                }

                lastExplosiveTime = Time.time;
            }

            public override void OnCollision(Collision collision, BaseEntity hitEntity)
            {
                if (IsKamikaze)
                {
                    Explode();
                }
                base.OnCollision(collision, hitEntity);
            }

            public override void OnKilled(HitInfo info)
            {
                base.OnKilled(info);
                Effect.server.Run("assets/prefabs/weapons/beancan grenade/effects/beancan_grenade_explosion.prefab", transform.position, transform.up);


                if (info?.InitiatorPlayer != null)
                {
                    DropLootContainer(transform.position, info.InitiatorPlayer.userID);
                }
                
                if (!isExploding && !c4.IsNullOrDestroyed())
                {
                    c4.ForceExplode();
                }

                Instance?.OnAttackDroneKilled(this);
            }

            private void SetupC4()
            {
                c4 = EntityTools.CreateEntity<RFTimedExplosive>("assets/prefabs/tools/c4/explosive.timed.deployed.prefab", Vector3.up * 0.1f, Quaternion.Euler(-90, 90, 0));
                c4.SetMotionEnabled(false);
                c4.SetCollisionEnabled(false);
                c4.SetParent(this);
                c4.Spawn();

                SetSlot(Slot.UpperModifier, c4);

                c4.CancelInvoke(c4.Explode);
                c4.DoStick(transform.position + Vector3.up * 0.1f, Vector3.up, this, null);

                c4.SetDamageScale(damage / 500f);
                c4.SendNetworkUpdate();
            }

            public void Explode()
            {
                isExploding = true;
                if (!c4.IsNullOrDestroyed())
                {
                    c4.ForceExplode();
                }

                Invoke(Kill, 0);
            }

            public void Kill()
            {
                Kill(DestroyMode.Gib);
            }
        }

        /*class CH47ContainerHelicopter : CH47HelicopterAIController
        {
            public int ContainerVariant { get; set; }

            private CargoShipContainer _container;

            private bool isAtLandingTarget;
            private float timeAtTarget;

            public override void ServerInit()
            {
                base.ServerInit();
                SpawnContainer();
                InvokeRepeating(CheckIsAtTarget, 5, 5);
            }

            private void CheckIsAtTarget()
            {
                LogDebug($"check is at target {(transform.position - landingTarget).magnitude}");
                if (!isAtLandingTarget && (transform.position - landingTarget).sqrMagnitude < 1)
                {
                    LogDebug("Is at target");
                    isAtLandingTarget = true;
                    timeAtTarget = Time.time;
                }
                else if (isAtLandingTarget)
                {
                    var landedTime = Time.time - timeAtTarget;
                    if (landedTime > 10)
                    {
                        LogDebug("Clear target");
                        ClearLandingTarget();
                        CancelInvoke(CheckIsAtTarget);
                    }
                    else if (landedTime > 5)
                    {
                        LogDebug("Release container");
                        ReleaseContainer();
                    }
                }
            }

            private void SpawnContainer()
            {
                _container = EntityTools.CreateEntity<CargoShipContainer>("assets/content/props/shipping_containers/harbor_dynamic_container.prefab", new Vector3(0, -2.5f, 0), new Vector3(0, 90, 0));
                Instance?._pendingContainers.Add(_container);
                _container.SetField("dressingVariant", ContainerVariant);
                _container.SetParent(this);
                _container.Spawn();
            }

            private void ReleaseContainer()
            {
                if (!_container.IsNullOrDestroyed())
                {
                    _container.SetParent(null, true);
                }
            }
        }*/
        
        #endregion
        
        #region Commands
        
        [UniversalCommand("radtown", Permission = PERM_ADMIN)]
        void CmdRadtown(IPlayer iPlayer, string command, string[] args)
        {
            var cmd = args.GetString(0);
            if (cmd == "start")
            {
                if (!_eventController.IsEventRunning)
                {
                    _eventController.StartEvent();
                }
                else
                {
                    iPlayer.Reply("Event is already running");
                }
            }
            else if (cmd == "stop")
            {
                if (_eventController.IsEventRunning)
                {
                    _eventController.EndEvent();
                    iPlayer.Reply("Event stopped");
                }
                else
                {
                    iPlayer.Reply("There is no event running at the moment");
                }
            }
            else
            {
                iPlayer.Reply("Invalid args! Usage: radtown start|stop");
            }
        }
        
        #endregion
        
        #region Misc

        static class HackableLockedCrate_HackProgress
        {
            public static bool Prefix(HackableLockedCrate __instance)
            {
                if (!_hackSecondsOverride.TryGetValue(__instance, out var requiredHackSeconds))
                {
                    return true;
                }
                
                ++__instance.hackSeconds;
                if (__instance.hackSeconds > requiredHackSeconds)
                {
                    Interface.CallHook("OnCrateHackEnd", __instance);
                    Facepunch.Rust.Analytics.Azure.OnLockedCrateFinished(__instance.originalHackerPlayerId, __instance);
                    if (__instance.originalHackerPlayer != null && __instance.originalHackerPlayer.serverClan != null)
                    {
                        __instance.originalHackerPlayer.AddClanScore(ClanScoreEventType.HackedCrate);
                    }
                    __instance.RefreshDecay();
                    __instance.SetFlag(BaseEntity.Flags.Reserved2, true);
                    __instance.isLootable = true;
                    __instance.CancelInvoke(__instance.HackProgress);
                    _hackSecondsOverride.Remove(__instance);
                }
                __instance.ClientRPC(RpcTarget.NetworkGroup("UpdateHackProgress"), (int)__instance.hackSeconds, requiredHackSeconds);
                return false;
            }
        }

        private static void DropLootContainer(Vector3 position, ulong ownerId)
        {
            var drop = EntityTools.CreateEntity<DroppedItemContainer>("assets/prefabs/misc/item drop/item_drop_buoyant.prefab", position - Vector3.up * 0.420f);
            
            drop.OwnerID = ownerId;
            if (drop.inventory == null)
            {
                drop.inventory = new ItemContainer();
                drop.inventory.ServerInitialize(null, 12);
                drop.inventory.GiveUID();
                drop.inventory.entityOwner = drop;
            }
            else
            {
                drop.inventory.Clear();
            }

            drop.Spawn();

            if (!LoottableApi.AssignPreset(Instance, drop.inventory, "drone"))
            {
                LootManager.FillWithLoot(drop.inventory, Config.DroneLootTable);
            }
        }
        
        private static void BroadcastLang(string langKey, params string[] args)
        {
            if (Config.UseNotify)
            {
                Instance?.lang.NotifyBroadcast(Config.NotifyType, langKey, args);
            }
            else
            {
                Instance?.lang.BroadcastMessage(langKey);
            }
        }
        
        #endregion

        #region Configuration

        public class Configuration
        {
            [JsonProperty("Custom chat prefix")]
            public string OverrideChatPrefix { get; set; }

            [JsonProperty("Use Notify instead of chat messages")]
            public bool UseNotify { get; set; } = false;

            [JsonProperty("Notify notification type")]
            public int NotifyType { get; set; } = 0;
            
            [JsonProperty("Time between events (minutes)")]
            public int EventDelayMinutes { get; set; } = 60;
            
            [JsonProperty("Crate hack time (seconds; drone waves will spawn until the crate is fully hacked)")]
            public int CrateHackSeconds { get; set; } = 600;

            [JsonProperty("Container health")]
            public int ContainerHealth { get; set; } = 8_000;

            [JsonProperty("Time before despawn after event has been completed (seconds)")]
            public int DespawnSecondsFinished { get; set; } = 300;

            [JsonProperty("Time before despawn if event is not started (minutes)")]
            public int IdleDespawnMinutes { get; set; } = 30;

            [JsonProperty("Make event zone visible")]
            public bool EventZoneVisible { get; set; } = false;
            
            [JsonProperty("Drone wave configuration", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<DroneWaveConfiguration> Waves { get; set; } = new()
            {
                new DroneWaveConfiguration()
                {
                    WaveNumber = 1,
                    PreparationTime = 5,
                    MaxConcurrentDrones = 3,
                    AttackDroneConfiguration = new AttackDroneConfiguration()
                    {
                        DroneCount = 20,
                        Damage = 40,
                        Health = 100,
                        AttackPlayers = false
                    },
                    KamikazeDroneConfiguration = new KamikazeDroneConfiguration()
                    {
                        DroneCount = 0,
                        Damage = 60,
                        Health = 100,
                        AttackPlayers = false
                    }
                },
                new DroneWaveConfiguration()
                {
                    WaveNumber = 2,
                    PreparationTime = 20,
                    MaxConcurrentDrones = 4,
                    AttackDroneConfiguration = new AttackDroneConfiguration()
                    {
                        DroneCount = 20,
                        Damage = 50,
                        Health = 150,
                        AttackPlayers = false
                    },
                    KamikazeDroneConfiguration = new KamikazeDroneConfiguration()
                    {
                        DroneCount = 30,
                        Damage = 60,
                        Health = 150,
                        AttackPlayers = false
                    }
                },
                new DroneWaveConfiguration()
                {
                    WaveNumber = 3,
                    PreparationTime = 20,
                    MaxConcurrentDrones = 5,
                    AttackDroneConfiguration = new AttackDroneConfiguration()
                    {
                        DroneCount = 40,
                        Damage = 40,
                        Health = 200,
                        AttackPlayers = true
                    },
                    KamikazeDroneConfiguration = new KamikazeDroneConfiguration()
                    {
                        DroneCount = 40,
                        Damage = 60,
                        Health = 200,
                        AttackPlayers = false
                    }
                },
                new DroneWaveConfiguration()
                {
                    WaveNumber = 4,
                    PreparationTime = 20,
                    MaxConcurrentDrones = 8,
                    AttackDroneConfiguration = new AttackDroneConfiguration()
                    {
                        DroneCount = 40,
                        Damage = 70,
                        Health = 250,
                        AttackPlayers = true
                    },
                    KamikazeDroneConfiguration = new KamikazeDroneConfiguration()
                    {
                        DroneCount = 40,
                        Damage = 80,
                        Health = 250,
                        AttackPlayers = true
                    }
                },
                new DroneWaveConfiguration()
                {
                    WaveNumber = 5,
                    PreparationTime = 20,
                    MaxConcurrentDrones = 8,
                    AttackDroneConfiguration = new AttackDroneConfiguration()
                    {
                        DroneCount = 40,
                        Damage = 70,
                        Health = 250,
                        AttackPlayers = true
                    },
                    KamikazeDroneConfiguration = new KamikazeDroneConfiguration()
                    {
                        DroneCount = 40,
                        Damage = 80,
                        Health = 250,
                        AttackPlayers = true
                    }
                }
            };

            [JsonProperty("Locked crate loot table")]
            public LootManager.LootTable LockedCrateLootTable { get; set; } = new()
            {
                enabled = false,
                minItems = 4,
                maxItems = 8,
                items = new List<LootManager.LootItem>
                {
                    new("scrap", 12, 48, 1)
                }
            };

            [JsonProperty("Drone loot table")]
            public LootManager.LootTable DroneLootTable { get; set; } = new()
            {
                enabled = true,
                minItems = 1,
                maxItems = 4,
                items = new List<LootManager.LootItem>
                {
                    new("gunpowder", 10, 30, 0.2f),
                    new("sulfur", 10, 30, 0.4f),
                    new("charcoal", 20, 60, 0.7f),
                    new("metal.fragments", 10, 60, 0.5f),
                    new("explosives", 1, 2, 0.08f),
                }
            };
        }

        public class DroneWaveConfiguration
        {
            [JsonProperty("Wave number")]
            public int WaveNumber { get; set; }
            [JsonProperty("Preparation time before drones spawn (seconds)")]
            public int PreparationTime { get; set; }
            [JsonProperty("Maximum number of concurrent drones")]
            public int MaxConcurrentDrones { get; set; }
            [JsonProperty("Explosive drone configuration")]
            public AttackDroneConfiguration AttackDroneConfiguration { get; set; }
            [JsonProperty("Kamikaze drone configuration")]
            public KamikazeDroneConfiguration KamikazeDroneConfiguration { get; set; }
        }

        public class AttackDroneConfiguration : IDroneConfig
        {
            [JsonIgnore]
            public bool IsKamikaze => false;
            [JsonProperty("Number of attack drones to spawn (0 to disable)")]
            public int DroneCount { get; set; }
            [JsonProperty("Drone health")]
            public int Health { get; set; }
            [JsonProperty("Damage per shell")]
            public int Damage { get; set; }
            [JsonProperty("Target players")]
            public bool AttackPlayers { get; set; }
        }

        public class KamikazeDroneConfiguration : IDroneConfig
        {
            [JsonIgnore]
            public bool IsKamikaze => true;
            [JsonProperty("Number of kamikaze drones to spawn (0 to disable)")]
            public int DroneCount { get; set; }
            [JsonProperty("Drone health")]
            public int Health { get; set; }
            [JsonProperty("Damage on explosion")]
            public int Damage { get; set; }
            [JsonProperty("Target players")]
            public bool AttackPlayers { get; set; }
        }

        private interface IDroneConfig
        {
            bool IsKamikaze { get; }
            int DroneCount { get; }
            int Health { get; }
            int Damage { get; }
            bool AttackPlayers { get; }
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["time_remaining"] = "Time remaining: {0}m {1}s",
                ["drones_remaining_2"] = "Drones remaining: {0}",
                ["next_wave_2"] = "Next wave in: {0}m {1}s",
                ["waves_remaining"] = "Wave {0}/{1}",
                ["zone_text_start"] = "Start hacking the crate to start the event",
                ["container_defended"] = "The <color=#FFD300>Nuclear Waste Container</color> has been successfully defended. It will be removed in {0}s",
                ["container_delivered"] = "A <color=#FFD300>Nuclear Waste Container</color> has been spotted at <color=red>{0}</color>. It may contain valuable loot.",
                ["container_hacked"] = "Attack drones have been spotted near the <color=#FFD300>Nuclear Waste Container</color>. Defend the container to avoid a catastrophic explosion.",
                ["container_exploded"] = "The <color=#FFD300>Nuclear Waste Container</color> has been severely damaged by drones and <color=red>exploded</color>."
            }, this, "en");
        }

        #endregion
    }
}
namespace PluginComponents.RadtownEvent{using JetBrains.Annotations;using Oxide.Plugins;using System;[AttributeUsage(AttributeTargets.Field,AllowMultiple=false),MeansImplicitUse]public sealed class PermAttribute:Attribute{}[AttributeUsage(AttributeTargets.Method,AllowMultiple=false),MeansImplicitUse]public sealed class UniversalCommandAttribute:Attribute{public UniversalCommandAttribute(string name){Name=name;}public string Name{get;set;}public string Permission{get;set;}}[AttributeUsage(AttributeTargets.Method),MeansImplicitUse]public sealed class HookAttribute:Attribute{}[AttributeUsage(AttributeTargets.Method,Inherited=false)]public sealed class DebugAttribute:Attribute{}public class MinMaxInt{public int min;public int max;public MinMaxInt(){}public MinMaxInt(int value):this(value,value){}public MinMaxInt(int min,int max){this.min=min;this.max=max;}public int Random(){return UnityEngine.Random.Range(min,max+1);}}}namespace PluginComponents.RadtownEvent.Core{using Oxide.Core.Plugins;using Oxide.Core;using Oxide.Plugins;using Newtonsoft.Json;using System.IO;using UnityEngine;using System;using System.Diagnostics;using System.Collections.Generic;using System.Linq;using Facepunch.Extend;using System.Reflection;using PluginComponents.RadtownEvent;public abstract class BasePlugin<TPlugin,TConfig>:BasePlugin<TPlugin>where TConfig:class,new()where TPlugin:RustPlugin{protected new static TConfig Config{get;private set;}private string ConfigPath=>Path.Combine(Interface.Oxide.ConfigDirectory,$"{Name}.json");protected override void LoadConfig()=>ReadConfig();protected override void SaveConfig()=>WriteConfig();protected override void LoadDefaultConfig()=>Config=new TConfig();private void ReadConfig(){if(File.Exists(ConfigPath)){Config=JsonConvert.DeserializeObject<TConfig>(File.ReadAllText(ConfigPath));if(Config==null){LogError("[CONFIG] Your configuration file contains an error. Using default configuration values.");LoadDefaultConfig();}}else{LoadDefaultConfig();}WriteConfig();}private void WriteConfig(){var directoryName=Utility.GetDirectoryName(ConfigPath);if(directoryName!=null&&!Directory.Exists(directoryName)){Directory.CreateDirectory(directoryName);}if(Config!=null){string text=JsonConvert.SerializeObject(Config,Formatting.Indented);File.WriteAllText(ConfigPath,text);}else{LogError("[CONFIG] Saving failed - config is null");}}}public abstract class BasePlugin<TPlugin>:BasePlugin where TPlugin:RustPlugin{public new static TPlugin Instance{get;private set;}protected static string DataFolder=>Path.Combine(Interface.Oxide.DataDirectory,typeof(TPlugin).Name);protected override void Init(){base.Init();Instance=this as TPlugin;}protected override void Unload(){Instance=null;base.Unload();}}public abstract class BasePlugin:RustPlugin{public const int OSI_DELAY=5;public const bool CARBONARA=
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
protected virtual void OnServerInitialized(){}protected virtual void OnServerInitializedDelayed(){}public static void Log(string s){if(Instance!=null){Interface.Oxide.LogInfo($"[{Instance.Title}] {s}");}}[Conditional("DEBUG")]public static void LogDebug(string s){if(DEBUG&&Instance!=null){if(CARBONARA){LogWarning("[DEBUG] "+s);}else{Interface.Oxide.LogDebug($"[{Instance.Title}] {s}");}}}public static void LogWarning(string s){if(Instance!=null){Interface.Oxide.LogWarning($"[{Instance.Title}] {s}");}}public static void LogError(string s){if(Instance!=null){Interface.Oxide.LogError($"[{Instance.Title}] {s}");}}private Dictionary<string,CommandCallback>uiCallbacks;private string uiCommandBase;private void PrepareCommandHandler(){if(uiCallbacks==null){uiCallbacks=new();uiCommandBase=$"{Title.ToLower()}.uicmd";cmd.AddConsoleCommand(uiCommandBase,this,HandleCommand);}}private bool HandleCommand(ConsoleSystem.Arg arg){var cmd=arg.GetString(0);if(uiCallbacks.TryGetValue(cmd,out var callback)){var player=arg.Player();try{callback.ButtonCallback?.Invoke(player);callback.InputCallback?.Invoke(player,string.Join(' ',arg.Args?.Skip(1)??Enumerable.Empty<string>()));}catch(Exception ex){PrintError($"Failed to run UI command {cmd}: {ex}");}}return false;}public string CreateUiCommand(string guid,Action<BasePlayer>callback,bool singleUse){PrepareCommandHandler();uiCallbacks.Add(guid,new CommandCallback(callback,singleUse));return$"{uiCommandBase} {guid}";}public string CreateUiCommand(string guid,Action<BasePlayer,string>callback,bool singleUse){PrepareCommandHandler();uiCallbacks.Add(guid,new CommandCallback(callback,singleUse));return$"{uiCommandBase} {guid}";}private readonly struct CommandCallback{public readonly bool SingleUse;public readonly Action<BasePlayer>ButtonCallback;public readonly Action<BasePlayer,string>InputCallback;public CommandCallback(Action<BasePlayer>buttonCallback,bool singleUse){ButtonCallback=buttonCallback;InputCallback=null;SingleUse=singleUse;}public CommandCallback(Action<BasePlayer,string>inputCallback,bool singleUse){ButtonCallback=null;InputCallback=inputCallback;SingleUse=singleUse;}}public void ChatMessage(BasePlayer player,string message){if(player){player.SendConsoleCommand("chat.add",2,0,$"{ChatPrefix} {message}");}}}}namespace PluginComponents.RadtownEvent.Cui{using System;using UnityEngine;using PluginComponents.RadtownEvent;using Facepunch;using Oxide.Game.Rust.Cui;using PluginComponents.RadtownEvent.Core;using PluginComponents.RadtownEvent.Cui.Style;using System.Collections.Generic;using UnityEngine.UI;public readonly record struct Anchor{public float XMin{get;init;}public float XMax{get;init;}public float YMin{get;init;}public float YMax{get;init;}public float OffsetXMin{get;init;}public float OffsetXMax{get;init;}public float OffsetYMin{get;init;}public float OffsetYMax{get;init;}internal readonly string Min()=>$"{XMin:N3} {YMin:N3}";internal readonly string Max()=>$"{XMax:N3} {YMax:N3}";internal readonly string OffsetMin()=>$"{OffsetXMin:N3} {OffsetYMin:N3}";internal readonly string OffsetMax()=>$"{OffsetXMax:N3} {OffsetYMax:N3}";public Anchor WithOffsetY(float offset){return this with{YMax=YMax+offset,YMin=YMin+offset};}public Anchor WithOffset(float xMin=0,float xMax=0,float yMin=0,float yMax=0){return this with{XMin=XMin+xMin,XMax=XMax+xMax,YMin=YMin+yMin,YMax=YMax+yMax,};}public override string ToString(){return$"(X: {XMin}-{XMax}, Y: {YMin}-{YMax})";}public static readonly Anchor Fill=Relative(0,1,0,1);public static Anchor Padding(float horizontal,float vertical){return Relative(horizontal,1-horizontal,vertical,1-vertical);}public static Anchor Relative(float xMin,float xMax,float yMin,float yMax,float offsetXMin=0,float offsetXMax=0,float offsetYMin=0,float offsetYMax=0){return new Anchor{XMin=xMin,XMax=xMax,YMin=yMin,YMax=yMax,OffsetXMin=offsetXMin,OffsetXMax=offsetXMax,OffsetYMin=offsetYMin,OffsetYMax=offsetYMax};}public static Anchor Absolute(float centerX,float centerY,float xMin,float xMax,float yMin,float yMax){return new Anchor{XMin=centerX,XMax=centerX,YMin=centerY,YMax=centerY,OffsetXMin=xMin,OffsetXMax=xMax,OffsetYMin=yMin,OffsetYMax=yMax};}public static Anchor AbsoluteCentered(float centerX,float centerY,float height,float width){return new Anchor{XMin=centerX,XMax=centerX,YMin=centerY,YMax=centerY,OffsetXMin=-width/2f,OffsetXMax=width/2f,OffsetYMin=-height/2f,OffsetYMax=height/2f};}public static Anchor MoveRow(ref MovingAnchorProperties p,int max=-1){var anchor=p.Anchor();p.StepColumn(max);return anchor;}public static Anchor MoveColumn(ref MovingAnchorProperties p,int max=-1){var anchor=p.Anchor();p.StepRow(max);return anchor;}public static Anchor MoveOffsetY(ref MovingAnchorProperties p,float offset,bool breakRow=false){var anchor=p.Anchor();p.offsetY+=offset;if(breakRow){p.NextRow(true);}return anchor with{YMin=anchor.YMin+offset};}public static Anchor MoveX(ref MovingAnchorProperties p,float offsetX,bool resetOnNewRow){var anchor=p.Anchor();anchor=anchor with{XMax=anchor.XMin+offsetX};p.offsetX+=offsetX+p.SpaceX;p.resetOffset=resetOnNewRow;return anchor;}public static implicit operator Anchor(in ValueTuple<float,float,float,float>tuple){return Relative(tuple.Item1,tuple.Item2,tuple.Item3,tuple.Item4);}public static Anchor FillOffset(float xMin,float xMax,float yMin,float yMax){return new Anchor{XMin=0,YMin=0,XMax=1,YMax=1,OffsetXMin=xMin,OffsetXMax=xMax,OffsetYMin=yMin,OffsetYMax=yMax,};}public static float CenteredDistribution(int i,int count,float d=1,float pivot=0){var even=count%2==0;var s=i%2==0?-1:1;var x=Mathf.FloorToInt((i+(even?2:1))/2)*s*d-(even?s*d*0.5f:0);return pivot+x;}}public struct MovingAnchorProperties{public int row;public int column;public float offsetX;public float offsetY;public bool resetOffset;public float StartX{get;init;}public float StartY{get;init;}public float Width{get;init;}public float Height{get;init;}public float SpaceX{get;init;}public float SpaceY{get;init;}public void NextRow(bool force=false){if(column!=0||force){row++;column=0;ResetOffsets();}}public void NextColumn(bool force=false){if(row!=0||force){column++;row=0;ResetOffsets();}}public void StepRow(int max){row++;if(row>=max&&max>0){row=0;column++;ResetOffsets();}}public void StepColumn(int max){column++;if(column>=max&&max>0){column=0;row++;ResetOffsets();}}private void ResetOffsets(){if(resetOffset){offsetX=0;offsetY=0;resetOffset=false;}}public readonly float XMin(){return StartX+offsetX+SpaceX+column*Width;}public readonly float XMax(){return StartX+offsetX+(column+1)*Width;}public readonly float YMin(){return StartY+offsetY+SpaceY-(row+1)*Height;}public readonly float YMax(){return StartY+offsetY-row*Height;}public readonly Anchor Anchor(){return PluginComponents.RadtownEvent.Cui.Anchor.Relative(XMin(),XMax(),YMin(),YMax());}public readonly bool CheckBoundsY(){return YMin()>=0&&YMax()<=1;}public static MovingAnchorProperties Create(float startX,float startY,float width,float height,float spaceX,float spaceY){return new MovingAnchorProperties{StartX=startX,StartY=startY,Width=width,Height=height,SpaceX=spaceX,SpaceY=spaceY};}}public class Cui:IDisposable{private readonly BasePlugin plugin;private readonly Stack<string>parents;private List<CuiElement>elements;private string Parent=>parents.Peek();public UiRef CurrentRef=>new UiRef(Parent);public UiRef RootRef{get;private set;}public Cui(BasePlugin plugin,string parent){this.plugin=plugin;elements=Pool.GetList<CuiElement>();parents=new Stack<string>();parents.Push(parent);RootRef=CurrentRef;}public void AddBox(in Anchor anchor,in UiColor color){CreateContainer(anchor,color,0).Dispose();}public IDisposable CreateContainer(in Anchor anchor)=>CreateContainer(anchor,UiColor.Black,0);public IDisposable CreateContainer(in Anchor anchor,in UiColor background)=>CreateContainer(anchor,background,0);public IDisposable CreateContainer(in Anchor anchor,in UiColor background,float fadeOutTime){var element=CuiPool.GetElement(Guid(),Parent,anchor);element.FadeOut=fadeOutTime;var img=CuiPool.GetComponent<CuiImageComponent>();img.Color=background;AddElement(element.WithComponent(img));return CreateParentScope(element.Name);}public IDisposable CreateContainer(in Anchor anchor,in UiColor background,out UiRef self){var container=CreateContainer(anchor,background);self=CurrentRef;return container;}public IDisposable CreateOrUpdateContainer(in Anchor anchor,in UiColor background,ref UiRef self){var element=CuiPool.GetElement(Guid(),Parent,anchor);if(self.IsValid){element.DestroyUi=self.Name;}var img=CuiPool.GetComponent<CuiImageComponent>();img.Color=background;AddElement(element.WithComponent(img));self=new UiRef(element.Name);return CreateParentScope(element.Name);}public IDisposable CreateScrollContainer(in Anchor anchor,in Anchor contentAnchor,in ScrollViewOptions options){var element=CuiPool.GetElement(Guid(),Parent,anchor);var scroll=CuiPool.GetComponent<CuiScrollViewComponent>();scroll.MovementType=options.ScrollType;scroll.Elasticity=options.Elasticity;scroll.Horizontal=options.HorizontalScrollBar!=null;scroll.HorizontalScrollbar=options.HorizontalScrollBar;scroll.Vertical=options.VerticalScrollBar!=null;scroll.VerticalScrollbar=options.VerticalScrollBar;scroll.ScrollSensitivity=options.ScrollSensitivity;scroll.ContentTransform=CuiPool.GetTransform(contentAnchor);AddElement(element.WithComponent(scroll));return CreateParentScope(element.Name);}public IDisposable CreateScrollContainer(in Anchor anchor,in Anchor contentAnchor,in ScrollViewOptions options,out UiRef self){var container=CreateScrollContainer(anchor,contentAnchor,options);self=CurrentRef;return container;}private void CreateRootContainer(in Anchor anchor,in UiColor background,float fadeOutTime,bool keyboard,bool mouse,string material){var element=CuiPool.GetElement(Guid(),Parent,anchor);element.FadeOut=fadeOutTime;var img=CuiPool.GetComponent<CuiImageComponent>();img.Color=background;img.Material=material;AddElement(element.WithComponent(img).WithCursor(mouse).WithKeyboard(keyboard));parents.Push(element.Name);RootRef=CurrentRef;}public void AddImage(in Anchor anchor,string png){var image=CuiPool.GetComponent<CuiRawImageComponent>();image.Png=png;var el=CuiPool.GetElement(Guid(),Parent,anchor);AddElement(el.WithComponent(image));}public void AddImageUrl(in Anchor anchor,string url){var image=CuiPool.GetComponent<CuiRawImageComponent>();image.Url=url;var el=CuiPool.GetElement(Guid(),Parent,anchor);AddElement(el.WithComponent(image));}public void AddItemImage(in Anchor anchor,int itemId)=>AddItemImage(anchor,itemId,0);public void AddItemImage(in Anchor anchor,int itemId,ulong skinId){var el=CuiPool.GetElement(Guid(),Parent,anchor);var image=CuiPool.GetComponent<CuiImageComponent>();image.ItemId=itemId;image.SkinId=skinId;AddElement(el.WithComponent(image));}public void AddLabel(in Anchor anchor,string text)=>AddLabel(anchor,text,LabelStyle.Default);public void AddLabel(in Anchor anchor,string text,in LabelStyle style){var element=CuiPool.GetElement(Guid(),Parent,anchor);var textComp=CuiPool.GetComponent<CuiTextComponent>();textComp.Text=text;textComp.Color=style.TextColor;textComp.Align=style.TextAnchor;textComp.Font=style.Font;textComp.FontSize=style.FontSize;if(style.Outline!=default){var outlineComp=CuiPool.GetComponent<CuiOutlineComponent>();outlineComp.Color=style.Outline;outlineComp.Distance=$"{style.OutlineDistance.x} {style.OutlineDistance.y}";element.WithComponent(outlineComp);}AddElement(element.WithComponent(textComp));}public void AddButton(in Anchor anchor,string text,Action<BasePlayer>callback,bool singleUse=true)=>AddButton(anchor,text,callback,ButtonStyle.Default,singleUse);public void AddButton(in Anchor anchor,string text,Action<BasePlayer>callback,in ButtonStyle style,bool singleUse=true)=>AddButton(anchor,text,callback,style.ButtonColor,style.TextStyle,singleUse);public void AddButton(in Anchor anchor,string text,Action<BasePlayer>callback,in UiColor color,in LabelStyle textStyle,bool singleUse=true){var buttonGuid=Guid();var buttonComp=CuiPool.GetComponent<CuiButtonComponent>();buttonComp.Color=color;buttonComp.Command=callback==null?null:plugin.CreateUiCommand(buttonGuid,callback,singleUse);var button=CuiPool.GetElement(buttonGuid,Parent,anchor);AddElement(button.WithComponent(buttonComp));var textComp=CuiPool.GetComponent<CuiTextComponent>();textComp.Text=text;textComp.Color=textStyle.TextColor;textComp.Align=textStyle.TextAnchor;textComp.Font=textStyle.Font;textComp.FontSize=textStyle.FontSize;var textElement=CuiPool.GetElement(Guid(),buttonGuid,Anchor.Fill);AddElement(textElement.WithComponent(textComp));}public void AddInputField(in Anchor anchor,string value,Action<BasePlayer,string>callback,InputFieldFlags flags=InputFieldFlags.None,bool singleUse=true)=>AddInputField(anchor,value,callback,LabelStyle.Default,flags,singleUse);public void AddInputField(in Anchor anchor,string value,Action<BasePlayer,string>callback,in LabelStyle style,InputFieldFlags flags=InputFieldFlags.None,bool singleUse=true){var guid=Guid();var inputComp=CuiPool.GetComponent<CuiInputFieldComponent>();inputComp.Command=plugin.CreateUiCommand(guid,callback,singleUse);inputComp.Text=value;inputComp.Color=style.TextColor;inputComp.Align=style.TextAnchor;inputComp.Font=style.Font;inputComp.FontSize=style.FontSize;inputComp.Autofocus=flags.HasFlag(InputFieldFlags.Autofocus);inputComp.ReadOnly=flags.HasFlag(InputFieldFlags.ReadOnly);inputComp.HudMenuInput=flags.HasFlag(InputFieldFlags.HudMenuInput);inputComp.IsPassword=flags.HasFlag(InputFieldFlags.Password);if(flags.HasFlag(InputFieldFlags.MultiLine)){inputComp.LineType=InputField.LineType.MultiLineNewline;}else if(flags.HasFlag(InputFieldFlags.WordWrap)){inputComp.LineType=InputField.LineType.MultiLineSubmit;}var element=CuiPool.GetElement(guid,Parent,anchor);AddElement(element.WithComponent(inputComp));}public UiRef Send(BasePlayer player)=>Send(player,default);public UiRef Send(BasePlayer player,UiRef old){if(old.IsValid&&elements.Count>0){elements[0].DestroyUi=old.Name;}CuiHelper.AddUi(player,elements);return new UiRef(Parent);}public void Send(BasePlayer player,ref UiRef containerRef){containerRef=Send(player,containerRef);}public static Cui Create(BasePlugin plugin,UiRef parent)=>Create(plugin,parent,null,0);public static Cui Create(BasePlugin plugin,UiRef parent,string material,float opacity){var cui=new Cui(plugin,parent.Name);cui.CreateRootContainer(Anchor.Fill,new UiColor(0,0,0,opacity),0,false,false,material);return cui;}public static Cui CreateWithoutContainer(BasePlugin plugin,UiParentLayer parent,UiFlags flags=UiFlags.None){var parentName=parent switch{UiParentLayer.Overlay=>"Overlay",UiParentLayer.OverlayNonScaled=>"OverlayNonScaled",UiParentLayer.Hud=>"Hud",UiParentLayer.Menu=>"Hud.Menu",_=>"Under"};return new Cui(plugin,parentName);}public static Cui Create(BasePlugin plugin,in Anchor anchor,in UiColor background,UiParentLayer parent,UiFlags flags=UiFlags.None,float fadeOutTime=0,string material=null){var parentName=parent switch{UiParentLayer.Overlay=>"Overlay",UiParentLayer.OverlayNonScaled=>"OverlayNonScaled",UiParentLayer.Hud=>"Hud",UiParentLayer.Menu=>"Hud.Menu",_=>"Under"};var cui=new Cui(plugin,parentName);cui.CreateRootContainer(anchor,background,fadeOutTime,flags.HasFlag(UiFlags.Keyboard),flags.HasFlag(UiFlags.Mouse),material);return cui;}public static void Destroy(BasePlayer player,UiRef cuiRef){CuiHelper.DestroyUi(player,cuiRef.Name);}public void Dispose(){CuiPool.FreeElements(elements);Pool.FreeList(ref elements);}private IDisposable CreateParentScope(string parent){parents.Push(parent);return DisposeAction.Create(()=>parents.Pop());}private void AddElement(CuiElement element){elements.Add(element);}private static string Guid(){return System.Guid.NewGuid().ToString();}private struct DisposeAction:IDisposable{public Action Action;public DisposeAction(Action action){Action=action;}public void Dispose(){Action?.Invoke();Action=null;}public static IDisposable Create(Action action){return new DisposeAction(action);}}}public readonly struct UiRef{internal string Name{get;init;}public bool IsValid{get;init;}internal UiRef(string name){Name=name;IsValid=true;}}public enum UiParentLayer{Overlay,Hud,Menu,Under,OverlayNonScaled}[Flags]public enum UiFlags{None=0,Mouse=1,Keyboard=2,MouseAndKeyboard=Mouse|Keyboard,}public enum InputFieldFlags{None=0,ReadOnly=1<<0,Password=1<<1,Autofocus=1<<2,HudMenuInput=1<<3,MultiLine=1<<4,WordWrap=1<<5,}public struct ScrollViewOptions{public CuiScrollbar VerticalScrollBar{get;set;}public CuiScrollbar HorizontalScrollBar{get;set;}public ScrollRect.MovementType ScrollType{get;set;}public float Elasticity{get;set;}public float ScrollSensitivity{get;set;}}internal static class CuiElementEx{private static CuiNeedsCursorComponent cursorComponent;private static CuiNeedsKeyboardComponent keyboardComponent;internal static CuiElement WithKeyboard(this CuiElement element,bool enabled=true){if(!enabled){return element;}keyboardComponent??=new CuiNeedsKeyboardComponent();element.Components.Add(keyboardComponent);return element;}internal static CuiElement WithCursor(this CuiElement element,bool enabled=true){if(!enabled){return element;}cursorComponent??=new CuiNeedsCursorComponent();element.Components.Add(cursorComponent);return element;}internal static CuiElement WithComponent(this CuiElement element,ICuiComponent component){element.Components.Add(component);return element;}}internal static class CuiPool{public static void FreeElements(List<CuiElement>elements){for(int i=0;i<elements.Count;i++){var el=elements[i];FreeElement(ref el);elements[i]=null;}}public static CuiElement GetElement(string name,string parent,Anchor anchor){var el=Pool.Get<CuiElement>();el.Name=name;el.Parent=parent;el.Components.Add(GetTransform(anchor));return el;}public static CuiRectTransformComponent GetTransform(Anchor anchor){var transform=Pool.Get<CuiRectTransformComponent>();transform.AnchorMin=anchor.Min();transform.AnchorMax=anchor.Max();transform.OffsetMin=anchor.OffsetMin();transform.OffsetMax=anchor.OffsetMax();return transform;}public static T GetComponent<T>()where T:class,ICuiComponent,new(){return Pool.Get<T>();}public static void FreeElement(ref CuiElement element){for(int i=0;i<element.Components.Count;i++){var comp=element.Components[i];if(comp is CuiRectTransformComponent rect){FreeComponent(ref rect);}else if(comp is CuiImageComponent image){FreeComponent(ref image);}else if(comp is CuiRawImageComponent rawImage){FreeComponent(ref rawImage);}else if(comp is CuiTextComponent text){FreeComponent(ref text);}else if(comp is CuiButtonComponent button){FreeComponent(ref button);}element.Components[i]=null;}element.Components.Clear();element.DestroyUi=default;element.FadeOut=default;element.Name=default;element.Parent=default;element.Update=default;Pool.FreeUnsafe(ref element);}public static void FreeComponent<T>(ref T component)where T:class,ICuiComponent,new(){if(component is CuiNeedsCursorComponent||component is CuiNeedsKeyboardComponent){return;}foreach(var prop in component.GetType().GetProperties()){if(prop.CanWrite){prop.SetValue(component,null);}}Pool.FreeUnsafe(ref component);}}}namespace PluginComponents.RadtownEvent.Cui.Style{using System;using UnityEngine;using PluginComponents.RadtownEvent;using PluginComponents.RadtownEvent.Cui;public readonly record struct ButtonStyle{public UiColor ButtonColor{get;init;}public LabelStyle TextStyle{get;init;}public static ButtonStyle Default{get;}=new ButtonStyle{ButtonColor=Color.blue,TextStyle=LabelStyle.Default};public static implicit operator ButtonStyle(in ValueTuple<UiColor,LabelStyle>valueTuple){return new ButtonStyle{ButtonColor=valueTuple.Item1,TextStyle=valueTuple.Item2};}}public readonly record struct LabelStyle{public TextAnchor TextAnchor{get;init;}public UiColor TextColor{get;init;}public UiFont Font{get;init;}public int FontSize{get;init;}public UiColor Outline{get;init;}public Vector2 OutlineDistance{get;init;}public LabelStyle(){Font=UiFont.robotoRegular;TextColor=UiColor.White;OutlineDistance=new Vector2(1,-1);}public static readonly LabelStyle Default=new LabelStyle{TextAnchor=TextAnchor.MiddleCenter,TextColor=Color.white,Font=UiFont.robotoRegular,FontSize=12};}public readonly record struct UiColor{public float R{get;init;}public float G{get;init;}public float B{get;init;}public float Opacity{get;init;}public UiColor(float r,float g,float b,float opacity=1f){R=r;G=g;B=b;Opacity=opacity;}public static implicit operator Color(UiColor color){return new Color(color.R,color.G,color.B,color.Opacity);}public static implicit operator UiColor(Color color){return new UiColor(color.r,color.g,color.b,color.a);}public static implicit operator string(UiColor color){return color.ToString();}public static UiColor operator*(UiColor color,float f){return new UiColor(Mathf.Clamp01(color.R*f),Mathf.Clamp01(color.G*f),Mathf.Clamp01(color.B*f),color.Opacity);}public override string ToString(){return$"{R:N3} {G:N3} {B:N3} {Opacity:N3}";}public static readonly UiColor Transparent=new UiColor(0f,0f,0f,0f);public static readonly UiColor White=new UiColor(1f,1f,1f,1f);public static readonly UiColor Black=new UiColor(0f,0f,0f,1f);}public class UiFont{public static readonly UiFont robotoRegular=new UiFont("robotocondensed-regular.ttf");public static readonly UiFont robotoBold=new UiFont("robotocondensed-bold.ttf");public static readonly UiFont permanentMarker=new UiFont("permanentmarker.ttf");public static readonly UiFont droidSansMono=new UiFont("droidsansmono.ttf");public string ResourceFile{get;}private UiFont(string resourceFile){ResourceFile=resourceFile;}public static implicit operator string(UiFont font){return font.ResourceFile;}}}namespace PluginComponents.RadtownEvent.Event{using Oxide.Core;using PluginComponents.RadtownEvent.Core;using System;using UnityEngine;using PluginComponents.RadtownEvent;public abstract class EventController:FacepunchBehaviour{public int EventDurationSeconds{get;set;}=-1;public int EventDelaySeconds{get;set;}=-1;public bool IsEventRunning{get;private set;}private bool _destroying;public void StartEventLoop()=>StartEventLoop(EventDelaySeconds);public void StartEventLoop(float initialDelay){if(initialDelay>=0||EventDelaySeconds>=0){Invoke(StartEvent,initialDelay>=0?initialDelay:EventDelaySeconds);}}public void StartEvent(){if(IsEventRunning){return;}IsEventRunning=true;CancelInvoke(StartEvent);try{OnEventStart();}catch(Exception ex){BasePlugin.LogError($"Failed to start event in '{GetType().Name}': {ex}");IsEventRunning=false;return;}if(EventDurationSeconds>=0){Invoke(EndEvent,EventDurationSeconds);}}public void EndEvent(){if(!IsEventRunning){return;}IsEventRunning=false;CancelInvoke(EndEvent);try{OnEventEnd();}catch(Exception ex){if(_destroying){throw;}BasePlugin.LogError($"Failed to end event in '{GetType().Name}': {ex}");}if(!_destroying&&EventDelaySeconds>=0){Invoke(StartEvent,EventDelaySeconds);}}protected virtual void OnDestroy(){_destroying=true;try{EndEvent();}catch(Exception ex){Interface.Oxide.LogError($"Failed to call EndEvent on {GetType().Name}. There might be left over objects. {ex}");}}protected abstract void OnEventStart();protected abstract void OnEventEnd();public void Destroy()=>DestroyImmediate(gameObject);public static T Create<T>()where T:EventController{return new GameObject().AddComponent<T>();}public static T Create<T>(int eventDelaySeconds,int eventDurationSeconds)where T:EventController{var comp=Create<T>();comp.EventDelaySeconds=eventDelaySeconds;comp.EventDurationSeconds=eventDurationSeconds;return comp;}}}namespace PluginComponents.RadtownEvent.Extensions.BaseNetworkable{using PluginComponents.RadtownEvent;using PluginComponents.RadtownEvent.Extensions;public static class BaseNetworkableEx{public static bool IsNullOrDestroyed(this global::BaseNetworkable baseNetworkable){return!baseNetworkable||baseNetworkable.IsDestroyed;}}}namespace PluginComponents.RadtownEvent.Extensions.Command{using Oxide.Core.Libraries.Covalence;using System;using PluginComponents.RadtownEvent;using PluginComponents.RadtownEvent.Extensions;public static class CommandExtensions{public static string GetString(this string[]args,int index,string def=""){if(args.Length>index){return args[index];}else{return def;}}public static ulong GetUlong(this string[]args,int index,ulong def=0){if(UInt64.TryParse(args.GetString(index),out var value)){return value;}return def;}public static int GetInt(this string[]args,int index,int def=0){if(Int32.TryParse(args.GetString(index),out var value)){return value;}return def;}public static BasePlayer ToBasePlayer(this IPlayer iPlayer){return iPlayer.Object as BasePlayer;}public static bool TryGetBasePlayer(this IPlayer iplayer,out BasePlayer player){player=iplayer.IsServer?null:iplayer.Object as BasePlayer;return player!=null;}}}namespace PluginComponents.RadtownEvent.Extensions.Reflection{using Oxide.Core;using System;using System.Linq;using System.Reflection;using System.Text.RegularExpressions;using PluginComponents.RadtownEvent;using PluginComponents.RadtownEvent.Extensions;public static class ReflectionExtensions{public static T GetField<T>(this object obj,string fieldName){var field=obj.GetType().GetField(fieldName,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(field!=null){return(T)field.GetValue(obj);}else{Interface.Oxide.LogError($"Failed to get value of field {obj.GetType().Name}.{fieldName}");return default;}}public static void SetField(this object obj,string fieldName,object value){var field=obj.GetType().GetField(fieldName,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(field!=null){field.SetValue(obj,value);}else{Interface.Oxide.LogError($"Failed to set value of field {obj.GetType().Name}.{fieldName}");}}public static void SetField<T>(this T obj,string fieldName,object value){var field=typeof(T).GetField(fieldName,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(field!=null){field.SetValue(obj,value);}else{Interface.Oxide.LogError($"Failed to set value of field {obj.GetType().Name}.{fieldName}");}}public static void CallMethod(this object obj,string methodName,params object[]args){var method=obj.GetType().GetMethod(methodName,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(method!=null){method.Invoke(obj,args);}else{Interface.Oxide.LogError($"Failed to invoke method {obj.GetType().Name}.{methodName} with {args.Length} args");}}public static T CallMethod<T>(this object obj,string methodName,params object[]args){var method=obj.GetType().GetMethod(methodName,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(method!=null){return(T)method.Invoke(obj,args);}else{Interface.Oxide.LogError($"Failed to invoke method {obj.GetType().Name}.{methodName} with {args.Length} args");return default;}}public static MethodInfo FindLocalMethod(this Type type,string surroundingName,string localName,bool capturesVariables){var methods=type.GetMethods(BindingFlags.NonPublic|BindingFlags.Public|(capturesVariables?BindingFlags.Instance:BindingFlags.Static));return methods.FirstOrDefault(m=>m.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>()!=null&&Regex.IsMatch(m.Name,$@"^<{surroundingName}>g__{localName}\|\d+(_\d+)?"));}}}namespace PluginComponents.RadtownEvent.Tools.Entity{using Facepunch;using JetBrains.Annotations;using PluginComponents.RadtownEvent.Extensions.BaseNetworkable;using System;using System.Collections.Generic;using System.Reflection;using UnityEngine;using PluginComponents.RadtownEvent;using PluginComponents.RadtownEvent.Tools;public static class EntityTools{public static TCustom CreateCustomEntity<TEnt,TCustom>(string prefab,Vector3 position=default,Quaternion rotation=default)where TEnt:BaseEntity where TCustom:TEnt{var entity=CreateEntity<TEnt>(prefab,position,rotation,false,false);var customEntity=entity.gameObject.AddComponent<TCustom>();var fields=typeof(TEnt).GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);foreach(var field in fields){field.SetValue(customEntity,field.GetValue(entity));}UnityEngine.Object.DestroyImmediate(entity,true);customEntity.gameObject.AwakeFromInstantiate();return customEntity;}public static T CreateEntity<T>(string prefab,bool save=false)where T:BaseEntity=>CreateEntity<T>(prefab,default,Quaternion.identity,save,true);public static T CreateEntity<T>(string prefab,Vector3 position,bool save=false)where T:BaseEntity=>CreateEntity<T>(prefab,position,Quaternion.identity,save,true);public static T CreateEntity<T>(string prefab,Vector3 position,Vector3 rotation,bool save=false)where T:BaseEntity=>CreateEntity<T>(prefab,position,Quaternion.Euler(rotation),save,true);public static T CreateEntity<T>(string prefab,Vector3 position,Quaternion rotation,bool save=false)where T:BaseEntity=>CreateEntity<T>(prefab,position,rotation,save,true);private static T CreateEntity<T>(string prefab,Vector3 position,Quaternion rotation,bool save,bool active)where T:BaseEntity{var ent=GameManager.server.CreateEntity(prefab,position,rotation,active);if(ent is not T entity){UnityEngine.Object.Destroy(ent);throw new InvalidCastException($"Failed to create entity of type '{typeof(T).Name}' from '{prefab}'");}ent.enableSaving=save;return entity;}public static void KillSafe<T>([ItemCanBeNull]IEnumerable<T>entities)where T:BaseEntity{var list=Pool.Get<List<T>>();list.AddRange(entities);Kill(list);Pool.FreeUnmanaged(ref list);}public static void Kill<T>([ItemCanBeNull]IEnumerable<T>entities)where T:BaseEntity{foreach(var entity in entities){Kill(entity);}}public static void Kill([CanBeNull]BaseEntity entity){if(entity is not null&&!entity.IsDestroyed){entity.Kill();}}}}namespace PluginComponents.RadtownEvent.Zone{using Rust;using System;using System.Collections.Generic;using UnityEngine;using PluginComponents.RadtownEvent;public class Zone:FacepunchBehaviour{public static Zone Create(BaseEntity parent,float radius,int sphereCount,Vector3 localPosition=default)=>Create<Zone>(parent,radius,sphereCount,localPosition);public static Zone Create(Vector3 position,float radius,int sphereCount)=>Create<Zone>(position,radius,sphereCount);public static TZone Create<TZone>(BaseEntity parent,float radius,int sphereCount,Vector3 localPosition=default)where TZone:Zone{var go=new GameObject();var zone=go.AddComponent<TZone>();zone.Setup(localPosition,radius,sphereCount,parent);return zone;}public static TZone Create<TZone>(Vector3 position,float radius,int sphereCount)where TZone:Zone{var go=new GameObject();var zone=go.AddComponent<TZone>();zone.Setup(position,radius,sphereCount);return zone;}private SphereCollider collider;private readonly List<SphereEntity>spheres=new();public readonly HashSet<BasePlayer>Players=new();public event Action<BasePlayer>OnPlayerEnter;public event Action<BasePlayer>OnPlayerLeave;protected virtual void PlayerEnter(BasePlayer player)=>OnPlayerEnter?.Invoke(player);protected virtual void PlayerLeave(BasePlayer player)=>OnPlayerLeave?.Invoke(player);void OnTriggerEnter(Collider other){var player=other.ToBaseEntity()as BasePlayer;if(player!=null&&!player.IsNpc){Players.Add(player);PlayerEnter(player);}}void OnTriggerExit(Collider other){var player=other.ToBaseEntity()as BasePlayer;if(player!=null&&!player.IsNpc){Players.Remove(player);PlayerLeave(player);}}public void Setup(Vector3 position,float radius,int sphereCount,BaseEntity parent=null){gameObject.layer=(int)Layer.Reserved1;if(parent!=null){gameObject.transform.SetParent(parent.transform,false);gameObject.transform.localPosition=position;}else{gameObject.transform.position=position;}collider=gameObject.AddComponent<SphereCollider>();collider.isTrigger=true;collider.radius=radius;for(int i=0;i<sphereCount;i++){var sphere=(SphereEntity)GameManager.server.CreateEntity("assets/prefabs/visualization/sphere.prefab",collider.transform.position);sphere.currentRadius=radius*2;sphere.lerpSpeed=0f;sphere.enableSaving=false;sphere.Spawn();if(parent!=null){sphere.SetParent(parent,true);sphere.transform.localPosition=position;sphere.SendNetworkUpdate();}spheres.Add(sphere);}}public void SetRadius(float radius){collider.radius=radius;foreach(var sphere in spheres){sphere.currentRadius=radius*2f;sphere.SendNetworkUpdate();}}public void UpdateSpherePosition(){foreach(var sphere in spheres){sphere.transform.position=transform.position;sphere.SendNetworkUpdate_Position();}}protected virtual void OnDestroy(){foreach(var sphere in spheres){if(!sphere.IsDestroyed){sphere.Kill();}}Players.Clear();}public void Destroy(){Destroy(gameObject);}}}namespace PluginComponents.RadtownEvent.Extensions.Lang{using PluginComponents.RadtownEvent.Core;using System.Collections.Generic;using PluginComponents.RadtownEvent;using PluginComponents.RadtownEvent.Extensions;public static class LangEx{public static string GetMessage(this Oxide.Core.Libraries.Lang lang,string key,BasePlayer player)=>GetMessage(lang,key,player.userID.Get());public static string GetMessage(this Oxide.Core.Libraries.Lang lang,string key,ulong playerId){return lang.GetMessage(key,BasePlugin.Instance,playerId.ToString());}public static string GetMessage(this Oxide.Core.Libraries.Lang lang,string key,BasePlayer player,params object[]args)=>GetMessage(lang,key,player.userID,args);public static string GetMessage(this Oxide.Core.Libraries.Lang lang,string key,ulong playerId,params object[]args){var msg=lang.GetMessage(key,BasePlugin.Instance,playerId.ToString());return string.Format(msg,args);}public static void SendMessage(this Oxide.Core.Libraries.Lang lang,string key,BasePlayer player){var msg=GetMessage(lang,key,player.userID);BasePlugin.Instance.ChatMessage(player,msg);}public static void SendMessage(this Oxide.Core.Libraries.Lang lang,string key,BasePlayer player,params object[]args){var msg=GetMessage(lang,key,player.userID,args);BasePlugin.Instance.ChatMessage(player,msg);}public static void BroadcastMessage(this Oxide.Core.Libraries.Lang lang,string key)=>BroadcastMessage(lang,key,BasePlayer.activePlayerList);public static void BroadcastMessage(this Oxide.Core.Libraries.Lang lang,string key,IEnumerable<BasePlayer>players){foreach(var player in players){var msg=GetMessage(lang,key,player.userID);BasePlugin.Instance.ChatMessage(player,msg);}}public static void BroadcastMessage(this Oxide.Core.Libraries.Lang lang,string key,params object[]args)=>BroadcastMessage(lang,key,BasePlayer.activePlayerList,args);public static void BroadcastMessage(this Oxide.Core.Libraries.Lang lang,string key,IEnumerable<BasePlayer>players,params object[]args){foreach(var player in players){var msg=GetMessage(lang,key,player.userID,args);BasePlugin.Instance.ChatMessage(player,msg);}}public static string GetLanguage(this Oxide.Core.Libraries.Lang lang,BasePlayer player)=>GetLanguage(lang,player.userID);public static string GetLanguage(this Oxide.Core.Libraries.Lang lang,ulong userId){return lang.GetLanguage(userId.ToString());}}}namespace PluginComponents.RadtownEvent.External.Notify{using Oxide.Core;using Oxide.Core.Libraries;using Oxide.Core.Plugins;using PluginComponents.RadtownEvent.Core;using System;using PluginComponents.RadtownEvent;using PluginComponents.RadtownEvent.External;public static class NotifyExtensions{public static bool NotifyAvailable=>Notify!=null;private static Plugin Notify=>Interface.Oxide.RootPluginManager.GetPlugin("Notify");public static void NotifyBroadcast(this Lang lang,int notificationType,string key,params string[]args){if(Notify==null){BasePlugin.LogWarning("Failed to send notification. Notify is not installed");return;}foreach(var player in BasePlayer.activePlayerList){string message=lang.GetMessage(key,BasePlugin.Instance,player.UserIDString);NotifyPlayer(player,notificationType,String.Format(message,args));}}public static void NotifyPlayer(this Lang lang,BasePlayer player,int notificationType,string key,params string[]args){if(Notify==null){BasePlugin.LogWarning("Failed to send notification. Notify is not installed");return;}string message=lang.GetMessage(key,BasePlugin.Instance,player.UserIDString);NotifyPlayer(player,notificationType,String.Format(message,args));}public static void NotifyPlayer(BasePlayer player,int notificationType,string message){Console.WriteLine("Send notify");Notify?.Call("SendNotify",player,notificationType,message);}}}namespace PluginComponents.RadtownEvent.Loot{using Newtonsoft.Json;using System;using System.Collections.Generic;using System.Linq;using UnityEngine;using PluginComponents.RadtownEvent;public static class LootManager{public static void FillWithLoot(StorageContainer container,IEnumerable<LootItem>lootTable)=>FillWithLoot(container.inventory,lootTable);public static void FillWithLoot(ItemContainer container,IEnumerable<LootItem>lootTable){ClearContainer(container);int amt=0;foreach(var itm in lootTable){if(UnityEngine.Random.Range(0f,1f)<=itm.chance){var item=itm.CreateItem();if(item==null){continue;}if(!item.MoveToContainer(container)){item.Remove();}amt++;}if(amt>=container.capacity){break;}}}public static void FillWithLoot(StorageContainer container,LootTable lootTable)=>FillWithLoot(container.inventory,lootTable);public static void FillWithLoot(ItemContainer container,LootTable lootTable){if(!lootTable.Enabled){return;}if(lootTable.minItems<=0||lootTable.maxItems<=0){FillWithLoot(container,lootTable.items);return;}ClearContainer(container);const int max_retries=50;int itemAmount=0;var targetItemAmount=UnityEngine.Random.Range(lootTable.minItems,lootTable.maxItems+1);targetItemAmount=Mathf.Min(targetItemAmount,lootTable.items.Count);var included=new HashSet<string>();container.capacity=targetItemAmount;for(int i=0;(i<max_retries&&itemAmount<targetItemAmount);i++){foreach(var itm in lootTable.items){if(!included.Contains(itm.shortname)&&UnityEngine.Random.Range(0f,1f)<=itm.chance){var item=itm.CreateItem();if(item==null){continue;}if(!item.MoveToContainer(container)){item.Remove();}included.Add(itm.shortname);itemAmount++;}}}}private static void ClearContainer(ItemContainer container){container.Clear();ItemManager.DoRemoves();}public class LootTable{[JsonIgnore]public bool Enabled=>enabled&&items.Count>0;[JsonProperty("Enabled")]public bool enabled;[JsonProperty("Minimum items",DefaultValueHandling=DefaultValueHandling.Ignore)]public int minItems;[JsonProperty("Maximum items",DefaultValueHandling=DefaultValueHandling.Ignore)]public int maxItems;[JsonProperty("Item list",ObjectCreationHandling=ObjectCreationHandling.Replace)]public List<LootItem>items=new();public LootTable Copy(){return new LootTable{enabled=enabled,minItems=minItems,maxItems=maxItems,items=items.ToList(),};}}public class LootItem{[JsonProperty("Short name")]public string shortname;[JsonProperty("Min amount")]public int min;[JsonProperty("Max amount")]public int max;[JsonProperty("Chance (1 = 100%)")]public float chance;[JsonProperty("Skin id")]public ulong skin=0;[JsonProperty("Custom name")]public string customName=string.Empty;[JsonProperty("Text",DefaultValueHandling=DefaultValueHandling.Ignore)]public string text;[JsonIgnore]public ItemDefinition ItemDefinition=>ItemManager.FindItemDefinition(shortname);public LootItem(){shortname="scrap";min=5;max=10;chance=1f;skin=0;}public LootItem(string shortname,int min,int max,float chance){this.shortname=shortname;this.min=min;this.max=max;this.chance=chance;}public LootItem(string shortname,int min,int max,float chance,ulong skin){this.shortname=shortname;this.min=min;this.max=max;this.chance=chance;this.skin=skin;}public Item CreateItem(){if(ItemDefinition==null||ItemDefinition.itemid==-996920608){return null;}var itm=ItemManager.Create(ItemDefinition,UnityEngine.Random.Range(min,max+1),skin);itm?.OnVirginSpawn();if(customName!=null&&customName.Length>0){itm.name=customName;}if(text!=null&&text.Length>0){itm.text=text;}return itm;}public override int GetHashCode(){return HashCode.Combine(shortname,skin);}}}}namespace PluginComponents.RadtownEvent.LoottableApi.Static{using Oxide.Core.Plugins;using PluginComponents.RadtownEvent;using PluginComponents.RadtownEvent.LoottableApi;public static class LoottableApi{private static Plugin Loottable=>Oxide.Core.Interface.Oxide.RootPluginManager.GetPlugin("Loottable");public static void ClearPresets(Plugin plugin){Loottable?.Call("ClearPresets",plugin);}public static void CreatePresetCategory(Plugin plugin,string displayName){Loottable?.Call("AddCategory",plugin,displayName);}public static void CreatePreset(Plugin plugin,string displayName,string iconOrUrl){CreatePreset(plugin,false,displayName,displayName,iconOrUrl);}public static void CreatePreset(Plugin plugin,bool isNpc,string displayName,string iconOrUrl){CreatePreset(plugin,isNpc,displayName,displayName,iconOrUrl);}public static void CreatePreset(Plugin plugin,string key,string displayName,string iconOrUrl){CreatePreset(plugin,false,key,displayName,iconOrUrl);}public static void CreatePreset(Plugin plugin,bool isNpc,string key,string displayName,string iconOrUrl){Loottable?.Call("AddPreset",plugin,isNpc,key,displayName,iconOrUrl);}public static bool AssignPreset(Plugin plugin,ScientistNPC npc,string key){return Loottable?.Call("AssignPreset",plugin,key,npc)!=null;}public static bool AssignPreset(Plugin plugin,StorageContainer container,string key){return Loottable?.Call("AssignPreset",plugin,key,container)!=null;}public static bool AssignPreset(Plugin plugin,ItemContainer container,string key){return Loottable?.Call("AssignPreset",plugin,key,container)!=null;}public static void ClearCustomItems(Plugin plugin){Loottable?.Call("ClearCustomItems",plugin);}public static void AddCustomItem(Plugin plugin,int itemId,ulong skinId){Loottable?.Call("AddCustomItem",plugin,itemId,skinId);}public static void AddCustomItem(Plugin plugin,int itemId,ulong skinId,string customName){Loottable?.Call("AddCustomItem",plugin,itemId,skinId,customName);}public static void AddCustomItem(Plugin plugin,int itemId,ulong skinId,string customName,bool persistent){Loottable?.Call("AddCustomItem",plugin,itemId,skinId,customName,persistent);}}}namespace PluginComponents.RadtownEvent.LoottableApi{using Oxide.Core.Plugins;using PluginComponents.RadtownEvent;public class LoottableApi{private static Plugin Loottable=>Oxide.Core.Interface.Oxide.RootPluginManager.GetPlugin("Loottable");private readonly Plugin plugin;public LoottableApi(Plugin plugin){this.plugin=plugin;}public void ClearPresets(){Loottable?.Call("ClearPresets",plugin);}public void CreatePresetCategory(string displayName){Loottable?.Call("AddCategory",plugin,displayName);}public void CreatePreset(string displayName,string iconOrUrl){CreatePreset(false,displayName,displayName,iconOrUrl);}public void CreatePreset(bool isNpc,string displayName,string iconOrUrl){CreatePreset(isNpc,displayName,displayName,iconOrUrl);}public void CreatePreset(string key,string displayName,string iconOrUrl){CreatePreset(false,key,displayName,iconOrUrl);}public void CreatePreset(bool isNpc,string key,string displayName,string iconOrUrl){Loottable?.Call("AddPreset",plugin,isNpc,key,displayName,iconOrUrl);}public bool AssignPreset(ScientistNPC npc,string key){return Loottable?.Call("AssignPreset",plugin,key,npc)!=null;}public bool AssignPreset(StorageContainer container,string key){return Loottable?.Call("AssignPreset",plugin,key,container)!=null;}public bool AssignPreset(ItemContainer container,string key){return Loottable?.Call("AssignPreset",plugin,key,container)!=null;}public void ClearCustomItems(){Loottable?.Call("ClearCustomItems",plugin);}public void AddCustomItem(int itemId,ulong skinId){Loottable?.Call("AddCustomItem",plugin,itemId,skinId);}public void AddCustomItem(int itemId,ulong skinId,string customName){Loottable?.Call("AddCustomItem",plugin,itemId,skinId,customName);}public void AddCustomItem(int itemId,ulong skinId,string customName,bool persistent){Loottable?.Call("AddCustomItem",plugin,itemId,skinId,customName,persistent);}}}namespace PluginComponents.RadtownEvent.MapMarker{using Oxide.Core;using PluginComponents.RadtownEvent.Core;using PluginComponents.RadtownEvent.Extensions.BaseNetworkable;using System;using System.Collections.Generic;using System.Linq;using System.Text;using UnityEngine;using PluginComponents.RadtownEvent;public class CustomMapMarker{public Vector3 Position{get=>vendingMarker.transform.position;set=>vendingMarker.transform.position=value;}private VendingMachineMapMarker vendingMarker;private MapMarkerGenericRadius colorMarker;private MapMarkerGenericRadius[]paintMarkers;private readonly string[]lines;private bool isParented;private CustomMapMarker(int lines){if(lines<1){throw new ArgumentException("Marker line count must be positive",nameof(lines));}this.lines=new string[lines];}private void Spawn(Vector3 position,Color color,float radius,BaseEntity parent){isParented=parent!=null;vendingMarker=GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab",position).GetComponent<VendingMachineMapMarker>();vendingMarker.enableSaving=false;vendingMarker.Spawn();if(isParented){vendingMarker.SetParent(parent);}vendingMarker.SendNetworkUpdate();colorMarker=CreateColorMarker(color,radius,vendingMarker);}private static MapMarkerGenericRadius CreateColorMarker(Color color,float radius,VendingMachineMapMarker markerParent,Vector3 offset=default){var colorMarker=GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab",offset).GetComponent<MapMarkerGenericRadius>();colorMarker.color1=color;colorMarker.color2=colorMarker.color1;colorMarker.radius=radius*(4000f/World.Size);colorMarker.alpha=color.a;colorMarker.enableSaving=false;colorMarker.SetParent(markerParent);colorMarker.Spawn();colorMarker.SendNetworkUpdate();colorMarker.SendUpdate();return colorMarker;}public void SetText(string text,bool networkUpdate=true)=>SetText(0,text,networkUpdate);public void SetText(int line,string text,bool networkUpdate=true){lines[line]=text.Trim();vendingMarker.markerShopName=String.Join('\n',lines);if(networkUpdate){SendNetworkUpdate();}}public void SendNetworkUpdate(bool fullUpdate=false){vendingMarker.SendNetworkUpdate();if(isParented||fullUpdate){colorMarker.SendNetworkUpdate();colorMarker.SendUpdate();if(paintMarkers!=null){foreach(var marker in paintMarkers){marker.SendNetworkUpdate();marker.SendUpdate();}}}}public void Destroy(){if(paintMarkers!=null){foreach(var marker in paintMarkers){if(!marker.IsNullOrDestroyed()){marker.Kill();}}}if(!colorMarker.IsNullOrDestroyed()){colorMarker.Kill();}if(!vendingMarker.IsNullOrDestroyed()){vendingMarker.Kill();}}public void Paint(Color color,Func<IEnumerable<Vector2>>dots,float dotSize){if(paintMarkers!=null){throw new InvalidOperationException("Marker can only be painted once");}var markerList=new List<MapMarkerGenericRadius>();foreach(var dot in dots.Invoke()){var marker=CreateColorMarker(color,dotSize,vendingMarker,dot.XZ3D());markerList.Add(marker);}paintMarkers=markerList.ToArray();}public static CustomMapMarker Create(Vector3 position,float radius,Color color,int lines=1)=>Create(null,position,radius,color,lines);public static CustomMapMarker Create(BaseEntity parent,Vector3 localPosition,float radius,Color color,int lines=1){var marker=new CustomMapMarker(lines);marker.Spawn(localPosition,color,radius,parent);marker.SendNetworkUpdate();return marker;}}}