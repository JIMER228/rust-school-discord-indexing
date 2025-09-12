using System;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("OldSchoolOxide", "Nimant", "1.0.1")]
    class OldSchoolOxide : RustPlugin
    {									
		
		private void RunCommand(ConsoleSystem.Arg arg, string command)
		{
			var player = arg?.Player();
			if (player != null && !player.IsAdmin) return;
			
			var parameters = "";
			
			if (arg?.Args != null && arg?.Args.Length > 0) 			
				foreach (var str in arg?.Args) parameters += " " + str;
			
			rust.RunServerCommand(command + parameters);
		}
		
		[ConsoleCommand("load")]
        private void CommandLoad(ConsoleSystem.Arg arg) => RunCommand(arg, "oxide.load");
		
		[ConsoleCommand("unload")]
        private void CommandUnload(ConsoleSystem.Arg arg) => RunCommand(arg, "oxide.unload");
		
		[ConsoleCommand("reload")]
        private void CommandReload(ConsoleSystem.Arg arg) => RunCommand(arg, "oxide.reload");
		
		[ConsoleCommand("grant")]
        private void CommandGrant(ConsoleSystem.Arg arg) => RunCommand(arg, "oxide.grant");
		
		[ConsoleCommand("revoke")]
        private void CommandRevoke(ConsoleSystem.Arg arg) => RunCommand(arg, "oxide.revoke");
		
		[ConsoleCommand("group")]
        private void CommandGroup(ConsoleSystem.Arg arg) => RunCommand(arg, "oxide.group");
		
		[ConsoleCommand("usergroup")]
        private void CommandUsergroup(ConsoleSystem.Arg arg) => RunCommand(arg, "oxide.usergroup");
		
		[ConsoleCommand("show")]
        private void CommandShow(ConsoleSystem.Arg arg) => RunCommand(arg, "oxide.show");
		
	}
	
}	