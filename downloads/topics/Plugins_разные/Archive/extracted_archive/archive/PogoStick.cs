using Enumerable = System.Linq.Enumerable;
using System.Collections.Generic;
using System.Reflection;
using System;
using Oxide.Plugins.PogoStickExtensionMethods;
using Oxide.Core.Plugins; 
using Oxide.Core;
using CompanionServer.Handlers;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    /*ПЛАГИН БЫЛ ПОФИКШЕН С ПОМОЩЬЮ ПРОГРАММЫ СКАЧАНОЙ С https://discord.gg/dNGbxafuJn */ [Info("PogoStick", "https://discord.gg/dNGbxafuJn", "1.0.0")]
    class PogoStick : RustPlugin
    {
        #region Variables
        const bool en = false;
        static PogoStick ins;
        [PluginReference] Plugin GUIAnnouncements, ZoneManager;
        HashSet<string> allHooks = new HashSet<string>
        {
            "OnActiveItemChanged",
            "CanWearItem",
            "OnLootSpawn",
            "OnEntityEnterZone"
        };
        #endregion Variables

        #region Hooks
        void Init() => Unsubscribes();

        void OnServerInitialized()
        {
            ins = this;
            UpdateConfig();
            Subscribes();
            RegisterPermissions();
        }

        void Unload()
        {
            PogoStickEntity.DestroyAllPogoSticks();
        }

        void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (!player.IsRealPlayer() || newItem == null) return;

            PogoConfig pogoConfig = _config.pogoConfigs.FirstOrDefault(x => x.itemConfig.shortname == newItem.info.shortname && x.itemConfig.skin == newItem.skin);
            if (pogoConfig != null)
            {
                if (!_config.mainConfig.allowInAir && player.isInAir) return;
                if (player.isMounted) return;
                PogoStickEntity.TryAttachPogoToPlayer(player, pogoConfig, newItem);
            }
        }

        object CanWearItem(PlayerInventory inventory, Item item)
        {
            if (inventory == null || item == null) return null;
            BasePlayer player = inventory.baseEntity;
            if (!player.IsRealPlayer()) return null;
            PogoConfig pogoConfig = _config.pogoConfigs.FirstOrDefault(x => x.itemConfig.shortname == item.info.shortname && x.itemConfig.skin == item.skin);
            if (pogoConfig != null)
            {
                if (!_config.mainConfig.allowInAir && player.isInAir) return true;
                if (player.isMounted) return true;
                PogoStickEntity.TryAttachPogoToPlayer(player, pogoConfig, item);
            }
            return null;
        }

        void OnLootSpawn(LootContainer container)
        {
            if (container == null || container.inventory == null) return;
            HashSet<PogoConfig> pogoConfigs = _config.pogoConfigs.Where(x => x.itemConfig.inLootSpawn && x.itemConfig.crateChanses.ContainsKey(container.PrefabName));
            int spawned = 0;
            foreach(PogoConfig pogoConfig in pogoConfigs)
            {
                LootManager.TrySpawnItemInDefaultCrate(container, pogoConfig.itemConfig, pogoConfig.itemConfig.crateChanses[container.PrefabName], spawned++);
            }
        }

        void OnEntityEnterZone(string ZoneID, MovableDroppedItem entity)
        {
            if (!_config.supportedPluginsConfig.zoneManager.enable) return;
            if (entity == null || entity.net == null) return;
            PogoStickEntity pogoStickEntity = PogoStickEntity.GetPogoByPlayerDroppedItemNetId(entity.net.ID.Value);
            if (pogoStickEntity != null)
            {
                if (!SupportedPluginsManager.CheckZoneOfZoneManager(ZoneID))
                {
                    pogoStickEntity.ForceDismount();
                }
            }
        }
        #endregion Hooks

        #region Commands
        [ChatCommand("givepogo")]
        void ChatGiveCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin) return;
            if (arg.Length == 0) return;
            PogoConfig pogoConfig = _config.pogoConfigs.FirstOrDefault(x => x.presetName == arg[0]);
            if (pogoConfig != null)
            {
                LootManager.GiveItemToPLayer(player, pogoConfig.itemConfig, 1);
                NotifyManager.SendMessageToPlayer(player, "GetPogoStick", ins._config.prefix);
            }    
        }

        [ConsoleCommand("givepogo")]
        void ModuleGiveConsoleCommande(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.Player() != null || arg.Args.Length < 2) return;
            ulong userId = Convert.ToUInt64(arg.Args[0]);
            BasePlayer target = BasePlayer.FindByID(userId);

            if (target == null)
            {
                PrintError("Player not found!");
                return;
            }
            string pogoPresetName = arg.Args[1];
            int amount = 1;
            if (arg.Args.Length > 2)
            {
                amount = Convert.ToInt32(arg.Args[2]);
                if (amount <= 0) return;
            }

            PogoConfig pogoConfig = _config.pogoConfigs.FirstOrDefault(x => x.presetName == pogoPresetName);
            if (pogoConfig == null) return;

            LootManager.GiveItemToPLayer(target, pogoConfig.itemConfig, amount);
            NotifyManager.SendMessageToPlayer(target, "GetPogoStick", ins._config.prefix);
            Puts($"{pogoConfig.itemConfig.name} was given to the {target.displayName}"); ;
        }
        #endregion Commands

        #region Methods
        void Unsubscribes()
        {
            foreach (string hook in allHooks) Unsubscribe(hook);
        }

        void Subscribes()
        {
            foreach (string hook in allHooks)
            {
                if (_config.mainConfig.itemType == 0 && (hook == "CanWearItem")) continue;
                if (_config.mainConfig.itemType == 1 && (hook == "OnActiveItemChanged")) continue;
                if (hook == "OnLootSpawn" && !_config.pogoConfigs.Any(x => x.itemConfig.inLootSpawn)) continue;
                if (hook == "OnEntityEnterZone" && !_config.supportedPluginsConfig.zoneManager.enable) continue;
                Subscribe(hook);
            }
        }

        void UpdateConfig()
        {
            //LoadDefaultConfig();
            SaveConfig();
        }

        void RegisterPermissions()
        {
            foreach (PogoConfig pogoConfig in _config.pogoConfigs)
            {
                if (pogoConfig.permission != "") permission.RegisterPermission(pogoConfig.permission, this);
            }
        }
        #endregion Methods

        #region Classes
        sealed class PogoStickEntity : FacepunchBehaviour
        {
            static HashSet<PogoStickEntity> pogoSticks = new HashSet<PogoStickEntity>();
            BasePlayer player;
            Item item;
            PogoConfig pogoConfig;
            Rigidbody rigidbody;
            MovableDroppedItem movableDroppedItem;
            MovableBaseMountable movableBaseMountable;
            SphereCollider sphereCollider;
            TimeSince timeSinceLastJump = 0f;
            TimeSince timeSinceCSpawn= 0f;
            bool isGroinded = true;
            bool wantDismount = false;
            bool forceDismount = false;

            internal static void TryAttachPogoToPlayer(BasePlayer player, PogoConfig pogoConfig, Item item)
            {
                if (GetPogoByPlayerUserId(player.userID) != null) return;
                if (pogoConfig.permission != "" && !CheckPlayerPermission(player, pogoConfig.permission)) return;
                if (Physics.Raycast(new Ray(player.transform.position + new Vector3(0f, 0.5f, 0), Vector3.up), 2f) || (ins._config.supportedPluginsConfig.zoneManager.enable && !SupportedPluginsManager.CheckAllPlayerZone(player)))
                {
                    NotifyManager.SendMessageToPlayer(player, "AreaBlock", ins._config.prefix);
                    return;
                }

                MovableDroppedItem movableDroppedItem = MovableDroppedItem.CreateMovableDroppedItem(player.transform.position + new Vector3(0, 0.45f, -0.035f), Quaternion.Euler(0, player.eyes.GetLookRotation().eulerAngles.y, 0));
                PogoStickEntity pogoStickEntity = movableDroppedItem.gameObject.AddComponent<PogoStickEntity>();
                pogoStickEntity.Init(player, movableDroppedItem, pogoConfig, item);
                pogoSticks.Add(pogoStickEntity);
            }

            internal static bool CheckPlayerPermission(BasePlayer player, string permission)
            {
                if (!ins.permission.UserHasPermission(player.UserIDString, permission))
                {
                    NotifyManager.SendMessageToPlayer(player, "NoPermission", ins._config.prefix);
                    return false;
                }
                return true;
            }

            internal static void DestroyAllPogoSticks()
            {
                foreach (PogoStickEntity pogoStickEntity in pogoSticks) if (pogoStickEntity != null) pogoStickEntity.DestroyPogoImmediately();
            }

            internal static PogoStickEntity GetPogoByPlayerUserId(ulong userId)
            {
                pogoSticks.RemoveWhere(x => x == null);
                return pogoSticks.FirstOrDefault(x => x != null && x.player != null && x.player.userID == userId);
            }

            internal static PogoStickEntity GetPogoByPlayerDroppedItemNetId(ulong netID)
            {
                pogoSticks.RemoveWhere(x => x == null);
                return pogoSticks.FirstOrDefault(x => x != null && x.movableDroppedItem != null && x.movableDroppedItem.net != null && x.movableDroppedItem.net.ID.Value == netID);
            }

            internal bool CanPlayerDismount()
            {
                if (!isGroinded) return false;
                if (Vector3.Angle(Vector3.up, movableDroppedItem.transform.up) > 45) return false;
                TimeSince timeNow = 0f;
                if (timeSinceCSpawn - timeNow < 1f) return false;
                return true;
            }

            internal void ForceDismount()
            {
                if (player != null) NotifyManager.SendMessageToPlayer(player, "AreaBlock", ins._config.prefix);
                forceDismount = true;
            }

            void Init(BasePlayer player, MovableDroppedItem movableDroppedItem, PogoConfig pogoConfig, Item item)
            {
                this.movableDroppedItem = movableDroppedItem;
                this.player = player;
                this.pogoConfig = pogoConfig;
                this.item = item;
                BuildPogo();
                GetAndUpdateRigidBody();
                movableBaseMountable.MountPlayer(player);
                if (ins._config.mainConfig.thirdPersonViewMode) SwitchPlayerViewMode(true);
            }

            void BuildPogo()
            {
                movableBaseMountable = MovableBaseMountable.CreateMovableBaseMountable(movableDroppedItem, "assets/prefabs/vehicle/seats/testseat.prefab", new Vector3(0, -0.015f, 0.035f), new Vector3(0f, -15, 0));
                CreateCollider();
                SpawnDecorEntities();
            }

            void SpawnDecorEntities()
            {
                foreach (EntityLocation entityLocation in EntityLocation.entityLocations)
                {
                    BaseEntity entity = GameManager.server.CreateEntity(entityLocation.prefabName, movableDroppedItem.transform.position);
                    entity.enableSaving = false;
                    entity.SetParent(movableDroppedItem, true, false);
                    entity.transform.localPosition = entityLocation.position + EntityLocation.delta;
                    entity.transform.localEulerAngles = entityLocation.rotation;
                    entity.SetFlag(BaseEntity.Flags.Reserved8, true);
                    entity.Spawn();
                }
            }

            void CreateCollider()
            {
                CapsuleCollider capsuleCollider = movableDroppedItem.gameObject.AddComponent<CapsuleCollider>();
                capsuleCollider.gameObject.layer = 12;
                capsuleCollider.radius = 0.55f;
                capsuleCollider.center = new Vector3(0, 1.1f, 0.31f);
                capsuleCollider.height = 1.8f;
                capsuleCollider.material.staticFriction = 1;
                capsuleCollider.material.dynamicFriction = 1;
                capsuleCollider.material.frictionCombine = PhysicMaterialCombine.Maximum;
                capsuleCollider.material.name = "1";

                sphereCollider = movableDroppedItem.gameObject.AddComponent<SphereCollider>();
                sphereCollider.gameObject.layer = 12;
                sphereCollider.radius = 0.15f;
                sphereCollider.center = new Vector3(0, -0.125f, 0.31f);
                sphereCollider.material.frictionCombine = PhysicMaterialCombine.Maximum;
                sphereCollider.material.staticFriction = 1;
                sphereCollider.material.dynamicFriction = 1;
            }

            void GetAndUpdateRigidBody()
            {
                rigidbody = movableDroppedItem.GetComponent<Rigidbody>();
                rigidbody.mass = 100;
                rigidbody.centerOfMass = new Vector3(0, -1.25f, 0.25f);
                rigidbody.angularDrag = 2f;
                rigidbody.drag = pogoConfig.drag;
                rigidbody.maxAngularVelocity = pogoConfig.rotationButtonSpeedScale > pogoConfig.tiltSpeedScale ? 1.5f * pogoConfig.rotationButtonSpeedScale : 1.5f * pogoConfig.tiltSpeedScale;
            }

            void SwitchPlayerViewMode(bool enable)
            {
                if (player.IsAdmin) return;
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, enable);
            }

            void OnCollisionExit(Collision collision)
            {
                if (collision == null || collision.collider == null) return;
                isGroinded = false;
            }

            void OnCollisionStay(Collision collision)
            {
                isGroinded = true;
            }

            void OnCollisionEnter(Collision collision)
            {
                if (collision == null || collision.collider == null) return;

                TimeSince timeNow = 0f;
                if (timeSinceLastJump - timeNow < 0.5f) return;

                float angle = Vector3.Angle(collision.relativeVelocity, movableDroppedItem.transform.up);
                if (angle < 120)
                {
                    BouncePogo(collision, angle);
                }
                else if (ins._config.mainConfig.minColisionSpeed < collision.relativeVelocity.magnitude)
                {
                    DoCollisionDamageToPlayer(collision.relativeVelocity.magnitude);
                }
                LossDurability();
            }

            void BouncePogo(Collision collision, float angle)
            {
                float bouncityScale = GetBouncityScale(collision, angle);
                Vector3 force = collision.relativeVelocity.magnitude * movableDroppedItem.transform.up;

                if (player.serverInput.IsDown(BUTTON.JUMP))
                {
                    force *= pogoConfig.bouncity;
                }
                if (force.magnitude > pogoConfig.maxSpeed * bouncityScale)
                {
                    force = force.normalized * pogoConfig.maxSpeed;
                }
                rigidbody.velocity = Vector3.zero;
                if (force.magnitude < 5) return;
                rigidbody.AddForce(force * bouncityScale, ForceMode.VelocityChange);
                Effect.server.Run("assets/bundled/prefabs/fx/impacts/jump-land/hide/gravel/jump-land-concrete.prefab", sphereCollider.transform.position);
                timeSinceLastJump = 0;
            }

            float GetBouncityScale(Collision collision, float collisionAngle)
            {
                float bouncityScale = 1;
                PhysicMaterial physicMaterial = collision.collider.GetMaterialAt(player.transform.position);
                if (physicMaterial != null)
                {
                    bouncityScale = ins._config.mainConfig.topologies.TryGetValue(physicMaterial.name, out bouncityScale) ? bouncityScale : 1;
                }

                float angelScale = 1;
                if (collisionAngle > 90)
                {
                    angelScale = -(80 - collisionAngle) / 10;
                    angelScale = 1 / angelScale;
                }

                return angelScale * bouncityScale;
            }

            void LossDurability()
            {
                item.LoseCondition(pogoConfig.lossOfDurability);
            }

            void DoCollisionDamageToPlayer(float collisionSpeed)
            {
                HurtPlayer((collisionSpeed - ins._config.mainConfig.minColisionSpeed) * ins._config.mainConfig.fallDamageScale);
                Effect.server.Run("assets/bundled/prefabs/fx/player/fall-damage.prefab", player.transform.position);
            }

            void HurtPlayer(float speed)
            {
                player.Hurt(speed * ins._config.mainConfig.fallDamageScale);
            }

            void FixedUpdate()
            {
                if (!CheckPlayer() || !CheckWater())
                {
                    DestroyPogoDelay();
                    return;
                }
                CheckPogoItemInPLayerInventoy();
                if (wantDismount || forceDismount)
                {
                    if (CanPlayerDismount()) DestroyPogoDelay();
                }

                ButtonControl();
                AutoHorizont();
            }

            bool CheckPlayer()
            {
                if (movableBaseMountable._mounted == null || player == null || player.IsSleeping() || !player.isMounted || !player.IsConnected) return false;
                return true;
            }

            void CheckPogoItemInPLayerInventoy()
            {
                if (ins._config.mainConfig.itemType == 0)
                {
                    Item activeItem = player.GetActiveItem();
                    wantDismount = activeItem == null || activeItem.info.shortname != pogoConfig.itemConfig.shortname || activeItem.skin != pogoConfig.itemConfig.skin;
                }

                else if (ins._config.mainConfig.itemType == 1)
                {
                    wantDismount = !player.inventory.containerWear.itemList.Any(x => x.info.shortname == pogoConfig.itemConfig.shortname && x.skin == pogoConfig.itemConfig.skin);
                }
            }

            bool CheckWater()
            {
                return player.transform.position.y > 0;
            }

            void ButtonControl()
            {
                if (player.serverInput.IsDown(BUTTON.BACKWARD))
                {
                    rigidbody.AddTorque(-movableDroppedItem.transform.right * 750 * pogoConfig.tiltSpeedScale);
                }
                else if (player.serverInput.IsDown(BUTTON.FORWARD))
                {
                    rigidbody.AddTorque(movableDroppedItem.transform.right * 750 * pogoConfig.tiltSpeedScale);
                }

                if (player.serverInput.IsDown(BUTTON.LEFT))
                {
                    movableDroppedItem.transform.Rotate(Vector3.up, -2 * pogoConfig.rotationButtonSpeedScale);
                    //rigidbody.AddTorque(-Vector3.up * 50);

                }
                else if (player.serverInput.IsDown(BUTTON.RIGHT))
                {
                    movableDroppedItem.transform.Rotate(Vector3.up, 2 * pogoConfig.rotationButtonSpeedScale);
                    //rigidbody.AddTorque(Vector3.up * 50);
                }

                if ((player.serverInput.WasJustPressed(BUTTON.JUMP) || player.serverInput.WasJustPressed(BUTTON.JUMP)) && rigidbody.velocity.magnitude < 1f && isGroinded)
                {
                    TimeSince timeNow = 0f;
                    if (timeSinceLastJump - timeNow < 0.5f) return;
                    timeSinceLastJump = 0;
                    rigidbody.AddForce(movableDroppedItem.transform.up * 8000);
                }
            }

            void AutoHorizont()
            {
                float horizonAngle = Vector3.Angle(movableDroppedItem.transform.right, new Vector3(movableDroppedItem.transform.right.x, 0, movableDroppedItem.transform.right.z));
                bool right = movableDroppedItem.transform.right.y < 0;
                if (right) horizonAngle *= -1;
                rigidbody.AddTorque(-movableDroppedItem.transform.forward * horizonAngle * 20);
            }

            void DestroyPogoDelay()
            {
                ins.NextTick(() => movableDroppedItem.Kill());
            }

            void DestroyPogoImmediately()
            {
                movableDroppedItem.Kill();
            }

            void OnDestroy()
            {
                if (player != null) SwitchPlayerViewMode(false);
            }

            class EntityLocation
            {
                internal static Vector3 delta = new Vector3(0, 0.51f, 0.31f);

                internal static HashSet<EntityLocation> entityLocations = new HashSet<EntityLocation>
                {
                    new EntityLocation("assets/prefabs/weapons/speargun/speargun.entity.prefab", new Vector3(-0.074f, 0.123f, -0.097f), new Vector3(90f, 90f, 0f)),
                    new EntityLocation("assets/prefabs/weapons/speargun/speargun.entity.prefab", new Vector3(0.076f, 0.123f, -0.097f), new Vector3(90f, 270f, 0f)),

                    new EntityLocation("assets/prefabs/tools/lumberjack_tools/lumberjack_axe.entity.prefab", new Vector3(-0.050f, -0.547f, -0.026f), new Vector3(5.53f, 95.032f, 172.670f)),
                    new EntityLocation("assets/prefabs/tools/lumberjack_tools/lumberjack_axe.entity.prefab", new Vector3(0.063f, -0.516f, -0.026f), new Vector3(353.712f, 265.957f, 343.086f)),

                    new EntityLocation("assets/prefabs/tools/lumberjack_tools/lumberjack_pick.entity.prefab", new Vector3(-0.244f, 0.432f, -0.103f), new Vector3(4.9f, 264.837f, 77.613f)),
                    new EntityLocation("assets/prefabs/tools/lumberjack_tools/lumberjack_pick.entity.prefab", new Vector3(0.247f, 0.429f, -0.09f), new Vector3(5.387f, 84.817f, 78.379f)),

                    new EntityLocation("assets/prefabs/tools/medical syringe/syringe_medical.entity.prefab", new Vector3(0.013f, -0.645f, -0.013f), new Vector3(277.479f, 68.598f, 201.577f)),
                    new EntityLocation("assets/prefabs/tools/lumberjack_tools/lumberjack_pick.entity.prefab", new Vector3(0.009f, -0.434f, -0.338f), new Vector3(5.327f, 174.756f, 77.722f)),
                };

                internal string prefabName;
                internal Vector3 position;
                internal Vector3 rotation;

                EntityLocation(string prefabName, Vector3 position, Vector3 rotation)
                {
                    this.position = position;
                    this.rotation = rotation;
                    this.prefabName = prefabName;
                }
            }
        }

        sealed class MovableDroppedItem : DroppedItem
        {
            internal static MovableDroppedItem CreateMovableDroppedItem(Vector3 position, Quaternion rotation)
            {
                DroppedItem droppedItem = GameManager.server.CreateEntity("assets/prefabs/misc/burlap sack/generic_world.prefab", position, rotation) as DroppedItem;
                droppedItem.enableSaving = false;
                droppedItem.allowPickup = false;
                droppedItem.item = ItemManager.CreateByName("weapon.mod.flashlight");
                MovableDroppedItem movableDroppedItem = droppedItem.gameObject.AddComponent<MovableDroppedItem>();
                BuildManager.CopySerializableFields(droppedItem, movableDroppedItem);
                droppedItem.StopAllCoroutines();
                UnityEngine.GameObject.DestroyImmediate(droppedItem, true);
                movableDroppedItem.Spawn();
                return movableDroppedItem;
            }

            public override float MaxVelocity()
            {
                return 100;
            }

            public override float GetDespawnDuration()
            {
                return float.MaxValue;
            }
        }

        sealed class MovableBaseMountable : BaseMountable
        {
            internal static MovableBaseMountable CreateMovableBaseMountable(BaseEntity parentEntity, string seatPrefab, Vector3 localPosition, Vector3 localRotation)
            {
                BaseMountable baseMountable = GameManager.server.CreateEntity(seatPrefab, parentEntity.transform.position) as BaseMountable;
                baseMountable.enableSaving = false;

                MovableBaseMountable movableBaseMountable = baseMountable.gameObject.AddComponent<MovableBaseMountable>();
                BuildManager.CopySerializableFields(baseMountable, movableBaseMountable);

                baseMountable.StopAllCoroutines();
                UnityEngine.GameObject.DestroyImmediate(baseMountable, true);
                BuildManager.SetParent(parentEntity, movableBaseMountable, localPosition, localRotation);
                movableBaseMountable.Spawn();
                return movableBaseMountable;
            }

            public override void DismountAllPlayers()
            {

            }

            public override bool GetDismountPosition(BasePlayer player, out Vector3 res)
            {
                res = player.transform.position;
                return true;
            }
        }

        static class BuildManager
        {
            internal static void SetParent(BaseEntity parrentEntity, BaseEntity childEntity, Vector3 localPosition, Vector3 localRotation)
            {
                childEntity.SetParent(parrentEntity, true, false);
                childEntity.transform.localPosition = localPosition;
                childEntity.transform.localEulerAngles = localRotation;
            }

            internal static void CopySerializableFields<T>(T src, T dst)
            {
                FieldInfo[] srcFields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (FieldInfo field in srcFields)
                {
                    object value = field.GetValue(src);
                    field.SetValue(dst, value);
                }
            }
        }

        static class LootManager
        {
            internal static void TrySpawnItemInDefaultCrate(LootContainer lootContatiner, ItemConfig itemConfig, float chance, int removeItemIndex = 0)
            {
                ins.NextTick(() =>
                {
                    if (UnityEngine.Random.Range(0f, 100f) <= chance)
                    {
                        Item item = CreateItem(itemConfig, 1);
                        if (lootContatiner.inventory.itemList.Count > removeItemIndex)
                        {
                            Item removeItem = lootContatiner.inventory.itemList[removeItemIndex];
                            if (removeItem != null) lootContatiner.inventory.Remove(removeItem);
                        }
                        if (!item.MoveToContainer(lootContatiner.inventory)) item.Remove();
                    }
                });
            }

            internal static Item CreateItem(ItemConfig itemConfig, int amount)
            {
                Item item = ItemManager.CreateByName(itemConfig.shortname, amount, itemConfig.skin);
                if (itemConfig.name != "") item.name = itemConfig.name;
                return item;
            }

            internal static void GiveItemToPLayer(BasePlayer player, ItemConfig itemConfig, int amount)
            {
                Item item = CreateItem(itemConfig, amount);
                int spaceCountItem = PLayerInventory.GetSpaceCountItem(player, item.info.shortname, 1, item.skin);
                int inventoryItemCount;
                if (spaceCountItem > item.amount) inventoryItemCount = item.amount;
                else inventoryItemCount = spaceCountItem;

                if (inventoryItemCount > 0)
                {
                    Item itemInventory = CreateItem(itemConfig, inventoryItemCount);
                    item.amount -= inventoryItemCount;
                    PLayerInventory.MoveInventoryItem(player, itemInventory);
                }

                if (item.amount > 0) PLayerInventory.DropExtraItem(player, item);
            }

            static class PLayerInventory
            {
                internal static int GetSpaceCountItem(BasePlayer player, string shortname, int stack, ulong skinID)
                {
                    int slots = player.inventory.containerMain.capacity + player.inventory.containerBelt.capacity;
                    int taken = player.inventory.containerMain.itemList.Count + player.inventory.containerBelt.itemList.Count;
                    int result = (slots - taken) * stack;
                    foreach (Item item in Enumerable.Concat(player.inventory.containerMain?.itemList ?? Enumerable.Empty<Item>(), Enumerable.Concat(player.inventory.containerBelt?.itemList ?? Enumerable.Empty<Item>(), player.inventory.containerWear?.itemList ?? Enumerable.Empty<Item>())))
                    {
                        if (item.info.shortname == shortname && item.skin == skinID && item.amount < stack) result += stack - item.amount;
                    }
                    return result;
                }

                internal static void MoveInventoryItem(BasePlayer player, Item item)
                {
                    if (item.amount <= item.MaxStackable())
                    {
                        foreach (Item itemInv in Enumerable.Concat(player.inventory.containerMain?.itemList ?? Enumerable.Empty<Item>(), Enumerable.Concat(player.inventory.containerBelt?.itemList ?? Enumerable.Empty<Item>(), player.inventory.containerWear?.itemList ?? Enumerable.Empty<Item>())))
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

                internal static void DropExtraItem(BasePlayer player, Item item)
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
            }
        }

        static class NotifyManager
        {
            internal static void PrintError(BasePlayer player, string langKey, params object[] args)
            {
                if (player == null) ins.PrintError(ClearColorAndSize(GetMessage(langKey, null, args)));
                else ins.PrintToChat(player, GetMessage(langKey, player.UserIDString, args));
            }

            internal static string ClearColorAndSize(string message)
            {
                message = message.Replace("</color>", string.Empty);
                message = message.Replace("</size>", string.Empty);
                while (message.Contains("<color="))
                {
                    int index = message.IndexOf("<color=");
                    message = message.Remove(index, message.IndexOf(">", index) - index + 1);
                }
                while (message.Contains("<size="))
                {
                    int index = message.IndexOf("<size=");
                    message = message.Remove(index, message.IndexOf(">", index) - index + 1);
                }
                return message;
            }

            internal static void SendMessageToPlayer(BasePlayer player, string langKey, params object[] args)
            {
                if (ins._config.mainConfig.useChat) ins.PrintToChat(player, GetMessage(langKey, player.UserIDString, args));
                if (ins._config.supportedPluginsConfig.GUIAnnouncements.enable) ins.GUIAnnouncements?.Call("CreateAnnouncement", ClearColorAndSize(GetMessage(langKey, player.UserIDString, args)), ins._config.supportedPluginsConfig.GUIAnnouncements.bannerColor, ins._config.supportedPluginsConfig.GUIAnnouncements.textColor, player, ins._config.supportedPluginsConfig.GUIAnnouncements.apiAdjustVPosition);
                if (ins._config.supportedPluginsConfig.notify.enable) player.SendConsoleCommand($"notify.show {ins._config.supportedPluginsConfig.notify.type} {ClearColorAndSize(GetMessage(langKey, player.UserIDString, args))}");
            }
        }

        static class SupportedPluginsManager
        {
            internal static bool CheckAllPlayerZone(BasePlayer player)
            {
                if (!ins.plugins.Exists("ZoneManager")) return true;
                string[] playerZones = (string[])ins.ZoneManager.Call("GetPlayerZoneIDs", player);
                if (playerZones == null || playerZones.Length == 0) return true;
                return !playerZones.Any(zoneName => ins._config.supportedPluginsConfig.zoneManager.blockedFlags.Any(flagName => (bool)ins.ZoneManager.Call("HasFlag", zoneName, flagName)));
            }

            internal static bool CheckZoneOfZoneManager(string ZoneID)
            {
                if (ins.plugins.Exists("ZoneManager") && ins._config.supportedPluginsConfig.zoneManager.blockedFlags.Any(x => (bool)ins.ZoneManager.Call("HasFlag", ZoneID, x))) return false;
                return true;
            }
        }
        #endregion Classes

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GetPogoStick"] = "{0} You <color=#738d43>got</color> a pogo stick!",
                ["NoPermission"] = "{0} You <color=#b03b1e>do not have permission</color> to use a pogo stick!",
                ["AreaBlock"] = "{0} It is <color=#b03b1e>forbidden</color> to use pogo in this area!",
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GetPogoStick"] = "{0} Вы <color=#738d43>получили</color> пого!",
                ["NoPermission"] = "{0} У вас <color=#b03b1e>нет разрешения</color> использовать пого!",
                ["AreaBlock"] = "{0} <color=#b03b1e>Запрещено</color> использовать пого в этой области!",
            }, this, "ru");
        }

        internal static string GetMessage(string langKey, string userID) => ins.lang.GetMessage(langKey, ins, userID);

        internal static string GetMessage(string langKey, string userID, params object[] args) => (args.Length == 0) ? GetMessage(langKey, userID) : string.Format(GetMessage(langKey, userID), args);
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

        public class MainConfig
        {
            [JsonProperty(en ? "Use Chat Notifications? [true/false]" : "Использовать ли чат для уведомлений?  [true/false]")] public bool useChat { get; set; }
            [JsonProperty(en ? "Item type (0 - item must be held in hands; 1 - clothing item)" : "Тип предмета (0 - нужно держать в руках; 1 - предмет одежды)")] public int itemType { get; set; }
            [JsonProperty(en ? "Third-person view" : "Вид от третьего лица")] public bool thirdPersonViewMode { get; set; }
            [JsonProperty(en ? "Allow pogo deployment in the air" : "Разрешить использование пого в воздухе")] public bool allowInAir { get; set; }
            [JsonProperty(en ? "Allow the use of pogo in the water" : "Разрешить использование пого в воде")] public bool allowInWater { get; set; }
            [JsonProperty(en ? "Health loss multiplier when landing incorrectly" : "Множитель потери здоровья при неправильном приземлении")] public float fallDamageScale { get; set; }
            [JsonProperty(en ? "Minimum speed at which collision damage will be caused" : "Минимальная скорость при которой будет нанесен урон от столкновения")] public float minColisionSpeed { get; set; }
            [JsonProperty(en ? "The crate prefab - chance" : "Название топологии - множитель упругости")] public Dictionary<string, float> topologies { get; set; }
        }

        public class PogoConfig
        {
            [JsonProperty(en ? "Preset Name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty(en ? "Permission to use" : "Разрешение для использования")] public string permission { get; set; }
            [JsonProperty(en ? "Bouncity" : "Упругость")] public float bouncity { get; set; }
            [JsonProperty(en ? "Maximum speed" : "Максимальная скорость")] public float maxSpeed { get; set; }
            [JsonProperty(en ? "Loss of durability with each jump" : "Потеря прочности при каждом прыжке")] public float lossOfDurability { get; set; }
            [JsonProperty(en ? "Drag" : "Сопротивление воздуха")] public float drag { get; set; }
            [JsonProperty(en ? "Rotation speed multiplier (A/D)" : "Множитель скорости поворота (A/D)")] public float rotationButtonSpeedScale { get; set; }
            [JsonProperty(en ? "Tilt speed multiplier (W/S)" : "Множитель скорости наклона (W/S)")] public float tiltSpeedScale { get; set; }
            [JsonProperty(en ? "Item" : "Предмет")] public SpawnedItemConfig itemConfig { get; set; }
        }

        public class SpawnedItemConfig : ItemConfig
        {
            [JsonProperty(en ? "Enable spawn in crates [true/false]" : "Включить спавн в ящиках [true/false]", Order = 100)] public bool inLootSpawn { get; set; }
            [JsonProperty(en ? "The crate prefab - chance" : "Префаб ящика - шанс", Order = 101)] public Dictionary<string, float> crateChanses { get; set; }
        }

        public class ItemConfig
        {
            [JsonProperty("Shortname")] public string shortname { get; set; }
            [JsonProperty("Skin")] public ulong skin { get; set; }
            [JsonProperty("Name")] public string name { get; set; }
        }

        public class SupportedPluginsConfig
        {
            [JsonProperty(en ? "GUIAnnouncements Settings" : "Настройка GUI Announcements")] public GUIAnnouncementsConfig GUIAnnouncements { get; set; }
            [JsonProperty(en ? "Notify Settings" : "Настройка Notify")] public NotifyPluginConfig notify { get; set; }
            [JsonProperty(en ? "ZoneManager Settings" : "Настройка ZoneManager")] public ZoneManagerPluginConfig zoneManager { get; set; }
        }

        public class GUIAnnouncementsConfig
        {
            [JsonProperty(en ? "GUIAnnouncements plugin in use? [true/false]" : "Использовать ли GUI Announcements? [true/false]")] public bool enable { get; set; }
            [JsonProperty(en ? "Banner color" : "Цвет баннера")] public string bannerColor { get; set; }
            [JsonProperty(en ? "Text color" : "Цвет текста")] public string textColor { get; set; }
            [JsonProperty(en ? "Adjust Vertical Position" : "Отступ от верхнего края")] public float apiAdjustVPosition { get; set; }
        }

        public class NotifyPluginConfig
        {
            [JsonProperty(en ? "Notify plugin in use? [true/false]" : "Использовать ли Notify? [true/false]")] public bool enable { get; set; }
            [JsonProperty(en ? "Type" : "Тип")] public string type { get; set; }
        }

        public class ZoneManagerPluginConfig
        {
            [JsonProperty(en ? "ZoneManager plugin in use? [true/false]" : "Использовать ли ZoneManager? [true/false]")] public bool enable { get; set; }
            [JsonProperty(en ? "List of flags prohibiting pogo stick" : "Список флагов запрещающих пого стик")] public HashSet<string> blockedFlags { get; set; }
        }

        private class PluginConfig
        {
            [JsonProperty(en ? "Version" : "Версия")] public VersionNumber version { get; set; }
            [JsonProperty(en ? "Chat Prefix" : "Префикс в чате")] public string prefix { get; set; }
            [JsonProperty(en ? "Main Settings" : "Основные настройки")] public MainConfig mainConfig { get; set; }
            [JsonProperty(en ? "Pogo presets" : "Пресеты")] public HashSet<PogoConfig> pogoConfigs { get; set; }
            [JsonProperty(en ? "Supported Plugins" : "Поддерживаемые плагины")] public SupportedPluginsConfig supportedPluginsConfig { get; set; }

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    version = new VersionNumber(1, 0, 0),
                    prefix = "[Pogo Stick]",
                    mainConfig = new MainConfig
                    {
                        useChat = true,
                        itemType = 0,
                        thirdPersonViewMode = false,
                        allowInAir = true,
                        allowInWater = false,
                        minColisionSpeed = 5,
                        fallDamageScale = 1,
                        topologies = new Dictionary<string, float>
                        {
                            ["Sand"] = 0.75f,
                            ["Gravel"] = 0.85f,
                            ["Grass"] = 0.85f,
                            ["Snow"] = 0.75f,
                            ["Dirt"] = 0.75f,
                        },
                    },
                    pogoConfigs = new HashSet<PogoConfig>
                    {
                        new PogoConfig
                        {
                            presetName = "default",
                            permission = "",
                            bouncity = 1.7f,
                            maxSpeed = 15.5f,
                            lossOfDurability = 0.1f,
                            drag = 0.05f,
                            rotationButtonSpeedScale = 2f, 
                            tiltSpeedScale = 2f,
                            itemConfig = new SpawnedItemConfig
                            {
                                shortname = "fuse",
                                skin = 2896727210,
                                name = en ? "POGO STICK" : "ПОГО СТИК",
                                inLootSpawn = true,
                                crateChanses = new Dictionary<string, float>
                                {
                                    ["assets/bundled/prefabs/radtown/crate_normal.prefab"] = 5,
                                    ["assets/bundled/prefabs/radtown/crate_elite.prefab"] = 5
                                }
                            }
                            
                        },
                        new PogoConfig
                        {
                            presetName = "upgraded",
                            permission = "",
                            bouncity = 1.8f,
                            maxSpeed = 17.5f,
                            lossOfDurability = 0.1f,
                            drag = 0.05f,
                            rotationButtonSpeedScale = 2f,
                            tiltSpeedScale = 3f,
                            itemConfig = new SpawnedItemConfig
                            {
                                shortname = "fuse",
                                skin = 2896727425,
                                name = en ? "UPGRADED POGO STICK" : "УЛУЧШЕННЫЙ ПОГО СТИК",
                                inLootSpawn = false,
                                crateChanses = new Dictionary<string, float>
                                {
                                    ["assets/bundled/prefabs/radtown/crate_elite.prefab"] = 5
                                }
                            }
                        },
                        new PogoConfig
                        {
                            presetName = "fast",
                            permission = "pogostick.fast",
                            bouncity = 1.9f,
                            maxSpeed = 21.5f,
                            lossOfDurability = 0.1f,
                            drag = 0.05f,
                            rotationButtonSpeedScale = 3f,
                            tiltSpeedScale = 4f,
                            itemConfig = new SpawnedItemConfig
                            {
                                shortname = "fuse",
                                skin = 2896727535,
                                name = en ? "FAST POGO STICK" : "БЫСТРЫЙ ПОГО СТИК",
                                inLootSpawn = false,
                                crateChanses = new Dictionary<string, float>
                                {
                                    ["assets/bundled/prefabs/radtown/crate_elite.prefab"] = 5
                                }
                            }
                        }
                    },
                    supportedPluginsConfig =  new SupportedPluginsConfig
                    {
                        GUIAnnouncements = new GUIAnnouncementsConfig
                        {
                            enable = false,
                            bannerColor = "Grey",
                            textColor = "White",
                            apiAdjustVPosition = 0.03f
                        },
                        notify = new NotifyPluginConfig
                        {
                            enable = false,
                            type = "0"
                        },
                        zoneManager = new ZoneManagerPluginConfig
                        { 
                            enable = false,
                            blockedFlags = new HashSet<string> 
                            {
                                "eject"
                            }
                        }
                    }
                };
            }
        }
        #endregion Config
    }
}

namespace Oxide.Plugins.PogoStickExtensionMethods
{
    public static class ExtensionMethods
    {
        public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return true;
            return false;
        }

        public static HashSet<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            HashSet<TSource> result = new HashSet<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) result.Add(enumerator.Current);
            return result;
        }

        public static TSource FirstOrDefault<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return enumerator.Current;
            return default(TSource);
        }

        public static HashSet<T> OfType<T>(this IEnumerable<BaseNetworkable> source)
        {
            HashSet<T> result = new HashSet<T>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (enumerator.Current is T) result.Add((T)(object)enumerator.Current);
            return result;
        }

        public static bool IsExists(this BaseNetworkable entity) => entity != null && !entity.IsDestroyed;

        public static bool IsRealPlayer(this BasePlayer player) => player != null && player.userID.IsSteamId();
    }
}
/* Boosty - https://boosty.to/skulidropek 
Discord - https://discord.gg/k3hXsVua7Q 
Discord The Rust Bay - https://discord.gg/Zq3TVjxKWk  */