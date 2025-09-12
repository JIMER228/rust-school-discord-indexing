namespace Oxide.Plugins
{
    [Info("RealQueue", "Hougan", "0.0.1")]
    public class RealQueue : RustPlugin
    {
        private void OnServerInitialized() => permission.RegisterPermission("realqueue.skip", this);
        
        private bool? CanBypassQueue(Network.Connection connection)
        { 
            if (!permission.UserHasPermission(connection.userid.ToString(), "realqueue.skip"))
                return null;
            
            return true;
        }
    }
} 