using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Oxide.Plugins.BossMonsterExtensionMethods;

namespace Oxide.Plugins
{
    [Info("BossMonster", "KpucTaJl", "2.0.3")]
    internal class BossMonster : RustPlugin
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
            _config.PluginVersion = Version;
            Puts("Config update completed!");
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        public class GuiAnnouncementsConfig
        {
            [JsonProperty(En ? "Do you use the GUI Announcements plugin? [true/false]" : "Использовать ли GUI Announcements? [true/false]")] public bool IsGuiAnnouncements { get; set; }
            [JsonProperty(En ? "Banner color" : "Цвет баннера")] public string BannerColor { get; set; }
            [JsonProperty(En ? "Text color" : "Цвет текста")] public string TextColor { get; set; }
            [JsonProperty(En ? "Adjust Vertical Position" : "Отступ от верхнего края")] public float ApiAdjustVPosition { get; set; }
        }

        public class NotifyConfig
        {
            [JsonProperty(En ? "Do you use the Notify plugin? [true/false]" : "Использовать ли плагин Notify? [true/false]")] public bool IsNotify { get; set; }
            [JsonProperty(En ? "Type" : "Тип")] public string Type { get; set; }
        }

        public class DiscordConfig
        {
            [JsonProperty(En ? "Do you use the Discord Messages plugin? [true/false]" : "Использовать ли плагин Discord Messages? [true/false]")] public bool IsDiscord { get; set; }
            [JsonProperty("Webhook URL")] public string WebhookUrl { get; set; }
            [JsonProperty(En ? "Embed Color (DECIMAL)" : "Цвет полосы (DECIMAL)")] public int EmbedColor { get; set; }
            [JsonProperty(En ? "Keys of required messages" : "Ключи необходимых сообщений")] public HashSet<string> Keys { get; set; }
        }

