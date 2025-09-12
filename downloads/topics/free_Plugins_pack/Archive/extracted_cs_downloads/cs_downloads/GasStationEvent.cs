using System;
using System.Collections.Generic;
using Facepunch;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System.Collections;
using System.IO;
using UnityEngine.Networking;
using Rust.Ai;
using Oxide.Plugins.GasStationEventExtensionMethods;

namespace Oxide.Plugins
{
    [Info("GasStationEvent", "KpucTaJl", "1.3.2")]
    internal class GasStationEvent : RustPlugin
    {
        #region Config
        private const bool En = false;

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
            if (_config.PluginVersion < new VersionNumber(1, 0, 9))
            {
                _config.FirstNpc.Config.Name = "Vagos";
                _config.SecondNpc.Config.Name = "Ballas";
            }
            if (_config.PluginVersion < new VersionNumber(1, 1, 0))
            {
                _config.CanMountSecondCar = true;
            }
            if (_config.PluginVersion < new VersionNumber(1, 1, 1))
            {
                _config.MainPoint = new PointConfig
                {
                    Enabled = true,
                    Text = "◈",
                    Size = 45,
                    Color = "#CCFF00"
                };
                _config.AdditionalPoint = new PointConfig
                {
                    Enabled = true,
                    Text = "◆",
                    Size = 25,
                    Color = "#FFC700"
                };
            }
            if (_config.PluginVersion < new VersionNumber(1, 1, 5))
            {
                _config.CanOpenFirstDoor = false;
            }
            if (_config.PluginVersion < new VersionNumber(1, 1, 6))
            {
                _config.GameTip = new GameTipConfig
                {
                    IsGameTip = false,
                    Style = 2
                };
                _config.Marker = new MarkerConfig
                {
                    Enabled = true,
                    Type = 1,
                    Radius = 0.37967f,
                    Alpha = 0.35f,
                    Color = new ColorConfig { R = 0.81f, G = 0.25f, B = 0.15f },
                    Text = "GasStationEvent"
                };
            }
            if (_config.PluginVersion < new VersionNumber(1, 2, 2))
            {
                _config.PveMode.ScaleDamage = new Dictionary<string, float>
                {
                    ["Npc"] = 1f
                };
            }
            if (_config.PluginVersion < new VersionNumber(1, 2, 6))
            {
                _config.Chat = new ChatConfig
                {
                    IsChat = true,
                    Prefix = "[GasStationEvent]"
                };
                _config.DistanceAlerts = 0f;
                _config.Notify.Type = 0;
            }
            if (_config.PluginVersion < new VersionNumber(1, 2, 8))
            {
                _config.DefaultCrates = new HashSet<string>();
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
            [JsonProperty(En ? "Is this a blueprint? [true/false]" : "Это чертеж? [true/false]")] public bool IsBluePrint { get; set; }
            [JsonProperty("SkinID (0 - default)")] public ulong SkinId { get; set; }
            [JsonProperty(En ? "Name (empty - default)" : "Название (empty - default)")] public string Name { get; set; }
        }

        public class LootTableConfig
        {
            [JsonProperty(En ? "Minimum numbers of items" : "Минимальное кол-во элементов")] public int Min { get; set; }
            [JsonProperty(En ? "Maximum numbers of items" : "Максимальное кол-во элементов")] public int Max { get; set; }
            [JsonProperty(En ? "Use minimum and maximum values? [true/false]" : "Использовать минимальное и максимальное значение? [true/false]")] public bool UseCount { get; set; }
            [JsonProperty(En ? "List of items" : "Список предметов")] public List<ItemConfig> Items { get; set; }
        }

        public class PrefabConfig
        {
            [JsonProperty(En ? "Chance [0.0-100.0]" : "Шанс выпадения [0.0-100.0]")] public float Chance { get; set; }
            [JsonProperty(En ? "The path to the prefab" : "Путь к prefab-у")] public string PrefabDefinition { get; set; }
        }

        public class PrefabLootTableConfig
        {
            [JsonProperty(En ? "Minimum numbers of prefabs" : "Минимальное кол-во prefab-ов")] public int Min { get; set; }
            [JsonProperty(En ? "Maximum numbers of prefabs" : "Максимальное кол-во prefab-ов")] public int Max { get; set; }
            [JsonProperty(En ? "Use minimum and maximum values? [true/false]" : "Использовать минимальное и максимальное значение? [true/false]")] public bool UseCount { get; set; }
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
            [JsonProperty(En ? "Do you use the countdown GUI? [true/false]" : "Использовать ли GUI обратного отсчета? [true/false]")] public bool IsGui { get; set; }
            [JsonProperty("OffsetMin Y")] public string OffsetMinY { get; set; }
        }

        public class ChatConfig
        {
            [JsonProperty(En ? "Do you use the chat? [true/false]" : "Использовать ли чат? [true/false]")] public bool IsChat { get; set; }
            [JsonProperty(En ? "Prefix of chat messages" : "Префикс сообщений в чате")] public string Prefix { get; set; }
        }

        public class GameTipConfig
        {
            [JsonProperty(En ? "Use Facepunch Game Tips (notification bar above hotbar)? [true/false]" : "Использовать ли Facepunch Game Tip (оповещения над слотами быстрого доступа игрока)? [true/false]")] public bool IsGameTip { get; set; }
            [JsonProperty(En ? "Style (0 - Blue Normal, 1 - Red Normal, 2 - Blue Long, 3 - Blue Short, 4 - Server Event)" : "Стиль (0 - Blue Normal, 1 - Red Normal, 2 - Blue Long, 3 - Blue Short, 4 - Server Event)")] public int Style { get; set; }
        }

        public class GuiAnnouncementsConfig
        {
            [JsonProperty(En ? "Do you use the GUI Announcements? [true/false]" : "Использовать ли GUI Announcements? [true/false]")] public bool IsGuiAnnouncements { get; set; }
            [JsonProperty(En ? "Banner color" : "Цвет баннера")] public string BannerColor { get; set; }
            [JsonProperty(En ? "Text color" : "Цвет текста")] public string TextColor { get; set; }
            [JsonProperty(En ? "Adjust Vertical Position" : "Отступ от верхнего края")] public float ApiAdjustVPosition { get; set; }
        }

        public class NotifyConfig
        {
            [JsonProperty(En ? "Do you use the Notify? [true/false]" : "Использовать ли Notify? [true/false]")] public bool IsNotify { get; set; }
            [JsonProperty(En ? "Type" : "Тип")] public int Type { get; set; }
        }

        public class DiscordConfig
        {
            [JsonProperty(En ? "Use the plugin DiscordMessages for posting event notifications? [true/false]" : "Использовать ли Discord? [true/false]")] public bool IsDiscord { get; set; }
            [JsonProperty("Webhook URL")] public string WebhookUrl { get; set; }
            [JsonProperty(En ? "Embed Color (DECIMAL)" : "Цвет полосы (DECIMAL)")] public int EmbedColor { get; set; }
            [JsonProperty(En ? "Keys of required messages" : "Ключи необходимых сообщений")] public HashSet<string> Keys { get; set; }
        }

        public class EconomyConfig
        {
            [JsonProperty(En ? "Which economy plugins do you want to use? (Economics, Server Rewards, IQEconomic)" : "Какие плагины экономики вы хотите использовать? (Economics, Server Rewards, IQEconomic)")] public HashSet<string> Plugins { get; set; }
            [JsonProperty(En ? "The minimum value that a player must collect to get points for the economy" : "Минимальное значение, которое игрок должен заработать, чтобы получить баллы за экономику")] public double Min { get; set; }
            [JsonProperty(En ? "Looting of crates" : "Ограбление ящиков")] public Dictionary<string, double> Crates { get; set; }
            [JsonProperty(En ? "Killing an NPC" : "Убийство NPC")] public double Npc { get; set; }
            [JsonProperty(En ? "Opening the first door" : "Открытие первой двери")] public double OpenFirstDoor { get; set; }
            [JsonProperty(En ? "Opening the second door" : "Открытие второй двери")] public double OpenSecondDoor { get; set; }
            [JsonProperty(En ? "List of commands that are executed in the console at the end of the event ({steamid} - the player who collected the highest number of points)" : "Список команд, которые выполняются в консоли по окончанию ивента ({steamid} - игрок, который набрал наибольшее кол-во баллов)")] public HashSet<string> Commands { get; set; }
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
            [JsonProperty("SkinID (0 - default)")] public ulong SkinID { get; set; }
            [JsonProperty(En ? "Mods" : "Модификации на оружие")] public HashSet<string> Mods { get; set; }
            [JsonProperty(En ? "Ammo" : "Боеприпасы")] public string Ammo { get; set; }
        }

        public class NpcWear
        {
            [JsonProperty("ShortName")] public string ShortName { get; set; }
            [JsonProperty("SkinID (0 - default)")] public ulong SkinID { get; set; }
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
            [JsonProperty(En ? "List of locations" : "Список расположений")] public List<string> Positions { get; set; }
            [JsonProperty(En ? "NPCs setting" : "Настройки NPC")] public NpcConfig Config { get; set; }
            [JsonProperty(En ? "Which loot table should the plugin use? (0 - default; 1 - own; 2 - AlphaLoot; 3 - CustomLoot; 4 - loot table of the Rust objects; 5 - combine the 1 and 4 methods)" : "Какую таблицу предметов необходимо использовать? (0 - стандартную; 1 - собственную; 2 - AlphaLoot; 3 - CustomLoot; 4 - таблица предметов объектов Rust; 5 - совместить 1 и 4 методы)")] public int TypeLootTable { get; set; }
            [JsonProperty(En ? "Loot table from prefabs (if the loot table type is 4 or 5)" : "Таблица предметов из prefab-ов (если тип таблицы предметов - 4 или 5)")] public PrefabLootTableConfig PrefabLootTable { get; set; }
            [JsonProperty(En ? "Own loot table (if the loot table type is 1 or 5)" : "Собственная таблица предметов (если тип таблицы предметов - 1 или 5)")] public LootTableConfig OwnLootTable { get; set; }
        }

