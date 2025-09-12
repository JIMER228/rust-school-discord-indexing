using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("PluginsTime", "Nimant", "1.0.2")]
    class PluginsTime : RustPlugin
    {											
		
		#region Commands
		
		[ConsoleCommand("pluginstime")]
        private void CommandPluginsTime(ConsoleSystem.Arg arg)        
		{	
			var player = arg?.Player();
			if (player != null && !player.IsAdmin) return;								
				
			string result = "Список плагинов с их нагрузкой на сервер:\n";	
			foreach(var plugin in plugins.PluginManager.GetPlugins().Where(x=> !x.IsCorePlugin).OrderByDescending(x=>x.TotalHookTime))
			{								
				string name = plugin.Filename?.Basename(null) ?? $"{plugin.Title.Replace(" ","")}.dll";
				
				if (string.IsNullOrEmpty(name))
					name = "N|A";
				
				string time = ((double)Math.Round(plugin.TotalHookTime, 2)).ToString("0.###");
				string percent = ((double)Math.Round((100f*plugin.TotalHookTime)/UnityEngine.Time.realtimeSinceStartup, 5)).ToString("0.###");
				string filename = plugin.Filename;
				
				result += $"{name} {time} с ({percent} %) | {filename}\n";
			}
				
			if (player != null)
				PrintToConsole(player, result);
			else
				Puts(result);
		}
		
		#endregion
						
	}
	
}	