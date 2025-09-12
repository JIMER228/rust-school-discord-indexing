using Network;
using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("KillInfo", "TopPlugin.ru", "1.0.0")]
    class KillInfo : RustPlugin
    {									
		
		private const int MaxRecords = 10;
		private static Dictionary<ulong, List<string>> LastInfo = new Dictionary<ulong, List<string>>();
		
		private void Init() => LoadData();
		
		private void OnNewSave()
		{
			LastInfo.Clear();
			SaveData();
		}
		
		private void OnServerSave() => SaveData();
		
		private void Unload() => SaveData();
		
		private void OnEntityDeath(BasePlayer player, HitInfo hitInfo)
        {
			if (player == null || hitInfo == null || !(hitInfo.Initiator is BasePlayer)) return;
			
			var attacker = hitInfo.Initiator as BasePlayer;
            
			if (attacker.userID.IsSteamId() && player.userID.IsSteamId() && attacker.userID != player.userID)
				AddInfo(attacker, player, true);
        }
		
		private void OnPlayerWound(BasePlayer player, BasePlayer attacker)
		{
			if (player == null || attacker == null) return; 
			
			if (attacker.userID.IsSteamId() && player.userID.IsSteamId() && attacker.userID != player.userID)
			{
				PrintToConsole(player, $"You wounded: wounded by {attacker.displayName} ({attacker.userID})"); 
				AddInfo(attacker, player, false);
			}
		}				
		
		private static void AddInfo(BasePlayer attacker, BasePlayer player, bool isKill)
		{			
			if (!LastInfo.ContainsKey(player.userID))
				LastInfo.Add(player.userID, new List<string>());
			
			if (isKill)
				LastInfo[player.userID].Add($"{GetCurDate()} You died: killed by {attacker.displayName} ({attacker.userID})");
			else
				LastInfo[player.userID].Add($"{GetCurDate()} You wounded: wounded by {attacker.displayName} ({attacker.userID})"); 
			
			while (LastInfo[player.userID].Count > MaxRecords)			
				LastInfo[player.userID].RemoveAt(0);						
		}
		
		[ConsoleCommand("kills")]
        private void CommandKills(ConsoleSystem.Arg arg)        
		{			
			var player = arg.Player();									
			if (player == null) return;
			
			var str = "--- Last 10 Kills and Wounds List ---\n";
			
			if (!LastInfo.ContainsKey(player.userID) || LastInfo[player.userID].Count == 0)
				PrintToConsole(player, "No Kills Data");
			else
			{
				foreach (var item in LastInfo[player.userID])
					str += item + "\n";
				
				PrintToConsole(player, str);				
			}
		}
		
		private static string GetCurDate() => "["+DateTime.Now.ToString("HH:mm:ss")+"]";
						
		private void LoadData() => LastInfo = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, List<string>>>("KillInfoData");					
		
		private static void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("KillInfoData", LastInfo);						
		
	}
	
}	