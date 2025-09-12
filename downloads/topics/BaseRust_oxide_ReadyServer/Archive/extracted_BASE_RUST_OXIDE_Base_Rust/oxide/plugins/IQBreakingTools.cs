using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("IQBreakingTools", "Menevt", "0.0.6")]
    [Description("rere")]
    class IQBreakingTools : RustPlugin
    {
        /// <summary>
        /// Обновление 0.0.6
        /// - Оптимизирован код
        /// - Добавлена возможность замедленной поломки предметов в N кол-во раз
        /// - Добавлена возможность отключения неломайки или замедленной поломки в чужой билде
        /// </summary>

        #region Vars
        public static readonly String IQBreakingToolsPermission = "IQBreakingTools.use".ToLower();
        public static readonly String IQWeapon = "IQBreakingTools.weapon".ToLower();
        public static readonly String IQTools = "IQBreakingTools.tools".ToLower();
        public static readonly String IQAttire = "IQBreakingTools.attire".ToLower();
        #endregion
    
        #region Configuration

        private static Configuration config = new Configuration();
        private class Configuration
        {
            [JsonProperty("Список предметов,которые не будут ломаться или на них будет замедленная поломка (shortname)")]
            public List<String> ToolsList = new List<String>();
            [JsonProperty("Список исключенных SkinID(Вещи с этим SkinID будут ломаться и на них не будет действовать замедленная поломка! Для кастомных предметов)")]
            public List<UInt64> BlackList = new List<UInt64>();
            [JsonProperty("Отключать неломайку если игрок атакует постройки в чужой билде(не авторизованный в шкафу)")]
            public Boolean StartLoseNoOwner;
            [JsonProperty("Настройка замедленной поломки")]
            public BreakingProcess BreakingProcesses = new BreakingProcess();
            internal class BreakingProcess
            {
                [JsonProperty("Включить замедленную поломку(неломайка заменится на замедленную поломку)")]
                public Boolean useProcessBreaking;
                [JsonProperty("На сколько срезать поломку (Пример : в 3 раза)")]
                public Single ProcessBreakingAmount;
            }

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    StartLoseNoOwner = false,
                    ToolsList = new List<String>
                    {
                        "rifle.ak",
                        "jackhammer",
                        "hatchet"
                    },
                    BlackList = new List<UInt64>
                    {
                        1337228,
                        2281337
                    },
                    BreakingProcesses = new BreakingProcess
                    {
                        useProcessBreaking = false,
                        ProcessBreakingAmount = 3,
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning("Ошибка #1975" + $"чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }
            NextTick(SaveConfig);
        }

        void RegisteredPermissions()
        {         
            permission.RegisterPermission(IQBreakingToolsPermission, this);
            permission.RegisterPermission(IQTools, this);
            permission.RegisterPermission(IQWeapon, this);
            permission.RegisterPermission(IQAttire, this);
            PrintWarning("Permissions - completed");
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Hooks
        private void OnServerInitialized()
        {
            RegisteredPermissions();
        }
        void OnLoseCondition(Item item, ref float amount)
        {
            if (item == null) return;
            BasePlayer player = item.GetOwnerPlayer();
            if (player == null) return;
            if (!player.UserIDString.IsSteamId()) return;
            if (config.StartLoseNoOwner)
                if (player.IsBuildingBlocked())
                    return;
            if (config.BlackList.Contains(item.skin)) return;
            var ItemCategory = ItemManager.FindItemDefinition(item.info.itemid).category;
            if (ItemCategory == ItemCategory.Weapon && permission.UserHasPermission(player.UserIDString, IQWeapon)
            || ItemCategory == ItemCategory.Attire && permission.UserHasPermission(player.UserIDString, IQAttire)
            || ItemCategory == ItemCategory.Tool && permission.UserHasPermission(player.UserIDString, IQTools))
            {
                if (!config.BreakingProcesses.useProcessBreaking)
                    amount = 0;
                else amount = amount -(Single)(amount / config.BreakingProcesses.ProcessBreakingAmount <= 0 ? 1 : config.BreakingProcesses.ProcessBreakingAmount);
            }
            else if (permission.UserHasPermission(player.UserIDString, IQBreakingToolsPermission))
                if (config.ToolsList.Contains(item.info.shortname))
                {
                    if (!config.BreakingProcesses.useProcessBreaking)
                        amount = 0;
                    else amount = amount- (Single)(amount / config.BreakingProcesses.ProcessBreakingAmount <= 0 ? 1 : config.BreakingProcesses.ProcessBreakingAmount);
                }
        }
        #endregion
    }
}
