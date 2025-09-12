using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Physics = UnityEngine.Physics;
using System.IO;

namespace Oxide.Plugins 
{
	[Info("Backpack", "OxideBro|Nimant", "1.4.24")] 
	public class Backpack: RustPlugin 
	{
		private static Backpack ins;
		
		private object OnEntityGroundMissing(BaseEntity entity) 
		{
			var container = entity as DroppedItemContainer;
			if (container != null) 
			{
				var opened = openedBackpacks.Values.Select(x => x.storage);
				if (opened.Contains(container)) return false;
			}
			
			return null;
		}
		
		private class BackpackBox: MonoBehaviour 
		{
			public DroppedItemContainer storage;
			BasePlayer owner;
			
			public ulong GetOwnerID() => owner.userID;
			
			public void Init(DroppedItemContainer storage, BasePlayer owner) 
			{
				this.storage = storage;
				this.owner = owner;			
			}
			
			public static BackpackBox Spawn(BasePlayer player, ulong ownerid, int size = 1) 
			{
				player.EndLooting();
				var storage = SpawnContainer(player, size, false, ownerid);
				var box = storage.gameObject.AddComponent < BackpackBox > ();
				box.Init(storage, player);
				return box;
			}
			
			private static int rayColl = LayerMask.GetMask("Construction", "Deployed", "Tree", "Terrain", "Resource", "World", "Water", "Default", "Prevent Building");
			
			public static DroppedItemContainer SpawnContainer(BasePlayer player, int size, bool die, ulong ownerid) 
			{
				var pos = player.transform.position;
				if (die) 
				{
					RaycastHit hit;
					if (Physics.Raycast(new Ray(player.GetCenter(), Vector3.down), out hit, 1000, rayColl, QueryTriggerInteraction.Ignore)) 					
						pos = hit.point;					
				} 
				else 				
					pos -= new Vector3(0, 100, 0);					
				
				return SpawnContainer(player, size, pos, ownerid);
			}
			
			private static DroppedItemContainer SpawnContainer(BasePlayer player, int size, Vector3 position, ulong ownerid) 
			{
				var storage = GameManager.server.CreateEntity("assets/prefabs/misc/item drop/item_drop_backpack.prefab") as DroppedItemContainer;
				if (storage == null) return null;
				storage.transform.position = position;
				//storage.panelName = "genericlarge";
				ItemContainer container = new ItemContainer();
				container.ServerInitialize(null, !ownerid.IsSteamId() ? ins.GetBackpackSize(player.UserIDString) : ins.GetBackpackSize(ownerid.ToString()));
				if ((int) container.uid == 0) container.GiveUID();
				storage.inventory = container;
				if (!storage) return null;
				storage.SendMessage("SetDeployedBy", player, (SendMessageOptions) 1);
				storage.OwnerID = player.userID;
				storage.playerSteamID = player.userID; 
				storage.playerName = "Ваш рюкзак";								
				storage.Spawn();				
				return storage;
			}
			
			private void PlayerStoppedLooting(BasePlayer player) 
			{			
				Interface.Oxide.RootPluginManager.GetPlugin("Backpack").Call("BackpackHide", player.userID);
			}
			
			public void Close() 
			{
				ClearItems();
				if (storage != null && !storage.IsDestroyed)
					storage.Kill();
			}
			
			public void StartLoot() 
			{
				if (storage == null) return;
				storage.SetFlag(BaseEntity.Flags.Open, true, false);				
				owner.inventory.loot.StartLootingEntity(storage, false);				
				owner.inventory.loot.AddContainer(storage.inventory);				
				owner.inventory.loot.SendImmediate();				
				owner.ClientRPCPlayer(null, owner, "RPC_OpenLootPanel", storage.lootPanelName);				
				//storage.DecayTouch();
				storage.SendNetworkUpdate();				
			}
			
			public void Push(List<Item> items) 
			{
				if (items == null || storage == null) return;
				for (int i = items.Count - 1; i >= 0; i--) items[i].MoveToContainer(storage.inventory);
			}
			
			public void ClearItems() 
			{
				if (storage == null) return;
				storage?.inventory?.itemList?.Clear();
			}						
			
