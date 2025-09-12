using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core;
using UnityEngine;
using Object = System.Object;

namespace Oxide.Plugins
{
    [Info("IQTurret", "Mercury", "1.17.29")]
    [Description("Турели без электричества с лимитами на игрока/шкаф")]
    public class IQTurret : RustPlugin
    {
        /// <summary>
        /// Обновление :
        /// - Добавлена поддержка модов для оружия в турелях при автоматической установке
        /// - Исправлено возможное возникновение ошибки NRE в OnEntityKill при перезапуске сервера
        /// - Добавлена проверка на турели из RaidableBases для исключения возможного появления тумблеров на турелях
        /// - Исправлено возможное залипание кнопки на турели при перезапуске сервера
        /// </summary>
        ///

        #region Vars
        private static IQTurret _;

        private enum TypeLimiter
        {
            Player,
            Building
        }
        private const Boolean LanguageEn = false;

        private Timer timerSubscrube;
        private Dictionary<BasePlayer, Double> CooldownCommanndTOnOff = new();
        private readonly List<UInt64> IDsPrefabs = new List<UInt64> { 3312510084, 2059775839 };
        private const String SwitchPrefab = "assets/prefabs/deployable/playerioents/simpleswitch/switch.prefab";
        private const String ButtonPrefab = "assets/prefabs/deployable/playerioents/button/button.prefab";
        private static Double CurrentTime => Facepunch.Math.Epoch.Current;
        private const String PermissionTurnAllTurretsOn = "iqturret.turnonall";
        private const String PermissionTurnAllTurretsOff = "iqturret.turnoffall";

        private ProtectionProperties ImmortalProtection;

        #endregion

        #region References

        [PluginReference] private Plugin IQChat, IQGuardianDrone, IQDronePatrol, RaidableBases;

        public void SendChat(String Message, BasePlayer player,
            ConVar.Chat.ChatChannel channel = ConVar.Chat.ChatChannel.Global)
        {
            if (IQChat)
                IQChat.Call("API_ALERT_PLAYER", player, Message, config.ReferencesPlugin.IQChatSetting.CustomPrefix, config.ReferencesPlugin.IQChatSetting.CustomAvatar);
            else player.SendConsoleCommand("chat.add", channel, 0, Message);
        }

        #region RaidBlocked

        public Boolean IsRaidBlocked(BasePlayer player)
        {
            if (!config.ReferencesPlugin.BlockedTumblerRaidblock) return false;
            String ret = Interface.Call("CanTeleport", player) as String;
            return ret != null;
        }

        #endregion

        #endregion

        #region Configuration

        private static Configuration config = new Configuration();

        private class Configuration
        {
            [JsonProperty(LanguageEn ? "Use the button (true) or tumblers (false) to activate the turret" : "Использовать кнопку (true) или тумблеры (false) для включения турели")]
            public Boolean buttonOrSwitch;
            [JsonProperty(LanguageEn ? "To add a toggle switch for SamSite" : "Добавлять тумблер для SamSite")]
            public Boolean UseSamSite;
            [JsonProperty(LanguageEn ? "Setting limits on turrets WITHOUT electricity" : "Настройка лимитов на турели БЕЗ электричества")]
            public LimitControll LimitController = new LimitControll();

            [JsonProperty(LanguageEn ? "Configuring plugins for Collaboration" : "Настройка плагинов для совместной работы")]
            public ReferenceSettings ReferencesPlugin = new ReferenceSettings();
            [JsonProperty(LanguageEn ? "Setting up automatic weapon addition to the turret upon installation" : "Настройка автоматического добавления оружия в турель при установке")]
            public WeaponControll weaponControll = new WeaponControll();
            [JsonProperty(LanguageEn ? "Setting up the automatic addition of cartridges in SamSite" : "Настройка автоматического добавления патронов в SamSite")]
            public SamSiteControll samSiteControll = new SamSiteControll();
            [JsonProperty(LanguageEn ? "Setting up the automatic addition of cartridges in FlameTurret" : "Настройка автоматического добавления патронов в огненную туррель")]
            public FlameTurretControl flameTurretControll = new FlameTurretControl();
            [JsonProperty(LanguageEn ? "Setting up the automatic addition of cartridges in GunTrap" : "Настройка автоматического добавления патронов в гантрап")]
            public GunTrapControl guntrapControll = new GunTrapControl();
            [JsonProperty(LanguageEn ? "Setting the cooldown for the commands t on | t off" : "Настройка перезарядки для команд t on | t off")]
            public CooldownUseCommand cooldownController = new CooldownUseCommand();
            internal class CooldownUseCommand
            {
                [JsonProperty(LanguageEn ? "Use cooldown for commands t on | t off" : "Использовать перезарядку на команды t on | t off")]
                public Boolean useCooldown;
                [JsonProperty(LanguageEn ? "Time in seconds" : "Время в секундах")]
                public Single timeCooldown;
            }

            internal class SamSiteControll
            {
                [JsonProperty(LanguageEn ? "Add ammo to SamSite" : "Добавлять патроны в SamSite")]
                public Boolean useSamAmmo;
                [JsonProperty(LanguageEn ? "Amount" : "Количество")]
                public Int32 amount;
                [JsonProperty(LanguageEn ? "Lock slots in SamSite after automatically adding a cartridge" : "Блокировать слоты в SamSite после автоматического добавления патрон")]
                public Boolean BlockSlotsAutoWeapon;
            }
            
            internal class FlameTurretControl
            {
                [JsonProperty(LanguageEn ? "Add ammo to FlameTurret" : "Добавлять патроны в огненную туррель")]
                public Boolean useFlameAmmo;
                [JsonProperty(LanguageEn ? "Amount" : "Количество")]
                public Int32 amount;
                [JsonProperty(LanguageEn ? "Lock slots in FlameTurret after automatically adding a cartridge" : "Блокировать слоты в огненную туррель после автоматического добавления патрон")]
                public Boolean BlockSlotsAutoWeapon;
            }
            
            internal class GunTrapControl
            {
                [JsonProperty(LanguageEn ? "Add ammo to GunTrap" : "Добавлять патроны в гантрап")]
                public Boolean useGunTrapAmmo;
                [JsonProperty(LanguageEn ? "Amount" : "Количество")]
                public Int32 amount;
                [JsonProperty(LanguageEn ? "Lock slots in GunTrap after automatically adding a cartridge" : "Блокировать слоты в гантрапе после автоматического добавления патрон")]
                public Boolean BlockSlotsAutoWeapon;
            }
            
            internal class WeaponControll
            {
                [JsonProperty(LanguageEn ? "Use the weapon embedding function after installing the auto turret" : "Использовать функцию встраивания оружия после установки автоматической турели")]
                public Boolean UseWeaponController;
                [JsonProperty(LanguageEn ? "Block slots in turrets - if weapons were added automatically" : "Блокировать слоты в автоматической турели - если были добавлены оружия автоматически")]
                public Boolean BlockSlotsAutoWeapon;
                
