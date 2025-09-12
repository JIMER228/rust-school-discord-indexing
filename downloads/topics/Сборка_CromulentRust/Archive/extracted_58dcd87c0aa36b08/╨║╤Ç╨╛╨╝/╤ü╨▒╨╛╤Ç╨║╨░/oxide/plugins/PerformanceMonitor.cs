using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

// TODO: Make this plugin actually useful.

namespace Oxide.Plugins
{
    [Info("Performance Monitor", "Orange", "1.2.5")]
    [Description("Tool for collecting information about server performance")]
    public class PerformanceMonitor : RustPlugin
    {
        #region Vars

        private const string commandString = "monitor.createreport";
        private const string commandString2 = "monitor.report";
        private PerformanceDump currentReport;

        #endregion

        #region Oxide Hooks

        private void Init()
        {

            cmd.AddConsoleCommand(commandString, this, nameof(cmdCompleteNow));
            cmd.AddConsoleCommand(commandString2, this, nameof(cmdCompleteNow));
        }

        private void OnServerInitialized()
        {
            if (config.checkTime > 0)
            {
                timer.Every(config.checkTime, CreateReport);
            }
        }

        #endregion

        #region Commands

        private void cmdCompleteNow(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin == false)
            {
                return;
            }

            CreateReport();
        }

        #endregion

        #region Core

        private void CreateReport()
        {
            ServerMgr.Instance.StartCoroutine(CreateActualReport());
        }

        private IEnumerator CreateActualReport()
        {
            if (currentReport != null)
            {
                yield break;
            }
            else
            {
                currentReport = new PerformanceDump();
            }

            var sw = new Stopwatch();
            sw.Start();

            CompletePluginsReport();
            ServerMgr.Instance.StartCoroutine(CompleteEntitiesReport());

            while (!currentReport.entities.completed && config.runEntitiesReport)
            {
                Puts($"Report status: {currentReport.statusBar}% [{currentReport.entitiesChecked}/{currentReport.entitiesTotal}]");
                yield return new WaitForEndOfFrame();
            }

            if (!config.runPluginsReportAverage)
            {
                SaveReport(currentReport);
                currentReport = null;
                sw.Stop();
                Puts($"Performance report was completed in {sw.Elapsed.Seconds + (sw.Elapsed.Milliseconds / 1000.0)} seconds.");
            }
            else
            {
                timer.Once(32 * 3, () =>
                {
                    SaveReport(currentReport);
                    currentReport = null;
                    sw.Stop();
                    Puts($"Performance report was completed in {sw.Elapsed.Seconds + (32 * 3) + (sw.Elapsed.Milliseconds / 1000.0)} seconds.");
                });
            }
        }
        
        private void CompletePluginsReport()
        {
            var list = new List<string>();

            if (config.runPluginsReport == false)
            {
                return;
            }
            // Make this use time as the determining factor, Do so by creating a new variable called 'pastTime'
            // This holds the last called time, if its larger, it puts it below that, otherwise above
            double pastTime = 0.0;
            foreach (var plugin in plugins.GetAll().OrderByDescending(x => x.TotalHookTime))
            {
                var name = plugin.Name;
                if (name == Name || plugin.IsCorePlugin || config.excludedPlugins.Contains(name))
                {
                    continue;
                }

                var version = plugin.Version;
                double time = plugin.TotalHookTime;
                var info = $"{name} ({version}), Total Hook Time = {time}";
                if(pastTime != 0.0 && pastTime> time){
                    list.Add(info);
                }else{
                    if(list.Count>2){
                        list.Insert(list.Count-2, info);
                    }else if(list.Count>1){
                        list.Insert(list.Count-1, info);
                    }else{
                        list.Add(info);
                    }
                }
                pastTime = time;
            }
            currentReport.plugins = list.ToArray();
            
            
            // Average Load Time for Plugins:
            if (!config.runPluginsReportAverage) return;
            var averageTime = new Dictionary<string, List<double>>();
            Puts($"Checking Average Time: Please Wait...");
            timer.Repeat(30f, 3, () =>
            {
                Puts($"Checking Average Time: Please Wait...");
                foreach (var plugin in plugins.GetAll().OrderByDescending(x => x.TotalHookTime))
                {
                    var name = plugin.Name;
                    if (name == Name || plugin.IsCorePlugin || config.excludedPlugins.Contains(name))
                    {
                        continue;
                    }
                    double time = plugin.TotalHookTime;
                    List<double> averageList;
                    if (!averageTime.TryGetValue(plugin.Name, out averageList))
                    {
                        averageList = new List<double>();
                    }
                    // Change into a list of strings just like the one above, but just with the average of it instead. Done by waiting until
                    // length is equal to the number of repeats. Then set info = $"{name} ({version}), Total Hook Time = {time}"; and time is
                    // equal to the average of the 3.
                    averageList.Add(time);
                    averageTime[plugin.Name] = averageList;
                    pastTime = time;
                }
            });
            currentReport.pluginsAverage = averageTime;
        }

