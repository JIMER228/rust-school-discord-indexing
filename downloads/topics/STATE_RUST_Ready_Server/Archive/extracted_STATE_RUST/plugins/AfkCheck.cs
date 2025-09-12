using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AfkCheck", "Nimant", "1.0.2")]
    class AfkCheck : RustPlugin
    {				
	
		#region Variables 
		
		private string AllowPerm = "afkcheck.access";
				
		private Dictionary<ulong, AFKInfo> AFKData = new Dictionary<ulong, AFKInfo>();			
		
		private class AFKInfo
		{
			public BasePlayer player;
			public Vector3 pos;
			public int wait;
		}
		
		#endregion
		
		#region Hooks
		
		private void Init() => permission.RegisterPermission(AllowPerm, this);
		
		#endregion
		
		#region Common
		
		private static string FixName(string name) => name.Replace("&","_").Replace("#","_").Replace("?","_");
		
		private bool HasPermission(BasePlayer player)
        {
            return player.IsAdmin || permission.UserHasPermission(player.UserIDString, AllowPerm);
        }
		
		private bool HasPermission(ulong userID)
        {
            return permission.UserHasPermission(userID.ToString(), AllowPerm);
        }
		
		private static List<BasePlayer> FindPlayers(string nameOrIdOrIp)
        {
			var players = new List<BasePlayer>();
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString == nameOrIdOrIp)
                    players.Add(activePlayer);
                else if (activePlayer.displayName.Contains(nameOrIdOrIp))
                    players.Add(activePlayer);
                else if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress.Split(new char [] {':'})[0] == nameOrIdOrIp)
                    players.Add(activePlayer);
            }
			
			if (players.Count > 0)
				return players;
			
            return null;
        }
		
		private void SendReply(BasePlayer player, Action<string> cbSendVkAnswer, string message)
		{
			if (cbSendVkAnswer == null)
			{
				if (player != null)
					SendReply(player, message);
			}
			else				
				cbSendVkAnswer(message);
		}
		
		#endregion
		
		#region Commands							
		
		[ChatCommand("isafk")]
        private void CmdChatIsAFK(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;                                
            if (!HasPermission(player)) return;                    
                        
			if (args == null || args.Length < 1)
			{
				SendReply(player, "Использование: /isafk <SteamID|Имя> [секунды]");
				return;
			}
						
			var plylist = FindPlayers(args[0]);
			
			if (plylist == null) 
			{
				SendReply(player, "Игрок не найден.");
				return;
			}
			else if (plylist.Count > 1) 
			{
				var players = "Найдено несколько игроков с похожим ником: ";
				foreach (var banned in plylist)				
					players = players+"\n"+banned.displayName+" - "+banned.UserIDString;
				
				SendReply(player, players);
				return;
			}						
			
			int sec = 10;
			if (args.Length >= 2)
			{
				if (!Int32.TryParse(args[1], out sec))
					sec = 10;
			}	
			
			if (sec > 60)
			{
				SendReply(player, "Нельзя указывать больше 60 секунд.");
				return;
			}											
			
			CheckAFK(player.userID, plylist[0].userID, sec, null);
        }				
		
		#endregion
		
		#region Commands API
		
		private void API_CheckAFK(ulong moderID, string user, int seconds, Action<string> cbSendVkAnswer)
		{
			if (!HasPermission(moderID))
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
			
			if (plylist == null) 
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
			
			int sec = 10;
			if (seconds > 0)						
				sec = seconds;
			
			if (sec > 60)
			{
				cbSendVkAnswer("Нельзя указывать больше 60 секунд.");
				return;
			}											
			
			CheckAFK(moderID, plylist[0].userID, sec, cbSendVkAnswer);
		}
		
		#endregion
		
		#region Main
		
		private void CheckAFK(ulong userID, ulong targetID, int sec, Action<string> cbSendVkAnswer)
		{
			var player = BasePlayer.activePlayerList.FirstOrDefault(x=> x.userID == userID);
			
			if (AFKData.ContainsKey(userID))
			{
				SendReply(player, cbSendVkAnswer, "Дождитесь окончания работы старого запроса.");
				return;
			}
			
			var target = BasePlayer.FindByID(targetID);
			
			if (target == null)
			{				
				SendReply(player, cbSendVkAnswer, "Указанный игрок отключён.");									
				return;
			}	
			
			var pos = new Vector3((float)Math.Truncate(target.transform.position.x), (float)Math.Truncate(target.transform.position.y), (float)Math.Truncate(target.transform.position.z));
			AFKData.Add(userID, new AFKInfo() { player = target, pos = pos, wait = sec } );			
			CheckUserAFK(player, userID, cbSendVkAnswer);
			SendReply(player, cbSendVkAnswer, "Начинаем отслеживать движения игрока.");
		}
		
		private void CheckUserAFK(BasePlayer player, ulong userID, Action<string> cbSendVkAnswer)
		{					
			if (cbSendVkAnswer == null && (player == null || !player.IsConnected))
			{				
				AFKData.Remove(userID);
				return;
			}	
			
			var afk = AFKData[userID];			
			
			if (afk.wait <= 0)
			{
				AFKData.Remove(userID);
				SendReply(player, cbSendVkAnswer, "Указанный игрок за заданное время не двигался.");
				return;
			}	
			
			if (afk.player == null || !afk.player.IsConnected)
			{
				AFKData.Remove(userID);
				SendReply(player, cbSendVkAnswer, "Указанный игрок отключился.");
				return;
			}
			
			var pos = new Vector3((float)Math.Truncate(afk.player.transform.position.x), (float)Math.Truncate(afk.player.transform.position.y), (float)Math.Truncate(afk.player.transform.position.z));
			
			if (pos != afk.pos)
			{
				AFKData.Remove(userID);
				SendReply(player, cbSendVkAnswer, "Указанный игрок в движении.");
				return;
			}

			afk.wait--;
			timer.Once(1f, ()=> CheckUserAFK(player, userID, cbSendVkAnswer));
		}
		
		#endregion					
						
	}
	
}	