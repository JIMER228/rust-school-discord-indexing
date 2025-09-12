using System.Collections.Generic;
using Newtonsoft.Json;
using System.Collections;
using System;
using UnityEngine;

namespace Oxide.Plugins
{
	/* основано на версии 0.4.6 by Tuntenfisch */
    [Info("RemoveAAA", "Nimant", "1.0.2", ResourceId = 1645)]    
    class RemoveAAA : RustPlugin
    {

		#region Variables

		private Dictionary<string, string> Messages = new Dictionary<string, string>()
		{
			{ "missing permission", "У вас нет прав выполнять эту команду!" },
			{ "no exec console", "Данную команду следует выполнять находясь на сервере!" },			
			{ "invalid item", "Не указан или не найден указанный предмет!" },
			{ "couldn't give item", "Невозможно получить предмет!" },
			{ "black listed item", "Предмет запрещён к выдаче!" },
			{ "couldn't find player", "Игрок не найден!" },
			{ "global give error", "Произошла ошибка при выдаче предмета:\n{0}" },
			{ "give self", "выдал себе"},
			{ "give all", "выдал всем"},
			{ "give to", "выдал игроку"}
		};
		
		#endregion
	
        #region Hooks        
		
        private void OnServerInitialized()
        {
            LoadConfigVariables();            
            RegisterPermissions("give", "giveall", "givearm", "giveid", "giveto", "nogivegui");
        }        		
		
        private object OnServerCommand(ConsoleSystem.Arg arg)
        {					
            if (arg == null || arg?.cmd == null) return null;									
			
