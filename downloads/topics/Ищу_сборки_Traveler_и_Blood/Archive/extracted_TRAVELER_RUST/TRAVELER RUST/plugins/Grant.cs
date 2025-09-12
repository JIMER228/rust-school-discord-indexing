using System.Text.RegularExpressions;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using System;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
	// - расширить список функций как на Moscow.Ovh		 
	
    [Info("Grant", "https://discord.gg/9vyTXsJyKR", "1.0.0")]    	 				
    public class Grant : RustPlugin
    {        
		
		#region Variables	
		
		private static GrantData grantData;
		
        private class GrantData
		{
			public Dictionary<string, Dictionary<string, long>> Group = new Dictionary<string, Dictionary<string, long>>();
			public Dictionary<string, Dictionary<string, long>> Permission = new Dictionary<string, Dictionary<string, long>>();						
		}
		
		#endregion                
        
        #region Hooks	
		
        private void Init()
        {            			            
			LoadVariables();
            LoadData();
        }
		
		private void OnServerInitialized() => timer.Once(3.6f, ()=> RunCheckPermissions());	        				                    

        #endregion

		#region Main
		
		private void RunCheckPermissions()
		{
			UpdatePlayerPrivs();
			if (configData.RestorePrivs) RestorePlayerPrivs();
			SaveData();					
			timer.Once(60f, ()=> RunCheckPermissions());			
		}   				
		
		private bool IsPermExists(string perm) => permission.PermissionExists(perm, null);
		
		private bool IsGroupExists(string group) => permission.GroupExists(group);
		
		private bool AddPermission(BasePlayer player, string userID, string perm, DateTime expireDate)
		{					
			if (grantData.Permission.ContainsKey(userID))
			{
				var perms = grantData.Permission[userID];
				if (perms.ContainsKey(perm))
				{
					if (!permission.UserHasPermission(userID, perm))
						permission.GrantUserPermission(userID, perm, null);
					
					perms[perm] += (long)Math.Round((expireDate - DateTime.Now).TotalSeconds);			
					Log(player, $"Игроку \"{GetPlayerName(userID)} ({userID})\" продлена привилегия \"{perm}\" в сумме до \"{GetTimeString(perms[perm] - ToEpochTime(DateTime.Now))}\"");
				}
				else
				{
					if (!IsPermExists(perm)) return false;
					
					if (!configData.ForceSetTempPriv)
					{
						if (permission.UserHasPermission(userID, perm))
						{
							Reply(player, $"Игрок \"{GetPlayerName(userID)} ({userID})\" уже имеет привилегию \"{perm}\" в качестве постоянной");
							return true;
						}
					}
					
					permission.GrantUserPermission(userID, perm, null);					
					perms.Add(perm, ToEpochTime(expireDate));
					Log(player, $"Игроку \"{GetPlayerName(userID)} ({userID})\" выдана привилегия \"{perm}\" на \"{GetTimeString(perms[perm] - ToEpochTime(DateTime.Now))}\"");
				}
			}
			else
			{
				if (!IsPermExists(perm)) return false;
							
				if (!configData.ForceSetTempPriv)
				{
					if (permission.UserHasPermission(userID, perm))
					{
						Reply(player, $"Игрок \"{GetPlayerName(userID)} ({userID})\" уже имеет привилегию \"{perm}\" в качестве постоянной");
						return true;
					}
				}
							
				grantData.Permission.Add(userID, new Dictionary<string, long>());
				var perms = grantData.Permission[userID];
				permission.GrantUserPermission(userID, perm, null);
				perms.Add(perm, ToEpochTime(expireDate));
				Log(player, $"Игроку \"{GetPlayerName(userID)} ({userID})\" выдана привилегия \"{perm}\" на \"{GetTimeString(perms[perm] - ToEpochTime(DateTime.Now))}\"");
			}			

			SaveData();
			return true;
		}

		private bool AddGroup(BasePlayer player, string userID, string group, DateTime expireDate)
		{
			if (grantData.Group.ContainsKey(userID))
			{
				var groups = grantData.Group[userID];
				if (groups.ContainsKey(group))
				{
					if (!permission.UserHasGroup(userID, group))
						permission.AddUserGroup(userID, group);
					
					groups[group] += (long)Math.Round((expireDate - DateTime.Now).TotalSeconds);
					Log(player, $"Игроку \"{GetPlayerName(userID)} ({userID})\" продлено пребывание в группе \"{group}\" в сумме до \"{GetTimeString(groups[group] - ToEpochTime(DateTime.Now))}\"");
				}
				else
				{
					if (!IsGroupExists(group)) return false;
					
					if (!configData.ForceSetTempPriv)
					{
						if (permission.UserHasGroup(userID, group))
						{
							Reply(player, $"Игрок \"{GetPlayerName(userID)} ({userID})\" уже входит в группу \"{group}\" на постоянной основе");
							return true;
						}
					}
					
					permission.AddUserGroup(userID, group);
					groups.Add(group, ToEpochTime(expireDate));
					Log(player, $"Игрок \"{GetPlayerName(userID)} ({userID})\" добавлен в группу \"{group}\" на \"{GetTimeString(groups[group] - ToEpochTime(DateTime.Now))}\"");
				}
			}
			else
			{
				if (!IsGroupExists(group)) return false;
						
				if (!configData.ForceSetTempPriv)
				{
					if (permission.UserHasGroup(userID, group))
					{
						Reply(player, $"Игрок \"{GetPlayerName(userID)} ({userID})\" уже входит в группу \"{group}\" на постоянной основе");
						return true;
					}
				}
						
				grantData.Group.Add(userID, new Dictionary<string, long>());
				var groups = grantData.Group[userID];
				permission.AddUserGroup(userID, group);
				groups.Add(group, ToEpochTime(expireDate));
				Log(player, $"Игрок \"{GetPlayerName(userID)} ({userID})\" добавлен в группу \"{group}\" на \"{GetTimeString(groups[group] - ToEpochTime(DateTime.Now))}\"");
			}			

			SaveData();
			return true;			
		}

		private void RemovePermission(BasePlayer player, string userID, string perm, bool saveNow = true)
		{						
			if (grantData.Permission.ContainsKey(userID))
			{
				var perms = grantData.Permission[userID];				
				if (perms.ContainsKey(perm))
				{
					perms.Remove(perm);
					if (saveNow) SaveData();
				
					if (permission.UserHasPermission(userID, perm))
					{
						Log(player, $"У игрока \"{GetPlayerName(userID)} ({userID})\" была снята привилегия \"{perm}\"");				
						permission.RevokeUserPermission(userID, perm);
						return;
					}																		
				}
			}							
			
			if (configData.ForceRevokePriv)
			{
				if (permission.UserHasPermission(userID, perm))
				{
					Log(player, $"У игрока \"{GetPlayerName(userID)} ({userID})\" была снята привилегия \"{perm}\"");
					permission.RevokeUserPermission(userID, perm);
					return;
				}												
			}
			
			Reply(player, $"У игрока \"{GetPlayerName(userID)} ({userID})\" нет привилегии \"{perm}\"");						
		}

		private void RemoveGroup(BasePlayer player, string userID, string group, bool saveNow = true)
		{						
			if (grantData.Group.ContainsKey(userID))
			{
				var groups = grantData.Group[userID];				
				if (groups.ContainsKey(group))
				{
					groups.Remove(group);
					if (saveNow) SaveData();					
				
					if (permission.UserHasGroup(userID, group))
					{
						Log(player, $"Игрок \"{GetPlayerName(userID)} ({userID})\" был исключён из группы \"{group}\"");				
						permission.RemoveUserGroup(userID, group);
						return;
					}														
				}
			}							
			
			if (configData.ForceRevokePriv)
			{
				if (permission.UserHasGroup(userID, group))
				{
					Log(player, $"Игрок \"{GetPlayerName(userID)} ({userID})\" был исключён из группы \"{group}\"");				
					permission.RemoveUserGroup(userID, group);
					return;
				}												
			}
			
			Reply(player, $"Игрок \"{GetPlayerName(userID)} ({userID})\" не состоял в группе \"{group}\"");						
		}				

		private void UpdatePlayerPrivs()
		{
			var curDt = ToEpochTime(DateTime.Now);
			
			foreach (var pair in grantData.Permission.ToDictionary(x=> x.Key, x=> x.Value))
				foreach (var perm in pair.Value.ToDictionary(x=> x.Key, x=> x.Value))
					if (perm.Value - curDt <= 0)
						RemovePermission(null, pair.Key, perm.Key, false);

			foreach (var pair in grantData.Group.ToDictionary(x=> x.Key, x=> x.Value))
				foreach (var group in pair.Value.ToDictionary(x=> x.Key, x=> x.Value))
					if (group.Value - curDt <= 0)
						RemoveGroup(null, pair.Key, group.Key, false);				
		}
		
		private void RestorePlayerPrivs()
		{
			foreach (var pair in grantData.Permission)
				foreach (var perm in pair.Value)
					if (!permission.UserHasPermission(pair.Key, perm.Key) && IsPermExists(perm.Key))
					{
						permission.GrantUserPermission(pair.Key, perm.Key, null);
						Log(null, $"Игроку \"{GetPlayerName(pair.Key)} ({pair.Key})\" была восстановлена привилегия \"{perm.Key}\"");	
					}
					
			foreach (var pair in grantData.Group)
				foreach (var group in pair.Value)
					if (!permission.UserHasGroup(pair.Key, group.Key) && IsGroupExists(group.Key))
					{
						permission.AddUserGroup(pair.Key, group.Key);
						Log(null, $"Игроку \"{GetPlayerName(pair.Key)} ({pair.Key})\" была восстановлена группа \"{group.Key}\"");	
					}		
		}
		
		#endregion
		
        #region Commands

		[ConsoleCommand("revoke.permission")]                
        private void CmdRevokePerm(ConsoleSystem.Arg arg)
        {
			var player = arg?.Player();
			if (player != null && !player.IsAdmin)
			{
				PrintToConsole(player, "У вас нет прав использовать эту команду.");
				return;
			}	
			
            if (arg?.Args?.Length != 2)
            {
				Reply(player, "Синтаксис: revoke.permission <name|steamid> <permission>");				
                return;
            }

            var userID = GetPlayerID(arg.Args[0], player);
            if (string.IsNullOrEmpty(userID)) return;            
            
			var permission = arg.Args[1].ToLower();
			
			RemovePermission(player, userID, permission);
        }
        
		[ConsoleCommand("grant.permission")]                
        private void CmdGrantPerm(ConsoleSystem.Arg arg)
        {
			var player = arg?.Player();
			if (player != null && !player.IsAdmin)
			{
				PrintToConsole(player, "У вас нет прав использовать эту команду.");
				return;
			}	
			
            if (arg?.Args?.Length != 3)
            {
                Reply(player, "Синтаксис: grant.permission <name|steamid> <permission> <time Ex: 1d12h30m>");
                return;
            }

            var userID = GetPlayerID(arg.Args[0], player);
            if (string.IsNullOrEmpty(userID)) return;            
            
			var permission = arg.Args[1].ToLower();
			
			DateTime expireDate;

            if (!TryGetDateTime(arg.Args[2], out expireDate))
            {
                Reply(player, "Неверный формат времени: Пример: 1d12h30m | d = дней, h = часов, m = минут");
                return;
            }
									
			expireDate = expireDate.AddSeconds(3);						

            if (!AddPermission(player, userID, permission, expireDate))
				Reply(player, $"Привилегия \"{permission}\" не существует.");
        }
        
		[ConsoleCommand("revoke.group")]                
        private void CmdRemoveGroup(ConsoleSystem.Arg arg)
        {
			var player = arg?.Player();
			if (player != null && !player.IsAdmin)
			{
				PrintToConsole(player, "У вас нет прав использовать эту команду.");
				return;
			}
			
            if (arg?.Args?.Length != 2)
            {
                Reply(player, "Синтаксис: revoke.group <name|steamid> <group>");
                return;
            }

            var userID = GetPlayerID(arg.Args[0], player);
            if (string.IsNullOrEmpty(userID)) return;            
            
			var group = arg.Args[1].ToLower();

            RemoveGroup(player, userID, group);
        }
        
		[ConsoleCommand("grant.group")]                
        private void CmdAddGroup(ConsoleSystem.Arg arg)
        {
			var player = arg?.Player();
			if (player != null && !player.IsAdmin)
			{
				PrintToConsole(player, "У вас нет прав использовать эту команду.");
				return;
			}
			
            if (arg?.Args?.Length != 3)
            {
                Reply(player, "Синтаксис: grant.group <name|steamid> <group> <time Ex: 1d12h30m>");
                return;
            }

            var userID = GetPlayerID(arg.Args[0], player);
            if (string.IsNullOrEmpty(userID)) return;            
            
			var group = arg.Args[1].ToLower();
			
			DateTime expireDate;

            if (!TryGetDateTime(arg.Args[2], out expireDate))
            {
                Reply(player, "Неверный формат времени: Пример: 1d12h30m | d = дней, h = часов, m = минут");
                return;
            }						

			expireDate = expireDate.AddSeconds(3);
			
			if (!AddGroup(player, userID, group, expireDate))										
				Reply(player, $"Группа \"{group}\" не существует.");
        }
        
		[ConsoleCommand("grant.info")]                
        private void CmdPlayerInfo(ConsoleSystem.Arg arg)
        {
			var player = arg?.Player();
			if (player != null && !player.IsAdmin)
			{
				PrintToConsole(player, "У вас нет прав использовать эту команду.");
				return;
			}
			
            if (arg?.Args?.Length != 1)
            {
                Reply(player, "Синтаксис: grant.info <name|steamid>");
                return;
            }

            var userID = GetPlayerID(arg.Args[0], player);
            if (string.IsNullOrEmpty(userID)) return;   			
            
			var curDt = ToEpochTime(DateTime.Now);			
			
			var permsStr = "нет";			
			if (grantData.Permission.ContainsKey(userID))
			{
				var perms = grantData.Permission[userID];											
				permsStr = "";
				foreach(var perm in perms.Where(x=> x.Value - curDt > 0).OrderBy(x=> x.Key))
					permsStr += $"\"{perm.Key}\" осталось \"{GetTimeString(perm.Value - curDt)}\", ";
				
				permsStr = permsStr.Trim(',',' ');
				if (string.IsNullOrEmpty(permsStr))
					permsStr = "нет";
			}
			
			var groupsStr = "нет";			
			if (grantData.Group.ContainsKey(userID))
			{
				var groups = grantData.Group[userID];											
				groupsStr = "";
				foreach(var group in groups.Where(x=> x.Value - curDt > 0).OrderBy(x=> x.Key))
					groupsStr += $"\"{group.Key}\" осталось \"{GetTimeString(group.Value - curDt)}\", ";
				
				groupsStr = groupsStr.Trim(',',' ');
				if (string.IsNullOrEmpty(groupsStr))
					groupsStr = "нет";
			}
			
			Reply(player, $"Данные игрока \"{GetPlayerName(userID)} ({userID})\":\nГруппы: {groupsStr}\nПривилегии: {permsStr}");			
        }

        #endregion        

        #region Message				       

		private void Reply(BasePlayer player, string msg)
		{
			if (player != null) PrintToConsole(player, msg); else Puts(msg);
		}				

		#endregion		
		
		#region TimeString
		
		private static long ToEpochTime(DateTime dateTime)
        {
            var date = dateTime.ToLocalTime();
            var ticks = date.Ticks - new DateTime(1970, 1, 1, 0, 0, 0, 0).Ticks;
            var ts = ticks / TimeSpan.TicksPerSecond;
            return ts;
        }
		
		private static bool TryGetDateTime(string source, out DateTime date)
        {
            int minutes = 0;
            int hours = 0;
            int days = 0;

            Match m = new Regex(@"(\d+?)m", RegexOptions.IgnoreCase).Match(source);
            Match h = new Regex(@"(\d+?)h", RegexOptions.IgnoreCase).Match(source);
            Match d = new Regex(@"(\d+?)d", RegexOptions.IgnoreCase).Match(source);

            if (m.Success)
                minutes = Convert.ToInt32(m.Groups[1].ToString());

            if (h.Success)
                hours = Convert.ToInt32(h.Groups[1].ToString());

            if (d.Success)
                days = Convert.ToInt32(d.Groups[1].ToString());

            source = source.Replace(minutes.ToString() + "m", string.Empty);
            source = source.Replace(hours.ToString() + "h", string.Empty);
            source = source.Replace(days.ToString() + "d", string.Empty);

            if (!string.IsNullOrEmpty(source) || (!m.Success && !h.Success && !d.Success))
            {
                date = default(DateTime);
                return false;
            }

            date = DateTime.Now + new TimeSpan(days, hours, minutes, 0);
            return true;
        }
		
		private static bool IsElementExists(int val, int[] mas) {
			foreach (int elem in mas)
				if (elem==val)
					return true;
			return false;
		}
		
		private static string GetStringDays(int days)
		{
			if (IsElementExists(days, (new int[] {1,21})))
				return days.ToString()+" день";
			else if (IsElementExists(days, (new int[] {2,3,4,22,23,24})))
				return days.ToString()+" дня";
			else return days.ToString()+" дней";						
		}
		
		private static string GetStringHours(int hours)
		{
			if (IsElementExists(hours, (new int[] {1,21})))
				return hours.ToString()+" час";
			else if (IsElementExists(hours, (new int[] {2,3,4,22,23,24})))
				return hours.ToString()+" часа";
			else return hours.ToString()+" часов";						
		}
		
		private static string GetStringMinutes(int minutes)
		{
			if (IsElementExists(minutes, (new int[] {1,21,31,41,51})))
				return minutes.ToString()+" минута";
			else if (IsElementExists(minutes, (new int[] {2,3,4,22,23,24,32,33,34,42,43,44,52,53,54})))
				return minutes.ToString()+" минуты";
			else return minutes.ToString()+" минут";						
		}
		
		private static string GetStringSeconds(int seconds)
		{
			if (IsElementExists(seconds, (new int[] {1,21,31,41,51})))
				return seconds.ToString()+" секунда";
			else if (IsElementExists(seconds, (new int[] {2,3,4,22,23,24,32,33,34,42,43,44,52,53,54})))
				return seconds.ToString()+" секунды";
			else return seconds.ToString()+" секунд";						
		}
		
		private static string GetTimeString(long time)
        {            			
			TimeSpan elapsedTime = TimeSpan.FromSeconds(time);
            int hours = elapsedTime.Hours;
            int minutes = elapsedTime.Minutes;
            int seconds = elapsedTime.Seconds;
            int days = Mathf.FloorToInt((float)elapsedTime.Days);
            string s = "";
			int count = 0;

            if (days > 0) 
			{	
				s += GetStringDays(days) + " ";
				count++;
			}	
            if (hours > 0 || count == 1) 
			{					
				if (hours > 0)
				{
					s += GetStringHours(hours) + " ";
					if (++count==2) return s.Trim(' ');
				}
				else				
					return s.Trim(' ');					
			}	
            if (minutes > 0 || count == 1) 
			{
				if (minutes > 0)
				{
					s += GetStringMinutes(minutes) + " ";
					if (++count==2) return s.Trim(' ');
				}
				else				
					return s.Trim(' ');					
			}	
            if (seconds > 0 || count == 1)
			{
				if (seconds > 0)
				{
					s += GetStringSeconds(seconds) + " ";
					if (++count==2) return s.Trim(' ');
				}
				else				
					return s.Trim(' ');				
			}	            					
			
            return s.Trim(' ');
        }			

		#endregion
		
		#region PlayerInfo				
		
		private string GetPlayerName(string userID)
		{
			var data = permission.GetUserData(userID);															
			return data.LastSeenNickname;
		}			
		
        private string GetPlayerID(string nameOrID, BasePlayer player)
        {
            if (IsParseableTo<ulong>(nameOrID) && nameOrID.StartsWith("7656119") && nameOrID.Length == 17)            
				return nameOrID;								            

            var foundPlayers = new List<BasePlayer>();

            foreach (BasePlayer current in BasePlayer.activePlayerList)
            {
                if (current.displayName.ToLower() == nameOrID.ToLower())                    
					return current.UserIDString;	

                if (current.displayName.ToLower().Contains(nameOrID.ToLower()))
                    foundPlayers.Add(current);
            }						

            switch (foundPlayers.Count)
            {
                case 0:					
                    Reply(player, $"Не найден игрок с именем \"{nameOrID}\"");
                    break;

                case 1:
                    return foundPlayers[0].UserIDString;

                default:
                    string[] names = (from current in foundPlayers select current.displayName).ToArray();
                    Reply(player, "Найдено несколько игроков с похожим именем: \n" + string.Join(", ", names));
                    break;
            }

            return null;
        }        				
		
        private static bool IsParseableTo<T>(object s)
        {
            try
            {
                var parsed = (T)Convert.ChangeType(s, typeof(T));
                return true;
            }
            catch
            {
                return false;
            }
        }           

		#endregion				
		
		#region Log
		
		private static List<LogNfo> QueueBuffer = new List<LogNfo>();
		
		private class LogNfo
		{
			public string prefix;
			public string date;
			public string text;
			public LogNfo(string prefix, string date, string text)
			{
				this.prefix = prefix;
				this.date = date;
				this.text = text;
			}
		}
		
		private string GetCurDate() => "["+DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")+"] ";		
		
		private bool TryLog(string prefix, string date, string text)
		{
			try
			{		
				LogToFile(prefix, date + text, this, false);
				string prefix_ = prefix;
				string text_ = text;				
			}
			catch
			{												
				return false;
			}
			return true;
		}
		
		private void Log(BasePlayer player, string text)
		{
			Reply(player, text);
			QueueBuffer.Add(new LogNfo("info", GetCurDate(), text));	
			while(QueueBuffer.Count > 0)
			{					
				if (TryLog(QueueBuffer[0].prefix, QueueBuffer[0].date, QueueBuffer[0].text))
					QueueBuffer.RemoveAt(0);
				else
					break;
			}				
		}	
		
		#endregion		
		
		#region Config        				
		
        private static ConfigData configData;
		
        private class ConfigData
        {            
			[JsonProperty(PropertyName = "Разрешать снимать с игрока постоянную привилегию или группу, которой нет в БД плагина, командами плагина")]
			public bool ForceRevokePriv;			
			[JsonProperty(PropertyName = "Разрешать перезаписывать постоянные привилегии и группы игрока временными, при их выдаче командами плагина")]
			public bool ForceSetTempPriv;
			[JsonProperty(PropertyName = "Следить и восстанавливать привилегии и группы игрокам согласно записям БД плагина")]
			public bool RestorePrivs;
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                ForceRevokePriv = false,
				RestorePrivs = false
            };
            SaveConfig(configData);
			timer.Once(0.1f, ()=>SaveConfig(configData));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
		
		#region Data		      		

        private void LoadData() => grantData = Interface.GetMod().DataFileSystem.ReadObject<GrantData>("GrantData");					
		
		private void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("GrantData", grantData);				
		       
        #endregion
		
		#region API
		
		/* 
			получить список привилегий игрока в формате Dictionary<название привилегии, оставшиеся время действия в секундах> 
			метод возвращает null, если у игрока нет привилегий 
		*/
		private Dictionary<string, int> GetPermissions(ulong userID) 
		{			
			var curDt = ToEpochTime(DateTime.Now);
			var userIDString = userID.ToString();
		
			if (grantData.Permission.ContainsKey(userIDString))			
				return grantData.Permission[userIDString].Where(x=> x.Value - curDt > 0).ToDictionary(x=> x.Key, x=> (int)(x.Value - curDt));
							
			return null;
		}
		
		/* 	
			получить список групп игрока в формате Dictionary<название группы, оставшиеся время действия в секундах>
			метод возвращает null, если у игрока нет групп
		*/	
		private Dictionary<string, int> GetGroups(ulong userID) 
		{
			var curDt = ToEpochTime(DateTime.Now);
			var userIDString = userID.ToString();
		
			if (grantData.Group.ContainsKey(userIDString))			
				return grantData.Group[userIDString].Where(x=> x.Value - curDt > 0).ToDictionary(x=> x.Key, x=> (int)(x.Value - curDt));	
				
			return null;			
		}
		
		#endregion				
    }
}