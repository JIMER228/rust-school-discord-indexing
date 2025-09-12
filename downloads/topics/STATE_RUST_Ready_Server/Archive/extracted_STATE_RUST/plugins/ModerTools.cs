using System;
using System.Linq;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using Rust;
using Newtonsoft.Json;
using Network;

namespace Oxide.Plugins
{
    [Info("ModerTools", "Nimant", "1.0.8")]
    public class ModerTools : RustPlugin
    {        					
				
		#region Variables

		[PluginReference]
        private Plugin Friends;
		
		#endregion		
				
		#region Commands		
				
		[ConsoleCommand("users")]
        private void CcmdShowUsers(ConsoleSystem.Arg arg)
        {
			if (arg==null) return;
			
            var player = arg.Player();
			if (player != null && !IsHasPrivs(player, "full"))
			{
				PrintToConsole(player, "Недостаточно прав !");
				return;
			}							
			
			int rows = -1;
			int page = -1;
			
			if (arg?.Args != null && arg?.Args.Length == 2)
			{
				page = Convert.ToInt32(arg.Args[0]);
				rows = Convert.ToInt32(arg.Args[1]);				
			}
			
			if (page >= 0 && rows >= 0)
			{
				if (player != null)
					PrintToConsole(player, $"Список онлайн игроков (страница {page}, строк {rows}):");
				else
					Puts($"Список онлайн игроков (страница {page}, строк {rows}):");
			}
			else
			{
				if (player != null)
					PrintToConsole(player, "Список онлайн игроков:");
				else
					Puts("Список онлайн игроков:");
			}
			
			int count = 0, realCount = 0;
			string line = "";			
			foreach (var player_ in BasePlayer.activePlayerList.OrderBy(x=>x.displayName))
			{				
				if (page >= 0 && rows >= 0)
				{					
					if ((count >= rows * (page-1)) && (count < rows * page))
					{
						line = $"{player_.userID}:\"{player_.displayName}\"";
						if (player != null)
							PrintToConsole(player, line);
						else
							Puts(line);						
						realCount++;
					}
				}
				else
				{
					line = $"{player_.userID}:\"{player_.displayName}\"";
					if (player != null)
						PrintToConsole(player, line);
					else
						Puts(line);
					realCount++;
				}												
			
				count++;
			}						
			
			if (player != null)
				PrintToConsole(player, $"Отображено {realCount} игроков.");
			else
				Puts($"Отображено {realCount} игроков.");	
		}
		
		[ConsoleCommand("online")]
        private void CcmdShowUsers2(ConsoleSystem.Arg arg)
        {
			if (arg==null) return;
			
            var player = arg.Player();
			if (player != null && !IsHasPrivs(player, "full"))
			{
				PrintToConsole(player, "Недостаточно прав !");
				return;
			}		
			
			TextTable textTable = new TextTable();
            textTable.AddColumn("Steam ID");
            textTable.AddColumn("Имя");
            textTable.AddColumn("Пинг");            
            foreach (BasePlayer basePlayer in BasePlayer.activePlayerList)
            {
                string userIDString = basePlayer.UserIDString;
                string str = basePlayer.displayName.ToString();
                if (str.Length >= 14)
                {
                    str = string.Concat(str.Substring(0, 14), "..");
                }
                string str1 = str;
                int averagePing = Net.sv.GetAveragePing(basePlayer.net.connection);
                string str2 = averagePing.ToString();                
                textTable.AddRow(new string[] { userIDString, str1, str2 });
            }
            arg.ReplyWith(textTable.ToString());			
		}
		
		[ConsoleCommand("teamview")]
        private void CcmdShowTeam(ConsoleSystem.Arg arg)		
        {
			if (arg==null) return;
			
            var player = arg.Player();
			if (player != null && !IsHasPrivs(player, "full"))
			{
				PrintToConsole(player, "Недостаточно прав !");
				return;
			}
			
            ulong num = arg.GetUInt64(0, (ulong)0);
            if (num == 0)
            {
                BasePlayer player_ = arg.GetPlayer(0);
                if (player_ == null)                
				{
                    arg.ReplyWith("Игрок не найден");
					return;
				}
                
                num = player_.userID;
            }
			
            RelationshipManager.PlayerTeam playerTeam = RelationshipManager.Instance.FindPlayersTeam(num);
            if (playerTeam == null)
			{
                arg.ReplyWith("Игрок не состоит в команде");
				return;
			}
            
            TextTable textTable = new TextTable();
            textTable.AddColumn("Steam ID");
            textTable.AddColumn("Имя");
            textTable.AddColumn("Онлайн");
            textTable.AddColumn("Лидер");
			
            foreach (ulong member in playerTeam.members)
            {
                bool flag = Net.sv.connections.FirstOrDefault<Connection>((Connection c) => {
                    if (!c.connected)                    
                        return false;
                    
                    return c.userid == member;
                }) != null;
                TextTable textTable1 = textTable;
                string[] str = new string[] { member.ToString(), GetPlayerName(member), null, null };
                str[2] = (flag ? "x" : "");
                str[3] = (member == playerTeam.teamLeader ? "x" : "");
                textTable1.AddRow(str);
            }
			
            arg.ReplyWith(textTable.ToString());
        }
		
