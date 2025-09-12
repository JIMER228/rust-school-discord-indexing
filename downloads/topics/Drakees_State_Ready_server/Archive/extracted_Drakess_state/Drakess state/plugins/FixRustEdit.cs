using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using System;
using Network;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("FixRustEdit", "Nimant", "1.0.0")]    
    internal class FixRustEdit : RustPlugin
    {
		
		private void Init()
        {			
			foreach (var plugin in plugins.PluginManager.GetPlugins().Where(x=> x.IsCorePlugin && x.Title.Contains("RustEdit")).ToList())						
				rust.RunServerCommand($"oxide.unload {plugin.Name}");
		}
		
    }
}