                [JsonProperty(LanguageEn ? "[Permissions] - Weapon settings" : "[Права] - Настройка оружия")]
                public Dictionary<String, WeaponListAutoTurret> privilageWeapon;

                public WeaponListAutoTurret GetWeaponTurretDefault(UInt64 userID)
                {
                    foreach (KeyValuePair<String, WeaponListAutoTurret> weaponList in privilageWeapon)
                    {
                        if (String.IsNullOrWhiteSpace(weaponList.Value.shortnameWeapon)) continue;
                        if (_.permission.UserHasPermission(userID.ToString(), weaponList.Key))
                            return weaponList.Value;
                    }

                    return null;
                }
                
                internal class WeaponListAutoTurret
                {
                    [JsonProperty(LanguageEn ? "Weapon shortname" : "Shortname оружия")]
                    public String shortnameWeapon;
                    [JsonProperty(LanguageEn ? "Weapon skin ID (mod)" : "ID скина (мода) для оружия")]
                    public UInt64 weaponSkin;
                    [JsonProperty(LanguageEn ? "Weapon attachments (mods)" : "Моды для оружия (прицелы, фонарики и т.д.)")]
                    public List<String> weaponMods = new List<String>();
                    [JsonProperty(LanguageEn ? "Ammo for turret" : "Патроны для турели")]
                    public List<AmmoList> ammoList = new List<AmmoList>();
                    internal class AmmoList
                    {
                        [JsonProperty(LanguageEn ? "Ammo shortname" : "Shortname патрона")]
                        public String shortname;
                        [JsonProperty(LanguageEn ? "Ammo amount" : "Количество патрон")]
                        public Int32 amount;
                    }
                }
            }
            internal class LimitControll
            {
                [JsonProperty(LanguageEn ? "Limit Type: 0 - Player, 1 - Building" : "Тип лимита : 0 - На игрока, 1 - На шкаф")]
                public TypeLimiter typeLimiter;
                [JsonProperty(LanguageEn ? "Use the limit on turrets WITHOUT electricity? (true - yes/false - no)" : "Использовать лимит на туррели БЕЗ электричества? (true - да/false - нет)")]
                public Boolean UseLimitControll;
                [JsonProperty(LanguageEn ? "Limit turrets WITHOUT electricity (If the player does not have privileges)" : "Лимит турелей БЕЗ электричества (Если у игрока нет привилегий)")]
                public Int32 LimitAmount;
                [JsonProperty(LanguageEn ? "The limit of turrets WITHOUT electricity by privileges [Permission] = Limit (Make a list from more to less)" : "Лимит турелей БЕЗ электричества по привилегиям [Права] = Лимит (Составляйте список от большего - к меньшему)")]
                public Dictionary<String, Int32> PermissionsLimits = new Dictionary<String, Int32>();
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
                }

