using Facepunch.Extend;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using Oxide.Game.Rust;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text;
using System;
using Time = UnityEngine.Time;
using UnityEngine;
using VLB;

namespace Oxide.Plugins
{
    [Info("CraftingPanel", "MTriper", "1.2.10")]
    [Description("A beautiful and convenient crafting panel, it's design and mechanics are similar to the in-game Rust crafting panel.")]
    class CraftingPanel : RustPlugin
    {
        #region Fields
        [PluginReference]
        Plugin ImageLibrary, ItemRetriever, Backpacks, Notify, GUIAnnouncements, SimpleStatus, AdvancedStatus, Economics, ServerRewards, IQEconomic, SkillTree, ZLevelsRemastered;
        CraftingPanelConfig config;
        Coroutine configCheck = null;
        Coroutine additionalLang = null;
        Dictionary<string, UserData> data;
        ItemBlueprint[] dlcItems;
        static CraftingPanel ins;
        const bool isRus = false;
        private bool readyToWork = false;
        #endregion

        #region Config
        class CraftingPanelConfig
        {
            [JsonProperty(isRus ? "Команда для открытия панели" : "Command to open the craft panel")]
            public string command = "craft";
            [JsonProperty(isRus ? "Разрешить масштабирование панели при изменении размера интерфейса?" : "Allow panel scaling when the interface is scaled?")]
            public bool canScale = false;
            [JsonProperty(isRus ? "Закрывать панель после начала крафта?" : "Close the panel after starting craft?")]
            public bool closeAfterCraft = false;
            [JsonProperty(isRus ? "Запоминать последний выбранный раздел и предмет?" : "Remember the last selected section and item?")]
            public bool saveLastItem = true;
            [JsonProperty(isRus ? "Включить многоязычный режим?" : "Enable multilingual mode?")]
            public bool multilingualMode= false;
            [JsonProperty(isRus ? "Разрешить работать с плагином Backpacks?" : "Allow work with the Backpacks plugin?")]
            public bool useBackpacks = true;
            [JsonProperty(isRus ? "Разрешить работать с плагином ItemRetriever?" : "Allow work with the ItemRetriever plugin?")]
            public bool useItemRetriever = true;
            [JsonProperty(isRus ? "Удалять данные игроков из Data, если они не заходили на сервер столько дней" : "Delete player data from the Data file if they have not logged into the server for so many days")]
            public int lastSeenDays = 182;
            [JsonProperty(isRus ? "Эффект при начале крафта [пусто - ничего]" : "Effect at the start of crafting [empty - nothing]")]
            public string effectStart = "assets/prefabs/misc/xmas/snowballgun/effects/reload_start.prefab";
            [JsonProperty(isRus ? "Эффект при выдаче предмета [пусто - ничего]" : "Effect at the end of crafting [empty - nothing]")]
            public string effectFinish = "assets/prefabs/misc/xmas/presents/effects/wrap.prefab";
            [JsonProperty(isRus ? "Эффект при отмене крафта [пусто - ничего]" : "Effect of canceling the craft [empty - nothing]")]
            public string effectCancel = "assets/prefabs/instruments/xylophone/effects/xylophone-deploy.prefab";
            [JsonProperty(isRus ? "Укажите плагин для работы с экономикой (ServerRewards, Economics, IQEconomic) [пусто - отключить]" : "Specify a plugin to work with the economy (ServerRewards, Economics, IQEconomic) [empty - disable]")]
            public string economyType = string.Empty;
            [JsonProperty(isRus ? "Настройка бонусов крафта" : "Crafting bonuses")]
            public CraftingBonuses craftingBonuses = new CraftingBonuses();
            [JsonProperty(isRus ? "Настройка уведомлений плагина" : "Plugin notifications")]
            public Notification notify = new Notification();
            [JsonProperty(isRus ? "Настройка игрового статуса" : "Game status")]
            public GameStatus status = new GameStatus();
            [JsonProperty(isRus ? "Разделы" : "Sections", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Section> sections = new List<Section> 
            {
                new Section 
                {
                    name = isRus ? "Избранное" : "Favorite",
                    permission = "favorite",
                    permissionIsActivate = true,
                    img = "https://i.postimg.cc/43jc0tWp/favorite.png"
                },
                new Section 
                {
                    name = isRus ? "Предметы" : "Items",
                    permission = "items",
                    img = "https://i.postimg.cc/kGbJ85TZ/items.png",
                    items = 
                    {
                        new SectionItem 
                        {
                            name = isRus ? "Нож для трофеев" : "Skinning Knife",
                            permission = "skinningknife",
                            description = isRus ? "Острый нож для снятия шкур. Чрезвычайно хорош в добыче мяса и может производить трофеи, которые можно установить на охотничьи трофеи." : "A sharp skinning knife. Extremely good at harvesting flesh and can produce trophies that can be mounted on Hunting Trophies.",
                            shortname = "knife.skinning",
                            properties = isRus ? "Урон   20\nСкорость   86\nРадиус атаки   0.3\nДальность   1.5\nДобыча мяса   18" : "Damage   20\nAttack Speed   86\nAttack Size   0.3\nRange   1.5\nFlesh Gather   18",
                            time = 5.2f,
                            ingredients = 
                            {
                                new Ingredient { name = isRus ? "Фрагменты металла" : "Metal fragments", shortname = "metal.fragments", amount = 75 }
                            }
                        },
                        new SectionItem 
                        {
                            name = isRus ? "Водолазный костюм бездны" : "Abyss Diver Suit",
                            permission = "hazmatdiver",
                            description = isRus ? "Водолазный костюм, найденный в бездне, также служит надежным антирадиационным костюмом." : "A divers suit found in the abyss, doubles as a reliable hazmat suit.",
                            shortname = "hazmatsuit.diver",
                            properties = isRus ? "Огнестрел   +30 %\nБлижний бой   +30 %\nУкус   +8 %\nРадиация   +50 %\nХолод   +8 %\nВзрыв   +5 %" : "Projectile   +30 %\nMelee   +30 %\nBite   +8 %\nRadiation   +50 %\nCold   +8 %\nExplosion   +5 %",
                            time = 0,
                            workbench = 2,
                            ingredients = 
                            {
                                new Ingredient { name = isRus ? "Ткань" : "Cloth", shortname = "cloth", amount = 20 },
                                new Ingredient { name = isRus ? "Топливо низкого качества" : "Low Grade Fuel", shortname = "lowgradefuel", amount = 5 }
                            }
                        }
                    }
                },
                new Section 
                {
                    name = isRus ? "Трансопрт" : "Vehicles",
                    permission = "vehicles",
                    img = "https://i.postimg.cc/HsrtYbkp/vehicles.png",
                    items = 
                    {
                        new SectionItem 
                        {
                            name = isRus ? "Миникоптер" : "Minicopter",
                            permission = "minicopter",
                            description = isRus ? "Миникоптер представляет собой небольшой вертолет, изготовленный из сломанных деталей." : "The Minicopter is a small helicopter made from scrapped parts.",
                            shortname = "box.wooden.large",
                            activPermission = true,
                            useDefaultName = false,
                            skinId = 2663605922,
                            time = 30,
                            workbench = 3,
                            economicPrice = 500,
                            ingredients = 
                            {
                                new Ingredient { name = isRus ? "Металлолом" : "Scrap", shortname = "scrap", amount = 300 },
                                new Ingredient { name = isRus ? "Фрагменты металла" : "Metal fragments", shortname = "metal.fragments", amount = 450 },
                                new Ingredient { name = isRus ? "Топливо низкого качества" : "Low Grade Fuel", shortname = "lowgradefuel", amount = 50 }
                            }
                        },
                        new SectionItem 
                        {
                            name = isRus ? "Снегоход" : "Snowmobile",
                            permission = "snowmobile",
                            description = isRus ? "Наземное транспортное средство, быстро передвигается по песку и снегу. Появляется на арктической лаборатории. Имеет небольшой инвентарь сзади." : "The snowmobile is a two-seat vehicle that spawns at the Arctic Research Base.",
                            img = "https://i.postimg.cc/VvVjGQVy/snowmobile.png",
                            workbench = 3,
                            time = 12,
                            command = "givesnowmobile %steamid% %amount%",
                            ingredients = 
                            {
                                new Ingredient { name = isRus ? "Металлолом" : "Scrap", shortname = "scrap", amount = 300 },
                                new Ingredient { name = isRus ? "Фрагменты металла" : "Metal fragments", shortname = "metal.fragments", amount = 450 },
                                new Ingredient { name = isRus ? "Топливо низкого качества" : "Low Grade Fuel", shortname = "lowgradefuel", amount = 50 }
                            }
                        }
                    }
                },
                new Section
                {
                    name = isRus ? "Лунный НГ" : "Lunar NY",
                    permission = "lunar",
                    img = "https://i.postimg.cc/Fz1hz3G4/lunarNY.png",
                    items = new List<SectionItem>
                    {
                        new SectionItem 
                        {
                            name = isRus ? "Небесный фонарик" : "Sky Lantern",
                            permission = "skylantern",
                            description = isRus ? "Красивый небесный фонарь. Может быть запущен в любом направлении. Содержит один слот. Может быть зажжен с помощью факела." : "A beautiful sky lantern. Can be launched in any direction. Has one inventory slot. Can be Ignited with a lit torch.",
                            shortname = "skylantern",
                            time = 5,
                            amount = 2,
                            maxAmount = 5,
                            itemSkins = 
                            {
                                new ItemSkin { name = isRus ? "ЗЕЛЕНЫЙ" : "GREEN", permission = "green", shortname = "skylantern.skylantern.green" },
                                new ItemSkin { name = isRus ? "ОРАНЖЕВЫЙ" : "ORANGE", permission = "orange", shortname = "skylantern.skylantern.orange" },
                                new ItemSkin { name = isRus ? "ФИОЛЕТОВЫЙ" : "PURPLE", activPermission = true, permission = "purple", shortname = "skylantern.skylantern.purple" },
                                new ItemSkin { name = isRus ? "КРАСНЫЙ" : "RED", permission = "red", shortname = "skylantern.skylantern.red" }
                            },
                            ingredients = new List<Ingredient> 
                            {
                                new Ingredient { name = isRus ? "Ткань" : "Cloth", shortname = "cloth", amount = 10 },
                                new Ingredient { name = isRus ? "Топливо низкого качества" : "Low Grade Fuel", shortname = "lowgradefuel", amount = 5 }
                            }
                        },
                        new SectionItem 
                        {
                            name = isRus ? "Новогодний гонг" : "New Year Gong",
                            permission = "newyeargong",
                            description = isRus ? "Встречайте Лунный Новый Год с массивным гонгом!" : "Ring in the Lunar new year with a massive gong!",
                            activPermission = true,
                            shortname = "newyeargong",
                            time = 5,
                            maxAmount = 3,
                            workbench = 1,
                            ingredients = new List<Ingredient> 
                            {
                                new Ingredient { name = isRus ? "Дерево" : "Wood", shortname = "wood", amount = 100 },
                                new Ingredient { name = isRus ? "Фрагменты металла" : "Metal fragments", shortname = "metal.fragments", amount = 50 }
                            }
                        }
                    }
                }
            };
        }

        class Section
        {
            [JsonIgnore]
            public string jsonReadyItems;
            [JsonProperty(isRus ? "Название раздела" : "Section name")]
            public string name;
            [JsonProperty(isRus ? "Включить раздел?" : "Enable this section?")]
            public bool isEnable = true;
            [JsonProperty(isRus ? "Permission раздела [обязательно]" : "Section permission [required]")]
            public string permission;
            [JsonProperty(isRus ? "Регистрировать эту permission?" : "Register this permission?")]
            public bool permissionIsActivate;
            [JsonProperty(isRus ? "Иконка раздела" : "Section icon")]
            public string img;
            [JsonProperty(isRus ? "Предметы раздела" : "Section items")]
            public List<SectionItem> items = new List<SectionItem>();
        }

        class SectionItem
        {
            [JsonIgnore]
            public string parentSection = string.Empty;
            [JsonProperty(isRus ? "Название предмета [обязательно]" : "Item name [required]")]
            public string name = string.Empty;
            [JsonProperty(isRus ? "Включить крафт этого предмета?" : "Enable crafting of this item?")]
            public bool isEnable = true;
            [JsonProperty(isRus ? "Permission предмета [обязательно]" : "Item permission [required]")]
            public string permission = string.Empty;
            [JsonProperty(isRus ? "Регистрировать эту permission?" : "Register this permission?")]
            public bool activPermission = false;
            [JsonProperty(isRus ? "Описание предмета" : "Item description")]
            public string description = string.Empty;
            [JsonProperty(isRus ? "Параметры предмета" : "Item properties")]
            public string properties = string.Empty;
            [JsonProperty(isRus ? "Shortname предмета" : "Item shortname")]
            public string shortname = string.Empty;
            [JsonProperty(isRus ? "SkinID предмета" : "Item skinId")]
            public ulong skinId = 0;
            [JsonProperty(isRus ? "Это чертеж?" : "Is this a blueprint? ")]
            public bool isBlueprint = false;
            [JsonProperty(isRus ? "Создавать предмет со стандартным названием (true) или указанным сверху (false)?" : "Create an item with the default name (true) or the above name (false)?")]
            public bool useDefaultName = true;
            [JsonProperty(isRus ? "Изображение предмета [необязательно]" : "Item image [optional]")]
            public string img = string.Empty;
            [JsonProperty(isRus ? "Консольные команды выполняемые после крафта (%steamid%, %username%, %amount%) [необязательно]" : "Console commands executed after crafting (%steamid%, %username%, %amount%) [optional]")]
            public string command = string.Empty;
            [JsonProperty(isRus ? "Выдаваемое количество при крафте" : "Amount per craft")]
            public int amount = 1;
            [JsonProperty(isRus ? "Максимальный множитель крафта" : "Max craft multiplier")]
            public int maxAmount = 1;
            [JsonProperty(isRus ? "Время крафта [сек]" : "Crafting time [sec]")]
            public float time = 5f;
            [JsonProperty(isRus ? "Показывать уведомление в статус баре (если они включены)?" : "Show notification in game status (if they are enabled)?")]
            public bool showStatus = true;
            [JsonProperty(isRus ? "Применять бонусы крафта (если они включены)?" : "Apply craft bonuses (if they are enabled)?")]
            public bool canUseBonuses = true;
            [JsonProperty(isRus ? "Уровень верстака [0 - не нужен]" : "Workbench level [0 - not needed]")]
            public int workbench = 0;
            [JsonProperty(isRus ? "Стоимость крафта в плагине экономики [0 - отключено]" : "Crafting cost in the economy plugin [0 - disable]")]
            public int economicPrice = 0;
            [JsonProperty(isRus ? "Исполнения предмета" : "Item variations")]
            public List<ItemSkin> itemSkins = new List<ItemSkin>();
            [JsonProperty(isRus ? "Ингредиенты для крафта" : "Crafting ingredients")]
            public List<Ingredient> ingredients = new List<Ingredient>();
        }

        class Ingredient
        {
            [JsonProperty(isRus ? "Название ресурса [обязательно]" : "Resource name [required]")]
            public string name;
            [JsonProperty(isRus ? "Использовать этот ресурс в ингредиентах?" : "Use this resource in ingredients?")]
            public bool isEnable = true;
            [JsonProperty(isRus ? "Shortname ресурса [обязательно]" : "Resource shortname [required]")]
            public string shortname;
            [JsonProperty(isRus ? "SkinID ресурса" : "Resource skinId")]
            public ulong skinId = 0;
            [JsonProperty(isRus ? "Необходимое количество для крафта" : "Required quantity for crafting")]
            public int amount = 1;
            [JsonProperty(isRus ? "Возвращать предмет со стандартным названием (true) или указанным сверху (false)?" : "Return an item with the default name (true) or the above name (false)?")]
            public bool useDefaultName = true;
        }

        class ItemSkin
        {
            [JsonProperty(isRus ? "Название предмета [обязательно]" : "Item name [required]")]
            public string name = string.Empty;
            [JsonProperty(isRus ? "Включить крафт этого предмета?" : "Enable crafting of this item?")]
            public bool isEnable = true;
            [JsonProperty(isRus ? "Permission предмета [обязательно]" : "Item permission [required]")]
            public string permission = string.Empty;
            [JsonProperty(isRus ? "Регистрировать эту permission?" : "Register this permission?")]
            public bool activPermission = false;
            [JsonProperty(isRus ? "Shortname предмета" : "Item shortname")]
            public string shortname = string.Empty;
            [JsonProperty(isRus ? "SkinID предмета" : "Item skinId")]
            public ulong skinId = 0;
            [JsonProperty(isRus ? "Это чертеж?" : "Is this a blueprint? ")]
            public bool isBlueprint = false;
            [JsonProperty(isRus ? "Создавать предмет со стандартным названием (true) или указанным сверху (false)?" : "Create an item with the default name (true) or the above name (false)?")]
            public bool useDefaultName = true;
            [JsonProperty(isRus ? "Изображение предмета [необязательно]" : "Item image [optional]")]
            public string img = string.Empty; 
            [JsonProperty(isRus ? "Консольные команды выполняемые после крафта (%steamid%, %username%, %amount%) [необязательно]" : "Console commands executed after crafting (%steamid%, %username%, %amount%) [optional]")]
            public string command = string.Empty;
        }

        class CraftingBonuses
        {
            [JsonProperty(isRus ? "Включить бонусы крафта?" : "Enable crafting bonuses?")]
            public bool isEnable = false;
            [JsonProperty(isRus ? "Укажите плагин для работы с бонусами (SkillTree, ZLevelsRemastered)" : "Specify a plugin to work with bonuses (SkillTree, ZLevelsRemastered)")]
            public string type = "SkillTree";
            [JsonProperty(isRus ? "Сколько выдавать опыта за крафт" : "Amount of experience given for crafting")]
            public float addXP = 0.25f;
            [JsonProperty(isRus ? "Сколько забирать опыта за отмену крафта" : "Amount of experience taken away for canceling the craft")]
            public float removeXP = 0.05f;
            [JsonProperty(isRus ? "Размер опыта будет зависеть от времени крафта?" : "Amount of experience will depend on crafting time?")]
            public bool basedOnTime = true;
            [JsonProperty(isRus ? "Иконка для сообщений в чате (из конфига используемого плагина) [0 - стандартная]" : "Icon for chat messages (from config of plugin used) [0 - default]")]
            public ulong chatId = 76561199514393612;
            [JsonProperty(isRus ? "SkillTree: использовать бафф Craft_Speed?" : "SkillTree: use Craft_Speed buff?")]
            public bool STCanUseCraftSpeed = true;
            [JsonProperty(isRus ? "SkillTree: использовать бафф Craft_Refund?" : "SkillTree: use Craft_Refund buff?")]
            public bool STCanUseCraftRefund = true;
            [JsonProperty(isRus ? "SkillTree: использовать бафф Craft_Duplicate?" : "SkillTree: use Craft_Duplicate buff?")]
            public bool STCanUseCraftDuplicate = true;
            [JsonProperty(isRus ? "ZLevelsRemastered: использовать бафф к скорости крафта?" : "ZLevelsRemastered: use the crafting speed buff?")]
            public bool ZLRCanUseCraftSpeed = true;
            [JsonProperty(isRus ? "ZLevelsRemastered: значение из конфига - Plugin Prefix" : "ZLevelsRemastered: config value - Plugin Prefix")]
            public string ZLRPluginPrefix = "<color=orange>ZLevels</color>: ";
            [JsonProperty(isRus ? "ZLevelsRemastered: значение из конфига - Skill Colors -> CRAFTING" : "ZLevelsRemastered: config value - Skill Colors -> CRAFTING")]
            public string ZLRSkillColor = "#00FF00";
            [JsonProperty(isRus ? "ZLevelsRemastered: значение из конфига - Crafting Details -> Percent Faster Per Level" : "ZLevelsRemastered: config value - Crafting Details -> Percent Faster Per Level")]
            public float ZLRPercentPerLevel = 5f;
            [JsonProperty(isRus ? "ZLevelsRemastered: значение из конфига - Level Caps -> CRAFTING" : "ZLevelsRemastered: config value - Level Caps -> CRAFTING")]
            public float ZLRLevelCup = 20f;
            [JsonProperty(isRus ? "ZLevelsRemastered: значение из конфига - Starting Stats -> Crafting Points" : "ZLevelsRemastered: config value - Starting Stats -> Crafting Points")]
            public float ZLRStartPoint = 10f;
        }

        class Notification
        {
            [JsonProperty(isRus ? "Включить уведомления?" : "Enable plugin notifications?")]
            public bool isEnable = true;
            [JsonProperty(isRus ? "Тип уведомления (Chat, GameTips, Notify, GUIAnnouncements)" : "Notification type (Chat, GameTips, Notify, GUIAnnouncements)")]
            public string type = "GameTips";
            [JsonProperty(isRus ? "Закрывать панель, чтобы показать уведомление?" : "Close the panel to show a notification?")]
            public bool closePanel = true;
            [JsonProperty(isRus ? "GameTips: тип уведомления (info, warning)" : "GameTips: notification type (info, warning)")]
            public string GameTipsType = "warning";
            [JsonProperty(isRus ? "Notify: тип уведомления" : "Notify: notification type")]
            public int NotifyType = 0;
            [JsonProperty(isRus ? "GUIAnnouncements: цвет банера" : "GUIAnnouncements: banner color")]
            public string GUIABanerColor = "Grey";
            [JsonProperty(isRus ? "GUIAnnouncements: цвет текста" : "GUIAnnouncements: text color")]
            public string GUIATextColor = "White";
            [JsonProperty(isRus ? "GUIAnnouncements: отступ сверху" : "GUIAnnouncements: vertical position")]
            public float GUIAMarginTop = 0.03f;
        }

        class GameStatus
        {
            [JsonProperty(isRus ? "Включить отображение игрового статуса?" : "Enable game status?")]
            public bool isEnable = true;
            [JsonProperty(isRus ? "Тип игрового статуса (Rust, SimpleStatus, AdvancedStatus)" : "Type of game status (Rust, SimpleStatus, AdvancedStatus)")]
            public string type = "Rust";
        }

        protected override void LoadDefaultConfig()
        {
            config = new CraftingPanelConfig();
            SaveConfig();
        }

        void ReadConfig()
        {
            config = Config.ReadObject<CraftingPanelConfig>();
            SaveConfig(); 
        }
        
        protected override void SaveConfig() => Config.WriteObject(config, true);

        IEnumerator CheckConfig(Action endAction)
        {
            Puts("The plugin has started checking the config ...");

            if (config.command == "") 
            {
                PrintError("No command to open the panel!");
                NextTick(() => Interface.Oxide.UnloadPlugin(Title));
                yield break;
            }
            else cmd.AddChatCommand(config.command, this, "OpenPanelCommand");

            if (config.useBackpacks && (Backpacks == null || !Backpacks.IsLoaded || ins.Backpacks.Author != "WhiteThunder"))
            {
                PrintWarning("The \"Backpacks\" plugin by WhiteThunder is not loaded. The plugin will not be able to take crafting ingredients from the optional backpack.");
                config.useBackpacks = false;
            }

            if (config.useItemRetriever && (ItemRetriever == null || !ItemRetriever.IsLoaded))
            {
                PrintWarning("The \"ItemRetriever\" plugin is not loaded. The plugin will not be able to take crafting ingredients from the suppliers containers.");
                config.useItemRetriever = false;
            }

            if (config.economyType != "")
            {
                switch (config.economyType)
                {
                    case "ServerRewards":
                        if (ServerRewards == null || !ServerRewards.IsLoaded)
                        {
                            PrintWarning("The \"ServerRewards\" plugin is not loaded. Economy will be disabled.");
                            config.economyType = "";
                        }
                        break;
                    case "Economics":
                        if (Economics == null || !Economics.IsLoaded)
                        {
                            PrintWarning("The \"Economics\" plugin is not loaded. Economy will be disabled.");
                            config.economyType = "";
                        }
                        break;
                    case "IQEconomic":
                        if (IQEconomic == null || !IQEconomic.IsLoaded)
                        {
                            PrintWarning("The \"IQEconomic\" plugin is not loaded. Economy will be disabled.");
                            config.economyType = "";
                        }
                        break;
                    default:
                        PrintWarning("Incorrect economy plugin is specified. Economy will be disabled.");
                        config.economyType = "";
                        break;
                }
            }

            if (config.craftingBonuses.isEnable)
            {
                switch (config.craftingBonuses.type)
                {
                    case "SkillTree":
                        if (SkillTree == null || !SkillTree.IsLoaded)
                        {
                            PrintError("SkillTree plugin is not loaded! Crafting bonuses will be disabled.");
                            config.craftingBonuses.isEnable = false;
                        }
                        break;
                    case "ZLevelsRemastered":
                        if (ZLevelsRemastered == null || !ZLevelsRemastered.IsLoaded)
                        {
                            PrintError("ZLevelsRemastered plugin is not loaded! Crafting bonuses will be disabled.");
                            config.craftingBonuses.isEnable = false;
                        }
                        break;
                    default:
                        PrintError("Incorrect plugin for working with crafting bonuses! Crafting bonuses will be disabled.");
                        config.craftingBonuses.isEnable  = false;
                        break;
                }
            }

            if (config.notify.isEnable)
            {
                switch (config.notify.type)
                {
                    case "Chat":
                        break;
                    case "GameTips":
                        if (config.notify.GameTipsType != "info" && config.notify.GameTipsType != "warning")
                        {
                            PrintError("GameTips: incorrect notification type! Notifications will be disabled.");
                            config.notify.isEnable = false;
                        }
                        break;
                    case "Notify":
                        if (Notify == null || !Notify.IsLoaded)
                        {
                            PrintError("Notify: plugin is not loaded! Notifications will be disabled.");
                            config.notify.isEnable = false;
                        }
                        break;
                    case "GUIAnnouncements":
                        if (GUIAnnouncements == null || !GUIAnnouncements.IsLoaded)
                        {
                            PrintError("GUIAnnouncements: plugin is not loaded! Notifications will be disabled.");
                            config.notify.isEnable = false;
                        }
                        break;
                    default:
                        PrintError("Incorrect notification type! Notifications will be disabled.");
                        config.notify.isEnable = false;
                        break;
                }
            }

            if (config.status.isEnable)
            {
                switch (config.status.type)
                {
                    case "Rust":
                        break;
                    case "SimpleStatus":
                        if (SimpleStatus == null || !SimpleStatus.IsLoaded)
                        {
                            PrintError("SimpleStatus plugin is not loaded! Game status will be disabled.");
                            config.status.isEnable = false;
                        }
                        else
                        {
                            SimpleStatus.Call("CreateStatus", this, "CraftingPanel.Craft", "0.11 0.41 0.6 1", "Title", "0.84 0.94 0.99 1", "Text", "0.67 0.87 1 1", "assets/icons/gear.png", "0.26 0.61 0.84 1");
                            SimpleStatus.Call("CreateStatus", this, "CraftingPanel.Give", "0.34 0.4 0.26 1", "Title", "1 1 1 1", "Text", "1 1 1 1", "assets/icons/picked up.png", "0.65 1 0.02 1");
                            //SimpleStatus.Call("CreateStatus", this, "CraftingPanel.Drop", "0.42 0.13 0.09 1", "Title", "0.82 0.49 0.44 1", "Text", "0.82 0.49 0.44 1", "assets/icons/close.png", "1 0.24 0.1 1");
                        }
                        break;
                    case "AdvancedStatus":
                        if (AdvancedStatus == null || !AdvancedStatus.IsLoaded)
                        {
                            PrintError("AdvancedStatus plugin is not loaded! Game status will be disabled.");
                            config.status.isEnable = false;
                        }
                        break;
                    default:
                        PrintError("Incorrect game status type! Game status will be disabled.");
                        config.status.isEnable = false;
                        break;
                } 
            }

            for (int i = config.sections.Count - 1; i > -1; i--)
            {
                yield return CoroutineEx.waitForSeconds(0.25f);

                if (!config.sections[i].isEnable)
                {
                    if (config.sections[i].permission == "favorite")
                    {
                        Unsubscribe("OnUserPermissionRevoked");
                        Unsubscribe("OnGroupPermissionRevoked");
                        Unsubscribe("OnUserGroupRemoved");
                    }

                    config.sections.RemoveAt(i);
                    continue;
                }

                if (config.sections[i].permission == "")
                {
                    PrintError("One of the sections has no permission set.");
                    NextTick(() => Interface.Oxide.UnloadPlugin(Title));
                    yield break;
                }
                else if (config.sections[i].permission == "favorite") config.sections[i].items.Clear();

                if (config.sections.Count(x => x.permission == config.sections[i].permission) > 1)
                {
                    PrintError($"Several sections have the same permission - \"{config.sections[i].permission}\".");
                    NextTick(() => Interface.Oxide.UnloadPlugin(Title));
                    yield break;
                }

                if (config.sections[i].permissionIsActivate) permission.RegisterPermission($"craftingpanel.section.{config.sections[i].permission}", this);
                ImageLibrary.Call("AddImage", config.sections[i].img, $"CraftingPanel.Section.{config.sections[i].permission}");

                for (int j = config.sections[i].items.Count - 1; j > -1; j--)
                {
                    //yield return CoroutineEx.waitForFixedUpdate;

                    if (!config.sections[i].items[j].isEnable)
                    {
                        config.sections[i].items.RemoveAt(j);
                        continue;
                    }

                    if (config.sections[i].items[j].name == "")
                    {
                        PrintError($"You need to specify name for the item \"{config.sections[i].items[j].permission}\" in the \"{config.sections[i].permission}\" section.");
                        NextTick(() => Interface.Oxide.UnloadPlugin(Title));
                        yield break;
                    }

                    if (config.sections[i].items[j].permission == "")
                    {
                        PrintError($"One of the items in the section \"{config.sections[i].permission})\" has no permission set.");
                        NextTick(() => Interface.Oxide.UnloadPlugin(Title));
                        yield break;
                    }

                    if (config.sections[i].items.Count(x => x.permission == config.sections[i].items[j].permission) > 1)
                    {
                        PrintError($"Several items have the same permission - \"{config.sections[i].items[j].permission}\" in the \"{config.sections[i].permission}\" section.");
                        NextTick(() => Interface.Oxide.UnloadPlugin(Title));
                        yield break;
                    }

                    if (config.sections[i].items[j].shortname == "" && config.sections[i].items[j].command == "")
                    {
                        PrintError($"You need to specify a shortname or command for the item \"{config.sections[i].items[j].permission}\" in the \"{config.sections[i].permission}\" section.");
                        NextTick(() => Interface.Oxide.UnloadPlugin(Title));
                        yield break;
                    }

                    config.sections[i].items[j].parentSection = config.sections[i].permission;
                    if (config.sections[i].items[j].activPermission) permission.RegisterPermission($"craftingpanel.{config.sections[i].permission}.{config.sections[i].items[j].permission}", this);
                    if (config.sections[i].items[j].img != "") ImageLibrary.Call("AddImage", config.sections[i].items[j].img, $"CraftingPanel.{config.sections[i].permission}.{config.sections[i].items[j].permission}");

                    for (int m = config.sections[i].items[j].itemSkins.Count - 1; m > -1; m--)
                    {
                        if (!config.sections[i].items[j].itemSkins[m].isEnable)
                        {
                            config.sections[i].items[j].itemSkins.RemoveAt(m);
                            continue;
                        }

                        if (config.sections[i].items[j].itemSkins[m].permission == "")
                        {
                            PrintError($"One of the variations ({config.sections[i].permission}/{config.sections[i].items[j].permission}/*your variation*) has no permission set.");
                            NextTick(() => Interface.Oxide.UnloadPlugin(Title));
                            yield break;
                        }

                        if (config.sections[i].items[j].itemSkins.Count(x => x.permission == config.sections[i].items[j].itemSkins[m].permission) > 1)
                        {
                            PrintError($"Several variations of items have the same permission - \"{config.sections[i].items[j].itemSkins[m].permission}\" ({config.sections[i].permission}/{config.sections[i].items[j].permission}/{config.sections[i].items[j].itemSkins[m].permission}).");
                            NextTick(() => Interface.Oxide.UnloadPlugin(Title));
                            yield break;
                        }

                        if (config.sections[i].items[j].itemSkins[m].shortname == "" && config.sections[i].items[j].itemSkins[m].command == "")
                        {
                            PrintError($"You need to specify a shortname or command for the variation ({config.sections[i].permission}/{config.sections[i].items[j].permission}/{config.sections[i].items[j].itemSkins[m].permission}).");
                            NextTick(() => Interface.Oxide.UnloadPlugin(Title));
                            yield break;
                        }
                        
                        if (config.sections[i].items[j].itemSkins[m].activPermission) permission.RegisterPermission($"craftingpanel.{config.sections[i].permission}.{config.sections[i].items[j].permission}.{config.sections[i].items[j].itemSkins[m].permission}", this);
                        if (config.sections[i].items[j].itemSkins[m].img != "") ImageLibrary.Call<string>("AddImage",config.sections[i].items[j].itemSkins[m].img, $"CraftingPanel.{config.sections[i].permission}.{config.sections[i].items[j].permission}.{config.sections[i].items[j].itemSkins[m].permission}");
                    }

                    if (config.sections[i].items[j].itemSkins.Count > 0) config.sections[i].items[j].itemSkins.Insert(0, new ItemSkin { permission = "default", shortname = config.sections[i].items[j].shortname, skinId = config.sections[i].items[j].skinId, isBlueprint = config.sections[i].items[j].isBlueprint });

                    for (int k = config.sections[i].items[j].ingredients.Count - 1; k > -1; k--)
                    {
                        if (!config.sections[i].items[j].ingredients[k].isEnable)
                        {
                            config.sections[i].items[j].ingredients.RemoveAt(k);
                            continue;
                        }
 
                        if (config.sections[i].items[j].ingredients[k].name == "")
                        {
                            PrintError($"You need to specify ingredients name of the item \"{config.sections[i].items[j].permission}\" in the \"{config.sections[i].permission}\" section.");
                            NextTick(() => Interface.Oxide.UnloadPlugin(Title));
                            yield break;
                        }

                        if (config.sections[i].items[j].ingredients[k].shortname == "")
                        {
                            PrintError($"You need to specify ingredients shortname of the item \"{config.sections[i].items[j].permission}\" in the \"{config.sections[i].permission}\" section.");
                            NextTick(() => Interface.Oxide.UnloadPlugin(Title));
                            yield break;
                        } 
                    }

                    if (config.sections[i].items[j].ingredients.Count == 0 && config.sections[i].items[j].economicPrice == 0)
                    {
                        PrintWarning($"All ingredients of the \"{config.sections[i].items[j].permission}\" item are disabled. Therefore, this item will also be disabled.");
                        config.sections[i].items.RemoveAt(j);
                    }
                }

                if (config.sections[i].items.Count == 0 && config.sections[i].permission != "favorite")
                {
                    PrintWarning($"All elements of the \"{config.sections[i].permission}\" section are disabled. Therefore, this section will also be disabled.");
                    config.sections.RemoveAt(i);
                    continue;
                }

                CuiElementContainer container = new CuiElementContainer();
                int a = 5; // Начало по x 
                int b = -89; // Начало по y (размер кнопки 84)
                int c = 89; // Промежуток по x
                int d = 89; // Промежуток по y
                int scrollboxSize = Mathf.CeilToInt(config.sections[i].items.Count / 4f) * d + 5;
                if (scrollboxSize < 475) scrollboxSize = 475;

                container.Add(new CuiPanel { Image = { Color = "0 0 0 0" } }, "CraftingPanel.ItemsBox", "CraftingPanel.InvisibleItemsBox", "CraftingPanel.InvisibleItemsBox");

                container.Add(new CuiElement
                {
                    Name = "CraftingPanel.ItemsBoxScrollbar",
                    Parent = "CraftingPanel.InvisibleItemsBox",
                    Components = 
                    {
                        new CuiRectTransformComponent { AnchorMin = "0 0.001", AnchorMax = "0.995 0.9948" },
                        new CuiScrollViewComponent 
                        {
                            ContentTransform = new CuiRectTransform { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{scrollboxSize}", OffsetMax = "0 0" },
                            Horizontal = false,
                            Vertical = true,
                            MovementType = UnityEngine.UI.ScrollRect.MovementType.Elastic,
                            Elasticity = 0.25f,
                            Inertia = true,
                            DecelerationRate = 0.3f,
                            ScrollSensitivity = 24f,
                            VerticalScrollbar = null
                        }
                    }
                });

                container.Add(new CuiPanel { Image = { Color = "0 0 0 0" } }, "CraftingPanel.ItemsBoxScrollbar");

                for (int l = 0; l < config.sections[i].items.Count; l++)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{a} {b}", OffsetMax = $"{a+84} {b+84}" },
                        Button = { Command = $"CraftingPanelSortingCommand items {config.sections[i].permission} {config.sections[i].items[l].permission} {config.sections[i].items[0].permission} {config.sections[i].items[l].parentSection}", Color = l == 0 ? "0.18 0.66 1 0.35" : "0 0 0 0" }
                    }, "CraftingPanel.ItemsBoxScrollbar", $"CraftingPanel.ItemsBoxScrollbar.Item.{config.sections[i].items[l].permission}");

                    if (config.sections[i].items[l].img != "")
                    {
                        container.Add(new CuiElement
                        {
                            Parent = $"CraftingPanel.ItemsBoxScrollbar.Item.{config.sections[i].items[l].permission}",
                            Components = 
                            {
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                                new CuiRawImageComponent { Png = ImageLibrary.Call<string>("GetImage", $"CraftingPanel.{config.sections[i].items[l].parentSection}.{config.sections[i].items[l].permission}") }
                            }
                        });
                    }
                    else
                    {
                        if (config.sections[i].items[l].isBlueprint)
                        {
                            container.Add(new CuiPanel
                            {
                                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                                Image = { ItemId = ItemManager.FindItemDefinition("blueprintbase").itemid }
                            }, $"CraftingPanel.ItemsBoxScrollbar.Item.{config.sections[i].items[l].permission}");
                        }

                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                            Image = { ItemId = ItemManager.FindItemDefinition(config.sections[i].items[l].shortname).itemid, SkinId = config.sections[i].items[l].skinId }
                        }, $"CraftingPanel.ItemsBoxScrollbar.Item.{config.sections[i].items[l].permission}");
                    }