			public List<Item> GetItems() 
			{
				if (storage == null) return new List<Item>();
				return storage?.inventory?.itemList?.Where(i => i != null).ToList();
			}
		}
		
		private static Dictionary < ulong, BackpackBox > openedBackpacks = new Dictionary < ulong, BackpackBox > ();
		private static Dictionary < ulong, List < SavedItem >> savedBackpacks;
		private static Dictionary < ulong, BaseEntity > visualBackpacks = new Dictionary < ulong, BaseEntity > ();				
		
		private DynamicConfigFile backpacksFile = Interface.Oxide.DataFileSystem.GetFile("BackpackData");
		
		private void LoadBackpacks() 
		{
			try 
			{
				savedBackpacks = backpacksFile.ReadObject < Dictionary < ulong, List < SavedItem >>> ();
			}
			catch (Exception) 
			{
				savedBackpacks = new Dictionary < ulong, List < SavedItem >> ();
			}
		}
		
		private void OnServerSave() 
		{
			SaveBackpacks();
		}
		
		private void SaveBackpacks() => backpacksFile.WriteObject(savedBackpacks);
		
		private object CanMoveItem(Item item, PlayerInventory playerLoot, uint targetContainer, int targetSlot, int amount) 
		{
			if (item == null || playerLoot == null) return null;
			var player = playerLoot.containerMain.playerOwner;
			if (player == null) return null;
						
			if (openedBackpacks.ContainsKey(player.userID)) 
			{
				var target = playerLoot.FindContainer(targetContainer)?.GetSlot(targetSlot);
				
				if (target != null && targetContainer != item.GetRootContainer()?.uid) 
				{
					if (!PermissionService.HasPermission(player.UserIDString, BPIgnoreBlackListed))
						if (BlackListed.Contains(target.info.shortname)) 
						{
							SendReply(player, $"Данный предмет <color=#ff566c>{target.info.displayName.english}</color> запрещено класть в рюкзак!");
							return false;
						}
				}
				
				if (openedBackpacks[player.userID].storage?.inventory?.uid == targetContainer) 
				{
					if (!PermissionService.HasPermission(player.UserIDString, BPIgnoreBlackListed))
						if (BlackListed.Contains(item.info.shortname)) 
						{
							SendReply(player, $"Данный предмет <color=#ff566c>{target.info.displayName.english}</color> запрещено класть в рюкзак!");
							return false;
						}
				}
				
				if (DisabledMoveOtherBackpack && openedBackpacks[player.userID].storage?.OwnerID != player.userID && openedBackpacks[player.userID].storage?.inventory?.uid == targetContainer) 
				{
					SendReply(player, $"Запрещено переносить предметы в чужой рюкзак!");
					return false;
				}
			}
			
			return null;
		}
		
		private bool IsBackpackContainer(uint uid, ulong userId) => openedBackpacks.ContainsKey(userId) ? openedBackpacks[userId].storage.inventory.uid == uid : false;
		
		private void OnEntityDeath(BaseCombatEntity ent, HitInfo info) 
		{
			if (!(ent is BasePlayer)) return;
			var player = (BasePlayer) ent;
			if (InDuel(player)) return;
			
			BackpackHide(player.userID);
			if (PermissionService.HasPermission(player.UserIDString, BPIGNORE)) return;
			
			List < SavedItem > savedItems;
			List < Item > items = new List < Item > ();
			
			if (savedBackpacks.TryGetValue(player.userID, out savedItems)) 
			{
				items = RestoreItems(savedItems);
				savedBackpacks.Remove(player.userID);
			}
			
			if (items.Count <= 0) return;
			
			if (DropWithoutBackpack) 
			{
				foreach(var item in items) 
				{
					item.Drop(player.transform.position + Vector3.up, Vector3.up);
				}
				return;
			}
			
			var iContainer = new ItemContainer();
			iContainer.ServerInitialize(null, items.Count);
			iContainer.GiveUID();
			iContainer.entityOwner = player;
			iContainer.SetFlag(ItemContainer.Flag.NoItemInput, true);
			
			for (int i = items.Count - 1; i >= 0; i--) items[i].MoveToContainer(iContainer);
			
			DroppedItemContainer droppedItemContainer = ItemContainer.Drop("assets/prefabs/misc/item drop/item_drop_backpack.prefab", player.transform.position + Vector3.up, Quaternion.identity, iContainer);
			
			if (droppedItemContainer != null) 
			{
				droppedItemContainer.playerName = $"Рюкзак игрока {player.displayName}";
				droppedItemContainer.playerSteamID = player.userID;
				timer.Once(KillTimeout, () => 
				{
					if (droppedItemContainer != null && !droppedItemContainer.IsDestroyed) droppedItemContainer.Kill();
				});
				//Effect.server.Run("assets/bundled/prefabs/fx/dig_effect.prefab", droppedItemContainer.transform.position);
			}
		}
		
