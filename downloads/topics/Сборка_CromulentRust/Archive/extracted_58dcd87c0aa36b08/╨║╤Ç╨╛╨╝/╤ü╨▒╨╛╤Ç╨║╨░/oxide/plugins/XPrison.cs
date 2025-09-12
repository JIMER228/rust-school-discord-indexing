using Newtonsoft.Json;
using System.Linq;
using Oxide.Core.Libraries.Covalence;
using System;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using ConVar;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
		   		 		  						  	   		  	 	 		  	  			  			 		  				
namespace Oxide.Plugins 
{ 
    [Info("XPrison", "Monster", "1.0.4")]
    class XPrison : RustPlugin
    {
		
		[ConsoleCommand("zona_c")]
		private void ccmdCategory(ConsoleSystem.Arg args)
		{
			BasePlayer player = args.Player();
			
			if(player != null && (player.IsAdmin || permission.UserHasPermission(player.UserIDString, "xprison.admin")))
			{
				if(Cooldowns.ContainsKey(player))
					if(Cooldowns[player].Subtract(DateTime.Now).TotalSeconds >= 0) return;
			
				Effect x = new Effect("assets/bundled/prefabs/fx/notice/loot.drag.grab.fx.prefab", player, 0, new Vector3(), new Vector3());
			
				switch(args.Args[0])
				{
					case "category":
					{
						int page = Convert.ToInt32(args.Args[2]);
					
						CuiHelper.DestroyUi(player, ".GUI_ADD");
						CuiHelper.DestroyUi(player, ".GUI_REMOVE");
						CuiHelper.DestroyUi(player, ".ZONA_ONLINE");
						CuiHelper.DestroyUi(player, ".ZONA_OFFLINE");
						CuiHelper.DestroyUi(player, ".ZONA_PRISONER");
					
						switch(args.Args[1])
						{
							case "online":
							{
								CategoryGUI(player, page);
								OnlineGUI(player);
								break;
							}				
							case "offline":
							{
								CategoryGUI(player, page);
								OfflineGUI(player);
								break;
							}				
							case "prisoner":
							{
								CategoryGUI(player, page);
								PrisonerGUI(player);
								break;
							}
						}
					
						break;
					}
					case "prisoner":
					{
						ulong steamID = Convert.ToUInt64(args.Args[2]);
					
						switch(args.Args[1])
						{
							case "add":
							{
								_adminsPrisonerCreate[player.userID] = new PrisonerCreate { SteamID = steamID, Life = false, Duration = 3600, Sentence = false, Reason = "Text reason" };
								AddPrisonerGUI(player);
								break;
							}
							case "remove":
							{
								RemovePrisonerGUI(player, steamID);
								break;
							}
						}
					
						break;
					}				
					case "prisoner_A":
					{
						switch(args.Args[1])
						{
							case "add":
							{
								string arg = args.GetString(2);
								
								if(_adminsPrisonerCreate.ContainsKey(player.userID))
								{
									switch(arg)
									{
										case "steamID":
										{
											ulong.TryParse(args.GetString(3), out ulong steamID);
											_adminsPrisonerCreate[player.userID].SteamID = steamID;
											
											break;
										}
										case "life":
										{
											_adminsPrisonerCreate[player.userID].Life = args.GetBool(3);
										
											break;
										}
										case "duration":
										{
											_adminsPrisonerCreate[player.userID].Duration = args.GetInt(3);
											
											break;
										}
										case "sentence":
										{
											_adminsPrisonerCreate[player.userID].Sentence = args.GetBool(3);
											
											break;
										}
										case "reason":
										{
											_adminsPrisonerCreate[player.userID].Reason = string.Join(" ", args.Args.Skip(3));
											
											break;
										}
									}
									
									PrisonerCreate create = _adminsPrisonerCreate[player.userID];
									AddPrisonerGUI(player);
								}
								break;
							}
							case "remove":
							{
								ulong steamID = 0;
							
								foreach(string arg in args.Args)
									if(arg.StartsWith("STEAMID:"))
										if(arg == "STEAMID:")
											ulong.TryParse(args.Args.Last(), out steamID);
										else
											ulong.TryParse(arg.Replace("STEAMID:", ""), out steamID);
								
								RemovePrisonerGUI(player, steamID);
								break;
							}
						}
					
						break;
					}
				}
			
				EffectNetwork.Send(x, player.Connection);
				Cooldowns[player] = DateTime.Now.AddSeconds(0.3f);
			}
		}
		
		private object OnPlayerRespawn(BasePlayer player)
		{
			if(API_IsOnlinePrisoner(player.userID))
			{
				NextTick(() => GiveAttire(player));
				
				if(_spawnpoints.Count == 0)
				{	
					PrintWarning(LanguageEnglish ? $"No spawn points found! Prisoner [ {player} ] be out!" : $"Не найдено точек для спавна! Заключенный [ {player} ] на свободе!");
					return null;
				}
				
				return new BasePlayer.SpawnPoint
				{
					pos = _spawnpoints.GetRandom()
				};
			}
			else
				return null;
		}
		
		private object OnPlayerCommand(BasePlayer player, string command, string[] args)
		{
			if(player.IsAdmin || permission.UserHasPermission(player.UserIDString, "xprison.admin"))
				return null;
			if(!config.Setting.ChatCommand.Contains(command) && API_IsOnlinePrisoner(player.userID))
			{
				Player.Reply(player, $"[ <color=#b9de0f>ZONA</color> ] - {lang.GetMessage("CMD_BLOCK", this, player.UserIDString)}", config.Setting.SteamID);
				
				return true;
			}	
			
			return null;
		}
		
		private void SaveData(BasePlayer player) => Interface.Oxide.DataFileSystem.WriteObject($"XDataSystem/XPrison/{player.userID}", StoredData[player.userID]);
		
		[ChatCommand("zona")]
		private void cmdInfoZona(BasePlayer player)
		{
			if(StoredData.ContainsKey(player.userID))
			{
				var data = StoredData[player.userID];
				
				if(data.Life)
					Player.Reply(player, lang.GetMessage("TERM_FORLIFE_INFO", this, player.UserIDString), config.Setting.SteamID);
				else
				{
					TimeSpan time = TimeSpan.FromSeconds(data.Sentence ? data.Time : data.Time - GetUnixTime());
					
					Player.Reply(player, string.Format(lang.GetMessage("TERM_DURATION_INFO", this, player.UserIDString), time.Days, time.Hours, time.Minutes, time.Seconds), config.Setting.SteamID);
				}
			}
			else
				Player.Reply(player, lang.GetMessage("NOT_PRISONER_INFO", this, player.UserIDString), config.Setting.SteamID);
		}
		
				
				
		private class Prisoner
		{
			[JsonProperty(LanguageEnglish ? "Prefix in chat" : "Префикс в чате")] public string PrefixChat;
			[JsonProperty(LanguageEnglish ? "For life" : "Пожизненно")] public bool Life;
			[JsonProperty(LanguageEnglish ? "Prisoner" : "Заключенный")] public bool IsPrisoner;
			[JsonProperty(LanguageEnglish ? "Release date" : "Дата освобождения")] public int Time;
			[JsonProperty(LanguageEnglish ? "Serving the sentence - [ True - online | False - by date ]" : "Отбывание наказания - [ True - в онлайне | False - по дату ]")] public bool Sentence;
			[JsonProperty(LanguageEnglish ? "Reason" : "Причина")] public string Reason;
			
			public Prisoner(string prefixchat, bool life, bool isprisoner, int time, bool sentence, string reason)
			{
				PrefixChat = prefixchat; Life = life; IsPrisoner = isprisoner; Time = time; Sentence = sentence; Reason = reason;
			}
		}
		protected override void LoadDefaultConfig() => config = PrisonConfig.GetNewConfiguration();
		
		private void API_RemoveOnlinePrisoner(BasePlayer prisoner)
		{
			if(!API_IsOnlinePrisoner(prisoner.userID))
			{
				PrintWarning(LanguageEnglish ? $"[ {prisoner} ] - is not a prisoner!" : $"[ {prisoner} ] - не является заключенным!");
				return;
			}
			
			if(config.Prisoner.TpOfPrison)
				NextTick(() =>
				{
					if(config.Prisoner.TpOfPrisonInvClear)
						prisoner.inventory.Strip();
					
					TP(prisoner, _safezonepoints.GetRandom());
				});
			else
				prisoner.Hurt(1000);
			
			StoredData[prisoner.userID] = new Prisoner("", false, false, 0, false, "");
			SaveData(prisoner);
			StoredData.Remove(prisoner.userID);
			StoredData_Time.Remove(prisoner.userID);
			
			LockUnlockPrisonerInventory(prisoner, false);
			
			foreach(BasePlayer player in BasePlayer.activePlayerList)
				Player.Reply(player, string.Format(lang.GetMessage("RELEASE_PRISON_BROADCAST", this, player.UserIDString), prisoner.displayName), config.Setting.SteamID);
			PrintWarning(LanguageEnglish ? $"[ {prisoner} ] - released!" : $"[ {prisoner} ] - освобожден!");
			
			if(config.Setting.UseDiscordLogs && DiscordMessages) DiscordMessages.Call("API_SendTextMessage", config.Setting.WebHook, LanguageEnglish ? $"``[ Online ] [ {prisoner} ] - released!``" : $"``[ Online ] [ {prisoner} ] - освобожден!``");
			
			CuiHelper.DestroyUi(prisoner, ".GUI_BLOCK");
		}
		
				
				
		private void ZonaGUI(BasePlayer player)
		{
            CuiElementContainer container = new CuiElementContainer(); 
			
			container.Add(new CuiPanel
            {
				CursorEnabled = true,
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-507.5 -260", OffsetMax = "507.5 290" },
                Image = { Color = "0.517 0.521 0.509 0.95031577", Material = "assets/icons/greyout.mat" }
            }, "Overlay", ".GUI_ZONA", ".GUI_ZONA");
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" },
                Image = { Color = "0.217 0.221 0.209 0.95031577" }
            }, ".GUI_ZONA", ".ZONA_GUI");
			
