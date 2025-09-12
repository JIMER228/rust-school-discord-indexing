using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core;
using Network;
using Oxide.Core.Configuration;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("PlayerInfo", "Nimant", "1.0.5")]
    class PlayerInfo : RustPlugin
    {				

		#region Variables
		
		private string apiKey;
		private uint appId;	
		
		private const string RootUrl = "http://api.steampowered.com/";
        private const string XmlUrl = "http://steamcommunity.com/profiles/{0}/?xml=1";
        private const string BansStr = "ISteamUser/GetPlayerBans/v1/?key={0}&steamids={1}";
		private const string SharingStr = "IPlayerService/IsPlayingSharedGame/v0001/?key={0}&steamid={1}&appid_playing={2}";
        private const string PlayerStr = "ISteamUser/GetPlayerSummaries/v0002/?key={0}&steamids={1}";
        private const string GamesStr = "IPlayerService/GetOwnedGames/v0001/?key={0}&steamid={1}";
		
		private class SteamInfo
		{			
			public bool isSharing;
			public bool isPrivate;
			public Bans bans;
			public Summaries summaries;
			public Sharing sharing;
			public Games games;
			public bool IsHaveSetUpProfile;			
			public bool IsReady;
			public bool IsActive;
			public int maxTry;
		}
		
		private static Dictionary<ulong, SteamInfo> SteamResult = new Dictionary<ulong, SteamInfo>();				
		
		private static Dictionary<string, string> LastBeen = new Dictionary<string, string>();				
		private static List<string> ActiveUser = new List<string>();
		
		private static Timer timer_;
		
		#endregion
	
		#region Chat
	
		[ChatCommand("player")]
        private void CmdChatView(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
			
			if (!IsHasPrivs(player))
			{
				SendReply(player, "У вас нет прав на эту команду");
				return;
			}
                        
			if (args == null || args.Length==0)
			{
				SendReply(player, "Использование: /player <часть имени игрока или SteamID>");
				return;
			}
			
			string search = "";
			foreach(var str in args) search += str + " ";
			search = search.Trim().ToLower();
			
			List<string> players = new List<string>();
			bool exact = false;
			
            foreach(var target in BasePlayer.activePlayerList)
			{
				if (target.UserIDString == search)
				{
					players.Clear();
					players.Add(target.UserIDString);
					exact = true;
					break;
				}	
				
				if (target.displayName.ToLower().Contains(search) && !players.Contains(target.UserIDString))					
					players.Add(target.UserIDString);										
			}
			
			Dictionary<string, string> users = new Dictionary<string, string>();
			
			if (!exact)
			{	
				users = permission.GetUsersInGroup("default").ToDictionary(x=>x.Substring(0, x.IndexOf(" ")), x=>x.Substring(x.IndexOf(" ")+1, x.Length-x.IndexOf(" ")-1 ).Trim(')', '('));							
		
				foreach(var user in users)
				{
					if (user.Key == search)
					{
						players.Clear();						
						players.Add(user.Key);
						exact = true;
						break;
					}	
					
					if (user.Value.ToLower().Contains(search) && !players.Contains(user.Key))				
						players.Add(user.Key);					
				}
			}
			
			if (players.Count == 0)
			{
				SendReply(player, "Игрок не найден.");
				return;
			}
			else 
				if (exact)				
					GetAndPrintPlayerInfo(player, players[0]);														
				else
					if (players.Count>1)
					{
						SendReply(player, "Уточните поиск, найдено несколько похожих игроков:");
						int max=10;
						foreach(var target in players)						
						{
							if (max<=0) 
							{
								SendReply(player, "<color=#F08080> ... и другие</color>");
								break;
							}	
							SendReply(player, " <color=#aae9f2>*</color> "+(users.ContainsKey(target)?users[target]+" ("+target+")":BasePlayer.activePlayerList.ToList().Find(x=>x.UserIDString==target)?.displayName+" ("+target+")"));
							max--;
						}	
					}	
					else											
						GetAndPrintPlayerInfo(player, players[0]);						
        }
		
		#endregion
		
		#region Common
		
		private void RunTimer(bool unload = false)
		{
			if (timer_ != null) timer_.Destroy();
			
			DateTime now = DateTime.Now;
			bool update = false;
			
			var players = BasePlayer.activePlayerList.ToList();
			
			for(int ii=ActiveUser.Count-1;ii>=0;ii--)
			{				
				if (unload || !players.Exists(x=> x != null && x.UserIDString == ActiveUser[ii]))
				{					
					if (!LastBeen.ContainsKey(ActiveUser[ii]))
						LastBeen.Add(ActiveUser[ii], now.ToString("dd.MM.yyyy HH:mm"));
					else
						LastBeen[ActiveUser[ii]] = now.ToString("dd.MM.yyyy HH:mm");
					
					ActiveUser.Remove(ActiveUser[ii]);					
					update = true;
				}				
			}
			
			if (update) SaveData();
			
			if (!unload)
				timer_ = timer.Once(60f, ()=>RunTimer());
		}						
		
		private void GetAndPrintPlayerInfo(BasePlayer player, string targetID, int count = 0)
		{	
			if (string.IsNullOrEmpty(targetID) || player == null) return;
			
			if (!SteamResult.ContainsKey(player.userID))				
				SteamResult.Add(player.userID, new SteamInfo());
			
			if (count == 0)
			{
				if (SteamResult[player.userID].IsActive)
				{
					SendReply(player, "Дождитесь окончания предыдущего запроса.");
					return;
				}
				
				SendReply(player, "Данные о игроке запрашиваются из стима, ожидайте ...");
				SteamResult[player.userID].IsActive = true;
				SteamResult[player.userID].IsReady = false;
				StartChecking(targetID, player.userID);
				int newCount = count + 1;
				timer.Once(0.5f, ()=> GetAndPrintPlayerInfo(player, targetID, newCount));
				return;
			}																											
			else
			{
				if (!SteamResult[player.userID].IsReady)
				{
					if (count >= 20)
					{
						if (SteamResult[player.userID].maxTry >= 3)						
							SendReply(player, "Сервис стима временно недоступен, данные по игроку ограничены и представлены без стимовской информации.");
						else	
						{
							SteamResult[player.userID].maxTry++;							
							SteamResult[player.userID].IsActive = false;
							GetAndPrintPlayerInfo(player, targetID, 0);
							return;
						}												
					}
					else
					{
						int newCount = count + 1;
						timer.Once(0.5f, ()=> GetAndPrintPlayerInfo(player, targetID, newCount));
						return;
					}
				}
			}												
			
			var target = BasePlayer.activePlayerList.FirstOrDefault(x=>x.UserIDString == targetID) ?? BasePlayer.sleepingPlayerList.FirstOrDefault(x=>x.UserIDString == targetID);
			
			List<string> result = new List<string>();	
			
			result.Add("Найден подходящий игрок:");
			
			var noData = "<color=#A9A9A9>нет данных</color>";
			
			var nameGame = target != null ? "<color=#aae9f2>"+target.displayName+"</color>" : noData;
			var nameSteam = SteamResult[player.userID].IsReady ? "<color=#aae9f2>"+GetPersonaName(player.userID)+"</color>" : noData;
			
			result.Add(string.Format("Имя в игре: {0}, Имя в стиме: {1}", nameGame, nameSteam));								
			result.Add(string.Format("Steam ID: <color=#aae9f2>{0}</color>", targetID));
			
			var privatProfile = SteamResult[player.userID].IsReady ? (IsPrivate(player.userID) ? "<color=#F08080>да</color>" : "<color=#aae9f2>нет</color>") : noData;
			var familyShare = SteamResult[player.userID].IsReady ? (IsSharing(player.userID) ? "<color=#F08080>да</color>" : "<color=#aae9f2>нет</color>") : noData;

			result.Add(string.Format("Скрытый профиль: {0}, Семейный доступ: {1}", privatProfile, familyShare));			
			
			var playHours = SteamResult[player.userID].IsReady && GetPlaytimeForever(player.userID) >= 0 ? (GetPlaytimeForever(player.userID) <= 50 ? "<color=#F08080>{0}</color>" : "<color=#aae9f2>{0}</color>") : noData;
			playHours = SteamResult[player.userID].IsReady && GetPlaytimeForever(player.userID) >= 0 ? string.Format(playHours, GetPlaytimeForever(player.userID)) : playHours;
						
			bool online = BasePlayer.activePlayerList.ToList().Exists(x=>x.UserIDString == targetID);			
			
			var status = online ? "<color=lime>на сервере</color>" : "<color=#F08080>отключён</color>";
			
			int pingValue = 100000;
			if (online) pingValue = Net.sv.GetAveragePing(target.net?.connection); 
			var ping = pingValue != 100000 && pingValue >= 0 ? string.Format(pingValue >= 100 ? "<color=#F08080>{0}</color>" : "<color=#aae9f2>{0}</color>", pingValue) : noData;			
						
			result.Add(string.Format("Наиграно часов: {0}, Статус: {1}, Ping: {2}", playHours, status, ping));						
			
			var lastBeen = LastBeen.ContainsKey(targetID) ? string.Format("<color=#F08080>{0}</color>", LastBeen[targetID]) : noData;
			
			if (!online)
				result.Add(string.Format("Последний раз заходил: {0}", lastBeen));
			
			var vacBans = SteamResult[player.userID].IsReady ? string.Format(VacBanCount(player.userID) > 0 ? "<color=#F08080>{0}</color>" : "<color=#aae9f2>{0}</color>", VacBanCount(player.userID)) : noData;
			var gameBans = SteamResult[player.userID].IsReady ? string.Format(GameBanCount(player.userID) > 0 ? "<color=#F08080>{0}</color>" : "<color=#aae9f2>{0}</color>", GameBanCount(player.userID)) : noData;
						
			result.Add(string.Format("VAC банов: {0}, Игровых банов: {1}", vacBans, gameBans));						
			
			if (VacBanCount(player.userID) > 0 || GameBanCount(player.userID) > 0)				
				result.Add(string.Format("После последнего бана прошло дней: <color=#F08080>{0}</color>", DaysSinceBan(player.userID)));							
			
			var profileCreate = SteamResult[player.userID].IsReady && GetTimeCreated(player.userID) > DateTime.Now.AddYears(-30) ? string.Format(GetTimeCreated(player.userID) >= DateTime.Now.AddDays(-7) ? "<color=#F08080>{0}</color>" : "<color=#aae9f2>{0}</color>", GetTimeCreated(player.userID).ToString("dd.MM.yyyy HH:mm")) : noData;						
			
			result.Add(string.Format("Профиль создан: {0}", profileCreate));
			
			var addr = noData;
			
			if (target != null)
			{
				addr = target.net?.connection?.ipaddress;
				if (addr != null)
				{
					addr = addr.Substring(0, addr.IndexOf(":"));
					addr = string.Format("<color=#aae9f2>{0}</color>", addr);
				}				
			}
			
			result.Add(string.Format("IP игрока: {0}", addr));
			
			var total = "";
			
			foreach(var line in result)						
				total += line + "\n";
			
			SendReply(player, total.TrimEnd('\n'));
			
			SteamResult[player.userID].IsActive = false;
			SteamResult[player.userID].IsReady = false;
		}
		
		private bool IsHasPrivs(BasePlayer player)
		{
			if (player == null) return false;			
			if (player.IsAdmin) return true;
			
			foreach(var group in configData.AllowGroups)
				if (permission.UserHasGroup(player.UserIDString, group))
					return true;
				
			return false;	
		}
		
		#endregion
		
		#region Deserialisation

        private class Bans
        {
            [JsonProperty("players")]
            public Player[] Players;

            public class Player
            {
                [JsonProperty("CommunityBanned")]
                public bool CommunityBanned;
                [JsonProperty("VACBanned")]
                public bool VacBanned;
                [JsonProperty("NumberOfVACBans")]
                public int NumberOfVacBans;
                [JsonProperty("DaysSinceLastBan")]
                public int DaysSinceLastBan;
                [JsonProperty("NumberOfGameBans")]
                public int NumberOfGameBans;
                [JsonProperty("EconomyBan")]
                public string EconomyBan;
            }
        }

        private class Summaries
        {
            [JsonProperty("response")]
            public Content Response;

            public class Content
            {
                [JsonProperty("players")]
                public Player[] Players;

                public class Player
                {
                    [JsonProperty("steamid")]
                    public string SteamId;
                    [JsonProperty("communityvisibilitystate")]
                    public int CommunityVisibilityState;
                    [JsonProperty("profilestate")]
                    public int ProfileState;
                    [JsonProperty("personaname")]
                    public string PersonaName;
                    [JsonProperty("lastlogoff")]
                    public double LastLogOff;
                    [JsonProperty("commentpermission")]
                    public int CommentPermission;
                    [JsonProperty("profileurl")]
                    public string ProfileUrl;
                    [JsonProperty("avatar")]
                    public string Avatar;
                    [JsonProperty("avatarmedium")]
                    public string AvatarMedium;
                    [JsonProperty("avatarfull")]
                    public string AvatarFull;
                    [JsonProperty("personastate")]
                    public int PersonaState;
                    [JsonProperty("realname")]
                    public string RealName;
                    [JsonProperty("primaryclanid")]
                    public string PrimaryClanId;
                    [JsonProperty("timecreated")]
                    public double TimeCreated;
                    [JsonProperty("personastateflags")]
                    public int PersonaStateFlags;
                    [JsonProperty("loccountrycode")]
                    public string LocCountryCode;
                    [JsonProperty("locstatecode")]
                    public string LocStateCode;
                }
            }
        }        

		private class Sharing
        {
            [JsonProperty("response")]
            public Content Response;

            public class Content
            {
                [JsonProperty("lender_steamid")]
                public ulong LenderSteamId;
            }
        }
		
        private class Games
        {
            [JsonProperty("response")]
            public Content Response;

            public class Content
            {
                [JsonProperty("game_count")]
                public int GameCount;
                [JsonProperty("games")]
                public Game[] Games;

                public class Game
                {
                    [JsonProperty("appid")]
                    public uint AppId;
                    [JsonProperty("playtime_2weeks")]
                    public int PlaytimeTwoWeeks;
                    [JsonProperty("playtime_forever")]
                    public int PlaytimeForever;
                }
            }
        }

        #endregion
		
		#region General

        private bool IsValidRequest(string request, ResponseCode code)
        {
            switch (code)
            {
                case ResponseCode.Valid:
                    return true;
                case ResponseCode.InvalidKey:
                    PrintError("Не верный API ключ !");
                    return false;
                case ResponseCode.Unavailable:
                    PrintWarning("Сервис временно недоступен.");
                    return false;
				case ResponseCode.NotAllowed:
				    PrintWarning("Сервис временно недоступен или неверный запрос: \n"+request);
                    return false;
                default:
                    PrintError("Неизвестная ошибка c кодом: "+ code.ToString());
                    return false;
            }
        }
		
		private T Deserialise<T>(string json) => JsonConvert.DeserializeObject<T>(json);
		
		private bool NoKey() => string.IsNullOrEmpty(apiKey);
		
		#endregion
		
		#region Enum

        private enum ResponseCode
        {
            Valid = 200,
            InvalidKey = 403,
            Unavailable = 503,
			NotAllowed = 405
        }

        #endregion
		
		#region Bans

        private bool IsCommunityBanned(ulong moderID) => SteamResult[moderID].bans.Players[0].CommunityBanned;
        private bool IsVacBanned(ulong moderID) => SteamResult[moderID].bans.Players[0].VacBanned;        
        private int VacBanCount(ulong moderID) => SteamResult[moderID].bans.Players[0].NumberOfVacBans;
        private int GameBanCount(ulong moderID) => SteamResult[moderID].bans.Players[0].NumberOfGameBans;
        private int DaysSinceBan(ulong moderID) => SteamResult[moderID].bans.Players[0].DaysSinceLastBan;

        #endregion

        #region Summaries
       
        private int GetCommunityVisibilityState(ulong moderID) => SteamResult[moderID].summaries.Response.Players[0].CommunityVisibilityState;
		private bool IsPrivate(ulong moderID) { return GetCommunityVisibilityState(moderID) < 3; }
        private int GetProfileState(ulong moderID) => SteamResult[moderID].summaries.Response.Players[0].ProfileState;
        private string GetPersonaName(ulong moderID) => SteamResult[moderID].summaries.Response.Players[0].PersonaName;
        private DateTime GetLastLogOff(ulong moderID) => new DateTime(1970, 1, 1).AddSeconds(SteamResult[moderID].summaries.Response.Players[0].LastLogOff);                        
        private int GetPersonaState(ulong moderID) => SteamResult[moderID].summaries.Response.Players[0].PersonaState;
        private string GetRealName(ulong moderID) => SteamResult[moderID].summaries.Response.Players[0].RealName;        
        private DateTime GetTimeCreated(ulong moderID) => new DateTime(1970, 1, 1).AddSeconds(SteamResult[moderID].summaries.Response.Players[0].TimeCreated);
        private int GetPersonaStateFlags(ulong moderID) => SteamResult[moderID].summaries.Response.Players[0].PersonaStateFlags;        

        #endregion        
		
		#region Sharing

        private ulong GetLenderSteamId(ulong moderID) => SteamResult[moderID].sharing.Response.LenderSteamId;
        private bool IsSharing(ulong moderID) => GetLenderSteamId(moderID) > 0;

        #endregion

        #region Games
        
        private int GetPlaytimeForever(ulong moderID) 
		{
			if (SteamResult[moderID].games?.Response?.Games?.Length > 0)
            {              
				try
				{
					return (int)Math.Round(SteamResult[moderID].games.Response.Games.FirstOrDefault(x => x.AppId == appId).PlaytimeForever / 60f);
				}
				catch {}				
            }

            return -1;
		}

        #endregion
		
		#region Init

        private void Init()
        {
			LoadVariables();
            appId = covalence.ClientAppId;            
            InitialiseConfig();            			
			LoadData();			
			
            if (NoKey())
                PrintError("Не задан API ключ !");
        }
		
		private void OnServerInitialized() 
		{
			foreach (BasePlayer player in BasePlayer.activePlayerList)			            
            {
				if (!ActiveUser.Contains(player.UserIDString))
					ActiveUser.Add(player.UserIDString);
			}				
			
			timer.Once(2.2f, ()=> RunTimer());
		}	
		
		private void OnPlayerConnected(BasePlayer player)
        {
        	if(player?.net == null || player.net.connection == null) return;
        	
			if (!ActiveUser.Contains(player.UserIDString))
				ActiveUser.Add(player.UserIDString);						
        }
		
		private void Unload() => RunTimer(true);	
		
		private void OnPlayerDisconnected(BasePlayer player)
		{
			if (player == null) return;
			
			DateTime now = DateTime.Now;
			
			if (!LastBeen.ContainsKey(player.UserIDString))
				LastBeen.Add(player.UserIDString, now.ToString("dd.MM.yyyy HH:mm"));
			else
				LastBeen[player.UserIDString] = now.ToString("dd.MM.yyyy HH:mm");
			
			ActiveUser.Remove(player.UserIDString);	
		}			

        #endregion
		
		#region Checking

        private void StartChecking(string ID, ulong moderID) => CheckBans(ID, moderID);							

        private void CheckBans(string ID, ulong moderID)
        {
            webrequest.Enqueue(string.Format(RootUrl + BansStr, apiKey, ID), null, (code, response) =>
            {
                if (!IsValidRequest(string.Format(RootUrl + BansStr, apiKey, ID), (ResponseCode)code))
				{
					SteamResult[moderID].IsReady = false;                
					SteamResult[moderID].IsActive = false;
					return;
				}

                SteamResult[moderID].bans = Deserialise<Bans>(response);                

                timer.Once(0.5f, ()=>CheckSummaries(ID, moderID));
            }, this);
        }

        private void CheckSummaries(string ID, ulong moderID)
        {
            webrequest.Enqueue(string.Format(RootUrl + PlayerStr, apiKey, ID), null, (code, response) =>
            {
                if (!IsValidRequest(string.Format(RootUrl + PlayerStr, apiKey, ID), (ResponseCode)code))
                {
					SteamResult[moderID].IsReady = false;            
					SteamResult[moderID].IsActive = false;
					return;
				}

                SteamResult[moderID].summaries = Deserialise<Summaries>(response);                
                
				timer.Once(0.5f, ()=>CheckXml(ID, moderID));
            }, this);
        }

        private void CheckXml(string ID, ulong moderID)
        {
            webrequest.Enqueue(string.Format(XmlUrl, ID), null, (code, response) =>
            {
                if (!IsValidRequest(string.Format(XmlUrl, ID), (ResponseCode)code))
                {
					SteamResult[moderID].IsReady = false;          
					SteamResult[moderID].IsActive = false;
					return;
				}

                SteamResult[moderID].IsHaveSetUpProfile = response.ToLower().Contains("this user has not yet set up their steam community profile");                

				timer.Once(0.5f, ()=>CheckSharing(ID, moderID));
            }, this);
        }        

		private void CheckSharing(string ID, ulong moderID)
        {
            webrequest.Enqueue(string.Format(RootUrl + SharingStr, apiKey, ID, appId), null, (code, response) =>
            {
                if (!IsValidRequest(string.Format(RootUrl + SharingStr, apiKey, ID, appId), (ResponseCode)code))
                {
					SteamResult[moderID].IsReady = false;            
					SteamResult[moderID].IsActive = false;
					return;
				}

                SteamResult[moderID].sharing = Deserialise<Sharing>(response);                
                
				timer.Once(0.5f, ()=>CheckGames(ID, moderID));
            }, this);
        }
		
        private void CheckGames(string ID, ulong moderID)
        {
            if (IsSharing(moderID) || IsPrivate(moderID)) 
			{
				SteamResult[moderID].IsReady = true;                
				SteamResult[moderID].IsActive = false;
				return;
			}				

            webrequest.Enqueue(string.Format(RootUrl + GamesStr, apiKey, ID), null, (code, response) =>
            {
                if (!IsValidRequest(string.Format(RootUrl + GamesStr, apiKey, ID), (ResponseCode)code))
				{
					SteamResult[moderID].IsReady = false;     
					SteamResult[moderID].IsActive = false;
					return;
				}

                SteamResult[moderID].games = Deserialise<Games>(response);
				SteamResult[moderID].IsReady = true;                
				SteamResult[moderID].IsActive = false;
            }, this);
        }

        #endregion
				
		#region Config        				
		
		private void InitialiseConfig() => apiKey = configData.ApiKey;		
		
        private static ConfigData configData;
		
        private class ConfigData
        {            
			[JsonProperty(PropertyName = "Ключ API Steam")]
			public string ApiKey;
			[JsonProperty(PropertyName = "Список разрешенных групп, кто имеет право просматривать информацию")]
			public List<string> AllowGroups;
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                ApiKey = "",
				AllowGroups = new List<string>() { "moderator" }
            };
            SaveConfig(configData);
			timer.Once(0.1f, ()=>SaveConfig(configData));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
		
		#region Data
		
		private void LoadData() => LastBeen = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<string, string>>("PlayerInfoData");										
		
		private void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("PlayerInfoData", LastBeen);
		
		#endregion
		
	}
	
}	