		private string BPIGNORE = "backpack.ignore";
		private string BPIgnoreBlackListed = "backpack.blignore";
		private string BPPrivilageMainLoot = "backpack.otherloot";
		private bool DropWithoutBackpack = false;
		private bool EnabledMainBackpackLoot = true;
		private float KillTimeout = 300f;
		private string ImageURL = "https://i.imgur.com/afIPQeW.png";
		private static Dictionary < string, int > permisions = new Dictionary < string, int > ();
		private List < string > BlackListed = new List < string > ();
		private string TextInButton = "<b>ОТКРЫТЬ</b>";
		private bool SizeEnabled = true;
		private bool AutoWipe = false;
		private bool DisabledMoveOtherBackpack = true;
		private bool EnabledUI = true;
		private bool DisabledOpenBPInFly = true;
		private int Type = 1;
		
		private void LoadConfigValues() 
		{
			bool changed = false;
			if (GetConfig("Основные настройки", "При смерти игрока выкидывать вещи без рюкзака", ref DropWithoutBackpack)) {
				changed = true;
			}
			if (GetConfig("Основные настройки", "Время удаления рюкзака после выпадения", ref KillTimeout)) {
				changed = true;
			}
			if (GetConfig("Основные настройки", "Привилегия игнорирования выпадение рюкзака", ref BPIGNORE)) {
				changed = true;
			}
			if (GetConfig("Основные настройки", "Привилегия игнорирования чёрного списка", ref BPIgnoreBlackListed)) {
				changed = true;
			}
			if (GetConfig("Основные настройки", "Запретить открывать рюкзак в полёте", ref DisabledOpenBPInFly)) {				
				changed = true;
			}
			if (GetConfig("Основные настройки", "Ссылка на изображение иконки UI", ref ImageURL)) {				
				changed = true;
			}
			if (GetConfig("Основные настройки", "Текст в кнопке UI (Если не хотите надпись, оставте поле пустым)", ref TextInButton)) {				
				changed = true;
			}
			if (GetConfig("Основные настройки", "Включить отображение размера рюкзака в UI", ref SizeEnabled)) {				
				changed = true;
			}
			if (GetConfig("Основные настройки", "Включить отображение UI рюкзака", ref EnabledUI)) {				
				changed = true;
			}
			if (GetConfig("Основные настройки", "Включить автоматическую очистку рюкзаков при вайпе карты", ref AutoWipe)) {				
				changed = true;
			}
			if (GetConfig("Основные настройки", "Разрешить лутание чужих рюкзаков по привилегии указаной в конфигурации", ref EnabledMainBackpackLoot)) {				
				changed = true;
			}
			if (GetConfig("Основные настройки", "Привилегия на разрешение лутания чужих рюкзаков", ref BPPrivilageMainLoot)) {				
				changed = true;
			}
			if (GetConfig("Основные настройки", "Запретить переносить свои предметы в чужой рюкзак (если включена функция лутания чужих рюкзаков)", ref DisabledMoveOtherBackpack)) {				
				changed = true;
			}
			if (GetConfig("Основные настройки", "Разрешить лутание чужих рюкзаков по привилегии указаной в конфигурации", ref EnabledMainBackpackLoot)) {
				changed = true;
			}
			if (GetConfig("Основные настройки", "Какой вид заполнения использовать в UI", ref Type)) {			
				changed = true;
			}
			
			var _permisions = new Dictionary < string,
				object > () {					
					{ "backpack.size6",  6 }, 
					{ "backpack.size12", 12 }, 
					{ "backpack.size18", 18 }, 
					{ "backpack.size24", 24 }, 
					{ "backpack.size30", 30 }, 
					{ "backpack.size36", 36 }, 
					{ "backpack.size42", 42 }
				};
				
			if (GetConfig("Основные настройки", "Список привилегий и размера рюкзака (Привилегия (формат: backpack.): Размер слотов)", ref _permisions)) {				
				changed = true;
			}
			
			permisions = _permisions.ToDictionary(p => p.Key.ToString(), p => Convert.ToInt32(p.Value));
			var _BlackListed = new List < object > () 
			{
				{ "ammo.rocket.basic" }, { "ammo.rifle.explosive" }
			};
			
			if (GetConfig("Основные настройки", "Список запрещенных вещей какие нельзя носить в рюкзаке", ref _BlackListed)) {				
				changed = true;
			}
			
			BlackListed = _BlackListed.Select(p => p.ToString()).ToList();
			
			if (changed) SaveConfig();
		}
		
