using Facepunch;
using System.Text;
using System.Collections.Generic;
using System;
using Oxide.Core;
using UnityEngine;
using System.Linq;
using Newtonsoft.Json;
using System.IO;
using static Oxide.Plugins.PopulationManager;

namespace Oxide.Plugins
{
    public static class SpawnPopulationDataExtionsions
    {
        public static void SetWeights(this SpawnPopulationBase spawnPopulation, WeightsData weights)
        {
            foreach (var prefabWeight in weights.Weights)
            {
                var prefab = spawnPopulation.Prefabs.Where(x => x.Name == prefabWeight.Key).FirstOrDefault();
                if (prefab.Parameters == null)
                {
                    prefab.Parameters = prefab.Object.AddComponent<PrefabParameters>();
                    prefab.Parameters.Count = prefabWeight.Value;
                }
                else
                {
                    prefab.Parameters.Count = prefabWeight.Value;
                }
            }
        }

        public static SpawnPopulationData GetData(this SpawnPopulationBase spawnPopulation)
        {
            if (SpawnPopulationData.LOADED_POPULATIONS.TryGetValue(spawnPopulation.name, out SpawnPopulationData spawnPopulationData))
                return spawnPopulationData;
            return null;
        }
    }
}

namespace Oxide.Plugins
{
    public static class SpawnPointSpawnPopulationExtensions
    {
        public static List<Vector3> GetPoints(this SpawnPointSpawnPopulation spawnPointSpawnPopulation)
        {
            switch (spawnPointSpawnPopulation.name)
            {
                case "Tugboat.Population":
                {
                    if (BaseSpawnPoint.spawnPoints.TryGetValue(BaseSpawnPoint.SpawnPointType.Tugboat, out List<BaseSpawnPoint> spawnPoints))
                    {
                        List<Vector3> points = new List<Vector3>();
                        foreach (var spawnPoint in spawnPoints)
                        {
                            spawnPoint.GetLocation(out Vector3 position, out Quaternion rotation);
                            points.Add(position);
                        }

                        return points;
                    }

                    return new List<Vector3>();
                }

                default:
                    return new List<Vector3>();
            }
        }

        public static void UpdatePoints(this SpawnPointSpawnPopulation spawnPointSpawnPopulation, List<Vector3> positions)
        {
            switch (spawnPointSpawnPopulation.name)
            {
                case "Tugboat.Population":
                {
                    if (BaseSpawnPoint.spawnPoints.TryGetValue(BaseSpawnPoint.SpawnPointType.Tugboat, out List<BaseSpawnPoint> spawnPoints))
                    {
                        var clonedList = new List<BaseSpawnPoint>(spawnPoints);
                        foreach (var spawnPoint in clonedList)
                            spawnPoint.enabled = false;
                        foreach (var spawnPoint in clonedList)
                            UnityEngine.Object.Destroy(spawnPoint.gameObject);
                        clonedList.Clear();
                    }

                    BaseSpawnPoint.spawnPoints[BaseSpawnPoint.SpawnPointType.Tugboat] = new List<BaseSpawnPoint>();
                    foreach (var position in positions)
                    {
                        GameObject go = new GameObject();
                        VehicleSpawnPoint spawnPoint = go.AddComponent<VehicleSpawnPoint>();
                        go.transform.position = position;
                        spawnPoint.transform.position = position;
                        spawnPoint.spawnPointType = BaseSpawnPoint.SpawnPointType.Tugboat;
                        spawnPoint.enabled = true;
                        Instantiate.GameObject(go);
                    }

                    break;
                }

                default:
                    return;
            }
        }
    }
}

namespace Oxide.Plugins
{
    public static class SpawnHandlerExtensions
    {
        public static void DestroySpawnable(this Spawnable spawnable)
        {
            BaseEntity baseEntity = GameObjectEx.ToBaseEntity(spawnable.gameObject);
            if (baseEntity.IsValid())
                baseEntity.Kill();
            else
                GameManager.Destroy(spawnable.gameObject);
        }

