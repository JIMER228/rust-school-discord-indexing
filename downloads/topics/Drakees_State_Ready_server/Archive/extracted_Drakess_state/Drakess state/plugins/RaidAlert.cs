using System;
using System.Linq;
using Random=System.Random;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using UnityEngine;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("RaidAlert", "Nimant", "1.0.10", ResourceId = 0)]
    class RaidAlert : RustPlugin
    {            		
				
		#region Variables
		
		[PluginReference]
        private Plugin Friends;

		private const string URL_SendMessage = "https://api.vk.com/method/messages.send?user_id={0}&message={1}&v=5.68&random_id={2}&access_token={3}";
		private const string URL_GetUserInfo = "https://api.vk.com/method/users.get?v=5.73&user_ids={0}&access_token={1}";
		private const string AllowPermission = "raidalert.access";		
		private static Random Rnd = new Random();
		private static Dictionary<ulong, Dictionary<string, string>> UsersVk = new Dictionary<ulong, Dictionary<string, string>>();								
		private Dictionary<ulong, DateTime> LastChangeVk = new Dictionary<ulong, DateTime>();
		private Dictionary<ulong, string> OldUsersVk = new Dictionary<ulong, string>();
		private static Dictionary<ulong, string> AnswerVk = new Dictionary<ulong, string>();
		private static Dictionary<ulong, string> PossibleVk = new Dictionary<ulong, string>();
		
		private Dictionary<string, string> InfoMessages = new Dictionary<string, string>()
		{
			{ "CMD.RA.HELP", "ДОСТУПНЫЕ КОМАНДЫ:\n/ra vk \"ссылка на профиль\" - прикрепить профиль ВК\n/ra remove - открепить профиль ВК" },
			{ "CMD.RA.ADD.HELP", "Используйте /ra vk \"ссылка на профиль\" чтобы прикрепить профиль ВК.\nПример: /ra vk \"vk.com/durov\" или /ra vk \"vk.com/id1\"" },
			{ "CMD.RA.REM.HELP", "Используйте /ra remove чтобы открепить свой профиль ВК от системы оповещений о рейде." },									

			{ "VK.PROFILE.ERROR.URL", "Вы указали неверную ссылку на профиль." },
			{ "VK.PROFILE.FIND.DATABASE", "Поиск профиля в базе данных социальной сети ВК..." },
			{ "VK.PROFILE.NOT.FOUND", "Указанный вами профиль не найден в базе данных социальной сети ВК. Проверьте правильность введенных данных." },			
			{ "VK.PROFILE.NOT.LINKED", "Вы не прикрепляли свой профиль ВК к системе оповещений о рейде." },
			{ "VK.PROFILE.LINKED", "Ваш профиль ВК успешно прикреплен к системе оповещений о рейде." },
			{ "VK.PROFILE.WAIT", "Вам нужно подождать {0}м. прежде чем вы сможете снова сменить ссылку на профиль ВК." },
			{ "VK.PROFILE.SUCCESS.UNLINKED", "Ваш профиль ВК успешно отвязан от системы оповещений о рейде." },			
			{ "VK.PROFILE.NO.ACCESS", "Сервис ВК временно недоступен. Повторите запрос позже." },
			{ "VK.PROFILE.BAN", "Ошибка отправки сообщения, указанный пользователь ВК в чёрном списке группы." },
			{ "VK.PROFILE.NO.MESSAGE", "Ошибка отправки сообщения, указанный пользователь ВК запретил приём сообщений." },
			{ "VK.PROFILE.SEND.TEST.MESSAGE", "Профиль \"{0} {1}\" в социальной сети ВК успешно найден, пытаемся отправить проверочное сообщение..." },
			{ "VK.PROFILE.ERROR.REQUEST", "При выполнении запроса произошла ошибка, сообщите о проблемах администратору." },
			{ "VK.PROFILE.ERROR.NOT.START.DIALOG", "Для подключения системы оповещений о рейде вам необходимо начать диалог с сообществом ВК. Зайдите в сообщество \"{0}\", нажмите на кнопку \"Написать сообщение\" и отправьте любое сообщение. После чего повторно используйте команду /ra add \"ссылка на профиль\"" },
			{ "VK.PROFILE.REQUEST.UNKNOWN.ERROR", "При выполнении запроса произошла неизвестная ошибка, попробуйте еще раз." },
			{ "VK.PROFILE.REQUEST.WAIT.ERROR", "Ошибка ожидания выполнения запроса, попробуйте еще раз." },
			{ "VK.PROFILE.REQUEST.BLOCK.USER", "Ошибка выполнения запроса, страница указанного пользователя ВК удалена или заблокирована." },
			
			{ "NO.ACCESS", "У вас нет доступа к этой команде." },

			{ "CMD.REGVK.HELP", "Используйте /regvk \"ссылка на профиль\", чтобы прикрепить профиль ВК к вашему Steam аккаунту.\nИли /regvk \"проверочное слово\", что бы завершить прикрепление профиля ВК." },
			{ "REGVK.PROFILE.OK", "Вы успешно прикрепили ваш ВК к Steam аккаунту." },
			{ "REGVK.PROFILE.NO", "Неверное проверочное слово. Убедитесь что вы указали правильный свой профиль ВК и попробуйте прикрепить профиль заново." },
			{ "REGVK.PROFILE.SEND.ANSWER.MESSAGE", "Профиль \"{0} {1}\" в социальной сети ВК успешно найден, пытаемся отправить проверочное сообщение.\nНайдите его у себя в личных сообщениях ВК и следуйте инструкции что там описана." }
		};
		
		private Dictionary<int, ulong> GetUserInfoStatus = new Dictionary<int, ulong>();				
		private Dictionary<int, bool> SendMessageStatus = new Dictionary<int, bool>();		
		
		#endregion
				
		#region HTTP				

		private ulong ProcessGetUserInfoRequest(BasePlayer player, string response, bool isRegVk)
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
				{
					if (!isRegVk)
						SendReply(player, string.Format(InfoMessages["VK.PROFILE.SEND.TEST.MESSAGE"], firstName, lastName));
					else
						SendReply(player, string.Format(InfoMessages["REGVK.PROFILE.SEND.ANSWER.MESSAGE"], firstName, lastName));
				} 
								
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
		
		private void GetUserInfo(BasePlayer player, string VkUser, int ID, bool isRegVk)
        {						
            webrequest.Enqueue(string.Format(URL_GetUserInfo, VkUser, configData.Token), null, (code, response) =>
            {										
				GetUserInfoStatus.Add(ID, ProcessGetUserInfoRequest(player, response, isRegVk));				
            }, this);
        }
		
		private void SendMessage(BasePlayer player, ulong VkID, string Message, int ID, string success)
        {							
            webrequest.Enqueue(string.Format(URL_SendMessage, VkID, Message, ID, configData.Token), null, (code, response) =>
            {
				SendMessageStatus.Add(ID, ProcessSendMessageRequest(player, response, success));				
            }, this);
        }
		
		#endregion
		
		#region Main				
		
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
						if (count >= startPos && "_0123456789abcdefghijklmnopqrstuvwxyz.".IndexOf(ch)>=0)					
							result += ch;					
						else
							if (count >= startPos && "_0123456789abcdefghijklmnopqrstuvwxyz.".IndexOf(ch)<0)
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
		
		private void TryGetUserInfo(BasePlayer player, string Vk, int random = -1, int wait = 0, bool isRegVk = false)
		{
			if (player == null) return;						
			
			if (wait>=100)
			{
				SendReply(player, InfoMessages["VK.PROFILE.REQUEST.WAIT.ERROR"]);
				return;
			}
			
			if (wait==0)
			{
				SendReply(player, InfoMessages["VK.PROFILE.FIND.DATABASE"]);				
				int random_ = Rnd.Next(1, int.MaxValue);						
				while (GetUserInfoStatus.ContainsKey(random_)) random_ = Rnd.Next(1, int.MaxValue);													
				GetUserInfo(player, Vk, random_, isRegVk);												
				random = random_;				
			}
			
			if (GetUserInfoStatus.ContainsKey(random))
			{				
				if (GetUserInfoStatus[random] > 0)															
					TrySendTestMsg(player, GetUserInfoStatus[random], -1, 0, isRegVk);
				
				GetUserInfoStatus.Remove(random);
			}	
			else
			{			
				int wait_ = wait + 1;
				int random_ = random;
				timer.Once(0.1f, ()=> TryGetUserInfo(player, Vk, random_, wait_, isRegVk));
			}							
		}
		
		private void TrySendTestMsg(BasePlayer player, ulong VkID, int random = -1, int wait = 0, bool isRegVk = false)
		{
			if (player == null) return;
			
			if (wait>=100)
			{
				SendReply(player, InfoMessages["VK.PROFILE.REQUEST.WAIT.ERROR"]);
				return;
			}
			
			if (wait==0)
			{								
				int random_ = Rnd.Next(1, int.MaxValue);						
				while (SendMessageStatus.ContainsKey(random_)) random_ = Rnd.Next(1, int.MaxValue);
				
				if (isRegVk)
					SendMessage(player, VkID, string.Format(configData.RegMessage, configData.ServerName, GetRandomAnswer(player.userID)), random_, "");
				else									
					SendMessage(player, VkID, string.Format(configData.TestMessage, configData.ServerName), random_, InfoMessages["VK.PROFILE.LINKED"]);												
				random = random_;
			}
			
			if (SendMessageStatus.ContainsKey(random))
			{
				if (SendMessageStatus[random])
				{
					if (!isRegVk)
					{
						if (!UsersVk.ContainsKey(player.userID))
							UsersVk.Add(player.userID, new Dictionary<string, string>() { {"VK", VkID.ToString()}, {"DT", ""} });
						else
							UsersVk[player.userID] = new Dictionary<string, string>() { {"VK", VkID.ToString()}, {"DT", ""} };
					}
					else
					{
						if (!PossibleVk.ContainsKey(player.userID))
							PossibleVk.Add(player.userID, VkID.ToString());
						else
							PossibleVk[player.userID] = VkID.ToString();
					}
					
					if (!LastChangeVk.ContainsKey(player.userID))
						LastChangeVk.Add(player.userID, DateTime.Now);
					else
						LastChangeVk[player.userID] = DateTime.Now;
					
					SaveData();
					SendMessageStatus.Remove(random);
				}					
			}
			else
			{
				int wait_ = wait + 1;
				int random_ = random;
				timer.Once(0.1f, ()=> TrySendTestMsg(player, VkID, random_, wait_, isRegVk));
			}	
		}
		
		private void TrySendUnlinkMsg(BasePlayer player, ulong VkID, int random = -1, int wait = 0)
		{
			if (player == null) return;
			
			if (wait>=100)
			{
				SendReply(player, InfoMessages["VK.PROFILE.REQUEST.WAIT.ERROR"]);
				return;
			}
			
			if (wait==0)
			{
				int random_ = Rnd.Next(1, int.MaxValue);					
				while (SendMessageStatus.ContainsKey(random_)) random_ = Rnd.Next(1, int.MaxValue);													
				SendMessage(player, VkID, string.Format(configData.MessageOff, configData.ServerName), random_, InfoMessages["VK.PROFILE.SUCCESS.UNLINKED"]);												
				random = random_;
			}
			
			if (SendMessageStatus.ContainsKey(random))
			{
				if (SendMessageStatus[random])
				{					
					UsersVk.Remove(player.userID);					
					SaveData();					
					SendMessageStatus.Remove(random);
				}					
			}
			else
			{
				int wait_ = wait + 1;
				int random_ = random;
				timer.Once(0.1f, ()=> TrySendUnlinkMsg(player, VkID, random_, wait_));
			}	
		}				
		
		#endregion
		
		#region Commands
		
		[ChatCommand("ra")]
		private void CmdChatRA(BasePlayer player, string command, string[] args)
        {
            if (player==null) return;
			
			if(!IsHavePerm(player))
			{				
				SendReply(player, InfoMessages["NO.ACCESS"]);
				return;
			}	
						
			if (args?.Length>=1)
			{								
				if (args[0].ToLower() != "vk" && args[0].ToLower() != "remove")
				{								
					SendReply(player, InfoMessages["CMD.RA.HELP"]);				
					return;				
				}			

				if (!( (args.Length==2 && args[0].ToLower()=="vk") || (args.Length==1 && args[0].ToLower()=="remove") || (args.Length==2 && args[0].ToLower()=="remove") ))
				{	
					if (args[0].ToLower()=="vk")
						SendReply(player, InfoMessages["CMD.RA.ADD.HELP"]);
					else
						SendReply(player, InfoMessages["CMD.RA.REM.HELP"]);
										
					return;				
				}				
				
				if (args[0].ToLower()=="remove")
				{					
					if (UsersVk.ContainsKey(player.userID))											
						TrySendUnlinkMsg(player, (ulong)Convert.ToInt64((UsersVk[player.userID])["VK"]));
					else
						SendReply(player, InfoMessages["VK.PROFILE.NOT.LINKED"]);															
					
					return;
				}	
				
				if (LastChangeVk.ContainsKey(player.userID))
				{
					var min = DateTime.Now.Subtract(LastChangeVk[player.userID]).TotalMinutes;
					if (min < configData.NextRegTime)
					{
						SendReply(player, string.Format(InfoMessages["VK.PROFILE.WAIT"], (int)Math.Ceiling(min)));
						return;
					}	
				}	
				
				var vk = TryParseVkNameOrID(args[1]);
				
				if (string.IsNullOrEmpty(vk))
				{
					SendReply(player, InfoMessages["VK.PROFILE.ERROR.URL"]);
					return;
				}	
				
				TryGetUserInfo(player, vk);								
			}
			else
			{								
				SendReply(player, InfoMessages["CMD.RA.HELP"]);				
				return;				
			}				
        }
		
		[ChatCommand("regvk")]
		private void CmdChatRegVk(BasePlayer player, string command, string[] args)
        {
            if (player==null) return;
						
			if (args?.Length>=1)
			{								
				if (args[0].StartsWith("answer"))
				{
					if (IsNormVkAnswer(player.userID, args[0]))
					{
						AddRegVk(player.userID);
						SendReply(player, InfoMessages["REGVK.PROFILE.OK"]);
					}
					else
						SendReply(player, InfoMessages["REGVK.PROFILE.NO"]);
					
					return;
				}
				
				if (LastChangeVk.ContainsKey(player.userID))
				{
					var min = DateTime.Now.Subtract(LastChangeVk[player.userID]).TotalMinutes;
					if (min < configData.NextRegTime)
					{
						SendReply(player, string.Format(InfoMessages["VK.PROFILE.WAIT"], (int)Math.Ceiling(min)));
						return;
					}	
				}	
				
				var vk = TryParseVkNameOrID(args[0]);
				
				if (string.IsNullOrEmpty(vk))
				{
					SendReply(player, InfoMessages["VK.PROFILE.ERROR.URL"]);
					return;
				}	
				
				TryGetUserInfo(player, vk, -1, 0, true);								
			}
			else
			{								
				SendReply(player, InfoMessages["CMD.REGVK.HELP"]);
				return;				
			}				
        }
		
		#endregion				
		
		#region Raid
		
		private void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
        {            
            if (hitInfo == null || entity == null) return;            

            if (hitInfo.Initiator != null)
            {
                BasePlayer initiator = hitInfo.Initiator.ToPlayer();
				
                if (initiator is BasePlayer)
                    if (IsEntityRaidable(entity))
						CheckAndSendInfo(initiator, entity);
            }
			
			//Puts($"{entity.ShortPrefabName}");
        }
		
		public bool IsEntityRaidable(BaseCombatEntity entity)
        {						
			if (entity is BuildingBlock)				
				if ((entity as BuildingBlock).grade.ToString() == "Twigs") return false;

            string prefabName = entity is BuildingBlock ? (entity as BuildingBlock).grade.ToString() + "," + entity.ShortPrefabName : entity.ShortPrefabName;
			
            foreach (var p in configData.InfoBlocks)            
                if (p.Key == prefabName) return true;                            

            return false;
        }
		
		private void CheckAndSendInfo(BasePlayer raider, BaseCombatEntity entity)
		{
			var players = BasePlayer.activePlayerList.ToList();
			
			foreach(var player in GetRaidedPlayers(entity))
			{
				if (IsHavePerm(player) && UsersVk.ContainsKey(player))
				{									
					if (!players.Exists(x=> x != null && x.userID==player) && !string.IsNullOrEmpty((UsersVk[player])["DT"]) && DateTime.Now.Subtract(Convert.ToDateTime((UsersVk[player])["DT"])).TotalSeconds <= configData.NextSendTime*60f)
						continue;
			
					if (raider.userID==player) continue;
			
					if (configData.NoInfoIfFriends && Friends != null)
					{
						var areFriends = Friends.CallHook("AreFriends", player, raider.userID);
						if (areFriends != null && areFriends is bool)						
							if (Convert.ToBoolean(areFriends)==true)
								continue;							
					}
			
					var obj = configData.DefaultBlock;
					
					string type = "";
					if (entity is BuildingBlock) type = (entity as BuildingBlock).grade.ToString() + ",";
					
					if (configData.InfoBlocks.ContainsKey($"{type}{entity.ShortPrefabName}"))
						obj = configData.InfoBlocks[$"{type}{entity.ShortPrefabName}"];															
						
					if (players.Exists(x=> x != null && x.userID==player))
						SendReply(players.Find(x=> x.userID==player), string.Format(configData.SrvRaidMessage, raider.displayName, $"{obj.pre} <color={configData.ColorBlock}>{obj.name}</color>"));
					else
					{	
						int random_ = Rnd.Next(1, int.MaxValue);
						while (SendMessageStatus.ContainsKey(random_)) random_ = Rnd.Next(1, int.MaxValue);													
						SendMessage(null, (ulong)Convert.ToInt64((UsersVk[player])["VK"]), string.Format(configData.VkRaidMessage, FixName(raider.displayName), configData.ServerName, $"{obj.pre} {obj.name}"), random_, null);																		
					}
					
					(UsersVk[player])["DT"] = DateTime.Now.ToString();
					SaveData();
				}
			}
		}								

		private List<ulong> GetRaidedPlayers(BaseCombatEntity entity)
        {            		
			List<ulong> result = new List<ulong>();
			
			if (entity.OwnerID>0)
				result.Add(entity.OwnerID);
			
			var bp = entity.GetBuildingPrivilege();			
            if (bp == null) return result;              
			
			foreach(var player in bp.authorizedPlayers)
			{
				if (!result.Contains(player.userid))
					result.Add(player.userid);					                    
			}            
            			
			return result;
        }
		
		#endregion
		
		#region Common
		
		private bool IsHavePerm(BasePlayer player)
		{								
			if (permission.UserHasPermission(player.UserIDString, AllowPermission))						
				return true;
			
			return false;			
		}
		
		private bool IsHavePerm(ulong userID)
		{									
			if (permission.UserHasPermission(userID.ToString(), AllowPermission))						
				return true;
			
			return false;			
		}
		
		private static string FixName(string name) => name.Replace("&","_").Replace("#","_");
		
		private static string GetRandomAnswer(ulong userID)
		{
			if (!AnswerVk.ContainsKey(userID))
				AnswerVk.Add(userID, null);
			
			AnswerVk[userID] = "answer" + Rnd.Next(100000, 999999999).ToString();
			return AnswerVk[userID];
		}
		
		private static bool IsNormVkAnswer(ulong userID, string answer)
		{
			if (string.IsNullOrEmpty(answer) || !AnswerVk.ContainsKey(userID))
				return false;
			
			return AnswerVk[userID] == answer.ToLower().Trim();
		}
		
		private static void AddRegVk(ulong userID)
		{
			if (!PossibleVk.ContainsKey(userID)) return; // такого не должно быть
			
			if (!UsersVk.ContainsKey(userID))
				UsersVk.Add(userID, new Dictionary<string, string>() { {"VK", PossibleVk[userID]}, {"DT", ""} });
			else
				UsersVk[userID] = new Dictionary<string, string>() { {"VK", PossibleVk[userID]}, {"DT", ""} };
			
			SaveData();
		}
		
		#endregion
		
		#region API
		
		private string API_GetPlayerVk(ulong userID)
		{
			if (!UsersVk.ContainsKey(userID))
				return null;
				
			return (UsersVk[userID])["VK"];
		}
		
		#endregion
				
		#region Hooks

		private void Init()
		{
			LoadVariables();
			LoadOldData();									
			LoadData();
			
			AnswerVk.Clear();
			PossibleVk.Clear();
			
			if (string.IsNullOrEmpty(configData.RegMessage))
			{
				configData.RegMessage = "Чтобы завершить прикрепление своего профиля ВК к Steam аккаунту, зайдите на сервер {0} и введите в чате /regvk {1}";
				SaveConfig(configData);
			}
			
			foreach(var user in OldUsersVk)			
				if (!UsersVk.ContainsKey(user.Key))
					UsersVk.Add(user.Key, new Dictionary<string, string>() { {"VK", user.Value}, {"DT", ""} });
				
			SaveData();				
			permission.RegisterPermission(AllowPermission, this);
		}	
		
		#endregion
				
		#region Config        
		
        private ConfigData configData;
		
        private class ConfigData
        {
            public string Token;			
			public string VkGroupName;
			public string ServerName;
			public int    NextSendTime;
			public double NextRegTime;
			public string TestMessage;
			public string RegMessage;
			public string VkRaidMessage;
			public string SrvRaidMessage;
			public string MessageOff;			
			public bool   NoInfoIfFriends;
			public WItem  DefaultBlock;
			public string ColorBlock;
			public Dictionary<string, WItem> InfoBlocks;
        }
		
		private class WItem
		{
			public string pre;
			public string name;
			public WItem(string pre, string name)
			{
				this.pre = pre;
				this.name = name;
			}
		}
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                Token = "",
				VkGroupName = "Test",
				ServerName = "Test",
				NextSendTime = 15,
				NextRegTime = 1,
				NoInfoIfFriends = true,
				TestMessage = "Вы успешно подключили систему оповещений о рейде на сервере {0}",
				RegMessage = "Чтобы завершить прикрепление своего профиля ВК к Steam аккаунту, зайдите на сервер {0} и введите в чате /regvk {1}",
				VkRaidMessage = "Игрок {0} разрушил {2} на сервере {1}",
				SrvRaidMessage = "<color=#FA8072>Игрок <color=#aae9f2>{0}</color> разрушил {1}</color>",
				MessageOff = "Вы успешно отключили систему оповещений о рейде на сервере {0}",
				DefaultBlock = new WItem("ваш", "Строительный блок"),
				ColorBlock = "#aae9f2",
				InfoBlocks = new Dictionary<string, WItem>() 
				{
					{ "floor.grill", new WItem("ваш", "Решетчатый настил")},
					{ "floor.triangle.grill", new WItem("ваш", "Треугольный решетчатый настил")},
					{ "door.hinged.toptier", new WItem("вашу", "Бронированную дверь")},
					{ "door.double.hinged.toptier", new WItem("вашу", "Двойную бронированную дверь")},
					{ "gates.external.high.stone", new WItem("ваши", "Высокие внешние каменные ворота")},
					{ "wall.external.high.stone", new WItem("вашу", "Высокую внешнюю каменную стену")},
					{ "gates.external.high.wood", new WItem("ваши", "Высокие внешние деревянные ворота")},
					{ "wall.external.high", new WItem("вашу", "Высокую внешнюю деревянную стену")},
					{ "floor.ladder.hatch", new WItem("ваш", "Люк с лестницей")},
					{ "floor.triangle.ladder.hatch", new WItem("ваш", "Треугольный люк с лестницей")},
					{ "shutter.metal.embrasure.a", new WItem("вашу", "Металлическую горизонтальную бойницу")},
										
					{ "shutter.metal.embrasure.b", new WItem("вашу", "Металлическую вертикальную бойницу")},
					{ "wall.window.bars.metal", new WItem("ваши", "Металлические оконные решетки")},
					{ "wall.frame.cell.gate", new WItem("вашу", "Тюремную дверь")},
					{ "wall.frame.cell", new WItem("вашу", "Тюремную решетку")},
					{ "wall.window.bars.toptier", new WItem("ваши", "Укрепленные оконные решетки")},
					
					{ "wall.window.glass.reinforced", new WItem("ваше", "Укрепленное оконное стекло")},
					
					{ "door.hinged.metal", new WItem("вашу", "Металлическую дверь")},
					{ "door.double.hinged.metal", new WItem("вашу", "Двойную металлическую дверь")},					
					{ "door.hinged.wood", new WItem("вашу", "Деревянную дверь")},
					{ "door.double.hinged.wood", new WItem("вашу", "Двойную деревянную дверь")},
					{ "wall.frame.garagedoor", new WItem("вашу", "Гаражную дверь")},
					{ "wall.frame.shopfront.metal", new WItem("вашу", "Металлическую витрину магазина")},										
					
					{ "Wood,foundation.triangle", new WItem("ваш", "Деревянный треугольный фундамент")},
					{ "Stone,foundation.triangle", new WItem("ваш", "Каменный треугольный фундамент")},
					{ "Metal,foundation.triangle", new WItem("ваш", "Металлический треугольный фундамент")},
					{ "TopTier,foundation.triangle", new WItem("ваш", "Бронированный треугольный фундамент")},
					
                    { "Wood,foundation.steps", new WItem("ваши", "Деревянные ступеньки для фундамента")},
					{ "Stone,foundation.steps", new WItem("ваши", "Каменные ступеньки для фундамента")},
					{ "Metal,foundation.steps", new WItem("ваши", "Металлические ступеньки для фундамента")},
					{ "TopTier,foundation.steps", new WItem("ваши", "Бронированные ступеньки для фундамента")},
					
                    { "Wood,foundation", new WItem("ваш", "Деревянный фундамент")},
					{ "Stone,foundation", new WItem("ваш", "Каменный фундамент")},
					{ "Metal,foundation", new WItem("ваш", "Металлический фундамент")},
					{ "TopTier,foundation", new WItem("ваш", "Бронированный фундамент")},
					
                    { "Wood,wall.frame", new WItem("ваш", "Деревянный настенный каркас")},
					{ "Stone,wall.frame", new WItem("ваш", "Каменный настенный каркас")},
					{ "Metal,wall.frame", new WItem("ваш", "Металлический настенный каркас")},
					{ "TopTier,wall.frame", new WItem("ваш", "Бронированный настенный каркас")},
					
                    { "Wood,wall.window", new WItem("ваш", "Деревянный оконный проём")},
					{ "Stone,wall.window", new WItem("ваш", "Каменный оконный проём")},
					{ "Metal,wall.window", new WItem("ваш", "Металлический оконный проём")},
					{ "TopTier,wall.window", new WItem("ваш", "Бронированный оконный проём")},
					
                    { "Wood,wall.doorway", new WItem("ваш", "Деревянный дверной проём")},
					{ "Stone,wall.doorway", new WItem("ваш", "Каменный дверной проём")},
					{ "Metal,wall.doorway", new WItem("ваш", "Металлический дверной проём")},
					{ "TopTier,wall.doorway", new WItem("ваш", "Бронированный дверной проём")},
					
                    { "Wood,wall", new WItem("вашу", "Деревянную стену")},                    
					{ "Stone,wall", new WItem("вашу", "Каменную стену")},                    
					{ "Metal,wall", new WItem("вашу", "Металлическую стену")},                    
					{ "TopTier,wall", new WItem("вашу", "Бронированную стену")},                    
					
                    { "Wood,floor.frame", new WItem("ваш", "Деревянный потолочный каркас")},
					{ "Stone,floor.frame", new WItem("ваш", "Каменный потолочный каркас")},
					{ "Metal,floor.frame", new WItem("ваш", "Металлический потолочный каркас")},
					{ "TopTier,floor.frame", new WItem("ваш", "Бронированный потолочный каркас")},
					
					{ "Wood,floor.triangle.frame", new WItem("ваш", "Деревянный треугольный потолочный каркас")},
					{ "Stone,floor.triangle.frame", new WItem("ваш", "Каменный треугольный потолочный каркас")},
					{ "Metal,floor.triangle.frame", new WItem("ваш", "Металлический треугольный потолочный каркас")},
					{ "TopTier,floor.triangle.frame", new WItem("ваш", "Бронированный треугольный потолочный каркас")},
					
                    { "Wood,floor.triangle", new WItem("ваш", "Деревянный треугольный потолок")},
					{ "Stone,floor.triangle", new WItem("ваш", "Каменный треугольный потолок")},
					{ "Metal,floor.triangle", new WItem("ваш", "Металлический треугольный потолок")},
					{ "TopTier,floor.triangle", new WItem("ваш", "Бронированный треугольный потолок")},
					
                    { "Wood,floor", new WItem("ваш", "Деревянный потолок")},                    
					{ "Stone,floor", new WItem("ваш", "Каменный потолок")},                    
					{ "Metal,floor", new WItem("ваш", "Металлический потолок")},                    
					{ "TopTier,floor", new WItem("ваш", "Бронированный потолок")},                    
					
                    { "Wood,roof", new WItem("вашу", "Деревянную крышу")},
					{ "Stone,roof", new WItem("вашу", "Каменную крышу")},
					{ "Metal,roof", new WItem("вашу", "Металлическую крышу")},
					{ "TopTier,roof", new WItem("вашу", "Бронированную крышу")},
					
					{ "Wood,roof.triangle", new WItem("вашу", "Деревянную треугольную крышу")},
					{ "Stone,roof.triangle", new WItem("вашу", "Каменную треугольную крышу")},
					{ "Metal,roof.triangle", new WItem("вашу", "Металлическую треугольную крышу")},
					{ "TopTier,roof.triangle", new WItem("вашу", "Бронированную треугольную крышу")},
					
                    { "Wood,block.stair.lshape", new WItem("вашу", "Деревянную лестницу")},                    
					{ "Stone,block.stair.lshape", new WItem("вашу", "Каменную лестницу")},
					{ "Metal,block.stair.lshape", new WItem("вашу", "Металлическую лестницу")},
					{ "TopTier,block.stair.lshape", new WItem("вашу", "Бронированную лестницу")},
					
                    { "Wood,block.stair.ushape", new WItem("вашу", "Деревянную лестницу")},
					{ "Stone,block.stair.ushape", new WItem("вашу", "Каменную лестницу")},
					{ "Metal,block.stair.ushape", new WItem("вашу", "Металлическую лестницу")},
					{ "TopTier,block.stair.ushape", new WItem("вашу", "Бронированную лестницу")},
					
					{ "Wood,block.stair.spiral", new WItem("вашу", "Деревянную спиральную лестницу")},
					{ "Stone,block.stair.spiral", new WItem("вашу", "Каменную спиральную лестницу")},
					{ "Metal,block.stair.spiral", new WItem("вашу", "Металлическую спиральную лестницу")},
					{ "TopTier,block.stair.spiral", new WItem("вашу", "Бронированную спиральную лестницу")},
					
					{ "Wood,block.stair.spiral.triangle", new WItem("вашу", "Деревянную треугольную спиральную лестницу")},
					{ "Stone,block.stair.spiral.triangle", new WItem("вашу", "Каменную треугольную спиральную лестницу")},
					{ "Metal,block.stair.spiral.triangle", new WItem("вашу", "Металлическую треугольную спиральную лестницу")},
					{ "TopTier,block.stair.spiral.triangle", new WItem("вашу", "Бронированную треугольную спиральную лестницу")},
					
					{ "Wood,pillar", new WItem("вашу", "Деревянную опору")},
					{ "Stone,pillar", new WItem("вашу", "Каменную опору")},
					{ "Metal,pillar", new WItem("вашу", "Металлическую опору")},
					{ "TopTier,pillar", new WItem("вашу", "Бронированную опору")},
					
					{ "Wood,wall.low", new WItem("вашу", "Деревянную низкую стену")},
					{ "Stone,wall.low", new WItem("вашу", "Каменную низкую стену")},
					{ "Metal,wall.low", new WItem("вашу", "Металлическую низкую стену")},
					{ "TopTier,wall.low", new WItem("вашу", "Бронированную низкую стену")},
					
					{ "Wood,wall.half", new WItem("вашу", "Деревянную полустенку")},
					{ "Stone,wall.half", new WItem("вашу", "Каменную полустенку")},
					{ "Metal,wall.half", new WItem("вашу", "Металлическую полустенку")},
					{ "TopTier,wall.half", new WItem("вашу", "Бронированную полустенку")},
					
					{ "Wood,ramp", new WItem("ваш", "Деревянный скат")},
					{ "Stone,ramp", new WItem("ваш", "Каменный скат")},
					{ "Metal,ramp", new WItem("ваш", "Металлический скат")},
					{ "TopTier,ramp", new WItem("ваш", "Бронированный скат")}
				}								
            };
            SaveConfig(config);
			timer.Once(0.1f, ()=> SaveConfig(configData));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
		private void LoadOldData() => OldUsersVk = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, string>>("RaidAlerts");
		
		private void LoadData() => UsersVk = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string, string>>>("RaidAlertData");		

		private static void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("RaidAlertData", UsersVk);										
		
        #endregion				
		
    }	
	
}