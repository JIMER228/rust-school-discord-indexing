using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ConVar;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using UnityEngine.Networking;
using Network;

namespace Oxide.Plugins
{
    [Info("BS", "Nimant", "1.1.14")]
    public class BS : RustPlugin
    {
        #region References

        [PluginReference] private Plugin ModerControl;
		[PluginReference] private Plugin MT;
        
        private static BS bS;
		private static WWWRequests requests;
		private static System.Random Rnd = new System.Random();

        #endregion
        
        #region Variables
        
		private static bool NeedSave = false;
		
        // Значения для конфигурации
        [JsonProperty("Стандартная причина блокировки, если администратор не указал свою")]
        private string DefaultReason = "Нарушение правил сервера!";
        [JsonProperty("Стандартное время блокировки, если амдинистратор не указал своё")]
        private string DefaultTime = "7d";
        [JsonProperty("Стандартное сообщение заблокированному игроку при входе на сервер")]
        private string DefaultMessage = "Вы заблокированы! {0}";

        [JsonProperty("Тотальное разрешение на блокировку любого игрока")]
        private string TotalPermission = "BS.Total";
        [JsonProperty("Разрешение на блокировку игроков")]
        private string BanPermission = "BS.Ban";
		[JsonProperty("Разрешение на кик игроков")]
        private string KickPermission = "BS.Kick";
        [JsonProperty("Разрешение на разблокировку игроков")]
        private string UnbanPermission = "BS.UnBan";
        [JsonProperty("Разрешение на отклонение от блокировки обычным модератором")]
        private string EvadePermission = "BS.Evade";
        

        private class PlayerBan
        {
            [JsonProperty("Отображаемое имя заблокированного игрока")]
            public string DisplayName;
            [JsonProperty("Отображаемый ID заблокированный игрока")]
            public ulong DisplayID;

            [JsonProperty("Администратор заблокировавший игрока")]
            public string AdminInfo;
            [JsonProperty("Причина блокировки игроа")]
            public string Reason;

            [JsonProperty("Время блокировки игрока")]
            public double BanTime;
			
			[JsonProperty("Срок блокировки")]
            public double BanStopTime;

            public PlayerBan(string BName, ulong BID, string AInfo, string BReason, double StopBTime)
            {
                this.DisplayName = BName;
                this.DisplayID = BID;
                this.AdminInfo = AInfo;
                this.Reason = BReason;
                this.BanTime = LogTime();
				this.BanStopTime = StopBTime;
            }
        }
        
        private static Dictionary<ulong, PlayerBan> playerBans = new Dictionary<ulong,PlayerBan>();

        #endregion

        #region Hooks

		private object CanUserLogin(string userName, string id, string ip)
		{
			if (!id.IsSteamId()) return null;
		
			ulong userID = (ulong)Convert.ToInt64(id), ownerID = 0;
			
			var conn = Network.Net.sv.connections.FirstOrDefault(x=> x.userid == userID);
			if (conn != null) ownerID = conn.ownerid;			
			if (ownerID == userID) ownerID = 0;
			
			if (userID > 0 && (!PlayerWaitCheckRequest.ContainsKey(userID) || PlayerWaitCheckRequest.ContainsKey(userID) && (ToEpochTime(DateTime.Now)-PlayerWaitCheckRequest[userID]) >= configData.ReCheckMinutes*60))
			{
				// если у чела есть временный бан, для него проверку на сайте не делаем
				if (!playerBans.ContainsKey(userID) || playerBans.ContainsKey(userID) && playerBans[userID].BanStopTime == 0)
				{
					CheckBan(userID, ownerID, ip);
					return null;
				}
			}
			
			if (ownerID > 0 && (!PlayerWaitCheckRequest.ContainsKey(ownerID) || PlayerWaitCheckRequest.ContainsKey(ownerID) && (ToEpochTime(DateTime.Now)-PlayerWaitCheckRequest[ownerID]) >= configData.ReCheckMinutes*60))
			{
				// если у чела есть временный бан, для него проверку на сайте не делаем
				if (!playerBans.ContainsKey(ownerID) || playerBans.ContainsKey(ownerID) && playerBans[ownerID].BanStopTime == 0)
				{
					CheckBan(userID, ownerID, ip);
					return null;
				}
			}
			
			if (userID > 0 && playerBans.ContainsKey(userID))
            {    
				PlayerBan ban = playerBans[userID];
				
				if (ban.BanStopTime > 0 && (ban.BanTime + ban.BanStopTime) < LogTime())
				{
					playerBans.Remove(userID);
					NeedSave = true;
					return null;
				}
				
				return DefaultMessage.Replace("{0}", ban.Reason);            
			}
			
			if (ownerID > 0 && playerBans.ContainsKey(ownerID))
            {    
				PlayerBan ban = playerBans[ownerID];
				
				if (ban.BanStopTime > 0 && (ban.BanTime + ban.BanStopTime) < LogTime())
				{
					playerBans.Remove(ownerID);
					NeedSave = true;
					return null;
				}
				
				return DefaultMessage.Replace("{0}", ban.Reason);            
			}
			
			return null;
		}
		
		private void OnPlayerConnected(BasePlayer player) 
		{
			if (player == null) return;			
            if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.Once(0.1f, () => OnPlayerConnected(player));
                return;
            }
			
			var ban = playerBans.ContainsKey(player.userID) ? playerBans[player.userID] : (playerBans.ContainsKey(player.net.connection.ownerid) ? playerBans[player.net.connection.ownerid] : (PlayerBan)null);
			
			if (ban != null)
                player.Kick(DefaultMessage.Replace("{0}", ban.Reason));
		}

        #endregion

        #region Initialization
		
		private void Init() => LoadVariables();

