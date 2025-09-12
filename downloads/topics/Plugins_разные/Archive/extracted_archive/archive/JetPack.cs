using System.Collections.Generic;
using Newtonsoft.Json;
using CompanionServer.Handlers;
using UnityEngine;
using System;
using Rust;
using Oxide.Core.Plugins;
using System.Reflection;
using Oxide.Plugins.JetPackExtensionMethods;
using Oxide.Core;
using System.Collections;

namespace Oxide.Plugins
{
    [Info("JetPack", "Adem", "1.1.3")]
    class JetPack : RustPlugin
    {
        #region Variables
        const bool en = true;
        private static JetPack ins;
        [PluginReference] private Plugin ZoneManager, Space;
        HashSet<ulong> noFallDamage = new HashSet<ulong>();
        HashSet<JetPackComponent> jetpacks = new HashSet<JetPackComponent>();
        HashSet<string> subscribeMetods = new HashSet<string>
        {
            "CanWearItem",
            "OnItemAddedToContainer",
            "OnItemRemovedFromContainer",
            "OnPlayerDeath",
            "OnPlayerDisconnected",
            "OnEntityDismounted",
            "CanMoveItem",
            "OnActiveItemChanged",
            "OnPlayerCommand"
        };
        #endregion Variables

        #region Hooks
        void Init()
        {
            ins = this;
            Unsubscribes();
        }

        void OnServerInitialized()
        {
            UpdateConfig();
            Subscribes();
            RegisterPermissions();
            JetpackCheckPlayerInputComponent.AddComponentsToAllPLayers();
            if (plugins.Exists("Space") && (bool)Space.Call("IsEventActive")) OnSpaceEventStart();
        }

