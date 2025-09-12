using System;
using Random = System.Random;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Rust;

namespace Oxide.Plugins
{	
    [Info("ChairBlock", "Nimant", "1.0.0")]
    class ChairBlock : RustPlugin
    {        				      
		
        private object OnItemCraft(ItemCraftTask task, BasePlayer crafter)
        {										
            var itemName = task.blueprint.targetItem.displayName.english;         																																																																				
			
			if (itemName == "Chair")
			{
				task.cancelled = true;
				
				SendReply(crafter, "Крафт стула запрещен!");	            
				
				RefundIngredients(task);
					
				task.owner.inventory.containerMain.MarkDirty();
				task.owner.inventory.containerBelt.MarkDirty();					            

				return false;
			}																					
			
			return null;						
        }
		
		private void RefundIngredients(ItemCraftTask task)
        {            
			Dictionary<int, int> items = new Dictionary<int, int>();
		
			foreach(var item in task.blueprint.ingredients)
			{
				if (!items.ContainsKey(item.itemid))
					items.Add(item.itemid, Convert.ToInt32(item.amount) * task.amount);
				else
					items[item.itemid] += Convert.ToInt32(item.amount) * task.amount;
			}					
			
			foreach(var item in items)
			{										
				var info = ItemManager.FindItemDefinition(item.Key);
				if (info == null) continue;
				var stackable = info.stackable;
				if (stackable <= 0) continue;
				var value = item.Value;
				
				while (value > stackable)
				{						
					Item i = ItemManager.CreateByItemID(item.Key, stackable);
					if (!i.MoveToContainer(task.owner.inventory.containerMain)) 
						if (!i.MoveToContainer(task.owner.inventory.containerBelt)) 
							i.Drop(task.owner.eyes.position, task.owner.eyes.BodyForward() * 2f); 

					value -= stackable;
				}
				
				if (value > 0)
				{
					Item i = ItemManager.CreateByItemID(item.Key, value);
					if (!i.MoveToContainer(task.owner.inventory.containerMain)) 
						if (!i.MoveToContainer(task.owner.inventory.containerBelt)) 
							i.Drop(task.owner.eyes.position, task.owner.eyes.BodyForward() * 2f); 
				}					
			}												        
        }
        
    }
}
