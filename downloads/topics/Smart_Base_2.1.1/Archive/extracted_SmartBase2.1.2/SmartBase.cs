using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Game.Rust.Cui;


namespace Oxide.Plugins
{
    [Info("SmartBase", "P1S4G0R", "2.1.2")]
    [Description("Allows players to mark and modify storage boxes.")]
    public class SmartBase : RustPlugin
    {
		
		[PluginReference]
        Plugin NoEscape, Clans, Friends;
		private Dictionary<ulong, StorageContainer> claimedBoxes =
		new Dictionary<ulong, StorageContainer>();
		private ConfigData configData;
		public string panelName = null;
		public string panelButtonName = null;
		public string SmartBasePanelName = "smartbasebackground";
		public string SmartBaseButtonPanelName = "smartbasebuttonbackground";
		public string SmartBaseContainerPanelName = "smartbasecontainerbackground";
		PlayerItemListData playerItemList;
		
		 Dictionary<string, int> itemDictionary = new Dictionary<string, int>();
		
		

			
		class PlayerItemListData
		{
			public Dictionary<ulong, Dictionary<ulong, Dictionary<ulong, ulong>>> Players = new Dictionary<ulong, Dictionary<ulong, Dictionary<ulong, ulong>>>();

			
			public void AddPlayer(ulong steamId)
			{
				
				// Eğer oyuncu zaten mevcut değilse, yeni bir giriş ekle
				if (!Players.ContainsKey(steamId))
				{
					// Yeni bir oyuncu için, boş bir Dictionary ekle
					Players.Add(steamId, new Dictionary<ulong, Dictionary<ulong, ulong>>());
				}
			}
			
			

			public void AddItem(ulong steamId, ulong itemNetId, ulong itemId, ulong itemSkinId)
			{
				// Oyuncunun mevcut olup olmadığını kontrol et
				if (Players.ContainsKey(steamId))
				{
					var playerItems = Players[steamId];
					
					// Eğer itemNetId için bir alt sözlük yoksa oluştur
					if (!playerItems.ContainsKey(itemNetId))
					{
						playerItems[itemNetId] = new Dictionary<ulong, ulong>();
					}
					
					// itemId ve itemSkinId'yi alt sözlüğe ekle
					var itemDetails = playerItems[itemNetId];
					itemDetails[itemId] = itemSkinId;
				}
				else
				{
					// Oyuncu mevcut değilse, oyuncuyu ekle ve ardından eşyayı ekle
					AddPlayer(steamId);
					AddItem(steamId, itemNetId, itemId, itemSkinId);
				}
			}
			
			
 
			public bool  GetItemInfo(ulong steamId, ulong itemNetId)
			{
				if (Players.ContainsKey(steamId) && Players[steamId].ContainsKey(itemNetId))
				{
					return true;
					//return Players[steamId][itemNetId];
				}
				return  false;
				//return  new Dictionary<ulong,ulong>();
			}
			 
						
			public void RemoveItem(ulong steamId, ulong itemNetId)
			{
				if (Players[steamId].ContainsKey(itemNetId))
				{
					Players[steamId].Remove(itemNetId);
				}
			}
			public Dictionary<ulong, Dictionary<ulong, ulong>> GetPlayerItems(ulong steamId)
			{
				// Kullanıcı verileri var mı diye kontrol edin
				if (Players.ContainsKey(steamId))
				{
					// Kullanıcının verilerini döndürün
					return Players[steamId];
				}
				
				// Eğer kullanıcı yoksa null döndür
				return new Dictionary<ulong, Dictionary<ulong, ulong>>();
			}
		}  
		
		private void Init()
        {
			permission.RegisterPermission ("smartbase.use", this);
			permission.RegisterPermission ("smartbase.limit.1", this);
			permission.RegisterPermission ("smartbase.limit.2", this);
			permission.RegisterPermission ("smartbase.limit.3", this);
			permission.RegisterPermission ("smartbase.limit.4", this);
			permission.RegisterPermission ("smartbase.limit.5", this);
			permission.RegisterPermission ("smartbase.limit.6", this);
			permission.RegisterPermission ("smartbase.limit.7", this);
			permission.RegisterPermission ("smartbase.limit.8", this);
			 
			
	        playerItemList = Interface.Oxide.DataFileSystem.ReadObject<PlayerItemListData>(this.Title);
			itemDictionary.Add("box.wooden.large", 833533164);
			itemDictionary.Add("coffinstorage", 573676040);
			itemDictionary.Add("woodbox_deployed", -180129657);
			itemDictionary.Add("cupboard.tool.deployed", -97956382);
			itemDictionary.Add("cupboard.tool.retro.deployed", 1488606552);
			itemDictionary.Add("cupboard.tool.shockbyte.deployed", 1174957864);
			itemDictionary.Add("electricfurnace.deployed", -1196547867);
			itemDictionary.Add("storage_barrel_b", 1307626005);
			itemDictionary.Add("storage_barrel_c", 	-1421257350);

        }
		 
		private class ConfigData
        {
			[JsonProperty(PropertyName = "Plugin Icon Id")]
            public ulong IconId = 0;
			
			[JsonProperty(PropertyName = "Gui Button Enabled")]
            public bool GuiButtonEnabled = true;
		
