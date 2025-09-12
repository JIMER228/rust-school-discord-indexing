/*
Плагин: ZRealtimeRates
Плагин позволяет вносить разнообразные рейты в игру относительно реального времени
Вы можете создавать несколько событий. Каждое событие имеет время начала и время окончания ивента.
Событие может влиять на следующие показатели:
 * Множитель урона по постройкам (К примеру защита построек в ночное время)
 * Множитель урона по игрокам (к примеру введение PVE времени на сервер)
 * Множители добычи и сбора игроками (время лучшего фарма)
 * Множители добычи и сбора карьерами (время лучшего фарма)
 * Множители выпадения дропа из бочек (время для лучшего дропа). 
 * Множители стоимости и (или) скорости исследований
 
 Множители, касающиеся добычи или выпадение предметов могут расапространяться на все допустимые предметы, либо заданием рейтов конкретным предметам
*/
using System;
using UnityEngine;
using System.Globalization;
using System.Collections.Generic;
using Rust;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Reflection;
using System.Linq;
using Oxide.Core.Configuration;
using UnityEngine.SceneManagement;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using UnityEngine.AI;

namespace Oxide.Plugins
{
    [Info("ZRealtimeRates", "BarakudaX777", "1.0.1")]
    [Description("Управление небольшими событиями, которые вносят изменения множителей в указанное серверное время")]
	

    class ZRealtimeRates : RustPlugin
    {		
		[PluginReference] private Plugin ImageLibrary;		
		#region Объявление классов и перименных
		private bool isStartEvent = false;
		private bool isCustomResearchSpeed = false;
		private int CurrentEvent=-1;
		private int timeEnum=381;
        private string Layer = "UI_ZRRMainUIHandler";
        private Timer GlobalTimer;
        private Timer OnceTimer;
		public TimeSettings LoadedTimeSettings;	
        private Configuration config;	
		
		public class TimeSettings{			
			public int startHour;
			public int startMinute;
			public int endHour;
			public int endMinute;
			public int timeDifference;
		}		
		
		public class ResearchSettings{			
            [JsonProperty("Множитель времени исследования")]
			public float MultiplierSpeed;
            [JsonProperty("Множитель стоимости исследования")]
			public float MultiplierCoast;
		}
        public class Anchors
        {
            [JsonProperty("Левая и нижняя позиция")]
			public string min;
            [JsonProperty("Правая и верхняя позиция")]
			public string max;
		}
        public class SystemConfig
        {
            [JsonProperty("Расположение GUI", Order = 1)]
			public Anchors GUIPosition { get; set; }
            [JsonProperty("Относительное расположение иконки", Order = 2)]
			public Anchors IconPosition { get; set; }
            [JsonProperty("Часовой пояс для учета и отображения времени UTC(3 = Москва)", Order = 3)]
			public int UTC=3;
		}
		
		public class TimeEvent{		
            [JsonProperty("Название режима", Order = 0)] 
			public string Name;			
            [JsonProperty("Подробное описание данного режима для справки", Order = 1)] 
			public string Description;			
			[JsonProperty("Иконка", Order = 2)] 
			public string Icon;
            [JsonProperty("Время начала действия режима (HH:mm)", Order = 3)]
			public string StartTime { get; set; }
            [JsonProperty("Время окончания действия режима (HH:mm)", Order = 4)]
			public string EndTime { get; set; }
            [JsonProperty("Множитель урона постройкам (По умолчанию 1)", Order = 5)]
			public float MultiplierDamageBuildings;
            [JsonProperty("Множитель урона игрокам (По умолчанию 1)", Order = 6)] 
			public float MultiplierDamagePlayers;
            [JsonProperty("Множитель добычи и сбора ресурсов", Order = 7)] 
			public Dictionary<string, float> MultiplierGather { get; set; }
            [JsonProperty("Множитель добычи карьерами", Order = 8)] 
			public Dictionary<string, float> MultiplierQuarry { get;  set; }
            [JsonProperty("Множитель добычи гигантского экскаватора", Order = 9)] 
			public Dictionary<string, float> MultiplierExcavator { get;  set; }
            [JsonProperty("Множитель добычи лута из бочек", Order =10)] 
			public Dictionary<string, float> MultiplierLoot { get;  set; }
            [JsonProperty("Настройка исследований", Order = 11)] 
			public ResearchSettings Research { get; set; }
		}
        public class Configuration
        {
            [JsonProperty("Системные настройки", Order = 0)]
			public SystemConfig System { get; set; }
            [JsonProperty("Список событий", Order = 1)]			
            public List<TimeEvent> Events = new List<TimeEvent>();
             
