using Newtonsoft.Json;
using System.Collections.Generic;

namespace Oxide.Plugins
{
	[Info("kryNickReward", "xkrystalll", "1.0.1")]
	class kryNickReward : RustPlugin
	{
		private ConfigData cfg;
		public class ConfigData
		{
			[JsonProperty("Включить награду за ник?")]
			public bool IsEnabled;

			[JsonProperty("Включить выдачу прав?")]
			public bool EnablePermissionGrant;

			[JsonProperty("Включить выдачу групп?")]
			public bool EnableGroupGrant;

			[JsonProperty("Какие права выдавать, если в нике есть слова ниже? (может быть пустым)")]
			public List<string> PermissionsToGrant;

			[JsonProperty("Какие группы выдавать, если в нике есть слова ниже? (может быть пустым)")]
			public List<string> GroupsToGrant;

			[JsonProperty("Какие должны быть слова в нике?")]
			public List<string> Prefixes;

			[JsonProperty("Забирать права, если человек зашёл без приставки?")]
			public bool RevokePermWithoutPrefix;
		}

		protected override void LoadDefaultConfig()
		{
			var config = new ConfigData
			{
				IsEnabled = true,
				EnablePermissionGrant = true,
				EnableGroupGrant = true,
				PermissionsToGrant = new()
				{
					"permission.one",
					"permission.two"
				},
				GroupsToGrant = new()
				{
					"group1",
					"group2"
				},
				Prefixes = new()
				{
					"testplugin",
					"topserver"
				},
				RevokePermWithoutPrefix = true
			};
			SaveConfig(config);
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			cfg = Config.ReadObject<ConfigData>();
			SaveConfig(cfg);
		}

		private void SaveConfig(object config)
		{
			Config.WriteObject(config, true);
		}


		private void OnServerInitialized()
		{
			PrintWarning($"Initialized {cfg.Prefixes.Count} prefixes!");
		}

		private void OnPlayerConnected(BasePlayer player)
		{
			if (!cfg.IsEnabled)
				return;

			var nickname = player.displayName.ToLower();

			foreach (var prefix in cfg.Prefixes)
			{
				if (nickname.Contains(prefix.ToLower()))
				{
					if (cfg.EnableGroupGrant)
						foreach (var x in cfg.GroupsToGrant)
							player.IPlayer.AddToGroup(x);

					if (cfg.EnablePermissionGrant)
						foreach (var x in cfg.PermissionsToGrant)
							player.IPlayer.GrantPermission(x);
					return;
				}
			}

			if (cfg.RevokePermWithoutPrefix)
			{
				foreach (var x in cfg.PermissionsToGrant)
					player.IPlayer.RevokePermission(x);

				foreach (var x in cfg.GroupsToGrant)
					player.IPlayer.RemoveFromGroup(x);
			}
		}
	}
}