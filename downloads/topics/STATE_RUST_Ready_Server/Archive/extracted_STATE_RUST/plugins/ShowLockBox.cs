using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("ShowLockBox", "Nimant", "1.0.1")]
    class ShowLockBox : RustPlugin
    {            		
				
		private object CanUseLockedEntity(BasePlayer player, BaseLock lock_)
		{
			if (player == null || lock_ == null) return null;
			
			if (!player.IsAdmin && player.net.connection.authLevel < 2) return null;
			
			var storage = BaseNetworkable.serverEntities.OfType<StorageContainer>().FirstOrDefault(box => box != null && (box.GetSlot(BaseEntity.Slot.Lock) as BaseLock) == lock_);
			if (storage == null) return null;
			
			return true;
		}
		
    }	
	
}