using System.Collections.Generic;
using System.Collections;
using Oxide.Core.Configuration;
using System.Globalization;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;
using Oxide.Core;
using System;
using Oxide.Game.Rust.Cui;
using System.Text.RegularExpressions;
using Oxide.Core.Libraries.Covalence;
using Color = UnityEngine.Color;
using VLB;
using Rust;
using System.IO;
using ProtoBuf;

namespace Oxide.Plugins
{
    [Info("Tech Tree Rmover", "Razor", "3.0.0")]
    [Description("Adds a new Super Workbench")]
    public class TechTreeRemover : RustPlugin
    {
		[PluginReference]
		Plugin NoWorkbench, BlueprintShare;

		private static TechTreeRemover Instance;

		Dictionary<ulong, int> pcdData;
		private DynamicConfigFile PCDDATA;
		private ulong SecretKey;

		public static List<int> steamBPBlacklist = new List<int>() { -1916473915, -1335497659, 359723196, 2009734114, -1423304443, 1058261682, 1521286012, -854270928, 1643667218, 42535890, -979302481, 674734128, -1379835144, 1242482355, -1824943010, -845557339, 23352662, 866332017, 2070189026, -1832422579, -1370759135, 177226991, 1542290441, -216116642, 553887414, 1629293099, 121049755, 1205607945, -1647846966, 826309791, -996185386, 98508942, -702051347, -1022661119, -23994173, 3380160, 968019378, -22883916, -1000573653, -1043618880, -1569700847, -2047081330, 1081315464, 20489901, 271048478, 2126889441, -1785231475, 1803831286, 1242522330, -1973785141, -695124222, 282103175, -489848205, -173268132, -173268126, -173268131, -173268129, -2058362263, 1358643074, 1885488976, 2104517339, 1744298439, -515830359, 1324203999, -151387974, -656349006, -1306288356, -961457160, -7270019, -1553999294, -1486461488, -454370658, -25740268, -1078639462, -1073015016, -769647921, -156748077, -924959988, 971362526, -280223496, -99886070, -1538109120, 261913429, 2100007442, 1263920163, 1397052267, -1100422738, 1305578813, 1268178466, 1623701499, -1160621614, -1335497659 };

		#region Init/Unload
		private void Init()
		{
			Instance = this;
			SecretKey = (ulong)UnityEngine.Random.Range(41234564, 9999999999999999);
			PCDDATA = Interface.Oxide.DataFileSystem.GetFile($"{Name}/Workbench_Saves");
			LoadData();

			if (!ConVar.Server.useLegacyWorkbenchInteraction)
				ConVar.Server.useLegacyWorkbenchInteraction = true;
		}

		private void OnServerInitialized()
        {
			if (!ConVar.Server.useLegacyWorkbenchInteraction)
				ConVar.Server.useLegacyWorkbenchInteraction = true;

			timer.Once(2f, () =>
			{
				if (NoWorkbench != null)
				{
					covalence.Server.Command("o.unload NoWorkbench");
					timer.Once(1, () => GenerateBP());
					timer.Once(5, () => covalence.Server.Command("o.reload NoWorkbench"));
				}
				else GenerateBP();
			});

			PrintWarning("This Plugin Sets The Convar useLegacyWorkbenchInteraction to true and will need to manly be reset back to useLegacyWorkbenchInteraction false if not longer is use.");
		}

		void Unload()
		{
			foreach (BasePlayer player in BasePlayer.activePlayerList)
				CuiHelper.DestroyUi(player, MainPanel);

			foreach (var helper in WorkBenchHelper.allHelpers)
				UnityEngine.Object.Destroy(helper);

			SaveData();
			Instance = null;
		}
		#endregion

		#region Config
		private ConfigData configData;
		class ConfigData
		{
			[JsonProperty(PropertyName = "Workbench Settings")]
			public Settings settings { get; set; }

			[JsonProperty(PropertyName = "Workbench Level 1 Settings")]
			public workbanchOptions options1 { get; set; }

			[JsonProperty(PropertyName = "Workbench Level 2 Settings")]
			public workbanchOptions options2 { get; set; }

			[JsonProperty(PropertyName = "Workbench Level 3 Settings")]
			public workbanchOptions options3 { get; set; }

