using System;
using System.Collections.Generic;
using Facepunch;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Plugins;
using Oxide.Core;
using System.Collections;
using Oxide.Game.Rust.Cui;
using System.IO;
using UnityEngine.Networking;
using Oxide.Plugins.SupermarketEventExtensionMethods;

namespace Oxide.Plugins
{
    [Info("SupermarketEvent", "KpucTaJl", "1.1.1")]
    internal class SupermarketEvent : RustPlugin
    {
        #region Config
        private const bool En = true;

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
            if (_config.PluginVersion < Version) UpdateConfigValues();
        }

        private void UpdateConfigValues()
        {
            Puts("Config update detected! Updating config values...");
            if (_config.PluginVersion < new VersionNumber(1, 0, 1))
            {
                _config.Cashier.Name = "Cashier";
                _config.Cashier.WearItems = new HashSet<NpcWear>
                {
                    new NpcWear { ShortName = "burlap.trousers", SkinId = 2042089723 },
                    new NpcWear { ShortName = "burlap.shirt", SkinId = 2042087814 },
                    new NpcWear { ShortName = "shoes.boots", SkinId = 826908114 },
                    new NpcWear { ShortName = "hat.boonie", SkinId = 2040709757 }
                };
                _config.Collector.Name = "Collector";
                _config.Collector.WearItems = new HashSet<NpcWear>
                {
                    new NpcWear { ShortName = "riot.helmet", SkinId = 1988565302 },
                    new NpcWear { ShortName = "shoes.boots", SkinId = 2186835886 },
                    new NpcWear { ShortName = "burlap.gloves", SkinId = 2186834533 },
                    new NpcWear { ShortName = "hoodie", SkinId = 2076428294 },
                    new NpcWear { ShortName = "pants", SkinId = 2076980911 },
                    new NpcWear { ShortName = "metal.plate.torso", SkinId = 1988550463 }
                };
            }
            if (_config.PluginVersion < new VersionNumber(1, 0, 4))
            {
                _config.PveMode.ScaleDamage = new Dictionary<string, float>
                {
                    ["Npc"] = 1f,
                    ["Animal"] = 1f
                };
            }
            if (_config.PluginVersion < new VersionNumber(1, 0, 7))
            {
                _config.DistanceAlerts = 0f;
            }
            if (_config.PluginVersion < new VersionNumber(1, 0, 8))
            {
                _config.SecurityAnimal.Enabled = true;
            }
            _config.PluginVersion = Version;
            Puts("Config update completed!");
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        public class ItemConfig
        {
            [JsonProperty("ShortName")] public string ShortName { get; set; }
            [JsonProperty(En ? "Minimum" : "Минимальное кол-во")] public int MinAmount { get; set; }
            [JsonProperty(En ? "Maximum" : "Максимальное кол-во")] public int MaxAmount { get; set; }
            [JsonProperty(En ? "Chance [0.0-100.0]" : "Шанс выпадения предмета [0.0-100.0]")] public float Chance { get; set; }
            [JsonProperty(En ? "Give blueprint? [true/false]" : "Это чертеж? [true/false]")] public bool IsBluePrint { get; set; }
            [JsonProperty("SkinID (0 = default)")] public ulong SkinId { get; set; }
            [JsonProperty(En ? "Name (empty = default)" : "Название (empty - default)")] public string Name { get; set; }
        }

        public class LootTableConfig
        {
            [JsonProperty(En ? "Minimum items" : "Минимальное кол-во элементов")] public int Min { get; set; }
            [JsonProperty(En ? "Maximum items" : "Максимальное кол-во элементов")] public int Max { get; set; }
            [JsonProperty(En ? "Enforce minimum and maximum values? [true/false]" : "Использовать минимальное и максимальное значение? [true/false]")] public bool UseCount { get; set; }
            [JsonProperty(En ? "List of items" : "Список предметов")] public List<ItemConfig> Items { get; set; }
        }

        public class PrefabConfig
        {
            [JsonProperty(En ? "Chance [0.0-100.0]" : "Шанс выпадения [0.0-100.0]")] public float Chance { get; set; }
            [JsonProperty(En ? "The path to the prefab (Entity full name)" : "Путь к prefab-у")] public string PrefabDefinition { get; set; }
        }

        public class PrefabLootTableConfig
        {
            [JsonProperty(En ? "Minimum number of prefabs" : "Минимальное кол-во prefab-ов")] public int Min { get; set; }
            [JsonProperty(En ? "Maximum number of prefabs" : "Максимальное кол-во prefab-ов")] public int Max { get; set; }
            [JsonProperty(En ? "Enforce minimum and maximum values? [true/false]" : "Использовать минимальное и максимальное значение? [true/false]")] public bool UseCount { get; set; }
            [JsonProperty(En ? "List of prefabs" : "Список prefab-ов")] public List<PrefabConfig> Prefabs { get; set; }
        }

        public class CrateConfig
        {
            [JsonProperty("Prefab")] public string Prefab { get; set; }
            [JsonProperty(En ? "Position" : "Позиция")] public string Position { get; set; }
            [JsonProperty(En ? "Rotation" : "Вращение")] public string Rotation { get; set; }
            [JsonProperty(En ? "Which loot table should the plugin use? (0 - default; 1 - own; 2 - AlphaLoot; 3 - CustomLoot; 4 - loot table of the Rust objects; 5 - combine the 1 and 4 methods)" : "Какую таблицу предметов необходимо использовать? (0 - стандартную; 1 - собственную; 2 - AlphaLoot; 3 - CustomLoot; 4 - таблица предметов объектов Rust; 5 - совместить 1 и 4 методы)")] public int TypeLootTable { get; set; }
            [JsonProperty(En ? "Loot table from prefabs (if the loot table type is 4 or 5)" : "Таблица предметов из prefab-ов (если тип таблицы предметов - 4 или 5)")] public PrefabLootTableConfig PrefabLootTable { get; set; }
            [JsonProperty(En ? "Own loot table (if the loot table type is 1 or 5)" : "Собственная таблица предметов (если тип таблицы предметов - 1 или 5)")] public LootTableConfig OwnLootTable { get; set; }
        }

        public class ColorConfig
        {
            [JsonProperty("r")] public float R { get; set; }
            [JsonProperty("g")] public float G { get; set; }
            [JsonProperty("b")] public float B { get; set; }
        }

        public class MarkerConfig
        {
            [JsonProperty(En ? "Use map marker? [true/false]" : "Использовать маркер на карте? [true/false]")] public bool Enabled { get; set; }
            [JsonProperty(En ? "Type (0 - simple, 1 - advanced)" : "Тип (0 - упрощенный, 1 - расширенный)")] public int Type { get; set; }
            [JsonProperty(En ? "Background radius (if the marker type is 0)" : "Радиус фона (если тип маркера - 0)")] public float Radius { get; set; }
            [JsonProperty(En ? "Background transparency" : "Прозрачность фона")] public float Alpha { get; set; }
            [JsonProperty(En ? "Color" : "Цвет")] public ColorConfig Color { get; set; }
            [JsonProperty(En ? "Text" : "Текст")] public string Text { get; set; }
        }

        public class PointConfig
        {
            [JsonProperty(En ? "Enabled? [true/false]" : "Включен? [true/false]")] public bool Enabled { get; set; }
            [JsonProperty(En ? "Text" : "Текст")] public string Text { get; set; }
            [JsonProperty(En ? "Size" : "Размер")] public int Size { get; set; }
            [JsonProperty(En ? "Color" : "Цвет")] public string Color { get; set; }
        }

        public class GuiConfig
        {
            [JsonProperty(En ? "Use the GUI? [true/false]" : "Использовать ли GUI? [true/false]")] public bool IsGui { get; set; }
            [JsonProperty("OffsetMin Y")] public string OffsetMinY { get; set; }
        }

        public class ChatConfig
        {
            [JsonProperty(En ? "Use chat messages? [true/false]" : "Использовать ли чат? [true/false]")] public bool IsChat { get; set; }
            [JsonProperty(En ? "Prefix for chat messages" : "Префикс сообщений в чате")] public string Prefix { get; set; }
        }

        public class GameTipConfig
        {
            [JsonProperty(En ? "Use Facepunch Game Tips (notification bar above hotbar)? [true/false]" : "Использовать ли Facepunch Game Tip (оповещения над слотами быстрого доступа игрока)? [true/false]")] public bool IsGameTip { get; set; }
            [JsonProperty(En ? "Style (0 - Blue Normal, 1 - Red Normal, 2 - Blue Long, 3 - Blue Short, 4 - Server Event)" : "Стиль (0 - Blue Normal, 1 - Red Normal, 2 - Blue Long, 3 - Blue Short, 4 - Server Event)")] public int Style { get; set; }
        }

        public class GuiAnnouncementsConfig
        {
            [JsonProperty(En ? "Do you use the plugin GUIAnnouncements? [true/false]" : "Использовать ли GUIAnnouncements? [true/false]")] public bool IsGuiAnnouncements { get; set; }
            [JsonProperty(En ? "Banner color" : "Цвет баннера")] public string BannerColor { get; set; }
            [JsonProperty(En ? "Text color" : "Цвет текста")] public string TextColor { get; set; }
            [JsonProperty(En ? "Adjust Vertical Position" : "Отступ от верхнего края")] public float ApiAdjustVPosition { get; set; }
        }

        public class NotifyConfig
        {
            [JsonProperty(En ? "Do you use the plugin Notify? [true/false]" : "Использовать ли Notify? [true/false]")] public bool IsNotify { get; set; }
            [JsonProperty(En ? "Type" : "Тип")] public int Type { get; set; }
        }

        public class DiscordConfig
        {
            [JsonProperty(En ? "Do you use the plugin DiscordMessages? [true/false]" : "Использовать ли DiscordMessages? [true/false]")] public bool IsDiscord { get; set; }
            [JsonProperty("Webhook URL")] public string WebhookUrl { get; set; }
            [JsonProperty(En ? "Embed Color (DECIMAL)" : "Цвет полосы (DECIMAL)")] public int EmbedColor { get; set; }
            [JsonProperty(En ? "Keys of required messages" : "Ключи необходимых сообщений")] public HashSet<string> Keys { get; set; }
        }

        public class EconomyConfig
        {
            [JsonProperty(En ? "Which economy plugins do you want to use? (Economics, ServerRewards, IQEconomic, XPerience)" : "Какие плагины экономики вы хотите использовать? (Economics, ServerRewards, IQEconomic, XPerience)")] public HashSet<string> Plugins { get; set; }
            [JsonProperty(En ? "The minimum value that a player must collect to get economy reward" : "Минимальное значение, которое игрок должен заработать, чтобы получить баллы за экономику")] public double Min { get; set; }
            [JsonProperty(En ? "Looting of crates" : "Ограбление ящиков")] public Dictionary<string, double> Crates { get; set; }
            [JsonProperty(En ? "Killing an NPC" : "Убийство NPC")] public double Npc { get; set; }
            [JsonProperty(En ? "Killing the merchant" : "Убийство торговца")] public double Cashier { get; set; }
            [JsonProperty(En ? "Killing the debt collector" : "Убийство инкассатора")] public double Collector { get; set; }
            [JsonProperty(En ? "Opening doors" : "Открытие дверей")] public double OpenDoors { get; set; }
            [JsonProperty(En ? "List of commands that are executed in server console at the end of the event ({steamid} - the player who collected the highest number of points)" : "Список команд, которые выполняются в консоли по окончанию ивента ({steamid} - игрок, который набрал наибольшее кол-во баллов)")] public HashSet<string> Commands { get; set; }
        }

        public class PveModeConfig
        {
            [JsonProperty(En ? "Use PVE mode the plugin? [true/false]" : "Использовать PVE режим работы плагина? [true/false]")] public bool Pve { get; set; }
            [JsonProperty(En ? "The amount of damage that the player has to do to become the Event Owner" : "Кол-во урона, которое должен нанести игрок, чтобы стать владельцем ивента")] public float Damage { get; set; }
            [JsonProperty(En ? "Damage Multipliers for calculate to become the Event Owner" : "Коэффициенты урона для подсчета, чтобы стать владельцем ивента")] public Dictionary<string, float> ScaleDamage { get; set; }
            [JsonProperty(En ? "Can the non-owner of the event loot the crates? [true/false]" : "Может ли не владелец ивента грабить ящики? [true/false]")] public bool LootCrate { get; set; }
            [JsonProperty(En ? "Can the non-owner of the event loot NPC corpses? [true/false]" : "Может ли не владелец ивента грабить трупы NPC? [true/false]")] public bool LootNpc { get; set; }
            [JsonProperty(En ? "Can the non-owner of the event deal damage to the NPC? [true/false]" : "Может ли не владелец ивента наносить урон по NPC? [true/false]")] public bool DamageNpc { get; set; }
            [JsonProperty(En ? "Can Npc attack a non-owner of the event? [true/false]" : "Может ли Npc атаковать не владельца ивента? [true/false]")] public bool TargetNpc { get; set; }
            [JsonProperty(En ? "Allow a non-owner of the event to enter the event zone? [true/false]" : "Разрешать входить внутрь зоны ивента не владельцу ивента? [true/false]")] public bool CanEnter { get; set; }
            [JsonProperty(En ? "Allow a player who has an active cooldown of the Event Owner to enter the event zone? [true/false]" : "Разрешать входить внутрь зоны ивента игроку, у которого активен кулдаун на получение статуса владельца ивента? [true/false]")] public bool CanEnterCooldownPlayer { get; set; }
            [JsonProperty(En ? "The time that the Event Owner may not be inside the event zone [sec.]" : "Время, которое владелец ивента может не находиться внутри зоны ивента [сек.]")] public int TimeExitOwner { get; set; }
            [JsonProperty(En ? "The time until the end of Event Owner status when it is necessary to warn the player [sec.]" : "Время таймера до окончания действия статуса владельца ивента, когда необходимо предупредить игрока [сек.]")] public int AlertTime { get; set; }
            [JsonProperty(En ? "Prevent the actions of the RestoreUponDeath plugin in the event zone? [true/false]" : "Запрещать работу плагина RestoreUponDeath в зоне действия ивента? [true/false]")] public bool RestoreUponDeath { get; set; }
            [JsonProperty(En ? "The time that the player can`t become the Event Owner, after the end of the event and the player was its owner [sec.]" : "Время, которое игрок не сможет стать владельцем ивента, после того как ивент окончен и игрок был его владельцем [sec.]")] public double CooldownOwner { get; set; }
            [JsonProperty(En ? "Darkening the dome (0 - disables the dome)" : "Затемнение купола (0 - отключает купол)")] public int Darkening { get; set; }
        }

        public class NpcBelt
        {
            [JsonProperty("ShortName")] public string ShortName { get; set; }
            [JsonProperty(En ? "Amount" : "Кол-во")] public int Amount { get; set; }
            [JsonProperty("SkinID (0 - default)")] public ulong SkinId { get; set; }
            [JsonProperty(En ? "Mods" : "Модификации на оружие")] public HashSet<string> Mods { get; set; }
            [JsonProperty(En ? "Ammo" : "Боеприпасы")] public string Ammo { get; set; }
        }

        public class NpcWear
        {
            [JsonProperty("ShortName")] public string ShortName { get; set; }
            [JsonProperty("SkinID (0 - default)")] public ulong SkinId { get; set; }
        }

        public class NpcConfig
        {
            [JsonProperty(En ? "Name" : "Название")] public string Name { get; set; }
            [JsonProperty(En ? "Health" : "Кол-во ХП")] public float Health { get; set; }
            [JsonProperty(En ? "Roam Range" : "Дальность патрулирования местности")] public float RoamRange { get; set; }
            [JsonProperty(En ? "Chase Range" : "Дальность погони за целью")] public float ChaseRange { get; set; }
            [JsonProperty(En ? "Attack Range Multiplier" : "Множитель радиуса атаки")] public float AttackRangeMultiplier { get; set; }
            [JsonProperty(En ? "Sense Range" : "Радиус обнаружения цели")] public float SenseRange { get; set; }
            [JsonProperty(En ? "Target Memory Duration [sec.]" : "Длительность памяти цели [sec.]")] public float MemoryDuration { get; set; }
            [JsonProperty(En ? "Scale damage" : "Множитель урона")] public float DamageScale { get; set; }
            [JsonProperty(En ? "Aim Cone Scale" : "Множитель разброса")] public float AimConeScale { get; set; }
            [JsonProperty(En ? "Detect the target only in the NPC's viewing vision cone? [true/false]" : "Обнаруживать цель только в углу обзора NPC? [true/false]")] public bool CheckVisionCone { get; set; }
            [JsonProperty(En ? "Vision Cone" : "Угол обзора")] public float VisionCone { get; set; }
            [JsonProperty(En ? "Speed" : "Скорость")] public float Speed { get; set; }
            [JsonProperty(En ? "Disable radio effects? [true/false]" : "Отключать эффекты рации? [true/false]")] public bool DisableRadio { get; set; }
            [JsonProperty(En ? "Is this a stationary NPC? [true/false]" : "Это стационарный NPC? [true/false]")] public bool Stationary { get; set; }
            [JsonProperty(En ? "Remove a corpse after death? (it is recommended to use the true value to improve performance) [true/false]" : "Удалять труп после смерти? (рекомендуется использовать значение true для повышения производительности) [true/false]")] public bool IsRemoveCorpse { get; set; }
            [JsonProperty(En ? "Wear items" : "Одежда")] public HashSet<NpcWear> WearItems { get; set; }
            [JsonProperty(En ? "Belt items" : "Быстрые слоты")] public HashSet<NpcBelt> BeltItems { get; set; }
            [JsonProperty(En ? "Kit (it is recommended to use the previous 2 settings to improve performance)" : "Kit (рекомендуется использовать предыдущие 2 пункта настройки для повышения производительности)")] public string Kit { get; set; }
        }

        public class PresetConfig
        {
            [JsonProperty(En ? "Minimum" : "Минимальное кол-во")] public int Min { get; set; }
            [JsonProperty(En ? "Maximum" : "Максимальное кол-во")] public int Max { get; set; }
            [JsonProperty(En ? "List of locations" : "Список расположений")] public HashSet<string> Positions { get; set; }
            [JsonProperty(En ? "NPCs setting" : "Настройки NPC")] public NpcConfig Config { get; set; }
            [JsonProperty(En ? "Which loot table should the plugin use? (0 - default; 1 - own; 2 - AlphaLoot; 3 - CustomLoot; 4 - loot table of the Rust objects; 5 - combine the 1 and 4 methods)" : "Какую таблицу предметов необходимо использовать? (0 - стандартную; 1 - собственную; 2 - AlphaLoot; 3 - CustomLoot; 4 - таблица предметов объектов Rust; 5 - совместить 1 и 4 методы)")] public int TypeLootTable { get; set; }
            [JsonProperty(En ? "Loot table from prefabs (if the loot table type is 4 or 5)" : "Таблица предметов из prefab-ов (если тип таблицы предметов - 4 или 5)")] public PrefabLootTableConfig PrefabLootTable { get; set; }
            [JsonProperty(En ? "Own loot table (if the loot table type is 1 or 5)" : "Собственная таблица предметов (если тип таблицы предметов - 1 или 5)")] public LootTableConfig OwnLootTable { get; set; }
        }

        public class AnimalConfig
        {
            [JsonProperty(En ? "Enabled? [true/false]" : "Включить появление животного? [true/false]")] public bool Enabled { get; set; }
            [JsonProperty(En ? "Type (1 - Polar Bear, 2 - Bear, 3 - Wolf, 4 - Boar, 5 - Stag, 6 - Chicken)" : "Тип (1 - Полярный медведь, 2 - Медведь, 3 - Волк, 4 - Кабан, 5 - Олень, 6 - Курица)")] public int Type { get; set; }
            [JsonProperty(En ? "Health" : "Кол-во ХП")] public float Health { get; set; }
            [JsonProperty(En ? "Chase Range" : "Дальность погони за целью")] public float ChaseRange { get; set; }
            [JsonProperty(En ? "Sense Range" : "Радиус обнаружения цели")] public float SenseRange { get; set; }
            [JsonProperty(En ? "Target Memory Duration [sec.]" : "Длительность памяти цели [sec.]")] public float MemoryDuration { get; set; }
            [JsonProperty(En ? "Attack Range" : "Радиус атаки")] public float AttackRange { get; set; }
            [JsonProperty(En ? "Attack Damage" : "Урон от атаки")] public float AttackDamage { get; set; }
            [JsonProperty(En ? "Attack Rate [sec.]" : "Минимальное время между атаками [sec.]")] public float AttackRate { get; set; }
            [JsonProperty(En ? "Detect the target only in the Animal's viewing vision cone? [true/false]" : "Обнаруживать цель только в углу обзора животного? [true/false]")] public bool CheckVisionCone { get; set; }
            [JsonProperty(En ? "Vision Cone" : "Угол обзора")] public float VisionCone { get; set; }
            [JsonProperty(En ? "Speed" : "Скорость")] public float Speed { get; set; }
        }

        public class BasePlayerConfig
        {
            [JsonProperty(En ? "Name" : "Название")] public string Name { get; set; }
            [JsonProperty(En ? "Health" : "Кол-во ХП")] public float Health { get; set; }
            [JsonProperty(En ? "Speed" : "Скорость")] public float Speed { get; set; }
            [JsonProperty(En ? "Wear items" : "Одежда")] public HashSet<NpcWear> WearItems { get; set; }
            [JsonProperty(En ? "Which loot table should the plugin use? (0 - own; 1 - loot table of the Rust objects; 2 - combine the 0 and 1 methods)" : "Какую таблицу предметов необходимо использовать? (0 - собственную; 1 - таблица предметов объектов Rust; 2 - совместить 0 и 1 методы)")] public int TypeLootTable { get; set; }
            [JsonProperty(En ? "Loot table from prefabs (if the loot table type is 1 or 2)" : "Таблица предметов из prefab-ов (если тип таблицы предметов - 1 или 2)")] public PrefabLootTableConfig PrefabLootTable { get; set; }
            [JsonProperty(En ? "Own loot table (if the loot table type is 0 or 2)" : "Собственная таблица предметов (если тип таблицы предметов - 0 или 2)")] public LootTableConfig OwnLootTable { get; set; }
        }

        private class PluginConfig
        {
            [JsonProperty(En ? "Minimum time between event [sec.]" : "Минимальное время между ивентами [sec.]")] public float MinStartTime { get; set; }
            [JsonProperty(En ? "Maximum time between event [sec.]" : "Максимальное время между ивентами [sec.]")] public float MaxStartTime { get; set; }
            [JsonProperty(En ? "Use minimum and maximum event start values? [true/false]" : "Активен ли таймер для запуска ивента? [true/false]")] public bool EnabledTimer { get; set; }
            [JsonProperty(En ? "Waiting time for players in the event area [sec.]" : "Время ожидания игроков в зоне ивента [sec.]")] public int FinishTime { get; set; }
            [JsonProperty(En ? "Delay of event start from the command or timer running (starts when chat message appears) [sec.]" : "Время до начала ивента после сообщения в чате [sec.]")] public float PreStartTime { get; set; }
            [JsonProperty(En ? "Notification time until the end of the event [sec.]" : "Время оповещения до окончания ивента [sec.]")] public int PreFinishTime { get; set; }
            [JsonProperty(En ? "List of crates inside the supermarket" : "Список ящиков внутри супермаркета")] public HashSet<CrateConfig> Crates { get; set; }
            [JsonProperty(En ? "Supermarket security guards settings inside" : "Настройки охранников супермаркета внутри")] public PresetConfig SecurityInside { get; set; }
            [JsonProperty(En ? "Supermarket security guards settings outside" : "Настройки охранников супермаркета снаружи")] public PresetConfig SecurityOutside { get; set; }
            [JsonProperty(En ? "Settings of the animal that walks with the supermarket guard outside" : "Настройки животного, которое ходит вместе с охранником супермаркета снаружи")] public AnimalConfig SecurityAnimal { get; set; }
            [JsonProperty(En ? "Settings of the debt collectors guards" : "Настройки охранников инкассатора")] public PresetConfig SecurityCollector { get; set; }
            [JsonProperty(En ? "Merchant Settings" : "Настройки торговца")] public BasePlayerConfig Cashier { get; set; }
            [JsonProperty(En ? "Debt Collector Settings" : "Настройки инкассатора")] public BasePlayerConfig Collector { get; set; }
            [JsonProperty(En ? "Marker configuration on the map" : "Настройка маркера на карте")] public MarkerConfig Marker { get; set; }
            [JsonProperty(En ? "Main marker settings for key event points shown on players screen" : "Настройки основного маркера на экране игрока")] public PointConfig MainPoint { get; set; }
            [JsonProperty(En ? "Additional marker settings for key event points shown on players screen" : "Настройки дополнительного маркера на экране игрока")] public PointConfig AdditionalPoint { get; set; }
            [JsonProperty(En ? "GUI setting" : "Настройки GUI")] public GuiConfig Gui { get; set; }
            [JsonProperty(En ? "Chat Message setting" : "Настройки сообщений в чате")] public ChatConfig Chat { get; set; }
            [JsonProperty(En ? "Facepunch Game Tips setting" : "Настройка сообщений Facepunch Game Tip")] public GameTipConfig GameTip { get; set; }
            [JsonProperty(En ? "GUI Announcements setting (only for GUIAnnouncements plugin)" : "Настройка GUI Announcements (только для тех, кто использует плагин GUI Announcements)")] public GuiAnnouncementsConfig GuiAnnouncements { get; set; }
            [JsonProperty(En ? "Notify setting (only for Notify plugin)" : "Настройка Notify (только для тех, кто использует плагин Notify)")] public NotifyConfig Notify { get; set; }
            [JsonProperty(En ? "The distance from the event to the player for global alerts (0 - no limit)" : "Расстояние от ивента до игрока для глобальных оповещений (0 - нет ограничений)")] public float DistanceAlerts { get; set; }
            [JsonProperty(En ? "Discord setting (only for DiscordMessages plugin)" : "Настройка оповещений в Discord (только для тех, кто использует плагин Discord Messages)")] public DiscordConfig Discord { get; set; }
            [JsonProperty(En ? "Radius of the event zone" : "Радиус зоны ивента")] public float Radius { get; set; }
            [JsonProperty(En ? "Do you create a PVP zone in the event area? (only for users TruePVE plugin) [true/false]" : "Создавать зону PVP в зоне проведения ивента? (только для тех, кто использует плагин TruePVE) [true/false]")] public bool IsCreateZonePvp { get; set; }
            [JsonProperty(En ? "PVE Mode Setting (only for users PveMode plugin)" : "Настройка PVE режима работы плагина (только для тех, кто использует плагин PveMode)")] public PveModeConfig PveMode { get; set; }
            [JsonProperty(En ? "Interrupt teleporting into and out of the event area? (only for users NTeleportation plugin) [true/false]" : "Запрещать телепорт в зоне проведения ивента? (только для тех, кто использует плагин NTeleportation) [true/false]")] public bool NTeleportationInterrupt { get; set; }
            [JsonProperty(En ? "Disable NPCs from the BetterNpc plugin on the monument while the event is on? [true/false]" : "Отключать NPC из плагина BetterNpc на монументе пока проходит ивент? [true/false]")] public bool RemoveBetterNpc { get; set; }
            [JsonProperty(En ? "Economy setting (total values will be added up and rewarded at the end of the event)" : "Настройка экономики (конечное значение суммируется и будет выдано игрокам по окончанию ивента)")] public EconomyConfig Economy { get; set; }
            [JsonProperty(En ? "List of commands banned in the event zone" : "Список команд запрещенных в зоне ивента")] public HashSet<string> Commands { get; set; }
            [JsonProperty(En ? "Should all doors open automatically when all NPCs outside are killed? [true/false]" : "Должны ли открываться все двери автоматически, когда будут убиты все Npc снаружи? [true/false]")] public bool CanOpenDoors { get; set; }
            [JsonProperty(En ? "The time the debt collector stays inside the supermarket [sec.]" : "Время нахождения инкассатора внутри супермаркета [sec.]")] public int TimeTransaction { get; set; }
            [JsonProperty(En ? "Configuration version" : "Версия конфигурации")] public VersionNumber PluginVersion { get; set; }

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig
                {
                    MinStartTime = 5400f,
                    MaxStartTime = 7200f,
                    EnabledTimer = true,
                    FinishTime = 1800,
                    PreStartTime = 300f,
                    PreFinishTime = 300,
                    Crates = new HashSet<CrateConfig>
                    {
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                            Position = "(0, 0, 0)",
                            Rotation = "(0, 0, 0)",
                            TypeLootTable = 0,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_normal.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 8, MaxAmount = 8, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" } }
                            }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/underwater_labs/crate_normal.prefab",
                            Position = "(-6.649, 2.277, -5.504)",
                            Rotation = "(0, 90, 0)",
                            TypeLootTable = 0,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/underwater_labs/crate_normal.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 8, MaxAmount = 8, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" } }
                            }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                            Position = "(4.306, 2.01, 4.71)",
                            Rotation = "(0, 0, 0)",
                            TypeLootTable = 0,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_normal.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 8, MaxAmount = 8, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" } }
                            }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                            Position = "(-6.409, 1.998, 2.067)",
                            Rotation = "(0, 90, 0)",
                            TypeLootTable = 0,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_normal.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 8, MaxAmount = 8, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" } }
                            }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_normal_2.prefab",
                            Position = "(-5.488, 0.521, 0.309)",
                            Rotation = "(0, 26.039, 0)",
                            TypeLootTable = 0,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_normal_2.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 5, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" } }
                            }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_normal_2.prefab",
                            Position = "(11.382, 0, 0.897)",
                            Rotation = "(0, 0, 0)",
                            TypeLootTable = 0,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_normal_2.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 5, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" } }
                            }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_normal_2.prefab",
                            Position = "(11.642, 0, -5.731)",
                            Rotation = "(0, 26.039, 0)",
                            TypeLootTable = 0,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_normal_2.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 5, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" } }
                            }
                        }
                    },
                    SecurityInside = new PresetConfig
                    {
                        Min = 2,
                        Max = 2,
                        Positions = new HashSet<string>
                        {
                            "(-3, 0.025, 2)",
                            "(8, 0.025, -2.5)"
                        },
                        Config = new NpcConfig
                        {
                            Name = "Security Inside",
                            Health = 125f,
                            RoamRange = 5f,
                            ChaseRange = 30f,
                            AttackRangeMultiplier = 3f,
                            SenseRange = 30f,
                            MemoryDuration = 15f,
                            DamageScale = 0.25f,
                            AimConeScale = 1.5f,
                            CheckVisionCone = false,
                            VisionCone = 135f,
                            Speed = 7.5f,
                            DisableRadio = false,
                            Stationary = false,
                            IsRemoveCorpse = true,
                            WearItems = new HashSet<NpcWear>
                            {
                                new NpcWear { ShortName = "hoodie", SkinId = 2380062321 },
                                new NpcWear { ShortName = "pants", SkinId = 1907958977 },
                                new NpcWear { ShortName = "hat.cap", SkinId = 915520890 },
                                new NpcWear { ShortName = "shoes.boots", SkinId = 869007492 }
                            },
                            BeltItems = new HashSet<NpcBelt>
                            {
                                new NpcBelt { ShortName = "pistol.m92", Amount = 1, SkinId = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                                new NpcBelt { ShortName = "icepick.salvaged", Amount = 1, SkinId = 2865984794, Mods = new HashSet<string>(), Ammo = string.Empty },
                                new NpcBelt { ShortName = "syringe.medical", Amount = 2, SkinId = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                            },
                            Kit = ""
                        },
                        TypeLootTable = 5,
                        PrefabLootTable = new PrefabLootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = false,
                            Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                        },
                        OwnLootTable = new LootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = false,
                            Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 8, MaxAmount = 33, Chance = 36f, IsBluePrint = false, SkinId = 0, Name = "" } }
                        }
                    },
                    SecurityOutside = new PresetConfig
                    {
                        Min = 3,
                        Max = 3,
                        Positions = new HashSet<string>
                        {
                            "(11, 0, 9)",
                            "(-11, 0, 7.5)",
                            "(8.5, 0, -11)"
                        },
                        Config = new NpcConfig
                        {
                            Name = "Security Outside",
                            Health = 125f,
                            RoamRange = 10f,
                            ChaseRange = 30f,
                            AttackRangeMultiplier = 3f,
                            SenseRange = 30f,
                            MemoryDuration = 15f,
                            DamageScale = 0.6f,
                            AimConeScale = 1.5f,
                            CheckVisionCone = false,
                            VisionCone = 135f,
                            Speed = 7.5f,
                            DisableRadio = false,
                            Stationary = false,
                            IsRemoveCorpse = true,
                            WearItems = new HashSet<NpcWear>
                            {
                                new NpcWear { ShortName = "hat.cap", SkinId = 2825016739 },
                                new NpcWear { ShortName = "pants", SkinId = 3023560021 },
                                new NpcWear { ShortName = "hoodie", SkinId = 3023560853 },
                                new NpcWear { ShortName = "sunglasses", SkinId = 0 },
                                new NpcWear { ShortName = "movembermoustache", SkinId = 0 },
                                new NpcWear { ShortName = "roadsign.gloves", SkinId = 2873444990 },
                                new NpcWear { ShortName = "shoes.boots", SkinId = 916448999 }
                            },
                            BeltItems = new HashSet<NpcBelt>
                            {
                                new NpcBelt { ShortName = "pistol.semiauto", Amount = 1, SkinId = 1408544688, Mods = new HashSet<string>(), Ammo = string.Empty },
                                new NpcBelt { ShortName = "syringe.medical", Amount = 2, SkinId = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                            },
                            Kit = ""
                        },
                        TypeLootTable = 5,
                        PrefabLootTable = new PrefabLootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = false,
                            Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                        },
                        OwnLootTable = new LootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = false,
                            Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 8, MaxAmount = 33, Chance = 36f, IsBluePrint = false, SkinId = 0, Name = "" } }
                        }
                    },
                    SecurityAnimal = new AnimalConfig
                    {
                        Enabled = true,
                        Type = 3,
                        Health = 150f,
                        ChaseRange = 45f,
                        SenseRange = 30f,
                        MemoryDuration = 15f,
                        AttackRange = 2f,
                        AttackDamage = 20f,
                        AttackRate = 2f,
                        CheckVisionCone = false,
                        VisionCone = 135f,
                        Speed = 9f
                    },
                    SecurityCollector = new PresetConfig
                    {
                        Min = 3,
                        Max = 3,
                        Positions = new HashSet<string>
                        {
                            "(11, 0, 9)",
                            "(-11, 0, 7.5)",
                            "(8.5, 0, -11)"
                        },
                        Config = new NpcConfig
                        {
                            Name = "Security Cash",
                            Health = 125f,
                            RoamRange = 10f,
                            ChaseRange = 30f,
                            AttackRangeMultiplier = 3f,
                            SenseRange = 30f,
                            MemoryDuration = 15f,
                            DamageScale = 0.3f,
                            AimConeScale = 1.5f,
                            CheckVisionCone = false,
                            VisionCone = 135f,
                            Speed = 7.5f,
                            DisableRadio = false,
                            Stationary = false,
                            IsRemoveCorpse = true,
                            WearItems = new HashSet<NpcWear>
                            {
                                new NpcWear { ShortName = "shoes.boots", SkinId = 2946627120 },
                                new NpcWear { ShortName = "burlap.gloves", SkinId = 2946636104 },
                                new NpcWear { ShortName = "metal.facemask", SkinId = 2815006919 },
                                new NpcWear { ShortName = "pants", SkinId = 2811533832 },
                                new NpcWear { ShortName = "hoodie", SkinId = 2811533300 },
                                new NpcWear { ShortName = "roadsign.kilt", SkinId = 2803024300 },
                                new NpcWear { ShortName = "jacket", SkinId = 2907457876 }
                            },
                            BeltItems = new HashSet<NpcBelt>
                            {
                                new NpcBelt { ShortName = "rifle.ak", Amount = 1, SkinId = 2940110554, Mods = new HashSet<string>(), Ammo = string.Empty },
                                new NpcBelt { ShortName = "syringe.medical", Amount = 2, SkinId = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                            },
                            Kit = ""
                        },
                        TypeLootTable = 5,
                        PrefabLootTable = new PrefabLootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = false,
                            Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                        },
                        OwnLootTable = new LootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = false,
                            Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 8, MaxAmount = 33, Chance = 36f, IsBluePrint = false, SkinId = 0, Name = "" } }
                        }
                    },
                    Cashier = new BasePlayerConfig
                    {
                        Name = "Cashier",
                        Health = 1000f,
                        Speed = 3f,
                        WearItems = new HashSet<NpcWear>
                        {
                            new NpcWear { ShortName = "burlap.trousers", SkinId = 2042089723 },
                            new NpcWear { ShortName = "burlap.shirt", SkinId = 2042087814 },
                            new NpcWear { ShortName = "shoes.boots", SkinId = 826908114 },
                            new NpcWear { ShortName = "hat.boonie", SkinId = 2040709757 }
                        },
                        TypeLootTable = 2,
                        PrefabLootTable = new PrefabLootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = true,
                            Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/underwater_labs/tech_parts_1.prefab" } }
                        },
                        OwnLootTable = new LootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = true,
                            Items = new List<ItemConfig>
                            {
                                new ItemConfig { ShortName = "scrap", MinAmount = 50, MaxAmount = 100, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" },
                                new ItemConfig { ShortName = "shotgun.double", MinAmount = 1, MaxAmount = 1, Chance = 5f, IsBluePrint = false, SkinId = 0, Name = "" },
                                new ItemConfig { ShortName = "smg.thompson", MinAmount = 1, MaxAmount = 1, Chance = 5f, IsBluePrint = false, SkinId = 0, Name = "" },
                                new ItemConfig { ShortName = "smg.2", MinAmount = 1, MaxAmount = 1, Chance = 5f, IsBluePrint = false, SkinId = 0, Name = "" },
                                new ItemConfig { ShortName = "shotgun.pump", MinAmount = 1, MaxAmount = 1, Chance = 5f, IsBluePrint = false, SkinId = 0, Name = "" },
                                new ItemConfig { ShortName = "pistol.semiauto", MinAmount = 1, MaxAmount = 1, Chance = 5f, IsBluePrint = false, SkinId = 0, Name = "" },
                                new ItemConfig { ShortName = "pistol.python", MinAmount = 1, MaxAmount = 1, Chance = 5f, IsBluePrint = false, SkinId = 0, Name = "" },
                                new ItemConfig { ShortName = "rifle.semiauto", MinAmount = 1, MaxAmount = 1, Chance = 5f, IsBluePrint = false, SkinId = 0, Name = "" }
                            }
                        }
                    },
                    Collector = new BasePlayerConfig
                    {
                        Name = "Collector",
                        Health = 1000f,
                        Speed = 3f,
                        WearItems = new HashSet<NpcWear>
                        {
                            new NpcWear { ShortName = "riot.helmet", SkinId = 1988565302 },
                            new NpcWear { ShortName = "shoes.boots", SkinId = 2186835886 },
                            new NpcWear { ShortName = "burlap.gloves", SkinId = 2186834533 },
                            new NpcWear { ShortName = "hoodie", SkinId = 2076428294 },
                            new NpcWear { ShortName = "pants", SkinId = 2076980911 },
                            new NpcWear { ShortName = "metal.plate.torso", SkinId = 1988550463 }
                        },
                        TypeLootTable = 2,
                        PrefabLootTable = new PrefabLootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = true,
                            Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/underwater_labs/tech_parts_1.prefab" } }
                        },
                        OwnLootTable = new LootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = true,
                            Items = new List<ItemConfig>
                            {
                                new ItemConfig { ShortName = "scrap", MinAmount = 50, MaxAmount = 100, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" },
                                new ItemConfig { ShortName = "shotgun.double", MinAmount = 1, MaxAmount = 1, Chance = 5f, IsBluePrint = false, SkinId = 0, Name = "" },
                                new ItemConfig { ShortName = "smg.thompson", MinAmount = 1, MaxAmount = 1, Chance = 5f, IsBluePrint = false, SkinId = 0, Name = "" },
                                new ItemConfig { ShortName = "smg.2", MinAmount = 1, MaxAmount = 1, Chance = 5f, IsBluePrint = false, SkinId = 0, Name = "" },
                                new ItemConfig { ShortName = "shotgun.pump", MinAmount = 1, MaxAmount = 1, Chance = 5f, IsBluePrint = false, SkinId = 0, Name = "" },
                                new ItemConfig { ShortName = "pistol.semiauto", MinAmount = 1, MaxAmount = 1, Chance = 5f, IsBluePrint = false, SkinId = 0, Name = "" },
                                new ItemConfig { ShortName = "pistol.python", MinAmount = 1, MaxAmount = 1, Chance = 5f, IsBluePrint = false, SkinId = 0, Name = "" },
                                new ItemConfig { ShortName = "rifle.semiauto", MinAmount = 1, MaxAmount = 1, Chance = 5f, IsBluePrint = false, SkinId = 0, Name = "" }
                            }
                        }
                    },
                    Marker = new MarkerConfig
                    {
                        Enabled = true,
                        Type = 1,
                        Radius = 0.37967f,
                        Alpha = 0.35f,
                        Color = new ColorConfig { R = 0.81f, G = 0.25f, B = 0.15f },
                        Text = "SupermarketEvent"
                    },
                    MainPoint = new PointConfig
                    {
                        Enabled = true,
                        Text = "◈",
                        Size = 45,
                        Color = "#CCFF00"
                    },
                    AdditionalPoint = new PointConfig
                    {
                        Enabled = true,
                        Text = "◆",
                        Size = 25,
                        Color = "#FFC700"
                    },
                    Gui = new GuiConfig
                    {
                        IsGui = true,
                        OffsetMinY = "-56"
                    },
                    Chat = new ChatConfig
                    {
                        IsChat = true,
                        Prefix = "[SupermarketEvent]"
                    },
                    GameTip = new GameTipConfig
                    {
                        IsGameTip = false,
                        Style = 2
                    },
                    GuiAnnouncements = new GuiAnnouncementsConfig
                    {
                        IsGuiAnnouncements = false,
                        BannerColor = "Orange",
                        TextColor = "White",
                        ApiAdjustVPosition = 0.03f
                    },
                    Notify = new NotifyConfig
                    {
                        IsNotify = false,
                        Type = 0
                    },
                    DistanceAlerts = 0f,
                    Discord = new DiscordConfig
                    {
                        IsDiscord = false,
                        WebhookUrl = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks",
                        EmbedColor = 13516583,
                        Keys = new HashSet<string>
                        {
                            "PreStart",
                            "Start",
                            "PreFinish",
                            "Finish",
                            "SupermarketAttacked",
                            "OpenDoors",
                            "CashierKilled",
                            "CollectorKilled"
                        }
                    },
                    Radius = 45f,
                    IsCreateZonePvp = false,
                    PveMode = new PveModeConfig
                    {
                        Pve = false,
                        Damage = 500f,
                        ScaleDamage = new Dictionary<string, float> { ["Npc"] = 1f, ["Animal"] = 1f },
                        LootCrate = false,
                        LootNpc = false,
                        DamageNpc = false,
                        TargetNpc = false,
                        CanEnter = false,
                        CanEnterCooldownPlayer = true,
                        TimeExitOwner = 300,
                        AlertTime = 60,
                        RestoreUponDeath = true,
                        CooldownOwner = 86400,
                        Darkening = 12
                    },
                    NTeleportationInterrupt = true,
                    RemoveBetterNpc = true,
                    Economy = new EconomyConfig
                    {
                        Plugins = new HashSet<string> { "Economics", "Server Rewards", "IQEconomic", "XPerience" },
                        Min = 0,
                        Crates = new Dictionary<string, double>
                        {
                            ["crate_normal"] = 0.3,
                            ["crate_normal_2"] = 0.2
                        },
                        Npc = 0.3,
                        Cashier = 0.8,
                        Collector = 0.9,
                        OpenDoors = 0.6,
                        Commands = new HashSet<string>()
                    },
                    Commands = new HashSet<string>
                    {
                        "/remove",
                        "remove.toggle"
                    },
                    CanOpenDoors = false,
                    TimeTransaction = 300,
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
                ["PreStart"] = "{0} In <color=#55aaff>{1}</color> a merchant will be working at one of the island's supermarkets",
                ["Start"] = "{0} The merchant <color=#738d43>arrived</color> on the island in square <color=#55aaff>{1}</color>. According to information from a reliable source, there will be a lot of valuable goods in the supermarket today",
                ["PreFinish"] = "{0} The supermarket event <color=#ce3f27>will end</color> in <color=#55aaff>{1}</color>",
                ["Finish"] = "{0} The supermarket event <color=#ce3f27>is over</color>",
                ["SupermarketAttacked"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>attacked</color> a supermarket with a merchant. An <color=#55aaff>armored helicopter</color> <color=#ce3f27>will arrive</color> soon to evacuate the merchant and basic goods from the island",
                ["HelicopterArrived"] = "{0} An <color=#55aaff>armored helicopter</color> with a cash collector and its guards <color=#ce3f27>arrived</color> at the supermarket to protect and evacuate the main goods and the merchant",
                ["OpenDoors"] = "{0} All <color=#55aaff>doors</color> in the supermarket <color=#738d43>are open</color>. Now it is <color=#738d43>possible to attack</color> the <color=#55aaff>merchant</color> and the <color=#55aaff>collector</color>",
                ["CashierKilled"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>killed</color> the merchant and <color=#738d43>may steal</color> the basic goods he was trying to evacuate",
                ["CollectorKilled"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>killed</color> the collector and <color=#738d43>may steal</color> the basic goods he was trying to evacuate",
                ["HelicopterDepartAllKilled"] = "{0} The armored helicopter has flown away. The <color=#55aaff>merchant</color> and the <color=#55aaff>collector</color> were <color=#738d43>killed</color>",
                ["HelicopterDepartCollectorKilled"] = "{0} The armored helicopter has flown away. <color=#ce3f27>The evacuation was successful</color> only for the <color=#55aaff>merchant</color>",
                ["HelicopterDepartCashierKilled"] = "{0} The armored helicopter has flown away. <color=#ce3f27>The evacuation was successful</color> only for the <color=#55aaff>collector</color>",
                ["HelicopterDepartAllAlive"] = "{0} The armored helicopter has flown away. <color=#ce3f27>The evacuation was successful</color> for the <color=#55aaff>merchant</color> and the <color=#55aaff>collector</color>",
                ["SetOwner"] = "{0} Player <color=#55aaff>{1}</color> <color=#738d43>has received</color> the owner status for the <color=#55aaff>Supermarket Event</color>",
                ["EventActive"] = "{0} This event is active now. To finish this event (<color=#55aaff>/supermarketstop</color>), then (<color=#55aaff>/supermarketstart</color>) to start the next one!",
                ["EnterPVP"] = "{0} You <color=#ce3f27>have entered</color> the PVP zone, now other players <color=#ce3f27>can damage</color> you!",
                ["ExitPVP"] = "{0} You <color=#738d43>have left</color> the PVP zone, now other players <color=#738d43>cannot damage</color> you!",
                ["NTeleportation"] = "{0} You <color=#ce3f27>cannot</color> teleport into the event zone!",
                ["SendEconomy"] = "{0} You <color=#738d43>have earned</color> <color=#55aaff>{1}</color> economics for participating in the event",
                ["NoCommand"] = "{0} You <color=#ce3f27>cannot</color> use this command in the event zone!"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PreStart"] = "{0} Через <color=#55aaff>{1}</color> на одном из супермаркетов острова будет работать торговец",
                ["Start"] = "{0} Торговец <color=#738d43>прибыл</color> на остров в квадрат <color=#55aaff>{1}</color>. По информации от надежного источника сегодня в супермаркете будет много ценных товаров",
                ["PreFinish"] = "{0} Ивент в супермаркете <color=#ce3f27>закончится</color> через <color=#55aaff>{1}</color>",
                ["Finish"] = "{0} Ивент в супермаркете <color=#ce3f27>окончен</color>",
                ["SupermarketAttacked"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>напал</color> на супермаркет с торговцем. В ближайшее время <color=#ce3f27>прибудет</color> <color=#55aaff>бронированный вертолет</color> для эвакуации торговца и основных товаров с острова",
                ["HelicopterArrived"] = "{0} <color=#55aaff>Бронированный вертолет</color> с инкассатором и его охраной <color=#ce3f27>прибыли</color> к супермаркету для защиты и эвакуации основных товаров и торговца",
                ["OpenDoors"] = "{0} Все <color=#55aaff>двери</color> в супермаркете <color=#738d43>открыты</color>. Теперь возможно <color=#738d43>совершить нападение</color> на <color=#55aaff>торговца</color> и <color=#55aaff>инкассатора</color>",
                ["CashierKilled"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>убил</color> торговца и <color=#738d43>может украсть</color> основные товары, которые он пытался эвакуировать",
                ["CollectorKilled"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>убил</color> инкассатора и <color=#738d43>может украсть</color> основные товары, которые он пытался эвакуировать",
                ["HelicopterDepartAllKilled"] = "{0} Бронированный вертолет улетел. <color=#55aaff>Торговец</color> и <color=#55aaff>инкассатор</color> были <color=#738d43>убиты</color>",
                ["HelicopterDepartCollectorKilled"] = "{0} Бронированный вертолет улетел. <color=#ce3f27>Эвакуация прошла успешно</color> только для <color=#55aaff>торговца</color>",
                ["HelicopterDepartCashierKilled"] = "{0} Бронированный вертолет улетел. <color=#ce3f27>Эвакуация прошла успешно</color> только для <color=#55aaff>инкассатора</color>",
                ["HelicopterDepartAllAlive"] = "{0} Бронированный вертолет улетел. <color=#ce3f27>Эвакуация прошла успешно</color> для <color=#55aaff>торговца</color> и <color=#55aaff>инкассатора</color>",
                ["SetOwner"] = "{0} Игрок <color=#55aaff>{1}</color> <color=#738d43>получил</color> статус владельца ивента для <color=#55aaff>Supermarket Event</color>",
                ["EventActive"] = "{0} Ивент в данный момент активен, сначала завершите текущий ивент (<color=#55aaff>/supermarketstop</color>), чтобы начать следующий!",
                ["EnterPVP"] = "{0} Вы <color=#ce3f27>вошли</color> в PVP зону, теперь другие игроки <color=#ce3f27>могут</color> наносить вам урон!",
                ["ExitPVP"] = "{0} Вы <color=#738d43>вышли</color> из PVP зоны, теперь другие игроки <color=#738d43>не могут</color> наносить вам урон!",
                ["NTeleportation"] = "{0} Вы <color=#ce3f27>не можете</color> телепортироваться в зоне ивента!",
                ["SendEconomy"] = "{0} Вы <color=#738d43>получили</color> <color=#55aaff>{1}</color> баллов в экономику за прохождение ивента",
                ["NoCommand"] = "{0} Вы <color=#ce3f27>не можете</color> использовать данную команду в зоне ивента!"
            }, this, "ru");
        }

        private string GetMessage(string langKey, string userId) => lang.GetMessage(langKey, _ins, userId);

        private string GetMessage(string langKey, string userId, params object[] args) => (args.Length == 0) ? GetMessage(langKey, userId) : string.Format(GetMessage(langKey, userId), args);
        #endregion Lang

        #region Oxide Hooks
        private static SupermarketEvent _ins;

        private void Init()
        {
            _ins = this;
            ToggleHooks(false);
        }

        private void OnServerInitialized()
        {
            if (GetMonument() == null)
            {
                PrintError("The Abandoned Supermarket location is missing on the map. The plugin cannot be loaded!");
                NextTick(() => Interface.Oxide.UnloadPlugin(Name));
                return;
            }
            CheckAllLootTables();
            ServerMgr.Instance.StartCoroutine(DownloadImages());
            StartTimer();
        }

        private void Unload()
        {
            if (Controller != null) Finish();
            _ins = null;
        }

        private object OnEntityTakeDamage(BuildingBlock buildingBlock, HitInfo info)
        {
            if (buildingBlock != null && Controller.Entities.Contains(buildingBlock)) return true;
            else return null;
        }

        private object OnEntityTakeDamage(DecorDeployable decorDeployable, HitInfo info)
        {
            if (decorDeployable != null && Controller.Entities.Contains(decorDeployable)) return true;
            else return null;
        }

        private object OnEntityTakeDamage(Door door, HitInfo info)
        {
            if (door != null && Controller.Entities.Contains(door)) return true;
            else return null;
        }

        private object OnEntityTakeDamage(Barricade barricade, HitInfo info)
        {
            if (barricade != null && Controller.Entities.Contains(barricade)) return true;
            else return null;
        }

        private object OnEntityTakeDamage(ModularCar car, HitInfo info)
        {
            if (car != null && car == Controller.Helicopter.Car) return true;
            else return null;
        }

        private object OnEntityTakeDamage(VehicleModuleSeating module, HitInfo info)
        {
            if (module != null && Controller.Helicopter.Modules.Contains(module)) return true;
            else return null;
        }

        private object OnEntityTakeDamage(ElectricBattery battery, HitInfo info)
        {
            if (battery != null && battery == Controller.Helicopter.Battery) return true;
            else return null;
        }

        private object OnEntityTakeDamage(AttackHelicopter helicopter, HitInfo info)
        {
            if (helicopter != null && helicopter == Controller.Helicopter.Helicopter) return true;
            else return null;
        }

        private object OnEntityTakeDamage(BasePlayer basePlayer, HitInfo info)
        {
            if (basePlayer == null || info == null) return null;

            if (basePlayer == Controller.Helicopter.Driver) return true;

            BasePlayer attacker = info.InitiatorPlayer;

            if (basePlayer == Controller.Cashier || basePlayer == Controller.Collector)
            {
                if (!attacker.IsPlayer() || !Controller.Players.Contains(attacker)) return true;
                if (Controller.CodeLocks.Count == 0 || Controller.TimeTransaction == 0) return null;
                else return true;
            }

            if (basePlayer is ScientistNPC)
            {
                ScientistNPC npc = basePlayer as ScientistNPC;
                if (!Controller.Scientists.Contains(npc)) return null;
                if (Controller.Helicopter.CurrentStage == 0 && attacker.IsPlayer())
                {
                    AlertToAllPlayers("SupermarketAttacked", _config.Chat.Prefix, attacker.displayName);
                    Controller.SpawnArmoredHelicopter();
                }
                BaseEntity weaponPrefab = info.WeaponPrefab;
                if (weaponPrefab != null && (weaponPrefab.ShortPrefabName == "explosive.timed.deployed" || weaponPrefab.ShortPrefabName == "car_chassis_4module.entity")) return true;
            }

            return null;
        }

        private object OnEntityTakeDamage(BaseAnimalNPC animal, HitInfo info)
        {
            if (animal == null || info == null) return null;
            BasePlayer attacker = info.InitiatorPlayer;
            if (Controller.Animals.Contains(animal) && Controller.Helicopter.CurrentStage == 0 && attacker.IsPlayer())
            {
                AlertToAllPlayers("SupermarketAttacked", _config.Chat.Prefix, attacker.displayName);
                Controller.SpawnArmoredHelicopter();
            }
            return null;
        }

        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            if (planner == null) return null;
            BasePlayer player = planner.GetOwnerPlayer();
            if (player == null) return null;
            if (Controller.Players.Contains(player)) return true;
            return null;
        }

        private object CanChangeGrade(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade, ulong skin)
        {
            if (block != null && Controller.Entities.Contains(block)) return false;
            else return null;
        }

        private object OnStructureRotate(BuildingBlock block, BasePlayer player)
        {
            if (block != null && Controller.Entities.Contains(block)) return true;
            else return null;
        }

        private void OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
        {
            if (codeLock == null || !player.IsPlayer() || string.IsNullOrEmpty(code)) return;
            Controller.TryToOpenDoor(player, codeLock, code);
        }

        private object OnNpcTarget(BaseEntity attacker, BasePlayer victim)
        {
            if (attacker == null || victim == null) return null;
            if (victim == Controller.Cashier || victim == Controller.Collector || victim == Controller.Helicopter.Driver) return true;
            return null;
        }

        private void OnEntityDeath(ScientistNPC npc, HitInfo info)
        {
            if (npc == null || info == null) return;
            BasePlayer attacker = info.InitiatorPlayer;
            if (!attacker.IsPlayer()) return;
            if (Controller.Scientists.Contains(npc)) ActionEconomy(attacker.userID, "Npc");
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (!_config.Marker.Enabled || Controller == null || !player.IsPlayer()) return;
            if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot)) timer.In(2f, () => OnPlayerConnected(player));
            else Controller.UpdateMapMarkers();
        }

        private object OnPlayerDeath(BasePlayer basePlayer, HitInfo info)
        {
            if (basePlayer == null || info == null) return null;

            BasePlayer attackerBasePlayer = info.InitiatorPlayer;

            if (attackerBasePlayer.IsPlayer())
            {
                if (basePlayer == Controller.Cashier)
                {
                    Controller.IsKillCashier = true;
                    ActionEconomy(basePlayer.userID, "Cashier");
                    AlertToAllPlayers("CashierKilled", _config.Chat.Prefix, attackerBasePlayer.displayName);
                    Controller.TryDepartHelicopter();
                }
                else if (basePlayer == Controller.Collector)
                {
                    Controller.IsKillCollector = true;
                    ActionEconomy(basePlayer.userID, "Collector");
                    AlertToAllPlayers("CollectorKilled", _config.Chat.Prefix, attackerBasePlayer.displayName);
                    Controller.TryDepartHelicopter();
                }
            }

            if (Controller.Players.Contains(basePlayer)) Controller.ExitPlayer(basePlayer);

            return null;
        }

        private void OnEntityKill(LootContainer entity)
        {
            if (entity != null && Controller.Crates.ContainsKey(entity))
                Controller.Crates.Remove(entity);
        }

        private HashSet<ulong> LootableCrates { get; } = new HashSet<ulong>();

        private void OnLootEntity(BasePlayer player, LootContainer container)
        {
            if (!player.IsPlayer() || container == null || LootableCrates.Contains(container.net.ID.Value)) return;
            if (Controller.Crates.ContainsKey(container))
            {
                LootableCrates.Add(container.net.ID.Value);
                ActionEconomy(player.userID, "Crates", container.ShortPrefabName);
            }
        }

        private object OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            if (player != null && Controller.Players.Contains(player))
            {
                command = "/" + command;
                if (_config.Commands.Contains(command.ToLower()))
                {
                    AlertToPlayer(player, GetMessage("NoCommand", player.UserIDString, _config.Chat.Prefix));
                    return true;
                }
            }
            return null;
        }

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.cmd == null) return null;
            BasePlayer player = arg.Player();
            if (player != null && Controller.Players.Contains(player))
            {
                if (_config.Commands.Contains(arg.cmd.Name.ToLower()) || _config.Commands.Contains(arg.cmd.FullName.ToLower()))
                {
                    AlertToPlayer(player, GetMessage("NoCommand", player.UserIDString, _config.Chat.Prefix));
                    return true;
                }
            }
            return null;
        }
        #endregion Oxide Hooks

        #region Controller
        public class Prefab { public string Path; public Vector3 Pos; public Vector3 Rot; public ulong Skin; }
        internal HashSet<Prefab> Prefabs { get; } = new HashSet<Prefab>
        {
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(-3.117f, -0.001f, -6.904f), Rot = new Vector3(0f, 90f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(-6.009f, -0.001f, -6.902f), Rot = new Vector3(0f, 90f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(-7.415f, -0.001f, -5.482f), Rot = new Vector3(0f, 180f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(-7.415f, -0.001f, -2.482f), Rot = new Vector3(0f, 180f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(-7.415f, -0.001f, 0.518f), Rot = new Vector3(0f, 180f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(-7.415f, -0.001f, 3.518f), Rot = new Vector3(0f, 180f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(-7.416f, -0.001f, 4.012f), Rot = new Vector3(0f, 180f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(-3.7f, -0.001f, 10.404f), Rot = new Vector3(0f, 270f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(-0.7f, -0.001f, 10.404f), Rot = new Vector3(0f, 270f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(2.3f, -0.001f, 10.404f), Rot = new Vector3(0f, 270f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(5.3f, -0.001f, 10.404f), Rot = new Vector3(0f, 270f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(6.512f, -0.001f, 10.406f), Rot = new Vector3(0f, 270f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(9.674f, -0.001f, 5.408f), Rot = new Vector3(0f, 270f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(11.516f, 0f, 5.409f), Rot = new Vector3(0f, 270f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(12.906f, 0f, 3.987f), Rot = new Vector3(0f, 0f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(12.908f, -0.002f, 3.109f), Rot = new Vector3(0f, 0f, 0f), Skin = 10223 },

            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-3.117f, 2.999f, -6.904f), Rot = new Vector3(0f, 90f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-5.994f, 2.999f, -6.904f), Rot = new Vector3(0f, 90f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-7.418f, 2.999f, -5.497f), Rot = new Vector3(0f, 180f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-7.418f, 2.999f, -2.497f), Rot = new Vector3(0f, 180f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-7.418f, 2.999f, 0.503f), Rot = new Vector3(0f, 180f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-7.418f, 2.999f, 3.503f), Rot = new Vector3(0f, 180f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-7.419f, 2.999f, 3.997f), Rot = new Vector3(0f, 180f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-3.715f, 2.999f, 10.406f), Rot = new Vector3(0f, 270f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-0.715f, 2.999f, 10.406f), Rot = new Vector3(0f, 270f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(2.285f, 2.999f, 10.406f), Rot = new Vector3(0f, 270f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(5.285f, 2.999f, 10.406f), Rot = new Vector3(0f, 270f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(6.497f, 2.998f, 10.408f), Rot = new Vector3(0f, 270f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(9.66f, 2.998f, 5.41f), Rot = new Vector3(0f, 270f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(11.502f, 3f, 5.411f), Rot = new Vector3(0f, 270f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(12.908f, 3f, 4.001f), Rot = new Vector3(0f, 0f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(12.91f, 2.998f, 3.124f), Rot = new Vector3(0f, 0f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(7.879f, 2.997f, -6.404f), Rot = new Vector3(0f, 90f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(7.879f, 2.997f, -6.398f), Rot = new Vector3(0f, 270f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(7.975f, 1.414f, 9.618f), Rot = new Vector3(90f, 0f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(7.974f, 2.547f, 9.617f), Rot = new Vector3(90f, 0f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(7.974f, 3.155f, 8.991f), Rot = new Vector3(0f, 180f, 180f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(7.977f, 3.154f, 6.951f), Rot = new Vector3(0f, 180f, 180f), Skin = 10223 },

            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-6.01f, 1.481f, 5.494f), Rot = new Vector3(90f, 270f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-6.01f, 2.519f, 5.493f), Rot = new Vector3(90f, 270f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-5.235f, 1.481f, 5.496f), Rot = new Vector3(90f, 270f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-5.235f, 2.519f, 5.494f), Rot = new Vector3(90f, 270f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-5.219f, 1.481f, 6.894f), Rot = new Vector3(90f, 180f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-5.22f, 2.553f, 6.895f), Rot = new Vector3(90f, 180f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-5.215f, 1.481f, 9.603f), Rot = new Vector3(90f, 180f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-5.217f, 2.553f, 9.604f), Rot = new Vector3(90f, 180f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-5.22f, 2.553f, 10.49f), Rot = new Vector3(90f, 180f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-5.218f, 1.481f, 10.489f), Rot = new Vector3(90f, 180f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-5.151f, 2.17f, 7.44f), Rot = new Vector3(0f, 180f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-5.152f, 2.573f, 7.111f), Rot = new Vector3(0f, 180f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-5.154f, 2.573f, 8.988f), Rot = new Vector3(0f, 180f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(7.973f, 1.481f, 6.866f), Rot = new Vector3(90f, 0f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(7.975f, 2.553f, 6.865f), Rot = new Vector3(90f, 0f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(7.976f, 2.555f, 5.481f), Rot = new Vector3(90f, 0f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(7.975f, 1.483f, 5.482f), Rot = new Vector3(90f, 0f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(7.976f, 2.553f, 9.055f), Rot = new Vector3(0f, 0f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(7.977f, 2.553f, 7.009f), Rot = new Vector3(0f, 0f, 0f), Skin = 10223 },

            new Prefab { Path = "assets/prefabs/building core/wall.doorway/wall.doorway.prefab", Pos = new Vector3(7.879f, -0.005f, -6.403f), Rot = new Vector3(0f, 90f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.doorway/wall.doorway.prefab", Pos = new Vector3(7.879f, -0.005f, -6.402f), Rot = new Vector3(0f, 270f, 0f), Skin = 10223 },

            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(12.796f, -0.049f, 3.078f), Rot = new Vector3(0f, 0f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(12.598f, -0.049f, 3.077f), Rot = new Vector3(0f, 0f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(12.796f, 1.043f, 3.08f), Rot = new Vector3(0f, 0f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(12.598f, 1.043f, 3.079f), Rot = new Vector3(0f, 0f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(-3.106f, 0f, -6.806f), Rot = new Vector3(0f, 270f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(-3.107f, 0f, -6.608f), Rot = new Vector3(0f, 270f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(-3.104f, 1.02f, -6.608f), Rot = new Vector3(0f, 270f, 0f), Skin = 10223 },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(-3.103f, 1.02f, -6.806f), Rot = new Vector3(0f, 270f, 0f), Skin = 10223 },

            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-7.669f, 3.867f, -5.74f), Rot = new Vector3(0f, 0f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-7.668f, 4.155f, -5.74f), Rot = new Vector3(0f, 0f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-7.669f, 3.867f, -2.767f), Rot = new Vector3(0f, 0f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-7.668f, 4.155f, -2.767f), Rot = new Vector3(0f, 0f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-7.668f, 4.155f, 0.193f), Rot = new Vector3(0f, 0f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-7.669f, 3.867f, 0.193f), Rot = new Vector3(0f, 0f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-7.668f, 4.155f, 3.167f), Rot = new Vector3(0f, 0f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-7.669f, 3.867f, 3.167f), Rot = new Vector3(0f, 0f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-6.26f, 3.867f, -7.349f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-6.26f, 4.155f, -7.348f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-3.287f, 3.867f, -7.349f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-3.287f, 4.155f, -7.348f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(2.66f, 4.155f, -7.348f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(2.66f, 3.867f, -7.349f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-0.314f, 4.155f, -7.348f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-0.314f, 3.867f, -7.349f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(5.629f, 3.867f, -7.349f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(5.629f, 4.155f, -7.348f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(8.602f, 3.867f, -7.349f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(8.602f, 4.155f, -7.348f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(11.579f, 3.867f, -7.349f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(11.579f, 4.155f, -7.348f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-7.668f, 4.153f, 4.275f), Rot = new Vector3(0f, 0f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-7.669f, 3.865f, 4.275f), Rot = new Vector3(0f, 0f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-6.263f, 3.869f, 5.684f), Rot = new Vector3(0f, 90f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-6.263f, 4.157f, 5.683f), Rot = new Vector3(0f, 90f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-3.3f, 4.159f, 5.685f), Rot = new Vector3(0f, 90f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-3.3f, 3.87f, 5.686f), Rot = new Vector3(0f, 90f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-0.34f, 3.869f, 5.684f), Rot = new Vector3(0f, 90f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-0.34f, 4.157f, 5.683f), Rot = new Vector3(0f, 90f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(2.636f, 4.161f, 5.684f), Rot = new Vector3(0f, 90f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(2.636f, 3.873f, 5.685f), Rot = new Vector3(0f, 90f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(5.582f, 3.872f, 5.686f), Rot = new Vector3(0f, 90f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(5.582f, 4.16f, 5.685f), Rot = new Vector3(0f, 90f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(8.521f, 4.159f, 5.683f), Rot = new Vector3(0f, 90f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(8.521f, 3.871f, 5.684f), Rot = new Vector3(0f, 90f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(11.409f, 3.871f, 5.685f), Rot = new Vector3(0f, 90f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(11.409f, 4.159f, 5.683f), Rot = new Vector3(0f, 90f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(11.86f, 4.158f, 5.685f), Rot = new Vector3(0f, 90f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(11.86f, 3.87f, 5.686f), Rot = new Vector3(0f, 90f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(13.23f, 3.867f, 4.071f), Rot = new Vector3(0f, 180f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(13.229f, 4.155f, 4.071f), Rot = new Vector3(0f, 180f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(13.23f, 3.867f, 1.098f), Rot = new Vector3(0f, 180f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(13.229f, 4.155f, 1.098f), Rot = new Vector3(0f, 180f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(13.23f, 3.867f, -1.862f), Rot = new Vector3(0f, 180f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(13.229f, 4.155f, -1.862f), Rot = new Vector3(0f, 180f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(13.23f, 3.867f, -4.836f), Rot = new Vector3(0f, 180f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(13.229f, 4.153f, -5.944f), Rot = new Vector3(0f, 180f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(13.23f, 3.865f, -5.944f), Rot = new Vector3(0f, 180f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(13.229f, 4.155f, -4.836f), Rot = new Vector3(0f, 180f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(13.234f, 3.866f, 4.269f), Rot = new Vector3(0f, 180f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(13.233f, 4.154f, 4.269f), Rot = new Vector3(0f, 180f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(11.819f, 3.867f, -7.349f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(11.819f, 4.155f, -7.348f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(11.82f, 3.864f, -7.163f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(11.82f, 4.152f, -7.162f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(11.58f, 3.864f, -7.163f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(11.58f, 4.152f, -7.162f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(8.603f, 3.864f, -7.163f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(8.603f, 4.152f, -7.162f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(5.63f, 3.864f, -7.163f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(5.63f, 4.152f, -7.162f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(2.661f, 4.152f, -7.162f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(2.661f, 3.864f, -7.163f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-0.313f, 3.864f, -7.163f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-0.313f, 4.152f, -7.162f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-3.286f, 3.864f, -7.163f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-3.286f, 4.152f, -7.162f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-6.259f, 3.864f, -7.163f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-6.259f, 4.152f, -7.162f), Rot = new Vector3(0f, 270f, 0f), Skin = 10225 },

            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-0.121f, 3.167f, -6.497f), Rot = new Vector3(0f, 90f, 270f), Skin = 3037014308 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(2.864f, 3.167f, -6.497f), Rot = new Vector3(0f, 90f, 270f), Skin = 3037014308 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(4.918f, 3.167f, -6.497f), Rot = new Vector3(0f, 90f, 270f), Skin = 3037014308 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-0.121f, 1.442f, -6.497f), Rot = new Vector3(0f, 90f, 270f), Skin = 3037014308 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(2.864f, 1.442f, -6.497f), Rot = new Vector3(0f, 90f, 270f), Skin = 3037014308 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(4.918f, 1.442f, -6.497f), Rot = new Vector3(0f, 90f, 270f), Skin = 3037014308 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-0.121f, -0.277f, -6.497f), Rot = new Vector3(0f, 90f, 270f), Skin = 3037014308 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(2.864f, -0.277f, -6.497f), Rot = new Vector3(0f, 90f, 270f), Skin = 3037014308 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(4.918f, -0.277f, -6.497f), Rot = new Vector3(0f, 90f, 270f), Skin = 3037014308 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(10.858f, 3.167f, -6.497f), Rot = new Vector3(0f, 90f, 270f), Skin = 3037014308 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(10.858f, 1.442f, -6.497f), Rot = new Vector3(0f, 90f, 270f), Skin = 3037014308 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(10.858f, -0.277f, -6.497f), Rot = new Vector3(0f, 90f, 270f), Skin = 3037014308 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(12.477f, 3.167f, -4.992f), Rot = new Vector3(0f, 0f, 270f), Skin = 3037014308 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(12.477f, 3.167f, -2.013f), Rot = new Vector3(0f, 0f, 270f), Skin = 3037014308 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(12.477f, 3.167f, 0.967f), Rot = new Vector3(0f, 0f, 270f), Skin = 3037014308 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(12.477f, 1.442f, 0.967f), Rot = new Vector3(0f, 0f, 270f), Skin = 3037014308 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(12.477f, 1.442f, -2.013f), Rot = new Vector3(0f, 0f, 270f), Skin = 3037014308 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(12.477f, 1.442f, -4.992f), Rot = new Vector3(0f, 0f, 270f), Skin = 3037014308 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(12.477f, -0.277f, -4.992f), Rot = new Vector3(0f, 0f, 270f), Skin = 3037014308 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(12.477f, -0.277f, -2.013f), Rot = new Vector3(0f, 0f, 270f), Skin = 3037014308 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(12.477f, -0.277f, 0.967f), Rot = new Vector3(0f, 0f, 270f), Skin = 3037014308 },

            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-4.024f, 0.025f, -5.542f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-5.433f, 0.023f, -5.542f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-4.024f, 0.025f, -3.806f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-5.433f, 0.023f, -3.806f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-4.024f, 0.025f, -2.084f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-5.433f, 0.023f, -2.084f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-4.024f, 0.025f, -0.355f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-5.433f, 0.023f, -0.355f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-4.024f, 0.025f, 1.363f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-5.433f, 0.023f, 1.363f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-4.024f, 0.025f, 3.092f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-5.433f, 0.023f, 3.092f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-4.024f, 0.025f, 4.608f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-5.433f, 0.023f, 4.608f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-1.038f, 0.025f, 4.608f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-1.038f, 0.025f, 3.092f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-1.038f, 0.025f, 1.363f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-1.038f, 0.025f, -0.355f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-1.038f, 0.025f, -2.084f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-1.038f, 0.025f, -3.806f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-1.038f, 0.025f, -5.542f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(1.948f, 0.025f, -5.542f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(1.948f, 0.025f, -3.806f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(1.948f, 0.025f, -2.084f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(1.948f, 0.025f, -0.355f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(1.948f, 0.025f, 1.363f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(1.948f, 0.025f, 3.092f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(1.948f, 0.025f, 4.608f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(4.922f, 0.025f, 4.608f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(4.922f, 0.025f, 3.092f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(4.922f, 0.025f, 1.363f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(4.922f, 0.025f, -0.355f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(4.922f, 0.025f, -2.084f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(4.922f, 0.025f, -3.806f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(4.922f, 0.025f, -5.542f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(7.9f, 0.025f, -5.542f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(7.9f, 0.025f, -3.806f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(7.9f, 0.025f, -2.084f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(7.9f, 0.025f, -0.355f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(7.9f, 0.025f, 1.363f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(7.9f, 0.025f, 3.092f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(7.9f, 0.025f, 4.608f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(10.89f, 0.025f, 4.608f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(10.89f, 0.025f, 3.092f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(10.89f, 0.025f, 1.363f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(10.89f, 0.025f, -0.355f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(10.89f, 0.025f, -2.084f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(10.89f, 0.025f, -3.806f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(10.89f, 0.025f, -5.542f), Rot = new Vector3(0f, 90f, 0f), Skin = 2991748206 },

            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(7.744f, 1.024f, 5.247f), Rot = new Vector3(0f, 270f, 90f), Skin = 2869553771 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(7.744f, 2.751f, 5.247f), Rot = new Vector3(0f, 270f, 90f), Skin = 2869553771 },

            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(4.36f, 2.909f, 5.247f), Rot = new Vector3(0f, 270f, 90f), Skin = 2530067046 },

            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(12.375f, 3.01f, 3.381f), Rot = new Vector3(0f, 0f, 90f), Skin = 2544663108 },

            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-7f, 2.947f, 3.527f), Rot = new Vector3(0f, 180f, 90f), Skin = 1701785217 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(-3.228f, 2.976f, -6.375f), Rot = new Vector3(0f, 90f, 90f), Skin = 1701785217 },
            new Prefab { Path = "assets/prefabs/deployable/rug/rug.deployed.prefab", Pos = new Vector3(7.879f, 4.767f, -7.426f), Rot = new Vector3(0f, 270f, 90f), Skin = 1701785217 },

            new Prefab { Path = "assets/prefabs/building/door.hinged/door.hinged.wood.prefab", Pos = new Vector3(7.879f, -0.005f, -6.402f), Rot = new Vector3(0f, 90f, 0f), Skin = 2963929253 },
            new Prefab { Path = "assets/prefabs/building/door.hinged/door.hinged.wood.prefab", Pos = new Vector3(-5.1f, -0.05f, 7.5f), Rot = new Vector3(0f, 180f, 0f), Skin = 2963929253 },
            new Prefab { Path = "assets/prefabs/building/door.hinged/door.hinged.wood.prefab", Pos = new Vector3(7.875f, -0.025f, 9f), Rot = new Vector3(0f, 0f, 0f), Skin = 2963929253 },

            new Prefab { Path = "assets/prefabs/deployable/barricades/barricade.sandbags.prefab", Pos = new Vector3(7.879f, -1.21f, -6.373f), Rot = new Vector3(0f, 0f, 0f), Skin = 0 },
            new Prefab { Path = "assets/prefabs/deployable/barricades/barricade.sandbags.prefab", Pos = new Vector3(9.979f, -1.21f, -6.373f), Rot = new Vector3(0f, 0f, 0f), Skin = 0 },
            new Prefab { Path = "assets/prefabs/deployable/barricades/barricade.sandbags.prefab", Pos = new Vector3(5.779f, -1.21f, -6.373f), Rot = new Vector3(0f, 0f, 0f), Skin = 0 }
        };

        internal HashSet<Vector3> Marker { get; } = new HashSet<Vector3>
        {
            new Vector3(46f, 0f, 0f),
            new Vector3(46f, 0f, -2f),
            new Vector3(46f, 0f, -4f),
            new Vector3(44f, 0f, 12f),
            new Vector3(44f, 0f, 10f),
            new Vector3(44f, 0f, 8f),
            new Vector3(44f, 0f, 6f),
            new Vector3(44f, 0f, 4f),
            new Vector3(44f, 0f, 2f),
            new Vector3(44f, 0f, 0f),
            new Vector3(44f, 0f, -2f),
            new Vector3(44f, 0f, -4f),
            new Vector3(44f, 0f, -6f),
            new Vector3(44f, 0f, -8f),
            new Vector3(44f, 0f, -10f),
            new Vector3(44f, 0f, -12f),
            new Vector3(44f, 0f, -14f),
            new Vector3(42f, 0f, 16f),
            new Vector3(42f, 0f, 14f),
            new Vector3(42f, 0f, 12f),
            new Vector3(42f, 0f, 10f),
            new Vector3(42f, 0f, 8f),
            new Vector3(42f, 0f, 6f),
            new Vector3(42f, 0f, 4f),
            new Vector3(42f, 0f, 2f),
            new Vector3(42f, 0f, -4f),
            new Vector3(42f, 0f, -6f),
            new Vector3(42f, 0f, -8f),
            new Vector3(42f, 0f, -10f),
            new Vector3(42f, 0f, -12f),
            new Vector3(42f, 0f, -14f),
            new Vector3(42f, 0f, -16f),
            new Vector3(42f, 0f, -18f),
            new Vector3(42f, 0f, -20f),
            new Vector3(40f, 0f, 20f),
            new Vector3(40f, 0f, 18f),
            new Vector3(40f, 0f, 16f),
            new Vector3(40f, 0f, 14f),
            new Vector3(40f, 0f, -16f),
            new Vector3(40f, 0f, -18f),
            new Vector3(40f, 0f, -20f),
            new Vector3(40f, 0f, -22f),
            new Vector3(38f, 0f, 24f),
            new Vector3(38f, 0f, 22f),
            new Vector3(38f, 0f, 20f),
            new Vector3(38f, 0f, 18f),
            new Vector3(38f, 0f, -22f),
            new Vector3(38f, 0f, -24f),
            new Vector3(38f, 0f, -26f),
            new Vector3(36f, 0f, 28f),
            new Vector3(36f, 0f, 26f),
            new Vector3(36f, 0f, 24f),
            new Vector3(36f, 0f, 22f),
            new Vector3(36f, 0f, -24f),
            new Vector3(36f, 0f, -26f),
            new Vector3(36f, 0f, -28f),
            new Vector3(36f, 0f, -30f),
            new Vector3(34f, 0f, 30f),
            new Vector3(34f, 0f, 28f),
            new Vector3(34f, 0f, 26f),
            new Vector3(34f, 0f, -28f),
            new Vector3(34f, 0f, -30f),
            new Vector3(34f, 0f, -32f),
            new Vector3(32f, 0f, 32f),
            new Vector3(32f, 0f, 30f),
            new Vector3(32f, 0f, 28f),
            new Vector3(32f, 0f, -30f),
            new Vector3(32f, 0f, -32f),
            new Vector3(32f, 0f, -34f),
            new Vector3(30f, 0f, 34f),
            new Vector3(30f, 0f, 32f),
            new Vector3(30f, 0f, 30f),
            new Vector3(30f, 0f, -32f),
            new Vector3(30f, 0f, -34f),
            new Vector3(30f, 0f, -36f),
            new Vector3(28f, 0f, 36f),
            new Vector3(28f, 0f, 34f),
            new Vector3(28f, 0f, 32f),
            new Vector3(28f, 0f, -34f),
            new Vector3(28f, 0f, -36f),
            new Vector3(28f, 0f, -38f),
            new Vector3(26f, 0f, 38f),
            new Vector3(26f, 0f, 36f),
            new Vector3(26f, 0f, 34f),
            new Vector3(26f, 0f, -36f),
            new Vector3(26f, 0f, -38f),
            new Vector3(26f, 0f, -40f),
            new Vector3(24f, 0f, 38f),
            new Vector3(24f, 0f, 36f),
            new Vector3(24f, 0f, 14f),
            new Vector3(24f, 0f, 12f),
            new Vector3(24f, 0f, 10f),
            new Vector3(24f, 0f, -38f),
            new Vector3(24f, 0f, -40f),
            new Vector3(22f, 0f, 40f),
            new Vector3(22f, 0f, 38f),
            new Vector3(22f, 0f, 14f),
            new Vector3(22f, 0f, 12f),
            new Vector3(22f, 0f, 10f),
            new Vector3(22f, 0f, 8f),
            new Vector3(22f, 0f, 6f),
            new Vector3(22f, 0f, 4f),
            new Vector3(22f, 0f, -40f),
            new Vector3(22f, 0f, -42f),
            new Vector3(20f, 0f, 40f),
            new Vector3(20f, 0f, 38f),
            new Vector3(20f, 0f, 14f),
            new Vector3(20f, 0f, 12f),
            new Vector3(20f, 0f, 10f),
            new Vector3(20f, 0f, 8f),
            new Vector3(20f, 0f, 6f),
            new Vector3(20f, 0f, 4f),
            new Vector3(20f, 0f, 2f),
            new Vector3(20f, 0f, 0f),
            new Vector3(20f, 0f, -2f),
            new Vector3(20f, 0f, -40f),
            new Vector3(20f, 0f, -42f),
            new Vector3(20f, 0f, -44f),
            new Vector3(18f, 0f, 42f),
            new Vector3(18f, 0f, 40f),
            new Vector3(18f, 0f, 14f),
            new Vector3(18f, 0f, 12f),
            new Vector3(18f, 0f, 10f),
            new Vector3(18f, 0f, 6f),
            new Vector3(18f, 0f, 4f),
            new Vector3(18f, 0f, 2f),
            new Vector3(18f, 0f, 0f),
            new Vector3(18f, 0f, -2f),
            new Vector3(18f, 0f, -4f),
            new Vector3(18f, 0f, -6f),
            new Vector3(18f, 0f, -8f),
            new Vector3(18f, 0f, -42f),
            new Vector3(18f, 0f, -44f),
            new Vector3(16f, 0f, 42f),
            new Vector3(16f, 0f, 40f),
            new Vector3(16f, 0f, 14f),
            new Vector3(16f, 0f, 12f),
            new Vector3(16f, 0f, 10f),
            new Vector3(16f, 0f, 0f),
            new Vector3(16f, 0f, -2f),
            new Vector3(16f, 0f, -4f),
            new Vector3(16f, 0f, -6f),
            new Vector3(16f, 0f, -8f),
            new Vector3(16f, 0f, -10f),
            new Vector3(16f, 0f, -18f),
            new Vector3(16f, 0f, -20f),
            new Vector3(16f, 0f, -42f),
            new Vector3(16f, 0f, -44f),
            new Vector3(16f, 0f, -46f),
            new Vector3(14f, 0f, 44f),
            new Vector3(14f, 0f, 42f),
            new Vector3(14f, 0f, 14f),
            new Vector3(14f, 0f, 12f),
            new Vector3(14f, 0f, 10f),
            new Vector3(14f, 0f, -6f),
            new Vector3(14f, 0f, -8f),
            new Vector3(14f, 0f, -10f),
            new Vector3(14f, 0f, -18f),
            new Vector3(14f, 0f, -20f),
            new Vector3(14f, 0f, -44f),
            new Vector3(14f, 0f, -46f),
            new Vector3(12f, 0f, 44f),
            new Vector3(12f, 0f, 42f),
            new Vector3(12f, 0f, 14f),
            new Vector3(12f, 0f, 12f),
            new Vector3(12f, 0f, 10f),
            new Vector3(12f, 0f, -8f),
            new Vector3(12f, 0f, -10f),
            new Vector3(12f, 0f, -18f),
            new Vector3(12f, 0f, -20f),
            new Vector3(12f, 0f, -26f),
            new Vector3(12f, 0f, -28f),
            new Vector3(12f, 0f, -44f),
            new Vector3(12f, 0f, -46f),
            new Vector3(10f, 0f, 44f),
            new Vector3(10f, 0f, 42f),
            new Vector3(10f, 0f, 14f),
            new Vector3(10f, 0f, 12f),
            new Vector3(10f, 0f, 10f),
            new Vector3(10f, 0f, -8f),
            new Vector3(10f, 0f, -10f),
            new Vector3(10f, 0f, -18f),
            new Vector3(10f, 0f, -20f),
            new Vector3(10f, 0f, -26f),
            new Vector3(10f, 0f, -28f),
            new Vector3(10f, 0f, -30f),
            new Vector3(10f, 0f, -46f),
            new Vector3(10f, 0f, -48f),
            new Vector3(8f, 0f, 46f),
            new Vector3(8f, 0f, 44f),
            new Vector3(8f, 0f, 14f),
            new Vector3(8f, 0f, 12f),
            new Vector3(8f, 0f, 10f),
            new Vector3(8f, 0f, -8f),
            new Vector3(8f, 0f, -10f),
            new Vector3(8f, 0f, -18f),
            new Vector3(8f, 0f, -20f),
            new Vector3(8f, 0f, -26f),
            new Vector3(8f, 0f, -28f),
            new Vector3(8f, 0f, -30f),
            new Vector3(8f, 0f, -46f),
            new Vector3(8f, 0f, -48f),
            new Vector3(6f, 0f, 46f),
            new Vector3(6f, 0f, 44f),
            new Vector3(6f, 0f, 14f),
            new Vector3(6f, 0f, 12f),
            new Vector3(6f, 0f, 10f),
            new Vector3(6f, 0f, -8f),
            new Vector3(6f, 0f, -10f),
            new Vector3(6f, 0f, -18f),
            new Vector3(6f, 0f, -20f),
            new Vector3(6f, 0f, -26f),
            new Vector3(6f, 0f, -28f),
            new Vector3(6f, 0f, -46f),
            new Vector3(6f, 0f, -48f),
            new Vector3(4f, 0f, 46f),
            new Vector3(4f, 0f, 44f),
            new Vector3(4f, 0f, 14f),
            new Vector3(4f, 0f, 12f),
            new Vector3(4f, 0f, 10f),
            new Vector3(4f, 0f, -8f),
            new Vector3(4f, 0f, -10f),
            new Vector3(4f, 0f, -18f),
            new Vector3(4f, 0f, -20f),
            new Vector3(4f, 0f, -46f),
            new Vector3(4f, 0f, -48f),
            new Vector3(2f, 0f, 46f),
            new Vector3(2f, 0f, 44f),
            new Vector3(2f, 0f, 14f),
            new Vector3(2f, 0f, 12f),
            new Vector3(2f, 0f, 10f),
            new Vector3(2f, 0f, -8f),
            new Vector3(2f, 0f, -10f),
            new Vector3(2f, 0f, -18f),
            new Vector3(2f, 0f, -20f),
            new Vector3(2f, 0f, -46f),
            new Vector3(2f, 0f, -48f),
            new Vector3(0f, 0f, 48f),
            new Vector3(0f, 0f, 46f),
            new Vector3(0f, 0f, 44f),
            new Vector3(0f, 0f, 14f),
            new Vector3(0f, 0f, 12f),
            new Vector3(0f, 0f, 10f),
            new Vector3(0f, 0f, -8f),
            new Vector3(0f, 0f, -10f),
            new Vector3(0f, 0f, -18f),
            new Vector3(0f, 0f, -20f),
            new Vector3(0f, 0f, -46f),
            new Vector3(0f, 0f, -48f),
            new Vector3(-2f, 0f, 48f),
            new Vector3(-2f, 0f, 46f),
            new Vector3(-2f, 0f, 44f),
            new Vector3(-2f, 0f, 14f),
            new Vector3(-2f, 0f, 12f),
            new Vector3(-2f, 0f, 10f),
            new Vector3(-2f, 0f, -8f),
            new Vector3(-2f, 0f, -10f),
            new Vector3(-2f, 0f, -18f),
            new Vector3(-2f, 0f, -20f),
            new Vector3(-2f, 0f, -46f),
            new Vector3(-2f, 0f, -48f),
            new Vector3(-4f, 0f, 48f),
            new Vector3(-4f, 0f, 46f),
            new Vector3(-4f, 0f, 44f),
            new Vector3(-4f, 0f, 14f),
            new Vector3(-4f, 0f, 12f),
            new Vector3(-4f, 0f, 10f),
            new Vector3(-4f, 0f, -8f),
            new Vector3(-4f, 0f, -10f),
            new Vector3(-4f, 0f, -18f),
            new Vector3(-4f, 0f, -20f),
            new Vector3(-4f, 0f, -46f),
            new Vector3(-4f, 0f, -48f),
            new Vector3(-6f, 0f, 46f),
            new Vector3(-6f, 0f, 44f),
            new Vector3(-6f, 0f, 14f),
            new Vector3(-6f, 0f, 12f),
            new Vector3(-6f, 0f, 10f),
            new Vector3(-6f, 0f, -8f),
            new Vector3(-6f, 0f, -10f),
            new Vector3(-6f, 0f, -18f),
            new Vector3(-6f, 0f, -20f),
            new Vector3(-6f, 0f, -46f),
            new Vector3(-6f, 0f, -48f),
            new Vector3(-8f, 0f, 46f),
            new Vector3(-8f, 0f, 44f),
            new Vector3(-8f, 0f, 14f),
            new Vector3(-8f, 0f, 12f),
            new Vector3(-8f, 0f, 10f),
            new Vector3(-8f, 0f, -8f),
            new Vector3(-8f, 0f, -10f),
            new Vector3(-8f, 0f, -18f),
            new Vector3(-8f, 0f, -20f),
            new Vector3(-8f, 0f, -46f),
            new Vector3(-8f, 0f, -48f),
            new Vector3(-10f, 0f, 46f),
            new Vector3(-10f, 0f, 44f),
            new Vector3(-10f, 0f, 14f),
            new Vector3(-10f, 0f, 12f),
            new Vector3(-10f, 0f, 10f),
            new Vector3(-10f, 0f, -8f),
            new Vector3(-10f, 0f, -10f),
            new Vector3(-10f, 0f, -18f),
            new Vector3(-10f, 0f, -20f),
            new Vector3(-10f, 0f, -26f),
            new Vector3(-10f, 0f, -28f),
            new Vector3(-10f, 0f, -46f),
            new Vector3(-10f, 0f, -48f),
            new Vector3(-12f, 0f, 46f),
            new Vector3(-12f, 0f, 44f),
            new Vector3(-12f, 0f, 14f),
            new Vector3(-12f, 0f, 12f),
            new Vector3(-12f, 0f, 10f),
            new Vector3(-12f, 0f, 0f),
            new Vector3(-12f, 0f, -2f),
            new Vector3(-12f, 0f, -4f),
            new Vector3(-12f, 0f, -6f),
            new Vector3(-12f, 0f, -8f),
            new Vector3(-12f, 0f, -10f),
            new Vector3(-12f, 0f, -18f),
            new Vector3(-12f, 0f, -20f),
            new Vector3(-12f, 0f, -26f),
            new Vector3(-12f, 0f, -28f),
            new Vector3(-12f, 0f, -30f),
            new Vector3(-12f, 0f, -46f),
            new Vector3(-12f, 0f, -48f),
            new Vector3(-14f, 0f, 46f),
            new Vector3(-14f, 0f, 44f),
            new Vector3(-14f, 0f, 14f),
            new Vector3(-14f, 0f, 12f),
            new Vector3(-14f, 0f, 10f),
            new Vector3(-14f, 0f, 8f),
            new Vector3(-14f, 0f, 6f),
            new Vector3(-14f, 0f, 4f),
            new Vector3(-14f, 0f, 2f),
            new Vector3(-14f, 0f, 0f),
            new Vector3(-14f, 0f, -2f),
            new Vector3(-14f, 0f, -4f),
            new Vector3(-14f, 0f, -6f),
            new Vector3(-14f, 0f, -8f),
            new Vector3(-14f, 0f, -10f),
            new Vector3(-14f, 0f, -18f),
            new Vector3(-14f, 0f, -20f),
            new Vector3(-14f, 0f, -26f),
            new Vector3(-14f, 0f, -28f),
            new Vector3(-14f, 0f, -30f),
            new Vector3(-14f, 0f, -46f),
            new Vector3(-14f, 0f, -48f),
            new Vector3(-16f, 0f, 44f),
            new Vector3(-16f, 0f, 42f),
            new Vector3(-16f, 0f, 18f),
            new Vector3(-16f, 0f, 16f),
            new Vector3(-16f, 0f, 14f),
            new Vector3(-16f, 0f, 12f),
            new Vector3(-16f, 0f, 10f),
            new Vector3(-16f, 0f, 8f),
            new Vector3(-16f, 0f, 6f),
            new Vector3(-16f, 0f, 4f),
            new Vector3(-16f, 0f, 2f),
            new Vector3(-16f, 0f, 0f),
            new Vector3(-16f, 0f, -2f),
            new Vector3(-16f, 0f, -4f),
            new Vector3(-16f, 0f, -8f),
            new Vector3(-16f, 0f, -10f),
            new Vector3(-16f, 0f, -12f),
            new Vector3(-16f, 0f, -14f),
            new Vector3(-16f, 0f, -16f),
            new Vector3(-16f, 0f, -18f),
            new Vector3(-16f, 0f, -20f),
            new Vector3(-16f, 0f, -44f),
            new Vector3(-16f, 0f, -46f),
            new Vector3(-18f, 0f, 44f),
            new Vector3(-18f, 0f, 42f),
            new Vector3(-18f, 0f, 24f),
            new Vector3(-18f, 0f, 22f),
            new Vector3(-18f, 0f, 20f),
            new Vector3(-18f, 0f, 18f),
            new Vector3(-18f, 0f, 16f),
            new Vector3(-18f, 0f, 14f),
            new Vector3(-18f, 0f, 12f),
            new Vector3(-18f, 0f, 10f),
            new Vector3(-18f, 0f, 8f),
            new Vector3(-18f, 0f, 6f),
            new Vector3(-18f, 0f, -10f),
            new Vector3(-18f, 0f, -12f),
            new Vector3(-18f, 0f, -14f),
            new Vector3(-18f, 0f, -16f),
            new Vector3(-18f, 0f, -18f),
            new Vector3(-18f, 0f, -44f),
            new Vector3(-18f, 0f, -46f),
            new Vector3(-20f, 0f, 44f),
            new Vector3(-20f, 0f, 42f),
            new Vector3(-20f, 0f, 24f),
            new Vector3(-20f, 0f, 22f),
            new Vector3(-20f, 0f, 20f),
            new Vector3(-20f, 0f, 18f),
            new Vector3(-20f, 0f, 16f),
            new Vector3(-20f, 0f, -12f),
            new Vector3(-20f, 0f, -14f),
            new Vector3(-20f, 0f, -16f),
            new Vector3(-20f, 0f, -44f),
            new Vector3(-20f, 0f, -46f),
            new Vector3(-22f, 0f, 42f),
            new Vector3(-22f, 0f, 40f),
            new Vector3(-22f, 0f, 24f),
            new Vector3(-22f, 0f, 22f),
            new Vector3(-22f, 0f, 20f),
            new Vector3(-22f, 0f, -42f),
            new Vector3(-22f, 0f, -44f),
            new Vector3(-24f, 0f, 42f),
            new Vector3(-24f, 0f, 40f),
            new Vector3(-24f, 0f, 24f),
            new Vector3(-24f, 0f, 22f),
            new Vector3(-24f, 0f, 20f),
            new Vector3(-24f, 0f, -42f),
            new Vector3(-24f, 0f, -44f),
            new Vector3(-26f, 0f, 40f),
            new Vector3(-26f, 0f, 38f),
            new Vector3(-26f, 0f, 24f),
            new Vector3(-26f, 0f, 22f),
            new Vector3(-26f, 0f, 20f),
            new Vector3(-26f, 0f, -40f),
            new Vector3(-26f, 0f, -42f),
            new Vector3(-28f, 0f, 40f),
            new Vector3(-28f, 0f, 38f),
            new Vector3(-28f, 0f, 36f),
            new Vector3(-28f, 0f, 24f),
            new Vector3(-28f, 0f, 22f),
            new Vector3(-28f, 0f, -40f),
            new Vector3(-28f, 0f, -42f),
            new Vector3(-30f, 0f, 38f),
            new Vector3(-30f, 0f, 36f),
            new Vector3(-30f, 0f, -38f),
            new Vector3(-30f, 0f, -40f),
            new Vector3(-32f, 0f, 36f),
            new Vector3(-32f, 0f, 34f),
            new Vector3(-32f, 0f, -36f),
            new Vector3(-32f, 0f, -38f),
            new Vector3(-34f, 0f, 36f),
            new Vector3(-34f, 0f, 34f),
            new Vector3(-34f, 0f, 32f),
            new Vector3(-34f, 0f, -34f),
            new Vector3(-34f, 0f, -36f),
            new Vector3(-34f, 0f, -38f),
            new Vector3(-36f, 0f, 34f),
            new Vector3(-36f, 0f, 32f),
            new Vector3(-36f, 0f, 30f),
            new Vector3(-36f, 0f, -32f),
            new Vector3(-36f, 0f, -34f),
            new Vector3(-36f, 0f, -36f),
            new Vector3(-38f, 0f, 32f),
            new Vector3(-38f, 0f, 30f),
            new Vector3(-38f, 0f, 28f),
            new Vector3(-38f, 0f, -30f),
            new Vector3(-38f, 0f, -32f),
            new Vector3(-38f, 0f, -34f),
            new Vector3(-40f, 0f, 30f),
            new Vector3(-40f, 0f, 28f),
            new Vector3(-40f, 0f, 26f),
            new Vector3(-40f, 0f, 24f),
            new Vector3(-40f, 0f, -28f),
            new Vector3(-40f, 0f, -30f),
            new Vector3(-40f, 0f, -32f),
            new Vector3(-42f, 0f, 26f),
            new Vector3(-42f, 0f, 24f),
            new Vector3(-42f, 0f, 22f),
            new Vector3(-42f, 0f, -24f),
            new Vector3(-42f, 0f, -26f),
            new Vector3(-42f, 0f, -28f),
            new Vector3(-44f, 0f, 24f),
            new Vector3(-44f, 0f, 22f),
            new Vector3(-44f, 0f, 20f),
            new Vector3(-44f, 0f, 18f),
            new Vector3(-44f, 0f, -20f),
            new Vector3(-44f, 0f, -22f),
            new Vector3(-44f, 0f, -24f),
            new Vector3(-44f, 0f, -26f),
            new Vector3(-46f, 0f, 20f),
            new Vector3(-46f, 0f, 18f),
            new Vector3(-46f, 0f, 16f),
            new Vector3(-46f, 0f, 14f),
            new Vector3(-46f, 0f, 12f),
            new Vector3(-46f, 0f, -14f),
            new Vector3(-46f, 0f, -16f),
            new Vector3(-46f, 0f, -18f),
            new Vector3(-46f, 0f, -20f),
            new Vector3(-46f, 0f, -22f),
            new Vector3(-48f, 0f, 16f),
            new Vector3(-48f, 0f, 14f),
            new Vector3(-48f, 0f, 12f),
            new Vector3(-48f, 0f, 10f),
            new Vector3(-48f, 0f, 8f),
            new Vector3(-48f, 0f, 6f),
            new Vector3(-48f, 0f, 4f),
            new Vector3(-48f, 0f, 2f),
            new Vector3(-48f, 0f, 0f),
            new Vector3(-48f, 0f, -2f),
            new Vector3(-48f, 0f, -4f),
            new Vector3(-48f, 0f, -6f),
            new Vector3(-48f, 0f, -8f),
            new Vector3(-48f, 0f, -10f),
            new Vector3(-48f, 0f, -12f),
            new Vector3(-48f, 0f, -14f),
            new Vector3(-48f, 0f, -16f),
            new Vector3(-48f, 0f, -18f),
            new Vector3(-50f, 0f, 10f),
            new Vector3(-50f, 0f, 8f),
            new Vector3(-50f, 0f, 6f),
            new Vector3(-50f, 0f, 4f),
            new Vector3(-50f, 0f, 2f),
            new Vector3(-50f, 0f, 0f),
            new Vector3(-50f, 0f, -2f),
            new Vector3(-50f, 0f, -4f),
            new Vector3(-50f, 0f, -6f),
            new Vector3(-50f, 0f, -8f),
            new Vector3(-50f, 0f, -10f),
            new Vector3(-50f, 0f, -12)
        };

        private ControllerSupermarketEvent Controller { get; set; } = null;
        private bool Active { get; set; } = false;

        private void StartTimer()
        {
            if (!_config.EnabledTimer) return;
            timer.In(UnityEngine.Random.Range(_config.MinStartTime, _config.MaxStartTime), () =>
            {
                if (!Active) Start(null);
                else Puts("This event is active now. To finish this event (supermarketstop), then to start the next one");
            });
        }

        private void Start(BasePlayer player)
        {
            if (!PluginExistsForStart("NpcSpawn") || !PluginExistsForStart("AnimalSpawn")) return;
            CheckVersionPlugin();
            Active = true;
            AlertToAllPlayers("PreStart", _config.Chat.Prefix, GetTimeFormat((int)_config.PreStartTime));
            timer.In(_config.PreStartTime, () =>
            {
                Puts("SupermarketEvent has begun");
                if (_config.RemoveBetterNpc && plugins.Exists("BetterNpc")) BetterNpc.Call("DestroyController", "Abandoned Supermarket");
                ToggleHooks(true);
                Controller = new GameObject().AddComponent<ControllerSupermarketEvent>();
                if (plugins.Exists("MonumentOwner")) MonumentOwner.Call("RemoveZone", Controller.Monument);
                Controller.EnablePveMode(_config.PveMode, player);
                Interface.Oxide.CallHook($"On{Name}Start", Controller.transform.position, _config.Radius);
                AlertToAllPlayers("Start", _config.Chat.Prefix, MapHelper.GridToString(MapHelper.PositionToGrid(Controller.transform.position)));
            });
        }

        private void Finish()
        {
            ToggleHooks(false);
            if (ActivePveMode) PveMode.Call("EventRemovePveMode", Name, true);
            if (Controller != null)
            {
                if (plugins.Exists("MonumentOwner")) MonumentOwner.Call("CreateZone", Controller.Monument);
                UnityEngine.Object.Destroy(Controller.gameObject);
            }
            Active = false;
            SendBalance();
            LootableCrates.Clear();
            AlertToAllPlayers("Finish", _config.Chat.Prefix);
            Interface.Oxide.CallHook($"On{Name}End");
            if (_config.RemoveBetterNpc && plugins.Exists("BetterNpc")) BetterNpc.Call("CreateController", "Abandoned Supermarket");
            Puts("SupermarketEvent has ended");
            StartTimer();
        }

        internal class ControllerSupermarketEvent : FacepunchBehaviour
        {
            private PluginConfig _config => _ins._config;

            internal MonumentInfo Monument { get; set; } = null;

            private SphereCollider SphereCollider { get; set; } = null;

            private VendingMachineMapMarker VendingMarker { get; set; } = null;
            private HashSet<MapMarkerGenericRadius> Markers { get; } = new HashSet<MapMarkerGenericRadius>();

            internal Door DefaultDoor { get; set; } = null;
            internal ElectricSwitch DefaultSwitch { get; set; } = null;

            internal HashSet<BaseEntity> Entities { get; } = new HashSet<BaseEntity>();

            private Barricade DestroyBarricade { get; set; } = null;
            private HashSet<Door> Doors { get; } = new HashSet<Door>();
            internal HashSet<CodeLock> CodeLocks { get; } = new HashSet<CodeLock>();
            internal string Code { get; set; } = $"{GetRandomNumber}{GetRandomNumber}{GetRandomNumber}{GetRandomNumber}";
            private static int GetRandomNumber => UnityEngine.Random.Range(0, 10);

            internal ArmoredHelicopter Helicopter { get; } = new ArmoredHelicopter();

            internal HashSet<ScientistNPC> Scientists { get; } = new HashSet<ScientistNPC>();
            internal HashSet<BaseAnimalNPC> Animals { get; } = new HashSet<BaseAnimalNPC>();

            internal BasePlayer Cashier { get; set; } = null;
            internal bool IsKillCashier { get; set; } = false;
            private AnimationTransformBasePlayer AnimationCashier { get; set; } = null;
            private HashSet<Vector3> LocalCashierPointsToHelicopter { get; } = new HashSet<Vector3>
            {
                new Vector3(3.2f, 0.025f,  3.3f),
                new Vector3(1.502f, 0.025f, 4.864f),
                new Vector3(1.502f, 0.025f, 5.918f),
                new Vector3(7.368f, 0.025f, 8.979f),
                new Vector3(8.417f, 0.025f, 8.979f),
                new Vector3(17.104f, 0f, 9.487f)
            };
            private HashSet<Vector3> GlobalCashierPointsToHelicopter { get; } = new HashSet<Vector3>();

            internal BasePlayer Collector { get; set; } = null;
            internal bool IsKillCollector { get; set; } = false;
            private AnimationTransformBasePlayer AnimationCollector { get; set; } = null;
            private HashSet<Vector3> LocalCollectorPointsToCashier { get; } = new HashSet<Vector3>
            {
                new Vector3(8.417f, 0.025f, 8.979f),
                new Vector3(7.368f, 0.025f, 8.979f),
                new Vector3(1.502f, 0.025f, 5.918f),
                new Vector3(1.502f, 0.025f, 4.864f),
                new Vector3(-0.565f, 0.025f, 2.469f),
                new Vector3(-0.565f, 0.025f, 0.599f),
                new Vector3(8.009f, 0.025f, 0.599f)
            };
            private HashSet<Vector3> GlobalCollectorPointsToCashier { get; } = new HashSet<Vector3>();
            private HashSet<Vector3> LocalCollectorPointsToHelicopter { get; } = new HashSet<Vector3>
            {
                new Vector3(-0.565f, 0.025f, 0.599f),
                new Vector3(-0.565f, 0.025f, 2.469f),
                new Vector3(1.502f, 0.025f, 4.864f),
                new Vector3(1.502f, 0.025f, 5.918f),
                new Vector3(7.368f, 0.025f, 8.979f),
                new Vector3(8.417f, 0.025f, 8.979f),
                new Vector3(17.104f, 0f, 9.487f)
            };
            private HashSet<Vector3> GlobalCollectorPointsToHelicopter { get; } = new HashSet<Vector3>();

            private Coroutine TransactionCoroutine { get; set; } = null;
            internal int TimeTransaction { get; set; } = _ins._config.TimeTransaction;

            internal Dictionary<LootContainer, int> Crates { get; } = new Dictionary<LootContainer, int>();

            internal int TimeToFinish { get; set; } = _ins._config.FinishTime;

            internal HashSet<BasePlayer> Players { get; } = new HashSet<BasePlayer>();
            internal BasePlayer Owner { get; set; } = null;

            private void Awake()
            {
                Monument = _ins.GetMonument();
                transform.position = Monument.transform.position;
                transform.rotation = Monument.transform.rotation;

                gameObject.layer = 3;
                SphereCollider = gameObject.AddComponent<SphereCollider>();
                SphereCollider.isTrigger = true;
                SphereCollider.radius = _config.Radius;

                foreach (Vector3 local in LocalCashierPointsToHelicopter) GlobalCashierPointsToHelicopter.Add(GetGlobalPosition(local));
                foreach (Vector3 local in LocalCollectorPointsToCashier) GlobalCollectorPointsToCashier.Add(GetGlobalPosition(local));
                foreach (Vector3 local in LocalCollectorPointsToHelicopter) GlobalCollectorPointsToHelicopter.Add(GetGlobalPosition(local));

                Door destroyDoor = GetNearEntity<Door>(GetGlobalPosition(new Vector3(7.875f, 0f, 9f)), 1f, 1 << 21);
                if (destroyDoor.IsExists()) destroyDoor.Kill();
                DefaultDoor = GetNearEntity<Door>(GetGlobalPosition(new Vector3(1.502f, 0.025f, 5.393f)), 1f, 1 << 21);
                if (DefaultDoor != null) DefaultDoor.SetOpen(false);
                DefaultSwitch = GetNearEntity<ElectricSwitch>(GetGlobalPosition(new Vector3(-4.25f, 0f, -6.4f)), 2f, 1 << 16);
                if (DefaultSwitch != null) DefaultSwitch.SetSwitch(true);

                SpawnEntities();

                SpawnCashier();
                SpawnPreset(_config.SecurityOutside, Vector3.zero);

                SpawnMapMarker(_config.Marker);

                Invoke(() => { foreach (BasePlayer player in Players) TryTeleportPlayer(player); }, 1f);
                InvokeRepeating(InvokeUpdates, 0f, 1f);
            }

            private void OnDestroy()
            {
                if (TransactionCoroutine != null) ServerMgr.Instance.StopCoroutine(TransactionCoroutine);
                CancelInvoke(InvokeUpdates);

                if (SphereCollider != null) Destroy(SphereCollider);

                if (VendingMarker.IsExists()) VendingMarker.Kill();
                foreach (MapMarkerGenericRadius marker in Markers) if (marker.IsExists()) marker.Kill();

                foreach (BasePlayer player in Players) CuiHelper.DestroyUi(player, "Tabs_KpucTaJl");

                if (Cashier.IsExists()) Cashier.Kill();
                if (Collector.IsExists()) Collector.Kill();

                foreach (BaseAnimalNPC animal in Animals) if (animal.IsExists()) animal.Kill();
                foreach (ScientistNPC npc in Scientists) if (npc.IsExists()) npc.Kill();

                foreach (KeyValuePair<LootContainer, int> dic in Crates) if (dic.Key.IsExists()) dic.Key.Kill();

                Helicopter.Destroy();

                foreach (BaseEntity entity in Entities) if (entity.IsExists()) entity.Kill();

                Door door = GameManager.server.CreateEntity("assets/bundled/prefabs/static/door.hinged.industrial_a_c.prefab", GetGlobalPosition(new Vector3(7.875f, -0.025f, 9f)), transform.rotation) as Door;
                door.enableSaving = true;
                door.Spawn();
            }

            private void OnTriggerEnter(Collider other) => EnterPlayer(other.GetComponentInParent<BasePlayer>());

            internal void EnterPlayer(BasePlayer player)
            {
                if (!player.IsPlayer()) return;
                if (Players.Contains(player)) return;
                Players.Add(player);
                Interface.Oxide.CallHook($"OnPlayerEnter{_ins.Name}", player);
                if (_config.IsCreateZonePvp) _ins.AlertToPlayer(player, _ins.GetMessage("EnterPVP", player.UserIDString, _config.Chat.Prefix));
                if (_config.Gui.IsGui) UpdateGui(player);
            }

            private void OnTriggerExit(Collider other) => ExitPlayer(other.GetComponentInParent<BasePlayer>());

            internal void ExitPlayer(BasePlayer player)
            {
                if (!player.IsPlayer()) return;
                if (!Players.Contains(player)) return;
                Players.Remove(player);
                Interface.Oxide.CallHook($"OnPlayerExit{_ins.Name}", player);
                if (_config.IsCreateZonePvp) _ins.AlertToPlayer(player, _ins.GetMessage("ExitPVP", player.UserIDString, _config.Chat.Prefix));
                if (_config.Gui.IsGui) CuiHelper.DestroyUi(player, "Tabs_KpucTaJl");
            }

            private void InvokeUpdates()
            {
                if (_config.Gui.IsGui) foreach (BasePlayer player in Players) UpdateGui(player);
                if (_config.Marker.Enabled) UpdateVendingMarker();
                UpdateMarkerForPlayers();
                UpdateTimeToFinish();
            }

            private void UpdateGui(BasePlayer player)
            {
                Dictionary<string, string> dic = new Dictionary<string, string>();
                if (TimeToFinish > 0) dic.Add("Clock_KpucTaJl", GetTimeFormat(TimeToFinish));
                if (TimeTransaction > 0 && TimeTransaction < _config.TimeTransaction && !dic.ContainsKey("Clock_KpucTaJl")) dic.Add("Clock_KpucTaJl", GetTimeFormat(TimeTransaction));
                if (!_config.CanOpenDoors && CodeLocks.Count > 0)
                {
                    if (Helicopter.CurrentStage >= 3 && Scientists.Count == 0) dic.Add("Password_KpucTaJl", Code);
                    else dic.Add("Password_KpucTaJl", "****");
                }
                if (CodeLocks.Count == 0 || TimeTransaction == 0)
                {
                    if (Collector.IsExists()) dic.Add("Collector_KpucTaJl", $"{(int)Collector._health} HP");
                    if (Cashier.IsExists()) dic.Add("Cashier_KpucTaJl", $"{(int)Cashier._health} HP");
                }
                if (Scientists.Count > 0) dic.Add("Npc_KpucTaJl", Scientists.Count.ToString());
                if (Crates.Count > 0) dic.Add("Crate_KpucTaJl", Crates.Count.ToString());
                _ins.CreateTabs(player, dic);
            }

            private void SpawnMapMarker(MarkerConfig config)
            {
                if (!config.Enabled) return;

                MapMarkerGenericRadius background = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", transform.position) as MapMarkerGenericRadius;
                background.Spawn();
                background.radius = config.Type == 0 ? config.Radius : 0.37967f;
                background.alpha = config.Alpha;
                background.color1 = new Color(config.Color.R, config.Color.G, config.Color.B);
                background.color2 = new Color(config.Color.R, config.Color.G, config.Color.B);
                Markers.Add(background);

                if (config.Type == 1)
                {
                    foreach (Vector3 pos in _ins.Marker)
                    {
                        MapMarkerGenericRadius marker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", transform.position + pos) as MapMarkerGenericRadius;
                        marker.Spawn();
                        marker.radius = 0.008f;
                        marker.alpha = 1f;
                        marker.color1 = new Color(config.Color.R, config.Color.G, config.Color.B);
                        marker.color2 = new Color(config.Color.R, config.Color.G, config.Color.B);
                        Markers.Add(marker);
                    }
                }

                VendingMarker = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", transform.position) as VendingMachineMapMarker;
                VendingMarker.Spawn();

                UpdateVendingMarker();
                UpdateMapMarkers();
            }

            private void UpdateVendingMarker()
            {
                VendingMarker.markerShopName = $"{_config.Marker.Text}";
                if (TimeToFinish > 0) VendingMarker.markerShopName += $"\n{GetTimeFormat(TimeToFinish)}";
                else if (TimeTransaction > 0 && TimeTransaction < _config.TimeTransaction) VendingMarker.markerShopName += $"\n{GetTimeFormat(TimeTransaction)}";
                if (_ins.ActivePveMode) VendingMarker.markerShopName += Owner == null ? "\nNo Owner" : $"\n{Owner.displayName}";
                VendingMarker.SendNetworkUpdate();
            }

            internal void UpdateMapMarkers() { foreach (MapMarkerGenericRadius marker in Markers) marker.SendUpdate(); }

            private void UpdateMarkerForPlayers()
            {
                if (Players.Count == 0) return;

                if (_config.MainPoint.Enabled)
                {
                    HashSet<Vector3> points = new HashSet<Vector3>();
                    if (CodeLocks.Count == 0 || TimeTransaction == 0)
                    {
                        if (Cashier.IsExists()) points.Add(Cashier.transform.position);
                        if (Collector.IsExists()) points.Add(Collector.transform.position);
                    }
                    if (points.Count == 0 && CodeLocks.Count > 0) foreach (CodeLock codelock in CodeLocks) if (codelock.IsExists()) points.Add(codelock.transform.position);
                    if (points.Count > 0) foreach (BasePlayer player in Players) foreach (Vector3 point in points) UpdateMarkerForPlayer(player, point, _config.MainPoint);
                    points = null;
                }

                if (_config.AdditionalPoint.Enabled && Crates.Count > 0)
                {
                    HashSet<Vector3> points = new HashSet<Vector3>();
                    foreach (KeyValuePair<LootContainer, int> dic in Crates) if (dic.Key.IsExists()) points.Add(dic.Key.transform.position);
                    if (points.Count > 0) foreach (BasePlayer player in Players) foreach (Vector3 point in points) UpdateMarkerForPlayer(player, point, _config.AdditionalPoint);
                    points = null;
                }
            }

            private void UpdateTimeToFinish()
            {
                if (Helicopter.CurrentStage == 5)
                {
                    if (TimeToFinish == 0) TimeToFinish = _config.FinishTime;
                    if (Crates.Count == 0 && Scientists.Count == 0 && TimeToFinish > _config.PreFinishTime) TimeToFinish = _config.PreFinishTime;
                }
                if (TimeToFinish > 0)
                {
                    if (TimeToFinish == _config.PreFinishTime) _ins.AlertToAllPlayers("PreFinish", _config.Chat.Prefix, GetTimeFormat(TimeToFinish));
                    TimeToFinish--;
                    if (TimeToFinish == 0)
                    {
                        CancelInvoke(InvokeUpdates);
                        _ins.Finish();
                    }
                }
            }

            private Vector3 GetGlobalPosition(Vector3 localPosition) => transform.TransformPoint(localPosition);

            private Quaternion GetGlobalRotation(Vector3 localRotation) => transform.rotation * Quaternion.Euler(localRotation);

            private static T GetNearEntity<T>(Vector3 position, float radius, int layerMask) where T : BaseEntity
            {
                List<T> list = Pool.Get<List<T>>();
                Vis.Entities<T>(position, radius, list, layerMask);
                T result = list.Count == 0 ? null : list.Min(s => Vector3.Distance(position, s.transform.position));
                Pool.FreeUnmanaged(ref list);
                return result;
            }

            private void SpawnEntities()
            {
                foreach (Prefab prefab in _ins.Prefabs)
                {
                    BaseEntity entity = SpawnEntity(prefab.Path, transform.TransformPoint(prefab.Pos), transform.rotation * Quaternion.Euler(prefab.Rot));

                    if (entity is BuildingBlock)
                    {
                        BuildingBlock buildingBlock = entity as BuildingBlock;
                        buildingBlock.ChangeGradeAndSkin(BuildingGrade.Enum.Stone, prefab.Skin);
                    }
                    else
                    {
                        entity.skinID = prefab.Skin;
                        entity.SendNetworkUpdate();
                    }

                    if (entity is Door)
                    {
                        Door door = entity as Door;
                        CodeLocks.Add(SetCodeLock(door, Code));
                        door.canTakeLock = false;
                        door.canTakeCloser = false;
                        door.canTakeKnocker = false;
                        Doors.Add(door);
                    }

                    if (entity is Barricade && DestroyBarricade == null) DestroyBarricade = entity as Barricade;

                    Entities.Add(entity);
                }
            }

            private void SpawnCrates()
            {
                foreach (CrateConfig crateConfig in _config.Crates)
                {
                    LootContainer crate = GameManager.server.CreateEntity(crateConfig.Prefab, GetGlobalPosition(crateConfig.Position.ToVector3()), GetGlobalRotation(crateConfig.Rotation.ToVector3())) as LootContainer;
                    crate.enableSaving = false;
                    crate.Spawn();

                    Crates.Add(crate, crateConfig.TypeLootTable);

                    if (crateConfig.TypeLootTable == 1 || crateConfig.TypeLootTable == 4 || crateConfig.TypeLootTable == 5)
                    {
                        _ins.NextTick(() =>
                        {
                            crate.inventory.ClearItemsContainer();
                            if (crateConfig.TypeLootTable == 4 || crateConfig.TypeLootTable == 5) _ins.AddToContainerPrefab(crate.inventory, crateConfig.PrefabLootTable);
                            if (crateConfig.TypeLootTable == 1 || crateConfig.TypeLootTable == 5) _ins.AddToContainerItem(crate.inventory, crateConfig.OwnLootTable);
                        });
                    }

                    if (_ins.ActivePveMode) _ins.PveMode.Call("EventAddCrates", _ins.Name, Crates.Select(x => x.Key.net.ID.Value));
                }
            }

            private static CodeLock SetCodeLock(Door door, string code)
            {
                CodeLock codelock = GameManager.server.CreateEntity("assets/prefabs/locks/keypad/lock.code.prefab") as CodeLock;
                codelock.SetParent(door, door.GetSlotAnchorName(BaseEntity.Slot.Lock));
                codelock.Spawn();
                door.SetSlot(BaseEntity.Slot.Lock, codelock);
                codelock.code = code;
                codelock.hasCode = true;
                codelock.SetFlag(BaseEntity.Flags.Locked, true);
                return codelock;
            }

            internal void TryToOpenDoor(BasePlayer target, CodeLock codeLock, string code)
            {
                if (code != Code || !CodeLocks.Contains(codeLock)) return;
                if (_ins.ActivePveMode && _ins.PveMode.Call("CanActionEvent", _ins.Name, target) != null) return;
                _ins.ActionEconomy(target.userID, "OpenDoors");
                OpenAllDoors();
            }

            internal void OpenAllDoors()
            {
                if (CodeLocks.Count == 0) return;

                SpawnPreset(_config.SecurityInside, Vector3.zero);

                foreach (Door door in Doors) door.SetOpen(true);
                _ins.NextTick(() =>
                {
                    foreach (CodeLock codelock in CodeLocks) if (codelock.IsExists()) codelock.Kill();
                    CodeLocks.Clear();
                });

                if (DestroyBarricade.IsExists()) DestroyBarricade.Kill();

                SpawnCrates();

                if (DefaultSwitch != null) DefaultSwitch.SetSwitch(true);

                _ins.AlertToAllPlayers("OpenDoors", _config.Chat.Prefix);
            }

            internal void SpawnArmoredHelicopter()
            {
                TimeToFinish = 0;
                Helicopter.Spawn(GetGlobalPosition(new Vector3(45.039f, 11.524f, -49.583f)), transform.rotation * Quaternion.Euler(new Vector3(15f, 315f, 0f)));
                Helicopter.CalculateAnimation(transform);
                Helicopter.StartEngine();
                Helicopter.StartArrivalAnimation();
            }

            internal void ArrivalHelicopter()
            {
                Helicopter.FinishEngine();
                Helicopter.CurrentStage = 3;
                SpawnCollector();
                SpawnPreset(_config.SecurityCollector, GetGlobalPosition(new Vector3(17.104f, 0f, 9.487f)));
                _ins.AlertToAllPlayers("HelicopterArrived", _config.Chat.Prefix);
            }

            internal void DepartHelicopter()
            {
                Helicopter.CurrentStage = 5;
                Helicopter.Destroy();
            }

            internal void TryDepartHelicopter()
            {
                _ins.NextTick(() =>
                {
                    if (Cashier.IsExists() || Collector.IsExists()) return;
                    Helicopter.StartDepartAnimation();
                    OpenAllDoors();
                    if (IsKillCashier && IsKillCollector) _ins.AlertToAllPlayers("HelicopterDepartAllKilled", _config.Chat.Prefix);
                    if (IsKillCashier && !IsKillCollector) _ins.AlertToAllPlayers("HelicopterDepartCashierKilled", _config.Chat.Prefix);
                    if (!IsKillCashier && IsKillCollector) _ins.AlertToAllPlayers("HelicopterDepartCollectorKilled", _config.Chat.Prefix);
                    if (!IsKillCashier && !IsKillCollector) _ins.AlertToAllPlayers("HelicopterDepartAllAlive", _config.Chat.Prefix);
                });
            }

            private void SpawnCashier()
            {
                Cashier = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", GetGlobalPosition(new Vector3(8f, 0.025f, 3.3f))) as BasePlayer;
                Cashier.enableSaving = false;
                Cashier.Spawn();

                Cashier.viewAngles = GetGlobalRotation(new Vector3(0f, 180f, 0f)).eulerAngles;

                Cashier.displayName = _config.Cashier.Name;

                Cashier.startHealth = _config.Cashier.Health;
                Cashier._health = _config.Cashier.Health;
                Cashier._maxHealth = _config.Cashier.Health;

                foreach (Item item in _config.Cashier.WearItems.Select(x => ItemManager.CreateByName(x.ShortName, 1, x.SkinId)))
                {
                    if (item == null) continue;
                    if (!Cashier.inventory.containerWear.Insert(item)) item.Remove();
                }

                AnimationCashier = Cashier.gameObject.AddComponent<AnimationTransformBasePlayer>();
            }

            private void CashierGoToHelicopter() => AnimationCashier.AddPath(GlobalCashierPointsToHelicopter, _config.Cashier.Speed);

            private bool CashierAtHelicopter => Vector3.Distance(GetGlobalPosition(new Vector3(17.104f, 0f, 9.487f)), Cashier.transform.position) < 1f;

            private void SpawnCollector()
            {
                Collector = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", GetGlobalPosition(new Vector3(17.104f, 0f, 9.487f))) as BasePlayer;
                Collector.enableSaving = false;
                Collector.Spawn();

                Collector.displayName = _config.Collector.Name;

                Collector.startHealth = _config.Collector.Health;
                Collector._health = _config.Collector.Health;
                Collector._maxHealth = _config.Collector.Health;

                foreach (Item item in _config.Collector.WearItems.Select(x => ItemManager.CreateByName(x.ShortName, 1, x.SkinId)))
                {
                    if (item == null) continue;
                    if (!Collector.inventory.containerWear.Insert(item)) item.Remove();
                }

                AnimationCollector = Collector.gameObject.AddComponent<AnimationTransformBasePlayer>();
                CollectorGoToCashier();
            }

            private void CollectorGoToCashier() => AnimationCollector.AddPath(GlobalCollectorPointsToCashier, _config.Collector.Speed);
            private void CollectorGoToHelicopter() => AnimationCollector.AddPath(GlobalCollectorPointsToHelicopter, _config.Collector.Speed);

            private bool CollectorAtCashier => Vector3.Distance(GetGlobalPosition(new Vector3(8.009f, 0.025f, 0.599f)), Collector.transform.position) < 1f;
            private bool CollectorAtHelicopter => Vector3.Distance(GetGlobalPosition(new Vector3(17.104f, 0f, 9.487f)), Collector.transform.position) < 1f;

            internal void CheckOpenDoor(BasePlayer basePlayer)
            {
                if (DefaultDoor != null && !DefaultDoor.HasFlag(BaseEntity.Flags.Open) && Vector3.Distance(basePlayer.transform.position, DefaultDoor.transform.position) < 1f)
                {
                    DefaultDoor.SetOpen(true);
                    Invoke(() => DefaultDoor.SetOpen(false), 1.4f);
                    return;
                }
                Door door = Doors.FirstOrDefault(x => Vector3.Distance(basePlayer.transform.position, x.transform.position) < 1f);
                if (door != null && !door.HasFlag(BaseEntity.Flags.Open))
                {
                    Barricade barricade = SpawnEntity("assets/prefabs/deployable/barricades/barricade.metal.prefab", GetGlobalPosition(new Vector3(7.875f, -1.8f, 9f)), Quaternion.Euler(door.transform.rotation.eulerAngles + new Vector3(0f, 90f, 0f))) as Barricade;
                    door.SetOpen(true);
                    Invoke(() =>
                    {
                        door.SetOpen(false);
                        if (barricade.IsExists()) barricade.Kill();
                    }, 1.4f);
                    return;
                }
            }

            internal void FinishPathBasePlayer(BasePlayer basePlayer)
            {
                if (basePlayer == null) return;
                if (basePlayer == Cashier)
                {
                    if (CashierAtHelicopter)
                    {
                        if (Cashier.IsExists()) Cashier.Kill();
                        TryDepartHelicopter();
                    }
                }
                else if (basePlayer == Collector)
                {
                    if (CollectorAtCashier)
                    {
                        foreach (BasePlayer player in Players) TryTeleportPlayer(player);

                        Collector.viewAngles = Quaternion.LookRotation(Cashier.transform.position - Collector.transform.position).eulerAngles;
                        Collector.TransformChanged();
                        Collector.SendNetworkUpdate();

                        Cashier.viewAngles = Quaternion.LookRotation(Collector.transform.position - Cashier.transform.position).eulerAngles;
                        Cashier.TransformChanged();
                        Cashier.SendNetworkUpdate();

                        TransactionCoroutine = ServerMgr.Instance.StartCoroutine(Transaction());
                    }
                    else if (CollectorAtHelicopter)
                    {
                        if (Collector.IsExists()) Collector.Kill();
                        TryDepartHelicopter();
                    }
                }
            }

            private IEnumerator Transaction()
            {
                int timeCashier = 1, timeCollector = 1;

                while (TimeTransaction > 0)
                {
                    yield return CoroutineEx.waitForSeconds(1f);

                    TimeTransaction--;

                    if (!Cashier.IsExists() && !Collector.IsExists())
                    {
                        TimeTransaction = _config.TimeTransaction;
                        yield break;
                    }

                    if (Cashier.IsExists())
                    {
                        timeCashier--;
                        if (timeCashier == 0)
                        {
                            StopCinematic(Cashier.userID);
                            timeCashier = UnityEngine.Random.Range(8, 12);
                            StartCinematic(Cashier.userID);
                        }
                    }

                    if (Collector.IsExists())
                    {
                        timeCollector--;
                        if (timeCollector == 0)
                        {
                            StopCinematic(Collector.userID);
                            timeCollector = UnityEngine.Random.Range(8, 12);
                            StartCinematic(Collector.userID);
                        }
                    }
                }

                if (Cashier.IsExists())
                {
                    StopCinematic(Cashier.userID);
                    CashierGoToHelicopter();
                }

                if (Collector.IsExists())
                {
                    StopCinematic(Collector.userID);
                    CollectorGoToHelicopter();
                }
            }

            private static void StartCinematic(ulong userUd) => ConsoleNetwork.BroadcastToAllClients($"cinematic_play talk_0{UnityEngine.Random.Range(1, 6)} {userUd} 1", Array.Empty<object>());

            private static void StopCinematic(ulong userUd) => ConsoleNetwork.BroadcastToAllClients($"cinematic_stop {userUd}", Array.Empty<object>());

            private void SpawnPreset(PresetConfig preset, Vector3 spawnPos)
            {
                int count = UnityEngine.Random.Range(preset.Min, preset.Max + 1);

                List<Vector3> positions = Pool.Get<List<Vector3>>();
                foreach (string pos in preset.Positions) positions.Add(GetGlobalPosition(pos.ToVector3()));

                for (int i = 0; i < count; i++)
                {
                    Vector3 pos = positions.GetRandom();
                    positions.Remove(pos);

                    JObject config = GetObjectConfig(preset.Config, spawnPos == Vector3.zero ? string.Empty : pos.ToString());

                    ScientistNPC npc = (ScientistNPC)_ins.NpcSpawn.Call("SpawnNpc", spawnPos == Vector3.zero ? pos : spawnPos, config);
                    Scientists.Add(npc);

                    if (preset == _config.SecurityOutside && _config.SecurityAnimal.Enabled)
                    {
                        BaseAnimalNPC animal = (BaseAnimalNPC)_ins.AnimalSpawn.Call("SpawnAnimal", npc.transform.position, GetObjectConfig(_config.SecurityAnimal));
                        _ins.AnimalSpawn.Call("SetParentEntity", animal, npc, new Vector3(1f, 0f, 0f));
                        Animals.Add(animal);
                    }
                }

                Pool.FreeUnmanaged(ref positions);

                if (_ins.ActivePveMode)
                {
                    _ins.PveMode.Call("EventAddScientists", _ins.Name, Scientists.Select(x => x.net.ID.Value));
                    if (preset == _config.SecurityOutside) _ins.PveMode.Call("EventAddScientists", _ins.Name, Animals.Select(x => x.net.ID.Value));
                }
            }

            private static JObject GetObjectConfig(NpcConfig config, string home)
            {
                HashSet<string> states = config.Stationary ? new HashSet<string> { "IdleState", "CombatStationaryState" } : new HashSet<string> { "RoamState", "ChaseState", "CombatState" };
                if (config.BeltItems.Any(x => x.ShortName == "rocket.launcher" || x.ShortName == "explosive.timed")) states.Add("RaidState");
                return new JObject
                {
                    ["Name"] = config.Name,
                    ["WearItems"] = new JArray { config.WearItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["SkinID"] = x.SkinId }) },
                    ["BeltItems"] = new JArray { config.BeltItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["Amount"] = x.Amount, ["SkinID"] = x.SkinId, ["Mods"] = new JArray { x.Mods }, ["Ammo"] = x.Ammo }) },
                    ["Kit"] = config.Kit,
                    ["Health"] = config.Health,
                    ["RoamRange"] = config.RoamRange,
                    ["ChaseRange"] = config.ChaseRange,
                    ["SenseRange"] = config.SenseRange,
                    ["ListenRange"] = config.SenseRange / 2f,
                    ["AttackRangeMultiplier"] = config.AttackRangeMultiplier,
                    ["CheckVisionCone"] = config.CheckVisionCone,
                    ["HostileTargetsOnly"] = false,
                    ["VisionCone"] = config.VisionCone,
                    ["DamageScale"] = config.DamageScale,
                    ["TurretDamageScale"] = 0f,
                    ["AimConeScale"] = config.AimConeScale,
                    ["DisableRadio"] = config.DisableRadio,
                    ["CanRunAwayWater"] = true,
                    ["CanSleep"] = false,
                    ["SleepDistance"] = 100f,
                    ["Speed"] = config.Speed,
                    ["AreaMask"] = 1,
                    ["AgentTypeID"] = -1372625422,
                    ["HomePosition"] = home,
                    ["MemoryDuration"] = config.MemoryDuration,
                    ["States"] = new JArray { states }
                };
            }

            private JObject GetObjectConfig(AnimalConfig config)
            {
                return new JObject
                {
                    ["Prefab"] = GetAnimalPrefab(config.Type),
                    ["Health"] = config.Health,
                    ["RoamRange"] = 0f,
                    ["ChaseRange"] = config.ChaseRange,
                    ["SenseRange"] = config.SenseRange,
                    ["ListenRange"] = config.SenseRange / 2f,
                    ["AttackRange"] = config.AttackRange,
                    ["CheckVisionCone"] = config.CheckVisionCone,
                    ["VisionCone"] = config.VisionCone,
                    ["HostileTargetsOnly"] = false,
                    ["AttackDamage"] = config.AttackDamage,
                    ["AttackRate"] = config.AttackRate,
                    ["TurretDamageScale"] = 0f,
                    ["CanRunAwayWater"] = false,
                    ["CanSleep"] = false,
                    ["SleepDistance"] = 100f,
                    ["Speed"] = config.Speed,
                    ["AreaMask"] = 1,
                    ["AgentTypeID"] = -1372625422,
                    ["HomePosition"] = transform.position.ToString(),
                    ["MemoryDuration"] = config.MemoryDuration,
                    ["States"] = new JArray { "RoamState", "ChaseState", "CombatState" }
                };
            }

            internal static string GetAnimalPrefab(int type)
            {
                if (type == 1) return "assets/rust.ai/agents/bear/polarbear.prefab";
                if (type == 2) return "assets/rust.ai/agents/bear/bear.prefab";
                if (type == 3) return "assets/rust.ai/agents/wolf/wolf.prefab";
                if (type == 4) return "assets/rust.ai/agents/boar/boar.prefab";
                if (type == 5) return "assets/rust.ai/agents/stag/stag.prefab";
                if (type == 6) return "assets/rust.ai/agents/chicken/chicken.prefab";
                return string.Empty;
            }

            private void TryTeleportPlayer(BasePlayer player)
            {
                if (player._limitedNetworking) return;
                if (IsInsideRoom1(player) || IsInsideRoom2(player))
                {
                    Vector3 pos = GetGlobalPosition(new Vector3(-11.214f, 0.1f, -19.1f));
                    player.Teleport(pos);
                }
            }

            private bool IsInsideRoom1(BasePlayer player)
            {
                Vector3 localPos = transform.InverseTransformPoint(player.transform.position);
                if (localPos.x < -7.433f || localPos.x > 12.367f) return false;
                if (localPos.y < 0f || localPos.y > 5.514f) return false;
                if (localPos.z < -6.316f || localPos.z > 5.484f) return false;
                return true;
            }

            private bool IsInsideRoom2(BasePlayer player)
            {
                Vector3 localPos = transform.InverseTransformPoint(player.transform.position);
                if (localPos.x < -5.24f || localPos.x > 7.96f) return false;
                if (localPos.y < 0f || localPos.y > 4.268f) return false;
                if (localPos.z < 5.353f || localPos.z > 10.453f) return false;
                return true;
            }

            internal void EnablePveMode(PveModeConfig config, BasePlayer player)
            {
                if (!_ins.ActivePveMode) return;

                Dictionary<string, object> dic = new Dictionary<string, object>
                {
                    ["Damage"] = config.Damage,
                    ["ScaleDamage"] = config.ScaleDamage,
                    ["LootCrate"] = config.LootCrate,
                    ["HackCrate"] = false,
                    ["LootNpc"] = config.LootNpc,
                    ["DamageNpc"] = config.DamageNpc,
                    ["DamageTank"] = false,
                    ["DamageHelicopter"] = false,
                    ["DamageTurret"] = false,
                    ["TargetNpc"] = config.TargetNpc,
                    ["TargetTank"] = false,
                    ["TargetHelicopter"] = false,
                    ["TargetTurret"] = false,
                    ["CanEnter"] = config.CanEnter,
                    ["CanEnterCooldownPlayer"] = config.CanEnterCooldownPlayer,
                    ["TimeExitOwner"] = config.TimeExitOwner,
                    ["AlertTime"] = config.AlertTime,
                    ["RestoreUponDeath"] = config.RestoreUponDeath,
                    ["CooldownOwner"] = config.CooldownOwner,
                    ["Darkening"] = config.Darkening
                };

                _ins.PveMode.Call("EventAddPveMode", _ins.Name, dic, transform.position, _config.Radius, new HashSet<ulong>(), new HashSet<ulong>(), new HashSet<ulong>(), new HashSet<ulong>(), new HashSet<ulong>(), new HashSet<ulong>(), player);
            }
        }
        #endregion Controller

        #region ArmoredHelicopter
        internal class ArmoredHelicopter
        {
            internal ModularCar Car { get; set; } = null;
            internal HashSet<VehicleModuleSeating> Modules { get; } = new HashSet<VehicleModuleSeating>();
            internal ElectricBattery Battery { get; set; } = null;
            internal AttackHelicopter Helicopter { get; set; } = null;
            internal BasePlayer Driver { get; set; } = null;

            private AttackHeliDriverSeat DriverSeat { get; set; } = null;
            private StorageContainer FuelContainer { get; set; } = null;

            private AnimationTransformVehicle Animation { get; set; } = null;
            private HashSet<PointAnimationTransform> LocalPointsArrival { get; } = new HashSet<PointAnimationTransform>
            {
                new PointAnimationTransform { Pos = new Vector3(42.31f, 11.524f, -46.878f), Rot = new Vector3(15f, 315f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(39.605f, 11.524f, -44.174f), Rot = new Vector3(15f, 315f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(36.863f, 11.524f, -41.535f), Rot = new Vector3(15f, 315f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(34.024f, 11.524f, -38.739f), Rot = new Vector3(15f, 315f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(31.276f, 11.524f, -36.04f), Rot = new Vector3(15f, 315f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(28.436f, 11.524f, -33.256f), Rot = new Vector3(15f, 315f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(25.966f, 11.524f, -30.474f), Rot = new Vector3(15f, 320f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(23.827f, 11.524f, -27.637f), Rot = new Vector3(15f, 325f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(21.896f, 11.524f, -24.638f), Rot = new Vector3(15f, 330f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(20.319f, 11.524f, -21.425f), Rot = new Vector3(15f, 335f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(19.113f, 11.524f, -18.404f), Rot = new Vector3(15f, 340f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(18.027f, 11.524f, -15.014f), Rot = new Vector3(15f, 345f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(17.491f, 11.524f, -12.821f), Rot = new Vector3(15f, 350f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(17.135f, 11.524f, -10.516f), Rot = new Vector3(15f, 355f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(17.104f, 11.524f, -8.088f), Rot = new Vector3(15f, 0f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(17.104f, 11.524f, -5.238f), Rot = new Vector3(15f, 0f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(17.104f, 11.524f, -2.347f), Rot = new Vector3(15f, 0f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(17.104f, 11.524f, 0.522f), Rot = new Vector3(15f, 0f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(17.104f, 10.069f, 2.063f), Rot = new Vector3(12f, 0f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(17.104f, 8.854f, 3.95f), Rot = new Vector3(9f, 0f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(17.104f, 7.873f, 5.436f), Rot = new Vector3(6f, 0f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(17.104f, 6.963f, 6.919f), Rot = new Vector3(3f, 0f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(17.104f, 6.003f, 8.02f), Rot = new Vector3(0f, 0f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(17.104f, 5.078f, 8.427f), Rot = new Vector3(357f, 0f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(17.104f, 4.026f, 8.959f), Rot = new Vector3(354f, 0f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(17.104f, 2.714f, 9.428f), Rot = new Vector3(351f, 0f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(17.104f, 1.605f, 9.505f), Rot = new Vector3(354f, 0f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(17.104f, 0.635f, 9.521f), Rot = new Vector3(357f, 0f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(17.104f, -0.085f, 9.487f), Rot = new Vector3(0f, 0f, 0f) }
            };
            private HashSet<PointAnimationTransform> GlobalPointsArrival { get; } = new HashSet<PointAnimationTransform>();
            private HashSet<PointAnimationTransform> LocalPointsDepart { get; } = new HashSet<PointAnimationTransform>
            {
                new PointAnimationTransform { Pos = new Vector3(17.104f, 1.359f, 9.487f), Rot = new Vector3(0f, 0f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(17.104f, 3.359f, 9.487f), Rot = new Vector3(0f, 0f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(17.104f, 5.359f, 9.487f), Rot = new Vector3(0f, 0f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(17.172f, 7.359f, 9.499f), Rot = new Vector3(0f, 340f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(17.232f, 9.359f, 9.534f), Rot = new Vector3(0f, 320f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(17.276f, 11.359f, 9.586f), Rot = new Vector3(0f, 300f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(17.3f, 13.359f, 9.651f), Rot = new Vector3(0f, 280f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(17.303f, 15.359f, 9.686f), Rot = new Vector3(0f, 270f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(16.334f, 15.403f, 9.686f), Rot = new Vector3(3f, 270f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(14.722f, 15.432f, 9.686f), Rot = new Vector3(6f, 270f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(12.029f, 15.432f, 9.686f), Rot = new Vector3(9f, 270f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(9.22f, 15.432f, 9.686f), Rot = new Vector3(12f, 270f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(6.072f, 15.432f, 9.686f), Rot = new Vector3(15f, 270f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(2.905f, 15.432f, 9.686f), Rot = new Vector3(15f, 270f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(-0.564f, 15.432f, 9.686f), Rot = new Vector3(15f, 270f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(-4.121f, 15.432f, 9.686f), Rot = new Vector3(15f, 270f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(-7.448f, 15.432f, 9.686f), Rot = new Vector3(15f, 270f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(-11.131f, 15.432f, 9.686f), Rot = new Vector3(15f, 270f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(-14.482f, 15.432f, 9.686f), Rot = new Vector3(15f, 270f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(-17.894f, 15.432f, 9.686f), Rot = new Vector3(15f, 270f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(-21.506f, 15.432f, 9.686f), Rot = new Vector3(15f, 270f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(-25.02f, 15.432f, 9.686f), Rot = new Vector3(15f, 270f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(-29.076f, 15.432f, 9.686f), Rot = new Vector3(15f, 270f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(-32.869f, 15.432f, 9.686f), Rot = new Vector3(15f, 270f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(-36.419f, 15.432f, 9.686f), Rot = new Vector3(15f, 270f, 0f) },
                new PointAnimationTransform { Pos = new Vector3(-39.972f, 15.432f, 9.686f), Rot = new Vector3(15f, 270f, 0f) }
            };
            private HashSet<PointAnimationTransform> GlobalPointsDepart { get; } = new HashSet<PointAnimationTransform>();

            internal int CurrentStage { get; set; } = 0;

            internal void CalculateAnimation(Transform transform)
            {
                foreach (PointAnimationTransform point in LocalPointsArrival)
                {
                    GlobalPointsArrival.Add(new PointAnimationTransform
                    {
                        Pos = transform.TransformPoint(point.Pos),
                        Rot = (transform.rotation * Quaternion.Euler(point.Rot)).eulerAngles
                    });
                }
                foreach (PointAnimationTransform point in LocalPointsDepart)
                {
                    GlobalPointsDepart.Add(new PointAnimationTransform
                    {
                        Pos = transform.TransformPoint(point.Pos),
                        Rot = (transform.rotation * Quaternion.Euler(point.Rot)).eulerAngles
                    });
                }
                Animation = Car.gameObject.AddComponent<AnimationTransformVehicle>();
            }

            internal void Spawn(Vector3 pos, Quaternion rot)
            {
                Car = GameManager.server.CreateEntity("assets/content/vehicles/modularcar/car_chassis_4module.entity.prefab", pos, rot) as ModularCar;
                Car.enableSaving = false;
                Car.spawnSettings.useSpawnSettings = false;
                UnityEngine.Object.DestroyImmediate(Car.GetComponent<MagnetLiftable>());
                Car.Spawn();
                Item moduleItem1 = ItemManager.CreateByName("vehicle.1mod.cockpit.armored");
                if (!Car.TryAddModule(moduleItem1)) moduleItem1.Remove();
                Item moduleItem2 = ItemManager.CreateByName("vehicle.1mod.passengers.armored");
                if (!Car.TryAddModule(moduleItem2)) moduleItem2.Remove();
                Item moduleItem3 = ItemManager.CreateByName("vehicle.1mod.passengers.armored");
                if (!Car.TryAddModule(moduleItem3)) moduleItem3.Remove();
                Item moduleItem4 = ItemManager.CreateByName("vehicle.1mod.passengers.armored");
                if (!Car.TryAddModule(moduleItem4)) moduleItem4.Remove();
                foreach (BaseEntity entity in Car.children)
                {
                    switch (entity.ShortPrefabName)
                    {
                        case "modular_car_fuel_storage":
                            entity.SetFlag(BaseEntity.Flags.Locked, true);
                            break;
                        case "1module_cockpit_armored":
                        case "1module_passengers_armored":
                            Modules.Add(entity as VehicleModuleSeating);
                            entity.SetFlag(BaseEntity.Flags.Busy, true);
                            break;
                    }
                }
                Car.rigidBody.detectCollisions = false;
                Car.SetToKinematic();

                Battery = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/batteries/large/large.rechargable.battery.deployed.prefab") as ElectricBattery;
                Battery.enableSaving = false;
                Battery.transform.localPosition = new Vector3(0f, 0.85f, 0.866f);
                Battery.SetParent(Car);
                UnityEngine.Object.DestroyImmediate(Battery.GetComponent<GroundWatch>());
                UnityEngine.Object.DestroyImmediate(Battery.GetComponent<DestroyOnGroundMissing>());
                UnityEngine.Object.DestroyImmediate(Battery.GetComponent<BoxCollider>());
                Battery.Spawn();
                Battery.pickup.enabled = false;

                Helicopter = GameManager.server.CreateEntity("assets/content/vehicles/attackhelicopter/attackhelicopter.entity.prefab") as AttackHelicopter;
                Helicopter.enableSaving = false;
                Helicopter.transform.localPosition = new Vector3(0f, 0.44f, 0.8f);
                Helicopter.SetParent(Car);
                Helicopter.Spawn();
                foreach (BaseEntity entity in Helicopter.children)
                {
                    switch (entity.ShortPrefabName)
                    {
                        case "turret_attackheli":
                        case "rockets_attackheli":
                            entity.SetFlag(BaseEntity.Flags.Locked, true);
                            break;
                        case "fuel_storage_attackheli":
                            FuelContainer = entity as StorageContainer;
                            Item fuel = ItemManager.CreateByName("lowgradefuel", 1000000);
                            if (!fuel.MoveToContainer(FuelContainer.inventory)) fuel.Remove();
                            FuelContainer.SetFlag(BaseEntity.Flags.Locked, true);
                            break;
                        case "attackhelidriver":
                            DriverSeat = entity as AttackHeliDriverSeat;
                            Driver = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", DriverSeat.transform.position) as BasePlayer;
                            Driver.Spawn();
                            Driver.DisablePlayerCollider();
                            Driver.playerRigidbody.isKinematic = true;
                            Driver.limitNetworking = true;
                            DriverSeat.AttemptMount(Driver, false);
                            break;
                        case "attackheligunner":
                            entity.SetFlag(BaseEntity.Flags.Busy, true);
                            break;
                    }
                }
                Helicopter.rigidBody.detectCollisions = false;
                Helicopter.SetToKinematic();

                CurrentStage = 1;
            }

            internal void Destroy()
            {
                if (Battery.IsExists()) Battery.Kill();
                if (FuelContainer != null) FuelContainer.inventory.ClearItemsContainer();
                if (Helicopter.IsExists()) Helicopter.Kill();
                if (Driver.IsExists()) Driver.Kill();
                if (Animation != null) UnityEngine.Object.Destroy(Animation);
                if (Car.IsExists()) Car.Kill();
                Modules.Clear();
            }

            internal void StartEngine()
            {
                if (Helicopter == null || Driver == null) return;
                Helicopter.engineController.TryStartEngine(Driver);
            }

            internal void FinishEngine()
            {
                if (Helicopter == null) return;
                Helicopter.engineController.FinishStartingEngine();
            }

            internal void StartArrivalAnimation()
            {
                if (Animation == null || GlobalPointsArrival.Count == 0) return;
                Animation.AddPath(GlobalPointsArrival, -1.3f, 15f);
                CurrentStage = 2;
            }

            internal void StartDepartAnimation()
            {
                if (Animation == null || GlobalPointsDepart.Count == 0) return;
                Animation.AddPath(GlobalPointsDepart, 1.3f, 15f);
                CurrentStage = 4;
            }

            internal void CheckVehicleKinematic()
            {
                Car?.SetToKinematic();
                Helicopter?.SetToKinematic();
            }
        }
        #endregion ArmoredHelicopter

        #region Animation
        internal class AnimationTransformBasePlayer : FacepunchBehaviour
        {
            private BasePlayer Main { get; set; } = null;

            private List<Vector3> Path { get; } = new List<Vector3>();

            private float SecondsTaken { get; set; } = 0f;
            private float SecondsToTake { get; set; } = 0f;
            private float WaypointDone { get; set; } = 0f;

            private Vector3 StartPos { get; set; } = Vector3.zero;
            private Vector3 EndPos { get; set; } = Vector3.zero;

            private float Speed { get; set; } = 0f;

            private void Awake()
            {
                Main = GetComponent<BasePlayer>();
                enabled = false;
            }

            internal void AddPath(HashSet<Vector3> path, float speed)
            {
                foreach (Vector3 point in path) Path.Add(point);
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
                        SecondsToTake = 0f;
                        SecondsTaken = 0f;
                        WaypointDone = 0f;
                        enabled = false;
                        _ins.Controller.FinishPathBasePlayer(Main);
                        return;
                    }
                    StartPos = transform.position;
                    if (Path[0] != StartPos)
                    {
                        EndPos = Path[0];
                        SecondsToTake = Vector3.Distance(EndPos, StartPos) / Speed;
                        Main.viewAngles = Quaternion.LookRotation(EndPos - StartPos).eulerAngles;
                        SecondsTaken = 0f;
                        WaypointDone = 0f;
                    }
                    Path.RemoveAt(0);
                }
                if (StartPos != EndPos)
                {
                    _ins.Controller.CheckOpenDoor(Main);
                    SecondsTaken += Time.deltaTime;
                    WaypointDone = Mathf.InverseLerp(0f, SecondsToTake, SecondsTaken);
                    transform.position = Vector3.Lerp(StartPos, EndPos, WaypointDone);
                    Main.viewAngles = Quaternion.LookRotation(EndPos - StartPos).eulerAngles;
                    Main.TransformChanged();
                    Main.SendNetworkUpdate();
                    if (WaypointDone >= 1f) SecondsTaken = 0f;
                }
            }
        }

        internal class PointAnimationTransform { public Vector3 Pos; public Vector3 Rot; }

        internal class AnimationTransformVehicle : FacepunchBehaviour
        {
            private BaseEntity Main { get; set; } = null;

            private List<PointAnimationTransform> Path { get; } = new List<PointAnimationTransform>();

            private float SecondsTaken { get; set; } = 0f;
            private float SecondsToTake { get; set; } = 0f;
            private float WaypointDone { get; set; } = 0f;

            private Vector3 StartPos { get; set; } = Vector3.zero;
            private Vector3 EndPos { get; set; } = Vector3.zero;

            private Vector3 StartRot { get; set; } = Vector3.zero;
            private Vector3 EndRot { get; set; } = Vector3.zero;

            private float Acceleration { get; set; } = 0f;
            private float SpeedMax { get; set; } = 0f;
            private float Speed { get; set; } = 0f;

            private void Awake()
            {
                Main = GetComponent<BaseEntity>();
                enabled = false;
            }

            internal void AddPath(HashSet<PointAnimationTransform> path, float acceleration, float speedMax)
            {
                foreach (PointAnimationTransform point in path) Path.Add(point);
                Acceleration = acceleration;
                SpeedMax = speedMax;
                if (Acceleration < 0f) Speed = SpeedMax;
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
                        switch (_ins.Controller.Helicopter.CurrentStage)
                        {
                            case 2:
                                _ins.Controller.ArrivalHelicopter();
                                break;
                            case 4:
                                _ins.Controller.DepartHelicopter();
                                break;
                        }
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
                    if (Acceleration > 0f)
                    {
                        if (Speed < SpeedMax)
                        {
                            Speed += Acceleration * Time.deltaTime;
                            if (Speed > SpeedMax) Speed = SpeedMax;
                            SecondsToTake = Vector3.Distance(EndPos, StartPos) / Speed;
                        }
                    }
                    else if (Acceleration < 0f)
                    {
                        if (Speed > 1f)
                        {
                            Speed += Acceleration * Time.deltaTime;
                            if (Speed < 1f) Speed = 1f;
                            SecondsToTake = Vector3.Distance(EndPos, StartPos) / Speed;
                        }
                    }
                    SecondsTaken += Time.deltaTime;
                    WaypointDone = Mathf.InverseLerp(0f, SecondsToTake, SecondsTaken);
                    if (StartPos != EndPos) transform.position = Vector3.Lerp(StartPos, EndPos, WaypointDone);
                    if (StartRot != EndRot) transform.rotation = Quaternion.Lerp(Quaternion.Euler(StartRot), Quaternion.Euler(EndRot), WaypointDone);
                    Main.TransformChanged();
                    Main.SendNetworkUpdate();
                    _ins.Controller.Helicopter.CheckVehicleKinematic();
                    if (WaypointDone >= 1f) SecondsTaken = 0f;
                }
            }
        }
        #endregion Animation

        #region Find Position
        internal MonumentInfo GetMonument()
        {
            List<MonumentInfo> list = Pool.Get<List<MonumentInfo>>();
            foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
            {
                if (monument.displayPhrase.english != "Abandoned Supermarket") continue;
                list.Add(monument);
            }
            MonumentInfo result = list.Count > 0 ? list.GetRandom() : null;
            Pool.FreeUnmanaged(ref list);
            return result;
        }
        #endregion Find Position

        #region Spawn Loot
        #region NPC
        private void OnPlayerCorpseSpawned(BasePlayer basePlayer, PlayerCorpse corpse)
        {
            if (basePlayer == null) return;
            if (basePlayer == Controller.Cashier)
            {
                NextTick(() =>
                {
                    if (corpse == null) return;
                    foreach (ItemContainer container in corpse.containers) container.ClearItemsContainer();
                    ItemContainer main = corpse.containers[0];
                    if (_config.Cashier.TypeLootTable == 1 || _config.Cashier.TypeLootTable == 2) AddToContainerPrefab(main, _config.Cashier.PrefabLootTable);
                    if (_config.Cashier.TypeLootTable == 0 || _config.Cashier.TypeLootTable == 2) AddToContainerItem(main, _config.Cashier.OwnLootTable);
                    corpse.Kill();
                });
            }
            else if (basePlayer == Controller.Collector)
            {
                NextTick(() =>
                {
                    if (corpse == null) return;
                    foreach (ItemContainer container in corpse.containers) container.ClearItemsContainer();
                    ItemContainer main = corpse.containers[0];
                    if (_config.Collector.TypeLootTable == 1 || _config.Collector.TypeLootTable == 2) AddToContainerPrefab(main, _config.Collector.PrefabLootTable);
                    if (_config.Collector.TypeLootTable == 0 || _config.Collector.TypeLootTable == 2) AddToContainerItem(main, _config.Collector.OwnLootTable);
                    corpse.Kill();
                });
            }
        }

        private void OnCorpsePopulate(ScientistNPC entity, NPCPlayerCorpse corpse)
        {
            if (entity == null) return;
            if (!Controller.Scientists.Contains(entity)) return;
            Controller.Scientists.Remove(entity);
            if (_config.CanOpenDoors && Controller.CodeLocks.Count > 0 && Controller.Helicopter.CurrentStage >= 3 && Controller.Scientists.Count == 0) Controller.OpenAllDoors();
            PresetConfig preset = GetPresetConfig(entity.displayName);
            if (preset == null) return;
            NextTick(() =>
            {
                if (corpse == null) return;
                ItemContainer container = corpse.containers[0];
                if (preset.TypeLootTable == 1 || preset.TypeLootTable == 4 || preset.TypeLootTable == 5)
                {
                    container.ClearItemsContainer();
                    if (preset.TypeLootTable == 4 || preset.TypeLootTable == 5) AddToContainerPrefab(container, preset.PrefabLootTable);
                    if (preset.TypeLootTable == 1 || preset.TypeLootTable == 5) AddToContainerItem(container, preset.OwnLootTable);
                }
                if (!_config.CanOpenDoors && Controller.CodeLocks.Count > 0 && Controller.Helicopter.CurrentStage >= 3 && Controller.Scientists.Count == 0)
                {
                    Item note = ItemManager.CreateByName("note");
                    note.text = $"Door = {Controller.Code}";
                    if (container.capacity < container.itemList.Count + 1) container.capacity++;
                    if (!note.MoveToContainer(container)) note.Remove();
                }
                if (preset.Config.IsRemoveCorpse && corpse.IsExists()) corpse.Kill();
            });
        }

        private object CanPopulateLoot(ScientistNPC entity, NPCPlayerCorpse corpse)
        {
            if (entity == null || Controller == null) return null;
            if (!Controller.Scientists.Contains(entity)) return null;
            PresetConfig preset = GetPresetConfig(entity.displayName);
            if (preset == null) return null;
            if (preset.TypeLootTable == 2) return null;
            else return true;
        }

        private object OnCustomLootNPC(NetworkableId netId)
        {
            if (Controller == null) return null;
            ScientistNPC entity = Controller.Scientists.FirstOrDefault(x => x.IsExists() && x.net.ID.Value == netId.Value);
            if (entity == null) return null;
            PresetConfig preset = GetPresetConfig(entity.displayName);
            if (preset == null) return null;
            if (preset.TypeLootTable == 3) return null;
            else return true;
        }

        private PresetConfig GetPresetConfig(string name)
        {
            if (name == _config.SecurityOutside.Config.Name) return _config.SecurityOutside;
            else if (name == _config.SecurityInside.Config.Name) return _config.SecurityInside;
            else if (name == _config.SecurityCollector.Config.Name) return _config.SecurityCollector;
            else return null;
        }
        #endregion NPC

        #region Crates
        private object CanPopulateLoot(LootContainer container)
        {
            if (container == null || Controller == null) return null;
            int typeLootTable;
            if (Controller.Crates.TryGetValue(container, out typeLootTable))
            {
                if (typeLootTable == 2) return null;
                else return true;
            }
            else return null;
        }

        private object OnCustomLootContainer(NetworkableId netId)
        {
            if (Controller == null) return null;
            if (Controller.Crates.Any(x => x.Key.IsExists() && x.Key.net.ID.Value == netId.Value))
            {
                if (Controller.Crates.FirstOrDefault(x => x.Key.IsExists() && x.Key.net.ID.Value == netId.Value).Value == 3) return null;
                else return true;
            }
            return null;
        }

        private object OnContainerPopulate(LootContainer container)
        {
            if (container == null || Controller == null) return null;
            int typeLootTable;
            if (Controller.Crates.TryGetValue(container, out typeLootTable))
            {
                if (typeLootTable == 6) return null;
                else return true;
            }
            else return null;
        }
        #endregion Crates

        private void AddToContainerPrefab(ItemContainer container, PrefabLootTableConfig lootTable)
        {
            if (lootTable.UseCount)
            {
                int count = 0, max = UnityEngine.Random.Range(lootTable.Min, lootTable.Max + 1);
                while (count < max)
                {
                    foreach (PrefabConfig prefab in lootTable.Prefabs)
                    {
                        if (UnityEngine.Random.Range(0f, 100f) > prefab.Chance) continue;
                        SpawnIntoContainer(container, prefab.PrefabDefinition);
                        count++;
                        if (count == max) break;
                    }
                }
            }
            else foreach (PrefabConfig prefab in lootTable.Prefabs) if (UnityEngine.Random.Range(0f, 100f) <= prefab.Chance) SpawnIntoContainer(container, prefab.PrefabDefinition);
        }

        private void SpawnIntoContainer(ItemContainer container, string prefab)
        {
            if (AllLootSpawnSlots.ContainsKey(prefab))
            {
                foreach (LootContainer.LootSpawnSlot lootSpawnSlot in AllLootSpawnSlots[prefab])
                    for (int j = 0; j < lootSpawnSlot.numberToSpawn; j++)
                        if (UnityEngine.Random.Range(0f, 1f) <= lootSpawnSlot.probability)
                            lootSpawnSlot.definition.SpawnIntoContainer(container);
            }
            else AllLootSpawn[prefab].SpawnIntoContainer(container);
        }

        private void AddToContainerItem(ItemContainer container, LootTableConfig lootTable)
        {
            if (lootTable.UseCount)
            {
                HashSet<int> indexMove = new HashSet<int>();
                int count = UnityEngine.Random.Range(lootTable.Min, lootTable.Max + 1);
                while (indexMove.Count < count)
                {
                    for (int i = 0; i < lootTable.Items.Count; i++)
                    {
                        if (indexMove.Contains(i)) continue;
                        if (SpawnIntoContainer(container, lootTable.Items[i]))
                        {
                            indexMove.Add(i);
                            if (indexMove.Count == count) break;
                        }
                    }
                }
                indexMove = null;
            }
            else foreach (ItemConfig item in lootTable.Items) SpawnIntoContainer(container, item);
        }

        private bool SpawnIntoContainer(ItemContainer container, ItemConfig config)
        {
            if (UnityEngine.Random.Range(0f, 100f) > config.Chance) return false;
            Item item = config.IsBluePrint ? ItemManager.CreateByName("blueprintbase") : ItemManager.CreateByName(config.ShortName, UnityEngine.Random.Range(config.MinAmount, config.MaxAmount + 1), config.SkinId);
            if (item == null)
            {
                PrintWarning($"Failed to create item! ({config.ShortName})");
                return false;
            }
            if (config.IsBluePrint) item.blueprintTarget = ItemManager.FindItemDefinition(config.ShortName).itemid;
            if (!string.IsNullOrEmpty(config.Name)) item.name = config.Name;
            if (container.capacity < container.itemList.Count + 1) container.capacity++;
            if (!item.MoveToContainer(container))
            {
                item.Remove();
                return false;
            }
            return true;
        }

        private void CheckAllLootTables()
        {
            CheckPrefabLootTable(_config.Cashier.PrefabLootTable);
            CheckLootTable(_config.Cashier.OwnLootTable);

            CheckPrefabLootTable(_config.Collector.PrefabLootTable);
            CheckLootTable(_config.Collector.OwnLootTable);

            CheckPrefabLootTable(_config.SecurityOutside.PrefabLootTable);
            CheckLootTable(_config.SecurityOutside.OwnLootTable);

            CheckPrefabLootTable(_config.SecurityInside.PrefabLootTable);
            CheckLootTable(_config.SecurityInside.OwnLootTable);

            CheckPrefabLootTable(_config.SecurityCollector.PrefabLootTable);
            CheckLootTable(_config.SecurityCollector.OwnLootTable);

            foreach (CrateConfig config in _config.Crates)
            {
                CheckPrefabLootTable(config.PrefabLootTable);
                CheckLootTable(config.OwnLootTable);
            }
        }

        private void CheckLootTable(LootTableConfig lootTable)
        {
            for (int i = lootTable.Items.Count - 1; i >= 0; i--)
            {
                ItemConfig item = lootTable.Items[i];

                if (!ItemManager.itemList.Any(x => x.shortname == item.ShortName))
                {
                    PrintWarning($"Unknown item removed! ({item.ShortName})");
                    lootTable.Items.Remove(item);
                    continue;
                }
                if (item.Chance <= 0f)
                {
                    PrintWarning($"An item with an incorrect probability has been removed from the loot table ({item.ShortName})");
                    lootTable.Items.Remove(item);
                    continue;
                }

                if (item.MinAmount <= 0) item.MinAmount = 1;
                if (item.MaxAmount < item.MinAmount) item.MaxAmount = item.MinAmount;
            }

            lootTable.Items = lootTable.Items.OrderByQuickSort(x => x.Chance);
            if (lootTable.Items.Any(x => x.Chance >= 100f))
            {
                HashSet<ItemConfig> newItems = new HashSet<ItemConfig>();

                for (int i = lootTable.Items.Count - 1; i >= 0; i--)
                {
                    ItemConfig itemConfig = lootTable.Items[i];
                    if (itemConfig.Chance < 100f) break;
                    newItems.Add(itemConfig);
                    lootTable.Items.Remove(itemConfig);
                }

                int count = newItems.Count;

                if (count > 0)
                {
                    foreach (ItemConfig itemConfig in lootTable.Items) newItems.Add(itemConfig);
                    lootTable.Items.Clear();
                    foreach (ItemConfig itemConfig in newItems) lootTable.Items.Add(itemConfig);
                }

                newItems = null;

                if (lootTable.Min < count) lootTable.Min = count;
                if (lootTable.Max < count) lootTable.Max = count;
            }

            if (lootTable.Max > lootTable.Items.Count) lootTable.Max = lootTable.Items.Count;
            if (lootTable.Min > lootTable.Max) lootTable.Min = lootTable.Max;
            if (lootTable.Items.Count == 0) lootTable.UseCount = false;
        }

        private void CheckPrefabLootTable(PrefabLootTableConfig lootTable)
        {
            HashSet<string> prefabs = new HashSet<string>();

            for (int i = lootTable.Prefabs.Count - 1; i >= 0; i--)
            {
                PrefabConfig prefab = lootTable.Prefabs[i];
                if (prefabs.Any(x => x == prefab.PrefabDefinition))
                {
                    lootTable.Prefabs.Remove(prefab);
                    PrintWarning($"Duplicate prefab removed from loot table! ({prefab.PrefabDefinition})");
                }
                else
                {
                    GameObject gameObject = GameManager.server.FindPrefab(prefab.PrefabDefinition);
                    global::HumanNPC humanNpc = gameObject.GetComponent<global::HumanNPC>();
                    ScarecrowNPC scarecrowNpc = gameObject.GetComponent<ScarecrowNPC>();
                    LootContainer lootContainer = gameObject.GetComponent<LootContainer>();
                    if (humanNpc != null && humanNpc.LootSpawnSlots.Length != 0)
                    {
                        if (!AllLootSpawnSlots.ContainsKey(prefab.PrefabDefinition)) AllLootSpawnSlots.Add(prefab.PrefabDefinition, humanNpc.LootSpawnSlots);
                        prefabs.Add(prefab.PrefabDefinition);
                    }
                    else if (scarecrowNpc != null && scarecrowNpc.LootSpawnSlots.Length != 0)
                    {
                        if (!AllLootSpawnSlots.ContainsKey(prefab.PrefabDefinition)) AllLootSpawnSlots.Add(prefab.PrefabDefinition, scarecrowNpc.LootSpawnSlots);
                        prefabs.Add(prefab.PrefabDefinition);
                    }
                    else if (lootContainer != null && lootContainer.LootSpawnSlots.Length != 0)
                    {
                        if (!AllLootSpawnSlots.ContainsKey(prefab.PrefabDefinition)) AllLootSpawnSlots.Add(prefab.PrefabDefinition, lootContainer.LootSpawnSlots);
                        prefabs.Add(prefab.PrefabDefinition);
                    }
                    else if (lootContainer != null && lootContainer.lootDefinition != null)
                    {
                        if (!AllLootSpawn.ContainsKey(prefab.PrefabDefinition)) AllLootSpawn.Add(prefab.PrefabDefinition, lootContainer.lootDefinition);
                        prefabs.Add(prefab.PrefabDefinition);
                    }
                    else
                    {
                        lootTable.Prefabs.Remove(prefab);
                        PrintWarning($"Unknown prefab removed! ({prefab.PrefabDefinition})");
                    }
                }
            }

            prefabs = null;

            lootTable.Prefabs = lootTable.Prefabs.OrderByQuickSort(x => x.Chance);
            if (lootTable.Prefabs.Any(x => x.Chance >= 100f))
            {
                HashSet<PrefabConfig> newPrefabs = new HashSet<PrefabConfig>();

                for (int i = lootTable.Prefabs.Count - 1; i >= 0; i--)
                {
                    PrefabConfig prefabConfig = lootTable.Prefabs[i];
                    if (prefabConfig.Chance < 100f) break;
                    newPrefabs.Add(prefabConfig);
                    lootTable.Prefabs.Remove(prefabConfig);
                }

                int count = newPrefabs.Count;

                if (count > 0)
                {
                    foreach (PrefabConfig prefabConfig in lootTable.Prefabs) newPrefabs.Add(prefabConfig);
                    lootTable.Prefabs.Clear();
                    foreach (PrefabConfig prefabConfig in newPrefabs) lootTable.Prefabs.Add(prefabConfig);
                }

                newPrefabs = null;

                if (lootTable.Min < count) lootTable.Min = count;
                if (lootTable.Max < count) lootTable.Max = count;
            }

            if (lootTable.Min > lootTable.Max) lootTable.Min = lootTable.Max;
            if (lootTable.Prefabs.Count == 0) lootTable.UseCount = false;
        }

        private Dictionary<string, LootSpawn> AllLootSpawn { get; } = new Dictionary<string, LootSpawn>();
        private Dictionary<string, LootContainer.LootSpawnSlot[]> AllLootSpawnSlots { get; } = new Dictionary<string, LootContainer.LootSpawnSlot[]>();
        #endregion Spawn Loot

        #region PveMode
        [PluginReference] private readonly Plugin PveMode;

        internal bool ActivePveMode => _config.PveMode.Pve && plugins.Exists("PveMode");

        private void SetOwnerPveMode(string shortname, BasePlayer player)
        {
            if (string.IsNullOrEmpty(shortname) || shortname != Name || !player.IsPlayer()) return;
            Controller.Owner = player;
            AlertToAllPlayers("SetOwner", _config.Chat.Prefix, player.displayName);
        }

        private void ClearOwnerPveMode(string shortname)
        {
            if (string.IsNullOrEmpty(shortname) || shortname != Name) return;
            Controller.Owner = null;
        }
        #endregion PveMode

        #region TruePVE
        private object CanEntityTakeDamage(BasePlayer victim, HitInfo hitinfo)
        {
            if (!_config.IsCreateZonePvp || victim == null || hitinfo == null || Controller == null) return null;
            BasePlayer attacker = hitinfo.InitiatorPlayer;
            if (Controller.Players.Contains(victim) && (attacker == null || Controller.Players.Contains(attacker))) return true;
            else return null;
        }
        #endregion TruePVE

        #region Buoyant Helicopters
        private object OnVehicleBuoyancyAdd(AttackHelicopter heli)
        {
            if (Controller == null || Controller.Helicopter == null || Controller.Helicopter.Helicopter == null || heli == null) return null;
            if (Controller.Helicopter.Helicopter == heli) return false;
            else return null;
        }
        #endregion Buoyant Helicopters

        #region NTeleportation
        private object CanTeleport(BasePlayer player, Vector3 to)
        {
            if (_config.NTeleportationInterrupt && Controller != null && (Controller.Players.Contains(player) || Vector3.Distance(Controller.transform.position, to) < _config.Radius)) return GetMessage("NTeleportation", player.UserIDString, _config.Chat.Prefix);
            else return null;
        }

        private void OnPlayerTeleported(BasePlayer player, Vector3 oldPos, Vector3 newPos)
        {
            if (Controller == null || !player.IsPlayer()) return;
            if (!Controller.Players.Contains(player) && Vector3.Distance(Controller.transform.position, newPos) < _config.Radius) Controller.EnterPlayer(player);
            if (Controller.Players.Contains(player) && Vector3.Distance(Controller.transform.position, newPos) > _config.Radius) Controller.ExitPlayer(player);
        }
        #endregion NTeleportation

        #region Economy
        [PluginReference] private readonly Plugin Economics, ServerRewards, IQEconomic, XPerience;

        private Dictionary<ulong, double> PlayersBalance { get; } = new Dictionary<ulong, double>();

        private void ActionEconomy(ulong playerId, string type, string arg = "")
        {
            switch (type)
            {
                case "Crates":
                    if (_config.Economy.Crates.ContainsKey(arg)) AddBalance(playerId, _config.Economy.Crates[arg]);
                    break;
                case "Npc":
                    AddBalance(playerId, _config.Economy.Npc);
                    break;
                case "Cashier":
                    AddBalance(playerId, _config.Economy.Cashier);
                    break;
                case "Collector":
                    AddBalance(playerId, _config.Economy.Collector);
                    break;
                case "OpenDoors":
                    AddBalance(playerId, _config.Economy.OpenDoors);
                    break;
            }
        }

        private void AddBalance(ulong playerId, double balance)
        {
            if (balance == 0) return;
            if (PlayersBalance.ContainsKey(playerId)) PlayersBalance[playerId] += balance;
            else PlayersBalance.Add(playerId, balance);
        }

        private void SendBalance()
        {
            if (PlayersBalance.Count == 0) return;
            if (_config.Economy.Plugins.Count > 0)
            {
                foreach (KeyValuePair<ulong, double> dic in PlayersBalance)
                {
                    if (dic.Value < _config.Economy.Min) continue;
                    int intCount = Convert.ToInt32(dic.Value);
                    if (_config.Economy.Plugins.Contains("Economics") && plugins.Exists("Economics") && dic.Value > 0) Economics?.Call("Deposit", dic.Key.ToString(), dic.Value);
                    if (_config.Economy.Plugins.Contains("Server Rewards") && plugins.Exists("ServerRewards") && intCount > 0) ServerRewards?.Call("AddPoints", dic.Key, intCount);
                    if (_config.Economy.Plugins.Contains("IQEconomic") && plugins.Exists("IQEconomic") && intCount > 0) IQEconomic?.Call("API_SET_BALANCE", dic.Key, intCount);
                    BasePlayer player = BasePlayer.FindByID(dic.Key);
                    if (player != null)
                    {
                        if (_config.Economy.Plugins.Contains("XPerience") && plugins.Exists("XPerience") && dic.Value > 0) XPerience?.Call("GiveXP", player, dic.Value);
                        AlertToPlayer(player, GetMessage("SendEconomy", player.UserIDString, _config.Chat.Prefix, dic.Value));
                    }
                }
            }
            ulong winnerId = PlayersBalance.Max(x => x.Value).Key;
            Interface.Oxide.CallHook($"On{Name}Winner", winnerId);
            foreach (string command in _config.Economy.Commands) Server.Command(command.Replace("{steamid}", $"{winnerId}"));
            PlayersBalance.Clear();
        }
        #endregion Economy

        #region Alerts
        [PluginReference] private readonly Plugin GUIAnnouncements, DiscordMessages, Notify;

        private string ClearColorAndSize(string message)
        {
            message = message.Replace("</color>", string.Empty);
            message = message.Replace("</size>", string.Empty);
            while (message.Contains("<color="))
            {
                int index = message.IndexOf("<color=", StringComparison.Ordinal);
                message = message.Remove(index, message.IndexOf(">", index, StringComparison.Ordinal) - index + 1);
            }
            while (message.Contains("<size="))
            {
                int index = message.IndexOf("<size=", StringComparison.Ordinal);
                message = message.Remove(index, message.IndexOf(">", index, StringComparison.Ordinal) - index + 1);
            }
            if (!string.IsNullOrEmpty(_config.Chat.Prefix)) message = message.Replace(_config.Chat.Prefix + " ", string.Empty);
            return message;
        }

        private bool CanSendDiscordMessage => _config.Discord.IsDiscord && !string.IsNullOrEmpty(_config.Discord.WebhookUrl) && _config.Discord.WebhookUrl != "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";

        private void AlertToAllPlayers(string langKey, params object[] args)
        {
            if (CanSendDiscordMessage && _config.Discord.Keys.Contains(langKey))
            {
                object fields = new[] { new { name = Title, value = ClearColorAndSize(GetMessage(langKey, null, args)), inline = false } };
                DiscordMessages?.Call("API_SendFancyMessage", _config.Discord.WebhookUrl, "", _config.Discord.EmbedColor, JsonConvert.SerializeObject(fields), null, this);
            }
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                if (_config.DistanceAlerts == 0f || Vector3.Distance(player.transform.position, Controller.transform.position) <= _config.DistanceAlerts)
                    AlertToPlayer(player, GetMessage(langKey, player.UserIDString, args));
        }

        private void AlertToPlayer(BasePlayer player, string message)
        {
            if (_config.Chat.IsChat) PrintToChat(player, message);
            if (_config.GameTip.IsGameTip) player.SendConsoleCommand("gametip.showtoast", _config.GameTip.Style, ClearColorAndSize(message), string.Empty);
            if (_config.GuiAnnouncements.IsGuiAnnouncements && plugins.Exists("GUIAnnouncements")) GUIAnnouncements?.Call("CreateAnnouncement", ClearColorAndSize(message), _config.GuiAnnouncements.BannerColor, _config.GuiAnnouncements.TextColor, player, _config.GuiAnnouncements.ApiAdjustVPosition);
            if (_config.Notify.IsNotify && plugins.Exists("Notify")) Notify?.Call("SendNotify", player, _config.Notify.Type, ClearColorAndSize(message));
        }
        #endregion Alerts

        #region GUI
        private HashSet<string> Names { get; } = new HashSet<string>
        {
            "Tab_KpucTaJl",
            "Clock_KpucTaJl",
            "Crate_KpucTaJl",
            "Cashier_KpucTaJl",
            "Collector_KpucTaJl",
            "Password_KpucTaJl",
            "Npc_KpucTaJl"
        };
        private Dictionary<string, string> Images { get; } = new Dictionary<string, string>();

        private IEnumerator DownloadImages()
        {
            foreach (string name in Names)
            {
                string url = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "Images" + Path.DirectorySeparatorChar + name + ".png";
                using (UnityWebRequest unityWebRequest = UnityWebRequestTexture.GetTexture(url))
                {
                    yield return unityWebRequest.SendWebRequest();
                    if (unityWebRequest.result != UnityWebRequest.Result.Success)
                    {
                        PrintError($"Image {name} was not found. Maybe you didn't upload it to the .../oxide/data/Images/ folder");
                        break;
                    }
                    else
                    {
                        Texture2D tex = DownloadHandlerTexture.GetContent(unityWebRequest);
                        Images.Add(name, FileStorage.server.Store(tex.EncodeToPNG(), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString());
                        Puts($"Image {name} download is complete");
                        UnityEngine.Object.DestroyImmediate(tex);
                    }
                }
            }
            if (Images.Count < Names.Count) Interface.Oxide.UnloadPlugin(Name);
        }

        private void CreateTabs(BasePlayer player, Dictionary<string, string> tabs)
        {
            CuiHelper.DestroyUi(player, "Tabs_KpucTaJl");

            CuiElementContainer container = new CuiElementContainer();

            float border = 52.5f + 54.5f * (tabs.Count - 1);
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = $"{-border} {_config.Gui.OffsetMinY}", OffsetMax = $"{border} {_config.Gui.OffsetMinY + 20}" },
                CursorEnabled = false,
            }, "Under", "Tabs_KpucTaJl");

            int i = 0;

            foreach (KeyValuePair<string, string> dic in tabs)
            {
                i++;
                float xmin = 109f * (i - 1);
                container.Add(new CuiElement
                {
                    Name = $"Tab_{i}_KpucTaJl",
                    Parent = "Tabs_KpucTaJl",
                    Components =
                    {
                        new CuiRawImageComponent { Png = Images["Tab_KpucTaJl"] },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = $"{xmin} 0", OffsetMax = $"{xmin + 105f} 20" }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = $"Tab_{i}_KpucTaJl",
                    Components =
                    {
                        new CuiRawImageComponent { Png = Images[dic.Key] },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "9 3", OffsetMax = "23 17" }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = $"Tab_{i}_KpucTaJl",
                    Components =
                    {
                        new CuiTextComponent() { Color = "1 1 1 1", Text = dic.Value, Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "robotocondensed-bold.ttf" },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "28 0", OffsetMax = "100 20" }
                    }
                });
            }

            CuiHelper.AddUi(player, container);
        }
        #endregion GUI

        #region Helpers
        [PluginReference] private readonly Plugin NpcSpawn, AnimalSpawn, BetterNpc, MonumentOwner;

        private HashSet<string> HooksInsidePlugin { get; } = new HashSet<string>
        {
            "OnEntityTakeDamage",
            "CanBuild",
            "CanChangeGrade",
            "OnStructureRotate",
            "OnCodeEntered",
            "OnNpcTarget",
            "OnEntityDeath",
            "OnPlayerConnected",
            "OnPlayerDeath",
            "OnEntityKill",
            "OnLootEntity",
            "OnPlayerCommand",
            "OnServerCommand",
            "OnPlayerCorpseSpawned",
            "OnCorpsePopulate",
            "CanPopulateLoot",
            "OnCustomLootNPC",
            "OnCustomLootContainer",
            "OnContainerPopulate",
            "SetOwnerPveMode",
            "ClearOwnerPveMode",
            "CanEntityTakeDamage",
            "OnVehicleBuoyancyAdd",
            "CanTeleport",
            "OnPlayerTeleported"
        };

        private void ToggleHooks(bool subscribe)
        {
            foreach (string hook in HooksInsidePlugin)
            {
                if (subscribe) Subscribe(hook);
                else Unsubscribe(hook);
            }
        }

        private const string StrSec = En ? "sec." : "сек.";
        private const string StrMin = En ? "min." : "мин.";
        private const string StrH = En ? "h." : "ч.";

        private static string GetTimeFormat(int time)
        {
            if (time <= 60) return $"{time} {StrSec}";
            else if (time <= 3600)
            {
                int sec = time % 60;
                int min = (time - sec) / 60;
                return sec == 0 ? $"{min} {StrMin}" : $"{min} {StrMin} {sec} {StrSec}";
            }
            else
            {
                int minSec = time % 3600;
                int hour = (time - minSec) / 3600;
                int sec = minSec % 60;
                int min = (minSec - sec) / 60;
                if (min == 0 && sec == 0) return $"{hour} {StrH}";
                else if (sec == 0) return $"{hour} {StrH} {min} {StrMin}";
                else return $"{hour} {StrH} {min} {StrMin} {sec} {StrSec}";
            }
        }

        private static BaseEntity SpawnEntity(string prefab, Vector3 pos, Quaternion rot)
        {
            BaseEntity entity = GameManager.server.CreateEntity(prefab, pos, rot);
            entity.enableSaving = false;

            GroundWatch groundWatch = entity.GetComponent<GroundWatch>();
            if (groundWatch != null) UnityEngine.Object.DestroyImmediate(groundWatch);

            DestroyOnGroundMissing destroyOnGroundMissing = entity.GetComponent<DestroyOnGroundMissing>();
            if (destroyOnGroundMissing != null) UnityEngine.Object.DestroyImmediate(destroyOnGroundMissing);

            entity.Spawn();

            if (entity is StabilityEntity) (entity as StabilityEntity).grounded = true;
            if (entity is BaseCombatEntity) (entity as BaseCombatEntity).pickup.enabled = false;

            return entity;
        }

        private static void UpdateMarkerForPlayer(BasePlayer player, Vector3 pos, PointConfig config)
        {
            if (player == null || player.IsSleeping()) return;
            bool isAdmin = player.IsAdmin;
            if (!isAdmin)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                player.SendNetworkUpdateImmediate();
            }
            try
            {
                player.SendConsoleCommand("ddraw.text", 1f, Color.white, pos, $"<size={config.Size}><color={config.Color}>{config.Text}</color></size>");
            }
            finally
            {
                if (!isAdmin)
                {
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                    player.SendNetworkUpdateImmediate();
                }
            }
        }

        private void CheckVersionPlugin()
        {
            webrequest.Enqueue("http://37.153.157.216:5000/Api/GetPluginVersions?pluginName=SupermarketEvent", null, (code, response) =>
            {
                if (code != 200 || string.IsNullOrEmpty(response)) return;
                string[] array = response.Replace("\"", string.Empty).Split('.');
                VersionNumber latestVersion = new VersionNumber(Convert.ToInt32(array[0]), Convert.ToInt32(array[1]), Convert.ToInt32(array[2]));
                if (Version < latestVersion) PrintWarning($"A new version ({latestVersion}) of the plugin is available! You need to update the plugin:\n- https://lone.design/product/supermarket-event\n- https://codefling.com/plugins/supermarket-event");
            }, this);
        }

        private bool PluginExistsForStart(string pluginName)
        {
            if (plugins.Exists(pluginName)) return true;
            PrintError($"{pluginName} plugin doesn`t exist! (https://drive.google.com/drive/folders/1-18L-mG7yiGxR-PQYvd11VvXC2RQ4ZCu?usp=sharing)");
            Interface.Oxide.UnloadPlugin(Name);
            return false;
        }
        #endregion Helpers

        #region Commands
        [ChatCommand("supermarketstart")]
        private void ChatStartEvent(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                if (!Active) Start(null);
                else PrintToChat(player, GetMessage("EventActive", player.UserIDString, _config.Chat.Prefix));
            }
        }

        [ChatCommand("supermarketstop")]
        private void ChatStopEvent(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                if (Controller != null) Finish();
                else Interface.Oxide.ReloadPlugin(Name);
            }
        }

        [ChatCommand("supermarketpos")]
        private void ChatCommandPos(BasePlayer player)
        {
            if (Controller == null || !player.IsAdmin) return;
            Vector3 pos = Controller.transform.InverseTransformPoint(player.transform.position);
            Puts($"Position: {pos}");
            PrintToChat(player, $"Position: {pos}");
        }

        [ConsoleCommand("supermarketstart")]
        private void ConsoleStartEvent(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null) return;
            if (!Active)
            {
                if (arg.Args == null || arg.Args.Length != 1)
                {
                    Start(null);
                    return;
                }
                ulong steamId = Convert.ToUInt64(arg.Args[0]);
                BasePlayer target = BasePlayer.FindByID(steamId);
                if (target == null)
                {
                    Start(null);
                    Puts($"Player with SteamID {steamId} not found!");
                    return;
                }
                Start(target);
            }
            else Puts("This event is active now. To finish this event (supermarketstop), then to start the next one");
        }

        [ConsoleCommand("supermarketstop")]
        private void ConsoleStopEvent(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
            {
                if (Controller != null) Finish();
                else Interface.Oxide.ReloadPlugin(Name);
            }
        }
        #endregion Commands
    }
}

namespace Oxide.Plugins.SupermarketEventExtensionMethods
{
    public static class ExtensionMethods
    {
        public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return true;
            return false;
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

        public static TSource Min<TSource>(this IEnumerable<TSource> source, Func<TSource, float> predicate)
        {
            TSource result = default(TSource);
            float resultValue = float.MaxValue;
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

        public static TSource Max<TSource>(this IEnumerable<TSource> source, Func<TSource, double> predicate)
        {
            TSource result = default(TSource);
            double resultValue = double.MinValue;
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    TSource element = enumerator.Current;
                    double elementValue = predicate(element);
                    if (elementValue > resultValue)
                    {
                        result = element;
                        resultValue = elementValue;
                    }
                }
            }
            return result;
        }

        private static void Replace<TSource>(this IList<TSource> source, int x, int y)
        {
            TSource t = source[x];
            source[x] = source[y];
            source[y] = t;
        }

        private static List<TSource> QuickSort<TSource>(this List<TSource> source, Func<TSource, float> predicate, int minIndex, int maxIndex)
        {
            if (minIndex >= maxIndex) return source;

            int pivotIndex = minIndex - 1;
            for (int i = minIndex; i < maxIndex; i++)
            {
                if (predicate(source[i]) < predicate(source[maxIndex]))
                {
                    pivotIndex++;
                    source.Replace(pivotIndex, i);
                }
            }
            pivotIndex++;
            source.Replace(pivotIndex, maxIndex);

            QuickSort(source, predicate, minIndex, pivotIndex - 1);
            QuickSort(source, predicate, pivotIndex + 1, maxIndex);

            return source;
        }

        public static List<TSource> OrderByQuickSort<TSource>(this List<TSource> source, Func<TSource, float> predicate) => source.QuickSort(predicate, 0, source.Count - 1);

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