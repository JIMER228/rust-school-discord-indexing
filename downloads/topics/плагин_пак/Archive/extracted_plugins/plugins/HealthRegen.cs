using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("HealthRegen", "Excellion", "1.0.2")]
    [Description("Regenerates health over time")]

    class HealthRegen : RustPlugin
    {
		
		
		private Configuration _config;
		private const string HealthRegenPermission = "healthregen.use";
		
		#region Config

        private class Configuration
        {
            [JsonProperty(PropertyName = "Сколько ХП будет регенерировать привелегия в секунду", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly Dictionary<string, float> PermList = new Dictionary<string, float>
            {
                ["HealthRegen.default"] = 1.0f,
                ["HealthRegen.vip"] = 2.0f,
                ["HealthRegen.cezar"] = 3.0f, 
                ["HealthRegen.admin"] = 10.0f
            };
         
        }
		
		 protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }
		
		protected override void SaveConfig()
        {
            
			Config.WriteObject(_config);
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }
		
		#endregion
		
		
		private float GetMaxPermissionValue(string userID)
        {
            float protect = 0;
            foreach (var check in _config.PermList)
                if (permission.UserHasPermission(userID, check.Key))
                    protect = Math.Max(protect, check.Value);
            return protect;
        }
			
		
		
		

		
		
		 private void OnServerInitialized()
		 {
            foreach (var check in _config.PermList) permission.RegisterPermission(check.Key, this);   
			permission.RegisterPermission(HealthRegenPermission, this); 
		 }
		 
		 private void OnPlayerConnected(BasePlayer player)
		 {
			timer.Repeat(1f, 0, () => RegenerateHealth(player));
		 }
      

        private void RegenerateHealth(BasePlayer player)
        {
            if (player.IsConnected &&  player.health < player.MaxHealth() && player.health > 0)
            {
                if (permission.UserHasPermission(player.UserIDString, HealthRegenPermission) && GetMaxPermissionValue(player.UserIDString) > 0)
				{
					player.Heal(GetMaxPermissionValue(player.UserIDString));
				}
				
            }
        }
    }
}