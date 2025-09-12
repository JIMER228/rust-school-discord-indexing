using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Oxide.Core;
using Oxide.Game.Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PM System", "https://topplugin.ru/", "1.0.41")]
    public class PMSystem : RustPlugin
    {
        #region Variables
        
        private Dictionary<ulong, PlayerData> PMHistory = new Dictionary<ulong, PlayerData>();
        private readonly Dictionary<ulong, BasePlayer> PMSelf = new Dictionary<ulong, BasePlayer>();

        class PlayerData
        {
            public readonly Dictionary<ulong, ulong> pmHistory = new Dictionary<ulong, ulong>();
            public string Name { get; set; } = string.Empty;
            public ulong Target;
            public List<ulong> BlackList { get; set; } = new List<ulong>();
        }
        
        private PlayerData GetPlayerData(ulong playerId)
        {
            var player = RustCore.FindPlayerById(playerId);
            PlayerData playerData;
            if (!PMHistory.TryGetValue(playerId, out playerData))
                PMHistory[playerId] = playerData = new PlayerData();
            if (player != null) playerData.Name = player.displayName;
            return playerData;
        }
        
        private string[] GetFriendList(ulong playerId)
        {
            var playerData = GetPlayerData(playerId);
            var players = new List<string>();
            foreach (var friend in playerData.BlackList)
                players.Add(GetPlayerData(friend).Name);
            return players.ToArray();
        }

        #endregion
 
        #region Helpers

        #region FindPlayer

        private static BasePlayer FindPlayer(string nameOrIdOrIp)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString == nameOrIdOrIp)
                    return activePlayer;
                if (activePlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.OrdinalIgnoreCase))
                    return activePlayer;;
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
        
        public void SendMSG(BasePlayer player, string message, ulong senderID = 0, string args = null)
        {
            bool arg = args != null;
            if (arg)
            {
                message = string.Format(message, args);
            }
            player.SendConsoleCommand("chat.add 0", new object[]
            {
                senderID,
                string.Format("{0}", message)
            });
        }

        #endregion

        #endregion

        #region Hooks
        private void Init()
        {
            try
            {
                PMHistory = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>(nameof(PMSystem));
            }
            catch
            {
                PMHistory = new Dictionary<ulong, PlayerData>();
            }
			
            //	Сохраняем
            saveDataImmediate();
        }
        Timer saveDataBatchedTimer = null;

        void saveDataImmediate()
        {
            if (saveDataBatchedTimer != null)
            {
                saveDataBatchedTimer.DestroyToPool();
                saveDataBatchedTimer = null;
            }
            Interface.Oxide.DataFileSystem.WriteObject("PMSystem", PMHistory);
        }
		
        void Unload()
        {
            saveDataImmediate();
        }
		
        void OnServerSave()
        {
            saveDataImmediate();
        }
		
        void OnServerShutdown()
        {
            saveDataImmediate();
        }
        #endregion

        #region Localization
        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);
        void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"cmdpmhelp", "Используйте:\n" +
                              "/pm [NICKNAME] [MESSAGE]\n" +
                              "/pm ignore add|remove|list"},
                {"cmdrhelp", "Используйте /r [MESSAGE]"},
                {"PM.IGNORE", "Используйте: /pm ignore add | remove | list"},
                {"PM.IGNORE.ADD", "Используйте: /pm ignore add [NICKNAME]"},
                {"PM.IGNORE.REMOVE", "Используйте: /pm ignore remove [NICKNAME]"},
                {"SelfTo", "Вы не можете написать себе сообщение"},
                {"NoFoundPlayer", "Данный игрок не найден"},
                {"PM.NO.MESSAGES", "Вы не получали личных сообщений."},
                {"AddIgnoreList", "Вы успешно добавили в черный список игрока {0}"},
                {"RemoveIgnoreList", "Вы успешно удалили из черного списка игрока {0}"},
                {"NoBL", "Ваш черный список пуст"},
                {"PMTo", "ЛС для <color=#55aaff>{0}</color>: {1}"},
                {"PMFrom", "ЛС от <color=#55aaff>{0}</color>: {1}"},
                
            }, this);
        }
        
        private void OnServerInitialized()
        {
            LoadMessages();
            LoadConfig();
        }

        #endregion

        #region Commands

        [ChatCommand("pm")]
        private void cmdpmchat(BasePlayer player, string command, string[] args)
        {
            if (args == null || args.Length == 0)
            {
                player.ChatMessage(GetMsg("cmdpmhelp"));
                return;
            }
            if (args.Length >= 1)
            {
                switch (args[0])
                {
                    case "ignore":
                    {
                        if (args.Length < 2)
                        {
                            player.ChatMessage(GetMsg("PM.IGNORE"));
                            return;
                        }
                        switch (args[1])
                        {
                            case "add":
                            {
                                if (args.Length < 3)
                                {
                                    player.ChatMessage(GetMsg("PM.IGNORE.ADD"));
                                    return;
                                }
                                var name1 = args[2];
                                var target1 = FindPlayer(name1);
                                if (target1 == null)
                                {
                                    SendReply(player, GetMsg("NoFoundPlayer"), args[0]);
                                    Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.forward);
                                    return;
                                }
                                if (PMHistory[player.userID].BlackList.Contains(target1.userID))
                                {
                                    SendReply(player, "Игрок уже в черном списке");
                                }
                                else
                                {
                                    GetPlayerData(player.userID).BlackList.Add(target1.userID);
                                    SendReply(player, "{0} успешно добавлен в черный список".Replace("{0}", target1.displayName));
                                }
                                break;
                            }
                            case "remove":
                            {
                                if (args.Length < 3)
                                {
                                    player.ChatMessage(GetMsg("PM.IGNORE.REMOVE"));
                                    return;
                                }
                                var name1 = args[2];
                                var target1 = FindPlayer(name1);
                                if (target1 == null)
                                {
                                    SendReply(player, GetMsg("NoFoundPlayer"), args[0]);
                                    Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.forward);
                                    return;
                                }
                                if (!PMHistory[player.userID].BlackList.Contains(target1.userID))
                                {
                                    SendReply(player, GetMsg("NoFoundPlayer"));
                                }
                                else
                                {
                                    PMHistory[player.userID].BlackList.Remove(target1.userID);
                                    SendReply(player, "{0} успешно удален из черного списка".Replace("{0}", target1.displayName));
                                }
                                break;
                            }
                            case "list":
                            {
                                var bl = GetFriendList(player.userID);
                                if (bl.Length > 0)
                                    SendReply(player, "Черный список:\n{1}".Replace("{1}", string.Join("\n", bl)));
                                else
                                    SendReply(player, "Черный список пуст");
                               
                            }
                            break;
                        }
                    }
                    return;
                }
                var name = args[0];
                var target = FindPlayer(name);
                
                if (target == player)
                {
                    SendReply(player, GetMsg("SelfTo"));
                    Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.forward);
                    return;
                }

                if (target == null)
                {
                    SendReply(player, GetMsg("NoFoundPlayer"), args[0]);
                    Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.forward);
                    return;
                }
                
                if (GetPlayerData(target.userID).BlackList.Contains(player.userID))
                {
                    SendReply(player, "Вы находитес" +
                                      "ь у {0} в черном списке".Replace("{0}", target.displayName));
                    return;
                }
                if (GetPlayerData(player.userID).BlackList.Contains(target.userID))
                {
                    SendReply(player, "{0} находится в черном списке".Replace("{0}", target.displayName));
                    return;
                }

                #region Hooks

                var msg = string.Empty;
                for (var i = 1; i < args.Length; i++)
                    msg = $"{msg} {args[i]}";                

                #endregion

                #region PmHistory

                PMHistory[player.userID].pmHistory[player.userID] = target.userID;
                PMHistory[target.userID].pmHistory[target.userID] = player.userID;

                #endregion
                
                #region Logs
                        
                Puts($"{player.displayName} Написал сообщение {target.displayName} Сообщение: {msg}");
                LogToFile("PM", $"[{DateTime.Now.ToShortTimeString()}] {player.displayName} написал {target.displayName}: Сообщение: {msg}", this, true); 
                    
                #endregion

                #region PrintToChat

                SendMSG(player, GetMsg("PMTo").Replace("{0}", target.displayName).Replace("{1}", msg), target.userID); // Сообщение для игрока
                player.ConsoleMessage(GetMsg("PMTo").Replace("{0}", target.displayName).Replace("{1}", msg));
                target.ConsoleMessage(GetMsg("PMFrom").Replace("{0}", player.displayName).Replace("{1}", msg));
                SendMSG(target, GetMsg("PMFrom").Replace("{0}", player.displayName).Replace("{1}", msg), player.userID); // Сообщение от игрока
                
                Effect.server.Run("assets/bundled/prefabs/fx/notice/stack.world.fx.prefab", target, 0, Vector3.zero, Vector3.forward);

                #endregion

            }
        }
        

        [ChatCommand("r")]
        void cmdPmReply(BasePlayer player, string command, string[] args)
        {
            if (args.Length < 1)
            {
                SendReply(player, GetMsg("CMD.R.HELP"));
                Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.forward);
                return;
            }
            
            ulong recieverUserId;
            var pmHistory = PMHistory[player.userID].pmHistory;
            if (!pmHistory.TryGetValue(player.userID, out recieverUserId))
            {
                SendReply(player, GetMsg("PM.NO.MESSAGES"));
                Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.forward);
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
                        SendReply(player, GetMsg("SelfPM"));
                        Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.forward);
                        return;
                    }
                    
                    if (target == null)
                    {
                        SendReply(player, GetMsg("PLAYER.NOT.FOUND"));
                        Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player, 0, Vector3.zero, Vector3.forward);
                        return;
                    }

                    var msg = string.Empty;
                    for (var i = 0; i < args.Length; i++)
                        msg = $"{msg} {args[i]}";
                    
                    #region Logs
                        
                    Puts($"{player.displayName} Написал сообщение {target.displayName} Сообщение: {msg}");
                    LogToFile("PM", $"[{DateTime.Now.ToShortTimeString()}] {player.displayName} написал {target.displayName}: Сообщение: {msg}", this, true); 
                    
                    #endregion

                    #region PrintToChat
                    SendMSG(player, GetMsg("PMTo").Replace("{0}", target.displayName).Replace("{1}", msg), target.userID); // Сообщение для игрока
                    player.ConsoleMessage(GetMsg("PMTo").Replace("{0}", target.displayName).Replace("{1}", msg));
                    target.ConsoleMessage(GetMsg("PMFrom").Replace("{0}", player.displayName).Replace("{1}", msg));
                    SendMSG(target, GetMsg("PMFrom").Replace("{0}", player.displayName).Replace("{1}", msg), player.userID); // Сообщение от игрока
                    Effect.server.Run("assets/bundled/prefabs/fx/notice/stack.world.fx.prefab", target, 0, Vector3.zero, Vector3.forward);

                    #endregion
                    
                }
            }
        }

        #endregion
    }
}