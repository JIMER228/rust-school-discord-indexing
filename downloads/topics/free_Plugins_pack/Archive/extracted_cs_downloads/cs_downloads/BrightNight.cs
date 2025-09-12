using System;
using Newtonsoft.Json;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Oxide.Plugins
{
    [Info("BrightNight", "RAREGUN▲", "1.0.0")]
	// 06D 08M 2019Y
	// BrightNight
	//    ___    ___    ___    ____  _____  __  __   _  __
	//   / _ \  / _ |  / _ \  / __/ / ___/ / / / /  / |/ /
	//  / , _/ / __ | / , _/ / _/  / (_ / / /_/ /  /    /
	// /_/|_| /_/ |_|/_/|_| /___/  \___/  \____/  /_/|_/  ^

    public class BrightNight : RustPlugin
	{
#region INITIALIZATION
		
		private ConfigFile config;

		public class ConfigFile
		{
			[JsonProperty(PropertyName = "Reference (Справка)")]
			public string Reference;
			[JsonProperty(PropertyName = "Morning lenght in minutes (Длина утра в минутах)")]
			public float morningLenght;
			[JsonProperty(PropertyName = "Day lenght in minutes (Длина дня в минутах)")]
			public float dayLenght;
			[JsonProperty(PropertyName = "Evening lenght in minutes (Длина вечера в минутах)")]
			public float eveningLenght;
			[JsonProperty(PropertyName = "Night lenght in minutes (Длина ночи в минутах)")]
			public float nightLenght;

			public ConfigFile()
			{
				if (Application.systemLanguage == SystemLanguage.Russian)
					Reference = "Соотношение Утра/Дня/Вечера/Ночи - 3/10/3/6. Установив значение 0 вы отключаете данное время суток.";
				else Reference = "The ratio of Morning/Day/Evening/Night is 3/10/3/6. By setting the value 0 you turn off this time of day.";

				morningLenght = 9f;
				dayLenght = 30f;
				eveningLenght = 9f;
				nightLenght = 24f;
			}
		}
		
		protected override void LoadDefaultConfig()
		{
			config = new ConfigFile();

			if (Application.systemLanguage == SystemLanguage.Russian)
			{
				Debug.LogError("Обнаружено отсутствие конфигурационного файла, создаю...");
				Debug.LogWarning($"Спасибо за приобретение \"{Name}\".");
				Debug.Log("Надеюсь на вашу оценку в случае вашего удовлетворения плагином, удачи!");
			}
			else
			{
				Debug.LogError("Found missing configuration file, creating...");
				Debug.LogWarning($"Thank you for purchasing \"{Name}\".");
				Debug.Log("I hope for your assessment if you are satisfied with the plugin, good luck!");
			}
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			config = Config.ReadObject<ConfigFile>();

			if (config == null)
			{
				if (Application.systemLanguage == SystemLanguage.Russian)
					Debug.LogError($"Обнаружено несоответствие в конфигурационном файле плагина \"{Name}\".\nДля восстановления удалите конфигурационный файл и загрузите плагин.");
				else Debug.LogError($"Discrepancy detected in configuration file of plugin \"{Name}\".\nTo restore, delete the configuration file and run the plugin.");
				error = true;

				NextTick(() => Server.Command($"o.unload {Name}"));
			}
			
			if (config.morningLenght == 0 && config.dayLenght == 0 && config.eveningLenght == 0 && config.nightLenght == 0)
			{
				if (Application.systemLanguage == SystemLanguage.Russian)
					Debug.LogError($"Увеличьте длительность хотя бы одного времени суток в конфигурации и запустите плагин заного!");
				else Debug.LogError($"Increase the duration of at least one time of day in the configuration and run plugin!");

				error = true;
				
				NextTick(() => Server.Command($"o.unload {Name}"));
			}
		}

		protected override void SaveConfig()
		{
			Config.WriteObject(config);
		}

#endregion
		
		private static int attemtps = 1;
		private static bool error;
		private static TOD_Time time;
		private static TOD_CycleParameters cycle;

		private void OnServerInitialized()
		{
			if (error) return;

			if (TOD_Sky.Instance == null)
			{
				++attemtps;
				timer.Once(attemtps > 30 ? 10 : 1, OnServerInitialized);
				
				return;
			}
			
			time = TOD_Sky.Instance.Components.Time;
			cycle = TOD_Sky.Instance.Cycle;
			
			time.ProgressTime = true;
			time.UseTimeCurve = false;
			time.DayLengthInMinutes = config.morningLenght;
			cycle.DateTime = new DateTime(2000, 4, 18, 6, 0, 0);

			time.OnDay += OnDay;
			time.OnHour += OnHour;
		}
		
		private void Unload()
		{
			if (error) return;
			
			time.OnDay -= OnDay;
			time.OnHour -= OnHour;
		}

		private void OnDay()
		{
			cycle.DateTime = new DateTime(2000, 4, 18, 0, 0, 0);
		}
		
		private void OnHour()
		{
			if (cycle.DateTime.Hour == 6)
			{
				if (config.morningLenght <= 0) cycle.Hour = 9;
				else time.DayLengthInMinutes = config.morningLenght;
			}
			else if (cycle.DateTime.Hour == 9)
			{
				if (config.dayLenght <= 0) cycle.Hour = 19;
				else time.DayLengthInMinutes = config.dayLenght;
			}
			else if (cycle.DateTime.Hour == 19)
			{
				if (config.eveningLenght <= 0) cycle.Hour = 22;
				else time.DayLengthInMinutes = config.eveningLenght;
			}
			else if (cycle.DateTime.Hour == 22)
			{
				if (config.nightLenght <= 0) cycle.Hour = 6;
				else time.DayLengthInMinutes = config.nightLenght;
			}
		}
	}
}