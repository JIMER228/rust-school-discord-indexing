using System;
using System.Collections.Generic;
using System.Globalization;
using Oxide.Core;
using Oxide.Core.Plugins;

using UnityEngine;

namespace Oxide.Plugins
{
    [Info("RustPM", "vk.com/zaharkotov", "1.0.0")]
    class RustPM : RustPlugin
    {
        private readonly Dictionary<ulong, ulong> pmHistory = new Dictionary<ulong, ulong>();

        private void Init()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"PMTo", "<size=18>Личное сообщение для <color=#6495ED>{0}</color></size>\nСообщение: <color=#6495ED>{1}</color>"},
                {"PMFrom", "<size=18>Личное сообщение от <color=#6495ED>{0}</color></size>\nСообщение: <color=#6495ED>{1}</color>"},
                {"PLAYER.NOT.FOUND", "Игрок <color=#6495ED>{0}</color> не найден"},
                {"PM.NO.MESSAGES", "Вы не получали личных сообщений."},
                {"PM.PLAYER.LEAVE", "Игрок с которым вы переписывались вышел с сервера"},
                {"SelfPM", "Вы не можете отправить себе сообщение."},
                {"CMD.R.HELP", "Используйте: /r <Сообщение>"},
                {"CMD.PM.HELP", "Используйте: /pm <Никнейм> <Сообщение>"}
            }, this);
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (pmHistory.ContainsKey(player.userID)) pmHistory.Remove(player.userID);
        }

        [ChatCommand("pm")]
        void cmdPm(BasePlayer player, string command, string[] args)
        {
            if (args.Length < 2)
            {
                PrintMessage(player, "CMD.PM.HELP");
                Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player.GetNetworkPosition());
                return;
            }
            
            if (args.Length > 1)
            {
                var name = args[0];
                var target = FindPlayer(name);
                if (target == player)
                {
                    PrintMessage(player, "SelfPM");
                    Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player.GetNetworkPosition());
                    return;
                }

                if (target == null)
                {
                    PrintMessage(player, "PLAYER.NOT.FOUND", args[0]);
                    Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player.GetNetworkPosition());
                    return;
                }

                #region Hooks

                var msg = string.Empty;
                for (var i = 1; i < args.Length; i++)
                    msg = $"{msg} {args[i]}";                

                #endregion

                #region PmHistory

                pmHistory[player.userID] = target.userID;
                pmHistory[target.userID] = player.userID;

                #endregion
                
                #region Logs
                        
                Puts($"{player.displayName} Написал сообщение {target.displayName} Сообщение: {msg}");
                    
                #endregion

                #region PrintToChat

                PrintMessage(player, "PMTo", target.displayName, msg); // Сообщение для игрока
                PrintMessage(target, "PMFrom", player.displayName, msg); // Сообщение от игрока

                #endregion
            }
        }

        [ChatCommand("r")]
        void cmdPmReply(BasePlayer player, string command, string[] args)
        {
            if (args.Length < 1)
            {
                PrintMessage(player, "CMD.R.HELP");
                Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player.GetNetworkPosition());
                return;
            }
            
            ulong recieverUserId;
            if (!pmHistory.TryGetValue(player.userID, out recieverUserId))
            {
                PrintMessage(player, "PM.NO.MESSAGES");
                Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player.GetNetworkPosition());
                return;
            }
            
            if (args.Length > 0)
            {
                ulong steamid;
                if (pmHistory.TryGetValue(player.userID, out steamid))
                {
                    var target = FindPlayer(steamid);  
                    
                    if (target == player)
                    {
                        PrintMessage(player, "SelfPM");
                        Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player.GetNetworkPosition());
                        return;
                    }
                    
                    if (target == null)
                    {
                        PrintMessage(player, "PLAYER.NOT.FOUND");
                        Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player.GetNetworkPosition());
                        return;
                    }

                    var msg = string.Empty;
                    for (var i = 0; i < args.Length; i++)
                        msg = $"{msg} {args[i]}";
                    
                    #region Logs
                        
                    Puts($"{player.displayName} Написал сообщение {target.displayName} Сообщение: {msg}");
                    
                    #endregion

                    #region PrintToChat

                    PrintMessage(player, "PMTo", target.displayName, msg); // Сообщение для игрока
                    PrintMessage(target, "PMFrom", player.displayName, msg); // Сообщение от игрока

                    #endregion
                    
                }
            }
        }

        private void PrintMessage(BasePlayer player, string msgId, params object[] args)
        {
            PrintToChat(player, lang.GetMessage(msgId, this, player.UserIDString), args);
        }

        private static BasePlayer FindPlayer(string nameOrIdOrIp)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString == nameOrIdOrIp)
                    return activePlayer;
                if (activePlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.OrdinalIgnoreCase))
                    return activePlayer;
                if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress == nameOrIdOrIp)
                    return activePlayer;
            }
            return null;
        }

        private static BasePlayer FindPlayer(ulong id)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.userID == id)
                    return activePlayer;
            }
            return null;
        }
    }
}
