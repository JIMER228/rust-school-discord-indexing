using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("NSupplySpeed", "North", "0.0.1")]
	class NSupplySpeed : RustPlugin
	{
		private ConfigData cfg;

		private void Loaded() => ReadConfig();

		private void OnEntitySpawned(SupplyDrop entity)
		{
			entity.GetComponent<Rigidbody>().drag /= cfg.SupplyDropSpeed;
		}

		class ConfigData
		{
			[JsonProperty("Скорость падения дропа: ")]
			public float SupplyDropSpeed = 10f;
		}
		protected override void LoadDefaultConfig()
		{
			var config = new ConfigData();
			SaveConfig(config);
		}
		void SaveConfig(object config)
		{
			Config.WriteObject(config, true);
		}
		void ReadConfig()
		{
			base.Config.Settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
			cfg = Config.ReadObject<ConfigData>();
			SaveConfig(cfg);
		}
	}
}