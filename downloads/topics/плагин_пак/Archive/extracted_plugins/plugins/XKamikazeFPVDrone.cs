using System.Collections.Generic;
using Oxide.Core;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using System.Linq;
using UnityEngine;
		   		 		  						  	   		  	   		  	 				   		 		  				
namespace Oxide.Plugins
{
    [Info("XKamikazeFPVDrone", "Monster", "1.0.0")]
    class XKamikazeFPVDrone : RustPlugin
    {
		protected override void LoadDefaultConfig() => config = DroneConfig.GetNewConfiguration();
		
				
				
		private bool API_IsFPVDrone(Drone drone) => drone != null && (drone.skinID == config.Drone.SkinID || drone.skinID == 203581911);
		
				
				
		private void InitializeLang()
		{
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TITLE"] = "CRAFT MENU FPV DRONE COOL SERVER",
                ["NOPERM"] = "No permissions!",
                ["MSG_RESOURCE"] = "INSUFFICIENT RESOURCES FOR CRAFTING!",
                ["MSG_WORKBENCH"] = "INSUFFICIENT WORKBENCH LEVEL FOR CRAFTING!",
                ["MSG_CRAFT"] = "DRONE SUCCESSFULLY CRAFTED!",
                ["LVL_WORKBENCH"] = "WORKBENCH: {0} LVL",
                ["BUTTON_CRAFT"] = "CRAFT"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TITLE"] = "МЕНЮ КРАФТА FPV ДРОНА КРУТОГО СЕРВЕРА",
                ["NOPERM"] = "Недостаточно прав!",
                ["MSG_RESOURCE"] = "НЕДОСТАТОЧНО РЕСУРСОВ ДЛЯ КРАФТА!",
                ["MSG_WORKBENCH"] = "НЕДОСТАТОЧНЫЙ УРОВЕНЬ ВЕРСТАКА ДЛЯ КРАФТА!",
                ["MSG_CRAFT"] = "ДРОН УСПЕШНО СКРАФЧЕН!",
                ["LVL_WORKBENCH"] = "ВЕРСТАК: {0} LVL",
                ["BUTTON_CRAFT"] = "КРАФТ"
            }, this, "ru");
			
			lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TITLE"] = "МЕНЮ КРАФТУ FPV ДРОНА КРУТОГО СЕРВЕРУ",
                ["NOPERM"] = "Недостатньо прав!",
                ["MSG_RESOURCE"] = "НЕДОСТАТНЬО РЕСУРСІВ ДЛЯ КРАФТУ!",
                ["MSG_WORKBENCH"] = "НЕДОСТАТНІЙ РІВЕНЬ ВЕРСТАТА ДЛЯ КРАФТУ!",
                ["MSG_CRAFT"] = "ДРОН УСПІШНО СКРАФЧЕНИЙ!",
                ["LVL_WORKBENCH"] = "ВЕРСТАТ: {0} LVL",
                ["BUTTON_CRAFT"] = "КРАФТ"
            }, this, "uk");
			
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TITLE"] = "MENÚ ARTESANAL FPV DRONE FRESCO SERVIDOR",
                ["NOPERM"] = "¡No tienes permisos!",
                ["MSG_RESOURCE"] = "¡NO HAY RECURSOS SUFICIENTES PARA LA ARTESANÍA!",
                ["MSG_WORKBENCH"] = "¡INSUFICIENTE NIVEL DE BANCO DE TRABAJO PARA LA ARTESANÍA!",
                ["MSG_CRAFT"] = "¡DRON DISEÑADO CON ÉXITO!",
                ["LVL_WORKBENCH"] = "WORKBENCH: {0} LVL",
                ["BUTTON_CRAFT"] = "CRAFT"
            }, this, "es-ES");
		}
		
		private const string permCraft = "xkamikazefpvdrone.usecraft";
		
				
				
		private void SendInfo(BasePlayer player, string message)
        {
			CuiElementContainer container = new CuiElementContainer();
			
			container.Add(new CuiLabel
            {
				FadeOut = 0.5f,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMax = config.Craft.WorkBench > 0 ? "337.75 36.75" : "497.75 36.75" },
                Text = { FadeIn = 0.5f, Text = message, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 14, Color = "0.75 0.75 0.75 1" }
            }, ".CraftDroneB", ".SI", ".SI");
			
			CuiHelper.AddUi(player, container);
			player.Invoke(() => CuiHelper.DestroyUi(player, ".SI"), 3);
        }
		
		[ConsoleCommand("craft_drone")]
        void ccmdCraftDrone(ConsoleSystem.Arg args)
		{
			BasePlayer player = args.Player();
			
			if(player != null && permission.UserHasPermission(player.UserIDString, permCraft))
			{
				Effect x = new Effect("assets/bundled/prefabs/fx/invite_notice.prefab", player, 0, new Vector3(), new Vector3());
				
				foreach(var item in config.Craft.ItemList)
					if(!GetItemAmount(player, _itemsId[item.Key], item.Value))
					{
						EffectNetwork.Send(x, player.Connection);
						SendInfo(player, lang.GetMessage("MSG_RESOURCE", this, player.UserIDString));
						
						return;
					}
					
				if(player.currentCraftLevel < config.Craft.WorkBench)
				{
					EffectNetwork.Send(x, player.Connection);
					SendInfo(player, lang.GetMessage("MSG_WORKBENCH", this, player.UserIDString));
					
					return;
				}
				
				foreach(var item in config.Craft.ItemList)
					player.inventory.Take(null, _itemsId[item.Key], item.Value);
				
				PlayerGiveDrone(player);
				
				EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/repairbench/itemrepair.prefab", player, 0, new Vector3(), new Vector3()), player.Connection);
				GUI(player);
				SendInfo(player, lang.GetMessage("MSG_CRAFT", this, player.UserIDString));
			}
		}
		
		protected override void LoadConfig()
        {
            base.LoadConfig();
			
			try
			{
				config = Config.ReadObject<DroneConfig>();
			}
			catch
			{
				PrintWarning(LanguageEnglish ? "Configuration read error! Creating a default configuration!" : "Ошибка чтения конфигурации! Создание дефолтной конфигурации!");
				LoadDefaultConfig();
			}
			
			SaveConfig();
        }
		
				
				
		private void OnServerInitialized()
		{
			PrintWarning("\n-----------------------------\n" +
			"     Author - Monster\n" +
			"     VK - vk.com/idannopol\n" +
			"     Discord - Monster#4837\n" +
			"     Config - v.08911\n" +
			"-----------------------------");
			
			foreach(var item in config.Craft.ItemList)
				_itemsId.Add(item.Key, ItemManager.FindItemDefinition(item.Key).itemid);
				
			permission.RegisterPermission(permCraft, this);
				
			InitializeLang();
		}
		
		private void PlayerGiveDrone(BasePlayer player, int amount = 1)
		{
			Item item = ItemManager.CreateByName("drone", amount, config.Drone.SkinID);
			
			player.GiveItem(item);
		}
		
		private void OnEntityDeath(Drone drone)
		{
			if(API_IsFPVDrone(drone))
			{
				TimedExplosive rocket = GameManager.server.CreateEntity("assets/prefabs/ammo/rocket/rocket_basic.prefab", drone.transform.position) as TimedExplosive;
				
				rocket.explosionRadius = config.Explosive.ExplosionRadius;
				rocket.SetDamageScale(config.Explosive.DamageScale);
				
				rocket.Spawn();
				rocket.Explode();
				
				Interface.CallHook("OnDroneExplode", drone, drone.transform.position);
			}
		}
		
				
		private DroneConfig config;
		
		private Dictionary<string, int> _itemsId = new Dictionary<string, int>();
		
				
				
		private void GUI(BasePlayer player)
		{
			CuiElementContainer container = new CuiElementContainer();
			
			container.Add(new CuiElement
			{
				Parent = "Overlay",
				Name = ".CraftDrone",
				DestroyUi = ".CraftDrone",
				Components =
				{
					new CuiImageComponent { Color = "0.517 0.521 0.509 0.95", Material = "assets/icons/greyout.mat" },
					new CuiRectTransformComponent { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-337 90", OffsetMax = "318 268.5" },
					new CuiNeedsCursorComponent {},
				}
			});
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" },
                Image = { Color = "0.217047301 0.221047301 0.209047301 0.95047301" }
            }, ".CraftDrone", ".CraftDroneB");
			
			container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -36.75", OffsetMax = "0 0" },
                Text = { Text = lang.GetMessage("TITLE", this, player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 18, Color = "1 1 1 0.75" }
            }, ".CraftDroneB");
			
			int count = config.Craft.ItemList.Count, workbenchLvl = config.Craft.WorkBench;
			
			foreach(var item in config.Craft.ItemList)
			{
				double offset = -(37.5 * count--) + -(2.5 * count--);
				
			    container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{offset} -37.5", OffsetMax = $"{offset + 75} 37.5" },
                    Image = { Color = "0.517047301 0.521047301 0.509047301 0.5047301", Material = "assets/icons/greyout.mat" }
                }, ".CraftDroneB", ".Item");
				
				container.Add(new CuiElement
				{
					Parent = ".Item",
					Components =
					{
						new CuiImageComponent { ItemId = _itemsId[item.Key] },
						new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" }
					}
				});
				
				container.Add(new CuiLabel
				{
					RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.5", OffsetMax = "-2 0" },
					Text = { Text = $"x{item.Value}", Align = TextAnchor.LowerRight, FontSize = 12, Font = "robotocondensed-regular.ttf", Color = GetItemAmount(player, _itemsId[item.Key], item.Value) ? "0.3047301 1 0.3047301 1" : "1 0.3047301 0.3047301 1" }
				}, ".Item");
			}
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -41.75", OffsetMax = "0 -36.75" },
                Image = { Color = "0.517 0.521 0.509 0.95", Material = "assets/icons/greyout.mat" }
            }, ".CraftDroneB");			
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 36.75", OffsetMax = "0 41.75" },
                Image = { Color = "0.517 0.521 0.509 0.95", Material = "assets/icons/greyout.mat" }
            }, ".CraftDroneB");
			
			container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-31.75 5", OffsetMax = "-5 31.75" },
                Button = { Color = "1 1 1 0.75", Sprite = "assets/icons/close.png", Close = ".CraftDrone" },
                Text = { Text = "" }
            }, ".CraftDroneB");
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-41.75 0", OffsetMax = "-36.75 41.75" },
                Image = { Color = "0.517 0.521 0.509 0.95", Material = "assets/icons/greyout.mat" }
            }, ".CraftDroneB");
			
			if(workbenchLvl > 0)
			{
				container.Add(new CuiPanel
				{
					RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-296.75 5", OffsetMax = "-151.75 31.75" },
					Image = { Color = workbenchLvl == 1 ? "0.71 0.95 0.29 0.57" : workbenchLvl == 2 ? "0.26 0.69 1 0.57" : "1 0.58 0.27 0.57" }
				}, ".CraftDroneB", ".WorkBench");
				
				container.Add(new CuiLabel
				{
					RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
					Text = { Text = string.Format(lang.GetMessage("LVL_WORKBENCH", this, player.UserIDString), workbenchLvl), Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-regular.ttf", Color = "0.8 0.8 0.8 1" }
				}, ".WorkBench");
				
				container.Add(new CuiPanel
				{
					RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-306.75 0", OffsetMax = "-301.75 41.75" },
					Image = { Color = "0.517 0.521 0.509 0.95", Material = "assets/icons/greyout.mat" }
				}, ".CraftDroneB");
			}
			
			container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-136.75 5", OffsetMax = "-46.75 31.75" },
                Button = { Color = "0.35 0.45 0.25 1", Command = "craft_drone" },
                Text = { Text = lang.GetMessage("BUTTON_CRAFT", this, player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-regular.ttf", Color = "0.75 0.95 0.41 1" }
            }, ".CraftDroneB");
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-146.75 0", OffsetMax = "-141.75 41.75" },
                Image = { Color = "0.517047301 0.521047301 0.509047301 0.95047301", Material = "assets/icons/greyout.mat" }
            }, ".CraftDroneB");
			
			CuiHelper.AddUi(player, container);
		}
		
				
				
		[ChatCommand("craft.d")]
		void cmdOpenGUI(BasePlayer player)
		{
			if(permission.UserHasPermission(player.UserIDString, permCraft))
				GUI(player);
			else
				SendReply(player, lang.GetMessage("NOPERM", this, player.UserIDString));
		}
		
		[ConsoleCommand("give_drone")]
        void ccmdGiveDrone(ConsoleSystem.Arg args)
		{
			if(args?.Args != null && args.Args.Length >= 2 && (args.Player() == null || args.Player().IsAdmin))
			{
				string id = args.Args[0];
				
				ulong steamID;
				ulong.TryParse(id, out steamID);
				
				int amount;
				if(!int.TryParse(args.Args[1], out amount))
					amount = 1;
				
				BasePlayer player = BasePlayer.FindByID(steamID);
				
				if(player == null)
					PrintError(LanguageEnglish ? $"Player [ {id} ] not found!" : $"Игрок [ {id} ] не найден!");
				else
					PlayerGiveDrone(player, amount);
			}
		}
		
		private bool GetItemAmount(BasePlayer player, int itemId, int amount) => player.inventory.GetAmount(itemId) >= amount;
		
        private class DroneConfig
        {
			
			internal class ExplosiveSetting
			{
				[JsonProperty(LanguageEnglish ? "Explosion radius. [ Default: 3.8 ]" : "Радиус взрыва. [ По умолчанию: 3.8 ]")] public float ExplosionRadius;
				[JsonProperty(LanguageEnglish ? "Damage scale. [ Default: 1.0 - 137 HP ]" : "Масштаб урона. [ По умолчанию: 1.0 - 137 HP ]")] public float DamageScale;
			}
			[JsonProperty(LanguageEnglish ? "Craft settings" : "Настройки крафта")]
			public CraftSetting Craft = new CraftSetting();
			[JsonProperty(LanguageEnglish ? "Explosive settings" : "Настройки взрывчатки")]
			public ExplosiveSetting Explosive = new ExplosiveSetting();			
			
			internal class CraftSetting
			{
				[JsonProperty(LanguageEnglish ? "Crafting workbench level. [ 0 - workbench is not required ]" : "Уровень верстака для крафта. [ 0 - верстак не требуется ]")] public int WorkBench;
				[JsonProperty(LanguageEnglish ? "List of crafting resources" : "Список ресурсов для крафта")] public Dictionary<string, int> ItemList;
			}
			internal class DroneSetting
			{
				[JsonProperty(LanguageEnglish ? "Drone skin" : "Скин дрона")] public ulong SkinID;
				[JsonProperty(LanguageEnglish ? "The speed at which the drone camera moves left and right. [ Default: 2.0 ]" : "Скорость, с которой камера дрона движется влево и вправо. [ По умолчанию: 2.0 ]")] public float YawSpeed;
				[JsonProperty(LanguageEnglish ? "The speed of the drone moving left, right, forward and backward. [ Default: 10.0 ]" : "Скорость дрона, движущегося влево, вправо, вперед и назад. [ По умолчанию: 10.0 ]")] public float MovementAcceleration;
				[JsonProperty(LanguageEnglish ? "The speed of the drone moving up and down. [ Default: 10.0 ]" : "Скорость дрона, движущегося вверх и вниз. [ По умолчанию: 10.0 ]")] public float AltitudeAcceleration;
				[JsonProperty(LanguageEnglish ? "The speed at which the drone camera moves up and down. [ Default: 2.0 ]" : "Скорость, с которой камера дрона движется вверх и вниз. [ По умолчанию: 2.0 ]")] public float UprightSpeed;
			}
			
			[JsonProperty(LanguageEnglish ? "Drone settings" : "Настройки дрона")]
			public DroneSetting Drone = new DroneSetting();
			
			public static DroneConfig GetNewConfiguration()
            {
                return new DroneConfig
                {
					Drone = new DroneSetting
					{
						SkinID = 3024482565,
						YawSpeed = 5.0f,
						UprightSpeed = 5.0f,
						MovementAcceleration = 50.0f,
						AltitudeAcceleration = 50.0f,
					},
					Explosive = new ExplosiveSetting
					{
						ExplosionRadius = 5.0f,
						DamageScale = 2.5f
					},
					Craft = new CraftSetting
					{
						WorkBench = 2,
						ItemList = new Dictionary<string, int>
						{
							["ammo.rocket.basic"] = 1,
							["techparts"] = 1,
							["battery.small"] = 1,
							["ducttape"] = 2,
							["wiretool"] = 1,
							["gunpowder"] = 25,
							["cloth"] = 10,
							["lowgradefuel"] = 5
						}
					}
				};
			}
        }
		
		private void OnEntitySpawned(Drone drone)
		{
			NextTick(() =>
			{
				if(API_IsFPVDrone(drone))
				{
					if(drone.GetComponentsInChildren<TeslaCoil>().Count() == 0)
					{
						BaseEntity entity = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/teslacoil/teslacoil.deployed.prefab");
						
						entity.SetParent(drone);
						
						entity.transform.localPosition = new Vector3(0, 0.2f, -0.2f);
						entity.transform.localRotation = Quaternion.Euler(new Vector3(90, 0, 0));
						
						entity.Spawn();
					}
					
					drone.yawSpeed = config.Drone.YawSpeed;
					drone.uprightSpeed = config.Drone.UprightSpeed;
					drone.movementAcceleration = config.Drone.MovementAcceleration;
					drone.altitudeAcceleration = config.Drone.AltitudeAcceleration;
				}
			});
		}
        protected override void SaveConfig() => Config.WriteObject(config);
		private const bool LanguageEnglish = false;
		
			}
}