			[JsonProperty(PropertyName = "Gui Button Image")]
            public string GuiButtonImage = "https://cdn-icons-png.flaticon.com/512/1507/1507052.png";
			
			[JsonProperty(PropertyName = "Button Cordinate AnchorMin")]
            public string ButtonAnchorMin = "0.5 0.0";
			
			[JsonProperty(PropertyName = "Button Cordinate AnchorMax")]
            public string ButtonAnchorMax = "0.5 0.0";
			
			[JsonProperty(PropertyName = "Button Cordinate OffsetMin")]
            public string ButtonOffsetMin = "250 18";
			
			[JsonProperty(PropertyName = "Button Cordinate OffsetMax")]
            public string ButtonOffsetMax = "310 78"; 
			
			[JsonProperty(PropertyName = "Dont Open Container If Player In Building Blocked")]
            public bool CheckBuildingBlocked = true; 
			
			[JsonProperty(PropertyName = "Dont Open container If Player has not Building Privilege")]
            public bool CheckInBuildingPrivilatege = true;
			
			[JsonProperty(PropertyName = "Check Player in Raid Block")]
            public bool CheckPlayerInRaidBlock = false;
			
			[JsonProperty(PropertyName = "Check Player in Combat Block")]
            public bool CheckPlayerInCombatBlock = false;
			
			[JsonProperty(PropertyName = "Use Teams")]
            public bool UseTeams = true;
			
			[JsonProperty(PropertyName = "Use Friends")]
            public bool UseFriends = true;
			
			[JsonProperty(PropertyName = "Use Clans")]
            public bool UseClans = true;
			
			
			
		}
		
		
		protected override void LoadConfig()
        {
		    base.LoadConfig();
            try
            {
                configData = Config.ReadObject<ConfigData>();
				
                if (configData == null)
                {
					LoadDefaultConfig();
                }
	             
            }
            catch (Exception ex)
            {
                PrintError($"The configuration file is corrupted. \n{ex}");
                LoadDefaultConfig();
            }
            SaveConfig();
        } 
		  
		protected override void LoadDefaultConfig()
        {
			
            PrintWarning("Creating a new configuration file");
            configData = new ConfigData();
            
        }
		protected override void LoadDefaultMessages()
        {
			lang.RegisterMessages(new Dictionary<string, string>
            {
				["smartbasetitle"] = "Smart Base Menu",
				["marked_box"] = "{0} is marked.",
				["coffinstorage"] = "Coffin",
				["electricfurnace.deployed"] = "Electical Furnace",
				["box.wooden.large"] = "Large Wooden Box",
				["woodbox_deployed"] = "Small Wooden Box",
				["cupboard.tool.deployed"] = "Cupboard",
				["cupboard.tool.retro.deployed"] = "Retro Cupboard",
				["cupboard.tool.shockbyte.deployed"] = "Shockbyte Cupboard",
				["only_own_item"] = "You can only mark the box you put yourself in.",
				["no_item_in_view_area"] = "There are no markable boxes in your view area.",
				["before_you_must_mark"] = "No box is marked.",
				["open"] = "Open",
				["added_before"] = "This has been added before.",
				["dont_have_permission_this_button"] = "You don't have permission to use this button.",
				["dont_have_permission"] = "You don't have permission to use this command.",
				["cant_use"] = "You can't use.",
				["markboxremoved"] = "{0} remove marked.",
				["max_select_limit"] = "You can select up to {0} boxes.",
				["select_box"] = "Select Box",
				["unselect_box"] = "Unselect Box",
				["box_unmarked"] = "The box is unselected",
				["not_fount_text"] = "Before you must select boxes.",
				["plugin_pre_tag"] = "<color=#FF0000>[Smart Base] :</color>",
				["unmark"] = "Unmark",
				
            }, this, "en"); 
			  
            lang.RegisterMessages(new Dictionary<string, string>
            {
				["smartbasetitle"] = "Akıllı Ev Menüsü",
				["marked_box"] = "{0} işaretlendi.",
				["coffinstorage"] = "Tabut",
				["electricfurnace.deployed"] = "Elektirikli Fırın",
				["box.wooden.large"] = "Büyük Ahşap Kutu",
				["woodbox_deployed"] = "Küçük Ahşap Kutu",
				["cupboard.tool.deployed"] = "Tc",
				["cupboard.tool.retro.deployed"] = "Retro Tc",
				["cupboard.tool.shockbyte.deployed"] = "Shockbyte Tc",
				["only_own_item"] = "Sadece kendi koyduğunuz kutuyu işaretleyebilirsiniz.",
				["no_item_in_view_area"] = "Görüş alanınızda işaretlenebilecek bir item yok.",
				["before_you_must_mark"] = "Önce işaretle",
				["open"] = "Aç",
				["max_select_limit"] = "En fazla {0} Kutuyu işaretleyebilirsiniz.",
				["added_before"] = "Bu daha önce eklendi.",
				["dont_have_permission"] = "Bu komutu kullanmaya yetkiniz yok.",
				["dont_have_permission_this_button"] = "Bu butonu kullanmaya yetkiniz yok.",
				["markboxremoved"] = "{0} Kutu işaretlenmesi kaldırıldı.",
				["select_box"] = "İşaretle",
				["unselect_box"] = "Kaldır",
				["box_unmarked"] = "İşaret Kaldırıldı.",
				["not_fount_text"] = "Önce Kutuları İşaretlemelisiniz.",
				["plugin_pre_tag"] = "<color=#FF0000>[Akıllı Ev] :</color>",
				["unmark"] = "İşareti Kaldır",				
            }, this, "tr");
             
        }
		private bool GetConfigValue<T>(out T value, params string[] path)
        {
            var configValue = Config.Get(path);
            if (configValue == null)
            {
                value = default(T);
                return false;
            }
            value = Config.ConvertValue<T>(configValue);
            return true;
        }
		
