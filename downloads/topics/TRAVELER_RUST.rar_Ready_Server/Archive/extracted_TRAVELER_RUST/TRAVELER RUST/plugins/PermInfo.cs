using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("PermInfo", "TopPlugin.ru", "1.0.0")]
    class PermInfo : RustPlugin
    {            		
				
		#region Variables 		
		
		[PluginReference]
		private Plugin Grant;

		private static Dictionary<ulong, bool> ShowInfo = new Dictionary<ulong, bool>();				
		
		#endregion
		
		#region Hooks
		
		private void OnServerInitialized()
		{
            PrintWarning("\n-----------------------------\n" +
            "     Author - https://topplugin.ru/\n" +
            "     VK - https://vk.com/rustnastroika\n" +
            "     Discord - https://discord.com/invite/5DPTsRmd3G\n" +
            "-----------------------------");
			ShowInfo.Clear(); 
			
			if (Grant == null)
				PrintWarning("Не найден плагин 'Grant'. Работа плагина невозможна !");											
		}
				
		private void OnPlayerConnected(BasePlayer player)
        {
			if (player == null) return;
			
            if (!ShowInfo.ContainsKey(player.userID)) 
				ShowInfo.Add(player.userID, false);
			else
				ShowInfo[player.userID] = false;
		}	
		
		private void OnPlayerSleepEnded(BasePlayer player)
        {					
			if (player == null || !ShowInfo.ContainsKey(player.userID) || ShowInfo[player.userID]) return;			            						
			timer.Once(1f, ()=> ShowPermInfo(player));
        }
		
		#endregion
		
		#region Main
		
		private bool ShowPermInfo(BasePlayer player)
		{
			if (Grant == null || player == null) return false;
			
			var groups = Grant.CallHook("GetGroups", player.userID) as Dictionary<string, int>;
			var privs = Grant.CallHook("GetPermissions", player.userID) as Dictionary<string, int>;
				
			var all = new Dictionary<string, int>();
				
			if (groups != null)
				foreach (var group in groups)
					foreach (var groupCfg in configData.Groups)					
						if (groupCfg.Privs.Contains(group.Key.ToLower()))
						{
							if (!all.ContainsKey(groupCfg.GroupName))
								all.Add(groupCfg.GroupName, group.Value);
							else
								all[groupCfg.GroupName] = all[groupCfg.GroupName] > group.Value ? group.Value : all[groupCfg.GroupName];
						}									
			
			if (privs != null)
				foreach (var priv in privs)				
					foreach (var groupCfg in configData.Groups)					
						if (groupCfg.Privs.Contains(priv.Key.ToLower()))
						{
							if (!all.ContainsKey(groupCfg.GroupName))
								all.Add(groupCfg.GroupName, priv.Value);
							else
								all[groupCfg.GroupName] = all[groupCfg.GroupName] > priv.Value ? priv.Value : all[groupCfg.GroupName];
						}									
							
			var showHead = false;
			var result = "";
			
			foreach(var info in all.OrderBy(x=> x.Key))
			{					
				if (!showHead)
				{						
					result += configData.TitleText + "\n";
					showHead = true;
				}	
								
				result += string.Format(configData.PermText, info.Key.ToUpper(), GetTime(info.Value)) + "\n";							
			}	
			
			var flag = false;
			
			if (!string.IsNullOrEmpty(result))
			{
				SendReply(player, result.TrimEnd('\n'));			
				flag = true;
			}
			
			ShowInfo[player.userID] = true;
			
			return flag;
		}		
		
		private static string GetTime(int rawSeconds)
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
		
		#region Commands
		
		[ChatCommand("privs")]
        private void cmdPrivs(BasePlayer player, string command, string[] args)
        {
			var result = ShowPermInfo(player);
				
			if (!result)
				SendReply(player, "У вас нет никаких привилегий.");	
		}
		
		#endregion
		
		#region Config   

		private void Init() => LoadVariables();
		
		private static ConfigData configData;
		
        private class ConfigData
        {			
			[JsonProperty(PropertyName = "Заголовок сообщения")]
			public string TitleText;
			[JsonProperty(PropertyName = "Текст строки группы")]
			public string PermText;
			[JsonProperty(PropertyName = "Список групп")]
            public List<Group> Groups;
		}
		
		private class Group
		{
			[JsonProperty(PropertyName = "Название виртуальной группы")]
			public string GroupName;
			[JsonProperty(PropertyName = "Список привилегий входящих в группу")]
			public List<string> Privs;
		}
		
        private void LoadDefaultConfig()
        {
			configData = new ConfigData
            {
				Groups = new List<Group>()
				{
					new Group() 
					{
						GroupName = "Elite привилегия",
						Privs = new List<string>() { "kits.elite" }
					},
					new Group() 
					{
						GroupName = "Premium привилегия",
						Privs = new List<string>() { "kits.premium" }
					},
					new Group() 
					{
						GroupName = "Nitro привилегия",
						Privs = new List<string>() { "kits.nitro" }
					},
					new Group() 
					{
						GroupName = "Vip привилегия",
						Privs = new List<string>() { "kits.vip" }
					},
					new Group() 
					{
						GroupName = "Lite привилегия",
						Privs = new List<string>() { "kits.lite" }
					},
					new Group() 
					{
						GroupName = "Скины",
						Privs = new List<string>() { "skins.change" }
					},
					new Group() 
					{
						GroupName = "Вход без очереди",
						Privs = new List<string>() { "queue.access" }
					},
					new Group() 
					{
						GroupName = "Уведомление о рейде",
						Privs = new List<string>() { "raidalerts.vk" }
					},
					new Group() 
					{
						GroupName = "Раскраска табличек",
						Privs = new List<string>() { "artist.premium" }
					},
					new Group() 
					{
						GroupName = "Метаболизм",
						Privs = new List<string>() { "metabolism.spawn" }
					},
					new Group() 
					{
						GroupName = "Неломаемые предметы",
						Privs = new List<string>() { "itemcondition.all.items" }
					},
					new Group() 
					{
						GroupName = "Большой рюкзак",
						Privs = new List<string>() { "backpacks.large" }
					},
					new Group() 
					{
						GroupName = "Средний рюкзак",
						Privs = new List<string>() { "backpacks.medium" }
					},
					new Group() 
					{
						GroupName = "Маленький рюкзак",
						Privs = new List<string>() { "backpacks.small" }
					},
					new Group() 
					{
						GroupName = "Переработчик",
						Privs = new List<string>() { "recycler.premium" }
					}
				},
				TitleText = "ВАШИ ПРИВИЛЕГИИ:",
				PermText = " * <color=orange>{0}</color>: осталось {1}"
            };
            SaveConfig(configData);
			timer.Once(0.1f, ()=> SaveConfig(configData));
        }
        
		private void LoadVariables() => configData = Config.ReadObject<ConfigData>();
        
		private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion

    }	
	
}