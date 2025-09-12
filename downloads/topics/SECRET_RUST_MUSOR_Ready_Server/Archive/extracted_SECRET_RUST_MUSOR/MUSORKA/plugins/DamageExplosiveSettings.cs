using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("DamageExplosiveSettings", "XAVIER", "1.0.0")]
    public class DamageExplosiveSettings : RustPlugin
    {

        #region Var

        private static FieldInfo hookSubscriptions = typeof(PluginManager).GetField("hookSubscriptions", (BindingFlags.Instance | BindingFlags.NonPublic)); 

        #endregion
        
        #region InitializedHooks

        
        private void SubscribeInternalHook(string hook)
        {
            var hookSubscriptions_ = hookSubscriptions.GetValue(Interface.Oxide.RootPluginManager) as IDictionary<string, IList<Plugin>>;
			
            IList<Plugin> plugins;
            if (!hookSubscriptions_.TryGetValue(hook, out plugins))
            {
                plugins = new List<Plugin>();
                hookSubscriptions_.Add(hook, plugins);
            }
			
            if (!plugins.Contains(this))            
                plugins.Add(this);            
        }
        

        #endregion

        #region ChatCommand


        public List<ulong> _listDebugPlayer = new List<ulong>();


        [ChatCommand("getweaponprefab")]
        void GetPrefabName(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            if (_listDebugPlayer.Contains(player.userID))
            {
                player.ChatMessage("Вы отключили дебаг при попадании");
                _listDebugPlayer.Remove(player.userID);
                return;
            }
            _listDebugPlayer.Add(player.userID);
            player.ChatMessage("Вы включили дебаг от попадания");
        }

        #endregion
        
        
        #region Hooks


        void OnServerInitialized() => SubscribeInternalHook("IOnBasePlayerHurt");

        private object IOnBasePlayerHurt(BasePlayer player, HitInfo info)
        {
            if (player == null || player is NPCPlayer || info?.Initiator == null) return null;
            var initiator = info?.Initiator.ToPlayer();
            if (initiator == null) return null;
            if (info.WeaponPrefab == null) return null;
            string damagePrefab = info.WeaponPrefab.ShortPrefabName;

            if (_listDebugPlayer.Contains(player.userID))
            {
                PrintWarning($"[DEBUG РЕЖИМ] Название предмета, которого ранил игрок: {damagePrefab}");
            }
            
            float damageType;
            if (config._damageType.TryGetValue(damagePrefab, out damageType))
            {
                info.damageTypes.ScaleAll(damageType);
            }
            
            return null;
        }

        #endregion
        
        #region Configuration


        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            config = PluginConfig.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();

            if (config.PluginVersion < Version)
                UpdateConfigValues();

            Config.WriteObject(config, true);
        }

        private void UpdateConfigValues()
        {
            PluginConfig baseConfig = PluginConfig.DefaultConfig();
            if (config.PluginVersion < new VersionNumber(1, 0, 0))
            {
                PrintWarning("Config update detected! Updating config values...");
                PrintWarning("Config update completed!");
            }

            config.PluginVersion = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }




        private class PluginConfig
        {

            [JsonProperty("Настройка Damage ( shortname => damage ( 1.0 default )")]
            public Dictionary<string, float> _damageType = new Dictionary<string, float>();

            [JsonProperty("Версия конфигурации")] 
            public VersionNumber PluginVersion = new VersionNumber();

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    _damageType = new Dictionary<string, float>()
                    {
                        ["rocket_basic"] = 0.5f,
                        ["40mm_grenade_he"] = 0.5f,
                    },
                    PluginVersion = new VersionNumber(),
                };
            }
        }

        #endregion
    }
}