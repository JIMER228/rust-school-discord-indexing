using System;
using Facepunch;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Rust;
using UnityEngine.AI;
using UnityEngine.SceneManagement; 

namespace Oxide.Plugins
{		
    [Info("TalsV2", "Nimant", "1.0.3")]
    public class TalsV2 : RustPlugin
    {                
		
		#region Variables
						
		[PluginReference]
        private Plugin RecycleManager, Grant, OnScreenLogo;
						
		private const string MainItemName = "fish.minnows";
		private const string ImgHelp = "https://i.imgur.com/1qgn3Ez.png";
				
		private static Dictionary<ulong, Timer> InfoTimers1 = new Dictionary<ulong, Timer>();
		private static Dictionary<ulong, Timer> InfoTimers2 = new Dictionary<ulong, Timer>();		
						
		private static System.Random Rnd = new System.Random();				
		
		#endregion
		
		#region Hooks
		
		private void Init() 
		{			
			LoadVariables();
			LoadData();
		}				
		
		private void OnServerInitialized()
		{			
			InfoTimers1.Clear();
			InfoTimers2.Clear();
			
			DownloadImages();
			
			var lootList = BaseNetworkable.serverEntities.OfType<LootContainer>();			
			foreach (var loot in lootList) FillLoot(loot);
			
			foreach (var tal in configData.Tals)
			{
				var outputItems = new Dictionary<string, string>();
				foreach (var pair in tal.RecycleItems)				
					outputItems.Add($"{pair.Key}|0", pair.Value);
				
				RecycleManager?.Call("API_AddRecycleItemInfo", MainItemName, tal.SkinID, outputItems);
			}
			
			foreach (var player in BasePlayer.activePlayerList)
				DrawLogoButton(player);
			
			timer.Once(5f, TimerReDrawPanel);
		}
		
		private void OnServerSave() 
		{
			Log(null);
			SaveData();
		}
		
		private void OnNewSave()
		{
			DataInfo.TouchedLoot.Clear();
			SaveData();
		}
		
		private void Unload() 
		{
			Log(null);
			SaveData();
			
			foreach (var player in BasePlayer.activePlayerList)
			{
				CuiHelper.DestroyUi(player, MainPanel);
				CuiHelper.DestroyUi(player, LogoPanel);
				CuiHelper.DestroyUi(player, InfoPanel);
				CuiHelper.DestroyUi(player, HelpPanel);
			}
		}				
		
		private void API_OnLogoDrew(BasePlayer player)
		{
			if (player == null) return;
			
			DrawLogoButton(player);
			DrawPanel(player);
		}
		
		private void OnLootSpawn(LootContainer container)
		{
			if (container == null) return;
			FillLoot(container);
		}
		
		private object OnItemSplit(Item item, int split_Amount)
        {
			if (item == null) return null;
			
			foreach (var tal in configData.Tals)
			{
				if (item.skin == tal.SkinID)
				{
					var byItemId = ItemManager.CreateByName(MainItemName, 1, tal.SkinID);   
					byItemId.name = tal.Name;				
					item.amount -= split_Amount;
					byItemId.amount = split_Amount;
					byItemId.name = item.name;				
					item.MarkDirty();
					return byItemId;
				}
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
			if (item.info.shortname == MainItemName && anotherItem.info.shortname == MainItemName)
			{
				foreach (var tal in configData.Tals)
				{									
					if (item.skin == tal.SkinID && anotherItem.skin != tal.SkinID)
						return false;
					
					if (anotherItem.skin == tal.SkinID && item.skin != tal.SkinID)
						return false;
				}
			}
			
			return null;
        }
		
		private bool? OnItemAction(Item item, string action, BasePlayer player)
        {			
            if (item == null || player == null) return null;
			if (!(item.info.shortname == MainItemName) || !(action == "consume") || !configData.Tals.Exists(x=> x.SkinID == item.skin)) return null;
						
			if (player.metabolism.CanConsume())
			{
				var tal = configData.Tals.FirstOrDefault(x=> x.SkinID == item.skin);
				item.UseItem();
				GiveTalPriv(player, tal);
			}
						
			return false;
		}
		
		private void OnUserGroupRemoved(string id, string groupName)
		{
			var player = BasePlayer.Find(id);
			if (player != null)
				DrawPanel(player);
		}
		
		#endregion
		
		#region Helpers
		
