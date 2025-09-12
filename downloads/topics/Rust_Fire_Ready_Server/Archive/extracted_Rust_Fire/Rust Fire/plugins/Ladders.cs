using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Ladders", "Orange", "1.0.1")]
	[Description("Managing ladder's work")]
	public class Ladders : RustPlugin
	{
		#region Vars

		[PluginReference] private Plugin NoEscape;
		private const string shortname = "ladder.wooden.wall";

		#endregion

		#region Oxide Hooks

		private void Init()
		{
			lang.RegisterMessages(EN, this);
		}

		private void OnEntityBuilt(Planner planner, GameObject gameObject)
		{
			var entity = gameObject.ToBaseEntity() as BaseLadder;
			if (entity == null) return;

			var player = planner.GetOwnerPlayer();
			if (player == null) return;

			CheckLadder(player, entity);
		}

		#endregion

		#region Helper

		private void CheckLadder(BasePlayer player, BaseEntity entity)
		{
			var raid = NoEscape?.Call<bool>("IsRaidBlocked", player) ?? false;

			var allowed = raid ? config.allowInRaid : config.allowOutOfRaid;
			var needPrivilege = raid ? config.needPrivelegeInRaid : config.needPrivelegeOutOfRaid;

			if (allowed)
			{
				if (!player.CanBuild() && needPrivilege)
				{
					entity.Kill();
					player.inventory.GiveItem(ItemManager.CreateByName(shortname));
					message(player, "Privilege");
					return;
				}
				
				return;
			}

			entity.Kill();
			player.inventory.GiveItem(ItemManager.CreateByName(shortname));
			message(player, "Not now");
		}

		#endregion
		
		#region Configuration
        
		private static ConfigData config;
        
		private class ConfigData
		{    
			[JsonProperty(PropertyName = "1. Allow placing in raidblock")]
			public bool allowInRaid;
			
			[JsonProperty(PropertyName = "2. Allow placing out of raidblock")]
			public bool allowOutOfRaid;
			
			[JsonProperty(PropertyName = "3. Need privilege in raidblock")]
			public bool needPrivelegeInRaid;
			
			[JsonProperty(PropertyName = "4. Need privilege out of raidblock")]
			public bool needPrivelegeOutOfRaid;
		}
        
		private ConfigData GetDefaultConfig()
		{
			return new ConfigData 
			{
                allowInRaid = true,
				allowOutOfRaid = true,
				needPrivelegeInRaid = false,
				needPrivelegeOutOfRaid = false
			};
		}
        
		protected override void LoadConfig()
		{
			base.LoadConfig();
   
			try
			{
				config = Config.ReadObject<ConfigData>();
        
				if (config == null)
				{
					LoadDefaultConfig();
				}
			}
			catch
			{
				LoadDefaultConfig();
			}

			SaveConfig();
		}

		protected override void LoadDefaultConfig()
		{
			PrintError("Configuration file is corrupt(or not exists), creating new one!");
			config = GetDefaultConfig();
		}

		protected override void SaveConfig()
		{
			Config.WriteObject(config);
		}
        
		#endregion
		
		#region Localization

		private Dictionary<string, string> EN = new Dictionary<string, string>
		{
			{"Not now", "You can't do that now!"},
			{"Privilege", "You need building privilege to do that!"},
		};
        
		private string getMessage(string playerID, string key, params object[] args)
		{
			return string.Format(lang.GetMessage(key, this, playerID), args);
		}

		private void message(BasePlayer player, string key, params object[] args)
		{
			if (player == null) {return;}
			var message = getMessage(player.UserIDString, key, args);
			player.ChatMessage(message);
		}

		private void broadcast(string key, params object[] args)
		{
			var message = getMessage(null, key, args);
			Server.Broadcast(message);
		}

		#endregion
	}
}