        public static void UpdateFromPopulationManager(this SpawnHandler spawnHandler, string value = "")
        {
            var allSpawnable = UnityEngine.Object.FindObjectsOfType<Spawnable>();
            for (int i = 0; i < spawnHandler.AllSpawnPopulations.Length; i++)
            {
                var spawnPopulation = spawnHandler.AllSpawnPopulations[i];
                var spawnPopulationData = spawnPopulation.GetData();
                if (spawnPopulationData != null)
                {
                    spawnHandler.SpawnDistributions[i] = new SpawnDistribution(spawnHandler, spawnPopulation.GetBaseMapValues(Mathf.NextPowerOfTwo((int)(World.Size * 0.25f))), TerrainMeta.Position, TerrainMeta.Size);
                    spawnHandler.population2distribution[spawnPopulation] = spawnHandler.SpawnDistributions[i];
                    Spawnable[] populationSpawnables = allSpawnable.Where(x => x.gameObject.activeInHierarchy && x.Population == spawnPopulation).ToArray();
                    foreach (var spawnable in populationSpawnables)
                        spawnHandler.SpawnDistributions[i].AddInstance(spawnable);
                    if (spawnPopulationData.EnforсeRemoveAllEntities)
                    {
                        foreach (var currentSpawnable in populationSpawnables)
                            currentSpawnable.DestroySpawnable();
                    }
                    else if (spawnPopulationData.EnforcePopulationLimits)
                    {
                        int targetCount = spawnPopulation.GetTargetCount(spawnHandler.population2distribution[spawnPopulation]);
                        int amountToDelete = populationSpawnables.Count() - targetCount;
                        if (amountToDelete > 0)
                        {
                            foreach (var currentSpawnable in populationSpawnables.Take(amountToDelete))
                                currentSpawnable.DestroySpawnable();
                        }
                    }
		   		 		  						   					  						  						  		 			  	 	 
                    if (spawnPopulationData.EnforсeRespawnEntities)
                    {
                        spawnHandler.SpawnInitial(spawnPopulation, spawnHandler.population2distribution[spawnPopulation]);
                    }
                }
            }
        }
    }
}

namespace Oxide.Plugins
{
    public static class TopologyDataExtensions
    {
        public static void SetTopology(this SpawnPopulationBase spawnPopulation, TopologyData tData)
        {
            spawnPopulation.GetSpawnFilter().TopologyAny = CombineFlags<TerrainTopology.Enum>(tData.TopologyAny);
            spawnPopulation.GetSpawnFilter().TopologyAll = CombineFlags<TerrainTopology.Enum>(tData.TopologyAll);
            spawnPopulation.GetSpawnFilter().TopologyNot = CombineFlags<TerrainTopology.Enum>(tData.TopologyNot);
            spawnPopulation.GetSpawnFilter().BiomeType = CombineFlags<TerrainBiome.Enum>(tData.BiomeType);
            spawnPopulation.GetSpawnFilter().SplatType = CombineFlags<TerrainSplat.Enum>(tData.SplatType);
        }

        private static TEnum CombineFlags<TEnum>(IEnumerable<string> flags)
            where TEnum : struct, Enum
        {
            TEnum result = default;
            foreach (var flag in flags)
            {
                if (Enum.TryParse(flag, out TEnum parsedFlag))
                {
                    result = (TEnum)(object)(((int)(object)result) | ((int)(object)parsedFlag));
                }
            }

            return result;
        }
    }
}
namespace Oxide.Plugins
{
    [Info("PopulationManager", "VooDoo", "3.0.4")]
    public partial class PopulationManager : RustPlugin
    {
        internal const string LOAD_POPULATIONS_PATH = "PopulationManager/";

        public class TopologyData
        {
            public List<string> TopologyAny { get; set; }
            public List<string> TopologyAll { get; set; }
            public List<string> TopologyNot { get; set; }
            public List<string> SplatType { get; set; }
            public List<string> BiomeType { get; set; }

            public TopologyData()
            {
            }

