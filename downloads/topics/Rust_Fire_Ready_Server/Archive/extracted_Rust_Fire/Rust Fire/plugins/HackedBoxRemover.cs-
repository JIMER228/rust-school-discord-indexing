namespace Oxide.Plugins
{
    [Info("HackedBoxRemover", "Anathar", "0.0.1")]
    public class HackedBoxRemover : RustPlugin
    {
        void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            if (!(entity is LootContainer))
                return;
            {
                timer.Once(50, () =>
                {
                        entity.Kill();
                });
            }
        }
    }
}