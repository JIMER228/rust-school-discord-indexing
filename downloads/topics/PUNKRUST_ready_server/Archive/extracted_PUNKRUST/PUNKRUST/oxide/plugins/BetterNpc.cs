using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Oxide.Plugins.BetterNpcExtensionMethods;

namespace Oxide.Plugins
{
    [Info("BetterNpc", "KpucTaJl", "1.1.8")]
    internal class BetterNpc : RustPlugin
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
                _config.UnderwaterSpawnPoints = new HashSet<string>
                {
                    "Military Tunnel",
                    "module_900x900_2way_moonpool",
                    "moonpool_1200x1500_1way",
                    "moonpool_1200x1500_2way",
                    "moonpool_1200x1500_3way",
                    "moonpool_1200x1800_ladder",
                    "module_1200x1200_1way",
                    "module_1200x1200_1way_ladder",
                    "module_1200x1200_3way",
                    "module_1200x1200_3way_ladder",
                    "module_1200x1200_4way",
                    "module_1200x1200_4way_ladder",
                    "module_1200x1800_2way",
                    "module_1200x600_2way_corridor",
                    "module_1500x1500_4way_lshaped",
                    "module_1500x1500_ladder",
                    "module_1500x1800_2way_ladder",
                    "module_1500x600_2way_corridor",
                    "module_1800x1800_4way_lshaped",
                    "module_2100x600_3way_corridor",
                    "module_900x900_1way",
                    "module_900x900_1way_ladder",
                    "module_900x900_3way",
                    "module_900x900_3way_ladder",
                    "module_900x900_4way",
                    "module_900x900_4way_ladder"
                };
                _config.SafeZoneRange = 150f;
            }
            if (_config.PluginVersion < new VersionNumber(1, 1, 2))
            {
                HashSet<string> names = new HashSet<string>
                {
                    "curve-ne-0",
                    "curve-ne-1",
                    "curve-nw-0",
                    "curve-nw-1",
                    "curve-se-0",
                    "curve-se-1",
                    "curve-sw-0",
                    "curve-sw-1",
                    "intersection",
                    "intersection-e",
                    "intersection-n",
                    "intersection-s",
                    "intersection-w",
                    "station-sn-0",
                    "station-sn-1",
                    "station-sn-2",
                    "station-sn-3",
                    "station-we-0",
                    "station-we-1",
                    "station-we-2",
                    "station-we-3",
                    "straight-sn-0",
                    "straight-sn-1",
                    "straight-sn-2",
                    "straight-sn-3",
                    "straight-sn-4",
                    "straight-sn-5",
                    "straight-we-0",
                    "straight-we-1",
                    "straight-we-2",
                    "straight-we-3",
                    "straight-we-4",
                    "straight-we-5"
                };
                foreach (string name in names) _config.UnderwaterSpawnPoints.Add(name);
            }
            if (_config.PluginVersion < new VersionNumber(1, 1, 7))
            {
                _config.OtherNpc = new HashSet<string>
                {
                    "NpcRaider",
                    "RandomRaider"
                };
            }
            if (_config.PluginVersion < new VersionNumber(1, 1, 8))
            {
                if (!_config.OtherNpc.Contains("56485621526987")) 
                    _config.OtherNpc.Add("56485621526987");
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

        public class NpcBelt
        {
            [JsonProperty("ShortName")] public string ShortName { get; set; }
            [JsonProperty(En ? "Amount" : "Кол-во")] public int Amount { get; set; }
            [JsonProperty("SkinID (0 - default)")] public ulong SkinID { get; set; }
            [JsonProperty(En ? "Mods" : "Модификации на оружие")] public List<string> Mods { get; set; }
            [JsonProperty(En ? "Ammo" : "Боеприпасы")] public string Ammo { get; set; }
        }

        public class NpcWear
        {
            [JsonProperty("ShortName")] public string ShortName { get; set; }
            [JsonProperty("SkinID (0 - default)")] public ulong SkinID { get; set; }
        }

        public class NpcEconomic
        {
            [JsonProperty("Economics")] public double Economics { get; set; }
            [JsonProperty(En ? "Server Rewards (minimum 1)" : "Server Rewards (минимум 1)")] public int ServerRewards { get; set; }
            [JsonProperty(En ? "IQEconomic (minimum 1)" : "IQEconomic (минимум 1)")] public int IQEconomic { get; set; }
        }

        public class NpcConfig
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
            [JsonProperty(En ? "Minimum time of appearance after death (not used for Events) [sec.]" : "Минимальное время появления после смерти (не используется для Events) [sec.]")] public float MinTime { get; set; }
            [JsonProperty(En ? "Maximum time of appearance after death (not used for Events) [sec.]" : "Максимальное время появления после смерти (не используется для Events) [sec.]")] public float MaxTime { get; set; }
            [JsonProperty(En ? "Disable radio effects? [true/false]" : "Отключать эффекты рации? [true/false]")] public bool DisableRadio { get; set; }
            [JsonProperty(En ? "Is this a stationary NPC? [true/false]" : "Это стационарный NPC? [true/false]")] public bool Stationary { get; set; }
            [JsonProperty(En ? "Remove a corpse after death? (it is recommended to use the true value to improve performance) [true/false]" : "Удалять труп после смерти? (рекомендуется использовать значение true для повышения производительности) [true/false]")] public bool IsRemoveCorpse { get; set; }
            [JsonProperty(En ? "Wear items" : "Одежда")] public List<NpcWear> WearItems { get; set; }
            [JsonProperty(En ? "Belt items" : "Быстрые слоты")] public List<NpcBelt> BeltItems { get; set; }
            [JsonProperty(En ? "Kits (it is recommended to use the previous 2 settings to improve performance)" : "Kits (рекомендуется использовать предыдущие 2 пункта настройки для повышения производительности)")] public List<string> Kits { get; set; }
        }

        public class PresetConfig
        {
            [JsonProperty(En ? "Enabled? [true/false]" : "Включен? [true/false]")] public bool Enabled { get; set; }
            [JsonProperty(En ? "Minimum numbers - Day" : "Минимальное кол-во днем")] public int MinDay { get; set; }
            [JsonProperty(En ? "Maximum numbers - Day" : "Максимальное кол-во днем")] public int MaxDay { get; set; }
            [JsonProperty(En ? "Minimum numbers - Night" : "Минимальное кол-во ночью")] public int MinNight { get; set; }
            [JsonProperty(En ? "Maximum numbers - Night" : "Максимальное кол-во ночью")] public int MaxNight { get; set; }
            [JsonProperty(En ? "NPCs setting" : "Настройки NPC")] public NpcConfig Config { get; set; }
            [JsonProperty(En ? "The amount of economics that is given for killing the NPC" : "Кол-во экономики, которое выдается за убийство NPC")] public NpcEconomic Economic { get; set; }
            [JsonProperty(En ? "Type of appearance (0 - random; 1 - own list) (not used for Road and Biome)" : "Тип появления (0 - рандомное; 1 - собственный список) (не используется для Road и Biome)")] public int TypeSpawn { get; set; }
            [JsonProperty(En ? "Own list of locations (not used for Road and Biome)" : "Собственный список расположений (не используется для Road и Biome)")] public List<string> OwnPositions { get; set; }
            [JsonProperty(En ? "The path to the crate that appears at the place of death (empty - not used)" : "Путь к ящику, который появляется на месте смерти (empty - not used)")] public string CratePrefab { get; set; }
            [JsonProperty(En ? "Which loot table should the plugin use? (0 - default; 1 - own; 2 - AlphaLoot; 3 - CustomLoot; 4 - loot table of the Rust objects; 5 - combine the 1 and 4 methods)" : "Какую таблицу предметов необходимо использовать? (0 - стандартную; 1 - собственную; 2 - AlphaLoot; 3 - CustomLoot; 4 - таблица предметов объектов Rust; 5 - совместить 1 и 4 методы)")] public int TypeLootTable { get; set; }
            [JsonProperty(En ? "Loot table from prefabs (if the loot table type is 4 or 5)" : "Таблица предметов из prefab-ов (если тип таблицы предметов - 4 или 5)")] public PrefabLootTableConfig PrefabLootTable { get; set; }
            [JsonProperty(En ? "Own loot table (if the loot table type is 1 or 5)" : "Собственная таблица предметов (если тип таблицы предметов - 1 или 5)")] public LootTableConfig OwnLootTable { get; set; }
        }

        private class PluginConfig
        {
            [JsonProperty(En ? "Start time of the day" : "Время начала дня")] public string StartDayTime { get; set; }
            [JsonProperty(En ? "Start time of the night" : "Время начала ночи")] public string StartNightTime { get; set; }
            [JsonProperty(En ? "Use the PVE mode of the plugin? (only for users PveMode plugin)" : "Использовать PVE режим работы плагина? (только для тех, кто использует плагин PveMode)")] public bool Pve { get; set; }
            [JsonProperty(En ? "List of spawn points for the appearance of NPCs that are under water" : "Список точек для появления NPC, которые находятся под водой")] public HashSet<string> UnderwaterSpawnPoints { get; set; }
            [JsonProperty(En ? "The distance from the center of the safe zone to the nearest place where the NPC appears [m.]" : "Расстояние от центра безопасной зоны до ближайшего места появления NPC [m.]")] public float SafeZoneRange { get; set; }
            [JsonProperty(En ? "List of Npc types that should not be deleted" : "Список типов Npc, которые не должны удаляться")] public HashSet<string> OtherNpc { get; set; }
            [JsonProperty(En ? "Configuration version" : "Версия конфигурации")] public VersionNumber PluginVersion { get; set; }

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    StartDayTime = "8:00",
                    StartNightTime = "20:00",
                    Pve = false,
                    UnderwaterSpawnPoints = new HashSet<string>
                    {
                        "Military Tunnel",
                        "module_900x900_2way_moonpool",
                        "moonpool_1200x1500_1way",
                        "moonpool_1200x1500_2way",
                        "moonpool_1200x1500_3way",
                        "moonpool_1200x1800_ladder",
                        "module_1200x1200_1way",
                        "module_1200x1200_1way_ladder",
                        "module_1200x1200_3way",
                        "module_1200x1200_3way_ladder",
                        "module_1200x1200_4way",
                        "module_1200x1200_4way_ladder",
                        "module_1200x1800_2way",
                        "module_1200x600_2way_corridor",
                        "module_1500x1500_4way_lshaped",
                        "module_1500x1500_ladder",
                        "module_1500x1800_2way_ladder",
                        "module_1500x600_2way_corridor",
                        "module_1800x1800_4way_lshaped",
                        "module_2100x600_3way_corridor",
                        "module_900x900_1way",
                        "module_900x900_1way_ladder",
                        "module_900x900_3way",
                        "module_900x900_3way_ladder",
                        "module_900x900_4way",
                        "module_900x900_4way_ladder",
                        "curve-ne-0",
                        "curve-ne-1",
                        "curve-nw-0",
                        "curve-nw-1",
                        "curve-se-0",
                        "curve-se-1",
                        "curve-sw-0",
                        "curve-sw-1",
                        "intersection",
                        "intersection-e",
                        "intersection-n",
                        "intersection-s",
                        "intersection-w",
                        "station-sn-0",
                        "station-sn-1",
                        "station-sn-2",
                        "station-sn-3",
                        "station-we-0",
                        "station-we-1",
                        "station-we-2",
                        "station-we-3",
                        "straight-sn-0",
                        "straight-sn-1",
                        "straight-sn-2",
                        "straight-sn-3",
                        "straight-sn-4",
                        "straight-sn-5",
                        "straight-we-0",
                        "straight-we-1",
                        "straight-we-2",
                        "straight-we-3",
                        "straight-we-4",
                        "straight-we-5"
                    },
                    SafeZoneRange = 150f,
                    OtherNpc = new HashSet<string>
                    {
                        "NpcRaider",
                        "RandomRaider",
                        "56485621526987"
                    },
                    PluginVersion = new VersionNumber()
                };
            }
        }
        #endregion Config

        #region Oxide Hooks
        [PluginReference] private readonly Plugin NpcSpawn, PveMode;

        private static BetterNpc _ins;

        private void Init() => _ins = this;

        private void OnServerInitialized()
        {
            if (!plugins.Exists("NpcSpawn"))
            {
                PrintError("NpcSpawn plugin doesn`t exist! Please read the file ReadMe.txt");
                NextTick(() => Interface.Oxide.UnloadPlugin(Name));
                return;
            }
            _isDay = TOD_Sky.Instance.Cycle.Hour < TimeSpan.Parse(_config.StartNightTime).TotalHours && TOD_Sky.Instance.Cycle.Hour > TimeSpan.Parse(_config.StartDayTime).TotalHours;
            _loadCoroutine = ServerMgr.Instance.StartCoroutine(LoadAllFiles());
        }

        private void Unload()
        {
            if (_loadCoroutine != null) ServerMgr.Instance.StopCoroutine(_loadCoroutine);
            if (_checkDayCoroutine != null) ServerMgr.Instance.StopCoroutine(_checkDayCoroutine);
            foreach (ControllerSpawnPoint controller in _controllers) if (controller != null) UnityEngine.Object.Destroy(controller.gameObject);
            _ins = null;
        }

        private void OnCorpsePopulate(ScientistNPC entity, NPCPlayerCorpse corpse)
        {
            if (entity == null || corpse == null) return;
            ControllerSpawnPoint controller = _controllers.FirstOrDefault(x => x.ActiveNpc.Any(y => y.Npc == entity));
            if (controller != null) controller.DieNpc(entity, corpse);
        }

        private void OnEntitySpawned(NPCPlayer npc)
        {
            ControllerSpawnPoint controller = _controllers.FirstOrDefault(s => s.RemoveOtherNpc && Vector3.Distance(s.transform.position, npc.transform.position) < (s.Size.x > s.Size.y ? s.Size.x : s.Size.y) * 2f && s.IsOtherNpc(npc));
            if (controller != null) NextTick(() => { if (npc.IsExists()) npc.Kill(); });
        }
        #endregion Oxide Hooks

        #region Day or Night
        private Coroutine _checkDayCoroutine = null;
        private bool _isDay;

        private IEnumerator CheckDay()
        {
            while (true)
            {
                if (_isDay)
                {
                    if (TOD_Sky.Instance.Cycle.Hour > TimeSpan.Parse(_config.StartNightTime).TotalHours || TOD_Sky.Instance.Cycle.Hour < TimeSpan.Parse(_config.StartDayTime).TotalHours)
                    {
                        _isDay = false;
                        foreach (ControllerSpawnPoint controller in _controllers) controller.SetDay(_isDay);
                    }
                }
                else
                {
                    if (TOD_Sky.Instance.Cycle.Hour < TimeSpan.Parse(_config.StartNightTime).TotalHours && TOD_Sky.Instance.Cycle.Hour > TimeSpan.Parse(_config.StartDayTime).TotalHours)
                    {
                        _isDay = true;
                        foreach (ControllerSpawnPoint controller in _controllers) controller.SetDay(_isDay);
                    }
                }
                yield return CoroutineEx.waitForSeconds(30f);
            }
        }
        #endregion Day or Night

        #region Controller
        private readonly HashSet<ControllerSpawnPoint> _controllers = new HashSet<ControllerSpawnPoint>();

        internal class ControllerSpawnPoint : FacepunchBehaviour
        {
            internal string Name;
            internal bool IsEvent;
            internal bool RemoveOtherNpc;
            internal Vector2 Size;
            internal List<PresetConfig> Presets;
            internal bool IsDay;

            internal HashSet<ActiveScientistNpc> ActiveNpc = new HashSet<ActiveScientistNpc>();
            private readonly HashSet<DeadScientistNpc> DeadNpc = new HashSet<DeadScientistNpc>();

            private int GetAmountPreset(PresetConfig preset) => ActiveNpc.Where(x => x.Preset == preset).Count + DeadNpc.Where(x => x.Preset == preset).Count;
            private int GetAmountPresetConfig(PresetConfig preset) => IsDay ? UnityEngine.Random.Range(preset.MinDay, preset.MaxDay) : UnityEngine.Random.Range(preset.MinNight, preset.MaxNight);

            private void OnDestroy()
            {
                CancelInvoke(ChangeDeadNpcTime);
                foreach (ActiveScientistNpc activeScientistNpc in ActiveNpc.Where(x => x.Npc.IsExists())) activeScientistNpc.Npc.Kill();
            }

            internal void Init()
            {
                if (RemoveOtherNpc)
                {
                    List<NPCPlayer> list = new List<NPCPlayer>();
                    Vis.Entities(transform.position, (Size.x > Size.y ? Size.x : Size.y) * 2f, list, 1 << 17);
                    foreach (NPCPlayer npc in list.Where(IsOtherNpc)) npc.Kill();
                }
                foreach (PresetConfig preset in Presets.Where(x => x.Enabled))
                {
                    int amount = GetAmountPresetConfig(preset);
                    for (int i = 0; i < amount; i++) SpawnNpc(preset);
                }
            }

            private void ChangeDeadNpcTime()
            {
                foreach (DeadScientistNpc deadScientistNpc in DeadNpc) deadScientistNpc.TimeToSpawn--;
                while (DeadNpc.Any(x => x.TimeToSpawn == 0))
                {
                    DeadScientistNpc deadScientistNpc = DeadNpc.FirstOrDefault(x => x.TimeToSpawn == 0);
                    int amountPresetConfig = GetAmountPresetConfig(deadScientistNpc.Preset);
                    int amountPreset = GetAmountPreset(deadScientistNpc.Preset) - 1;
                    if (amountPresetConfig > amountPreset) SpawnNpc(deadScientistNpc.Preset);
                    DeadNpc.Remove(deadScientistNpc);
                }
                if (DeadNpc.Count == 0) CancelInvoke(ChangeDeadNpcTime);
            }

            internal void SetDay(bool day)
            {
                IsDay = day;
                foreach (PresetConfig preset in Presets)
                {
                    int amountPresetConfig = GetAmountPresetConfig(preset);
                    int amountPreset = GetAmountPreset(preset);
                    if (amountPresetConfig > amountPreset)
                    {
                        int amount = amountPresetConfig - amountPreset;
                        for (int i = 0; i < amount; i++) SpawnNpc(preset);
                    }
                    else if (amountPresetConfig < amountPreset)
                    {
                        int amount = amountPreset - amountPresetConfig;
                        for (int i = 0; i < amount; i++) KillNpc(preset);
                    }
                }
            }

            private void SpawnNpc(PresetConfig preset)
            {
                Vector3 pos = Vector3.zero;
                int attempts = 0;
                while (pos == Vector3.zero && attempts < 100)
                {
                    attempts++;
                    if (Name == "Arid" || Name == "Temperate" || Name == "Tundra" || Name == "Arctic")
                    {
                        object point = _ins.NpcSpawn.Call("GetSpawnPoint", Name);
                        if (point is Vector3) pos = (Vector3)point;
                    }
                    else if (preset.TypeSpawn == 0) pos = GetRandomSpawnPos();
                    else pos = transform.TransformPoint(preset.OwnPositions.GetRandom().ToVector3());
                    if (ActiveNpc.Any(x => Vector3.Distance(x.Npc.transform.position, pos) < 5f)) pos = Vector3.zero;
                    if (!_ins._config.UnderwaterSpawnPoints.Contains(Name) && pos.y < -0.25f) pos = Vector3.zero;
                    if (pos != Vector3.zero && TriggerSafeZone.allSafeZones.Count > 0)
                    {
                        TriggerSafeZone nearSafeZone = TriggerSafeZone.allSafeZones.Min(x => Vector3.Distance(pos, x.transform.position));
                        if (nearSafeZone != null && Vector3.Distance(pos, nearSafeZone.transform.position) < _ins._config.SafeZoneRange) pos = Vector3.zero;
                    }
                }
                if (pos != Vector3.zero)
                {
                    ScientistNPC npc = (ScientistNPC)_ins.NpcSpawn.Call("SpawnNpc", pos, GetObjectConfig(preset.Config));
                    if (npc != null)
                    {
                        ActiveNpc.Add(new ActiveScientistNpc { Preset = preset, Npc = npc });
                        if (_ins._config.Pve && _ins.plugins.Exists("PveMode")) _ins.PveMode.Call("ScientistAddPveMode", npc);
                    }
                }
            }

            private void KillNpc(PresetConfig preset)
            {
                if (DeadNpc.Any(x => x.Preset == preset))
                {
                    DeadScientistNpc deadScientistNpc = DeadNpc.Where(x => x.Preset == preset).ToList().GetRandom();
                    DeadNpc.Remove(deadScientistNpc);
                }
                else
                {
                    ActiveScientistNpc activeScientistNpc = ActiveNpc.Where(x => x.Preset == preset).ToList().GetRandom();
                    if (activeScientistNpc.Npc.IsExists()) activeScientistNpc.Npc.Kill();
                    ActiveNpc.Remove(activeScientistNpc);
                }
            }

            internal void DieNpc(ScientistNPC npc, NPCPlayerCorpse corpse)
            {
                ActiveScientistNpc activeScientistNpc = ActiveNpc.FirstOrDefault(x => x.Npc == npc);
                PresetConfig preset = activeScientistNpc.Preset;
                if (!string.IsNullOrEmpty(preset.CratePrefab))
                {
                    BaseEntity entity = GameManager.server.CreateEntity(preset.CratePrefab, npc.transform.position, npc.transform.rotation);
                    if (entity == null) _ins.PrintWarning($"Unknown entity! ({preset.CratePrefab})");
                    else
                    {
                        entity.enableSaving = false;
                        entity.Spawn();
                    }
                }
                if (!IsEvent)
                {
                    DeadNpc.Add(new DeadScientistNpc { Preset = preset, TimeToSpawn = (int)UnityEngine.Random.Range(preset.Config.MinTime, preset.Config.MaxTime) });
                    if (DeadNpc.Count == 1) InvokeRepeating(ChangeDeadNpcTime, 1f, 1f);
                }
                ActiveNpc.Remove(activeScientistNpc);
                BasePlayer attacker = npc.lastAttacker as BasePlayer;
                if (attacker.IsPlayer()) _ins.SendBalance(attacker.userID, preset.Economic);
                _ins.NextTick(() =>
                {
                    if (preset.TypeLootTable == 1 || preset.TypeLootTable == 4 || preset.TypeLootTable == 5)
                    {
                        ItemContainer container = corpse.containers[0];
                        for (int i = container.itemList.Count - 1; i >= 0; i--)
                        {
                            Item item = container.itemList[i];
                            item.RemoveFromContainer();
                            item.Remove();
                        }
                        if (preset.TypeLootTable == 4 || preset.TypeLootTable == 5) _ins.AddToContainerPrefab(container, preset.PrefabLootTable);
                        if (preset.TypeLootTable == 1 || preset.TypeLootTable == 5) _ins.AddToContainerItem(container, preset.OwnLootTable);
                    }
                    if (preset.Config.IsRemoveCorpse && corpse.IsExists()) corpse.Kill();
                    if (IsEvent && ActiveNpc.Count == 0)
                    {
                        _ins._controllers.Remove(this);
                        Destroy(gameObject);
                    }
                });
            }

            internal bool IsOtherNpc(NPCPlayer npc)
            {
                if (!npc.IsExists()) return false;
                if (_ins._config.OtherNpc.Contains(npc.GetType().Name)) return false;
                if (_ins._config.OtherNpc.Contains(npc.displayName)) return false;
                if (_ins._config.OtherNpc.Contains(npc.skinID.ToString())) return false;
                if (npc.ShortPrefabName.Contains("scientistnpc_patrol") ||
                    npc.ShortPrefabName.Contains("scientistnpc_excavator") ||
                    npc.ShortPrefabName.Contains("scientistnpc_roamtethered") ||
                    npc.ShortPrefabName.Contains("scientistnpc_full") ||
                    npc.ShortPrefabName.Contains("scientistnpc_roam") ||
                    npc.ShortPrefabName.Contains("scientistnpc_oilrig") ||
                    npc.ShortPrefabName.Contains("npc_underwaterdweller") ||
                    npc.ShortPrefabName.Contains("npc_tunneldweller") ||
                    npc.ShortPrefabName.Contains("scarecrow")) return true;
                return false;
            }

            private Vector3 GetRandomSpawnPos()
            {
                RaycastHit raycastHit;
                NavMeshHit navmeshHit;
                int attempts = 0;
                while (attempts < 10)
                {
                    attempts++;
                    Vector3 pos = transform.TransformPoint(new Vector3(UnityEngine.Random.Range(-Size.x, Size.x), 500f, UnityEngine.Random.Range(-Size.y, Size.y)));
                    if (!Physics.Raycast(pos, Vector3.down, out raycastHit, 500f, 1 << 16 | 1 << 23)) continue;
                    pos.y = raycastHit.point.y;
                    if (!NavMesh.SamplePosition(pos, out navmeshHit, 2f, 1)) continue;
                    pos = navmeshHit.position;
                    if (pos.y < -0.25f) continue;
                    return pos;
                }
                return Vector3.zero;
            }

            private JObject GetObjectConfig(NpcConfig config)
            {
                HashSet<string> states = config.Stationary ? new HashSet<string> { "IdleState", "CombatStationaryState" } : new HashSet<string> { "RoamState", "ChaseState", "CombatState" };
                if (config.BeltItems.Any(x => x.ShortName == "rocket.launcher" || x.ShortName == "explosive.timed")) states.Add("RaidState");
                return new JObject
                {
                    ["Name"] = config.Names.GetRandom(),
                    ["WearItems"] = new JArray { config.WearItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["SkinID"] = x.SkinID }) },
                    ["BeltItems"] = new JArray { config.BeltItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["Amount"] = x.Amount, ["SkinID"] = x.SkinID, ["Mods"] = new JArray { x.Mods }, ["Ammo"] = x.Ammo }) },
                    ["Kit"] = config.Kits.GetRandom(),
                    ["Health"] = config.Health,
                    ["RoamRange"] = config.RoamRange,
                    ["ChaseRange"] = config.ChaseRange,
                    ["DamageScale"] = config.DamageScale,
                    ["TurretDamageScale"] = 1f,
                    ["AimConeScale"] = config.AimConeScale,
                    ["DisableRadio"] = config.DisableRadio,
                    ["CanUseWeaponMounted"] = true,
                    ["CanRunAwayWater"] = !_ins._config.UnderwaterSpawnPoints.Contains(Name),
                    ["Speed"] = config.Speed,
                    ["AreaMask"] = Name == "Large Oil Rig" || Name == "Oil Rig" || _ins._underwaterLabSpawnPoints.ContainsKey(Name) ? 25 : 1,
                    ["AgentTypeID"] = Name == "Large Oil Rig" || Name == "Oil Rig" || _ins._underwaterLabSpawnPoints.ContainsKey(Name) ? 0 : -1372625422,
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

            internal class ActiveScientistNpc { public PresetConfig Preset; public ScientistNPC Npc; }

            internal class DeadScientistNpc { public PresetConfig Preset; public int TimeToSpawn; }
        }
        #endregion Controller

        #region Economy
        [PluginReference] private readonly Plugin Economics, ServerRewards, IQEconomic;

        internal void SendBalance(ulong playerId, NpcEconomic economic)
        {
            if (plugins.Exists("Economics") && economic.Economics > 0) Economics.Call("Deposit", playerId.ToString(), economic.Economics);
            if (plugins.Exists("ServerRewards") && economic.ServerRewards > 0) ServerRewards.Call("AddPoints", playerId, economic.ServerRewards);
            if (plugins.Exists("IQEconomic") && economic.IQEconomic > 0) IQEconomic.Call("API_SET_BALANCE", playerId, economic.IQEconomic);
        }
        #endregion Economy

        #region Loot Spawn
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

        private void CheckAllLootTables()
        {
            foreach (KeyValuePair<string, MonumentSpawnPoint> dic in _monumentSpawnPoints)
            {
                foreach (PresetConfig preset in dic.Value.Presets.Where(x => x.Enabled))
                {
                    CheckLootTable(preset.OwnLootTable);
                    CheckPrefabLootTable(preset.PrefabLootTable);
                }
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Monument/{dic.Key}", dic.Value);
            }

            foreach (KeyValuePair<string, MonumentSpawnPoint> dic in _underwaterLabSpawnPoints)
            {
                foreach (PresetConfig preset in dic.Value.Presets.Where(x => x.Enabled))
                {
                    CheckLootTable(preset.OwnLootTable);
                    CheckPrefabLootTable(preset.PrefabLootTable);
                }
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Monument/Underwater Lab/{dic.Key}", dic.Value);
            }

            foreach (KeyValuePair<string, MonumentSpawnPoint> dic in _tunnelSpawnPoints)
            {
                foreach (PresetConfig preset in dic.Value.Presets.Where(x => x.Enabled))
                {
                    CheckLootTable(preset.OwnLootTable);
                    CheckPrefabLootTable(preset.PrefabLootTable);
                }
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Monument/Tunnel/{dic.Key}", dic.Value);
            }

            foreach (KeyValuePair<string, CustomSpawnPoint> dic in _customSpawnPoints)
            {
                foreach (PresetConfig preset in dic.Value.Presets.Where(x => x.Enabled))
                {
                    CheckLootTable(preset.OwnLootTable);
                    CheckPrefabLootTable(preset.PrefabLootTable);
                }
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Custom/{dic.Key}", dic.Value);
            }

            foreach (KeyValuePair<string, EventSpawnPoint> dic in _eventSpawnPoints)
            {
                foreach (PresetConfig preset in dic.Value.Presets.Where(x => x.Enabled))
                {
                    CheckLootTable(preset.OwnLootTable);
                    CheckPrefabLootTable(preset.PrefabLootTable);
                }
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Event/{dic.Key}", dic.Value);
            }

            foreach (KeyValuePair<string, RoadSpawnPoint> dic in _roadSpawnPoints)
            {
                foreach (PresetConfig preset in dic.Value.Presets.Where(x => x.Enabled))
                {
                    CheckLootTable(preset.OwnLootTable);
                    CheckPrefabLootTable(preset.PrefabLootTable);
                }
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Road/{dic.Key}", dic.Value);
            }

            foreach (KeyValuePair<string, BiomeSpawnPoint> dic in _biomeSpawnPoints)
            {
                foreach (PresetConfig preset in dic.Value.Presets.Where(x => x.Enabled))
                {
                    CheckLootTable(preset.OwnLootTable);
                    CheckPrefabLootTable(preset.PrefabLootTable);
                }
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Biome/{dic.Key}", dic.Value);
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

        private object CanPopulateLoot(ScientistNPC entity, NPCPlayerCorpse corpse)
        {
            if (entity == null || corpse == null) return null;
            ControllerSpawnPoint controller = _controllers.FirstOrDefault(x => x.ActiveNpc.Any(y => y.Npc == entity));
            if (controller != null)
            {
                ControllerSpawnPoint.ActiveScientistNpc activeScientist = controller.ActiveNpc.FirstOrDefault(x => x.Npc == entity);
                if (activeScientist != null)
                {
                    PresetConfig preset = activeScientist.Preset;
                    if (preset != null)
                    {
                        if (preset.TypeLootTable == 2) return null;
                        else return true;
                    }
                    else return null;
                }
                else return null;
            }
            else return null;
        }

        private object OnCustomLootNPC(uint netID)
        {
            ControllerSpawnPoint controller = _controllers.FirstOrDefault(x => x.ActiveNpc.Any(y => y.Npc != null && !y.Npc.IsDestroyed && y.Npc.net.ID == netID));
            if (controller != null)
            {
                ControllerSpawnPoint.ActiveScientistNpc activeScientist = controller.ActiveNpc.FirstOrDefault(x => x.Npc != null && !x.Npc.IsDestroyed && x.Npc.net.ID == netID);
                if (activeScientist != null)
                {
                    PresetConfig preset = activeScientist.Preset;
                    if (preset != null)
                    {
                        if (preset.TypeLootTable == 3) return null;
                        else return true;
                    }
                    else return null;
                }
                else return null;
            }
            else return null;
        }
        #endregion Loot Spawn

        #region Update Data Files
        private void UpdateMonumentDataFiles()
        {
            foreach (KeyValuePair<string, MonumentSpawnPoint> dic in _monumentSpawnPoints)
            {
                foreach (PresetConfig preset in dic.Value.Presets) foreach (NpcBelt belt in preset.Config.BeltItems) if (belt.Ammo == null) belt.Ammo = string.Empty;
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Monument/{dic.Key}", dic.Value);
            }
        }

        private void UpdateUnderwaterLabDataFiles()
        {
            foreach (KeyValuePair<string, MonumentSpawnPoint> dic in _underwaterLabSpawnPoints)
            {
                foreach (PresetConfig preset in dic.Value.Presets) foreach (NpcBelt belt in preset.Config.BeltItems) if (belt.Ammo == null) belt.Ammo = string.Empty;
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Monument/Underwater Lab/{dic.Key}", dic.Value);
            }
        }

        private void UpdateTunnelDataFiles()
        {
            foreach (KeyValuePair<string, MonumentSpawnPoint> dic in _tunnelSpawnPoints)
            {
                foreach (PresetConfig preset in dic.Value.Presets) foreach (NpcBelt belt in preset.Config.BeltItems) if (belt.Ammo == null) belt.Ammo = string.Empty;
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Monument/Tunnel/{dic.Key}", dic.Value);
            }
        }

        private void UpdateCustomDataFiles()
        {
            foreach (KeyValuePair<string, CustomSpawnPoint> dic in _customSpawnPoints)
            {
                foreach (PresetConfig preset in dic.Value.Presets) foreach (NpcBelt belt in preset.Config.BeltItems) if (belt.Ammo == null) belt.Ammo = string.Empty;
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Custom/{dic.Key}", dic.Value);
            }
        }

        private void UpdateEventDataFiles()
        {
            foreach (KeyValuePair<string, EventSpawnPoint> dic in _eventSpawnPoints)
            {
                foreach (PresetConfig preset in dic.Value.Presets) foreach (NpcBelt belt in preset.Config.BeltItems) if (belt.Ammo == null) belt.Ammo = string.Empty;
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Event/{dic.Key}", dic.Value);
            }
        }

        private void UpdateRoadDataFiles()
        {
            foreach (KeyValuePair<string, RoadSpawnPoint> dic in _roadSpawnPoints)
            {
                foreach (PresetConfig preset in dic.Value.Presets) foreach (NpcBelt belt in preset.Config.BeltItems) if (belt.Ammo == null) belt.Ammo = string.Empty;
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Road/{dic.Key}", dic.Value);
            }
        }

        private void UpdateBiomeDataFiles()
        {
            foreach (KeyValuePair<string, BiomeSpawnPoint> dic in _biomeSpawnPoints)
            {
                foreach (PresetConfig preset in dic.Value.Presets) foreach (NpcBelt belt in preset.Config.BeltItems) if (belt.Ammo == null) belt.Ammo = string.Empty;
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Biome/{dic.Key}", dic.Value);
            }
        }
        #endregion Update Data Files

        #region Load All Files
        private Coroutine _loadCoroutine = null;

        private IEnumerator LoadAllFiles()
        {
            LoadMonumentSpawnPoints();
            UpdateMonumentDataFiles();
            SpawnMonumentSpawnPoints();
            yield return CoroutineEx.waitForSeconds(1f);
            LoadUnderwaterLabSpawnPoints();
            UpdateUnderwaterLabDataFiles();
            SpawnUnderwaterLabSpawnPoints();
            yield return CoroutineEx.waitForSeconds(1f);
            LoadTunnelSpawnPoints();
            UpdateTunnelDataFiles();
            SpawnTunnelSpawnPoints();
            yield return CoroutineEx.waitForSeconds(1f);
            LoadCustomSpawnPoints();
            UpdateCustomDataFiles();
            SpawnCustomSpawnPoints();
            yield return CoroutineEx.waitForSeconds(1f);
            LoadEventSpawnPoints();
            UpdateEventDataFiles();
            yield return CoroutineEx.waitForSeconds(1f);
            LoadRoadSpawnPoints();
            UpdateRoadDataFiles();
            SpawnRoadSpawnPoints();
            yield return CoroutineEx.waitForSeconds(1f);
            LoadBiomeSpawnPoints();
            UpdateBiomeDataFiles();
            SpawnBiomeSpawnPoints();
            yield return CoroutineEx.waitForSeconds(1f);
            CheckAllLootTables();
            _checkDayCoroutine = ServerMgr.Instance.StartCoroutine(CheckDay());
        }
        #endregion Load All Files

        #region Monuments
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

        private bool IsNecessaryMonument(MonumentInfo monument)
        {
            string name = GetNameMonument(monument);
            if (string.IsNullOrEmpty(name) || _unnecessaryMonuments.Contains(name)) return false;
            return _monumentSpawnPoints.Any(x => x.Key == name && x.Value.Enabled);
        }

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

        public class MonumentSpawnPoint
        {
            [JsonProperty(En ? "Enabled? [true/false]" : "Включен? [true/false]")] public bool Enabled { get; set; }
            [JsonProperty(En ? "The size of the monument" : "Размер монумента")] public string Size { get; set; }
            [JsonProperty(En ? "Remove other NPCs? [true/false]" : "Удалить других NPC? [true/false]")] public bool RemoveOtherNpc { get; set; }
            [JsonProperty(En ? "Presets" : "Наборы NPC")] public List<PresetConfig> Presets { get; set; }
        }

        private readonly Dictionary<string, MonumentSpawnPoint> _monumentSpawnPoints = new Dictionary<string, MonumentSpawnPoint>();

        private void LoadMonumentSpawnPoints()
        {
            foreach (string name in Interface.Oxide.DataFileSystem.GetFiles("BetterNpc/Monument/"))
            {
                string fileName = name.Split('/').Last().Split('.').First();
                MonumentSpawnPoint spawnPoint = Interface.Oxide.DataFileSystem.ReadObject<MonumentSpawnPoint>($"BetterNpc/Monument/{fileName}");
                if (spawnPoint != null)
                {
                    Puts($"File {fileName} has been loaded successfully!");
                    _monumentSpawnPoints.Add(fileName, spawnPoint);
                }
                else PrintError($"File {fileName} is corrupted and cannot be loaded!");
            }
        }

        private void SpawnMonumentSpawnPoints()
        {
            foreach (MonumentInfo monument in TerrainMeta.Path.Monuments.Where(IsNecessaryMonument))
            {
                string monumentName = GetNameMonument(monument);
                MonumentSpawnPoint spawnPoint = _monumentSpawnPoints[monumentName];
                ControllerSpawnPoint controller = new GameObject().AddComponent<ControllerSpawnPoint>();
                controller.transform.position = monument.transform.position;
                controller.transform.rotation = monument.transform.rotation;
                controller.Name = monumentName;
                controller.IsEvent = false;
                controller.RemoveOtherNpc = spawnPoint.RemoveOtherNpc;
                controller.Size = new Vector2(spawnPoint.Size.ToVector3().x, spawnPoint.Size.ToVector3().z);
                controller.Presets = spawnPoint.Presets.Where(x => x.Enabled).ToList();
                controller.IsDay = _isDay;
                controller.Init();
                _controllers.Add(controller);
                Puts($"Monument {monumentName} has been successfully loaded!");
            }
        }

        private readonly Dictionary<string, MonumentSpawnPoint> _underwaterLabSpawnPoints = new Dictionary<string, MonumentSpawnPoint>();

        private void LoadUnderwaterLabSpawnPoints()
        {
            foreach (string name in Interface.Oxide.DataFileSystem.GetFiles("BetterNpc/Monument/Underwater Lab/"))
            {
                string fileName = name.Split('/').Last().Split('.').First();
                MonumentSpawnPoint spawnPoint = Interface.Oxide.DataFileSystem.ReadObject<MonumentSpawnPoint>($"BetterNpc/Monument/Underwater Lab/{fileName}");
                if (spawnPoint != null)
                {
                    Puts($"File {fileName} has been loaded successfully!");
                    _underwaterLabSpawnPoints.Add(fileName, spawnPoint);
                }
                else PrintError($"File {fileName} is corrupted and cannot be loaded!");
            }
        }

        private void SpawnUnderwaterLabSpawnPoints()
        {
            foreach (DungeonBaseInfo baseModule in TerrainMeta.Path.DungeonBaseEntrances)
            {
                if (_underwaterLabSpawnPoints.ContainsKey(baseModule.name)) SpawnUnderwaterLabSpawnPoint(baseModule.name, baseModule.transform);
                foreach (GameObject module in baseModule.Links)
                {
                    string moduleName = module.name.Split('/').Last().Split('.').First();
                    if (_underwaterLabSpawnPoints.ContainsKey(moduleName)) SpawnUnderwaterLabSpawnPoint(moduleName, module.transform);
                }
            }
        }

        private void SpawnUnderwaterLabSpawnPoint(string moduleName, Transform transform)
        {
            MonumentSpawnPoint spawnPoint = _underwaterLabSpawnPoints[moduleName];
            if (!spawnPoint.Enabled) return;
            ControllerSpawnPoint controller = new GameObject().AddComponent<ControllerSpawnPoint>();
            controller.transform.position = transform.position;
            controller.transform.rotation = transform.rotation;
            controller.Name = moduleName;
            controller.IsEvent = false;
            controller.RemoveOtherNpc = spawnPoint.RemoveOtherNpc;
            controller.Size = new Vector2(spawnPoint.Size.ToVector3().x, spawnPoint.Size.ToVector3().z);
            controller.Presets = spawnPoint.Presets.Where(x => x.Enabled).ToList();
            controller.IsDay = _isDay;
            controller.Init();
            _controllers.Add(controller);
            Puts($"Underwater Module {moduleName} has been successfully loaded!");
        }

        private readonly Dictionary<string, MonumentSpawnPoint> _tunnelSpawnPoints = new Dictionary<string, MonumentSpawnPoint>();

        private void LoadTunnelSpawnPoints()
        {
            foreach (string name in Interface.Oxide.DataFileSystem.GetFiles("BetterNpc/Monument/Tunnel/"))
            {
                string fileName = name.Split('/').Last().Split('.').First();
                MonumentSpawnPoint spawnPoint = Interface.Oxide.DataFileSystem.ReadObject<MonumentSpawnPoint>($"BetterNpc/Monument/Tunnel/{fileName}");
                if (spawnPoint != null)
                {
                    Puts($"File {fileName} has been loaded successfully!");
                    _tunnelSpawnPoints.Add(fileName, spawnPoint);
                }
                else PrintError($"File {fileName} is corrupted and cannot be loaded!");
            }
        }

        private void SpawnTunnelSpawnPoints()
        {
            foreach (DungeonGridCell gridCell in TerrainMeta.Path.DungeonGridCells)
            {
                string cellName = gridCell.name.Split('/').Last().Split('.').First();
                if (_tunnelSpawnPoints.ContainsKey(cellName))
                {
                    MonumentSpawnPoint spawnPoint = _tunnelSpawnPoints[cellName];
                    if (!spawnPoint.Enabled) continue;
                    ControllerSpawnPoint controller = new GameObject().AddComponent<ControllerSpawnPoint>();
                    controller.transform.position = gridCell.transform.position;
                    controller.transform.rotation = gridCell.transform.rotation;
                    controller.Name = cellName;
                    controller.IsEvent = false;
                    controller.RemoveOtherNpc = spawnPoint.RemoveOtherNpc;
                    controller.Size = new Vector2(spawnPoint.Size.ToVector3().x, spawnPoint.Size.ToVector3().z);
                    controller.Presets = spawnPoint.Presets.Where(x => x.Enabled).ToList();
                    controller.IsDay = _isDay;
                    controller.Init();
                    _controllers.Add(controller);
                    Puts($"Tunnel Module {cellName} has been successfully loaded!");
                }
            }
        }
        #endregion Monuments

        #region Custom
        public class CustomSpawnPoint
        {
            [JsonProperty(En ? "Enabled? [true/false]" : "Включен? [true/false]")] public bool Enabled { get; set; }
            [JsonProperty(En ? "Position" : "Позиция")] public string Position { get; set; }
            [JsonProperty(En ? "Rotation" : "Вращение")] public string Rotation { get; set; }
            [JsonProperty(En ? "Radius" : "Радиус")] public float Radius { get; set; }
            [JsonProperty(En ? "Remove other NPCs? [true/false]" : "Удалить других NPC? [true/false]")] public bool RemoveOtherNpc { get; set; }
            [JsonProperty(En ? "Presets" : "Наборы NPC")] public List<PresetConfig> Presets { get; set; }
        }

        private readonly Dictionary<string, CustomSpawnPoint> _customSpawnPoints = new Dictionary<string, CustomSpawnPoint>();

        private void LoadCustomSpawnPoints()
        {
            foreach (string name in Interface.Oxide.DataFileSystem.GetFiles("BetterNpc/Custom/"))
            {
                string fileName = name.Split('/').Last().Split('.').First();
                CustomSpawnPoint spawnPoint = Interface.Oxide.DataFileSystem.ReadObject<CustomSpawnPoint>($"BetterNpc/Custom/{fileName}");
                if (spawnPoint != null)
                {
                    Puts($"File {fileName} has been loaded successfully!");
                    _customSpawnPoints.Add(fileName, spawnPoint);
                }
                else PrintError($"File {fileName} is corrupted and cannot be loaded!");
            }
        }

        private void SpawnCustomSpawnPoints()
        {
            foreach (KeyValuePair<string, CustomSpawnPoint> dic in _customSpawnPoints.Where(x => x.Value.Enabled))
            {
                ControllerSpawnPoint controller = new GameObject().AddComponent<ControllerSpawnPoint>();
                controller.transform.position = dic.Value.Position.ToVector3();
                controller.transform.rotation = Quaternion.Euler(dic.Value.Rotation.ToVector3());
                controller.Name = dic.Key;
                controller.IsEvent = false;
                controller.RemoveOtherNpc = dic.Value.RemoveOtherNpc;
                controller.Size = new Vector2(dic.Value.Radius, dic.Value.Radius);
                controller.Presets = dic.Value.Presets.Where(x => x.Enabled).ToList();
                controller.IsDay = _isDay;
                controller.Init();
                _controllers.Add(controller);
                Puts($"Custom location {dic.Key} has been successfully loaded!");
            }
        }
        #endregion Custom

        #region Events
        public class EventSpawnPoint
        {
            [JsonProperty(En ? "Enabled? [true/false]" : "Включен? [true/false]")] public bool Enabled { get; set; }
            [JsonProperty(En ? "Radius" : "Радиус")] public float Radius { get; set; }
            [JsonProperty(En ? "Presets" : "Наборы NPC")] public List<PresetConfig> Presets { get; set; }
        }

        private readonly Dictionary<string, EventSpawnPoint> _eventSpawnPoints = new Dictionary<string, EventSpawnPoint>();

        private void LoadEventSpawnPoints()
        {
            foreach (string name in Interface.Oxide.DataFileSystem.GetFiles("BetterNpc/Event/"))
            {
                string fileName = name.Split('/').Last().Split('.').First();
                EventSpawnPoint spawnPoint = Interface.Oxide.DataFileSystem.ReadObject<EventSpawnPoint>($"BetterNpc/Event/{fileName}");
                if (spawnPoint != null)
                {
                    Puts($"File {fileName} has been loaded successfully!");
                    _eventSpawnPoints.Add(fileName, spawnPoint);
                }
                else PrintError($"File {fileName} is corrupted and cannot be loaded!");
            }
        }

        #region AirDrop
        private readonly HashSet<CargoPlane> _cargoPlanesSignaled = new HashSet<CargoPlane>();

        private void OnCargoPlaneSignaled(CargoPlane cargoPlane, SupplySignal supplySignal) { if (!_cargoPlanesSignaled.Contains(cargoPlane)) _cargoPlanesSignaled.Add(cargoPlane); }

        private void OnSupplyDropDropped(SupplyDrop supplyDrop, CargoPlane cargoPlane)
        {
            if (_cargoPlanesSignaled.Contains(cargoPlane)) _cargoPlanesSignaled.Remove(cargoPlane);
            else if (_eventSpawnPoints["AirDrop"].Enabled)
            {
                if (Interface.CallHook("CanAirDropSpawnNpc", supplyDrop) is bool) return;
                Vector3 pos = supplyDrop.transform.position;
                pos.y = TerrainMeta.HeightMap.GetHeight(pos);
                ControllerSpawnPoint controller = new GameObject().AddComponent<ControllerSpawnPoint>();
                controller.transform.position = pos;
                controller.Name = supplyDrop.net.ID.ToString();
                controller.IsEvent = true;
                controller.RemoveOtherNpc = false;
                controller.Size = new Vector2(_eventSpawnPoints["AirDrop"].Radius, _eventSpawnPoints["AirDrop"].Radius);
                controller.Presets = _eventSpawnPoints["AirDrop"].Presets.Where(x => x.Enabled).ToList();
                controller.IsDay = _isDay;
                controller.Init();
                _controllers.Add(controller);
            }
        }

        private void OnEntityKill(SupplyDrop supplyDrop)
        {
            ControllerSpawnPoint controller = _controllers.FirstOrDefault(x => x.Name == supplyDrop.net.ID.ToString());
            if (controller != null)
            {
                _controllers.Remove(controller);
                UnityEngine.Object.Destroy(controller.gameObject);
            }
        }
        #endregion AirDrop

        #region CH47
        private void OnHelicopterDropCrate(CH47HelicopterAIController ai)
        {
            if (_eventSpawnPoints["CH47"].Enabled)
            {
                if (Interface.CallHook("CanCh47SpawnNpc", ai) is bool) return;
                Vector3 pos = ai.transform.position;
                pos.y = TerrainMeta.HeightMap.GetHeight(pos);
                ControllerSpawnPoint controller = new GameObject().AddComponent<ControllerSpawnPoint>();
                controller.transform.position = pos;
                controller.Name = ai.net.ID.ToString();
                controller.IsEvent = true;
                controller.RemoveOtherNpc = false;
                controller.Size = new Vector2(_eventSpawnPoints["CH47"].Radius, _eventSpawnPoints["CH47"].Radius);
                controller.Presets = _eventSpawnPoints["CH47"].Presets.Where(x => x.Enabled).ToList();
                controller.IsDay = _isDay;
                controller.Init();
                _controllers.Add(controller);
            }
        }

        private void OnEntityKill(HackableLockedCrate crate)
        {
            ControllerSpawnPoint controller = _controllers.FirstOrDefault(x => x.Name == crate.net.ID.ToString());
            if (controller != null)
            {
                _controllers.Remove(controller);
                UnityEngine.Object.Destroy(controller.gameObject);
            }
        }
        #endregion CH47

        #region Bradley and Helicopter
        private readonly Dictionary<string, KeyValuePair<Vector3, HashSet<LockedByEntCrate>>> _bradleyCrates = new Dictionary<string, KeyValuePair<Vector3, HashSet<LockedByEntCrate>>>();

        private readonly Dictionary<string, KeyValuePair<Vector3, HashSet<LockedByEntCrate>>> _helicopterCrates = new Dictionary<string, KeyValuePair<Vector3, HashSet<LockedByEntCrate>>>();

        private void OnEntityDeath(BradleyAPC bradley, HitInfo info)
        {
            if (_eventSpawnPoints.ContainsKey("Bradley") && _eventSpawnPoints["Bradley"].Enabled)
            {
                if (Interface.CallHook("CanBradleySpawnNpc", bradley) is bool) return;
                Vector3 pos = bradley.transform.position;
                pos.y = TerrainMeta.HeightMap.GetHeight(pos);
                ControllerSpawnPoint controller = new GameObject().AddComponent<ControllerSpawnPoint>();
                controller.transform.position = pos;
                controller.Name = bradley.net.ID.ToString();
                controller.IsEvent = true;
                controller.RemoveOtherNpc = false;
                controller.Size = new Vector2(_eventSpawnPoints["Bradley"].Radius, _eventSpawnPoints["Bradley"].Radius);
                controller.Presets = _eventSpawnPoints["Bradley"].Presets.Where(x => x.Enabled).ToList();
                controller.IsDay = _isDay;
                controller.Init();
                _controllers.Add(controller);
                _bradleyCrates.Add(bradley.net.ID.ToString(), new KeyValuePair<Vector3, HashSet<LockedByEntCrate>>(bradley.transform.position, new HashSet<LockedByEntCrate>()));
            }
        }

        private void OnEntityDeath(BaseHelicopter helicopter, HitInfo info)
        {
            if (_eventSpawnPoints.ContainsKey("Helicopter") && _eventSpawnPoints["Helicopter"].Enabled)
            {
                if (Interface.CallHook("CanHelicopterSpawnNpc", helicopter) is bool) return;
                Vector3 pos = helicopter.transform.position;
                pos.y = TerrainMeta.HeightMap.GetHeight(pos);
                ControllerSpawnPoint controller = new GameObject().AddComponent<ControllerSpawnPoint>();
                controller.transform.position = pos;
                controller.Name = helicopter.net.ID.ToString();
                controller.IsEvent = true;
                controller.RemoveOtherNpc = false;
                controller.Size = new Vector2(_eventSpawnPoints["Helicopter"].Radius, _eventSpawnPoints["Helicopter"].Radius);
                controller.Presets = _eventSpawnPoints["Helicopter"].Presets.Where(x => x.Enabled).ToList();
                controller.IsDay = _isDay;
                controller.Init();
                _controllers.Add(controller);
                _helicopterCrates.Add(helicopter.net.ID.ToString(), new KeyValuePair<Vector3, HashSet<LockedByEntCrate>>(helicopter.transform.position, new HashSet<LockedByEntCrate>()));
            }
        }

        private void OnEntitySpawned(LockedByEntCrate crate)
        {
            if (crate.ShortPrefabName == "bradley_crate" && _bradleyCrates.Any(x => Vector3.Distance(x.Value.Key, crate.transform.position) < 10f))
                _bradleyCrates.FirstOrDefault(x => Vector3.Distance(x.Value.Key, crate.transform.position) < 10f).Value.Value.Add(crate);

            if (crate.ShortPrefabName == "heli_crate" && _helicopterCrates.Any(x => Vector3.Distance(x.Value.Key, crate.transform.position) < 10f))
                _helicopterCrates.FirstOrDefault(x => Vector3.Distance(x.Value.Key, crate.transform.position) < 10f).Value.Value.Add(crate);
        }

        private void OnEntityKill(LockedByEntCrate crate)
        {
            if (crate.ShortPrefabName == "bradley_crate" && _bradleyCrates.Any(x => x.Value.Value.Contains(crate)))
            {
                KeyValuePair<string, KeyValuePair<Vector3, HashSet<LockedByEntCrate>>> dic = _bradleyCrates.FirstOrDefault(x => x.Value.Value.Contains(crate));
                dic.Value.Value.Remove(crate);
                if (dic.Value.Value.Count == 0)
                {
                    ControllerSpawnPoint controller = _controllers.FirstOrDefault(x => x.Name == dic.Key);
                    _controllers.Remove(controller);
                    if (controller != null) UnityEngine.Object.Destroy(controller.gameObject);
                    _bradleyCrates.Remove(dic.Key);
                }
            }

            if (crate.ShortPrefabName == "heli_crate" && _helicopterCrates.Any(x => x.Value.Value.Contains(crate)))
            {
                KeyValuePair<string, KeyValuePair<Vector3, HashSet<LockedByEntCrate>>> dic = _helicopterCrates.FirstOrDefault(x => x.Value.Value.Contains(crate));
                dic.Value.Value.Remove(crate);
                if (dic.Value.Value.Count == 0)
                {
                    ControllerSpawnPoint controller = _controllers.FirstOrDefault(x => x.Name == dic.Key);
                    _controllers.Remove(controller);
                    if (controller != null) UnityEngine.Object.Destroy(controller.gameObject);
                    _helicopterCrates.Remove(dic.Key);
                }
            }
        }
        #endregion Bradley and Helicopter
        #endregion Events

        #region Roads
        public class RoadSpawnPoint
        {
            [JsonProperty(En ? "Enabled? [true/false]" : "Включен? [true/false]")] public bool Enabled { get; set; }
            [JsonProperty(En ? "Presets" : "Наборы NPC")] public List<PresetConfig> Presets { get; set; }
        }

        private readonly Dictionary<string, RoadSpawnPoint> _roadSpawnPoints = new Dictionary<string, RoadSpawnPoint>();

        private readonly Dictionary<string, HashSet<string>> _roadPositions = new Dictionary<string, HashSet<string>>
        {
            ["ExtraNarrow"] = new HashSet<string>(),
            ["ExtraWide"] = new HashSet<string>(),
            ["Standard"] = new HashSet<string>()
        };

        private void LoadRoadSpawnPoints()
        {
            foreach (string name in Interface.Oxide.DataFileSystem.GetFiles("BetterNpc/Road/"))
            {
                string fileName = name.Split('/').Last().Split('.').First();
                RoadSpawnPoint spawnPoint = Interface.Oxide.DataFileSystem.ReadObject<RoadSpawnPoint>($"BetterNpc/Road/{fileName}");
                if (spawnPoint != null)
                {
                    Puts($"File {fileName} has been loaded successfully!");
                    _roadSpawnPoints.Add(fileName, spawnPoint);
                }
                else PrintError($"File {fileName} is corrupted and cannot be loaded!");
            }
            if (_roadSpawnPoints.Count > 0 && _roadSpawnPoints.Values.Any(x => x.Enabled))
            {
                foreach (PathList path in TerrainMeta.Path.Roads)
                {
                    string name = path.Width < 5f ? "ExtraNarrow" : path.Width > 10 ? "ExtraWide" : "Standard";
                    foreach (Vector3 vector3 in path.Path.Points) _roadPositions[name].Add(vector3.ToString());
                }
                foreach (KeyValuePair<string, HashSet<string>> dic in _roadPositions)
                {
                    Puts($"Found {dic.Value.Count} points of road {dic.Key} on the map");
                    if (dic.Value.Count == 0 && _roadSpawnPoints[dic.Key].Enabled) _roadSpawnPoints[dic.Key].Enabled = false;
                }
            }
        }

        private void SpawnRoadSpawnPoints()
        {
            foreach (KeyValuePair<string, RoadSpawnPoint> dic in _roadSpawnPoints.Where(x => x.Value.Enabled))
            {
                ControllerSpawnPoint controller = new GameObject().AddComponent<ControllerSpawnPoint>();
                controller.Name = dic.Key;
                controller.IsEvent = false;
                controller.RemoveOtherNpc = false;
                controller.Size = new Vector2();
                controller.Presets = dic.Value.Presets.Where(x => x.Enabled).ToList();
                controller.IsDay = _isDay;
                foreach (PresetConfig preset in controller.Presets)
                {
                    preset.TypeSpawn = 1;
                    preset.OwnPositions = _roadPositions[dic.Key].ToList();
                }
                controller.Init();
                _controllers.Add(controller);
                Puts($"Road {dic.Key} has been successfully loaded!");
            }
        }
        #endregion Roads

        #region Biomes
        public class BiomeSpawnPoint
        {
            [JsonProperty(En ? "Enabled? [true/false]" : "Включен? [true/false]")] public bool Enabled { get; set; }
            [JsonProperty(En ? "Presets" : "Наборы NPC")] public List<PresetConfig> Presets { get; set; }
        }

        private readonly Dictionary<string, BiomeSpawnPoint> _biomeSpawnPoints = new Dictionary<string, BiomeSpawnPoint>();

        private void LoadBiomeSpawnPoints()
        {
            foreach (string name in Interface.Oxide.DataFileSystem.GetFiles("BetterNpc/Biome/"))
            {
                string fileName = name.Split('/').Last().Split('.').First();
                BiomeSpawnPoint spawnPoint = Interface.Oxide.DataFileSystem.ReadObject<BiomeSpawnPoint>($"BetterNpc/Biome/{fileName}");
                if (spawnPoint != null)
                {
                    Puts($"File {fileName} has been loaded successfully!");
                    _biomeSpawnPoints.Add(fileName, spawnPoint);
                }
                else PrintError($"File {fileName} is corrupted and cannot be loaded!");
            }
        }

        private void SpawnBiomeSpawnPoints()
        {
            foreach (KeyValuePair<string, BiomeSpawnPoint> dic in _biomeSpawnPoints.Where(x => x.Value.Enabled))
            {
                ControllerSpawnPoint controller = new GameObject().AddComponent<ControllerSpawnPoint>();
                controller.Name = dic.Key;
                controller.IsEvent = false;
                controller.RemoveOtherNpc = false;
                controller.Size = new Vector2();
                controller.Presets = dic.Value.Presets.Where(x => x.Enabled).ToList();
                controller.IsDay = _isDay;
                controller.Init();
                _controllers.Add(controller);
                Puts($"Biome {dic.Key} has been successfully loaded!");
            }
        }
        #endregion Biomes

        #region Commands
        [ChatCommand("SpawnPointPos")]
        private void ChatCommandSpawnPointPos(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;

            if (args == null || args.Length == 0)
            {
                PrintToChat(player, "You <color=#ce3f27>didn't</color> write the name of the spawn point!");
                return;
            }

            string name = ""; foreach (string arg in args) name += arg != args.Last() ? arg + " " : arg;

            if (!_controllers.Any(x => x.Name == name))
            {
                PrintToChat(player, $"Spawn Point with the name <color=#55aaff>{name}</color> was <color=#ce3f27>not</color> found!");
                return;
            }

            ControllerSpawnPoint controller = _controllers.Where(x => x.Name == name).Min(x => Vector3.Distance(x.transform.position, player.transform.position));
            Vector3 pos = controller.transform.InverseTransformPoint(player.transform.position);

            Puts($"Spawn Point: {name}. Position: {pos}");
            PrintToChat(player, $"Spawn Point: <color=#55aaff>{name}</color>\nPosition: <color=#55aaff>{pos}</color>");
        }

        [ChatCommand("SpawnPointAdd")]
        private void ChatCommandSpawnPointAdd(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            if (args == null || args.Length == 0)
            {
                PrintToChat(player, "You <color=#ce3f27>didn't</color> write the name of the spawn point!");
                return;
            }
            string name = ""; foreach (string arg in args) name += arg != args.Last() ? arg + " " : arg;
            CustomSpawnPoint spawnPoint = new CustomSpawnPoint
            {
                Enabled = true,
                Position = player.transform.position.ToString(),
                Rotation = Vector3.zero.ToString(),
                Radius = 20f,
                RemoveOtherNpc = true,
                Presets = new List<PresetConfig>
                {
                    new PresetConfig
                    {
                        Enabled = true,
                        MinDay = 2,
                        MaxDay = 4,
                        MinNight = 1,
                        MaxNight = 2,
                        Config = new NpcConfig
                        {
                            Names = new List<string> { "Scientist", "Soldier" },
                            Health = 200f,
                            RoamRange = 10f,
                            ChaseRange = 100f,
                            AttackRangeMultiplier = 1f,
                            SenseRange = 50f,
                            MemoryDuration = 10f,
                            DamageScale = 2.0f,
                            AimConeScale = 1f,
                            CheckVisionCone = false,
                            VisionCone = 135f,
                            Speed = 7.5f,
                            MinTime = 600f,
                            MaxTime = 900f,
                            DisableRadio = false,
                            Stationary = false,
                            IsRemoveCorpse = true,
                            WearItems = new List<NpcWear> { new NpcWear { ShortName = "hazmatsuit_scientist", SkinID = 0 } },
                            BeltItems = new List<NpcBelt>
                            {
                                new NpcBelt { ShortName = "rifle.lr300", Amount = 1, SkinID = 0, Mods = new List<string> { "weapon.mod.holosight", "weapon.mod.flashlight" }, Ammo = string.Empty },
                                new NpcBelt { ShortName = "syringe.medical", Amount = 10, SkinID = 0, Mods = new List<string>(), Ammo = string.Empty },
                                new NpcBelt { ShortName = "grenade.f1", Amount = 10, SkinID = 0, Mods = new List<string>(), Ammo = string.Empty },
                                new NpcBelt { ShortName = "grenade.smoke", Amount = 10, SkinID = 0, Mods = new List<string>(), Ammo = string.Empty },
                                new NpcBelt { ShortName = "explosive.timed", Amount = 10, SkinID = 0, Mods = new List<string>(), Ammo = string.Empty },
                                new NpcBelt { ShortName = "rocket.launcher", Amount = 1, SkinID = 0, Mods = new List<string>(), Ammo = string.Empty }
                            },
                            Kits = new List<string>()
                        },
                        Economic = new NpcEconomic { Economics = 0, ServerRewards = 0, IQEconomic = 0 },
                        TypeSpawn = 0,
                        OwnPositions = new List<string>(),
                        CratePrefab = "",
                        TypeLootTable = 4,
                        PrefabLootTable = new PrefabLootTableConfig { Min = 1, Max = 1, UseCount = true, Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } } },
                        OwnLootTable = new LootTableConfig { Min = 1, Max = 1, UseCount = true, Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 100, MaxAmount = 200, Chance = 50.0f, IsBluePrint = false, SkinID = 0, Name = "" } } }
                    }
                }
            };
            Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Custom/{name}", spawnPoint);
            PrintToChat(player, $"You <color=#738d43>have successfully added</color> a new spawn point named <color=#55aaff>{name}</color>. You <color=#738d43>can edit</color> this spawn point in the file <color=#55aaff>BetterNpc/Custom/{name}</color>");
            _customSpawnPoints.Add(name, spawnPoint);
            ControllerSpawnPoint controller = new GameObject().AddComponent<ControllerSpawnPoint>();
            controller.transform.position = spawnPoint.Position.ToVector3();
            controller.transform.rotation = Quaternion.Euler(spawnPoint.Rotation.ToVector3());
            controller.Name = name;
            controller.IsEvent = false;
            controller.RemoveOtherNpc = spawnPoint.RemoveOtherNpc;
            controller.Size = new Vector2(spawnPoint.Radius, spawnPoint.Radius);
            controller.Presets = spawnPoint.Presets.Where(x => x.Enabled).ToList();
            controller.IsDay = _isDay;
            controller.Init();
            _controllers.Add(controller);
            Puts($"Custom location {name} has been successfully loaded!");
        }

        [ChatCommand("SpawnPointAddPos")]
        private void ChatCommandSpawnPointAddPos(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;

            if (args == null || args.Length == 0)
            {
                PrintToChat(player, "You <color=#ce3f27>didn't</color> write the preset number!");
                return;
            }

            if (args.Length == 1)
            {
                PrintToChat(player, "You <color=#ce3f27>didn't</color> write the name of the spawn point!");
                return;
            }

            int number = Convert.ToInt32(args[0]) - 1;
            if (number < 0) return;
            string name = ""; foreach (string arg in args.Skip(1)) name += arg != args.Last() ? arg + " " : arg;

            if (!_controllers.Any(x => x.Name == name))
            {
                PrintToChat(player, $"Spawn Point with the name <color=#55aaff>{name}</color> was <color=#ce3f27>not</color> found!");
                return;
            }

            ControllerSpawnPoint controller = _controllers.Where(x => x.Name == name).Min(x => Vector3.Distance(x.transform.position, player.transform.position));

            if (_monumentSpawnPoints.ContainsKey(name))
            {
                MonumentSpawnPoint spawnPoint = _monumentSpawnPoints[name];
                if (spawnPoint.Presets.Count < number + 1) return;
                Vector3 position = controller.transform.InverseTransformPoint(player.transform.position);
                spawnPoint.Presets[number].OwnPositions.Add(position.ToString());
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Monument/{name}", spawnPoint);
                PrintToChat(player, $"You <color=#738d43>have successfully saved</color> a new position for Spawn Point <color=#55aaff>{name}</color> and for preset <color=#55aaff>{number + 1}</color>!");
            }
            else if (_underwaterLabSpawnPoints.ContainsKey(name))
            {
                MonumentSpawnPoint spawnPoint = _underwaterLabSpawnPoints[name];
                if (spawnPoint.Presets.Count < number + 1) return;
                Vector3 position = controller.transform.InverseTransformPoint(player.transform.position);
                spawnPoint.Presets[number].OwnPositions.Add(position.ToString());
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Monument/Underwater Lab/{name}", spawnPoint);
                PrintToChat(player, $"You <color=#738d43>have successfully saved</color> a new position for Spawn Point <color=#55aaff>{name}</color> and for preset <color=#55aaff>{number + 1}</color>!");
            }
            else if (_tunnelSpawnPoints.ContainsKey(name))
            {
                MonumentSpawnPoint spawnPoint = _tunnelSpawnPoints[name];
                if (spawnPoint.Presets.Count < number + 1) return;
                Vector3 position = controller.transform.InverseTransformPoint(player.transform.position);
                spawnPoint.Presets[number].OwnPositions.Add(position.ToString());
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Monument/Tunnel/{name}", spawnPoint);
                PrintToChat(player, $"You <color=#738d43>have successfully saved</color> a new position for Spawn Point <color=#55aaff>{name}</color> and for preset <color=#55aaff>{number + 1}</color>!");
            }
            else if (_customSpawnPoints.ContainsKey(name))
            {
                CustomSpawnPoint spawnPoint = _customSpawnPoints[name];
                if (spawnPoint.Presets.Count < number + 1) return;
                Vector3 position = controller.transform.InverseTransformPoint(player.transform.position);
                spawnPoint.Presets[number].OwnPositions.Add(position.ToString());
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Custom/{name}", spawnPoint);
                PrintToChat(player, $"You <color=#738d43>have successfully saved</color> a new position for Spawn Point <color=#55aaff>{name}</color> and for preset <color=#55aaff>{number + 1}</color>!");
            }
        }

        [ChatCommand("SpawnPointAddWear")]
        private void ChatCommandSpawnPointAddWear(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;

            if (args == null || args.Length == 0)
            {
                PrintToChat(player, "You <color=#ce3f27>didn't</color> write the preset number!");
                return;
            }

            if (args.Length == 1)
            {
                PrintToChat(player, "You <color=#ce3f27>didn't</color> write the name of the spawn point!");
                return;
            }

            int number = Convert.ToInt32(args[0]) - 1;
            if (number < 0) return;
            string name = ""; foreach (string arg in args.Skip(1)) name += arg != args.Last() ? arg + " " : arg;

            List<NpcWear> wears = player.inventory.containerWear.itemList.Select(x => new NpcWear { ShortName = x.info.shortname, SkinID = x.skin });

            if (_monumentSpawnPoints.ContainsKey(name))
            {
                MonumentSpawnPoint spawnPoint = _monumentSpawnPoints[name];
                if (spawnPoint.Presets.Count < number + 1) return;
                spawnPoint.Presets[number].Config.WearItems = wears;
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Monument/{name}", spawnPoint);
                PrintToChat(player, $"You <color=#738d43>have successfully saved</color> the wear slots for Spawn Point <color=#55aaff>{name}</color> and for preset <color=#55aaff>{number + 1}</color>!");
            }
            else if (_underwaterLabSpawnPoints.ContainsKey(name))
            {
                MonumentSpawnPoint spawnPoint = _underwaterLabSpawnPoints[name];
                if (spawnPoint.Presets.Count < number + 1) return;
                spawnPoint.Presets[number].Config.WearItems = wears;
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Monument/Underwater Lab/{name}", spawnPoint);
                PrintToChat(player, $"You <color=#738d43>have successfully saved</color> the wear slots for Spawn Point <color=#55aaff>{name}</color> and for preset <color=#55aaff>{number + 1}</color>!");
            }
            else if (_tunnelSpawnPoints.ContainsKey(name))
            {
                MonumentSpawnPoint spawnPoint = _tunnelSpawnPoints[name];
                if (spawnPoint.Presets.Count < number + 1) return;
                spawnPoint.Presets[number].Config.WearItems = wears;
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Monument/Tunnel/{name}", spawnPoint);
                PrintToChat(player, $"You <color=#738d43>have successfully saved</color> the wear slots for Spawn Point <color=#55aaff>{name}</color> and for preset <color=#55aaff>{number + 1}</color>!");
            }
            else if (_customSpawnPoints.ContainsKey(name))
            {
                CustomSpawnPoint spawnPoint = _customSpawnPoints[name];
                if (spawnPoint.Presets.Count < number + 1) return;
                spawnPoint.Presets[number].Config.WearItems = wears;
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Custom/{name}", spawnPoint);
                PrintToChat(player, $"You <color=#738d43>have successfully saved</color> the wear slots for Spawn Point <color=#55aaff>{name}</color> and for preset <color=#55aaff>{number + 1}</color>!");
            }
            else if (_eventSpawnPoints.ContainsKey(name))
            {
                EventSpawnPoint spawnPoint = _eventSpawnPoints[name];
                if (spawnPoint.Presets.Count < number + 1) return;
                spawnPoint.Presets[number].Config.WearItems = wears;
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Event/{name}", spawnPoint);
                PrintToChat(player, $"You <color=#738d43>have successfully saved</color> the wear slots for Spawn Point <color=#55aaff>{name}</color> and for preset <color=#55aaff>{number + 1}</color>!");
            }
            else if (_roadSpawnPoints.ContainsKey(name))
            {
                RoadSpawnPoint spawnPoint = _roadSpawnPoints[name];
                if (spawnPoint.Presets.Count < number + 1) return;
                spawnPoint.Presets[number].Config.WearItems = wears;
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Road/{name}", spawnPoint);
                PrintToChat(player, $"You <color=#738d43>have successfully saved</color> the wear slots for Spawn Point <color=#55aaff>{name}</color> and for preset <color=#55aaff>{number + 1}</color>!");
            }
            else if (_biomeSpawnPoints.ContainsKey(name))
            {
                BiomeSpawnPoint spawnPoint = _biomeSpawnPoints[name];
                if (spawnPoint.Presets.Count < number + 1) return;
                spawnPoint.Presets[number].Config.WearItems = wears;
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Biome/{name}", spawnPoint);
                PrintToChat(player, $"You <color=#738d43>have successfully saved</color> the wear slots for Spawn Point <color=#55aaff>{name}</color> and for preset <color=#55aaff>{number + 1}</color>!");
            }
        }

        [ChatCommand("SpawnPointAddBelt")]
        private void ChatCommandSpawnPointAddBelt(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;

            if (args == null || args.Length == 0)
            {
                PrintToChat(player, "You <color=#ce3f27>didn't</color> write the preset number!");
                return;
            }

            if (args.Length == 1)
            {
                PrintToChat(player, "You <color=#ce3f27>didn't</color> write the name of the spawn point!");
                return;
            }

            int number = Convert.ToInt32(args[0]) - 1;
            if (number < 0) return;
            string name = ""; foreach (string arg in args.Skip(1)) name += arg != args.Last() ? arg + " " : arg;

            List<NpcBelt> belts = player.inventory.containerBelt.itemList.Select(x => new NpcBelt { ShortName = x.info.shortname, Amount = x.amount, SkinID = x.skin, Mods = x.contents != null && x.contents.itemList.Count > 0 ? x.contents.itemList.Select(y => y.info.shortname) : new List<string>() });

            if (_monumentSpawnPoints.ContainsKey(name))
            {
                MonumentSpawnPoint spawnPoint = _monumentSpawnPoints[name];
                if (spawnPoint.Presets.Count < number + 1) return;
                spawnPoint.Presets[number].Config.BeltItems = belts;
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Monument/{name}", spawnPoint);
                PrintToChat(player, $"You <color=#738d43>have successfully saved</color> the belt slots for Spawn Point <color=#55aaff>{name}</color> and for preset <color=#55aaff>{number + 1}</color>!");
            }
            else if (_underwaterLabSpawnPoints.ContainsKey(name))
            {
                MonumentSpawnPoint spawnPoint = _underwaterLabSpawnPoints[name];
                if (spawnPoint.Presets.Count < number + 1) return;
                spawnPoint.Presets[number].Config.BeltItems = belts;
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Monument/Underwater Lab/{name}", spawnPoint);
                PrintToChat(player, $"You <color=#738d43>have successfully saved</color> the belt slots for Spawn Point <color=#55aaff>{name}</color> and for preset <color=#55aaff>{number + 1}</color>!");
            }
            else if (_tunnelSpawnPoints.ContainsKey(name))
            {
                MonumentSpawnPoint spawnPoint = _tunnelSpawnPoints[name];
                if (spawnPoint.Presets.Count < number + 1) return;
                spawnPoint.Presets[number].Config.BeltItems = belts;
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Monument/Tunnel/{name}", spawnPoint);
                PrintToChat(player, $"You <color=#738d43>have successfully saved</color> the belt slots for Spawn Point <color=#55aaff>{name}</color> and for preset <color=#55aaff>{number + 1}</color>!");
            }
            else if (_customSpawnPoints.ContainsKey(name))
            {
                CustomSpawnPoint spawnPoint = _customSpawnPoints[name];
                if (spawnPoint.Presets.Count < number + 1) return;
                spawnPoint.Presets[number].Config.BeltItems = belts;
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Custom/{name}", spawnPoint);
                PrintToChat(player, $"You <color=#738d43>have successfully saved</color> the belt slots for Spawn Point <color=#55aaff>{name}</color> and for preset <color=#55aaff>{number + 1}</color>!");
            }
            else if (_eventSpawnPoints.ContainsKey(name))
            {
                EventSpawnPoint spawnPoint = _eventSpawnPoints[name];
                if (spawnPoint.Presets.Count < number + 1) return;
                spawnPoint.Presets[number].Config.BeltItems = belts;
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Event/{name}", spawnPoint);
                PrintToChat(player, $"You <color=#738d43>have successfully saved</color> the belt slots for Spawn Point <color=#55aaff>{name}</color> and for preset <color=#55aaff>{number + 1}</color>!");
            }
            else if (_roadSpawnPoints.ContainsKey(name))
            {
                RoadSpawnPoint spawnPoint = _roadSpawnPoints[name];
                if (spawnPoint.Presets.Count < number + 1) return;
                spawnPoint.Presets[number].Config.BeltItems = belts;
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Road/{name}", spawnPoint);
                PrintToChat(player, $"You <color=#738d43>have successfully saved</color> the belt slots for Spawn Point <color=#55aaff>{name}</color> and for preset <color=#55aaff>{number + 1}</color>!");
            }
            else if (_biomeSpawnPoints.ContainsKey(name))
            {
                BiomeSpawnPoint spawnPoint = _biomeSpawnPoints[name];
                if (spawnPoint.Presets.Count < number + 1) return;
                spawnPoint.Presets[number].Config.BeltItems = belts;
                Interface.Oxide.DataFileSystem.WriteObject($"BetterNpc/Biome/{name}", spawnPoint);
                PrintToChat(player, $"You <color=#738d43>have successfully saved</color> the belt slots for Spawn Point <color=#55aaff>{name}</color> and for preset <color=#55aaff>{number + 1}</color>!");
            }
        }

        [ChatCommand("SpawnPointShowPos")]
        private void ChatCommandSpawnPointShowPos(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;

            if (args == null || args.Length == 0)
            {
                PrintToChat(player, "You <color=#ce3f27>didn't</color> write the preset number!");
                return;
            }

            if (args.Length == 1)
            {
                PrintToChat(player, "You <color=#ce3f27>didn't</color> write the name of the spawn point!");
                return;
            }

            int number = Convert.ToInt32(args[0]) - 1;
            if (number < 0) return;
            string name = ""; foreach (string arg in args.Skip(1)) name += arg != args.Last() ? arg + " " : arg;

            List<ControllerSpawnPoint> controllers = _controllers.Where(x => x.Name == name).ToList();
            if (controllers.Count == 0)
            {
                PrintToChat(player, $"Spawn Point with the name <color=#55aaff>{name}</color> was <color=#ce3f27>not</color> found!");
                return;
            }

            foreach (ControllerSpawnPoint controller in controllers)
            {
                if (_monumentSpawnPoints.ContainsKey(name))
                {
                    MonumentSpawnPoint spawnPoint = _monumentSpawnPoints[name];
                    if (spawnPoint.Presets.Count < number + 1) return;
                    foreach (string pos in spawnPoint.Presets[number].OwnPositions)
                    {
                        Vector3 position = controller.transform.TransformPoint(pos.ToVector3());
                        player.SendConsoleCommand("ddraw.sphere", 10f, Color.green, position, 2f);
                        player.SendConsoleCommand("ddraw.line", 10f, Color.green, position, position + Vector3.up * 200f);
                        player.SendConsoleCommand("ddraw.text", 10f, Color.green, position, $"<size=40>{spawnPoint.Presets[number].OwnPositions.IndexOf(pos) + 1}</size>");
                    }
                }
                else if (_underwaterLabSpawnPoints.ContainsKey(name))
                {
                    MonumentSpawnPoint spawnPoint = _underwaterLabSpawnPoints[name];
                    if (spawnPoint.Presets.Count < number + 1) return;
                    foreach (string pos in spawnPoint.Presets[number].OwnPositions)
                    {
                        Vector3 position = controller.transform.TransformPoint(pos.ToVector3());
                        player.SendConsoleCommand("ddraw.sphere", 10f, Color.green, position, 2f);
                        player.SendConsoleCommand("ddraw.line", 10f, Color.green, position, position + Vector3.up * 200f);
                        player.SendConsoleCommand("ddraw.text", 10f, Color.green, position, $"<size=40>{spawnPoint.Presets[number].OwnPositions.IndexOf(pos) + 1}</size>");
                    }
                }
                else if (_tunnelSpawnPoints.ContainsKey(name))
                {
                    MonumentSpawnPoint spawnPoint = _tunnelSpawnPoints[name];
                    if (spawnPoint.Presets.Count < number + 1) return;
                    foreach (string pos in spawnPoint.Presets[number].OwnPositions)
                    {
                        Vector3 position = controller.transform.TransformPoint(pos.ToVector3());
                        player.SendConsoleCommand("ddraw.sphere", 10f, Color.green, position, 2f);
                        player.SendConsoleCommand("ddraw.line", 10f, Color.green, position, position + Vector3.up * 200f);
                        player.SendConsoleCommand("ddraw.text", 10f, Color.green, position, $"<size=40>{spawnPoint.Presets[number].OwnPositions.IndexOf(pos) + 1}</size>");
                    }
                }
                else if (_customSpawnPoints.ContainsKey(name))
                {
                    CustomSpawnPoint spawnPoint = _customSpawnPoints[name];
                    if (spawnPoint.Presets.Count < number + 1) return;
                    foreach (string pos in spawnPoint.Presets[number].OwnPositions)
                    {
                        Vector3 position = controller.transform.TransformPoint(pos.ToVector3());
                        player.SendConsoleCommand("ddraw.sphere", 10f, Color.green, position, 2f);
                        player.SendConsoleCommand("ddraw.line", 10f, Color.green, position, position + Vector3.up * 200f);
                        player.SendConsoleCommand("ddraw.text", 10f, Color.green, position, $"<size=40>{spawnPoint.Presets[number].OwnPositions.IndexOf(pos) + 1}</size>");
                    }
                }
            }
        }

        [ChatCommand("SpawnPointReload")]
        private void ChatCommandSpawnPointReload(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            if (args == null || args.Length == 0)
            {
                PrintToChat(player, "You <color=#ce3f27>didn't</color> write the name of the spawn point!");
                return;
            }
            string name = ""; foreach (string arg in args) name += arg != args.Last() ? arg + " " : arg;
            DestroyController(name);
            CreateController(name);
            PrintToChat(player, $"SpawnPoint with the name <color=#55aaff>{name}</color> <color=#738d43>has been reloaded</color>!");
        }

        [ConsoleCommand("ShowAllNpc")]
        private void ConsoleCommandShowAllNpc(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
            {
                string message = "The number of NPCs from the BetterNpc plugin:";
                int all = 0;
                foreach (ControllerSpawnPoint controller in _controllers)
                {
                    message += $"\n- {controller.Name} = {controller.ActiveNpc.Count}";
                    all += controller.ActiveNpc.Count;
                }
                message += $"\nTotal number = {all}";
                Puts(message);
            }
        }
        #endregion Commands

        #region API
        private void DestroyController(string name)
        {
            while (_controllers.Any(x => x.Name == name))
            {
                ControllerSpawnPoint controller = _controllers.FirstOrDefault(x => x.Name == name);
                _controllers.Remove(controller);
                UnityEngine.Object.Destroy(controller.gameObject);
            }
        }

        private void CreateController(string name)
        {
            if (_controllers.Any(x => x.Name == name)) return;
            if (_monumentSpawnPoints.ContainsKey(name))
            {
                MonumentSpawnPoint spawnPoint = _monumentSpawnPoints[name];
                if (!spawnPoint.Enabled) return;
                foreach (MonumentInfo monument in TerrainMeta.Path.Monuments.Where(x => GetNameMonument(x) == name))
                {
                    ControllerSpawnPoint controller = new GameObject().AddComponent<ControllerSpawnPoint>();
                    controller.transform.position = monument.transform.position;
                    controller.transform.rotation = monument.transform.rotation;
                    controller.Name = name;
                    controller.IsEvent = false;
                    controller.RemoveOtherNpc = spawnPoint.RemoveOtherNpc;
                    controller.Size = new Vector2(spawnPoint.Size.ToVector3().x, spawnPoint.Size.ToVector3().z);
                    controller.Presets = spawnPoint.Presets.Where(x => x.Enabled).ToList();
                    controller.IsDay = _isDay;
                    controller.Init();
                    _controllers.Add(controller);
                }
            }
            else if (_underwaterLabSpawnPoints.ContainsKey(name))
            {
                MonumentSpawnPoint spawnPoint = _underwaterLabSpawnPoints[name];
                if (!spawnPoint.Enabled) return;
                foreach (DungeonBaseInfo baseModule in TerrainMeta.Path.DungeonBaseEntrances)
                {
                    if (baseModule.name == name) SpawnUnderwaterLabSpawnPoint(name, baseModule.transform);
                    foreach (GameObject module in baseModule.Links)
                    {
                        string moduleName = module.name.Split('/').Last().Split('.').First();
                        if (moduleName == name) SpawnUnderwaterLabSpawnPoint(name, module.transform);
                    }
                }
            }
            else if (_tunnelSpawnPoints.ContainsKey(name))
            {
                MonumentSpawnPoint spawnPoint = _tunnelSpawnPoints[name];
                if (!spawnPoint.Enabled) return;
                foreach (DungeonGridCell gridCell in TerrainMeta.Path.DungeonGridCells)
                {
                    string cellName = gridCell.name.Split('/').Last().Split('.').First();
                    if (cellName == name)
                    {
                        ControllerSpawnPoint controller = new GameObject().AddComponent<ControllerSpawnPoint>();
                        controller.transform.position = gridCell.transform.position;
                        controller.transform.rotation = gridCell.transform.rotation;
                        controller.Name = name;
                        controller.IsEvent = false;
                        controller.RemoveOtherNpc = spawnPoint.RemoveOtherNpc;
                        controller.Size = new Vector2(spawnPoint.Size.ToVector3().x, spawnPoint.Size.ToVector3().z);
                        controller.Presets = spawnPoint.Presets.Where(x => x.Enabled).ToList();
                        controller.IsDay = _isDay;
                        controller.Init();
                        _controllers.Add(controller);
                    }
                }
            }
            else if (_customSpawnPoints.ContainsKey(name))
            {
                CustomSpawnPoint spawnPoint = _customSpawnPoints[name];
                if (!spawnPoint.Enabled) return;
                ControllerSpawnPoint controller = new GameObject().AddComponent<ControllerSpawnPoint>();
                controller.transform.position = spawnPoint.Position.ToVector3();
                controller.Name = name;
                controller.IsEvent = false;
                controller.RemoveOtherNpc = spawnPoint.RemoveOtherNpc;
                controller.Size = new Vector2(spawnPoint.Radius, spawnPoint.Radius);
                controller.Presets = spawnPoint.Presets.Where(x => x.Enabled).ToList();
                controller.IsDay = _isDay;
                controller.Init();
                _controllers.Add(controller);
            }
            else if (_roadSpawnPoints.ContainsKey(name))
            {
                RoadSpawnPoint spawnPoint = _roadSpawnPoints[name];
                if (!spawnPoint.Enabled) return;
                ControllerSpawnPoint controller = new GameObject().AddComponent<ControllerSpawnPoint>();
                controller.Name = name;
                controller.IsEvent = false;
                controller.RemoveOtherNpc = false;
                controller.Size = new Vector2();
                controller.Presets = spawnPoint.Presets.Where(x => x.Enabled).ToList();
                controller.IsDay = _isDay;
                foreach (PresetConfig preset in controller.Presets)
                {
                    preset.TypeSpawn = 1;
                    preset.OwnPositions = _roadPositions[name].ToList();
                }
                controller.Init();
                _controllers.Add(controller);
            }
        }
        #endregion API
    }
}

namespace Oxide.Plugins.BetterNpcExtensionMethods
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

        public static KeyValuePair<TKey, TValue> FirstOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> source, Func<KeyValuePair<TKey, TValue>, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return enumerator.Current;
            return default(KeyValuePair<TKey, TValue>);
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

        public static List<TSource> ToList<TSource>(this IEnumerable<TSource> source)
        {
            List<TSource> result = new List<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(enumerator.Current);
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

        public static bool IsPlayer(this BasePlayer player) => player != null && player.userID.IsSteamId();

        public static bool IsExists(this BaseNetworkable entity) => entity != null && !entity.IsDestroyed;
    }
}