using Oxide.Core.Plugins;
using System.Collections;
using UnityEngine;    
using Oxide.Core;
using System;
using System.Collections.Generic;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using System.Linq;   
  
namespace Oxide.Plugins 
{ 
    [Info("XSkinMenu", "https://discord.gg/eGWvxAKB4c SkuliDropek", "1.0.8")]
    class XSkinMenu : RustPlugin  
    { 
		
		private void SetSkinItem(BasePlayer player, string item, ulong skin) 
		{
			foreach (var i in player.inventory.FindItemIDs(ItemManager.FindItemDefinition(item).itemid))
			{
				if (i.skin == skin || config.Setting.Blacklist.Contains(i.skin)) continue;
				
				if(errorskins.ContainsKey(skin))
				{
					i.UseItem();
					Item newitem = ItemManager.CreateByName(errorskins[skin]);
					newitem.condition = i.condition;
					newitem.maxCondition = i.maxCondition;
					
					if (i.contents != null)   
						foreach(var module in i.contents.itemList)
						{
							Item content = ItemManager.CreateByName(module.info.shortname, module.amount);
							content.condition = module.condition;
							content.maxCondition = module.maxCondition;
					
							content.MoveToContainer(newitem.contents);
						}	
					
					player.GiveItem(newitem); 
				}
				else
				{
                    i.skin = skin;
                    i.MarkDirty();
		   		 		  						  	   		  		 			  	 	 		  	 	 		  			 
                    BaseEntity entity = i.GetHeldEntity();
                    if (entity != null)
                    {
                        entity.skinID = skin;
                        entity.SendNetworkUpdate();
                    }
				}
			}
		}
		
		private Dictionary<ulong, Data> StoredData = new Dictionary<ulong, Data>();
		
		[ConsoleCommand("skin_c")]
		private void ccmdCategoryS(ConsoleSystem.Arg args)
		{
			BasePlayer player = args.Player();
			
			if(!permission.UserHasPermission(player.UserIDString, "xskinmenu.use")) 
			{
				SendReply(player, lang.GetMessage("NOPERM", this, player.UserIDString));
				return;
			}
			
			if (Cooldowns.ContainsKey(player))
                if (Cooldowns[player].Subtract(DateTime.Now).TotalSeconds >= 0) return;
			
			Effect x = new Effect("assets/bundled/prefabs/fx/notice/loot.drag.grab.fx.prefab", player, 0, new Vector3(), new Vector3());
			Effect z = new Effect("assets/bundled/prefabs/fx/weapons/survey_charge/survey_charge_stick.prefab", player, 0, new Vector3(), new Vector3());
			
			switch(args.Args[0])
			{
				case "category":
				{
					CategoryGUI(player, int.Parse(args.Args[2]));
					ItemGUI(player, args.Args[1]);
					EffectNetwork.Send(x, player.Connection);
					break;
				}				
				case "skin":
				{ 
					SkinGUI(player, args.Args[1]);
					EffectNetwork.Send(x, player.Connection);
					
					CuiHelper.DestroyUi(player, ".ItemGUI");
					break; 
				}				
				case "setskin":
				{ 
					string item = args.Args[1];
					ulong skin = ulong.Parse(args.Args[2]);
					
					if(!StoredData[player.userID].Skins.ContainsKey(item)) return;
					
					Effect y = new Effect("assets/prefabs/deployable/repair bench/effects/skinchange_spraypaint.prefab", player, 0, new Vector3(), new Vector3());
					StoredData[player.userID].Skins[item] = skin;
					
					if(!permission.UserHasPermission(player.UserIDString, "xskinmenu.inventory"))
						SendReply(player, lang.GetMessage("NOPERM", this, player.UserIDString));
					else
					{
					    if(StoredData[player.userID].ChangeSI) SetSkinItem(player, item, skin);
						if(config.GUI.SkinUP) SkinGUI(player, item, int.Parse(args.Args[3]));
					}
					
					EffectNetwork.Send(y, player.Connection);
					break;
				}				
				case "clear":
				{
					string item = args.Args[1];
					StoredData[player.userID].Skins[item] = 0;
					
					CuiHelper.DestroyUi(player, $".I + {args.Args[2]}");
					if(StoredData[player.userID].ChangeSCL) SetSkinItem(player, item, 0);
					
					EffectNetwork.Send(z, player.Connection);
					break;
				}				
				case "clearall":
				{
					StoredData[player.userID].Skins.Clear();
					
					foreach(var skin in StoredDataSkins) 
						StoredData[player.userID].Skins.Add(skin.Key, 0);
					
					GUI(player);
					EffectNetwork.Send(z, player.Connection);
					break;
				}
			}
			
			Cooldowns[player] = DateTime.Now.AddSeconds(0.5f); 
		}
		private Dictionary<ulong, bool> StoredDataFriends = new Dictionary<ulong, bool>();
		
		private void GenerateItems()
		{
			if(config.Setting.UpdateSkins)
				foreach (var pair in Rust.Workshop.Approved.All)
				{
					if (pair.Value == null || pair.Value.Skinnable == null) continue;
				
					ulong skinID = pair.Value.WorkshopdId; 
				
					string item = pair.Value.Skinnable.ItemName;
					if (item.Contains("lr300")) item = "rifle.lr300";
				
					if(!StoredDataSkins.ContainsKey(item))
						StoredDataSkins.Add(item, new List<ulong>());
				
					if(!StoredDataSkins[item].Contains(skinID))
						StoredDataSkins[item].Add(skinID);
				}
			
			if(config.Setting.UpdateSkinsFacepunch)
				foreach (ItemDefinition item in ItemManager.GetItemDefinitions())
				{
					foreach(var skinID in ItemSkinDirectory.ForItem(item).Select(skin => Convert.ToUInt64(skin.id)))
					{
						if(!StoredDataSkins.ContainsKey(item.shortname))
						StoredDataSkins.Add(item.shortname, new List<ulong>());
				
						if(!StoredDataSkins[item.shortname].Contains(skinID))
						StoredDataSkins[item.shortname].Add(skinID);
					}
				}	
			
			Interface.Oxide.DataFileSystem.WriteObject("XSkinMenu/Skins", StoredDataSkins);
		}
		
		private void SetSkinEntity(BasePlayer player, BaseEntity entity, string shortname)
		{
			ulong skin = StoredData[player.userID].Skins[shortname];
			
			if(skin == entity.skinID || skin == 0) return;
			if(errorskins.ContainsKey(skin))
			{
				SendInfo(player, lang.GetMessage("ERRORSKIN", this, player.UserIDString));
				return;
			}
			
			entity.skinID = skin;
            entity.SendNetworkUpdate();
			Effect.server.Run("assets/prefabs/deployable/repair bench/effects/skinchange_spraypaint.prefab", entity.transform.localPosition);
		}
        protected override void SaveConfig() => Config.WriteObject(config);
		
