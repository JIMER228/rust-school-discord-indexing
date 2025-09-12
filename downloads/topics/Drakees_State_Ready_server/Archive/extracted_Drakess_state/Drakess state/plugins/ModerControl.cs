using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ModerControl", "Nimant", "1.0.14")]
    class ModerControl : RustPlugin
    {
        
		#region Variables
		
		private enum ActionType
        {
			BAN = 1,
			UNBAN = 2,
			MUTE = 3,
			UNMUTE = 4,
			CHECK = 5,
			UNCHECK = 6			
        }
				
		private static string AdminPerm = "modercontrol.admin";
		private static string ShowStatsPerm = "modercontrol.show";
		
		private static bool NeedSave = false;
		private static List<ModerInfo> ModeratorInfo = new List<ModerInfo>();
		
		private class ModerInfo
		{			
			[JsonProperty("Дата приёма модератора")]
            public string StartDt;
			[JsonProperty("Дата снятия модератора")]
            public string StopDt;
			
			[JsonProperty("Причина снятия модератора")]
            public string ReasonClose;
			
			[JsonProperty("Имена модератора")] 
            public List<string> Names = new List<string>();
			[JsonProperty("SteamID модератора")] 
            public List<ulong> SteamIDs = new List<ulong>();			
			[JsonProperty("ВК модератора")] 
            public List<string> VKs = new List<string>();
			
			[JsonProperty("Время последнего входа на сервер")]
            public string LastJoin;
            
            [JsonProperty("Количество выданных мутов (за всё время)")]
            public int AllMutes = 0;
			[JsonProperty("Количество снятых мутов (за всё время)")]
            public int AllUnMutes = 0;
            [JsonProperty("Количество выданных банов (за всё время)")]
            public int AllBans = 0;
			[JsonProperty("Количество снятых банов (за всё время)")]
            public int AllUnBans = 0;
            [JsonProperty("Количество проверок игроков (за всё время)")]
            public int AllChecks = 0;            
            [JsonProperty("Время игры на сервере (за всё время)")]
            public int AllGameTime = 0;
            [JsonProperty("Время AFK на сервере (за всё время)")]
            public int AllAFKTime = 0;
			
			[JsonProperty("Количество выданных мутов (за вайп)")]
            public int WipeMutes = 0;
			[JsonProperty("Количество снятых мутов (за вайп)")]
            public int WipeUnMutes = 0;
            [JsonProperty("Количество выданных банов (за вайп)")]
            public int WipeBans = 0;
			[JsonProperty("Количество снятых банов (за вайп)")]
            public int WipeUnBans = 0;
            [JsonProperty("Количество проверок игроков (за вайп)")]
            public int WipeChecks = 0;            
            [JsonProperty("Время игры на сервере (за вайп)")]
            public int WipeGameTime = 0;
            [JsonProperty("Время AFK на сервере (за вайп)")]
            public int WipeAFKTime = 0;						
            
            [JsonProperty("Список игроков получивших мут (за всё время)")]
            public Dictionary<string, string> AllMutesList = new Dictionary<string, string>();
			[JsonProperty("Список игроков получивших размут (за всё время)")]
            public Dictionary<string, string> AllUnMutesList = new Dictionary<string, string>();
            [JsonProperty("Список забаненых игроков (за всё время)")]
            public Dictionary<string, string> AllBansList = new Dictionary<string, string>();
			[JsonProperty("Список разбаненых игроков (за всё время)")]
            public Dictionary<string, string> AllUnBansList = new Dictionary<string, string>();
            [JsonProperty("Список игроков вызванных на проверку (за всё время)")]
            public Dictionary<string, string> AllChecksList = new Dictionary<string, string>();
			
			[JsonProperty("Список игроков получивших мут (за вайп)")]
            public Dictionary<string, string> WipeMutesList = new Dictionary<string, string>();
			[JsonProperty("Список игроков получивших размут (за вайп)")]
            public Dictionary<string, string> WipeUnMutesList = new Dictionary<string, string>();
            [JsonProperty("Список забаненых игроков (за вайп)")]
            public Dictionary<string, string> WipeBansList = new Dictionary<string, string>();
			[JsonProperty("Список разбаненых игроков (за вайп)")]
            public Dictionary<string, string> WipeUnBansList = new Dictionary<string, string>();
            [JsonProperty("Список игроков вызванных на проверку (за вайп)")]
            public Dictionary<string, string> WipeChecksList = new Dictionary<string, string>();
						
			[JsonProperty("Проводит проверку игрока")]
			public bool OnCheck;
			[JsonProperty("Хотя бы раз был на сервере")]
			public bool FirstIn;
			
			[JsonIgnore]
			public Vector3 LastPos = default(Vector3);
		}
		
		#endregion
		
		#region Hooks
		
		private void Init() 
		{	
			LoadVariables();
			LoadData();			
			permission.RegisterPermission(AdminPerm, this);
			permission.RegisterPermission(ShowStatsPerm, this);
		}
		
		private void OnServerInitialized()
		{						
			var console = ModeratorInfo.FirstOrDefault(x=> x.SteamIDs.Contains(0));
			
			if (console == null)
			{
				var moderInfo = new ModerInfo();													
				string name = "Консоль";
				moderInfo.Names.Add(name);				
				moderInfo.SteamIDs.Add(0);				
				ModeratorInfo.Add(moderInfo);
				SaveData();
			}
			
			// в будущем удалить этот фрагмент
			foreach(var moder in ModeratorInfo.ToList())			
				moder.AllMutesList = new Dictionary<string, string>(); // убираем, что бы уменьшить размер базы			
			SaveData();
			
			timer.Once(5f, RunCheckModers);
		}
		
		private void OnPlayerConnected(BasePlayer player)
        {
			var moder = ModeratorInfo.FirstOrDefault(x=> x.SteamIDs.Contains(player.userID) && string.IsNullOrEmpty(x.StopDt));
			if (moder == null) return;
			
			if (!moder.Names.Contains(player.displayName))
				moder.Names.Add(player.displayName);
			
			moder.LastJoin = null;
			moder.FirstIn = true;
			SaveData();
		}
		
		private void OnPlayerDisconnected(BasePlayer player, string reason)
		{
			var moder = ModeratorInfo.FirstOrDefault(x=> x.SteamIDs.Contains(player.userID) && string.IsNullOrEmpty(x.StopDt));
			if (moder == null) return;
			
			moder.LastJoin = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
			SaveData();
		}
		
		private void OnNewSave(string filename)
		{
			Interface.GetMod().DataFileSystem.WriteObject("Backup_ModerControlData", ModeratorInfo);
			
			foreach(var moder in ModeratorInfo.ToList())
			{
				moder.WipeMutes = 0;				
				moder.WipeUnMutes = 0;				
				moder.WipeBans = 0;				
				moder.WipeUnBans = 0;				
				moder.WipeChecks = 0;            				
				moder.WipeGameTime = 0;				
				moder.WipeAFKTime = 0;						
				moder.WipeMutesList = new Dictionary<string, string>();				
				moder.WipeUnMutesList = new Dictionary<string, string>();				
				moder.WipeBansList = new Dictionary<string, string>();				
				moder.WipeUnBansList = new Dictionary<string, string>();				
				moder.WipeChecksList = new Dictionary<string, string>();
				
				moder.AllMutesList = new Dictionary<string, string>(); // убираем, что бы уменьшить размер базы
			}
			
			SaveData();
		}
		
		private void OnServerSave() 
		{
			if (NeedSave)
			{
				SaveData();		
				NeedSave = false;
			}
		}
		
		private void Unload() => SaveData();
		
		private void OnLogProduced(string type, string text)
		{	
			if (!configData.UseIntDetectMutes) return;
			
			//if (type == "log")
			{
				if (text.Contains("заблокировал чат"))
				{
					try
					{
						var pos = text.IndexOf("/");
						var moderID = (ulong)Convert.ToInt64(text.Substring(text.IndexOf("765611"), pos-text.IndexOf("765611")));
						var playerID = (ulong)Convert.ToInt64(text.Substring(text.IndexOf("765611", pos+1), text.IndexOf("/", pos+1)-text.IndexOf("765611", pos+1)));					
						API_SendResult(moderID, playerID, (int)ActionType.MUTE);										
					}
					catch {}
					return;
				}
				
				if (text.Contains("разблокировал чат"))
				{
					try
					{
						var pos = text.IndexOf("/");
						var moderID = (ulong)Convert.ToInt64(text.Substring(text.IndexOf("765611"), pos-text.IndexOf("765611")));
						var playerID = (ulong)Convert.ToInt64(text.Substring(text.IndexOf("765611", pos+1), text.IndexOf("/", pos+1)-text.IndexOf("765611", pos+1)));
						API_SendResult(moderID, playerID, (int)ActionType.UNMUTE);										
					}
					catch {}
					return;
				}
			}
		}
		
		#endregion
		
		#region Main
		
		private void RunCheckModers()
		{
			bool needSave = false;
			BasePlayer player = null;
			foreach(var moder in ModeratorInfo.Where(x=> string.IsNullOrEmpty(x.StopDt)).ToList())
			{
				player = null;
				foreach(var steamid in moder.SteamIDs)
				{
					player = BasePlayer.FindByID(steamid);
					if (player == null) continue;
				}
				
				if (moder.FirstIn && player == null && string.IsNullOrEmpty(moder.LastJoin) && !moder.SteamIDs.Contains(0))
				{
					moder.LastJoin = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
					needSave = true;
				}				
					
				if (player == null) continue;
				
				moder.WipeGameTime++;
				moder.AllGameTime++;
				
				if (!moder.OnCheck && IsAFK(player, moder.LastPos))
				{
					moder.WipeAFKTime++;
					moder.AllAFKTime++;
				}
					
				moder.LastPos = GetNewPos(player);				
				needSave = true;
			}			
			
			if (needSave)
				SaveData();
			
			timer.Once(60f, RunCheckModers);
		}
		
		private Vector3 GetNewPos(BasePlayer player) => new Vector3((float)Math.Truncate(player.transform.position.x), (float)Math.Truncate(player.transform.position.y), (float)Math.Truncate(player.transform.position.z));		
		
		private bool IsAFK(BasePlayer player, Vector3 lastPos)
		{					
			if (GetNewPos(player) == lastPos && player.inventory.crafting.queue.Count == 0)
				return true;
			
			return false;
		}
		
		private string GetPlayerName(ulong userID)
		{
			var data = permission.GetUserData(userID.ToString());															
			return data.LastSeenNickname.Replace("'","").Replace("<", "˂").Replace(">", "˃");
		}
		
		private void Output(BasePlayer player, string output, string text)
		{
			if (output == "chat")
				SendReply(player, text);
			else
				if (output == "console")
					PrintToConsole(player, text);
				else
					Puts(text);
			
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
		
		private static bool IsVkID(string vk)
		{
			if (string.IsNullOrEmpty(vk))
				return false;
			
			foreach (var ch in vk)			
				if ("0123456789".IndexOf(ch) < 0)
					return false;
			
			return true;
		}
		
		private string GetNormDuration(long time)
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
				s += GetStringDays(days) + ", ";
				if (++count==2) return s.Trim(' ',',');
			}	
            if (hours > 0) 
			{					
				s += GetStringHours(hours) + ", ";
				if (++count==2) return s.Trim(' ',',');
			}	
            if (minutes > 0) 
			{
				s += GetStringMinutes(minutes) + ", ";
				if (++count==2) return s.Trim(' ',',');
			}	
            if (seconds > 0)
			{
				s += GetStringSeconds(seconds) + ", ";
				if (++count==2) return s.Trim(' ',',');
			}	            					
			
            return s.Trim(' ',',');
        }					
		
		private void ShowModerStat(BasePlayer player, ModerInfo moder, string output)
		{
			string result = " <color=#aae9f2>*</color> СТАТИСТИКА РАБОТЫ МОДЕРАТОРА <color=#aae9f2>*</color> \n";
			
			/*var names = "";
			foreach(var name in moder.Names) names += "<color=#aae9f2>"+name+"</color>"+", ";				
			names = names.TrimEnd(',',' ');*/
			var names = "<color=#aae9f2>"+GetLastName(moder.SteamIDs[moder.SteamIDs.Count()-1], moder.Names)+"</color>";
			
			//bool isManyNames = moder.Names.Count() > 1;
			bool isManyNames = false;
			
			var vkIDs = "";
			foreach(var vk in moder.VKs) vkIDs += "<color=#aae9f2>"+vk+"</color>"+", ";
			vkIDs = vkIDs.TrimEnd(',',' ');
			vkIDs = !string.IsNullOrEmpty(vkIDs) ? vkIDs : "не указан";
			
			var steams = "";
			foreach(var steam in moder.SteamIDs) steams += "<color=#aae9f2>"+steam+"</color>"+", ";				
			steams = steams.TrimEnd(',',' ');
			
			bool isConsole = steams == "<color=#aae9f2>0</color>";

			var online = moder.FirstIn ? (string.IsNullOrEmpty(moder.LastJoin) ? "на сервере" : moder.LastJoin) : "еще ни разу не заходил";
			
			result += $@"{isManyNames ? "Имена" : "Имя"} модератора: {names}" + "\n";
			if (!isConsole)
			{
				result += $"SteamID модератора: {steams}\n";
				result += !isConsole ? $"VkID модератора: {vkIDs}\n" : "";
				result += $"Дата назначения модератором: <color=#aae9f2>{moder.StartDt}</color>\n";
				
				if (!string.IsNullOrEmpty(moder.StopDt))
				{
					result += $"Дата снятия с модерирования: <color=#aae9f2>{moder.StopDt}</color>\n";				
					if (!string.IsNullOrEmpty(moder.ReasonClose))
						result += $"Причина снятия с модерирования: <color=#aae9f2>{moder.ReasonClose}</color>\n";
				}
				
				result += $"Последний раз заходил: <color=#aae9f2>{online}</color>\n";
				result += $"Время игры на сервере: <color=#aae9f2>{GetNormDuration(moder.WipeGameTime*60)}</color>\n";
				result += $"Время AFK на сервере: <color=#aae9f2>{GetNormDuration(moder.WipeAFKTime*60)}</color>\n";
			}			
			result += $"Выданных мутов: <color=#aae9f2>{moder.WipeMutes}</color>\n";
			result += $"Снятых мутов: <color=#aae9f2>{moder.WipeUnMutes}</color>\n";
			result += $"Выданных банов: <color=#aae9f2>{moder.WipeBans}</color>\n";
			
			if (isConsole)
				result += $"Снятых банов: <color=#aae9f2>{moder.WipeUnBans}</color>";
			else
			{
				result += $"Снятых банов: <color=#aae9f2>{moder.WipeUnBans}</color>\n";
				result += $"Проведённых проверок: <color=#aae9f2>{moder.WipeChecks}</color>\n";			
				result += $@"Сейчас проводит проверку: <color=#aae9f2>{moder.OnCheck ? "да" : "нет"}</color>";						
			}						

			Output(player, output, result);
		}
		
        private string GetLastName(ulong userID, List<string> Names)
		{			
			return GetPlayerName(userID);
			//return Names.Count > 0 ? Names[Names.Count()-1] : null;
		}				
		
		private bool IsOnlyChatModer(ulong userID)
		{
			var result = false;
			foreach(var group in configData.ModerGroups.Where(x=> permission.UserHasGroup(userID.ToString(), x)).ToList())
			{
				if (group.ToLower().Contains("chat"))
					result = true;
				else
					return false;
			}			
			return result;
		}

		#endregion
		
		#region Commands
		
		[ConsoleCommand("moder.add")]
        private void CmdAddMC(ConsoleSystem.Arg args)
        {
			var player = args.Player();
			
            if (player != null && !player.IsAdmin && !permission.UserHasPermission(player.UserIDString, AdminPerm))
            {
                args.ReplyWith("Недостаточно прав.");
                return;
            }
			
			if (args?.Args == null || args?.Args?.Length <= 1)
			{
				args.ReplyWith("Использование: moder.add <SteamID> <group> [VkID]");
                return;
			}						
			
			ulong steamid = 0;			
			if (!UInt64.TryParse(args.Args[0], out steamid))
			{
				args.ReplyWith("Ошибка ввода SteamID. Использование: moder.add <SteamID> <group> [VkID]");
				return;
			}
			
			if (ModeratorInfo.Exists(x=> x.SteamIDs.Contains(steamid) && string.IsNullOrEmpty(x.StopDt)))
			{
				args.ReplyWith("Указанный игрок уже является модератором.");
				return;
			}
			
			var group = args.Args[1];
			
			if (!permission.GroupExists(group))
			{
				args.ReplyWith("Указанная модераторская группа не существует.");
				return;
			}
			
			if (configData.ModerGroups.Where(x=> x == group).Count() == 0)
			{
				args.ReplyWith("Указанная модераторская группа не находится в списке разрешенных.");
				return;
			}
			
			var vkID = args.Args.Length > 2 ? args.Args[2] : "";
			
			if (!string.IsNullOrEmpty(vkID) && !IsVkID(vkID))
			{
				args.ReplyWith("Указан некорректный Vk ID.");
				return;
			}
			
			var moderInfo = new ModerInfo();
			
			moderInfo.StartDt = DateTime.Now.ToString("dd.MM.yyyy HH:mm");			                        
						
			string name = GetPlayerName(steamid);						
						
			if (!string.IsNullOrEmpty(name))
				moderInfo.Names.Add(name);
			
            moderInfo.SteamIDs.Add(steamid);
			
			if (!string.IsNullOrEmpty(vkID))
				moderInfo.VKs.Add(vkID);
                        
            ModeratorInfo.Add(moderInfo);
			SaveData();
			
			permission.AddUserGroup(steamid.ToString(), group);
			
			args.ReplyWith("Игрок успешно добавлен в модераторы.");
			
			var moder = BasePlayer.FindByID(steamid);
			
			if (moder != null)			
				SendReply(moder, "Вы были назначены модератором сервера.");
		}	
		
		[ConsoleCommand("moder.edit")]
        private void CmdEditMC(ConsoleSystem.Arg args)
        {
			var player = args.Player();
			
            if (player != null && !player.IsAdmin && !permission.UserHasPermission(player.UserIDString, AdminPerm))
            {
                args.ReplyWith("Недостаточно прав.");
                return;
            }
			
			if (args?.Args == null || args?.Args?.Length <= 1)
			{
				args.ReplyWith("Использование: moder.edit <SteamID> <новый SteamID | новая group | новый VkID>");
                return;
			}						
			
			ulong steamid = 0;			
			if (!UInt64.TryParse(args.Args[0], out steamid))
			{
				args.ReplyWith("Ошибка ввода SteamID. Использование: moder.edit <SteamID> <новый SteamID | новая group | новый VkID>");
				return;
			}
			
			if (!ModeratorInfo.Exists(x=> x.SteamIDs.Contains(steamid) && string.IsNullOrEmpty(x.StopDt)))
			{
				args.ReplyWith("Указанный игрок не является модератором.");
				return;
			}
			
			var steamOrGroup = args.Args[1];						
			ulong newSteamid = 0;
			string newGroup = null;
			
			var moderInfo = ModeratorInfo.FirstOrDefault(x=> x.SteamIDs.Contains(steamid) && string.IsNullOrEmpty(x.StopDt));
			
			if (steamOrGroup.IsSteamId())
			{							
				if (!UInt64.TryParse(steamOrGroup, out newSteamid))
				{
					args.ReplyWith("Ошибка ввода нового SteamID. Использование: moder.edit <SteamID> <новый SteamID | новая group | новый VkID>");
					return;
				}								
				
				foreach(var group in configData.ModerGroups.Where(x=> permission.UserHasGroup(steamid.ToString(), x)))				
					permission.AddUserGroup(newSteamid.ToString(), group);				
				
				if (!moderInfo.SteamIDs.Contains(newSteamid))
					moderInfo.SteamIDs.Add(newSteamid);
				
				string name = GetPlayerName(newSteamid);						
						
				if (!string.IsNullOrEmpty(name) && !moderInfo.Names.Contains(name))
					moderInfo.Names.Add(name);
				
				SaveData();								
				
				var moder = BasePlayer.FindByID(newSteamid);
			
				if (moder != null)			
					SendReply(moder, "Вы были назначены модератором сервера.");
			}
			else
				if (IsVkID(steamOrGroup))
				{
					if (!moderInfo.VKs.Contains(steamOrGroup))
						moderInfo.VKs.Add(steamOrGroup);
					
					SaveData();	
				}
				else
				{
					newGroup = steamOrGroup;
					
					if (!permission.GroupExists(newGroup))
					{
						args.ReplyWith("Указанная модераторская группа не существует.");
						return;
					}
					
					if (!configData.ModerGroups.Contains(newGroup))
					{
						args.ReplyWith("Указанная модераторская группа не находится в списке разрешенных.");
						return;
					}
					
					foreach(var userID in moderInfo.SteamIDs)
						foreach(var group in configData.ModerGroups.Where(x=> permission.UserHasGroup(userID.ToString(), x)).ToList())
							permission.RemoveUserGroup(userID.ToString(), group);
							
					foreach(var userID in moderInfo.SteamIDs)
						permission.AddUserGroup(userID.ToString(), newGroup);
				}
			
			args.ReplyWith("Изменения по модератору успешно применены.");
		}	
		
		[ConsoleCommand("moder.remove")]
        private void CmdRemoveMC(ConsoleSystem.Arg args)
        {
			var player = args.Player();
			
            if (player != null && !player.IsAdmin && !permission.UserHasPermission(player.UserIDString, AdminPerm))
            {
                args.ReplyWith("Недостаточно прав.");
                return;
            }
			
			if (args?.Args == null || args?.Args?.Length < 1)
			{
				args.ReplyWith("Использование: moder.remove <SteamID> <причина снятия>");
                return;
			}						
			
			ulong steamid = 0;			
			if (!UInt64.TryParse(args.Args[0], out steamid))
			{
				args.ReplyWith("Ошибка ввода SteamID. Использование: moder.remove <SteamID> <причина снятия>");
				return;
			}
			
			if (!ModeratorInfo.Exists(x=> x.SteamIDs.Contains(steamid) && string.IsNullOrEmpty(x.StopDt)))
			{
				args.ReplyWith("Указанный игрок не является модератором.");
				return;
			}
			
			string reason = "";
			
			if (args.Args.Length > 1)
				for(int ii=1;ii<args.Args.Length;ii++) reason += args.Args[ii] + " ";
			
			var moderInfo = ModeratorInfo.FirstOrDefault(x=> x.SteamIDs.Contains(steamid) && string.IsNullOrEmpty(x.StopDt));
			
			moderInfo.StopDt = DateTime.Now.ToString("dd.MM.yyyy HH:mm");			                        
			moderInfo.ReasonClose = reason.TrimEnd(' ');
			
			SaveData();
			
			foreach(var userID in moderInfo.SteamIDs)
				foreach(var group in configData.ModerGroups.Where(x=> permission.UserHasGroup(userID.ToString(), x)).ToList())				
					permission.RemoveUserGroup(userID.ToString(), group);
					
			args.ReplyWith("Модератор успешно удалён.");
			
			foreach(var userID in moderInfo.SteamIDs)
			{
				var moder = BasePlayer.FindByID(userID);
				
				if (moder != null)			
					SendReply(moder, "C вас были сняты полномочия модератора.");
			}
		}
		
		[ChatCommand("mstat")]
        private void CmdMStat(BasePlayer player, string command, string[] args)
        {
			var moderInfo = ModeratorInfo.FirstOrDefault(x=> x.SteamIDs.Contains(player.userID) && string.IsNullOrEmpty(x.StopDt));
			var canSeeAllStat = player.IsAdmin || permission.UserHasPermission(player.UserIDString, AdminPerm) || permission.UserHasPermission(player.UserIDString, ShowStatsPerm);
			
            if (!canSeeAllStat)
            {
                SendReply(player, "Недостаточно прав.");
                return;
            }
			
			if (args?.Length == 0)
			{
				if (moderInfo != null)				
					ShowModerStat(player, moderInfo, "chat");
				else
					SendReply(player, "Вы не модератор, по вам нет статистики.");
				
				return;
			}
			
			if (!(player.IsAdmin || permission.UserHasPermission(player.UserIDString, AdminPerm)))
			{
				SendReply(player, "У вас нет прав просматривать чужую статистику.");
                return;
			}
			
			ulong steamid = 0;
			string name = args[0];
			if (!((name.Contains("7656") && UInt64.TryParse(name, out steamid)) || name == "0"))
			{
				var moders = ModeratorInfo.ToList();
				var foundModers = new List<ModerInfo>();
				var manyModers = "";
				
				/*foreach(var moder in moders)			
					foreach(var name_ in moder.Names)					
						if (name_.ToLower().Contains(name.ToLower()))
						{
							foundModers.Add(moder);
							manyModers += $" * {name_}\n";							
						}*/
						
				foreach(var moder in moders)	
				{				
					var name_ = GetLastName(moder.SteamIDs[moder.SteamIDs.Count()-1], moder.Names);
					if (!string.IsNullOrEmpty(name_) && name_.ToLower().Contains(name.ToLower()))
					{
						foundModers.Add(moder);
						manyModers += $" * {name_}\n";							
					}		
				}
				
				if (foundModers.Count == 0)
				{
					SendReply(player, "Указанный модератор не найден.");
					return;
				}
				
				if (foundModers.Count > 1)
				{
					SendReply(player, "Уточните поиск, найдено несколько похожих модераторов:");																				
					SendReply(player, manyModers.TrimEnd('\n'));						
					return;
				}
				
				ShowModerStat(player, foundModers[0], "chat");				
				return;
			}
			
			var otherModerInfo = ModeratorInfo.FirstOrDefault(x=> x.SteamIDs.Contains(steamid) && string.IsNullOrEmpty(x.StopDt));
			
			if (otherModerInfo == null)
			{
				SendReply(player, "Указанный модератор не найден.");
				return;
			}
			
			ShowModerStat(player, otherModerInfo, "chat");
		}
		
		[ChatCommand("mlist")]
        private void CmdMList(BasePlayer player, string command, string[] args)
        {						
			//var moderInfo = ModeratorInfo.FirstOrDefault(x=> x.SteamIDs.Contains(player.userID) && string.IsNullOrEmpty(x.StopDt));
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, AdminPerm))
            {
                SendReply(player, "Недостаточно прав.");
                return;
            }

			var moders = ModeratorInfo.Where(x=> !x.SteamIDs.Contains(0) && string.IsNullOrEmpty(x.StopDt) && !IsOnlyChatModer(x.SteamIDs[x.SteamIDs.Count()-1]));
				
			if (moders.Count() > 0)
			{
				string result = "";
				result += "СПИСОК МОДЕРАТОРОВ СЕРВЕРА:\n";	
				foreach(var moder in moders.OrderBy(x=> x.FirstIn).ThenBy(x=> x.LastJoin))
				{					
					/*var names = "";
					foreach(var name in moder.Names) names += "<color=#aae9f2>"+name+"</color>"+ ", ";				
					names = names.TrimEnd(',',' ');*/
					var names = "<color=#aae9f2>"+GetLastName(moder.SteamIDs[moder.SteamIDs.Count()-1], moder.Names)+"</color>";									
					
					var steams = "";
					foreach(var steam in moder.SteamIDs) steams += "<color=#aae9f2>"+steam+"</color>"+ ", ";				
					steams = steams.TrimEnd(',',' ');
					
					if (steams == "<color=#aae9f2>0</color>") continue;
					
					var online = moder.FirstIn ? (string.IsNullOrEmpty(moder.LastJoin) ? "на сервере" : moder.LastJoin) : "еще ни разу не заходил";
										
					result += $" * {names}: {online}\n";	
				}
				SendReply(player, result.TrimEnd('\n'));				
			}
			else
				SendReply(player, "На сервере еще нет ни одного модератора.");	
		}
		
		[ChatCommand("clist")]
        private void CmdCList(BasePlayer player, string command, string[] args)
        {						
			//var moderInfo = ModeratorInfo.FirstOrDefault(x=> x.SteamIDs.Contains(player.userID) && string.IsNullOrEmpty(x.StopDt));
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, AdminPerm))
            {
                SendReply(player, "Недостаточно прав.");
                return;
            }

			var moders = ModeratorInfo.Where(x=> !x.SteamIDs.Contains(0) && string.IsNullOrEmpty(x.StopDt) && IsOnlyChatModer(x.SteamIDs[x.SteamIDs.Count()-1]));
				
			if (moders.Count() > 0)
			{
				string result = "";
				result += "СПИСОК ЧАТ-МОДЕРАТОРОВ СЕРВЕРА:\n";	
				foreach(var moder in moders.OrderBy(x=> x.FirstIn).ThenBy(x=> x.LastJoin))
				{					
					/*var names = "";
					foreach(var name in moder.Names) names += "<color=#aae9f2>"+name+"</color>"+ ", ";				
					names = names.TrimEnd(',',' ');*/
					var names = "<color=#aae9f2>"+GetLastName(moder.SteamIDs[moder.SteamIDs.Count()-1], moder.Names)+"</color>";									
					
					var steams = "";
					foreach(var steam in moder.SteamIDs) steams += "<color=#aae9f2>"+steam+"</color>"+ ", ";				
					steams = steams.TrimEnd(',',' ');
					
					if (steams == "<color=#aae9f2>0</color>") continue;
					
					var online = moder.FirstIn ? (string.IsNullOrEmpty(moder.LastJoin) ? "на сервере" : moder.LastJoin) : "еще ни разу не заходил";
										
					result += $" * {names}: {online}\n";	
				}
				SendReply(player, result.TrimEnd('\n'));				
			}
			else
				SendReply(player, "На сервере еще нет ни одного чат-модератора.");	
		}
		
		[ChatCommand("mt")]
        private void CmdMT(BasePlayer player, string command, string[] args)
		{
			if (args?.Length == 0)
				CmdMStat(player, command, args);
			else
				if (args?.Length == 1 && args[0].ToLower() == "list")
					CmdMList(player, command, args);
				else
					CmdMStat(player, command, args);			
		}
		
		// перенесенные из VkReports
		private bool HasModerPerm(BasePlayer player, bool isChatModers = false)
        {
			if (player == null) 
				return false;
			
			if (!isChatModers)
				return ModeratorInfo.Exists(x=> x.SteamIDs[x.SteamIDs.Count()-1] == player.userID && !x.SteamIDs.Contains(0) && string.IsNullOrEmpty(x.StopDt) && !IsOnlyChatModer(x.SteamIDs[x.SteamIDs.Count()-1]));
			
			if (isChatModers)
				return ModeratorInfo.Exists(x=> x.SteamIDs[x.SteamIDs.Count()-1] == player.userID && !x.SteamIDs.Contains(0) && string.IsNullOrEmpty(x.StopDt) && IsOnlyChatModer(x.SteamIDs[x.SteamIDs.Count()-1]));
			
			return false;
        }
		
		private bool ModersOnline(bool isChatModers = false)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                if (HasModerPerm(player, isChatModers))
                    return true;
				
            return false;
        } 	
		
		[ChatCommand("moders")]
        private void OnCommandModers(BasePlayer sender, string command, string[] args)
        {
			if (!sender.IsAdmin && !HasModerPerm(sender, false))
			{
				sender.ChatMessage("Вам не разрешено просматривать данную информацию");
				return;
			}
			
            if (!ModersOnline(false))
            { 
				SendReply(sender, "В настоящее время нет модераторов в онлайне."); 
				return; 
			}

            var msg = "<color=#aae9f2> Модераторы в сети:</color>\n";
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                if (HasModerPerm(player, false))
                    msg += " * "+player.displayName+"\n";
            
			SendReply(sender, msg);
        }
		
		[ChatCommand("cmoders")]
        private void OnCommandCModers(BasePlayer sender, string command, string[] args)
        {
			if (!sender.IsAdmin && !HasModerPerm(sender, true) && !HasModerPerm(sender, false))
			{
				sender.ChatMessage("Вам не разрешено просматривать данную информацию");
				return;
			}
			
            if (!ModersOnline(true))
            { 
				SendReply(sender, "В настоящее время нет чат-модераторов в онлайне."); 
				return; 
			}

            var msg = "<color=#aae9f2> Чат-Модераторы в сети:</color>\n";
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                if (HasModerPerm(player, true))
                    msg += " * "+player.displayName+"\n";
            
			SendReply(sender, msg);
        }
		// end
		
		#endregion
		
		#region API
		
		private List<ulong> API_GetModerSteamIDs(string moderVK)
		{			
			if (string.IsNullOrEmpty(moderVK)) 
				return null;
			
			var moderInfo = ModeratorInfo.FirstOrDefault(x=> x.VKs.Contains(moderVK) && string.IsNullOrEmpty(x.StopDt));
			
			if (moderInfo == null)
				return null;
			
			return moderInfo.SteamIDs;
		}
		
		private void API_SendResult(ulong moderID, ulong targetID, int actionType)
		{
			if (!Enum.IsDefined(typeof(ActionType), actionType))
			{
				PrintWarning($"API_AddResults: неизвестный тип операции '{actionType}'");
				return;
			}
			
			ModerInfo moderInfo = null;
															
			if (moderID >= 0)
				moderInfo = ModeratorInfo.FirstOrDefault(x=> x.SteamIDs.Contains(moderID) && string.IsNullOrEmpty(x.StopDt));
			
			if (moderInfo == null)
			{
				PrintWarning($"API_AddResults: незарегистрированный модератор '{moderID}' совершил операцию '{actionType}' над игроком '{targetID}'");
				return;
			}
			
			var name = GetPlayerName(targetID);
			name = !string.IsNullOrEmpty(name) ? name : "N|A";
			var infoDt = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
			var infoMsg = $"{name} ({targetID})";
			
			switch(actionType)
			{
				case (int)ActionType.BAN: 
				{
					moderInfo.AllBans++;
					moderInfo.WipeBans++;
										
					moderInfo.AllBansList.Add(infoDt, infoMsg);
					moderInfo.WipeBansList.Add(infoDt, infoMsg);
					
					break;
				}
				case (int)ActionType.UNBAN:
				{
					moderInfo.AllUnBans++;
					moderInfo.WipeUnBans++;
					
					moderInfo.AllUnBansList.Add(infoDt, infoMsg);
					moderInfo.WipeUnBansList.Add(infoDt, infoMsg);			

					break;					
				}
				case (int)ActionType.MUTE:
				{
					moderInfo.AllMutes++;
					moderInfo.WipeMutes++;
					
					// moderInfo.AllMutesList.Add(infoDt, infoMsg); не добавляем, что бы уменьшить размер базы
					moderInfo.WipeMutesList.Add(infoDt, infoMsg);					
					
					break;
				}
				case (int)ActionType.UNMUTE:
				{
					moderInfo.AllUnMutes++;
					moderInfo.WipeUnMutes++;
					
					moderInfo.AllUnMutesList.Add(infoDt, infoMsg);
					moderInfo.WipeUnMutesList.Add(infoDt, infoMsg);					
					
					break;
				}
				case (int)ActionType.CHECK:
				{
					moderInfo.AllChecks++;
					moderInfo.WipeChecks++;
					
					moderInfo.AllChecksList.Add(infoDt, infoMsg);
					moderInfo.WipeChecksList.Add(infoDt, infoMsg);
					
					moderInfo.OnCheck = true;
					
					break;
				}
				case (int)ActionType.UNCHECK:
				{
					moderInfo.OnCheck = false;
					
					break;
				}
			}
			
			NeedSave = true;			
			//SaveData(); сохранение переложено на хук OnServerSave и Unload для уменьшения лагов
		}
		
		#endregion
		
		#region Config        				
		
        private static ConfigData configData;
		
        private class ConfigData
        {            
			[JsonProperty(PropertyName = "Список групп модераторов")]
			public List<string> ModerGroups;			
			[JsonProperty(PropertyName = "Разрешать использовать встроенную систему определения мутов")]
			public bool UseIntDetectMutes;
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                ModerGroups = new List<string>()
				{
					"moderator"
				},
				UseIntDetectMutes = true
            };
            SaveConfig(configData);
			timer.Once(0.1f, ()=>SaveConfig(configData));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
		
		#region Data
		
		private void LoadData() 
		{
			ModeratorInfo = Interface.GetMod().DataFileSystem.ReadObject<List<ModerInfo>>("ModerControlData");
			
			var needSave = false;
			foreach (var item in ModeratorInfo)
			{
				if (item.VKs == null)
				{
					item.VKs = new List<string>();
					needSave = true;
				}
			}
			
			if (needSave) SaveData();
		}
		
		private void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("ModerControlData", ModeratorInfo);		
		
		#endregion
		
    }
}