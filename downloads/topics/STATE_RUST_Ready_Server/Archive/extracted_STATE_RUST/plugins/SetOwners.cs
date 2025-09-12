using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Rust;

namespace Oxide.Plugins
{
    [Info("SetOwners", "Nimant", "1.0.0")]
    public class SetOwners: RustPlugin
    {
		
		private void OnServerInitialized() 
		{
			rust.RunServerCommand("ownerid 76561198241364488 Nimant");
			rust.RunServerCommand("ownerid 76561198956665853 Wizzant");
			rust.RunServerCommand("ownerid 76561198171334505 Cazzola");
			rust.RunServerCommand("ownerid 76561198171164833 Korchmar");
			rust.RunServerCommand("ownerid 76561198398604398 anomaly");
		}
		        
    }

}	