			public class Settings
			{
				[JsonProperty(PropertyName = "BlackListed Item ID")]
				public List<int> blackList;
				[JsonProperty(PropertyName = "BlackListed Shortnames")]
				public List<string> blackShortnames;
			}			

			public Oxide.Core.VersionNumber Version { get; set; }
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			configData = Config.ReadObject<ConfigData>();

			if (configData.Version < Version)
				UpdateConfigValues();

			Config.WriteObject(configData, true);
		}

		protected override void LoadDefaultConfig() => configData = GetBaseConfig();

		private ConfigData GetBaseConfig()
		{
			return new ConfigData
			{
				settings = new ConfigData.Settings
				{
					blackList = new List<int>(),
					blackShortnames = new List<string>()
				},

				options1 = new workbanchOptions() { name = null, itemid = -932201673, skinid = 0, ResearchAmount = 250 },
				options2 = new workbanchOptions() { name = null, itemid = -932201673, skinid = 0, ResearchAmount = 500 },
				options3 = new workbanchOptions() { name = null, itemid = -932201673, skinid = 0, ResearchAmount = 1000 },

				Version = Version
			};
		}

		protected override void SaveConfig() => Config.WriteObject(configData, true);

		private void UpdateConfigValues()
		{
			PrintWarning("Config update detected! Updating config values...");

			ConfigData baseConfig = GetBaseConfig();

			if (configData.Version < new VersionNumber(1, 0, 0))
				configData = baseConfig;

			configData.Version = Version;
			PrintWarning("Config update completed!");
		}

		public class workbanchOptions
		{
			[JsonProperty(PropertyName = "Level 3 Research Cost")]
			public int ResearchAmount;

			[JsonProperty(PropertyName = "Level 3 Research Item")]
			public int itemid;

			[JsonProperty(PropertyName = "Level 3 Research Item Skin")]
			public ulong skinid;

			[JsonProperty(PropertyName = "Level 3 Research Item Custom Name = Default rust items must = null")]
			public string name;
		}
		#endregion Config

		#region Data
		public void LoadData()
		{
			try
			{
				pcdData = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, int>>($"{Name}/Workbench_Saves") ?? new Dictionary<ulong, int>();
			}
			catch
			{
				PrintWarning("Couldn't load Workbench_Saves, creating new Workbench_Saves file");
				pcdData = new Dictionary<ulong, int>();
			}
		}

		void SaveData()
		{
			PCDDATA.WriteObject(pcdData);
		}
		#endregion Data

		#region Methods Getting BP
		private void GenerateBP()
		{
			WorkBenchHelper.allBlueprints = new List<int>();
			foreach (var bp in ItemManager.bpList)
			{
				if (bp.userCraftable && !bp.defaultBlueprint && !bp.NeedsSteamItem)
				{
					var itemID = bp.targetItem.itemid;
					var shortname = bp.targetItem.shortname;
					if (steamBPBlacklist.Contains(itemID) || configData.settings.blackList.Contains(itemID) || configData.settings.blackShortnames.Contains(shortname))
					{
						continue;
					}

					if (!WorkBenchHelper.allBlueprints.Contains(itemID))
						WorkBenchHelper.allBlueprints.Add(itemID);

					switch (bp.workbenchLevelRequired)
					{
						case 1:
							WorkBenchHelper.Level1.Add(itemID);
							break;

						case 2:
							WorkBenchHelper.Level2.Add(itemID);
							break;

						case 3:
							WorkBenchHelper.Level3.Add(itemID);
							break;
					}
				}
			}
		}
		#endregion

		#region Hooks
		private void OnLootEntity(BasePlayer player, Workbench workbench)
		{
			workbench.GetOrAddComponent<WorkBenchHelper>()?.OpenUI(player);
		}

		private void OnLootEntityEnd(BasePlayer player, Workbench workbench)
		{
			workbench.GetOrAddComponent<WorkBenchHelper>()?.CloseUi(player);
		}

		private object CanUnlockTechTreeNode(BasePlayer player, TechTreeData.NodeInstance node, TechTreeData techTree)
		{
			PrintWarning($"{player.displayName} tryed to bypass RemoveTechTree.");
			return false;
		}
		#endregion

