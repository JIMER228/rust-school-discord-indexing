//Reference: Facepunch.Sqlite
//Reference: UnityEngine.UnityWebRequestModule
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace Oxide.Plugins
{	
	/* Based on version 2.0.45 by Absolut & K1lly0u */
	
	// продумать калбеки, что бы всегда вызывались ? Сейчас они не всегда будут вызыватся, например если картинка уже есть или в случаях ошибок	
	// продумать кеш для клиента, добавить ли апи инициализации картинок на клиенте ?
	
	// !!! В ПЛАГИНЕ ИСПОЛЬЗУЕТСЯ МОЙ STEAM API KEY. ПЛАГИН НЕ ПЕРЕДАВАТЬ НИКОМУ !!!
	
	/*
		Правило пользования плагином:
		 - добавить нужные свои картинки заранее на OnServerInitialization, желательно сделав либо задержку (что бы ImageLibrary успел инициализироватся), 
		   либо в цикле ждать IsReady и только после этого добавлять картинки
		 - на те скины которые нужны в работе (кроме стандартных) следует заранее сделать на них GetImage, что бы они прогрузились в кеш сервера
		 - в своем плагине не стоит использовать дополнительное кеширование возвращаемых данных GetImage, это будет вредно
	*/
	
    [Info("Image Library", "Nimant", "1.0.4")]    
    class ImageLibrary : RustPlugin
    {
        #region Fields        

        private static ImageLibrary instance;
		private static uint instanceID;
        private static ImageAssets assets;
        
		private static string SteamPublishedSkinsURL = "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/";
		
        private static bool isInitialized;
		
		private static System.Random Rnd = new System.Random();
		
		private static bool NeedStandartSave, NeedWorkshopSkinsSave, NeedPlayerAvatarsSave, NeedCustomImagesSave;

		private static List<string> ItemShortNames = new List<string>();
		
		private static Dictionary<ulong, WorkshopSkinInfo> WorkShopAllowItems = new Dictionary<ulong, WorkshopSkinInfo>();				
		
        private static readonly Regex avatarFilter = new Regex(@"<avatarFull><!\[CDATA\[(.*)\]\]></avatarFull>");
		
		private static readonly Dictionary<string, string> workshopNameToShortname = new Dictionary<string, string>
        {
            {"ak47", "rifle.ak" },
            {"balaclava", "mask.balaclava" },
            {"bandana", "mask.bandana" },
            {"bearrug", "rug.bear" },
            {"beenie", "hat.beenie" },
            {"boltrifle", "rifle.bolt" },
            {"boonie", "hat.boonie" },
            {"buckethat", "bucket.helmet" },
            {"burlapgloves", "burlap.gloves" },
            {"burlappants", "burlap.trousers" },
            {"cap", "hat.cap" },
            {"collaredshirt", "shirt.collared" },
            {"deerskullmask", "deer.skull.mask" },
            {"hideshirt", "attire.hide.vest" },
            {"hideshoes", "attire.hide.boots" },
            {"longtshirt", "tshirt.long" },
            {"lr300", "rifle.lr300" },
            {"minerhat", "hat.miner" },
            {"mp5", "smg.mp5" },
            {"pipeshotgun", "shotgun.waterpipe" },
            {"roadsignpants", "roadsign.kilt" },
            {"roadsignvest", "roadsign.jacket" },
            {"semiautopistol", "pistol.semiauto" },
            {"snowjacket", "jacket.snow" },
            {"sword", "salvaged.sword" },
            {"vagabondjacket", "jacket" },
            {"woodstorage", "box.wooden" },
            {"workboots", "shoes.boots" }
        };
		
		private class SteamSkinPublishDet
        {
            [JsonProperty("response")] 
			public Response response;

            public class Tag
            {
                [JsonProperty("tag")] 
				public string tag;
            }

            public class Response
            {
                [JsonProperty("result")] 
				public int result;
                [JsonProperty("resultcount")] 
				public int resultcount;
                [JsonProperty("publishedfiledetails")] 
				public List<Publishedfiledetail> publishedfiledetails;

                public class Publishedfiledetail
                {
                    [JsonProperty("publishedfileid")] 
					public ulong publishedfileid;
                    [JsonProperty("result")] 
					public int result;
                    [JsonProperty("creator")] 
					public string creator;
                    [JsonProperty("creator_app_id")] 
					public int creator_app_id;
                    [JsonProperty("consumer_app_id")] 
					public int consumer_app_id;
                    [JsonProperty("filename")] 
					public string filename;
                    [JsonProperty("file_size")] 
					public int file_size;
                    [JsonProperty("preview_url")] 
					public string preview_url;
                    [JsonProperty("hcontent_preview")] 
					public string hcontent_preview;
                    [JsonProperty("title")] 
					public string title;
                    [JsonProperty("description")] 
					public string description;
                    [JsonProperty("time_created")] 
					public int time_created;
                    [JsonProperty("time_updated")] 
					public int time_updated;
                    [JsonProperty("visibility")] 
					public int visibility;
                    [JsonProperty("banned")] 
					public int banned;
                    [JsonProperty("ban_reason")] 
					public string ban_reason;
                    [JsonProperty("subscriptions")] 
					public int subscriptions;
                    [JsonProperty("favorited")] 
					public int favorited;

                    [JsonProperty("lifetime_subscriptions")]
                    public int lifetime_subscriptions;

                    [JsonProperty("lifetime_favorited")] 
					public int lifetime_favorited;
                    [JsonProperty("views")] 
					public int views;
                    [JsonProperty("tags")] 
					public List<Tag> tags;
                }
            }
        }

        #endregion Fields
		
		#region Hooks
		
		private void Init() 
		{
			LoadVariables();
			LoadData();
			
			if (string.IsNullOrEmpty(configData.SteamApiKey))
				configData.SteamApiKey = "4069ACBB1052EDB8D40A24B448A5C397";
			
			SaveConfig(configData);
		}
		
		private void OnServerInitialized()
        {
			instance = this;
			isInitialized = false;
			
			instanceID = 0;
			try { instanceID = CommunityEntity.ServerInstance.net.ID; } catch { }			
			if (instanceID == 0)
			{
				PrintError("Плагин не инициализирован, ServerInstance ID пустой!");
				return;
			}
			            
			WorkShopAllowItems.Clear();
			if (assets == null) assets = new GameObject("WebObject").AddComponent<ImageAssets>();
			
			foreach(var item in ItemManager.itemList)
			{
				if (!ItemShortNames.Contains(item.shortname))
					ItemShortNames.Add(item.shortname);
				
				var workshopName = item.displayName.english.ToLower().Replace("skin", "").Replace(" ", "").Replace("-", "");
                if (!workshopNameToShortname.ContainsKey(workshopName))
                    workshopNameToShortname.Add(workshopName, item.shortname);
			}						
			
            isInitialized = true;
			
			AddImage("http://i.imgur.com/sZepiWv.png", "NONE", 0);
            AddImage("http://i.imgur.com/lydxb0u.png", "LOADING", 0);
			
			if (LastCEID != instanceID)			
				RestoreOldCache();											
			
			var pack = new List<List<object>>();
			foreach(var name in ItemShortNames)			
				pack.Add(new List<object>() {null, name, 0l, null});									
			
			AddImagePack("Обновление стандартных скинов", pack);
			
			GetWorkshopAllowSkinsInfo();
			
			if (configData.EnableStoreAvatars)
				foreach (var player in BasePlayer.activePlayerList)
					OnPlayerConnected(player);
        }
		
		private void OnPlayerConnected(BasePlayer player) 
		{
			if (configData.EnableStoreAvatars)
				AddImage(null, null, player.userID);
		}

        private void Unload()
        {
            SaveStandartSkinsData();
			SaveWorkshopSkinsData();
			SavePlayerAvatarsData();
			SaveCustomImagesData();
			
            UnityEngine.Object.Destroy(assets);
            instance = null;
			
			WorkShopAllowItems.Clear();
        }
		
		private void OnServerSave()
        {
			if (NeedStandartSave)
			{
				NeedStandartSave = false;
				SaveStandartSkinsData();
			}			
			
			if (NeedWorkshopSkinsSave)
			{
				NeedWorkshopSkinsSave = false;
				SaveWorkshopSkinsData();
			}
			
			if (NeedPlayerAvatarsSave)
			{
				NeedPlayerAvatarsSave = false;
				SavePlayerAvatarsData();
			}
			
			if (NeedCustomImagesSave)
			{
				NeedCustomImagesSave = false;
				SaveCustomImagesData();
			}			
        }
		
		#endregion
		
		#region Gather Images
		
		private static List<Rust.Workshop.ItemSchema.Item> GetSkinItemsInfo()
		{
			var result = new List<Rust.Workshop.ItemSchema.Item>();
			var aproved = Rust.Workshop.Approved.All.Select(x=> x.Value).ToList();
			
			foreach (var itemRaw in aproved)
			{
				var item = new Rust.Workshop.ItemSchema.Item();
				item.itemshortname = itemRaw.Skinnable.ItemName;
				item.workshopid = itemRaw.WorkshopdId > 0 ? itemRaw.WorkshopdId.ToString() : null;
				item.workshopdownload = item.workshopid;
				item.name = itemRaw.Name;
				item.itemdefid = (uint)itemRaw.InventoryId;
				item.icon_url = "https://files.facepunch.com/rust/icons/inventory/rust/" + itemRaw.InventoryId + "_small.png";
				result.Add(item);
			}
			
			return result;
		}
		
		private void GetWorkshopAllowSkinsInfo()
		{	
			foreach (var item in GetSkinItemsInfo())
			{
				if (string.IsNullOrEmpty(item.icon_url)) continue;
				var url = item.icon_url.Substring(0, item.icon_url.IndexOf(".png")+4);
				if (string.IsNullOrEmpty(url) || !url.EndsWith(".png")) continue;
				var skinIDString = !string.IsNullOrEmpty(item.workshopid) ? item.workshopid : item.itemdefid.ToString();
				if (string.IsNullOrEmpty(skinIDString)) continue;
				
				ulong skinID = 0l;
				try { skinID = (ulong)Convert.ToInt64(skinIDString); } catch { }
				if (skinID == 0l) continue;
				
				if (WorkShopAllowItems.ContainsKey(skinID)) continue;						
				
				var info = new WorkshopSkinInfo();
				info.Url = url;
				info.Name = item.name;
				info.shortname = item.itemshortname;

				WorkShopAllowItems.Add(skinID, info);
			}										
			
			LoadOldVerWorkshopSkins();
			
			if (configData.EnableStoreAllowWorkshopSkins)
				LoadAllowWorkshopSkins();
		}				
		
		private static string GetShortNameByTag(List<SteamSkinPublishDet.Tag> tags)
		{
			foreach (var tag in tags)
			{
				var adjTag = tag.tag.ToLower().Replace("skin", "").Replace(" ", "").Replace("-", "");
				if (workshopNameToShortname.ContainsKey(adjTag))
					return workshopNameToShortname[adjTag];
			}
			
			return null;
		}
		
		private void LoadOldVerWorkshopSkins()
		{
			var list = new Dictionary<string, Link>();
			
			foreach(var pair in WorkShopAllowItems.Where(x=> !HasImage(null, x.Key) && x.Key < 1000000))
				list.Add(pair.Key.ToString(), new Link(null, pair.Value.Url, pair.Value.Name, pair.Value.shortname));
			
			if (list.Count == 0) return;
			
			assets.AddOrder(new LoadOrder("разрешенных старых версий WorkShop скинов", "workshop", list));
		}
		
		private void LoadAllowWorkshopSkins()
		{
			var list = new Dictionary<string, Link>();
			
			foreach(var pair in WorkShopAllowItems.Where(x=> !HasImage(null, x.Key) && x.Key >= 1000000))
				list.Add(pair.Key.ToString(), new Link(null, pair.Value.Url, pair.Value.Name, pair.Value.shortname));
			
			if (list.Count == 0) return;
			
			assets.AddOrder(new LoadOrder("разрешенных новых версий WorkShop скинов", "workshop", list));
		}
		
		private void RestoreOldCache()
		{			
			var oldStandartSkinsFiles = new Dictionary<string, Link>();			
			foreach (var pair in StandartSkins.ToDictionary(x=> x.Key, x=> x.Value))
			{
				if (!string.IsNullOrEmpty(pair.Value.Cache) && IsInStorage(pair.Value.Cache, LastCEID))
				{
					var bytes = FileStorage.server.Get(uint.Parse(pair.Value.Cache), FileStorage.Type.png, LastCEID);
					oldStandartSkinsFiles.Add(pair.Key, new Link(bytes, pair.Value.Url, pair.Value.Name));
				}
				else
					StandartSkins.Remove(pair.Key);
			}
		
			var oldWorkshopSkinsFiles = new Dictionary<string, Link>();			
			foreach (var pair in WorkshopSkins.ToDictionary(x=> x.Key, x=> x.Value))
			{
				if (!string.IsNullOrEmpty(pair.Value.Cache) && IsInStorage(pair.Value.Cache, LastCEID))
				{
					var bytes = FileStorage.server.Get(uint.Parse(pair.Value.Cache), FileStorage.Type.png, LastCEID);
					oldWorkshopSkinsFiles.Add(pair.Key.ToString(), new Link(bytes, pair.Value.Url, pair.Value.Name));
				}
				else
					WorkshopSkins.Remove(pair.Key);
			}
		
			var oldPlayerAvatarsFiles = new Dictionary<string, Link>();			
			foreach (var pair in PlayerAvatars.ToDictionary(x=> x.Key, x=> x.Value))
			{
				if (!string.IsNullOrEmpty(pair.Value.Cache) && IsInStorage(pair.Value.Cache, LastCEID))
				{
					var bytes = FileStorage.server.Get(uint.Parse(pair.Value.Cache), FileStorage.Type.png, LastCEID);
					oldPlayerAvatarsFiles.Add(pair.Key.ToString(), new Link(bytes, pair.Value.Url, null));
				}
				else
					PlayerAvatars.Remove(pair.Key);
			}
		
			var oldCustomImagesFiles = new Dictionary<string, Link>();			
			foreach (var pair in CustomImages.ToDictionary(x=> x.Key, x=> x.Value))
			{
				if (!string.IsNullOrEmpty(pair.Value.Cache) && IsInStorage(pair.Value.Cache, LastCEID))
				{
					var bytes = FileStorage.server.Get(uint.Parse(pair.Value.Cache), FileStorage.Type.png, LastCEID);
					oldCustomImagesFiles.Add(pair.Key, new Link(bytes, pair.Value.Url, pair.Value.Name));
				}
				else
					CustomImages.Remove(pair.Key);
			}
			
			Facepunch.Sqlite.Database db = new Facepunch.Sqlite.Database();
            try
            {
                db.Open($"{ConVar.Server.rootFolder}/sv.files.0.db");
                db.Execute("DELETE FROM data WHERE entid = ?", LastCEID);
                db.Close();
            }
            catch { }
			
			if (oldStandartSkinsFiles.Count > 0)
				assets.AddOrder(new LoadOrder("Перенос стандартных скинов", "standart", oldStandartSkinsFiles));
			
			if (oldWorkshopSkinsFiles.Count > 0)
				assets.AddOrder(new LoadOrder("Перенос WorkShop скинов", "workshop", oldWorkshopSkinsFiles));
			
			if (oldPlayerAvatarsFiles.Count > 0)
				assets.AddOrder(new LoadOrder("Перенос аватарок игроков", "player", oldPlayerAvatarsFiles));
			
			if (oldCustomImagesFiles.Count > 0)
				assets.AddOrder(new LoadOrder("Перенос кастомных изображений", "custom", oldCustomImagesFiles));
			
			LastCEID = instanceID;
            SaveLastCEIDData();			
		}
		
		private IEnumerator ClearDataByTypeAndInit(BasePlayer player, string type)
		{
			var wasDefault = false;
			
			switch (type.ToLower())
			{
				case "standart": 
				{
					Reply(player, "Запущена чистка и реинициализация стандартныйх скинов.");
					foreach (var item in StandartSkins.ToDictionary(x=> x.Key, x=> x.Value))
					{						
						if (!HasImage(item.Key, 0l)) continue;
						
						var cache = item.Value.Cache;						
						uint crc = uint.Parse(cache);
						FileStorage.server.Remove(crc, FileStorage.Type.png, instanceID);
						
						StandartSkins.Remove(item.Key);
						yield return new WaitForSeconds(0.01f);
					}															
					NeedStandartSave = true;
					break;
				}
				case "workshop": 
				{
					Reply(player, "Запущена чистка и реинициализация скинов из workshop.");
					foreach (var item in WorkshopSkins.ToDictionary(x=> x.Key, x=> x.Value))
					{						
						if (!HasImage(null, item.Key)) continue;
						
						var cache = item.Value.Cache;						
						uint crc = uint.Parse(cache);
						FileStorage.server.Remove(crc, FileStorage.Type.png, instanceID);
						
						WorkshopSkins.Remove(item.Key);
						yield return new WaitForSeconds(0.01f);
					}
					NeedWorkshopSkinsSave = true;
					break;
				}
				case "player": 
				{
					Reply(player, "Запущена чистка и реинициализация аватарок игроков.");
					foreach (var item in PlayerAvatars.ToDictionary(x=> x.Key, x=> x.Value))
					{						
						if (!HasImage(null, item.Key)) continue;
						
						var cache = item.Value.Cache;						
						uint crc = uint.Parse(cache);
						FileStorage.server.Remove(crc, FileStorage.Type.png, instanceID);
						
						PlayerAvatars.Remove(item.Key);
						yield return new WaitForSeconds(0.01f);
					}
					NeedPlayerAvatarsSave = true;
					break;
				}
				case "custom": 
				{
					Reply(player, "Запущена чистка и реинициализация кастомных картинок.");
					foreach (var item in CustomImages.ToDictionary(x=> x.Key, x=> x.Value))
					{						
						if (!HasImage(item.Key, 0l)) continue;
						
						var cache = item.Value.Cache;						
						uint crc = uint.Parse(cache);
						FileStorage.server.Remove(crc, FileStorage.Type.png, instanceID);
						
						CustomImages.Remove(item.Key);
						yield return new WaitForSeconds(0.01f);
					}
					NeedCustomImagesSave = true;
					break;
				}
				default:
				{
					Reply(player, "Ошибка, используйте один из перечисленных параметров: standart, workshop, player, custom");					
					wasDefault = true;
					break;
				}
			}
			
			if (!wasDefault)
			{
				Reply(player, "Чистка данных завершена.");			
				OnServerInitialized();
			}
		}
		
		#endregion
		
		#region API
		
		/* Добавляет картинку базируясь сперва на переданном url или массиве байтов, а если там пусто то пытается сам загрузить или скин или аватарку */
		/* callback - вызывается только при успешной операции, а если картинка уже была ранее загружена, то callback не будет вызван так же */
		/* Данный метод подходит для предварительной загрузки картинки (без вывода её содержимого на экран), для ускорения последующего доступа к ней */
		/* Первый параметр при вызове стоит явно приводить к object типу, что бы данный метод точно вызвался */
		[HookMethod("AddImage")]
        public bool AddImage(object url_bytes, string imageName, ulong imageId, string fullName = null, bool replace = false, Action callback = null)
        {
			if (!isInitialized) 
				return false;
			
			string url = url_bytes is string ? url_bytes as string : null;
			byte[] bytes = url_bytes is byte[] ? url_bytes as byte[] : null;
			
			LoadOrder order;						
			
			if (imageId > 0)
			{				
				if (imageId.IsSteamId() && PlayerAvatars.ContainsKey(imageId) && !string.IsNullOrEmpty(PlayerAvatars[imageId].Cache) && IsInStorage(PlayerAvatars[imageId].Cache) ||
					WorkshopSkins.ContainsKey(imageId) && !string.IsNullOrEmpty(WorkshopSkins[imageId].Cache) && IsInStorage(WorkshopSkins[imageId].Cache))
					if (!replace) return true;
				
				if (imageId.IsSteamId())									
					order = new LoadOrder(null, "player", new Dictionary<string, Link> { { $"{imageId}", new Link(bytes, url, fullName) } }, callback);
				else
					order = new LoadOrder(null, "workshop", new Dictionary<string, Link> { { $"{imageId}", new Link(bytes, url, fullName) } }, callback);				
			}
			else
			{
				if (string.IsNullOrEmpty(imageName) || 
					StandartSkins.ContainsKey(imageName) && !string.IsNullOrEmpty(StandartSkins[imageName].Cache) && IsInStorage(StandartSkins[imageName].Cache) ||
					CustomImages.ContainsKey(imageName) && !string.IsNullOrEmpty(CustomImages[imageName].Cache) && IsInStorage(CustomImages[imageName].Cache))
					if (!replace || string.IsNullOrEmpty(imageName)) return true;
				
				if (ItemShortNames.Contains(imageName))									
					order = new LoadOrder(null, "standart", new Dictionary<string, Link> { { $"{imageName}", new Link(bytes, url, fullName) } }, callback);
				else
					order = new LoadOrder(null, "custom", new Dictionary<string, Link> { { $"{imageName}", new Link(bytes, url, fullName) } }, callback);
			}
						
			assets.AddOrder(order);
            return true;
        }
		
		/* Поддержка хука из старой библиотеки ImageLibrary */ 
		[HookMethod("AddImage")]
        public bool AddImage(string url, string imageName, ulong imageId, Action callback = null) => AddImage(url, imageName, imageId, null, true, callback);
		
		/* Поддержка хука из старой библиотеки ImageLibrary */
		[HookMethod("AddImageData")]
        public bool AddImageData(string imageName, byte[] array, ulong imageId, Action callback = null) => AddImage((object)array, imageName, imageId, null, true, callback);
		
		/* Тоже что и предыдущий метод, только выполняется загрузка пакетом (это будет выгодно при загрузке скинов с workshop, в остальных случаях подойдет и AddImage в цикле)*/
		/* Строки пакета в списке: string url_bytes, string imageName, ulong imageId, string fullName */
		[HookMethod("AddImagePack")]
        public bool AddImagePack(string title, List<List<object>> pack, bool replace = false, Action callback = null)
        {
			if (!isInitialized) 
				return false;
						
			var playerLinks = new Dictionary<string, Link>();
			var workshopLinks = new Dictionary<string, Link>();
			var standartLinks = new Dictionary<string, Link>();
			var customLinks = new Dictionary<string, Link>();
			
			foreach (var item in pack)
			{								
				string url = item[0] is string ? item[0] as string : null;
				byte[] bytes = item[0] is byte[] ? item[0] as byte[] : null;				
				string imageName = item[1] is string ? item[1] as string : null;
				string fullName = item[3] is string ? item[3] as string : null;
								
				ulong imageId = 0l;
				try { imageId = (ulong)Convert.ToInt64(item[2]); } catch { continue; }				
				
				if (imageId > 0)
				{				
					if (imageId.IsSteamId() && PlayerAvatars.ContainsKey(imageId) && !string.IsNullOrEmpty(PlayerAvatars[imageId].Cache) && IsInStorage(PlayerAvatars[imageId].Cache) ||
						WorkshopSkins.ContainsKey(imageId) && !string.IsNullOrEmpty(WorkshopSkins[imageId].Cache) && IsInStorage(WorkshopSkins[imageId].Cache))
						if (!replace) continue;
					
					if (imageId.IsSteamId())
						playerLinks.Add($"{imageId}", new Link(bytes, url, fullName));
					else											
						workshopLinks.Add($"{imageId}", new Link(bytes, url, fullName));
				}
				else
				{
					if (string.IsNullOrEmpty(imageName) || 
						StandartSkins.ContainsKey(imageName) && !string.IsNullOrEmpty(StandartSkins[imageName].Cache) && IsInStorage(StandartSkins[imageName].Cache) ||
						CustomImages.ContainsKey(imageName) && !string.IsNullOrEmpty(CustomImages[imageName].Cache) && IsInStorage(CustomImages[imageName].Cache))
						if (!replace || string.IsNullOrEmpty(imageName)) continue;										
					
					if (ItemShortNames.Contains(imageName))
						standartLinks.Add($"{imageName}", new Link(bytes, url, fullName));
					else
						customLinks.Add($"{imageName}", new Link(bytes, url, fullName));						
				}				
			}
			
			int diffCount = (playerLinks.Count > 0 ? 1 : 0) + (workshopLinks.Count > 0 ? 1 : 0) + (standartLinks.Count > 0 ? 1 : 0) + (customLinks.Count > 0 ? 1 : 0);
			int count = diffCount;
			
			if (playerLinks.Count > 0)
			{				
				assets.AddOrder(new LoadOrder(title + (diffCount > 1 ? " (аватарки игроков)" : ""), "player", playerLinks, count == 1 ? callback : null));
				count--;
			}
			
			if (workshopLinks.Count > 0)
			{				
				assets.AddOrder(new LoadOrder(title + (diffCount > 1 ? " (скины из WorkShop)" : ""), "workshop", workshopLinks, count == 1 ? callback : null));
				count--;
			}
			
			if (standartLinks.Count > 0)
			{				
				assets.AddOrder(new LoadOrder(title + (diffCount > 1 ? " (стандартные скины)" : ""), "standart", standartLinks, count == 1 ? callback : null));
				count--;
			}
			
			if (customLinks.Count > 0)
			{				
				assets.AddOrder(new LoadOrder(title + (diffCount > 1 ? " (кастомные картинки)" : ""), "custom", customLinks, count == 1 ? callback : null));
				count--;
			}
			
			if (diffCount == 0 && callback != null)
				callback.Invoke();				
			
            return true;
        }				
		
		/* Возвращает либо кеш, либо url картинки, а если картинка еще не была загружена, то делается попытка её загрузить и возвращается картинка "ожидания" */
		/* Данный метод подходит для предварительной загрузки картинки (без вывода её содержимого на экран), для ускорения последующего доступа к ней */
		[HookMethod("GetImage")]
        public string GetImage(string imageName, ulong imageId = 0, bool returnUrl = false)
        {
			if (!isInitialized || imageId == 0 && string.IsNullOrEmpty(imageName)) 
				return returnUrl ? GetCustomSkin("NONE")?.Url : GetCustomSkin("NONE")?.Cache;
			
			string url = null;
			string loadType = null;			
			
			if (imageId > 0)
			{
				if (imageId.IsSteamId())
				{
					if (PlayerAvatars.ContainsKey(imageId))
					{
						if (returnUrl)
						{
							if (!string.IsNullOrEmpty(PlayerAvatars[imageId].Url))
								return PlayerAvatars[imageId].Url;							
						}
						else
						{
							if (!string.IsNullOrEmpty(PlayerAvatars[imageId].Cache) && IsInStorage(PlayerAvatars[imageId].Cache))
								return PlayerAvatars[imageId].Cache;
							else
								url = PlayerAvatars[imageId].Url;
						}
					}
					else
						loadType = "player";
				}
				else
				{
					if (WorkshopSkins.ContainsKey(imageId))
					{
						if (returnUrl)
						{
							if (!string.IsNullOrEmpty(WorkshopSkins[imageId].Url))
								return WorkshopSkins[imageId].Url;							
						}
						else
						{
							if (!string.IsNullOrEmpty(WorkshopSkins[imageId].Cache) && IsInStorage(WorkshopSkins[imageId].Cache))
								return WorkshopSkins[imageId].Cache;
							else
								url = WorkshopSkins[imageId].Url;							
						}
					}
					else
						loadType = "workshop";
				}					 																							
			}
			else
			{
				if (ItemShortNames.Contains(imageName))
				{
					if (StandartSkins.ContainsKey(imageName))
					{
						if (returnUrl)
						{
							if (!string.IsNullOrEmpty(StandartSkins[imageName].Url))
								return StandartSkins[imageName].Url;							
						}
						else
						{
							if (!string.IsNullOrEmpty(StandartSkins[imageName].Cache) && IsInStorage(StandartSkins[imageName].Cache))
								return StandartSkins[imageName].Cache;
							else
								url = StandartSkins[imageName].Url;
						}
					}
					else
						loadType = "standart";
				}
				else
				{
					if (CustomImages.ContainsKey(imageName))
					{
						if (returnUrl)
						{
							if (!string.IsNullOrEmpty(CustomImages[imageName].Url))
								return CustomImages[imageName].Url;							
						}
						else
						{
							if (!string.IsNullOrEmpty(CustomImages[imageName].Cache) && IsInStorage(CustomImages[imageName].Cache))
								return CustomImages[imageName].Cache;
							else
								url = CustomImages[imageName].Url;
						}
					}					
				}								
			}	
			
			switch (loadType)
			{
				case "player": AddImage(null, null, imageId); break;							   
				case "workshop": AddImage(null, null, imageId); break;
				case "standart": AddImage(null, imageName, 0l); break;								 
			}
			
			if (!string.IsNullOrEmpty(loadType))
				return returnUrl ? GetCustomSkin("LOADING")?.Url : GetCustomSkin("LOADING")?.Cache;
			
			if (string.IsNullOrEmpty(url))
				return returnUrl ? GetCustomSkin("NONE")?.Url : GetCustomSkin("NONE")?.Cache;							
			
			AddImage(url, imageName, imageId);
            return returnUrl ? GetCustomSkin("LOADING")?.Url : CustomImages["LOADING"].Cache;
        }
		
		/* То же что и GetImage только чисто возвращает url */
		[HookMethod("GetImageURL")]
        public string GetImageURL(string imageName, ulong imageId = 0) => GetImage(imageName, imageId, true);        
		
		/* Возвращает список ИД скинов по shortname предмета. Возвращаются только те скины, которые были загружены заранее и есть в дата файле */
		[HookMethod("GetImageList")]
        public List<ulong> GetImageList(string shortname) => WorkshopSkins.Where(x=> x.Value.shortname == shortname).Select(x=> x.Key).ToList();        
		
		/* Возвращает структуру с данными по скину, но по факту тут есть только название предмета от автора скина, остальные поля пусты (оставлены для совместимости со старым плагином) */
		[HookMethod("GetSkinInfo")]
        public Dictionary<string, object> GetSkinInfo(string imageName, ulong imageId)
        {
			var skinInfo = new Dictionary<string, object>();
			skinInfo.Add("votesup", 0);
			skinInfo.Add("votesdown", 0);
			skinInfo.Add("score", 0);
			skinInfo.Add("views", 0);
			skinInfo.Add("created", new DateTime());
			skinInfo.Add("description", "");
			
			if (imageId > 0)
			{
				if (WorkshopSkins.ContainsKey(imageId) && !string.IsNullOrEmpty(WorkshopSkins[imageId].Name))
				{					
					skinInfo.Add("title", WorkshopSkins[imageId].Name);
					return skinInfo;
				}
			}
			else
			{
				if (StandartSkins.ContainsKey(imageName) && !string.IsNullOrEmpty(StandartSkins[imageName].Name))
				{
					skinInfo.Add("title", StandartSkins[imageName].Name);
					return skinInfo;
				}
				
				if (CustomImages.ContainsKey(imageName) && !string.IsNullOrEmpty(CustomImages[imageName].Name))
				{
					skinInfo.Add("title", CustomImages[imageName].Name);
					return skinInfo;
				}
			}
			
            return null;
        }				
		
		/* Проверка на существование картинки как в дата файлах, так и в кеше раста. Если картинка где то из этих двух мест отсутствует, то возвращает false */
		[HookMethod("HasImage")]
        public bool HasImage(string imageName, ulong imageId)
        {
			if (!isInitialized || imageId == 0 && string.IsNullOrEmpty(imageName)) 
				return false;
			
			if (imageId > 0)
			{
				if (imageId.IsSteamId() && PlayerAvatars.ContainsKey(imageId) && !string.IsNullOrEmpty(PlayerAvatars[imageId].Cache) && IsInStorage(PlayerAvatars[imageId].Cache) ||
					WorkshopSkins.ContainsKey(imageId) && !string.IsNullOrEmpty(WorkshopSkins[imageId].Cache) && IsInStorage(WorkshopSkins[imageId].Cache))
					return true;
			}
			else
			{
				if (StandartSkins.ContainsKey(imageName) && !string.IsNullOrEmpty(StandartSkins[imageName].Cache) && IsInStorage(StandartSkins[imageName].Cache) ||
					CustomImages.ContainsKey(imageName) && !string.IsNullOrEmpty(CustomImages[imageName].Cache) && IsInStorage(CustomImages[imageName].Cache))
					return true;
			}			            

            return false;
        }

		/* проверяет входит ли указанный кеш в хранилище с кешами раста */
		public bool IsInStorage(string crc) 
		{
			if (string.IsNullOrEmpty(crc)) 
				return false;
			
			return FileStorage.server.Get(uint.Parse(crc), FileStorage.Type.png, instanceID) != null;
		}
		
		/* Возвращает статус загрузки картинок */
		[HookMethod("IsReady")]
        public bool IsReady() => isInitialized && assets.IsReady();		
		
		/* Функция удаляет кастомные картинки (удаление скинов и аватарок запрещено) */
		/* Если было загружено некорректное изображение скина или аватарки, его следует перезагрузить новым, корректным значением */
		[HookMethod("RemoveImage")]
        public void RemoveImage(string imageName, ulong imageId)
		{
			if (imageId > 0 || StandartSkins.ContainsKey(imageName) || !HasImage(imageName, imageId)) return;
			
			var cache = CustomImages[imageName].Cache;
			uint crc = uint.Parse(cache);
            FileStorage.server.Remove(crc, FileStorage.Type.png, instanceID);
			
			CustomImages.Remove(imageName);
			
			NeedCustomImagesSave = true;
		}
		
		/* Заглушка для хука из старой библиотеки ImageLibrary (не поддерживается, используйте AddImagePack) */
		[HookMethod("ImportImageList")]
        public void ImportImageList(string title, Dictionary<string, string> imageList, ulong imageId = 0, bool replace = false, Action callback = null) { }
        
		/* Заглушка для хука из старой библиотеки ImageLibrary (не поддерживается, используйте AddImagePack) */
		[HookMethod("ImportItemList")]
        public void ImportItemList(string title, Dictionary<string, Dictionary<ulong, string>> itemList, bool replace = false, Action callback = null) { }
        		
		/* Заглушка для хука из старой библиотеки ImageLibrary (не поддерживается, используйте AddImagePack) */		
		[HookMethod("ImportImageData")]
        public void ImportImageData(string title, Dictionary<string, byte[]> imageList, ulong imageId = 0, bool replace = false, Action callback = null) { }		
				
		/* Заглушка для хука из старой библиотеки ImageLibrary (не поддерживается, используйте AddImagePack) */				
		[HookMethod("LoadImageList")]
        public void LoadImageList(string title, List<KeyValuePair<string, ulong>> imageList, Action callback = null) { }		
				
		#endregion

		#region Image Storage				
		
		private bool IsInStorage(string crc, uint CEID) 
		{
			if (string.IsNullOrEmpty(crc) || CEID == 0)
				return false;
			
			return FileStorage.server.Get(uint.Parse(crc), FileStorage.Type.png, CEID) != null;
		}

        private struct LoadOrder
        {
            public string loadName;            
			public string type; // standart, workshop, player, custom           
			
			public Dictionary<string, Link> imageDataLink;

            public Action callback;
                       
			public LoadOrder(string loadName, string type, Dictionary<string, Link> imageData, Action callback = null)
            {
                this.loadName = loadName;
				this.type = type;
                this.imageDataLink = imageData;                
                this.callback = callback;				
            }
        }
		
		private class Link
		{
			public byte[] raw;
			public string url;
			public string name;
			public string shortname;
			
			public Link(byte[] raw, string url, string name)
			{
				this.raw = raw;
				this.url = url;
				this.name = name;
				this.shortname = null;
			}
			public Link(byte[] raw, string url, string name, string shortname)
			{
				this.raw = raw;
				this.url = url;
				this.name = name;
				this.shortname = shortname;
			}
		}

        private class ImageAssets : MonoBehaviour
        {
            private Queue<QueueItem> queueList = new Queue<QueueItem>();
			private Queue<LoadOrder> loadOrders = new Queue<LoadOrder>();
			private Dictionary<int, Result> Waiting = new Dictionary<int, Result>();
			
			private class Result
			{
				public bool isEnded;
				public List<SteamSkinPublishDet.Response.Publishedfiledetail> dList = new List<SteamSkinPublishDet.Response.Publishedfiledetail>();
			}
			
            private string request;
			private Action callback;
			private int saveMask; // 1 - standart, 2 - workshop, 4 - player, 8 - custom
			private bool isBusy, isAbort;			

            private void OnDestroy() 
			{
				InvokeHandler.CancelInvoke(this, Activity);
				queueList.Clear();
				loadOrders.Clear();
				saveMask = 0;
				request = null;
				callback = null;
				isBusy = false;
			}
			
			public void AddOrder(LoadOrder order) => loadOrders.Enqueue(order);
			
			public bool IsReady() => isInitialized && loadOrders?.Count == 0 && !isBusy;
			
			public string GetCurrentTaskName() 
			{
				if (!IsReady())
				{
					if (!string.IsNullOrEmpty(request))
						return request;										
				}
				
				return "N|A";
			}
			
			public void AbortLoading()
			{
				if (IsReady()) return;
				
				loadOrders.Clear();
				queueList.Clear();				
				isAbort = true;
				
				if (string.IsNullOrEmpty(request))
					instance.PrintWarning($"Загрузка изображений была прервана!");
			}
			
			private void Awake() => InvokeHandler.InvokeRepeating(this, Activity, 1f, 1f);
			
			private void Activity()
			{
				if (instance == null)
				{
					UnityEngine.Object.Destroy(this);
					return;
				}					
					
				if (!isInitialized || isBusy || loadOrders?.Count == 0) return;
				
				isBusy = true;
				
				var nextLoad = loadOrders.Dequeue();
				
				request = nextLoad.loadName;
				
                if (!string.IsNullOrEmpty(request))
                    instance.Puts($"Начинаем пакетную загрузку изображений '{request}'");

				callback = nextLoad.callback;				
				StartCoroutine(CheckNeedWorkShopSkinsAndRunLoad(nextLoad));				
			}						

            private void AddQueueItem(string name, string fullName, string type, string url, byte[] bytes, string shortname) => queueList.Enqueue(new QueueItem(name, type, fullName, url, bytes, shortname));

            private void Next()
            {								
                if (queueList.Count == 0)
                {                                        
					if ((saveMask & 1) == 1)
						NeedStandartSave = true; 
					
					if ((saveMask & 2) == 2)
						NeedWorkshopSkinsSave = true;
					
					if ((saveMask & 4) == 4)
						NeedPlayerAvatarsSave = true;
					
					if ((saveMask & 8) == 8)
						NeedCustomImagesSave = true;
					
					saveMask = 0;										
					
                    if (!string.IsNullOrEmpty(request))
						if (!isAbort)
							instance.Puts($"Пакетная загрузка изображений '{request}' успешно завершена.");
						else
							instance.PrintWarning($"Пакетная загрузка изображений '{request}' была прервана!");											

					isBusy = false;	
						
                    request = string.Empty;                    
                    if (callback != null) callback.Invoke();
					
					isAbort = false;
					
                    return;
                }                               

                QueueItem queueItem = queueList.Dequeue();
				
				if (queueItem.bytes == null || queueItem.bytes.Length == 0)
				{
					if (!string.IsNullOrEmpty(queueItem.url))
						StartCoroutine(DownloadImage(queueItem));
					else
					{
						if (queueItem.type == "player")
							StartCoroutine(DownloadPlayerAvatarUrl(queueItem));
						else
							if (queueItem.type == "standart")
							{
								queueItem.url = $"https://rustlabs.com/img/items180/{queueItem.name}.png";
								StartCoroutine(DownloadImage(queueItem));
							}
							else
							{
								var who = queueItem.type == "custom" ? "workshop скина" : "кастомной картинки";
								instance.PrintWarning($"Ошибка скачивания {who} '{queueItem.name}'! Не наден url для загрузки." );
								Next();
							}
					}
				}
                else                
					StoreByteArray(queueItem);
            }
			
			private IEnumerator DownloadPlayerAvatarUrl(QueueItem queueItem)
			{
				ulong userID = 0l;
				try { userID = (ulong)Convert.ToInt64(queueItem.name); } catch {}
				if (userID == 0l)
				{
					Next();
                    yield break;
				}

				UnityWebRequest www = UnityWebRequest.Get($"http://steamcommunity.com/profiles/{userID}?xml=1");

                yield return www.SendWebRequest();
                if (instance == null) yield break;
                if (www.isNetworkError || www.isHttpError)
                {
                    instance.PrintWarning(string.Format("Ошибка скачивания аватарки! Ошибка: {0}, Steam ID игрока {1}", www.error, queueItem.name));
                    www.Dispose();
					                                        
					Next();
                    yield break;
                }
				
				string avatar = avatarFilter.Match(www.downloadHandler.text).Groups[1].ToString();
				if (!string.IsNullOrEmpty(avatar))
				{
					queueItem.url = avatar;
					StartCoroutine(DownloadImage(queueItem));					
				}
				else
					Next();
				
				www.Dispose();
			}
			
			private void RunWorkShopSkinsDetLoading(List<ulong> skinIds, int waitKey)
			{
				var str = string.Format("?key={0}&itemcount={1}", configData.SteamApiKey, skinIds.Count);
				int cnt = 0;
				foreach (var skin in skinIds)
				{
					str += string.Format("&publishedfileids[{0}]={1}", cnt, skin);
					cnt++;
				}
				
				try
				{
					instance.webrequest.Enqueue(SteamPublishedSkinsURL, str, (code, response) => 
					{
						if (!(response == null || code != 200))
						{
							var data = JsonConvert.DeserializeObject<SteamSkinPublishDet>(response);
							if (!(data == null || !(data is SteamSkinPublishDet) || data.response.result == 0 || data.response.resultcount == 0))						
								Waiting[waitKey].dList = data.response.publishedfiledetails;
						}
						
						Waiting[waitKey].isEnded = true;
					}, instance, RequestMethod.POST);
				}
				catch 
				{ 
					Waiting[waitKey].isEnded = true;
				}
			}
			
			private IEnumerator CheckNeedWorkShopSkinsAndRunLoad(LoadOrder nextLoad)
			{
				if (nextLoad.imageDataLink != null && nextLoad.imageDataLink.Count > 0)
				{
					if (nextLoad.type == "workshop")
					{
						var skinIds = new List<ulong>();
						
						foreach (var pair in nextLoad.imageDataLink.Where(x=> string.IsNullOrEmpty(x.Value.url)))
						{
							ulong skinID = 0l;
							try { skinID = (ulong)Convert.ToInt64(pair.Key); } catch {}
							if (skinID == 0l) continue;
							
							if (!WorkshopSkins.ContainsKey(skinID) || string.IsNullOrEmpty(WorkshopSkins[skinID].Url))							
								skinIds.Add(skinID);
						}	
						 													
						if (skinIds.Count > 0)
						{
							int waitKey = 1;
							
							while (Waiting.ContainsKey(waitKey))							
								waitKey = Rnd.Next(1, 9999999);
							
							Waiting.Add(waitKey, new Result());														
							RunWorkShopSkinsDetLoading(skinIds, waitKey);
							
							yield return new WaitWhile(() => !Waiting[waitKey].isEnded);

							foreach (var item in Waiting[waitKey].dList)
							{				
								if (string.IsNullOrEmpty(item.preview_url)) continue;			
								var url = item.preview_url;
								if (string.IsNullOrEmpty(url)) continue;				
								var skinIDString = item.publishedfileid.ToString();
								if (string.IsNullOrEmpty(skinIDString)) continue;				
								
								ulong skinID = 0l;
								try { skinID = (ulong)Convert.ToInt64(skinIDString); } catch { }
								if (skinID == 0l) continue;				
								
								if (nextLoad.imageDataLink.ContainsKey(skinIDString))
								{
									nextLoad.imageDataLink[skinIDString].url = url;
									nextLoad.imageDataLink[skinIDString].name = item.title;
									nextLoad.imageDataLink[skinIDString].shortname = GetShortNameByTag(item.tags);
								}							
							}
						}
					}
										
					foreach (var item in nextLoad.imageDataLink)
						AddQueueItem(item.Key, item.Value.name, nextLoad.type, item.Value.url, item.Value.raw, item.Value.shortname);
                }
				
                Next();
			}

            private IEnumerator DownloadImage(QueueItem info)
            {
                UnityWebRequest www = UnityWebRequest.Get(info.url);

                yield return www.SendWebRequest();
                if (instance == null) yield break;
                if (www.isNetworkError || www.isHttpError)
                {
                    instance.PrintWarning(string.Format("Ошибка скачивания картинки! Ошибка: {0}, Название картинки: {1}, URL картинки: {2}", www.error, info.name, info.url));
                    www.Dispose();
					                                        
					Next();
                    yield break;
                }

                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(www.downloadHandler.data);
                if (texture != null)
                {
                    byte[] bytes = texture.EncodeToPNG();
                    DestroyImmediate(texture);
                    StoreByteArray(info, bytes);
                }
				else
					Next();					
				
                www.Dispose();
            }

            private void StoreByteArray(QueueItem info, byte[] bytes = null)
            {
				switch (info.type)
				{
					case "standart": 
					{
						if (!StandartSkins.ContainsKey(info.name))
							StandartSkins.Add(info.name, new StdCtmSkinInfo());
						
						StandartSkins[info.name].Name = !string.IsNullOrEmpty(info.fullName) ? info.fullName : ItemManager.FindItemDefinition(info.name)?.displayName?.english;
						StandartSkins[info.name].Url = info.url;
						
						var bytes_ = bytes != null && bytes.Length > 0 ? bytes : info.bytes;
						
						if (bytes_ != null && bytes_.Length > 0)
							StandartSkins[info.name].Cache = FileStorage.server.Store(bytes_, FileStorage.Type.png, instanceID).ToString();
						
						if ((saveMask & 1) != 1) saveMask += 1;
						
						break;
					}
					case "workshop": 
					{
						ulong skinID = 0l;
						try { skinID = (ulong)Convert.ToInt64(info.name); } catch {}
						if (skinID == 0l) break;
						
						if (!WorkshopSkins.ContainsKey(skinID))
							WorkshopSkins.Add(skinID, new WorkshopSkinInfo());
						
						WorkshopSkins[skinID].Name = info.fullName;
						WorkshopSkins[skinID].Url = info.url;
						WorkshopSkins[skinID].shortname = info.shortname;
						
						var bytes_ = bytes != null ? bytes : info.bytes;
						
						if (bytes_ != null)
							WorkshopSkins[skinID].Cache = FileStorage.server.Store(bytes_, FileStorage.Type.png, instanceID).ToString();
						
						if ((saveMask & 2) != 2) saveMask += 2;
						
						break;
					}
					case "player": 
					{
						ulong userID = 0l;
						try { userID = (ulong)Convert.ToInt64(info.name); } catch {}
						if (userID == 0l) break;
						
						if (!PlayerAvatars.ContainsKey(userID))
							PlayerAvatars.Add(userID, new PlayerSkinInfo());
												
						PlayerAvatars[userID].Url = info.url;
						
						var bytes_ = bytes != null ? bytes : info.bytes;
						
						if (bytes_ != null)
							PlayerAvatars[userID].Cache = FileStorage.server.Store(bytes_, FileStorage.Type.png, instanceID).ToString();
						
						if ((saveMask & 4) != 4) saveMask += 4;
						
						break;
					}
					case "custom": 
					{
						if (!CustomImages.ContainsKey(info.name))
							CustomImages.Add(info.name, new StdCtmSkinInfo());
						
						CustomImages[info.name].Name = info.fullName;
						CustomImages[info.name].Url = info.url;
						
						var bytes_ = bytes != null ? bytes : info.bytes;
						
						if (bytes_ != null)
							CustomImages[info.name].Cache = FileStorage.server.Store(bytes_, FileStorage.Type.png, instanceID).ToString();
						
						if ((saveMask & 8) != 8) saveMask += 8;
						
						break;
					}
				}
				                                			                
				Next();
            }

            private class QueueItem
            {
				public string fullName;
				public string name;
				public string type;
                public byte[] bytes;
                public string url;
                public string shortname;
				
                public QueueItem(string name, string type, string fullName, string url, byte[] bytes, string shortname)
                {
					this.fullName = fullName;
					this.name = name;
					this.type = type;
                    this.bytes = bytes;
                    this.url = url;
					this.shortname = shortname;
                }
            }
        }

        #endregion Image Storage
		
		#region Commands
				
		/* Load Allow Workshop Skins - загружает все разрешенные скины из воркшопа, а если какие то уже были загружены, то их пропускает */
		/* Данная операция долгая, минут 20, потому её стоит выполнять когда на сервере мало людей */
		[ConsoleCommand("il.loadallowworkshopskins")]
        private void cmdLoadWorkshopSkins(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player != null && !player.IsAdmin) return;
			
			LoadAllowWorkshopSkins();
        }
		
		/* Ready State - проверка статуса очереди загрузки картинок */
		[ConsoleCommand("il.readystate")]
        private void cmdIsLoadEmpty(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player != null && !player.IsAdmin) return;
			
			Reply(player, IsReady() ? "очередь загрузки пуста" : $"в очереди загрузки есть задания {assets.GetCurrentTaskName()}");
		}
		
		/* Abort Loading - прерывает длительную загрузку картинок */
		[ConsoleCommand("il.abortloading")]
        private void cmdAbortLoading(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player != null && !player.IsAdmin) return;
			
			if (IsReady())
			{
				Reply(player, "в очереди загрузки нет заданий для прерывания");
				return;
			}
			
			assets.AbortLoading();
		}
		
		/* Wipe And Init - вайп и реинициализация указанной группы данных с картинками, удаляет так же связанные данные из кеша */
		[ConsoleCommand("il.wipeandinit")]
        private void cmdWipeAndInit(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player != null && !player.IsAdmin) return;
			
			if (!IsReady())
			{
				Reply(player, "В очереди загрузки есть задания, необходимо или прервать их или дождатся их завершения.");
				return;
			}
			
			if (args.Args != null && args.Args.Length == 0)
			{
				Reply(player, "Использование: il.wipeandinit <standart | workshop | player | custom> - вайп и реинициализация указанной группы данных");
				return;
			}						
			
			CommunityEntity.ServerInstance.StartCoroutine(ClearDataByTypeAndInit(player, args.Args[0]));
		}
		
		#endregion
		
		#region Common
		
		private void Reply(BasePlayer player, string msg)
		{
			if (player != null)
				PrintToConsole(player, msg);
			else
				Puts(msg);
		}
		
		private static StdCtmSkinInfo GetCustomSkin(string value) => CustomImages.ContainsKey(value) ? CustomImages[value] : (StdCtmSkinInfo)null;
		
		#endregion
		
        #region Config        				
		
        private static ConfigData configData;
		
        private class ConfigData
        {            
			[JsonProperty(PropertyName = "Собирать и кешировать аватарки игроков")]
			public bool EnableStoreAvatars;
			[JsonProperty(PropertyName = "Загрузить и закешировать все разрешенные скины (не рекомендуется)")]
			public bool EnableStoreAllowWorkshopSkins;
			[JsonProperty(PropertyName = "Steam API Key")]
			public string SteamApiKey;
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                EnableStoreAvatars = false,
				EnableStoreAllowWorkshopSkins = false,
				SteamApiKey = "4069ACBB1052EDB8D40A24B448A5C397"
            };
            SaveConfig(configData);
			timer.Once(0.1f, ()=> SaveConfig(configData));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
		
		#region Data
		
		private class PlayerSkinInfo
		{		
			public string Url;
			public string Cache;		
		}
		
		private class StdCtmSkinInfo: PlayerSkinInfo // Standart + Custom SkinInfo
		{
			public string Name;
		}
		
		private class WorkshopSkinInfo: StdCtmSkinInfo
		{			
			public string shortname;
		}
		
		private static uint LastCEID;
		private static Dictionary<string, StdCtmSkinInfo> StandartSkins = new Dictionary<string, StdCtmSkinInfo>(); // ключ - item.info.shortname
		private static Dictionary<ulong, WorkshopSkinInfo> WorkshopSkins = new Dictionary<ulong, WorkshopSkinInfo>(); // ключ - skinID		
		private static Dictionary<ulong, PlayerSkinInfo> PlayerAvatars = new Dictionary<ulong, PlayerSkinInfo>(); // ключ - userID
		private static Dictionary<string, StdCtmSkinInfo> CustomImages = new Dictionary<string, StdCtmSkinInfo>(); // ключ - свой идентификатор картинки
		
		private void LoadData()
		{
			LastCEID = Interface.GetMod().DataFileSystem.ReadObject<uint>("ImageLibrary/LastCEIDData");
			StandartSkins = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<string, StdCtmSkinInfo>>("ImageLibrary/StandartSkinsData");
			WorkshopSkins = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, WorkshopSkinInfo>>("ImageLibrary/WorkshopSkinsData");			
			PlayerAvatars = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, PlayerSkinInfo>>("ImageLibrary/PlayerAvatarsData");
			CustomImages = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<string, StdCtmSkinInfo>>("ImageLibrary/CustomImagesData");
		}
		
		private static void SaveLastCEIDData() => Interface.GetMod().DataFileSystem.WriteObject("ImageLibrary/LastCEIDData", LastCEID);
		
		private static void SaveStandartSkinsData() => Interface.GetMod().DataFileSystem.WriteObject("ImageLibrary/StandartSkinsData", StandartSkins);
		
		private static void SaveWorkshopSkinsData() => Interface.GetMod().DataFileSystem.WriteObject("ImageLibrary/WorkshopSkinsData", WorkshopSkins);				
		
		private static void SavePlayerAvatarsData() => Interface.GetMod().DataFileSystem.WriteObject("ImageLibrary/PlayerAvatarsData", PlayerAvatars);
		
		private static void SaveCustomImagesData() => Interface.GetMod().DataFileSystem.WriteObject("ImageLibrary/CustomImagesData", CustomImages);
		
		#endregion
		
    }
}
