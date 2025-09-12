using System;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Plugins.TrainHomesExtensionMethods;
using Facepunch;
using System.Reflection;
using Newtonsoft.Json;
using Oxide.Core;
using System.Threading.Tasks;
using Oxide.Core.Plugins;
using Network;
using System.Collections;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Libraries;

namespace Oxide.Plugins
{
    [Info("TrainHomes", "jtedal", "1.1.0")]
    class TrainHomes : RustPlugin
    {
        private const bool En = false;

        #region Config

        private PluginConfig _config;

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a default config...");
            _config = PluginConfig.DefaultConfig();
            _config.PluginVersion = Version;
            SaveConfig();
            Puts("Creation of the default config completed!");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
            if (_config.PluginVersion < Version)
            {
                UpdateConfigValues();
            }
        }

        private void UpdateConfigValues()
        {
            Puts("Config update detected! Updating config values...");
            if (_config.PluginVersion < new VersionNumber(1, 0, 1))
            {
                _config.PermissionsForAmountWagons = new Dictionary<string, int>()
                {
                    ["trainhomes.amount1"] = 1,
                    ["trainhomes.amount10"] = 10
                };
            }
            if (_config.PluginVersion < new VersionNumber(1, 0, 5))
            {
                _config.CheckTopology = false;
            }
            if (_config.PluginVersion < new VersionNumber(1, 0, 7))
            {
                _config.Log = true;
            }
            _config.PluginVersion = Version;
            Puts("Config update completed!");
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        public class NotifyConfig
        {
            [JsonProperty(En ? "Prefix" : "Префикс сообщений")] public string prefix { get; set; }
            [JsonProperty(En ? "Chat Message setting" : "Настройки сообщений в чате")] public ChatConfig chatConfig { get; set; }
            [JsonProperty(En ? "Facepunch Game Tips setting" : "Настройка сообщений Facepunch Game Tip")] public GameTipConfig gameTipConfig { get; set; }
            [JsonProperty(En ? "GUI Announcements setting (only for GUIAnnouncements plugin)" : "Настройка GUI Announcements (только для тех, кто использует плагин GUI Announcements)")] public GUIAnnouncementsConfig guiAnnouncementsConfig { get; set; }
            [JsonProperty(En ? "Notify setting (only for Notify plugin)" : "Настройка Notify (только для тех, кто использует плагин Notify)")] public NotifyPluginConfig notifyPluginConfig { get; set; }
            [JsonProperty(En ? "Discord setting (only for DiscordMessages plugin)" : "Настройка оповещений в Discord (только для тех, кто использует плагин Discord Messages)")] public DiscordMessagesConfig discordMessagesConfig { get; set; }
        }

        public class ChatConfig
        {
            [JsonProperty(En ? "Use chat notifications? [true/false]" : "Использовать ли чат? [true/false]")] public bool isEnabled { get; set; }
        }

        public class GameTipConfig
        {
            [JsonProperty(En ? "Use Facepunch Game Tips (notification bar above hotbar)? [true/false]" : "Использовать ли Facepunch Game Tip (оповещения над слотами быстрого доступа игрока)? [true/false]")] public bool isEnabled { get; set; }
            [JsonProperty(En ? "Style (0 - Blue Normal, 1 - Red Normal, 2 - Blue Long, 3 - Blue Short, 4 - Server Event)" : "Стиль (0 - Blue Normal, 1 - Red Normal, 2 - Blue Long, 3 - Blue Short, 4 - Server Event)")] public int style { get; set; }
        }

        public class GUIAnnouncementsConfig
        {
            [JsonProperty(En ? "Do you use GUI Announcements integration? [true/false]" : "Использовать ли GUI Announcements? [true/false]")] public bool isEnabled { get; set; }
            [JsonProperty(En ? "Banner color" : "Цвет баннера")] public string bannerColor { get; set; }
            [JsonProperty(En ? "Text color" : "Цвет текста")] public string textColor { get; set; }
            [JsonProperty(En ? "Adjust Vertical Position" : "Отступ от верхнего края")] public float apiAdjustVPosition { get; set; }
        }

        public class NotifyPluginConfig
        {
            [JsonProperty(En ? "Do you use Notify integration? [true/false]" : "Использовать ли Notify? [true/false]")] public bool isEnabled { get; set; }
            [JsonProperty(En ? "Type" : "Тип")] public string type { get; set; }
        }

        public class DiscordMessagesConfig
        {
            [JsonProperty(En ? "Do you use DiscordMessages? [true/false]" : "Использовать ли DiscordMessages? [true/false]")] public bool isEnabled { get; set; }
            [JsonProperty("Webhook URL")] public string webhookUrl { get; set; }
            [JsonProperty(En ? "Embed Color (DECIMAL)" : "Цвет полосы (DECIMAL)")] public int embedColor { get; set; }
            [JsonProperty(En ? "Keys of required messages" : "Ключи необходимых сообщений")] public HashSet<string> keys { get; set; }
        }

        public class AcceptableEntities
        {
            [JsonProperty("ShortName")] public string ShortName { get; set; }
            [JsonProperty(En ? "Allow this entity to be placed" : "Разрешить ставить этот предмет")] public bool IsAllowPut { get; set; }
            [JsonProperty(En ? "The maximum amount that can be placed in one wagon (If previous parameter true)" : "Максимальное количество, которое можно ставить в одном вагоне (Если стоит true)")] public int Amount { get; set; }
        }

        public class PermissionsConfig
        {
            [JsonProperty(En ? "Permission name (Example: trainhomes.name)" : "Название пермишена (Пример: trainhomes.name)")] public string Name { get; set; }
        }

        public class CrateConfig
        {
            [JsonProperty("Prefab")] public string Prefab { get; set; }
            [JsonProperty(En ? "Chance [0.0-100.0]" : "Шанс выпадения предмета [0.0-100.0]")] public float Chance { get; set; }
        }

        public class PluginConfig
        {
            [JsonProperty(En ? "Record plugin logs" : "Записывать логи плагина")] public bool Log { get; set; }
            [JsonProperty(En ? "Allow items to be placed on the roof of the wagon" : "Разрешить ставить предметы на крыше поезда")] public bool IsAllowPutOnRoof { get; set; }
            [JsonProperty(En ? "Enable topology checking when a player calls a wagon onto the tracks" : "Включить проверку топологий, когда игрок вызывает вагон на рельсы")] public bool CheckTopology { get; set; }
            [JsonProperty(En ? "The maximum distance at which a wagon can be moved" : "Максимальная дистанция при которой вагон может быть перемещен")] public int MaxDistance { get; set; }
            [JsonProperty(En ? "HP of the wagon" : "HP вагона")] public int HpWagon { get; set; }
            [JsonProperty(En ? "HP of the wagon stand" : "HP подставки")] public int HpBarricade { get; set; }
            [JsonProperty(En ? "A constant amount of free wagons on the tracks" : "Постоянное количество бесплатных вагонов на рельсах")] public int amountFreeWagons { get; set; }
            [JsonProperty(En ? "Wagon spawn settings in crates" : "Настройка появления вагона в ящиках")] public HashSet<CrateConfig> Crates { get; set; }
            [JsonProperty(En ? "Whether to use permits for the number of wagons" : "Использовать ли пермишены на кол-во вагонов")] public bool AmountWagons { get; set; }
            [JsonProperty(En ? "Permissions for amount wagons in using" : "Пермишены на кол-во вагонов для игрока")] public Dictionary<string, int> PermissionsForAmountWagons { get; set; }
            [JsonProperty(En ? "List of entities" : "Список Entity")] public HashSet<AcceptableEntities> AcceptableEntities { get; set; }
            [JsonProperty(En ? "Notification Settings" : "Настройки уведомлений")] public NotifyConfig NotifyConfig { get; set; }
            [JsonProperty(En ? "Configuration Version" : "Версия конфигурации")] public VersionNumber PluginVersion { get; set; }
            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    Log = true,
                    IsAllowPutOnRoof = true,
                    CheckTopology = false,
                    MaxDistance = 1500,
                    HpWagon = 5000,
                    HpBarricade = 1000,
                    amountFreeWagons = 5,
                    Crates = new HashSet<CrateConfig>()
                    {
                        new CrateConfig { Prefab = "assets/bundled/prefabs/radtown/crate_normal.prefab", Chance = 100f },
                        new CrateConfig { Prefab = "assets/bundled/prefabs/radtown/crate_elite.prefab", Chance = 50f }
                    },
                    AmountWagons = false,
                    PermissionsForAmountWagons = new Dictionary<string, int>()
                    {
                        ["trainhomes.amount1"] = 1,
                        ["trainhomes.amount10"] = 10
                    },                    
                    AcceptableEntities = new HashSet<AcceptableEntities>()
                    {
                        new AcceptableEntities{ ShortName = "box.wooden.large", IsAllowPut = true, Amount = 100 }
                    },
                    NotifyConfig = new NotifyConfig
                    {
                        prefix = "[TrainHomes]",
                        chatConfig = new ChatConfig
                        {
                            isEnabled = true,
                        },
                        gameTipConfig = new GameTipConfig
                        {
                            isEnabled = false,
                            style = 2,
                        },
                        guiAnnouncementsConfig = new GUIAnnouncementsConfig
                        {
                            isEnabled = false,
                            bannerColor = "Grey",
                            textColor = "White",
                            apiAdjustVPosition = 0.03f
                        },
                        notifyPluginConfig = new NotifyPluginConfig
                        {
                            isEnabled = false,
                            type = "0"
                        },
                        discordMessagesConfig = new DiscordMessagesConfig
                        {
                            isEnabled = false,
                            webhookUrl = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks",
                            embedColor = 13516583,
                            keys = new HashSet<string>
                            {
                                "PreStart",
                                "EventStart",
                                "Finish"
                            }
                        }
                    },
                    PluginVersion = new VersionNumber()
                };
            }
        }

        #endregion Config

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BaseHasntCupboard"] = "{0} You can't do this because there is no cupboard.",
                ["NotAuthedInCupboard"] = "{0} You can't do this because you're not authorized in the cupboard.",
                ["NoNeedAmountBlocks"] = "{0} Your base must have at least 21 square foundations. (Details: /thinstruction)",
                ["NotValidPosForTrainCar"] = "{0} TrainCar cannot be spawned in this position.",
                ["CanntPutEntityOnRoof"] = "{0} You can't put objects on the roof.",
                ["CanntPutEntity"] = "{0} You can't put this object on the wagon.",
                ["SetMaxAmountObjects"] = "{0} You have set the maximum amount of these objects on the wagon.",
                ["LimitFloorsForHouse"] = "{0} Building blocks cannot be outside the wagon or above the 1st floor.",
                ["CodeOfTheWagon"] = "{0} Your code from the wagon [{1}]. You can change these parameters.",
                ["FrequencyOfTheWagon"] = "{0} Your frequency from the wagon [{1}]. You can change this parameter.",
                ["LongDistance"] = "{0} The distance from the wagon to the spawn point is too long. (Distance: {1}m, Maximum distance: {2}m)",
                ["TwoCallers"] = "{0} When calling a wagon, the detonator must be in the hand of only 1 player.",
                ["WagonAlreadyInGarage"] = "{0} The wagon is already in another base.",
                ["WagonAlreadyOnTracks"] = "{0} The wagon is already on tracks.",
                ["NotValidTopologies"] = "{0} There are no topologies for spawn in this position. Try to spawn the wagon in another position or contact the server owner.",
                ["TimerForSpawn"] = "{0} The wagon will spawn in {1}...",
                ["NotValidConstruction"] = "{0} Incorrect сonstruction for the wagon spawn. (Details: /thinstruction)",
                ["NotValidTarget"] = "{0} Incorrect сonstruction for the wagon spawn or cannot be spawned in this position. (Подробности: /thinstruction)",
                ["FoundationsExistInHashSet"] = "{0} You are trying to spawn the wagon in a place where there is already a position for the wagon. (Подробности: /thinstruction)",
                ["CommandForAdminsOnly"] = "{0} This command is for admins only.",
                ["RemoveTimeEnd"] = "{0} The time for removing the wagon is over.",
                ["CantCallAnotherWagon"] = "{0} Wait until the other wagon is spawned.",
                ["WagonAlreadyCalled"] = "{0} This wagon has already been called by another player.",
                ["WagonOnMonument"] = "{0} This wagon is located on the custom monument, in building mode. You can't move it.",
                ["ModeIsNotBuilding"] = "{0} Turn on the monument building mode using the button on the pillar.",
                ["NotOnBaseAndNotOnMonument"] = "{0} You can't build on a wagon not on base and not on a special custom monument.",
                ["MuchWagons"] = "{0} To enable building mode, there should not be more than 1 wagon inside the monument.",
                ["NotCustomWagon"] = "{0} To enable building mode, there should be a custom wagon inside the monument.",
                ["NotHaveWagons"] = "{0} To enable building mode, there should be a wagon inside the monument.",
                ["InvalidCmdChat1"] = "{0} You <color=#ce3f27>didn't</color> write amount and player's SteamID! \n/givewagon <amount> <SteamID>",
                ["InvalidCmdChat2"] = "{0} You <color=#ce3f27>didn't</color> write some player's SteamID! \n/givewagon <amount> <SteamID>",
                ["InvalidCmdChat3"] = "{0} Player with SteamID: {1} <color=#ce3f27>not found</color>!",
                ["ValidCmdChat"] = "{0} The wagon was successfully given to the player!",
                ["YouNotOwner"] = "{0} You can't do this because you are not the owner of the wagon!",
                ["CantBuildingOnFreeWagon"] = "{0} You can't build on the free wagon! Set the code for the codelock and try again.",
                ["NotHavePerm"] = "{0} You dont have permission to use this command.",
                ["NotHavePermForMoreWagons"] = "{0} You do not have perms to own a larger number of wagons.",

                ["GUI_General"] = "General",
                ["GUI_General_Description"] = "1) In the crates, you can find an item for the wagon spawn." +
                "\n2) On the railway tracks, occasionally, you can find an free wagon. To become its owner, enter any code." +
                "\n3) You can only place the wagon at your base. For details, see the section 'Construction'." +
                "\n4) You can build on a wagon only at your base or on a custom monument, if there is one. For more information, see the section 'Custom monument'." +
                "\n5) Any player can build on the wagon." +
                "\n6) There is no need to install a cupboard on the wagon, because the objects on it do not decay, as well as the wagon itself." +
                "\n7) To move the wagon from the base to the rails and back, you will need a detonator. Enter the frequency that is indicated on the wagon, stand in front of the place where you want to move it and press the button." +
                "\n8) All players who are authorized in the codelock have access to the wagon movement." +
                "\n9) Any player can change the frequency of the wagon." +
                "\n10) The owner of the wagon can remove his wagon and get a spawn item in return. Be careful! All objects on it will destroy, and resources will not be returned. (Command: /removewagon)",

                ["GUI_Construction"] = "Construction",
                ["GUI_Construction_Description"] = "1) The сonstruction must contain 7x3 foundations." +
                "\n2) To spawn the wagon, you must definitely put a cupboard." +
                "\n3) All foundations must be at the same height." +
                "\n4) If a cupboard, or one of the 21 foundations, or a stand breaks down on the сonstruction while the wagon is located, then the one will also be destroyed." +
                "\n5) To place a wagon, you must strictly specify the foundation that is the most central of the 21." +
                "\n6) The wagon must not be placed on foundations other than those.",

                ["GUI_CustomMonument"] = "Custom Monument",
                ["GUI_CustomMonument_Description"] = "1) You will not be able to build anything on the wagon until you activate the building mode." +
                "\n2) The construction mode is activated by pressing a button on one of the pillars." +
                "\n3) After pressing the button, the wagon will automatically reach the desired position if it is not entirely located in the construction area." +
                "\n4) Be careful! During the building mode, any player will be able to build anything on your wagon." +
                "\n5) To exit the building mode, press the button again.",

            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BaseHasntCupboard"] = "{0} Вы не можете это сделать, потому что нет шкафа.",
                ["NotAuthedInCupboard"] = "{0} Вы не можете это сделать, потому что не авторизованы в шкафу.",
                ["NoNeedAmountBlocks"] = "{0} У вашей постройки должно быть минимум 21 квадратных фундаментов. (Подробности: /thinstruction)",
                ["NotValidPosForTrainCar"] = "{0} В этой позиции нельзя заспавнить вагон.",
                ["CanntPutEntityOnRoof"] = "{0} Нельзя ставить объекты на крыше.",
                ["CanntPutEntity"] = "{0} Нельзя ставить этот объект на поезде.",
                ["SetMaxAmountObjects"] = "{0} Вы установили максимальное количество этих объектов на вагоне.",
                ["LimitFloorsForHouse"] = "{0} Строительные блоки не могут выходить за пределы одноэтажной конструкции.",
                ["CodeOfTheWagon"] = "{0} Ваш код от вагона [{1}]. Вы можете изменить этот параметр.",
                ["FrequencyOfTheWagon"] = "{0} Ваша частота от вагона [{1}]. Вы можете изменить этот параметр.",
                ["LongDistance"] = "{0} Дистанция от вагона до точки спавна слишком большая. (Дистанция: {1}м, Максимальная дистанция: {2}м)",
                ["TwoCallers"] = "{0} При вызове вагона, детонатор должен быть в руке только 1 игрока.",
                ["WagonAlreadyInGarage"] = "{0} Вагон уже находится на другой базе.",
                ["WagonAlreadyOnTracks"] = "{0} Вагон уже находится на рельсах.",
                ["NotValidTopologies"] = "{0} В этой позиции нет топологий для спавна. Попробуйте заспавнить вагон в другом месте или обратитесь к владельцу сервера.",
                ["TimerForSpawn"] = "{0} Вагон появится через {1}...",
                ["NotValidConstruction"] = "{0} Неправильная конструкция для спавна вагона. (Подробности: /thinstruction)",
                ["NotValidTarget"] = "{0} Неправильная конструкция или позиция для спавна вагона. (Подробности: /thinstruction)",
                ["FoundationsExistInHashSet"] = "{0} Вы пытаетесь заспавнить вагон в том месте, где уже есть позиция для вагона. (Подробности: /thinstruction)",
                ["CommandForAdminsOnly"] = "{0} Эта команда только для админов.",
                ["RemoveTimeEnd"] = "{0} Время для удаления вагона истекло.",
                ["CantCallAnotherWagon"] = "{0} Дождитесь пока другой вагон будет заспавнен.",
                ["WagonAlreadyCalled"] = "{0} Этот вагон уже вызвал другой игрок.",
                ["WagonOnMonument"] = "{0} Этот вагон находится на монументе, в режиме строитедства. Вы не можете его перемещать.",
                ["ModeIsNotBuilding"] = "{0} Включите режим строителтсва на монументе через кнопку на столбе.",
                ["NotOnBaseAndNotOnMonument"] = "{0} Вы не можете строить на вагоне не дома и не на специальном кастомном монументе.",
                ["MuchWagons"] = "{0} Чтобы включить режим редактирования, внутри монумента не должно быть более 1 вагона.",
                ["NotCustomWagon"] = "{0} Чтобы включить режим редактирования, внутри монумента должен быть вагон для строительста.",
                ["NotHaveWagons"] = "{0} Чтобы включить режим редактирования, внутри монумента должен быть вагон.",
                ["InvalidCmdChat1"] = "{0} Вы <color=#ce3f27>не написали</color> количество и SteamID игрока! \n/givewagon <amount> <SteamID>",
                ["InvalidCmdChat2"] = "{0} Вы <color=#ce3f27>не написали</color> SteamID игрока! \n/givewagon <amount> <SteamID>",
                ["InvalidCmdChat3"] = "{0} Игрок с SteamID: {1} <color=#ce3f27>не найден</color>",
                ["ValidCmdChat"] = "{0} Вагон был удачно выдан игоку!",
                ["YouNotOwner"] = "{0} Вы не можете это сделать потому что не являетесь владельцем вагона!",
                ["CantBuildingOnFreeWagon"] = "{0} Строиться на свободном вагоне нельзя! Введите код для кодлока и повторите попытку.",
                ["NotHavePerm"] = "{0} У вас нет разрешений на использование этой команды.",
                ["NotHavePermForMoreWagons"] = "{0} У вас нет разрешений на владение большего кол-ва вагонов.",

                ["GUI_General"] = "Общее",
                ["GUI_General_Description"] = "1) В ящиках, можно найти предмет для спавна вагона." +
                "\n2) На железнодорожных путях, изредка, можно встретить свободный вагон. Чтобы стать его владельцем, введите любой код." +
                "\n3) Разместить вагон вы можете только у себя на базе. Подробности смотрите в разделе 'Конструкция'." +
                "\n4) Строиться на вагоне вы можете только у себя на базе или на кастомном монументе, если он есть. Подробности смотрите в разделе ‘Кастом монумент’." +
                "\n5) Строиться на вагоне может любой игрок." +
                "\n6) На вагоне не нужно устанавливать шкаф, поскольку объекты на нем не гниют, также как и сам вагон." +
                "\n7) Для перемещения вагона с базы на рельсы и обратно, вам понадобиться детонатор. Введите частоту, которая указана на вагоне, встаньте перед тем место, куда хотите его переместить и нажмите кнопку." +
                "\n8) Доступ к перемещению вагона имеют все игроки, которые авторизованы в кодлоке." +
                "\n9) Частоту вагона может поменять любой игрок." +
                "\n10) Владелец вагона может удалить свой вагон и взамен получить предмет для спавна. Будьте осторожны! Все объекты на нем пропадут, а ресурсы возвращены не будут. (Команда: /removewagon)",

                ["GUI_Construction"] = "Конструкция",
                ["GUI_Construction_Description"] = "1) Конструкция должна содержать 7x3 фундамента." +
                "\n2) Для размещения вагона вы обязательно должны поставить шкаф." +
                "\n3) Все фундаменты должны быть на одной высоте." +
                "\n4) Если во время нахождения вагона на конструкции сломается шкаф, или один из 21 фундамента, или подставка, то вагон также уничтожится." +
                "\n5) Для размещения вагона, вы должны строго указывать тот фундамент, который самый центральный из 21." +
                "\n6) Вагон нельзя размещать не на фундаментах.",

                ["GUI_CustomMonument"] = "Кастом монумент",
                ["GUI_CustomMonument_Description"] = "1) Вы не сможете что либо построить на вагоне пока не активируете режим строительства." +
                "\n2) Активация режима строительства происходит путем нажатия кнопки на одном из столбов." +
                "\n3) После нажатия кнопки, вагон автоматически доедет до нужной позиции, если он целиком не находится в зоне строительства" +
                "\n4) Будьте осторожны! Во время режима строительства, любой игрок сможет построить что либо на вашем вагоне." +
                "\n5) Чтобы выйти из режима строительства, нажмите еще раз на кнопку.",

            }, this, "ru");
        }

        private static string GetMessage(string langKey, string userID) => ownIns.lang.GetMessage(langKey, ownIns, userID);

        private static string GetMessage(string langKey, string userID, params object[] args) => (args.Length == 0) ? GetMessage(langKey, userID) : string.Format(GetMessage(langKey, userID), args);

        #endregion Lang

        #region GUI

        [ConsoleCommand("Close_GUI")]
        void DestroyGUI(ConsoleSystem.Arg arg) => GUIManager.DestroyGUI(arg.Player());

        [ConsoleCommand("GUI_General")]
        void CreateGUI_General(ConsoleSystem.Arg arg) => GUIManager.CreateGUI(arg.Player(), GUIManager.GUITopic.General);

        [ConsoleCommand("GUI_Construction")]
        void CreateGUI_Construction(ConsoleSystem.Arg arg) => GUIManager.CreateGUI(arg.Player(), GUIManager.GUITopic.Construction);

        [ConsoleCommand("GUI_CustomMonument")]
        void CreateGUI_CustomMonument(ConsoleSystem.Arg arg) => GUIManager.CreateGUI(arg.Player(), GUIManager.GUITopic.CustomMonument);

        #endregion GUI

        #region Data System

        private void WriteData()
        {
            CreateData();
            Interface.Oxide.DataFileSystem.WriteObject("MM_Data/TrainHomes/Wagons", dataForWagons);
            Interface.Oxide.DataFileSystem.WriteObject("MM_Data/TrainHomes/Garages", dataForGarages);
        }

        private void LoadFiles()
        {
            dataForWagons = Interface.Oxide.DataFileSystem.ReadObject<HashSet<WagonData>>("MM_Data/TrainHomes/Wagons");
            dataForGarages = Interface.Oxide.DataFileSystem.ReadObject<HashSet<GarageData>>("MM_Data/TrainHomes/Garages");

            LoadDataForWagon();
            LoadDataForGarage();
        }

        void LoadDataForWagon()
        {
            foreach (WagonData wagonData in dataForWagons)
            {
                Wagon wagon = new Wagon();
                bool isValidData = true;
                wagon.InitWithData(wagonData, ref isValidData);

                if (isValidData) wagons.Add(wagon);
                else
                {
                    wagon.RemoveWagon();
                    wagon = null;
                }
            }
        }

        void LoadDataForGarage()
        {
            foreach (GarageData garageData in dataForGarages)
            {
                BuildingPrivlidge cupboard = null;
                foreach (var entity in BaseNetworkable.serverEntities)
                {
                    if (entity.net.ID.Value == garageData.idCupboard)
                    {
                        cupboard = entity as BuildingPrivlidge;
                        break;
                    }
                }

                Garage garage = new Garage();
                garage.InitWithData(garageData);

                if (cupboard == null)
                {
                    garage.DestroyAllWagons();
                    continue;
                }

                if (garage.wagons.Count > 0) dicGarages.Add(cupboard, garage);
                else garage = null;
            }
        }

        void CreateData()
        {
            dataForWagons.Clear();
            dataForGarages.Clear();

            CreateDataForWagon();
            CreateDataForGarage();
        }

        void CreateDataForWagon()
        {
            foreach (Wagon wagon in wagons.ToHashSet())
            {
                if (wagon == null || wagon.freeWagon) continue;
                if (wagon.baseWagon != null) wagon.CreateObjectForUnload();
                WagonData data = wagon.CreateInfoForData();
                wagon.UpdateForUnload();
                dataForWagons.Add(data);
            }
        }

        void CreateDataForGarage()
        {
            foreach (KeyValuePair<BuildingPrivlidge, Garage> garage in dicGarages)
            {
                GarageData data = garage.Value.CreateInfoForData(garage.Key.net.ID.Value);
                dataForGarages.Add(data);
            }
        }

        #endregion Data System

        #region Oxide Hooks

        private void Init()
        {
            ownIns = this;
            PermissionManager.RegisterPermissions();
        }

        private void Unload()
        {
            WriteData();
            DestroyAllCustomMonuments();
            dicGarages.Clear();
            ControllerSpawnsFreeWagon.Unload();

            ownIns = null;
        }

        private void OnServerInitialized()
        {
            PluginUpdateManager.CheckPluginUpdate();
            LoadFiles();

            DetermineSetingsEntities();

            ControllerSpawnsFreeWagon.Init();
            DetermineAllMonumentsForWagon();
        }

        object OnEntityKill(BaseEntity entity)
        {
            if (entity.net == null) return null;
            ulong netId = entity.net.ID.Value;
            
            if (entity is BaseCombatEntity)
            {
                if (allBaseWagons.ContainsKey(netId)) OnWagonKill(allBaseWagons[netId]);
                if (allBaseBarricades.ContainsKey(netId)) OnWagonKill(allBaseBarricades[netId]);
            }
            
            OnTrainCarKill(entity as TrainCar, netId);
            
            OnBuildingBlockKill(entity as BuildingBlock, netId);
            
            OnCupboardKill(entity as BuildingPrivlidge);
            
            if (allBaseEntityByWagons.ContainsKey(netId))
            {
                Wagon wagon = allBaseEntityByWagons[netId];
                wagon.RemoveEntityInLists(entity);
                allBaseEntityByWagons.Remove(netId);
            }
            
            return null;
        }

        void OnWagonKill(Wagon wagon)
        {
            wagon.RemoveWagon();
            wagon = null;
        }

        void OnTrainCarKill(TrainCar trainCar, ulong netId)
        {
            if (trainCar == null) return;

            if (onServerInit) ControllerSpawnsFreeWagon.RemoveFreeWagon(trainCar);
            if (allTrainCars.ContainsKey(netId)) OnWagonKill(allTrainCars[netId]);
        }

        void OnBuildingBlockKill(BuildingBlock block, ulong netId)
        {
            if (block == null) return;
            if (block.PrefabName != "assets/prefabs/building core/foundation/foundation.prefab" || !allFoundationsUnderWagons.ContainsKey(netId)) return;
            Garage garage = allFoundationsUnderWagons[netId];
            Vector3 pos = garage.GetPosWagon(block);
            Wagon wagon = garage.GetWagon(pos);
            wagon.RemoveWagon();
        }

        void OnCupboardKill(BuildingPrivlidge cupboard)
        {
            if (cupboard == null) return;
            if (!dicGarages.ContainsKey(cupboard)) return;
            Garage garage = dicGarages[cupboard];
            garage.DestroyAllWagons();
            dicGarages.Remove(cupboard);
            garage = null;
        }

        object OnConstructionPlace(BaseEntity baseEntity, Construction construction, Construction.Target target, BasePlayer player)
        {
            NextTick(() => CheckBaseEntity(baseEntity, construction, target, player));
            return null;
        }

        object CanPickupLock(BasePlayer player, CodeLock codelock)
        {
            if (allCodeLocks.Contains(codelock)) return true;
            return null;
        }

        object OnEntityTakeDamage(BaseCombatEntity baseCombatEntity, HitInfo info)
        {
            if (baseCombatEntity == null || info == null || baseCombatEntity.net == null) return null;

            if (_config.Log) LogManager.AddLogDamage(baseCombatEntity, info);

            return null;
        }

        object OnEntityTakeDamage(BuildingBlock entity, HitInfo info)
        {
            if (entity == null || entity.net == null) return null;
            if (entity.PrefabName == "assets/prefabs/building core/foundation.steps/foundation.steps.prefab" && allObjectsOfWalls.Contains(entity.net.ID.Value)) return true;
            return null;
        }

        object OnEntityTakeDamage(LootContainer container, HitInfo info)
        {
            if (container == null || container.net == null) return null;
            if (allObjectsOfWalls.Contains(container.net.ID.Value)) return true;
            return null;
        }

        object OnEntityTakeDamage(Barricade barricade, HitInfo info)
        {
            if (barricade == null || barricade.net == null) return null;
            if (allObjectsOfWalls.Contains(barricade.net.ID.Value)) return true;
            return null;
        }

        object OnEntityTakeDamage(NeonSign neonSign, HitInfo info)
        {
            if (neonSign == null || neonSign.net == null) return null;
            if (allObjectsOfWalls.Contains(neonSign.net.ID.Value)) return true;
            return null;
        }

        object OnEntityTakeDamage(RFReceiver receiver, HitInfo info)
        {
            if (receiver == null) return null;
            if (allReceivers.Contains(receiver)) return true;
            return null;
        }

        object OnEntityTakeDamage(RANDSwitch randSwitch, HitInfo info)
        {
            if (randSwitch == null) return null;
            if (allSwitches.Contains(randSwitch)) return true;
            return null;
        }

        object OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
        {
            if (entity == null || entity.net == null) return null;
            if (!allBaseWagons.ContainsKey(entity.net.ID.Value)) return null;
            if (!playersWhoWantsToRemoveBaseWagon.Contains(player)) return null;
            Item item = ItemManager.CreateByName("electric.flasherlight", 1, SkindIdFlasher);
            MoveItem(player, item);

            Wagon wagon = allBaseWagons[entity.net.ID.Value];
            if (wagon.owner == player)
            {
                wagon.RemoveWagon();
                wagon = null;
            }
            else NotifyManager.SendMessageToPlayer(player, "YouNotOwner", ownIns._config.NotifyConfig.prefix);
            return true;
        }

        object OnButtonPress(PressButton button, BasePlayer player)
        {
            if (button == null || !player.IsPlayer()) return null;
            CustomMonument monument;
            if (buttonsOnMonuments.TryGetValue(button, out monument)) monument.TryUpdateState(player);
            return null;
        }

        private void OnLootSpawn(LootContainer container)
        {
            if (container == null || container.inventory == null) return;
            CrateConfig config = _config.Crates.FirstOrDefault(x => x.Prefab == container.PrefabName);
            if (config == null) return;
            if (UnityEngine.Random.Range(0f, 100f) > config.Chance) return;

            if (container.inventory.itemList.Count == container.inventory.capacity) container.inventory.capacity++;
            Item wagon = ItemManager.CreateByName("electric.flasherlight", 1, SkindIdFlasher);
            if (!wagon.MoveToContainer(container.inventory)) wagon.Remove();
        }

        object CanChangeCode(BasePlayer player, CodeLock codeLock, string newCode, bool isGuestCode)
        {
            if (allCodeLocks.Contains(codeLock))
            {
                ModificationsWagon modifications = codeLock.GetParentEntity() as ModificationsWagon;
                if (modifications != null)
                {
                    if (!CanHaveMoreWagons(player)) return true;
                    if (!amountWagons.ContainsKey(player)) amountWagons.Add(player, 0);
                    amountWagons[player]++;
                    NextTick(() => modifications.ChangeStateToLocked(player));
                }
            }
            return null;
        }

        void OnEntitySpawned(TrainCar trainCar)
        {
            NextTick(() => ControllerSpawnsFreeWagon.TryAddNewTrainCar(trainCar));
        }

        #endregion Oxide Hooks

        #region RustEdit Hooks

        void RustEdit_OnMapDataProcessed() => DetermineAllMonumentsForWagon();

        #endregion RustEdit Hooks

        #region Helpers

        void CheckBaseEntity(BaseEntity baseEntity, Construction construction, Construction.Target target, BasePlayer player)
        {
            if (!player.IsPlayer()) return;
            if (baseEntity == null) return;

            if (IsItItemForWagonSpawn(baseEntity))
            {
                if (!CanHaveMoreWagons(player))
                {
                    CancelAction(baseEntity, player);
                    return;
                }  
                TryCreateNewGarage(baseEntity, target.entity, player);
            }

            Wagon wagon;
            if (target.entity == null) return;
            if (!IsTargetEntityBelongWagon(target.entity, out wagon)) return;

            if (wagon.baseWagon != null) wagon.CheckNewEntity(baseEntity, target.entity, player);
            else CheckWagonOnCustomMonument(player, baseEntity, target.entity, wagon);
        }

        bool IsItItemForWagonSpawn(BaseEntity baseEntity)
        {
            return baseEntity.ShortPrefabName == "electric.flasherlight.deployed" && baseEntity.skinID == SkindIdFlasher;
        }

        static bool CanHaveMoreWagons(BasePlayer player)
        {
            if (!ownIns._config.AmountWagons) return true;

            int maxAmountWagons = 0;
            foreach (KeyValuePair<string, int> kvp in ownIns._config.PermissionsForAmountWagons)
            {
                if (ownIns.permission.UserHasPermission(player.UserIDString, kvp.Key) && kvp.Value > maxAmountWagons) maxAmountWagons = kvp.Value;
            }

            if ((maxAmountWagons == 0) || (ownIns.amountWagons.ContainsKey(player) && ownIns.amountWagons[player] >= maxAmountWagons))
            {
                NotifyManager.SendMessageToPlayer(player, "NotHavePermForMoreWagons", ownIns._config.NotifyConfig.prefix);
                return false;
            }
            
            return true;
        }

        void TryCreateNewGarage(BaseEntity baseEntity, BaseEntity targetEntity, BasePlayer player)
        {
            if (!IsExistCupboard(baseEntity, player)) return;
            if (!IsTargetValid(baseEntity, targetEntity, player)) return;
            baseEntity.Kill();
            TryCreateNewPosInGarage(targetEntity, player, player.GetBuildingPrivilege());
        }

        bool IsExistCupboard(BaseEntity baseEntity, BasePlayer player)
        {
            BuildingPrivlidge cupboard = player.GetBuildingPrivilege();
            if (cupboard == null)
            {
                NotifyManager.SendMessageToPlayer(player, "BaseHasntCupboard", ownIns._config.NotifyConfig.prefix);
                CancelAction(baseEntity, player);
                return false;
            }
            return true;
        }

        static bool IsTargetValid(BaseEntity baseEntity, BaseEntity targetEntity, BasePlayer player)
        {
            if (!(targetEntity is BuildingBlock) || targetEntity.PrefabName != "assets/prefabs/building core/foundation/foundation.prefab")
            {
                NotifyManager.SendMessageToPlayer(player, "NotValidTarget", ownIns._config.NotifyConfig.prefix);
                if (baseEntity.ShortPrefabName == "electric.flasherlight.deployed") CancelAction(baseEntity, player);
                return false;
            }

            return true;
        }

        void TryCreateNewPosInGarage(BaseEntity targetEntity, BasePlayer player, BuildingPrivlidge cupboard, Wagon wagon = null)
        {
            List<BuildingBlock> buildingBlocks = Facepunch.Pool.GetList<BuildingBlock>();
            ConstructionMatrix matrix = new ConstructionMatrix();
            Garage garage;

            CreateListWithFoundations(buildingBlocks, targetEntity);
            if (!IsRightAmountFoundations(buildingBlocks, player))
            {
                Facepunch.Pool.FreeList(ref buildingBlocks);
                return;
            }
            if (!IsValidConstruction(matrix, targetEntity, buildingBlocks, player, wagon))
            {
                Facepunch.Pool.FreeList(ref buildingBlocks);
                return;
            }

            if (!ownIns.dicGarages.TryGetValue(cupboard, out garage))
            {
                garage = new Garage();
                dicGarages.Add(cupboard, garage);
            }

            if (wagon == null) wagon = GetNewWagon(player, matrix, garage);
            else wagon.MoveWagonInBase(matrix.posForSpawnWagon, garage);

            garage.AddNewPos(matrix.posForSpawnWagon, matrix.blocksUnderWagon);
            garage.wagons.Add(matrix.posForSpawnWagon.Key, wagon);

            Facepunch.Pool.FreeList(ref buildingBlocks);
            return;
        }

        void CreateListWithFoundations(List<BuildingBlock> buildingBlocks, BaseEntity targetEntity)
        {
            Vis.Entities(targetEntity.transform.position, 11f, buildingBlocks);

            foreach (BuildingBlock buildingBlock in buildingBlocks.ToHashSet())
            {
                if (buildingBlock.PrefabName != "assets/prefabs/building core/foundation/foundation.prefab") buildingBlocks.Remove(buildingBlock);
            }
        }

        bool IsRightAmountFoundations(List<BuildingBlock> buildingBlocks, BasePlayer player)
        {
            if (buildingBlocks.Count < 21)
            {
                NotifyManager.SendMessageToPlayer(player, "NoNeedAmountBlocks", ownIns._config.NotifyConfig.prefix);
                GiveWagon(player, 1);
                return false;
            }
            return true;
        }

        bool IsValidConstruction(ConstructionMatrix matrix, BaseEntity baseEntity, List<BuildingBlock> buildingBlocks, BasePlayer player, Wagon wagon)
        {
            matrix.Init(baseEntity, buildingBlocks, player);
            if (!matrix.isValid)
            {
                NotifyManager.SendMessageToPlayer(player, "NotValidConstruction", ownIns._config.NotifyConfig.prefix);
                if (wagon == null) GiveWagon(player, 1);
                return false;
            }
            return true;
        }

        static Wagon GetNewWagon(BasePlayer player, ConstructionMatrix matrix, Garage garage)
        {
            if (!ownIns.amountWagons.ContainsKey(player)) ownIns.amountWagons.Add(player, 0);
            ownIns.amountWagons[player]++;
            Wagon wagon = new Wagon();
            wagon.Init(player, matrix.posForSpawnWagon, garage);
            return wagon;
        }

        bool IsTargetEntityBelongWagon(BaseEntity targetEntity, out Wagon wagon)
        {
            return allBaseEntityByWagons.TryGetValue(targetEntity.net.ID.Value, out wagon);
        }

        void CheckWagonOnCustomMonument(BasePlayer player, BaseEntity baseEntity, BaseEntity targetEntity, Wagon wagon)
        {
            CustomMonument monument1;
            if (playersOnMonuments.TryGetValue(player, out monument1) && !monument1.IsWagonOnMonument(wagon.trainCar)) return;

            if (monument1 == null)
            {
                NotifyManager.SendMessageToPlayer(player, "NotOnBaseAndNotOnMonument", ownIns._config.NotifyConfig.prefix);
                CancelAction(baseEntity, player);
            }
            else if (monument1.isBuildingState) wagon.CheckNewEntity(baseEntity, targetEntity, player);
            else
            {
                NotifyManager.SendMessageToPlayer(player, "ModeIsNotBuilding", ownIns._config.NotifyConfig.prefix);
                CancelAction(baseEntity, player);
            }
        }

        void DetermineAllMonumentsForWagon()
        {
            if (customMonuments.Count > 0) return;
            foreach (BaseNetworkable entity in BaseNetworkable.serverEntities.entityList.Get().Values)
            {
                ElectricalBranch branch = entity as ElectricalBranch;
                if (branch == null || branch.branchAmount != 888444) continue;
                CustomMonument monument = new GameObject().AddComponent<CustomMonument>();
                monument.Init(branch);
                customMonuments.Add(monument);
            }
        }

        void DestroyAllCustomMonuments()
        {
            foreach (CustomMonument monument in customMonuments)
            {
                monument.Destroy();
                UnityEngine.Object.Destroy(monument.gameObject);
            }
        }

        void DetermineSetingsEntities()
        {
            foreach (AcceptableEntities entity in ownIns._config.AcceptableEntities)
            {
                acceptableEntities.Add(entity.ShortName, new SettingsEntity { IsAllowPut = entity.IsAllowPut, Amount = entity.Amount });
            }
        }

        static void CopySerializableFields<T>(T src, T dst)
        {
            FieldInfo[] srcFields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in srcFields)
            {
                object value = field.GetValue(src);
                field.SetValue(dst, value);
            }
        }

        static Quaternion GetGlobalRot(Transform Transform, Vector3 localRotation)
        {
            return Transform.rotation * Quaternion.Euler(localRotation);
        }

        static Vector3 GetGlobalPos(Transform Transform, Vector3 localPosition)
        {
            return Transform.TransformPoint(localPosition);
        }

        static void GiveWagon(BasePlayer player, int amount)
        {
            Item item = ItemManager.CreateByName("electric.flasherlight", amount, SkindIdFlasher);
            item.name = "Wagon";
            MoveItem(player, item);
        }

        static void CancelAction(BaseEntity entity, BasePlayer player)
        {
            if (entity is BuildingBlock) ReturnResourcesToPlayer(entity as BuildingBlock, player);
            else ReturnEntityToPlayer(entity, player);

            if (entity.IsExists()) entity.Kill();
        }

        static void ReturnResourcesToPlayer(BuildingBlock buildingBlock, BasePlayer player)
        {
            foreach (ItemAmount itemAmount in buildingBlock.BuildCost())
            {
                Item item = ItemManager.CreateByItemID(itemAmount.itemid, (int)itemAmount.amount, 0);
                MoveItem(player, item);
            }
        }

        static void ReturnEntityToPlayer(BaseEntity entity, BasePlayer player)
        {
            BaseCombatEntity baseCombatEntity = entity as BaseCombatEntity;
            string strName = GetItemName(baseCombatEntity);
            Item item = ItemManager.CreateByName(strName, 1, entity.skinID);
            MoveItem(player, item);
        }

        static string GetItemName(BaseCombatEntity baseCombatEntity)
        {
            if (baseCombatEntity.pickup.itemTarget == null)
            {
                string strName;
                if (ownIns.entityToItem.TryGetValue(baseCombatEntity.PrefabName, out strName)) return strName;
                else return GetShortNameItem(baseCombatEntity);
            }
            else return baseCombatEntity.pickup.itemTarget.shortname;
        }

        static string GetShortNameItem(BaseCombatEntity baseCombatEntity)
        {
            foreach (ItemDefinition itemDef in ItemManager.GetItemDefinitions())
            {
                if (itemDef == null) continue;
                ItemModDeployable itemMod = itemDef.GetComponent<ItemModDeployable>();
                if (itemMod == null || itemMod.entityPrefab == null) continue;
                string path = itemMod.entityPrefab.resourcePath;
                if (ownIns.entityToItem.ContainsKey(path)) continue;
                if (path == baseCombatEntity.PrefabName)
                {
                    ownIns.entityToItem.Add(path, itemDef.shortname);
                    return itemDef.shortname;
                }
            }

            return null;
        }

        static bool IsValidVariablesForBase(BasePlayer player, BaseEntity entity, out BuildingPrivlidge cupboard)
        {
            cupboard = null;

            if (!player.IsPlayer()) return false;
            if (entity == null) return false;

            cupboard = player.GetBuildingPrivilege();
            if (cupboard == null)
            {
                NotifyManager.SendMessageToPlayer(player, "BaseHasntCupboard", ownIns._config.NotifyConfig.prefix);
                return false;
            }
            if (cupboard != null && !cupboard.IsAuthed(player))
            {
                NotifyManager.SendMessageToPlayer(player, "NotAuthedInCupboard", ownIns._config.NotifyConfig.prefix);
                return false;
            }

            return true;
        }

        private static HashSet<T> GetEntities<T>(Vector3 position, float radius, int layerMask) where T : BaseEntity
        {
            HashSet<T> result = new HashSet<T>();
            List<T> list = Pool.GetList<T>();
            Vis.Entities<T>(position, radius, list, layerMask);
            foreach (T entity in list) result.Add(entity);
            Pool.FreeList(ref list);
            return result;
        }

        public static void Debug(object format) => ownIns.Puts(format.ToString());

        #endregion Helpers

        #region Variables

        [PluginReference] Plugin GUIAnnouncements, DiscordMessages, Notify, MadMappersPluginUpdate;

        HashSet<WagonData> dataForWagons { get; set; } = new HashSet<WagonData>();
        HashSet<GarageData> dataForGarages { get; set; } = new HashSet<GarageData>();

        Dictionary<string, SettingsEntity> acceptableEntities { get; } = new Dictionary<string, SettingsEntity>();

        HashSet<Wagon> wagons { get; } = new HashSet<Wagon>();
        HashSet<CodeLock> allCodeLocks { get; } = new HashSet<CodeLock>();
        HashSet<RFReceiver> allReceivers { get; } = new HashSet<RFReceiver>();
        HashSet<RANDSwitch> allSwitches { get; } = new HashSet<RANDSwitch>();

        HashSet<BasePlayer> callers { get; } = new HashSet<BasePlayer>();
        HashSet<Wagon> wagonsBeingCalled { get; } = new HashSet<Wagon>();
        HashSet<BasePlayer> playersWhoWantsToRemoveBaseWagon { get; } = new HashSet<BasePlayer>();

        Dictionary<ulong, Garage> allFoundationsUnderWagons { get; } = new Dictionary<ulong, Garage>();
        Dictionary<ulong, Wagon> allBaseBarricades { get; } = new Dictionary<ulong, Wagon>();
        Dictionary<ulong, Wagon> allBaseEntityByWagons { get; } = new Dictionary<ulong, Wagon>();
        Dictionary<ulong, Wagon> allTrainCars { get; } = new Dictionary<ulong, Wagon>();
        Dictionary<ulong, Wagon> allBaseWagons { get; } = new Dictionary<ulong, Wagon>();

        Dictionary<BuildingPrivlidge, Garage> dicGarages { get; } = new Dictionary<BuildingPrivlidge, Garage>();
        Dictionary<BasePlayer, int> amountWagons { get; } = new Dictionary<BasePlayer, int>();

        HashSet<CustomMonument> customMonuments { get; } = new HashSet<CustomMonument>();
        Dictionary<PressButton, CustomMonument> buttonsOnMonuments { get; } = new Dictionary<PressButton, CustomMonument>();
        Dictionary<BasePlayer, CustomMonument> playersOnMonuments { get; } = new Dictionary<BasePlayer, CustomMonument>();
        Dictionary<ulong, Wagon> doorCloserToWagon { get; } = new Dictionary<ulong, Wagon>();
        HashSet<ulong> allObjectsOfWalls { get; } = new HashSet<ulong>();

        static TrainHomes ownIns { get; set; }
        bool onServerInit { get; set; }

        Dictionary<string, string> entityToItem { get; } = new Dictionary<string, string>();

        static HashSet<ulong> skinIdFromCustomEntity = new HashSet<ulong>()
        {
            1594245394
        };

        static ulong SkindIdFlasher { get; } = 3171237719;
        static int delayTime { get; } = 20;

        #endregion Variables

        #region Classes

        static class LogManager
        {
            static string fileName;
            static string playerResult;
            static string damageResult;
            static string newHhp;
            static string owner;
            static Wagon wagon;

            internal static void AddLogDamage(BaseCombatEntity baseCombatEntity, HitInfo info)
            {
                ulong netId = baseCombatEntity.net.ID.Value;

                if (ownIns.allBaseWagons.TryGetValue(netId, out wagon)) owner = wagon.owner.displayName;
                else if (ownIns.allTrainCars.TryGetValue(netId, out wagon)) owner = wagon.owner.displayName;
                else return;

                fileName = baseCombatEntity.ShortPrefabName;

                string text = AddTimeToText();

                damageResult = info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Decay ? "decay" : info.damageTypes.Total().ToString();

                BasePlayer attacker = info.InitiatorPlayer;
                playerResult = attacker != null ? attacker.displayName : info.Initiator.PrefabName.ToString();

                newHhp = baseCombatEntity.health.ToString();

                text += $"Wagon {netId}; Damage: {damageResult}; Attacker: {playerResult}; NewHP: {newHhp}; Owner {owner}";

                AddLog(fileName, text);
            }

            static string AddTimeToText()
            {
                return $"[{DateTime.Now.ToShortTimeString()}] ";
            }

            static void AddLog(string fileName, string text) => ownIns.LogToFile(fileName, text, ownIns);
        }

        static class PluginUpdateManager
        {
            const int badRequest = 400;
            const int pluginNotFound = 404;
            const int internalErrorFromWebServer = 500;

            static bool debug = false;

            static bool IsValidAnswer(int code)
            {
                if (code == 200 || code == 204)
                    return true;
                if (debug)
                    DebugError(code);

                return false;
            }

            internal static void CheckPluginUpdate()
            {
                if (ownIns.MadMappersPluginUpdate != null) return;

                var UrlToApi = $"https://madmappers.store/api/plugins/getversion?pluginName={ownIns.Title}";
                ownIns.webrequest.Enqueue(UrlToApi, null, (code, response) =>
                {
                    if (!IsValidAnswer(code)) return;

                    string[] parts = response.Split('.');
                    var versionNumber = new VersionNumber(int.Parse(parts[0]), int.Parse(parts[1]),
                        int.Parse(parts[2]));
                    if (versionNumber > ownIns.Version)
                    {
                        ownIns.PrintWarning($"Your version of the plugin is outdated, upgrade to the version {response}");
                    }
                }, ownIns, RequestMethod.GET);
            }

            static void DebugError(int code)
            {
                switch (code)
                {
                    case badRequest:
                        ownIns.PrintWarning($"Bad Request");
                        break;
                    case pluginNotFound:
                        ownIns.PrintWarning($"Plugin Not Found");
                        break;
                    case internalErrorFromWebServer:
                        ownIns.PrintWarning($"Internal Error From Web Server");
                        break;
                }
            }
        }

        static class PermissionManager
        {
            internal const string giveWagon = "trainhomes.givewagon";
            internal const string freeWagons = "trainhomes.showfreewagons";

            static PluginConfig config => ownIns._config;
            static Permission perm => ownIns.permission;

            internal static void RegisterPermissions()
            {
                string message = "";

                if (config.PermissionsForAmountWagons.Count == 0) return;
                foreach (KeyValuePair<string, int> kvp in config.PermissionsForAmountWagons)
                {
                    perm.RegisterPermission(kvp.Key, ownIns);
                    message += $"'{kvp.Key}' ";
                }

                perm.RegisterPermission(giveWagon, ownIns);
                perm.RegisterPermission(freeWagons, ownIns);

                message += $"'{giveWagon}' '{freeWagons}'";
                ownIns.PrintWarning($"Parmissions {message} has been registered.");
            }

        }

        static class GUIManager
        {
            static string invisRGBA = "0 0 0 0";

            static string constAnchor = "0.5 0.5";
            static string strdParent = "Hud.Menu";
            static string strdNameCuiPanel = "InvisBackground";
            static string closeGui = "Close_GUI";

            static string hexWhite = "#ffffff";
            static string hexRed = "#d10000";

            static string hex1 = "#857d73";
            static string hex2 = "#ff8c00";

            internal enum GUITopic
            {
                Base,
                General,
                Construction,
                CustomMonument
            }

            internal static void CreateGUI(BasePlayer player, GUITopic topic)
            {
                DestroyGUI(player);
                CuiElementContainer container = new CuiElementContainer();

                AddStandardCuiPanel(container);
                AddNewCuiElement(container, "Background", "InvisBackground", "#1c2933", "-448 -250", "448 250");
                AddNewCuiButton(container, "Button1", "Background", closeGui, "[X]", hexWhite, hexRed, TextAnchor.MiddleCenter, 24, "400 202", "448 250");

                switch (topic)
                {
                    case GUITopic.Base:
                        CreateBaseGUI(container, player);
                        break;
                    case GUITopic.General:
                        CreateGeneralGUI(container, player);
                        break;
                    case GUITopic.Construction:
                        CreateConstructionGUI(container, player);
                        break;
                    case GUITopic.CustomMonument:
                    default:
                        CreateCustomMonumentGUI(container, player);
                        break;
                }

                CuiHelper.AddUi(player, container);
            }

            static void CreateBaseGUI(CuiElementContainer container, BasePlayer player)
            {
                AddNewCuiButton(container, "Button2", "Background", "GUI_General", GetMessage("GUI_General", player.UserIDString), hexWhite, hex1, TextAnchor.MiddleCenter, 24, "-448 202", "-236 250");

                AddNewCuiButton(container, "Button3", "Background", "GUI_Construction", GetMessage("GUI_Construction", player.UserIDString), hexWhite, hex1, TextAnchor.MiddleCenter, 24, "-236 202", "-24 250");

                AddNewCuiButton(container, "Button4", "Background", "GUI_CustomMonument", GetMessage("GUI_CustomMonument", player.UserIDString), hexWhite, hex1, TextAnchor.MiddleCenter, 24, "-24 202", "188 250");
            }

            static void CreateGeneralGUI(CuiElementContainer container, BasePlayer player)
            {
                AddNewCuiButton(container, "Button2", "Background", "GUI_General", GetMessage("GUI_General", player.UserIDString), hexWhite, hex2, TextAnchor.MiddleCenter, 24, "-448 202", "-236 250");

                AddNewCuiButton(container, "Button3", "Background", "GUI_Construction", GetMessage("GUI_Construction", player.UserIDString), hexWhite, hex1, TextAnchor.MiddleCenter, 24, "-236 202", "-24 250");

                AddNewCuiButton(container, "Button4", "Background", "GUI_CustomMonument", GetMessage("GUI_CustomMonument", player.UserIDString), hexWhite, hex1, TextAnchor.MiddleCenter, 24, "-24 202", "188 250");

                AddNewInvisCuiElement(container, "InvisBackgroundForText", "Background", "-424 -178", "424 178");

                AddNewCuiText(container, "Text", "InvisBackgroundForText", GetMessage("GUI_General_Description", player.UserIDString), hexWhite, TextAnchor.UpperLeft, 18);
            }

            static void CreateConstructionGUI(CuiElementContainer container, BasePlayer player)
            {
                AddNewCuiButton(container, "Button2", "Background", "GUI_General", GetMessage("GUI_General", player.UserIDString), hexWhite, hex1, TextAnchor.MiddleCenter, 24, "-448 202", "-236 250");

                AddNewCuiButton(container, "Button3", "Background", "GUI_Construction", GetMessage("GUI_Construction", player.UserIDString), hexWhite, hex2, TextAnchor.MiddleCenter, 24, "-236 202", "-24 250");

                AddNewCuiButton(container, "Button4", "Background", "GUI_CustomMonument", GetMessage("GUI_CustomMonument", player.UserIDString), hexWhite, hex1, TextAnchor.MiddleCenter, 24, "-24 202", "188 250");

                AddNewInvisCuiElement(container, "InvisBackgroundForText", "Background", "-424 -178", "424 178");

                AddNewCuiText(container, "Text", "InvisBackgroundForText", GetMessage("GUI_Construction_Description", player.UserIDString), hexWhite, TextAnchor.UpperLeft, 18);
            }

            static void CreateCustomMonumentGUI(CuiElementContainer container, BasePlayer player)
            {
                AddNewCuiButton(container, "Button2", "Background", "GUI_General", GetMessage("GUI_General", player.UserIDString), hexWhite, hex1, TextAnchor.MiddleCenter, 24, "-448 202", "-236 250");

                AddNewCuiButton(container, "Button3", "Background", "GUI_Construction", GetMessage("GUI_Construction", player.UserIDString), hexWhite, hex1, TextAnchor.MiddleCenter, 24, "-236 202", "-24 250");

                AddNewCuiButton(container, "Button4", "Background", "GUI_CustomMonument", GetMessage("GUI_CustomMonument", player.UserIDString), hexWhite, hex2, TextAnchor.MiddleCenter, 24, "-24 202", "188 250");

                AddNewInvisCuiElement(container, "InvisBackgroundForText", "Background", "-424 -178", "424 178");

                AddNewCuiText(container, "Text", "InvisBackgroundForText", GetMessage("GUI_CustomMonument_Description", player.UserIDString), hexWhite, TextAnchor.UpperLeft, 18);
            }

            static void AddStandardCuiPanel(CuiElementContainer container, bool cursorEnabled = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = invisRGBA },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    CursorEnabled = cursorEnabled,
                }, strdParent, strdNameCuiPanel);
            }

            static void AddNewInvisCuiElement(CuiElementContainer container, string name, string parent, string offsetMin, string offsetMax)
            {
                container.Add(new CuiElement
                {
                    Name = name,
                    Parent = parent,
                    Components =
                    {
                        new CuiImageComponent { Color = invisRGBA },
                        new CuiRectTransformComponent { AnchorMin = constAnchor, AnchorMax = constAnchor, OffsetMin = offsetMin, OffsetMax = offsetMax }
                    }
                });
            }

            static void AddNewCuiElement(CuiElementContainer container, string name, string parent, string hexColor, string offsetMin, string offsetMax)
            {
                container.Add(new CuiElement
                {
                    Name = name,
                    Parent = parent,
                    Components =
                    {
                        new CuiImageComponent { Color = ConvertHexToRGBA(hexColor) },
                        new CuiRectTransformComponent { AnchorMin = constAnchor, AnchorMax = constAnchor, OffsetMin = offsetMin, OffsetMax = offsetMax }
                    }
                });
            }

            static void AddNewCuiText(CuiElementContainer container, string name, string parent, string text, string hexColorText, TextAnchor align, int fontSize)
            {
                container.Add(new CuiElement
                {
                    Name = name,
                    Parent = parent,
                    Components =
                    {
                        new CuiTextComponent() { Color = ConvertHexToRGBA(hexColorText), Text = text, Align = align, FontSize = fontSize },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                    }
                });
            }

            static void AddNewCuiButton(CuiElementContainer container, string name, string parent, string command, string text, string hexColorText, string hexColor, TextAnchor align, int fontSize, string offsetMin, string offsetMax)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = constAnchor, AnchorMax = constAnchor, OffsetMin = offsetMin, OffsetMax = offsetMax },
                    Button = { Command = command, Color = ConvertHexToRGBA(hexColor) },
                    Text = { Text = text, Color = ConvertHexToRGBA(hexColorText), Align = align, FontSize = fontSize }
                }, parent, name);
            }

            internal static void DestroyGUI(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "InvisBackground");
            }

            static string ConvertHexToRGBA(string hexColor)
            {
                Color color;
                if (ColorUtility.TryParseHtmlString(hexColor, out color))
                {
                    float red = color.r;
                    float green = color.g;
                    float blue = color.b;
                    float alpha = 1f;

                    return $"{red} {green} {blue} {alpha}";
                }
                return null;
            }
        }

        static class NotifyManager
        {
            internal static void PrintInfoMessage(BasePlayer player, string langKey, params object[] args)
            {
                if (player == null)
                    ownIns.PrintWarning(ClearColorAndSize(GetMessage(langKey, null, args)));
                else
                    ownIns.PrintToChat(player, GetMessage(langKey, player.UserIDString, args));
            }

            internal static void PrintError(BasePlayer player, string langKey, params object[] args)
            {
                if (player == null)
                    ownIns.PrintError(ClearColorAndSize(GetMessage(langKey, null, args)));
                else
                    ownIns.PrintToChat(player, GetMessage(langKey, player.UserIDString, args));
            }

            internal static void PrintLogMessage(string langKey, params object[] args)
            {
                for (int i = 0; i < args.Length; i++)
                    if (args[i] is int)
                        args[i] = GetTimeMessage(null, (int)args[i]);

                ownIns.Puts(ClearColorAndSize(GetMessage(langKey, null, args)));
            }

            internal static void PrintWarningMessage(string langKey, params object[] args)
            {
                ownIns.PrintWarning(ClearColorAndSize(GetMessage(langKey, null, args)));
            }

            internal static string ClearColorAndSize(string message)
            {
                message = message.Replace("</color>", string.Empty);
                message = message.Replace("</size>", string.Empty);
                while (message.Contains("<color="))
                {
                    int index = message.IndexOf("<color=");
                    message = message.Remove(index, message.IndexOf(">", index) - index + 1);
                }
                while (message.Contains("<size="))
                {
                    int index = message.IndexOf("<size=");
                    message = message.Remove(index, message.IndexOf(">", index) - index + 1);
                }
                return message;
            }

            internal static void SendMessageToAll(string langKey, params object[] args)
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    if (player != null)
                        SendMessageToPlayer(player, langKey, args);

                TrySendDiscordMessage(langKey, args);
            }

            internal static void SendMessageToPlayer(BasePlayer player, string langKey, params object[] args)
            {
                for (int i = 0; i < args.Length; i++)
                    if (args[i] is int)
                        args[i] = GetTimeMessage(player.UserIDString, (int)args[i]);

                string playerMessage = GetMessage(langKey, player.UserIDString, args);

                if (ownIns._config.NotifyConfig.chatConfig.isEnabled)
                    ownIns.PrintToChat(player, playerMessage);

                if (ownIns._config.NotifyConfig.gameTipConfig.isEnabled)
                    player.SendConsoleCommand("gametip.showtoast", ownIns._config.NotifyConfig.gameTipConfig.style, ClearColorAndSize(playerMessage));

                if (ownIns._config.NotifyConfig.guiAnnouncementsConfig.isEnabled && ownIns.plugins.Exists("GUIAnnouncements"))
                    ownIns.GUIAnnouncements?.Call("CreateAnnouncement", ClearColorAndSize(playerMessage), ownIns._config.NotifyConfig.guiAnnouncementsConfig.bannerColor, ownIns._config.NotifyConfig.guiAnnouncementsConfig.textColor, player, ownIns._config.NotifyConfig.guiAnnouncementsConfig.apiAdjustVPosition);

                if (ownIns._config.NotifyConfig.notifyPluginConfig.isEnabled && ownIns.plugins.Exists("Notify"))
                    ownIns.Notify?.Call("SendNotify", player, ownIns._config.NotifyConfig.notifyPluginConfig.type, ClearColorAndSize(playerMessage));
            }

            internal static string GetTimeMessage(string userIDString, int seconds)
            {
                string message = "";

                TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);
                if (timeSpan.Hours > 0) message += $" {timeSpan.Hours} {GetMessage("Hours", userIDString)}";
                if (timeSpan.Minutes > 0) message += $" {timeSpan.Minutes} {GetMessage("Minutes", userIDString)}";
                if (message == "") message += $" {timeSpan.Seconds} {GetMessage("Seconds", userIDString)}";

                return message;
            }

            static void TrySendDiscordMessage(string langKey, params object[] args)
            {
                if (CanSendDiscordMessage(langKey))
                {
                    for (int i = 0; i < args.Length; i++)
                        if (args[i] is int)
                            args[i] = GetTimeMessage(null, (int)args[i]);

                    object fields = new[] { new { name = ownIns.Title, value = ClearColorAndSize(GetMessage(langKey, null, args)), inline = false } };
                    ownIns.DiscordMessages?.Call("API_SendFancyMessage", ownIns._config.NotifyConfig.discordMessagesConfig.webhookUrl, "", ownIns._config.NotifyConfig.discordMessagesConfig.embedColor, JsonConvert.SerializeObject(fields), null, ownIns);
                }
            }

            static bool CanSendDiscordMessage(string langKey)
            {
                return ownIns._config.NotifyConfig.discordMessagesConfig.keys.Contains(langKey) && ownIns._config.NotifyConfig.discordMessagesConfig.isEnabled && !string.IsNullOrEmpty(ownIns._config.NotifyConfig.discordMessagesConfig.webhookUrl) && ownIns._config.NotifyConfig.discordMessagesConfig.webhookUrl != "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
            }
        }

        public class WagonData
        {
            public ulong idPlayer;
            public ulong idObjectForUnload;
            public ulong idTrainCar;

            public string code;
            public int frequency;

            public string pos;
            public string rot;

            public HashSet<ulong> authPlayers;
        }

        public class GarageData
        {
            public ulong idCupboard;
            public HashSet<PosInGarageData> poss { get; set; } = new HashSet<PosInGarageData>();
        }

        public class PosInGarageData
        {
            public string pos;
            public string rot;
            public ulong idObjectForUnload;
            public HashSet<ulong> foundations { get; set; } = new HashSet<ulong>();
        }

        internal class SettingsEntity
        {
            public bool IsAllowPut { get; set; }
            public int Amount { get; set; }
        }

        internal class BaseBarricade : BaseCombatEntity
        {

        }

        internal class Wagon
        {
            internal BasePlayer owner { get; set; }

            BaseEntity wagonToMove { get; set; }
            internal DoorCloser objectForUnload { get; set; }
            internal BaseCombatEntity baseWagon { get; set; }
            internal TrainCar trainCar { get; set; }

            internal Garage garage { get; set; }
            internal bool freeWagon { get; set; }
            internal bool inGarage { get; set; }
            internal bool onCustomMonument { get; set; }
            ModificationsWagon modifications { get; set; }

            uint newBuildingID { get; } = BuildingManager.server.NewBuildingID();
            internal HashSet<BuildingBlock> allBuilidngBlocks { get; } = new HashSet<BuildingBlock>();
            Dictionary<BaseEntity, BaseEntity> allNotBuilidngBlocks { get; } = new Dictionary<BaseEntity, BaseEntity>();
            HashSet<BaseBarricade> allObjectOfStand { get; } = new HashSet<BaseBarricade>();

            KeyValuePair<Vector3, Quaternion> posInGarage { get; set; }

            static float localFloorHeight { get; } = 1.52f;
            static HashSet<string> localPosFloorOnWagon { get; } = new HashSet<string>()
            {
                "(0, 1.52, -6)",
                "(0, 1.52, -3)",
                "(0, 1.52, 0)",
                "(0, 1.52, 3)",
                "(0, 1.52, 6)"
            };
            static Dictionary<string, string> localPosObjects { get; } = new Dictionary<string, string>()
            {
                ["(-2.214, 1.006, 6.944)"] = "(0, 183, 180)",
                ["(0, 1.145, 8.298)"] = "(0, 356, 180)",
                ["(2.355, 1.006, 6.944)"] = "(0, 340, 180)",
                ["(2.286, 1.006, -6.648)"] = "(0, 15, 180)",
                ["(0, 1.145, -8.348)"] = "(0, 6, 180)",
                ["(-2.316, 1.006, -6.785)"] = "(0, 160, 180)",
            };

            internal void Init(BasePlayer player, KeyValuePair<Vector3, Quaternion> pos, Garage garage)
            {
                ownIns.wagons.Add(this);
                owner = player;
                posInGarage = pos;
                this.garage = garage;

                StartCreateOnBase();
            }

            internal void InitFreeWagon(Vector3 pos, Quaternion rot)
            {
                owner = null;
                posInGarage = new KeyValuePair<Vector3, Quaternion>();
                garage = null;

                StartCreateFreeWagon(pos, rot);
            }

            internal void InitWithData(WagonData data, ref bool isValidData)
            {
                posInGarage = new KeyValuePair<Vector3, Quaternion>(data.pos.ToVector3(), Quaternion.Euler(data.rot.ToVector3()));

                if (data.idTrainCar != 0) InitUsingTrainCar(data, ref isValidData);
                else InitUsingDoorCloser(data, ref isValidData);

                if (!isValidData) return;

                if (!IsExistOwnerOnServer(data.idPlayer.ToString(), ref isValidData)) return;

                SpawnModifications(data);
                if (!ownIns.amountWagons.ContainsKey(owner)) ownIns.amountWagons.Add(owner, 0);
                ownIns.amountWagons[owner]++;
                garage = null;
            }

            void InitUsingTrainCar(WagonData data, ref bool isValidData)
            {
                trainCar = BaseNetworkable.serverEntities.entityList.Get().FirstOrDefault(p => p.Value.net.ID.Value == data.idTrainCar).Value as TrainCar;
                if (trainCar == null)
                {
                    isValidData = false;
                    return;
                }
                InitChildrenWithData(trainCar);
                InitAllNotBuildingBlocks();
                ChangeParametersTrainCar();
                if (!ownIns.allTrainCars.ContainsKey(trainCar.net.ID.Value)) ownIns.allTrainCars.Add(trainCar.net.ID.Value, this);
            }

            void InitUsingDoorCloser(WagonData data, ref bool isValidData)
            {
                Debug("Search for door closer...");
                objectForUnload = BaseNetworkable.serverEntities.entityList.Get().FirstOrDefault(p => p.Value.IsExists() && p.Value.net != null && p.Value.net.ID.Value == data.idObjectForUnload).Value as DoorCloser;
                if (objectForUnload == null)
                {
                    Debug("door closer == null");
                    isValidData = false;
                    return;
                }
                Debug("door closer != null");
                InitChildrenWithData(objectForUnload);
                InitAllNotBuildingBlocks();

                SpawnBaseWagon();
                SpawnStand();
                SetParentForBuildingBlocks(baseWagon);
                RemoveParentForNotBuildingBlock();
                ownIns.doorCloserToWagon.Add(objectForUnload.net.ID.Value, this);
                objectForUnload.Kill();

                inGarage = true;
                if (!ownIns.allBaseWagons.ContainsKey(baseWagon.net.ID.Value)) ownIns.allBaseWagons.Add(baseWagon.net.ID.Value, this);
            }

            void InitChildrenWithData(BaseEntity parent)
            {
                if (parent.children.Count == 0) return;
                BaseEntity child;
                for (int i = 0; i < parent.children.Count; i++)
                {
                    child = parent.children[i];
                    TryMakeEntityNotDecay(child);

                    if (child is BuildingBlock)
                    {
                        allBuilidngBlocks.Add((BuildingBlock)child);
                        if (!ownIns.allBaseEntityByWagons.ContainsKey(child.net.ID.Value)) ownIns.allBaseEntityByWagons.Add(child.net.ID.Value, this);
                        continue;
                    }
                }
            }

            void InitAllNotBuildingBlocks()
            {
                BaseEntity child1;
                BaseEntity child2;
                foreach (BuildingBlock block in allBuilidngBlocks)
                {
                    if (block.children.Count == 0) continue;
                    for (int i = 0; i < block.children.Count; i++)
                    {
                        child1 = block.children[i];
                        TryMakeEntityNotDecay(child1);
                        if (!allNotBuilidngBlocks.ContainsKey(child1)) allNotBuilidngBlocks.Add(child1, block);
                        if (!ownIns.allBaseEntityByWagons.ContainsKey(child1.net.ID.Value)) ownIns.allBaseEntityByWagons.Add(child1.net.ID.Value, this);

                        if (child1.children.Count == 0) continue;
                        for (int x = 0; x < child1.children.Count; x++)
                        {
                            child2 = child1.children[x];
                            TryMakeEntityNotDecay(child2);
                            if (!allNotBuilidngBlocks.ContainsKey(child2)) allNotBuilidngBlocks.Add(child2, child1);
                            if (!ownIns.allBaseEntityByWagons.ContainsKey(child2.net.ID.Value)) ownIns.allBaseEntityByWagons.Add(child2.net.ID.Value, this);
                        }
                    }
                }
            }

            bool IsExistOwnerOnServer(string str, ref bool isValidData)
            {
                owner = BasePlayer.FindAwakeOrSleeping(str);
                if (owner == null)
                {
                    isValidData = false;
                    return false;
                }
                return true;
            }

            internal void StartCreateOnBase()
            {
                SpawnBaseWagon();
                SpawnStand();
                SpawnModifications();
                SpawnHouse();

                inGarage = true;
            }

            internal async void StartCreateFreeWagon(Vector3 pos, Quaternion rot)
            {
                SpawnTrainCar(pos, rot);
                await Delay(delayTime);

                if (trainCar == null) return;
                ownIns.allTrainCars.Add(trainCar.net.ID.Value, this);

                SpawnModifications();
                SpawnHouse();

                inGarage = false;
                freeWagon = true;

                ownIns.wagons.Add(this);
            }

            async void SpawnBaseWagon()
            {
                baseWagon = CreateBaseWagon.Start(posInGarage.Key, posInGarage.Value);
                baseWagon.Spawn();

                await Delay(delayTime);

                if (!ownIns.allBaseWagons.ContainsKey(baseWagon.net.ID.Value))
                {
                    ownIns.allBaseWagons.Add(baseWagon.net.ID.Value, this);
                }
            }

            void SpawnStand()
            {
                foreach (KeyValuePair<string, string> kvp in localPosObjects)
                {
                    Vector3 pos = GetGlobalPos(baseWagon.transform, kvp.Key.ToVector3());
                    Quaternion rot = GetGlobalRot(baseWagon.transform, kvp.Value.ToVector3());

                    BaseBarricade baseBarricade = CreateStandObject(pos, rot);

                    baseBarricade.SetParent(baseWagon, true, true);
                    baseBarricade.Spawn();

                    allObjectOfStand.Add(baseBarricade);
                    ownIns.allBaseBarricades.Add(baseBarricade.net.ID.Value, this);
                }
            }

            BaseBarricade CreateStandObject(Vector3 pos, Quaternion rot)
            {
                BaseBarricade baseBarricade;

                Barricade barricade = GameManager.server.CreateEntity("assets/prefabs/deployable/barricades/barricade.stone.prefab", pos, rot) as Barricade;

                baseBarricade = barricade.gameObject.AddComponent<BaseBarricade>();

                CopySerializableFields((BaseCombatEntity)barricade, baseBarricade);

                UnityEngine.Object.DestroyImmediate(barricade, true);

                baseBarricade.enableSaving = true;

                baseBarricade._maxHealth = ownIns._config.HpBarricade;
                baseBarricade.startHealth = ownIns._config.HpBarricade;
                baseBarricade.health = ownIns._config.HpBarricade;

                return baseBarricade;
            }

            void SpawnModifications(WagonData data = null)
            {
                CreateModifications();
                modifications.Spawn();

                modifications.SetParent(GetActiveWagon, true, true);
                if (owner != null) modifications.Init(owner, this, data);
                else modifications.InitForFreeWagon(this);
            }

            void CreateModifications()
            {
                BaseCombatEntity entity = GetActiveWagon;
                Vector3 pos; Quaternion rot;
                pos = entity.transform.position + new Vector3(0f, 1.332f, 0f);
                rot = entity.transform.rotation;

                DoorCloser closer = GameManager.server.CreateEntity("assets/prefabs/misc/doorcloser/doorcloser.prefab", pos, rot) as DoorCloser;

                modifications = closer.gameObject.AddComponent<ModificationsWagon>();

                CopySerializableFields(closer, modifications);

                UnityEngine.Object.DestroyImmediate(closer, true);
            }

            private BaseCombatEntity GetActiveWagon => baseWagon != null ? baseWagon : trainCar != null ? trainCar : null;

            void SpawnHouse()
            {
                SpawnFloorsOnWagon();
            }

            internal void MoveWagonInBase(KeyValuePair<Vector3, Quaternion> newPosInGarage, Garage newGarage)
            {
                posInGarage = newPosInGarage;
                garage = newGarage;
                wagonToMove = trainCar;
                SpawnBaseWagon();
                SpawnStand();
                ChangeParent(baseWagon);
                modifications.LimitNetworkingForBlocks();
                inGarage = true;
            }

            internal async void MoveWagonToTrack(Vector3 pos, BasePlayer caller)
            {
                SpawnTrainCar(pos, new Quaternion());
                await Delay(100);

                if (trainCar == null)
                {
                    NotifyManager.SendMessageToPlayer(caller, "NotValidPosForTrainCar", ownIns._config.NotifyConfig.prefix);
                    return;
                }

                wagonToMove = baseWagon;
                ownIns.allTrainCars.Add(trainCar.net.ID.Value, this);
                ChangeParent(trainCar);

                if (garage != null) garage.TryRemoveWagon(this);
                inGarage = false;

                await Delay(1000);

                modifications.LimitNetworkingForBlocks();
            }

            void SpawnTrainCar(Vector3 pos, Quaternion rot)
            {
                trainCar = GameManager.server.CreateEntity("assets/content/vehicles/trains/wagons/trainwagonc.entity.prefab", pos, rot) as TrainCar;
                trainCar.Spawn();
                ChangeParametersTrainCar();
            }

            void ChangeParametersTrainCar()
            {
                trainCar._maxHealth = ownIns._config.HpWagon;
                trainCar.startHealth = ownIns._config.HpWagon;
                trainCar.health = ownIns._config.HpWagon;
                trainCar.CancelInvoke(trainCar.DecayTick);
            }

            internal async void ChangeParent(BaseEntity parent)
            {
                if (wagonToMove == trainCar) GameObject.DestroyImmediate(trainCar.platformParentTrigger);
                else UpdateForNotBuildingBlocks();

                wagonToMove.transform.position = parent.transform.position;
                wagonToMove.transform.rotation = parent.transform.rotation;

                SetParentForBuildingBlocks(parent);

                modifications.SetParent(parent);

                await Delay(delayTime);

                if (wagonToMove == trainCar)
                {
                    RemoveParentForNotBuildingBlock();
                    ownIns.allTrainCars.Remove(trainCar.net.ID.Value);
                    trainCar.Kill();
                }
                else RemoveEverythingLinkedToBaseWagon();
            }

            void UpdateForNotBuildingBlocks()
            {
                foreach (KeyValuePair<BaseEntity, BaseEntity> kvp in allNotBuilidngBlocks)
                {
                    IOEntity entity = kvp.Key as IOEntity;
                    if (entity != null) UpdateIOEntity(entity as IOEntity);

                    SetParentForNotBuildingBlock(kvp.Key, kvp.Value);
                }
            }

            void UpdateIOEntity(IOEntity entity)
            {
                if (entity.outputs.Length > 0) UpdateOutputsOrInputs(entity.outputs);
                if (entity.inputs.Length > 0) UpdateOutputsOrInputs(entity.inputs);
            }

            void UpdateOutputsOrInputs(IOEntity.IOSlot[] slots)
            {
                foreach (IOEntity.IOSlot iORef in slots)
                {
                    IOEntity iOEntity = iORef.connectedTo.ioEnt;
                    if (iOEntity == null) continue;
                    if (!allNotBuilidngBlocks.ContainsKey(iOEntity))
                    {
                        iOEntity.ClearConnections();
                    }
                }
            }

            internal void SetParentForAllNotBuildingBlocks()
            {
                foreach (KeyValuePair<BaseEntity, BaseEntity> kvp in allNotBuilidngBlocks)
                {
                    BaseEntity baseEntity = kvp.Key;
                    SetParentForNotBuildingBlock(baseEntity, kvp.Value);
                    baseEntity.SendNetworkUpdate();
                }
            }

            void SetParentForNotBuildingBlock(BaseEntity entity, BaseEntity targetEntity)
            {
                if (entity.GetParentEntity() != null) return;
                
                Vector3 localPos = targetEntity.transform.InverseTransformPoint(entity.transform.position);
                Vector3 rot = entity.transform.rotation.eulerAngles;

                entity.SetParent(targetEntity);

                entity.transform.localPosition = localPos;
                entity.transform.rotation = Quaternion.Euler(rot);
            }

            void SetParentForBuildingBlocks(BaseEntity parent)
            {
                foreach (BuildingBlock buildingBlock in allBuilidngBlocks) buildingBlock.SetParent(parent);
            }

            void RemoveEverythingLinkedToBaseWagon()
            {
                ownIns.allBaseWagons.Remove(baseWagon.net.ID.Value);
                RemoveStand();
                allObjectOfStand.Clear();
                baseWagon.Kill();
            }

            void RemoveStand()
            {
                foreach (BaseBarricade baseBarricade in allObjectOfStand)
                {
                    ownIns.allBaseBarricades.Remove(baseBarricade.net.ID.Value);
                    baseBarricade.Kill();
                }
            }

            async void RemoveParentForNotBuildingBlock()
            {
                await Delay(delayTime);

                foreach (KeyValuePair<BaseEntity, BaseEntity> kvp in allNotBuilidngBlocks)
                {
                    BaseEntity baseEntity = kvp.Key;
                    if (IsIgnoredEntity(baseEntity)) continue;
                    baseEntity.SetParent(null, true, true);
                    baseEntity.SendNetworkUpdate();
                };
            }

            bool IsIgnoredEntity(BaseEntity entity)
            {
                return entity is DoorCloser || entity is KeyLock || entity is CodeLock ||
                        entity is DoorKnocker || entity is GrowableEntity || entity is IndustrialStorageAdaptor ||
                        entity is StorageMonitor || entity is IndustrialCrafter || entity is TorchDeployableLightSource ||
                        entity is TorchWeapon || entity is Door;
            }

            internal void ThrowAwayFlare(BasePlayer player) 
            {
                RoadFlare flare = GameManager.server.CreateEntity("assets/prefabs/tools/flareold/flare.deployed.prefab", player.transform.position + new Vector3(0f, 1f, 0f)) as RoadFlare;
                flare.Spawn();
                Rigidbody rigidbody = flare.GetComponent<Rigidbody>();

                Vector3 throwDirection = player.eyes.HeadForward();
                throwDirection.y = throwDirection.y + 0.5f;
                rigidbody.AddForce(throwDirection.normalized * 1.3f, ForceMode.Impulse);

                SpawnerWagon spawner = flare.gameObject.AddComponent<SpawnerWagon>();
                spawner.Init(this, player);
            }

            internal void CreateObjectForUnload()
            {
                objectForUnload = GameManager.server.CreateEntity("assets/prefabs/misc/doorcloser/doorcloser.prefab", baseWagon.transform.position, baseWagon.transform.rotation) as DoorCloser;
                objectForUnload.enableSaving = true;
                objectForUnload.Spawn();
            }

            internal WagonData CreateInfoForData()
            {
                WagonData wagonData = new WagonData();
                wagonData.idPlayer = owner.userID;
                wagonData.code = modifications.code;
                wagonData.frequency = modifications.frequency;

                wagonData.authPlayers = new HashSet<ulong>();
                foreach (ulong playerID in modifications.GetWhitelist())
                {
                    wagonData.authPlayers.Add(playerID);
                }

                if (inGarage)
                {
                    wagonData.idTrainCar = 0;
                    wagonData.idObjectForUnload = objectForUnload.net.ID.Value;
                }
                else
                {
                    wagonData.idTrainCar = trainCar.net.ID.Value;
                    wagonData.idObjectForUnload = 0;
                }

                wagonData.pos = posInGarage.Key.ToString();
                wagonData.rot = posInGarage.Value.eulerAngles.ToString();

                return wagonData;
            }

            internal void UpdateForUnload()
            {
                SetParentForAllNotBuildingBlocks();

                onCustomMonument = false;
                modifications.RemoveModifications();
                modifications.Kill();

                if (GetActiveWagon == baseWagon)
                {
                    SetParentForBuildingBlocks(objectForUnload);
                    baseWagon.Kill();
                }
            }

            async Task Delay(int millisecondsDelay)
            {
                await Task.Delay(millisecondsDelay);
            }

            internal void RemoveWagon()
            {
                if (owner != null && ownIns.amountWagons.ContainsKey(owner) && ownIns.amountWagons[owner] > 0) ownIns.amountWagons[owner]--;
                if (objectForUnload != null) objectForUnload.Kill();
                if (garage != null) garage.TryRemoveWagon(this);
                if (modifications != null) modifications.RemoveModifications();
                if (modifications != null) modifications.Kill();

                foreach (BuildingBlock buildingBlock in allBuilidngBlocks.ToHashSet()) buildingBlock.Kill();

                if (baseWagon != null)
                {
                    RemoveStand();
                    if (ownIns.allBaseWagons.ContainsKey(baseWagon.net.ID.Value)) ownIns.allBaseWagons.Remove(baseWagon.net.ID.Value);
                    baseWagon.Kill();
                }

                if (trainCar != null)
                {
                    if (ownIns.allTrainCars.ContainsKey(trainCar.net.ID.Value)) ownIns.allTrainCars.Remove(trainCar.net.ID.Value);
                    trainCar.Kill();
                }

                allNotBuilidngBlocks.Clear();
                allBuilidngBlocks.Clear();
                if (ownIns.wagons.Contains(this)) ownIns.wagons.Remove(this);
            }

            internal void UpdateStateOnMonument(bool isBuildingState)
            {
                onCustomMonument = isBuildingState;

                if (isBuildingState) RemoveParentForNotBuildingBlock();
                else UpdateForNotBuildingBlocks();
                SetParentForBuildingBlocks(GetActiveWagon);
            }

            internal HashSet<BaseEntity> GetAllDeployedObjects()
            {
                return allNotBuilidngBlocks.Keys.ToHashSet();
            }

            internal void ChangeStateToLocked(BasePlayer player)
            {
                ControllerSpawnsFreeWagon.RemoveFreeWagon(trainCar);
                freeWagon = false;
                owner = player;
            }

            #region House

            void SpawnFloorsOnWagon()
            {
                BaseEntity activeWagon = GetActiveWagon;
                foreach (string localPos in localPosFloorOnWagon)
                {
                    Vector3 pos = GetGlobalPos(activeWagon.transform, localPos.ToVector3());
                    Quaternion rot = GetGlobalRot(activeWagon.transform, new Vector3(0f, 0f, 0f));

                    BaseEntity entity = GameManager.server.CreateEntity("assets/prefabs/building core/floor/floor.prefab", pos, rot);

                    entity.enableSaving = true;
                    entity.transform.localPosition = localPos.ToVector3();
                    entity.transform.localRotation = new Quaternion(0f, 0f, 0f, 0f);
                    entity.SetParent(activeWagon);

                    entity.Spawn();
                    BuildingBlock floor = entity as BuildingBlock;
                    floor.lifestate = BaseCombatEntity.LifeState.Dead;
                    floor.AttachToBuilding(newBuildingID);
                    floor.SetHealthToMax();
                    floor.ChangeGradeAndSkin(BuildingGrade.Enum.Twigs, 0, true, true);

                    allBuilidngBlocks.Add(floor);
                    ownIns.allBaseEntityByWagons.Add(floor.net.ID.Value, this);
                }
            }

            internal void CheckNewEntity(BaseEntity baseEntity, BaseEntity target, BasePlayer player)
            {
                if (IsCupboard(baseEntity, player)) return;

                if (baseEntity is Door) SetParentForNotBuildingBlock(baseEntity, target);

                TryDestroyGrounded(baseEntity);
                TryMakeEntityNotDecay(baseEntity);

                if (baseEntity is BuildingBlock)
                {
                    CheckNewBuildingBlock(baseEntity, player);
                    return;
                }

                if (IsEntityOnRoof(baseEntity.transform.position) && !IsAllowPutOnRoof(baseEntity, player)) return;
                if (ownIns.acceptableEntities.ContainsKey(baseEntity.ShortPrefabName) && !IsValidSettingsEntity(baseEntity, player)) return;
                if (skinIdFromCustomEntity.Contains(baseEntity.skinID)) ProcessCustomEntity(baseEntity, target);

                allNotBuilidngBlocks.Add(baseEntity, target);
                ownIns.allBaseEntityByWagons.Add(baseEntity.net.ID.Value, this);
            }

            bool IsCupboard(BaseEntity baseEntity, BasePlayer player)
            {
                if (baseEntity is BuildingPrivlidge)
                {
                    NotifyManager.SendMessageToPlayer(player, "CanntPutEntity", ownIns._config.NotifyConfig.prefix);
                    CancelAction(baseEntity, player);
                    return true;
                }

                return false;
            }

            void TryDestroyGrounded(BaseEntity baseEntity)
            {
                GroundWatch groundWatch = baseEntity.GetComponent<GroundWatch>();
                if (groundWatch != null) UnityEngine.Object.DestroyImmediate(groundWatch);

                DestroyOnGroundMissing destroyOnGroundMissing = baseEntity.GetComponent<DestroyOnGroundMissing>();
                if (destroyOnGroundMissing != null) UnityEngine.Object.DestroyImmediate(destroyOnGroundMissing);
                if (baseEntity is StabilityEntity) (baseEntity as StabilityEntity).grounded = true;
            }

            void TryMakeEntityNotDecay(BaseEntity entity)
            {
                DecayEntity decay = entity as DecayEntity;
                if (decay == null) return;

                decay.AttachToBuilding(newBuildingID);
                decay.lastDecayTick = float.MaxValue;
            }

            void CheckNewBuildingBlock(BaseEntity baseEntity, BasePlayer player)
            {
                if (IsAllowPlaceBuildingBlock(baseEntity, player))
                {
                    allBuilidngBlocks.Add(baseEntity as BuildingBlock);
                    ownIns.allBaseEntityByWagons.Add(baseEntity.net.ID.Value, this);

                    SetParentForNewEntity(baseEntity);
                }
                else CancelAction(baseEntity, player);
            }

            bool IsAllowPutOnRoof(BaseEntity baseEntity, BasePlayer player)
            {
                if (!ownIns._config.IsAllowPutOnRoof)
                {
                    NotifyManager.SendMessageToPlayer(player, "CanntPutEntityOnRoof", ownIns._config.NotifyConfig.prefix);
                    CancelAction(baseEntity, player);
                    return false;
                }

                return true;
            }

            bool IsValidSettingsEntity(BaseEntity baseEntity, BasePlayer player)
            {
                SettingsEntity settingsEntity = ownIns.acceptableEntities[baseEntity.ShortPrefabName];
                if (!settingsEntity.IsAllowPut)
                {
                    NotifyManager.SendMessageToPlayer(player, "CanntPutEntity", ownIns._config.NotifyConfig.prefix);
                    CancelAction(baseEntity, player);
                    return false;
                }

                int amount = allNotBuilidngBlocks.GetCountVariable(x => x.Key.ShortPrefabName == baseEntity.ShortPrefabName);

                if (settingsEntity.Amount <= amount)
                {
                    NotifyManager.SendMessageToPlayer(player, "SetMaxAmountObjects", ownIns._config.NotifyConfig.prefix);
                    CancelAction(baseEntity, player);
                    return false;
                }

                return true;
            }

            void ProcessCustomEntity(BaseEntity baseEntity, BaseEntity target)
            {
                baseEntity.SetParent(target);
            }

            void SetParentForNewEntity(BaseEntity baseEntity)
            {
                BaseEntity activeWagon = GetActiveWagon;
                Vector3 localPos = activeWagon.transform.InverseTransformPoint(baseEntity.transform.position);
                Vector3 rot = baseEntity.transform.rotation.eulerAngles;

                baseEntity.SetParent(activeWagon);
                baseEntity.transform.localPosition = localPos;

                baseEntity.transform.rotation = Quaternion.Euler(rot);
            }

            internal void RemoveEntityInLists(BaseEntity entity)
            {
                if (entity == null) return;
                if (entity is BuildingBlock)
                {
                    foreach (KeyValuePair<BaseEntity, BaseEntity> kvp in allNotBuilidngBlocks.ToDictionary())
                    {
                        if (kvp.Value == entity) kvp.Key.Kill();
                    }
                    allBuilidngBlocks.Remove(entity as BuildingBlock);
                }
                else allNotBuilidngBlocks.Remove(entity);
            }

            #endregion House

            #region IsAllowBaseEntity

            bool IsEntityOnRoof(Vector3 buildingPos)
            {
                if (buildingPos.y > GetActiveWagon.transform.position.y + localFloorHeight + 3.02f)
                {
                    return true;
                }
                return false;
            }

            #endregion IsAllowBaseEntity

            #region IsAllowBuildingBlockPos

            bool IsAllowPlaceBuildingBlock(BaseEntity baseEntity, BasePlayer player)
            {
                switch (baseEntity.PrefabName)
                {
                    case "assets/prefabs/building core/wall/wall.prefab":
                    case "assets/prefabs/building core/wall.doorway/wall.doorway.prefab":
                    case "assets/prefabs/building core/wall.window/wall.window.prefab":
                    case "assets/prefabs/building core/wall.frame/wall.frame.prefab":
                        return IsAllowWallPos(baseEntity.transform.position, player);

                    case "assets/prefabs/building core/foundation.steps/foundation.steps.prefab":
                    case "assets/prefabs/building core/ramp/ramp.prefab":
                    case "assets/prefabs/building core/wall.half/wall.half.prefab":
                    case "assets/prefabs/building core/wall.low/wall.low.prefab":
                    case "assets/prefabs/building core/stairs.u/block.stair.ushape.prefab":
                    case "assets/prefabs/building core/stairs.l/block.stair.lshape.prefab":
                    case "assets/prefabs/building core/stairs.spiral/block.stair.spiral.prefab":
                    case "assets/prefabs/building core/stairs.spiral.triangle/block.stair.spiral.triangle.prefab":
                        return IsAllowBuildingBlockPos(baseEntity.transform.position, player);

                    case "assets/prefabs/building core/floor/floor.prefab":
                    case "assets/prefabs/building core/floor.frame/floor.frame.prefab":
                        return IsAllowFloorPos(baseEntity.transform.position, player);

                    case "assets/prefabs/building core/floor.triangle/floor.triangle.prefab":
                    case "assets/prefabs/building core/floor.triangle.frame/floor.triangle.frame.prefab":
                        return IsAllowFloorPos(baseEntity.transform.position + baseEntity.transform.forward, player);

                    case "assets/prefabs/building core/roof/roof.prefab":
                    case "assets/prefabs/building core/roof.triangle/roof.triangle.prefab":
                        return IsAllowRoofPos(baseEntity.transform.position, player);

                    default:
                        return true;
                }
            }

            bool IsAllowBuildingBlockPos(Vector3 buildingPos, BasePlayer player)
            {
                if (buildingPos.y > GetActiveWagon.transform.position.y + localFloorHeight + 2.98f)
                {
                    NotifyManager.SendMessageToPlayer(player, "LimitFloorsForHouse", ownIns._config.NotifyConfig.prefix);
                    return false;
                }
                return true;
            }

            bool IsAllowWallPos(Vector3 buildingPos, BasePlayer player)
            {
                if (buildingPos.y > GetActiveWagon.transform.position.y + localFloorHeight + 0.03f)
                {
                    NotifyManager.SendMessageToPlayer(player, "LimitFloorsForHouse", ownIns._config.NotifyConfig.prefix);
                    return false;
                }
                return true;
            }

            bool IsAllowFloorPos(Vector3 buildingPos, BasePlayer player)
            {
                Vector3 localPos = GetActiveWagon.transform.InverseTransformPoint(buildingPos);

                if (localPos.x > 1.4f || localPos.x < -1.4f || localPos.z > 7.4f || localPos.z < -7.4f)
                {
                    NotifyManager.SendMessageToPlayer(player, "LimitFloorsForHouse", ownIns._config.NotifyConfig.prefix);
                    return false;
                }

                return true;
            }

            bool IsAllowRoofPos(Vector3 buildingPos, BasePlayer player)
            {
                if (!IsAllowFloorPos(buildingPos, player))
                {
                    return false;
                }
                if (!IsAllowWallPos(buildingPos, player))
                {
                    return false;
                }
                return true;
            }

            #endregion IsAllowBuildingBlockPos

            static class CreateBaseWagon
            {
                #region Variables

                static TrainCar trainCar { get; set; }
                static BaseCombatEntity baseWagon { get; set; }

                #endregion Variables

                internal static BaseCombatEntity Start(Vector3 pos, Quaternion rot)
                {
                    trainCar = GameManager.server.CreateEntity("assets/content/vehicles/trains/wagons/trainwagonc.entity.prefab", new Vector3(pos.x, pos.y + 0.2f, pos.z), rot) as TrainCar;

                    RemoveTriggers();

                    baseWagon = trainCar.gameObject.AddComponent<BaseCombatEntity>();

                    CopySerializableFields(trainCar, baseWagon);

                    UnityEngine.Object.DestroyImmediate(trainCar, true);

                    baseWagon.enableSaving = true;

                    RemoveComponents();

                    baseWagon._maxHealth = ownIns._config.HpWagon;
                    baseWagon.startHealth = ownIns._config.HpWagon;
                    baseWagon.health = ownIns._config.HpWagon;

                    return baseWagon;
                }

                static void RemoveTriggers()
                {
                    UnityEngine.Object.Destroy(trainCar.frontCollisionTrigger);
                    UnityEngine.Object.Destroy(trainCar.rearCollisionTrigger);
                    UnityEngine.Object.Destroy(trainCar.hurtTriggerFront);
                    UnityEngine.Object.Destroy(trainCar.hurtTriggerRear);
                    UnityEngine.Object.Destroy(trainCar.platformParentTrigger);
                }

                static void RemoveComponents()
                {
                    Spawnable spawnable = baseWagon.GetComponent<Spawnable>();
                    if (spawnable != null) UnityEngine.Object.Destroy(spawnable);

                    Model model = baseWagon.GetComponent<Model>();
                    if (model != null) UnityEngine.Object.Destroy(model);

                    PrefabParameters prefabParameters = baseWagon.GetComponent<PrefabParameters>();
                    if (prefabParameters != null) UnityEngine.Object.Destroy(prefabParameters);

                    Rigidbody rigidbody = baseWagon.GetComponent<Rigidbody>();
                    if (rigidbody != null) UnityEngine.Object.Destroy(rigidbody);
                }
            }
        }

        class ModificationsWagon : DoorCloser
        {
            Wagon wagon { get; set; }

            CodeLock codelock { get; set; }
            RFReceiver receiver { get; set; }

            CustomRANDSwitch customRANDSwitch { get; set; }
            RANDSwitch randSwitch { get; set; }

            internal int frequency => receiver.frequency;
            internal string code => codelock.code;

            int countdown { get; set; } = 10;
            bool limNet = false;
            bool firstCall = true;

            static HashSet<string> strForLimNet = new HashSet<string>()
            {
                "assets/prefabs/building core/wall.frame/wall.frame.prefab",
                "assets/prefabs/building core/floor.frame/floor.frame.prefab",
                "assets/prefabs/building core/floor.triangle.frame/floor.triangle.frame.prefab"
            };

            internal void Init(BasePlayer player, Wagon wagon, WagonData data)
            {
                this.wagon = wagon;
                InvokeRepeating(Updater, 0f, 1f);

                SetCodelock();
                SetReceiver();
                SetRANDSwitch();

                if (data != null) UpdateWithData(data);
                UpdateConnections();
                SendMessageToPlayer(player);
            }

            internal void InitForFreeWagon(Wagon wagon)
            {
                this.wagon = wagon;
                InvokeRepeating(Updater, 0f, 1f);

                SetFreeCodeLock();
            }

            internal void ChangeStateToLocked(BasePlayer player)
            {
                wagon.ChangeStateToLocked(player);

                SetReceiver();
                SetRANDSwitch();
                UpdateConnections();
                SendMessageToPlayer(player);
            }

            void UpdateConnections()
            {
                receiver.UpdateFromInput(100, 0);

                receiver.outputs[0].connectedTo.Set(customRANDSwitch);
                customRANDSwitch.inputs[0].connectedTo.Set(receiver);
            }

            void SendMessageToPlayer(BasePlayer player)
            {
                NotifyManager.SendMessageToPlayer(player, "CodeOfTheWagon", ownIns._config.NotifyConfig.prefix, codelock.code);
                NotifyManager.SendMessageToPlayer(player, "FrequencyOfTheWagon", ownIns._config.NotifyConfig.prefix, receiver.frequency.ToString());
            }

            void UpdateWithData(WagonData data)
            {
                UpdateCodelockWithData(data);
                UpdateReceiverWithData(data);
            }

            void UpdateCodelockWithData(WagonData data)
            {
                codelock.code = data.code;
                foreach (ulong playerID in data.authPlayers)
                {
                    if (!codelock.whitelistPlayers.Contains(playerID)) codelock.whitelistPlayers.Add(playerID);
                }
            }

            void UpdateReceiverWithData(WagonData data)
            {
                RFManager.ChangeFrequency(receiver.frequency, data.frequency, receiver, isListener: true);
                receiver.frequency = data.frequency;
                receiver.MarkDirty();
                SendNetworkUpdate();
            }

            void Updater()
            {
                if (wagon.trainCar == null) return;
                if (IsStopping()) return;

                countdown--;
                if (countdown == 0) limNet = true;
                if (!firstCall) firstCall = true;

                foreach (BuildingBlock block in wagon.allBuilidngBlocks)
                {
                    if (block == null) return;

                    if (limNet && strForLimNet.Contains(block.PrefabName))
                    {
                        block.limitNetworking = true;
                        block.limitNetworking = false;
                        countdown = 10;
                    }
                    SendNewWrite(block);
                }

                if (limNet) limNet = false;

                foreach (BaseEntity entity in wagon.GetAllDeployedObjects()) entity.SendNetworkUpdate();
            }

            bool IsStopping()
            {
                if (wagon.trainCar.IsStationary())
                {
                    if (firstCall)
                    {
                        LimitNetworkingForBlocks();
                        firstCall = false;
                    }
                    return true;
                }
                return false;
            }

            internal void LimitNetworkingForBlocks()
            {
                foreach (BuildingBlock block in wagon.allBuilidngBlocks)
                {
                    if (block == null) return;

                    block.limitNetworking = true;
                    block.limitNetworking = false;
                }
            }

            void SendNewWrite(BuildingBlock block)
            {
                NetWrite newWrite = Net.sv.StartWrite();

                newWrite.PacketID(Message.Type.RPCMessage);
                newWrite.EntityID(block.net.ID);
                newWrite.UInt32(StringPool.Get("RefreshSkin"));
                newWrite.UInt64(0);
                newWrite.Send(new SendInfo(block.net.group.subscribers));
            }

            void SetCodelock()
            {
                CreateCodelock();

                ownIns.allCodeLocks.Add(codelock);
                codelock.SetParent(this, true, true);
                codelock.transform.rotation = transform.rotation;
                codelock.code = GetRandomCode();
                codelock.hasCode = true;
                codelock.SetFlag(Flags.Locked, true);
                codelock.whitelistPlayers.Add(wagon.owner.userID);
                codelock.Spawn();
            }

            void SetFreeCodeLock()
            {
                CreateCodelock();

                ownIns.allCodeLocks.Add(codelock);
                codelock.SetParent(this, true, true);
                codelock.transform.rotation = transform.rotation;
                codelock.SetFlag(Flags.Locked, false);
                codelock.Spawn();
            }

            void CreateCodelock()
            {
                Vector3 pos = GetGlobalPos(transform, new Vector3(1.382f, 0.0f, 0f));
                codelock = GameManager.server.CreateEntity("assets/prefabs/locks/keypad/lock.code.prefab", pos, Quaternion.Euler(new Vector3(0f, 0f, 0))) as CodeLock;
            }

            string GetRandomCode()
            {
                string randomNumber = "";

                for (int i = 0; i < 4; i++)
                {
                    randomNumber += UnityEngine.Random.Range(0, 10).ToString();
                }

                return randomNumber;
            }

            void SetReceiver()
            {
                CreateReceiver();
                Vector3 rot = receiver.transform.rotation.eulerAngles;

                ownIns.allReceivers.Add(receiver);
                receiver.SetParent(this, true, true);

                receiver.pickup.enabled = false;
                receiver.transform.rotation = Quaternion.Euler(rot);
                receiver.frequency = Convert.ToInt32(codelock.code);

                receiver.Spawn();
            }

            void CreateReceiver()
            {
                Vector3 pos = GetGlobalPos(transform, new Vector3(0.691f, 0f, 0.994f));
                Quaternion rot = GetGlobalRot(transform, new Vector3(90f, 90f, 0f));

                receiver = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/gates/rfreceiver/rfreceiver.prefab", pos, rot) as RFReceiver;
            }

            void SetRANDSwitch()
            {
                CreateRANDSwitch();

                customRANDSwitch = randSwitch.gameObject.AddComponent<CustomRANDSwitch>();

                CopySerializableFields(randSwitch, customRANDSwitch);
                DestroyImmediate(randSwitch, true);

                Vector3 rot = customRANDSwitch.transform.rotation.eulerAngles;
                ownIns.allSwitches.Add(customRANDSwitch);
                customRANDSwitch.SetParent(this, true, true);
                customRANDSwitch.transform.rotation = Quaternion.Euler(rot);

                customRANDSwitch.wagon = wagon;
                customRANDSwitch.pickup.enabled = false;

                customRANDSwitch.Spawn();
            }

            void CreateRANDSwitch()
            {
                Vector3 pos = GetGlobalPos(transform, new Vector3(0f, 0f, 0f));
                Quaternion rot = GetGlobalRot(transform, new Vector3(90f, 180f, 0f));

                randSwitch = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/gates/randswitch/electrical.random.switch.deployed.prefab", pos, rot) as RANDSwitch;
            }

            internal void RemoveModifications()
            {
                CancelInvoke(Updater);

                ownIns.allSwitches.Remove(customRANDSwitch);
                ownIns.allReceivers.Remove(receiver);
                ownIns.allCodeLocks.Remove(codelock);

                codelock.Kill();
                if (!wagon.freeWagon)
                {
                    customRANDSwitch.Kill();
                    RFManager.RemoveListener(receiver.frequency, receiver);
                    receiver.Kill();
                }
            }

            internal HashSet<ulong> GetWhitelist()
            {
                return codelock.whitelistPlayers.ToHashSet();
            }

            internal HashSet<ulong> GetGuestList()
            {
                return codelock.guestPlayers.ToHashSet();
            }
        }

        class CustomRANDSwitch : RANDSwitch
        {
            internal Wagon wagon { get; set; }

            ModificationsWagon modifications { get; set; }
            BasePlayer caller;

            bool firstCall = true;
            bool falseCall = false;

            public override void UpdateFromInput(int inputAmount, int inputSlot)
            {
                if (firstCall)
                {
                    modifications = GetParentEntity() as ModificationsWagon;
                    firstCall = false;
                    return;
                }

                if (falseCall)
                {
                    falseCall = false;
                    return;
                }

                falseCall = true;

                if (inputAmount > 0)
                {
                    if (wagon.onCustomMonument)
                    {
                        NotifyManager.SendMessageToPlayer(caller, "WagonOnMonument", ownIns._config.NotifyConfig.prefix);
                        return;
                    }

                    if (wagon.inGarage) TrySpawnTrainCar();
                    else TrySpawnBaseWagon();

                    caller = null;
                }
            }

            void TrySpawnTrainCar()
            {
                caller = GetCallerInWhitelist();

                if (caller != null)
                {
                    if (ownIns.callers.Contains(caller))
                    {
                        NotifyManager.SendMessageToPlayer(caller, "CantCallAnotherWagon", ownIns._config.NotifyConfig.prefix);
                        return;
                    }
                    if (IsLongDistance()) return;
                    if (Interface.CallHook("OnWagonSpawn", caller) is bool) return;
                    ownIns.callers.Add(caller);
                    wagon.ThrowAwayFlare(caller);
                    return;
                }
            }

            void TrySpawnBaseWagon()
            {
                caller = GetCallerInWhitelist();

                if (caller == null) return;

                if (ownIns.callers.Contains(caller))
                {
                    NotifyManager.SendMessageToPlayer(caller, "CantCallAnotherWagon", ownIns._config.NotifyConfig.prefix);
                    return;
                }
                if (IsLongDistance()) return;
                if (Interface.CallHook("OnBaseWagonSpawn", caller) is bool) return;
                ownIns.callers.Add(caller);
                wagon.ThrowAwayFlare(caller);
            }

            BasePlayer GetCallerInWhitelist()
            {
                HashSet<BasePlayer> callers = new HashSet<BasePlayer>();

                foreach (ulong steamID in modifications.GetWhitelist())
                {
                    BasePlayer possibleCaller = BasePlayer.FindByID(steamID);
                    if (possibleCaller == null || possibleCaller.IsSleeping()) continue;

                    HeldEntity heldEntity = possibleCaller.GetHeldEntity();
                    if (heldEntity == null || heldEntity.ShortPrefabName != "detonator.entity") continue;

                    Detonator detonator = heldEntity as Detonator;
                    if (detonator.frequency == modifications.frequency)
                    {
                        caller = possibleCaller;
                        callers.Add(caller);
                    }
                }

                if (callers.Count > 1)
                {
                    foreach (BasePlayer caller in callers)
                    {
                        NotifyManager.SendMessageToPlayer(caller, "TwoCallers", ownIns._config.NotifyConfig.prefix);
                    }
                    return null;
                }

                return caller;
            }

            bool IsLongDistance()
            {
                float disctance = 0;
                if (wagon.baseWagon != null) disctance = Vector3.Distance(wagon.baseWagon.transform.position, caller.transform.position);
                else disctance = Vector3.Distance(wagon.trainCar.transform.position, caller.transform.position);

                if (disctance > ownIns._config.MaxDistance)
                {
                    NotifyManager.SendMessageToPlayer(caller, "LongDistance", ownIns._config.NotifyConfig.prefix, disctance, (float)ownIns._config.MaxDistance);
                    return true;
                }
                return false;
            }
        }

        class SpawnerWagon : FacepunchBehaviour
        {
            delegate void MyDelegate();
            MyDelegate methodBeingCalled;

            const int validTopologies = (int)(TerrainTopology.Enum.Rail | TerrainTopology.Enum.Railside);

            Wagon wagon { get; set; }

            BuildingPrivlidge cupboard;
            BasePlayer caller { get; set; }
            BaseEntity entityCollision { get; set; }
            BaseEntity thisEntity { get; set; }

            bool active = false;

            int countdown = 3;

            internal void Init(Wagon wagon, BasePlayer player)
            {
                this.wagon = wagon;
                caller = player;
            }

            void OnCollisionEnter(Collision collision)
            {
                if (!active)
                {
                    thisEntity = gameObject.GetComponent<BaseEntity>();
                    active = true;

                    if (IsCollisionInBase(collision)) ownIns.NextTick(() => TryMoveWagonToBase());
                    else TryMoveWagonOnTracks();
                }
            }

            bool IsCollisionInBase(Collision collision)
            {
                entityCollision = collision.GetEntity();
                if (entityCollision == null) return false;
                return IsTargetValid(thisEntity, entityCollision, caller);
            }

            void TryMoveWagonToBase()
            {
                if (!IsValidVariablesForBase(caller, entityCollision, out cupboard))
                {
                    CancelSpawn();
                    return;
                }
                if (IsWagonAlreadyBeingCalled()) return;
                if (IsWagonInGarage()) return;
                methodBeingCalled = SpawnInBase;
                InvokeRepeating(Timer, 0f, 1f);
            }

            void TryMoveWagonOnTracks()
            {
                if (ownIns._config.CheckTopology && !IsValidTopologies()) return;
                if (IsWagonAlreadyBeingCalled()) return;
                if (IsWagonOnTrack()) return;
                methodBeingCalled = SpawnOnTrack;
                InvokeRepeating(Timer, 0f, 1f);
            }

            bool IsValidTopologies()
            {
                int currentTopologies = TerrainMeta.TopologyMap.GetTopology(gameObject.transform.position);
                if ((currentTopologies & validTopologies) == 0)
                {
                    NotifyManager.SendMessageToPlayer(caller, "NotValidTopologies", ownIns._config.NotifyConfig.prefix);
                    CancelSpawn();
                    return false;
                }
                return true;
            }

            bool IsWagonAlreadyBeingCalled()
            {
                if (ownIns.wagonsBeingCalled.Contains(wagon))
                {
                    NotifyManager.SendMessageToPlayer(caller, "WagonAlreadyCalled", ownIns._config.NotifyConfig.prefix);
                    thisEntity.Kill();
                    return true;
                }
                ownIns.wagonsBeingCalled.Add(wagon);
                return false;
            }

            bool IsWagonInGarage()
            {
                if (wagon.inGarage)
                {
                    NotifyManager.SendMessageToPlayer(caller, "WagonAlreadyInGarage", ownIns._config.NotifyConfig.prefix);
                    CancelSpawn();
                    return true;
                }
                return false;
            }

            bool IsWagonOnTrack()
            {
                if (!wagon.inGarage)
                {
                    NotifyManager.SendMessageToPlayer(caller, "WagonAlreadyOnTracks", ownIns._config.NotifyConfig.prefix);
                    CancelSpawn();
                    return true;
                }
                return false;
            }

            void Timer()
            {
                if (countdown > 0)
                {
                    NotifyManager.SendMessageToPlayer(caller, "TimerForSpawn", ownIns._config.NotifyConfig.prefix, countdown);
                    countdown--;
                    return;
                }
                methodBeingCalled();
                CancelSpawn();
                CancelInvoke(Timer);
            }

            void SpawnInBase()
            {
                ownIns.TryCreateNewPosInGarage(entityCollision, caller, cupboard, wagon);
            }

            void SpawnOnTrack()
            {
                wagon.MoveWagonToTrack(gameObject.transform.position, caller);
            }

            void CancelSpawn()
            {
                thisEntity.Kill();
                ownIns.wagonsBeingCalled.Remove(wagon);
                ownIns.callers.Remove(caller);
            }
        }

        internal class Garage
        {
            Dictionary<KeyValuePair<Vector3, Quaternion>, HashSet<BuildingBlock>> placesForWagons { get; set; } = new Dictionary<KeyValuePair<Vector3, Quaternion>, HashSet<BuildingBlock>>();
            internal Dictionary<Vector3, Wagon> wagons { get; set; } = new Dictionary<Vector3, Wagon>();

            internal void InitWithData(GarageData garageData)
            {
                foreach (PosInGarageData posData in garageData.poss)
                {
                    if (!ownIns.doorCloserToWagon.ContainsKey(posData.idObjectForUnload)) continue;

                    HashSet<BuildingBlock> foundations = new HashSet<BuildingBlock>();
                    bool isValidPos = true;
                    CheckAllFoundations(posData, foundations, ref isValidPos);

                    if (isValidPos) AddInGlobalHashSet(foundations);
                    else continue;

                    KeyValuePair<Vector3, Quaternion> kvp = new KeyValuePair<Vector3, Quaternion>(posData.pos.ToVector3(), Quaternion.Euler(posData.rot.ToVector3()));

                    placesForWagons.Add(kvp, foundations);
                    DetermineWagons(posData);
                }
            }

            void CheckAllFoundations(PosInGarageData posData, HashSet<BuildingBlock> foundations, ref bool isValidPos)
            {
                foreach (ulong idFoundation in posData.foundations)
                {
                    BuildingBlock foundation = BaseNetworkable.serverEntities.entityList.Get().FirstOrDefault(p => p.Value.net.ID.Value == idFoundation).Value as BuildingBlock;
                    if (foundation == null)
                    {
                        isValidPos = false;
                        Wagon wagon = ownIns.doorCloserToWagon[posData.idObjectForUnload];
                        wagon.RemoveWagon();
                        break;
                    }
                    foundations.Add(foundation);
                }
            }

            void AddInGlobalHashSet(HashSet<BuildingBlock> foundations)
            {
                foreach (BuildingBlock foundation in foundations)
                {
                    ownIns.allFoundationsUnderWagons.Add(foundation.net.ID.Value, this);
                }
            }

            void DetermineWagons(PosInGarageData posData)
            {
                if (!ownIns.doorCloserToWagon.ContainsKey(posData.idObjectForUnload)) return;

                Wagon wagon = ownIns.doorCloserToWagon[posData.idObjectForUnload];
                wagons.Add(posData.pos.ToVector3(), wagon);
                wagon.garage = this;
            }

            internal void AddNewPos(KeyValuePair<Vector3, Quaternion> newPosForWagon, HashSet<BuildingBlock> foundationsUnderWagon)
            {
                if (!placesForWagons.ContainsKey(newPosForWagon)) placesForWagons.Add(newPosForWagon, foundationsUnderWagon);

                foreach (BuildingBlock block in foundationsUnderWagon)
                {
                    if (!ownIns.allFoundationsUnderWagons.ContainsKey(block.net.ID.Value)) ownIns.allFoundationsUnderWagons.Add(block.net.ID.Value, this);
                }
            }

            internal void TryRemoveWagon(Wagon wagon)
            {
                foreach (KeyValuePair<Vector3, Wagon> kvp in wagons.ToHashSet())
                {
                    if (kvp.Value != wagon) continue;

                    RemovePlace(kvp.Key);
                    wagons.Remove(kvp.Key);
                }
            }

            void RemovePlace(Vector3 pos)
            {
                KeyValuePair<KeyValuePair<Vector3, Quaternion>, HashSet<BuildingBlock>> kvp = placesForWagons.FirstOrDefault(x => x.Key.Key == pos);

                foreach (BuildingBlock block in kvp.Value)
                {
                    ownIns.allFoundationsUnderWagons.Remove(block.net.ID.Value);
                }

                placesForWagons.Remove(kvp.Key);
            }

            internal void DestroyAllWagons()
            {
                foreach (KeyValuePair<Vector3, Wagon> kvp in wagons.ToHashSet())
                {
                    kvp.Value.RemoveWagon();
                }
            }

            internal Vector3 GetPosWagon(BuildingBlock foundation)
            {
                return placesForWagons.FirstOrDefault(x => x.Value.Contains(foundation)).Key.Key;
            }

            internal Wagon GetWagon(Vector3 pos)
            {
                return wagons.FirstOrDefault(x => x.Key == pos).Value;
            }

            internal GarageData CreateInfoForData(ulong cupboard)
            {
                GarageData garageData = new GarageData();

                garageData.idCupboard = cupboard;

                foreach (KeyValuePair<KeyValuePair<Vector3, Quaternion>, HashSet<BuildingBlock>> kvp in placesForWagons)
                {
                    Vector3 posWagon = kvp.Key.Key;
                    Vector3 rotWagon = kvp.Key.Value.eulerAngles;
                    PosInGarageData posInGarageData = new PosInGarageData();

                    posInGarageData.pos = posWagon.ToString();
                    posInGarageData.rot = rotWagon.ToString();
                    if (wagons.ContainsKey(posWagon)) posInGarageData.idObjectForUnload = wagons[posWagon].objectForUnload.net.ID.Value;
                    else posInGarageData.idObjectForUnload = 0;
                    foreach (BuildingBlock foundation in kvp.Value)
                    {
                        posInGarageData.foundations.Add(foundation.net.ID.Value);
                    }

                    garageData.poss.Add(posInGarageData);
                }

                return garageData;
            }
        }

        internal class ConstructionMatrix
        {
            BasePlayer player { get; set; }
            BuildingBlock mainFoundation { get; set; }
            internal KeyValuePair<Vector3, Quaternion> posForSpawnWagon { get; set; }
            internal bool isValid = false;

            List<BuildingBlock> blocksAroundMainFoundation;
            internal HashSet<BuildingBlock> blocksUnderWagon { get; set; } = new HashSet<BuildingBlock>();


            List<BuildingBlock> blocks;
            HashSet<string> poss { get; set; } = new HashSet<string>();


            readonly static HashSet<string> validLocalPosFoundations1 = new HashSet<string>()
            {
                "(-3, 0, 0)",
                "(-6, 0, 0)",
                "(-9, 0, 0)",
                "(3, 0, 0)",
                "(6, 0, 0)",
                "(9, 0, 0)",
                "(0, 0, 3)",
                "(0, 0, -3)",
                "(3, 0, 3)",
                "(6, 0, 3)",
                "(9, 0, 3)",
                "(3, 0, -3)",
                "(6, 0, -3)",
                "(9, 0, -3)",
                "(-3, 0, 3)",
                "(-6, 0, 3)",
                "(-9, 0, 3)",
                "(-3, 0, -3)",
                "(-6, 0, -3)",
                "(-9, 0, -3)"
            };

            readonly static HashSet<string> validLocalPosFoundations2 = new HashSet<string>()
            {
                "(0, 0, 3)",
                "(0, 0, 6)",
                "(0, 0, 9)",
                "(0, 0, -3)",
                "(0, 0, -6)",
                "(0, 0, -9)",
                "(3, 0, 0)",
                "(-3, 0, 0)",
                "(-3, 0, 3)",
                "(-3, 0, 6)",
                "(-3, 0, 9)",
                "(-3, 0, -3)",
                "(-3, 0, -6)",
                "(-3, 0, -9)",
                "(3, 0, -3)",
                "(3, 0, -6)",
                "(3, 0, -9)",
                "(3, 0, 3)",
                "(3, 0, 6)",
                "(3, 0, 9)",
            };

            internal void Init(BaseEntity baseEntity, List<BuildingBlock> foundations, BasePlayer player)
            {
                this.player = player;

                mainFoundation = baseEntity as BuildingBlock;
                blocksAroundMainFoundation = foundations;

                if (blocksAroundMainFoundation.Contains(mainFoundation)) blocksAroundMainFoundation.Remove(mainFoundation);

                StartCheck();

                blocksUnderWagon.Add(mainFoundation);

                Pool.FreeList(ref blocksAroundMainFoundation);
                Pool.FreeList(ref blocks);
            }

            void StartCheck()
            {
                if (IsValidConstruction(validLocalPosFoundations1))
                {
                    isValid = true;
                    return;
                }

                blocksUnderWagon.Clear();
                if (IsValidConstruction(validLocalPosFoundations2))
                {
                    isValid = true;
                    return;
                }
            }

            bool IsValidConstruction(HashSet<string> hashSet)
            {
                UpdateVariables(hashSet);
                CheckFoundations(poss);

                if (poss.Count == 0 && IsPosFoundationsValid() && !IsExistFoundationsInGlobalHashSet())
                {
                    Quaternion quaternion = GetQuaternionForSpawnWagon(hashSet);
                    posForSpawnWagon = new KeyValuePair<Vector3, Quaternion>(mainFoundation.transform.position, quaternion);
                    return true;
                }

                return false;
            }

            Quaternion GetQuaternionForSpawnWagon(HashSet<string> hashSet)
            {
                Vector3 localPos = hashSet.ElementAt(3).ToVector3();
                Vector3 pos1 = GetGlobalPos(mainFoundation.transform, localPos);

                return Quaternion.LookRotation(pos1 - mainFoundation.transform.position);
            }

            void UpdateVariables(HashSet<string> hashSet)
            {
                blocks = blocksAroundMainFoundation.ToList();
                poss = new HashSet<string>(hashSet);
            }

            void CheckFoundations(HashSet<string> validLocalPosFoundationsCopy)
            {
                for (int i = 0; i < blocks.Count; i++)
                {
                    BuildingBlock block = blocks[i];

                    Vector3 localPos = mainFoundation.transform.InverseTransformPoint(block.transform.position);

                    if (validLocalPosFoundationsCopy.Count == 0) break;
                    foreach (string stringPos in validLocalPosFoundationsCopy.ToHashSet())
                    {
                        if (Vector3.Distance(stringPos.ToVector3(), localPos) < 0.2f)
                        {
                            blocksUnderWagon.Add(block);
                            blocks.Remove(block);

                            validLocalPosFoundationsCopy.Remove(stringPos);

                            i--;
                            break;
                        }
                    }
                }
            }

            bool IsPosFoundationsValid()
            {
                float posY1 = mainFoundation.transform.position.y;

                foreach (BuildingBlock buildingBlock in blocksUnderWagon)
                {
                    float posY2 = buildingBlock.transform.position.y;

                    if (Vector3.Distance(new Vector3(0f, posY1, 0f), new Vector3(0f, posY2, 0f)) > 0.1f) return false;
                }

                return true;
            }

            bool IsExistFoundationsInGlobalHashSet()
            {
                foreach (BuildingBlock buildingBlock in blocksUnderWagon)
                {
                    if (ownIns.allFoundationsUnderWagons.ContainsKey(buildingBlock.net.ID.Value))
                    {
                        NotifyManager.SendMessageToPlayer(player, "FoundationsExistInHashSet", ownIns._config.NotifyConfig.prefix);
                        return true;
                    }
                }
                return false;
            }
        }

        internal static class ControllerSpawnsFreeWagon
        {
            static HashSet<TrainCar> trainCars = new HashSet<TrainCar>();
            internal static HashSet<TrainCar> freeWagons = new HashSet<TrainCar>();
            internal static HashSet<string> strValidWagons = new HashSet<string>()
            {
                "trainwagona.entity",
                "trainwagonb.entity",
                "trainwagonc.entity"
            };

            static Coroutine timer { get; set; } = null;

            internal static void Init()
            {
                trainCars = BaseNetworkable.serverEntities.entityList.Get().Where(x => x.Value is TrainCar && strValidWagons.Contains(x.Value.ShortPrefabName) && !ownIns.allTrainCars.ContainsKey(x.Key.Value)).Select(c => c.Value as TrainCar);

                ownIns.onServerInit = true;

                timer = ServerMgr.Instance.StartCoroutine(Timer());
            }

            internal static void Unload()
            {
                if (timer != null) ServerMgr.Instance.StopCoroutine(timer);
                timer = null;
                
                foreach (TrainCar trainCar in freeWagons) if (trainCar.IsExists()) trainCar.Kill();
                
                freeWagons.Clear();
                trainCars.Clear();
            }

            static IEnumerator Timer()
            {
                while (true)
                {
                    if (freeWagons.Count < ownIns._config.amountFreeWagons && trainCars.Count > 0)
                    {
                        foreach (TrainCar trainCar in trainCars.ToHashSet())
                        {
                            if (freeWagons.Count == ownIns._config.amountFreeWagons) break;
                            if (trainCar == null || trainCar.net == null || 
                                ownIns.allTrainCars.ContainsKey(trainCar.net.ID.Value) || ownIns.allBaseWagons.ContainsKey(trainCar.net.ID.Value))
                            {
                                trainCars.Remove(trainCar);
                                continue;
                            }

                            trainCar.Kill();
                            Wagon wagon = new Wagon();
                            wagon.InitFreeWagon(trainCar.transform.position, trainCar.transform.rotation);
                            CheckTrainCar(wagon, trainCar);
                        }
                    }

                    ownIns.PrintWarning($"Count of free wagons: {freeWagons.Count}");
                    yield return CoroutineEx.waitForSeconds(750.0f);
                }
            }

            static void CheckTrainCar(Wagon wagon, TrainCar trainCar)
            {
                if (wagon.trainCar == null)
                {
                    wagon.RemoveWagon();
                    wagon = null;
                    return;
                }

                trainCars.Remove(trainCar);
                freeWagons.Add(wagon.trainCar);
            }

            internal static void RemoveFreeWagon(TrainCar trainCar)
            {
                freeWagons.Remove(trainCar);
                trainCars.Remove(trainCar);
            }

            internal static void TryAddNewTrainCar(TrainCar trainCar)
            {
                if (trainCar == null || trainCar.net == null) return;
                
                if (IsValidTrainCar(trainCar)) trainCars.Add(trainCar);
            }

            static bool IsValidTrainCar(TrainCar trainCar)
            {
                if (ownIns.onServerInit &&
                    strValidWagons.Contains(trainCar.ShortPrefabName) &&
                    //!ownIns.allBaseWagons.ContainsKey(trainCar.net.ID.Value) &&
                    //!ownIns.allTrainCars.ContainsKey(trainCar.net.ID.Value) &&
                    !freeWagons.Contains(trainCar)) return true;
                
                return false;
            }

            internal static void ShowPosFreeWagons(BasePlayer player)
            {
                ownIns.Puts($"Count Free Wagons: {freeWagons.Count}");

                foreach (TrainCar trainCar in freeWagons)
                {
                    player.SendConsoleCommand("ddraw.line", 10f, Color.green, trainCar.transform.position, trainCar.transform.position + Vector3.up * 200f);
                }
            }
        }

        internal class CustomMonument : FacepunchBehaviour
        {
            ElectricalBranch branch { get; set; }
            PressButton button { get; set; }
            AnimationTransformVehicle animation { get; set; }
            TrainCar activeTrainCar { get; set; }

            BoxCollider collider { get; set; }
            HashSet<BasePlayer> playersInside { get; set; } = new HashSet<BasePlayer>();

            internal bool isBuildingState = false;
            static int delay = 200;
            Vector3 localPosForWagon = new Vector3(0f, 0.413f, 0f);

            HashSet<BaseEntity> objectsOfWalls { get; set; } = new HashSet<BaseEntity>();

            readonly static Dictionary<Vector3, Vector3> possBarricades = new Dictionary<Vector3, Vector3>
            {
                [new Vector3(-2.415f, 0f, 12.626f)] = new Vector3(0f, 332.401f, 0f),
                [new Vector3(0f, 0f, 13.194f)] = new Vector3(0f, 0f, 0f),
                [new Vector3(2.415f, 0f, 12.626f)] = new Vector3(0f, 27.6f, 0f),
                [new Vector3(2.415f, 0f, -11.430f)] = new Vector3(0f, 152.401f, 0f),
                [new Vector3(0f, 0f, -11.998f)] = new Vector3(0f, 180f, 0f),
                [new Vector3(-2.415f, 0f, -11.430f)] = new Vector3(0f, 207.6f, 0f)
            };
            readonly static Dictionary<Vector3, Vector3> possStairs = new Dictionary<Vector3, Vector3>
            {
                [new Vector3(2.988f, -0.376f, -11.142f)] = new Vector3(0f, 242.401f, 63.175f),
                [new Vector3(1.852f, -0.376f, -11.737f)] = new Vector3(0f, 242.401f, 63.175f),
                [new Vector3(0.641f, -0.376f, -12.009f)] = new Vector3(0f, 270f, 63.175f),
                [new Vector3(-0.641f, -0.376f, -12.009f)] = new Vector3(0f, 270f, 63.175f),
                [new Vector3(-2.988f, -0.376f, -11.142f)] = new Vector3(0f, 297.6f, 63.175f),
                [new Vector3(-1.852f, -0.376f, -11.737f)] = new Vector3(0f, 297.6f, 63.175f),
                [new Vector3(-2.988f, -0.376f, 12.339f)] = new Vector3(0f, 62.4f, 63.175f),
                [new Vector3(-1.852f, -0.376f, 12.933f)] = new Vector3(0f, 62.4f, 63.175f),
                [new Vector3(0.641f, -0.376f, 13.206f)] = new Vector3(0f, 90f, 63.175f),
                [new Vector3(-0.641f, -0.376f, 13.205f)] = new Vector3(0f, 90f, 63.175f),
                [new Vector3(2.988f, -0.376f, 12.339f)] = new Vector3(0f, 117.6f, 63.175f),
                [new Vector3(1.852f, -0.376f, 12.932f)] = new Vector3(0f, 117.6f, 63.175f),
            };
            readonly static Dictionary<Vector3, Vector3> possSigns = new Dictionary<Vector3, Vector3>
            {
                [new Vector3(-2.44f, 0.268f, 12.674f)] = new Vector3(0f, 152.4f, 0f),
                [new Vector3(0f, 0.268f, 13.248f)] = new Vector3(0f, 180f, 0f),
                [new Vector3(2.44f, 0.268f, 12.674f)] = new Vector3(0f, 207.6f, 0f),
                [new Vector3(2.44f, 0.268f, -11.477f)] = new Vector3(0f, 332.4f, 0f),
                [new Vector3(0f, 0.268f, -12.052f)] = new Vector3(0f, 0f, 0f),
                [new Vector3(-2.44f, 0.268f, -11.477f)] = new Vector3(0f, 27.6f, 0f),
            };
            readonly static Dictionary<Vector3, Vector3> possDoorBarricades = new Dictionary<Vector3, Vector3>
            {
                [new Vector3(2.4f, -0.738f, -11.4f)] = new Vector3(0f, 332.4f, 0f),
                [new Vector3(0f, -0.738f, -11.965f)] = new Vector3(0f, 0f, 0f),
                [new Vector3(-2.4f, -0.738f, -11.4f)] = new Vector3(0f, 27.6f, 0f),
                [new Vector3(-2.4f, -0.738f, 12.597f)] = new Vector3(0f, 152.4f, 0f),
                [new Vector3(0f, -0.738f, 13.162f)] = new Vector3(0f, 180f, 0f),
                [new Vector3(2.4f, -0.738f, 12.597f)] = new Vector3(0f, 207.6f, 0f),
            };
            readonly static Dictionary<Vector3, Vector3> possRoadSigns = new Dictionary<Vector3, Vector3>
            {
                [new Vector3(-2.46f, -2.013f, 12.712f)] = new Vector3(346.296f, 62.4f, 0f),
                [new Vector3(-2.46f, -1.29f, 12.712f)] = new Vector3(0f, 62.4f, 0f),
                [new Vector3(-2.46f, -2.013f, 12.713f)] = new Vector3(13.172f, 62.4f, 0f),
                [new Vector3(0f, -2.013f, 13.291f)] = new Vector3(346.296f, 90f, 0f),
                [new Vector3(0f, -1.29f, 13.291f)] = new Vector3(0f, 90f, 0f),
                [new Vector3(0f, -2.013f, 13.292f)] = new Vector3(13.172f, 90f, 0f),
                [new Vector3(2.46f, -2.013f, 12.712f)] = new Vector3(346.296f, 117.6f, 0f),
                [new Vector3(2.46f, -1.29f, 12.712f)] = new Vector3(0f, 117.6f, 0f),
                [new Vector3(2.46f, -2.013f, 12.713f)] = new Vector3(13.172f, 117.6f, 0f),
                [new Vector3(2.46f, -2.013f, -11.515f)] = new Vector3(346.296f, 242.4f, 0f),
                [new Vector3(2.46f, -1.29f, -11.515f)] = new Vector3(0f, 242.4f, 0f),
                [new Vector3(2.46f, -2.013f, -11.516f)] = new Vector3(13.172f, 242.4f, 0f),
                [new Vector3(0f, -2.013f, -12.095f)] = new Vector3(346.296f, 270f, 0f),
                [new Vector3(0f, -1.29f, -12.095f)] = new Vector3(0f, 270f, 0f),
                [new Vector3(0f, -2.013f, -12.096f)] = new Vector3(13.172f, 270f, 0f),
                [new Vector3(-2.46f, -2.013f, -11.515f)] = new Vector3(346.296f, 297.6f, 0f),
                [new Vector3(-2.46f, -1.29f, -11.515f)] = new Vector3(0f, 297.6f, 0f),
                [new Vector3(-2.46f, -2.013f, -11.516f)] = new Vector3(13.172f, 297.6f, 0f)
            };

            readonly static HashSet<string> strRoadSigns = new HashSet<string>()
            {
                "assets/content/props/roadsigns/roadsign1.prefab",
                "assets/content/props/roadsigns/roadsign2.prefab",
                "assets/content/props/roadsigns/roadsign3.prefab",
                "assets/content/props/roadsigns/roadsign4.prefab",
                "assets/content/props/roadsigns/roadsign5.prefab",
                "assets/content/props/roadsigns/roadsign6.prefab",
                "assets/content/props/roadsigns/roadsign7.prefab",
                "assets/content/props/roadsigns/roadsign8.prefab",
                "assets/content/props/roadsigns/roadsign9.prefab"
            };

            private void OnTriggerEnter(Collider other)
            {
                BasePlayer player = other.GetComponentInParent<BasePlayer>();
                if (player.IsPlayer())
                {
                    OnEnterPlayer(player);
                }
            }

            void OnEnterPlayer(BasePlayer player)
            {
                playersInside.Add(player);
                if (!ownIns.playersOnMonuments.ContainsKey(player)) ownIns.playersOnMonuments.Add(player, this);
            }

            private void OnTriggerExit(Collider other)
            {
                BasePlayer player = other.GetComponentInParent<BasePlayer>();
                if (player.IsPlayer())
                {
                    OnExitPlayer(player);
                }
            }

            internal void OnExitPlayer(BasePlayer player)
            {
                playersInside.Remove(player);
                ownIns.playersOnMonuments.Remove(player);
            }

            internal void Init(ElectricalBranch branch)
            {
                Vector3 pos = branch.transform.position;
                transform.position = new Vector3(pos.x, pos.y, pos.z + 0.6f);
                transform.rotation = branch.transform.rotation;
                InitCollider();

                this.branch = branch;
                CreateButton();
            }

            internal void InitCollider()
            {
                gameObject.layer = 3;
                collider = gameObject.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.size = new Vector3(17f, 18.665f, 32f);
            }

            void CreateButton()
            {
                Vector3 pos = GetGlobalPos(branch.transform, new Vector3(3.411f, 0.057f, -4.668f));
                Quaternion rot = GetGlobalRot(branch.transform, new Vector3(0f, 0f, 0f));

                button = GameManager.server.CreateEntity("assets/prefabs/io/electric/switches/pressbutton/pressbutton.prefab", pos, rot) as PressButton;

                button.enableSaving = false;
                button.pickup.enabled = false;

                button.Spawn();

                ownIns.buttonsOnMonuments.Add(button, this);
            }

            internal void TryUpdateState(BasePlayer player)
            {
                if (isBuildingState)
                {
                    UpdateState(activeTrainCar);
                    return;
                }

                HashSet<TrainCar> trainCarsArround = GetTrainCarsArroundMonument();

                if (!IsValidCountTrainCar(trainCarsArround, player)) return;
                TrainCar trainCar = trainCarsArround.ElementAt(0);
                if (!IsCustomWagon(trainCar, player)) return;
                if (IsFreeWagon(trainCar, player)) return;
                TryDisconnectOtherTrainCars(trainCar);
                AddAnimationToTrainCar(trainCar, player);
            }

            HashSet<TrainCar> GetTrainCarsArroundMonument()
            {
                Vector3 pos = branch.transform.position;
                return GetEntities<TrainCar>(new Vector3(pos.x, pos.y, pos.z + 0.6f), 14f, -1);
            }

            bool IsValidCountTrainCar(HashSet<TrainCar> trainCarsArround, BasePlayer player)
            {
                if (trainCarsArround.Count == 0)
                {
                    NotifyManager.SendMessageToPlayer(player, "NotHaveWagons", ownIns._config.NotifyConfig.prefix);
                    return false;
                }

                if (trainCarsArround.Count > 1)
                {
                    NotifyManager.SendMessageToPlayer(player, "MuchWagons", ownIns._config.NotifyConfig.prefix);
                    return false;
                }

                return true;
            }

            bool IsCustomWagon(TrainCar trainCar, BasePlayer player)
            {
                if (!ownIns.allTrainCars.ContainsKey(trainCar.net.ID.Value))
                {
                    NotifyManager.SendMessageToPlayer(player, "NotCustomWagon", ownIns._config.NotifyConfig.prefix);
                    return false;
                }

                return true;
            }

            bool IsFreeWagon(TrainCar trainCar, BasePlayer player)
            {
                Wagon wagon = ownIns.allTrainCars[trainCar.net.ID.Value];
                if (wagon.freeWagon)
                {
                    NotifyManager.SendMessageToPlayer(player, "CantBuildingOnFreeWagon", ownIns._config.NotifyConfig.prefix);
                    return true;
                }

                return false;
            }

            void TryDisconnectOtherTrainCars(TrainCar trainCar)
            {
                trainCar.coupling.frontCoupling.Uncouple(true);
                trainCar.coupling.rearCoupling.Uncouple(true);
            }

            void AddAnimationToTrainCar(TrainCar trainCar, BasePlayer player)
            {
                animation = trainCar.gameObject.AddComponent<AnimationTransformVehicle>();

                PointAnimationTransform point2 = new PointAnimationTransform
                {
                    Pos = GetGlobalPos(branch.transform, localPosForWagon),
                    Rot = GetGlobalRot(branch.transform, new Vector3(0f, 0f, 0f)).eulerAngles
                };
                animation.whoTryMoveWagon = player;
                animation.customMonument = this;
                animation.AddPath(point2, 5f);
            }

            internal void TryEnabledBuildingState(BasePlayer player)
            {
                UnityEngine.Object.DestroyImmediate(animation);
                HashSet<TrainCar> trainCarsArround = GetTrainCarsArroundMonument();
                if (!IsValidCountTrainCar(trainCarsArround, player)) return;

                UpdateState(trainCarsArround.ElementAt(0));
            }

            internal void UpdateState(TrainCar trainCar)
            {
                if (isBuildingState)
                {
                    activeTrainCar = null;
                    isBuildingState = false;
                    DestroyWalls();
                }
                else
                {
                    activeTrainCar = trainCar;
                    isBuildingState = true;
                    CreateWalls();
                }

                Wagon wagon = GetWagon(trainCar);
                wagon.UpdateStateOnMonument(isBuildingState);
            }

            void DestroyWalls()
            {
                if (objectsOfWalls.Count > 0)
                {
                    Effect.server.Run("assets/bundled/prefabs/fx/building/stone_gib.prefab", GetGlobalPos(branch.transform, new Vector3(0f, 0f, 13.194f)));
                    Effect.server.Run("assets/bundled/prefabs/fx/building/stone_gib.prefab", GetGlobalPos(branch.transform, new Vector3(0f, 0f, -11.998f)));
                }

                foreach (BaseEntity entity in objectsOfWalls.ToHashSet())
                {
                    ownIns.allObjectsOfWalls.Remove(entity.net.ID.Value);
                    objectsOfWalls.Remove(entity);
                    entity.Kill();
                }
            }

            async void CreateWalls()
            {
                await SortThroughPossOfObject(possBarricades, "assets/prefabs/deployable/barricades/barricade.concrete.prefab");
                await SortThroughPossOfObject(possStairs, "assets/prefabs/building core/foundation.steps/foundation.steps.prefab");
                await SortThroughPossOfObject(possSigns, "assets/prefabs/misc/xmas/neon_sign/sign.neon.xl.prefab");
                await SortThroughPossOfObject(possDoorBarricades, "assets/prefabs/deployable/door barricades/door_barricade_dbl_b.prefab");
                await SortThroughPossOfObject(possRoadSigns);
            }

            async Task SortThroughPossOfObject(Dictionary<Vector3, Vector3> dic, string strPrefab = null)
            {
                if (this == null || !isBuildingState) return;
                foreach (KeyValuePair<Vector3, Vector3> kvp in dic)
                {
                    if (this == null || !isBuildingState) return;
                    await Delay(delay);

                    Vector3 pos = GetGlobalPos(branch.transform, kvp.Key);
                    Quaternion rot = GetGlobalRot(branch.transform, kvp.Value);

                    SpawnObject(strPrefab != null ? strPrefab : GetRandomSignName(), pos, rot);
                }
            }

            void SpawnObject(string strPrefab, Vector3 pos, Quaternion rot)
            {
                BaseEntity entity = GameManager.server.CreateEntity(strPrefab, pos, rot) as BaseEntity;

                entity.enableSaving = false;

                GroundWatch groundWatch = entity.GetComponent<GroundWatch>();
                if (groundWatch != null) DestroyImmediate(groundWatch);

                DestroyOnGroundMissing destroyOnGroundMissing = entity.GetComponent<DestroyOnGroundMissing>();
                if (destroyOnGroundMissing != null) DestroyImmediate(destroyOnGroundMissing);

                entity.Spawn();

                if (entity is BuildingBlock)
                {
                    BuildingBlock block = entity as BuildingBlock;
                    block.ChangeGrade(BuildingGrade.Enum.Stone);
                    block.SetHealthToMax();
                }
                if (entity is StabilityEntity) (entity as StabilityEntity).grounded = true;
                if (entity is BaseCombatEntity) (entity as BaseCombatEntity).pickup.enabled = false;
                if (entity is NeonSign) entity.SetFlag(BaseEntity.Flags.Busy, true);

                objectsOfWalls.Add(entity);
                ownIns.allObjectsOfWalls.Add(entity.net.ID.Value);

                Effect.server.Run("assets/bundled/prefabs/fx/build/promote_stone.prefab", entity.transform.position);

                if (this == null || !isBuildingState)
                {
                    ownIns.allObjectsOfWalls.Remove(entity.net.ID.Value);
                    objectsOfWalls.Remove(entity);
                    entity.Kill();
                }
            }

            async Task Delay(int millisecondsDelay)
            {
                await Task.Delay(millisecondsDelay);
            }

            string GetRandomSignName()
            {
                int i = UnityEngine.Random.Range(0, 9);
                return strRoadSigns.ElementAt(i);
            }

            internal bool IsWagonOnMonument(TrainCar trainCar)
            {
                Vector3 pos = branch.transform.position;
                HashSet<TrainCar> trainCarsInside = GetEntities<TrainCar>(new Vector3(pos.x, pos.y, pos.z + 0.6f), 14f, -1);

                if (trainCarsInside.Contains(trainCar)) return true;
                else return false;
            }

            Wagon GetWagon(TrainCar trainCar)
            {
                return ownIns.allTrainCars[trainCar.net.ID.Value];
            }

            internal void Destroy()
            {
                DestroyWalls();
                button.Kill();
            }
        }

        internal class PointAnimationTransform { public Vector3 Pos; public Vector3 Rot; }

        internal class AnimationTransformVehicle : FacepunchBehaviour
        {
            internal BasePlayer whoTryMoveWagon { get; set; }
            internal CustomMonument customMonument { get; set; }
            private BaseEntity Main { get; set; } = null;

            private List<PointAnimationTransform> Path { get; } = new List<PointAnimationTransform>();

            private float SecondsTaken { get; set; } = 0f;
            private float SecondsToTake { get; set; } = 0f;
            private float WaypointDone { get; set; } = 0f;

            private Vector3 StartPos { get; set; } = Vector3.zero;
            private Vector3 EndPos { get; set; } = Vector3.zero;

            private Vector3 StartRot { get; set; } = Vector3.zero;
            private Vector3 EndRot { get; set; } = Vector3.zero;

            private float Speed { get; set; } = 0f;

            private void Awake()
            {
                Main = GetComponent<BaseEntity>();
                enabled = false;
            }

            internal void AddPath(PointAnimationTransform point, float speed)
            {
                Path.Add(point);
                Speed = speed;
                enabled = true;
            }

            private void FixedUpdate()
            {
                if (SecondsTaken == 0f)
                {
                    if (Path.Count == 0)
                    {
                        StartPos = EndPos = Vector3.zero;
                        StartRot = EndRot = Vector3.zero;
                        SecondsToTake = 0f;
                        SecondsTaken = 0f;
                        WaypointDone = 0f;
                        enabled = false;

                        UpdatePosTrainCar();
                        customMonument.TryEnabledBuildingState(whoTryMoveWagon);

                        return;
                    }
                    StartPos = transform.position;
                    StartRot = transform.rotation.eulerAngles;
                    if (Path[0].Pos != StartPos || Path[0].Rot != StartRot)
                    {
                        EndPos = Path[0].Pos != StartPos ? Path[0].Pos : StartPos;
                        EndRot = Path[0].Rot != StartRot ? Path[0].Rot : StartRot;
                        SecondsToTake = Vector3.Distance(EndPos, StartPos) / Speed;
                        SecondsTaken = 0f;
                        WaypointDone = 0f;
                    }
                    Path.RemoveAt(0);
                }
                if (StartPos != EndPos || StartRot != EndRot)
                {
                    SecondsTaken += UnityEngine.Time.deltaTime;
                    WaypointDone = Mathf.InverseLerp(0f, SecondsToTake, SecondsTaken);
                    if (StartPos != EndPos) transform.position = Vector3.Lerp(StartPos, EndPos, WaypointDone);
                    if (StartRot != EndRot) transform.rotation = Quaternion.Lerp(Quaternion.Euler(StartRot), Quaternion.Euler(EndRot), WaypointDone);
                    Main.TransformChanged();
                    Main.SendNetworkUpdate();
                    if (WaypointDone >= 1f) SecondsTaken = 0f;
                }
            }

            internal void UpdatePosTrainCar()
            {
                TrainCar trainCar = Main as TrainCar;
                if (TrainTrackSpline.TryFindTrackNear(trainCar.GetFrontWheelPos(), 15f, out TrainTrackSpline splineResult, out float distResult))
                {
                    trainCar.FrontWheelSplineDist = distResult;
                    Vector3 positionAndTangent = splineResult.GetPositionAndTangent(trainCar.FrontWheelSplineDist, transform.forward, out Vector3 tangent);
                    trainCar.SetTheRestFromFrontWheelData(ref splineResult, positionAndTangent, tangent, trainCar.localTrackSelection, null, instantMove: true);
                    trainCar.FrontTrackSection = splineResult;
                }
            }
        }

        #endregion Classes

        #region NTeleportation

        private void OnPlayerTeleported(BasePlayer player, Vector3 oldPos, Vector3 newPos)
        {
            CustomMonument monument;
            if (playersOnMonuments.TryGetValue(player, out monument)) monument.OnExitPlayer(player);
        }

        #endregion NTeleportation

        #region MoveItem
        private static void MoveItem(BasePlayer player, Item item)
        {
            ownIns.timer.In(1f, () =>
            {
                int spaceCountItem = GetSpaceCountItem(player, item.info.shortname, item.MaxStackable(), item.skin);
                int inventoryItemCount;
                if (spaceCountItem > item.amount) inventoryItemCount = item.amount;
                else inventoryItemCount = spaceCountItem;

                if (inventoryItemCount > 0)
                {
                    Item itemInventory = ItemManager.CreateByName(item.info.shortname, inventoryItemCount, item.skin);
                    if (item.skin != 0) itemInventory.name = item.name;

                    item.amount -= inventoryItemCount;
                    MoveInventoryItem(player, itemInventory);
                }

                if (item.amount > 0) MoveOutItem(player, item);
            });
        }

        private static int GetSpaceCountItem(BasePlayer player, string shortname, int stack, ulong skinID)
        {
            int slots = player.inventory.containerMain.capacity + player.inventory.containerBelt.capacity;
            int taken = player.inventory.containerMain.itemList.Count + player.inventory.containerBelt.itemList.Count;
            int result = (slots - taken) * stack;
            foreach (Item item in player.inventory.AllItems()) if (item.info.shortname == shortname && item.skin == skinID && item.amount < stack) result += stack - item.amount;
            return result;
        }

        private static void MoveInventoryItem(BasePlayer player, Item item)
        {
            if (item.amount <= item.MaxStackable())
            {
                foreach (Item itemInv in player.inventory.AllItems())
                {
                    if (itemInv.info.shortname == item.info.shortname && itemInv.skin == item.skin && itemInv.amount < itemInv.MaxStackable())
                    {
                        if (itemInv.amount + item.amount <= itemInv.MaxStackable())
                        {
                            itemInv.amount += item.amount;
                            itemInv.MarkDirty();
                            return;
                        }
                        else
                        {
                            item.amount -= itemInv.MaxStackable() - itemInv.amount;
                            itemInv.amount = itemInv.MaxStackable();
                        }
                    }
                }
                if (item.amount > 0) player.inventory.GiveItem(item);
            }
            else
            {
                while (item.amount > item.MaxStackable())
                {
                    Item thisItem = ItemManager.CreateByName(item.info.shortname, item.MaxStackable(), item.skin);
                    if (item.skin != 0) thisItem.name = item.name;
                    player.inventory.GiveItem(thisItem);
                    item.amount -= item.MaxStackable();
                }
                if (item.amount > 0) player.inventory.GiveItem(item);
            }
        }

        private static void MoveOutItem(BasePlayer player, Item item)
        {
            if (item.amount <= item.MaxStackable()) item.Drop(player.transform.position, Vector3.up);
            else
            {
                while (item.amount > item.MaxStackable())
                {
                    Item thisItem = ItemManager.CreateByName(item.info.shortname, item.MaxStackable(), item.skin);
                    if (item.skin != 0) thisItem.name = item.name;
                    thisItem.Drop(player.transform.position, Vector3.up);
                    item.amount -= item.MaxStackable();
                }
                if (item.amount > 0) item.Drop(player.transform.position, Vector3.up);
            }
        }
        #endregion MoveItem

        #region Commands

        [ConsoleCommand("clearallwagons")]
        private void ClearAllWagons(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null) return;
            int amount = wagons.Count;
            foreach (Wagon wagon in wagons.ToHashSet()) wagon.RemoveWagon();
            PrintWarning($"All custom wagons have been successfully removed! Count: {amount}");
        }

        [ConsoleCommand("givewagon")]
        private void GiveWagon(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length != 2 || arg.Player() != null)
            {
                Puts($"Incorrect syntax!");
                Puts("Example: givewagon 10 76561145679474806");
                return;
            }

            int amount = Convert.ToInt32(arg.Args[0]);
            string str = arg.Args[1].ToString();
            BasePlayer target = BasePlayer.Find(str);
            if (target == null)
            {
                Puts($"Player {str} not found!");
                return;
            }
            Puts($"The wagon was successfully given to the player!");
            GiveWagon(target, amount);
        }


        [ChatCommand("showfreewagons")]
        private void ShowFreeWagons(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionManager.freeWagons))
            {
                NotifyManager.SendMessageToPlayer(player, "NotHavePerm", ownIns._config.NotifyConfig.prefix);
                return;
            }

            ControllerSpawnsFreeWagon.ShowPosFreeWagons(player);
        }

        [ChatCommand("givewagon")]
        private void GiveWagonForAdmin(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionManager.giveWagon))
            {
                NotifyManager.SendMessageToPlayer(player, "NotHavePerm", ownIns._config.NotifyConfig.prefix);
                return;
            }

            if (args == null || args.Length == 0)
            {
                NotifyManager.SendMessageToPlayer(player, "InvalidCmdChat1", ownIns._config.NotifyConfig.prefix);
                return;
            }
            if (args == null || args.Length == 1)
            {
                NotifyManager.SendMessageToPlayer(player, "InvalidCmdChat2", ownIns._config.NotifyConfig.prefix);
                return;
            }

            int amount = Convert.ToInt32(args[0]);
            string str = args[1].ToString();
            BasePlayer target = BasePlayer.Find(str);
            if (target == null)
            {
                NotifyManager.SendMessageToPlayer(player, "InvalidCmdChat3", ownIns._config.NotifyConfig.prefix, str);
                return;
            }

            NotifyManager.SendMessageToPlayer(player, "ValidCmdChat", ownIns._config.NotifyConfig.prefix);
            GiveWagon(player, amount);
        }


        [ChatCommand("removewagon")]
        private void RemoveWagon(BasePlayer player)
        {
            playersWhoWantsToRemoveBaseWagon.Add(player);

            timer.In(5f, () =>
            {
                if (playersWhoWantsToRemoveBaseWagon.Contains(player)) playersWhoWantsToRemoveBaseWagon.Remove(player);
                NotifyManager.SendMessageToPlayer(player, "RemoveTimeEnd", ownIns._config.NotifyConfig.prefix);
            });
        }

        [ChatCommand("thinstruction")]
        private void Instruction(BasePlayer player, string message, string[] arg)
        {
            GUIManager.CreateGUI(player, GUIManager.GUITopic.Base);
        }

        #endregion Commands

        #region API

        private bool IsEntityFromBaseWagon(ulong netIdValue)
        {
            if (allBaseEntityByWagons.ContainsKey(netIdValue)) return true;
            else return false;
        }

        private bool IsBaseWagon(ulong netIdValue)
        {
            if (allBaseWagons.ContainsKey(netIdValue)) return true;
            else return false;
        }

        private bool IsTrainHomes(ulong netIdValue)
        {
            if (allTrainCars.ContainsKey(netIdValue)) return true;
            else return false;
        }

        private bool IsFreeWagon(ulong netIdValue)
        {
            foreach (TrainCar car in ControllerSpawnsFreeWagon.freeWagons)
            {
                if (car.net.ID.Value == netIdValue) return true;
            }
            return false;
        }

        #endregion API
    }
}

