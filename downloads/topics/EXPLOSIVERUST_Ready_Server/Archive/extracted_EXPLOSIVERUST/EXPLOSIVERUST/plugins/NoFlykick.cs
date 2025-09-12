
namespace Oxide.Plugins
{
    [Info("No Flykick", "Drop Dead", "1.0.0")]
    class NoFlykick : RustPlugin
    {
        #region Hooks
        object OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            if (type != AntiHackType.FlyHack)
            {
                return null;
            }
            if (player.IsAdmin)
            {
                return false;
            }
            return null;
        }
        #endregion
    }
}
