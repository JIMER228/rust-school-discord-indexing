using System.Text.RegularExpressions;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;
using System;
using Newtonsoft.Json;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("WipeInfo", "https://discord.gg/9vyTXsJyKR", "1.0.0")]    
    internal class WipeInfo : RustPlugin
    {        
        
		#region Variables
		
		private class WipeData
		{
			public string LastNormalWipeDt;
			public string LastGlobalWipeDt;
			public string NextNormalWipeDt;
			public string NextGlobalWipeDt;
		}				
		
		#endregion
		
		#region Hooks
		
		private void Init() 
		{
			LoadData();
			LoadVariables();
			
			if (configData.WipeHour == 0)
			{
				configData.WipeHour = 14;
				SaveConfig(configData);
			}
			
			if (configData.GlWipes == null)
			{
				configData.GlWipes = new List<string>();
				SaveConfig(configData);
			}
		}
		
		private void OnNewSave() => FillDataFile(DateTime.Now);        
		
		#endregion
		
		#region Helpers
		
		private static string GetDtStr(DateTime dt) => dt.ToString("d MMMM", CultureInfo.CreateSpecificCulture("ru-RU"));
		
		#endregion
		
		#region API
		
		string API_GetLastGlobalWipe() => data.LastGlobalWipeDt;
		
		string API_GetNextGlobalWipe() => data.NextGlobalWipeDt;
		
		string API_GetLastWipe() => data.LastNormalWipeDt;
		
		string API_GetNextWipe() => data.NextNormalWipeDt;
		
		#endregion
		
		#region Some Custom API
		
		string GetNextGlobalWipe()
		{
			return $"Следующий глобальный вайп: {data.NextGlobalWipeDt}";
		}
		
		string GetLastWipeInfo()
		{
			var dt = DateTime.Now;
			
			for(int ii=0;ii<35;ii++)
			{
				var dtStr = GetDtStr(dt);
				
				if (dtStr == data.LastGlobalWipeDt)
					return $"Вайп был <color=#98b8ff>{data.LastGlobalWipeDt}</color> (вайп карты и чертежей)";
			
				if (dtStr == data.LastNormalWipeDt)
					return $"Вайп был <color=#98b8ff>{data.LastNormalWipeDt}</color> (вайп карты)";
				
				dt = dt.AddDays(-1);
			}
			
			return $"Вайп был <color=#98b8ff>{data.LastNormalWipeDt}</color> (вайп карты)";
		}
		
		string GetNextWipeInfo()
		{
			var dt = DateTime.Now;
			
			for(int ii=0;ii<35;ii++)
			{
				var dtStr = GetDtStr(dt);
				
				if (dtStr == data.NextGlobalWipeDt)
					return $"Следующий вайп будет <color=#FF6c6c>{data.NextGlobalWipeDt}</color> (вайп карты и чертежей)";
			
				if (dtStr == data.NextNormalWipeDt)
					return $"Следующий вайп будет <color=#FF6c6c>{data.NextNormalWipeDt}</color> (вайп карты)";
				
				dt = dt.AddDays(1);
			}
			
			return $"Следующий вайп будет <color=#FF6c6c>{data.NextNormalWipeDt}</color> (вайп карты)";
		}
		
		#endregion
		
		#region Commands
		
		[ConsoleCommand("wi.reset")]
        private void cmdResetData(ConsoleSystem.Arg args)
        {
            if (args.Connection != null) return;						
			
			var dt = GetLastWipeDt();
			FillDataFile(dt);
			
			Puts($@"Данные о датах вайпа были пересозданы для даты {dt.ToString("dd.MM.yyyy")}.");
		}
		
		#endregion
		
		#region Main
				
		private static DateTime GetLastWipeDt()
		{
			var lNW = data.LastNormalWipeDt;
			var lGW = data.LastGlobalWipeDt;
			
			if (string.IsNullOrEmpty(lNW) || string.IsNullOrEmpty(lGW))
				return default(DateTime);
			
			var lNW_ = DateTime.ParseExact(lNW, "d MMMM", CultureInfo.CreateSpecificCulture("ru-RU"));
			var lGW_ = DateTime.ParseExact(lGW, "d MMMM", CultureInfo.CreateSpecificCulture("ru-RU"));						
			
			if (lNW_ > lGW_)
				return lNW_;
			
			return lGW_;
		}
				
		private static DateTime GetLastDevGlobalWipe(DateTime dt)
		{
			var currDt = DateTime.Now;
			var wipeDt = default(DateTime);
            var currMonth = dt.Month;

            while (true)
            {
                if (dt.Month != currMonth)
                {
                    if (wipeDt == default(DateTime))
                        currMonth = dt.Month;
                    else
                        return wipeDt.AddDays(1);
                }								

                if ( (dt.Hour >= configData.WipeHour && dt.ToString("dd.MM.yyyy") == currDt.ToString("dd.MM.yyyy") || dt.ToString("dd.MM.yyyy") != currDt.ToString("dd.MM.yyyy")) && (int)dt.DayOfWeek == 4 ) wipeDt = dt;

                dt = dt.AddDays(-1);
            }
		}
		
		private static DateTime GetNextDevGlobalWipe(DateTime dt)
		{
			var lastDGW = GetLastDevGlobalWipe(dt);
			return GetLastDevGlobalWipe(lastDGW.AddDays(45));
		}
		
		private static Dictionary<DateTime, bool> GetMonthWipeList(DateTime dt)
		{
			var result = new Dictionary<DateTime, bool>();
			
			var lastDGW = GetLastDevGlobalWipe(dt);
			var nextDGW = GetNextDevGlobalWipe(dt);
			var days = (nextDGW-lastDGW).Days + 1;						
			var indexDt = lastDGW;
			int cnt = 0;
			
			for (int ii = 0; ii < days; ii++)
			{
				if (indexDt == lastDGW || indexDt == nextDGW || configData.GlWipes.Contains(indexDt.ToString("dd.MM.yyyy")))
					result.Add(indexDt, true);
				
				if (ii > 0 && ii < days - 1)
				{
					var day = (int)indexDt.DayOfWeek;
					day = day == 0 ? 7 : day;
					
					if (day == configData.WipeDay) 
					{
						if (!result.ContainsKey(indexDt))
							result.Add(indexDt, false);
						
						cnt++;
						
						if (cnt == 2 || cnt == 4 && (nextDGW-indexDt).Days > 7) 
							result[indexDt] = true;
					}
				}
				
				indexDt = indexDt.AddDays(1);
			}
			
			return result;
		}
		
		private static void FillDataFile(DateTime dt)
		{		
			var dtNow = DateTime.ParseExact(dt.ToString("dd.MM.yyyy"), "dd.MM.yyyy", CultureInfo.CreateSpecificCulture("ru-RU"));
			var wipes = GetMonthWipeList(dtNow.AddHours(configData.WipeHour));
			
			data.LastNormalWipeDt = "";
			data.LastGlobalWipeDt = "";
			data.NextNormalWipeDt = "";
			data.NextGlobalWipeDt = "";
			
			foreach (var pair in wipes.OrderBy(x=> x.Key))
			{				
				var dtIndex = DateTime.ParseExact(pair.Key.ToString("dd.MM.yyyy"), "dd.MM.yyyy", CultureInfo.CreateSpecificCulture("ru-RU"));
				
				if (dtIndex > dtNow)
				{
					if (pair.Value && string.IsNullOrEmpty(data.NextGlobalWipeDt))
						data.NextGlobalWipeDt = GetDtStr(pair.Key);
					
					if (!pair.Value && string.IsNullOrEmpty(data.NextNormalWipeDt))
						data.NextNormalWipeDt = GetDtStr(pair.Key);
				}								
			}
			
			foreach (var pair in wipes.OrderByDescending(x=> x.Key))
			{				
				var dtIndex = DateTime.ParseExact(pair.Key.ToString("dd.MM.yyyy"), "dd.MM.yyyy", CultureInfo.CreateSpecificCulture("ru-RU"));
				
				if (dtIndex <= dtNow)
				{
					if (pair.Value && string.IsNullOrEmpty(data.LastGlobalWipeDt))
						data.LastGlobalWipeDt = GetDtStr(pair.Key);
					
					if (!pair.Value && string.IsNullOrEmpty(data.LastNormalWipeDt))
						data.LastNormalWipeDt = GetDtStr(pair.Key);
				}
			}
			
			if (string.IsNullOrEmpty(data.NextNormalWipeDt))
				data.NextNormalWipeDt = data.NextGlobalWipeDt;
			
			if (string.IsNullOrEmpty(data.LastNormalWipeDt))
				data.LastNormalWipeDt = data.LastGlobalWipeDt;

			SaveData();
		}								
		
		#endregion
		
		#region Config
		
        private static ConfigData configData;
		
        private class ConfigData
        {            
			[JsonProperty(PropertyName = "День недели, когда происходит вайп (номер, где 1 = ПН)")]
			public int WipeDay;
			[JsonProperty(PropertyName = "Час дня, когда происходит вайп")]
			public int WipeHour;
			[JsonProperty(PropertyName = "Глобальный вайп каждые 2 недели (исключая последнюю неделю до глобала)")]
			public bool TwoWeeksGlobal;
			[JsonProperty(PropertyName = "Свой список глобальных вайпов (исключая основной глобальный вайп)")]
			public List<string> GlWipes;
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                WipeDay = 5,
				WipeHour = 14,
				TwoWeeksGlobal = true,
				GlWipes = new List<string>()
            };
            SaveConfig(configData);
			timer.Once(0.1f, ()=> SaveConfig(configData));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
		
		#region Data
		
		private static WipeData data;
		
		private void LoadData() => data = Interface.GetMod().DataFileSystem.ReadObject<WipeData>("WipeInfoData");					
		
		private static void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("WipeInfoData", data);		
		
		#endregion
		
    }
}