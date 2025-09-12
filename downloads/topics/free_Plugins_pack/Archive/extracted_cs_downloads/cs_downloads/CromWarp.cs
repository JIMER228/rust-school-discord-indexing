using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CromWarp", "Baks", "1.0")]
    public class CromWarp : RustPlugin
    {
        #region Fields

        

        #endregion

        #region Config

        class WarpConfig
        {
            public string Permission;

            public Vector3 Position;
        }

        static Configuration config = new Configuration();

        class Configuration
        {
            [JsonProperty("Список точек")] public Dictionary<string,WarpConfig> WarpConfigs;
            [JsonProperty("Максимальное расстояние для телепорта")] public int TeleportDistance;

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    WarpConfigs = new Dictionary<string, WarpConfig>
                    {
                        
                    },
                    TeleportDistance = 100
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) PrintWarning("NULL");
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintError($"Не удалось найти конфигурацию 'oxide/config/{Name}', Создание конфига!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Hooks

        void OnServerInitialized()
        {
            foreach (var warp in config.WarpConfigs.Values) permission.RegisterPermission(warp.Permission,this);
        }

        #endregion

        #region Methods

        void CreateWarp(BasePlayer player, string key)
        {
            if (config.WarpConfigs.ContainsKey(key))
            {
                SendReply(player,$"Warp {key} already exist's");
                return;
            }
            config.WarpConfigs.Add(key,new WarpConfig
            {
                Position = player.transform.position,
                Permission = $"cromwarp.{key}"
            });
            permission.RegisterPermission($"cromwarp.{key}",this);
            SendReply(player,$"Warp {key} succesfully created!\nCommmand to tp: /warp {key}\nPermission:cromwarp.{key}");
            SaveConfig();
        }

        void TryTeleportToWarp(BasePlayer player, string key)
        {
            if (!config.WarpConfigs.ContainsKey(key))
            {
                SendReply(player,$"Неверно указан код точки");
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString,$"cromwarp.{key}"))
            {
                SendReply(player,$"Недостаточно прав для использования команды");
                return;
            }

            if (Vector3.Distance(player.transform.position,config.WarpConfigs[key].Position)> config.TeleportDistance)
            {
                SendReply(player,$"Расстояние должно быть менее {config.TeleportDistance} метров");
                return;
            }
            player.Teleport(config.WarpConfigs[key].Position);
        }

        #endregion

        #region Commands

        [ChatCommand("acwarp")]
        void AdminCreateWarp(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin || args.Length < 1) return;
            CreateWarp(player,args[0]);
            
        }

        [ChatCommand("warp")]
        void WarpTeleportCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length>0) TryTeleportToWarp(player,args[0]);
        }

        #endregion
    }
}