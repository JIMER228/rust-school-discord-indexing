using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("TPAll", "Crunchy", "1.0.0")]
    [Description("Teleports all players to you except those with bypass permission")]

    public class TPAll : RustPlugin
    {
        // Permission for players who can use the TPAll command
        private const string TPAllPermission = "tpall.use";

        // Permission for players who can bypass the teleportation
        private const string BypassPermission = "tpall.bypass";

        private void Init()
        {
            permission.RegisterPermission(TPAllPermission, this);
            permission.RegisterPermission(BypassPermission, this);
        }

        // Command to teleport all players to the admin
        [ChatCommand("tpall")]
        private void TPAllCommand(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.UserIDString, TPAllPermission))
            {
                TeleportAllToPlayer(player);
            }
            else
            {
                SendReply(player, "You do not have permission to use this command.");
            }
        }

        // Method to teleport all players to the admin
        private void TeleportAllToPlayer(BasePlayer admin)
        {
            List<BasePlayer> playersToTeleport = BasePlayer.activePlayerList.ToList();

            foreach (BasePlayer targetPlayer in playersToTeleport)
            {
                if (!permission.UserHasPermission(targetPlayer.UserIDString, BypassPermission))
                {
                    targetPlayer.Teleport(admin.transform.position);
                    SendReply(targetPlayer, $"You have been teleported to {admin.displayName}.");
                }
            }

            Puts($"{playersToTeleport.Count} players have been teleported to {admin.displayName}.");
        }
    }
}
