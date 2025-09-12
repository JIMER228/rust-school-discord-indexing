using System;
using Random = System.Random;
using System.Linq;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using Rust;
using Newtonsoft.Json;
using Oxide.Core.Configuration;

namespace Oxide.Plugins
{
    [Info("PlayerWatch", "Nimant", "1.0.10")]
    public class PlayerWatch : RustPlugin
    {        

		#region Variables
		
		[PluginReference] 
		private Plugin VkCommandRouter;
		
		private Random Rnd = new Random();
		
		private const string URL_SendMessage = "https://api.vk.com/method/messages.send?peer_id={0}&message={1}&v=5.80&random_id={2}&access_token={3}";
		private const string URL_GetUserInfo = "https://api.vk.com/method/users.get?v=5.73&user_ids={0}&access_token={1}";		
		
		private static Timer timer_;		
		private static Dictionary<string, string> ActiveUser = new Dictionary<string, string>();
		
		private Dictionary<string, string> InfoMessages = new Dictionary<string, string>()
		{
			{ "CMD.RA.HELP", "ДОСТУПНЫЕ КОМАНДЫ:\n/watch vk \"ссылка на профиль\" - прикрепить профиль ВК\n/watch remove - открепить профиль ВК" },
			{ "CMD.RA.ADD.HELP", "Используйте /watch vk \"ссылка на профиль\" чтобы прикрепить профиль ВК.\nПример: /watch vk \"vk.com/durov\" или /watch vk \"vk.com/id1\"" },
			{ "CMD.RA.REM.HELP", "Используйте /watch remove чтобы открепить свой профиль ВК." },									

			{ "VK.PROFILE.ERROR.URL", "Вы указали неверную ссылку на профиль." },
			{ "VK.PROFILE.FIND.DATABASE", "Поиск профиля в базе данных социальной сети ВК..." },
			{ "VK.PROFILE.NOT.FOUND", "Указанный вами профиль не найден в базе данных социальной сети ВК. Проверьте правильность введенных данных." },			
			{ "VK.PROFILE.NOT.LINKED", "Вы не прикрепляли свой профиль ВК." },
			{ "VK.PROFILE.LINKED", "Ваш профиль ВК успешно прикреплен." },			
			{ "VK.PROFILE.SUCCESS.UNLINKED", "Ваш профиль ВК успешно откреплён." },			
			{ "VK.PROFILE.NO.ACCESS", "Сервис ВК временно недоступен. Повторите запрос позже." },
			{ "VK.PROFILE.BAN", "Ошибка отправки сообщения, указанный пользователь ВК в чёрном списке группы." },
			{ "VK.PROFILE.NO.MESSAGE", "Ошибка отправки сообщения, указанный пользователь ВК запретил приём сообщений." },
			{ "VK.PROFILE.SEND.TEST.MESSAGE", "Профиль \"{0} {1}\" в социальной сети ВК успешно найден, пытаемся отправить проверочное сообщение..." },
			{ "VK.PROFILE.ERROR.REQUEST", "При выполнении запроса произошла ошибка, сообщите о проблемах администратору." },
			{ "VK.PROFILE.ERROR.NOT.START.DIALOG", "Для подключения системы оповещений, вам необходимо начать диалог с сообществом ВК. Зайдите в сообщество \"{0}\", нажмите на кнопку \"Написать сообщение\" и отправьте любое сообщение. После чего повторно используйте команду /watch add \"ссылка на профиль\"" },
			{ "VK.PROFILE.REQUEST.UNKNOWN.ERROR", "При выполнении запроса произошла неизвестная ошибка, попробуйте еще раз." },
			{ "VK.PROFILE.REQUEST.WAIT.ERROR", "Ошибка ожидания выполнения запроса, попробуйте еще раз." },
			{ "VK.PROFILE.REQUEST.BLOCK.USER", "Ошибка выполнения запроса, страница указанного пользователя ВК удалена или заблокирована." },
			
			{ "NO.ACCESS", "У вас нет доступа к этой команде." }		
		};
		
		private Dictionary<int, ulong> GetUserInfoStatus = new Dictionary<int, ulong>();				
		private Dictionary<int, bool> SendMessageStatus = new Dictionary<int, bool>();	
		
		#endregion
		
		#region HTTP				

		private ulong ProcessGetUserInfoRequest(BasePlayer player, string response)
        {												
			Dictionary<string, object> Response = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, new KeyValuesConverter());
			
			if (Response.ContainsKey("error"))
			{
				Dictionary<string, object> data = Response["error"] as Dictionary<string, object>;
				
				if (data.ContainsKey("error_code"))
				{
					var code = Convert.ToString(data["error_code"]);					
					
					switch (code)
					{																																		
						case "1":							
							if (player != null)
								SendReply(player, InfoMessages["VK.PROFILE.REQUEST.UNKNOWN.ERROR"]);							
							return 0;
						case "5":						    
							PrintError("Не верный Token ключ !");
							if (player != null)
								SendReply(player, InfoMessages["VK.PROFILE.ERROR.REQUEST"]);
							return 0;	
						case "8":							
							if (player != null)
								SendReply(player, InfoMessages["VK.PROFILE.REQUEST.UNKNOWN.ERROR"]);							
							return 0;	
						case "10":							
							if (player != null)
								SendReply(player, InfoMessages["VK.PROFILE.REQUEST.UNKNOWN.ERROR"]);							
							return 0;		
						case "18":							
							if (player != null)
								SendReply(player, InfoMessages["VK.PROFILE.REQUEST.BLOCK.USER"]);
							return 0;			
						case "113":							
							if (player != null)
								SendReply(player, InfoMessages["VK.PROFILE.NOT.FOUND"]);
							return 0;		
						case "405":							
							if (player != null)
								SendReply(player, InfoMessages["VK.PROFILE.NO.ACCESS"]);
							return 0;	
						case "503":							
							if (player != null)
								SendReply(player, InfoMessages["VK.PROFILE.NO.ACCESS"]);
							return 0;											
						default:
							PrintWarning("Неизвестная ошибка c кодом: "+ code);
							if (player != null)								
								SendReply(player, InfoMessages["VK.PROFILE.REQUEST.UNKNOWN.ERROR"]);								
							return 0;
					}
				}
				else
				{
					PrintWarning("Ошибка возвращаемой структуры данных ВК");
					if (player != null)
						SendReply(player, InfoMessages["VK.PROFILE.REQUEST.UNKNOWN.ERROR"]);						
					return 0;
				}					
			}
						
