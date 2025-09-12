using UnityEngine;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Impact Grenades", "Lincoln", "1.0.3")]
    [Description("Makes grenades explode on impact")]
    public class ImpactGrenades : RustPlugin
    {
        private const string PERMISSION_USE = "ImpactGrenades.use";
        private HashSet<ulong> enabledPlayers = new HashSet<ulong>();
        const float fuseTime = 100f;

        private void Init()
        {
            permission.RegisterPermission(PERMISSION_USE, this);
            cmd.AddChatCommand("impact", this, CmdToggleImpact);
            LoadDefaultMessages();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You don't have permission to use impact grenades!",
                ["Disabled"] = "Impact grenades disabled!",
                ["Enabled"] = "Impact grenades enabled!",
                ["Prefix"] = "<color=#ff0000>[Impact Grenades]</color> "
            }, this);
        }

        private void Message(BasePlayer player, string messageKey)
        {
            if (player == null) return;
            player.ChatMessage($"{GetMsg("Prefix")}{GetMsg(messageKey, player.UserIDString)}");
        }

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        private void CmdToggleImpact(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                Message(player, "NoPermission");
                return;
            }

            if (enabledPlayers.Contains(player.userID))
            {
                enabledPlayers.Remove(player.userID);
                Message(player, "Disabled");
                return;
            }

            enabledPlayers.Add(player.userID);
            Message(player, "Enabled");
        }
        private void SetupImpactGrenade(TimedExplosive grenade)
        {
            grenade.SetFuse(fuseTime);
            grenade.explodeOnContact = true;
        }

        private void OnExplosiveThrown(BasePlayer player, TimedExplosive grenade)
        {
            if (!CanUseImpactGrenade(player, grenade)) return;
            SetupImpactGrenade(grenade);
        }

        private void OnExplosiveDropped(BasePlayer player, TimedExplosive grenade)
        {
            if (!CanUseImpactGrenade(player, grenade)) return;
            SetupImpactGrenade(grenade);
        }
        private bool CanUseImpactGrenade(BasePlayer player, TimedExplosive grenade)
        {
            return !(grenade == null || player == null) &&
                   enabledPlayers.Contains(player.userID) &&
                   grenade.ShortPrefabName.Contains("grenade", System.StringComparison.OrdinalIgnoreCase) && permission.UserHasPermission(player.UserIDString, PERMISSION_USE);
        }
    }
}