		private void GiveTalPriv(BasePlayer player, TalConf tal)
		{
			if (player == null || tal == null) return;
			rust.RunServerCommand($"grant.group {player.userID} {tal.PrivGroup} {tal.HoursAction}h");
			//SendReply(player, $"Вы активировали <color=orange>{tal.Name}</color>.\nДлительность действия талисмана <color=orange>{tal.HoursAction}ч</color>.\nИнформация о талисманах <color=orange>/tals</color>.");			
			DrawInfo(player, tal);			
			Log($"Игрок {player.displayName} ({player.userID}) активировал '{tal.Name}'.");
			timer.Once(0.1f, ()=> DrawPanel(player));
		}
		
		private string GetPermDuration(ulong userID, string group)
		{
			//GetGroups(ulong playerId) return null / Dictionary<string, int> - получить список групп игрока в формате Dictionary<название группы, оставшиеся время действия в секундах> метод возвращает null если у игрока нет групп
			var groups = Grant?.Call("GetGroups", userID) as Dictionary<string, int>;
			if (groups == null) return "";
			
			var time = groups.FirstOrDefault(x=> x.Key == group).Value;
			if (time <= 0) return "";
			
			var res = time/3600f < 1f ? "<1" : (((int)Math.Round(time/3600f)).ToString());
			
			return $"{res}ч";
		}
		
		#endregion
		
		#region GUI
		
		private void TimerReDrawPanel()
		{
			foreach (var player in BasePlayer.activePlayerList)
				DrawPanel(player);
				
			timer.Once(60f, TimerReDrawPanel);
		}
		
		private void DrawLogoButton(BasePlayer player)
		{
			if (player == null) return;
			var container = new CuiElementContainer();
			
			var logoPos = OnScreenLogo?.Call("API_GetLogoSize", player.userID) as List<string>;
			if (logoPos == null) return;			
			
			UI_MainPanel(ref container, "0 0 0 0", logoPos[0], logoPos[1], true, false, false, LogoPanel);
			UI_Button(ref container, "0 0 0 0", "", 15, "0 0", "1 1", "tv2_89d899er.toggle", "robotocondensed-regular.ttf", TextAnchor.MiddleCenter, LogoPanel);
			
			CuiHelper.DestroyUi(player, LogoPanel);
            CuiHelper.AddUi(player, container);
		}
		
		private void DrawInfo(BasePlayer player, TalConf tal)
		{
			if (player == null || !AreImagesLoaded()) return;
			
			var container = new CuiElementContainer(); 
			
			UI_MainPanel(ref container, "0 0 0 0", "1 1", "1 1", false, false, false, InfoPanel, $"{-430} {-150}", $"{-430 + 400} {-150 + 120}");
			UI_Image(ref container, Images[tal.ImgInfo], "0 0", "1 1", 0.5f, 0.5f, InfoPanel, "InfoPanel_Image");
			UI_Button(ref container, "0 0 0 0", "", 15, "0.87 0.65", "0.96 0.88", "tv2_89d899er.close_info", "robotocondensed-regular.ttf", TextAnchor.MiddleCenter, InfoPanel);
			
			if (!InfoTimers1.ContainsKey(player.userID))
				InfoTimers1.Add(player.userID, null);
			
			if (InfoTimers1[player.userID] != null)			
				InfoTimers1[player.userID].Destroy();
			
			if (!InfoTimers2.ContainsKey(player.userID))
				InfoTimers2.Add(player.userID, null);
			
			if (InfoTimers2[player.userID] != null)			
				InfoTimers2[player.userID].Destroy();
			
			InfoTimers1[player.userID] = timer.Once(9f, ()=>
			{
				if (player != null)
				{
					CuiHelper.DestroyUi(player, "InfoPanel_Image");
					InfoTimers1[player.userID] = null;
					
					InfoTimers2[player.userID] = timer.Once(0.5f, ()=>
					{
						if (player != null)
						{
							CuiHelper.DestroyUi(player, InfoPanel);
							InfoTimers2[player.userID] = null;
						}
					});
				}
			});
			
			CuiHelper.DestroyUi(player, InfoPanel);
            CuiHelper.AddUi(player, container);
		}
		