                [JsonProperty(LanguageEn ? "Prohibit the use of a switch during a raidBlock?" : "Запретить использовать рубильник во время рейдблока?")]
                public Boolean BlockedTumblerRaidblock;
            }

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    buttonOrSwitch = false,
                    UseSamSite = true,
                    flameTurretControll = new FlameTurretControl
                    {
                        useFlameAmmo = false,
                        amount = 300,
                        BlockSlotsAutoWeapon = true
                    },
                    guntrapControll = new GunTrapControl
                    {
                        useGunTrapAmmo = false,
                        amount = 50,
                        BlockSlotsAutoWeapon = true
                    },
                    samSiteControll = new SamSiteControll
                    {
                        useSamAmmo = false,
                        amount = 30,
                        BlockSlotsAutoWeapon = true
                    },
                    cooldownController = new CooldownUseCommand()
                    {
                        useCooldown = true,
                        timeCooldown = 60f,
                    },
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
                    weaponControll = new WeaponControll()
                    {
                        UseWeaponController = false,
                        BlockSlotsAutoWeapon = false,
                        privilageWeapon = new Dictionary<String, WeaponControll.WeaponListAutoTurret>()
                        {
                            ["iqturret.ultra"] = new WeaponControll.WeaponListAutoTurret()
                            {
                                shortnameWeapon = "rifle.ak",
                                weaponSkin = 0,
                                weaponMods = new List<String>() { "weapon.mod.holosight", "weapon.mod.lasersight", "weapon.mod.silencer" },
                                ammoList = new List<WeaponControll.WeaponListAutoTurret.AmmoList>()
                                {
                                    new WeaponControll.WeaponListAutoTurret.AmmoList()
                                    {
                                        shortname = "ammo.rifle.explosive",
                                        amount = 128,
                                    },
                                    new WeaponControll.WeaponListAutoTurret.AmmoList()
                                    {
                                        shortname = "ammo.rifle.explosive",
                                        amount = 128,
                                    },
                                    new WeaponControll.WeaponListAutoTurret.AmmoList()
                                    {
                                        shortname = "ammo.rifle.explosive",
                                        amount = 128,
                                    },
                                },
                            },
                            ["iqturret.king"] = new WeaponControll.WeaponListAutoTurret()
                            {
                                shortnameWeapon = "rifle.ak",
                                weaponSkin = 0,
                                weaponMods = new List<String>() { "weapon.mod.holosight", "weapon.mod.lasersight" },
                                ammoList = new List<WeaponControll.WeaponListAutoTurret.AmmoList>()
                                {
                                    new WeaponControll.WeaponListAutoTurret.AmmoList()
                                    {
                                        shortname = "ammo.rifle",
                                        amount = 128,
                                    },
                                    new WeaponControll.WeaponListAutoTurret.AmmoList()
                                    {
                                        shortname = "ammo.rifle",
                                        amount = 128,
                                    },
                                    new WeaponControll.WeaponListAutoTurret.AmmoList()
                                    {
                                        shortname = "ammo.rifle",
                                        amount = 128,
                                    },
                                }
                            },
                            ["iqturret.premium"] = new WeaponControll.WeaponListAutoTurret()
                            {
                                shortnameWeapon = "rifle.semiauto",
                                weaponSkin = 0,
                                weaponMods = new List<String>() { "weapon.mod.flashlight", "weapon.mod.extendedmags" },
                                ammoList = new List<WeaponControll.WeaponListAutoTurret.AmmoList>()
                                {
                                    new WeaponControll.WeaponListAutoTurret.AmmoList()
                                    {
                                        shortname = "ammo.rifle",
                                        amount = 128,
                                    },
                                    new WeaponControll.WeaponListAutoTurret.AmmoList()
                                    {
                                        shortname = "ammo.rifle",
                                        amount = 128,
                                    },
                                    new WeaponControll.WeaponListAutoTurret.AmmoList()
                                    {
                                        shortname = "ammo.rifle",
                                        amount = 128,
                                    },
                                }
                            },
                            ["iqturret.vip"] = new WeaponControll.WeaponListAutoTurret()
                            {
                                shortnameWeapon = "smg.thompson",
                                weaponSkin = 0,
                                weaponMods = new List<String>() { "weapon.mod.flashlight" },
                                ammoList = new List<WeaponControll.WeaponListAutoTurret.AmmoList>()
                                {
                                    new WeaponControll.WeaponListAutoTurret.AmmoList()
                                    {
                                        shortname = "ammo.pistol",
                                        amount = 128,
                                    },
                                    new WeaponControll.WeaponListAutoTurret.AmmoList()
                                    {
                                        shortname = "ammo.pistol",
                                        amount = 128,
                                    },
                                    new WeaponControll.WeaponListAutoTurret.AmmoList()
                                    {
                                        shortname = "ammo.pistol",
                                        amount = 128,
                                    },
                                }
                            },
                        }
                    },
                    ReferencesPlugin = new ReferenceSettings
                    {
                        IQChatSetting = new ReferenceSettings.IQChatPlugin
                        {
                            CustomPrefix = "[<color=#ffff40>IQTurret</color>] ",
                            CustomAvatar = "0",
                        },
                        BlockedTumblerRaidblock = true,
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();

                if (config.weaponControll == null)
                {
                    config.weaponControll = new Configuration.WeaponControll()
                    {
                        UseWeaponController = false,
                        BlockSlotsAutoWeapon = false,
                        privilageWeapon = new Dictionary<String, Configuration.WeaponControll.WeaponListAutoTurret>()
                        {
                            ["iqturret.ultra"] = new Configuration.WeaponControll.WeaponListAutoTurret()
                            {
                                shortnameWeapon = "rifle.ak",
                                weaponSkin = 0,
                                weaponMods = new List<String>() { "weapon.mod.holosight", "weapon.mod.lasersight", "weapon.mod.silencer" },
                                ammoList = new List<Configuration.WeaponControll.WeaponListAutoTurret.AmmoList>()
                                {
                                    new Configuration.WeaponControll.WeaponListAutoTurret.AmmoList()
                                    {
                                        shortname = "ammo.rifle.explosive",
                                        amount = 128,
                                    },
                                    new Configuration.WeaponControll.WeaponListAutoTurret.AmmoList()
                                    {
                                        shortname = "ammo.rifle.explosive",
                                        amount = 128,
                                    },
                                    new Configuration.WeaponControll.WeaponListAutoTurret.AmmoList()
                                    {
                                        shortname = "ammo.rifle.explosive",
                                        amount = 128,
                                    },
                                },
                            },
                            ["iqturret.king"] = new Configuration.WeaponControll.WeaponListAutoTurret()
                            {
                                shortnameWeapon = "rifle.ak",
                                weaponSkin = 0,
                                weaponMods = new List<String>() { "weapon.mod.holosight", "weapon.mod.lasersight" },
                                ammoList = new List<Configuration.WeaponControll.WeaponListAutoTurret.AmmoList>()
                                {
                                    new Configuration.WeaponControll.WeaponListAutoTurret.AmmoList()
                                    {
                                        shortname = "ammo.rifle",
                                        amount = 128,
                                    },
                                    new Configuration.WeaponControll.WeaponListAutoTurret.AmmoList()
                                    {
                                        shortname = "ammo.rifle",
                                        amount = 128,
                                    },
                                    new Configuration.WeaponControll.WeaponListAutoTurret.AmmoList()
                                    {
                                        shortname = "ammo.rifle",
                                        amount = 128,
                                    },
                                }
                            },
                            ["iqturret.premium"] = new Configuration.WeaponControll.WeaponListAutoTurret()
                            {
                                shortnameWeapon = "rifle.semiauto",
                                weaponSkin = 0,
                                weaponMods = new List<String>() { "weapon.mod.flashlight", "weapon.mod.extendedmags" },
                                ammoList = new List<Configuration.WeaponControll.WeaponListAutoTurret.AmmoList>()
                                {
                                    new Configuration.WeaponControll.WeaponListAutoTurret.AmmoList()
                                    {
                                        shortname = "ammo.rifle",
                                        amount = 128,
                                    },
                                    new Configuration.WeaponControll.WeaponListAutoTurret.AmmoList()
                                    {
                                        shortname = "ammo.rifle",
                                        amount = 128,
                                    },
                                    new Configuration.WeaponControll.WeaponListAutoTurret.AmmoList()
                                    {
                                        shortname = "ammo.rifle",
                                        amount = 128,
                                    },
                                }
                            },
                            ["iqturret.vip"] = new Configuration.WeaponControll.WeaponListAutoTurret()
                            {
                                shortnameWeapon = "smg.thompson",
                                weaponSkin = 0,
                                weaponMods = new List<String>() { "weapon.mod.flashlight" },
                                ammoList = new List<Configuration.WeaponControll.WeaponListAutoTurret.AmmoList>()
                                {
                                    new Configuration.WeaponControll.WeaponListAutoTurret.AmmoList()
                                    {
                                        shortname = "ammo.pistol",
                                        amount = 128,
                                    },
                                    new Configuration.WeaponControll.WeaponListAutoTurret.AmmoList()
                                    {
                                        shortname = "ammo.pistol",
                                        amount = 128,
                                    },
                                    new Configuration.WeaponControll.WeaponListAutoTurret.AmmoList()
                                    {
                                        shortname = "ammo.pistol",
                                        amount = 128,
                                    },
                                }
                            },
                        }
                    };
                }
            }
            catch
            {
                PrintWarning(LanguageEn
                    ? $"Error #8344445 configuration readings 'oxide/config/{Name}', creating a new configuration!"
                    : $"Ошибка #8344445 чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!"); 
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Data
        
        private Dictionary<UInt64, List<LocalRepository>> localRepositories = new();
        
        internal class LocalRepository
        {
            public BaseEntity turret;
            public IOEntity ioEntity;
        }

        #endregion

        #region Hooks

        void Init()
        {
            _ = this;
            
            Unsubscribe(nameof(OnEntitySpawned));
        }

        void Unload() => UnloadPlugin();
        void OnServerShutdown() => UnloadPlugin();
        
        private Object OnInterferenceUpdate(AutoTurret turret)
        {
            BaseEntity switchEntity = GetSwtich(turret.OwnerID, turret);
            if (switchEntity != null && switchEntity.HasFlag(BaseEntity.Flags.On))
                return true;

            return null;
        }
        
        void OnServerInitialized()
        {
            ImmortalProtection = ScriptableObject.CreateInstance<ProtectionProperties>();
            ImmortalProtection.name = "TurretsSwitchProtection";
            ImmortalProtection.Add(1);
            
            foreach (String Permissions in config.LimitController.PermissionsLimits.Keys)
                if (!permission.PermissionExists(Permissions))
                    permission.RegisterPermission(Permissions, this);
            
            foreach (String Permissions in config.weaponControll.privilageWeapon.Keys)
                if (!permission.PermissionExists(Permissions))
                    permission.RegisterPermission(Permissions, this);

            permission.RegisterPermission(PermissionTurnAllTurretsOn, this);
            permission.RegisterPermission(PermissionTurnAllTurretsOff, this);

            LoadPlugin();

            Unsubscribe(config.buttonOrSwitch ? nameof(OnSwitchToggle) : nameof(OnButtonPress));
        }

        void OnEntitySpawned(AutoTurret turret) => NextTick(() => { SetupTurret(turret); });

        void OnEntitySpawned(SamSite samSite)
        {
            if (!config.UseSamSite) return;
            NextTick(() => { SetupTurret(samSite); });
        }
        
        void OnEntitySpawned(FlameTurret flameTurret)
        {
            if (!config.flameTurretControll.useFlameAmmo) return;
            NextTick(() => { SetupTurret(flameTurret); });
        }
        
        void OnEntitySpawned(GunTrap gunTrap)
        {
            if (!config.guntrapControll.useGunTrapAmmo) return;
            NextTick(() => { SetupTurret(gunTrap); });
        }
        
        void OnEntityKill(AutoTurret turret)
        {
            if (turret == null) return;
            RemoveSwitch(turret);
        }

        void OnEntityKill(SamSite samSite)
        {
            if (samSite == null || !config.UseSamSite) return;
            RemoveSwitch(samSite);
        }

        object OnWireConnect(BasePlayer player, IOEntity entity1, int inputs, IOEntity entity2, int outputs)
        {
            if ((entity1 is not (AutoTurret or SamSite))) return null;
            ElectricSwitch Switch = GetSwtich(entity1.OwnerID, entity1) as ElectricSwitch;
            if (Switch == null) return null;
            if (entity1.HasFlag(BaseEntity.Flags.Reserved8)) return false;
            return null;
        }
        
        object OnWireClear(BasePlayer player, IOEntity entity1, int connected, IOEntity entity2, bool flag)
        {
            ElectricSwitch switchConnected = entity1 as ElectricSwitch;
            if (switchConnected == null) return null;
            BaseEntity turret = GetTurret(switchConnected.OwnerID, switchConnected);
            if (turret != null)
                return false;

            return null;
        }
        
        Boolean CanPickupEntity(BasePlayer player, AutoTurret turret)
        {
            Boolean canPickUp = true;
            Object canRemove = Interface.Call("canRemove", player, turret);

            if (IQGuardianDrone && canRemove != null)
                canPickUp = canRemove is not String; 
            
            if (turret == null || turret.OwnerID == 0 || !localRepositories.ContainsKey(player.userID)) return canPickUp;
     
            RemoveSwitch(turret);
            return canPickUp;
        }

        private void OnButtonPress(PressButton button, BasePlayer player) => HandleTurretToggle(player, button);
        Object OnSwitchToggle(ElectricSwitch electricSwitch, BasePlayer player) => HandleTurretToggle(player, electricSwitch);
        
        #endregion

        #region Metods

        #region Init/Unload

        private void LoadPlugin()
        {
            List<BaseNetworkable> turretServerList = BaseNetworkable.serverEntities
                .Where(b => b != null && b.net != null && IDsPrefabs.Contains(b.prefabID)).ToList();

            foreach (BaseNetworkable turretList in turretServerList)
            {
                BaseEntity turret = turretList as BaseEntity;
                if (turret == null) continue;
                
                UInt64 userID = turret.OwnerID;
                if (!userID.IsSteamId() || userID == 0) continue;

                if (IQGuardianDrone && turret is AutoTurret)
                {
                    Boolean IsValidTurret = IQGuardianDrone.Call<Boolean>("IsValidTurret", turret);
                    if (IsValidTurret || turret.skinID != 0)
                        continue;
                }
                
                NextTick(() =>
                {
                    if (turret == null) return;
                    SetupTurret(turret);
                });
            }

            // Fix for button sticking issue on server restart
            List<BaseNetworkable> switchList = BaseNetworkable.serverEntities
                .Where(b => b != null && b.net != null && 
                            (b.GetComponent<ElectricSwitch>() != null || b.GetComponent<PressButton>() != null) &&
                            (b as BaseEntity)?.HasFlag(BaseEntity.Flags.Reserved8) == true).ToList();

            foreach (BaseNetworkable switchEntity in switchList)
            {
                if (switchEntity.GetParentEntity() != null && 
                    IDsPrefabs.Contains(switchEntity.GetParentEntity().prefabID))
                {
                    BaseEntity buttonEntity = switchEntity as BaseEntity;
                    if (buttonEntity != null)
                    {
                        buttonEntity.SetFlag(BaseEntity.Flags.On, false);
                        buttonEntity.SendNetworkUpdate();
                    }
                }
            }

            timerSubscrube = timer.Once(2f, () => Subscribe(nameof(OnEntitySpawned)));
        }

        private void UnloadPlugin()
        {
            if (_ == null) return;

            if (localRepositories != null)
            {
                foreach (KeyValuePair<UInt64, List<LocalRepository>> repositories in localRepositories)
                {
                    foreach (LocalRepository localRepository in repositories.Value)
                    {
                        if (localRepository.ioEntity != null && !localRepository.ioEntity.IsDestroyed)
                            localRepository.ioEntity.Kill();
                    }
                }
                
                localRepositories.Clear();
                localRepositories = null;
            }

            if (timerSubscrube is { Destroyed: false })
            {
                timerSubscrube.Destroy();
                timerSubscrube = null;
            }
            
            if (CooldownCommanndTOnOff != null)
            {
                CooldownCommanndTOnOff.Clear();
                CooldownCommanndTOnOff = null;
            }
            
            _ = null;
        }

        #endregion

        #region SetupTurret

        #region WeaponTurret

        private void AddWeaponTurret(AutoTurret turret, UInt64 userID)
        {
            if (!config.weaponControll.UseWeaponController) return;
            Configuration.WeaponControll.WeaponListAutoTurret weaponInfo = config.weaponControll.GetWeaponTurretDefault(userID);
            if (weaponInfo == null) return;
            
            Item weapon = ItemManager.CreateByName(weaponInfo.shortnameWeapon, 1);
            
            if (weaponInfo.weaponSkin > 0)
                weapon.skin = weaponInfo.weaponSkin;
                
            // Add weapon mods/attachments
            if (weaponInfo.weaponMods != null && weaponInfo.weaponMods.Count > 0)
            {
                foreach (String modShortname in weaponInfo.weaponMods)
                {
                    if (String.IsNullOrEmpty(modShortname)) continue;
                    
                    Item mod = ItemManager.CreateByName(modShortname, 1);
                    if (mod != null)
                    {
                        mod.MoveToContainer(weapon.contents);
                        mod.MarkDirty();
                    }
                }
            }
            
            if (!weapon.MoveToContainer(turret.inventory, 0))
                weapon.Remove();
            else
            {
                turret.CancelInvoke(turret.UpdateAttachedWeapon);
                turret.UpdateAttachedWeapon();
        
                turret.Invoke(() => {                
                    AddReserveAmmo(turret, weaponInfo);
                    if (config.weaponControll.BlockSlotsAutoWeapon)
                    {
                        turret.dropsLoot = false;
                        turret.inventory.capacity = 0;
                    }
                },0.3f);
            }
        }

        private void AddReserveAmmo(AutoTurret turret, Configuration.WeaponControll.WeaponListAutoTurret weaponInfo)
        {
            Int32 slot = 1;
            Int32 maxSlot = turret.inventory.capacity - 1;

            foreach (Configuration.WeaponControll.WeaponListAutoTurret.AmmoList ammoList in weaponInfo.ammoList)
            {
                if (slot > maxSlot)
                    break;
                
                Item item = ItemManager.CreateByName(ammoList.shortname, ammoList.amount);
                if (!item.MoveToContainer(turret.inventory, slot))
                    item.Remove();

                item.MarkDirty();
                slot++;
            }
        }
        
        private void AddSamAmmo(SamSite samSite)
        {
            Item ammoItem = ItemManager.CreateByName("ammo.rocket.sam", config.samSiteControll.amount);
            if (ammoItem == null) return;
            if (!ammoItem.MoveToContainer(samSite.inventory))
                ammoItem.Remove();

            ammoItem.MarkDirty();
        }
        
        private void AddFlameAmmo(FlameTurret flameTurret)
        {
            Item ammoItem = ItemManager.CreateByName("lowgradefuel", config.flameTurretControll.amount);
            if (ammoItem == null) return;
            if (!ammoItem.MoveToContainer(flameTurret.inventory))
                ammoItem.Remove();

            ammoItem.MarkDirty();
        }
        
        private void AddGunTrapAmmo(GunTrap gunTrapTurret)
        {
            Item ammoItem = ItemManager.CreateByName("ammo.handmade.shell", config.guntrapControll.amount);
            if (ammoItem == null) return;
            if (!ammoItem.MoveToContainer(gunTrapTurret.inventory))
                ammoItem.Remove();

            ammoItem.MarkDirty();
        }

        #endregion

        private Boolean IsOtherPluginTurret(BaseEntity entityTurret)
        {
            Boolean isValidTurretDrone = false;

            if (IQGuardianDrone && entityTurret as AutoTurret)
                isValidTurretDrone = IQGuardianDrone.Call<Boolean>("IsValidTurret", entityTurret) && entityTurret.skinID != 0;

            if (isValidTurretDrone) return true;
             
            if (IQDronePatrol && entityTurret as AutoTurret)
                isValidTurretDrone = IQDronePatrol.Call<Boolean>("IsValidTurret", entityTurret.OwnerID) && entityTurret.skinID != 0;
            
            if (isValidTurretDrone) return true;
            
            // Check for RaidableBases
            if (entityTurret.OwnerID == 0) return true;
            
            BaseEntity parentEntity = entityTurret.GetParentEntity();
            if (parentEntity != null && (parentEntity.PrefabName.Contains("raidable") || parentEntity.name.Contains("raidable")))
                return true;
            
            return false;
        }
        
        private void SetupTurret(BaseEntity entityTurret, Boolean IsInit = false)
        {
            Object status = Interface.CallHook("OnSetupTurret", entityTurret);
            if (status != null)
                return;
            
            Boolean isOtherPluginTurret = IsOtherPluginTurret(entityTurret);
            
            if (isOtherPluginTurret) return;
            
            // Check for RaidableBases integration
            if (RaidableBases != null && entityTurret != null)
            {
                Object result = RaidableBases.Call("IsTargetBuildingBlock", entityTurret);
                if (result != null && (bool)result)
                    return;
            }

            if (!IsInit)
            {
                if (config.flameTurretControll.useFlameAmmo)
                    if (entityTurret as FlameTurret)
                    {
                        FlameTurret flameTurret = (FlameTurret)entityTurret;
                        flameTurret.Invoke(() =>
                        {
                            AddFlameAmmo(flameTurret);
                            if (config.flameTurretControll.BlockSlotsAutoWeapon)
                            {
                                flameTurret.dropsLoot = false;
                                flameTurret.inventory.SetFlag(ItemContainer.Flag.IsLocked, true);
                            }
                        }, 0.3f);

                        flameTurret.SendNetworkUpdate();
                        return;
                    }
                
                if (config.guntrapControll.useGunTrapAmmo)
                    if (entityTurret as GunTrap)
                    {
                        GunTrap gunTrap = (GunTrap)entityTurret;
                        gunTrap.Invoke(() =>
                        {
                            AddGunTrapAmmo(gunTrap);
                            if (config.guntrapControll.BlockSlotsAutoWeapon)
                            {
                                gunTrap.dropsLoot = false;
                                gunTrap.inventory.SetFlag(ItemContainer.Flag.IsLocked, true);
                            }
                        }, 0.3f);

                        gunTrap.SendNetworkUpdate();
                        return;
                    }
                
                if (entityTurret as AutoTurret)
                {
                    AutoTurret autoTurret = (AutoTurret)entityTurret;
                    AddWeaponTurret(autoTurret, entityTurret.OwnerID);
                    autoTurret.SendNetworkUpdate();
                }

                if(config.samSiteControll.useSamAmmo)
                    if (entityTurret as SamSite)
                    {
                        SamSite samSite = (SamSite)entityTurret;
                        samSite.Invoke(() =>
                        {
                            AddSamAmmo(samSite);
                            if (config.samSiteControll.BlockSlotsAutoWeapon)
                            {
                                samSite.dropsLoot = false;
                                samSite.inventory.capacity = 0;
                            }
                        }, 0.3f);

                        samSite.SendNetworkUpdate();
                    }
            }

            if (entityTurret == null || entityTurret is NPCAutoTurret ||
                !IDsPrefabs.Contains(entityTurret.prefabID) && entityTurret.skinID == 1587601905 ||
                entityTurret.skinID == 732323205) return;

            Vector3 PositionSwitch = entityTurret is AutoTurret
                ? new Vector3(0f, 0.35f, 0.32f)
                : new Vector3(0f, 0.35f, 0.95f);

            String ioPrefab = config.buttonOrSwitch ? ButtonPrefab : SwitchPrefab;
            
            IOEntity smartSwitch = GameManager.server.CreateEntity(ioPrefab,
                entityTurret.transform.TransformPoint(PositionSwitch),
                Quaternion.Euler(entityTurret.transform.rotation.eulerAngles.x,
                    entityTurret.transform.rotation.eulerAngles.y, 0f), true) as IOEntity;

            if (smartSwitch == null) return;

            smartSwitch.OwnerID = entityTurret.OwnerID;
            smartSwitch.pickup.enabled = false;
            smartSwitch.SetFlag(IOEntity.Flag_HasPower, true);
            smartSwitch.baseProtection = ImmortalProtection;

            #region Remove Components

            foreach (var meshCollider in smartSwitch.GetComponentsInChildren<MeshCollider>())
                UnityEngine.Object.DestroyImmediate(meshCollider);

            UnityEngine.Object.DestroyImmediate(smartSwitch.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.DestroyImmediate(smartSwitch.GetComponent<GroundWatch>());

            #endregion

            #region Hide InputAndOutput Slots

            foreach (IOEntity.IOSlot input in smartSwitch.inputs)
                input.type = IOEntity.IOType.Generic;

            foreach (IOEntity.IOSlot output in smartSwitch.outputs)
                output.type = IOEntity.IOType.Generic;

            #endregion

            smartSwitch.Spawn();
            smartSwitch.SetFlag(BaseEntity.Flags.Reserved8, true);
            smartSwitch.SetFlag(BaseEntity.Flags.On, false);
            smartSwitch.MarkDirty();
            
            RegisteredTurret(entityTurret.OwnerID, smartSwitch, entityTurret);
        }

        private void RegisteredTurret(UInt64 userID, IOEntity smartSwitch, BaseEntity entityTurret)
        {
            BasePlayer player = BasePlayer.FindByID(userID);
            UInt64 buildingID = 0;

            BuildingPrivlidge buildingPrivilage = !player ? entityTurret.GetBuildingPrivilege() : player.GetBuildingPrivilege();
            buildingID = buildingPrivilage ? buildingPrivilage.buildingID : 0;
            
            AutoTurret autoTurret = entityTurret as AutoTurret;
            if (autoTurret != null && autoTurret.currentEnergy <= 0 && autoTurret.HasFlag(BaseEntity.Flags.Reserved8))
            {
                autoTurret.SetFlag(BaseEntity.Flags.Reserved8, true);
                smartSwitch.SetFlag(BaseEntity.Flags.On, true);
            }
            
            SamSite samSite = entityTurret as SamSite;
            if (samSite != null && samSite.currentEnergy <= 0 && samSite.HasFlag(BaseEntity.Flags.Reserved8))
            {
                samSite.SetFlag(BaseEntity.Flags.Reserved8, true);
                smartSwitch.SetFlag(BaseEntity.Flags.On, true);
            }

            if (!localRepositories.ContainsKey(userID))
                localRepositories.Add(userID, new List<LocalRepository> { });

            localRepositories[userID].Add(new LocalRepository
            {
                turret = entityTurret,
                ioEntity = smartSwitch
            });
        }

        #endregion

        #region Destroyed Turret

        private void RemoveSwitch(BaseEntity entity)
        {
            if (entity == null) return;
            UInt64 userID = entity.OwnerID;
            if (!localRepositories.ContainsKey(userID)) return;
            BaseEntity smartSwtich = GetSwtich(userID, entity);
            if (smartSwtich != null && !smartSwtich.IsDestroyed)
                smartSwtich.Kill();

            if (localRepositories.ContainsKey(userID))
                localRepositories[userID].RemoveAll(item => item.turret == entity);
        }

        #endregion

        #region Limitter

        private Boolean IsLimitPlayer(UInt64 userID, BasePlayer player, BaseEntity turret) => GetAmountActiveTurretPlayer(userID, player, turret) >= GetLimitPlayer(userID);
        private Int32 GetAmountActiveTurretPlayer(UInt64 userID, BasePlayer player, BaseEntity turret)
        {
            if (!localRepositories.TryGetValue(userID, out List<LocalRepository> localTurrets)) return 0;

            Int32 countTurret = 0;
            UInt64 buildingID = config.LimitController.typeLimiter == TypeLimiter.Building ? GetBuildingID(player, turret) : 0;
            
            foreach (LocalRepository localTurret in localTurrets)
            {
                if (localTurret.turret == null) continue;
                if (IsTurretElectricalTurned(localTurret.turret)) continue;
                if (!IsTurretFlagsTurned(localTurret.turret)) continue;

                switch (config.LimitController.typeLimiter)
                {
                    case TypeLimiter.Building when GetBuildingID(userID, localTurret.turret) != buildingID:
                    case TypeLimiter.Player when localTurret.turret.OwnerID != userID:
                        continue;
                    default:
                        countTurret++;
                        break;
                }
            }

            return countTurret;
        }

        private Boolean IsTurretElectricalTurned(BaseEntity entityTurret)
        {
            if (entityTurret == null) return false;
            return entityTurret switch
            {
                AutoTurret turret => turret.currentEnergy > 0,
                SamSite site => site.currentEnergy > 0,
                _ => false
            };
        }

        private Boolean IsTurretWireConnected(BaseEntity entityTurret)
        {
            if (entityTurret == null) return false;
            ContainerIOEntity containerIOEntity = entityTurret as ContainerIOEntity;
            return containerIOEntity == null || containerIOEntity.inputs.All(input => input.connectedTo.Get() == null);
        }

        private Boolean IsTurretFlagsTurned(BaseEntity entityTurret) => entityTurret != null && entityTurret.HasFlag(BaseEntity.Flags.Reserved8);

        private Int32 GetLimitPlayer(UInt64 userID)
        {
            foreach (KeyValuePair<String, Int32> LimitPrivilage in config.LimitController.PermissionsLimits)
                if (permission.UserHasPermission(userID.ToString(), LimitPrivilage.Key))
                    return LimitPrivilage.Value;

            return config.LimitController.LimitAmount;
        }

        #endregion

        #region Helper

        private UInt64 GetBuildingID(BasePlayer player, BaseEntity turret)
        {
            if (turret)
            {
                BuildingPrivlidge turretBuildingPrivilage = turret.GetBuildingPrivilege();
                if (turretBuildingPrivilage)
                    return turretBuildingPrivilage.buildingID;
            }

            if (!player) return 0;
            BuildingPrivlidge playerBuildingPrivilage = player.GetBuildingPrivilege();
            return playerBuildingPrivilage ? playerBuildingPrivilage.buildingID : 0;
        } 

        private UInt64 GetBuildingID(UInt64 userID, BaseEntity turret)
        {
            if (turret)
            {
                BuildingPrivlidge turretBuildingPrivilage = turret.GetBuildingPrivilege();
                if (turretBuildingPrivilage)
                    return turretBuildingPrivilage.buildingID;
            }

            BasePlayer player = BasePlayer.FindByID(userID);
            if (!player) return 0;
            BuildingPrivlidge playerBuildingPrivilage = player.GetBuildingPrivilege();
            return playerBuildingPrivilage ? playerBuildingPrivilage.buildingID : 0;
        }

        private BaseEntity GetTurret(UInt64 userID, IOEntity electricSwitch)
        {
            if (!localRepositories.TryGetValue(userID, out List<LocalRepository> localRepository)) return null;

            foreach (LocalRepository repository in localRepository)
            {
                if (repository.ioEntity == null) continue;
                if (repository.ioEntity != electricSwitch) continue;
                return repository.turret;
            }

            return null;
        }
        
        private BaseEntity GetSwtich(UInt64 userID, BaseEntity turret)
        {
            if (!localRepositories.TryGetValue(userID, out List<LocalRepository> localRepository)) return null;

            foreach (LocalRepository repository in localRepository)
            {
                if (repository.turret == null) continue;
                if (repository.turret != turret) continue;
                return repository.ioEntity;
            }

            return null;
        }

        #endregion

        #region Toggles
        private Object HandleTurretToggle(BasePlayer player, IOEntity entity)
        {
            if (entity == null || player == null) return null; 
            if (entity.OwnerID == 0) return null;
            
            BaseEntity turret = GetTurret(entity.OwnerID, entity);
            if (turret == null) return null;
            
            if (IsRaidBlocked(player)) return false;

            if (!player.IsBuildingAuthed())
            {
                SendChat(GetLang("IS_BUILDING_BLOCK_TOGGLE", player.UserIDString), player);
                return false;
            }

            if (IsTurretElectricalTurned(turret))
            {
                if (entity.HasFlag(BaseEntity.Flags.On))
                    entity.SetFlag(BaseEntity.Flags.On, false);
        
                return false;
            }

            if (entity.HasFlag(BaseEntity.Flags.On))
            {
                TurretToggle(player, entity);
                return null;
            }

            if (config.LimitController.UseLimitControll)
            {
                if (IsLimitPlayer(entity.OwnerID, player, turret))
                {
                    SendChat(GetLang(entity.OwnerID != player.userID ? "IS_LIMIT_TRUE_OTHER" : "IS_LIMIT_TRUE", player.UserIDString), player);
                    return false;
                }
            }

            TurretToggle(player, entity);
            return null; 
        }

        private void TurretToggle(BasePlayer player, IOEntity ioEntity, Boolean useInCommand = false, Boolean primaryState = false)
        {
            if (ioEntity == null) return;

            BaseEntity turret = GetTurret(ioEntity.OwnerID, ioEntity);
            if (turret == null) return;

            Boolean IsFlag = false;

            const BaseEntity.Flags flags = BaseEntity.Flags.Reserved8;

            Boolean state = useInCommand ? primaryState : !turret.HasFlag(flags);
            turret.SetFlag(flags, state);
            
            if (turret is AutoTurret aTurret)
                aTurret.IOStateChanged(state ? 100 : 0, 0);
  
            IsFlag = turret.HasFlag(flags);
            
            if (useInCommand)
            {
                ioEntity.SetFlag(BaseEntity.Flags.On, state);
                return;
            }
            
            if (!config.LimitController.UseLimitControll) return;

           // UInt64 buildingID = GetBuildingID(player, turret);
            Int32 LimitCount = (GetLimitPlayer(ioEntity.OwnerID) -
                                GetAmountActiveTurretPlayer(ioEntity.OwnerID, player, turret));
            SendChat(
                GetLang(
                    IsFlag
                        ? (ioEntity.OwnerID != player.userID
                            ? "INFORMATION_USER_ON_OTHER"
                            : "INFORMATION_USER_ON")
                        : (ioEntity.OwnerID != player.userID
                            ? "INFORMATION_USER_OFF_OTHER"
                            : "INFORMATION_USER_OFF"), player.UserIDString, LimitCount), player);
        }

        #endregion

        #endregion

        #region Commands

        [ConsoleCommand("all.turrets")]
        private void GetAllTurrrets(ConsoleSystem.Arg arg)
        {
            if(arg.Player() != null)
                if (!arg.Player().IsAdmin)
                    return;
            
            Int32 turretServerCount = BaseNetworkable.serverEntities.Count(b => b != null && b.net != null && IDsPrefabs.Contains(b.prefabID));
            Puts($"Turret count : {turretServerCount}");
        }
        
        Boolean IsValidTurret(BaseEntity turret, String action)
        {
            if (action == "off")
                return turret.HasFlag(BaseEntity.Flags.Reserved8);

            return !turret.HasFlag(BaseEntity.Flags.Reserved8);
        }

        Boolean HasValidIoEntity(BaseEntity ioEntity, String action)
        {
            if (action == "off")
                return ioEntity != null && !ioEntity.IsDestroyed &&
                       ioEntity.HasFlag(BaseEntity.Flags.On);

            return ioEntity != null && !ioEntity.IsDestroyed &&
                   !ioEntity.HasFlag(BaseEntity.Flags.On);
        }
        
        private void TurretAction(BasePlayer player, String action)
        {
            UInt64 playerBuildingID = GetBuildingID(player, null);
            if(playerBuildingID == 0)
            {
                SendChat(GetLang("INFORMATION_USER_ACTION_COMMAND_BUILDING", player.UserIDString), player);
                return;
            }

            if (config.cooldownController.useCooldown && !action.Equals("limit"))
            {
                if (!CooldownCommanndTOnOff.TryGetValue(player, out Double cooldown) || cooldown - CurrentTime <= 0)
                    CooldownCommanndTOnOff[player] = CurrentTime + config.cooldownController.timeCooldown;
                else
                {
                    SendChat(GetLang("INFORMATION_USER_ACTION_COMMAND_COOLDOWN", player.UserIDString, cooldown - CurrentTime), player);
                    return;
                }
            }

            Int32 limitPlayer = GetLimitPlayer(player.userID);

            if (action.Equals("limit"))
            {
                String lang = GetLang("INFORMATION_MY_LIMIT", player.UserIDString, (limitPlayer - GetAmountActiveTurretPlayer(player.userID, player, null)));
                SendChat(lang, player);

                return;
            }
            
            if (localRepositories.TryGetValue(player.userID, out List<LocalRepository> localRepository))
            {
                List<LocalRepository> filteredList = new List<LocalRepository>();

                foreach (LocalRepository item in localRepository)
                {
                    UInt64 turretBuildingID = GetBuildingID(null, item.turret);
                    if (item.turret != null && turretBuildingID != 0 && playerBuildingID == turretBuildingID)
                        filteredList.Add(item);
                }

                filteredList.Sort((x, y) =>
                {
                    if (x.turret == null || y.turret == null || x.turret.IsDestroyed || y.turret.IsDestroyed) return 0;
                    
                    Boolean xFlag = IsValidTurret(x.turret, action) && HasValidIoEntity(x.ioEntity, action) && x.turret.OwnerID == player.userID && GetBuildingID(null, x.turret) == playerBuildingID;
                    Boolean yFlag = IsValidTurret(y.turret, action) && HasValidIoEntity(y.ioEntity, action) && y.turret.OwnerID == player.userID && GetBuildingID(null, y.turret) == playerBuildingID;

                    if (xFlag != yFlag) 
                        return action == "off" ? yFlag.CompareTo(xFlag) : xFlag.CompareTo(yFlag);

                    if (action == "off") return 0;

                    return xFlag.CompareTo(yFlag);
                });

                foreach (LocalRepository item in filteredList)
                {
                    if (limitPlayer < 0) break;
                    if (item.turret.IsDestroyed || item.ioEntity == null ||
                        item.ioEntity.IsDestroyed) continue;

                   // UInt64 buildingID = GetBuildingID(player, item.turret);
                    
                    switch (action)
                    {
                        case "off" when permission.UserHasPermission(player.UserIDString, PermissionTurnAllTurretsOff):
                            TurretToggle(player, item.ioEntity, true, false);
                            limitPlayer--;
                            break;
                        case "off":
                            SendChat(GetLang("PERMISSION_COMMAND_ERROR", player.UserIDString), player);
                            return;
                        case "on" when permission.UserHasPermission(player.UserIDString, PermissionTurnAllTurretsOn) &&
                                       IsLimitPlayer(player.userID, player, item.turret):
                            return;
                        case "on" when permission.UserHasPermission(player.UserIDString, PermissionTurnAllTurretsOn):
                            TurretToggle(player, item.ioEntity, true, true);
                            limitPlayer--;
                            break;
                        case "on":
                            SendChat(GetLang("PERMISSION_COMMAND_ERROR", player.UserIDString), player);
                            return;
                    }
                }
            }
        }

        [ChatCommand("t")]
        void TurretControllChatCommand(BasePlayer player, String cmd, String[] arg)
        {
            if (player == null || arg == null || arg.Length == 0)
            {
                SendChat(GetLang("SYNTAX_COMMAND_ERROR", player.UserIDString), player);
                return;
            }

            String action = arg[0];
            TurretAction(player, action);
        }

        [ConsoleCommand("t")]
        void TurretControllConsoleCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || arg == null || !arg.HasArgs(1))
            {
                SendChat(GetLang("SYNTAX_COMMAND_ERROR", player.UserIDString), player);
                return;
            }

            String action = arg.Args[0];
            TurretAction(player, action);
        }