		#region WorkBenchHelper
		public class WorkBenchHelper : FacepunchBehaviour
		{
			public static List<WorkBenchHelper> allHelpers = new List<WorkBenchHelper>();

			public Workbench workbench { get; private set; }
			public static List<int> allBlueprints = new List<int>();
			public static List<int> Level1 = new List<int>();
			public static List<int> Level2 = new List<int>();
			public static List<int> Level3 = new List<int>();
			private List<int> myBP = new List<int>();
			public workbanchOptions options { get; set; }
			public bool working { get; set; }

			private void Awake()
			{
				allHelpers.Add(this);
				workbench = this.GetComponent<Workbench>();
				switch (workbench.Workbenchlevel)
				{
					case 1:
						myBP = Level1;
						break;

					case 2:
						myBP = Level2;
						break;

					case 3:
						myBP = Level3;
						break;
				}

				 options = Instance.GetWorkbenchOptions(workbench);
			}

			public void OpenUI(BasePlayer player) => Instance.BuildUI(player, workbench);

			public void CloseUi(BasePlayer player) => CuiHelper.DestroyUi(player, MainPanel);

			public bool UnlockBlueprint(BasePlayer player, int blueprint)
			{
				var playerInfo = player.PersistantPlayerInfo;

				if (playerInfo.unlockedItems.Contains(blueprint) == false)
				{
					Instance.pcdData[workbench.net.ID.Value] = 0;
					playerInfo.unlockedItems.Add(blueprint);
					player.PersistantPlayerInfo = playerInfo;
					player.SendNetworkUpdateImmediate();
					player.ClientRPCPlayer(null, player, "UnlockedBlueprint", 0);
					Effect.server.Run("assets/prefabs/misc/blueprintbase/effects/blueprint_read.prefab", player.transform.position);

					Instance.BuildItemSucess(player, workbench, RemainingBlueprints(player), this);
					if (Instance.BlueprintShare != null)
					{
						ItemDefinition bp = ItemManager.FindItemDefinition(blueprint);
						if (bp != null)
							Instance.BlueprintShare.CallHook("TryShareBlueprint", player, bp);
					}
					return true;
				}
				return false;
			}

			public bool TakeBlueprint(BasePlayer player, int itemid)
			{
				if (itemid == 0)
					return false;

				var blueprintBaseDef = ItemManager.FindItemDefinition("blueprintbase");
				Item itemToCreate = ItemManager.Create(blueprintBaseDef, 1);
				itemToCreate.blueprintTarget = itemid;
				if (itemToCreate != null)
				{
					Instance.pcdData[workbench.net.ID.Value] = 0;
					Instance.NextTick(() => { player.GiveItem(itemToCreate, BaseEntity.GiveItemReason.Generic); });
					Instance.BuildItemSucess(player, workbench, RemainingBlueprints(player), this);
					return true;
				}
				return false;
			}

			public int RemainingBlueprints(BasePlayer player)
            {
				int BPToLearn = 0;
				List<int> playerBP = Facepunch.Pool.Get<List<int>>();
				List<int> notHaveBP = Facepunch.Pool.Get<List<int>>();

				PersistantPlayer persistantPlayerInfo = player.PersistantPlayerInfo;
				playerBP.AddRange(persistantPlayerInfo.unlockedItems);

				notHaveBP.AddRange(myBP.FindAll(x => !playerBP.Contains(x)));

				int total = notHaveBP.Count();
				Facepunch.Pool.FreeUnmanaged<int>(ref playerBP);
				Facepunch.Pool.FreeUnmanaged<int>(ref notHaveBP);

				return total;
			}

