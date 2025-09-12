using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core;
using Oxide.Plugins;
using Rust;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("NoSteam", "Аслан", "1.0.0")]
    public class NoSteam : RustPlugin
    {
        [HookMethod("OnUserApprove")]
        object OnUserApprove(Network.Connection connection)
        {
            connection.authStatus = "ok";
            ConnectionAuth.m_AuthConnection.Remove(connection);
            SingletonComponent<ServerMgr>.Instance.ConnectionApproved(connection);
            return false;
        }
    }
}