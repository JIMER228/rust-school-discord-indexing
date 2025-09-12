using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core;
using Oxide.Plugins;
using Oxide.Core.Libraries.Covalence;
using Rust;
using UnityEngine;
using Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace Oxide.Plugins
{
    [Info("AntiCraft", "Аслан", "1.0.0")]
    class AntiCraft : RustPlugin
    {	
        object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg.cmd.namefull == "craft.add")
            {
                string[] ex = arg.ArgsStr.Split(' ');
                if (ex.Length > 1)
                {
                    int count = 0;
                    int.TryParse(ex[1], out count);
                    if (count > 100000)
                        return false;
                }
            }
            return null;
        }

    }
}