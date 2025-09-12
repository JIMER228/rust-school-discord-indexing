using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Friends", "Nimant", "1.0.5")]
	class Friends : RustPlugin
    {
		
		#region Variables
		
		private static Dictionary<ulong, List<ulong>> FriendsData = new Dictionary<ulong, List<ulong>>();
		private static Dictionary<ulong, List<ulong>> FriendsWipeData = new Dictionary<ulong, List<ulong>>();
		private static Dictionary<ulong, PlayerInfo>  PlayerTempData = new Dictionary<ulong, PlayerInfo>();
		private static Dictionary<ulong, List<ulong>> FriendsWaitAccept = new Dictionary<ulong, List<ulong>>();
		
		private class PlayerInfo
		{
			public Dictionary<ulong, long> RemoveFriends = new Dictionary<ulong, long>();
			public int CountAddFriends;
			public long LastRemoveFriends;
		}														
		
		#endregion
		
		#region Hooks
		
		private void Init()
		{
			LoadVariables();
			LoadData();
			LoadWipeData();
			LoadTmpData();			
			LoadDefaultMessages();
		}
		
		private void Unload() => SaveWipeData();
		
		private void OnServerSave() => SaveWipeData();
		
		private void OnNewSave()
		{								
			PlayerTempData.Clear();
			SaveTmpData();
			
			foreach(var pair in FriendsWipeData)
			{
				if (FriendsData.ContainsKey(pair.Key))
					FriendsData[pair.Key] = pair.Value;
				else
					FriendsData.Add(pair.Key, pair.Value);
			}
			SaveData();
			
			FriendsWipeData.Clear();
			SaveWipeData();
		}
		
		private void OnPlayerConnected(BasePlayer player) => LoadFriendList(player.userID);		
		
		private void OnServerInitialized()
		{
			foreach (var player in BasePlayer.activePlayerList)  
				OnPlayerConnected(player);							          
		}
		
		#endregion
        
		#region Commands
		
		[ChatCommand("friend")]
        private void ChatFriend(BasePlayer player, string command, string[] args)
        {			
			if (player == null) return;							
			
			if (args == null || args.Length < 1)
            {
                GetMsg(player, "CMD.FRIEND.HELP");
                return;
            }
			
			if (args[0].ToLower() == "list")
			{				
				if (!FriendsWipeData.ContainsKey(player.userID) || FriendsWipeData[player.userID].Count == 0)
				{
					GetMsg(player, "YOU.NOT.FRIEND");
					return;
				}	
				
				string friendList = "";
				int count = 0;
				var players = BasePlayer.activePlayerList.ToList();
				
				foreach(var friend in FriendsWipeData[player.userID].Where(x=> players.Exists(y=> y != null && y.userID == x)))
				{
					if (count >= 15)
					{
						friendList += " и другие...";
						break;
					}
					friendList += " <color=#aae9f2>*</color> " + /*"<color=#90EE90>"+*/FindPlayerName(friend, true)/*+"</color>"*/ + "\n";
					count++;
				}
					
				if (count < 15)
				{
					foreach(var friend in FriendsWipeData[player.userID].Where(x=> !players.Exists(y=> y != null && y.userID == x)))
					{
						if (count >= 15)
						{
							friendList += " и другие...";
							break;
						}
						friendList += " <color=#aae9f2>*</color> " + /*"<color=#FFA07A>"+*/FindPlayerName(friend, true)/*+"</color>\n"*/ + "\n";
						count++;
					}
				}
				
				GetMsg(player, "FRIEND.LIST", new List<object>() { friendList.Trim('\n') });
				return;
			}
			
			if (args[0].ToLower() == "add")
			{
				if (args.Length < 2)
				{					
					GetMsg(player, "FRIEND.ADD.HELP");
					return;
				}
								
				if (FriendsWipeData.ContainsKey(player.userID) && FriendsWipeData[player.userID].Count >= configData.MaxFriends)
				{
					GetMsg(player, "YOUR.FRIENDS.LIMIT", new List<object>() { configData.MaxFriends });
					return;
				}	
								
				string nameOrId = "";
				for(int ii=1;ii<args.Length;ii++)
					nameOrId += args[ii] + " ";
				nameOrId = nameOrId.Trim(' ');
								
				var friend = FindOnlinePlayer(player, nameOrId);
				if (friend == null) return;								
				
				if (friend.userID == player.userID)
				{
					GetMsg(player, "CANT.SEND.REQUEST.YOURSELF");					
					return;
				}
				
				if (FriendsWipeData.ContainsKey(player.userID) && FriendsWipeData[player.userID].Contains(friend.userID))
				{
					GetMsg(player, "PLAYER.ALREADY.FRIENDS", new List<object>() { friend.displayName });
					return;
				} 
				
				if (FriendsWaitAccept.ContainsKey(player.userID) && FriendsWaitAccept[player.userID].Contains(friend.userID))
				{
					GetMsg(player, "YOUR.ACTIVE.REQUEST", new List<object>() { friend.displayName });
					return;
				}
				
				if (FriendsWaitAccept.ContainsKey(friend.userID) && FriendsWaitAccept[friend.userID].Contains(player.userID))
				{
					GetMsg(player, "PLAYER.SEND.YOU.REQUEST", new List<object>() { friend.displayName });
					return;
				}								
				
				foreach(var pair in FriendsWaitAccept.Where(x=> x.Value.Contains(friend.userID)))
				{
					GetMsg(player, "PLAYER.ACTIVE.REQUEST", new List<object>() { friend.displayName, FindPlayerName(pair.Key) });
					return;
				}	
				
				if (FriendsWipeData.ContainsKey(friend.userID) && FriendsWipeData[friend.userID].Count >= configData.MaxFriends)
				{
					GetMsg(player, "PLAYER.FRIENDS.LIMIT", new List<object>() { friend.displayName, configData.MaxFriends });
					return;
				}
								
				if (PlayerTempData.ContainsKey(player.userID) && (ToEpochTime(DateTime.Now) - PlayerTempData[player.userID].LastRemoveFriends) <= configData.BlockTimeToAddAgain * 60)
				{
					GetMsg(player, "CANT.SEND.REQUEST.YOU.COOLDOWN", new List<object>() { GetTime(configData.BlockTimeToAddAgain * 60 - (ToEpochTime(DateTime.Now) - PlayerTempData[player.userID].LastRemoveFriends)) });
					return;
				}
				
				if (PlayerTempData.ContainsKey(friend.userID) && (ToEpochTime(DateTime.Now) - PlayerTempData[friend.userID].LastRemoveFriends) <= configData.BlockTimeToAddAgain * 60)
				{
					GetMsg(player, "CANT.SEND.REQUEST.TARGET.COOLDOWN", new List<object>() { friend.displayName, GetTime(configData.BlockTimeToAddAgain * 60 - (ToEpochTime(DateTime.Now) - PlayerTempData[friend.userID].LastRemoveFriends)) });
					return;
				}
				
				if (PlayerTempData.ContainsKey(player.userID) && PlayerTempData[player.userID].RemoveFriends.ContainsKey(friend.userID) && (ToEpochTime(DateTime.Now) - PlayerTempData[player.userID].RemoveFriends[friend.userID]) <= configData.BlockTimeToAddFriendAgain * 60)
				{
					GetMsg(player, "CANT.SEND.REQUEST.YOU.COOLDOWN.OLD.FRIENDS", new List<object>() { friend.displayName, GetTime(configData.BlockTimeToAddFriendAgain * 60 - (ToEpochTime(DateTime.Now) - PlayerTempData[player.userID].RemoveFriends[friend.userID])) });
					return;
				}
				
				if (PlayerTempData.ContainsKey(friend.userID) && PlayerTempData[friend.userID].RemoveFriends.ContainsKey(player.userID) && (ToEpochTime(DateTime.Now) - PlayerTempData[friend.userID].RemoveFriends[player.userID]) <= configData.BlockTimeToAddFriendAgain * 60)
				{
					GetMsg(player, "CANT.SEND.REQUEST.TARGET.COOLDOWN.OLD.FRIENDS", new List<object>() { friend.displayName, GetTime(configData.BlockTimeToAddFriendAgain * 60 - (ToEpochTime(DateTime.Now) - PlayerTempData[friend.userID].RemoveFriends[player.userID])) });
					return;
				}
				
				if (PlayerTempData.ContainsKey(player.userID) && PlayerTempData[player.userID].CountAddFriends >= configData.LimitToAddFrinds)
				{
					GetMsg(player, "CANT.SEND.REQUEST.YOU.LIMIT.ADD.FRIENDS");
					return;
				}
				
				if (PlayerTempData.ContainsKey(friend.userID) && PlayerTempData[friend.userID].CountAddFriends >= configData.LimitToAddFrinds)
				{
					GetMsg(player, "CANT.SEND.REQUEST.TARGET.LIMIT.ADD.FRIENDS", new List<object>() { friend.displayName } );
					return;
				}
				
				TryAddFrind(player, friend);																				
				return;
			}	
			
			if (args[0].ToLower() == "accept")
			{				
				foreach(var pair in FriendsWaitAccept.Where(x=> x.Value.Contains(player.userID)).ToDictionary(x=> x.Key, x=> x.Value))
				{
					if (!FriendsWipeData.ContainsKey(pair.Key))
						FriendsWipeData.Add(pair.Key, new List<ulong>());
					
					FriendsWipeData[pair.Key].Add(player.userID);
					
					if (!FriendsWipeData.ContainsKey(player.userID))
						FriendsWipeData.Add(player.userID, new List<ulong>());
					
					FriendsWipeData[player.userID].Add(pair.Key);
										
					(FriendsWaitAccept[pair.Key]).Remove(player.userID);										
										
					var friend = BasePlayer.FindByID(pair.Key);
					var friendName = FindPlayerName(pair.Key);
					
					GetMsg(player, "YOU.ADDED.PLAYER", new List<object>() { friendName } );					
					GetMsg(friend, "PLAYER.ADDED.YOU", new List<object>() { player.displayName });																															
					
					if (!PlayerTempData.ContainsKey(player.userID))
						PlayerTempData.Add(player.userID, new PlayerInfo());
					
					PlayerTempData[player.userID].CountAddFriends++;
					
					if (!PlayerTempData.ContainsKey(pair.Key))
						PlayerTempData.Add(pair.Key, new PlayerInfo());
					
					PlayerTempData[pair.Key].CountAddFriends++;
					
					GetMsg(player, "PLAYER.ADDED.YOU.LIMIT", new List<object>() { PlayerTempData[player.userID].CountAddFriends, configData.LimitToAddFrinds });
					GetMsg(friend, "PLAYER.ADDED.YOU.LIMIT", new List<object>() { PlayerTempData[pair.Key].CountAddFriends, configData.LimitToAddFrinds });
							
					Interface.Oxide.CallHook("OnFriendAdded", player.userID.ToString(), pair.Key.ToString());
					Interface.Oxide.CallHook("OnFriendAdded", pair.Key.ToString(), player.userID.ToString());										
					
					CallSomeOvhHooks(player.userID);
					CallSomeOvhHooks(pair.Key);
							
					SaveTmpData();
					return;
				}
				
				GetMsg(player, "YOU.NO.REQUEST");
				return;				
			}	
			
			if (args[0].ToLower() == "deny")
			{				
				foreach(var pair in FriendsWaitAccept.Where(x=> x.Value.Contains(player.userID)).ToDictionary(x=> x.Key, x=> x.Value))
				{																				
					(FriendsWaitAccept[pair.Key]).Remove(player.userID);	

					var friend = BasePlayer.FindByID(pair.Key);					
					
					GetMsg(player, "YOU.REFUSE.REQUEST");					
					GetMsg(friend, "PLAYER.REFUSE.REQUEST", new List<object>() { player.displayName });	
												
					return;
				}
				
				GetMsg(player, "YOU.NO.REQUEST");
				return;				
			}	
			
			if (args[0].ToLower() == "remove")
			{
				if (args.Length < 2)
				{
					GetMsg(player, "FRIEND.REMOVE.HELP");					
					return;
				}								
								
				string nameOrId = "";
				for(int ii=1;ii<args.Length;ii++)
					nameOrId += args[ii] + " ";
				nameOrId = nameOrId.Trim(' ');
								
				var friendID = FindYourFriend(player, nameOrId);
				if (friendID == 0) return;																
				
				if (friendID == player.userID)
				{
					GetMsg(player, "CANT.SEND.REMOVE.YOURSELF");					
					return;
				}
				
				if (!FriendsWipeData.ContainsKey(player.userID) || (FriendsWipeData.ContainsKey(player.userID) && !FriendsWipeData[player.userID].Contains(friendID)))
				{
					GetMsg(player, "PLAYER.NOTFOUND.FRIEND", new List<object>() { FindPlayerName(friendID) });
					return;
				} 
				
				if (FriendsWipeData.ContainsKey(player.userID))
					FriendsWipeData[player.userID].Remove(friendID);
				
				if (FriendsWipeData.ContainsKey(friendID))
					FriendsWipeData[friendID].Remove(player.userID);
				else
				{
					LoadFriendList(friendID);
					if (FriendsWipeData.ContainsKey(friendID))
						FriendsWipeData[friendID].Remove(player.userID);
				}
				
				var friend = BasePlayer.FindByID(friendID);
				
				GetMsg(player, "YOU.REMOVED.FRIEND", new List<object>() { FindPlayerName(friendID) } );				
				GetMsg(friend, "FRIEND.REMOVED.YOU", new List<object>() { player.displayName });	
				
				if (!PlayerTempData.ContainsKey(player.userID))
					PlayerTempData.Add(player.userID, new PlayerInfo());
				
				if (!PlayerTempData[player.userID].RemoveFriends.ContainsKey(friendID))
					PlayerTempData[player.userID].RemoveFriends.Add(friendID, ToEpochTime(DateTime.Now));
				else
					PlayerTempData[player.userID].RemoveFriends[friendID] = ToEpochTime(DateTime.Now);
				
				PlayerTempData[player.userID].LastRemoveFriends = ToEpochTime(DateTime.Now);
								
				if (!PlayerTempData.ContainsKey(friendID))
					PlayerTempData.Add(friendID, new PlayerInfo());
				
				if (!PlayerTempData[friendID].RemoveFriends.ContainsKey(player.userID))
					PlayerTempData[friendID].RemoveFriends.Add(player.userID, ToEpochTime(DateTime.Now));
				else
					PlayerTempData[friendID].RemoveFriends[player.userID] = ToEpochTime(DateTime.Now);
				
				PlayerTempData[friendID].LastRemoveFriends = ToEpochTime(DateTime.Now);
				
				Interface.Oxide.CallHook("OnFriendRemoved", player.userID.ToString(), friendID.ToString());
				Interface.Oxide.CallHook("OnFriendRemoved", friendID.ToString(), player.userID.ToString());
				
				CallSomeOvhHooks(player.userID);
				CallSomeOvhHooks(friendID);
								
				SaveTmpData();			
				return;
			}	
			
			if (args[0].ToLower() == "removeall")
			{				
				if (!FriendsWipeData.ContainsKey(player.userID) || FriendsWipeData[player.userID].Count == 0)
				{
					GetMsg(player, "YOU.NOT.FRIEND");
					return;
				} 
				
				if (!PlayerTempData.ContainsKey(player.userID))
					PlayerTempData.Add(player.userID, new PlayerInfo());
				
				foreach(var friendID in FriendsWipeData[player.userID].ToList())
				{							
					FriendsWipeData[player.userID].Remove(friendID);
					
					if (FriendsWipeData.ContainsKey(friendID))
						FriendsWipeData[friendID].Remove(player.userID);
					else
					{
						LoadFriendList(friendID);
						if (FriendsWipeData.ContainsKey(friendID))
							FriendsWipeData[friendID].Remove(player.userID);
					}
					
					var friend = BasePlayer.FindByID(friendID);
										
					GetMsg(friend, "FRIEND.REMOVED.YOU", new List<object>() { player.displayName });											
					
					if (!PlayerTempData[player.userID].RemoveFriends.ContainsKey(friendID))
						PlayerTempData[player.userID].RemoveFriends.Add(friendID, ToEpochTime(DateTime.Now));
					else
						PlayerTempData[player.userID].RemoveFriends[friendID] = ToEpochTime(DateTime.Now);
																			
					if (!PlayerTempData.ContainsKey(friendID))
						PlayerTempData.Add(friendID, new PlayerInfo());
					
					if (!PlayerTempData[friendID].RemoveFriends.ContainsKey(player.userID))
						PlayerTempData[friendID].RemoveFriends.Add(player.userID, ToEpochTime(DateTime.Now));
					else
						PlayerTempData[friendID].RemoveFriends[player.userID] = ToEpochTime(DateTime.Now);										
					
					PlayerTempData[friendID].LastRemoveFriends = ToEpochTime(DateTime.Now);
					
					Interface.Oxide.CallHook("OnFriendRemoved", player.userID.ToString(), friendID.ToString());
					Interface.Oxide.CallHook("OnFriendRemoved", friendID.ToString(), player.userID.ToString());
					
					CallSomeOvhHooks(player.userID);
					CallSomeOvhHooks(friendID);
				}
				
				PlayerTempData[player.userID].LastRemoveFriends = ToEpochTime(DateTime.Now);				
				GetMsg(player, "YOU.REMOVED.FRIENDS");
				
				SaveTmpData();			
				return;
			}
			
			GetMsg(player, "CMD.FRIEND.HELP");		
        }    
		
		#endregion
		
		#region Main
		
		private void CallSomeOvhHooks(ulong userID)
		{
			var result = new List<BasePlayer>();
						
			var players = BasePlayer.activePlayerList.ToList();
						
			foreach(var friend2 in GetFriends(userID).Where(x=> players.Exists(y=> y != null && y.userID == x)))
				result.Add(BasePlayer.FindByID(friend2));		
					
			if (players.Exists(y=> y != null && y.userID == userID))																				
				Interface.Oxide.CallHook("OnActiveFriendsUpdate", BasePlayer.FindByID(userID), result);						
			else																					
				Interface.Oxide.CallHook("OnActiveFriendsUpdateUserId", userID, result);	
		}
		
		private void TryAddFrind(BasePlayer player, BasePlayer friend)
		{
			if (!FriendsWaitAccept.ContainsKey(player.userID))
				FriendsWaitAccept.Add(player.userID, new List<ulong>());
			
			FriendsWaitAccept[player.userID].Add(friend.userID);
			GetMsg(player, "YOU.SEND.REQUEST", new List<object>() { friend.displayName } );
			GetMsg(friend, "PLAYER.SEND.REQUEST.YOU", new List<object>() { player.displayName } );
			
			var playerID = player.userID;
			var friendID = friend.userID;
			var friendName = friend.displayName;
			
			timer.Once(configData.TimeToAnswer, ()=>
			{
				if (FriendsWaitAccept.ContainsKey(playerID) && FriendsWaitAccept[playerID].Contains(friendID))
				{								
					(FriendsWaitAccept[playerID]).Remove(friendID);
					GetMsg(player, "PLAYER.CANCELED.WAIT.REQUEST", new List<object>() { friendName } );
					GetMsg(friend, "YOU.CANCELED.WAIT.REQUEST");
				}	
			});									
		}
		
		private static void LoadFriendList(ulong userID)
		{
			if (!FriendsWipeData.ContainsKey(userID) && FriendsData.ContainsKey(userID))						
				FriendsWipeData.Add(userID, FriendsData[userID]);															
		}
		
		#endregion
		
		#region Common
		
		private string GetPlayerName(ulong userID)
		{
			var data = permission.GetUserData(userID.ToString());															
			return data.LastSeenNickname;
		}
		
		private string FindPlayerName(ulong userID, bool isFull = false)
        {            
			var player = BasePlayer.activePlayerList.FirstOrDefault(x=>x.userID == userID);
			
			if (player == null)
			{
				player = BasePlayer.sleepingPlayerList.FirstOrDefault(x=>x.userID == userID);
				if (player == null)
				{
					var name = GetPlayerName(userID);					

					if (name != "Unnamed")
						return isFull ? (name + " (" + userID.ToString() + ")") : name;
					else
						return isFull ? ("Без имени" + " (" + userID.ToString() + ")") : "Без имени";
				}	
			}							

            return isFull ? (player.displayName + " (" + userID.ToString() + ")") : player.displayName;
        }       
		
		private BasePlayer FindOnlinePlayer(BasePlayer player, string nameOrID)
		{
			if (nameOrID.IsSteamId())
			{
				var target = BasePlayer.FindByID((ulong)Convert.ToInt64(nameOrID));
				if (target == null)
				{					
					GetMsg(player, "PLAYER.NOTFOUND", new List<object>() { nameOrID });
					return null;
				}
				
				return target;
			}
			
			var targets = BasePlayer.activePlayerList.Where(x=> x.displayName == nameOrID).ToList();
			
			if (targets.Count() == 1)
				return targets[0];
			
			if (targets.Count() > 1)
			{
				GetMsg(player, "PLAYER.MULTIPLE.FOUND", new List<object>() { targets.Count() });
				return null;
			}
			
            targets = BasePlayer.activePlayerList.Where(x=> x.displayName.ToLower() == nameOrID.ToLower()).ToList();
			
			if (targets.Count() == 1)
				return targets[0];
			
			if (targets.Count() > 1)
			{
				GetMsg(player, "PLAYER.MULTIPLE.FOUND", new List<object>() { targets.Count() });
				return null;
			}	
					
			targets = BasePlayer.activePlayerList.Where(x=> x.displayName.Contains(nameOrID)).ToList();
			
			if (targets.Count() == 1)
				return targets[0];
			
			if (targets.Count() > 1)
			{
				GetMsg(player, "PLAYER.MULTIPLE.FOUND", new List<object>() { targets.Count() });
				return null;
			}
			
			targets = BasePlayer.activePlayerList.Where(x=> x.displayName.ToLower().Contains(nameOrID.ToLower())).ToList();
			
			if (targets.Count() == 1)
				return targets[0];
			
			if (targets.Count() > 1)
			{
				GetMsg(player, "PLAYER.MULTIPLE.FOUND", new List<object>() { targets.Count() });
				return null;
			}
			
			GetMsg(player, "PLAYER.NOTFOUND", new List<object>() { nameOrID });
			return null;
		}
		
		private ulong FindYourFriend(BasePlayer player, string nameOrID)
		{
			if (!FriendsWipeData.ContainsKey(player.userID))
			{
				GetMsg(player, "YOU.NOT.FRIEND");
				return 0;
			}
			
			var friends = FriendsWipeData[player.userID];

			if (friends.Count() == 0)
			{
				GetMsg(player, "YOU.NOT.FRIEND");
				return 0;
			}
			
			if (nameOrID.IsSteamId())
			{
				var targetID = (ulong)Convert.ToInt64(nameOrID);
				if (!friends.Contains(targetID))
				{					
					GetMsg(player, "PLAYER.NOTFOUND.FRIEND", new List<object>() { FindPlayerName(targetID) });
					return 0;
				}
				
				return targetID;
			}
			
			var targets = friends.Where(x=> FindPlayerName(x) == nameOrID).ToList();
			
			if (targets.Count() == 1)
				return targets[0];
			
			if (targets.Count() > 1)
			{
				GetMsg(player, "PLAYER.MULTIPLE.FOUND", new List<object>() { targets.Count() });
				return 0;
			}
			
			targets = friends.Where(x=> FindPlayerName(x).ToLower() == nameOrID.ToLower()).ToList();
			
			if (targets.Count() == 1)
				return targets[0];
			
			if (targets.Count() > 1)
			{
				GetMsg(player, "PLAYER.MULTIPLE.FOUND", new List<object>() { targets.Count() });
				return 0;
			}
			
			targets = friends.Where(x=> FindPlayerName(x).Contains(nameOrID)).ToList();
			
			if (targets.Count() == 1)
				return targets[0];
			
			if (targets.Count() > 1)
			{
				GetMsg(player, "PLAYER.MULTIPLE.FOUND", new List<object>() { targets.Count() });
				return 0;
			}
			
			targets = friends.Where(x=> FindPlayerName(x).ToLower().Contains(nameOrID.ToLower())).ToList();
			
			if (targets.Count() == 1)
				return targets[0];
			
			if (targets.Count() > 1)
			{
				GetMsg(player, "PLAYER.MULTIPLE.FOUND", new List<object>() { targets.Count() });
				return 0;
			}
			
			GetMsg(player, "PLAYER.NOTFOUND.FRIEND.LIST", new List<object>() { nameOrID });
			return 0;
		}								
		
		private static string GetTime(long time)
        {            
			TimeSpan elapsedTime = TimeSpan.FromSeconds(time);
            int hours = elapsedTime.Hours;
            int minutes = elapsedTime.Minutes;
            int seconds = elapsedTime.Seconds;
            int days = Mathf.FloorToInt((float)elapsedTime.TotalDays);
            string s = "";
			int cnt = 0;
			
            if (days > 0) { s += $"{GetStringCount(days, new List<string>() {"день","дня","дней"})} "; cnt+=1; }
            if (hours > 0) { s += (cnt == 1 ? "и " : "") + $@"{GetStringCount(hours, new List<string>() {"час","часа","часов"})} "; cnt+=2; }
			if (cnt == 1 || cnt == 3) return s.TrimEnd(' ');
			
            if (minutes > 0) { s += (cnt == 2 ? "и " : "") + $"{GetStringCount(minutes, new List<string>() {"минута","минуты","минут"})} "; cnt+=4; }			
			if (cnt == 2 || cnt == 6) return s.TrimEnd(' ');
			
            if (seconds > 0) s += (cnt == 4 ? "и " : "") + $"{GetStringCount(seconds, new List<string>() {"секунда","секунды","секунд"})} ";            						
			if (string.IsNullOrEmpty(s)) return "несколько секунд";
			
            return s.TrimEnd(' ');
        }				
		
		private static string GetStringCount(long count, List<string> words)
		{	
			switch(count)
			{
				case 11: 
				case 12: 
				case 13: 
				case 14: return $"{count} {words[2]}";
			}
			
			var countString = count.ToString();			
			switch(countString[countString.Length-1])
			{
				case '1': return $"{count} {words[0]}";
				case '2': 
				case '3': 
				case '4': return $"{count} {words[1]}";				
			}
			
			return $"{count} {words[2]}";
		}				
		
		private long ToEpochTime(DateTime dateTime)
        {
            var date = dateTime.ToLocalTime();
            var ticks = date.Ticks - new DateTime(1970, 1, 1, 0, 0, 0, 0).Ticks;
            var ts = ticks / TimeSpan.TicksPerSecond;
            return ts;
        }
		
		private void GetMsg(BasePlayer player, string key, List<object> params_ = null)
		{
			var message = GetLangMessage(key);			
			
			if (params_ != null)
				for(int ii=0;ii<params_.Count;ii++) message = message.Replace("{"+ii+"}", Convert.ToString(params_[ii]));
			
			if (player != null)
				SendReply(player, message);						
		}
		
		#endregion
		
		#region Lang
		
        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {                
				{ "CMD.FRIEND.HELP", "ДОСТУПНЫЕ КОМАНДЫ:\n<color=#aae9f2>/friend add \"имя игрока\"</color> - добавить в список друзей\n<color=#aae9f2>/friend remove \"имя игрока\"</color> - удалить из списка друзей\n<color=#aae9f2>/friend removeall</color> - очистить список друзей\n<color=#aae9f2>/friend list</color> - показать список друзей"},
				{ "FRIEND.REMOVE.HELP", "Используете <color=#aae9f2>/friend remove \"имя игрока\"</color> чтобы удалить игрока из списка друзей."},
				{ "FRIEND.ADD.HELP", "Используйте <color=#aae9f2>/friend add \"имя игрока\"</color> чтобы добавить игрока в список друзей"},
				{ "YOUR.ACTIVE.REQUEST", "Вы уже отправили игроку <color=#aae9f2>{0}</color> предложение дружбы, дождитесь ответа."},
				{ "PLAYER.ACTIVE.REQUEST", "Игрок <color=#aae9f2>{0}</color> имеет активное предложение дружбы от <color=#aae9f2>{1}</color>, попробуйте позже."},
				{ "PLAYER.SEND.YOU.REQUEST", "Игрок <color=#aae9f2>{0}</color> уже отправил вам предложение дружбы.\nИспользуйте <color=#aae9f2>/friend accept</color> чтобы принять предложение или <color=#aae9f2>/friend deny</color> чтобы отменить предложение."},
				{ "PLAYER.ALREADY.FRIENDS", "Игрок <color=#aae9f2>{0}</color> уже есть в списке друзей."},
				{ "CANT.SEND.REQUEST.YOURSELF", "Вы не можете отправить предложение дружбы самому себе."},
				{ "CANT.SEND.REMOVE.YOURSELF", "Вы не можете удалять самого себя."},
				{ "CANT.SEND.REQUEST.YOU.COOLDOWN", "Вы не можете отправлять предложения дружбы, вы недавно удалили из списка одного из друзей, подождите <color=#aae9f2>{0}</color>."},
				{ "CANT.SEND.REQUEST.TARGET.COOLDOWN", "Вы не можете отправить предложение дружбы игроку <color=#aae9f2>{0}</color>, так как он недавно удалил из списка одного из друзей, подождите <color=#aae9f2>{1}</color>."},
				{ "CANT.SEND.REQUEST.YOU.COOLDOWN.OLD.FRIENDS", "Вы не можете отправить предложение дружбы игроку <color=#aae9f2>{0}</color>, так как вы недавно удалили его из списка друзей, подождите <color=#aae9f2>{1}</color>."},
				{ "CANT.SEND.REQUEST.TARGET.COOLDOWN.OLD.FRIENDS", "Вы не можете отправить предложение дружбы игроку <color=#aae9f2>{0}</color>, так как он недавно удалил вас из списка друзей, подождите <color=#aae9f2>{1}</color>."},			
				{ "CANT.SEND.REQUEST.YOU.LIMIT.ADD.FRIENDS", "Вы не можете отправлять предложения дружбы, вы исчерпали лимит на количество добавлений в друзья."},
				{ "CANT.SEND.REQUEST.TARGET.LIMIT.ADD.FRIENDS", "Вы не можете отправить предложение дружбы игроку <color=#aae9f2>{0}</color>, он исчерпал лимит на количество добавлений в друзья."},
				{ "YOUR.FRIENDS.LIMIT", "Вы имеете максимальное количество друзей <color=#aae9f2>{0}</color>."},
				{ "PLAYER.FRIENDS.LIMIT", "Игрок <color=#aae9f2>{0}</color> имеет максимальное количество друзей <color=#aae9f2>{1}</color>."},
				{ "YOU.NO.REQUEST", "У вас нет предложений дружбы."},
				{ "YOU.REFUSE.REQUEST", "Вы отказались от предложения дружбы."},
				{ "PLAYER.REFUSE.REQUEST", "Игрок <color=#aae9f2>{0}</color> отказался от предложения дружбы."},
				{ "PLAYER.CANCELED.WAIT.REQUEST", "Игрок <color=#aae9f2>{0}</color> не ответил на предложение дружбы."},
				{ "YOU.CANCELED.WAIT.REQUEST", "Вы не ответили на предложение дружбы."},
				{ "YOU.SEND.REQUEST", "Предложение дружбы для <color=#aae9f2>{0}</color> успешно отправлено."},
				{ "PLAYER.SEND.REQUEST.YOU", "Игрок <color=#aae9f2>{0}</color> отправил вам предложение дружбы.\nИспользуйте <color=#aae9f2>/friend accept</color> чтобы принять предложение или <color=#aae9f2>/friend deny</color> чтобы отменить предложение."},
				{ "YOU.ADDED.PLAYER", "<color=#aae9f2>{0}</color> добавлен в список друзей."},
				{ "PLAYER.ADDED.YOU", "<color=#aae9f2>{0}</color> добавил вас в список друзей."},
				{ "PLAYER.ADDED.YOU.LIMIT", "Лимит на количество добавлений в друзья <color=#aae9f2>{0}</color>/<color=#aae9f2>{1}</color>."},
				{ "PLAYER.NOTFOUND.FRIEND", "<color=#aae9f2>{0}</color> не является вашим другом."},
				{ "YOU.REMOVED.FRIEND", "<color=#aae9f2>{0}</color> удален из списка друзей."},
				{ "YOU.REMOVED.FRIENDS", "Вы очистили свой список друзей."},
				{ "FRIEND.REMOVED.YOU", "<color=#aae9f2>{0}</color> удалил вас из списка друзей."},
				{ "FRIEND.LIST", "СПИСОК ДРУЗЕЙ:\n{0}"},
				{ "YOU.NOT.FRIEND", "У вас нет друзей."},
				{ "PLAYER.NOTFOUND", "Игрок <color=#aae9f2>{0}</color> не найден, возможно он отключён."},
				{ "PLAYER.NOTFOUND.FRIEND.LIST", "Игрок <color=#aae9f2>{0}</color> не найден в списке ваших друзей."},
				{ "PLAYER.MULTIPLE.FOUND", "Найдено <color=#aae9f2>{0}</color> похожих игроков, уточните запрос или используйте steam id игрока."}	
            }, this);
        }

        private string GetLangMessage(string key, string steamID = null) => lang.GetMessage(key, this, steamID);
		
		#endregion
		
		#region Config        				
		
        private static ConfigData configData;
		
        private class ConfigData
        {
			[JsonProperty(PropertyName = "Максимальное количество друзей")]
			public int MaxFriends;
			[JsonProperty(PropertyName = "Время для ответа на предложение дружбы (в секундах)")]
			public int TimeToAnswer;
			[JsonProperty(PropertyName = "Блокировка добавления игроков в друзья после удаления (в минутах)")]
			public int BlockTimeToAddAgain;			
			[JsonProperty(PropertyName = "Блокировка добавления удаленного игрока в друзья после удаления (в минутах)")]
			public int BlockTimeToAddFriendAgain;			
			[JsonProperty(PropertyName = "Лимит на количество добавлений в друзья")]
			public int LimitToAddFrinds;						
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                MaxFriends = 15,
				TimeToAnswer = 20,
				BlockTimeToAddAgain = 5,
				BlockTimeToAddFriendAgain = 10,
				LimitToAddFrinds = 20
            };
            SaveConfig(config);
			timer.Once(0.3f, ()=>SaveConfig(config));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);				
		
        #endregion
		
		#region Data
		
		private void LoadData() => FriendsData = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, List<ulong>>>("FriendsMainData");					
		
		private void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("FriendsMainData", FriendsData);		
		
		private void LoadTmpData() => PlayerTempData = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, PlayerInfo>>("FriendsTempData");					
		
		private void SaveTmpData() => Interface.GetMod().DataFileSystem.WriteObject("FriendsTempData", PlayerTempData);		
		
		private void LoadWipeData() => FriendsWipeData = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, List<ulong>>>("FriendsWipeData");					
		
		private void SaveWipeData() => Interface.GetMod().DataFileSystem.WriteObject("FriendsWipeData", FriendsWipeData);		
		
		#endregion
		
		#region API											
		
		private bool HasFriend(ulong playerId, ulong friendId) 
		{
			if (!FriendsWipeData.ContainsKey(playerId))							
				return false;
			
			return FriendsWipeData[playerId].Contains(friendId);
		}

        private bool HasFriendS(string playerS, string friendS)
		{
			if (string.IsNullOrEmpty(playerS) || string.IsNullOrEmpty(friendS)) return false;			
			if (!playerS.IsSteamId() || !friendS.IsSteamId()) return false;
			var playerId = Convert.ToUInt64(playerS);
            var friendId = Convert.ToUInt64(friendS);
            return HasFriend(playerId, friendId);			
		}		                

        private bool AreFriends(ulong playerId, ulong friendId)
        {
			if (!FriendsWipeData.ContainsKey(playerId))							
				return false;
			
			if (!FriendsWipeData.ContainsKey(friendId))							
				return false;
			
            return FriendsWipeData[playerId].Contains(friendId) && FriendsWipeData[friendId].Contains(playerId);
        }

        private bool AreFriendsS(string playerS, string friendS)
        {
            if (string.IsNullOrEmpty(playerS) || string.IsNullOrEmpty(friendS)) return false;			
			if (!playerS.IsSteamId() || !friendS.IsSteamId()) return false;
			var playerId = Convert.ToUInt64(playerS);
            var friendId = Convert.ToUInt64(friendS);
            return AreFriends(playerId, friendId);
        }

		private bool HadFriend(ulong playerId, ulong friendId) => HasFriend(playerId, friendId);        

        private bool HadFriendS(string playerS, string friendS) => HasFriendS(playerS, friendS);        
		
        private bool WereFriends(ulong playerId, ulong friendId) => AreFriends(playerId, friendId);        

        private bool WereFriendsS(string playerS, string friendS) => AreFriendsS(playerS, friendS);         

        private bool IsFriend(ulong playerId, ulong friendId) => HasFriend(playerId, friendId);        

        private bool IsFriendS(string playerS, string friendS) => HasFriendS(playerS, friendS);            

        private bool WasFriend(ulong playerId, ulong friendId) => AreFriends(playerId, friendId);        

        private bool WasFriendS(string playerS, string friendS) => AreFriendsS(playerS, friendS);          
		
		private ulong[] GetFriends(ulong playerId) 
		{
			if (!FriendsWipeData.ContainsKey(playerId))							
				return null;
						
			return FriendsWipeData[playerId].ToArray();
		}				

        private string[] GetFriendsS(string playerS)
        {
			if (string.IsNullOrEmpty(playerS)) return null;
			if (!playerS.IsSteamId()) return null;
			
            var playerId = Convert.ToUInt64(playerS);
			
			if (!FriendsWipeData.ContainsKey(playerId))							
				return null;
			
            return FriendsWipeData[playerId].ToList().ConvertAll(f => f.ToString()).ToArray();
        }

        private string[] GetFriendList(ulong playerId)
        {
			if (!FriendsWipeData.ContainsKey(playerId))							
				return null;
			            
            var players = new List<string>();
            foreach (var friendID in FriendsWipeData[playerId])
                players.Add(FindPlayerName(friendID));
				
            return players.ToArray();
        }

        private string[] GetFriendListS(string playerS)
		{
			if (string.IsNullOrEmpty(playerS)) return null;
			if (!playerS.IsSteamId()) return null;
			
			return GetFriendList(Convert.ToUInt64(playerS));
		}

        private ulong[] IsFriendOf(ulong playerId)
        {
			return FriendsWipeData.Where(x=> x.Value.Contains(playerId)).Select(x=> x.Key).ToArray();
        }

        private string[] IsFriendOfS(string playerS)
        {
			if (string.IsNullOrEmpty(playerS)) return null;
			if (!playerS.IsSteamId()) return null;
			
            var playerId = Convert.ToUInt64(playerS);
            var friends = IsFriendOf(playerId);
            return friends.ToList().ConvertAll(f => f.ToString()).ToArray();
        }
		
		#endregion
		
		#region API Moscow.Ovh
		
		// ApiIsFriend(ulong playerId, ulong targetId) return true / null - являются ли игроки друзьями
		private bool ApiIsFriend(ulong playerId, ulong targetId) => AreFriends(playerId, targetId);
		
		// ApiGetFriends(ulong playerId) return List<ulong> / null - получить список друзей		
		private List<ulong> ApiGetFriends(ulong playerId) 
		{
			var friends = GetFriends(playerId);
			if (friends != null)
				return friends.ToList();
			
			return null;
		}
		
		// ApiGetActiveFriends(BasePlayer player) return List<ulong> / null - список друзей онлайн
		private List<ulong> ApiGetActiveFriends(BasePlayer player)
		{
			var result = new List<ulong>();
			if (player != null)
			{
				var players = BasePlayer.activePlayerList.ToList();
				
				var friends = GetFriends(player.userID);				
				if (friends != null)
					foreach(var friend in friends.Where(x=> players.Exists(y=> y != null && y.userID == x)))
						result.Add(friend);
				else
					return null;
			}
			return result;
		}
		
		// ApiGetActiveFriendsUserId(ulong userId) return List<ulong> / null - список друзей онлайн
		private List<ulong> ApiGetActiveFriendsUserId(ulong userId)
		{
			var result = new List<ulong>();									
			var friends = GetFriends(userId);
			
			var players = BasePlayer.activePlayerList.ToList();
			
			if (friends != null)
				foreach(var friend in friends.Where(x=> players.Exists(y=> y != null && y.userID == x)))
					result.Add(friend);					
			else
				return null;
			
			return result;
		}				
		
		#endregion
		
    }
}