                    if ((l + 1) % 4 == 0) { a = 5; b -= d; }
                    else a += c;
                }

                config.sections[i].jsonReadyItems = CuiHelper.ToJson(container);
            }

            if (config.sections.Count == 0)
            {
                PrintError("All sections are disabled!");
                NextTick(() => Interface.Oxide.UnloadPlugin(Title));
                yield break;
            }

            Puts("The config has been successfully checked.");
            endAction?.Invoke();
            configCheck = null;
        }
        #endregion

        #region Lang
        IEnumerator LoadCustomLang()
        {
            CraftingPanelConfig rawConfig = Config.ReadObject<CraftingPanelConfig>();

            Dictionary<string, string> rus = new Dictionary<string, string>
            {
                ["SEARCH"] = "Поиск...",
                ["SEARCHING"] = "ИДЕТ ПОИСК ...",
                ["SEARCH_EMPTY"] = "НЕ НАЙДЕНО",
                ["CRAFTING_QUEUE"] = "ОЧЕРЕДЬ СОЗДАНИЯ",
                ["BACK"] = "НАЗАД",
                ["NEXT"] = "ВПЕРЕД",
                ["FAVORITE"] = "ИЗБРАННОЕ",
                ["PERMISSION"] = "ПРИВИЛЕГИЯ",
                ["NO_PERMISSION"] = "НЕТ\nПРИВИЛЕГИИ",
                ["INFORMATION"] = "ИНФОРМАЦИЯ",
                ["VARIATIONS"] = "ИСПОЛНЕНИЯ",
                ["AMOUNT"] = "НЕОБХОДИМО",
                ["ITEM_TYPE"] = "НАЗВАНИЕ ПРЕДМЕТА",
                ["TOTAL"] = "ВСЕГО",
                ["HAVE"] = "ИМЕЕТСЯ",
                ["CRAFT"] = "СОЗДАТЬ",
                ["BUY"] = "КУПИТЬ",
                ["ECONOMY"] = "БАЛАНС:\nСТОИМОСТЬ:",
                ["CURRENCY"] = "{0} $\n{1} $",
                ["CRAFT_ADMIN"] = "ВЫДАТЬ СЕБЕ",
                ["WB_TIER"] = "ТРЕБУЕТСЯ ВЕРСТАК {0}-ГО УРОВНЯ",
                ["UNAVAILABLE"] = "ОТСУТСТВУЮТ",
                ["TIMER"] = "{0}с",
                ["SS_AMOUNT"] = "{0} шт",
                //["QUEUE_BUSY"] = "Вся очередь крафта занята!",
                ["WB_LEVEL"] = "Недостаточный уровень верстака!",
                ["NO_PERM"] = "Отсутствует необходимая привилегия!",
                ["NO_INGREDIENT"] = "Недостаточно ингредиентов для крафта!",
                ["NO_MONEY"] = "На балансе недостаточно средств для покупки!",
                ["PERM_REVOKE"] = "С вас снята привилегия на раздел '{0}'. Предметы этого раздела удалены из '{1}'.",
                ["COMMAND_INVALID"] = "Команда указана неверно.\nВведите:",
                ["COMMAND_PLAYER_ERROR"] = "Игрок не найден.\nВведите:",
                ["COMMAND_SECTION_ERROR"] = "Раздел не найден.\nВведите:",
                ["COMMAND_ITEM_ERROR"] = "Предмет не найден.\nВведите:",
                ["COMMAND_NUMBER_ERROR"] = "Кол-во указано неверно.\nВведите:",
                ["COMMAND_VARIATION_ERROR"] = "Исполнение предемта указано неверно.\nВведите:",
                ["COMMAND_PLUGIN_ERROR"] = "У этого игрока не работает панель крафта! Пожалуйста сообщите разработчику об этой ошибке."
            };
            
            Dictionary<string, string> eng = new Dictionary<string, string>
            {
                ["SEARCH"] = "Search...",
                ["SEARCHING"] = "SEARCHING ...",
                ["SEARCH_EMPTY"] = "NOT FOUND",
                ["CRAFTING_QUEUE"] = "CRAFTING QUEUE",
                ["BACK"] = "BACK",
                ["NEXT"] = "NEXT",
                ["FAVORITE"] = "FAVORITE",
                ["PERMISSION"] = "PERMISSION",
                ["NO_PERMISSION"] = "NO\nPERMISSION",
                ["INFORMATION"] = "INFORMATION",
                ["VARIATIONS"] = "VARIATIONS",
                ["AMOUNT"] = "AMOUNT",
                ["ITEM_TYPE"] = "ITEM TYPE",
                ["TOTAL"] = "TOTAL",
                ["HAVE"] = "HAVE",
                ["CRAFT"] = "CRAFT",
                ["BUY"] = "PURCHASE",
                ["ECONOMY"] = "BALANCE:\nCOST:",
                ["CURRENCY"] = "{0} $\n{1} $",
                ["CRAFT_ADMIN"] = "GIVE YOURSELF",
                ["WB_TIER"] = "WORKBENCH LEVEL {0} REQUIRED",
                ["UNAVAILABLE"] = "UNAVAILABLE",
                ["TIMER"] = "{0}s",
                ["SS_AMOUNT"] = "{0} pcs",
                //["QUEUE_BUSY"] = "The craft queue is busy!",
                ["WB_LEVEL"] = "Insufficient workbench level!",
                ["NO_PERM"] = "Missing a necessary permission!",
                ["NO_INGREDIENT"] = "Not enough ingredients for crafting!",
                ["NO_MONEY"] = "Not enough funds in the balance to purchase!",
                ["PERM_REVOKE"] = "Your permission for the '{0}' section has been revoked. Items in this section have been removed from '{1}'.",
                ["COMMAND_INVALID"] = "Invalid command.\nUse:",
                ["COMMAND_PLAYER_ERROR"] = "Player not found.\nUse:",
                ["COMMAND_SECTION_ERROR"] = "Section not found.\nUse:",
                ["COMMAND_ITEM_ERROR"] = "Item not found.\nUse:",
                ["COMMAND_NUMBER_ERROR"] = "Incorrect number.\nUse:",
                ["COMMAND_VARIATION_ERROR"] = "Variation not found.\nUse:",
                ["COMMAND_PLUGIN_ERROR"] = "This player does not have a working crafting panel. Please inform the plugin developer about it."
            };

            if (config.multilingualMode)
            {
                Puts("The plugin has started loading additional language phrases ...");

                Dictionary<string, string> additional = new Dictionary<string, string>();
                Dictionary<string, string> ingredients = new Dictionary<string, string>();

                foreach (var section in rawConfig.sections) 
                {
                    yield return CoroutineEx.waitForSeconds(0.25f);

                    additional.TryAdd($"section.{section.permission}", section.name);

                    foreach (var item in section.items)
                    {
                        additional.TryAdd($"{section.permission}.{item.permission}.name", item.name);
                        if (item.description != "") additional.TryAdd($"{section.permission}.{item.permission}.description", item.description);
                        if (item.properties != "") additional.TryAdd($"{section.permission}.{item.permission}.properties", item.properties);
                        foreach (var skin in item.itemSkins) additional.TryAdd($"{section.permission}.{item.permission}.variations.{skin.permission}", skin.name);
                        foreach (var ingredient in item.ingredients) ingredients.TryAdd($"{ingredient.shortname}.{ingredient.skinId}", ingredient.name);
                    }
                }
                
                foreach (var ingredient in ingredients)
                {
                    rus.TryAdd(ingredient.Key, ingredient.Value);
                    eng.TryAdd(ingredient.Key, ingredient.Value);
                }

                foreach (var item in additional)
                {
                    rus.TryAdd(item.Key, item.Value);
                    eng.TryAdd(item.Key, item.Value);
                }

                Puts("Additional language phrases have been successfully added.");
            }

            lang.RegisterMessages(rus, this, "ru");
            lang.RegisterMessages(eng, this);
            readyToWork = true;
            additionalLang = null;
        }

        string GetMessage(string langKey, string userID) => lang.GetMessage(langKey, this, userID);
        string GetMessage(string langKey, string userID, params object[] args) => string.Format(lang.GetMessage(langKey, this, userID), args);
        #endregion

        #region Data
        class UserData
        {
            public string lastSeenTime = "";
            public bool dlcUnlocked = false;
            public HashSet<string> favoriteItems = new HashSet<string>();
        }

        void LoadData()
        {
            try { data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, UserData>>(Title); }
            catch  { data = new Dictionary<string, UserData>(); }

            for (int i = data.Count - 1; i > -1; i--)
            {
                DateTime dt;
                if (DateTime.TryParse(data.ElementAt(i).Value.lastSeenTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                {
                    if ((DateTime.Now - dt).Days > config.lastSeenDays) data.Remove(data.ElementAt(i).Key);
                }
                else PrintError($"Date format not recognized for player \"{data.ElementAt(i).Key}\". Example date: 01/19/2023 (month/day/year).");
            }
        }

        void SaveData()
        {
            if (data != null) Interface.Oxide.DataFileSystem.WriteObject(Title, data);
        }
        #endregion

        #region Hooks
        private void OnServerInitialized()
        {
            if (ImageLibrary == null || !ImageLibrary.IsLoaded)
            {
                PrintError("The \"ImageLibrary\" plugin is missing!");
                NextTick(() => Interface.Oxide.UnloadPlugin(Title));
                return;
            }

            permission.RegisterPermission("craftingpanel._use", this);        // Открытие панели
            permission.RegisterPermission("craftingpanel._admin", this);      // Кнопка "Выдать себе", команды для выдачи предметов
            permission.RegisterPermission("craftingpanel._instant", this);    // Моментальный крафт
            permission.RegisterPermission("craftingpanel._death", this);      // Не отменять очередь крафта при смерти игрока
            permission.RegisterPermission("craftingpanel._disconnect", this); // Не отменять очередь крафта при дисконнекте
            permission.RegisterPermission("craftingpanel._economics", this);  // Покапука предметов за очки плагинов экономики
            permission.RegisterPermission("craftingpanel._bonuses", this);    // Применение бонусов крафта от поддерживамых плагинов
            permission.RegisterPermission("craftingpanel._workbench", this);  // Уменьшение времени крафта из-за текущего уовня верстака
            permission.RegisterPermission("craftingpanel._unlockdlc", this);  // Разблокирует крафт dlc предметов через внутреигровую панель крафта

            ReadConfig();
            LoadData();
            ins = this;
            dlcItems = ItemManager.bpList.Where(x => x.userCraftable && x.NeedsSteamDLC).ToArray();

            configCheck = ServerMgr.Instance.StartCoroutine(CheckConfig(new Action(() => 
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList) OnPlayerConnected(player);
                additionalLang = ServerMgr.Instance.StartCoroutine(LoadCustomLang());
            })));

            AddCovalenceCommand("givecraft", "GiveCraftItem");

            ImageLibrary.Call("AddImage", "https://i.postimg.cc/NGkKvXL5/clock.png", "CraftingPanel.Clock");
            ImageLibrary.Call("AddImage", "https://i.postimg.cc/rmH64Qwf/amount.png", "CraftingPanel.Amount");
            ImageLibrary.Call("AddImage", "https://i.postimg.cc/25kLzJjS/cross.png", "CraftingPanel.Cross");
            ImageLibrary.Call("AddImage", "https://i.postimg.cc/x138XRqp/check.png", "CraftingPanel.Check");
        }

        void Unload() 
        {
            if (configCheck != null) 
            {
                ServerMgr.Instance.StopCoroutine(configCheck);
                configCheck = null;
            }
            
            if (additionalLang != null) 
            {
                ServerMgr.Instance.StopCoroutine(additionalLang);
                additionalLang = null;
            }

            foreach (BasePlayer player in BasePlayer.activePlayerList) OnPlayerDisconnected(player, "PluginUnload");
            SaveData();
        }
        
        void OnServerSave() => SaveData();

        void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || player.IsBot || !player.userID.IsSteamId()) return;
            CraftingQueueController cqc = player.GetOrAddComponent<CraftingQueueController>();
            cqc.favoriteItems.Clear();
            cqc.foundItems.Clear();
            UserData userData;

            if (data.TryGetValue(player.UserIDString, out userData))
            {
                if (userData.dlcUnlocked && !permission.UserHasPermission(player.UserIDString, "craftingpanel._unlockdlc")) UnlockDLCItems(player, false);
                if (!userData.dlcUnlocked && permission.UserHasPermission(player.UserIDString, "craftingpanel._unlockdlc")) UnlockDLCItems(player, true);

                for (int i = userData.favoriteItems.Count - 1; i > -1; i--)
                {
                    string[] itemArgs = userData.favoriteItems.ElementAtOrDefault(i)?.Split(".");
                    if (itemArgs.IsNullOrEmpty()) continue;

                    Section foundSection = config.sections.FirstOrDefault(x => x.permission == itemArgs[0]);
                    if (foundSection != null) 
                    {
                        if (foundSection.permissionIsActivate && !permission.UserHasPermission(player.UserIDString, $"craftingpanel.section.{itemArgs[0]}")) 
                        {
                           userData.favoriteItems.RemoveWhere(x => x.StartsWith(itemArgs[0]));
                           continue;
                        }
                    }
                    else continue;

                    SectionItem sectionItem = foundSection.items.FirstOrDefault(x => x.permission == itemArgs[1]);
                    if (sectionItem == null) continue;

                    cqc.favoriteItems.Insert(0, sectionItem);
                }
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason) 
        {
            ClosePanel(player);
            CraftingQueueController component;
            if (player.TryGetComponent(out component)) 
            {
                if (reason == "PluginUnload") 
                {
                    component.CancelAll();
                    UnityEngine.Object.DestroyImmediate(component);
                }
                else
                {
                    if (!permission.UserHasPermission(player.UserIDString, "craftingpanel._disconnect") || component.queue.Count == 0) 
                    {
                        component.CancelAll();
                        UnityEngine.Object.DestroyImmediate(component);
                    }
                }
            }

            if (data.ContainsKey(player.UserIDString) && data[player.UserIDString].favoriteItems.Count != 0) data[player.UserIDString].lastSeenTime = DateTime.Now.ToString("d", CultureInfo.InvariantCulture);
            else data.Remove(player.UserIDString);
        }

        void OnPlayerDeath(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, "craftingpanel._death"))
            {
                CraftingQueueController component;
                if (player.TryGetComponent(out component)) component.CancelAll();
            }

            ClosePanel(player);
        }

        void OnGroupPermissionGranted(string name, string perm)
        {
            if (perm == "craftingpanel._unlockdlc") 
            {
                foreach (string user in permission.GetUsersInGroup(name))
                {
                    BasePlayer player = RustCore.FindPlayerByIdString(user.Split(" ").First());
                    UnlockDLCItems(player, true);
                }
            }
        }

        void OnUserPermissionGranted(string id, string permName)
        {
            if (permName == "craftingpanel._unlockdlc") 
            {
                BasePlayer player = RustCore.FindPlayerByIdString(id);
                UnlockDLCItems(player, true);
            }
        }
        
        void OnUserPermissionRevoked(string id, string permName)
        {
            if (permName.StartsWith("craftingpanel")) 
            {
                BasePlayer player = RustCore.FindPlayerByIdString(id);
                if (permName == "craftingpanel._unlockdlc") UnlockDLCItems(player, false);
                else RemoveFavoriteItem(player, permName);
            }
        }

        void OnGroupPermissionRevoked(string name, string perm)
        {
            if (perm.StartsWith("craftingpanel"))
            {
                foreach (string user in permission.GetUsersInGroup(name)) 
                {
                    BasePlayer player = RustCore.FindPlayerByIdString(user.Split(" ").First());
                    if (perm == "craftingpanel._unlockdlc") UnlockDLCItems(player, false);
                    else RemoveFavoriteItem(player, perm);
                }
            }
        }

        void OnUserGroupRemoved(string id, string groupName)
        {
            BasePlayer player = RustCore.FindPlayerByIdString(id);
            if (player == null) return;
            foreach (var perm in permission.GetGroupPermissions(groupName).Where(x => x.StartsWith("craftingpanel"))) 
            {
                if (perm == "craftingpanel._unlockdlc") UnlockDLCItems(player, false);
                else RemoveFavoriteItem(player, perm);
            }
        }
        #endregion

        #region Commands
        void OpenPanelCommand(BasePlayer player)
        {
            if (player != null && readyToWork && permission.UserHasPermission(player.UserIDString, "craftingpanel._use") && player.CanInteract()) 
            {
                CraftingQueueController cqc;
                if(player.TryGetComponent(out cqc)) 
                {
                    if (!cqc.isPanelOpen)
                    {
                        if (config.saveLastItem && cqc.currentSection != null && cqc.currentSection.permissionIsActivate && !permission.UserHasPermission(player.UserIDString, $"craftingpanel.section.{cqc.currentSection.permission}"))
                        {
                            cqc.currentSection = null;
                            cqc.currentItem = null;
                            cqc.currentSkin = null;
                        }

                        cqc.isPanelOpen = true;
                        cqc.MainPanel();
                    }
                    else ClosePanel(player);
                }
            }           
        }

        void GiveCraftItem(Core.Libraries.Covalence.IPlayer iPlayer, string command, string[] args)
        {
            BasePlayer player = iPlayer.Object as BasePlayer;
            bool playerIsNull = player == null;
            if (!playerIsNull && !permission.UserHasPermission(player.UserIDString,"craftingpanel._admin")) return;
            
            if (args.Length != 5) 
            {
                if (playerIsNull) PrintWarning("Invalid command. Use: givecraft steamid/nickname section item 5 skin");
                else SendReply(player, GetMessage("COMMAND_INVALID", player.UserIDString) + " givecraft steamid/nickname section item 5 skin");
                return;
            }

            BasePlayer foundPlayer = BasePlayer.FindAwakeOrSleeping(args[0]);
            if (foundPlayer == null)
            {
                if (playerIsNull) PrintWarning("Player not found. Use: givecraft steamid/nickname section item 5 skin");
                else SendReply(player, GetMessage("COMMAND_PLAYER_ERROR", player.UserIDString) + " givecraft steamid/nickname section item 5 skin");
                return;
            }

            Section section = config.sections.FirstOrDefault(x => x.permission == args[1]);
            if (section == null) 
            {
                if (playerIsNull) PrintWarning("Section not found. Use: givecraft steamid/nickname section item 5 skin");
                else SendReply(player, GetMessage("COMMAND_SECTION_ERROR", player.UserIDString) + " givecraft steamid/nickname section item 5 skin");
                return;
            }

            SectionItem sectionItem = section?.items.FirstOrDefault(x => x.permission == args[2]);
            if (sectionItem == null)
            {
                if (playerIsNull) PrintWarning("Item not found. Use: givecraft steamid/nickname section item 5 skin");
                else SendReply(player, GetMessage("COMMAND_ITEM_ERROR", player.UserIDString) + " givecraft steamid/nickname section item 5 skin");
                return;
            }

            int amount;
            if (!int.TryParse(args[3], out amount))
            {
                if (playerIsNull) PrintWarning("Incorrect number. Use: givecraft steamid/nickname section item 5 skin");
                else SendReply(player, GetMessage("COMMAND_NUMBER_ERROR", player.UserIDString) + " givecraft steamid/nickname section item 5 skin");
                return;
            }

            ItemSkin itemSkin = sectionItem.itemSkins.FirstOrDefault(x => x.permission == args[4]);
            if (itemSkin == null && args[4] != "default")
            {
                if (playerIsNull) PrintWarning("Variation not found. Use: givecraft steamid/nickname section item 5 skin");
                else SendReply(player, GetMessage("COMMAND_VARIATION_ERROR", player.UserIDString) + " givecraft steamid/nickname section item 5 skin");
                return;
            }

            CraftingQueueController cqc;
            if (!foundPlayer.TryGetComponent(out cqc))
            {
                if (playerIsNull) PrintError("This player does not have a working crafting panel. Please inform the plugin developer about it.");
                else SendReply(player, GetMessage("COMMAND_PLUGIN_ERROR", player.UserIDString));
                return;
            }

            if (args[4] == "default") 
            {
                cqc.FinishCrafting(new CraftingQueueController.QueueItem
                {
                    fullTime = 0,
                    command = sectionItem.command,
                    name = config.multilingualMode ? GetMessage($"{sectionItem.parentSection}.{sectionItem.permission}.name", player.UserIDString) : sectionItem.name,
                    useDefaultName = sectionItem.useDefaultName,
                    shortname = sectionItem.shortname,
                    skinId = sectionItem.skinId,
                    showStatus = sectionItem.showStatus,
                    canUseBonuses = sectionItem.canUseBonuses,
                    isBlueprint = sectionItem.isBlueprint,
                    amount = sectionItem.amount,
                    remainingMultiplier = amount,
                    ingredients = Array.Empty<Ingredient>()
                }, true);
            }
            else
            {
                string name;
                if (!config.multilingualMode) name = $"{sectionItem.name} ({itemSkin.name})";
                else name = string.Format("{0} ({1})", GetMessage($"{sectionItem.parentSection}.{sectionItem.permission}.name", player.UserIDString), GetMessage($"{sectionItem.parentSection}.{sectionItem.permission}.variations.{itemSkin.permission}", player.UserIDString));

                cqc.FinishCrafting(new CraftingQueueController.QueueItem
                {
                    fullTime = 0,
                    command = itemSkin.command,
                    name = name,
                    useDefaultName = itemSkin.useDefaultName,
                    shortname = itemSkin.shortname,
                    skinId = itemSkin.skinId,
                    isBlueprint = itemSkin.isBlueprint,
                    showStatus = sectionItem.showStatus,
                    canUseBonuses = sectionItem.canUseBonuses,
                    amount = sectionItem.amount,
                    remainingMultiplier = amount,
                    ingredients = Array.Empty<Ingredient>()
                }, true);
            }
        }

        [ConsoleCommand("CraftingPanelSortingCommand")]
        void CraftingPanelSortingCommand(ConsoleSystem.Arg args)
        {
            BasePlayer player = args?.Player();
            if (player == null) return;
            CraftingQueueController cqc;
            if(!player.TryGetComponent(out cqc)) return;

            switch (args.Args[0])
            {
                case "menu": // 1 - текущий раздел меню
                    cqc.ButtonsSection(config.sections.First(x => x.permission == args.Args[1]));
                    break;
                case "items": // 1 - родительский раздел элемента, 2 - активный элемент
                    cqc.ItemsSection(config.sections.First(x => x.permission == args.Args[1]).items.First(x => x.permission == args.Args[2]));
                    break;
                case "skins": // 1 - активный скин
                    cqc.SkinSection(config.sections.First(x => x.permission == cqc.currentItem.parentSection).items.First(x => x.permission == cqc.currentItem.permission).itemSkins.First(x => x.permission == args.Args[1]), true);
                    break;
                case "multiplier": // 1 - тип, 2,3,4... - аргументы
                    if (args.Args[1] == "change") cqc.IngredientsSection(int.Parse(args.Args[2]));
                    else if (args.Args[1] == "max") cqc.IngredientsSection(GetMaxItemAmount(player, cqc.currentItem.parentSection, cqc.currentItem.permission));
                    else if (args.Args[1] == "input") 
                    {
                        int multiplier;
                        if (args.Args.Length == 3 && int.TryParse(args.Args[2], out multiplier))
                        {
                            int maxMultiplier = cqc.currentItem.maxAmount;
                            if (multiplier < 1) multiplier = 1;
                            else if (multiplier > maxMultiplier) multiplier = maxMultiplier;
                        }
                        else multiplier = 1;
                        cqc.IngredientsSection(multiplier);
                    }
                    break;
                case "craft": // 1 - множитель, 2 - тип оплаты
                    cqc.AddToQueue(int.Parse(args.Args[1]), cqc.currentSkin?.permission ?? "default", args.Args[2] == "economics");
                    if (cqc.isPanelOpen) cqc.IngredientsSection(int.Parse(args.Args[1]));
                    break;
                case "cancelcraft": // 1 - taskId
                    cqc.CancelCraft(int.Parse(args.Args[1]));
                    break;
                case "favorite":
                    cqc.AddOrRemoveFavoriteItem();
                    break;
                case "search": // 1,2,3,... - фразы для поиска
                    if(args.Args.Length >= 2) cqc.StartItemsSearch(string.Join(" ", args.Args.Skip(1)));
                    break;
                case "givecraft": // 1 - множитель
                    GiveCraftItem(player.IPlayer, "givecraft", new string[] { player.UserIDString, cqc.currentItem.parentSection, cqc.currentItem.permission, args.Args[1], cqc.currentSkin?.permission ?? "default" });
                    break;
                case "close":
                    ClosePanel(player);
                    break;
            }
        }
        #endregion

        #region Core
        class CraftingQueueController : MonoBehaviour
        {
            private BasePlayer player;
            private int taskId = 0;
            internal bool isPanelOpen = false;
            internal bool isSearchActive = false;
            internal Coroutine searchCoroutine;
            internal List<QueueItem> queue = new List<QueueItem>();
            internal List<SectionItem> favoriteItems = new List<SectionItem>();
            internal List<SectionItem> foundItems = new List<SectionItem>();
            internal Section currentSection = null;
            internal SectionItem currentItem = null;
            internal ItemSkin currentSkin = null;

            internal enum NotifyType { Start, Cancel, Finish }

            internal class QueueItem
            {
                internal int taskId;
                internal float endTime;
                internal float fullTime;
                internal float crafingTime;
                internal string img;
                internal string command;
                internal string name;
                internal bool useDefaultName;
                internal string shortname;
                internal ulong skinId;
                internal bool isBlueprint;
                internal bool showStatus;
                internal bool canUseBonuses;
                internal int amount;
                internal int  remainingMultiplier;
                internal int economicPrice;
                internal Ingredient[] ingredients;
            }

            void Awake() => player = GetComponent<BasePlayer>();

            void OnDestroy() 
            {
                if (searchCoroutine != null) ServerMgr.Instance.StopCoroutine(searchCoroutine);
            }

            internal void AddToQueue(int multiplier, string activeSkin, bool isEconomics = false)
            {
                if (!HasNeededWorkbench(currentItem.workbench))
                {
                    SendNotify(ins.GetMessage("WB_LEVEL", player.UserIDString));
                    return;
                }

                if (currentItem.activPermission && !ins.permission.UserHasPermission(player.UserIDString, $"craftingpanel.{currentItem.parentSection}.{currentItem.permission}"))
                {
                    SendNotify(ins.GetMessage("NO_PERM", player.UserIDString));
                    return;
                }

                ItemSkin itemSkin = currentItem.itemSkins.FirstOrDefault(x => x.permission == activeSkin);

                if (activeSkin != "default" && itemSkin.activPermission && !ins.permission.UserHasPermission(player.UserIDString, $"craftingpanel.{currentItem.parentSection}.{currentItem.permission}.{itemSkin.permission}"))
                {
                    SendNotify(ins.GetMessage("NO_PERM", player.UserIDString));
                    return;
                }
  
                if (isEconomics)
                {
                    if (!ins.permission.UserHasPermission(player.UserIDString, "craftingpanel._economics"))
                    {
                        SendNotify(ins.GetMessage("NO_PERM", player.UserIDString));
                        return;
                    }

                    if (!EconomicItemPurchase(currentItem.economicPrice * multiplier))
                    {
                        SendNotify(ins.GetMessage("NO_MONEY", player.UserIDString));
                        return;
                    }
                }
                else
                {
                    List<ItemDefinition> ingredientsCache = new List<ItemDefinition>(currentItem.ingredients.Count);

                    foreach (var ingredient in currentItem.ingredients)
                    {
                        ingredientsCache.Add(ItemManager.FindItemDefinition(ingredient.shortname));

                        if (ins.GetItemAmount(player, ingredient.shortname, ingredient.skinId) < ingredient.amount * multiplier)
                        {
                            SendNotify(ins.GetMessage("NO_INGREDIENT", player.UserIDString));
                            return;
                        }
                    }

                    for (int i = 0; i < currentItem.ingredients.Count; i++) TakeItem(ingredientsCache[i], currentItem.ingredients[i].amount * multiplier, currentItem.ingredients[i].skinId);
                }

                float crafingTime = ins.GetWorkbenchTime(player, currentItem.time, currentItem.workbench);
                float bonusTime = ins.BonusHandler(player, "GetCraftTime", currentItem.canUseBonuses, crafingTime);
                if (bonusTime > 0) crafingTime = bonusTime;

                if (crafingTime == 0 || ins.permission.UserHasPermission(player.UserIDString, "craftingpanel._instant"))
                {
                    if (activeSkin == "default")
                    {
                        FinishCrafting(new QueueItem
                        {
                            fullTime = currentItem.time,
                            command = currentItem.command,
                            name = ins.config.multilingualMode ? ins.GetMessage($"{currentItem.parentSection}.{currentItem.permission}.name", player.UserIDString) : currentItem.name,
                            useDefaultName = currentItem.useDefaultName,
                            shortname = currentItem.shortname,
                            skinId = currentItem.skinId,
                            isBlueprint = currentItem.isBlueprint,
                            showStatus = currentItem.showStatus,
                            canUseBonuses = currentItem.canUseBonuses,
                            amount = currentItem.amount,
                            remainingMultiplier = multiplier,
                            ingredients = isEconomics ? Array.Empty<Ingredient>() : currentItem.ingredients.ToArray()
                        }, true);
                    }
                    else
                    {
                        string name;
                        if (!ins.config.multilingualMode) name = $"{currentItem.name} ({itemSkin.name})";
                        else name = string.Format("{0} ({1})", ins.GetMessage($"{currentItem.parentSection}.{currentItem.permission}.name", player.UserIDString), ins.GetMessage($"{currentItem.parentSection}.{currentItem.permission}.variations.{itemSkin.permission}", player.UserIDString));

                        FinishCrafting(new QueueItem
                        {
                            fullTime = currentItem.time,
                            command = itemSkin.command,
                            name = name,
                            useDefaultName = itemSkin.useDefaultName,
                            shortname = itemSkin.shortname,
                            skinId = itemSkin.skinId,
                            isBlueprint = itemSkin.isBlueprint,
                            showStatus = currentItem.showStatus,
                            canUseBonuses = currentItem.canUseBonuses,
                            amount = currentItem.amount,
                            remainingMultiplier = multiplier,
                            ingredients = isEconomics ? Array.Empty<Ingredient>() : currentItem.ingredients.ToArray()
                        }, true);
                    }
                    return;
                }

                PlayEffect(NotifyType.Start);
                taskId++;

                if (activeSkin == "default")
                {
                    queue.Add(new QueueItem
                    {
                        taskId = taskId,
                        endTime = 0f,
                        fullTime = currentItem.time,
                        crafingTime = crafingTime,
                        img = currentItem.img == "" ? "" : $"CraftingPanel.{currentItem.parentSection}.{currentItem.permission}",
                        command = currentItem.command,
                        name = ins.config.multilingualMode ? ins.GetMessage($"{currentItem.parentSection}.{currentItem.permission}.name", player.UserIDString) : currentItem.name,
                        useDefaultName = currentItem.useDefaultName,
                        shortname = currentItem.shortname,
                        skinId = currentItem.skinId,
                        isBlueprint = currentItem.isBlueprint,
                        showStatus = currentItem.showStatus,
                        canUseBonuses = currentItem.canUseBonuses,
                        amount = currentItem.amount,
                        remainingMultiplier = multiplier,
                        economicPrice = currentItem.economicPrice,
                        ingredients = isEconomics ? Array.Empty<Ingredient>() : currentItem.ingredients.ToArray()
                    });
                }
                else 
                {
                    string name;
                    if (!ins.config.multilingualMode) name = $"{currentItem.name} ({itemSkin.name})";
                    else name = string.Format("{0} ({1})", ins.GetMessage($"{currentItem.parentSection}.{currentItem.permission}.name", player.UserIDString), ins.GetMessage($"{currentItem.parentSection}.{currentItem.permission}.variations.{itemSkin.permission}", player.UserIDString));

                    queue.Add(new QueueItem
                    {
                        taskId = taskId,
                        endTime = 0f,
                        fullTime = currentItem.time,
                        crafingTime = crafingTime,
                        img = itemSkin.img == "" ? "" : $"CraftingPanel.{currentItem.parentSection}.{currentItem.permission}.{activeSkin}",
                        command = itemSkin.command,
                        name = name,
                        useDefaultName = itemSkin.useDefaultName,
                        shortname = itemSkin.shortname,
                        skinId = itemSkin.skinId,
                        isBlueprint = itemSkin.isBlueprint,
                        showStatus = currentItem.showStatus,
                        canUseBonuses = currentItem.canUseBonuses,
                        amount = currentItem.amount,
                        remainingMultiplier = multiplier,
                        economicPrice = currentItem.economicPrice,
                        ingredients = isEconomics ? Array.Empty<Ingredient>() : currentItem.ingredients.ToArray()
                    });
                }

                if (ins.config.closeAfterCraft) ClosePanel(player);
                SendStatus(NotifyType.Start, Mathf.CeilToInt(queue.Last().crafingTime * multiplier), queue.Last().name, multiplier.ToString());
                if (queue.Count > 1 && !ins.config.closeAfterCraft) UpdateQueuePanel();
                if (!IsInvoking("CraftingProcess")) InvokeRepeating("CraftingProcess", 0.1f, 1f);
            }

            private void CraftingProcess()
            {
                if (queue.Count == 0) 
                { 
                    CancelInvoke("CraftingProcess");
                    UpdateQueuePanel();
                    if (!player.IsConnected) Destroy(this);
                    return;
                }

                if (queue[0].endTime > Time.realtimeSinceStartup) return;
                
                if (queue[0].endTime == 0) 
                {
                    queue[0].endTime = Time.realtimeSinceStartup + queue[0].crafingTime;
                    SendStatus(NotifyType.Start, Mathf.CeilToInt(queue[0].crafingTime * queue[0].remainingMultiplier), queue[0].name, queue[0].remainingMultiplier.ToString());
                    UpdateQueuePanel();
                    return;
                }

                FinishCrafting(queue[0]);

                if (queue[0].remainingMultiplier > 1)
                {
                    queue[0].remainingMultiplier -= 1;
                    queue[0].endTime = 0;
                    SendStatus(NotifyType.Start, -1, queue[0].name, queue[0].remainingMultiplier.ToString());
                }
                else 
                {
                    SendStatus(NotifyType.Start);
                    queue.RemoveAt(0);
                }  

                CraftingProcess();
            }

            internal void FinishCrafting(QueueItem qi, bool isInstant = false)
            {
                float duplicateChance = ins.BonusHandler(player, "GetCraftDuplicate", qi.canUseBonuses);
                float refundChance = ins.BonusHandler(player, "GetCraftRefund", qi.canUseBonuses);

                if (qi.command != "")
                {
                    StringBuilder sb = new StringBuilder(qi.command); // окончательно собранная строка
                    sb.Replace("%amount%", qi.amount.ToString());
                    StringBuilder sb2 = new StringBuilder(); // только первоначальная команда
                    sb2.Append(sb);

                    if (isInstant)
                    {
                        for (int i = 1; i < qi.remainingMultiplier; i++)
                        {
                            if (duplicateChance > 0 && UnityEngine.Random.Range(0f, 1f) < duplicateChance) 
                            {
                                sb.Append(',');
                                sb.Append(sb2);
                                sb.Append(',');
                                sb.Append(sb2);
                                ins.BonusHandler(player, "SendMessage", qi.canUseBonuses, "DuplicateProc", qi.name);
                            }
                            else
                            {
                                sb.Append(',');
                                sb.Append(sb2);
                            }
                        }
                    }
                    else
                    {
                        if (UnityEngine.Random.Range(0f, 1f) < duplicateChance) 
                        {
                            sb.Append(',');
                            sb.Append(sb2);
                            ins.BonusHandler(player, "SendMessage", qi.canUseBonuses, "DuplicateProc", qi.name);
                        }
                    }

                    sb.Replace("%steamid%", player.UserIDString);
                    sb.Replace("%username%", player.displayName);
                    
                    foreach (var command in sb.ToString().Split(",")) ins.Server.Command(command.Trim());
                    if (qi.showStatus) SendStatus(NotifyType.Finish, 4, qi.name, isInstant ? qi.remainingMultiplier.ToString() : "1");
                }
                else 
                {
                    int amount = 0;

                    if (isInstant)
                    {
                        for (int i = 0; i < qi.remainingMultiplier; i++)
                        {
                            if (UnityEngine.Random.Range(0f, 1f) < duplicateChance) 
                            {
                                amount += qi.amount * 2;
                                ins.BonusHandler(player, "SendMessage", qi.canUseBonuses, "DuplicateProc", qi.name);
                            }
                            else  amount += qi.amount;
                        }
                    }
                    else 
                    {
                        if (UnityEngine.Random.Range(0f, 1f) < duplicateChance) 
                        {
                            amount = qi.amount * 2;
                            ins.BonusHandler(player, "SendMessage", qi.canUseBonuses, "DuplicateProc", qi.name);
                        }
                        else amount = qi.amount;
                    }

                    GiveItem(qi.shortname, amount, qi.skinId, qi.name, qi.useDefaultName, qi.isBlueprint, qi.showStatus, true);
                }

                if (!qi.ingredients.IsNullOrEmpty() && refundChance > 0 && UnityEngine.Random.Range(0f, 1f) < refundChance)
                {
                    ReturnIngredients(qi);
                    ins.BonusHandler(player, "SendMessage", qi.canUseBonuses, "CraftRefund");
                }

                ins.BonusHandler(player, "SendAward", qi.canUseBonuses, qi.fullTime);
                PlayEffect(NotifyType.Finish);
            }

            internal void CancelCraft(int taskId)
            {
                int index = queue.FindIndex(x => x.taskId == taskId);
                if (index != -1) 
                {
                    ins.BonusHandler(player, "RemoveXP", queue[index].canUseBonuses, queue[index].endTime - Time.realtimeSinceStartup);
                    ReturnIngredients(queue[index]);
                    queue.RemoveAt(index);
                    UpdateQueuePanel();
                    PlayEffect(NotifyType.Cancel);
                    SendStatus(NotifyType.Start);
                    CraftingProcess();
                }
            }

            internal void CancelAll()
            {
                for (int i = queue.Count - 1; i > -1; i--)
                {
                    ReturnIngredients(queue[i]);
                    queue.RemoveAt(i);
                }
            }

            internal void AddOrRemoveFavoriteItem()
            {
                ins.data.TryAdd(player.UserIDString, new UserData());
                string str = string.Join(".", currentItem.parentSection, currentItem.permission);
                string color, sprite;

                if (ins.data[player.UserIDString].favoriteItems.Add(str)) 
                {
                    favoriteItems.Add(currentItem);
                    color = "0.94 0.83 0.25 1";
                    sprite = "assets/icons/favourite_active.png";
                    if (currentSection.permission == "favorite") ItemsSection();
                }
                else 
                {
                    ins.data[player.UserIDString].favoriteItems.Remove(str);
                    favoriteItems.Remove(currentItem);
                    color = "0.84 0.8 0.76 1";
                    sprite = "assets/icons/favourite_inactive.png";

                    if (currentSection.permission == "favorite") 
                    {
                        if (favoriteItems.Count == 0) CuiHelper.DestroyUi(player, "CraftingPanel.InvisibleItemsBox");
                        else
                        {
                            currentItem = favoriteItems[0];
                            ItemsSection();
                            return;
                        } 
                    }
                }      

                CuiHelper.AddUi(player, new CuiElementContainer
                {
                    new CuiElement
                    {
                        Name = "CraftingPanel.Menu.favorite.Amount",
                        Update = true,
                        Components = { new CuiTextComponent { Text = favoriteItems.Count.ToString() } }
                    },
                    new CuiElement
                    {
                        Name = "CraftingPanel.InvisibleInfoBox.Favorites.Img",
                        Update = true,
                        Components = { new CuiImageComponent { Color = color, Sprite = sprite } }
                    }
                });
            }

            internal void StartItemsSearch(string str)
            {
                CuiHelper.AddUi(player, new CuiElementContainer
                {
                    new CuiElement
                    {
                        Name = "CraftingPanel.InvisibleItemsBox",
                        Parent = "CraftingPanel.ItemsBox",
                        DestroyUi = "CraftingPanel.InvisibleItemsBox",
                        Components = { new CuiTextComponent { Text = ins.GetMessage("SEARCHING", player.UserIDString), Color = "0.84 0.8 0.76 0.1", FontSize = 40, Align = TextAnchor.MiddleCenter } }
                    }
                });

                isSearchActive = true;
                searchCoroutine = ServerMgr.Instance.StartCoroutine(ItemsSearch(str));
            }

            private IEnumerator ItemsSearch(string str)
            {
                foundItems.Clear();

                foreach (var section in ins.config.sections)
                {
                    if (section.permission == "favorite") continue;
                    yield return CoroutineEx.waitForSeconds(0.25f);

                    foreach (var item in section.items)
                    {
                        if (item.name.Contains(str, StringComparison.CurrentCultureIgnoreCase)) foundItems.Add(item);
                    }
                }

                if (isPanelOpen) 
                {
                    if (foundItems.Count != 0) ItemsSection(isSearch: true);
                    else 
                    {
                        CuiHelper.AddUi(player, new CuiElementContainer
                        {
                            new CuiElement
                            {
                                Name = "CraftingPanel.InvisibleItemsBox",
                                Update = true,
                                Components = { new CuiTextComponent { Text = ins.GetMessage("SEARCH_EMPTY", player.UserIDString) } }
                            }
                        });
                    }
                }

                searchCoroutine = null;
            }

            private void UpdateQueuePanel()
            {
                if (isPanelOpen) CraftingQueue();
            }

            private bool HasNeededWorkbench(int workbenchTier)
            {
                bool canCraft = false;
                bool workbench1 = player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench1);
                bool workbench2 = player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench2);
                bool workbench3 = player.HasPlayerFlag(BasePlayer.PlayerFlags.Workbench3);

                switch (workbenchTier)
                {
                    case 0:
                        canCraft = true;
                        break;
                    case 1:
                        if (workbench1 || workbench2 || workbench3) canCraft = true;
                        break;
                    case 2:
                        if (workbench2 || workbench3) canCraft = true;
                        break;
                    case 3:
                        if (workbench3) canCraft = true;
                        break;
                }

                return canCraft;
            }

            private void GiveItem(string shortname, int amount, ulong skinId, string name, bool useDefaultName, bool isBlueprint = false, bool showStatus = true, bool isCreatedItem = false)
            {
                int itemId = ItemManager.FindItemDefinition(shortname).itemid;
                Item item;
                bool itemIsEmpty;

                if (isBlueprint)
                {
                    item = ItemManager.CreateByName("blueprintbase");
                    item.blueprintTarget = itemId;
                }
                else item = ItemManager.CreateByItemID(itemId, amount, skinId);

                if (ins.config.status.isEnable && ins.config.status.type != "Rust")
                {
                    if (!useDefaultName) item.name = name;
                }
                else if (!useDefaultName)
                {
                    item.name = name;
                    itemId = 0;
                }
                
                if (isCreatedItem)
                {
                    itemIsEmpty = item.MoveToContainer(player.inventory.containerBelt);
                    if (!itemIsEmpty) itemIsEmpty = item.MoveToContainer(player.inventory.containerMain);
                }
                else
                {
                    itemIsEmpty = item.MoveToContainer(player.inventory.containerMain);
                    if (!itemIsEmpty) itemIsEmpty = item.MoveToContainer(player.inventory.containerBelt);
                }

                if (!itemIsEmpty)
                {
                    Item backpack = player.inventory.GetBackpackWithInventory();
                    if (backpack != null) itemIsEmpty = item.MoveToContainer(backpack.contents);
                }

                if (!itemIsEmpty)
                {
                    if (ins.config.useBackpacks && ins.Backpacks != null && ins.Backpacks.IsLoaded && ins.Backpacks.Author == "WhiteThunder")
                    {
                        object obj = ins.Backpacks.Call<object>("API_TryDepositBackpackItem", (ulong)player.userID, item);
                        itemIsEmpty = obj is not bool || (bool)obj;
                    }
                }

                if (itemIsEmpty)
                {
                    if (showStatus) SendStatus(NotifyType.Finish, 4, name, amount.ToString(), itemId);
                }
                else
                {
                    SendStatus(NotifyType.Cancel, 4, name, item.amount.ToString(), itemId);
                    item.Drop(player.inventory.containerMain.dropPosition + UnityEngine.Random.value * Vector3.down + UnityEngine.Random.insideUnitSphere, player.inventory.containerMain.dropVelocity);
                }
            }

            private void TakeItem(ItemDefinition itemDef, int amount, ulong skinId = 0)
            {
                if (ins.config.useItemRetriever && ins.ItemRetriever != null && ins.ItemRetriever.IsLoaded)
                {
                    ins.ItemRetriever.Call<int>("API_TakePlayerItems", player, new Dictionary<string, object>{ ["ItemId"] = itemDef.itemid, ["SkinId"] = skinId }, amount);
                }
                else
                {
                    int remainingQuantity = amount;

                    if (ins.config.useBackpacks && ins.Backpacks != null && ins.Backpacks.IsLoaded && ins.Backpacks.Author == "WhiteThunder")
                    {
                        remainingQuantity -= ins.Backpacks.Call<int>("API_TakeBackpackItems", (ulong)player.userID, new Dictionary<string, object>{ ["ItemId"] = itemDef.itemid, ["SkinId"] = skinId }, amount);
                    }

                    if (remainingQuantity == 0) return;

                    Item backpack = player.inventory.GetBackpackWithInventory();

                    if (backpack != null)
                    {
                        List<Item> backpackItems = backpack.contents.FindItemsByItemID(itemDef.itemid);

                        foreach (var item in backpackItems)
                        {
                            if (item.skin != skinId) continue;
                            if (remainingQuantity <= 0) break;

                            if (item.amount <= remainingQuantity)
                            {
                                remainingQuantity -= item.amount;
                                item.RemoveFromContainer();
                            }
                            else 
                            {
                                item.UseItem(remainingQuantity);
                                remainingQuantity = 0;
                            } 
                        }
                    }

                    List<Item> inventoryItems = player.inventory.FindItemsByItemID(itemDef.itemid);

                    foreach (var item in inventoryItems)
                    {
                        if (item.skin != skinId) continue;
                        if (remainingQuantity <= 0) break;

                        if (item.amount <= remainingQuantity)
                        {
                            remainingQuantity -= item.amount;
                            item.RemoveFromContainer();
                        }
                        else 
                        {
                            item.UseItem(remainingQuantity);
                            remainingQuantity = 0;
                        }              
                    }
                }
            }

            private void ReturnIngredients(QueueItem queueItem)
            {
                if (queueItem.ingredients.IsEmpty())
                {
                    switch (ins.config.economyType)
                    {
                        case "ServerRewards":
                            ins.ServerRewards.Call("AddPoints", (ulong)player.userID, queueItem.economicPrice * queueItem.remainingMultiplier);
                            break;
                        case "Economics":
                            ins.Economics.Call("Deposit", player.UserIDString, (double)(queueItem.economicPrice * queueItem.remainingMultiplier));
                            break;
                        case "IQEconomic":
                            ins.IQEconomic.Call("API_SET_BALANCE", player.UserIDString, queueItem.economicPrice * queueItem.remainingMultiplier);
                            break;
                    }
                }
                else 
                {
                    foreach (var ingredient in queueItem.ingredients) GiveItem(ingredient.shortname, ingredient.amount * queueItem.remainingMultiplier, ingredient.skinId, ingredient.name, ingredient.useDefaultName, showStatus:false);
                }
            }

            private bool EconomicItemPurchase(int price)
            {
                bool isPaid = false;

                switch (ins.config.economyType)
                {
                    case "ServerRewards":
                        object serverRewardsObj= ins.ServerRewards.Call<object>("CheckPoints", (ulong)player.userID);
                        if (serverRewardsObj != null)
                        {
                            int serverRewardsBalance = (int)serverRewardsObj;
                            if (serverRewardsBalance >= price)
                            {
                                object obj = ins.ServerRewards.Call<object>("TakePoints", (ulong)player.userID, price);
                                if (obj != null) isPaid = (bool)obj;
                            }
                        }
                        break;
                    case "Economics":
                        isPaid = ins.Economics.Call<bool>("Withdraw", player.UserIDString, (double)price);
                        break;
                    case "IQEconomic":
                        if (ins.IQEconomic.Call<bool>("API_IS_REMOVED_BALANCE", player.UserIDString, price))
                        {
                            ins.IQEconomic.Call("API_REMOVE_BALANCE", player.UserIDString, price);
                            isPaid = true;
                        }
                        break;
                }

                return isPaid;
            }     

            internal void SendNotify(string message)
            {
                if (!ins.config.notify.isEnable) return;
                if (ins.config.notify.closePanel) ClosePanel(player);

                switch (ins.config.notify.type)
                {
                    case "Chat":
                        ins.SendReply(player, message);
                        break;
                    case "GameTips":
                        if (ins.config.notify.GameTipsType == "info") player.ShowToast(GameTip.Styles.Blue_Normal, message);
                        else player.ShowToast(GameTip.Styles.Red_Normal, message);
                        break;
                    case "Notify":
                        ins.Notify.Call("SendNotify", player, ins.config.notify.NotifyType, message);
                        break;
                    case "GUIAnnouncements":
                        ins.GUIAnnouncements.Call("CreateAnnouncement", message, ins.config.notify.GUIABanerColor, ins.config.notify.GUIATextColor, player, ins.config.notify.GUIAMarginTop);
                        break;
                }
            }

            internal void SendStatus(NotifyType type, int time = 0, string text = "", string subText = "", int itemId = 0)
            {
                if (!ins.config.status.isEnable) return;

                if (ins.config.status.type == "Rust")
                {
                    switch(type)
                    {
                        case NotifyType.Finish:
                            player.Command("note.inv", itemId == 0 ? 204391461 : itemId, int.Parse(subText), itemId == 0 ? text : "", BaseEntity.GiveItemReason.PickedUp); // itemId, amount, name, reason
                            break;
                        case NotifyType.Cancel:
                            player.Command("note.inv", itemId == 0 ? 204391461 : itemId, -int.Parse(subText), itemId == 0 ? text : "", BaseEntity.GiveItemReason.PickedUp);
                            break;
                    }
                }
                else if (ins.config.status.type == "SimpleStatus")
                {
                    switch (type)
                    {
                        case NotifyType.Start:
                            if (ins.SimpleStatus.Call<int>("GetDuration", (ulong)player.userID, "CraftingPanel.Craft") == 0 || time == -1 || time == 0) 
                            {
                                if (time != -1) 
                                {
                                    ins.SimpleStatus.Call("SetStatus", (ulong)player.userID, "CraftingPanel.Craft", time * 2);
                                    ins.SimpleStatus.Call("SetStatusTitle", (ulong)player.userID, "CraftingPanel.Craft", text.Length > 15 ? string.Format("<size=13>{0}...</size>", text.ToUpper(CultureInfo.InvariantCulture)[..15]) : text.ToUpper(CultureInfo.InvariantCulture));
                                }
                                ins.SimpleStatus.Call("SetStatusText", (ulong)player.userID, "CraftingPanel.Craft", string.Format("<size=11>{0}</size>", ins.GetMessage("SS_AMOUNT", player.UserIDString, subText)));
                            }
                            else return;
                            break;
                        case NotifyType.Finish:
                            ins.SimpleStatus.Call("SetStatus", (ulong)player.userID, "CraftingPanel.Give", time);
                            ins.SimpleStatus.Call("SetStatusProperty", (ulong)player.userID, "CraftingPanel.Give", new Dictionary<string, object>
                            {
                                ["title"] = text.Length > 16 ? string.Format("<size=13>{0}...</size>", text.ToUpper(CultureInfo.InvariantCulture)[..16]) : text.ToUpper(CultureInfo.InvariantCulture),
                                ["text"] = string.Format("<size=14>+{0}</size>", subText)
                            });
                            break;
                    }
                }
                else if (ins.config.status.type == "AdvancedStatus")
                {
                    Dictionary<string, object> parameters = new Dictionary<string, object> 
                    { 
                        ["Plugin"] = ins.Title,
                        ["Text_Size"] = 13,
                        ["Main_Material"] = "assets/content/ui/namefontmaterial.mat",
                        ["Main_Transparency"] = 1f,
                        ["Text_Font"] = "RobotoCondensed-Bold.ttf",
                        ["SubText_Font"] = "RobotoCondensed-Bold.ttf",
                    };

                    switch (type)
                    {
                        case NotifyType.Start:
                            if (!ins.AdvancedStatus.Call<bool>("BarExists", player, "CraftingPanel.Craft", ins.Title) || time == -1 || time == 0)
                            {
                                if (time == 0) 
                                {
                                    ins.AdvancedStatus.Call("DeleteBar", player, "CraftingPanel.Craft", ins.Title);
                                    return;
                                }
                                else if (time != -1)
                                {
                                    parameters.Add("BarType", "Timed");
                                    parameters.Add("Main_Color", "#1c6999");
                                    parameters.Add("Image_Sprite", "assets/icons/gear.png");
                                    parameters.Add("Image_Color", "#429cd6"); 
                                    parameters.Add("Text", text.Length > 14 ? string.Format("{0}...", text.ToUpper(CultureInfo.InvariantCulture)[..14]) : text.ToUpper(CultureInfo.InvariantCulture)); 
                                    parameters.Add("Text_Color", "#d5effc");
                                    parameters.Add("SubText_Color", "#abddff");
                                    parameters.Add("SubText_Size", 11); 
                                    parameters.Add("TimeStamp", Network.TimeEx.currentTimestamp + time * 2);
                                }

                                parameters.Add("Id", "CraftingPanel.Craft");
                                parameters.Add("SubText", ins.GetMessage("SS_AMOUNT", player.UserIDString, subText));
                            }
                            else return;
                            break;
                        case NotifyType.Finish:
                            parameters.Add("Id", "CraftingPanel.Give." + UnityEngine.Random.Range(int.MinValue, int.MaxValue));
                            parameters.Add("BarType", "Timed");
                            parameters.Add("Main_Color", "#576642");
                            parameters.Add("Image_Sprite", "assets/icons/picked up.png");
                            parameters.Add("Image_Color", "#a6ff05"); 
                            parameters.Add("Text", text.Length > 15 ? string.Format("{0}...", text.ToUpper(CultureInfo.InvariantCulture)[..15]) : text.ToUpper(CultureInfo.InvariantCulture)); 
                            parameters.Add("Text_Color", "#ffffff");
                            parameters.Add("SubText", $"+{subText}");
                            parameters.Add("SubText_Color", "#ffffff");
                            parameters.Add("SubText_Size", 14); 
                            parameters.Add("TimeStamp", Network.TimeEx.currentTimestamp + time);
                            break;
                        case NotifyType.Cancel:
                            parameters.Add("Id", "CraftingPanel.Give." + UnityEngine.Random.Range(int.MinValue, int.MaxValue));
                            parameters.Add("BarType", "Timed");
                            parameters.Add("Main_Color", "#6b2117");
                            parameters.Add("Image_Sprite", "assets/icons/close.png");
                            parameters.Add("Image_Color", "#ff3d1a"); 
                            parameters.Add("Text", text.Length > 15 ? string.Format("{0}...", text.ToUpper(CultureInfo.InvariantCulture)[..15]) : text.ToUpper(CultureInfo.InvariantCulture));
                            parameters.Add("Text_Color", "#d17d70");
                            parameters.Add("SubText", $"-{subText}");
                            parameters.Add("SubText_Color", "#d17d70");
                            parameters.Add("SubText_Size", 13);
                            parameters.Add("TimeStamp", Network.TimeEx.currentTimestamp + time);
                            break;
                    }

                    ins.AdvancedStatus.Call("CreateBar", player, parameters);
                }
            }

            private void PlayEffect(NotifyType type)
            {
                string effectName;

                switch (type)
                {
                    case NotifyType.Start:
                        if (!string.IsNullOrEmpty(ins.config.effectStart)) effectName = ins.config.effectStart;
                        else return;
                        break;
                    case NotifyType.Cancel:
                        if (!string.IsNullOrEmpty(ins.config.effectCancel)) effectName = ins.config.effectCancel;
                        else return;
                        break;
                    case NotifyType.Finish:
                        if (!string.IsNullOrEmpty(ins.config.effectFinish)) effectName = ins.config.effectFinish;
                        else return;
                        break;
                    default:
                        return;
                }

                Effect effect = new Effect(effectName, player, 0, Vector3.zero, Vector3.forward, player.limitNetworking ? player.Connection : null);
                if (player.limitNetworking) EffectNetwork.Send(effect, player.Connection);
                else EffectNetwork.Send(effect);
            }

            #region Interface
            internal void MainPanel()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image = { Color = "0.24 0.23 0.19 0.8", FadeIn = 0.3f },
                    CursorEnabled = true
                }, ins.config.canScale ? "Overlay" : "OverlayNonScaled", "CraftingPanel.Background", "CraftingPanel.Background");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image = { Color = "0.17 0.16 0.14 0.8", Material = "assets/content/ui/uibackgroundblur-notice.mat" }
                }, "CraftingPanel.Background", "CraftingPanel.BackgroundBoxMat");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image = { Color = "0.17 0.16 0.14 0.9", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" }
                }, "CraftingPanel.BackgroundBoxMat", "CraftingPanel.BackgroundBoxSpr");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Color = "0 0 0 0", Command = "CraftingPanelSortingCommand close" }
                }, "CraftingPanel.BackgroundBoxSpr", "CraftingPanel.ButtonBox");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-550 -270", OffsetMax = "550 300" },
                    Image = { Color = "0.19 0.89 0.18 0" }
                }, "CraftingPanel.BackgroundBoxSpr", "CraftingPanel.MainBox");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0.106", AnchorMax = "0.15 1" },
                    Image = { Color = "0.35 0.34 0.32 0.5", Sprite = "assets/content/ui/ui.background.tile.psd", Material = "assets/icons/greyout.mat" }
                }, "CraftingPanel.MainBox", "CraftingPanel.LeftBox");

                container.Add(new CuiPanel
                {
                    //RectTransform = { AnchorMin = "0.153 0.946", AnchorMax = "0.48 1" },
                    RectTransform = { AnchorMin = "0.153 0.106", AnchorMax = "0.48 0.159" },
                    Image = { Color = "0.35 0.34 0.32 0.5", Sprite = "assets/content/ui/ui.background.tile.psd", Material = "assets/icons/greyout.mat" }
                }, "CraftingPanel.MainBox", "CraftingPanel.SearchBox");

                container.Add(new CuiPanel
                {
                    //RectTransform = { AnchorMin = "0.153 0.106", AnchorMax = "0.48 0.94" },
                    RectTransform = { AnchorMin = "0.153 0.166", AnchorMax = "0.48 1" },
                    Image = { Color = "0.35 0.34 0.32 0.5", Sprite = "assets/content/ui/ui.background.tile.psd", Material = "assets/icons/greyout.mat" }
                }, "CraftingPanel.MainBox", "CraftingPanel.ItemsBox");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.48 0.1" },
                    Image = { Color = "0.35 0.34 0.32 0.5", Sprite = "assets/content/ui/ui.background.tile.psd", Material = "assets/icons/greyout.mat" }
                }, "CraftingPanel.MainBox", "CraftingPanel.QueueBox");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.02 0", AnchorMax = "1 0.98" },
                    Text = { Text = ins.GetMessage("CRAFTING_QUEUE", player.UserIDString), Color = "0.84 0.8 0.76 0.1", FontSize = 40, Align = TextAnchor.MiddleLeft }
                }, "CraftingPanel.QueueBox");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.4835 0.6", AnchorMax = "1 1" },
                    Image = { Color = "0.35 0.34 0.32 0.5", Sprite = "assets/content/ui/ui.background.tile.psd", Material = "assets/icons/greyout.mat" }
                }, "CraftingPanel.MainBox", "CraftingPanel.InfoBox");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.4835 0.367", AnchorMax = "1 0.593" },
                    Image = { Color = "0.35 0.34 0.32 0.5", Sprite = "assets/content/ui/ui.background.tile.psd", Material = "assets/icons/greyout.mat" }
                }, "CraftingPanel.MainBox", "CraftingPanel.SkinBox");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.4835 0", AnchorMax = "1 0.36" },
                    Image = { Color = "0.35 0.34 0.32 0.5", Sprite = "assets/content/ui/ui.background.tile.psd", Material = "assets/icons/greyout.mat" }
                }, "CraftingPanel.MainBox", "CraftingPanel.IngredientsBox");

                CuiHelper.AddUi(player, container);
                ButtonsSection(isFirstOpen: true);
            }

            internal void ButtonsSection(Section section = null, bool isFirstOpen = false, bool thisOnly = false)
            {
                if (section != null && currentSection == section && !isSearchActive) return;
                CuiElementContainer container = new CuiElementContainer();

                if (!isFirstOpen || isSearchActive)
                {
                    if (section != currentSection)
                    {
                        container.Add(new CuiElement
                        {
                            Name = $"CraftingPanel.Menu.{section.permission}",
                            Update = true,
                            Components = { new CuiButtonComponent { Color = "0.18 0.66 1 0.35", Material = "assets/icons/greyout.mat" } }
                        });

                        container.Add(new CuiElement
                        {
                            Name = $"CraftingPanel.Menu.{section.permission}.Text",
                            Update = true,
                            Components = { new CuiTextComponent { Text = null, Color = "0.96 0.92 0.88 0.8" } }
                        });

                        container.Add(new CuiElement
                        {
                            Name = $"CraftingPanel.Menu.{section.permission}.Background",
                            Update = true,
                            Components = { new CuiImageComponent { Color = "0.96 0.92 0.88 0.8" } }
                        });

                        container.Add(new CuiElement
                        {
                            Name = $"CraftingPanel.Menu.{currentSection.permission}",
                            Update = true,
                            Components = { new CuiButtonComponent { Color = "0 0 0 0", Material = "assets/Icons/IconMaterial.mat" } }
                        });

                        container.Add(new CuiElement
                        {
                            Name = $"CraftingPanel.Menu.{currentSection.permission}.Text",
                            Update = true,
                            Components = { new CuiTextComponent { Text = null, Color = "0.96 0.92 0.88 0.18" } }
                        });

                        container.Add(new CuiElement
                        {
                            Name = $"CraftingPanel.Menu.{currentSection.permission}.Background",
                            Update = true,
                            Components = { new CuiImageComponent { Color = "0.96 0.92 0.88 0.18" } }
                        });

                        container.Add(new CuiElement
                        {
                            Name = "CraftingPanel.SearchBox.Input",
                            Update = true,
                            Components = { new CuiInputFieldComponent { Text = ins.GetMessage("SEARCH", player.UserIDString), Autofocus = false, NeedsKeyboard = true, HudMenuInput = true } }
                        });
                    }

                    if (isSearchActive)
                    {
                        if (searchCoroutine != null) 
                        {
                            ServerMgr.Instance.StopCoroutine(searchCoroutine);
                            searchCoroutine = null;
                        }

                        foundItems.Clear();
                        isSearchActive = false;
                    }

                    currentSection = section;

                    if (currentSection.permission == "favorite" && favoriteItems.Count == 0)
                    {
                        CuiHelper.DestroyUi(player, "CraftingPanel.InvisibleItemsBox");
                        thisOnly = true;
                    }
                    else
                    {
                        currentItem = null;
                        currentSkin = null;
                    }
                }
                else
                {
                    List<Section> availableSections = ins.config.sections.Where(x => !x.permissionIsActivate || (x.permissionIsActivate && ins.permission.UserHasPermission(player.UserIDString, $"craftingpanel.section.{x.permission}"))).ToList();
                    int scrollboxSize = availableSections.Count * 34;
                    if (scrollboxSize < 511) scrollboxSize = 511;
                    int a = 0;
                    string tc, bc;

                    if (currentSection == null)
                    {
                        if (availableSections[0].permission == "favorite")
                        {
                            if (favoriteItems.Count == 0) currentSection = availableSections[1];
                            else currentSection = availableSections[0];
                        }
                        else currentSection = availableSections[0];
                    }
                    else isFirstOpen = false;

                    container.Add(new CuiElement
                    {
                        Name = "CraftingPanel.LeftBoxScrollbar",
                        DestroyUi = "CraftingPanel.LeftBoxScrollbar",
                        Parent = "CraftingPanel.LeftBox",
                        Components = 
                        {
                            new CuiRectTransformComponent { AnchorMin = "-0.002 0.001", AnchorMax = "0.982 0.998" },
                            new CuiScrollViewComponent 
                            {
                                ContentTransform = new CuiRectTransform { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{scrollboxSize}", OffsetMax = "0 0" },
                                Horizontal = false,
                                Vertical = true,
                                MovementType = UnityEngine.UI.ScrollRect.MovementType.Elastic,
                                Elasticity = 0.25f,
                                Inertia = true,
                                DecelerationRate = 0.3f,
                                ScrollSensitivity = 24f,
                                VerticalScrollbar = null
                            }
                        }
                    });

                    container.Add(new CuiPanel { Image = { Color = "0 0 0 0" } }, "CraftingPanel.LeftBoxScrollbar");

                    container.Add(new CuiElement
                    {
                        Name = "CraftingPanel.SearchBox.Input",
                        Parent = "CraftingPanel.SearchBox",
                        DestroyUi = "CraftingPanel.SearchBox.Input",
                        Components =
                        {
                            new CuiRectTransformComponent { AnchorMin = "0.02 0", AnchorMax = "1 0.98" },
                            new CuiInputFieldComponent { Text = ins.GetMessage("SEARCH", player.UserIDString), Autofocus = false, Command = "CraftingPanelSortingCommand search", Color = "0.84 0.8 0.76 0.1", Align = TextAnchor.MiddleLeft, FontSize = 21, CharsLimit = 35, NeedsKeyboard = true, HudMenuInput = true }
                        }
                    });

                    foreach (var item in availableSections)
                    {
                        string count;

                        if (item.permission == "favorite") count = favoriteItems.Count.ToString(); 
                        else count = item.items.Count.ToString();
                        

                        if (item == currentSection)
                        {
                            tc = "0.96 0.92 0.88 0.8";
                            bc = "0.18 0.66 1 0.35";
                        }
                        else
                        {
                            tc = "0.96 0.92 0.88 0.18";
                            bc = "0 0 0 0";
                        }

                        container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 {a-34}", OffsetMax = $"0 {a}" },
                            Button = { Color = bc, Command = $"CraftingPanelSortingCommand menu {item.permission}", Sprite = "assets/content/ui/ui.background.tile.psd", Material = item != currentSection ? "assets/Icons/IconMaterial.mat" : "assets/icons/greyout.mat" }
                        }, "CraftingPanel.LeftBoxScrollbar", $"CraftingPanel.Menu.{item.permission}");

                        container.Add(new CuiLabel
                        {
                            Text = { Color = tc, Text = string.Format("        {0}", ins.config.multilingualMode ? ins.GetMessage($"section.{item.permission}", player.UserIDString) : item.name), FontSize = 16, Align = TextAnchor.MiddleLeft }
                        }, $"CraftingPanel.Menu.{item.permission}", $"CraftingPanel.Menu.{item.permission}.Text");

                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0.1 0.5", AnchorMax = "0.1 0.5", OffsetMin = "-8 -8", OffsetMax = "8 8" },
                            Image = { Color = tc, Png = ins.ImageLibrary.Call<string>("GetImage", $"CraftingPanel.Section.{item.permission}") }
                        }, $"CraftingPanel.Menu.{item.permission}", $"CraftingPanel.Menu.{item.permission}.Background");

                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "0.94 1" },
                            Text = { Text = count, Color = "0.48 0.78 1 0.8", FontSize = 12, Align = TextAnchor.MiddleRight }
                        }, $"CraftingPanel.Menu.{item.permission}", $"CraftingPanel.Menu.{item.permission}.Amount");
                        
                        a -= 34;
                    }
                }

                CraftingQueue();
                CuiHelper.AddUi(player, container);
                if (!thisOnly) ItemsSection(isFirstOpen: isFirstOpen);
            }
            
            internal void ItemsSection(SectionItem sectionItem = null, bool isFirstOpen = false, bool isSearch = false)
            {
                if (sectionItem != null && sectionItem == currentItem) return;
                string color;
                int a = 5; // Начало по x 
                int b = -89; // Начало по y (размер кнопки 84)
                bool isFavorite = currentSection.permission == "favorite";
                bool activeItemChanged = sectionItem != null && sectionItem != currentItem;
                bool isSearchOrFavorite = isFavorite || isSearch;
                bool isReopening = ins.config.saveLastItem && !isFirstOpen && currentItem != null;

                if (!activeItemChanged && isSearchOrFavorite)
                {
                    CuiElementContainer container = new CuiElementContainer();
                    List<SectionItem> sectionItems;
                    if (isSearch) 
                    { 
                        sectionItems = foundItems; 
                        currentItem = sectionItems[0];
                        currentSkin = currentItem.itemSkins.ElementAtOrDefault(0);
                    }
                    else if (isFavorite) sectionItems = favoriteItems;
                    else sectionItems = currentSection.items;
                    currentItem ??= sectionItems[0];
                    int scrollboxSize = Mathf.CeilToInt(sectionItems.Count / 4f) * 89 + 5;
                    if (scrollboxSize < 475) scrollboxSize = 475;

                    container.Add(new CuiPanel { Image = { Color = "0 0 0 0" } }, "CraftingPanel.ItemsBox", "CraftingPanel.InvisibleItemsBox", "CraftingPanel.InvisibleItemsBox");

                    container.Add(new CuiElement
                    {
                        Name = "CraftingPanel.ItemsBoxScrollbar",
                        Parent = "CraftingPanel.InvisibleItemsBox",
                        Components = 
                        {
                            new CuiRectTransformComponent { AnchorMin = "0 0.001", AnchorMax = "0.995 0.9948" },
                            new CuiScrollViewComponent 
                            {
                                ContentTransform = new CuiRectTransform { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{scrollboxSize}", OffsetMax = "0 0" },
                                Horizontal = false,
                                Vertical = true,
                                MovementType = UnityEngine.UI.ScrollRect.MovementType.Elastic,
                                Elasticity = 0.25f,
                                Inertia = true,
                                DecelerationRate = 0.3f,
                                ScrollSensitivity = 24f,
                                VerticalScrollbar = null
                            }
                        }
                    });

                    container.Add(new CuiPanel { Image = { Color = "0 0 0 0" } }, "CraftingPanel.ItemsBoxScrollbar");

                    for (int i = 0; i < sectionItems.Count; i++)
                    {
                        if(sectionItems[i].permission == currentItem.permission) color = "0.18 0.66 1 0.35";
                        else color = "0 0 0 0";

                        container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{a} {b}", OffsetMax = $"{a+84} {b+84}" },
                            Button = { Command = $"CraftingPanelSortingCommand items {sectionItems[i].parentSection} {sectionItems[i].permission}", Color = color }
                        }, "CraftingPanel.ItemsBoxScrollbar", $"CraftingPanel.ItemsBoxScrollbar.Item.{sectionItems[i].permission}");

                        if (sectionItems[i].img != "")
                        {
                            container.Add(new CuiElement
                            {
                                Parent = $"CraftingPanel.ItemsBoxScrollbar.Item.{sectionItems[i].permission}",
                                Components = 
                                {
                                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                                    new CuiRawImageComponent { Png = ins.ImageLibrary.Call<string>("GetImage", $"CraftingPanel.{sectionItems[i].parentSection}.{sectionItems[i].permission}") }
                                }
                            });
                        }
                        else
                        {
                            if (sectionItems[i].isBlueprint)
                            {
                                container.Add(new CuiPanel
                                {
                                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                                    Image = { ItemId = ItemManager.FindItemDefinition("blueprintbase").itemid }
                                }, $"CraftingPanel.ItemsBoxScrollbar.Item.{sectionItems[i].permission}");
                            }

                            container.Add(new CuiPanel
                            {
                                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                                Image = { ItemId = ItemManager.FindItemDefinition(sectionItems[i].shortname).itemid, SkinId = sectionItems[i].skinId }
                            }, $"CraftingPanel.ItemsBoxScrollbar.Item.{sectionItems[i].permission}");
                        }

                        if ((i + 1) % 4 == 0) { a = 5; b -= 89; }
                        else a += 89;
                    }

                    CuiHelper.AddUi(player, container);
                }
                else if (activeItemChanged)
                {
                    CuiHelper.AddUi(player, new CuiElementContainer
                    {
                        new CuiElement
                        {
                            Name = $"CraftingPanel.ItemsBoxScrollbar.Item.{sectionItem.permission}",
                            Update = true,
                            Components = { new CuiButtonComponent { Color = "0.18 0.66 1 0.35" } }
                        },
                        new CuiElement
                        {
                            Name = $"CraftingPanel.ItemsBoxScrollbar.Item.{currentItem.permission}",
                            Update = true,
                            Components = { new CuiButtonComponent { Color = "0 0 0 0" } }
                        }
                    });

                    currentItem = sectionItem;
                    currentSkin = null;
                }
                else 
                {
                    CuiHelper.AddUi(player, currentSection.jsonReadyItems);
                    currentItem ??= currentSection.items[0];

                    if (isReopening && currentSection.items?.IndexOf(currentItem) != 0)
                    {
                        CuiHelper.AddUi(player, new CuiElementContainer
                        {
                            new CuiElement
                            {
                                Name = $"CraftingPanel.ItemsBoxScrollbar.Item.{currentItem.permission}",
                                Update = true,
                                Components = { new CuiButtonComponent { Color = "0.18 0.66 1 0.35" } }
                            },
                            new CuiElement
                            {
                                Name = $"CraftingPanel.ItemsBoxScrollbar.Item.{currentSection.items[0].permission}",
                                Update = true,
                                Components = { new CuiButtonComponent { Color = "0 0 0 0" } }
                            }
                        });
                    }
                }

                InfoSection();
            }

            internal void InfoSection()
            {
                CuiElementContainer container = new CuiElementContainer();
                float crafingTime = ins.GetWorkbenchTime(player, currentItem.time, currentItem.workbench);
                float bonusTime = ins.BonusHandler(player, "GetCraftTime", currentItem.canUseBonuses, crafingTime);
                if (bonusTime > 0) crafingTime = bonusTime;
                float a = -20;

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.998 0.998" },
                    Image = { Color = "1 1 1 0" }
                }, "CraftingPanel.InfoBox", "CraftingPanel.InvisibleInfoBox", "CraftingPanel.InvisibleInfoBox");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -32", OffsetMax = "0 0" },
                    Text = { Text = ins.config.multilingualMode ? ins.GetMessage($"{currentItem.parentSection}.{currentItem.permission}.name", player.UserIDString) : currentItem.name, Color = "0.84 0.8 0.76 1", FontSize = 24, Align = TextAnchor.MiddleCenter }
                }, "CraftingPanel.InvisibleInfoBox");

                if (ins.permission.UserHasPermission(player.UserIDString, "CraftingPanel.section.favorite"))
                {
                    string color, sprite;
                    UserData userData;

                    if (ins.data.TryGetValue(player.UserIDString, out userData) && userData.favoriteItems.Contains($"{currentItem.parentSection}.{currentItem.permission}")) 
                    { 
                        color = "0.94 0.83 0.25 1"; 
                        sprite = "assets/icons/favourite_active.png";
                    }
                    else 
                    { 
                        color = "0.84 0.8 0.76 1"; 
                        sprite = "assets/icons/favourite_inactive.png"; 
                    }

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"4.5 {a}", OffsetMax = $"75 {a+15}" },
                        Button = { Color = "0 0 0 0.6", Command = "CraftingPanelSortingCommand favorite" },
                        Text = { Text = ins.GetMessage("FAVORITE", player.UserIDString) + "   ", Color = "0.84 0.8 0.76 1", FontSize = 9, Align = TextAnchor.MiddleRight }
                    }, "CraftingPanel.InvisibleInfoBox", "CraftingPanel.InvisibleInfoBox.Favorites");

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0.14 0.5", AnchorMax = "0.14 0.5", OffsetMin = "-5 -5", OffsetMax = "5 5" },
                        Image = { Color = color, Sprite = sprite } 
                    }, "CraftingPanel.InvisibleInfoBox.Favorites", "CraftingPanel.InvisibleInfoBox.Favorites.Img");

                    a += -19.5f;
                }

                if (currentItem.activPermission)
                {
                    string color, image;
                    if (ins.permission.UserHasPermission(player.UserIDString, $"craftingpanel.{currentSection.permission}.{currentItem.permission}")) 
                    {
                        color = "0.34 0.82 0.22 1";
                        image = ins.ImageLibrary.Call<string>("GetImage", "CraftingPanel.Check");
                    }
                    else 
                    {
                        color = "0.74 0.17 0.17 1";
                        image = ins.ImageLibrary.Call<string>("GetImage", "CraftingPanel.Cross");
                    }

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"4.5 {a}", OffsetMax = $"75 {a+15}" },
                        Image = { Color = "0 0 0 0.6" }
                    }, "CraftingPanel.InvisibleInfoBox", "CraftingPanel.InvisibleInfoBox.Permission");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "0.94 1" },
                        Text = { Text = ins.GetMessage("PERMISSION", player.UserIDString), Color = "0.84 0.8 0.76 1", FontSize = 9, Align = TextAnchor.MiddleRight  }
                    }, "CraftingPanel.InvisibleInfoBox.Permission");

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0.14 0.5", AnchorMax = "0.14 0.5", OffsetMin = "-5 -5", OffsetMax = "5 5" },
                        Image = { Color = color, Png = image }
                    }, "CraftingPanel.InvisibleInfoBox.Permission");
                }

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.968 1", AnchorMax = "0.968 1", OffsetMin = "-54 -30", OffsetMax = "14 -5" },
                    Image = { Color = "0 0 0 0.6" }
                }, "CraftingPanel.InvisibleInfoBox", "CraftingPanel.InvisibleInfoBox.Time");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.22 0.5", AnchorMax = "0.22 0.5", OffsetMin = "-9 -9", OffsetMax = "9 9" },
                    Image = { Color = "0.84 0.8 0.76 1", Png = ins.ImageLibrary.Call<string>("GetImage", "CraftingPanel.Clock") }
                }, "CraftingPanel.InvisibleInfoBox.Time");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.9 0.95" },
                    Text = { Text = crafingTime.ToString("0.0", CultureInfo.InvariantCulture), Color = "0.84 0.8 0.76 0.5", FontSize = 18, Align = TextAnchor.MiddleRight }
                }, "CraftingPanel.InvisibleInfoBox.Time");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.968 1", AnchorMax = "0.968 1", OffsetMin = "-38 -60", OffsetMax = "14 -35" },
                    Image = { Color = "0 0 0 0.6" }
                }, "CraftingPanel.InvisibleInfoBox", "CraftingPanel.InvisibleInfoBox.Amount");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.28 0.5", AnchorMax = "0.28 0.5", OffsetMin = "-9 -9", OffsetMax = "9 9" },
                    Image = { Color = "0.84 0.8 0.76 1", Png = ins.ImageLibrary.Call<string>("GetImage", "CraftingPanel.Amount") }
                }, "CraftingPanel.InvisibleInfoBox.Amount");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.87 0.95" },
                    Text = { Text = currentItem.amount.ToString(), Color = "0.84 0.8 0.76 0.5", FontSize = 18, Align = TextAnchor.MiddleRight }
                }, "CraftingPanel.InvisibleInfoBox.Amount");

                if (currentItem.workbench != 0)
                {
                    string wbbc, wbtc;

                    if (currentItem.workbench == 1) { wbbc = "0.3 0.38 0.17 1"; wbtc = "0.65 0.91 0.21 1"; }
                    else if (currentItem.workbench == 2) { wbbc = "0.03 0.3 0.5 1"; wbtc = "0.17 0.64 0.98 1"; }
                    else { wbbc = "0.74 0.17 0.17 1"; wbtc = "0.99 0.53 0.19 1"; }

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-120 -53", OffsetMax = "120 -33" },
                        Image = { Color = wbbc, Sprite = "assets/content/ui/ui.background.tile.psd", Material = "assets/icons/greyout.mat" }
                    }, "CraftingPanel.InvisibleInfoBox", "CraftingPanel.InvisibleInfoBox.Workbench");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 -0.12", AnchorMax = "1 1" },
                        Text = { Text = ins.GetMessage("WB_TIER", player.UserIDString, currentItem.workbench), Color = wbtc, Align = TextAnchor.MiddleCenter }
                    }, "CraftingPanel.InvisibleInfoBox.Workbench");
                }

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.01 0.03", AnchorMax = "0.63 0.7" },
                    Text = { Text = ins.config.multilingualMode ? ins.GetMessage($"{currentItem.parentSection}.{currentItem.permission}.description", player.UserIDString) : currentItem.description, Color = "0.84 0.8 0.76 1", FontSize = 14, Font = "robotocondensed-regular.ttf" }
                }, "CraftingPanel.InvisibleInfoBox");

                if (currentItem.properties != "")
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-200 5", OffsetMax = "-5 140" },
                        Image = { Color = "1 1 1 0.15" }
                    }, "CraftingPanel.InvisibleInfoBox", "CraftingPanel.InvisibleInfoBox.Properties");

                    container.Add(new CuiLabel 
                    {
                        RectTransform = { AnchorMin = "0.03 0.03", AnchorMax = "0.97 0.97" },
                        Text = { Text = ins.config.multilingualMode ? ins.GetMessage($"{currentItem.parentSection}.{currentItem.permission}.properties", player.UserIDString) : currentItem.properties, Color = "1 1 1 0.8", FontSize = 12 }
                    }, "CraftingPanel.InvisibleInfoBox.Properties");

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 1", OffsetMax = "0 20" },
                        Image = { Color = "1 1 1 0.25" }
                    }, "CraftingPanel.InvisibleInfoBox.Properties", "CraftingPanel.InvisibleInfoBox.Properties.Title");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.04 0", AnchorMax = "1 1" },
                        Text = { Text = ins.GetMessage("INFORMATION", player.UserIDString), Color = "1 1 1 0.5", FontSize = 12, Align = TextAnchor.MiddleLeft }
                    }, "CraftingPanel.InvisibleInfoBox.Properties.Title");
                }

                CuiHelper.AddUi(player, container);
                SkinSection();
            }

            internal void SkinSection(ItemSkin itemSkin = null, bool thisOnly = false)
            {
                if (itemSkin != null && currentSkin == itemSkin) return;
                CuiElementContainer container = new CuiElementContainer();

                if (itemSkin == null)
                {
                    container.Add(new CuiPanel { Image = { Color = "1 1 1 0" } }, "CraftingPanel.SkinBox", "CraftingPanel.InvisibleSkinBox", "CraftingPanel.InvisibleSkinBox");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.015 0.75", AnchorMax = "0.6 1" },
                        Text = { Text = ins.GetMessage("VARIATIONS", player.UserIDString), Color = "0.84 0.8 0.76 1", FontSize = 16, Align = TextAnchor.MiddleLeft }
                    }, "CraftingPanel.InvisibleSkinBox");

                    if (currentItem.itemSkins.Count == 0)
                    {
                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                            Text = { Text = ins.GetMessage("UNAVAILABLE", player.UserIDString), Color = "0.84 0.8 0.76 0.1", FontSize = 40, Align = TextAnchor.MiddleCenter }
                        }, "CraftingPanel.InvisibleSkinBox");
                    }
                    else
                    {
                        float a = 5.7f;
                        string color;
                        int scrollboxSize = currentItem.itemSkins.Count * 94 + 7;
                        if (scrollboxSize < 569) scrollboxSize = 569;
                        currentSkin ??= currentItem.itemSkins[0];

                        container.Add(new CuiElement
                        {
                            Name = "CraftingPanel.SkinBoxScrollbar",
                            Parent = "CraftingPanel.InvisibleSkinBox",
                            Components = 
                            {
                                new CuiRectTransformComponent { AnchorMin = "-0.002 0", AnchorMax = "0.994 0.988" },
                                new CuiScrollViewComponent 
                                {
                                    ContentTransform = new CuiRectTransform { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = "0 0", OffsetMax = $"{scrollboxSize} 0" },
                                    Horizontal = true,
                                    Vertical = false,
                                    MovementType = UnityEngine.UI.ScrollRect.MovementType.Elastic,
                                    Elasticity = 0.25f,
                                    Inertia = true,
                                    DecelerationRate = 0.3f,
                                    ScrollSensitivity = 24f,
                                    HorizontalScrollbar = null
                                }
                            }
                        });

                        container.Add(new CuiPanel { Image = { Color = "0 0 0 0" } }, "CraftingPanel.SkinBoxScrollbar");

                        foreach (var skin in currentItem.itemSkins)
                        {
                            if (skin.permission == currentSkin.permission) color = "0.18 0.66 1 0.35";
                            else color = "0 0 0 0";

                            container.Add(new CuiButton
                            {
                                RectTransform = { AnchorMin = "0 0.41", AnchorMax = "0 0.41", OffsetMin = $"{a} -45", OffsetMax = $"{a+90} 45" },
                                Button = { Command = $"CraftingPanelSortingCommand skins {skin.permission}", Color = color }
                            }, "CraftingPanel.SkinBoxScrollbar", $"CraftingPanel.SkinBoxScrollbar.Skin.{skin.permission}");

                            if (skin.img != "")
                            {
                                container.Add(new CuiElement
                                {
                                    Parent = $"CraftingPanel.SkinBoxScrollbar.Skin.{skin.permission}",
                                    Components = 
                                    {
                                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                                        new CuiRawImageComponent { Png = ins.ImageLibrary.Call<string>("GetImage", $"CraftingPanel.{currentItem.parentSection}.{currentItem.permission}.{skin.permission}") }
                                    }
                                });
                            }
                            else
                            {
                                if (skin.isBlueprint)
                                {
                                    container.Add(new CuiPanel
                                    {
                                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                                        Image = { ItemId = ItemManager.FindItemDefinition("blueprintbase").itemid }
                                    }, $"CraftingPanel.SkinBoxScrollbar.Skin.{skin.permission}");
                                }

                                container.Add(new CuiPanel
                                {
                                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                                    Image = { ItemId = ItemManager.FindItemDefinition(skin.shortname).itemid, SkinId = skin.skinId }
                                }, $"CraftingPanel.SkinBoxScrollbar.Skin.{skin.permission}");
                            }

                            if (skin.activPermission && !ins.permission.UserHasPermission(player.UserIDString, $"craftingpanel.{currentItem.parentSection}.{currentItem.permission}.{skin.permission}"))
                            {
                                container.Add(new CuiButton
                                {
                                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                                    Button = { Command = "", Color = "0 0 0 0.6", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                                    Text = { Text = ins.GetMessage("NO_PERMISSION", player.UserIDString), Color = "0.84 0.8 0.76 1", FontSize = 12, Align = TextAnchor.MiddleCenter }
                                }, $"CraftingPanel.SkinBoxScrollbar.Skin.{skin.permission}");
                            }
                            else
                            {
                                container.Add(new CuiElement
                                {
                                    Parent = $"CraftingPanel.SkinBoxScrollbar.Skin.{skin.permission}",
                                    Components = 
                                    {
                                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 0.2" },
                                        new CuiTextComponent { Text = ins.config.multilingualMode && skin.name != "" ? ins.GetMessage($"{currentItem.parentSection}.{currentItem.permission}.variations.{skin.permission}", player.UserIDString) : skin.name, Color = "0.84 0.8 0.76 1", FontSize = 10, Align = TextAnchor.MiddleCenter },
                                        new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.5 0.5" }
                                    }
                                });
                            }

                            a += 94;
                        }
                    }
                }
                else 
                {
                    container.Add(new CuiElement
                    {
                        Name = $"CraftingPanel.SkinBoxScrollbar.Skin.{itemSkin.permission}",
                        Update = true,
                        Components = { new CuiButtonComponent { Color = "0.18 0.66 1 0.35" } }
                    });

                    container.Add(new CuiElement
                    {
                        Name = $"CraftingPanel.SkinBoxScrollbar.Skin.{currentSkin.permission}",
                        Update = true,
                        Components = { new CuiButtonComponent { Color = "0 0 0 0" } }
                    });

                    currentSkin = itemSkin;
                }

                CuiHelper.AddUi(player, container);
                if (!thisOnly) IngredientsSection();
            }

            internal void IngredientsSection(int multiplier = 1)
            {
                CuiElementContainer container = new CuiElementContainer();
                string tc, pc, tNeed, tItemName, tAll, tHave;
                int rowAmount = currentItem.ingredients.Count < 6 ? 6 : currentItem.ingredients.Count;
                int scrollboxSize = currentItem.ingredients.Count * 26 - 2;
                if (scrollboxSize < 154) scrollboxSize = 154;
                int a = 0;
                float b = 0.903f;

                container.Add(new CuiPanel { Image = { Color = "0 0 0 0" } }, "CraftingPanel.IngredientsBox", "CraftingPanel.InvisibleIngredientsBox", "CraftingPanel.InvisibleIngredientsBox");

                container.Add(new CuiElement
                {
                    Name = "CraftingPanel.IngredientsScrollbar",
                    Parent = "CraftingPanel.InvisibleIngredientsBox",
                    Components = 
                    {
                        new CuiRectTransformComponent { AnchorMin = "0.006 0.16", AnchorMax = "0.986 0.9" },
                        new CuiScrollViewComponent 
                        {
                            ContentTransform = new CuiRectTransform { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{scrollboxSize}", OffsetMax = "0 0" },
                            Horizontal = false,
                            Vertical = true,
                            MovementType = UnityEngine.UI.ScrollRect.MovementType.Elastic,
                            Elasticity = 0.25f,
                            Inertia = true,
                            DecelerationRate = 0.3f,
                            ScrollSensitivity = 24f,
                            VerticalScrollbar = null
                        }
                    }
                });

                container.Add(new CuiPanel { Image = { Color = "0 0 0 0" } }, "CraftingPanel.IngredientsScrollbar");
                
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.095 0.954", AnchorMax = "0.095 0.954", OffsetMin = "-50 -12", OffsetMax = "50 12" },
                    Text = { Text = ins.GetMessage("AMOUNT", player.UserIDString), Color = "0.84 0.8 0.76 1", FontSize = 12, Align = TextAnchor.MiddleCenter }
                }, "CraftingPanel.InvisibleIngredientsBox");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.41 0.954", AnchorMax = "0.41 0.954", OffsetMin = "-128.5 -12", OffsetMax = "128 12" },
                    Text = { Text = ins.GetMessage("ITEM_TYPE", player.UserIDString), Color = "0.84 0.8 0.76 1", FontSize = 12, Align = TextAnchor.MiddleCenter }
                }, "CraftingPanel.InvisibleIngredientsBox");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.725 0.954", AnchorMax = "0.725 0.954", OffsetMin = "-50 -12", OffsetMax = "50 12" },
                    Text = { Text = ins.GetMessage("TOTAL", player.UserIDString), Color = "0.84 0.8 0.76 1", FontSize = 12, Align = TextAnchor.MiddleCenter }
                }, "CraftingPanel.InvisibleIngredientsBox");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.903 0.954", AnchorMax = "0.903 0.954", OffsetMin = "-50 -12", OffsetMax = "50 12" },
                    Text = { Text = ins.GetMessage("HAVE", player.UserIDString), Color = "0.84 0.8 0.76 1", FontSize = 12, Align = TextAnchor.MiddleCenter }
                }, "CraftingPanel.InvisibleIngredientsBox");

                for (int i = 0; i < rowAmount; i++)
                {
                    int height = a + 24;

                    if (rowAmount > 6 || i < currentItem.ingredients.Count)
                    {
                        int playerHave = ins.GetItemAmount(player, currentItem.ingredients[i].shortname, currentItem.ingredients[i].skinId);
                        tAll = (currentItem.ingredients[i].amount * multiplier).ToString("###,###,###,###", CultureInfo.InvariantCulture);
                        tNeed = currentItem.ingredients[i].amount.ToString("###,###,###,###", CultureInfo.InvariantCulture);
                        tItemName = ins.config.multilingualMode ? ins.GetMessage($"{currentItem.ingredients[i].shortname}.{currentItem.ingredients[i].skinId}", player.UserIDString) : currentItem.ingredients[i].name;
                        pc = "0 0 0 0.7";

                        if (playerHave == 0)
                        {
                            tc = "0.87 0.73 0.41 1";
                            tHave = "0";
                        }
                        else if (playerHave < (currentItem.ingredients[i].amount * multiplier))
                        {
                            tc = "0.87 0.73 0.41 1";
                            tHave = playerHave.ToString("###,###,###,###", CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            tc = "0.84 0.8 0.76 1";
                            tHave = playerHave.ToString("###,###,###,###", CultureInfo.InvariantCulture);
                        }
                    }
                    else
                    {
                        pc = "0 0 0 0.5";
                        tc = "0 0 0 0";
                        tNeed = "";
                        tItemName = "";
                        tAll = "";
                        tHave = ""; 
                    }

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0.089 1", AnchorMax = "0.089 1", OffsetMin = $"-50 -{height}", OffsetMax = $"50 -{a}" },
                        Image = { Color = pc }
                    }, "CraftingPanel.IngredientsScrollbar", $"CraftingPanel.IngredientsScrollbar.Amount.{i}");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "0.95 1" },
                        Text = { Text = tNeed, Color = tc, FontSize = 12, Align = TextAnchor.MiddleRight, Font = "robotocondensed-regular.ttf" }
                    }, $"CraftingPanel.IngredientsScrollbar.Amount.{i}");

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0.41 1", AnchorMax = "0.41 1", OffsetMin = $"-128.5 -{height}", OffsetMax = $"128 -{a}" },
                        Image = { Color = pc }
                    }, "CraftingPanel.IngredientsScrollbar", $"CraftingPanel.IngredientsScrollbar.Item.{i}");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.015 0", AnchorMax = "1 1" },
                        Text = { Text = tItemName, Color = tc, FontSize = 12, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf" }
                    }, $"CraftingPanel.IngredientsScrollbar.Item.{i}");

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0.73 1", AnchorMax = "0.73 1", OffsetMin = $"-50 -{height}", OffsetMax = $"50 -{a}" },
                        Image = { Color = pc }
                    }, "CraftingPanel.IngredientsScrollbar", $"CraftingPanel.IngredientsScrollbar.Total.{i}");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.03 0", AnchorMax = "1 1" },
                        Text = { Text = tAll, Color = tc, FontSize = 12, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf" }
                    }, $"CraftingPanel.IngredientsScrollbar.Total.{i}");

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0.911 1", AnchorMax = "0.911 1", OffsetMin = $"-50 -{height}", OffsetMax = $"50 -{a}" },
                        Image = { Color = pc }
                    }, "CraftingPanel.IngredientsScrollbar", $"CraftingPanel.IngredientsScrollbar.Have.{i}");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.03 0", AnchorMax = "1 1" },
                        Text = { Text = tHave, Color = tc, FontSize = 12, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf" }
                    }, $"CraftingPanel.IngredientsScrollbar.Have.{i}");

                    a += 26;
                }

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.035 0.075", AnchorMax = "0.035 0.075", OffsetMin = "-16 -12", OffsetMax = "16 12" },
                    Button = { Command = multiplier - 1 < 1 ? "" : $"CraftingPanelSortingCommand multiplier change {multiplier - 1}", Color = "1 1 1 0.1", Sprite = "assets/content/ui/ui.background.tile.psd", Material = "assets/icons/greyout.mat" },
                    Text = { Text = "―", Color = "0.84 0.8 0.76 1", FontSize = 20, Align = TextAnchor.MiddleCenter }
                }, "CraftingPanel.InvisibleIngredientsBox");

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.1 0.075", AnchorMax = "0.1 0.075", OffsetMin = "-20 -12", OffsetMax = "20 12" },
                    Image = { Color = "0 0 0 0.6" }
                }, "CraftingPanel.InvisibleIngredientsBox", "CraftingPanel.InvisibleIngredientsBox.Input");

                container.Add(new CuiElement
                {
                    Parent = "CraftingPanel.InvisibleIngredientsBox.Input",
                    Components = 
                    {
                        new CuiRectTransformComponent { AnchorMin = "0.1 0", AnchorMax = "1 1" },
                        new CuiInputFieldComponent { Text = multiplier.ToString(), Command = "CraftingPanelSortingCommand multiplier input", Color = "0.84 0.8 0.76 1", Align = TextAnchor.MiddleLeft, FontSize = 14, CharsLimit = 4, HudMenuInput = true }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.166 0.075", AnchorMax = "0.166 0.075", OffsetMin = "-16 -12", OffsetMax = "16 12" }, 
                    Button = { Command = multiplier + 1 > currentItem.maxAmount ? "" : $"CraftingPanelSortingCommand multiplier change {multiplier + 1}", Color = "1 1 1 0.1", Sprite = "assets/content/ui/ui.background.tile.psd", Material = "assets/icons/greyout.mat" },
                    Text = { Text = "✚", Color = "0.84 0.8 0.76 1", FontSize = 16, Align = TextAnchor.MiddleCenter }
                }, "CraftingPanel.InvisibleIngredientsBox");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.225 0.075", AnchorMax = "0.225 0.075", OffsetMin = "-16 -12", OffsetMax = "16 12" },
                    Button = { Command = "CraftingPanelSortingCommand multiplier max", Color = "1 1 1 0.1", Sprite = "assets/content/ui/ui.background.tile.psd", Material = "assets/icons/greyout.mat" },
                    Text = { Text = "▶", Color = "0.84 0.8 0.76 1", FontSize = 16, Align = TextAnchor.MiddleCenter }
                }, "CraftingPanel.InvisibleIngredientsBox");

                if (currentItem.ingredients.Count != 0)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = $"{b} 0.075", AnchorMax = $"{b} 0.075", OffsetMin = "-50 -12", OffsetMax = "50 12" },
                        Button = { Command = $"CraftingPanelSortingCommand craft {multiplier} ingredients", Color = "1 1 1 0.1", Sprite = "assets/content/ui/ui.background.tile.psd", Material = "assets/icons/greyout.mat" },
                        Text = { Text = ins.GetMessage("CRAFT", player.UserIDString), Color = "0.84 0.8 0.76 1", FontSize = 13, Align = TextAnchor.MiddleCenter }
                    }, "CraftingPanel.InvisibleIngredientsBox");

                    b -= 0.178f;
                }

                if (ins.config.economyType != "" && currentItem.economicPrice != 0)
                {
                    NumberFormatInfo nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
                    nfi.NumberGroupSeparator = " ";
                    nfi.NumberDecimalSeparator = ".";
                    double balance = ins.GetEconomicBalance(player);
                    int cost = multiplier * currentItem.economicPrice;
                    string str = ins.GetMessage("CURRENCY", player.UserIDString, balance.ToString("#,0.##", nfi), cost.ToString("### ### ### ###", CultureInfo.InvariantCulture));

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = $"{b} 0.075", AnchorMax = $"{b} 0.075", OffsetMin = "-50 -12", OffsetMax = "50 12" },
                        Button = { Command = $"CraftingPanelSortingCommand craft {multiplier} economics", Color = "1 1 1 0.1", Sprite = "assets/content/ui/ui.background.tile.psd", Material = "assets/icons/greyout.mat" },
                        Text = { Text = ins.GetMessage("BUY", player.UserIDString), Color = "0.84 0.8 0.76 1", FontSize = 13, Align = TextAnchor.MiddleCenter }
                    }, "CraftingPanel.InvisibleIngredientsBox");

                    b -= 0.191f;

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{b} 0.075", AnchorMax = $"{b} 0.075", OffsetMin = "-57.5 -12", OffsetMax = "57.5 12" },
                        Image = { Color = "1 1 1 0.1", Sprite = "assets/content/ui/ui.background.tile.psd", Material = "assets/icons/greyout.mat" }
                    }, "CraftingPanel.InvisibleIngredientsBox", "CraftingPanel.InvisibleIngredientsBox.Economics");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.03 0", AnchorMax = "1 1" },
                        Text = { Text = ins.GetMessage("ECONOMY", player.UserIDString), Color = "0.84 0.8 0.76 1", FontSize = 9, Align = TextAnchor.MiddleLeft }
                    }, "CraftingPanel.InvisibleIngredientsBox.Economics");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "0.97 1" },
                        Text = { Text = str, Color = cost > balance ? "0.87 0.73 0.41 1" : "0.84 0.8 0.76 1", FontSize = 9, Align = TextAnchor.MiddleRight, Font = "robotocondensed-regular.ttf" }
                    }, "CraftingPanel.InvisibleIngredientsBox.Economics");

                    b -= 0.191f;
                }
