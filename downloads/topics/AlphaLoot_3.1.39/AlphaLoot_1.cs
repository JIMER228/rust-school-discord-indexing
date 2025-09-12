using Facepunch;
using HarmonyLib;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Rust;
using Steamworks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Oxide.Plugins
{
    [Info("AlphaLoot", "k1lly0u", "3.1.41")]
    class AlphaLoot : RustPlugin
    {
        #region Fields
        [PluginReference] Plugin CustomLootSpawns, EventLoot, FancyDrop;

        private StoredData storedData;
        private StoredData bradleyData;
        private StoredData heliData;

        private DynamicConfigFile data, bradley, heli, skinData;

        private bool updateContainerCapacities = false;

        private static Hash<string, HashSet<SkinEntry>> weightedSkinIds;
        private static Hash<string, List<ulong>> importedSkinIds;
        private static Hash<string, int> defaultScrapAmounts;

        private readonly Hash<string, string> shortnameReplacements = new Hash<string, string>
        {
            ["chocholate"] = "chocolate"
        };

        private const string ADMIN_PERMISSION = "alphaloot.admin";

        private const string HELI_CRATE = "heli_crate";
        private const string BRADLEY_CRATE = "bradley_crate";
        private const string UNDERWATER_LABS = "underwater_labs/";
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            importedSkinIds = new Hash<string, List<ulong>>();
            defaultScrapAmounts = new Hash<string, int>();

            permission.RegisterPermission(ADMIN_PERMISSION, this);
            
            LoadData();

            if (updateContainerCapacities)
                SetCapacityLimits();
            
            Unsubscribe(nameof(OnLootSpawn));
        }

        private void OnServerInitialized()
        {
            PopulateContainerDefinitions(ref storedData, ref heliData, ref bradleyData);

            SaveData();

            Puts($"Loaded {storedData.loot_advanced.Count + storedData.loot_simple.Count} loot container definitions, {storedData.npcs_advanced.Count + storedData.npcs_simple.Count} npc loot definitions and {storedData.custom_advanced.Count + storedData.custom_simple.Count} custom loot definitions");
            Puts($"Loaded {heliData.loot_advanced.Count + heliData.loot_simple.Count} heli loot profiles");
            Puts($"Loaded {bradleyData.loot_advanced.Count + bradleyData.loot_simple.Count} bradley loot profiles");

            if (configData.AutoUpdate)
                AutoUpdateItemLists();

            FindAndRemoveInvalidItems();

            Subscribe(nameof(OnLootSpawn));
            
            if (configData.UseSkinboxSkins || configData.UseApprovedSkins)
            {
                if ((Steamworks.SteamInventory.Definitions?.Length ?? 0) == 0)
                {
                    PrintWarning("Waiting for Steamworks to initialize to load item skins");
                    Steamworks.SteamInventory.OnDefinitionsUpdated += LoadSkins;
                }
                else LoadSkins();
            } 
            else RefreshLootContents();
        }
               
        private void OnEntitySpawned(BradleyAPC bradleyApc)
        {
            if (bradleyApc && configData.BradleyCrates > 0)
                bradleyApc.maxCratesToSpawn = configData.BradleyCrates;
        }

        private void OnEntitySpawned(PatrolHelicopter baseHelicopter)
        {
            if (baseHelicopter && configData.HelicopterCrates > 0)
                baseHelicopter.maxCratesToSpawn = configData.HelicopterCrates;
        }

        private object OnCorpsePopulate(BaseEntity entity, LootableCorpse corpse)
        {
            if (!entity || !corpse)
                return null;

            object obj = Interface.CallHook("CanPopulateLoot", entity, corpse);
            if (obj != null)
                return null;

            return PopulateLoot(entity, corpse) ? corpse : null;
        }

        private object OnLootSpawn(LootContainer container)
        {
            if (!container)
                return null;

            if (CustomLootSpawns && (bool)CustomLootSpawns.Call("IsLootBox", container as BaseEntity))
                return null;

            if (EventLoot && (bool)EventLoot.Call("IsEventLootContainer", container as BaseEntity))
                return null;

            if (FancyDrop && container is SupplyDrop && !configData.OverrideFancyDrop)
                return null;
            
            object obj = Interface.CallHook("CanPopulateLoot", container);
            if (obj != null)
                return null;

            if (PopulateLoot(container))
                return true;

            return null;
        }

        private object OnItemUnwrap(Item item, BasePlayer player, ItemModUnwrap itemModUnwrap)
        {
            if (PopulateLoot(itemModUnwrap, item, player))
            {
                item.UseItem(1);

                if (itemModUnwrap.successEffect.isValid)                
                    Effect.server.Run(itemModUnwrap.successEffect.resourcePath, player.eyes.position, new Vector3(), null, false);
                
                return true;
            }

            return null;
        }

        private void OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (!player || info == null || !info.HitEntity)
                return;

            if (!permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
                return;

            LootContainer lootContainer = info.HitEntity.GetComponent<LootContainer>();
            if (!lootContainer)
                return;

            player.ChatMessage($"Viewing loot generation for: <color=#ffff00>{ToProfileName(lootContainer)}</color>");
            lootContainer.gameObject.AddComponent<LootCycler>();
            player.inventory.loot.StartLootingEntity(lootContainer, false);
            player.inventory.loot.AddContainer(lootContainer.inventory);
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", lootContainer.panelName);
        }

        private void OnLootEntityEnd(BasePlayer player, StorageContainer container)
        {
            if (!container)
                return;
            
            LootCycler lootCycler = container.GetComponent<LootCycler>();
            if (lootCycler)
                UnityEngine.Object.Destroy(lootCycler);
        }

        private void Unload()
        {
            RefreshLootContents(null, true);

            LootCycler[] lootCyclers = UnityEngine.Object.FindObjectsOfType<LootCycler>();
            for (int i = 0; i < lootCyclers?.Length; i++)            
                UnityEngine.Object.Destroy(lootCyclers[i]);
            
            configData = null;

            weightedSkinIds = null;
            importedSkinIds = null;
            defaultScrapAmounts = null;
        }
        #endregion

        #region Skins
        private void LoadSkins()
        {
            Steamworks.SteamInventory.OnDefinitionsUpdated -= LoadSkins;

            if (configData.UseApprovedSkins)
                PopulateSkinListFromApproved();

            RefreshLootContents();
        }

        private void OnSkinBoxSkinsLoaded(Hash<string, HashSet<ulong>> skinList)
        {
            if (!configData.UseSkinboxSkins)
                return;

            foreach (KeyValuePair<string, HashSet<ulong>> kvp in skinList)
            {
                if (!importedSkinIds.TryGetValue(kvp.Key, out List<ulong> list))
                    list = importedSkinIds[kvp.Key] = new List<ulong>();

                foreach(ulong skinId in kvp.Value)
                {
                    if (!list.Contains(skinId))
                        list.Add(skinId);
                }
            }
        }

        private void OnPlayerSkinsSkinsLoaded(Hash<string, HashSet<ulong>> skinList)
        {
            if (!configData.UsePlayerSkinsSkins)
                return;

            foreach (KeyValuePair<string, HashSet<ulong>> kvp in skinList)
            {
                if (!importedSkinIds.TryGetValue(kvp.Key, out List<ulong> list))
                    list = importedSkinIds[kvp.Key] = new List<ulong>();

                foreach (ulong skinId in kvp.Value)
                {
                    if (!list.Contains(skinId))
                        list.Add(skinId);
                }
            }
        }

        private void PopulateSkinListFromApproved()
        {
            if (importedSkinIds == null)
                importedSkinIds = new Hash<string, List<ulong>>();

            List<int> itemSkinDirectory = Pool.Get<List<int>>();
            itemSkinDirectory.AddRange(ItemSkinDirectory.Instance.skins.Select(x => x.id));

            int count = 0;

            foreach (InventoryDef item in Steamworks.SteamInventory.Definitions)
            {
                string shortname = item.GetProperty("itemshortname");
                if (string.IsNullOrEmpty(shortname) || item.Id < 100)
                    continue;

                ulong wsid;
                if (itemSkinDirectory.Contains(item.Id))
                    wsid = (ulong)item.Id;
                else
                {
                    if (!ulong.TryParse(item.GetProperty("workshopid"), out wsid))
                        continue;
                }

                if (!importedSkinIds.ContainsKey(shortname))
                    importedSkinIds[shortname] = new List<ulong>();

                if (!importedSkinIds[shortname].Contains(wsid))
                {
                    importedSkinIds[shortname].Add(wsid);
                    count++;
                }
            }

            Puts($"Imported {count} approved skins that weren't already in the list");
            Pool.FreeUnmanaged(ref itemSkinDirectory);
        }
        #endregion

        #region Container Population
        private void RefreshLootContents(ConsoleSystem.Arg arg = null, bool setDefaultScrap = false)
        {
            LootContainer[] lootContainers = UnityEngine.Object.FindObjectsOfType<LootContainer>();
            
            if (arg != null)
                SendReply(arg, $"Repopulating loot for {lootContainers?.Length} containers");
            else Puts($"Repopulating loot for {lootContainers?.Length} containers");

            for (int i = 0; i < lootContainers?.Length; i++)
            {
                LootContainer lootContainer = lootContainers[i];
                if (lootContainer && !lootContainer.IsDestroyed)
                {
                    if (lootContainer.inventory == null)
                    {
                        lootContainer.CreateInventory(true);
                        lootContainer.OnInventoryFirstCreated(lootContainer.inventory);
                    }

                    if (setDefaultScrap)
                    {
                        if (defaultScrapAmounts.TryGetValue(ToProfileName(lootContainer), out int scrapAmount))
                            lootContainer.scrapAmount = scrapAmount;
                    }

                    lootContainer.inventory.capacity = lootContainer.inventorySlots;
                    lootContainer.CancelInvoke(lootContainer.SpawnLoot);
                    lootContainer.Invoke(lootContainer.SpawnLoot, UnityEngine.Random.Range(1f, 20f));
                }
            }
        }

        private void CreateLootDefinitionFor(LootContainer lootContainer, ref StoredData storedData, ref StoredData heliData, ref StoredData bradleyData) 
        {
            string profileName = ToProfileName(lootContainer);
            
            if (!defaultScrapAmounts.ContainsKey(profileName))
                defaultScrapAmounts[profileName] = lootContainer.scrapAmount;

            if (profileName.Equals(HELI_CRATE))
            {
                if (storedData.TryGetLootProfile(HELI_CRATE, out BaseLootContainerProfile lootContainerProfile))
                {
                    heliData.CloneLootProfile(HELI_CRATE, lootContainerProfile);
                    storedData.RemoveProfile(HELI_CRATE);
                    Debug.LogWarning($"Helicopter loot profiles have been removed from your loot table and placed in its own data file. (/data/AlphaLoot/LootProfiles/{configData.HeliProfileName}.json)");
                }
                else
                {
                    if (!heliData.HasAnyProfiles)                     
                        heliData.CreateDefaultLootProfile(lootContainer);
                }
            }
            else if (profileName.Equals(BRADLEY_CRATE))
            {
                if (storedData.TryGetLootProfile(BRADLEY_CRATE, out BaseLootContainerProfile lootContainerProfile))
                {
                    bradleyData.CloneLootProfile(BRADLEY_CRATE, lootContainerProfile);
                    storedData.RemoveProfile(BRADLEY_CRATE);
                    Debug.LogWarning($"Bradley loot profiles have been removed from your loot table and placed in its own data file. (/data/AlphaLoot/LootProfiles/{configData.BradleyProfileName}.json)");
                }
                else
                {
                    if (!bradleyData.HasAnyProfiles)                     
                        bradleyData.CreateDefaultLootProfile(lootContainer);
                }
            }
            else
            {
                storedData.CreateDefaultLootProfile(lootContainer);
            }
        }

        private void CreateLootDefinitionFor(NPCPlayer npcPlayer, ref StoredData storedData)
        {
            global::HumanNPC humanNPC = npcPlayer as global::HumanNPC;
            if (humanNPC)
            {
                storedData.CreateDefaultLootProfile(npcPlayer.ShortPrefabName, humanNPC.LootSpawnSlots, npcPlayer.loadouts);
                return;
            }

            ScarecrowNPC scarecrowNPC = npcPlayer as ScarecrowNPC;
            if (scarecrowNPC)
            {
                storedData.CreateDefaultLootProfile(npcPlayer.ShortPrefabName, scarecrowNPC.LootSpawnSlots, npcPlayer.loadouts);
                return;
            }
        }
  
        private void PopulateContainerDefinitions(ref StoredData storedData, ref StoredData heliData, ref StoredData bradleyData)
        {
            storedData.IsBaseLootTable = true;

            int loot = 0;
            int npc = 0;
            int item = 0;

            foreach (KeyValuePair<string, Object> kvp in FileSystem.Backend.cache)
            {
                if (kvp.Value is GameObject gameObject)
                {
                    LootContainer lootContainer = gameObject.GetComponent<LootContainer>();
                    if (lootContainer)
                    {
                        loot++;
                        CreateLootDefinitionFor(lootContainer, ref storedData, ref heliData, ref bradleyData);
                        continue;
                    }

                    NPCPlayer npcPlayer = gameObject.GetComponent<NPCPlayer>();
                    if (npcPlayer)
                    {
                        npc++;
                        CreateLootDefinitionFor(npcPlayer, ref storedData);
                        continue;
                    }
                }
            }

            foreach(ItemDefinition itemDefinition in ItemManager.itemList)
            {
                ItemModUnwrap itemModUnwrap = itemDefinition.GetComponentInChildren<ItemModUnwrap>();
                if (itemModUnwrap)
                {
                    storedData.CreateDefaultLootProfile(itemDefinition, itemModUnwrap);
                    item++;
                }                
            }

            Debug.Log($"Found {loot} loot containers, {npc} NPC prefabs and {item} unwrapable items in bundles");
        }

        private bool PopulateLoot(LootContainer container)
        {
            string profileName = ToProfileName(container);
            
            if (configData.TryGetContainerOverride(profileName, out string overrideProfile))
                profileName = overrideProfile;

            return PopulateLoot(container, profileName);
        }
        
        private bool PopulateLoot(LootContainer container, string profileName)
        {            
            BaseLootContainerProfile lootProfile;

            if (profileName.Equals(HELI_CRATE))
            {
                if (heliData.GetRandomLootProfile(out lootProfile))
                {
                    PopulateLootContainer(container, lootProfile);
                    return true;
                }
            }
            else if (profileName.Equals(BRADLEY_CRATE))
            {
                if (bradleyData.GetRandomLootProfile(out lootProfile))
                {
                    PopulateLootContainer(container, lootProfile);
                    return true;
                }
            }
            else
            {
                if (storedData.TryGetLootProfile(profileName, out lootProfile) && lootProfile.Enabled)
                {                    
                    PopulateLootContainer(container, lootProfile);
                    return true;
                }
            }

            return false;
        }

        private bool PopulateLoot(ItemModUnwrap itemModUnwrap, Item item, BasePlayer player)
        {
            if (storedData.TryGetLootProfile(item.info.shortname, out BaseLootContainerProfile lootProfile) && lootProfile.Enabled)
            {
                int attempts = UnityEngine.Random.Range(itemModUnwrap.minTries, itemModUnwrap.maxTries + 1);
                for (int i = 0; i < attempts; i++)                
                    lootProfile.PopulateLoot(player.inventory.containerMain);                    
                                
                return true;
            }

            return false;
        }

        private bool PopulateLoot(BaseEntity entity, LootableCorpse corpse)
        {
            string profileName = entity.ShortPrefabName;
            
            if (configData.TryGetContainerOverride(profileName, out string overrideProfile))
                profileName = overrideProfile;
            
            if (storedData.TryGetNPCProfile(profileName, out BaseLootProfile lootProfile))
            {
                if (!lootProfile.Enabled)
                    return false;

                string loadoutName = entity is global::HumanNPC humanNpc ? humanNpc.GetLoadoutName() : string.Empty;
                
                lootProfile.PopulateLoot(corpse.containers[0], loadoutName);
                
                return true;
            }
            return false;
        }
        
        private bool PopulateLoot(ItemContainer container, string profileName)
        {
            if (storedData.TryGetLootProfile(profileName, out BaseLootContainerProfile lootContainerProfile))
            {
                if (!lootContainerProfile.Enabled)
                    return false;

                lootContainerProfile.PopulateLoot(container);
                return true;
            }

            if (storedData.TryGetNPCProfile(profileName, out BaseLootProfile lootProfile))
            {
                if (!lootProfile.Enabled)
                    return false;

                lootProfile.PopulateLoot(container, profileName);
                return true;
            }
            
            if (storedData.TryGetCustomProfile(profileName, out lootProfile))
            {
                if (!lootProfile.Enabled)
                    return false;

                lootProfile.PopulateLoot(container);
                return true;
            }
            return false;
        }

        private bool ProfileExists(string name) => storedData.Exists(name);
        
        private void PopulateLootContainer(LootContainer container, BaseLootContainerProfile lootProfile)
        {
            container.destroyOnEmpty = lootProfile.DestroyOnEmpty;

            lootProfile.PopulateLoot(container.inventory);

            container.CancelInvoke(container.SpawnLoot);

            if (lootProfile.ShouldRefreshContents)
                container.Invoke(container.SpawnLoot, Mathf.Max(60, UnityEngine.Random.Range(lootProfile.MinSecondsBetweenRefresh, lootProfile.MaxSecondsBetweenRefresh)));
        }

        #endregion

        #region Functions
        private string ToShortName(string name)
        {
            return name.Split('/').Last().Replace(".prefab", "");
        }

        private static string ToProfileName(LootContainer container)
        {
            if (container.PrefabName.Contains(UNDERWATER_LABS))
                return UNDERWATER_LABS + container.ShortPrefabName;
            
            return container.ShortPrefabName;
        }

        private LootContainer FindContainer(BasePlayer player)
        {
            if (Physics.Raycast(player.eyes.HeadRay(), out RaycastHit raycastHit, 20f))
            {
                LootContainer lootContainer = raycastHit.GetEntity() as LootContainer;
                return lootContainer;
            }
            return null;
        }

        private object WantsToHandleFancyDropLoot() => configData.OverrideFancyDrop ? (object)true : null;
        #endregion

        #region Auto-Updater
        private void AutoUpdateItemLists()
        {
            const string lastDefaultTable = "AlphaLoot/AutoUpdater/do_not_edit_this_file";
            
            ItemList lastItemList;

            if (Interface.Oxide.DataFileSystem.ExistsDatafile(lastDefaultTable))
            {
                lastItemList = Interface.Oxide.DataFileSystem.GetFile(lastDefaultTable).ReadObject<ItemList>();

                if (lastItemList != null)
                {
                    if (lastItemList.protocol == Rust.Protocol.printable)
                    {
                        Debug.Log("[AlphaLoot Auto Updater] - Last item list protocol matches current protocol. No new items added");
                        return;
                    }

                    List<int> newItems = Pool.Get<List<int>>();

                    ItemManager.itemList.ForEach(x =>
                    {
                        if (!lastItemList.itemIds.Contains(x.itemid))
                            newItems.Add(x.itemid);
                    });

                    if (newItems.Count > 0)
                    {
                        Debug.Log($"[AlphaLoot Auto Updater] - Found {newItems.Count} new game items. Adding them to your loot table");

                        AddItemsToLootTable(newItems, out int additions);

                        if (additions > 0)
                        {
                            Debug.Log($"[AlphaLoot Auto Updater] - Added {additions} new loot definitions to the loot table");
                            SaveData();
                            Interface.Oxide.DataFileSystem.WriteObject<ItemList>(lastDefaultTable, new ItemList() { itemIds = ItemManager.itemDictionary.Keys.ToList(), protocol = Rust.Protocol.printable });
                        }
                    }
                    else Debug.Log("[AlphaLoot Auto Updater] - No new items in game");

                    Pool.FreeUnmanaged(ref newItems);
                }
            }
            else
            {
                Debug.Log("[AlphaLoot Auto Updater] - Generating item list for auto-updater. Future game updates will automatically add new items to your loot table. You can disable this feature in the config");
                Interface.Oxide.DataFileSystem.WriteObject<ItemList>(lastDefaultTable, new ItemList() { itemIds = ItemManager.itemDictionary.Keys.ToList(), protocol = Rust.Protocol.printable });
            }
        }

        private void AddSpecifiedItemsToLootTable(params string[] args)
        {
            List<int> items = Pool.Get<List<int>>();

            foreach(string str in args)
            {
                ItemDefinition itemDefinition = ItemManager.FindItemDefinition(str);
                if (itemDefinition)
                    items.Add(itemDefinition.itemid);
            }

            if (items.Count > 0)
            {
                Debug.Log($"[AlphaLoot] - Adding {items.Count} specified items to your loot table");

                AddItemsToLootTable(items, out int additions);

                if (additions > 0)
                {
                    Debug.Log($"[AlphaLoot] - Successfully added {additions} new loot definitions to the loot table");
                    SaveData();
                }
                else Debug.Log($"[AlphaLoot] - Failed to find any loot tables with the specified items");
            }
            else Debug.Log("[AlphaLoot] - Failed to find item definitions for the shortname's supplied");

            Pool.FreeUnmanaged(ref items);
        }
                
        private void AddItemsToLootTable(List<int> items, out int additions)
        {
            additions = 0;

            StoredData defaultLootTable = new StoredData();
            StoredData defaultHeliLootTable = new StoredData();
            StoredData defaultBradleyLootTable = new StoredData();

            PopulateContainerDefinitions(ref defaultLootTable, ref defaultHeliLootTable, ref defaultBradleyLootTable);
            
            AddItemsToLootTable(defaultLootTable, storedData, items, ref additions);
            AddItemsToLootTable(defaultHeliLootTable, heliData, items, ref additions);
            AddItemsToLootTable(defaultBradleyLootTable, bradleyData, items, ref additions);
        }

        private void AddItemsToLootTable(StoredData source, StoredData dest, List<int> items, ref int additions)
        {
            foreach (int itemId in items)
            {
                string shortname = ItemManager.itemDictionary[itemId].shortname;
                
                foreach (KeyValuePair<string, AdvancedLootContainerProfile> kvp in source.loot_advanced)
                {
                    NewLootItem newLootItem = new NewLootItem();

                    CalculateItemScore(shortname, kvp.Value.LootSpawnSlots, ref newLootItem);

                    if (newLootItem.HasItems)
                    {
                        if (dest.loot_advanced.TryGetValue(kvp.Key, out AdvancedLootContainerProfile lootProfile))
                        {
                            InsertLootSpawnSlot(kvp.Key, shortname, newLootItem, ref lootProfile.LootSpawnSlots, ref additions);
                        }
                        else if (dest.loot_simple.TryGetValue(kvp.Key, out SimpleLootContainerProfile simpleLootProfile))
                        {
                            InsertItemAmountSpawnsWith(kvp.Key, shortname, newLootItem, ref simpleLootProfile.Items, ref additions);
                        }
                    }
                }

                foreach (KeyValuePair<string, AdvancedNPCLootProfile> kvp in source.npcs_advanced)
                {
                    NewLootItem newLootItem = new NewLootItem();

                    CalculateItemScore(shortname, kvp.Value.LootSpawnSlots, ref newLootItem);

                    if (newLootItem.HasItems)
                    {
                        if (dest.npcs_advanced.TryGetValue(kvp.Key, out AdvancedNPCLootProfile lootProfile))
                        {
                            InsertLootSpawnSlot(kvp.Key, shortname, newLootItem, ref lootProfile.LootSpawnSlots, ref additions);
                        }
                        else if (dest.npcs_simple.TryGetValue(kvp.Key, out SimpleNPCLootProfile simpleLootProfile))
                        {
                            InsertItemAmountSpawnsWith(kvp.Key, shortname, newLootItem, ref simpleLootProfile.Items, ref additions);
                        }
                    }
                }
            }
        }
        
        private void CalculateItemScore(string shortname, LootSpawnSlot[] source, ref NewLootItem newLootItem)
        {
            foreach (LootSpawnSlot lootSpawnSlot in source)
            {
                if (lootSpawnSlot?.LootDefinition == null)
                    continue;
                
                // See if the item is in its own loot spawn slot
                if (!ShouldDuplicateLootSpawnSlot(shortname, lootSpawnSlot, ref newLootItem))
                {
                    if (lootSpawnSlot.Eras?.Length > 0)
                        newLootItem.AddEras(lootSpawnSlot.Eras);
                    
                    // Search recursively through the loot spawn slot and calculate a probability
                    if (!string.IsNullOrEmpty(lootSpawnSlot.OnlyWithLoadoutNamed))
                        newLootItem.RequiredLoadout = lootSpawnSlot.OnlyWithLoadoutNamed;

                    FindItemAndCalculateProbabilityRecursive(lootSpawnSlot.LootDefinition, shortname, newLootItem, lootSpawnSlot.Probability);
                }
            }
        }

        private bool ShouldDuplicateLootSpawnSlot(string shortname, LootSpawnSlot source, ref NewLootItem newLootItem)
        {
            if (source.LootDefinition.SubSpawn.Length == 0 && source.LootDefinition.Items.Length > 0)
            {
                if (source.LootDefinition.Items.Any(x => x.Shortname == shortname))
                {
                    newLootItem.AddItems(source.LootDefinition.Items);
                    newLootItem.Probability = source.Probability;
                    newLootItem.RequiredLoadout = source.OnlyWithLoadoutNamed;
                    return true;
                }
            }

            return false;
        }
        
        private void FindItemAndCalculateProbabilityRecursive(LootSpawn lootSpawn, string shortname, NewLootItem newLootItem, float probability)
        {
            if (lootSpawn.SubSpawn.Length > 0)
            {
                float subspawnWeight = lootSpawn.SubSpawn.Sum(x => x.Weight);
                
                foreach(LootSpawn.Entry lootSpawnEntry in lootSpawn.SubSpawn)
                {
                    if (lootSpawnEntry.RestrictedEras?.Length > 0)
                        newLootItem.AddEras(lootSpawnEntry.RestrictedEras);
                    
                    FindItemAndCalculateProbabilityRecursive(lootSpawnEntry.Category, shortname, newLootItem, probability * ((float)lootSpawnEntry.Weight / (float)subspawnWeight));
                }
            }
            else if (lootSpawn.Items.Length > 0)
            {
                int occurances = 0;
                float minAmount = 0;
                float maxAmount = 0;
                
                foreach(ItemAmountRanged itemAmountRanged in lootSpawn.Items)
                {
                    if (itemAmountRanged.Shortname == shortname)
                    {
                        minAmount += itemAmountRanged.MinAmount;
                        maxAmount += itemAmountRanged.MaxAmount;
                        occurances++;
                    }
                }
                
                if (occurances > 0)
                {
                    ItemAmountRanged item = new ItemAmountRanged
                    {
                        Shortname = shortname,
                        MinAmount = minAmount / (float)occurances,
                        MaxAmount = maxAmount / (float)occurances
                    };

                    newLootItem.AddItem(item);
                    newLootItem.Probability = probability;
                }
            }
        }
        
        private void InsertLootSpawnSlot(string container, string shortname, NewLootItem newLootItem, ref LootSpawnSlot[] dest, ref int additions)
        {
            LootSpawnSlot newLootSpawnSlot = new LootSpawnSlot
            {
                LootDefinition = new LootSpawn()
                {
                    Items = newLootItem.Items.ToArray(),
                    SubSpawn = new LootSpawn.Entry[0]
                },
                NumberToSpawn = 1,
                Probability = newLootItem.Probability,
                OnlyWithLoadoutNamed = newLootItem.RequiredLoadout,
                Eras = newLootItem.RequiredEras?.ToArray() ?? new Era[0]
            };
            
            int index = dest.Length;
            Array.Resize(ref dest, index + 1);
            dest[index] = newLootSpawnSlot;

            additions++;
            Debug.Log($"[AlphaLoot] - Added {shortname} to advanced loot profile ({container}) with a calculated probability of {newLootItem.Probability}");
        }

        private void InsertItemAmountSpawnsWith(string container, string shortname, NewLootItem newLootItem, ref ItemAmountSpawnsWith[] dest, ref int additions)
        {
            float weight = dest.Sum(x => x.Weight);
            int index = dest.Length;
            
            Array.Resize(ref dest, index + newLootItem.Items.Count);

            for (int i = 0; i < newLootItem.Items.Count; i++)
            {
                ItemAmountRanged itemAmountRanged = newLootItem.Items[i];
                ItemAmountSpawnsWith itemAmountWeighted = new ItemAmountSpawnsWith
                {
                    Condition = new ItemAmount.ConditionItem(),
                    MaxAmount = itemAmountRanged.MaxAmount,
                    MinAmount = itemAmountRanged.MinAmount,
                    Shortname = itemAmountRanged.Shortname,
                    Weight = Mathf.Max(Mathf.RoundToInt(weight * newLootItem.Probability), 1),
                    RestrictedEras = newLootItem.RequiredEras?.ToArray() ?? new Era[0],
                    SpawnsWith = new ItemAmountWeighted[0]
                };

                dest[index+i] = itemAmountWeighted;
            }

            additions++;
            Debug.Log($"[AlphaLoot] - Added {shortname} to simple loot profile ({container}) with a calculated probability of {newLootItem.Probability}");
        }


        private class NewLootItem
        {
            public List<ItemAmountRanged> Items;
            public float Probability;
            public string RequiredLoadout;
            public List<Era> RequiredEras;

            public bool HasItems => Items != null;

            public void AddItem(ItemAmountRanged itemAmountRanged)
            {
                Items ??= new List<ItemAmountRanged>();
                Items.Add(itemAmountRanged);
            }
            
            public void AddItems(ItemAmountRanged[] items)
            {
                Items ??= new List<ItemAmountRanged>();
                Items.AddRange(items);
            }

            public void AddEras(IEnumerable<Era> enumerable)
            {
                RequiredEras ??= new List<Era>();
                
                foreach (Era era in enumerable)
                {
                    if (!RequiredEras.Contains(era))
                        RequiredEras.Add(era);
                }
            }
        }

        private class ItemList
        {
            public List<int> itemIds = new List<int>();
            public string protocol;
        }
        #endregion

        #region Removed Item Scan
        private List<ItemAmountRanged> keepItemsRanged = new List<ItemAmountRanged>();

        private List<ItemAmountSpawnsWith> keepItemsWeighted = new List<ItemAmountSpawnsWith>();

        private void FindAndRemoveInvalidItems()
        {
            keepItemsRanged.Clear();
            keepItemsWeighted.Clear();
            
            Puts("Scanning loot tables for removed items...");

            FindAndRemoveInvalidItems(data, storedData, "Loot Table");
            FindAndRemoveInvalidItems(heli, heliData, "Heli Loot Table");
            FindAndRemoveInvalidItems(bradley, bradleyData, "Bradley Loot Table");

            Puts("Removed item scan completed!");
        }

        private void FindAndRemoveInvalidItems(DynamicConfigFile dynamicConfigFile, StoredData data, string table)
        {
            Puts($"Scanning {table}...");

            bool shouldSave = false;

            foreach (KeyValuePair<string, AdvancedLootContainerProfile> kvp in data.loot_advanced)
            {
                foreach (LootSpawnSlot lootSpawnSlot in kvp.Value.LootSpawnSlots)
                {
                    if (lootSpawnSlot?.LootDefinition == null)
                        continue;
                    
                    FindItemRemoveInvalidItemRecursive(kvp.Key, ref lootSpawnSlot.LootDefinition, ref shouldSave);
                }            
            }

            foreach (KeyValuePair<string, SimpleLootContainerProfile> kvp in storedData.loot_simple)
                FindItemRemoveInvalidItemRecursive(kvp.Key, ref kvp.Value.Items, ref shouldSave);

            foreach (KeyValuePair<string, AdvancedNPCLootProfile> kvp in data.npcs_advanced)
            {
                foreach (LootSpawnSlot lootSpawnSlot in kvp.Value.LootSpawnSlots)
                {
                    if (lootSpawnSlot?.LootDefinition == null)
                        continue;
                    
                    FindItemRemoveInvalidItemRecursive(kvp.Key, ref lootSpawnSlot.LootDefinition, ref shouldSave);
                }              
            }

            foreach (KeyValuePair<string, SimpleNPCLootProfile> kvp in storedData.npcs_simple)            
                FindItemRemoveInvalidItemRecursive(kvp.Key, ref kvp.Value.Items, ref shouldSave);            

            foreach (KeyValuePair<string, AdvancedCustomLootProfile> kvp in data.custom_advanced)
            {
                foreach (LootSpawnSlot lootSpawnSlot in kvp.Value.LootSpawnSlots)
                {
                    if (lootSpawnSlot?.LootDefinition == null)
                        continue;
                    
                    FindItemRemoveInvalidItemRecursive(kvp.Key, ref lootSpawnSlot.LootDefinition, ref shouldSave);
                }                
            }

            foreach (KeyValuePair<string, SimpleCustomLootProfile> kvp in storedData.custom_simple)            
                FindItemRemoveInvalidItemRecursive(kvp.Key, ref kvp.Value.Items, ref shouldSave);        
            
            if (shouldSave)
                dynamicConfigFile.WriteObject(data);
        }

        private void FindItemRemoveInvalidItemRecursive(string container, ref LootSpawn lootSpawn, ref bool shouldSave)
        {
            if (lootSpawn.SubSpawn.Length > 0)
            {
                foreach (LootSpawn.Entry lootSpawnEntry in lootSpawn.SubSpawn)
                {
                    FindItemRemoveInvalidItemRecursive(container, ref lootSpawnEntry.Category, ref shouldSave);
                }
            }
            else if (lootSpawn.Items.Length > 0)
            {
                bool shouldUpdate = false;
                for (int i = 0; i < lootSpawn.Items.Length; i++)
                {
                    ItemAmountRanged itemAmountRanged = lootSpawn.Items[i];
                    if (ItemManager.itemDictionaryByName.ContainsKey(itemAmountRanged.Shortname))
                        keepItemsRanged.Add(itemAmountRanged);
                    else
                    {
                        if (shortnameReplacements.TryGetValue(itemAmountRanged.Shortname, out string shortname) && ItemManager.itemDictionaryByName.ContainsKey(shortname))
                        {
                            Puts($"Replacing invalid shortname {itemAmountRanged.Shortname} with {shortname} in {container}");
                            
                            itemAmountRanged.Shortname = shortname;
                            keepItemsRanged.Add(itemAmountRanged);
                            
                            shouldSave = true;
                        }
                        else
                        {
                            shouldSave = true;
                            shouldUpdate = true;
                            Puts($"Removing {itemAmountRanged.Shortname} from {container}");
                        }
                    }
                }

                if (shouldUpdate)
                    lootSpawn.Items = keepItemsRanged.ToArray();

                keepItemsRanged.Clear();
            }
        }

        private void FindItemRemoveInvalidItemRecursive(string container, ref ItemAmountSpawnsWith[] items, ref bool shouldSave)
        {
            bool shouldUpdate = false;
            for (int i = 0; i < items.Length; i++)
            {
                ItemAmountSpawnsWith itemAmountSpawnsWith = items[i];
                if (ItemManager.itemDictionaryByName.ContainsKey(itemAmountSpawnsWith.Shortname))
                {
                    if (itemAmountSpawnsWith.SpawnsWith?.Length > 0)
                    {
                        for (int i1 = itemAmountSpawnsWith.SpawnsWith.Length - 1; i1 >= 0; i1--)
                        {
                            ItemAmountWeighted spawnsWith = itemAmountSpawnsWith.SpawnsWith[i1];
                            if (!ItemManager.itemDictionaryByName.ContainsKey(spawnsWith.Shortname))
                            {
                                RemoveFromArray(ref itemAmountSpawnsWith.SpawnsWith, spawnsWith);
                                shouldSave = true;
                            }
                        }
                    }
                    keepItemsWeighted.Add(itemAmountSpawnsWith);
                }
                else
                {
                    shouldSave = true;
                    shouldUpdate = true;
                    Puts($"Removing '{itemAmountSpawnsWith.Shortname}' from container '{container}'");
                }
            }

            if (shouldUpdate)
                items = keepItemsWeighted.ToArray();

            keepItemsWeighted.Clear();
        }
        #endregion
        
        #region Remove Items By Shortname
        private void FindAndRemoveItems(string[] shortnames)
        {
            keepItemsRanged.Clear();
            keepItemsWeighted.Clear();
            
            Puts($"Scanning loot tables for {shortnames.ToSentence()}...");

            int count = 0;
            
            FindAndRemoveItems(data, storedData, "Loot Table", shortnames, ref count);
            FindAndRemoveItems(heli, heliData, "Heli Loot Table", shortnames, ref count);
            FindAndRemoveItems(bradley, bradleyData, "Bradley Loot Table", shortnames, ref count);

            Puts($"Removed {count} of {shortnames.ToSentence()}!");
        }

        private void FindAndRemoveItems(DynamicConfigFile dynamicConfigFile, StoredData data, string table, string[] shortnames, ref int count)
        {
            Puts($"Scanning {table}...");

            bool shouldSave = false;

            foreach (KeyValuePair<string, AdvancedLootContainerProfile> kvp in data.loot_advanced)
            {
                foreach (LootSpawnSlot lootSpawnSlot in kvp.Value.LootSpawnSlots)
                {
                    if (lootSpawnSlot?.LootDefinition == null)
                        continue;
                    
                    FindItemRemoveItemByNameRecursive(kvp.Key, shortnames, ref lootSpawnSlot.LootDefinition, ref shouldSave, ref count);
                }              
            }

            foreach (KeyValuePair<string, SimpleLootContainerProfile> kvp in storedData.loot_simple)
                FindItemRemoveItemByNameRecursive(kvp.Key, shortnames, ref kvp.Value.Items, ref shouldSave, ref count);

            foreach (KeyValuePair<string, AdvancedNPCLootProfile> kvp in data.npcs_advanced)
            {
                foreach (LootSpawnSlot lootSpawnSlot in kvp.Value.LootSpawnSlots)
                {
                    if (lootSpawnSlot?.LootDefinition == null)
                        continue;
                    
                    FindItemRemoveItemByNameRecursive(kvp.Key, shortnames, ref lootSpawnSlot.LootDefinition, ref shouldSave, ref count);
                }            
            }

            foreach (KeyValuePair<string, SimpleNPCLootProfile> kvp in storedData.npcs_simple)            
                FindItemRemoveItemByNameRecursive(kvp.Key, shortnames, ref kvp.Value.Items, ref shouldSave, ref count);            

            foreach (KeyValuePair<string, AdvancedCustomLootProfile> kvp in data.custom_advanced)
            {
                foreach (LootSpawnSlot lootSpawnSlot in kvp.Value.LootSpawnSlots)
                {
                    if (lootSpawnSlot?.LootDefinition == null)
                        continue;
                    
                    FindItemRemoveItemByNameRecursive(kvp.Key, shortnames, ref lootSpawnSlot.LootDefinition, ref shouldSave, ref count);
                }           
            }

            foreach (KeyValuePair<string, SimpleCustomLootProfile> kvp in storedData.custom_simple)            
                FindItemRemoveItemByNameRecursive(kvp.Key, shortnames, ref kvp.Value.Items, ref shouldSave, ref count);        
            
            if (shouldSave)
                dynamicConfigFile.WriteObject(data);
        }

        private void FindItemRemoveItemByNameRecursive(string container, string[] shortnames, ref LootSpawn lootSpawn, ref bool shouldSave, ref int count)
        {
            if (lootSpawn.SubSpawn.Length > 0)
            {
                foreach (LootSpawn.Entry lootSpawnEntry in lootSpawn.SubSpawn)
                {
                    FindItemRemoveItemByNameRecursive(container, shortnames, ref lootSpawnEntry.Category, ref shouldSave, ref count);
                }
            }
            else if (lootSpawn.Items.Length > 0)
            {
                bool shouldUpdate = false;
                for (int i = 0; i < lootSpawn.Items.Length; i++)
                {
                    ItemAmountRanged itemAmountRanged = lootSpawn.Items[i];
                    if (!shortnames.Contains(itemAmountRanged.Shortname))
                        keepItemsRanged.Add(itemAmountRanged);
                    else
                    {
                        count++;
                        shouldSave = true;
                        shouldUpdate = true;
                        Puts($"Removing {itemAmountRanged.Shortname} from {container}");
                    }
                }

                if (shouldUpdate)
                    lootSpawn.Items = keepItemsRanged.ToArray();

                keepItemsRanged.Clear();
            }
        }

        private void FindItemRemoveItemByNameRecursive(string container, string[] shortnames, ref ItemAmountSpawnsWith[] items, ref bool shouldSave, ref int count)
        {
            bool shouldUpdate = false;
            for (int i = 0; i < items.Length; i++)
            {
                ItemAmountSpawnsWith itemAmountSpawnsWith = items[i];
                if (!shortnames.Contains(itemAmountSpawnsWith.Shortname))
                {
                    if (itemAmountSpawnsWith.SpawnsWith?.Length > 0)
                    {
                        for (int i1 = itemAmountSpawnsWith.SpawnsWith.Length - 1; i1 >= 0; i1--)
                        {
                            ItemAmountWeighted spawnsWith = itemAmountSpawnsWith.SpawnsWith[i1];
                            if (shortnames.Contains(spawnsWith.Shortname))
                            {
                                RemoveFromArray(ref itemAmountSpawnsWith.SpawnsWith, spawnsWith);
                                count++;
                                shouldSave = true;
                            }
                        }
                    }
                    keepItemsWeighted.Add(itemAmountSpawnsWith);
                }
                else
                {
                    count++;
                    shouldSave = true;
                    shouldUpdate = true;
                    Puts($"Removing '{itemAmountSpawnsWith.Shortname}' from container '{container}'");
                }
            }

            if (shouldUpdate)
                items = keepItemsWeighted.ToArray();

            keepItemsWeighted.Clear();
        }
        #endregion
        
        #region Remove Empty Definitions
        
        private void FindAndRemoveEmptyDefinitions()
        {
            Puts($"Scanning loot tables for empty loot definitions...");

            int count = 0;
            
            FindAndRemoveEmptyDefinitions(data, storedData, "Loot Table", ref count);
            FindAndRemoveEmptyDefinitions(heli, heliData, "Heli Loot Table", ref count);
            FindAndRemoveEmptyDefinitions(bradley, bradleyData, "Bradley Loot Table", ref count);

            Puts($"Removed {count} empty loot definitions!");
        }

        private void FindAndRemoveEmptyDefinitions(DynamicConfigFile dynamicConfigFile, StoredData data, string table, ref int count)
        {
            Puts($"Scanning {table}...");

            bool shouldSave = false;

            foreach (KeyValuePair<string, AdvancedLootContainerProfile> kvp in data.loot_advanced)
            {
                foreach (LootSpawnSlot lootSpawnSlot in kvp.Value.LootSpawnSlots)
                {
                    if (lootSpawnSlot?.LootDefinition == null)
                        continue;
                    
                    FindRemoveEmptyDefinitionsRecursive(kvp.Key, ref lootSpawnSlot.LootDefinition, ref shouldSave, ref count);
                    
                    if (IsEmptyDefinition(lootSpawnSlot.LootDefinition))
                    {
                        RemoveFromArray(ref kvp.Value.LootSpawnSlots, lootSpawnSlot);
                        count++;
                        shouldSave = true;
                        Puts($"Removing empty definition from {kvp.Key}");
                    }
                }          
            }

            foreach (KeyValuePair<string, AdvancedNPCLootProfile> kvp in data.npcs_advanced)
            {
                foreach (LootSpawnSlot lootSpawnSlot in kvp.Value.LootSpawnSlots)
                {
                    if (lootSpawnSlot?.LootDefinition == null)
                        continue;
                    
                    FindRemoveEmptyDefinitionsRecursive(kvp.Key, ref lootSpawnSlot.LootDefinition, ref shouldSave, ref count);
                    
                    if (IsEmptyDefinition(lootSpawnSlot.LootDefinition))
                    {
                        RemoveFromArray(ref kvp.Value.LootSpawnSlots, lootSpawnSlot);
                        count++;
                        shouldSave = true;
                        Puts($"Removing empty definition from {kvp.Key}");
                    }
                }
            }

            foreach (KeyValuePair<string, AdvancedCustomLootProfile> kvp in data.custom_advanced)
            {
                foreach (LootSpawnSlot lootSpawnSlot in kvp.Value.LootSpawnSlots)
                {
                    if (lootSpawnSlot?.LootDefinition == null)
                        continue;
                    
                    FindRemoveEmptyDefinitionsRecursive(kvp.Key, ref lootSpawnSlot.LootDefinition, ref shouldSave, ref count);
                    
                    if (IsEmptyDefinition(lootSpawnSlot.LootDefinition))
                    {
                        RemoveFromArray(ref kvp.Value.LootSpawnSlots, lootSpawnSlot);
                        count++;
                        shouldSave = true;
                        Puts($"Removing empty definition from {kvp.Key}");
                    }
                }       
            }
            
            if (shouldSave)
                dynamicConfigFile.WriteObject(data);
        }

        private bool IsEmptyDefinition(LootSpawn lootSpawn) => lootSpawn.SubSpawn?.Length == 0 && lootSpawn.Items?.Length == 0;
        
        private void FindRemoveEmptyDefinitionsRecursive(string container, ref LootSpawn lootSpawn, ref bool shouldSave, ref int count)
        {
            if (lootSpawn.SubSpawn.Length > 0)
            {
                for (int i = lootSpawn.SubSpawn.Length - 1; i >= 0; i--)
                {
                    LootSpawn.Entry lootSpawnEntry = lootSpawn.SubSpawn[i];
                    if (lootSpawnEntry.Category != null)
                    {
                        FindRemoveEmptyDefinitionsRecursive(container, ref lootSpawnEntry.Category, ref shouldSave, ref count);
                        
                        if (IsEmptyDefinition(lootSpawnEntry.Category))
                        {
                            RemoveFromArray(ref lootSpawn.SubSpawn, lootSpawnEntry);
                            count++;
                            shouldSave = true;
                            Puts($"Removing empty definition from {container}");
                        }
                    }
                    else
                    {
                        if (lootSpawn.Items?.Length == 0)
                        {
                            RemoveFromArray(ref lootSpawn.SubSpawn, lootSpawnEntry);
                            count++;
                            shouldSave = true;
                            Puts($"Removing empty definition from {container}");
                        }
                    }
                }
            }
        }

        private void RemoveFromArray<T>(ref T[] array, T item)
        {
            if (array == null || array.Length == 0)
                return;

            int index = Array.IndexOf(array, item);
    
            if (index < 0)
                return;

            T[] newArray = new T[array.Length - 1];

            if (index > 0)
            {
                Array.Copy(array, 0, newArray, 0, index);
            }

            if (index < array.Length - 1)
            {
                Array.Copy(array, index + 1, newArray, index, array.Length - index - 1);
            }

            array = newArray;
        }
        #endregion

        #region Components
        private class LootCycler : MonoBehaviour
        {
            private LootContainer lootContainer;

            private void Awake()
            {
                lootContainer = GetComponent<LootContainer>();
                InvokeHandler.InvokeRepeating(this, lootContainer.SpawnLoot, 1f, 1f);
            }

            private void OnDestroy()
            {
                InvokeHandler.CancelInvoke(this, lootContainer.SpawnLoot);
            }
        }
        #endregion

        #region Commands
        [ChatCommand("aloot")]
        private void cmdRepopulateTarget(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
            {
                SendReply(player, "You do not have permission to use this command");
                return;
            }
            
            if (args == null || args.Length == 0)
            {
                SendReply(player, "/aloot repopulate - Repopulate the container you are looking at");
                SendReply(player, "/aloot view - List the contents of the container you are looking at");
                SendReply(player, "/aloot repopulateall - Repopulate every loot container on the map (can take upto 20 seconds)");
                return;
            }

            switch (args[0].ToLower())
            {
                case "repopulate":
                    {
                        LootContainer lootContainer = FindContainer(player);
                        if (lootContainer)
                        {
                            lootContainer.CancelInvoke(lootContainer.SpawnLoot);
                            lootContainer.SpawnLoot();

                            SendReply(player, $"Refreshed loot contents for {ToProfileName(lootContainer)}");
                        }
                        else SendReply(player, "No loot container found");
                    }
                    return;
                case "view":
                    {
                        LootContainer lootContainer = FindContainer(player);
                        if (lootContainer)
                        {                            
                            SendReply(player, $"Loot contents for {ToProfileName(lootContainer)};");
                            SendReply(player, lootContainer.inventory.itemList.Select(x => $"{x.info.displayName.english} x{x.amount}").ToSentence());
                        }
                        else SendReply(player, "No loot container found");
                    }
                    return;
                case "repopulateall":
                    {
                        SendReply(player, "Refreshing all loot containers...");
                        RefreshLootContents();
                    }
                    return;
                default:
                    SendReply(player, "Invalid syntax!");
                    break;
            }            
        }        

        [ConsoleCommand("al.repopulateall")]
        private void ccmdRepopulateAll(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                BasePlayer player = arg.Player();
                if (!player || !permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
                {
                    SendReply(arg, "You do not have permission to use this command");
                    return;
                }
            }

            RefreshLootContents(arg);
        }

        [ConsoleCommand("al.additems")]
        private void ccmdAddItemsl(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                BasePlayer player = arg.Player();
                if (!player || !permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
                {
                    SendReply(arg, "You do not have permission to use this command");
                    return;
                }
            }

            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(arg, "al.additems <shortname> <opt:shortname> <opt:shortname>... - Add the specified item(s) to your loot table.\nThis finds the containers the items are in from the default loot table, calculates a score and adds it to your existing loot table.\nYou can enter as many shortnames as you like");
                return;
            }

            AddSpecifiedItemsToLootTable(arg.Args);
        }
        
        [ConsoleCommand("al.removeitems")]
        private void ccmdRemoveItemsl(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                BasePlayer player = arg.Player();
                if (!player || !permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
                {
                    SendReply(arg, "You do not have permission to use this command");
                    return;
                }
            }

            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(arg, "al.removeitems <shortname> <opt:shortname> <opt:shortname>... - Removes the specified item(s) from your loot table.\nYou can enter as many shortnames as you like");
                return;
            }

            FindAndRemoveItems(arg.Args);
            FindAndRemoveEmptyDefinitions();
        }

        [ConsoleCommand("al.search")]
        private void ccmdSearchItem(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                BasePlayer player = arg.Player();
                if (!player || !permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
                {
                    SendReply(arg, "You do not have permission to use this command");
                    return;
                }
            }

            string shortname = arg.GetString(0);
            if (string.IsNullOrEmpty(shortname) || !ItemManager.itemDictionaryByName.ContainsKey(shortname))
            {
                SendReply(arg, "You must enter a valid item shortname to search");
                return;
            }

            Hash<string, int> containers = new Hash<string, int>();

            foreach (KeyValuePair<string, AdvancedLootContainerProfile> profile in storedData.loot_advanced)
            {
                int count = 0;
                foreach (LootSpawnSlot lootSpawnSlot in profile.Value.LootSpawnSlots)
                {
                    if (lootSpawnSlot?.LootDefinition == null)
                        continue;
                    
                    FindItemCountRecursive(lootSpawnSlot.LootDefinition, shortname, ref count);
                }

                if (count > 0)
                    containers[profile.Key] = count;
            }

            foreach (KeyValuePair<string, AdvancedNPCLootProfile> profile in storedData.npcs_advanced)
            {
                int count = 0;
                foreach (LootSpawnSlot lootSpawnSlot in profile.Value.LootSpawnSlots)
                {
                    if (lootSpawnSlot?.LootDefinition == null)
                        continue;
                    
                    FindItemCountRecursive(lootSpawnSlot.LootDefinition, shortname, ref count);
                }

                if (count > 0)
                    containers[profile.Key] = count;
            }
            
            foreach (KeyValuePair<string, AdvancedCustomLootProfile> profile in storedData.custom_advanced)
            {
                int count = 0;
                foreach (LootSpawnSlot lootSpawnSlot in profile.Value.LootSpawnSlots)
                {
                    if (lootSpawnSlot?.LootDefinition == null)
                        continue;
                    
                    FindItemCountRecursive(lootSpawnSlot.LootDefinition, shortname, ref count);
                }

                if (count > 0)
                    containers[profile.Key] = count;
            }

            foreach (KeyValuePair<string, SimpleLootContainerProfile> profile in storedData.loot_simple)
            {
                int count = 0;
                foreach (ItemAmountWeighted itemAmountWeighted in profile.Value.Items)
                {
                    if (itemAmountWeighted.Shortname == shortname)
                        count++;
                }

                if (count > 0)
                    containers[profile.Key] = count;
            }

            foreach (KeyValuePair<string, SimpleNPCLootProfile> profile in storedData.npcs_simple)
            {
                int count = 0;
                foreach (ItemAmountWeighted itemAmountWeighted in profile.Value.Items)
                {
                    if (itemAmountWeighted.Shortname == shortname)
                        count++;
                }

                if (count > 0)
                    containers[profile.Key] = count;
            }
            
            foreach (KeyValuePair<string, SimpleCustomLootProfile> profile in storedData.custom_simple)
            {
                int count = 0;
                foreach (ItemAmountWeighted itemAmountWeighted in profile.Value.Items)
                {
                    if (itemAmountWeighted.Shortname == shortname)
                        count++;
                }

                if (count > 0)
                    containers[profile.Key] = count;
            }

            if (containers.Count == 0)
            {
                SendReply(arg, $"The item {shortname} was not found in any loot profiles");
                return;
            }
            else
            {
                SendReply(arg, $"Found item {shortname} {containers.Sum(x => x.Value)} times in {containers.Count} loot profiles;{containers.Select(x => $"\n{x.Key} (x{x.Value})").ToSentence()}");
                return;
            }
        }

        private void FindItemCountRecursive(LootSpawn lootSpawn, string shortname, ref int count)
        {
            if (lootSpawn.SubSpawn.Length > 0)
            {
                foreach (LootSpawn.Entry lootSpawnEntry in lootSpawn.SubSpawn)                
                    FindItemCountRecursive(lootSpawnEntry.Category, shortname, ref count);                
            }
            else if (lootSpawn.Items.Length > 0)
            {
                foreach (ItemAmountRanged itemAmountRanged in lootSpawn.Items)
                {
                    if (itemAmountRanged.Shortname == shortname)                    
                        count++;                    
                }
            }
        }

        [ConsoleCommand("al.setloottable")]
        private void ccmdChangeConfig(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                BasePlayer player = arg.Player();
                if (!player || !permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
                {
                    SendReply(arg, "You do not have permission to use this command");
                    return;
                }
            }

            if (arg.Args is not { Length: 1 })
            {
                SendReply(arg, "Invalid arguments supplied! al.setloottable \"file name\"");
                return;
            }

            if (!Interface.Oxide.DataFileSystem.ExistsDatafile($"AlphaLoot/LootProfiles/{arg.Args[0]}"))
            {
                SendReply(arg, $"Unable to find a loot table with the name {arg.Args[0]}");
                return;
            }

            configData.ProfileName = arg.Args[0];
            SaveConfig();
           
            SendReply(arg, $"Loot table set to: {configData.ProfileName}");

            LoadLootTable();

            RefreshLootContents(arg);
        }

        [ConsoleCommand("al.setheliloottable")]
        private void ccmdChangeHeliConfig(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                BasePlayer player = arg.Player();
                if (!player || !permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
                {
                    SendReply(arg, "You do not have permission to use this command");
                    return;
                }
            }

            if (arg.Args is not { Length: 1 })
            {
                SendReply(arg, "Invalid arguments supplied! al.setheliloottable \"heli file name\"");
                return;
            }

            if (!Interface.Oxide.DataFileSystem.ExistsDatafile($"AlphaLoot/LootProfiles/{arg.Args[0]}"))
            {
                SendReply(arg, $"Unable to find a heli loot table with the name {arg.Args[0]}");
                return;
            }

            configData.HeliProfileName = arg.Args[0];
            SaveConfig();

            SendReply(arg, $"Heli Loot table set to: {configData.HeliProfileName}");

            LoadHeliTable();
        }

        [ConsoleCommand("al.setbradleyloottable")]
        private void ccmdChangeBradleyConfig(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                BasePlayer player = arg.Player();
                if (!player || !permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
                {
                    SendReply(arg, "You do not have permission to use this command");
                    return;
                }
            }

            if (arg.Args is not { Length: 1 })
            {
                SendReply(arg, "Invalid arguments supplied! al.setbradleyloottable \"heli file name\"");
                return;
            }

            if (!Interface.Oxide.DataFileSystem.ExistsDatafile($"AlphaLoot/LootProfiles/{arg.Args[0]}"))
            {
                SendReply(arg, $"Unable to find a bradley loot table with the name {arg.Args[0]}");
                return;
            }

            configData.BradleyProfileName = arg.Args[0];
            SaveConfig();

            SendReply(arg, $"Bradley Loot table set to: {configData.BradleyProfileName}");

            LoadBradleyTable();
        }

        [ConsoleCommand("al.generatetable")]
        private void ccmdGenerateTable(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                BasePlayer player = arg.Player();
                if (!player || !permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
                {
                    SendReply(arg, "You do not have permission to use this command");
                    return;
                }
            }

            if (arg.Args == null || arg.Args.Length < 3)
            {
                SendReply(arg, "al.generatetable <filename> <heli_filename> <bradley_filename> - Generate the default loot table to the specified file");
                return;
            }

            string fileName = arg.GetString(0);
            if (fileName.Equals(configData.ProfileName, System.StringComparison.OrdinalIgnoreCase))
            {
                SendReply(arg, "The filename you entered is the same as the loot table currently being used. Change the filename to something else");
                return;
            }

            string heliFileName = arg.GetString(1);
            if (fileName.Equals(configData.HeliProfileName, System.StringComparison.OrdinalIgnoreCase))
            {
                SendReply(arg, "The heli filename you entered is the same as the heli loot table currently being used. Change the filename to something else");
                return;
            }

            string bradleyFileName = arg.GetString(2);
            if (fileName.Equals(configData.BradleyProfileName, System.StringComparison.OrdinalIgnoreCase))
            {
                SendReply(arg, "The bradley filename you entered is the same as the bradley loot table currently being used. Change the filename to something else");
                return;
            }

            StoredData storedData = new StoredData();
            StoredData heliData = new StoredData();
            StoredData bradleyData = new StoredData();

            PopulateContainerDefinitions(ref storedData, ref heliData, ref bradleyData);

            Interface.Oxide.DataFileSystem.WriteObject<StoredData>($"AlphaLoot/LootProfiles/{fileName}", storedData);
            Interface.Oxide.DataFileSystem.WriteObject<StoredData>($"AlphaLoot/LootProfiles/{heliFileName}", heliData);
            Interface.Oxide.DataFileSystem.WriteObject<StoredData>($"AlphaLoot/LootProfiles/{bradleyFileName}", bradleyData);

            SendReply(arg, $"Generated a default loot table to /oxide/data/AlphaLoot/LootProfiles/ ({fileName}.json, {heliFileName}.json and {bradleyFileName}.json)");
        }

        [ConsoleCommand("al.skins")]
        private void ccmdALSkins(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                BasePlayer player = arg.Player();
                if (!player || !permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION))
                {
                    SendReply(arg, "You do not have permission to use this command");
                    return;
                }
            }

            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(arg, "al.skins add <shortname> <skinid> <opt:weight> - Add a single skin to the random skin list, with a optional argument to specify the weight of this skin");
                SendReply(arg, "al.skins remove <shortname> - Remove all skins for the specified item");
                SendReply(arg, "al.skins remove <shortname> <skinid> - Remove an individual skin");
                return;
            }

            switch (arg.Args[0].ToLower())
            {
                case "add":
                    {                        
                        string shortname = arg.GetString(1, string.Empty);
                        ulong skinId = arg.GetULong(2, 0UL);
                        int weight = arg.GetInt(3, 1); //13670

                        if (string.IsNullOrEmpty(shortname) || !ItemManager.FindItemDefinition(shortname))
                        {
                            SendReply(arg, "You must enter a valid item shortname");
                            return;
                        }
                                                
                        if (!weightedSkinIds.ContainsKey(shortname))
                            weightedSkinIds[shortname] = new HashSet<SkinEntry>();

                        weightedSkinIds[shortname].Add(new SkinEntry(skinId, weight));
                        skinData.WriteObject(weightedSkinIds);

                        SendReply(arg, $"You have added the skin {skinId} for item {shortname} with a weight of {weight}");
                    }
                    return;
                case "remove":
                    {
                        string shortname = arg.GetString(1, string.Empty);
                        if (string.IsNullOrEmpty(shortname) || !ItemManager.FindItemDefinition(shortname))
                        {
                            SendReply(arg, "You must enter a valid item shortname");
                            return;
                        }

                        if (arg.Args.Length < 3)
                        {
                            weightedSkinIds.Remove(shortname);
                            skinData.WriteObject(weightedSkinIds);
                            SendReply(arg, $"You have removed all skins for item {shortname}");
                        }
                        else
                        {
                            ulong skinId = arg.GetULong(2, 0UL);

                            if (weightedSkinIds.TryGetValue(shortname, out HashSet<SkinEntry> list))
                            {
                                for (int i = list.Count - 1; i >= 0; i--)
                                {
                                    SkinEntry skinEntry = list.ElementAt(i);
                                    if (skinEntry.SkinID == skinId)
                                    {
                                        weightedSkinIds[shortname].Remove(skinEntry);
                                        skinData.WriteObject(weightedSkinIds);
                                        SendReply(arg, $"You have removed the skin {skinId} for item {shortname}");
                                        return;
                                    }
                                }                                
                            }
                            else
                            {
                                SendReply(arg, $"There are no skins saved for item {shortname}");
                                return;
                            }
                        }                        
                    }
                    return;
                default:
                    break;
            }
        }
        #endregion

        #region Config        
        private static ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Auto-update loot tables with new items")]
            public bool AutoUpdate { get; set; }

            [JsonProperty(PropertyName = "Global Loot Multiplier (multiplies all loot amounts by the number specified)")]
            public float GlobalMultiplier { get; set; }

            [JsonProperty(PropertyName = "Apply global and individual loot multipliers to un-stackable items")]
            public bool MultiplyUnstackable { get; set; }

            [JsonProperty(PropertyName = "Loot Table Name")]
            public string ProfileName { get; set; }

            [JsonProperty(PropertyName = "Heli Loot Table Name")]
            public string HeliProfileName { get; set; }

            [JsonProperty(PropertyName = "Bradley Loot Table Name")]
            public string BradleyProfileName { get; set; }

            [JsonProperty(PropertyName = "Amount of crates to drop (Bradley APC - default 3, Set to -1 to disable)")]
            public int BradleyCrates { get; set; }

            [JsonProperty(PropertyName = "Amount of crates to drop (Patrol Helicopter - default 4, Set to -1 to disable)")]
            public int HelicopterCrates { get; set; }

            [JsonProperty(PropertyName = "Override FancyDrop containers with supply drop profile")]
            public bool OverrideFancyDrop { get; set; }

            [JsonProperty(PropertyName = "Use skins from the SkinBox skin list")]
            public bool UseSkinboxSkins { get; set; }

            [JsonProperty(PropertyName = "Use skins from the PlayerSkins skin list")]
            public bool UsePlayerSkinsSkins { get; set; }

            [JsonProperty(PropertyName = "Use skins from the approved skin list")]
            public bool UseApprovedSkins { get; set; }

            [JsonProperty(PropertyName = "Don't apply random workshop skins to the following items (shortnames)")]
            public HashSet<string> IgnoreSkinsFor { get; set; }

            [JsonProperty(PropertyName = "Override specified container profiles with another profile (profile name, override profile name)")]
            public Hash<string, string> ContainerOverrides { get; set; }

            public class IgnoreStackable
            {
                public string Shortname { get; set; }

                public ulong SkinID { get; set; } = 0;
            }
            
            public bool TryGetContainerOverride(string container, out string overrideContainer)
            {
                overrideContainer = string.Empty;
                
                return ContainerOverrides != null && ContainerOverrides.TryGetValue(container, out overrideContainer);
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
                AutoUpdate = false,
                GlobalMultiplier = 1f,
                MultiplyUnstackable = false,                
                ProfileName = "default_loottable",
                HeliProfileName = "default_heli_loottable",
                BradleyProfileName = "default_bradley_loottable",
                BradleyCrates = -1,
                HelicopterCrates = -1,
                OverrideFancyDrop = false,
                UseSkinboxSkins = false,
                UsePlayerSkinsSkins = false,
                UseApprovedSkins = false,
                IgnoreSkinsFor = new HashSet<string>(){"example.shortname1", "example.shortname2", "example.shortname3"},
                ContainerOverrides = new Hash<string, string>()
                {
                    ["example-to-override.underwater_labs/crate_elite"] = "overridden-by.crate_elite",
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(3, 0, 1))
            {
                configData.BradleyCrates = 3;
                configData.HelicopterCrates = 4;
            }

            if (configData.Version < new VersionNumber(3, 0, 4))
            {
                configData.UseApprovedSkins = false;
                configData.UseSkinboxSkins = false;
            }

            if (configData.Version < new VersionNumber(3, 0, 5))
            {
                updateContainerCapacities = true;
            }

            if (configData.Version < new VersionNumber(3, 0, 14))
            {
                configData.HeliProfileName = baseConfig.HeliProfileName;
                configData.BradleyProfileName = baseConfig.BradleyProfileName;
            }

            if (configData.Version < new VersionNumber(3, 1, 9))
            {
                configData.BradleyCrates = -1;
                configData.HelicopterCrates = -1;
            }

            if (configData.Version < new VersionNumber(3, 1, 25))
            {
                configData.IgnoreSkinsFor = baseConfig.IgnoreSkinsFor;
            }

            if (configData.Version < new VersionNumber(3, 1, 38))
            {
                configData.ContainerOverrides = baseConfig.ContainerOverrides;
            }

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Data Management
        private void SaveData()
        {
            data.WriteObject(storedData);
            heli.WriteObject(heliData);
            bradley.WriteObject(bradleyData);
        }

        private void LoadData()
        {
            LoadLootTable();
            LoadHeliTable();
            LoadBradleyTable();
            LoadSkinsData();
        }

        private void LoadLootTable()
        {
            PrintWarning($"Loading Loot Table from {configData.ProfileName}.json!");

            data = Interface.Oxide.DataFileSystem.GetFile($"AlphaLoot/LootProfiles/{configData.ProfileName}");
            
            try
            {
                storedData = data.ReadObject<StoredData>();
            }
            catch
            {
                storedData = new StoredData();
            }

            if (!storedData.IsValid)
            {
                PrintWarning("Invalid loot table file loaded, it contains no loot definitions! If this is a fresh install you can ignore this message, otherwise are you trying to load a ALv2.x.x loot table in to v3.x.x?");
                storedData = new StoredData();
            }
        }

        private void LoadHeliTable()
        {
            PrintWarning($"Loading Heli Loot Table from {configData.HeliProfileName}.json!");

            heli = Interface.Oxide.DataFileSystem.GetFile($"AlphaLoot/LootProfiles/{configData.HeliProfileName}");
            
            try
            {
                heliData = heli.ReadObject<StoredData>();
            }
            catch
            {
                heliData = new StoredData();
            }

            heliData.IsBaseLootTable = false;
            heliData.ProfileName = "heli_crate";
        }

        private void LoadBradleyTable()
        {
            PrintWarning($"Loading Bradley Loot Table from {configData.BradleyProfileName}.json!");

            bradley = Interface.Oxide.DataFileSystem.GetFile($"AlphaLoot/LootProfiles/{configData.BradleyProfileName}");
            
            try
            {
                bradleyData = bradley.ReadObject<StoredData>();
            }
            catch
            {
                bradleyData = new StoredData();
            }

            bradleyData.IsBaseLootTable = false;
            bradleyData.ProfileName = "bradley_crate";
        }

        private void LoadSkinsData()
        {
            skinData = Interface.Oxide.DataFileSystem.GetFile("AlphaLoot/item_skin_ids");

            try
            {
                weightedSkinIds = skinData.ReadObject<Hash<string, HashSet<SkinEntry>>>();
            }
            catch
            {
                weightedSkinIds = new Hash<string, HashSet<SkinEntry>>();
            }

            weightedSkinIds ??= new Hash<string, HashSet<SkinEntry>>();
        }

        private void SetCapacityLimits()
        {
            int count = 0;

            foreach (KeyValuePair<string, AdvancedLootContainerProfile> kvp in storedData.loot_advanced)
            {
                if (kvp.Value.MaximumItems == -1)
                {
                    string prefabPath = string.Empty;
                    for (int i = 0; i < GameManifest.Current.entities.Length; i++)
                    {
                        //int prefabId = 13670;
                        string path = GameManifest.Current.entities[i];

                        if (path.EndsWith($"{kvp.Key}.prefab", System.StringComparison.OrdinalIgnoreCase))
                        {
                            prefabPath = path;
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(prefabPath))
                    {
                        LootContainer container = GameManager.server.FindPrefab(prefabPath.ToLower()).GetComponent<LootContainer>();                       
                        kvp.Value.MaximumItems = container.inventorySlots;
                        count++;
                    }
                }
            }

            if (count > 0)
            {
                Puts($"Updated capacity limits for {count} advanced loot profiles");
                SaveData();
            }
        }
        
        #region Data Structure
        public class BaseLootProfile
        {
            public bool Enabled = true;

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public bool AllowSkinnedItems = true;

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public float LootMultiplier = 1;

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public int MinScrapAmount;
            
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public int MaxScrapAmount;

            [JsonIgnore]
            private static ItemDefinition _scrapDefinition;

            [JsonIgnore]
            public ItemDefinition ScrapDefinition
            {
                get
                {
                    if (!_scrapDefinition)
                        _scrapDefinition = ItemManager.FindItemDefinition("scrap");
                    return _scrapDefinition;
                }
            }

            [JsonIgnore]
            private static ItemDefinition _blueprintBase;

            [JsonIgnore]
            public static ItemDefinition BlueprintBaseDefinition
            {
                get
                {
                    if (!_blueprintBase)
                        _blueprintBase = ItemManager.FindItemDefinition("blueprintbase");
                    return _blueprintBase;
                }
            }


            public int GetScrapAmount() => UnityEngine.Random.Range(MinScrapAmount, MaxScrapAmount);

            public virtual void PopulateLoot(ItemContainer container)
            {
                if (container.playerOwner)                
                    return;                

                int scrapAmount = Mathf.RoundToInt(GetScrapAmount() * configData.GlobalMultiplier);
                if (scrapAmount > 0)
                {
                    container.capacity = container.itemList.Count + 1;

                    if (container.entityOwner is LootContainer lootContainer)
                    {
                        lootContainer.scrapAmount = scrapAmount;
                        lootContainer.GenerateScrap();
                    }
                    else ItemManager.Create(ScrapDefinition, scrapAmount).MoveToContainer(container);
                }
                else container.capacity = container.itemList.Count;
            }

            public virtual void PopulateLoot(ItemContainer itemContainer, string loadoutName)
            {
                PopulateLoot(itemContainer);
            }
        }

        public class BaseLootContainerProfile : BaseLootProfile
        {
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public bool DestroyOnEmpty = true;
            
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public bool ShouldRefreshContents;
            
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public bool IsItemLoot = false;
            
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public int MinSecondsBetweenRefresh = 3600;
            
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public int MaxSecondsBetweenRefresh = 7200;
        }

        public class SimpleNPCLootProfile : BaseLootProfile
        {
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public int MinimumItems;
            
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public int MaximumItems;

            public ItemAmountSpawnsWith[] Items;

            public SimpleNPCLootProfile() { }

            public SimpleNPCLootProfile(SimpleNPCLootProfile lootContainerProfile)
            {
                AllowSkinnedItems = lootContainerProfile.AllowSkinnedItems;
               
                MinScrapAmount = lootContainerProfile.MinScrapAmount;
                MaxScrapAmount = lootContainerProfile.MaxScrapAmount;

                MinimumItems = lootContainerProfile.MinimumItems;
                MaximumItems = lootContainerProfile.MaximumItems;

                Items = lootContainerProfile.Items;

                Enabled = lootContainerProfile.Enabled;
            }

            public override void PopulateLoot(ItemContainer container)
            {
                int count = UnityEngine.Random.Range(MinimumItems, MaximumItems + 1);

                container.capacity = count;

                List<ItemAmountSpawnsWith> items = Pool.Get<List<ItemAmountSpawnsWith>>();
                items.AddRange(Items);

                int itemCount = 0;
                while (itemCount < count)
                {
                    int totalWeight = items.Sum((ItemAmountSpawnsWith x) => x.Weight);

                    int random = UnityEngine.Random.Range(0, totalWeight);

                    for (int y = 0; y < items.Count; y++)
                    {
                        ItemAmountSpawnsWith itemAmountSpawnsWith = items[y];

                        totalWeight -= items[y].Weight;
                        if (random >= totalWeight)
                        {
                            items.Remove(itemAmountSpawnsWith);

                            itemAmountSpawnsWith.Create(container, LootMultiplier, AllowSkinnedItems, true, ref itemCount);
                            break;
                        }
                    }

                    if (items.Count == 0)
                        items.AddRange(Items);
                }
                
                container.capacity = items.Count;

                Pool.FreeUnmanaged(ref items);
                base.PopulateLoot(container);
            }
        }

        public class AdvancedNPCLootProfile : BaseLootProfile
        {       
            public LootSpawnSlot[] LootSpawnSlots;

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public int MaximumItems = -1;

            public AdvancedNPCLootProfile() { }

            public AdvancedNPCLootProfile(LootContainer.LootSpawnSlot[] lootSpawnSlots)
            {                
                LootSpawnSlots = new LootSpawnSlot[lootSpawnSlots?.Length ?? 0];

                for (int i = 0; i < lootSpawnSlots?.Length; i++)
                {
                    LootSpawnSlots[i] = new LootSpawnSlot(lootSpawnSlots[i], true);
                }
            }

            public AdvancedNPCLootProfile(AdvancedNPCLootProfile lootContainerProfile)
            {                
                MinScrapAmount = lootContainerProfile.MinScrapAmount;
                MaxScrapAmount = lootContainerProfile.MaxScrapAmount;

                MaximumItems = lootContainerProfile.MaximumItems;

                LootSpawnSlots = lootContainerProfile.LootSpawnSlots;

                AllowSkinnedItems = lootContainerProfile.AllowSkinnedItems;

                LootMultiplier = lootContainerProfile.LootMultiplier;

                Enabled = lootContainerProfile.Enabled;
            }

            public override void PopulateLoot(ItemContainer container, string loadoutName)
            {
                if (LootSpawnSlots != null && LootSpawnSlots.Length != 0)
                {
                    container.capacity = MaximumItems == -1 ? 36 : MaximumItems;
                    for (int i = 0; i < LootSpawnSlots.Length; i++)
                    {
                        LootSpawnSlot lootSpawnSlot = LootSpawnSlots[i];
                        if (lootSpawnSlot?.LootDefinition == null)
                            continue;
                        
                        for (int j = 0; j < lootSpawnSlot.NumberToSpawn; j++)
                        {
                            if ((string.IsNullOrEmpty(lootSpawnSlot.OnlyWithLoadoutNamed) || lootSpawnSlot.OnlyWithLoadoutNamed == loadoutName) && UnityEngine.Random.Range(0f, 1f) <= lootSpawnSlot.Probability)
                            {
                                lootSpawnSlot.LootDefinition.SpawnIntoContainer(container, this);
                            }
                        }
                    }
                }

                base.PopulateLoot(container);
            }
        }
        
        public class SimpleCustomLootProfile : BaseLootProfile
        {
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public int MinimumItems;
            
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public int MaximumItems;

            public ItemAmountSpawnsWith[] Items;

            public SimpleCustomLootProfile() { }

            public override void PopulateLoot(ItemContainer container)
            {
                int count = UnityEngine.Random.Range(MinimumItems, MaximumItems + 1);

                container.capacity = count;

                List<ItemAmountSpawnsWith> items = Pool.Get<List<ItemAmountSpawnsWith>>();
                items.AddRange(Items);

                int itemCount = 0;
                while (itemCount < count)
                {
                    int totalWeight = items.Sum((ItemAmountSpawnsWith x) => x.Weight);

                    int random = UnityEngine.Random.Range(0, totalWeight);

                    for (int y = 0; y < items.Count; y++)
                    {
                        ItemAmountSpawnsWith itemAmountSpawnsWith = items[y];

                        totalWeight -= items[y].Weight;
                        if (random >= totalWeight)
                        {
                            items.Remove(itemAmountSpawnsWith);

                            itemAmountSpawnsWith.Create(container, LootMultiplier, AllowSkinnedItems, true, ref itemCount);
                            break;
                        }
                    }

                    if (items.Count == 0)
                        items.AddRange(Items);
                }
                
                container.capacity = items.Count;

                Pool.FreeUnmanaged(ref items);
                base.PopulateLoot(container);
            }
        }
        
        public class AdvancedCustomLootProfile : BaseLootProfile
        {
            public LootSpawnSlot[] LootSpawnSlots;

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public int MaximumItems = 24;
            
            public AdvancedCustomLootProfile() { }

            public override void PopulateLoot(ItemContainer container)
            {
                if (LootSpawnSlots != null && LootSpawnSlots.Length != 0)
                {
                    container.capacity = Mathf.Min(MaximumItems, 24);
                    for (int i = 0; i < LootSpawnSlots.Length; i++)
                    {
                        LootSpawnSlot lootSpawnSlot = LootSpawnSlots[i];
                        if (lootSpawnSlot?.LootDefinition == null)
                            continue;
                        
                        for (int j = 0; j < lootSpawnSlot.NumberToSpawn; j++)
                        {
                            if (UnityEngine.Random.Range(0f, 1f) <= lootSpawnSlot.Probability)
                            {
                                lootSpawnSlot.LootDefinition.SpawnIntoContainer(container, this);
                            }
                        }
                    }
                }

                base.PopulateLoot(container);
            }
        }

        public class SimpleLootContainerProfile : BaseLootContainerProfile
        {
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public int MinimumItems;
            
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public int MaximumItems;

            public ItemAmountSpawnsWith[] Items;

            public SimpleLootContainerProfile() { }

            public SimpleLootContainerProfile(SimpleLootContainerProfile lootContainerProfile)
            {                
                DestroyOnEmpty = lootContainerProfile.DestroyOnEmpty;

                AllowSkinnedItems = lootContainerProfile.AllowSkinnedItems;
                ShouldRefreshContents = lootContainerProfile.ShouldRefreshContents;
                MinSecondsBetweenRefresh = lootContainerProfile.MinSecondsBetweenRefresh;
                MaxSecondsBetweenRefresh = lootContainerProfile.MaxSecondsBetweenRefresh;

                MinScrapAmount = lootContainerProfile.MinScrapAmount;
                MaxScrapAmount = lootContainerProfile.MaxScrapAmount;

                MinimumItems = lootContainerProfile.MinimumItems;
                MaximumItems = lootContainerProfile.MaximumItems;

                Items = lootContainerProfile.Items;

                Enabled = lootContainerProfile.Enabled;
            }

            public override void PopulateLoot(ItemContainer container)
            {
                int count = UnityEngine.Random.Range(MinimumItems, MaximumItems + 1);

                if (!container.playerOwner)
                    container.capacity = count;

                List<ItemAmountSpawnsWith> items = Pool.Get<List<ItemAmountSpawnsWith>>();
                items.AddRange(Items);

                int itemCount = 0;
                while (itemCount < count)
                {
                    int totalWeight = items.Sum((ItemAmountWeighted x) => x.Weight);

                    int random = UnityEngine.Random.Range(0, totalWeight);

                    for (int y = 0; y < items.Count; y++)
                    {
                        ItemAmountSpawnsWith itemAmountSpawnsWith = items[y];
                        
                        totalWeight -= items[y].Weight;
                        if (random >= totalWeight)
                        {
                            items.Remove(itemAmountSpawnsWith);
                            itemAmountSpawnsWith.Create(container, LootMultiplier, AllowSkinnedItems, true, ref itemCount);
                            break;
                        }
                    }

                    if (items.Count == 0)
                        items.AddRange(Items);
                }
                
                container.capacity = items.Count;

                Pool.FreeUnmanaged(ref items);
                base.PopulateLoot(container);
            }
        }

        public class AdvancedLootContainerProfile : BaseLootContainerProfile
        {
            public LootSpawnSlot[] LootSpawnSlots;

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public int MaximumItems = -1;

            public AdvancedLootContainerProfile() { }

            public AdvancedLootContainerProfile(LootContainer container)
            {
                bool hasCondition = container.SpawnType is LootContainer.spawnType.ROADSIDE or LootContainer.spawnType.TOWN;

                DestroyOnEmpty = container.destroyOnEmpty;
                ShouldRefreshContents = (!float.IsInfinity(container.minSecondsBetweenRefresh) && !float.IsInfinity(container.maxSecondsBetweenRefresh)) && container.shouldRefreshContents;

                if (!defaultScrapAmounts.TryGetValue(ToProfileName(container), out int scrapAmount))
                    scrapAmount = 1;

                MinScrapAmount = MaxScrapAmount = scrapAmount;

                MinSecondsBetweenRefresh = !ShouldRefreshContents ? 0 : Mathf.RoundToInt(container.minSecondsBetweenRefresh);
                MaxSecondsBetweenRefresh = !ShouldRefreshContents ? 0 : Mathf.RoundToInt(container.maxSecondsBetweenRefresh);

                MaximumItems = container.inventorySlots;

                LootSpawnSlots = new LootSpawnSlot[(container.LootSpawnSlots?.Length ?? 0) + 1];

                if (container.LootSpawnSlots?.Length > 0)
                {
                    LootSpawnSlots = new LootSpawnSlot[container.LootSpawnSlots?.Length ?? 0];
                    for (int i = 0; i < container.LootSpawnSlots?.Length; i++)
                    {
                        LootSpawnSlots[i] = new LootSpawnSlot(container.LootSpawnSlots[i], hasCondition);
                    }
                }
                else
                {
                    if (container.lootDefinition)
                    {
                        LootSpawnSlots = new LootSpawnSlot[]
                        {
                            new LootSpawnSlot(container.lootDefinition, container.maxDefinitionsToSpawn, hasCondition)
                        };
                    }
                }
            }

            public AdvancedLootContainerProfile(ItemModUnwrap itemModUnwrap)
            {                
                MaximumItems = -1;
                IsItemLoot = true;

                LootSpawnSlots = new LootSpawnSlot[]
                {
                    new LootSpawnSlot(itemModUnwrap.revealList, 1, false)
                };
            }

            public AdvancedLootContainerProfile(AdvancedLootContainerProfile lootContainerProfile)
            {
                DestroyOnEmpty = lootContainerProfile.DestroyOnEmpty;

                AllowSkinnedItems = lootContainerProfile.AllowSkinnedItems;

                ShouldRefreshContents = lootContainerProfile.ShouldRefreshContents;

                MinSecondsBetweenRefresh = lootContainerProfile.MinSecondsBetweenRefresh;
                MaxSecondsBetweenRefresh = lootContainerProfile.MaxSecondsBetweenRefresh;

                MinScrapAmount = lootContainerProfile.MinScrapAmount;
                MaxScrapAmount = lootContainerProfile.MaxScrapAmount;
                             
                MaximumItems = lootContainerProfile.MaximumItems;

                LootSpawnSlots = lootContainerProfile.LootSpawnSlots;

                Enabled = lootContainerProfile.Enabled;
            }

            public override void PopulateLoot(ItemContainer container)
            {
                if (LootSpawnSlots != null && LootSpawnSlots.Length != 0)
                {
                    if (!container.playerOwner)
                        container.capacity = MaximumItems == -1 ? 36 : MaximumItems;

                    for (int i = 0; i < LootSpawnSlots.Length; i++)
                    {
                        LootSpawnSlot lootSpawnSlot = LootSpawnSlots[i];
                        if (lootSpawnSlot?.LootDefinition == null)
                            continue;
                        
                        if (lootSpawnSlot.Eras == null || lootSpawnSlot.Eras.Length == 0 || Array.IndexOf(lootSpawnSlot.Eras, ConVar.Server.Era) != -1)
                        {
                            for (int j = 0; j < lootSpawnSlot.NumberToSpawn; j++)
                            {
                                if (UnityEngine.Random.Range(0f, 1f) <= lootSpawnSlot.Probability)
                                {
                                    lootSpawnSlot.LootDefinition.SpawnIntoContainer(container, this);
                                }
                            }
                        }
                    }
                }

                base.PopulateLoot(container);
            }
        }

        public class LootSpawnSlot
        {
            public LootSpawn LootDefinition;

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public int NumberToSpawn;

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public float Probability;

            [DefaultValue("")]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public string OnlyWithLoadoutNamed;
            
            public Era[] Eras = new Era[0];
            
            public bool ShouldSerializeEras() => Eras is { Length: > 0 };
            
            public LootSpawnSlot() { }

            public LootSpawnSlot(global::LootSpawn lootSpawn, int numberToSpawn, bool hasCondition)
            {
                LootDefinition = new LootSpawn(lootSpawn, hasCondition);
                NumberToSpawn = numberToSpawn;
                Probability = 1f;
            }

            public LootSpawnSlot(LootContainer.LootSpawnSlot lootSpawnSlot, bool hasCondition)
            {
                LootDefinition = new LootSpawn(lootSpawnSlot.definition, hasCondition);
                NumberToSpawn = lootSpawnSlot.numberToSpawn;
                Probability = lootSpawnSlot.probability;
                OnlyWithLoadoutNamed = lootSpawnSlot.onlyWithLoadoutNamed;
                Eras = lootSpawnSlot.eras;
            }
        }

        public class LootSpawn
        {
            public ItemAmountRanged[] Items = new ItemAmountRanged[0];

            public Entry[] SubSpawn = new Entry[0];
            
            public byte[] Node = new byte[0];

            [JsonIgnore]
            private Entry[] allowedSubSpawn;

            [JsonIgnore]
            private ItemAmountRanged[] allowedItems;
            
            [JsonIgnore]
            private Era era;
            
            public bool ShouldSerializeItems() => Items is { Length: > 0 };
            
            public bool ShouldSerializeSubSpawn() => SubSpawn is { Length: > 0 };
            
            public bool ShouldSerializeNode() => Node is { Length: > 0 };
            
            public LootSpawn() { }

            public LootSpawn(global::LootSpawn lootSpawn, bool hasCondition)
            {
                Items = new ItemAmountRanged[lootSpawn.items?.Length ?? 0];

                for (int i = 0; i < lootSpawn.items?.Length; i++)
                {
                    global::ItemAmountRanged itemAmountRanged = lootSpawn.items[i];

                    Items[i] = new ItemAmountRanged(itemAmountRanged.itemDef, itemAmountRanged.amount, itemAmountRanged.maxAmount, hasCondition);
                }

                SubSpawn = new Entry[lootSpawn.subSpawn?.Length ?? 0];

                for (int i = 0; i < lootSpawn.subSpawn?.Length; i++)
                {
                    global::LootSpawn.Entry subspawn = lootSpawn.subSpawn[i];

                    SubSpawn[i] = new Entry
                    {
                        Category = new LootSpawn(subspawn.category, hasCondition),
                        Weight = subspawn.weight,
                        ExtraSpawns = subspawn.extraSpawns,
                        RestrictedEras = subspawn.restrictedEras,
                    };                   
                }
            }
            
            private bool HasAnySpawns()
            {
                EnsureFilterUpdated();
                return allowedSubSpawn.Length != 0 || allowedItems.Length != 0;
            }
            
            private void EnsureFilterUpdated()
            {
                if (allowedSubSpawn != null && era == ConVar.Server.Era)
                    return;
                
                era = ConVar.Server.Era;
                
                for (int i = 0; i < SubSpawn.Length; i++)
                    SubSpawn[i].Category.EnsureFilterUpdated();
                
                if (SubSpawn == null || SubSpawn.Length == 0)
                    allowedSubSpawn = Array.Empty<Entry>();
                
                else allowedSubSpawn = SubSpawn.Where((entry) => 
                    entry.Category.HasAnySpawns() && 
                    (entry.RestrictedEras == null || entry.RestrictedEras.Length == 0 || Array.IndexOf(entry.RestrictedEras, ConVar.Server.Era) != -1)).ToArray();
                
                if (Items == null || Items.Length == 0)
                {
                    allowedItems = Array.Empty<ItemAmountRanged>();
                    return;
                }
                
                allowedItems = Items.Where((ItemAmountRanged x) => x.ItemDefinition.IsAllowedInEra(EraRestriction.Loot)).ToArray();
            }

            public void SpawnIntoContainer(ItemContainer container, BaseLootProfile lootProfile)
            {
                EnsureFilterUpdated();
                if (allowedSubSpawn != null && allowedSubSpawn.Length != 0)
                {
                    SubCategoryIntoContainer(container, lootProfile);
                    return;
                }

                if (allowedItems == null) 
                    return;
                
                int itemCount = 0;
                foreach (ItemAmountRanged itemAmountRanged in allowedItems)
                {
                    itemAmountRanged?.Create(container, lootProfile.LootMultiplier, lootProfile.AllowSkinnedItems, false, ref itemCount);
                }
            }

            private void SubCategoryIntoContainer(ItemContainer container, BaseLootProfile lootProfile)
            {
                int totalWeight = allowedSubSpawn.Sum((LootSpawn.Entry x) => x.Weight);

                int random = UnityEngine.Random.Range(0, totalWeight);

                for (int i = 0; i < allowedSubSpawn.Length; i++)
                {
                    if (allowedSubSpawn[i].Category != null)
                    {
                        totalWeight -= allowedSubSpawn[i].Weight;
                        if (random >= totalWeight)
                        {
                            for (int j = 0; j < 1 + allowedSubSpawn[i].ExtraSpawns; j++)
                                allowedSubSpawn[i].Category.SpawnIntoContainer(container, lootProfile);

                            return;
                        }
                    }
                }
            }

            public class Entry
            {
                public LootSpawn Category;

                [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
                public int Weight;
                
                [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
                public int ExtraSpawns;
                
                public Era[] RestrictedEras = new Era[0];

                public byte[] Node = new byte[0];
                
                public bool ShouldSerializeRestrictedEras() => RestrictedEras is { Length: > 0 };
                
                public bool ShouldSerializeNode() => Node is { Length: > 0 };
            }
        }

        public class ItemAmountSpawnsWith : ItemAmountWeighted
        {
            public ItemAmountWeighted[] SpawnsWith = new ItemAmountWeighted[0];

            public override void CreateAdditionalItems(ItemContainer container, float lootMultiplier, bool allowSkinnedItems, bool expandContainer, ref int itemCount)
            {
                if (SpawnsWith == null || SpawnsWith.Length == 0)
                    return;

                foreach (ItemAmountWeighted itemAmountWeighted in SpawnsWith)
                {
                    if (!container.playerOwner && expandContainer)
                        container.capacity++;
                    
                    itemAmountWeighted.Create(container, lootMultiplier, allowSkinnedItems, expandContainer, ref itemCount);
                }
            }
            
            public bool ShouldSerializeSpawnsWith() => SpawnsWith is { Length: > 0 };
        }

        public class ItemAmountWeighted : ItemAmountRanged
        {
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public int Weight = 1;
            
            public Era[] RestrictedEras = new Era[0];
            
            public bool ShouldSerializeRestrictedEras() => RestrictedEras is { Length: > 0 };
        }

        public class ItemAmountRanged : ItemAmount
        {
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public float MaxAmount = -1f;

            public ItemAmountRanged() : base() { }

            public ItemAmountRanged(ItemDefinition item = null, float amount = 0f, float maxAmount = -1f, bool hasCondition = false) : base(item, amount, hasCondition)
            {
                this.MaxAmount = Mathf.Max(maxAmount, amount);
            }
            
            
            public void Create(ItemContainer container, float lootMultiplier, bool allowSkinnedItems, bool expandContainer, ref int itemCount)
            {
                Item item = null;
                if (WantsBlueprint())
                {
                    ItemDefinition blueprintBaseDef = BaseLootProfile.BlueprintBaseDefinition;
                    if (!blueprintBaseDef)
                        return;

                    item = ItemManager.Create(blueprintBaseDef, 1, 0UL);
                    item.blueprintTarget = ItemID;
                }
                else
                {
                    item = ItemManager.CreateByItemID(ItemID, (int)GetAmount(lootMultiplier), GetSkinID(allowSkinnedItems));

                    if (!string.IsNullOrEmpty(ItemName))
                        item.name = ItemName;
                                
                    if (!string.IsNullOrEmpty(ItemText))
                        item.text = ItemText;

                    if (item.hasCondition)
                        item.condition = GetConditionFraction() * item.info.condition.max;
                }
                
                item.OnVirginSpawn();
                if (!item.MoveToContainer(container, -1, true))
                {
                    if (!container.playerOwner)
                        item.Remove(0f);
                    else item.Drop(container.playerOwner.GetDropPosition(), container.playerOwner.GetDropVelocity(), Quaternion.identity);
                }

                itemCount++;
                
                CreateAdditionalItems(container, lootMultiplier, allowSkinnedItems, expandContainer, ref itemCount);
            }
            
            public virtual void CreateAdditionalItems(ItemContainer container, float lootMultiplier, bool allowSkinnedItems, bool expandContainer, ref int itemCount){}

            public override float GetAmount(float lootMultiplier)
            {
                ItemDefinition itemDefinition = ItemDefinition;
                if (!itemDefinition)
                    return 0;
                
                bool isStackable = (itemDefinition.stackable > 1 && !itemDefinition.condition.enabled) || configData.MultiplyUnstackable;
                                
                if (MinAmount == MaxAmount)
                {
                    if (!isStackable || DontMultiply)
                        return Mathf.Clamp(MinAmount, 1f, float.MaxValue);

                    return Mathf.Clamp((MinAmount * lootMultiplier) * configData.GlobalMultiplier, 1f, float.MaxValue);
                }

                if (!isStackable || DontMultiply)
                    return Mathf.Clamp(UnityEngine.Random.Range(MinAmount, MaxAmount), 1f, float.MaxValue);

                return Mathf.Clamp((UnityEngine.Random.Range(MinAmount, MaxAmount) * lootMultiplier) * configData.GlobalMultiplier, 1f, float.MaxValue);
            }
        }
                
        public class ItemAmount
        {
            public string Shortname;

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public float BlueprintChance;
           
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public float MinAmount;

            [DefaultValue("")]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public string ItemName = string.Empty;
            
            [DefaultValue("")]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public string ItemText = string.Empty;

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public ulong SkinID = 0UL;

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
            public bool DontMultiply = false;

            public ConditionItem Condition = new ConditionItem();

            [JsonIgnore]
            private int _itemId = -1;

            [JsonIgnore]
            public int ItemID
            {
                get
                {
                    if (_itemId < 0)
                    {
                        ItemDefinition itemDefinition = ItemDefinition;
                        if (itemDefinition)
                            _itemId = itemDefinition.itemid;
                    }
                    return _itemId;
                }
            }

            [JsonIgnore] 
            private ItemDefinition _itemDefinition;

            [JsonIgnore]
            public ItemDefinition ItemDefinition
            {
                get
                {
                    if (!_itemDefinition && !string.IsNullOrEmpty(Shortname))
                        _itemDefinition = ItemManager.FindItemDefinition(Shortname);

                    if (!_itemDefinition)
                        Debug.LogError($"[AlphaLoot] - Failed to find ItemDefinition for {Shortname}!");
                    
                    return _itemDefinition;
                }
            }
            
            public bool ShouldSerializeCondition() => Condition != null && (Condition.MinCondition != 1f || Condition.MaxCondition != 1f);

            public ItemAmount() { }

            public ItemAmount(ItemDefinition item = null, float amount = 0f, bool hasCondition = false)
            {
                Shortname = item.shortname;

                BlueprintChance = item.spawnAsBlueprint ? 1f : 0f;

                MinAmount = amount;

                Condition.MinCondition = hasCondition && item.condition.enabled ? item.condition.foundCondition.fractionMin : 1f;
                Condition.MaxCondition = hasCondition && item.condition.enabled ? item.condition.foundCondition.fractionMax : 1f;
            }

            public virtual float GetAmount(float lootMultiplier)
            {
                ItemDefinition itemDefinition = ItemDefinition;
                if (!itemDefinition)
                    return 0;
                
                bool isStackable = (itemDefinition.stackable > 1 && !itemDefinition.condition.enabled) || configData.MultiplyUnstackable;

                if (!isStackable || DontMultiply)
                    return Mathf.Clamp(MinAmount, 1f, float.MaxValue);

                return Mathf.Clamp((MinAmount * lootMultiplier) * configData.GlobalMultiplier, 1f, float.MaxValue);
            }

            public ulong GetSkinID(bool allowRandomSkins)
            {
                if (SkinID != 0UL)
                    return SkinID;

                if (allowRandomSkins)
                    return RandomSkinID();

                return 0UL;
            }

            private ulong RandomSkinID()
            {
                if (weightedSkinIds.TryGetValue(Shortname, out HashSet<SkinEntry> hashset) && hashset.Count > 0)
                {
                    int totalWeight = hashset.Sum((SkinEntry x) => x.Weight);

                    int random = UnityEngine.Random.Range(0, totalWeight);

                    foreach (SkinEntry skinEntry in hashset)
                    {
                        totalWeight -= skinEntry.Weight;

                        if (random >= totalWeight)                        
                            return skinEntry.SkinID;                        
                    }

                }

                if (!configData.IgnoreSkinsFor.Contains(Shortname) && importedSkinIds != null)
                {
                    if (importedSkinIds.TryGetValue(Shortname, out List<ulong> list) && list.Count > 0)
                    {
                        return list.GetRandom();
                    }
                }

                return 0UL;           
            }

            public float GetConditionFraction() => Condition == null ? 1f : UnityEngine.Random.Range(Condition.MinCondition, Condition.MaxCondition);
            
            public bool WantsBlueprint() => UnityEngine.Random.Range(0.0f, 1.0f) < BlueprintChance;

            public class ConditionItem
            {
                [DefaultValue(1f)]
                [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
                public float MinCondition = 1f;

                [DefaultValue(1f)]
                [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
                public float MaxCondition = 1f;
            }
        }

        public class SkinEntry
        {
            public int Weight;
            public ulong SkinID;

            public SkinEntry() { }

            public SkinEntry(ulong skinId, int weight = 1)
            {
                this.SkinID = skinId;
                this.Weight = weight;
            }
        }
        #endregion

        private class StoredData
        {
            public Hash<string, SimpleLootContainerProfile> loot_simple = new Hash<string, SimpleLootContainerProfile>();

            public Hash<string, AdvancedLootContainerProfile> loot_advanced = new Hash<string, AdvancedLootContainerProfile>();

            public Hash<string, AdvancedNPCLootProfile> npcs_advanced = new Hash<string, AdvancedNPCLootProfile>();

            public Hash<string, SimpleNPCLootProfile> npcs_simple = new Hash<string, SimpleNPCLootProfile>();
            
            public Hash<string, AdvancedCustomLootProfile> custom_advanced = new Hash<string, AdvancedCustomLootProfile>();

            public Hash<string, SimpleCustomLootProfile> custom_simple = new Hash<string, SimpleCustomLootProfile>();

            public Hash<string, List<string>> npc_loadouts = new Hash<string, List<string>>();

            public bool IsBaseLootTable = true;

            public string ProfileName = string.Empty;
           
            [JsonIgnore]
            public bool IsValid => loot_simple != null && loot_advanced != null && npcs_advanced != null && npcs_simple != null && (loot_advanced.Count > 0 || loot_simple.Count > 0) && (npcs_advanced.Count != 0 || npcs_simple.Count != 0);

            [JsonIgnore]
            public bool HasAnyProfiles => loot_simple != null && loot_advanced != null && npcs_advanced != null && npcs_simple != null && (loot_advanced.Count > 0 || loot_simple.Count > 0 || npcs_advanced.Count > 0 || npcs_simple.Count > 0);

            public void CreateDefaultLootProfile(LootContainer container)
            {
                string profileName = ToProfileName(container);
                
                if (string.IsNullOrEmpty(profileName))                
                    profileName = container.name;
                
                if (loot_advanced.ContainsKey(profileName) || loot_simple.ContainsKey(profileName))
                    return;

                loot_advanced.Add(profileName, new AdvancedLootContainerProfile(container));
            }

            public void CreateDefaultLootProfile(ItemDefinition itemDefinition, ItemModUnwrap itemModUnwrap)
            {                
                if (loot_advanced.ContainsKey(itemDefinition.shortname) || loot_simple.ContainsKey(itemDefinition.shortname))
                    return;

                loot_advanced.Add(itemDefinition.shortname, new AdvancedLootContainerProfile(itemModUnwrap));
            }

            public void CloneLootProfile(string shortname, BaseLootProfile lootContainerProfile)
            {
                if (lootContainerProfile is AdvancedLootContainerProfile profile)
                {
                    loot_advanced[shortname] = new AdvancedLootContainerProfile(profile);
                }
                else if (lootContainerProfile is SimpleLootContainerProfile containerProfile)
                {
                    loot_simple[shortname] = new SimpleLootContainerProfile(containerProfile);
                }
                else if (lootContainerProfile is AdvancedNPCLootProfile lootProfile)
                {
                    npcs_advanced[shortname] = new AdvancedNPCLootProfile(lootProfile);
                }
                else if (lootContainerProfile is SimpleNPCLootProfile npcLootProfile)
                {
                    npcs_simple[shortname] = new SimpleNPCLootProfile(npcLootProfile);
                }
            }

            public void CreateDefaultLootProfile(string shortPrefabName, LootContainer.LootSpawnSlot[] lootSpawnSlots, PlayerInventoryProperties[] loadouts)
            {                
                if (!npc_loadouts.TryGetValue(shortPrefabName, out List<string> _loadouts))
                    npc_loadouts[shortPrefabName] = _loadouts = new List<string>();
                
                foreach (PlayerInventoryProperties playerInventoryProperties in loadouts)
                {
                    if (!_loadouts.Contains(playerInventoryProperties.niceName))
                        _loadouts.Add(playerInventoryProperties.niceName);
                }
                
                if (npcs_advanced.ContainsKey(shortPrefabName) || npcs_simple.ContainsKey(shortPrefabName))
                    return;

                npcs_advanced.Add(shortPrefabName, new AdvancedNPCLootProfile(lootSpawnSlots));
            }

            public bool Exists(string name)
            {
                return loot_advanced.ContainsKey(name) || loot_simple.ContainsKey(name) ||
                       npcs_advanced.ContainsKey(name) || npcs_simple.ContainsKey(name) ||
                       custom_advanced.ContainsKey(name) || custom_simple.ContainsKey(name);
            }

            public bool TryGetLootProfile(string shortname, out BaseLootContainerProfile profile)
            {
                if (loot_advanced.TryGetValue(shortname, out AdvancedLootContainerProfile advancedLootContainerProfile))
                {
                    profile = advancedLootContainerProfile;
                    return true;
                }

                if (loot_simple.TryGetValue(shortname, out SimpleLootContainerProfile simpleLootContainerProfile))
                {
                    profile = simpleLootContainerProfile;
                    return true;
                }

                profile = null;
                return false;
            }

            public bool TryGetNPCProfile(string shortname, out BaseLootProfile profile)
            {
                if (npcs_advanced.TryGetValue(shortname, out AdvancedNPCLootProfile advancedNPCLootProfile))
                {
                    profile = advancedNPCLootProfile;
                    return true;
                }

                if (npcs_simple.TryGetValue(shortname, out SimpleNPCLootProfile simpleNPCLootProfile))
                {
                    profile = simpleNPCLootProfile;
                    return true;
                }

                profile = null;
                return false;
            }
            
            public bool TryGetCustomProfile(string shortname, out BaseLootProfile profile)
            {
                if (custom_advanced.TryGetValue(shortname, out AdvancedCustomLootProfile advancedCustomLootProfile))
                {
                    profile = advancedCustomLootProfile;
                    return true;
                }

                if (custom_simple.TryGetValue(shortname, out SimpleCustomLootProfile simpleCustomLootProfile))
                {
                    profile = simpleCustomLootProfile;
                    return true;
                }

                profile = null;
                return false;
            }

            #region Random Profiles
            [JsonIgnore]
            private List<BaseLootContainerProfile> randomList = new List<BaseLootContainerProfile>();

            public bool GetRandomLootProfile(out BaseLootContainerProfile profile)
            {
                if (randomList.Count == 0)
                {
                    randomList.AddRange(loot_simple.Values);
                    randomList.AddRange(loot_advanced.Values);
                }

                RESTART_RANDOM:
                if (randomList.Count == 0)
                {
                    profile = null;
                    return false;
                }

                profile = randomList.GetRandom();                
                randomList.Remove(profile);

                if (!profile.Enabled)
                    goto RESTART_RANDOM;

                return true;
            }
            #endregion

            public void RemoveProfile(string shortname)
            {
                loot_simple.Remove(shortname);
                loot_advanced.Remove(shortname);
                npcs_simple.Remove(shortname);
                npcs_advanced.Remove(shortname);
            }
        }
        #endregion
        
        [AutoPatch]
        [HarmonyPatch(typeof(LootContainer), nameof(LootContainer.SpawnLoot))]
        private class LootContainer_SpawnLoot
        {
            [HarmonyPrefix]
            private static void Prefix(LootContainer __instance)
            {
                if (__instance is not HackableLockedCrate)
                    return;
                
                __instance.inventory.onItemAddedRemoved = null;
            }
            
            [HarmonyPostfix]
            private static void Postfix(LootContainer __instance)
            {
                if (__instance is not HackableLockedCrate)
                    return;
                
                __instance.inventory.onItemAddedRemoved = __instance.OnItemAddedOrRemoved;
            }
        }
        
        #region Manual Loot Update Feb 2025

        [ConsoleCommand("al.reset.primitive")]
        private void ccmdResetPrimitive(ConsoleSystem.Arg arg)
        {
            string[] primitiveItems = new string[]
            {
                "ballista.bolt.hammerhead",
                "ballista.bolt.incendiary",
                "ballista.bolt.piercer",
                "ballista.bolt.pitchfork",
                "ballista.mounted",
                "ballista.static",
                "batteringram",
                "catapult.ammo.explosive",
                "catapult.ammo.incendiary",
                "catapult.ammo.boulder",
                "catapult",
                "siegetower",
                "metal.shield",
                "minicrossbow",
                "reinforced.wooden.shield",
            };
            
            FindAndRemoveItems(primitiveItems);
            FindAndRemoveEmptyDefinitions();
            AddEraToLootTable(Era.Primitive);
        }
        
        private void AddEraToLootTable(Era era)
        {
            StoredData defaultLootTable = new StoredData();
            StoredData defaultHeliLootTable = new StoredData();
            StoredData defaultBradleyLootTable = new StoredData();

            int additions = 0;
            
            Puts("Finding era specific loot definitions. This only applies to advanced loot profiles...");
            
            PopulateContainerDefinitions(ref defaultLootTable, ref defaultHeliLootTable, ref defaultBradleyLootTable);
            
            AddEraToLootTable(defaultLootTable, storedData, era, ref additions);
            AddEraToLootTable(defaultHeliLootTable, heliData, era, ref additions);
            AddEraToLootTable(defaultBradleyLootTable, bradleyData, era, ref additions);
            
            if (additions > 0)
                SaveData();
            
            Puts($"Added {additions} era specific loot definitions to the loot tables.");
        }

        private void AddEraToLootTable(StoredData source, StoredData dest, Era era, ref int additions)
        {
            foreach (KeyValuePair<string, AdvancedLootContainerProfile> kvp in source.loot_advanced)
            {
                if (dest.loot_advanced.TryGetValue(kvp.Key, out AdvancedLootContainerProfile lootProfile))
                {
                    InsertLootDefinitionsWithEra(kvp.Key, era, ref kvp.Value.LootSpawnSlots, ref lootProfile.LootSpawnSlots, ref additions);
                }
            }

            foreach (KeyValuePair<string, AdvancedNPCLootProfile> kvp in source.npcs_advanced)
            {
                if (dest.npcs_advanced.TryGetValue(kvp.Key, out AdvancedNPCLootProfile lootProfile))
                {
                    InsertLootDefinitionsWithEra(kvp.Key, era, ref kvp.Value.LootSpawnSlots, ref lootProfile.LootSpawnSlots, ref additions);
                }
            }
        }
        
        private void InsertLootDefinitionsWithEra(string container, Era era, ref LootSpawnSlot[] sourceSlots, ref LootSpawnSlot[] destSlots, ref int additions)
        {
            for (int i = 0; i < sourceSlots.Length; i++)
            {
                
                LootSpawnSlot lootSpawnSlot = sourceSlots[i];
                if (lootSpawnSlot?.LootDefinition == null)
                    continue;
                
                if (lootSpawnSlot.Eras == null || !lootSpawnSlot.Eras.Contains(era))
                    continue;
                
                Puts($"Inserting era specific loot definition in to {container}");
                Array.Resize(ref destSlots, destSlots.Length + 1);
                destSlots[destSlots.Length - 1] = lootSpawnSlot;
                additions++;
            }
        }
        #endregion       
    }      
}   
 