		private bool GetConfig < T > (string MainMenu, string Key, ref T var) 
		{
			if (Config[MainMenu, Key] != null) 
			{
				var = (T) Convert.ChangeType(Config[MainMenu, Key], typeof (T));
				return false;
			}
			
			Config[MainMenu, Key] = var;
			return true;
		}
		
		private void OnNewSave() 
		{
			if (AutoWipe) 
			{
				LoadBackpacks();
				savedBackpacks = new Dictionary < ulong, List < SavedItem >> ();
				SaveBackpacks();
				PrintWarning("Произошел вайп, рюкзаки были очищены.");
			}
		}
		
		private void Init() 
		{
			ins = this;
			LoadConfig();
			LoadConfigValues();
			LoadBackpacks();
		}
		
		private bool loaded = false;
		
		private void OnServerInitialized() 
		{			
			InitFileManager();
			
			ServerMgr.Instance.StartCoroutine(m_FileManager.LoadFile("backpackImage", ImageURL));
			PermissionService.RegisterPermissions(this, permisions.Keys.ToList());
			PermissionService.RegisterPermissions(this, new List < string > () 
			{
				BPIGNORE,
				BPIgnoreBlackListed,
				BPPrivilageMainLoot
			});
			
			BasePlayer.activePlayerList.ToList().ForEach(p => OnPlayerConnected(p));
		}
		
		private void OnPlayerConnected(BasePlayer player) 
		{
			if (!EnabledUI) return;
			DrawUI(player);
		}
		
		private void Unload() 
		{
			var keys = openedBackpacks.Keys.ToList();
			for (int i = openedBackpacks.Count - 1; i >= 0; i--) BackpackHide(keys[i]);
			SaveBackpacks();
			foreach(var player in BasePlayer.activePlayerList) DestroyUI(player);
			UnityEngine.Object.Destroy(FileManagerObject);
		}				
		
		private void OnPlayerSleepEnded(BasePlayer player) 
		{			
			if (player == null || !EnabledUI) return;
			DrawUI(player);
		}
		
		private void OnLootEntity(BasePlayer player, BaseEntity entity) 
		{
			var target = entity.GetComponent < BasePlayer > ();
			if (target != null) ShowUIPlayer(player, target);
		}
		
		private void BackpackShow(BasePlayer player, ulong target = 0) 
		{
			if (InDuel(player)) return;
			if (BackpackHide(player.userID)) return;
			
			if (player.inventory.loot?.entitySource != null) player.EndLooting();
			var backpackSize = GetBackpackSize(player.UserIDString);
			if (backpackSize == 0) return;
			
			timer.Once(0.1f, ()=> 
			{				
				if (DisabledOpenBPInFly && !player.IsOnGround()) return;
				List < SavedItem > savedItems;
				List < Item > items = new List < Item > ();
				
				if (target != 0 && savedBackpacks.TryGetValue(target, out savedItems)) items = RestoreItems(savedItems);
				if (target == 0 && savedBackpacks.TryGetValue(player.userID, out savedItems)) items = RestoreItems(savedItems);
				
				BackpackBox box = BackpackBox.Spawn(player, target, backpackSize);
				
				if (!openedBackpacks.ContainsKey(player.userID))				
					openedBackpacks.Add(player.userID, box);
				else
					openedBackpacks[player.userID] = box;
				
				box.storage.OwnerID = target != 0 ? target : player.userID;
				box.storage.playerSteamID = target != 0 ? target : player.userID;
				
				if (box.GetComponent < DroppedItemContainer > () != null) 
				{
					box.GetComponent < DroppedItemContainer > ().OwnerID = target != 0 ? target : player.userID;
					box.GetComponent < DroppedItemContainer > ().playerSteamID = target != 0 ? target : player.userID;
					box.GetComponent < DroppedItemContainer > ().SendNetworkUpdate();
				}
				
				if (items.Count > 0) box.Push(items);
				box.StartLoot();
			});
		}
		
