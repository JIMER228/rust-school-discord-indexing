using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Metabolism", "Nimant", "1.0.1")]    
    public class Metabolism : RustPlugin
    {        

        #region Variables
		
		[PluginReference]
        private Plugin Friends;
        
        private const string permNone = "metabolism.none";
        private const string permSpawn = "metabolism.spawn"; 						// оставлен для совместимости
		
		private const string permFoodSpawn = "metabolism.food_spawn";				// полная еда после респавна
		private const string permAllSpawn = "metabolism.all_spawn";     			// полная еда и хп после респавна
		private const string permAllSpawnFriends = "metabolism.all_spawn_friends"; 	// полная еда и хп после респавна включая друзей
		
		#endregion
		
		#region Hooks

        private void Init()
        {            
            permission.RegisterPermission(permNone, this);
            permission.RegisterPermission(permSpawn, this);
			
			permission.RegisterPermission(permFoodSpawn, this);
			permission.RegisterPermission(permAllSpawn, this);
			permission.RegisterPermission(permAllSpawnFriends, this);
			
			LoadVariables();
        }
		
		private void OnServerInitialized() => timer.Once(0.5f, ()=> SetMaxMetabolism());
		
		private void OnPlayerRespawned(BasePlayer player)
        {
			if (player == null) return;
			
			if (permission.UserHasPermission(player.UserIDString, permAllSpawn) || permission.UserHasPermission(player.UserIDString, permAllSpawnFriends) || IsFriendHasPerm(player))
            {
                player.health = player.MaxHealth();
                player.metabolism.calories.value = configData.CaloriesSpawnAmount;
                player.metabolism.hydration.value = configData.HydrationSpawnAmount;
            }
			else
				if (permission.UserHasPermission(player.UserIDString, permSpawn) || permission.UserHasPermission(player.UserIDString, permFoodSpawn))
				{
					player.health = configData.HealthSpawnAmount;
					player.metabolism.calories.value = configData.CaloriesSpawnAmount;
					player.metabolism.hydration.value = configData.HydrationSpawnAmount;
				}
        }        

        #endregion        
		
		#region Main
		
		private void SetMaxMetabolism()
		{
			foreach(var player in BasePlayer.activePlayerList.Where(x=> permission.UserHasPermission(x.UserIDString, permNone)))
			{
				player.metabolism.calories.value = player.metabolism.calories.max;
                player.metabolism.hydration.value = player.metabolism.hydration.max;
			}
			
			timer.Once(configData.RefreshMaxMetabolism, SetMaxMetabolism);
		}
		
		private bool IsFriendHasPerm(BasePlayer player)
		{						
			if (player.currentTeam > 0)
			{
				var team = BasePlayer.activePlayerList.Where(x=> x != null && x.currentTeam == player.currentTeam).Select(x=> x.UserIDString).ToList();
				team.AddRange(BasePlayer.sleepingPlayerList.Where(x=> x != null && x.currentTeam == player.currentTeam).Select(x=> x.UserIDString).ToList());
				
				if (team.Exists(x=> permission.UserHasPermission(x, permAllSpawnFriends)))
					return true;
			}
			
			if (Friends == null)
				return false;
			
			var friends = (string[]) Friends.Call("GetFriendsS", player.UserIDString);
			if (friends == null)
				return false;
			
			return Array.Exists(friends, x=> permission.UserHasPermission(x, permAllSpawnFriends));
		}
		
		#endregion
		
		#region Config        				
		
        private static ConfigData configData;
		
        private class ConfigData
        {            
			[JsonProperty(PropertyName = "Величина здоровья при спавне (0.0 - 100.0)")]
            public float HealthSpawnAmount;
			
            [JsonProperty(PropertyName = "Число калорий при спавне (0.0 - 500.0)")]
            public float CaloriesSpawnAmount;            

            [JsonProperty(PropertyName = "Величина жажды при спавне (0.0 - 250.0)")]
            public float HydrationSpawnAmount;
			
			[JsonProperty(PropertyName = "Частота установки максимальных калорий (секунды)")]
            public float RefreshMaxMetabolism;
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                HealthSpawnAmount = 100f,
				CaloriesSpawnAmount = 500f,
				HydrationSpawnAmount = 250f,
				RefreshMaxMetabolism = 4f
            };
            SaveConfig(configData);
			timer.Once(0.1f, ()=> SaveConfig(configData));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
    }
}