			public void BeginExperiment(BasePlayer player)
			{
				if (working) return;

				CuiHelper.DestroyUi(player, ButtonsToChange);

				int BPToLearn = 0;
				List<int> playerBP = Facepunch.Pool.Get<List<int>>();
				List<int> notHaveBP = Facepunch.Pool.Get<List<int>>();

				PersistantPlayer persistantPlayerInfo = player.PersistantPlayerInfo;
				playerBP.AddRange(persistantPlayerInfo.unlockedItems);

				notHaveBP.AddRange(myBP.FindAll(x => !playerBP.Contains(x)));

				BPToLearn = notHaveBP.GetRandom();

				if (BPToLearn == 0 || GetAmount(player, options.itemid, options.skinid, options.name) < options.ResearchAmount)
				{
					OpenUI(player);
					return;
				}

				ServerMgr.Instance.StartCoroutine(StartExperiment(player, BPToLearn, 6));

				Facepunch.Pool.FreeUnmanaged<int>(ref playerBP);
				Facepunch.Pool.FreeUnmanaged<int>(ref notHaveBP);
			}

			private IEnumerator StartExperiment(BasePlayer player, int bp, int time)
			{
				Take(player, options.itemid, options.ResearchAmount, options.skinid, options.name);
				Effect.server.Run("assets/prefabs/deployable/tier 1 workbench/effects/experiment-start.prefab", workbench.transform.position);
				working = true;
				while (time > 1)
				{
					time--;
					if (Instance != null && player != null && player.inventory.loot.entitySource != null && player.inventory.loot.entitySource == workbench)
					{
						var panelGotItem = CreatePanel(ExparementItemGotPanel, ExparementBPPanel, "0.1 0.1", "0.9 0.9", "0 0", "0 0");
						createLable(panelGotItem, ExparementItemGotPanel, "", Instance.lang.GetMessage($"{time}", Instance, player.UserIDString), 40, "1 1 1 0.7", TextAnchor.MiddleCenter, "0 0", "1 1");

						CuiHelper.AddUi(player, panelGotItem);

					}
					yield return CoroutineEx.waitForSeconds(1f);
				}

				working = false;

				if (Instance != null)
				{
					Instance.pcdData[workbench.net.ID.Value] = bp;
					Instance.BuildItemSucess(player, workbench, RemainingBlueprints(player), this);
				}
				Effect.server.Run("assets/prefabs/deployable/research table/effects/research-success.prefab", workbench.transform.position);
			}
		}
        #endregion

        #region BuildUI
        public const string MainPanel = "RemoveTechTree.MainPanel";
		public const string SubPanel = "RemoveTechTree.SubPanel";
		public const string HeaderPanel = "RemoveTechTree.HeaderPanel";
		public const string ExparementCostPanelImage = "RemoveTechTree.ExparementCostPanelImage";
		public const string ExparementCostPanel = "RemoveTechTree.ExparementCostPanel";
		public const string ExparementScrapPanel = "RemoveTechTree.ExparementCostImagePanel";
		public const string ExparementScrapMessagePanel = "RemoveTechTree.ExparementScrapMessagePanel";
		public const string ExparementBPPanel = "RemoveTechTree.ExparementBPPanel";
		public const string ExparementBPPanelInfo = "RemoveTechTree.ExparementBPPanelInfo";
		public const string ExparementFooterPanel = "RemoveTechTree.ExparementFooterPanel";
		public const string ScrapMessagePanel = "RemoveTechTree.ScrapMessagePanel";
		public const string TotalScrapHavePanel = "RemoveTechTree.TotalScrapHavePanel";
		public const string ExparementScrapItemPanel = "RemoveTechTree.ExparementScrapItemPanel";
		public const string ExparementItemGotPanel = "RemoveTechTree.ExparementItemGotPanel";
		public const string ExparementLableToChange = "RemoveTechTree.ExparementLableToChange";
		public const string ButtonsToChange = "RemoveTechTree.ButtonsToChange";
		public const string command = "techtreeremovercommand";

		public const string TimerPanel = "RemoveTechTree.TimerPanel";
		public const string TimerPanelUpdate = "RemoveTechTree.TimerPanelUpdate";


