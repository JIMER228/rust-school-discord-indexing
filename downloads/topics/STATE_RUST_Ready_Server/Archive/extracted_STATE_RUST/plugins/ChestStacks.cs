using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Facepunch;

namespace Oxide.Plugins
{
	/* Based on version 1.2.1 by Jake_Rich */
    [Info("ChestStacks", "Nimant", "1.0.2")]
    public class ChestStacks : RustPlugin
    {        				        
	
		#region Variables
		
		private static ItemContainer TargetContainer;
		
		#endregion
	
        #region Hooks
		
		private void Init() => LoadVariables();		             

        private object OnMaxStackable(Item item)
        {   						
            if (item.info.itemType == ItemContainer.ContentsType.Liquid)            
                return null;
            
            if (item.info.stackable == 1)            
                return null;
            
            if (TargetContainer != null)
            {
                var entity = TargetContainer.entityOwner ?? TargetContainer.playerOwner;
                if (entity != null)
                {
                    int stacksize = Mathf.FloorToInt(GetStackSize(entity) * item.info.stackable);
                    TargetContainer = null;
                    return stacksize;
                }
            }
			
            if (item?.parent?.entityOwner != null)
            {
                int stacksize = Mathf.FloorToInt(GetStackSize(item.parent.entityOwner) * item.info.stackable);
                return stacksize;
            }
			
            return null;
        }        

        private object CanMoveItem(Item movedItem, PlayerInventory playerInventory, uint targetContainerID, int targetSlot, int amount)
        {							
			if (movedItem == null || playerInventory == null) 
				return null;			            			
			            
            var player = playerInventory.containerMain.playerOwner;			
			if (player == null) return null;
			
			var container = playerInventory.FindContainer(targetContainerID);
			
            var lootContainer = playerInventory.loot?.FindContainer(targetContainerID);			
			
            TargetContainer = container;								            

            // Right click overstacks into player inventory				
            if (targetSlot == -1)  
            {               				
                if (lootContainer == null) 
                {					
                    if (movedItem.amount > movedItem.info.stackable)
                    {                        
						if (container != null) return null;
				
						var itemToMove = movedItem.SplitItem(movedItem.info.stackable);
						bool moved = playerInventory.GiveItem(itemToMove);																														
						
						if (moved == false)
						{							
							movedItem.amount += itemToMove.amount;								
							itemToMove.Remove();
							playerInventory.ServerUpdate(0f);
							return false;
						}
						
						if (movedItem != null)						
							movedItem.MarkDirty();						                        						
						
                        playerInventory.ServerUpdate(0f);
                        return false;
                    }					
                }                
            }						            

            // Moving Overstacks Around In Chest					
            if (amount > movedItem.info.stackable && lootContainer != null)
            {				
                var targetItem = container.GetSlot(targetSlot);
                if (targetItem == null)
                {										
                    //Split item into chest
                    if (amount < movedItem.amount)                    
                        ItemHelper.SplitMoveItem(movedItem, amount, container, targetSlot);                    
					//Moving items when amount > info.stacksize
                    else                                            
                        movedItem.MoveToContainer(container, targetSlot);                    
                }
                else
                {
					//Swapping positions of items
                    if (!targetItem.CanStack(movedItem) && amount == movedItem.amount)                                            
                        ItemHelper.SwapItems(movedItem, targetItem);                    
                    else
                    {
                        if (amount < movedItem.amount)                        
                            ItemHelper.SplitMoveItem(movedItem, amount, playerInventory);                        
                        else                        
                            movedItem.MoveToContainer(container, targetSlot);                        
                        //Stacking items when amount > info.stacksize
                    }
                }
				
                playerInventory.ServerUpdate(0f);
                return false;
            }						            

            // Prevent Moving Overstacks To Inventory  						
            if (lootContainer != null)
            {				
                var targetItem = container.GetSlot(targetSlot);
                if (targetItem != null && movedItem.parent != null)
                {
                    if (movedItem.parent.playerOwner == player)
                    {
                        if (!movedItem.CanStack(targetItem))
                        {
                            if (targetItem.amount > targetItem.info.stackable)                            
                                return false;                            
                        }						
                    }
                }
            }            	
			
