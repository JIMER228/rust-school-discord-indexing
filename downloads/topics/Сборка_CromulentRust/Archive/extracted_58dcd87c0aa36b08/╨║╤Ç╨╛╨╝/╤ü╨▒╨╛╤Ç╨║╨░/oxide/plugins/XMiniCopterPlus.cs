using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("XMiniCopterPlus", "Monster", "1.0.4")]
    class XMiniCopterPlus : RustPlugin
    {
        protected override void LoadConfig()
        {
            base.LoadConfig();
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            config = Config.ReadObject<CopterConfig>();
        }

        private class CopterConfig
        {		
			
			internal class WorkSetting
            {            			
				[JsonProperty("Включить третье место")]
                public bool Chair = true;				
				[JsonProperty("Включить стеш")]
                public bool Stash = true;
            }			      
			
			public static CopterConfig GetNewConfiguration()
            {
                return new CopterConfig
                {
					Settings = new WorkSetting
					{
						Chair = false,
						Stash = true
					},
					Copter = new CopterSetting
					{
						CopterSkinID = 2199754843,
						CopterName = "КОПТЕР"
					}
				};
			}
			
			[JsonProperty("Настройки коптера")]
            public CopterSetting Copter = new CopterSetting();
            internal class CopterSetting
            {				    
				[JsonProperty("Имя")] 
                public string CopterName;			
				[JsonProperty("SkinID")] 
                public ulong CopterSkinID;				
            }	
			[JsonProperty("Общее")]
            public WorkSetting Settings = new WorkSetting();
        }

        Minicopter.MountPointInfo SpawnPassenger(Minicopter copter)
        {
            return new Minicopter.MountPointInfo
            {
				pos = new Vector3(0f, 0.335f, -1.35f),
                rot = new Vector3(0, 180, 0),
                prefab = copter.mountPoints[1].prefab,
                mountable = copter.mountPoints[1].mountable,
            };
        }
		
        
        		
		private void SpawnEntity(BaseEntity entity)
        {
            if (entity == null) return;
			
            if (entity.skinID == config.Copter.CopterSkinID)
			{
				var copter = GameManager.server.CreateEntity("assets/content/vehicles/minicopter/minicopter.entity.prefab", entity.transform.position, entity.transform.rotation = Quaternion.Euler(new Vector3(0f, 0f, 0f)));
                                
                copter.Spawn(); 						
				NextTick(() => { entity.Kill(); });
			}			
        }
		
        private CopterConfig config;
			
		private void Stash(Minicopter copter)
        {
			foreach (var container in copter.GetComponentsInChildren<StorageContainer>()) {
				if (container.name == "assets/content/vehicles/boats/rowboat/subents/rowboat_storage.prefab") return;
			}	
			
			BaseEntity entity = GameManager.server.CreateEntity("assets/content/vehicles/boats/rowboat/subents/rowboat_storage.prefab") as StorageContainer;

            if (entity == null) return;
			
            entity.transform.localPosition = new Vector3(0f, 0.44f, -0.677f);
			entity.transform.localRotation = Quaternion.Euler(new Vector3(0, 180, 0));

			entity.Spawn();
			entity.SetParent(copter);
        }

        protected override void LoadDefaultConfig()
        {
            config = CopterConfig.GetNewConfiguration();
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            PrintWarning("Создание начальной конфигурации плагина!!!");
        }

        		
				
		[ConsoleCommand("c_give")]
        void cmdConsoleCommand(ConsoleSystem.Arg args)
        {
			if (args.Player() != null) return;
			
			BasePlayer player = BasePlayer.FindByID(ulong.Parse(args.Args[0]));
			
			if (player == null) return;
			
			Item item = ItemManager.CreateByName("woodcross", int.Parse(args.Args[1]), config.Copter.CopterSkinID);
            item.name = config.Copter.CopterName;
			
            player.GiveItem(item);
        }

        		
        		
		private void OnServerInitialized()
		{		
			PrintWarning("\n-----------------------------\n" +
			"     Author - Monster\n" +
			"     VK - vk.com/idannopol\n" +
			"     Discord - Monster#4837\n" +
			//"     Config - v.3939\n" +
			"-----------------------------");
		}
		
		private void OnEntityBuilt(Planner plan, GameObject go)
        {
            SpawnEntity(go.ToBaseEntity());
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private void Chair(Minicopter copter)
        {
            BaseEntity entity = GameManager.server.CreateEntity("assets/prefabs/vehicle/seats/passengerchair.prefab");
			
            if (entity == null) return;
			
            entity.transform.localPosition = new Vector3(0f, 0.4f, -1.0f);
			entity.transform.localRotation = Quaternion.Euler(new Vector3(0, 180, 0));

			entity.Spawn();
			entity.SetParent(copter);
        }            
		
        private void OnEntitySpawned(Minicopter copter)
        {
            if (copter.mountPoints.Count < 3 && copter.ShortPrefabName == "minicopter.entity")
            {
				if (config.Settings.Chair) copter.mountPoints.Add(SpawnPassenger(copter));
				
				timer.Once(0.1f, () => {
                    if (config.Settings.Chair) Chair(copter);
		            if (config.Settings.Stash) Stash(copter);
				});
            }
        }
			
            }
}