		private string langWithParameter(string langString, params object[] obj)
		{

			return string.Format(langString, obj);
		}
		
		public string getLangString(BasePlayer player, string langKey,  params object[] obj) 
		{
			return lang.GetMessage("plugin_pre_tag", this, player.UserIDString) + " " +  langWithParameter(lang.GetMessage(langKey, this, player.UserIDString), obj);
		
		}
		public string getLangStringWithoutPreTag(BasePlayer player, string langKey, params object[] obj)
		{
			return langWithParameter(lang.GetMessage(langKey, this, player.UserIDString),obj);
		}
		public string getLangStringGui(BasePlayer player, string langKey)
		{
			return lang.GetMessage(langKey, this, player.UserIDString);
		}
		void SendChatMessage(BasePlayer player, string langKey= "", params object[] obj)
		{
			if (player == null ) return;
			string chatMessage = getLangString(player, langKey, obj);
			Player.Reply(player, chatMessage, configData.IconId);
					
			 
		}
		private bool IsInBuildingPrivilege(BasePlayer player)
		{ 
			
			BuildingPrivlidge buildingPrivilege = player.GetBuildingPrivilege();
			if (buildingPrivilege != null)
			{
				foreach (ProtoBuf.PlayerNameID playerNameID in buildingPrivilege.authorizedPlayers)
				{
					if (playerNameID.userid == player.userID)
					{
						Puts("Player is authorized in building privilege.");
						return true;
					}
				}
			}

			//Puts("Player is not authorized in building privilege.");
			return false;
		}
		
		
		[ChatCommand("smartbase")]
		private void DiscordMenuComamnd(BasePlayer player)
		{
			if (!permission.UserHasPermission(player.UserIDString,"smartbase.use"))
			{
				 SendChatMessage(player, "dont_have_permission");
				 return;
			}
			createMenu(player);
		}
		void OnLootEntity(BasePlayer player, BaseEntity entity)
		{
			
			
			if(AreFriends(entity.OwnerID,player.userID))
			{
				
				
				if (itemDictionary.TryGetValue(entity.ShortPrefabName, out int coffinItemId))
				{
					NetworkableId networkableId = entity.net.ID;
					ulong idAsUlong = networkableId.Value;
			
					
					createMarkUnmarkButton(player, idAsUlong, (ulong)coffinItemId, entity.skinID, entity.ShortPrefabName );
				}
				
				
			 	
			}
		}
		void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
		{
			CuiHelper.DestroyUi(player, SmartBaseContainerPanelName); 
		}
		
		[ConsoleCommand("removemarkboxbutton")]
		private void RemoveMarkBoxButton(ConsoleSystem.Arg args)
		{
			// Oyuncu nesnesini al
			var player = args.Player();
			
			// args.Args[0]'ın türünü kontrol et ve uygun bir dönüşüm yap
			if (args.Args.Length < 1)
			{
				SendChatMessage(player, "Missing arguments");
				return;
			}
			
			// İlk argümanın itemNetId olduğuna ve ikincisinin itemId olduğuna varsayıyoruz
			if (!ulong.TryParse(args.Args[0], out ulong itemNetId))
			{
				SendChatMessage(player, "Invalid itemNetId format");
				return;
			}

			
			
			// Item'ı oyuncu listesinde kaldır
			playerItemList.RemoveItem(player.userID, itemNetId);
			
			// UI'ları temizle
			CuiHelper.DestroyUi(player, SmartBasePanelName);
			CuiHelper.DestroyUi(player, "SmartBasePanelNew");
			
			// Menü oluştur
			createMenu(player);
		}
		
