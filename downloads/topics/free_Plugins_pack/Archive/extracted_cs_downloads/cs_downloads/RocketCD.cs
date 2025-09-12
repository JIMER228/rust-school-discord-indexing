using System;
using System.Collections.Generic;

namespace Oxide.Plugins;

[Info("RocketCD", "TEGIR", "1.0.0")]
public class RocketCD : RustPlugin
{
    private int secondscd = 55;
    
    private Dictionary<ulong, DateTime> playerCD = new Dictionary<ulong, DateTime>();
    
    void OnRocketLaunched(BasePlayer player, BaseEntity entity)
    {
        if (entity.ShortPrefabName == "rocket_fire")
        {
            if (playerCD.TryGetValue(player.userID, out DateTime time))
            {
                if (DateTime.UtcNow.ToLocalTime() > time)
                {
                    playerCD[player.userID] = DateTime.UtcNow.ToLocalTime().AddSeconds(secondscd);
                }
                else
                {
                    entity.Kill();
                    player.GiveItem(ItemManager.CreateByName("ammo.rocket.fire", 1, 0));
                    SendReply(player, $"Вы не можете использовать зажигательную ракету. Подождите еще {(time - DateTime.UtcNow.ToLocalTime()).TotalSeconds.ToString("00")} секунд");
                }
            }
            else
            {
                playerCD.Add(player.userID, DateTime.UtcNow.ToLocalTime().AddSeconds(secondscd));
            }
        }
        
    }
    
    
}