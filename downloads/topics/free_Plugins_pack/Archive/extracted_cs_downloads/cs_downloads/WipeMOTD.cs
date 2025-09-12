using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Plugins;

namespace Oxide.Plugins 
{ 
	[Info("Wipe MOTD", "Hol0ZeN", "1.0.1")] 
	public class WipeMOTD : RustPlugin
	{
		#region Configuration

		private Configuration config = new Configuration();

		protected override void LoadConfig()
		{
			base.LoadConfig();

			try
			{
				config = Config.ReadObject<Configuration>();
				if (config == null) throw new Exception();
			}
			catch
			{
				LoadDefaultConfig();
			}
		}

		protected override void SaveConfig() => Config.WriteObject(config);

		protected override void LoadDefaultConfig()
		{
			config = new Configuration();
			Config.WriteObject(config);
		}

		private class Configuration
		{
			[JsonProperty("Включить переименование")]
			public bool Enable { get; set; } = true;
			
			[JsonProperty("Название сервера")]
			public string MOTD { get; set; } = "SERVER NAME [Wiped: {DATE}]";

			[JsonProperty("Формат даты")]
			public string DataFormat { get; set; } = "dd-MM";

			[JsonProperty("Дата последнего вайпа")]
			public DateTime LastWipe { get; set; } = DateTime.MinValue;
		}

		#endregion Configuration

		#region Hooks
		
		private void OnServerInitialized()
		{
			LoadConfig();
			
			string MOTD = config.MOTD.Replace("{DATE}", config.LastWipe.ToString(config.DataFormat));

			if (ConVar.Server.hostname != MOTD && config.LastWipe != DateTime.MinValue)
				UpdateMOTD();
		}
		
		private void OnNewSave() => SetWipe(DateTime.Now);
		
		#endregion Hooks

		#region API

		[HookMethod("SetWipe")]
		private void SetWipe(DateTime time)
		{
			config.LastWipe = time;
			SaveConfig();
			
			if (config.LastWipe != DateTime.MinValue)
				UpdateMOTD();
		}

		[HookMethod("UpdateMOTD")]
		private void UpdateMOTD()
		{
			if (!config.Enable) return;
			
			FUpdateMOTD();
		}

		[HookMethod("FUpdateMOTD")]
		private void FUpdateMOTD()
		{
			ConVar.Server.hostname = config.MOTD.Replace("{DATE}", config.LastWipe.ToString(config.DataFormat));
			ConsoleSystem.Run(ConsoleSystem.Option.Server, "writecfg");
		}

		#endregion API
	}
}