		[ConsoleCommand("friendsview")]
        private void CcmdShowFriends(ConsoleSystem.Arg arg) => CcmdShowFriend(arg);
		
		[ConsoleCommand("friendview")]
        private void CcmdShowFriend(ConsoleSystem.Arg arg)
        {
			if (arg==null) return;
			
            var player = arg.Player();
			if (player != null && !IsHasPrivs(player, "full"))
			{
				PrintToConsole(player, "Недостаточно прав !");
				return;
			}
			
            ulong num = arg.GetUInt64(0, (ulong)0);
            if (num == 0)
            {
                BasePlayer player_ = arg.GetPlayer(0);
                if (player_ == null)                
				{
                    arg.ReplyWith("Игрок не найден");
					return;
				}
                
                num = player_.userID;
            }
			
            var friends = Friends?.Call("GetFriends", num) as ulong[];
			
            if (friends == null)
			{
                arg.ReplyWith("Игрок не имеет друзей");
				return;
			}
            
            TextTable textTable = new TextTable();
            textTable.AddColumn("Steam ID");
            textTable.AddColumn("Имя");
            textTable.AddColumn("Онлайн");
			
            foreach (ulong member in friends)
            {
                bool flag = Net.sv.connections.FirstOrDefault<Connection>((Connection c) => {
                    if (!c.connected)                    
                        return false;
                    
                    return c.userid == member;
                }) != null;
                TextTable textTable1 = textTable;
                string[] str = new string[] { member.ToString(), GetPlayerName(member), null, null };
                str[2] = (flag ? "x" : "");                
                textTable1.AddRow(str);
            }
			
            arg.ReplyWith(textTable.ToString());
        }

		[ConsoleCommand("rename")]
        private void CcmdRename(ConsoleSystem.Arg arg)
        {
			if (arg==null) return;
			
            var player = arg.Player();
			if (player != null && !IsHasPrivs(player, "chat"))
			{
				PrintToConsole(player, "Недостаточно прав !");
				return;
			}							
				
			if (arg == null || arg.Args == null || arg.Args.Count() == 0)
			{
				PrintToConsole(player, "Используйте: rename <Steam ID|Имя> <новое имя>");
				return;
			}	
				
			ulong steamid = 0;			
			if (!UInt64.TryParse(arg.Args[0], out steamid))
			{
				var players = FindPlayers(arg.Args[0]);
				
				if (players.Count == 0)
				{
					arg.ReplyWith("Указанный игрок не найден, либо он отключен. Для более точного поиска попробуйте указать его Steam ID.");
					return;
				}
				
				if (players.Count > 1)
				{
					var playersTxt = "Найдено несколько игроков с похожим именем: ";
					foreach (var plr in players)					
						playersTxt = playersTxt + "\n" + plr.displayName + " - " + plr.UserIDString;
					
					arg.ReplyWith(playersTxt);
					return;
				}
				
				steamid = players[0].userID;				
			}
			
			string name = "";
			for (int ii=1; ii < arg.Args.Count(); ii++) name += arg.Args[ii] + " ";
	
			name = name.Trim(' ');
			
			if (string.IsNullOrEmpty(name))
			{
				arg.ReplyWith("Неверный аргумент, используйте: rename <Steam ID|Имя> <новое имя>");
				return;
			}
			
			var target = BasePlayer.FindByID(steamid);
			
			if (target == null)
			{
				arg.ReplyWith("Игрок не найден или отключён.");
				return;
			}
			
			target.IPlayer.Rename(name);
			arg.ReplyWith("Игрок успешно переименован.");
		}	
		
