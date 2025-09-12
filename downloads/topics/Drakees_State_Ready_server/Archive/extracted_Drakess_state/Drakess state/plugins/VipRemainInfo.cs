using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("VipRemainInfo", "Nimant", "1.0.4", ResourceId = 0)]
    class VipRemainInfo : RustPlugin
    {            		
				
		#region Variables 		
		
		[PluginReference]
		private Plugin Grant;

		private Dictionary<ulong, bool>	ShowedVipInfo = new Dictionary<ulong, bool>();				
		
		#endregion
		
		#region Hooks
		
		private void OnServerInitialized()
		{
			if (Grant == null)			
				PrintWarning("Не найден плагин 'Grant'. Работа плагина невозможна !");											
		}
				
		private void OnPlayerConnected(BasePlayer player)
        {
			if (player == null) return;
			
            if (!ShowedVipInfo.ContainsKey(player.userID)) 
				ShowedVipInfo.Add(player.userID, false);
			else
				ShowedVipInfo[player.userID] = false;
		}	
		
		private void OnPlayerSleepEnded(BasePlayer player)
        {					
			if (player == null) return;
			
            if (!ShowedVipInfo.ContainsKey(player.userID)) return;
			
			if (!ShowedVipInfo[player.userID])
				timer.Once(2f, () => ShowRemainVipInfo(player));            
        }
		
		#endregion
		
		#region Main
		
		private void ShowRemainVipInfo(BasePlayer player)
		{
			if (Grant == null) return;			
			var groupInfo = (Dictionary<string, int>)(Grant.CallHook("GetGroups", player.userID));
				
			if (groupInfo != null)
			{
				string color = "";
				bool showHead = false;
				string result = "";
				
				foreach(var info in groupInfo)
				{					
					if (!showHead)
					{						
						result += configData.HeadText + "\n";
						showHead = true;
					}	
					
					var name = info.Key;
					if (configData.GroupsNewName != null && configData.GroupsNewName.ContainsKey(info.Key))					
						name = configData.GroupsNewName[info.Key];						
					
					if (configData.GroupsColor.TryGetValue(info.Key, out color))						
						result += string.Format(configData.VipText, color, name.ToUpper(), GetTime(info.Value)) + "\n";
					else
						if (configData.GroupsColor.TryGetValue("default", out color))							
							result += string.Format(configData.VipText, color, name.ToUpper(), GetTime(info.Value)) + "\n";
						else
							result += string.Format(configData.VipText, "white", name.ToUpper(), GetTime(info.Value)) + "\n";							
				}	
				
				if (!string.IsNullOrEmpty(result))
					SendReply(player, result.TrimEnd('\n'));
			}
			
			ShowedVipInfo[player.userID] = true;
		}		
		
		private string GetTime(int rawSeconds)
		{							
			int days    = (int)Math.Truncate((((decimal)rawSeconds/60)/60)/24);
			int hours   = (int)Math.Truncate((((decimal)rawSeconds-days*24*60*60)/60)/60);
			int minutes = (int)Math.Truncate((((decimal)rawSeconds-days*24*60*60)/60)%60);			
			
			string time = "";
		
			if (days!=0)
				time += $"{days}д ";			
			if (hours!=0)
				time += $"{hours}ч ";			
			if (minutes!=0)
				time += $"{minutes}м ";			
			
			if (string.IsNullOrEmpty(time))
				time = "несколько секунд";
			
			return time;
		}
		
		#endregion
		
		#region Config   

		private void Loaded() => LoadConfigVariables();
		
		private ConfigData configData;
		
        private class ConfigData
        {
            public Dictionary<string, string> GroupsColor;		
			public Dictionary<string, string> GroupsNewName;		
			public string HeadText;
			public string VipText;
        }				                        
		
        private void LoadDefaultConfig()
        {
			var config = new ConfigData
            {
				GroupsColor = new Dictionary<string, string>()
				{
					{"epic", "#0FED02"},	
					{"heroic", "#A202ED"},
					{"legend", "#FF170A"},
					{"default", "#FFA500"}
				},
				GroupsNewName = new Dictionary<string, string>()
				{
					{"epic", "epic"},	
					{"heroic", "heroic"},
					{"legend", "legend"},
					{"xtrm_insta", "instacraft"},
					{"xtrm_x5", "x5"}
				},
				HeadText = "Остаток времени у ваших привилегий:",
				VipText = "Привилегия <color={0}>{1}</color> - осталось {2}"
            };
            SaveConfig(config);
        }
        
		private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        
		private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion

    }	
	
}