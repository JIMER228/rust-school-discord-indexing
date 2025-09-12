using Facepunch;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Nav Killer", "nivex", "0.1.5")]
    [Description("Kills npcs that fail to spawn on the navmesh, and hides annoying debug messages.")]
    class NavKiller : RustPlugin
    {
        [PluginReference] Plugin ConsoleFilter;

        private List<string> messages = new List<string>
        {
            "can only be called on an active agent that has been placed on a NavMesh",
            "failed to sample navmesh at position",
            "Found null entries in the RF listener list for frequency",
            "Kinematic body only supports",
            "Bone error in SkeletonProperties.BuildDictionary for ?"
        };

        private List<string> _animals = new List<string>
        {
            "bear", "boar", "chicken", "horse", "shark", "stag", "wolf"
        };

        private void Init()
        {
            UnityEngine.Application.logMessageReceived -= Output.LogHandler;
            UnityEngine.Application.logMessageReceived += LogHandler;
        }

        private void Unload()
        {
            UnityEngine.Application.logMessageReceived -= LogHandler;
            UnityEngine.Application.logMessageReceived += Output.LogHandler;
        }

        private void LogHandler(string message, string stackTrace, LogType type) 
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            if (messages.Exists(message.Contains))
            {
                TryKillNpc(message);
            }
            else if (ConsoleFilter == null)
            {
                Output.LogHandler(message, stackTrace, type);
            }
        }

        private void TryKillNpc(string message)
        {
            int i, n;

            if ((i = message.IndexOf('(')) == -1 || (n = message.IndexOf(')')) == -1)
            {
                return;
            }

            bool canKill = false;

            if (config.Other && !message.Contains("junkpile") && !message.Contains("animal"))
            {
                canKill = true;
            }

            if (config.Junkpile && message.Contains("junkpile"))
            {
                canKill = true;
            }

            if (config.Animals && _animals.Exists(message.Contains))
            {
                canKill = true;
            }

            if (!canKill)
            {
                return;
            }

            var position = message.Substring(i, n - i + 1).ToVector3();
            var entities = Pool.GetList<BaseEntity>();

            Vis.Entities(position, 1f, entities);

            foreach (var entity in entities)
            {
                if (entity.IsNpc)
                {
                    entity.Kill();
                    break;
                }
            }

            Pool.FreeList(ref entities);
        }

        #region Configuration

        private Configuration config;
        
        public class Configuration
        {
            [JsonProperty(PropertyName = "Kill Junkpile Scientists")]
            public bool Junkpile { get; set; }

            [JsonProperty(PropertyName = "Kill Other Scientists")]
            public bool Other { get; set; } = true;

            [JsonProperty(PropertyName = "Kill Animals")]
            public bool Animals { get; set; } = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
                SaveConfig();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadDefaultConfig() => config = new Configuration();

        #endregion Configuration
    }
}