using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Carbon.Plugins
{
    [Info("Node Controller", "Carbon Framework", "1.0.0")]
    [Description("Controls resource node minimum distances and performs automatic cleanup")]
    public class NodeController : CarbonPlugin
    {
        #region Configuration
        private Configuration _config = Configuration.Default();

        private sealed class Configuration
        {
            public bool Enabled = true;
            public Dictionary<string, float> MinimumDistances = new();
            public CleanupSettings CleanupSettings = new();

            public static Configuration Default()
            {
                return new Configuration
                {
                    MinimumDistances = new Dictionary<string, float>
                    {
                        ["ore.stone"] = 5f,
                        ["ore.metal"] = 8f,
                        ["ore.sulfur"] = 10f,
                        ["tree"] = 3f,
                        ["collectable.hemp"] = 2f,
                        ["collectable.mushroom"] = 2f,
                        ["collectable.pumpkin"] = 3f,
                        ["collectable.corn"] = 3f,
                        ["collectable.berry"] = 2f
                    },
                    CleanupSettings = new CleanupSettings
                    {
                        MaxNodesPerType = new Dictionary<string, int>
                        {
                            ["ore.stone"] = 500,
                            ["ore.metal"] = 300,
                            ["ore.sulfur"] = 200,
                            ["tree"] = 1000,
                            ["collectable.hemp"] = 200,
                            ["collectable.mushroom"] = 150,
                            ["collectable.pumpkin"] = 100,
                            ["collectable.corn"] = 100,
                            ["collectable.berry"] = 150
                        },
                        CleanupInterval = 300f,
                        RemovePercentage = 0.2f
                    }
                };
            }
        }

        private sealed class CleanupSettings
        {
            public bool Enabled = true;
            public Dictionary<string, int> MaxNodesPerType = new();
            public float CleanupInterval = 300f;
            public float RemovePercentage = 0.2f;
        }

        protected override void LoadConfig()
        {
            try
            {
                if (Config == null)
                {
                    LoadDefaultConfig();
                    return;
                }

                Configuration loadedConfig = Config.ReadObject<Configuration>();
                if (loadedConfig == null)
                {
                    LoadDefaultConfig();
                    return;
                }

                _config = loadedConfig;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading config: {ex.Message}\nStack: {ex.StackTrace}");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = Configuration.Default();
            SaveConfig();
        }

        protected override void SaveConfig()
        {
            if (Config == null)
            {
                return;
            }
            Config.WriteObject(_config);
        }
        #endregion Configuration

        #region Runtime Data
        private readonly Dictionary<string, List<Vector3>> _nodePositions = new();
        private readonly Dictionary<string, int> _nodeCounts = new();
        #endregion Runtime Data

        #region Lifecycle
        public override void Load()
        {
            try
            {
                LoadConfig();
                permission.RegisterPermission("nodecontroller.admin", this);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in Load: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        public override void IUnload()
        {
            try
            {
                timer?.DestroyAll();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in IUnload: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        private void OnServerInitialized(bool initial)
        {
            try
            {
                if (_config == null)
                {
                    LoadConfig();
                }

                if (!(_config?.Enabled ?? false))
                {
                    return;
                }

                if (timer == null)
                {
                    Logger.Error("Timer not initialized in OnServerInitialized");
                    return;
                }

                InitializeNodeCollections();
                TrackExistingNodes();

                if (_config.CleanupSettings?.Enabled ?? false)
                {
                    _ = timer.Every(_config.CleanupSettings.CleanupInterval, CleanupNodes);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in OnServerInitialized: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }
        #endregion Lifecycle

        #region Core Logic
        private void TrackExistingNodes()
        {
            IEnumerable<BaseNetworkable> entities = BaseNetworkable.serverEntities.Where(entity => entity is ResourceEntity or CollectibleEntity);
            foreach (BaseEntity entity in entities.Cast<BaseEntity>())
            {
                TrackNode(entity);
            }
        }

        private void TrackNode(BaseEntity entity)
        {
            try
            {
                if (entity?.transform == null)
                {
                    return;
                }

                string type = GetNodeType(entity);
                if (string.IsNullOrEmpty(type))
                {
                    return;
                }

                if (!_nodePositions.TryGetValue(type, out List<Vector3>? positions))
                {
                    positions = new List<Vector3>();
                    _nodePositions[type] = positions;
                    _nodeCounts[type] = 0;
                }

                Vector3 pos = entity.transform.position;

                if (_config.MinimumDistances.TryGetValue(type, out float minDistance) &&
                    positions.Any(p => Vector3.Distance(p, pos) < minDistance))
                {
                    entity.Kill();
                    return;
                }

                positions.Add(pos);
                _nodeCounts[type] = _nodeCounts.TryGetValue(type, out int count) ? count + 1 : 1;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in TrackNode: {ex.Message}");
            }
        }

        private string GetNodeType(BaseEntity entity)
        {
            if (entity == null)
            {
                return string.Empty;
            }

            string prefab = entity.PrefabName;

            return entity switch
            {
                ResourceEntity => GetResourceType(prefab),
                CollectibleEntity => GetCollectibleType(prefab),
                _ => string.Empty
            };
        }

        private string GetResourceType(string prefab)
        {
            return prefab switch
            {
                string s when s.Contains("stone-ore") => "ore.stone",
                string s when s.Contains("metal-ore") => "ore.metal",
                string s when s.Contains("sulfur-ore") => "ore.sulfur",
                string s when s.Contains("tree") => "tree",
                _ => string.Empty
            };
        }

        private string GetCollectibleType(string prefab)
        {
            return prefab switch
            {
                string s when s.Contains("hemp") => "collectable.hemp",
                string s when s.Contains("mushroom") => "collectable.mushroom",
                string s when s.Contains("pumpkin") => "collectable.pumpkin",
                string s when s.Contains("corn") => "collectable.corn",
                string s when s.Contains("berry") => "collectable.berry",
                _ => string.Empty
            };
        }

        private void CleanupNodes()
        {
            if (!_config.CleanupSettings.Enabled)
            {
                return;
            }

            foreach (string? type in _nodeCounts.Keys.ToList())
            {
                if (!_config.CleanupSettings.MaxNodesPerType.TryGetValue(type, out int max))
                {
                    continue;
                }

                if (_nodeCounts[type] <= max)
                {
                    continue;
                }

                int removeCount = Mathf.CeilToInt((_nodeCounts[type] - max) * _config.CleanupSettings.RemovePercentage);
                RemoveNodes(type, removeCount);
            }
        }

        private void RemoveNodes(string type, int count)
        {
            List<BaseEntity> toRemove = UnityEngine.Object.FindObjectsOfType<BaseEntity>()
                .Where(e => GetNodeType(e) == type)
                .Take(count)
                .ToList();

            foreach (BaseEntity? entity in toRemove)
            {
                entity.Kill();
                if (entity.transform != null)
                {
                    _ = _nodePositions[type].Remove(entity.transform.position);
                }
                _nodeCounts[type]--;
            }
        }

        private void InitializeNodeCollections()
        {
            try
            {
                if (_config == null)
                {
                    return;
                }

                // Initialize collections for all configured node types
                foreach (string nodeType in _config.MinimumDistances.Keys)
                {
                    if (!_nodePositions.ContainsKey(nodeType))
                    {
                        _nodePositions[nodeType] = new List<Vector3>();
                        _nodeCounts[nodeType] = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in InitializeNodeCollections: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }
        #endregion Core Logic

        #region Hooks
        private bool IsEntityProtected(BaseEntity entity)
        {
            return entity is ResourceEntity or CollectibleEntity;
        }

        private void OnEntitySpawned(BaseEntity entity)
        {
            if (entity == null || _config?.Enabled != true)
            {
                return;
            }

            try
            {
                if (entity.transform == null)
                {
                    return;
                }

                if (!IsEntityProtected(entity))
                {
                    return;
                }

                string type = GetNodeType(entity);
                if (string.IsNullOrEmpty(type))
                {
                    return;
                }

                // Initialize collections for this type if they don't exist
                if (!_nodePositions.ContainsKey(type))
                {
                    _nodePositions[type] = new List<Vector3>();
                    _nodeCounts[type] = 0;
                }

                TrackNode(entity);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in OnEntitySpawned: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        private void OnEntityKill(BaseEntity entity)
        {
            if (entity == null || _config?.Enabled != true)
            {
                return;
            }

            try
            {
                if (entity.transform == null)
                {
                    return;
                }

                if (!IsEntityProtected(entity))
                {
                    return;
                }

                string type = GetNodeType(entity);
                if (string.IsNullOrEmpty(type))
                {
                    return;
                }

                if (!_nodePositions.TryGetValue(type, out List<Vector3>? positions))
                {
                    return;
                }

                Vector3 pos = entity.transform.position;
                _ = positions.Remove(pos);

                if (_nodeCounts.TryGetValue(type, out int count) && count > 0)
                {
                    _nodeCounts[type] = count - 1;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in OnEntityKill: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }
        #endregion Hooks

        #region Commands
        [ChatCommand("nodecontroller.status")]
        private void StatusCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, "nodecontroller.admin"))
            {
                return;
            }

            player.ChatMessage(string.Join("\n",
                "Node Controller Status:",
                $"Enabled: {_config.Enabled}",
                "Counts: " + string.Join(", ", _nodeCounts.Select(kvp => $"{kvp.Key}: {kvp.Value}"))
            ));
        }
        #endregion Commands

        private bool HasPermission(BasePlayer player, string perm)
        {
            return player != null && permission.UserHasPermission(player.UserIDString, perm);
        }
    }
}