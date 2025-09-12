using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System.Globalization;
using System;

namespace Carbon.Plugins
{
    [Info("BiomeManager", "YourName", "1.1.0")]
    [Description("Advanced biome and topology management system")]
    public class BiomeManager : CarbonPlugin
    {
        #region Configuration

        private Configuration _config = null!;
        private readonly Dictionary<string, BiomePreset> _biomePresets = new();
        private readonly Dictionary<string, TopologyPreset> _topologyPresets = new();

        private sealed class BiomePreset
        {
            public float Temperature { get; set; }
            public float Humidity { get; set; }
            public float Radiation { get; set; }
            [JsonProperty("AnimalSpawns")]
            public Dictionary<string, float> AnimalSpawnRates { get; set; } = new();
            [JsonProperty(nameof(CollectibleDensity))]
            public Dictionary<string, float> CollectibleDensity { get; set; } = new();
        }

        private sealed class TopologyPreset
        {
            [JsonProperty("TopologyLayers")]
            public Dictionary<string, bool> Layers { get; set; } = new()
            {
                ["Forest"] = true,
                ["Road"] = false,
                ["Monument"] = false
            };

            [JsonProperty("NodeSpawns")]
            public Dictionary<string, float> NodeDensity { get; set; } = new()
            {
                ["ore"] = 0.5f,
                ["tree"] = 0.7f
            };

            public float TerrainHeight { get; set; }
            public float WaterLevel { get; set; }
            public float CliffSteepness { get; set; }
        }

        private sealed class Configuration
        {
            [JsonProperty(nameof(CurrentBiome))]
            public string CurrentBiome { get; set; } = string.Empty;

            [JsonProperty(nameof(CurrentTopology))]
            public string CurrentTopology { get; set; } = string.Empty;

            [JsonProperty(nameof(BiomePresets))]
            public Dictionary<string, BiomePreset> BiomePresets { get; set; } = new();

            [JsonProperty(nameof(TopologyPresets))]
            public Dictionary<string, TopologyPreset> TopologyPresets { get; set; } = new();

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    CurrentBiome = "Temperate",
                    CurrentTopology = "Flatlands",
                    BiomePresets = new Dictionary<string, BiomePreset>
                    {
                        ["Temperate"] = new BiomePreset { Temperature = 20f, Humidity = 50f, Radiation = 0f },
                        ["Arctic"] = new BiomePreset
                        {
                            Temperature = -10f,
                            Humidity = 30f,
                            Radiation = 0f,
                            AnimalSpawnRates = new Dictionary<string, float>
                            {
                                ["polarbear"] = 0.8f,
                                ["wolf"] = 0.5f,
                                ["stag"] = 0.1f
                            },
                            CollectibleDensity = new Dictionary<string, float>
                            {
                                ["hemp"] = 0.2f,
                                ["berry"] = 0.3f,
                                ["mushroom"] = 0.4f
                            }
                        },
                        ["Desert"] = new BiomePreset { Temperature = 40f, Humidity = 10f, Radiation = 0f }
                    },
                    TopologyPresets = new Dictionary<string, TopologyPreset>
                    {
                        ["Flatlands"] = new TopologyPreset { TerrainHeight = 0f, WaterLevel = 0f, CliffSteepness = 0f },
                        ["Mountains"] = new TopologyPreset { TerrainHeight = 100f, WaterLevel = 50f, CliffSteepness = 45f },
                        ["Islands"] = new TopologyPreset { TerrainHeight = 30f, WaterLevel = 20f, CliffSteepness = 60f }
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();
            if (_config == null)
            {
                LoadDefaultConfig();
            }
            else
            {
                // Copy presets to working dictionaries
                _biomePresets.Clear();
                foreach (KeyValuePair<string, BiomePreset> preset in _config.BiomePresets)
                {
                    _biomePresets[preset.Key] = preset.Value;
                }

                _topologyPresets.Clear();
                foreach (KeyValuePair<string, TopologyPreset> preset in _config.TopologyPresets)
                {
                    _topologyPresets[preset.Key] = preset.Value;
                }
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = Configuration.DefaultConfig();

            // Copy presets to working dictionaries
            _biomePresets.Clear();
            foreach (KeyValuePair<string, BiomePreset> preset in _config.BiomePresets)
            {
                _biomePresets[preset.Key] = preset.Value;
            }

            _topologyPresets.Clear();
            foreach (KeyValuePair<string, TopologyPreset> preset in _config.TopologyPresets)
            {
                _topologyPresets[preset.Key] = preset.Value;
            }

            SaveConfig();
        }

        protected override void SaveConfig()
        {
            // Update config with current presets
            _config.BiomePresets.Clear();
            foreach (KeyValuePair<string, BiomePreset> preset in _biomePresets)
            {
                _config.BiomePresets[preset.Key] = preset.Value;
            }

            _config.TopologyPresets.Clear();
            foreach (KeyValuePair<string, TopologyPreset> preset in _topologyPresets)
            {
                _config.TopologyPresets[preset.Key] = preset.Value;
            }

            Config.WriteObject(_config);
        }

        #endregion Configuration

        #region Hooks

        private void OnServerInitialized(bool isfirstload)
        {
            // Register permissions
            permission.RegisterPermission("biomemanager.admin", this);

            // Register commands
            cmd.AddChatCommand("biome.set", this, nameof(CmdSetBiome));
            cmd.AddChatCommand("topology.set", this, nameof(CmdSetTopology));
            cmd.AddChatCommand("biome.animals", this, nameof(BiomeAnimalsCommand));
            cmd.AddChatCommand("biome.collectibles", this, nameof(BiomeCollectiblesCommand));

            // Load initial biome/topology if set
            if (!string.IsNullOrEmpty(_config.CurrentBiome) && _biomePresets.ContainsKey(_config.CurrentBiome))
            {
                ApplyBiomeSettings(_config.CurrentBiome);
            }

            if (!string.IsNullOrEmpty(_config.CurrentTopology) && _topologyPresets.ContainsKey(_config.CurrentTopology))
            {
                ApplyTopologySettings(_config.CurrentTopology);
            }
        }

        #endregion Hooks

        #region Commands

        private void CmdSetBiome(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "biomemanager.admin"))
            {
                player.ChatMessage(GetMessage("PermissionDenied", player.UserIDString));
                return;
            }

