namespace Oxide.Plugins
{
    [Info("StrawFix", "Ryamkk", "2.0.0")]
    public class StrawFix : RustPlugin
    {
        void OnServerInitialized()
        {
            permission.RegisterPermission("straw.fix", this);
        }
        
        private object OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum gradeEnum)
        {
            if (block.grade < gradeEnum) return null;

            player.Kick("Вы были забанены за использование читов");
            ModeratorLogs($"Игрок: {player.displayName} был заблокирован за использования читов!\nПРИЧИНА: DOWNGRADE HACK");
            ConsoleSystem.Run.Server.Normal(string.Format("player.ban {0}", player.userID));
            return true;
        }

        private void ModeratorLogs(string report)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (!permission.UserHasPermission(player.UserIDString, "straw.fix"))
                {
                    player.ChatMessage(report);
                }
            }
        }
    }
}