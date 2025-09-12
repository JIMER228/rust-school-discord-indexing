/*
*  < ----- End-User License Agreement ----->
*  
*  You may not copy, modify, merge, publish, distribute, sublicense, or sell copies of this software without the developer’s consent.
*
*  THIS SOFTWARE IS PROVIDED BY IIIaKa AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, 
*  THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS 
*  BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
*  GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT 
*  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*
*  Developer: IIIaKa
*      https://t.me/iiiaka
*      Discord: @iiiaka
*      https://github.com/IIIaKa
*      https://umod.org/user/IIIaKa
*      https://codefling.com/iiiaka
*      https://lone.design/vendor/iiiaka/
*      https://www.patreon.com/iiiaka
*      https://boosty.to/iiiaka
*  Codefling plugin page: https://codefling.com/plugins/activity-rewards
*  Codefling license: https://codefling.com/plugins/activity-rewards?tab=downloads_field_4
*  
*  Lone.Design plugin page: https://lone.design/product/activity-rewards/
*
*  Copyright © 2024 IIIaKa
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("ActivityRewards", "IIIaKa", "0.1.4")]
	[Description("Plugin rewarding players for their in-game activity.")]
	class ActivityRewards : RustPlugin
	{
		[PluginReference]
		private Plugin ImageLibrary, AdvancedStatus, ServerPanels;
		
		#region ~Variables~
		private static ActivityRewards Instance { get; set; }
		private bool _imgLibIsLoaded = false, _statusIsLoaded = false;
		private const string StatusCreateBar = "CreateBar", NoteInv = "note.inv",
			_gatherPath = @"ActivityRewards\GatherRewards", _killPath = @"ActivityRewards\KillRewards", _openPath = @"ActivityRewards\FirstLootOpenRewards", _pickupPath = @"ActivityRewards\PickupRewards", _plantingPath = @"ActivityRewards\PlantingRewards", _fishingPath = @"ActivityRewards\FishingRewards";
		private static Dictionary<string, RewardData> _gatherConfig, _killConfig, _openConfig, _pickupConfig, _plantingConfig, _fishingConfig;
		private readonly string[] HttpScheme = new string[2] { "http://", "https://" };
		private HashSet<string> _plugins = new HashSet<string>();
		private Hash<ulong, ulong> _patrolLastHit = new Hash<ulong, ulong>();
		#endregion

        #region ~Configuration~
        private static Configuration _config;

		private class Configuration
        {
			[JsonProperty(PropertyName = "Is it worth using the AdvancedStatus plugin?")]
            public bool AdvancedStatus_Enabled = true;
			
			[JsonProperty(PropertyName = "Is it worth enabling the Gather Rewards?")]
            public bool Gather_Enabled = true;
			
			[JsonProperty(PropertyName = "Is it worth enabling the Kill Rewards?")]
            public bool Kill_Enabled = true;
			
			[JsonProperty(PropertyName = "Is it worth enabling the Loot Open Rewards?")]
            public bool LootOpen_Enabled = true;
			
			[JsonProperty(PropertyName = "Is it worth enabling the Pickup Rewards?")]
            public bool Pickup_Enabled = true;
			
			[JsonProperty(PropertyName = "Is it worth enabling the Planting Rewards?")]
            public bool Planting_Enabled = true;
			
			[JsonProperty(PropertyName = "Is it worth enabling the Fishing Rewards?")]
            public bool Fishing_Enabled = true;
			
			[JsonProperty(PropertyName = "List of reward multipliers for each permission")]
            public Dictionary<string, double> PermissionsList;
			
			[JsonProperty(PropertyName = "The list of economy plugins for rewards")]
			public Dictionary<string, EcoPlugin> EcoPlugins;
			
			public Oxide.Core.VersionNumber Version;
		}
		
		protected override void LoadConfig()
        {
			base.LoadConfig();
            try { _config = Config.ReadObject<Configuration>(); }
            catch (Exception ex) { PrintError($"{ex.Message}\n\n[{Title}] Your configuration file contains an error."); }
            if (_config == null || _config.Version == new VersionNumber())
            {
                PrintWarning("The configuration file is not found or contains errors. Creating a new one...");
                LoadDefaultConfig();
            }
            else if (_config.Version < Version)
            {
                PrintWarning($"Your configuration file version({_config.Version}) is outdated. Updating it to {Version}.");
                _config.Version = Version;
                PrintWarning($"The configuration file has been successfully updated to version {_config.Version}!");
            }
			
			if (_config.PermissionsList == null || !_config.PermissionsList.Any())
				_config.PermissionsList = new Dictionary<string, double>() { { "realpve.default", 1d }, { "realpve.vip", 1.1d } };
			
			if (_config.EcoPlugins == null)
				_config.EcoPlugins = new Dictionary<string, EcoPlugin>();
			_config.EcoPlugins = _config.EcoPlugins.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
			
			if (!_config.EcoPlugins.TryGetValue("Economics", out var economicsPlugin) || economicsPlugin == null)
				_config.EcoPlugins["Economics"] = new EcoPlugin("Economics", false);
			if (!_config.EcoPlugins.TryGetValue("ServerRewards", out var rewardPlugin) || rewardPlugin == null)
				_config.EcoPlugins["ServerRewards"] = new EcoPlugin("ServerRewards", true, "AddPoints", "TakePoints");
			if (!_config.EcoPlugins.TryGetValue("BankSystem", out var bankPlugin) || bankPlugin == null)
				_config.EcoPlugins["BankSystem"] = new EcoPlugin("BankSystem", true);
			
			foreach (var kvp in _config.EcoPlugins)
            {
                var ecoPlugin = kvp.Value;
                ecoPlugin.Name = kvp.Key;
                ecoPlugin.BarId = $"{Name}_{ecoPlugin.Name}";
                if (string.IsNullOrWhiteSpace(ecoPlugin.Text_Key))
                    ecoPlugin.Text_Key = $"Msg{ecoPlugin.Name}";
				if (string.IsNullOrWhiteSpace(ecoPlugin.Deposit))
					ecoPlugin.Deposit = "Deposit";
				if (string.IsNullOrWhiteSpace(ecoPlugin.Withdraw))
					ecoPlugin.Withdraw = "Withdraw";
				if (ecoPlugin.BarSettings == null)
					ecoPlugin.BarSettings = new BarSettings();
			}
			
			SaveConfig();
        }
		
		protected override void SaveConfig() => Config.WriteObject(_config);
		protected override void LoadDefaultConfig() => _config = new Configuration() { Version = Version };
		#endregion
		
		#region ~Language~
		protected override void LoadDefaultMessages()
        {
			var keys = _config.EcoPlugins.Select(kvp => kvp.Value.Text_Key).Where(key => !string.IsNullOrWhiteSpace(key)).ToHashSet();
			lang.RegisterMessages(keys.ToDictionary(key => key, key => "Bonus"), this);
			lang.RegisterMessages(keys.ToDictionary(key => key, key => "Бонус"), this, "ru");
		}
        #endregion

        #region ~Methods~
		private void ToggleImageLib(bool isLoaded)
        {
			if (!_config.AdvancedStatus_Enabled) return;
			
			_imgLibIsLoaded = isLoaded;
            if (_imgLibIsLoaded)
			{
				var imgList = new Dictionary<string, string>();
                foreach (var ecoPlugin in _config.EcoPlugins.Values)
                {
                    if (string.IsNullOrWhiteSpace(ecoPlugin.BarSettings.Image_Sprite) && string.IsNullOrWhiteSpace(ecoPlugin.BarSettings.Image_Local) && ecoPlugin.BarSettings.Image_Url.StartsWithAny(HttpScheme))
                        imgList.Add(ecoPlugin.BarId, ecoPlugin.BarSettings.Image_Url);
                }
                if (imgList.Any())
                    ImageLibrary?.Call("ImportImageList", Name, imgList, 0uL, true);
			}
			if (_statusIsLoaded)
            {
                foreach (var ecoPlugin in _config.EcoPlugins.Values)
                {
                    if (ecoPlugin.StatusBar != null)
                        ecoPlugin.SelectBarImage();
                }
            }
        }
		
		private void CheckPlugins(Plugin plugin, bool isLoad = false)
        {
			if (!_plugins.Contains(plugin.Name)) return;
			foreach (var ecoPlugin in _config.EcoPlugins.Values)
            {
                if (!ecoPlugin.IsEnabled || plugin.Name != ecoPlugin.Name) continue;
				ecoPlugin.Plugin = isLoad ? plugin : null;
                ecoPlugin.IsReady = ecoPlugin.Plugin != null;
				if (_config.AdvancedStatus_Enabled)
                {
					if (ecoPlugin.IsReady)
                    {
                        ecoPlugin.PrepareStatusBar();
                        ecoPlugin.SelectBarImage();
                    }
					else if (ecoPlugin.StatusBar != null)
						ecoPlugin.ClearStatusBar();
				}
				break;
			}
		}
		
		private void KillReward(BasePlayer player, string shortName)
        {
			if (!_killConfig.TryGetValue(shortName, out var rewData))
            {
				foreach (var kvp in _killConfig)
                {
                    if (shortName.StartsWith(kvp.Key))
                    {
                        rewData = kvp.Value;
                        break;
                    }
                }
            }
			if (rewData != null)
				GiveReward(player, rewData);
		}
		
		private void GiveReward(BasePlayer player, RewardData rewData)
        {
            foreach (var ecoPlugin in _config.EcoPlugins.Values)
            {
				if (!ecoPlugin.IsReady) continue;
				if (ecoPlugin.IsRewardInt)
					GivePluginReward(player, ecoPlugin, rewData.PluginInt);
				else
					GivePluginReward(player, ecoPlugin, rewData.PluginDouble);
			}
			
			if (rewData.ItemsList == null) return;
			foreach (var itemReward in rewData.ItemsList)
            {
				if (!itemReward.IsReady) continue;
				var item = ItemManager.Create(itemReward.ItemA.itemDef, (int)itemReward.ItemA.amount, itemReward.SkinId);
                if (item == null) continue;
				if (player.inventory.GiveItem(item))
					player.SendConsoleCommand(NoteInv, itemReward.ItemA.itemid, item.amount);
				else
					item.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity);
			}
        }
		
		private void GivePluginReward(BasePlayer player, EcoPlugin ecoPlugin, int reward)
        {
            int amount = (int)(reward * GetRewardMultiplier(player.UserIDString));
			if (amount > 0)
			{
				ecoPlugin.Plugin.Call(ecoPlugin.Deposit, player.userID.Get(), amount);
				ShowBar(player, ecoPlugin, amount, true);
			}
			else if (amount < 0)
			{
				ecoPlugin.Plugin.Call(ecoPlugin.Withdraw, player.userID.Get(), -amount);
				ShowBar(player, ecoPlugin, amount, false);
			}
		}
		
		private void GivePluginReward(BasePlayer player, EcoPlugin ecoPlugin, double reward)
		{
			double amount = reward * GetRewardMultiplier(player.UserIDString);
			if (amount > 0d)
			{
				ecoPlugin.Plugin.Call(ecoPlugin.Deposit, player.userID.Get(), amount);
				ShowBar(player, ecoPlugin, amount, true);
			}
			else if (reward < 0d)
			{
				ecoPlugin.Plugin.Call(ecoPlugin.Withdraw, player.userID.Get(), -amount);
				ShowBar(player, ecoPlugin, amount, false);
			}
		}
		
		private void ShowBar(BasePlayer player, EcoPlugin ecoPlugin, double amount, bool isPositive)
        {
			if (!_statusIsLoaded || !_config.AdvancedStatus_Enabled || ecoPlugin.StatusBar == null)
            {
				if (isPositive)
					player.SendConsoleCommand(NoteInv, 963906841, 1, $"<color=#A3FF00>+{amount} {lang.GetMessage(ecoPlugin.Text_Key, this, player.UserIDString)}</color>");
				else
					player.SendConsoleCommand(NoteInv, 963906841, 0, $"{amount} {lang.GetMessage(ecoPlugin.Text_Key, this, player.UserIDString)}");
			}
			else
			{
				AdvancedStatus?.Call(StatusCreateBar, player.userID.Get(), new Dictionary<int, object>(ecoPlugin.StatusBar)
				{
					{ 15, lang.GetMessage(ecoPlugin.Text_Key, this, player.UserIDString) },
					{ 22, $"{(isPositive ? "+" : string.Empty)}{amount}" },
					{ 29, Network.TimeEx.currentTimestamp + 4d }
				});
			}
		}
		
		private void PrepareRewardItems(ICollection<RewardData> list)
        {
			foreach (var rewData in list)
			{
				if (rewData.ItemsList == null) continue;
				foreach (var rewardItem in rewData.ItemsList)
                {
					var itemDef = ItemManager.FindItemDefinition(rewardItem.ShortName);
					if (itemDef == null)
					{
						PrintError($"Failed to find an item with the name '{rewardItem.ShortName}'!");
						rewardItem.IsReady = false;
					}
					else
					{
						rewardItem.ItemA = new ItemAmount(itemDef, rewardItem.Amount);
						rewardItem.IsReady = true;
					}
				}
			}
		}
		
		private double GetRewardMultiplier(string userID)
		{
            double result = 1d;
			foreach (var kvp in _config.PermissionsList)
			{
				if (kvp.Value > result && permission.UserHasPermission(userID, kvp.Key))
					result = kvp.Value;
			}
			return result;
		}
		
		private static bool TryLoadReward(string filePath, out Dictionary<string, RewardData> result)
        {
			result = null;
			try { result = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, RewardData>>(filePath); }
			catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
			return result != null && result.Any();
		}
		private static void SaveRewardConfig(string path, Dictionary<string, RewardData> obj) => Interface.Oxide.DataFileSystem.WriteObject(path, obj);
        #endregion

        #region ~Oxide Hooks~
		void OnPatrolHelicopterTakeDamage(PatrolHelicopter patrol)
        {
            if (patrol.lastAttacker is BasePlayer attacker && attacker != null && attacker.userID.IsSteamId())
                _patrolLastHit[patrol.net.ID.Value] = attacker.userID;
        }
		
		void OnItemDropped(Item item, DroppedItem droppedItem)
        {
            if (item != null && droppedItem != null && droppedItem.DroppedBy == 0uL && item.parent == null && droppedItem.DropReason == DroppedItem.DropReasonEnum.Unknown)
                droppedItem.DroppedBy++;
        }
		
		void OnAdvancedStatusLoaded()
		{
			_statusIsLoaded = true;
			var imgList = new List<string>();
            foreach (var ecoPlugin in _config.EcoPlugins.Values)
            {
				if (!string.IsNullOrWhiteSpace(ecoPlugin.BarSettings.Image_Local))
                    imgList.Add(ecoPlugin.BarSettings.Image_Local);
			}
			if (imgList.Any())
                AdvancedStatus?.Call("LoadImages", imgList);
		}
		
		void OnPluginLoaded(Plugin plugin)
        {
			if (plugin == ImageLibrary)
				ToggleImageLib(true);
			else if (plugin != AdvancedStatus)
				CheckPlugins(plugin, true);
		}
		
		void OnPluginUnloaded(Plugin plugin)
		{
			if (plugin.Name == "ImageLibrary")
				ToggleImageLib(false);
			else if (plugin.Name == "AdvancedStatus")
				_statusIsLoaded = false;
			else
				CheckPlugins(plugin);
		}
		
		void Init()
        {
			Unsubscribe(nameof(OnPluginLoaded));
			Unsubscribe(nameof(OnPluginUnloaded));
			Unsubscribe(nameof(OnDispenserBonusReceived));
			Unsubscribe(nameof(OnEntityDeath));
			Unsubscribe(nameof(OnPatrolHelicopterTakeDamage));
			Unsubscribe(nameof(CanLootEntity));
			Unsubscribe(nameof(OnCollectiblePickup));
			Unsubscribe(nameof(OnGrowableGathered));
			Unsubscribe(nameof(OnItemPickup));
			Unsubscribe(nameof(OnItemDropped));
			Unsubscribe(nameof(OnEntitySpawned));
			Unsubscribe(nameof(OnFishCatch));
			Unsubscribe(nameof(OnAdvancedStatusLoaded));
			Instance = this;
			
			if (!TryLoadReward(_gatherPath, out _gatherConfig))
			{
				_gatherConfig = new Dictionary<string, RewardData>() { { "wood", new RewardData(5, 0.5d) }, { "stones", new RewardData(10, 1d) }, { "metal.ore", new RewardData(15, 1.5d) }, { "sulfur.ore", new RewardData(20, 2d) } };
				SaveRewardConfig(_gatherPath, _gatherConfig);
			}
			if (!TryLoadReward(_killPath, out _killConfig))
			{
				_killConfig = new Dictionary<string, RewardData>()
				{
					{ "dead_log_", new RewardData(2, 0.25d) }, { "player", new RewardData(10, 1d) }, { "suicide", new RewardData(-5, -0.5d) }, { "loot-barrel", new RewardData(5, 0.5d) }, { "loot_barrel", new RewardData(5, 0.5d) },
					{ "oil_barrel", new RewardData(10, 1d) }, { "roadsign", new RewardData(5, 0.5d) }, { "scientistnpc", new RewardData(15, 1.5d) }, { "npc_tunneldweller", new RewardData(15, 1.5d) }, { "npc_underwaterdweller", new RewardData(15, 1.5d) },
					{ "scientistnpc_junkpile_pistol", new RewardData(10, 1d) }, { "scientistnpc_heavy", new RewardData(20, 2d) }, { "chicken", new RewardData(5, 0.5d) }, { "boar", new RewardData(10, 1d) }, { "stag", new RewardData(15, 1.5d) },
					{ "wolf", new RewardData(20, 2d) }, { "bear", new RewardData(20, 2d) }, { "simpleshark", new RewardData(30, 3d) }, { "chicken.corpse", new RewardData(3, 0.25d) }, { "boar.corpse", new RewardData(5, 0.5d) },
					{ "stag.corpse", new RewardData(7, 0.75d) }, { "wolf.corpse", new RewardData(10, 1d) }, { "bear.corpse", new RewardData(10, 1d) }, { "shark.corpse", new RewardData(15, 1.5d) }, { "patrolhelicopter", new RewardData(100, 10d) },
					{ "bradleyapc", new RewardData(100, 10d) }
				};
				SaveRewardConfig(_killPath, _killConfig);
			}
			if (!TryLoadReward(_openPath, out _openConfig))
            {
				_openConfig = new Dictionary<string, RewardData>()
				{
					{ "foodbox", new RewardData(5, 0.5d) }, { "crate_food_1", new RewardData(5, 0.5d) }, { "crate_food_2", new RewardData(5, 0.5d) },
					{ "crate_normal_2_food", new RewardData(10, 1d) }, { "wagon_crate_normal_2_food", new RewardData(10, 1d) }, { "crate_normal_2_medical", new RewardData(10, 1d) }, { "vehicle_parts", new RewardData(5, 0.5d) },
					{ "crate_basic", new RewardData(5, 0.5d) }, { "crate_normal_2", new RewardData(10, 1d) }, { "crate_mine", new RewardData(10, 1d) }, { "crate_tools", new RewardData(15, 1.5d) }, { "crate_normal", new RewardData(20, 2d) }, { "crate_elite", new RewardData(25, 2.5d) },
					{ "crate_underwater_basic", new RewardData(5, 0.5d) }, { "crate_underwater_advanced", new RewardData(10, 1d) },
					{ "crate_medical", new RewardData(5, 0.5d) }, { "crate_fuel", new RewardData(10, 1d) }, { "crate_ammunition", new RewardData(10, 1d) }, { "heli_crate", new RewardData(30, 3d) }, { "bradley_crate", new RewardData(30, 3d) },
					{ "codelockedhackablecrate", new RewardData(100, 10d) }, { "codelockedhackablecrate_oilrig", new RewardData(100, 10d) }
				};
				SaveRewardConfig(_openPath, _openConfig);
			}
			if (!TryLoadReward(_pickupPath, out _pickupConfig))
            {
				_pickupConfig = new Dictionary<string, RewardData>()
				{
					{ "Wood", new RewardData(1, 0.1d) }, { "Stone", new RewardData(2, 0.25d) }, { "Metal Ore", new RewardData(5, 0.5d) }, { "Sulfur Ore", new RewardData(7, 0.75d) }, { "Green Keycard", new RewardData(10, 1d) }, { "Blue Keycard", new RewardData(20, 2d) }, { "Red Keycard", new RewardData(30, 3d) },
					{ "Diesel Fuel", new RewardData(10, 1d) }, { "Bones", new RewardData(1, 0.1d) }, { "Corn", new RewardData(1, 0.1d) }, { "Potato", new RewardData(1, 0.1d) }, { "Pumpkin", new RewardData(1, 0.1d) }, { "Wild Mushroom", new RewardData(1, 0.1d) }, { "Hemp Fibers", new RewardData(1, 0.1d) },
					{ "Black Berry", new RewardData(1, 0.1d) }, { "Blue Berry", new RewardData(1, 0.1d) }, { "Green Berry", new RewardData(1, 0.1d) }, { "Red Berry", new RewardData(1, 0.1d) }, { "White Berry", new RewardData(1, 0.1d) }, { "Yellow Berry", new RewardData(1, 0.1d) }
				};
				SaveRewardConfig(_pickupPath, _pickupConfig);
			}
			if (!TryLoadReward(_plantingPath, out _plantingConfig))
            {
				_plantingConfig = new Dictionary<string, RewardData>()
				{
					{ "hemp.entity", new RewardData(1, 0.1d) }, { "corn.entity", new RewardData(1, 0.1d) }, { "pumpkin.entity", new RewardData(1, 0.1d) }, { "potato.entity", new RewardData(1, 0.1d) }, { "black_berry.entity", new RewardData(1, 0.1d) },
					{ "blue_berry.entity", new RewardData(1, 0.1d) }, { "green_berry.entity", new RewardData(1, 0.1d) }, { "red_berry.entity", new RewardData(1, 0.1d) }, { "white_berry.entity", new RewardData(1, 0.1d) }, { "yellow_berry.entity", new RewardData(1, 0.1d) }
				};
				SaveRewardConfig(_plantingPath, _plantingConfig);
			}
			if (!TryLoadReward(_fishingPath, out _fishingConfig))
            {
                _fishingConfig = new Dictionary<string, RewardData>()
				{
					{ "fish.minnows", new RewardData(1, 0.1d) }, { "fish.anchovy", new RewardData(3, 0.3d) }, { "fish.herring", new RewardData(3, 0.3d) }, { "fish.sardine", new RewardData(3, 0.3d) }, { "fish.troutsmall", new RewardData(5, 0.5d) },
					{ "fish.yellowperch", new RewardData(5, 0.5d) }, { "fish.salmon", new RewardData(10, 1d) }, { "fish.catfish", new RewardData(10, 1d) }, { "fish.orangeroughy", new RewardData(10, 1d) },
					{ "fish.smallshark", new RewardData(50, 5d, new List<RewardItem>() { new RewardItem("scrap", 10) }) }
				};
                SaveRewardConfig(_fishingPath, _fishingConfig);
            }
		}
		
		void OnServerInitialized(bool initial)
        {
			foreach (var ecoPlugin in _config.EcoPlugins.Values)
            {
                if (!ecoPlugin.IsEnabled) continue;
				ecoPlugin.Plugin = Manager.GetPlugin(ecoPlugin.Name);
                ecoPlugin.IsReady = ecoPlugin.Plugin != null;
				if (ecoPlugin.IsReady && _config.AdvancedStatus_Enabled)
                {
                    ecoPlugin.PrepareStatusBar();
                    ecoPlugin.SelectBarImage();
                }
				_plugins.Add(ecoPlugin.Name);
			}
			
			if (_config.Gather_Enabled)
			{
				PrepareRewardItems(_gatherConfig.Values);
				Subscribe(nameof(OnDispenserBonusReceived));
			}
			if (_config.Kill_Enabled)
            {
				PrepareRewardItems(_killConfig.Values);
				Subscribe(nameof(OnEntityDeath));
				Subscribe(nameof(OnPatrolHelicopterTakeDamage));
			}
			if (_config.LootOpen_Enabled)
			{
				PrepareRewardItems(_openConfig.Values);
				Subscribe(nameof(CanLootEntity));
			}
			if (_config.Pickup_Enabled)
            {
				PrepareRewardItems(_pickupConfig.Values);
				Subscribe(nameof(OnCollectiblePickup));
				Subscribe(nameof(OnGrowableGathered));
				Subscribe(nameof(OnItemPickup));
				Subscribe(nameof(OnItemDropped));
			}
			if (_config.Planting_Enabled)
			{
				PrepareRewardItems(_plantingConfig.Values);
				Subscribe(nameof(OnEntitySpawned));
			}
			if (_config.Fishing_Enabled)
			{
				PrepareRewardItems(_fishingConfig.Values);
				Subscribe(nameof(OnFishCatch));
			}
			
			if (_config.AdvancedStatus_Enabled)
			{
				ToggleImageLib(ImageLibrary != null && ImageLibrary.IsLoaded);
				_statusIsLoaded = AdvancedStatus != null && AdvancedStatus?.Call("IsReady") != null;
                if (!_statusIsLoaded)
                {
                    if (initial && AdvancedStatus != null)
                        PrintWarning("AdvancedStatus plugin found, but not ready yet. Waiting for it to load...");
                    else
						PrintWarning("AdvancedStatus plugin not found! To function, it is necessary to install it!\n* https://codefling.com/plugins/advanced-status\n* https://lone.design/product/advanced-status/");
				}
                else
                    OnAdvancedStatusLoaded();
				Subscribe(nameof(OnAdvancedStatusLoaded));
			}
			Subscribe(nameof(OnPluginLoaded));
			Subscribe(nameof(OnPluginUnloaded));
		}
		
		void Unload()
		{
            _gatherConfig = null;
            _killConfig = null;
            _openConfig = null;
            _pickupConfig = null;
            _plantingConfig = null;
			Instance = null;
			_config = null;
		}
        #endregion
		
		#region ~Activity Rewards~
        void OnDispenserBonusReceived(ResourceDispenser dispenser, BasePlayer player, Item item)
		{
			if (_gatherConfig.TryGetValue(item.info.shortname, out var rewData))
				GiveReward(player, rewData);
		}

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
			if (info != null && info.InitiatorPlayer is BasePlayer attacker && attacker.userID.IsSteamId())
				KillReward(attacker, entity.ShortPrefabName);
		}
		
		void OnEntityDeath(ResourceEntity entity, HitInfo info)
        {
			if (info != null && info.InitiatorPlayer is BasePlayer attacker && attacker.userID.IsSteamId())
				KillReward(attacker, entity.ShortPrefabName);
		}
		
		void OnEntityDeath(BasePlayer player, HitInfo info)
        {
			if (info != null && info.InitiatorPlayer is BasePlayer attacker && attacker.userID.IsSteamId())
				KillReward(attacker, info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Suicide ? "suicide" : player.ShortPrefabName);
		}

        void OnEntityDeath(PatrolHelicopter patrol)
        {
			if (_patrolLastHit.TryGetValue(patrol.net.ID.Value, out var attackerID))
			{
				if (BasePlayer.TryFindByID(attackerID, out var attacker))
					KillReward(attacker, patrol.ShortPrefabName);
				_patrolLastHit.Remove(patrol.net.ID.Value);
			}
		}
		
		void CanLootEntity(BasePlayer player, StorageContainer container)
        {
			if (container.LastLootedBy != 0uL) return;
			NextTick(() =>
			{
				if (container.LastLootedBy == player.userID && _openConfig.TryGetValue(container.ShortPrefabName, out var rewData))
					GiveReward(player, rewData);
			});
		}
		
		void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
			NextTick(() =>
			{
				if (collectible.IsDestroyed && _pickupConfig.TryGetValue(collectible.itemName.english, out var rewData))
					GiveReward(player, rewData);
			});
		}
		
		void OnGrowableGathered(GrowableEntity plant, Item item, BasePlayer player)
		{
			if (_pickupConfig.TryGetValue(item.info.displayName.english, out var rewData))
				GiveReward(player, rewData);
		}
		
		void OnItemPickup(Item item, BasePlayer player)
        {
			if (item.GetWorldEntity() is not DroppedItem dropped || dropped.DroppedBy != 0uL) return;
			NextTick(() =>
			{
				if (item.parent != null && _pickupConfig.TryGetValue(item.info.displayName.english, out var rewData))
					GiveReward(player, rewData);
			});
		}
		
		void OnEntitySpawned(GrowableEntity growable)
        {
			if (BasePlayer.TryFindByID(growable.OwnerID, out var player) && _plantingConfig.TryGetValue(growable.ShortPrefabName, out var rewData))
				GiveReward(player, rewData);
		}

        void OnFishCatch(Item item, BaseFishingRod rod, BasePlayer player)
        {
			NextTick(() =>
			{
				if (item != null && _fishingConfig.TryGetValue(item.info.shortname, out var rewData))
					GiveReward(player, rewData);
			});
		}
        #endregion
		
		#region ~Classes~
        public class RewardData
        {
			public int PluginInt { get; set; }
			public double PluginDouble { get; set; }
			public List<RewardItem> ItemsList { get; set; }
			
			public RewardData() {}
            public RewardData(int pInt, double pDouble, List<RewardItem> items = null)
            {
                PluginInt = pInt;
                PluginDouble = pDouble;
				if (items != null && items.Any())
					ItemsList = items;
			}
		}
		
		public class RewardItem
        {
			public string ShortName { get; set; }
			public int Amount { get; set; }
			public ulong SkinId { get; set; }
			
			[JsonIgnore] public bool IsReady { get; set; }
			[JsonIgnore] public ItemAmount ItemA { get; set; }
			
			public RewardItem() {}
			public RewardItem(string shortName, int amount, ulong skinId = 0uL)
            {
				ShortName = shortName;
				Amount = amount;
				SkinId = skinId;
			}
		}
		
		public class EcoPlugin
        {
			[JsonProperty(PropertyName = "Is it worth enabling the plugin for rewards?")]
			public bool IsEnabled { get; set; } = true;

			[JsonProperty(PropertyName = "Reward Type: true - int, false - double")]
			public bool IsRewardInt { get; set; }
			
			[JsonProperty(PropertyName = "Language key for the text")]
			public string Text_Key { get; set; }
			
			[JsonProperty(PropertyName = "API method name for deposit")]
			public string Deposit { get; set; }
			
			[JsonProperty(PropertyName = "API method name for withdraw")]
			public string Withdraw { get; set; }
			
			public BarSettings BarSettings { get; set; }
			
			[JsonIgnore] public string Name { get; set; }
			[JsonIgnore] public Plugin Plugin { get; set; }
			[JsonIgnore] public bool IsReady { get; set; }
			[JsonIgnore] public string BarId { get; set; }
			[JsonIgnore] public Dictionary<int, object> StatusBar { get; private set; }
			
			public EcoPlugin() {}
			public EcoPlugin(string name, bool isInt, string deposit = "Deposit", string withdraw = "Withdraw")
            {
				Name = name;
				IsRewardInt = isInt;
				Deposit = deposit;
				Withdraw = withdraw;
			}
			
			public void ClearStatusBar()
            {
                StatusBar.Clear();
                StatusBar = null;
			}
			
			public void PrepareStatusBar()
            {
				StatusBar = new Dictionary<int, object>
                {
                    { 0, BarId },
                    { 1, Instance.Name },
                    { 2, "Timed" },
                    { 4, BarSettings.Order },
                    { 5, BarSettings.Height },
                    { 6, BarSettings.Main_Color },
                    { 11, BarSettings.Image_IsRawImage },
                    { 16, BarSettings.Text_Size },
                    { 17, BarSettings.Text_Color },
                    { 18, BarSettings.Text_Font },
                    { 23, BarSettings.SubText_Size },
                    { 24, BarSettings.SubText_Color },
                    { 25, BarSettings.SubText_Font }
				};

                if (BarSettings.Main_Color.StartsWith("#"))
                    StatusBar.Add(-6, BarSettings.Main_Transparency);
                if (!string.IsNullOrWhiteSpace(BarSettings.Main_Material))
                    StatusBar.Add(7, BarSettings.Main_Material);
                if (!BarSettings.Image_IsRawImage)
                {
                    StatusBar.Add(12, BarSettings.Image_Color);
                    if (BarSettings.Image_Color.StartsWith("#"))
                        StatusBar.Add(-12, BarSettings.Image_Transparency);
                }
                if (BarSettings.Image_Outline_Enabled)
                {
                    StatusBar.Add(13, BarSettings.Image_Outline_Color);
                    if (BarSettings.Image_Outline_Color.StartsWith("#"))
                        StatusBar.Add(-13, BarSettings.Image_Outline_Transparency);
                    StatusBar.Add(14, BarSettings.Image_Outline_Distance);
                }
                if (BarSettings.Text_Offset_Horizontal != 0)
                    StatusBar.Add(19, BarSettings.Text_Offset_Horizontal);
                if (BarSettings.Text_Outline_Enabled)
                {
                    StatusBar.Add(20, BarSettings.Text_Outline_Color);
                    if (BarSettings.Text_Outline_Color.StartsWith("#"))
                        StatusBar.Add(-20, BarSettings.Text_Outline_Transparency);
                    StatusBar.Add(21, BarSettings.Text_Outline_Distance);
                }
                if (BarSettings.SubText_Outline_Enabled)
                {
                    StatusBar.Add(26, BarSettings.SubText_Outline_Color);
                    if (BarSettings.SubText_Outline_Color.StartsWith("#"))
                        StatusBar.Add(-26, BarSettings.SubText_Outline_Transparency);
                    StatusBar.Add(27, BarSettings.SubText_Outline_Distance);
                }
			}
			
			public void SelectBarImage()
            {
                StatusBar.Remove(10);
                StatusBar.Remove(9);
                StatusBar.Remove(8);
                if (!string.IsNullOrWhiteSpace(BarSettings.Image_Sprite))
                    StatusBar.Add(10, BarSettings.Image_Sprite);
                else if (!string.IsNullOrWhiteSpace(BarSettings.Image_Local))
                    StatusBar.Add(9, BarSettings.Image_Local);
                else
                    StatusBar.Add(8, Instance._imgLibIsLoaded && BarSettings.Image_Url.StartsWithAny(Instance.HttpScheme) ? BarId : BarSettings.Image_Url);
            }
		}
		
		public class BarSettings
        {
			public int Order { get; set; } = 20;
            public int Height { get; set; } = 26;

            [JsonProperty(PropertyName = "Main_Color(Hex or RGBA)")]
            public string Main_Color { get; set; } = "#84AB49";

            public float Main_Transparency { get; set; } = 0.8f;

            [JsonProperty(PropertyName = "Main_Material(empty to disable)")]
            public string Main_Material { get; set; } = string.Empty;

            public string Image_Url { get; set; } = "https://i.imgur.com/k8jq7yY.png";

            [JsonProperty(PropertyName = "Image_Local(Leave empty to use Image_Url)")]
            public string Image_Local { get; set; } = "ActivityRewards_Default";

            [JsonProperty(PropertyName = "Image_Sprite(Leave empty to use Image_Local or Image_Url)")]
            public string Image_Sprite { get; set; } = string.Empty;

            public bool Image_IsRawImage { get; set; } = false;

            [JsonProperty(PropertyName = "Image_Color(Hex or RGBA)")]
            public string Image_Color { get; set; } = "#B9D134";

            public float Image_Transparency { get; set; } = 1f;

            [JsonProperty(PropertyName = "Is it worth enabling an outline for the image?")]
            public bool Image_Outline_Enabled { get; set; } = false;

            [JsonProperty(PropertyName = "Image_Outline_Color(Hex or RGBA)")]
            public string Image_Outline_Color { get; set; } = "0.1 0.3 0.8 0.9";

            public float Image_Outline_Transparency { get; set; } = 1f;
            public string Image_Outline_Distance { get; set; } = "0.75 0.75";
            public int Text_Size { get; set; } = 12;

            [JsonProperty(PropertyName = "Text_Color(Hex or RGBA)")]
            public string Text_Color { get; set; } = "#DAEBAD";

            [JsonProperty(PropertyName = "Text_Font(https://umod.org/guides/rust/basic-concepts-of-gui#fonts)")]
            public string Text_Font { get; set; } = "RobotoCondensed-Bold.ttf";

            public int Text_Offset_Horizontal { get; set; } = 0;

            [JsonProperty(PropertyName = "Is it worth enabling an outline for the text?")]
            public bool Text_Outline_Enabled { get; set; } = false;

            [JsonProperty(PropertyName = "Text_Outline_Color(Hex or RGBA)")]
            public string Text_Outline_Color { get; set; } = "#000000";

            public float Text_Outline_Transparency { get; set; } = 1f;
            public string Text_Outline_Distance { get; set; } = "0.75 0.75";
            public int SubText_Size { get; set; } = 12;

            [JsonProperty(PropertyName = "SubText_Color(Hex or RGBA)")]
            public string SubText_Color { get; set; } = "#DAEBAD";

            public string SubText_Font { get; set; } = "RobotoCondensed-Bold.ttf";

            [JsonProperty(PropertyName = "Is it worth enabling an outline for the sub text?")]
            public bool SubText_Outline_Enabled { get; set; } = false;

            [JsonProperty(PropertyName = "SubText_Outline_Color(Hex or RGBA)")]
            public string SubText_Outline_Color { get; set; } = "0.5 0.6 0.7 0.5";

            public float SubText_Outline_Transparency { get; set; } = 1f;
            public string SubText_Outline_Distance { get; set; } = "0.75 0.75";
		}
		#endregion
    }
}