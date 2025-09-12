using System.Text;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System.Collections;
using System;
		   		 		  						  	   		  		 			  		  		  			 		  		 	
namespace Oxide.Plugins
{
    [Info("IQRates", "Mercury", "1.5.16")]
    [Description("Настройка рейтинга на сервере")]
    class IQRates : RustPlugin
    {
        private void StartCargoPlane(Configuration.PluginSettings.OtherSettings.EventSettings EventSettings)
        {
            if (!EventSettings.CargoPlaneSetting.FullOff && EventSettings.CargoPlaneSetting.UseEventCustom)
            {
                Int32 TimeSpawn = EventSettings.CargoPlaneSetting.RandomTimeSpawn.UseRandomTime ? GetRandomTime(EventSettings.CargoPlaneSetting.RandomTimeSpawn.MinEventSpawnTime, EventSettings.CargoPlaneSetting.RandomTimeSpawn.MaxEventSpawnTime) : EventSettings.CargoPlaneSetting.EventSpawnTime;
                timer.Once(TimeSpawn, () =>
                {
                    StartCargoPlane(EventSettings);
                    SpawnPlane();
                });
            }
        }

        void OnAirdrop(CargoPlane plane, Vector3 dropPosition) => plane.OwnerID = 999999999999;
        private void UnSubProSub(int time = 1)
        {
            Unsubscribe("OnEntitySpawned");
            timer.Once(time, () =>
            {
                Subscribe("OnEntitySpawned");
            });
        }
        void AddQuarryPlayer(UInt64 NetID, UInt64 userID)
        {
            if (DataQuarryPlayer.ContainsKey(NetID))
                DataQuarryPlayer[NetID] = userID;
            else DataQuarryPlayer.Add(NetID, userID);
            WriteData();    
        }
        bool IsTime()
        {
            var Settings = config.pluginSettings.OtherSetting;
            float TimeServer = TOD_Sky.Instance.Cycle.Hour;
            return TimeServer < Settings.NightStart && Settings.DayStart <= TimeServer;
        }
        private void OnEntitySpawned(CargoPlane entity)
        {
            NextTick(() =>
            {
                if (entity.OwnerID != 0 || entity.skinID != 0) return;
                var EvenTimer = config.pluginSettings.OtherSetting.EventSetting.CargoPlaneSetting;
                if ((EvenTimer.FullOff || EvenTimer.UseEventCustom))
                    entity.Kill();
            });
        }
        void SpawnCH47()
        {
            UnSubProSub();

            var position = new Vector3(ConVar.Server.worldsize, 100, ConVar.Server.worldsize) - new Vector3(50f, 0f, 50f);
            var entity = GameManager.server.CreateEntity(prefabCH47, position) as CH47HelicopterAIController;
            entity?.TriggeredEventSpawn();
            entity?.Spawn();
        }
		   		 		  						  	   		  		 			  		  		  			 		  		 	
        void API_BONUS_RATE_ADDPLAYER(UInt64 userID, Single Rate)
        {
            BasePlayer player = BasePlayer.FindByID(userID);
            API_BONUS_RATE_ADDPLAYER(player, Rate);
        }
        int API_CONVERT_GATHER(string Shortname, float Amount, BasePlayer player = null) => Converted(Types.Gather, Shortname, Amount, player);
        private const string prefabPlane = "assets/prefabs/npc/cargo plane/cargo_plane.prefab";
        
                

        
        