            public TopologyData(SpawnPopulationBase spawnPopulation)
            {
                TopologyAny = new List<string>();
                TopologyAll = new List<string>();
                TopologyNot = new List<string>();
                BiomeType = new List<string>();
                SplatType = new List<string>();
                foreach (TerrainTopology.Enum topologyEnum in Enum.GetValues(typeof(TerrainTopology.Enum)))
                {
                    if (spawnPopulation.GetSpawnFilter().TopologyAny.HasFlag(topologyEnum))
                    {
                        TopologyAny.Add(topologyEnum.ToString());
                    }

                    if (spawnPopulation.GetSpawnFilter().TopologyAll.HasFlag(topologyEnum))
                    {
                        TopologyAll.Add(topologyEnum.ToString());
                    }

                    if (spawnPopulation.GetSpawnFilter().TopologyNot.HasFlag(topologyEnum))
                    {
                        TopologyNot.Add(topologyEnum.ToString());
                    }
                }

                foreach (TerrainBiome.Enum biomeEnum in Enum.GetValues(typeof(TerrainBiome.Enum)))
                {
                    if (spawnPopulation.GetSpawnFilter().BiomeType.HasFlag(biomeEnum))
                    {
                        BiomeType.Add(biomeEnum.ToString());
                    }
                }

                foreach (TerrainSplat.Enum splatEnum in Enum.GetValues(typeof(TerrainSplat.Enum)))
                {
                    if (spawnPopulation.GetSpawnFilter().SplatType.HasFlag(splatEnum))
                    {
                        SplatType.Add(splatEnum.ToString());
                    }
                }
            }
        }

        internal const string DEFAULT_POPULATIONS_PATH = "PopulationManager/Default/";
		   		 		  						   					  						  						  		 			  	 	 
        internal void Log(string v)
        {
            PrintWarning(v);
        }

        public PopulationConfig PConfig;

        public class ConvarControlledSpawnPopulationData : DensitySpawnPopulationData
        {
            public ConvarControlledSpawnPopulationData() : base()
            {
            }

            public ConvarControlledSpawnPopulationData(ConvarControlledSpawnPopulation spawnPopulation) : base(spawnPopulation)
            {
            }

            public override void SetPopulationLimits(SpawnPopulationBase spawnPopulation)
            {
                base.SetPopulationLimits(spawnPopulation);
                if (spawnPopulation is ConvarControlledSpawnPopulation convarControlledSpawnPopulation)
                    Instance.Command($"{convarControlledSpawnPopulation.PopulationConvar} {convarControlledSpawnPopulation._targetDensity.ToString()}");
            }
        }

        internal void Command(string v)
        {
            rust.RunServerCommand(v);
        }

        private void Unload()
        {
            if (PConfig.RevertChangesOnUnloadPlugin)
            {
                foreach (var loadedPopulation in SpawnPopulationData.LOADED_POPULATIONS.Values)
                {
                    if (SpawnPopulationData.DEFAULT_POPULATIONS.TryGetValue(loadedPopulation.Name, out SpawnPopulationData defaultPopulation))
                        defaultPopulation.UpdatePopulation(false);
                }

                if (IsHandlerChanged)
                    GetSpawnHandler().UpdateFromPopulationManager(UNLOAD_PREFAB_ID);
                if (DEBUG && UNLOAD_PREFAB_ID == "8360")
                {
                    Log("Плагин выгружен. Все изменения отменены");
                }
            }

            SpawnPopulationData.DEFAULT_POPULATION_VALUES = new System.Collections.Generic.Dictionary<string, int>();
            Instance = null;
        }

        public class SpawnPopulationData
        {
            public string Type { get; set; }

            [JsonProperty("Название")]
            public string Name { get; set; }

            [JsonProperty("Настройка топологии")]
            public TopologyData Topology { get; set; }

            [JsonProperty("Настройка пропорций")]
            public WeightsData Weights { get; set; }
		   		 		  						   					  						  						  		 			  	 	 
            [JsonProperty("Скорость спавна")]
            public float SpawnRate { get; set; }
		   		 		  						   					  						  						  		 			  	 	 
            [JsonProperty("Масштабировать в зависимости от онлайна")]
            public bool ScaleWithServerPopulation { get; set; }

            [JsonProperty("Очистка лишнего после загрузки/выгрузки плагина")]
            public bool EnforcePopulationLimits { get; set; }

            [JsonProperty("Спавн до максимума после загрузки/выгрузки плагина")]
            public bool EnforсeRespawnEntities { get; set; }

            [JsonProperty("Полная очистка после загрузки/выгрузки плагина")]
            public bool EnforсeRemoveAllEntities { get; set; }

            [JsonIgnore]
            public static Dictionary<string, SpawnPopulationData> DEFAULT_POPULATIONS = new Dictionary<string, SpawnPopulationData>();
            [JsonIgnore]
            public static Dictionary<string, SpawnPopulationData> LOADED_POPULATIONS = new Dictionary<string, SpawnPopulationData>();
            [JsonIgnore]
            public static Dictionary<string, int> DEFAULT_POPULATION_VALUES = new Dictionary<string, int>();
            public SpawnPopulationData()
            {
            }

