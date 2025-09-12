using System;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using System.Linq;
using Oxide.Core.Libraries;

namespace Oxide.Plugins
{
    [Info("Bubble", "Gammaa", "1.3.16")]
    [Description("Creates a capture zone sphere upon cupboard destruction")]
    public class BubbleZone : RustPlugin
    {
        [PluginReference]
        private Plugin Clans; // Reference to Clans plugin

        private const string SPHERE_PREFAB = "assets/bundled/prefabs/modding/events/twitch/br_sphere_purple.prefab";
        private const float SPHERE_RADIUS = 7.5f;
        private const float NOTIFICATION_RADIUS = 200f;
        private const float BUBBLE_LIFETIME = 5400f;
        private const float POST_CAPTURE_LIFETIME = 300f;
        private const float CAPTURE_TIME = 300f;
        private const string UI_PARENT = "BubbleZone_UI";
        private const string UI_NOTIFICATION = "BubbleZone_Notification";
        private const string WEBHOOK_URL = "https://discord.com/api/webhooks/1369012103050166455/bQd147qklPoHBppOsnJzRfp7ZgCw4PLEDwld5xaxqPscXg274Jqo-FLB4aywlF5lc3XM";
        private Dictionary<string, BubbleInfo> activeBubbles = new Dictionary<string, BubbleInfo>();
        private Dictionary<ulong, BubbleData> cupboardBubbles = new Dictionary<ulong, BubbleData>();

        private class BubbleInfo
        {
            public GameObject GameObject { get; set; }
            public Timer DestroyTimer { get; set; }
            public Vector3 Position { get; set; }
            public float CaptureProgress { get; set; }
            public Timer CaptureTimer { get; set; }
            public HashSet<ulong> PlayersInZone { get; set; } = new HashSet<ulong>();
            public HashSet<ulong> PlayersNotified { get; set; } = new HashSet<ulong>();
            public MapMarkerGenericRadius MapMarker { get; set; }
            public ulong CupboardId { get; set; }
            public ulong DefendingTeamId { get; set; }
            public ulong CapturingTeamId { get; set; }
            public string AttackingClan { get; set; }
            public string DefendingClan { get; set; }
        }

        private class BubbleData
        {
            public Vector3 Position { get; set; }
            public ulong TeamId { get; set; }
        }

        void Init()
        {
            permission.RegisterPermission("bubblezone.admin", this);
            cmd.AddChatCommand("bubbleadd", this, "CmdBubbleAdd");
            cmd.AddChatCommand("bubbleremove", this, "CmdBubbleRemove");

            if (Clans == null)
            {
                Puts("Warning: Clans plugin not loaded. Clan tags will default to 'Unknown'.");
            }
        }

        void Unload()
        {
            foreach (var bubbleInfo in activeBubbles.Values)
            {
                if (bubbleInfo.GameObject != null)
                    UnityEngine.Object.Destroy(bubbleInfo.GameObject);
                if (bubbleInfo.MapMarker != null)
                    bubbleInfo.MapMarker.Kill();
                bubbleInfo.DestroyTimer?.Destroy();
                bubbleInfo.CaptureTimer?.Destroy();
                
                foreach (var playerId in bubbleInfo.PlayersInZone.Union(bubbleInfo.PlayersNotified))
                {
                    var player = BasePlayer.FindByID(playerId);
                    if (player != null)
                    {
                        DestroyUI(player);
                        DestroyNotificationUI(player);
                    }
                }
            }
            activeBubbles.Clear();
            cupboardBubbles.Clear();
        }

        private void ShowUI(BasePlayer player, float progress)
        {
            DestroyUI(player);
            
            var elements = new CuiElementContainer();
            
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.2 0.2 0.2 0.8" },
                RectTransform = { AnchorMin = "0.3 0.9", AnchorMax = "0.7 0.92" },
                CursorEnabled = false
            }, "Overlay", UI_PARENT);

            elements.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.8" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, UI_PARENT);

            elements.Add(new CuiPanel
            {
                Image = { Color = "0.5 0 1 0.8" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = $"{progress} 1" }
            }, UI_PARENT);

