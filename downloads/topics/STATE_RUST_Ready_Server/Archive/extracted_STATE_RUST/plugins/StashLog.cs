using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("StashLog", "Nimant", "1.0.7", ResourceId = 0)]
    class StashLog : RustPlugin
    {            		
		
		[PluginReference("Friends")]
		private Plugin Friends;
		
		[PluginReference] 
		private Plugin VkCommandRouter;
				
		private FieldInfo lastToggleTime = typeof(StashContainer).GetField("lastToggleTime", BindingFlags.NonPublic | BindingFlags.Instance);				
				
		private void CanSeeStash(BasePlayer player, StashContainer stash)
		{
			if (player == null || stash == null || player.IsAdmin || Time.realtimeSinceStartup - (float)lastToggleTime.GetValue(stash) < 3f) return;
			
			if (stash.OwnerID != player.userID)
			{
				var target = BasePlayer.FindByID(stash.OwnerID);
				if (target == null) target = BasePlayer.FindSleeping(stash.OwnerID);
				if (target != null && target.currentTeam > 0 && target.currentTeam == player.currentTeam) return;						
				var isFriends = (Friends?.Call("AreFriends", stash.OwnerID, player.userID) as bool?) ?? false;
				if (isFriends) return;				
				var owner = stash.OwnerID > 0 ? $", владелец стеша {GetPlayerName(stash.OwnerID)} ({stash.OwnerID})" : "";
				var pos = stash.transform.position.ToString().Replace(",", "");
				var msg = $"🚧 Игрок {FixName(player.displayName)} ({player.userID}) откопал чужой смолстеш в координатах {pos}{owner}";
				PrintWarning(msg);				
				VkCommandRouter?.Call("API_SendInfoMsgVK", msg);
			}
		}
		
		private static string FixName(string name) => name.Replace("&","_").Replace("#","_").Replace("?","_");
		
		private string GetPlayerName(ulong userID)
		{
			var data = permission.GetUserData(userID.ToString());															
			return FixName(data.LastSeenNickname);
		}
		
    }	
	
}