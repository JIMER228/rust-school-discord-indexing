using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("PM System", "https://topplugin.ru / https://discord.com/invite/5DPTsRmd3G", "1.0.3")]
    public class PMSystem : RustPlugin
    {
        #region Вар
        public Dictionary<ulong, ulong> pmHistory = new Dictionary<ulong, ulong>();
        #endregion

        #region Команды
        [ChatCommand("pm")]
        void ChatPM(BasePlayer player, string command, string[] args)
        {
            if (args == null || args.Length <= 1)
            {
                SendReply(player, "Используйте: /pm [ник игрока] [сообщение]");
                return;
            }

            var target = FindBasePlayer(args[0]);
            if (target == null)
            {
                SendReply(player, $"Игрок {args[0]} не найден");
                Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.forward);
                return;
            }

            if (target == player)
            {
                SendReply(player, "Вы не можете написать себе сообщение");
                Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.forward);
                return;
            }

            string message = "";
            for (int z = 1; z < args.Length; z++)
                    message += args[z] + " ";
            
            var text = message.Count() > 128 ? message.Remove(128) : message;

            pmHistory[player.userID] = target.userID;
            pmHistory[target.userID] = player.userID;

            SendReply(player, $"Сообщение для <color=#ee3e61>{target.displayName}</color>: {text}");
            SendReply(target, $"Сообщение от <color=#ee3e61>{player.displayName}</color>: {text}");
            Effect.server.Run("assets/bundled/prefabs/fx/notice/stack.world.fx.prefab", target, 0, Vector3.zero, Vector3.forward);
        }

        [ChatCommand("r")]
        void ChatR(BasePlayer player, string command, string[] args)
        {
            if (args == null || args.Length == 0)
            {
                SendReply(player, "Используйте: /r [сообщение]");
                return;
            }

            ulong recieverUserId;
            if (!pmHistory.TryGetValue(player.userID, out recieverUserId))
            {
                SendReply(player, "Вы не получали личных сообщений");
                Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.forward);
                return;
            }

            var target = BasePlayer.activePlayerList.FirstOrDefault(p => p.userID == recieverUserId);
            if (target == null)
            {
                SendReply(player, "Игрок покинул сервер, сообщение не отправлено!");
                return;
            }

            string message = "";
            for (int z = 0; z < args.Length; z++)
                message += args[z] + " ";

            var text = message.Count() > 128 ? message.Remove(128) : message;

            SendReply(player, $"Сообщение для <color=#ee3e61>{target.displayName}</color>: {text}");
            SendReply(target, $"Сообщение от <color=#ee3e61>{player.displayName}</color>: {text}");
            Effect.server.Run("assets/bundled/prefabs/fx/notice/stack.world.fx.prefab", target, 0, Vector3.zero, Vector3.forward);
        }
        #endregion

        #region Метод
        BasePlayer FindBasePlayer(string nameOrUserId)
        {
            nameOrUserId = nameOrUserId.ToLower();
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.displayName.ToLower().Contains(nameOrUserId) || player.UserIDString == nameOrUserId) return player;
            }
            return default(BasePlayer);
        }
        #endregion
    }
}