		private void OnPlayerLootEnd(PlayerLoot inventory) 
		{
			var player = inventory.GetComponent < BasePlayer > ();
			if (player != null) CuiHelper.DestroyUi(player, "backpack_playermain");
		}
		
		private void ShowUIPlayer(BasePlayer player, BasePlayer target) 
		{
			CuiHelper.DestroyUi(player, "backpack_playermain");
			if (EnabledMainBackpackLoot && !permission.UserHasPermission(player.UserIDString, BPPrivilageMainLoot)) return;
			CuiElementContainer container = new CuiElementContainer();
			container.Add(new CuiButton 
			{
				RectTransform = {
					AnchorMin = "0.5 0",
					AnchorMax = "0.5 0",
					OffsetMin = "215 18",
					OffsetMax = "430 60"
				}, Button = {
					Color = "1 1 1 0.03",
					Command = savedBackpacks.ContainsKey(target.userID) ? $"backpack72645_mainopen {target.userID}" : "",
					Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
				}, Text = {
					Text = "",
					Align = TextAnchor.MiddleCenter,
					Font = "robotocondensed-regular.ttf",
					FontSize = 24
				},
			}, "Overlay", "backpack_playermain");
			
			container.Add(new CuiElement 
			{
				Parent = "backpack_playermain", Components = {
					new CuiRawImageComponent {
						Png = m_FileManager.GetPng("backpackImage"), Color = "0.91 0.87 0.84 1.00"
					},
					new CuiRectTransformComponent {
						AnchorMin = "0.005 0.1", AnchorMax = "0.18 0.9"
					},
				},
			});
			
			container.Add(new CuiElement 
			{
				Parent = "backpack_playermain", Components = {
					new CuiTextComponent {
						Color = savedBackpacks.ContainsKey(target.userID) ? "0.91 0.87 0.84 1.00" : "1.00 0.37 0.38 1.00", Text = savedBackpacks.ContainsKey(target.userID) ? $"ОТКРЫТЬ РЮКЗАК ИГРОКА" : "РЮКЗАК ИГРОКА ПУСТ", FontSize = 14, Align = TextAnchor.MiddleCenter,
					},
					new CuiRectTransformComponent {
						AnchorMin = $"0.14 0", AnchorMax = $"1 1"
					},
				},
			});
			
			CuiHelper.AddUi(player, container);			
		}
		
		[ConsoleCommand("backpack72645_mainopen")] 
		private void ConsoleOpenMainBackpack(ConsoleSystem.Arg args) 
		{
			var player = args.Player();
			ulong targetID = ulong.Parse(args.Args[0]);
			if (EnabledMainBackpackLoot && !permission.UserHasPermission(player.UserIDString, BPPrivilageMainLoot)) return;
			BackpackShow(player, targetID);
		}
		
		private int GetBackpackSize(string UserID) 
		{
			int size = 0;
			permisions.ToList().ForEach(p => {
				if (PermissionService.HasPermission(UserID, p.Key))
					if (p.Value > size) size = p.Value;
			});
			
			return size;
		}
		
