using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;  
using UnityEngine;  
using System.Linq;
  
namespace Oxide.Plugins 
{ 
    [Info("XRestartUI", "Monster.", "1.0.2")]
    class XRestartUI : RustPlugin
    {	 
		private Coroutine _coroutine; 
		 
		#region Configuration  
  
        private RestartConfig config; 
 
        private class RestartConfig 
        {		  
		    internal class GeneralSetting
			{
			    [JsonProperty("Использовать сообщения в чате")] public bool Message;
			    [JsonProperty("Использовать эффект тика")] public bool EffectTickUse;
			    [JsonProperty("Использовать эффект предупреждения")] public bool EffectWarningUse;
			    [JsonProperty("Используемый эффект тика")] public string EffectTick;
			    [JsonProperty("Используемый эффект предупреждения")] public string EffectWarning;
				[JsonProperty("SteamID профиля для кастомной аватарки")] public ulong SteamID;
			}	 				 			
			
			[JsonProperty("Общие настройки")] 
            public GeneralSetting Setting;	
			[JsonProperty("Список уникальных имен(ключей) причин рестарта - [ Настройка текста в lang ]")] 
			public List<string> ListMessage;			
			[JsonProperty("Настройка рестартов по расписанию [ Можно запланировать любую команду в любое время ]")] 
			public Dictionary<string, string> ARestart;			
			[JsonProperty("Настройка предупреждений за N минут до рестарта")] 
			public List<int> Warning;			
			
			public static RestartConfig GetNewConfiguration()
            {
                return new RestartConfig  
                {
					Setting = new GeneralSetting      
					{ 
						Message = true,
						EffectTickUse = true,
						EffectWarningUse = true,
						EffectTick = "assets/bundled/prefabs/fx/notice/loot.drag.dropsuccess.fx.prefab",
						EffectWarning = "assets/bundled/prefabs/fx/item_unlock.prefab",
						SteamID = 0
					},    
					ListMessage = new List<string>
					{
						"M_DEFAULT", "M_1", "M_2"
					},
					ARestart = new Dictionary<string, string>
					{
						["08:00"] = "restart 300",
						["21:00"] = "restart 300 M_1"
					},
					Warning = new List<int>
					{
						60,
						45,
						30,
						15,
						10,
						5
					}
				};
			}
        } 
  
        protected override void LoadConfig()
        {
            base.LoadConfig();
			
			try
			{
				config = Config.ReadObject<RestartConfig>(); 
			}
			catch
			{
				PrintWarning("Ошибка чтения конфигурации! Создание дефолтной конфигурации!");
				LoadDefaultConfig();
			}
			
			SaveConfig();
        }
		protected override void LoadDefaultConfig() => config = RestartConfig.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion
		
		#region Hooks 
		 
		private void OnServerInitialized()
		{ 
			PrintWarning("\n-----------------------------\n" + 
			"     Author - Monster\n" +
			"     VK - vk.com/idannopol\n" + 
			"     Discord - Monster#4837\n" +
			"     Config - v.528\n" + 
			"-----------------------------"); 
			
			InitializeLang();
			timer.Every(60, () => AutoRestart());
		}
		  
		private void Unload()
		{
			if(_coroutine != null) 
				ServerMgr.Instance.StopCoroutine(_coroutine);
			
			BasePlayer.activePlayerList.ToList().ForEach(x => CuiHelper.DestroyUi(x, ".TimerGUI"));
		} 

		private void AutoRestart() 
		{
			string time = DateTime.Now.ToString("t");
			
			foreach(int minute in config.Warning)
			{
				string newtime = DateTime.Now.AddMinutes(minute).ToString("t");
				
				if(config.ARestart.ContainsKey(newtime) && config.ARestart[newtime].Contains("restart"))
				{
					TimeSpan t = TimeSpan.FromSeconds(minute * 60);
					
					if(config.Setting.Message)
						BasePlayer.activePlayerList.ToList().ForEach(x => Player.Reply(x, string.Format(lang.GetMessage("CHAT_WARNING_RESTART", this, x.UserIDString), t.Hours, t.Minutes, t.Seconds), config.Setting.SteamID));
					
					BasePlayer.activePlayerList.ToList().ForEach(x => WarningGUI(x, t));
					break;
				}
			}
			
			if(config.ARestart.ContainsKey(time))
				Server.Command(config.ARestart[time]);
		}		
	    
