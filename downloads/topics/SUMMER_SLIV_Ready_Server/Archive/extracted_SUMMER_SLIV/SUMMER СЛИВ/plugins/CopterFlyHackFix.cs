using UnityEngine;
using Oxide.Core.Libraries;
using System.Linq;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Copter FlyHack Kick Fix", "Zirper", "1.0.0")]
    public class CopterFlyHackFix : RustPlugin
    {
        private void OnEntityDismounted(BaseNetworkable entity, BasePlayer player) {
            if (1 > 0 && entity.GetParentEntity() is MiniCopter)
                player.PauseFlyHackDetection(5);
        }
    }
}