		[HookMethod("BackpackHide")] 
		private bool BackpackHide(ulong userId) 
		{						
			BackpackBox box;
			if (!openedBackpacks.TryGetValue(userId, out box)) return false;			
			openedBackpacks.Remove(userId);
			if (box == null) return false;			
			var items = SaveItems(box.GetItems());			
			var owner = box.GetComponent < DroppedItemContainer > ();
			if (items.Count > 0) savedBackpacks[userId] = items;
			else savedBackpacks.Remove(userId);
			box.Close();
			var otherPlayer = BasePlayer.FindByID(userId);
			if (otherPlayer != null) DrawUI(otherPlayer);
			else DrawUI(BasePlayer.FindByID(userId));
			return true;
		}
		
		private void OnUserPermissionGranted(string id, string permName) 
		{
			if (permisions.ContainsKey(permName)) {
				var player = BasePlayer.Find(id);
				if (player != null) DrawUI(player);
			}
		}
		
		private void OnUserPermissionRevoked(string id, string permName) 
		{
			if (permisions.ContainsKey(permName)) {
				var player = BasePlayer.Find(id);
				if (player != null) DrawUI(player);
			}
		}
		
		private void DrawUI(BasePlayer player) 
		{
			if (!EnabledUI) return;
			if (!m_FileManager.IsFinished) {
				timer.Once(1f, () => DrawUI(player));
				return;
			}
			
			CuiHelper.DestroyUi(player, "backpack.image");
			List < SavedItem > savedItems;
			if (!savedBackpacks.TryGetValue(player.userID, out savedItems)) savedItems = new List < SavedItem > ();
			var bpSize = GetBackpackSize(player.UserIDString);
			if (bpSize == 0) return;
			int backpackCount = savedItems?.Count ?? 0;
			if (backpackCount > bpSize) backpackCount = bpSize;
			
			var container = new CuiElementContainer();
			
			container.Add(new CuiPanel {
				Image = {
					Color = "1 1 1 0.03",
					Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
				}, RectTransform = {
					AnchorMin = "0.296 0.025",
					AnchorMax = "0.295 0.025",
					OffsetMax = "60 60"
				},
			}, "Overlay", "backpack.image");
			
			var AnchorType = (float) backpackCount / bpSize - 0.03f;
			string AcnhorMax = "1 1";
			string alpha = "1";
			
			switch (Type) {
				case 1:
					AnchorType = (float) Math.Min(backpackCount, bpSize) / bpSize - 0.03f;
					AcnhorMax = $"0.05 {AnchorType}";
					break;
				case 2:
					AnchorType = (float) backpackCount / bpSize - 0.03f;
					AcnhorMax = $"1 {AnchorType}";
					alpha = "0.5";
					break;
				case 3:
					AnchorType = (float) backpackCount / bpSize - 0.03f;
					AcnhorMax = $"{AnchorType} 1";
					alpha = "0.5";
					break;
				default:
					AnchorType = (float) Math.Min(backpackCount, bpSize) / bpSize - 0.03f;
					AcnhorMax = $"0.05 {AnchorType}";
					break;
			}
			
			container.Add(new CuiPanel 
			{
				Image = {
					Color = "0 0 0 0.5",
					Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
				}, RectTransform = {
					AnchorMin = "0 0",
					AnchorMax = $"{AcnhorMax}"
				},
			}, "backpack.image");
			
			container.Add(new CuiElement 
			{
				Parent = "backpack.image", Components = {
					new CuiRawImageComponent {
						Png = m_FileManager.GetPng("backpackImage"), Color = "1 1 1 0.5"
					},
					new CuiRectTransformComponent {
						AnchorMin = "0.1 0.25", AnchorMax = "0.9 0.95"
					},
				},
			});
			
			if (!string.IsNullOrEmpty(TextInButton)) container.Add(new CuiElement 
			{
				Parent = "backpack.image", Components = {
					new CuiTextComponent {
						Color = "1 1 1 0.5", Text = TextInButton, FontSize = 11, Align = TextAnchor.MiddleCenter,
					},
					new CuiRectTransformComponent {
						AnchorMin = $"0 0", AnchorMax = $"1 0.3"
					},
				},
			});
			
			if (SizeEnabled) container.Add(new CuiElement 
			{
				Parent = "backpack.image", Components = {
					new CuiTextComponent {
						Color = "1 1 1 0.2", Text = $"{backpackCount}/{bpSize}", FontSize = 11, Align = TextAnchor.MiddleRight,
					},
					new CuiRectTransformComponent {
						AnchorMin = $"0.5 0.79", AnchorMax = $"0.997 1"
					},
				},
			});
			
			container.Add(new CuiElement 
			{
				Parent = "backpack.image", Components = {
					new CuiButtonComponent {
						Color = "0 0 0 0", Command = "backpack.open"
					},
					new CuiRectTransformComponent {
						AnchorMin = "0 0", AnchorMax = "1 1"
					},
				},
			});
			
			CuiHelper.AddUi(player, container);
		}				
		
