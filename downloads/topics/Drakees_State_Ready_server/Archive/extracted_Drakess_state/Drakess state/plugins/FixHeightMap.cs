using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Rust;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("FixHeightMap", "Nimant", "1.0.0")]
    class FixHeightMap : RustPlugin
    {
		
		private void OnServerInitialized()
        {
			var ents = UnityEngine.Object.FindObjectsOfType<AddToHeightMap>().ToArray();			
			foreach (var ent in ents)			
				if (ent != null) ent.Process();
		}
		
	}
	
}	