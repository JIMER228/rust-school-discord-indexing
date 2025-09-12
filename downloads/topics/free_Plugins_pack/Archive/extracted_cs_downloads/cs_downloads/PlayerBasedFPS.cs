using Oxide.Core.Plugins;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PlayerBasedFPS", "Sery", "1.0.1")]
    [Description("This plugin dynamically adjusts the FPS limit according to the number of players on the server, providing high performance when there are players and reducing resource usage when there are no players.")]

    public class PlayerBasedFPS : RustPlugin
    {
        private class PluginConfig
        {
            public int FPSWithPlayers { get; set; } = 30;
            public int FPSWithoutPlayers { get; set; } = 5;
        }

        private PluginConfig config;
        private int lastPlayerCount = -1;

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating configuration...");
            config = new PluginConfig();
            SaveConfig();
        }

        private void SaveConfig()
        {
            Config.WriteObject(config, true);
        }

        private void LoadConfigData()
        {
            try
            {
                config = Config.ReadObject<PluginConfig>() ?? new PluginConfig();
            }
            catch (System.Exception ex)
            {
                PrintError($"Config is invalid: {ex.Message}, loading default values...");
                config = new PluginConfig();
            }
            SaveConfig();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            CheckPlayerCountAndSetFPS();
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            timer.Once(3f, () => {
                CheckPlayerCountAndSetFPS();
            });
        }

        private void CheckPlayerCountAndSetFPS()
        {
            timer.Once(1f, () =>
            {
                int playerCount = BasePlayer.activePlayerList.Count;

                if (playerCount != lastPlayerCount)
                {
                    if (playerCount > 0)
                    {
                        ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "fps.limit", config.FPSWithPlayers.ToString());
                    }
                    else
                    {
                        ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "fps.limit", config.FPSWithoutPlayers.ToString());
                    }

                    lastPlayerCount = playerCount;
                }
            });
        }

        private void Init()
        {
            LoadConfigData();
            timer.Once(3f, CheckPlayerCountAndSetFPS); 
        }

        private void OnServerInitialized()
        {
            timer.Once(3f, CheckPlayerCountAndSetFPS); 
        }

        private void Unload()
        {
            PrintWarning("PlayerBasedFPS plugin has been disabled. Resetting FPS limit.");
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "fps.limit", "default");
        }
    }
}
