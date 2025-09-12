namespace Oxide.Plugins
{
    [Info("ShopFront Bugfix", "Orange", "1.0.0")]
    [Description("https://rustworkshop.space/")]
    public class ShopFrontBugfix : RustPlugin
    {
        #region Oxide Hooks

        private void Init()
        {
            Unsubscribe(nameof(OnEntitySpawned));
        }

        private void OnServerInitialized()
        {
            Subscribe(nameof(OnEntitySpawned));
            
            foreach (var entity in UnityEngine.Object.FindObjectsOfType<ShopFront>())
            {
                OnEntitySpawned(entity);
            }
        }

        private void OnEntitySpawned(ShopFront entity)
        {
            Modify(entity);
        }

        #endregion

        #region Core

        private void Modify(ShopFront entity)
        {
            entity.vendorInventory.canAcceptItem += (item, i) =>
            {
                var player = item.GetOwnerPlayer();
                if (player == null)
                {
                    return true;
                }

                if (entity.vendorPlayer != player || entity.HasFlag(BaseEntity.Flags.Reserved1) == true)
                {
                    ReturnBack(item, player);
                    return false;
                }

                return true;
            };
            
            entity.customerInventory.canAcceptItem += (item, i) =>
            {
                var player = item.GetOwnerPlayer();
                if (player == null)
                {
                    return true;
                }

                if (entity.customerPlayer != player || entity.HasFlag(BaseEntity.Flags.Reserved2) == true)
                {
                    ReturnBack(item, player);
                    return false;
                }

                return true;
            };
        }

        private void ReturnBack(Item item, BasePlayer player)
        {
            var oldPos = item.position;
            var oldContainer = item.parent;
            
            NextTick(() =>
            {
                if (oldContainer == null)
                {
                    player.GiveItem(item);
                }
                else
                {
                    item.MoveToContainer(oldContainer, oldPos);
                }
            });
        }

        #endregion
    }
}