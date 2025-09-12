using Newtonsoft.Json;
using System.Text;
using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using Oxide.Core.Plugins;

///Скачано с дискорд сервера Rust Edit [PRO+]
///discord.gg/9vyTXsJyKR

namespace Oxide.Plugins
{
    [Info("IQTurret", "discord.gg/9vyTXsJyKR", "1.0.5")]
    [Description("Турели без электричества с лимитами на игрока/шкаф")]
    internal class IQTurret : RustPlugin
    {
        private Boolean IsLimitPlayer(BasePlayer player, UInt64 ID) => GetAmountTurretPlayer(player, ID) >= GetLimitPlayer(player);
        ElectricSwitch API_GET_SWITCH(BasePlayer player, AutoTurret turret) => GetSwitchForTurret(turret);

        private readonly String PermissionTurnAllTurretsOn = "iqturret.turnonall";

        private void InitializeData()
        {
            List<BaseNetworkable> Turrets = BaseNetworkable.serverEntities.Where(b => b != null && (b is AutoTurret || b is SamSite)).ToList();
            List<BaseNetworkable> ElletircSwitchs = BaseNetworkable.serverEntities.Where(b => b != null && b is ElectricSwitch).ToList();
            if (Turrets == null || ElletircSwitchs == null) return;

            for (Int32 index = 0; index < Turrets.Count; index++)
            {
                AutoTurret turret = Turrets[index] as AutoTurret;
                if (turret != null)
                {
                    if (turret.skinID == 0)
                    {
                        SetupTurret(turret);
                        continue;
                    }
                    else
                    {
                        ElectricSwitch electricSwitch = (ElectricSwitch)ElletircSwitchs.FirstOrDefault(x => (x as ElectricSwitch).skinID == turret.skinID);//[index] as ElectricSwitch;
                        if (electricSwitch == null) continue;

                        if (turret.currentEnergy != 0 && electricSwitch.HasFlag(BaseEntity.Flags.On))
                            electricSwitch.SetFlag(BaseEntity.Flags.On, false);

                        UInt64 ID = turret.skinID;
                        BasePlayer player = BasePlayer.FindByID(turret.OwnerID);

                        RegisteredTurret(ID, turret.OwnerID, electricSwitch, player, turret: turret);
                    }
                }
                else
                {
                    SamSite samSite = Turrets[index] as SamSite;
                    if(samSite != null)
                    {
                        if (samSite.skinID == 0)
                        {
                            SetupTurret(samSite);
                            continue;
                        }
                        else
                        {
                            ElectricSwitch electricSwitch = (ElectricSwitch)ElletircSwitchs.FirstOrDefault(x => (x as ElectricSwitch).skinID == samSite.skinID);//[index] as ElectricSwitch;
                            if (electricSwitch == null) continue;

                            if (samSite.currentEnergy != 0 && electricSwitch.HasFlag(BaseEntity.Flags.On))
                                electricSwitch.SetFlag(BaseEntity.Flags.On, false);
		   		 		  						  	   		  	  			  						  	   		  			 
                            UInt64 ID = samSite.skinID;
                            BasePlayer player = BasePlayer.FindByID(samSite.OwnerID);

                            RegisteredTurret(ID, samSite.OwnerID, electricSwitch, player, samSite: samSite);
                        }
                    }
                }                
            }

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                Int32 LimitPlayer = GetLimitPlayer(player);
                Dictionary<AutoTurret, ElectricSwitch> PlayerTurrets = GetPlayerTurretAndSwitch(player);
                if (PlayerTurrets != null)
                {
                    foreach (KeyValuePair<AutoTurret, ElectricSwitch> Item in PlayerTurrets.Take(LimitPlayer))
                        if (!Item.Key.HasFlag(BaseEntity.Flags.On))
                        {
                            Item.Key.SetFlag(BaseEntity.Flags.On, true);
                            Item.Value.SetFlag(BaseEntity.Flags.On, true);
                            LimitPlayer--;
                        }
                }

                Dictionary<SamSite, ElectricSwitch> PlayerSamSite = GetPlayerSamSiteAndSwitch(player);
                if (PlayerSamSite != null)
                {
                    foreach (KeyValuePair<SamSite, ElectricSwitch> Item in PlayerSamSite.Take(LimitPlayer))
                        if (!Item.Key.HasFlag(BaseEntity.Flags.Reserved8))
                        {
                            Item.Key.SetFlag(BaseEntity.Flags.Reserved8, true);
                            Item.Value.SetFlag(BaseEntity.Flags.On, true);
                            LimitPlayer--;
                        }
                }
            }
        }
        void OnEntitySpawned(AutoTurret turret) => SetupTurret(turret);
        bool CanPickupEntity(BasePlayer player, AutoTurret turret)
        {
            if (turret != null && turret.skinID != 0 && TurretList.ContainsKey(turret.skinID))
            {
                turret.skinID = 0;
                return true;
            }
            return true;
        }

        
                private Dictionary<UInt64, List<ControllerInformation>> TurretList = new Dictionary<UInt64, List<ControllerInformation>>();

        
        
