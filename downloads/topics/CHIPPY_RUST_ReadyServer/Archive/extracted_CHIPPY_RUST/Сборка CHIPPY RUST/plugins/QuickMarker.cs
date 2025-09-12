
/*
 ########### README ####################################################
 #                                                                     #
 #   1. If you found a bug, please report them to developer!           #
 #   2. Don't edit that file (edit files only in CONFIG/LANG/DATA)     #
 #                                                                     #
 ########### CONTACT INFORMATION #######################################
 #                                                                     #
 #   Website: https://rustworkshop.space/                              #
 #   Discord: Orange#0900                                              #
 #   Email: admin@rustworkshop.space                                   #
 #                                                                     #
 #######################################################################
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using UnityEngine;
using VLB;
using Time = UnityEngine.Time;

namespace Oxide.Plugins
{
    // Creation date: 11-04-2021
    // Last update date: 05-11-2021
    [Info("Quick Marker", "Menevt", "1.0.1")] 
    [Description("by oxide russia")]
    public class QuickMarker : RustPlugin
    {
        #region Vars

        private static int ScanLayer = -1;
        private const string permUse = "quickmarker.use";
        private static Permission permissionEx;
        
        private static string[] layerNames =
        {
            "Player (Server)",
            "World",
            "Water", 
            "Terrain",
            "Construction", 
            "Default"
        };
        
        #endregion

        #region Oxide Hooks

        private void Init()
        {
            ScanLayer = LayerMask.GetMask(layerNames);
            permission.RegisterPermission(permUse, this);
            permissionEx = permission;
        }

        private void OnServerInitialized()
        {
            timer.Once(1f, AddScripts);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            player.gameObject.GetOrAddComponent<Script>();
        }

        private void Unload()
        {
            RemoveScripts();
        }

        private void CanCraft(ItemCrafter itemCrafter, ItemBlueprint bp, int amount)
        {
            var player = itemCrafter.baseEntity;
            if (player == null)
            {
                return;
            }
            
            MarkPlayer(player);
        }

        private static void MarkPlayer(BasePlayer player)
        {
            var component = player.GetComponent<Script>();
            if (component != null)
            {
                component.BlockPlacement(config.blockTime);
            }
        }

        #endregion

        #region Core

        private void AddScripts()
        {
            if (config.buttons.Length == 0)
            {
                PrintWarning("Buttons to marking is not defined! Plugin is not working!");
                Unsubscribe(nameof(OnPlayerConnected));
                return;
            }
            
            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
        }

        private void RemoveScripts()
        {
            foreach (var obj in UnityEngine.Object.FindObjectsOfType<Script>())
            {
                UnityEngine.Object.Destroy(obj);
            }
        }

        private static void ShowMark(BasePlayer player, Vector3 position, string text)
        {
            if (position == new Vector3())
            {
                return;
            }
            
            if (player.Connection.authLevel == 0)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                player.SendNetworkUpdateImmediate();
            }

            text = text.Replace("{m}", Vector3.Distance(player.transform.position, position).ToString("0"));
            player.SendConsoleCommand("ddraw.text", config.markRefreshRate, Color.clear, position, text);

            if (player.Connection.authLevel == 0)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                player.SendNetworkUpdateImmediate();
            }
        }

        #endregion

        #region Classes

        private static ConfigDefinition config = new ConfigDefinition();

        private partial class ConfigDefinition
        {
            [JsonProperty("Buttons to use")]
            public BUTTON[] buttons = new[]
            {
                BUTTON.FIRE_THIRD,
            };
            
            [JsonProperty("Marker duration (seconds)")]
            public float markDuration = 5f;

            [JsonProperty("Marker cooldown (seconds)")]
            public float markCooldown = 1f;

            [JsonProperty("Marker refresh rate (seconds)")]
            public float markRefreshRate = 1f;

            [JsonProperty("Markers limit (amount)")]  
            public int marksLimit = 3;
            
            [JsonProperty("Markers range limit (meters)")]
            public float rangeLimit = 150;

            [JsonProperty("Marker text")]
            public string markerText = "<color=#ff0000>●\nDanger</color> ({m}m, {t}s ago)";

            [JsonProperty("Effects on placement")]  
            public string[] effects =
            { 
                "assets/bundled/prefabs/fx/invite_notice.prefab",
                "assets/bundled/prefabs/fx/invite_notice.prefab",
            };

            [JsonIgnore] 
            public float blockTime = 1f;
        }

        #endregion

        #region Scripts

        private class Script : MonoBehaviour
        {
            private BasePlayer player;
            private RelationshipManager.PlayerTeam team;
            private float nextKeysCheck;
            private float blockUntil;
            private List<Marker> markers = new List<Marker>();
            private bool havePermission;
            private float currentTime => Time.realtimeSinceStartup;
            private const float keysCheckDelay = 0.2f;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
            }

            private void Start()
            {
                player.ConsoleMessage($"[{nameof(QuickMarker)}] Loaded markers by Orange#0900");
                InvokeRepeating(nameof(CheckPermission), 0f, 60f);
            }

            private void LateUpdate()
            {
                CheckInput();
            }

            public void BlockPlacement(float time)
            {
                blockUntil = currentTime + time;
            }
 
            private void CheckPermission()
            {
                if (permissionEx != null)
                {
                    havePermission = permissionEx.UserHasPermission(player.UserIDString, permUse);
                }
                else
                {
                    havePermission = true;
                }
            }

            private void CheckInput()
            {
                if (havePermission == false)
                {
                    return;
                }
                
                if (nextKeysCheck > currentTime)
                { 
                    return;
                }
                
                nextKeysCheck = currentTime + keysCheckDelay; 
               
                if (ButtonsActive() == false)
                {
                    return;
                }

                Invoke(nameof(TryCreate), keysCheckDelay / 2);
            }

            private void TryCreate()
            {
                var loot = player.inventory.loot;
                if (loot.entitySource != null || loot.containers.Count > 0)
                {
                    player.ConsoleMessage($"[{nameof(QuickMarker)}] Preventing marker placement while looting");
                    return;
                }

                var blockDifference = Math.Abs(blockUntil - currentTime);
                if (blockDifference < config.blockTime + keysCheckDelay / 2)
                {
                    player.ConsoleMessage($"[{nameof(QuickMarker)}] Marker placement block {blockDifference}s");
                    CancelInvoke(nameof(CreateMarker));
                    return;
                }
                
                CreateMarker();
            }

            private bool ButtonsActive()
            {
                var input = player.serverInput;
                var buttons = config.buttons;
                if (buttons.Length == 0)
                {
                    Destroy(this);
                    return false;
                }
                
                if (buttons.Length == 1)
                {
                    var button = buttons[0];
                    if (input.IsDown(button) == false && input.WasDown(button) == false)
                    {
                        return false;
                    }
                }
                else
                {
                    foreach (var button in buttons)
                    {
                        if (input.IsDown(button) == false && input.WasDown(button) == false)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }

            private void CreateMarker()
            {
                player.ConsoleMessage($"[{nameof(QuickMarker)}] Trying to place marker");
                
                RaycastHit hit;
                if (Physics.Raycast(player.eyes.HeadRay(), out hit, config.rangeLimit, ScanLayer) == false)
                {
                    return;
                }
                
                var position = hit.point;
                BroadcastMarker(position);
                player.ConsoleMessage($"[{nameof(QuickMarker)}] Added marker at {position}");
            }

            private void BroadcastMarker(Vector3 position)
            {
                nextKeysCheck = Time.realtimeSinceStartup + config.markCooldown;
                RefreshTeam();
                var players = (BasePlayer[]) null;
                
                if (team == null)
                {
                    players = new[] {player};
                }
                else
                {
                    players = team.GetOnlineMemberConnections().Select(x => x.player as BasePlayer).ToArray();
                }

                var obj = new GameObject().AddComponent<Marker>();
                obj.players = players;
                obj.transform.position = position;

                if (config.marksLimit > 0)
                {
                    markers.RemoveAll(x => x == null);
                    markers.Add(obj);

                    if (markers.Count > config.marksLimit)
                    {
                        var old = markers.ElementAt(0);
                        Destroy(old);
                    }
                }

                if (config.effects.Length > 0)
                {
                    foreach (var prefab in config.effects)
                    {
                        var effect = new EffectEx();
                        effect.connections.AddRange(players.Select(x => x.Connection));
                        effect.path = prefab;
                        effect.Run();
                    }
                }
            }

            private void RefreshTeam()
            {
                if (player.currentTeam == 0ul)
                {
                    team = null;
                    return;
                }

                if (team == null || team.members.Contains(player.userID) == false)
                {
                    team = player.Team;
                }
            }
        }

        private class Marker : MonoBehaviour
        {
            public IEnumerable<BasePlayer> players;
            public float secondsPassed = 0;

            private void Start()
            {
                Invoke(nameof(TimedDestroy), config.markDuration);
                secondsPassed -= config.markRefreshRate;
                InvokeRepeating(nameof(RefreshMark), 0f, config.markRefreshRate);
            }

            private void RefreshMark()
            {
                if (players == null)
                {
                    Destroy(this);
                    return;
                }

                secondsPassed += config.markRefreshRate;

                foreach (var player in players)
                {
                    if (player == null || player.IsConnected == false)
                    {
                        continue;
                    }

                    var text = config.markerText.Replace("{t}", secondsPassed.ToString("0"));
                    ShowMark(player, transform.position, text);
                }
            }

            private void TimedDestroy()
            {
                Destroy(this);
            }
        }

        #endregion

        #region Effect Helper 1.0

        private class EffectEx
        {
            public List<Connection> connections = new List<Connection>();
            public string path = string.Empty;
            public Vector3 position;
            public int volume = 1;

            public void Run()
            {
                if (volume < 1)
                {
                    position.y -= volume;
                    volume = 1;
                }
            
                var effect = new Effect();
                effect.Init(Effect.Type.Generic, position, Vector3.zero);
                effect.pooledString = path;

                for (var i = 0; i < volume; i++)
                {
                    if (connections.Count == 0)
                    {
                        EffectNetwork.Send(effect);
                    }
                    else
                    {
                        foreach (var connection in connections)
                        {
                            if (position == new Vector3())
                            {
                                var player = connection.player as BasePlayer;
                                if (player != null)
                                {
                                    effect.worldPos = effect.origin = player.transform.position;
                                }
                            }
                            
                            EffectNetwork.Send(effect, connection);
                        }
                    }
                }   
            }
        }

        #endregion
        
        #region Configuration v2.2
        
        private partial class ConfigDefinition
        {
            
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigDefinition>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                for (var i = 0; i < 3; i++)
                {
                    PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                }
                
                LoadDefaultConfig();
                return;
            }

            ValidateConfig();
            SaveConfig();
        }

        private void ValidateConfig()
        {
            if (Interface.Oxide.CallHook("OnConfigValidate") != null)
            {
                PrintWarning("Using default configuration...");
                config = new ConfigDefinition();
            }
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigDefinition();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion
}
}
