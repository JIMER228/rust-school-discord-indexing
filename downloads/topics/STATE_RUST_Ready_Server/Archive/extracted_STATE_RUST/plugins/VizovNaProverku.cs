using System;
using System.Collections.Generic;
using UnityEngine;
using System.Globalization;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("VizovNaProverku", "Vokidu", "0.0.20")]
    class VizovNaProverku : RustPlugin
    {
		
		#region Variables
		
		[PluginReference] 
		private Plugin ModerControl;
		
		private static List<ulong> Vizvannie = new List<ulong>();				
		private static Dictionary<ulong, bool> ShowedVizovInfo = new Dictionary<ulong, bool>();
		private static ConfigData cfg;
		
        private class ConfigData
        {
            public string api;
            public int vk_chat;
			public string server;
			public List<ulong> exclude;
        }
		
		#endregion
		
		#region Helpers
		
		private void VkLog(string text)
		{
			text = $"Сервер: {cfg.server}\n" + text;
			webrequest.EnqueuePost("https://api.vk.com/method/messages.send", "peer_id="+cfg.vk_chat+"&message="+text+"&v=5.80&access_token="+cfg.api, (code, response) => {Puts(code+" "+response);}/*(code, response, player)*/, this);
		}
		
		private static string FixName(string name) => name.Replace("&","_").Replace("#","_");
		
		private static List<BasePlayer> FindPlayers(string nameOrIdOrIp)
        {
			var players = new List<BasePlayer>();
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString == nameOrIdOrIp)
                    players.Add(activePlayer);
                else if (activePlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.OrdinalIgnoreCase))
                    players.Add(activePlayer);
                else if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress.Split(new char [] {':'})[0] == nameOrIdOrIp)
                    players.Add(activePlayer);
            }
			
			if (players.Count > 0)
				return players;
			
            return null;
        }
		
		private string GetPlayerName(ulong userID)
		{
			var data = permission.GetUserData(userID.ToString());															
			return data.LastSeenNickname.Replace("'","").Replace("<", "˂").Replace(">", "˃");
		}
		
		#endregion
		
		#region Hooks
		
		private void Init()
        {
            cfg = Config.ReadObject<ConfigData>();
			LoadData();
        }
		
		private void Unload() => SaveData();
		
        protected override void LoadDefaultConfig()
        {			
			var config = new ConfigData
            {
                api = "Тут ввести свой айпи ключ от вк",
                vk_chat = 0,
				server = "Test Server",
				exclude = new List<ulong>()                
            };
            Config.WriteObject(config, true);
        }
		
		private void OnPlayerConnected(BasePlayer player)
        {
			if (player == null || !Vizvannie.Contains(player.userID)) return;
			
            if (!ShowedVizovInfo.ContainsKey(player.userID)) 
				ShowedVizovInfo.Add(player.userID, false);
			else
				ShowedVizovInfo[player.userID] = false;
		}	
		
		private void OnPlayerSleepEnded(BasePlayer player)
        {					
			if (player == null || !Vizvannie.Contains(player.userID) || !ShowedVizovInfo.ContainsKey(player.userID)) return;
			
			if (!ShowedVizovInfo[player.userID])
				timer.Once(0.5f, () => ShowVizov(player));            
        }
		
		private void OnPlayerDisconnected(BasePlayer player, string reason)
		{	
			if (Vizvannie.Contains(player.userID))							
				VkLog("Игрок "+FixName(player.displayName)+$" покинул сервер во время проверки.\nПричина: {reason}.");
		}
		
		#endregion
		
		#region Commands
		
		[ChatCommand("discord")]
		private void SendDiscord(BasePlayer player, string command, string[] args)
		{
			if(!Vizvannie.Contains(player.userID))
				return;
			
			if(args.Length < 1){
				SendReply(player, "Вы ничего не написали.");
				return;
			}
				
			var discord = "";
			foreach(var arg in args)
				discord = discord+" "+arg;
				
			var text = "Игрок "+FixName(player.displayName)+" предоставил свой дискорд на проверку.\n"+discord;
			VkLog(text);
			SendReply(player, "Ваш дискорд отправлен модераторам. Ожидайте.");
		}
		
		[ChatCommand("check")]
		private void VizovPlayer(BasePlayer player, string command, string[] args)
		{
			if (!permission.UserHasPermission(player.UserIDString, "BS.Total") && !permission.UserHasPermission(player.UserIDString, "BS.Ban"))
			{
				SendReply(player, "У Вас нет прав на использование этой команды.");
				return;
			} else if(args.Length < 1) {
				SendReply(player, "Вы не указали игрока.");
				return;
			}
			
			var plylist = FindPlayers(args[0]);
			if(plylist == null) {
				SendReply(player, "Игрок не найден.");
				return;
			} else if(plylist.Count > 1) {
				var players = "Найдено несколько игроков с похожим ником: ";
				foreach (var banned in plylist)
				{
					players = players+"\n"+banned.displayName+" - "+banned.UserIDString;
				}
				SendReply(player, players);
				return;
			}
			
			var discord = "";
			if(args.Length > 1)
				discord = args[1];
			
			if (cfg.exclude.Contains(plylist[0].userID))
			{
				SendReply(player, "Игрок не найден.");
				return;
			}
			
			Vizvannie.Remove(plylist[0].userID);
			Vizvannie.Add(plylist[0].userID);
			SaveData();
			AddQueuePriorityGroup(plylist[0].userID);
			ShowVizov(plylist[0], discord);
			SendReply(player, plylist[0].displayName + " вызван на проверку. Напишите <color=#FD0>/stop "+plylist[0].displayName+"</color> для отмены проверки.");
			VkLog(FixName(player.displayName)+" вызвал на проверку игрока "+FixName(plylist[0].displayName)+$" ({plylist[0].userID}).");
			Server.Broadcast("Модератор вызвал подозреваемого игрока на проверку.");
			ModerControl?.Call("API_SendResult", player.userID, plylist[0].userID, 5);
		}
		
		[ChatCommand("stop")]
		private void StopVizovPlayer(BasePlayer player, string command, string[] args)
		{
			if (!permission.UserHasPermission(player.UserIDString, "BS.Total") && !permission.UserHasPermission(player.UserIDString, "BS.Ban"))
			{
				SendReply(player, "У Вас нет прав на использование этой команды.");
				return;
			} else if(args.Length < 1) {
				SendReply(player, "Вы не указали игрока.");
				return;
			}
			
			BasePlayer user = null;
			
			if (!args[0].IsSteamId())
			{
				var plylist = FindPlayers(args[0]);
				if(plylist == null) {
					SendReply(player, "Игрок не найден.");
					return;
				} else if(plylist.Count > 1) {
					var players = "Найдено несколько игроков с похожим ником: ";
					foreach (var banned in plylist)
					{
						players = players+"\n"+banned.displayName+" - "+banned.UserIDString;
					}
					SendReply(player, players);
					return;
				}				
				user = plylist[0];
			}
			else
			{
				user = BasePlayer.Find(args[0]);
				if (user == null)
					user = BasePlayer.FindSleeping(args[0]);
			}
			
			var userID = user != null ? user.userID : (ulong)Convert.ToInt64(args[0]);
			var userName = GetPlayerName(userID);
			
			if (!Vizvannie.Contains(userID)){
				SendReply(player, userName + " не вызывался на проверку");
				return;
			}
			
			Vizvannie.Remove(userID);
			SaveData();
			RemoveQueuePriorityGroup(userID);
			if (user != null)
			{
				CuiHelper.DestroyUi(user, "VizovText");
				CuiHelper.DestroyUi(user, "VizovOverlay");
			}
			ModerControl?.Call("API_SendResult", player.userID, userID, 6);
			VkLog(FixName(player.displayName)+" остановил проверку игрока "+FixName(userName)+".");
			SendReply(player, userName + " больше не проверяется.");
		}
		
		[ChatCommand("visov_test")]
		private void TestVizovPlayer(BasePlayer player, string command, string[] args)
		{
			if (player.IsAdmin)
			{
				if (args.Length > 0 && args[0] == "on")
					ShowVizov(player, "123");
				
				if (args.Length > 0 && args[0] == "off")
				{
					CuiHelper.DestroyUi(player, "VizovText");
					CuiHelper.DestroyUi(player, "VizovOverlay");
				}
			}
		}
		
		#endregion
		
		#region Group
		
		private void AddQueuePriorityGroup(ulong userID)
		{
			if (!permission.UserHasGroup(userID.ToString(), "vizov"))
				permission.AddUserGroup(userID.ToString(), "vizov");
		}
		
		private void RemoveQueuePriorityGroup(ulong userID)
		{
			if (permission.UserHasGroup(userID.ToString(), "vizov"))
				permission.RemoveUserGroup(userID.ToString(), "vizov");
		}
		
		#endregion
		
		#region Commands API
		
		private void API_VizovPlayer(ulong moderID, string moderName, string user, string discord, Action<string> cbSendVkAnswer)
		{
			if (!permission.UserHasPermission(moderID.ToString(), "BS.Total") && !permission.UserHasPermission(moderID.ToString(), "BS.Ban"))
			{
				cbSendVkAnswer("У Вас нет прав на использование этой команды.");
				return;
			} 
			else if(string.IsNullOrEmpty(user))
			{
				cbSendVkAnswer("Вы не указали игрока.");
				return;
			}
			
			var plylist = FindPlayers(user);
			
			if(plylist == null) 
			{
				cbSendVkAnswer("Игрок не найден.");
				return;
			}
			else if (plylist.Count > 1) 
			{
				var players = "Найдено несколько игроков с похожим ником: ";
				foreach (var banned in plylist)				
					players = players+"\n"+FixName(banned.displayName)+" - "+banned.UserIDString;
				
				cbSendVkAnswer(players);
				return;
			}
			
			if (cfg.exclude.Contains(plylist[0].userID))
			{
				cbSendVkAnswer("Игрок не найден.");
				return;
			}
			
			Vizvannie.Remove(plylist[0].userID);
			Vizvannie.Add(plylist[0].userID);
			SaveData();
			AddQueuePriorityGroup(plylist[0].userID);
			ShowVizov(plylist[0], discord);
			cbSendVkAnswer(FixName(plylist[0].displayName) + " вызван на проверку. Напишите /stop "+FixName(plylist[0].displayName)+" для отмены проверки.");
			VkLog(FixName(moderName)+$" ({moderID}) через ВК вызвал на проверку игрока "+FixName(plylist[0].displayName)+$" ({plylist[0].userID}).");
			Server.Broadcast("Модератор вызвал подозреваемого игрока на проверку.");
			ModerControl?.Call("API_SendResult", moderID, plylist[0].userID, 5); 
		}
		
		private void API_StopVizovPlayer(ulong moderID, string moderName, string user_, Action<string> cbSendVkAnswer)
		{
			if (!permission.UserHasPermission(moderID.ToString(), "BS.Total") && !permission.UserHasPermission(moderID.ToString(), "BS.Ban"))
			{
				cbSendVkAnswer("У Вас нет прав на использование этой команды.");
				return;
			}
			else if(string.IsNullOrEmpty(user_))
			{
				cbSendVkAnswer("Вы не указали игрока.");
				return;
			}
						
			BasePlayer user = null;
			
			if (!user_.IsSteamId())
			{
				var plylist = FindPlayers(user_);
				if(plylist == null) {
					cbSendVkAnswer("Игрок не найден.");
					return;
				} else if(plylist.Count > 1) {
					var players = "Найдено несколько игроков с похожим ником: ";
					foreach (var banned in plylist)				
						players = players+"\n"+FixName(banned.displayName)+" - "+banned.UserIDString;
					
					cbSendVkAnswer(players);
					return;
				}				
				user = plylist[0];
			}
			else
			{
				user = BasePlayer.Find(user_);
				if (user == null)
					user = BasePlayer.FindSleeping(user_);
			}
			
			var userID = user != null ? user.userID : (ulong)Convert.ToInt64(user_);
			var userName = GetPlayerName(userID);			
			
			if (!Vizvannie.Contains(userID))
			{
				cbSendVkAnswer(FixName(userName) + " не вызывался на проверку");
				return;
			}
			
			Vizvannie.Remove(userID);
			SaveData();
			RemoveQueuePriorityGroup(userID);
			if (user != null)
			{
				CuiHelper.DestroyUi(user, "VizovText");
				CuiHelper.DestroyUi(user, "VizovOverlay");
			}	
			ModerControl?.Call("API_SendResult", moderID, userID, 6);
			VkLog(FixName(moderName)+$" ({moderID}) через ВК остановил проверку игрока "+FixName(userName)+".");
			cbSendVkAnswer(FixName(userName) + " больше не проверяется.");
		}
		
		#endregion
		
		#region GUI
		
		private void ShowVizov(BasePlayer player, string discord = "")
        {
			if (player == null) return;
			
			var text = "<size=14>\n</size>Вы подозреваетесь в использовании читов. Пройдите проверку на наличие читов.\nНапишите свой дискорд используя команду <color=#FD0>/discord</color> в течение <color=#FD0>5 минут</color>.\n\n<color=white>ЕСЛИ ВЫ ПОКИНЕТЕ СЕРВЕР, ВЫ БУДЕТЕ ЗАБАНЕНЫ!</color>";
			if(discord != "")
				text = "<size=14>\n</size>Вы подозреваетесь в использовании читов. Пройдите проверку на наличие читов.\nДобавьте в дискорд модератора <color=#FD0>"+discord+"</color> или напишите свой дискорд используя команду <color=#FD0>/discord</color>.\n\n<color=white>ЕСЛИ ВЫ ПОКИНЕТЕ СЕРВЕР, ВЫ БУДЕТЕ ЗАБАНЕНЫ!</color>";
		
			CuiHelper.DestroyUi(player, "VizovText");
			CuiHelper.DestroyUi(player, "VizovOverlay");
		
			var elements = new CuiElementContainer();
			
			elements.Add(new CuiPanel
            {
                Image = { Color = "0.6 0 0 0.9", FadeIn = 0.15f},
                RectTransform = { AnchorMin = "0 0.85", AnchorMax = "1 1" },
				FadeOut = 0f
            }, "Hud", "VizovOverlay");
			
			elements.Add(new CuiLabel
			{
				Text = {
					Text = text,
					Align = TextAnchor.MiddleCenter,
					Color = "1 1 1 0.9",
					FontSize = 18,
					FadeIn = 0f
				},
				RectTransform = {
					AnchorMin = "0 0",
					AnchorMax = "1 1",
				},
				FadeOut = 0f
			}, "VizovOverlay", "VizovText");
			
			SendUI(player, elements);
		}				
		
		private static void SendUI(BasePlayer player, CuiElementContainer container)
		{
			var json = JsonConvert.SerializeObject(container, Formatting.None, new JsonSerializerSettings
			{
				StringEscapeHandling = StringEscapeHandling.Default,
				DefaultValueHandling = DefaultValueHandling.Ignore,
				Formatting = Formatting.Indented
			});
			json = json.Replace(@"\t", "\t");
			json = json.Replace(@"\n", "\n");
		
			CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", json);
		}
		
		#endregion				
		
		#region Data
		
		private void LoadData() => Vizvannie = Interface.GetMod().DataFileSystem.ReadObject<List<ulong>>("VizovNaProverkuData");					
		
		private static void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("VizovNaProverkuData", Vizvannie);		
		
		#endregion
		
    }
}