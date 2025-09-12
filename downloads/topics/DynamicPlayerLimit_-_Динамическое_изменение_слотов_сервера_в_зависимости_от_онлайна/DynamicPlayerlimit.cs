using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Libraries;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("DynamicPlayerLimit", "Сингапур", "1.0.0")]
    [Description("Динамически изменяет количество слотов на сервере в зависимости от количества игроков.")]
    public class DynamicPlayerLimit : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            public int MaxPlayers { get; set; }
            public int MinPlayers { get; set; }
            public int IncreaseThreshold { get; set; }
            public int DecreaseThreshold { get; set; }
            public int UpdateInterval { get; set; }

            public void Init()
            {
                MaxPlayers = MaxPlayers > 0 ? MaxPlayers : 100;
                MinPlayers = MinPlayers > 0 && MinPlayers < MaxPlayers ? MinPlayers : 20;
                IncreaseThreshold = IncreaseThreshold > MinPlayers && IncreaseThreshold < MaxPlayers ? IncreaseThreshold : 80;
                DecreaseThreshold = DecreaseThreshold > MinPlayers && DecreaseThreshold < MaxPlayers ? DecreaseThreshold : 40;
                UpdateInterval = UpdateInterval > 0 ? UpdateInterval : 300;
            }
        }

        #endregion

        #region Initialization

        private Timer timer;

        private void Init()
        {
            config = Config.ReadObject<Configuration>();
            config.Init();
            Config.WriteObject(config);
            timer = Timer.Repeat(config.UpdateInterval, 0, () => AdjustPlayerLimit());
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new Configuration
            {
                MaxPlayers = 100,
                MinPlayers = 20,
                IncreaseThreshold = 80,
                DecreaseThreshold = 40,
                UpdateInterval = 300
            }, true);
        }

        #endregion

        #region Core Logic

        private void AdjustPlayerLimit()
        {
            var currentPlayers = BasePlayer.activePlayerList.Count;
            var serverLimit = ConVar.Server.maxplayers;

            if (currentPlayers >= config.IncreaseThreshold && serverLimit < config.MaxPlayers)
            {
                int newLimit = Math.Min(serverLimit + 10, config.MaxPlayers);
                SetServerLimit(newLimit);
                Puts($"Увеличено количество слотов до {newLimit}");
            }
            else if (currentPlayers <= config.DecreaseThreshold && serverLimit > config.MinPlayers)
            {
                int newLimit = Math.Max(serverLimit - 10, config.MinPlayers);
                SetServerLimit(newLimit);
                Puts($"Уменьшено количество слотов до {newLimit}");
            }
        }

        private void SetServerLimit(int newLimit)
        {
            ConVar.Server.maxplayers = newLimit;
            // Notify players if needed
            var players = covalence.Players.Connected;
            foreach (var player in players)
            {
                player.Message($"Количество слотов на сервере изменено на {newLimit}");
            }
        }

        #endregion

        #region Shutdown

        private void Unload()
        {
            timer.Destroy();
        }

        #endregion
    }
}