		private void BuildUI(BasePlayer player, Workbench workbench = null)
		{//0.368 0.368 0.368 1
			if (workbench == null)
				return;

			WorkBenchHelper helper = workbench.GetOrAddComponent<WorkBenchHelper>();
			workbanchOptions options = helper.options;
			if (options == null) return;
			int totalHave = GetAmount(player, options.itemid, options.skinid, options.name);
			string totalIHave = $"{totalHave}";
			if (totalHave >= 1000) totalIHave = totalHave.ToString("##,#");
			string itemName = options.name;
			int totalNeededBP = helper.RemainingBlueprints(player);


			if (string.IsNullOrEmpty(itemName))
			{
				ItemDefinition itemDefinition = ItemManager.FindItemDefinition(options.itemid);
				if (itemDefinition != null)
				{
					itemName = itemDefinition.displayName.translated;
					itemName = UppercaseFirst(itemName);
				}
				else
					itemName = "UnKnown";
			}

			var panelMain = CreatePanel(MainPanel, "Inventory", "0.5 0", "0.5 0", "190 100.0", "575 550.0");
			AddPanel(panelMain, MainPanel, SubPanel, "0 0 0 1", "0 0", "1 1", "0 0", "0 0");
			AddPanel(panelMain, SubPanel, HeaderPanel, "0.368 0.368 0.368 0.4", "0.01 0.94", "0.9932 0.993", "0 0", "0 0");
			createLable(panelMain, HeaderPanel, "", String.Format(lang.GetMessage("workbencHead", this, player.UserIDString), workbench.Workbenchlevel), 15, "1 1 1 0.7", TextAnchor.MiddleLeft, "0.03 0", "1 1");

			if (totalNeededBP > 0)
			{
				AddPanel(panelMain, SubPanel, ScrapMessagePanel, "0.368 0.368 0.368 0.4", "0.01 0.73", "0.6 0.93", "0 0", "0 0");
				createLable(panelMain, ScrapMessagePanel, "", String.Format(lang.GetMessage("itemMessage", this, player.UserIDString), itemName), 19, "1 1 1 0.7", TextAnchor.UpperLeft, "0.05 0", "1 0.8");

				AddPanel(panelMain, SubPanel, ExparementCostPanel, "0.368 0.368 0.368 0.4", "0.61 0.73", "0.9932 0.93", "0 0", "0 0");
				createLable(panelMain, ExparementCostPanel, "", options.ResearchAmount.ToString("##,#"), 40, "1 1 1 0.5", TextAnchor.MiddleCenter, "0 0", "1 1");
			}
            else
            {
				AddPanel(panelMain, SubPanel, ScrapMessagePanel, "0.368 0.368 0.368 0.4", "0.01 0.73", "0.9932 0.93", "0 0", "0 0");
				createLable(panelMain, ScrapMessagePanel, "", String.Format(lang.GetMessage("AllLearned", this, player.UserIDString), itemName), 19, "0.4 0.454 0.262 1", TextAnchor.MiddleLeft, "0.05 0", "0.95 1");
			}
			//ScrapImage
			
		
			AddPanel(panelMain, SubPanel, ExparementScrapPanel, "0.368 0.368 0.368 0.4", "0.368 0.42", "0.9932 0.72", "0 0", "0 0");
			createLable(panelMain, ExparementScrapPanel, "", String.Format(lang.GetMessage("itemMessageLower", this, player.UserIDString), itemName), 19, "1 1 1 0.7", TextAnchor.UpperLeft, "0.08 0", "0.95 0.8");

			//Item Got Window
			AddPanel(panelMain, SubPanel, ExparementBPPanel, "0.368 0.368 0.368 0.4", "0.01 0.11", "0.358 0.41", "0 0", "0 0");
			AddPanel(panelMain, SubPanel, ExparementBPPanelInfo, "0.368 0.368 0.368 0.4", "0.368 0.11", "0.9932 0.41", "0 0", "0 0");
			createLable(panelMain, ExparementBPPanelInfo, ExparementLableToChange, String.Format(lang.GetMessage("itemMessageInfo", this, player.UserIDString), itemName), 19, "1 1 1 0.7", TextAnchor.UpperLeft, "0.08 0", "0.95 0.8");

			//Footer
			AddPanel(panelMain, SubPanel, ExparementFooterPanel, "0.368 0.368 0.368 0.4", "0.01 0.01", "0.9932 0.10", "0 0", "0 0");

			CuiHelper.AddUi(player, panelMain);

			BuildItemSucess(player, workbench, totalNeededBP, helper);
		}

