using System;
using System.Collections.Generic;
namespace Oxide.Plugins
{
    [Info("TeamRestriction", "wazzzup", "1.0.1")]
    [Description("TeamRestriction")]

    public class TeamRestriction : RustPlugin
    {
        void OnTurretAuthorize(AutoTurret turret, BasePlayer player)
        {
            if (turret.authorizedPlayers.Count >= RelationshipManager.maxTeamSize)
            {
                SendMsg(player, "turretCleared"); 
                turret.authorizedPlayers.Clear();
            }
        }

        void OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
        { 
            if (privilege.authorizedPlayers.Count >= RelationshipManager.maxTeamSize)
            {
                SendMsg(player, "cupboardCleared");
                privilege.authorizedPlayers.Clear();
				privilege.SendNetworkUpdate();
            }
        }

        void OnCodeEntered(CodeLock lck, BasePlayer player, string code)
        {
            bool flag1 = code == lck.code;
            bool flag2 = (lck.hasGuestCode && code == lck.guestCode);
            if ((flag1 || flag2) && lck.whitelistPlayers.Count + lck.guestPlayers.Count >= RelationshipManager.maxTeamSize)
            {
                SendMsg(player, "codelockCleared");
                lck.whitelistPlayers.Clear();
            }
        }
        private string msg(string key, BasePlayer player = null) => lang.GetMessage(key, this, player?.UserIDString);
        private void SendMsg(BasePlayer player, string langkey, bool title = true, params string[] args)
        {
            string message = String.Format(msg(langkey, player), args);
            if (title) message = msg("Title", player)+message;
            SendReply(player, message);
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"Title", "<color=orange>Team:</color>" },
                {"turretCleared", "<color=red>Limit reached. Turret cleared</color>" },
                {"cupboardCleared", "<color=red>Limit reached. Cupboard cleared</color>" },
                {"codelockCleared", "<color=red>Limit reached. Lock cleared</color>" }
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"Title", "<color=orange>Друзья:</color>" },
                {"turretCleared", "<color=red>Превышен лимит, туррель очищена</color>" },
                {"cupboardCleared", "<color=red>Превышен лимит, шкаф очищен</color>" },
                {"codelockCleared", "<color=red>Превышен лимит, замок очищен</color>" }
            }, this, "ru");
        }
    }
}
