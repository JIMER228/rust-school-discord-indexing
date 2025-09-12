using System;
using Facepunch;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins{

    [Info("QuarryLevels", "ninco90", "1.4.91")]
    [Description("Improve the quarries by levels.")]
    public class QuarryLevels : RustPlugin {

        #region Fields
        [PluginReference] Plugin ServerRewards, Economics, NoEscape;

        private const string permUse = "quarrylevels.use";
        private const string permNoCost = "quarrylevels.nocost";
        private const string permAdmin = "quarrylevels.admin";
        private string soundupgrade = "assets/prefabs/deployable/quarry/effects/mining-quarry-deploy.prefab";
        private string soundupgrade2 = "assets/bundled/prefabs/fx/build/promote_toptier.prefab";
        private string sounderror = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab";
        private List<NetworkableId> quarrys_on = new List<NetworkableId>();
        private List<NetworkableId> quarrys_off = new List<NetworkableId>();
        private readonly List<Timer> timers = new List<Timer>();

        private Dictionary<ulong, int> quarrysPlayers = new Dictionary<ulong, int>();
        private Dictionary<ulong, int> quarrysOilPlayers = new Dictionary<ulong, int>();

        private const string elemq1 = "quarrylevels.main";
        private const string elemq2 = "quarrylevels.error";
        private const string elemq4 = "quarrylevels.items";
        private const string elemq5 = "quarrylevels.fuel";
        #endregion

        private void Init(){
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(CanBuild));

            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permNoCost, this);
            permission.RegisterPermission(permAdmin, this);
            cmd.AddConsoleCommand("quarrylevels.up", this, nameof(cmdInfoConsole));
            cmd.AddConsoleCommand("quarrylevels.close", this, nameof(cmdCloseConsole));
            cmd.AddConsoleCommand("quarrylevels.close2", this, nameof(cmdClose2Console));
        }

        private void OnServerInitialized(){
            Subscribe(nameof(OnEntitySpawned));

            foreach (var quarry in BaseNetworkable.serverEntities.OfType<MiningQuarry>()){
                if (quarry == null) continue;
                UpdateConfig(quarry);
            }
            
            if (config.Notify.notifystop) timer.Repeat(config.Notify.stoprefresh, 0, Refresh);
            if(config.maxQuarrys != 0 || config.maxOilQuarrys != 0){
                Subscribe(nameof(CanBuild));
            }
        }

        private void OnLootEntity(BasePlayer player, StorageContainer entity){
            if (!config.upgradeMap && entity.OwnerID == 0) return;
            if(entity.ShortPrefabName == "hopperoutput" || entity.ShortPrefabName == "crudeoutput"){
            	if (permission.UserHasPermission(player.UserIDString, permUse)) OpenPanel(player);
            }
        }

        private void OnPlayerLootEnd(PlayerLoot inventory){
            BasePlayer player = inventory.GetComponent<BasePlayer>();
            if (player != null) ClosePanel(player);
        }

        private bool? CanBuild(Planner planner, Construction prefab, Construction.Target target) {
        	BasePlayer player = planner.GetOwnerPlayer();
            if (player == null) return null;
            if (prefab.deployable == null || prefab.prefabID != 672916883 && prefab.prefabID != 1599225199) return null; //player.IsAdmin ||
            if(prefab.prefabID == 672916883){
                if(!quarrysPlayers.ContainsKey(player.userID)) quarrysPlayers.Add(player.userID, 0);
                int count = quarrysPlayers[player.userID];
                //planner.GetOwnerPlayer().ChatMessage("Quarrys Existentes: " + count);
                if (count - config.maxQuarrys > 1) PrintWarning($"PLAYER {player.displayName} has {count-1}x Quarry !");
                if (count >= config.maxQuarrys){
                    planner.GetOwnerPlayer().ShowToast(GameTip.Styles.Red_Normal, Lan("MaxLimit", player.UserIDString));
                    planner.GetOwnerPlayer().ChatMessage(Lan("MaxLimit", player.UserIDString));
                    return false;
                }
            } else {
                if(!quarrysOilPlayers.ContainsKey(player.userID)) quarrysOilPlayers.Add(player.userID, 0);
                int count = quarrysOilPlayers[player.userID];
                if (count - config.maxOilQuarrys > 1) PrintWarning($"PLAYER {player.displayName} has {count-1}x Oil Quarry !");
                if (count >= config.maxOilQuarrys){
                    planner.GetOwnerPlayer().ShowToast(GameTip.Styles.Red_Normal, Lan("MaxLimitOil", player.UserIDString));
                    planner.GetOwnerPlayer().ChatMessage(Lan("MaxLimitOil", player.UserIDString));
                    return false;
                }
            }
            return null;
        }

        private void OnResourceDepositCreated(ResourceDepositManager.ResourceDeposit resourceDeposit){
        	if (resourceDeposit._resources == null) return;
            if (UnityEngine.Random.Range(0f, 100f) >= config.oilChance || config.oilChance == 0f || config.addCrude) {
                if (config.Debug) Puts("Deposit Created Quarry Default");
                if (config.allResources){
                    resourceDeposit._resources.Clear();
                    resourceDeposit.Add(ItemManager.FindItemDefinition("stones"), 1f, 1000, config.Extract["stones"], ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, false);
                    resourceDeposit.Add(ItemManager.FindItemDefinition("metal.ore"), 1f, 1000, config.Extract["metal.ore"], ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, false);
                    resourceDeposit.Add(ItemManager.FindItemDefinition("sulfur.ore"), 1f, 1000, config.Extract["sulfur.ore"], ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, false);
                    resourceDeposit.Add(ItemManager.FindItemDefinition("hq.metal.ore"), 1f, 1000, config.Extract["hq.metal.ore"], ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, false);
                    if(config.addCrude) resourceDeposit.Add(ItemManager.FindItemDefinition("crude.oil"), 1f, 1000, config.Extract["crude.oil"], ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, false);
                }
                return;
            } else {
                if (config.Debug) Puts("Deposit Created Quarry Oil");
                resourceDeposit._resources.Clear();
                resourceDeposit.Add(ItemManager.FindItemDefinition("crude.oil"), 1f, 1000, config.Extract["crude.oil"], ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, false);
                return;
            }
        }

        private void OnSurveyGather(SurveyCharge survey, Item item){
            if (item.info.name == "crude_oil.item" && !config.addCrude){
                Vector3 pos = survey.transform.position;
                Delete(pos);
                timer.Once(0.1f, () => {
                    pos.y = pos.y-0.20f;
                    BaseEntity replacement = GameManager.server.CreateEntity("assets/prefabs/tools/surveycharge/survey_crater_oil.prefab", pos) as BaseEntity;
                    if (replacement == null) return;
                    replacement.Spawn();
                    Delete(pos, 200f);
                });
                return;
            }
        }

        void OnQuarryGather(MiningQuarry quarry, Item item){
			var entity2 = quarry.hopperPrefab.instance.GetComponent<StorageContainer>();
            if (entity2.ShortPrefabName == "hopperoutput" && entity2.inventory.capacity == config.Mining.Default.slots || entity2.ShortPrefabName == "crudeoutput" && entity2.inventory.capacity == config.Oil.Default.slots){
                GatherMultiplier(item, 1.0f);
                return;
            } else {
                var i = 0;
                var levels = entity2.ShortPrefabName == "hopperoutput" ? config.Mining.Levels : config.Oil.Levels;
                foreach (var entry in levels){
                    i = i+1;
                    if (entity2.inventory.capacity == entry.Info.slots){
                        //Puts("GatherMultiplier "+quarry.ShortPrefabName+": " + quarry.transform.position + " Rate: " + entry.Info.rate + " Level:" + i);
                        GatherMultiplier(item, entry.Info.rate);
                        return;
                    }
                }
            }
            return;
		}

        private void OnEntitySpawned(MiningQuarry entity){
            if (config.Debug) Puts("MiningQuarry Place Info: "+ entity.ShortPrefabName +" Liquid: " + entity.canExtractLiquid + " Solid: " + entity.canExtractSolid);
            var storage = entity.hopperPrefab.instance.GetComponent<StorageContainer>();
            if (storage.ShortPrefabName == "hopperoutput"){
                entity.health = entity._maxHealth = config.Mining.Default.health;
                entity.workPerFuel = config.Mining.Default.workPerFuel;
                storage.inventory.capacity = config.Mining.Default.slots;
            }
            if (storage.ShortPrefabName == "crudeoutput"){
                entity.canExtractSolid = true;
                entity.health = entity._maxHealth = config.Oil.Default.health;
                entity.workPerFuel = config.Oil.Default.workPerFuel;
                storage.inventory.capacity = config.Oil.Default.slots;
            }
            storage.OwnerID = entity.OwnerID;
            storage.SendNetworkUpdateImmediate();
            
            var fuelStorage = entity.fuelStoragePrefab.instance as StorageContainer;
            fuelStorage.inventory.canAcceptItem = (item, amount) => item.info.shortname == (entity.ShortPrefabName.Contains("pumpjack") ? config.Oil.fuel : config.Mining.fuel);
            fuelStorage.OwnerID = entity.OwnerID;
            
            SetupProtection(entity);
            entity.SendNetworkUpdateImmediate();
            timer.Once(2f, () => DepostitEntity(entity));
        }

        private void UpdateConfig(MiningQuarry entity){
            var configDefault = config.Mining.Default;
            var configLevels = config.Mining.Levels;
            if (entity.ShortPrefabName.Contains("pumpjack")){
                configDefault = config.Oil.Default;
                configLevels = config.Oil.Levels;
                if(!quarrysOilPlayers.ContainsKey(entity.OwnerID)){
                    quarrysOilPlayers.Add(entity.OwnerID, 1);
                } else {
                    quarrysOilPlayers[entity.OwnerID]++;
                }
                entity.canExtractSolid = true;
                entity.canExtractLiquid = true;
            } else {
                if(!quarrysPlayers.ContainsKey(entity.OwnerID)){
                    quarrysPlayers.Add(entity.OwnerID, 1);
                } else {
                    quarrysPlayers[entity.OwnerID]++;
                }
                entity.canExtractSolid = true;
                entity.canExtractLiquid = false;
            }

            var entity3 = entity.hopperPrefab.instance.GetComponent<StorageContainer>();
            entity3.OwnerID = entity.OwnerID;
            entity3.SendNetworkUpdateImmediate();

            timer.Once(2f, () => DepostitEntity(entity));

            if (entity3.inventory.capacity == configDefault.slots){
                entity.workPerFuel = configDefault.workPerFuel;
                var missing = configDefault.health - entity.Health();
                if (missing != 0){
                    entity.Heal(missing);
                    entity.health = entity._maxHealth = configDefault.health;
                    entity.OnRepair();
                }
                entity.SendNetworkUpdateImmediate();
            } else {
                foreach (var entry in configLevels){
                    if (entity3.inventory.capacity == entry.Info.slots){
                        entity.workPerFuel = entry.Info.workPerFuel;
                        var missing = entry.Info.health - entity.Health();
                        if (missing != 0){
                            entity.Heal(missing);
                            entity.health = entity._maxHealth = entry.Info.health;
                            entity.OnRepair();
                        }
                        entity.SendNetworkUpdateImmediate();
                        return;
                    }
                }
            }
        }

        private void DepostitEntity(MiningQuarry quarry){
            if(quarry == null){
                if (config.Debug) Puts("Error Update Quarry");
                return;
            }

            var fuel = quarry.fuelStoragePrefab.instance as StorageContainer;
            fuel.inventory.canAcceptItem = (item, amount) => item.info.shortname == (quarry.ShortPrefabName.Contains("pumpjack") ? config.Oil.fuel : config.Mining.fuel);
            fuel.SendNetworkUpdateImmediate();
            
            if (config.Debug) Puts("Updating "+quarry.ShortPrefabName+" " + quarry.transform.position + " Owner: " + quarry.OwnerID + " | Static: " + quarry.isStatic + " | Liquid: " + quarry.canExtractLiquid + " | Solid: " + quarry.canExtractSolid);

            ResourceDepositManager.ResourceDeposit resourceDeposit = quarry._linkedDeposit;
            if (quarry.ShortPrefabName.Contains("pumpjack")){
                resourceDeposit._resources.Clear();
                resourceDeposit.Add(ItemManager.FindItemDefinition("crude.oil"), 1f, 1000, config.Extract["crude.oil"], ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, false);
            } else {
                if (config.allResources){
                    
                    resourceDeposit._resources.Clear();
                    resourceDeposit.Add(ItemManager.FindItemDefinition("stones"), 1f, 1000, config.Extract["stones"], ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, false);
                    resourceDeposit.Add(ItemManager.FindItemDefinition("metal.ore"), 1f, 1000, config.Extract["metal.ore"], ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, false);
                    resourceDeposit.Add(ItemManager.FindItemDefinition("sulfur.ore"), 1f, 1000, config.Extract["sulfur.ore"], ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, false);
                    resourceDeposit.Add(ItemManager.FindItemDefinition("hq.metal.ore"), 1f, 1000, config.Extract["hq.metal.ore"], ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, false);
                    if(config.addCrude) resourceDeposit.Add(ItemManager.FindItemDefinition("crude.oil"), 1f, 1000, config.Extract["crude.oil"], ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, false);
                } else {
                    var crudeOil = ItemManager.FindItemDefinition("crude.oil");
                    var lowGradeFuel = ItemManager.FindItemDefinition("lowgradefuel");
                    resourceDeposit._resources.RemoveAll(resource => resource.type == crudeOil || resource.type == lowGradeFuel);
                }
            }

            quarry.SendNetworkUpdateImmediate();

            if (config.Debug){
                var resources = "Resource: ";
                foreach (ResourceDepositManager.ResourceDeposit.ResourceDepositEntry resourceDepositEntry in quarry._linkedDeposit._resources){
                    resources += resourceDepositEntry.type.shortname + " ";
                }
                Puts(resources);
            }
        }

        private void OnQuarryToggled(MiningQuarry quarry, BasePlayer player){
            if (!CheckFuel(quarry)){
                quarry.SetOn(false);
            	var item = ItemManager.FindItemDefinition(quarry.ShortPrefabName.Contains("pumpjack") ? config.Oil.fuel : config.Mining.fuel);
                if (config.Notify.notifychat) PrintToChat(player, Lan("NoFuel", player.UserIDString, item.displayName.english));
                if (config.Notify.notifygui) OpenPanelError(player, Lan("NoFuel", player.UserIDString, item.displayName.english));
                return;
            }
        }

        private object OnQuarryConsumeFuel(MiningQuarry quarry, Item item){
            if (quarry == null) return item;
            var fuel = quarry.fuelStoragePrefab.instance as StorageContainer;
            var result = fuel.inventory.FindItemByItemName(quarry.ShortPrefabName.Contains("pumpjack") ? config.Oil.fuel : config.Mining.fuel);
            if (result != null) return result;
            return null;
        }

        private bool CheckFuel(MiningQuarry quarry){
            var fuel = quarry.fuelStoragePrefab.instance as StorageContainer;
            var result = fuel.inventory.FindItemByItemName (quarry.ShortPrefabName.Contains("pumpjack") ? config.Oil.fuel : config.Mining.fuel);
            return result != null;
        }

        private void SetupProtection(BaseCombatEntity quarry){
            float x = config.Damage.damagePercentage;
            quarry.baseProtection = ScriptableObject.CreateInstance<ProtectionProperties>();
            if (config.Damage.block == true){
            	Puts("Daño bloqueado total!");
                quarry.baseProtection.amounts = new float[]{
                    1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1
                };
                return;
            }
            if (config.Damage.damageOnly == true){
            	Puts("Melee Damage Only (Blocks damage from Fire, Explosives and Bullets)");
                quarry.baseProtection.amounts = new float[]{
                    1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,x,1,1,1,1,1,1
                };
                return;
            }
            quarry.baseProtection.amounts = new float[]{
                1,1,1,1,1,1,1,1,1,x,1,1,1,1,1,x,x,1,1,1,1,x
            };
        }

        private void OnHammerHit(BasePlayer player, HitInfo info){
            var entity = info.HitEntity?.GetComponent<BaseCombatEntity>();
            if (entity == null || entity.SecondsSinceAttacked < 30) return;
            if (!config.upgradeMap && entity.OwnerID == 0) return;
            if (IsNoEscapeBlocked(player)) return;
            if (entity is BaseResourceExtractor){
                var entity2 = entity.GetComponent<MiningQuarry>();
                var entity3 = entity2.hopperPrefab.instance.GetComponent<StorageContainer>();
                var configDefault = config.Mining.Default;
                var configLevels = config.Mining.Levels;
                if (entity.ShortPrefabName.Contains("pumpjack")){
                    configDefault = config.Oil.Default;
                    configLevels = config.Oil.Levels;
                }
                if (entity3.inventory.capacity == configDefault.slots){
                    var missing = configDefault.health - entity.Health();
                    if (missing == 0) return;
                    entity2.Heal(missing);
                    entity2.health = entity2._maxHealth = configDefault.health;
                    entity2.OnRepair();
                } else {
                    foreach (var entry in configLevels){
                        if (entity3.inventory.capacity == entry.Info.slots){
                            var missing = entry.Info.health - entity.Health();
                            if (missing == 0) return;
                            entity2.Heal(missing);
                            entity2.health = entity2._maxHealth = entry.Info.health;
                            entity2.OnRepair();
                            return;
                        }
                    }
                }
            }
        }

        private void Unload(){
            foreach (var player in BasePlayer.activePlayerList){
                ClosePanel(player);
            }        
        }

        [ChatCommand("qldefault")]
        private void QuarryDefault(BasePlayer player, string command, string[] args){
            if (permission.UserHasPermission(player.UserIDString, permAdmin)){
                var entity = FindEntity(player);
                if (entity != null && entity.GetComponent<MiningQuarry>()){
                    var entity2 = entity.GetComponent<MiningQuarry>();
                    var entity3 = entity2.hopperPrefab.instance.GetComponent<StorageContainer>();
                    var defaultQuarry = entity3.ShortPrefabName == "hopperoutput" ? config.Mining.Default : config.Oil.Default;
                    if (config.Sounds){ Effect.server.Run(soundupgrade, entity.transform.position); Effect.server.Run(soundupgrade2, entity.transform.position);}
                    entity3.inventory.capacity = defaultQuarry.slots;
                    entity2.workPerFuel = defaultQuarry.workPerFuel;
                    entity2.health = entity2._maxHealth = defaultQuarry.health;
                    var missing = entity2.MaxHealth() - entity2.Health();
                    entity2.Heal(missing);
                    entity2.OnRepair();
                } else {
                    PrintToChat(player, Lan("LookQuarry", player.UserIDString));
                }
            } else {
                PrintToChat(player, Lan("NotAllowed", player.UserIDString));
            }
        }

        #region Functions
        private BaseEntity FindEntity(BasePlayer player){
            var currentRot = Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward;
            Vector3 eyesAdjust = new Vector3(0f, 1.5f, 0f);
            var rayResult = Ray(player.transform.position + eyesAdjust, currentRot);
            if (rayResult is BaseEntity){
                var target = rayResult as BaseEntity;
                return target;
            }
            return null;
        }

        private object Ray(Vector3 Pos, Vector3 Aim){
            var hits = Physics.RaycastAll(Pos, Aim);
            float distance = 100f;
            object target = null;
            foreach (var hit in hits){
                if (hit.collider.GetComponentInParent<BaseEntity>() != null){
                    if (hit.distance < distance){
                        distance = hit.distance;
                        target = hit.collider.GetComponentInParent<BaseEntity>();
                    }
                }
            }
            return target;
        }

        private string getGrid(Vector3 pos) {
			char letter = 'A';
			var x = Mathf.Floor((pos.x+(ConVar.Server.worldsize/2)) / 146.3f)%26;
			var z = (Mathf.Floor(ConVar.Server.worldsize/146.3f)-1)-Mathf.Floor((pos.z+(ConVar.Server.worldsize/2)) / 146.3f);
			letter = (char)(((int)letter)+x);
			return $"{letter}{z}";
		}

        private bool IsNoEscapeBlocked(BasePlayer player){
            if (NoEscape == null) return false;
            if (permission.UserHasPermission(player.UserIDString, "noescape.raid.repairblock") && NoEscape.Call<bool>("IsRaidBlocked", player.UserIDString)) return true;
            return permission.UserHasPermission(player.UserIDString, "noescape.combat.repairblock") && NoEscape.Call<bool>("IsCombatBlocked", player.UserIDString);
        }

        private int CheckRewardsPoints(ulong id, bool money){
            if (money){
                return ServerRewards?.Call<int>("CheckPoints", id) ?? 0;
            } else {
                return Economics?.Call<int>("Balance", id) ?? 0;
            }
        }

        private void TakeRewardsPoints(ulong id, int amount, bool money){
            if (money){
                ServerRewards?.Call("TakePoints", id, amount);
            } else {
                Economics?.Call("Withdraw", id, Convert.ToDouble(amount));
            }
        }

        private void Delete(Vector3 position, float time = 0f){
            timers.Add(timer.Once(time, () => {
                var nearby = Pool.Get<List<SurveyCrater>>();
                Vis.Entities(position, 1f, nearby);
                foreach (SurveyCrater ent in nearby){
                    if (ent.PrefabName.Contains("survey_crater")) ent.KillMessage();
                }
                Pool.FreeUnmanaged(ref nearby);
            }));
        }

        private void GatherMultiplier(Item item, float mult) {			
            //Puts("Amount: " + item.amount + " . " + mult);
			item.amount = (int)(item.amount * mult);
        }

        private void Refresh(){
            foreach (var entity in BaseNetworkable.serverEntities.OfType<MiningQuarry>()){
                if (entity == null) continue;
                var entityId = entity.net.ID;
                if (entity.HasFlag(BaseEntity.Flags.On)){
                    if (!quarrys_on.Contains(entityId)){
                        quarrys_on.Add(entityId);
                        quarrys_off.Remove(entityId);
                    }
                } else {
                    if (!quarrys_off.Contains(entityId)){
                        quarrys_off.Add(entityId);
                        if (quarrys_on.Contains(entityId)){
                            var player = BasePlayer.FindByID(entity.OwnerID);
                            if (player != null){
                                string gridPosition = getGrid(entity.transform.position);
                                string message = Lan("StopQuarry", player.UserIDString, gridPosition);
                                if (config.Notify.notifychat) PrintToChat(player, message);
                                if (config.Notify.notifygui) OpenPanelError(player, message);
                            }
                            quarrys_on.Remove(entityId);
                        }
                    }
                }
            }
        }

        private void cmdOpenAuthsChat(BasePlayer player, string command, string[] args){
            OpenPanel(player);
        }
        private void cmdInfoConsole(ConsoleSystem.Arg arg){
            cmdInfo(arg.Player(), string.Empty, arg.Args);
        }
        private void cmdCloseConsole(ConsoleSystem.Arg arg){
            ClosePanel(arg.Player());
        }

        private void cmdClose2Console(ConsoleSystem.Arg arg){
            CuiHelper.DestroyUi(arg.Player(), elemq2);
        }

        private void cmdInfo(BasePlayer player, string command, string[] args){
            var eID = new NetworkableId(Convert.ToUInt32(args[0]));
            var level = Convert.ToInt32(args[1]);
            var entity = BaseNetworkable.serverEntities.Find(eID)?.GetComponent<MiningQuarry>();
            if (entity == null) return;
            var entity2 = entity.hopperPrefab.instance.GetComponent<StorageContainer>();
            var info = entity2.ShortPrefabName == "hopperoutput" ? config.Mining : config.Oil;
            if (CanUpdate(player, info.Levels[level].List, Boolean.Parse(args[2]), info.Levels[level].Info.rp, info.money)){
                if (config.Sounds){ Effect.server.Run(soundupgrade, entity2.transform.position); Effect.server.Run(soundupgrade2, entity.transform.position);}
                entity.workPerFuel = info.Levels[level].Info.workPerFuel;
                entity2.inventory.capacity = info.Levels[level].Info.slots;
                entity.health = entity._maxHealth = info.Levels[level].Info.health;
                var missing = entity.MaxHealth() - entity.Health();
                entity.Heal(missing);
                entity.OnRepair();
                ClosePanel(player);
                player.EndLooting();
            }
        }
        #endregion

        #region Resources
        private class IngredientData{
            public int amount;
            public List<Item> items = new List<Item>();
        }

        private bool CanUpdate(BasePlayer player, Dictionary<string, int> cost, bool rp, int rplevel, bool money) {
            var playerInventoryAllItems = Pool.Get<List<Item>>();
            player.inventory.GetAllItems(playerInventoryAllItems);
            
            var existing = GetExistingIngredients(playerInventoryAllItems);
            var id = player.userID;

            if (permission.UserHasPermission(player.UserIDString, permNoCost) == true) return true;
            if (rp){
                if (CheckRewardsPoints(id, money) < rplevel){
                    OpenPanelError(player, Lan("NoRP", player.UserIDString));
                    Pool.FreeUnmanaged(ref playerInventoryAllItems);
                    return false;
                } else {
                    TakeRewardsPoints(id, rplevel, money);
                    Pool.FreeUnmanaged(ref playerInventoryAllItems);
                    return true;
                } 
            }
            
            if (HasIngredients(existing, cost, id)){
                TakeIngredients(existing, cost, id);
                Pool.FreeUnmanaged(ref playerInventoryAllItems);
                return true;
            } else {
                OpenPanelError(player, Lan("NoResources", player.UserIDString));
                Pool.FreeUnmanaged(ref playerInventoryAllItems);
                return false;
            }
        }

        private Dictionary<string, IngredientData> GetExistingIngredients(List<Item> items) {
            var existing = new Dictionary<string, IngredientData>();
            foreach (var item in items) {
                var skin = item.skin;
                var name = skin == 0 ? item.info.shortname : skin.ToString();
                existing.TryAdd(name, new IngredientData());
                existing[name].amount += item.amount;
                existing[name].items.Add(item);
            }
            return existing;
        }

        private bool HasIngredients(Dictionary<string, IngredientData> existing, Dictionary<string, int> cost, ulong id) {
            foreach (var ingredient in cost) {
                var NameCost = ingredient.Key.ToLower();
                var TotalAmount = ingredient.Value;
                if (!existing.ContainsKey(NameCost)) return false;
                if (existing[NameCost].amount < TotalAmount) return false;
            }
            return true;
        }

        private void TakeIngredients(Dictionary<string, IngredientData> existing, Dictionary<string, int> cost, ulong id) {
            foreach (var ingredient in cost) {
                var NameCost = ingredient.Key;
                var TotalAmount = ingredient.Value;
                var items = existing[NameCost].items;
                foreach (var item in items) {
                    if (TotalAmount == 0) {
                        break;
                    }
                    if (item.amount > TotalAmount) {
                        item.amount -= TotalAmount;
                        break;
                    } else {
                        TotalAmount -= item.amount;
                        item.GetHeldEntity()?.Kill();
                        item.DoRemove();
                    }
                }
            }
        }
        #endregion

        #region GUI
        private void OpenPanel(BasePlayer player){
            var container = new CuiElementContainer();
            container.AddRange(GetPanel(player));
            CuiHelper.DestroyUi(player, elemq1);
            CuiHelper.AddUi(player, container);
        }

        private void OpenPanelError(BasePlayer player, string textshow){
            if (config.Sounds){Effect.server.Run(sounderror, player.transform.position);}
            var container = new CuiElementContainer();
            container.AddRange(GetPanel2(player, textshow));
            CuiHelper.DestroyUi(player, elemq2);
            CuiHelper.AddUi(player, container);
            timer.Once(8, () => CuiHelper.DestroyUi(player, elemq2));
        }

        private void ClosePanel(BasePlayer player){
            CuiHelper.DestroyUi(player, elemq1);
        }

        int GetItemIdFromShortname(string shortname){
            ItemDefinition itemDefinition = ItemManager.FindItemDefinition(shortname);
            if (itemDefinition != null){
                return itemDefinition.itemid;
            }
            return -1;
        }

        //Panel Main
        private CuiElementContainer GetPanel(BasePlayer player){
            var entitydepos = player.inventory.loot.entitySource?.GetComponent<StorageContainer>();
            var entityquarry = entitydepos.parentEntity.Get(true);
            
            MiningQuarry quarry = entityquarry.GetComponent<MiningQuarry>();

            var info = config.Mining;
            var improve = Lan("Improve", player.UserIDString);
            if (quarry.ShortPrefabName.Contains("pumpjack")){
                info = config.Oil;
                improve = Lan("ImproveOil", player.UserIDString);
            }

            var i = 0;
            var levelr = 0;
            if (entitydepos.inventory.capacity == info.Default.slots){
                levelr = 0;
            } else {
                i = 0;
                foreach (var entry in info.Levels){
                    i = i+1;
                    if (entitydepos.inventory.capacity == entry.Info.slots){
                        levelr = i;
                    }
                }
            }

            var level = levelr + 1;
            var leveltotal = info.Levels.Count;
            var container = new CuiElementContainer();

            container.Add(new CuiElement {
                Name = elemq1,
                Parent = "Hud.Menu",
                Components = {
                    new CuiImageComponent {
                        FadeIn = 0.2f,
                        Color = "0.42 0.42 0.42 0.2",
                        Material = "assets/content/ui/namefontmaterial.mat"
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-200 4",
                        OffsetMax = "180 270"
                    }
                }
            });

            container.Add(new CuiElement {
                Parent = elemq1,
                Components = {
                    new CuiTextComponent {
                        FadeIn = 0.2f,
                        Color = "1 1 1 0.9",
                        Text = improve,
                        FontSize = 20,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = info.onlyrp ? "-200 70" : "-200 95",
                        OffsetMax = info.onlyrp ? "200 100" : "200 125",
                    }
                }
            });

            i = 0;
            var textodes2 = "";
            bool onlyrpbool = true;
            var sizeX = 45;
            var sizeY = 45;
            var startX = config.position;
            var startY = -5;
            var x = startX;
            var y = startY;
            if (levelr < leveltotal){
                var slots = info.Levels[levelr].Info.slots;
                var speed = info.Levels[levelr].Info.rate;
                var rp = info.Levels[levelr].Info.rp;
                var health = info.Levels[levelr].Info.health;

                if (!info.onlyrp){
                    foreach (var entry in info.Levels[levelr].List){
                        i = i+1;
                        if (i != 0 && i % 7 == 0){
                            x = startX;
                            y -= sizeY + 15;
                        }

                        string ID = entry.Key;
                        int texto = entry.Value;

                        container.Add(new CuiElement {
                            Name = elemq4,
                            Parent = elemq1,
                            Components = {
                                new CuiImageComponent {
                                    FadeIn = 0.2f,
                                    ItemId = GetItemIdFromShortname(ID), 
                                    SkinId = 0
                                },
                                new CuiRectTransformComponent{
                                    AnchorMin = "0.5 0.5",
                                    AnchorMax = "0.5 0.5",
                                    OffsetMin = $"{x} {y-sizeY}",
                                    OffsetMax = $"{x + sizeX} {y}"
                                }
                            }
                        });
                        
                        container.Add(new CuiButton {
                            Button = {
                                FadeIn = 0.2f,
                                Color = "0 0 0 0.5",
                                Material = "assets/content/ui/namefontmaterial.mat"
                            },
                            Text = {
                                Text = texto.ToString(),
                                Align = TextAnchor.MiddleCenter,
                                FontSize = 12,
                                Font = "robotocondensed-regular.ttf"
                            },
                            RectTransform = {
                                AnchorMin = "0.1 -0.32",
                                AnchorMax = "0.9 0"
                            }
                        }, elemq4);
                        
                        x += sizeX + 5;
                    }
                }

                if (!info.onlyingredients){
                    container.Add(new CuiButton{
                        Button = {
                            FadeIn = 0.2f,
                            Command = $"quarrylevels.up {entityquarry.net.ID} {levelr} true",
                            Color = "0 0 0.9 0.2",
                            Material = "assets/content/ui/namefontmaterial.mat"
                        },
                        RectTransform = {
                            AnchorMin = info.onlyrp ? "0.08 0.06" : "0.51 0.06",
                            AnchorMax = "0.91 0.16"
                        },
                        Text = {
                            Text = Lan("AcceptRP", player.UserIDString, rp),
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        }
                    }, elemq1);
                }

                if (!info.onlyrp){
                    container.Add(new CuiButton{
                        Button = {
                            FadeIn = 0.2f,
                            Command = $"quarrylevels.up {entityquarry.net.ID} {levelr} false",
                            Color = "0 0.8 0 0.2",
                            Material = "assets/content/ui/namefontmaterial.mat"
                        },
                        RectTransform = {
                            AnchorMin = "0.08 0.06",
                            AnchorMax = info.onlyingredients ? "0.91 0.16" : "0.48 0.16"
                        },
                        Text = {
                            Text = Lan("Accept", player.UserIDString),
                            FontSize = 15,
                            Align = TextAnchor.MiddleCenter
                        }
                    }, elemq1);
                }

                textodes2 = Lan("Description", player.UserIDString, levelr, leveltotal, slots, speed, health);
                onlyrpbool = info.onlyrp;
            } else {
                textodes2 = Lan("Completed", player.UserIDString);
                container.Add(new CuiElement {
                    Parent = elemq1,
                    Components = {
                        new CuiImageComponent {
                            FadeIn = 0.2f,
                            ItemId = quarry.ShortPrefabName.Contains("pumpjack") ? -1130709577 : 1052926200, 
                            SkinId = 0
                        },
                        new CuiRectTransformComponent{
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-70 -95",
                            OffsetMax = "70 25"
                        }
                    }
                });
                onlyrpbool = false;
            }
            
            container.Add(new CuiElement {
                Name = elemq5,
                Parent = elemq1,
                Components = {
                    new CuiImageComponent {
                        FadeIn = 0.2f,
                        ItemId = GetItemIdFromShortname(quarry.ShortPrefabName.Contains("pumpjack") ? config.Oil.fuel : config.Mining.fuel), 
                        SkinId = 0
                    },
                    new CuiRectTransformComponent{
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-180 92",
                        OffsetMax = "-152 120"
                    }
                }
            });

            sizeX = 23;
            sizeY = 23;
            startX = 155;
            startY = 145;
            x = startX;
            y = startY;
            i = 0;

            foreach (ResourceDepositManager.ResourceDeposit.ResourceDepositEntry resourceDepositEntry in quarry._linkedDeposit._resources){
                i = i+1;
                if (i != 0 && i % 1 == 0){
                    y -= sizeY + 1;
                }
                container.Add(new CuiElement {
                    Name = elemq4,
                    Parent = elemq1,
                    Components = {
                        new CuiImageComponent {
                            FadeIn = 0.2f,
                            ItemId = resourceDepositEntry.type.itemid, 
                            SkinId = 0
                        },
                        new CuiRectTransformComponent{
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = $"{x} {y-sizeY}",
                            OffsetMax = $"{x + sizeX} {y}"
                        }
                    }
                });
            }

            container.Add(new CuiElement {
                Parent = elemq1,
                Components = {
                    new CuiTextComponent {
                        FadeIn = 0.2f,
                        Text = textodes2,
                        Color = "1 1 1 0.5",
                        FontSize = 13,
                        Font = "robotocondensed-regular.ttf",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = onlyrpbool ? "-200 -70" : "-200 0",
                        OffsetMax = onlyrpbool ? "200 80" : "200 100",
                    }
                }
            });
            return container;
        }

        ///Panel Error
        private CuiElementContainer GetPanel2(BasePlayer player, string textshow){
            var container = new CuiElementContainer();
            container.Add(new CuiElement {
                Name = elemq2,
                Parent = "Hud.Menu",
                Components = {
                    new CuiImageComponent {
                        Color = "1.0 0 0.0 0.4",
                        Material = "assets/content/ui/namefontmaterial.mat"
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-200 275",
                        OffsetMax = "180 305"
                    }
                }
            });

            container.Add(new CuiElement {
                Parent = elemq2,
                Components = {
                    new CuiTextComponent {
                        Text = textshow,
                        FontSize = 12,
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf"
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-200 -10",
                        OffsetMax = "200 10"
                    }
                }
            });

            container.Add(new CuiButton{
				Button =
				{
					Command = "quarrylevels.close2",
					Color = "0 0 0 0.2",
                    Material = "assets/content/ui/namefontmaterial.mat"
				},
				RectTransform =
				{
					AnchorMin = "0.88 0",
					AnchorMax = "1 1"
				},
				Text =
				{
					Text = "X",
					FontSize = 13,
					Align = TextAnchor.MiddleCenter
				}
			}, elemq2);

            return container;
        }
        #endregion
        
        #region Config
        private static ConfigData config;

        private class ConfigData{
            [JsonProperty(PropertyName = "Debug Console")]
            public bool Debug;

            [JsonProperty(PropertyName = "Chance of Oil Quarry")]
            public float oilChance;

            [JsonProperty(PropertyName = "Extract all resources")]
            public bool allResources;

            [JsonProperty(PropertyName = "Add Crude Oil as an additional resource in Mining Quarry")]
            public bool addCrude;

            [JsonProperty(PropertyName = "Maximum Quarrys per player (0 = Unlimited)")]
            public int maxQuarrys;

            [JsonProperty(PropertyName = "Maximum Oil Quarrys per player (0 = Unlimited)")]
            public int maxOilQuarrys;

            [JsonProperty(PropertyName = "Allow upgrading Quarries on the Map")]
            public bool upgradeMap;
            [JsonProperty(PropertyName = "Damage Quarrys")]
            public Damage Damage;
            [JsonProperty(PropertyName = "Sounds")]
            public bool Sounds;
            [JsonProperty(PropertyName = "Center the resource list")]
            public int position;
            [JsonProperty(PropertyName = "Notify")]
            public Notify Notify;

            [JsonProperty(PropertyName = "Mining Quarry Config")]
            public TypeQuarry Mining;

            [JsonProperty(PropertyName = "Pump Jack Config")]
            public TypeQuarry Oil;

            [JsonProperty(PropertyName = "Mineral Extraction Speed (Modify this only if you want to nerf some kind of ore)")]
            public Dictionary<string, float> Extract;

            public Oxide.Core.VersionNumber Version;
        }

        private class Damage{
            [JsonProperty(PropertyName = "Damage Block")]
            public bool block;
            [JsonProperty(PropertyName = "Melee Damage Only (Blocks damage from Fire, Explosives and Bullets)")]
            public bool damageOnly;
            [JsonProperty(PropertyName = "Damage Protection (Default 0.0 = 0% | 1.0 = 100%)")]
            public float damagePercentage;
        }

        private class Notify{
            [JsonProperty(PropertyName = "Notify Stop")]
            public bool notifystop;
            [JsonProperty(PropertyName = "Notify by Chat")]
            public bool notifychat;
            [JsonProperty(PropertyName = "Notify by GUI")]
            public bool notifygui;
            [JsonProperty(PropertyName = "Stop Checks Speed (Default 20.0)")]
            public float stoprefresh;
        }

        private class TypeQuarry{
            [JsonProperty(PropertyName = "Use ServerRewards? = true OR Use Economics? = False")]
            public bool money;
            [JsonProperty(PropertyName = "UPGRADE use only RP/Money (Default False)")]
            public bool onlyrp;
            [JsonProperty(PropertyName = "UPGRADE use only Ingredients (Default False)")]
            public bool onlyingredients;
            [JsonProperty(PropertyName = "Type of Fuel Required")]
            public string fuel;

            [JsonProperty(PropertyName = "Level 0 (Default)")]
            public LevelDefault Default;
            [JsonProperty(PropertyName = "Levels Upgrade")]
            public List<Level> Levels;
        }

        private class LevelDefault{
            [JsonProperty(PropertyName = "Slots")]
            public int slots;
            [JsonProperty(PropertyName = "Rate")]
            public float rate;
            [JsonProperty(PropertyName = "Fuel Consumption (Higher number = more duration per unit of fuel)")]
            public float workPerFuel;
            [JsonProperty(PropertyName = "Health")]
            public float health;
        }

        private class Level{
            [JsonProperty(PropertyName = "Info")]
            public LevelInfo Info;
            
            [JsonProperty(PropertyName = "Ingredients")]
            public Dictionary<string, int> List;
        }
        
        private class LevelInfo{
            [JsonProperty(PropertyName = "Slots")]
            public int slots;
            
            [JsonProperty(PropertyName = "Rate")]
            public float rate;

            [JsonProperty(PropertyName = "Fuel Consumption (Higher number = more duration per unit of fuel)")]
            public float workPerFuel;

            [JsonProperty(PropertyName = "RP")]
            public int rp;

            [JsonProperty(PropertyName = "Health")]
            public float health;
        }
        
        private class IngredientInfo{
            [JsonProperty(PropertyName = "Amount")]
            public int amount;
        }

        private ConfigData GetDefaultConfig() {
            return new ConfigData {
                Debug = true,
                oilChance = 50.0f,
                allResources = false,
                addCrude = false,
                maxQuarrys = 0,
                maxOilQuarrys = 0,
                upgradeMap = false,
                Damage = new Damage {
                    block = false,
                    damageOnly = false,
                    damagePercentage = 0.0f
                },
                Sounds = true,
                position = -125,
                Notify = new Notify {
                    notifystop = false,
                    notifychat = true,
                    notifygui = true,
                    stoprefresh = 20.0f
                },
                Mining = new TypeQuarry {
                    money = true,
                    onlyrp = false,
                    onlyingredients = false,
                    fuel = "lowgradefuel",
                    Default = new LevelDefault {
                        slots = 6,
                        rate = 1,
                        workPerFuel = 20.0f,
                        health = 1500
                    },
                    Levels = new List<Level> {
                        new Level {
                            Info = new LevelInfo {
                                slots = 12,
                                rate = 3.0f,
                                workPerFuel = 20.0f,
                                rp = 30,
                                health = 3000.0f
                            },
                            List = new Dictionary<string, int>(){
                                { "metal.refined", 50 },
                                { "metal.fragments", 5000 },
                                { "sheetmetal", 15 },
                                { "gears", 15 },
                                { "scrap", 75 }
                            }
                        },
                        new Level {
                            Info = new LevelInfo {
                                slots = 18,
                                rate = 4.0f,
                                workPerFuel = 20.0f,
                                rp = 50,
                                health = 3500.0f
                            },
                            List = new Dictionary<string, int>(){
                                { "metal.refined", 150 },
                                { "metal.fragments", 12000 },
                                { "sheetmetal", 25 },
                                { "gears", 25 },
                                { "scrap", 300 }
                            }
                        },
                        new Level {
                            Info = new LevelInfo {
                                slots = 24,
                                rate = 5.0f,
                                workPerFuel = 20.0f,
                                rp = 80,
                                health = 4000.0f
                            },
                            List = new Dictionary<string, int>(){
                                { "metal.refined", 300 },
                                { "metal.fragments", 25000 },
                                { "sheetmetal", 50 },
                                { "gears", 40 },
                                { "scrap", 750 }
                            }
                        },
                    },
                },
                Oil = new TypeQuarry {
                    money = true,
                    onlyrp = false,
                    onlyingredients = false,
                    fuel = "diesel_barrel",
                    Default = new LevelDefault {
                        slots = 6,
                        rate = 1,
                        workPerFuel = 1000.0f,
                        health = 1500
                    },
                    Levels = new List<Level> {
                        new Level {
                            Info = new LevelInfo {
                                slots = 12,
                                rate = 3.0f,
                                workPerFuel = 1000.0f,
                                rp = 30,
                                health = 3000.0f
                            },
                            List = new Dictionary<string, int>(){
                                { "metal.refined", 50 },
                                { "metal.fragments", 5000 },
                                { "sheetmetal", 15 },
                                { "gears", 15 },
                                { "scrap", 75 }
                            }
                        },
                        new Level {
                            Info = new LevelInfo {
                                slots = 18,
                                rate = 4.0f,
                                workPerFuel = 1000.0f,
                                rp = 50,
                                health = 3500.0f
                            },
                            List = new Dictionary<string, int>(){
                                { "metal.refined", 150 },
                                { "metal.fragments", 12000 },
                                { "sheetmetal", 25 },
                                { "gears", 25 },
                                { "scrap", 300 }
                            }
                        },
                        new Level {
                            Info = new LevelInfo {
                                slots = 24,
                                rate = 5.0f,
                                workPerFuel = 1000.0f,
                                rp = 80,
                                health = 4000.0f
                            },
                            List = new Dictionary<string, int>(){
                                { "metal.refined", 300 },
                                { "metal.fragments", 25000 },
                                { "sheetmetal", 50 },
                                { "gears", 40 },
                                { "scrap", 750 }
                            }
                        },
                    },
                },
                Extract = new Dictionary<string, float>(){
                    { "stones", 0.3f },
                    { "metal.ore", 5.0f },
                    { "sulfur.ore", 7.5f },
                    { "hq.metal.ore", 75.0f },
                    { "crude.oil", 15f }
                },
                Version = Version
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

            if (config.Version < Version)
                UpdateConfigValues();

            SaveConfig();
        }

        protected override void LoadDefaultConfig(){
            config = GetDefaultConfig();
        }

        private void UpdateConfigValues(){
            PrintWarning("Config update detected!");
            ConfigData baseConfig = GetDefaultConfig();

            if (config.Version < new VersionNumber(1, 4, 8)){
                config.maxQuarrys = 0;
                config.maxOilQuarrys = 0;
                config.addCrude = false;
            }

            config.Version = Version;
            PrintWarning("Config update completed!");
        }

        protected override void SaveConfig(){
            Config.WriteObject(config);
        }
        #endregion

        #region Lang
        protected override void LoadDefaultMessages() {
            lang.RegisterMessages(new Dictionary<string, string> {
                ["NoResources"] = "You do not have the necessary materials.",
                ["NoRP"] = "You don't have enough RP/Money.",
                ["Improve"] = "Minerals Quarry Upgrades",
                ["ImproveOil"] = "Oil Quarry Upgrades",
                ["Upgrade"] = "Upgrade",
                ["Accept"] = "Accept with Resources",
                ["AcceptRP"] = "Accept with <color=#FFA500>{0}</color> RP/Money",
                ["Completed"] = "<color=#FFA500>All improvements have been made.</color>",
                ["Description"] = "Current Level: <color=#FFA500>{0}</color>/<color=#FFA500>{1}</color>\n\n<color=#26ADE8>Next level improvements</color>\nDeposit Slots: <color=#FFA500>{2}</color>\nCollection Speed: <color=#FFA500>x{3}</color>\nHealth: <color=#FFA500>{4}</color>\n\nYou need the following resources to improve.",
                ["StopQuarry"] = "Your Quarry on <color=#FFA500>{0}</color> has been turned off.",
                ["NotAllowed"] = "You do not have permission to use this command.",
                ["LookQuarry"] = "You have to look at a quarry.",
                ["NoFuel"] = "No fuel to run. Required fuel: {0}",
                ["MaxLimit"] = "Maximum placed Quarrys reached.",
                ["MaxLimitOil"] = "Maximum placed Oil Quarrys reached."
            }, this);
        }

        private string Lan(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void PrintToChat(BasePlayer player, string message) => Player.Message(player, "<color=#f74d31>QuarryLevels:</color> " + message);
        #endregion
    }
}