            public SpawnPopulationData(SpawnPopulationBase spawnPopulation)
            {
                spawnPopulation.Initialize();
                this.Type = this.GetType().Name;
                this.Name = spawnPopulation.name;
                this.EnforcePopulationLimits = spawnPopulation.EnforcePopulationLimits;
                this.EnforсeRemoveAllEntities = false;
                this.EnforсeRespawnEntities = false;
                this.Topology = new TopologyData(spawnPopulation);
                this.Weights = new WeightsData(spawnPopulation);
            }

            public virtual void UpdatePopulation(bool cacheToLoaded = true)
            {
                var spawnPopulation = Instance.GetSpawnHandler().AllSpawnPopulations.Where(x => x.name == Name).FirstOrDefault();
                if (spawnPopulation != null)
                {
                    if (cacheToLoaded)
                        CacheToLoaded();
                    SetPopulation(spawnPopulation);
                    spawnPopulation.SetTopology(Topology);
                    spawnPopulation.SetWeights(Weights);
                    SetPopulationLimits(spawnPopulation);
                }
            }

            public virtual void SetPopulation(SpawnPopulationBase spawnPopulation)
            {
                spawnPopulation.SpawnRate = SpawnRate;
                spawnPopulation.ScaleWithServerPopulation = ScaleWithServerPopulation;
            }

            public virtual void SetPopulationLimits(SpawnPopulationBase spawnPopulation)
            {
            }

            public virtual SpawnPopulationData CacheToDefault()
            {
                DEFAULT_POPULATIONS[this.Name] = this;
                return this;
            }

            public virtual SpawnPopulationData CacheToLoaded()
            {
                LOADED_POPULATIONS[this.Name] = this;
                return this;
            }
        }
        internal readonly string UNLOAD_PREFAB_ID = "8360";

        internal bool IsManagerInitialized { get; set; }

        public class DensitySpawnPopulationData : SpawnPopulationData
        {
            [JsonProperty("Масштабировать в зависимости от Density в SpawnDistribution")]
            public bool ScaleWithSpawnFilter { get; set; }
		   		 		  						   					  						  						  		 			  	 	 
            [JsonProperty("Масштабировать на основе полной площади карты (>4000)")]
            public bool ScaleWithLargeMaps { get; set; }

            [JsonProperty("Плотность спавна")]
            public float SpawnDensity { get; set; }

            [JsonProperty("Плотность спавна (в шт.)")]
            public int SpawnCount { get; set; }

            [JsonProperty("Плотность спавна (в процентах, может зависеть от остальных параметров)")]
            public float SpawnPercentCount { get; set; }

            public DensitySpawnPopulationData() : base()
            {
            }
		   		 		  						   					  						  						  		 			  	 	 
            public DensitySpawnPopulationData(DensitySpawnPopulation spawnPopulation) : base(spawnPopulation)
            {
                ScaleWithServerPopulation = spawnPopulation.ScaleWithServerPopulation;
                ScaleWithSpawnFilter = spawnPopulation.ScaleWithSpawnFilter;
                ScaleWithLargeMaps = spawnPopulation.ScaleWithLargeMaps;
                SpawnRate = spawnPopulation.SpawnRate;
                SpawnDensity = spawnPopulation._targetDensity;
                SpawnCount = -1;
                SpawnPercentCount = -1f;
            }

            public override void SetPopulation(SpawnPopulationBase spawnPopulation)
            {
                if (spawnPopulation is DensitySpawnPopulation densitySpawnPopulation)
                {
                    densitySpawnPopulation.SpawnRate = SpawnRate;
                    densitySpawnPopulation.ScaleWithLargeMaps = ScaleWithLargeMaps;
                    densitySpawnPopulation.ScaleWithServerPopulation = ScaleWithServerPopulation;
                    densitySpawnPopulation.ScaleWithSpawnFilter = ScaleWithSpawnFilter;
                }
            }

