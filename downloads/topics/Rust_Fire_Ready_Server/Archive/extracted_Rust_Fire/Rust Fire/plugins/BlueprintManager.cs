using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Blueprint Manager", "Orange", "1.0.0")]
    public class BlueprintManager : RustPlugin
    {
        #region Vars

        private const string permUse = "blueprintmanager.all";
        private List<int> blueprints = new List<int>();

        #endregion

        #region Oxide Hooks

        private void OnServerInitialized()
        {
            permission.RegisterPermission(permUse, this);

            foreach (var item in ItemManager.itemList)
            {
                blueprints.Add(item.itemid);
            }
        }
        
        private void OnPlayerConnected(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permUse))
            {
                UnlockAll(player);
            }
        }

        #endregion

        #region Core

        private void UnlockAll(BasePlayer player)
        {
            var playerInfo = SingletonComponent<ServerMgr>.Instance.persistance.GetPlayerInfo(player.userID);
            playerInfo.unlockedItems = blueprints;
            SingletonComponent<ServerMgr>.Instance.persistance.SetPlayerInfo(player.userID, playerInfo);
            player.SendNetworkUpdate();
        }

        #endregion
    }
}