        private IEnumerator CompleteEntitiesReport()
        {
            if (config.runEntitiesReport == false)
            {
                yield break;
            }
            var entities = UnityEngine.Object.FindObjectsOfType<BaseEntity>();
            var entitiesByShortname = currentReport.entities.list;
            currentReport.entitiesTotal = entities.Length;

            for (var i = 0; i < entities.Length; i++)
            {
                currentReport.entitiesChecked++;
                currentReport.statusBar = Convert.ToInt32(i * 100 / entities.Length);

                var entity = entities[i];
                if (entity.IsValid() == false)
                {
                    continue;
                }

                var shortname = entity.ShortPrefabName;
                if (config.excludedEntities.Contains(shortname) == true)
                {
                    continue;
                }

                var info = (EntityInfo) null;
                if (entitiesByShortname.TryGetValue(shortname, out info) == false)
                {
                    info = new EntityInfo();
                    entitiesByShortname.Add(shortname, info);
                }


                if (entity.OwnerID == 0)
                {
                    info.countUnowned++;
                    currentReport.entities.countUnowned++;
                }
                else
                {
                    info.countOwned++;
                    currentReport.entities.countOwned++;
                }

                info.countGlobal++;
                currentReport.entities.countGlobal++;
            }

            currentReport.entities.list = currentReport.entities.list.OrderByDescending(x => x.Value.countGlobal).ToDictionary(x => x.Key, y => y.Value);

            currentReport.entities.completed = true;
        }

        #endregion

        #region Utils
        
        private void SaveReport(PerformanceDump dump)
        {
            string name1 = "";
            if(config.EuropeanTimeSave) name1 = DateTime.Now.ToString("dd/MM/yyyy").Replace("/", "-");
            else name1 = DateTime.Now.ToString("MM/dd/yyyy").Replace("/", "-");
            string name2 = DateTime.Now.ToString(Time()).Replace(':', '-');
            string filename = $"PerformanceMonitor/Reports/{name1}/{name2}";
            Interface.Oxide.DataFileSystem.WriteObject(filename, dump);
        }

        private string Time()
        {
            return DateTime.Now.ToString("HH:mm:ss");
        }

        #endregion
        
        #region Configuration | 2.0.0

        private static ConfigData config = new ConfigData();

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Save file dates in European Format 27-06-2023 (other format is North American 06-27-2023)")]
            public bool EuropeanTimeSave = false;
            
            [JsonProperty(PropertyName = "Create reports every (seconds)")]
            public int checkTime = 0;

            [JsonProperty(PropertyName = "Create plugins report")]
            public bool runPluginsReport = true;
            
            [JsonProperty(PropertyName = "Create plugins average hook-time report (Will substantially increase time of report)")]
            public bool runPluginsReportAverage = false;

            [JsonProperty(PropertyName = "Create entities report")]
            public bool runEntitiesReport = true;

            [JsonProperty(PropertyName = "Excluded entities")]
            public string[] excludedEntities =
            {
                "shortname here",
                "another here"
            };

            [JsonProperty(PropertyName = "Excluded plugins")]
            public string[] excludedPlugins =
            {
                "name here",
                "another name"
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();

                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");

                timer.Every(10f,
                    () =>
                    {
                        PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                    });
                LoadDefaultConfig();
                return;
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Classes

        private class PerformanceDump
        {
            [JsonProperty(PropertyName = "Online players")]
            public int onlinePlayers = BasePlayer.activePlayerList.Count;

            [JsonProperty(PropertyName = "Offline players")]
            public int offlinePlayers = BasePlayer.sleepingPlayerList.Count;

            [JsonProperty(PropertyName = "Entities report")]
            public EntitiesReport entities = new EntitiesReport();
            
            [JsonProperty(PropertyName = "Plugins report")]
            public string[] plugins;

            [JsonProperty(PropertyName = "Plugins Average")]
            public Dictionary<string, List<double>> pluginsAverage;

            [JsonProperty(PropertyName = "Performance report")]
            public Performance.Tick performance = Performance.current;
            
            [JsonIgnore] 
            public int statusBar;

            [JsonIgnore] 
            public int entitiesChecked;

            [JsonIgnore]
            public int entitiesTotal;
        }

        private class EntitiesReport
        {
            [JsonProperty(PropertyName = "Total")]
            public int countGlobal;
            
            [JsonProperty(PropertyName = "Owned")]
            public int countOwned;
            
            [JsonProperty(PropertyName = "Unowned")]
            public int countUnowned;
            
            [JsonProperty(PropertyName = "List")]
            public Dictionary<string, EntityInfo> list = new Dictionary<string, EntityInfo>();

            [JsonIgnore] 
            public bool completed;
        }

        private class EntityInfo
        {
            [JsonProperty(PropertyName = "Total")]
            public int countGlobal;
            
            [JsonProperty(PropertyName = "Owned")]
            public int countOwned;
            
            [JsonProperty(PropertyName = "Unowned")]
            public int countUnowned;
        }

        #endregion
    }
}