        void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList) RemoveJetPack(player.userID);
            ins = null;
            JetpackCheckPlayerInputComponent.RemoveAllPlayerComponents();
        }

        object CanWearItem(PlayerInventory inventory, Item item)
        {
            if (inventory == null || item == null) return null;
            BasePlayer player = inventory.baseEntity;
            if (player == null) return null;

            if (item.info.shortname == _config.itemSetting.shortname && item.skin == _config.itemSetting.skin)
            {
                if (!permission.UserHasPermission(player.UserIDString, _config.usePermission))
                {
                    PrintToChat(player, GetMessage("NoPermission", player.userID, ins._config.prefics));
                    return true;
                }
                WearJetPack(player, false);
                JetpackCheckPlayerInputComponent.TryAddComponentToPlayer(player);
            }
            else if (!_config.airTakeOff)
            {
                JetPackComponent jetPack = jetpacks.FirstOrDefault(x => x != null && x.GetPlayerUserId() == player.userID);
                if (jetPack != null) return false;
            }
            return null;
        }

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (!IsJetPackItem(item)) return;
            BasePlayer player = container.playerOwner;
            if (_config.fuel.fuel && player != null && container == player.inventory.containerBelt && item.info.shortname == _config.fuel.itemShortname)
            {
                JetPackComponent jetPack = jetpacks.FirstOrDefault(x => x != null && x.GetPlayerUserId() == player.userID);
                if (jetPack != null) jetPack.CheckFuel(false);
            }
        }

        void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (!IsJetPackItem(item)) return;
            BasePlayer player = container.playerOwner;
            if (player == null || container != player.inventory.containerWear) return;
            RemoveJetPack(player.userID);
            JetpackCheckPlayerInputComponent.TryRemoveComponentFromPlayer(player.userID);
        }

        object CanMoveItem(Item item, PlayerInventory playerLoot, uint targetContainer, int targetSlot, int amount)
        {
            if (item.info.shortname == _config.itemSetting.shortname && item.skin == _config.itemSetting.skin)
            {
                BasePlayer player = playerLoot.GetComponentInParent<BasePlayer>();
                if (player != null)
                {
                    JetPackComponent jetPack = jetpacks.FirstOrDefault(x => x != null && x.GetPlayerUserId() == player.userID);
                    if (jetPack == null || (SupportedPluginsController.isSpaceEventActive && SupportedPluginsController.IsPositionInSpace(player.transform.position))) return null;
                    if (!jetPack.onGround)
                    {
                        foreach (Collider collider in UnityEngine.Physics.OverlapSphere(player.transform.position, 2f))
                        {
                            BaseEntity entity = collider.ToBaseEntity();
                            if (entity == null) continue;
                            if (entity is CargoShip) return null;
                        }
                        return true;
                    }
                }
            }
            return null;
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (PlayerHaveJetPackItem(player)) JetpackCheckPlayerInputComponent.TryAddComponentToPlayer(player);
        }

        void OnPlayerDeath(BasePlayer player)
        {
            RemoveJetPack(player.userID);
            JetpackCheckPlayerInputComponent.TryRemoveComponentFromPlayer(player.userID);
        }

        void OnPlayerSleep(BasePlayer player)
        {
            RemoveJetPack(player.userID);
            JetpackCheckPlayerInputComponent.TryRemoveComponentFromPlayer(player.userID);
        }

        void OnPlayerKicked(BasePlayer player, string reason)
        {
            RemoveJetPack(player.userID);
            JetpackCheckPlayerInputComponent.TryRemoveComponentFromPlayer(player.userID);
        }

        void OnEntityDismounted(BaseMountable entity, BasePlayer player) => RemoveJetPack(player.userID);

        void OnLootSpawn(LootContainer container)
        {
            if (!_config.inLoot || container == null || container.inventory == null) return;
            CrateConfig config = _config.crates.FirstOrDefault(x => x.Prefab == container.PrefabName);
            if (config == null) return;
            if (UnityEngine.Random.Range(0f, 100f) <= config.Chance)
            {
                Item jetPack = CreateItem();
                container.inventory.Remove(container.inventory.itemList.FirstOrDefault(x => true));
                if (!jetPack.MoveToContainer(container.inventory)) jetPack.Remove();
            }
        }

        void OnSamSiteTargetScan(SamSite samSite, List<SamSite.ISamSiteTarget> targetList)
        {
            foreach (JetPackComponent jetPackComponent in jetpacks)
            {
                if (jetPackComponent == null || jetPackComponent.player == null) continue;
                BuildingPrivlidge buildingPrivlidge = samSite.GetBuildingPrivilege();
                if (buildingPrivlidge != null && buildingPrivlidge.IsAuthed(jetPackComponent.player)) continue;
                if (samSite.ShortPrefabName == "sam_static")
                {
                    if (_config.samSiteStatic) targetList.Add(jetPackComponent.samSiteComponent);
                    continue;
                }
                else if (_config.samSite) targetList.Add(jetPackComponent.samSiteComponent);
            }
        }

        void OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
            if (player != null && player.userID.IsSteamId() && noFallDamage.Contains(player.userID)) info.damageTypes.Scale(DamageType.Fall, 0);
        }

        object CanLootEntity(BasePlayer player, SupplyDrop container)
        {
            JetPackComponent jetPack = jetpacks.FirstOrDefault(x => x != null && x.GetPlayerUserId() == player.userID);
            if (jetPack != null) return true;
            return null;
        }

        object OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            if (player != null && _config.blockedCommands.Any(x => x.ToLower() == command.ToLower()) && jetpacks.Any(x => x != null && x.GetPlayerUserId() == player.userID))
            {
                PrintToChat(player, GetMessage("CommandBlock", player.userID, ins._config.prefics));
                return true;
            }
            return null;
        }

        #region SupportedPlugins
        void OnEntityEnterZone(string ZoneID, DroppedItemContainer entity)
        {
            if (!_config.zoneManager) return;
            JetPackComponent jetPackComponent = jetpacks.FirstOrDefault(x => x != null && x.droppedItemContainer.net.ID == entity.net.ID);
            if (jetPackComponent == null) return;
            if (plugins.Exists("ZoneManager") && (bool)ZoneManager.Call("HasFlag", ZoneID, "eject"))
            {
                if (!noFallDamage.Contains(jetPackComponent.player.userID))
                {
                    noFallDamage.Add(jetPackComponent.player.userID);
                    timer.In(5f, () => noFallDamage.Remove(jetPackComponent.player.userID));
                }
                RemoveJetPack(jetPackComponent.player.userID);
            }
        }

        void OnSpaceEventStart()
        {
            SupportedPluginsController.spaceHeight = (float)Space.Call("GetMinSpaceAltitude");
            SupportedPluginsController.isSpaceEventActive = true;
        }

        void OnSpaceEventStop()
        {
            SupportedPluginsController.isSpaceEventActive = false;
            SupportedPluginsController.spaceHeight = 0;
        }
        #endregion SupportedPlugins
        #endregion Hooks

        #region Commands
        [ChatCommand("jet")]
        void JetCommandk(BasePlayer player, string command, string[] arg)
        {
            if (player == null) return;
            JetPackComponent jetPack = jetpacks.FirstOrDefault(x => x != null && x.GetPlayerUserId() == player.userID);
            if (jetPack != null && (jetPack.onGround || _config.airTakeOff || (SupportedPluginsController.isSpaceEventActive && SupportedPluginsController.IsPositionInSpace(player.transform.position)))) RemoveJetPack(player.userID);
            else if (jetPack == null) WearJetPack(player);
        }

        [ChatCommand("givejetpack")]
        void JetGiveCommandk(BasePlayer player, string command, string[] arg)
        {
            if (player == null) return;

            if (!permission.UserHasPermission(player.UserIDString, _config.giveSelfPermission))
            {
                PrintToChat(player, "You do not have permission to use this command!");
                return;
            }

            MoveItem(player, CreateItem());
            PrintToChat(player, GetMessage("GetJetpack", player.userID, ins._config.prefics));
        }

        [ConsoleCommand("givejetpack")]
        void GiveCustomItemCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (player == null)
            {
                if (arg.Args == null || arg.Args.Length < 1) return;
                BasePlayer target = BasePlayer.FindByID(Convert.ToUInt64(arg.Args[0]));
                if (target == null)
                {
                    Puts("Player not found");
                    return;
                }
                MoveItem(target, CreateItem());
                PrintToChat(target, GetMessage("GetJetpack", target.userID, ins._config.prefics));
                Puts($"A jetpack was given to {target.displayName}!");
            }
            else
            {
                BasePlayer target;
                if (arg.Args == null || arg.Args.Length == 0)
                {
                    if (!permission.UserHasPermission(player.UserIDString, _config.giveSelfPermission))
                    {
                        PrintToChat(player, "You do not have permission to use this command!");
                        return;
                    }
                    target = player;
                }
                else
                {
                    if (!permission.UserHasPermission(player.UserIDString, _config.givePermission))
                    {
                        PrintToChat(player, "You do not have permission to use this command!");
                        return;
                    }
                    target = BasePlayer.FindByID(Convert.ToUInt64(arg.Args[0]));
                }

                if (target == null)
                {
                    PrintToChat(player, "Player not found!");
                    return;
                }

                MoveItem(target, CreateItem());
                PrintToChat(target, GetMessage("GetJetpack", player.userID, ins._config.prefics));
                PrintToConsole(player, $"A jetpack was given to {target.displayName}");
            }
        }
        #endregion Commands

        #region Methods
        void UpdateConfig()
        {
            if (_config.version != Version.ToString())
            {
                VersionNumber versionNumber;
                var versionArray = _config.version.Split('.');
                versionNumber.Major = Convert.ToInt32(versionArray[0]);
                versionNumber.Minor = Convert.ToInt32(versionArray[1]);
                versionNumber.Patch = Convert.ToInt32(versionArray[2]);

                if (versionNumber.Minor == 0)
                {
                    if (versionNumber.Patch < 4)
                    {
                        PrintError("Delete the configuration file!");
                        NextTick(() => Server.Command($"o.unload {Name}"));
                        return;
                    }
                    _config.maxHeight = 1000;
                    _config.blockedCommands = new HashSet<string>
                    {
                        "home"
                    };
                    versionNumber = new VersionNumber(1, 1, 0);
                }
                if (versionNumber.Minor == 1)
                {

                }

                _config.version = Version.ToString();
                SaveConfig();
            }
        }

        void RegisterPermissions()
        {
            permission.RegisterPermission(_config.usePermission, this);
            permission.RegisterPermission(_config.givePermission, this);
            permission.RegisterPermission(_config.giveSelfPermission, this);
            permission.RegisterPermission(_config.fallPermission, this);
            permission.RegisterPermission(_config.fuelPermission, this);
        }

        void RemoveJetPack(ulong playerUserId)
        {
            JetPackComponent jetPack = jetpacks.FirstOrDefault(x => x != null && x.GetPlayerUserId() == playerUserId);
            if (jetPack == null) return;
            jetPack.RemoveJetPack();
            jetpacks.Remove(jetPack);
        }

        void WearJetPack(BasePlayer player, bool checkInv = true)
        {
            if (jetpacks.Any(x => x != null && x.GetPlayerUserId() == player.userID) || player.HasPlayerFlag(BasePlayer.PlayerFlags.Wounded) || (checkInv && !player.inventory.containerWear.itemList.Any(x => x.info.shortname == _config.itemSetting.shortname && x.skin == _config.itemSetting.skin)))
            {
                RemoveJetPack(player.userID);
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, _config.usePermission))
            {
                PrintToChat(player, GetMessage("NoPermission", player.userID, ins._config.prefics));
                return;
            }

            if (SupportedPluginsController.isSpaceEventActive && player.transform.position.y >= SupportedPluginsController.spaceHeight)
            {
                RaycastHit raycastHit;
                Physics.Raycast(player.eyes.transform.position, Vector3.up, out raycastHit, 10, 1 << 21);

                if (Physics.Raycast(player.eyes.transform.position, Vector3.up, out raycastHit, 10, 1 << 21) && Physics.Raycast(player.eyes.transform.position, -Vector3.up, out raycastHit, 10, 1 << 21) && !raycastHit.collider.name.Contains("floor.triangle.twig") && !raycastHit.collider.name.Contains("floor.frame"))
                {
                    PrintToChat(player, GetMessage("AtHome", player.userID, ins._config.prefics));
                    return;
                }
            }

            else if (Physics.Raycast(new Ray(player.transform.position + new Vector3(0, 2.1f, 0), Vector3.up), 2f) || Physics.Raycast(new Ray(player.transform.position + new Vector3(0, 1f, 0), Vector3.forward), 1f) || Physics.Raycast(new Ray(player.transform.position + new Vector3(0, 1f, 0), -Vector3.forward), 1f) || Physics.Raycast(new Ray(player.transform.position + new Vector3(0, 1f, 0), -Vector3.forward), 1f))
            {
                PrintToChat(player, GetMessage("AtHome", player.userID, ins._config.prefics));
                return;
            }
            Vector3 rotation = player.eyes.GetLookRotation().eulerAngles;

            if (SupportedPluginsController.isSpaceEventActive && player.transform.position.y > SupportedPluginsController.spaceHeight && player.GetMounted() != null && player.GetMounted().PrefabName == "assets/prefabs/vehicle/seats/testseat.prefab")//Fix for space plugin
            {
                player.GetMounted().Kill();
            }

            DroppedItemContainer droppedItemContainer = GameManager.server.CreateEntity("assets/prefabs/misc/item drop/item_drop_backpack.prefab", player.transform.position + new Vector3(0, 1.3f, 0), Quaternion.Euler(270, rotation.y, rotation.z)) as DroppedItemContainer;
            droppedItemContainer.enableSaving = false;
            MovableDroppedItemContainer newEntity = droppedItemContainer.gameObject.AddComponent<MovableDroppedItemContainer>();
            CopySerializableFields(droppedItemContainer, newEntity);
            droppedItemContainer.StopAllCoroutines();
            UnityEngine.GameObject.DestroyImmediate(droppedItemContainer, true);
            droppedItemContainer = newEntity;

            droppedItemContainer.SetFlag(BaseEntity.Flags.Open, true);
            droppedItemContainer.OwnerID = player.userID;
            droppedItemContainer.Spawn();
            droppedItemContainer.SendNetworkUpdate();
            JetPackComponent component = droppedItemContainer.gameObject.AddComponent<JetPackComponent>();
            Interface.CallHook("OnJetpackWear", new object[] { player });
            jetpacks.Add(component);
            component.Init(player);
            jetpacks.RemoveWhere(x => x == null);
        }

        bool PlayerHaveJetPackItem(BasePlayer player)
        {
            return player != null && player.inventory.containerWear.itemList.Any(x => x != null && IsJetPackItem(x));
        }

        bool IsJetPackItem(Item item)
        {
            return item != null && item.info.shortname == ins._config.itemSetting.shortname && item.skin == ins._config.itemSetting.skin;
        }

        static void CopySerializableFields<T>(T src, T dst)
        {
            FieldInfo[] srcFields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in srcFields)
            {
                object value = field.GetValue(src);
                field.SetValue(dst, value);
            }
        }

        void Unsubscribes()
        {
            Unsubscribe("OnLootSpawn");
            Unsubscribe("CanLootEntity");
            Unsubscribe("OnPlayerInput");
            if (!_config.samSite && !_config.samSiteStatic) Unsubscribe("OnSamSiteTargetScan");

            foreach (string hook in subscribeMetods)
            {
                Unsubscribe(hook);
            }
        }

        void Subscribes()
        {
            if (_config.inLoot) Subscribe("OnLootSpawn");
            if (!_config.allowLoot) Subscribe("CanLootEntity");
            if (!_config.control.buttonOn) Subscribe("OnPlayerInput");

            foreach (string hook in subscribeMetods)
            {
                Subscribe(hook);
            }
        }
        #region MoveItem
        void MoveItem(BasePlayer player, Item item)
        {
            int spaceCountItem = GetSpaceCountItem(player, item.info.shortname, item.MaxStackable(), item.skin);
            int inventoryItemCount;
            if (spaceCountItem > item.amount) inventoryItemCount = item.amount;
            else inventoryItemCount = spaceCountItem;

            if (inventoryItemCount > 0)
            {
                Item itemInventory = ItemManager.CreateByName(item.info.shortname, inventoryItemCount, item.skin);
                if (item.skin != 0) itemInventory.name = item.name;

                item.amount -= inventoryItemCount;
                MoveInventoryItem(player, itemInventory);
            }

            if (item.amount > 0) MoveOutItem(player, item);
        }

        int GetSpaceCountItem(BasePlayer player, string shortname, int stack, ulong skinID)
        {
            int slots = player.inventory.containerMain.capacity + player.inventory.containerBelt.capacity;
            int taken = player.inventory.containerMain.itemList.Count + player.inventory.containerBelt.itemList.Count;
            int result = (slots - taken) * stack;
            foreach (Item item in player.inventory.AllItems()) if (item.info.shortname == shortname && item.skin == skinID && item.amount < stack) result += stack - item.amount;
            return result;
        }

        void MoveInventoryItem(BasePlayer player, Item item)
        {
            if (item.amount <= item.MaxStackable())
            {
                foreach (Item itemInv in player.inventory.AllItems())
                {
                    if (itemInv.info.shortname == item.info.shortname && itemInv.skin == item.skin && itemInv.amount < itemInv.MaxStackable())
                    {
                        if (itemInv.amount + item.amount <= itemInv.MaxStackable())
                        {
                            itemInv.amount += item.amount;
                            itemInv.MarkDirty();
                            return;
                        }
                        else
                        {
                            item.amount -= itemInv.MaxStackable() - itemInv.amount;
                            itemInv.amount = itemInv.MaxStackable();
                        }
                    }
                }
                if (item.amount > 0) player.inventory.GiveItem(item);
            }
            else
            {
                while (item.amount > item.MaxStackable())
                {
                    Item thisItem = ItemManager.CreateByName(item.info.shortname, item.MaxStackable(), item.skin);
                    if (item.skin != 0) thisItem.name = item.name;
                    player.inventory.GiveItem(thisItem);
                    item.amount -= item.MaxStackable();
                }
                if (item.amount > 0) player.inventory.GiveItem(item);
            }
        }

        void MoveOutItem(BasePlayer player, Item item)
        {
            if (item.amount <= item.MaxStackable()) item.Drop(player.transform.position, Vector3.up);
            else
            {
                while (item.amount > item.MaxStackable())
                {
                    Item thisItem = ItemManager.CreateByName(item.info.shortname, item.MaxStackable(), item.skin);
                    if (item.skin != 0) thisItem.name = item.name;
                    thisItem.Drop(player.transform.position, Vector3.up);
                    item.amount -= item.MaxStackable();
                }
                if (item.amount > 0) item.Drop(player.transform.position, Vector3.up);
            }
        }

        Item CreateItem()
        {
            Item item = ItemManager.CreateByName(ins._config.itemSetting.shortname, 1, ins._config.itemSetting.skin);
            item.OnBroken();
            if (ins._config.itemSetting.name != "") item.name = ins._config.itemSetting.name;
            return item;
        }
        #endregion MoveItem
        #endregion Methods

        #region Classes
        class JetpackCheckPlayerInputComponent : FacepunchBehaviour
        {
            BasePlayer player;
            InputState inputState;
            static HashSet<JetpackCheckPlayerInputComponent> jetpackCheckPlayerInputs = new HashSet<JetpackCheckPlayerInputComponent>();

            internal static void TryAddComponentToPlayer(BasePlayer player)
            {
                if (player == null || PlayerHasInputClass(player.userID)) return;
                AddComponentToPlayer(player);
            }

            static bool PlayerHasInputClass(ulong userId)
            {
                return jetpackCheckPlayerInputs.Any(x => x != null && x.player.userID == userId);
            }

            static void AddComponentToPlayer(BasePlayer player)
            {
                JetpackCheckPlayerInputComponent jetpackCheckPlayerInput = player.gameObject.AddComponent<JetpackCheckPlayerInputComponent>();
                jetpackCheckPlayerInput.InitComponent(player);
                jetpackCheckPlayerInputs.Add(jetpackCheckPlayerInput);
            }

            internal static void TryRemoveComponentFromPlayer(ulong userID)
            {
                JetpackCheckPlayerInputComponent jetpackCheckPlayerInput = GetInputComponentFromUserId(userID);
                if (jetpackCheckPlayerInput != null) RemoveComponent(jetpackCheckPlayerInput);
            }

            static JetpackCheckPlayerInputComponent GetInputComponentFromUserId(ulong userId)
            {
                return jetpackCheckPlayerInputs.FirstOrDefault(x => x != null && x.player.userID == userId);
            }

            internal static void RemoveAllPlayerComponents()
            {
                foreach (JetpackCheckPlayerInputComponent jetpackCheckPlayerInput in jetpackCheckPlayerInputs)
                {
                    RemoveComponent(jetpackCheckPlayerInput);
                }
            }

            internal static void AddComponentsToAllPLayers()
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    if (player == null) continue;
                    if (ins.PlayerHaveJetPackItem(player)) TryAddComponentToPlayer(player);
                }
            }

            static void RemoveComponent(JetpackCheckPlayerInputComponent jetpackCheckPlayerInput)
            {
                if (jetpackCheckPlayerInput != null) UnityEngine.Object.Destroy(jetpackCheckPlayerInput);
            }

            void InitComponent(BasePlayer player)
            {
                this.player = player;
                inputState = player.serverInput;
            }

            void FixedUpdate()
            {
                if (inputState == null || player.IsSleeping() || player.IsDead()) RemoveComponent(this);
                else if (inputState.WasJustPressed(BUTTON.FIRE_THIRD))
                {
                    JetPackComponent jetPack = ins.jetpacks.FirstOrDefault(x => x != null && x.GetPlayerUserId() == player.userID);
                    if (jetPack != null)
                    {
                        if (jetPack.onGround || ins._config.airTakeOff || (SupportedPluginsController.isSpaceEventActive && SupportedPluginsController.IsPositionInSpace(player.transform.position))) ins.RemoveJetPack(player.userID);
                        else
                        {
                            foreach (Collider collider in UnityEngine.Physics.OverlapSphere(player.transform.position, 2f))
                            {
                                BaseEntity entity = collider.ToBaseEntity();
                                if (entity == null) continue;
                                if (entity is CargoShip) ins.RemoveJetPack(player.userID);
                            }
                        }
                    }
                    else ins.WearJetPack(player);
                }
            }
        }

        class JetPackComponent : BaseVehicle
        {
            internal BasePlayer player;
            internal DroppedItemContainer droppedItemContainer;
            Rigidbody rigidbody;
            BaseMountable chair;
            SphereCollider sphereCollider;
            Coroutine updateCorountine;
            public SAMTargetComponent samSiteComponent;

            List<BaseEntity> rockets = new List<BaseEntity>();
            List<BaseEntity> fires = new List<BaseEntity>();
            float lastSpeed = 0;

            bool haveFuel = true;
            bool up = false;
            bool forward = false;
            bool back = false;
            bool left = false;
            bool right = false;
            bool leftCren = false;
            bool rightCren = false;

            public bool onGround = false;
            public bool infinityFuel = false;
            public bool disableFall = false;

            internal void Init(BasePlayer player)
            {
                this.player = player;
                CheckPermissions();
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, true);
                droppedItemContainer = GetComponent<DroppedItemContainer>();


                CreateSphere();

                if (ins._config.fuel.fuel && !infinityFuel) CheckFuel(false);
                if (haveFuel)
                {
                    SpawnJetPack();
                    SeatPlayer();
                    rigidbody.centerOfMass = new Vector3(0, 0, -1);
                    rigidbody.mass = 100;
                    if (ins._config.fuel.fuel && !infinityFuel) InvokeRepeating(() => CheckFuel(true), 0, ins._config.fuel.periodFuel);
                    if (ins._config.thirdPersonView) player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, true);
                    if (ins._config.lowVolume && ins._config.sound) InvokeRepeating(Sound, 0, 0.2f);
                    if (ins._config.samSite || ins._config.samSiteStatic) samSiteComponent = gameObject.AddComponent<SAMTargetComponent>();
                }
                else
                {
                    ins.RemoveJetPack(player.userID);
                }
                updateCorountine = ServerMgr.Instance.StartCoroutine(UpdateCorountine());
            }

            void OnDestroy()
            {
                CancelInvoke(() => CheckFuel(true));
                CancelInvoke(Sound);
                if (updateCorountine != null) ServerMgr.Instance.StopCoroutine(updateCorountine);
                if (player != null) player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, false);
                Interface.CallHook("OnJetpackRemoved", new object[] { player });
            }

            internal void RemoveJetPack()
            {
                droppedItemContainer.Kill();
            }

            internal ulong GetPlayerUserId()
            {
                return player.userID;
            }

            #region Controller
            IEnumerator UpdateCorountine()
            {
                while (true)
                {
                    if (!CheckPasanger())
                    {
                        ins.RemoveJetPack(player.userID);
                        break;
                    }

                    float speed = rigidbody.velocity.magnitude;
                    CheckPlayerInput();
                    CheckFall(speed);
                    CheckGround(speed);
                    CheckWater();
                    ForceControl();
                    DirectionControl();
                    SpacePluginController();
                    lastSpeed = speed;
                    yield return CoroutineEx.waitForSeconds(0.2f);
                }
            }

            bool CheckPasanger()
            {
                if (chair._mounted == null || player.IsSleeping() || player.userID != chair._mounted.userID || player.GetMounted() == null) return false;
                return true;
            }

            void CheckFall(float currentSpeed)
            {
                float deltaSpeed = Math.Abs(rigidbody.velocity.magnitude - lastSpeed);
                if (ins._config.fallDamage && !disableFall && deltaSpeed > ins._config.fallDamagePoint * 2) player.Hurt((deltaSpeed - ins._config.fallDamagePoint) * ins._config.fallDamageMultiplicator, DamageType.Fall, null, false);
            }

            void CheckGround(float currentSpeed)
            {
                if (currentSpeed > 2.5f) rigidbody.freezeRotation = true;
                else rigidbody.freezeRotation = false;
                if (currentSpeed <= 0.1f) onGround = true;
                else onGround = false;
            }

            void CheckWater()
            {
                if (ins._config.waterOff && transform.position.y <= 0) ins.RemoveJetPack(player.userID);
            }

            void ForceControl()
            {
                if (!haveFuel) return;
                if (transform.position.y < ins._config.maxHeight)
                {
                    if (player.serverInput.IsDown(BUTTON.JUMP))
                    {
                        if (ins._config.sound && !ins._config.lowVolume) Effect.server.Run("assets/bundled/prefabs/fx/impacts/blunt/snow/snow.prefab", player.transform.position);
                        rigidbody.AddForce(transform.forward * ins._config.control.force * 300, ForceMode.Force);
                        Fire(true);
                    }
                    else Fire(false);
                }
                else
                {
                    rigidbody.AddForce(Vector3.down * ins._config.control.force * 150, ForceMode.Force);
                    Fire(false);
                }
            }

            void DirectionControl()
            {
                if (forward) transform.RotateAround(transform.position, transform.right, ins._config.control.upDown * 3);

                if (back) transform.RotateAround(transform.position, transform.right, -ins._config.control.upDown * 3);

                if (right) transform.RotateAround(transform.position, Vector3.up, ins._config.control.yaw * 3);
                else if (left) transform.RotateAround(transform.position, Vector3.up, -ins._config.control.yaw * 3);

                if (rightCren) transform.RotateAround(transform.position, transform.up, ins._config.control.cren * 3);
                else if (leftCren) transform.RotateAround(transform.position, transform.up, -ins._config.control.cren * 3);
            }

            void Sound()
            {
                if (up && transform.position.y < ins._config.maxHeight) Effect.server.Run("assets/bundled/prefabs/fx/impacts/blunt/sand/sand.prefab", player.transform.position);
            }

            internal void CheckFuel(bool take)
            {
                Item item = player.inventory.containerBelt.itemList.FirstOrDefault(x => x != null && x.info.shortname == ins._config.fuel.itemShortname);

                if (haveFuel)
                {
                    if (item != null && item.amount >= ins._config.fuel.fuelCount)
                    {
                        if (!up) return;
                        if (item.amount <= 1) item.RemoveFromContainer();
                        else
                        {
                            item.amount = item.amount - ins._config.fuel.fuelCount;
                            item.MarkDirty();
                        }
                    }
                    else
                    {
                        ins.PrintToChat(player, ins.GetMessage("NoFuel", player.userID, ins._config.prefics));
                        haveFuel = false;
                        Fire(false);
                    }
                }
                else if (item != null)
                {
                    if (up && take && item.amount >= ins._config.fuel.fuelCount)
                    {
                        if (item.amount <= 1) item.RemoveFromContainer();
                        else
                        {
                            item.amount = item.amount - ins._config.fuel.fuelCount;
                            item.MarkDirty();
                        }
                    }
                    haveFuel = true;
                }
            }

            void Fire(bool on)
            {
                if (!on)
                {
                    if (fires.Count == 0) return;
                    foreach (BaseEntity entity in fires) entity.Kill();
                    fires.Clear();
                }

                else if (fires.Count == 0)
                {
                    foreach (Position transform in ins.firesCoord)
                    {
                        BaseEntity entity = CreateEntity(transform.Name, transform.position, transform.rotation);
                        fires.Add(entity);
                        entity.SetFlag(BaseEntity.Flags.Reserved8, true);
                    }
                }
            }

            void CheckPlayerInput()
            {
                if (player.serverInput.IsDown(BUTTON.JUMP)) up = true;
                else up = false;

                if (player.serverInput.IsDown(BUTTON.SPRINT)) leftCren = true;
                else leftCren = false;

                if (player.serverInput.IsDown(BUTTON.DUCK)) rightCren = true;
                else rightCren = false;

                if (player.serverInput.IsDown(BUTTON.FORWARD)) forward = true;
                else forward = false;

                if (player.serverInput.IsDown(BUTTON.BACKWARD)) back = true;
                else back = false;

                if (player.serverInput.IsDown(BUTTON.RIGHT)) right = true;
                else right = false;

                if (player.serverInput.IsDown(BUTTON.LEFT)) left = true;
                else left = false;
            }

            void SpacePluginController()
            {
                if (SupportedPluginsController.isSpaceEventActive)
                {
                    if (SupportedPluginsController.IsPositionInSpace(player.transform.position))
                    {
                        if (rigidbody.useGravity)
                        {
                            rigidbody.useGravity = false;
                        }
                    }
                    else if (!rigidbody.useGravity) rigidbody.useGravity = true;
                }
                else if (!rigidbody.useGravity) rigidbody.useGravity = true;
            }
            #endregion Controller

            #region BuidJetPack
            public void CreateSphere()
            {
                sphereCollider = droppedItemContainer.gameObject.AddComponent<SphereCollider>();
                sphereCollider.gameObject.layer = 12;
                sphereCollider.radius = 1.4f;
            }

            public void CheckPermissions()
            {
                if (ins.permission.UserHasPermission(player.UserIDString, ins._config.fuelPermission)) infinityFuel = true;
                if (ins.permission.UserHasPermission(player.UserIDString, ins._config.fallPermission)) disableFall = true;
            }

            public void SpawnJetPack()
            {
                rigidbody = gameObject.GetComponent<Rigidbody>();
                rigidbody.drag = ins._config.control.drag;

                SpawnChair();


                foreach (Position transform in ins.rocketsCoord) rockets.Add(CreateEntity(transform.Name, transform.position, transform.rotation));
            }

            public void SpawnChair()
            {
                chair = GameManager.server.CreateEntity(ins._config.allowWeapon ? "assets/prefabs/vehicle/seats/testseat.prefab" : "assets/prefabs/vehicle/seats/standingdriver.prefab") as BaseMountable;
                chair.enableSaving = false;
                chair.SetParent(droppedItemContainer, true, false);
                chair.transform.localPosition = new Vector3(0, -0.1f, -1.18f);
                chair.transform.localEulerAngles = new Vector3(90, 0, 0);
                chair.Spawn();
            }

            void SeatPlayer()
            {
                player.StopWounded();
                player.StopSpectating();
                chair.MountPlayer(player);
                player.SendNetworkUpdate();
            }

            public BaseEntity CreateEntity(string name, Vector3 position, Vector3 rotation)
            {
                BaseEntity entity = GameManager.server.CreateEntity(name);
                entity.enableSaving = false;
                entity.SetParent(droppedItemContainer, true, false);
                entity.transform.localPosition = position;
                entity.transform.localEulerAngles = rotation;
                entity.SetFlag(BaseEntity.Flags.Reserved8, true);
                entity.Spawn();
                return entity;
            }
            #endregion BuidJetPack
        }

        private class SAMTargetComponent : FacepunchBehaviour, SamSite.ISamSiteTarget
        {
            BaseEntity baseEntity;
            Rigidbody rigidbody;

            private void Awake()
            {
                baseEntity = GetComponent<BaseEntity>();
                rigidbody = GetComponent<Rigidbody>();
            }

            public bool IsValidSAMTarget() => true;

            public Vector3 Position => baseEntity.transform.position;

            public SamSite.SamTargetType SAMTargetType => ins._config.samSitePlus ? SamSite.targetTypeMissile : SamSite.targetTypeVehicle;

            public bool isClient => false;

            public bool IsValidSAMTarget(bool isStaticSamSite) => true;

            public Vector3 CenterPoint() => transform.position;

            public Vector3 GetWorldVelocity()
            {
                return rigidbody.velocity;
            }

            public bool IsVisible(Vector3 position, float distance) => baseEntity.IsVisible(position, distance);
        }

        class MovableDroppedItemContainer : DroppedItemContainer
        {
            public override float MaxVelocity()
            {
                return 100;
            }
        }

        static class SupportedPluginsController
        {
            internal static bool isSpaceEventActive = false;
            internal static float spaceHeight = 0;

            internal static bool IsPositionInSpace(Vector3 position)
            {
                return isSpaceEventActive && spaceHeight > 0 && position.y > spaceHeight;
            }
        }

        class Position
        {
            public string Name;
            public Vector3 position;
            public Vector3 rotation;
        }

        List<Position> rocketsCoord = new List<Position>()
        {
            new Position {Name = "assets/prefabs/weapons/rocketlauncher/rocket_launcher.entity.prefab", position = new Vector3(- 0.394f, 0.015f, -0.238f), rotation = new Vector3(6.547f, 263.840f, 306.934f)},
            new Position {Name = "assets/prefabs/weapons/rocketlauncher/rocket_launcher.entity.prefab", position = new Vector3(0.394f, 0.195f, -0.238f), rotation = new Vector3(353.453f, 96.160f, 126.934f)},
        };

        List<Position> firesCoord = new List<Position>()
        {
            new Position {Name = "assets/prefabs/weapons/flamethrower/flamethrower.entity.prefab", position = new Vector3(-0.147f, 0.119f, 0.224f), rotation = new Vector3(344.522f, 76.532f, 182.924f)},
            new Position {Name = "assets/prefabs/weapons/flamethrower/flamethrower.entity.prefab", position = new Vector3(0.157f, 0.119f, 0.224f), rotation = new Vector3(352.666f, 283.863f, 357.569f)},
        };
        #endregion Classes

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GetJetpack"] = "{0} You <color=#738d43>got</color> a jetpack!",
                ["NoPermission"] = "{0} You <color=#b03b1e>do not have permission</color> to use a jetpack!",
                ["AtHome"] = "{0} You <color=#b03b1e>cannot</color> use a jetpack indoors!",
                ["NoFuel"] = "{0} To use the jetpack, put fuel in the <color=#738d43>quick slots</color>!",
                ["CommandBlock"] = "{0} This command is <color=#b03b1e>not allowed</color> to be used with a jetpack!"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GetJetpack"] = "{0} Вы <color=#738d43>получили</color> реактивный ранец!",
                ["NoPermission"] = "{0} У вас <color=#b03b1e>нет разрешения</color> использовать реактивный ранец!",
                ["AtHome"] = "{0} <color=#b03b1e>Запрещено</color> использовать реактивный ранец в помещении!",
                ["NoFuel"] = "{0} Для использования реактивного ранца положите топливо в <color=#2257b3>быстрые слоты</color>!",
                ["CommandBlock"] = "{0} Эту команду <color=#b03b1e>запрещено</color> использовать на джетпаке!"
            }, this, "ru");
        }

        string GetMessage(string langKey, ulong UID) => lang.GetMessage(langKey, this, UID.ToString());

        string GetMessage(string langKey, ulong UID, params object[] args) => (args.Length == 0) ? GetMessage(langKey, UID) : string.Format(GetMessage(langKey, UID), args);
        #endregion Lang

        #region Config  

        private PluginConfig _config;

        protected override void LoadDefaultConfig() => _config = PluginConfig.DefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(_config, true);
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        public class ItemSetting
        {
            [JsonProperty(en ? "Shortname" : "Shortname")] public string shortname;
            [JsonProperty(en ? "Skin" : "Skin")] public ulong skin;
            [JsonProperty(en ? "Name" : "Name")] public string name;
        }

        public class FuelSetting
        {
            [JsonProperty(en ? "Use fuel" : "Использовать топливо?")] public bool fuel;
            [JsonProperty(en ? "Fuel period" : "Период забора топлива")] public float periodFuel;
            [JsonProperty(en ? "Fuel consumption" : "Потребление топлива за период")] public int fuelCount;
            [JsonProperty(en ? "Item" : "Предмет")] public string itemShortname;
        }

        public class Control
        {
            [JsonProperty(en ? "Wear/taking off a jetpack with the middle mouse button?" : "Надевать/снимать ранец средней кнопкой мыши")] public bool buttonOn;
            [JsonProperty(en ? "Air resistance" : "Сопротивление воздуха")] public float drag;
            [JsonProperty(en ? "Thrust" : "Тяга")] public float force;
            [JsonProperty(en ? "Pitch" : "Тангаж")] public float upDown;
            [JsonProperty(en ? "Roll" : "Крен")] public float cren;
            [JsonProperty(en ? "Yaw" : "Рыскание")] public float yaw;
        }

        public class CrateConfig
        {
            [JsonProperty(en ? "Prefab" : "Префаб ящика")] public string Prefab;
            [JsonProperty(en ? "Chance" : "Шанс")] public float Chance;
        }

        private class PluginConfig
        {
            [JsonProperty(en ? "Version" : "Версия")] public string version;
            [JsonProperty(en ? "Prefix of chat messages" : "Префикс в чате")] public string prefics;
            [JsonProperty(en ? "Permission to use" : "Разрешение для использования")] public string usePermission;
            [JsonProperty(en ? "Permission to give to other players" : "Разрешение для выдачи другим игрокам")] public string givePermission;
            [JsonProperty(en ? "Permission to give to yourself" : "Разрешение для выдачи себе")] public string giveSelfPermission;
            [JsonProperty(en ? "Permission to turn off fuel" : "Разрешение для отключения топлива")] public string fuelPermission;
            [JsonProperty(en ? "Permission to disable collision damage" : "Разрешение для отключения столкновений")] public string fallPermission;
            [JsonProperty(en ? "List of commands that are prohibited while using the jetpack" : "Список команд, которые запрещены во время использования джетпака")] public HashSet<string> blockedCommands;
            [JsonProperty(en ? "Allow the use of weapons on the jetpack?" : "Разрешить использование оружия на джетпаке?")] public bool allowWeapon;
            [JsonProperty(en ? "Take off a jetpack in the water?" : "Снимать джетпак при попадании в воду")] public bool waterOff;
            [JsonProperty(en ? "Allow to take off the jetpack in the air" : "Разрешить снимать джетпак в воздухе")] public bool airTakeOff;
            [JsonProperty(en ? "Third-person view" : "Вид от третьего лица")] public bool thirdPersonView;
            [JsonProperty(en ? "Turn on the sound?" : "Включить звук при полете")] public bool sound;
            [JsonProperty(en ? "Use quiet sound" : "Уменьшить громкость звука")] public bool lowVolume;
            [JsonProperty(en ? "Enable collision damage" : "Включить урон от столкновений")] public bool fallDamage;
            [JsonProperty(en ? "The SamSite will attack the jetpack" : "ПВО будет атаковать джетпак")] public bool samSite;
            [JsonProperty(en ? "The SamSite on the monuments will attack the jetpack" : "ПВО на рт будет атаковать джетпак")] public bool samSiteStatic;
            [JsonProperty(en ? "Increased missile speed and SamSite attack radius by jetpack" : "Увеличеные скорость ракет и радиус атаки ПВО по джетпаку")] public bool samSitePlus;
            [JsonProperty(en ? "Allow loot supply drop when using a jetpack" : "Разрешить лутать аирдроп при использовании джетпака")] public bool allowLoot;
            [JsonProperty(en ? "Use the Zone Manager plugin?" : "Использовать плагин ZoneManager?")] public bool zoneManager;
            [JsonProperty(en ? "Collision Damage Multiplier" : "Множитель урона от столкновений")] public float fallDamageMultiplicator;
            [JsonProperty(en ? "Collision acceleration threshold" : "Порог урона от столкновений")] public float fallDamagePoint;
            [JsonProperty(en ? "Maximum flight altitude" : "Максимальная высота полета")] public float maxHeight;
            [JsonProperty(en ? "Control" : "Управление")] public Control control;
            [JsonProperty(en ? "Fuel" : "Топливо")] public FuelSetting fuel;
            [JsonProperty(en ? "Item" : "Предмет")] public ItemSetting itemSetting;
            [JsonProperty(en ? "Enable spawn in crates" : "Включить спавн в ящиках")] public bool inLoot;
            [JsonProperty(en ? "Spawn setting" : "Настройка спавна в ящиках")] public List<CrateConfig> crates;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    version = "1.1.3",
                    prefics = "[JetPack]",
                    usePermission = "jetpack.use",
                    givePermission = "jetpack.give",
                    giveSelfPermission = "jetpack.giveself",
                    fuelPermission = "jetpack.fuel",
                    fallPermission = "jetpack.fall",
                    blockedCommands = new HashSet<string>
                    {
                        "home"
                    },
                    allowWeapon = true,
                    waterOff = true,
                    airTakeOff = false,
                    thirdPersonView = false,
                    sound = true,
                    lowVolume = false,
                    fallDamage = true,
                    samSite = true,
                    samSitePlus = true,
                    zoneManager = false,
                    fallDamageMultiplicator = 5f,
                    fallDamagePoint = 5f,
                    maxHeight = 1000,
                    control = new Control
                    {
                        buttonOn = true,
                        force = 25f,
                        cren = 1.5f,
                        upDown = 1.5f,
                        yaw = 1.5f,
                        drag = 0.7f
                    },
                    fuel = new FuelSetting
                    {
                        fuel = true,
                        periodFuel = 1,
                        fuelCount = 1,
                        itemShortname = "lowgradefuel",

                    },
                    itemSetting = new ItemSetting
                    {
                        shortname = "burlap.gloves.new",
                        skin = 2632956407,
                        name = "JetPack"
                    },
                    inLoot = false,
                    crates = new List<CrateConfig>
                    {
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_elite.prefab",
                            Chance = 5f
                        }
                    }

                };
            }
        }
        #endregion Config
    }
}

namespace Oxide.Plugins.JetPackExtensionMethods
{
    public static class ExtensionMethods
    {
        public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return true;
            return false;
        }

        public static TSource FirstOrDefault<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return enumerator.Current;
            return default(TSource);
        }

        public static List<TSource> ToList<TSource>(this IEnumerable<TSource> source)
        {
            List<TSource> result = new List<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(enumerator.Current);
            return result;
        }

        public static TSource ElementAt<TSource>(this IEnumerable<TSource> source, int index)
        {
            int movements = 0;
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (movements == index) return enumerator.Current;
                    movements++;
                }
            }
            return default(TSource);
        }
    }
}