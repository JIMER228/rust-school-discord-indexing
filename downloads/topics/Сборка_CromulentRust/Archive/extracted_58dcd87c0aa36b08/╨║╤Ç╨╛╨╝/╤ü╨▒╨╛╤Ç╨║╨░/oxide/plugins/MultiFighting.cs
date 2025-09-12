
using System;

namespace Oxide.Plugins
{
    [Info("MultiFighting", "FourTeen", "1.0.0")]
    class MultiFighting : RustPlugin
    {
        private bool IsSteam(Network.Connection connection)
        {
            if (connection == null) return true;
            if (connection.os != null && connection.os == "editor") return false;
            return (BitConverter.ToUInt32(connection.token, 72) != 480);
        }
    }
}