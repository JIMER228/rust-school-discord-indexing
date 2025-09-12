using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("NoDeadCopter", "b1xbyy", "1.0.0")]
    class NoDeadCopter : RustPlugin
    {
        private static readonly HashSet<Type> ExemptInitiators = new()
        {
            typeof(Minicopter),
            typeof(ScrapTransportHelicopter),
            typeof(AttackHelicopter)
        };

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BasePlayer && info?.Initiator != null && ExemptInitiators.Contains(info.Initiator.GetType()))
            {
                info.damageTypes.ScaleAll(0.0f);
            }
            return null;
        }
    }
}