using System;
using System.Collections.Generic;
using ConVar;
using Facepunch.Rust;
using Pool = Facepunch.Pool;
using UnityEngine;
// Developed by Khan Discord ID @khan8615
/*
 * Update 1.0.1
 * Removed rotation & demolition limits after a building block is placed it now can always be rotated or demolished after a single hit from the hammer or tool-gun.
 */
namespace Oxide.Plugins
{
    [Info("BetterHammer", "Khan", "1.0.1")]
    [Description("Modifies the hammer & tool-gun so you can do full repairs instead of partial + always do rotation & demolition")]
    public class BetterHammer : RustPlugin
    {
        #region License Agreement (EULA) of Better Hammer

        /*
        End-User License Agreement (EULA) of Better Hammer

        This End-User License Agreement ("EULA") is a legal agreement between you and Kyle. This EULA agreement
        governs your acquisition and use of our Better Hammer plugin ("Software") directly from Kyle.

        Please read this EULA agreement carefully before downloading and using the Better Hammer plugin.
        It provides a license to use the Better Hammer and contains warranty information and liability disclaimers.

        If you are entering into this EULA agreement on behalf of a company or other legal entity, you represent that you have the authority
        to bind such entity and its affiliates to these terms and conditions. If you do not have such authority or if you do not agree with the
        terms and conditions of this EULA agreement, DO NOT purchase or download the Software.

        This EULA agreement shall apply only to the Software supplied by Kyle Farris regardless of whether other software is referred
        to or described herein. The terms also apply to any Kyle updates, supplements, Internet-based services, and support services for the Software,
        unless other terms accompany those items on delivery. If so, those terms apply.

        License Grant

        Kyle hereby grants you a personal, non-transferable, non-exclusive license to use the Better Hammer software on your devices in
        accordance with the terms of this EULA agreement. You are permitted to load the Better Hammer on your personal server owned by you.

        You are not permitted to:

        Edit, alter, modify, adapt, translate or otherwise change the whole or any part of the Software nor permit the whole or any part
        of the Software to be combined with or become incorporated in any other software, nor decompile, disassemble or reverse
        engineer the Software or attempt to do any such things.
        Reproduce, copy, distribute, resell or otherwise use the Software for any commercial purpose
        Allow any third party to use the Software on behalf of or for the benefit of any third party
        Use the Software in any way which breaches any applicable local, national or international law
        use the Software for any purpose that Kyle considers is a breach of this EULA agreement

        Intellectual Property and Ownership

        Kyle shall at all times retain ownership of the Software as originally downloaded by you and all subsequent downloads of the Software by you. 
        The Software (and the copyright, and other intellectual property rights of whatever nature in the Software, including any modifications made thereto) are and shall remain the property of Kyle.

        Termination

        This EULA agreement is effective from the date you first use the Software and shall continue until terminated. 
        You may terminate it at any time upon written notice to Kyle.
        It will also terminate immediately if you fail to comply with any term of this EULA agreement. 
        Upon such termination, the licenses granted by this EULA agreement will immediately terminate and you agree to stop all access and use of the Software. 
        The provisions that by their nature continue and survive will survive any termination of this EULA agreement.
        */

        #endregion

        #region Fields

        private const string Use = "betterhammer.use";

        #endregion

        #region Oxide Hooks

        private void Init() => permission.RegisterPermission(Use, this);

        private object OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
        {
            if (!(bool)player?.GetHeldEntity()?.isBuildingTool || !permission.UserHasPermission(player.UserIDString, Use))
                return null;

            DoRepair(entity, player);
            return false;
        }

        #endregion

        #region Helpers

        private void DoRepair(BaseCombatEntity entity, BasePlayer player)
        {
            float attackCoolDown = player.IsInCreativeMode && Creative.freeRepair ? 0.0f : 30f;

            if ((double)entity.SecondsSinceAttacked <= (double)attackCoolDown)
            {
                entity.OnRepairFailed(player, BaseCombatEntity.RecentlyDamagedError, (attackCoolDown - entity.SecondsSinceAttacked).ToString("N0"));
                return;
            }

            bool check = entity is BuildingBlock;
            if (check)
            {
                BuildingBlock block = entity as BuildingBlock;
                if (block.blockDefinition.canRotateAfterPlacement)
                    block.StartBeingRotatable();

                block.StartBeingDemolishable();
            }

            float max = entity.MaxHealth();
            float damage = max - entity.Health();
            if ((double)damage <= 0.0)
            {
                if (check)
                {
                    Effect.server.Run(entity.repair.repairFailedEffect.isValid ? entity.repair.repairFailedEffect.resourcePath : "assets/bundled/prefabs/fx/build/repair_failed.prefab", (BaseEntity) entity, posLocal: Vector3.zero, normLocal: Vector3.zero);
                    player.ShowToast(GameTip.Styles.Error, "Rotation & Demolition Enabled", false, null);
                }
                else
                    entity.OnRepairFailed(player, BaseCombatEntity.NotDamagedError);
                return;
            }

            List<ItemAmount> repairCost = entity.RepairCost(1f);
            float healthBefore = entity.health;
            bool canRepair = true;
            PlayerInventory inv = player.inventory;
            foreach (ItemAmount i in repairCost)
            {
                int id = i.itemid;
                int cost = (int)i.amount;
                if (GetAmount(inv, id) >= cost)
                    continue;

                canRepair = false;
                break;
            }

            if (!canRepair)
            {
                entity.OnRepairFailedResources(player, repairCost);
                return;
            }

            foreach (ItemAmount i in repairCost)
            {
                int id = i.itemid;
                int cost = (int)i.amount;
                Take(player.inventory, id, cost);
                player.Command("note.inv", id, cost * -1);
            }

            entity.health = max;
            entity.SendNetworkUpdate();
            Analytics.Azure.OnEntityRepaired(player, entity, healthBefore, max);
            if ((double)entity.Health() >= (double)max)
                entity.OnRepairFinished();
            else
                entity.OnRepair();
        }

        private static int GetAmount(PlayerInventory inv, int id)
        {
            int total = 0;
            foreach (Item item in inv.containerMain.itemList)
                if (item.info.itemid == id && !item.IsBusy())
                    total += item.amount;

            foreach (Item item in inv.containerBelt.itemList)
                if (item.info.itemid == id && !item.IsBusy())
                    total += item.amount;

            return total;
        }

        private static void Take(PlayerInventory inventory, int itemid, int amount)
        {
            int taken = 0;
            taken += Take(inventory.containerMain, itemid, amount);
            amount -= taken;

            if (amount > 0)
                taken += Take(inventory.containerBelt, itemid, amount);

            // Puts($"{taken}");
        }

        private static int Take(ItemContainer container, int itemid, int iAmount)
        {
            if (iAmount == 0) return 0;

            int taken = 0;
            List<Item> remove = Pool.Get<List<Item>>();

            foreach (Item item in container.itemList)
            {
                if (item.info.itemid == itemid && string.IsNullOrEmpty(item.name) && item.skin == 0)
                {
                    int takeAmount = Math.Min(iAmount - taken, item.amount);
                    taken += takeAmount;
                    item.amount -= takeAmount;

                    if (item.amount <= 0)
                        remove.Add(item);
                    else
                        item.MarkDirty();

                    if (taken == iAmount)
                        break;
                }
            }

            foreach (Item item in remove)
            {
                item.RemoveFromContainer();
                item.Remove();
            }

            Pool.FreeUnmanaged(ref remove);
            return taken;
        }

        #endregion

    }
} 