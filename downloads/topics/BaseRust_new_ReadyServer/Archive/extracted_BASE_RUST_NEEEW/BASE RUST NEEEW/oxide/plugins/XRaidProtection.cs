using Oxide.Core.Plugins;
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using System;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("XRaidProtection", "Monster", "1.1.1")]
    class XRaidProtection : RustPlugin
	{	
		
				
				
		private Dictionary<BasePlayer, DateTime> Cooldowns = new Dictionary<BasePlayer, DateTime>();

        		
				
		private void IQChatPuts(BasePlayer player, string Message) => IQChat?.Call("API_ALERT_PLAYER", player, Message);
		protected override void LoadDefaultConfig() => config = RaidConfig.GetNewConfiguration();
		
		private void Protection(BaseCombatEntity entity, HitInfo info, float damage)
		{
			if (_time)
			{
				BasePlayer player = info.InitiatorPlayer;
				bool ent = entity is BuildingBlock;
				
				if(config.Setting.Twigs && ent)
					if((entity as BuildingBlock).grade == BuildingGrade.Enum.Twigs)
						return;
				
		        if (ent || entity is Door || entity is SimpleBuildingBlock || config.Prefab.Prefabs.Contains(entity.ShortPrefabName))
			    {
					var dm = info.damageTypes.Total();
					
					info.damageTypes.ScaleAll(1.0f - damage);
					
					if(dm >= 1.5)
					{
						if (Cooldowns.ContainsKey(player))
							if (Cooldowns[player].Subtract(DateTime.Now).TotalSeconds >= 0) return;
						
						string s_damage = $"{damage * 100}";
						
						if (config.Setting.TEffect) EffectNetwork.Send(new Effect(config.Setting.Effect, player, 0, new Vector3(), new Vector3()), player.Connection);
						if (config.Setting.TGUIMessage) Message(player, s_damage);
						if (config.Setting.TMessage)
						{
							string message = config.Setting.PDay ? string.Format(lang.GetMessage("WIPE_CHAT_MESSAGE", this, player.UserIDString), config.Setting.PDay, s_damage) : s_damage == "100" ? string.Format(lang.GetMessage("FULL_CHAT_MESSAGE", this, player.UserIDString), s_damage, Off.ToString(@"hh\:mm"), On.ToString(@"hh\:mm")) : string.Format(lang.GetMessage("CHAT_MESSAGE", this, player.UserIDString), s_damage, On.ToString(@"hh\:mm"), Off.ToString(@"hh\:mm"));
							
							if (IQChat)
								IQChatPuts(player, message);
							else
								SendReply(player, message);
						}
					
						if (config.Message.TimeMessage < 10)
							Cooldowns[player] = DateTime.Now.AddSeconds(10);
						else
							Cooldowns[player] = DateTime.Now.AddSeconds(config.Message.TimeMessage);
					}
			    }
			}
		}
        protected override void SaveConfig() => Config.WriteObject(config);
		
        		
	    
        private RaidConfig config;
		   		 		  						  	   		  	 	 		  		  		  		 			  			 
        private class RaidConfig
        {													
			
			internal class GUISettings
			{
				[JsonProperty("AnchorMin")] public string AnchorMin;				
				[JsonProperty("AnchorMax")] public string AnchorMax;				
				[JsonProperty("OffsetMin")] public string OffsetMin;				
				[JsonProperty("OffsetMax")] public string OffsetMax;				
				[JsonProperty("Цвет текста")] public string ColorText;				
				[JsonProperty("Размер текста")] public int SizeText;				
				[JsonProperty("Использовать иконки")] public bool Icon;
			}			
			
			internal class PermisssionSetting
			{
				[JsonProperty("Процент защиты по пермишену. 1.0 - 100%")] public float PermisssionDamage;
			}
			[JsonProperty("Время")]			
			public DateTime MSCTime;

			public static RaidConfig GetNewConfiguration()
            {
                return new RaidConfig
                {
					Message = new MessageSetting
					{
						TimeMessage = 30
					},
					Time = new TimeSetting
					{
						HourStart = 22,
						MinuteStart = 0,
						HourEnd = 7,
						MinuteEnd = 0,
						GMT = 3,
						Timer = 120
					},
					Setting = new Settings
					{
						Effect = "assets/bundled/prefabs/fx/invite_notice.prefab",
						Damage = 0.5f,
						TEffect = true,
						PDays = 3,
						PDay = false,
						TGUIMessage = true,
						TMessage = false,
						Twigs = true,
						TypeProtection = 1
						
					},					
					GUI = new GUISettings
					{
						AnchorMin = "0.5 0",
						AnchorMax = "0.5 0",
						OffsetMin = "-194.5 80",
						OffsetMax = "175.5 110",
						ColorText = "1 1 1 0.4",
						SizeText = 12,
						Icon = true
					},
					Prefab = new PrefabSetting
					{
					    Prefabs = new List<string>
					    {
						    "cupboard.tool.deployed",
							"wall.frame.shopfront.metal"
					    }
					},
					Permisssion = new Dictionary<string, PermisssionSetting>
					{
						["xraidprotection.vip"] = new PermisssionSetting
						{
							PermisssionDamage = 0.70f
						}
					}
				}; 
			}			 
			[JsonProperty("Общее")]
            public Settings Setting = new Settings();			
			[JsonProperty("Дата вайпа")]
            public string DateWipe;
			[JsonProperty("Настройка GUI")]
            public GUISettings GUI = new GUISettings();			
            [JsonProperty("Настройка пермишенов")]			
			public Dictionary<string, PermisssionSetting> Permisssion = new Dictionary<string, PermisssionSetting>();
			
			internal class Settings
			{
				[JsonProperty("Звуковой эффект")] public string Effect;				
				[JsonProperty("Процент защиты для всех игроков. 1.0 - 100%")] public float Damage;				
				[JsonProperty("Включить звуковой эффект")] public bool TEffect;				
				[JsonProperty("Первые N дни активности защиты после вайпа")] public int PDays;				
				[JsonProperty("Защита только в первые N дней после вайпа")] public bool PDay;				
				[JsonProperty("Включить GUI сообщение")] public bool TGUIMessage;				
				[JsonProperty("Включить чат сообщение")] public bool TMessage;				
				[JsonProperty("Разрешить ломать солому во время защиты")] public bool Twigs;				
				[JsonProperty("0 - Защита только для игроков с пермишеном, 1 - Защита для всех игроков, 2 - Защита для игроков с пермишеном и для всех игроков")] public int TypeProtection;
			}			
			[JsonProperty("Список префабов которые будут под защитой")]
            public PrefabSetting Prefab = new PrefabSetting();
			internal class MessageSetting
			{			
				[JsonProperty("Интервал между сообщениями. Мин - 10 сек")] public int TimeMessage;
			}			
			[JsonProperty("Настройка времени действия защиты")]
            public TimeSetting Time = new TimeSetting();			
			
			internal class PrefabSetting
			{
				[JsonProperty("Префабы")] public List<string> Prefabs;
			}			
			
			[JsonProperty("Сообщения в чат и GUI")]
            public MessageSetting Message = new MessageSetting();			
			
			internal class TimeSetting
			{
				[JsonProperty("Начало защиты | Часы")] public int HourStart;				
				[JsonProperty("Начало защиты | Минуты")] public int MinuteStart;				
				[JsonProperty("Конец защиты | Часы")] public int HourEnd;		 		
				[JsonProperty("Конец защиты | Минуты")] public int MinuteEnd;
				[JsonProperty("Часовой пояс - UTC+0:00")] public int GMT;
				[JsonProperty("Время таймера проверки активности защиты (.сек)")] public int Timer;
			}			
		}			
		
		private void MSC()
		{
			webrequest.Enqueue("http://worldtimeapi.org/api/timezone/Europe/London", null, (code, response) =>
            {
                if (code != 200 || response == null) return;
				
			    config.MSCTime = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(double.Parse(JsonConvert.DeserializeObject<JObject>(response)["unixtime"].ToString()) + (config.Time.GMT * 3600));
			    SaveConfig();
				
				timer.Once(3, () => _time = Time());
            }, this);
		} 
		   		 		  						  	   		  	 	 		  		  		  		 			  			 
        		
				
		private void OnServerInitialized()
		{		
			PrintWarning("\n-----------------------------\n" +
			"     Author - Monster\n" +
			"     VK - vk.com/idannopol\n" +
			"     Discord - Monster#4837\n" +
			"     Config - v.3233\n" +
			"-----------------------------");
			
			foreach(var perm in config.Permisssion)
			    permission.RegisterPermission(perm.Key, this);
			
			On = new TimeSpan(config.Time.HourStart, config.Time.MinuteStart, 0);
			Off = new TimeSpan(config.Time.HourEnd, config.Time.MinuteEnd, 0);
			
			MSC();
			InitializeLang();
			timer.Once(1, () => 
			{
				config.DateWipe = SaveRestore.SaveCreatedTime.ToString("dd/MM/yyyy");
				SaveConfig();
			});
			timer.Every(config.Time.Timer, () => MSC());
		}
		
				 
		
        private void Message(BasePlayer player, string s_damage) 
        {
            CuiHelper.DestroyUi(player, ".MessagePanel");
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiLabel
            {
				FadeOut = 0.75f,
                RectTransform = { AnchorMin = config.GUI.AnchorMin, AnchorMax = config.GUI.AnchorMax, OffsetMin = config.GUI.OffsetMin, OffsetMax = config.GUI.OffsetMax },
                Text = { FadeIn = 0.75f, Text = config.Setting.PDay ? string.Format(lang.GetMessage("WIPE_UI_MESSAGE", this, player.UserIDString), config.Setting.PDays, s_damage) : s_damage == "100" ? string.Format(lang.GetMessage("FULL_UI_MESSAGE", this, player.UserIDString), s_damage, Off.ToString(@"hh\:mm"), On.ToString(@"hh\:mm")) : string.Format(lang.GetMessage("UI_MESSAGE", this, player.UserIDString), s_damage, On.ToString(@"hh\:mm"), Off.ToString(@"hh\:mm")), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = config.GUI.SizeText, Color = config.GUI.ColorText }
            }, "Hud", ".MessagePanel");
			
			if (config.GUI.Icon) 
			{
			    container.Add(new CuiButton
                {
				    FadeOut = 0.75f,
                    RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-180 2", OffsetMax = "-154 28" },
                    Button = { Color = "0.8003233 0.5003233 0.5003233 0.8", Sprite = "assets/icons/vote_down.png" },
                    Text = { Text = "" }
                }, ".MessagePanel", ".Icon1");			
			
			    container.Add(new CuiButton
                {
				    FadeOut = 0.75f,
                    RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "154 2", OffsetMax = "180 28" },
                    Button = { Color = "0.8003233 0.5003233 0.5003233 0.8", Sprite = "assets/icons/vote_down.png" },
                    Text = { Text = "" }
                }, ".MessagePanel", ".Icon2");
			}
		   		 		  						  	   		  	 	 		  		  		  		 			  			 
            CuiHelper.AddUi(player, container);  

            timer.Once(7.5f, () => { CuiHelper.DestroyUi(player, ".MessagePanel"); CuiHelper.DestroyUi(player, ".Icon1"); CuiHelper.DestroyUi(player, ".Icon2"); });
        }
				
		[PluginReference] private Plugin IQChat;
		bool _time;
		
		private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
		{
			if (entity == null || info?.InitiatorPlayer == null) return;
				
			if (config.Setting.TypeProtection == 0 || config.Setting.TypeProtection == 2)
				foreach(var perm in config.Permisssion)
					if(permission.UserHasPermission(entity.OwnerID.ToString(), perm.Key))
					{
					    Protection(entity, info, perm.Value.PermisssionDamage);
						return;
					}
			
			if (config.Setting.TypeProtection == 1 || config.Setting.TypeProtection == 2)
				Protection(entity, info, config.Setting.Damage);		
		}

		protected override void LoadConfig()
        {
            base.LoadConfig(); 
			 
			try 
			{ 
				config = Config.ReadObject<RaidConfig>();
			}
			catch  
			{
				PrintWarning("Ошибка чтения конфигурации! Создание дефолтной конфигурации!");
				LoadDefaultConfig();
			}
			
			SaveConfig();
        }
		
		private bool Time()
		{ 
			if(config.Setting.PDay)
			{
				DateTime days = config.MSCTime - DateTime.ParseExact(config.DateWipe, "dd/MM/yyyy", CultureInfo.InvariantCulture).Subtract(new DateTime(1970, 1, 1));
				int d = days.Subtract(new DateTime(1970, 1, 1)).Days;
				 
				return config.Setting.PDays > d;
			}
			else
			{
				var Now = config.MSCTime.TimeOfDay;
			
				if (On < Off)
					return On <= Now && Now <= Off;
     
				return !(Off < Now && Now < On);
			}
		}
		TimeSpan On, Off;
		   		 		  						  	   		  	 	 		  		  		  		 			  			 
        		
		 
        private void InitializeLang()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UI_MESSAGE"] = "NIGHT RAIDING PROTECTION ACTIVE: {0}%. FROM {1} TO {2}!",
                ["CHAT_MESSAGE"] = "NIGHT RAIDING PROTECTION ACTIVE: {0}%. FROM {1} TO {2}!",
				["FULL_UI_MESSAGE"] = "PROTECTION: {0}%. RAIDING IS ALLOWED FROM {1} TO {2}!",
                ["FULL_CHAT_MESSAGE"] = "PROTECTION: {0}%. RAIDING IS ALLOWED FROM {1} TO {2}!",				
				["WIPE_UI_MESSAGE"] = "IN THE FIRST {0} WIPE DAYS PROTECTION IN ACTIVITY! {1}%",
                ["WIPE_CHAT_MESSAGE"] = "IN THE FIRST {0} WIPE DAYS PROTECTION IN ACTIVITY! {1}%",
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UI_MESSAGE"] = "АКТИВНА НОЧНАЯ ЗАЩИТА ОТ РЕЙДА: {0}%. С {1} ДО {2}!",
                ["CHAT_MESSAGE"] = "АКТИВНА НОЧНАЯ ЗАЩИТА ОТ РЕЙДА: {0}%. С {1} ДО {2}!",
				["FULL_UI_MESSAGE"] = "ЗАЩИТА: {0}%. РЕЙДИТЬ РАЗРЕШЕНО С {1} ДО {2}!",
                ["FULL_CHAT_MESSAGE"] = "ЗАЩИТА: {0}%. РЕЙДИТЬ РАЗРЕШЕНО С {1} ДО {2}!",				
				["WIPE_UI_MESSAGE"] = "В ПЕРВЫЕ {0} ДНЯ ВАЙПА ДЕЙСТВУЕТ ЗАЩИТА! {1}%",
                ["WIPE_CHAT_MESSAGE"] = "В ПЕРВЫЕ {0} ДНЯ ВАЙПА ДЕЙСТВУЕТ ЗАЩИТА! {1}%",
            }, this, "ru");
        }
		
			}
}