			public static Configuration GetNewCong()
            {
                return new Configuration
                {
					System = new SystemConfig{
						GUIPosition = new Anchors{
							min = "0.86 0.93",
							max = "0.99 0.99"
						},					
						IconPosition = new Anchors{
							min = "0.0 0.0",
							max = "0.26 1"
						},						
						UTC=3
						
					},
					Events =  new List<TimeEvent>(){
						new TimeEvent
						{
							Name="Ночной защитник",
							Description="В ночное время урон по игрокам и сооружениям снижен вдвое.",
							Icon = "https://i.ibb.co/pv4NC64/arm.png",
							StartTime="23:00",
							EndTime="06:00",
							MultiplierDamageBuildings=0.5f,
							MultiplierDamagePlayers=0.5f,
							MultiplierGather =  new Dictionary<string, float>{{"*",1.0f}},					
							MultiplierQuarry = new Dictionary<string, float>{{"*",1.0f}},				
							MultiplierExcavator = new Dictionary<string, float>{{"*",1.0f}},
							MultiplierLoot = new Dictionary<string, float>{{"*",1.0f}},
							Research = new ResearchSettings{
								MultiplierSpeed=1f,
								MultiplierCoast=1f
							}
						},
						new TimeEvent
						{
							Name="Время фарма",
							Description="Самое удачное время для фарма. Лут с бочек увеличен в два раза. Добыча и сбор ресурсов увеличен в 2 раза. Добыча карьерами увеличена в 2 раза.",
							Icon = "https://i.ibb.co/GHhfCgH/shovel.png",
							StartTime="12:00",
							EndTime="17:00",
							MultiplierDamageBuildings=1f,
							MultiplierDamagePlayers=1f,
							MultiplierGather =  new Dictionary<string, float>{
								{"*",1.0f},
								{"Wood", 2.0f},
								{"Stones", 2.0f},
								{"Sulfur", 2.0f},
								{"Sulfur Ore", 2.0f},
								{"Metal", 2.0f},
								{"Metal Ore", 2.0f},
								{"Cloth", 2.0f},
								{"Hemp Seed", 2.0f}
							},					
							MultiplierQuarry = new Dictionary<string, float>
							{
								{"*",2.0f}
							},				
							MultiplierExcavator = new Dictionary<string, float>
							{
								{"*",2.0f}
							},
							MultiplierLoot = new Dictionary<string, float>
							{
								{"*",2.0f}
							},
							Research = new ResearchSettings{
								MultiplierSpeed=1f,
								MultiplierCoast=1f
							}
						},						
						new TimeEvent
						{
							Name="Время исследований",
							Description="Стоимость и время проведения исследований уменьшены вдвое",
							Icon = "https://i.ibb.co/4pzJ1xY/brain.png",
							StartTime="21:00",
							EndTime="23:00",
							MultiplierDamageBuildings=1f,
							MultiplierDamagePlayers=1f,
							MultiplierGather =  new Dictionary<string, float>{{"*",1.0f}},					
							MultiplierQuarry = new Dictionary<string, float>{{"*",1.0f}},			
							MultiplierExcavator = new Dictionary<string, float>{{"*",1.0f}},
							MultiplierLoot = new Dictionary<string, float>{{"*",1.0f}},
							Research = new ResearchSettings{
								MultiplierSpeed=0.5f,
								MultiplierCoast=0.5f
							}
						}
					}
                };
            }
        }
		#endregion
		
