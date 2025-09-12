using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Ext.Chaos;
using Oxide.Ext.Chaos.Data;
using Oxide.Ext.Chaos.UIFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Chaos = Oxide.Ext.Chaos;
using Color = Oxide.Ext.Chaos.UIFramework.Color;

namespace Oxide.Plugins
{
    [Info("AbsolutSorter", "k1lly0u", "2.0.29")]
    [Description("Sort items from your inventory into designated storage containers with the click of a button")]
    class AbsolutSorter : ChaosPlugin
    {
        #region Fields
        private Datafile<StoredData> storedData;
        private DynamicConfigFile data;

        [PluginReference] Plugin NoEscape, SkinBox;
        
        private List<ItemCategory> itemCategories = new List<ItemCategory>();

        private bool wipeDetected;

        private List<ulong> hiddenPlayers = new List<ulong>();

        private const string SortingUI = "asui.sorting";

        [Chaos.Permission] private const string PermissionAllow = "absolutsorter.allow";
        [Chaos.Permission] private const string PermissionLootAll = "absolutsorter.lootall";
        [Chaos.Permission] private const string PermissionAllowDump = "absolutsorter.dumpall";
        [Chaos.Permission] private const string PermissionNearby = "absolutsorter.nearby";
        [Chaos.Permission] private const string PermissionNearbyCommand = "absolutsorter.nearby.command";

        private const int AllowedCategories = 210943;
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            storedData = new Datafile<StoredData>("absolutsorter");

            foreach (ItemCategory itemCategory in (ItemCategory[]) Enum.GetValues(typeof(ItemCategory)))
            {
                if ((AllowedCategories & (1 << (int)itemCategory)) != 0)
                    itemCategories.Add(itemCategory);
            }
        }

        private void OnServerInitialized()
        {
            CreateLocalization();
            
            m_CallbackHandler = new CommandCallbackHandler(this);
            
            if (wipeDetected)
            {
                storedData.Data.boxes.Clear();
                storedData.Save();
            }

            BroadcastInformation();
        }

        private void OnNewSave(string filename) => wipeDetected = true;

        private void OnServerSave() => storedData.Save();

        private void OnPlayerDisconnected(BasePlayer player) => ChaosUI.Destroy(player, SortingUI);

        private void OnPlayerRespawned(BasePlayer player) => ChaosUI.Destroy(player, SortingUI);

        private void OnLootEntity(BasePlayer player, ContainerIOEntity ioEntity) => OnLootEntityInternal(player, ioEntity);
        private void OnLootEntity(BasePlayer player, StorageContainer container) => OnLootEntityInternal(player, container);
        
        private void OnLootEntityInternal(BasePlayer player, BaseEntity container)
        {
            if (!player || !container.IsValid())
                return;

            if (!player.HasPermission(PermissionAllow))
                return;

            if (!Configuration.AllowedBoxes.Contains(container.ShortPrefabName))
                return;

            if (Configuration.RequirePrivilege)
            {
                if (IsVehicleMounted(container) && !IsVehicleAuthed(player, container))
                    return;
                
                if (!player.IsBuildingAuthed())
                    return;
            }
            
            if (Configuration.UseRaidBlocked && (NoEscape && (bool)NoEscape.Call("IsRaidBlocked", player)))
                return;

            NextTick(() =>
            {
                if (!player || !container || !container.IsValid())
                    return;

                if (SkinBox)
                {
                    object isSkinBoxPlayer = SkinBox.Call("IsSkinBoxPlayer", player.userID);
                    if (isSkinBoxPlayer is bool and true)
                        return;
                }

                CreateSortingPanel(player, container.net.ID);
            });
        }

        private void OnPlayerLootEnd(PlayerLoot loot)
        {
            BasePlayer player = loot.GetComponentInParent<BasePlayer>();
            if (player)
                ChaosUI.Destroy(player, SortingUI);
        }

