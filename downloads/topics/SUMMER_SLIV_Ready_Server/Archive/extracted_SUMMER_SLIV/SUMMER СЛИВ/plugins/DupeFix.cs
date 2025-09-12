using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using Network;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("DupeFix", "Zirper", "1.0.0")]
    class DupeFix : RustPlugin
    {
        object OnPlayerAttack(BasePlayer player, HitInfo info)
        {                   
            if (info.HitEntity is BaseEntity)
            {
                if (info.HitEntity.name == "assets/prefabs/npc/patrol helicopter/heli_crate.prefab" || info.HitEntity.name == "assets/prefabs/npc/m2bradley/bradley_crate.prefab" || info.HitEntity.name == "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab" || info.HitEntity.name == "assets/bundled/prefabs/radtown/crate_elite.prefab" || info.HitEntity.name == "assets/bundled/prefabs/radtown/crate_normal.prefab" || info.HitEntity.name == "assets/bundled/prefabs/radtown/crate_normal_2.prefab" || info.HitEntity.name == "assets/bundled/prefabs/radtown/crate_tools.prefab")
                {
                    return true;
                }
            }
            return null;
        }        
	}
}