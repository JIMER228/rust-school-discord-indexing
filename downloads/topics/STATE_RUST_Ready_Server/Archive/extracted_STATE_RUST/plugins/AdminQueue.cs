using Network;

namespace Oxide.Plugins
{
    [Info("AdminQueue", "Nimant", "1.0.0")]
    class AdminQueue : RustPlugin
    {
        private const string Perm = "adminqueue.allow";

        private void Init() => permission.RegisterPermission(Perm, this);        

        private object CanBypassQueue(Connection connection)
        {
            if (permission.UserHasPermission(connection.userid.ToString(), Perm))
                return true;
			
            return null;
        }
    }
}