namespace Oxide.Plugins.TrainHomesExtensionMethods
{
    public static class ExtensionMethods
    {
        public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return true;
            return false;
        }

        public static HashSet<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            HashSet<TSource> result = new HashSet<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) result.Add(enumerator.Current);
            return result;
        }

        public static int GetCountVariable<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            int result = 0;
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) result++;
            return result;
        }

        public static List<TSource> WhereToList<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            List<TSource> result = new List<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) result.Add(enumerator.Current);
            return result;
        }

        public static TSource GetRandomToHashSet<TSource>(this HashSet<TSource> source)
        {
            int iRandom = UnityEngine.Random.Range(0, source.Count);
            int i = 0;

            foreach (TSource x in source)
            {
                if (i == iRandom) return x;
                i++;
            }
            return default(TSource);
        }

        public static TSource FirstOrDefault<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return enumerator.Current;
            return default(TSource);
        }

        public static HashSet<TResult> Select<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> predicate)
        {
            HashSet<TResult> result = new HashSet<TResult>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(predicate(enumerator.Current));
            return result;
        }

        public static List<TResult> Select<TSource, TResult>(this IList<TSource> source, Func<TSource, TResult> predicate)
        {
            List<TResult> result = new List<TResult>();
            for (int i = 0; i < source.Count; i++)
            {
                TSource element = source[i];
                result.Add(predicate(element));
            }
            return result;
        }

        public static TSource First<TSource>(this IList<TSource> source) => source[0];

        public static TSource Last<TSource>(this IList<TSource> source) => source[source.Count - 1];

        public static TSource Min<TSource>(this IEnumerable<TSource> source, Func<TSource, float> predicate)
        {
            TSource result = source.ElementAt(0);
            float resultValue = predicate(result);
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    TSource element = enumerator.Current;
                    float elementValue = predicate(element);
                    if (elementValue < resultValue)
                    {
                        result = element;
                        resultValue = elementValue;
                    }
                }
            }
            return result;
        }

        public static HashSet<T> OfType<T>(this IEnumerable<BaseNetworkable> source)
        {
            HashSet<T> result = new HashSet<T>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (enumerator.Current is T) result.Add((T)(object)enumerator.Current);
            return result;
        }

        public static List<TSource> ToList<TSource>(this IEnumerable<TSource> source)
        {
            List<TSource> result = new List<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(enumerator.Current);
            return result;
        }

        public static HashSet<TSource> ToHashSet<TSource>(this IEnumerable<TSource> source)
        {
            HashSet<TSource> result = new HashSet<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(enumerator.Current);
            return result;
        }

        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this Dictionary<TKey, TValue> source)
        {
            Dictionary<TKey, TValue> result = new Dictionary<TKey, TValue>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(enumerator.Current.Key, enumerator.Current.Value);
            return result;
        }

        public static List<TSource> OrderBy<TSource>(this IEnumerable<TSource> source, Func<TSource, float> predicate)
        {
            List<TSource> result = source.ToList();
            for (int i = 0; i < result.Count; i++)
            {
                for (int j = 0; j < result.Count - 1; j++)
                {
                    if (predicate(result[j]) > predicate(result[j + 1]))
                    {
                        TSource z = result[j];
                        result[j] = result[j + 1];
                        result[j + 1] = z;
                    }
                }
            }
            return result;
        }

        public static TSource Max<TSource>(this IEnumerable<TSource> source, Func<TSource, float> predicate)
        {
            TSource result = source.ElementAt(0);
            float resultValue = predicate(result);
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    TSource element = enumerator.Current;
                    float elementValue = predicate(element);
                    if (elementValue > resultValue)
                    {
                        result = element;
                        resultValue = elementValue;
                    }
                }
            }
            return result;
        }

        public static TSource ElementAt<TSource>(this IEnumerable<TSource> source, int index)
        {
            int movements = 0;
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (movements == index) return enumerator.Current;
                    movements++;
                }
            }
            return default(TSource);
        }

        public static bool IsPlayer(this BasePlayer player) => player != null && player.userID.IsSteamId();

        public static bool IsExists(this BaseNetworkable entity) => entity != null && !entity.IsDestroyed;

        public static void ClearItemsContainer(this ItemContainer container)
        {
            for (int i = container.itemList.Count - 1; i >= 0; i--)
            {
                Item item = container.itemList[i];
                item.RemoveFromContainer();
                item.Remove();
            }
        }
    }
}