		[ConsoleCommand("server.restart")]
		void checkOxideCommits(ConsoleSystem.Arg arg)
		{
			if(arg.Connection != null && arg.Connection.authLevel < 2) return;
			if(_coroutine != null) 
				ServerMgr.Instance.StopCoroutine(_coroutine); 
			
			if(Convert.ToInt32(arg.Args[1]) > 0)
			{
				arg.Args[0] = String.IsNullOrEmpty(arg.Args[0]) ? "M_DEFAULT" : arg.Args[0];
				_coroutine = ServerMgr.Instance.StartCoroutine(Restart(arg.Args[0], Convert.ToInt32(arg.Args[1]))); 
			}
			else if(_coroutine != null) 
			{
				if(config.Setting.Message)
					BasePlayer.activePlayerList.ToList().ForEach(x => Player.Reply(x, lang.GetMessage("CANCELED_RESTART", this, x.UserIDString), config.Setting.SteamID));
				Unload(); 
				
				_coroutine = null;
			}
		}
		
		private object OnServerRestart(string message, int seconds)
		{ 
			if(_coroutine != null) 
				ServerMgr.Instance.StopCoroutine(_coroutine); 
			
			if(seconds > 0)
			{
				message = String.IsNullOrEmpty(message) ? "M_DEFAULT" : message;
			    _coroutine = ServerMgr.Instance.StartCoroutine(Restart(message, seconds)); 
			}
			else if(_coroutine != null) 
			{
				if(config.Setting.Message)
					BasePlayer.activePlayerList.ToList().ForEach(x => Player.Reply(x, lang.GetMessage("CANCELED_RESTART", this, x.UserIDString), config.Setting.SteamID));
				Unload(); 
				
				_coroutine = null;
			} 
			
			return true;
		} 
		
		private IEnumerator Restart(string message, int seconds) 
		{
			if(config.Setting.Message)
			{
				TimeSpan t = TimeSpan.FromSeconds(seconds);
				BasePlayer.activePlayerList.ToList().ForEach(x => Player.Reply(x, string.Format(lang.GetMessage("CHAT_RESTART", this, x.UserIDString), t.Minutes, t.Seconds, lang.GetMessage(message, this, x.UserIDString)), config.Setting.SteamID));
			}
			
			for(int i = 0; i <= seconds; i++)
			{
				BasePlayer.activePlayerList.ToList().ForEach(x => TimerGUI(x, message, seconds - i));
				
				yield return CoroutineEx.waitForSeconds(1);
			}
			
			yield return CoroutineEx.waitForSeconds(1);
			
			BasePlayer.activePlayerList.ToList().ForEach(x => x.Kick("Server Restarting"));
			
			yield return CoroutineEx.waitForSeconds(2);
			
			ConsoleSystem.Run(ConsoleSystem.Option.Server, "quit", Array.Empty<object>());
			
			yield break;
		}
 
		#endregion
		
		#region GUI
		 
		private void TimerGUI(BasePlayer player, string message, int seconds)
		{
			TimeSpan t = TimeSpan.FromSeconds(seconds);
			
			CuiHelper.DestroyUi(player, ".TimerGUI");
            CuiElementContainer container = new CuiElementContainer();
			 
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.85", AnchorMax = "1 0.85", OffsetMin = "0 -25", OffsetMax = "0 25" },
                Image = { Color = "0.217 0.221 0.209 0.4", Material = "assets/icons/greyout.mat" }
            }, "Hud", ".TimerGUI");
			
			container.Add(new CuiElement 
			{ 
				Parent = ".TimerGUI", 
				Components =
				{
					new CuiTextComponent { Text = string.Format(lang.GetMessage("RESTART", this, player.UserIDString), t.Minutes, t.Seconds, lang.GetMessage(message, this, player.UserIDString)), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 18, Color = "1 1 1 0.75" },
				    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
					new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.2 -0.2" }
				} 
			});	
			 
			CuiHelper.AddUi(player, container);
			
