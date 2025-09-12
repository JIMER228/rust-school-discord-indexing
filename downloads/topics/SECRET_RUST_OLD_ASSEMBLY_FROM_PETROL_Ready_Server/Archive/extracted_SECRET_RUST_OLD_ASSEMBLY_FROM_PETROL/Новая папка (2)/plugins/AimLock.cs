using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("AimLock", "Molik", "1.0.0")]
    [Description("Aim Lock.")]
    public class AimLock : RustPlugin
    {
        [PluginReference] Plugin MultiFighting;
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            try
            {
                if (entity == null || entity?.net?.ID == null || info == null) return;
                BasePlayer attacker = info.Initiator.ToPlayer();
                if (attacker == null) return;
                var victim = entity as BasePlayer;
                if (victim == null) return;
                if (victim.IsNpc) return;
                if (victim.GetComponent<NPCPlayer>() != null) return;
                var distance = info.Initiator.Distance(victim.transform.position);
                string suspectid = attacker.userID.ToString();
                var isSteamSprite = IsSteam(suspectid);
                if (isSteamSprite == "IS_STEAM") return;
                if (distance > 10)
                {
                    var item = attacker.GetActiveItem();
                    if (item == null) return;
                    string weapon = item.info.shortname;
                    if (weapon == null) return;
                    BaseProjectile weapon1 = item.GetHeldEntity() as BaseProjectile;
                    string ammo = weapon1.primaryMagazine.ammoType.shortname;
                    if (weapon == "bow" || weapon == "shotgun.spas12" || weapon == "shotgun.pump" || weapon == "crossbow")
                    {
                        if (distance > 100 && info.isHeadshot)
                        {
                            if (ammo == "ammo.shotgun.slug") return;
                            Server.Command($"bs.ban {attacker.userID} AIM_DETECT");
                        }
                    }
                    if (weapon == "pistol.eoka" || weapon == "shotgun.waterpipe" || weapon == "shotgun.double")
                    {
                        if (distance > 50)
                        {
                            if (ammo == "ammo.shotgun.slug") return;
                            Server.Command($"bs.ban {attacker.userID} AIM_DETECT");
                        }
                    }
                    if (weapon == "rifle.ak" || weapon == "rifle.ak.ice" || weapon == "hmlmg" || weapon == "lmg.m249" || weapon == "rifle.lr300")
                    {
                        if (distance > 350)
                        {
                            Server.Command($"bs.ban {attacker.userID} AIM_DETECT");
                        }
                    }
                    if (weapon == "smg.mp5" || weapon == "smg.smg2" || weapon == "smg.thompson")
                    {
                        if (distance > 250 && info.isHeadshot)
                        {
                            Server.Command($"bs.ban {attacker.userID} AIM_DETECT");
                        }
                    }
                    if (weapon == "rifle.semiauto" || weapon == "rifle.m39")
                    {
                        if (distance > 300)
                        {
                            Server.Command($"bs.ban {attacker.userID} AIM_DETECT");
                        }
                    }
                    if (weapon == "pistol.semiauto" || weapon == "pistol.revolver" || weapon == "pistol.python" || weapon == "pistol.m92")
                    {
                        if (distance > 200 && info.isHeadshot)
                        {
                            Server.Command($"bs.ban {attacker.userID} AIM_DETECT");
                        }
                    }
                }
            }
            catch (NullReferenceException) { }
        }
        string IsSteam(string suspectid)
        {
            if (MultiFighting != null)
            {
                var player = BasePlayer.Find(suspectid);
                if (player == null)
                {
                    return "ERROR #1";
                }

                var obj = MultiFighting.CallHook("IsSteam", player.Connection);
                if (obj is bool)
                {
                    if ((bool)obj)
                    {
                        return ("IS_STEAM");
                    }
                    else
                    {
                        return ("IS_PIRATE");
                    }
                }
                else
                {
                    return "ERROR #2";
                }
            }
            else return ("IS_STEAM");
        }
    }
}