		private void DrawPanel(BasePlayer player)
		{			
			if (player == null || !AreImagesLoaded()) return;
			var container = new CuiElementContainer();			
			var shrinked = DataInfo.Shrinked.Contains(player.userID); 
				
			UI_MainPanel(ref container, "0 0 0 0", "0 1", "0 1", true, false, false, MainPanel, $"{143} {0-51}", $"{143 + 220} {-51 + 45}");
			int shiftX = 0;				
				
			if (!shrinked)
			{												
				foreach (var tal in configData.Tals.OrderBy(x=> x.PosInGUI))
				{
					var hasGroup = permission.UserHasGroup(player.UserIDString, tal.PrivGroup);
					var png = Images[tal.ImgNonActive];
					if (hasGroup)
					{
						UI_Image(ref container, png, $"{0f + shiftX*0.2f} 0", $"{0.2f + shiftX*0.2f} 1", 0f, 0f);
						UI_FLabel(ref container, GetPermDuration(player.userID, tal.PrivGroup), "0 0 0 1", 13, $"{-0.03f + shiftX*0.2f} -0.45", $"{0.23f - 0.03f + shiftX*0.2f} 0.9", 0, 0, TextAnchor.MiddleRight);
						UI_Button(ref container, "0 0 0 0", "", 15, $"{0f + shiftX*0.2f} 0", $"{0.2f + shiftX*0.2f} 1", "tv2_89d899er.open_help");
						shiftX++;
					}					
				}								
			}
			else
			{
				var flag = false;
				foreach (var tal in configData.Tals.OrderBy(x=> x.PosInGUI))
				{					
					if (permission.UserHasGroup(player.UserIDString, tal.PrivGroup))
					{
						flag = true;
						break;
					}					
				}
				
				if (!flag)				
				{
					DataInfo.Shrinked.Remove(player.userID);
					SaveData();
				}
			}
			
			CuiHelper.DestroyUi(player, MainPanel);
            CuiHelper.AddUi(player, container);
		}
		
		private void DrawHelpPanel(BasePlayer player)
		{			
			if (player == null || !AreImagesLoaded()) return;
			var container = new CuiElementContainer();
			
			UI_MainPanel(ref container, "0 0 0 0", "0.5 0.5", "0.5 0.5", true, true, false, HelpPanel, $"{-410} {-330}", $"{410} {330}");					
			
			UI_Image(ref container, Images[ImgHelp], "0 0", "1 1", 0f, 0f, HelpPanel);
			
			UI_Button(ref container, "0 0 0 0", "", 15, "0 0", "1 1", "tv2_89d899er.close_help", "robotocondensed-regular.ttf", TextAnchor.MiddleCenter, HelpPanel);
			
			CuiHelper.DestroyUi(player, HelpPanel);
            CuiHelper.AddUi(player, container);
		}
		
		#endregion
		
		#region Commands
				
		[ConsoleCommand("tv2_89d899er.toggle")]
        private void cmdGUIToggle(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null) return;
									
			if (DataInfo.Shrinked.Contains(player.userID))
				DataInfo.Shrinked.Remove(player.userID);
			else
				DataInfo.Shrinked.Add(player.userID);
			
			SaveData();
									
			DrawPanel(player);
		}
		
		[ConsoleCommand("tv2_89d899er.close_info")]
        private void cmdGUICloseInfo(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null) return;
									
			CuiHelper.DestroyUi(player, InfoPanel);
		}
		
		[ConsoleCommand("tv2_89d899er.close_help")]
        private void cmdGUICloseHelp(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null) return;
									
