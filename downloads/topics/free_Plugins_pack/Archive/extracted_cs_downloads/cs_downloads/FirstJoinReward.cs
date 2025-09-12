using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Oxide.Plugins
{
    [Info("FirstJoinReward", "MaltrzD", "0.0.2")]
    [Description("Плагин сделал [olol321 Aka MaltrzD] // Discord: 36.66")]
    class FirstJoinReward : RustPlugin
    {
        private List<ulong> rewardedUsers;
        private ConfigData configuration;

        #region OXIDE HOOKS
        private void Loaded()
        {
            ReadConfig();
            LoadRewardedUsers();
        }
        private void OnPlayerConnected(BasePlayer player)
        {
            GiveReward(player);
        }
        #endregion

        #region EXT
        private bool CheckPlayerHaveReward(ulong userID)
        {
            if (rewardedUsers.Contains(userID)) return true;
            else return false;
        }
        #endregion
        #region METHODS
        private void GiveReward(BasePlayer player)
        {
            if (player == null) return;
            if (CheckPlayerHaveReward(player.userID)) return;

            foreach (var commandToExecute in configuration.rewardCommand)
                Server.Command(string.Format(commandToExecute, player.userID));

            player.ChatMessage(string.Format(configuration.rewardMessage, player.displayName));

            rewardedUsers.Add(player.userID); SaveRewardedUsers();
        }
        #endregion

        #region CONSOLE COMMANDS
        [ConsoleCommand("fjrclear")]
        private void ClearRewardedUsers()
        {
            rewardedUsers = new List<ulong>();
            SaveRewardedUsers();
            Puts("Успешно очистили список игроков котором уже выдали награды!");
        }
        #endregion

        #region USERS DATA
        private DynamicConfigFile RewardedUsers_File = Interface.Oxide.DataFileSystem.GetFile("FirstJoinReward/RewardedUsers");

        private void LoadRewardedUsers()
        {
            if (RewardedUsers_File.Exists() == false) { rewardedUsers = new List<ulong>(); SaveRewardedUsers(); }

            rewardedUsers = RewardedUsers_File.ReadObject<List<ulong>>();

            if (configuration == null)
            {
                rewardedUsers = new List<ulong>();
                SaveRewardedUsers();
            }
        }
        private void SaveRewardedUsers() => RewardedUsers_File.WriteObject(rewardedUsers);
        #endregion
        #region CONFIGURATION
        class ConfigData
        {
            [JsonProperty("Команда после первого входа")] public List<string> rewardCommand = new List<string>() 
            {
                "o.usergroup add {0} titan"
            };
            [JsonProperty("Сообщение игроку после выполнения команды")] public string rewardMessage = "{0}, вы впервые зашли на наш сервер, и получили привелегию <color=yellow>TITAN!</color>";
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData();
            SaveConfig(config);
        }
        void SaveConfig(object config)
        {
            Config.WriteObject(config, true);
        }
        void ReadConfig()
        {
            base.Config.Settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
            configuration = Config.ReadObject<ConfigData>();
            SaveConfig(configuration);
        }
        #endregion
    }
}