using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Rust;
using System.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Oxide.Core;
using Rust.Ai;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("EasyGather", "Qbis", "1.0.4")]
    public class EasyGather : RustPlugin
    {
        #region [Vars]
        [PluginReference]
        private Plugin ZoneManager, CopyPaste, ImageLibrary;

        private static EasyGather plugin;
        private PlaceManager placeManager = null;
        private GatherManager gatherManager = null;
        private GameObject gatherGameObject = null;
        private Dictionary<string, List<SpawnData>> spawnData = new Dictionary<string, List<SpawnData>>();
        private Dictionary<ulong, PlayerData> playerData = new Dictionary<ulong, PlayerData>();
        private List<TextData> textData = new List<TextData>();
        private Vector3 BuildingSpawnPoint = new Vector3(1562f, 500f, 2161f);
        private string zoneID = "994422";
        private DefaultSpawns Spawns;
        private AddText addText = new AddText();


        #region [Classes]
        public class PlayerData
        {
            public int gatherTime;
            public int lastTeleport;
            public Vector3 lastPosition;
            public int topPoints;
            public Dictionary<string, int> topAmount = new Dictionary<string, int>();
        }

        public class DefaultSpawns
        {
            public Vector3 TimerTextPosition;
            public Vector3 PlayerSpawnPosition;
        }

        public class SpawnData
        {
            public int spawnID;
            public Vector3 position;
            public Vector3 rotation;
        }

        public class TextData
        {
            public Vector3 position;
            public float dist;
            public string text;
        }

        public class AddText
        {
            public string text;
            public string dist;
            public string size;
        }

        public class SpawnSettings
        {
            public string langName;
            public string category;
        }
        #endregion

        private Dictionary<string, SpawnSettings> spawnSettings = new Dictionary<string, SpawnSettings>()
        {
            ["assets/bundled/prefabs/autospawn/resource/ores/stone-ore.prefab"] = new SpawnSettings() { category = "Ore", langName = "Stone_Ore" },
            ["assets/bundled/prefabs/autospawn/resource/ores/metal-ore.prefab"] = new SpawnSettings() { category = "Ore", langName = "Metal_Ore" },
            ["assets/bundled/prefabs/autospawn/resource/ores/sulfur-ore.prefab"] = new SpawnSettings() { category = "Ore", langName = "Sulfur_Ore" },
            ["assets/rust.ai/agents/stag/stag.prefab"] = new SpawnSettings() { category = "Animal", langName = "Stag" },
            ["assets/rust.ai/agents/bear/bear.prefab"] = new SpawnSettings() { category = "Animal", langName = "Bear" },
            ["assets/rust.ai/agents/boar/boar.prefab"] = new SpawnSettings() { category = "Animal", langName = "Boar" },
            ["assets/rust.ai/agents/wolf/wolf.prefab"] = new SpawnSettings() { category = "Animal", langName = "Wolf" },
            ["assets/rust.ai/agents/chicken/chicken.prefab"] = new SpawnSettings() { category = "Animal", langName = "Chicken" },
            ["assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab"] = new SpawnSettings() { category = "Barrel", langName = "Barrel_1" },
            ["assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab"] = new SpawnSettings() { category = "Barrel", langName = "Barrel_2" },
            ["assets/bundled/prefabs/radtown/loot_barrel_1.prefab"] = new SpawnSettings() { category = "Barrel", langName = "Barrel_3" },
            ["assets/bundled/prefabs/radtown/loot_barrel_2.prefab"] = new SpawnSettings() { category = "Barrel", langName = "Barrel_4" },
            ["assets/bundled/prefabs/radtown/oil_barrel.prefab"] = new SpawnSettings() { category = "Barrel", langName = "Barrel_Oil" },
            ["assets/bundled/prefabs/radtown/crate_elite.prefab"] = new SpawnSettings() { category = "Crate", langName = "Crate_Elite" },
            ["assets/bundled/prefabs/radtown/crate_normal.prefab"] = new SpawnSettings() { category = "Crate", langName = "Crate_Normal" },
            ["assets/bundled/prefabs/radtown/crate_normal_2.prefab"] = new SpawnSettings() { category = "Crate", langName = "Crate_Normal_2" },
            ["assets/bundled/prefabs/radtown/crate_tools.prefab"] = new SpawnSettings() { category = "Crate", langName = "Crate_Tools" },
            ["assets/bundled/prefabs/radtown/underwater_labs/crate_ammunition.prefab"] = new SpawnSettings() { category = "Crate", langName = "Crate_Ammun_u" },
            ["assets/bundled/prefabs/radtown/underwater_labs/crate_tools.prefab"] = new SpawnSettings() { category = "Crate", langName = "Crate_Tools_u" },
            ["assets/bundled/prefabs/radtown/underwater_labs/crate_elite.prefab"] = new SpawnSettings() { category = "Crate", langName = "Crate_Elite_u" },
            ["assets/bundled/prefabs/radtown/underwater_labs/crate_normal.prefab"] = new SpawnSettings() { category = "Crate", langName = "Crate_Normal_u" },
            ["assets/bundled/prefabs/radtown/underwater_labs/crate_normal_2.prefab"] = new SpawnSettings() { category = "Crate", langName = "Crate_Noram_2_u" }
        };
        #endregion

        #region [Config]
        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            config = PluginConfig.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();

            if (config.PluginVersion < Version)
                UpdateConfigValues();

            Config.WriteObject(config, true);
        }

        private void UpdateConfigValues()
        {
            PluginConfig baseConfig = PluginConfig.DefaultConfig();
            if (config.PluginVersion < Version)
            {
                PrintWarning("Plugin update detected! Checking config values...");
                if (Version == new VersionNumber(1, 0, 3))
                {
                    config.commands = new List<string>()
                    {
                        "kit"
                    };

                    config.main.removeDroppedItems = 10;
                }

                if(Version == new VersionNumber(1, 0, 4))
                {
                    config.main.penaltyType = 1;
                }


                config.PluginVersion = Version;
                PrintWarning("Config checked completed!");
            }
            config.PluginVersion = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private class PluginConfig
        {
            [JsonProperty("Общие настройки")]
            public MainSettings main;

            [JsonProperty("Настройки тикетов")]
            public List<TicketSettings> tickets;

            [JsonProperty("Настройки респавна объектов (в секундах)")]
            public Dictionary<string, int> spawn;

            [JsonProperty("Настройка очков для топа")]
            public Dictionary<string, int> top;

            [JsonProperty("Запрещеные предметы (shortName)")]
            public List<string> blockedItems;

            [JsonProperty("Запрещенные комманды")]
            public List<string> commands;

            [JsonProperty("Config version")]
            public VersionNumber PluginVersion = new VersionNumber();

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    main = new MainSettings()
                    {
                        fileName = "eg_room_v1",
                        imgUrl = "https://i.imgur.com/k9HzQLM.jpg",
                        zoneSize = new Vector3(150f, 150f, 150f),
                        maxPlayers = 10,
                        removeCrateAfterLooting = 4,
                        teleportCoolDown = 600,
                        isTopActive = true,
                        autoWipe = true,
                        shortName = "glue",
                        OnlyOne = true,
                        removeDroppedItems = 10,
                        penaltyType = 1
                    },
                    tickets = new List<TicketSettings>
                    {
                        new TicketSettings()
                        {
                            skinID = 2852472024,
                            customName = "Ticket 60",
                            imageUrl = "https://i.imgur.com/BHUj7e3.png",
                            time = 60,
                            dropCrates = new Dictionary<string, int>()
                            {
                                ["crate_elite"] = 30,
                                ["crate_normal"] = 10,
                            }
                        },
                        new TicketSettings()
                        {
                            skinID = 2852471535,
                            customName = "Ticket 30",
                            imageUrl = "https://i.imgur.com/VusjY8D.png",
                            time = 30,
                            dropCrates = new Dictionary<string, int>()
                            {
                                ["crate_normal"] = 40
                            }
                        },
                        new TicketSettings()
                        {
                            skinID = 2852471429,
                            customName = "Ticket 10",
                            imageUrl = "https://i.imgur.com/A1dN4N0.png",
                            time = 10,
                            dropCrates = new Dictionary<string, int>()
                            {
                                ["crate_normal_2"] = 20,
                                ["crate_tools"] = 20
                            }
                        },
                    },
                    spawn = new Dictionary<string, int>()
                    {
                        ["assets/bundled/prefabs/autospawn/resource/ores/stone-ore.prefab"] = 120,
                        ["assets/bundled/prefabs/autospawn/resource/ores/metal-ore.prefab"] = 240,
                        ["assets/bundled/prefabs/autospawn/resource/ores/sulfur-ore.prefab"] = 360,
                        ["assets/rust.ai/agents/stag/stag.prefab"] = 120,
                        ["assets/rust.ai/agents/bear/bear.prefab"] = 360,
                        ["assets/rust.ai/agents/boar/boar.prefab"] = 360,
                        ["assets/rust.ai/agents/wolf/wolf.prefab"] = 240,
                        ["assets/rust.ai/agents/chicken/chicken.prefab"] = 60,
                        ["assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab"] = 120,
                        ["assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab"] = 160,
                        ["assets/bundled/prefabs/radtown/loot_barrel_1.prefab"] = 120,
                        ["assets/bundled/prefabs/radtown/loot_barrel_2.prefab"] = 160,
                        ["assets/bundled/prefabs/radtown/oil_barrel.prefab"] = 140,
                        ["assets/bundled/prefabs/radtown/crate_elite.prefab"] = 480,
                        ["assets/bundled/prefabs/radtown/crate_normal.prefab"] = 360,
                        ["assets/bundled/prefabs/radtown/crate_normal_2.prefab"] = 360,
                        ["assets/bundled/prefabs/radtown/crate_tools.prefab"] = 240,
                        ["assets/bundled/prefabs/radtown/underwater_labs/crate_ammunition.prefab"] = 240,
                        ["assets/bundled/prefabs/radtown/underwater_labs/crate_tools.prefab"] = 240,
                        ["assets/bundled/prefabs/radtown/underwater_labs/crate_elite.prefab"] = 480,
                        ["assets/bundled/prefabs/radtown/underwater_labs/crate_normal.prefab"] = 360,
                        ["assets/bundled/prefabs/radtown/underwater_labs/crate_normal_2.prefab"] = 240

                    },
                    top = new Dictionary<string, int>()
                    {
                        ["assets/bundled/prefabs/autospawn/resource/ores/stone-ore.prefab"] = 1,
                        ["assets/bundled/prefabs/autospawn/resource/ores/metal-ore.prefab"] = 1,
                        ["assets/bundled/prefabs/autospawn/resource/ores/sulfur-ore.prefab"] = 1,
                        ["assets/rust.ai/agents/stag/stag.prefab"] = 1,
                        ["assets/rust.ai/agents/bear/bear.prefab"] = 1,
                        ["assets/rust.ai/agents/boar/boar.prefab"] = 1,
                        ["assets/rust.ai/agents/wolf/wolf.prefab"] = 1,
                        ["assets/rust.ai/agents/chicken/chicken.prefab"] = 1,
                        ["assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab"] = 1,
                        ["assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab"] = 1,
                        ["assets/bundled/prefabs/radtown/loot_barrel_1.prefab"] = 1,
                        ["assets/bundled/prefabs/radtown/loot_barrel_2.prefab"] = 1,
                        ["assets/bundled/prefabs/radtown/oil_barrel.prefab"] = 1,
                        ["assets/bundled/prefabs/radtown/crate_elite.prefab"] = 4,
                        ["assets/bundled/prefabs/radtown/crate_normal.prefab"] = 3,
                        ["assets/bundled/prefabs/radtown/crate_normal_2.prefab"] = 2,
                        ["assets/bundled/prefabs/radtown/crate_tools.prefab"] = 2,
                        ["assets/bundled/prefabs/radtown/underwater_labs/crate_ammunition.prefab"] = 2,
                        ["assets/bundled/prefabs/radtown/underwater_labs/crate_tools.prefab"] = 2,
                        ["assets/bundled/prefabs/radtown/underwater_labs/crate_elite.prefab"] = 4,
                        ["assets/bundled/prefabs/radtown/underwater_labs/crate_normal.prefab"] = 3,
                        ["assets/bundled/prefabs/radtown/underwater_labs/crate_normal_2.prefab"] = 2

                    },
                    blockedItems = new List<string>()
                    {
                        "syringe.medical",
                        "antiradpills"
                    },
                    commands = new List<string>()
                    {
                        "kit"
                    },
                    PluginVersion = new VersionNumber()

                };
            }
        }

        public class MainSettings
        {
            [JsonProperty("Через сколько секунд удалять залутаный ящик")]
            public int removeCrateAfterLooting;

            [JsonProperty("Откат на телепорт")]
            public int teleportCoolDown;

            [JsonProperty("Максимальное количество добывающих")]
            public int maxPlayers;

            [JsonProperty("Включить топ?")]
            public bool isTopActive;

            [JsonProperty("Авто очистка данных игроков вайпе (Время игроков и Топ)")]
            public bool autoWipe;

            [JsonProperty("Название файла (copypaste)")]
            public string fileName;

            [JsonProperty("Фото для пользовательского интерфейса")]
            public string imgUrl;

            [JsonProperty("Размер зоны")]
            public Vector3 zoneSize;

            [JsonProperty("Shortname для тикетов")]
            public string shortName;

            [JsonProperty("Только 1 тикет в ящике")]
            public bool OnlyOne;

            [JsonProperty("Через сколько секунд удалять выброщенные предметы")]
            public int removeDroppedItems;

            [JsonProperty("Наказание за нахождение в зоне после окончания времени фарма (1 - радиация + урон, 2 - мгновенная смерть)")]
            public int penaltyType;
        }

        public class TicketSettings
        {
            [JsonProperty("Скин айди тикета")]
            public ulong skinID;

            [JsonProperty("Имя тикета")]
            public string customName;

            [JsonProperty("Ссылка на картинку")]
            public string imageUrl;

            [JsonProperty("Сколько тикет дает времени на фарм (в секундах)")]
            public int time;

            [JsonProperty("Ящики из которых может выпасть тикет с шансом")]
            public Dictionary<string, int> dropCrates;
        }
        #endregion

        #region [Localization⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠]
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Text_Succes"] = "Text added successfully",
                ["Stone_Ore"] = "Stone Ore",
                ["Metal_Ore"] = "Metal Ore",
                ["Sulfur_Ore"] = "Sulfur Ore",
                ["Stag"] = "Stag",
                ["Bear"] = "Bear",
                ["Boar"] = "Boar",
                ["Wolf"] = "Wolf",
                ["Chicken"] = "Chicken",
                ["Barrel_1"] = "Barrel 1 (Road)",
                ["Barrel_2"] = "Barrel 2 (Road)",
                ["Barrel_3"] = "Barrel 1 (RT)",
                ["Barrel_4"] = "Barrel 2 (RT)",
                ["Barrel_Oil"] = "Oil Barrel",
                ["Crate_Elite"] = "Crate Elite",
                ["Crate_Normal"] = "Crate Millitary",
                ["Crate_Normal_2"] = "Crate Basic",
                ["Crate_Tools"] = "Crate Tools",
                ["Crate_Ammun_u"] = "Crate Ammunition (Labs)",
                ["Crate_Tools_u"] = "Crate Tools (Labs)",
                ["Crate_Elite_u"] = "Crate Elite (Labs)",
                ["Crate_Normal_u"] = "Crate Millitary (Labs)",
                ["Crate_Noram_2_u"] = "Crate Basic (Labs)",
                ["TimerText1"] = "Time for gathering\n{0}h. {1}m. {2}s.",
                ["TimerText2"] = "<color=red>You don't have gather time\nLeave this location, otherwise you will die!</color>",
                ["UI_Admin_Header"] = "EG Admin Panel | Building {0} | {1}",
                ["UI_Admin_Main"] = "Main",
                ["UI_Admin_Spawns"] = "Loot Spawns",
                ["UI_Admin_AddSpawn"] = "Add Spawns",
                ["UI_Admin_EditSpawn"] = "Edit Spawns",
                ["UI_Admin_RefreshSpawn"] = "Refresh Spawns",
                ["UI_Admin_RemoveSpawn"] = "Remove All Spawns",
                ["UI_Admin_Texts"] = "3D Texts",
                ["UI_Admin_AddText"] = "Add Text",
                ["UI_Admin_EditText"] = "All Texts",
                ["UI_Admin_SetTimerText"] = "Set Timer Text (Your position)",
                ["UI_Admin_RemoveText"] = "Remove All Texts",
                ["UI_Admin_Settings"] = "Settings",
                ["UI_Admin_Name"] = "Name",
                ["UI_Admin_RespawnTime"] = "Respawn Time (seconds)",
                ["UI_Admin_RemoveSpawnID"] = "Remove Spawn (ID)",
                ["UI_Admin_ShowSpawn"] = "Show spawns",
                ["UI_Admin_WriteText"] = "Write the text",
                ["UI_Admin_Size"] = "Size",
                ["UI_Admin_Radius"] = "Show Radius",
                ["UI_Admin_SettingsChangeName"] = "Change building name..",
                ["UI_Admin_SettingsSetSpawn"] = "Player Spawn (Your position)",
                ["UI_Admin_SettingsFullUpdate"] = "Full Building Update",
                ["UI_Admin_Ores"] = "Ores",
                ["UI_Admin_Animals"] = "Animals",
                ["UI_Admin_Crates"] = "Crates",
                ["UI_Admin_Barrels"] = "Barrels",
                ["UI_Admin_Teleport"] = "Teleport",
                ["UI_Admin_FreeTime"] = "Free Time",
                ["UI_Admin_Notice_RemoveSpawn"] = "Removed all spawns successfully",
                ["UI_Admin_Notice_RemoveSpawn_2"] = "Removed spawns successfully",
                ["UI_Admin_Notice_RefreshSpawn"] = "Refreshed all spawns successfully",
                ["UI_Admin_Notice_SetTimerText"] = "Set timer position successfully",
                ["UI_Admin_Notice_RemoveText"] = "Removed all texts successfully",
                ["UI_Admin_Notice_SettingsSetSpawn"] = "Set players spawn position successfully ",
                ["UI_Admin_Notice_SettingsFullUpdate"] = "Update Building successfully",
                ["UI_Admin_Notice_AlreadyUse"] = "Someone alreday use it",
                ["UI_Admin_Incorrect_ID"] = "Incorrect ID",
                ["UI_Admin_Notice_OnlyChar"] = "ID can be only numbers",
                ["UI_Admin_Notice_Remove_NotFound"] = "ID not found",
                ["UI_Admin_Notice_Remove_Suc"] = "Spawn removed successfully",
                ["UI_Admin_Notice_FreeTime"] = "You got free 1 hour",
                ["UI_Player_Time"] = "{0}h {1}m {2}s",
                ["UI_Player_Have"] = "You have : {0}",
                ["UI_Player_Change"] = "Change",
                ["UI_Player_Leave"] = "Leave",
                ["UI_Player_Teleport"] = "Teleport",
                ["UI_Player_Players"] = "Players {0} | {1}",
                ["UI_Player_FarmTime"] = "You have : {0}h {1}m {2}s",
                ["UI_Player_TopNotActive"] = "Top is off",
                ["UI_Player_Notice_Already"] = "You are already teleported",
                ["UI_Player_Notice_NoTime"] = "You haven't time",
                ["UI_Player_Notice_BlockedItem"] = "You have blocked item to teleport\n<color=red>{0}</color>",
                ["UI_Player_Notice_CoolDown"] = "You can teleport again after\n{0}h {1}m {2}s",
                ["UI_Player_Notice_NotChanged"] = "<color=red>You haven't this ticket</color>",
                ["UI_Player_Notice_NoGather"] = "You are not in gather room!",
                ["UI_Player_Notice_NoReturnPos"] = "You haven't return position :("
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Text_Succes"] = "Текст добавлен успешно",
                ["Stone_Ore"] = "Каменная руда",
                ["Metal_Ore"] = "Металлическая руда",
                ["Sulfur_Ore"] = "Серная руда",
                ["Stag"] = "Олень",
                ["Bear"] = "Медведь",
                ["Boar"] = "Кабан",
                ["Wolf"] = "Волк",
                ["Chicken"] = "Курица",
                ["Barrel_1"] = "Бочка 1 (Дорога)",
                ["Barrel_2"] = "Бочка 2 (Дорога)",
                ["Barrel_3"] = "Бочка 1 (РТ)",
                ["Barrel_4"] = "Бочка 2 (РТ)",
                ["Barrel_Oil"] = "Бочка с топливом",
                ["Crate_Elite"] = "Элитный ящик",
                ["Crate_Normal"] = "Военный яшик",
                ["Crate_Normal_2"] = "Обычный ящик",
                ["Crate_Tools"] = "Ящик с инструментами",
                ["Crate_Ammun_u"] = "Ящик с амуницией (Лаба)",
                ["Crate_Tools_u"] = "Ящик с инструментами (Лаба)",
                ["Crate_Elite_u"] = "Элитный ящик (Лаба)",
                ["Crate_Normal_u"] = "Военный ящик (Лаба)",
                ["Crate_Noram_2_u"] = "Обычный ящик (Лаба)",
                ["TimerText1"] = "У вас осталось\n{0}ч. {1}м. {2}с.",
                ["TimerText2"] = "<color=red>У вас нет времени на добычу!\nПокиньте ее или вы умрете!</color>",
                ["UI_Admin_Header"] = "EG Админ панель | Строение {0} | {1}",
                ["UI_Admin_Main"] = "Главная",
                ["UI_Admin_Spawns"] = "Спавны лута",
                ["UI_Admin_AddSpawn"] = "Добавить спавны",
                ["UI_Admin_EditSpawn"] = "Редактировать спавны",
                ["UI_Admin_RefreshSpawn"] = "Обновить спавны",
                ["UI_Admin_RemoveSpawn"] = "Удалить все спавны",
                ["UI_Admin_Texts"] = "3D Тексты",
                ["UI_Admin_AddText"] = "Добавить текст",
                ["UI_Admin_EditText"] = "Все тексты",
                ["UI_Admin_SetTimerText"] = "Уст. таймер (Ваша позиция)",
                ["UI_Admin_RemoveText"] = "Удалить все тексты",
                ["UI_Admin_Settings"] = "Настройки",
                ["UI_Admin_Name"] = "Имя",
                ["UI_Admin_WriteText"] = "Напишите текст",
                ["UI_Admin_Size"] = "Размер",
                ["UI_Admin_Radius"] = "Радиус показа",
                ["UI_Admin_RespawnTime"] = "Время спавна (в секундах)",
                ["UI_Admin_RemoveSpawnID"] = "Удалить спавн (ID)",
                ["UI_Admin_ShowSpawn"] = "Показать спавны",
                ["UI_Admin_SettingsChangeName"] = "Сменить имя постройки...",
                ["UI_Admin_SettingsSetSpawn"] = "Спавн (Ваша позиция)",
                ["UI_Admin_SettingsFullUpdate"] = "Полное обновление постр.",
                ["UI_Admin_Ores"] = "Руды",
                ["UI_Admin_Animals"] = "Животные",
                ["UI_Admin_Crates"] = "Ящики",
                ["UI_Admin_Barrels"] = "Бочки",
                ["UI_Admin_Teleport"] = "Телепорт",
                ["UI_Admin_FreeTime"] = "Взять время",
                ["UI_Admin_Notice_RemoveSpawn"] = "Все спавны лута удалены",
                ["UI_Admin_Notice_RemoveSpawn_2"] = "Все спавны удалены",
                ["UI_Admin_Notice_RefreshSpawn"] = "Все спавны лута обновлены",
                ["UI_Admin_Notice_SetTimerText"] = "Новая точка для таймера установлена",
                ["UI_Admin_Notice_RemoveText"] = "Все тексты удалены",
                ["UI_Admin_Notice_SettingsSetSpawn"] = "Новая точка спавна игроков установлена",
                ["UI_Admin_Notice_SettingsFullUpdate"] = "Вы полностью обновили постройку",
                ["UI_Admin_Notice_AlreadyUse"] = "Кто-то уже раставляет спавны!",
                ["UI_Admin_Incorrect_ID"] = "Неверный ID",
                ["UI_Admin_Notice_OnlyChar"] = "В ID могут быть только цифры",
                ["UI_Admin_Notice_Remove_NotFound"] = "ID не найден",
                ["UI_Admin_Notice_Remove_Suc"] = "Спавн успешно удален",
                ["UI_Admin_Notice_FreeTime"] = "Вы получили 1 бесплатный час",
                ["UI_Player_Time"] = "{0}ч {1}м {2}с",
                ["UI_Player_Have"] = "У тебя есть : {0}",
                ["UI_Player_Change"] = "Поменять",
                ["UI_Player_Leave"] = "Покинуть",
                ["UI_Player_Teleport"] = "Войти",
                ["UI_Player_Players"] = "Игроков {0} | {1}",
                ["UI_Player_FarmTime"] = "Фарм время : {0}ч {1}м {2}с",
                ["UI_Player_TopNotActive"] = "Топ выключен",
                ["UI_Player_Notice_Already"] = "Вы уже телепортированы",
                ["UI_Player_Notice_NoTime"] = "У Вас нет фарм времени",
                ["UI_Player_Notice_BlockedItem"] = "Нельзя телепортироваться с \n<color=red>{0}</color>",
                ["UI_Player_Notice_CoolDown"] = "Телепорт будет доступен через\n{0}ч {1}м {2}с",
                ["UI_Player_Notice_NotChanged"] = "<color=red>У вас нет этого тикета</color>",
                ["UI_Player_Notice_NoGather"] = "Вы не в комнате добычи",
                ["UI_Player_Notice_NoReturnPos"] = "К сожалению у Вас нет точки возвращения :("
            }, this, "ru");
        }
        string GetMsg(string key, BasePlayer player = null) => lang.GetMessage(key, this, player?.UserIDString);
        string GetMsg(string key) => lang.GetMessage(key, this);
        #endregion

        #region [Oxide Hooks]
        void OnEntityEnterZone(string ZoneID, BaseEntity entity)
        {
            if (ZoneID != zoneID)
                return;


            if (!(entity is ItemPickup) && entity is DroppedItem)
            {
                DroppedItem item = entity as DroppedItem;

                if (item != null && !item.IsDestroyed)
                {
                    item.CancelInvoke(item.IdleDestroy);
                    item.Invoke(item.IdleDestroy, config.main.removeDroppedItems);
                }
            }
        }

        private object OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null || string.IsNullOrEmpty(command))
                return null;

            if (!config.commands.Contains(command))
                return null;

            if (gatherManager.players.Contains(player))
                return false;

            return null;
        }

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg == null)
                return null;

            var connection = arg.Connection;
            if (connection == null || string.IsNullOrEmpty(arg.cmd.Name))
                return null;

            var player = arg.Player();
            if (player == null)
                return null;

            if (!config.commands.Contains(arg.cmd.Name.ToLower()))
                return null;

            if(gatherManager.players.Contains(player))
                return false;


            return null;
        }

        private void Init()
        {
            plugin = this;
            AddCovalenceCommand("gtic", "GiveTicket", "easygather.give");
        }

        private void Unload()
        {
            config = null;
            SavePlayerData();

            UnityEngine.Object.Destroy(placeManager);
            UnityEngine.Object.Destroy(gatherManager);
            UnityEngine.Object.Destroy(gatherGameObject);
        }
        private void OnServerInitialized()
        {
            LoadSpawnData();
            LoadTextData();
            LoadPlayerData();
            UpdateOrSpawnRoom();
            Unsubscribe(nameof(CanLootEntity));

            ImageLibrary?.Call("AddImage", config.main.imgUrl, "mainImage");

            foreach (var ticket in config.tickets)
            {
                ImageLibrary?.Call("AddImage", ticket.imageUrl, ticket.skinID.ToString());
            }
        }

        private void OnNewSave()
        {
            if (config.main.autoWipe)
            {
                playerData.Clear();
                SavePlayerData();
            }
        }

        private void OnServerSave()
        {
            SavePlayerData();
        }

        private object CanLootEntity(BasePlayer player, LootContainer container)
        {
            if (placeManager != null && placeManager.Player == player)
                return false;

            return null;
        }

        private void OnEntityDeath(BaseEntity ent, HitInfo info)
        {
            if (ent == null) return;
            if (gatherManager == null) return;
            if (!gatherManager.loot.ContainsKey(ent)) return;
            gatherManager.UpdateSpawn(ent);

            if (info == null || info.Initiator.ToPlayer() == null) return;
            var player = info.Initiator.ToPlayer();

            if (playerData.ContainsKey(player.userID))
            {
                if (playerData[player.userID].gatherTime >= 0)
                {
                    if (!config.main.isTopActive)
                        return;

                    if (!config.top.ContainsKey(ent.PrefabName))
                        return;

                    if (!playerData[player.userID].topAmount.ContainsKey(ent.PrefabName))
                        playerData[player.userID].topAmount.Add(ent.PrefabName, 1);
                    else
                        playerData[player.userID].topAmount[ent.PrefabName]++;

                    playerData[player.userID].topPoints += config.top[ent.PrefabName];
                    return;
                }
                else
                {

                    player.metabolism.radiation_poison.value += 100;
                    player.health -= 50;
                }
            }
            else
            {
                player.metabolism.radiation_poison.value += 100;
                player.health -= 50;
            }
        }

        private void OnLootEntityEnd(BasePlayer player, LootContainer ent)
        {
            if (ent == null) return;
            if (gatherManager == null) return;
            if (!gatherManager.loot.ContainsKey(ent)) return;
            gatherManager.UpdateSpawn(ent);
            timer.Once(config.main.removeCrateAfterLooting, () => { if (ent != null) ent.Kill(); });

            if (playerData.ContainsKey(player.userID))
            {
                if (playerData[player.userID].gatherTime >= 0)
                    return;

                if (playerData[player.userID].gatherTime >= 0)
                {
                    if (!config.main.isTopActive)
                        return;

                    if (!config.top.ContainsKey(ent.PrefabName))
                        return;

                    if (!playerData[player.userID].topAmount.ContainsKey(ent.PrefabName))
                        playerData[player.userID].topAmount.Add(ent.PrefabName, 1);
                    else
                        playerData[player.userID].topAmount[ent.PrefabName]++;

                    playerData[player.userID].topPoints += config.top[ent.PrefabName];
                    return;
                }
                else
                {

                    player.metabolism.radiation_poison.value += 100;
                    player.health -= 50;
                }
            }
            else
            {
                player.metabolism.radiation_poison.value += 100;
                player.health -= 50;
            }
        }

        private object OnLootSpawn(LootContainer container)
        {
            if (container == null || container.inventory == null)
                return null;

            if (gatherManager == null)
                return null;

            if (gatherManager.loot.ContainsKey(container))
                return null;

            NextTick(() =>
            {

               if (container == null)
                   return;

               var tickets = config.tickets.Where(b => b.dropCrates.ContainsKey(container.ShortPrefabName)).ToList();
               
               foreach (var ticket in tickets)
               {

                   var rnd = UnityEngine.Random.Range(1, 101);
                    if (!ticket.dropCrates.ContainsKey(container.ShortPrefabName))
                        continue;

                   if (rnd > ticket.dropCrates[container.ShortPrefabName])
                       continue;

                    container.inventory.capacity++;

                   var item = ItemManager.CreateByName(config.main.shortName, 1, ticket.skinID);
                   item.name = ticket.customName;
                   item.MoveToContainer(container.inventory);

                   if (config.main.OnlyOne)
                       return;
               }
           });


            return null;
        }
        #endregion

        #region [Function]
        private void UpdateOrSpawnRoom(bool foolUpdate = false)
        {
            Vector3 zoneLoc = (Vector3)ZoneManager?.Call("GetZoneLocation", zoneID);
            if (zoneLoc != BuildingSpawnPoint)
            {
                string[] zoneOptions = new string[] { "size", $"{config.main.zoneSize.x} {config.main.zoneSize.y} {config.main.zoneSize.z}", "nobuild", "true", "nodecay", "true", "noentitypickup", "true", "undestr", "true", "nosignupdates", "true", "notp", "true", "pvpgod", "true" };
                ZoneManager?.Call("CreateOrUpdateZone", zoneID, zoneOptions, BuildingSpawnPoint);
            }

            List<BaseEntity> ents = new List<BaseEntity>();
            Vis.Entities(BuildingSpawnPoint, 150f, ents);
            bool needRemove = false;
            string[] pasteOptions = new string[]{ "autoheight", "false", "stability", "false", "auth", "false", "entityowner", "false" };

            if (ents.Count > 0 && foolUpdate)
            {
                foreach (var ent in ents)
                    if (ent != null && !(ent is BasePlayer))
                        ent.Kill();

                LoadSpawnData();
                LoadTextData();

                timer.Once(5f, () =>
                {
                    CopyPaste?.Call("TryPasteFromVector3", BuildingSpawnPoint, 0f, config.main.fileName, pasteOptions);
                });
            }
            else if (ents.Count <= 0)
            {
                CopyPaste?.Call("TryPasteFromVector3", BuildingSpawnPoint, 0f, config.main.fileName, pasteOptions);
                needRemove = true;
            }
            else
            {
                needRemove = true;
            }

            if(gatherManager != null || gatherGameObject != null)
            {
                UnityEngine.Object.Destroy(gatherManager);
                UnityEngine.Object.Destroy(gatherGameObject);
            }
            GameObject obj = new GameObject();
            gatherManager = obj.AddComponent<GatherManager>();
            UpdateAllSpawns(needRemove);
        }

        private void UpdateAllSpawns(bool needRemoveEnts = true)
        {
            if (needRemoveEnts)
            {
                List<BaseEntity> ents = new List<BaseEntity>();
                Vis.Entities(BuildingSpawnPoint, 150f, ents);

                foreach (var ent in ents)
                {
                    if (ent != null)
                        if (spawnSettings.ContainsKey(ent.PrefabName))
                            ent.Kill();
                }
            }

            gatherManager.spawns.Clear();
            gatherManager.loot.Clear();

            foreach (var spawn in spawnData)
            {
                if (!config.spawn.ContainsKey(spawn.Key))
                    continue;

                if (!spawnSettings.ContainsKey(spawn.Key))
                    continue;

                foreach (var data in spawn.Value)
                {
                    if (gatherManager.spawns.ContainsKey(data.spawnID))
                        continue;

                    var loot = GameManager.server.CreateEntity(spawn.Key, BuildingSpawnPoint + data.position);
                    loot.transform.eulerAngles = data.rotation;
                    loot.enableSaving = false;
                    loot.Spawn();

                    if (loot as BaseNpc)
                       plugin.RemoveAi(loot as BaseNpc);

                   gatherManager.spawns.Add(data.spawnID, new GatherManager.spawn() { prefab = spawn.Key, position = data.position, rotation = data.rotation, spawnTime = -1 });
                   gatherManager.loot.Add(loot, data.spawnID);
                }
            }
        }

        private void RemoveAllSpawns()
        {
            spawnData.Clear();
            foreach (var ent in gatherManager.loot)
                if (ent.Key != null)
                    ent.Key.Kill();

            gatherManager.loot.Clear();
            gatherManager.spawns.Clear();
            SaveSpawnData();
        }

        private void RemoveAllTexts()
        {
            textData.Clear();
            SaveTextData();
        }

        private void AddNewText()
        {
            if (placeManager == null)
            {
                PrintError("PlaceManager null");
                return;
            }

            textData.Add(new TextData() { dist = placeManager.Dist, position = placeManager.direct - BuildingSpawnPoint, text = placeManager.Text });
            DrawText(placeManager.Player, 2f, Color.white, placeManager.direct, GetMsg("Text_Succes", placeManager.Player));
            SaveTextData();
            placeManager.DestroyComp();
        }

        private void AddNewSpawn()
        {
            if (placeManager == null)
            {
                PrintError("PlaceManager null");
                return;
            }

            if (!spawnSettings.ContainsKey(placeManager.ent.PrefabName))
            {
                PrintError($"This prefab {placeManager.ent.prefabID} not in data!");
                return;
            }

            var ent = placeManager.ent;

            if (!spawnData.ContainsKey(ent.PrefabName))
                spawnData.Add(ent.PrefabName, new List<SpawnData>());

            SpawnData data = new SpawnData();
            data.position = ent.transform.position - BuildingSpawnPoint;
            data.rotation = ent.transform.eulerAngles;
            data.spawnID = UnityEngine.Random.Range(1000, 9999);
            spawnData[ent.PrefabName].Add(data);
            DrawBox(placeManager.Player, 10f, Color.green, ent.transform.position);
            DrawText(placeManager.Player, 10f, Color.white, ent.transform.position, $"{GetMsg(spawnSettings[ent.PrefabName].langName, placeManager.Player)} : #{data.spawnID}");
            SaveSpawnData();

        }

        private void CreatePlaceManager(BasePlayer player, string prefab)
        {
            if(placeManager != null)
            {
                Notice(player, "UI_Admin_Notice_AlreadyUse");
                return;
            }

            CuiHelper.DestroyUi(player, "EasyGatherAdmin");

            placeManager = player.gameObject.AddComponent<PlaceManager>();
            placeManager.Init(player, prefab, Convert.ToInt32(addText.dist), Convert.ToInt32(addText.size), addText.text);
        }

        private void RemoveAi(BaseNpc npc)
        {
            if (npc == null)
            {
                return;
            }

            npc.CancelInvoke(npc.TickAi);
            var script1 = npc.GetComponent<AiManagedAgent>();
            UnityEngine.Object.Destroy(script1);
            var script2 = npc.GetComponent<AnimalBrain>();
            UnityEngine.Object.Destroy(script2);
            var script3 = npc.GetComponent<NPCNavigator>();
            UnityEngine.Object.Destroy(script3);

            var obj = npc as BaseAnimalNPC;
            if (obj != null)
            {
                AIThinkManager.RemoveAnimal(obj);
            }
        }

        private void ShowSpawns(BasePlayer player, string prefab)
        {
            foreach(var spawn in spawnData[prefab])
            {
                DrawBox(player, 15f, Color.green, BuildingSpawnPoint + spawn.position);
                DrawText(player, 15f, Color.white, BuildingSpawnPoint + spawn.position, $"{GetMsg(spawnSettings[prefab].langName, player)} : #{spawn.spawnID}");
            }
        }

        private void RemoveSpawns(BasePlayer player, string prefab)
        {
            spawnData[prefab].Clear();
            SaveSpawnData();
        }

        private string RemoveSpawnID(BasePlayer player, string prefab, string id)
        {
            if (string.IsNullOrEmpty(id))
                return "UI_Admin_Notice_Incorrect_ID";

            foreach (var c in id)
                if (!char.IsNumber(c))
                    return "UI_Admin_Notice_OnlyChar";

            var spawn = spawnData[prefab].Where(s => s.spawnID == Convert.ToInt32(id)).FirstOrDefault();
            if (spawn == null)
                return "UI_Admin_Notice_Remove_NotFound";

            spawnData[prefab].Remove(spawn);
            SaveSpawnData();
            return "UI_Admin_Notice_Remove_Suc";
        }

        private int HaveTickets(BasePlayer player, ulong SkinID)
        {
            int amount = 0;
            foreach (var item in player.inventory.containerMain.itemList)
            {
                if (item.skin == SkinID)
                    amount += item.amount;
            }

            foreach (var item in player.inventory.containerBelt.itemList)
            {
                if (item.skin == SkinID)
                    amount += item.amount;
            }

            return amount;
        }

        private bool removeTicket(BasePlayer player, ulong SkinID)
        {
            for (int i = 0; i < 24; i++)
            {
                var item = player.inventory.containerMain.GetSlot(i);
                if (item == null)
                    continue;

                if (item.skin == SkinID)
                {
                    item.UseItem(1);
                    return true;
                }
            }

            for (int i = 0; i < 6; i++)
            {
                var item = player.inventory.containerBelt.GetSlot(i);
                if (item == null)
                    continue;

                if (item.skin == SkinID)
                {
                    item.UseItem(1);
                    return true;
                }
            }

            return false;
        }

        private void DrawText(BasePlayer player, float time, Color color, Vector3 pos, string text)
        {
            player.SendConsoleCommand("ddraw.text", time, color, pos, text);
        }

        private void DrawBox(BasePlayer player, float time, Color color, Vector3 pos, float size = 1f)
        {
            player.SendConsoleCommand("ddraw.box", time, color, pos, size);
        }

        private void DrawSphere(BasePlayer player, float time, Color color, Vector3 pos, float size = 1f)
        {
            player.SendConsoleCommand("ddraw.sphere", time, Color.blue, pos, size);
        }

        void TeleportPlayerToPos(BasePlayer player, Vector3 xyz)
        {
            player.UpdateActiveItem(0u);
            player.EnsureDismounted();
            player.Server_CancelGesture();

            if (player.HasParent())
            {
                player.SetParent(null, true, true);
            }

            player.RemoveFromTriggers();

            if (player.IsConnected)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
                player.ClientRPCPlayer(null, player, "StartLoading");
                player.SendEntityUpdate();
                player.EndLooting();
                if (!player.IsSleeping())
                {
                    Interface.CallHook("OnPlayerSleep", player);
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
                    player.sleepStartTime = Time.time;
                    BasePlayer.sleepingPlayerList.Add(player);
                    player.CancelInvoke("InventoryUpdate");
                    player.CancelInvoke("TeamUpdate");
                }

                player.Teleport(xyz);
                player.SendFullSnapshot();
                player.UpdateNetworkGroup();
                player.SendNetworkUpdateImmediate(false);
            }
        }

        private void GivePlayerTicket(IPlayer sender, BasePlayer player, ulong skinID)
        {
            var ticket = config.tickets.Where(t => t.skinID == skinID).FirstOrDefault();
            if (ticket == null)
            {
                sender.Message($"Ticket with skin {skinID} not found");
                return;
            }

            var newitem = ItemManager.CreateByName(config.main.shortName, 1, skinID);
            newitem.name = ticket.customName;
            player.GiveItem(newitem);
            player.ChatMessage($"You have been given a ticket for : {ticket.time} seconds");
        }

        private Dictionary<ulong, string> GetPlayers(string nameOrId)
        {
            var pl = covalence.Players.FindPlayers(nameOrId).ToList();
            return pl.Select(p => new KeyValuePair<ulong, string>(ulong.Parse(p.Id), p.Name)).ToDictionary(x => x.Key, x => x.Value);
        }

        private BasePlayer FindBasePlayer(ulong userId)
        {
            BasePlayer player = BasePlayer.activePlayerList.FirstOrDefault(p => p.userID == userId);
            player = player ?? BasePlayer.sleepingPlayerList.FirstOrDefault(p => p.userID == userId);
            return player;
        }
        #endregion

        #region [Data]
        private void LoadSpawnData() => spawnData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, List<SpawnData>>>($"EasyGather/SpawnData/{config.main.fileName}");
        private void SaveSpawnData() => Interface.Oxide.DataFileSystem.WriteObject($"EasyGather/SpawnData/{config.main.fileName}", spawnData);

        private void LoadTextData()
        {
            textData = Interface.Oxide.DataFileSystem.ReadObject<List<TextData>>($"EasyGather/TextData/{config.main.fileName}");
            Spawns = Interface.Oxide.DataFileSystem.ReadObject<DefaultSpawns>($"EasyGather/TextData/Spawns/{config.main.fileName}");
        }

        private void SaveTextData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"EasyGather/TextData/{config.main.fileName}", textData);
            Interface.Oxide.DataFileSystem.WriteObject($"EasyGather/TextData/Spawns/{config.main.fileName}", Spawns);
        }

        private void SavePlayerData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("EasyGather/PlayerData/data", playerData);
        }

        private void LoadPlayerData()
        {
            playerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>("EasyGather/PlayerData/data");
        }


        #endregion

        #region [GatherManager]
        private class GatherManager : MonoBehaviour
        {
            public Dictionary<BaseEntity, int> loot = new Dictionary<BaseEntity, int>();
            public Dictionary<int, spawn> spawns = new Dictionary<int, spawn>();
            public List<BasePlayer> players;


            public Vector3 spawnPlayer;
            public Vector3 textWithMain;

            private TimeSpan time;

            public class spawn
            {
                public int spawnTime;
                public string prefab;
                public Vector3 position;
                public Vector3 rotation;
            }

            public void Awake()
            {
                InvokeRepeating("Timer", 1f, 1);
            }

            private void Timer()
            {
                players = (List<BasePlayer>)plugin.ZoneManager?.Call("GetPlayersInZone", plugin.zoneID);
                foreach (var player in players)
                {
                    
                    if(!plugin.playerData.ContainsKey(player.userID))
                    {
                        player.metabolism.radiation_poison.value += 5;
                        player.health -= 5;

                        SetPlayerFlag(player, BasePlayer.PlayerFlags.IsAdmin, true);
                        plugin.DrawText(player, 1f, Color.white, plugin.BuildingSpawnPoint + plugin.Spawns.TimerTextPosition, plugin.GetMsg("TimerText2", player));
                        SetPlayerFlag(player, BasePlayer.PlayerFlags.IsAdmin, false);

                        continue;
                    }

                    if(plugin.playerData[player.userID].gatherTime <= 0)
                    {
                        if (plugin.config.main.penaltyType == 1)
                        {
                            player.metabolism.radiation_poison.value += 5;
                            player.health -= 5;
                        }
                        else if(plugin.config.main.penaltyType == 2)
                        {
                            player.Die();
                        }

                        SetPlayerFlag(player, BasePlayer.PlayerFlags.IsAdmin, true);
                        plugin.DrawText(player, 1f, Color.white, plugin.BuildingSpawnPoint + plugin.Spawns.TimerTextPosition, plugin.GetMsg("TimerText2", player));
                        SetPlayerFlag(player, BasePlayer.PlayerFlags.IsAdmin, false);
                        continue;
                    }

                    plugin.playerData[player.userID].gatherTime--;
                    time = TimeSpan.FromSeconds(plugin.playerData[player.userID].gatherTime);
                    SetPlayerFlag(player, BasePlayer.PlayerFlags.IsAdmin, true);
                    plugin.DrawText(player, 1f, Color.white, plugin.BuildingSpawnPoint + plugin.Spawns.TimerTextPosition, String.Format(plugin.GetMsg("TimerText1", player), time.Hours, time.Minutes, time.Seconds));
                    SetPlayerFlag(player, BasePlayer.PlayerFlags.IsAdmin, false);

                    foreach (var tData in plugin.textData)
                    {
                        if (String.IsNullOrEmpty(tData.text))
                            continue;

                        if (Vector3.Distance(player.transform.position, plugin.BuildingSpawnPoint + tData.position) > tData.dist)
                            continue;

                        SetPlayerFlag(player, BasePlayer.PlayerFlags.IsAdmin, true);
                        plugin.DrawText(player, 1f, Color.white, plugin.BuildingSpawnPoint + tData.position, tData.text);
                        SetPlayerFlag(player, BasePlayer.PlayerFlags.IsAdmin, false);
                    }
                }

                foreach(var spawn in spawns.Where(t => t.Value.spawnTime != -1))
                {
                    if(spawn.Value.spawnTime == 0)
                    {
                        var ent = GameManager.server.CreateEntity(spawn.Value.prefab, plugin.BuildingSpawnPoint + spawn.Value.position);
                        ent.transform.eulerAngles = spawn.Value.rotation;
                        ent.enableSaving = false;
                        ent.Spawn();

                        if (ent as BaseNpc)
                            plugin.RemoveAi(ent as BaseNpc);

                        spawn.Value.spawnTime = -1;
                        loot.Add(ent, spawn.Key);
                        continue;
                    }

                    spawn.Value.spawnTime--;
                }
            }

            public void UpdateSpawn(BaseEntity ent)
            {
                if (!loot.ContainsKey(ent)) return;
                var spawnID = loot[ent];
                if (!spawns.ContainsKey(spawnID)) return;
                spawns[spawnID].spawnTime = plugin.config.spawn[spawns[spawnID].prefab];                
            }

            private void SetPlayerFlag(BasePlayer player, BasePlayer.PlayerFlags f, bool b)
            {
                if (plugin.permission.UserHasGroup(player.UserIDString, "admin")) return;

                if (b)
                {
                    if (player.HasPlayerFlag(f)) return;
                    player.playerFlags |= f;
                }
                else
                {
                    if (!player.HasPlayerFlag(f)) return;
                    player.playerFlags &= ~f;
                }
                player.SendNetworkUpdateImmediate(false);
            }

            public void DestroyComp() => OnDestroy();
            private void OnDestroy()
            {
                Destroy(this);
            }
        }
        #endregion

        #region [PlaceManager]
        private class PlaceManager : MonoBehaviour
        {
            public BaseEntity ent = null;
            public BasePlayer Player = null;
            public string Prefab = "";
            public float Dist = 0f;
            public string Text = "";
            public bool isText = false;
            private Vector3 curPos;
            public Vector3 direct;
            private RaycastHit hit;

            public void Init(BasePlayer player, string prefab, float dist = 1f, int size = 16, string text = "...")
            {
                Player = player;
                Prefab = prefab;

                if (player == null)
                {
                    DestroyComp();
                    return;
                }

                if (!prefab.Contains("addText"))
                {
                    ent = GameManager.server.CreateEntity(prefab, Player.eyes.position + Player.eyes.rotation * new Vector3(0f, 0f, 5f));
                    ent.enableSaving = false;
                    ent.Spawn();

                    if (ent as BaseNpc)
                        plugin.RemoveAi(ent as BaseNpc);

                    if (ent as LootContainer)
                        plugin.Subscribe(nameof(CanLootEntity));

                    plugin.CreateButtons(Player);
                }
                else
                {
                    Dist = dist;
                    Text = $"<size={size}>{text}</size>";
                    plugin.CreateButtons(Player, true);
                    isText = true;
                }
            }


            public void DestroyComp() => OnDestroy();
            private void OnDestroy()
            {
                if (ent != null)
                {
                    if(ent as LootContainer)
                        plugin.Unsubscribe(nameof(CanLootEntity));

                    ent.Kill();
                }
                if(Player != null)
                    CuiHelper.DestroyUi(Player, "ButtonsMain");

                Destroy(this);
            }

            private void FixedUpdate()
            {
                if (isText)
                {
                    direct = Player.eyes.position + Player.eyes.rotation * new Vector3(0f, 0f, 5f);

                    plugin.DrawText(Player, Time.deltaTime, Color.white, direct, Text);
                    plugin.DrawSphere(Player, Time.deltaTime, Color.green, direct, Dist);
                    plugin.DrawBox(Player, Time.deltaTime, Color.red, direct, 0.5f);

                    if (Player.serverInput.WasJustPressed(BUTTON.FIRE_PRIMARY))
                    {
                        plugin.AddNewText();
                    }
                    else if (Player.serverInput.WasJustPressed(BUTTON.FIRE_SECONDARY))
                    {
                        DestroyComp();
                    }
                    return;
                }

                if (ent == null)
                    DestroyComp();

                
                direct = Player.eyes.position + Player.eyes.rotation * new Vector3(0f, 0f, 5f);

                if (!Physics.Linecast(Player.eyes.position, direct, out hit, Layers.Construction))
                {
                    curPos = direct;
                }
                else
                {
                    curPos = hit.point;
                }
                
                ent.transform.position = curPos;
                ent.SendNetworkUpdateImmediate();

                if (Player.serverInput.WasJustPressed(BUTTON.FIRE_PRIMARY))
                {
                    plugin.AddNewSpawn();
                }
                else if (Player.serverInput.WasJustPressed(BUTTON.FIRE_SECONDARY))
                {
                    plugin.CreateAdminUI_AddSpawn(Player, "AddSpawn", plugin.spawnSettings[Prefab].category);
                    DestroyComp();
                }
                
                if (Player.serverInput.IsDown(BUTTON.USE))
                {
                    ent.transform.Rotate(0f, 100f * Time.deltaTime, 0f);
                }
                else if (Player.serverInput.IsDown(BUTTON.RELOAD))
                {
                    ent.transform.Rotate(0f, -100f * Time.deltaTime, 0f);
                }
            }
        }
        #endregion

        #region [Commands]
        [ChatCommand("aeg")]
        private void cmd_EG_Admin(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
                return;

            CreateAdminUI(player);
        }

        [ChatCommand("eg")]
        private void cmd_EG(BasePlayer player, string command, string[] args)
        {
            CreatePlayerUI(player);
        }

        private void GiveTicket(IPlayer player, string command, string[] args)
        {
            if (args.Length < 2)
            {
                player.Message($"Incorret command\n/gtic \"Nick|SteamID\" \"SkinID\" ");
                return;
            }
            var recivers = GetPlayers(args[0]);
            if (recivers == null || recivers.Count == 0)
            {
                player.Message($"Player Not Found");
                return;
            }
            if (recivers.Count > 1)
            {
                player.Message($"Found multiple players {string.Join("\n", recivers.Select(p => $"{p.Value} ({p.Key})").ToArray())}");
                return;
            }
            var Ireciver = recivers.First();
            var reciver = FindBasePlayer(Ireciver.Key);
            if (reciver == null)
            {
                player.Message($"Player not connected");
                return;
            }
            GivePlayerTicket(player, reciver, Convert.ToUInt64(args[1]));
        }
        #endregion

        #region [AdminIU]
            private void CreateAdminUI(BasePlayer player)
        {
            if (!player.IsAdmin)
                return;

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.3584906 0.3584906 0.2790139 0.5529412", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-392.61 -215.836", OffsetMax = "392.61 215.836" }
            }, "Overlay", "EasyGatherAdmin");

            container.Add(new CuiElement
            {
                Name = "Header",
                Parent = "EasyGatherAdmin",
                Components = {
                    new CuiTextComponent { Text = String.Format(GetMsg("UI_Admin_Header", player), config.main.fileName, GetMsg("UI_Admin_Main", player)), Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-381.799 181", OffsetMax = "340.349 215.84" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Spawn",
                Parent = "EasyGatherAdmin",
                Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_Spawns", player), Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "0.7181212 0.764151 0.6812478 0.772549" },

                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-349.335 139.58", OffsetMax = "-124.665 174.42" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.5351295 0.7264151 0.3871929 0.5686275", Command = "UI_ADMIN_MAIN_ACTION AddSpawn" },
                Text = { Text = "   ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-349.335 91.863", OffsetMax = "-124.665 119.283" }
            }, "EasyGatherAdmin", "bnt_addSpawn");

            container.Add(new CuiElement
            {
                Name = "Text",
                Parent = "bnt_addSpawn",
                Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_AddSpawn", player), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.2705882", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112.335 -13.71", OffsetMax = "112.335 13.71" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.5351295 0.7264151 0.3871929 0.5686275", Command = "UI_ADMIN_MAIN_ACTION EditSpawn" },
                Text = { Text = "   ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-349.335 58.39", OffsetMax = "-124.665 85.81" }
            }, "EasyGatherAdmin", "bnt_EditSpawn");

            container.Add(new CuiElement
            {
                Name = "Text",
                Parent = "bnt_EditSpawn",
                Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_EditSpawn", player), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.2705882", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112.335 -13.71", OffsetMax = "112.335 13.71" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.5351295 0.7264151 0.3871929 0.5686275", Command = "UI_ADMIN_MAIN_ACTION UpdateSpawns" },
                Text = { Text = "   ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-349.335 24.19", OffsetMax = "-124.665 51.61" }
            }, "EasyGatherAdmin", "bnt_UpdateSpawns");

            container.Add(new CuiElement
            {
                Name = "Text",
                Parent = "bnt_UpdateSpawns",
                Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_RefreshSpawn", player), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.2705882", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112.335 -13.71", OffsetMax = "112.335 13.71" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.6603774 0.3769135 0.3769135 0.5686275", Command = "UI_ADMIN_MAIN_ACTION RemoveSpawns" },
                Text = { Text = "   ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-349.335 -80.61", OffsetMax = "-124.665 -53.19" }
            }, "EasyGatherAdmin", "bnt_RemoveSpawns");

            container.Add(new CuiElement
            {
                Name = "Text",
                Parent = "bnt_RemoveSpawns",
                Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_RemoveSpawn", player), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.2705882", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112.335 -13.71", OffsetMax = "112.335 13.71" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Text",
                Parent = "EasyGatherAdmin",
                Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_Texts", player), Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "0.7181212 0.764151 0.6812478 0.772549" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112.334 139.58", OffsetMax = "112.336 174.42" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.5351295 0.7264151 0.3871929 0.5686275",  Command = "UI_ADMIN_MAIN_ACTION AddText" },
                Text = { Text = "   ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-109.335 91.863", OffsetMax = "115.335 119.283" }
            }, "EasyGatherAdmin", "bnt_addSpawnText");

            container.Add(new CuiElement
            {
                Name = "Text",
                Parent = "bnt_addSpawnText",
                Components = {
                    new CuiTextComponent { Text =  GetMsg("UI_Admin_AddText", player), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.2705882", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112.335 -13.71", OffsetMax = "112.335 13.71" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.5351295 0.7264151 0.3871929 0.5686275", Command = "UI_ADMIN_MAIN_ACTION EditText" },
                Text = { Text = "   ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-109.335 58.39", OffsetMax = "115.335 85.81" }
            }, "EasyGatherAdmin", "bnt_EditSpawnText");

            container.Add(new CuiElement
            {
                Name = "Text",
                Parent = "bnt_EditSpawnText",
                Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_EditText", player), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.2705882", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112.335 -13.71", OffsetMax = "112.335 13.71" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.5351295 0.7264151 0.3871929 0.5686275", Command = "UI_ADMIN_MAIN_ACTION SetTimerText" },
                Text = { Text = "   ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-109.335 24.19", OffsetMax = "115.335 51.61" }
            }, "EasyGatherAdmin", "bnt_SetTimerText");

            container.Add(new CuiElement
            {
                Name = "Text",
                Parent = "bnt_SetTimerText",
                Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_SetTimerText", player), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.2705882", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112.335 -13.71", OffsetMax = "112.335 13.71" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.6603774 0.3769135 0.3769135 0.5686275", Command = "UI_ADMIN_MAIN_ACTION RemoveAllText" },
                Text = { Text = "   ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-109.335 -80.61", OffsetMax = "115.335 -53.19" }
            }, "EasyGatherAdmin", "bnt_RemoveAllText");

            container.Add(new CuiElement
            {
                Name = "Text",
                Parent = "bnt_RemoveAllText",
                Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_RemoveText", player), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.2705882", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112.335 -13.71", OffsetMax = "112.335 13.71" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Settings",
                Parent = "EasyGatherAdmin",
                Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_Settings", player), Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "0.7181212 0.764151 0.6812478 0.772549" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "143.665 139.58", OffsetMax = "368.335 174.42" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.2745098 0.282353 0.2705882 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "143.666 91.86", OffsetMax = "368.336 119.28" }
            }, "EasyGatherAdmin", "Input_Name_Panel");

            container.Add(new CuiElement
            {
                Name = "Input_Name",
                Parent = "Input_Name_Panel",
                Components = {
                    new CuiNeedsKeyboardComponent(),
                    new CuiInputFieldComponent { Color = "1 1 1 0.9", FontSize = 16, Align = TextAnchor.MiddleCenter, IsPassword = false, Command = "UI_ADMIN_MAIN_ACTION ChangeName", Text = GetMsg("UI_Admin_SettingsChangeName", player)  },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112.336 -13.71", OffsetMax = "112.334 13.71" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.5351295 0.7264151 0.3871929 0.5686275", Command = "UI_ADMIN_MAIN_ACTION SetPlayerSpawn" },
                Text = { Text = "   ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "143.665 58.39", OffsetMax = "368.335 85.81" }
            }, "EasyGatherAdmin", "bnt_PlayerSpawn");

            container.Add(new CuiElement
            {
                Name = "Text",
                Parent = "bnt_PlayerSpawn",
                Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_SettingsSetSpawn", player), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.2705882", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112.335 -13.71", OffsetMax = "112.335 13.71" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.6603774 0.3769135 0.3769135 0.5686275", Command = "UI_ADMIN_MAIN_ACTION FullUpdate" },
                Text = { Text = "   ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "143.665 -80.61", OffsetMax = "368.335 -53.19" }
            }, "EasyGatherAdmin", "bnt_FullUpdateBuildin");

            container.Add(new CuiElement
            {
                Name = "Text",
                Parent = "bnt_FullUpdateBuildin",
                Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_SettingsFullUpdate", player), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.2705882", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112.335 -13.71", OffsetMax = "112.335 13.71" }
                }
            });


            container.Add(new CuiButton
            {
                Button = { Color = "0.5351295 0.7264151 0.3871929 0.5686275", Command = "UI_ADMIN_MAIN_ACTION tp" },
                Text = { Text = "   ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-349.335 -176.71", OffsetMax = "-124.665 -149.29" }
            }, "EasyGatherAdmin", "bnt_Teleport");

            container.Add(new CuiElement
            {
                Name = "Text",
                Parent = "bnt_Teleport",
                Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_Teleport", player), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.2705882", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112.335 -13.71", OffsetMax = "112.335 13.71" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.5351295 0.7264151 0.3871929 0.5686275", Command = "UI_ADMIN_MAIN_ACTION ftime" },
                Text = { Text = "   ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-109.335 -176.71", OffsetMax = "115.335 -149.29" }
            }, "EasyGatherAdmin", "bnt_FreeTime");

            container.Add(new CuiElement
            {
                Name = "Text",
                Parent = "bnt_FreeTime",
                Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_FreeTime", player), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.2705882", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112.335 -13.71", OffsetMax = "112.335 13.71" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = "UI_ADMIN_CLOSE" },
                Text = { Text = "X", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter, Color = "1 0.01415092 0.01415092 0.45" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "351.778 181", OffsetMax = "392.61 215.84" }
            }, "EasyGatherAdmin", "Exit");

            CuiHelper.DestroyUi(player, "EasyGatherAdmin");
            CuiHelper.AddUi(player, container);
        }

        private void CreateAdminUI_AddSpawn(BasePlayer player, string action, string category = "")
        {
            if (!player.IsAdmin)
                return;

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.3584906 0.3584906 0.2790139 0.5529412", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-392.61 -215.836", OffsetMax = "392.61 215.836" }
            }, "Overlay", "EasyGatherAdmin");

            if (action == "AddSpawn")
            {
                container.Add(new CuiElement
                {
                    Name = "Header",
                    Parent = "EasyGatherAdmin",
                    Components = {
                    new CuiTextComponent { Text = String.Format(GetMsg("UI_Admin_Header", player), config.main.fileName, GetMsg("UI_Admin_AddSpawn", player)), Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-381.799 181", OffsetMax = "340.349 215.84" }
                }
                });
            }
            else
            {
                container.Add(new CuiElement
                {
                    Name = "Header",
                    Parent = "EasyGatherAdmin",
                    Components = {
                    new CuiTextComponent { Text = String.Format(GetMsg("UI_Admin_Header", player), config.main.fileName, GetMsg("UI_Admin_EditSpawn", player)), Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-381.799 181", OffsetMax = "340.349 215.84" }
                }
                });
            }

            if (category == "")
            {
                container.Add(new CuiButton
                {
                    Button = { Color = "0.5351295 0.7264151 0.3871929 0.5686275", Command = $"UI_ADMIN_SPAWN {action} Ore" },
                    Text = { Text = "   ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-349.335 91.863", OffsetMax = "-124.665 119.283" }
                }, "EasyGatherAdmin", "bnt_Ores");

                container.Add(new CuiElement
                {
                    Name = "Text",
                    Parent = "bnt_Ores",
                    Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_Ores", player), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.2705882", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112.335 -13.71", OffsetMax = "112.335 13.71" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "0.5351295 0.7264151 0.3871929 0.5686275", Command = $"UI_ADMIN_SPAWN {action} Animal" },
                    Text = { Text = "   ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-349.335 58.39", OffsetMax = "-124.665 85.81" }
                }, "EasyGatherAdmin", "bnt_Animals");

                container.Add(new CuiElement
                {
                    Name = "Text",
                    Parent = "bnt_Animals",
                    Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_Animals", player), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.2705882", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112.335 -13.71", OffsetMax = "112.335 13.71" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "0.5351295 0.7264151 0.3871929 0.5686275", Command = $"UI_ADMIN_SPAWN {action} Crate" },
                    Text = { Text = "   ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-349.335 24.19", OffsetMax = "-124.665 51.61" }
                }, "EasyGatherAdmin", "bnt_Crates");

                container.Add(new CuiElement
                {
                    Name = "Text",
                    Parent = "bnt_Crates",
                    Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_Crates", player), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.2705882", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112.335 -13.71", OffsetMax = "112.335 13.71" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "0.5351295 0.7264151 0.3871929 0.5686275", Command = $"UI_ADMIN_SPAWN {action} Barrel" },
                    Text = { Text = "   ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-349.335 -9.41", OffsetMax = "-124.665 18.01" }
                }, "EasyGatherAdmin", "bnt_Barrels");

                container.Add(new CuiElement
                {
                    Name = "Text",
                    Parent = "bnt_Barrels",
                    Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_Barrels", player), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.2705882", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112.335 -13.71", OffsetMax = "112.335 13.71" }
                }
                });
            }
            else
            {
                int i = 0;
                int j = 0;

                foreach (var prefab in spawnSettings.Where(p => p.Value.category == category))
                {
                    container.Add(new CuiButton
                    {
                        Button = { Color = "0.5351295 0.7264151 0.3871929 0.5686275", Command = $"UI_ADMIN_SPAWN {action} show {prefab.Key}" },
                        Text = { Text = "   ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{-349.335 + (i * 235.0)} {91.863 - (j * 30)}", OffsetMax = $"{-124.665 + (i * 235.0)} {119.283 - (j * 30)}" }
                    }, "EasyGatherAdmin", "bnts");

                    container.Add(new CuiElement
                    {
                        Name = "Text",
                        Parent = "bnts",
                        Components = {
                    new CuiTextComponent { Text = GetMsg(prefab.Value.langName, player), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.2705882", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112.335 -13.71", OffsetMax = "112.335 13.71" }
                    }
                    });

                    i++;
                    if (i == 3)
                    {

                        i = 0;
                        j++;
                    }
                }

            }

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = "UI_ADMIN_SPAWN return" },
                Text = { Text = "<-", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter, Color = "1 0.01415092 0.01415092 0.45" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-392.606 181", OffsetMax = "-351.774 215.84" }
            }, "EasyGatherAdmin", "Return");

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = "UI_ADMIN_CLOSE" },
                Text = { Text = "X", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter, Color = "1 0.01415092 0.01415092 0.45" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "351.778 181", OffsetMax = "392.61 215.84" }
            }, "EasyGatherAdmin", "Exit");


            CuiHelper.DestroyUi(player, "EasyGatherAdmin");
            CuiHelper.AddUi(player, container);
        }

        private void CreateAdminUI_ShowSpawn(BasePlayer player, string prefab)
        {
            if (!player.IsAdmin)
                return;

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.3584906 0.3584906 0.2790139 0.5529412", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-392.61 -215.836", OffsetMax = "392.61 215.836" }
            }, "Overlay", "EasyGatherAdmin");

            container.Add(new CuiElement
            {
                Name = "Header",
                Parent = "EasyGatherAdmin",
                Components = {
                    new CuiTextComponent { Text = String.Format(GetMsg("UI_Admin_Header", player), config.main.fileName, GetMsg("UI_Admin_EditSpawn", player)), Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-381.799 181", OffsetMax = "340.349 215.84" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Name",
                Parent = "EasyGatherAdmin",
                Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_Name", player), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleLeft, Color = "0.7181212 0.764151 0.6812478 0.772549" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-385.686 117.549", OffsetMax = "-260.046 147.851" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.4716981 0.4716981 0.4249733 0.8705882", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-385.7 87.249", OffsetMax = "0 117.551" }
            }, "EasyGatherAdmin", "panelName");

            container.Add(new CuiElement
            {
                Name = "Name",
                Parent = "panelName",
                Components = {
                    new CuiTextComponent { Text = GetMsg(spawnSettings[prefab].langName, player), Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.6862745" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-186.329 -15.151", OffsetMax = "182.918 15.151" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Prefab",
                Parent = "EasyGatherAdmin",
                Components = {
                    new CuiTextComponent { Text = "Prefab", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleLeft, Color = "0.7181212 0.764151 0.6812478 0.772549" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-385.69 48.551", OffsetMax = "-260.05 78.853" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.4716981 0.4716981 0.4249733 0.8705882", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-385.685 18.249", OffsetMax = "0.005 48.551" }
            }, "EasyGatherAdmin", "panelPrefab");

            container.Add(new CuiElement
            {
                Name = "Prefab",
                Parent = "panelPrefab",
                Components = {
                    new CuiTextComponent { Text = prefab, Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.6862745" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-188.085 -15.151", OffsetMax = "186.008 15.151" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "RespawnTime",
                Parent = "EasyGatherAdmin",
                Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_RespawnTime", player), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleLeft, Color = "0.7181212 0.764151 0.6812478 0.772549" },

                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-385.69 -22.551", OffsetMax = "-0.01 7.751" }
                }
});

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.2745098 0.282353 0.2705882 1", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-385.685 -52.853", OffsetMax = "0.005 -22.551" }
            }, "EasyGatherAdmin", "panelTime");

            container.Add(new CuiElement
            {
                Name = "Time",
                Parent = "panelTime",
                Components = {
                    new CuiNeedsKeyboardComponent(),
                    new CuiInputFieldComponent { Color = "1 1 1 0.9", FontSize = 16, Align = TextAnchor.MiddleCenter, IsPassword = false, Command = $"UI_ADMIN_EDIT_SPAWN time {prefab}", Text = config.spawn[prefab].ToString()  },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112.336 -13.71", OffsetMax = "112.334 13.71" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "RemoveSpawn",
                Parent = "EasyGatherAdmin",
                Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_RemoveSpawnID", player), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleLeft, Color = "0.7181212 0.764151 0.6812478 0.772549" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "120.788 117.549", OffsetMax = "287.331 147.851" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.2745098 0.282353 0.2705882 1", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "73.328 86.849", OffsetMax = "312.068 117.151" }
            }, "EasyGatherAdmin", "removeSpawn");

            container.Add(new CuiElement
            {
                Name = "panelRemoveSpawn",
                Parent = "removeSpawn",
                Components = {
                    new CuiNeedsKeyboardComponent(),
                    new CuiInputFieldComponent { Color = "1 1 1 0.9", FontSize = 16, Align = TextAnchor.MiddleCenter, IsPassword = false, Command = $"UI_ADMIN_EDIT_SPAWN removeid {prefab}", Text = "ID"  },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.5351295 0.7264151 0.3871929 0.5686275", Command = $"UI_ADMIN_EDIT_SPAWN show {prefab}" },
                Text = { Text = "   ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-386.24 -194.01", OffsetMax = "-197.24 -166.59" }
            }, "EasyGatherAdmin", "bnt_ShowAllSpawn");

            container.Add(new CuiElement
            {
                Name = "Text",
                Parent = "bnt_ShowAllSpawn",
                Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_ShowSpawn", player), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.2705882", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-86.794 -13.71", OffsetMax = "86.796 13.71" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.6603774 0.3769135 0.3769135 0.5686275", Command = $"UI_ADMIN_EDIT_SPAWN remove {prefab}" },
                Text = { Text = "   ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-189.324 -194.01", OffsetMax = "-0.005 -166.59" }
            }, "EasyGatherAdmin", "bnt_RemoveAllSpawn");

            container.Add(new CuiElement
            {
                Name = "Text",
                Parent = "bnt_RemoveAllSpawn",
                Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_RemoveSpawn", player), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.2705882", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-94.659 -13.71", OffsetMax = "94.661 13.71" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = "UI_ADMIN_SPAWN return" },
                Text = { Text = "<-", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter, Color = "1 0.01415092 0.01415092 0.45" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-392.606 181", OffsetMax = "-351.774 215.84" }
            }, "EasyGatherAdmin", "Return");

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = "UI_ADMIN_CLOSE" },
                Text = { Text = "X", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter, Color = "1 0.01415092 0.01415092 0.45" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "351.778 181", OffsetMax = "392.61 215.84" }
            }, "EasyGatherAdmin", "Exit");

            CuiHelper.DestroyUi(player, "EasyGatherAdmin");
            CuiHelper.AddUi(player, container);
        }

        private void CreateAdminUI_AddText(BasePlayer player)
        {
            if (!player.IsAdmin)
                return;

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.3584906 0.3584906 0.2790139 0.5529412", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-392.61 -215.836", OffsetMax = "392.61 215.836" }
            }, "Overlay", "EasyGatherAdmin");

            container.Add(new CuiElement
            {
                Name = "Header",
                Parent = "EasyGatherAdmin",
                Components = {
                    new CuiTextComponent { Text = String.Format(GetMsg("UI_Admin_Header", player), config.main.fileName, GetMsg("UI_Admin_AddText", player)), Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-381.799 181", OffsetMax = "340.349 215.84" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Text",
                Parent = "EasyGatherAdmin",
                Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_WriteText", player), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.7181212 0.764151 0.6812478 0.772549" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-26.919 117.549", OffsetMax = "98.721 147.851" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.4716981 0.4716981 0.4249733 0.8705882", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-292.173 87.249", OffsetMax = "349.181 117.551" }
            }, "EasyGatherAdmin", "panelText");

            container.Add(new CuiElement
            {
                Name = "Text",
                Parent = "panelText",
                Components = {
                    new CuiNeedsKeyboardComponent(),
                    new CuiInputFieldComponent { Color = "1 1 1 0.9", FontSize = 16, Align = TextAnchor.MiddleCenter, IsPassword = false, Command = $"UI_ADMIN_TEXT text", Text = addText.text  },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112.336 -13.71", OffsetMax = "112.334 13.71" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "ShowRadius",
                Parent = "EasyGatherAdmin",
                Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_Radius", player), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.7181212 0.764151 0.6812478 0.772549" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-156.82 48.551", OffsetMax = "-31.18 78.853" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.4716981 0.4716981 0.4249733 0.8705882", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-201.34 18.249", OffsetMax = "13.34 48.551" }
            }, "EasyGatherAdmin", "panelRadius");

            container.Add(new CuiElement
            {
                Name = "Radius",
                Parent = "panelRadius",
                Components = {
                    new CuiNeedsKeyboardComponent(),
                    new CuiInputFieldComponent { Color = "1 1 1 0.9", FontSize = 16, Align = TextAnchor.MiddleCenter, IsPassword = false, Command = $"UI_ADMIN_TEXT radius", Text = addText.dist  },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112.336 -13.71", OffsetMax = "112.334 13.71" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "0.5351295 0.7264151 0.3871929 0.5686275", Command = $"UI_ADMIN_TEXT addText" },
                Text = { Text = "   ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-71.439 -41.01", OffsetMax = "143.239 -13.59" }
            }, "EasyGatherAdmin", "bnt_AddText");

            container.Add(new CuiElement
            {
                Name = "Text",
                Parent = "bnt_AddText",
                Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_AddText", player), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.2705882", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-107.341 -13.71", OffsetMax = "107.339 13.71" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Size",
                Parent = "EasyGatherAdmin",
                Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Admin_Size", player), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.7181212 0.764151 0.6812478 0.772549" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "82.68 48.551", OffsetMax = "208.32 78.853" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.4716981 0.4716981 0.4249733 0.8705882", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "38.16 18.249", OffsetMax = "252.84 48.551" }
            }, "EasyGatherAdmin", "panelSize");

            container.Add(new CuiElement
            {
                Name = "Size",
                Parent = "panelSize",
                Components = {
                    new CuiNeedsKeyboardComponent(),
                    new CuiInputFieldComponent { Color = "1 1 1 0.9", FontSize = 16, Align = TextAnchor.MiddleCenter, IsPassword = false, Command = $"UI_ADMIN_TEXT size", Text = addText.size  },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112.336 -13.71", OffsetMax = "112.334 13.71" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = "UI_ADMIN_SPAWN return" },
                Text = { Text = "<-", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter, Color = "1 0.01415092 0.01415092 0.45" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-392.606 181", OffsetMax = "-351.774 215.84" }
            }, "EasyGatherAdmin", "Return");

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = "UI_ADMIN_CLOSE" },
                Text = { Text = "X", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter, Color = "1 0.01415092 0.01415092 0.45" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "351.778 181", OffsetMax = "392.61 215.84" }
            }, "EasyGatherAdmin", "Exit");

            CuiHelper.DestroyUi(player, "EasyGatherAdmin");
            CuiHelper.AddUi(player, container);
        }

        private void CreateAdminUI_EditText(BasePlayer player)
        {
            if (!player.IsAdmin)
                return;

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.3584906 0.3584906 0.2790139 0.5529412", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-392.61 -215.836", OffsetMax = "392.61 215.836" }
            }, "Overlay", "EasyGatherAdmin");

            container.Add(new CuiElement
            {
                Name = "Header",
                Parent = "EasyGatherAdmin",
                Components = {
                    new CuiTextComponent { Text = String.Format(GetMsg("UI_Admin_Header", player), config.main.fileName, GetMsg("UI_Admin_EditText", player)), Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-381.799 181", OffsetMax = "340.349 215.84" }
                }
            });

            int i = 0;

            foreach (var text in textData)
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.4716981 0.4716981 0.4249733 0.8705882", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-383.877 {121.549 - i * 35}", OffsetMax = $"257.477 {151.851 - i * 35}" }
                }, "EasyGatherAdmin", "panelText");

                container.Add(new CuiElement
                {
                    Name = "Text",
                    Parent = "panelText",
                    Components = {
                    new CuiTextComponent { Text = text.text, Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleLeft, Color = "0.7181212 0.764151 0.6812478 0.772549" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-305.748 -15.151", OffsetMax = "311.784 15.151" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "0.6603774 0.3769135 0.3769135 0.5686275", Command = $"UI_ADMIN_TEXT remove {i}" },
                    Text = { Text = "   ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"262.079 {121.549 - i - i * 35}", OffsetMax = $"389.521 {151.851 - i * 35}" }
                }, "EasyGatherAdmin", "bnt_Remove");

                container.Add(new CuiElement
                {
                    Name = "Text",
                    Parent = "bnt_Remove",
                    Components = {
                    new CuiTextComponent { Text = "Remove", Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.2705882", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-63.721 -13.71", OffsetMax = "63.719 13.71" }
                }
                });

                i++;
            }

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = "UI_ADMIN_SPAWN return" },
                Text = { Text = "<-", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter, Color = "1 0.01415092 0.01415092 0.45" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-392.606 181", OffsetMax = "-351.774 215.84" }
            }, "EasyGatherAdmin", "Return");

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = "UI_ADMIN_CLOSE" },
                Text = { Text = "X", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter, Color = "1 0.01415092 0.01415092 0.45" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "351.778 181", OffsetMax = "392.61 215.84" }
            }, "EasyGatherAdmin", "Exit");

            CuiHelper.DestroyUi(player, "EasyGatherAdmin");
            CuiHelper.AddUi(player, container);
        }

        private void CreateButtons(BasePlayer player, bool isText = false)
        {
            if (!player.IsAdmin)
                return;

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.3584906 0.3584906 0.2790139 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-197.24 -273.229", OffsetMax = "179.912 -215.171" }
            }, "Hud", "ButtonsMain");

            container.Add(new CuiElement
            {
                Name = "Mouse",
                Parent = "ButtonsMain",
                Components = {
                    new CuiRawImageComponent { Color = "0.5377358 0.5199804 0.5199804 0.7450981", Url = "https://i.imgur.com/FmtPXI3.png" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-74.658 -29.029", OffsetMax = "-32.142 16.336" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.9433962 0.2892489 0.2892489 0.7803922" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-85.447 7.086", OffsetMax = "-61.621 8.587" }
            }, "ButtonsMain", "Line1");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.9433962 0.2892489 0.2892489 0.7803922" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-46.405 7.086", OffsetMax = "-22.579 8.587" }
            }, "ButtonsMain", "Line2");

            container.Add(new CuiElement
            {
                Name = "AddSpawn",
                Parent = "ButtonsMain",
                Components = {
                    new CuiTextComponent { Text = "Add Spawn", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.LowerCenter, Color = "1 1 1 0.5803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-151.056 0", OffsetMax = "-85.444 20.442" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Return",
                Parent = "ButtonsMain",
                Components = {
                    new CuiTextComponent { Text = "Return", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.LowerCenter, Color = "1 1 1 0.5803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-22.579 0", OffsetMax = "22.714 20.442" }
                }
            });

            if (!isText)
            {
                container.Add(new CuiElement
                {
                    Name = "btn_1",
                    Parent = "ButtonsMain",
                    Components = {
                    new CuiRawImageComponent { Color = "0.5377358 0.5199804 0.5199804 0.7450981", Url = "https://i.imgur.com/4NIVG10.png" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "48.842 -29.029", OffsetMax = "91.358 16.336" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "E",
                    Parent = "btn_1",
                    Components = {
                    new CuiTextComponent { Text = "E", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.9215686" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-21.257 -17.642", OffsetMax = "21.258 18.133" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "btn_2",
                    Parent = "ButtonsMain",
                    Components = {
                    new CuiRawImageComponent { Color = "0.5377358 0.5199804 0.5199804 0.7450981", Url = "https://i.imgur.com/XbU8aD0.png" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "98.542 -29.029", OffsetMax = "141.058 16.336" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "R",
                    Parent = "btn_2",
                    Components = {
                    new CuiTextComponent { Text = "R", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.9215686" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-21.257 -17.888", OffsetMax = "21.258 17.888" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "Rotate",
                    Parent = "ButtonsMain",
                    Components = {
                    new CuiTextComponent { Text = "Rotate", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.UpperCenter, Color = "1 1 1 0.5803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "48.842 10.055", OffsetMax = "144.272 29.029" }
                }
                });
            }

            CuiHelper.DestroyUi(player, "ButtonsMain");
            CuiHelper.AddUi(player, container);
        }

        private void Notice(BasePlayer player, string text)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = "Notice",
                Parent = "EasyGatherAdmin",
                Components = {
                    new CuiTextComponent { Text = GetMsg(text, player), Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "0.7181212 0.764151 0.6812478 0.772549" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-306.335 -215.838", OffsetMax = "308.029 -189.344" }
                }
            });

            CuiHelper.DestroyUi(player, "Notice");
            CuiHelper.AddUi(player, container);
        }

        private void Notice2(BasePlayer player, string text)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = "Notice",
                Parent = "EasyGatherAdmin",
                Components = {
                    new CuiTextComponent { Text = GetMsg(text, player), Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.7181212 0.764151 0.6812478 0.772549" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "71.987 37.283", OffsetMax = "312.752 78.104" }
                }
            });

            CuiHelper.DestroyUi(player, "Notice");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("UI_ADMIN_CLOSE")]
        private void cmd_UI_ADMIN_CLOSE(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            CuiHelper.DestroyUi(player, "EasyGatherAdmin");
        }

        [ConsoleCommand("UI_ADMIN_MAIN_ACTION")]
        private void cmd_UI_ADMIN_MAIN_ACTION(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            if (!player.IsAdmin)
                return;

            switch (arg.Args[0])
            {
                case "tp":
                    player.Teleport(BuildingSpawnPoint);
                    CuiHelper.DestroyUi(player, "EasyGatherAdmin");
                    break;

                case "ftime":
                    if (!playerData.ContainsKey(player.userID))
                        playerData.Add(player.userID, new PlayerData() { gatherTime = 0, lastPosition = Vector3.zero, lastTeleport = 0, topAmount = new Dictionary<string, int>(), topPoints = 0 });

                    playerData[player.userID].gatherTime += 3600;
                    Notice(player, "UI_Admin_Notice_FreeTime");
                    break;

                case "UpdateSpawns":
                    UpdateAllSpawns();
                    Notice(player, "UI_Admin_Notice_RefreshSpawn");
                    break;

                case "RemoveSpawns":
                    RemoveAllSpawns();
                    Notice(player, "UI_Admin_Notice_RemoveSpawn");
                    break;

                case "SetTimerText":
                    Spawns.TimerTextPosition = player.transform.position - BuildingSpawnPoint;
                    SaveTextData();
                    Notice(player, "UI_Admin_Notice_SetTimerText");
                    break;

                case "RemoveAllText":
                    RemoveAllTexts();
                    Notice(player, "UI_Admin_Notice_RemoveText");
                    break;

                case "SetPlayerSpawn":
                    Spawns.PlayerSpawnPosition = player.transform.position - BuildingSpawnPoint;
                    SaveTextData();
                    Notice(player, "UI_Admin_Notice_SettingsSetSpawn");
                    break;

                case "FullUpdate":
                    UpdateOrSpawnRoom(true);
                    Notice(player, "UI_Admin_Notice_SettingsFullUpdate");
                    break;

                case "ChangeName":
                    if (arg.Args.Length < 2)
                        return;
                    config.main.fileName = arg.Args[1];
                    SaveConfig();
                    CreateAdminUI(player);
                    break;

                case "AddSpawn":
                    CreateAdminUI_AddSpawn(player, "AddSpawn");
                    break;

                case "EditSpawn":
                    CreateAdminUI_AddSpawn(player, "editSpawn");
                    break;

                case "AddText":
                    addText.text = "Text...";
                    addText.dist = "1";
                    addText.size = "16";

                    CreateAdminUI_AddText(player);
                    break;

                case "EditText":
                    CreateAdminUI_EditText(player);
                    break;
            }
        }

        [ConsoleCommand("UI_ADMIN_SPAWN")]
        private void cmd_UI_ADMIN_SPAWN(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            if (!player.IsAdmin)
                return;

            if (arg.Args[0] == "return")
            {
                CreateAdminUI(player);
                return;
            }

            if (arg.Args[0] == "AddSpawn")
            {
                switch (arg.Args[1])
                {
                    case "Ore":
                    case "Animal":
                    case "Crate":
                    case "Barrel":
                        CreateAdminUI_AddSpawn(player, "AddSpawn", arg.Args[1]);
                        break;

                    case "show":
                        CreatePlaceManager(player, arg.Args[2]);
                        break;
                }
            }
            else
            {
                switch (arg.Args[1])
                {
                    case "Ore":
                    case "Animal":
                    case "Crate":
                    case "Barrel":
                        CreateAdminUI_AddSpawn(player, "editSpawn", arg.Args[1]);
                        break;

                    case "show":
                        CreateAdminUI_ShowSpawn(player, arg.Args[2]);
                        break;
                }
            }
        }

        [ConsoleCommand("UI_ADMIN_EDIT_SPAWN")]
        private void cmd_UI_ADMIN_EDIT_SPAWN(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            if (!player.IsAdmin)
                return;

            switch (arg.Args[0])
            {
                case "time":
                    if (arg.Args[2] == "")
                        return;

                    foreach (var c in arg.Args[2])
                        if (!char.IsNumber(c))
                            return;

                    config.spawn[arg.Args[1]] = arg.GetInt(2);
                    SaveConfig();
                    CreateAdminUI_ShowSpawn(player, arg.Args[1]);
                    break;

                case "removeid":
                    Notice2(player, RemoveSpawnID(player, arg.Args[1], arg.Args[2]));
                    break;

                case "remove":
                    RemoveSpawns(player, arg.Args[1]);
                    Notice2(player, "UI_Admin_Notice_RemoveSpawn_2");
                    break;

                case "show":
                    ShowSpawns(player, arg.Args[1]);
                    break;
            }
        }

        [ConsoleCommand("UI_ADMIN_TEXT")]
        private void cmd_UI_ADMIN_TEXT(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            if (!player.IsAdmin)
                return;

            switch(arg.Args[0])
            {
                case "remove":
                    var id = arg.GetInt(1);
                    textData.RemoveAt(id);
                    SaveTextData();
                    CreateAdminUI_EditText(player);
                    break;

                case "text":
                    if (arg.Args.Length < 2)
                    {
                        CreateAdminUI_AddText(player);
                        return;
                    }

                    string text = String.Join(" ", arg.Args.Skip(1));
                    addText.text = text;
                    CreateAdminUI_AddText(player);
                    break;

                case "radius":
                    if (arg.Args.Length < 2)
                    {
                        CreateAdminUI_AddText(player);
                        return;
                    }

                    foreach (var c in arg.Args[1])
                        if (!Char.IsNumber(c))
                        {
                            CreateAdminUI_AddText(player);
                            return;
                        }

                    addText.dist = arg.Args[1];
                    CreateAdminUI_AddText(player);
                    break;

                case "size":
                    CreateAdminUI_AddText(player);
                    if (arg.Args.Length < 2)
                    {
                        CreateAdminUI_AddText(player);
                        return;
                    }

                    foreach (var c in arg.Args[1])
                        if (!Char.IsNumber(c))
                        {
                            CreateAdminUI_AddText(player);
                            return;
                        }

                    addText.size = arg.Args[1];
                    CreateAdminUI_AddText(player);
                    break;

                case "addText":
                    CreatePlaceManager(player, "addText");
                    return;
            }
        }
        #endregion

        #region [PlayerUI]
        private void CreatePlayerUI(BasePlayer player)
        {
            if (gatherManager == null)
                return;

            var container = new CuiElementContainer();
            int i = 0;

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "1 1 1 0.25" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-392.611 -215.168", OffsetMax = "424.449 299.822" }
            }, "Overlay", "EasyGatherPlayer");

            #region [Top]
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.2901961 0.2862745 0.2352941 0.6509804" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-399.705 -248.29", OffsetMax = "-249.126 228.29" }
            }, "EasyGatherPlayer", "TopPanel");

            if (config.main.isTopActive)
            {
                i = 0;
                foreach (var tPlayer in playerData.OrderByDescending(p => p.Value.topPoints).Take(10))
                {
                    container.Add(new CuiPanel
                    {
                        CursorEnabled = true,
                        Image = { Color = "0.2901961 0.2862745 0.2352941 0" },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-63.642 {193.047 - i * 40}", OffsetMax = $"63.642 {226.353 - i * 40}" }
                    }, "TopPanel", "panel");

                    container.Add(new CuiElement
                    {
                        Name = "Avatar",
                        Parent = "panel",
                        Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = (string)ImageLibrary?.Call("GetImage",tPlayer.Key.ToString()) },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-62.046 -15.867", OffsetMax = "-30.407 15.797" }
                }
                    });

                    container.Add(new CuiElement
                    {
                        Name = "point",
                        Parent = "panel",
                        Components = {
                    new CuiTextComponent { Text = tPlayer.Value.topPoints.ToString(), Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.4901961" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-22.745 -10.475", OffsetMax = "55.345 10.405" }
                }
                    });

                    container.Add(new CuiButton
                    {
                        Button = { Color = "0 0 0 0", Command = $"UI_PLAYER_ACTION top {tPlayer.Key}" },
                        Text = { Text = "", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "0 0 0 0" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
                    }, "panel", "TopBtn");

                    i++;
                }

                container.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image = { Color = "0.2901961 0.2862745 0.2352941 0" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-63.642 -221.653", OffsetMax = "63.642 -188.347" }
                }, "TopPanel", "mypanel");

                container.Add(new CuiElement
                {
                    Name = "Avatar2",
                    Parent = "mypanel",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = (string)ImageLibrary?.Call("GetImage", player.UserIDString) },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-62.046 -15.867", OffsetMax = "-30.407 15.797" }
                }
                });

                if (playerData.ContainsKey(player.userID))
                {
                    container.Add(new CuiElement
                    {
                        Name = "point",
                        Parent = "mypanel",
                        Components = {
                    new CuiTextComponent { Text = playerData[player.userID].topPoints.ToString(), Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.4901961" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-22.745 -10.475", OffsetMax = "55.345 10.405" }
                    }
                    });

                    container.Add(new CuiButton
                    {
                        Button = { Color = "0 0 0 0", Command = $"UI_PLAYER_ACTION top {player.userID}" },
                        Text = { Text = "", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "0 0 0 0" },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
                    }, "mypanel", "TopBtn_My");
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Name = "point",
                        Parent = "mypanel",
                        Components = {
                    new CuiTextComponent { Text = "0", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.4901961" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-22.745 -10.475", OffsetMax = "55.345 10.405" }
                }
                    });
                }
            }
            else
            {
                container.Add(new CuiElement
                {
                    Name = "NoActive",
                    Parent = "TopPanel",
                    Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Player_TopNotActive", player), Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.4901961" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1"}
                    }
                });
            }

            #endregion

            #region [Main]
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.2901961 0.2862745 0.2352941 0.6509804" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-215.725 -248.29", OffsetMax = "108.392 228.29" }
            }, "EasyGatherPlayer", "MainPanel");

            container.Add(new CuiElement
            {
                Name = "BuildingImage",
                Parent = "MainPanel",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 0.7", Png = (string)ImageLibrary?.Call("GetImage","mainImage") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-136.198 0", OffsetMax = "136.198 212" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Players",
                Parent = "BuildingImage",
                Components = {
                    new CuiTextComponent { Text = String.Format(GetMsg("UI_Player_Players", player), gatherManager.players.Count, config.main.maxPlayers), Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.6588235" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-106.199 -106", OffsetMax = "106.201 -85.12" }
                }
            });


            container.Add(new CuiButton
            {
                Button = { Color = "0.5351295 0.7264151 0.3871929 0.5686275", Command = "UI_PLAYER_ACTION tp" },
                Text = { Text = "   ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-106.2 -89.51", OffsetMax = "106.2 -62.09" }
            }, "MainPanel", "bnt_Teleport");

            container.Add(new CuiElement
            {
                Name = "Text",
                Parent = "bnt_Teleport",
                Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Player_Teleport", player), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.2705882", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-107.341 -13.71", OffsetMax = "107.339 13.71" }
                }
            });
            if (gatherManager.players.Contains(player))
            {
                container.Add(new CuiButton
                {
                    Button = { Color = "0.6603774 0.3769135 0.3769135 0.5686275", Command = "UI_PLAYER_ACTION leave" },
                    Text = { Text = "   ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-106.201 -128.855", OffsetMax = "106.199 -99.385" }
                }, "MainPanel", "bnt_Remove");


                container.Add(new CuiElement
                {
                    Name = "Text",
                    Parent = "bnt_Remove",
                    Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Player_Leave", player), Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.2705882", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-106.2 -14.735", OffsetMax = "106.2 14.735" }
                }
                });
            }
            #endregion

            #region [Ticket]

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.2901961 0.2862745 0.2352941 0.6509804" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"135.941 -248.29", OffsetMax = "390.861 228.29" }
            }, "EasyGatherPlayer", "ChangePanel");

            i = 0;
            foreach (var ticket in config.tickets)
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image = { Color = "0.2901961 0.2862745 0.2352941 0" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-115.433 {192.227 - (i * 40.0)}", OffsetMax = $"117.633 {225.533 - (i * 40.0)}" }
                }, "ChangePanel", "panel");

                container.Add(new CuiElement
                {
                    Name = "Avatar3",
                    Parent = "panel",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = (string)ImageLibrary?.Call("GetImage",ticket.skinID.ToString()) },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-110.519 -15.867", OffsetMax = "-78.881 15.797" }
                }
                });

                var time = TimeSpan.FromSeconds(ticket.time);

                container.Add(new CuiElement
                {
                    Name = "time",
                    Parent = "panel",
                    Components = {
                    new CuiTextComponent { Text = String.Format(GetMsg("UI_Player_Time", player), time.Hours, time.Minutes, time.Seconds), Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.4901961" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-66.55 0", OffsetMax = "10.549 15.797" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "have",
                    Parent = "panel",
                    Components = {
                    new CuiTextComponent { Text = String.Format(GetMsg("UI_Player_Have", player), HaveTickets(player, ticket.skinID)), Font = "robotocondensed-bold.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.4901961" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-66.549 -15.867", OffsetMax = "10.549 -0.07" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "0.5351295 0.7264151 0.3871929 0.5686275", Command = $"UI_PLAYER_ACTION change {ticket.skinID}" },
                    Text = { Text = "   ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "14.53 -10.223", OffsetMax = "110.67 10.223" }
                }, "panel", "bnt_Change");

                container.Add(new CuiElement
                {
                    Name = "Text",
                    Parent = "bnt_Change",
                    Components = {
                    new CuiTextComponent { Text = GetMsg("UI_Player_Change", player), Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7803922" },
                    new CuiOutlineComponent { Color = "0 0 0 0.2705882", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-107.341 -13.71", OffsetMax = "107.339 13.71" }
                }
                });

                i++;
            }

            if (playerData.ContainsKey(player.userID))
            {
                var time = TimeSpan.FromSeconds(playerData[player.userID].gatherTime);
                container.Add(new CuiElement
                {
                    Name = "FarmTime",
                    Parent = "ChangePanel",
                    Components = {
                    new CuiTextComponent { Text = String.Format(GetMsg("UI_Player_FarmTime", player), time.Hours, time.Minutes, time.Seconds), Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.4901961" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-110.567 -227.326", OffsetMax = "117.633 -192.874" }
                }
                });
            }
            else
            {
                container.Add(new CuiElement
                {
                    Name = "FarmTime",
                    Parent = "ChangePanel",
                    Components = {
                    new CuiTextComponent { Text = String.Format(GetMsg("UI_Player_FarmTime", player), "0", "0", "0"), Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.4901961" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-110.567 -227.326", OffsetMax = "117.633 -192.874" }
                }
                });
            }

            #endregion

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = "UI_PLAYER_CLOSE" },
                Text = { Text = "✖", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "0.9811321 0 0 0.5333334" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "381.691 232.006", OffsetMax = "408.53 257.49" }
            }, "EasyGatherPlayer", "Exit");

            CuiHelper.DestroyUi(player, "EasyGatherPlayer");
            CuiHelper.AddUi(player, container);
        }

        private void CreateTopUI(BasePlayer player, ulong id)
        {
            if (!playerData.ContainsKey(id))
                return;


            var container = new CuiElementContainer();
            var data = playerData[id];
            int i = 0;

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.2901961 0.2862745 0.2352941 0.6509804" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-399.705 -248.29", OffsetMax = "-249.126 228.29" }
            }, "EasyGatherPlayer", "TopPanel");

            foreach (var loot in data.topAmount)
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image = { Color = "0.3773585 0.3772314 0.3755785 0.6509804" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-75.29 {189.283 - i * 28}", OffsetMax = $"75.29 {216.517 - i * 28}" }
                }, "TopPanel", "loot");

                container.Add(new CuiElement
                {
                    Name = "text",
                    Parent = "loot",
                    Components = {
                    new CuiTextComponent { Text = GetMsg(spawnSettings[loot.Key].langName, player), Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.4901961" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-72.425 -13.618", OffsetMax = "35.379 13.617" }
                }
                });

                container.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image = { Color = "0.6603774 0.6557049 0.5949627 0.6509804" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "41.866 -13.618", OffsetMax = "75.291 13.618" }
                }, "loot", "amount");

                container.Add(new CuiElement
                {
                    Name = "text (1)",
                    Parent = "amount",
                    Components = {
                    new CuiTextComponent { Text = loot.Value.ToString(), Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.4901961" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-16.712 -13.618", OffsetMax = "16.715 13.617" }
                }
                });

                i++;
            }

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = "UI_PLAYER_ACTION return" },
                Text = { Text = "«", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 0 0 0.4470588" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-75.29 216.516", OffsetMax = "75.29 238.284" }
            }, "TopPanel", "return");

            CuiHelper.DestroyUi(player, "TopPanel");
            CuiHelper.AddUi(player, container);
        }

        private void NoticePlayer(BasePlayer player, string text)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = "Notice",
                Parent = "MainPanel",
                Components = {
                    new CuiTextComponent { Text = text, Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "0.4786628 0.7924528 0.4074404 0.8235294" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-162.06 -238.29", OffsetMax = "162.06 -185.289" }
                }
            });

            CuiHelper.DestroyUi(player, "Notice");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("UI_PLAYER_CLOSE")]
        private void cmd_UI_PLAYER_CLOSE(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            CuiHelper.DestroyUi(player, "EasyGatherPlayer");
        }

        [ConsoleCommand("UI_PLAYER_ACTION")]
        private void cmd_UI_PLAYER_ACTION(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            var action = arg.Args[0];

            switch(action)
            {
                case "top":
                    CreateTopUI(player, arg.GetUInt64(1));
                    break;

                case "return":
                    CreatePlayerUI(player);
                    break;

                case "change":
                    var skin = arg.GetUInt64(1);
                    if(removeTicket(player, skin))
                    {
                        if (!playerData.ContainsKey(player.userID))
                            playerData.Add(player.userID, new PlayerData() { gatherTime = 0, lastPosition = Vector3.zero, lastTeleport = 0, topAmount = new Dictionary<string, int>(), topPoints = 0 });

                        var ticket = config.tickets.Where(t => t.skinID == skin).FirstOrDefault();
                        if (ticket == null)
                            return;

                        playerData[player.userID].gatherTime += ticket.time;
                        CreatePlayerUI(player);
                        return;
                    }

                    NoticePlayer(player, GetMsg("UI_Player_Notice_NotChanged", player));
                    break;

                case "leave":
                    if (!gatherManager.players.Contains(player) || !playerData.ContainsKey(player.userID))
                    {
                        NoticePlayer(player, GetMsg("UI_Player_Notice_NoGather", player));
                        return;
                    }

                    if(playerData[player.userID].lastPosition == Vector3.zero)
                    {
                        NoticePlayer(player, GetMsg("UI_Player_Notice_NoReturnPos", player));
                        return;
                    }

                    TeleportPlayerToPos(player, playerData[player.userID].lastPosition);
                    playerData[player.userID].lastPosition = Vector3.zero;
                    CuiHelper.DestroyUi(player, "EasyGatherPlayer");
                    break;

                case "tp":
                    if(gatherManager.players.Contains(player))
                    {
                        NoticePlayer(player, GetMsg("UI_Player_Notice_Already", player));
                        return;
                    }

                    if(!playerData.ContainsKey(player.userID) || playerData[player.userID].gatherTime <= 0)
                    {
                        NoticePlayer(player, GetMsg("UI_Player_Notice_NoTime", player));
                        return;
                    }

                    if(Facepunch.Math.Epoch.Current - playerData[player.userID].lastTeleport < config.main.teleportCoolDown)
                    {

                        var time = TimeSpan.FromSeconds(playerData[player.userID].lastTeleport + config.main.teleportCoolDown - Facepunch.Math.Epoch.Current);
                        NoticePlayer(player, String.Format(GetMsg("UI_Player_Notice_CoolDown", player), time.Hours, time.Minutes, time.Seconds));
                        return;
                    }

                    foreach(var item in player.inventory.containerMain.itemList)
                    {
                        if(config.blockedItems.Contains(item.info.shortname))
                        {
                            NoticePlayer(player, String.Format(GetMsg("UI_Player_Notice_BlockedItem", player), item.info.displayName.english));
                            return;
                        }
                    }

                    foreach (var item in player.inventory.containerBelt.itemList)
                    {
                        if (config.blockedItems.Contains(item.info.shortname))
                        {
                            NoticePlayer(player, String.Format(GetMsg("UI_Player_Notice_BlockedItem", player), item.info.displayName.english));
                            return;
                        }
                    }

                    foreach (var item in player.inventory.containerWear.itemList)
                    {
                        if (config.blockedItems.Contains(item.info.shortname))
                        {
                            NoticePlayer(player, String.Format(GetMsg("UI_Player_Notice_BlockedItem", player), item.info.displayName));
                            return;
                        }
                    }

                    playerData[player.userID].lastPosition = player.transform.position;
                    playerData[player.userID].lastTeleport = Facepunch.Math.Epoch.Current;
                    CuiHelper.DestroyUi(player, "EasyGatherPlayer");
                    TeleportPlayerToPos(player, BuildingSpawnPoint + Spawns.PlayerSpawnPosition);
                    break;
            }
        }
        #endregion
    }
}
