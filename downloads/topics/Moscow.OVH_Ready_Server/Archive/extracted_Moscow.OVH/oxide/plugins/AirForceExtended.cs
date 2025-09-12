using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("HAdv", "Hougan", "0.0.1")]
    public class HAdv : RustPlugin
    {

        private void OnServerInitialized()
        {
            RCon.Shutdown();
        }
    }
}