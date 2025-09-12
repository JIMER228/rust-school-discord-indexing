using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using ProtoBuf;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Friends Compability API", "Orange", "2.2.5")]
    [Description("To make UniTeam work as Friends API")]
    public class Friends : RustPlugin
    {
        #region Utils

        private static BasePlayer FindPlayer(ulong id)
        {
            return BasePlayer.FindByID(id) ?? BasePlayer.FindSleeping(id);
        }

        #endregion
        
        #region Compability API

        private bool HasFriend(ulong playerId, ulong friendId)
        {
            var player1 = FindPlayer(playerId);
            if (player1 != null)
            {
                if (player1.currentTeam == 0ul)
                {
                    return false;
                }
                
                var team = RelationshipManager.Instance.FindTeam(player1.currentTeam);
                return team != null && team.members.Contains(friendId);
            }
            
            var player2 = FindPlayer(friendId);
            if (player2 != null)
            {
                if (player2.currentTeam == 0ul)
                {
                    return false;
                }
                
                var team = RelationshipManager.Instance.FindTeam(player2.currentTeam);
                return team != null && team.members.Contains(playerId);
            }

            return false;
        }
        
        private ulong[] GetFriends(ulong playerId)
        {
            var player = FindPlayer(playerId);
            if (player != null)
            {
                if (player.currentTeam == 0ul)
                {
                    return new ulong[]{};
                }
                
                var team = RelationshipManager.Instance.FindTeam(player.currentTeam);
                if (team == null)
                {
                    return new ulong[]{};
                }

                return team.members.Where(x => x  != playerId).ToArray();
            }
            
            return new ulong[]{};
        }
        
        private bool HasFriendS(string playerId, string friendId)
        {
            return HasFriend(Convert.ToUInt64(playerId), Convert.ToUInt64(friendId));
        }
        
        private bool AreFriends(ulong playerId, ulong friendId)
        {
            return HasFriend(playerId, friendId);
        }
        
        private bool AreFriendsS(string playerId, string friendId)
        {
            return HasFriendS(playerId, friendId);
        }
        
        private bool IsFriend(ulong playerId, ulong friendId)
        {
            return HasFriend(friendId, playerId);
        }
        
        private bool IsFriendS(string playerId, string friendId)
        {
            return HasFriendS(friendId, playerId);
        }
        
        private string[] GetFriends(string playerId)
        {
            return GetFriends(Convert.ToUInt64(playerId)).Select(x => x.ToString()).ToArray();
        }
        
        private string[] GetFriendList(ulong playerId)
        {
            return GetFriends(playerId.ToString());
        }

        private string[] GetFriendListS(string playerId)
        {
            return GetFriends(playerId);
        }

        private ulong[] IsFriendOf(ulong playerId)
        {
            return GetFriends(playerId);
        }

        private string[] IsFriendOfS(string playerId)
        {
            return GetFriends(playerId);
        }

        #endregion
    }
}