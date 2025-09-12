using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("FixSkins", "Nimant", "3.1.6")]
    class FixSkins : RustPlugin
    {            		
				
		#region Variables		
				
		private static PropertyInfo Definitions = typeof(Steamworks.SteamInventory).GetProperty("Definitions", (BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
		private static FieldInfo _defMap = typeof(Steamworks.SteamInventory).GetField("_defMap", (BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance));
		private static FieldInfo _properties = typeof(Steamworks.InventoryDef).GetField("_properties", (BindingFlags.NonPublic | BindingFlags.Instance));		
		private static List<Rust.Workshop.ItemSchema.Item> WebItemSkins1 = new List<Rust.Workshop.ItemSchema.Item>();
		private static List<Rust.Workshop.ItemSchema.Item> WebItemSkins2 = new List<Rust.Workshop.ItemSchema.Item>();
		private static List<Rust.Workshop.ItemSchema.Item> WebItemSkins3 = new List<Rust.Workshop.ItemSchema.Item>();
		private static List<Rust.Workshop.ItemSchema.Item> SchemaData = new List<Rust.Workshop.ItemSchema.Item>();
		
		#endregion
		
		#region Init
		
		private void Init() => LoadData();
		
		private void OnServerInitialized() 
		{
			WebItemSkins1.Clear();
			WebItemSkins2.Clear();
			WebItemSkins3.Clear();
			timer.Once(30f, ()=> TryLoadWebSkins());
		}
		
		#endregion
		
		#region Main
		
		private void TryLoadWebSkins(int countRepeat = 0)
		{
			if (GetSkinItemsInfoWeb().Count() > 0 || countRepeat > 5) 
			{				
				ReloadAllSkins();
				return;
			}
			
			webrequest.Enqueue("https://files.facepunch.com/rust/icons/inventory/rust/schema.json", null, (int code, string response) =>
			{
				if (response != null && code == 200)
				{
					var schema = JsonConvert.DeserializeObject<Rust.Workshop.ItemSchema>(response);
					
					if (schema != null && schema.items != null)
					{
						WebItemSkins1 = schema.items.ToList();						
						Puts($"на сайте facepunch {WebItemSkins1.Count} шт");
					}
				}
			}, this);
			
			webrequest.Enqueue("https://rust-map.ru/schema.php", null, (int code, string response) =>
			{
				if (response != null && code == 200)
				{
					var schema = JsonConvert.DeserializeObject<Rust.Workshop.ItemSchema>(response);
					
					if (schema != null && schema.items != null)
					{						
						WebItemSkins2 = schema.items.ToList();						
						Puts($"на сайте rust-map {WebItemSkins2.Count} шт");
					}
				}
			}, this);
			
			webrequest.Enqueue("http://rust-schema.zzz.com.ua/schema.php", null, (int code, string response) =>
			{
				if (response != null && code == 200)
				{
					var schema = JsonConvert.DeserializeObject<Rust.Workshop.ItemSchema>(response);
					
					if (schema != null && schema.items != null)
					{						
						WebItemSkins3 = schema.items.ToList();						
						Puts($"на сайте rust-schema {WebItemSkins3.Count} шт");
					}
				}
			}, this);
											
			timer.Once(10f, ()=> TryLoadWebSkins(++countRepeat));
		}
		
		private void ReloadAllSkins()
		{						
			var defs = new List<Steamworks.InventoryDef>();
								
			if (Steamworks.SteamInventory.Definitions != null)
			{
				Puts($"Всего в наличии скинов {Steamworks.SteamInventory.Definitions.Length} шт.");
				
				foreach (var item in Steamworks.SteamInventory.Definitions)
					defs.Add(item);
			}
			else
				Puts($"Всего в наличии скинов 0 шт.");
						
			int count = 0;
			List<Rust.Workshop.ItemSchema.Item> items = SchemaData;
			string source = "";
			
			if (items.Count < GetSkinItemsInfoWeb().Count)
				items = GetSkinItemsInfoWeb();
			
			if (items.Count < GetSkinItemsInfoInbuild().Count)
				items = GetSkinItemsInfoInbuild();
			
			if (SchemaData.Count < items.Count)
			{
				SchemaData = items;
				SaveData();
			}			
			
			if (items.Count() == 0)
			{
				PrintWarning("Источник для дозагрузки скинов пуст, дозагрузка невозможна!");
				return;
			}
			
			foreach (var item in items)
			{
				if (item == null || string.IsNullOrEmpty(item.itemshortname) || defs.Exists(x=> x.Id == (int)item.itemdefid)) continue;
				
				var itemSkin = new Steamworks.InventoryDef((int)item.itemdefid);
				
				var props = new Dictionary<string, string>();
				props.Add("itemshortname", item.itemshortname);
				props.Add("workshopid", item.workshopid);
				props.Add("workshopdownload", item.workshopdownload);
				
				_properties.SetValue(itemSkin, props);
				
				defs.Add(itemSkin);
				count++;
			}

			if (count > 0)
			{
				Definitions.SetValue(null, defs.ToArray());
				
				var maps = new Dictionary<int, Steamworks.InventoryDef>();
				var definitions = Steamworks.SteamInventory.Definitions;
				for (int i = 0; i < (int)definitions.Length; i++)
					maps.Add(definitions[i].Id, definitions[i]);						
				
				_defMap.SetValue(null, maps);
				
				PrintWarning($"Догружено {count} пропавших скинов!");
				PrintWarning($"Окончательно стало в наличии {Steamworks.SteamInventory.Definitions.Length} шт скинов.");
			}
			else
				Puts($"Все скины на месте, дозагрузка не требуется.");
		}
		
		private static List<Rust.Workshop.ItemSchema.Item> GetSkinItemsInfoInbuild()
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
				result.Add(item);
			}
			
			return result;
		}

		private static List<Rust.Workshop.ItemSchema.Item> GetSkinItemsInfoWeb() 
		{
			List<Rust.Workshop.ItemSchema.Item> result = null;
			
			if (WebItemSkins1.Count > WebItemSkins2.Count)
				result = WebItemSkins1;
			else
				result = WebItemSkins2;
			
			if (result.Count < WebItemSkins3.Count)
				result = WebItemSkins3;
			
			return result;
		}
		
		#endregion
		
		#region Data
		
		/*private class SItem
		{
			public int itemdefid;
			public string itemshortname;
			public string workshopid;
			public string workshopdownload;	

			public SItem(int a, string b, string c, string d)
			{
				itemdefid = a;
				itemshortname = !string.IsNullOrEmpty(b) ? b : "";
				workshopid = !string.IsNullOrEmpty(c) ? c : "";
				workshopdownload = !string.IsNullOrEmpty(d) ? d : "";
			}
		}*/
		
		private void LoadData() => SchemaData = Interface.GetMod().DataFileSystem.ReadObject<List<Rust.Workshop.ItemSchema.Item>>("FixSkinsData");		
		
		private static void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("FixSkinsData", SchemaData);
		
		#endregion
		
    }	
	
}