			container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "470 237.5", OffsetMax = "497.5 265" },
                Button = { Color = "1 1 1 0.75031577", Sprite = "assets/icons/close.png", Close = ".GUI_ZONA" },
                Text = { Text = "" }
            }, ".ZONA_GUI");
			
			container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-502.5 237.5", OffsetMax = "455 265" },
                Text = { Text = lang.GetMessage("TITLE", this, player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 18, Color = "1 1 1 0.75" }
            }, ".ZONA_GUI");
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-507.5 227.5", OffsetMax = "507.5 232.5" },
                Image = { Color = "0.517 0.521 0.509 0.95031577", Material = "assets/icons/greyout.mat" }
            }, ".ZONA_GUI");				
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-507.5 177.5", OffsetMax = "507.5 182.5" },
                Image = { Color = "0.517 0.521 0.509 0.95031577", Material = "assets/icons/greyout.mat" }
            }, ".ZONA_GUI");			
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "460 227.5", OffsetMax = "465 275" },
                Image = { Color = "0.517 0.521 0.509 0.95031577", Material = "assets/icons/greyout.mat" }
            }, ".ZONA_GUI"); 
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 36.75", OffsetMax = "0 41.75" },
                Image = { Color = "0.517 0.521 0.509 0.95031577", Material = "assets/icons/greyout.mat" }
            }, ".ZONA_GUI");

			container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "7.5 7.5", OffsetMax = "29.5 29.5" },
                Button = { Color = "1 1 1 0.75031577", Sprite = "assets/icons/add.png", Command = "zona_cmd add" },
                Text = { Text = "" }
            }, ".ZONA_GUI");			
			
			container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "39.5 7.5", OffsetMax = "61.5 29.5" },
                Button = { Color = "1 1 1 0.75031577", Sprite = "assets/icons/subtract.png", Command = "zona_cmd remove" },
                Text = { Text = "" }
            }, ".ZONA_GUI");

			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "69 0", OffsetMax = "74 37" },
                Image = { Color = "0.517 0.521 0.509 0.95031577", Material = "assets/icons/greyout.mat" }
            }, ".ZONA_GUI");
			
			container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "79 5", OffsetMax = "106 32" },
                Button = { Color = "1 1 1 0.75031577", Sprite = "assets/icons/home.png", Command = "chat.say /tpzona" },
                Text = { Text = "" }
            }, ".ZONA_GUI");
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "111 0", OffsetMax = "116 37" },
                Image = { Color = "0.517 0.521 0.509 0.95031577", Material = "assets/icons/greyout.mat" }
            }, ".ZONA_GUI");
			
			CuiHelper.AddUi(player, container);
			CategoryGUI(player, -1);
		}
		
		[ChatCommand("zonamenu")]
		private void cmdOpenGUIZona(BasePlayer player)
		{
			if(player.IsAdmin || permission.UserHasPermission(player.UserIDString, "xprison.admin"))
				ZonaGUI(player);
		}
		
		private void OnPlayerConnected(BasePlayer player)
		{
			if(player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player));
                return;
            }		
			
			LoadData(player);
		}  
		
		private void AddPrisonerGUI(BasePlayer player)
		{
			CuiElementContainer container = new CuiElementContainer();
			
			PrisonerCreate create = _adminsPrisonerCreate[player.userID];
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-275 -106.25", OffsetMax = "275 53.75" },
                Image = { Color = "0.517 0.521 0.509 1", Material = "assets/icons/greyout.mat" }
            }, ".ZONA_GUI", ".GUI_ADD", ".GUI_ADD");
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" },
                Image = { Color = "0.217 0.221 0.209 0.95" }
            }, ".GUI_ADD", ".ADD_GUI");	

						
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-265 20", OffsetMax = "-95 45" },
                Image = { Color = "0.217 0.221 0.209 0.95" }
            }, ".ADD_GUI", ".BUTTON_STEAMID");
			
			container.Add(new CuiElement
            {
                Parent = ".BUTTON_STEAMID",
                Components =
                {
                    new CuiInputFieldComponent { Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 15, Command = $"zona_c prisoner_A add steamID", CharsLimit = 17, NeedsKeyboard = true, Text = $"{create.SteamID}" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                }
            });
			
			container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-265 0", OffsetMax = "-95 20" },
                Text = { Text = "SteamID:", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "1 1 1 0.75" }
            }, ".ADD_GUI");
			
						
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "95 20", OffsetMax = "265 45" },
                Image = { Color = "0.217 0.221 0.209 0.95" }
            }, ".ADD_GUI", ".BUTTON_DURATION");			
			
			container.Add(new CuiElement
            {
                Parent = ".BUTTON_DURATION",
                Components =
                {
                    new CuiInputFieldComponent { Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 15, Command = $"zona_c prisoner_A add duration", CharsLimit = 10, NeedsKeyboard = true, Text = $"{create.Duration}" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                }
            });
			
			container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "95 0", OffsetMax = "265 20" },
                Text = { Text = lang.GetMessage("TERM", this, player.UserIDString), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "1 1 1 0.75" }
            }, ".ADD_GUI");
			
						
			int count = config.Setting.TimeList.Count;
			
			foreach(var time in config.Setting.TimeList)
			{
				double offset = -(13 * count--) + -(1.35 * count--);
				
				container.Add(new CuiButton
				{
					RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{offset} 15", OffsetMax = $"{offset + 26} 35" },
					Button = { Color = "0.35 0.35 0.35 1", Command = $"zona_c prisoner_A add duration {time.Value}" },
					Text = { Text = time.Key, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 10 }
				}, ".BUTTON_DURATION");
			}
			
			container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-70 20", OffsetMax = "-20 70" },
                Button = { Color = create.Life ? "0.53 0.77 0.35 0.8" : "1 0.4 0.35 0.8", Command = $"zona_c prisoner_A add life {!create.Life}" },
                Text = { Text = "" }
            }, ".ADD_GUI");	

			container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-80 0", OffsetMax = "-10 20" },
                Text = { Text = lang.GetMessage("FORLIFE", this, player.UserIDString), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "1 1 1 0.75" }
            }, ".ADD_GUI");			
			
			container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "20 20", OffsetMax = "70 70" },
                Button = { Color = create.Sentence ? "0.53 0.77 0.35 0.8" : "1 0.4 0.35 0.8", Command = $"zona_c prisoner_A add sentence {!create.Sentence}" },
                Text = { Text = "" }
            }, ".ADD_GUI");	
		   		 		  						  	   		  	 	 		  	  			  			 		  				
			container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "10 0", OffsetMax = "80 20" },
                Text = { Text = lang.GetMessage("FORONLINE", this, player.UserIDString), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "1 1 1 0.75" }
            }, ".ADD_GUI");	
		   		 		  						  	   		  	 	 		  	  			  			 		  				
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.5", AnchorMax = "1 0.5", OffsetMin = "5 -40", OffsetMax = "-5 0" },
                Image = { Color = "0.217 0.221 0.209 0.95" }
            }, ".ADD_GUI", ".BUTTON_REASON");			
			
			container.Add(new CuiElement
            {
                Parent = ".BUTTON_REASON",
                Components =
                {
                    new CuiInputFieldComponent { Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 15, Command = $"zona_c prisoner_A add reason", NeedsKeyboard = true, Text = create.Reason },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                }
            });			
			
			container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-75 -70", OffsetMax = "75 -45" },
                Button = { Color = "0.53 0.77 0.35 0.8", Command = $"cmd_prisoner add {create.SteamID} {create.Life} {create.Duration} {create.Sentence} {create.Reason}", Close = ".GUI_ADD" },
                Text = { Text = lang.GetMessage("TO_PRISON", this, player.UserIDString), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12 }
            }, ".ADD_GUI");
			
			CuiHelper.AddUi(player, container);
		}
		
		private void API_AddOfflinePrisoner(ulong userID, bool life = false, int duration = 0, bool sentence = false, string reason = "")
		{
			if(API_IsOfflinePrisoner(userID))
			{
				PrintWarning(LanguageEnglish ? $"[ {userID} ] - is already a prisoner!" : $"[ {userID} ] - уже является заключенным!");
				return;
			}
			
			Prisoner Data = new Prisoner(config.Prisoner.NamePrisoner.GetRandom(), life, true, life ? 0 : sentence ? duration : duration + GetUnixTime(), sentence, reason);
			
			PrintWarning(LanguageEnglish ? $"[ {userID} ] - went to prison!" : $"[ {userID} ] - отправился на зону!");
			Interface.Oxide.DataFileSystem.WriteObject($"XDataSystem/XPrison/{userID}", Data);
			
			if(config.Setting.UseDiscordLogs && DiscordMessages) DiscordMessages.Call("API_SendTextMessage", config.Setting.WebHook, LanguageEnglish ? $"``[ Offline ] [ {userID} ] - went to prison!``" : $"``[ Offline ] [ {userID} ] - отправился на зону!``");
			
			BasePlayer prisonerSleep = BasePlayer.FindSleeping(userID);
			
			if(prisonerSleep != null)
				LockUnlockPrisonerInventory(prisonerSleep, true);
		}
		
		private void Unload()
		{
			foreach(BasePlayer player in BasePlayer.activePlayerList)
			{
				CuiHelper.DestroyUi(player, ".GUI_ZONA");
				CuiHelper.DestroyUi(player, ".GUI_BLOCK");
				
				if(API_IsOnlinePrisoner(player.userID))
					LockUnlockPrisonerInventory(player, false);
			}
			
			foreach(BasePlayer player in BasePlayer.sleepingPlayerList)
				if(API_IsOfflinePrisoner(player.userID))
					LockUnlockPrisonerInventory(player, false);
			
			config = null;
		}
		
		[ConsoleCommand("zona_cmd")]
		private void ccmdCMD(ConsoleSystem.Arg args)
		{
			BasePlayer player = args.Player();
			
			if(player != null && (player.IsAdmin || permission.UserHasPermission(player.UserIDString, "xprison.admin")))
			{
				Effect x = new Effect("assets/bundled/prefabs/fx/notice/loot.drag.grab.fx.prefab", player, 0, new Vector3(), new Vector3());
			
				switch(args.Args[0])
				{
					case "add":
					{
						_adminsPrisonerCreate[player.userID] = new PrisonerCreate { SteamID = 76561100000000000, Life = false, Duration = 3600, Sentence = false, Reason = "Text reason" };
						AddPrisonerGUI(player);
						break;
					}
					case "remove":
					{
						RemovePrisonerGUI(player);
						break;
					}
				}
			
				EffectNetwork.Send(x, player.Connection);
			}
		}
		
		[ChatCommand("zonaloc")]
		private void cmgZonaLoc(BasePlayer player)
		{
			if(player.IsAdmin || permission.UserHasPermission(player.UserIDString, "xprison.admin"))
				Player.Reply(player, string.Format(lang.GetMessage("ZONA_LOC", this, player.UserIDString), config.Zona.PositionZona, player.transform.position), config.Setting.SteamID);
		}
		
		private void RemovePrisonerGUI(BasePlayer player, ulong steamID = 76561100000000000)
		{
			CuiElementContainer container = new CuiElementContainer();
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-275 -91.25", OffsetMax = "275 38.75" },
                Image = { Color = "0.517 0.521 0.509 1", Material = "assets/icons/greyout.mat" }
            }, ".ZONA_GUI", ".GUI_REMOVE", ".GUI_REMOVE");
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" },
                Image = { Color = "0.217 0.221 0.209 0.95" }
            }, ".GUI_REMOVE", ".REMOVE_GUI");	

						
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-85 5", OffsetMax = "85 30" },
                Image = { Color = "0.217 0.221 0.209 0.95" }
            }, ".REMOVE_GUI", ".BUTTON_STEAMID");
			
			container.Add(new CuiElement
            {
                Parent = ".BUTTON_STEAMID",
                Components =
                {
                    new CuiInputFieldComponent { Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 15, Command = $"zona_c prisoner_A remove STEAMID:", CharsLimit = 17, NeedsKeyboard = true, Text = $"{steamID}" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                }
            });
			
			container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-85 -15", OffsetMax = "85 5" },
                Text = { Text = "SteamID:", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "1 1 1 0.75" }
            }, ".REMOVE_GUI");
			
						
			container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-75 -55", OffsetMax = "75 -30" },
                Button = { Color = "0.53 0.77 0.35 0.8", Command = $"cmd_prisoner remove {steamID} false 0 false", Close = ".GUI_REMOVE" },
                Text = { Text = lang.GetMessage("RELEASE_PRISON", this, player.UserIDString), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12 }
            }, ".REMOVE_GUI");
			
			CuiHelper.AddUi(player, container);
		}
		
		private void OnPluginUnloaded(Plugin name)
		{
			if(name.Title == "Better Chat" || name.Title == "IQChat")
				Subscribe(nameof(OnPlayerChat));
		}
		
		private List<Vector3> _safezonepoints = new List<Vector3>();
		
		private void LockUnlockPrisonerInventory(BasePlayer prisoner, bool locked)
		{
			if(config.Prisoner.BlockWear)
				prisoner.inventory.containerWear.SetLocked(locked);
			if(config.Prisoner.BlockBelt)
				prisoner.inventory.containerBelt.SetLocked(locked);
			if(config.Prisoner.BlockMain)
				prisoner.inventory.containerMain.SetLocked(locked);
		}
		
		private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info) => entity.OwnerID == 100001 ? true : null;
		
		private void CategoryGUI(BasePlayer player, int page = 0)
		{
            CuiElementContainer container = new CuiElementContainer();
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-497.5 187.5", OffsetMax = "497.5 222.5" },
                Image = { Color = "0 0 0 0" }
            }, ".ZONA_GUI", ".ZonaBUTTON", ".ZonaBUTTON");
			
			int x = 0, count = _command.Count; 
			
			foreach(var category in _command)
			{
				string color = page == x ? "0.53 0.77 0.35 0.8" : "0 0 0 0";
				double offset = -(164 * count--) + -(2.5 * count--);
		   		 		  						  	   		  	 	 		  	  			  			 		  				
				container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{offset} -17.5", OffsetMax = $"{offset + 328} 17.5" },
                    Button = { Color = "0.517 0.521 0.509 0.5", Material = "assets/icons/greyout.mat", Command = category.Value + $" {x}" },
                    Text = { Text = lang.GetMessage(category.Key, this, player.UserIDString), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 18, Color = "0.75 0.75 0.75 1" }
                }, ".ZonaBUTTON", ".BUTTON");
 
			    container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMax = "0 1.5" },
                    Image = { Color = color, Material = "assets/icons/greyout.mat" }
                }, ".BUTTON");
				
				x++;
			}
			
			CuiHelper.AddUi(player, container);
		}
		
				
				
		private PrisonConfig config;
		
		private void OfflineGUI(BasePlayer player, int Page = 0)
		{
            CuiElementContainer container = new CuiElementContainer();
			
			List<BasePlayer> offline_players = BasePlayer.sleepingPlayerList.Where(xx => !xx.IsConnected).ToList();
			int x = 0, y = 0;
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-502.5 -228.5", OffsetMax = "502.5 177.5" },
                Image = { Color = "0 0 0 0" }
            }, ".ZONA_GUI", ".ZONA_OFFLINE");
			
			foreach(BasePlayer p in offline_players.Skip(Page * 32))
			{
				container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{-497.5 + (x * 250)} {153 - (y * 50)}", OffsetMax = $"{-252.5 + (x * 250)} {198 - (y * 50)}" },
                    Image = { Color = "0.517 0.521 0.509 0.5", Material = "assets/icons/greyout.mat" }
                }, ".ZONA_OFFLINE", ".PlayerGUI");
				
				container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "45 0", OffsetMax = "-195 0" },
                    Image = { Color = "0.9 0.25 0.25 1", Material = "assets/icons/greyout.mat" }
                }, ".PlayerGUI");	
				
				container.Add(new CuiElement
                {
                    Parent = ".PlayerGUI",
                    Components =
                    {
					    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", p.UserIDString) },
                        new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "2.5 -20", OffsetMax = "42.5 20" }
                    }
                });
				
				container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "52.5 30", OffsetMax = "-22.5 0" },
                    Text = { Text = $"NAME: {p.displayName}", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 10 }
                }, ".PlayerGUI");			    
				
				container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "52.5 17", OffsetMax = "-22.5 -13" },
                    Text = { Text = $"ID: {p.userID}", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 10 }
                }, ".PlayerGUI");
				
				x++;
				
                if(x == 4)
                {
                    x = 0;
                    y++;
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                    if(y == 8)
                        break;
                }
			}
			
			bool back = Page != 0;
			bool next = offline_players.Count > ((Page + 1) * 32);
		   		 		  						  	   		  	 	 		  	  			  			 		  				
			container.Add(new CuiButton 
            {    
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-190 -36.75", OffsetMax = "-100 -10" },
                Button = { Color = back ? "0.65 0.29 0.24 1" : "0.65 0.29 0.24 0.4", Command = back ? $"page.xprison offline back {Page}" : "" },
                Text = { Text = lang.GetMessage("BACK", this, player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-regular.ttf", Color = back ? "0.92 0.79 0.76 1" : "0.92 0.79 0.76 0.4" }
            }, ".ZONA_OFFLINE");				 			
			
			container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-95 -36.75", OffsetMax = "-5 -10" },
                Button = { Color = next ? "0.35 0.45 0.25 1" : "0.35 0.45 0.25 0.4", Command = next ? $"page.xprison offline next {Page}" : "" },
                Text = { Text = lang.GetMessage("NEXT", this, player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-regular.ttf", Color = next ? "0.75 0.95 0.41 1" : "0.75 0.95 0.41 0.4" }
            }, ".ZONA_OFFLINE");
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-200 -46.75", OffsetMax = "-195 0" },
                Image = { Color = "0.517 0.521 0.509 0.95", Material = "assets/icons/greyout.mat" }
            }, ".ZONA_OFFLINE");
			
			CuiHelper.AddUi(player, container);
		}		
		
		private void PrisonerGUI(BasePlayer player, int Page = 0)
		{
            CuiElementContainer container = new CuiElementContainer();
		   		 		  						  	   		  	 	 		  	  			  			 		  				
			int x = 0, y = 0, unixtime = GetUnixTime();
			bool multifighting = MultiFighting;
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-502.5 -228.5", OffsetMax = "502.5 177.5" },
                Image = { Color = "0 0 0 0" }
            }, ".ZONA_GUI", ".ZONA_PRISONER");
			
			foreach(var p in StoredData.Skip(Page * 32))
			{
				BasePlayer prisoner = BasePlayer.FindByID(p.Key);
				
				container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{-497.5 + (x * 250)} {153 - (y * 50)}", OffsetMax = $"{-252.5 + (x * 250)} {198 - (y * 50)}" },
                    Image = { Color = "0.517 0.521 0.509 0.5", Material = "assets/icons/greyout.mat" }
                }, ".ZONA_PRISONER", ".PlayerGUI");
				
				container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "45 0", OffsetMax = "-195 0" },
                    Image = { Color = "0.9 0.53 0 1", Material = "assets/icons/greyout.mat" }
                }, ".PlayerGUI");	
				
				container.Add(new CuiElement
                {
                    Parent = ".PlayerGUI",
                    Components =
                    {
					    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", prisoner.UserIDString) },
                        new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "2.5 -20", OffsetMax = "42.5 20" }
                    }
                });
				
				container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "52.5 30", OffsetMax = "0 0" },
                    Text = { Text = $"NAME: {prisoner.displayName}", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 10 }
                }, ".PlayerGUI");			    
				
				container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "52.5 21.5", OffsetMax = "0 -9" },
                    Text = { Text = $"ID: {prisoner.userID}", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 9 }
                }, ".PlayerGUI");
				
				if(!string.IsNullOrEmpty(p.Value.Reason))
					container.Add(new CuiLabel
					{
						RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "52.5 12.5", OffsetMax = "0 24" },
						Text = { Text = $"- {p.Value.Reason}", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 9, Color = "1 0.8 0.8 1" }
					}, ".PlayerGUI");
		   		 		  						  	   		  	 	 		  	  			  			 		  				
				TimeSpan time = TimeSpan.FromSeconds(p.Value.Sentence ? p.Value.Time : p.Value.Time - unixtime);
				
				container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "52.5 0", OffsetMax = "-22.5 15" },
                    Text = { Text = p.Value.Life ? lang.GetMessage("TERM_FORLIFE_UI", this, player.UserIDString) : string.Format(lang.GetMessage("TERM_DURATION_UI", this, player.UserIDString), time.Days, time.Hours, time.Minutes, time.Seconds), Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 9 }
                }, ".PlayerGUI");		
				
				container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-20 25", OffsetMax = "-2.5 42.5" },
                    Button = { Color = "1 1 1 0.75", Sprite = "assets/icons/subtract.png", Command = $"zona_c prisoner remove {p.Key}" },
                    Text = { Text = "" }
                }, ".PlayerGUI");
				
				if(multifighting)
					container.Add(new CuiButton
					{
						RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-20 2.5", OffsetMax = "-2.5 20" },
						Button = { Color = "1 1 1 0.75", Sprite = (bool)MultiFighting?.CallHook("IsSteam", player.Connection) ? "assets/icons/steam.png" : "assets/icons/warning_2.png" },
						Text = { Text = "" }
					}, ".PlayerGUI");
				
				x++;
				
                if(x == 4)
                {
                    x = 0;
                    y++;
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                    if(y == 8)
                        break;
                }
			}
			
			bool back = Page != 0;
			bool next = StoredData.Count > ((Page + 1) * 32);

			container.Add(new CuiButton 
            {    
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-190 -36.75", OffsetMax = "-100 -10" },
                Button = { Color = back ? "0.65 0.29 0.24 1" : "0.65 0.29 0.24 0.4", Command = back ? $"page.xprison prisoner back {Page}" : "" },
                Text = { Text = lang.GetMessage("BACK", this, player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-regular.ttf", Color = back ? "0.92 0.79 0.76 1" : "0.92 0.79 0.76 0.4" }
            }, ".ZONA_PRISONER");				 			
			
			container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-95 -36.75", OffsetMax = "-5 -10" },
                Button = { Color = next ? "0.35 0.45 0.25 1" : "0.35 0.45 0.25 0.4", Command = next ? $"page.xprison prisoner next {Page}" : "" },
                Text = { Text = lang.GetMessage("NEXT", this, player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-regular.ttf", Color = next ? "0.75 0.95 0.41 1" : "0.75 0.95 0.41 0.4" }
            }, ".ZONA_PRISONER");
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-200 -46.75", OffsetMax = "-195 0" },
                Image = { Color = "0.517 0.521 0.509 0.95", Material = "assets/icons/greyout.mat" }
            }, ".ZONA_PRISONER");
			
			CuiHelper.AddUi(player, container);
		}
		
				
				
		private bool API_IsOnlinePrisoner(ulong userID) => StoredData.ContainsKey(userID);
		private Dictionary<ulong, int> StoredData_Time = new Dictionary<ulong, int>();
		
				
				
		private readonly List<ulong> _netIDsPrefabs = new List<ulong> { 1005787, 1005998, 1015789, 1102356, 1187247, 1236001, 132174607, 18554159, 24101201, 26998405, 288741214 };
		
		private bool IsInvisible(BasePlayer player) => Vanish != null && Convert.ToBoolean(Vanish.Call("IsInvisible", player));
		
		private void CheckPrisoner()
		{
			int time = GetUnixTime();
			
			foreach(var p in StoredData_Time)
			{
				ulong steamID = p.Key;
				var data = StoredData[steamID];
				
				if(data.Sentence)
				{
					data.Time -= 30;
					
					if(data.Time <= 0)
					{
						BasePlayer prisoner = BasePlayer.FindByID(steamID);
					
						if(prisoner != null) 
							NextTick(() => API_RemoveOnlinePrisoner(prisoner));
					}
				}
				else
					if(p.Value < time)
					{
						BasePlayer prisoner = BasePlayer.FindByID(steamID);
					
						if(prisoner != null) 
							NextTick(() => API_RemoveOnlinePrisoner(prisoner));
					}
			}
		}
		
		[ConsoleCommand("page.xprison")]
		private void ccmdPage(ConsoleSystem.Arg args)
		{
			BasePlayer player = args.Player();
			
			if(player != null && (player.IsAdmin || permission.UserHasPermission(player.UserIDString, "xprison.admin")))
			{
				Effect x = new Effect("assets/bundled/prefabs/fx/notice/loot.drag.grab.fx.prefab", player, 0, new Vector3(), new Vector3());
			
				int Page = int.Parse(args.Args[2]);
			
				CuiHelper.DestroyUi(player, ".GUI_ADD");
				CuiHelper.DestroyUi(player, ".GUI_REMOVE");
				CuiHelper.DestroyUi(player, ".ZONA_ONLINE");
				CuiHelper.DestroyUi(player, ".ZONA_OFFLINE");
				CuiHelper.DestroyUi(player, ".ZONA_PRISONER");
			
				switch (args.Args[0])
				{
					case "online":
					{
						switch(args.Args[1])
						{
							case "next":
							{
								OnlineGUI(player, Page + 1);	
								break;
							}						
							case "back":
							{
								OnlineGUI(player, Page - 1);
								break;
							}
						}
						break;
					}				
					case "offline":
					{
						switch(args.Args[1])
						{
							case "next":
							{
								OfflineGUI(player, Page + 1);	
								break;
							}						
							case "back":
							{
								OfflineGUI(player, Page - 1);
								break;
							}
						}
						break;
					}				
					case "prisoner":
					{
						switch(args.Args[1])
						{
							case "next":
							{
								PrisonerGUI(player, Page + 1);	
								break;
							}						
							case "back":
							{
								PrisonerGUI(player, Page - 1);
								break;
							}
						}
						break;
					}
				}
			
				EffectNetwork.Send(x, player.Connection);
			}
		}
		
		private void API_AddOnlinePrisoner(BasePlayer prisoner, bool life = false, int duration = 0, bool sentence = false, string reason = "")
		{
			if(API_IsOnlinePrisoner(prisoner.userID))
			{
				PrintWarning(LanguageEnglish ? $"[ {prisoner} ] - is already a prisoner!" : $"[ {prisoner} ] - уже является заключенным!");
				return;
			}
			
			TimeSpan _time = TimeSpan.FromSeconds(duration);
			int time = sentence ? duration : duration + GetUnixTime();
			
			if(config.Prisoner.TpToPrison)
			{
				if(_spawnpoints.Count != 0)
					NextTick(() =>
					{
						if(config.Prisoner.TpToPrisonInvClear)
						{
							prisoner.inventory.Strip();
							GiveAttire(prisoner);
						}
						
						TP(prisoner, _spawnpoints.GetRandom());
					});
			}
			else
				prisoner.Hurt(1000);
			
			StoredData.Add(prisoner.userID, new Prisoner(config.Prisoner.NamePrisoner.GetRandom(), life, true, life ? 0 : time, sentence, reason));
			
			if(!life && duration > 0)
				StoredData_Time.Add(prisoner.userID, time);
			
			foreach(BasePlayer player in BasePlayer.activePlayerList)
				Player.Reply(player, string.Format(lang.GetMessage("TO_PRISON_BROADCAST", this, player.UserIDString), prisoner.displayName) + (life ? lang.GetMessage("TERM_FORLIFE_BROADCAST", this, player.UserIDString) : string.Format(lang.GetMessage("TERM_DURATION_BROADCAST", this, player.UserIDString), _time.Days, _time.Hours, _time.Minutes, _time.Seconds) + (sentence ? lang.GetMessage("TERM_SENTENCE_ONLINE", this, player.UserIDString) : lang.GetMessage("TERM_SENTENCE_DATE", this, player.UserIDString))), config.Setting.SteamID);
			PrintWarning(LanguageEnglish ? $"[ {prisoner} ] - went to prison!" : $"[ {prisoner} ] - отправился на зону!");
			SaveData(prisoner);
			
			if(config.Setting.UseDiscordLogs && DiscordMessages) DiscordMessages.Call("API_SendTextMessage", config.Setting.WebHook,
			LanguageEnglish ? $"```[ {prisoner} ] - send to prison!" + (string.IsNullOrEmpty(reason) ? "" : $"\n- {reason}") + (life ? "\nTERM: FOR TERM OF LIFE```" : string.Format("\nTERM: {0} D. : {1} HR. : {2} MIN. : {3} SEC.", _time.Days, _time.Hours, _time.Minutes, _time.Seconds) + (sentence ? "\nSERVING THE PENALTY: ONLINE [ NEED TO BE ONLINE ]```" : "\nSERVING THE PENALTY: BY DATE [ CAN BE OFFLINE ]```"))
			: $"```[ {prisoner} ] - отправился на зону!" + (string.IsNullOrEmpty(reason) ? "" : $"\n- {reason}") + (life ? "\nСРОК: ПОЖИЗНЕННО```" : string.Format("\nСРОК: {0} Д. : {1} ЧАС. : {2} МИН. : {3} СЕК.", _time.Days, _time.Hours, _time.Minutes, _time.Seconds) + (sentence ? "\nОТБЫВАНИЕ НАКАЗАНИЯ: В ОНЛАЙНЕ [ НУЖНО БЫТЬ ОНЛАЙН ]```" : "\nОТБЫВАНИЕ НАКАЗАНИЯ: ПО ДАТЕ [ МОЖНО БЫТЬ ОФФЛАЙН ]```")));
			
			if(config.Prisoner.UseBlockImageUI) BlockGUI(prisoner);
		}		
		
				
				
		private void InitializeLang()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TITLE"] = "COOL SERVER ZONA MENU",
				["NEXT"] = "NEXT",
				["BACK"] = "BACK",
				["ONLINE"] = "ONLINE PLAYERS",
				["OFFLINE"] = "OFFLINE PLAYERS/PRISONERS",
				["PRISONER"] = "ONLINE PRISONERS",
				["CMD_BLOCK"] = "It is forbidden to use this command in prison!",
				["SPAWN_ADMIN"] = "No admin spawn point found!",
				["SPAWN_VISITOR"] = "No visitor spawn points found!",
				["TERM_FORLIFE_INFO"] = "You've been jailed life imprisonment!",
				["NOT_PRISONER_INFO"] = "You are not a prisoner!",
				["TERM_DURATION_INFO"] = "You have to sit - [ <color=orange>{0} D. : {1} HR. : {2} MIN. : {3} SEC.</color> ]",
				["CLEAR_INVENTORY"] = "Before visiting the prison, empty your inventory.",
				["FORLIFE"] = "For term of life:",
				["FORONLINE"] = "Online:",
				["TERM"] = "Term: sec.",
				["TO_PRISON"] = "SEND TO PRISON",
				["RELEASE_PRISON"] = "RELEASED FROM PRISON",
				["TERM_FORLIFE_UI"] = "TERM: FOR TERM OF LIFE",
				["TERM_DURATION_UI"] = "TERM: {0} D. : {1} HR. : {2} MIN. : {3} SEC.",
				["RELEASE_PRISON_BROADCAST"] = "<size=18>[ <color=#b9de0f>{0}</color> ]</size> - released from prison!",
				["TO_PRISON_BROADCAST"] = "<size=18>[ <color=#b9de0f>{0}</color> ]</size> - send to prison!",
				["TERM_FORLIFE_BROADCAST"] = "\n<size=12><color=#b9de0f>TERM</color>: <color=orange>FOR TERM OF LIFE</color></size>",
				["TERM_DURATION_BROADCAST"] = "\n<size=12><color=#b9de0f>TERM</color>: <color=orange>{0}</color> D. : <color=orange>{1}</color> HR. : <color=orange>{2}</color> MIN. : <color=orange>{3}</color> SEC.</size>",
				["TERM_SENTENCE_ONLINE"] = "\n<size=12><color=#b9de0f>SERVING THE PENALTY</color>: ONLINE [ NEED TO BE ONLINE ].</size>",
				["TERM_SENTENCE_DATE"] = "\n<size=12><color=#b9de0f>SERVING THE PENALTY</color>: BY DATE [ CAN BE OFFLINE ].</size>",
				["ZONA_LOC"] = "<size=12><color=#b9de0f>Coordinates specified in the config</color> - <color=#c4feff>{0}</color></size>\n<size=12><color=#b9de0f>Your coordinates</color> - <color=#c4feff>{1}</color></size>",
				["BUTTON_PRESS"] = "You have reduced your time in prison by {0} seconds.",
				["ITEM_RECYCLE"] = "Recycled item - <color=#008fbf>{0}</color>\nYou have reduced your time in prison by {1} seconds."
            }, this);
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TITLE"] = "МЕНЮ ЗОНЫ КРУТОГО СЕРВЕРА",
				["NEXT"] = "ДАЛЕЕ",
				["BACK"] = "НАЗАД",
				["ONLINE"] = "ОНЛАЙН ИГРОКИ",
				["OFFLINE"] = "ОФФЛАЙН ИГРОКИ/ЗАКЛЮЧЕННЫЕ",
				["PRISONER"] = "ОНЛАЙН ЗАКЛЮЧЕННЫЕ",
				["CMD_BLOCK"] = "На зоне запрещено использовать данную команду!",
				["SPAWN_ADMIN"] = "Не найдено точки спавна для администрации!",
				["SPAWN_VISITOR"] = "Не найдено точек спавна для посетителей!",
				["TERM_FORLIFE_INFO"] = "Вас посадили пожизненно!",
				["NOT_PRISONER_INFO"] = "Вы не являетесь заключенным!",
				["TERM_DURATION_INFO"] = "Вам осталось сидеть - [ <color=orange>{0} Д. : {1} ЧАС. : {2} МИН. : {3} СЕК.</color> ]",
				["CLEAR_INVENTORY"] = "Прежде чем посетить зону, освободите свой инвентарь.",
				["FORLIFE"] = "Пожизненно:",
				["FORONLINE"] = "В онлайне:",
				["TERM"] = "Срок: сек.",
				["TO_PRISON"] = "ОТПРАВИТЬ НА ЗОНУ",
				["RELEASE_PRISON"] = "ОСВОБОДИТЬ",
				["TERM_FORLIFE_UI"] = "СРОК: ПОЖИЗНЕННО",
				["TERM_DURATION_UI"] = "СРОК: {0} Д. : {1} ЧАС. : {2} МИН. : {3} СЕК.",
				["RELEASE_PRISON_BROADCAST"] = "<size=18>[ <color=#b9de0f>{0}</color> ]</size> - освобожден!",
				["TO_PRISON_BROADCAST"] = "<size=18>[ <color=#b9de0f>{0}</color> ]</size> - отправился на зону!",
				["TERM_FORLIFE_BROADCAST"] = "\n<size=12><color=#b9de0f>СРОК</color>: <color=orange>ПОЖИЗНЕННО</color></size>",
				["TERM_DURATION_BROADCAST"] = "\n<size=12><color=#b9de0f>СРОК</color>: <color=orange>{0}</color> Д. : <color=orange>{1}</color> ЧАС. : <color=orange>{2}</color> МИН. : <color=orange>{3}</color> СЕК.</size>",
				["TERM_SENTENCE_ONLINE"] = "\n<size=12><color=#b9de0f>ОТБЫВАНИЕ НАКАЗАНИЯ</color>: В ОНЛАЙНЕ [ НУЖНО БЫТЬ ОНЛАЙН ].</size>",
				["TERM_SENTENCE_DATE"] = "\n<size=12><color=#b9de0f>ОТБЫВАНИЕ НАКАЗАНИЯ</color>: ПО ДАТЕ [ МОЖНО БЫТЬ ОФФЛАЙН ].</size>",
				["ZONA_LOC"] = "<size=12><color=#b9de0f>Координаты указанные в конфиге</color> - <color=#c4feff>{0}</color></size>\n<size=12><color=#b9de0f>Ваши координаты</color> - <color=#c4feff>{1}</color></size>",
				["BUTTON_PRESS"] = "Вы сократили время пребывания в тюрьме на {0} секунд.",
				["ITEM_RECYCLE"] = "Переработанный предмет - <color=#008fbf>{0}</color>\nВы сократили время пребывания в тюрьме на {1} секунд."
            }, this, "ru");
			
			lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TITLE"] = "МЕНЮ ЗОНИ КРУТОГО СЕРВЕРУ",
				["NEXT"] = "ДАЛІ",
				["BACK"] = "НАЗАД",
				["ONLINE"] = "ОНЛАЙН ГРАВЦІ",
				["OFFLINE"] = "ОФФЛАЙН ГРАВЦІ/УВ'ЯЗНЕНІ",
				["PRISONER"] = "ОНЛАЙН УВ'ЯЗНЕНІ",
				["CMD_BLOCK"] = "На зоні заборонено використовувати цю команду!",
				["SPAWN_ADMIN"] = "Не знайдено точки спавна для адміністрації!",
				["SPAWN_VISITOR"] = "Не знайдено точки спавна для відвідувачів!",
				["TERM_FORLIFE_INFO"] = "Вас посадили довічно!",
				["NOT_PRISONER_INFO"] = "Ви не є ув'язненим!",
				["TERM_DURATION_INFO"] = "Вам залишилося сидіти - [ <color=orange>{0} Д. : {1} ГОД. : {2} ХВ. : {3} СЕК.</color> ]",
				["CLEAR_INVENTORY"] = "Перш ніж відвідати зону, звільніть свій інвентар.",
				["FORLIFE"] = "Довічно:",
				["FORONLINE"] = "В онлайні:",
				["TERM"] = "Термін: сек.",
				["TO_PRISON"] = "ВІДПРАВИТИ НА ЗОНУ",
				["RELEASE_PRISON"] = "ЗВІЛЬНИТИ",
				["TERM_FORLIFE_UI"] = "ТЕРМІН: ДОВІЧНО",
				["TERM_DURATION_UI"] = "ТЕРМІН: {0} Д. : {1} ГОД. : {2} ХВ. : {3} СЕК.",
				["RELEASE_PRISON_BROADCAST"] = "<size=18>[ <color=#b9de0f>{0}</color> ]</size> - звільнений!",
				["TO_PRISON_BROADCAST"] = "<size=18>[ <color=#b9de0f>{0}</color> ]</size> - відправлений на зону!",
				["TERM_FORLIFE_BROADCAST"] = "\n<size=12><color=#b9de0f>ТЕРМІН</color>: <color=orange>ДОВІЧНО</color></size>",
				["TERM_DURATION_BROADCAST"] = "\n<size=12><color=#b9de0f>ТЕРМІН</color>: <color=orange>{0}</color> Д. : <color=orange>{1}</color> ГОД. : <color=orange>{2}</color> ХВ. : <color=orange>{3}</color> СЕК.</size>",
				["TERM_SENTENCE_ONLINE"] = "\n<size=12><color=#b9de0f>ВІДБУВАННЯ ПОКАРАННЯ</color>: В ОНЛАЙНІ [ ТРЕБА БУТИ ОНЛАЙН ].</size>",
				["TERM_SENTENCE_DATE"] = "\n<size=12><color=#b9de0f>ВІДБУВАННЯ ПОКАРАННЯ</color>: ПО ДАТІ [ МОЖНА БУТИ ОФФЛАЙН ].</size>",
				["ZONA_LOC"] = "<size=12><color=#b9de0f>Координати вказані у конфігі</color> - <color=#c4feff>{0}</color></size>\n<size=12><color=#b9de0f>Ваші координати</color> - <color=#c4feff>{1}</color></size>",
				["BUTTON_PRESS"] = "Ви скоротили час перебування у в'язниці на {0} секунд.",
				["ITEM_RECYCLE"] = "Перероблений предмет - <color=#008fbf>{0}</color>\nВи скоротили час перебування у в'язниці на {1} секунд."
            }, this, "uk");
			
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TITLE"] = "MENÚ ZONA SERVIDOR",
				["NEXT"] = "MÁS",
				["BACK"] = "ATRÁS",
				["ONLINE"] = "JUGADORES EN LÍNEA",
				["OFFLINE"] = "JUGADORES SIN CONEXIÓN/PRISIONEROS",
				["PRISONER"] = "PRISIONEROS EN LÍNEA",
				["CMD_BLOCK"] = "¡Está prohibido usar este comando en la zona!",
				["SPAWN_ADMIN"] = "¡No se encontró ningún punto de generación de administrador!",
				["SPAWN_VISITOR"] = "¡No se encontraron puntos de generación de visitantes!",
				["TERM_FORLIFE_INFO"] = "¡Has sido encarcelado de por vida!",
				["NOT_PRISONER_INFO"] = "¡No eres un prisionero!",
				["TERM_DURATION_INFO"] = "Tienes que sentarte - [ <color=orange>{0} D. : {1} HORA. : {2} MINUTOS. : {3} SEG.</color> ]",
				["CLEAR_INVENTORY"] = "Antes de visitar la zona, vacía tu inventario.",
				["FORLIFE"] = "Por vida:",
				["FORONLINE"] = "En línea:",
				["TERM"] = "Término: seg.",
				["TO_PRISON"] = "ENVIAR A ZONA",
				["RELEASE_PRISON"] = "LIBERAR",
				["TERM_FORLIFE_UI"] = "TÉRMINO: DE POR VIDA",
				["TERM_DURATION_UI"] = "TÉRMINO: {0} D. : {1} HORA. : {2} MINUTOS. : {3} SEG.",
				["RELEASE_PRISON_BROADCAST"] = "<size=18>[ <color=#b9de0f>{0}</color> ]</size> - liberado!",
				["TO_PRISON_BROADCAST"] = "<size=18>[ <color=#b9de0f>{0}</color> ]</size> - fue a la zona!",
				["TERM_FORLIFE_BROADCAST"] = "\n<size=12><color=#b9de0f>TÉRMINO</color>: <color=orange>POR VIDA</color></size>",
				["TERM_DURATION_BROADCAST"] = "\n<size=12><color=#b9de0f>TÉRMINO</color>: <color=orange>{0}</color> D. : <color=orange>{1}</color> HORA. : <color=orange>{2}</color> MINUTOS. : <color=orange>{3}</color> SEG.</size>",
				["TERM_SENTENCE_ONLINE"] = "\n<size=12><color=#b9de0f>CUMPLIENDO LA PENA</color>: EN LÍNEA [ NECESITA ESTAR EN LÍNEA ].</size>",
				["TERM_SENTENCE_DATE"] = "\n<size=12><color=#b9de0f>CUMPLIENDO LA PENA</color>: POR FECHA [ PUEDE ESTAR SIN CONEXIÓN ].</size>",
				["ZONA_LOC"] = "<size=12><color=#b9de0f>Coordenadas especificadas en la configuración</color> - <color=#c4feff>{0}</color></size>\n<size=12><color=#b9de0f>Tus coordenadas</color> - <color=#c4feff>{1}</color></size>",
				["BUTTON_PRESS"] = "Has reducido tu tiempo de prisión en {0} segundos.",
				["ITEM_RECYCLE"] = "Artículo reciclado - <color=#008fbf>{0}</color>\nHas reducido tu tiempo de prisión en {1} segundos."
            }, this, "es-ES");
        }
		
		private void OnBoomBox()
		{
			foreach(var boombox in _zonaentity.OfType<DeployableBoomBox>())
			{
				boombox.BoxController.ServerTogglePlay(false);
				timer.Once(1, () => boombox.BoxController.ServerTogglePlay(true));
			}
		}
		
		private object OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
		{
			if(API_IsOnlinePrisoner(player.userID))
			{
				if(channel == ConVar.Chat.ChatChannel.Team)
					return null;
				else
				{
					PrintToChat($"[ <color=orange>{StoredData[player.userID].PrefixChat}</color> ] | " + $"<color=#538fef>{player.displayName}</color>: " + message);
					return false;
				}
			}
			else
				return null;
		}
		
	    private void OnPlayerDisconnected(BasePlayer player)
		{
			if(StoredData.ContainsKey(player.userID)) 
			{   
				SaveData(player);
				StoredData.Remove(player.userID);
				StoredData_Time.Remove(player.userID);
			}
		}
		private List<Vector3> _spawnpoints = new List<Vector3>();
		
		private readonly Dictionary<string, string> _command = new Dictionary<string, string>
		{
			["ONLINE"] = "zona_c category online",
			["OFFLINE"] = "zona_c category offline",
			["PRISONER"] = "zona_c category prisoner"
		};
		private List<BaseEntity> _zonaentity = new List<BaseEntity>();
		
		[ChatCommand("tpzona")]
		private void cmdTPZona(BasePlayer player)
		{
			Vector3 spawnpoint = Vector3.zero;
			
			if(player.IsAdmin || permission.UserHasPermission(player.UserIDString, "xprison.admin"))
			{
				if(_adminspawnpoint == Vector3.zero)
				{
					Player.Reply(player, lang.GetMessage("SPAWN_ADMIN", this, player.UserIDString), config.Setting.SteamID);
					return;
				}
				else
					spawnpoint = _adminspawnpoint;
			}
			else if(config.Zona.VisitorValide && permission.UserHasPermission(player.UserIDString, "xprison.visitor"))
			{
				if(config.Zona.ClearInventory && player.inventory.AllItems().Count() != 0)
				{
					Player.Reply(player, lang.GetMessage("CLEAR_INVENTORY", this, player.UserIDString), config.Setting.SteamID);
					return;
				}
				
				if(_visitorsspawnpoints.Count == 0)
				{
					Player.Reply(player, lang.GetMessage("SPAWN_VISITOR", this, player.UserIDString), config.Setting.SteamID);
					return;
				}
				else
					spawnpoint = _visitorsspawnpoints.GetRandom();
			}
			
			if(spawnpoint != Vector3.zero)
				TP(player, spawnpoint);
		}
		
				
				
		private void TP(BasePlayer player, Vector3 position)
		{
			if(position == Vector3.zero || position == null || player == null || player.IsDead()) return;
			
			try
			{
				if(player.IsSleeping())
				{
					player.RemoveFromTriggers();
					player.Teleport(position);
					
					if(!IsInvisible(player))
					{
						player.UpdateNetworkGroup();
						player.SendNetworkUpdateImmediate(false);
					}
				}
				else
				{
					player.UpdateActiveItem(default(ItemId));
					player.EnsureDismounted();
		   		 		  						  	   		  	 	 		  	  			  			 		  				
					if(player.HasParent())
						player.SetParent(null, true, true);
	
					player.EndLooting();
					player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
					player.CancelInvoke("InventoryUpdate");
					player.CancelInvoke("TeamUpdate");
	
					player.RemoveFromTriggers();
					player.Teleport(position);

					player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
					player.ClientRPCPlayer(null, player, "StartLoading");
					player.SendEntityUpdate();
		   		 		  						  	   		  	 	 		  	  			  			 		  				
					if(!IsInvisible(player))
					{
						player.UpdateNetworkGroup();
						player.SendNetworkUpdateImmediate(false);
					}
				}
			}
			finally
			{
				if(!IsInvisible(player))
					player.ForceUpdateTriggers();
			}
		}
		
		private void GetVisitorsSpawnPoint()
		{
			foreach(var visitorspawnpoint in _zonaentity.OfType<Igniter>())
			    _visitorsspawnpoints.Add(visitorspawnpoint.transform.position);
				
			PrintWarning(LanguageEnglish ? $"Found {_visitorsspawnpoints.Count} visitor spawn points!" : $"Найдено {_visitorsspawnpoints.Count} точек для спавна посетителей!");
		}
		
		private void OnPluginLoaded(Plugin name)
		{
			if(name.Title == "Better Chat" || name.Title == "IQChat")
				Unsubscribe(nameof(OnPlayerChat));
		}
		private object OnPlayerAttack(BasePlayer attacker, HitInfo info) => StoredData.ContainsKey(attacker.userID) ? true : null;
		
				
		private readonly Dictionary<string, List<Vector3>> _safezone = new Dictionary<string, List<Vector3>>
		{
			["assets/bundled/prefabs/autospawn/monument/medium/compound.prefab"] = new List<Vector3>
			{
				new Vector3(-18.0f, 0.1f, 27.4f),
				new Vector3(20.8f, 0.2f, 32.3f),
				new Vector3(28.9f, 0.3f, -5.9f),
				new Vector3(-16.0f, 0.2f, -49.7f)
			},
			["assets/bundled/prefabs/autospawn/monument/medium/bandit_town.prefab"] = new List<Vector3>
			{
				new Vector3(37.8f, 2.0f, -40.6f),
				new Vector3(57.9f, 2.0f, -20.5f),
				new Vector3(0.7f, 1.8f, 29.3f),
				new Vector3(-48.0f, 2.0f, 27.9f)
			}
		};
		
		private void BlockGUI(BasePlayer player)
		{
			CuiElementContainer container = new CuiElementContainer();
			
			container.Add(new CuiElement
            {
				DestroyUi = ".GUI_BLOCK",
                Parent = config.Prisoner.BlockLayerUI,
				Name = ".GUI_BLOCK",
                Components =
                {
					new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", ".BlockIMG") },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                }
            });
			
			CuiHelper.AddUi(player, container);
		}

        private class PrisonConfig
        {
			internal class GeneralSetting  
			{
				[JsonProperty(LanguageEnglish ? "Profile SteamID for custom avatar" : "SteamID профиля для кастомной аватарки")] public ulong SteamID;
				[JsonProperty(LanguageEnglish ? "List of allowed console commands on the prison" : "Список разрешенных консольных команд на зоне")] public List<string> ConsoleCommand;
				[JsonProperty(LanguageEnglish ? "List of allowed chat commands in the prison" : "Список разрешенных чат команд на зоне")] public List<string> ChatCommand;
				[JsonProperty(LanguageEnglish ? "List of time templates" : "Список шаблонов времени")] public Dictionary<string, int> TimeList;
				[JsonProperty(LanguageEnglish ? "Enable logs in Discord - [ DiscordMessages plugin required ]" : "Включить логи в Discord - [ Требуется плагин DiscordMessages ]")] public bool UseDiscordLogs;
				public string WebHook;
			}
			
			internal class ZonaSetting  
			{
				[JsonProperty(LanguageEnglish ? "Prison location coordinates" : "Координаты расположения зоны")] public Vector3 PositionZona;
				[JsonProperty(LanguageEnglish ? "Prison file name for the CopyPaste plugin" : "Имя файла зоны для плагина CopyPaste")] public string NameFileZona;
				[JsonProperty(LanguageEnglish ? "Diameter from the point you specify to search for prison structures, spawners, and more" : "Диаметр от указаной вами точки для поиска конструкций зоны, спавнеров и прочего")] public float DiameterZona;
				[JsonProperty(LanguageEnglish ? "Automatically turn on the boombox after the prison spawns" : "Автоматически включить музыкальный центр после спавна зоны")] public bool OnBoomBox;
				[JsonProperty(LanguageEnglish ? "Use the CopyPaste plugin for the prison [ True - CopyPaste | False - the prison location point you specified, you can use a custom prefab or build the prison manually ]" : "Использовать плагин CopyPaste для зоны [ True - CopyPaste | False - указаная вами точка расположения зоны, можно использовать кастомный префаб или построить зону вручную ]")] public bool UseCopyPaste;
				[JsonProperty(LanguageEnglish ? "Allow regular players to visit the prison" : "Разрешить посещать зону простым игрокам")] public bool VisitorValide;
				[JsonProperty(LanguageEnglish ? "Allow ordinary players to visit the prison only with an empty inventory" : "Разрешить посещать зону простым игрокам только с пустым инвентарем")] public bool ClearInventory;
			}
			
			internal class PrisonerSetting  
			{
				[JsonProperty(LanguageEnglish ? "Lock the clothing slots" : "Заблокировать слоты с одеждой")] public bool BlockWear;
				[JsonProperty(LanguageEnglish ? "Lock the slots on the belt" : "Заблокировать слоты на поясе")] public bool BlockBelt;
				[JsonProperty(LanguageEnglish ? "Lock inventory slots" : "Заблокировать слоты в инвентаре")] public bool BlockMain;
				[JsonProperty(LanguageEnglish ? "Lock clothing items - [ Player will not be able to discard them ]" : "Заблокировать предметы одежды - [ Игрок не сможет их выбросить ]")] public bool BlockWearItems;
				[JsonProperty(LanguageEnglish ? "Use a nickname prefix for the prisoner - [ Set to False if the prefix should be disabled or the prefix is used by a third party chat plugin ]" : "Использовать префикс с кличкой для заключенного  - [ Установите False, если префикс нужно отключить или префикс используется сторонним плагином для чата ]")] public bool UseName;
				[JsonProperty(LanguageEnglish ? "Link to prisoner lock screen image" : "Ссылка на картинку блокировки экрана заключенного")] public string BlockURLImage;
				[JsonProperty(LanguageEnglish ? "Layer UI lock screen image - [ Overlay | Hud ]" : "Слой UI картинки блокировки экрана - [ Overlay | Hud ]")] public string BlockLayerUI;
				[JsonProperty(LanguageEnglish ? "Use screen lock" : "Использовать блокировку экрана")] public bool UseBlockImageUI;
				[JsonProperty(LanguageEnglish ? "Do not kill the prisoner when he enters the prison, but simply teleporting" : "Не убивать заключенного когда он отправляется в тюрьму, а просто телепортировать")] public bool TpToPrison;
				[JsonProperty(LanguageEnglish ? "When a prisoner is teleported to prison, clear his inventory" : "Когда заключенный телепортируется в тюрьму, очищать его инвентарь")] public bool TpToPrisonInvClear;
				[JsonProperty(LanguageEnglish ? "Do not kill the prisoner when he is released from prison, but simply teleporting to the Outpost or Bandit Camp" : "Не убивать заключенного когда он освобождается из тюрьмы, а просто телепортировать в Outpost или Bandit Camp")] public bool TpOfPrison;
				[JsonProperty(LanguageEnglish ? "When a prisoner teleports out of prison, clear his inventory" : "Когда заключенный телепортируется из тюрьмы, очищать его инвентарь")] public bool TpOfPrisonInvClear;
				[JsonProperty(LanguageEnglish ? "[ Mini-game ] Allow prisoners to shorten their time by pressing buttons inside the prison" : "[ Мини-игра ] Разрешить заключенным сокращать время заключения нажимая кнопки внутри тюрьмы")] public bool ButtonPress;
				[JsonProperty(LanguageEnglish ? "[ Mini-game ] For pressing one button, how many seconds to reduce the term of imprisonment" : "[ Мини-игра ] За нажатие одной кнопки на сколько секунд сокращать заключение")] public int ButtonPressSeconds;
				[JsonProperty(LanguageEnglish ? "[ Mini-game 2 ] Allow inmates to reduce their incarceration time by recycling items inside the prison" : "[ Мини-игра 2 ] Разрешить заключенным сокращать время заключения перерабатывая предметы внутри тюрьмы")] public bool ItemRecycle;
				[JsonProperty(LanguageEnglish ? "[ Mini-game 2 ] List of items, skins and number of seconds" : "[ Мини-игра 2 ] Список предметов, скинов и кол-во секунд")] public Dictionary<string, Dictionary<ulong, int>> ItemRecycleList;
				[JsonProperty(LanguageEnglish ? "List of prisoner's attires [ Shortname - SkinID ]" : "Список одежды заключенного [ Shortname - SkinID ]")] public Dictionary<string, ulong> AttireList;
				[JsonProperty(LanguageEnglish ? "List of prisoner nicknames" : "Список кличек заключенных")] public List<string> NamePrisoner;
			}
			
			[JsonProperty(LanguageEnglish ? "General settings" : "Общие настройки")]
			public GeneralSetting Setting = new GeneralSetting();			
			[JsonProperty(LanguageEnglish ? "Settings prison" : "Настройки зоны")]
			public ZonaSetting Zona = new ZonaSetting();			
			[JsonProperty(LanguageEnglish ? "Prisoner settings" : "Настройки заключенных")]
			public PrisonerSetting Prisoner = new PrisonerSetting();						
			
			public static PrisonConfig GetNewConfiguration()
            {
                return new PrisonConfig 
                {
					Setting = new GeneralSetting
					{
						SteamID = 0,
						ConsoleCommand = new List<string> { "global.respawn" },
						ChatCommand = new List<string> { "zona" },
						TimeList = new Dictionary<string, int> { ["30m"] = 1800, ["2h"] = 7200, ["6h"] = 21600, ["1d"] = 86400, ["3d"] = 259200, ["7d"] = 604800 },
						UseDiscordLogs = false,
						WebHook = ""
					},
					Zona = new ZonaSetting
					{
						PositionZona = new Vector3(1000, 700, 0),
						NameFileZona = "zona606",
						DiameterZona = 35f,
						OnBoomBox = true,
						UseCopyPaste = true,
						VisitorValide = false,
						ClearInventory = true
					},
					Prisoner = new PrisonerSetting
					{
						BlockWear = true,
						BlockBelt = false,
						BlockMain = false,
						BlockWearItems = true,
						UseName = true,
						BlockURLImage = "https://i.imgur.com/SZoYTLt.png",
						BlockLayerUI = "Hud",
						UseBlockImageUI = false,
						TpToPrison = false,
						TpToPrisonInvClear = false,
						TpOfPrison = false,
						TpOfPrisonInvClear = false,
						ButtonPress = true,
						ButtonPressSeconds = 1,
						ItemRecycle = true,
						ItemRecycleList = new Dictionary<string, Dictionary<ulong, int>> { ["rock"] = new Dictionary<ulong, int> { [0] = 1, [1000] = 3, [2000] = 5 } },
						AttireList = new Dictionary<string, ulong>
						{
							["burlap.headwrap"] = 2655848185,
							["burlap.shirt"] = 2655843517,
							["burlap.trousers"] = 2655838948
						},
						NamePrisoner = LanguageEnglish ? new List<string> { "Schellen", "Schilten", "Espadas", "Bastos", "Oros", "Denari", "Rosen", "Copas", "Herz", "Eichel", "Kule", "Laub", "Zelený", "Grün" } : new List<string> { "Автозак", "Авторитет", "Аристократ", "Аркашка", "Бадяга", "Базарило", "Баклан", "Барыга", "Батя", "Блатной", "Бродяга", "Борзый", "Болтун", "Валет" }
					}
				};
			}
        }
		
		private void OnItemRecycle(Item item, Recycler recycler)
		{
			BasePlayer player = BasePlayer.FindByID(recycler.LastLootedBy);
			
			if(player != null && API_IsOnlinePrisoner(player.userID) && !StoredData[player.userID].Life)
			{
				string shortname = item.info.shortname;
				
				if(config.Prisoner.ItemRecycleList.ContainsKey(shortname) && config.Prisoner.ItemRecycleList[shortname].ContainsKey(item.skin))
				{
					int x = config.Prisoner.ItemRecycleList[shortname][item.skin];
					
					StoredData[player.userID].Time -= x;
					StoredData_Time[player.userID] -= x;
					
					Player.Reply(player, string.Format(lang.GetMessage("ITEM_RECYCLE", this, player.UserIDString), item.info.displayName.english.ToUpper(), x), config.Setting.SteamID);
				}
			}
		}
		
		private object OnBetterChat(Dictionary<string, object> chat)
        {
			if(!config.Prisoner.UseName) return chat;
			
			IPlayer player = chat["Player"] as IPlayer;
			
			ulong userID = Convert.ToUInt64(player.Id);
			
			if(API_IsOnlinePrisoner(userID))
			{
				string name = $"[ <color=orange>{StoredData[userID].PrefixChat}</color> ] | " + chat["Username"];
				chat["Username"] = name;
			}
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            return chat;
        }
		private const bool LanguageEnglish = false;
		
		private void OnlineGUI(BasePlayer player, int Page = 0)
		{
            CuiElementContainer container = new CuiElementContainer();
			
			List<BasePlayer> online_players = BasePlayer.activePlayerList.Where(xx => !StoredData.ContainsKey(xx.userID)).ToList();
			int x = 0, y = 0;
			bool multifighting = MultiFighting;
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-502.5 -228.5", OffsetMax = "502.5 177.5" },
                Image = { Color = "0 0 0 0" }
            }, ".ZONA_GUI", ".ZONA_ONLINE");
			
			foreach(BasePlayer p in online_players.Skip(Page * 32))
			{
				container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{-497.5 + (x * 250)} {153 - (y * 50)}", OffsetMax = $"{-252.5 + (x * 250)} {198 - (y * 50)}" },
                    Image = { Color = "0.517 0.521 0.509 0.5", Material = "assets/icons/greyout.mat" }
                }, ".ZONA_ONLINE", ".PlayerGUI");
				
				container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "45 0", OffsetMax = "-195 0" },
                    Image = { Color = "0.53 0.77 0.35 1", Material = "assets/icons/greyout.mat" }
                }, ".PlayerGUI");	
				
				container.Add(new CuiElement
                {
                    Parent = ".PlayerGUI",
                    Components =
                    {
					    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", p.UserIDString) },
                        new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "2.5 -20", OffsetMax = "42.5 20" }
                    }
                });
				
				container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "52.5 30", OffsetMax = "-22.5 0" },
                    Text = { Text = $"NAME: {p.displayName}", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 10 }
                }, ".PlayerGUI");			    
				
				container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "52.5 17", OffsetMax = "-22.5 -13" },
                    Text = { Text = $"ID: {p.userID}", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 10 }
                }, ".PlayerGUI");		
				
				container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-20 25", OffsetMax = "-2.5 42.5" },
                    Button = { Color = "1 1 1 0.75", Sprite = "assets/icons/add.png", Command = $"zona_c prisoner add {p.userID}" },
                    Text = { Text = "" }
                }, ".PlayerGUI");
				
				if(multifighting)
					container.Add(new CuiButton
					{
						RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-20 2.5", OffsetMax = "-2.5 20" },
						Button = { Color = "1 1 1 0.75", Sprite = (bool)MultiFighting?.CallHook("IsSteam", p.Connection) ? "assets/icons/steam.png" : "assets/icons/warning_2.png" },
						Text = { Text = "" }
					}, ".PlayerGUI");
				
				x++;
				
                if(x == 4)
                {
                    x = 0;
                    y++;
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                    if(y == 8)
                        break;
                }
			}
			
			bool back = Page != 0;
			bool next = online_players.Count > ((Page + 1) * 32);

			container.Add(new CuiButton 
            {    
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-190 -36.75", OffsetMax = "-100 -10" },
                Button = { Color = back ? "0.65 0.29 0.24 1" : "0.65 0.29 0.24 0.4", Command = back ? $"page.xprison online back {Page}" : "" },
                Text = { Text = lang.GetMessage("BACK", this, player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-regular.ttf", Color = back ? "0.92 0.79 0.76 1" : "0.92 0.79 0.76 0.4" }
            }, ".ZONA_ONLINE");				 			
			
			container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-95 -36.75", OffsetMax = "-5 -10" },
                Button = { Color = next ? "0.35 0.45 0.25 1" : "0.35 0.45 0.25 0.4", Command = next ? $"page.xprison online next {Page}" : "" },
                Text = { Text = lang.GetMessage("NEXT", this, player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-regular.ttf", Color = next ? "0.75 0.95 0.41 1" : "0.75 0.95 0.41 0.4" }
            }, ".ZONA_ONLINE");
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-200 -46.75", OffsetMax = "-195 0" },
                Image = { Color = "0.517 0.521 0.509 0.95", Material = "assets/icons/greyout.mat" }
            }, ".ZONA_ONLINE");	
			
			CuiHelper.AddUi(player, container);
		}		
		
		private class PrisonerCreate
		{
			public string Reason;
			public ulong SteamID;
			public bool Sentence;
			public bool Life;
			public int Duration;
		}
		
		private void API_RemoveOfflinePrisoner(ulong userID)
		{
			if(!API_IsOfflinePrisoner(userID))
			{
				PrintWarning(LanguageEnglish ? $"[ {userID} ] - is not a prisoner!" : $"[ {userID} ] - не является заключенным!");
				return;
			}
			
			Prisoner Data = new Prisoner("", false, false, 0, false, "");
			
			PrintWarning(LanguageEnglish ? $"[ {userID} ] - released!" : $"[ {userID} ] - освобожден!");
			Interface.Oxide.DataFileSystem.WriteObject($"XDataSystem/XPrison/{userID}", Data);
			
			if(config.Setting.UseDiscordLogs && DiscordMessages) DiscordMessages.Call("API_SendTextMessage", config.Setting.WebHook, LanguageEnglish ? $"``[ Offline ] [ {userID} ] - released!``" : $"``[ Offline ] [ {userID} ] - освобожден!``");
			
			BasePlayer prisonerSleep = BasePlayer.FindSleeping(userID);
			
			if(prisonerSleep != null)
				LockUnlockPrisonerInventory(prisonerSleep, false);
		}
		
				
				
		[ConsoleCommand("cmd_prisoner")]
		private void ccmdAddPrisoner(ConsoleSystem.Arg args)
		{
			BasePlayer player = args.Player();
			
			if(player == null || player.IsAdmin || permission.UserHasPermission(player.UserIDString, "xprison.admin"))
			{
				ulong steamID = Convert.ToUInt64(args.Args[1]);
				bool life = Convert.ToBoolean(args.Args[2]), sentence = Convert.ToBoolean(args.Args[4]);
				int duration = Convert.ToInt32(args.Args[3]);
				string reason = string.Join(" ", args.Args.Skip(5));
				
				if(!steamID.IsSteamId())
				{
					PrintWarning(LanguageEnglish ? $"{steamID} - is not SteamID64!" : $"{steamID} - не является SteamID64!");
					return;
				}
				
				BasePlayer prisoner = BasePlayer.FindByID(steamID);
				
				switch(args.Args[0])
				{
					case "add":
					{
						if(prisoner == null)
							API_AddOfflinePrisoner(steamID, life, duration, sentence, reason);
						else if(prisoner.IsConnected)
							API_AddOnlinePrisoner(prisoner, life, duration, sentence, reason);
						else
							PrintWarning(LanguageEnglish ? "Couldn't find the prisoner!" : "Не удалось найти заключенного!");
						
						break;
					}					
					case "remove":
					{
						if(prisoner == null)
							API_RemoveOfflinePrisoner(steamID);
						else if(prisoner.IsConnected)
							API_RemoveOnlinePrisoner(prisoner);
						else
							PrintWarning(LanguageEnglish ? "Couldn't find the prisoner!" : "Не удалось найти заключенного!");
						
						break;
					}
				}
			}
		}
		
		private int GetUnixTime() => (int)((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();
		
		private Dictionary<BasePlayer, DateTime> Cooldowns = new Dictionary<BasePlayer, DateTime>();
		
				
		[PluginReference] private Plugin ImageLibrary, CopyPaste, Vanish, MultiFighting, BetterChat, IQChat, DiscordMessages;
		
		private Dictionary<ulong, PrisonerCreate> _adminsPrisonerCreate = new Dictionary<ulong, PrisonerCreate>();
        protected override void SaveConfig() => Config.WriteObject(config);
		private List<Vector3> _visitorsspawnpoints = new List<Vector3>();
		
				
				
		private object OnServerCommand(ConsoleSystem.Arg arg)
		{
			BasePlayer player = arg.Connection?.player as BasePlayer;
			
			if(player != null && (player.IsAdmin || permission.UserHasPermission(player.UserIDString, "xprison.admin")))
				return null;
			if(player != null && !config.Setting.ConsoleCommand.Contains(arg.cmd.FullName) && API_IsOnlinePrisoner(player.userID))
			{
				player.SendConsoleCommand($"echo [ <color=white>ZONA</color> ] - {lang.GetMessage("CMD_BLOCK", this, player.UserIDString)}");
				
				return true;
			}

			return null;
		}
		
		private bool API_IsOfflinePrisoner(ulong userID)
		{
			if(Interface.Oxide.DataFileSystem.ExistsDatafile($"XDataSystem/XPrison/{userID}"))
			{
				Prisoner Data = Interface.Oxide.DataFileSystem.ReadObject<Prisoner>($"XDataSystem/XPrison/{userID}");
				
				if(Data.Life || Data.IsPrisoner)
					return true;
				else
					return false;
			}
			return false;
		}
		private Vector3 _adminspawnpoint = Vector3.zero;
		
		private void LoadData(BasePlayer player)
		{ 
			if(Interface.Oxide.DataFileSystem.ExistsDatafile($"XDataSystem/XPrison/{player.userID}"))
			{
				Prisoner Data = Interface.Oxide.DataFileSystem.ReadObject<Prisoner>($"XDataSystem/XPrison/{player.userID}");
			
				NextTick(() =>
				{
					if(Data != null)
					{
						if(Data.Life)
						{
							if(config.Prisoner.TpToPrison)
								TP(player, _spawnpoints.GetRandom());
							else
								player.Hurt(1000);
							
							StoredData[player.userID] = Data;
							
							if(config.Prisoner.UseBlockImageUI) BlockGUI(player);
						}
						else if(Data.IsPrisoner && (Data.Sentence && Data.Time > 0 || !Data.Sentence && Data.Time > GetUnixTime()))
						{
							if(config.Prisoner.TpToPrison)
								TP(player, _spawnpoints.GetRandom());
							else
								player.Hurt(1000);
							
							StoredData[player.userID] = Data;
							StoredData_Time[player.userID] = Data.Time;
							
							if(config.Prisoner.UseBlockImageUI) BlockGUI(player);
						}
						else if(Data.IsPrisoner)
						{
							StoredData[player.userID] = Data;
							NextTick(() => API_RemoveOnlinePrisoner(player));
							
							CuiHelper.DestroyUi(player, ".GUI_BLOCK");
						}
					}
				});
			}
		} 
		 
		protected override void LoadConfig()
        {
            base.LoadConfig(); 
			 
			try
			{
				config = Config.ReadObject<PrisonConfig>();
			}
			catch  
			{
				PrintWarning(LanguageEnglish ? "Configuration read error! Creating a default configuration!" : "Ошибка чтения конфигурации! Создание дефолтной конфигурации!");
				LoadDefaultConfig();
			}
			
			if(config.Setting.TimeList == null)
				config.Setting.TimeList = new Dictionary<string, int> { ["30m"] = 1800, ["2h"] = 7200, ["6h"] = 21600, ["1d"] = 86400, ["3d"] = 259200, ["7d"] = 604800 };
			
			SaveConfig();
        }
		
				
		private void OnServerInitialized(bool valide)
		{
			PrintWarning("\n-----------------------------\n" +
			"     Author - Monster\n" +
			"     VK - vk.com/idannopol\n" +
			"     Discord - Monster#4837\n" +
			"     Config - v.80401\n" +
			"-----------------------------");
			
			ImageLibrary.Call("AddImage", config.Prisoner.BlockURLImage, ".BlockIMG");
			
			foreach(BasePlayer player in BasePlayer.activePlayerList)
				OnPlayerConnected(player);
			timer.Every(30, () => CheckPrisoner());
			
			timer.Once(valide ? 20 : 5, () =>
			{
				if(config.Zona.UseCopyPaste)
				{
					GetZonaEntity();
				
					if(_zonaentity.Count != 0 && _zonaentity.Count <= 30)
						foreach(var entity in _zonaentity)
							entity.Kill();
					else if(_zonaentity.Count > 30)
					{
						PrintWarning(LanguageEnglish ? "Prison not initialized! Perhaps it already exists (as it should be if the plugin previously initialized it) or many extraneous entities were found!" : "Зона не инициализирована! Возможно она уже существует(так должно быть, если плагин ранее инициализировал её) или найдено много посторонних сущностей!");
						return;
					}
					
					timer.Once(2, () =>
					{
						string[] args = {"height", config.Zona.PositionZona.y.ToString()};
						var zonavalide = CopyPaste.Call("TryPasteFromVector3", config.Zona.PositionZona, 0f, config.Zona.NameFileZona, args);
				
						if(zonavalide is string)
						{
							PrintWarning(LanguageEnglish ? "Prison initialization error! Check CopyPaste plugin and prison file." : "Ошибка инициализации зоны! Проверьте плагин CopyPaste и файл зоны.");
							return;
						}
						else
							PrintWarning(LanguageEnglish ? "Prison initialized successfully!" : "Зона успешно инициализирована!");
					});
				}
			});
			
			timer.Once(valide ? 25 : 10, () => GetSpawnPoints());
			
			InitializeLang();
			permission.RegisterPermission("xprison.admin", this);
			permission.RegisterPermission("xprison.visitor", this);
			
			NextTick(() =>
			{
				if(!config.Prisoner.ButtonPress)
					Unsubscribe(nameof(OnButtonPress));
				
				if(!config.Prisoner.ItemRecycle)
					Unsubscribe(nameof(OnItemRecycle));
				
				if(!config.Prisoner.UseName || BetterChat || IQChat)
					Unsubscribe(nameof(OnPlayerChat));
			});
			
			timer.Once(2, () =>
			{
				if(!ImageLibrary)
				{
					PrintError(LanguageEnglish ? "You don't have the plugin installed - ImageLibrary!" : "У вас не установлен плагин - ImageLibrary!");
					Interface.Oxide.UnloadPlugin(Name);
				}
				
				if(config.Zona.UseCopyPaste && !CopyPaste)
				{
					PrintError(LanguageEnglish ? "You don't have the plugin installed - CopyPaste!" : "У вас не установлен плагин - CopyPaste!");
					Interface.Oxide.UnloadPlugin(Name);
				}
			});
		}
		
		private string API_GetOnlinePrisonerPrefix(BasePlayer player)
		{
			if(StoredData.ContainsKey(player.userID))
				return StoredData[player.userID].PrefixChat;
			
			return string.Empty;
		}
		
		private void GetAdminSpawnPoint()
		{
			var adminspawnpoints = _zonaentity.OfType<PressurePad>();
			int count = adminspawnpoints.Count();
			if(count != 0)
				_adminspawnpoint = adminspawnpoints.ToList().GetRandom().transform.position;
			
			PrintWarning(LanguageEnglish ? $"Found {count} administration spawn points!" : $"Найдено {count} точек для спавна администрации!");
		}
		
		private void GetSpawnPoints()
		{
			GetZonaEntity();		

			foreach(var spawnpoint in _zonaentity.OfType<TeslaCoil>())
			    _spawnpoints.Add(spawnpoint.transform.position);
				
			PrintWarning(LanguageEnglish ? $"Found {_spawnpoints.Count} spawn points!" : $"Найдено {_spawnpoints.Count} точек для спавна!");
			
			GetAdminSpawnPoint();
			GetVisitorsSpawnPoint();
			if(config.Zona.OnBoomBox)
				OnBoomBox();
			
			timer.Once(1, () => _zonaentity.ForEach(xx => xx.OwnerID = 100001));
			
			timer.Once(5, () => {
				_zonaentity.Clear();
				_zonaentity = null;
			});
			
			foreach(var monument in TerrainMeta.Path.Monuments)
				if(_safezone.ContainsKey(monument.name))
				{
					var MTransform = monument.transform;
					
					foreach(Vector3 point in _safezone[monument.name])
						_safezonepoints.Add(MTransform.position + MTransform.rotation * point);
				}
				
			PrintWarning(LanguageEnglish ? $"Found {_safezonepoints.Count} spawn points in the safe zone!" : $"Найдено {_safezonepoints.Count} точек для спавна в сейф зоне!");
		}
		
		private void OnButtonPress(PressButton button, BasePlayer player)
		{
			if(button.OwnerID == 100001 && API_IsOnlinePrisoner(player.userID) && !StoredData[player.userID].Life)
			{
				int x = config.Prisoner.ButtonPressSeconds;
				
				StoredData[player.userID].Time -= x;
				StoredData_Time[player.userID] -= x;
				
				Player.Reply(player, string.Format(lang.GetMessage("BUTTON_PRESS", this, player.UserIDString), x), config.Setting.SteamID);
			}
		}
		
		private Dictionary<ulong, Prisoner> StoredData = new Dictionary<ulong, Prisoner>();
		
		private void GetZonaEntity()
		{
			Vis.Entities(config.Zona.PositionZona, config.Zona.DiameterZona, _zonaentity);
			_zonaentity = _zonaentity.Distinct().ToList();
			_zonaentity = _zonaentity.Where(x => !(x is BasePlayer) && !(x is PlayerCorpse) && !(x is DroppedItemContainer) && !(x is DroppedItem)).ToList();
		}
		
		private void GiveAttire(BasePlayer player)
		{
			player.inventory.Strip();
			
			foreach(var attire in config.Prisoner.AttireList)
			{
				Item item = ItemManager.CreateByName(attire.Key, 1, attire.Value);
				item.MoveToContainer(player.inventory.containerWear);
				
				if(config.Prisoner.BlockWearItems)
					item.LockUnlock(true);
			}
			
			LockUnlockPrisonerInventory(player, true);
		}
		
			}
}
