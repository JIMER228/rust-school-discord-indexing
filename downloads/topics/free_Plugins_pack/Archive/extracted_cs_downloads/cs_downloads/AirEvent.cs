using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Facepunch;
using System.Reflection;
using UnityEngine.Networking;
using Oxide.Plugins.AirEventExtensionMethods;

namespace Oxide.Plugins
{
    [Info("AirEvent", "KpucTaJl", "2.2.7")]
    internal class AirEvent : RustPlugin
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
            if (_config.PluginVersion < new VersionNumber(2, 0, 3))
            {
                _config.Gui = new GuiConfig
                {
                    IsGui = true,
                    OffsetMinY = "-56"
                };
                foreach (PresetConfig preset in _config.Npc) foreach (NpcBelt belt in preset.Config.BeltItems) belt.Ammo = string.Empty;
            }
            if (_config.PluginVersion < new VersionNumber(2, 0, 6))
            {
                _config.Commands = new HashSet<string>
                {
                    "/remove",
                    "remove.toggle"
                };
            }
            if (_config.PluginVersion < new VersionNumber(2, 1, 1))
            {
                _config.Radius = 50f;
            }
            if (_config.PluginVersion < new VersionNumber(2, 1, 3))
            {
                foreach (PresetConfig preset in _config.Npc)
                {
                    preset.Config.RoamRange = 5f;
                    preset.Config.ChaseRange = 50f;
                    preset.Config.Speed = 7.5f;
                    preset.Config.Stationary = false;
                }
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
                    Text = "AirEvent"
                };
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
                _config.Positions = new HashSet<string>();
            }
            if (_config.PluginVersion < new VersionNumber(2, 1, 7))
            {
                _config.PveMode.ScaleDamage = new Dictionary<string, float>
                {
                    ["Npc"] = 1f
                };
            }
            if (_config.PluginVersion < new VersionNumber(2, 1, 9))
            {
                _config.Chat = new ChatConfig
                {
                    IsChat = true,
                    Prefix = "[AirEvent]"
                };
                _config.DistanceAlerts = 0f;
                _config.Notify.Type = 0;
            }
            _config.PluginVersion = Version;
            SaveConfig();
            Puts("Config update completed!");
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
            [JsonProperty(En ? "Loot table from prefabs (if the loot table type is 4 or 5)" : "Таблица предметов из prefab-ов (если тип таблицы предметов - 4 или 5)")] public PrefabLootTableConfig PrefabLootTable { get; set; }
            [JsonProperty(En ? "Own loot table (if the loot table type is 1 or 5)" : "Собственная таблица предметов (если тип таблицы предметов - 1 или 5)")] public LootTableConfig OwnLootTable { get; set; }
        }

        public class HackCrateConfig
        {
            [JsonProperty(En ? "Time to unlock the Crates [sec.]" : "Время разблокировки ящиков [sec.]")] public float UnlockTime { get; set; }
            [JsonProperty(En ? "Increase the event time if it's not enough to unlock the locked crate? [true/false]" : "Увеличивать время ивента, если недостаточно чтобы разблокировать заблокированный ящик? [true/false]")] public bool IncreaseEventTime { get; set; }
            [JsonProperty(En ? "Which loot table should the plugin use? (0 - default; 1 - own; 2 - AlphaLoot; 3 - CustomLoot; 4 - loot table of the Rust objects; 5 - combine the 1 and 4 methods)" : "Какую таблицу лута необходимо использовать? (0 - стандартную; 1 - собственную; 2 - AlphaLoot; 3 - CustomLoot; 4 - таблица предметов объектов Rust; 5 - совместить 1 и 4 методы)")] public int TypeLootTable { get; set; }
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
            [JsonProperty(En ? "Do you use the Discord? [true/false]" : "Использовать ли Discord? [true/false]")] public bool IsDiscord { get; set; }
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
            [JsonProperty(En ? "Hacking a locked crate" : "Взлом заблокированного ящика")] public double LockedCrate { get; set; }
            [JsonProperty(En ? "List of commands that are executed in the console at the end of the event ({steamid} - the player who collected the highest number of points)" : "Список команд, которые выполняются в консоли по окончанию ивента ({steamid} - игрок, который набрал наибольшее кол-во баллов)")] public HashSet<string> Commands { get; set; }
        }

        public class PveModeConfig
        {
            [JsonProperty(En ? "Use the PVE mode of the plugin? [true/false]" : "Использовать PVE режим работы плагина? [true/false]")] public bool Pve { get; set; }
            [JsonProperty(En ? "The amount of damage that the player has to do to become the Event Owner" : "Кол-во урона, которое должен нанести игрок, чтобы стать владельцем ивента")] public float Damage { get; set; }
            [JsonProperty(En ? "Damage Multipliers for calculate to become the Event Owner" : "Коэффициенты урона для подсчета, чтобы стать владельцем ивента")] public Dictionary<string, float> ScaleDamage { get; set; }
            [JsonProperty(En ? "Can the non-owner of the event loot the crates? [true/false]" : "Может ли не владелец ивента грабить ящики? [true/false]")] public bool LootCrate { get; set; }
            [JsonProperty(En ? "Can the non-owner of the event hack locked crates? [true/false]" : "Может ли не владелец ивента взламывать заблокированные ящики? [true/false]")] public bool HackCrate { get; set; }
            [JsonProperty(En ? "Can the non-owner of the event loot NPC corpses? [true/false]" : "Может ли не владелец ивента грабить трупы NPC? [true/false]")] public bool LootNpc { get; set; }
            [JsonProperty(En ? "Can the non-owner of the event deal damage to the NPC? [true/false]" : "Может ли не владелец ивента наносить урон по NPC? [true/false]")] public bool DamageNpc { get; set; }
            [JsonProperty(En ? "Can an Npc attack a non-owner of the event? [true/false]" : "Может ли Npc атаковать не владельца ивента? [true/false]")] public bool TargetNpc { get; set; }
            [JsonProperty(En ? "Allow the non-owner of the event to enter the event zone? [true/false]" : "Разрешать входить внутрь зоны ивента не владельцу ивента? [true/false]")] public bool CanEnter { get; set; }
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

        private class PluginConfig
        {
            [JsonProperty(En ? "Minimum time between events [sec.]" : "Минимальное время между ивентами [sec.]")] public float MinStartTime { get; set; }
            [JsonProperty(En ? "Maximum time between events [sec.]" : "Максимальное время между ивентами [sec.]")] public float MaxStartTime { get; set; }
            [JsonProperty(En ? "Is active the timer on to start the event? [true/false]" : "Активен ли таймер для запуска ивента? [true/false]")] public bool EnabledTimer { get; set; }
            [JsonProperty(En ? "Duration of the event [sec.]" : "Время проведения ивента [sec.]")] public int FinishTime { get; set; }
            [JsonProperty(En ? "Time before the starting of the event after receiving a chat message [sec.]" : "Время до начала ивента после сообщения в чате [sec.]")] public float PreStartTime { get; set; }
            [JsonProperty(En ? "Time until the end of the event after the last locked crate has been looted [sec.]" : "Время до окончания ивента после того, как последний заблокированный ящик будет украден [sec.]")] public int PreFinishTime { get; set; }
            [JsonProperty(En ? "Time to spawn each object during a airship appears on the map [sec.]" : "Время для спавна каждого объекта при появлении дирижабля на карте [sec.]")] public float Delay { get; set; }
            [JsonProperty(En ? "Which loot table should the plugin use in the crates? (0 - default; 1 - own; 2 - AlphaLoot; 3 - CustomLoot; 4 - loot table of the Rust objects; 5 - combine the 1 and 4 methods)" : "Какую таблицу лута необходимо использовать в ящиках? (0 - стандартную; 1 - собственную; 2 - AlphaLoot; 3 - CustomLoot; 4 - таблица предметов объектов Rust; 5 - совместить 1 и 4 методы)")] public int TypeLootTableCrates { get; set; }
            [JsonProperty(En ? "Crates setting" : "Настройка ящиков")] public HashSet<CrateConfig> DefaultCrates { get; set; }
            [JsonProperty(En ? "Locked Crates setting" : "Настройка заблокированных ящиков")] public HackCrateConfig HackCrate { get; set; }
            [JsonProperty(En ? "NPCs setting" : "Настройка NPC")] public HashSet<PresetConfig> Npc { get; set; }
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
            [JsonProperty(En ? "Interrupt the teleport in a airship? (only for users NTeleportation plugin) [true/false]" : "Запрещать телепорт на дирижабле? (только для тех, кто использует плагин NTeleportation) [true/false]")] public bool NTeleportationInterrupt { get; set; }
            [JsonProperty(En ? "Economy setting (total values will be added up and rewarded at the end of the event)" : "Настройка экономики (конечное значение суммируется и будет выдано игрокам по окончанию ивента)")] public EconomyConfig Economy { get; set; }
            [JsonProperty(En ? "List of commands banned in the event zone" : "Список команд запрещенных в зоне ивента")] public HashSet<string> Commands { get; set; }
            [JsonProperty(En ? "The first CCTV camera" : "Название первой камеры")] public string Cctv1 { get; set; }
            [JsonProperty(En ? "The second CCTV camera" : "Название второй камеры")] public string Cctv2 { get; set; }
            [JsonProperty(En ? "Height above the ground for the event appearance" : "Высота над землей для появления ивента")] public float Height { get; set; }
            [JsonProperty(En ? "Custom positions for the event to appear on the map" : "Кастомные позиции для появления ивента на карте")] public HashSet<string> Positions { get; set; }
            [JsonProperty(En ? "Do you want to make a smoke screen for the airship appearance? [true/false]" : "Создавать ли дымовую завесу для появления дирижабля? [true/false]")] public bool IsSmoke { get; set; }
            [JsonProperty(En ? "Configuration version" : "Версия конфигурации")] public VersionNumber PluginVersion { get; set; }

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig
                {
                    MinStartTime = 10800f,
                    MaxStartTime = 10800f,
                    EnabledTimer = true,
                    FinishTime = 3600,
                    PreStartTime = 300f,
                    PreFinishTime = 300,
                    Delay = 0.001f,
                    TypeLootTableCrates = 0,
                    DefaultCrates = new HashSet<CrateConfig>
                    {
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_elite.prefab",
                            Position = "(-7.637, 7.350, 13.646)",
                            Rotation = "(0.121, 152.430, 356.192)",
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1,
                                Max = 1,
                                UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 50.0f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_elite.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1,
                                Max = 1,
                                UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 100, MaxAmount = 200, Chance = 50.0f, IsBluePrint = false, SkinId = 0, Name = "" } }
                            }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_elite.prefab",
                            Position = "(6.899, 7.279, -14.312)",
                            Rotation = "(359.880, 332.430, 3.808)",
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1,
                                Max = 1,
                                UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 50.0f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_elite.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1,
                                Max = 1,
                                UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 100, MaxAmount = 200, Chance = 50.0f, IsBluePrint = false, SkinId = 0, Name = "" } }
                            }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                            Position = "(-3.389, 2.982, -3.597)",
                            Rotation = "(356.192, 62.438, 89.879)",
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1,
                                Max = 1,
                                UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 50.0f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_normal.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1,
                                Max = 1,
                                UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 100, MaxAmount = 200, Chance = 50.0f, IsBluePrint = false, SkinId = 0, Name = "" } }
                            }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                            Position = "(4.698, 3.599, 0.826)",
                            Rotation = "(359.879, 332.430, 93.808)",
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1,
                                Max = 1,
                                UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 50.0f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_normal.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1,
                                Max = 1,
                                UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 100, MaxAmount = 200, Chance = 50.0f, IsBluePrint = false, SkinId = 0, Name = "" } }
                            }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_normal_2.prefab",
                            Position = "(0.595, 6.674, 0.731)",
                            Rotation = "(359.880, 332.430, 3.808)",
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1,
                                Max = 1,
                                UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 50.0f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_normal_2.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1,
                                Max = 1,
                                UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 100, MaxAmount = 200, Chance = 50.0f, IsBluePrint = false, SkinId = 0, Name = "" } }
                            }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_normal_2.prefab",
                            Position = "(-2.830, 3.935, 3.819)",
                            Rotation = "(0.121, 152.430, 356.192)",
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1,
                                Max = 1,
                                UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 50.0f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_normal_2.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1,
                                Max = 1,
                                UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 100, MaxAmount = 200, Chance = 50.0f, IsBluePrint = false, SkinId = 0, Name = "" } }
                            }
                        }
                    },
                    HackCrate = new HackCrateConfig
                    {
                        UnlockTime = 600f,
                        IncreaseEventTime = true,
                        TypeLootTable = 0,
                        PrefabLootTable = new PrefabLootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = false,
                            Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab" } }
                        },
                        OwnLootTable = new LootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = true,
                            Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 100, MaxAmount = 200, Chance = 50.0f, IsBluePrint = false, SkinId = 0, Name = "" } }
                        }
                    },
                    Npc = new HashSet<PresetConfig>
                    {
                        new PresetConfig
                        {
                            Min = 15,
                            Max = 15,
                            Positions = new HashSet<string>
                            {
                                "(9.4, 7.2, -8.5)",
                                "(0.7, 6.6, -13.0)",
                                "(5.2, 6.9, -11.0)",
                                "(7.6, 7.3, -2.0)",
                                "(4.7, 7.3, 3.3)",
                                "(-3.4, 6.5, -7.8)",
                                "(-6.2, 6.5, -2.4)",
                                "(-12.9, 4.5, 11.1)",
                                "(-2.3, 5.3, 16.8)",
                                "(-1.4, 3.5, -10.4)",
                                "(8.7, 4.3, -4.6)",
                                "(-8.9, 3.5, 3.0)",
                                "(2.0, 4.3, 8.6)",
                                "(-8.9, 3.9, 13.1)",
                                "(-6.1, 4.1, 14.5)"
                            },
                            Config = new NpcConfig
                            {
                                Name = "AirEvent",
                                Health = 200f,
                                RoamRange = 5f,
                                ChaseRange = 50f,
                                AttackRangeMultiplier = 1f,
                                SenseRange = 50f,
                                MemoryDuration = 10f,
                                DamageScale = 2f,
                                AimConeScale = 1f,
                                CheckVisionCone = false,
                                VisionCone = 135f,
                                Speed = 7.5f,
                                DisableRadio = true,
                                Stationary = false,
                                IsRemoveCorpse = true,
                                WearItems = new HashSet<NpcWear>
                                {
                                    new NpcWear { ShortName = "hoodie", SkinId = 1700935391 },
                                    new NpcWear { ShortName = "movembermoustache", SkinId = 0 },
                                    new NpcWear { ShortName = "pants", SkinId = 1700938224 },
                                    new NpcWear { ShortName = "shoes.boots", SkinId = 2575506021 },
                                    new NpcWear { ShortName = "burlap.headwrap", SkinId = 1694253807 }
                                },
                                BeltItems = new HashSet<NpcBelt>
                                {
                                    new NpcBelt { ShortName = "rifle.lr300", Amount = 1, SkinId = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }, Ammo = string.Empty },
                                    new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinId = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                                },
                                Kit = ""
                            },
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = false,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = false,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "scrap", MinAmount = 8, MaxAmount = 33, Chance = 36f, IsBluePrint = false, SkinId = 0, Name = string.Empty }
                                }
                            }
                        },
                        new PresetConfig
                        {
                            Min = 1,
                            Max = 1,
                            Positions = new HashSet<string>
                            {
                                "(-4.8, 7.0, 8.2)"
                            },
                            Config = new NpcConfig
                            {
                                Name = "Boss",
                                Health = 500f,
                                RoamRange = 5f,
                                ChaseRange = 50f,
                                AttackRangeMultiplier = 1f,
                                SenseRange = 50f,
                                MemoryDuration = 10f,
                                DamageScale = 2f,
                                AimConeScale = 1f,
                                CheckVisionCone = false,
                                VisionCone = 135f,
                                Speed = 7.5f,
                                DisableRadio = true,
                                Stationary = false,
                                IsRemoveCorpse = true,
                                WearItems = new HashSet<NpcWear>
                                {
                                    new NpcWear { ShortName = "hoodie", SkinId = 1700935391 },
                                    new NpcWear { ShortName = "movembermoustache", SkinId = 0 },
                                    new NpcWear { ShortName = "pants", SkinId = 1700938224 },
                                    new NpcWear { ShortName = "shoes.boots", SkinId = 2575506021 },
                                    new NpcWear { ShortName = "burlap.headwrap", SkinId = 1694253807 },
                                    new NpcWear { ShortName = "gloweyes", SkinId = 0 }
                                },
                                BeltItems = new HashSet<NpcBelt>
                                {
                                    new NpcBelt { ShortName = "lmg.m249", Amount = 1, SkinId = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }, Ammo = string.Empty },
                                    new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinId = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                                },
                                Kit = ""
                            },
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = false,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_heavy.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = false,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "scrap", MinAmount = 25, MaxAmount = 25, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = string.Empty }
                                }
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
                        Text = "AirEvent"
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
                        Prefix = "[AirEvent]"
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
                            "HackCrate"
                        }
                    },
                    Radius = 50f,
                    IsCreateZonePvp = false,
                    PveMode = new PveModeConfig
                    {
                        Pve = false,
                        Damage = 500f,
                        ScaleDamage = new Dictionary<string, float> { ["Npc"] = 1f },
                        LootCrate = false,
                        HackCrate = false,
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
                    Economy = new EconomyConfig
                    {
                        Plugins = new HashSet<string> { "Economics", "Server Rewards", "IQEconomic", "XPerience" },
                        Min = 0,
                        Crates = new Dictionary<string, double>
                        {
                            ["crate_elite"] = 0.4,
                            ["crate_normal"] = 0.2,
                            ["crate_normal_2"] = 0.1
                        },
                        Npc = 0.3,
                        LockedCrate = 0.5,
                        Commands = new HashSet<string>()
                    },
                    Commands = new HashSet<string>
                    {
                        "/remove",
                        "remove.toggle"
                    },
                    Cctv1 = "AirShipBow",
                    Cctv2 = "AirShipStern",
                    Height = 150f,
                    Positions = new HashSet<string>(),
                    IsSmoke = false,
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
                ["PreStart"] = "{0} The Airship with scientists is coming to the island!\nIt will arrive in <color=#55aaff>{1}</color>",
                ["Start"] = "{0} The Airship scientists <color=#738d43>have arrived</color>!\nThe Airship is located in grid <color=#55aaff>{1}</color> at a height of <color=#55aaff>{2} m.</color>\nCCTV cameras: <color=#55aaff>{3}</color>, <color=#55aaff>{4}</color>",
                ["PreFinish"] = "{0} The Airship <color=#ce3f27>will self destruct</color> in <color=#55aaff>{1}</color>!",
                ["Finish"] = "{0} The Airship <color=#ce3f27>has self destructed</color>!",
                ["SetOwner"] = "{0} Player <color=#55aaff>{1}</color> <color=#738d43>has received</color> the owner status for the <color=#55aaff>Air Event</color>",
                ["EventActive"] = "{0} This event is active now. To finish this event (<color=#55aaff>/airstop</color>), then (<color=#55aaff>/airstart</color>) to start the next one!",
                ["EnterPVP"] = "{0} You <color=#ce3f27>have entered</color> the PVP zone, now other players <color=#ce3f27>can damage</color> you!",
                ["ExitPVP"] = "{0} You <color=#738d43>have left</color> the PVP zone, now other players <color=#738d43>cannot damage</color> you!",
                ["HackCrate"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>has started</color> hacking a locked crate on The Airship!",
                ["NTeleportation"] = "{0} You <color=#ce3f27>cannot</color> teleport into the event zone!",
                ["SendEconomy"] = "{0} You <color=#738d43>have earned</color> <color=#55aaff>{1}</color> points in economics for participating in the event",
                ["NoCommand"] = "{0} You <color=#ce3f27>cannot</color> use this command in the event zone!"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PreStart"] = "{0} Дирижабль с учеными приближается к острову!\nПрибудет через <color=#55aaff>{1}</color>",
                ["Start"] = "{0} Ученые <color=#738d43>прибыли</color>!\nДирижабрь находится в квадрате <color=#55aaff>{1}</color> на высоте <color=#55aaff>{2} м.</color>\nКамеры: <color=#55aaff>{3}</color>, <color=#55aaff>{4}</color>",
                ["PreFinish"] = "{0} Дирижабль будет <color=#ce3f27>уничтожен</color> через <color=#55aaff>{1}</color>!",
                ["Finish"] = "{0} Дирижабль <color=#ce3f27>уничтожен</color>!",
                ["SetOwner"] = "{0} Игрок <color=#55aaff>{1}</color> <color=#738d43>получил</color> статус владельца ивента для <color=#55aaff>Air Event</color>",
                ["EventActive"] = "{0} Ивент в данный момент активен, сначала завершите текущий ивент (<color=#55aaff>/airstop</color>), чтобы начать следующий!",
                ["EnterPVP"] = "{0} Вы <color=#ce3f27>вошли</color> в PVP зону, теперь другие игроки <color=#ce3f27>могут</color> наносить вам урон!",
                ["ExitPVP"] = "{0} Вы <color=#738d43>вышли</color> из PVP зоны, теперь другие игроки <color=#738d43>не могут</color> наносить вам урон!",
                ["HackCrate"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>начал</color> взлом заблокированного ящика на дирижабле!",
                ["NTeleportation"] = "{0} Вы <color=#ce3f27>не можете</color> телепортироваться в зоне ивента!",
                ["SendEconomy"] = "{0} Вы <color=#738d43>получили</color> <color=#55aaff>{1}</color> баллов в экономику за прохождение ивента",
                ["NoCommand"] = "{0} Вы <color=#ce3f27>не можете</color> использовать данную команду в зоне ивента!"
            }, this, "ru");
        }

        private string GetMessage(string langKey, string userId) => lang.GetMessage(langKey, _ins, userId);

        private string GetMessage(string langKey, string userId, params object[] args) => (args.Length == 0) ? GetMessage(langKey, userId) : string.Format(GetMessage(langKey, userId), args);
        #endregion Lang

        #region Oxide Hooks
        private static AirEvent _ins;

        private void Init()
        {
            _ins = this;
            ToggleHooks(false);
        }

        private void OnServerInitialized()
        {
            CheckAllLootTables();
            ServerMgr.Instance.StartCoroutine(DownloadImages());
            StartTimer();
        }

        private void Unload()
        {
            if (Controller != null) Finish();
            _ins = null;
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;
            if (entity.ShortPrefabName == "door_barricade_b") return null;
            if (Controller.Entities.Contains(entity)) return true;
            HotAirBalloon attackerBalloon = info.Initiator as HotAirBalloon;
            if (attackerBalloon == null) return null;
            if (Controller.AirBalloons.Contains(attackerBalloon) && (entity as BasePlayer).IsPlayer()) return true;
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

        private void OnEntitySpawned(DroppedItemContainer container) { if (container != null && Vector3.Distance(Controller.transform.position, container.transform.position) < _config.Radius) Controller.Backpacks.Add(container); }

        private void OnEntitySpawned(SimpleShark shark) { if (shark.IsExists() && Vector2.Distance(new Vector2(Controller.transform.position.x, Controller.transform.position.z), new Vector2(shark.transform.position.x, shark.transform.position.z)) < _config.Radius) shark.Kill(); }

        private object OnEntityKill(BaseEntity entity)
        {
            if (entity == null) return null;

            DroppedItemContainer droppedContainer = entity as DroppedItemContainer;
            if (droppedContainer != null && Controller.Backpacks.Contains(droppedContainer))
            {
                Controller.Backpacks.Remove(droppedContainer);
                return null;
            }

            HackableLockedCrate hackCrate = entity as HackableLockedCrate;
            if (hackCrate != null && Controller.HackCrates.Contains(hackCrate))
            {
                Controller.HackCrates.Remove(hackCrate);
                return null;
            }

            LootContainer lootContainer = entity as LootContainer;
            if (lootContainer != null && Controller.Crates.Contains(lootContainer))
            {
                Controller.Crates.Remove(lootContainer);
                return null;
            }

            if (Controller.Entities.Contains(entity))
            {
                if (entity.ShortPrefabName == "door_barricade_b") return null;
                if (!Controller.KillEntities) return true;
            }

            return null;
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
            if (Controller.Scientists.Contains(npc)) ActionEconomy(attacker.userID, "Npc");
        }

        private Dictionary<ulong, BasePlayer> StartHackCrates { get; } = new Dictionary<ulong, BasePlayer>();

        private void CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            if (player == null || crate == null) return;
            if (Controller.HackCrates.Contains(crate))
            {
                if (StartHackCrates.ContainsKey(crate.net.ID.Value)) StartHackCrates[crate.net.ID.Value] = player;
                else StartHackCrates.Add(crate.net.ID.Value, player);
            }
        }

        private void OnCrateHack(HackableLockedCrate crate)
        {
            if (crate == null) return;
            ulong crateId = crate.net.ID.Value;
            BasePlayer player;
            if (StartHackCrates.TryGetValue(crateId, out player))
            {
                StartHackCrates.Remove(crateId);
                if (_config.HackCrate.IncreaseEventTime && Controller.TimeToFinish < (int)_config.HackCrate.UnlockTime) Controller.TimeToFinish += (int)_config.HackCrate.UnlockTime;
                ActionEconomy(player.userID, "LockedCrate");
                AlertToAllPlayers("HackCrate", _config.Chat.Prefix, player.displayName);
            }
        }

        private object OnSamSiteTarget(SamSite entity, HotAirBalloon target)
        {
            if (entity == null || target == null) return null;
            if (Controller.AirBalloons.Contains(target)) return true;
            else return null;
        }

        private HashSet<ulong> LootableCrates { get; } = new HashSet<ulong>();

        private void OnLootEntity(BasePlayer player, LootContainer container)
        {
            if (!player.IsPlayer() || container == null || LootableCrates.Contains(container.net.ID.Value)) return;
            if (Controller.Crates.Contains(container))
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
        internal class Prefab { public string Path; public Vector3 Pos; public Vector3 Rot; }
        internal HashSet<Prefab> Prefabs { get; } = new HashSet<Prefab>
        {
            //floor
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(6.422f, 1.005f, -5.951f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(3.769f, 0.805f, -7.336f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(1.115f, 0.606f, -8.721f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-0.273f, 0.612f, -6.062f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(2.380f, 0.812f, -4.677f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(5.034f, 1.011f, -3.292f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(3.645f, 1.017f, -0.633f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(0.992f, 0.818f, -2.018f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-1.662f, 0.619f, -3.403f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-3.050f, 0.625f, -0.743f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-0.397f, 0.824f, 0.642f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(2.257f, 1.024f, 2.027f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(0.868f, 1.030f, 4.686f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-1.785f, 0.831f, 3.301f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-4.439f, 0.631f, 1.916f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-5.827f, 0.638f, 4.575f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-3.174f, 0.837f, 5.960f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-0.520f, 1.036f, 7.345f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-1.909f, 1.042f, 10.005f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-4.562f, 0.843f, 8.620f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-7.216f, 0.644f, 7.235f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-11.258f, 0.451f, 8.509f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-8.604f, 0.650f, 9.894f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-5.951f, 0.850f, 11.279f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-3.297f, 1.049f, 12.664f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-0.643f, 1.248f, 14.049f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-2.032f, 1.254f, 16.708f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-4.686f, 1.055f, 15.323f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-7.339f, 0.856f, 13.938f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-9.993f, 0.657f, 12.553f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-12.647f, 0.457f, 11.168f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(8.902f, 4.197f, -4.664f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(6.249f, 3.998f, -6.049f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(3.595f, 3.799f, -7.434f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(0.941f, 3.600f, -8.819f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-1.712f, 3.400f, -10.204f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-3.101f, 3.407f, -7.545f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-0.447f, 3.606f, -6.160f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(2.207f, 3.805f, -4.775f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(4.860f, 4.004f, -3.390f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(7.514f, 4.204f, -2.005f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(6.125f, 4.210f, 0.655f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(3.472f, 4.011f, -0.730f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(0.818f, 3.811f, -2.115f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-1.836f, 3.612f, -3.501f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-4.489f, 3.413f, -4.886f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-5.878f, 3.419f, -2.226f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-3.224f, 3.618f, -0.841f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-0.570f, 3.818f, 0.544f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(2.083f, 4.017f, 1.929f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(4.737f, 4.216f, 3.314f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(3.348f, 4.222f, 5.973f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(0.695f, 4.023f, 4.588f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-1.959f, 3.824f, 3.203f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-4.613f, 3.625f, 1.818f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-7.266f, 3.426f, 0.433f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-8.655f, 3.432f, 3.092f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-6.001f, 3.631f, 4.477f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-3.347f, 3.830f, 5.863f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-0.694f, 4.030f, 7.248f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(1.960f, 4.229f, 8.633f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(0.519f, 5.137f, 11.263f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-2.082f, 4.036f, 9.907f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-4.736f, 3.837f, 8.522f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-7.390f, 3.637f, 7.137f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-10.096f, 4.340f, 5.722f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-11.484f, 4.344f, 8.382f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-8.778f, 3.644f, 9.796f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-6.124f, 3.843f, 11.181f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-3.471f, 4.042f, 12.566f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-0.869f, 5.141f, 13.922f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-2.258f, 5.148f, 16.581f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-4.859f, 4.049f, 15.226f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-7.513f, 3.849f, 13.841f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-10.167f, 3.650f, 12.455f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-12.872f, 4.351f, 11.041f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(1.835f, 6.381f, 8.562f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(3.175f, 7.209f, 5.876f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(4.564f, 7.202f, 3.216f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(5.952f, 7.196f, 0.556f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(7.341f, 7.190f, -2.103f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(8.729f, 7.191f, -4.762f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-8.780f, 5.584f, 3.021f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-7.440f, 6.412f, 0.334f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-6.051f, 6.405f, -2.325f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-4.663f, 6.399f, -4.984f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-3.275f, 6.400f, -7.644f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-1.886f, 6.394f, -10.303f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-6.175f, 6.624f, 4.380f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-3.520f, 6.816f, 5.765f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-0.867f, 7.023f, 7.150f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-2.255f, 7.022f, 9.809f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-4.909f, 6.823f, 8.424f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-7.563f, 6.631f, 7.039f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-8.952f, 6.637f, 9.698f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-6.298f, 6.836f, 11.083f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-3.644f, 7.028f, 12.468f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-10.393f, 7.549f, 12.328f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-10.340f, 6.636f, 12.358f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-5.032f, 7.035f, 15.128f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-5.085f, 7.948f, 15.098f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor/floor.prefab", Pos = new Vector3(-7.686f, 6.835f, 13.743f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            //floor.triangle
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(10.685f, 7.227f, -6.901f), Rot = new Vector3(3.237f, 272.491f, 2.011f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(8.032f, 7.028f, -8.285f), Rot = new Vector3(356.763f, 92.491f, 357.989f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(8.032f, 7.028f, -8.285f), Rot = new Vector3(3.237f, 272.491f, 2.011f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(5.379f, 6.829f, -9.669f), Rot = new Vector3(356.763f, 92.491f, 357.989f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(5.379f, 6.829f, -9.669f), Rot = new Vector3(3.237f, 272.491f, 2.011f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(2.726f, 6.630f, -11.053f), Rot = new Vector3(356.763f, 92.491f, 357.989f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(2.726f, 6.630f, -11.053f), Rot = new Vector3(3.237f, 272.491f, 2.011f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(1.398f, 6.530f, -11.745f), Rot = new Vector3(3.358f, 212.381f, 358.199f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(0.073f, 6.430f, -12.436f), Rot = new Vector3(3.237f, 272.491f, 2.011f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(2.605f, 6.525f, -14.049f), Rot = new Vector3(3.237f, 272.491f, 2.011f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(3.932f, 6.624f, -13.355f), Rot = new Vector3(3.358f, 212.381f, 358.199f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(5.259f, 6.724f, -12.664f), Rot = new Vector3(3.237f, 272.491f, 2.011f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(6.586f, 6.824f, -11.971f), Rot = new Vector3(3.358f, 212.381f, 358.199f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(7.912f, 6.923f, -11.279f), Rot = new Vector3(3.237f, 272.491f, 2.011f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(9.240f, 7.023f, -10.586f), Rot = new Vector3(3.358f, 212.381f, 358.199f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(10.564f, 7.122f, -9.897f), Rot = new Vector3(3.237f, 272.491f, 2.011f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(9.114f, 6.918f, -13.583f), Rot = new Vector3(356.642f, 32.381f, 1.802f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(9.053f, 6.865f, -15.081f), Rot = new Vector3(359.879f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(6.523f, 6.771f, -13.469f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(6.399f, 6.666f, -16.466f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(3.807f, 6.526f, -16.352f), Rot = new Vector3(356.642f, 32.381f, 1.802f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(10.862f, 4.241f, -6.800f), Rot = new Vector3(3.237f, 272.491f, 2.011f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(8.208f, 4.042f, -8.185f), Rot = new Vector3(356.763f, 92.491f, 357.989f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(8.208f, 4.042f, -8.185f), Rot = new Vector3(3.237f, 272.491f, 2.011f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(6.880f, 3.942f, -8.877f), Rot = new Vector3(3.358f, 212.381f, 358.198f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(5.553f, 3.843f, -9.571f), Rot = new Vector3(3.237f, 272.491f, 2.011f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(2.901f, 3.643f, -10.955f), Rot = new Vector3(356.763f, 92.491f, 357.989f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(2.901f, 3.643f, -10.955f), Rot = new Vector3(3.237f, 272.491f, 2.011f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(0.248f, 3.437f, -12.340f), Rot = new Vector3(356.763f, 92.491f, 357.989f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(0.248f, 3.438f, -12.340f), Rot = new Vector3(3.237f, 272.491f, 2.011f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(2.779f, 3.532f, -13.951f), Rot = new Vector3(3.237f, 272.491f, 2.011f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(4.106f, 3.638f, -13.257f), Rot = new Vector3(3.358f, 212.381f, 358.199f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(4.106f, 3.638f, -13.257f), Rot = new Vector3(356.643f, 32.381f, 1.802f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(5.433f, 3.738f, -12.564f), Rot = new Vector3(356.763f, 92.491f, 357.989f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(8.086f, 3.937f, -11.181f), Rot = new Vector3(3.237f, 272.491f, 2.011f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(9.413f, 4.037f, -10.487f), Rot = new Vector3(3.358f, 212.381f, 358.199f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(10.740f, 4.130f, -9.796f), Rot = new Vector3(3.237f, 272.491f, 2.011f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(9.288f, 3.925f, -13.484f), Rot = new Vector3(356.642f, 32.381f, 1.802f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(9.226f, 3.872f, -14.983f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(6.634f, 3.732f, -14.869f), Rot = new Vector3(356.642f, 32.381f, 1.802f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(6.573f, 3.673f, -16.368f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(3.981f, 3.526f, -16.255f), Rot = new Vector3(356.643f, 32.381f, 1.802f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(1.809f, 0.603f, -10.051f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(4.401f, 0.750f, -10.165f), Rot = new Vector3(3.358f, 212.381f, 178.199f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(4.463f, 0.802f, -8.666f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(5.728f, 0.849f, -9.472f), Rot = new Vector3(356.763f, 92.491f, 177.990f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(7.117f, 1.001f, -7.281f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(6.994f, 0.896f, -10.277f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(6.932f, 0.844f, -11.776f), Rot = new Vector3(3.358f, 212.381f, 178.199f) },
            new Prefab { Path = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", Pos = new Vector3(4.339f, 0.697f, -11.663f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            //wall.low
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(3.113f, 7.322f, 9.227f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(4.675f, 4.322f, 6.666f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(5.890f, 7.309f, 3.909f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(7.452f, 4.309f, 1.347f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(8.667f, 7.297f, -1.410f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(10.230f, 4.290f, -3.972f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(10.688f, 7.235f, -6.898f), Rot = new Vector3(357.993f, 2.377f, 3.239f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(10.739f, 4.136f, -9.795f), Rot = new Vector3(357.993f, 2.377f, 3.239f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(10.566f, 7.130f, -9.893f), Rot = new Vector3(357.993f, 2.377f, 3.239f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(1.277f, 6.432f, -14.741f), Rot = new Vector3(358.202f, 122.487f, 356.641f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-1.254f, 6.338f, -13.130f), Rot = new Vector3(358.202f, 122.487f, 356.641f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(1.451f, 3.438f, -14.643f), Rot = new Vector3(358.202f, 122.487f, 356.641f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-3.039f, 3.301f, -10.897f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-4.601f, 6.300f, -8.335f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-5.816f, 3.313f, -5.578f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-7.378f, 6.313f, -3.017f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-8.593f, 3.326f, -0.259f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-10.155f, 6.326f, 2.302f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-9.371f, 3.435f, 4.464f), Rot = new Vector3(356.192f, 62.438f, 359.879f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-8.755f, 3.535f, 6.424f), Rot = new Vector3(359.879f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-10.140f, 3.541f, 9.085f), Rot = new Vector3(359.879f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-11.528f, 3.548f, 11.745f), Rot = new Vector3(359.879f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(1.244f, 4.225f, 10.005f), Rot = new Vector3(356.192f, 62.438f, 359.879f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-0.711f, 4.139f, 10.622f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-2.101f, 4.145f, 13.281f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-3.492f, 4.151f, 15.939f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-3.705f, 7.134f, 15.820f), Rot = new Vector3(359.879f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-4.338f, 7.031f, 13.798f), Rot = new Vector3(356.192f, 62.438f, 359.879f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-5.727f, 7.038f, 16.457f), Rot = new Vector3(3.808f, 242.438f, 0.121f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-9.646f, 6.640f, 11.028f), Rot = new Vector3(356.192f, 62.438f, 359.879f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-11.666f, 6.537f, 11.665f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-11.034f, 6.639f, 13.687f), Rot = new Vector3(3.808f, 242.438f, 0.121f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-9.013f, 6.736f, 13.050f), Rot = new Vector3(359.879f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/wall.low/wall.low.prefab", Pos = new Vector3(-6.359f, 6.935f, 14.435f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            //wall.half
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-11.370f, 3.339f, 5.059f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-12.759f, 3.345f, 7.719f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-14.147f, 3.351f, 10.378f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-13.514f, 3.454f, 12.400f), Rot = new Vector3(3.808f, 242.438f, 0.121f) },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-2.900f, 4.251f, 17.940f), Rot = new Vector3(3.808f, 242.438f, 0.121f) },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(-0.879f, 4.347f, 17.303f), Rot = new Vector3(359.879f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(0.510f, 4.341f, 14.644f), Rot = new Vector3(359.879f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/wall.half/wall.half.prefab", Pos = new Vector3(1.898f, 4.335f, 11.985f), Rot = new Vector3(359.879f, 332.430f, 3.808f) },
            //wall
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(-10.564f, 0.448f, 7.179f), Rot = new Vector3(356.192f, 62.438f, 359.879f) },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(-12.585f, 0.351f, 7.816f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(-13.973f, 0.358f, 10.476f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(-13.341f, 0.461f, 12.498f), Rot = new Vector3(3.808f, 242.438f, 0.121f) },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(-10.687f, 0.660f, 13.883f), Rot = new Vector3(3.808f, 242.438f, 0.121f) },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(-8.033f, 0.859f, 15.268f), Rot = new Vector3(3.808f, 242.438f, 0.121f) },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(-5.380f, 1.058f, 16.653f), Rot = new Vector3(3.808f, 242.438f, 0.121f) },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(-2.726f, 1.258f, 18.038f), Rot = new Vector3(3.808f, 242.438f, 0.121f) },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(-5.553f, 4.052f, 16.555f), Rot = new Vector3(3.808f, 242.438f, 0.121f) },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(-8.207f, 3.852f, 15.170f), Rot = new Vector3(3.808f, 242.438f, 0.121f) },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(-10.861f, 3.653f, 13.785f), Rot = new Vector3(3.808f, 242.438f, 0.121f) },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(-0.705f, 1.354f, 17.401f), Rot = new Vector3(359.879f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(0.683f, 1.348f, 14.742f), Rot = new Vector3(359.879f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(0.051f, 1.245f, 12.719f), Rot = new Vector3(356.192f, 62.438f, 359.879f) },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(10.442f, 7.018f, -12.889f), Rot = new Vector3(357.993f, 2.377f, 3.239f) },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(9.053f, 6.865f, -15.081f), Rot = new Vector3(356.192f, 62.438f, 359.879f) },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(6.399f, 6.666f, -16.466f), Rot = new Vector3(356.192f, 62.438f, 359.879f) },
            new Prefab { Path = "assets/prefabs/building core/wall/wall.prefab", Pos = new Vector3(3.807f, 6.526f, -16.352f), Rot = new Vector3(358.202f, 122.487f, 356.641f) },
            //wall.window
            new Prefab { Path = "assets/prefabs/building core/wall.window/wall.window.prefab", Pos = new Vector3(9.226f, 3.872f, -14.983f), Rot = new Vector3(356.192f, 62.438f, 359.879f) },
            new Prefab { Path = "assets/prefabs/building core/wall.window/wall.window.prefab", Pos = new Vector3(6.573f, 3.673f, -16.368f), Rot = new Vector3(356.192f, 62.438f, 359.879f) },
            //shutter.metal.embrasure.a
            new Prefab { Path = "assets/prefabs/building/wall.window.embrasure/shutter.metal.embrasure.a.prefab", Pos = new Vector3(9.166f, 4.906f, -15.017f), Rot = new Vector3(356.192f, 62.438f, 359.879f) },
            new Prefab { Path = "assets/prefabs/building/wall.window.embrasure/shutter.metal.embrasure.a.prefab", Pos = new Vector3(6.513f, 4.706f, -16.402f), Rot = new Vector3(356.192f, 62.438f, 359.879f) },
            //wall.frame
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(3.287f, 4.328f, 9.325f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(6.064f, 4.316f, 4.006f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(8.841f, 4.303f, -1.312f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(0.758f, 3.387f, 8.007f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(10.862f, 4.241f, -6.800f), Rot = new Vector3(357.993f, 2.377f, 3.239f) },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(10.739f, 4.136f, -9.795f), Rot = new Vector3(357.993f, 2.377f, 3.239f) },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(10.615f, 4.030f, -12.791f), Rot = new Vector3(357.993f, 2.377f, 3.239f) },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(3.980f, 3.532f, -16.254f), Rot = new Vector3(358.202f, 122.487f, 356.641f) },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(1.451f, 3.438f, -14.643f), Rot = new Vector3(358.202f, 122.487f, 356.641f) },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(-1.081f, 3.344f, -13.032f), Rot = new Vector3(358.202f, 122.487f, 356.641f) },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(-4.428f, 3.307f, -8.237f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(-7.205f, 3.320f, -2.919f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(-9.982f, 3.332f, 2.400f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(-7.289f, 2.782f, 3.807f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(0.001f, 4.019f, 5.918f), Rot = new Vector3(356.192f, 62.438f, 359.879f) },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(-2.653f, 3.827f, 4.533f), Rot = new Vector3(356.192f, 62.438f, 359.879f) },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(-5.307f, 3.628f, 3.148f), Rot = new Vector3(356.192f, 62.438f, 359.879f) },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(6.942f, 3.995f, -7.380f), Rot = new Vector3(356.192f, 62.438f, 359.879f) },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(1.636f, 3.596f, -10.149f), Rot = new Vector3(356.192f, 62.438f, 359.879f) },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(8.775f, 6.972f, -11.383f), Rot = new Vector3(356.682f, 90.087f, 358.127f) },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(5.802f, 6.837f, -12.096f), Rot = new Vector3(356.192f, 62.438f, 359.879f) },
            new Prefab { Path = "assets/prefabs/building core/wall.frame/wall.frame.prefab", Pos = new Vector3(3.518f, 6.575f, -14.107f), Rot = new Vector3(356.574f, 34.639f, 1.668f) },
            //wall.frame.netting
            new Prefab { Path = "assets/prefabs/building/wall.frame.netting/wall.frame.netting.prefab", Pos = new Vector3(6.942f, 3.995f, -7.380f), Rot = new Vector3(356.192f, 62.438f, 359.879f) },
            new Prefab { Path = "assets/prefabs/building/wall.frame.netting/wall.frame.netting.prefab", Pos = new Vector3(1.636f, 3.596f, -10.149f), Rot = new Vector3(356.192f, 62.438f, 359.879f) },
            //wall.frame.fence
            new Prefab { Path = "assets/prefabs/building/wall.frame.fence/wall.frame.fence.prefab", Pos = new Vector3(8.775f, 6.972f, -11.383f), Rot = new Vector3(356.682f, 90.087f, 358.127f) },
            new Prefab { Path = "assets/prefabs/building/wall.frame.fence/wall.frame.fence.prefab", Pos = new Vector3(3.518f, 6.575f, -14.107f), Rot = new Vector3(356.574f, 34.639f, 1.668f) },
            //wall.frame.fence.gate
            new Prefab { Path = "assets/prefabs/building/wall.frame.fence/wall.frame.fence.gate.prefab", Pos = new Vector3(5.802f, 6.837f, -12.096f), Rot = new Vector3(3.808f, 242.438f, 0.121f) },
            //barricade.concrete
            new Prefab { Path = "assets/prefabs/deployable/barricades/barricade.concrete.prefab", Pos = new Vector3(3.449f, 7.171f, 2.634f), Rot = new Vector3(356.192f, 62.438f, 359.879f) },
            new Prefab { Path = "assets/prefabs/deployable/barricades/barricade.concrete.prefab", Pos = new Vector3(6.226f, 7.158f, -2.684f), Rot = new Vector3(356.192f, 62.438f, 359.879f) },
            new Prefab { Path = "assets/prefabs/deployable/barricades/barricade.concrete.prefab", Pos = new Vector3(-4.942f, 6.541f, -1.745f), Rot = new Vector3(3.808f, 242.438f, 0.121f) },
            new Prefab { Path = "assets/prefabs/deployable/barricades/barricade.concrete.prefab", Pos = new Vector3(-2.166f, 6.547f, -7.064f), Rot = new Vector3(3.808f, 242.438f, 0.121f) },
            new Prefab { Path = "assets/prefabs/deployable/barricades/barricade.concrete.prefab", Pos = new Vector3(1.395f, 6.594f, -11.747f), Rot = new Vector3(3.358f, 212.381f, 358.199f) },
            new Prefab { Path = "assets/prefabs/deployable/barricades/barricade.concrete.prefab", Pos = new Vector3(1.569f, 3.587f, -11.649f), Rot = new Vector3(3.358f, 212.381f, 358.199f) },
            new Prefab { Path = "assets/prefabs/deployable/barricades/barricade.concrete.prefab", Pos = new Vector3(8.028f, 7.092f, -8.287f), Rot = new Vector3(356.763f, 92.491f, 357.989f) },
            new Prefab { Path = "assets/prefabs/deployable/barricades/barricade.concrete.prefab", Pos = new Vector3(8.203f, 4.085f, -8.189f), Rot = new Vector3(356.763f, 92.491f, 357.989f) },
            new Prefab { Path = "assets/prefabs/deployable/barricades/barricade.concrete.prefab", Pos = new Vector3(6.694f, 3.829f, -13.373f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/deployable/barricades/barricade.concrete.prefab", Pos = new Vector3(4.227f, 6.845f, -9.091f), Rot = new Vector3(359.879f, 332.430f, 3.808f) },
            //barricade.stone
            new Prefab { Path = "assets/prefabs/deployable/barricades/barricade.stone.prefab", Pos = new Vector3(9.691f, 7.018f, -13.690f), Rot = new Vector3(357.993f, 2.377f, 3.239f) },
            new Prefab { Path = "assets/prefabs/deployable/barricades/barricade.stone.prefab", Pos = new Vector3(9.759f, 7.075f, -12.068f), Rot = new Vector3(357.993f, 2.377f, 3.239f) },
            new Prefab { Path = "assets/prefabs/deployable/barricades/barricade.stone.prefab", Pos = new Vector3(8.527f, 7.005f, -12.015f), Rot = new Vector3(357.993f, 2.377f, 3.239f) },
            new Prefab { Path = "assets/prefabs/deployable/barricades/barricade.stone.prefab", Pos = new Vector3(4.891f, 6.664f, -16.200f), Rot = new Vector3(356.642f, 32.381f, 1.802f) },
            new Prefab { Path = "assets/prefabs/deployable/barricades/barricade.stone.prefab", Pos = new Vector3(3.517f, 6.613f, -15.328f), Rot = new Vector3(356.642f, 32.381f, 1.802f) },
            new Prefab { Path = "assets/prefabs/deployable/barricades/barricade.stone.prefab", Pos = new Vector3(4.180f, 6.685f, -14.282f), Rot = new Vector3(356.642f, 32.381f, 1.802f) },
            //electric.flasherlight.deployed
            new Prefab { Path = "assets/prefabs/deployable/playerioents/lights/flasherlight/electric.flasherlight.deployed.prefab", Pos = new Vector3(-3.644f, 7.036f, 12.468f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/lights/flasherlight/electric.flasherlight.deployed.prefab", Pos = new Vector3(-6.298f, 6.836f, 11.083f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/lights/flasherlight/electric.flasherlight.deployed.prefab", Pos = new Vector3(-8.952f, 6.637f, 9.698f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/lights/flasherlight/electric.flasherlight.deployed.prefab", Pos = new Vector3(-4.910f, 6.830f, 8.424f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/lights/flasherlight/electric.flasherlight.deployed.prefab", Pos = new Vector3(-0.867f, 7.023f, 7.150f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/lights/flasherlight/electric.flasherlight.deployed.prefab", Pos = new Vector3(-3.521f, 6.824f, 5.765f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/lights/flasherlight/electric.flasherlight.deployed.prefab", Pos = new Vector3(-6.175f, 6.624f, 4.380f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            //furnace.large
            new Prefab { Path = "assets/prefabs/deployable/furnace.large/furnace.large.prefab", Pos = new Vector3(-2.380f, 3.041f, 16.212f), Rot = new Vector3(3.808f, 242.438f, 270.121f) },
            new Prefab { Path = "assets/prefabs/deployable/furnace.large/furnace.large.prefab", Pos = new Vector3(-12.299f, 2.296f, 11.035f), Rot = new Vector3(3.808f, 242.438f, 270.121f) },
            //sign.pole.banner.large
            new Prefab { Path = "assets/prefabs/deployable/signs/sign.pole.banner.large.prefab", Pos = new Vector3(0.708f, 7.990f, -2.510f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            //sign.pictureframe.tall
            new Prefab { Path = "assets/prefabs/deployable/signs/sign.pictureframe.tall.prefab", Pos = new Vector3(8.591f, 2.373f, -13.985f), Rot = new Vector3(320.154f, 327.277f, 95.161f) },
            //roof
            new Prefab { Path = "assets/prefabs/building core/roof/roof.prefab", Pos = new Vector3(-8.778f, 3.644f, 9.796f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/roof/roof.prefab", Pos = new Vector3(-6.124f, 3.843f, 11.181f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/roof/roof.prefab", Pos = new Vector3(-3.471f, 4.042f, 12.566f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            new Prefab { Path = "assets/prefabs/building core/roof/roof.prefab", Pos = new Vector3(-10.043f, 3.438f, 5.752f), Rot = new Vector3(3.808f, 242.438f, 180.121f) },
            new Prefab { Path = "assets/prefabs/building core/roof/roof.prefab", Pos = new Vector3(-8.655f, 3.432f, 3.092f), Rot = new Vector3(3.808f, 242.438f, 180.121f) },
            new Prefab { Path = "assets/prefabs/building core/roof/roof.prefab", Pos = new Vector3(-7.266f, 3.426f, 0.433f), Rot = new Vector3(3.808f, 242.438f, 180.121f) },
            new Prefab { Path = "assets/prefabs/building core/roof/roof.prefab", Pos = new Vector3(-5.878f, 3.419f, -2.226f), Rot = new Vector3(3.808f, 242.438f, 180.121f) },
            new Prefab { Path = "assets/prefabs/building core/roof/roof.prefab", Pos = new Vector3(-4.489f, 3.413f, -4.886f), Rot = new Vector3(3.808f, 242.438f, 180.121f) },
            new Prefab { Path = "assets/prefabs/building core/roof/roof.prefab", Pos = new Vector3(-3.101f, 3.407f, -7.545f), Rot = new Vector3(3.808f, 242.438f, 180.121f) },
            new Prefab { Path = "assets/prefabs/building core/roof/roof.prefab", Pos = new Vector3(-1.712f, 3.400f, -10.204f), Rot = new Vector3(3.808f, 242.438f, 180.121f) },
            new Prefab { Path = "assets/prefabs/building core/roof/roof.prefab", Pos = new Vector3(0.571f, 4.235f, 11.292f), Rot = new Vector3(356.192f, 62.438f, 179.879f) },
            new Prefab { Path = "assets/prefabs/building core/roof/roof.prefab", Pos = new Vector3(1.960f, 4.229f, 8.633f), Rot = new Vector3(356.192f, 62.438f, 179.879f) },
            new Prefab { Path = "assets/prefabs/building core/roof/roof.prefab", Pos = new Vector3(3.348f, 4.222f, 5.973f), Rot = new Vector3(356.192f, 62.438f, 179.879f) },
            new Prefab { Path = "assets/prefabs/building core/roof/roof.prefab", Pos = new Vector3(4.737f, 4.216f, 3.314f), Rot = new Vector3(356.192f, 62.438f, 179.879f) },
            new Prefab { Path = "assets/prefabs/building core/roof/roof.prefab", Pos = new Vector3(6.125f, 4.210f, 0.655f), Rot = new Vector3(356.192f, 62.438f, 179.879f) },
            new Prefab { Path = "assets/prefabs/building core/roof/roof.prefab", Pos = new Vector3(7.514f, 4.204f, -2.005f), Rot = new Vector3(356.192f, 62.438f, 179.879f) },
            new Prefab { Path = "assets/prefabs/building core/roof/roof.prefab", Pos = new Vector3(8.902f, 4.197f, -4.664f), Rot = new Vector3(356.192f, 62.438f, 179.879f) },
            //hotairballoon
            new Prefab { Path = "assets/prefabs/deployable/hot air balloon/hotairballoon.prefab", Pos = new Vector3(6.952f, 6.863f, -14.305f), Rot = new Vector3(359.880f, 332.430f, 3.808f) },
            new Prefab { Path = "assets/prefabs/deployable/hot air balloon/hotairballoon.prefab", Pos = new Vector3(-7.691f, 6.930f, 13.740f), Rot = new Vector3(0.121f, 152.430f, 356.192f) },
            //roof.triangle
            new Prefab { Path = "assets/prefabs/building core/roof.triangle/roof.triangle.prefab", Pos = new Vector3(-0.558f, 3.402f, -12.210f), Rot = new Vector3(3.358f, 212.381f, 178.199f) },
            new Prefab { Path = "assets/prefabs/building core/roof.triangle/roof.triangle.prefab", Pos = new Vector3(1.953f, 3.495f, -13.828f), Rot = new Vector3(3.358f, 212.381f, 178.199f) },
            new Prefab { Path = "assets/prefabs/building core/roof.triangle/roof.triangle.prefab", Pos = new Vector3(4.469f, 3.588f, -15.430f), Rot = new Vector3(3.358f, 212.381f, 178.199f) },
            new Prefab { Path = "assets/prefabs/building core/roof.triangle/roof.triangle.prefab", Pos = new Vector3(6.089f, 3.682f, -15.441f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/roof.triangle/roof.triangle.prefab", Pos = new Vector3(8.739f, 3.881f, -14.058f), Rot = new Vector3(0.121f, 152.430f, 176.192f) },
            new Prefab { Path = "assets/prefabs/building core/roof.triangle/roof.triangle.prefab", Pos = new Vector3(9.623f, 3.975f, -12.732f), Rot = new Vector3(356.763f, 92.491f, 177.990f) },
            new Prefab { Path = "assets/prefabs/building core/roof.triangle/roof.triangle.prefab", Pos = new Vector3(9.763f, 4.081f, -9.749f), Rot = new Vector3(356.763f, 92.491f, 177.990f) },
            new Prefab { Path = "assets/prefabs/building core/roof.triangle/roof.triangle.prefab", Pos = new Vector3(9.886f, 4.186f, -6.769f), Rot = new Vector3(356.763f, 92.491f, 177.990f) },
            //door_barricade_b
            new Prefab { Path = "assets/prefabs/deployable/door barricades/door_barricade_b.prefab", Pos = new Vector3(-4.143f, 3.622f, -2.325f), Rot = new Vector3(273.810f, 240.619f, 91.815f) },
            new Prefab { Path = "assets/prefabs/deployable/door barricades/door_barricade_b.prefab", Pos = new Vector3(3.862f, 4.228f, 1.741f), Rot = new Vector3(273.810f, 240.619f, 91.815f) },
            //cctv_deployed
            new Prefab { Path = "assets/prefabs/deployable/cctvcamera/cctv.static.prefab", Pos = new Vector3(7.688f, 6.701f, -15.680f), Rot = new Vector3(28.894f, 334.543f, 4.350f) },
            new Prefab { Path = "assets/prefabs/deployable/cctvcamera/cctv.static.prefab", Pos = new Vector3(-5.477f, 3.941f, 9.970f), Rot = new Vector3(359.879f, 332.430f, 3.808f) },
        };

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
            new Vector3(44f, 0f, 20f),
            new Vector3(44f, 0f, 18f),
            new Vector3(44f, 0f, 16f),
            new Vector3(44f, 0f, 14f),
            new Vector3(44f, 0f, 12f),
            new Vector3(44f, 0f, 10f),
            new Vector3(44f, 0f, 8f),
            new Vector3(44f, 0f, -8f),
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
            new Vector3(42f, 0f, 16f),
            new Vector3(42f, 0f, -16f),
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
            new Vector3(38f, 0f, 24f),
            new Vector3(38f, 0f, -24f),
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
            new Vector3(28f, 0f, -36f),
            new Vector3(28f, 0f, -38f),
            new Vector3(26f, 0f, 40f),
            new Vector3(26f, 0f, 38f),
            new Vector3(26f, 0f, -38f),
            new Vector3(26f, 0f, -40f),
            new Vector3(24f, 0f, 42f),
            new Vector3(24f, 0f, 40f),
            new Vector3(24f, 0f, 38f),
            new Vector3(24f, 0f, -38f),
            new Vector3(24f, 0f, -40f),
            new Vector3(24f, 0f, -42f),
            new Vector3(22f, 0f, 42f),
            new Vector3(22f, 0f, 40f),
            new Vector3(22f, 0f, 18f),
            new Vector3(22f, 0f, 16f),
            new Vector3(22f, 0f, 14f),
            new Vector3(22f, 0f, 12f),
            new Vector3(22f, 0f, 10f),
            new Vector3(22f, 0f, 8f),
            new Vector3(22f, 0f, 6f),
            new Vector3(22f, 0f, 4f),
            new Vector3(22f, 0f, 2f),
            new Vector3(22f, 0f, -40f),
            new Vector3(22f, 0f, -42f),
            new Vector3(20f, 0f, 44f),
            new Vector3(20f, 0f, 42f),
            new Vector3(20f, 0f, 22f),
            new Vector3(20f, 0f, 20f),
            new Vector3(20f, 0f, 18f),
            new Vector3(20f, 0f, 16f),
            new Vector3(20f, 0f, 14f),
            new Vector3(20f, 0f, 12f),
            new Vector3(20f, 0f, 10f),
            new Vector3(20f, 0f, 8f),
            new Vector3(20f, 0f, 6f),
            new Vector3(20f, 0f, 4f),
            new Vector3(20f, 0f, 2f),
            new Vector3(20f, 0f, 0f),
            new Vector3(20f, 0f, -2f),
            new Vector3(20f, 0f, -42f),
            new Vector3(20f, 0f, -44f),
            new Vector3(18f, 0f, 44f),
            new Vector3(18f, 0f, 42f),
            new Vector3(18f, 0f, 24f),
            new Vector3(18f, 0f, 22f),
            new Vector3(18f, 0f, 20f),
            new Vector3(18f, 0f, 18f),
            new Vector3(18f, 0f, 16f),
            new Vector3(18f, 0f, 14f),
            new Vector3(18f, 0f, 12f),
            new Vector3(18f, 0f, 8f),
            new Vector3(18f, 0f, 6f),
            new Vector3(18f, 0f, 4f),
            new Vector3(18f, 0f, 2f),
            new Vector3(18f, 0f, 0f),
            new Vector3(18f, 0f, -2f),
            new Vector3(18f, 0f, -4f),
            new Vector3(18f, 0f, -6f),
            new Vector3(18f, 0f, -42f),
            new Vector3(18f, 0f, -44f),
            new Vector3(16f, 0f, 46f),
            new Vector3(16f, 0f, 44f),
            new Vector3(16f, 0f, 42f),
            new Vector3(16f, 0f, 26f),
            new Vector3(16f, 0f, 24f),
            new Vector3(16f, 0f, 22f),
            new Vector3(16f, 0f, 20f),
            new Vector3(16f, 0f, 2f),
            new Vector3(16f, 0f, 0f),
            new Vector3(16f, 0f, -2f),
            new Vector3(16f, 0f, -4f),
            new Vector3(16f, 0f, -6f),
            new Vector3(16f, 0f, -8f),
            new Vector3(16f, 0f, -42f),
            new Vector3(16f, 0f, -44f),
            new Vector3(14f, 0f, 46f),
            new Vector3(14f, 0f, 44f),
            new Vector3(14f, 0f, 28f),
            new Vector3(14f, 0f, 26f),
            new Vector3(14f, 0f, 24f),
            new Vector3(14f, 0f, 22f),
            new Vector3(14f, 0f, 18f),
            new Vector3(14f, 0f, 16f),
            new Vector3(14f, 0f, 14f),
            new Vector3(14f, 0f, 12f),
            new Vector3(14f, 0f, 10f),
            new Vector3(14f, 0f, 8f),
            new Vector3(14f, 0f, -2f),
            new Vector3(14f, 0f, -4f),
            new Vector3(14f, 0f, -6f),
            new Vector3(14f, 0f, -8f),
            new Vector3(14f, 0f, -10f),
            new Vector3(14f, 0f, -44f),
            new Vector3(14f, 0f, -46f),
            new Vector3(12f, 0f, 46f),
            new Vector3(12f, 0f, 44f),
            new Vector3(12f, 0f, 28f),
            new Vector3(12f, 0f, 26f),
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
            new Vector3(12f, 0f, -6f),
            new Vector3(12f, 0f, -8f),
            new Vector3(12f, 0f, -10f),
            new Vector3(12f, 0f, -12f),
            new Vector3(12f, 0f, -44f),
            new Vector3(12f, 0f, -46f),
            new Vector3(10f, 0f, 46f),
            new Vector3(10f, 0f, 44f),
            new Vector3(10f, 0f, 30f),
            new Vector3(10f, 0f, 28f),
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
            new Vector3(10f, 0f, -44f),
            new Vector3(10f, 0f, -46f),
            new Vector3(8f, 0f, 48f),
            new Vector3(8f, 0f, 46f),
            new Vector3(8f, 0f, 44f),
            new Vector3(8f, 0f, 30f),
            new Vector3(8f, 0f, 28f),
            new Vector3(8f, 0f, 26f),
            new Vector3(8f, 0f, 24f),
            new Vector3(8f, 0f, 22f),
            new Vector3(8f, 0f, 20f),
            new Vector3(8f, 0f, 6f),
            new Vector3(8f, 0f, 4f),
            new Vector3(8f, 0f, 2f),
            new Vector3(8f, 0f, 0f),
            new Vector3(8f, 0f, -2f),
            new Vector3(8f, 0f, -4f),
            new Vector3(8f, 0f, -6f),
            new Vector3(8f, 0f, -8f),
            new Vector3(8f, 0f, -10f),
            new Vector3(8f, 0f, -12f),
            new Vector3(8f, 0f, -14f),
            new Vector3(8f, 0f, -16f),
            new Vector3(8f, 0f, -44f),
            new Vector3(8f, 0f, -46f),
            new Vector3(8f, 0f, -48f),
            new Vector3(6f, 0f, 48f),
            new Vector3(6f, 0f, 46f),
            new Vector3(6f, 0f, 32f),
            new Vector3(6f, 0f, 30f),
            new Vector3(6f, 0f, 28f),
            new Vector3(6f, 0f, 26f),
            new Vector3(6f, 0f, 24f),
            new Vector3(6f, 0f, -2f),
            new Vector3(6f, 0f, -4f),
            new Vector3(6f, 0f, -6f),
            new Vector3(6f, 0f, -8f),
            new Vector3(6f, 0f, -10f),
            new Vector3(6f, 0f, -12f),
            new Vector3(6f, 0f, -14f),
            new Vector3(6f, 0f, -16f),
            new Vector3(6f, 0f, -18f),
            new Vector3(6f, 0f, -46f),
            new Vector3(6f, 0f, -48f),
            new Vector3(4f, 0f, 48f),
            new Vector3(4f, 0f, 46f),
            new Vector3(4f, 0f, 32f),
            new Vector3(4f, 0f, 30f),
            new Vector3(4f, 0f, 28f),
            new Vector3(4f, 0f, 26f),
            new Vector3(4f, 0f, -8f),
            new Vector3(4f, 0f, -10f),
            new Vector3(4f, 0f, -12f),
            new Vector3(4f, 0f, -14f),
            new Vector3(4f, 0f, -16f),
            new Vector3(4f, 0f, -18f),
            new Vector3(4f, 0f, -24f),
            new Vector3(4f, 0f, -26f),
            new Vector3(4f, 0f, -28f),
            new Vector3(4f, 0f, -30f),
            new Vector3(4f, 0f, -32f),
            new Vector3(4f, 0f, -46f),
            new Vector3(4f, 0f, -48f),
            new Vector3(2f, 0f, 48f),
            new Vector3(2f, 0f, 46f),
            new Vector3(2f, 0f, 32f),
            new Vector3(2f, 0f, 30f),
            new Vector3(2f, 0f, 28f),
            new Vector3(2f, 0f, -12f),
            new Vector3(2f, 0f, -14f),
            new Vector3(2f, 0f, -16f),
            new Vector3(2f, 0f, -18f),
            new Vector3(2f, 0f, -24f),
            new Vector3(2f, 0f, -32f),
            new Vector3(2f, 0f, -46f),
            new Vector3(2f, 0f, -48f),
            new Vector3(0f, 0f, 48f),
            new Vector3(0f, 0f, 46f),
            new Vector3(0f, 0f, 32f),
            new Vector3(0f, 0f, 30f),
            new Vector3(0f, 0f, 28f),
            new Vector3(0f, 0f, -14f),
            new Vector3(0f, 0f, -16f),
            new Vector3(0f, 0f, -18f),
            new Vector3(0f, 0f, -24f),
            new Vector3(0f, 0f, -32f),
            new Vector3(0f, 0f, -46f),
            new Vector3(0f, 0f, -48f),
            new Vector3(-2f, 0f, 48f),
            new Vector3(-2f, 0f, 46f),
            new Vector3(-2f, 0f, 32f),
            new Vector3(-2f, 0f, 30f),
            new Vector3(-2f, 0f, 28f),
            new Vector3(-2f, 0f, -12f),
            new Vector3(-2f, 0f, -14f),
            new Vector3(-2f, 0f, -16f),
            new Vector3(-2f, 0f, -18f),
            new Vector3(-2f, 0f, -24f),
            new Vector3(-2f, 0f, -32f),
            new Vector3(-2f, 0f, -46f),
            new Vector3(-2f, 0f, -48f),
            new Vector3(-4f, 0f, 48f),
            new Vector3(-4f, 0f, 46f),
            new Vector3(-4f, 0f, 32f),
            new Vector3(-4f, 0f, 30f),
            new Vector3(-4f, 0f, 28f),
            new Vector3(-4f, 0f, 26f),
            new Vector3(-4f, 0f, -8f),
            new Vector3(-4f, 0f, -10f),
            new Vector3(-4f, 0f, -12f),
            new Vector3(-4f, 0f, -14f),
            new Vector3(-4f, 0f, -16f),
            new Vector3(-4f, 0f, -18f),
            new Vector3(-4f, 0f, -24f),
            new Vector3(-4f, 0f, -26f),
            new Vector3(-4f, 0f, -28f),
            new Vector3(-4f, 0f, -30f),
            new Vector3(-4f, 0f, -32f),
            new Vector3(-4f, 0f, -46f),
            new Vector3(-4f, 0f, -48f),
            new Vector3(-6f, 0f, 48f),
            new Vector3(-6f, 0f, 46f),
            new Vector3(-6f, 0f, 32f),
            new Vector3(-6f, 0f, 30f),
            new Vector3(-6f, 0f, 28f),
            new Vector3(-6f, 0f, 26f),
            new Vector3(-6f, 0f, 24f),
            new Vector3(-6f, 0f, -2f),
            new Vector3(-6f, 0f, -4f),
            new Vector3(-6f, 0f, -6f),
            new Vector3(-6f, 0f, -8f),
            new Vector3(-6f, 0f, -10f),
            new Vector3(-6f, 0f, -12f),
            new Vector3(-6f, 0f, -14f),
            new Vector3(-6f, 0f, -16f),
            new Vector3(-6f, 0f, -18f),
            new Vector3(-6f, 0f, -46f),
            new Vector3(-6f, 0f, -48f),
            new Vector3(-8f, 0f, 48f),
            new Vector3(-8f, 0f, 46f),
            new Vector3(-8f, 0f, 44f),
            new Vector3(-8f, 0f, 30f),
            new Vector3(-8f, 0f, 28f),
            new Vector3(-8f, 0f, 26f),
            new Vector3(-8f, 0f, 24f),
            new Vector3(-8f, 0f, 22f),
            new Vector3(-8f, 0f, 20f),
            new Vector3(-8f, 0f, 6f),
            new Vector3(-8f, 0f, 4f),
            new Vector3(-8f, 0f, 2f),
            new Vector3(-8f, 0f, 0f),
            new Vector3(-8f, 0f, -2f),
            new Vector3(-8f, 0f, -4f),
            new Vector3(-8f, 0f, -6f),
            new Vector3(-8f, 0f, -8f),
            new Vector3(-8f, 0f, -10f),
            new Vector3(-8f, 0f, -12f),
            new Vector3(-8f, 0f, -14f),
            new Vector3(-8f, 0f, -16f),
            new Vector3(-8f, 0f, -44f),
            new Vector3(-8f, 0f, -46f),
            new Vector3(-8f, 0f, -48f),
            new Vector3(-10f, 0f, 46f),
            new Vector3(-10f, 0f, 44f),
            new Vector3(-10f, 0f, 30f),
            new Vector3(-10f, 0f, 28f),
            new Vector3(-10f, 0f, 26f),
            new Vector3(-10f, 0f, 24f),
            new Vector3(-10f, 0f, 22f),
            new Vector3(-10f, 0f, 20f),
            new Vector3(-10f, 0f, 18f),
            new Vector3(-10f, 0f, 16f),
            new Vector3(-10f, 0f, 14f),
            new Vector3(-10f, 0f, 12f),
            new Vector3(-10f, 0f, 10f),
            new Vector3(-10f, 0f, 8f),
            new Vector3(-10f, 0f, 6f),
            new Vector3(-10f, 0f, 4f),
            new Vector3(-10f, 0f, 2f),
            new Vector3(-10f, 0f, 0f),
            new Vector3(-10f, 0f, -2f),
            new Vector3(-10f, 0f, -4f),
            new Vector3(-10f, 0f, -6f),
            new Vector3(-10f, 0f, -8f),
            new Vector3(-10f, 0f, -10f),
            new Vector3(-10f, 0f, -12f),
            new Vector3(-10f, 0f, -14f),
            new Vector3(-10f, 0f, -44f),
            new Vector3(-10f, 0f, -46f),
            new Vector3(-12f, 0f, 46f),
            new Vector3(-12f, 0f, 44f),
            new Vector3(-12f, 0f, 28f),
            new Vector3(-12f, 0f, 26f),
            new Vector3(-12f, 0f, 24f),
            new Vector3(-12f, 0f, 22f),
            new Vector3(-12f, 0f, 20f),
            new Vector3(-12f, 0f, 18f),
            new Vector3(-12f, 0f, 16f),
            new Vector3(-12f, 0f, 14f),
            new Vector3(-12f, 0f, 12f),
            new Vector3(-12f, 0f, 10f),
            new Vector3(-12f, 0f, 8f),
            new Vector3(-12f, 0f, 6f),
            new Vector3(-12f, 0f, 4f),
            new Vector3(-12f, 0f, 2f),
            new Vector3(-12f, 0f, 0f),
            new Vector3(-12f, 0f, -2f),
            new Vector3(-12f, 0f, -6f),
            new Vector3(-12f, 0f, -8f),
            new Vector3(-12f, 0f, -10f),
            new Vector3(-12f, 0f, -12f),
            new Vector3(-12f, 0f, -44f),
            new Vector3(-12f, 0f, -46f),
            new Vector3(-14f, 0f, 46f),
            new Vector3(-14f, 0f, 44f),
            new Vector3(-14f, 0f, 28f),
            new Vector3(-14f, 0f, 26f),
            new Vector3(-14f, 0f, 24f),
            new Vector3(-14f, 0f, 22f),
            new Vector3(-14f, 0f, 18f),
            new Vector3(-14f, 0f, 16f),
            new Vector3(-14f, 0f, 14f),
            new Vector3(-14f, 0f, 12f),
            new Vector3(-14f, 0f, 10f),
            new Vector3(-14f, 0f, 8f),
            new Vector3(-14f, 0f, -2f),
            new Vector3(-14f, 0f, -4f),
            new Vector3(-14f, 0f, -6f),
            new Vector3(-14f, 0f, -8f),
            new Vector3(-14f, 0f, -10f),
            new Vector3(-14f, 0f, -44f),
            new Vector3(-14f, 0f, -46f),
            new Vector3(-16f, 0f, 46f),
            new Vector3(-16f, 0f, 44f),
            new Vector3(-16f, 0f, 42f),
            new Vector3(-16f, 0f, 26f),
            new Vector3(-16f, 0f, 24f),
            new Vector3(-16f, 0f, 22f),
            new Vector3(-16f, 0f, 20f),
            new Vector3(-16f, 0f, 2f),
            new Vector3(-16f, 0f, 0f),
            new Vector3(-16f, 0f, -2f),
            new Vector3(-16f, 0f, -4f),
            new Vector3(-16f, 0f, -6f),
            new Vector3(-16f, 0f, -8f),
            new Vector3(-16f, 0f, -42f),
            new Vector3(-16f, 0f, -44f),
            new Vector3(-18f, 0f, 44f),
            new Vector3(-18f, 0f, 42f),
            new Vector3(-18f, 0f, 24f),
            new Vector3(-18f, 0f, 22f),
            new Vector3(-18f, 0f, 20f),
            new Vector3(-18f, 0f, 18f),
            new Vector3(-18f, 0f, 16f),
            new Vector3(-18f, 0f, 14f),
            new Vector3(-18f, 0f, 8f),
            new Vector3(-18f, 0f, 6f),
            new Vector3(-18f, 0f, 4f),
            new Vector3(-18f, 0f, 2f),
            new Vector3(-18f, 0f, 0f),
            new Vector3(-18f, 0f, -2f),
            new Vector3(-18f, 0f, -4f),
            new Vector3(-18f, 0f, -6f),
            new Vector3(-18f, 0f, -42f),
            new Vector3(-18f, 0f, -44f),
            new Vector3(-20f, 0f, 44f),
            new Vector3(-20f, 0f, 42f),
            new Vector3(-20f, 0f, 22f),
            new Vector3(-20f, 0f, 20f),
            new Vector3(-20f, 0f, 18f),
            new Vector3(-20f, 0f, 16f),
            new Vector3(-20f, 0f, 14f),
            new Vector3(-20f, 0f, 12f),
            new Vector3(-20f, 0f, 10f),
            new Vector3(-20f, 0f, 8f),
            new Vector3(-20f, 0f, 6f),
            new Vector3(-20f, 0f, 4f),
            new Vector3(-20f, 0f, 2f),
            new Vector3(-20f, 0f, 0f),
            new Vector3(-20f, 0f, -2f),
            new Vector3(-20f, 0f, -42f),
            new Vector3(-20f, 0f, -44f),
            new Vector3(-22f, 0f, 42f),
            new Vector3(-22f, 0f, 40f),
            new Vector3(-22f, 0f, 18f),
            new Vector3(-22f, 0f, 16f),
            new Vector3(-22f, 0f, 14f),
            new Vector3(-22f, 0f, 12f),
            new Vector3(-22f, 0f, 10f),
            new Vector3(-22f, 0f, 8f),
            new Vector3(-22f, 0f, 6f),
            new Vector3(-22f, 0f, 4f),
            new Vector3(-22f, 0f, 2f),
            new Vector3(-22f, 0f, -40f),
            new Vector3(-22f, 0f, -42f),
            new Vector3(-24f, 0f, 42f),
            new Vector3(-24f, 0f, 40f),
            new Vector3(-24f, 0f, 38f),
            new Vector3(-24f, 0f, -38f),
            new Vector3(-24f, 0f, -40f),
            new Vector3(-24f, 0f, -42f),
            new Vector3(-26f, 0f, 40f),
            new Vector3(-26f, 0f, 38f),
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
            new Vector3(-38f, 0f, -30f),
            new Vector3(-40f, 0f, 26f),
            new Vector3(-40f, 0f, 24f),
            new Vector3(-40f, 0f, 22f),
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
            new Vector3(-46f, 0f, 16f),
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
            new Vector3(-46f, 0f, -16f),
            new Vector3(-48f, 0f, 8f),
            new Vector3(-48f, 0f, 6f),
            new Vector3(-48f, 0f, 4f),
            new Vector3(-48f, 0f, 2f),
            new Vector3(-48f, 0f, 0f),
            new Vector3(-48f, 0f, -2f),
            new Vector3(-48f, 0f, -4f),
            new Vector3(-48f, 0f, -6f),
            new Vector3(-48f, 0f, -8)
        };

        private ControllerAirEvent Controller { get; set; } = null;
        private bool Active { get; set; } = false;

        private void StartTimer()
        {
            if (!_config.EnabledTimer) return;
            timer.In(UnityEngine.Random.Range(_config.MinStartTime, _config.MaxStartTime), () =>
            {
                if (!Active) Start(null);
                else Puts("This event is active now. To finish this event (airstop), then to start the next one");
            });
        }

        private void Start(BasePlayer player)
        {
            if (!PluginExistsForStart("NpcSpawn")) return;
            CheckVersionPlugin();
            Active = true;
            AlertToAllPlayers("PreStart", _config.Chat.Prefix, GetTimeFormat((int)_config.PreStartTime));
            timer.In(_config.PreStartTime - 10f <= 0f ? 0f : _config.PreStartTime - 10f, () =>
            {
                Puts($"{Name} has begun");
                CalculateSpawnPos();
                if (_config.IsSmoke)
                {
                    Vector3 pos = new Vector3(SpawnPos.x, SpawnPos.y + 10f, SpawnPos.z);
                    for (int i = 0; i < 100; i++)
                    {
                        SmokeGrenade grenade = GameManager.server.CreateEntity("assets/prefabs/tools/smoke grenade/grenade.smoke.deployed.prefab", pos + UnityEngine.Random.insideUnitSphere * 25f) as SmokeGrenade;
                        grenade.enableSaving = false;
                        grenade.Spawn();
                        grenade.GetComponent<Rigidbody>().useGravity = false;
                    }
                }
                timer.In(10f, () =>
                {
                    ToggleHooks(true);
                    Controller = new GameObject().AddComponent<ControllerAirEvent>();
                    Controller.Init(player);
                    AlertToAllPlayers("Start", _config.Chat.Prefix, MapHelper.GridToString(MapHelper.PositionToGrid(SpawnPos)), (int)SpawnPos.y, _config.Cctv1, _config.Cctv2);
                });
            });
        }

        private void Finish()
        {
            ToggleHooks(false);
            if (ActivePveMode) PveMode.Call("EventRemovePveMode", Name, true);
            if (Controller != null) UnityEngine.Object.Destroy(Controller.gameObject);
            Active = false;
            SendBalance();
            LootableCrates.Clear();
            AlertToAllPlayers("Finish", _config.Chat.Prefix);
            Interface.Oxide.CallHook($"On{Name}End");
            Puts($"{Name} has ended");
            StartTimer();
        }

        internal class ControllerAirEvent : FacepunchBehaviour
        {
            private PluginConfig _config => _ins._config;

            private SphereCollider SphereCollider { get; set; } = null;

            private VendingMachineMapMarker VendingMarker { get; set; } = null;
            private HashSet<MapMarkerGenericRadius> Markers { get; } = new HashSet<MapMarkerGenericRadius>();

            internal bool KillEntities { get; set; } = false;
            internal HashSet<BaseEntity> Entities { get; } = new HashSet<BaseEntity>();
            internal HashSet<HotAirBalloon> AirBalloons { get; } = new HashSet<HotAirBalloon>();

            private Coroutine SpawnEntitiesCoroutine { get; set; } = null;

            internal HashSet<LootContainer> Crates { get; } = new HashSet<LootContainer>();
            internal HashSet<HackableLockedCrate> HackCrates { get; } = new HashSet<HackableLockedCrate>();

            internal HashSet<DroppedItemContainer> Backpacks { get; } = new HashSet<DroppedItemContainer>();
            internal HashSet<ScientistNPC> Scientists { get; } = new HashSet<ScientistNPC>();

            internal int TimeToFinish { get; set; } = _ins._config.FinishTime;

            internal HashSet<BasePlayer> Players { get; } = new HashSet<BasePlayer>();
            internal BasePlayer Owner { get; set; } = null;

            private void Awake()
            {
                transform.position = _ins.SpawnPos;
                transform.rotation = Quaternion.Euler(new Vector3(1.860f, 27.461f, 356.719f));

                gameObject.layer = 3;
                SphereCollider = gameObject.AddComponent<SphereCollider>();
                SphereCollider.isTrigger = true;
                SphereCollider.radius = _config.Radius;
            }

            internal void Init(BasePlayer player)
            {
                SpawnEntitiesCoroutine = ServerMgr.Instance.StartCoroutine(SpawnEntities(player));
            }

            private void OnDestroy()
            {
                if (SpawnEntitiesCoroutine != null) ServerMgr.Instance.StopCoroutine(SpawnEntitiesCoroutine);
                CancelInvoke(InvokeUpdates);

                if (SphereCollider != null) Destroy(SphereCollider);

                if (VendingMarker.IsExists()) VendingMarker.Kill();
                foreach (MapMarkerGenericRadius marker in Markers) if (marker.IsExists()) marker.Kill();

                foreach (BasePlayer player in Players) CuiHelper.DestroyUi(player, "Tabs_KpucTaJl");

                foreach (ScientistNPC npc in Scientists) if (npc.IsExists()) npc.Kill();

                foreach (DroppedItemContainer backpack in Backpacks) if (backpack.IsExists()) backpack.Kill();

                foreach (LootContainer crate in Crates) if (crate.IsExists()) crate.Kill();
                foreach (HackableLockedCrate hackCrate in HackCrates) if (hackCrate.IsExists()) hackCrate.Kill();

                KillEntities = true;
                foreach (BaseEntity entity in Entities)
                {
                    if (entity is HotAirBalloon)
                    {
                        HotAirBalloon airBalloon = entity as HotAirBalloon;
                        if (Players.Any(x => Vector3.Distance(airBalloon.transform.position, x.transform.position) < 1f))
                        {
                            EntityFuelSystem fuelSystem = airBalloon.GetFuelSystem() as EntityFuelSystem;
                            fuelSystem.GetFuelContainer().SetFlag(BaseEntity.Flags.Locked, false);
                            airBalloon.myRigidbody.isKinematic = false;
                        }
                        else if (airBalloon.IsExists()) airBalloon.Kill();
                    }
                    else if (entity.IsExists()) entity.Kill();
                }
            }

            private void FixedUpdate()
            {
                foreach (HotAirBalloon airBalloon in AirBalloons)
                {
                    airBalloon.inflationLevel = 0.9f;
                    airBalloon.myRigidbody.isKinematic = true;
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
                if (Crates.Count + HackCrates.Count > 0) dic.Add("Crate_KpucTaJl", $"{Crates.Count + HackCrates.Count}");
                if (Scientists.Count > 0) dic.Add("Npc_KpucTaJl", Scientists.Count.ToString());
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
                if (Players.Count == 0) return;

                if (_config.MainPoint.Enabled && HackCrates.Count > 0)
                {
                    HashSet<Vector3> points = new HashSet<Vector3>();
                    foreach (HackableLockedCrate crate in HackCrates) if (crate.IsExists()) points.Add(crate.transform.position);
                    if (points.Count > 0) foreach (BasePlayer player in Players) foreach (Vector3 point in points) UpdateMarkerForPlayer(player, point, _config.MainPoint);
                    points = null;
                }

                if (_config.AdditionalPoint.Enabled && Crates.Count > 0)
                {
                    HashSet<Vector3> points = new HashSet<Vector3>();
                    foreach (LootContainer crate in Crates) if (crate.IsExists()) points.Add(crate.transform.position);
                    if (points.Count > 0) foreach (BasePlayer player in Players) foreach (Vector3 point in points) UpdateMarkerForPlayer(player, point, _config.AdditionalPoint);
                    points = null;
                }
            }

            private void UpdateTimeToFinish()
            {
                TimeToFinish--;
                if ((TimeToFinish == _config.PreFinishTime || Crates.Count + HackCrates.Count == 0) && TimeToFinish >= _config.PreFinishTime)
                {
                    TimeToFinish = _config.PreFinishTime;
                    _ins.AlertToAllPlayers("PreFinish", _config.Chat.Prefix, GetTimeFormat(_config.PreFinishTime));
                }
                else if (TimeToFinish == 0)
                {
                    CancelInvoke(InvokeUpdates);
                    _ins.Finish();
                }
            }

            private Vector3 GetGlobalPosition(Vector3 local) => transform.TransformPoint(local);

            private Quaternion GetGlobalRotation(Vector3 local) => transform.rotation * Quaternion.Euler(local);

            private Vector3 GetLocalPosition(Vector3 global) => transform.InverseTransformPoint(global);

            private IEnumerator SpawnEntities(BasePlayer player)
            {
                SpawnDiveSite();

                foreach (Prefab prefab in _ins.Prefabs)
                {
                    BaseEntity entity = SpawnEntity(prefab.Path, GetGlobalPosition(prefab.Pos), GetGlobalRotation(prefab.Rot));

                    if (entity is BuildingBlock)
                    {
                        BuildingBlock buildingBlock = entity as BuildingBlock;
                        buildingBlock.ChangeGradeAndSkin(BuildingGrade.Enum.Metal, 0);
                    }

                    if (entity is FlasherLight) (entity as FlasherLight).UpdateFromInput(1, 0);

                    if (entity is BaseOven)
                    {
                        BaseOven oven = entity as BaseOven;
                        oven.SetFlag(BaseEntity.Flags.On, true);
                        oven.SetFlag(BaseEntity.Flags.Locked, true);
                    }

                    if (entity is HotAirBalloon)
                    {
                        HotAirBalloon airBalloon = entity as HotAirBalloon;
                        EntityFuelSystem fuelSystem = airBalloon.GetFuelSystem() as EntityFuelSystem;
                        fuelSystem.GetFuelContainer().SetFlag(BaseEntity.Flags.Locked, true);
                        airBalloon.myRigidbody.isKinematic = true;
                        AirBalloons.Add(airBalloon);
                    }

                    if (entity is CCTV_RC)
                    {
                        CCTV_RC cctv = entity as CCTV_RC;
                        cctv.UpdateFromInput(5, 0);
                        cctv.rcIdentifier = Vector3.Distance(new Vector3(7.688f, 6.701f, -15.680f), prefab.Pos) < 1f ? _config.Cctv1 : _config.Cctv2;
                    }

                    if (entity is Signage) (entity as Signage).SetFlag(BaseEntity.Flags.Busy, true, true);

                    if (entity is Barricade && entity.ShortPrefabName == "barricade.concrete")
                    {
                        Barricade barricade = entity as Barricade;
                        barricade.skinID = 940092249;
                        barricade.SendNetworkUpdate();
                        DestroyImmediate(barricade.NpcTriggerBox);
                    }

                    if (entity is Door)
                    {
                        Door door = entity as Door;
                        door.canTakeLock = false;
                        door.canTakeCloser = false;
                        door.canTakeKnocker = false;
                        door.SetOpen(true);
                    }

                    Entities.Add(entity);

                    yield return CoroutineEx.waitForSeconds(_config.Delay);
                }

                SpawnCrates();
                SpawnHackCrate(new Vector3(5.488f, 3.882f, -11.071f), new Vector3(359.880f, 332.430f, 3.808f));
                SpawnHackCrate(new Vector3(-7.823f, 3.946f, 14.420f), new Vector3(0.121f, 152.430f, 356.192f));

                foreach (PresetConfig preset in _config.Npc) SpawnPreset(preset);

                Interface.Oxide.CallHook($"On{_ins.Name}Start", Entities, transform.position, _config.Radius);

                EnablePveMode(_config.PveMode, player);

                SpawnMapMarker(_config.Marker);

                InvokeRepeating(InvokeUpdates, 0f, 1f);
            }

            private void SpawnDiveSite()
            {
                DiveSite diveSite = GameManager.server.CreateEntity("assets/prefabs/misc/divesite/divesite_a.prefab", transform.position, transform.rotation, false) as DiveSite;
                BaseEntity newDiveSite = diveSite.gameObject.AddComponent<BaseEntity>();
                CopySerializableFields(diveSite, newDiveSite);
                DestroyImmediate(diveSite, true);
                newDiveSite.enableSaving = false;
                newDiveSite.Spawn();
                Entities.Add(newDiveSite);
            }

            private void SpawnCrates()
            {
                foreach (CrateConfig crateConfig in _config.DefaultCrates)
                {
                    LootContainer crate = GameManager.server.CreateEntity(crateConfig.Prefab, GetGlobalPosition(crateConfig.Position.ToVector3()), GetGlobalRotation(crateConfig.Rotation.ToVector3())) as LootContainer;
                    crate.enableSaving = false;
                    crate.Spawn();
                    Crates.Add(crate);
                    if (_config.TypeLootTableCrates == 1 || _config.TypeLootTableCrates == 4 || _config.TypeLootTableCrates == 5)
                    {
                        _ins.NextTick(() =>
                        {
                            crate.inventory.ClearItemsContainer();
                            if (_config.TypeLootTableCrates == 4 || _config.TypeLootTableCrates == 5) _ins.AddToContainerPrefab(crate.inventory, crateConfig.PrefabLootTable);
                            if (_config.TypeLootTableCrates == 1 || _config.TypeLootTableCrates == 5) _ins.AddToContainerItem(crate.inventory, crateConfig.OwnLootTable);
                        });
                    }
                }
            }

            private void SpawnHackCrate(Vector3 posLocal, Vector3 rotLocal)
            {
                HackableLockedCrate hackCrate = GameManager.server.CreateEntity("assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab", GetGlobalPosition(posLocal), GetGlobalRotation(rotLocal)) as HackableLockedCrate;
                hackCrate.enableSaving = false;
                hackCrate.Spawn();

                hackCrate.hackSeconds = HackableLockedCrate.requiredHackSeconds - _config.HackCrate.UnlockTime;

                hackCrate.shouldDecay = false;
                hackCrate.CancelInvoke(hackCrate.DelayedDestroy);

                hackCrate.KillMapMarker();

                HackCrates.Add(hackCrate);
                if (_config.HackCrate.TypeLootTable is 1 or 4 or 5)
                {
                    _ins.NextTick(() =>
                    {
                        hackCrate.inventory.ClearItemsContainer();
                        if (_config.HackCrate.TypeLootTable is 4 or 5) _ins.AddToContainerPrefab(hackCrate.inventory, _config.HackCrate.PrefabLootTable);
                        if (_config.HackCrate.TypeLootTable is 1 or 5) _ins.AddToContainerItem(hackCrate.inventory, _config.HackCrate.OwnLootTable);
                    });
                }
            }

            private void SpawnPreset(PresetConfig preset)
            {
                DoorCloser closer = SpawnEntity("assets/prefabs/misc/doorcloser/doorcloser.prefab", transform.position, Quaternion.identity) as DoorCloser;

                int count = UnityEngine.Random.Range(preset.Min, preset.Max + 1);

                List<Vector3> positions = Pool.Get<List<Vector3>>();
                foreach (string pos in preset.Positions) positions.Add(GetGlobalPosition(pos.ToVector3()));

                JObject config = GetObjectConfig(preset.Config);

                for (int i = 0; i < count; i++)
                {
                    Vector3 pos = positions.GetRandom();
                    positions.Remove(pos);

                    ScientistNPC npc = (ScientistNPC)_ins.NpcSpawn.Call("SpawnNpc", pos, config);
                    _ins.NpcSpawn.Call("SetCustomNavMesh", npc, closer.transform, GetLocalPosition(pos).y > 5.5f ? "AirEvent_Floor2" : "AirEvent_Floor1");

                    Scientists.Add(npc);
                }

                Pool.FreeUnmanaged(ref positions);
                if (closer.IsExists()) closer.Kill();
            }

            private static JObject GetObjectConfig(NpcConfig config)
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
                    ["CanRunAwayWater"] = false,
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

            private void EnablePveMode(PveModeConfig config, BasePlayer player)
            {
                if (!_ins.ActivePveMode) return;

                Dictionary<string, object> dic = new Dictionary<string, object>
                {
                    ["Damage"] = config.Damage,
                    ["ScaleDamage"] = config.ScaleDamage,
                    ["LootCrate"] = config.LootCrate,
                    ["HackCrate"] = config.HackCrate,
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

                HashSet<ulong> crates = Crates.Select(x => x.net.ID.Value);
                foreach (HackableLockedCrate crate in HackCrates) crates.Add(crate.net.ID.Value);

                _ins.PveMode.Call("EventAddPveMode", _ins.Name, dic, transform.position, _config.Radius, crates, Scientists.Select(x => x.net.ID.Value), new HashSet<ulong>(), new HashSet<ulong>(), new HashSet<ulong>(), new HashSet<ulong>(), player);
            }
        }
        #endregion Controller

        #region Find Position
        private const int BlockedTopology = (int)(TerrainTopology.Enum.Monument | TerrainTopology.Enum.Road | TerrainTopology.Enum.Roadside | TerrainTopology.Enum.Rail | TerrainTopology.Enum.Railside);

        private static bool IsAvailableTopology(Vector3 position) => (TerrainMeta.TopologyMap.GetTopology(position) & BlockedTopology) == 0;

        private bool IsBuildingBlock(Vector3 position)
        {
            List<BuildingBlock> list = Pool.Get<List<BuildingBlock>>();
            Vis.Entities<BuildingBlock>(position, _config.Radius, list, 1 << 21);
            bool hasEntity = list.Count > 0;
            Pool.FreeUnmanaged(ref list);
            return hasEntity;
        }

        internal Vector3 SpawnPos { get; set; } = Vector3.zero;

        private void CalculateSpawnPos()
        {
            List<Vector3> list = Pool.Get<List<Vector3>>();

            if (_config.Positions.Count > 0) foreach (string pos in _config.Positions) list.Add(pos.ToVector3());
            else
            {
                for (int i = 0; i < 100; i++)
                {
                    Vector2 random = World.Size * 0.475f * UnityEngine.Random.insideUnitCircle;
                    Vector3 center = new Vector3(random.x, _config.Height, random.y);

                    if (!IsAvailableTopology(center)) continue;

                    float height = TerrainMeta.HeightMap.GetHeight(center);
                    if (height > 0f) center.y += height;

                    if (IsBuildingBlock(center)) continue;

                    list.Add(center);
                }
            }

            SpawnPos = list.GetRandom();

            Pool.FreeUnmanaged(ref list);
        }
        #endregion Find Position

        #region Spawn Loot
        #region NPC
        private void OnCorpsePopulate(ScientistNPC entity, NPCPlayerCorpse corpse)
        {
            if (entity == null) return;
            if (!Controller.Scientists.Contains(entity)) return;
            Controller.Scientists.Remove(entity);
            PresetConfig preset = _config.Npc.FirstOrDefault(x => x.Config.Name == entity.displayName);
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
                if (preset.Config.IsRemoveCorpse && corpse.IsExists()) corpse.Kill();
            });
        }

        private object CanPopulateLoot(ScientistNPC entity, NPCPlayerCorpse corpse)
        {
            if (entity == null || Controller == null) return null;
            if (!Controller.Scientists.Contains(entity)) return null;
            PresetConfig preset = _config.Npc.FirstOrDefault(x => x.Config.Name == entity.displayName);
            if (preset == null) return null;
            if (preset.TypeLootTable == 2) return null;
            else return true;
        }

        private object OnCustomLootNPC(NetworkableId netId)
        {
            if (Controller == null) return null;
            ScientistNPC entity = Controller.Scientists.FirstOrDefault(x => x.IsExists() && x.net.ID.Value == netId.Value);
            if (entity == null) return null;
            PresetConfig preset = _config.Npc.FirstOrDefault(x => x.Config.Name == entity.displayName);
            if (preset == null) return null;
            if (preset.TypeLootTable == 3) return null;
            else return true;
        }
        #endregion NPC

        #region Crates
        private object CanPopulateLoot(LootContainer container)
        {
            if (container == null || Controller == null) return null;

            if (Controller.Crates.Contains(container))
            {
                if (_config.TypeLootTableCrates == 2) return null;
                else return true;
            }

            HackableLockedCrate lockedCrate = container as HackableLockedCrate;
            if (lockedCrate != null && Controller.HackCrates.Contains(lockedCrate))
            {
                if (_config.HackCrate.TypeLootTable == 2) return null;
                else return true;
            }

            return null;
        }

        private object OnCustomLootContainer(NetworkableId netId)
        {
            if (Controller == null) return null;

            if (Controller.Crates.Any(x => x.IsExists() && x.net.ID.Value == netId.Value))
            {
                if (_config.TypeLootTableCrates == 3) return null;
                else return true;
            }

            if (Controller.HackCrates.Any(x => x.IsExists() && x.net.ID.Value == netId.Value))
            {
                if (_config.HackCrate.TypeLootTable == 3) return null;
                else return true;
            }

            return null;
        }

        private object OnContainerPopulate(LootContainer container)
        {
            if (container == null || Controller == null) return null;

            if (Controller.Crates.Contains(container))
            {
                if (_config.TypeLootTableCrates == 6) return null;
                else return true;
            }

            HackableLockedCrate lockedCrate = container as HackableLockedCrate;
            if (lockedCrate != null && Controller.HackCrates.Contains(lockedCrate))
            {
                if (_config.HackCrate.TypeLootTable == 6) return null;
                else return true;
            }

            return null;
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
            foreach (CrateConfig crateConfig in _config.DefaultCrates)
            {
                CheckLootTable(crateConfig.OwnLootTable);
                CheckPrefabLootTable(crateConfig.PrefabLootTable);
            }

            CheckLootTable(_config.HackCrate.OwnLootTable);
            CheckPrefabLootTable(_config.HackCrate.PrefabLootTable);

            foreach (PresetConfig preset in _config.Npc)
            {
                CheckLootTable(preset.OwnLootTable);
                CheckPrefabLootTable(preset.PrefabLootTable);
            }

            SaveConfig();
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

        #region The Walking Dead
        private object CanSpawnWalkingDeadNPC(BasePlayer player, HitInfo info)
        {
            if (Controller == null || !player.IsPlayer()) return null;
            if (Vector3.Distance(Controller.transform.position, player.transform.position) < _config.Radius) return false;
            return null;
        }
        #endregion The Walking Dead

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
                case "LockedCrate":
                    AddBalance(playerId, _config.Economy.LockedCrate);
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
            if (_config.GuiAnnouncements.IsGuiAnnouncements) GUIAnnouncements?.Call("CreateAnnouncement", ClearColorAndSize(message), _config.GuiAnnouncements.BannerColor, _config.GuiAnnouncements.TextColor, player, _config.GuiAnnouncements.ApiAdjustVPosition);
            if (_config.Notify.IsNotify && plugins.Exists("Notify")) Notify?.Call("SendNotify", player, _config.Notify.Type, ClearColorAndSize(message));
        }
        #endregion Alerts

        #region GUI
        private HashSet<string> Names { get; } = new HashSet<string>
        {
            "Tab_KpucTaJl",
            "Clock_KpucTaJl",
            "Npc_KpucTaJl",
            "Crate_KpucTaJl"
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
        [PluginReference] private readonly Plugin NpcSpawn;

        private HashSet<string> HooksInsidePlugin { get; } = new HashSet<string>
        {
            "OnEntityTakeDamage",
            "CanBuild",
            "CanChangeGrade",
            "OnStructureRotate",
            "OnEntitySpawned",
            "OnEntityKill",
            "OnPlayerConnected",
            "OnPlayerDeath",
            "OnEntityDeath",
            "CanHackCrate",
            "OnCrateHack",
            "OnSamSiteTarget",
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
            "CanSpawnWalkingDeadNPC"
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

        private static void CopySerializableFields<T>(T src, T dst)
        {
            FieldInfo[] srcFields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in srcFields)
            {
                object value = field.GetValue(src);
                field.SetValue(dst, value);
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
            webrequest.Enqueue("http://37.153.157.216:5000/Api/GetPluginVersions?pluginName=AirEvent", null, (code, response) =>
            {
                if (code != 200 || string.IsNullOrEmpty(response)) return;
                string[] array = response.Replace("\"", string.Empty).Split('.');
                VersionNumber latestVersion = new VersionNumber(Convert.ToInt32(array[0]), Convert.ToInt32(array[1]), Convert.ToInt32(array[2]));
                if (Version < latestVersion) PrintWarning($"A new version ({latestVersion}) of the plugin is available! You need to update the plugin:\n- https://lone.design/product/air-event-rust-plugin\n- https://codefling.com/plugins/air-event");
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
        [ChatCommand("airstart")]
        private void ChatStartEvent(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                if (!Active) Start(null);
                else PrintToChat(player, GetMessage("EventActive", player.UserIDString, _config.Chat.Prefix));
            }
        }

        [ChatCommand("airstop")]
        private void ChatStopEvent(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                if (Controller != null) Finish();
                else Interface.Oxide.ReloadPlugin(Name);
            }
        }

        [ChatCommand("airpos")]
        private void ChatCommandPos(BasePlayer player)
        {
            if (!player.IsAdmin || Controller == null) return;
            Vector3 pos = Controller.transform.InverseTransformPoint(player.transform.position);
            Puts($"Position: {pos}");
            PrintToChat(player, $"Position: {pos}");
        }

        [ConsoleCommand("airstart")]
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
            else Puts("This event is active now. To finish this event (airstop), then to start the next one");
        }

        [ConsoleCommand("airstop")]
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

namespace Oxide.Plugins.AirEventExtensionMethods
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

        public static void KillMapMarker(this HackableLockedCrate crate)
        {
            if (!crate.mapMarkerInstance.IsExists()) return;
            crate.mapMarkerInstance.Kill();
            crate.mapMarkerInstance = null;
        }
    }
}