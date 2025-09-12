using Oxide.Core.Plugins;

///Скачано с дискорд сервера Rust Edit [PRO+]
///discord.gg/9vyTXsJyKR

namespace Oxide.Plugins
{
    [Info("Online", "discord.gg/9vyTXsJyKR", "1.0.2")]
    public class Online : RustPlugin
    {
		[ChatCommand("online")]
		private void ShowOnlineInChat(BasePlayer player)
		{
			SendReply(player, "Текущий онлайн на сервере:\n"+BasePlayer.activePlayerList.Count+" из "+ConVar.Server.maxplayers+".");
		}
    }
	
}