        private static StringBuilder sb = new StringBuilder();
        
        
        public Dictionary<BasePlayer, Single> BonusRates = new Dictionary<BasePlayer, Single>();
        
                
        void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            foreach(ItemAmount item in collectible.itemList)
                item.amount = Converted(Types.PickUP, item.itemDef.shortname, (Int32)item.amount, player);
        }
		   		 		  						  	   		  		 			  		  		  			 		  		 	
        
                TOD_Time timeComponent = null;
        private object OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            return OvenController.GetOrAdd(oven).Switch(player);
        }

        private void Init() => ReadData();
        
        
        bool IsBlackList(string Shortname)
        {
            var BlackList = config.pluginSettings.RateSetting.BlackList;
            if (BlackList.Contains(Shortname))
                return true;
            else return false;
        }      
        void ReadData() => DataQuarryPlayer = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<UInt64, UInt64>>("IQSystem/IQRates/Quarrys");
        float GetRareCoal(BasePlayer player = null)
        {
            Boolean IsTimes = IsTime();

            var Rates = IsTimes ? config.pluginSettings.RateSetting.DayRates : config.pluginSettings.RateSetting.NightRates;
            var PrivilegyRates = config.pluginSettings.RateSetting.PrivilegyRates;

            if (player != null)
            {
                foreach (var RatesSetting in PrivilegyRates)
                    if (IsPermission(player.UserIDString, RatesSetting.Key))
                        Rates = IsTimes ? RatesSetting.Value.DayRates : RatesSetting.Value.NightRates;
            }
		   		 		  						  	   		  		 			  		  		  			 		  		 	
            float Rare = Rates.CoalRare;
            float RareResult = (100 - Rare) / 100;
            return RareResult;
        }
        void SpawnCargo()
        {
            UnSubProSub();
            
            var cargoShip = GameManager.server.CreateEntity(prefabShip) as CargoShip;
            if (cargoShip == null) return;
            cargoShip.TriggeredEventSpawn();
            cargoShip.SendNetworkUpdate();
            cargoShip.RefreshActiveLayout();
            cargoShip.Spawn();
        }
        public void SendChat(String Message, BasePlayer player, ConVar.Chat.ChatChannel channel = ConVar.Chat.ChatChannel.Global)
        {
            if (IQChat)
                if (config.pluginSettings.ReferenceSettings.IQChatSetting.UIAlertUse)
                    IQChat?.Call("API_ALERT_PLAYER_UI", player, Message);
                else IQChat?.Call("API_ALERT_PLAYER", player, Message, config.pluginSettings.ReferenceSettings.IQChatSetting.CustomPrefix, config.pluginSettings.ReferenceSettings.IQChatSetting.CustomAvatar);
            else player.SendConsoleCommand("chat.add", channel, 0, Message);
        }

        private void SpawnTank()
        {
            UnSubProSub();
            if (!BradleySpawner.singleton.spawned.isSpawned)
                BradleySpawner.singleton?.SpawnBradley();
        }
        private void StartCargoShip(Configuration.PluginSettings.OtherSettings.EventSettings EventSettings)
        {
            if (!EventSettings.CargoShipSetting.FullOff && EventSettings.CargoShipSetting.UseEventCustom)
            {
                Int32 TimeSpawn = EventSettings.CargoShipSetting.RandomTimeSpawn.UseRandomTime ? GetRandomTime(EventSettings.CargoShipSetting.RandomTimeSpawn.MinEventSpawnTime, EventSettings.CargoShipSetting.RandomTimeSpawn.MaxEventSpawnTime) : EventSettings.CargoShipSetting.EventSpawnTime;
                timer.Once(TimeSpawn, () =>
                {
                    StartCargoShip(EventSettings);
                    SpawnCargo();
                });
            }
        }
        private void StartBreadley(Configuration.PluginSettings.OtherSettings.EventSettings EventSettings)
        {
            if (SpacePort == null) return;
            if (!EventSettings.BreadlaySetting.FullOff && EventSettings.BreadlaySetting.UseEventCustom)
            {
                Int32 TimeSpawn = EventSettings.BreadlaySetting.RandomTimeSpawn.UseRandomTime ? GetRandomTime(EventSettings.BreadlaySetting.RandomTimeSpawn.MinEventSpawnTime, EventSettings.BreadlaySetting.RandomTimeSpawn.MaxEventSpawnTime) : EventSettings.BreadlaySetting.EventSpawnTime;
                timer.Once(TimeSpawn, () =>
                {
                    StartBreadley(EventSettings);
                    SpawnTank();
                });
            }
        }
        private void OnEntitySpawned(MiniCopter copter)
        {
            if (copter == null) return;
            FuelSystemRating(copter.GetFuelSystem(), config.pluginSettings.OtherSetting.FuelSetting.AmountMinicopter);
            
            copter.fuelPerSec = config.pluginSettings.OtherSetting.FuelConsumedTransportSetting.ConsumedCopter;
        }

        private Int32 GetRandomTime(Int32 Min, Int32 Max) => UnityEngine.Random.Range(Min, Max);
        
        void OnGrowableGathered(GrowableEntity plant, Item item, BasePlayer player)
        {
            if (item == null || player == null) return;
            item.amount = Converted(Types.Growable, item.info.shortname, item.amount, player);
        }
        private const string prefabShip = "assets/content/vehicles/boats/cargoship/cargoshiptest.prefab";
        
        private void OnEntitySpawned(MotorRowboat boat)
        {
            if (boat == null) return;
            boat.fuelPerSec = config.pluginSettings.OtherSetting.FuelConsumedTransportSetting.ConsumedBoat;
        } 
        
        
        private Single GetSpeedRecycler(BasePlayer player)
        {
            Configuration.PluginSettings.Rates.RecyclerController Recycler = config.pluginSettings.RateSetting.RecyclersController;

            foreach (Configuration.PluginSettings.Rates.RecyclerController.PresetRecycler presetRecycler in Recycler.PrivilageSpeedRecycler)
            {
                if (permission.UserHasPermission(player.UserIDString, presetRecycler.Permissions))
                    return presetRecycler.SpeedRecyclers;
            }
            
            return Recycler.DefaultSpeedRecycler;
        }

        UInt64? GetQuarryPlayer(UInt64 NetID)
        {
            if (!DataQuarryPlayer.ContainsKey(NetID)) return null;
            if (DataQuarryPlayer[NetID] == 0) return null;

            return DataQuarryPlayer[NetID];
        }
        private void OnEntitySpawned(CargoShip entity)
        {
            NextTick(() =>
            {
                if (entity.OwnerID != 0 || entity.skinID != 0) return;
                var EvenTimer = config.pluginSettings.OtherSetting.EventSetting.CargoShipSetting;
                if ((EvenTimer.FullOff || EvenTimer.UseEventCustom))
                    entity.Kill();
            });
        }
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MY_RATES_INFO"] = "Your resource rating at the moment :" +
                "\n- Rating of extracted resources: <color=#FAF0F5>x{0}</color>" +
                "\n- Rating of found items: <color=#FAF0F5>х{1}</color>" +
                "\n- Rating of raised items: <color=#FAF0F5>х{2}</color>" +
                "\n- Career rankings: <color=#FAF0F5>x{3}</color>" +
                "\n- Excavator Rating: <color=#FAF0F5>x{4}</color>" +
                "\n- Rating of growable : <color=#FAF0F5>x{5}</color>",

                ["DAY_RATES_ALERT"] = "The day has come!" +
                "\nThe global rating on the server has been changed :" +
                "\n- Rating of extracted resources: <color=#FAF0F5>x{0}</color>" +
                "\n- Rating of found items: <color=#FAF0F5>х{1}</color>" +
                "\n- Rating of raised items: <color=#FAF0F5>х{2}</color>" +
                "\n- Career rankings: <color=#FAF0F5>x{3}</color>" +
                "\n- Excavator Rating: <color=#FAF0F5>x{4}</color>" +
                "\n- Rating of growable : <color=#FAF0F5>x{5}</color>",

                ["NIGHT_RATES_ALERT"] = "Night came!" +
                "\nThe global rating on the server has been changed :" +
                "\n- Rating of extracted resources: <color=#FAF0F5>x{0}</color>" +
                "\n- Rating of found items: <color=#FAF0F5>х{1}</color>" +
                "\n- Rating of raised items: <color=#FAF0F5>х{2}</color>" +
                "\n- Career rankings: <color=#FAF0F5>x{3}</color>" +
                "\n- Excavator Rating: <color=#FAF0F5>x{4}</color>" +
                "\n- Rating of growable : <color=#FAF0F5>x{5}</color>",


            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MY_RATES_INFO"] = "Ваш рейтинг ресурсов на данный момент :" +
                "\n- Рейтинг добываемых ресурсов: <color=#FAF0F5>x{0}</color>" +
                "\n- Рейтинг найденных предметов: <color=#FAF0F5>х{1}</color>" +
                "\n- Рейтинг поднимаемых предметов: <color=#FAF0F5>х{2}</color>" +
                "\n- Рейтинг карьеров: <color=#FAF0F5>x{3}</color>" +
                "\n- Рейтинг экскаватора: <color=#FAF0F5>x{4}</color>" +
                "\n- Рейтинг грядок : <color=#FAF0F5>x{5}</color>",

                ["DAY_RATES_ALERT"] = "Наступил день!" +
                "\nГлобальный рейтинг на сервере был изменен :" +
                "\n- Рейтинг добываемых ресурсов: <color=#FAF0F5>x{0}</color>" +
                "\n- Рейтинг найденных предметов: <color=#FAF0F5>х{1}</color>" +
                "\n- Рейтинг поднимаемых предметов: <color=#FAF0F5>х{2}</color>" +
                "\n- Рейтинг карьеров: <color=#FAF0F5>x{3}</color>" +
                "\n- Рейтинг экскаватора: <color=#FAF0F5>x{4}</color>" +
                "\n- Рейтинг грядок : <color=#FAF0F5>x{5}</color>", 
                
                ["NIGHT_RATES_ALERT"] = "Наступила ночь!" +
                "\nГлобальный рейтинг на сервере был изменен :" +
                "\n- Рейтинг добываемых ресурсов: <color=#FAF0F5>x{0}</color>" +
                "\n- Рейтинг найденных предметов: <color=#FAF0F5>х{1}</color>" +
                "\n- Рейтинг поднимаемых предметов: <color=#FAF0F5>х{2}</color>" +
                "\n- Рейтинг карьеров: <color=#FAF0F5>x{3}</color>" +
                "\n- Рейтинг экскаватора: <color=#FAF0F5>x{4}</color>" +
                "\n- Рейтинг грядок : <color=#FAF0F5>x{5}</color>",
            }, this, "ru");
        }
        /// <summary>

        /// Обновление 1.5.16
        /// - Добавлена проверка при спавне поездов (до этого вызывало ошибку)
        /// - Изменил метод спавна корабля в

        [PluginReference] Plugin IQChat;
        private void OnEntitySpawned(BradleyAPC entity)
        {
            NextTick(() =>
            {
                if (entity.OwnerID != 0 || entity.skinID != 0) return;
                var EvenTimer = config.pluginSettings.OtherSetting.EventSetting.BreadlaySetting;
                if ((EvenTimer.FullOff || EvenTimer.UseEventCustom))
                    entity.Kill();
            });
        }
        private void OnEntitySpawned(BaseSubmarine submarine)
        {
            if (submarine == null) return;
            FuelSystemRating(submarine.GetFuelSystem(), config.pluginSettings.OtherSetting.FuelSetting.AmountSubmarine);
            
            submarine.maxFuelPerSec = config.pluginSettings.OtherSetting.FuelConsumedTransportSetting.ConsumedSubmarine;
        }
        
        void OnQuarryGather(MiningQuarry quarry, Item item)
        {
            if (item == null || quarry == null) return;
		   		 		  						  	   		  		 			  		  		  			 		  		 	
            // PrintError(GetQuarryPlayer(quarry.net.ID).ToString() + "   " + Converted(Types.Quarry, item.info.shortname,
            //     item.amount, null, GetQuarryPlayer(quarry.OwnerID).ToString()).ToString());
            item.amount = Converted(Types.Quarry, item.info.shortname, item.amount, null, GetQuarryPlayer(quarry.net.ID).ToString());
        }
        private void OnEntitySpawned(CH47Helicopter entity)
        {
            NextTick(() =>
            {
                if (entity.OwnerID != 0 || entity.skinID != 0) return;
                timer.Once(3f, () =>
                {
                    var EvenTimer = config.pluginSettings.OtherSetting.EventSetting.ChinoockSetting;
                    if ((EvenTimer.FullOff || EvenTimer.UseEventCustom) && entity.mountPoints.Where(x => x.mountable.GetMounted() != null && x.mountable.GetMounted().ShortPrefabName.Contains("scientistnpc_heavy")).Count() <= 0)
                        timer.Once(1f, () => { entity.Kill();});
                });
            });
        }   
        
        enum Types
        {
            Gather,
            Loot,
            PickUP,
            Quarry,
            Excavator,
            Growable,
        }
        int API_CONVERT(Types RateType, string Shortname, float Amount, BasePlayer player = null) => Converted(RateType, Shortname, Amount, player);
        bool IsPermission(string userID,string Permission)
        {
            if (permission.UserHasPermission(userID, Permission))
                return true;
            else return false;
        }
        private void OnEntitySpawned(ScrapTransportHelicopter helicopter)
        {
            if (helicopter == null) return;
            FuelSystemRating(helicopter.GetFuelSystem(), config.pluginSettings.OtherSetting.FuelSetting.AmountScrapTransport);
            
            helicopter.fuelPerSec = config.pluginSettings.OtherSetting.FuelConsumedTransportSetting.ConsumedScrapTransport;
        }
        void OnContainerDropItems(ItemContainer container)
        {
            if (container == null) return;
            var Container = container.entityOwner as LootContainer;
            if (Container == null) return;
            uint NetID = Container.net.ID;
            if (LootersListCrateID.Contains(NetID)) return;
            
            BasePlayer player = Container.lastAttacker as BasePlayer;
		   		 		  						  	   		  		 			  		  		  			 		  		 	
            foreach (var item in container.itemList)
                item.amount = Converted(Types.Loot, item.info.shortname, item.amount, player);
        }


        private const Boolean LanguageEn = false;
		   		 		  						  	   		  		 			  		  		  			 		  		 	
        
        
                [ChatCommand("rates")]
        private void GetInfoMyRates(BasePlayer player)
        {
            if (player == null) return;

            var PrivilegyRates = config.pluginSettings.RateSetting.PrivilegyRates;
            Boolean IsTimes = IsTime();
            var Rates = IsTimes ? config.pluginSettings.RateSetting.DayRates : config.pluginSettings.RateSetting.NightRates;
            var CustomRate = IsTimes ? config.pluginSettings.RateSetting.CustomRatesPermissions.DayRates : config.pluginSettings.RateSetting.CustomRatesPermissions.NightRates;

            var Rate = CustomRate.FirstOrDefault(x => IsPermission(player.UserIDString, x.Key)); 

            foreach (var RatesSetting in PrivilegyRates)
                if (IsPermission(player.UserIDString, RatesSetting.Key))
                    Rates = IsTimes ? RatesSetting.Value.DayRates : RatesSetting.Value.NightRates;

            SendChat(GetLang("MY_RATES_INFO", player.UserIDString, Rates.GatherRate, Rates.LootRate, Rates.PickUpRate, Rates.QuarryRate, Rates.ExcavatorRate, Rates.GrowableRate), player);
        }
        protected override void SaveConfig() => Config.WriteObject(config);

        void OnDispenserBonus(ResourceDispenser disp, BasePlayer player, Item item)
        {
            if (item == null || player == null) return;
            
            int Rate = Converted(Types.Gather, item.info.shortname, item.amount, player);
            item.amount = Rate;
        }

        Single GetBonusRate(BasePlayer player)
        {
            Single Bonus = 0;
            if (player == null) return Bonus;

            if (BonusRates.ContainsKey(player))
                Bonus = BonusRates[player];

            return Bonus;
        }

        
                public Single GetMultiplaceBurnableSpeed(String ownerid)
        {
            Single Multiplace = config.pluginSettings.RateSetting.SpeedBurnable;
            if (config.pluginSettings.RateSetting.UseSpeedBurnableList)
            {
                var SpeedInList = config.pluginSettings.RateSetting.SpeedBurableList.OrderByDescending(z => z.SpeedBurnable).FirstOrDefault(x => permission.UserHasPermission(ownerid, x.Permissions));
                if (SpeedInList != null)
                    Multiplace = SpeedInList.SpeedBurnable;
            }
            return Multiplace;
        }

        
        
        
        object OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            BasePlayer player = entity as BasePlayer;
            if (item == null || player == null) return null;

            Int32 Rate = Converted(Types.Gather, item.info.shortname, item.amount, player);
            item.amount = Rate;

            return null;
        }
		   		 		  						  	   		  		 			  		  		  			 		  		 	
        
                private void Unload()
        {
            OvenController.KillAll();
            if (timeComponent == null) return;
            timeComponent.OnSunrise -= OnSunrise;
            timeComponent.OnSunset -= OnSunset;
            timeComponent.OnHour -= OnHour;
		   		 		  						  	   		  		 			  		  		  			 		  		 	
            if (initializeTransport != null)
            {
                ServerMgr.Instance.StopCoroutine(initializeTransport);
                initializeTransport = null;
            }
        }
        Boolean activatedDay;

        
                public void Register(string Permissions)
        {
            if (!String.IsNullOrWhiteSpace(Permissions))
                if (!permission.PermissionExists(Permissions, this))
                    permission.RegisterPermission(Permissions, this);
        }

        void SpawnHeli()
        {
            UnSubProSub();

            var position = new Vector3(ConVar.Server.worldsize, 100, ConVar.Server.worldsize) - new Vector3(50f, 0f, 50f);
            var entity = GameManager.server.CreateEntity(prefabPatrol, position);
            entity?.Spawn();
        }
        void WriteData() => Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/IQRates/Quarrys", DataQuarryPlayer);

        void OnSunrise()
        {
            timeComponent.DayLengthInMinutes = config.pluginSettings.OtherSetting.DayTime * (24.0f / (TOD_Sky.Instance.SunsetTime - TOD_Sky.Instance.SunriseTime));
            activatedDay = true;
            if (config.pluginSettings.OtherSetting.UseSkipTime)
            {
                if (config.pluginSettings.OtherSetting.TypeSkipped == SkipType.Day)
                    TOD_Sky.Instance.Cycle.Hour = config.pluginSettings.OtherSetting.NightStart;
                else
                {
                    if (config.pluginSettings.OtherSetting.UseAlertDayNight)
                    {
                        Configuration.PluginSettings.Rates.AllRates Rate = config.pluginSettings.RateSetting.DayRates;
                        foreach (BasePlayer player in BasePlayer.activePlayerList)
                            SendChat(GetLang("DAY_RATES_ALERT", player.UserIDString, Rate.GatherRate, Rate.LootRate, Rate.PickUpRate, Rate.QuarryRate, Rate.ExcavatorRate, Rate.GrowableRate), player); 
                    }
                }
                return;
            }
            if (config.pluginSettings.OtherSetting.UseAlertDayNight)
            {
                Configuration.PluginSettings.Rates.AllRates Rate = config.pluginSettings.RateSetting.DayRates;
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    SendChat(GetLang("DAY_RATES_ALERT", player.UserIDString, Rate.GatherRate, Rate.LootRate, Rate.PickUpRate, Rate.QuarryRate, Rate.ExcavatorRate, Rate.GrowableRate), player);
            }
        }
        private const string prefabPatrol = "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab";

        void OnHour()
        {
            if (TOD_Sky.Instance.Cycle.Hour > TOD_Sky.Instance.SunriseTime && TOD_Sky.Instance.Cycle.Hour < TOD_Sky.Instance.SunsetTime && TOD_Sky.Instance.Cycle.Hour >= config.pluginSettings.OtherSetting.DayStart && !activatedDay)
            {
                OnSunrise();
                return;
            }
            if ((TOD_Sky.Instance.Cycle.Hour > TOD_Sky.Instance.SunsetTime || TOD_Sky.Instance.Cycle.Hour < TOD_Sky.Instance.SunriseTime) && TOD_Sky.Instance.Cycle.Hour >= config.pluginSettings.OtherSetting.NightStart && activatedDay)
            {
                OnSunset();
                return;
            }
        }

        private void OnEntitySpawned(BaseHelicopter entity)
        {
            NextTick(() =>
            {
                if (entity.OwnerID != 0 || entity.skinID != 0) return;
                var EvenTimer = config.pluginSettings.OtherSetting.EventSetting.HelicopterSetting;
                if ((EvenTimer.FullOff || EvenTimer.UseEventCustom))
                    entity.Kill();
            });
        }
        
                private BasePlayer ExcavatorPlayer = null;
        private void OnEntitySpawned(HotAirBalloon hotAirBalloon)
        {
            if (hotAirBalloon == null) return;
            hotAirBalloon.fuelPerSec = config.pluginSettings.OtherSetting.FuelConsumedTransportSetting.ConsumedHotAirBalloon;
        }
        
        
        Single GetRateQuarryDetalis(Configuration.PluginSettings.Rates.AllRates Rates, String Shortname)
        {
            Single Rate = Rates.QuarryRate;

            if (!Rates.QuarryDetalis.UseDetalisRateQuarry) return Rate;
            return Rates.QuarryDetalis.ShortnameListQuarry.ContainsKey(Shortname) ? Rates.QuarryDetalis.ShortnameListQuarry[Shortname] : Rate;
        }
        
        
        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (entity == null) return;

            if(config.pluginSettings.RateSetting.UseBlackListPrefabs)
                if (config.pluginSettings.RateSetting.BlackListPrefabs.Contains(entity.ShortPrefabName))
                    return;
            
            LootContainer container = entity as LootContainer;

            if (entity.net == null) return;
            UInt64 NetID = entity.net.ID;
            if (LootersListCrateID.Contains(NetID)) return;

            if (container == null)
            {
                if (!(entity is NPCPlayerCorpse)) return;
                
                NPCPlayerCorpse corpse = (NPCPlayerCorpse)entity;
                foreach (ItemContainer corpseContainer in corpse.containers)
                {
                    foreach (Item item in corpseContainer.itemList)
                        item.amount = Converted(Types.Loot, item.info.shortname, item.amount, player);
                }
            }
            else
            {
                foreach (Item item in container.inventory.itemList)
                    item.amount = Converted(Types.Loot, item.info.shortname, item.amount, player);
            }
            
            LootersListCrateID.Add(NetID);
        }

        
                private void FuelSystemRating(EntityFuelSystem FuelSystem, Int32 Amount)
        {
            if (FuelSystem == null) return;
            NextTick(() =>
            {
                Item Fuel = FuelSystem.GetFuelItem();
                if (Fuel == null) return;
		   		 		  						  	   		  		 			  		  		  			 		  		 	
                if (Fuel.amount == 50 || Fuel.amount == 100)
                    Fuel.amount = Amount;
            });
        }

        private class OvenController : FacepunchBehaviour
        {
            private static readonly Dictionary<BaseOven, OvenController> Controllers = new Dictionary<BaseOven, OvenController>();
            private BaseOven _oven;
            private float _speed;
            private string _ownerId;
            private Int32 _ticks;
            private Int32 _speedFuel;
            
            private bool IsFurnace => (int)_oven.temperature >= 2;

            private void Awake()
            {
                _oven = (BaseOven)gameObject.ToBaseEntity();
                _ownerId = _oven.OwnerID.ToString();
            }

            public object Switch(BasePlayer player)
            {
                if (!IsFurnace || _oven.needsBuildingPrivilegeToUse && !player.CanBuild())
                    return null;

                if (_oven.IsOn())
                    StopCooking();
                else
                {
                    _ownerId = _oven.OwnerID != 0 ? _oven.OwnerID.ToString() : player.UserIDString;
                    StartCooking();
                }
                return false;
            }
		   		 		  						  	   		  		 			  		  		  			 		  		 	
            public void TryRestart()
            {
                if (!_oven.IsOn())
                    return;
                _oven.CancelInvoke(_oven.Cook);
                StopCooking();
                StartCooking();
            }
            private void Kill()
            {
                if (_oven.IsOn())
                {
                    StopCooking();
                    _oven.StartCooking();
                }
                Destroy(this);
            }

            
            public static OvenController GetOrAdd(BaseOven oven)
            {
                OvenController controller;
                if (Controllers.TryGetValue(oven, out controller))
                    return controller;
                controller = oven.gameObject.AddComponent<OvenController>();
                Controllers[oven] = controller;
                return controller;
            }

            public static void TryRestartAll()
            {
                foreach (var pair in Controllers)
                {
                    pair.Value.TryRestart();
                }
            }
            public static void KillAll()
            {
                foreach (var pair in Controllers)
                {
                    pair.Value.Kill();
                }
                Controllers.Clear();
            }
            public void OnDestroy()
            {
                Destroy(this);
            }
                        
            private void StartCooking()
            {
                if (_oven.FindBurnable() == null)
                    return;
                Single Multiplace = _.GetMultiplaceBurnableSpeed(_ownerId);
                _speed = (Single)(0.1f / Multiplace); // 0.5 * M
                Int32 MultiplaceFuel = _.GetMultiplaceBurnableFuelSpeed(_ownerId);
                _speedFuel = MultiplaceFuel;
                
                StopCooking();
                
                _oven.inventory.temperature = _oven.cookingTemperature;
                _oven.UpdateAttachmentTemperature();

                InvokeRepeating(Cook, _speed, _speed);
                _oven.SetFlag(BaseEntity.Flags.On, true);

            }
            
            private void StopCooking()
            {
                CancelInvoke(Cook);
                _oven.StopCooking();
                _oven.SetFlag(BaseEntity.Flags.On, false);
                _oven.SendNetworkUpdate();
                
                if (_oven.inventory != null)
                {
                    foreach (Item item in _oven.inventory.itemList)
                    {
                        if (item.HasFlag(global::Item.Flag.Cooking))
                            item.SetFlag(global::Item.Flag.Cooking, false);
                        item.MarkDirty();
                    }
                }
            }
            
            public void Cook()
            {
                if (!_oven.HasFlag(BaseEntity.Flags.On))
                {
                    StopCooking();
                    return;
                }
                
                Item item = _oven.FindBurnable();
                if (item == null)
                {
                    StopCooking();
                    return;
                }
                _oven.Cook();

                SmeltItems();
                
                var component = item.info.GetComponent<ItemModBurnable>();
                item.fuel -= 0.5f * (_oven.cookingTemperature / 200f) * _speedFuel; 

                if (!item.HasFlag(global::Item.Flag.OnFire))
                {
                    item.SetFlag(global::Item.Flag.OnFire, true);
                    item.MarkDirty();
                }
                

                if (item.fuel <= 0f)
                {
                    _oven.ConsumeFuel(item, component);
                }

                _ticks++;
            }
            private void SmeltItems()
            {
                if (_ticks % 1 != 0)
                    return;

                for (var i = 0; i < _oven.inventory.itemList.Count; i++)
                {
                    var item = _oven.inventory.itemList[i];
                    if (item == null || !item.IsValid() || item.info == null || _.IsBlackListBurnable(item.info.shortname))
                        continue;
		   		 		  						  	   		  		 			  		  		  			 		  		 	
                    var cookable = item.info.GetComponent<ItemModCookable>();
                    if (cookable == null)
                        continue;

                    Single temperature = item.temperature;
                    if ((temperature < cookable.lowTemp || temperature > cookable.highTemp)) 
                        {
                            if (!cookable.setCookingFlag || !item.HasFlag(global::Item.Flag.Cooking)) continue;
                            item.SetFlag(global::Item.Flag.Cooking, false);
                            item.MarkDirty();
                            continue;
                        }
                    
                    if (!cookable.CanBeCookedByAtTemperature(temperature))
                    {
                        if (!cookable.setCookingFlag || !item.HasFlag(global::Item.Flag.Cooking))
                            continue;

                        item.SetFlag(global::Item.Flag.Cooking, false);
                        item.MarkDirty();
                        continue;
                    }

                    if (cookable.cookTime > 0 && _ticks * 1f / 1 % cookable.cookTime > 0)
                        continue;
		   		 		  						  	   		  		 			  		  		  			 		  		 	
                    if (cookable.setCookingFlag && !item.HasFlag(global::Item.Flag.Cooking))
                    {
                        item.SetFlag(global::Item.Flag.Cooking, true);
                        item.MarkDirty();
                    }

                    var position = item.position;
                    if (item.amount > 1)
                    {
                        item.amount--;
                        item.MarkDirty();
                    }
                    else
                    {
                        item.Remove();
                    }

                    if (cookable.becomeOnCooked == null) continue;

                    var item2 = ItemManager.Create(cookable.becomeOnCooked,
                        (int)(cookable.amountOfBecome * 1f));
		   		 		  						  	   		  		 			  		  		  			 		  		 	
                    if (item2 == null || item2.MoveToContainer(item.parent, position) ||
                        item2.MoveToContainer(item.parent))
                        continue;

                    item2.Drop(item.parent.dropPosition, item.parent.dropVelocity);
                    //if (!item.parent.entityOwner) continue;
                    StopCooking();
                }
            }
        }
        public List<UInt64> LootersListCrateID = new List<UInt64>();
        
        int Converted(Types RateType, string Shortname, float Amount, BasePlayer player = null, String UserID = null)
        {
            float ConvertedAmount = Amount;
            if (IsBlackList(Shortname)) return Convert.ToInt32(ConvertedAmount);
            
            var PrivilegyRates = config.pluginSettings.RateSetting.PrivilegyRates;
            Boolean IsTimes = IsTime();
            Configuration.PluginSettings.Rates.AllRates Rates = IsTimes ? config.pluginSettings.RateSetting.DayRates : config.pluginSettings.RateSetting.NightRates;
            
            UserID = player != null ? player.UserIDString : UserID;

            if (UserID != null)
            {
                var CustomRate = IsTimes ? config.pluginSettings.RateSetting.CustomRatesPermissions.DayRates : config.pluginSettings.RateSetting.CustomRatesPermissions.NightRates;

                var Rate = CustomRate.FirstOrDefault(x => IsPermission(UserID, x.Key)); //dbg
                if (Rate.Value != null)
                    foreach (var RateValue in Rate.Value.Where(x => x.Shortname == Shortname))
                    {
                        ConvertedAmount = Amount * RateValue.Rate;
                        return (int)ConvertedAmount;
                    }

                foreach (var RatesSetting in PrivilegyRates)
                    if (IsPermission(UserID, RatesSetting.Key))
                        Rates = IsTimes ? RatesSetting.Value.DayRates : RatesSetting.Value.NightRates;
            }

            Single BonusRate = GetBonusRate(player); 

            switch (RateType)
            {
                case Types.Gather:
                    {
                        ConvertedAmount = Amount * (Rates.GatherRate + BonusRate);
                        break;
                    }
                case Types.Loot:
                    {
                        ConvertedAmount = Amount * (Rates.LootRate + BonusRate);
                        break;
                    }
                case Types.PickUP:
                    {
                        ConvertedAmount = Amount * (Rates.PickUpRate + BonusRate);
                        break;
                    }
                case Types.Growable:
                    {
                        ConvertedAmount = Amount * (Rates.GrowableRate + BonusRate);
                        break;
                    }
                case Types.Quarry:
                {
                    Single QuarryRates = GetRateQuarryDetalis(Rates, Shortname);
                    ConvertedAmount = Amount * (QuarryRates  + BonusRate); //Rates.QuarryRate;
                    break;
                }
                case Types.Excavator:
                    {
                        ConvertedAmount = Amount * (Rates.ExcavatorRate + BonusRate);
                        break;
                    }
            }
            return Convert.ToInt32(ConvertedAmount);
        }
        private void OnServerInitialized()
        {
            _ = this;
 
            SpacePort = TerrainMeta.Path.Monuments.FirstOrDefault(p => p.name.ToLower().Contains("launch_site"));

            StartEvent();
            foreach (var RateCustom in config.pluginSettings.RateSetting.PrivilegyRates)
                Register(RateCustom.Key);

            foreach (Configuration.PluginSettings.Rates.RecyclerController.PresetRecycler presetRecycler in config.pluginSettings.RateSetting.RecyclersController.PrivilageSpeedRecycler)
                    Register(presetRecycler.Permissions);
            
            if (config.pluginSettings.RateSetting.UseSpeedBurnableList)
                foreach (var BurnableList in config.pluginSettings.RateSetting.SpeedBurableList)
                    Register(BurnableList.Permissions);

            List<String> PrivilegyCustomRatePermissions = config.pluginSettings.RateSetting.CustomRatesPermissions.NightRates.Keys.Union(config.pluginSettings.RateSetting.CustomRatesPermissions.DayRates.Keys).ToList();
            foreach (var RateItemCustom in PrivilegyCustomRatePermissions)
                Register(RateItemCustom);

            timer.Once(5, GetTimeComponent);
            
            if(config.pluginSettings.RateSetting.UseSpeedBurnable)
             foreach (BaseOven oven in BaseNetworkable.serverEntities.OfType<BaseOven>())
                OvenController.GetOrAdd(oven).TryRestart();

            if (!config.pluginSettings.RateSetting.UseSpeedBurnable)
                Unsubscribe(nameof(OnOvenToggle));
            
            if(!config.pluginSettings.RateSetting.RecyclersController.UseRecyclerSpeed)
                Unsubscribe(nameof(OnRecyclerToggle));

            initializeTransport = ServerMgr.Instance.StartCoroutine(InitializeTransport());
        }

                private const string prefabCH47 = "assets/prefabs/npc/ch47/ch47scientists.entity.prefab";
        private void OnEntitySpawned(TrainCar trainCar)
        {
            if (trainCar == null) return;
            StorageContainer fuelContainer = trainCar.GetFuelSystem()?.GetFuelContainer();
            if (fuelContainer == null) return;
            TrainEngine trainEngine = fuelContainer.GetComponent<TrainEngine>();
            if (trainEngine == null) return;
            trainEngine.maxFuelPerSec = config.pluginSettings.OtherSetting.FuelConsumedTransportSetting.ConsumedTrain;
        } 

        private void GetTimeComponent()
        {
            timeComponent = TOD_Sky.Instance.Components.Time;
            if (timeComponent == null) return;
            SetTimeComponent();
            StartupFreeze();
        }
        void SpawnPlane()
        {
            UnSubProSub();

            var position = new Vector3(ConVar.Server.worldsize, 100, ConVar.Server.worldsize) - new Vector3(50f, 0f, 50f);
            var entity = GameManager.server.CreateEntity(prefabPlane, position);
            entity?.Spawn();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();

                if (config.pluginSettings.RateSetting.RecyclersController.DefaultSpeedRecycler == 0f)
                    config.pluginSettings.RateSetting.RecyclersController.DefaultSpeedRecycler = 5f;
                if (config.pluginSettings.RateSetting.BlackListPrefabs == null ||
                    config.pluginSettings.RateSetting.BlackListPrefabs.Count == 0)
                    config.pluginSettings.RateSetting.BlackListPrefabs = new List<String>()
                    {
                        "crate_elite",
                        "crate_normal"
                    };
                    
                if (config.pluginSettings.RateSetting.RecyclersController.PrivilageSpeedRecycler == null ||
                    config.pluginSettings.RateSetting.RecyclersController.PrivilageSpeedRecycler.Count == 0)
                {
                    config.pluginSettings.RateSetting.RecyclersController.PrivilageSpeedRecycler =
                        new List<Configuration.PluginSettings.Rates.RecyclerController.PresetRecycler>()
                        {
                            new Configuration.PluginSettings.Rates.RecyclerController.PresetRecycler()
                            {
                                Permissions = "iqrates.recyclerhyperfast",
                                SpeedRecyclers = 0
                            },
                            new Configuration.PluginSettings.Rates.RecyclerController.PresetRecycler()
                            {
                                Permissions = "iqrates.recyclerfast",
                                SpeedRecyclers = 3
                            },
                        };
                }

                if (config.pluginSettings.RateSetting.DayRates.QuarryDetalis.ShortnameListQuarry.Count == 0)
                    config.pluginSettings.RateSetting.DayRates.QuarryDetalis =
                        new Configuration.PluginSettings.Rates.AllRates.QuarryRateDetalis()
                        {
                            UseDetalisRateQuarry = false,
                            ShortnameListQuarry = new Dictionary<String, Single>()
                            {
                                ["metal.ore"] = 10,
                                ["sulfur.ore"] = 5
                            }
                        };
                
                if (config.pluginSettings.RateSetting.NightRates.QuarryDetalis.ShortnameListQuarry.Count == 0)
                    config.pluginSettings.RateSetting.NightRates.QuarryDetalis =
                        new Configuration.PluginSettings.Rates.AllRates.QuarryRateDetalis()
                        {
                            UseDetalisRateQuarry = false,
                            ShortnameListQuarry = new Dictionary<String, Single>()
                            {
                                ["metal.ore"] = 10,
                                ["sulfur.ore"] = 5
                            }
                        };
            }
            catch
            {                       
                PrintWarning(LanguageEn ? "Error #35853343" + $"read configuration 'oxide/config/{Name}', create a new configuration!!" : "Ошибка #3585343" + $"чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!"); //#333
                LoadDefaultConfig();
            }
            NextTick(SaveConfig);
        }
        private object OnExcavatorGather(ExcavatorArm arm, Item item)
        {
            if (arm == null) return null;
            if (item == null) return null;
            item.amount = Converted(Types.Excavator, item.info.shortname, item.amount, ExcavatorPlayer);
            return null;
        }
        private void OnEntitySpawned(Snowmobile snowmobiles)
        {
            if (snowmobiles == null) return;
            snowmobiles.maxFuelPerSec = config.pluginSettings.OtherSetting.FuelConsumedTransportSetting.ConsumedSnowmobile;
        } 
        
        
        void OnFuelConsume(BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
            if (oven == null) return;
            
            burnable.byproductChance = GetRareCoal(BasePlayer.FindByID(oven.OwnerID));
            if (burnable.byproductChance == 0)
                burnable.byproductChance = -1;
        }
        public static IQRates _;
        void OnExcavatorResourceSet(ExcavatorArm arm, string resourceName, BasePlayer player)
        {
            if (arm == null || player == null) return;
            ExcavatorPlayer = player;
        }
        private void StartHelicopter(Configuration.PluginSettings.OtherSettings.EventSettings EventSettings)
        {
            if (!EventSettings.HelicopterSetting.FullOff && EventSettings.HelicopterSetting.UseEventCustom)
            {
                Int32 TimeSpawn = EventSettings.HelicopterSetting.RandomTimeSpawn.UseRandomTime ? GetRandomTime(EventSettings.HelicopterSetting.RandomTimeSpawn.MinEventSpawnTime, EventSettings.HelicopterSetting.RandomTimeSpawn.MaxEventSpawnTime) : EventSettings.HelicopterSetting.EventSpawnTime;
                timer.Once(TimeSpawn, () => 
                {
                    StartHelicopter(EventSettings);
                    SpawnHeli();
                });
            }
        }
        private void OnEntitySpawned(RHIB boat)
        {
            if (boat == null) return;
            boat.fuelPerSec = config.pluginSettings.OtherSetting.FuelConsumedTransportSetting.ConsumedBoat;
        }    
        private void StartChinoock(Configuration.PluginSettings.OtherSettings.EventSettings EventSettings)
        {
            if (!EventSettings.ChinoockSetting.FullOff && EventSettings.ChinoockSetting.UseEventCustom)
            {
                Int32 TimeSpawn = EventSettings.ChinoockSetting.RandomTimeSpawn.UseRandomTime ? GetRandomTime(EventSettings.ChinoockSetting.RandomTimeSpawn.MinEventSpawnTime, EventSettings.ChinoockSetting.RandomTimeSpawn.MaxEventSpawnTime) : EventSettings.ChinoockSetting.EventSpawnTime;
                timer.Once(TimeSpawn, () =>
                {
                    StartChinoock(EventSettings);
                    SpawnCH47();
                });
            }
        }
        void StartupFreeze()
        {
            if (!config.pluginSettings.OtherSetting.UseFreezeTime) return;
            timeComponent.ProgressTime = false;
            ConVar.Env.time = config.pluginSettings.OtherSetting.FreezeTime;
        }
        
                
                public Dictionary<UInt64, UInt64> DataQuarryPlayer = new Dictionary<UInt64, UInt64>();

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        void StartEvent()
        {
            var EventSettings = config.pluginSettings.OtherSetting.EventSetting;
            StartCargoShip(EventSettings);
            StartCargoPlane(EventSettings);
            StartBreadley(EventSettings);
            StartChinoock(EventSettings);
            StartHelicopter(EventSettings);
        }
        
        private void OnEntitySpawned(SupplySignal entity) => UnSubProSub(10);
        
        
        
        private IEnumerator InitializeTransport()
        {
            foreach (BaseNetworkable entity in BaseNetworkable.serverEntities.Where(e => e is BaseVehicle)) 
            {
                if (entity is MotorRowboat)
                    OnEntitySpawned((MotorRowboat)entity);
                if(entity is Snowmobile)
                    OnEntitySpawned((Snowmobile)entity);
                if(entity is HotAirBalloon)
                    OnEntitySpawned((HotAirBalloon)entity);
                if(entity is RHIB)
                    OnEntitySpawned((RHIB)entity);
                if(entity is BaseSubmarine)
                    OnEntitySpawned((BaseSubmarine)entity);
                if(entity is MiniCopter)
                    OnEntitySpawned((MiniCopter)entity);
                if(entity is ScrapTransportHelicopter)
                    OnEntitySpawned((ScrapTransportHelicopter)entity);
                
                yield return CoroutineEx.waitForSeconds(0.03f); 
            }
        }
        private class Configuration
        {

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    pluginSettings = new PluginSettings
                    {
                        ReferenceSettings = new PluginSettings.ReferencePlugin
                        {
                            IQChatSetting = new PluginSettings.ReferencePlugin.IQChatReference
                            {
                                CustomAvatar = "0",
                                CustomPrefix = "[IQRates]",
                                UIAlertUse = false,
                            },
                        },
                        RateSetting = new PluginSettings.Rates
                        {
                            UseBlackListPrefabs = false,
                            BlackListPrefabs = new List<String>()
                            {
                                "crate_elite",
                                "crate_normal"
                            },
                            UseSpeedBurnable = true,
                            SpeedBurnable = 3.5f,
                            SpeedFuelBurnable = 1,
                            BlackListBurnable = new List<String>
                            {
                                "wolfmeat.cooked",
                                "deermeat.cooked",
                                "meat.pork.cooked",
                                "humanmeat.cooked",
                                "chicken.cooked",
                                "bearmeat.cooked",
                                "horsemeat.cooked",
                            },
                            RecyclersController = new PluginSettings.Rates.RecyclerController
                            {
                                UseRecyclerSpeed = false,
                                DefaultSpeedRecycler = 5,
                                PrivilageSpeedRecycler = new List<PluginSettings.Rates.RecyclerController.PresetRecycler>
                                {
                                    new PluginSettings.Rates.RecyclerController.PresetRecycler 
                                    {
                                        Permissions = "iqrates.recyclerhyperfast",
                                        SpeedRecyclers = 0 
                                    },
                                   new PluginSettings.Rates.RecyclerController.PresetRecycler
                                   {
                                       Permissions = "iqrates.recyclerfast",
                                       SpeedRecyclers = 3
                                   },
                                },
                            },
                            UseSpeedBurnableList = true,
                            SpeedBurableList = new List<PluginSettings.Rates.SpeedBurnablePreset>
                            {
                                new PluginSettings.Rates.SpeedBurnablePreset
                                {
                                    Permissions = "iqrates.vip",
                                    SpeedBurnable = 5.0f,
                                    SpeedFuelBurnable = 20,
                                },
                                new PluginSettings.Rates.SpeedBurnablePreset
                                {
                                    Permissions = "iqrates.speedrun",
                                    SpeedBurnable = 55.0f,
                                    SpeedFuelBurnable = 20,
                                },
                                new PluginSettings.Rates.SpeedBurnablePreset
                                {
                                    Permissions = "iqrates.fuck",
                                    SpeedBurnable = 200f,
                                    SpeedFuelBurnable = 20,
                                },
                            },
                            DayRates = new PluginSettings.Rates.AllRates
                            {
                                GatherRate = 1.0f,
                                LootRate = 1.0f,
                                PickUpRate = 1.0f,
                                GrowableRate = 1.0f,
                                QuarryRate = 1.0f,
                                QuarryDetalis = new PluginSettings.Rates.AllRates.QuarryRateDetalis
                                {
                                    UseDetalisRateQuarry = false,
                                    ShortnameListQuarry = new Dictionary<String, Single>()
                                    {
                                        ["metal.ore"] = 10,
                                        ["sulfur.ore"] = 5
                                    }
                                },
                                ExcavatorRate = 1.0f,
                                CoalRare = 10,
                            },
                            NightRates = new PluginSettings.Rates.AllRates
                            {
                                GatherRate = 2.0f,
                                LootRate = 2.0f,
                                PickUpRate = 2.0f,
                                GrowableRate = 2.0f,
                                QuarryRate = 2.0f,
                                QuarryDetalis = new PluginSettings.Rates.AllRates.QuarryRateDetalis
                                {
                                    UseDetalisRateQuarry = false,
                                    ShortnameListQuarry = new Dictionary<String, Single>()
                                    {
                                        ["metal.ore"] = 10,
                                        ["sulfur.ore"] = 5
                                    }
                                },
                                ExcavatorRate = 2.0f,
                                CoalRare = 15,
                            },
                            CustomRatesPermissions = new PluginSettings.Rates.PermissionsRate
                            {
                                DayRates = new Dictionary<String, List<PluginSettings.Rates.PermissionsRate.PermissionsRateDetalis>>
                                {
                                    ["iqrates.gg"] = new List<PluginSettings.Rates.PermissionsRate.PermissionsRateDetalis>
                                    {
                                        new PluginSettings.Rates.PermissionsRate.PermissionsRateDetalis
                                        {
                                            Rate = 200.0f,
                                            Shortname = "wood",
                                        },
                                        new PluginSettings.Rates.PermissionsRate.PermissionsRateDetalis
                                        {
                                              Rate = 200.0f,
                                              Shortname = "stones",
                                        }
                                    }
                                },
                                NightRates = new Dictionary<string, List<PluginSettings.Rates.PermissionsRate.PermissionsRateDetalis>>
                                {
                                    ["iqrates.gg"] = new List<PluginSettings.Rates.PermissionsRate.PermissionsRateDetalis>
                                    {
                                        new PluginSettings.Rates.PermissionsRate.PermissionsRateDetalis
                                        {
                                            Rate = 400.0f,
                                            Shortname = "wood",
                                        },
                                        new PluginSettings.Rates.PermissionsRate.PermissionsRateDetalis
                                        {
                                              Rate = 400.0f,
                                              Shortname = "stones",
                                        }
                                    }
                                },
                            },
                            PrivilegyRates = new Dictionary<string, PluginSettings.Rates.DayAnNightRate>
                            {
                                ["iqrates.vip"] = new PluginSettings.Rates.DayAnNightRate
                                {
                                    DayRates =
                                    {
                                        GatherRate = 3.0f,
                                        LootRate = 3.0f,
                                        PickUpRate = 3.0f,
                                        QuarryRate = 3.0f,
                                        QuarryDetalis = new PluginSettings.Rates.AllRates.QuarryRateDetalis
                                        {
                                            UseDetalisRateQuarry = false,
                                            ShortnameListQuarry = new Dictionary<String, Single>()
                                            {
                                                ["metal.ore"] = 10,
                                                ["sulfur.ore"] = 5
                                            }
                                        },
                                        GrowableRate = 3.0f,
                                        ExcavatorRate = 3.0f,
                                        CoalRare = 15,
                                    },
                                    NightRates = new PluginSettings.Rates.AllRates
                                    {
                                        GatherRate = 13.0f,
                                        LootRate = 13.0f,
                                        PickUpRate = 13.0f,
                                        GrowableRate = 13.0f,
                                        QuarryRate = 13.0f,
                                        QuarryDetalis = new PluginSettings.Rates.AllRates.QuarryRateDetalis
                                        {
                                            UseDetalisRateQuarry = false,
                                            ShortnameListQuarry = new Dictionary<String, Single>()
                                            {
                                                ["metal.ore"] = 10,
                                                ["sulfur.ore"] = 5
                                            }
                                        },
                                        ExcavatorRate = 13.0f,
                                        CoalRare = 25,
                                    }
                                },
                                ["iqrates.premium"] = new PluginSettings.Rates.DayAnNightRate
                                {
                                    DayRates =
                                    {
                                        GatherRate = 3.5f,
                                        LootRate = 3.5f,
                                        PickUpRate = 3.5f,
                                        GrowableRate = 3.5f,
                                        QuarryRate = 3.5f,
                                        QuarryDetalis = new PluginSettings.Rates.AllRates.QuarryRateDetalis
                                        {
                                            UseDetalisRateQuarry = false,
                                            ShortnameListQuarry = new Dictionary<String, Single>()
                                            {
                                                ["metal.ore"] = 10,
                                                ["sulfur.ore"] = 5
                                            }
                                        },
                                        ExcavatorRate = 3.5f,
                                        CoalRare = 20,
                                    },
                                    NightRates = new PluginSettings.Rates.AllRates
                                    {
                                        GatherRate = 13.5f,
                                        LootRate = 13.5f,
                                        PickUpRate = 13.5f,
                                        GrowableRate = 13.5f,
                                        QuarryRate = 13.5f,
                                        QuarryDetalis = new PluginSettings.Rates.AllRates.QuarryRateDetalis
                                        {
                                            UseDetalisRateQuarry = false,
                                            ShortnameListQuarry = new Dictionary<String, Single>()
                                            {
                                                ["metal.ore"] = 10,
                                                ["sulfur.ore"] = 5
                                            }
                                        },
                                        ExcavatorRate = 13.5f,
                                        CoalRare = 20,
                                    }
                                },
                            },
                            BlackList = new List<String>
                            {
                                "sulfur.ore",
                            },
                        },
                        OtherSetting = new PluginSettings.OtherSettings
                        {
                            UseAlertDayNight = true,
                            UseSkipTime = true,
                            TypeSkipped = SkipType.Night,
                            UseTime = false,
                            FreezeTime = 12,
                            UseFreezeTime = true,
                            DayStart = 10,
                            NightStart = 22,
                            DayTime = 5,
                            NightTime = 1,
                            FuelConsumedTransportSetting = new PluginSettings.OtherSettings.FuelConsumedTransport
                            {
                                ConsumedHotAirBalloon = 0.25f,
                                ConsumedSnowmobile = 0.15f,
                                ConsumedTrain = 0.15f,
                                ConsumedBoat = 0.25f,
                                ConsumedSubmarine = 0.15f,
                                ConsumedCopter = 0.25f,
                                ConsumedScrapTransport = 0.25f,
                            },
                            FuelSetting = new PluginSettings.OtherSettings.FuelSettings
                            {
                                AmountBoat = 200,
                                AmountMinicopter = 200,
                                AmountScrapTransport = 200,
                                AmountSubmarine = 200
                            },
                            EventSetting = new PluginSettings.OtherSettings.EventSettings
                            {
                                BreadlaySetting = new PluginSettings.OtherSettings.EventSettings.Setting
                                {
                                    FullOff = false,
                                    UseEventCustom = true,
                                    EventSpawnTime = 3000,
                                    RandomTimeSpawn = new PluginSettings.OtherSettings.EventSettings.Setting.RandomingTime
                                    {
                                        UseRandomTime = false,
                                        MaxEventSpawnTime = 3000,
                                        MinEventSpawnTime = 1000,
                                    },
                                },
                                CargoPlaneSetting = new PluginSettings.OtherSettings.EventSettings.Setting
                                {
                                    FullOff = false,
                                    UseEventCustom = true,
                                    EventSpawnTime = 5000,
                                    RandomTimeSpawn = new PluginSettings.OtherSettings.EventSettings.Setting.RandomingTime
                                    {
                                        UseRandomTime = false,
                                        MaxEventSpawnTime = 3000,
                                        MinEventSpawnTime = 1000,
                                    },
                                },
                                CargoShipSetting = new PluginSettings.OtherSettings.EventSettings.Setting
                                {
                                    FullOff = false,
                                    UseEventCustom = true,
                                    EventSpawnTime = 0,
                                    RandomTimeSpawn = new PluginSettings.OtherSettings.EventSettings.Setting.RandomingTime
                                    {
                                        UseRandomTime = true,
                                        MaxEventSpawnTime = 3000,
                                        MinEventSpawnTime = 8000,
                                    },
                                },
                                ChinoockSetting = new PluginSettings.OtherSettings.EventSettings.Setting
                                {
                                    FullOff = true,
                                    UseEventCustom = false,
                                    EventSpawnTime = 3000,
                                    RandomTimeSpawn = new PluginSettings.OtherSettings.EventSettings.Setting.RandomingTime
                                    {
                                        UseRandomTime = false,
                                        MaxEventSpawnTime = 3000,
                                        MinEventSpawnTime = 1000,
                                    },
                                },
                                HelicopterSetting = new PluginSettings.OtherSettings.EventSettings.Setting
                                {
                                    FullOff = true,
                                    UseEventCustom = false,
                                    EventSpawnTime = 3000,
                                    RandomTimeSpawn = new PluginSettings.OtherSettings.EventSettings.Setting.RandomingTime
                                    {
                                        UseRandomTime = false,
                                        MaxEventSpawnTime = 3000,
                                        MinEventSpawnTime = 1000,
                                    },
                                },
                            }
                        },
                    }
                };
            }

            internal class PluginSettings
            {
                [JsonProperty(LanguageEn ? "Configuring supported plugins" : "Настройка поддерживаемых плагинов")]
                public ReferencePlugin ReferenceSettings = new ReferencePlugin();
                [JsonProperty(LanguageEn ? "Additional plugin settings" : "Дополнительная настройка плагина")]
                public OtherSettings OtherSetting = new OtherSettings();     
                internal class OtherSettings
                {
                    [JsonProperty(LanguageEn ? "Event settings on the server" : "Настройки ивентов на сервере")]
                    public EventSettings EventSetting = new EventSettings();   
                    [JsonProperty(LanguageEn ? "Fuel settings when buying vehicles from NPCs" : "Настройки топлива при покупке транспорта у NPC")]
                    public FuelSettings FuelSetting = new FuelSettings();
                    [JsonProperty(LanguageEn ? "Fuel Consumption Rating Settings" : "Настройки рейтинга потребления топлива")]
                    public FuelConsumedTransport FuelConsumedTransportSetting = new FuelConsumedTransport();
                    internal class FuelConsumedTransport
                    {
                        [JsonProperty(LanguageEn ? "Hotairballoon fuel consumption rating (Default = 0.25)" : "Рейтинг потребление топлива у воздушного шара (Стандартно = 0.25)")]
                        public Single ConsumedHotAirBalloon= 0.25f;
                        [JsonProperty(LanguageEn ? "Snowmobile fuel consumption rating (Default = 0.15)" : "Рейтинг потребление топлива снегоходов (Стандартно = 0.15)")]
                        public Single ConsumedSnowmobile = 0.15f;         
                        [JsonProperty(LanguageEn ? "Train fuel consumption rating (Default = 0.15)" : "Рейтинг потребление топлива поездов (Стандартно = 0.15)")]
                        public Single ConsumedTrain = 0.15f;
                        [JsonProperty(LanguageEn ? "Rowboat fuel consumption rating (Default = 0.25)" : "Рейтинг потребление топлива у лодок (Стандартно = 0.25)")]
                        public Single ConsumedBoat = 0.25f;
                        [JsonProperty(LanguageEn ? "Submarine fuel consumption rating (Default = 0.15)" : "Рейтинг потребление топлива у субмарин (Стандартно = 0.15)")]
                        public Single ConsumedSubmarine = 0.15f;
                        [JsonProperty(LanguageEn ? "Minicopter fuel consumption rating (Default = 0.25)" : "Рейтинг потребление топлива у миникоптера (Стандартно = 0.25)")]
                        public Single ConsumedCopter = 0.25f;
                        [JsonProperty(LanguageEn ? "ScrapTransportHelicopter fuel consumption rating (Default = 0.25)" : "Рейтинг потребление топлива у коровы (Стандартно = 0.25)")]
                        public Single ConsumedScrapTransport = 0.25f;
                    }
                    internal class FuelSettings
                    {
                        [JsonProperty(LanguageEn ? "Amount of fuel for boats" : "Кол-во топлива у лодок")]
                        public Int32 AmountBoat = 200;
                        [JsonProperty(LanguageEn ? "The amount of fuel in submarines" : "Кол-во топлива у подводных лодок")]
                        public Int32 AmountSubmarine = 200;
                        [JsonProperty(LanguageEn ? "Minicopter fuel quantity" : "Кол-во топлива у миникоптера")]
                        public Int32 AmountMinicopter = 200;
                        [JsonProperty(LanguageEn ? "Helicopter fuel quantity" : "Кол-во топлива у вертолета")]
                        public Int32 AmountScrapTransport = 200;
                    }

                    [JsonProperty(LanguageEn ? "Use Time Acceleration" : "Использовать ускорение времени")]
                    public Boolean UseTime;
                    [JsonProperty(LanguageEn ? "Use time freeze (the time will be the one you set in the item &lt;Frozen time on the server&gt;)" : "Использовать заморозку времени(время будет такое, какое вы установите в пунке <Замороженное время на сервере>)")]
                    public Boolean UseFreezeTime;
                    [JsonProperty(LanguageEn ? "Frozen time on the server (Set time that will not change and be forever on the server, must be true on &lt;Use time freeze&gt;" : "Замороженное время на сервере (Установите время, которое не будет изменяться и будет вечно на сервере, должен быть true на <Использовать заморозку времени>")]
                    public Int32 FreezeTime;
                    [JsonProperty(LanguageEn ? "What time will the day start?" : "Укажите во сколько будет начинаться день")]
                    public Int32 DayStart;
                    [JsonProperty(LanguageEn ? "What time will the night start?" : "Укажите во сколько будет начинаться ночь")]
                    public Int32 NightStart;
                    [JsonProperty(LanguageEn ? "Specify how long the day will be in minutes" : "Укажите сколько будет длится день в минутах")]
                    public Int32 DayTime;
                    [JsonProperty(LanguageEn ? "Specify how long the night will last in minutes" : "Укажите сколько будет длится ночь в минутах")]
                    public Int32 NightTime;

                    [JsonProperty(LanguageEn ? "Use notification of players about the change of day and night (switching rates. The message is configured in the lang)" : "Использовать уведомление игроков о смене дня и ночи (переключение рейтов. Сообщение настраивается в лэнге)")]
                    public Boolean UseAlertDayNight = true;
                    [JsonProperty(LanguageEn ? "Enable the ability to completely skip the time of day (selected in the paragraph below)" : "Включить возможность полного пропуска времени суток(выбирается в пункте ниже)")]
                    public Boolean UseSkipTime = true;
                    [JsonProperty(LanguageEn ? "Select the type of time-of-day skip (0 - Skip day, 1 - Skip night)" : "Выберите тип пропуска времени суток (0 - Пропускать день, 1 - Пропускать ночь)(Не забудьте включить возможность полного пропуска времени суток)")]
                    public SkipType TypeSkipped = SkipType.Night;

                    internal class EventSettings
                    {
                        [JsonProperty(LanguageEn ? "Helicopter spawn custom settings" : "Кастомные настройки спавна вертолета")]
                        public Setting HelicopterSetting = new Setting();
                        [JsonProperty(LanguageEn ? "Custom tank spawn settings" : "Кастомные настройки спавна танка")]
                        public Setting BreadlaySetting = new Setting();
                        [JsonProperty(LanguageEn ? "Custom ship spawn settings" : "Кастомные настройки спавна корабля")]
                        public Setting CargoShipSetting = new Setting();
                        [JsonProperty(LanguageEn ? "Airdrop spawn custom settings" : "Кастомные настройки спавна аирдропа")]
                        public Setting CargoPlaneSetting = new Setting();
                        [JsonProperty(LanguageEn ? "Chinook custom spawn settings" : "Кастомные настройки спавна чинука")]
                        public Setting ChinoockSetting = new Setting();
                        internal class Setting
                        {
                            [JsonProperty(LanguageEn ? "Completely disable event spawning on the server (true - yes/false - no)" : "Полностью отключить спавн ивента на сервере(true - да/false - нет)")]
                            public Boolean FullOff;
                            [JsonProperty(LanguageEn ? "Enable custom spawn event (true - yes/false - no)" : "Включить кастомный спавн ивент(true - да/false - нет)")]
                            public Boolean UseEventCustom;
                            [JsonProperty(LanguageEn ? "Static event spawn time" : "Статическое время спавна ивента")]
                            public Int32 EventSpawnTime;
                            [JsonProperty(LanguageEn ? "Random spawn time settings" : "Настройки случайного времени спавна")]
                            public RandomingTime RandomTimeSpawn = new RandomingTime();
                            internal class RandomingTime
                            {
                                [JsonProperty(LanguageEn ? "Use random event spawn time (static time will not be taken into account) (true - yes/false - no)" : "Использовать случайное время спавно ивента(статическое время не будет учитываться)(true - да/false - нет)")]
                                public Boolean UseRandomTime;
                                [JsonProperty(LanguageEn ? "Minimum event spawn value" : "Минимальное значение спавна ивента")]
                                public Int32 MinEventSpawnTime;
                                [JsonProperty(LanguageEn ? "Max event spawn value" : "Максимальное значении спавна ивента")]
                                public Int32 MaxEventSpawnTime;
                            }
                        }
                    }
                }

                internal class ReferencePlugin
                {
                    internal class IQChatReference
                    {
                        [JsonProperty(LanguageEn ? "IQChat : Use UI Notifications" : "IQChat : Использовать UI уведомления")]
                        public Boolean UIAlertUse = false;
                        [JsonProperty(LanguageEn ? "IQChat : Custom prefix in chat" : "IQChat : Кастомный префикс в чате")]
                        public String CustomPrefix = "[IQRates]";
                        [JsonProperty(LanguageEn ? "IQChat : Custom chat avatar (If required)" : "IQChat : Кастомный аватар в чате(Если требуется)")]
                        public String CustomAvatar = "0";
                    }
                    [JsonProperty(LanguageEn ? "Setting up IQChat" : "Настройка IQChat")]
                    public IQChatReference IQChatSetting = new IQChatReference();
                }
                [JsonProperty(LanguageEn ? "Rating settings" : "Настройка рейтингов")]
                public Rates RateSetting = new Rates();
		   		 		  						  	   		  		 			  		  		  			 		  		 	
                internal class Rates
                {
                    [JsonProperty(LanguageEn ? "Ranking setting during the day" : "Настройка рейтинга днем")]
                    public AllRates DayRates = new AllRates();
                    [JsonProperty(LanguageEn ? "Setting the rating at night" : "Настройка рейтинга ночью")]
                    public AllRates NightRates = new AllRates();
                    [JsonProperty(LanguageEn ? "Setting privileges and ratings specifically for them [iqrates.vip] = { Setting } (Descending)" : "Настройка привилегий и рейтингов конкретно для них [iqrates.vip] = { Настройка } (По убыванию)")]
                    public Dictionary<String, DayAnNightRate> PrivilegyRates = new Dictionary<String, DayAnNightRate>();

                    [JsonProperty(LanguageEn ? "Setting custom rates (items) by permission - setting (Descending)" : "Настройка кастомных рейтов(предметов) по пермишенсу - настройка (По убыванию)")]
                    public PermissionsRate CustomRatesPermissions = new PermissionsRate();

                    [JsonProperty(LanguageEn ? "Black list of items that will not be categorically affected by the rating" : "Черный лист предметов,на которые катигорично не будут действовать рейтинг")]
                    public List<String> BlackList = new List<String>();
		   		 		  						  	   		  		 			  		  		  			 		  		 	
                    [JsonProperty(LanguageEn ? "Use a prefabs blacklist?" : "Использовать черный лист префабов?")]
                    public Boolean UseBlackListPrefabs;
                    [JsonProperty(LanguageEn ? "Black list of prefabs(ShortPrefabName) - which will not be affected by ratings" : "Черный лист префабов(ShortPrefabName) - на которые не будут действовать рейтинги")]
                    public List<String> BlackListPrefabs = new List<String>();
                    
                    [JsonProperty(LanguageEn ? "Enable melting speed in furnaces (true - yes/false - no)" : "Включить скорость плавки в печах(true - да/false - нет)")]
                    public Boolean UseSpeedBurnable;
                    [JsonProperty(LanguageEn ? "Smelting Fuel Usage Rating (If the list is enabled, this value will be the default value for all non-licensed)" : "Рейтинг использования топлива при переплавки(Если включен список - это значение будет стандартное для всех у кого нет прав)")]
                    public Int32 SpeedFuelBurnable = 1;
                    [JsonProperty(LanguageEn ? "A black list of items for the stove, which will not be categorically affected by melting" : "Черный лист предметов для печки,на которые катигорично не будут действовать плавка")]
                    public List<String> BlackListBurnable = new List<String>();
                    [JsonProperty(LanguageEn ? "Furnace smelting speed (If the list is enabled, this value will be the default for everyone who does not have rights)" : "Скорость плавки печей(Если включен список - это значение будет стандартное для всех у кого нет прав)")]
                    public Single SpeedBurnable;
                    [JsonProperty(LanguageEn ? "Enable list of melting speed in furnaces (true - yes/false - no)" : "Включить список скорости плавки в печах(true - да/false - нет)")]
                    public Boolean UseSpeedBurnableList;
                    [JsonProperty(LanguageEn ? "Setting the melting speed in furnaces by privileges" : "Настройка скорости плавки в печах по привилегиям")]
                    public List<SpeedBurnablePreset> SpeedBurableList = new List<SpeedBurnablePreset>();
                    internal class DayAnNightRate
                    {
                        [JsonProperty(LanguageEn ? "Ranking setting during the day" : "Настройка рейтинга днем")]
                        public AllRates DayRates = new AllRates();
                        [JsonProperty(LanguageEn ? "Setting the rating at night" : "Настройка рейтинга ночью")]
                        public AllRates NightRates = new AllRates();
                    }
                    
                    [JsonProperty(LanguageEn ? "Setting up a recycler" : "Настройка переработчика")]
                    public RecyclerController RecyclersController = new RecyclerController(); 
                    internal class RecyclerController
                    {
                        [JsonProperty(LanguageEn ? "Use the processing time editing functions" : "Использовать функции редактирования времени переработки")]
                        public Boolean UseRecyclerSpeed;
                        [JsonProperty(LanguageEn ? "Static processing time (in seconds) (Will be set according to the standard or if the player does not have the privilege) (Default = 5s)" : "Статичное время переработки (в секундах) (Будет установлено по стандарту или если у игрока нет привилеии) (По умолчанию = 5с)")]
                        public Single DefaultSpeedRecycler;
		   		 		  						  	   		  		 			  		  		  			 		  		 	
                        [JsonProperty(LanguageEn ? "Setting the processing time for privileges (adjust from greater privilege to lesser privilege)" : "Настройка времени переработки для привилегий (настраивать от большей привилегии к меньшей)")]
                        public List<PresetRecycler> PrivilageSpeedRecycler = new List<PresetRecycler>();
                        internal class PresetRecycler
                        {
                            [JsonProperty(LanguageEn ? "Permissions" : "Права")]
                            public String Permissions;
                            [JsonProperty(LanguageEn ? "Standard processing time (in seconds)" : "Стандартное время переработки (в секундах)")]
                            public Single SpeedRecyclers;
                        }
                    }
                    internal class SpeedBurnablePreset
                    {
                        [JsonProperty(LanguageEn ? "Permissions" : "Права")]
                        public String Permissions;
                        [JsonProperty(LanguageEn ? "Furnace melting speed" : "Скорость плавки печей")]
                        public Single SpeedBurnable;
                        [JsonProperty(LanguageEn ? "Smelting Fuel Use Rating" : "Рейтинг использования топлива при переплавки")]
                        public Int32 SpeedFuelBurnable = 1;
                    }
                    internal class PermissionsRate
                    {
                        [JsonProperty(LanguageEn ? "Ranking setting during the day" : "Настройка рейтинга днем")]
                        public Dictionary<String, List<PermissionsRateDetalis>> DayRates = new Dictionary<String, List<PermissionsRateDetalis>>();
                        [JsonProperty(LanguageEn ? "Setting the rating at night" : "Настройка рейтинга ночью")]
                        public Dictionary<String, List<PermissionsRateDetalis>> NightRates = new Dictionary<String, List<PermissionsRateDetalis>>();
                        public class PermissionsRateDetalis
                        {
                            [JsonProperty(LanguageEn ? "Shortname" : "Shortname")]
                            public String Shortname;
                            [JsonProperty(LanguageEn ? "Rate" : "Рейтинг")]
                            public Single Rate;
                        }
                    }
                    internal class AllRates
                    {
                        [JsonProperty(LanguageEn ? "Rating of extracted resources" : "Рейтинг добываемых ресурсов")]
                        public Single GatherRate;
                        [JsonProperty(LanguageEn ? "Rating of found items" : "Рейтинг найденных предметов")]
                        public Single LootRate;
                        [JsonProperty(LanguageEn ? "Pickup Rating" : "Рейтинг поднимаемых предметов")]
                        public Single PickUpRate;
                        [JsonProperty(LanguageEn ? "Rating of plants raised from the beds" : "Рейтинг поднимаемых растений с грядок")]
                        public Single GrowableRate = 1.0f;
                        [JsonProperty(LanguageEn ? "Quarry rating" : "Рейтинг карьеров")]
                        public Single QuarryRate;
                        [JsonProperty(LanguageEn ? "Detailed rating settings in the career" : "Детальная настройка рейтинга в карьере")]
                        public QuarryRateDetalis QuarryDetalis = new QuarryRateDetalis();
                        [JsonProperty(LanguageEn ? "Excavator Rating" : "Рейтинг экскаватора")]
                        public Single ExcavatorRate;
                        [JsonProperty(LanguageEn ? "Coal drop chance" : "Шанс выпадения угля")]
                        public Single CoalRare;
		   		 		  						  	   		  		 			  		  		  			 		  		 	
                        internal class QuarryRateDetalis
                        {
                            [JsonProperty(LanguageEn ? "Use the detailed setting of the career rating (otherwise the 'Career Rating' will be taken for all subjects)" : "Использовать детальную настройку рейтинга карьеров (иначе будет браться 'Рейтинг карьеров' для всех предметов)")]
                            public Boolean UseDetalisRateQuarry;
                            [JsonProperty(LanguageEn ? "The item dropped out of the career - rating" : "Предмет выпадаемый из карьера - рейтинг")]
                            public Dictionary<String, Single> ShortnameListQuarry = new Dictionary<String, Single>();
                        }
                    }
                }
            }
            [JsonProperty(LanguageEn ? "Plugin setup" : "Настройка плагина")]
            public PluginSettings pluginSettings = new PluginSettings();
        }
        public enum SkipType
        {
            Day,
            Night
        }
        public Int32 GetMultiplaceBurnableFuelSpeed(String ownerid)
        {
            Int32 Multiplace = config.pluginSettings.RateSetting.SpeedFuelBurnable;
            if (config.pluginSettings.RateSetting.UseSpeedBurnableList)
            {
                var SpeedInList = config.pluginSettings.RateSetting.SpeedBurableList.OrderByDescending(z => z.SpeedFuelBurnable).FirstOrDefault(x => permission.UserHasPermission(ownerid, x.Permissions));
                if (SpeedInList != null)
                    Multiplace = SpeedInList.SpeedFuelBurnable;
            }
            return Multiplace;
        }
                
        void OnQuarryToggled(MiningQuarry quarry, BasePlayer player)
        {
            AddQuarryPlayer(quarry.net.ID, player.userID);
        }
        public string GetLang(string LangKey, string userID = null, params object[] args)
        {
            sb.Clear();
            if (args != null)
            {
                sb.AppendFormat(lang.GetMessage(LangKey, this, userID), args);
                return sb.ToString();
            }
            return lang.GetMessage(LangKey, this, userID);
        }
        void API_BONUS_RATE_ADDPLAYER(BasePlayer player, Single Rate)
        {
            if (player == null) return;
            if (!BonusRates.ContainsKey(player))
                BonusRates.Add(player, Rate);
            else BonusRates[player] = Rate;
        }

        void OnSunset()
        {
            timeComponent.DayLengthInMinutes = config.pluginSettings.OtherSetting.NightTime * (24.0f / (24.0f - (TOD_Sky.Instance.SunsetTime - TOD_Sky.Instance.SunriseTime)));
            activatedDay = false;
            if (config.pluginSettings.OtherSetting.UseSkipTime)
            {
                if (config.pluginSettings.OtherSetting.TypeSkipped == SkipType.Night)
                    TOD_Sky.Instance.Cycle.Hour = config.pluginSettings.OtherSetting.DayStart;
                else
                {
                    if (config.pluginSettings.OtherSetting.UseAlertDayNight)
                    {
                        Configuration.PluginSettings.Rates.AllRates Rate = config.pluginSettings.RateSetting.NightRates;
                        foreach (BasePlayer player in BasePlayer.activePlayerList)
                            SendChat(GetLang("NIGHT_RATES_ALERT", player.UserIDString, Rate.GatherRate, Rate.LootRate, Rate.PickUpRate, Rate.QuarryRate, Rate.ExcavatorRate, Rate.GrowableRate), player);
                    }
                }
                return;
            }
            if (config.pluginSettings.OtherSetting.UseAlertDayNight)
            {
                Configuration.PluginSettings.Rates.AllRates Rate = config.pluginSettings.RateSetting.NightRates;
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    SendChat(GetLang("NIGHT_RATES_ALERT", player.UserIDString, Rate.GatherRate, Rate.LootRate, Rate.PickUpRate, Rate.QuarryRate, Rate.ExcavatorRate, Rate.GrowableRate), player);
            }
        }
        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null || entity.net == null) return; 
            UInt64 NetID = entity.net.ID;
            if (LootersListCrateID.Contains(NetID))
                LootersListCrateID.Remove(NetID);           
        }
        void SetTimeComponent()
        {
            if (!config.pluginSettings.OtherSetting.UseTime) return;

            timeComponent.ProgressTime = true;
            timeComponent.UseTimeCurve = false;
            timeComponent.OnSunrise += OnSunrise;
            timeComponent.OnSunset += OnSunset;
            timeComponent.OnHour += OnHour;

            if (TOD_Sky.Instance.Cycle.Hour > TOD_Sky.Instance.SunriseTime && TOD_Sky.Instance.Cycle.Hour < TOD_Sky.Instance.SunsetTime)
                OnSunrise();
            else
                OnSunset();
        }
        
        
        private void OnRecyclerToggle(Recycler recycler, BasePlayer player)
        {
            if (!recycler.IsOn())
            {
                NextTick(() =>
                {
                    if (!recycler.IsOn())
                        return;

                    Single Speed = GetSpeedRecycler(player);
                    recycler.InvokeRepeating(recycler.RecycleThink, Speed, Speed);
                });
            }
        }
        bool IsBlackListBurnable(string Shortname)
        {
            var BlackList = config.pluginSettings.RateSetting.BlackListBurnable;
            if (BlackList.Contains(Shortname))
                return true;
            else return false;
        }
        
        
        private static Configuration config = new Configuration();

                private MonumentInfo SpacePort;
        private Coroutine initializeTransport = null;

                private void OnEntitySpawned(BaseBoat boat)
        {
            if (boat == null) return;
            FuelSystemRating(boat.GetFuelSystem(), config.pluginSettings.OtherSetting.FuelSetting.AmountBoat);
        }
            }
}