        #region Локализация


        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"StartEvent", "<color=#19beff>Running mode \"{0}\"</color>\n{1}\n<color=#8bc34a>Time of action:</color> с {2} по {3}"},
				{"InfoEvent", "<color=#19beff>Information for  \"{0}\"</color>\n{1}\n<color=#8bc34a>Time of action:</color> с {2} по {3}"},
                {"NoEvent", "There are no active events at the moment"}
            }, this,"en");			
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"StartEvent", "<color=#19beff>Запущен режим \"{0}\"</color>\n{1}\n<color=#8bc34a>Время действия:</color> с {2} по {3}"},
				{"InfoEvent", "<color=#19beff>Описание режима \"{0}\"</color>\n{1}\n<color=#8bc34a>Время действия:</color> с {2} по {3}"},
                {"NoEvent", "В настоящий момент нет активных событий"}
            }, this,"ru");
        }

        #endregion
		
		#region Конфиг
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.System == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Создаём новую конфигурацию!");
                LoadDefaultConfig();
            }
            NextTick(SaveConfig);
        }
		protected override void LoadDefaultConfig() => config = Configuration.GetNewCong();
		protected override void SaveConfig() => Config.WriteObject(config);	
		#endregion
		
		void OnServerInitialized()
        {
            #if !RUST
            throw new NotSupportedException("Плагин не поддерживает игру");
            #endif
			if (config.Events!=null)
            foreach (var Event in config.Events) ImageLibrary.Call("AddImage", Event.Icon, "protect.icon"+config.Events.IndexOf(Event).ToString());
			
			CheckTime();
			 DateTime localDate = DateTime.Now;
			int nsec= 60 - localDate.Second;
			timer.Once(nsec, () =>
			{	
				CheckTime();
				GlobalTimer = timer.Repeat(60.0f, 0, () => CheckTime());
			});            			
		}
		
		void Unload()
        {
			//СМОТРИМ НУЖНО ЛИ ИЗМЕНЯТЬ СТОИМОСТИ ИССЛЕДОВАНИЯ И СКОРОСТЬ КРАФТА В СУЩЕСТВУЮЩИХ СТОЛАХ
			foreach (var table in UnityEngine.Object.FindObjectsOfType<ResearchTable>())
			{
				table.researchDuration = 10;
			}
			DestroyGUI();
        }
		
		
		//Проверка ивента по времени
        public void CheckTime()
        {	
			DateTime localDate;
			if (config.System.UTC!=null){
				TimeSpan offSet = TimeSpan.Parse(config.System.UTC+":00:00");
				localDate = DateTime.UtcNow + offSet;
				//Puts(localDate.ToString("dd.MM.yyyy HH:mm:ss"));					
				}
			else{localDate = DateTime.Now;}
			int localMinutes,startMinutes,endMinutes;
			bool execMe=false;
			//Puts("CHECKING STARTED");
			
			foreach (var Event in config.Events)
			{
				if (Event==null) return;
				
				localMinutes= localDate.Hour*60 + localDate.Minute;
				
				//Формируем массив времени
				DateTime Start=DateTime.ParseExact(Event.StartTime, "HH:mm", CultureInfo.InvariantCulture);
				DateTime End=DateTime.ParseExact(Event.EndTime, "HH:mm", CultureInfo.InvariantCulture);
				LoadedTimeSettings = new TimeSettings{
							startHour=Start.Hour,
							startMinute=Start.Minute,
							endHour=End.Hour,
							endMinute=End.Minute,
							timeDifference=3600*timeEnum
						};
				startMinutes= LoadedTimeSettings.startHour*60 + LoadedTimeSettings.startMinute;
				endMinutes= LoadedTimeSettings.endHour*60 + LoadedTimeSettings.endMinute;
				
				
				if (startMinutes<endMinutes){//&&
					if ((localMinutes>=startMinutes) && (localMinutes<endMinutes)) {					
						execMe=true;
					}
				}else {
					if ((localMinutes>=startMinutes)|| (localMinutes<endMinutes)) {					
						execMe=true; 
					}
				}				
				if (execMe){
				//	
					if (!isStartEvent) {
						//Puts("ISSTART"+isStartEvent.ToString());
						isStartEvent=true;
						CurrentEvent=config.Events.IndexOf(Event);
						//СМОТРИМ НУЖНО ЛИ ИЗМЕНЯТЬ СТОИМОСТИ ИССЛЕДОВАНИЯ И СКОРОСТЬ КРАФТА В СУЩЕСТВУЮЩИХ СТОЛАХ
						if (config.Events[CurrentEvent].Research.MultiplierSpeed!=1){
							isCustomResearchSpeed = true;
							foreach (var table in UnityEngine.Object.FindObjectsOfType<ResearchTable>())
							{					
								table.researchDuration= 10*config.Events[CurrentEvent].Research.MultiplierSpeed;  
							} 
						}
						foreach (var player in BasePlayer.activePlayerList){
							string msg = lang.GetMessage("StartEvent", this, player.UserIDString);
							PrintToChat(player,string.Format(msg, config.Events[CurrentEvent].Name, config.Events[CurrentEvent].Description, config.Events[CurrentEvent].StartTime, config.Events[CurrentEvent].EndTime));
						}
					}
					//Каждые 60 секунд обновляем ГУИ что игроки защищены
					foreach (var player in BasePlayer.activePlayerList){
						ShowGUI(player);
					}
					return;	//Выходим из циклов если мы нашли первое вхождение активного ивента
				}
			}
			if (isStartEvent){
				isStartEvent=false;
				LoadedTimeSettings=null;
				CurrentEvent=-1;
				DestroyGUI();
				if (isCustomResearchSpeed){
					foreach (var table in UnityEngine.Object.FindObjectsOfType<ResearchTable>())
					{					
						table.researchDuration= 10;
					}
				}
			}
        }
		
		//Проверяем если у нас активирован режим исследований 
		//(и изменение скорости не соответствует стандартной) 
		//то меняем скорости на всех исследовательских станках
		void OnEntityBuilt(Planner planner, GameObject gameobject)
        {   
			if (CurrentEvent==-1) return;    
			if (config.Events[CurrentEvent].Research.MultiplierSpeed==1f)return;
            BaseEntity entity = gameobject.ToBaseEntity();
			if (entity!=null)
			if (entity is ResearchTable){
				ResearchTable table = (ResearchTable)entity;
				table.researchDuration= 10*config.Events[CurrentEvent].Research.MultiplierSpeed;  
			}
		}
		
		//СОБЫТИЕ ПРИ НАНЕСЕНИИ УРОНА
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info) {    
			//Проверяем запущен ли ивент
            if (!isStartEvent) return; 
			if (CurrentEvent==-1) return;       
            if (entity == null || info == null || info.InitiatorPlayer == null || info.Initiator is BaseNpc || info.Initiator is ScientistNPC) return; //убираем ненужное
            
			if (entity.name.Contains("npc"))return; //Не будем влиять на НПС (если все таки нужно то закомментируйте)
			//Защита игроков
			if (entity is BasePlayer) {
				if ((config.Events[CurrentEvent].MultiplierDamagePlayers<=0) || (config.Events[CurrentEvent].MultiplierDamagePlayers==1)) return;	
                if (info.InitiatorPlayer.userID == (entity as BasePlayer).userID) return;	//если урон нанес сам себе	
					
                BasePlayer atacker = info.InitiatorPlayer;
				info.damageTypes.ScaleAll((float) (config.Events[CurrentEvent].MultiplierDamagePlayers));	
            }
			//Защита сооружений
			if ((config.Events[CurrentEvent].MultiplierDamageBuildings<=0) || (config.Events[CurrentEvent].MultiplierDamageBuildings==1)) return;
			if ((entity is BuildingBlock) || (entity is Door) || (entity.name.Contains("building"))){
				info.damageTypes.ScaleAll((float) (config.Events[CurrentEvent].MultiplierDamageBuildings));
			}            
        }
		
		// ПРИ СБОРЕ РЕСУРСОВ
		private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (!entity.ToPlayer())return;
			if (CurrentEvent==-1) return;

            float modifier; 
            if (config.Events[CurrentEvent].MultiplierGather.TryGetValue(item.info.displayName.english, out modifier))
            {
                if (modifier!=1f) item.amount = (int)(item.amount * modifier);
            }
            else if (config.Events[CurrentEvent].MultiplierGather.TryGetValue("*", out modifier))
            {
                if (modifier!=1f) item.amount = (int)(item.amount * modifier);
            }
        } 
		
		//При подъеме ресурсов
		private void OnCollectiblePickup(Item item, BasePlayer player)
		{
			if (CurrentEvent==-1) return;
            float modifier;
            if (config.Events[CurrentEvent].MultiplierGather.TryGetValue(item.info.displayName.english, out modifier))
            {
                if (modifier!=1f) item.amount = (int)(item.amount * modifier);
            }
            else if (config.Events[CurrentEvent].MultiplierGather.TryGetValue("*", out modifier))
            {
                if (modifier!=1f) item.amount = (int)(item.amount * modifier);
            }
		}
		
        private void OnCropGather(PlantEntity plant, Item item)
        {
			if (CurrentEvent==-1) return;
            float modifier;
            if (config.Events[CurrentEvent].MultiplierGather.TryGetValue(item.info.displayName.english, out modifier))
            {
                if (modifier!=1f) item.amount = (int)(item.amount * modifier);
            }
            else if (config.Events[CurrentEvent].MultiplierGather.TryGetValue("*", out modifier))
            {
                if (modifier!=1f) item.amount = (int)(item.amount * modifier);
            }
        }
		
		
		// ПРИ ДОБЫЧИ КАРЬЕРАМИ
		private void OnQuarryGather(MiningQuarry quarry, Item item)
        { 
			if (CurrentEvent==-1) return;
            float modifier=0;
			if (config.Events[CurrentEvent].MultiplierQuarry.TryGetValue(item.info.displayName.english, out modifier))
            {
				
                if (modifier!=1f) item.amount = (int)(item.amount * modifier);
            }
            else if (config.Events[CurrentEvent].MultiplierQuarry.TryGetValue("*", out modifier))
            {
                if (modifier!=1f) item.amount = (int)(item.amount * modifier);
            }
        } 
		
		//ПРИ ДОБЫЧИ ГИГАНТСКИМ ЭКСКАВАТОРОМ
		private void OnExcavatorGather(ExcavatorArm excavator, Item item)
		{
			if (CurrentEvent==-1) return;
            float modifier=0;
			if (config.Events[CurrentEvent].MultiplierExcavator.TryGetValue(item.info.displayName.english, out modifier))
            {
				
                if (modifier!=1f) item.amount = (int)(item.amount * modifier);
            }
            else if (config.Events[CurrentEvent].MultiplierExcavator.TryGetValue("*", out modifier))
            {
                if (modifier!=1f) item.amount = (int)(item.amount * modifier);
            }			
		}
		
		
		// ПРИ ЛУТАНИИ БОЧЕК
		private void OnContainerDropItems (ItemContainer  container)
		{			
			if (CurrentEvent==-1) return;
			if (container != null)
            {
				float modifier;
				foreach (Item lootitem in container.itemList) { 
					if (config.Events[CurrentEvent].MultiplierLoot.TryGetValue(lootitem.info.displayName.english, out modifier))
					{
						lootitem.amount = (int)(lootitem.amount * modifier);
					}
					else if (config.Events[CurrentEvent].MultiplierLoot.TryGetValue("*", out modifier))
					{
						lootitem.amount = (int)(lootitem.amount * modifier);
					}
				}
			}
		}
		
		//Меняем стоимость исследований
        int OnItemScrap(ResearchTable table, Item item)
        {	
			if (CurrentEvent==-1) return (int)(GetDefaultPrice(item.info));
			return (int)(GetDefaultPrice(item.info)*config.Events[CurrentEvent].Research.MultiplierCoast);          
        }
		 private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (player==null || !isStartEvent) return;
			string msg = lang.GetMessage("StartEvent", this, player.UserIDString);
			PrintToChat(player,string.Format(msg, config.Events[CurrentEvent].Name, config.Events[CurrentEvent].Description, config.Events[CurrentEvent].StartTime, config.Events[CurrentEvent].EndTime));
			ShowGUI(player);            
			//Puts("WAKED");
        }
		//Информируем об экономии или перерасходе		
		void OnPlayerLootEnd(PlayerLoot inventory)
		{	
			if (CurrentEvent==-1) return;
			if (inventory.entitySource is ResearchTable) {
				var player = (BasePlayer) inventory.gameObject.ToBaseEntity();				
				string s="МЕНЬШЕ";
				int proc=(int)(Math.Abs(1-config.Events[CurrentEvent].Research.MultiplierCoast)*100);
				if (config.Events[CurrentEvent].Research.MultiplierCoast>1) s = "БОЛЬШЕ";
				SendInfoMessage(player, "БЛАГОДАРЯ РЕЖИМА <<" + config.Events[CurrentEvent].Name.ToUpper() + ">> ВЫ ПОТРАТИЛИ СКРАПА "+s+" НА "+(proc).ToString()+"%");
			}
		}
		
		//Вывод в чат описания события
        [ChatCommand("show_event_desc")]
        private void cmdShowDesc(BasePlayer player)
        {
			if (CurrentEvent==-1) return;
			if (config.Events[CurrentEvent]==null) return;
			if ((config.Events[CurrentEvent].Description=="") || (config.Events[CurrentEvent].Description==null)) return;
			
			string msg = lang.GetMessage("InfoEvent", this, player.UserIDString);
			PrintToChat(player,string.Format(msg, config.Events[CurrentEvent].Name, config.Events[CurrentEvent].Description, config.Events[CurrentEvent].StartTime, config.Events[CurrentEvent].EndTime));
			
		}
		#region GUI		
        //GUI 
        public void DestroyGUI(BasePlayer player1=null)
        {   //Если не указан конкретный игрок удаляем у всех
			if (player1==null) {
				foreach (var player in BasePlayer.activePlayerList){
					CuiHelper.DestroyUi(player, Layer);
				}
			}else{
				CuiHelper.DestroyUi(player1, Layer);						
			}
		}
		
        public void ShowGUI(BasePlayer player)
        {   
			if (CurrentEvent==-1) return;
            DestroyGUI(player);			
            var container = new CuiElementContainer();
            var Panel = container.Add(new CuiPanel
            {
                Image = { Color = HexToCuiColor("#333333AA") },
                RectTransform = { AnchorMin = config.System.GUIPosition.min, AnchorMax = config.System.GUIPosition.max  },
                CursorEnabled = false,
            },  "Hud", Layer);

            //ПОЗИЦИЯ ИКОНКИ ЗАЩИТЫ
			
            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".Icon",
                Components =
                {
                    new CuiImageComponent { Color = HexToCuiColor("#FF000000") },
                    new CuiRectTransformComponent {AnchorMin = config.System.IconPosition.min,AnchorMax =  config.System.IconPosition.max, OffsetMax = "0 0"}
                }
            });
            //картинка
            container.Add(new CuiElement
            {
                Parent = Layer + ".Icon",
                Components =
                {
                    new CuiRawImageComponent {Png = (string) ImageLibrary.Call("GetImage", "protect.icon"+CurrentEvent.ToString()), Color = "1 1 1 1"},
                    new CuiRectTransformComponent { AnchorMin = "0.05 0.05", AnchorMax = "0.95 0.95", OffsetMax = "0 0" }
                }
            });
            // КОНЕЦ Иконки
			string name = config.Events[CurrentEvent].Name;
			 container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.23 0.4", AnchorMax = "1 0.9", OffsetMax = "0.4 0.4"},
                Text = {Text= $"{name}", Align = TextAnchor.UpperCenter, Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.7"}
            }, Layer);
			
            container.Add(new CuiButton
            {
                RectTransform ={AnchorMin = "0.23 0",AnchorMax = "0.998 0.4"},
                Button ={Command = "realrates desc",Color = "0.7 0 0 0.5"},
                Text ={Text = "подробнее",FontSize = 14 , Font = "robotocondensed-regular.ttf",Align = TextAnchor.MiddleCenter,Color = "1 1 1 1"}
            }, Layer);


            CuiHelper.AddUi(player, container);
        }
		
		[ConsoleCommand("realrates")]
		private void UI_ToggleKitMenu(ConsoleSystem.Arg arg)
		{
			if (arg.Player() == null) return;
            BasePlayer player = arg.Player();
			string[] args = arg.Args ?? new string[] { };						
			if (args[0] == "desc"){				
				if (CurrentEvent==-1) return;				
				if (config.Events[CurrentEvent]==null) return;
				if ((config.Events[CurrentEvent].Description=="") || (config.Events[CurrentEvent].Description==null)) return;
				string msg = lang.GetMessage("InfoEvent", this, player.UserIDString);
				PrintToChat(player,string.Format(msg, config.Events[CurrentEvent].Name, config.Events[CurrentEvent].Description, config.Events[CurrentEvent].StartTime, config.Events[CurrentEvent].EndTime));
			}
		}
			
		#endregion
		
        #region Functions        
        private DateTime ParseTime(string time) => DateTime.ParseExact(time, "HH:mm", CultureInfo.InvariantCulture);		
		
        private static string HexToCuiColor(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                hex = "#FFFFFFFF";
            }
            var str = hex.Trim('#');
            if (str.Length == 6)
                str += "FF";
            if (str.Length != 8)
            {
                throw new Exception(hex);
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);
            Color color = new Color32(r, g, b, a);
            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }
        
        public int GetDefaultPrice(ItemDefinition def)
        {
            int num = 0;
            if (def.rarity == Rust.Rarity.Common)
                num = 20;
            if (def.rarity == Rust.Rarity.Uncommon)
                num = 75;
            if (def.rarity == Rust.Rarity.Rare)
                num = 125;
            if (def.rarity == Rust.Rarity.VeryRare || def.rarity == Rust.Rarity.None)
                num = 500;
            return num;
        }
		
		 private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);
		
        private void SendInfoMessage(BasePlayer player, string message)
        {
            player?.SendConsoleCommand("gametip.showgametip", message);
            timer.Once(3f, () => player?.SendConsoleCommand("gametip.hidegametip"));
        }
		#endregion
		
    }
}
        