        private class PluginConfig
        {
            [JsonProperty(En ? "Prefix of chat messages" : "Префикс сообщений в чате")] public string Prefix { get; set; }
            [JsonProperty(En ? "Do you use the chat? [true/false]" : "Использовать ли чат? [true/false]")] public bool IsChat { get; set; }
            [JsonProperty(En ? "GUI Announcements setting" : "Настройка GUI Announcements")] public GuiAnnouncementsConfig GuiAnnouncements { get; set; }
            [JsonProperty(En ? "Notify setting" : "Настройка Notify")] public NotifyConfig Notify { get; set; }
            [JsonProperty(En ? "Discord setting (only for users DiscordMessages plugin)" : "Настройка оповещений в Discord (только для тех, кто использует плагин DiscordMessages)")] public DiscordConfig Discord { get; set; }
            [JsonProperty(En ? "Use the PVE mode of the plugin? (only for users PveMode plugin)" : "Использовать PVE режим работы плагина? (только для тех, кто использует плагин PveMode)")] public bool Pve { get; set; }
            [JsonProperty(En ? "NPC Turret Damage Multiplier" : "Множитель урона от турелей по NPC")] public float TurretDamageScale { get; set; }
            [JsonProperty(En ? "Configuration version" : "Версия конфигурации")] public VersionNumber PluginVersion { get; set; }

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    Prefix = "[BossMonster]",
                    IsChat = true,
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
                        Type = "0"
                    },
                    Discord = new DiscordConfig
                    {
                        IsDiscord = false,
                        WebhookUrl = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks",
                        EmbedColor = 13516583,
                        Keys = new HashSet<string>
                        {
                            "Start",
                            "Finish"
                        }
                    },
                    Pve = false,
                    TurretDamageScale = 0.5f,
                    PluginVersion = new VersionNumber()
                };
            }
        }
        #endregion Config

        #region Data
        public class NpcWear
        {
            [JsonProperty("ShortName")] public string ShortName { get; set; }
            [JsonProperty("SkinID (0 - default)")] public ulong SkinID { get; set; }
        }

        public class NpcBelt
        {
            [JsonProperty("ShortName")] public string ShortName { get; set; }
            [JsonProperty(En ? "Amount" : "Кол-во")] public int Amount { get; set; }
            [JsonProperty("SkinID (0 - default)")] public ulong SkinID { get; set; }
            [JsonProperty(En ? "Mods" : "Модификации на оружие")] public HashSet<string> Mods { get; set; }
            [JsonProperty(En ? "Ammo" : "Боеприпасы")] public string Ammo { get; set; }
        }

        public class ColorConfig
        {
            [JsonProperty("r")] public float R { get; set; }
            [JsonProperty("g")] public float G { get; set; }
            [JsonProperty("b")] public float B { get; set; }
        }

        public class MarkerConfig
        {
            [JsonProperty(En ? "Do you use the Marker? [true/false]" : "Использовать ли маркер? [true/false]")] public bool IsMarker { get; set; }
            [JsonProperty(En ? "Radius" : "Радиус")] public float Radius { get; set; }
            [JsonProperty(En ? "Transparency" : "Прозрачность")] public float Alpha { get; set; }
            [JsonProperty(En ? "Marker color" : "Цвет маркера")] public ColorConfig Color { get; set; }
        }

        public class NpcEconomic
        {
            [JsonProperty("Economics")] public double Economics { get; set; }
            [JsonProperty(En ? "Server Rewards (minimum 1)" : "Server Rewards (минимум 1)")] public int ServerRewards { get; set; }
            [JsonProperty(En ? "IQEconomic (minimum 1)" : "IQEconomic (минимум 1)")] public int IQEconomic { get; set; }
        }

        public class MonumentPositionsConfig
        {
            [JsonProperty(En ? "Name of monument" : "Название монумента")] public string Name { get; set; }
            [JsonProperty(En ? "List of positions" : "Список позиций")] public HashSet<string> Positions { get; set; }
        }

        public class ItemConfig
        {
            [JsonProperty("ShortName")] public string ShortName { get; set; }
            [JsonProperty(En ? "Minimum" : "Минимальное кол-во")] public int MinAmount { get; set; }
            [JsonProperty(En ? "Maximum" : "Максимальное кол-во")] public int MaxAmount { get; set; }
            [JsonProperty(En ? "Chance [0.0-100.0]" : "Шанс выпадения предмета [0.0-100.0]")] public float Chance { get; set; }
            [JsonProperty(En ? "Is this a blueprint? [true/false]" : "Это чертеж? [true/false]")] public bool IsBluePrint { get; set; }
            [JsonProperty("SkinID (0 - default)")] public ulong SkinID { get; set; }
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

        public class RadiusActionsConfig
        {
            [JsonProperty(En ? "Use only one ability at a time? [true/false]" : "Одновременно использовать только одну способность? [true/false]")] public bool UseOnlyOneAbility { get; set; }
            [JsonProperty(En ? "Radius (to disable all abilities, set the value to 0)" : "Радиус (чтобы отключить все способности установите значение 0)")] public float Radius { get; set; }
            [JsonProperty(En ? "Spikes ability cooldown time (to disable the ability, set the value -1)" : "Время перезарядки способности Spikes (чтобы отключить способность установите значение -1)")] public int TimeToSpikes { get; set; }
            [JsonProperty(En ? "Applied damage to player from Spikes" : "Получаемый урон игроком от Spikes")] public float DamageSpikes { get; set; }
            [JsonProperty(En ? "FireBall ability cooldown time (to disable the ability, set the value -1)" : "Время перезарядки способности FireBall (чтобы отключить способность установите значение -1)")] public int TimeToFire { get; set; }
            [JsonProperty(En ? "Applied damage to player from FireBall" : "Получаемый урон игроком от FireBall")] public float DamageFire { get; set; }
            [JsonProperty(En ? "ElectricShock ability cooldown time (to disable the ability, set the value -1)" : "Время перезарядки способности ElectricShock (чтобы отключить способность установите значение -1)")] public int TimeToElectricShock { get; set; }
            [JsonProperty(En ? "Applied damage to player from ElectricShock" : "Получаемый урон игроком от ElectricShock")] public float DamageElectricShock { get; set; }
            [JsonProperty(En ? "Wounded ability cooldown time (to disable the ability, set the value -1)" : "Время перезарядки способности Wounded (чтобы отключить способность установите значение -1)")] public int TimeToWounded { get; set; }
            [JsonProperty(En ? "Animal Ability Settings" : "Настройки способности Animal")] public AnimalAbility AnimalAbility { get; set; }
            [JsonProperty(En ? "NPC Ability Settings" : "Настройки способности NPC")] public NpcAbility NpcAbility { get; set; }
            [JsonProperty(En ? "Radiation" : "Радиация")] public float Radiation { get; set; }
            [JsonProperty(En ? "Temperature" : "Температура")] public float Temperature { get; set; }
        }

        public class AnimalAbility
        {
            [JsonProperty(En ? "Ability Cooldown Time (to disable the ability, set the value -1)" : "Время перезарядки способности (чтобы отключить способность установите значение -1)")] public int Time { get; set; }
            [JsonProperty(En ? "Type of animal (Wolf, Bear)" : "Тип животного (Wolf, Bear, Polar Bear)")] public string Type { get; set; }
            [JsonProperty(En ? "Number of animals" : "Кол-во животных")] public int Count { get; set; }
            [JsonProperty(En ? "Despawn time animals" : "Время удаления животных")] public float DespawnTime { get; set; }
        }

        public class NpcAbility
        {
            [JsonProperty(En ? "Ability Cooldown Time (to disable the ability, set the value -1)" : "Время перезарядки способности (чтобы отключить способность установите значение -1)")] public int Time { get; set; }
            [JsonProperty(En ? "NPC Settings" : "Настройки NPC")] public AddNpcConfig ConfigNpc { get; set; }
            [JsonProperty(En ? "Number of NPCs" : "Кол-во NPC")] public int Count { get; set; }
            [JsonProperty(En ? "Despawn time NPCs" : "Время удаления NPC")] public float DespawnTime { get; set; }
        }

        public class AddNpcConfig
        {
            [JsonProperty(En ? "Names" : "Названия")] public List<string> Names { get; set; }
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
            [JsonProperty(En ? "Wear items" : "Одежда")] public HashSet<NpcWear> WearItems { get; set; }
            [JsonProperty(En ? "Belt items" : "Быстрые слоты")] public HashSet<NpcBelt> BeltItems { get; set; }
            [JsonProperty(En ? "Kit (it is recommended to use the previous 2 settings to improve performance)" : "Kit (рекомендуется использовать предыдущие 2 пункта настройки для повышения производительности)")] public string Kit { get; set; }
        }

        public class TakeDamageActionsConfig
        {
            [JsonProperty(En ? "Disable all abilities when applying damage? [true/false]" : "Отключить все способности при нанесении урона? [true/false]")] public bool IsDisable { get; set; }
            [JsonProperty(En ? "Regeneration of health from the applied damage [%]" : "Восстановление здоровья от нанесенного урона [%]")] public float Vampirism { get; set; }
            [JsonProperty(En ? "The amount of calories consumed" : "Кол-во калорий, которое расходуется")] public float CaloriesTarget { get; set; }
            [JsonProperty(En ? "The amount of water consumed" : "Кол-во воды, которое расходуется")] public float HydrationTarget { get; set; }
            [JsonProperty(En ? "The amount of added radiation" : "Кол-во добавляемой радиации")] public float RadiationTarget { get; set; }
            [JsonProperty(En ? "The amount of added bleeding" : "Кол-во добавляемого кровотечения")] public float BleedingTarget { get; set; }
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
            [JsonProperty(En ? "Minimum time of appearance after death [sec.]" : "Минимальное время появления после смерти [sec.]")] public float MinTime { get; set; }
            [JsonProperty(En ? "Maximum time of appearance after death [sec.]" : "Максимальное время появления после смерти [sec.]")] public float MaxTime { get; set; }
            [JsonProperty(En ? "Disable radio effects? [true/false]" : "Отключать эффекты рации? [true/false]")] public bool DisableRadio { get; set; }
            [JsonProperty(En ? "Remove a corpse after death? (it is recommended to use the true value to improve performance) [true/false]" : "Удалять труп после смерти? (рекомендуется использовать значение true для повышения производительности) [true/false]")] public bool IsRemoveCorpse { get; set; }
            [JsonProperty(En ? "Wear items" : "Одежда")] public HashSet<NpcWear> WearItems { get; set; }
            [JsonProperty(En ? "Belt items" : "Быстрые слоты")] public HashSet<NpcBelt> BeltItems { get; set; }
            [JsonProperty(En ? "Kit (it is recommended to use the previous 2 settings to improve performance)" : "Kit (рекомендуется использовать предыдущие 2 пункта настройки для повышения производительности)")] public string Kit { get; set; }
            [JsonProperty(En ? "Marker settings" : "Настройки маркера")] public MarkerConfig Marker { get; set; }
            [JsonProperty(En ? "The amount of economics that is given for killing the boss" : "Кол-во экономики, которое выдается за убийство босса")] public NpcEconomic Economic { get; set; }
            [JsonProperty(En ? "List of monument locations" : "Список расположений на монументах")] public HashSet<MonumentPositionsConfig> Monuments { get; set; }
            [JsonProperty(En ? "The distance at which you can apply damage to the boss (use 0 at any distance)" : "Дистанция, при которой можно наносить урон по боссу (при любой дистанции использовать 0)")] public float PreventDamageRange { get; set; }
            [JsonProperty(En ? "Notify in a chat about actions with the boss? [true/false]" : "Оповещать в чате о действиях с боссом? [true/false]")] public bool IsChat { get; set; }
            [JsonProperty(En ? "Should I apply the boss behavior to a place that is below ocean level? [true/false]" : "Применить ли поведение босса для места, которое находится ниже уровня океана? [true/false]")] public bool IsBelowOceanLevel { get; set; }
            [JsonProperty(En ? "The path to the crate that appears at the place of death (empty - not used)" : "Путь к ящику, который появляется на месте смерти (empty - not used)")] public string CratePrefab { get; set; }
            [JsonProperty(En ? "Which loot table should the plugin use? (0 - default; 1 - own; 2 - AlphaLoot; 3 - CustomLoot; 4 - loot table of the Rust objects; 5 - combine the 1 and 4 methods)" : "Какую таблицу предметов необходимо использовать? (0 - стандартную; 1 - собственную; 2 - AlphaLoot; 3 - CustomLoot; 4 - таблица предметов объектов Rust; 5 - совместить 1 и 4 методы)")] public int TypeLootTable { get; set; }
            [JsonProperty(En ? "Loot table from prefabs (if the loot table type is 4 or 5)" : "Таблица предметов из prefab-ов (если тип таблицы предметов - 4 или 5)")] public PrefabLootTableConfig PrefabLootTable { get; set; }
            [JsonProperty(En ? "Own loot table (if the loot table type is 1 or 5)" : "Собственная таблица предметов (если тип таблицы предметов - 1 или 5)")] public LootTableConfig OwnLootTable { get; set; }
            [JsonProperty(En ? "All actions that occur with the player within the NPC radius" : "Все действия, которые происходят с игроком в радиусе NPC")] public RadiusActionsConfig RadiusActions { get; set; }
            [JsonProperty(En ? "All actions that occur when applying NPC damage" : "Все действия, которые происходят при нанесении урона от NPC")] public TakeDamageActionsConfig TakeDamageActions { get; set; }
            [JsonProperty(En ? "Use the invisibility ability? (use only for bosses with melee weapons) [true/false]" : "Использовать способность невидимости? (использовать только для боссов с оружием ближнего боя) [true/false]")] public bool UseInvisible { get; set; }
        }

        internal HashSet<NpcConfig> Configs = new HashSet<NpcConfig>();

        private void LoadConfigs()
        {
            Puts("Loading files on the /oxide/data/BossMonster/Bosses/ path has started...");
            HashSet<string> allNamesForBosses = new HashSet<string>();
            foreach (string name in Interface.Oxide.DataFileSystem.GetFiles("BossMonster/Bosses/"))
            {
                string fileName = name.Split('/').Last().Split('.').First();
                NpcConfig config = Interface.Oxide.DataFileSystem.ReadObject<NpcConfig>($"BossMonster/Bosses/{fileName}");
                if (config != null)
                {
                    CheckLootTable(config.OwnLootTable);
                    CheckPrefabLootTable(config.PrefabLootTable);

                    if (allNamesForBosses.Contains(config.Name))
                    {
                        PrintWarning($"You can't use the same names for bosses! ({config.Name} -> {config.Name}|)");
                        config.Name += "|";
                    }
                    allNamesForBosses.Add(config.Name);

                    if (config.RoamRange > config.ChaseRange)
                    {
                        config.RoamRange = config.ChaseRange;
                        PrintWarning($"Roam Range should not be higher than Chase Range! ({fileName})");
                    }

                    if (config.RadiusActions.AnimalAbility.Time != -1)
                    {
                        if (config.RadiusActions.AnimalAbility.DespawnTime > config.RadiusActions.AnimalAbility.Time)
                        {
                            config.RadiusActions.AnimalAbility.DespawnTime = config.RadiusActions.AnimalAbility.Time;
                            PrintWarning($"Despawn time animals should not be higher than Ability Cooldown Time! ({fileName})");
                        }
                    }

                    if (config.RadiusActions.NpcAbility.Time != -1)
                    {
                        if (config.RadiusActions.NpcAbility.DespawnTime > config.RadiusActions.NpcAbility.Time)
                        {
                            config.RadiusActions.NpcAbility.DespawnTime = config.RadiusActions.NpcAbility.Time;
                            PrintWarning($"Despawn time NPCs should not be higher than Ability Cooldown Time! ({fileName})");
                        }
                        if (config.RadiusActions.NpcAbility.ConfigNpc.RoamRange > config.RadiusActions.NpcAbility.ConfigNpc.ChaseRange) config.RadiusActions.NpcAbility.ConfigNpc.RoamRange = config.RadiusActions.NpcAbility.ConfigNpc.ChaseRange;
                    }

                    Puts($"File {fileName} has been loaded successfully!");

                    Configs.Add(config);

                    Interface.Oxide.DataFileSystem.WriteObject($"BossMonster/Bosses/{fileName}", config);
                }
                else PrintError($"File {fileName} is corrupted and cannot be loaded!");
            }
        }
        #endregion Data

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Start"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>arrived</color> to zone <color=#55aaff>{2}</color>!",
                ["Finish"] = "{0} <color=#55aaff>{1}</color> killed <color=#55aaff>{2}</color> to zone <color=#55aaff>{3}</color>",
                ["NoDamage"] = "{0} You <color=#ce3f27>cannot</color> damage an boss from your position!"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Start"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>прибыл</color> в квадрат <color=#55aaff>{2}</color>!",
                ["Finish"] = "{0} <color=#55aaff>{1}</color> убил <color=#55aaff>{2}</color> в квадрате <color=#55aaff>{3}</color>",
                ["NoDamage"] = "{0} Вы <color=#ce3f27>не можете</color> нанести урон боссу с текущей позиции!"
            }, this, "ru");
        }

        private string GetMessage(string langKey, string userID) => lang.GetMessage(langKey, _ins, userID);

        private string GetMessage(string langKey, string userID, params object[] args) => (args.Length == 0) ? GetMessage(langKey, userID) : string.Format(GetMessage(langKey, userID), args);
        #endregion Lang

        #region Oxide Hooks
        [PluginReference] private readonly Plugin NpcSpawn, PveMode;

        private static BossMonster _ins;

        private void Init() => _ins = this;

        private void OnServerInitialized()
        {
            LoadDefaultMessages();

            if (!plugins.Exists("NpcSpawn"))
            {
                PrintError("NpcSpawn plugin doesn`t exist! Please read the file ReadMe.txt");
                NextTick(() => Interface.Oxide.UnloadPlugin(Name));
                return;
            }

            LoadConfigs();

            LoadIDs();
            LoadCustomMapPositions();

            _monuments = TerrainMeta.Path.Monuments.Where(IsNecessaryMonument);

            timer.In(10f, () => { foreach (NpcConfig npcConfig in Configs) SpawnBoss(npcConfig); });
        }

        private void Unload()
        {
            foreach (ControllerBoss controller in _controllers.Values) if (controller.Npc.IsExists()) controller.Npc.Kill();
            _ins = null;
        }

        private void OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
            if (!player.IsPlayer() || info == null) return;
            ScientistNPC npc = info.Initiator as ScientistNPC;
            if (npc == null) return;
            ControllerBoss controller = null;
            if (_controllers.TryGetValue(npc.net.ID, out controller)) controller.TakeDamageActions(player, info);
        }

        private object OnEntityTakeDamage(ScientistNPC entity, HitInfo info)
        {
            if (entity == null || info == null) return null;
            if (_controllers.ContainsKey(entity.net.ID))
            {
                BasePlayer attacker = info.InitiatorPlayer;
                BaseEntity weaponPrefab = info.WeaponPrefab;

                if (!attacker.IsPlayer()) return null;

                if ((weaponPrefab == null || weaponPrefab.ShortPrefabName == "grenade.molotov.deployed" || weaponPrefab.ShortPrefabName == "rocket_fire") && info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Heat) return true;

                NpcConfig config = Configs.FirstOrDefault(x => x.Name == entity.displayName);
                if (config.PreventDamageRange > 0f && Vector3.Distance(attacker.transform.position, entity.transform.position) > config.PreventDamageRange)
                {
                    AlertToPlayer(attacker, GetMessage("NoDamage", attacker.UserIDString, _config.Prefix));
                    return true;
                }
            }
            return null;
        }

        private object OnEntityTakeDamage(BaseAnimalNPC animal, HitInfo info)
        {
            if (animal == null || info == null) return null;
            if (_controllers.Any(x => x.Value.Animals.Contains(animal)))
            {
                if (info.InitiatorPlayer.IsPlayer()) return null;
                else return true;
            }
            else return null;
        }

        private void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            if (!player.IsPlayer()) return;
            ControllerBoss controller = _controllers.Values.FirstOrDefault(x => x.Players.Contains(player));
            if (controller != null) controller.Players.Remove(player);
        }
        #endregion Oxide Hooks

        #region Controller
        private readonly Dictionary<uint, ControllerBoss> _controllers = new Dictionary<uint, ControllerBoss>();

        private void SpawnBoss(NpcConfig config)
        {
            Vector3 pos = GetSpawnPos(config);
            if (pos == Vector3.zero)
            {
                timer.In(UnityEngine.Random.Range(config.MinTime, config.MaxTime), () => SpawnBoss(config));
                return;
            }

            ScientistNPC npc = (ScientistNPC)NpcSpawn.Call("SpawnNpc", pos, GetObjectConfig(config));
            if (npc == null)
            {
                PrintError($"{config.Name} spawn error!");
                return;
            }

            ControllerBoss controller = npc.gameObject.AddComponent<ControllerBoss>();
            controller.InitData(config);

            _controllers.Add(npc.net.ID, controller);

            if (_config.Pve && plugins.Exists("PveMode")) PveMode.Call("ScientistAddPveMode", npc);

            if (config.IsChat) AlertToAllPlayers("Start", _config.Prefix, config.Name, PhoneController.PositionToGridCoord(npc.transform.position));

            Interface.Oxide.CallHook("OnBossSpawn", npc);
        }

        private JObject GetObjectConfig(NpcConfig config)
        {
            HashSet<string> states = new HashSet<string> { "RoamState", "ChaseState", "CombatState" };
            if (config.BeltItems.Any(x => x.ShortName == "rocket.launcher" || x.ShortName == "explosive.timed")) states.Add("RaidState");
            return new JObject
            {
                ["Name"] = config.Name,
                ["WearItems"] = new JArray { config.WearItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["SkinID"] = x.SkinID }) },
                ["BeltItems"] = new JArray { config.BeltItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["Amount"] = x.Amount, ["SkinID"] = x.SkinID, ["Mods"] = new JArray { x.Mods }, ["Ammo"] = x.Ammo }) },
                ["Kit"] = config.Kit,
                ["Health"] = config.Health,
                ["RoamRange"] = config.RoamRange,
                ["ChaseRange"] = config.ChaseRange,
                ["DamageScale"] = config.DamageScale,
                ["TurretDamageScale"] = _config.TurretDamageScale,
                ["AimConeScale"] = config.AimConeScale,
                ["DisableRadio"] = config.DisableRadio,
                ["CanUseWeaponMounted"] = true,
                ["CanRunAwayWater"] = !config.IsBelowOceanLevel,
                ["Speed"] = config.Speed,
                ["AreaMask"] = config.IsBelowOceanLevel ? 25 : 1,
                ["AgentTypeID"] = config.IsBelowOceanLevel ? 0 : -1372625422,
                ["HomePosition"] = string.Empty,
                ["States"] = new JArray { states },
                ["Sensory"] = new JObject
                {
                    ["AttackRangeMultiplier"] = config.AttackRangeMultiplier,
                    ["SenseRange"] = config.SenseRange,
                    ["MemoryDuration"] = config.MemoryDuration,
                    ["CheckVisionCone"] = config.CheckVisionCone,
                    ["VisionCone"] = config.VisionCone
                }
            };
        }

        internal class ControllerBoss : FacepunchBehaviour
        {
            internal ScientistNPC Npc;

            private MapMarkerGenericRadius _mapmarker;
            private VendingMachineMapMarker _vendingMarker;

            private float _maxHealth;
            private bool _isBelowOceanLevel;

            internal RadiusActionsConfig radiusActions = null;
            private GameObject _customSphere = new GameObject();
            private int _timeToSpikes = -1;
            private int _timeToFire = -1;
            private int _timeToElectricShock = -1;
            private int _timeToWounded = -1;
            private int _timeToAnimal = -1;
            private int _timeToNpc = -1;
            private readonly HashSet<Barricade> _allSpikes = new HashSet<Barricade>();
            private Coroutine _fireBallCoroutine = null;
            private Coroutine _electricShockCoroutine = null;
            private readonly HashSet<BasePlayer> _woundedPlayers = new HashSet<BasePlayer>();
            internal HashSet<BaseAnimalNPC> Animals = new HashSet<BaseAnimalNPC>();
            internal HashSet<ScientistNPC> Scientists = new HashSet<ScientistNPC>();

            private TakeDamageActionsConfig _takeDamageActions = null;

            internal HashSet<BasePlayer> Players = new HashSet<BasePlayer>();

            private Vector3 _homePosition;
            private int _timeToInvis = 5;
            private int _timeToGoHome = 0;
            private int _timeToGhost = 3;

            private void Awake() { Npc = GetComponent<ScientistNPC>(); }

            internal void InitData(NpcConfig config)
            {
                if (config.Marker.IsMarker) SpawnMapMarker(config.Marker);

                _maxHealth = config.Health;
                _isBelowOceanLevel = config.IsBelowOceanLevel;

                if (config.RadiusActions.Radius > 0f)
                {
                    radiusActions = config.RadiusActions;

                    _customSphere.AddComponent<CustomSphereCollider>().InitData(this);

                    _timeToSpikes = radiusActions.TimeToSpikes;
                    _timeToFire = radiusActions.TimeToFire;
                    _timeToElectricShock = radiusActions.TimeToElectricShock;
                    _timeToWounded = radiusActions.TimeToWounded;
                    _timeToAnimal = radiusActions.AnimalAbility.Time;
                    _timeToNpc = radiusActions.NpcAbility.Time;

                    if (radiusActions.Radiation > 0f) InitRadiation(radiusActions.Radiation);

                    if (radiusActions.Temperature != 0f) InitTemperature(radiusActions.Temperature);

                    InvokeRepeating(RadiusActions, 1f, 1f);
                }

                if (!config.TakeDamageActions.IsDisable) _takeDamageActions = config.TakeDamageActions;

                _homePosition = Npc.transform.position;
                if (config.UseInvisible) InvokeRepeating(CheckInvisible, 1f, 1f);
            }

            private void OnDestroy()
            {
                CancelInvoke(UpdateMapMarker);
                if (_mapmarker.IsExists()) _mapmarker.Kill();
                if (_vendingMarker.IsExists()) _vendingMarker.Kill();

                CancelInvoke(RadiusActions);
                if (_customSphere != null) Destroy(_customSphere);

                CancelInvoke(DeleteAllSpikes);
                DeleteAllSpikes();

                if (_fireBallCoroutine != null) ServerMgr.Instance.StopCoroutine(_fireBallCoroutine);

                if (_electricShockCoroutine != null) ServerMgr.Instance.StopCoroutine(_electricShockCoroutine);

                CancelInvoke(FinishWounded);
                FinishWounded();

                CancelInvoke(DeleteAllAnimals);
                DeleteAllAnimals();

                CancelInvoke(DeleteAllScientists);
                DeleteAllScientists();

                CancelInvoke(CheckInvisible);
            }

            private void SpawnMapMarker(MarkerConfig config)
            {
                _mapmarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", transform.position) as MapMarkerGenericRadius;
                _mapmarker.Spawn();
                _mapmarker.radius = config.Radius;
                _mapmarker.alpha = config.Alpha;
                _mapmarker.color1 = new Color(config.Color.R, config.Color.G, config.Color.B);

                _vendingMarker = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", transform.position) as VendingMachineMapMarker;
                _vendingMarker.Spawn();
                _vendingMarker.markerShopName = $"{Npc.displayName} ({(int)Npc.health} HP)";

                InvokeRepeating(UpdateMapMarker, 0, 1f);
            }

            private void UpdateMapMarker()
            {
                _mapmarker.transform.position = transform.position;
                _mapmarker.SendUpdate();
                _mapmarker.SendNetworkUpdate();

                _vendingMarker.transform.position = transform.position;
                _vendingMarker.markerShopName = $"{Npc.displayName} ({(int)Npc.health} HP)";
                _vendingMarker.SendNetworkUpdate();
            }

            private void InitRadiation(float value)
            {
                TriggerRadiation trigger = _customSphere.AddComponent<TriggerRadiation>();
                trigger.RadiationAmountOverride = value;
                trigger.interestLayers = 1 << 17;
            }

            private void InitTemperature(float value)
            {
                TriggerTemperature trigger = _customSphere.AddComponent<TriggerTemperature>();
                trigger.Temperature = value;
                trigger.triggerSize = radiusActions.Radius;
                trigger.interestLayers = 1 << 17;
            }

            private void RadiusActions()
            {
                if (_timeToSpikes > 0) _timeToSpikes--;
                if (_timeToFire > 0) _timeToFire--;
                if (_timeToElectricShock > 0) _timeToElectricShock--;
                if (_timeToAnimal > 0) _timeToAnimal--;
                if (_timeToWounded > 0) _timeToWounded--;
                if (_timeToNpc > 0) _timeToNpc--;

                if (Players.Count == 0) return;

                if (_timeToSpikes == 0)
                {
                    _timeToSpikes = radiusActions.TimeToSpikes;
                    foreach (BasePlayer player in Players.ToList())
                    {
                        Barricade spikes = GameManager.server.CreateEntity("assets/prefabs/deployable/floor spikes/spikes.floor.prefab", player.transform.position, player.transform.rotation) as Barricade;
                        spikes.enableSaving = false;
                        spikes.Spawn();
                        foreach (Collider collider in spikes.GetComponentsInChildren<Collider>()) DestroyImmediate(collider);

                        player.Hurt(radiusActions.DamageSpikes, DamageType.Stab, Npc, false);

                        _allSpikes.Add(spikes);
                        Invoke(DeleteAllSpikes, 5f);
                    }
                    if (radiusActions.UseOnlyOneAbility) return;
                }

                if (_timeToFire == 0)
                {
                    _timeToFire = radiusActions.TimeToFire;
                    _fireBallCoroutine = ServerMgr.Instance.StartCoroutine(AbilityFireBall());
                    if (radiusActions.UseOnlyOneAbility) return;
                }

                if (_timeToElectricShock == 0)
                {
                    _timeToElectricShock = radiusActions.TimeToElectricShock;
                    _electricShockCoroutine = ServerMgr.Instance.StartCoroutine(AbilityElectricShock());
                    if (radiusActions.UseOnlyOneAbility) return;
                }

                if (_timeToWounded == 0)
                {
                    _timeToWounded = radiusActions.TimeToWounded;
                    foreach (BasePlayer player in Players.ToList())
                    {
                        if (player.IsFlying || player._limitedNetworking) continue;
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, true);
                        _woundedPlayers.Add(player);
                    }
                    Invoke(FinishWounded, 5f);
                    if (radiusActions.UseOnlyOneAbility) return;
                }

                if (_timeToAnimal == 0)
                {
                    BasePlayer target = (BasePlayer)_ins.NpcSpawn.Call("GetCurrentTarget", Npc);
                    if (target == null) return;
                    _timeToAnimal = radiusActions.AnimalAbility.Time;
                    string prefab = radiusActions.AnimalAbility.Type == "Wolf" ? "assets/rust.ai/agents/wolf/wolf.prefab" : radiusActions.AnimalAbility.Type == "Bear" ? "assets/rust.ai/agents/bear/bear.prefab" : "assets/rust.ai/agents/bear/polarbear.prefab";
                    for (int i = 0; i < radiusActions.AnimalAbility.Count; i++)
                    {
                        BaseAnimalNPC animal = GameManager.server.CreateEntity(prefab, transform.position) as BaseAnimalNPC;
                        animal.enableSaving = false;
                        animal.Spawn();
                        animal.Attack(target);
                        Animals.Add(animal);
                    }
                    Invoke(DeleteAllAnimals, radiusActions.AnimalAbility.DespawnTime);
                    if (radiusActions.UseOnlyOneAbility) return;
                }

                if (_timeToNpc == 0)
                {
                    _timeToNpc = radiusActions.NpcAbility.Time;
                    HashSet<string> states = new HashSet<string> { "RoamState", "ChaseState", "CombatState" };
                    if (radiusActions.NpcAbility.ConfigNpc.BeltItems.Any(x => x.ShortName == "rocket.launcher" || x.ShortName == "explosive.timed")) states.Add("RaidState");
                    JObject config = new JObject
                    {
                        ["Name"] = radiusActions.NpcAbility.ConfigNpc.Names.GetRandom(),
                        ["WearItems"] = new JArray { radiusActions.NpcAbility.ConfigNpc.WearItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["SkinID"] = x.SkinID }) },
                        ["BeltItems"] = new JArray { radiusActions.NpcAbility.ConfigNpc.BeltItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["Amount"] = x.Amount, ["SkinID"] = x.SkinID, ["Mods"] = new JArray { x.Mods }, ["Ammo"] = x.Ammo }) },
                        ["Kit"] = radiusActions.NpcAbility.ConfigNpc.Kit,
                        ["Health"] = radiusActions.NpcAbility.ConfigNpc.Health,
                        ["RoamRange"] = radiusActions.NpcAbility.ConfigNpc.RoamRange,
                        ["ChaseRange"] = radiusActions.NpcAbility.ConfigNpc.ChaseRange,
                        ["DamageScale"] = radiusActions.NpcAbility.ConfigNpc.DamageScale,
                        ["TurretDamageScale"] = _ins._config.TurretDamageScale,
                        ["AimConeScale"] = radiusActions.NpcAbility.ConfigNpc.AimConeScale,
                        ["DisableRadio"] = radiusActions.NpcAbility.ConfigNpc.DisableRadio,
                        ["CanUseWeaponMounted"] = true,
                        ["CanRunAwayWater"] = !_isBelowOceanLevel,
                        ["Speed"] = radiusActions.NpcAbility.ConfigNpc.Speed,
                        ["AreaMask"] = _isBelowOceanLevel ? 25 : 1,
                        ["AgentTypeID"] = _isBelowOceanLevel ? 0 : -1372625422,
                        ["HomePosition"] = string.Empty,
                        ["States"] = new JArray { states },
                        ["Sensory"] = new JObject
                        {
                            ["AttackRangeMultiplier"] = radiusActions.NpcAbility.ConfigNpc.AttackRangeMultiplier,
                            ["SenseRange"] = radiusActions.NpcAbility.ConfigNpc.SenseRange,
                            ["MemoryDuration"] = radiusActions.NpcAbility.ConfigNpc.MemoryDuration,
                            ["CheckVisionCone"] = radiusActions.NpcAbility.ConfigNpc.CheckVisionCone,
                            ["VisionCone"] = radiusActions.NpcAbility.ConfigNpc.VisionCone
                        }
                    };
                    for (int i = 0; i < radiusActions.NpcAbility.Count; i++)
                    {
                        ScientistNPC npc = (ScientistNPC)_ins.NpcSpawn.Call("SpawnNpc", transform.position, config);
                        if (npc != null) Scientists.Add(npc);
                    }
                    Invoke(DeleteAllScientists, radiusActions.NpcAbility.DespawnTime);
                    if (radiusActions.UseOnlyOneAbility) return;
                }
            }

            internal void TakeDamageActions(BasePlayer player, HitInfo info)
            {
                if (_timeToInvis == 0)
                {
                    Invisible(false);
                    _timeToInvis = 5;
                    _timeToGoHome = 0;
                    info.damageTypes.ScaleAll(0.2f);
                }
                if (_takeDamageActions == null) return;
                if (_takeDamageActions.Vampirism > 0f && Npc.health < _maxHealth)
                {
                    float newHealth = Npc.health + info.damageTypes.Total() * _takeDamageActions.Vampirism / 100f;
                    if (newHealth > _maxHealth) newHealth = _maxHealth;
                    _ins.NextTick(() => Npc._health = newHealth);
                }
                if (_takeDamageActions.CaloriesTarget != 0f) player.metabolism.calories.Add(-_takeDamageActions.CaloriesTarget);
                if (_takeDamageActions.HydrationTarget != 0f) player.metabolism.hydration.Add(-_takeDamageActions.HydrationTarget);
                if (_takeDamageActions.RadiationTarget != 0f) player.metabolism.radiation_poison.Add(_takeDamageActions.RadiationTarget);
                if (_takeDamageActions.BleedingTarget != 0f) player.metabolism.bleeding.Add(_takeDamageActions.BleedingTarget);
            }

            private IEnumerator AbilityFireBall()
            {
                for (int j = 0; j < 5; j++)
                {
                    foreach (BasePlayer player in Players.ToList())
                    {
                        FireBall fireBall = GameManager.server.CreateEntity("assets/bundled/prefabs/fireball.prefab", player.transform.position, player.transform.rotation) as FireBall;
                        fireBall.enableSaving = false;
                        fireBall.Spawn();
                        player.Hurt(radiusActions.DamageFire, DamageType.Heat, Npc, false);
                    }
                    yield return CoroutineEx.waitForSeconds(1f);
                }
            }

            private IEnumerator AbilityElectricShock()
            {
                for (int j = 0; j < 5; j++)
                {
                    for (int i = 0; i < 10; i++) Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.shock.prefab", Npc.transform.position + UnityEngine.Random.insideUnitSphere * 1.5f);
                    foreach (BasePlayer player in Players.ToList())
                    {
                        for (int i = 0; i < 10; i++) Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.shock.prefab", player.transform.position + UnityEngine.Random.insideUnitSphere * 1.5f);
                        player.Hurt(radiusActions.DamageElectricShock, DamageType.ElectricShock, Npc, false);
                    }
                    yield return CoroutineEx.waitForSeconds(1f);
                }
            }

            private void DeleteAllSpikes()
            {
                foreach (Barricade spikes in _allSpikes) if (spikes.IsExists()) spikes.Kill();
                _allSpikes.Clear();
            }

            private void DeleteAllAnimals()
            {
                foreach (BaseAnimalNPC animal in Animals) if (animal.IsExists()) animal.Kill();
                Animals.Clear();
            }

            private void DeleteAllScientists()
            {
                foreach (ScientistNPC npc in Scientists) if (npc.IsExists()) npc.Kill();
                Scientists.Clear();
            }

            private void FinishWounded()
            {
                foreach (BasePlayer player in _woundedPlayers) if (player.IsExists() && player.HasPlayerFlag(BasePlayer.PlayerFlags.Wounded)) player.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, false);
                _woundedPlayers.Clear();
            }

            private void CheckInvisible()
            {
                BasePlayer target = (BasePlayer)_ins.NpcSpawn.Call("GetCurrentTarget", Npc);
                CheckPath(target);
                CheckGhost(target);
            }

            private void CheckGhost(BasePlayer target)
            {
                if (target == null) return;
                float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);
                if (distanceToTarget > 15f && distanceToTarget < 25f)
                {
                    _timeToGhost--;
                    if (_timeToGhost == 0)
                    {
                        Invisible(true);
                        Npc.Brain.Navigator.Stop();
                        Npc.transform.position = GetPositionGhost(transform.position + (target.transform.position - transform.position).normalized * (Vector3.Distance(transform.position, target.transform.position) + 3f));
                        Npc.viewAngles = Quaternion.LookRotation(target.transform.position - transform.position).eulerAngles;
                        Invisible(false);
                        _timeToGhost = 3;
                    }
                }
                else _timeToGhost = 3;
            }

            private Vector3 GetPositionGhost(Vector3 pos)
            {
                NavMeshHit navMeshHit;
                if (NavMesh.SamplePosition(pos, out navMeshHit, 2f, Npc.NavAgent.areaMask)) return navMeshHit.position;
                else return Vector3.zero;
            }

            private void CheckPath(BasePlayer target)
            {
                if (_timeToGoHome > 0)
                {
                    _timeToGoHome--;
                    if (_timeToGoHome == 0)
                    {
                        Npc.Brain.Navigator.Stop();
                        Npc.transform.position = _homePosition;
                        Invisible(false);
                        _timeToInvis = 5;
                        _timeToGoHome = 0;
                    }
                    return;
                }

                if (target == null) return;

                if (IsPath(target.transform.position)) _timeToInvis = 5;
                else
                {
                    if (_timeToInvis > 0)
                    {
                        _timeToInvis--;
                        if (_timeToInvis == 0)
                        {
                            Invisible(true);
                            _timeToGoHome = 10;
                        }
                    }
                }
            }

            private bool IsPath(Vector3 pos)
            {
                NavMeshHit navMeshHit;
                if (NavMesh.SamplePosition(pos, out navMeshHit, 2f, Npc.NavAgent.areaMask))
                {
                    NavMeshPath path = new NavMeshPath();
                    if (NavMesh.CalculatePath(transform.position, navMeshHit.position, Npc.NavAgent.areaMask, path))
                    {
                        if (path.status == NavMeshPathStatus.PathComplete) return true;
                        else return Vector3.Distance(path.corners.Last(), pos) < 2f;
                    }
                    else return false;
                }
                else return false;
            }

            private void Invisible(bool enabled)
            {
                Effect.server.Run("assets/prefabs/weapons/flashbang/effects/fx-flashbang-boom.prefab", transform.position, Vector3.up, null, true);
                Npc.limitNetworking = enabled;
                if (enabled) Npc.Brain.Navigator.Speed *= 2f;
                else Npc.Brain.Navigator.Speed /= 2f;
            }
        }

        internal class CustomSphereCollider : FacepunchBehaviour
        {
            private SphereCollider _sphereCollider;
            private ControllerBoss _controller;
            private Transform _transform;

            internal void InitData(ControllerBoss controller)
            {
                _controller = controller;
                _transform = controller.transform;

                gameObject.layer = 3;
                _sphereCollider = gameObject.AddComponent<SphereCollider>();
                _sphereCollider.isTrigger = true;
                _sphereCollider.radius = controller.radiusActions.Radius;

                InvokeRepeating(UpdatePosition, 0, 1f);
            }

            private void OnDestroy() => CancelInvoke(UpdatePosition);

            private void OnTriggerEnter(Collider other)
            {
                BasePlayer player = other.GetComponentInParent<BasePlayer>();
                if (player.IsPlayer()) _controller.Players.Add(player);
            }

            private void OnTriggerExit(Collider other)
            {
                BasePlayer player = other.GetComponentInParent<BasePlayer>();
                if (player.IsPlayer()) _controller.Players.Remove(player);
            }

            private void UpdatePosition() => transform.position = _transform.position;
        }
        #endregion Controller

        #region Spawn Loot
        private void OnCorpsePopulate(ScientistNPC entity, NPCPlayerCorpse corpse)
        {
            if (entity == null) return;
            if (_controllers.ContainsKey(entity.net.ID))
            {
                _controllers.Remove(entity.net.ID);

                NpcConfig config = Configs.FirstOrDefault(x => x.Name == entity.displayName);

                timer.In(UnityEngine.Random.Range(config.MinTime, config.MaxTime), () => SpawnBoss(config));

                BasePlayer attacker = entity.lastAttacker as BasePlayer;

                if (attacker.IsPlayer())
                {
                    if (config.IsChat) AlertToAllPlayers("Finish", _config.Prefix, attacker.displayName, entity.displayName, PhoneController.PositionToGridCoord(entity.transform.position));
                    SendBalance(attacker.userID, config.Economic);
                }

                Interface.Oxide.CallHook("OnBossKilled", entity, attacker);

                if (!string.IsNullOrEmpty(config.CratePrefab))
                {
                    BaseEntity crate = GameManager.server.CreateEntity(config.CratePrefab, entity.transform.position, entity.transform.rotation);
                    if (crate == null) _ins.PrintWarning($"Unknown entity! ({config.CratePrefab})");
                    else
                    {
                        crate.enableSaving = false;
                        crate.Spawn();
                    }
                }

                NextTick(() =>
                {
                    if (corpse == null) return;
                    ItemContainer container = corpse.containers[0];
                    if (config.TypeLootTable == 0)
                    {
                        for (int i = container.itemList.Count - 1; i >= 0; i--)
                        {
                            Item item = container.itemList[i];
                            if (config.WearItems.Any(x => x.ShortName == item.info.shortname))
                            {
                                item.RemoveFromContainer();
                                item.Remove();
                            }
                        }
                        return;
                    }
                    if (config.TypeLootTable == 2 || config.TypeLootTable == 3)
                    {
                        if (config.IsRemoveCorpse && !corpse.IsDestroyed) corpse.Kill();
                        return;
                    }
                    container.ClearItemsContainer();
                    if (config.TypeLootTable == 4 || config.TypeLootTable == 5) AddToContainerPrefab(container, config.PrefabLootTable);
                    if (config.TypeLootTable == 1 || config.TypeLootTable == 5) AddToContainerItem(container, config.OwnLootTable);
                    if (config.IsRemoveCorpse && !corpse.IsDestroyed) corpse.Kill();
                });
            }
            else if (_controllers.Any(x => x.Value.Scientists.Contains(entity)))
            {
                NextTick(() =>
                {
                    if (corpse == null) return;
                    corpse.containers[0].ClearItemsContainer();
                    if (!corpse.IsDestroyed) corpse.Kill();
                });
            }
        }

        private object CanPopulateLoot(ScientistNPC entity, NPCPlayerCorpse corpse)
        {
            if (entity == null || entity.net == null) return null;
            if (_controllers.ContainsKey(entity.net.ID))
            {
                NpcConfig config = Configs.FirstOrDefault(x => x.Name == entity.displayName);
                if (config.TypeLootTable == 2) return null;
                else return true;
            }
            return null;
        }

        private object OnCustomLootNPC(uint netID)
        {
            if (_controllers.ContainsKey(netID))
            {
                ScientistNPC entity = _controllers[netID].Npc;
                NpcConfig config = Configs.FirstOrDefault(x => x.Name == entity.displayName);
                if (config.TypeLootTable == 3) return null;
                else return true;
            }
            return null;
        }

        private void AddToContainerPrefab(ItemContainer container, PrefabLootTableConfig lootTable)
        {
            if (lootTable.UseCount)
            {
                int count = UnityEngine.Random.Range(lootTable.Min, lootTable.Max + 1);
                HashSet<string> prefabsInContainer = new HashSet<string>();
                while (prefabsInContainer.Count < count)
                {
                    foreach (PrefabConfig prefab in lootTable.Prefabs)
                    {
                        if (prefabsInContainer.Contains(prefab.PrefabDefinition)) continue;
                        if (UnityEngine.Random.Range(0.0f, 100.0f) <= prefab.Chance)
                        {
                            if (_allLootSpawnSlots.ContainsKey(prefab.PrefabDefinition))
                            {
                                LootContainer.LootSpawnSlot[] lootSpawnSlots = _allLootSpawnSlots[prefab.PrefabDefinition];
                                foreach (LootContainer.LootSpawnSlot lootSpawnSlot in lootSpawnSlots)
                                    for (int j = 0; j < lootSpawnSlot.numberToSpawn; j++)
                                        if (UnityEngine.Random.Range(0f, 1f) <= lootSpawnSlot.probability)
                                            lootSpawnSlot.definition.SpawnIntoContainer(container);
                            }
                            else _allLootSpawn[prefab.PrefabDefinition].SpawnIntoContainer(container);
                            prefabsInContainer.Add(prefab.PrefabDefinition);
                            if (prefabsInContainer.Count == count) return;
                        }
                    }
                }
            }
            else
            {
                HashSet<string> prefabsInContainer = new HashSet<string>();
                foreach (PrefabConfig prefab in lootTable.Prefabs)
                {
                    if (prefabsInContainer.Contains(prefab.PrefabDefinition)) continue;
                    if (UnityEngine.Random.Range(0.0f, 100.0f) <= prefab.Chance)
                    {
                        if (_allLootSpawnSlots.ContainsKey(prefab.PrefabDefinition))
                        {
                            LootContainer.LootSpawnSlot[] lootSpawnSlots = _allLootSpawnSlots[prefab.PrefabDefinition];
                            foreach (LootContainer.LootSpawnSlot lootSpawnSlot in lootSpawnSlots)
                                for (int j = 0; j < lootSpawnSlot.numberToSpawn; j++)
                                    if (UnityEngine.Random.Range(0f, 1f) <= lootSpawnSlot.probability)
                                        lootSpawnSlot.definition.SpawnIntoContainer(container);
                        }
                        else _allLootSpawn[prefab.PrefabDefinition].SpawnIntoContainer(container);
                        prefabsInContainer.Add(prefab.PrefabDefinition);
                    }
                }
            }
        }

        private void AddToContainerItem(ItemContainer container, LootTableConfig lootTable)
        {
            if (lootTable.UseCount)
            {
                int count = UnityEngine.Random.Range(lootTable.Min, lootTable.Max + 1);
                HashSet<int> indexMove = new HashSet<int>();
                while (indexMove.Count < count)
                {
                    foreach (ItemConfig item in lootTable.Items)
                    {
                        if (indexMove.Contains(lootTable.Items.IndexOf(item))) continue;
                        if (UnityEngine.Random.Range(0.0f, 100.0f) <= item.Chance)
                        {
                            Item newItem = item.IsBluePrint ? ItemManager.CreateByName("blueprintbase") : ItemManager.CreateByName(item.ShortName, UnityEngine.Random.Range(item.MinAmount, item.MaxAmount + 1), item.SkinID);
                            if (newItem == null)
                            {
                                PrintWarning($"Failed to create item! ({item.ShortName})");
                                continue;
                            }
                            if (item.IsBluePrint) newItem.blueprintTarget = ItemManager.FindItemDefinition(item.ShortName).itemid;
                            if (!string.IsNullOrEmpty(item.Name)) newItem.name = item.Name;
                            if (container.capacity < container.itemList.Count + 1) container.capacity++;
                            if (!newItem.MoveToContainer(container)) newItem.Remove();
                            else
                            {
                                indexMove.Add(lootTable.Items.IndexOf(item));
                                if (indexMove.Count == count) return;
                            }
                        }
                    }
                }
            }
            else
            {
                HashSet<int> indexMove = new HashSet<int>();
                foreach (ItemConfig item in lootTable.Items)
                {
                    if (indexMove.Contains(lootTable.Items.IndexOf(item))) continue;
                    if (UnityEngine.Random.Range(0.0f, 100.0f) <= item.Chance)
                    {
                        Item newItem = item.IsBluePrint ? ItemManager.CreateByName("blueprintbase") : ItemManager.CreateByName(item.ShortName, UnityEngine.Random.Range(item.MinAmount, item.MaxAmount + 1), item.SkinID);
                        if (newItem == null)
                        {
                            PrintWarning($"Failed to create item! ({item.ShortName})");
                            continue;
                        }
                        if (item.IsBluePrint) newItem.blueprintTarget = ItemManager.FindItemDefinition(item.ShortName).itemid;
                        if (!string.IsNullOrEmpty(item.Name)) newItem.name = item.Name;
                        if (container.capacity < container.itemList.Count + 1) container.capacity++;
                        if (!newItem.MoveToContainer(container)) newItem.Remove();
                        else indexMove.Add(lootTable.Items.IndexOf(item));
                    }
                }
            }
        }

        private void CheckLootTable(LootTableConfig lootTable)
        {
            lootTable.Items = lootTable.Items.OrderBy(x => x.Chance);
            if (lootTable.Max > lootTable.Items.Count) lootTable.Max = lootTable.Items.Count;
            if (lootTable.Min > lootTable.Max) lootTable.Min = lootTable.Max;
        }

        private void CheckPrefabLootTable(PrefabLootTableConfig lootTable)
        {
            HashSet<PrefabConfig> prefabs = new HashSet<PrefabConfig>();
            foreach (PrefabConfig prefabConfig in lootTable.Prefabs)
            {
                if (prefabs.Any(x => x.PrefabDefinition == prefabConfig.PrefabDefinition)) PrintWarning($"Duplicate prefab removed from loot table! ({prefabConfig.PrefabDefinition})");
                else
                {
                    GameObject gameObject = GameManager.server.FindPrefab(prefabConfig.PrefabDefinition);
                    global::HumanNPC humanNpc = gameObject.GetComponent<global::HumanNPC>();
                    ScarecrowNPC scarecrowNPC = gameObject.GetComponent<ScarecrowNPC>();
                    LootContainer lootContainer = gameObject.GetComponent<LootContainer>();
                    if (humanNpc != null && humanNpc.LootSpawnSlots.Length != 0)
                    {
                        if (!_allLootSpawnSlots.ContainsKey(prefabConfig.PrefabDefinition)) _allLootSpawnSlots.Add(prefabConfig.PrefabDefinition, humanNpc.LootSpawnSlots);
                        prefabs.Add(prefabConfig);
                    }
                    else if (scarecrowNPC != null && scarecrowNPC.LootSpawnSlots.Length != 0)
                    {
                        if (!_allLootSpawnSlots.ContainsKey(prefabConfig.PrefabDefinition)) _allLootSpawnSlots.Add(prefabConfig.PrefabDefinition, scarecrowNPC.LootSpawnSlots);
                        prefabs.Add(prefabConfig);
                    }
                    else if (lootContainer != null && lootContainer.LootSpawnSlots.Length != 0)
                    {
                        if (!_allLootSpawnSlots.ContainsKey(prefabConfig.PrefabDefinition)) _allLootSpawnSlots.Add(prefabConfig.PrefabDefinition, lootContainer.LootSpawnSlots);
                        prefabs.Add(prefabConfig);
                    }
                    else if (lootContainer != null && lootContainer.lootDefinition != null)
                    {
                        if (!_allLootSpawn.ContainsKey(prefabConfig.PrefabDefinition)) _allLootSpawn.Add(prefabConfig.PrefabDefinition, lootContainer.lootDefinition);
                        prefabs.Add(prefabConfig);
                    }
                    else PrintWarning($"Unknown prefab removed! ({prefabConfig.PrefabDefinition})");
                }
            }
            lootTable.Prefabs = prefabs.OrderBy(x => x.Chance);
            if (lootTable.Max > lootTable.Prefabs.Count) lootTable.Max = lootTable.Prefabs.Count;
            if (lootTable.Min > lootTable.Max) lootTable.Min = lootTable.Max;
        }

        private readonly Dictionary<string, LootSpawn> _allLootSpawn = new Dictionary<string, LootSpawn>();

        private readonly Dictionary<string, LootContainer.LootSpawnSlot[]> _allLootSpawnSlots = new Dictionary<string, LootContainer.LootSpawnSlot[]>();
        #endregion Spawn Loot

        #region Economy
        [PluginReference] private readonly Plugin Economics, ServerRewards, IQEconomic;

        internal void SendBalance(ulong playerId, NpcEconomic economic)
        {
            if (plugins.Exists("Economics") && economic.Economics > 0) Economics.Call("Deposit", playerId.ToString(), economic.Economics);
            if (plugins.Exists("ServerRewards") && economic.ServerRewards > 0) ServerRewards.Call("AddPoints", playerId, economic.ServerRewards);
            if (plugins.Exists("IQEconomic") && economic.IQEconomic > 0) IQEconomic.Call("API_SET_BALANCE", playerId, economic.IQEconomic);
        }
        #endregion Economy

        #region Alerts
        [PluginReference] private readonly Plugin GUIAnnouncements, DiscordMessages;

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
            if (!string.IsNullOrEmpty(_config.Prefix)) message = message.Replace(_config.Prefix + " ", string.Empty);
            return message;
        }

        private bool CanSendDiscordMessage() => _config.Discord.IsDiscord && !string.IsNullOrEmpty(_config.Discord.WebhookUrl) && _config.Discord.WebhookUrl != "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";

        private void AlertToAllPlayers(string langKey, params object[] args)
        {
            if (CanSendDiscordMessage() && _config.Discord.Keys.Contains(langKey))
            {
                object fields = new[] { new { name = Title, value = ClearColorAndSize(GetMessage(langKey, null, args)), inline = false } };
                DiscordMessages?.Call("API_SendFancyMessage", _config.Discord.WebhookUrl, "", _config.Discord.EmbedColor, JsonConvert.SerializeObject(fields), null, this);
            }
            foreach (BasePlayer player in BasePlayer.activePlayerList) AlertToPlayer(player, GetMessage(langKey, player.UserIDString, args));
        }

        private void AlertToPlayer(BasePlayer player, string message)
        {
            if (_config.IsChat) PrintToChat(player, message);
            if (_config.GuiAnnouncements.IsGuiAnnouncements) GUIAnnouncements?.Call("CreateAnnouncement", ClearColorAndSize(message), _config.GuiAnnouncements.BannerColor, _config.GuiAnnouncements.TextColor, player, _config.GuiAnnouncements.ApiAdjustVPosition);
            if (_config.Notify.IsNotify) player.SendConsoleCommand($"notify.show {_config.Notify.Type} {ClearColorAndSize(message)}");
        }
        #endregion Alerts

        #region Spawn Position
        private HashSet<MonumentInfo> _monuments = new HashSet<MonumentInfo>();

        private readonly HashSet<string> _unnecessaryMonuments = new HashSet<string>
        {
            "Substation",
            "Outpost",
            "Bandit Camp",
            "Fishing Village",
            "Large Fishing Village",
            "Ranch",
            "Large Barn",
            "Ice Lake",
            "Mountain"
        };

        private static string GetNameMonument(MonumentInfo monument)
        {
            if (monument.name.Contains("harbor_1")) return "Small " + monument.displayPhrase.english.Replace("\n", string.Empty);
            if (monument.name.Contains("harbor_2")) return "Large " + monument.displayPhrase.english.Replace("\n", string.Empty);
            if (monument.name.Contains("desert_military_base_a")) return monument.displayPhrase.english.Replace("\n", string.Empty) + " A";
            if (monument.name.Contains("desert_military_base_b")) return monument.displayPhrase.english.Replace("\n", string.Empty) + " B";
            if (monument.name.Contains("desert_military_base_c")) return monument.displayPhrase.english.Replace("\n", string.Empty) + " C";
            if (monument.name.Contains("desert_military_base_d")) return monument.displayPhrase.english.Replace("\n", string.Empty) + " D";
            return monument.displayPhrase.english.Replace("\n", string.Empty);
        }

        private bool IsNecessaryMonument(MonumentInfo monument)
        {
            string name = GetNameMonument(monument);
            if (string.IsNullOrEmpty(name) || _unnecessaryMonuments.Contains(name)) return false;
            return Configs.Any(x => x.Monuments.Any(y => y.Name == name));
        }

        private Vector3 GetSpawnPos(NpcConfig config)
        {
            List<string> results = Facepunch.Pool.GetList<string>();

            foreach (MonumentInfo monument in _monuments)
            {
                MonumentPositionsConfig monumentConfig = config.Monuments.FirstOrDefault(x => x.Name == GetNameMonument(monument));
                if (monumentConfig == null) continue;
                foreach (string position in monumentConfig.Positions) results.Add(monument.transform.TransformPoint(position.ToVector3()).ToString());
            }

            foreach (CustomMapConfig customMap in _customMaps)
                foreach (CustomMapBossPositionsConfig customMapBoss in customMap.Bosses)
                    if (customMapBoss.NameBoss == config.Name)
                        foreach (string position in customMapBoss.Positions)
                            results.Add(position);

            if (results.Count > 0)
            {
                Vector3 result = results.GetRandom().ToVector3();
                Facepunch.Pool.FreeList(ref results);
                return result;
            }
            else
            {
                int number = UnityEngine.Random.Range(0, 4);
                string biome = number == 0 ? "Arid" : number == 1 ? "Temperate" : number == 2 ? "Tundra" : "Arctic";
                object point = NpcSpawn.Call("GetSpawnPoint", biome);
                if (point is Vector3) return (Vector3)point;
                else return GetSpawnPos(config);
            }
        }

        public class CustomMapBossPositionsConfig
        {
            [JsonProperty(En ? "Boss Name" : "Название босса")] public string NameBoss { get; set; }
            [JsonProperty(En ? "List of positions" : "Список позиций")] public HashSet<string> Positions { get; set; }
        }

        public class CustomMapConfig
        {
            [JsonProperty(En ? "ID" : "Идентификатор")] public string ID { get; set; }
            [JsonProperty(En ? "List of bosses" : "Список боссов")] public HashSet<CustomMapBossPositionsConfig> Bosses { get; set; }
        }

        private void LoadCustomMapPositions()
        {
            Puts("Loading files on the /oxide/data/BossMonster/CustomMap/ path has started...");
            foreach (string name in Interface.Oxide.DataFileSystem.GetFiles("BossMonster/CustomMap/"))
            {
                string fileName = name.Split('/').Last().Split('.').First();
                CustomMapConfig config = Interface.Oxide.DataFileSystem.ReadObject<CustomMapConfig>($"BossMonster/CustomMap/{fileName}");
                if (config == null)
                {
                    PrintError($"File {fileName} is corrupted and cannot be loaded!");
                    continue;
                }
                if (!string.IsNullOrEmpty(config.ID) && !_ids.Any(x => Math.Abs(x - Convert.ToSingle(config.ID)) < 0.001f))
                {
                    PrintWarning($"File {fileName} cannot be loaded on the current map!");
                    continue;
                }
                Puts($"File {fileName} has been loaded successfully!");
                _customMaps.Add(config);
            }
        }

        private readonly HashSet<CustomMapConfig> _customMaps = new HashSet<CustomMapConfig>();

        private readonly HashSet<float> _ids = new HashSet<float>();

        private void LoadIDs() { foreach (RANDSwitch entity in BaseNetworkable.serverEntities.OfType<RANDSwitch>()) _ids.Add(entity.transform.position.x + entity.transform.position.y + entity.transform.position.z); }
        #endregion Spawn Position

        #region API
        private ScientistNPC SpawnBoss(string name, Vector3 pos)
        {
            NpcConfig config = Configs.FirstOrDefault(x => x.Name == name);
            if (config == null) return null;

            ScientistNPC npc = (ScientistNPC)NpcSpawn.Call("SpawnNpc", pos, GetObjectConfig(config));
            if (npc == null) return null;

            ControllerBoss controller = npc.gameObject.AddComponent<ControllerBoss>();
            controller.InitData(config);

            _controllers.Add(npc.net.ID, controller);

            Interface.Oxide.CallHook("OnBossSpawn", npc);

            return npc;
        }

        private void DestroyBoss(ScientistNPC entity)
        {
            if (entity == null) return;
            if (_controllers.ContainsKey(entity.net.ID)) _controllers.Remove(entity.net.ID);
            if (entity.IsExists()) entity.Kill();
        }
        #endregion API

        #region Commands
        [ChatCommand("WorldPos")]
        private void ChatCommandWorldPos(BasePlayer player)
        {
            if (!player.IsAdmin) return;
            Puts($"Position: {player.transform.position}");
            PrintToChat(player, $"Position: {player.transform.position}");
        }

        [ChatCommand("SavePos")]
        private void ChatCommandSavePos(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;

            if (args == null || args.Length == 0)
            {
                PrintToChat(player, "You didn't write the name of the NPC");
                return;
            }

            string name = "";
            for (int i = 0; i < args.Length; i++) name += i == 0 ? args[i] : $" {args[i]}";

            NpcConfig config = Configs.FirstOrDefault(x => x.Name == name);
            if (config == null)
            {
                PrintToChat(player, $"The NPC named <color=#55aaff>{name}</color> <color=#ce3f27>does not exist</color> in the configuration");
                return;
            }

            MonumentInfo Monument = null;
            foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
            {
                string monumentName = GetNameMonument(monument);
                if (string.IsNullOrEmpty(monumentName) || _unnecessaryMonuments.Contains(monumentName)) continue;
                if (Monument == null || Vector3.Distance(player.transform.position, monument.transform.position) < Vector3.Distance(player.transform.position, Monument.transform.position)) Monument = monument;
            }
            if (Monument == null) return;
            string MonumentName = GetNameMonument(Monument);
            MonumentPositionsConfig monumentPositionsConfig = config.Monuments.FirstOrDefault(x => x.Name == MonumentName);
            string pos = Monument.transform.InverseTransformPoint(player.transform.position).ToString();

            if (monumentPositionsConfig == null) config.Monuments.Add(new MonumentPositionsConfig { Name = MonumentName, Positions = new HashSet<string> { pos } });
            else monumentPositionsConfig.Positions.Add(pos);

            Interface.Oxide.DataFileSystem.WriteObject($"BossMonster/Bosses/{config.Name}", config);

            PrintToChat(player, $"You <color=#738d43>have added</color> new coordinates to the <color=#55aaff>List of locations on standard monuments</color>:\nMonument: <color=#55aaff>{MonumentName}</color>\nPosition: <color=#55aaff>{pos}</color>");
        }
        #endregion Commands
    }
}

