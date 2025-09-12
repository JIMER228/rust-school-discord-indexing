using System;

namespace Oxide.Plugins
{
    [Info("ConnectionLogger", "Orange", "1.0.0")]
    public class ConnectionLogger : RustPlugin
    {
        private void OnPlayerConnected(BasePlayer player)
        {
            var id = player.userID;
            var ip = player.Connection.ipaddress;
            var name = player.displayName;
            LogToFile("connections", $"[{DateTime.Now.ToLongTimeString()}] {name}({id}) connected from '{ip}'", this);
        }
        
        private void OnPlayerDisconnected1(BasePlayer player, string reason)
        {
            var id = player.userID;
            var ip = player.Connection.ipaddress;
            var name = player.displayName;
            LogToFile("disconnections", $"[{DateTime.Now.ToLongTimeString()}] {name}({id}) disconnected from '{ip}' for '{reason}'", this);
        }
    }
}