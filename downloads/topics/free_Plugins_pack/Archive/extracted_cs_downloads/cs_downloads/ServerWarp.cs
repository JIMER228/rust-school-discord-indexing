using Oxide.Core.Plugins;
using Oxide.Core;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("ServerWarp", "Ghosty", "1.0.1")]
    [Description("Enables players to seamlessly warp between different servers")]
    class ServerWarp : RustPlugin
    {
        private ConfigData configData;
        private Dictionary<ulong, Timer> warpTimers = new Dictionary<ulong, Timer>();
        
        #region Permissions

        private const string PermissionCanAddWarp = "serverwarp.CanAddWarp";
        private const string PermissionWarp = "serverwarp.CanWarp";
        private const string PermissionCancelWarp = "serverwarp.CanCancel";

        #endregion

        #region Initialization

        private void Init()
        {
            permission.RegisterPermission(PermissionCanAddWarp, this);
            permission.RegisterPermission(PermissionWarp, this);
            permission.RegisterPermission(PermissionCancelWarp, this);
        }

        #endregion

        #region Configuration

        class ConfigData
        {
            public string WarpMessage { get; set; }
            public string CancelMessage { get; set; }
            public int CountdownSeconds { get; set; }
            public Dictionary<string, ServerInfo> Warps { get; set; }
        }

        class ServerInfo
        {
            public string IP { get; set; }
            public string Port { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                WarpMessage = "<size=20><color=red>Server</color>Warp</size>\n<size=15><color=white>You are being warped to {0} in {1} seconds...</size>",
                CancelMessage = "<size=20><color=red>Server</color>Warp</size>\n<size=15><color=white>Warp cancelled!</size>",
                CountdownSeconds = 5,
                Warps = new Dictionary<string, ServerInfo>
                {
                    { "Example_warp", new ServerInfo { IP = "0.0.0.0", Port = "28015" } }
                    
                }
            };
            Config.WriteObject(configData, true);
            SaveConfig();
        }

        private void Loaded()
        {
            configData = Config.ReadObject<ConfigData>();
        }

        #endregion

        #region Chat Commands

        [ChatCommand("warp")]
        private void WarpCommand(BasePlayer player, string command, string[] args)
        {
        if (!permission.UserHasPermission(player.UserIDString, PermissionWarp))
            {
        SendReply(player, "<size=20><color=red>Server</color>Warp</size>\n<size=15><color=white>You do not have permission to use this command.</size>");
        return;
            }
            if (args.Length == 0 || !configData.Warps.ContainsKey(args[0].ToLower()))
            {
                SendReply(player, "<size=20><color=red>Server</color>Warp</size>\n<size=15><color=white>Invalid warp. Available warps: " + string.Join(", </size>", configData.Warps.Keys));
                return;
            }

            CancelWarp(player);

            string warpName = args[0].ToLower();
            ServerInfo serverInfo = configData.Warps[warpName];

            string message = string.Format(configData.WarpMessage, warpName, configData.CountdownSeconds);
            SendReply(player, message);
            warpTimers[player.userID] = timer.Once(configData.CountdownSeconds, () => WarpPlayer(player, serverInfo.IP, serverInfo.Port));
        }

        [ChatCommand("addwarp")]
        private void AddWarpCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionCanAddWarp))
            {
                SendReply(player, "<size=20><color=red>Server</color>Warp</size>\n<size=15><color=white>You do not have permission to use this command.</size>");
                return;
            }

            if (args.Length != 3)
            {
                SendReply(player, "<size=20><color=red>Server</color>Warp</size>\n<size=15><color=white>Usage: /addwarp [warpName] [ip] [port]</size>");
                return;
            }

            string warpName = args[0].ToLower();
            string ip = args[1];
            string port = args[2];

            if (configData.Warps.ContainsKey(warpName))
            {
                SendReply(player, $"<size=20><color=red>Server</color>Warp</size>\n<size=15><color=white>Warp {warpName} already exists.</size>");
                return;
            }

            configData.Warps.Add(warpName, new ServerInfo { IP = ip, Port = port });
            Config.WriteObject(configData, true);
            SaveConfig();
            SendReply(player, $"<size=20><color=red>Server</color>Warp</size>\n<size=15><color=white>Warp {warpName} added successfully.</size>");
        }

        [ChatCommand("cancelwarp")]
        private void CancelWarpCommand(BasePlayer player, string command, string[] args)
        {
        if (!permission.UserHasPermission(player.UserIDString, PermissionCancelWarp))
            {
        SendReply(player, "<size=20><color=red>Server</color>Warp</size>\n<size=15><color=white>You do not have permission to use this command.</size>");
        return;
            }
            if (CancelWarp(player))
            {
                SendReply(player, configData.CancelMessage);
            }
        }

        private bool CancelWarp(BasePlayer player)
        {
            if (warpTimers.TryGetValue(player.userID, out Timer timer))
            {
                timer.Destroy();
                warpTimers.Remove(player.userID);
                return true;
            }
            return false;
        }

        #endregion

        #region Main Method

        private void WarpPlayer(BasePlayer player, string ip, string port)
        {
            warpTimers.Remove(player.userID);
            ConsoleNetwork.SendClientCommandImmediate(player.Connection, "nexus.redirect", ip, port, "");
            ConnectionAuth.Reject(player.Connection, "Redirecting to FreeFields/Creative/Building/Sandbox");
            PlatformService.Instance.EndPlayerSession(player.userID);
        }

        #endregion
    }
}