        private void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity) => ChaosUI.Destroy(player, SortingUI);
        
        private void Unload()
        {
            if (!ServerMgr.Instance.Restarting)
                storedData.Save();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                ChaosUI.Destroy(player, SortingUI);
        }
        #endregion

        #region Functions
        private void BroadcastInformation()
        {
            if (Configuration.NotificationInterval == 0)
                return;

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                player.LocalizedMessage(this, "Global.Notification");

            timer.In(Configuration.NotificationInterval, BroadcastInformation);
        }

        private void ArrangeSort(BasePlayer player, NetworkableId id)
        {
            if (!player.inventory || !player.inventory.loot)
                return;
            
            IItemContainerEntity container = player.inventory.loot.entitySource as IItemContainerEntity;
            if (container == null)
                return;

            if (storedData.Data.IsSortingBox(id, out StoredData.BoxData data))
            {
                List<Item> items = Pool.Get<List<Item>>();
                items.AddRange(container.inventory.itemList);

                for (int i = items.Count - 1; i >= 0; i--)
                    items[i].RemoveFromContainer();

                container.inventory.itemList.Clear();               
               
                items.Sort(delegate (Item a, Item b)
                {
                    bool hasCategoryA = data.HasCategory(a.info.category);
                    bool hasCategoryB = data.HasCategory(b.info.category);

                    if (hasCategoryA && !hasCategoryB)
                        return -1;
                    if (!hasCategoryA && hasCategoryB)
                        return 1;
                    if (hasCategoryA && hasCategoryB)
                        return a.info.displayName.english.CompareTo(b.info.displayName.english);
                    return 1;
                });
                                
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    Item item = items[i];

                    if (Configuration.IsItemBlocked(item))
                        continue;
                    
                    if (!FindBestSlotForItem(container, item))
                        item.Drop(container.inventory.dropPosition, container.inventory.dropVelocity, Quaternion.identity);                    
                }
                                
                Pool.FreeUnmanaged(ref items);
            }
        }

        private void ThisSort(BasePlayer player, NetworkableId id)
        {
            if (!player.inventory || !player.inventory.loot)
                return;
            
            IItemContainerEntity container = player.inventory.loot.entitySource as IItemContainerEntity;
            BaseEntity entity = container as BaseEntity;
            if (container == null || !entity)
                return;

            List<Item> items = GetPlayerItems(player);
            int itemCount = items.Count;

            if (itemCount > 0)
            {
                storedData.Data.IsSortingBox(entity.net.ID, out StoredData.BoxData data);

                for (int i = items.Count - 1; i >= 0; i--)
                {
                    Item item = items[i];

                    if (Configuration.IsItemBlocked(item))
                        continue;
                    
                    if (StoredData.BoxData.AcceptsItem(data, item) && FindBestSlotForItem(container, item))
                        items.Remove(item);                    
                }

                if (items.Count == itemCount)
                    player.LocalizedMessage(this, "Error.NoThisItems");

            }
            Pool.FreeUnmanaged(ref items);
        }
        
        private void NearbySort(BasePlayer player)
        {
            List<Item> items = GetPlayerItems(player);

            int itemCount = items.Count;
            if (itemCount > 0)
            {
                List<IItemContainerEntity> list = Pool.Get<List<IItemContainerEntity>>();

                Vis.Entities(player.transform.position, Configuration.SortingRadius, list);

                list.Sort((a, b) 
                    => Vector3.Distance(player.transform.position, a.Transform.position).CompareTo(Vector3.Distance(player.transform.position, b.Transform.position)));

                int validSortingContainers = 0;

                for (int i = 0; i < list.Count; i++)
                {
                    IItemContainerEntity container = list[i];
                    BaseEntity entity = container as BaseEntity;

                    if (!entity)
                        continue;

                    if (Configuration.NearbySortOptions.FriendlyRequired && !IsFriendlySortContainer(player.userID, entity.OwnerID))
                        continue;
                    
                    if (!Configuration.AllowedBoxes.Contains(entity.ShortPrefabName))
                        continue;

                    if (storedData.Data.IsSortingBox(entity.net.ID, out StoredData.BoxData data))
                    {
                        validSortingContainers++;

                        for (int y = items.Count - 1; y >= 0; y--)
                        {
                            Item item = items[y];
                            
                            if (Configuration.IsItemBlocked(item))
                                continue;
                            
                            if (StoredData.BoxData.AcceptsItem(data, item) && FindBestSlotForItem(container, item))
                                items.Remove(item);                            
                        }
                    }

                    if (items.Count == 0)
                        break;
                }
                Pool.FreeUnmanaged(ref list);

                if (validSortingContainers == 0)
                    player.LocalizedMessage(this, "Error.NoNearbyContainers");

                else if (items.Count == itemCount)                
                    player.LocalizedMessage(this, "Error.NoNearbyItems");                            
                                  
            }
            Pool.FreeUnmanaged(ref items);
        }
        
        private bool IsFriendlySortContainer(ulong playerId, ulong ownerId)
        {
            if (playerId == ownerId)
                return true;

            if (Configuration.NearbySortOptions.Clans && Clans.IsClanMember(playerId, ownerId))
                return true;
            
            if (Configuration.NearbySortOptions.Friends && Friends.IsFriend(playerId, ownerId))
                return true;

            if (Configuration.NearbySortOptions.Team && (RelationshipManager.ServerInstance.FindPlayersTeam(ownerId)?.members?.Contains(playerId) ?? false))
                return true;

            return false;
        }

        private void DumpAll(BasePlayer player)
        {
            List<Item> items = GetPlayerItems(player);
            if (items.Count > 0)
            {
                if (player.inventory && player.inventory.loot)
                {
                    if (player.inventory.loot.entitySource is IItemContainerEntity container)
                    {
                        for (int i = items.Count - 1; i >= 0; i--)
                        {
                            Item item = items[i];

                            if (Configuration.IsItemBlocked(item))
                                continue;

                            FindBestSlotForItem(container, item);
                        }
                    }
                }
            }
            Pool.FreeUnmanaged(ref items);
        }

        private void LootAll(BasePlayer player)
        {
            if (!player.inventory || !player.inventory.loot)
                return;

            if (player.inventory.loot.entitySource is IItemContainerEntity container)
            {
                List<Item> items = container.inventory.itemList;

                for (int i = items.Count - 1; i >= 0; i--)
                {
                    Item item = items[i];
                    
                    if (Configuration.IsItemBlocked(item))
                        continue;
                    
                    if (!item.MoveToContainer(player.inventory.containerMain))
                    {
                        if (!item.MoveToContainer(player.inventory.containerBelt))
                        {
                            item.MoveToContainer(player.inventory.containerWear);
                        }
                    }
                }
            }
        }

        private readonly List<int> m_FindBestEmptySlots = new List<int>();

        private bool FindBestSlotForItem(IItemContainerEntity container, Item item)
        {
            m_FindBestEmptySlots.Clear();

            for (int i = 0; i < container.inventory.capacity; i++)
            {
                Item slot = container.inventory.GetSlot(i);
                if (slot != null && slot.info == item.info && slot.CanStack(item))
                {
                    int freeSpace = slot.MaxStackable() - slot.amount;
                    if (freeSpace <= 0)
                        continue;

                    if (freeSpace >= item.amount)
                    {
                        if (item.MoveToContainer(container.inventory, i))
                            return true;
                    }
                    else
                    {
                        item.amount -= freeSpace;
                        slot.amount += freeSpace;

                        item.MarkDirty();
                        slot.MarkDirty();
                    }
                }

                if (slot == null && container.inventory.canAcceptItem(item, i))
                    m_FindBestEmptySlots.Add(i);
            }

            for (int i = 0; i < m_FindBestEmptySlots.Count; i++)
            {
                if (item.MoveToContainer(container.inventory, m_FindBestEmptySlots[i]))
                    return true;
            }

            return false;
        }

        private List<Item> GetPlayerItems(BasePlayer player)
        {
            List<Item> items = Pool.Get<List<Item>>();

            items.AddRange(player.inventory.containerMain.itemList);

            if (Configuration.IncludeBelt)
                items.AddRange(player.inventory.containerBelt.itemList);

            return items;
        }

        private bool IsHidden(BasePlayer player) => hiddenPlayers.Contains(player.userID.Get());

        private bool IsVehicleMounted(BaseEntity container)
        {
            if (!container.HasParent())
                return false;

            return container.GetParentEntity() is BaseVehicle;
        }

        private bool IsVehicleAuthed(BasePlayer player, BaseEntity container)
        {
            if (!container.HasParent())
                return false;
            
            BaseVehicle baseVehicle = container.GetParentEntity() as BaseVehicle;
            
            return baseVehicle && baseVehicle.IsAuthed(player);
        }
        #endregion

        #region UI

        private Color m_ButtonColor = new Color(1f, 1f, 1f, 0.2f);

        private Color m_ButtonGreen = new Color(0.4509804f, 0.5529412f, 0.2705882f, 0.7607843f);

        private Color m_ButtonRed = new Color(0.8078431f, 0.2588235f, 0.1686275f, 0.7607843f);

        private GridLayoutGroup m_LayoutGrid = new GridLayoutGroup(3, 3, Axis.Horizontal)
        {
            Area = new Area(-121f, -31.5f, 121f, 31.5f),
            Spacing = new Spacing(5f, 5f),
            Padding = new Padding(0f, 5f, 0f, 0f),
            Corner = Corner.TopLeft
        };

        private CommandCallbackHandler m_CallbackHandler;
        
        private void CreateSortingPanel(BasePlayer player, NetworkableId id)
        {
            if (!player)
                return;

            BaseContainer root = BaseContainer.Create(SortingUI, Layer.Overlay, Anchor.FullStretch, new Offset(0f, 16f, -16f, -16f))
                .WithChildren(rootContainer =>
                {
                    BaseContainer.Create(rootContainer, Anchor.BottomStretch, new Offset(0f, 0f, 0f, 64f))
                        .WithChildren(bottom =>
                        {
                            BaseContainer.Create(bottom, Anchor.BottomCenter, new Offset(-190f, 0f, 190f, 64f))
                                .WithChildren(beltBar =>
                                {
                                    BaseContainer.Create(beltBar, Anchor.CenterRight, new Offset(3.999992f + Configuration.Offset.Horizontal, -32f + Configuration.Offset.Vertical, 246f + Configuration.Offset.Horizontal, 50f  + Configuration.Offset.Vertical))
                                        .WithChildren(parent =>
                                        {
                                            if (IsHidden(player))
                                            {
                                                ButtonContainer.Create(parent, Anchor.TopLeft, new Offset(0f, -18f, 118.5f, 0f))
                                                    .WithColor(m_ButtonColor)
                                                    .WithCallback(m_CallbackHandler, arg =>
                                                    {
                                                        hiddenPlayers.Remove(player.userID.Get());
                                                        CreateSortingPanel(player, id);
                                                    }, $"{player.UserIDString}.togglevisible")
                                                    .WithChildren(showSorter =>
                                                    {
                                                        TextContainer.Create(showSorter, Anchor.FullStretch, Offset.zero)
                                                            .WithSize(10)
                                                            .WithText(GetString("UI.Sort.Show", player))
                                                            .WithAlignment(TextAnchor.MiddleCenter);
                                                    });
                                            }
                                            else
                                            {
                                                storedData.Data.boxes.TryGetValue(id.Value, out StoredData.BoxData data);

                                                ButtonContainer.Create(parent, Anchor.TopLeft, new Offset(0f, -18f, 118.5f, 0f))
                                                    .WithColor(m_ButtonColor)
                                                    .WithCallback(m_CallbackHandler, arg =>
                                                    {
                                                        if (data == null)
                                                            data = storedData.Data.boxes[id.Value] = new StoredData.BoxData();

                                                        OpenSelector(player, id, itemCategories,
                                                            (boxData, category) => boxData.HasCategory(category),
                                                            (category, p) => GetString($"ItemCategory.{category}", p),
                                                            (boxData, category) => boxData.ToggleCategory(category));
                                                        
                                                    }, $"{player.UserIDString}.selectcategories")
                                                    .WithChildren(selectCategories =>
                                                    {
                                                        TextContainer.Create(selectCategories, Anchor.FullStretch, Offset.zero)
                                                            .WithSize(10)
                                                            .WithText(GetString("UI.SelectCategories", player))
                                                            .WithAlignment(TextAnchor.MiddleCenter);
                                                    });

                                                if (data?.HasAnyCategory() ?? false)
                                                {
                                                    ButtonContainer.Create(parent, Anchor.TopLeft, new Offset(123.5f, -18f, 242f, 0f))
                                                        .WithColor(m_ButtonColor)
                                                        .WithCallback(m_CallbackHandler, arg =>
                                                                OpenSelector(player, id,
                                                                    ItemManager.itemList.Where(x => !x.hidden && data.HasCategory(x.category)),
                                                                    (boxData, itemDefinition) => boxData.HasItem(itemDefinition.shortname),
                                                                    (itemDefinition, p) => GetString($"Item.{itemDefinition.shortname}", p),
                                                                    (boxData, itemDefinition) => boxData.ToggleItem(itemDefinition.shortname)),
                                                            $"{player.UserIDString}.selectitems")
                                                        .WithChildren(selectItems =>
                                                        {
                                                            TextContainer.Create(selectItems, Anchor.FullStretch, Offset.zero)
                                                                .WithSize(10)
                                                                .WithText(GetString("UI.SelectItems", player))
                                                                .WithAlignment(TextAnchor.MiddleCenter);
                                                        });
                                                }

                                                int itemCount = player.inventory.containerMain.itemList.Count;
                                                if (Configuration.IncludeBelt)
                                                    itemCount += player.inventory.containerBelt.itemList.Count;

                                                if (itemCount > 0)
                                                {
                                                    ButtonContainer.Create(parent, Anchor.TopLeft, new Offset(0f, -39f, 78f, -21f))
                                                        .WithColor(m_ButtonColor)
                                                        .WithCallback(m_CallbackHandler, arg =>
                                                        {
                                                            ThisSort(player, id);
                                                            CreateSortingPanel(player, id);
                                                        }, $"{player.UserIDString}.thissort")
                                                        .WithChildren(thisSort =>
                                                        {
                                                            TextContainer.Create(thisSort, Anchor.FullStretch, Offset.zero)
                                                                .WithSize(10)
                                                                .WithText(GetString("UI.Sort.This", player))
                                                                .WithAlignment(TextAnchor.MiddleCenter);
                                                        });

                                                    if (!Configuration.UseNearbySortPerm || (Configuration.UseNearbySortPerm && player.HasPermission(PermissionNearby)))
                                                    {
                                                        ButtonContainer.Create(parent, Anchor.TopCenter, new Offset(-39f, -39f, 39f, -21f))
                                                            .WithColor(m_ButtonColor)
                                                            .WithCallback(m_CallbackHandler, arg =>
                                                            {
                                                                NearbySort(player);
                                                                CreateSortingPanel(player, id);
                                                            }, $"{player.UserIDString}.nearbysort")
                                                            .WithChildren(nearbySort =>
                                                            {
                                                                TextContainer.Create(nearbySort, Anchor.FullStretch, Offset.zero)
                                                                    .WithSize(10)
                                                                    .WithText(GetString("UI.Sort.Nearby", player))
                                                                    .WithAlignment(TextAnchor.MiddleCenter);
                                                            });
                                                    }
                                                }

                                                if (data != null)
                                                {
                                                    ButtonContainer.Create(parent, Anchor.TopRight, new Offset(-78f, -39f, 0f, -21f))
                                                        .WithColor(m_ButtonColor)
                                                        .WithCallback(m_CallbackHandler, arg =>
                                                        {
                                                            ArrangeSort(player, id);
                                                            CreateSortingPanel(player, id);
                                                        }, $"{player.UserIDString}.arrangesort")
                                                        .WithChildren(arrangeSort =>
                                                        {
                                                            TextContainer.Create(arrangeSort, Anchor.FullStretch, Offset.zero)
                                                                .WithSize(10)
                                                                .WithText(GetString("UI.Sort.Arrange", player))
                                                                .WithAlignment(TextAnchor.MiddleCenter);
                                                        });
                                                }

                                                if (player.HasPermission(PermissionAllowDump) && itemCount > 0)
                                                {
                                                    ButtonContainer.Create(parent, Anchor.TopLeft, new Offset(7.629395E-06f, -60f, 78.00001f, -42f))
                                                        .WithColor(m_ButtonColor)
                                                        .WithCallback(m_CallbackHandler, arg =>
                                                        {
                                                            DumpAll(player);
                                                            CreateSortingPanel(player, id);
                                                        }, $"{player.UserIDString}.dumpsort")
                                                        .WithChildren(dumpAll =>
                                                        {
                                                            TextContainer.Create(dumpAll, Anchor.FullStretch, Offset.zero)
                                                                .WithSize(10)
                                                                .WithText(GetString("UI.Sort.Dump", player))
                                                                .WithAlignment(TextAnchor.MiddleCenter);
                                                        });
                                                }

                                                if (player.HasPermission(PermissionLootAll) && (player.inventory?.loot?.entitySource as IItemContainerEntity)?.inventory?.itemList?.Count > 0)
                                                {
                                                    ButtonContainer.Create(parent, Anchor.TopLeft, new Offset(81.9985f, -60f, 160.0015f, -42f))
                                                        .WithColor(m_ButtonColor)
                                                        .WithCallback(m_CallbackHandler, arg =>
                                                        {
                                                            LootAll(player);
                                                            CreateSortingPanel(player, id);
                                                        }, $"{player.UserIDString}.takesort")
                                                        .WithChildren(takeAll =>
                                                        {
                                                            TextContainer.Create(takeAll, Anchor.FullStretch, Offset.zero)
                                                                .WithSize(10)
                                                                .WithText(GetString("UI.Sort.Take", player))
                                                                .WithAlignment(TextAnchor.MiddleCenter);
                                                        });
                                                }

                                                ButtonContainer.Create(parent, Anchor.BottomLeft, new Offset(0f, 1f, 78f, 19f))
                                                    .WithColor(m_ButtonGreen)
                                                    .WithCallback(m_CallbackHandler, arg =>
                                                    {
                                                        player.inventory.loot.Clear();
                                                        SendHelpText(player);
                                                    }, $"{player.UserIDString}.help")
                                                    .WithChildren(help =>
                                                    {
                                                        TextContainer.Create(help, Anchor.FullStretch, Offset.zero)
                                                            .WithSize(10)
                                                            .WithText(GetString("UI.Sort.Help", player))
                                                            .WithAlignment(TextAnchor.MiddleCenter);
                                                    });

                                                if (data != null)
                                                {
                                                    ButtonContainer.Create(parent, Anchor.BottomCenter, new Offset(-39.00153f, 1f, 38.99847f, 19f))
                                                        .WithColor(m_ButtonRed)
                                                        .WithCallback(m_CallbackHandler, arg =>
                                                        {
                                                            storedData.Data.RemoveSortingBox(id);
                                                            CreateSortingPanel(player, id);
                                                        }, $"{player.UserIDString}.remove")
                                                        .WithChildren(remove =>
                                                        {
                                                            TextContainer.Create(remove, Anchor.FullStretch, Offset.zero)
                                                                .WithSize(10)
                                                                .WithText(GetString("UI.Sort.Reset", player))
                                                                .WithAlignment(TextAnchor.MiddleCenter);
                                                        });
                                                }

                                                ButtonContainer.Create(parent, Anchor.BottomCenter, new Offset(43f, 1f, 121f, 19f))
                                                    .WithColor(m_ButtonRed)
                                                    .WithCallback(m_CallbackHandler, arg =>
                                                    {
                                                        if (IsHidden(player))
                                                            hiddenPlayers.Remove(player.userID);
                                                        else hiddenPlayers.Add(player.userID);

                                                        CreateSortingPanel(player, id);
                                                    }, $"{player.UserIDString}.hide")
                                                    .WithChildren(hide =>
                                                    {
                                                        TextContainer.Create(hide, Anchor.FullStretch, Offset.zero)
                                                            .WithSize(10)//709
                                                            .WithText(GetString("UI.Sort.Hide", player))
                                                            .WithAlignment(TextAnchor.MiddleCenter);
                                                    });

                                            }
                                        });
                                });
                        });
                })
                .DestroyExisting();

            ChaosUI.Show(player, root);
        }

        private void OpenSelector<T>(BasePlayer player, NetworkableId id, IEnumerable<T> collection, Func<StoredData.BoxData, T, bool> isSelected, Func<T, BasePlayer, string> toName, Action<StoredData.BoxData, T> onToggle, int page = 0)
        {
            storedData.Data.IsSortingBox(id, out StoredData.BoxData data);
            
            BaseContainer root = BaseContainer.Create(SortingUI, Layer.Overlay, Anchor.FullStretch, new Offset(0f, 16f, -16f, -16f))
                .WithChildren(asuiSelector =>
                {
                    BaseContainer.Create(asuiSelector, Anchor.BottomStretch, new Offset(0f, 0f, 0f, 64f))
                        .WithChildren(bottom =>
                        {
                            BaseContainer.Create(bottom, Anchor.BottomCenter, new Offset(-190f, 0f, 190f, 64f))
                                .WithChildren(beltBar =>
                                {
                                    BaseContainer.Create(beltBar, Anchor.CenterRight, new Offset(3.999992f + Configuration.Offset.Horizontal, -32f + Configuration.Offset.Vertical, 246f + Configuration.Offset.Horizontal, 50f  + Configuration.Offset.Vertical))
                                        .WithChildren(panel =>
                                        {
                                            BaseContainer.Create(panel, Anchor.FullStretch, new Offset(0f, 19f, 0f, 0f))
                                                .WithLayoutGroup(m_LayoutGrid, collection, Mathf.Clamp(page, 0, Mathf.CeilToInt(collection.Count() / m_LayoutGrid.PerPage)), (i, t, layout, anchor, offset) =>
                                                {
                                                    string name = toName(t, player);
                                                    ButtonContainer.Create(layout, anchor, offset)
                                                        .WithColor(isSelected(data, t) ? m_ButtonGreen : m_ButtonColor)
                                                        .WithCallback(m_CallbackHandler, arg =>
                                                        {
                                                            onToggle(data, t);
                                                            OpenSelector(player, id, collection, isSelected, toName, onToggle, page);
                                                        }, $"{player.UserIDString}.toggle.{name}")
                                                        .WithChildren(template =>
                                                        {
                                                            TextContainer.Create(template, Anchor.FullStretch, Offset.zero)
                                                                .WithSize(10)
                                                                .WithText(name)
                                                                .WithAlignment(TextAnchor.MiddleCenter);
                                                        });
                                                });

                                            ButtonContainer.Create(panel, Anchor.BottomLeft, new Offset(0f, 1f, 78f, 19f))
                                                .WithColor(m_ButtonGreen)
                                                .WithCallback(m_CallbackHandler, arg => OpenSelector(player, id, collection, isSelected, toName, onToggle, page - 1), $"{player.UserIDString}.back")
                                                .WithChildren(back =>
                                                {
                                                    TextContainer.Create(back, Anchor.FullStretch, Offset.zero)
                                                        .WithSize(10)
                                                        .WithText(GetString("UI.PreviousPage", player))
                                                        .WithAlignment(TextAnchor.MiddleCenter);
                                                });

                                            ButtonContainer.Create(panel, Anchor.BottomCenter, new Offset(-39.00153f, 1f, 38.99847f, 19f))
                                                .WithColor(m_ButtonRed)
                                                .WithCallback(m_CallbackHandler, arg => CreateSortingPanel(player, id), $"{player.UserIDString}.return")
                                                .WithChildren(close =>
                                                {
                                                    TextContainer.Create(close, Anchor.FullStretch, Offset.zero)
                                                        .WithSize(10)
                                                        .WithText(GetString("UI.Return", player))
                                                        .WithAlignment(TextAnchor.MiddleCenter);
                                                });

                                            ButtonContainer.Create(panel, Anchor.BottomCenter, new Offset(43f, 1f, 121f, 19f))
                                                .WithColor(m_ButtonGreen)
                                                .WithCallback(m_CallbackHandler, arg => OpenSelector(player, id, collection, isSelected, toName, onToggle, page + 1), $"{player.UserIDString}.next")
                                                .WithChildren(next =>
                                                {
                                                    TextContainer.Create(next, Anchor.FullStretch, Offset.zero)
                                                        .WithSize(10)
                                                        .WithText(GetString("UI.NextPage", player))
                                                        .WithAlignment(TextAnchor.MiddleCenter);
                                                });

                                        });
                                });
                        });
                })
                .DestroyExisting();

            ChaosUI.Show(player, root);
        }
        #endregion

        #region Commands
        [ChatCommand("sorthelp")]
        private void cmdSortHelp(BasePlayer player, string command, string[] args) => SendHelpText(player);

        [ChatCommand("whatisthis")]
        private void cmdWhatIsThis(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
                return;

            if (Physics.Raycast(player.eyes.HeadRay(), out RaycastHit raycastHit, 3f, 1 << 0 | 1 << 8 | 1 << 16 | 1 << 21, QueryTriggerInteraction.Ignore))//709
            {
                BaseEntity baseEntity = raycastHit.collider.ToBaseEntity();
                if (baseEntity)
                {
                    SendReply(player, $"Entity: {baseEntity.ShortPrefabName}");
                    return;
                }
            }

            SendReply(player, "No entity found");
        }
        
        [ChatCommand("sort.nearby")]
        private void ChatCommandNearbySort(BasePlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PermissionNearbyCommand))
            {
                player.LocalizedMessage(this, "Error.NoPermission");
                return;
            }
            
            NearbySort(player);
        }
        [ConsoleCommand("sort.nearby")]
        private void ConsoleCommandNearbySort(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (!player)
                return;

            if (!player.HasPermission(PermissionNearbyCommand))
            {
                SendReply(arg, lang.GetMessage("Error.NoPermission", this, player.UserIDString));
                return;
            }
            
            NearbySort(player);
        }

        private void SendHelpText(BasePlayer player)
        {
            SendReply(player, $"<color=#ce422b>{Title}</color> v<color=#ce422b>{Version}</color>\nYou can view this help at anytime by typing <color=#ce422b>/sorthelp</color>");

            string str = GetString("Help.Options.1", player);
            str += GetString("Help.Options.2", player);
            str += GetString("Help.Options.3", player);
            str += GetString("Help.Options.4", player);
            str += GetString("Help.Options.5", player);
            str = GetString("Help.Sorting.1", player);
            str += GetString("Help.Sorting.2", player);
            str += FormatString("Help.Sorting.3", player, Configuration.SortingRadius);
            str += GetString("Help.Sorting.4", player);
            str += GetString("Help.Sorting.5", player);
            str += GetString("Help.Sorting.6", player);
            str += GetString("Help.Sorting.7", player);
            player.ChatMessage(str);
        }
        #endregion
        
        #region Config        
        private ConfigData Configuration => ConfigurationData as ConfigData;
        
        private class ConfigData : BaseConfigData
        {
            [JsonProperty(PropertyName = "Allowed containers (short prefab name)")]
            public string[] AllowedBoxes { get; set; }
            
            [JsonProperty(PropertyName = "Items to block from any sorting function (item shortname)")]
            public string[] BlockedItems { get; set; }
            
            [JsonProperty(PropertyName = "Skins to block from any sorting function")]
            public ulong[] BlockedSkins { get; set; }

            [JsonProperty(PropertyName = "Sorting radius")]
            public float SortingRadius { get; set; }

            [JsonProperty(PropertyName = "Include hotbar items when sorting")]
            public bool IncludeBelt { get; set; }

            [JsonProperty(PropertyName = "Require building privilege to use sorting")]
            public bool RequirePrivilege { get; set; }

            [JsonProperty(PropertyName = "Help notification interval (seconds, set to 0 to disable)")]
            public int NotificationInterval { get; set; }

            [JsonProperty(PropertyName = "Disable sorting functionality if raid blocked")]
            public bool UseRaidBlocked { get; set; }

            [JsonProperty(PropertyName = "Use permission to allow nearby sorting")]
            public bool UseNearbySortPerm { get; set; }

            [JsonProperty(PropertyName = "Valid nearby sorting options")]
            public NearbyOptions NearbySortOptions { get; set; } = new NearbyOptions();
            
            [JsonProperty(PropertyName = "UI Offset")]
            public UIOffest Offset { get; set; }

            public class NearbyOptions
            {
                [JsonProperty(PropertyName = "Only nearby sort into friendly containers")]
                public bool FriendlyRequired { get; set; } = true;
                
                [JsonProperty(PropertyName = "Allow sort into clan members containers")]
                public bool Clans { get; set; } = true;
                
                [JsonProperty(PropertyName = "Allow sort into friends containers")]
                public bool Friends { get; set; }
                
                [JsonProperty(PropertyName = "Allow sort into team member containers")]
                public bool Team { get; set; } = true;
            }
            public class UIOffest
            {
                public float Horizontal { get; set; }
                public float Vertical { get; set; }
            }

            public bool IsItemBlocked(Item item) => BlockedItems.Contains(item.info.shortname) || BlockedSkins.Contains(item.skin);
        }

        protected override ConfigurationFile OnLoadConfig(ref ConfigurationFile configurationFile) => configurationFile = new ConfigurationFile<ConfigData>(Config);

        protected override T GenerateDefaultConfiguration<T>()
        {
            return new ConfigData
            {
                AllowedBoxes = new[]
                {
                    "campfire",
                    "furnace",
                    "woodbox_deployed",
                    "box.wooden.large",
                    "small_stash_deployed",
                    "fridge.deployed",
                    "coffinstorage"
                },
                BlockedItems = new string[0],
                BlockedSkins = new ulong[0],
                IncludeBelt = false,
                RequirePrivilege = true,
                SortingRadius = 30f,
                NotificationInterval = 1800,
                UseRaidBlocked = true,
                UseNearbySortPerm = false,
                Offset = new ConfigData.UIOffest
                {
                    Horizontal = 0,
                    Vertical = 0
                }
            } as T;
        }

        protected override void OnConfigurationUpdated(VersionNumber oldVersion)
        {
            ConfigData baseConfigData = GenerateDefaultConfiguration<ConfigData>();

            if (Configuration.Version < new VersionNumber(2, 0, 0))
                ConfigurationData = baseConfigData;

            if (Configuration.Version < new VersionNumber(2, 0, 4))
                Configuration.UseRaidBlocked = baseConfigData.UseRaidBlocked;

            if (Configuration.Version < new VersionNumber(2, 0, 21))
            {
                Configuration.BlockedItems = baseConfigData.BlockedItems;
                Configuration.BlockedSkins = baseConfigData.BlockedSkins;
                Configuration.Offset = baseConfigData.Offset;
            }
        }

        #endregion

        #region Data Management
        internal class StoredData
        {
            public Hash<ulong, BoxData> boxes = new Hash<ulong, BoxData>();

            public BoxData CreateSortingBox(ulong id)
            {
                BoxData data = new BoxData();

                boxes.Add(id, data);

                return data;
            }

            public bool IsSortingBox(NetworkableId id, out BoxData data) => boxes.TryGetValue(id.Value, out data);
            
            public void RemoveSortingBox(NetworkableId id) => boxes.Remove(id.Value);

            public class BoxData
            {
                public int categories;

                public List<string> allowedItems = new List<string>();

                public void ToggleCategory(ItemCategory category)
                {
                    if (HasCategory(category))
                        RemoveCategory(category);
                    else AddCategory(category);
                }
                
                private void AddCategory(ItemCategory category)
                {
                    categories |= (1 << (int)category);
                }

                private void RemoveCategory(ItemCategory category)
                {
                    categories &= ~(1 << (int)category);

                    if (allowedItems.Count > 0)
                    {
                        for (int i = allowedItems.Count - 1; i >= 0; i--)
                        {
                            ItemDefinition itemDefinition = ItemManager.FindItemDefinition(allowedItems[i]);
                            if (itemDefinition && itemDefinition.category == category)
                                allowedItems.RemoveAt(i);
                        }
                    }
                }

                public bool HasCategory(ItemCategory category)
                {
                    return ((categories & (1 << (int)category)) != 0);
                }

                public bool HasAnyCategory() => categories != 0;

                public bool AcceptsItem(ItemDefinition itemDefinition)
                {
                    if (allowedItems.Count == 0 && categories == 0)
                        return true;

                    if (allowedItems.Contains(itemDefinition.shortname) && HasCategory(itemDefinition.category))
                        return true;

                    if (allowedItems.Count == 0 && HasCategory(itemDefinition.category))
                        return true;

                    return false;
                }

                public static bool AcceptsItem(BoxData data, Item item)
                {
                    if (data == null)
                        return true;

                    return data.AcceptsItem(item.info);
                }

                public void ToggleItem(string shortname)
                {
                    if (HasItem(shortname))
                        allowedItems.Remove(shortname);
                    else allowedItems.Add(shortname);
                }

                public bool HasItem(string shortname) => allowedItems.Contains(shortname);
            }
        }
        #endregion

        #region Localization

        private void CreateLocalization()
        {
            m_Messages = new Dictionary<string, string>()
            {
                ["Help.Options.1"] = "<color=#ce422b>Sorting Preferences</color>;\n\n",
                ["Help.Options.2"] = "To begin, press the 'Select Categories' button to select which item categories are allowed in this box\n\n",
                ["Help.Options.3"] = "By selecting categories only all items in those categories will be accepted\n\n",
                ["Help.Options.4"] = "To pick individual items press the 'Select Items' button and pick the items from the list\n\n",
                ["Help.Options.5"] = "When using the 'This' or 'Nearby' functions the boxes will only take items that meet these requirements",
                ["Help.Sorting.1"] = "<color=#ce422b>Sorting Options</color>;\n\n",
                ["Help.Sorting.2"] = "<color=#ce422b>This</color> - Will move items from your inventory to the box if the item meets the set requirements\n\n",
                ["Help.Sorting.3"] = "<color=#ce422b>Nearby</color> - Will move items from your inventory to boxes to boxes within a {0} radius if the item meets the set requirements\n\n",
                ["Help.Sorting.4"] = "<color=#ce422b>Arrange Box</color> - Will arrange all items in the box with the selected categories/items listed first\n\n",
                ["Help.Sorting.5"] = "<color=#ce422b>Dump All</color> - Empty your inventory into the box regardless of the accepted items\n\n",
                ["Help.Sorting.6"] = "<color=#ce422b>Loot All</color> - Empty the box into your inventory\n\n",
                ["Help.Sorting.7"] = "<color=#ce422b>Remove sorter from container</color> - Removes all sorting settings from this box",
                ["UI.SelectCategories"] = "Select Categories",
                ["UI.SelectItems"] = "Select Items",
                ["UI.SortingOptions"] = "Sorting Options",
                ["UI.Sort.This"] = "This",
                ["UI.Sort.Nearby"] = "Nearby",
                ["UI.Sort.Arrange"] = "Arrange Box",
                ["UI.Sort.Dump"] = "Dump All",
                ["UI.Sort.Take"] = "Loot All",
                ["UI.Sort.Help"] = "Help",
                ["UI.Sort.Reset"] = "Reset",
                ["UI.Sort.Hide"] = "Hide",
                ["UI.Sort.Show"] = "Show",
                ["UI.Return"] = "Return",
                ["UI.NextPage"] = ">>>",
                ["UI.PreviousPage"] = "<<<",
                ["Error.NoNearbyContainers"] = "No nearby containers have sorting options defined",
                ["Error.NoPermission"] = "You do not have permission to use this command",
                ["Error.NoNearbyItems"] = "No items in your inventory match the sorting options defined for nearby containers",
                ["Error.NoThisItems"] = "No items in your inventory match the sorting options defined for this container",
                ["Global.Notification"] = "This server is running <color=#ce422b>AbsolutSorter</color>! Type <color=#ce422b>/sorthelp</color> for information on how to use it",
            };

            foreach (ItemCategory category in (ItemCategory[])Enum.GetValues(typeof(ItemCategory)))
            {
                m_Messages["ItemCategory." + category] = category.ToString();
            }

            foreach (ItemDefinition itemDefinition in ItemManager.itemList)
            {
                m_Messages["Item." + itemDefinition.shortname] = itemDefinition.displayName.english.Replace("High Quality", "HQ").Replace("Medium Quality", "MQ").Replace("Low Quality", "LQ");
            }
            
            lang.RegisterMessages(m_Messages, this);
        }
        protected override void PopulatePhrases()
        {
        }
        #endregion
    }
}
