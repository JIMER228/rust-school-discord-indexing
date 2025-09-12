namespace Oxide.Plugins
{
    [Info("ConnectP", "raidbuly | Drop Dead", "1.0.0")]

    class connectP : RustPlugin
    {
        void Loaded()
        {
            if (!permission.PermissionExists("connectP.join")) permission.RegisterPermission("connectP.join", this);
        }
        void OnPlayerConnected(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, "connectP.join"))
            {
                SendReply(player, "Добро пожаловать");
                return;
            }
            else
            {
                player.Kick("На сервере выключен WhiteList");
                return;
            }
        }
    }
}