		private void SkinGUI(BasePlayer player, string item, int Page = 0)
		{
			CuiHelper.DestroyUi(player, ".SkinGUI");
            CuiElementContainer container = new CuiElementContainer(); 
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-502.5 -270", OffsetMax = "502.5 177.5" },
                Image = { Color = config.GUI.BColor3 }
            }, ".SGUI", ".SkinGUI");
			
			int x = 0, y = 0;
			ulong s = StoredData[player.userID].Skins[item];
			
			foreach(var skin in StoredDataSkins[item].Skip(Page * 40))
			{		
			    container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{-497.5 + (x * 100)} {123.25 - (y * 100)}", OffsetMax = $"{-402.5 + (x * 100)} {218.25 - (y * 100)}" },
                    Image = { Color = s == skin ? config.GUI.ActiveBlockColor : config.GUI.BlockColor, Material = "assets/icons/greyout.mat" }
                }, ".SkinGUI", ".Skin");
				
				container.Add(new CuiElement
                {
                    Parent = ".Skin",
                    Components =
                    {
					    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", $"{skin}152") },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "7.5 7.5", OffsetMax = "-7.5 -7.5" }
                    }
                });
				
				container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0", Command = $"skin_c setskin {item} {skin} {Page}" },
                    Text = { Text = "" }
                }, ".Skin");
		   		 		  						  	   		  		 			  	 	 		  	 	 		  			 
				if(config.Setting.DeleteButton && player.IsAdmin)
				    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-20 5", OffsetMax = "-5 20" },
                        Button = { Color = "1 1 1 0.750604", Sprite = "assets/icons/clear.png", Command = $"xskin remove_ui {item} {skin} {Page}" },
                        Text = { Text = "" }
                    }, ".Skin");				
				
				x++;
				
				if(x == 10)
				{
					x = 0;
					y++;
					
					if(y == 4)
						break;
				}
			}
			
			bool back = Page != 0;
			bool next = StoredDataSkins[item].Count > ((Page + 1) * 40);
			
			container.Add(new CuiButton 
            {    
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-190 5", OffsetMax = "-100 31.75" },
                Button = { Color = back ? config.GUI.ActiveBackColor : config.GUI.InactiveBackColor, Command = back ? $"page.xskinmenu skin back {item} {Page}" : "" },
                Text = { Text = lang.GetMessage("BACK", this, player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-regular.ttf", Color = back ? config.GUI.ActiveBackColorText : config.GUI.InactiveBackColorText }
            }, ".SkinGUI");				 			
			
			container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-95 5", OffsetMax = "-5 31.75" },
                Button = { Color = next ? config.GUI.ActiveNextReloadColor : config.GUI.InactiveNextReloadColor, Command = next ? $"page.xskinmenu skin next {item} {Page}" : "" },
                Text = { Text = lang.GetMessage("NEXT", this, player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-regular.ttf", Color = next ? config.GUI.ActiveNextReloadColorText : config.GUI.InactiveNextReloadColorText }
            }, ".SkinGUI");
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 36.75", OffsetMax = "0 41.75" },
                Image = { Color = config.GUI.BColor1, Material = "assets/icons/greyout.mat" }
            }, ".SkinGUI");			
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-200 0", OffsetMax = "-195 36.75" },
                Image = { Color = config.GUI.BColor1, Material = "assets/icons/greyout.mat" }
            }, ".SkinGUI");
			
			CuiHelper.AddUi(player, container);
		}
		
		private void OnPlayerRespawned(BasePlayer player)
		{
			if(config.Setting.ReskinRespawn)
			{
				var items = player.inventory.AllItems();
			
				timer.Once(1, () => 
				{
					foreach(Item item in items)
						if(StoredData[player.userID].Skins.ContainsKey(item.info.shortname)) 
							SetSkinCraftGive(player, item);
				});
			}
		}
		 
		protected override void LoadConfig()
        {
            base.LoadConfig(); 
			 
			try
			{
				config = Config.ReadObject<SkinConfig>();
			}
			catch  
			{
				PrintWarning("Ошибка чтения конфигурации! Создание дефолтной конфигурации!");
				LoadDefaultConfig();
			}
			
			SaveConfig();
        }
		
		[ChatCommand("skinentity")]
		private void cmdSetSkinEntity(BasePlayer player)
		{
			if(!permission.UserHasPermission(player.UserIDString, "xskinmenu.entity"))
			{
				SendReply(player, lang.GetMessage("NOPERM", this, player.UserIDString));
				return;
			}
			
			if(StoredData[player.userID].ChangeSE)
			{
			    RaycastHit rhit;
 
			    if (!Physics.Raycast(player.eyes.HeadRay(), out rhit, 3f, LayerMask.GetMask("Deployed", "Construction", "Prevent Building"))) return;
			    var entity = rhit.GetEntity();
				
				if(entity == null) return;
				
				if(entity is BaseVehicle)
				{
					var vehicle = entity as BaseVehicle;
					var shortname = vehicle.ShortPrefabName;
					if(!StoredData[player.userID].Skins.ContainsKey(shortname)) return;
					
					SetSkinTransport(player, vehicle, shortname);
				}
				else
					if(entity.OwnerID == player.userID || player.currentTeam != 0 && player.Team.members.Contains(entity.OwnerID) && StoredDataFriends.ContainsKey(entity.OwnerID) && StoredDataFriends[entity.OwnerID])
						if(_shortnamesEntity.ContainsKey(entity.ShortPrefabName))
						{
							var shortname = _shortnamesEntity[entity.ShortPrefabName];
							if(!StoredData[player.userID].Skins.ContainsKey(shortname)) return;
					
							SetSkinEntity(player, entity, shortname);
						}
			}
		}
		
		[ConsoleCommand("xskin")]
		private void ccmdAdmin(ConsoleSystem.Arg args)
		{
			if (args.Player() == null || args.Player().IsAdmin)
			{
				string item = args.Args[1];
				
				if(!StoredDataSkins.ContainsKey(item))
				{
					PrintWarning($"Не найдено предмета <{item}> в списке!");
					return;
				}
				
				switch(args.Args[0])
				{
					case "add": 
					{
						ulong skinID = ulong.Parse(args.Args[2]);
							
						if(StoredDataSkins[item].Contains(skinID))
							PrintWarning($"Скин <{skinID}> уже есть в списке скинов предмета <{item}>!");
						else
						{
							StoredDataSkins[item].Add(skinID);
							PrintWarning($"Скин <{skinID}> успешно добавлен в список скинов предмета <{item}>!");
							
							ImageLibrary.Call("AddImage", $"http://api.skyplugins.ru/api/getskin/{skinID}/150", $"{skinID}" + 152);
						}
						
						break;
					}					
					case "remove":
					{
						ulong skinID = ulong.Parse(args.Args[2]);
						
						if(StoredDataSkins[item].Contains(skinID))
						{
							StoredDataSkins[item].Remove(skinID);
							PrintWarning($"Скин <{skinID}> успешно удален из списка скинов предмета <{item}>!");
						}
						else
							PrintWarning($"Скин <{skinID}> не найден в списке скинов предмета <{item}>!");
						
						break;
					}					
					case "remove_ui":
					{
						ulong skinID = ulong.Parse(args.Args[2]);
						
						if(StoredDataSkins[item].Contains(skinID))
						{
							BasePlayer player = args.Player();
							
							if(player != null)
							{
								StoredDataSkins[item].Remove(skinID);
								if(config.GUI.DelSkinUP) SkinGUI(player, item, int.Parse(args.Args[3]));
								EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/weapons/survey_charge/survey_charge_stick.prefab", player, 0, new Vector3(), new Vector3()), player.Connection);
								PrintWarning($"Скин <{skinID}> успешно удален из списка скинов предмета <{item}>!");
							}
						}
						else
							PrintWarning($"Скин <{skinID}> не найден в списке скинов предмета <{item}>!");
						
						break;
					}
					case "list": 
					{
						if(StoredDataSkins[item].Count == 0)
						{
							PrintWarning($"Список скинов предмета <{item}> пуст!");
							return;
						}
						
						string skinslist = $"Список скинов предмета <{item}>:\n";
						
						foreach(ulong skinID in StoredDataSkins[item])
						    skinslist += $"\n{skinID}";
						
						PrintWarning(skinslist);
						
						break;
					}					
					case "clearlist":
					{
						if(StoredDataSkins[item].Count == 0)
						{
							PrintWarning($"Список скинов предмета <{item}> уже пуст!");
							return;
						}
						else
						{
							StoredDataSkins[item].Clear();
							PrintWarning($"Список скинов предмета <{item}> успешно очищен!");
						} 
						
						break;  
					}					  
				}
				
				Interface.Oxide.DataFileSystem.WriteObject("XSkinMenu/Skins", StoredDataSkins);
			}
		}
		
		private void ItemGUI(BasePlayer player, string category, int Page = 0)
		{
			CuiHelper.DestroyUi(player, ".SettingGUI");
			CuiHelper.DestroyUi(player, ".SkinGUI");
			CuiHelper.DestroyUi(player, ".ItemGUI");
            CuiElementContainer container = new CuiElementContainer(); 
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-502.5 -270", OffsetMax = "502.5 177.5" },
                Image = { Color = "0 0 0 0" }
            }, ".SGUI", ".ItemGUI");
			
			int x = 0, y = 0, z = 0;
			
			foreach(var item in config.Category[category].Skip(Page * 40))
			{		
			    container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{-497.5 + (x * 100)} {123.25 - (y * 100)}", OffsetMax = $"{-402.5 + (x * 100)} {218.25 - (y * 100)}" },
                    Image = { Color = config.GUI.BlockColor, Material = "assets/icons/greyout.mat" }
                }, ".ItemGUI", ".Item");
				
				container.Add(new CuiElement 
                {
                    Parent = ".Item",
                    Components =
                    {
					    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", item + 150) },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "7.5 7.5", OffsetMax = "-7.5 -7.5" }
                    }
                });
				
				if(StoredDataSkins.ContainsKey(item) && StoredDataSkins[item].Count != 0 && StoredData[player.userID].Skins.ContainsKey(item))
				{
				    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Button = { Color = "0 0 0 0", Command = $"skin_c skin {item}" },
                        Text = { Text = "" }
                    }, ".Item");				    
				
				    if(StoredData[player.userID].Skins[item] != 0)
				        container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-20 5", OffsetMax = "-5 20" },
                            Button = { Color = "1 1 1 0.75", Sprite = "assets/icons/clear.png", Command = $"skin_c clear {item} {z}" },
                            Text = { Text = "" }
                        }, ".Item", $".I + {z}");
				}
				
				x++;
				z++;
				
				if(x == 10)
				{
					x = 0;
					y++;
					
					if(y == 4)
						break;
				}
			}
			
			bool back = Page != 0;
			bool next = config.Category[category].Count > ((Page + 1) * 40);
		   		 		  						  	   		  		 			  	 	 		  	 	 		  			 
			container.Add(new CuiButton 
            {    
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-190 5", OffsetMax = "-100 31.75" },
                Button = { Color = back ? config.GUI.ActiveBackColor : config.GUI.InactiveBackColor, Command = back ? $"page.xskinmenu item back {category} {Page}" : "" },
                Text = { Text = lang.GetMessage("BACK", this, player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-regular.ttf", Color = back ? config.GUI.ActiveBackColorText : config.GUI.InactiveBackColorText }
            }, ".ItemGUI");				 			
			
			container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-95 5", OffsetMax = "-5 31.75" },
                Button = { Color = next ? config.GUI.ActiveNextReloadColor : config.GUI.InactiveNextReloadColor, Command = next ? $"page.xskinmenu item next {category} {Page}" : "" },
                Text = { Text = lang.GetMessage("NEXT", this, player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-regular.ttf", Color = next ? config.GUI.ActiveNextReloadColorText : config.GUI.InactiveNextReloadColorText }
            }, ".ItemGUI");
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 36.75", OffsetMax = "0 41.75" },
                Image = { Color = config.GUI.BColor1, Material = "assets/icons/greyout.mat" }
            }, ".ItemGUI");			
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-200 0", OffsetMax = "-195 36.75" },
                Image = { Color = config.GUI.BColor1, Material = "assets/icons/greyout.mat" }
            }, ".ItemGUI");
			
			if(config.Setting.ButtonClear)
			{
				container.Add(new CuiButton
				{
					RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "5 5", OffsetMax = "190 31.75" },
					Button = { Color = config.GUI.ActiveNextReloadColor, Command = "skin_c clearall" },
					Text = { Text = lang.GetMessage("CLEARALL", this, player.UserIDString), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf", Color = config.GUI.ActiveNextReloadColorText }
				}, ".ItemGUI");
			
				container.Add(new CuiPanel
				{
					RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "195 0", OffsetMax = "200 36.75" },
					Image = { Color = config.GUI.BColor1, Material = "assets/icons/greyout.mat" }
				}, ".ItemGUI");
			}
			
			CuiHelper.AddUi(player, container);
		}		
		
		[ConsoleCommand("skinimage_reload")]
		private void ccmdReloadIMG(ConsoleSystem.Arg args)
		{
			if (args.Player() == null || args.Player().IsAdmin)
				if(_coroutine == null)
					_coroutine = ServerMgr.Instance.StartCoroutine(ReloadImage());
				else
					PrintWarning("Загрузка изображений продолжается. Подождите!");
		}
		
		private void OnItemCraftFinished(ItemCraftTask task, Item item)
		{
			if(task.skinID == 0)
			{
				BasePlayer player = task.owner;
				
				if(!StoredData[player.userID].Skins.ContainsKey(item.info.shortname) || !permission.UserHasPermission(player.UserIDString, "xskinmenu.craft")) return;
				if(!StoredData[player.userID].ChangeSG && StoredData[player.userID].ChangeSC) 
					SetSkinCraftGive(player, item);
			}
		}
		
		[ConsoleCommand("skin_s")]
		private void ccmdSetting(ConsoleSystem.Arg args)
		{
			BasePlayer player = args.Player();
			
			if(!permission.UserHasPermission(player.UserIDString, "xskinmenu.setting")) 
			{
				SendReply(player, lang.GetMessage("NOPERM", this, player.UserIDString));
				return;
			}
			
			if (Cooldowns.ContainsKey(player))
                if (Cooldowns[player].Subtract(DateTime.Now).TotalSeconds >= 0) return;
			
			Effect x = new Effect("assets/bundled/prefabs/fx/notice/loot.drag.grab.fx.prefab", player, 0, new Vector3(), new Vector3());
			
			switch(args.Args[0])
			{
				case "open":
				{
					SettingGUI(player);
					break;
				}				
				case "inventory":
				{
					StoredData[player.userID].ChangeSI = !StoredData[player.userID].ChangeSI;
					SettingGUI(player);
					break;
				}				
				case "entity":
				{
					StoredData[player.userID].ChangeSE = !StoredData[player.userID].ChangeSE;
					SettingGUI(player);
					break;
				}				
				case "craft":
				{
					StoredData[player.userID].ChangeSC = !StoredData[player.userID].ChangeSC;
					SettingGUI(player);
					break;
				}				
				case "clear":
				{
					StoredData[player.userID].ChangeSCL = !StoredData[player.userID].ChangeSCL;
					SettingGUI(player);
					break;
				}				
				case "give":
				{
					StoredData[player.userID].ChangeSG = !StoredData[player.userID].ChangeSG;
					SettingGUI(player);
					break;
				}				
				case "friends":
				{
					StoredDataFriends[player.userID] = !StoredDataFriends[player.userID];
					SettingGUI(player);
					break;
				}
			}
			
			EffectNetwork.Send(x, player.Connection);
			Cooldowns[player] = DateTime.Now.AddSeconds(0.5f);
		}
		
				
				
		private void SendInfo(BasePlayer player, string message)
        {
            player.SendConsoleCommand("gametip.showgametip", message);
            timer.Once(5f, () => player.SendConsoleCommand("gametip.hidegametip"));
        }
		
		private void SetSkinCraftGive(BasePlayer player, Item item)
		{
			if(player == null || item == null) return;
			
			string shortname = item.info.shortname;
			ulong skin = StoredData[player.userID].Skins[shortname];
			
			if(config.Setting.ReskinConfig && !_items.Contains(shortname)) return;
			if (item.skin == skin || config.Setting.Blacklist.Contains(item.skin)) return;
			
			if(errorskins.ContainsKey(skin))
			{
				item.UseItem();
				Item newitem = ItemManager.CreateByName(errorskins[skin]);
				newitem.condition = item.condition;
				newitem.maxCondition = item.maxCondition;
				
				if (item.contents != null)   
					foreach(var module in item.contents.itemList)
					{
						Item content = ItemManager.CreateByName(module.info.shortname, module.amount);
						content.condition = module.condition;
						content.maxCondition = module.maxCondition;
					
						content.MoveToContainer(newitem.contents);
					}	
					
				player.GiveItem(newitem);
			}
			else
		    {
                item.skin = skin; 
                item.MarkDirty();
		   		 		  						  	   		  		 			  	 	 		  	 	 		  			 
                BaseEntity entity = item.GetHeldEntity(); 
                if (entity != null)
                {
                    entity.skinID = skin;
                    entity.SendNetworkUpdate();
				}
			}
		}		
		
				
				
	    internal class Data 
		{
			[JsonProperty("Смена скинов в инвентаре")] public bool ChangeSI = true;
			[JsonProperty("Смена скинов на предметах")] public bool ChangeSE = true;
			[JsonProperty("Смена скинов при крафте")] public bool ChangeSC = true;
			[JsonProperty("Смена скинов в инвентаре после удаления")] public bool ChangeSCL = true;
			[JsonProperty("Смена скинов при попадании в инвентарь")] public bool ChangeSG;
			[JsonProperty("Скины")] public Dictionary<string, ulong> Skins = new Dictionary<string, ulong>();
		}
		
		[ConsoleCommand("page.xskinmenu")]
		private void ccmdPage(ConsoleSystem.Arg args)
		{
			BasePlayer player = args.Player();
			Effect x = new Effect("assets/bundled/prefabs/fx/notice/loot.drag.grab.fx.prefab", player, 0, new Vector3(), new Vector3());
			
			string item = args.Args[2];
			int Page = int.Parse(args.Args[3]);
			
			switch (args.Args[0])
			{
				case "item":
				{
					switch(args.Args[1])
					{
				        case "next":
				        {
				        	ItemGUI(player, item, Page + 1);	
				        	break;
				        }						
				        case "back":
				        {
				        	ItemGUI(player, item, Page - 1);
				        	break;
				        }
					}
					break;
				}				
				case "skin":
				{
					switch(args.Args[1])
					{
				        case "next":
				        {
				        	SkinGUI(player, item, Page + 1);	
				        	break;
				        }						
				        case "back":
				        {
				    	    SkinGUI(player, item, Page - 1);
				    	    break;
				        }
					}
					break;
				}
			}
			
			EffectNetwork.Send(x, player.Connection);
		}
		
		private void SettingGUI(BasePlayer player)
		{
			CuiHelper.DestroyUi(player, ".SettingGUI");
			CuiHelper.DestroyUi(player, ".SkinGUI");
			CuiHelper.DestroyUi(player, ".ItemGUI");
            CuiElementContainer container = new CuiElementContainer();
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-502.5 -270", OffsetMax = "502.5 177.5" },
                Image = { Color = config.GUI.BColor3 }
            }, ".SGUI", ".SettingGUI");
			
			container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-300 -105", OffsetMax = "300 -80" },
                Text = { Text = lang.GetMessage("SETINFO", this, player.UserIDString), Align = TextAnchor.LowerCenter, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "0.750604 0.750604 0.750604 0.4" }
            }, ".SettingGUI");
			
			container.Add(new CuiPanel
            { 
				CursorEnabled = true,
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400 -75", OffsetMax = "400 75" },
                Image = { Color = config.GUI.BColor1, Material = "assets/icons/greyout.mat" }
            }, ".SettingGUI", ".SGUIM");
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" },
                Image = { Color = config.GUI.BColor2 }
            }, ".SGUIM", ".SGUIMM");
			
			Dictionary<string, bool> setting = new Dictionary<string, bool>
			{
				["inventory"] = StoredData[player.userID].ChangeSI,
				["entity"] = StoredData[player.userID].ChangeSE,
				["craft"] = StoredData[player.userID].ChangeSC,
				["clear"] = StoredData[player.userID].ChangeSCL,
				["give"] = StoredData[player.userID].ChangeSG,
				["friends"] = StoredDataFriends[player.userID]
			};
			
			int x = 0, y = 0;
			
			foreach(var s in setting) 
			{		
			    container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{-390 + (x * 392.5)} {25 - (y * 45)}", OffsetMax = $"{-2.5 + (x * 392.5)} {65 - (y * 45)}" },
                    Image = { Color = config.GUI.SettingColor, Material = "assets/icons/greyout.mat" }
                }, ".SGUIMM", ".SM");
				
				container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-190 -15", OffsetMax = "-160 15" },
                    Button = { Color = s.Value ? config.MenuS.CTButton : config.MenuS.CFButton, Sprite = s.Value ? config.MenuS.TButtonIcon : config.MenuS.FButtonIcon, Command = $"skin_s {s.Key}" },
                    Text = { Text = "" }
                }, ".SM");				
				
			    container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-155 -15", OffsetMax = "190 15" },
                    Text = { Text = lang.GetMessage(s.Key, this, player.UserIDString), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 15, Color = "0.750604 0.750604 0.750604 1" }
                }, ".SM");
				
				x++;
				
				if(x == 2)
				{
					x = 0;
					y++;
				}
			}
			
			CuiHelper.AddUi(player, container);
		}  
		public Dictionary<string, List<ulong>> StoredDataSkins = new Dictionary<string, List<ulong>>();
		
				 
	    private void OnServerInitialized()
		{
			PrintWarning("\n-----------------------------\n" +
			"     Author - PUSHOK\n" +
			"     VK - vk.com/pusok_arena\n" + 
			"     Discord - PUSHOK#7907\n" +
			"     Config - v.604\n" + 
			"-----------------------------"); 

			if (Interface.Oxide.DataFileSystem.ExistsDatafile("XSkinMenu/Friends"))
                StoredDataFriends = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, bool>>("XSkinMenu/Friends");			
			if (Interface.Oxide.DataFileSystem.ExistsDatafile("XSkinMenu/Skins"))
                StoredDataSkins = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, List<ulong>>>("XSkinMenu/Skins");			
			
			if(_coroutine == null)
				_coroutine = ServerMgr.Instance.StartCoroutine(LoadImage());	
				
			foreach (var item in ItemManager.GetItemDefinitions())
			{
				var prefab = item.GetComponent<ItemModDeployable>()?.entityPrefab?.resourcePath;
				if (string.IsNullOrEmpty(prefab)) continue;
				 
				var shortPrefabName = Utility.GetFileNameWithoutExtension(prefab);
				if (!string.IsNullOrEmpty(shortPrefabName) && !_shortnamesEntity.ContainsKey(shortPrefabName))
				    _shortnamesEntity.Add(shortPrefabName, item.shortname);
			}
			  
			GenerateItems();
				
			BasePlayer.activePlayerList.ToList().ForEach(OnPlayerConnected);
			timer.Every(180, () => BasePlayer.activePlayerList.ToList().ForEach(SaveData));
			timer.Every(200, () => Interface.Oxide.DataFileSystem.WriteObject("XSkinMenu/Friends", StoredDataFriends));
			
			InitializeLang();
			permission.RegisterPermission("xskinmenu.use", this);
			permission.RegisterPermission("xskinmenu.setting", this);
			permission.RegisterPermission("xskinmenu.craft", this);
			permission.RegisterPermission("xskinmenu.entity", this);
			permission.RegisterPermission("xskinmenu.inventory", this);   
			permission.RegisterPermission("xskinmenu.give", this);
			
			_items = config.Category.Values.SelectMany(x => x).ToList();
		}
		protected override void LoadDefaultConfig() => config = SkinConfig.GetNewConfiguration();
		
		public Dictionary<ulong, string> errorskins = new Dictionary<ulong, string>
		{
			[10180] = "hazmatsuit.spacesuit",
			[10201] = "hazmatsuit.nomadsuit",
			[10207] = "hazmatsuit.arcticsuit",
			[13070] = "rifle.ak.ice",
			[13068] = "snowmobiletomaha",
			[10189] = "door.hinged.industrial.a",
			[13050] = "skullspikes.candles",
			[13051] = "skullspikes.pumpkin",
			[13052] = "skull.trophy.jar",
			[13053] = "skull.trophy.jar2",
			[13054] = "skull.trophy.table",
			[13056] = "sled.xmas", 
			[13057] = "discofloor.largetiles",
			[10198] = "factorydoor", 
			//[] = "sofa.pattern" 
		};
		
		private object OnItemSkinChange(int skinID, Item item, StorageContainer container, BasePlayer player)
		{
			if(config.Setting.RepairBench && config.Setting.Blacklist.Contains(item.skin))
			{
				EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/invite_notice.prefab", player, 0, new Vector3(), new Vector3()), player.Connection);
				
				return false;
			}
			else
				return null;
		}
		
	    private void Unload()
		{
			foreach (BasePlayer player in BasePlayer.activePlayerList)
			{
				SaveData(player);
				CuiHelper.DestroyUi(player, ".GUIS");
			}
			
			if(_coroutine != null) 
				ServerMgr.Instance.StopCoroutine(_coroutine); 
			
			Interface.Oxide.DataFileSystem.WriteObject("XSkinMenu/Friends", StoredDataFriends);
		}
		
		private void CategoryGUI(BasePlayer player, int page = 0)
		{
			CuiHelper.DestroyUi(player, ".SkinBUTTON");
            CuiElementContainer container = new CuiElementContainer();
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-497.5 187.5", OffsetMax = "497.5 222.5" },
                Image = { Color = "0 0 0 0" }
            }, ".SGUI", ".SkinBUTTON");
			
			int x = 0, count = config.Category.Count; 
			
			foreach(var category in config.Category)
			{
				string color = page == x ? config.GUI.ActiveColor : config.GUI.InactiveColor;
				double offset = -(81 * count--) + -(2.5 * count--);

				container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{offset} -17.5", OffsetMax = $"{offset + 162} 17.5" },
                    Button = { Color = config.GUI.CategoryColor, Material = "assets/icons/greyout.mat", Command = $"skin_c category {category.Key} {x}" },
                    Text = { Text = lang.GetMessage(category.Key, this, player.UserIDString), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 18, Color = "0.750604 0.750604 0.750604 1" }
                }, ".SkinBUTTON", ".BUTTON");
 
			    container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMax = "0 1.5" },
                    Image = { Color = color, Material = "assets/icons/greyout.mat" }
                }, ".BUTTON");
				
				x++;
			}
			
			CuiHelper.AddUi(player, container);
		}
		
				 
				
		private SkinConfig config;
		
		private IEnumerator ReloadImage()
		{
			foreach (var category in config.Category)
			    foreach (var item in category.Value)
				{
				    ImageLibrary.Call("AddImage", $"http://api.skyplugins.ru/api/getimage/{item}/{150}", item + 150);
					
					yield return CoroutineEx.waitForSeconds(0.03f);
				}
					
			foreach(var item in StoredDataSkins)
			    foreach(var skin in item.Value)
				{
					ImageLibrary.Call("AddImage", $"http://api.skyplugins.ru/api/getskin/{skin}/150", $"{skin}" + 152);
					
					yield return CoroutineEx.waitForSeconds(0.03f);
				}
				
			_coroutine = null;
			yield return 0;
		}
		
		private void SaveData(BasePlayer player) => Interface.Oxide.DataFileSystem.WriteObject($"XSkinMenu/UserSettings/{player.userID}", StoredData[player.userID]);
		
		private void OnItemAddedToContainer(ItemContainer container, Item item)
		{
			if(item == null) return;
			if(container?.playerOwner != null)
			{
				BasePlayer player = container.playerOwner;
			
				if(player == null || player.IsNpc || !player.userID.IsSteamId() || player.IsSleeping()) return;
				if(config.Setting.ReskinConfig && !_items.Contains(item.info.shortname)) return;
				if(!permission.UserHasPermission(player.UserIDString, "xskinmenu.give") || !StoredData.ContainsKey(player.userID) || !StoredData[player.userID].Skins.ContainsKey(item.info.shortname)) return;
				if(StoredData[player.userID].ChangeSG) 
					SetSkinCraftGive(player, item);
			}
		}
		
				
		private Dictionary<BasePlayer, DateTime> Cooldowns = new Dictionary<BasePlayer, DateTime>();
		
	    private void OnPlayerDisconnected(BasePlayer player)
		{
			if (StoredData.ContainsKey(player.userID)) 
			{   
				SaveData(player);
				StoredData.Remove(player.userID);
			}			
			  
			if (Cooldowns.ContainsKey(player)) 
				Cooldowns.Remove(player);  
		}
		 
				
		private readonly Dictionary<string, string> _shortnamesEntity = new Dictionary<string, string>();
		   		 		  						  	   		  		 			  	 	 		  	 	 		  			 
        private class SkinConfig
        {
			[JsonProperty("Настройки GUI")]
            public GUISetting GUI = new GUISetting();			
			[JsonProperty("Настройка категорий")]
            public Dictionary<string, List<string>> Category = new Dictionary<string, List<string>>();								
			     
			internal class MenuSSetting 
			{ 
				[JsonProperty("Иконка включенного параметра")] public string TButtonIcon;
				[JsonProperty("Иконка вылюченного параметра")] public string FButtonIcon;				
				[JsonProperty("Цвет включенного параметра")] public string CTButton;
				[JsonProperty("Цвет вылюченного параметра")] public string CFButton;
			}			
			
			[JsonProperty("Общие настройки")]
			public GeneralSetting Setting = new GeneralSetting();
			
			internal class GUISetting
			{
				[JsonProperty("Слой UI - [ Overlay - поверх инвентаря | Hud - под инвентарем (для просмотра установленных скинов не закрывая меню) ]")] public string LayerUI = "Overlay";
				[JsonProperty("Обновлять UI страницу после выбора скина")] public bool SkinUP;
				[JsonProperty("Обновлять UI страницу после удаления скина")] public bool DelSkinUP;
				[JsonProperty("Цвет_фон_1")] public string BColor1;
				[JsonProperty("Цвет_фон_2")] public string BColor2;
				[JsonProperty("Цвет_фон_3")] public string BColor3;
				[JsonProperty("Цвет активной категории")] public string ActiveColor;
				[JsonProperty("Цвет неактивной категории")] public string InactiveColor;
				[JsonProperty("Цвет кнопок категорий")] public string CategoryColor;
				[JsonProperty("Цвет кнопок настроек")] public string SettingColor;
				[JsonProperty("Цвет кнопок (иконки)")] public string IconColor;
				[JsonProperty("Цвет блоков предметов/скинов")] public string BlockColor;
				[JsonProperty("Цвет блока выбранного скина")] public string ActiveBlockColor;
				[JsonProperty("Цвет активной кнопки далее/обновить")] public string ActiveNextReloadColor;
				[JsonProperty("Цвет неактивной кнопки далее/обновить")] public string InactiveNextReloadColor;
				[JsonProperty("Цвет текста активной кнопки далее/обновить")] public string ActiveNextReloadColorText;				
				[JsonProperty("Цвет текста неактивной кнопки далее/обновить")] public string InactiveNextReloadColorText;				
				[JsonProperty("Цвет активной кнопки назад")] public string ActiveBackColor;
				[JsonProperty("Цвет неактивной кнопки назад")] public string InactiveBackColor;
				[JsonProperty("Цвет текста активной кнопки назад")] public string ActiveBackColorText;
				[JsonProperty("Цвет текста неактивной кнопки назад")] public string InactiveBackColorText;
			}
			
			public static SkinConfig GetNewConfiguration()
            {
                return new SkinConfig
                {
					Setting = new GeneralSetting
					{
						UpdateSkins = true,
						UpdateSkinsFacepunch = false,
						ButtonClear = true,
						RepairBench = true,
						DeleteButton = true,
						ReskinConfig = false,
						ReskinRespawn = true,
						Blacklist = new List<ulong>
						{
							1742796979,
							841106268
						}
					},
					GUI = new GUISetting
					{
						LayerUI = "Overlay",
						SkinUP = true,
						DelSkinUP = true,
						BColor1 = "0.517 0.521 0.509 0.95",
						BColor2 = "0.217 0.221 0.209 0.95",
						BColor3 = "0.217 0.221 0.209 1",
						ActiveColor = "0.53 0.77 0.35 0.8",
						InactiveColor = "0 0 0 0",
						CategoryColor = "0.517 0.521 0.509 0.5",
						SettingColor = "0.517 0.521 0.509 0.5",
						IconColor = "1 1 1 0.75",
						BlockColor = "0.517 0.521 0.509 0.5",
						ActiveBlockColor = "0.53 0.77 0.35 0.8",
						ActiveNextReloadColor = "0.35 0.45 0.25 1",
						InactiveNextReloadColor = "0.35 0.45 0.25 0.4",
						ActiveNextReloadColorText = "0.75 0.95 0.41 1",						
						InactiveNextReloadColorText = "0.75 0.95 0.41 0.4",
						ActiveBackColor = "0.65 0.29 0.24 1",
						InactiveBackColor = "0.65 0.29 0.24 0.4",						
						ActiveBackColorText = "0.92 0.79 0.76 1",
						InactiveBackColorText = "0.92 0.79 0.76 0.4"
					},
					MenuS = new MenuSSetting
					{
						TButtonIcon = "assets/icons/check.png",
						FButtonIcon = "assets/icons/close.png",
						CTButton = "0.53 0.77 0.35 0.8",
						CFButton = "1 0.4 0.35 0.8"
					},
					Category = new Dictionary<string, List<string>>
					{
						["weapon"] = new List<string> { "gun.water", "pistol.revolver", "pistol.semiauto", "pistol.python", "pistol.eoka", "shotgun.waterpipe", "shotgun.double", "shotgun.pump", "bow.hunting", "crossbow", "grenade.f1", "smg.2", "smg.thompson", "smg.mp5", "rifle.ak", "rifle.lr300", "lmg.m249", "rocket.launcher", "rifle.semiauto", "rifle.m39", "rifle.bolt", "rifle.l96", "longsword", "salvaged.sword", "knife.combat", "bone.club", "knife.bone" },
						["construction"] = new List<string> { "wall.frame.garagedoor", "door.double.hinged.toptier", "door.double.hinged.metal", "door.double.hinged.wood", "door.hinged.toptier", "door.hinged.metal", "door.hinged.wood", "barricade.concrete", "barricade.sandbags" },
						["item"] = new List<string> { "locker", "vending.machine", "fridge", "furnace", "table", "chair", "box.wooden.large", "box.wooden", "rug.bear", "rug", "sleepingbag", "water.purifier", "target.reactive", "sled", "discofloor", "paddlingpool", "innertube", "boogieboard", "beachtowel", "beachparasol", "beachchair", "skull.trophy", "skullspikes", "skylantern" },
						["attire"] = new List<string> { "metal.facemask", "coffeecan.helmet", "riot.helmet", "bucket.helmet", "deer.skull.mask", "twitch.headset", "sunglasses", "mask.balaclava", "burlap.headwrap", "hat.miner", "hat.beenie", "hat.boonie", "hat.cap", "mask.bandana", "metal.plate.torso", "roadsign.jacket", "roadsign.kilt", "roadsign.gloves", "burlap.gloves", "attire.hide.poncho", "jacket.snow", "jacket", "tshirt.long", "hazmatsuit", "hoodie", "shirt.collared", "tshirt", "burlap.shirt", "attire.hide.vest", "shirt.tanktop", "attire.hide.helterneck", "pants", "burlap.trousers", "pants.shorts", "attire.hide.pants", "attire.hide.skirt", "shoes.boots", "burlap.shoes", "attire.hide.boots" },
						["tool"] = new List<string> { "fun.guitar", "jackhammer", "icepick.salvaged", "pickaxe", "stone.pickaxe", "rock", "hatchet", "stonehatchet", "explosive.satchel", "hammer" },
						["transport"] = new List<string> { "snowmobile" }
					}
				};
			}
			[JsonProperty("Меню настроект")]
			public MenuSSetting MenuS = new MenuSSetting();
			internal class GeneralSetting 
			{
				[JsonProperty("Включить кнопку для удаления скинов через UI")] public bool DeleteButton;
				[JsonProperty("Запретить менять скин предмета которого нет в конфиге")] public bool ReskinConfig;
				[JsonProperty("Распространять черный список скинов на ремонтный верстак")] public bool RepairBench;
				[JsonProperty("Сгенерировать/Проверять и добавлять новые скины принятые разработчиками или сделаные для твич дропсов")] public bool UpdateSkins;
				[JsonProperty("Сгенерировать/Проверять и добавлять новые скины добавленные разработчиками [ К примеру скин на хазмат ]")] public bool UpdateSkinsFacepunch;
				[JsonProperty("Отображать кнопку для удаления всех скинов")] public bool ButtonClear;
				[JsonProperty("Изменять скины на предметы после респавна игрока")] public bool ReskinRespawn;
				[JsonProperty("Черный список скинов которые нельзя изменить. [ Например: огненные перчатки, огненный топор ]]")] public List<ulong> Blacklist = new List<ulong>();
			}
        }
		public List<string> _items = new List<string>();
		
				
				
		private void GUI(BasePlayer player)
		{
			CuiHelper.DestroyUi(player, ".GUIS");
            CuiElementContainer container = new CuiElementContainer(); 
			
			container.Add(new CuiPanel
            {
				CursorEnabled = true,
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-507.5 -260", OffsetMax = "507.5 290" },
                Image = { Color = config.GUI.BColor1, Material = "assets/icons/greyout.mat" }
            }, config.GUI.LayerUI, ".GUIS");
			
			container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" },
                Image = { Color = config.GUI.BColor2 }
            }, ".GUIS", ".SGUI");
			
			container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "-2 -2", AnchorMax = "2 2" },
                Button = { Color = "0 0 0 0", Close = ".GUIS" },
                Text = { Text = "" }
            }, ".SGUI");			
			
			container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-497.5 237.5", OffsetMax = "-470 265" },
                Button = { Color = config.GUI.IconColor, Sprite = "assets/icons/gear.png", Command = "skin_s open" },
                Text = { Text = "" }
            }, ".SGUI");
			
			container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-455 237.5", OffsetMax = "455 265" },
                Text = { Text = $"МЕНЮ СКИНОВ <color=orange>BASE RUST</color>", Align = TextAnchor.MiddleCenter, FontSize = 18, Color = "1 1 1 0.750604" }
            }, ".SGUI");
			
			CuiHelper.AddUi(player, container);
			
			CategoryGUI(player);
			if(config.Category.Count != 0) ItemGUI(player, config.Category.ElementAt(0).Key);
		}
		
		private void LoadData(BasePlayer player)
		{ 
            var Data = Interface.Oxide.DataFileSystem.ReadObject<Data>($"XSkinMenu/UserSettings/{player.userID}");
             
            if (!StoredData.ContainsKey(player.userID)) 
                StoredData.Add(player.userID, new Data());            
			if (!StoredDataFriends.ContainsKey(player.userID)) 
                StoredDataFriends.Add(player.userID, false);
  
            StoredData[player.userID] = Data ?? new Data();  
			
			if (StoredData[player.userID].Skins.Count == 0)
			    foreach(var skin in StoredDataSkins) 
					StoredData[player.userID].Skins.Add(skin.Key, 0);
		}  
		
		private void OnPlayerConnected(BasePlayer player)
		{
			if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player));
                return;
            }		
			
			LoadData(player);
		}  
		
				
		 
        private void InitializeLang()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TITLE"] = "COOL SERVER SKINS MENU",
				["SETINFO"] = "YOU CAN CUSTOMIZE THE MENU DEPENDING ON THE SITUATION!",
				["ERRORSKIN"] = "THE SKIN YOU CHOSE CAN BE CHANGED ONLY IN THE INVENTORY OR WHEN CRAFTING!",
				["CLEARALL"] = "UPDATE MY SKIN LIST",
				["NOPERM"] = "No permissions!",
				["NEXT"] = "NEXT",
				["BACK"] = "BACK",
                ["weapon"] = "WEAPON",
                ["construction"] = "CONSTRUCTION",
                ["item"] = "ITEM",
                ["attire"] = "ATTIRE",
                ["tool"] = "TOOL",
                ["transport"] = "TRANSPORT",
                ["inventory"] = "CHANGE SKIN IN INVENTORY",
                ["entity"] = "CHANGE SKIN ON OBJECTS", 
                ["craft"] = "CHANGE SKIN WHEN CRAFTING",
                ["clear"] = "CHANGE SKIN WHEN DELETING",
                ["give"] = "SKIN CHANGE WHEN DROP IN INVENTORY",
				["friends"] = "ALLOW FRIENDS TO CHANGE YOUR SKINS"
            }, this);
		   		 		  						  	   		  		 			  	 	 		  	 	 		  			 
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TITLE"] = "МЕНЮ СКИНОВ КРУТОГО СЕРВЕРА",
                ["SETINFO"] = "ВЫ МОЖЕТЕ КАСТОМНО НАСТРАИВАТЬ МЕНЮ В ЗАВИСИМОСТИ ОТ СИТУАЦИИ!",
                ["ERRORSKIN"] = "ВЫБРАННЫЙ ВАМИ СКИН МОЖНО ИЗМЕНИТЬ ТОЛЬКО В ИНВЕНТАРЕ ИЛИ ПРИ КРАФТЕ!",
                ["CLEARALL"] = "ОБНОВИТЬ МОЙ СПИСОК СКИНОВ",
				["NOPERM"] = "Недостаточно прав!",
				["NEXT"] = "ДАЛЕЕ",
				["BACK"] = "НАЗАД",
                ["weapon"] = "ОРУЖИЕ",
                ["construction"] = "СТРОИТЕЛЬСТВО",
                ["item"] = "ПРЕДМЕТЫ",
                ["attire"] = "ОДЕЖДА",
                ["tool"] = "ИНСТРУМЕНТЫ",
				["transport"] = "ТРАНСПОРТ",
                ["inventory"] = "ПОМЕНЯТЬ СКИН В ИНВЕНТАРЕ",
                ["entity"] = "ПОМЕНЯТЬ СКИН НА ПРЕДМЕТАХ",
                ["craft"] = "ПОМЕНЯТЬ СКИН ПРИ КРАФТЕ",
                ["clear"] = "ПОМЕНЯТЬ СКИН ПРИ УДАЛЕНИИ",
                ["give"] = "ПОМЕНЯТЬ СКИН ПРИ ПОПАДАНИИ В ИНВЕНТАРЬ",
                ["friends"] = "РАЗРЕШИТЬ ДРУЗЬЯМ ИЗМЕНЯТЬ ВАШИ СКИНЫ"
            }, this, "ru");
        }
		
		private void SetSkinTransport(BasePlayer player, BaseVehicle vehicle, string shortname)
		{
			ulong skin = StoredData[player.userID].Skins[shortname];
			
			if(skin == vehicle.skinID || skin == 0) return;
			
			if(errorskins.ContainsKey(skin))
				shortname = errorskins[skin];
			if(errorshortnames.ContainsKey(shortname))
				shortname = errorshortnames[shortname];
			
			BaseVehicle transport = GameManager.server.CreateEntity($"assets/content/vehicles/snowmobiles/{shortname}.prefab", vehicle.transform.position, vehicle.transform.rotation) as BaseVehicle;
			transport.health = vehicle.health;
			transport.skinID = skin;
			
			vehicle.Kill();
			transport.Spawn();
			Effect.server.Run("assets/prefabs/deployable/repair bench/effects/skinchange_spraypaint.prefab", transport.transform.localPosition);
		}
		
				
		[ChatCommand("skin")]
		private void cmdOpenGUI(BasePlayer player) 
		{
			if(!permission.UserHasPermission(player.UserIDString, "xskinmenu.use"))
				SendReply(player, lang.GetMessage("NOPERM", this, player.UserIDString));
			else
			    GUI(player);		
		}
		
		public Dictionary<string, string> errorshortnames = new Dictionary<string, string>
		{
			["snowmobiletomaha"] = "tomahasnowmobile"
		};
				
		[PluginReference] private Plugin ImageLibrary;
		
		private Coroutine _coroutine;
		
		private IEnumerator LoadImage()
		{
			foreach (var category in config.Category)
			    foreach (var item in category.Value)
				{
					if (!ImageLibrary.Call<bool>("HasImage", item + 150))
				        ImageLibrary.Call("AddImage", $"http://api.skyplugins.ru/api/getimage/{item}/{150}", item + 150);
					
					yield return CoroutineEx.waitForSeconds(0.03f);
				}
					
			foreach(var item in StoredDataSkins)
			    foreach(var skin in item.Value)
				{
					if (!ImageLibrary.Call<bool>("HasImage", $"{skin}" + 152))
					    ImageLibrary.Call("AddImage", $"http://api.skyplugins.ru/api/getskin/{skin}/150", $"{skin}" + 152);
					
					yield return CoroutineEx.waitForSeconds(0.03f);
				}
			
			_coroutine = null;
			yield return 0;
		}

        	}
}