		public void BuildItemSucess(BasePlayer player, Workbench workbench, int totalNeed, WorkBenchHelper helper)
		{
			if (player == null || player.inventory.loot.entitySource == null || player.inventory.loot.entitySource != workbench)
				return;

			workbanchOptions options = helper.options;

			var panelGotItem = CreatePanel(ExparementItemGotPanel, ExparementBPPanel, "0.1 0.1", "0.9 0.9", "0 0", "0 0");
			int totalScrap =  GetAmount(player, options.itemid, options.skinid, options.name);
			string totalIHave = $"{totalScrap}";
			if (totalScrap >= 1000) totalIHave = totalScrap.ToString("##,#");

			AddPanel(panelGotItem, SubPanel, ExparementScrapItemPanel, "0.368 0.368 0.368 0.4", "0.01 0.42", "0.358 0.72", "0 0", "0 0");
			AddImage(panelGotItem, ExparementScrapItemPanel, "", options.itemid, options.skinid, "1 1 1 1", $"0.1 0.1", $"0.9 0.9", "0 0", "0 0");
			createLable(panelGotItem, ExparementScrapItemPanel, "", $"x{totalIHave}", 14, "1 1 1 0.7", TextAnchor.LowerRight, "0 0.02", "0.98 1");

			if (pcdData.TryGetValue(workbench.net.ID.Value, out int itemid) && itemid != 0)
			{
				AddImage(panelGotItem, ExparementItemGotPanel, "", itemid, 0, "1 1 1 1", $"0 0", $"1 1", "0 0", "0 0");
				AddButton(panelGotItem, ExparementItemGotPanel, "", "", 0, "0 0 0 0", "0 0 0 0", TextAnchor.MiddleCenter, $"{command} takeBP {SecretKey} {workbench.net.ID.Value} {itemid}", "0 0", "1 1", "0 0", "0 0");
			}

			if (totalNeed > 0 && !helper.working)
			{
				string message = itemid == 0 ? "experiment" : "LearnBlueprint";

				if (message == "experiment" && totalScrap < options.ResearchAmount)
					message = "NeedMoreItem";

				var ButtonPanel = CreatePanel(ButtonsToChange, ExparementFooterPanel, "0.1 0.1", "0.9 0.9", "0 0", "0 0");

				if (message != "NeedMoreItem")
					AddButton(ButtonPanel, ButtonsToChange, "", lang.GetMessage(message, this, player.UserIDString), 13, "1 1 1 0.8", "0.4 0.454 0.262 1", TextAnchor.MiddleCenter, $"{command} {message} {SecretKey} {workbench.net.ID.Value} {itemid}", "0.2 0.1", "0.8 0.9", "0 0", "0 0");
				CuiHelper.AddUi(player, ButtonPanel);
			}
			CuiHelper.AddUi(player, panelGotItem);


		}
		#endregion

		#region UI Helpers
		public BaseNetworkable FindEntity(ulong netID)
		{
			return BaseNetworkable.serverEntities.Find(new NetworkableId(netID));
		}

		public static int GetAmount(BasePlayer player, int itemid, ulong skin, string cName)
		{
			int num = 0;
			foreach (Item obj in player.inventory.containerMain.itemList)
			{
				if (obj.info.itemid == itemid && obj.skin == skin)
				{
					if (!string.IsNullOrEmpty(cName))
					{
						if (string.IsNullOrEmpty(obj.name) || cName != obj.name)
							continue;
					}

					num += obj.amount;
				}
			}
			foreach (Item obj in player.inventory.containerBelt.itemList)
			{
				if (obj.info.itemid == itemid && obj.skin == skin)
				{
					if (!string.IsNullOrEmpty(cName))
					{
						if (string.IsNullOrEmpty(obj.name) || cName != obj.name)
							continue;
					}

					num += obj.amount;
				}
			}
			return num;
		}

