using System;
using Oxide.Core;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("KillJets", "Singularity", "1.0.1")]
    [Description("Plugin to delete F15e jets")]
    public class KillJets : RustPlugin
    {
        private const string PermissionKillJets = "killjets.use";

        private void Init()
        {
            permission.RegisterPermission(PermissionKillJets, this);
        }

        [ChatCommand("killjets")]
        private void KillJetsCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionKillJets))
            {
                SendReply(player, "You do not have permission to use this command.");
                return;
            }

            var jets = BaseNetworkable.serverEntities
                .Where(x => x is BaseEntity && ((BaseEntity)x).ShortPrefabName == "f15e")
                .ToList();

            int jetCount = jets.Count;

            foreach (BaseEntity jet in jets)
            {
                jet.Kill();
            }

            if (jetCount > 0)
            {
                SendReply(player, $"Deleted {jetCount} F15e jets.");
            }
            else
            {
                SendReply(player, "No F15e jets found.");
            }
        }
    }
}