        #endregion

        #region Lang

        private static StringBuilder sb = new StringBuilder();

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

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["IS_LIMIT_TRUE"] = ":exclamation:At you <color=#dd6363>exceeded</color> limit of active turrets <color=#dd6363>WITHOUT ELECTRICITY</color>",
                ["IS_TURRET_ELECTRIC_TRUE"] = ":exclamation:This turret is connected <color=#dd6363>to electricity</color>, you can't use the switch!",
                ["IS_BUILDING_BLOCK_TOGGLE"] = ":exclamation:You cannot use the switch in <color=#dd6363>someone else's house</color>",
                ["INFORMATION_USER_ON"] = ":heart:You have successfully <color=#66e28b>enabled</color> the turret, you can still enable <color=#dd6363>{0}</color> turret",
                ["INFORMATION_USER_OFF"] = ":heart:You have successfully <color=#dd6363>disabled</color> the turret, you can still enable <color=#dd6363>{0}</color> turret",
                ["INFORMATION_MY_LIMIT"] = ":exclamation:<color=#dd6363> is available to you</color> to enable <color=#dd6363>{0}</color> turrets",
                ["SYNTAX_COMMAND_ERROR"] = ":exclamation:<color=#dd6363>Syntax error : </color>\nUse the commands :\n1. t on - enables all disabled turrets\n2. t off - turns off all enabled turrets\n3. t limit - shows how many turrets are still available to you without electricity",
                ["PERMISSION_COMMAND_ERROR"] = ":exclamation:<color=#dd6363>Access error : </color>\nYou don't have enough rights to use this command!",
                ["INFORMATION_WEAPON_MODS"] = ":heart:Weapon mods support has been added. You can now add attachments like extended mags, flashlights, etc.",

