using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using System;
using Network;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("FixSave", "Nimant", "1.0.0")]    
    internal class FixSave : RustPlugin
    {
		
		private void OnServerShutdown() => SaveRestore.Save(true);
		
		/*private void OnLogProduced(string type, string msg)
		{
			if (type == "log" && msg.Contains("[Executer] До рестарта осталось 30 секунд"))			
				SaveRestore.Save(false);
		}*/
		
    }
}