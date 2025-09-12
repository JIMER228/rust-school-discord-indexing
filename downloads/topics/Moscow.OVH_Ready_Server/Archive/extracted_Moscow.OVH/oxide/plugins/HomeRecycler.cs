using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("HomeRecycler", "wazzzup", "1.4.3")]
    [Description("Allows to have Recycler at home")]
    internal class HomeRecycler : RustPlugin
    {
        public static HomeRecycler Instance;
        private ItemBlueprint bp;
        private ConfigData configData;
        private bool craftImagesItited = true;
        [PluginReference] private Plugin Friends, ImageLibrary;

        public Dictionary<int, KeyValuePair<string, int>> itemsNeededToCraft =
            new Dictionary<int, KeyValuePair<string, int>>();

        private readonly Dictionary<BasePlayer, RecyclerEntity> pickipRecyclers =
            new Dictionary<BasePlayer, RecyclerEntity>();

        private PluginData pluginData;
        private int recyclerItemId;
        private readonly Dictionary<uint, ulong> startedRecyclers = new Dictionary<uint, ulong>();

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Title, pluginData);
        }

        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                PermissionsRates = new Dictionary<string, Rates>
                    {{"viptest", new Rates()}, {"viptest2", new Rates {Priority = 2, Ratio = 0.7f, Speed = 3f}}}
            };
            SaveConfig(configData);
            PrintWarning("New configuration file created.");
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(
                new Dictionary<string, string>
                {
                    {"Title", "Recycler:"}, {"badCommand", "you can get recycler in kit home"},
                    {"buldingBlocked", "you need building privilege"}, {"cooldown", "Сooldown, wait {0} seconds"},
                    {"cooldown craft", "Сooldown, wait {0} seconds"},
                    {"recycler crafted", "You have crafted a recycler"}, {"recycler got", "You have got a recycler"},
                    {"cannot craft", "Sorry, you can't craft a recycler"},
                    {"not enough ingredient", "You should have {0} x{1}"},
                    {"inventory full", "You should have space in inventory"},
                    {"limit", "You have reached the limit of {0} recyclers"},
                    {"place on construction", "You can't place it on ground"},
                    {"cant pick", "You can pickup only your own or friend recycler"}, {"UIPickup", "Pickup recycler?"},
                    {"UIPickupYes", "Yes"}, {"UIPickupNo", "No"}, {"UICraft", "Crafting recycler"},
                    {"UICraftYes", "Craft"}, {"UICraftNo", "Cancel"}, {"notEnough", "not enough"},
                    {"repair first", "You should repair it first"}
                }, this);
            lang.RegisterMessages(
                new Dictionary<string, string>
                {
                    {"Title", "Переработчик:"}, {"badCommand", "ты можешь получить его в kit home"},
                    {"buldingBlocked", "нужна авторизация в шкафу"}, {"cooldown", "Подождите еще {0} секунд"},
                    {"cooldown craft", "Подождите еще {0} секунд"}, {"recycler crafted", "Ты скрафтил переработчик"},
                    {"recycler got", "Ты получил переработчик"}, {"cannot craft", "Ты не можешь крафтить переработчик"},
                    {"not enough ingredient", "Тебе нужно {0} x{1}"}, {"inventory full", "Нет места в инвентаре"},
                    {"limit", "Достигнут лимит в {0} переработчика"},
                    {"place on construction", "Нельзя ставить на землю"},
                    {"cant pick", "Ты можешь поднять только свой переработчик или друга"},
                    {"UIPickup", "Поднять переработчик?"}, {"UIPickupYes", "Да"}, {"UIPickupNo", "Нет"},
                    {"UICraft", "Крафт переработчика"}, {"UICraftYes", "Скрафтить"}, {"UICraftNo", "Отмена"},
                    {"notEnough", "недостаточно"}, {"repair first", "Сначала отремонтируй"}
                }, this, "ru");
        }

        private void Init()
        {
            Instance = this;
            configData = Config.ReadObject<ConfigData>();
            configData.PermissionsRates = configData.PermissionsRates.OrderBy(i => -i.Value.Priority)
                .ToDictionary(x => x.Key, x => x.Value);
            SaveConfig(configData);
            Unsubscribe(nameof(OnLootSpawn));
            if (!configData.allowRepair && !configData.allowPickupByHammerHit) Unsubscribe(nameof(OnHammerHit));
            if (!configData.trackStacking)
            {
                Unsubscribe(nameof(CanCombineDroppedItem));
                Unsubscribe(nameof(CanStackItem));
                Unsubscribe(nameof(OnItemSplit));
            }

            try
            {
                pluginData = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Title);
            }
            catch
            {
                pluginData = new PluginData();
            }

            foreach (var perm in configData.PermissionsRates)
                permission.RegisterPermission("homerecycler." + perm.Key, this);
            permission.RegisterPermission("homerecycler.canget", this);
            permission.RegisterPermission("homerecycler.cancraft", this);
            permission.RegisterPermission("homerecycler.ignorecooldown", this);
            permission.RegisterPermission("homerecycler.ignorecraftcooldown", this);
            if (configData.useSpawning)
                cmd.AddChatCommand(configData.chatCommand, this, "cmdRec");
            else
                PrintWarning(
                    $"Spawning/getting by command {configData.chatCommand} is disabled, check config if needed");
            if (configData.useCrafting)
            {
                if (configData.itemsNeededToCraft.Count < 1)
                    PrintWarning("no items set to craft, check config");
                else
                    cmd.AddChatCommand(configData.craftCommand, this, "cmdCraft");
            }
            else
            {
                PrintWarning($"Crafting by command {configData.craftCommand} is disabled, check config if needed");
            }
        }

        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }

        private void OnNewSave()
        {
            pluginData = new PluginData();
            SaveData();
        }

        private void OnServerInitialized()
        {
            cmd.AddConsoleCommand("giverecycler", this, "cmdGiveRecycler");
            cmd.AddConsoleCommand("pickuprecycler", this, "cmdPickupRecycler");
            recyclerItemId = ItemManager.FindItemDefinition(configData.recyclerItemName).itemid;
            var allobjects = UnityEngine.Object.FindObjectsOfType<Recycler>();
            foreach (var r in allobjects)
                if (r.OwnerID != 0)
                {
                    if (r.gameObject.GetComponent<RecyclerEntity>() == null)
                        r.gameObject.AddComponent<RecyclerEntity>();
                    RecyclerSettings(r, true);
                }

            if (configData.useCrafting)
            {
                var newLoadOrder = new Dictionary<string, string>();
                foreach (var i in configData.itemsNeededToCraft)
                {
                    var def = ItemManager.FindItemDefinition(i.Key);
                    if (def == null)
                    {
                        PrintWarning($"cannot find item {i.Key} for crafting, check config");
                        continue;
                    }

                    itemsNeededToCraft.Add(def.itemid, i);
                    newLoadOrder.Add(i.Key, $"http://rustlabs.com/img/items180/{i.Key}.png");
                }

                craftImagesItited = false;
                ImageLibrary?.CallHook("ImportImageList", Title, newLoadOrder, 0UL, true, new Action(IMInit));
            }

            if (configData.spawnInLoot)
            {
                foreach (var container in Resources.FindObjectsOfTypeAll<LootContainer>())
                    if (configData.Loot.FirstOrDefault(c => c.containerName == container.ShortPrefabName) == null)
                        configData.Loot.Add(new Loot {containerName = container.ShortPrefabName});
                Subscribe(nameof(OnLootSpawn));
                foreach (var container in UnityEngine.Object.FindObjectsOfType<LootContainer>()) container.SpawnLoot();
            }

            SaveConfig(configData);
        }

        private void IMInit()
        {
            craftImagesItited = true;
        }

        private void OnLootSpawn(LootContainer container)
        {
            timer.In(1f, () =>
            {
                if (container == null) return;
                var cont = configData.Loot.FirstOrDefault(c => c.containerName == container.ShortPrefabName);
                if (cont == null || cont.probability < 1) return;
                var current = Random.Range(0, 100);
                if (current <= cont.probability)
                {
                    container.inventorySlots = container.inventory.itemList.Count() + 5;
                    container.inventory.capacity = container.inventory.itemList.Count() + 5;
                    container.SendNetworkUpdateImmediate();
                    GiveRecycler(container.inventory);
                }
            });
        }

        private void Unload()
        {
            var allobjects = UnityEngine.Object.FindObjectsOfType<RecyclerEntity>();
            foreach (var r in allobjects) UnityEngine.Object.Destroy(r);
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "RecyclerUIPickup");
                CuiHelper.DestroyUi(player, "RecyclerUICraft");
            }
        }

        private object CanStackItem(Item item, Item anotherItem)
        {
            if (item.info.itemid == recyclerItemId && item.skin != anotherItem.skin) return false;
            return null;
        }

        private object OnItemSplit(Item item, int split_Amount)
        {
            if (item.info.itemid == recyclerItemId && item.skin == 2226164994)
            {
                var byItemId = ItemManager.CreateByItemID(item.info.itemid, 1, item.skin);
                item.amount -= split_Amount;
                byItemId.amount = split_Amount;
                byItemId.name = item.name;
                item.MarkDirty();
                return byItemId;
            }

            return null;
        }

        private object CanCombineDroppedItem(DroppedItem drItem, DroppedItem anotherDrItem)
        {
            if (drItem.item.info.itemid == recyclerItemId &&
                drItem.item.info.itemid == anotherDrItem.item.info.itemid &&
                drItem.item.skin != anotherDrItem.item.skin) return false;
            return null;
        }

        private float GetCraftLimit(BasePlayer player)
        {
            foreach (var perm in configData.PermissionsRates)
                if (permission.UserHasPermission(player.UserIDString, "homerecycler." + perm.Key))
                    return perm.Value.craftLimit;
            return configData.DefaultRates.craftLimit;
        }

        private float GetSpawnLimit(BasePlayer player)
        {
            foreach (var perm in configData.PermissionsRates)
                if (permission.UserHasPermission(player.UserIDString, "homerecycler." + perm.Key))
                    return perm.Value.spawnLimit;
            return configData.DefaultRates.spawnLimit;
        }

        private float GetCraftCooldown(BasePlayer player)
        {
            foreach (var perm in configData.PermissionsRates)
                if (permission.UserHasPermission(player.UserIDString, "homerecycler." + perm.Key))
                    return perm.Value.craftCooldown;
            return configData.DefaultRates.craftCooldown;
        }

        private float GetSpawnCooldown(BasePlayer player)
        {
            foreach (var perm in configData.PermissionsRates)
                if (permission.UserHasPermission(player.UserIDString, "homerecycler." + perm.Key))
                    return perm.Value.spawnCooldown;
            return configData.DefaultRates.spawnCooldown;
        }

        private void cmdRec(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "homerecycler.canget"))
            {
                SendMsg(player, "badCommand");
                return;
            }

            if (configData.useSpawnCooldown)
                if (!permission.UserHasPermission(player.UserIDString, "homerecycler.ignorecooldown"))
                {
                    var time = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
                    var spawnCooldown = GetSpawnCooldown(player);
                    if (!pluginData.userCooldowns.ContainsKey(player.userID))
                    {
                        pluginData.userCooldowns.Add(player.userID, time + spawnCooldown);
                    }
                    else
                    {
                        var nextUseTime = pluginData.userCooldowns[player.userID];
                        if (nextUseTime > time)
                        {
                            SendMsg(player, "cooldown", true, ((int) (nextUseTime - time)).ToString());
                            return;
                        }

                        pluginData.userCooldowns[player.userID] = time + spawnCooldown;
                    }

                    SaveData();
                }

            if (configData.useSpawnLimit)
            {
                if (!pluginData.userSpawned.ContainsKey(player.userID)) pluginData.userSpawned.Add(player.userID, 0);
                var spawnLimit = GetSpawnLimit(player);
                if (pluginData.userSpawned[player.userID] >= spawnLimit)
                {
                    SendMsg(player, "limit", true, spawnLimit.ToString());
                    return;
                }

                pluginData.userSpawned[player.userID]++;
                SaveData();
            }

            if (GiveRecycler(player))
                SendMsg(player, "recycler got");
            else
                SendMsg(player, "inventory full");
        }

        private void cmdCraft(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "homerecycler.cancraft"))
            {
                SendMsg(player, "cannot craft");
                return;
            }

            if (configData.useCraftCooldown)
                if (!permission.UserHasPermission(player.UserIDString, "homerecycler.ignorecraftcooldown"))
                {
                    var time = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
                    if (pluginData.userCooldownsCraft.ContainsKey(player.userID))
                    {
                        var nextUseTime = pluginData.userCooldownsCraft[player.userID];
                        if (nextUseTime > time)
                        {
                            SendMsg(player, "cooldown craft", true, ((int) (nextUseTime - time)).ToString());
                            return;
                        }
                    }
                }

            if (configData.useCraftLimit)
            {
                if (!pluginData.userCrafted.ContainsKey(player.userID)) pluginData.userCrafted.Add(player.userID, 0);
                var craftLimit = GetCraftLimit(player);
                if (pluginData.userCrafted[player.userID] >= craftLimit)
                {
                    SendMsg(player, "limit", true, craftLimit.ToString());
                    return;
                }
            }

            if (configData.useUIForCrafting)
            {
                ShowUICraft(player);
                return;
            }

            EndCrafting(player);
        }

        [ConsoleCommand("craftrecycler")]
        private void CcmdCraft(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            CuiHelper.DestroyUi(player, "RecyclerUICraft");
            if (!permission.UserHasPermission(player.UserIDString, "homerecycler.cancraft"))
            {
                SendMsg(player, "cannot craft");
                return;
            }

            EndCrafting(player);
        }

        private void EndCrafting(BasePlayer player)
        {
            var mess = "";
            var enough = true;
            foreach (var item in itemsNeededToCraft)
            {
                var haveCount = player.inventory.GetAmount(item.Key);
                if (haveCount >= item.Value.Value) continue;
                mess += string.Format(msg("not enough ingredient", player) + "\n", item.Value.Key, item.Value.Value);
                enough = false;
            }

            if (!enough)
            {
                SendReply(player, mess);
                return;
            }

            if (configData.useCraftCooldown)
            {
                var craftCooldown = GetCraftCooldown(player);
                var time = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
                pluginData.userCooldownsCraft[player.userID] = time + craftCooldown;
                SaveData();
            }

            if (configData.useCraftLimit)
            {
                if (!pluginData.userCrafted.ContainsKey(player.userID)) pluginData.userCrafted.Add(player.userID, 0);
                pluginData.userCrafted[player.userID]++;
                SaveData();
            }

            foreach (var item in itemsNeededToCraft) player.inventory.Take(null, item.Key, item.Value.Value);
            if (GiveRecycler(player))
                SendMsg(player, "recycler crafted");
            else
                SendMsg(player, "inventory full");
        }

        private string msg(string key, BasePlayer player = null)
        {
            return lang.GetMessage(key, this, player?.UserIDString);
        }

        private void SendMsg(BasePlayer player, string langkey, bool title = true, params string[] args)
        {
            var message = string.Format(msg(langkey, player), args);
            if (title) message = $"<color=orange>{msg("Title", player)}</color> {message}";
            SendReply(player, message);
        }

        private void cmdGiveRecycler(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player() ?? null;
            if (player?.net.connection.authLevel < 2) return;
            if (arg.Args == null || arg.Args.Length < 1)
            {
                SendReply(arg, "bad syntax");
                return;
            }

            var targetPlayer = BasePlayer.Find(arg.Args[0]);
            if (targetPlayer == null)
            {
                SendReply(arg, "error player not found for give");
                return;
            }

            if (GiveRecycler(targetPlayer))
                SendReply(targetPlayer, msg("recycler got", targetPlayer));
            else
                SendReply(targetPlayer, msg("inventory full", targetPlayer));
        }

        private bool GiveRecycler(ItemContainer container)
        {
            var item = ItemManager.CreateByName(configData.recyclerItemName, 1, 2226164994);
            item.name = "Домашний переработчик";
            return item.MoveToContainer(container, -1, false);
        }

        private bool GiveRecycler(BasePlayer player)
        {
            var item = ItemManager.CreateByName(configData.recyclerItemName, 1, 2226164994);
            item.name = "Домашний переработчик";
            if (!player.inventory.GiveItem(item))
            {
                item.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity);
                return false;
            }

            return true;
        }

        private bool Check(BaseEntity entity)
        {
            var component = entity.gameObject.GetComponent<GroundWatch>();
            var list = Pool.GetList<Collider>();
            Vis.Colliders(entity.transform.TransformPoint(component.groundPosition), component.radius, list,
                component.layers);
            foreach (var collider in list)
                if (!(collider.transform.root == entity.gameObject.transform.root))
                {
                    var baseEntity = collider.gameObject.ToBaseEntity();
                    if ((!(bool) baseEntity || !baseEntity.IsDestroyed && !baseEntity.isClient) &&
                        baseEntity is BuildingBlock)
                    {
                        Pool.FreeList(ref list);
                        return true;
                    }
                }

            Pool.FreeList(ref list);
            return false;
        }

        private void OnEntityBuilt(Planner plan, GameObject obj)
        {
            var entity = obj.GetComponent<BaseEntity>();
            if (entity != null && entity.ShortPrefabName == configData.recyclerShortPrefabName &&
                entity.skinID == 2226164994)
            {
                var player = plan.GetOwnerPlayer();
                if (!configData.allowDeployOnGround && player.net.connection.authLevel < 2)
                    if (!Check(entity))
                    {
                        GiveRecycler(player);
                        SendMsg(player, "place on construction");
                        entity.Kill();
                        return;
                    }

                var recycler = GameManager.server.CreateEntity("assets/bundled/prefabs/static/recycler_static.prefab",
                    entity.transform.position, entity.transform.rotation) as Recycler;
                if (configData.adminSpawnsPublicRecycler && player.net.connection.authLevel == 2) recycler.OwnerID = 0;
                else recycler.OwnerID = player.userID;
                recycler.Spawn();
                NextTick(() => {
                entity?.Kill();
                });
                recycler.gameObject.AddComponent<RecyclerEntity>();
                RecyclerSettings(recycler);
            }
        }

        private void RecyclerSettings(BaseCombatEntity recycler, bool init = false)
        {
            if (configData.allowDamage || configData.allowRepair)
            {
                var itemToClone = GameManager.server.FindPrefab(configData.prefabToCloneDamage)
                    .GetComponent<BaseCombatEntity>();
                recycler._maxHealth = configData.health;
                if (!init) recycler.health = recycler.MaxHealth();
                if (configData.allowRepair)
                {
                    recycler.repair.enabled = true;
                    recycler.repair.itemTarget = itemToClone.repair.itemTarget;
                }

                if (configData.allowDamage) recycler.baseProtection = itemToClone.baseProtection;
            }
        }

        private void ShowUIHit(BasePlayer player, string message)
        {
            var elements = new CuiElementContainer();
            var x = Random.Range(0.45f, 0.5f);
            var y = Random.Range(0.4f, 0.45f);
            var name = $"HitRecyclerUI{Random.Range(1, 10)}";
            CuiHelper.DestroyUi(player, name);
            var panel = elements.Add(
                new CuiPanel
                {
                    Image = {Color = "0 0 0 0"}, CursorEnabled = false,
                    RectTransform = {AnchorMin = $"{x} {y}", AnchorMax = $"{x + 0.1f} {y + 0.05f}"}
                }, "Overlay", name);
            elements.Add(
                new CuiLabel
                {
                    Text = {Text = message, FontSize = 14, Align = TextAnchor.LowerLeft, Color = "1 1 1 0.7"},
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}
                }, name);
            CuiHelper.AddUi(player, elements);
            timer.In(0.5f, () => { CuiHelper.DestroyUi(player, name); });
        }

        private void ShowUICraft(BasePlayer player)
        {
            if (!craftImagesItited) return;
            CuiHelper.DestroyUi(player, "RecyclerUICraft");
            var elements = new CuiElementContainer();
            elements.Add(
                new CuiPanel
                {
                    Image = {FadeIn = 0.5f, Color = "0 0 0 0.8"}, CursorEnabled = true,
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}
                }, "Overlay", "RecyclerUICraft");
            elements.Add(new CuiElement
            {
                Parent = "RecyclerUICraft",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Color = "0 0 0 1", Material = "assets/content/ui/uibackgroundblur.mat",
                        Sprite = "assets/content/textures/generic/fulltransparent.tga"
                    },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });
            var panel = elements.Add(
                new CuiPanel
                {
                    Image = {Color = "1 1 1 0"}, CursorEnabled = true,
                    RectTransform = {AnchorMin = "0.2 0.25", AnchorMax = "0.2 0.25", OffsetMax = "700 400"}
                }, "RecyclerUICraft", "RecyclerUICraft_front");
            elements.Add(new CuiElement
            {
                Parent = "RecyclerUICraft_front",
                Components =
                {
                    new CuiTextComponent
                    {
                        Color = "1 1 1 1", FadeIn = 0.5f, Text = msg("UICraft", player), FontSize = 22,
                        Align = TextAnchor.UpperCenter
                    },
                    new CuiOutlineComponent {Distance = "1 1", Color = "0 0 0 1.0"},
                    new CuiRectTransformComponent {AnchorMin = "0 0.5", AnchorMax = "1 1"}
                }
            });
            var left = 0.5f - configData.itemsNeededToCraft.Count * 0.2f / 2f;
            var canAfford = true;
            foreach (var i in itemsNeededToCraft)
            {
                var image = (string) ImageLibrary?.Call("GetImage", i.Value.Key, 0UL) ?? "";
                if (image != "")
                    elements.Add(new CuiElement
                    {
                        Parent = "RecyclerUICraft_front",
                        Components =
                        {
                            new CuiRawImageComponent {Color = "1 1 1 1", FadeIn = 0.5f, Png = image},
                            new CuiRectTransformComponent
                                {AnchorMin = $"{left} 0.5", AnchorMax = $"{left} 0.5", OffsetMax = "100 100"}
                        }
                    });
                var amount = player.inventory.GetAmount(i.Key);
                var enough = amount < i.Value.Value ? msg("notEnough", player) : "";
                if (enough != "") canAfford = false;
                elements.Add(new CuiElement
                {
                    Parent = "RecyclerUICraft_front",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Color = "1 1 1 1", FadeIn = 0.5f, Text = $"{enough}\n{i.Value.Value}", FontSize = 14,
                            Align = TextAnchor.LowerCenter
                        },
                        new CuiOutlineComponent
                            {Distance = "1 1", Color = enough == "" ? "0 0 0 1" : "0.94 0.55 0.19 1.0"},
                        new CuiRectTransformComponent
                            {AnchorMin = $"{left} 0.5", AnchorMax = $"{left} 0.5", OffsetMax = "100 100"}
                    }
                });
                left += 0.2f;
            }

            if (canAfford)
                elements.Add(
                    new CuiButton
                    {
                        Button = {Command = "craftrecycler", Color = "0 1 0 0.5"},
                        Text =
                        {
                            Text = msg("UICraftYes", player), FontSize = 18, Color = "1 1 1 1",
                            Align = TextAnchor.MiddleCenter
                        },
                        RectTransform = {AnchorMin = "0.2 0.1", AnchorMax = "0.4 0.2"}
                    }, panel);
            elements.Add(
                new CuiButton
                {
                    Button = {Close = "RecyclerUICraft", Color = "1 0 0 0.5"},
                    Text =
                    {
                        Text = msg("UICraftNo", player), FontSize = 18, Color = "1 1 1 1",
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform = {AnchorMin = "0.6 0.1", AnchorMax = "0.8 0.2"}
                }, panel);
            CuiHelper.AddUi(player, elements);
        }

        private void ShowUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "RecyclerUIPickup");
            var elements = new CuiElementContainer();
            elements.Add(
                new CuiPanel
                {
                    Image = {FadeIn = 0.5f, Color = "0 0 0 0.8"}, CursorEnabled = true,
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}
                }, "Overlay", "RecyclerUIPickup");
            elements.Add(new CuiElement
            {
                Parent = "RecyclerUIPickup",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Color = "0 0 0 1", Material = "assets/content/ui/uibackgroundblur.mat",
                        Sprite = "assets/content/textures/generic/fulltransparent.tga"
                    },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });
            var panel = elements.Add(
                new CuiPanel
                {
                    Image = {Color = "0 0 0 0"}, CursorEnabled = true,
                    RectTransform = {AnchorMin = "0.35 0.4", AnchorMax = "0.65 0.6"}
                }, "RecyclerUIPickup", "RecyclerUIPickup_front");
            elements.Add(new CuiElement
            {
                Parent = "RecyclerUIPickup_front",
                Components =
                {
                    new CuiTextComponent
                    {
                        Color = "1 1 1 1", FadeIn = 0.5f, Text = msg("UIPickup", player), FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiOutlineComponent {Distance = "1 1", Color = "0 0 0 1.0"},
                    new CuiRectTransformComponent {AnchorMin = "0 0.5", AnchorMax = "1 1"}
                }
            });
            elements.Add(
                new CuiButton
                {
                    Button = {Command = "pickuprecycler", Color = "0 1 0 0.5"},
                    Text =
                    {
                        Text = msg("UIPickupYes", player), FontSize = 18, Color = "1 1 1 1",
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform = {AnchorMin = "0.1 0.1", AnchorMax = "0.45 0.45"}
                }, panel);
            elements.Add(
                new CuiButton
                {
                    Button = {Close = "RecyclerUIPickup", Color = "1 0 0 0.5"},
                    Text =
                    {
                        Text = msg("UIPickupNo", player), FontSize = 18, Color = "1 1 1 1",
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform = {AnchorMin = "0.55 0.1", AnchorMax = "0.9 0.45"}
                }, panel);
            CuiHelper.AddUi(player, elements);
        }

        private void cmdPickupRecycler(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player() ?? null;
            if (player == null) return;
            CuiHelper.DestroyUi(player, "RecyclerUIPickup");
            if (!pickipRecyclers.ContainsKey(player)) return;
            var rec2 = pickipRecyclers[player]?.gameObject?.GetComponent<Recycler>();
            pickipRecyclers.Remove(player);
            if (rec2 != null)
            {
                if (rec2.inventory.itemList.Count > 0)
                    rec2.inventory.Drop("assets/prefabs/misc/item drop/item_drop.prefab",
                        rec2.transform.position + new Vector3(0f, 1f, 0f), rec2.transform.rotation);
                rec2.Kill();
                if (GiveRecycler(player))
                    SendMsg(player, "recycler got");
                else
                    SendMsg(player, "inventory full");
            }
        }

        private void OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null || info.HitEntity == null) return;
            var rec = info.HitEntity.GetComponent<RecyclerEntity>();
            if (rec != null && rec.OwnerID != 0)
            {
                if (configData.allowRepair)
                    ShowUIHit(player, $"{info.HitEntity.Health()}/{info.HitEntity.MaxHealth()}");
                if (configData.allowPickupByHammerHit)
                {
                    if (pickipRecyclers.ContainsKey(player)) return;
                    if (configData.restrictUseByCupboard &&
                        (player.IsBuildingBlocked() || player.GetBuildingPrivilege() == null) &&
                        !player.IsBuildingAuthed())
                    {
                        SendMsg(player, "buldingBlocked");
                        return;
                    }

                    if (configData.pickupOnlyOwnerFriends &&
                        !(rec.OwnerID == player.userID ||
                          (bool) (Friends?.Call("AreFriends", rec.OwnerID, player.userID) ?? false)))
                    {
                        SendMsg(player, "cant pick");
                        return;
                    }

                    pickipRecyclers.Add(player, rec);
                    timer.In(30f, () =>
                    {
                        if (player == null) return;
                        if (pickipRecyclers.ContainsKey(player)) pickipRecyclers.Remove(player);
                    });
                    if (configData.allowRepair && info.HitEntity.Health() < info.HitEntity.MaxHealth())
                    {
                        SendMsg(player, "repair first");
                        return;
                    }

                    ShowUI(player);
                }
            }
        }

        private class RecyclerEntity : MonoBehaviour
        {
            private DestroyOnGroundMissing desGround;
            private GroundWatch groundWatch;
            public ulong OwnerID;

            private void Awake()
            {
                OwnerID = GetComponent<BaseEntity>().OwnerID;
                desGround = GetComponent<DestroyOnGroundMissing>();
                if (!desGround) gameObject.AddComponent<DestroyOnGroundMissing>();
                groundWatch = GetComponent<GroundWatch>();
                if (!groundWatch) gameObject.AddComponent<GroundWatch>();
            }
        }

        private class PluginData
        {
            public readonly Dictionary<ulong, double> userCooldowns = new Dictionary<ulong, double>();
            public readonly Dictionary<ulong, double> userCooldownsCraft = new Dictionary<ulong, double>();
            public readonly Dictionary<ulong, int> userCrafted = new Dictionary<ulong, int>();
            public readonly Dictionary<ulong, int> userSpawned = new Dictionary<ulong, int>();
        }

        private class ConfigData
        {
            public readonly bool adminSpawnsPublicRecycler = false;
            public readonly bool allowDamage = true;
            public readonly bool allowDeployOnGround = false;
            public readonly bool allowPickupByHammerHit = true;
            public readonly bool allowRepair = true;
            public readonly List<string> blackList = new List<string>();
            public readonly bool canChangePublicRecyclerParams = false;
            public readonly string chatCommand = "rec";
            public readonly string craftCommand = "craftrecycler";
            public readonly Rates DefaultRates = new Rates();
            public readonly float health = 500f;

            public readonly Dictionary<string, int> itemsNeededToCraft = new Dictionary<string, int>
                {{"scrap", 750}, {"gears", 25}, {"metalspring", 25}};

            public readonly List<Loot> Loot = new List<Loot>();
            public Dictionary<string, Rates> PermissionsRates = new Dictionary<string, Rates>();
            public readonly bool pickupOnlyOwnerFriends = true;

            public readonly string prefabToCloneDamage =
                "assets/prefabs/deployable/research table/researchtable_deployed.prefab";

            public readonly string recyclerItemName = "research.table";
            public readonly string recyclerShortPrefabName = "researchtable_deployed";
            public readonly bool restrictUseByCupboard = true;
            public readonly bool spawnInLoot = false;
            public readonly bool trackStacking = false;
            public readonly bool useCraftCooldown = true;
            public readonly bool useCrafting = false;
            public readonly bool useCraftLimit = false;
            public readonly bool useSpawnCooldown = true;
            public readonly bool useSpawning = false;
            public readonly bool useSpawnLimit = false;
            public readonly bool useUIForCrafting = true;
        }

        public class Loot
        {
            public string containerName;
            public int probability = 0;
        }

        private class Rates
        {
            public readonly float craftCooldown = 86400f;
            public readonly int craftLimit = 1;
            public readonly float percentOfMaxStackToTake = 0.1f;
            public int Priority = 1;
            public float Ratio = 0.5f;
            public readonly float RatioScrap = 1f;
            public readonly float spawnCooldown = 86400f;
            public readonly int spawnLimit = 1;
            public float Speed = 5f;
        }
    }
}