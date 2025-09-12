using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ATeleportation", "Nimant", "1.0.0")]
    class ATeleportation : RustPlugin
    {                

		#region Variables
	
		private const string NewLine = "\n";
		private static float boundary;		
		
		#endregion
	
		#region Hooks
	
        private void Init()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
				{"PlayerNotFound", "Указанный игрок не найден!"},
                {"MultiplePlayers", "Найдено несколько игроков: {0}"},
				{"InvalidCoordinates", "Вы указали неверные координаты!"},			
                {"AdminTPOutOfBounds", "Вы попытались телепортироватся за пределы карты!"},			
                {"AdminTPBoundaries", "X и Z значения должны быть между -{0} и {0}, а Y должно быть между -100 и 2000!"},
                {"CantTeleportToSelf", "Вы не можете телепортироватся сами к себе!"},
                {"CantTeleportPlayerToSelf", "Вы не можете телепортировать игрока к самому себе!"},				

                {"AdminTP", "Вы телепортировались к игроку {0}!"},				
				{"AdminTPPlayers", "Вы телепортировали игрока {0} к игроку {1}!"},								                               
				{"AdminTPCoordinates", "Вы телепортировались в точку {0}!"},
				{"AdminTPTargetCoordinates", "Вы телепортировали игрока {0} в точку {1}!"},
				
                {"AdminTPPlayer", "Администратор телепортировал вас к игроку {1}!"},				
                {"AdminTPPlayerTarget", "Администратор телепортировал игрока {1} к вам!"},                
				{"AdminTPTargetCoordinatesTarget", "Администратор телепортировал вас в точку {1}!"},
                								
				{"LogTeleport", "Игрок {0} телепортировался к игроку или в точку {1}."},
                {"LogTeleportPlayer", "Игрок {0} телепортировал игрока {1} к игроку или в точку {2}."},
				
				{
                    "SyntaxCommandTP", string.Join(NewLine, new[]
                    {                        
                        "Использование:",
                        "/tp \"targetplayer\" - телепортировать себя к игроку",
                        "/tp \"player\" \"targetplayer\" - телепортировать игрока к игроку",
                        "/tp x y z - телепортироватся в заданные координаты",
                        "/tp \"player\" x y z - телепортировать игрока в заданные координаты"
                    })
                }				
            }, this);            			
        }        

        private void OnServerInitialized() => boundary = TerrainMeta.Size.x / 2;
		
		#endregion

		#region Commands
		
        [ChatCommand("tp")]
        private void cmdChatTeleport(BasePlayer player, string command, string[] args)
        {
			if (!player.IsAdmin) return;
			            
            BasePlayer target;
            float x, y, z;
            switch (args.Length)
            {
                case 1:
                    target = FindPlayersSingle(args[0], player);
                    if (target == null) return;
                    if (target == player)
                    {
                        PrintMsgL(player, "CantTeleportToSelf");
                        return;
                    }
                    TeleportToPlayer(player, target);
                    PrintMsgL(player, "AdminTP", target.displayName);
                    Puts(_("LogTeleport", null, player.displayName, target.displayName));                    
                    break;
                case 2:
                    var origin = FindPlayersSingle(args[0], player);
                    if (origin == null) return;
                    target = FindPlayersSingle(args[1], player);
                    if (target == null) return;
                    if (target == origin)
                    {
                        PrintMsgL(player, "CantTeleportPlayerToSelf");
                        return;
                    }
                    TeleportToPlayer(origin, target);
                    PrintMsgL(player, "AdminTPPlayers", origin.displayName, target.displayName);
                    PrintMsgL(origin, "AdminTPPlayer", player.displayName, target.displayName);
                    PrintMsgL(target, "AdminTPPlayerTarget", player.displayName, origin.displayName);
                    Puts(_("LogTeleportPlayer", null, player.displayName, origin.displayName, target.displayName));
                    break;
                case 3:
                    if (!float.TryParse(args[0], out x) || !float.TryParse(args[1], out y) || !float.TryParse(args[2], out z))
                    {
                        PrintMsgL(player, "InvalidCoordinates");
                        return;
                    }
                    if (!CheckBoundaries(x, y, z))
                    {
                        PrintMsgL(player, "AdminTPOutOfBounds");
                        PrintMsgL(player, "AdminTPBoundaries", boundary);
                        return;
                    }
                    TeleportToPosition(player, x, y, z);
                    PrintMsgL(player, "AdminTPCoordinates", new Vector3(x, y, z));
                    Puts(_("LogTeleport", null, player.displayName, new Vector3(x, y, z)));
                    break;
                case 4:
                    target = FindPlayersSingle(args[0], player);
                    if (target == null) return;
                    if (!float.TryParse(args[0], out x) || !float.TryParse(args[1], out y) || !float.TryParse(args[2], out z))
                    {
                        PrintMsgL(player, "InvalidCoordinates");
                        return;
                    }
                    if (!CheckBoundaries(x, y, z))
                    {
                        PrintMsgL(player, "AdminTPOutOfBounds");
                        PrintMsgL(player, "AdminTPBoundaries", boundary);
                        return;
                    }
                    TeleportToPosition(target, x, y, z);
                    if (player == target)
                    {
                        PrintMsgL(player, "AdminTPCoordinates", new Vector3(x, y, z));
                        Puts(_("LogTeleport", null, player.displayName, new Vector3(x, y, z)));
                    }
                    else
                    {
                        PrintMsgL(player, "AdminTPTargetCoordinates", target.displayName, new Vector3(x, y, z));
                        PrintMsgL(target, "AdminTPTargetCoordinatesTarget", player.displayName, new Vector3(x, y, z));
                        Puts(_("LogTeleportPlayer", null, player.displayName, target.displayName, new Vector3(x, y, z)));
                    }
                    break;
                default:
                    PrintMsgL(player, "SyntaxCommandTP");
                    break;
            }
        }		        

		#endregion
		
        #region Teleport

        public void TeleportToPlayer(BasePlayer player, BasePlayer target) => Teleport(player, target.transform.position);

        public void TeleportToPosition(BasePlayer player, float x, float y, float z) => Teleport(player, new Vector3(x, y, z));

        public void Teleport(BasePlayer player, Vector3 position)
        {                        
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "StartLoading");
            StartSleeping(player);
            player.MovePosition(position);
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "ForcePositionTo", position);
            if (player.net?.connection != null)
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.UpdateNetworkGroup();
            //player.UpdatePlayerCollider(true, false);
            player.SendNetworkUpdateImmediate(false);
            if (player.net?.connection == null) return;
            //TODO temporary for potential rust bug
            try
            {
                player.ClearEntityQueue(null);
            }
            catch
            {
            }
            player.SendFullSnapshot();
        }

        private void StartSleeping(BasePlayer player)
        {
            if (player.IsSleeping())
                return;
			object[] objArray = new object[] { player };
			Interface.CallHook("OnPlayerTeleport", objArray);				
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
            if (!BasePlayer.sleepingPlayerList.Contains(player))
                BasePlayer.sleepingPlayerList.Add(player);
            player.CancelInvoke("InventoryUpdate");            
        }

        #endregion

        #region Checks                                

        private static bool CheckBoundaries(float x, float y, float z)
        {
            return x <= boundary && x >= -boundary && y < 2000 && y >= -100 && z <= boundary && z >= -boundary;
        }                                     

        #endregion

        #region Message

        private string _(string msgId, BasePlayer player, params object[] args)
        {
            var msg = lang.GetMessage(msgId, this, player?.UserIDString);
            return args.Length > 0 ? string.Format(msg, args) : msg;
        }

        private void PrintMsgL(BasePlayer player, string msgId, params object[] args)
        {
            if (player == null) return;			
			SendReply(player, _(msgId, player, args));
        }        

        #endregion        

        #region FindPlayer        

        private BasePlayer FindPlayersSingle(string nameOrIdOrIp, BasePlayer player)
        {
            var targets = FindPlayers(nameOrIdOrIp);
            if (targets.Count <= 0)
            {
                PrintMsgL(player, "PlayerNotFound");
                return null;
            }
            if (targets.Count > 1)
            {
                PrintMsgL(player, "MultiplePlayers", string.Join(", ", targets.Select(p => p.displayName).ToArray()));
                return null;
            }
            return targets.First();
        }

        private static HashSet<BasePlayer> FindPlayers(string nameOrIdOrIp)
        {
            var players = new HashSet<BasePlayer>();
            if (string.IsNullOrEmpty(nameOrIdOrIp)) return players;
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString.Equals(nameOrIdOrIp))
                    players.Add(activePlayer);
                else if (!string.IsNullOrEmpty(activePlayer.displayName) && activePlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.IgnoreCase))
                    players.Add(activePlayer);
                else if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress.Equals(nameOrIdOrIp))
                    players.Add(activePlayer);
            }
            foreach (var sleepingPlayer in BasePlayer.sleepingPlayerList)
            {
                if (sleepingPlayer.UserIDString.Equals(nameOrIdOrIp))
                    players.Add(sleepingPlayer);
                else if (!string.IsNullOrEmpty(sleepingPlayer.displayName) && sleepingPlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.IgnoreCase))
                    players.Add(sleepingPlayer);
            }
            return players;
        }       

        #endregion
        
    }
}
