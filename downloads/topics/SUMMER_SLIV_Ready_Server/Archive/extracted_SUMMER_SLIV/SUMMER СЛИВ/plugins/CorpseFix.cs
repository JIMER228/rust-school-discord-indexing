using System;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("CorpseFix","Zirper","1.0.0")]

    public class CorpseFix : RustPlugin
    {
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity != null)
            {
                if (entity.name.Contains("player_corpse.prefab"))
                {
                    timer.Once(120f, () =>
                    {
                        if (entity != null)
                        {
                            entity.Kill();
                        }
                    });
                }
            }			
        }    
    }
}