;
                if (ins.permission.UserHasPermission(player.UserIDString, "craftingpanel._admin"))
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = $"{b} 0.075", AnchorMax = $"{b} 0.075", OffsetMin = "-50 -12", OffsetMax = "50 12" },
                        Button = { Command   = $"CraftingPanelSortingCommand givecraft {multiplier}", Color = "1 1 1 0.1", Sprite = "assets/content/ui/ui.background.tile.psd", Material = "assets/icons/greyout.mat" },
                        Text = { Text = ins.GetMessage("CRAFT_ADMIN", player.UserIDString), Color = "0.84 0.8 0.76 1", FontSize = 13, Align = TextAnchor.MiddleCenter }
                    }, "CraftingPanel.InvisibleIngredientsBox");
                }

                CuiHelper.AddUi(player, container);
            }

            internal void CraftingQueue()
            {
                if (queue.Count == 0 || !isPanelOpen) { CuiHelper.DestroyUi(player, "CraftingPanel.QueueScrollbarBox"); return; }
                CuiElementContainer container = new CuiElementContainer();
                int a = 9;
                int scrollboxSize = queue.Count * 52 + 10;
                if (scrollboxSize < 530) scrollboxSize = 530;

                container.Add(new CuiElement
                {
                    Name = "CraftingPanel.QueueScrollbarBox",
                    DestroyUi = "CraftingPanel.QueueScrollbarBox",
                    Parent = "CraftingPanel.QueueBox",
                    Components = 
                    {
                        new CuiRectTransformComponent { AnchorMin = "-0.001 0", AnchorMax = "0.995 0.985" },
                        new CuiScrollViewComponent 
                        {
                            ContentTransform = new CuiRectTransform { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = $"-{scrollboxSize} 0", OffsetMax = "0 0" },
                            Horizontal = true,
                            Vertical = false,
                            MovementType = UnityEngine.UI.ScrollRect.MovementType.Elastic,
                            Elasticity = 0.25f,
                            Inertia = true,
                            DecelerationRate = 0.3f,
                            ScrollSensitivity = 24f,
                            HorizontalScrollbar = null
                        }
                    }
                });

                container.Add(new CuiPanel { Image = { Color = "0 0 0 0" } }, "CraftingPanel.QueueScrollbarBox");

                for (int i = 0; i < queue.Count; i++)
                {
                    if (queue[i].img != "")
                    {
                        container.Add(new CuiElement
                        {
                            Name = $"CraftingPanel.QueueScrollbarBox.Item.{i}",
                            Parent = "CraftingPanel.QueueScrollbarBox",
                            Components = 
                            {
                                new CuiRectTransformComponent { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = $"-{a+44} -22", OffsetMax = $"-{a} 22" },
                                new CuiRawImageComponent { Png = ins.ImageLibrary.Call<string>("GetImage", queue[i].img) }
                            }
                        });
                    }
                    else
                    {
                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = $"-{a+44} -22", OffsetMax = $"-{a} 22" },
                            Image = { Color = "1 1 1 1", ItemId = ItemManager.FindItemDefinition(queue[i].shortname).itemid, SkinId = queue[i].skinId }
                        }, "CraftingPanel.QueueScrollbarBox", $"CraftingPanel.QueueScrollbarBox.Item.{i}"); 
                    }

                    if (i == 0)
                    {
                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0.15 0.5", AnchorMax = "0.85 1.4" },
                            Image = { Color = "0.64 0.88 0.2 1", Sprite = "assets/icons/subtract.png" },
                        }, $"CraftingPanel.QueueScrollbarBox.Item.{i}", $"CraftingPanel.QueueScrollbarBox.Item.{i}.Time");

                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0.3 0.5", AnchorMax = "0.3 0.5", OffsetMin = "-5 -5", OffsetMax = "5 5" },
                            Image = { Color = "0 0 0 1", Sprite = "assets/icons/stopwatch.png" }
                        }, $"CraftingPanel.QueueScrollbarBox.Item.{i}.Time");

                        container.Add(new CuiElement
                        {
                            Parent = $"CraftingPanel.QueueScrollbarBox.Item.{i}.Time",
                            Components = 
                            {
                                new CuiRectTransformComponent { AnchorMin = "0 -0.05", AnchorMax = "0.8 1" },
                                new CuiTextComponent { Text = ins.GetMessage("TIMER", player.UserIDString, "%TIME_LEFT%"), Color = "0 0 0 1", FontSize = 8, Align = TextAnchor.MiddleRight },
                                new CuiCountdownComponent { Command = "", StartTime = Mathf.CeilToInt(queue[0].endTime - Time.realtimeSinceStartup), EndTime = -1, Step = 1 }
                            }
                        });
                    }
                    
                    if (queue[i].remainingMultiplier > 1)
                    {
                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0.8 0.05", AnchorMax = "0.8 0.05", OffsetMin = "-11 -20", OffsetMax = "11 20" },
                            Image = { Color = "0.84 0.8 0.76 1", Sprite = "assets/icons/subtract.png" },
                        }, $"CraftingPanel.QueueScrollbarBox.Item.{i}", $"CraftingPanel.QueueScrollbarBox.Item.{i}.Amount");

                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.96" },
                            Text = { Text = $"x{queue[i].remainingMultiplier}", Color = "0 0 0 1", FontSize = 8, Align = TextAnchor.MiddleCenter }
                        }, $"CraftingPanel.QueueScrollbarBox.Item.{i}.Amount");
                    }

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Button = { Command = $"CraftingPanelSortingCommand cancelcraft {queue[i].taskId}", Color = "0 0 0 0" }
                    }, $"CraftingPanel.QueueScrollbarBox.Item.{i}");

                    a += 52;
                }

                CuiHelper.AddUi(player, container);
            }
            #endregion
        }
        #endregion

        #region Functions
        static void ClosePanel(BasePlayer player)
        {
            CraftingQueueController cqc;
            if(player.TryGetComponent(out cqc)) 
            {
                if (!ins.config.saveLastItem || (ins.config.saveLastItem && cqc.isSearchActive) || (ins.config.saveLastItem && cqc.currentSection?.permission == "favorite" && cqc.favoriteItems.Count == 0))
                {
                    cqc.currentSection = null;
                    cqc.currentItem = null;
                    cqc.currentSkin = null;
                }

                if (cqc.searchCoroutine != null) 
                {
                    ServerMgr.Instance.StopCoroutine(cqc.searchCoroutine);
                    cqc.searchCoroutine = null;
                }

                cqc.foundItems.Clear();
                cqc.isPanelOpen = false;
                cqc.isSearchActive = false;
            }
            CuiHelper.DestroyUi(player, "CraftingPanel.Background");
        }

        void RemoveFavoriteItem(BasePlayer player, string permName)
        {
            if (player == null) return;
            CraftingQueueController cqc;
            if (!player.TryGetComponent(out cqc)) return;
            string[] permArgs = permName.Split(".");

            if (permArgs[1] == "section" && permArgs.Length == 3) 
            {
                UserData userData;
                if (data.TryGetValue(player.UserIDString, out userData))
                {
                    if (userData.favoriteItems.Any(x => x.StartsWith(permArgs[2])))
                    {
                        cqc.favoriteItems.RemoveAll(x => x.parentSection == permArgs[2]);
                        userData.favoriteItems.RemoveWhere(x => x.StartsWith(permArgs[2]));
                       
                        if (cqc.isPanelOpen)
                        {
                            Section favorite = config.sections.FirstOrDefault(x => x.permission == "favorite");
                            Section section = config.sections.FirstOrDefault(x => x.permission == permArgs[2]);
                            if (section != null) cqc.SendNotify(ins.GetMessage("PERM_REVOKE", player.UserIDString, section.name, favorite.name));
                            ClosePanel(player);
                        }
                    }
                    else
                    {
                        if (cqc.isPanelOpen) 
                        {
                            cqc.currentSection = null;
                            cqc.currentItem = null;
                            cqc.currentSkin = null;
                            cqc.ButtonsSection(isFirstOpen: true);
                        }
                    }
                }
            } 
        }

        int GetMaxItemAmount(BasePlayer player, string section, string activeItem)
        {
            SectionItem item = config.sections.First(x => x.permission == section).items.First(x => x.permission == activeItem);
            int minMultiplier = item.maxAmount;

            foreach (var ingredient in item.ingredients)
            {
                int a = GetItemAmount(player, ingredient.shortname, ingredient.skinId) / ingredient.amount;

                if (a == 0) return 1;
                else if (a < minMultiplier) minMultiplier = a; 
            }

            return minMultiplier;
        }

        int GetItemAmount(BasePlayer player, string shortname, ulong skinId = 0)
        {
            ItemDefinition neededItem = ItemManager.FindItemDefinition(shortname);

            if (ItemRetriever != null && ItemRetriever.IsLoaded)
            {
                return ItemRetriever.Call<int>("API_SumPlayerItems", player, new Dictionary<string, object>{ ["ItemId"] = neededItem.itemid, ["SkinId"] = skinId });
            }
            else
            {
                int itemAmount = 0;

                if (config.useBackpacks && Backpacks != null && Backpacks.IsLoaded && Backpacks.Author == "WhiteThunder")
                {
                    itemAmount += Backpacks.Call<int>("API_GetBackpackItemAmount", (ulong)player.userID, neededItem.itemid, skinId);
                }

                List<Item> items = player.inventory.FindItemsByItemID(neededItem.itemid);

                foreach (var item in items)
                {
                    if (item.skin == skinId) itemAmount += item.amount;
                }

                Item backpack = player.inventory.GetBackpackWithInventory();

                if (backpack != null)
                {
                    foreach (var item in backpack.contents.FindItemsByItemID(neededItem.itemid))
                    {
                        if (item.skin == skinId) itemAmount += item.amount;
                    }
                }

                return itemAmount;
            }
        }

        float GetWorkbenchTime(BasePlayer player, float craftingTime, float neededLevel)
        {
            if (permission.UserHasPermission(player.UserIDString, "craftingpanel._workbench"))
            {
                float lvl = player.currentCraftLevel - neededLevel;
                if (lvl == 1) return craftingTime * 0.5f;
                else if (lvl >= 2) return craftingTime * 0.25f;
                else return craftingTime;
            }
            else return craftingTime;
        }

        double GetEconomicBalance(BasePlayer player)
        {
            double balance = 0;

            switch (config.economyType)
            {
                case "ServerRewards":
                    object obj = ServerRewards.Call<object>("CheckPoints", (ulong)player.userID);
                    if (obj != null) balance = (int)obj;
                    break;
                case "Economics":
                    balance = Economics.Call<double>("Balance", player.UserIDString);
                    break;
                case "IQEconomic":
                    balance = IQEconomic.Call<int>("API_GET_BALANCE", player.UserIDString);
                    break;
            }

            return balance;
        }
        
        float BonusHandler(BasePlayer player, string type, bool canUse, params object[] args)
        {
            if (!config.craftingBonuses.isEnable || !canUse || !permission.UserHasPermission(player.UserIDString, "craftingpanel._bonuses")) return 0;
            float value = 0;

            if (config.craftingBonuses.type == "SkillTree")
            {
                if (SkillTree == null || !SkillTree.IsLoaded) return value;

                switch (type)
                {
                    case "GetCraftTime":
                        if (config.craftingBonuses.STCanUseCraftSpeed)
                        {
                            float fullCraftingTime = Convert.ToSingle(args[0]);
                            float reducedTime = fullCraftingTime - fullCraftingTime * SkillTree.Call<float>("GetBuffValue", player, "Craft_Speed");
                            value = reducedTime <= 0 ? 0 : reducedTime;
                        }
                        break;
                    case "GetCraftDuplicate":
                        if (config.craftingBonuses.STCanUseCraftDuplicate) value = SkillTree.Call<float>("GetBuffValue", player, "Craft_Duplicate");
                        break;
                    case "GetCraftRefund":
                        if (config.craftingBonuses.STCanUseCraftRefund) value = SkillTree.Call<float>("GetBuffValue", player, "Craft_Refund");
                        break;
                    case "SendAward":
                        if (config.craftingBonuses.addXP > 0)
                        {
                            float fullCraftingTime = Convert.ToSingle(args[0]);
                            double addXP = config.craftingBonuses.basedOnTime ? Math.Round(fullCraftingTime * config.craftingBonuses.addXP, 2) : config.craftingBonuses.addXP;
                            SkillTree.Call("AwardXP", player, addXP, Title);
                        }
                        break;
                    case "RemoveXP":
                        if (config.craftingBonuses.removeXP > 0)
                        {
                            float fullCraftingTime = Convert.ToSingle(args[0]);
                            double removeXP = config.craftingBonuses.basedOnTime ? Math.Round(fullCraftingTime * config.craftingBonuses.removeXP, 2) : config.craftingBonuses.removeXP;
                            SkillTree.Call("RemoveXP", player, removeXP);
                        }
                        break;
                    case "SendMessage":
                        if (SkillTree.Call<bool>("NotificationsOn", player))
                        {
                            string[] langArgs = Array.ConvertAll(args, x => x.ToString() ?? string.Empty);
                            Player.Message(player, string.Format(lang.GetMessage(langArgs[0], SkillTree, player.UserIDString), langArgs.Skip(1).ToArray()), config.craftingBonuses.chatId);
                        }
                        break;
                }
            }
            else if (config.craftingBonuses.type == "ZLevelsRemastered")
            {
                //В плагине ZLevelsRemastered отсутсвует нормальное api. Поэтому все что здесь написано реализовано через "костыли".
                if (ZLevelsRemastered == null || !ZLevelsRemastered.IsLoaded) return value;

                switch (type)
                {
                    case "GetCraftTime":
                        if (config.craftingBonuses.ZLRCanUseCraftSpeed)
                        {
                            float fullCraftingTime = Convert.ToSingle(args[0]);
                            string piStr = ZLevelsRemastered.Call<string>("api_GetPlayerInfo", (ulong)player.userID); 
                            List<string> piData = piStr.Split('|').ToList();
                            float reducedTime = fullCraftingTime - fullCraftingTime * (config.craftingBonuses.ZLRPercentPerLevel * int.Parse(piData[2]) * 0.01f);
                            value = reducedTime <= 0 ? 0 : reducedTime;
                        }
                        break;
                    case "SendAward":
                        if (config.craftingBonuses.addXP > 0)
                        {
                            float fullCraftingTime = Convert.ToSingle(args[0]);
                            double addXP = config.craftingBonuses.basedOnTime ? Math.Round(fullCraftingTime * config.craftingBonuses.addXP, 2) : config.craftingBonuses.addXP;
                            List<string> piData = ZLevelsRemastered.Call<string>("api_GetPlayerInfo", (ulong)player.userID).Split('|').ToList();
                            double points = addXP + double.Parse(piData[3]);
                            double level = double.Parse(piData[2]) + 1;
                            double levelPoints = ZLevelsRemastered.Call<double>("getLevelPoints", level);
                            if (points >= levelPoints && level <= config.craftingBonuses.ZLRLevelCup) 
                            {
                                piData[2] = level.ToString("R"); // очки не обнуляются с новым уровнем
                                string message = string.Format($"<color={config.craftingBonuses.ZLRSkillColor}>{lang.GetMessage("LevelUpText", ZLevelsRemastered, player.UserIDString)}</color>", lang.GetMessage("CRAFTINGSkill", ZLevelsRemastered, player.UserIDString), level, points, levelPoints, level * config.craftingBonuses.ZLRPercentPerLevel);
                                Player.Message(player, message, config.craftingBonuses.ZLRPluginPrefix, config.craftingBonuses.chatId);
                            }
                            piData[3] = points.ToString("R");
                            piData.AddRange(Enumerable.Repeat(string.Empty, 2)); // добавляем пустые строки, т.к. в плагине ZLevelsRemastered ошибка в проверке на количество возвращаемых строк
                            if (!ZLevelsRemastered.Call<bool>("api_SetPlayerInfo", (ulong)player.userID, string.Join("|", piData))) PrintWarning("Error when sending data to \"ZLevelsRemastered\" plugin.");
                        }
                        break;
                    case "RemoveXP":
                        if (config.craftingBonuses.removeXP > 0)
                        {
                            float fullCraftingTime = Convert.ToSingle(args[0]);
                            double removeXP = config.craftingBonuses.basedOnTime ? Math.Round(fullCraftingTime * config.craftingBonuses.removeXP, 2) : config.craftingBonuses.removeXP;
                            List<string> piData = ZLevelsRemastered.Call<string>("api_GetPlayerInfo", (ulong)player.userID).Split('|').ToList();
                            double points = double.Parse(piData[3]) - removeXP;
                            double level = double.Parse(piData[2]) - 1;
                            if (points <= ZLevelsRemastered.Call<double>("getLevelPoints", level + 1) && level >= 1) piData[2] = level.ToString("R");
                            if (level <= 0) piData[3] = config.craftingBonuses.ZLRStartPoint.ToString("R");
                            else piData[3] = points.ToString("R");
                            piData.AddRange(Enumerable.Repeat(string.Empty, 2)); // добавляем пустые строки, т.к. в плагине ZLevelsRemastered ошибка в проверке на количество возвращаемых строк
                            if (!ZLevelsRemastered.Call<bool>("api_SetPlayerInfo", (ulong)player.userID, string.Join("|", piData))) PrintWarning("Error when sending data to \"ZLevelsRemastered\" plugin.");
                        }
                        break;
                }
            }            

            return value;
        }
        
        void UnlockDLCItems(BasePlayer player, bool unlock)
        {
            ProtoBuf.PersistantPlayer playerInfo = player.PersistantPlayerInfo;

            if (unlock)
            {
                foreach (ItemBlueprint bp in dlcItems)
                {
                    if (!playerInfo.unlockedItems.Contains(bp.targetItem.itemid)) playerInfo.unlockedItems.Add(bp.targetItem.itemid);
                }
            }
            else
            {
                foreach (ItemBlueprint bp in dlcItems)
                {
                    //if (!bp.targetItem.steamDlc.HasLicense((ulong)player.userID)) playerInfo.unlockedItems.Remove(bp.targetItem.itemid);
                    playerInfo.unlockedItems.Remove(bp.targetItem.itemid);
                }
            }

            player.PersistantPlayerInfo = playerInfo;
            player.SendNetworkUpdateImmediate();
            player.ClientRPC(RpcTarget.Player("UnlockedBlueprint", player), 0);
            data[player.UserIDString].dlcUnlocked = unlock;
        }
        #endregion 
    }
}