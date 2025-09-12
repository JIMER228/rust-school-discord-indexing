using Oxide.Core;
using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Oxide.Core.Plugins;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System.Diagnostics;

namespace Oxide.Plugins
{
    [Info("NF", "", "1.0.0", ResourceId = 1348)]
    [Description("Tweak various settings of helicopters.")]
    class NF : RustPlugin
    {
        private HashSet<FireBall> FireBalls = new HashSet<FireBall>();
        bool DisableNapalm;
        int WaterRequired;

        void OnServerInitialized()
        {
            FireBalls = new HashSet<FireBall>(BaseNetworkable.serverEntities?.Where(p => p != null && p is FireBall && (p.PrefabName.Contains("napalm") || p.PrefabName.Contains("fireball")))?.Select(p => p as FireBall) ?? null);
        }

        void Init()
        {
            string[] perms = { "killnapalm" };
            for (int j = 0; j < perms.Length; j++) permission.RegisterPermission("nf." + perms[j], this);

            AddCovalenceCommand("km", "cmdKillFB");
        }

        protected override void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                {"noPerms", "You do not have permission to use this command!"},
                {"entityDestroyed", "{0} {1} удалены"},
            };
            lang.RegisterMessages(messages, this);
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null) return;

            var prefabname = entity?.ShortPrefabName ?? string.Empty;
            var longprefabname = entity?.PrefabName ?? string.Empty;

            if (string.IsNullOrEmpty(prefabname) || string.IsNullOrEmpty(longprefabname)) return;

            var ownerID = (entity as BaseEntity)?.OwnerID ?? 0;

            if ((prefabname.Contains("napalm") || prefabname.Contains("fireball")) && !prefabname.Contains("rocket"))
            
            {

                var fireball = entity?.GetComponent<FireBall>() ?? null;

                if (fireball == null) return;
                fireball.Kill();
                if (DisableNapalm)

                {

                    fireball.enableSaving = false; //potential fix for entity is null but still in save list

                    NextTick(() => { if (!(entity?.IsDestroyed ?? false)) entity.Kill(); });

                }

                else

                {

                    if (!entity.IsDestroyed)

                    {

                        fireball.waterToExtinguish = WaterRequired;

                        fireball.SendNetworkUpdate();

                        if (!FireBalls.Contains(fireball)) FireBalls.Add(fireball);

                    }

                }
           }
       }

       void OnEntityKill(BaseNetworkable entity)

        {

            if (entity == null) return;
            var name = entity?.ShortPrefabName ?? string.Empty;
            var crate = entity?.GetComponent<LockedByEntCrate>() ?? null;

            if (entity is FireBall || name.Contains("fireball") || name.Contains("napalm"))
            {
                var fireball = entity?.GetComponent<FireBall>() ?? null;
                if (fireball != null && FireBalls.Contains(fireball)) FireBalls.Remove(fireball);
            }
        }

        private string GetMessage(string key, string steamId = null) => lang.GetMessage(key, this, steamId);

        private void SendNoPerms(IPlayer player) => player?.Message(GetMessage("noPerms", player.Id));

        private void SendNoPerms(BasePlayer player) { if (player != null && player.IsConnected) player.ChatMessage(GetMessage("noPerms", player.UserIDString)); }

        private string GetNoPerms(string userID = "") { return GetMessage("noPerms", userID); }

        private bool HasPerms(string userId, string perm) { return (userId == "server_console" || permission.UserHasPermission(userId, "helicontrol.admin")) ? true : permission.UserHasPermission(userId, (!perm.StartsWith("nf") ? "nf." + perm : perm)); }

        private void cmdKillFB(IPlayer player, string command, string[] args)
        {
            if (!HasPerms(player.Id, "killnapalm"))
            {
                SendNoPerms(player);
                return;
            }
            player.Message(string.Format(GetMessage("entityDestroyed", player.Id), killAllFB().ToString("N0"), "fireball"));
        }
        private void CheckHelicopter()
        {
            FireBalls.RemoveWhere(p => (p?.IsDestroyed ?? true));
        }
        private void UnlockCrate(LockedByEntCrate crate)
        {
            if (crate == null || (crate?.IsDestroyed ?? true)) return;
            var lockingEnt = (crate?.lockingEnt != null) ? crate.lockingEnt.GetComponent<FireBall>() : null;
            if (lockingEnt != null && !lockingEnt.IsDestroyed)
            {
                lockingEnt.enableSaving = false; //again trying to fix issue with savelist
                lockingEnt.CancelInvoke(lockingEnt.Extinguish);
                lockingEnt.Invoke(lockingEnt.Extinguish, 30f);
            }
            crate.CancelInvoke(crate.Think);
            crate.SetLocked(false);
            crate.lockingEnt = null;
        }
        private int killAllFB()
        {
            CheckHelicopter();
            var countfb = 0;
            if (FireBalls.Count < 1) return countfb;
            foreach (var fb in FireBalls.ToList())
            {
                if (fb == null || fb.IsDestroyed) continue;
                fb.Kill();
                countfb++;
            }
            CheckHelicopter();
            return countfb;
        }


   }
}