		private void DestroyUI(BasePlayer player) 
		{
			CuiHelper.DestroyUi(player, "backpack.image");
		}
		
		[ChatCommand("bp")] 
		private void cmdBackpackShow(BasePlayer player, string command, string[] args) 
		{
			if (player == null) return;
			player.EndLooting();
			NextTick(() => BackpackShow(player));
		}
		
		[ChatCommand("backpack")] 
		void cmdBackpackShow2(BasePlayer player, string command, string[] args) => cmdBackpackShow(player, command, args);	
		
		[ConsoleCommand("backpack.open")] 
		private void cmdOnBackPackShowClick(ConsoleSystem.Arg arg) 
		{
			var player = arg.Player();
			if (player == null) return;
			
			if (player.inventory.loot?.entitySource != null) 
			{
				BackpackBox bpBox;
				if (openedBackpacks.TryGetValue(player.userID, out bpBox) && bpBox != null && bpBox.gameObject == player.inventory.loot.entitySource.gameObject) return;
				player.EndLooting();
				NextTick(() => BackpackShow(player));
				return;
			} 
			else BackpackShow(player);
		}
		
		[ConsoleCommand("backpack.toggle")] 
		private void cmdOnBackPackShowClick2(ConsoleSystem.Arg arg) => cmdOnBackPackShowClick(arg);
		
		public class SavedItem 
		{
			public string shortname;
			public int itemid;
			public float condition;
			public float maxcondition;
			public int amount;
			public int ammoamount;
			public string ammotype;
			public int flamefuel;
			public ulong skinid;
			public bool weapon;
			public int blueprint;
			public List < SavedItem > mods;
		}
		
		private List<SavedItem> SaveItems(List<Item> items) 
		{			
			if (items == null) return new List<SavedItem>();
			
			var result = new List<SavedItem>();
			foreach(var item in items)
			{
				if (item == null) continue;
				result.Add(SaveItem(item));
			}						
			
			return result;
		}
		
		private SavedItem SaveItem(Item item) 
		{
			SavedItem iItem = new SavedItem 
			{
				shortname = item.info?.shortname, amount = item.amount, mods = new List < SavedItem > (), skinid = item.skin, blueprint = item.blueprintTarget
			};
			
			if (item.info == null) return iItem;
			iItem.itemid = item.info.itemid;
			iItem.weapon = false;
			
			if (item.hasCondition) 
			{
				iItem.condition = item.condition;
				iItem.maxcondition = item.maxCondition;
			}
			
			FlameThrower flameThrower = item.GetHeldEntity()?.GetComponent < FlameThrower > ();
			if (flameThrower != null) iItem.flamefuel = flameThrower.ammo;
			Chainsaw chainsaw = item.GetHeldEntity()?.GetComponent < Chainsaw > ();
			if (chainsaw != null) iItem.flamefuel = chainsaw.ammo;
			if (item.contents != null) foreach(var mod in item.contents.itemList) if (mod.info.itemid != 0) iItem.mods.Add(SaveItem(mod));
			if (item.info.category.ToString() != "Weapon") return iItem;
			BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
			if (weapon == null) return iItem;
			if (weapon.primaryMagazine == null) return iItem;
			iItem.ammoamount = weapon.primaryMagazine.contents;
			iItem.ammotype = weapon.primaryMagazine.ammoType.shortname;
			iItem.weapon = true;
			
			return iItem;
		}
		
		private List<Item> RestoreItems(List < SavedItem > sItems) 
		{
			return sItems.Where(x=> x != null).Select(sItem => 
			{
				if (sItem.weapon) return BuildWeapon(sItem);
				return BuildItem(sItem);
			}).Where(i => i != null).ToList();
		}
		