			try
			{			
				string command = arg?.cmd?.Name;
				var adminPlayer = arg?.Player();

				// give
				if (command.Equals("give"))
				{
					if (adminPlayer == null)
					{
						Puts(Messages["no exec console"]);	
						return false;
					}	
					
					if (!HasPermission(arg, "give"))
					{
						if (adminPlayer != null)                    
							SendToChat(adminPlayer, Messages["missing permission"]);
						
						return false;
					}								
								  
					Item item = ItemManager.CreateByPartialName(arg.GetString(0), 1);
					if (item == null)
					{
						if (adminPlayer != null)                    
							SendToChat(adminPlayer, Messages["invalid item"]);					
						
						return false;
					}
					
					if (configData.ItemBlackList.Contains(item.info.shortname))
					{
						if (adminPlayer != null)                    
							SendToChat(adminPlayer, Messages["black listed item"]);					
						
						item.Remove(0f);
						
						return false;
					}
									
					item.amount = arg.GetInt(1, 1);	
					var amount = item.amount;					
					
					if (!adminPlayer.inventory.GiveItem(item, null))
					{                    
						if (adminPlayer != null)                    
							SendToChat(adminPlayer, Messages["couldn't give item"]);
						
						item.Remove(0f);
						
						return false;
					}
					
					if (!HasPermission(adminPlayer, "nogivegui"))
						adminPlayer.Command("note.inv", new object[] { item.info.itemid, amount });
					
					string message = adminPlayer.displayName + " ("+adminPlayer.userID.ToString()+") "+Messages["give self"]+" "+item.info.displayName.english+" x "+amount.ToString();
					Debug.Log(string.Concat(new object[] { "[RemoveAAA] ", message }));				
					Log("info", GetCurDate(), message);
					
					return false;
				}            

				// givearm
				else if (command.Equals("givearm"))
				{
					if (adminPlayer == null)
					{
						Puts(Messages["no exec console"]);	
						return false;
					}	
					
					if (!HasPermission(arg, "givearm"))
					{
						if (adminPlayer)                    
							SendToChat(adminPlayer, Messages["missing permission"]);
						
						return false;
					}				               

					Item item = ItemManager.CreateByItemID(arg.GetInt(0), 1, 0);					
					if (item == null)
					{
						if (adminPlayer)                    
							SendToChat(adminPlayer, Messages["invalid item"]);
						
						return false;
					}
					
					if (configData.ItemBlackList.Contains(item.info.shortname))
					{
						if (adminPlayer)                    
							SendToChat(adminPlayer, Messages["black listed item"]);
						
						item.Remove(0f);
						
						return false;
					}
					
					item.amount = arg.GetInt(1, 1);
					var amount = item.amount;	

					if (!adminPlayer.inventory.GiveItem(item, adminPlayer.inventory.containerBelt))
					{                    
						if (adminPlayer)                    
							SendToChat(adminPlayer, Messages["couldn't give item"]);
						
						item.Remove(0f);
						
						return false;
					}
					
					if (!HasPermission(adminPlayer, "nogivegui"))
						adminPlayer.Command("note.inv", new object[] { item.info.itemid, amount });
					
					string message = adminPlayer.displayName + " ("+adminPlayer.userID.ToString()+") "+Messages["give self"]+" "+item.info.displayName.english+" x "+amount.ToString();
					Debug.Log(string.Concat(new object[] { "[RemoveAAA] ", message }));				
					Log("info", GetCurDate(), message);
					
					return false;
				}            

				// giveid
				else if (command.Equals("giveid"))
				{
					if (adminPlayer == null)
					{
						Puts(Messages["no exec console"]);	
						return false;
					}	
					
					if (!HasPermission(arg, "giveid"))
					{
						if (adminPlayer)                    
							SendToChat(adminPlayer, Messages["missing permission"]);
						
						return false;
					}                

					Item item = ItemManager.CreateByItemID(arg.GetInt(0), 1, 0);					
					if (item == null)
					{
						if (adminPlayer)                    
							SendToChat(adminPlayer, Messages["invalid item"]);
						
						return false;
					}
					
					if (configData.ItemBlackList.Contains(item.info.shortname))
					{
						if (adminPlayer)                    
							SendToChat(adminPlayer, Messages["black listed item"]);
						
						item.Remove(0f);
						
						return false;
					}
					
					item.amount = arg.GetInt(1, 1);
					var amount = item.amount;

					if (!adminPlayer.inventory.GiveItem(item, null))
					{                    
						if (adminPlayer)                    
							SendToChat(adminPlayer, Messages["couldn't give item"]);
						
						item.Remove(0f);
						
						return false;
					}
					
					if (!HasPermission(adminPlayer, "nogivegui"))
						adminPlayer.Command("note.inv", new object[] { item.info.itemid, amount });
					
					string message = adminPlayer.displayName + " ("+adminPlayer.userID.ToString()+") "+Messages["give self"]+" "+item.info.displayName.english+" x "+amount.ToString();
					Debug.Log(string.Concat(new object[] { "[RemoveAAA] ", message }));
					Log("info", GetCurDate(), message);
					
					return false;
				}

				// giveall
				else if (command.Equals("giveall"))
				{
					if (!HasPermission(arg, "giveall"))
					{
						if (adminPlayer != null)                    
							SendToChat(adminPlayer, Messages["missing permission"]);
						
						return false;
					}
					
					string itemName = "";
					int itemAmount = 0;
					
					foreach (BasePlayer player in BasePlayer.activePlayerList)
					{
						Item item = ItemManager.CreateByPartialName(arg.GetString(0), 1);
						if (item != null)
						{
							if (!configData.ItemBlackList.Contains(item.info.shortname))
							{
								item.amount = arg.GetInt(1, 1);
								var amount = item.amount;	
								
								if (!player.inventory.GiveItem(item, null))                            
									item.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity, new Quaternion());										                                                                                                                           
								
								if (!HasPermission(player, "nogivegui"))
									player.Command("note.inv", new object[] { item.info.itemid, amount });																
									
								itemName = item.info.displayName.english;
								itemAmount = amount;
							}
							else
							{
								if (adminPlayer != null)                            
									SendToChat(adminPlayer, Messages["black listed item"]);
								else
									Puts(Messages["black listed item"]);	
								
								item.Remove(0f);
								
								return false;
							}
						}
						else
						{
							if (adminPlayer != null)                        
								SendToChat(adminPlayer, Messages["invalid item"]);
							else
								Puts(Messages["invalid item"]);	
							
							return false;
						}
					}
					
					string message = "";
					
					if (adminPlayer != null)       								
						message = adminPlayer.displayName + " ("+adminPlayer.userID.ToString()+") "+Messages["give all"]+" "+itemName+" x "+itemAmount.ToString();																	
					else								
						message = "SERVER "+Messages["give all"]+" "+itemName+" x "+itemAmount.ToString();								
					
					Debug.Log(string.Concat(new object[] { "[RemoveAAA] ", message }));
					Log("info", GetCurDate(), message);
				
					return false;								
				}
				