        private void OnServerInitialized()
        {
			InProgress.Clear();
			requests = new GameObject("WebObject").AddComponent<WWWRequests>();
			
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("BS/BanList"))
            {
                playerBans = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerBan>>("BS/BanList");
                //PrintWarning("Список заблокированных игроков успешно загружен!");
            }
            
            permission.RegisterPermission(TotalPermission, this);
            permission.RegisterPermission(BanPermission, this);
			permission.RegisterPermission(KickPermission, this);
            permission.RegisterPermission(UnbanPermission, this);
            permission.RegisterPermission(EvadePermission, this);
            //PrintWarning("Список разрешений зарегистрирован в системе!");

            foreach (var check in playerBans.Where(p => BasePlayer.FindByID(p.Key) != null))
            {
                // TODO: Проверка на истекающий бан
                BasePlayer.Find(check.ToString()).Kick(DefaultMessage.Replace("{0}", check.Value.Reason));
            }
			
			RunCheckLogonPlayer();
        }				
        
        private void Unload() => SaveData();
		
		private void OnServerSave() => SaveData();
		
		private void SaveData() 
		{
			if (NeedSave) 
			{
				Interface.Oxide.DataFileSystem.WriteObject("BS/BanList", playerBans);
				NeedSave = false;
			}
		}

        #endregion
		
		#region Body
		
		private void BanPlayerNoSync(ulong userID, string reason)
		{
			if (userID <= 0 || playerBans.ContainsKey(userID)) return;                        							
						
			if (string.IsNullOrEmpty(reason))
				reason = DefaultReason;					            
            
			var name = GetPlayerName(userID).name;
			
            playerBans.Add(userID, new PlayerBan(name, userID, "SERVER", reason, 0));
			NeedSave = true;
            
			Kick(userID, reason);			
						
			Puts("Игрок заблокирован! (синхронизация)");
			Log($"Синхронизация бана через сайт, был заблокирован игрок {name} ({userID})");
		}
		
		private void UnBanPlayerNoSync(ulong userID)
		{			
            PlayerBan target = null;			
            foreach (var check in playerBans.Where(p => p.Value.DisplayID == userID))
			{
                target = check.Value;
				break;
			}

            if (target == null) return;
			
            playerBans.Remove(target.DisplayID);
			NeedSave = true;
			
            Puts("Игрок успешно разблокирован на сервере! (синхронизация)");									
			Log($"Синхронизация разбана через сайт, был разблокирован игрок {GetPlayerName(userID).name} ({userID})");
		}
		
		// это кик для бана
		private void Kick(ulong userID, string reason)
		{
			var conn = Network.Net.sv.connections.FirstOrDefault(x=> x.userid == userID);
			if (conn != null)
				Network.Net.sv.Kick(conn, DefaultMessage.Replace("{0}", reason));	
		}
		
		#endregion

        #region Commands
        
        [ChatCommand("ban")]
        private void cmdChatBan(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, TotalPermission) && !permission.UserHasPermission(player.UserIDString, BanPermission))
            {
                player.ChatMessage("У вас недостаточно привилегий!");
                return;
            }

            if (args.Length == 0) { player.ChatMessage("Используйте: /ban ID/Nick [время бана] <причина>"); return; }
            
            List<BasePlayer> probablyPlayers = FindPlayer(args[0]);
			
			ulong userID = 0;
			bool isUlong = false;
			UUser uInfo = null;									
			
			if (ulong.TryParse(args[0], out userID))
			{
				isUlong = true;
				uInfo = GetPlayerName(userID);
			}
			
            if (probablyPlayers.Count == 0 && !isUlong) // не нашли по имени
            {
                player.ChatMessage("Мы не нашли игрока по вашему запросу!");
                return;
            }

            if (probablyPlayers.Count > 1)
            {
                string result = "Мы нашли несколько игроков по вашему запросу:";
                foreach (var check in probablyPlayers)
                    result += $"\n - {check.displayName} [{check.userID}]";
                player.ChatMessage(result);
                return;
            }

			if (probablyPlayers.Count == 1)
			{
				uInfo = new UUser();
				uInfo.name = probablyPlayers[0].displayName;
				uInfo.userID = probablyPlayers[0].userID;				
			}
			
			if (uInfo == null && isUlong)
			{
                uInfo = new UUser();
				uInfo.name = "N|A";
				uInfo.userID = userID;	
            }	
			
            if (playerBans.ContainsKey(uInfo.userID))
            {
                player.ChatMessage("Игрок уже заблокирован!");
				
				var data = playerBans[uInfo.userID];				
				var target2 = BasePlayer.Find(uInfo.userID.ToString());				
				
				string addr2 = target2?.net?.connection?.ipaddress;
				if (!string.IsNullOrEmpty(addr2))			
					addr2 = addr2.Substring(0, addr2.IndexOf(":"));
				
				SendBan(data.DisplayID, !string.IsNullOrEmpty(data.Reason) ? data.Reason : "", (long)Math.Round(data.BanStopTime), player.userID, addr2, target2?.net?.connection?.ownerid > 0 ? target2.net.connection.ownerid : 0);				
                return;
            }

			double timeBan = 0;
			if (args.Length > 1)			
				timeBan = GetTimeBan(args[1]);							
			
			string reason = "";
			if (timeBan > 0)
			{			
				for(int ii=0;ii<args.Length;ii++) 
				{
					if (ii < 2) continue;
					reason += args[ii] + " ";
				}
				reason = reason.Trim(' ');
			}
			else
			{				
				for(int ii=0;ii<args.Length;ii++) 
				{
					if (ii < 1) continue;
					reason += args[ii] + " ";
				}
				reason = reason.Trim(' ');
			}
			
			if (string.IsNullOrEmpty(reason))
				reason = DefaultReason;						
            
            playerBans.Add(uInfo.userID, new PlayerBan(uInfo.name, uInfo.userID, player.displayName, reason, timeBan));            
			NeedSave = true;
			
			var target = BasePlayer.Find(uInfo.userID.ToString());
            if (target != null)
                target.Kick(DefaultMessage.Replace("{0}", playerBans[uInfo.userID].Reason));			
			
			MT?.Call("API_AddResults", player.userID, uInfo.userID, 2);
            ModerControl?.Call("API_SendResult", player.userID, uInfo.userID, 1);
			
			if (timeBan > 0)
				player.ChatMessage("Игрок успешно временно заблокирован на сервере!");
			else
				player.ChatMessage("Игрок успешно заблокирован на сервере!");						
			
			string addr = target?.net?.connection?.ipaddress;
			if (!string.IsNullOrEmpty(addr))			
				addr = addr.Substring(0, addr.IndexOf(":"));
			
			SendBan(uInfo.userID, !string.IsNullOrEmpty(reason) ? reason : "", (long)Math.Round(timeBan), player.userID, addr, target?.net?.connection?.ownerid > 0 ? target.net.connection.ownerid : 0);
			
			Log($@"Модератор {player.displayName} ({player.userID}) {timeBan > 0 ? "временно " : ""}заблокировал игрока {uInfo.name} ({uInfo.userID})");
        }
        
        [ChatCommand("kick")]
        private void cmdChatKick(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, TotalPermission) && !permission.UserHasPermission(player.UserIDString, KickPermission))
            {
                player.ChatMessage("У вас недостаточно привилегий!");
                return;
            }

            if (args.Length == 0) { player.ChatMessage("Используйте: /kick ID/Nick <причина>"); return; }
            
            List<BasePlayer> probablyPlayers = FindPlayer(args[0], true);
			
            if (probablyPlayers.Count == 0)
            {
                player.ChatMessage("Мы не нашли игрока по вашему запросу!");
                return;
            }

            if (probablyPlayers.Count > 1)
            {
                string result = "Мы нашли несколько игроков по вашему запросу:";
                foreach (var check in probablyPlayers)
                    result += $"\n - {check.displayName} [{check.userID}]";
                player.ChatMessage(result);
                return;
            }

            BasePlayer target = probablyPlayers[0];

            string reason = args.Length != 2 ? DefaultReason : args[1];
            
            if (BasePlayer.Find(target.UserIDString) != null)
                target.Kick(reason);

            //MT.Call("API_AddResults", player.userID, target.userID, 2);
            player.ChatMessage("Игрок успешно кикнут с сервера!");
			
			var name = target != null ? target.displayName : "N|A";
			
			Log($"Модератор {player.displayName} ({player.userID}) кикнул игрока {name} ({target.UserIDString})");
        }

        [ConsoleCommand("bs.unban")]
        private void consoleUnBan(ConsoleSystem.Arg args)
        {
            if (args.Player() != null)
            {
                PrintWarning("Данная команда предназначена для консоли");
                return;
            }
            
            if (args == null || args?.Args == null || args.Args.Length == 0) { Puts("Используйте: /unban ID/Nick"); return; }
            
			var targetName = args.Args[0];
			ulong targetID = 0;
			try { targetID = Convert.ToUInt64(args.Args[0]); } catch { targetID = 0; };
			
            PlayerBan target = null;			
            foreach (var check in playerBans.Where(p => p.Value.DisplayName.ToLower().Contains(targetName.ToLower()) || p.Value.DisplayID == targetID))
			{
                target = check.Value;
				break;
			}

            if (target == null)
            {
                Puts("Игрок не заблокирован на сервере!");
				
				if (targetID > 0)
					SendUnBan(targetID, 0);
				
                return;
            }
			
			ModerControl?.Call("API_SendResult", (ulong)0, target.DisplayID, 2);
			
            playerBans.Remove(target.DisplayID);
			NeedSave = true;
            Puts("Игрок успешно разблокирован на сервере!");
			
			SendUnBan(target.DisplayID, 0);
			
			Log($"Администратор через консоль разблокировал игрока {target.DisplayName} ({target.DisplayID})");
        }                

        [ConsoleCommand("bs.ban")]
        private void consoleBan(ConsoleSystem.Arg args)
        {
            if (args?.Player() != null)
            {
                PrintWarning("Данная команда предназначена для консоли");
                return;
            }            			
			
            if (args == null || args?.Args == null || args?.Args.Length == 0) { Puts("Используйте: /ban ID/Nick [время бана] <причина>"); return; }						
            
            List<BasePlayer> probablyPlayers = FindPlayer(args.Args[0]);
            			
			ulong userID = 0;
			bool isUlong = false;
			UUser uInfo = null;
			if (ulong.TryParse(args.Args[0], out userID))
			{
				isUlong = true;
				uInfo = GetPlayerName(userID);
			}
			
            if (probablyPlayers.Count == 0 && !isUlong) // не нашли по имени
            {
                Puts("Мы не нашли игрока по вашему запросу!");
                return;
            }

            if (probablyPlayers.Count > 1)
            {
                string result = "Мы нашли несколько игроков по вашему запросу:";
                foreach (var check in probablyPlayers)
                    result += $"\n - {check.displayName} [{check.userID}]";
                Puts(result);
                return;
            }

			if (probablyPlayers.Count == 1)
			{
				uInfo = new UUser();
				uInfo.name = probablyPlayers[0].displayName;
				uInfo.userID = probablyPlayers[0].userID;				
			}
			
			if (uInfo == null && isUlong)
			{
                uInfo = new UUser();
				uInfo.name = "N|A";
				uInfo.userID = userID;	
            }	
			
            if (playerBans.ContainsKey(uInfo.userID))
            {
                Puts("Игрок уже заблокирован!");
				
				var data = playerBans[uInfo.userID];				
				var target2 = BasePlayer.Find(uInfo.userID.ToString());				
				
				string addr2 = target2?.net?.connection?.ipaddress;
				if (!string.IsNullOrEmpty(addr2))			
					addr2 = addr2.Substring(0, addr2.IndexOf(":"));
				
				SendBan(data.DisplayID, !string.IsNullOrEmpty(data.Reason) ? data.Reason : "", (long)Math.Round(data.BanStopTime), 0, addr2, target2?.net?.connection?.ownerid > 0 ? target2.net.connection.ownerid : 0);
				
                return;
            }

			double timeBan = 0;
			if (args.Args.Length > 1)			
				timeBan = GetTimeBan(args.Args[1]);							
			
			string reason = "";
			if (timeBan > 0)
			{			
				for(int ii=0;ii<args.Args.Length;ii++) 
				{
					if (ii < 2) continue;
					reason += args.Args[ii] + " ";
				}
				reason = reason.Trim(' ');
			}
			else
			{				
				for(int ii=0;ii<args.Args.Length;ii++) 
				{
					if (ii < 1) continue;
					reason += args.Args[ii] + " ";
				}
				reason = reason.Trim(' ');
			}
			
			if (string.IsNullOrEmpty(reason))
				reason = DefaultReason;					            
            
            playerBans.Add(uInfo.userID, new PlayerBan(uInfo.name, uInfo.userID, "SERVER", reason, timeBan));
			NeedSave = true;
            
			var target = BasePlayer.Find(uInfo.userID.ToString());
            if (target != null)
                target.Kick(DefaultMessage.Replace("{0}", playerBans[uInfo.userID].Reason));            						
			
			ModerControl?.Call("API_SendResult", (ulong)0, uInfo.userID, 1);
			
			if (timeBan > 0)
				Puts("Игрок временно заблокирован!");
			else
				Puts("Игрок заблокирован!");
			
			string addr = target?.net?.connection?.ipaddress;
			if (!string.IsNullOrEmpty(addr))			
				addr = addr.Substring(0, addr.IndexOf(":"));
			
			SendBan(uInfo.userID, !string.IsNullOrEmpty(reason) ? reason : "", (long)Math.Round(timeBan), 0, addr, target?.net?.connection?.ownerid > 0 ? target.net.connection.ownerid : 0);
			
			var name = target != null ? target.displayName : "N|A";
			
			Log($@"Администратор через консоль {timeBan > 0 ? "временно " : ""}заблокировал игрока {name} ({uInfo.userID})");
        }
        
        [ChatCommand("unban")]
        private void cmdChatUnBan(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, TotalPermission) && !permission.UserHasPermission(player.UserIDString, UnbanPermission))
            {
                player.ChatMessage("У вас недостаточно привилегий!");
                return;
            }

            if (args.Length == 0) { player.ChatMessage("Используйте: /unban ID/Nick"); return; }
            
			var targetName = args[0];
			ulong targetID = 0;
			try { targetID = Convert.ToUInt64(args[0]); } catch { targetID = 0; };
			
            PlayerBan target = null;
            foreach (var check in playerBans.Where(p => p.Value.DisplayName.ToLower().Contains(targetName.ToLower()) || p.Value.DisplayID == targetID))
			{
                target = check.Value;
				break;
			}

            if (target == null)
            {
                player.ChatMessage("Игрок не заблокирован на сервере!");
				
				if (targetID > 0)
					SendUnBan(targetID, 0);
				
                return;
            }
						
			ModerControl?.Call("API_SendResult", player.userID, target.DisplayID, 2);

            playerBans.Remove(target.DisplayID);
			NeedSave = true;
            player.ChatMessage("Игрок успешно разблокирован на сервере!");
			
			SendUnBan(target.DisplayID, player.userID);
			
			Log($"Модератор {player.displayName} ({player.userID}) разблокировал игрока {target.DisplayName} ({target.DisplayID})");
        }		
		
        #endregion
		
		#region LoadList
		
		[ConsoleCommand("bs.loadbans")]
        private void consoleLoadBans(ConsoleSystem.Arg args)
        {
            if (args?.Player() != null)
            {
                PrintWarning("Данная команда предназначена для консоли");
                return;
            }          
		
			var users = permission.GetUsersInGroup("default").ToDictionary(x=>x.Substring(0, x.IndexOf(" ")), x=>x.Substring(x.IndexOf(" ")+1, x.Length-x.IndexOf(" ")-1 ).Trim(')', '('));
		
			var syncList = new List<SyncBanData>();
			foreach (var pair in playerBans)
			{
				var tmp = new SyncBanData();
				tmp.steamID = pair.Key;
				tmp.familyShare = 0;
				tmp.bannedBy = GetIdByName(pair.Value.AdminInfo, users);
				tmp.reason = pair.Value.Reason.Replace("\\","|").Replace("@","_").Replace("#","_").Replace("%","_").Replace("^","_").Replace("&","_").Replace("*","_").Replace("/","|").Replace(@"\","|");
				tmp.ip = "0.0.0.0";
				tmp.banTime = (uint)pair.Value.BanStopTime;
				tmp.singleBan = 0;
				
				tmp.reason = !string.IsNullOrEmpty(tmp.reason) ? tmp.reason : "";
				
				syncList.Add(tmp);								
			}            
		
			SendBanPack(syncList);
			Puts("Баны отправляются на сайт.");		
		}
		
		private HashSet<SyncBanData> noSyncBans = new HashSet<SyncBanData>();
		
		private class SyncBanData
		{
		    public ulong steamID;
		    public ulong familyShare;
		    public ulong bannedBy;
		    public string reason;
		    public string ip;
		    public uint banTime;
		    public int singleBan;
		}
		
		#endregion

        #region Functions
		
		private class UUser
		{
			public string name;
			public ulong userID;
		}		
		
		private UUser GetPlayerName(ulong userID)
		{
			var data = permission.GetUserData(userID.ToString());

			if (data.LastSeenNickname == "Unnamed")
				return null;
			
			return new UUser() {name = data.LastSeenNickname, userID = userID};
		}

        private static List<BasePlayer> FindPlayer(string info, bool onlyActive = false)
        {
            List<BasePlayer> probablyPlayers = new List<BasePlayer>();
            
            foreach (var check in BasePlayer.activePlayerList)
            {
                if (check.displayName.ToLower().Contains(info.ToLower()))
                    probablyPlayers.Add(check);
                if (check.UserIDString == info.ToLower())
                    return new List<BasePlayer> {check};
            };
			
			if (onlyActive)
				return probablyPlayers;
			
            foreach (var check in BasePlayer.sleepingPlayerList)
            {
                if (check.displayName.ToLower().Contains(info.ToLower()))
                    probablyPlayers.Add(check);
                if (check.UserIDString == info.ToLower())
                    return new List<BasePlayer> {check};
            };			

            return probablyPlayers;
        }

        #endregion

        #region Helpers

        static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static double LogTime() => DateTime.UtcNow.Subtract(epoch).TotalSeconds;
        
        string FormatTime(TimeSpan time) => $"{(time.Days == 0 ? string.Empty : $"{time.Days} day(s)")}{(time.Days != 0 && time.Hours != 0 ? $", " : string.Empty)}{(time.Hours == 0 ? string.Empty : $"{time.Hours} hour(s)")}{(time.Hours != 0 && time.Minutes != 0 ? $", " : string.Empty)}{(time.Minutes == 0 ? string.Empty : $"{time.Minutes} minute(s)")}{(time.Minutes != 0 && time.Seconds != 0 ? $", " : string.Empty)}{(time.Seconds == 0 ? string.Empty : $"{time.Seconds} second(s)")}";
        
        private bool TryParseTimeSpan(string source, out TimeSpan timeSpan)
        {
            int seconds = 0, minutes = 0, hours = 0, days = 0;

            Match s = new Regex(@"(\d+?)s", RegexOptions.IgnoreCase).Match(source);
            Match m = new Regex(@"(\d+?)m", RegexOptions.IgnoreCase).Match(source);
            Match h = new Regex(@"(\d+?)h", RegexOptions.IgnoreCase).Match(source);
            Match d = new Regex(@"(\d+?)d", RegexOptions.IgnoreCase).Match(source);

            if (s.Success)
                seconds = Convert.ToInt32(s.Groups[1].ToString());

            if (m.Success)
                minutes = Convert.ToInt32(m.Groups[1].ToString());

            if (h.Success)
                hours = Convert.ToInt32(h.Groups[1].ToString());

            if (d.Success)
                days = Convert.ToInt32(d.Groups[1].ToString());

            source = source.Replace(seconds + "s", string.Empty);
            source = source.Replace(minutes + "m", string.Empty);
            source = source.Replace(hours + "h", string.Empty);
            source = source.Replace(days + "d", string.Empty);

            if (!string.IsNullOrEmpty(source) || (!s.Success && !m.Success && !h.Success && !d.Success))
            {
                timeSpan = default(TimeSpan);
                return false;
            }

            timeSpan = new TimeSpan(days, hours, minutes, seconds);

            return true;
        }

		private static bool StringToTime(string source, out TimeSpan time)
		{
			int seconds = 0, minutes = 0, hours = 0, days = 0;
			Match s = new Regex(@"(\d+?)s", RegexOptions.IgnoreCase).Match(source);
			Match m = new Regex(@"(\d+?)m", RegexOptions.IgnoreCase).Match(source);
			Match h = new Regex(@"(\d+?)h", RegexOptions.IgnoreCase).Match(source);
			Match d = new Regex(@"(\d+?)d", RegexOptions.IgnoreCase).Match(source);
			if (s.Success)
				seconds = Convert.ToInt32(s.Groups[1].ToString());
			if (m.Success)
				minutes = Convert.ToInt32(m.Groups[1].ToString());
			if (h.Success)
				hours = Convert.ToInt32(h.Groups[1].ToString());
			if (d.Success)
				days = Convert.ToInt32(d.Groups[1].ToString());
			source = source.Replace(seconds + "s", string.Empty);
			source = source.Replace(minutes + "m", string.Empty);
			source = source.Replace(hours + "h", string.Empty);
			source = source.Replace(days + "d", string.Empty);
			if (!string.IsNullOrEmpty(source) || (!s.Success && !m.Success && !h.Success && !d.Success))
			{
				time = TimeSpan.Zero;
				return false;
			}
			time = new TimeSpan(days, hours, minutes, seconds);
			return true;
		}
		
		private double GetTimeBan(string text)
		{
			TimeSpan time;
			if (StringToTime(text.ToLower(), out time))
            {
				if (time.TotalSeconds <= 0)								
					return 0;					
				
				return time.TotalSeconds;								
            }
			else			
				return 0;				
		}
		
		private string GetCurDate() => "["+DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")+"] ";		
		
		private void Log(string message)
		{
			if (string.IsNullOrEmpty(message)) return;
			
			LogToFile("history", GetCurDate() + message, this, false);
			Puts(message);
		}
		
		private ulong GetIdByName(string name, Dictionary<string, string> users)
		{
			if (string.IsNullOrEmpty(name) || name == "SERVER") return 0;						
			
			ulong foundID = 0;
			int cnt = 0;
			foreach (var pair in users)
			{
				if (pair.Value == name)
				{
					foundID = (ulong)Convert.ToInt64(pair.Key);
					cnt++;
				}
			}
			
			if (cnt == 0 || cnt > 1)
				return 0;
			
			return foundID;
		}
		
		private long ToEpochTime(DateTime dateTime)
        {
            var date = dateTime.ToLocalTime();
            var ticks = date.Ticks - new DateTime(1970, 1, 1, 0, 0, 0, 0).Ticks;
            var ts = ticks / TimeSpan.TicksPerSecond;
            return ts;
        }
		
		private static string FixName(string name) => name.Replace("&","_").Replace("#","_").Replace("?","_");
		
        #endregion
		
		#region API
				        
		private void API_Ban(ulong moderID, string moderName, string user, string[] timeOrAndReason, Action<string> cbSendVkAnswer)
        {
            if (!permission.UserHasPermission(moderID.ToString(), TotalPermission) && !permission.UserHasPermission(moderID.ToString(), BanPermission))
            {
                cbSendVkAnswer("У вас недостаточно привилегий!");
                return;
            }

            if (string.IsNullOrEmpty(user)) { cbSendVkAnswer("Используйте: /ban ID/Nick [время бана] [причина]"); return; }
            
            List<BasePlayer> probablyPlayers = FindPlayer(user);
			
			ulong userID = 0;
			bool isUlong = false;
			UUser uInfo = null;									
			
			if (ulong.TryParse(user, out userID))
			{
				isUlong = true;
				uInfo = GetPlayerName(userID);
			}
			
            if (probablyPlayers.Count == 0 && !isUlong) // не нашли по имени
            {
                cbSendVkAnswer("Мы не нашли игрока по вашему запросу!");
                return;
            }

            if (probablyPlayers.Count > 1)
            {
                string result = "Мы нашли несколько игроков по вашему запросу:";
                foreach (var check in probablyPlayers)
                    result += $"\n - {FixName(check.displayName)} [{check.userID}]";
                cbSendVkAnswer(result);
                return;
            }

			if (probablyPlayers.Count == 1)
			{
				uInfo = new UUser();
				uInfo.name = probablyPlayers[0].displayName;
				uInfo.userID = probablyPlayers[0].userID;				
			}
			
			if (uInfo == null && isUlong)
			{
                uInfo = new UUser();
				uInfo.name = "N|A";
				uInfo.userID = userID;	
            }
			
            if (playerBans.ContainsKey(uInfo.userID))
            {
                cbSendVkAnswer("Игрок уже заблокирован!");
				
				var data = playerBans[uInfo.userID];				
				var target2 = BasePlayer.Find(uInfo.userID.ToString());				
				
				string addr2 = target2?.net?.connection?.ipaddress;
				if (!string.IsNullOrEmpty(addr2))			
					addr2 = addr2.Substring(0, addr2.IndexOf(":"));
				
				SendBan(data.DisplayID, !string.IsNullOrEmpty(data.Reason) ? data.Reason : "", (long)Math.Round(data.BanStopTime), moderID, addr2, target2?.net?.connection?.ownerid > 0 ? target2.net.connection.ownerid : 0);				
                return;
            }			
			
			double timeBan = 0;
			if (timeOrAndReason != null && timeOrAndReason.Length > 0)
				timeBan = GetTimeBan(timeOrAndReason[0]);							
			
			string reason = "";
			if (timeBan > 0)
			{			
				for(int ii=1;ii<timeOrAndReason.Length;ii++) 									
					reason += timeOrAndReason[ii] + " ";
				
				reason = reason.Trim(' ');
			}
			else
			{				
				for(int ii=0;ii<timeOrAndReason.Length;ii++) 									
					reason += timeOrAndReason[ii] + " ";
				
				reason = reason.Trim(' ');
			}
			
			if (string.IsNullOrEmpty(reason))
				reason = DefaultReason;						
            
            playerBans.Add(uInfo.userID, new PlayerBan(uInfo.name, uInfo.userID, moderName, reason, timeBan));            
			NeedSave = true;
			
			var target = BasePlayer.Find(uInfo.userID.ToString());
            if (target != null)
                target.Kick(DefaultMessage.Replace("{0}", playerBans[uInfo.userID].Reason));			
			
			MT?.Call("API_AddResults", moderID, uInfo.userID, 2);
            ModerControl?.Call("API_SendResult", moderID, uInfo.userID, 1);
			
			if (timeBan > 0)
				cbSendVkAnswer("Игрок успешно временно заблокирован на сервере!");
			else
				cbSendVkAnswer("Игрок успешно заблокирован на сервере!");						
			
			string addr = target?.net?.connection?.ipaddress;
			if (!string.IsNullOrEmpty(addr))			
				addr = addr.Substring(0, addr.IndexOf(":"));
			
			SendBan(uInfo.userID, !string.IsNullOrEmpty(reason) ? reason : "", (long)Math.Round(timeBan), moderID, addr, target?.net?.connection?.ownerid > 0 ? target.net.connection.ownerid : 0);
			
			Log($@"Модератор {moderName} ({moderID}) через ВК {timeBan > 0 ? "временно " : ""}заблокировал игрока {uInfo.name} ({uInfo.userID})");
        }
				        
		private void API_UnBan(ulong moderID, string moderName, string user, Action<string> cbSendVkAnswer)
        {
            if (!permission.UserHasPermission(moderID.ToString(), TotalPermission) && !permission.UserHasPermission(moderID.ToString(), UnbanPermission))
            {
                cbSendVkAnswer("У вас недостаточно привилегий!");
                return;
            }

            if (string.IsNullOrEmpty(user)) { cbSendVkAnswer("Используйте: /unban ID/Nick"); return; }
            
			var targetName = user;
			ulong targetID = 0;
			try { targetID = Convert.ToUInt64(user); } catch { targetID = 0; };
			
            PlayerBan target = null;
            foreach (var check in playerBans.Where(p => p.Value.DisplayName.ToLower().Contains(targetName.ToLower()) || p.Value.DisplayID == targetID))
			{
                target = check.Value;
				break;
			}

            if (target == null)
            {
                cbSendVkAnswer("Игрок не заблокирован на сервере!");
				
				if (targetID > 0)
					SendUnBan(targetID, 0);
				
                return;
            }
						
			ModerControl?.Call("API_SendResult", moderID, target.DisplayID, 2);

            playerBans.Remove(target.DisplayID);
			NeedSave = true;
            cbSendVkAnswer("Игрок успешно разблокирован на сервере!");
			
			SendUnBan(target.DisplayID, moderID);
			
			Log($"Модератор {moderName} ({moderID}) через ВК разблокировал игрока {target.DisplayName} ({target.DisplayID})");
        }		
		
		private void API_Kick(ulong moderID, string moderName, string user, string reason, Action<string> cbSendVkAnswer)
        {
            if (!permission.UserHasPermission(moderID.ToString(), TotalPermission) && !permission.UserHasPermission(moderID.ToString(), KickPermission))
            {
                cbSendVkAnswer("У вас недостаточно привилегий!");
                return;
            }

            if (string.IsNullOrEmpty(user)) { cbSendVkAnswer("Используйте: /kick ID/Nick"); return; }
            			
			List<BasePlayer> probablyPlayers = FindPlayer(user, true);
			
            if (probablyPlayers.Count == 0)
            {
                cbSendVkAnswer("Мы не нашли игрока по вашему запросу!");
                return;
            }

            if (probablyPlayers.Count > 1)
            {
                string result = "Мы нашли несколько игроков по вашему запросу:";
                foreach (var check in probablyPlayers)
                    result += $"\n - {FixName(check.displayName)} [{check.userID}]";
                cbSendVkAnswer(result);
                return;
            }

            BasePlayer target = probablyPlayers[0];
            reason = string.IsNullOrEmpty(reason) ? DefaultReason : reason;                        
            target.Kick(reason);
			
			//MT.Call("API_AddResults", player.userID, target.userID, 2);
            cbSendVkAnswer("Игрок успешно кикнут с сервера!");
						
			Log($"Модератор {moderName} ({moderID}) через ВК кикнул игрока {target.displayName} ({target.userID})");			
        }
		
		#endregion
		
		#region Player Login Action
		
		private void RunCheckLogonPlayer()
		{		
			if (InProgress.Count == 0 && CheckPlayerQueue.Count > 0)
			{
				//Puts("Чтение массива с игроками для проверки");
				var first = CheckPlayerQueue[0];				
				SendData(first);
				CheckPlayerQueue.RemoveAt(0);
			}
		
			timer.Once(5f, RunCheckLogonPlayer);
		}
		
		private void DoPlayerAction(Dictionary<string, object> result, Dictionary<string, string> data)
		{
			//Puts("DoPlayerAction");
			
			if (result.ContainsKey("message"))
			{
				ulong userID = 0, ownerID = 0;
				
				try { userID = (ulong)Convert.ToInt64(data.ContainsKey("steamID") ? data["steamID"] : "0"); } catch { userID = 0; }
				try { ownerID = (ulong)Convert.ToInt64(data.ContainsKey("familyShare") ? data["familyShare"] : "0"); } catch { ownerID = 0; }				
				if (ownerID == userID) ownerID = 0;
				
				// добавляем задержку на следующее повторное чтение данных с сайта для данного игрока
				if (userID > 0)
				{					
					if (!PlayerWaitCheckRequest.ContainsKey(userID))
						PlayerWaitCheckRequest.Add(userID, ToEpochTime(DateTime.Now));
					else
						PlayerWaitCheckRequest[userID] = ToEpochTime(DateTime.Now);
				}
				
				if (ownerID > 0)
				{
					if (!PlayerWaitCheckRequest.ContainsKey(ownerID))
						PlayerWaitCheckRequest.Add(ownerID, ToEpochTime(DateTime.Now));
					else
						PlayerWaitCheckRequest[ownerID] = ToEpochTime(DateTime.Now);
				}
				// ..
				
				// Бан
				if ((string)result["message"] == "banned")
				{						
					//Puts("баним");
					
					var reason = result.ContainsKey("data") ? (string)result["data"] : "";
					
					if (userID > 0) 
						if (!playerBans.ContainsKey(userID))
							BanPlayerNoSync(userID, reason);
						else
							Kick(userID, reason);
					
					if (ownerID > 0)
						if (!playerBans.ContainsKey(ownerID))
							BanPlayerNoSync(ownerID, reason);
						else
							Kick(ownerID, reason);
					
					return;
				}
				
				// Разбан
				if ((string)result["message"] == "notFound")
				{
					//Puts("разбаним");
					
					if (userID > 0 && playerBans.ContainsKey(userID))
						UnBanPlayerNoSync(userID);
					
					if (ownerID > 0 && playerBans.ContainsKey(ownerID))
						UnBanPlayerNoSync(ownerID);
				}
			}
		}
		
		#endregion
		
		#region Site
		
		private static Dictionary<string, Dictionary<string, string>> InProgress = new Dictionary<string, Dictionary<string, string>>();				
		private static List<Dictionary<string, string>> CheckPlayerQueue = new List<Dictionary<string, string>>();
		private static Dictionary<ulong, long> PlayerWaitCheckRequest = new Dictionary<ulong, long>();
		
		private void CheckBan(ulong userID, ulong ownerID, string ip)
		{						
			//Puts("CheckBan");
			
			var data = new Dictionary<string, string>();
			data["storeID"] = configData.ShopNo.ToString();
			data["serverID"] = configData.ServerNo.ToString();
			data["serverKey"] = configData.ServerKey;		    
			data["modules"] = "banlist";
		    data["action"] = "checkUserBan";			
		    data["steamID"] = userID.ToString();
		    data["ip"] = ip;
		    data["familyShare"] = ownerID.ToString();
		    			
			CheckPlayerQueue.Add(data);						
		}
		
		private void SendBan(ulong bannedID, string reason, long banTime, ulong moderID, string ip, ulong ownerID)
		{
			if (banTime > 0) return; // не синхронизируем временные баны
			
			var data = new Dictionary<string, string>();
			data["storeID"] = configData.ShopNo.ToString();
			data["serverID"] = configData.ServerNo.ToString();
			data["serverKey"] = configData.ServerKey;
		    data["modules"] = "banlist";
		    data["action"] = "addBan";
		    data["steamID"] = bannedID.ToString();
		    data["reason"] = !string.IsNullOrEmpty(reason) ? reason : "";
		    data["banTime"] = banTime.ToString();
		    data["bannedBy"] = moderID.ToString();
		    data["ip"] = !string.IsNullOrEmpty(ip) ? ip : "0.0.0.0";
		    data["familyShare"] = ownerID.ToString();
		    data["singleBan"] = "0";
		    
			timer.Once(Rnd.Next(0, 9), ()=>SendData(data));
		}
		
		private void SendUnBan(ulong bannedID, ulong moderID)
		{
			var data = new Dictionary<string, string>();
			data["storeID"] = configData.ShopNo.ToString();
			data["serverID"] = configData.ServerNo.ToString();
			data["serverKey"] = configData.ServerKey;
		    data["modules"] = "banlist";
		    data["action"] = "removeBan";
		    data["steamID"] = bannedID.ToString();
			data["bannedBy"] = moderID.ToString();
			
			timer.Once(Rnd.Next(0, 9), ()=>SendData(data));
		}
		
		private void SendBanPack(List<SyncBanData> syncList)
		{
			var syncCopy = syncList.ToArray<SyncBanData>();   
			if (syncCopy.Length == 0) return;    
			
			var data = new Dictionary<string, string>();
			data["storeID"] = configData.ShopNo.ToString();
			data["serverID"] = configData.ServerNo.ToString();
			data["serverKey"] = configData.ServerKey;
			data["modules"] = "banlist";
		    data["action"] = "importBans";
			data["data"] = JsonConvert.SerializeObject((object) syncCopy);
			
			SendData(data);
		}
		
		private void SendData(Dictionary<string, string> data, string key = null)
		{
			if (!string.IsNullOrEmpty(key) && !InProgress.ContainsKey(key)) return;
			
			if (string.IsNullOrEmpty(key))
			{
				key = (data.ContainsKey("steamID") ? data["steamID"] + "_" : "") + Rnd.Next(1,99999999).ToString();				
				InProgress.Add(key, data);
			}
			
			var key_ = key;
			
			requests.Request(configData.ShopURL, data, (Action<string, string>)((resp, error) => 
			{ 
				try 
				{
					var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include, MissingMemberHandling = MissingMemberHandling.Error };
					var result = JsonConvert.DeserializeObject(resp, typeof(Dictionary<string, object>), settings);												
					
					if (result != null && result.GetType() == typeof(Dictionary<string, object>))
					{
						var result_ = result as Dictionary<string, object>; 
						if (result_.ContainsKey("status") && (string)result_["status"] == "success")
						{
							InProgress.Remove(key_);
							
							if (result_.ContainsKey("message") && data.ContainsKey("action") && data["action"] == "checkUserBan")
								DoPlayerAction(result_, data);
						}						
					}
				} 
				catch (Exception e)
				{
					PrintWarning($"Ошибка приёма данных с сайта: {error}");
				}
			}));
			
			timer.Once(1f, ()=> CheckResponse(key_));
		}
		
		private void CheckResponse(string key)
		{			
			if (!InProgress.ContainsKey(key)) return;			
			timer.Once(1f, ()=> 
			{
				if (InProgress.ContainsKey(key))
					SendData(InProgress[key], key);
			});
		}
		
		#endregion
		
		#region WWW
		
		public class WWWRequests : MonoBehaviour
		{
			private readonly List<UnityWebRequest> activeRequests = new List<UnityWebRequest>();

			public void Request(string url, Dictionary<string, string> data = null, Action<string, string> onRequestComplete = null)
			{
				this.StartCoroutine(this.WaitForRequest(url, data, onRequestComplete));
			}

			private IEnumerator WaitForRequest(string url, Dictionary<string, string> data = null, Action<string, string> onRequestComplete = null)
			{			
				var www = data == null ? UnityWebRequest.Get(url) : UnityWebRequest.Post(url, data);
				activeRequests.Add(www);
				yield return www.Send();

				onRequestComplete?.Invoke(www.downloadHandler.text, www.error);
				activeRequests.Remove(www);
			}    

			private void OnDestroy()
			{
				foreach (UnityWebRequest activeRequest in this.activeRequests)
				{
					try { activeRequest.Dispose(); }
					catch {}
				}
			}
		}
		
		#endregion
		
		#region Config        				
		
        private static ConfigData configData;
		
        private class ConfigData
        {
            [JsonProperty(PropertyName = "api сайта")]
			public string ShopURL;
			[JsonProperty(PropertyName = "номер магазина")]
			public int ShopNo;
			[JsonProperty(PropertyName = "номер сервера")]
			public int ServerNo;
			[JsonProperty(PropertyName = "ключ сервера")]
			public string ServerKey;
			[JsonProperty(PropertyName = "через сколько минут повторно проверять игрока через сайт")]
			public int ReCheckMinutes;
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
				ShopURL = "https://store-api.moscow.ovh/index.php",
                ShopNo = 0,
				ServerNo = 0,
				ServerKey = null,
				ReCheckMinutes = 15
            };
            SaveConfig(configData);
			timer.Once(0.1f, ()=> SaveConfig(configData));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
        
    }
}