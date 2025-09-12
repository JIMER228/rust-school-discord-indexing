using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Online", "https://discord.gg/9vyTXsJyKR", "1.0.0")]
    public class Online : RustPlugin
    {
		[ChatCommand("online")]
		private void ShowOnlineInChat(BasePlayer player)
		{
			SendReply(player, "Текущий онлайн на сервере:\n"+BasePlayer.activePlayerList.Count+" из "+ConVar.Server.maxplayers+".");
		}
    }
	
}