		[ChatCommand("removemarkbox")] 
        private void RemoveMarkBoxCommand(BasePlayer player, string command, string[] args)
        {
			RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit))
            {
                var entity = hit.GetEntity();
			
				if (entity != null && entity is StorageContainer)
                {
					
					var storage = entity as StorageContainer;
					if((storage as BaseEntity).OwnerID !=  player.userID)
					{
						SendChatMessage(player, "only_own_item");
						return;
					}
					if(playerItemList.GetItemInfo(player.userID, entity.net.ID.Value)) 
					{
						playerItemList.RemoveItem(player.userID, entity.net.ID.Value);
						
						SaveData(); 
					}
				}
				
			}
		}
		
		
		private int CheckPlayerLimit(BasePlayer player)
		{
			
			if (permission.UserHasPermission(player.UserIDString,"smartbase.limit.8"))
				return 8;
			if (permission.UserHasPermission(player.UserIDString,"smartbase.limit.7"))
				return 7;
			if (permission.UserHasPermission(player.UserIDString,"smartbase.limit.6"))
				return 6;
			if (permission.UserHasPermission(player.UserIDString,"smartbase.limit.5"))
				return 5;
			if (permission.UserHasPermission(player.UserIDString,"smartbase.limit.4"))
				return 4;
			if (permission.UserHasPermission(player.UserIDString,"smartbase.limit.3"))
				return 3;
			if (permission.UserHasPermission(player.UserIDString,"smartbase.limit.2"))
				return 2;
			if (permission.UserHasPermission(player.UserIDString,"smartbase.limit.1"))
				return 1;
			return 0;
			
		}
		
		[ChatCommand("markbox")] 
        private void MarkBoxCommand(BasePlayer player, string command, string[] args)
        {
			 
			 
			 if (!permission.UserHasPermission(player.UserIDString,"smartbase.use"))
			 {
				 SendChatMessage(player, "dont_have_permission");
				 return;
			 }
			 
			 
            

            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit))
            {
                var entity = hit.GetEntity();
				
				
			
			
				if (entity != null && entity is StorageContainer)
                {
					
					var storage = entity as StorageContainer;
					if((storage as BaseEntity).OwnerID !=  player.userID)
					{
						SendChatMessage(player, "only_own_item");
						return;
					}
					
					if (playerItemList.Players.ContainsKey(player.userID))
					{
						
					}else
					{
						playerItemList.AddPlayer(player.userID);
					}
					
					int limitPlayer = CheckPlayerLimit(player);

					if(limitPlayer <= playerItemList.Players[player.userID].Count )
					{
						if(limitPlayer == 0)
						SendChatMessage(player, "dont_have_permission_this_button");
						else
						SendChatMessage(player, "max_select_limit", limitPlayer.ToString());
						return;
					} 
					
					NetworkableId networkableId = entity.net.ID;
					ulong idAsUlong = networkableId.Value;
					
					 
					
					string prefabName = entity.ShortPrefabName;	 

					if(playerItemList.GetItemInfo(player.userID, idAsUlong))
					{
						
						
						SendChatMessage(player, "added_before", getLangStringWithoutPreTag(player, prefabName));
						return;
					}else{

						
				
					
						if (itemDictionary.TryGetValue(prefabName, out int coffinItemId))
						{
							playerItemList.AddItem(player.userID, idAsUlong,  (ulong)coffinItemId, (storage as BaseEntity).skinID);
							SendChatMessage(player, "marked_box", getLangStringWithoutPreTag(player, prefabName));
						   
						}else
						{
						   Puts("bulunmadı");
						}
						
						
											
					}
					
					
					
					SaveData(); 
					
					
				}else{
				 SendChatMessage(player, "no_item_in_view_area");
				}
				
            }
            else
            {
                SendChatMessage(player, "no_item_in_view_area");
            }
        }
		
		[ConsoleCommand("UnMarkBoxButtonCommand")]
		private void UnMarkBoxButtonCommand(ConsoleSystem.Arg args)
		{
			 
			 var player = args.Player();
			if (player == null)
			{
				return;
			}
			
			 if (!permission.UserHasPermission(player.UserIDString,"smartbase.use"))
			 {
				 SendChatMessage(player, "dont_have_permission");
				 return;
			 }
			 
			 if (!ulong.TryParse(args.Args[0], out ulong itemNetId))
			{
				SendChatMessage(player, "Invalid itemNetId format");
				return;
			}
			 
			 playerItemList.RemoveItem(player.userID, itemNetId);
			 
			 SendChatMessage(player, "box_unmarked");
			 return;
		}
		[ConsoleCommand("MarkBoxButtonCommand")]
		private void MarkBoxButtonCommand(ConsoleSystem.Arg arg)
		{
			 
			 var player = arg.Player();
			if (player == null)
			{
				return;
			}
			
			 if (!permission.UserHasPermission(player.UserIDString,"smartbase.use"))
			 {
				 SendChatMessage(player, "dont_have_permission");
				 return;
			 }
			
			int limitPlayer = CheckPlayerLimit(player);
			if (playerItemList.Players.ContainsKey(player.userID))
			{
				
			}else
			{
				playerItemList.AddPlayer(player.userID);
			}
			
			if(limitPlayer <= playerItemList.Players[player.userID].Count )
			{ 
				if(limitPlayer == 0)
					SendChatMessage(player, "dont_have_permission_this_button");
				else
					SendChatMessage(player, "max_select_limit", limitPlayer.ToString());
				return;
			} 
			
			if (ulong.TryParse(arg.Args[0], out ulong netId) && ulong.TryParse(arg.Args[1], out ulong itemId) && ulong.TryParse(arg.Args[2], out ulong skinID) )
			{ 
               
				var entity = BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as BaseEntity;
         
				playerItemList.AddItem(player.userID, netId,  itemId, skinID); 
											  
				SendChatMessage(player, "marked_box", getLangStringWithoutPreTag(player, arg.Args[3])); 
					 
					SaveData(); 
				 	
					
				
				
            }
            else
            {
                SendChatMessage(player, "no_item_in_view_area");
            }
        }
		
		
		private void openBox(BasePlayer player, ulong ItemNetId)
		{
			
			int limitPlayer = CheckPlayerLimit(player);

			if(limitPlayer == 0 )
			{
				return;
			}
			
			if (NoEscape != null)
            {
                // NoEscapePlugin içindeki IsBlocked metodunu çağırın
                bool isBlocked = (bool)NoEscape.Call("IsBlocked", player);
                bool isRaidBlocked = (bool)NoEscape.Call("IsRaidBlocked", player);

                // Kontrol sonucunu işleyin
                if (configData.CheckPlayerInCombatBlock && isBlocked) return;
                if (configData.CheckPlayerInRaidBlock && isRaidBlocked) return;
                
               
			}
 


			if(configData.CheckInBuildingPrivilatege)
			{
				//Puts("tc yetkisi sorgusu açık");
				if(!IsInBuildingPrivilege(player))
					return;
				
			}
			
			if(configData.CheckBuildingBlocked)
			{
				//Puts("buildingblocked sorgusu açık");
				if(!player.CanBuild())
					return;
				
			}
			
				
			
			if(!playerItemList.Players.ContainsKey(player.userID))
				return;
			
			if(playerItemList.Players[player.userID].ContainsKey(ItemNetId))
			{
			
				NetworkableId networkableId = new NetworkableId(ItemNetId);

				
				var storage = BaseNetworkable.serverEntities.Find(networkableId) as StorageContainer;
				
				if (storage != null) 
				{
					
						timer.In (0.1f, () => { 
							
							storage.PlayerOpenLoot (player, "", false); 
						
						});
					
					
				}
				
			}
		}
		 
		
		private bool checkBox(BasePlayer player, ulong ItemNetID)
		{
				 
			try
			{
				if(playerItemList.Players.ContainsKey(player.userID) || playerItemList.Players[player.userID].ContainsKey(ItemNetID))
				{
					
					
					NetworkableId networkableId = new NetworkableId(ItemNetID);
					var storage = BaseNetworkable.serverEntities.Find(networkableId) as StorageContainer;
					if (storage != null)
					{
						return true;
					}
					
				}
				return false; 
			}catch (Exception ex)
			{
			
				return false;
			}
		}
	
		
		void SaveData() 
        { 
		    Interface.Oxide.DataFileSystem.WriteObject(this.Title, playerItemList);
		}
		
		
		[ConsoleCommand("OpenBoxClickCommand")]
        private void OpenBoxClickCommand(ConsoleSystem.Arg args)
        {
			var player = args.Player();
						
			ulong netId;
			 
			if (ulong.TryParse(args.Args[0], out netId))
			{ 
				openBox(player, netId);
			}
			DestroySmartbaseMenu2(player);
			
		}
		  
		public CuiElement GuiImageSkinId(string name, string parent, int itemId, ulong skinId = 0, string anchorMin = "0.15 0.3", string anchorMax = "0.95 0.85", string color ="1 1 1 1")
		{
			return new CuiElement
			{
				Name = name,
				Parent = parent,
				Components =
			{
				new CuiImageComponent
				{
					ItemId = itemId,
					SkinId = skinId
				},
				new CuiRectTransformComponent
				{
					AnchorMin = anchorMin,
					AnchorMax = anchorMax
				}
			}
				
			}; 
		}
		
		public CuiElement GuiImage(string name, string parent, string url, string anchorMin = "0.15 0.3", string anchorMax = "0.95 0.85", string color ="1 1 1 1")
		{
			return new CuiElement
			{
				Name = name,
				Parent = parent,
				Components =
				{
					new CuiRawImageComponent 
					{
						Color = color,
						Url= url
					},
					new CuiRectTransformComponent 
					{
						AnchorMin = anchorMin,
						AnchorMax = anchorMax
						
					},
				},
			}; 
		}
		
		
		


		
		public CuiButton GuiButton(BasePlayer player, string command, string langKey, string anchorMin= "0.1 0.1", string anchorMax = "0.95 0.2")
		{
			return new CuiButton
			{
				Button =
				{
					Command = command, // Parametre komutla birlikte gönderiliyor
					Color = "0.3 0.6 0.8 0.6",
					FadeIn = 0.4f
				},
				RectTransform =
				{
					AnchorMin = anchorMin,
					AnchorMax = anchorMax
				},
				Text =
				{
					Text = getLangStringWithoutPreTag(player, langKey),
					FontSize = 16,
					Align = TextAnchor.MiddleCenter
				}
			};
		}
		
		public CuiElement GuiElement(string name, string parent, string anchorMin, string anchorMax, string color=  "0.2 0.2 0.2 1" )
		{
			return 	new CuiElement
			{
				Name = name,
				Parent = parent,
				Components =
				{
					new CuiImageComponent
					{
						Color = color
					},
					new CuiRectTransformComponent
					{
						AnchorMin = anchorMin,
						AnchorMax = anchorMax
					}
				}
			};
			
		}
		
		private void createMarkUnmarkButton(BasePlayer player, ulong containerId, ulong itemId, ulong skinID, string prefabName)
		{
			CuiElementContainer panelButton = new CuiElementContainer();


			var background = new CuiElement
			{
				Name = SmartBaseContainerPanelName,
				Parent = "Overlay",
				Components =
				{
					new CuiImageComponent { Color = "0 0 0 0" },
					new CuiRectTransformComponent
					{
						AnchorMin = "0.5 0.0",
						AnchorMax = "0.5 0.0",
						OffsetMin = "570 110",  // iki slotluk yer
						OffsetMax = "680 160"
					}
				}
			};
			panelButton.Add(background);


			CuiButton CreateOffsetButton(string command, string text, int offsetX, int offsetY = 18, int width = 60, int height = 29)
			{
				return new CuiButton
				{
					Button = {
						Command = command,
						Color = "0.969 0.922 0.882 0.1",
						FadeIn = 0.4f
					},
					RectTransform = {
						AnchorMin = "0.0 0.0",
						AnchorMax = "0.0 0.0",
						OffsetMin = $"{offsetX} {offsetY}",
						OffsetMax = $"{offsetX + width} {offsetY + height}"
					},
					Text = {
						Text = text,
						FontSize = 12,
						Align = TextAnchor.MiddleCenter
					}
				};
			}

			// Butonlar
			var markButton = CreateOffsetButton(
				$"MarkBoxButtonCommand {containerId} {itemId} {skinID} {prefabName}",
				getLangStringWithoutPreTag(player, "select_box"),
				0,   // X
				30   // Y → üstte
			);

			var unmarkButton = CreateOffsetButton(
				$"UnMarkBoxButtonCommand {containerId}",
				getLangStringWithoutPreTag(player, "unselect_box"),
				0,   // X
				0    // Y → altta
			);

			panelButton.Add(unmarkButton, SmartBaseContainerPanelName);
			panelButton.Add(markButton, SmartBaseContainerPanelName);

			CuiHelper.AddUi(player, panelButton);
		}


	
		private void createGuiButton(BasePlayer player)
		{
			if (!configData.GuiButtonEnabled)
			{
				 return;
			} 
			
			if (!permission.UserHasPermission(player.UserIDString, "smartbase.use"))
				return;

			CuiElementContainer panelButton = new CuiElementContainer();

			var bgElement = new CuiElement
			{
				Name = SmartBaseButtonPanelName,
				Parent = "Overlay",
				Components =
				{
					new CuiImageComponent
					{
						Color = "0.969 0.922 0.882 0.135"
					},
					new CuiRectTransformComponent
					{
						AnchorMin = configData.ButtonAnchorMin,  // alt orta referans
						AnchorMax = configData.ButtonAnchorMax,
						OffsetMin = configData.ButtonOffsetMin,   // 1 slot sağa kaydırıldı
						OffsetMax = configData.ButtonOffsetMax,
					}
				}
			};

			panelButton.Add(bgElement);

			panelButton.Add(GuiElement("GuiButton", SmartBaseButtonPanelName, "0 0", "1 1", "0.969 0.922 0.882 0.035"));
			panelButton.Add(GuiImage("GuiButtonImage", "GuiButton", configData.GuiButtonImage, "0 0", "1 1", "1 1 1 1"));

			var buttonClick = new CuiButton
			{
				Button = {
					Command = "OpenSmartMenuButton",
					Color = "0 0 0 0",
					FadeIn = 0.4f
				},
				RectTransform = {
					AnchorMin = "0 0",
					AnchorMax = "1 1"
				},
				Text = {
					Text = "",
					FontSize = 20,
					Align = TextAnchor.MiddleCenter
				}
			};

			panelButton.Add(buttonClick, SmartBaseButtonPanelName);

			CuiHelper.AddUi(player, panelButton);
		}





 
		
		private void createMenu(BasePlayer player)
		{
			
			if (!permission.UserHasPermission(player.UserIDString,"smartbase.use"))
			{
				 
				 return;
			}
			DestroySmartbaseMenu2(player);
			
			CuiElementContainer panel = new CuiElementContainer()
			{
				{
					 new CuiPanel
                        {
                            Image = {
								Color = "0.969 0.922 0.882 0.135", 
								Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
								},
                            //RectTransform = {AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax()},
                            CursorEnabled = true
                        },
					
					new CuiElement().Parent = "Hud",
					"SmartBasePanelNew".ToString()
				}
				 
			}; 


			var bgElement = new CuiElement
			{
				Name = SmartBasePanelName,

				Parent = "Overlay",
				Components =
				{
					new CuiImageComponent
					{
						Color = "0.1 0.1 0.1 0.7"
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0.10 0.10",
						AnchorMax = "0.90 0.90"
					}
				}
			};
			panel.Add(bgElement);

			var titleElement = new CuiElement
			{
				Name = "Title",
			
				Parent = SmartBasePanelName,

				Components =
				{
					new CuiTextComponent
					{ 
						Text = getLangStringWithoutPreTag(player, "smartbasetitle"), 
						FontSize = 18, 
						Align = TextAnchor.MiddleCenter 
					},
					new CuiRectTransformComponent 
					{ 
						AnchorMin = "0 0.9",
						AnchorMax = "1 1" 
					}
				} 
			};
			panel.Add(titleElement);
			
			var playerItems = new Dictionary<ulong, Dictionary<ulong, ulong>>(playerItemList.GetPlayerItems(player.userID)); 
			int countContainer = 0; 
			
			if(playerItems is null)
				countContainer = 0;
			else 	
				countContainer = playerItems.Count;
			var LimitElement = new CuiElement
			{
				Name = "Title",
			
				Parent = SmartBasePanelName,

				Components =
				{
					new CuiTextComponent
					{ 
						Text = $"{countContainer}/{CheckPlayerLimit(player)}",  
						FontSize = 16, 
						Align = TextAnchor.MiddleRight 
					},
					new CuiRectTransformComponent 
					{ 
						AnchorMin = "0 0.9",
						AnchorMax = "0.95 1" 
					}
				} 
			};
			panel.Add(LimitElement);
			var ListBox = new CuiElement
				{
					Name = "ListBox",

					Parent = SmartBasePanelName,
					Components =
					{
						new CuiImageComponent
						{
							Color = "0 0 0 0" 
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0.05 0.01",
							AnchorMax = "0.95 0.90" 
						}
					}
				}; 
				
				
				
				panel.Add(ListBox);
			
			

			if (playerItems != null)
			{
				double rowMultiple = (double)2 / 4;
				double columnMultiple = 0.25f;
				double x1 = 0.01;
				double x2 = 0.50;
				double x3 = 0.19;
				double x4 = 0.9;
				double xx1 = 0.01;
				double xx2 = 0.50;
				double xx3 = 0.19;
				double xx4 = 0.9;
				int k = 0;
				
				foreach (var itemNetId in playerItems)
				{
					
				
				var ItemPostion = FindElementPosition(k, 4, 2, rowMultiple, columnMultiple);
				
				
					
					x1= xx1 + k * 0.20f;
					x3= xx3 + k * 0.20f; 
					
					
					foreach (var itemId in itemNetId.Value)
					{ 
					
						if(checkBox(player,  itemNetId.Key))
						 {
						
							var TcBG = GuiElement($"{itemNetId.Key.ToString()}", "ListBox", $"{ItemPostion.B1} {ItemPostion.A1}", $"{ItemPostion.C1} {ItemPostion.D1}");
							var TcButton = GuiButton(player,
							$"OpenBoxClickCommand {itemNetId.Key}",
							getLangStringWithoutPreTag(player,"open"));
							
							var CupboardImage = GuiImageSkinId("CupboardImage", $"{itemNetId.Key.ToString()}", (int)itemId.Key, itemId.Value);
							
							//var CupboardImage = GuiImageSkinId("CupboardImage", $"{itemNetId.Key.ToString()}", 833533164, 813269955);
							
							panel.Add(TcBG);
							panel.Add(TcButton, $"{itemNetId.Key.ToString()}");
							
							var RemoveMarkButton = GuiButton(player, $"removemarkboxbutton {itemNetId.Key}", getLangStringWithoutPreTag(player,"unmark"), "0.1 0.85", "0.95 0.95");
							panel.Add(RemoveMarkButton, $"{itemNetId.Key}");
							panel.Add(CupboardImage);
							
							
							k++;
						}else
						{
							playerItemList.RemoveItem(player.userID, itemNetId.Key);
						}
					} 
					
				}
				if(k == 0)
				{
					var not_fount_text = new CuiElement
					{
						Name = "not_found",
					
						Parent = SmartBasePanelName,

						Components =
						{
							new CuiTextComponent
							{ 
								Text = getLangStringWithoutPreTag(player, "not_fount_text"), 
								FontSize = 18, 
								Align = TextAnchor.MiddleLeft 
							},
							new CuiRectTransformComponent 
							{ 
								AnchorMin = "0.05 0.8",
								AnchorMax = "1 0.9" 
							}
						}
					};
					
					panel.Add(not_fount_text);
				}
				
			}
			else
			{
				var not_fount_text = new CuiElement
				{
					Name = "not_found",
				
					Parent = SmartBasePanelName,

					Components =
					{
						new CuiTextComponent
						{ 
							Text = getLangStringWithoutPreTag(player, "not_fount_text"), 
							FontSize = 18, 
							Align = TextAnchor.MiddleLeft 
						},
						new CuiRectTransformComponent 
						{ 
							AnchorMin = "0.05 0.8",
							AnchorMax = "1 0.9" 
						}
					}
				};
				
				panel.Add(not_fount_text);
			 	
			} 
			
			
			var buttonx = new CuiButton
            {
                Button = {
                    Command ="DestroySmartbaseMenu", 
                    Color = "1 0 0 1", 
                    FadeIn = 0.4f 
                },
                RectTransform = {
                    AnchorMin = "0.96 0.93", 
                    AnchorMax = "0.99 0.99" 
                },
                Text = {
                    Text = "x", 
                    FontSize = 20, 
                    Align = TextAnchor.MiddleCenter 
                }
            };
 
 
			panel.Add(buttonx, SmartBasePanelName);
			
			CuiHelper.AddUi(player, panel);
		}
		[ConsoleCommand("DestroySmartbaseMenu")]
        private void DestroySmartbaseMenu(ConsoleSystem.Arg args)
        {
			
            var player = args.Player();

			CuiHelper.DestroyUi(player, SmartBasePanelName); 
			
			CuiHelper.DestroyUi(player, "SmartBasePanelNew"); 
			
 
        }
		
		[ConsoleCommand("OpenSmartMenuButton")]
        private void OpenSmartMenuButton(ConsoleSystem.Arg args)
        {
			
            var player = args.Player();

			createMenu(player);
			
			

        }
		 
		private void DestroySmartbaseMenu2(BasePlayer player)
        {
			
           

			CuiHelper.DestroyUi(player, SmartBasePanelName); 
			
			CuiHelper.DestroyUi(player, "SmartBasePanelNew"); 
			
			

        }
		[ConsoleCommand("DestroySmartbaseGuiButton")]
        private void DestroySmartbaseGuiButton(ConsoleSystem.Arg args)
        {
			
            var player = args.Player();
 
 
			CuiHelper.DestroyUi(player, SmartBaseButtonPanelName); 

			

        }
		 private void DestroySmartbaseGuiButton2(BasePlayer player)
        {
			
			
             

			CuiHelper.DestroyUi(player, SmartBaseButtonPanelName); 

			
  
        }
		private void OnPlayerConnected(BasePlayer player) => createGuiButton(player);
		protected override void SaveConfig() => Config.WriteObject(configData);
		void Loaded()
		{
			  
			foreach(var player  in BasePlayer.activePlayerList)
			{
				
				
				createGuiButton(player); 
				//createMenu(player);
				
    
			}
		}
		void Unload()
        {
			foreach(var player  in BasePlayer.activePlayerList)
			{
				
				CuiHelper.DestroyUi(player, SmartBasePanelName); 
				
				CuiHelper.DestroyUi(player, "SmartBasePanelNew"); 
				CuiHelper.DestroyUi(player, SmartBaseButtonPanelName); 
				CuiHelper.DestroyUi(player, SmartBaseContainerPanelName); 
			
				SaveData(); 
				

			}
        }
		
		public PostionItem  FindElementPosition(int itemIndex, int columCount, int rowCount, double rowMultiple, double columnMultiple)
       {
            double blankPadding = 0.01;
            int gridCount = columCount * rowCount;
            int item = itemIndex % gridCount;
			item = gridCount - item -1;
			
			
            int rowNumber = item / columCount;


            int columnNumber = item % columCount;
			columnNumber = columCount - columnNumber -1; 
			
			
            double A1, B1, C1, D1;

         
            A1 = Math.Round((rowNumber * rowMultiple),2);
            D1 = Math.Round((A1 + rowMultiple - blankPadding),2);
            A1 = Math.Round((A1 + blankPadding),2);

            B1 = Math.Round((columnNumber * columnMultiple), 2);
            C1 = Math.Round((B1 + columnMultiple - blankPadding), 2);
            B1 = Math.Round((B1 + blankPadding), 2);

			
			return new PostionItem(B1, A1, C1, D1);
        }
		
	 	 
		public class PostionItem
		{
			public string B1, A1, C1, D1;
			public  PostionItem(double BB1, double AA1 , double CC1, double DD1)
			{
				this.B1 = BB1.ToString();
				this.A1 = AA1.ToString();
				this.C1 = CC1.ToString();
				this.D1 = DD1.ToString();
			}
			
		}
		
		
		 #region AreFriends

        private bool AreFriends(ulong playerId, ulong friendId)
        {
            if (!playerId.IsSteamId())
            {
                return false;
            }
            if (playerId == friendId)
            {
                return true;
            }
            if (configData.UseTeams && SameTeam(playerId, friendId))
            {
                return true;
            }
            if (configData.UseFriends && HasFriend(playerId, friendId))
            {
                return true;
            }
            if (configData.UseClans && SameClan(playerId, friendId))
            {
                return true;
            }
            return false;
        }

        private static bool SameTeam(ulong playerId, ulong friendId)
        {
            if (!RelationshipManager.TeamsEnabled())
            {
                return false;
            }
            var playerTeam = RelationshipManager.ServerInstance.FindPlayersTeam(playerId);
            if (playerTeam == null)
            {
                return false;
            }
            var friendTeam = RelationshipManager.ServerInstance.FindPlayersTeam(friendId);
            if (friendTeam == null)
            {
                return false;
            }
            return playerTeam == friendTeam;
        }

        private bool HasFriend(ulong playerId, ulong friendId)
        {
            if (Friends == null)
            {
                return false;
            }
            return (bool)Friends.Call("HasFriend", playerId, friendId);
        }

        private bool SameClan(ulong playerId, ulong friendId)
        {
            if (Clans == null)
            {
                return false;
            }
            //Clans
            var isMember = Clans.Call("IsClanMember", playerId.ToString(), friendId.ToString());
            if (isMember != null)
            {
                return (bool)isMember;
            }
            //Rust:IO Clans
            var playerClan = Clans.Call("GetClanOf", playerId);
            if (playerClan == null)
            {
                return false;
            }
            var friendClan = Clans.Call("GetClanOf", friendId);
            if (friendClan == null)
            {
                return false;
            }
            return (string)playerClan == (string)friendClan;
        }

        #endregion AreFriends 
    }
}
