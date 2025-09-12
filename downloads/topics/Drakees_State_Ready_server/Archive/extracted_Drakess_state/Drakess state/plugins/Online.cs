namespace Oxide.Plugins
{
    [Info("Online", "Online", "1.0.2")]
    public class Online : RustPlugin
    {
		private void OnServerInitialized()
		{
			timer.Repeat(720f, 0, () => {
				ShowOnlineInChat();
			});
		}
		
		[ChatCommand("online")]
		private void ShowOnlineInChat(BasePlayer player = null)
		{
			var online = BasePlayer.activePlayerList.Count;
			var sleep = BasePlayer.sleepingPlayerList.Count;
			var connect = ServerMgr.Instance.connectionQueue.Joining;
			var queue = ServerMgr.Instance.connectionQueue.Queued;
			
			SendOnlineMsg(player, online, sleep, connect, queue);
		}
		
		[ChatCommand("online_test")]
		private void ShowOnlineTestInChat(BasePlayer player = null)
		{
			if (player.IsAdmin)			
				SendOnlineMsg(player, 123, 456, 324, 77);
		}
		
		private void SendOnlineMsg(BasePlayer player, int online, int sleep, int connect, int queue)
		{
			var msg = $"<size=15>Сейчас <color=#ffa35c>"+FormatString((uint)online, "игрок", "игрока", "игроков")+"</color> онлайн";
			if(connect != 0) {
				if(sleep == 0 && queue == 0)
					msg += " и ";
				else
					msg += ", ";
				msg += "<color=#ff801f>"+FormatString((uint)connect, "игрок", "игрока", "игроков")+"</color> подключаются";
			}
			if(sleep != 0) {
				if(queue == 0)
					msg += " и ";
				else
					msg += ", ";
				msg += "<color=#ff6e00>"+FormatString((uint)sleep, "игрок", "игрока", "игроков")+"</color> спят";
			}
			if(queue != 0 && online + connect > ConVar.Server.maxplayers - 1) {
				msg += " и ";
				msg += "<color=#cc5800>"+FormatString((uint)queue, "игрок", "игрока", "игроков")+"</color> в очереди";
			}
			msg += "</size>.";
			if(player != null)
				SendReply(player, msg);
			else
				Server.Broadcast(msg);
		}
		
		private static string FormatString(uint str, string first, string second, string third)
		{
			var formatted = str + " ";
			if (str > 100)
				str = str % 100;
			if (str > 9 && str < 21)
				formatted += third;
			else
				switch (str % 10)
				{
					case 1:
						formatted += first;
						break;
					case 2:
					case 3:
					case 4:
						formatted += second;
						break;
					default:
						formatted += third;
						break;
				}

			return formatted;
		}
    }
	
}