            public override void SetPopulationLimits(SpawnPopulationBase spawnPopulation)
            {
                if (spawnPopulation is DensitySpawnPopulation densitySpawnPopulation)
                {
                    if (SpawnDensity >= 0)
                        densitySpawnPopulation._targetDensity = SpawnDensity;
                    if (SpawnCount >= 0)
                    {
                        densitySpawnPopulation.ScaleWithLargeMaps = true;
                        densitySpawnPopulation.ScaleWithServerPopulation = false;
                        densitySpawnPopulation.ScaleWithSpawnFilter = false;
                        densitySpawnPopulation._targetDensity = (SpawnCount / (TerrainMeta.Size.x * TerrainMeta.Size.z)) / (1f * 1E-06f);
                    }

                    if (SpawnPercentCount >= 0)
                        densitySpawnPopulation._targetDensity = densitySpawnPopulation._targetDensity *= SpawnPercentCount;
                }
            }
        }
        [ConsoleCommand("pm.report")]
        private void ConsoleCommandSpawnReport(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin == false)
                return;
            string filter = arg.HasArgs() ? arg.Args[0] : "";
            StringBuilder stringBuilder = new StringBuilder();
            if (GetSpawnHandler().AllSpawnPopulations != null && GetSpawnHandler().SpawnDistributions != null)
            {
                stringBuilder.AppendLine();
                stringBuilder.AppendLine("Name".PadRight(40) + "Current".PadRight(25) + "Maximum".PadRight(25) + "Default");
                for (int i = 0; i < GetSpawnHandler().AllSpawnPopulations.Length; i++)
                {
                    if (GetSpawnHandler().AllSpawnPopulations[i] == null)
                        continue;
                    if (GetSpawnHandler().SpawnDistributions[i] == null)
                        continue;
                    if (string.IsNullOrEmpty(filter) == false && GetSpawnHandler().AllSpawnPopulations[i].name.Contains(filter) == false)
                        continue;
                    stringBuilder.AppendLine();
                    stringBuilder.Append(GetSpawnHandler().AllSpawnPopulations[i].name.PadRight(40));
                    stringBuilder.Append(GetSpawnHandler().SpawnDistributions[i].Count.ToString().PadRight(25));
                    stringBuilder.Append(GetSpawnHandler().AllSpawnPopulations[i].GetTargetCount(GetSpawnHandler().SpawnDistributions[i]).ToString().PadRight(25));
                    if (SpawnPopulationData.DEFAULT_POPULATION_VALUES.ContainsKey(GetSpawnHandler().AllSpawnPopulations[i].name))
                        stringBuilder.Append(SpawnPopulationData.DEFAULT_POPULATION_VALUES[GetSpawnHandler().AllSpawnPopulations[i].name]);
                }
            }
		   		 		  						   					  						  						  		 			  	 	 
            SendReply(arg, stringBuilder.ToString());
        }

        public class SpawnPopulationDataType
        {
            public string Type { get; set; }
        }
        internal readonly bool DEBUG = false;

        public PopulationConfig GetDefaultConfig()
        {
            return new PopulationConfig
            {
                LoadPluginOnLoadSave = true,
                RevertChangesOnUnloadPlugin = true,
            };
        }

        public class WeightsData
        {
            [JsonProperty("Пропорции")]
            public Dictionary<string, float> Weights { get; set; }

            public WeightsData()
            {
            }

            public WeightsData(SpawnPopulationBase spawnPopulation)
            {
                Weights = new Dictionary<string, float>();
                foreach (var prefab in spawnPopulation.Prefabs)
                    Weights[prefab.Name] = prefab.Parameters == null ? 1 : prefab.Parameters.Count;
            }
        }