        [ChatCommand("t")]
        void TurretControllChatCommand(BasePlayer player, String cmd, String[] arg)
        {
            if (player == null || arg == null || arg.Length == 0)
            {
                SendChat(GetLang("SYNTAX_COMMAND_ERROR", player.UserIDString), player);
                return;
            }

            String Action = arg[0];
            if (String.IsNullOrWhiteSpace(Action)) return;
            switch (Action)
            {
                case "limit":
                    {
                        UInt64 ID = 0;
                        if (config.LimitController.typeLimiter == TypeLimiter.Building && player.GetBuildingPrivilege() != null)
                            ID = player.GetBuildingPrivilege().buildingID;

                        String Lang = GetLang("INFORMATION_MY_LIMIT", player.UserIDString, (GetLimitPlayer(player) - GetAmountTurretPlayer(player, ID)));
                        SendChat(Lang, player);
                        break;
                    }
                case "off":
                    {
                        if(!permission.UserHasPermission(player.UserIDString, PermissionTurnAllTurretsOff))
                        {
                            SendChat(GetLang("PERMISSION_COMMAND_ERROR", player.UserIDString), player);
                            return;
                        }

                        Int32 LimitPlayer = GetLimitPlayer(player);
                        Dictionary<AutoTurret, ElectricSwitch> PlayerTurrets = GetPlayerTurretAndSwitch(player);
                        if (PlayerTurrets != null)
                        {
                            foreach (KeyValuePair<AutoTurret, ElectricSwitch> Item in PlayerTurrets.Take(LimitPlayer))
                                if (Item.Key.HasFlag(BaseEntity.Flags.On))
                                {
                                    Item.Key.SetFlag(BaseEntity.Flags.On, false);
                                    Item.Value.SetFlag(BaseEntity.Flags.On, false);
                                    LimitPlayer--;
                                }
                        }
                        Dictionary<SamSite, ElectricSwitch> PlayerSamSite = GetPlayerSamSiteAndSwitch(player);
                        if (PlayerSamSite != null)
                        {
                            foreach (KeyValuePair<SamSite, ElectricSwitch> Item in PlayerSamSite.Take(LimitPlayer))
                                if (Item.Key.HasFlag(BaseEntity.Flags.Reserved8))
                                {
                                    Item.Key.SetFlag(BaseEntity.Flags.Reserved8, false);
                                    Item.Value.SetFlag(BaseEntity.Flags.On, false);
                                    LimitPlayer--;
                                }
                        }
                        break;
                    }
                case "on":
                    {
                        if (!permission.UserHasPermission(player.UserIDString, PermissionTurnAllTurretsOn))
                        {
                            SendChat(GetLang("PERMISSION_COMMAND_ERROR", player.UserIDString), player);
                            return;
                        }

                        Int32 LimitPlayer = GetLimitPlayer(player);
                        Dictionary<AutoTurret, ElectricSwitch> PlayerTurrets = GetPlayerTurretAndSwitch(player);
                        if (PlayerTurrets != null)
                        {
                            foreach (KeyValuePair<AutoTurret, ElectricSwitch> Item in PlayerTurrets.Take(LimitPlayer))
                                if (!Item.Key.HasFlag(BaseEntity.Flags.On))
                                {
                                    Item.Key.SetFlag(BaseEntity.Flags.On, true);
                                    Item.Value.SetFlag(BaseEntity.Flags.On, true);
                                    LimitPlayer--;
                                }
                        }
                        Dictionary<SamSite, ElectricSwitch> PlayerSamSite = GetPlayerSamSiteAndSwitch(player);
                        if (PlayerSamSite != null)
                        {
                            foreach (KeyValuePair<SamSite, ElectricSwitch> Item in PlayerSamSite.Take(LimitPlayer))
                                if (!Item.Key.HasFlag(BaseEntity.Flags.Reserved8))
                                {
                                    Item.Key.SetFlag(BaseEntity.Flags.Reserved8, true);
                                    Item.Value.SetFlag(BaseEntity.Flags.On, true);
                                    LimitPlayer--;
                                }
                        }
                        break;
                    }
            }
        }

