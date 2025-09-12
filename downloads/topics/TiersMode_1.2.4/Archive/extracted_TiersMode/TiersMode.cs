using Facepunch;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins{

    [Info("TiersMode", "ninco90", "1.2.4")]
    [Description("Tiers game mode.")]
    public class TiersMode : RustPlugin {

        [PluginReference] Plugin ImageLibrary, DiscordMessages;

        #region Fields
        private DynamicConfigFile _pluginData;
        private TiersData _Data;
        private bool _PluginReady = false;
        private float minNextTier = 100.0f;
        private float timerInterval = 10.0f;
        private DateTime wipeTime;
        private Timer cuiTimer;
        private static CuiElementContainer containerTime;
        private float refreshTime = 10.0f;

        private bool counterEnabled = true;
        private bool screenCounterIcon = true;
        private bool screenCounterButton = true;
        private int tierCurrent = 0;
        private int tiercountAll = 0;
        private string tierName = null;
        private string tierColor = null;
        private string tierIcon = null;
        private bool enabledMaxCUP = false;
        private bool enabledAlertDestrTC = false;
        private bool alertDestrName = true;
        private bool alertDestrPos = true;
        private int limitCupboard = 0;
        private bool useWorkbench = true;
        private bool allPlaceWorkbench = true;
        private bool cargoEvent = true;
        private bool oilEvent = true;
        private bool ch47Event = true;
        private bool heliEvent = true;
        private bool supplyEvent = true;
        private bool bradleyEvent = true;
        private bool sellersHeli = true;
        private bool sellersBoat = true;
        private bool sellersHorse = true;
        private bool sellersDrones = true;
        private bool enabledMLRS = false;
        private bool enabledWORKCART = true;
        private bool enabledTRAIN = true;
        private bool sellersVending = true;
        private bool blockCraft = true;
        private bool blockDeployables = false;
        private int maxUpgrade = 0;
        private float craftingDuration = 1.0f;
        private float researchDuration = 10.0f;
        private float recyclerDuration = 5.0f;
        private bool gatherEnabled = false;
        private float multResource = 0.0f;					
		private float multResourceBonus = 0.0f;	
		private float multPickup = 0.0f;
		private float multGrowable = 0.0f;
        private float multSurvey = 0.0f;
        private float multQuarry = 0.0f;
        private float multExcavator = 0.0f;
        private List<string> listWorkbench = new List<string>();
        private List<string> listCards = new List<string>();
        private List<string> listVehicles = new List<string>();
        private List<string> blackItems = new List<string>();
        private Dictionary<string, string> blackItemsBuilding = new Dictionary<string, string>();
        private List<string> listCommand = new List<string>();

        private const string commandOpenInfo = "tier.open";
        private const string commandInfoClose = "open.close";
        private const string commandSelect = "tier.select";
        private const string commandEditClose = "edit.close";
        private const string commandClearList = "edit.clear_list";
        private const string commandClearItem = "edit.clear_iteam";
        private const string commandCopy = "edit.copy";
        private const string commandTierEditPage = "edit.page";
        private const string commandTierInfoPage = "info.page";
        private const string commandItemInfo  = "info.item";
        private const string commandItemInfoClose = "info.item.close";

        private const string permissionAdmin = "tiersmode.admin";
        private const string permByPassUseWorkcart = "tiersmode.bypass.use_workcart";
        private const string permByPassUseLocomotive = "tiersmode.bypass.use_locomotive";
        private const string permByPassUseMLRS = "tiersmode.bypass.use_mlrs";
        private const string permByPassLimitCupboard = "tiersmode.bypass.limit_cupboard";
        private const string permByPassBlockItems = "tiersmode.bypass.block_items";
        private const string permByPassBlockPlaceNoBuilding = "tiersmode.bypass.block_place_no_building";
        private const string permByPassPlaceAllWorkbench = "tiersmode.bypass.block_place_all_workbench";
        private const string permByPassBlockResearch = "tiersmode.bypass.block_research";
        private const string permByPassBlockHelis = "tiersmode.bypass.block_vending_helis";
        private const string permByPassBlockBoat = "tiersmode.bypass.block_vending_boat";
        private const string permByPassBlockHorses = "tiersmode.bypass.block_vending_horses";
        private const string permByPassInstantResearch = "tiersmode.instant_research";
        private const string permByPassInstantRecycler = "tiersmode.instant_recycler";
        private const string permByPassAlertsBlock = "tiersmode.alerts.block";
        private const string permByPassUpgrade = "tiersmode.bypass.upgrade";
        public string prefabMarketplace = "assets/prefabs/misc/marketplace/marketplace.prefab";

        //GUI
        private Dictionary<int, Dictionary<string, List<SectionInfo>>> itemType = new Dictionary<int, Dictionary<string, List<SectionInfo>>>();
        private class SectionInfo {
            public string img { get; set; }
            public string title { get; set; }
            public string text { get; set; }
        }

        private const string elemq0 = "gui.modal";
        private const string elemq1 = "gui.count";
        private const string elemq2 = "gui.count_time";
        private const string elemq3 = "gui.edit";
        private const string elemq4 = "gui.edit_item";
        private const string elemq5 = "gui.open";
        private const string elemq6 = "gui.open_item";
        private const string elemq7 = "gui.modal_changue";
        private const string elemq8 = "gui.modal_events";
        private const string elemq9 = "gui.open_info_item";
        private const string elemq10 = "gui.open_info_subitem";
        #endregion

        #region Hooks
        private void Init(){
            _pluginData = Interface.Oxide.DataFileSystem.GetFile(nameof(TiersMode));
            cmd.AddConsoleCommand(commandSelect, this, nameof(cmdSelectTier));
            cmd.AddConsoleCommand(commandEditClose, this, nameof(cmdCloseEdit));
            cmd.AddConsoleCommand(commandClearList, this, nameof(cmdClearList));
            cmd.AddConsoleCommand(commandClearItem, this, nameof(cmdClearItem));
            cmd.AddConsoleCommand(commandCopy, this, nameof(cmdCopy));
            cmd.AddConsoleCommand(commandTierEditPage, this, nameof(cmdTierEditPage));
            cmd.AddConsoleCommand(commandOpenInfo, this, nameof(cmdOpenInfo));
            cmd.AddConsoleCommand(commandInfoClose, this, nameof(cmdCloseInfo));
            cmd.AddConsoleCommand(commandTierInfoPage, this, nameof(cmdTierInfoPage));
            cmd.AddConsoleCommand(commandItemInfo, this, nameof(cmdItemInfo));
            cmd.AddConsoleCommand(commandItemInfoClose, this, nameof(cmdCloseItemInfo));
            
            cmd.AddConsoleCommand("tier.edit", this, nameof(cmdEditTier));
            
            permission.RegisterPermission(permissionAdmin, this);
            permission.RegisterPermission(permByPassAlertsBlock, this);
            permission.RegisterPermission(permByPassBlockBoat, this);
            permission.RegisterPermission(permByPassBlockHelis, this);
            permission.RegisterPermission(permByPassBlockHorses, this);
            permission.RegisterPermission(permByPassBlockItems, this);
            permission.RegisterPermission(permByPassBlockPlaceNoBuilding, this);
            permission.RegisterPermission(permByPassBlockResearch, this);
            permission.RegisterPermission(permByPassInstantRecycler, this);
            permission.RegisterPermission(permByPassInstantResearch, this);
            permission.RegisterPermission(permByPassLimitCupboard, this);
            permission.RegisterPermission(permByPassPlaceAllWorkbench, this);
            permission.RegisterPermission(permByPassUseLocomotive, this);
            permission.RegisterPermission(permByPassUseMLRS, this);
            permission.RegisterPermission(permByPassUseWorkcart, this);
            permission.RegisterPermission(permByPassUpgrade, this);

            LoadData();
        }

        private void OnServerInitialized(){
            if (ImageLibrary == null) {
                PrintWarning("The ImageLibrary plugin is not installed, I can't work without it. Load ImageLibrary and then load me again.");
                Interface.Oxide.RootPluginManager.RemovePlugin(this);
                return;
            }

            LoadImages();
            SaveDefaultDurationCraft();

            wipeTime = SaveRestore.SaveCreatedTime;
            if (_Data.activeTier == 999){
                tierCurrent = 0;
                NotifyInit();
            } else {
                tierCurrent = _Data.activeTier;
            }

            SelectTier(_Data.activeTier, tierCurrent);
            InfoData();
            PluginReady();
        }

        private void OnServerSave(){
            SaveData();
        }
        
        private void OnNewSave(){
            ClearData();
        }

        private void Unload(){
            SaveData();
            foreach (var player in BasePlayer.activePlayerList){
                CuiHelper.DestroyUi(player, elemq0);
                CuiHelper.DestroyUi(player, elemq1);
                CuiHelper.DestroyUi(player, elemq5);
                CuiHelper.DestroyUi(player, elemq7);
                CuiHelper.DestroyUi(player, elemq8);
                CuiHelper.DestroyUi(player, elemq9);
            }        
        }

        private void OnPlayerConnected(BasePlayer player){
            if (config.GUI.screenCounter && counterEnabled){ Countdown(player); if(containerTime != null) CuiHelper.AddUi(player, containerTime); } 
            if (config.MSG.chatEnabled && config.MSG.chatWelcomeEnabled) PrintToChat(player, Languaje("WelcomeChat", null, player.displayName, tierName));
            if (config.GUI.WINDOWS.showWindowsEnter) OpenPanel(player, tierCurrent, 0);
        }

        private bool? CanBuild(Planner planner, Construction prefab, Construction.Target target) {
            BasePlayer player = planner.GetOwnerPlayer();
            if (player == null) return null;
            if (prefab.deployable == null) return null;
            var fullname = prefab.deployable.fullName;
            if (fullname.Contains("cupboard.tool") && enabledMaxCUP){
                if(HasPermission(player.UserIDString, permByPassLimitCupboard)) return null;
                var cupCount = 0;
                _Data.armariosCount.TryGetValue(player.userID, out cupCount);
                if (cupCount > (limitCupboard-1)){
                    Notify(player, "red", "cupboard.tool", Languaje("BuildBlock", player.UserIDString), Languaje("MaxLimitCup", player.UserIDString, cupCount + "/" + limitCupboard), "Hud"); 
                    return false;
                } else {
                    _Data.armariosCount[player.userID] = cupCount + 1;
                    return null;
                }
            }

            if (fullname.Contains("workbench") && !allPlaceWorkbench){
                if(HasPermission(player.UserIDString, permByPassPlaceAllWorkbench)) return null;
                if (listWorkbench.Contains(fullname)){
                    return null;
                } else {
                    var workbench = "workbench3";
                    var level = "3";
                    if (fullname.Contains("1")){
                        workbench = "workbench1";
                        level = "1";
                    } else if (fullname.Contains("2")){
                        workbench = "workbench2";
                        level = "2";
                    }
                    Notify(player, "red", workbench, Languaje("BuildBlock", player.UserIDString), Languaje("NoPlaceWorkbench", player.UserIDString, level), "Hud");
                    return false;
                }
            }
 
            if (blackItemsBuilding.ContainsValue(fullname) && blockDeployables && !HasPermission(player.UserIDString, permByPassBlockPlaceNoBuilding)){
                var myKey = blackItemsBuilding.FirstOrDefault(x => x.Value == fullname).Key;
                BuildingPrivlidge priv = player.GetBuildingPrivilege();
                if (priv == null){
                    Notify(player, "red", myKey, Languaje("BuildBlock", player.UserIDString), Languaje("NoBuilding", player.UserIDString), "Hud");
                    return false;
                }
                if (priv.IsAuthed(player)) return null;
                Notify(player, "red", myKey, Languaje("BuildBlock", player.UserIDString), Languaje("NoBuilding", player.UserIDString), "Hud");
                return false;
            }
            return null;
        }

        private void OnEntitySpawned(BaseEntity entity){
            //Puts("NAME " + entity.ShortPrefabName);
            if(_PluginReady){
                if (entity.ShortPrefabName.Contains("codelockedhackablecrate_oilrig")){ 
                    if (!oilEvent){ NextTick(() =>{ if (entity != null) entity.Kill(); }); 
                    } else { NotifyEvents("blue", "hackable", Languaje("ServerEvents", null), Languaje("Event_HackableCrate", null)); }
                }
                if (entity.ShortPrefabName.Contains("ch47scientists")){ 
                    if (!ch47Event){ NextTick(() =>{ if (entity != null) entity.Kill(); });
                    } else { NotifyEvents("blue", "ch47", Languaje("ServerEvents", null), Languaje("Event_CH47", null)); }
                }
                if (entity.ShortPrefabName.Contains("patrolhelicopter")){ 
                    if (!heliEvent){ NextTick(() =>{ if (entity != null) entity.Kill(); });
                    } else { NotifyEvents("blue", "patrol", Languaje("ServerEvents", null), Languaje("Event_PatrolHelicopter", null)); }
                }
                if (entity.ShortPrefabName.Contains("cargo_plane")){ 
                    if (!supplyEvent){ NextTick(() =>{ if (entity != null) entity.Kill(); });
                    } else { NotifyEvents("blue", "airdrop", Languaje("ServerEvents", null), Languaje("Event_Airdrop", null)); }
                }
                if (entity.ShortPrefabName.Contains("cargoshiptest")){ 
                    if (!supplyEvent){ NextTick(() =>{ if (entity != null) entity.Kill(); });
                    } else { NotifyEvents("blue", "cargoship", Languaje("ServerEvents", null), Languaje("Event_Cargoship", null)); }
                }
                if (entity.ShortPrefabName.Contains("bradleyapc")){ 
                    if (!supplyEvent){ NextTick(() =>{ if (entity != null) entity.Kill(); });
                    } else { NotifyEvents("blue", "bradley", Languaje("ServerEvents", null), Languaje("Event_Bradley", null)); }
                }
                if (entity.ShortPrefabName.Contains("keycard") && entity.ShortPrefabName.Contains("_pickup.entity")){
                    if (!listCards.Contains(entity.ShortPrefabName)){
                        NextTick(() =>{ if (entity != null) entity.Kill(); });
                    }
                }
                if (entity.ShortPrefabName.Contains("module_car_spawned")){
                    if (!listVehicles.Contains(entity.ShortPrefabName)){
                        NextTick(() =>{ if (entity != null) entity.Kill(); });
                    }
                }

                if (entity.ShortPrefabName.Contains("workbench") && !allPlaceWorkbench){
                	//Puts(entity.ShortPrefabName);
                    bool destroy = true;
                    if (entity.ShortPrefabName == "workbench1.deployed"){
                        if (listWorkbench.Contains("assets/prefabs/deployable/tier 1 workbench/workbench1.deployed.prefab")) destroy = false;
                    } else if (entity.ShortPrefabName == "workbench2.deployed"){
                        if (listWorkbench.Contains("assets/prefabs/deployable/tier 2 workbench/workbench2.deployed.prefab")) destroy = false;
                    } else if (entity.ShortPrefabName == "workbench3.deployed"){ 
                        if (listWorkbench.Contains("assets/prefabs/deployable/tier 3 workbench/workbench3.deployed.prefab")) destroy = false;
                    }
                    NextTick(() =>{ if (entity != null && destroy) entity.Kill(); });
                }
            }
        }

        private void OnEntityKill(BaseNetworkable entity){
            if (entity != null){
                if (entity.ShortPrefabName.Contains("cupboard.tool")){
                    BaseEntity cup = (BaseEntity)(entity as BaseEntity);
                    if (!cup) return;
                    var cupCount = 0;
                    _Data.armariosCount.TryGetValue(cup.OwnerID, out cupCount);
                    if (cupCount != 0) _Data.armariosCount[cup.OwnerID] = cupCount - 1;
                }
            }
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo){
            if (entity != null){
                if (entity.ShortPrefabName.Contains("cupboard.tool") && enabledAlertDestrTC){
                    if (hitInfo == null || hitInfo.Initiator == null) return;
                    BasePlayer playerDestroyer = hitInfo?.Initiator as BasePlayer;
                    if (entity.OwnerID == playerDestroyer.userID) return;
                    RelationshipManager.PlayerTeam team = RelationshipManager.ServerInstance.FindTeam(playerDestroyer.currentTeam); 
                    if (team != null){
                        foreach (ulong memberID in team.members){
                            if(memberID == entity.OwnerID) return;
                        }
                    }
                    var message = Languaje("CupboardDestInfo", null);
                    if (alertDestrName){ message = Languaje("CupboardDestInfoPlayer", null, playerDestroyer.displayName); }
                    if (alertDestrPos){ message = Languaje("CupboardDestInfoPos", null, GridPos(entity.transform.position)); }
                    if (alertDestrName && alertDestrPos){
                        message = Languaje("CupboardDestInfoPosPlayer", null, GridPos(entity.transform.position), playerDestroyer.displayName);
                    }
                    SendAlert(Languaje("CupboardDestroyed", null), message, "#BF2E11", "cupboard.tool", true);
                }
            }
        }

        private bool? CanUnlockTechTreeNode(BasePlayer player, TechTreeData.NodeInstance node, TechTreeData techTree){
            if (useWorkbench || HasPermission(player.UserIDString, permByPassBlockResearch)){ return null;
            } else {
                Notify(player, "red", "researchpaper", Languaje("ResearchBlock", player.UserIDString), Languaje("NoResearchWorkbench", player.UserIDString));
                return false;
            }
        }

        private bool? CanUnlockTechTreeNodePath(BasePlayer player, TechTreeData.NodeInstance node, TechTreeData techTree){
            if (useWorkbench || HasPermission(player.UserIDString, permByPassBlockResearch)){ return null; } else { return false; }
        }

        private bool? CanUseVending(BasePlayer player, VendingMachine machine){
            if (!sellersVending){ 
                if(machine.GetComponent<NPCVendingMachine>()){
                    if(machine.shopName == "Stables Shopkeeper" && sellersBoat) return true;
                    if(machine.shopName == "Boat Vendor" && sellersHorse) return true;
                    Notify(player, "red", "vending.machine", Languaje("BlockVending", player.UserIDString), Languaje("VendingBlock", player.UserIDString));
                }
            }
            return true;
        }

        private bool? OnNpcConversationStart(VehicleVendor vendor, BasePlayer player, ConversationData conversationData){
            if (conversationData.shortname == "airwolf_heli_vendor" && !sellersHeli && !HasPermission(player.UserIDString, permByPassBlockHelis)){
                Notify(player, "red", "scrapheli", Languaje("BlockVendor", player.UserIDString), Languaje("VendorDesHeli", player.UserIDString));
                return false;
            }
            return null;
        }

        private object OnNpcConversationRespond(NPCTalking npcTalking, BasePlayer player, ConversationData conversationData, ConversationData.ResponseNode responseNode){
            //Puts(responseNode.responseText + " - " + conversationData.shortname);
            if (conversationData.shortname == "airwolf_heli_vendor" && !sellersHeli && responseNode.responseText == "I'd like to buy a helicopter" && !HasPermission(player.UserIDString, permByPassBlockHelis)){
                npcTalking.ForceEndConversation(player);
                Notify(player, "red", "scrapheli", Languaje("BlockVendor", player.UserIDString), Languaje("VendorDesHeli", player.UserIDString));
                return false;
            }

            if (conversationData.shortname == "boatvendor" && !sellersBoat && responseNode.responseText == "I'm looking to buy a boat" && !HasPermission(player.UserIDString, permByPassBlockBoat)){
                npcTalking.ForceEndConversation(player);
                Notify(player, "red", "rowboat", Languaje("BlockVendor", player.UserIDString), Languaje("VendorDesBoat", player.UserIDString));
                return false;
            }

            if (conversationData.shortname == "stablesvendor" && !sellersHorse && responseNode.responseText == "I'd like to buy a horse" && !HasPermission(player.UserIDString, permByPassBlockHorses)){
                npcTalking.ForceEndConversation(player);
                Notify(player, "red", "horse", Languaje("BlockVendor", player.UserIDString), Languaje("VendorDesHorse", player.UserIDString));
                return false;
            }
            return null;
        }

        private bool CanChangeGrade(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade){
            if(HasPermission(player.UserIDString, permByPassUpgrade)) return true;
            if (grade == BuildingGrade.Enum.Wood && maxUpgrade > 0){ return true; }
            if (grade == BuildingGrade.Enum.Stone && maxUpgrade > 1){ return true; }
            if (grade == BuildingGrade.Enum.Metal && maxUpgrade > 2){ return true; }
            if (grade == BuildingGrade.Enum.TopTier && maxUpgrade > 3){ return true; }
            Notify(player, "blue", "hammer", Languaje("BlockUpgrade", player.UserIDString), Languaje("UpgradeDes", player.UserIDString, Languaje(grade.ToString(), player.UserIDString)), "Hud");
            return false;
        }

        private object CanMountEntity(BasePlayer player, BaseMountable entity){
            if (entity && !enabledMLRS && entity.GetComponent<MLRS>() && !HasPermission(player.UserIDString, permByPassUseMLRS)){
                Notify(player, "red", "mlrs", Languaje("BlockVehicle", player.UserIDString), Languaje("MlrsDes", player.UserIDString), "Hud");
                return false;
            }

            if (entity && !enabledWORKCART && entity.ShortPrefabName == "workcartdriver" && !HasPermission(player.UserIDString, permByPassUseWorkcart)){
                Notify(player, "red", "workcart", Languaje("BlockVehicle", player.UserIDString), Languaje("TrainDes", player.UserIDString), "Hud");
                return false;
            }

            if (entity && !enabledTRAIN && entity.ShortPrefabName == "locomotivedriver" && !HasPermission(player.UserIDString, permByPassUseLocomotive)){
                Notify(player, "red", "locomotive", Languaje("BlockVehicle", player.UserIDString), Languaje("TrainDes", player.UserIDString), "Hud");
                return false;
            }
            return null;
        }

        private void OnItemResearch(ResearchTable table, Item targetItem, BasePlayer player){
            if (HasPermission(player.UserIDString, permByPassInstantResearch)){
                table.researchDuration = 0.0f;
            } else {
                if (researchDuration != 10.0f) table.researchDuration = researchDuration;
            }
        }

        private void OnRecyclerToggle(Recycler recycler, BasePlayer player) {
			if (recycler.IsOn()) return;
            recycler.CancelInvoke(nameof(recycler.RecycleThink));
            if (HasPermission(player.UserIDString, permByPassInstantRecycler)) {
                timer.Once(0.1f, () => recycler.InvokeRepeating(recycler.RecycleThink, 0.0f, 0.0f));
            } else {
                timer.Once(0.1f, () => recycler.InvokeRepeating(recycler.RecycleThink, recyclerDuration - 0.1f, recyclerDuration));
            }
		}
	
        #region GatherSystem
		private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item){			
			GatherMultiplier(item, multResource);						
        }
		private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item) {
			GatherMultiplier(item, multResourceBonus);	
		}		
		private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player){
            foreach(ItemAmount item in collectible.itemList){
				item.amount = (int)(item.amount * multPickup);
            }
		}
		private void OnGrowableGathered(GrowableEntity plant, Item item, BasePlayer player){
			GatherMultiplier(item, multGrowable);
		}
		private void OnSurveyGather(SurveyCharge surveyCharge, Item item){
            GatherMultiplier(item, multSurvey);
        }
		private void OnQuarryGather(MiningQuarry quarry, Item item){
            GatherMultiplier(item, multQuarry);
        }
        private void OnExcavatorGather(ExcavatorArm excavator, Item item){
			GatherMultiplier(item, multExcavator);
		}
		#endregion

        #region BlockItems
        private object CanEquipItem(PlayerInventory inventory, Item item, int target){
            return CanWearItem(inventory, item, target);
        }
        
        private object OnWeaponReload(BaseProjectile projectile, BasePlayer player){
            return OnMagazineReload(projectile, -1, player);
        }

        private object CanWearItem(PlayerInventory inventory, Item item, int target){
            if (HasPermission(inventory.GetComponent<BasePlayer>().UserIDString, permByPassBlockItems)) return null;
            var result = CanUseItem(inventory.GetComponent<BasePlayer>(), item.info.shortname);
            return result ? (object) null : false;
        }
        
        private object OnMagazineReload(BaseProjectile projectile, int desiredAmount, BasePlayer player){
            if (HasPermission(player.UserIDString, permByPassBlockItems)) return null;
            
            if (projectile.primaryMagazine.definition.ammoTypes == AmmoTypes.RIFLE_556MM){
                NextTick(()=> {CheckGun(player, projectile);});
            }

            var result = CanUseItem(player, projectile.primaryMagazine.ammoType.shortname);
            return result ? (object) null : true;
        }

        private object OnItemAction(Item item, string action, BasePlayer player){
            if(action == null) return null;
            if(action == "consume") {
                var result = CanUseItem(player, item.info.shortname);
                return result ? (object) null : false;
            }
            return null;
        }

		ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {

            if (container == null || container.playerOwner == null)
                return null;

            BasePlayer player = container.playerOwner;


            bool hasLargeBackpackEquipped = player.inventory.containerWear.itemList.Any(wearable => wearable.info.shortname == "largebackpack");


            if (hasLargeBackpackEquipped && container.entityOwner != null && container.entityOwner.ShortPrefabName == "largebackpack")
                return ItemContainer.CanAcceptResult.CannotAccept;


            Item parentItem = container.parent;
            if (parentItem != null)
            {
                BasePlayer parentPlayer = parentItem.GetOwnerPlayer() ?? item.GetOwnerPlayer();
                if (parentPlayer == null)
                    return null;


                var result = CanUseItem(parentPlayer, item.info.shortname);
                return result ? (ItemContainer.CanAcceptResult?)null : ItemContainer.CanAcceptResult.CannotAccept;
            }

            return null;
        }

        private object CanCraft(PlayerBlueprints blueprints, ItemDefinition definition, int skinId) {
            if(!blockCraft) return null;
            BasePlayer player = blueprints.GetComponentInParent<BasePlayer>();
            if (HasPermission(player.UserIDString, permByPassBlockItems)) return null;
            var result = CanUseItem(player, definition.shortname);
            return result ? (object) null : false;
        }
        #endregion

        #endregion

        #region Comandos
        private void cmdSelectTier(ConsoleSystem.Arg arg){
            if (arg.IsAdmin == false){
                SendReply(arg, "You don't have access to that command!");
                return;
            }
            
            var args = arg.Args;
            if (args == null || args?.Length == 0){ 
                SendReply(arg, "Usage: tier.select 0-3");
                return;
            }
            int newtier = Int16.Parse(args[0]);
            SelectTier(tierCurrent, newtier);
        }

        private void cmdOpenInfo(ConsoleSystem.Arg arg){
            OpenPanel(arg.Player(), tierCurrent);
        }

        [ChatCommand("tier")]
        void cmdChatTier(BasePlayer player, string command, string[] args){
            if (args.Length == 0){
                if(config.GUI.WINDOWS.showWindowsCommand) OpenPanel(player, tierCurrent, 0);
            } else {
                if (HasPermission(player.UserIDString, permissionAdmin)){
                    switch (args[0]){
                        case "edit":
                            if (args?.Length != 2){ 
                                EditTier(player, tierCurrent);
                                return;
                            }
                            int tiercount = Int16.Parse(args[1])-1;
                            if (tiercountAll > tiercount){
                                EditTier(player, tiercount);
                                return;
                            } else {
                                SendReply(player, "The specified Tier does not exist make sure to enter a number from 1 to " + tiercountAll);
                            }
                            return;
                    }
                } else {
                    PrintToChat(player, Languaje("NotAllowed", player.UserIDString));
                }
            }
        }

        [ChatCommand("tieralert")]
        void cmdChatTierAlert(BasePlayer player, string command, string[] args){
            if (HasPermission(player.UserIDString, permissionAdmin)){
                if (args.Length == 0){
                    PrintToChat(player, Languaje("Unspecified", player.UserIDString));
                } else {
                    string message = string.Join(" ", args.Skip(0).ToArray());
                    SendAlert(Languaje("TierAlertTitle", null), message, null, null, true);
                }
            } else {
                PrintToChat(player, Languaje("NotAllowed", player.UserIDString));
            }
        }

        private void cmdEditTier(ConsoleSystem.Arg arg){
            if (arg.IsAdmin == false){
                SendReply(arg, "You don't have access to that command!");
                return;
            }
            
            NotifyChangue(0, 1);
            var args = arg.Args;
            if (args == null || args?.Length == 0){ 
                SendReply(arg, "Usage: tier.edit 0-3");
                return;
            }
            int newtier = Int16.Parse(args[0]);
            //SelectTier(tierCurrent, newtier);
        }
        #endregion

        #region Functions
        private void SelectTier(int old_tier, int new_tier){
            Puts("Applying Tier ("+new_tier+"): " + config.TIERS[new_tier].name + " - Previous Tier ("+old_tier+")");
            
            screenCounterIcon = config.GUI.COUNTER.screenCounterIcon;
            screenCounterButton = config.GUI.COUNTER.screenCounterButton;
            tierName = config.TIERS[new_tier].name;
            tierIcon = config.TIERS[new_tier].icon;
            tierColor = config.TIERS[new_tier].color;
            enabledMaxCUP = config.TIERS[new_tier].cupboards.limitCupboard;
            limitCupboard = config.TIERS[new_tier].cupboards.maxCupboards;
            enabledAlertDestrTC = config.TIERS[new_tier].cupboards.enabledAlert;
            alertDestrName = config.TIERS[new_tier].cupboards.ALERT.name;
            alertDestrPos = config.TIERS[new_tier].cupboards.ALERT.pos;
            maxUpgrade = config.TIERS[new_tier].cupboards.maxGrade;
            useWorkbench = config.TIERS[new_tier].workbench.researchWorkbench;
            allPlaceWorkbench = config.TIERS[new_tier].workbench.placeAllWorkbench;
            listWorkbench = config.TIERS[new_tier].workbench.onlyWorkbench;
            listCards = config.TIERS[new_tier].spawns.spawnCards;
            listVehicles = config.TIERS[new_tier].spawns.spawnVehicles;
            enabledMLRS = config.TIERS[new_tier].spawns.spawnMLRS;
            enabledWORKCART = config.TIERS[new_tier].spawns.spawnWORKCART;
            enabledTRAIN = config.TIERS[new_tier].spawns.spawnTRAIN;

            cargoEvent = config.TIERS[new_tier].events.cargoship;
            oilEvent = config.TIERS[new_tier].events.oilrig;
            ch47Event = config.TIERS[new_tier].events.chinook;
            heliEvent = config.TIERS[new_tier].events.helicopter;
            supplyEvent = config.TIERS[new_tier].events.supplysignal;
            bradleyEvent = config.TIERS[new_tier].events.bradley;

            sellersHeli = config.TIERS[new_tier].sellers.helicopter;
            sellersBoat = config.TIERS[new_tier].sellers.boat;
            sellersHorse = config.TIERS[new_tier].sellers.horse;
            sellersDrones = config.TIERS[new_tier].sellers.drones;
            sellersVending = config.TIERS[new_tier].sellers.vending;

            craftingDuration = config.TIERS[new_tier].speed.craftingSpeed;
            researchDuration = config.TIERS[new_tier].speed.researchSpeed;
            recyclerDuration = config.TIERS[new_tier].speed.recyclerSpeed;
            gatherEnabled = config.TIERS[new_tier].speed.gatherSpeed;	
            multResource = config.TIERS[new_tier].speed.gatherConfig.Resource;					
            multResourceBonus = config.TIERS[new_tier].speed.gatherConfig.ResourceBonus;
            multPickup = config.TIERS[new_tier].speed.gatherConfig.Pickup;
            multGrowable = config.TIERS[new_tier].speed.gatherConfig.Growable;
            multSurvey = config.TIERS[new_tier].speed.gatherConfig.Survey;
            multQuarry = config.TIERS[new_tier].speed.gatherConfig.Quarry;
            multExcavator = config.TIERS[new_tier].speed.gatherConfig.Excavator;

            blockCraft = config.TIERS[new_tier].lists.itemsBlockCraft;
            blockDeployables = config.TIERS[new_tier].lists.itemsBlockDeployables;
            listCommand = config.TIERS[new_tier].lists.commands;

            minNextTier = 0;
            int count = 0;
            foreach (var tier in config.TIERS){
                if(new_tier >= count){
                    minNextTier += config.TIERS[count].durationMinutes;
                }
                count++;
            }

            if (listVehicles.Count == 0){ 
                Puts("Vehicles disabled in this Tier, disabling car population (Better performance).");
                Server.Command("modularcar.population 0"); 
            } else {
                Puts("There is some kind of car set to Spawn, resetting population.");
                Server.Command("modularcar.population 3"); 
            }

            if(config.SERVERTITLE.titleEnabled){ 
                ConVar.Server.hostname = string.Format(config.SERVERTITLE.titleText, tierName);
            }

            tierCurrent = new_tier;
            tiercountAll = config.TIERS.Count();
            _Data.activeTier = new_tier;

            UpdateEvents();
            UpdateVending();
            CheckExistMarketplace();
            CheckExistVehicles();
            ItemsBlock();
            SetDurationCraft();
            UpdateHooks();
            ExecuteCommands();
            SaveData();
            ControlTime();
            ClearEntities();
        }

        private void InfoData(){
            int count = 0;
            foreach (var tier in config.TIERS){
                itemType.Add(count, new Dictionary<string, List<SectionInfo>>{
                    {
                        "Building",
                        new List<SectionInfo>{
                            new SectionInfo { img = "cupboard.tool", title = "CupboardsMax", text = tier.cupboards.limitCupboard ? "Max: " + tier.cupboards.maxCupboards : "Unlimited" },
                            new SectionInfo { img = "hammer", title = "MaxUpgrade", text = Grade(tier.cupboards.maxGrade) }
                        }
                    },
                    {
                        "Workbench",
                        new List<SectionInfo>{
                            new SectionInfo { img = "blueprintbase", title = "ResearchWorkbench", text = tier.workbench.researchWorkbench ? "Enabled" : "Disabled" },
                            new SectionInfo { img = Workbench(tier.workbench), title = "MaxWorkbenchLevel", text = Workbench(tier.workbench) }
                        }
                    },
                    {
                        "Events",
                        new List<SectionInfo>{
                            new SectionInfo { img = "cargoship", title = "Cargoship", text = tier.events.cargoship ? "Enabled" : "Disabled" },
                            new SectionInfo { img = "hackable", title = "OilRig", text = tier.events.oilrig ? "Enabled" : "Disabled" },
                            new SectionInfo { img = "ch47", title = "Chinook", text = tier.events.chinook ? "Enabled" : "Disabled" },
                            new SectionInfo { img = "patrol", title = "PatrolHelicopter", text = tier.events.helicopter ? "Enabled" : "Disabled" },
                            new SectionInfo { img = "airdrop", title = "Airdrop", text = tier.events.supplysignal ? "Enabled" : "Disabled" },
                            new SectionInfo { img = "bradley", title = "BradleyAPC", text = tier.events.bradley ? "Enabled" : "Disabled" }
                        }
                    },
                    {
                        "Vending",
                        new List<SectionInfo>{
                            new SectionInfo { img = "scrapheli", title = "HelicopterSale", text = tier.sellers.helicopter ? "Enabled" : "Disabled" },
                            new SectionInfo { img = "rowboat", title = "BoatSales", text = tier.sellers.boat ? "Enabled" : "Disabled" },
                            new SectionInfo { img = "horse", title = "HorseSales", text = tier.sellers.horse ? "Enabled" : "Disabled" },
                            new SectionInfo { img = "drone2", title = "UseofDrones", text = tier.sellers.drones ? "Enabled" : "Disabled" },
                            new SectionInfo { img = "vending.machine", title = "UseofVending", text = tier.sellers.vending ? "Enabled" : "Disabled" }
                        }
                    },
                    {
                        "Spawn",
                        new List<SectionInfo>{
                            new SectionInfo { img = KeyCard(tier.spawns.spawnCards), title = "SpawnCards", text = KeyCard(tier.spawns.spawnCards) },
                            new SectionInfo { img = Vehicles(tier.spawns.spawnVehicles, true), title = "SpawnVehicles", text = Vehicles(tier.spawns.spawnVehicles) },
                            new SectionInfo { img = "mlrs", title = "UseVehicleMLRS", text = tier.spawns.spawnMLRS ? "Enabled" : "Disabled" },
                            new SectionInfo { img = "workcart", title = "UseVehicleWORKCART", text = tier.spawns.spawnWORKCART ? "Enabled" : "Disabled" },
                            new SectionInfo { img = "locomotive", title = "UseVehicleTRAIN", text = tier.spawns.spawnTRAIN ? "Enabled" : "Disabled" }
                        }
                    },
                    {
                        "Speed",
                        new List<SectionInfo>{
                            new SectionInfo { img = "building.planner", title = "CraftingRate", text = tier.speed.craftingSpeed + " (1.0 = Vanilla | 0.0 = Instant)." },
                            new SectionInfo { img = "research.table", title = "ResearchSeconds", text = tier.speed.researchSpeed + " seconds." },
                            new SectionInfo { img = "gears", title = "RecyclerSeconds", text = tier.speed.recyclerSpeed + " seconds."}
                        }.Concat(
                            tier.speed.gatherSpeed ? new[] { new SectionInfo { img = "pickaxe", title = "GatherSystem", text = "x" + tier.speed.gatherConfig.ResourceBonus } } : Enumerable.Empty<SectionInfo>()
                        ).ToList()
                    }
                });
                count++;
            }
        }

        private string Grade(int grade, bool name = false){
            switch (grade){
                case 1:
                    return name ? "wood" : "Wood";
                case 2:
                    return name ? "stones" :"Stone";
                case 3:
                    return name ? "metal.fragments" :"Metal";
                case 4:
                    return name ? "metal.refined" :"TopTier";
                default:
                    return name ? "metal.refined" :"TopTier";
            }
        }

        private string Workbench(WorkbenchConfig tier){
            if(tier.placeAllWorkbench) return "workbench3";
            if(tier.onlyWorkbench.Contains("assets/prefabs/deployable/tier 3 workbench/workbench3.deployed.prefab")) return "workbench3";
            if(tier.onlyWorkbench.Contains("assets/prefabs/deployable/tier 2 workbench/workbench2.deployed.prefab")) return "workbench2";
            return "workbench1";
        }

        private string KeyCard(List<string> list){
            if(list.Contains("keycard_red_pickup.entity")) return "keycard_red";
            if(list.Contains("keycard_blue_pickup.entity")) return "keycard_blue";
            return "keycard_green";
        }

        private string Vehicles(List<string> list, bool name = false){
            if(list.Contains("4module_car_spawned.entity")) return name ? "vehicle.chassis.4mod" : "4module_car_spawned";
            if(list.Contains("3module_car_spawned.entity")) return name ? "vehicle.chassis.3mod" : "3module_car_spawned";
            if(list.Contains("2module_car_spawned.entity")) return name ? "vehicle.chassis.2mod" : "2module_car_spawned";
            return "nocars";
        }

        private void ControlTime(){
            if (config.GUI.screenCounter){
                foreach (var player in BasePlayer.activePlayerList){
                    if (player == null) return;
                    Countdown(player);
                }
            }
            TierTimer();
            SpeedTimer();
        }

        private void SpeedTimer(){
            cuiTimer = timer.Every(refreshTime, () => { TierTimer(); });
        }

        private void TierTimer(){
            if (ShouldDestroy(wipeTime, minNextTier)){
                cuiTimer?.Destroy();
                int tiercount = tierCurrent + 1;
                if ((tiercountAll-1) >= tiercount){
                    NotifyChangue(tierCurrent, tiercount);
                    SelectTier(tierCurrent, tiercount);
                    return;
                } else {
                    counterEnabled = false;
                    foreach (var player in BasePlayer.activePlayerList){
                        if (player == null) return;
                        CuiHelper.DestroyUi(player, elemq1);
                        CuiHelper.DestroyUi(player, elemq2);
                    }
                    return;
                }
            }

            var velocity = FormatTimeSpeed(wipeTime.AddMinutes(minNextTier) - DateTime.UtcNow);
            if(velocity != refreshTime){
                cuiTimer?.Destroy();
                refreshTime = velocity;
                SpeedTimer();
            }

            if (config.GUI.screenCounter){
                containerTime = UpdateClock(wipeTime, minNextTier);
                foreach (var player in BasePlayer.activePlayerList){
                    if (player == null) return;
                    CuiHelper.DestroyUi(player, elemq2);
                    CuiHelper.AddUi(player, containerTime);
                }
            }
        }

        private void NotifyInit(){
            string namenew = config.TIERS[0].name;
  
            if (DiscordMessages != null && config.MSG.discordEnabled){
                DiscordMessages?.Call("API_SendTextMessage", config.MSG.discordWebhook, Languaje("InitTierDiscord", null, namenew));
            }
            if (config.MSG.chatEnabled) PrintToChat(Languaje("InitTier", null, namenew));
        }

        private void NotifyChangue(int tierold, int tierCurrent){
            string nameold = config.TIERS[tierold].name;
            string colorold = config.TIERS[tierold].color;
            string namenew = config.TIERS[tierCurrent].name;
            string colornew = config.TIERS[tierCurrent].color;

            if (DiscordMessages != null && config.MSG.discordEnabled){
                DiscordMessages?.Call("API_SendTextMessage", config.MSG.discordWebhook, Languaje("ChangueTierDiscord", null, nameold, namenew));
            }

           if (config.MSG.chatEnabled) PrintToChat(Languaje("ChangueTier", null, nameold, namenew, ColorToHex(colorold), ColorToHex(colornew)));

            if (config.GUI.modalChangueEnabled){
                var container = Notify_Changue(tierold, tierCurrent);
                foreach (var player in BasePlayer.activePlayerList){
                    if (player == null) return;
                    if (HasPermission(player.UserIDString, permByPassAlertsBlock)) return;
                    if (config.GUI.MODALCHANGUE.soundEnabled) Effect.server.Run(config.GUI.MODALCHANGUE.prefabSound, player.transform.position, Vector3.up, null, true);
                    CuiHelper.DestroyUi(player, elemq7);
                    CuiHelper.AddUi(player, container);
                    timer.Once(config.GUI.MODALCHANGUE.duration, () => CuiHelper.DestroyUi(player, elemq7));
                }
            }
        }

        private bool ShouldDestroy(DateTime time, float minutes){
            if (time.AddMinutes(minutes) <= DateTime.UtcNow || FormatTime((time.AddMinutes(minutes) - DateTime.UtcNow).TotalSeconds) == null){
                return true;
            }
            return false;
        }

        private void UpdateEvents(){
            if (cargoEvent){ Server.Command($"cargoship.event_enabled True"); } else { Server.Command($"cargoship.event_enabled False"); }
            if (bradleyEvent){ Server.Command($"bradley.enabled True"); } else { Server.Command($"bradley.enabled False"); }
        }

        private void UpdateHooks(){
            if (!counterEnabled && !config.MSG.chatWelcomeEnabled){ Unsubscribe("OnPlayerConnected"); } else { Subscribe("OnPlayerConnected"); }
            if (!enabledMaxCUP){ Unsubscribe("OnEntityKill"); } else { Subscribe("OnEntityKill"); }
            if (!enabledAlertDestrTC){ Unsubscribe("OnEntityDeath"); } else { Subscribe("OnEntityDeath"); }

            if (useWorkbench){ Unsubscribe("CanUnlockTechTreeNode"); Unsubscribe("CanUnlockTechTreeNodePath"); } else { Subscribe("CanUnlockTechTreeNode"); Subscribe("CanUnlockTechTreeNodePath"); }
            if (sellersHeli){ Unsubscribe("OnNpcConversationStart"); } else { Subscribe("OnNpcConversationStart"); }
            if (sellersHeli && sellersBoat && sellersHorse){ Unsubscribe("OnNpcConversationRespond"); } else { Subscribe("OnNpcConversationRespond"); }
            if (maxUpgrade == 4){ Unsubscribe("CanChangeGrade"); } else { Subscribe("CanChangeGrade"); }
            if (enabledMLRS && enabledWORKCART && enabledTRAIN){ Unsubscribe("CanMountEntity"); } else { Subscribe("CanMountEntity"); }
            if (sellersVending){ Unsubscribe("CanUseVending"); } else { Subscribe("CanUseVending"); }
            if (researchDuration == 10.0f){ Unsubscribe("OnItemResearch"); } else { Subscribe("OnItemResearch"); }
            if (recyclerDuration == 5.0f){ Unsubscribe("OnRecyclerToggle"); } else { Subscribe("OnRecyclerToggle"); }

            if (!gatherEnabled || multResource == 0.0f){ Unsubscribe("OnDispenserGather"); } else { Subscribe("OnDispenserGather"); }
            if (!gatherEnabled || multResourceBonus == 0.0f){ Unsubscribe("OnDispenserBonus"); } else { Subscribe("OnDispenserBonus"); }
            if (!gatherEnabled || multPickup == 0.0f){ Unsubscribe("OnCollectiblePickup"); } else { Subscribe("OnCollectiblePickup"); }
            if (!gatherEnabled || multGrowable == 0.0f){ Unsubscribe("OnGrowableGathered"); } else { Subscribe("OnGrowableGathered"); }
            if (!gatherEnabled || multSurvey == 0.0f){ Unsubscribe("OnSurveyGather"); } else { Subscribe("OnSurveyGather"); }
            if (!gatherEnabled || multQuarry == 0.0f){ Unsubscribe("OnQuarryGather"); } else { Subscribe("OnQuarryGather"); }
            if (!gatherEnabled || multExcavator == 0.0f){ Unsubscribe("OnExcavatorGather"); } else { Subscribe("OnExcavatorGather"); }

            if (blackItems.Count == 0){ Unsubscribe("CanEquipItem"); } else { Subscribe("CanEquipItem"); }
            if (blackItems.Count == 0){ Unsubscribe("OnWeaponReload"); } else { Subscribe("OnWeaponReload"); }
            if (blackItems.Count == 0){ Unsubscribe("OnMagazineReload"); } else { Subscribe("OnMagazineReload"); }
            if (blackItems.Count == 0){ Unsubscribe("CanCraft"); } else { Subscribe("CanCraft"); }
            Puts("Updated Hook Subscriptions.");
        }

        private void UpdateVending(){
            foreach (var entity in BaseNetworkable.serverEntities.OfType<NPCVendingMachine>()){
                if (entity == null) { continue; }
                if (sellersVending){
                    entity.SetFlag(global::BaseEntity.Flags.Reserved2, false, false, true);
                } else { 
                    if(entity.shopName == "Stables Shopkeeper" && sellersBoat) continue;
                    if(entity.shopName == "Boat Vendor" && sellersHorse) continue;
                    entity.SetFlag(global::BaseEntity.Flags.Reserved2, true, false, true);
                }
                entity.SendNetworkUpdateImmediate();
            }
        }

        private void ClearEntities(){
            if (!allPlaceWorkbench){
                foreach (var entity in BaseNetworkable.serverEntities.OfType<Workbench>()){
                    if (entity == null) { continue; }
                    //Puts(entity.ShortPrefabName);
                    bool destroy = true;
                    if (entity.ShortPrefabName == "workbench1.deployed"){
                        if (listWorkbench.Contains("assets/prefabs/deployable/tier 1 workbench/workbench1.deployed.prefab")) destroy = false;
                    } else if (entity.ShortPrefabName == "workbench2.deployed"){
                        if (listWorkbench.Contains("assets/prefabs/deployable/tier 2 workbench/workbench2.deployed.prefab")) destroy = false;
                    } else if (entity.ShortPrefabName == "workbench3.deployed"){ 
                        if (listWorkbench.Contains("assets/prefabs/deployable/tier 3 workbench/workbench3.deployed.prefab")) destroy = false;
                    }
                    NextTick(() =>{ if (entity != null && destroy) entity.Kill(); });
                }
            }
        }

        private void CheckExistMarketplace(){
            foreach (var entity in BaseNetworkable.serverEntities.OfType<Marketplace>()){
                if (entity == null) { continue; }
                Vector3 pos = entity.ServerPosition;
                var grid = GridPos(pos);
                MarketPlaceData outRotation;
                if (!(_Data.marketplace.TryGetValue(grid, out outRotation))){
                    _Data.marketplace[grid] = new MarketPlaceData {
                        Position = pos,
                        Rotation = entity.transform.rotation.eulerAngles
                    };
                }
            }

            if (_Data.marketplace != null && _Data.marketplace.Any()){
                foreach (var market in _Data.marketplace.Values){
                    checkMarketExist(market.Position, market.Rotation, sellersDrones);
                }
            }
        }

        private void CheckExistVehicles(){
            foreach (var entity in BaseNetworkable.serverEntities.OfType<Marketplace>()){
                if (entity == null) { continue; }
                if (!listVehicles.Contains(entity.ShortPrefabName)){
                    NextTick(() =>{ if (entity != null) entity.Kill(); });
                }
            }
        }

        private void checkMarketExist(Vector3 position, Vector3 rotation, bool sellersDrones){
            bool existMarket = false;
            var nearby = Pool.GetList<BaseEntity>();
            Vis.Entities<BaseEntity>(position, 1.0f, nearby);
            foreach (BaseEntity item in nearby.Distinct().ToList()){
                if (item.name == "Marketplace" || item.name == prefabMarketplace){
                    if (!sellersDrones) item?.Kill();
                    existMarket = true; 
                }
            }
            NextTick(() =>{
                if (!existMarket && sellersDrones){
                    PrintWarning("Spawming Marketplace in your position.");
                    BaseEntity market = GameManager.server.CreateEntity(prefabMarketplace, position, Quaternion.Euler(rotation)) as BaseEntity;
                    if (market == null) return;
                    market.Spawn();
                    market.SendNetworkUpdateImmediate();
                }
            });
            Pool.FreeList(ref nearby);
        }

        private void ExecuteCommands(){
            foreach (var cmd in listCommand){
                Server.Command(cmd);
            }
        }

        private bool HasPermission(string userID, string perm){
            return string.IsNullOrEmpty(perm) || permission.UserHasPermission(userID, perm);
        }

        private string GridPos(Vector3 pos){
            char letter = 'A';
            var x = Mathf.Floor((pos.x + (ConVar.Server.worldsize / 2)) / 146.3f) % 26;
            var count = Mathf.Floor(Mathf.Floor((pos.x + (ConVar.Server.worldsize / 2)) / 146.3f) / 26);
            var z = (Mathf.Floor(ConVar.Server.worldsize / 146.3f)) - Mathf.Floor((pos.z + (ConVar.Server.worldsize / 2)) / 146.3f);
            letter = (char)(letter + x);
            var secondLetter = count <= 0 ? string.Empty : ((char)('A' + (count - 1))).ToString();
            return $"{secondLetter}{letter}{z}";
        }

        private void GatherMultiplier(Item item, float mult){
            if (mult != 0.0f && gatherEnabled) item.amount = (int)(item.amount * mult);
        }

        private void ItemsBlock(){
            int count = 1;
            blackItems.Clear();
            blackItemsBuilding.Clear();
            foreach (var tier in config.TIERS){
                if(tierCurrent < count){
                    foreach (var item in tier.lists.items){
                        if(!blackItems.Contains(item)) blackItems.Add(item);
                    }
                    foreach (var item in tier.lists.itemsDeployables){
                        if(!blackItemsBuilding.ContainsKey(item.Key)){
                            blackItemsBuilding.Add(item.Key, item.Value);
                        } 
                    }
                }
                count++;
            }
        }

        private void SaveDefaultDurationCraft(){
            _Data.durationCraft.Clear();
            foreach (var bp in ItemManager.bpList){
                if (!_Data.durationCraft.ContainsKey(bp.name)) { _Data.durationCraft.Add(bp.name, bp.time); }
            }
            Puts("Default Crafting Duration stored in the Data.");
        }

        private void SetDurationCraft(){
            if (tierCurrent != 0){
                float oldSpeed = config.TIERS[tierCurrent-1].speed.craftingSpeed;
                if(oldSpeed == craftingDuration) return;
            } else {
                if(craftingDuration == 1.0f) return;
            }

            foreach (var bp in ItemManager.bpList){
                if (_Data.durationCraft.ContainsKey(bp.name)) { 
                    bp.time = (craftingDuration/1) * _Data.durationCraft[bp.name];
                }
            }
            Puts("Item Crafting Duration has been changed.");
        }

        private bool CanUseItem(BasePlayer player, string shortname){
            if (player.userID.IsSteamId() == false) return true;
            if (blackItems.Contains(shortname)){
                Notify(player, "red", shortname, Languaje("ItemBlock", player.UserIDString), Languaje("ItemBlockDes", player.UserIDString)); 
                return false;
            } 
            return true;
        }

        private void CheckGun(BasePlayer player, BaseProjectile weapon){
            var magazine = weapon.primaryMagazine;
            if (magazine.contents > 0 && blackItems.Contains(magazine.ammoType.shortname)){
                var item = player.inventory.AllItems().FirstOrDefault(x => x.GetHeldEntity() == weapon);
                if (item != null){
                    item._condition = 0f;
                    item._maxCondition = 0f;
                    item.MarkDirty();
                    magazine.contents = 0;
                    magazine.capacity = 0; 
                }
            }
        }

        private void LoadImages(){
            Dictionary<string, string> imageList = new Dictionary<string, string>();
            List<KeyValuePair<string, ulong>> itemIcons = new List<KeyValuePair<string, ulong>>();
            itemIcons.Add(new KeyValuePair<string, ulong>("cupboard.tool", 0));
            itemIcons.Add(new KeyValuePair<string, ulong>("wood", 0));
            itemIcons.Add(new KeyValuePair<string, ulong>("stones", 0));
            itemIcons.Add(new KeyValuePair<string, ulong>("metal.fragments", 0));
            itemIcons.Add(new KeyValuePair<string, ulong>("metal.refined", 0));
            itemIcons.Add(new KeyValuePair<string, ulong>("keycard_blue", 0));
            itemIcons.Add(new KeyValuePair<string, ulong>("keycard_green", 0));
            itemIcons.Add(new KeyValuePair<string, ulong>("keycard_red", 0));
            itemIcons.Add(new KeyValuePair<string, ulong>("workbench1", 0));
            itemIcons.Add(new KeyValuePair<string, ulong>("workbench2", 0));
            itemIcons.Add(new KeyValuePair<string, ulong>("workbench3", 0));
            itemIcons.Add(new KeyValuePair<string, ulong>("hammer", 0));
            itemIcons.Add(new KeyValuePair<string, ulong>("vending.machine", 0));
            itemIcons.Add(new KeyValuePair<string, ulong>("researchpaper", 0));
            imageList.Add("rowboat", "https://img.rustspain.com/tiers/rowboat.png");
            imageList.Add("minigun", "https://rustlabs.com/img/items180/minigun.png");
            imageList.Add("rocket.launcher.dragon", "https://rustlabs.com/img/skins/324/10236.png");
            imageList.Add("shotgun.m4", "https://rustlabs.com/img/items180/shotgun.m4.png");
            imageList.Add("rifle.sks", "https://i.ibb.co/sFtdkYt/png-clipart-assault-rifle-gun-barrel-shotgun-sks-assault-rifle-ak47-assault-rifle.png");
            imageList.Add("military flamethrower", "https://i.ibb.co/hcr9BY2/rust-military-flamethrower-300x300.png");
            imageList.Add("scrapheli", "https://img.rustspain.com/tiers/scrapheli.png");
            imageList.Add("horse", "https://img.rustspain.com/tiers/horse.png");
            imageList.Add("info3", "https://img.rustspain.com/tiers/info.png");
            imageList.Add("hackable", "https://img.rustspain.com/tiers/hackable.png");
            imageList.Add("ch47", "https://img.rustspain.com/tiers/ch47.png");
            imageList.Add("patrol", "https://img.rustspain.com/tiers/patrol.png");
            imageList.Add("airdrop", "https://img.rustspain.com/tiers/airdrop.png");
            imageList.Add("cargoship", "https://img.rustspain.com/tiers/cargoship.png");
            imageList.Add("bradley", "https://img.rustspain.com/tiers/bradley.png");
            imageList.Add("drone2", "https://img.rustspain.com/tiers/drone.png");
            imageList.Add("nocars", "https://img.rustspain.com/tiers/nocars.png");
            imageList.Add("mlrs", "https://img.rustspain.com/tiers/mlrs.png");

            foreach (var tier in config.TIERS){
                imageList.Add(tier.name, tier.icon);
                foreach (var itemName in tier.lists.items){
                    if(!itemIcons.Contains(new KeyValuePair<string, ulong>(itemName, 0))) itemIcons.Add(new KeyValuePair<string, ulong>(itemName, 0));
                }
                foreach (var itemName in tier.lists.itemsDeployables){
                    if(!itemIcons.Contains(new KeyValuePair<string, ulong>(itemName.Key, 0))) itemIcons.Add(new KeyValuePair<string, ulong>(itemName.Key, 0));
                }
            }

            if (itemIcons.Count > 0){
                ImageLibrary?.Call("LoadImageList", Title, itemIcons, null);
            }

            ImageLibrary?.Call("ImportImageList", Title, imageList, 0UL, true, new Action(PluginReady));
        }

        private void PluginReady(){
            _PluginReady = true;
        }

        private string GetImage(string name, ulong skinid = 0){ 
            return ImageLibrary?.Call<string>("GetImage", name, skinid); 
        }
        #endregion

        #region GUI
        private void Notify(BasePlayer player, string color, string img, string title, string description, string pos = "Overlay"){
            if (config.GUI.modalEnabled && !HasPermission(player.UserIDString, permByPassAlertsBlock)){
                CuiHelper.DestroyUi(player, elemq0);
                var container = new CuiElementContainer();
                container.Add(new CuiElement {
                    Name = elemq0,
                    Parent = pos,
                    Components = {
                        new CuiImageComponent {
                            FadeIn = 0.3f,
                            Color = "0 0 0 0.6",
                            Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                        },
                        new CuiRectTransformComponent {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = config.GUI.MODAL.minPosition,
                            OffsetMax = config.GUI.MODAL.maxPosition
                        }
                    }
                });
            
                UI.Panel(ref container, elemq0, GetColor(color), "0 0", "0.22 0.985");
                UI.Image(ref container, elemq0, GetImage(img), "0.042 0.15", "0.178 0.85");
                UI.Image(ref container, elemq0, GetImage("info3"), "0.93 0.62", "0.98 0.88");
                UI.Label(ref container, elemq0, title, 16, "0.26 0.55", "1.0 0.85", config.GUI.MODAL.colorTitle, TextAnchor.MiddleLeft, true);

                if (description.Length < 56){
                    UI.Label(ref container, elemq0, description, 12, "0.26 0.20", "1.0 0.50", config.GUI.MODAL.colorDescription, TextAnchor.MiddleLeft);
                } else {
                    UI.Label(ref container, elemq0, description, 11, "0.26 0.15", "1.0 0.55", config.GUI.MODAL.colorDescription, TextAnchor.MiddleLeft);
                }

                CuiHelper.AddUi(player, container);
                timer.Once(config.GUI.MODAL.duration, () => CuiHelper.DestroyUi(player, elemq0));

                if (config.GUI.MODAL.soundEnabled) Effect.server.Run(config.GUI.MODAL.prefabSound, player.transform.position, Vector3.up, null, true);
            }
        }

        [HookMethod("SendAlert")]
        private void SendAlert(string title, string description, string color, string img, bool sound = false, string pos = "Overlay"){
            if (config.GUI.modalCustomEnabled){
                if(color == null) color = "#159AC8";
                if(img == null) img = "info3";
                var container = new CuiElementContainer();
                container.Add(new CuiElement {
                    Name = elemq0,
                    Parent = pos,
                    Components = {
                        new CuiImageComponent {
                            FadeIn = 0.3f,
                            Color = "0 0 0 0.6",
                            Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                        },
                        new CuiRectTransformComponent {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = config.GUI.MODALCUSTOM.minPosition,
                            OffsetMax = config.GUI.MODALCUSTOM.maxPosition
                        }
                    }
                });

                UI.Panel(ref container, elemq0, GetColorHex(color), "0 0", "0.22 0.985");
                UI.Image(ref container, elemq0, GetImage(img), "0.042 0.15", "0.178 0.85");
                UI.Image(ref container, elemq0, GetImage("info3"), "0.93 0.62", "0.98 0.88");
                UI.Label(ref container, elemq0, title, 16, "0.26 0.55", "1.0 0.85", config.GUI.MODAL.colorTitle, TextAnchor.MiddleLeft, true);

                if (description.Length < 56){
                    UI.Label(ref container, elemq0, description, 12, "0.26 0.20", "1.0 0.50", config.GUI.MODALCUSTOM.colorDescription, TextAnchor.MiddleLeft);
                } else {
                    UI.Label(ref container, elemq0, description, 11, "0.26 0.15", "1.0 0.55", config.GUI.MODALCUSTOM.colorDescription, TextAnchor.MiddleLeft);
                }

                foreach (var player in BasePlayer.activePlayerList){
                    if (player == null) return;
                    CuiHelper.DestroyUi(player, elemq0);
                    CuiHelper.AddUi(player, container);
                    timer.Once(config.GUI.MODALCUSTOM.duration, () => CuiHelper.DestroyUi(player, elemq0));
                    if (config.GUI.MODALCUSTOM.soundEnabled && sound) Effect.server.Run(config.GUI.MODALCUSTOM.prefabSound, player.transform.position, Vector3.up, null, true);
                }
            }
        }

        private void NotifyEvents(string color, string img, string title, string description){
            if (config.GUI.modalEventsEnabled){
                var container = Notify_Event(color, img, title, description);
                foreach (var player in BasePlayer.activePlayerList){
                    if (player == null) return;
                    if (HasPermission(player.UserIDString, permByPassAlertsBlock)) return;
                    if (config.GUI.MODALEVENTS.soundEnabled) Effect.server.Run(config.GUI.MODALEVENTS.prefabSound, player.transform.position, Vector3.up, null, true);
                    CuiHelper.DestroyUi(player, elemq8);
                    CuiHelper.AddUi(player, container);
                    timer.Once(config.GUI.MODALEVENTS.duration, () => CuiHelper.DestroyUi(player, elemq8));
                }
            }
        }

        private void EditTier(BasePlayer player, int tierCurrent, int page = 0){
            var tierEditName = config.TIERS[tierCurrent].name;
            var tierEditColor = config.TIERS[tierCurrent].color;
            var itemsListAll = config.TIERS[tierCurrent].lists.items;
            var itemsList = itemsListAll.Skip(32 * page).Take(32).ToList();

            CuiHelper.DestroyUi(player, elemq3);
            var container = new CuiElementContainer();
            container.Add(new CuiElement {
                Name = elemq3,
                Parent = "Overlay",
                Components = {
                    new CuiImageComponent {
                        FadeIn = 0.3f,
                        Color = "0 0 0 0.6",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "400 -400",
                        OffsetMax = "640 360"
                    },
                    new CuiNeedsCursorComponent()
                }
            });
        
            UI.Panel(ref container, elemq3, tierEditColor, "0 0.93", "0.22 1");
            UI.Image(ref container, elemq3, GetImage(tierEditName), "0.042 0.94", "0.178 0.99");
            UI.Label(ref container, elemq3, Languaje("TitleEditPanel", player.UserIDString, tierEditName), 15, "0.26 0.93", "1.0 1", config.GUI.MODAL.colorTitle, TextAnchor.MiddleLeft, true);
            UI.Button(ref container, elemq3, GetColor("blue"), Languaje("AddInventory", player.UserIDString), 14, "0.05 0.855", "0.48 0.91", $"{commandCopy} {tierCurrent}");
            UI.Button(ref container, elemq3, GetColor("red"), Languaje("ClearList", player.UserIDString), 14, "0.52 0.855", "0.95 0.91", $"{commandClearList} {tierCurrent} {page}");
            
            if(tierCurrent != (tiercountAll-1)){
                UI.Label(ref container, elemq3, Languaje("ItemsList", player.UserIDString), 12, "0.05 0.80", "0.95 0.84", GetColor("white"), TextAnchor.MiddleLeft);
            } else {
                UI.Label(ref container, elemq3, Languaje("LastTier", player.UserIDString), 12, "0.05 0.50", "0.95 0.84", GetColor("white"), TextAnchor.MiddleCenter);
            }
            
            var list_sizeX = 46;
            var list_sizeY = 46;
            var list_startX = -105;
            var list_startY = 222;
            var list_x = list_startX;
            var list_y = list_startY;

            var po = 0;
            var e = 0;

            foreach (var itemName in itemsList){
                if (po != 0 && po % 4 == 0){
                    list_x = list_startX;
                    list_y -= list_sizeY + 10;
                }
                po++;

                container.Add(new CuiElement {
                    Name = elemq4, Parent = elemq3, Components = {
                        new CuiRawImageComponent{
                            Png = GetImage(itemName)
                        },
                        new CuiRectTransformComponent{
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = $"{list_x} {list_y-list_sizeY}",
                            OffsetMax = $"{list_x + list_sizeX} {list_y}"
                        }
                    }
                });

                container.Add(new CuiButton {
                    Button = {
                        Command = $"{commandClearItem} {itemName} {tierCurrent} {page}",
                        Color = GetColor("red"),
                        Material = "assets/content/ui/namefontmaterial.mat"
                    },
                    Text = {
                        Text = "Delete",
                        Align = TextAnchor.MiddleCenter,
                        FontSize = 10,
                        Font = "robotocondensed-regular.ttf"
                    },
                    RectTransform = {
                        AnchorMin = "0 0.3",
                        AnchorMax = "1 0.6"
                    }
                }, elemq4);
       
                list_x += list_sizeX + 8;
                e++;
            }

            if (itemsListAll.Count > 32 || page != 0){
                UI.Button(ref container, elemq3, page > 0 ? GetColor("blue") : "0.3 0.3 0.3 0.2", Languaje("Back", player.UserIDString), 15, "0.08 0.17", "0.48 0.205", page > 0 ? $"{commandTierEditPage} {tierCurrent} {page - 1}": "");
                UI.Button(ref container, elemq3, itemsListAll.Skip(32 * (page + 1)).Count() > 0 ? GetColor("blue") : "0.3 0.3 0.3 0.2", Languaje("Next", player.UserIDString), 15, "0.52 0.17", "0.92 0.205", itemsListAll.Skip(32 * (page + 1)).Count() > 0 ? $"{commandTierEditPage} {tierCurrent} {page + 1}": $"");
            }
            UI.Button(ref container, elemq3, GetColor("red"), Languaje("Close", player.UserIDString), 13, "0.08 0.11", "0.92 0.14", commandEditClose);
            UI.Button(ref container, elemq3, tierCurrent != 0 ? GetColor("green") : "0.3 0.3 0.3 0.2", Languaje("TierBack", player.UserIDString), 13, "0.08 0.06", "0.48 0.095", tierCurrent != 0 ? $"{commandTierEditPage} {tierCurrent-1} 0": "");
            UI.Button(ref container, elemq3, tierCurrent != (tiercountAll-1) ? GetColor("green") : "0.3 0.3 0.3 0.2", Languaje("TierNext", player.UserIDString), 13, "0.52 0.06", "0.92 0.095", tierCurrent != (tiercountAll-1) ? $"{commandTierEditPage} {tierCurrent+1} 0": $"");

            CuiHelper.AddUi(player, container);
        }

        private void OpenPanel(BasePlayer player, int tierCurrent, int page = 0){
            var tierName = config.TIERS[tierCurrent].name;
            var tierColor = config.TIERS[tierCurrent].color;
            var itemsListAll = new List<string>();
            var itemsList = new List<string>();
            if(tierCurrent != 0){
                itemsListAll = config.TIERS[tierCurrent-1].lists.items;
                itemsList = itemsListAll.Skip(16 * page).Take(16).ToList();
            }

            CuiHelper.DestroyUi(player, elemq5);
            var container = new CuiElementContainer();
            container.Add(new CuiElement {
                Name = elemq5,
                Parent = "Overlay",
                Components = {
                    new CuiImageComponent {
                        FadeIn = 0.3f,
                        Color = "0 0 0 0.6",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-250 -260",
                        OffsetMax = "250 315"
                    },
                    new CuiNeedsCursorComponent()
                }
            });
        
            UI.PanelColor(ref container, elemq5, tierColor, "0 0.91", "0.998 0.998");
            UI.Image(ref container, elemq5, GetImage(tierName), "0.32 0.92", "0.40 0.985");
            UI.Label(ref container, elemq5, Name, 20, "0.04 0.92", "0.3 0.99", config.GUI.MODAL.colorTitle, TextAnchor.MiddleLeft, false);
            UI.Label(ref container, elemq5, Languaje("CurrentTier", player.UserIDString), 13, "0.42 0.94", "1.0 1", config.GUI.MODAL.colorTitle, TextAnchor.MiddleLeft);
            UI.Label(ref container, elemq5, tierName, 18, "0.42 0.91", "1.0 0.97", config.GUI.MODAL.colorTitle, TextAnchor.MiddleLeft, true);
            UI.Button(ref container, elemq5, config.GUI.WINDOWS.colorButtonClose, Languaje("Close", player.UserIDString), 13, "0.83 0.93", "0.98 0.98", commandInfoClose);

            //BUILD
            UI.Panel(ref container, elemq5, "0.2 0.2 0.2 0.5", "0.04 0.76", "0.49 0.88");
            UI.Image(ref container, elemq5, GetImage(Grade(config.TIERS[tierCurrent].cupboards.maxGrade, true)), "0.06 0.782", "0.16 0.862");
            UI.Label(ref container, elemq5, Languaje("Building", player.UserIDString), 16, "0.19 0.81", "0.46 0.86", config.GUI.WINDOWS.colorTitle, TextAnchor.MiddleLeft, true);
            UI.Label(ref container, elemq5, Languaje("BuildingDes", player.UserIDString), 11, "0.19 0.78", "0.46 0.83", config.GUI.WINDOWS.colorText, TextAnchor.MiddleLeft);
            UI.Button(ref container, elemq5, "0 0 0 0", null, 1, "0.04 0.76", "0.49 0.88", $"{commandItemInfo} {tierCurrent} Building");

            //WORKBENCH
            UI.Panel(ref container, elemq5, "0.2 0.2 0.2 0.5", "0.51 0.76", "0.96 0.88");
            UI.Image(ref container, elemq5, GetImage(Workbench(config.TIERS[tierCurrent].workbench)), "0.53 0.782", "0.63 0.862");
            UI.Label(ref container, elemq5, Languaje("Workbench", player.UserIDString), 16, "0.66 0.81", "0.96 0.86", config.GUI.WINDOWS.colorTitle, TextAnchor.MiddleLeft, true);
            UI.Label(ref container, elemq5, Languaje("WorkbenchDes", player.UserIDString), 11, "0.66 0.78", "0.96 0.83", config.GUI.WINDOWS.colorText, TextAnchor.MiddleLeft);
            UI.Button(ref container, elemq5, "0 0 0 0", null, 1, "0.51 0.76", "0.96 0.88", $"{commandItemInfo} {tierCurrent} Workbench");

            //EVENTS
            UI.Panel(ref container, elemq5, "0.2 0.2 0.2 0.5", "0.04 0.62", "0.49 0.74");
            UI.Image(ref container, elemq5, GetImage("hackable"), "0.06 0.642", "0.16 0.722");
            UI.Label(ref container, elemq5, Languaje("Events", player.UserIDString), 16, "0.19 0.67", "0.46 0.73", config.GUI.WINDOWS.colorTitle, TextAnchor.MiddleLeft, true);
            UI.Label(ref container, elemq5, Languaje("EventsDes", player.UserIDString), 11, "0.19 0.63", "0.46 0.69", config.GUI.WINDOWS.colorText, TextAnchor.MiddleLeft);
            UI.Button(ref container, elemq5, "0 0 0 0", null, 1, "0.04 0.62", "0.49 0.74", $"{commandItemInfo} {tierCurrent} Events");

            //VENDING
            UI.Panel(ref container, elemq5, "0.2 0.2 0.2 0.5", "0.51 0.62", "0.96 0.74");
            UI.Image(ref container, elemq5, GetImage("scrapheli"), "0.53 0.642", "0.63 0.722");
            UI.Label(ref container, elemq5, Languaje("Vending", player.UserIDString), 16, "0.66 0.67", "0.96 0.73", config.GUI.WINDOWS.colorTitle, TextAnchor.MiddleLeft, true);
            UI.Label(ref container, elemq5, Languaje("VendingDes", player.UserIDString), 11, "0.66 0.63", "0.96 0.693", config.GUI.WINDOWS.colorText, TextAnchor.MiddleLeft);
            UI.Button(ref container, elemq5, "0 0 0 0", null, 1, "0.51 0.62", "0.96 0.74", $"{commandItemInfo} {tierCurrent} Vending");

            //SPAWN
            UI.Panel(ref container, elemq5, "0.2 0.2 0.2 0.5", "0.04 0.48", "0.49 0.60");
            UI.Image(ref container, elemq5, GetImage(KeyCard(config.TIERS[tierCurrent].spawns.spawnCards)), "0.06 0.502", "0.16 0.582");
            UI.Label(ref container, elemq5, Languaje("Spawn", player.UserIDString), 16, "0.19 0.53", "0.46 0.59", config.GUI.WINDOWS.colorTitle, TextAnchor.MiddleLeft, true);
            UI.Label(ref container, elemq5, Languaje("SpawnDes", player.UserIDString), 11, "0.19 0.50", "0.46 0.543", config.GUI.WINDOWS.colorText, TextAnchor.MiddleLeft);
            UI.Button(ref container, elemq5, "0 0 0 0", null, 1, "0.04 0.48", "0.49 0.60", $"{commandItemInfo} {tierCurrent} Spawn");

            //SPEED
            UI.Panel(ref container, elemq5, "0.2 0.2 0.2 0.5", "0.51 0.48", "0.96 0.60");
            UI.Image(ref container, elemq5, GetImage("pickaxe"), "0.53 0.502", "0.63 0.582");
            UI.Label(ref container, elemq5, Languaje("Speed", player.UserIDString), 16, "0.66 0.53", "0.96 0.59", config.GUI.WINDOWS.colorTitle, TextAnchor.MiddleLeft, true);
            UI.Label(ref container, elemq5, Languaje("SpeedDes", player.UserIDString), 11, "0.66 0.50", "0.96 0.543", config.GUI.WINDOWS.colorText, TextAnchor.MiddleLeft);
            UI.Button(ref container, elemq5, "0 0 0 0", null, 1, "0.51 0.48", "0.96 0.60", $"{commandItemInfo} {tierCurrent} Speed");  

            //ITEMS
            UI.Panel(ref container, elemq5, "0.2 0.2 0.2 0.5", "0.04 0.15", "0.96 0.46");
            UI.Label(ref container, elemq5, Languaje("ItemsBlock", player.UserIDString), 16, "0.06 0.39", "1.0 0.46", config.GUI.WINDOWS.colorTitle, TextAnchor.MiddleLeft, true);
            UI.Label(ref container, elemq5, Languaje("ItemsBlockDes", player.UserIDString), 11, "0.06 0.37", "1.0 0.41", config.GUI.WINDOWS.colorText, TextAnchor.MiddleLeft);
            
            var list_sizeX = 40;
            var list_sizeY = 40;
            var list_startX = -190;
            var list_startY = -92;
            var list_x = list_startX;
            var list_y = list_startY;

            var po = 0;
            var e = 0;

            foreach (var itemName in itemsList){
                if (po != 0 && po % 8 == 0){
                    list_x = list_startX;
                    list_y -= list_sizeY + 10;
                }
                po++;

                container.Add(new CuiElement {
                    Name = elemq6, Parent = elemq5, Components = {
                        new CuiRawImageComponent{
                            Png = GetImage(itemName)
                        },
                        new CuiRectTransformComponent{
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = $"{list_x} {list_y-list_sizeY}",
                            OffsetMax = $"{list_x + list_sizeX} {list_y}"
                        }
                    }
                });
                list_x += list_sizeX + 8;
                e++;
            }
            
            if (itemsListAll.Count > 16 || page != 0){
                UI.Button(ref container, elemq5, page > 0 ? GetColor("blue") : "0.2 0.2 0.2 0.1", "<<", 18, "0.04 0.15", "0.10 0.36", page > 0 ? $"{commandTierInfoPage} {tierCurrent} {page - 1}": "");
                UI.Button(ref container, elemq5, itemsListAll.Skip(16 * (page + 1)).Count() > 0 ? GetColor("blue") : "0.2 0.2 0.2 0.1", ">>", 18, "0.90 0.15", "0.96 0.36", itemsListAll.Skip(16 * (page + 1)).Count() > 0 ? $"{commandTierInfoPage} {tierCurrent} {page + 1}": $"");
            }


            if(tierCurrent != 0 && config.GUI.WINDOWS.showBtnPrevius){
                var tierNameBack = config.TIERS[tierCurrent-1].name;
                var tierColorBack = config.TIERS[tierCurrent-1].color;
                UI.Panel(ref container, elemq5, tierColorBack, "0.02 0.02", "0.48 0.12");
                UI.Label(ref container, elemq5, tierNameBack, 13, "0.21 0.05", "0.51 0.09", GetColor("white"), TextAnchor.MiddleCenter);
                UI.Image(ref container, elemq5, GetImage(tierNameBack), "0.11 0.04", "0.19 0.10");
                UI.Button(ref container, elemq5, "0.2 0.2 0.2 0.2", "<<", 13, "0.02 0.02", "0.48 0.12", $"{commandTierInfoPage} {tierCurrent-1} 0");
            }

            if(tierCurrent != (tiercountAll-1) && config.GUI.WINDOWS.showBtnNext){
                var tierNameNext = config.TIERS[tierCurrent+1].name;
                var tierColorNext = config.TIERS[tierCurrent+1].color;
                UI.Panel(ref container, elemq5, tierColorNext, "0.52 0.02", "0.98 0.12");
                UI.Label(ref container, elemq5, tierNameNext, 13, "0.52 0.05", "0.76 0.09", GetColor("white"), TextAnchor.MiddleCenter);
                UI.Image(ref container, elemq5, GetImage(tierNameNext), "0.81 0.04", "0.89 0.10");
                UI.Button(ref container, elemq5, "0.2 0.2 0.2 0", ">>", 13, "0.52 0.02", "0.98 0.12", $"{commandTierInfoPage} {tierCurrent+1} 0");
            }
            
            CuiHelper.AddUi(player, container);
        }

         private void OpenPanelItem(BasePlayer player, int tierCurrent, string type){
            var tierName = config.TIERS[tierCurrent].name;
            var tierColor = config.TIERS[tierCurrent].color;

            CuiHelper.DestroyUi(player, elemq9);
            var container = new CuiElementContainer();
            container.Add(new CuiElement {
                Name = elemq9,
                Parent = "Overlay",
                Components = {
                    new CuiImageComponent {
                        FadeIn = 0.3f,
                        Color = "0 0 0 0.8",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-250 -260",
                        OffsetMax = "250 315"
                    },
                    new CuiNeedsCursorComponent()
                }
            });
        
            UI.PanelColor(ref container, elemq9, config.GUI.WINDOWS.colorBG, "0 0.91", "0.998 0.998");
            UI.Image(ref container, elemq9, GetImage(tierName), "0.32 0.92", "0.40 0.985");
            UI.Label(ref container, elemq9, Languaje(type, player.UserIDString), 20, "0.04 0.92", "0.3 0.99", config.GUI.MODAL.colorTitle, TextAnchor.MiddleLeft, false);
            UI.Label(ref container, elemq9, Languaje("CurrentTier", player.UserIDString), 13, "0.42 0.94", "1.0 1", config.GUI.MODAL.colorTitle, TextAnchor.MiddleLeft);
            UI.Label(ref container, elemq9, tierName, 18, "0.42 0.91", "1.0 0.97", config.GUI.MODAL.colorTitle, TextAnchor.MiddleLeft, true);
            UI.Button(ref container, elemq9, config.GUI.WINDOWS.colorButtonClose, Languaje("Close", player.UserIDString), 13, "0.83 0.93", "0.98 0.98", commandItemInfoClose);

            var list_sizeX = 460;
            var list_sizeY = 70;
            var list_startX = -230;
            var list_startY = 220;
            var list_x = list_startX;
            var list_y = list_startY;

            var po = 0;
            var e = 0;

            foreach (var item in itemType[tierCurrent][type]){
                if (po != 0 && po % 1 == 0){
                    list_x = list_startX;
                    list_y -= list_sizeY + 10;
                }
                po++;

                container.Add(new CuiElement {
                    Name = elemq10, Parent = elemq9, Components = {
                        new CuiImageComponent {
                            Color = "0.4 0.4 0.4 0.8"
                        },
                        new CuiRectTransformComponent{
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = $"{list_x} {list_y-list_sizeY}",
                            OffsetMax = $"{list_x + list_sizeX} {list_y}"
                        }
                    }
                });

                UI.Image(ref container, elemq10, GetImage(item.img), "0.02 0.2", "0.12 0.8");
                UI.Label(ref container, elemq10, Languaje(item.title, player.UserIDString), 16, "0.16 0.48", "0.96 0.85", config.GUI.WINDOWS.colorTitle, TextAnchor.MiddleLeft, true);
                UI.Label(ref container, elemq10, Languaje(item.text, player.UserIDString), 11, "0.16 0.18", "0.96 0.52", config.GUI.WINDOWS.colorText, TextAnchor.MiddleLeft);

                list_x += list_sizeX + 8;
                e++;
            }
            CuiHelper.AddUi(player, container);
        }

        private void Countdown(BasePlayer player){
            CuiHelper.DestroyUi(player, elemq1);

            var container = new CuiElementContainer();
            container.Add(new CuiElement {
                Name = elemq1,
                Parent = "Hud",
                Components = {
                    new CuiImageComponent {
                        FadeIn = 0.3f,
                        Color = "0 0 0 0.6",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = config.GUI.COUNTER.minPosition,
                        OffsetMax = screenCounterIcon ? config.GUI.COUNTER.maxPosition : config.GUI.COUNTER.maxPositionNoIcon
                    }
                }
            });
        
            if (screenCounterIcon){
                UI.Panel(ref container, elemq1, tierColor, "0 0", "0.22 0.98");
                UI.Image(ref container, elemq1, GetImage(tierName), "0.035 0.15", "0.185 0.85");
            }
            UI.Label(ref container, elemq1, tierName, 13, screenCounterIcon ? "0.26 0.47" : "0.05 0.47", "1.0 0.89", config.GUI.COUNTER.colorTitle, TextAnchor.MiddleLeft, true);
            if(screenCounterButton) UI.Button(ref container, elemq1, config.GUI.COUNTER.colorButton, Languaje("Info", player.UserIDString), 9, screenCounterIcon ? "0.85 0.57" : "0.81 0.57","0.98 0.915", commandOpenInfo);
            CuiHelper.AddUi(player, container);
        }

        public CuiElementContainer UpdateClock(DateTime time, float minutes){
            var container = new CuiElementContainer();

            container.Add(new CuiPanel{
                Image = {
                    Color="0 0 0 0"
                },
                RectTransform = {
                    AnchorMin= screenCounterIcon ? "0.26 0" : "0.05 0", AnchorMax=$"1 0.55"
                }
            }, elemq1, elemq2);

            var showTime = Languaje("CountTime", null) + FormatTime((time.AddMinutes(minutes) - DateTime.UtcNow).TotalSeconds);
            UI.Label(ref container, elemq2, showTime, 10, "0 0", "1.0 1.0", config.GUI.COUNTER.colorSubttitle, TextAnchor.MiddleLeft);
            return container;
        }

        public CuiElementContainer Notify_Changue(int tierold, int tierCurrent){
            string nameold = config.TIERS[tierold].name;
            string colorold = config.TIERS[tierold].color;
            string namenew = config.TIERS[tierCurrent].name;
            string colornew = config.TIERS[tierCurrent].color;

            var container = new CuiElementContainer();
            container.Add(new CuiElement {
                Name = elemq7,
                Parent = "Hud",
                Components = {
                    new CuiImageComponent {
                        FadeIn = 0.3f,
                        Color = "0 0 0 0.6",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = config.GUI.MODALCHANGUE.minPosition,
                        OffsetMax = config.GUI.MODALCHANGUE.maxPosition
                    }
                }
            });
        
            UI.Panel(ref container, elemq7, colorold, "0 0", "0.20 0.985");
            UI.Image(ref container, elemq7, GetImage(nameold), "0.032 0.15", "0.168 0.85");
            UI.Panel(ref container, elemq7, colornew, "0.798 0", "0.998 0.985");
            UI.Image(ref container, elemq7, GetImage(namenew), "0.824 0.15", "0.96 0.85");
            UI.Label(ref container, elemq7, Languaje("UpLevel", null), 16, "0 0.55", "1.0 0.85", config.GUI.MODALCHANGUE.colorTitle, TextAnchor.MiddleCenter, true);

            UI.Label(ref container, elemq7, Languaje("ChangueLevel", null, nameold, namenew), 13, "0 0.20", "1.0 0.50", config.GUI.MODALCHANGUE.colorDescription, TextAnchor.MiddleCenter);
  
            return container;
        }

        public CuiElementContainer Notify_Event(string color, string img, string title, string description){
            var container = new CuiElementContainer();
            container.Add(new CuiElement {
                Name = elemq8,
                Parent = "Hud",
                Components = {
                    new CuiImageComponent {
                        FadeIn = 0.3f,
                        Color = "0 0 0 0.6",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = config.GUI.MODALEVENTS.minPosition,
                        OffsetMax = config.GUI.MODALEVENTS.maxPosition
                    }
                }
            });

            UI.Panel(ref container, elemq8, GetColor(color), "0 0", "0.20 0.985");
            UI.Image(ref container, elemq8, GetImage(img), "0.032 0.15", "0.168 0.85");
            UI.Image(ref container, elemq8, GetImage("info3"), "0.93 0.62", "0.98 0.88");
            UI.Label(ref container, elemq8, title, 16, "0.26 0.55", "1.0 0.85", config.GUI.MODALCHANGUE.colorTitle, TextAnchor.MiddleLeft, true);
            UI.Label(ref container, elemq8, description, 12, "0.26 0.20", "1.0 0.50", config.GUI.MODALCHANGUE.colorDescription, TextAnchor.MiddleLeft);
            return container;
        }

        private string FormatTime(double time){
            TimeSpan timeSpan = TimeSpan.FromSeconds(time);
            if (timeSpan.TotalSeconds < 1) return null;

            if (Math.Floor(timeSpan.TotalDays) >= 1)
                return Languaje("Days", null, timeSpan.Days, timeSpan.Hours, timeSpan.Minutes);
            if (Math.Floor(timeSpan.TotalMinutes) >= 60)
                return Languaje("Hours", null, timeSpan.Hours, timeSpan.Minutes);
            if (Math.Floor(timeSpan.TotalSeconds) >= 60)
                return Languaje("Mins", null, timeSpan.Minutes, timeSpan.Seconds);
            return Languaje("Secs", null, timeSpan.Seconds);
        }

        private float FormatTimeSpeed(TimeSpan timeSpan){
            if (timeSpan.TotalSeconds < 1) return 12.0f;
            if (Math.Floor(timeSpan.TotalDays) >= 1) return 60.0f; //Días
            if (Math.Floor(timeSpan.TotalMinutes) >= 60) return 30.0f; //Horas
            if (Math.Floor(timeSpan.TotalSeconds) >= 60) return 10.0f; // Minutos
            if (Math.Floor(timeSpan.TotalSeconds) >= 30) return 5.0f;
            return 1.0f;
        }

        private void cmdCloseEdit(ConsoleSystem.Arg arg){
            if (arg.Player() != null) CuiHelper.DestroyUi(arg.Player(), elemq3);
        }

        private void cmdCloseInfo(ConsoleSystem.Arg arg){
            if (arg.Player() != null) CuiHelper.DestroyUi(arg.Player(), elemq5);
        }

        private void cmdCloseItemInfo(ConsoleSystem.Arg arg){
            if (arg.Player() != null) CuiHelper.DestroyUi(arg.Player(), elemq9);
        }

        private void cmdClearList(ConsoleSystem.Arg arg){
            if (arg.Player() != null){
                var player = arg.Player();
                var tier = int.Parse(arg.Args[0]);
                var itemsList = config.TIERS[tier].lists.items;
                itemsList.Clear();
                SaveConfig();
                EditTier(player, tier, 0);
            }
        }

        private void cmdClearItem(ConsoleSystem.Arg arg){
            if (arg.Player() != null){
                var player = arg.Player();
                var itemName = arg.Args[0];
                var tier = int.Parse(arg.Args[1]);
                var page = int.Parse(arg.Args[2]);
                var itemsList = config.TIERS[tier].lists.items;
                if (itemsList.Contains(itemName)) itemsList.Remove(itemName);
                SaveConfig();
                EditTier(player, tier, page);
            }
        }    

        private void cmdCopy(ConsoleSystem.Arg arg){
            if (arg.Player() != null){
                var player = arg.Player();
                var tier = int.Parse(arg.Args[0]);
                var itemsList = config.TIERS[tier].lists.items;
                foreach (var item in player.inventory.AllItems()){
                    var name = item.info.shortname;
                    if (!itemsList.Contains(name)) itemsList.Add(name);
                }
                SaveConfig();
                EditTier(player, tier, 0);
            }
        }

        private void cmdTierEditPage(ConsoleSystem.Arg arg){
             if (arg.Player() != null){
                var player = arg.Player();
                var tier = int.Parse(arg.Args[0]);
                var page = int.Parse(arg.Args[1]);
                EditTier(player, tier, page);
            }
        }

        private void cmdTierInfoPage(ConsoleSystem.Arg arg){
             if (arg.Player() != null){
                var player = arg.Player();
                var tier = int.Parse(arg.Args[0]);
                var page = int.Parse(arg.Args[1]);
                OpenPanel(player, tier, page);
            }
        }

        private void cmdItemInfo(ConsoleSystem.Arg arg){
             if (arg.Player() != null){
                var player = arg.Player();
                var tier = int.Parse(arg.Args[0]);
                var type = arg.Args[1];
                OpenPanelItem(player, tier, type);
            }
        }
        #endregion

        #region CUI Helper
        public class UI {
            static public void Panel(ref CuiElementContainer container, string panel, string color, string aMin, string aMax, bool cursor = false, string material = "assets/content/ui/namefontmaterial.mat"){
                container.Add(new CuiPanel{
                    Image = { Color = color, Material = material},
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax},
                    CursorEnabled = cursor
                },
                panel);
            }

            static public void PanelColor(ref CuiElementContainer container, string panel, string color, string aMin, string aMax, bool cursor = false){
                container.Add(new CuiPanel{
                    Image = { Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax},
                    CursorEnabled = cursor
                },
                panel);
            }

            static public void Label(ref CuiElementContainer container, string panel, string text, int size, string aMin, string aMax, string color = "1 1 1 0.6", TextAnchor align = TextAnchor.MiddleCenter, bool font = false){
                container.Add(new CuiLabel{
                    Text = { FontSize = size, Font = font? "robotocondensed-bold.ttf" : "robotocondensed-regular.ttf", Color = color, Align = align, Text = text},
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax}
                },
                panel);
            }

            static public void Button(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter){
                container.Add(new CuiButton{
                    Button = { Color = color, Material = "assets/content/ui/namefontmaterial.mat", Command = command, FadeIn = 0f},
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax},
                    Text = { Text = text, FontSize = size, Align = align}
                },
                panel);
            }

            static public void Input(ref CuiElementContainer container, string panel, string color, string text, int size, string command, string aMin, string aMax){
                container.Add(new CuiElement{
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components = {
                        new CuiInputFieldComponent {
                            Align = TextAnchor.MiddleLeft,
                            CharsLimit = 50,
                            Color = color,
                            Command = command + text,
                            FontSize = size,
                            IsPassword = false,
                            Text = text
                        },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax}
                    }
                });
            }

            static public void Image(ref CuiElementContainer container, string panel, string png, string aMin, string aMax){
                container.Add(new CuiElement{
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components = {
                        new CuiRawImageComponent {Png = png},
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax}
                    }
                });
            }
        }

        private string GetColor(string color){
            switch (color){
                case "red":
                    return "0.78 0.00 0.00 0.70";
                case "blue":
                    return "0 0.50 1.00 0.70";
                case "green":
                    return "0.00 0.77 0.41 0.70";
                case "white":
                    return "1.0 1.0 1.0 0.70";
                default:
                    return color;
            }
        }

        public string GetColorHex(string color){
            color = color.TrimStart('#');
            if (color.Length != 6 && color.Length != 8){ color = "000000"; }
            int red = int.Parse(color.Substring(0, 2), NumberStyles.AllowHexSpecifier);
            int green = int.Parse(color.Substring(2, 2), NumberStyles.AllowHexSpecifier);
            int blue = int.Parse(color.Substring(4, 2), NumberStyles.AllowHexSpecifier);
            int alpha = 255;
            if (color.Length == 8){
                alpha = int.Parse(color.Substring(6, 2), NumberStyles.AllowHexSpecifier);
            }
            return $"{red / 255.0} {green / 255.0} {blue / 255.0} {alpha / 255.0}";
        }

        public string ColorToHex(string color){
            string[] rgba = color.Split(' ');
            var colortxt = "#" + ColorUtility.ToHtmlStringRGB(new Color(float.Parse(rgba[0]), float.Parse(rgba[1]), float.Parse(rgba[2]), float.Parse(rgba[3])));
            return colortxt;
        }
        #endregion

        #region Config
        private static ConfigData config;

        private class ConfigData {
            [JsonProperty(PropertyName = "Automatic Server Title")]
            public ServerHostname SERVERTITLE;

            [JsonProperty(PropertyName = "GUI Config")]
            public GUIConfig GUI;

            [JsonProperty(PropertyName = "Messages")]
            public Messages MSG;

            [JsonProperty(PropertyName = "Tiers")]
            public List<TierInfo> TIERS;
        }

        private class ServerHostname {
            [JsonProperty(PropertyName = "Enabled Auto Changue")]
            public bool titleEnabled;

            [JsonProperty(PropertyName = "Base title to modify")]
            public string titleText;
        }

        private class GUIConfig {
            [JsonProperty(PropertyName = "On-Screen GUI Counter")]
            public bool screenCounter;

            [JsonProperty(PropertyName = "Counter Config")]
            public Counter COUNTER;

            [JsonProperty(PropertyName = "Enabled Alert Modal")]
            public bool modalEnabled;

            [JsonProperty(PropertyName = "Modal Config")]
            public Modal MODAL;

            [JsonProperty(PropertyName = "Enabled Alert Modal Changue Tier")]
            public bool modalChangueEnabled;

            [JsonProperty(PropertyName = "Modal Changue Config")]
            public Modal MODALCHANGUE;

            [JsonProperty(PropertyName = "Enabled Alert Modal Events")]
            public bool modalEventsEnabled;

            [JsonProperty(PropertyName = "Modal Events Config")]
            public Modal MODALEVENTS;

            [JsonProperty(PropertyName = "Enabled Custom Alert Modal")]
            public bool modalCustomEnabled;

            [JsonProperty(PropertyName = "Modal Custom Config")]
            public Modal MODALCUSTOM;

            [JsonProperty(PropertyName = "Windows Info Config")]
            public Info WINDOWS;
        }

        private class Counter {
            [JsonProperty(PropertyName = "Color Title")]
            public string colorTitle;

            [JsonProperty(PropertyName = "Color Subtitle")]
            public string colorSubttitle;

            [JsonProperty(PropertyName = "Color Button")]
            public string colorButton;

            [JsonProperty(PropertyName = "Show Icon/Color")]
            public bool screenCounterIcon;

            [JsonProperty(PropertyName = "Show Button Info")]
            public bool screenCounterButton;

            [JsonProperty(PropertyName = "AnchorMin")]
            public string minPosition;

            [JsonProperty(PropertyName = "AnchorMax")]
            public string maxPosition;

            [JsonProperty(PropertyName = "AnchorMax No Show Icon")]
            public string maxPositionNoIcon;
        }

        private class Modal {
            [JsonProperty(PropertyName = "Duration Show")]
            public float duration;

            [JsonProperty(PropertyName = "Color Title")]
            public string colorTitle;

            [JsonProperty(PropertyName = "Color Description")]
            public string colorDescription;

            [JsonProperty(PropertyName = "Sound Alert")]
            public bool soundEnabled;

            [JsonProperty(PropertyName = "Prefab Sound")]
            public string prefabSound;

            [JsonProperty(PropertyName = "AnchorMin")]
            public string minPosition;

            [JsonProperty(PropertyName = "AnchorMax")]
            public string maxPosition;
        }

        private class Info {
            [JsonProperty(PropertyName = "Show GUI Info Window on Enter")]
            public bool showWindowsEnter;

            [JsonProperty(PropertyName = "Show GUI Info use Command")]
            public bool showWindowsCommand;

            [JsonProperty(PropertyName = "Show Previous Tier Button")]
            public bool showBtnPrevius;

            [JsonProperty(PropertyName = "Show Next Tier Button")]
            public bool showBtnNext;

            [JsonProperty(PropertyName = "Color Title")]
            public string colorTitle;

            [JsonProperty(PropertyName = "Color Text")]
            public string colorText;

            [JsonProperty(PropertyName = "Color Header Info")]
            public string colorBG;

            [JsonProperty(PropertyName = "Color Button Close")]
            public string colorButtonClose;
        }

        private class Messages {
            [JsonProperty(PropertyName = "Enabled Chat")]
            public bool chatEnabled;

            [JsonProperty(PropertyName = "Enabled Welcome Chat")]
            public bool chatWelcomeEnabled;

            [JsonProperty(PropertyName = "Use Discord Message Plugin")]
            public bool discordEnabled;

            [JsonProperty(PropertyName = "Webhook Channel Announcement Change of Stag")]
            public string discordWebhook;
        }
        
        private class TierInfo {
            [JsonProperty(PropertyName = "Tier Name")]
            public string name;

            [JsonProperty(PropertyName = "Tier Icon URL")]
            public string icon;

            [JsonProperty(PropertyName = "Tier Color")]
            public string color;
            
            [JsonProperty(PropertyName = "Duration of the tier in Minutes")]
            public int durationMinutes;

            [JsonProperty(PropertyName = "Cupboards")]
            public CupboardsConfig cupboards;
           
            [JsonProperty(PropertyName = "Workbench")]
            public WorkbenchConfig workbench;

            [JsonProperty(PropertyName = "Events")]
            public EventsConfig events;

            [JsonProperty(PropertyName = "Sellers")]
            public SellersConfig sellers;

            [JsonProperty(PropertyName = "Spawn")]
            public SpawnConfig spawns;

            [JsonProperty(PropertyName = "Speed")]
            public SpeedConfig speed;

            [JsonProperty(PropertyName = "Lists")]
            public ListsConfig lists;
        }

        private class CupboardsConfig {
            [JsonProperty(PropertyName = "Limit Maximum Placed Cupboards")]
            public bool limitCupboard;

            [JsonProperty(PropertyName = "Maximum Cupboards per player")]
            public int maxCupboards;

            [JsonProperty(PropertyName = "Global Alert for Destruction of Cupboard")]
            public bool enabledAlert;

            [JsonProperty(PropertyName = "Config Destruction Alert")]
            public DestructionConfig ALERT;

            [JsonProperty(PropertyName = "Maximum Upgrade Level (1 = Wood, 2 = Stones, 3 = Metal, 4 = HQ)")]
            public int maxGrade;
        }

        private class DestructionConfig {
            [JsonProperty(PropertyName = "Show Name of the player who destroyed it in the info")]
            public bool name;

            [JsonProperty(PropertyName = "Show Position in info")]
            public bool pos;
        }

        private class WorkbenchConfig {
            [JsonProperty(PropertyName = "Allow research from Workbench")]
            public bool researchWorkbench;

            [JsonProperty(PropertyName = "Allow Place All Workbench")]
            public bool placeAllWorkbench;

            [JsonProperty(PropertyName = "Allow placing only the following workbenches")]
            public List<string> onlyWorkbench;
        }

        private class EventsConfig {
            [JsonProperty(PropertyName = "Cargoship")]
            public bool cargoship;

            [JsonProperty(PropertyName = "Oil Rig")]
            public bool oilrig;

            [JsonProperty(PropertyName = "Chinook")]
            public bool chinook;

            [JsonProperty(PropertyName = "Patrol Helicopter")]
            public bool helicopter;

            [JsonProperty(PropertyName = "Airdrop")]
            public bool supplysignal;

            [JsonProperty(PropertyName = "Bradley APC")]
            public bool bradley;
        }

        private class SellersConfig {
            [JsonProperty(PropertyName = "Helicopter Sale")]
            public bool helicopter;

            [JsonProperty(PropertyName = "Boat Sales")]
            public bool boat;

            [JsonProperty(PropertyName = "Horse Sales")]
            public bool horse;

            [JsonProperty(PropertyName = "Use of Drones")]
            public bool drones;

            [JsonProperty(PropertyName = "Use of Public Vending Machine (Rads)")]
            public bool vending;
        }

        private class SpawnConfig {
            [JsonProperty(PropertyName = "Spawn Cards")]
            public List<string> spawnCards;

            [JsonProperty(PropertyName = "Spawn Vehicles (Cars)")]
            public List<string> spawnVehicles;

            [JsonProperty(PropertyName = "Use Vehicle MLRS")]
            public bool spawnMLRS;

            [JsonProperty(PropertyName = "Use Vehicle Workcart")]
            public bool spawnWORKCART;

            [JsonProperty(PropertyName = "Use Vehicle Locomotive")]
            public bool spawnTRAIN;
        }

        private class SpeedConfig {
            [JsonProperty(PropertyName = "Crafting Rate (1.0 = Vanilla | 0.0 = Instant)")]
            public float craftingSpeed;

            [JsonProperty(PropertyName = "Research Seconds (10.0 = Vanilla | 0.0 = Instant)")]
            public float researchSpeed;

            [JsonProperty(PropertyName = "Recycler Seconds (5.0 = Vanilla | 0.0 = Instant)")]
            public float recyclerSpeed;

            [JsonProperty(PropertyName = "Gather System (If you use another plugin for the Gather disable this or remove the other one)")]
            public bool gatherSpeed;

            [JsonProperty(PropertyName = "Gather Config (Setting 0.0 will disable this option and it will work as vanilla)")]
            public GatherConfig gatherConfig;
        }

        private class GatherConfig {
            [JsonProperty(PropertyName = "DispenserGather")]
            public float Resource;

            [JsonProperty(PropertyName = "DispenserBonus")]
            public float ResourceBonus;

            [JsonProperty(PropertyName = "CollectiblePickup")]
            public float Pickup;

            [JsonProperty(PropertyName = "GrowableGathered")]
            public float Growable;

            [JsonProperty(PropertyName = "SurveyGather")]
            public float Survey;

            [JsonProperty(PropertyName = "QuarryGather")]
            public float Quarry;

            [JsonProperty(PropertyName = "ExcavatorGather")]
            public float Excavator;
        }

        private class ListsConfig {
            [JsonProperty(PropertyName = "Items lock Craft")]
            public bool itemsBlockCraft;

            [JsonProperty(PropertyName = "Items lock")]
            public List<string> items;

            [JsonProperty(PropertyName = "Deployable lock without Building Permissions")]
            public bool itemsBlockDeployables;

            [JsonProperty(PropertyName = "Items lock without Building Permissions")]
            public Dictionary<string,string> itemsDeployables;

            [JsonProperty(PropertyName = "Server Commands")]
            public List<string> commands;
        }

        private ConfigData GetDefaultConfig() {
            return new ConfigData {
                SERVERTITLE = new ServerHostname {
                    titleEnabled = false,
                    titleText = "[ESP/LATAM] BELLUM2 [X2|SKINS|WIPE10/06]"
                },
                GUI = new GUIConfig {
                    screenCounter = true,
                    COUNTER = new Counter {
                        colorTitle = "1.0 1.0 1.0 0.70",
                        colorSubttitle = "1.0 1.0 1.0 0.70",
                        colorButton = "0.46 0.46 0.46 0.49",
                        screenCounterIcon = true,
                        screenCounterButton = true,
                        minPosition = "-630 310",
                        maxPosition = "-420 350",
                        maxPositionNoIcon = "-470 350"
                    },
                    modalEnabled = true,
                    MODAL = new Modal {
                        duration = 10.0f,
                        colorTitle = "1.0 1.0 1.0 0.70",
                        colorDescription = "1.0 1.0 1.0 0.70",
                        soundEnabled = true,
                        prefabSound = "assets/prefabs/tools/pager/effects/beep.prefab",
                        minPosition = "-200 -275",
                        maxPosition = "180 -205"
                    },
                    modalChangueEnabled = true,
                    MODALCHANGUE = new Modal {
                        duration = 15.0f,
                        colorTitle = "1.0 1.0 1.0 0.70",
                        colorDescription = "1.0 1.0 1.0 0.70",
                        soundEnabled = true,
                        prefabSound = "assets/bundled/prefabs/fx/item_unlock.prefab",
                        minPosition = "-200 -275",
                        maxPosition = "180 -205"
                    },
                    modalEventsEnabled = true,
                    MODALEVENTS = new Modal {
                        duration = 15.0f,
                        colorTitle = "1.0 1.0 1.0 0.70",
                        colorDescription = "1.0 1.0 1.0 0.70",
                        soundEnabled = true,
                        prefabSound = "assets/prefabs/tools/pager/effects/vibrate.prefab",
                        minPosition = "-200 -275",
                        maxPosition = "180 -205"
                    },
                    modalCustomEnabled = true,
                    MODALCUSTOM = new Modal {
                        duration = 10.0f,
                        colorTitle = "1.0 1.0 1.0 0.70",
                        colorDescription = "1.0 1.0 1.0 0.70",
                        soundEnabled = true,
                        prefabSound = "assets/prefabs/tools/pager/effects/beep.prefab",
                        minPosition = "-200 -275",
                        maxPosition = "180 -205"
                    },
                    WINDOWS = new Info {
                        showWindowsEnter = true,
                        showWindowsCommand = true,
                        showBtnPrevius = true,
                        showBtnNext = true,
                        colorTitle = "1.00 1.00 1.00 1.00",
                        colorText = "0.90 0.90 0.90 1.00",
                        colorBG = "0.0 0.0 0.0 1.0",
                        colorButtonClose = "0.20 0.20 0.20 0.80"
                    },
                },
                MSG = new Messages {
                    chatEnabled = true,
                    chatWelcomeEnabled = true,
                    discordEnabled = true,
                    discordWebhook = "Put here your Webhook of the Channel where you want to announce the changes of Tiers."
                },
                TIERS = new List<TierInfo> {
                    new TierInfo {
                        name = "Wood Age",
                        icon = "https://img.rustspain.com/tiers/1.png",
                        color = "0.17 0.75 0.42 0.50",
                        durationMinutes = 24,
                        cupboards = new CupboardsConfig {
                            limitCupboard = false,
                            maxCupboards = 5,
                            enabledAlert = true,
                            ALERT = new DestructionConfig {
                                name = true,
                                pos = true
                            },
                            maxGrade = 1
                        },
                        workbench = new WorkbenchConfig {
                            researchWorkbench = true,
                            placeAllWorkbench = true,
                            onlyWorkbench = new List<string>(){
                                "assets/prefabs/deployable/tier 1 workbench/workbench1.deployed.prefab",
                                "assets/prefabs/deployable/tier 2 workbench/workbench2.deployed.prefab",
                                "assets/prefabs/deployable/tier 3 workbench/workbench3.deployed.prefab"
                            }
                        },
                        events = new EventsConfig {
                            cargoship = true,
                            oilrig = true,
                            chinook = true,
                            helicopter = true,
                            supplysignal = true,
                            bradley = true
                        },
                        sellers = new SellersConfig {
                            helicopter = true,
                            boat = true,
                            horse = true,
                            drones = true,
                            vending = true
                        },
                        spawns = new SpawnConfig {
                            spawnCards = new List<string>(){
                                "keycard_green_pickup.entity",
                                "keycard_blue_pickup.entity",
                                "keycard_red_pickup.entity"
                            },
                            spawnVehicles = new List<string>(){
                            },
                            spawnMLRS = false,
                            spawnWORKCART = true,
                            spawnTRAIN = true
                        },
                        speed = new SpeedConfig {
                            craftingSpeed = 1.0f,
                            researchSpeed = 10.0f,
                            recyclerSpeed = 5.0f,
                            gatherSpeed = false,
                            gatherConfig = new GatherConfig {
                                Resource = 0.0f,
                                ResourceBonus = 0.0f,
                                Pickup = 0.0f,
                                Growable = 0.0f,
                                Survey = 0.0f,
                                Quarry = 0.0f,
                                Excavator = 0.0f
                            },
                        },
                        lists = new ListsConfig {
                            itemsBlockCraft = true,
                            items = new List<string>(){
                                "rock",
                                "torch",
                                "apple"
                            },
                            itemsBlockDeployables = false,
                            itemsDeployables = new Dictionary<string, string>{
                                ["trap.bear"] = "assets/prefabs/deployable/bear trap/beartrap.prefab",
                                ["trap.landmine"] = "assets/prefabs/deployable/landmine/landmine.prefab"
                            },
                            commands = new List<string>(){
                                "o.load Trade",
                                "o.load Kits",
                                "o.load Shop"
                            }
                        }
                    },
                    new TierInfo {
                        name = "Stone Age",
                        icon = "https://img.rustspain.com/tiers/2.png",
                        color = "0.06 0.58 0.86 0.50",
                        durationMinutes = 24,
                        cupboards = new CupboardsConfig {
                            limitCupboard = false,
                            maxCupboards = 5,
                            enabledAlert = true,
                            ALERT = new DestructionConfig {
                                name = true,
                                pos = true
                            },
                            maxGrade = 1
                        },
                        workbench = new WorkbenchConfig {
                            researchWorkbench = true,
                            placeAllWorkbench = true,
                            onlyWorkbench = new List<string>(){
                                "assets/prefabs/deployable/tier 1 workbench/workbench1.deployed.prefab"
                            }
                        },
                        events = new EventsConfig {
                            cargoship = true,
                            oilrig = true,
                            chinook = true,
                            helicopter = true,
                            supplysignal = true,
                            bradley = true
                        },
                        sellers = new SellersConfig {
                            helicopter = true,
                            boat = true,
                            horse = true,
                            drones = true,
                            vending = true
                        },
                        spawns = new SpawnConfig {
                            spawnCards = new List<string>(){
                                "keycard_green_pickup.entity",
                                "keycard_blue_pickup.entity",
                                "keycard_red_pickup.entity"
                            },
                            spawnVehicles = new List<string>(){
                                "2module_car_spawned.entity",
                                "3module_car_spawned.entity",
                                "4module_car_spawned.entity"
                            },
                            spawnMLRS = false,
                            spawnWORKCART = true,
                            spawnTRAIN = true
                        },
                        speed = new SpeedConfig {
                            craftingSpeed = 0.6f,
                            researchSpeed = 10.0f,
                            recyclerSpeed = 5.0f,
                            gatherSpeed = true,
                            gatherConfig = new GatherConfig {
                                Resource = 2.0f,
                                ResourceBonus = 2.0f,
                                Pickup = 2.0f,
                                Growable = 2.0f,
                                Survey = 2.0f,
                                Quarry = 2.0f,
                                Excavator = 2.0f
                            },
                        },
                        lists = new ListsConfig {
                            itemsBlockCraft = true,
                            items = new List<string>(){
                                "rock",
                                "torch",
                                "apple"
                            },
                            itemsBlockDeployables = false,
                            itemsDeployables = new Dictionary<string, string>{
                                ["trap.bear"] = "assets/prefabs/deployable/bear trap/beartrap.prefab",
                                ["trap.landmine"] = "assets/prefabs/deployable/landmine/landmine.prefab"
                            },
                            commands = new List<string>(){
                                "o.load Trade",
                                "o.load Kits",
                                "o.load Shop"
                            }
                        }
                    },
                    new TierInfo {
                        name = "Metal Age",
                        icon = "https://img.rustspain.com/tiers/3.png",
                        color = "0.87 0.75 0.05 0.50",
                        durationMinutes = 24,
                        cupboards = new CupboardsConfig {
                            limitCupboard = false,
                            maxCupboards = 5,
                            enabledAlert = true,
                            ALERT = new DestructionConfig {
                                name = true,
                                pos = true
                            },
                            maxGrade = 1
                        },
                        workbench = new WorkbenchConfig {
                            researchWorkbench = true,
                            placeAllWorkbench = true,
                            onlyWorkbench = new List<string>(){
                                "assets/prefabs/deployable/tier 1 workbench/workbench1.deployed.prefab",
                                "assets/prefabs/deployable/tier 2 workbench/workbench2.deployed.prefab"
                            }
                        },
                        events = new EventsConfig {
                            cargoship = true,
                            oilrig = true,
                            chinook = true,
                            helicopter = true,
                            supplysignal = true,
                            bradley = true
                        },
                        sellers = new SellersConfig {
                            helicopter = true,
                            boat = true,
                            horse = true,
                            drones = true,
                            vending = true
                        },
                        spawns = new SpawnConfig {
                            spawnCards = new List<string>(){
                                "keycard_green_pickup.entity",
                                "keycard_blue_pickup.entity",
                                "keycard_red_pickup.entity"
                            },
                            spawnVehicles = new List<string>(){
                                "2module_car_spawned.entity",
                                "3module_car_spawned.entity",
                                "4module_car_spawned.entity"
                            },
                            spawnMLRS = true,
                            spawnWORKCART = true,
                            spawnTRAIN = true
                        },
                        speed = new SpeedConfig {
                            craftingSpeed = 0.3f,
                            researchSpeed = 10.0f,
                            recyclerSpeed = 5.0f,
                            gatherSpeed = true,
                            gatherConfig = new GatherConfig {
                                Resource = 3.0f,
                                ResourceBonus = 3.0f,
                                Pickup = 3.0f,
                                Growable = 3.0f,
                                Survey = 3.0f,
                                Quarry = 3.0f,
                                Excavator = 3.0f
                            },
                        },
                        lists = new ListsConfig {
                            itemsBlockCraft = true,
                            items = new List<string>(){
                                "rock",
                                "torch",
                                "apple"
                            },
                            itemsBlockDeployables = false,
                            itemsDeployables = new Dictionary<string, string>{
                                ["trap.bear"] = "assets/prefabs/deployable/bear trap/beartrap.prefab",
                                ["trap.landmine"] = "assets/prefabs/deployable/landmine/landmine.prefab"
                            },
                            commands = new List<string>(){
                                "o.load Trade",
                                "o.load Kits",
                                "o.load Shop"
                            }
                        }
                    },
                    new TierInfo {
                        name = "Explosive Age",
                        icon = "https://img.rustspain.com/tiers/4.png",
                        color = "0.80 0.26 0.13 0.50",
                        durationMinutes = 24,
                        cupboards = new CupboardsConfig {
                            limitCupboard = false,
                            maxCupboards = 5,
                            enabledAlert = true,
                            ALERT = new DestructionConfig {
                                name = true,
                                pos = true
                            },
                            maxGrade = 1
                        },
                        workbench = new WorkbenchConfig {
                            researchWorkbench = true,
                            placeAllWorkbench = true,
                            onlyWorkbench = new List<string>(){
                                "assets/prefabs/deployable/tier 1 workbench/workbench1.deployed.prefab",
                                "assets/prefabs/deployable/tier 2 workbench/workbench2.deployed.prefab",
                                "assets/prefabs/deployable/tier 3 workbench/workbench3.deployed.prefab"
                            }
                        },
                        events = new EventsConfig {
                            cargoship = true,
                            oilrig = true,
                            chinook = true,
                            helicopter = true,
                            supplysignal = true,
                            bradley = true
                        },
                        sellers = new SellersConfig {
                            helicopter = true,
                            boat = true,
                            horse = true,
                            drones = true,
                            vending = true
                        },
                        spawns = new SpawnConfig {
                            spawnCards = new List<string>(){
                                "keycard_green_pickup.entity",
                                "keycard_blue_pickup.entity",
                                "keycard_red_pickup.entity"
                            },
                            spawnVehicles = new List<string>(){
                                "2module_car_spawned.entity",
                                "3module_car_spawned.entity",
                                "4module_car_spawned.entity"
                            },
                            spawnMLRS = true,
                            spawnWORKCART = true,
                            spawnTRAIN = true
                        },
                        speed = new SpeedConfig {
                            craftingSpeed = 0.0f,
                            researchSpeed = 10.0f,
                            recyclerSpeed = 5.0f,
                            gatherSpeed = true,
                            gatherConfig = new GatherConfig {
                                Resource = 5.0f,
                                ResourceBonus = 5.0f,
                                Pickup = 5.0f,
                                Growable = 5.0f,
                                Survey = 5.0f,
                                Quarry = 5.0f,
                                Excavator = 5.0f
                            },
                        },
                        lists = new ListsConfig {
                            itemsBlockCraft = true,
                            items = new List<string>(){
                                "rock",
                                "torch",
                                "apple"
                            },
                            itemsBlockDeployables = false,
                            itemsDeployables = new Dictionary<string, string>{
                                ["trap.bear"] = "assets/prefabs/deployable/bear trap/beartrap.prefab",
                                ["trap.landmine"] = "assets/prefabs/deployable/landmine/landmine.prefab"
                            },
                            commands = new List<string>(){
                                "o.load Trade",
                                "o.load Kits",
                                "o.load Shop"
                            }
                        }
                    },
                }
            };
        }

        protected override void LoadConfig(){
            base.LoadConfig();
            try {
                config = Config.ReadObject<ConfigData>();
                if (config == null){
                    LoadDefaultConfig();
                }
            } catch {
                PrintError("Configuration file is corrupt! Unloading plugin...");
                Interface.Oxide.RootPluginManager.RemovePlugin(this);
                return;
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig(){
            config = GetDefaultConfig();
        }

        protected override void SaveConfig(){
            Config.WriteObject(config);
        }
        #endregion

        #region Data
        private class TiersData {
            public int activeTier { get; set; }
            public Dictionary<ulong, int> armariosCount { get; set; } = new Dictionary<ulong, int>();
            public Dictionary<string, MarketPlaceData> marketplace { get; set; } = new Dictionary<string, MarketPlaceData>();
            public Dictionary<string, float> durationCraft { get; set; } = new Dictionary<string, float>();
        }

        private class MarketPlaceData {
            public Vector3 Position { get; set; }
            public Vector3 Rotation { get; set; }
        }

        void ClearData(){
            _Data.activeTier = 999;
            _Data.armariosCount.Clear();
            _Data.marketplace.Clear();
            _Data.durationCraft.Clear();
            SaveData();
        }

        private void SaveData(){
            _pluginData.WriteObject(_Data);
        }

        private void LoadData(){
            try{
                _Data = _pluginData.ReadObject<TiersData>();
            } catch {
                Puts("Couldn't load TiersMode Data, creating new datafile.");
                _Data = new TiersData();
                SaveData();
            }
        }
        #endregion

        #region Language
        protected override void LoadDefaultMessages() {
            lang.RegisterMessages(new Dictionary<string, string> {
                ["NotAllowed"] = "You do not have permission to use this command.",
                ["Close"] = "CLOSE",
                ["MaxLimitCup"] = "Maximum number of cupboards placed ({0}).",
                ["BuildBlock"] = "Blocked Placement",
                ["NoPlaceWorkbench"] = "You cannot currently place this level {0} workbench.",
                ["BlockVehicle"] = "Blocked Vehicle",
                ["MlrsDes"] = "Currently the MLRS vehicle is locked.",
                ["BlockUpgrade"] = "Building Upgrade Blocked",
                ["UpgradeDes"] = "<b>{0}</b> level upgrade is currently locked.",
                ["ResearchBlock"] = "Research Blocked",
                ["NoResearchWorkbench"] = "It cannot currently be researched from the Workbench.\nUse the research table.",
                ["BlockVending"] = "Vending Blocked",
                ["VendingBlock"] = "Vending machine under maintenance.",
                ["BlockVendor"] = "Seller Blocked",
                ["VendorDesHeli"] = "Currently it cannot sell you no <b>Helicopter</b>.",
                ["VendorDesBoat"] = "Currently it cannot sell you no <b>Boat</b>.",
                ["VendorDesHorse"] = "Currently it cannot sell you no <b>Horse</b>.\nYou'll have to look for horses out there.",
                ["CountTime"] = "Change of Tier in: ",
                ["ChangueTier"] = "The <color={2}>{0}</color> Tier is over. A new tier begins: <color={3}>{1}</color>",
                ["ChangueTierDiscord"] = "The **{0}** Tier is over.\nA new tier begins: **{1}**",
                ["InitTier"] = "Start a new Wipe with the: {0}",
                ["InitTierDiscord"] = "Start a new Wipe with the **{0}**\nJoin our Tier server now!",
                ["WelcomeChat"] = "Welcome {0}! This Server has a Tiers mode active. Currently the <color=#0FACF5>{1}</color> is active.\nDiscover how it works with the command: <color=#0FACF5>/tier</color>",
                ["ItemBlock"] = "Locked Item",
                ["ItemBlockDes"] = "At this tier you cannot use the selected item.",
                ["Wood"] = "Wood",
                ["Stone"] = "Stone",
                ["Metal"] = "Metal",
                ["TopTier"] = "HQ",
                ["Back"] = "< BACK",
                ["Next"] = "NEXT >",
                ["Info"] = "INFO",
                ["Days"] = "<b>{0}</b>d <b>{1}</b>h <b>{2}</b>m",
                ["Hours"] = "<b>{0}</b>h <b>{1}</b>m",
                ["Mins"] = "<b>{0}</b>m <b>{1}</b>s",
                ["Secs"] = "<b>{0}</b>s",
                ["NewTier"] = "New Tier",
                ["CurrentTier"] = "Current Tier:",
                ["TitleEditPanel"] = "Edit Block Items: Tier {0}",
                ["AddInventory"] = "Add Inventory",
                ["ClearList"] = "Clear List",
                ["ItemsList"] = "List of Blocked Items (they will be unlocked by leveling up):",
                ["LastTier"] = "<color=#f74d31>[ IMPORTANT ]</color>\nThis is the last level, it is normal not to add anything here so that everything is unlocked.\n\nIf you want to block something for the entire Wipe then yes.",
                ["TierBack"] = "Tier Back",
                ["TierNext"] = "Tier Next",
                ["PreviousTier"] = "Previus Tier",
                ["NextTier"] = "Next Tier",
                ["TitleInfoPanel"] = "Current Tier: {0}",
                ["TierConfig"] = "Current Tier Configuration",
                ["ItemsNextUnlock"] = "Items that will be unlocked in the next Tier:",
                ["ItemsUnlockAll"] = "All items in the game are unlocked.",
                ["ServerEvents"] = "Server Events",
                ["Event_HackableCrate"] = "A Hackable Crate has just appeared on an oil rig.",
                ["Event_CH47"] = "A Chinook just entered the map.",
                ["Event_PatrolHelicopter"] = "A Patrol Helicopter has just entered the map.",
                ["Event_Airdrop"] = "A plane will drop a payload shortly.",
                ["Event_Cargoship"] = "Yes, you heard it right. The cargo ship is here.",
                ["Event_Bradley"] = "A Bradley Tank has begun patrolling the Launch Site.",
                ["NoBuilding"] = "You cannot place without a Build Zone.",
                ["Unspecified"] = "You have not specified any message to display.",
                ["TierAlertTitle"] = "Notification",
                ["CupboardDestroyed"] = "Cupboard Destroyed",
                ["CupboardDestInfo"] = "A cupboard has just been destroyed.",
                ["CupboardDestInfoPos"] = "A Cupboard has just been destroyed in: {0}",
                ["CupboardDestInfoPlayer"] = "A Cupboard was just destroyed by {0}.",
                ["CupboardDestInfoPosPlayer"] = "A Cupboard was just destroyed by {1} in: {0}",
                ["ItemsBlock"] = "Items Block",
                ["ItemsBlockDes"] = "Items unlocked in this tier:",
                ["Building"] = "Building",
                ["BuildingDes"] = "Cupboards, Upgrade...",
                ["Workbench"] = "Workbench",
                ["WorkbenchDes"] = "Limitations of Use",
                ["Events"] = "Events",
                ["EventsDes"] = "Cargoship, Drops, Helicopters, Oil Rig,...",
                ["Vending"] = "Vending",
                ["VendingDes"] = "Horses, Boats, Helicopters, Drones...",
                ["Spawn"] = "Spawn",
                ["SpawnDes"] = "Access Cards, Cars, MLRS,...",
                ["Speed"] = "Speed",
                ["SpeedDes"] = "Crafting, Research, Recycling, Gather,...",
                ["CupboardsMax"] = "Maximum Cupboards to place",
                ["MaxUpgrade"] = "Maximum Building Upgrade Level",
                ["ResearchWorkbench"] = "Research from the Workbench",
                ["MaxWorkbenchLevel"] = "Max Workbench Level",
                ["Cargoship"] = "Cargoship",
                ["OilRig"] = "Oil Rig",
                ["Chinook"] = "CH47 Helicopter",
                ["PatrolHelicopter"] = "Patrol Helicopter",
                ["Airdrop"] = "Airdrop",
                ["BradleyAPC"] = "Bradley APC",
                ["HelicopterSale"] = "Helicopter Sales",
                ["BoatSales"] = "Boat Sales",
                ["HorseSales"] = "Horse Sales",
                ["UseofDrones"] = "Use of Drones",
                ["UseofVending"] = "Use of Vending",
                ["SpawnCards"] = "Access Card Spawn",
                ["SpawnVehicles"] = "Vehicle Spawn",
                ["UseVehicleMLRS"] = "Use of the MLRS Vehicle",
                ["UseVehicleWORKCART"] = "Use of the Workcart Vehicle",
                ["UseVehicleTRAIN"] = "Use of the Train Vehicle",
                ["CraftingRate"] = "Crafting Speed",
                ["ResearchSeconds"] = "Research Speed (Seconds)",
                ["RecyclerSeconds"] = "Recycler Speed (Seconds)",
                ["GatherSystem"] = "Gather System",
                ["keycard_red"] = "Red Access Card",
                ["keycard_blue"] = "Blue Access Card",
                ["keycard_green"] = "Green Access Card",
                ["4module_car_spawned"] = "4 Module Vehicles",
                ["3module_car_spawned"] = "3 Module Vehicles",
                ["2module_car_spawned"] = "2 Module Vehicles",
                ["nocars"] = "No Vehicles on the map",
                ["Enabled"] = "<color=#2AF561>Enabled</color>",
                ["Disabled"] = "<color=#F5492A>Disabled</color>",
                ["workbench1"] = "Level 1 Workbench",
                ["workbench2"] = "Level 2 Workbench",
                ["workbench3"] = "Level 3 Workbench",
                ["ChangueLevel"] = "{0} <color=#F5492A>➔➔➔</color> {1}",
                ["UpLevel"] = "Upgrade Level",
                ["Unlimited"] = "Unlimited"
            }, this);
        }

        private string Languaje(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void PrintToChat(BasePlayer player, string message) => Player.Message(player, "<color=#f74d31>[TiersMode]</color> " + message);
        #endregion
    }
}