			if (Response.ContainsKey("response") && (Response["response"] as List<object>).Count() > 0)
			{							
				List<object> json = Response["response"] as List<object>;																																								
				var data = json[0] as Dictionary<string, object>;												
				
				ulong vkID = 0;
				string firstName = "N/A";
				string lastName = "N/A";
				
				if (data.ContainsKey("id")) vkID = (ulong)Convert.ToInt64(data["id"]);				
				if (data.ContainsKey("first_name")) firstName = Convert.ToString(data["first_name"]);												
				if (data.ContainsKey("last_name")) lastName = Convert.ToString(data["last_name"]);																											
										
				if (player != null)	
					SendReply(player, string.Format(InfoMessages["VK.PROFILE.SEND.TEST.MESSAGE"], firstName, lastName));
								
				return vkID;
			}
			
			if (player != null)
				SendReply(player, InfoMessages["VK.PROFILE.REQUEST.UNKNOWN.ERROR"]);				
			
			return 0;
        }
		
		private bool ProcessSendMessageRequest(BasePlayer player, string response, string success)
        {												
			Dictionary<string, object> Response = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, new KeyValuesConverter());
			
			if (Response.ContainsKey("error"))
			{
				Dictionary<string, object> data = Response["error"] as Dictionary<string, object>;
				
				if (data.ContainsKey("error_code"))
				{
					var code = Convert.ToString(data["error_code"]);					
					
					switch (code)
					{				
						case "1":							
							if (player != null)
								SendReply(player, InfoMessages["VK.PROFILE.REQUEST.UNKNOWN.ERROR"]);							
							return false;
						case "5":							
							PrintError("Не верный Token ключ !");
							if (player != null)
								SendReply(player, InfoMessages["VK.PROFILE.ERROR.REQUEST"]);
							return false;	
						case "8":							
							if (player != null)
								SendReply(player, InfoMessages["VK.PROFILE.REQUEST.UNKNOWN.ERROR"]);							
							return false;	
						case "10":							
							if (player != null)
								SendReply(player, InfoMessages["VK.PROFILE.REQUEST.UNKNOWN.ERROR"]);							
							return false;									
						case "405":							
							if (player != null)
								SendReply(player, InfoMessages["VK.PROFILE.NO.ACCESS"]);
							return false;	
						case "503":							
							if (player != null)
								SendReply(player, InfoMessages["VK.PROFILE.NO.ACCESS"]);
							return false;						
						case "900":				
							if (player != null)
								SendReply(player, InfoMessages["VK.PROFILE.BAN"]);
							return false;
						case "901":							
							if (player != null)
								SendReply(player, string.Format(InfoMessages["VK.PROFILE.ERROR.NOT.START.DIALOG"], configData.VkGroupName));							
							return false;
						case "902":				
							if (player != null)
								SendReply(player, InfoMessages["VK.PROFILE.NO.MESSAGE"]);
							return false;									
						default:
							PrintWarning("Неизвестная ошибка c кодом: "+ code);
							if (player != null)								
								SendReply(player, InfoMessages["VK.PROFILE.REQUEST.UNKNOWN.ERROR"]);													
							return false;
					}
				}
				else
				{
					PrintWarning("Ошибка возвращаемой структуры данных ВК");
					if (player != null)
						SendReply(player, InfoMessages["VK.PROFILE.REQUEST.UNKNOWN.ERROR"]);						
					return false;
				}					
			}
			
			if (Response.ContainsKey("response"))
			{
				if (player != null)	
					SendReply(player, success);			
				return true;
			}
			
			if (player != null)
				SendReply(player, InfoMessages["VK.PROFILE.REQUEST.UNKNOWN.ERROR"]);		
			
