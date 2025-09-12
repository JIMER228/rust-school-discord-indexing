using System.Linq;
using UnityEngine;
using Network;
using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("fix", "Developer x SwUn ", "0.0.1")]
    class fix : RustPlugin
    {
        object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null || arg.Connection.authLevel >= 2) return null;
            var command = arg?.cmd?.FullName;
            if (command == null || !command.StartsWith("o.") && !command.StartsWith("oxide.") && !command.StartsWith("plugin.") && !command.StartsWith("perm.")) return null;
            return false;
        }
    }
}