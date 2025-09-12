using System.Collections.Generic;
using VLB;
using System.Linq;
using UnityEngine;
using System;

namespace Oxide.Plugins
{
    [Info("HandcuffFix", "Mercury", "1.0.0")]
    [Description("HandcuffFix")]
    public class HandcuffFix : RustPlugin
    {
        private static HandcuffFix _;

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (!componentList.ContainsKey(player)) return;
            if (componentList[player] == null)
            {
                componentList.Remove(player);
                return;
            }
            componentList[player].Kill();
            componentList.Remove(player);
        }
        private void OnPlayerConnected(BasePlayer player)
        {
            HandcuffController controller = player.GetOrAddComponent<HandcuffController>();
		   		 		  						  	   		  	  			  	   		  	  			  				
            if (componentList.ContainsKey(player))
            {
                if (componentList[player] != null)
                    componentList[player].Kill();
            }
		   		 		  						  	   		  	  			  	   		  	  			  				
            componentList[player] = controller;
        }
        private Dictionary<BasePlayer, HandcuffController> componentList = new();
        
        private void Unload()
        {
            List<BasePlayer> pListInComponent = Facepunch.Pool.GetList<BasePlayer>();
            pListInComponent = componentList.Keys.ToList();

            foreach (BasePlayer player in pListInComponent)
                if (componentList[player] != null)
                    componentList[player].Kill();
            
            Facepunch.Pool.FreeList(ref pListInComponent);
            
            componentList.Clear();
            _ = null;
        }
        
        private void OnServerInitialized()
        {
            _ = this;
            foreach (BasePlayer player in BasePlayer.allPlayerList)
                OnPlayerConnected(player);
        }
        
        public class HandcuffController : MonoBehaviour
        {
            
            void Awake()
            {
                player = GetComponent<BasePlayer>();
                InvokeRepeating(nameof(HandcuffCheck), 1f, 1f);
            }
            
            private void UnlockedHandcuff()
            {
                if (!IsHandcuffLocked()) return;

                player.inventory.SetLockedByRestraint(false);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsRestrained, false);
            }
            
            public void Kill()
            {
                DestroyImmediate(this);
            }

            private void OnDestroy()
            {
                if (player != null && _.componentList != null)
                {
                    if (_.componentList.ContainsKey(player))
                        _.componentList[player] = null;
                }
            }
            private BasePlayer player;
            
            private Boolean IsHandcuffLocked()
            {
                Boolean isLockedBp = player.inventory.GetContainer(PlayerInventory.Type.BackpackContents) != null && player.inventory.GetContainer(PlayerInventory.Type.BackpackContents).IsLocked();
                return player.inventory.containerMain.IsLocked() || player.inventory.containerBelt.IsLocked() ||
                       player.inventory.containerWear.IsLocked() || isLockedBp || player.HasPlayerFlag(BasePlayer.PlayerFlags.IsRestrained);
            }
            private void HandcuffCheck()
            {
                if (!IsHandcuffLocked())
                {
                    UnlockedHandcuff();
                    return;
                }
            
                HeldEntity heldEntity = player.GetHeldEntity();
                if (heldEntity == null)
                {
                    UnlockedHandcuff();
                    return;
                }
            
                if (!heldEntity.ShortPrefabName.Contains("handcuff"))
                    UnlockedHandcuff();
            }
        }
    }
}