            return null;
        }
        
        // Hook not implmented, using OnItemDropped for now
        private object OnDropItem(PlayerInventory inventory, Item item, int amount)
        {
            return null;
			
            var player = inventory.GetComponent<BasePlayer>();
            if (inventory.loot.entitySource == null)            
                return null;
            
            if (item.amount > item.info.stackable)
            {
                int loops = Mathf.CeilToInt((float)item.amount / item.info.stackable);
                for (int i = 0; i < loops; i++)
                {
                    if (item.amount <= item.info.stackable)
                    {
                        item.Drop(player.eyes.position, player.eyes.BodyForward() * 4f + Vector3Ex.Range(-1f, 1f));
                        break;
                    }
					
                    var splitItem = item.SplitItem(item.info.stackable);
					
                    if (splitItem != null)                    
                        splitItem.Drop(player.eyes.position, player.eyes.BodyForward() * 4f + Vector3Ex.Range(-1f, 1f));                    
                }
				
                player.SignalBroadcast(BaseEntity.Signal.Gesture, "drop_item", null);
                return false;
            }
			
            return null;
        }

        // Covers dropping overstacks from chests onto the ground
        private void OnItemDropped(Item item, BaseEntity entity)
        {
            item.RemoveFromContainer();
            int stackSize = item.MaxStackable();
			
            if (item.amount > stackSize)
            {
                int loops = Mathf.FloorToInt((float)item.amount / stackSize);
                if (loops > 20) return;
                
                for (int i = 0; i < loops; i++)
                {
                    if (item.amount <= stackSize) break;
                    
                    var splitItem = item.SplitItem(stackSize);
                    if (splitItem != null)                    
                        splitItem.Drop(entity.transform.position, entity.GetComponent<Rigidbody>().velocity + Vector3Ex.Range(-1f, 1f));                    
                }
            }
        }

        #endregion

        #region Plugin API

        [HookMethod("GetChestSize")]
        object GetChestSize_PluginAPI(BaseEntity entity)
        {
            if (entity == null) return 1f;            
            return GetStackSize(entity);
        }

        #endregion           
		
		#region Helpers
		
		public class ItemHelper
        {
            public static bool SplitMoveItem(Item item, int amount, ItemContainer targetContainer, int targetSlot)
            {
                var splitItem = item.SplitItem(amount);
                if (splitItem == null)                
                    return false;
                
                if (!splitItem.MoveToContainer(targetContainer, targetSlot))
                {
                    item.amount += splitItem.amount;					
                    splitItem.Remove();
                }
				
                return true;
            }

            public static bool SplitMoveItem(Item item, int amount, BasePlayer player) => SplitMoveItem(item, amount, player.inventory);            

            public static bool SplitMoveItem(Item item, int amount, PlayerInventory inventory)
            {
				amount = amount > item.info.stackable ? item.info.stackable : amount;
                var splitItem = item.SplitItem(amount);
                
				if (splitItem == null) return false;
                                
				if (!splitItem.MoveToContainer(inventory.containerMain, -1, false))
				{
					if (!splitItem.MoveToContainer(inventory.containerBelt, -1, false))
					{						
						item.amount += splitItem.amount;					
						splitItem.Remove();
					}				
				}
					
                return true;
            }

            public static void SwapItems(Item item1, Item item2)
            {
                var container1 = item1.parent;
                var container2 = item2.parent;
                var slot1 = item1.position;
                var slot2 = item2.position;
                item1.RemoveFromContainer();
                item2.RemoveFromContainer();
                item1.MoveToContainer(container2, slot2);
                item2.MoveToContainer(container1, slot1);
            }
        }    
		