                ["IS_LIMIT_TRUE_OTHER"] = ":exclamation:The owner of the turret <color=#dd6363>exceeded</color> limit of active turrets <color=#dd6363>WITHOUT ELECTRICITY</color>",
                ["INFORMATION_USER_ON_OTHER"] = ":heart:You have successfully <color=#66e28b>enabled</color> the player's turret, the player can still turn on <color=#dd6363>{0}</color> turret",
                ["INFORMATION_USER_OFF_OTHER"] = ":heart:You have successfully <color=#dd6363>disabled</color> тthe player's turret, the player is still available for inclusion <color=#dd6363>{0}</color> turret",
                ["INFORMATION_USER_ACTION_COMMAND_BUILDING"] = ":exclamation:To use this command, you must be within the range of a cupboard",
                ["INFORMATION_USER_ACTION_COMMAND_COOLDOWN"] = ":exclamation:Cooldown for using the command, please wait another <color=#dd6363>{0}</color> seconds",

            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["IS_LIMIT_TRUE"] = ":exclamation:У вас <color=#dd6363>превышен</color> лимит активных турелей <color=#dd6363>БЕЗ ЭЛЕКТРИЧЕСТВА</color>",
                ["IS_TURRET_ELECTRIC_TRUE"] = ":exclamation:Данная турель подключена <color=#dd6363>к электричеству</color>, вы не можете использовать рубильник!",
                ["IS_BUILDING_BLOCK_TOGGLE"] = ":exclamation:Вы не можете использовать рубильник в <color=#dd6363>чужом доме</color>",
                ["INFORMATION_USER_ON"] = ":heart:Вы успешно <color=#66e28b>включили</color> турель, вам доступно еще для включения <color=#dd6363>{0}</color> турели",
                ["INFORMATION_USER_OFF"] = ":heart:Вы успешно <color=#dd6363>выключили</color> турель, вам доступно еще для включения <color=#dd6363>{0}</color> турели",
                ["INFORMATION_MY_LIMIT"] = ":exclamation:Вам <color=#dd6363>доступно</color> для включения <color=#dd6363>{0}</color> турелей",
                ["SYNTAX_COMMAND_ERROR"] = ":exclamation:<color=#dd6363>Ошибка синтаксиса : </color>\nИспользуйте команды :\n1. t on - включает все выключенные\n2. t off - выключает все включенные турели\n3. t limit - показывает сколько вам еще доступно турелей без электричества",
                ["PERMISSION_COMMAND_ERROR"] = ":exclamation:<color=#dd6363>Ошибка доступа : </color>\nУ вас недостаточно прав для использования данной команды!",
                ["INFORMATION_WEAPON_MODS"] = ":heart:Добавлена поддержка модификаций для оружия. Теперь вы можете добавлять такие аксессуары, как увеличенные магазины, фонарики и т.д.",

