// https://static.imperialplugins.com/Legal/LICENSE.txt

// For personal use only, not to be copied, distributed, altered or sold.
// ImperialPlugins.com

using System;
using Newtonsoft.Json;
using Oxide.Core.Configuration;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Simple Airdrop", "Dana", "0.0.3")]
    [Description("Modifies the Airdrop fall speed.")]
    public class SimpleAirdrop : RustPlugin
    {
        private PluginConfig _pluginConfig;
        protected override void LoadConfig()
        {
            var configPath = $"{Manager.ConfigPath}/{Name}.json";
            var newConfig = new DynamicConfigFile(configPath);
            if (!newConfig.Exists())
            {
                LoadDefaultConfig();
                newConfig.Save();
            }
            try
            {
                newConfig.Load();
            }
            catch (Exception ex)
            {
                RaiseError("Failed to load config file (is the config file corrupt?) (" + ex.Message + ")");
                return;
            }

            newConfig.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = newConfig.ReadObject<PluginConfig>();
            if (_pluginConfig.Config == null)
            {
                _pluginConfig.Config = new SimpleAirdropConfig
                {
                    AirResistance = 2
                };
            }

            newConfig.WriteObject(_pluginConfig);
            PrintWarning("Config Loaded");
        }
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        private void OnEntitySpawned(SupplyDrop entity)
        {
            if (_pluginConfig.Config.AirResistance <= 0)
            {
                entity.RemoveParachute();
                return;
            }
            var rigidbody = entity.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.drag = _pluginConfig.Config.AirResistance < 0.6f ? 0.6f : _pluginConfig.Config.AirResistance;
            }
        }
        void OnAirdrop(CargoPlane plane, Vector3 dropPosition)
        {
             plane.secondsToTake = Vector3.Distance(plane.startPos, plane.endPos) / _pluginConfig.Config.PlaneSpeed;
        }
        private class PluginConfig
        {
            public SimpleAirdropConfig Config { get; set; }
        }
        private class SimpleAirdropConfig
        {
            [JsonProperty(PropertyName = "Supply Drop Air Resistance (Default 2)")]
            public float AirResistance { get; set; } = 2f;
            [JsonProperty(PropertyName = "Plane Speed (Default 50)")]
            public float PlaneSpeed { get; set; } = 50f;
        }
    }
}