using System.Text.RegularExpressions;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;
using System;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Advert Messages", "https://discord.gg/9vyTXsJyKR", "1.0.0")]    
    internal class AdvertMessages : RustPlugin
    {        
        
		#region Variables
		
		private static int IndexAdvert = 0;
		
		#endregion
		
		#region Hooks
		
		private void Init() 
		{
			LoadVariables();
			LoadData();
		}
		
		private void OnServerInitialized() => RunAdvertsTimer();        
		
		private void OnServerSave() => SaveData();
		
		private void Unload() => SaveData();
		
		#endregion
		
		#region Main
		
		private void RunAdvertsTimer()
		{
            PrintWarning("\n-----------------------------\n" +
            "     Author - https://topplugin.ru/\n" +
            "     VK - https://vk.com/rustnastroika\n" +
            "     Discord - https://discord.com/invite/5DPTsRmd3G\n" +
            "-----------------------------");
			if (IndexAdvert > configData.Adverts.Count-1)
				IndexAdvert = 0;
			
			var message = "";
			if (configData.Adverts.ElementAtOrDefault(IndexAdvert) != null)
				message = configData.Adverts.ElementAt(IndexAdvert);
			
			IndexAdvert++;
			
			var commands = GetCommands(message);
			foreach (var command in commands)
			{
				var result = Interface.CallHook(command) as string;
				if (string.IsNullOrEmpty(result))
				{
					RunAdvertsTimer();
					return;
				}
				
				message = message.Replace("{" + command + "}", result);
			}
			
			foreach(var player in BasePlayer.activePlayerList.Where(x=> x != null && !PlayersDisableMessages.Contains(x.userID)))
				player.SendConsoleCommand("chat.add", new object[] { 2, 0, message });
											
			timer.Once(configData.RepeatTime, RunAdvertsTimer);
		}
		
		private static List<string> GetCommands(string message)
		{
			var commands = new List<string>();
			var command = "";
			var needWrite = false;
			
			foreach(var ch in message)
			{
				if (ch == '{')
				{
					needWrite = true;
					continue;
				}
				
				if (ch == '}')
				{
					needWrite = false;
					if (!string.IsNullOrEmpty(command))
						commands.Add(command);
					command = "";
					continue;
				}
				
				if (needWrite)
					command += ch;
			}
			
			return commands;
		}
		
		#endregion
		
		#region Commands
		
		[ChatCommand("messages")]
        private void cmdChatToggleMsg(BasePlayer player, string command, string[] args)
        {			
			if (player == null) return;
			
			if (args == null || args.Length == 0)
			{
				SendReply(player, "Использование: /messages on|off - включить или выключить подсказки в чате");
				return;
			}
			
			switch (args[0].ToLower())
			{
				case "on": 
				{
					if (PlayersDisableMessages.Contains(player.userID))
						PlayersDisableMessages.Remove(player.userID);
					
					SendReply(player, "Подсказки в чате включены");
					break;
				}
				case "off": 
				{
					if (!PlayersDisableMessages.Contains(player.userID))
						PlayersDisableMessages.Add(player.userID);
					
					SendReply(player, "Подсказки в чате выключены");
					break;
				}
				default:
				{
					SendReply(player, "Указан неверный параметр.\nИспользование: /messages on|off - включить или выключить подсказки в чате");
					break;
				}
			}
		}
		
		#endregion
		
		#region Config
		
        private static ConfigData configData;
		
        private class ConfigData
        {            
			[JsonProperty(PropertyName = "Список сообщений для чата")]
			public List<string> Adverts;
			[JsonProperty(PropertyName = "Частота повторения сообщений (секунды)")]
			public float RepeatTime;
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                Adverts = new List<string>()
				{
					"Тестовое сообщение"
				},
				RepeatTime = 300
            };
            SaveConfig(configData);
			timer.Once(0.1f, ()=> SaveConfig(configData));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
		
		#region Data
		
		private static HashSet<ulong> PlayersDisableMessages = new HashSet<ulong>();
		
		private void LoadData() => PlayersDisableMessages = Interface.GetMod().DataFileSystem.ReadObject<HashSet<ulong>>("AdvertMessagesData");					
		
		private static void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("AdvertMessagesData", PlayersDisableMessages);		
		
		#endregion
		
    }
}