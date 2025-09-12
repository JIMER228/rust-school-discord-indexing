using System.Collections.Generic;
using Oxide.Core;
using System;
using System.Text;


namespace Oxide.Plugins
{
    [Info("Custom Rock", "birthdates", "0.5", ResourceId = 0)]
    [Description("Custom rock when you spawn.")]
    public class CustomRock : RustPlugin
    {
        private ulong DefaultRockSkin = 2523879784;    

        private void OnPlayerRespawned(BasePlayer player)
        {
            GiveKit(player);
        }


        private void GiveKit(BasePlayer player)
        {
            ulong skin = DefaultRockSkin;
            foreach (var i in player.inventory.AllItems())
                if (i.info.shortname == "rock" && skin != null)
                {
                    var b = ItemManager.CreateByName("rock", 1, skin);

                    if (i != null) i.Remove();

                    if (b != null) player.GiveItem(b);
                }
            if(skin != 0) {
                return;
            }
        }


    }
    
}