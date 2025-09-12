//это не полноценный плагин если чё сдесь чисто изначально включено кому я это пишу хз, джанги привет

namespace Oxide.Plugins
{
    [Info("NoFlykick", "FourTeen", "1.0.0")]
    [Description("Edit")]
    class NoFlykick : RustPlugin
    {
        #region Fields/Initialization
        private const string permUse = "noflykick.use";
        private bool IsEnabled;

        private void Init()
        {
            permission.RegisterPermission(permUse, this);

            Toggle();

        }
        #endregion

        #region Functions
        private void GetStatus()
        {
            if (IsEnabled)
            {
                Puts("enabled");
            }
            else
            {
                Puts("disabled");
            }
        }

        private void Toggle()
        {
            if (IsEnabled)
            {
                Unsubscribe(nameof(OnPlayerViolation));
                Puts("enabled");
            }
            else
            {
                Subscribe(nameof(OnPlayerViolation));
                Puts("disabled");
            }
            IsEnabled = !IsEnabled;
        }
        #endregion

        #region Hooks
        object OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            if (type == AntiHackType.FlyHack)
            {
                if (permission.UserHasPermission(player.UserIDString, permUse))
                {
                    return true;
                }
            }
            return null;
        }
        #endregion
    }
}