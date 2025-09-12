using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("OnlineMoney", "Nimant", "1.0.7")]
    class OnlineMoney : RustPlugin
    {            		
		
		#region Variables
		
		[PluginReference]
        private Plugin RaidAlert;

		private const string URL_IsUserInGroup = "https://api.vk.com/method/groups.isMember?group_id={0}&user_id={1}&extended=1&access_token={2}&v=5.73";	
		private const string URL_OutMoney = "https://gamestores.ru/api?shop_id={0}&secret={1}&action=moneys&type=plus&steam_id={2}&amount={3}";
		
		private static System.Random Rnd = new System.Random();
		private static Dictionary<ulong, DateTime> PlannedCheckVk = new Dictionary<ulong, DateTime>();
		private static List<ulong> NotifyAfterSleep = new List<ulong>();
		private static Dictionary<ulong, ulong> SteamVk = new Dictionary<ulong, ulong>(); // steam, vk
		
		private Dictionary<string, string> InfoMessages = new Dictionary<string, string>()
		{
			{ "INFO.AFTER.SLEEP", "<color=#FA8072>Чтобы получать бонусные деньги за проведенное время на сервере на свой баланс в магазине, выполните следующее:</color>\n<color=yellow>1.</color> Подпишитесь на нашу группу в ВК: <color=yellow>vk.com/drakess_state</color>\n<color=yellow>2.</color> Прикрепите свой ВК к Steam аккаунту, выполнив <color=yellow>/regvk</color>\n<color=yellow>3.</color> Выполните команду <color=yellow>/money</color>, чтобы получить больше информации по выводу бонусных денег." },
			{ "INFO.NO.BALANCE.MONEY", "Недостаточно денег на балансе для вывода." },
			{ "INFO.SUCCESS.WITHDRAW", "Вы успешно вывели <color=yellow>{0} руб.</color>\nПроверьте свой баланс в магазине." },
			{ "INFO.ERROR.WITHDRAW", "Ошибка вывода, возможно магазин временно недоступен, попробуйте сделать вывод позже." },
			{ "INFO.ERROR.NOREG", "Ошибка вывода, возможно вы не зарегистрированы в магазине, если так - зайдите в магазин https://drakess.store и зарегистрируйтесь сперва." },
			{ "INFO.NEED.ATTACH", "У вас не прикреплён ВК к Steam аккаунту.\nВыполните команду /regvk \"ссылка на профиль\", чтобы прикрепить профиль ВК к вашему Steam аккаунту." },
			{ "INFO.NEED.SIGN", "Вы не подписаны на нашу группу ВК, или если подписаны, то плагин обновит об этом информацию в течение часа, подождите это время." },
			{ "INFO.BAD.PARAM", "Неверный аргумент. Используйте команду <color=yellow>/money</color> - для просмотра информации о накопленных вами денег или выполните с параметром <color=yellow>/money go</color> - для вывода накопленных денег в магазин." },
			{ "INFO.MIN.ONLINE", "когда наиграете минимум {0}, ваш текущий онлайн {1}" },
			
			//{ "GUI.INFO.MIN.ONLINE", "В следующий раз сможете вывести когда наиграете минимум {0}, ваш текущий онлайн {1}" },  
			{ "GUI.INFO.MIN.ONLINE", "Чтобы вывести деньги, вам необходимо наиграть на сервере минимум <color=yellow>{0}</color>. Осталось наиграть <color=yellow>{1}</color>." },
			
			{ "INFO.WAIT.COOLDOWN", "через {0}, при условии что это не новый вайп" },
			{ "GUI.INFO.WAIT.COOLDOWN", "В следующий раз сможете вывести через <color=yellow>{0}</color>, при условии что это не новый вайп." },
			{ "INFO.READY", "сейчас (команда /money go)" },
			{ "INFO.MONEY.TOTAL", " <color=yellow>***</color> ДЕНЬГИ ЗА ОНЛАЙН <color=yellow>***</color>\n <color=yellow>-</color> вы заработали за вайп {0} руб.\n <color=yellow>-</color> из них вывели {1} руб.\n <color=yellow>-</color> <color=#7CFC00>доступно для вывода</color> <color=yellow>{2} руб.</color>\n\n<i>Вывод можно сделать <color=yellow>{3}</color></i>." },
			{ "INFO.WAIT.COOLDOWN2", "Вы не можете сейчас вывести деньги, вывод можно будет сделать {0}." }
		};
		
		#endregion
		
		#region Hooks
		
		private void Init() 
		{
			PlannedCheckVk.Clear();
			NotifyAfterSleep.Clear();			
			LoadVariables();
			LoadData();
		}
		
		private void OnServerSave() => SaveData();
		
		private void OnNewSave()
		{
			foreach (var pair in PlayerData)
			{
				pair.Value.wipeMinutesOnline = 0;
				pair.Value.wipeMoneyEarned = 0;
				pair.Value.wipeMoneyGived = 0;
				pair.Value.lastGived = default(DateTime);
			}
			SaveData();
		}
		
		private void Unload() 
		{
			Log(null); // сброс застрявших сообщений
			SaveData();
			
			foreach (var player in BasePlayer.activePlayerList)
			{
				if (player == null) continue;
				CuiHelper.DestroyUi(player, MainPanel);				
			}
		}
		
		private void OnServerInitialized() 
		{
			foreach (var player in BasePlayer.activePlayerList.Where(x=> x != null && x.userID.IsSteamId()))
				OnPlayerConnected(player);
			
			timer.Once(60f, CheckInMinute);
		}
		
		private void OnPlayerConnected(BasePlayer player)
		{
			if (player == null) return;						
				
			var vkID = RaidAlert?.Call("API_GetPlayerVk", player.userID) as string;
			
			ulong vkIDu = 0;
			try { vkIDu = string.IsNullOrEmpty(vkID) ? 0 : (ulong)Convert.ToInt64(vkID); } catch {}
			
			if (vkIDu == 0 && !NotifyAfterSleep.Contains(player.userID))
				NotifyAfterSleep.Add(player.userID);
			
			if (vkIDu > 0)
			{
				if (!SteamVk.ContainsKey(player.userID))
					SteamVk.Add(player.userID, vkIDu);
				else
					SteamVk[player.userID] = vkIDu;
			}
		}
		
		private void OnPlayerSleepEnded(BasePlayer player)
        {
			if (player == null || !NotifyAfterSleep.Contains(player.userID)) return;
			
			timer.Once(1.5f, ()=>
			{
				if (player != null)
				{
					SendReply(player, InfoMessages["INFO.AFTER.SLEEP"]);
					NotifyAfterSleep.Remove(player.userID);
				}
			});
		}
		
		#endregion
		
		#region Main
		
		private void CheckInMinute()
		{
			foreach (var player in BasePlayer.activePlayerList.Where(x=> x != null && x.userID.IsSteamId() && SteamVk.ContainsKey(x.userID)))
				AddMoneyForOnline(player.userID);
			
			Log(null); // сброс застрявших сообщений			
			timer.Once(60f, CheckInMinute); 
		}
		
		private void AddMoneyForOnline(ulong userID)
		{
			if (!PlayerData.ContainsKey(userID))
				PlayerData.Add(userID, new PData());
			
			if (PlayerData[userID].hadVkSign)
			{
				PlayerData[userID].totalMinutesOnline++;
				PlayerData[userID].wipeMinutesOnline++;
				PlayerData[userID].totalMoneyEarned += GetCurrentMoneyRate()*(1f/60f);
				PlayerData[userID].wipeMoneyEarned += GetCurrentMoneyRate()*(1f/60f);
			}
			
			var dtNow = DateTime.Now;
			
			if (PlayerData[userID].lastCheckVk.AddMinutes(configData.CheckVkGroupMinutes) < dtNow)
			{
				if (!PlannedCheckVk.ContainsKey(userID))
					PlannedCheckVk.Add(userID, default(DateTime));
			
				if (PlannedCheckVk[userID] == default(DateTime))
					PlannedCheckVk[userID] = dtNow.AddSeconds(Rnd.Next(1, configData.CheckPeriodMinutes*60+1));
				else
					if (PlannedCheckVk[userID] < dtNow)
						DoCheckVkSign(userID);
			}
		}
		
		private void DoCheckVkSign(ulong userID)
		{
			if (!SteamVk.ContainsKey(userID)) return;
			
			webrequest.Enqueue(string.Format(URL_IsUserInGroup, configData.GroupID, SteamVk[userID], configData.TokenVK), null, (code, response) =>
            {
				if (!string.IsNullOrEmpty(response))
				{
					try
					{
						var response_ = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, new KeyValuesConverter());										
						
						if (response_.ContainsKey("response") && (response_["response"] as Dictionary<string, object>).Count() > 0)
						{						
							var json = response_["response"] as Dictionary<string, object>;											
							if (json.ContainsKey("member"))
							{							
								if (!PlayerData.ContainsKey(userID))
									PlayerData.Add(userID, new PData());
								
								PlayerData[userID].hadVkSign = json["member"].ToString() == "1";
								PlayerData[userID].lastCheckVk = DateTime.Now;
								
								if (!PlannedCheckVk.ContainsKey(userID))
									PlannedCheckVk.Add(userID, default(DateTime));
								else
									PlannedCheckVk[userID] = default(DateTime);
							}
						}
					}
					catch (Exception ex)
					{
						PrintWarning($"Ошибка парсинга данных с ВК: {ex.Message}");
					}
				}				
            }, this);
		}
		
		private void TryOutMoney(BasePlayer player, bool needCallGui = false)
		{
			if (player == null) return;			
			var userID = player.userID;
			var name = player.displayName;
			if (!PlayerData.ContainsKey(userID)) return;
			
			var money = Get2DgMoney(PlayerData[userID].wipeMoneyEarned-PlayerData[userID].wipeMoneyGived);
			if (money <= 0)
			{
				SendReply(player, InfoMessages["INFO.NO.BALANCE.MONEY"]);
				return;
			}
			
			webrequest.EnqueueGet(string.Format(URL_OutMoney, configData.ShopID, configData.SecretKey, userID, money.ToString().Replace(",", ".")), (code, response) =>
			{
				var response_ = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, new KeyValuesConverter());									
				
				if (response_.ContainsKey("result") && response_["result"].ToString() == "success")
				{
					PlayerData[userID].wipeMoneyGived += money;
					PlayerData[userID].totalMoneyGived += money;
					PlayerData[player.userID].lastGived = DateTime.Now;
					
					if (player != null)
						SendReply(player, string.Format(InfoMessages["INFO.SUCCESS.WITHDRAW"], money));
					
					var msg = $"Игрок {name} ({userID}) успешно вывел {money} руб. на баланс в магазин.";
					Puts(msg);
					Log(GetCurDate() + $"{msg}");
					
					if (needCallGui && player != null)
						CmdChatMoney2(player, null, null);
					
					return;
				}
				
				var noreg = false;
				if (response_.ContainsKey("result") && response_["result"].ToString() == "fail")
				{
					if (player != null)
						SendReply(player, InfoMessages["INFO.ERROR.NOREG"]);
					
					noreg = true;
				}
				else
				{
					if (player != null)
						SendReply(player, InfoMessages["INFO.ERROR.WITHDRAW"]);
				}
				
				var add = noreg ? " (не зарегистрирован в магазине)." : ".";
				
				var msg2 = $"Игрок {name} ({userID}) не смог вывести деньги на баланс в магазин{add}";
				PrintWarning(msg2);
				Log(GetCurDate() + $"{msg2}");
				
				if (needCallGui && player != null)
					CmdChatMoney2(player, null, null); 
				
			}, this);
		}
		
		#endregion
		
		#region Log
		
		private static List<string> QueueBuffer = new List<string>();
		
		private bool TryLog(string message)
		{
			if (string.IsNullOrEmpty(message))
				return true;
			
			try { LogToFile("withdraw", $"{message}", this); }
			catch { return false; }
			
			return true;
		}
		
		private void Log(string message)
		{						
			QueueBuffer.Add(message);	
			while(QueueBuffer.Count > 0)
			{					
				if (TryLog(QueueBuffer[0]))
					QueueBuffer.RemoveAt(0);
				else
					break;
			}				
		}
		
		#endregion
		
		#region Helpers
		
		private static float Get2DgMoney(float money) => (float)Math.Truncate(money * 100f)/100f;		
		
		private static string GetCurDate() => "["+DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")+"] ";		
		
		private static string TimeToString(long seconds2, bool isA)
		{
			var char_ = isA ? "а" : "у";			
			var minutes_ = new List<string>() {$"минут{char_}","минуты","минут"};
			var seconds_ = new List<string>() {$"секунд{char_}","секунды","секунд"};
			
			int days = (int)Math.Truncate(seconds2/60f/60f/24f);
            int hours = (int)Math.Truncate((seconds2-days*60f*60f*24f)/60f/60f);
            int minutes = (int)Math.Truncate((seconds2-days*60f*60f*24f-hours*60f*60f)/60f);
            int seconds = (int)Math.Truncate(seconds2-days*60f*60f*24f-hours*60f*60f-minutes*60f);
            
            string s = "";
			int cnt = 0;
			
            if (days > 0) { s += $"{GetStringCount(days, new List<string>() {"день","дня","дней"})} "; cnt+=1; }
            if (hours > 0) { s += (cnt == 1 ? "и " : "") + $@"{GetStringCount(hours, new List<string>() {"час","часа","часов"})} "; cnt+=2; }
			if (cnt == 1 || cnt == 3) return s.TrimEnd(' ');
			
            if (minutes > 0) { s += (cnt == 2 ? "и " : "") + $"{GetStringCount(minutes, minutes_)} "; cnt+=4; }			
			if (cnt == 2 || cnt == 6) return s.TrimEnd(' ');
			
            if (seconds > 0) s += (cnt == 4 ? "и " : "") + $"{GetStringCount(seconds, seconds_)} ";            						
			if (string.IsNullOrEmpty(s)) return "несколько секунд";
			
            return s.TrimEnd(' ');
		}
		
		private static string TimeToString(TimeSpan elapsedTime, bool isA)
        {
			var char_ = isA ? "а" : "у";			
			var minutes_ = new List<string>() {$"минут{char_}","минуты","минут"};
			var seconds_ = new List<string>() {$"секунд{char_}","секунды","секунд"};
			
            int hours = elapsedTime.Hours;
            int minutes = elapsedTime.Minutes;
            int seconds = elapsedTime.Seconds;
            int days = Mathf.FloorToInt((float)elapsedTime.TotalDays);
            string s = "";
			int cnt = 0;
			
            if (days > 0) { s += $"{GetStringCount(days, new List<string>() {"день","дня","дней"})} "; cnt+=1; }
            if (hours > 0) { s += (cnt == 1 ? "и " : "") + $@"{GetStringCount(hours, new List<string>() {"час","часа","часов"})} "; cnt+=2; }
			if (cnt == 1 || cnt == 3) return s.TrimEnd(' ');
			
            if (minutes > 0) { s += (cnt == 2 ? "и " : "") + $"{GetStringCount(minutes, minutes_)} "; cnt+=4; }			
			if (cnt == 2 || cnt == 6) return s.TrimEnd(' ');
			
            if (seconds > 0) s += (cnt == 4 ? "и " : "") + $"{GetStringCount(seconds, seconds_)} ";            						
			if (string.IsNullOrEmpty(s)) return "несколько секунд";
			
            return s.TrimEnd(' ');
        }				
		
		private static string GetStringCount(long count, List<string> words)
		{	
			switch(count)
			{
				case 11: 
				case 12: 
				case 13: 
				case 14: return $"{count} {words[2]}";
			}
			
			var countString = count.ToString();			
			switch(countString[countString.Length-1])
			{
				case '1': return $"{count} {words[0]}";
				case '2': 
				case '3': 
				case '4': return $"{count} {words[1]}";				
			}
			
			return $"{count} {words[2]}";
		}
		
		private static float GetCurrentMoneyRate()
		{
			var dayOfWipe = (int)Math.Ceiling(DateTime.UtcNow.Subtract(SaveRestore.SaveCreatedTime).TotalSeconds/60f/60f/24f);
			
			if (configData.BonusMoneyPerDay.ContainsKey(dayOfWipe))
				return configData.BonusMoneyPerDay[dayOfWipe];
			
			return configData.BonusMoneyPerDay.OrderByDescending(x=> x.Key).FirstOrDefault().Value;
		}
		
		#endregion
		
		#region Commands
		
		/*[ChatCommand("money")]
		private void CmdChatMoney(BasePlayer player, string command, string[] args)
        {
            if (player == null) return; 
			
			var vkID = RaidAlert?.Call("API_GetPlayerVk", player.userID) as string;
			
			ulong vkIDu = 0;
			try { vkIDu = string.IsNullOrEmpty(vkID) ? 0 : (ulong)Convert.ToInt64(vkID); } catch {}
			
			if (vkIDu > 0)
			{
				if (!SteamVk.ContainsKey(player.userID))
					SteamVk.Add(player.userID, vkIDu);
				else
					SteamVk[player.userID] = vkIDu;
			}
			
			if (!SteamVk.ContainsKey(player.userID))
			{
				SendReply(player, InfoMessages["INFO.NEED.ATTACH"]);
				return;
			}
			
			if (!PlayerData.ContainsKey(player.userID))
				PlayerData.Add(player.userID, new PData());
			
			if (!PlayerData[player.userID].hadVkSign)
			{
				SendReply(player, InfoMessages["INFO.NEED.SIGN"]);
				return;
			}
			
			bool isNeedMoney = false, isOk = false;
			
			if (args != null && args.Length > 0)
			{
				if (args[0].ToLower() != "go")
				{
					SendReply(player, InfoMessages["INFO.BAD.PARAM"]);
					return;
				}
				isNeedMoney = true;
			}
			
			var out_ = "";
			if (PlayerData[player.userID].wipeMinutesOnline < configData.BonusHours*60)
				out_ = string.Format(InfoMessages["INFO.MIN.ONLINE"], TimeToString(configData.BonusHours*60*60, false), TimeToString(PlayerData[player.userID].wipeMinutesOnline*60, true));
			else
				if (DateTime.Now < PlayerData[player.userID].lastGived.AddHours(configData.DelayHours))				
					out_ = string.Format(InfoMessages["INFO.WAIT.COOLDOWN"], TimeToString(PlayerData[player.userID].lastGived.AddHours(configData.DelayHours) - DateTime.Now, false));
				else
				{					
					out_ = InfoMessages["INFO.READY"];					
					isOk = true;
				}				
			
			if (!isNeedMoney)
			{
				var result = string.Format(InfoMessages["INFO.MONEY.TOTAL"], 
								Get2DgMoney(PlayerData[player.userID].wipeMoneyEarned),
								Get2DgMoney(PlayerData[player.userID].wipeMoneyGived),
								Get2DgMoney(PlayerData[player.userID].wipeMoneyEarned-PlayerData[player.userID].wipeMoneyGived),
								out_);
				SendReply(player, result);
			}
			else
			{
				if (!isOk)				
					SendReply(player, string.Format(InfoMessages["INFO.WAIT.COOLDOWN2"], out_));
				else
				{
					if ((PlayerData[player.userID].wipeMoneyEarned-PlayerData[player.userID].wipeMoneyGived) > 0)
						TryOutMoney(player);
					else
						SendReply(player, InfoMessages["INFO.NO.BALANCE.MONEY"]);
				}
			}
		}*/
		
		[ChatCommand("money")]
		private void CmdChatMoney2(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
			
			var vkID = RaidAlert?.Call("API_GetPlayerVk", player.userID) as string;
			
			ulong vkIDu = 0;
			try { vkIDu = string.IsNullOrEmpty(vkID) ? 0 : (ulong)Convert.ToInt64(vkID); } catch {}
			
			if (vkIDu > 0)
			{
				if (!SteamVk.ContainsKey(player.userID))
					SteamVk.Add(player.userID, vkIDu);
				else
					SteamVk[player.userID] = vkIDu;
			}
			
			if (!SteamVk.ContainsKey(player.userID))
			{
				DrawMenu(player, -999,-999,-999, InfoMessages["INFO.NEED.ATTACH"]);
				return;
			}
			
			if (!PlayerData.ContainsKey(player.userID)) 
				PlayerData.Add(player.userID, new PData());
			
			if (!PlayerData[player.userID].hadVkSign)
			{
				DrawMenu(player, -999,-999,-999, InfoMessages["INFO.NEED.SIGN"]);
				return; 
			}
			
			var out_ = "";  
			if (PlayerData[player.userID].wipeMinutesOnline < configData.BonusHours*60)
				out_ = string.Format(InfoMessages["GUI.INFO.MIN.ONLINE"], TimeToString(configData.BonusHours*60*60, false), TimeToString(configData.BonusHours*60*60-PlayerData[player.userID].wipeMinutesOnline*60, true));
			else
				if (DateTime.Now < PlayerData[player.userID].lastGived.AddHours(configData.DelayHours))				
					out_ = string.Format(InfoMessages["GUI.INFO.WAIT.COOLDOWN"], TimeToString(PlayerData[player.userID].lastGived.AddHours(configData.DelayHours) - DateTime.Now, false));
								
			DrawMenu(player, Get2DgMoney(PlayerData[player.userID].wipeMoneyEarned),
							 Get2DgMoney(PlayerData[player.userID].wipeMoneyGived),
							 Get2DgMoney(PlayerData[player.userID].wipeMoneyEarned-PlayerData[player.userID].wipeMoneyGived), 
							 out_);
		}
		
		#endregion
		
		#region GUI
		
		private void DrawMenu(BasePlayer player, float earned, float gived, float toOut, string err)
		{
			CuiHelper.DestroyUi(player, MainPanel);
			
			var container = new CuiElementContainer();
			UI_MainPanel(ref container, "0.3 0.3 0.3 0.9", "0.25 0.33", "0.75 0.73");
			
			UI_Panel(ref container, "0.2 0.1 0.1 0.75", "0 0.9", "1 1");									
			UI_Label(ref container, "<color=yellow>***</color> <b>ДЕНЬГИ ЗА ОНЛАЙН</b> <color=yellow>***</color>", 16, "0 0.9", "1 1", 0,0, TextAnchor.MiddleCenter, "robotocondensed-regular.ttf");
			UI_Button(ref container, "1 1 1 0.95", "<color=#8B0000>X</color>", 20, "0.945 0.9", "1 1", "om_3567590.close", "robotocondensed-bold.ttf"); 
			
			UI_Panel(ref container, "0.2 0.1 0.1 0.75", "0.02 0.34", "0.98 0.86", MainPanel, "P1");			
			UI_Panel(ref container, "0.2 0.1 0.1 0.75", "0.02 0.04", "0.98 0.30", MainPanel, "P2");
			
			UI_Label(ref container, $" <color=yellow>*</color> всего вы заработали за вайп <color=yellow>{earned < 0 ? 0 : earned}</color> рублей", 18, "0.02 0.7", "1 0.9", 0,0, TextAnchor.MiddleLeft, "robotocondensed-regular.ttf", "P1", "P1_earned");
			UI_Label(ref container, $" <color=yellow>*</color> из них вы уже вывели <color=yellow>{gived < 0 ? 0 : gived}</color> рублей", 18, "0.02 0.3", "1 0.7", 0,0, TextAnchor.MiddleLeft, "robotocondensed-regular.ttf", "P1", "P1_gived");
			UI_Label(ref container, $" <color=yellow>*</color> доступно для вывода <color=yellow>{toOut < 0 ? 0 : toOut}</color> рублей", 18, "0.02 0.1", "1 0.3", 0,0, TextAnchor.MiddleLeft, "robotocondensed-regular.ttf", "P1", "P1_toout");
			
			if (!string.IsNullOrEmpty(err))
			{
				UI_Label(ref container, $"<color=red>{err}</color>", 16, "0.02 0.05", "0.98 0.95", 0,0, TextAnchor.MiddleLeft, "robotocondensed-regular.ttf", "P2", "P2_err");
			}
			else
			{
				UI_Label(ref container, "<color=yellow>Вы можете вывести деньги сейчас</color>", 18, "0.02 0.05", "0.7 0.95", 0,0, TextAnchor.MiddleCenter, "robotocondensed-regular.ttf", "P2", "P2_err");
				UI_Button(ref container, "1 1 1 0.95", "<color=#8B0000>ВЫВЕСТИ</color>", 20, "0.72 0.26", "0.93 0.74", "om_3567590.out", "robotocondensed-bold.ttf", TextAnchor.MiddleCenter, "P2"); 
			}
						
            CuiHelper.AddUi(player, container);
		}
		
		#endregion
		
		#region GUI Commands
		
		[ConsoleCommand("om_3567590.close")]
        private void ccmdTMClose(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null) return;
			
			CuiHelper.DestroyUi(player, MainPanel);
		}
		
		[ConsoleCommand("om_3567590.out")]
        private void ccmdTMOut(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null) return;
					
			var container = new CuiElementContainer();	 	
			UI_Panel(ref container, "0 0 0 0", "0.02 0.04", "0.98 0.30", MainPanel, "P2.1");
			CuiHelper.AddUi(player, container);
					
			TryOutMoney(player, true);												
		}
		
		#endregion
		
		#region GUI Helpers
		
		private const string MainPanel = "OM_MainPanel";				
		
		private static void UI_MainPanel(ref CuiElementContainer container, string color, string aMin, string aMax, bool isHud = true, bool isNeedCursor = true, bool isBlur = false, string panel = MainPanel)
		{					
			container.Add(new CuiPanel
			{
				Image = { Color = color, Material = isBlur ? "assets/content/ui/uibackgroundblur.mat" : "Assets/Icons/IconMaterial.mat" },
				RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
				CursorEnabled = isNeedCursor
			}, isHud ? "Hud" : "Overlay", panel);
		}
		
		private static void UI_Panel(ref CuiElementContainer container, string color, string aMin, string aMax, string panel = MainPanel, string name = null, string oMin = "0.0 0.0", string oMax = "0.0 0.0", float fadeIn = 0f, float fadeOut = 0f)
		{			
			container.Add(new CuiPanel
			{
				FadeOut = fadeOut,
				Image = { Color = color, FadeIn = fadeIn },
				RectTransform = { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax }
			}, panel, name);
		}
		
		private static void UI_Label(ref CuiElementContainer container, string text, int size, string aMin, string aMax, float fadeIn = 0f, float fadeOut = 0f, TextAnchor align = TextAnchor.MiddleCenter, string font = "robotocondensed-regular.ttf", string panel = MainPanel, string name = null)
		{						
			container.Add(new CuiElement
			{
				Name = !string.IsNullOrEmpty(name) ? name : CuiHelper.GetGuid(),
				Parent = panel,
				FadeOut = fadeOut,
				Components =
				{
					new CuiTextComponent { FontSize = size, Align = align, Text = text, Font = font, FadeIn = fadeIn },
					new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax }					
				}
			});
		}
		
		private static void UI_FLabel(ref CuiElementContainer container, string text, string fcolor, int size, string aMin, string aMax, float fadeIn = 0f, float fadeOut = 0f, TextAnchor align = TextAnchor.MiddleCenter, string font = "robotocondensed-regular.ttf", string panel = MainPanel, string name = null)
		{						
			if (string.IsNullOrEmpty(fcolor))
				fcolor = "0.0 0.0 0.0 1.0";
			
			container.Add(new CuiElement
			{
				Name = !string.IsNullOrEmpty(name) ? name : CuiHelper.GetGuid(),
				Parent = panel,
				FadeOut = fadeOut,
				Components =
				{
					new CuiTextComponent { FontSize = size, Align = align, Text = text, Font = font, FadeIn = fadeIn },
					new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax },
					new CuiOutlineComponent { Distance = "1 1", Color = fcolor }
				}
			});
		}
				
		private static void UI_Button(ref CuiElementContainer container, string color, string text, int size, string aMin, string aMax, string command, string font = "robotocondensed-regular.ttf", TextAnchor align = TextAnchor.MiddleCenter, string panel = MainPanel, string name = null, float fadeIn = 0f, float fadeOut = 0f)
		{
			if (string.IsNullOrEmpty(color)) color = "0 0 0 0";
			
			container.Add(new CuiButton
			{
				FadeOut = fadeOut,
				Button = { Color = color, Command = command, FadeIn = fadeIn },
				RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
				Text = { Text = text, FontSize = size, Align = align, Font = font }
			}, panel, name);
		}
		
		private static void UI_Image(ref CuiElementContainer container, string png, string aMin, string aMax, float fadeIn = 0f, float fadeOut = 0f, string panel = MainPanel, string name = null, string oMin = null, string oMax = null)
		{
			container.Add(new CuiElement
			{
				Name = !string.IsNullOrEmpty(name) ? name : CuiHelper.GetGuid(),
				Parent = panel,
				FadeOut = fadeOut,
				Components =
				{
					(png.Contains("https://") || png.Contains("http://")) ? new CuiRawImageComponent { Url = png, FadeIn = fadeIn } : new CuiRawImageComponent { Png = png, FadeIn = fadeIn },
					string.IsNullOrEmpty(oMin) ? new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax } : new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax }
				}
			});
		}
		
		#endregion
		
		#region API
		
		private bool API_CanGiveMoney(ulong userID) => SteamVk.ContainsKey(userID);
						
		private void API_AddMoney(ulong userID, float amount)
		{
			if (!PlayerData.ContainsKey(userID))
				PlayerData.Add(userID, new PData());
						
			PlayerData[userID].totalMoneyEarned += amount;
			PlayerData[userID].wipeMoneyEarned += amount;
		}		
		
		#endregion
		
		#region Config
		
        private static ConfigData configData;
		
        private class ConfigData
        {            
			[JsonProperty(PropertyName = "Секретный ключ магазина")]
			public string SecretKey;
			[JsonProperty(PropertyName = "ИД магазина")]
			public string ShopID;
			[JsonProperty(PropertyName = "Токен группы ВК")]
			public string TokenVK;
			[JsonProperty(PropertyName = "ИД группы ВК")]
			public string GroupID;
			[JsonProperty(PropertyName = "Сколько часов нужно наиграть для возможности вывода денег")]
			public int BonusHours;
			[JsonProperty(PropertyName = "Задержка в часах на повторный вывод денег")]
			public int DelayHours;
			[JsonProperty(PropertyName = "Частота опроса подписки на группу в минутах")]
			public int CheckVkGroupMinutes;
			[JsonProperty(PropertyName = "Период за который нужно проверить игроков в минутах")]
			public int CheckPeriodMinutes;
			[JsonProperty(PropertyName = "Сколько насчитывать рублей в час в разрезе дней после вайпа")]
			public Dictionary<int, float> BonusMoneyPerDay;
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                SecretKey = "fd7c8b83e25b71b9df28fe56c38ebfaf",
				ShopID = "5298",
				TokenVK = "7105ca6957f89d0560adad066c5fa7949aff8c06793d1a9fac0c6ef43c546ff2a241bcdfd346e5f8b2ab9",
				GroupID = "122782627",
				BonusHours = 24,
				DelayHours = 24,
				CheckVkGroupMinutes = 720,
				CheckPeriodMinutes = 60,
				BonusMoneyPerDay = new Dictionary<int, float>()
				{
					{ 1, 2f },
					{ 2, 3f },
					{ 3, 4f }
				}
            };
            SaveConfig(configData);
			timer.Once(0.1f, ()=> SaveConfig(configData));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
		
		#region Data
		
		private static Dictionary<ulong, PData> PlayerData = new Dictionary<ulong, PData>();
		
		private class PData
		{
			[JsonProperty(PropertyName = "tMO")]
			public int totalMinutesOnline;
			[JsonProperty(PropertyName = "tME")]
			public float totalMoneyEarned;
			[JsonProperty(PropertyName = "tMG")]
			public float totalMoneyGived;
			[JsonProperty(PropertyName = "wMO")]
			public int wipeMinutesOnline;
			[JsonProperty(PropertyName = "wME")]			
			public float wipeMoneyEarned;
			[JsonProperty(PropertyName = "wMG")]
			public float wipeMoneyGived;
			[JsonProperty(PropertyName = "lG")]
			public DateTime lastGived;
			[JsonProperty(PropertyName = "lCV")]
			public DateTime lastCheckVk;
			[JsonProperty(PropertyName = "hVS")]
			public bool hadVkSign;
		}
		
		private void LoadData() => PlayerData = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, PData>>("OnlineMoneyData");					
		
		private static void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("OnlineMoneyData", PlayerData);		
		
		#endregion
		
    }	
	
}