        [ConsoleCommand("t")]
        void TurretControllConsoleCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || arg == null || arg.Args.Length == 0)
            {
                SendChat(GetLang("SYNTAX_COMMAND_ERROR", player.UserIDString), player);
                return;
            }

            String Action = arg.Args[0];
            if (String.IsNullOrWhiteSpace(Action)) return;
            switch (Action)
            {
                case "limit":
                    {
                        UInt64 ID = 0;
                        if (config.LimitController.typeLimiter == TypeLimiter.Building && player.GetBuildingPrivilege() != null)
                            ID = player.GetBuildingPrivilege().buildingID;

                        String Lang = GetLang("INFORMATION_MY_LIMIT", player.UserIDString, (GetLimitPlayer(player) - GetAmountTurretPlayer(player, ID)));
                        SendChat(Lang, player);
                        break;
                    }
                case "off":
                    {
                        if (!permission.UserHasPermission(player.UserIDString, PermissionTurnAllTurretsOff))
                        {
                            SendChat(GetLang("PERMISSION_COMMAND_ERROR", player.UserIDString), player);
                            return;
                        }

                        Int32 LimitPlayer = GetLimitPlayer(player);
                        Dictionary<AutoTurret, ElectricSwitch> PlayerTurrets = GetPlayerTurretAndSwitch(player);
                        if (PlayerTurrets != null)
                        {
                            foreach (KeyValuePair<AutoTurret, ElectricSwitch> Item in PlayerTurrets.Take(LimitPlayer))
                                if (Item.Key.HasFlag(BaseEntity.Flags.On))
                                {
                                    Item.Key.SetFlag(BaseEntity.Flags.On, false);
                                    Item.Value.SetFlag(BaseEntity.Flags.On, false);
                                    LimitPlayer--;
                                }
                        }
                        Dictionary<SamSite, ElectricSwitch> PlayerSamSite = GetPlayerSamSiteAndSwitch(player);
                        if (PlayerSamSite != null)
                        {
                            foreach (KeyValuePair<SamSite, ElectricSwitch> Item in PlayerSamSite.Take(LimitPlayer))
                                if (Item.Key.HasFlag(BaseEntity.Flags.Reserved8))
                                {
                                    Item.Key.SetFlag(BaseEntity.Flags.Reserved8, false);
                                    Item.Value.SetFlag(BaseEntity.Flags.On, false);
                                    LimitPlayer--;
                                }
                        }
                        break;
                    }
                case "on":
                    {
                        if (!permission.UserHasPermission(player.UserIDString, PermissionTurnAllTurretsOn))
                        {
                            SendChat(GetLang("PERMISSION_COMMAND_ERROR", player.UserIDString), player);
                            return;
                        }

                        Int32 LimitPlayer = GetLimitPlayer(player);
                        Dictionary<AutoTurret, ElectricSwitch> PlayerTurrets = GetPlayerTurretAndSwitch(player);
                        if (PlayerTurrets != null)
                        {
                            foreach (KeyValuePair<AutoTurret, ElectricSwitch> Item in PlayerTurrets.Take(LimitPlayer))
                                if (!Item.Key.HasFlag(BaseEntity.Flags.On))
                                {
                                    Item.Key.SetFlag(BaseEntity.Flags.On, true);
                                    Item.Value.SetFlag(BaseEntity.Flags.On, true);
                                    LimitPlayer--;
                                }
                        }
                        Dictionary<SamSite, ElectricSwitch> PlayerSamSite = GetPlayerSamSiteAndSwitch(player);
                        if (PlayerSamSite != null)
                        {
                            foreach (KeyValuePair<SamSite, ElectricSwitch> Item in PlayerSamSite.Take(LimitPlayer))
                                if (!Item.Key.HasFlag(BaseEntity.Flags.Reserved8))
                                {
                                    Item.Key.SetFlag(BaseEntity.Flags.Reserved8, true);
                                    Item.Value.SetFlag(BaseEntity.Flags.On, true);
                                    LimitPlayer--;
                                }
                        }
                        break;
                    }
            }
        }

        private void SetupTurret(AutoTurret turret)
        {
            if (turret == null) return;
            UInt64 PlayerID = turret.OwnerID;
            UInt64 ID = PlayerID + (UInt64)Oxide.Core.Random.Range(999999999);
            ElectricSwitch smartSwitch = GameManager.server.CreateEntity(SwitchPrefab, turret.transform.TransformPoint(new Vector3(0f, -0.65f, 0.32f)), Quaternion.Euler(turret.transform.rotation.eulerAngles.x, turret.transform.rotation.eulerAngles.y, 0f)) as ElectricSwitch;
            if (smartSwitch == null) return;

            smartSwitch.OwnerID = PlayerID;
            smartSwitch.skinID = ID;
            turret.skinID = ID;

            smartSwitch.Spawn();

            SwitchBlocked(smartSwitch);

            NextTick(() =>
            {
                smartSwitch.SetFlag(BaseEntity.Flags.Reserved8, true);
                smartSwitch.SetFlag(BaseEntity.Flags.On, false);
            });

            UnityEngine.Object.DestroyImmediate(smartSwitch.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.DestroyImmediate(smartSwitch.GetComponent<GroundWatch>());

            smartSwitch.MarkDirty();
            smartSwitch.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            smartSwitch.UpdateNetworkGroup();

            smartSwitch.SetParent(turret, turret.GetSlotAnchorName(BaseEntity.Slot.FireMod), true, true);

            BasePlayer player = BasePlayer.FindByID(PlayerID);
            RegisteredTurret(ID, PlayerID, smartSwitch, player, turret: turret);
        }

        private Boolean IsTurretElectricalTurned(ElectricSwitch Switch)
        {
            if (Switch == null) return false;
            AutoTurret turret = GetTurretForSwitch(Switch);
            SamSite samSite = GetSamSiteForSwitch(Switch);

            if (turret != null)
                return turret?.GetConnectedInputCount() > 0;

            if (samSite != null)
                return samSite?.GetConnectedInputCount() > 0;

            return false;
        }
		   		 		  						  	   		  	  			  						  	   		  			 
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning(LanguageEn ? $"Error #85 configuration readings 'oxide/config/{Name}', creating a new configuration!" : $"Ошибка #85 чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!");
                LoadDefaultConfig();
            }
            NextTick(SaveConfig);
        }

        
                private void RegisteredTurret(UInt64 ID, UInt64 PlayerID, ElectricSwitch smartSwitch, BasePlayer player, AutoTurret turret = null, SamSite samSite = null)
        {
            if (player == null) return;

            ControllerInformation information = new ControllerInformation();
            information.samSite = samSite;
            information.turret = turret;
            information.electricSwitch = smartSwitch;
            information.PlayerID = PlayerID;
            information.BuildingID = player.GetBuildingPrivilege() == null ? 0 : player.GetBuildingPrivilege().buildingID;

            if (!TurretList.ContainsKey(ID))
                TurretList.Add(ID, new List<ControllerInformation> { information });
            else TurretList[ID].Add(information);
        }
        private SamSite GetSamSiteForSwitch(ElectricSwitch Switch)
        {
            if (Switch == null || Switch.IsDestroyed || !TurretList.ContainsKey(Switch.skinID)) return null;
            List<ControllerInformation> InformationList = TurretList[Switch.skinID];
            if (InformationList == null) return null;

            foreach (ControllerInformation Info in InformationList)
                if (Info.electricSwitch.skinID.Equals(Switch.skinID))
                    return Info.samSite;

            return null;
        }
        public string GetLang(String LangKey, String userID = null, params object[] args)
        {
            sb.Clear();
            if (args != null)
            {
                sb.AppendFormat(lang.GetMessage(LangKey, this, userID), args);
                return sb.ToString();
            }
            return lang.GetMessage(LangKey, this, userID);
        }

        private void SetupTurret(SamSite samSite)
        {
            if (samSite == null) return;
            UInt64 PlayerID = samSite.OwnerID;
            UInt64 ID = PlayerID + (UInt64)Oxide.Core.Random.Range(999999999);
            ElectricSwitch smartSwitch = GameManager.server.CreateEntity(SwitchPrefab, samSite.transform.TransformPoint(new Vector3(0f, -0.65f, 0.95f)), Quaternion.Euler(samSite.transform.rotation.eulerAngles.x, samSite.transform.rotation.eulerAngles.y, 0f)) as ElectricSwitch;
            if (smartSwitch == null) return;

            smartSwitch.OwnerID = PlayerID;
            smartSwitch.skinID = ID;
            samSite.skinID = ID;

            smartSwitch.Spawn();
		   		 		  						  	   		  	  			  						  	   		  			 
            SwitchBlocked(smartSwitch);

            NextTick(() =>
            {
                smartSwitch.SetFlag(BaseEntity.Flags.Reserved8, true);
                smartSwitch.SetFlag(BaseEntity.Flags.On, false);
            });

            UnityEngine.Object.DestroyImmediate(smartSwitch.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.DestroyImmediate(smartSwitch.GetComponent<GroundWatch>());

            smartSwitch.MarkDirty();
            smartSwitch.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            smartSwitch.UpdateNetworkGroup();

            smartSwitch.SetParent(samSite, samSite.GetSlotAnchorName(BaseEntity.Slot.FireMod), true, true);

            BasePlayer player = BasePlayer.FindByID(PlayerID);
            RegisteredTurret(ID, PlayerID, smartSwitch, player, samSite: samSite);
        }
        
        
        private static Configuration config = new Configuration();
        
                private enum TypeLimiter
        {
            Player,
            Building
        }
        /// <summary>
        /// </summary>
        /// Обновление 1.0.х
        /// - Исправил баг с лимитом на шкаф (когда можно было активировать турель на рядом находящейся постройки без шкафа)
        /// - Добавлена поддержка SamSite 
        /// - Теперь когда игрок будет стоять в зоне действия других шкафов - при использовании команды (/)t limit у него будет отображаться лимит конкретного шкафа
        /// - Добавлена поддержка IQGradeRemove

        
        [PluginReference] private Plugin IQChat;

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            BasePlayer damager = hitInfo.InitiatorPlayer;
            if (entity == null || hitInfo == null || damager == null) return;

            ElectricSwitch Switch = entity as ElectricSwitch;
            if (Switch != null && Switch.skinID != 0 && TurretList.ContainsKey(Switch.skinID))
                hitInfo.damageTypes.ScaleAll(0);
        }

        object OnWireClear(BasePlayer player, IOEntity entity1, int connected, IOEntity entity2, bool flag)
        {
            ElectricSwitch switchConnected = entity1 as ElectricSwitch;
            if (switchConnected != null && switchConnected.skinID != 0 && TurretList.ContainsKey(switchConnected.skinID))
                return false;

            return null;
        }

        object OnWireConnect(BasePlayer player, IOEntity entity1, int inputs, IOEntity entity2, int outputs)
        {
            AutoTurret turret = entity1 as AutoTurret;
            if ((entity1 is AutoTurret) || (entity1 is SamSite)) return null;

            ElectricSwitch Switch = GetSwitchForTurret(turret);
            if (Switch == null) return null;

            if (Switch.HasFlag(BaseEntity.Flags.On))
            {
                Switch.SetFlag(BaseEntity.Flags.On, false);
                Switch.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                Switch.MarkDirty();
            }
		   		 		  						  	   		  	  			  						  	   		  			 
            return null;
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        void OnEntityKill(SamSite samSite)
        {
            if (samSite == null || samSite.skinID == 0) return;
            UInt64 ID = samSite.skinID;

            if (TurretList.ContainsKey(ID))
                TurretList.Remove(ID);
        }

        private void SwitchBlocked(ElectricSwitch electricSwitch)
        {
            if (electricSwitch == null || electricSwitch.IsDestroyed) return;

            electricSwitch.outputs[0].connectedTo = new IOEntity.IORef();
            electricSwitch.outputs[0].connectedTo.Set(electricSwitch);
            electricSwitch.outputs[0].connectedToSlot = 0;
            electricSwitch.outputs[0].connectedTo.Init();

            electricSwitch.inputs[0].connectedTo = new IOEntity.IORef();
            electricSwitch.inputs[0].connectedTo.Set(electricSwitch);
            electricSwitch.inputs[0].connectedToSlot = 0;
            electricSwitch.inputs[0].connectedTo.Init();

            electricSwitch.inputs[1].connectedTo = new IOEntity.IORef();
            electricSwitch.inputs[1].connectedTo.Set(electricSwitch);
            electricSwitch.inputs[1].connectedToSlot = 0;
            electricSwitch.inputs[1].connectedTo.Init();

            electricSwitch.inputs[2].connectedTo = new IOEntity.IORef();
            electricSwitch.inputs[2].connectedTo.Set(electricSwitch);
            electricSwitch.inputs[2].connectedToSlot = 0;
            electricSwitch.inputs[2].connectedTo.Init();

            electricSwitch.MarkDirtyForceUpdateOutputs();
            electricSwitch.SendNetworkUpdate();
        }
        AutoTurret API_GET_TURRET(BasePlayer player, ElectricSwitch electricSwitch) => GetTurretForSwitch(electricSwitch);
        object OnSwitchToggle(IOEntity entity, BasePlayer player)
        {
            if (entity == null || player == null) return null;

            ElectricSwitch Switch = entity as ElectricSwitch;
            if (Switch == null) return null;

            if (!player.IsBuildingAuthed())
            {
                SendChat(GetLang("IS_BUILDING_BLOCK_TOGGLE", player.UserIDString), player);
                return false;
            }

            if (Switch.HasFlag(BaseEntity.Flags.On))
            {
                TurretToggle(player, Switch);
                return null;
            }

            if (IsTurretElectricalTurned(Switch))
            {
                SendChat(GetLang("IS_TURRET_ELECTRIC_TRUE", player.UserIDString), player);
                return false;
            }

            if (config.LimitController.UseLimitControll)
            {
                UInt64 ID = 0;
                if (config.LimitController.typeLimiter == TypeLimiter.Building && player.GetBuildingPrivilege() != null)
                {
                    ID = player.GetBuildingPrivilege().buildingID;
                    UInt64 IDTurret = Switch.skinID;
                    if (TurretList.ContainsKey(IDTurret))
                    {
                        ControllerInformation controller = TurretList[IDTurret].FirstOrDefault(x => x.electricSwitch == Switch);
                        if (controller == null) return null;
		   		 		  						  	   		  	  			  						  	   		  			 
                        controller.BuildingID = ID;
                    }
                }

                if (IsLimitPlayer(player, ID))
                {
                    SendChat(GetLang("IS_LIMIT_TRUE", player.UserIDString), player);
                    return false;
                }
            }

            TurretToggle(player, Switch);
            return null;
        }
		   		 		  						  	   		  	  			  						  	   		  			 
        public void SendChat(String Message, BasePlayer player, ConVar.Chat.ChatChannel channel = ConVar.Chat.ChatChannel.Global)
        {
            if (IQChat)
                if (config.ReferencesPlugin.IQChatSetting.UIAlertUse)
                    IQChat?.Call("API_ALERT_PLAYER_UI", player, Message);
                else IQChat?.Call("API_ALERT_PLAYER", player, Message, config.ReferencesPlugin.IQChatSetting.CustomPrefix, config.ReferencesPlugin.IQChatSetting.CustomAvatar);
            else player.SendConsoleCommand("chat.add", channel, 0, Message);
        }
        Boolean API_IS_TURRETLIST(BaseEntity entity)
        {
            if (entity.skinID == 0) return false;
            return TurretList.ContainsKey(entity.skinID);
        }
        
        
        Boolean API_IS_TURRETLIST(UInt64 ID)
        {
            if (ID == 0) return false;
            return TurretList.ContainsKey(ID);
        }
        internal class ControllerInformation
        {
            public AutoTurret turret;
            public SamSite samSite;
            public ElectricSwitch electricSwitch;
            public UInt64 PlayerID;
            public UInt64 BuildingID;
        }

        private void TurretToggle(BasePlayer player, ElectricSwitch electricSwitch)
        {
            if(electricSwitch == null)
            {
                return;
            }

            SamSite samSite = GetSamSiteForSwitch(electricSwitch);
            AutoTurret turret = GetTurretForSwitch(electricSwitch);
            Boolean IsFlag = false;

            if (turret != null && !turret.IsDestroyed)
            {
                if (!turret.HasFlag(BaseEntity.Flags.On))
                    turret.SetFlag(BaseEntity.Flags.On, true);
                else turret.SetFlag(BaseEntity.Flags.On, false);

                IsFlag = turret.HasFlag(BaseEntity.Flags.On);
            }
            else if (samSite != null && !samSite.IsDestroyed)
            {
                if (!samSite.HasFlag(BaseEntity.Flags.Reserved8))
                    samSite.SetFlag(BaseEntity.Flags.Reserved8, true);
                else samSite.SetFlag(BaseEntity.Flags.Reserved8, false);

                IsFlag = samSite.HasFlag(BaseEntity.Flags.Reserved8);
            }
		   		 		  						  	   		  	  			  						  	   		  			 
            if (config.LimitController.UseLimitControll)
            {
                UInt64 ID = 0;
                if (config.LimitController.typeLimiter == TypeLimiter.Building && player.GetBuildingPrivilege() != null)
                    ID = player.GetBuildingPrivilege().buildingID;

                Int32 LimitCount = (GetLimitPlayer(player) - GetAmountTurretPlayer(player, ID));
                SendChat(GetLang(IsFlag ? "INFORMATION_USER_ON" : "INFORMATION_USER_OFF", player.UserIDString, LimitCount), player);
            }
        }
        private Int32 GetAmountTurretPlayer(BasePlayer player, UInt64 ID)
        {
            TypeLimiter Type = config.LimitController.typeLimiter;

            Int32 CountTurret = 0;
		   		 		  						  	   		  	  			  						  	   		  			 
            if (Type == TypeLimiter.Player)
            {
                foreach (KeyValuePair<UInt64, List<ControllerInformation>> Turrets in TurretList)
                    foreach (ControllerInformation ControllerInformation in Turrets.Value)
                        if (ControllerInformation.PlayerID == player.userID
                        && ((ControllerInformation.turret != null && !ControllerInformation.turret.IsDestroyed) || (ControllerInformation.samSite != null && !ControllerInformation.samSite.IsDestroyed))
                        && ControllerInformation.electricSwitch != null
                        && !ControllerInformation.electricSwitch.IsDestroyed
                        && !IsTurretElectricalTurned(ControllerInformation.electricSwitch)
                        && ((ControllerInformation.turret != null && ControllerInformation.turret.HasFlag(BaseEntity.Flags.On)) || (ControllerInformation.samSite != null && ControllerInformation.samSite.HasFlag(BaseEntity.Flags.Reserved8))))
                            CountTurret++;
            }
            else
            {
                foreach (KeyValuePair<UInt64, List<ControllerInformation>> Turrets in TurretList)
                    foreach (ControllerInformation ControllerInformation in Turrets.Value)
                        if (ControllerInformation.BuildingID == ID
                        && ((ControllerInformation.turret != null && !ControllerInformation.turret.IsDestroyed) || (ControllerInformation.samSite != null && !ControllerInformation.samSite.IsDestroyed))
                        && ControllerInformation.electricSwitch != null
                        && !ControllerInformation.electricSwitch.IsDestroyed
                        && !IsTurretElectricalTurned(ControllerInformation.electricSwitch)
                        && ((ControllerInformation.turret != null && ControllerInformation.turret.HasFlag(BaseEntity.Flags.On)) || (ControllerInformation.samSite != null && ControllerInformation.samSite.HasFlag(BaseEntity.Flags.Reserved8))))
                            CountTurret++;
            }

            return CountTurret;
        }

        private Dictionary<AutoTurret, ElectricSwitch> GetPlayerTurretAndSwitch(BasePlayer player)
        {
            Dictionary<AutoTurret, ElectricSwitch> keyValuePairs = new Dictionary<AutoTurret, ElectricSwitch>();

            if (config.LimitController.typeLimiter == TypeLimiter.Player)
            {
                foreach (KeyValuePair<UInt64, List<ControllerInformation>> Turrets in TurretList)
                    foreach (ControllerInformation ControllerInformation in Turrets.Value)
                        if (ControllerInformation.PlayerID == player.userID
                        && ControllerInformation.turret != null
                        && !ControllerInformation.turret.IsDestroyed
                        && ControllerInformation.electricSwitch != null
                        && !ControllerInformation.electricSwitch.IsDestroyed
                        && !IsTurretElectricalTurned(ControllerInformation.electricSwitch))
                            keyValuePairs.Add(ControllerInformation.turret, ControllerInformation.electricSwitch);
            }
            else
            {
                foreach (KeyValuePair<UInt64, List<ControllerInformation>> Turrets in TurretList)
                    foreach (ControllerInformation ControllerInformation in Turrets.Value)
                        if (ControllerInformation.BuildingID == (player.GetBuildingPrivilege() == null ? 0 : player.GetBuildingPrivilege().buildingID)
                        && ControllerInformation.turret != null
                        && !ControllerInformation.turret.IsDestroyed
                        && ControllerInformation.electricSwitch != null
                        && !ControllerInformation.electricSwitch.IsDestroyed
                        && !IsTurretElectricalTurned(ControllerInformation.electricSwitch))
                            keyValuePairs.Add(ControllerInformation.turret, ControllerInformation.electricSwitch);
            }

            return keyValuePairs;
        }
		   		 		  						  	   		  	  			  						  	   		  			 
        private ElectricSwitch GetSwitchForTurret(AutoTurret Turret)
        {
            if (Turret == null || Turret.IsDestroyed || !TurretList.ContainsKey(Turret.skinID)) return null;
            List<ControllerInformation> InformationList = TurretList[Turret.skinID];
            if (InformationList == null) return null;

            foreach (ControllerInformation Info in InformationList)
                if (Info.electricSwitch.skinID.Equals(Turret.skinID))
                    return Info.electricSwitch;

            return null;
        }
        private const Boolean LanguageEn = false;
        protected override void SaveConfig() => Config.WriteObject(config);
        private class Configuration
        {
            [JsonProperty(LanguageEn ? "Configuring plugins for Collaboration" : "Настройка плагинов для совместной работы")]
            public ReferenceSettings ReferencesPlugin = new ReferenceSettings();
            [JsonProperty(LanguageEn ? "Setting limits on turrets WITHOUT electricity" : "Настройка лимитов на турели БЕЗ электричества")]
            public LimitControll LimitController = new LimitControll();

            internal class LimitControll
            {
                [JsonProperty(LanguageEn ? "Use the limit on turrets WITHOUT electricity? (true - yes/false - no)" : "Использовать лимит на туррели БЕЗ электричества? (true - да/false - нет)")]
                public Boolean UseLimitControll;
                [JsonProperty(LanguageEn ? "The limit of turrets WITHOUT electricity by privileges [Permission] = Limit (Make a list from more to less)" : "Лимит турелей БЕЗ электричества по привилегиям [Права] = Лимит (Составляйте список от большего - к меньшему)")]
                public Dictionary<String, Int32> PermissionsLimits = new Dictionary<String, Int32>();
                [JsonProperty(LanguageEn ? "Limit Type: 0 - Player, 1 - Building" : "Тип лимита : 0 - На игрока, 1 - На шкаф")]
                public TypeLimiter typeLimiter;
                [JsonProperty(LanguageEn ? "Limit turrets WITHOUT electricity (If the player does not have privileges)" : "Лимит турелей БЕЗ электричества (Если у игрока нет привилегий)")]
                public Int32 LimitAmount;
            }

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    LimitController = new LimitControll
                    {
                        typeLimiter = TypeLimiter.Building,
                        UseLimitControll = true,
                        LimitAmount = 3,
                        PermissionsLimits = new Dictionary<String, Int32>()
                        {
                            ["iqturret.ultra"] = 150,
                            ["iqturret.king"] = 15,
                            ["iqturret.premium"] = 10,
                            ["iqturret.vip"] = 6,
                        }
                    },
                    ReferencesPlugin = new ReferenceSettings
                    {
                        IQChatSetting = new ReferenceSettings.IQChatPlugin
                        {
                            CustomPrefix = "[<color=#ffff40>IQTurret</color>] ",
                            CustomAvatar = "0",
                            UIAlertUse = false,
                        }
                    }
                };
            }
            internal class ReferenceSettings
            {
                [JsonProperty(LanguageEn ? "Setting up collaboration with IQChat" : "Настройка совместной работы с IQChat")]
                public IQChatPlugin IQChatSetting = new IQChatPlugin();
                internal class IQChatPlugin
                {
                    [JsonProperty(LanguageEn ? "IQChat :Custom prefix in the chat" : "IQChat : Кастомный префикс в чате")]
                    public String CustomPrefix;
                    [JsonProperty(LanguageEn ? "IQChat : Custom avatar in the chat(If required)" : "IQChat : Кастомный аватар в чате(Если требуется)")]
                    public String CustomAvatar;
                    [JsonProperty(LanguageEn ? "IQChat : Use UI notifications" : "IQChat : Использовать UI-уведомления")]
                    public Boolean UIAlertUse = false;
                }
            }
        }
        private readonly String SwitchPrefab = "assets/prefabs/deployable/playerioents/simpleswitch/switch.prefab";

        void Unload() => UnloadPlugin();
        
                void OnServerInitialized()
        {
            foreach (String Permissions in config.LimitController.PermissionsLimits.Keys)
                permission.RegisterPermission(Permissions, this);

            permission.RegisterPermission(PermissionTurnAllTurretsOn, this);
            permission.RegisterPermission(PermissionTurnAllTurretsOff, this);

            InitializeData();
        }

        private AutoTurret GetTurretForSwitch(ElectricSwitch Switch)
        {
            if (Switch == null || Switch.IsDestroyed || !TurretList.ContainsKey(Switch.skinID)) return null;
            List<ControllerInformation> InformationList = TurretList[Switch.skinID];
            if (InformationList == null) return null;

            foreach (ControllerInformation Info in InformationList)
                if (Info.electricSwitch.skinID.Equals(Switch.skinID))
                    return Info.turret;

            return null;
        }
        void OnEntitySpawned(SamSite samSite) => SetupTurret(samSite);


        
        
        private void UnloadPlugin()
        {
            foreach (List<ControllerInformation> TurretInformation in TurretList.Values)
                foreach(ControllerInformation controllerInformation in TurretInformation)
                {
                    AutoTurret turret = controllerInformation.turret;
                    SamSite samSite = controllerInformation.samSite;

                    if (turret != null && turret.HasFlag(BaseEntity.Flags.On) && turret.currentEnergy == 0)
                        turret.SetFlag(BaseEntity.Flags.On, false);

                    if (samSite != null && samSite.HasFlag(BaseEntity.Flags.Reserved8) && samSite.currentEnergy == 0)
                        samSite.SetFlag(BaseEntity.Flags.Reserved8, false);

                    ElectricSwitch electricSwitch = controllerInformation.electricSwitch;

                    if(electricSwitch != null && electricSwitch.HasFlag(BaseEntity.Flags.On))
                        electricSwitch.SetFlag(BaseEntity.Flags.On, false);
                }
        }

        
        
                private static StringBuilder sb = new StringBuilder(); 
        private readonly String PermissionTurnAllTurretsOff = "iqturret.turnoffall";

        void OnEntityKill(AutoTurret turret)
        {
            if (turret == null || turret.skinID == 0) return;
            UInt64 ID = turret.skinID;

            if (TurretList.ContainsKey(ID))
                TurretList.Remove(ID);
        }
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["IS_LIMIT_TRUE"] = "At you <color=#dd6363>превышен</color> limit of active turrets <color=#dd6363>WITHOUT ELECTRICITY</color>",
                ["IS_TURRET_ELECTRIC_TRUE"] = "This turret is connected <color=#dd6363>to electricity</color>, you can't use the switch!",
                ["IS_BUILDING_BLOCK_TOGGLE"] = "You cannot use the switch in <color=#dd6363>someone else's house</color>",
                ["INFORMATION_USER_ON"] = "You have successfully <color=#66e28b>enabled</color> the turret, you can still enable <color=#dd6363>{0}</color> turret",
                ["INFORMATION_USER_OFF"] = "You have successfully <color=#dd6363>disabled</color> the turret, you can still enable <color=#dd6363>{0}</color> turret",
                ["INFORMATION_MY_LIMIT"] = "<color=#dd6363> is available to you</color> to enable <color=#dd6363>{0}</color> turrets",
                ["SYNTAX_COMMAND_ERROR"] = "<color=#dd6363>Syntax error : </color>\nUse the commands :\n1. t on - enables all disabled turrets\n2. t off - turns off all enabled turrets\n3. t limit - shows how many turrets are still available to you without electricity",
                ["PERMISSION_COMMAND_ERROR"] = "<color=#dd6363>Access error : </color>\nYou don't have enough rights to use this command!",

            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["IS_LIMIT_TRUE"] = "У вас <color=#dd6363>превышен</color> лимит активных турелей <color=#dd6363>БЕЗ ЭЛЕКТРИЧЕСТВА</color>",
                ["IS_TURRET_ELECTRIC_TRUE"] = "Данная турель подключена <color=#dd6363>к электричеству</color>, вы не можете использовать рубильник!",
                ["IS_BUILDING_BLOCK_TOGGLE"] = "Вы не можете использовать рубильник в <color=#dd6363>чужом доме</color>",
                ["INFORMATION_USER_ON"] = "Вы успешно <color=#66e28b>включили</color> турель, вам доступно еще для включения <color=#dd6363>{0}</color> турели",
                ["INFORMATION_USER_OFF"] = "Вы успешно <color=#dd6363>выключили</color> турель, вам доступно еще для включения <color=#dd6363>{0}</color> турели",
                ["INFORMATION_MY_LIMIT"] = "Вам <color=#dd6363>доступно</color> для включения <color=#dd6363>{0}</color> турелей",
                ["SYNTAX_COMMAND_ERROR"] = "<color=#dd6363>Ошибка синтаксиса : </color>\nИспользуйте команды :\n1. t on - включает все выключенные\n2. t off - выключает все включенные турели\n3. t limit - показывает сколько вам еще доступно турелей без электричества",
                ["PERMISSION_COMMAND_ERROR"] = "<color=#dd6363>Ошибка доступа : </color>\nУ вас недостаточно прав для использования данной команды!",
		   		 		  						  	   		  	  			  						  	   		  			 
            }, this, "ru");
            PrintWarning("Logs : #32471912 | Языковой файл загружен успешно"); 
        }
        private Dictionary<SamSite, ElectricSwitch> GetPlayerSamSiteAndSwitch(BasePlayer player)
        {
            Dictionary<SamSite, ElectricSwitch> keyValuePairs = new Dictionary<SamSite, ElectricSwitch>();

            if (config.LimitController.typeLimiter == TypeLimiter.Player)
            {
                foreach (KeyValuePair<UInt64, List<ControllerInformation>> Turrets in TurretList)
                    foreach (ControllerInformation ControllerInformation in Turrets.Value)
                        if (ControllerInformation.PlayerID == player.userID
                        && ControllerInformation.samSite != null
                        && !ControllerInformation.samSite.IsDestroyed
                        && ControllerInformation.electricSwitch != null
                        && !ControllerInformation.electricSwitch.IsDestroyed
                        && !IsTurretElectricalTurned(ControllerInformation.electricSwitch))
                            keyValuePairs.Add(ControllerInformation.samSite, ControllerInformation.electricSwitch);
            }
            else
            {
                foreach (KeyValuePair<UInt64, List<ControllerInformation>> Turrets in TurretList)
                    foreach (ControllerInformation ControllerInformation in Turrets.Value)
                        if (ControllerInformation.BuildingID == (player.GetBuildingPrivilege() == null ? 0 : player.GetBuildingPrivilege().buildingID)
                        && ControllerInformation.samSite != null
                        && !ControllerInformation.samSite.IsDestroyed
                        && ControllerInformation.electricSwitch != null
                        && !ControllerInformation.electricSwitch.IsDestroyed
                        && !IsTurretElectricalTurned(ControllerInformation.electricSwitch))
                            keyValuePairs.Add(ControllerInformation.samSite, ControllerInformation.electricSwitch);
            }

            return keyValuePairs;
        }

        private Int32 GetLimitPlayer(BasePlayer player)
        {
            foreach (KeyValuePair<String, Int32> LimitPrivilage in config.LimitController.PermissionsLimits)
                if (permission.UserHasPermission(player.UserIDString, LimitPrivilage.Key))
                    return LimitPrivilage.Value;

            return config.LimitController.LimitAmount;
        }
        bool CanPickupEntity(BasePlayer player, ElectricSwitch Switch)
        {
            if (Switch != null && Switch.skinID != 0 && TurretList.ContainsKey(Switch.skinID))
                return false;
            return true;
        }

            }
}