namespace Oxide.Plugins.BossMonsterExtensionMethods
{
    public static class ExtensionMethods
    {
        public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return true;
            return false;
        }

        public static bool Any<TKey, TValue>(this Dictionary<TKey, TValue> source, Func<KeyValuePair<TKey, TValue>, bool> predicate)
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

        public static List<TSource> ToList<TSource>(this IEnumerable<TSource> source)
        {
            List<TSource> result = new List<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(enumerator.Current);
            return result;
        }

        public static TSource First<TSource>(this IList<TSource> source) => source[0];

        public static TSource Last<TSource>(this IList<TSource> source) => source[source.Count - 1];

        public static HashSet<T> OfType<T>(this IEnumerable<BaseNetworkable> source)
        {
            HashSet<T> result = new HashSet<T>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (enumerator.Current is T) result.Add((T)(object)enumerator.Current);
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

        public static string[] Skip(this string[] source, int count)
        {
            if (source.Length == 0) return Array.Empty<string>();
            string[] result = new string[source.Length - count];
            int n = 0;
            for (int i = 0; i < source.Length; i++)
            {
                if (i < count) continue;
                result[n] = source[i];
                n++;
            }
            return result;
        }

        public static bool IsExists(this BaseNetworkable entity) => entity != null && !entity.IsDestroyed;

        public static bool IsPlayer(this BasePlayer player) => player != null && player.userID.IsSteamId();

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