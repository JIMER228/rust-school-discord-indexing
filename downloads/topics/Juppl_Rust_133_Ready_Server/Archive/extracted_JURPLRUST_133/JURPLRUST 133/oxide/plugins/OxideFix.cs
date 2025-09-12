using System.Linq;
using UnityEngine;
using Network;
using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("OxideFix", "Аслан", "1.0.0")]
    class OxideFix : RustPlugin
    { 
        object OnServerCommand(ConsoleSystem.Arg arg)
        {
        	bool oContains = arg.cmd.namefull.Contains("o.");
			bool oContainss = arg.cmd.namefull.Contains("global.");
        	bool oxideContains = arg.cmd.namefull.Contains("oxide.");
        	bool haveConnection = arg.connection != null;
        	bool isPlayer = arg.Player() != null;

        	if ((oContains || oContainss || oxideContains) && (haveConnection || isPlayer))
        	{
        		if (haveConnection && isPlayer)
        			return null;

				string allArgs = "";
				foreach (var check in arg.Args) allArgs += check + " ";
        		PrintWarning($"Попытка взлома {arg.connection.ipaddress}, команда: {arg.cmd.namefull} {allArgs}");
        		ConVar.Server.Log("LogOxideBug.txt", $"Попытка взлома {arg.connection.ipaddress}, команда: {arg.cmd.namefull} {allArgs}");
				return false;
        	}
			return null;
        }
    }
}