		private Item BuildItem(SavedItem sItem) 
		{
			if (sItem.amount < 1) sItem.amount = 1;
			Item item = ItemManager.CreateByItemID(sItem.itemid, sItem.amount, sItem.skinid);
			item.blueprintTarget = sItem.blueprint;
			if (item.hasCondition) 
			{
				item.condition = sItem.condition;
				item.maxCondition = sItem.maxcondition;
			}
			FlameThrower flameThrower = item.GetHeldEntity()?.GetComponent < FlameThrower > ();
			if (flameThrower) flameThrower.ammo = sItem.flamefuel;
			Chainsaw chainsaw = item.GetHeldEntity()?.GetComponent < Chainsaw > ();
			if (chainsaw) chainsaw.ammo = sItem.flamefuel;
			if (sItem.mods != null) foreach(var mod in sItem.mods) item.contents.AddItem(BuildItem(mod).info, mod.amount);
			
			return item;
		}
		
		private Item BuildWeapon(SavedItem sItem) 
		{
			Item item = ItemManager.CreateByItemID(sItem.itemid, 1, sItem.skinid);
			if (item.hasCondition) 
			{
				item.condition = sItem.condition;
				item.maxCondition = sItem.maxcondition;
			}
			var weapon = item.GetHeldEntity() as BaseProjectile;
			if (weapon != null) 
			{
				var def = ItemManager.FindItemDefinition(sItem.ammotype);
				weapon.primaryMagazine.ammoType = def;
				weapon.primaryMagazine.contents = sItem.ammoamount;
			}
			if (sItem.mods != null) foreach(var mod in sItem.mods) item.contents.AddItem(BuildItem(mod).info, 1);
			return item;
		}
		
		[PluginReference] 
		private Plugin Duel;
		
		private bool InDuel(BasePlayer player) => Duel?.Call < bool > ("IsPlayerOnActiveDuel", player) ?? false;
		
		public static class PermissionService 
		{
			public static Permission permission = Interface.GetMod().GetLibrary < Permission > ();
			public static bool HasPermission(string userId, string permissionName) 
			{
				if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(permissionName)) return false;
				if (permission.UserHasPermission(userId, permissionName)) return true;
				return false;
			}
			
			public static void RegisterPermissions(Plugin owner, List < string > permissions) 
			{
				if (owner == null) throw new ArgumentNullException("owner");
				if (permissions == null) throw new ArgumentNullException("commands");
				foreach(var permissionName in permissions.Where(permissionName => !permission.PermissionExists(permissionName))) {
					permission.RegisterPermission(permissionName, owner);
				}
			}
		}
		
		private GameObject FileManagerObject;
		
		private FileManager m_FileManager;
		
		private void InitFileManager() 
		{
			FileManagerObject = new GameObject("MAP_FileManagerObject");
			m_FileManager = FileManagerObject.AddComponent < FileManager > ();
		}
		
		private class FileManager: MonoBehaviour 
		{
			int loaded = 0;
			int needed = 0;
			public bool IsFinished => needed == loaded;
			const ulong MaxActiveLoads = 10;
			Dictionary < string, FileInfo > files = new Dictionary < string, FileInfo > ();
			
			private class FileInfo 
			{
				public string Url;
				public string Png;
			}
			
			public string GetPng(string name) => files[name].Png;
			
			public IEnumerator LoadFile(string name, string url) 
			{
				if (files.ContainsKey(name) && files[name].Url == url && !string.IsNullOrEmpty(files[name].Png)) yield
				break;
				files[name] = new FileInfo() {
					Url = url
				};
				needed++;
				yield
				return StartCoroutine(LoadImageCoroutine(name, url));
			}
			
			IEnumerator LoadImageCoroutine(string name, string url) 
			{
				using(WWW www = new WWW(url)) {
					yield
					return www;
					using(MemoryStream stream = new MemoryStream()) {
						if (string.IsNullOrEmpty(www.error)) {
							var entityId = CommunityEntity.ServerInstance.net.ID;
							var crc32 = FileStorage.server.Store(www.bytes, FileStorage.Type.png, entityId).ToString();
							files[name].Png = crc32;
						}
					}
				}
				loaded++;
			}
		}
	}
}