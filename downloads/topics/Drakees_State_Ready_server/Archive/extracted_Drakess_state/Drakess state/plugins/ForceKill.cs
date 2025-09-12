using Oxide.Core;
using Oxide.Core.Plugins;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ForceKill", "Nimant", "1.0.2")]    
    internal class ForceKill : RustPlugin
    {				
		
		private static ValidBounds KWInstance = null;
		
		private void OnServerInitialized() 
		{			
			KWInstance = SingletonComponent<ValidBounds>.Instance;
		}
		
		[ConsoleCommand("forcekill")]
        private void cmdForceKill(ConsoleSystem.Arg arg)
        {            
			var player = arg?.Player();			
			if (player == null) return;			
			
			if (player.IsDead()/*IsNearKillWall(player)*/)
			{				
				player.inventory?.Strip();			
				var spawnPoint = ServerMgr.FindSpawnPoint();												
				Teleport(player, spawnPoint.pos);
			}
		}
		
		private bool IsNearKillWall(BasePlayer player)
		{
			if (player == null || KWInstance == null) return false;
			
			if (Math.Abs(player.transform.position.x) + 10 > KWInstance.worldBounds.extents.x || 
			    Math.Abs(player.transform.position.y) + 10 > KWInstance.worldBounds.extents.y || 
			    Math.Abs(player.transform.position.z) + 10 > KWInstance.worldBounds.extents.z)
				return true;
			
			return false;
		}
		
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
            try { player.ClearEntityQueue(null); } catch {}
            player.SendFullSnapshot();
        }

        private void StartSleeping(BasePlayer player)
        {
            if (player.IsSleeping())
                return;
			object[] objArray = new object[] { player };			
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
            if (!BasePlayer.sleepingPlayerList.Contains(player))
                BasePlayer.sleepingPlayerList.Add(player);
            player.CancelInvoke("InventoryUpdate");            
        }
		
    }
}