        private class PluginConfig
        {
            [JsonProperty(En ? "Minimum time between events [sec.]" : "Минимальное время между ивентами [sec.]")] public float MinStartTime { get; set; }
            [JsonProperty(En ? "Maximum time between events [sec.]" : "Максимальное время между ивентами [sec.]")] public float MaxStartTime { get; set; }
            [JsonProperty(En ? "Is the countdown timer active for the event? [true/false]" : "Активен ли таймер для запуска ивента? [true/false]")] public bool EnabledTimer { get; set; }
            [JsonProperty(En ? "Duration of the event [sec.]" : "Время проведения ивента [sec.]")] public int FinishTime { get; set; }
            [JsonProperty(En ? "Time before the starting of the event after receiving a chat message [sec.]" : "Время до начала ивента после сообщения в чате [sec.]")] public float PreStartTime { get; set; }
            [JsonProperty(En ? "Notification time until the end of the event [sec.]" : "Время оповещения до окончания ивента [sec.]")] public int PreFinishTime { get; set; }
            [JsonProperty(En ? "Loot crate settings for the first room" : "Список ящиков для первой комнаты")] public HashSet<CrateConfig> FirstCrates { get; set; }
            [JsonProperty(En ? "Loot crate settings for the second room" : "Список ящиков для второй комнаты")] public HashSet<CrateConfig> SecondCrates { get; set; }
            [JsonProperty(En ? "NPC settings for the first car" : "Настройка NPC, которые высаживаются из первой машины")] public PresetConfig FirstNpc { get; set; }
            [JsonProperty(En ? "NPC settings for the second car" : "Настройка NPC, которые высаживаются из второй машины")] public PresetConfig SecondNpc { get; set; }
            [JsonProperty(En ? "Marker configuration on the map" : "Настройка маркера на карте")] public MarkerConfig Marker { get; set; }
            [JsonProperty(En ? "Main marker settings for key event points shown on players screen" : "Настройки основного маркера на экране игрока")] public PointConfig MainPoint { get; set; }
            [JsonProperty(En ? "Additional marker settings for key event points shown on players screen" : "Настройки дополнительного маркера на экране игрока")] public PointConfig AdditionalPoint { get; set; }
            [JsonProperty(En ? "GUI setting" : "Настройки GUI")] public GuiConfig Gui { get; set; }
            [JsonProperty(En ? "Chat setting" : "Настройки чата")] public ChatConfig Chat { get; set; }
            [JsonProperty(En ? "Facepunch Game Tips setting" : "Настройка сообщений Facepunch Game Tip")] public GameTipConfig GameTip { get; set; }
            [JsonProperty(En ? "GUI Announcements setting" : "Настройка GUI Announcements")] public GuiAnnouncementsConfig GuiAnnouncements { get; set; }
            [JsonProperty(En ? "Notify setting" : "Настройка Notify")] public NotifyConfig Notify { get; set; }
            [JsonProperty(En ? "The distance from the event to the player for global alerts (0 - no limit)" : "Расстояние от ивента до игрока для глобальных оповещений (0 - нет ограничений)")] public float DistanceAlerts { get; set; }
            [JsonProperty(En ? "Discord setting (only for users DiscordMessages plugin)" : "Настройка оповещений в Discord (только для тех, кто использует плагин DiscordMessages)")] public DiscordConfig Discord { get; set; }
            [JsonProperty(En ? "Radius of the event zone" : "Радиус зоны ивента")] public float Radius { get; set; }
            [JsonProperty(En ? "Do you create a PVP zone in the event area? (only for users TruePVE plugin) [true/false]" : "Создавать зону PVP в зоне проведения ивента? (только для тех, кто использует плагин TruePVE) [true/false]")] public bool IsCreateZonePvp { get; set; }
            [JsonProperty(En ? "PVE Mode Setting (only for users PveMode plugin)" : "Настройка PVE режима работы плагина (только для тех, кто использует плагин PveMode)")] public PveModeConfig PveMode { get; set; }
            [JsonProperty(En ? "Interrupt the teleport in the event area? (only for users NTeleportation plugin) [true/false]" : "Запрещать телепорт в зоне проведения ивента? (только для тех, кто использует плагин NTeleportation) [true/false]")] public bool NTeleportationInterrupt { get; set; }
            [JsonProperty(En ? "Disable NPCs from the BetterNpc plugin on the monument while the event is on? [true/false]" : "Отключать NPC из плагина BetterNpc на монументе пока проходит ивент? [true/false]")] public bool RemoveBetterNpc { get; set; }
            [JsonProperty(En ? "Economy setting (total values will be added up and rewarded at the end of the event)" : "Настройка экономики (конечное значение суммируется и будет выдано игрокам по окончанию ивента)")] public EconomyConfig Economy { get; set; }
            [JsonProperty(En ? "List of commands banned in the event zone" : "Список команд запрещенных в зоне ивента")] public HashSet<string> Commands { get; set; }
            [JsonProperty(En ? "Engine parts quality for the second car (1 = low, 2 = medium, 3 = high)" : "Уровень компонентов второй машины (1, 2, 3)")] public int CarComponentLevel { get; set; }
            [JsonProperty(En ? "Can players mount in the second car? [true/false]" : "Могут ли игроки садиться во вторую машину? [true/false]")] public bool CanMountSecondCar { get; set; }
            [JsonProperty(En ? "Should the first room door open without entering the password when all NPCs from the second car have been killed? [true/false]" : "Должна ли открываться дверь в первую комнату без ввода пароля, когда все Npc из второй машины были убиты? [true/false]")] public bool CanOpenFirstDoor { get; set; }
            [JsonProperty(En ? "The list of crates on the monument that must be killed before the start of the event" : "Список ящиков на монументе, которые должны быть удалены перед началом ивента")] public HashSet<string> DefaultCrates { get; set; }
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
                    FirstCrates = new HashSet<CrateConfig>
                    {
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                            Position = "(-12.349, 4.488, 19.361)",
                            Rotation = "(0, 114.4, 0)",
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
                            Position = "(-12.512, 3.25, 18.659)",
                            Rotation = "(0, 310.261, 0)",
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
                            Position = "(-13.664, 3.25, 19.916)",
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
                            Prefab = "assets/bundled/prefabs/radtown/crate_normal_2_food.prefab",
                            Position = "(-13.357, 4.488, 19.861)",
                            Rotation = "(0, 31.016, 0)",
                            TypeLootTable = 0,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_normal_2_food.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 1, MaxAmount = 1, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" } }
                            }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_normal_2_medical.prefab",
                            Position = "(-12.215, 3.25, 20.066)",
                            Rotation = "(0, 23.649, 0)",
                            TypeLootTable = 0,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_normal_2_medical.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 1, MaxAmount = 1, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" } }
                            }
                        }
                    },
                    SecondCrates = new HashSet<CrateConfig>
                    {
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/underwater_labs/crate_normal.prefab",
                            Position = "(12.709, -5.026, 14.924)",
                            Rotation = "(353.106, 71.077, 28.212)",
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
                            Prefab = "assets/bundled/prefabs/radtown/underwater_labs/crate_normal_2.prefab",
                            Position = "(13.95, -5.113, 16.062)",
                            Rotation = "(353.052, 75.008, 34.306)",
                            TypeLootTable = 0,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/underwater_labs/crate_normal_2.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 5, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" } }
                            }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/underwater_labs/crate_normal_2.prefab",
                            Position = "(16.564, -5.029, 15.672)",
                            Rotation = "(2.71, 37.996, 33.909)",
                            TypeLootTable = 0,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/underwater_labs/crate_normal_2.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 5, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" } }
                            }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_normal_2_food.prefab",
                            Position = "(10.778, -3.862, 16.185)",
                            Rotation = "(21.782, 7.434, 324.395)",
                            TypeLootTable = 0,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_normal_2_food.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 1, MaxAmount = 1, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" } }
                            }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_normal_2_medical.prefab",
                            Position = "(13.803, -4.279, 14.274)",
                            Rotation = "(30.908, 358.572, 12.651)",
                            TypeLootTable = 0,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_normal_2_medical.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 1, MaxAmount = 1, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" } }
                            }
                        }
                    },
                    FirstNpc = new PresetConfig
                    {
                        Min = 4,
                        Max = 4,
                        Positions = new List<string>
                        {
                            "(-1.661, 3.25, -13.205)",
                            "(-0.415, 3.25, -5.062)",
                            "(-0.415, 3.25, 3.988)",
                            "(-14.444, 3.25, 1.745)",
                            "(-13.462, 3.25, -5.102)",
                            "(-13.487, 3.25, 16.148)"
                        },
                        Config = new NpcConfig
                        {
                            Name = "Vagos",
                            Health = 125f,
                            RoamRange = 5f,
                            ChaseRange = 30f,
                            AttackRangeMultiplier = 3f,
                            SenseRange = 30f,
                            MemoryDuration = 15f,
                            DamageScale = 1.0f,
                            AimConeScale = 1.0f,
                            CheckVisionCone = false,
                            VisionCone = 135f,
                            Speed = 7.5f,
                            DisableRadio = true,
                            Stationary = false,
                            IsRemoveCorpse = true,
                            WearItems = new HashSet<NpcWear>
                            {
                                new NpcWear { ShortName = "hat.cap", SkinID = 2414488473 },
                                new NpcWear { ShortName = "hoodie", SkinID = 920529282 },
                                new NpcWear { ShortName = "shoes.boots", SkinID = 2592902166 },
                                new NpcWear { ShortName = "pants", SkinID = 1295278038 },
                                new NpcWear { ShortName = "sunglasses", SkinID = 0 }
                            },
                            BeltItems = new HashSet<NpcBelt>
                            {
                                new NpcBelt { ShortName = "pistol.revolver", Amount = 1, SkinID = 2932859228, Mods = new HashSet<string>(), Ammo = string.Empty },
                                new NpcBelt { ShortName = "syringe.medical", Amount = 2, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
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
                            Items = new List<ItemConfig>
                            {
                                new ItemConfig { ShortName = "scrap", MinAmount = 8, MaxAmount = 33, Chance = 36f, IsBluePrint = false, SkinId = 0, Name = "" }
                            }
                        }
                    },
                    SecondNpc = new PresetConfig
                    {
                        Min = 4,
                        Max = 4,
                        Positions = new List<string>
                        {
                            "(26.901, 3.25, -10.623)",
                            "(28.735, 3.25, -2.477)",
                            "(23.599, 3.25, 7.294)",
                            "(19.353, 3.25, 19.4)",
                            "(11.958, 3.25, 17.853)",
                            "(31.545, 3.25, 21.983)"
                        },
                        Config = new NpcConfig
                        {
                            Name = "Ballas",
                            Health = 125f,
                            RoamRange = 5f,
                            ChaseRange = 30f,
                            AttackRangeMultiplier = 3f,
                            SenseRange = 30f,
                            MemoryDuration = 15f,
                            DamageScale = 1.0f,
                            AimConeScale = 1.0f,
                            CheckVisionCone = false,
                            VisionCone = 135f,
                            Speed = 7.5f,
                            DisableRadio = true,
                            Stationary = false,
                            IsRemoveCorpse = true,
                            WearItems = new HashSet<NpcWear>
                            {
                                new NpcWear { ShortName = "mask.bandana", SkinID = 1583890819 },
                                new NpcWear { ShortName = "hoodie", SkinID = 1467513541 },
                                new NpcWear { ShortName = "shoes.boots", SkinID = 1135820993 },
                                new NpcWear { ShortName = "pants", SkinID = 1134379860 },
                                new NpcWear { ShortName = "metal.plate.torso", SkinID = 1134374285 },
                                new NpcWear { ShortName = "burlap.gloves", SkinID = 1134361393 }
                            },
                            BeltItems = new HashSet<NpcBelt>
                            {
                                new NpcBelt { ShortName = "pistol.semiauto", Amount = 1, SkinID = 864800401, Mods = new HashSet<string>(), Ammo = string.Empty },
                                new NpcBelt { ShortName = "syringe.medical", Amount = 2, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
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
                            Items = new List<ItemConfig>
                            {
                                new ItemConfig { ShortName = "scrap", MinAmount = 8, MaxAmount = 33, Chance = 36f, IsBluePrint = false, SkinId = 0, Name = "" }
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
                        Text = "GasStationEvent"
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
                        Prefix = "[GasStationEvent]"
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
                            "KillAllVagos",
                            "KillAllBallas"
                        }
                    },
                    Radius = 45f,
                    IsCreateZonePvp = false,
                    PveMode = new PveModeConfig
                    {
                        Pve = false,
                        Damage = 500f,
                        ScaleDamage = new Dictionary<string, float> { ["Npc"] = 1f },
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
                        Plugins = new HashSet<string> { "Economics", "Server Rewards", "IQEconomic" },
                        Min = 0,
                        Crates = new Dictionary<string, double>
                        {
                            ["crate_normal"] = 0.3,
                            ["crate_normal_2"] = 0.2,
                            ["crate_normal_2_food"] = 0.1,
                            ["crate_normal_2_medical"] = 0.1
                        },
                        Npc = 0.3,
                        OpenFirstDoor = 0.4,
                        OpenSecondDoor = 0.4,
                        Commands = new HashSet<string>()
                    },
                    Commands = new HashSet<string>
                    {
                        "/remove",
                        "remove.toggle"
                    },
                    CarComponentLevel = 1,
                    CanMountSecondCar = true,
                    CanOpenFirstDoor = false,
                    DefaultCrates = new HashSet<string>(),
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
                ["PreStart"] = "{0} There was a conflict between two gangs <color=#55aaff>Vagos</color> and <color=#55aaff>Ballas</color>. After <color=#55aaff>{1}</color> <color=#55aaff>Vagos</color> want to attack the <color=#55aaff>Ballas</color> base to free their friend from captivity",
                ["Start"] = "{0} As a result of the chase, <color=#55aaff>Vagos</color> lost control of their car and crashed into a gas tank at <color=#55aaff>Oxum's Gas Station</color> monument in the square <color=#55aaff>{1}</color>. A shootout began between both gangs. <color=#738d43>Help</color> one of the gangs cope with the second and then they <color=#738d43>will thank you</color> for your help",
                ["PreFinish"] = "{0} The Oxum's Gas Station Event <color=#ce3f27>will end</color> in <color=#55aaff>{1}</color>!",
                ["Finish"] = "{0} The Oxum's Gas Station Event <color=#ce3f27>has concluded</color>!",
                ["KillAllVagos"] = "{0} All members of the <color=#55aaff>Vagos</color> gang are <color=#ce3f27>killed</color>. As a thank you, the <color=#55aaff>Ballas</color> gang <color=#738d43>opened their hiding place</color> in the basement of the auto repair shop at the Oxum's Gas Station monument. You <color=#738d43>can take</color> everything you need from there",
                ["KillAllBallas"] = "{0} All members of the <color=#55aaff>Ballas</color> gang are <color=#ce3f27>killed</color>. As a thank you, the <color=#55aaff>Vagos</color> gang <color=#738d43>shared the password to their hiding place</color> in the toilet at the Oxum's Gas Station monument. You <color=#738d43>can take</color> everything you need from there",
                ["SetOwner"] = "{0} Player <color=#55aaff>{1}</color> <color=#738d43>has received</color> the owner status for the <color=#55aaff>Gas Station Event</color>",
                ["EventActive"] = "{0} This event is active now. To finish this event (<color=#55aaff>/gsstop</color>), then (<color=#55aaff>/gsstart</color>) to start the next one!",
                ["EnterPVP"] = "{0} You <color=#ce3f27>have entered</color> the PVP zone, now other players <color=#ce3f27>can damage</color> you!",
                ["ExitPVP"] = "{0} You <color=#738d43>have left</color> the PVP zone, now other players <color=#738d43>cannot damage</color> you!",
                ["NTeleportation"] = "{0} You <color=#ce3f27>cannot</color> teleport into the event zone!",
                ["SendEconomy"] = "{0} You <color=#738d43>have earned</color> <color=#55aaff>{1}</color> economics for participating in the event",
                ["NoCommand"] = "{0} You <color=#ce3f27>cannot</color> use this command in the event zone!"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PreStart"] = "{0} Между двумя бандами <color=#55aaff>Vagos</color> и <color=#55aaff>Ballas</color> произошел конфликт. Через <color=#55aaff>{1}</color> <color=#55aaff>Vagos</color> хотят напасть на базу <color=#55aaff>Ballas</color>, чтобы освободить из плена своего друга",
                ["Start"] = "{0} В результате погони <color=#55aaff>Vagos</color> не справились с управлением их автомобиля и врезались в бензобак на <color=#55aaff>Заправке</color> в квадрате <color=#55aaff>{1}</color>. Между бандами началась перестрелка. <color=#738d43>Помогите</color> одной из банд справится со второй и тогда они <color=#738d43>отблагодарят</color> вас за помощь",
                ["PreFinish"] = "{0} Ивент на Заправке <color=#ce3f27>закончится</color> через <color=#55aaff>{1}</color>",
                ["Finish"] = "{0} Ивент на Заправке <color=#ce3f27>окончен</color>",
                ["KillAllVagos"] = "{0} Все участники банды <color=#55aaff>Vagos</color> <color=#ce3f27>убиты</color>. В качестве благодарности банда <color=#55aaff>Ballas</color> <color=#738d43>открыла свой тайник</color> в подвале автомастерской на Заправке. Вы <color=#738d43>можете взять</color> оттуда все, что вам необходимо",
                ["KillAllBallas"] = "{0} Все участники банды <color=#55aaff>Ballas</color> <color=#ce3f27>убиты</color>. В качестве благодарности банда <color=#55aaff>Vagos</color> <color=#738d43>поделилась паролем от своего тайника</color> в туалете на Заправке. Вы <color=#738d43>можете взять</color> оттуда все, что вам необходимо",
                ["SetOwner"] = "{0} Игрок <color=#55aaff>{1}</color> <color=#738d43>получил</color> статус владельца ивента для <color=#55aaff>Gas Station Event</color>",
                ["EventActive"] = "{0} Ивент в данный момент активен, сначала завершите текущий ивент (<color=#55aaff>/gsstop</color>), чтобы начать следующий!",
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
        private static GasStationEvent _ins;

        private void Init()
        {
            _ins = this;
            ToggleHooks(false);
        }

        private void OnServerInitialized()
        {
            if (GetMonument() == null)
            {
                PrintError("The Oxum's Gas Station location is missing on the map. The plugin cannot be loaded!");
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

        private object OnEntityTakeDamage(ScientistNPC npc, HitInfo info)
        {
            if (npc == null || info == null) return null;
            if (!Controller.IsArrived && (Controller.FirstScientists.Contains(npc) || Controller.SecondScientists.Contains(npc))) return true;
            BasePlayer attacker = info.InitiatorPlayer;
            if (!attacker.IsPlayer()) return null;
            if (Controller.FirstScientists.Contains(npc) && !Controller.FirstAttackPlayers.Contains(attacker)) Controller.FirstAttackPlayers.Add(attacker);
            else if (Controller.SecondScientists.Contains(npc) && !Controller.SecondAttackPlayers.Contains(attacker)) Controller.SecondAttackPlayers.Add(attacker);
            return null;
        }

        private object OnEntityTakeDamage(Door door, HitInfo info)
        {
            if (door != null && door == Controller.SecondDoor) return true;
            else return null;
        }

        private object OnEntityTakeDamage(BuildingBlock buildingBlock, HitInfo info)
        {
            if (buildingBlock != null && buildingBlock == Controller.Doorway) return true;
            else return null;
        }

        private object OnEntityTakeDamage(BasicCar car, HitInfo info)
        {
            if (car != null && car == Controller.FirstCar) return true;
            else return null;
        }

        private object OnEntityTakeDamage(ModularCar car, HitInfo info)
        {
            if (!Controller.IsArrived && car != null && car == Controller.SecondCar) return true;
            else return null;
        }

        private void OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
        {
            if (codeLock == null || !player.IsPlayer() || string.IsNullOrEmpty(code)) return;
            Controller.TryToOpenDoor(player, codeLock, code);
        }

        private object CanMountEntity(BasePlayer player, BaseMountable entity)
        {
            if (_config.CanMountSecondCar || !player.IsPlayer() || entity == null || Controller.SecondCar == null) return null;
            ModularCar parent = entity.VehicleParent() as ModularCar;
            if (parent == null) return null;
            if (parent == Controller.SecondCar) return true;
            else return null;
        }

        private object OnVehiclePush(ModularCar vehicle, BasePlayer player)
        {
            if (_config.CanMountSecondCar || vehicle == null || !player.IsPlayer() || Controller.SecondCar == null) return null;
            if (vehicle == Controller.SecondCar) return true;
            else return null;
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (!_config.Marker.Enabled || Controller == null || !player.IsPlayer()) return;
            if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot)) timer.In(2f, () => OnPlayerConnected(player));
            else Controller.UpdateMapMarkers();
        }

        private object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player.IsPlayer() && Controller.Players.Contains(player)) Controller.ExitPlayer(player);
            return null;
        }

        private void OnEntityDeath(ScientistNPC npc, HitInfo info)
        {
            if (npc == null || info == null) return;
            BasePlayer attacker = info.InitiatorPlayer;
            if (!attacker.IsPlayer()) return;
            if (Controller.FirstScientists.Contains(npc) || Controller.SecondScientists.Contains(npc)) ActionEconomy(attacker.userID, "Npc");
        }

        private void OnEntityKill(LootContainer entity)
        {
            if (entity == null || Controller == null) return;
            if (Controller.Crates.ContainsKey(entity)) Controller.Crates.Remove(entity);
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

        #region Car Controllers
        internal class ControllerFirstCar : FacepunchBehaviour
        {
            private BasicCar Car { get; set; } = null;

            internal List<Vector3> Path { get; set; } = null;

            private Vector3 LastPos { get; set; } = Vector3.zero;

            private void Awake()
            {
                Car = GetComponent<BasicCar>();
                LastPos = transform.position;
            }

            private void FixedUpdate()
            {
                if (Path.Count == 0 || Vector3.Distance(transform.position, Path[Path.Count - 1]) < 5f)
                {
                    Path.Clear();
                    Car.steering = 0f;
                    Car.brakePedal = 30f;
                    Car.gasPedal = 0f;
                    _ins.Controller.FinishPathFirstCar(this);
                    return;
                }

                if (Vector3.Distance(transform.position, Path[0]) < 5f)
                {
                    Path.RemoveAt(0);
                    return;
                }

                float velocity = Vector3.Distance(transform.position, LastPos) / Time.deltaTime;
                LastPos = transform.position;

                Vector3 target = Path.Count > 2 && velocity > 15f ? Path[2] : Path.Count > 1 && velocity > 10f ? Path[1] : Path[0];

                float dot = Vector3.Dot(transform.right, (target - transform.position).normalized);
                float absDot = Math.Abs(dot);
                bool isBrake = (absDot > 0.5f && velocity > 5f) || (Path.Count > 2 && velocity > 20f);
                Car.steering = dot * 60f;
                Car.brakePedal = isBrake ? 30f : 0f;
                Car.gasPedal = isBrake ? 0f : (1 - absDot) * 100f;
            }
        }

        internal class ControllerSecondCar : FacepunchBehaviour
        {
            private ModularCar Car { get; set; } = null;
            private BasePlayer Driver { get; set; } = null;

            internal List<Vector3> Path { get; set; } = null;

            private Vector3 LastPos { get; set; } = Vector3.zero;

            private void Awake()
            {
                Car = GetComponent<ModularCar>();
                Driver = Car.GetDriver();
                LastPos = transform.position;
            }

            private void FixedUpdate()
            {
                if (Path.Count == 0)
                {
                    Driver.serverInput.current.buttons = (int)BUTTON.SPRINT;
                    Car.PlayerServerInput(Driver.serverInput, Driver);
                    _ins.Controller.FinishPathSecondCar(this);
                    return;
                }

                if (Vector3.Distance(transform.position, Path[0]) < 5f)
                {
                    Path.RemoveAt(0);
                    return;
                }

                float velocity = Vector3.Distance(transform.position, LastPos) / Time.deltaTime;
                LastPos = transform.position;

                Vector3 target = Path.Count == 4 ? Path[1] : Path.Count <= 3 ? Path[0] : Path.Count > 1 && velocity > 10f ? Path[1] : Path[0];

                float dot = Vector3.Dot(transform.right, (target - transform.position).normalized);
                bool isBrake = Math.Abs(dot) > 0.5f || velocity > 20f || (Path.Count == 4 && velocity > 15f) || (Path.Count <= 3 && Path.Count > 1 && velocity > 10f) || (Path.Count == 1 && velocity > 5f);

                if (isBrake)
                {
                    if (dot < -0.2f) Driver.serverInput.current.buttons = (int)BUTTON.SPRINT | (int)BUTTON.LEFT;
                    else if (dot > 0.2f) Driver.serverInput.current.buttons = (int)BUTTON.SPRINT | (int)BUTTON.RIGHT;
                    else Driver.serverInput.current.buttons = (int)BUTTON.SPRINT;
                }
                else
                {
                    if (dot < -0.2f) Driver.serverInput.current.buttons = (int)BUTTON.FORWARD | (int)BUTTON.LEFT;
                    else if (dot > 0.2f) Driver.serverInput.current.buttons = (int)BUTTON.FORWARD | (int)BUTTON.RIGHT;
                    else Driver.serverInput.current.buttons = (int)BUTTON.FORWARD;
                }

                Car.PlayerServerInput(Driver.serverInput, Driver);
            }
        }
        #endregion Car Controllers

        #region Controller
        internal HashSet<Vector3> Marker { get; } = new HashSet<Vector3>
        {
            new Vector3(48f, 0f, 8f),
            new Vector3(48f, 0f, 6f),
            new Vector3(48f, 0f, 4f),
            new Vector3(48f, 0f, 2f),
            new Vector3(48f, 0f, 0f),
            new Vector3(48f, 0f, -2f),
            new Vector3(48f, 0f, -4f),
            new Vector3(48f, 0f, -6f),
            new Vector3(48f, 0f, -8f),
            new Vector3(46f, 0f, 16f),
            new Vector3(46f, 0f, 14f),
            new Vector3(46f, 0f, 12f),
            new Vector3(46f, 0f, 10f),
            new Vector3(46f, 0f, 8f),
            new Vector3(46f, 0f, 6f),
            new Vector3(46f, 0f, 4f),
            new Vector3(46f, 0f, 2f),
            new Vector3(46f, 0f, 0f),
            new Vector3(46f, 0f, -2f),
            new Vector3(46f, 0f, -4f),
            new Vector3(46f, 0f, -6f),
            new Vector3(46f, 0f, -8f),
            new Vector3(46f, 0f, -10f),
            new Vector3(46f, 0f, -12f),
            new Vector3(46f, 0f, -14f),
            new Vector3(46f, 0f, -16f),
            new Vector3(44f, 0f, 20f),
            new Vector3(44f, 0f, 18f),
            new Vector3(44f, 0f, 16f),
            new Vector3(44f, 0f, 14f),
            new Vector3(44f, 0f, 12f),
            new Vector3(44f, 0f, 10f),
            new Vector3(44f, 0f, -10f),
            new Vector3(44f, 0f, -12f),
            new Vector3(44f, 0f, -14f),
            new Vector3(44f, 0f, -16f),
            new Vector3(44f, 0f, -18f),
            new Vector3(44f, 0f, -20f),
            new Vector3(42f, 0f, 24f),
            new Vector3(42f, 0f, 22f),
            new Vector3(42f, 0f, 20f),
            new Vector3(42f, 0f, 18f),
            new Vector3(42f, 0f, -18f),
            new Vector3(42f, 0f, -20f),
            new Vector3(42f, 0f, -22f),
            new Vector3(42f, 0f, -24f),
            new Vector3(40f, 0f, 26f),
            new Vector3(40f, 0f, 24f),
            new Vector3(40f, 0f, 22f),
            new Vector3(40f, 0f, -22f),
            new Vector3(40f, 0f, -24f),
            new Vector3(40f, 0f, -26f),
            new Vector3(38f, 0f, 30f),
            new Vector3(38f, 0f, 28f),
            new Vector3(38f, 0f, 26f),
            new Vector3(38f, 0f, -26f),
            new Vector3(38f, 0f, -28f),
            new Vector3(38f, 0f, -30f),
            new Vector3(36f, 0f, 32f),
            new Vector3(36f, 0f, 30f),
            new Vector3(36f, 0f, 28f),
            new Vector3(36f, 0f, -28f),
            new Vector3(36f, 0f, -30f),
            new Vector3(36f, 0f, -32f),
            new Vector3(34f, 0f, 34f),
            new Vector3(34f, 0f, 32f),
            new Vector3(34f, 0f, 30f),
            new Vector3(34f, 0f, -30f),
            new Vector3(34f, 0f, -32f),
            new Vector3(34f, 0f, -34f),
            new Vector3(32f, 0f, 36f),
            new Vector3(32f, 0f, 34f),
            new Vector3(32f, 0f, 32f),
            new Vector3(32f, 0f, -32f),
            new Vector3(32f, 0f, -34f),
            new Vector3(32f, 0f, -36f),
            new Vector3(30f, 0f, 38f),
            new Vector3(30f, 0f, 36f),
            new Vector3(30f, 0f, 34f),
            new Vector3(30f, 0f, -34f),
            new Vector3(30f, 0f, -36f),
            new Vector3(30f, 0f, -38f),
            new Vector3(28f, 0f, 38f),
            new Vector3(28f, 0f, 36f),
            new Vector3(28f, 0f, 10f),
            new Vector3(28f, 0f, 8f),
            new Vector3(28f, 0f, 6f),
            new Vector3(28f, 0f, 4f),
            new Vector3(28f, 0f, 2f),
            new Vector3(28f, 0f, 0f),
            new Vector3(28f, 0f, -2f),
            new Vector3(28f, 0f, -4f),
            new Vector3(28f, 0f, -6f),
            new Vector3(28f, 0f, -8f),
            new Vector3(28f, 0f, -10f),
            new Vector3(28f, 0f, -12f),
            new Vector3(28f, 0f, -14f),
            new Vector3(28f, 0f, -16f),
            new Vector3(28f, 0f, -36f),
            new Vector3(28f, 0f, -38f),
            new Vector3(26f, 0f, 40f),
            new Vector3(26f, 0f, 38f),
            new Vector3(26f, 0f, 12f),
            new Vector3(26f, 0f, 10f),
            new Vector3(26f, 0f, 8f),
            new Vector3(26f, 0f, 0f),
            new Vector3(26f, 0f, -2f),
            new Vector3(26f, 0f, -4f),
            new Vector3(26f, 0f, -14f),
            new Vector3(26f, 0f, -16f),
            new Vector3(26f, 0f, -18f),
            new Vector3(26f, 0f, -38f),
            new Vector3(26f, 0f, -40f),
            new Vector3(24f, 0f, 42f),
            new Vector3(24f, 0f, 40f),
            new Vector3(24f, 0f, 14f),
            new Vector3(24f, 0f, 12f),
            new Vector3(24f, 0f, 10f),
            new Vector3(24f, 0f, 2f),
            new Vector3(24f, 0f, 0f),
            new Vector3(24f, 0f, -2f),
            new Vector3(24f, 0f, -18f),
            new Vector3(24f, 0f, -38f),
            new Vector3(24f, 0f, -40f),
            new Vector3(24f, 0f, -42f),
            new Vector3(22f, 0f, 42f),
            new Vector3(22f, 0f, 40f),
            new Vector3(22f, 0f, 16f),
            new Vector3(22f, 0f, 14f),
            new Vector3(22f, 0f, 12f),
            new Vector3(22f, 0f, 10f),
            new Vector3(22f, 0f, 8f),
            new Vector3(22f, 0f, 6f),
            new Vector3(22f, 0f, 4f),
            new Vector3(22f, 0f, 2f),
            new Vector3(22f, 0f, 0f),
            new Vector3(22f, 0f, -18f),
            new Vector3(22f, 0f, -40f),
            new Vector3(22f, 0f, -42f),
            new Vector3(20f, 0f, 44f),
            new Vector3(20f, 0f, 42f),
            new Vector3(20f, 0f, 18f),
            new Vector3(20f, 0f, 16f),
            new Vector3(20f, 0f, 14f),
            new Vector3(20f, 0f, 12f),
            new Vector3(20f, 0f, 10f),
            new Vector3(20f, 0f, 8f),
            new Vector3(20f, 0f, 6f),
            new Vector3(20f, 0f, 4f),
            new Vector3(20f, 0f, 2f),
            new Vector3(20f, 0f, -18f),
            new Vector3(20f, 0f, -42f),
            new Vector3(20f, 0f, -44f),
            new Vector3(18f, 0f, 44f),
            new Vector3(18f, 0f, 42f),
            new Vector3(18f, 0f, 20f),
            new Vector3(18f, 0f, 18f),
            new Vector3(18f, 0f, 16f),
            new Vector3(18f, 0f, -6f),
            new Vector3(18f, 0f, -8f),
            new Vector3(18f, 0f, -10f),
            new Vector3(18f, 0f, -12f),
            new Vector3(18f, 0f, -14f),
            new Vector3(18f, 0f, -16f),
            new Vector3(18f, 0f, -18f),
            new Vector3(18f, 0f, -42f),
            new Vector3(18f, 0f, -44f),
            new Vector3(16f, 0f, 46f),
            new Vector3(16f, 0f, 44f),
            new Vector3(16f, 0f, 42f),
            new Vector3(16f, 0f, 22f),
            new Vector3(16f, 0f, 20f),
            new Vector3(16f, 0f, 18f),
            new Vector3(16f, 0f, -2f),
            new Vector3(16f, 0f, -4f),
            new Vector3(16f, 0f, -6f),
            new Vector3(16f, 0f, -8f),
            new Vector3(16f, 0f, -10f),
            new Vector3(16f, 0f, -12f),
            new Vector3(16f, 0f, -14f),
            new Vector3(16f, 0f, -16f),
            new Vector3(16f, 0f, -42f),
            new Vector3(16f, 0f, -44f),
            new Vector3(16f, 0f, -46f),
            new Vector3(14f, 0f, 46f),
            new Vector3(14f, 0f, 44f),
            new Vector3(14f, 0f, 22f),
            new Vector3(14f, 0f, 20f),
            new Vector3(14f, 0f, -2f),
            new Vector3(14f, 0f, -4f),
            new Vector3(14f, 0f, -22f),
            new Vector3(14f, 0f, -24f),
            new Vector3(14f, 0f, -26f),
            new Vector3(14f, 0f, -28f),
            new Vector3(14f, 0f, -44f),
            new Vector3(14f, 0f, -46f),
            new Vector3(12f, 0f, 46f),
            new Vector3(12f, 0f, 44f),
            new Vector3(12f, 0f, 24f),
            new Vector3(12f, 0f, 22f),
            new Vector3(12f, 0f, 20f),
            new Vector3(12f, 0f, 18f),
            new Vector3(12f, 0f, 16f),
            new Vector3(12f, 0f, 14f),
            new Vector3(12f, 0f, 12f),
            new Vector3(12f, 0f, 10f),
            new Vector3(12f, 0f, 8f),
            new Vector3(12f, 0f, 6f),
            new Vector3(12f, 0f, 4f),
            new Vector3(12f, 0f, 2f),
            new Vector3(12f, 0f, 0f),
            new Vector3(12f, 0f, -2f),
            new Vector3(12f, 0f, -4f),
            new Vector3(12f, 0f, -6f),
            new Vector3(12f, 0f, -8f),
            new Vector3(12f, 0f, -10f),
            new Vector3(12f, 0f, -12f),
            new Vector3(12f, 0f, -14f),
            new Vector3(12f, 0f, -16f),
            new Vector3(12f, 0f, -18f),
            new Vector3(12f, 0f, -20f),
            new Vector3(12f, 0f, -22f),
            new Vector3(12f, 0f, -24f),
            new Vector3(12f, 0f, -26f),
            new Vector3(12f, 0f, -28f),
            new Vector3(12f, 0f, -44f),
            new Vector3(12f, 0f, -46f),
            new Vector3(10f, 0f, 46f),
            new Vector3(10f, 0f, 44f),
            new Vector3(10f, 0f, 26f),
            new Vector3(10f, 0f, 24f),
            new Vector3(10f, 0f, 22f),
            new Vector3(10f, 0f, 20f),
            new Vector3(10f, 0f, 18f),
            new Vector3(10f, 0f, 16f),
            new Vector3(10f, 0f, 14f),
            new Vector3(10f, 0f, 12f),
            new Vector3(10f, 0f, 10f),
            new Vector3(10f, 0f, 8f),
            new Vector3(10f, 0f, 6f),
            new Vector3(10f, 0f, 4f),
            new Vector3(10f, 0f, 2f),
            new Vector3(10f, 0f, 0f),
            new Vector3(10f, 0f, -2f),
            new Vector3(10f, 0f, -4f),
            new Vector3(10f, 0f, -6f),
            new Vector3(10f, 0f, -8f),
            new Vector3(10f, 0f, -10f),
            new Vector3(10f, 0f, -12f),
            new Vector3(10f, 0f, -14f),
            new Vector3(10f, 0f, -16f),
            new Vector3(10f, 0f, -18f),
            new Vector3(10f, 0f, -20f),
            new Vector3(10f, 0f, -22f),
            new Vector3(10f, 0f, -24f),
            new Vector3(10f, 0f, -26f),
            new Vector3(10f, 0f, -28f),
            new Vector3(10f, 0f, -44f),
            new Vector3(10f, 0f, -46f),
            new Vector3(8f, 0f, 48f),
            new Vector3(8f, 0f, 46f),
            new Vector3(8f, 0f, 44f),
            new Vector3(8f, 0f, 28f),
            new Vector3(8f, 0f, 26f),
            new Vector3(8f, 0f, -22f),
            new Vector3(8f, 0f, -24f),
            new Vector3(8f, 0f, -26f),
            new Vector3(8f, 0f, -28f),
            new Vector3(8f, 0f, -44f),
            new Vector3(8f, 0f, -46f),
            new Vector3(8f, 0f, -48f),
            new Vector3(6f, 0f, 48f),
            new Vector3(6f, 0f, 46f),
            new Vector3(6f, 0f, 28f),
            new Vector3(6f, 0f, 26f),
            new Vector3(6f, 0f, 22f),
            new Vector3(6f, 0f, 20f),
            new Vector3(6f, 0f, 18f),
            new Vector3(6f, 0f, 16f),
            new Vector3(6f, 0f, 14f),
            new Vector3(6f, 0f, 12f),
            new Vector3(6f, 0f, 10f),
            new Vector3(6f, 0f, 8f),
            new Vector3(6f, 0f, 6f),
            new Vector3(6f, 0f, -22f),
            new Vector3(6f, 0f, -24f),
            new Vector3(6f, 0f, -26f),
            new Vector3(6f, 0f, -28f),
            new Vector3(6f, 0f, -46f),
            new Vector3(6f, 0f, -48f),
            new Vector3(4f, 0f, 48f),
            new Vector3(4f, 0f, 46f),
            new Vector3(4f, 0f, 28f),
            new Vector3(4f, 0f, 26f),
            new Vector3(4f, 0f, 24f),
            new Vector3(4f, 0f, 22f),
            new Vector3(4f, 0f, 18f),
            new Vector3(4f, 0f, 16f),
            new Vector3(4f, 0f, 14f),
            new Vector3(4f, 0f, 12f),
            new Vector3(4f, 0f, 10f),
            new Vector3(4f, 0f, 8f),
            new Vector3(4f, 0f, 6f),
            new Vector3(4f, 0f, -22f),
            new Vector3(4f, 0f, -24f),
            new Vector3(4f, 0f, -26f),
            new Vector3(4f, 0f, -28f),
            new Vector3(4f, 0f, -46f),
            new Vector3(4f, 0f, -48f),
            new Vector3(2f, 0f, 48f),
            new Vector3(2f, 0f, 46f),
            new Vector3(2f, 0f, 28f),
            new Vector3(2f, 0f, 26f),
            new Vector3(2f, 0f, 24f),
            new Vector3(2f, 0f, 22f),
            new Vector3(2f, 0f, 8f),
            new Vector3(2f, 0f, 6f),
            new Vector3(2f, 0f, -22f),
            new Vector3(2f, 0f, -24f),
            new Vector3(2f, 0f, -26f),
            new Vector3(2f, 0f, -28f),
            new Vector3(2f, 0f, -46f),
            new Vector3(2f, 0f, -48f),
            new Vector3(0f, 0f, 48f),
            new Vector3(0f, 0f, 46f),
            new Vector3(0f, 0f, 28f),
            new Vector3(0f, 0f, 26f),
            new Vector3(0f, 0f, 24f),
            new Vector3(0f, 0f, 22f),
            new Vector3(0f, 0f, 8f),
            new Vector3(0f, 0f, 6f),
            new Vector3(0f, 0f, -22f),
            new Vector3(0f, 0f, -24f),
            new Vector3(0f, 0f, -26f),
            new Vector3(0f, 0f, -28f),
            new Vector3(0f, 0f, -46f),
            new Vector3(0f, 0f, -48f),
            new Vector3(-2f, 0f, 48f),
            new Vector3(-2f, 0f, 46f),
            new Vector3(-2f, 0f, 28f),
            new Vector3(-2f, 0f, 26f),
            new Vector3(-2f, 0f, 24f),
            new Vector3(-2f, 0f, 22f),
            new Vector3(-2f, 0f, 8f),
            new Vector3(-2f, 0f, 6f),
            new Vector3(-2f, 0f, -22f),
            new Vector3(-2f, 0f, -24f),
            new Vector3(-2f, 0f, -26f),
            new Vector3(-2f, 0f, -28f),
            new Vector3(-2f, 0f, -46f),
            new Vector3(-2f, 0f, -48f),
            new Vector3(-4f, 0f, 48f),
            new Vector3(-4f, 0f, 46f),
            new Vector3(-4f, 0f, 28f),
            new Vector3(-4f, 0f, 26f),
            new Vector3(-4f, 0f, 24f),
            new Vector3(-4f, 0f, 22f),
            new Vector3(-4f, 0f, 8f),
            new Vector3(-4f, 0f, 6f),
            new Vector3(-4f, 0f, -22f),
            new Vector3(-4f, 0f, -24f),
            new Vector3(-4f, 0f, -26f),
            new Vector3(-4f, 0f, -28f),
            new Vector3(-4f, 0f, -46f),
            new Vector3(-4f, 0f, -48f),
            new Vector3(-6f, 0f, 48f),
            new Vector3(-6f, 0f, 46f),
            new Vector3(-6f, 0f, 28f),
            new Vector3(-6f, 0f, 26f),
            new Vector3(-6f, 0f, 24f),
            new Vector3(-6f, 0f, 22f),
            new Vector3(-6f, 0f, 8f),
            new Vector3(-6f, 0f, 6f),
            new Vector3(-6f, 0f, -22f),
            new Vector3(-6f, 0f, -24f),
            new Vector3(-6f, 0f, -26f),
            new Vector3(-6f, 0f, -28f),
            new Vector3(-6f, 0f, -46f),
            new Vector3(-6f, 0f, -48f),
            new Vector3(-8f, 0f, 48f),
            new Vector3(-8f, 0f, 46f),
            new Vector3(-8f, 0f, 44f),
            new Vector3(-8f, 0f, 28f),
            new Vector3(-8f, 0f, 26f),
            new Vector3(-8f, 0f, 24f),
            new Vector3(-8f, 0f, 22f),
            new Vector3(-8f, 0f, 8f),
            new Vector3(-8f, 0f, 6f),
            new Vector3(-8f, 0f, -22f),
            new Vector3(-8f, 0f, -24f),
            new Vector3(-8f, 0f, -26f),
            new Vector3(-8f, 0f, -28f),
            new Vector3(-8f, 0f, -44f),
            new Vector3(-8f, 0f, -46f),
            new Vector3(-8f, 0f, -48f),
            new Vector3(-10f, 0f, 46f),
            new Vector3(-10f, 0f, 44f),
            new Vector3(-10f, 0f, 28f),
            new Vector3(-10f, 0f, 26f),
            new Vector3(-10f, 0f, 24f),
            new Vector3(-10f, 0f, 22f),
            new Vector3(-10f, 0f, 8f),
            new Vector3(-10f, 0f, 6f),
            new Vector3(-10f, 0f, -22f),
            new Vector3(-10f, 0f, -24f),
            new Vector3(-10f, 0f, -26f),
            new Vector3(-10f, 0f, -28f),
            new Vector3(-10f, 0f, -44f),
            new Vector3(-10f, 0f, -46f),
            new Vector3(-12f, 0f, 46f),
            new Vector3(-12f, 0f, 44f),
            new Vector3(-12f, 0f, 28f),
            new Vector3(-12f, 0f, 26f),
            new Vector3(-12f, 0f, 24f),
            new Vector3(-12f, 0f, 22f),
            new Vector3(-12f, 0f, 8f),
            new Vector3(-12f, 0f, 6f),
            new Vector3(-12f, 0f, -22f),
            new Vector3(-12f, 0f, -24f),
            new Vector3(-12f, 0f, -26f),
            new Vector3(-12f, 0f, -28f),
            new Vector3(-12f, 0f, -44f),
            new Vector3(-12f, 0f, -46f),
            new Vector3(-14f, 0f, 46f),
            new Vector3(-14f, 0f, 44f),
            new Vector3(-14f, 0f, 28f),
            new Vector3(-14f, 0f, 26f),
            new Vector3(-14f, 0f, 24f),
            new Vector3(-14f, 0f, 22f),
            new Vector3(-14f, 0f, 8f),
            new Vector3(-14f, 0f, 6f),
            new Vector3(-14f, 0f, -22f),
            new Vector3(-14f, 0f, -24f),
            new Vector3(-14f, 0f, -26f),
            new Vector3(-14f, 0f, -28f),
            new Vector3(-14f, 0f, -44f),
            new Vector3(-14f, 0f, -46f),
            new Vector3(-16f, 0f, 44f),
            new Vector3(-16f, 0f, 42f),
            new Vector3(-16f, 0f, 28f),
            new Vector3(-16f, 0f, 26f),
            new Vector3(-16f, 0f, 24f),
            new Vector3(-16f, 0f, 22f),
            new Vector3(-16f, 0f, 8f),
            new Vector3(-16f, 0f, 6f),
            new Vector3(-16f, 0f, -22f),
            new Vector3(-16f, 0f, -24f),
            new Vector3(-16f, 0f, -26f),
            new Vector3(-16f, 0f, -28f),
            new Vector3(-16f, 0f, -42f),
            new Vector3(-16f, 0f, -44f),
            new Vector3(-18f, 0f, 44f),
            new Vector3(-18f, 0f, 42f),
            new Vector3(-18f, 0f, 28f),
            new Vector3(-18f, 0f, 26f),
            new Vector3(-18f, 0f, 22f),
            new Vector3(-18f, 0f, 20f),
            new Vector3(-18f, 0f, 18f),
            new Vector3(-18f, 0f, 16f),
            new Vector3(-18f, 0f, 14f),
            new Vector3(-18f, 0f, 12f),
            new Vector3(-18f, 0f, 10f),
            new Vector3(-18f, 0f, 8f),
            new Vector3(-18f, 0f, 6f),
            new Vector3(-18f, 0f, -22f),
            new Vector3(-18f, 0f, -24f),
            new Vector3(-18f, 0f, -26f),
            new Vector3(-18f, 0f, -28f),
            new Vector3(-18f, 0f, -42f),
            new Vector3(-18f, 0f, -44f),
            new Vector3(-20f, 0f, 44f),
            new Vector3(-20f, 0f, 42f),
            new Vector3(-20f, 0f, 40f),
            new Vector3(-20f, 0f, 28f),
            new Vector3(-20f, 0f, 26f),
            new Vector3(-20f, 0f, -22f),
            new Vector3(-20f, 0f, -24f),
            new Vector3(-20f, 0f, -26f),
            new Vector3(-20f, 0f, -28f),
            new Vector3(-20f, 0f, -40f),
            new Vector3(-20f, 0f, -42f),
            new Vector3(-20f, 0f, -44f),
            new Vector3(-22f, 0f, 42f),
            new Vector3(-22f, 0f, 40f),
            new Vector3(-22f, 0f, 26f),
            new Vector3(-22f, 0f, 24f),
            new Vector3(-22f, 0f, 22f),
            new Vector3(-22f, 0f, 20f),
            new Vector3(-22f, 0f, 18f),
            new Vector3(-22f, 0f, 16f),
            new Vector3(-22f, 0f, 14f),
            new Vector3(-22f, 0f, 12f),
            new Vector3(-22f, 0f, 10f),
            new Vector3(-22f, 0f, 8f),
            new Vector3(-22f, 0f, 6f),
            new Vector3(-22f, 0f, 4f),
            new Vector3(-22f, 0f, 2f),
            new Vector3(-22f, 0f, 0f),
            new Vector3(-22f, 0f, -2f),
            new Vector3(-22f, 0f, -4f),
            new Vector3(-22f, 0f, -6f),
            new Vector3(-22f, 0f, -8f),
            new Vector3(-22f, 0f, -10f),
            new Vector3(-22f, 0f, -12f),
            new Vector3(-22f, 0f, -14f),
            new Vector3(-22f, 0f, -16f),
            new Vector3(-22f, 0f, -18f),
            new Vector3(-22f, 0f, -20f),
            new Vector3(-22f, 0f, -22f),
            new Vector3(-22f, 0f, -24f),
            new Vector3(-22f, 0f, -26f),
            new Vector3(-22f, 0f, -28f),
            new Vector3(-22f, 0f, -40f),
            new Vector3(-22f, 0f, -42f),
            new Vector3(-24f, 0f, 42f),
            new Vector3(-24f, 0f, 40f),
            new Vector3(-24f, 0f, 38f),
            new Vector3(-24f, 0f, 24f),
            new Vector3(-24f, 0f, 22f),
            new Vector3(-24f, 0f, 20f),
            new Vector3(-24f, 0f, 18f),
            new Vector3(-24f, 0f, 16f),
            new Vector3(-24f, 0f, 14f),
            new Vector3(-24f, 0f, 12f),
            new Vector3(-24f, 0f, 10f),
            new Vector3(-24f, 0f, 8f),
            new Vector3(-24f, 0f, 6f),
            new Vector3(-24f, 0f, 4f),
            new Vector3(-24f, 0f, 2f),
            new Vector3(-24f, 0f, 0f),
            new Vector3(-24f, 0f, -2f),
            new Vector3(-24f, 0f, -4f),
            new Vector3(-24f, 0f, -6f),
            new Vector3(-24f, 0f, -8f),
            new Vector3(-24f, 0f, -10f),
            new Vector3(-24f, 0f, -12f),
            new Vector3(-24f, 0f, -14f),
            new Vector3(-24f, 0f, -16f),
            new Vector3(-24f, 0f, -18f),
            new Vector3(-24f, 0f, -20f),
            new Vector3(-24f, 0f, -22f),
            new Vector3(-24f, 0f, -24f),
            new Vector3(-24f, 0f, -26f),
            new Vector3(-24f, 0f, -28f),
            new Vector3(-24f, 0f, -38f),
            new Vector3(-24f, 0f, -40f),
            new Vector3(-24f, 0f, -42f),
            new Vector3(-26f, 0f, 40f),
            new Vector3(-26f, 0f, 38f),
            new Vector3(-26f, 0f, -22f),
            new Vector3(-26f, 0f, -24f),
            new Vector3(-26f, 0f, -26f),
            new Vector3(-26f, 0f, -28f),
            new Vector3(-26f, 0f, -38f),
            new Vector3(-26f, 0f, -40f),
            new Vector3(-28f, 0f, 38f),
            new Vector3(-28f, 0f, 36f),
            new Vector3(-28f, 0f, -36f),
            new Vector3(-28f, 0f, -38f),
            new Vector3(-30f, 0f, 38f),
            new Vector3(-30f, 0f, 36f),
            new Vector3(-30f, 0f, 34f),
            new Vector3(-30f, 0f, -34f),
            new Vector3(-30f, 0f, -36f),
            new Vector3(-30f, 0f, -38f),
            new Vector3(-32f, 0f, 36f),
            new Vector3(-32f, 0f, 34f),
            new Vector3(-32f, 0f, 32f),
            new Vector3(-32f, 0f, -32f),
            new Vector3(-32f, 0f, -34f),
            new Vector3(-32f, 0f, -36f),
            new Vector3(-34f, 0f, 34f),
            new Vector3(-34f, 0f, 32f),
            new Vector3(-34f, 0f, 30f),
            new Vector3(-34f, 0f, -30f),
            new Vector3(-34f, 0f, -32f),
            new Vector3(-34f, 0f, -34f),
            new Vector3(-36f, 0f, 32f),
            new Vector3(-36f, 0f, 30f),
            new Vector3(-36f, 0f, 28f),
            new Vector3(-36f, 0f, -28f),
            new Vector3(-36f, 0f, -30f),
            new Vector3(-36f, 0f, -32f),
            new Vector3(-38f, 0f, 30f),
            new Vector3(-38f, 0f, 28f),
            new Vector3(-38f, 0f, 26f),
            new Vector3(-38f, 0f, 24f),
            new Vector3(-38f, 0f, -24f),
            new Vector3(-38f, 0f, -26f),
            new Vector3(-38f, 0f, -28f),
            new Vector3(-40f, 0f, 26f),
            new Vector3(-40f, 0f, 24f),
            new Vector3(-40f, 0f, 22f),
            new Vector3(-40f, 0f, 20f),
            new Vector3(-40f, 0f, -20f),
            new Vector3(-40f, 0f, -22f),
            new Vector3(-40f, 0f, -24f),
            new Vector3(-40f, 0f, -26f),
            new Vector3(-42f, 0f, 24f),
            new Vector3(-42f, 0f, 22f),
            new Vector3(-42f, 0f, 20f),
            new Vector3(-42f, 0f, 18f),
            new Vector3(-42f, 0f, 16f),
            new Vector3(-42f, 0f, -16f),
            new Vector3(-42f, 0f, -18f),
            new Vector3(-42f, 0f, -20f),
            new Vector3(-42f, 0f, -22f),
            new Vector3(-42f, 0f, -24f),
            new Vector3(-44f, 0f, 20f),
            new Vector3(-44f, 0f, 18f),
            new Vector3(-44f, 0f, 16f),
            new Vector3(-44f, 0f, 14f),
            new Vector3(-44f, 0f, 12f),
            new Vector3(-44f, 0f, 10f),
            new Vector3(-44f, 0f, 8f),
            new Vector3(-44f, 0f, -8f),
            new Vector3(-44f, 0f, -10f),
            new Vector3(-44f, 0f, -12f),
            new Vector3(-44f, 0f, -14f),
            new Vector3(-44f, 0f, -16f),
            new Vector3(-44f, 0f, -18f),
            new Vector3(-44f, 0f, -20f),
            new Vector3(-46f, 0f, 14f),
            new Vector3(-46f, 0f, 12f),
            new Vector3(-46f, 0f, 10f),
            new Vector3(-46f, 0f, 8f),
            new Vector3(-46f, 0f, 6f),
            new Vector3(-46f, 0f, 4f),
            new Vector3(-46f, 0f, 2f),
            new Vector3(-46f, 0f, 0f),
            new Vector3(-46f, 0f, -2f),
            new Vector3(-46f, 0f, -4f),
            new Vector3(-46f, 0f, -6f),
            new Vector3(-46f, 0f, -8f),
            new Vector3(-46f, 0f, -10f),
            new Vector3(-46f, 0f, -12f),
            new Vector3(-46f, 0f, -14f),
            new Vector3(-48f, 0f, 6)
        };

        private ControllerGasStationEvent Controller { get; set; } = null;
        private bool Active { get; set; } = false;

        private void StartTimer()
        {
            if (!_config.EnabledTimer) return;
            timer.In(UnityEngine.Random.Range(_config.MinStartTime, _config.MaxStartTime), () =>
            {
                if (!Active) Start(null);
                else Puts("This event is active now. To finish this event (gsstop), then to start the next one");
            });
        }

        private void Start(BasePlayer player)
        {
            if (!PluginExistsForStart("NpcSpawn")) return;
            CheckVersionPlugin();
            Active = true;
            AlertToAllPlayers("PreStart", _config.Chat.Prefix, GetTimeFormat((int)_config.PreStartTime));
            timer.In(_config.PreStartTime, () =>
            {
                Puts($"{Name} has begun");
                if (_config.RemoveBetterNpc && plugins.Exists("BetterNpc")) BetterNpc.Call("DestroyController", "Oxum's Gas Station");
                ToggleHooks(true);
                Controller = new GameObject().AddComponent<ControllerGasStationEvent>();
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
            if (_config.RemoveBetterNpc && plugins.Exists("BetterNpc")) BetterNpc.Call("CreateController", "Oxum's Gas Station");
            Puts($"{Name} has ended");
            StartTimer();
        }

        internal class ControllerGasStationEvent : FacepunchBehaviour
        {
            private static PluginConfig _config => _ins._config;

            internal MonumentInfo Monument { get; set; } = null;

            private SphereCollider SphereCollider { get; set; } = null;

            private VendingMachineMapMarker VendingMarker { get; set; } = null;
            private HashSet<MapMarkerGenericRadius> Markers { get; } = new HashSet<MapMarkerGenericRadius>();

            internal Door FirstDoor { get; set; } = null;
            internal CodeLock FirstCodeLock { get; set; } = null;
            internal string FirstPassword { get; set; } = $"{GetRandomNumber}{GetRandomNumber}{GetRandomNumber}{GetRandomNumber}";
            private static int GetRandomNumber => UnityEngine.Random.Range(0, 10);
            internal BasicCar FirstCar { get; set; } = null;
            internal bool Arrived1 { get; set; } = false;
            private HashSet<FireBall> FireBalls { get; } = new HashSet<FireBall>();
            internal HashSet<ScientistNPC> FirstScientists { get; } = new HashSet<ScientistNPC>();
            internal HashSet<BasePlayer> FirstAttackPlayers { get; } = new HashSet<BasePlayer>();

            internal Door SecondDoor { get; set; } = null;
            private CodeLock SecondCodeLock { get; set; } = null;
            internal BuildingBlock Doorway { get; set; } = null;
            internal GameObject SecondDoorSphere { get; } = new GameObject();
            internal ModularCar SecondCar { get; set; } = null;
            internal bool Arrived2 { get; set; } = false;
            internal HashSet<ScientistNPC> SecondScientists { get; } = new HashSet<ScientistNPC>();
            internal HashSet<BasePlayer> SecondAttackPlayers { get; } = new HashSet<BasePlayer>();

            internal List<Vector3> PathFirstCar { get; } = new List<Vector3>();
            internal List<Vector3> PathSecondCar { get; } = new List<Vector3>();

            internal bool IsArrived => Arrived1 && Arrived2;

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

                CheckDefaultCrates();

                SpawnEntities();

                FindPaths();
                SpawnFirstCar();
                Invoke(SpawnSecondCar, 3f);
                Invoke(CheckFinishCars, 30f);

                SpawnMapMarker(_config.Marker);

                Invoke(() => { foreach (BasePlayer player in Players) TryTeleportPlayer(player); }, 1f);
                InvokeRepeating(InvokeUpdates, 0f, 1f);
            }

            private void OnDestroy()
            {
                CancelInvoke(InvokeUpdates);
                CancelInvoke(CheckFinishCars);
                CancelInvoke(SpawnSecondCar);

                if (SphereCollider != null) Destroy(SphereCollider);

                if (VendingMarker.IsExists()) VendingMarker.Kill();
                foreach (MapMarkerGenericRadius marker in Markers) if (marker.IsExists()) marker.Kill();

                foreach (BasePlayer player in Players) CuiHelper.DestroyUi(player, "Tabs_KpucTaJl");

                foreach (ScientistNPC npc in FirstScientists)
                {
                    if (npc.IsExists())
                    {
                        npc.EnsureDismounted();
                        npc.Kill();
                    }
                }

                foreach (ScientistNPC npc in SecondScientists)
                {
                    if (npc.IsExists())
                    {
                        npc.EnsureDismounted();
                        npc.Kill();
                    }
                }

                foreach (KeyValuePair<LootContainer, int> dic in Crates) if (dic.Key.IsExists()) dic.Key.Kill();

                if (FirstCodeLock.IsExists()) FirstCodeLock.Kill();
                if (SecondDoorSphere != null) Destroy(SecondDoorSphere);
                if (SecondCodeLock.IsExists()) SecondCodeLock.Kill();
                if (SecondDoor.IsExists()) SecondDoor.Kill();
                if (Doorway.IsExists()) Doorway.Kill();
                foreach (FireBall ent in FireBalls) if (ent.IsExists()) ent.Kill();

                if (FirstCar.IsExists()) FirstCar.Kill();
                if (SecondCar.IsExists() && (!IsArrived || Vector3.Distance(SecondCar.transform.position, GetGlobalPosition(new Vector3(27f, 3f, 7f))) < 10f))
                {
                    foreach (BaseEntity entity in SecondCar.children)
                    {
                        if (entity.ShortPrefabName == "modular_car_fuel_storage") (entity as StorageContainer).inventory.ClearItemsContainer();
                        else if (entity.ShortPrefabName == "1module_cockpit_with_engine")
                        {
                            foreach (BaseEntity ent in entity.children)
                                if (ent is Rust.Modular.EngineStorage)
                                    (ent as Rust.Modular.EngineStorage).inventory.ClearItemsContainer();
                        }
                    }
                    SecondCar.Kill();
                }
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
                Dictionary<string, string> dic = new Dictionary<string, string> { ["Clock_KpucTaJl"] = GetTimeFormat(TimeToFinish) };
                if (IsArrived)
                {
                    if (FirstScientists.Count > 0) dic.Add("FirstNpc_KpucTaJl", FirstScientists.Count.ToString());
                    if (SecondScientists.Count > 0) dic.Add("SecondNpc_KpucTaJl", SecondScientists.Count.ToString());
                    if (FirstCodeLock.IsExists() && !_config.CanOpenFirstDoor) dic.Add("Password_KpucTaJl", SecondScientists.Count == 0 ? FirstPassword : "****");
                    if (Crates.Count > 0) dic.Add("Crate_KpucTaJl", Crates.Count.ToString());
                }
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
                VendingMarker.markerShopName = $"{_config.Marker.Text}\n{GetTimeFormat(TimeToFinish)}";
                if (_ins.ActivePveMode) VendingMarker.markerShopName += Owner == null ? "\nNo Owner" : $"\n{Owner.displayName}";
                VendingMarker.SendNetworkUpdate();
            }

            internal void UpdateMapMarkers() { foreach (MapMarkerGenericRadius marker in Markers) marker.SendUpdate(); }

            private void UpdateMarkerForPlayers()
            {
                if (!IsArrived || Players.Count == 0) return;
                if (_config.MainPoint.Enabled)
                {
                    HashSet<Vector3> points = new HashSet<Vector3>();
                    if (FirstScientists.Count == 0 && !SecondDoor.IsOpen()) points.Add(SecondDoor.transform.position);
                    if (SecondScientists.Count == 0 && FirstCodeLock.IsExists()) points.Add(FirstCodeLock.transform.position);
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
                if (IsArrived && FirstScientists.Count == 0 && SecondScientists.Count == 0 && Crates.Count == 0 && TimeToFinish > _config.PreFinishTime) TimeToFinish = _config.PreFinishTime;
                else TimeToFinish--;
                if (TimeToFinish == _config.PreFinishTime) _ins.AlertToAllPlayers("PreFinish", _config.Chat.Prefix, GetTimeFormat(_config.PreFinishTime));
                else if (TimeToFinish == 0)
                {
                    CancelInvoke(InvokeUpdates);
                    _ins.Finish();
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
                ClearAllCodeLocks();

                FirstDoor = GetNearEntity<Door>(GetGlobalPosition(new Vector3(-10.5f, 3.16f, 18f)), 1f, 1 << 21);
                if (FirstDoor == null)
                {
                    FirstDoor = GameManager.server.CreateEntity("assets/bundled/prefabs/static/door.hinged.industrial_a_c.prefab", GetGlobalPosition(new Vector3(-10.5f, 3.16f, 18f))) as Door;
                    FirstDoor.enableSaving = true;
                    FirstDoor.Spawn();
                }
                FirstDoor.SetOpen(false);
                FirstCodeLock = SetCodeLock(FirstDoor, FirstPassword);

                Doorway = GameManager.server.CreateEntity("assets/prefabs/building core/wall.doorway/wall.doorway.prefab", GetGlobalPosition(new Vector3(13.545f, -4.583f, 20.259f)), GetGlobalRotation(new Vector3(0f, 90f, 0f))) as BuildingBlock;
                Doorway.enableSaving = false;
                Doorway.Spawn();
                Doorway.grounded = true;
                Doorway.SetGrade(BuildingGrade.Enum.TopTier);
                Doorway.SetHealthToMax();

                SecondDoor = GameManager.server.CreateEntity("assets/bundled/prefabs/modding/asset_store/bankheist_package/bankheist_vol03/prefabs/door.vault.static.prefab", GetGlobalPosition(new Vector3(12.448f, -2.24f, 19.957f)), GetGlobalRotation(new Vector3(90f, 90f, 0f))) as Door;
                SecondDoor.enableSaving = false;
                SecondDoor.Spawn();
                SecondCodeLock = SetCodeLock(SecondDoor, $"{GetRandomNumber}{GetRandomNumber}{GetRandomNumber}{GetRandomNumber}");
                SecondDoorSphere.transform.position = GetGlobalPosition(new Vector3(13.373f, -3.99f, 22.929f));
            }

            internal void SpawnCrates(HashSet<CrateConfig> crates)
            {
                foreach (CrateConfig crateConfig in crates)
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
                }
                if (_ins.ActivePveMode) _ins.PveMode.Call("EventAddCrates", _ins.Name, Crates.Select(x => x.Key.net.ID.Value));
            }

            internal void ClearAllCodeLocks()
            {
                List<CodeLock> list = Pool.Get<List<CodeLock>>();
                Vis.Entities<CodeLock>(GetGlobalPosition(new Vector3(-10.5f, 3.16f, 18f)), 3f, list, 1 << 21);
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    CodeLock codelock = list[i];
                    if (!codelock.IsExists()) continue;
                    if (codelock == FirstCodeLock) continue;
                    codelock.Kill();
                }
                Pool.FreeUnmanaged(ref list);
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
                if (codeLock != FirstCodeLock || code != FirstPassword) return;
                if (_ins.ActivePveMode && _ins.PveMode.Call("CanActionEvent", _ins.Name, target) != null) return;
                _ins.ActionEconomy(target.userID, "OpenFirstDoor");
                FirstDoor.SetOpen(true);
                _ins.NextTick(() => { if (codeLock.IsExists()) codeLock.Kill(); });
            }

            internal void OpenSecondDoor()
            {
                Destroy(SecondDoorSphere);
                SecondDoor.SetOpen(true);
            }

            private void FindPaths()
            {
                Vector3 penultimate1 = GetGlobalPosition(new Vector3(15.5f, 3f, -14f));
                Vector3 penultimate2 = GetGlobalPosition(new Vector3(22f, 3f, -10f));

                Dictionary<Vector3, string> all = new Dictionary<Vector3, string>();
                foreach (PathList path in TerrainMeta.Path.Roads)
                {
                    if (path.Width < 5f) continue;
                    foreach (Vector3 vector3 in path.Path.Points)
                    {
                        if (Vector3.Distance(penultimate1, vector3) > 100f || Vector3.Dot(transform.right, (vector3 - penultimate1).normalized) < 0.6f) continue;
                        if (!all.Any(x => x.Key.IsEqualVector3(vector3))) all.Add(vector3, path.Name);
                    }
                }

                if (all.Count > 0)
                {
                    KeyValuePair<Vector3, string> dic = all.Min(x => Vector3.Distance(penultimate1, x.Key));
                    Vector3 pos1 = dic.Key;

                    string roadName = dic.Value;

                    all.Remove(dic.Key);
                    all = all.Where(x => x.Value == roadName);

                    dic = all.Min(x => Vector3.Distance(penultimate1, x.Key));
                    Vector3 pos2 = dic.Key;

                    PathList pathList = TerrainMeta.Path.Roads.FirstOrDefault(x => x.Name == roadName);

                    int index1 = Array.FindIndex(pathList.Path.Points, x => x.IsEqualVector3(pos1));
                    int index2 = Array.FindIndex(pathList.Path.Points, x => x.IsEqualVector3(pos2));

                    if (index2 < index1)
                    {
                        for (int i = 0; i <= index1; i++)
                        {
                            Vector3 pos = pathList.Path.Points[i];
                            if (Vector3.Distance(pos, penultimate1) < 100f) PathFirstCar.Add(pos);
                            else if (PathFirstCar.Count > 0) PathFirstCar.Clear();
                        }
                    }
                    else
                    {
                        for (int i = pathList.Path.Points.Length - 1; i >= index1; i--)
                        {
                            Vector3 pos = pathList.Path.Points[i];
                            if (Vector3.Distance(pos, penultimate1) < 100f) PathFirstCar.Add(pos);
                            else if (PathFirstCar.Count > 0) PathFirstCar.Clear();
                        }
                    }

                    foreach (Vector3 pos in PathFirstCar) PathSecondCar.Add(pos);

                    if (PathSecondCar.Count > 0)
                    {
                        Vector3 vector31 = PathSecondCar[PathSecondCar.Count - 1];
                        Vector3 vector32 = PathSecondCar.Count == 1 ? Vector3.zero : PathSecondCar[PathSecondCar.Count - 2];
                        if (Vector3.Dot(transform.right, (vector31 - penultimate2).normalized) < 0.6f) PathSecondCar.Remove(vector31);
                        if (vector32 != Vector3.zero && Vector3.Dot(transform.right, (vector32 - penultimate2).normalized) < 0.6f) PathSecondCar.Remove(vector32);
                    }
                }

                PathFirstCar.Add(penultimate1);
                PathFirstCar.Add(GetGlobalPosition(new Vector3(4.5f, 3.25f, -5f)));

                PathSecondCar.Add(penultimate2);
                PathSecondCar.Add(GetGlobalPosition(new Vector3(27f, 3f, 7f)));
            }

            private void SpawnFirstCar()
            {
                FirstCar = GameManager.server.CreateEntity("assets/content/vehicles/sedan_a/sedantest.entity.prefab", PathFirstCar[0]) as BasicCar;
                FirstCar.enableSaving = false;
                FirstCar.Spawn();
                FirstCar.transform.LookAt(PathFirstCar[1]);

                FirstCar.rigidBody.detectCollisions = false;

                JObject config = new JObject
                {
                    ["Name"] = _config.FirstNpc.Config.Name,
                    ["WearItems"] = new JArray { _config.FirstNpc.Config.WearItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["SkinID"] = x.SkinID }) },
                    ["BeltItems"] = new JArray { _config.FirstNpc.Config.BeltItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["Amount"] = x.Amount, ["SkinID"] = x.SkinID, ["Mods"] = new JArray { x.Mods }, ["Ammo"] = x.Ammo }) },
                    ["Kit"] = _config.FirstNpc.Config.Kit,
                    ["Health"] = _config.FirstNpc.Config.Health,
                    ["RoamRange"] = _config.FirstNpc.Config.RoamRange,
                    ["ChaseRange"] = _config.FirstNpc.Config.ChaseRange,
                    ["SenseRange"] = _config.FirstNpc.Config.SenseRange,
                    ["ListenRange"] = _config.FirstNpc.Config.SenseRange / 2f,
                    ["AttackRangeMultiplier"] = _config.FirstNpc.Config.AttackRangeMultiplier,
                    ["CheckVisionCone"] = _config.FirstNpc.Config.CheckVisionCone,
                    ["VisionCone"] = _config.FirstNpc.Config.VisionCone,
                    ["HostileTargetsOnly"] = false,
                    ["DamageScale"] = _config.FirstNpc.Config.DamageScale,
                    ["TurretDamageScale"] = 1f,
                    ["AimConeScale"] = _config.FirstNpc.Config.AimConeScale,
                    ["DisableRadio"] = _config.FirstNpc.Config.DisableRadio,
                    ["CanRunAwayWater"] = true,
                    ["CanSleep"] = false,
                    ["SleepDistance"] = 100f,
                    ["Speed"] = _config.FirstNpc.Config.Speed,
                    ["AreaMask"] = 1,
                    ["AgentTypeID"] = -1372625422,
                    ["HomePosition"] = string.Empty,
                    ["MemoryDuration"] = _config.FirstNpc.Config.MemoryDuration,
                    ["States"] = new JArray { "IdleState", "CombatStationaryState" }
                };

                foreach (BaseEntity ent in FirstCar.children)
                {
                    if (ent is BaseVehicleSeat)
                    {
                        BaseVehicleSeat seat = ent as BaseVehicleSeat;
                        ScientistNPC npc = (ScientistNPC)_ins.NpcSpawn.Call("SpawnNpc", FirstCar.transform.position, config);
                        if (npc.NavAgent.enabled)
                        {
                            npc.NavAgent.destination = npc.transform.position;
                            npc.NavAgent.isStopped = true;
                            npc.NavAgent.enabled = false;
                        }
                        seat.AttemptMount(npc, false);
                        FirstScientists.Add(npc);
                    }
                }

                FirstCar.gameObject.AddComponent<ControllerFirstCar>().Path = PathFirstCar;
            }

            internal void FinishPathFirstCar(ControllerFirstCar controller)
            {
                Destroy(controller);

                foreach (ScientistNPC npc in FirstScientists)
                {
                    if (npc.IsExists())
                    {
                        npc.EnsureDismounted();
                        npc.Kill();
                    }
                }
                FirstScientists.Clear();

                FirstCar.rigidBody.detectCollisions = true;

                Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/heli_explosion.prefab", FirstCar.transform.position, Vector3.up, null, true);
                Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/component_damged.prefab", FirstCar.transform.position, Vector3.up, null, true);

                SpawnFireBall(Vector3.zero, FirstCar);
                SpawnFireBall(GetGlobalPosition(new Vector3(3.234f, 3.25f, -5.045f)));
                SpawnFireBall(GetGlobalPosition(new Vector3(5.763f, 3.25f, -5.045f)));

                SpawnPreset(_config.FirstNpc, true);

                if (_ins.ActivePveMode) _ins.PveMode.Call("EventAddScientists", _ins.Name, FirstScientists.Select(x => x.net.ID.Value));

                Arrived1 = true;

                if (Arrived2)
                {
                    foreach (ScientistNPC first in FirstScientists)
                    {
                        foreach (ScientistNPC second in SecondScientists)
                        {
                            second.Brain.Senses.Memory.All.Add(new SimpleAIMemory.SeenInfo { Entity = first, Position = first.transform.position, Timestamp = Time.realtimeSinceStartup + TimeToFinish });
                            first.Brain.Senses.Memory.All.Add(new SimpleAIMemory.SeenInfo { Entity = second, Position = second.transform.position, Timestamp = Time.realtimeSinceStartup + TimeToFinish });
                        }
                    }
                }
            }

            private void SpawnSecondCar()
            {
                SecondCar = GameManager.server.CreateEntity("assets/content/vehicles/modularcar/3module_car_spawned.entity.prefab", PathSecondCar[0]) as ModularCar;
                SecondCar.enableSaving = false;
                SecondCar.spawnSettings.useSpawnSettings = false;
                SecondCar.Spawn();
                SecondCar.transform.LookAt(PathSecondCar[1]);

                SecondCar.rigidBody.detectCollisions = false;

                foreach (string shortname in new HashSet<string> { "vehicle.1mod.cockpit.with.engine", "vehicle.1mod.rear.seats", "vehicle.1mod.passengers.armored" })
                {
                    Item moduleItem = ItemManager.CreateByName(shortname);
                    if (!SecondCar.TryAddModule(moduleItem)) moduleItem.Remove();
                }

                JObject config = new JObject
                {
                    ["Name"] = _config.SecondNpc.Config.Name,
                    ["WearItems"] = new JArray { _config.SecondNpc.Config.WearItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["SkinID"] = x.SkinID }) },
                    ["BeltItems"] = new JArray { _config.SecondNpc.Config.BeltItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["Amount"] = x.Amount, ["SkinID"] = x.SkinID, ["Mods"] = new JArray { x.Mods }, ["Ammo"] = x.Ammo }) },
                    ["Kit"] = _config.SecondNpc.Config.Kit,
                    ["Health"] = _config.SecondNpc.Config.Health,
                    ["RoamRange"] = _config.SecondNpc.Config.RoamRange,
                    ["ChaseRange"] = _config.SecondNpc.Config.ChaseRange,
                    ["SenseRange"] = _config.SecondNpc.Config.SenseRange,
                    ["ListenRange"] = _config.SecondNpc.Config.SenseRange / 2f,
                    ["AttackRangeMultiplier"] = _config.SecondNpc.Config.AttackRangeMultiplier,
                    ["CheckVisionCone"] = _config.SecondNpc.Config.CheckVisionCone,
                    ["VisionCone"] = _config.SecondNpc.Config.VisionCone,
                    ["HostileTargetsOnly"] = false,
                    ["DamageScale"] = _config.SecondNpc.Config.DamageScale,
                    ["TurretDamageScale"] = 1f,
                    ["AimConeScale"] = _config.SecondNpc.Config.AimConeScale,
                    ["DisableRadio"] = _config.SecondNpc.Config.DisableRadio,
                    ["CanRunAwayWater"] = true,
                    ["CanSleep"] = false,
                    ["SleepDistance"] = 100f,
                    ["Speed"] = _config.SecondNpc.Config.Speed,
                    ["AreaMask"] = 1,
                    ["AgentTypeID"] = -1372625422,
                    ["HomePosition"] = string.Empty,
                    ["MemoryDuration"] = _config.SecondNpc.Config.MemoryDuration,
                    ["States"] = new JArray { "IdleState", "CombatStationaryState" }
                };

                foreach (BaseEntity entity in SecondCar.children)
                {
                    if (entity.ShortPrefabName == "modular_car_fuel_storage")
                    {
                        StorageContainer container = entity as StorageContainer;
                        container.SetFlag(BaseEntity.Flags.Locked, true);
                        ItemManager.CreateByName("lowgradefuel", 50).MoveToContainer(container.inventory);
                    }
                    else if (entity.ShortPrefabName == "1module_cockpit_with_engine" || entity.ShortPrefabName == "1module_rear_seats" || entity.ShortPrefabName == "1module_passengers_armored")
                    {
                        foreach (BaseEntity ent in entity.children)
                        {
                            if (ent is Rust.Modular.EngineStorage)
                            {
                                Rust.Modular.EngineStorage container = ent as Rust.Modular.EngineStorage;
                                container.SetFlag(BaseEntity.Flags.Locked, true);
                                ItemManager.CreateByName("carburetor3").MoveToContainer(container.inventory);
                                ItemManager.CreateByName("crankshaft3").MoveToContainer(container.inventory);
                                ItemManager.CreateByName("piston3").MoveToContainer(container.inventory);
                                ItemManager.CreateByName("sparkplug3").MoveToContainer(container.inventory);
                                ItemManager.CreateByName("valve3").MoveToContainer(container.inventory);
                                container.accelerationBoostPercent *= 4;
                                container.topSpeedBoostPercent *= 4;
                            }
                            else if (ent is ModularCarSeat)
                            {
                                ModularCarSeat seat = ent as ModularCarSeat;
                                ScientistNPC npc = (ScientistNPC)_ins.NpcSpawn.Call("SpawnNpc", SecondCar.transform.position, config);
                                if (npc.NavAgent.enabled)
                                {
                                    npc.NavAgent.destination = npc.transform.position;
                                    npc.NavAgent.isStopped = true;
                                    npc.NavAgent.enabled = false;
                                }
                                seat.AttemptMount(npc, false);
                                SecondScientists.Add(npc);
                            }
                        }
                    }
                }

                SecondCar.gameObject.AddComponent<ControllerSecondCar>().Path = PathSecondCar;
            }

            internal void FinishPathSecondCar(ControllerSecondCar controller)
            {
                Destroy(controller);

                SecondCar.DismountAllPlayers();
                foreach (ScientistNPC npc in SecondScientists)
                {
                    if (npc.IsExists())
                    {
                        npc.EnsureDismounted();
                        npc.Kill();
                    }
                }
                SecondScientists.Clear();

                SecondCar.rigidBody.detectCollisions = true;

                foreach (BaseEntity entity in SecondCar.children)
                {
                    if (entity.ShortPrefabName == "modular_car_fuel_storage") (entity as StorageContainer).SetFlag(BaseEntity.Flags.Locked, false);
                    else if (entity.ShortPrefabName == "1module_cockpit_with_engine")
                    {
                        foreach (BaseEntity ent in entity.children)
                        {
                            if (ent is Rust.Modular.EngineStorage)
                            {
                                Rust.Modular.EngineStorage container = ent as Rust.Modular.EngineStorage;
                                container.accelerationBoostPercent /= 4;
                                container.topSpeedBoostPercent /= 4;
                                container.inventory.ClearItemsContainer();
                                container.SetFlag(BaseEntity.Flags.Locked, false);
                                ItemManager.CreateByName($"carburetor{_config.CarComponentLevel}").MoveToContainer(container.inventory);
                                ItemManager.CreateByName($"crankshaft{_config.CarComponentLevel}").MoveToContainer(container.inventory);
                                ItemManager.CreateByName($"piston{_config.CarComponentLevel}").MoveToContainer(container.inventory);
                                ItemManager.CreateByName($"sparkplug{_config.CarComponentLevel}").MoveToContainer(container.inventory);
                                ItemManager.CreateByName($"valve{_config.CarComponentLevel}").MoveToContainer(container.inventory);
                            }
                        }
                    }
                }

                SecondCar.engineController.StopEngine();
                SecondCar.rigidBody.velocity = Vector3.zero;

                SpawnPreset(_config.SecondNpc, false);

                if (_ins.ActivePveMode) _ins.PveMode.Call("EventAddScientists", _ins.Name, SecondScientists.Select(x => x.net.ID.Value));

                Arrived2 = true;

                if (Arrived1)
                {
                    foreach (ScientistNPC first in FirstScientists)
                    {
                        foreach (ScientistNPC second in SecondScientists)
                        {
                            second.Brain.Senses.Memory.All.Add(new SimpleAIMemory.SeenInfo { Entity = first, Position = first.transform.position, Timestamp = Time.realtimeSinceStartup + TimeToFinish });
                            first.Brain.Senses.Memory.All.Add(new SimpleAIMemory.SeenInfo { Entity = second, Position = second.transform.position, Timestamp = Time.realtimeSinceStartup + TimeToFinish });
                        }
                    }
                }
            }

            private void CheckFinishCars()
            {
                if (!Arrived1)
                {
                    foreach (ScientistNPC npc in SecondScientists)
                    {
                        if (npc.IsExists())
                        {
                            npc.EnsureDismounted();
                            npc.Kill();
                        }
                    }
                    SecondScientists.Clear();

                    if (FirstCar.IsExists()) FirstCar.Kill();

                    FirstCar = GameManager.server.CreateEntity("assets/content/vehicles/sedan_a/sedantest.entity.prefab", GetGlobalPosition(new Vector3(4.5f, 3f, -9f))) as BasicCar;
                    FirstCar.enableSaving = false;
                    FirstCar.Spawn();

                    Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/heli_explosion.prefab", FirstCar.transform.position, Vector3.up, null, true);
                    Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/component_damged.prefab", FirstCar.transform.position, Vector3.up, null, true);

                    SpawnFireBall(Vector3.zero, FirstCar);
                    SpawnFireBall(GetGlobalPosition(new Vector3(3.234f, 3.25f, -5.045f)));
                    SpawnFireBall(GetGlobalPosition(new Vector3(5.763f, 3.25f, -5.045f)));

                    SpawnPreset(_config.FirstNpc, true);

                    if (_ins.ActivePveMode) _ins.PveMode.Call("EventAddScientists", _ins.Name, FirstScientists.Select(x => x.net.ID.Value));

                    Arrived1 = true;

                    if (Arrived2)
                    {
                        foreach (ScientistNPC first in FirstScientists)
                        {
                            foreach (ScientistNPC second in SecondScientists)
                            {
                                second.Brain.Senses.Memory.All.Add(new SimpleAIMemory.SeenInfo { Entity = first, Position = first.transform.position, Timestamp = Time.realtimeSinceStartup + TimeToFinish });
                                first.Brain.Senses.Memory.All.Add(new SimpleAIMemory.SeenInfo { Entity = second, Position = second.transform.position, Timestamp = Time.realtimeSinceStartup + TimeToFinish });
                            }
                        }
                    }
                }
                if (!Arrived2)
                {
                    foreach (ScientistNPC npc in SecondScientists)
                    {
                        if (npc.IsExists())
                        {
                            npc.EnsureDismounted();
                            npc.Kill();
                        }
                    }
                    SecondScientists.Clear();
                    SecondCar.DismountAllPlayers();

                    if (SecondCar.IsExists())
                    {
                        foreach (BaseEntity entity in SecondCar.children)
                        {
                            if (entity.ShortPrefabName == "modular_car_fuel_storage") (entity as StorageContainer).inventory.ClearItemsContainer();
                            else if (entity.ShortPrefabName == "1module_cockpit_with_engine")
                            {
                                foreach (BaseEntity ent in entity.children)
                                    if (ent is Rust.Modular.EngineStorage)
                                        (ent as Rust.Modular.EngineStorage).inventory.ClearItemsContainer();
                            }
                        }
                        SecondCar.Kill();
                    }

                    SecondCar = GameManager.server.CreateEntity("assets/content/vehicles/modularcar/3module_car_spawned.entity.prefab", GetGlobalPosition(new Vector3(27f, 3f, 7f))) as ModularCar;
                    SecondCar.enableSaving = false;
                    SecondCar.spawnSettings.useSpawnSettings = false;
                    SecondCar.Spawn();

                    foreach (string shortname in new HashSet<string> { "vehicle.1mod.cockpit.with.engine", "vehicle.1mod.rear.seats", "vehicle.1mod.passengers.armored" })
                    {
                        Item moduleItem = ItemManager.CreateByName(shortname);
                        if (!SecondCar.TryAddModule(moduleItem)) moduleItem.Remove();
                    }

                    foreach (BaseEntity entity in SecondCar.children)
                    {
                        if (entity.ShortPrefabName == "modular_car_fuel_storage") ItemManager.CreateByName("lowgradefuel", 50).MoveToContainer((entity as StorageContainer).inventory);
                        else if (entity.ShortPrefabName == "1module_cockpit_with_engine")
                        {
                            foreach (BaseEntity ent in entity.children)
                            {
                                if (ent is Rust.Modular.EngineStorage)
                                {
                                    Rust.Modular.EngineStorage container = ent as Rust.Modular.EngineStorage;
                                    ItemManager.CreateByName($"carburetor{_config.CarComponentLevel}").MoveToContainer(container.inventory);
                                    ItemManager.CreateByName($"crankshaft{_config.CarComponentLevel}").MoveToContainer(container.inventory);
                                    ItemManager.CreateByName($"piston{_config.CarComponentLevel}").MoveToContainer(container.inventory);
                                    ItemManager.CreateByName($"sparkplug{_config.CarComponentLevel}").MoveToContainer(container.inventory);
                                    ItemManager.CreateByName($"valve{_config.CarComponentLevel}").MoveToContainer(container.inventory);
                                }
                            }
                        }
                    }

                    SpawnPreset(_config.SecondNpc, false);

                    if (_ins.ActivePveMode) _ins.PveMode.Call("EventAddScientists", _ins.Name, SecondScientists.Select(x => x.net.ID.Value));

                    Arrived2 = true;

                    foreach (ScientistNPC first in FirstScientists)
                    {
                        foreach (ScientistNPC second in SecondScientists)
                        {
                            second.Brain.Senses.Memory.All.Add(new SimpleAIMemory.SeenInfo { Entity = first, Position = first.transform.position, Timestamp = Time.realtimeSinceStartup + TimeToFinish });
                            first.Brain.Senses.Memory.All.Add(new SimpleAIMemory.SeenInfo { Entity = second, Position = second.transform.position, Timestamp = Time.realtimeSinceStartup + TimeToFinish });
                        }
                    }
                }
            }

            private void SpawnFireBall(Vector3 pos, BaseEntity parent = null)
            {
                FireBall fireBall = GameManager.server.CreateEntity("assets/bundled/prefabs/oilfireballsmall.prefab", pos) as FireBall;
                fireBall.enableSaving = false;
                fireBall.Spawn();

                if (parent != null) fireBall.SetParent(parent);

                fireBall.GetComponent<Rigidbody>().isKinematic = true;
                fireBall.GetComponent<Collider>().enabled = false;

                fireBall.lifeTimeMin = TimeToFinish;
                fireBall.lifeTimeMax = TimeToFinish;
                fireBall.AddLife(TimeToFinish);

                FireBalls.Add(fireBall);
            }

            internal void SpawnPreset(PresetConfig preset, bool first)
            {
                int count = UnityEngine.Random.Range(preset.Min, preset.Max + 1);

                List<Vector3> positions = Pool.Get<List<Vector3>>();
                foreach (string pos in preset.Positions) positions.Add(GetGlobalPosition(pos.ToVector3()));

                JObject config = GetObjectConfig(preset.Config, preset.Config.Name);

                for (int i = 0; i < count; i++)
                {
                    Vector3 pos = positions.GetRandom();
                    positions.Remove(pos);

                    config["HomePosition"] = pos.ToString();

                    ScientistNPC npc = (ScientistNPC)_ins.NpcSpawn.Call("SpawnNpc", first ? FirstCar.transform.position : SecondCar.transform.position, config);

                    if (first) FirstScientists.Add(npc);
                    else SecondScientists.Add(npc);
                }

                Pool.FreeUnmanaged(ref positions);

                Interface.Oxide.CallHook("OnGasStationNpcSpawn", first ? FirstScientists.Select(x => x.net.ID.Value) : SecondScientists.Select(x => x.net.ID.Value));
            }

            private static JObject GetObjectConfig(NpcConfig config, string name)
            {
                HashSet<string> states = config.Stationary ? new HashSet<string> { "IdleState", "CombatStationaryState" } : new HashSet<string> { "RoamState", "ChaseState", "CombatState" };
                if (config.BeltItems.Any(x => x.ShortName == "rocket.launcher" || x.ShortName == "explosive.timed")) states.Add("RaidState");
                return new JObject
                {
                    ["Name"] = name,
                    ["WearItems"] = new JArray { config.WearItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["SkinID"] = x.SkinID }) },
                    ["BeltItems"] = new JArray { config.BeltItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["Amount"] = x.Amount, ["SkinID"] = x.SkinID, ["Mods"] = new JArray { x.Mods }, ["Ammo"] = x.Ammo }) },
                    ["Kit"] = config.Kit,
                    ["Health"] = config.Health,
                    ["RoamRange"] = config.RoamRange,
                    ["ChaseRange"] = config.ChaseRange,
                    ["SenseRange"] = config.SenseRange,
                    ["ListenRange"] = config.SenseRange / 2f,
                    ["AttackRangeMultiplier"] = config.AttackRangeMultiplier,
                    ["CheckVisionCone"] = config.CheckVisionCone,
                    ["VisionCone"] = config.VisionCone,
                    ["HostileTargetsOnly"] = false,
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
                    ["HomePosition"] = string.Empty,
                    ["MemoryDuration"] = config.MemoryDuration,
                    ["States"] = new JArray { states }
                };
            }

            private void TryTeleportPlayer(BasePlayer player)
            {
                if (player._limitedNetworking) return;
                if (IsInsideRoom1(player) || IsInsideRoom2(player))
                {
                    Vector3 pos = GetGlobalPosition(new Vector3(-12.68f, 2.991f, -12.26f));
                    player.Teleport(pos);
                }
            }

            private bool IsInsideRoom1(BasePlayer player)
            {
                Vector3 localPos = transform.InverseTransformPoint(player.transform.position);
                if (localPos.x is < -14.869f or > -10.469f) return false;
                if (localPos.y is < 3.159f or > 7.259f) return false;
                if (localPos.z is < 16.612f or > 21.612f) return false;
                return true;
            }

            private bool IsInsideRoom2(BasePlayer player)
            {
                Vector3 localPos = transform.InverseTransformPoint(player.transform.position);
                if (localPos.x is < 9.078f or > 18.078f) return false;
                if (localPos.y is < -6.118f or > 0.882f) return false;
                if (localPos.z is < 12.97f or > 26.97f) return false;
                return true;
            }

            private void CheckDefaultCrates()
            {
                if (_config.DefaultCrates.Count == 0) return;
                List<LootContainer> list = Pool.Get<List<LootContainer>>();
                Vis.Entities<LootContainer>(transform.position, _config.Radius, list);
                foreach (LootContainer container in list)
                    if (container.IsExists() && _config.DefaultCrates.Contains(container.ShortPrefabName))
                        container.Kill();
                Pool.FreeUnmanaged(ref list);
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

        internal class CustomSphereCollider : FacepunchBehaviour
        {
            private SphereCollider SphereCollider { get; set; } = null;

            private void Awake()
            {
                gameObject.layer = 3;
                SphereCollider = gameObject.AddComponent<SphereCollider>();
                SphereCollider.isTrigger = true;
                SphereCollider.radius = 4f;
            }

            private void OnTriggerEnter(Collider other)
            {
                BasePlayer player = other.GetComponentInParent<BasePlayer>();
                if (player.IsPlayer())
                {
                    if (_ins.ActivePveMode && _ins.PveMode.Call("CanActionEvent", _ins.Name, player) != null) return;
                    _ins.ActionEconomy(player.userID, "OpenSecondDoor");
                    _ins.Controller.OpenSecondDoor();
                }
            }
        }
        #endregion Controller

        #region Find Position
        internal MonumentInfo GetMonument()
        {
            List<MonumentInfo> list = Pool.Get<List<MonumentInfo>>();
            foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
            {
                if (monument.displayPhrase.english != "Oxum's Gas Station") continue;
                list.Add(monument);
            }
            MonumentInfo result = list.Count > 0 ? list.GetRandom() : null;
            Pool.FreeUnmanaged(ref list);
            return result;
        }
        #endregion Find Position

        #region Spawn Loot
        #region NPC
        private void OnCorpsePopulate(ScientistNPC entity, NPCPlayerCorpse corpse)
        {
            if (entity == null) return;
            if (Controller.FirstScientists.Contains(entity))
            {
                Controller.FirstScientists.Remove(entity);
                if (Controller.FirstScientists.Count == 0)
                {
                    AlertToAllPlayers("KillAllVagos", _config.Chat.Prefix);
                    Controller.SecondDoorSphere.AddComponent<CustomSphereCollider>();
                    Controller.SpawnCrates(_config.SecondCrates);
                    if (Controller.TimeToFinish < _config.PreFinishTime) Controller.TimeToFinish += _config.PreFinishTime;
                }
                NextTick(() =>
                {
                    if (corpse == null) return;
                    ItemContainer container = corpse.containers[0];
                    if (_config.FirstNpc.TypeLootTable == 1 || _config.FirstNpc.TypeLootTable == 4 || _config.FirstNpc.TypeLootTable == 5)
                    {
                        container.ClearItemsContainer();
                        if (_config.FirstNpc.TypeLootTable == 4 || _config.FirstNpc.TypeLootTable == 5) AddToContainerPrefab(container, _config.FirstNpc.PrefabLootTable);
                        if (_config.FirstNpc.TypeLootTable == 1 || _config.FirstNpc.TypeLootTable == 5) AddToContainerItem(container, _config.FirstNpc.OwnLootTable);
                    }
                    if (_config.FirstNpc.Config.IsRemoveCorpse && corpse.IsExists()) corpse.Kill();
                });
            }
            else if (Controller.SecondScientists.Contains(entity))
            {
                Controller.SecondScientists.Remove(entity);
                if (Controller.SecondScientists.Count == 0)
                {
                    AlertToAllPlayers("KillAllBallas", _config.Chat.Prefix);
                    Controller.SpawnCrates(_config.FirstCrates);
                    Controller.ClearAllCodeLocks();
                    if (_config.CanOpenFirstDoor)
                    {
                        Controller.FirstDoor.SetOpen(true);
                        if (Controller.FirstCodeLock.IsExists()) Controller.FirstCodeLock.Kill();
                    }
                    if (Controller.TimeToFinish < _config.PreFinishTime) Controller.TimeToFinish += _config.PreFinishTime;
                }
                NextTick(() =>
                {
                    if (corpse == null) return;
                    ItemContainer container = corpse.containers[0];
                    if (_config.SecondNpc.TypeLootTable == 1 || _config.SecondNpc.TypeLootTable == 4 || _config.SecondNpc.TypeLootTable == 5)
                    {
                        container.ClearItemsContainer();
                        if (_config.SecondNpc.TypeLootTable == 4 || _config.SecondNpc.TypeLootTable == 5) AddToContainerPrefab(container, _config.SecondNpc.PrefabLootTable);
                        if (_config.SecondNpc.TypeLootTable == 1 || _config.SecondNpc.TypeLootTable == 5) AddToContainerItem(container, _config.SecondNpc.OwnLootTable);
                    }
                    if (!_config.CanOpenFirstDoor && Controller.SecondScientists.Count == 0)
                    {
                        Item note = ItemManager.CreateByName("note");
                        note.text = $"Door = {Controller.FirstPassword}";
                        if (container.capacity < container.itemList.Count + 1) container.capacity++;
                        if (!note.MoveToContainer(container)) note.Remove();
                    }
                    if (_config.SecondNpc.Config.IsRemoveCorpse && corpse.IsExists()) corpse.Kill();
                });
            }
        }

        private object CanPopulateLoot(ScientistNPC entity, NPCPlayerCorpse corpse)
        {
            if (entity == null || corpse == null || Controller == null) return null;
            if (Controller.FirstScientists.Contains(entity))
            {
                if (_config.FirstNpc.TypeLootTable == 2) return null;
                else return true;
            }
            else if (Controller.SecondScientists.Contains(entity))
            {
                if (_config.SecondNpc.TypeLootTable == 2) return null;
                else return true;
            }
            return null;
        }

        private object OnCustomLootNPC(NetworkableId netId)
        {
            if (Controller == null) return null;
            ScientistNPC entity = Controller.FirstScientists.FirstOrDefault(x => x.IsExists() && x.net.ID.Value == netId.Value);
            if (entity != null)
            {
                if (_config.FirstNpc.TypeLootTable == 3) return null;
                else return true;
            }
            entity = Controller.SecondScientists.FirstOrDefault(x => x.IsExists() && x.net.ID.Value == netId.Value);
            if (entity != null)
            {
                if (_config.SecondNpc.TypeLootTable == 3) return null;
                else return true;
            }
            return null;
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
            CheckPrefabLootTable(_config.FirstNpc.PrefabLootTable);
            CheckLootTable(_config.FirstNpc.OwnLootTable);

            CheckPrefabLootTable(_config.SecondNpc.PrefabLootTable);
            CheckLootTable(_config.SecondNpc.OwnLootTable);

            foreach (CrateConfig config in _config.FirstCrates)
            {
                CheckPrefabLootTable(config.PrefabLootTable);
                CheckLootTable(config.OwnLootTable);
            }

            foreach (CrateConfig config in _config.SecondCrates)
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
                case "OpenFirstDoor":
                    AddBalance(playerId, _config.Economy.OpenFirstDoor);
                    break;
                case "OpenSecondDoor":
                    AddBalance(playerId, _config.Economy.OpenSecondDoor);
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
                    if (_config.Economy.Plugins.Contains("Economics") && plugins.Exists("Economics") && dic.Value > 0) Economics.Call("Deposit", dic.Key.ToString(), dic.Value);
                    if (_config.Economy.Plugins.Contains("Server Rewards") && plugins.Exists("ServerRewards") && intCount > 0) ServerRewards.Call("AddPoints", dic.Key, intCount);
                    if (_config.Economy.Plugins.Contains("IQEconomic") && plugins.Exists("IQEconomic") && intCount > 0) IQEconomic.Call("API_SET_BALANCE", dic.Key, intCount);
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
            if (_config.GuiAnnouncements.IsGuiAnnouncements) GUIAnnouncements?.Call("CreateAnnouncement", ClearColorAndSize(message), _config.GuiAnnouncements.BannerColor, _config.GuiAnnouncements.TextColor, player, _config.GuiAnnouncements.ApiAdjustVPosition);
            if (_config.Notify.IsNotify && plugins.Exists("Notify")) Notify?.Call("SendNotify", player, _config.Notify.Type, ClearColorAndSize(message));
        }
        #endregion Alerts

        #region GUI
        private HashSet<string> Names { get; } = new HashSet<string>
        {
            "Tab_KpucTaJl",
            "Clock_KpucTaJl",
            "Crate_KpucTaJl",
            "FirstNpc_KpucTaJl",
            "SecondNpc_KpucTaJl",
            "Password_KpucTaJl"
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
        [PluginReference] private readonly Plugin NpcSpawn, BetterNpc, MonumentOwner;

        private HashSet<string> HooksInsidePlugin { get; } = new HashSet<string>
        {
            "OnEntityTakeDamage",
            "CanMountEntity",
            "OnVehiclePush",
            "OnCodeEntered",
            "OnPlayerConnected",
            "OnPlayerDeath",
            "OnEntityDeath",
            "OnEntityKill",
            "OnLootEntity",
            "OnPlayerCommand",
            "OnServerCommand",
            "OnCorpsePopulate",
            "CanPopulateLoot",
            "OnCustomLootNPC",
            "OnCustomLootContainer",
            "OnContainerPopulate",
            "SetOwnerPveMode",
            "ClearOwnerPveMode",
            "CanEntityTakeDamage",
            "CanTeleport",
            "OnPlayerTeleported",
            "OnCustomNpcTarget"
        };

        private void ToggleHooks(bool subscribe)
        {
            foreach (string hook in HooksInsidePlugin)
            {
                if (subscribe) Subscribe(hook);
                else Unsubscribe(hook);
            }
        }

        private object OnCustomNpcTarget(ScientistNPC npc, BasePlayer target)
        {
            if (Controller == null || npc == null || target == null) return null;
            if (target.IsPlayer())
            {
                if (Controller.FirstScientists.Contains(npc))
                {
                    if (Controller.FirstAttackPlayers.Contains(target)) return null;
                    else return false;
                }
                else if (Controller.SecondScientists.Contains(npc))
                {
                    if (Controller.SecondAttackPlayers.Contains(target)) return null;
                    else return false;
                }
            }
            else if (target.skinID == 11162132011012)
            {
                ScientistNPC targetNpc = target as ScientistNPC;
                if (Controller.FirstScientists.Contains(npc))
                {
                    if (Controller.SecondScientists.Contains(targetNpc)) return true;
                    else if (Controller.FirstScientists.Contains(targetNpc)) return false;
                    else return null;
                }
                else if (Controller.SecondScientists.Contains(npc))
                {
                    if (Controller.FirstScientists.Contains(targetNpc)) return true;
                    else if (Controller.SecondScientists.Contains(targetNpc)) return false;
                    else return null;
                }
            }
            return null;
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
            webrequest.Enqueue("http://37.153.157.216:5000/Api/GetPluginVersions?pluginName=GasStationEvent", null, (code, response) =>
            {
                if (code != 200 || string.IsNullOrEmpty(response)) return;
                string[] array = response.Replace("\"", string.Empty).Split('.');
                VersionNumber latestVersion = new VersionNumber(Convert.ToInt32(array[0]), Convert.ToInt32(array[1]), Convert.ToInt32(array[2]));
                if (Version < latestVersion) PrintWarning($"A new version ({latestVersion}) of the plugin is available! You need to update the plugin:\n- https://lone.design/product/gasstationevent\n- https://codefling.com/plugins/gas-station-event");
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
        [ChatCommand("gsstart")]
        private void ChatStartEvent(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                if (!Active) Start(null);
                else PrintToChat(player, GetMessage("EventActive", player.UserIDString, _config.Chat.Prefix));
            }
        }

        [ChatCommand("gsstop")]
        private void ChatStopEvent(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                if (Controller != null) Finish();
                else Interface.Oxide.ReloadPlugin(Name);
            }
        }

        [ChatCommand("gspos")]
        private void ChatCommandPos(BasePlayer player)
        {
            if (Controller == null || !player.IsAdmin) return;
            Vector3 pos = Controller.transform.InverseTransformPoint(player.transform.position);
            Puts($"Position: {pos}");
            PrintToChat(player, $"Position: {pos}");
        }

        [ConsoleCommand("gsstart")]
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
            else Puts("This event is active now. To finish this event (gsstop), then to start the next one");
        }

        [ConsoleCommand("gsstop")]
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

namespace Oxide.Plugins.GasStationEventExtensionMethods
{
    public static class ExtensionMethods
    {
        public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return true;
            return false;
        }

        public static Dictionary<TKey, TValue> Where<TKey, TValue>(this Dictionary<TKey, TValue> source, Func<KeyValuePair<TKey, TValue>, bool> predicate)
        {
            Dictionary<TKey, TValue> result = new Dictionary<TKey, TValue>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) result.Add(enumerator.Current.Key, enumerator.Current.Value);
            return result;
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

        public static bool IsEqualVector3(this Vector3 a, Vector3 b) => Vector3.Distance(a, b) < 0.001f;
    }
}