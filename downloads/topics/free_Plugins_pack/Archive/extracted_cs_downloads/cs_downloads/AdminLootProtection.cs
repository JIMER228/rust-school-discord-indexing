using Oxide.Core;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Admin Loot Protection", "RustFlash", "2.0.0")]
    [Description("Prevents looting admins and specified groups")]
    public class AdminLootProtection : RustPlugin
    {
        public const string PermissionBypass = "adminlootprotection.use";

        private void OnServerInitialized()
        {
            permission.RegisterPermission(PermissionBypass, this);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "AdminLootPermission", "You are not authorised to loot admins!" },
            }, this);
        }

        object CanLootPlayer(BasePlayer target, BasePlayer looter)
        {
            if (target.IsAdmin && target.userID != looter.userID && !permission.UserHasPermission(looter.UserIDString, PermissionBypass))
            {
                looter.ChatMessage(lang.GetMessage("AdminLootPermission", this, looter.UserIDString));
                return false;
            }
            return null;
        }

        object CanLootEntity(BasePlayer player, DroppedItemContainer container)
        {
            var target = BasePlayer.FindAwakeOrSleeping(container.playerSteamID.ToString());
            if (target != null && target.IsAdmin && target.userID != player.userID && !permission.UserHasPermission(player.UserIDString, PermissionBypass))
            {
                player.ChatMessage(lang.GetMessage("AdminLootPermission", this, player.UserIDString));
                return false;
            }
            return null;
        }

        object CanLootEntity(BasePlayer player, LootableCorpse corpse)
        {
            var target = BasePlayer.FindAwakeOrSleeping(corpse.playerSteamID.ToString());
            if (target != null && target.IsAdmin && target.userID != player.userID && !permission.UserHasPermission(player.UserIDString, PermissionBypass))
            {
                player.ChatMessage(lang.GetMessage("AdminLootPermission", this, player.UserIDString));
                return false;
            }
            return null;
        }
    }
}
