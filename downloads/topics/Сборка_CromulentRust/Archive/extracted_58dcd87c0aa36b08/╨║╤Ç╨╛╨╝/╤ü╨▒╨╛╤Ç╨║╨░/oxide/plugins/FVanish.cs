using Oxide.Core.Plugins;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("FVanish", "FourTeen", "1.0.0")]
    internal class FVanish : RustPlugin
    {
        [PluginReference] private Plugin BetterVanish;
        private Dictionary<ulong, string> Vanish = new Dictionary<ulong, string>();
        private Dictionary<ulong, float> cooldown = new Dictionary<ulong, float>();
        private const string PERM = "fvanish.use";
        private const string PERM2 = "fvanish.35sec";
        private void OnServerInitialized()
        {
            permission.RegisterPermission(PERM, this);
            permission.RegisterPermission(PERM2, this);
        }
        [ChatCommand("v")]
        void vanishCMD(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, PERM))
            {
                player.ChatMessage("Нет прав!");
                return;
            }
            float cd = GetGlobalCooldown(player);
            if (cd != 0)
            {
                string formattedString = cd.ToString("0");
                player.ChatMessage($"Вы уже использовали эту команду. Используйте её снова через <color=#FF9740>{formattedString}</color> секунд.");
                return;
            }
            if (BetterVanish != null)
            {
                Collider[] hitColliders = Physics.OverlapSphere(player.transform.position, 10, LayerMask.GetMask("Player (Server)"));
                RelationshipManager.PlayerTeam team = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);

                foreach (Collider collider in hitColliders)
                {
                    BasePlayer targetPlayer = collider.GetComponent<BasePlayer>();
                    string ul = Random.Range(0, int.MaxValue).ToString();
                    bool teameit = false;
                    if (team != null && team.members.Contains(targetPlayer.userID.Get()) && !targetPlayer.IsSleeping() && !targetPlayer.IsDead() && !targetPlayer.IsWounded()) teameit = true;
                    if (targetPlayer != null && teameit != false)
                    {
                        GiveVanish(targetPlayer, ul);
                    }
                    else
                    {
                        GiveVanish(player, ul);
                    }
                    SetCooldown(player);
                }
            }
        }
        void OnPlayerSpawn(BasePlayer player)
        {
            if (player != null && Vanish.ContainsKey(player.userID.Get()))
            {
                Vanish.Remove(player.userID.Get());
                BetterVanish.CallHook("_Reappear", player);
            }
        }
        void OnPlayerConnected(BasePlayer player)
        {
            if (player != null && Vanish.ContainsKey(player.userID.Get()))
            {
                Vanish.Remove(player.userID.Get());
                BetterVanish.CallHook("_Reappear", player);
            }
        }
        void GiveVanish(BasePlayer player, string ul)
        {
            if (player == null) return;
            if (!Vanish.ContainsKey(player.userID.Get())) Vanish.Add(player.userID.Get(), ul);
            BetterVanish.CallHook("_Disappear", player);
            if (permission.UserHasPermission(player.UserIDString, PERM2))
            {
                timer.Once(35, () =>
                {
                    if (Vanish.ContainsKey(player.userID.Get())) Vanish.Remove(player.userID.Get());
                    BetterVanish.CallHook("_Reappear", player);
                });
            }
            else
            {
                timer.Once(10, () =>
                {
                    if (Vanish.ContainsKey(player.userID.Get())) Vanish.Remove(player.userID.Get());
                    BetterVanish.CallHook("_Reappear", player);
                });
            }
        }
        private void SetCooldown(BasePlayer player)
        {
            if (player == null) return;
            ulong userid = player.userID;
            if (permission.UserHasPermission(player.UserIDString, PERM2))
            {
                cooldown[userid] = Time.time + 120;
                timer.Once(120, () => cooldown.Remove(userid));
            }
            else
            {
                cooldown[userid] = Time.time + 180;
                timer.Once(180, () => cooldown.Remove(userid));
            }
        }
        private float GetGlobalCooldown(BasePlayer player)
        {
            if (player == null) return 0f;
            float cd;
            if (!cooldown.TryGetValue(player.userID.Get(), out cd))
            {
                return 0f;
            }

            return cd - Time.time;
        }
    }
}