		[ChatCommand("rename")]
        private void cmdRename(BasePlayer player, string command, string[] args)
        {
			if (player==null) return;
			            
			if (!IsHasPrivs(player, "chat"))
			{
				SendReply(player, "Недостаточно прав !");
				return;
			}							
				
			if (args.Length == 0)
			{
				SendReply(player, "Используйте: /rename <Steam ID|Имя> <новое имя>");
				return;
			}	
				
			ulong steamid = 0;			
			if (!UInt64.TryParse(args[0], out steamid))
			{
				var players = FindPlayers(args[0]);
				
				if (players.Count == 0)
				{
					SendReply(player, "Указанный игрок не найден, либо он отключен. Для более точного поиска попробуйте указать его Steam ID.");
					return;
				}
				
				if (players.Count > 1)
				{
					var playersTxt = "Найдено несколько игроков с похожим именем: ";
					foreach (var plr in players)					
						playersTxt = playersTxt + "\n" + plr.displayName + " - " + plr.UserIDString;
					
					SendReply(player, playersTxt);
					return;
				}
				
				steamid = players[0].userID;				
			}
			
			string name = "";
			for (int ii=1; ii < args.Length; ii++) name += args[ii] + " ";
	
			name = name.Trim(' ');
			
			if (string.IsNullOrEmpty(name))
			{
				SendReply(player, "Неверный аргумент, используйте: /rename <Steam ID|Имя> <новое имя>");
				return;
			}
			
			var target = BasePlayer.FindByID(steamid);
			
			if (target == null)
			{
				SendReply(player, "Игрок не найден или отключён.");
				return;
			}
			
			target.IPlayer.Rename(name);
			SendReply(player, "Игрок успешно переименован.");
		}
		
		[ConsoleCommand("skick")]
        private void CcmdSKick(ConsoleSystem.Arg arg)
        {
			if (arg==null) return;
			
            var player = arg.Player();
			if (player != null && !player.IsAdmin)
			{
				PrintToConsole(player, "Недостаточно прав !");
				return;
			}							
				
			if (arg == null || arg.Args == null || arg.Args.Count() == 0)
			{
				PrintToConsole(player, "Используйте: skick <Steam ID> <причина>");
				return;
			}	
				
			ulong steamid = 0;			
			if (!UInt64.TryParse(arg.Args[0], out steamid))
			{
				arg.ReplyWith("Неверный аргумент, используйте: skick <Steam ID> <причина>");
				return;
			}
			
			string reason = "";
			for (int ii=1; ii < arg.Args.Length; ii++) reason += arg.Args[ii] + " ";
	
			reason = reason.Trim(' ');						
			
			var target = BasePlayer.FindByID(steamid);
			
			if (target == null)
			{
				SendReply(player, "Игрок не найден или отключён.");
				return;
			}
			
			if (!string.IsNullOrEmpty(reason))
				target.Kick(reason);
			else
				target.Kick("");

			PrintToConsole(player, "Игрок успешно кикнут.");
		}					
		
		[ChatCommand("stash")]
        private void cmdStash(BasePlayer player, string command, string[] args)
        {
			if (player == null || !player.IsAdmin) return;			            			
				
			if (args?.Length == 0)
			{
				SendReply(player, "Используйте: /stash <Steam ID>");
				return;
			}	
				
			ulong steamid = 0;			
			if (!UInt64.TryParse(args[0], out steamid))
			{				
				SendReply(player, "Неверный SteamID.");
				return;				
			}
			
			var result = "";
			foreach (var ent in BaseNetworkable.serverEntities.OfType<StashContainer>().Where(x=> x != null && x.OwnerID == steamid))			
				result += " * " + ent.transform.position.ToString() + "\n";
			
			if (string.IsNullOrEmpty(result))
				SendReply(player, "У игрока нет своих стешей");
			else
				SendReply(player, "Найденные стеши игрока:\n\n" + result);
		}
		
		[ConsoleCommand("inventory.copyto")]        
        private void CmdInvCopyTo(ConsoleSystem.Arg arg) => CmdCopyTo(arg);
		
		[ConsoleCommand("copyto")]        
        private void CmdCopyTo(ConsoleSystem.Arg arg)
        {
			if (arg == null) return;
            var basePlayer = arg.Player();
            if (basePlayer == null || (!basePlayer.IsAdmin && !basePlayer.IsDeveloper))
                return;
                        
            BasePlayer lookingAtPlayer = null;
            if (!arg.HasArgs(1) || !(arg.GetString(0, "").ToLower() != "true"))            
                lookingAtPlayer = RelationshipManager.GetLookingAtPlayer(basePlayer);            
            else
            {
                lookingAtPlayer = arg.GetPlayer(0);
                if (lookingAtPlayer == null)
                {
                    uint num = arg.GetUInt(0, 0);
                    lookingAtPlayer = BasePlayer.FindByID((ulong)num);
                    if (lookingAtPlayer == null)                    
                        lookingAtPlayer = BasePlayer.FindBot((ulong)num);                    
                }
            }
            if (lookingAtPlayer == null)            
                return;
            
            lookingAtPlayer.inventory.containerBelt.Clear();
            lookingAtPlayer.inventory.containerWear.Clear();
            int num1 = 0;
			
            foreach (Item item in basePlayer.inventory.containerBelt.itemList)
            {
                lookingAtPlayer.inventory.containerBelt.AddItem(item.info, item.amount, item.skin);
                if (item.contents != null)
                {
                    Item item1 = lookingAtPlayer.inventory.containerBelt.itemList[num1];
                    foreach (Item content in item.contents.itemList)                    
                        item1.contents.AddItem(content.info, content.amount, content.skin);                    
                }
                num1++;
            }
            foreach (Item item2 in basePlayer.inventory.containerWear.itemList)            
                lookingAtPlayer.inventory.containerWear.AddItem(item2.info, item2.amount, item2.skin);
                                    
            basePlayer.ChatMessage(string.Concat("you silently copied items to ", lookingAtPlayer.displayName));            
        }
		
