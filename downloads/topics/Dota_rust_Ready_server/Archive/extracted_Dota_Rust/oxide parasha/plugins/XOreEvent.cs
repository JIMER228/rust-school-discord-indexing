using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;    
using System;
using UnityEngine;
using System.Collections;
using Oxide.Game.Rust.Cui;
  
///Скачано с дискорд сервера Rust Edit [PRO+]
///discord.gg/9vyTXsJyKR

namespace Oxide.Plugins 
{  
    [Info("XOreEvent", "discord.gg/9vyTXsJyKR", "1.0.0")]
    class XOreEvent : RustPlugin  
    { 		
		
				
		
        private void InitializeLang()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["START_EVENT"] = "START EVENT - <color=orange>STONE FEVER</color>\n<size=12><color=#a3f0ff>Spawn stones...</color></size>",	
                ["END_SPAWN_ORE"] = "<size=12><color=#a3f0ff>Gem spawning is complete!</color> Amount: {0}.</size>",	
                ["END_EVENT"] = "END EVENT - <color=orange>STONE FEVER</color>\n<size=12><color=#a3f0ff>Removing stones...</color></size>",	
                ["END_KILL_ORE"] = "<size=12><color=#a3f0ff>Removal stones is complete!</color> Amount: {0}.</size>",
				["TIME"] = "LEFT: {0}"				
            }, this);
		   		 		  						  	   		  		 			  	 	 		  	 	 		  	  	
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["START_EVENT"] = "НАЧАЛО ИВЕНТА - <color=orange>КАМЕННАЯ ЛИХОРАДКА</color>\n<size=12><color=#a3f0ff>Спавн камней...</color></size>",	
                ["END_SPAWN_ORE"] = "<size=12><color=#a3f0ff>Спавн камней завершен!</color> Кол-во: {0}.</size>",	
                ["END_EVENT"] = "ЗАВЕРШЕНИЕ ИВЕНТА - <color=orange>КАМЕННАЯ ЛИХОРАДКА</color>\n<size=12><color=#a3f0ff>Удаление камней...</color></size>",	
                ["END_KILL_ORE"] = "<size=12><color=#a3f0ff>Удаление камней завершено!</color> Кол-во: {0}.</size>",
				["TIME"] = "ОСТАЛОСЬ: {0}"
            }, this, "ru");
        }
		   		 		  						  	   		  		 			  	 	 		  	 	 		  	  	
        private class OreEventConfig  
        {		
			[JsonProperty("Настройки GUI")] 
			public GUISetting GUI = new GUISetting();								
			
			internal class GUISetting
			{
				[JsonProperty("AnchorMin")] public string AnchorMin;
				[JsonProperty("AnchorMax")] public string AnchorMax;
				[JsonProperty("OffsetMin")] public string OffsetMin;
				[JsonProperty("OffsetMax")] public string OffsetMax;
			}		
			internal class GeneralSetting
			{
				[JsonProperty("Длительность ивента")] public int EventTime;
				[JsonProperty("Использовать эффект при спавне камней")] public bool EffectSpawn;
				[JsonProperty("Использовать эффект при удалении камней")] public bool EffectKill;
				[JsonProperty("Минимальный онлайн для начала ивента")] public int MinOnline;
				[JsonProperty("Интервал начала ивента")] public int EventEvery;
			}				
			
			[JsonProperty("Общие настройки")]
			public GeneralSetting Setting = new GeneralSetting();			
			
			public static OreEventConfig GetNewConfiguration()
            { 
                return new OreEventConfig 
                {
					Setting = new GeneralSetting
					{
						MinOnline = 7,
						EventEvery = 5400,
						EventTime = 210,
						EffectSpawn = true,
						EffectKill = true
					},
					GUI = new GUISetting
					{
						AnchorMin = "1 0.7",
						AnchorMax = "1 0.7",
						OffsetMin = "-155 -17.5",
						OffsetMax = "-5 17.5"
					} 
				}; 
			}
        }
		
				
				
		private void TimerGUI(BasePlayer player, int seconds)
		{
			CuiHelper.DestroyUi(player, ".OreEventUI");
			CuiElementContainer container = new CuiElementContainer(); 
					
			container.Add(new CuiLabel
			{
				RectTransform = { AnchorMin = config.GUI.AnchorMin, AnchorMax = config.GUI.AnchorMax, OffsetMin = config.GUI.OffsetMin, OffsetMax = config.GUI.OffsetMax },
				Text = { Text = string.Format(lang.GetMessage("TIME", this, player.UserIDString), TimeSpan.FromSeconds(seconds)), Align = TextAnchor.MiddleCenter, FontSize = 14, Color = "1 1 1 0.55" }
			}, "Hud", ".OreEventUI");
				
			CuiHelper.AddUi(player, container);
		}
		
				 
		private OreEventConfig config;
		 
		protected override void LoadConfig()
        { 
            base.LoadConfig(); 
			 
			try
			{ 
				config = Config.ReadObject<OreEventConfig>();
			}
			catch  
			{
				PrintWarning("Ошибка чтения конфигурации! Создание дефолтной конфигурации!");
				LoadDefaultConfig();
			}
			
			SaveConfig();
        }
		Coroutine _coroutine, _coroutineTimer;
		
		private IEnumerator Timer(int seconds)
		{
			for(int i = 0; i <= seconds; i++)
			{
				BasePlayer.activePlayerList.ToList().ForEach(x => TimerGUI(x, seconds - i));
				
				yield return CoroutineEx.waitForSeconds(1);
			} 
			
			BasePlayer.activePlayerList.ToList().ForEach(x => CuiHelper.DestroyUi(x, ".OreEventUI"));
			_coroutineTimer = null;
			ServerMgr.Instance.StartCoroutine(OreEventKill());
		}
		
		private IEnumerator OreEventKill()
		{
			BasePlayer.activePlayerList.ToList().ForEach(x => Player.Reply(x, lang.GetMessage("END_EVENT", this, x.UserIDString)));
			
			yield return CoroutineEx.waitForSeconds(1);
			
			var ores = BaseNetworkable.serverEntities.Where(x => x is StagedResourceEntity && _OresEvent.Contains(x.net.ID)).ToList();
			int y = 0;
			
			foreach(StagedResourceEntity ore in ores)
			{
				if(ore == null) continue;
				
				ore.Kill();
				y++; 
				
				if(config.Setting.EffectKill)
					Effect.server.Run("assets/bundled/prefabs/fx/survey_explosion.prefab", ore.transform.position);
				
				yield return CoroutineEx.waitForSeconds(0.02f);
			}
			
			yield return CoroutineEx.waitForSeconds(1);
			
			BasePlayer.activePlayerList.ToList().ForEach(x => Player.Reply(x, string.Format(lang.GetMessage("END_KILL_ORE", this, x.UserIDString), y)));
			_OresEvent.Clear();
			_coroutine = null;
		}
		protected override void LoadDefaultConfig() => config = OreEventConfig.GetNewConfiguration();
		public List<uint> _OresEvent = new List<uint>(); 
		
		private void Unload() 
		{  
		    if(_coroutine != null)
				ServerMgr.Instance.StopCoroutine(_coroutine);		    
			if(_coroutineTimer != null)
				ServerMgr.Instance.StopCoroutine(_coroutineTimer);
			
			if(_OresEvent.Count != 0)
			{
				var ores = BaseNetworkable.serverEntities.Where(x => x is StagedResourceEntity && _OresEvent.Contains(x.net.ID)).ToList();
				
				foreach(StagedResourceEntity ore in ores)
					ore.Kill();
					 
				_OresEvent.Clear(); 
			}
			
			BasePlayer.activePlayerList.ToList().ForEach(x => CuiHelper.DestroyUi(x, ".OreEventUI"));
		}
		
				
				
		[ConsoleCommand("start_oreevent_admin")]
		private void ccmdStartEventAdmin(ConsoleSystem.Arg arg)
		{
			BasePlayer player = arg.Player();
			
			if ((player == null || player.IsAdmin) && _coroutine == null && _coroutineTimer == null)
				_coroutine = ServerMgr.Instance.StartCoroutine(OreEventStart());
		}		
		
		private void StartEvent() 
		{
			int online = BasePlayer.activePlayerList.ToList().Count;
				
			if(online >= config.Setting.MinOnline && _coroutine == null && _coroutineTimer == null)
				_coroutine = ServerMgr.Instance.StartCoroutine(OreEventStart());
			else
				PrintWarning($"Недостаточный онлайн на сервере! Нужно: {config.Setting.MinOnline} | Сейчас: {online} | Или ивент уже запущен![{_coroutine != null || _coroutineTimer != null}]");
		}
		
		private IEnumerator OreEventStart()
		{
			BasePlayer.activePlayerList.ToList().ForEach(x => Player.Reply(x, lang.GetMessage("START_EVENT", this, x.UserIDString)));
			
			yield return CoroutineEx.waitForSeconds(1);
			
			int y = 0;
			var ores = BaseNetworkable.serverEntities.Where(x => x is StagedResourceEntity).ToList();
			
			foreach(StagedResourceEntity ore in ores)
			{ 
			    if(ore == null) continue;
			
				BaseEntity entity = GameManager.server.CreateEntity(ore.name, ore.transform.position + new Vector3(0, 0.8f, 0), ore.transform.rotation);
				entity.Spawn();
				y++;
				
				_OresEvent.Add(entity.net.ID);
				if(config.Setting.EffectSpawn)
					Effect.server.Run("assets/bundled/prefabs/fx/survey_explosion.prefab", entity.transform.position);
				
				yield return CoroutineEx.waitForSeconds(0.02f);
			}
			
			yield return CoroutineEx.waitForSeconds(1);
			
			BasePlayer.activePlayerList.ToList().ForEach(x => Player.Reply(x, string.Format(lang.GetMessage("END_SPAWN_ORE", this, x.UserIDString), y)));
			_coroutineTimer = ServerMgr.Instance.StartCoroutine(Timer(config.Setting.EventTime));
		}
        protected override void SaveConfig() => Config.WriteObject(config);
		
				
				
	    private void OnServerInitialized()
		{

			
			timer.Every(config.Setting.EventEvery, () => StartEvent());
			InitializeLang();
		}

        	}
}