			if(config.Setting.EffectTickUse)
				EffectNetwork.Send(new Effect(config.Setting.EffectTick, player, 0, new Vector3(), new Vector3()), player.Connection);
		}		
		
		private void WarningGUI(BasePlayer player, TimeSpan time)
		{
			CuiHelper.DestroyUi(player, ".TimerGUI");
            CuiElementContainer container = new CuiElementContainer();
			  
			container.Add(new CuiPanel
            {
				FadeOut = 2.5f,
                RectTransform = { AnchorMin = "0 0.85", AnchorMax = "1 0.85", OffsetMin = "0 -25", OffsetMax = "0 25" },
                Image = { Color = "0.217 0.221 0.209 0.4", Material = "assets/icons/greyout.mat", FadeIn = 2.5f }
            }, "Hud", ".TimerGUI");
			
			container.Add(new CuiElement 
			{ 
				Parent = ".TimerGUI", 
				Name = ".TimerGUIText", 
				FadeOut = 2.5f,
				Components =
				{
					new CuiTextComponent { Text = string.Format(lang.GetMessage("WARNING_RESTART", this, player.UserIDString), time.Hours, time.Minutes, time.Seconds), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 20, Color = "1 1 1 0.75", FadeIn = 2.5f },
				    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
					new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.2 -0.2" }
				} 
			});	
			 
			CuiHelper.AddUi(player, container);
			 
			if(config.Setting.EffectWarningUse)
				EffectNetwork.Send(new Effect(config.Setting.EffectWarning, player, 0, new Vector3(), new Vector3()), player.Connection);
			player.Invoke(() => { CuiHelper.DestroyUi(player, ".TimerGUI"); CuiHelper.DestroyUi(player, ".TimerGUIText"); }, 15.0f);
		} 
		 
		#endregion
		
	    #region Lang
  
        private void InitializeLang() 
        {	
			Dictionary<string, string> langen = new Dictionary<string, string>
			{
				["RESTART"] = "SERVER RESTART THROUGH: {0} MIN. {1} SEC.\n<size=12>{2}</size>",
				["CHAT_RESTART"] = "<color=#a3f0ff>SERVER RESTART THROUGH</color>: <color=orange>{0} MIN. {1} SEC.</color>\n<size=10>{2}</size>",
				["WARNING_RESTART"] = "SERVER RESTART WILL START IN: {0} HR. {1} MIN. {2} SEC.",
				["CHAT_WARNING_RESTART"] = "<size=13><color=#a3f0ff>SERVER RESTART WILL START IN</color>: <color=orange>{0} HR. {1} MIN. {2} SEC.</color></size>",
				["CANCELED_RESTART"] = "<color=#a3f0ff>RESTART CANCELED</color>"
			};		    
			 
			Dictionary<string, string> langru = new Dictionary<string, string>
			{
                ["RESTART"] = "РЕСТАРТ СЕРВЕРА ЧЕРЕЗ: {0} МИН. {1} СЕК.\n<size=12>{2}</size>",
                ["CHAT_RESTART"] = "<color=#a3f0ff>РЕСТАРТ СЕРВЕРА ЧЕРЕЗ</color>: <color=orange>{0} МИН. {1} СЕК.</color>\n<size=10>{2}</size>",				
                ["WARNING_RESTART"] = "РЕСТАРТ СЕРВЕРА НАЧНЕТСЯ ЧЕРЕЗ: {0} ЧАС. {1} МИН. {2} СЕК.",									
                ["CHAT_WARNING_RESTART"] = "<size=13><color=#a3f0ff>РЕСТАРТ СЕРВЕРА НАЧНЕТСЯ ЧЕРЕЗ</color>: <color=orange>{0} ЧАС. {1} МИН. {2} СЕК.</color></size>",									
				["CANCELED_RESTART"] = "<color=#a3f0ff>РЕСТАРТ ОТМЕНЕН</color>"				
			};				
			  
			Dictionary<string, string> langes = new Dictionary<string, string>
			{
                ["RESTART"] = "REINICIAR EL SERVIDOR A TRAVÉS: {0} MIN. {1} SEG.\n<size=12>{2}</size>",	
                ["CHAT_RESTART"] = "<color=#a3f0ff>REINICIAR EL SERVIDOR A TRAVÉS</color>: <color=orange>{0} MIN. {1} SEG.</color>\n<size=10>{2}</size>",				
                ["WARNING_RESTART"] = "EL REINICIO DEL SERVIDOR COMENZARÁ EN: {0} HR. {1} MIN. {2} SEG.",								
                ["CHAT_WARNING_RESTART"] = "<size=13><color=#a3f0ff>EL REINICIO DEL SERVIDOR COMENZARÁ EN</color>: <color=orange>{0} HR. {1} MIN. {2} SEG.</color></size>",								
				["CANCELED_RESTART"] = "<color=#a3f0ff>REINICIO CANCELADO</color>"
			};			
			
			foreach(var message in config.ListMessage)
			{
			   	langen.Add(message, "RESTART RESTART RESTART");
				langru.Add(message, "РЕСТАРТ РЕСТАРТ РЕСТАРТ");
				langes.Add(message, "REINICIAR REINICIAR REINICIAR");
			}
			
			lang.RegisterMessages(langen, this);
			lang.RegisterMessages(langru, this, "ru");
			lang.RegisterMessages(langes, this, "es-ES");
        }

        #endregion
	}
}