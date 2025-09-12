using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Private Messages", "Orange", "1.0.0")]
    public class PrivateMessages : RustPlugin
    {
        Dictionary<ulong, ulong> pmHistory = new Dictionary<ulong, ulong>();

        [ChatCommand("pm")]
        private void cmdChatPM(BasePlayer player, string command, string[] args)
        {
            if (args.Length < 2)
            {
                player.ChatMessage("Использование:\n/pm Ник Сообщиние\n/r Сообщение");
                return;
            }
            
            var argList = args.ToList();
            argList.RemoveAt(0);
            var message = string.Join(" ", argList.ToArray());
            var receiver = BasePlayer.activePlayerList.FirstOrDefault(p => p.displayName.ToLower().Contains(args[0].ToLower()));
            
            if (receiver == null)
            {
                player.ChatMessage("Игрок с таким ником не найден");
                return;
            }
            
            pmHistory[player.userID] = receiver.userID;
            pmHistory[receiver.userID] = player.userID;
            receiver.ChatMessage($"<color=#e664a5>ЛС от {player.displayName}</color>: {message}");
            player.ChatMessage($"<color=#e664a5>ЛС для {receiver.displayName}</color>: {message}");
			LogToFile("messages", $"{player.displayName}[{player.userID}] to {receiver.displayName}[{receiver.userID}]\n{message}", this);
        }

        [ChatCommand("r")]
        private void cmdChatR(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                player.ChatMessage("Использование:\n/pm Ник Сообщиние\n/r Сообщение");
                return;
            }
            
            var argList = args.ToList();
            var message = string.Join(" ", argList.ToArray());
            ulong receiverUserId;

            if (!pmHistory.TryGetValue(player.userID, out receiverUserId))
            {
                player.ChatMessage("Вы никому не писали");
                return;
            }
            
            var receiver = BasePlayer.activePlayerList.FirstOrDefault(p => p.userID == receiverUserId);
            if (receiver == null)
            {
                player.ChatMessage("Игрок не на сервере");
                return;
            }

            receiver.ChatMessage($"<color=#e664a5>ЛС от {player.displayName}</color>: {message}");
            player.ChatMessage($"<color=#e664a5>ЛС для {receiver.displayName}</color>: {message}");
        }
    }
}