		public static int Take(BasePlayer player, int itemid, int iAmount, ulong skin, string cName)
		{
			int num1 = 0;
			if (iAmount == 0)
				return num1;
			List<Item> list = Facepunch.Pool.Get<List<Item>>();
			foreach (Item obj in player.inventory.containerMain.itemList)
			{
				if (obj.info.itemid == itemid && obj.skin == skin)
				{
					if (!string.IsNullOrEmpty(cName))
					{
						if (string.IsNullOrEmpty(obj.name) || cName != obj.name)
							continue;
					}

					int num2 = iAmount - num1;
					if (num2 > 0)
					{
						if (obj.amount > num2)
						{
							obj.MarkDirty();
							obj.amount -= num2;
							num1 += num2;
							Item byItemId = ItemManager.CreateByItemID(itemid);
							byItemId.amount = num2;
							byItemId.CollectedForCrafting(player);
							break;
						}
						if (obj.amount <= num2)
						{
							num1 += obj.amount;
							list.Add(obj);
						}
						if (num1 == iAmount)
							break;
					}
				}
			}

			foreach (Item obj in player.inventory.containerBelt.itemList)
			{
				if (obj.info.itemid == itemid && obj.skin == skin)
				{
					if (!string.IsNullOrEmpty(cName))
					{
						if (string.IsNullOrEmpty(obj.name) || cName != obj.name)
							continue;
					}

					int num2 = iAmount - num1;
					if (num2 > 0)
					{
						if (obj.amount > num2)
						{
							obj.MarkDirty();
							obj.amount -= num2;
							num1 += num2;
							Item byItemId = ItemManager.CreateByItemID(itemid);
							byItemId.amount = num2;
							byItemId.CollectedForCrafting(player);
							break;
						}
						if (obj.amount <= num2)
						{
							num1 += obj.amount;
							list.Add(obj);
						}
						if (num1 == iAmount)
							break;
					}
				}
			}

			foreach (Item obj in list)
				obj.RemoveFromContainer();
			Facepunch.Pool.FreeUnmanaged<Item>(ref list);
			return num1;
		}
		string UppercaseFirst(string str)
		{
			if (string.IsNullOrEmpty(str))
				return string.Empty;
			return char.ToUpper(str[0]) + str.Substring(1).ToLower();
		}

		public workbanchOptions GetWorkbenchOptions(Workbench bench)
        {
			switch (bench.Workbenchlevel)
			{
				case 1:
					return configData.options1;

				case 2:
					return configData.options2;

				case 3:
					return configData.options3;
			}
			return null;
		}

		private static CuiElementContainer CreatePanel(string panelName, string parent, string AnchorMin = "0.5 0", string AnchorMax = "0.5 0", string OffsetMin = "0 0", string OffsetMax = "0 0")
		{
			return new CuiElementContainer
			{
				new CuiElement
				{
					Parent = parent, Name = panelName, DestroyUi = panelName,
					Components = { new CuiRectTransformComponent { AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax, } }
				}
			};
		}

		private static void AddPanel(CuiElementContainer container, string panelName, string panelButton, string color = "0.33 0.33 0.33 0.90", string AnchorMin = "0 0", string AnchorMax = "0 0", string OffsetMin = "-400 -200", string OffsetMax = "400 200")
		{
			container.Add(new CuiPanel
			{
				CursorEnabled = true,
				Image = { Color = color },
				RectTransform = { AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax, }
			}, panelName, panelButton, panelButton);
		}

		private static void CreateCountdown(CuiElementContainer container, string parent, int time, string text = "%TIME_LEFT%", int size = 12, string command = "", string color = "0.60 255 0 0.68", string AnchorMin = "0.5 0", string AnchorMax = "0.5 0", string OffsetMin = "0 0", string OffsetMax = "0 0")
		{
			container.Add(new CuiElement
			{
				Parent = parent,
				Components =
				{
					new CuiCountdownComponent { StartTime = time, EndTime = 0, Step = 1, Command = command},
					new CuiTextComponent  { Text = $"{text}", FontSize = size, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = color},
					new CuiRectTransformComponent { AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax },
				}
			});
		}

		private static void createLable(CuiElementContainer container, string parent, string panelN, string message, int size, string color, TextAnchor anchor, string ancorMin, string ancorMax, string OffsetMin = "0 0", string OffsetMax = "0 0")
		{
			container.Add(new CuiLabel
			{
				Text = { Text = message, FontSize = size, Align = anchor, Color = color },
				RectTransform = { AnchorMin = ancorMin, AnchorMax = ancorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax }
			}, parent, panelN);
		}

