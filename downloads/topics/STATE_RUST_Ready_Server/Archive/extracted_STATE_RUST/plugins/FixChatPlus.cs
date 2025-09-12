using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Network;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("FixChatPlus", "Nimant", "1.0.5")]
    class FixChatPlus : RustPlugin
    {            																					
		
		private static FieldInfo hookSubscriptions = typeof(PluginManager).GetField("hookSubscriptions", (BindingFlags.Instance | BindingFlags.NonPublic));            		
				
		private void OnServerInitialized() 
		{			
			SubscribeInternalHook("IOnPlayerChat");
			UnsubscribeInternalHook("IOnPlayerChat", "RustCore");
		}	
		
		private void Unload()
		{
			UnsubscribeInternalHook("IOnPlayerChat");
			SubscribeInternalHook("IOnPlayerChat", "RustCore");
		}
						
		private object IOnPlayerChat(ulong playerId, string playerName, string message, ConVar.Chat.ChatChannel channel, BasePlayer basePlayer)
        {            
			var str = message?.Trim();
            if (string.IsNullOrEmpty(str) || str.Equals("text"))            
                return true;
            
			// Fixing '<' '>'			
			str = str.Replace("<", "˂").Replace(">", "˃");
			str = str.Replace(@"\u003c", "˂").Replace(@"\u003e", "˃");
			str = str.Replace(@"\U003c", "˂").Replace(@"\U003e", "˃");
			str = str.Replace(@"\U003C", "˂").Replace(@"\U003E", "˃");
			// End
			
            if (basePlayer == null || !basePlayer.IsConnected)            
                return Interface.CallHook("OnPlayerOfflineChat", playerId, playerName, str, channel);                        
                        
            if (basePlayer.IPlayer == null)            
                return null;
			
			// Фикс тимы для москвы
			if ((int)channel == 1)
			{
				DebugEx.Log(string.Format("[TEAM CHAT] {0} : {1}", basePlayer.ToString(), str), StackTraceLogType.None);
				
				List<Connection> onlineMemberConnections = null;
				var team = basePlayer.Team;
				
				if (team != null)				
					onlineMemberConnections = team.GetOnlineMemberConnections();
								
				if (onlineMemberConnections != null)				
					ConsoleNetwork.SendClientCommand(onlineMemberConnections, "chat.add2", new object[] { 1, basePlayer.userID, str, basePlayer.displayName.EscapeRichText(), "#5af", 1f });
				
				return false;	
			}
			// End
            
            return Interface.CallHook("OnPlayerChat", basePlayer, str, channel) ?? Interface.CallHook("OnUserChat", basePlayer.IPlayer, str);
        }
		
		private void SubscribeInternalHook(string hook, string name = null)
		{
			var hookSubscriptions_ = hookSubscriptions.GetValue(Interface.Oxide.RootPluginManager) as IDictionary<string, IList<Plugin>>;
			
			IList<Plugin> plugins;						
			
			if (!hookSubscriptions_.TryGetValue(hook, out plugins))
            {
                plugins = new List<Plugin>();
                hookSubscriptions_.Add(hook, plugins);
            }						
			
			if (string.IsNullOrEmpty(name))		
			{
				if (!plugins.Contains(this))            
					plugins.Add(this);
			}
			else
			{
				var plg = Interface.Oxide.RootPluginManager.GetPlugin(name);
				
				if (plg != null && !plugins.Contains(plg))
					plugins.Add(plg);
			}           
		}
		
		private void UnsubscribeInternalHook(string hook, string name = null)
        {            
			var hookSubscriptions_ = hookSubscriptions.GetValue(Interface.Oxide.RootPluginManager) as IDictionary<string, IList<Plugin>>;		
			
			IList<Plugin> plugins;
			
			if (string.IsNullOrEmpty(name))		
			{				
				if (hookSubscriptions_.TryGetValue(hook, out plugins) && plugins.Contains(this))            
					plugins.Remove(this);
			}
			else
			{
				var plg = Interface.Oxide.RootPluginManager.GetPlugin(name);
				
				if (plg != null && hookSubscriptions_.TryGetValue(hook, out plugins) && plugins.Contains(plg))
					plugins.Remove(plg);
			}
        }				
		
    }	
	
}