				// giveto
				else if (command.Equals("giveto"))
				{
					if (!HasPermission(arg, "giveto"))
					{
						if (adminPlayer)                    
							SendToChat(adminPlayer, Messages["missing permission"]);
						
						return false;
					}
					
					BasePlayer target = BasePlayer.Find(arg.GetString(0));
					if (target == null)
					{
						if (adminPlayer)                    
							SendToChat(adminPlayer, Messages["couldn't find player"]);
						
						return false;
					}

					Item item = ItemManager.CreateByPartialName(arg.GetString(1), 1);
					if (item == null)
					{
						if (adminPlayer)                    
							SendToChat(adminPlayer, Messages["invalid item"]);
						
						return false;
					}
					
					if (configData.ItemBlackList.Contains(item.info.shortname))
					{
						if (adminPlayer)                    
							SendToChat(adminPlayer, Messages["black listed item"]);
						
						item.Remove(0f);
						
						return false;
					}
					
					item.amount = arg.GetInt(2, 1);
					var amount = item.amount;	

					if (!target.inventory.GiveItem(item, null))
					{                    
						if (adminPlayer)                    
							SendToChat(adminPlayer, Messages["couldn't give item"]);
					 
						item.Remove(0f);
					 
						return false;
					}
					
					if (!HasPermission(target, "nogivegui"))
						target.Command("note.inv", new object[] { item.info.itemid, amount });
					
					string message = "";
					
					if (adminPlayer != null)       								
						message = adminPlayer.displayName + " ("+adminPlayer.userID.ToString()+") "+Messages["give to"]+" "+target.displayName + " ("+target.userID.ToString()+") "+item.info.displayName.english+" x "+amount.ToString();
					else								
						message = "SERVER "+Messages["give to"]+" "+target.displayName + " ("+target.userID.ToString()+") "+item.info.displayName.english+" x "+amount.ToString();
										
					Debug.Log(string.Concat(new object[] { "[RemoveAAA] ", message }));
					Log("info", GetCurDate(), message);
					
					return false;
				}			
			}
			catch (Exception e)
			{
				PrintError(string.Format(Messages["global give error"], e.Message));
				return false;
			}
			
            return null;
        }
		
        #endregion        

        #region Permissions        
		
        private void RegisterPermissions(params string[] permissions)
        {
            foreach (string permission in permissions)            
                this.permission.RegisterPermission(Title.ToLower() + "." + permission, this);            
        }
        
        private bool HasPermission(ConsoleSystem.Arg arg, string permission)
        {			
            if (arg?.cmd?.ServerAdmin == true && arg?.Connection == null) return true;
			if (arg?.Connection != null && arg?.Player()?.IsAdmin == true ) return true;
            if (arg?.Connection != null) return this.permission.UserHasPermission(arg?.Connection?.userid.ToString(), Title.ToLower() + "." + permission);
            
			return false;
        }
		
		private bool HasPermission(BasePlayer player, string perm)
        {
			if (player == null) return false;
            return this.permission.UserHasPermission(player.UserIDString, Title.ToLower() + "." + perm);            			
        }
		
        #endregion
		
		#region Common				
		
		private string GetCurDate() 
		{
			return "["+DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")+"] ";
		}
		
		private void Log(string prefix, string date, string text, int tryCount = 600, string Message = "")
		{
			if (tryCount <= 0)
			{
				PrintError(string.Format("Ошибка записи в файл {0}:\n{1}", "Logger_"+prefix+".txt", Message));
				return;
			}	
			
			try
			{		
				LogToFile(prefix, date + text, this, false);
			}
			catch (Exception e)
			{								
				int count = tryCount - 1;
				timer.Once(0.5f, ()=> Log(prefix, date, text, count, e.Message));
			}
		}	
		
		private void SendToChat(BasePlayer player, string message)
		{
			if (configData.EnableChatMessages)
				SendReply(player, message);
		}
		
		#endregion
		
		#region Config      
		
        private ConfigData configData;
		
        private class ConfigData
        {
			public List<string> ItemBlackList;
			public bool EnableChatMessages;
        }		        
		
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
				ItemBlackList = new List<string>()   
				{
                    "flare",
                    "generator.wind.scrap"
                },
				EnableChatMessages = true				
            };
            SaveConfig(config);
        }
		
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();								
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);				
		
        #endregion
		
    }
}