        [ConsoleCommand("pm.reload")]
        private void ConsoleCommandUnload(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin == false)
                return;
            Server.Command("o.reload " + this.Name);
        }

        private void Init()
        {
            PConfig = Config.ReadObject<PopulationConfig>();
        }

        public class SpawnPointSpawnPopulationData : SpawnPopulationData
        {
            [JsonProperty("Координаты точек спавна")]
            public List<Vector3> SpawnPoints { get; set; }

            public SpawnPointSpawnPopulationData() : base()
            {
            }

            public SpawnPointSpawnPopulationData(SpawnPointSpawnPopulation spawnPopulation) : base(spawnPopulation)
            {
                SpawnPoints = spawnPopulation.GetPoints();
            }

            public override void SetPopulation(SpawnPopulationBase spawnPopulation)
            {
                if (SpawnPoints == null)
                    return;
                if (spawnPopulation is SpawnPointSpawnPopulation spawnPointSpawnPopulation)
                {
                    spawnPointSpawnPopulation.UpdatePoints(SpawnPoints);
                }
            }
        }

        private void OnServerInitialized()
        {
            if (IsManagerInitialized == false)
            {
                Instance = this;
                foreach (var spawnPopulation in GetSpawnHandler().AllSpawnPopulations)
                {
                    SpawnPopulationData populationData;
                    if (spawnPopulation is ConvarControlledSpawnPopulation convarControlledSpawnPopulation)
                        populationData = new ConvarControlledSpawnPopulationData(convarControlledSpawnPopulation).CacheToDefault();
                    else if (spawnPopulation is DensitySpawnPopulation densitySpawnPopulation)
                        populationData = new DensitySpawnPopulationData(densitySpawnPopulation).CacheToDefault();
                    else if (spawnPopulation is SpawnPointSpawnPopulation spawnPointSpawnPopulation)
                        populationData = new SpawnPointSpawnPopulationData(spawnPointSpawnPopulation).CacheToDefault();
                    else
                        populationData = new SpawnPopulationData(spawnPopulation).CacheToDefault();
                    Interface.Oxide.DataFileSystem.WriteObject(DEFAULT_POPULATIONS_PATH + "/" + spawnPopulation.name, populationData);
                }

                try
                {
                    PrintWarning("Please do not restart this plugin while it is in the process of initializing.".ToUpper());
                    foreach (var pd in GetSpawnHandler().population2distribution)
                        SpawnPopulationData.DEFAULT_POPULATION_VALUES[pd.Key.name] = pd.Key.GetTargetCount(pd.Value);
                    PrintWarning("INIT PART 1/2".ToUpper());
                    foreach (var filePath in Interface.Oxide.DataFileSystem.GetFiles(LOAD_POPULATIONS_PATH))
                    {
                        string correctFilePath = filePath.Replace("/", "\\").Replace(".json", "");
                        try
                        {
                            var populationType = Interface.Oxide.DataFileSystem.ReadObject<SpawnPopulationDataType>(correctFilePath);
                            if (string.IsNullOrEmpty(populationType.Type))
                                continue;
                            SpawnPopulationData populationData;
                            if (populationType.Type == "DensitySpawnPopulationData")
                                populationData = Interface.Oxide.DataFileSystem.ReadObject<DensitySpawnPopulationData>(correctFilePath);
                            else if (populationType.Type == "ConvarControlledSpawnPopulationData")
                                populationData = Interface.Oxide.DataFileSystem.ReadObject<ConvarControlledSpawnPopulationData>(correctFilePath);
                            else if (populationType.Type == "SpawnPointSpawnPopulationData")
                                populationData = Interface.Oxide.DataFileSystem.ReadObject<SpawnPointSpawnPopulationData>(correctFilePath);
                            else
                                populationData = Interface.Oxide.DataFileSystem.ReadObject<SpawnPopulationData>(correctFilePath);
                            if (populationData != null)
                            {
                                populationData.UpdatePopulation();
                                if (DEBUG)
                                {
                                    Log("File: \t" + correctFilePath + ".json");
                                    Log("Type: \t" + populationType.Type);
                                    Log("Class: \t" + populationData.GetType().Name);
                                }

                                IsHandlerChanged = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log("Файл " + correctFilePath + " содержит ошибки или во время инициализации настроек произошла ошибка");
                            Log(ex.ToString());
                        }
                    }

                    if (IsHandlerChanged)
                    {
                        timer.In(10f, () =>
                        {
                            GetSpawnHandler().UpdateFromPopulationManager();
                            PrintWarning("INIT PART 2/2".ToUpper());
                            PrintWarning("Plugin initialized".ToUpper());
                        });
                    }
                }
                catch (DirectoryNotFoundException)
                {
                }

                IsManagerInitialized = true;
            }
        }
        internal bool IsHandlerChanged { get; set; }
        internal SpawnHandler GetSpawnHandler()
        {
            return SingletonComponent<SpawnHandler>.Instance;
        }

        internal static PopulationManager Instance { get; private set; }

        private void OnSaveLoad()
        {
            if (IsManagerInitialized == false && PConfig.LoadPluginOnLoadSave)
            {
                OnServerInitialized();
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }
        public class PopulationConfig
        {
            [JsonProperty("Отменить изменения при выгрузке плагина")]
            public bool RevertChangesOnUnloadPlugin;
            [JsonProperty("Применить изменения при загрузке сохранения карты")]
            public bool LoadPluginOnLoadSave;
        }
    }
}