            if (args.Length < 1)
            {
                player.ChatMessage($"Usage: {command} <biome>");
                player.ChatMessage($"Available biomes: {string.Join(", ", _biomePresets.Keys)}");
                return;
            }

            string biomeName = args[0];
            if (!_biomePresets.ContainsKey(biomeName))
            {
                player.ChatMessage($"Unknown biome: {biomeName}");
                player.ChatMessage($"Available biomes: {string.Join(", ", _biomePresets.Keys)}");
                return;
            }

            ApplyBiomeSettings(biomeName);
            player.ChatMessage(GetMessage("BiomeChanged", player.UserIDString, biomeName));
        }

        private void CmdSetTopology(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "biomemanager.admin"))
            {
                player.ChatMessage(GetMessage("PermissionDenied", player.UserIDString));
                return;
            }

            if (args.Length < 1)
            {
                player.ChatMessage($"Usage: {command} <preset>");
                player.ChatMessage($"Available presets: {string.Join(", ", _topologyPresets.Keys)}");
                return;
            }

            string presetName = args[0];
            if (!_topologyPresets.ContainsKey(presetName))
            {
                player.ChatMessage($"Unknown topology preset: {presetName}");
                player.ChatMessage($"Available presets: {string.Join(", ", _topologyPresets.Keys)}");
                return;
            }