                ["IS_LIMIT_TRUE_OTHER"] = ":exclamation:У владельца турели <color=#dd6363>превышен</color> лимит активных турелей <color=#dd6363>БЕЗ ЭЛЕКТРИЧЕСТВА</color>",
                ["INFORMATION_USER_ON_OTHER"] = ":heart:Вы успешно <color=#66e28b>включили</color> турель игрока, игроку доступно еще для включения <color=#dd6363>{0}</color> турели",
                ["INFORMATION_USER_OFF_OTHER"] = ":heart:Вы успешно <color=#dd6363>выключили</color> турель игрока, игрока доступно еще для включения <color=#dd6363>{0}</color> турели",
                ["INFORMATION_USER_ACTION_COMMAND_BUILDING"] = ":exclamation:Для использования этой команды вы должны находиться в зоне действия шкафа",
                ["INFORMATION_USER_ACTION_COMMAND_COOLDOWN"] = ":exclamation:Перезарядка на использование команды, подождите еще <color=#dd6363>{0}</color> секунд",

            }, this, "ru");
            PrintWarning(LanguageEn ? "Logs: #832545 | Language file loaded successfully" : "Logs : #832545 | Языковой файл загружен успешно");
        }

        #endregion

        #region API

        private Boolean API_IS_TURRETLIST(ElectricSwitch electricSwitch)
        {
            BaseEntity turret = GetTurret(electricSwitch.OwnerID, electricSwitch);
            return turret != null;
        }
        
        private Boolean API_IS_TURRETLIST(AutoTurret turret)
        {
            BaseEntity switchTurret = GetSwtich(turret.OwnerID, turret);
            return switchTurret != null;
        }

        #endregion
    }
}