			return false;
        }				
		
		private void GetUserInfo(BasePlayer player, string VkUser, int ID)
        {						
            webrequest.Enqueue(string.Format(URL_GetUserInfo, VkUser, configData.Token), null, (code, response) =>
            {										
				GetUserInfoStatus.Add(ID, ProcessGetUserInfoRequest(player, response));				
            }, this);
        }
		
		private void SendMessage(BasePlayer player, ulong VkID, string Message, int ID, string success)
        {							
            webrequest.Enqueue(string.Format(URL_SendMessage, VkID, Message, ID, configData.Token), null, (code, response) =>
            {
				SendMessageStatus.Add(ID, ProcessSendMessageRequest(player, response, success));				
            }, this);						
        }
		
		private string TryParseVkNameOrID(string vk)
		{
			string vk_ = vk.ToLower();
			
			//попытка раcпарсить: https://vk.com/id353770849 
			if ((vk_.Contains("/id")||vk_.StartsWith("id")) && vk_.Length>3)
			{
				string result = "";
				int count = 0;
				int startPos = 2;
				if (vk_.Contains("/id"))
					startPos = vk_.IndexOf("/id")+3;
					
				foreach(var ch in vk_)
				{
					if (count >= startPos && "0123456789".IndexOf(ch)>=0)					
						result += ch;					
					else
						if (count >= startPos && "0123456789".IndexOf(ch)<0)
							break;
					
					count++;
				}
				
				if (string.IsNullOrEmpty(result)) return null;
				return result;
			}	
			else
				//попытка раcпарсить: https://vk.com/nimant_true
				if (vk_.Contains(".com/") && vk_.Length>5)
				{
					string result = "";
					int count = 0;
					int startPos = vk_.IndexOf(".com/")+5;											
						
					foreach(var ch in vk_)
					{
						if (count >= startPos && "_0123456789abcdefghijklmnopqrstuvwxyz".IndexOf(ch)>=0)					
							result += ch;					
						else
							if (count >= startPos && "_0123456789abcdefghijklmnopqrstuvwxyz".IndexOf(ch)<0)
								break;
						
						count++;
					}
					
					if (string.IsNullOrEmpty(result)) return null;					
					return result;
				}
				else
				{
					string result = "";
					
					//попытка раcпарсить: 353770849 (указанный без префикса)
					bool notID = false;
					
					foreach(var ch in vk_)
					{
						if ("0123456789".IndexOf(ch)>=0)					
							result += ch;					
						else
						{
							notID = true;		
							break;
						}													
					}
															
					if (!notID && !string.IsNullOrEmpty(result))
						return result;
					
					//попытка раcпарсить: nimant_true (указанный без префикса)
					bool notName = false;
					
					foreach(var ch in vk_)
					{
						if ("_0123456789abcdefghijklmnopqrstuvwxyz".IndexOf(ch)>=0)					
							result += ch;					
						else
						{
							notName = true;		
							break;
						}													
					}
					
					if (!notName && !string.IsNullOrEmpty(result))
						return result;
				}	
				
			return null;		
		}
		
		private void TryGetUserInfo(BasePlayer player, string Vk, int random = -1, int wait = 0, ulong moderID = 0, Action<string> cbSendVkAnswer = null)
		{
			var playerID = player != null ? player.userID : moderID;			
			if (playerID == 0) return;
			
			if (wait>=100)
			{
				if (player != null)
					SendReply(player, InfoMessages["VK.PROFILE.REQUEST.WAIT.ERROR"]);
				else
					cbSendVkAnswer(InfoMessages["VK.PROFILE.REQUEST.WAIT.ERROR"]);
				return;
			}
			
			if (wait==0)
			{
				if (player != null)
					SendReply(player, InfoMessages["VK.PROFILE.FIND.DATABASE"]);				
				else
					cbSendVkAnswer(InfoMessages["VK.PROFILE.FIND.DATABASE"]);				
				
				int random_ = Rnd.Next(1, int.MaxValue);						
				while (GetUserInfoStatus.ContainsKey(random_)) random_ = Rnd.Next(1, int.MaxValue);													
				GetUserInfo(player, Vk, random_); // передавать в player можно null (упрощаем немного для ВК, не выдаем текст ошибок регистрации, для модеров это и так излишне)
				random = random_;
			}
			
			if (GetUserInfoStatus.ContainsKey(random))
			{				
				if (GetUserInfoStatus[random] > 0)															
					TrySendTestMsg(player, GetUserInfoStatus[random], -1, 0, moderID, cbSendVkAnswer);
				
				GetUserInfoStatus.Remove(random);
			}	
			else
			{			
				int wait_ = wait + 1;
				int random_ = random;
				timer.Once(0.1f, ()=> TryGetUserInfo(player, Vk, random_, wait_, moderID, cbSendVkAnswer));
			}	
			
			return;
		}
		
		private void TrySendTestMsg(BasePlayer player, ulong VkID, int random = -1, int wait = 0, ulong moderID = 0, Action<string> cbSendVkAnswer = null)
		{
			var playerID = player != null ? player.userID : moderID;			
			if (playerID == 0) return;
			
			if (wait>=100)
			{
				if (player != null)
					SendReply(player, InfoMessages["VK.PROFILE.REQUEST.WAIT.ERROR"]);
				else
					cbSendVkAnswer(InfoMessages["VK.PROFILE.REQUEST.WAIT.ERROR"]);
				return;
			}
			
			if (wait==0)
			{								
				int random_ = Rnd.Next(1, int.MaxValue);						
				while (SendMessageStatus.ContainsKey(random_)) random_ = Rnd.Next(1, int.MaxValue);																					
				// тут так же player может быть null, т.к. игнорим возможные ошибки ВК при отправке
				SendMessage(player, VkID, string.Format(configData.TestMessage, configData.ServerTitle), random_, InfoMessages["VK.PROFILE.LINKED"]);												
				random = random_;
			}
			
			if (SendMessageStatus.ContainsKey(random))
			{
				if (SendMessageStatus[random])
				{					
					if (!data.UsersVk.ContainsKey(playerID))
						data.UsersVk.Add(playerID, VkID.ToString());
					else
						data.UsersVk[playerID] = VkID.ToString();										
					
					SaveData();
					SendMessageStatus.Remove(random);
				}					
			}
			else
			{
				int wait_ = wait + 1;
				int random_ = random;
				timer.Once(0.1f, ()=> TrySendTestMsg(player, VkID, random_, wait_, moderID, cbSendVkAnswer));
			}	
		}				
		
		#endregion
				
		#region Hooks		
				
		private void OnServerInitialized() 
		{
			foreach (BasePlayer player in BasePlayer.activePlayerList.Where(x=> x.userID.IsSteamId()))			            
            {
				if (!ActiveUser.ContainsKey(player.UserIDString))
				{
					var ip = player.net.connection.ipaddress.Split(new char [] {':'})[0];
					ActiveUser.Add(player.UserIDString, ip);
					ShowMessage(player.userID, ip, "already_connect");
				}
			}				
			
			timer.Once(5.8f, ()=> RunTimer());
		}					
		
		private void Init()
		{
			LoadVariables();
			LoadData();
		}
		
		private void OnPlayerConnected(BasePlayer player)
        {
        	if(player?.net == null || player.net.connection == null) return;
        	
			if (!ActiveUser.ContainsKey(player.UserIDString))
			{
				var ip = player.net.connection.ipaddress.Split(new char [] {':'})[0];
				ActiveUser.Add(player.UserIDString, ip);						
				ShowMessage(player.userID, ip, "connect");
			}
        }
		
		private void Unload() => RunTimer(true);	
		
		private void OnPlayerDisconnected(BasePlayer player)
		{
			if (player == null) return;									
			ActiveUser.Remove(player.UserIDString);			
			var ip = player.net.connection.ipaddress.Split(new char [] {':'})[0];
			ShowMessage(player.userID, ip, "disconnect");
		}
		
		#endregion

		#region Commands
		
		[ChatCommand("watch")]
        private void chatWatch(BasePlayer player, string command, string[] args)
        {						            			
            if (!IsHasPrivs(player, player.userID))
			{
				SendReply(player, "У вас нет прав на эту команду");
				return;
			}
			
			if (args == null || args.Length == 0)
			{
				SendReply(player, "Использование:\n * /watch <add> <часть имени, steam_id или ip> <причина> - отслеживать вход и выход игрока с сервера\n * /watch <remove> <часть имени, steam_id или ip> - прекратить отслеживать игрока\n * /watch <list> - посмотреть свой список отслеживаемых игроков\n * /watch vk <vk.com\\профиль> - прикрепить свой ВК для информирования туда\n * /watch <removeall> - очистить весь свой список слежения");
				return;
			}
			
			var param = args[0].ToLower();
			
			switch (param)
			{
				case "add": 
				{
					if (args.Length == 1)
					{
						SendReply(player, "Вы не указали данные игрока.");
						return;
					}
					AddWatchPlayer(player, args.ToList().Where(x=> IndexOfStr(args, x) > 0).ToArray());
					break;
				}
				case "remove": 
				{
					if (args.Length == 1)
					{
						SendReply(player, "Вы не указали данные игрока.");
						return;
					}
					RemoveWatchPlayer(player, args.ToList().Where(x=> IndexOfStr(args, x) > 0).ToArray());
					break;
				}
				case "list": 
				{					
					ShowWatchPlayer(player);
					break;
				}
				case "removeall": 
				{					
					ClearWatchList(player);
					break;
				}
				case "vk": 
				{					
					if (args.Length == 1)
					{
						SendReply(player, "Вы не указали свой ВК.");
						return;
					}
					
					var vk = TryParseVkNameOrID(args[1]);
				
					if (string.IsNullOrEmpty(vk))
					{
						SendReply(player, InfoMessages["VK.PROFILE.ERROR.URL"]);
						return;
					}	
					
					TryGetUserInfo(player, vk);
					
					break;
				}
				default:
				{
					SendReply(player, "Использование:\n * /watch <add> <часть имени, steam_id или ip> <причина> - отслеживать вход и выход игрока с сервера\n * /watch <remove> <часть имени, steam_id или ip> - прекратить отслеживать игрока\n * /watch <list> - посмотреть свой список отслеживаемых игроков\n * /watch vk <vk.com\\профиль> - прикрепить свой ВК для информирования туда\n * /watch <removeall> - очистить весь свой список слежения");
					return;
				}
			}
		}	
		
		#endregion
		
		#region Commands API
		
		private void API_Watch(ulong moderID, string command, string[] user, string vkStr, Action<string> cbSendVkAnswer)
		{			
			if (!IsHasPrivs(null, moderID))
			{
				cbSendVkAnswer("У вас нет прав на эту команду");
				return;
			}
			
			switch (command)
			{
				case "add": 
				{
					if (user == null || user.Length == 0)
					{
						cbSendVkAnswer("Вы не указали данные игрока.");
						return;
					}
					AddWatchPlayer(null, user, moderID, cbSendVkAnswer);
					break;
				}
				case "remove": 
				{
					if (user == null || user.Length == 0)
					{
						cbSendVkAnswer("Вы не указали данные игрока.");
						return;
					}
					RemoveWatchPlayer(null, user, moderID, cbSendVkAnswer);
					break;
				}
				case "list": 
				{					
					ShowWatchPlayer(null, moderID, cbSendVkAnswer);
					break;
				}
				case "removeall": 
				{					
					ClearWatchList(null, moderID, cbSendVkAnswer);
					break;
				}
				case "vk": 
				{					
					if (string.IsNullOrEmpty(vkStr))
					{
						cbSendVkAnswer("Вы не указали свой ВК.");
						return;
					}
					
					var vk = TryParseVkNameOrID(vkStr);
				
					if (string.IsNullOrEmpty(vk))
					{
						cbSendVkAnswer(InfoMessages["VK.PROFILE.ERROR.URL"]);
						return;
					}	
					
					TryGetUserInfo(null, vk, -1, 0, moderID, cbSendVkAnswer);
					
					break;
				}
				default:
				{
					cbSendVkAnswer("Использование:\n * /watch <add> <часть имени, steam_id или ip> <причина> - отслеживать вход и выход игрока с сервера\n * /watch <remove> <часть имени, steam_id или ip> - прекратить отслеживать игрока\n * /watch <list> - посмотреть свой список отслеживаемых игроков\n * /watch vk <vk.com\\профиль> - прикрепить свой ВК для информирования туда\n * /watch <removeall> - очистить весь свой список слежения");
					return;
				}
			}
		}
		
		#endregion
		
		#region Main
		
		private static bool IsIP(string value)
		{						
			if (string.IsNullOrEmpty(value))
				return false;
			
			if (value.IsSteamId())
				return false;
			
			int cnt = 0;
			foreach (var ch in value)
			{
				if ("0123456789.".IndexOf(ch) < 0)
					return false;
				
				if (ch == '.') cnt++;
			}
			
			return cnt == 3;
		}
		
		private void SendVkReply(ulong userID, string msg)
		{
			if (!data.UsersVk.ContainsKey(userID)) return;
			
			int random_ = Rnd.Next(1, int.MaxValue);
			while (SendMessageStatus.ContainsKey(random_)) random_ = Rnd.Next(1, int.MaxValue);													
			SendMessage(null, (ulong)Convert.ToInt64(data.UsersVk[userID]), msg, random_, null);
			VkCommandRouter?.Call("API_SendInfoMsgVK", msg);
		}
		
		private void ShowMessage(ulong userID, string ip, string oper)
		{
			foreach(var pair in data.WatchPlayers.Where(x=> IsHasPrivs(null, x.Key)))
			{
				var player = BasePlayer.activePlayerList.FirstOrDefault(x=> x.userID == pair.Key);
				//if (player == null) continue;
				
				if (pair.Value.ContainsKey(userID))
				{
					switch (oper)
					{
						case "connect":
						{
							if (player != null)
								SendReply(player, string.Format("<color=#FA8072>Отслеживаемый игрок <color=white>{0} ({1})</color> зашел на сервер.\n{2}</color>", GetPlayerName(userID), userID, pair.Value[userID]));
							else
								SendVkReply(pair.Key, string.Format("🔍 Отслеживаемый игрок '{0}' ({1}) зашел на сервер {2}.\n{3}", FixName(GetPlayerName(userID)), userID, configData.ServerTitle, pair.Value[userID]));
							
							break;
						}
						case "already_connect":
						{
							if (player != null)
								SendReply(player, string.Format("<color=#FA8072>Отслеживаемый игрок <color=white>{0} ({1})</color> находится на сервере.\n{2}</color>", GetPlayerName(userID), userID, pair.Value[userID]));
							else
								SendVkReply(pair.Key, string.Format("🔍 Отслеживаемый игрок '{0}' ({1}) находится на сервере {2}.\n{3}", FixName(GetPlayerName(userID)), userID, configData.ServerTitle, pair.Value[userID]));
							
							break;
						}
						case "disconnect":
						{
							if (player != null)
								SendReply(player, string.Format("<color=#FA8072>Отслеживаемый игрок <color=white>{0} ({1})</color> покинул сервер.\n{2}</color>", GetPlayerName(userID), userID, pair.Value[userID]));
							else
								SendVkReply(pair.Key, string.Format("🔍 Отслеживаемый игрок '{0}' ({1}) покинул сервер {2}.\n{3}", FixName(GetPlayerName(userID)), userID, configData.ServerTitle, pair.Value[userID]));
							
							break;
						}
						case "unnormal_disconnect":
						{
							if (player != null)
								SendReply(player, string.Format("<color=#FA8072>Отслеживаемый игрок <color=white>{0} ({1})</color> покинул сервер, возможно у него был краш.\n{2}</color>", GetPlayerName(userID), userID, pair.Value[userID]));
							else
								SendVkReply(pair.Key, string.Format("🔍 Отслеживаемый игрок '{0}' ({1}) покинул сервер {2}, возможно у него был краш.\n{3}", FixName(GetPlayerName(userID)), userID, configData.ServerTitle, pair.Value[userID]));
							
							break;
						}
					}
				}
			}
			
			if (!string.IsNullOrEmpty(ip) && ip != "0.0.0.0")
				foreach(var pair in data.WatchIPs.Where(x=> IsHasPrivs(null, x.Key)))
				{
					var player = BasePlayer.activePlayerList.FirstOrDefault(x=> x.userID == pair.Key);
					//if (player == null) continue;
					
					if (pair.Value.ContainsKey(ip))
					{
						switch (oper)
						{
							case "connect":
							{
								if (player != null)
									SendReply(player, string.Format("<color=#FA8072>Отслеживаемый по IP <color=white>{0}</color> игрок <color=white>{1} ({2})</color> зашел на сервер.\n{3}</color>", ip, GetPlayerName(userID), userID, pair.Value[ip]));
								else
									SendVkReply(pair.Key, string.Format("🔍 Отслеживаемый по IP {0} игрок '{1}' ({2}) зашел на сервер {3}.\n{4}", ip, FixName(GetPlayerName(userID)), userID, configData.ServerTitle, pair.Value[ip]));
								
								break;
							}
							case "already_connect":
							{
								if (player != null)
									SendReply(player, string.Format("<color=#FA8072>Отслеживаемый по IP <color=white>{0}</color> игрок <color=white>{1} ({2})</color> находится на сервере.\n{3}</color>", ip, GetPlayerName(userID), userID, pair.Value[ip]));
								else
									SendVkReply(pair.Key, string.Format("🔍 Отслеживаемый по IP {0} игрок '{1}' ({2}) находится на сервере {3}.\n{4}", ip, FixName(GetPlayerName(userID)), userID, configData.ServerTitle, pair.Value[ip]));
								
								break;
							}
							case "disconnect":
							{
								if (player != null)
									SendReply(player, string.Format("<color=#FA8072>Отслеживаемый по IP <color=white>{0}</color> игрок <color=white>{1} ({2})</color> покинул сервер.\n{3}</color>", ip, GetPlayerName(userID), userID, pair.Value[ip]));
								else
									SendVkReply(pair.Key, string.Format("🔍 Отслеживаемый по IP {0} игрок '{1}' ({2}) покинул сервер {3}.\n{4}", ip, FixName(GetPlayerName(userID)), userID, configData.ServerTitle, pair.Value[ip]));
								
								break;
							}
							case "unnormal_disconnect":
							{
								if (player != null)
									SendReply(player, string.Format("<color=#FA8072>Отслеживаемый по IP <color=white>{0}</color> игрок <color=white>{1} ({2})</color> покинул сервер, возможно у него был краш.\n{3}</color>", ip, GetPlayerName(userID), userID, pair.Value[ip]));
								else
									SendVkReply(pair.Key, string.Format("🔍 Отслеживаемый по IP {0} игрок '{1}' ({2}) покинул сервер {3}, возможно у него был краш.\n{4}", ip, FixName(GetPlayerName(userID)), userID, configData.ServerTitle, pair.Value[ip]));
								
								break;
							}
						}
					}
				}
		}
		
		private void RunTimer(bool unload = false)
		{
			if (timer_ != null) timer_.Destroy();
			
			DateTime now = DateTime.Now;			
			var players = BasePlayer.activePlayerList.ToList();						
				
			foreach (var pair in ActiveUser.ToDictionary(x=> x.Key, x=> x.Value))				
				if (unload || !players.Exists(x=> x != null && x.UserIDString == pair.Key))																			
				{	
					ActiveUser.Remove(pair.Key);																											
					if (!unload)
						ShowMessage((ulong)Convert.ToInt64(pair.Key), pair.Value, "unnormal_disconnect");
				}			
			
			if (!unload)
				timer_ = timer.Once(60f, ()=>RunTimer());
		}
		
		private int IndexOfStr(string[] array, string item)
		{
			for(int ii = 0; ii < array.Length; ii++)			
				if (array[ii] == item)
					return ii;
			
			return -1;
		}
		
		private void CheckStatus(BasePlayer player, ulong userID, Action<string> cbSendVkAnswer = null)
		{
			var isWasPlayer = player != null;
			timer.Once(1f, ()=>
			{
				if (player != null)
				{					
					if (BasePlayer.activePlayerList.ToList().Exists(x=>x.userID == userID))					
						SendReply(player, string.Format("<color=#FA8072>Отслеживаемый игрок <color=white>{0} ({1})</color> сейчас находится на сервере.</color>", GetPlayerName(userID), userID));					
					else
						SendReply(player, string.Format("<color=#FA8072>Отслеживаемый игрок <color=white>{0} ({1})</color> в данный момент отключён.</color>", GetPlayerName(userID), userID));
				}
				else
					if (!isWasPlayer)
					{
						if (BasePlayer.activePlayerList.ToList().Exists(x=>x.userID == userID))					
							cbSendVkAnswer(string.Format("<color=#FA8072>Отслеживаемый игрок <color=white>{0} ({1})</color> сейчас находится на сервере.</color>", FixName(GetPlayerName(userID)), userID));					
						else
							cbSendVkAnswer(string.Format("<color=#FA8072>Отслеживаемый игрок <color=white>{0} ({1})</color> в данный момент отключён.</color>", FixName(GetPlayerName(userID)), userID));
					}
			});
		}
		
		private void CheckStatus(BasePlayer player, string userIP, Action<string> cbSendVkAnswer = null)
		{
			var isWasPlayer = player != null;
			timer.Once(1f, ()=>
			{
				if (player != null)
				{					
					if (BasePlayer.activePlayerList.ToList().Exists(x=> x.net.connection.ipaddress.Split(new char [] {':'})[0] == userIP))
						SendReply(player, string.Format("<color=#FA8072>Отслеживаемый по IP <color=white>{0}</color> игрок <color=white>{1} ({2})</color> сейчас находится на сервере.</color>", userIP, GetPlayerName(userIP), GetPlayerID(userIP)));
					else
						SendReply(player, string.Format("<color=#FA8072>Отслеживаемый по IP <color=white>{0}</color> игрок в данный момент отключён.</color>", userIP));
				}
				else
					if (!isWasPlayer)
					{
						if (BasePlayer.activePlayerList.ToList().Exists(x=> x.net.connection.ipaddress.Split(new char [] {':'})[0] == userIP))					
							cbSendVkAnswer(string.Format("<color=#FA8072>Отслеживаемый по IP <color=white>{0}</color> игрок <color=white>{1} ({2})</color> сейчас находится на сервере.</color>", userIP, FixName(GetPlayerName(userIP)), GetPlayerID(userIP)));
						else
							cbSendVkAnswer(string.Format("<color=#FA8072>Отслеживаемый по IP <color=white>{0}</color> игрок в данный момент отключён.</color>", userIP));
					}
			});
		}
		
		private void AddWatchPlayer(BasePlayer player, string[] args, ulong moderID = 0, Action<string> cbSendVkAnswer = null)
		{						
			string search = (args != null && args.Length > 0) ? args[0] : "";			
			search = search.Trim().ToLower();
			
			string reason = "";
			
			if (args != null && args.Length > 1)
				for(int ii = 1; ii < args.Length; ii++) reason += args[ii] + " ";
			
			reason = reason.Trim();
			
			List<string> players = new List<string>();
			bool exact = false;
			
			if (search.IsSteamId())
			{
				players.Add(search);
				exact = true;
			}
			
			if (!exact && IsIP(search))
			{
				players.Add(search);
				exact = true;
			}
			
			if (!exact)
			{
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
				if (player != null)
					SendReply(player, "Игрок не найден.");
				else
					cbSendVkAnswer("Игрок не найден.");
				return;
			}
			else 
				if (exact)				
				{
					var playerId = player != null ? player.userID : moderID;
					
					if (IsIP(players[0]))
					{
						if (!data.WatchIPs.ContainsKey(playerId))
							data.WatchIPs.Add(playerId, new Dictionary<string, string>());
						
						if (!data.WatchIPs[playerId].ContainsKey(players[0]))
						{
							data.WatchIPs[playerId].Add(players[0], reason);
							SaveData();
							if (player != null)
								SendReply(player, "IP успешно добавлен в список слежения.");
							else
								cbSendVkAnswer("IP успешно добавлен в список слежения.");
							
							CheckStatus(player, players[0], cbSendVkAnswer);
							return;
						}
						else
						{
							if (player != null)
								SendReply(player, "IP уже был добавлен в список слежения.");
							else
								cbSendVkAnswer("IP уже был добавлен в список слежения.");
							return;
						}	
						
						return;					
					}
					
					if (!data.WatchPlayers.ContainsKey(playerId))
						data.WatchPlayers.Add(playerId, new Dictionary<ulong, string>());
					
					if (!data.WatchPlayers[playerId].ContainsKey((ulong)Convert.ToInt64(players[0])))
					{
						data.WatchPlayers[playerId].Add((ulong)Convert.ToInt64(players[0]), reason);
						SaveData();
						if (player != null)
							SendReply(player, "Игрок успешно добавлен в список слежения.");
						else
							cbSendVkAnswer("Игрок успешно добавлен в список слежения.");
						
						CheckStatus(player, (ulong)Convert.ToInt64(players[0]), cbSendVkAnswer);
						return;
					}
					else
					{
						if (player != null)
							SendReply(player, "Игрок уже был добавлен в список слежения.");
						else
							cbSendVkAnswer("Игрок уже был добавлен в список слежения.");
						return;
					}															
				}
				else
					if (players.Count>1)
					{
						if (player != null)
							SendReply(player, "Уточните поиск, найдено несколько похожих игроков:");
						else
							cbSendVkAnswer("Уточните поиск, найдено несколько похожих игроков:");
						int max=10;
						foreach(var target in players)						
						{
							if (max<=0) 
							{
								if (player != null)
									SendReply(player, "<color=#F08080> ... и другие</color>");
								else
									cbSendVkAnswer("<color=#F08080> ... и другие</color>");
								break;
							}
							if (player != null)
								SendReply(player, " <color=#aae9f2>*</color> "+(users.ContainsKey(target)?users[target]+" ("+target+")":BasePlayer.activePlayerList.ToList().Find(x=>x.UserIDString==target)?.displayName+" ("+target+")"));
							else
								cbSendVkAnswer(" <color=#aae9f2>*</color> "+(users.ContainsKey(target)?users[target]+" ("+target+")":BasePlayer.activePlayerList.ToList().Find(x=>x.UserIDString==target)?.displayName+" ("+target+")"));
							max--;
						}	
					}	
					else											
					{
						var playerId = player != null ? player.userID : moderID;
						
						if (!data.WatchPlayers.ContainsKey(playerId))
							data.WatchPlayers.Add(playerId, new Dictionary<ulong, string>());
						
						if (!data.WatchPlayers[playerId].ContainsKey((ulong)Convert.ToInt64(players[0])))
						{
							data.WatchPlayers[playerId].Add((ulong)Convert.ToInt64(players[0]), reason);
							SaveData();
							if (player != null)
								SendReply(player, "Игрок успешно добавлен в список слежения.");
							else
								cbSendVkAnswer("Игрок успешно добавлен в список слежения.");
							
							CheckStatus(player, (ulong)Convert.ToInt64(players[0]), cbSendVkAnswer);
							return;
						}
						else
						{
							if (player != null)
								SendReply(player, "Игрок уже был добавлен в список слежения.");
							else
								cbSendVkAnswer("Игрок уже был добавлен в список слежения.");
							return;
						}
					}
		}
		
		private void RemoveWatchPlayer(BasePlayer player, string[] args, ulong moderID = 0, Action<string> cbSendVkAnswer = null)
		{						
			string search = "";
			foreach(var str in args) search += str + " ";
			search = search.Trim().ToLower();
			
			var playerId = player != null ? player.userID : moderID;
			
			if (!data.WatchPlayers.ContainsKey(playerId) && !data.WatchIPs.ContainsKey(playerId))
			{
				if (player != null)
					SendReply(player, "Нечего удалять, ваш список слежения пуст.");
				else
					cbSendVkAnswer("Нечего удалять, ваш список слежения пуст.");
				return;
			}						
			
			Dictionary<string, string> users = new Dictionary<string, string>();
			List<string> players = new List<string>();
			bool exact = false;
			
			if (search.IsSteamId())			
			{
				players.Add(search);
				exact = true;
			}
			
			if (!exact && IsIP(search))
			{
				players.Add(search);
				exact = true;
			}
			else
			{
				if (data.WatchPlayers.ContainsKey(playerId))
					foreach(var target in BasePlayer.activePlayerList.Where(x=> data.WatchPlayers[playerId].ContainsKey(x.userID)))
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
				
				if (!exact)
				{	
					users = permission.GetUsersInGroup("default").ToDictionary(x=>x.Substring(0, x.IndexOf(" ")), x=>x.Substring(x.IndexOf(" ")+1, x.Length-x.IndexOf(" ")-1 ).Trim(')', '('));							
			
					if (data.WatchPlayers.ContainsKey(playerId))
						foreach(var user in users.Where(x=> data.WatchPlayers[playerId].ContainsKey((ulong)Convert.ToInt64(x.Key))))
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
			}
			
			if (players.Count == 0)
			{
				if (player != null)
					SendReply(player, "Игрок не найден.");
				else
					cbSendVkAnswer("Игрок не найден.");
				return;
			}
			else 
				if (exact)				
				{										
					if (IsIP(players[0]))
					{
						if (data.WatchIPs.ContainsKey(playerId) && data.WatchIPs[playerId].ContainsKey(players[0]))
						{
							data.WatchIPs[playerId].Remove(players[0]);
							SaveData();
							if (player != null)
								SendReply(player, "IP успешно удалён из списка слежения.");
							else
								cbSendVkAnswer("IP успешно удалён из списка слежения.");
							return;
						}
						else
						{
							if (player != null)
								SendReply(player, "IP уже был удалён из списка слежения.");
							else
								cbSendVkAnswer("IP уже был удалён из списка слежения.");
							return;
						}
					
						return;
					}
			
					if (data.WatchPlayers.ContainsKey(playerId) && data.WatchPlayers[playerId].ContainsKey((ulong)Convert.ToInt64(players[0])))
					{
						data.WatchPlayers[playerId].Remove((ulong)Convert.ToInt64(players[0]));
						SaveData();
						if (player != null)
							SendReply(player, "Игрок успешно удалён из списка слежения.");
						else
							cbSendVkAnswer("Игрок успешно удалён из списка слежения.");
						return;
					}
					else
					{
						if (player != null)
							SendReply(player, "Игрок уже был удалён из списка слежения.");
						else
							cbSendVkAnswer("Игрок уже был удалён из списка слежения.");
						return;
					}															
				}
				else
					if (players.Count>1)
					{
						if (player != null)
							SendReply(player, "Уточните поиск, найдено несколько похожих игроков:");
						else
							cbSendVkAnswer("Уточните поиск, найдено несколько похожих игроков:");
						int max=10;
						foreach(var target in players)						
						{
							if (max<=0) 
							{
								if (player != null)
									SendReply(player, "<color=#F08080> ... и другие</color>");
								else
									cbSendVkAnswer("<color=#F08080> ... и другие</color>");
								break;
							}
							if (player != null)
								SendReply(player, " <color=#aae9f2>*</color> "+(users.ContainsKey(target)?users[target]+" ("+target+")":BasePlayer.activePlayerList.ToList().Find(x=>x.UserIDString==target)?.displayName+" ("+target+")"));
							else
								cbSendVkAnswer(" <color=#aae9f2>*</color> "+(users.ContainsKey(target)?users[target]+" ("+target+")":BasePlayer.activePlayerList.ToList().Find(x=>x.UserIDString==target)?.displayName+" ("+target+")"));
							max--;
						}	
					}	
					else											
					{
						if (data.WatchPlayers.ContainsKey(playerId) && data.WatchPlayers[playerId].ContainsKey((ulong)Convert.ToInt64(players[0])))
						{
							data.WatchPlayers[playerId].Remove((ulong)Convert.ToInt64(players[0]));
							SaveData();
							if (player != null)
								SendReply(player, "Игрок успешно удалён из списка слежения.");
							else
								cbSendVkAnswer("Игрок успешно удалён из списка слежения.");
							return;
						}
						else
						{
							if (player != null)
								SendReply(player, "Игрок уже был удалён из списка слежения.");
							else
								cbSendVkAnswer("Игрок уже был удалён из списка слежения.");
							return;
						}
					}
		}
		
		private void ShowWatchPlayer(BasePlayer player, ulong moderID = 0, Action<string> cbSendVkAnswer = null)
		{
			var playerId = player != null ? player.userID : moderID;
			
			if ( (!data.WatchPlayers.ContainsKey(playerId) || data.WatchPlayers[playerId].Count() == 0) && 
			     (!data.WatchIPs.ContainsKey(playerId) || data.WatchIPs[playerId].Count() == 0) )
			{
				if (player != null)
					SendReply(player, "Ваш список слежения пуст.");
				else
					cbSendVkAnswer("Ваш список слежения пуст.");
				return;
			}
			
			var result = "Ваш список слежения:\n";						
			
			if (data.WatchPlayers.ContainsKey(playerId))
				foreach(var target in data.WatchPlayers[playerId])
					result += string.Format("{0} ({1})\n", GetPlayerName(target.Key), target.Key);
					
			if (data.WatchIPs.ContainsKey(playerId))
				foreach(var target in data.WatchIPs[playerId])
					result += string.Format("IP {0}\n", target.Key);		
			
			if (player != null)
			{
				SendReply(player, result.TrimEnd('\n'));
				PrintToConsole(player, result.TrimEnd('\n'));
			}
			else			
				cbSendVkAnswer(FixName(result).TrimEnd('\n'));
		}
		
		private void ClearWatchList(BasePlayer player, ulong moderID = 0, Action<string> cbSendVkAnswer = null)
		{
			var playerId = player != null ? player.userID : moderID;
			
			if (data.WatchPlayers.ContainsKey(playerId))
				data.WatchPlayers[playerId].Clear();
			
			if (data.WatchIPs.ContainsKey(playerId))
				data.WatchIPs[playerId].Clear();
			
			SaveData();
			
			if (player != null)
				SendReply(player, "Ваш список слежения очищен.");
			else
				cbSendVkAnswer("Ваш список слежения очищен.");
		}		
		
		private static string FixName(string name) => name.Replace("&","_").Replace("#","_").Replace("?","_");
		
		private string GetPlayerName(ulong userID)
		{
			var data = permission.GetUserData(userID.ToString());															
			return data.LastSeenNickname;
		}
		
		private string GetPlayerName(string userIP)
		{
			var player = BasePlayer.activePlayerList.FirstOrDefault(x=> x.net.connection.ipaddress.Split(new char [] {':'})[0] == userIP);
			
			if (player != null)
				return player.displayName;
			
			return "Unnamed";
		}
		
		private ulong GetPlayerID(string userIP)
		{
			var player = BasePlayer.activePlayerList.FirstOrDefault(x=> x.net.connection.ipaddress.Split(new char [] {':'})[0] == userIP);
			
			if (player != null)
				return player.userID;
			
			return 0;
		}
		
		private bool IsHasPrivs(BasePlayer player, ulong userID)
		{			
			if (player != null && player.IsAdmin) return true;
			
			foreach(var group in configData.AllowGroups)
				if (permission.UserHasGroup(userID.ToString(), group))
					return true;
				
			return false;	
		}				
		
		#endregion
		
		#region Config        								
		
        private static ConfigData configData;
		
        private class ConfigData
        {            			
			[JsonProperty(PropertyName = "Список разрешенных групп, кто имеет право работать с плагином")]
			public List<string> AllowGroups;
			[JsonProperty(PropertyName = "Токен группы ВК")]
			public string Token;			
			[JsonProperty(PropertyName = "Имя группы ВК")]
			public string VkGroupName;
			[JsonProperty(PropertyName = "Название сервера")]
			public string ServerTitle;
			[JsonProperty(PropertyName = "Сообщение о подключении оповещений в ВК")]
			public string TestMessage;
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {                				
				AllowGroups = new List<string>() { "moderator" },
				Token = null,
				VkGroupName = "Test",
				ServerTitle = "Test",
				TestMessage = "WATCH:\nВы успешно подключили модераторскую систему оповещений на сервере {0}",
            };
            SaveConfig(configData);
			timer.Once(0.1f, ()=>SaveConfig(configData));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
		
		#region Data
		
		private static DataClass data = new DataClass();
		
		private class DataClass
		{
			public Dictionary<ulong, Dictionary<ulong, string>> WatchPlayers = new Dictionary<ulong, Dictionary<ulong, string>>();
			public Dictionary<ulong, Dictionary<string, string>> WatchIPs = new Dictionary<ulong, Dictionary<string, string>>();
			public Dictionary<ulong, string> UsersVk = new Dictionary<ulong, string>();
		}
		
		private class DataClassOld
		{
			public Dictionary<ulong, List<ulong>> WatchPlayers = new Dictionary<ulong, List<ulong>>();
			public Dictionary<ulong, List<string>> WatchIPs = new Dictionary<ulong, List<string>>();
			public Dictionary<ulong, string> UsersVk = new Dictionary<ulong, string>();
		}
		
		private void LoadData() 
		{
			int err = 0;
			try { data = Interface.GetMod().DataFileSystem.ReadObject<DataClass>("PlayerWatchData"); }
			catch { err = 1; }
			
			if (err == 1)
			{
				var dataOld = Interface.GetMod().DataFileSystem.ReadObject<DataClassOld>("PlayerWatchData");				
				data = new DataClass();
				
				data.UsersVk = dataOld.UsersVk;
				
				foreach (var pair in dataOld.WatchPlayers)
				{
					data.WatchPlayers.Add(pair.Key, new Dictionary<ulong, string>());
					foreach (var list in pair.Value)					
						data.WatchPlayers[pair.Key].Add(list, "");
				}
				
				foreach (var pair in dataOld.WatchIPs)
				{
					data.WatchIPs.Add(pair.Key, new Dictionary<string, string>());
					foreach (var list in pair.Value)					
						data.WatchIPs[pair.Key].Add(list, "");
				}
				
				SaveData();
			}
			
			if (data.WatchIPs == null)
			{
				data.WatchIPs = new Dictionary<ulong, Dictionary<string, string>>();
				SaveData();
			}
		}
		
		private void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("PlayerWatchData", data);				
		
		#endregion
		
    }
}