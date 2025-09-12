using System;
using System.Text.RegularExpressions;
using System.Linq;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using Rust;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("VkCommandRouter", "Nimant", "1.0.8")]
    public class VkCommandRouter : RustPlugin
    {
		
		#region Variables
		
		[PluginReference] 
		private Plugin ModerControl;
		
		[PluginReference] 
		private Plugin VizovNaProverku;
		
		[PluginReference] 
		private Plugin BS;
		
		[PluginReference] 
		private Plugin AfkCheck;
		
		[PluginReference] 
		private Plugin ModerTools;
		
		[PluginReference] 
		private Plugin CheckCupboard;
		
		[PluginReference] 
		private Plugin PlayerWatch;
		
		[PluginReference]
        private Plugin RaidAlert;
		
		[PluginReference]
        private Plugin VkReports;
		
		private static Dictionary<ulong, UserData> PlayersData = new Dictionary<ulong, UserData>();
		
		private class UserData
		{
			public int minutesOnline;
			public DateTime lastPlay;
			public string lastIP = "нет данных";
		}				
		
		#endregion
		
		#region Hooks
		
		private void Init() 
		{
			LoadVariables();
			
			if (string.IsNullOrEmpty(configData.ChatInfoID))
			{
				configData.ChatInfoID = "2000000046";
				SaveConfig(configData);
			}
			
			LoadData();
		}
		
		private void OnNewSave()
		{
			PlayersData.Clear();
			SaveData();
		}
		
		private void OnServerSave() => SaveData();
		
		private void Unload() => SaveData();
		
		private void OnPlayerConnected(BasePlayer player)
        {
			if (!PlayersData.ContainsKey(player.userID))
				PlayersData.Add(player.userID, new UserData());
			
			PlayersData[player.userID].lastPlay = DateTime.Now;
			var addr = player.net?.connection?.ipaddress;
			PlayersData[player.userID].lastIP = !string.IsNullOrEmpty(addr) ? addr.Substring(0, addr.IndexOf(":")) : PlayersData[player.userID].lastIP;
		}
		
		private void OnServerInitialized() => timer.Once(15f, CheckOnline);
		
		#endregion
		
		#region Timers
		
		private void CheckOnline()
		{			
			var currDt = DateTime.Now;
			
			foreach (var player in BasePlayer.activePlayerList)
			{
				if (!PlayersData.ContainsKey(player.userID))
					OnPlayerConnected(player);
				
				PlayersData[player.userID].lastPlay = currDt;
				PlayersData[player.userID].minutesOnline++;
			}
			
			timer.Once(60f, CheckOnline);
		}
		
		#endregion
		
		#region Helpers
		
		private static string FixName(string name) => name.Replace("&","_").Replace("#","_").Replace("?","_");
		
		private string GetPlayerName(ulong userID)
		{
			var data = permission.GetUserData(userID.ToString());															
			return data.LastSeenNickname;
		}
		
		private static string GetServerName()
		{
			var serverName = ConVar.Server.hostname;
			
			if (serverName.IndexOf('(') > 0)
				return serverName.Substring(0, serverName.IndexOf('(')-1);
			
			return serverName;
		}

		private void SendVkMessage(string text) => SendVkMessageExt(text);
		
		private void SendVkMessageExt(string text, string chatID = null)		
		{
			if (string.IsNullOrEmpty(chatID))
				chatID = configData.ChatID;
			
			var srv = VkReports != null ? VkReports.Call("API_GetServerName") as string : GetServerName();
			
			text = $"Cервер: {srv}\n" + RemoveFormatting(text);
			webrequest.EnqueuePost("https://api.vk.com/method/messages.send", "peer_id="+chatID+"&message="+text+"&v=5.80&access_token="+configData.Token, (code, response) => { /*Puts(response);*/ }, this);
		}
		
		private string RemoveFormatting(string old)
		{
			string _new = old;
			
			var matches = new Regex(@"(<color=.+?>)", RegexOptions.IgnoreCase).Matches(_new);
			foreach(Match match in matches)
			{
				if(match.Success) _new = _new.Replace(match.Groups[1].ToString(), "");
			}
			
			_new = _new.Replace("</color>", "");
			
			return _new;
		}
		
		private static bool IsVkID(string vk)
		{
			if (string.IsNullOrEmpty(vk))
				return false;
			
			foreach (var ch in vk)			
				if ("0123456789".IndexOf(ch) < 0)
					return false;
			
			return true;
		}
		
		#endregion
		
		#region Commands
		
		[ConsoleCommand("checkuser")]
        private void ConsoleVkCommandCheck(ConsoleSystem.Arg args)
        {
            if (args == null || args.Connection != null) return;
			
            if (args?.Args == null || args.Args.Length < 2)
			{
				SendVkMessage("Вы не указали Vk ID и Steam ID игрока"); 
                return;
            }
			
			var vkID = args.Args[0];
			
			if (!IsVkID(vkID))
			{
				SendVkMessage("Указан некорректный Vk ID"); 
                return;
			}
			
			var user = args.Args[1];
			
			var discord = "";
			if (args.Args.Length > 2)			
				discord = args.Args[2];
			
			var steamIDs = ModerControl?.Call("API_GetModerSteamIDs", vkID) as List<ulong>;
			
			if (steamIDs == null || steamIDs.Count == 0)
			{
				SendVkMessage("Вы не зарегистрированы как модератор на сервере.\nОбратитесь к администратору, что бы он вас зарегистрировал");
                return;
			}
			
			var lastSteam = steamIDs.Last();
			var moderName = GetPlayerName(lastSteam);
			moderName = (string.IsNullOrEmpty(moderName) || (moderName == "Unnamed")) ? $"Vk ID {vkID}" : moderName;
			
			VizovNaProverku?.Call("API_VizovPlayer", lastSteam, moderName, user, discord, (Action<string>) SendVkMessage);
		}	
		
		[ConsoleCommand("stopuser")]
        private void ConsoleVkCommandStop(ConsoleSystem.Arg args)
        {
            if (args == null || args.Connection != null) return;
			
            if (args?.Args == null || args.Args.Length < 2)
			{
				SendVkMessage("Вы не указали Vk ID и Steam ID игрока"); 
                return;
            }
			
			var vkID = args.Args[0];
			
			if (!IsVkID(vkID))
			{
				SendVkMessage("Указан некорректный Vk ID"); 
                return;
			}
			
			var user = args.Args[1];
			
			var steamIDs = ModerControl?.Call("API_GetModerSteamIDs", vkID) as List<ulong>;
			
			if (steamIDs == null || steamIDs.Count == 0)
			{
				SendVkMessage("Вы не зарегистрированы как модератор на сервере.\nОбратитесь к администратору, что бы он вас зарегистрировал");
                return;
			}
			
			var lastSteam = steamIDs.Last();
			var moderName = GetPlayerName(lastSteam);
			moderName = (string.IsNullOrEmpty(moderName) || (moderName == "Unnamed")) ? $"Vk ID {vkID}" : moderName;
			
			VizovNaProverku?.Call("API_StopVizovPlayer", lastSteam, moderName, user, (Action<string>) SendVkMessage);
		}
		
		[ConsoleCommand("banuser")]
        private void ConsoleVkCommandBan(ConsoleSystem.Arg args)
        {
            if (args == null || args.Connection != null) return;
			
            if (args?.Args == null || args.Args.Length < 2)
			{
				SendVkMessage("Вы не указали Vk ID и Steam ID игрока"); 
                return;
            }
			
			var vkID = args.Args[0];
			
			if (!IsVkID(vkID))
			{
				SendVkMessage("Указан некорректный Vk ID"); 
                return;
			}
			
			var userOrServer = args.Args[1];						
			var user = "";
			var isLocalBan = false;
			
			if (userOrServer.ToLower() == "prime" || userOrServer.ToLower() == "medium" || userOrServer.ToLower() == "barren")
			{
				var serv = GetServerName().ToLower();
				if (!serv.Contains(userOrServer.ToLower()))
					return;
				
				user = args.Args.Length > 2 ? args.Args[2] : "";
				isLocalBan = true;
			}
			else
				user = userOrServer;
			
			var param = new List<string>();
			
			for(int ii=isLocalBan ? 3 : 2; ii < args.Args.Length; ii++)
				param.Add(args.Args[ii]);
						
			var steamIDs = ModerControl?.Call("API_GetModerSteamIDs", vkID) as List<ulong>;
			
			if (steamIDs == null || steamIDs.Count == 0)
			{
				SendVkMessage("Вы не зарегистрированы как модератор на сервере.\nОбратитесь к администратору, что бы он вас зарегистрировал");
                return;
			}
			
			var lastSteam = steamIDs.Last();
			var moderName = GetPlayerName(lastSteam);
			moderName = (string.IsNullOrEmpty(moderName) || (moderName == "Unnamed")) ? $"Vk ID {vkID}" : moderName;
			
			BS?.Call("API_Ban", lastSteam, moderName, user, param.ToArray(), (Action<string>) SendVkMessage);
		}
		
		[ConsoleCommand("unbanuser")]
        private void ConsoleVkCommandUnban(ConsoleSystem.Arg args)
        {
            if (args == null || args.Connection != null) return;
			
            if (args?.Args == null || args.Args.Length < 2)
			{
				SendVkMessage("Вы не указали Vk ID и Steam ID игрока"); 
                return;
            }
			
			var vkID = args.Args[0];
			
			if (!IsVkID(vkID))
			{
				SendVkMessage("Указан некорректный Vk ID"); 
                return;
			}
			
			var user = args.Args[1];
			
			var steamIDs = ModerControl?.Call("API_GetModerSteamIDs", vkID) as List<ulong>;
			
			if (steamIDs == null || steamIDs.Count == 0)
			{
				SendVkMessage("Вы не зарегистрированы как модератор на сервере.\nОбратитесь к администратору, что бы он вас зарегистрировал");
                return;
			}
			
			var lastSteam = steamIDs.Last();
			var moderName = GetPlayerName(lastSteam);
			moderName = (string.IsNullOrEmpty(moderName) || (moderName == "Unnamed")) ? $"Vk ID {vkID}" : moderName;
			
			BS?.Call("API_UnBan", lastSteam, moderName, user, (Action<string>) SendVkMessage);
		}
		
		[ConsoleCommand("kickuser")]
        private void ConsoleVkCommandKick(ConsoleSystem.Arg args)
        {
            if (args == null || args.Connection != null) return;
			
            if (args?.Args == null || args.Args.Length < 2)
			{
				SendVkMessage("Вы не указали Vk ID и Steam ID игрока"); 
                return;
            }
			
			var vkID = args.Args[0];
			
			if (!IsVkID(vkID))
			{
				SendVkMessage("Указан некорректный Vk ID"); 
                return;
			}
			
			var user = args.Args[1];
			
			var steamIDs = ModerControl?.Call("API_GetModerSteamIDs", vkID) as List<ulong>;
			
			if (steamIDs == null || steamIDs.Count == 0)
			{
				SendVkMessage("Вы не зарегистрированы как модератор на сервере.\nОбратитесь к администратору, что бы он вас зарегистрировал");
                return;
			}
			
			var lastSteam = steamIDs.Last();
			var moderName = GetPlayerName(lastSteam);
			moderName = (string.IsNullOrEmpty(moderName) || (moderName == "Unnamed")) ? $"Vk ID {vkID}" : moderName;
			
			var reason = "";			
			for(int ii = 2; ii < args.Args.Length; ii++)
				reason += args.Args[ii] + ' ';
			
			BS?.Call("API_Kick", lastSteam, moderName, user, reason, (Action<string>) SendVkMessage);
		}
		
		[ConsoleCommand("infouser")]
        private void ConsoleVkCommandInfoUser(ConsoleSystem.Arg args)
        {
            if (args == null || args.Connection != null) return;

            if (args?.Args == null || args.Args.Length < 1)
			{
				SendVkMessage("Вы не указали Steam ID игрока"); 
                return;
            }
			
			var userID_ = args.Args[0];
            
            if (!userID_.IsSteamId())
			{
				SendVkMessage("Указанный ID не является Steam ID"); 
                return;
			}

			var userID = (ulong)Convert.ToInt64(userID_);
			
			var player = BasePlayer.activePlayerList.FirstOrDefault(x=> x.userID == userID);
			if (player == null)
				player = BasePlayer.sleepingPlayerList.FirstOrDefault(x=> x.userID == userID);
			
			var playerName = player != null ? FixName(player.displayName) : GetPlayerName(userID).Replace("Unnamed", "нет данных");			
			var data = PlayersData.ContainsKey(userID) ? PlayersData[userID] : (UserData)null;			
			var playTime = data != null ? (data.minutesOnline <= 0 ? "не играл" : (data.minutesOnline < 60 ? $"{data.minutesOnline} мин." : $"{(int)Math.Truncate(data.minutesOnline/60f)} ч.")) : "не играл";			
			var lastIP = data != null ? data.lastIP : "нет данных";
			var position = player != null ? player.transform.position.ToString().Replace("(","").Replace(")","") : "нет данных";
			var isOnline = player != null && player.IsConnected;
			var lastPlay = isOnline ? "" : ("\nЗаходил: " + (data != null ? data.lastPlay.ToString("dd.MM.yyyy HH:mm") : "нет данных"));
			var status = player != null ? (player.IsConnected ? "онлайн" : "спит") : (data != null ? "убит (нет тела)" : "нет данных");						
			var vkID = RaidAlert?.Call("API_GetPlayerVk", userID) as string;
			var vk = !string.IsNullOrEmpty(vkID) ? $"https://vk.com/id{vkID}" : "нет данных";
			
			var msg = $"Данные о игроке:\nSteam ID: {userID}\nИмя: {playerName}\nНаиграно: {playTime}\nСтатус: {status}\nПозиция: {position}{lastPlay}\nПоследний IP: {lastIP}\nВК: {vk}";
			
			SendVkMessage(msg);
        }
		
		[ConsoleCommand("isafkuser")]
        private void ConsoleVkCommandIsAFK(ConsoleSystem.Arg args)
        {
            if (args == null || args.Connection != null) return;
			
            if (args?.Args == null || args.Args.Length < 2)
			{
				SendVkMessage("Вы не указали Vk ID и Steam ID игрока"); 
                return;
            }
			
			var vkID = args.Args[0];
			
			if (!IsVkID(vkID))
			{
				SendVkMessage("Указан некорректный Vk ID"); 
                return;
			}
			
			var user = args.Args[1];
			
			var steamIDs = ModerControl?.Call("API_GetModerSteamIDs", vkID) as List<ulong>;
			
			if (steamIDs == null || steamIDs.Count == 0)
			{
				SendVkMessage("Вы не зарегистрированы как модератор на сервере.\nОбратитесь к администратору, что бы он вас зарегистрировал");
                return;
			}
			
			var lastSteam = steamIDs.Last();
			AfkCheck?.Call("API_CheckAFK", lastSteam, user, (int)30, (Action<string>) SendVkMessage);
		}
		
		[ConsoleCommand("teamviewuser")]
        private void ConsoleVkCommandTeamView(ConsoleSystem.Arg args)
        {
            if (args == null || args.Connection != null) return;
			
            if (args?.Args == null || args.Args.Length < 2)
			{
				SendVkMessage("Вы не указали Vk ID и Steam ID игрока"); 
                return;
            }
			
			var vkID = args.Args[0];
			
			if (!IsVkID(vkID))
			{
				SendVkMessage("Указан некорректный Vk ID"); 
                return;
			}
			
			var user = args.Args[1];
			
			var steamIDs = ModerControl?.Call("API_GetModerSteamIDs", vkID) as List<ulong>;
			
			if (steamIDs == null || steamIDs.Count == 0)
			{
				SendVkMessage("Вы не зарегистрированы как модератор на сервере.\nОбратитесь к администратору, что бы он вас зарегистрировал");
                return;
			}
			
			var lastSteam = steamIDs.Last();
			ModerTools?.Call("API_TeamView", lastSteam, user, (Action<string>) SendVkMessage);
		}
		
		[ConsoleCommand("friendviewuser")]
        private void ConsoleVkCommandFriendView(ConsoleSystem.Arg args)
        {
            if (args == null || args.Connection != null) return;
			
            if (args?.Args == null || args.Args.Length < 2)
			{
				SendVkMessage("Вы не указали Vk ID и Steam ID игрока"); 
                return;
            }
			
			var vkID = args.Args[0];
			
			if (!IsVkID(vkID))
			{
				SendVkMessage("Указан некорректный Vk ID"); 
                return;
			}
			
			var user = args.Args[1];
			
			var steamIDs = ModerControl?.Call("API_GetModerSteamIDs", vkID) as List<ulong>;
			
			if (steamIDs == null || steamIDs.Count == 0)
			{
				SendVkMessage("Вы не зарегистрированы как модератор на сервере.\nОбратитесь к администратору, что бы он вас зарегистрировал");
                return;
			}
			
			var lastSteam = steamIDs.Last();
			ModerTools?.Call("API_FriendView", lastSteam, user, (Action<string>) SendVkMessage);
		}
		
		[ConsoleCommand("cupboarduser")]
        private void ConsoleVkCommandCupboard(ConsoleSystem.Arg args)
        {
            if (args == null || args.Connection != null) return;
			
            if (args?.Args == null || args.Args.Length < 2)
			{
				SendVkMessage("Вы не указали Vk ID и Steam ID игрока"); 
                return;
            }
			
			var vkID = args.Args[0];
			
			if (!IsVkID(vkID))
			{
				SendVkMessage("Указан некорректный Vk ID"); 
                return;
			}
			
			var user = args.Args[1];
			
			var steamIDs = ModerControl?.Call("API_GetModerSteamIDs", vkID) as List<ulong>;
			
			if (steamIDs == null || steamIDs.Count == 0)
			{
				SendVkMessage("Вы не зарегистрированы как модератор на сервере.\nОбратитесь к администратору, что бы он вас зарегистрировал");
                return;
			}
			
			var lastSteam = steamIDs.Last();
			CheckCupboard?.Call("API_CupboardAuth", lastSteam, user, (Action<string>) SendVkMessage);
		}
		
		[ConsoleCommand("watchuser")]
        private void ConsoleVkCommandWatch(ConsoleSystem.Arg args)
        {
            if (args == null || args.Connection != null) return;
			
            if (args?.Args == null || args.Args.Length < 2)
			{
				SendVkMessage("Вы не указали Vk ID и команду (add, remove, list)"); 
                return;
            }
			
			var vkID = args.Args[0];
			
			if (!IsVkID(vkID))
			{
				SendVkMessage("Указан некорректный Vk ID"); 
                return;
			}
			
			var command = args.Args[1].ToLower();
			if (command != "add" && command != "remove" && command != "list" && command != "removeall")
			{
				SendVkMessage("Вы указали неверную команду, нужна одна из перечисленных: add, remove, list, removeall"); 
                return;
			}
			
			if (command != "list" && command != "removeall" && args.Args.Length == 2)
			{
				SendVkMessage("Вы не указали Steam ID игрока или IP"); 
                return;
			}
			
			var user = "";
			
			if (args.Args.Length > 2)
				for (int ii = 2; ii < args.Args.Length; ii++)
					user += args.Args[ii] + " ";
				
			user = user.Trim();
			
			var steamIDs = ModerControl?.Call("API_GetModerSteamIDs", vkID) as List<ulong>;
			
			if (steamIDs == null || steamIDs.Count == 0)
			{
				SendVkMessage("Вы не зарегистрированы как модератор на сервере.\nОбратитесь к администратору, что бы он вас зарегистрировал");
                return;
			}
			
			var lastSteam = steamIDs.Last();
			// поддержку регистрации через ВК пока не делал, пусть заходят модеры на сервер, регаются
			PlayerWatch?.Call("API_Watch", lastSteam, command, user.Split(' '), "" /*тут должно быть ВК модера, ссылка*/, (Action<string>) SendVkMessage);
		}
		
		#endregion
		
		#region API
		
		private void API_SendInfoMsgVK(string msg) => SendVkMessageExt(msg, configData.ChatInfoID);
		
		#endregion
		
		#region Config
		
        private static ConfigData configData;
		
        private class ConfigData
        {            
			[JsonProperty(PropertyName = "VK Токен")]
			public string Token;
			[JsonProperty(PropertyName = "ID Беседы")]
			public string ChatID;
			[JsonProperty(PropertyName = "ID Инфо Беседы")]
			public string ChatInfoID;
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                Token = "6b6367cbccab1449ce2752b953f7a21a3b644c3e48a73d8de2b8f8a8e67636f31cbae0a20a8e5e6014745",
				ChatID = "2000000009",
				ChatInfoID = "2000000046"
            };
            SaveConfig(configData);
			timer.Once(0.1f, ()=> SaveConfig(configData));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
		
		#region Data
		
		private void LoadData() => PlayersData = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, UserData>>("VkCommandRouterData");
		
		private static void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("VkCommandRouterData", PlayersData);		
		
		#endregion		
		
    }
}