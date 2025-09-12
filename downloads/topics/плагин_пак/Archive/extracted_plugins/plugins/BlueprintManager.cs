namespace Oxide.Plugins
{
    [Info("BlueprintManager", "cheese - lox", "1.0.0")]
    class BlueprintManager : RustPlugin
    {
        void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || !player.IsConnected || player.blueprints == null)
            {
                return;
            }

            player.blueprints.UnlockAll();
        }
    }
}