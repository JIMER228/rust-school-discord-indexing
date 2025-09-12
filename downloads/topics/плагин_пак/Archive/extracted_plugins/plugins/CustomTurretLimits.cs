using Oxide.Core;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Custom Turret Limits", "YourName", "1.0.0")]
    [Description("Custom turret limits with VIP support")]
    public class CustomTurretLimits : RustPlugin
    {
        private Dictionary<string, TurretLimits> playerLimits = new Dictionary<string, TurretLimits>();
        
        private class TurretLimits
        {
            public int AutoTurrets { get; set; } = 2;
            public int ShotgunTurrets { get; set; } = 4;
            public int FlameTurrets { get; set; } = 1;
            public int SamTurrets { get; set; } = 1;
            public int Recyclers { get; set; } = 1;
            public int StaticRecyclers { get; set; } = 0;
            public bool IsVIP { get; set; } = false;
        }

        void Init()
        {
            // Загружаем конфигурацию
            LoadConfig();
            
            // Регистрируем команды
            cmd.AddChatCommand("turretlimits", this, CmdTurretLimits);
            cmd.AddChatCommand("setvip", this, CmdSetVIP);
        }

        void OnServerInitialized()
        {
            // Устанавливаем базовые лимиты из конфигурации
            // Эти строки удалены, так как таких переменных не существует в ConVar.Server
            //ConVar.Server.maxautoturretsperplayer = config.DefaultLimits.AutoTurrets;
            //ConVar.Server.maxshotgunturretsperplayer = config.DefaultLimits.ShotgunTurrets;
            //ConVar.Server.maxflameturretsperplayer = config.DefaultLimits.FlameTurrets;
            //ConVar.Server.maxsamturretsperplayer = config.DefaultLimits.SamTurrets;
            
            Puts("CustomTurretLimits: Лимиты турелей установлены");
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (player == null) return;
            
            // Проверяем, является ли игрок VIP
            bool isVIP = permission.UserHasPermission(player.UserIDString, "customturretlimits.vip");
            
            playerLimits[player.UserIDString] = new TurretLimits
            {
                AutoTurrets = isVIP ? config.VIPLimits.AutoTurrets : config.DefaultLimits.AutoTurrets,
                ShotgunTurrets = isVIP ? config.VIPLimits.ShotgunTurrets : config.DefaultLimits.ShotgunTurrets,
                FlameTurrets = isVIP ? config.VIPLimits.FlameTurrets : config.DefaultLimits.FlameTurrets,
                SamTurrets = isVIP ? config.VIPLimits.SamTurrets : config.DefaultLimits.SamTurrets,
                Recyclers = isVIP ? config.VIPLimits.Recyclers : config.DefaultLimits.Recyclers,
                StaticRecyclers = isVIP ? config.VIPLimits.StaticRecyclers : config.DefaultLimits.StaticRecyclers,
                IsVIP = isVIP
            };
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null) return;
            playerLimits.Remove(player.UserIDString);
        }

        void OnEntityBuilt(BaseEntity entity, BasePlayer player)
        {
            if (entity == null || player == null) return;
            
            // Проверяем различные типы турелей и переработчиков
            if (entity is AutoTurret || entity is FlameTurret || entity is SamSite ||
                entity.ShortPrefabName == "recycler" || entity.ShortPrefabName == "recycler_static")
            {
                CheckTurretLimits(player, entity);
            }
        }

        private void CheckTurretLimits(BasePlayer player, BaseEntity entity)
        {
            if (!playerLimits.ContainsKey(player.UserIDString))
            {
                OnPlayerInit(player);
            }
            
            var limits = playerLimits[player.UserIDString];
            int currentAutoTurrets = GetPlayerTurretCount(player, "AutoTurret");
            int currentShotgunTurrets = GetPlayerTurretCount(player, "ShotgunTurret");
            int currentFlameTurrets = GetPlayerTurretCount(player, "FlameTurret");
            int currentSamTurrets = GetPlayerTurretCount(player, "SamSite");
            int currentRecyclers = GetPlayerTurretCount(player, "Recycler");
            int currentStaticRecyclers = GetPlayerTurretCount(player, "RecyclerStatic");
            
            bool canPlace = true;
            string typeName = "";
            int maxLimit = 0;
            
            if (entity is AutoTurret)
            {
                typeName = "авто-турели";
                canPlace = currentAutoTurrets < limits.AutoTurrets;
                maxLimit = limits.AutoTurrets;
            }
            else if (entity is FlameTurret)
            {
                typeName = "огнемет-турели";
                canPlace = currentFlameTurrets < limits.FlameTurrets;
                maxLimit = limits.FlameTurrets;
            }
            else if (entity is SamSite)
            {
                typeName = "ЗРК-турели";
                canPlace = currentSamTurrets < limits.SamTurrets;
                maxLimit = limits.SamTurrets;
            }
            else if (entity.ShortPrefabName == "recycler")
            {
                typeName = "переработчики (обычные)";
                canPlace = currentRecyclers < limits.Recyclers;
                maxLimit = limits.Recyclers;
            }
            else if (entity.ShortPrefabName == "recycler_static")
            {
                typeName = "переработчики (статические)";
                canPlace = currentStaticRecyclers < limits.StaticRecyclers;
                maxLimit = limits.StaticRecyclers;
            }
            
            if (!canPlace)
            {
                player.ChatMessage($"Вы достигли лимита {typeName}! Максимум: {maxLimit}");
                entity.Kill();
            }
        }

        private int GetPlayerTurretCount(BasePlayer player, string type)
        {
            int count = 0;
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var baseEntity = entity as BaseEntity;
                if (baseEntity == null) continue;
                if (baseEntity.OwnerID != player.userID) continue;
                switch (type)
                {
                    case "AutoTurret":
                        if (baseEntity is AutoTurret) count++;
                        break;
                    case "ShotgunTurret":
                        if (baseEntity.ShortPrefabName == "shotguntrap") count++;
                        break;
                    case "FlameTurret":
                        if (baseEntity is FlameTurret) count++;
                        break;
                    case "SamSite":
                        if (baseEntity is SamSite) count++;
                        break;
                    case "Recycler":
                        if (baseEntity.ShortPrefabName == "recycler") count++;
                        break;
                    case "RecyclerStatic":
                        if (baseEntity.ShortPrefabName == "recycler_static") count++;
                        break;
                }
            }
            return count;
        }

        private int GetTurretLimit(BaseEntity entity, TurretLimits limits)
        {
            if (entity is AutoTurret) return limits.AutoTurrets;
            if (entity is FlameTurret) return limits.FlameTurrets;
            if (entity is SamSite) return limits.SamTurrets;
            if (entity.ShortPrefabName == "recycler") return limits.Recyclers;
            if (entity.ShortPrefabName == "recycler_static") return limits.StaticRecyclers;
            return 0;
        }

        void CmdTurretLimits(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            
            if (!playerLimits.ContainsKey(player.UserIDString))
            {
                OnPlayerInit(player);
            }
            
            var limits = playerLimits[player.UserIDString];
            
            player.ChatMessage($"Ваши лимиты:");
            player.ChatMessage($"Авто-турели: {limits.AutoTurrets}");
            player.ChatMessage($"Дробовик-турели: {limits.ShotgunTurrets}");
            player.ChatMessage($"Огнемет-турели: {limits.FlameTurrets}");
            player.ChatMessage($"ЗРК-турели: {limits.SamTurrets}");
            player.ChatMessage($"Переработчики (обычные): {limits.Recyclers}");
            player.ChatMessage($"Переработчики (статические): {limits.StaticRecyclers}");
            player.ChatMessage($"VIP статус: {(limits.IsVIP ? "Да" : "Нет")}");
        }

        void CmdSetVIP(BasePlayer player, string command, string[] args)
        {
            if (player == null || !player.IsAdmin) return;
            
            if (args.Length < 2)
            {
                player.ChatMessage("Использование: /setvip <steamid> <true/false>");
                return;
            }
            
            string targetID = args[0];
            bool isVIP = args[1].ToLower() == "true";
            
            if (isVIP)
            {
                permission.GrantUserPermission(targetID, "customturretlimits.vip", this);
            }
            else
            {
                permission.RevokeUserPermission(targetID, "customturretlimits.vip");
            }
            
            // Обновляем лимиты для игрока, если он онлайн
            var targetPlayer = BasePlayer.FindByID(ulong.Parse(targetID));
            if (targetPlayer != null)
            {
                OnPlayerInit(targetPlayer);
                targetPlayer.ChatMessage($"Ваш VIP статус изменен на: {(isVIP ? "Да" : "Нет")}");
            }
            
            player.ChatMessage($"VIP статус для {targetID} установлен: {isVIP}");
        }

        #region Configuration
        private ConfigData config;
        
        private class ConfigData
        {
            public TurretLimits DefaultLimits { get; set; } = new TurretLimits
            {
                AutoTurrets = 15,
                ShotgunTurrets = 18,
                FlameTurrets = 13,
                SamTurrets = 12,
                Recyclers = 1,
                StaticRecyclers = 0
            };
            
            public TurretLimits VIPLimits { get; set; } = new TurretLimits
            {
                AutoTurrets = 25,
                ShotgunTurrets = 30,
                FlameTurrets = 20,
                SamTurrets = 18,
                Recyclers = 2,
                StaticRecyclers = 0
            };
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }
        
        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }
        
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion
    }
} 