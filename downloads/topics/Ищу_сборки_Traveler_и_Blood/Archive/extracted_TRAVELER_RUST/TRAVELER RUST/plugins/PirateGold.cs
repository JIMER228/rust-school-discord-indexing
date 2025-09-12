using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("PirateGold", "https://discord.gg/9vyTXsJyKR", "1.0.0")] 
    class PirateGold : RustPlugin
    {            																					
								
		#region Variables

		[PluginReference]
        private Plugin ImageLibrary, ComponentPlus;
		
		private static System.Random Rnd = new System.Random();
		private static List<ulong> OpenButtons = new List<ulong>();
		private static Vector3 ShopPos = default(Vector3);
		
		#endregion
		
		#region Hooks
		
		private void Init() 
		{
			LoadVariables();
			LoadData();
		}
		
		private void OnServerInitialized()
        {
            PrintWarning("\n-----------------------------\n" +
            "     Author - https://topplugin.ru/\n" +
            "     VK - https://vk.com/rustnastroika\n" +
            "     Discord - https://discord.com/invite/5DPTsRmd3G\n" +
            "-----------------------------");
			OpenButtons.Clear();
			
			if (data.GoldGoods == null || data.GoldGoods.Count == 0)
				OnNewSave();
			
			if (!ImageLibrary)            
                PrintError("ImageLibrary не найдена!");
			else
				foreach (var itemList in data.GoldGoods)
					foreach (var item in itemList)
						ImageLibrary?.Call("GetImage", item.ItemName);
			
			var vending = BaseNetworkable.serverEntities.OfType<NPCVendingMachine>().FirstOrDefault(x=> x != null && x.shopName == configData.ShopName);
			if (vending == null)			
				PrintWarning("Не найден магазин для пиратских товаров!");
			else
				ShopPos = vending.transform.position;
			
			DownloadImages();
			
			var lootList = BaseNetworkable.serverEntities.OfType<LootContainer>().Where(x=> x != null && x.ShortPrefabName == "crate_underwater_basic").ToList();
			foreach (var loot in lootList) FillLoot(loot);
			
			data.TouchedLoot.Clear();
			foreach (var loot in lootList)			
				data.TouchedLoot.Add(loot.net.ID);			
			
			timer.Once(250f, ()=> SpawnPirateBoxes());			
			
			if (ShopPos != default(Vector3))
				CheckButtonClose();						
		}
		
		private void OnServerSave() => SaveData();
		
		private void OnNewSave()
		{
			data = new PirateData();
			
			foreach (var itemList in configData.GoldGoods)
			{
				int cnt = 0;
				var list = new List<BlackItem>();
				
				foreach (var item in itemList.OrderBy(x=> Rnd.Next()))
				{
					if (cnt >= 5) break;					
					list.Add(item);					
					cnt++;
				}
				
				data.GoldGoods.Add(list);
			}
			
			SaveData();						
		}
		
		private void Unload() 
		{									
			foreach (var player in BasePlayer.activePlayerList)
			{
				CuiHelper.DestroyUi(player, MainPanel);
				CuiHelper.DestroyUi(player, ButtonPanel);
			}
			
			SaveData();
		}
		
		private void OnServerShutdown()
		{
			// удаляем все пиратские ящики, что бы после рестарта иметь возможность заново зареспавнить ящики с не нулевым количеством в точках спавна							
			var boxes = BaseNetworkable.serverEntities.OfType<StorageContainer>().Where(x=> x != null && x.skinID == configData.BoxSkinID).ToList();
			foreach (var box in boxes)				
				if (box != null && !box.IsDestroyed) box.Kill();
						
			data.RockPirateBoxes.Clear();
			data.ShipPirateBoxes.Clear();
			
			SaveData();
		}
		
		private void OnLootEntity(BasePlayer player, StorageContainer entity)
		{									
			if (entity == null || entity.skinID != configData.BoxSkinID) return;
			ComponentPlus?.Call("ChangeContLoot", player, entity, true);
		}
		
		private bool? OnLootSpawn(LootContainer container)
		{
			if (container == null || !(container.ShortPrefabName == "crate_underwater_basic")) return null;
			
			FillLoot(container);
			return false;
		}
		
		private object OnItemSplit(Item item, int split_Amount)
        {
			if (item == null) return null;
			
			if (item.skin == configData.GoldSkinID)
			{
				var byItemId = ItemManager.CreateByName(configData.GoldItemName, 1, configData.GoldSkinID);			
				item.amount -= split_Amount;
				byItemId.amount = split_Amount;
				byItemId.name = item.name;
				item.MarkDirty();
				return byItemId;
			}
			
			if (item.skin == configData.KeySkinID)
			{
				var byItemId = ItemManager.CreateByName(configData.KeyItemName, 1, configData.KeySkinID);
				item.amount -= split_Amount;
				byItemId.amount = split_Amount;
				byItemId.name = item.name;
				item.MarkDirty();
				return byItemId;
			}
			
            return null;
        }
		
		private bool? CanCombineDroppedItem(DroppedItem drItem, DroppedItem anotherDrItem)
        {
			var item = drItem.item;
			var anotherItem = anotherDrItem.item;
			
            return CanStackItem(item, anotherItem);
        }

        private bool? CanStackItem(Item item, Item anotherItem)
        {
			if (item.info.shortname == configData.GoldItemName && anotherItem.info.shortname == configData.GoldItemName)
			{													
				if (item.skin == configData.GoldSkinID && anotherItem.skin != configData.GoldSkinID)
					return false;
				
				if (anotherItem.skin == configData.GoldSkinID && item.skin != configData.GoldSkinID)
					return false;
				
				if (item.skin == 0 && anotherItem.skin == 0) // запрещаем стак угля, т.к. он всегда по 1 штуке стакается
					return false;
			}
			
			if (item.info.shortname == configData.KeyItemName && anotherItem.info.shortname == configData.KeyItemName)
			{													
				if (item.skin == configData.KeySkinID && anotherItem.skin != configData.KeySkinID)
					return false;
				
				if (anotherItem.skin == configData.KeySkinID && item.skin != configData.KeySkinID)
					return false;			
			}
			
			return null;
        }
		
		private bool? CanPickupEntity(BasePlayer player, StorageContainer entity)
		{
			if (entity == null) return null;
			
			if (entity.skinID == configData.BoxSkinID)
				return false;						
			
			return null;
		}
		
		private void OnEntityTakeDamage(StorageContainer entity, HitInfo info)
        {
            if (entity == null || info == null || entity.skinID != configData.BoxSkinID) return;						
			info.damageTypes.ScaleAll(0);
        }		
		
		/*private void OnEntityKill(StorageContainer entity, HitInfo info)
		{
			if (entity == null || info == null || entity.skinID != configData.BoxSkinID) return;						
			foreach (var item in entity.inventory.itemList.ToList())
			{
				if (item != null)
				{
					item.RemoveFromContainer();
					item.Remove(0f);
				}
			}
		}*/
		
		private bool? CanDeployItem(BasePlayer player, Deployer deployer, uint entityId)
		{
			if (entityId == 0) return null;
			
			foreach (var pair in data.ShipPirateBoxes)
				if (pair.Value.Contains(entityId))								
					return false;				
				
			foreach (var pair in data.RockPirateBoxes)			
				if (pair.Value.Contains(entityId))
					return false;	
			
			return null;
		}
		
		private void OnPlayerLootEnd(PlayerLoot inventory)
		{
			if (inventory?.entitySource == null) return;
			
			if (inventory.entitySource.skinID == configData.BoxSkinID)
			{
				var box = inventory?.entitySource as StorageContainer;			
				
				if (box != null && !box.IsDestroyed && box.inventory.itemList.Count == 0)
					box.Die();
				
				return;
			}
			
			if (inventory.entitySource is NPCVendingMachine)
			{
				var player = inventory.GetComponent<BasePlayer>();
				if (player != null)
				{
					CuiHelper.DestroyUi(player, ButtonPanel);
					try { OpenButtons.Remove(player.userID); } catch {}
				}
			}
		}
		
		// кривой хук, он запретит работу рекуклера, даже если там есть разрешенные к переработке предметы
		private bool? CanRecycle(Recycler rec, Item item)
		{
			if (rec == null || item == null) return null;
			
			if (item.skin == configData.GoldSkinID || item.skin == configData.KeySkinID)
				return false;
			
			return null;
		}
		
		private bool? CanLootEntity(BasePlayer player, StorageContainer cont)
		{
			if (player == null || cont == null || cont.skinID != configData.BoxSkinID) return null;
			
			var slot = cont.GetSlot(BaseEntity.Slot.Lock) as KeyLock;
			if (slot == null) return null;			
			
			var item = player.GetActiveItem();
			if (item == null || item.skin != configData.KeySkinID)
			{
				SendReply(player, "Что бы открыть пиратский ящик, у вас в активном слоте должна быть отмычка.");
				return false; 
			}
						
			item.UseItem();
			Effect.server.Run("assets/bundled/prefabs/fx/item_break.prefab", player, 0, new Vector3(0f, 1.5f, 0f), Vector3.zero, null, false);
			slot.Kill();
			
			if (!data.GoldMiners.ContainsKey(player.userID))
				data.GoldMiners.Add(player.userID, 0);
			
			data.GoldMiners[player.userID] += cont.inventory.itemList.Where(x=> x.info.shortname == configData.GoldItemName && x.skin == configData.GoldSkinID).Sum(x=> x.amount);
			
			return null;
		}
		
		private void CanUseVending(BasePlayer player, NPCVendingMachine vending)
		{
			if (!(vending.shopName == configData.ShopName)) return;
			DrawButton(player);
		}
		
		#endregion
		
		#region Loot
		
		private void FillLoot(LootContainer cont)
		{
			if (cont == null || data.TouchedLoot.Contains(cont.net.ID)) return;												
						
			data.TouchedLoot.Add(cont.net.ID);
			
			cont.inventory.Clear();			
			ItemManager.DoRemoves();
			cont.PopulateLoot();
			
			var needSpawn = Rnd.Next(0, 1001)/10f <= configData.SpawnKeyPercent;			
			if (!needSpawn) return;						
			
			var item = ItemManager.CreateByName(configData.KeyItemName, 1, configData.KeySkinID);
			item.name = configData.KeyText;
			
			if (cont.inventory.capacity < cont.inventory.itemList.Count()+1)
				cont.inventory.capacity++;
			
			item.MoveToContainer(cont.inventory, -1, true);
		}
		
		private List<BlackItem> GetCurrentBlackItems()
		{
			var tmpWipeDt = SaveRestore.SaveCreatedTime.ToLocalTime();
			var wipeDt = new DateTime(tmpWipeDt.Year, tmpWipeDt.Month, tmpWipeDt.Day, configData.TriggerHour, 0, 0);
			var index = (DateTime.Now - wipeDt).Days;
			var item = data.GoldGoods.ElementAtOrDefault(index);
			
			return item == null ? data.GoldGoods.LastOrDefault() : item;
		}
		
		#endregion
		
		#region PirateBoxes
		
		private void FillPirateLoot(StorageContainer cont)
		{
			if (cont == null) return;
			
			foreach (var pack2 in configData.LootPacks)
			{
				var pack3 = pack2.OrderBy(x=> Rnd.Next()).FirstOrDefault();
				var loot = pack3.OrderBy(x=> Rnd.Next()).FirstOrDefault();
				
				var itemName = loot.LootName == "{GOLD}" ? configData.GoldItemName : loot.LootName;
				var itemText = loot.LootName == "{GOLD}" ? configData.GoldText : "";
				var itemSkin = loot.LootName == "{GOLD}" ? configData.GoldSkinID : 0ul;
				var itemAmount = Rnd.Next(loot.MinAmount, loot.MaxAmount+1);
				
				if (itemAmount <= 0) continue;								
				
				var item = ItemManager.CreateByName(itemName, itemAmount, itemSkin);
				
				if (!string.IsNullOrEmpty(itemText))
					item.name = itemText;								
				
				item.MoveToContainer(cont.inventory, -1, true);
			}
			
			cont.inventory.capacity = cont.inventory.itemList.Count;
			cont.inventorySlots = cont.inventory.itemList.Count;			
			cont.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);
			cont.inventory.MarkDirty();
		}
		
		private void SpawnPirateBoxes()
		{			
			CommunityEntity.ServerInstance.StartCoroutine(RunRefillPirateLoot());
			timer.Once(configData.RespawnMinutes * 60f, SpawnPirateBoxes);
		}
		
		private IEnumerator RunRefillPirateLoot()
		{
			int count = 0;
			//Puts("RunRefillPirateLoot Начало");			
						
			var devs = BaseNetworkable.serverEntities.OfType<DiveSite>().ToList();
			
			var mons = new List<Transform>();			
			foreach (var mon_ in GameObject.FindObjectsOfType<Transform>())
			{				
				if (mon_ != null && mon_.ToString().Contains("rock_formation") && mon_.ToString().Contains("_underwater"))				
					mons.Add(mon_);
				
				if (count++ % 1000 == 0)
					yield return null;
			}
											
			var boxes = BaseNetworkable.serverEntities.OfType<StorageContainer>().Where(x=> x != null && x.skinID == configData.BoxSkinID).ToList();
			
			// чистка списка пиратских ящиков от удаленных-залутаных ящиков (катера и баржи)
			count = 0;
			var newShipPirateBoxes = new Dictionary<uint, List<uint>>();
			foreach (var pair in data.ShipPirateBoxes)
			{
				foreach (var boxID in pair.Value)
				{
					if (boxID == 0)
					{
						if (!newShipPirateBoxes.ContainsKey(pair.Key))
							newShipPirateBoxes.Add(pair.Key, new List<uint>());
						
						newShipPirateBoxes[pair.Key].Add(boxID);
					}
					else
					{
						var box = boxes.FirstOrDefault(x=> x != null && x.net != null && x.net.ID == boxID);
						if (box != null && !box.IsDestroyed)
						{
							if (!newShipPirateBoxes.ContainsKey(pair.Key))
								newShipPirateBoxes.Add(pair.Key, new List<uint>());
							
							newShipPirateBoxes[pair.Key].Add(boxID);
						}
						yield return null;
					}
				}
			}						
			
			// чистка списка пиратских ящиков от удаленных-залутаных ящиков (скалы)
			count = 0;
			var newRockPirateBoxes = new Dictionary<string, List<uint>>();
			foreach (var pair in data.RockPirateBoxes)
			{
				foreach (var boxID in pair.Value)
				{
					if (boxID == 0)
					{
						if (!newRockPirateBoxes.ContainsKey(pair.Key))
							newRockPirateBoxes.Add(pair.Key, new List<uint>());
						
						newRockPirateBoxes[pair.Key].Add(boxID);
					}
					else
					{
						var box = boxes.FirstOrDefault(x=> x != null && x.net != null && x.net.ID == boxID);
						if (box != null && !box.IsDestroyed)
						{
							if (!newRockPirateBoxes.ContainsKey(pair.Key))
								newRockPirateBoxes.Add(pair.Key, new List<uint>());
							
							newRockPirateBoxes[pair.Key].Add(boxID);
						}
						yield return null;
					}
				}
			}						
			
			// чистка пиратских ящиков без точки спавна, только катера и баржи (а скалы не надо, они всегда существуют)
			count = 0;
			var newShipPirateBoxes2 = new Dictionary<uint, List<uint>>();
			foreach (var pair in newShipPirateBoxes)
			{
				var exists = false;
				foreach (var dev in devs)
				{
					if (dev == null || dev.IsDestroyed) continue;
					if (pair.Key == dev.net.ID)
					{
						exists = true;
						break;
					}
					
					if (count++ % 100 == 0)
						yield return null;
				}				
				
				if (!exists)
				{
					foreach (var boxID in pair.Value.Where(x=> x > 0))
					{
						var box = boxes.FirstOrDefault(x=> x != null && x.net != null && x.net.ID == boxID);
						if (box != null && !box.IsDestroyed) 
						{
							box.Kill();
							yield return new WaitForSeconds(0.1f);
						}
						else						
							if (count++ % 50 == 0)
								yield return null;						
					}
				}
				else
					newShipPirateBoxes2.Add(pair.Key, pair.Value);
			}									
			
			// добавляем новые ящики к новым точкам спавна (катера и баржи)
			foreach (var dev in devs)
			{
				if (dev == null || dev.IsDestroyed) continue;
				var devID = dev.net.ID;
				
				if (!newShipPirateBoxes2.ContainsKey(devID))
				{
					newShipPirateBoxes2.Add(devID, new List<uint>());
					
					var cfg = configData.SpawnLimits[dev.ShortPrefabName == "divesite_b" ? "баржа" : "катер"];
					var minAmount = Convert.ToInt32(cfg.Split('|')[0]);
					var maxAmount = Convert.ToInt32(cfg.Split('|')[1]);
					
					var amount = Rnd.Next(minAmount, maxAmount+1);
					if (amount <= 0)
						newShipPirateBoxes2[devID].Add(0);
					else
						for (int ii = 0; ii < amount; ii++)
						{
							var box = SpawnPirateBox(dev.transform.position, configData.SpawnRadius[dev.ShortPrefabName == "divesite_b" ? "баржа" : "катер"], configData.SpawnDelta[dev.ShortPrefabName == "divesite_b" ? "баржа" : "катер"]);
							if (box != null)
							{
								newShipPirateBoxes2[devID].Add(box.net.ID);
								yield return new WaitForSeconds(0.02f);
							}							
						}
						
					if (newShipPirateBoxes2[devID].Count == 0)
						newShipPirateBoxes2.Remove(devID);
				}				
			}												
			
			// добавляем новые ящики к новым точкам спавна (скалы)
			count = 0;
			foreach (var mon in mons)
			{			
				var monPos = $"{mon.position.x} {mon.position.y} {mon.position.z}";
				if (!newRockPirateBoxes.ContainsKey(monPos))
				{
					newRockPirateBoxes.Add(monPos, new List<uint>());
					
					var cfg = configData.SpawnLimits["скала"];
					var minAmount = Convert.ToInt32(cfg.Split('|')[0]);
					var maxAmount = Convert.ToInt32(cfg.Split('|')[1]);
					
					var amount = Rnd.Next(minAmount, maxAmount+1);
					if (amount <= 0)
						newRockPirateBoxes[monPos].Add(0);
					else
						for (int ii = 0; ii < amount; ii++)
						{
							var box = SpawnPirateBox(mon.position, configData.SpawnRadius["скала"], configData.SpawnDelta["скала"]);
							if (box != null)
							{
								newRockPirateBoxes[monPos].Add(box.net.ID);
								yield return new WaitForSeconds(0.1f);
							}							
						}
						
					if (newRockPirateBoxes[monPos].Count == 0)
						newRockPirateBoxes.Remove(monPos);
				}
								
				yield return null;
			}						
			
			try { data.ShipPirateBoxes = newShipPirateBoxes2; } catch {}
			try { data.RockPirateBoxes = newRockPirateBoxes; } catch {}
			
			//Puts("RunRefillPirateLoot Конец");			
		}
		
		private StorageContainer SpawnPirateBox(Vector3 pos, float radius, float delta)
		{
			Vector3 newPos = default(Vector3);
			for (int ii = 0; ii < 100; ii++)
			{
				var tmpPos = FindNewPosition(pos, radius);
				if (tmpPos == default(Vector3)) continue;				
				newPos = tmpPos;
				break;
			}
			
			if (newPos == default(Vector3))
				return null;
			
			newPos = new Vector3(newPos.x, newPos.y + delta, newPos.z);
			
			var storage = GameManager.server.CreateEntity("assets/prefabs/deployable/large wood storage/box.wooden.large.prefab", newPos, Quaternion.Euler(0, UnityEngine.Random.Range(0.0f, 360.0f), 0)) as StorageContainer;
			storage.skinID = configData.BoxSkinID;
			storage.dropChance = 0f;
			if (storage == null) return null;
			storage.Spawn();						
			
			timer.Once(0.1f, ()=> 
			{
				if (storage != null)
				{
					var code = GameManager.server.CreateEntity("assets/prefabs/locks/keylock/lock.key.prefab") as KeyLock;
					if (code != null)
					{
						code.Spawn();
						code.SetParent(storage, storage.GetSlotAnchorName(BaseEntity.Slot.Lock));
						storage.SetSlot(BaseEntity.Slot.Lock, code);
						code.SetFlag(BaseEntity.Flags.Locked, true); 
					}
					FillPirateLoot(storage);
				}
			});
			
			return storage;
		}
		
		private Vector3 FindNewPosition(Vector3 position, float radius)
        {			
            var targetPos = UnityEngine.Random.insideUnitCircle * radius;
            var sourcePos = new Vector3(position.x + targetPos.x, 300, position.z + targetPos.y);
			var hits = UnityEngine.Physics.SphereCastAll(sourcePos, 1.5f, Vector3.down, 500f);            			
            
			foreach (var hit in hits.OrderBy(x=> x.distance))
            {				
				var name = hit.collider?.name?.ToString();				
				if (name.Contains("Collider")) continue;
				
                if (name.Contains("Terrain"))
					return new Vector3(sourcePos.x, hit.point.y, sourcePos.z);
				
				return default(Vector3);
            }
			
			return default(Vector3);
        }
		
		private string GetPlayerName(ulong userID)
		{
			var data = permission.GetUserData(userID.ToString());															
			return data.LastSeenNickname;
		}
		
		#endregion
		
		#region Images				
		
		private static Dictionary<string, string> Images = new Dictionary<string, string>();
		
		private static bool AreImagesLoaded() => Images.Count >= 1;
		
		private void DownloadImages() 
		{			
			ServerMgr.Instance.StartCoroutine(DownloadImage(configData.GoldURL));
		}
		
		private IEnumerator DownloadImage(string url)
        {
            using (var www = new WWW(url))
            {
                yield return www;                
                if (www.error != null)                
                    PrintWarning($"Ошибка добавления изображения. Неверная ссылка на изображение:\n {url}");
                else
                {
                    var tex = www.texture;
                    byte[] bytes = tex.EncodeToPNG();															
                    var image = FileStorage.server.Store(bytes, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
					
					if (!Images.ContainsKey(url))
						Images.Add(url, image);
					else
						Images[url] = image;
					
                    UnityEngine.Object.DestroyImmediate(tex);
                    yield break;
                }
            }
        }
		
		#endregion
		
		#region Test				
					
		[ChatCommand("pg.loot")]
        private void cmdBox(BasePlayer player, string command, string[] args)
        {
			if (player == null || !player.IsAdmin) return;			
			
			var boxes = BaseNetworkable.serverEntities.OfType<StorageContainer>().Where(x=> x != null && x.skinID == configData.BoxSkinID).ToList();
				foreach (var box in boxes)				
					player.SendConsoleCommand("ddraw.text", 60f, Color.magenta, box.transform.position + new Vector3(0f, 2f, 0f), "ТУТ");												
		}
		
		[ChatCommand("pg.give")]
        private void cmdBox2(BasePlayer player, string command, string[] args)
        {
			if (player == null || !player.IsAdmin) return;
			
			var item = ItemManager.CreateByName(configData.KeyItemName, 10, configData.KeySkinID);
			item.name = configData.KeyText;
			
			player.GiveItem(item);
			
			var item2 = ItemManager.CreateByName(configData.GoldItemName, 100, configData.GoldSkinID);
			item2.name = configData.GoldText;
			
			player.GiveItem(item2);
		}
		
		[ConsoleCommand("pg.lock")]
        private void consoleLock(ConsoleSystem.Arg args)
        {
            if (args?.Player() != null) return;

			var boxes = BaseNetworkable.serverEntities.OfType<StorageContainer>().Where(x=> x != null && x.skinID == configData.BoxSkinID).ToList();
			foreach (var loot in boxes.Where(x=> x.GetSlot(BaseEntity.Slot.Lock) == null))
			{
				Puts(loot.transform.position.ToString());
			}			
		}
		
		[ChatCommand("gold_top")]
        private void CommandGoldTop(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;			
			
			int num = 0;
			var result = "ТОП собирателей золота за текущий вайп:\n";
			foreach (var pair in data.GoldMiners.OrderByDescending(x=> x.Value))
			{				
				result += $"{++num}. "+"<color=#56f0f0>" + GetPlayerName(pair.Key) + "</color>" + $": {pair.Value} шт\n";
				if (num >= 10) break;
			}
						
			SendReply(player, "<size=15>"+result+"</size>");
        }
		
		#endregion
		
		#region GUI + Button
		
		private void CheckButtonClose()
		{
			try
			{
				foreach (var userID in OpenButtons.ToList())
				{
					var player = BasePlayer.FindByID(userID);
					if (player == null)
						OpenButtons.Remove(userID);
					else
						if ((player.transform.position-ShopPos).sqrMagnitude > 16f)
						{						
							CuiHelper.DestroyUi(player, ButtonPanel);
							OpenButtons.Remove(userID);
						}
				}
			}
			catch {}
			
			timer.Once(5f, CheckButtonClose);
		}
		
		private void DrawButton(BasePlayer player)
		{
			if (player == null) return;
			var container = new CuiElementContainer();						
			
			UI_MainPanel(ref container, "0 0 0 0", "0.73 0.78", "0.87 0.84", false, false, false, ButtonPanel);
			UI_Button(ref container, "0.45 0.55 0.28 0.99", "<color=#b3f03d>КОНТРАБАНДА</color>", 18, "0 0", "1 1", "pg_349dfg.open", "robotocondensed-bold.ttf", TextAnchor.MiddleCenter, ButtonPanel);
			
			CuiHelper.DestroyUi(player, ButtonPanel);
            CuiHelper.AddUi(player, container);
			
			if (!OpenButtons.Contains(player.userID))
				OpenButtons.Add(player.userID);
		}
		
		[ConsoleCommand("pg_349dfg.open")]
        private void cmdGUIOpen(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null) return;
			
			//CuiHelper.DestroyUi(player, ButtonPanel);			
			//try { OpenButtons.Remove(player.userID); } catch {}

			player.EndLooting();
			DrawPanel(player);
		}
		
		#endregion
		
		#region GUI + Shop
		
		private void DrawPanel(BasePlayer player, bool refresh = false)
		{
			if (player == null || !AreImagesLoaded()) return;
			
			var container = new CuiElementContainer();						
			
			float minY = 217, deltaY = 3f, sizeY = 90f;
				
			if (!refresh)
			{
				UI_MainPanel(ref container, "0 0 0 0", "0 0", "1 1", false, true, false, MainPanel);
				
				UI_Button(ref container, "0 0 0 0", "", 15, "0 0", "1 1", "pg_349dfg.close", "robotocondensed-regular.ttf", TextAnchor.MiddleCenter, MainPanel);	
				
				UI_Panel(ref container, "0.36 0.34 0.32 0.95", "0.5 0.5", "0.5 0.5", MainPanel, "PG_Shop", $"-205 {minY - GetCurrentBlackItems().Count * (sizeY + deltaY) - deltaY+1}", "205 255", 0.1f, 0.1f);
				UI_Panel(ref container, "0 0 0 0", "0.5 0.5", "0.5 0.5", MainPanel, "PG_Shop_Title", "-200 220", "200 250", 0.1f, 0.1f);
				
				UI_FLabel(ref container, "<color=#CFCFCF>КОНТРАБАНДНЫЕ ТОВАРЫ ЗА ЗОЛОТО</color>", "0 0 0 0.1", 15, "-0.1 -0.1", "1.1 1.1", 0.1f, 0.1f, TextAnchor.MiddleCenter, "robotocondensed-bold.ttf", "PG_Shop_Title");
			}			
			
			var goldAmount = GetGoldAmount(player);
			
			int num = 0;
			foreach (var item in GetCurrentBlackItems())
			{
				if (!refresh)
				{
					var png = (string)ImageLibrary?.Call("GetImage", item.ItemName);
					
					UI_Panel(ref container, "0.27 0.25 0.22 0.85", "0.5 0.5", "0.5 0.5", MainPanel, $"PG_Shop_{num}", $"-200 {minY-sizeY}", $"200 {minY}", 0.1f, 0.1f);
					
					if (!string.IsNullOrEmpty(png))
						UI_Image(ref container, png, "0.025 0.1", "0.21 0.9", 0.1f, 0.1f, $"PG_Shop_{num}");
					else
						UI_FLabel(ref container, $"<color=#CFCFCF>{item.ItemName}</color>", "0 0 0 0.1", 14, "0 0.1", "0.25 0.9", 0.1f, 0.1f, TextAnchor.MiddleCenter, "robotocondensed-bold.ttf", $"PG_Shop_{num}");
					
					if (item.ItemAmount > 1)
						UI_FLabel(ref container, $"<color=#CFCFCF>x{item.ItemAmount}</color>", "0 0 0 0.1", 15, "0.025 0.05", "0.21 0.9", 0.1f, 0.1f, TextAnchor.LowerRight, "robotocondensed-bold.ttf", $"PG_Shop_{num}");
									
					UI_Image(ref container, Images[configData.GoldURL], "0.36 0.1", "0.54 0.9", 0.1f, 0.1f, $"PG_Shop_{num}");
					
					if (item.GoldAmount > 1)
						UI_FLabel(ref container, $"<color=#CFCFCF>x{item.GoldAmount}</color>", "0 0 0 0.1", 15, "0.36 0.05", "0.54 0.9", 0.1f, 0.1f, TextAnchor.LowerRight, "robotocondensed-bold.ttf", $"PG_Shop_{num}");
				}
				else
					CuiHelper.DestroyUi(player, $"PG_Shop_{num}_button");
				
				if (goldAmount >= item.GoldAmount)
					UI_Button(ref container, "0.45 0.55 0.28 0.99", "<color=#b3f03d>КУПИТЬ</color>", 15, "0.75 0.3", "0.95 0.7", $"pg_349dfg.buy {num}", "robotocondensed-bold.ttf", TextAnchor.MiddleCenter, $"PG_Shop_{num}", $"PG_Shop_{num}_button", 0f, 0f);
				else
					UI_Button(ref container, "0.33 0.20 0.16 0.99", "<color=#c03f36>не хватает\nзолота</color>", 13, "0.75 0.3", "0.95 0.7", "pg_349dfg.empty", "robotocondensed-bold.ttf", TextAnchor.MiddleCenter, $"PG_Shop_{num}", $"PG_Shop_{num}_button", 0f, 0f);
				
				num++;
				minY -= sizeY + deltaY;
			}
			
			if (!refresh)
				CuiHelper.DestroyUi(player, MainPanel);
			
            CuiHelper.AddUi(player, container);
		}				
		
		[ConsoleCommand("pg_349dfg.close")]
        private void cmdGUIClose(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null) return;
			
			CuiHelper.DestroyUi(player, MainPanel);
		}
		
		[ConsoleCommand("pg_349dfg.buy")]
        private void cmdGUIBuy(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null) return;
			
			int num = Convert.ToInt32(arg.Args[0]);			
			var item = GetCurrentBlackItems()[num];			
			var goldAmount = GetGoldAmount(player);
			
			if (goldAmount >= item.GoldAmount)
			{
				ProcessBuy(player, item);
				DrawPanel(player, true);
				ShowMessage(player, "Покупка успешно совершена.", true);
			}
			else			
			{
				DrawPanel(player, true);
				ShowMessage(player, "У вас не хватает золота.", false);
			}						
		}
		
		private static int GetGoldAmount(BasePlayer player)
		{
			if (player == null) return 0;
			
			int amount = 0;
			foreach (var item in player.inventory.AllItems())
			{
				if (item.info.shortname == configData.GoldItemName && item.skin == configData.GoldSkinID)
					amount += item.amount;
			}
			
			return amount;
		}
		
		private static void ProcessBuy(BasePlayer player, BlackItem item)
		{
			var newItem = ItemManager.CreateByName(item.ItemName, item.ItemAmount);
			
			int need = item.GoldAmount;
			foreach (var invItem in player.inventory.AllItems())
			{
				if (invItem.info.shortname == configData.GoldItemName && invItem.skin == configData.GoldSkinID)
				{
					if (invItem.amount > need)
					{
						invItem.amount -= need;
						invItem.MarkDirty();						
						break;
					}
					else
						if (invItem.amount == need)
						{
							invItem.RemoveFromContainer();
							invItem.Remove(0f);
							break;
						}
						else
						{
							need -= invItem.amount;
							invItem.RemoveFromContainer();
							invItem.Remove(0f);
						}
				}				
			}
			
			player.GiveItem(newItem);
		}
		
		#endregion
		
		#region GUI Message
		
		private static Dictionary<ulong, Timer> SMTimer = new Dictionary<ulong, Timer>();
		
		private void ShowMessage(BasePlayer player, string message, bool isOk)
		{						
			if (player == null || string.IsNullOrEmpty(message)) return;
		
			ClearMessages(player);						
			
			var container = new CuiElementContainer();																
			
			if (isOk)
				UI_Panel(ref container, "0.5 0.5 0.5 0.9", "0.35 0.87", "0.65 0.93", MainPanel, MainPanel + "_sm_container");
			else
				UI_Panel(ref container, "0.6 0.1 0.1 0.9", "0.35 0.87", "0.65 0.93", MainPanel, MainPanel + "_sm_container");
			
			UI_Label(ref container, message, 18, "0 0", "1 1", 0.1f, 0.1f, TextAnchor.MiddleCenter, "robotocondensed-bold.ttf", MainPanel + "_sm_container");
			
			CuiHelper.AddUi(player, container);	
			
			if (!SMTimer.ContainsKey(player.userID))
				SMTimer.Add(player.userID, null);
			
			SMTimer[player.userID] = timer.Once(5f, ()=> CuiHelper.DestroyUi(player, MainPanel + "_sm_container"));
		}
		
		private static void ClearMessages(BasePlayer player)
		{
			if (player == null) return;
			
			if (SMTimer.ContainsKey(player.userID) && SMTimer[player.userID] != null)
			{	
				SMTimer[player.userID].Destroy();
				SMTimer[player.userID] = null;
			}
			
			CuiHelper.DestroyUi(player, MainPanel + "_sm_container");
		}
		
		#endregion
		
		#region GUI Helpers
		
		private const string MainPanel = "PG_MainPanel";
		private const string ButtonPanel = "PG_ButtonPanel";
		
		private static void UI_MainPanel(ref CuiElementContainer container, string color, string aMin, string aMax, bool isHud = true, bool isNeedCursor = true, bool isBlur = false, string panel = MainPanel, string oMin = "0.0 0.0", string oMax = "0.0 0.0")
		{					
			container.Add(new CuiPanel
			{
				Image = { Color = color, Material = isBlur ? "assets/content/ui/uibackgroundblur.mat" : "Assets/Icons/IconMaterial.mat" },
				RectTransform = { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax },
				CursorEnabled = isNeedCursor
			}, isHud ? "Hud" : "Overlay", panel);
		}
		
		private static void UI_Panel(ref CuiElementContainer container, string color, string aMin, string aMax, string panel = MainPanel, string name = null, string oMin = "0.0 0.0", string oMax = "0.0 0.0", float fadeIn = 0f, float fadeOut = 0f)
		{			
			container.Add(new CuiPanel
			{
				FadeOut = fadeOut,
				Image = { Color = color, FadeIn = fadeIn, Material = "Assets/Content/UI/UI.Background.Tile.psd" },
				RectTransform = { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax }
			}, panel, name);
		}
		
		private static void UI_Label(ref CuiElementContainer container, string text, int size, string aMin, string aMax, float fadeIn = 0f, float fadeOut = 0f, TextAnchor align = TextAnchor.MiddleCenter, string font = "robotocondensed-regular.ttf", string panel = MainPanel, string name = null)
		{						
			container.Add(new CuiElement
			{
				Name = !string.IsNullOrEmpty(name) ? name : CuiHelper.GetGuid(),
				Parent = panel,
				FadeOut = fadeOut,
				Components =
				{
					new CuiTextComponent { FontSize = size, Align = align, Text = text, Font = font, FadeIn = fadeIn },
					new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax }					
				}
			});
		}
		
		private static void UI_FLabel(ref CuiElementContainer container, string text, string fcolor, int size, string aMin, string aMax, float fadeIn = 0f, float fadeOut = 0f, TextAnchor align = TextAnchor.MiddleCenter, string font = "robotocondensed-regular.ttf", string panel = MainPanel, string name = null)
		{						
			if (string.IsNullOrEmpty(fcolor))
				fcolor = "0.0 0.0 0.0 1.0";
			
			container.Add(new CuiElement
			{
				Name = !string.IsNullOrEmpty(name) ? name : CuiHelper.GetGuid(),
				Parent = panel,
				FadeOut = fadeOut,
				Components =
				{
					new CuiTextComponent { FontSize = size, Align = align, Text = text, Font = font, FadeIn = fadeIn },
					new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax },
					new CuiOutlineComponent { Distance = "1 1", Color = fcolor }
				}
			});
		}
				
		private static void UI_Button(ref CuiElementContainer container, string color, string text, int size, string aMin, string aMax, string command, string font = "robotocondensed-regular.ttf", TextAnchor align = TextAnchor.MiddleCenter, string panel = MainPanel, string name = null, float fadeIn = 0f, float fadeOut = 0f)
		{
			if (string.IsNullOrEmpty(color)) color = "0 0 0 0";
			
			container.Add(new CuiButton
			{
				FadeOut = fadeOut,
				Button = { Color = color, Command = command, FadeIn = fadeIn, Material = "Assets/Content/UI/UI.Background.Tile.psd" },
				RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
				Text = { Text = text, FontSize = size, Align = align, Font = font }
			}, panel, name);
		}
		
		private static void UI_Image(ref CuiElementContainer container, string png, string aMin, string aMax, float fadeIn = 0f, float fadeOut = 0f, string panel = MainPanel, string name = null, string oMin = null, string oMax = null)
		{
			container.Add(new CuiElement
			{
				Name = !string.IsNullOrEmpty(name) ? name : CuiHelper.GetGuid(),
				Parent = panel,
				FadeOut = fadeOut,
				Components =
				{
					(png.Contains("https://") || png.Contains("http://")) ? new CuiRawImageComponent { Sprite = "assets/content/textures/generic/fulltransparent.tga", Url = png, FadeIn = fadeIn } : new CuiRawImageComponent { Sprite = "assets/content/textures/generic/fulltransparent.tga", Png = png, FadeIn = fadeIn },
					string.IsNullOrEmpty(oMin) ? new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax } : new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax }
				}
			});
		}
		
		#endregion				
		
		#region Config
		
        private static ConfigData configData;
		
        private class ConfigData
        {
			[JsonProperty(PropertyName = "Скин пиратского ящика")]
			public ulong BoxSkinID;
			[JsonProperty(PropertyName = "Предмет для отмычки (shortname)")]
			public string KeyItemName;
			[JsonProperty(PropertyName = "Скин отмычки")]
			public ulong KeySkinID;
			[JsonProperty(PropertyName = "Текст для отмычки")]
			public string KeyText;
			[JsonProperty(PropertyName = "Предмет для золота (shortname)")]
			public string GoldItemName;
			[JsonProperty(PropertyName = "Скин золота")]
			public ulong GoldSkinID;
			[JsonProperty(PropertyName = "Текст для золота")]
			public string GoldText;
			[JsonProperty(PropertyName = "Максимальный радиус спавна пиратских ящиков")]
			public Dictionary<string, int> SpawnRadius;
			[JsonProperty(PropertyName = "Вертикальная дельта спавна пиратских ящиков")]
			public Dictionary<string, float> SpawnDelta;
			[JsonProperty(PropertyName = "Через сколько минут заново респавнить пиратские ящики")]
			public float RespawnMinutes;
			[JsonProperty(PropertyName = "Сколько минимум и максимум пиратских ящиков спавнить для каждого типа локации")]
			public Dictionary<string, string> SpawnLimits;			
			[JsonProperty(PropertyName = "Вероятность спавна отмычки в малом подводном ящике")]
			public float SpawnKeyPercent;
			[JsonProperty(PropertyName = "Пакеты с лутом для пиратского ящика")]
			public List<List<List<Loot>>> LootPacks;
			[JsonProperty(PropertyName = "Ссылка на картинку с золотом")]
			public string GoldURL;
			[JsonProperty(PropertyName = "Игровое название магазина торговца")]
			public string ShopName;
			[JsonProperty(PropertyName = "Час когда нужно переключить группу товаров (раз в день)")]
			public int TriggerHour;
			[JsonProperty(PropertyName = "Товары в обмен на золото")]
			public List<List<BlackItem>> GoldGoods;
        }				
		
		private class Loot
		{
			[JsonProperty(PropertyName = "Название лута (shortname)")]
			public string LootName;
			[JsonProperty(PropertyName = "Минимум штук")]
			public int MinAmount; 
			[JsonProperty(PropertyName = "Максимум штук")]
			public int MaxAmount; 			
		}
		
		private class BlackItem
		{
			[JsonProperty(PropertyName = "Название товара (shortname)")]
			public string ItemName;
			[JsonProperty(PropertyName = "Штук товара")]
			public int ItemAmount; 			
			[JsonProperty(PropertyName = "Стоимость золота")]
			public int GoldAmount;
		}
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
				BoxSkinID = 1382717194,
				KeyItemName = "sticks",
				KeySkinID = 2128568391,
				KeyText = "Отмычка",
				GoldItemName = "coal",
				GoldSkinID = 2128569139,
				GoldText = "Пиратское золото",
				SpawnDelta = new Dictionary<string, float>()
				{
					{ "катер", 0.75f },
					{ "баржа", 0.75f },
					{ "скала", 0.1f }
				},
				SpawnRadius = new Dictionary<string, int>()
				{
					{ "катер", 10 },
					{ "баржа", 25 },
					{ "скала", 25 }
				},
				RespawnMinutes = 20,
				SpawnLimits = new Dictionary<string, string>()
				{
					{ "катер", "0|2" },
					{ "баржа", "1|2" },
					{ "скала", "1|2" }
				},
				SpawnKeyPercent = 50, 
				GoldURL	= "https://i.imgur.com/aaVdAA6.png",
				ShopName = "Food Market",
				TriggerHour = 11,
				GoldGoods = new List<List<BlackItem>>()
				{
					new List<BlackItem>()
					{
						new BlackItem() { ItemName = "pistol.revolver", ItemAmount = 1, GoldAmount = 50 },
						new BlackItem() { ItemName = "shotgun.double", ItemAmount = 1, GoldAmount = 70 },
						new BlackItem() { ItemName = "pistol.semiauto", ItemAmount = 1, GoldAmount = 100 },
						new BlackItem() { ItemName = "pistol.python", ItemAmount = 1, GoldAmount = 100 },
						new BlackItem() { ItemName = "grenade.beancan", ItemAmount = 1, GoldAmount = 80 },
						new BlackItem() { ItemName = "bed", ItemAmount = 1, GoldAmount = 50 },
						new BlackItem() { ItemName = "scrap", ItemAmount = 100, GoldAmount = 100 },
						new BlackItem() { ItemName = "ladder.wooden.wall", ItemAmount = 1, GoldAmount = 70 },
						new BlackItem() { ItemName = "wall.frame.garagedoor", ItemAmount = 1, GoldAmount = 150 },
						new BlackItem() { ItemName = "wall.window.glass.reinforced", ItemAmount = 1, GoldAmount = 70 }
					},
					new List<BlackItem>()
					{
						new BlackItem() { ItemName = "pistol.semiauto", ItemAmount = 1, GoldAmount = 100 },
						new BlackItem() { ItemName = "pistol.python", ItemAmount = 1, GoldAmount = 100 },
						new BlackItem() { ItemName = "smg.thompson", ItemAmount = 1, GoldAmount = 150 },
						new BlackItem() { ItemName = "rifle.semiauto", ItemAmount = 1, GoldAmount = 150 },
						new BlackItem() { ItemName = "explosive.satchel", ItemAmount = 1, GoldAmount = 200 },
						new BlackItem() { ItemName = "flamethrower", ItemAmount = 1, GoldAmount = 150 },
						new BlackItem() { ItemName = "wall.frame.garagedoor", ItemAmount = 1, GoldAmount = 150 },
						new BlackItem() { ItemName = "floor.ladder.hatch", ItemAmount = 1, GoldAmount = 100 },
						new BlackItem() { ItemName = "floor.triangle.ladder.hatch", ItemAmount = 1, GoldAmount = 100 },
						new BlackItem() { ItemName = "scrap", ItemAmount = 100, GoldAmount = 100 }
					},
					new List<BlackItem>()
					{
						new BlackItem() { ItemName = "smg.thompson", ItemAmount = 1, GoldAmount = 150 },
						new BlackItem() { ItemName = "rifle.semiauto", ItemAmount = 1, GoldAmount = 150 },
						new BlackItem() { ItemName = "coffeecan.helmet", ItemAmount = 1, GoldAmount = 100 },
						new BlackItem() { ItemName = "roadsign.jacket", ItemAmount = 1, GoldAmount = 100 },
						new BlackItem() { ItemName = "roadsign.kilt", ItemAmount = 1, GoldAmount = 100 },
						new BlackItem() { ItemName = "scrap", ItemAmount = 100, GoldAmount = 100 },
						new BlackItem() { ItemName = "ladder.wooden.wall", ItemAmount = 1, GoldAmount = 70 },
						new BlackItem() { ItemName = "small.oil.refinery", ItemAmount = 1, GoldAmount = 100 },
						new BlackItem() { ItemName = "furnace.large", ItemAmount = 1, GoldAmount = 100 },
						new BlackItem() { ItemName = "explosive.satchel", ItemAmount = 1, GoldAmount = 200 }
					},
					new List<BlackItem>()
					{
						new BlackItem() { ItemName = "gates.external.high.stone", ItemAmount = 1, GoldAmount = 200 },
						new BlackItem() { ItemName = "wall.external.high.stone", ItemAmount = 1, GoldAmount = 150 },
						new BlackItem() { ItemName = "floor.ladder.hatch", ItemAmount = 1, GoldAmount = 100 },
						new BlackItem() { ItemName = "floor.triangle.ladder.hatch", ItemAmount = 1, GoldAmount = 100 },
						new BlackItem() { ItemName = "door.hinged.toptier", ItemAmount = 1, GoldAmount = 200 },
						new BlackItem() { ItemName = "rifle.semiauto", ItemAmount = 1, GoldAmount = 150 },
						new BlackItem() { ItemName = "scrap", ItemAmount = 100, GoldAmount = 100 },
						new BlackItem() { ItemName = "coffeecan.helmet", ItemAmount = 1, GoldAmount = 100 },
						new BlackItem() { ItemName = "roadsign.jacket", ItemAmount = 1, GoldAmount = 100 },
						new BlackItem() { ItemName = "roadsign.kilt", ItemAmount = 1, GoldAmount = 100 }
					},
					new List<BlackItem>()
					{
						new BlackItem() { ItemName = "door.hinged.toptier", ItemAmount = 1, GoldAmount = 200 },
						new BlackItem() { ItemName = "door.double.hinged.toptier", ItemAmount = 1, GoldAmount = 250 },
						new BlackItem() { ItemName = "gates.external.high.stone", ItemAmount = 1, GoldAmount = 200 },
						new BlackItem() { ItemName = "wall.external.high.stone", ItemAmount = 1, GoldAmount = 150 },
						new BlackItem() { ItemName = "ladder.wooden.wall", ItemAmount = 1, GoldAmount = 70 },
						new BlackItem() { ItemName = "explosive.satchel", ItemAmount = 1, GoldAmount = 200 },
						new BlackItem() { ItemName = "smg.thompson", ItemAmount = 1, GoldAmount = 150 },
						new BlackItem() { ItemName = "scrap", ItemAmount = 100, GoldAmount = 100 },
						new BlackItem() { ItemName = "small.oil.refinery", ItemAmount = 1, GoldAmount = 100 },
						new BlackItem() { ItemName = "furnace.large", ItemAmount = 1, GoldAmount = 100 }
					},
					new List<BlackItem>()
					{
						new BlackItem() { ItemName = "wall.window.glass.reinforced", ItemAmount = 1, GoldAmount = 70 },
						new BlackItem() { ItemName = "bed", ItemAmount = 1, GoldAmount = 50 },
						new BlackItem() { ItemName = "floor.ladder.hatch", ItemAmount = 1, GoldAmount = 100 },
						new BlackItem() { ItemName = "floor.triangle.ladder.hatch", ItemAmount = 1, GoldAmount = 100 },
						new BlackItem() { ItemName = "wall.frame.garagedoor", ItemAmount = 1, GoldAmount = 150 },
						new BlackItem() { ItemName = "rifle.semiauto", ItemAmount = 1, GoldAmount = 150 },
						new BlackItem() { ItemName = "smg.thompson", ItemAmount = 1, GoldAmount = 150 },
						new BlackItem() { ItemName = "scrap", ItemAmount = 100, GoldAmount = 100 },
						new BlackItem() { ItemName = "door.hinged.toptier", ItemAmount = 1, GoldAmount = 200 },
						new BlackItem() { ItemName = "door.double.hinged.toptier", ItemAmount = 1, GoldAmount = 250 }
					},
					new List<BlackItem>()
					{
						new BlackItem() { ItemName = "scrap", ItemAmount = 100, GoldAmount = 100 },
						new BlackItem() { ItemName = "gates.external.high.stone", ItemAmount = 1, GoldAmount = 200 },
						new BlackItem() { ItemName = "wall.external.high.stone", ItemAmount = 1, GoldAmount = 150 },
						new BlackItem() { ItemName = "smg.thompson", ItemAmount = 1, GoldAmount = 150 },
						new BlackItem() { ItemName = "rifle.semiauto", ItemAmount = 1, GoldAmount = 150 },
						new BlackItem() { ItemName = "small.oil.refinery", ItemAmount = 1, GoldAmount = 100 },
						new BlackItem() { ItemName = "furnace.large", ItemAmount = 1, GoldAmount = 100 },
						new BlackItem() { ItemName = "explosive.satchel", ItemAmount = 1, GoldAmount = 200 },
						new BlackItem() { ItemName = "wall.frame.garagedoor", ItemAmount = 1, GoldAmount = 150 },
						new BlackItem() { ItemName = "floor.ladder.hatch", ItemAmount = 1, GoldAmount = 100 }
					}
				},
				LootPacks = new List<List<List<Loot>>>()
				{
					new List<List<Loot>>()
					{
						new List<Loot>()
						{
							new Loot() { LootName = "{GOLD}", MinAmount = 10, MaxAmount = 15 },
						}
					},
					new List<List<Loot>>()
					{
						new List<Loot>()
						{
							new Loot() { LootName = "scrap", MinAmount = 10, MaxAmount = 20 },
						}
					},
					new List<List<Loot>>()
					{
						new List<Loot>()
						{
							new Loot() { LootName = "metal.refined", MinAmount = 10, MaxAmount = 20 },
							new Loot() { LootName = "metal.fragments", MinAmount = 100, MaxAmount = 200 },
							new Loot() { LootName = "gunpowder", MinAmount = 75, MaxAmount = 150 },
							new Loot() { LootName = "sulfur", MinAmount = 100, MaxAmount = 200 },
							new Loot() { LootName = "lowgradefuel", MinAmount = 30, MaxAmount = 70 },
							new Loot() { LootName = "crude.oil", MinAmount = 10, MaxAmount = 20 }
						}
					},
					new List<List<Loot>>()
					{
						new List<Loot>()
						{
							new Loot() { LootName = "metalblade", MinAmount = 2, MaxAmount = 4 },
							new Loot() { LootName = "rope", MinAmount = 2, MaxAmount = 4 },
							new Loot() { LootName = "propanetank", MinAmount = 2, MaxAmount = 4 },
							new Loot() { LootName = "sewingkit", MinAmount = 2, MaxAmount = 4 },
							new Loot() { LootName = "tarp", MinAmount = 2, MaxAmount = 4 },
							new Loot() { LootName = "gears", MinAmount = 1, MaxAmount = 3 },
							new Loot() { LootName = "metalspring", MinAmount = 1, MaxAmount = 2 },
							new Loot() { LootName = "metalpipe", MinAmount = 1, MaxAmount = 3 },
							new Loot() { LootName = "semibody", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "sheetmetal", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "roadsigns", MinAmount = 2, MaxAmount = 3 }
						}
					},
					new List<List<Loot>>()
					{
						new List<Loot>()
						{
							new Loot() { LootName = "ceilinglight", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "computerstation", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "smart.alarm", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "smart.switch", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.battery.rechargable.large", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.battery.rechargable.medium", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.battery.rechargable.small", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.button", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.counter", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.hbhfsensor", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.laserdetector", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.pressurepad", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.doorcontroller", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.heater", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "fluid.combiner", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "fluid.splitter", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "fluid.switch", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.andswitch", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.blocker", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electrical.branch", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electrical.combiner", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electrical.memorycell", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.orswitch", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.random.switch", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.rf.broadcaster", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.rf.receiver", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.xorswitch", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.fuelgenerator.small", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.solarpanel.large", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.igniter", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.flasherlight", MinAmount = 1, MaxAmount = 1 },							
							new Loot() { LootName = "electric.sirenlight", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "powered.water.purifier", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.switch", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.splitter", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.sprinkler", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.teslacoil", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "electric.timer", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "waterpump", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "generator.wind.scrap", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "autoturret", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "rf_pager", MinAmount = 1, MaxAmount = 1 }
						}
					},
					new List<List<Loot>>()
					{
						new List<Loot>()
						{
							new Loot() { LootName = "fuse", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "gates.external.high.wood", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "wall.external.high", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "shutter.metal.embrasure.a", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "shutter.metal.embrasure.b", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "wall.window.glass.reinforced", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "wall.window.bars.metal", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "floor.grill", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "floor.triangle.grill", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "wall.frame.fence.gate", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "wall.frame.fence", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "ladder.wooden.wall", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "wall.frame.garagedoor", MinAmount = 1, MaxAmount = 1 }
						},
						new List<Loot>()
						{
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "flameturret", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "guntrap", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "trap.bear", MinAmount = 2, MaxAmount = 4 },
							new Loot() { LootName = "trap.landmine", MinAmount = 1, MaxAmount = 3 },
							new Loot() { LootName = "barricade.wood", MinAmount = 2, MaxAmount = 5 },
							new Loot() { LootName = "barricade.woodwire", MinAmount = 2, MaxAmount = 5 },
							new Loot() { LootName = "fridge", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "dropbox", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "bed", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "tool.binoculars", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "horse.shoes.advanced", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "horse.armor.roadsign", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "shelves", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "rug", MinAmount = 1, MaxAmount = 1 }
						},
						new List<Loot>()
						{
							new Loot() { LootName = "salvaged.sword", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "knife.combat", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "hatchet", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "pickaxe", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "weapon.mod.silencer", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "flashlight.held", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "weapon.mod.muzzleboost", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "weapon.mod.muzzlebrake", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "weapon.mod.lasersight", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "shotgun.waterpipe", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "shotgun.pump", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "shotgun.double", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "pistol.revolver", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "grenade.beancan", MinAmount = 1, MaxAmount = 2 },
							new Loot() { LootName = "explosive.satchel", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "explosives", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "bow.compound", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "crossbow", MinAmount = 1, MaxAmount = 1 }
						},
						new List<Loot>()
						{
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "", MinAmount = 0, MaxAmount = 0 },
							new Loot() { LootName = "diving.fins", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "diving.mask", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "diving.tank", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "diving.wetsuit", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "wood.armor.pants", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "wood.armor.jacket", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "riot.helmet", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "coffeecan.helmet", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "pants", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "hoodie", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "jacket.snow", MinAmount = 1, MaxAmount = 1 },
							new Loot() { LootName = "nightvisiongoggles", MinAmount = 1, MaxAmount = 1 }
						}
					}
				}
            };
            SaveConfig(configData);
			timer.Once(0.1f, ()=> SaveConfig(configData));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
		
		#region Data
		
		private static PirateData data = new PirateData();
		
		private class PirateData
		{
			public HashSet<uint> TouchedLoot = new HashSet<uint>();
			public Dictionary<string, List<uint>> RockPirateBoxes = new Dictionary<string, List<uint>>();									
			public Dictionary<uint, List<uint>> ShipPirateBoxes = new Dictionary<uint, List<uint>>();
			public Dictionary<ulong, int> GoldMiners = new Dictionary<ulong, int>();
			public List<List<BlackItem>> GoldGoods = new List<List<BlackItem>>();
		}
		
		private void LoadData() 
		{
			data = Interface.GetMod().DataFileSystem.ReadObject<PirateData>("PirateGoldData");
			
			if (data == null)
				data = new PirateData();
			
			if (data.TouchedLoot == null)
				data.TouchedLoot = new HashSet<uint>();						
			
			if (data.RockPirateBoxes == null)
				data.RockPirateBoxes = new Dictionary<string, List<uint>>();						
			
			if (data.ShipPirateBoxes == null)
				data.ShipPirateBoxes = new Dictionary<uint, List<uint>>();
			
			if (data.GoldMiners == null)
				data.GoldMiners = new Dictionary<ulong, int>();
			
			if (data.GoldGoods == null)
				data.GoldGoods = new List<List<BlackItem>>();
		}
		
		private void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("PirateGoldData", data);		
		
		#endregion			
				
	}
	
}