            elements.Add(new CuiLabel
            {
                Text = { Text = $"{Math.Round(progress * 100)}%", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, UI_PARENT);

            CuiHelper.AddUi(player, elements);
        }

        private void ShowNotificationUI(BasePlayer player)
        {
            DestroyNotificationUI(player);
            
            var elements = new CuiElementContainer();
            
            elements.Add(new CuiPanel
            {
                Image = { Color = "0.2 0.2 0.2 0.8" },
                RectTransform = { AnchorMin = "0.3 0.88", AnchorMax = "0.7 0.9" },
                CursorEnabled = false
            }, "Overlay", UI_NOTIFICATION);

            elements.Add(new CuiLabel
            {
                Text = { Text = "Capture Zone Open!", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.5 0 1 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, UI_NOTIFICATION);

            CuiHelper.AddUi(player, elements);
        }

        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UI_PARENT);
        }

        private void DestroyNotificationUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UI_NOTIFICATION);
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            foreach (var bubble in activeBubbles.Values)
            {
                bubble.PlayersInZone.Remove(player.userID);
                bubble.PlayersNotified.Remove(player.userID);
                DestroyUI(player);
                DestroyNotificationUI(player);
            }
        }

        private void CheckPlayersInZone()
        {
            foreach (var bubble in activeBubbles.Values)
            {
                bubble.PlayersInZone.Clear();
                var spherePosition = bubble.Position;
                var teamsInZone = new HashSet<ulong>();

                foreach (var player in BasePlayer.activePlayerList)
                {
                    float distance = Vector3.Distance(player.transform.position, spherePosition);
                    
                    if (distance <= SPHERE_RADIUS)
                    {
                        bubble.PlayersInZone.Add(player.userID);
                        ShowUI(player, bubble.CaptureProgress);
                        if (player.currentTeam != 0)
                            teamsInZone.Add(player.currentTeam);
                    }
                    else if (distance <= NOTIFICATION_RADIUS)
                    {
                        bubble.PlayersNotified.Add(player.userID);
                        ShowNotificationUI(player);
                    }
                    else
                    {
                        if (bubble.PlayersInZone.Contains(player.userID) || bubble.PlayersNotified.Contains(player.userID))
                        {
                            DestroyUI(player);
                            DestroyNotificationUI(player);
                        }
                    }
                }

                if (bubble.CaptureTimer == null && teamsInZone.Count == 1)
                {
                    var teamId = teamsInZone.First();
                    if (teamId != bubble.DefendingTeamId)
                    {
                        bubble.CapturingTeamId = teamId;
                        StartCaptureTimer(bubble);
                    }
                }
                else if (bubble.CaptureTimer != null && (teamsInZone.Count != 1 || !teamsInZone.Contains(bubble.CapturingTeamId)))
                {
                    bubble.CaptureTimer?.Destroy();
                    bubble.CaptureTimer = null;
                    bubble.CapturingTeamId = 0;
                }
            }
        }

        void OnServerInitialized()
        {
            timer.Every(1f, CheckPlayersInZone);
            timer.Every(5f, () => {
                foreach (var bubble in activeBubbles.Values)
                {
                    if (bubble.MapMarker != null)
                    {
                        bubble.MapMarker.SendUpdate();
                        bubble.MapMarker.SendNetworkUpdate();
                    }
                }
            });
        }

        private void BroadcastMessage(string message)
        {
            Server.Broadcast($"<color=#8000FF>[Zone]</color> {message}");
        }

        private string GetMapSquare(Vector3 pos)
        {
            float gridSize = 150f;
            float worldSize = ConVar.Server.worldsize;
            float offset = worldSize / 2;
            float relativeX = pos.x + offset;
            float relativeZ = pos.z + offset;
            int columnIndex = Mathf.FloorToInt(relativeX / gridSize);
            int rowIndex = Mathf.FloorToInt((worldSize - relativeZ) / gridSize);
            string letter = "";
            int tempIndex = columnIndex;
            while (tempIndex >= 0)
            {
                letter = (char)('A' + (tempIndex % 26)) + letter;
                tempIndex = tempIndex / 26 - 1;
            }
            return $"{letter}{rowIndex}";
        }

        private string GetClanTag(ulong playerId)
        {
            if (Clans == null)
            {
                Puts("Clans plugin not loaded!");
                return "Unknown";
            }

            string clanTag = Clans.Call<string>("GetClanOf", playerId);
            return clanTag ?? "Unknown";
        }

        private string GetClanTagFromTeam(ulong teamId)
        {
            if (teamId == 0)
                return "Unknown";

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.currentTeam == teamId)
                    return GetClanTag(player.userID);
            }
            return "Unknown";
        }

        private void SendWebhook(string message)
        {
            webrequest.Enqueue(WEBHOOK_URL, JsonConvert.SerializeObject(new { content = message }), (code, response) => {}, this, RequestMethod.POST, new Dictionary<string, string> { { "Content-Type", "application/json" } });
        }

        private void SendBubbleWebhook(Vector3 position, string attackingClan, string defendingClan)
        {
            var square = GetMapSquare(position);

            var embed = new
            {
                embeds = new[]
                {
                    new
                    {
                        title = "Bubble Opened!",
                        description = $"Clan **{attackingClan}** opened a bubble at square **{square}** against clan: **{defendingClan}**",
                        color = 8388736,
                        fields = new[]
                        {
                            new { name = "Capture Time", value = "90 minutes", inline = false }
                        },
                        image = new { url = "https://i.imgur.com/YourImage.png" },
                        footer = new { text = "BubbleEvent | By •GAMMA RUST" },
                        timestamp = DateTime.UtcNow.ToString("o")
                    }
                }
            };

            webrequest.Enqueue(WEBHOOK_URL, JsonConvert.SerializeObject(embed), (code, response) => {}, this, RequestMethod.POST, new Dictionary<string, string> { { "Content-Type", "application/json" } });
        }

        private void SendBubbleClosedWebhook(Vector3 position, string clanTag, bool captured)
        {
            var square = GetMapSquare(position);
            var action = captured ? "captured" : "defended";

            var embed = new
            {
                embeds = new[]
                {
                    new
                    {
                        title = "Bubble Closed!",
                        description = $"Clan **{clanTag}** {action} the bubble at square **{square}**!",
                        color = 16711680,
                        fields = new[]
                        {
                            new { name = "Capture Time", value = "90 minutes", inline = true },
                            new { name = "Coordinates", value = $"X: {position.x:F0}, Z: {position.z:F0}", inline = true }
                        },
                        image = new { url = "https://i.imgur.com/YourImage.png" },
                        footer = new { text = "BubbleEvent | By •GAMMA RUST" },
                        timestamp = DateTime.UtcNow.ToString("o")
                    }
                }
            };

            webrequest.Enqueue(WEBHOOK_URL, JsonConvert.SerializeObject(embed), (code, response) => {}, this, RequestMethod.POST, new Dictionary<string, string> { { "Content-Type", "application/json" } });
        }

        private void CmdBubbleAdd(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "bubblezone.admin"))
            {
                player.ChatMessage("You don't have permission to use this command!");
                return;
            }

            var cupboard = RaycastCupboard(player);
            if (cupboard == null)
            {
                player.ChatMessage("Look at a cupboard to add a bubble!");
                return;
            }

            var teamId = cupboard.GetBuildingPrivilege()?.authorizedPlayers.FirstOrDefault()?.userid ?? 0;
            cupboardBubbles[cupboard.net.ID.Value] = new BubbleData
            {
                Position = cupboard.transform.position,
                TeamId = teamId
            };
            player.ChatMessage("Bubble added to cupboard!");
        }

        private void CmdBubbleRemove(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "bubblezone.admin"))
            {
                player.ChatMessage("You don't have permission to use this command!");
                return;
            }

            var cupboard = RaycastCupboard(player);
            if (cupboard == null)
            {
                player.ChatMessage("Look at a cupboard to remove a bubble!");
                return;
            }

            if (cupboardBubbles.Remove(cupboard.net.ID.Value))
            {
                player.ChatMessage("Bubble removed from cupboard!");
            }
            else
            {
                player.ChatMessage("No bubble assigned to this cupboard!");
            }
        }

        private BuildingPrivlidge RaycastCupboard(BasePlayer player)
        {
            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, 5f))
            {
                return hit.GetEntity() as BuildingPrivlidge;
            }
            return null;
        }

        void OnEntityDeath(BaseEntity entity, HitInfo info)
        {
            if (entity is BuildingPrivlidge cupboard && cupboardBubbles.ContainsKey(cupboard.net.ID.Value))
            {
                var bubbleData = cupboardBubbles[cupboard.net.ID.Value];
                var attacker = info?.Initiator as BasePlayer;
                var attackingClan = attacker != null ? GetClanTag(attacker.userID) : "Unknown";
                CreateBubble(bubbleData.Position, cupboard.net.ID.Value, bubbleData.TeamId, attackingClan);
                cupboardBubbles.Remove(cupboard.net.ID.Value);
            }
        }

        private void CreateBubble(Vector3 position, ulong cupboardId, ulong defendingTeamId, string attackingClan)
        {
            var bubbleObj = GameManager.server.CreateEntity(SPHERE_PREFAB, position, Quaternion.identity) as SphereEntity;
            if (bubbleObj == null)
                return;

            bubbleObj.currentRadius = SPHERE_RADIUS * 2;
            bubbleObj.lerpSpeed = 0f;
            bubbleObj.Spawn();

            var renderer = bubbleObj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var material = new Material(Shader.Find("Hidden/Internal-Colored"));
                material.color = new Color(0.5f, 0f, 1f, 0.04f);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                material.SetInt("_ZWrite", 0);
                renderer.material = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var collider = bubbleObj.GetComponent<SphereCollider>();
            if (collider == null)
            {
                collider = bubbleObj.gameObject.AddComponent<SphereCollider>();
            }
            collider.radius = SPHERE_RADIUS;
            collider.isTrigger = true;

            bubbleObj.gameObject.layer = LayerMask.NameToLayer("Transparent");
            bubbleObj.gameObject.SetActive(true);

            var mapMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position) as MapMarkerGenericRadius;
            if (mapMarker != null)
            {
                mapMarker.alpha = 0.5f;
                mapMarker.color1 = new Color(0.5f, 0f, 1f, 0.3f);
                mapMarker.color2 = new Color(0.5f, 0f, 1f, 0.1f);
                mapMarker.radius = 0.2f;
                mapMarker.enableSaving = false;
                mapMarker.Spawn();
                mapMarker.SendUpdate();
                mapMarker.SendNetworkUpdate();
            }

            string bubbleId = cupboardId.ToString();
            var defendingClan = GetClanTagFromTeam(defendingTeamId);
            var bubbleInfo = new BubbleInfo
            {
                GameObject = bubbleObj.gameObject,
                Position = position,
                CaptureProgress = 0f,
                MapMarker = mapMarker,
                CupboardId = cupboardId,
                DefendingTeamId = defendingTeamId,
                AttackingClan = attackingClan,
                DefendingClan = defendingClan,
                DestroyTimer = timer.Once(BUBBLE_LIFETIME, () =>
                {
                    if (activeBubbles.ContainsKey(bubbleId))
                    {
                        CleanupBubble(bubbleId, "expired");
                    }
                })
            };

            activeBubbles[bubbleId] = bubbleInfo;

            BroadcastMessage("Capture Zone Event Started!");
            BroadcastMessage($"Zone is located at {GetMapSquare(position)}");
            BroadcastMessage($"Zone will exist for {BUBBLE_LIFETIME / 60} minutes!");
            SendBubbleWebhook(position, attackingClan, defendingClan);
        }

        private void StartCaptureTimer(BubbleInfo bubble)
        {
            bubble.CaptureTimer = timer.Every(1f, () =>
            {
                bubble.CaptureProgress = Math.Min(1f, bubble.CaptureProgress + (1f / CAPTURE_TIME));
                
                if (Math.Abs(bubble.CaptureProgress - 1f) < 0.001f)
                {
                    var captured = bubble.CapturingTeamId != bubble.DefendingTeamId;
                    var clanTag = captured ? GetClanTagFromTeam(bubble.CapturingTeamId) : bubble.DefendingClan;
                    var result = captured ? "captured" : "defended";
                    var message = $"<color=#8000FF>Capture Zone Event Ended! Zone at {GetMapSquare(bubble.Position)} {result} by clan {clanTag}!</color>";
                    BroadcastMessage(message);
                    SendBubbleClosedWebhook(bubble.Position, clanTag, captured);

                    var renderer = bubble.GameObject?.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = new Color(1f, 0f, 0f, 0.04f);
                    }

                    if (bubble.MapMarker != null)
                    {
                        bubble.MapMarker.color1 = new Color(1f, 0f, 0f, 0.3f);
                        bubble.MapMarker.color2 = new Color(1f, 0f, 0f, 0.1f);
                        bubble.MapMarker.SendUpdate();
                        bubble.MapMarker.SendNetworkUpdate();
                    }

                    foreach (var playerId in bubble.PlayersInZone.Union(bubble.PlayersNotified))
                    {
                        var player = BasePlayer.FindByID(playerId);
                        if (player != null)
                        {
                            DestroyUI(player);
                            DestroyNotificationUI(player);
                        }
                    }

                    bubble.CaptureTimer?.Destroy();
                    bubble.DestroyTimer?.Destroy();
                    bubble.DestroyTimer = timer.Once(POST_CAPTURE_LIFETIME, () =>
                    {
                        if (activeBubbles.ContainsKey(bubble.CupboardId.ToString()))
                        {
                            CleanupBubble(bubble.CupboardId.ToString(), result);
                        }
                    });
                }
            });
        }

        private void CleanupBubble(string bubbleId, string reason)
        {
            if (!activeBubbles.ContainsKey(bubbleId))
                return;

            var bubbleInfo = activeBubbles[bubbleId];
            if (bubbleInfo.GameObject != null)
                UnityEngine.Object.Destroy(bubbleInfo.GameObject);
            if (bubbleInfo.MapMarker != null)
                bubbleInfo.MapMarker.Kill();
            
            BroadcastMessage($"Zone at {GetMapSquare(bubbleInfo.Position)} {reason}!");
            
            foreach (var playerId in bubbleInfo.PlayersInZone.Union(bubbleInfo.PlayersNotified))
            {
                var zonePlayer = BasePlayer.FindByID(playerId);
                if (zonePlayer != null)
                {
                    DestroyUI(zonePlayer);
                    DestroyNotificationUI(zonePlayer);
                }
            }

            bubbleInfo.DestroyTimer?.Destroy();
            bubbleInfo.CaptureTimer?.Destroy();
            activeBubbles.Remove(bubbleId);
        }
    }
}