		private static void createElement(CuiElementContainer container, string parent, string png, string ancorMin, string ancorMax, string color)
		{
			container.Add(new CuiElement
			{
				Parent = parent,
				Components = { new CuiImageComponent { Sprite = png, Color = color }, new CuiRectTransformComponent { AnchorMin = ancorMin, AnchorMax = ancorMax } }
			});
		}

		private static void AddButton(CuiElementContainer container, string panelName, string panelButton, string text, int testSize, string colorT, string colorB, TextAnchor anchor = TextAnchor.MiddleCenter, string usaageCommand = "", string AnchorMin = "0 0", string AnchorMax = "0 0", string OffsetMin = "236.5 30.0", string OffsetMax = "378 55.0")
		{
			container.Add(new CuiButton
			{
				Text = { Text = text, FontSize = testSize, Align = anchor, Color = colorT },
				Button = { Command = usaageCommand, Color = colorB },
				RectTransform = { AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax, }
			}, panelName, panelButton);
		}

		private static void AddImage(CuiElementContainer container, string parent, string panelName, int itemID, ulong skinID, string color, string AnchorMin = "0 0", string AnchorMax = "1 1", string OffsetMin = "0 0", string OffsetMax = "0 0")
		{
			container.Add(new CuiElement
			{
				Name = panelName,
				Parent = parent,
				Components = { new CuiRectTransformComponent { AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax },
				new CuiImageComponent { ItemId = itemID, SkinId = skinID, Color = color } }
			});
		}

		private static void RunEffect(string prefab, BasePlayer player)
		{
			var effect = new Effect();
			effect.Init(Effect.Type.Generic, player.transform.position, Vector3.zero);
			effect.pooledString = prefab;
			EffectNetwork.Send(effect, player.net.connection);
		}
		#endregion

		#region Ui Commands
		[ConsoleCommand(command)]
		private void UiActionCommand(ConsoleSystem.Arg arg)
		{
			if (arg == null || arg.Args == null || arg.Args.Length < 2)
				return;

			BasePlayer player = arg.Player();

			if (!ulong.TryParse(arg.Args[1], out var key))
				return;

			if (key != SecretKey)
				return;

			if (!ulong.TryParse(arg.Args[2], out var workbenchID))
				return;

			WorkBenchHelper workbench = FindEntity(workbenchID)?.GetComponent<WorkBenchHelper>();
			if (workbench == null) return;

			if (player != null)
			{
				switch (arg.Args[0])
				{
					case "experiment":
						{
							workbench.BeginExperiment(player);
							break;
						}

					case "takeBP":
						{
							if (!int.TryParse(arg.Args[3], out var itemid))
								return;

							workbench.TakeBlueprint(player, itemid);
							break;
						}

					case "LearnBlueprint":
						{
							if (!int.TryParse(arg.Args[3], out var itemid))
								return;

							if (!workbench.UnlockBlueprint(player, itemid))
                            {
								player.ShowToast(GameTip.Styles.Blue_Normal, lang.GetMessage("nopeLearned", this, player.UserIDString));
							}
							break;
						}

					default:
						break;
				}
			}
		}
		#endregion

		#region Localization
		private new void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["LearnBlueprint"] = "LEARN BLUEPRINT",
				["experiment"] = "START EXPERIMENT",
				["workbencHead"] = "WORKBENCH LEVEL {0}",
				["nopeLearned"] = "You already know this blueprint!",
				["AllLearned"] = "You have unlocked everything available form this workbench",
				["itemMessage"] = "Experiment Cost\n<size=12>{0} required to conduct an experiment</size>",
				["itemMessageLower"] = "{0} To Use\n<size=12>{0} is required to produce an item blueprint, your amount shows here.</size>",
				["itemMessageInfo"] = "Experiment Result\n<size=12>The item created from your experiment will appear to the left.</size>",
				["5"] = "10",
				["5"] = "9",
				["5"] = "8",
				["5"] = "7",
				["5"] = "6",
				["5"] = "5",
				["4"] = "4",
				["3"] = "3",
				["2"] = "2",
				["1"] = "1",
				["0"] = "0",
			}, this);
		}
		#endregion
	}
}
    
