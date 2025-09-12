using UnityEngine;

namespace Carbon.Plugins
{
    [Info("RecyclerUI", "DeepSeekR1", "1.0.0")]
    [Description("Opens recycler UI with a chat command")]
    public class RecyclerUI : CarbonPlugin
    {
        private const string RECYCLER_PERMISSION = "recyclerui.use";

        private void Init()
        {
            Puts("RecyclerUI plugin initializing...");
            permission.RegisterPermission(RECYCLER_PERMISSION, this);
            cmd.AddChatCommand("rec", this, nameof(RecyclerCommand));
            Puts("Recycler command registered.");
        }

        private void RecyclerCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, RECYCLER_PERMISSION))
            {
                player.ChatMessage("You don't have permission to use this command.");
                return;
            }

            if (player?.IsValid() != true)
            {
                Puts("Player is not valid.");
                return;
            }

            if (player.IsDead() || player.IsWounded())
            {
                player.ChatMessage("You cannot use the recycler while dead or wounded.");
                return;
            }

            OpenRecyclerUI(player);
        }

        private void OpenRecyclerUI(BasePlayer player)
        {
            if (player.inventory?.loot == null)
            {
                return;
            }

            player.inventory.loot.Clear();
            player.inventory.loot.PositionChecks = false;

            BaseEntity entity = GameManager.server.CreateEntity("assets/bundled/prefabs/static/recycler_static.prefab", Vector3.zero);
            if (entity == null)
            {
                return;
            }

            Recycler? recycler = entity as Recycler;
            if (recycler == null)
            {
                entity.Kill();
                return;
            }

            recycler.enableSaving = false;
            recycler.Spawn();
            recycler.SetFlag(BaseEntity.Flags.On, true);
            recycler.SetFlag(BaseEntity.Flags.Locked, false);
            recycler.SetFlag(BaseEntity.Flags.Disabled, false);
            recycler.SetFlag(BaseEntity.Flags.Busy, false);
            recycler.SendNetworkUpdate();
            recycler.SendNetworkUpdate_Position();

            recycler.inventory.capacity = 6;
            recycler.inventory.itemList.Clear();
            recycler.inventory.MarkDirty();

            _ = player.inventory.loot.StartLootingEntity(recycler, true);
            player.inventory.loot.AddContainer(recycler.inventory);
            player.inventory.loot.SendImmediate();
            player.inventory.loot.MarkDirty();

            _ = timer.Once(0.1f, () =>
            {
                if (!player.inventory.loot.IsLooting())
                {
                    recycler.Kill();
                }
                else
                {
                    player.inventory.loot.SendImmediate();
                    recycler.SendNetworkUpdate();
                }
            });
        }
    }
}