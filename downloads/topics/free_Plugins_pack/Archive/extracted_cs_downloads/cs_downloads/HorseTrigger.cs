using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("HorseTrigger", "bmgjet", "1.0.0")]
    [Description("Allows testridablehorse to teleport though terrainmesh when on a invisable collider path")]
    class HorseTrigger : RustPlugin
    {
        public static HorseTrigger plugin;
        List<HorseMod> ModdedHorses = new List<HorseMod>();

        private void Init() { plugin = this; }

        private void OnServerInitialized()
        {
            foreach (KeyValuePair<uint, BaseNetworkable> bn in BaseEntity.serverEntities.entityList)
            {
                if (bn.Value == null) continue;
                RidableHorse horse = bn.Value as RidableHorse;
                if (horse == null || horse.IsDead()) continue;
                if (!horse.HasComponent<HorseMod>()) { HorseMod Fix = horse.gameObject.AddComponent<HorseMod>(); if (Fix != null) { ModdedHorses.Add(Fix); } }
            }
        }        

        private void Unload(){foreach (HorseMod mod in ModdedHorses) { GameObject.Destroy(mod); }plugin = null;}

        private void movehorse(RidableHorse horse, BasePlayer player, Vector3 difference)
        {
            if (horse == null || player == null) return;
            Vector3 pos = player.transform.position;
            Vector3 newdiff = horse.transform.position - pos;
            timer.Once(0.5f, () => { if (horse == null || player == null || !horse.IsLeading() || player.isMounted) { return; } movehorse(horse, player, newdiff); });
            if (Vector3.Distance(horse.transform.position, pos) > 5f){horse.transform.position = pos - difference + (newdiff);}
        }

        object OnHorseLead(RidableHorse horse, BasePlayer player){movehorse(horse, player, horse.transform.position - player.transform.position);return null;}

        private void OnEntitySpawned(RidableHorse horse)
        {
            if (horse == null) return;
            HorseMod Fix = horse.gameObject.AddComponent<HorseMod>();
            if (Fix != null) { ModdedHorses.Add(Fix); }
        }

        public string[] Triggers = new string[]
        {
            //"road_tunnel_double_str_a_36m",
            "road_tunnel_double_exit_a_36m",
            //"assets/content/structures/road_tunnels/road_tunnel_double_slope_b_72m.prefab",
            //"assets/content/structures/road_tunnels/road_tunnel_double_slope_a_72m.prefab",
            //"assets/content/structures/road_tunnels/road_tunnel_double_bend_a_36m.prefab",
            "assets/content/structures/tunnels/tunnel.single.entrance.prefab",
            //"assets/content/structures/tunnels/tunnel.single.straight.slope.72.prefab",
            //"assets/content/structures/tunnels/tunnel.single.straight.36.prefab",
            "assets/content/structures/tunnels/tunnel.double.entrance.prefab",
            //"assets/content/structures/tunnels/tunnel.single.straight.slope.72.prefab",
            //"assets/content/structures/tunnels/tunnel.double.straight.36.prefab",
            //"assets/content/structures/tunnels/tunnel.double.splitter.prefab",
            //"assets/content/structures/tunnels/tunnel.single.corner.90.prefab",
            //"assets/content/structures/tunnels/tunnel.double.gate.prefab",
            //"assets/content/structures/tunnels/tunnel.single.straight.gate.prefab",
        };

        private class HorseMod : MonoBehaviour
        {
            public BaseEntity _horse;
            public RidableHorse horse;

            private void Awake()
            {
                _horse = GetComponent<BaseEntity>();
                horse = _horse as RidableHorse;
            }

            public void FixedUpdate()
            {
                if (_horse == null || !horse.HasDriver()) { return; }
                BasePlayer driver = horse.GetDriver();
                if (driver != null && driver.serverInput.IsDown(BUTTON.FORWARD) && horse.currentSpeed < 0.01f)
                {
                    if (TerrainMeta.Collision.GetIgnore(horse.transform.position, 0.01f))
                    {
                        var hits = Physics.SphereCastAll(horse.transform.position + (horse.transform.forward * 4f) + (horse.transform.up * 1.1f), 0.1f, Vector3.down);
                        foreach (var hit in hits)
                        {
                            Collider bc = hit.GetCollider();
                            if (bc == null) { continue; }
                            if (plugin.Triggers.Contains(bc.name)){horse.transform.position = horse.transform.position + (horse.transform.forward * 1.1f); return;}
                        }
                    }
                }
            }
        }
    }
}
