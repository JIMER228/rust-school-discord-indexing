using System.Collections.Generic;
using Network;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Change Icon", "Kalvi", "1.2.0")]
    [Description("Set a customizable icon for all non user messages")]

    class ChangeIcon : CovalencePlugin
    {
        #region Config
        
        private Configuration _configuration;
        
        private class Configuration
        {
            [JsonProperty(PropertyName = "User SteamID")]
            public ulong SteamAvatarUserID = 76561198272553228; // Default SteamID
        }
        
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            _configuration = new Configuration();
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            _configuration = Config.ReadObject<Configuration>();
            SaveConfig();
        }
        
        protected override void SaveConfig() => Config.WriteObject(_configuration);
        
        #endregion

        #region Initialization

        private void Init()
        {
            permission.RegisterPermission("changeicon.admin", this);
        }

        #endregion

        #region Commands
        
        [Command("changeicon")]
        private void ChangeIconCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission("changeicon.admin"))
            {
                player.Reply(Lang("NoPermission", player.Id));
                return;
            }

            if (args.Length == 1 && ulong.TryParse(args[0], out ulong steamID))
            {
                _configuration.SteamAvatarUserID = steamID;
                SaveConfig();
                player.Reply(Lang("IconChanged", player.Id, steamID));
            }
            else if (args.Length == 1 && args[0].ToLower() == "reload")
            {
                LoadConfig();
                player.Reply(Lang("ConfigReloaded", player.Id));
            }
            else
            {
                player.Reply(Lang("Usage", player.Id));
            }
        }
        
        #endregion

        #region Hooks 
        
        private void OnBroadcastCommand(string command, object[] args)
        {
            TryApplySteamAvatarUserID(command, args);
        }
        
        private void OnSendCommand(Connection cn, string command, object[] args)
        {
            TryApplySteamAvatarUserID(command, args);
        }
        
        private void OnSendCommand(List<Connection> cn, string command, object[] args)
        {
            TryApplySteamAvatarUserID(command, args);
        }

        #endregion

        #region Helpers

        private void TryApplySteamAvatarUserID(string command, object[] args)
        {
            if (args == null || _configuration == null) 
                return;
            
            if (args.Length < 2 || (command != "chat.add" && command != "chat.add2")) 
                return;

            ulong providedID;
            if (ulong.TryParse(args[1].ToString(), out providedID) && providedID == 0)
                args[1] = _configuration.SteamAvatarUserID;
        }

        private string Lang(string key, string userId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, userId), args);
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "<color=#FF0000>You do not have permission to use this command.</color>",
                ["IconChanged"] = "<color=#00FF00>Steam ID has been changed to: {0}</color>",
                ["InvalidSteamID"] = "<color=#FF0000>Invalid Steam ID.</color>",
                ["ConfigReloaded"] = "<color=#00FF00>Configuration reloaded.</color>",
                ["Usage"] = "<color=#FFFF00>Usage: /changeicon <SteamID> or /changeicon reload</color>"
            }, this);
        }

        #endregion
    }
} 