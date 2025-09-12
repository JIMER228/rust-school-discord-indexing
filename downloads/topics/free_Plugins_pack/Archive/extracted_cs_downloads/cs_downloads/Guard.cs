
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Guard", "FourTeen", "1.0.4")]
    class Guard : RustPlugin
    {
        List<string> itemname = new List<string>()
        {
            "grenade.molotov.item", "xmas.advanced.lights.item", "ammo_rocket_fire.item",
            "flamethrower.item", "militaryflamethrower.item", "igniter.item"
        };
        bool init = false;
        private void OnServerInitialized()
        {
            init = true;
        }
        void OnEntitySpawned(BaseEntity entity)
        {
            if (init == false) return;
            if (entity == null) return;
            //Puts(entity.name);
            if (entity.prefabID == 1202855575 || entity.prefabID == 3550347674 || entity.prefabID == 901927673)
            {
                entity.Kill();
            }
            if (entity.name == "assets/prefabs/deployable/search light/searchlight.deployed.prefab")
            {
                entity.Kill();
            }
            else if (entity.name == "assets/prefabs/misc/xmas/poweredlights/xmas.advanced.lights.deployed.prefab")
            {
                entity.Kill();
            }
            else if (entity.name == "assets/prefabs/misc/xmas/christmas_lights/xmas.lightstring.deployed.prefab")
            {
                entity.Kill();
            }
            else if (entity.name == "assets/bundled/prefabs/fireball_small.prefab")
            {
                entity.Kill();
            }
            else if (entity.name == "assets/bundled/prefabs/fireball_small_shotgun.prefab")
            {
                entity.Kill();
            }
            else if (entity.name == "assets/bundled/prefabs/fireball.prefab")
            {
                entity.Kill();
            }
            else if (entity.name == "assets/prefabs/npc/m2bradley/oilfireball2.prefab")
            {
                entity.Kill();
            }
            else if (entity.name == "assets/bundled/prefabs/fireball_small_arrow.prefab")
            {
                entity.Kill();
            }
            else if (entity.name == "assets/bundled/prefabs/fireball_small_molotov.prefab")
            {
                entity.Kill();
            }
            else if (entity.name == "assets/prefabs/deployable/playerioents/industrialconveyor/industrialconveyor.deployed.prefab")
            {
                entity.Kill();
            }
        }
        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (init == false) return;
            if (container != null && item != null && item.info != null)
            {
                //Puts(item.info.name);
                foreach (string name in itemname)
                {
                    if (item.info.name == name)
                    {
                        item.RemoveFromContainer();
                    }
                }
            }
        }
    }
}