            ApplyTopologySettings(presetName);
            player.ChatMessage(GetMessage("TopologyChanged", player.UserIDString, presetName));
        }

        [ChatCommand("biome.animals")]
        private void BiomeAnimalsCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "biomemanager.admin"))
            {
                player.ChatMessage(GetMessage("PermissionDenied", player.UserIDString));
                return;
            }

            if (args.Length < 3)
            {
                player.ChatMessage($"Usage: {command} <biome> <animal> <rate 0.0-1.0>");
                player.ChatMessage("Example: !biome.animals arctic polarbear 0.8");
                return;
            }

            string biomeName = args[0].ToLower(CultureInfo.CurrentCulture);
            string animalType = args[1].ToLower(CultureInfo.CurrentCulture);
            if (float.TryParse(args[2], out float rate) && _biomePresets.TryGetValue(biomeName, out BiomePreset? preset))
            {
                preset.AnimalSpawnRates[animalType] = Math.Clamp(rate, 0f, 1f);
                SaveConfig();
                player.ChatMessage($"Set {animalType} spawn rate to {rate} in {biomeName}");
            }
        }

        [ChatCommand("biome.collectibles")]
        private void BiomeCollectiblesCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length < 3)
            {
                player.ChatMessage($"Usage: {command} <biome> <resource> <density 0.0-1.0>");
                player.ChatMessage("Example: !biome.collectibles arctic hemp 0.5");
                return;
            }

            string biomeName = args[0].ToLower(CultureInfo.CurrentCulture);
            string resourceType = args[1].ToLower(CultureInfo.CurrentCulture);
            if (float.TryParse(args[2], out float density) && _biomePresets.TryGetValue(biomeName, out BiomePreset? preset))
            {
                preset.CollectibleDensity[resourceType] = Math.Clamp(density, 0f, 1f);
                SaveConfig();
                player.ChatMessage($"Set {resourceType} density to {density} in {biomeName}");
            }
        }

        #endregion Commands

        #region Utility Methods

        private void ApplyBiomeSettings(string biomeName)
        {
            BiomePreset preset = _biomePresets[biomeName];
            if (TerrainMeta.BiomeMap != null)
            {
                // Get terrain dimensions
                int width = Mathf.RoundToInt(TerrainMeta.Size.x);
                int height = Mathf.RoundToInt(TerrainMeta.Size.z);

                // Iterate over all terrain cells
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        int index = (y * width) + x;
                        TerrainMeta.BiomeMap.SetBiome(index, (int)preset.Temperature, (int)preset.Humidity, (int)preset.Radiation);
                    }
                }

                // Force biome map update
                TerrainMeta.BiomeMap.Push();
            }

            // Apply animal spawn rates
            foreach (KeyValuePair<string, float> animal in preset.AnimalSpawnRates)
            {
                _ = ConsoleSystem.Run(ConsoleSystem.Option.Server,
                    $"spawnpopulation.set {animal.Key} {animal.Value.ToString(CultureInfo.InvariantCulture)}");
            }

            // Apply resource density
            foreach (KeyValuePair<string, float> resource in preset.CollectibleDensity)
            {
                _ = ConsoleSystem.Run(ConsoleSystem.Option.Server,
                    $"spawnresource.set {resource.Key} {resource.Value.ToString(CultureInfo.InvariantCulture)}");
            }

            _config.CurrentBiome = biomeName;
            SaveConfig();
        }

        private void ApplyTopologySettings(string presetName)
        {
            TopologyPreset preset = _topologyPresets[presetName];

            // Apply topology layers
            if (TerrainMeta.TopologyMap != null)
            {
                int width = Mathf.RoundToInt(TerrainMeta.Size.x);
                int height = Mathf.RoundToInt(TerrainMeta.Size.z);

                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        int topology = 0;
                        foreach (KeyValuePair<string, bool> layer in preset.Layers)
                        {
                            if (layer.Value)
                            {
                                topology |= GetTopologyID(layer.Key);
                            }
                        }
                        TerrainMeta.TopologyMap.SetTopology(x, y, topology);
                    }
                }
                TerrainMeta.TopologyMap.Push();
            }

            // Apply node spawn rates
            foreach (KeyValuePair<string, float> node in preset.NodeDensity)
            {
                _ = ConsoleSystem.Run(ConsoleSystem.Option.Server,
                    $"spawnpopulation.set \"{node.Key}\" {node.Value.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        private int GetTopologyID(string name)
        {
            return name.ToLower(CultureInfo.CurrentCulture) switch
            {
                "field" => 1 << 0,
                "cliff" => 1 << 1,
                "forest" => 1 << 2,
                // Add all other topology types
                _ => 0
            };
        }

        #endregion Utility Methods

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BiomeChanged"] = "Biome changed to {0}",
                ["TopologyChanged"] = "Topology changed to {0}",
                ["PermissionDenied"] = "You don't have permission to use this command",
                ["AnimalRateSet"] = "{0} spawn rate set to {1} in {2}",
                ["ResourceDensitySet"] = "{0} density set to {1} in {2}"
            }, this);
        }

        private string GetMessage(string key, string userId, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, lang.GetMessage(key, this, userId), args);
        }

        #endregion Localization
    }
}