		public float GetStackSize(BaseEntity entity)
		{
			if (entity is LootContainer || entity is BaseCorpse || entity is BasePlayer)			
				return 1f;						
			
			if (configData.StackConfig.ContainsKey(entity.ShortPrefabName))
				return configData.StackConfig[entity.ShortPrefabName] >= 1f ? configData.StackConfig[entity.ShortPrefabName] : 1f;
			
			configData.StackConfig.Add(entity.ShortPrefabName, 1f);
			timer.Once(0.1f, ()=> SaveConfig(configData));
			PrintWarning($"В конфигурационный файл был добавлен новый контейнер '{entity.ShortPrefabName}', измените его рейт стака при необходимости.");
			
			return 1f;
		}
		
		private static Dictionary<string, float> GetDefaultContainers()
		{
			return new Dictionary<string, float>()
            {
                { "cupboard.tool.deployed", 1f }, 	// Tool Cupboard
                { "campfire", 1f }, 				// Fireplace
                { "lantern.deployed", 1f }, 		// Lantern
                { "box.wooden.large", 1f }, 		// Large wooden box
                { "small_stash_deployed", 1f }, 	// Stash
                { "dropbox.deployed", 1f }, 		// Drop box
                { "woodbox_deployed", 1f }, 		// Small wooden box
                { "vendingmachine.deployed", 1f }, 	// Vending Machine
                { "bbq.deployed", 1f }, 			// BBQ
                { "furnace.large", 1f }, 			// Large furnace
                { "skull_fire_pit", 1f }, 			// Skull fireplace
                { "mailbox.deployed", 1f }, 		// Mailbox
                { "furnace", 1f }, 					// Small furnace
                { "hopperoutput", 1f }, 			// Quarry output
                { "fuelstorage", 1f }, 				// Pumpjack and quarry fuel storage
                { "crudeoutput", 1f }, 				// Pumpjack output
                { "refinery_small_deployed", 1f }, 	// Oil Refinery
                { "fireplace.deployed", 1f }, 		// New Large Fireplace
                { "foodbox", 1f }, 					// Small food pile
                { "trash-pile-1", 1f}, 				// Other food pile
                { "supply_drop", 1f }, 				// Supply Drop 
                { "recycler_static", 1f }, 			// Recycler
                { "water_catcher_small", 1f }, 		// Small Water Catcher
                { "water_catcher_large", 1f }, 		// Large Water Catcher
                { "small_refinery_static", 1f }, 	// Refineries in radtowns
                { "player_corpse", 1f }, 			// Corpses
                { "item_drop_backpack", 1f }, 		// Backpacks
                { "fridge.deployed", 1f }, 			// Fridge
                { "survivalfishtrap.deployed", 1f },// Fridge
                { "hobobarrel_static", 1f }, 		// Hobo barrels!
                { "item_drop", 1f }, 				// Dropped container when chests are broken
                { "workbench1.deployed", 1f }, 		// Workbench (scrap)
                { "workbench2.deployed", 1f }, 		// Workbench (scrap)
                { "workbench3.deployed", 1f }, 		// Workbench (scrap)
                { "tunalight.deployed", 1f }, 		// Tunacan lamp (fuel)
                { "researchtable_deployed", 1f }, 	// Research table (Scrap)
                { "guntrap.deployed", 1f }, 		// Shotgun trap
                { "scientist_corpse", 1f }, 		// Corpse scientist                
                { "murderer_corpse", 1f }, 			// Corpse for murderer
                { "searchlight.deployed", 1f }, 	// Search light
                { "rowboat_storage", 1f }, 			// Rowboat storage
                { "fuel_storage", 1f } 				// Boat storage
			};
		}		
		
		#endregion
		
		#region Config        						
		
        private static ConfigData configData;
		
        private class ConfigData
        {            
			[JsonProperty(PropertyName = "Контейнера и их рейт стака")]
			public Dictionary<string, float> StackConfig = new Dictionary<string, float>();
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                StackConfig = GetDefaultContainers()
            };
            SaveConfig(configData);
			timer.Once(0.1f, ()=> SaveConfig(configData));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
        
    }
}