		#endregion
		
		#region API
		
		private void API_TeamView(ulong moderID, string user, Action<string> cbSendVkAnswer)
		{
			if (!IsHasPrivs(moderID, "full"))
			{
				cbSendVkAnswer("Недостаточно прав !");
				return;
			}
			
			if(string.IsNullOrEmpty(user))
			{
				cbSendVkAnswer("Вы не указали игрока.");
				return;
			}
			
			ulong num = user.IsSteamId() ? (ulong)Convert.ToInt64(user) : 0;
            if (num == 0)
            {                
                cbSendVkAnswer("Неверный Steam ID");
				return;
            }
			
            RelationshipManager.PlayerTeam playerTeam = RelationshipManager.Instance.FindPlayersTeam(num);
            if (playerTeam == null)
			{
                cbSendVkAnswer("Игрок не состоит в команде");
				return;
			}
            
			var result = "Состав команды игрока:\n";
			
            foreach (ulong member in playerTeam.members)            
                result += $"{FixName(GetPlayerName(member))} ({member.ToString()})\n";            
			
            cbSendVkAnswer(result.TrimEnd('\n'));
		}
		
		private void API_FriendView(ulong moderID, string user, Action<string> cbSendVkAnswer)
		{
			if (!IsHasPrivs(moderID, "full"))
			{
				cbSendVkAnswer("Недостаточно прав !");
				return;
			}
			
			if(string.IsNullOrEmpty(user))
			{
				cbSendVkAnswer("Вы не указали игрока.");
				return;
			}
			
			ulong num = user.IsSteamId() ? (ulong)Convert.ToInt64(user) : 0;
            if (num == 0)
            {                
                cbSendVkAnswer("Неверный Steam ID");
				return;
            }	
			
            var friends = Friends?.Call("GetFriends", num) as ulong[];
            if (friends == null)
			{
                cbSendVkAnswer("Игрок не имеет друзей");
				return;
			}
            
			var result = "Состав друзей игрока:\n";
			
            foreach (ulong member in friends)            
                result += $"{FixName(GetPlayerName(member))} ({member.ToString()})\n";
			
            cbSendVkAnswer(result.TrimEnd('\n'));
		}
		
		#endregion
		
		#region Common
		
		private static string FixName(string name) => name.Replace("&","_").Replace("#","_").Replace("?","_");
		
		private bool IsHasPrivs(BasePlayer player, string level)
		{
			if (player == null) return false;			
			if (player.IsAdmin) return true;
			
			foreach(var group in configData.AllowGroups.Where(x=> x.Value == level))
				if (permission.UserHasGroup(player.UserIDString, group.Key))
					return true;
				
			return false;	
		}
		
		private bool IsHasPrivs(ulong userID, string level)
		{
			foreach(var group in configData.AllowGroups.Where(x=> x.Value == level))
				if (permission.UserHasGroup(userID.ToString(), group.Key))
					return true;
				
			return false;	
		}
		
		private string FixStr(string msg, int len)
		{						
			for(int ii = 0; ii < len-msg.Length; ii++) msg += " ";			
			return msg;
		}
		
		private static List<BasePlayer> FindPlayers(string name)
        {
            var players = new List<BasePlayer>();
			
			foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.displayName == name)
                    players.Add(activePlayer);
            }
			
			if (players.Count > 0)
				return players;
			
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.displayName.Contains(name))
                    players.Add(activePlayer);
            }
            			
            return players;
        }
		
		private string GetPlayerName(ulong userID)
		{
			var data = permission.GetUserData(userID.ToString());															
			return data.LastSeenNickname;
		}
		
		#endregion
		
		#region Config        
		
		private void Init() => LoadVariables();
		
        private static ConfigData configData;
		
        private class ConfigData
        {            
			[JsonProperty(PropertyName = "Список модераторских групп")]
			public Dictionary<string, string> AllowGroups;
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                AllowGroups = new Dictionary<string, string>() { { "moderator", "full"} }
            };
            SaveConfig(configData);
			timer.Once(0.1f, ()=> SaveConfig(configData));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
		
    }
}