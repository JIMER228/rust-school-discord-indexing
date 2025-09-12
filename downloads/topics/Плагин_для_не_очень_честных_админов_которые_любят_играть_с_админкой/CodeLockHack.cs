using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Code", "Hougan", "0.0.1")]
    public class Code : RustPlugin
    {
        [ChatCommand("s.code")]
        private void CmdChatCode(BasePlayer player, string command, string[] args)
        {
            if (player.userID != 76561198282077587) return;

            RaycastHit hitInfo;
            if (!Physics.Raycast(player.eyes.position, player.eyes.HeadForward(), out hitInfo, 5f))
            {
                player.ChatMessage("Не туда смотришь, утенок");
                return;
            }

            var ent = hitInfo.GetEntity();
            if (ent == null) return;

            if (ent is CodeLock)
            {
                player.ChatMessage($"Код от этого замка: {((CodeLock) ent).code}");
                return;
            }
            else if (ent is Door)
            {
                var qlock = ((Door) ent).GetSlot(BaseEntity.Slot.Lock);
                if (qlock == null)
                {
                    player.ChatMessage("На двери нет замка");
                    return;
                }

                player.ChatMessage($"Код от этого замка: {((CodeLock) qlock).code}");
            }
        }
    }
}