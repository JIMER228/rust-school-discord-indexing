using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("Welcomer", "Dana", "2.0.0")]
    [Description("Welcomes players and announces when they join or leave.")]
    public class Welcomer : RustPlugin
    {
        #region Fields

        private const string permissionBypass = "welcomer.bypass";

        private static Configuration config;

        private Data data;
        private DynamicConfigFile dataFile;

        private List<ulong> playersToWelcome = new List<ulong>();

        #endregion

        #region Configuration

        private class Configuration
        {
            [JsonProperty(PropertyName = "Avatar Image")]
            public ulong AvatarImage { get; set; }

            [JsonProperty(PropertyName = "Enable Chat Welcome Message")]
            public bool EnableChatWelcomeMessage { get; set; }

            [JsonProperty(PropertyName = "Enable Console Welcome Message")]
            public bool EnableConsoleWelcomeMessage { get; set; }

            [JsonProperty(PropertyName = "Enable Join Message")]
            public bool EnableJoinMessage { get; set; }

            [JsonProperty(PropertyName = "Enable Newcomer Join Message")]
            public bool EnableNewcomerJoinMessage { get; set; }

            [JsonProperty(PropertyName = "Enable Leave Message")]
            public bool EnableLeaveMessage { get; set; }

            [JsonProperty(PropertyName = "Clear Data On Wipe")]
            public bool ClearDataOnWipe { get; set; }
        }

        private Configuration DefaultConfig()
        {
            return new Configuration
            {
                AvatarImage = 0,
                EnableChatWelcomeMessage = true,
                EnableConsoleWelcomeMessage = true,
                EnableJoinMessage = true,
                EnableNewcomerJoinMessage = true,
                EnableLeaveMessage = true,
                ClearDataOnWipe = true,
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch (Exception exception)
            {
                PrintWarning("Failed to load the configuration file");
                PrintError(exception.ToString());
                return;
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            config = DefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config, true);
        }

        #endregion

        #region Data

        private class Data
        {
            [JsonProperty(PropertyName = "Players")]
            public List<ulong> Players { get; set; } = new List<ulong>();
        }

        private class PlayerData
        {
            public string Country { get; set; }
        }

        private void LoadData()
        {
            try
            {
                dataFile = Interface.Oxide.DataFileSystem.GetFile(Name);
                data = dataFile.ReadObject<Data>();

                if (data == null)
                    data = new Data();
            }
            catch (Exception exception)
            {
                PrintWarning("Failed to load the data file");
                PrintError(exception.ToString());
                PrintWarning("Creating a new data file");
                data = new Data();
            }
            SaveData();
        }

        private void SaveData()
        {
            dataFile.WriteObject(data);
        }

        private void ClearData()
        {
            data = new Data();
            SaveData();
        }

        #endregion

        #region Initialization and Quitting

        private void Init()
        {
            permission.RegisterPermission(permissionBypass, this);
            LoadData();
        }

        private void Unload()
        {
            config = null;
            SaveData();
        }

        #endregion

        #region Server Save

        private void OnServerSave()
        {
            SaveData();
        }

        private void OnNewSave(string fileName)
        {
            if (config.ClearDataOnWipe)
                ClearData();
        }

        #endregion

        #region Player Hooks

        private void OnPlayerConnected(BasePlayer player)
        {
            if (config.EnableChatWelcomeMessage || config.EnableConsoleWelcomeMessage)
                playersToWelcome.Add(player.userID);

            if (HasPermission(player, permissionBypass))
                return;

            if (!config.EnableJoinMessage && !config.EnableNewcomerJoinMessage)
                return;

            string country = string.Empty;
            string apiUrl = "http://ip-api.com/json/";
            string ipAddress = ProcessAddress(player);

            webrequest.Enqueue(apiUrl + ipAddress, null, (statusResponseCode, response) =>
            {
                if (statusResponseCode != 200 || string.IsNullOrEmpty(response))
                    PrintWarning($"The web request to {apiUrl} for {player.userID} failed.");

                country = JsonConvert.DeserializeObject<PlayerData>(response).Country ?? "Unknown";

                if (!data.Players.Contains(player.userID) && config.EnableNewcomerJoinMessage)
                {
                    SendMessageToAll(GetMessage(MessageKey.JoinNewcomer, player.UserIDString, player.displayName, country));
                    data.Players.Add(player.userID);
                }
                else
                {
                    if (config.EnableJoinMessage)
                        SendMessageToAll(GetMessage(MessageKey.Join, player.UserIDString, player.displayName, country));
                }
            }, this, RequestMethod.GET);
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!playersToWelcome.Contains(player.userID))
                return;

            int activePlayers = BasePlayer.activePlayerList.Count;
            int sleepingPlayers = BasePlayer.sleepingPlayerList.Count;
            int queuedPlayers = ServerMgr.Instance.connectionQueue.queue.Count;
            int joiningPlayers = ServerMgr.Instance.connectionQueue.joining.Count;

            if (config.EnableChatWelcomeMessage)
                SendChatMessage(player, GetMessage(MessageKey.WelcomeChat, player.UserIDString, activePlayers, sleepingPlayers, joiningPlayers, queuedPlayers));

            if (config.EnableConsoleWelcomeMessage)
                SendConsoleMessage(player, GetMessage(MessageKey.WelcomeConsole, player.UserIDString, activePlayers, sleepingPlayers, joiningPlayers, queuedPlayers));

            playersToWelcome.Remove(player.userID);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (HasPermission(player, permissionBypass))
                return;

            if (!config.EnableLeaveMessage)
                return;

            SendMessageToAll(GetMessage(MessageKey.Leave, player.UserIDString, player.displayName, reason));
        }

        #endregion

        #region Commands

        private static class Command
        {
            public const string Clear = "welcomer.clear";
            public const string Test = "welcomer.test";
        }

        [ConsoleCommand(Command.Clear)]
        private void CmdClearData(ConsoleSystem.Arg conArgs)
        {
            if (conArgs.IsClientside)
                return;

            ClearData();
            Puts("The data file has been cleared");
        }

        [ChatCommand(Command.Test)]
        private void CmdSendTestMessage(BasePlayer player, string command, string[] args)
        {
            if (player == null)
                return;

            if (!player.IsAdmin)
                return;

            SendReply(player, GetMessage(MessageKey.Test, player.UserIDString));
        }

        #endregion

        #region Helper Functions

        private bool HasPermission(BasePlayer player, string permissionName)
        {
            return permission.UserHasPermission(player.UserIDString, permissionName);
        }

        private void SendConsoleMessage(BasePlayer player, string message)
        {
            player.ConsoleMessage(message);
        }

        private void SendChatMessage(BasePlayer player, string message)
        {
            Player.Message(player, message, config.AvatarImage);
        }

        private void SendMessageToAll(string message)
        {
            Server.Broadcast(message, config.AvatarImage);
        }

        private string ProcessAddress(BasePlayer player)
        {
            string[] ipAddress = player.net?.connection?.ipaddress?.Split(':');

            if (ipAddress == null || ipAddress.Length == 0)
                return null;

            string ipResult = ipAddress[0];
            return ipResult;
        }

        #endregion

        #region Localization

        private static class MessageKey
        {
            public const string WelcomeChat = "Welcome.Chat";
            public const string WelcomeConsole = "Welcome.Console";
            public const string Join = "Join";
            public const string JoinNewcomer = "Join.Newcomer ";
            public const string Leave = "Leave";
            public const string Test = "Test";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [MessageKey.WelcomeChat] = "There are currently {0} active players, {1} joining, {2} sleepers, and {3} in the queue",
                [MessageKey.WelcomeConsole] = "There are currently {0} active players, {1} joining, {2} sleepers, and {3} in the queue",
                [MessageKey.Join] = "{0} joined from {1}",
                [MessageKey.JoinNewcomer] = "{0} joined for the very first time from {1}",
                [MessageKey.Leave] = "{0} left ( {1} )",
                [MessageKey.Test] = "Test message",
            }, this, "en");
        }

        private string GetMessage(string messageKey, string playerId = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(messageKey, this, playerId), args);
            }
            catch (Exception exception)
            {
                PrintError(exception.ToString());
                throw;
            }
        }

        #endregion
    }
}