			CuiHelper.DestroyUi(player, HelpPanel);
		}
		
		[ConsoleCommand("tv2_89d899er.open_help")]
        private void cmdGUIOpenHelp(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null) return;
									
			DrawHelpPanel(player);
		}
		
		#endregion
		
		#region GUI Helpers
		
		private const string MainPanel = "TV2_MainPanel";
		private const string LogoPanel = "TV2_LogoPanel";
		private const string InfoPanel = "TV2_InfoPanel";
		private const string HelpPanel = "TV2_HelpPanel";
		
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
				Image = { Color = color, FadeIn = fadeIn },
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
				Button = { Color = color, Command = command, FadeIn = fadeIn },
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
		
		#region Loot				
		
		private void SuppressRefresh(LootContainer container) 
		{			
            container.minSecondsBetweenRefresh = -1;
            container.maxSecondsBetweenRefresh = 0;
            container.GetComponent<FacepunchBehaviour>().CancelInvoke(new Action(container.SpawnLoot));
        } 
		
		private void FillLoot(LootContainer cont)
		{
			if (cont == null) return;
			
			var spawnTals = new Dictionary<int, float>();
			
			foreach (var tal in configData.Tals)			
				foreach (var pair in tal.PercentPerCont) 				
					if (pair.Key == cont.PrefabName)
						spawnTals.Add(tal.PosInGUI, pair.Value);
				
			if (spawnTals.Count == 0) return;
			
			if (DataInfo.TouchedLoot.Contains(cont.net.ID)) return;
			
			if (!DataInfo.TouchedLoot.Contains(cont.net.ID))
				DataInfo.TouchedLoot.Add(cont.net.ID);
			
			SuppressRefresh(cont);
			
			int talSpawnId = 0;
			
			var talXX = spawnTals.OrderBy(x => Rnd.Next()).FirstOrDefault();						
			
			var needSpawn = Rnd.Next(0, 1001)/10f <= talXX.Value;
			if (needSpawn)			
				talSpawnId = talXX.Key;
			
			if (talSpawnId == 0) return;						
			
			var tal_ = configData.Tals.FirstOrDefault(x=> x.PosInGUI == talSpawnId);			
			if (tal_ == null) return;
			
			var item = ItemManager.CreateByName(MainItemName, 1, tal_.SkinID);
			item.name = tal_.Name;
			
			if (cont.inventory.capacity < cont.inventory.itemList.Count()+1)
				cont.inventory.capacity++;
			
			item.MoveToContainer(cont.inventory, -1, true);
		}				
		
		#endregion
		
		#region Images
		
		private const int MaxImages = 11; // изменять тут
		
		private static Dictionary<string, string> Images = new Dictionary<string, string>();
		
		private static bool AreImagesLoaded() => Images.Count >= MaxImages;		
		
		private void DownloadImages() 
		{			
			ServerMgr.Instance.StartCoroutine(DownloadImage(ImgHelp));
			
			foreach (var tals in configData.Tals)
			{
				ServerMgr.Instance.StartCoroutine(DownloadImage(tals.ImgInfo));
				ServerMgr.Instance.StartCoroutine(DownloadImage(tals.ImgNonActive));
			}
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
		
		#region Log
		
		private static List<string> QueueBuffer = new List<string>();
		
		private static string GetCurDate() => "["+DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")+"] ";	
		
		private bool TryLog(string message)
		{
			if (string.IsNullOrEmpty(message))
				return true;
			
			try { LogToFile("info", $"{message}", this); }
			catch { return false; }
			
			return true;
		}
		
		private void Log(string message)
		{						
			QueueBuffer.Add(message);	
			while(QueueBuffer.Count > 0)
			{					
				if (TryLog(QueueBuffer[0]))
					QueueBuffer.RemoveAt(0);
				else
					break;
			}				
		}
		
		#endregion
		
		#region Config        				
		
        private static ConfigData configData;
		
        private class ConfigData
        {            
			[JsonProperty(PropertyName = "Талисманы")]
			public List<TalConf> Tals;
        }
		
		private class TalConf
		{
			[JsonProperty(PropertyName = "Позиция на экране в меню (начиная с 1)")]
			public int PosInGUI;
			[JsonProperty(PropertyName = "Название талисмана")]
			public string Name;
			[JsonProperty(PropertyName = "Скин талисмана")]
			public ulong SkinID;
			[JsonProperty(PropertyName = "Группа с привилегией талисмана")]
			public string PrivGroup;
			[JsonProperty(PropertyName = "Время действия талисмана (команды) в часах")]
			public int HoursAction;
			[JsonProperty(PropertyName = "Контейнер и процент вероятности спавна там талисмана")]
			public Dictionary<string, float> PercentPerCont;
			[JsonProperty(PropertyName = "Выпадающие предметы при переработке талисмана")]
			public Dictionary<string, string> RecycleItems;			
			[JsonProperty(PropertyName = "Картинка с активным талисманом")]
			public string ImgInfo;
			[JsonProperty(PropertyName = "Картинка с неактивным талисманом")]
			public string ImgNonActive;
		}
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();
		
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                Tals = new List<TalConf>()
				{
					new TalConf()
					{						
						PosInGUI = 1,
						Name = "Талисман носорога",
						SkinID = 2082320181,
						PrivGroup = "talsv2_metabolism",
						HoursAction = 6,
						PercentPerCont = new Dictionary<string, float>()
						{
							{ "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab", 15f },
							{ "assets/bundled/prefabs/radtown/crate_elite.prefab", 10f },
							{ "assets/bundled/prefabs/radtown/crate_normal.prefab", 5f },
							{ "assets/prefabs/npc/patrol helicopter/heli_crate.prefab", 20f },
							{ "assets/prefabs/misc/supply drop/supply_drop.prefab", 10f }
						},
						RecycleItems = new Dictionary<string, string>()
						{
							{ "gunpowder", "200|500|10|A|1" }
						},
						ImgInfo = "https://i.imgur.com/D1HlHqj.png",
						ImgNonActive = "https://i.imgur.com/0c9HttF.png"
					},
					new TalConf()
					{						
						PosInGUI = 2,
						Name = "Талисман быка",
						SkinID = 2082322192,
						PrivGroup = "talsv2_backpack", 
						HoursAction = 6,
						PercentPerCont = new Dictionary<string, float>()
						{
							{ "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab", 15f },
							{ "assets/bundled/prefabs/radtown/crate_elite.prefab", 10f },
							{ "assets/bundled/prefabs/radtown/crate_normal.prefab", 5f },
							{ "assets/prefabs/npc/patrol helicopter/heli_crate.prefab", 20f },
							{ "assets/prefabs/misc/supply drop/supply_drop.prefab", 10f }
						},
						RecycleItems = new Dictionary<string, string>()
						{
							{ "gunpowder", "200|500|10|A|1" }
						},
						ImgInfo = "https://i.imgur.com/jp2pcQc.png",
						ImgNonActive = "https://i.imgur.com/4X7mqwx.png"
					},
					new TalConf()
					{						
						PosInGUI = 3,
						Name = "Талисман хамелеона",
						SkinID = 2082323231,
						PrivGroup = "talsv2_skins",
						HoursAction = 24,
						PercentPerCont = new Dictionary<string, float>()
						{
							{ "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab", 15f },
							{ "assets/bundled/prefabs/radtown/crate_elite.prefab", 10f },
							{ "assets/bundled/prefabs/radtown/crate_normal.prefab", 5f },
							{ "assets/prefabs/npc/patrol helicopter/heli_crate.prefab", 20f },
							{ "assets/prefabs/misc/supply drop/supply_drop.prefab", 10f }
						},
						RecycleItems = new Dictionary<string, string>()
						{
							{ "gunpowder", "200|500|10|A|1" }
						},
						ImgInfo = "https://i.imgur.com/fyEf09x.png",
						ImgNonActive = "https://i.imgur.com/R0BzaNC.png"
					},
					new TalConf()
					{						
						PosInGUI = 4,
						Name = "Талисман кролика",
						SkinID = 2082324429,
						PrivGroup = "talsv2_crafting",
						HoursAction = 3,
						PercentPerCont = new Dictionary<string, float>()
						{
							{ "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab", 15f },
							{ "assets/bundled/prefabs/radtown/crate_elite.prefab", 10f },
							{ "assets/bundled/prefabs/radtown/crate_normal.prefab", 5f },
							{ "assets/prefabs/npc/patrol helicopter/heli_crate.prefab", 20f },
							{ "assets/prefabs/misc/supply drop/supply_drop.prefab", 10f }
						},
						RecycleItems = new Dictionary<string, string>()
						{
							{ "gunpowder", "200|500|10|A|1" }
						},
						ImgInfo = "https://i.imgur.com/QqrXmmB.png",
						ImgNonActive = "https://i.imgur.com/QVSCx7O.png"
					},
					new TalConf()
					{						
						PosInGUI = 5,
						Name = "Талисман петуха",
						SkinID = 2082324977,
						PrivGroup = "talsv2_raidalert",
						HoursAction = 24,
						PercentPerCont = new Dictionary<string, float>()
						{
							{ "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab", 15f },
							{ "assets/bundled/prefabs/radtown/crate_elite.prefab", 10f },
							{ "assets/bundled/prefabs/radtown/crate_normal.prefab", 5f },
							{ "assets/prefabs/npc/patrol helicopter/heli_crate.prefab", 20f },
							{ "assets/prefabs/misc/supply drop/supply_drop.prefab", 10f }
						},
						RecycleItems = new Dictionary<string, string>()
						{							
							{ "gunpowder", "200|500|10|A|1" }
						},
						ImgInfo = "https://i.imgur.com/FtmjP2E.png",
						ImgNonActive = "https://i.imgur.com/nqdP6VL.png"
					}
				}				
            };
            SaveConfig(configData);
			timer.Once(0.1f, ()=> SaveConfig(configData));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
		
		#region Data
		
		private class DataClass
		{
			public HashSet<uint> TouchedLoot = new HashSet<uint>();
			public List<ulong> Shrinked = new List<ulong>();
		}
		
		private static DataClass DataInfo = new DataClass();
		
		private void LoadData() 
		{
			DataInfo = Interface.GetMod().DataFileSystem.ReadObject<DataClass>("TalsV2Data");
			
			if (DataInfo == null)
				DataInfo = new DataClass();
			
			if (DataInfo.TouchedLoot == null)
				DataInfo.TouchedLoot = new HashSet<uint>();
			
			if (DataInfo.Shrinked == null)
				DataInfo.Shrinked = new List<ulong>(); 
		}
		
		private static void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("TalsV2Data", DataInfo);		
		
		#endregion
		
    }
}