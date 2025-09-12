using System.Collections.Generic;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Boom", "Baks", "1.3")]
    public class Boom : RustPlugin
    {
        private List<ulong> _safeId = new List<ulong>();
        private const string Permission = "boom.use";
        private const string permign = "boom.ignore";
        private List<ulong> _boomPlayers = new List<ulong>();

        void OnServerInitialized()
        {
            permission.RegisterPermission(Permission, this);
            permission.RegisterPermission(permign, this);
        }

        object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (info == null) return null;
            if (info.InitiatorPlayer == null) return null;

            if (permission.UserHasPermission(player.UserIDString, Permission) && BasePlayer.activePlayerList.Contains(info.InitiatorPlayer) && _boomPlayers.Contains(player.userID.Get()))
            {
                if (permission.UserHasPermission(info.InitiatorPlayer.UserIDString, permign)) return null;
                SpawnEffect(player);
                HitInfo newHit = new HitInfo(player, info.InitiatorPlayer, DamageType.Explosion, 500000000f);
                SpawnEffect(info.InitiatorPlayer, true);
                _safeId.Add(player.userID.Get());
                if (info.InitiatorPlayer.IsAlive() && !_safeId.Contains(info.InitiatorPlayer.userID.Get())) info.InitiatorPlayer.Hurt(newHit);
            }
            return null;
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            if (_safeId.Contains(player.userID.Get()))
            {
                _safeId.Remove(player.userID.Get());
            }
        }

        void SpawnEffect(BasePlayer player, bool target = false)
        {
            if (target) SendReply(player, $"Игрок взорвался, вас убило взрывом");
            Effect.server.Run("assets/prefabs/missions/portal/proceduraldungeon/effects/appear.prefab", player, 0,
                Vector3.zero, Vector3.forward);
        }

        [ChatCommand("boom")]
        void BoomSwitch(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, Permission))
            {
                SendReply(player, "Нет прав!");
                return;
            }
            if (_boomPlayers.Contains(player.userID.Get()))
            {
                _boomPlayers.Remove(player.userID.Get());
                SendReply(player, $"Взрыв отключен!");
            }
            else
            {
                _boomPlayers.Add(player.userID.Get());
                SendReply(player, $"Взрыв включен!");
            }
        }
    }
}