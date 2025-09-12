using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SimpleTurrets", "Shady14u", "1.0.4")]
    [Description("Controls Auto Turrets activation based on proximity.")]
    public class SimpleTurrets : RustPlugin
    {
        private const float AutoTurretsProximityRange = 40f;
        private const float FlameTurretsProximityRange = 20f;
        private const float GunTrapProximityRange = 10f;

        private readonly Dictionary<ulong, FlameTurret> _cachedFlameTurrets = new();
        private readonly Dictionary<ulong, GunTrap> _cachedGunTraps = new();
        private readonly Dictionary<ulong, AutoTurret> _cachedTurrets = new();

        private Coroutine _checkFlameTurretsCoroutine;
        private Coroutine _checkGunTrapsCoroutine;
        private Coroutine _checkTurretsCoroutine;

        private void CacheAllTurrets()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                switch (entity)
                {
                    case NPCAutoTurret npcAutoTurret:
                        break;
                    case AutoTurret autoTurret:
                        SlowDownTurret(autoTurret);
                        if (autoTurret.OwnerID.IsSteamId())
                        {
                            _cachedTurrets.Add(autoTurret.net.ID.Value, autoTurret);
                        }

                        break;
                    case FlameTurret flameTurret:
                        _cachedFlameTurrets.Add(flameTurret.net.ID.Value, flameTurret);
                        break;
                    case GunTrap gunTrap:
                        _cachedGunTraps.Add(gunTrap.net.ID.Value, gunTrap);
                        break;
                }
            }
        }

        private IEnumerator CheckFlameTurrets()
        {
            while (true)
            {
                foreach (var flameTurret in _cachedFlameTurrets.Values)
                {
                    if (IsPlayerInRange(flameTurret.transform.position, FlameTurretsProximityRange))
                    {
                        flameTurret.InvokeRepeating(flameTurret.SendAimDir, 0f, 0.1f);
                    }
                    else
                    {
                        flameTurret.CancelInvoke(flameTurret.SendAimDir);
                    }
                }

                yield return CoroutineEx.waitForSeconds(5f);
            }
        }

        private IEnumerator CheckGunTraps()
        {
            while (true)
            {
                foreach (var gunTrap in _cachedGunTraps.Values)
                {
                    if (IsPlayerInRange(gunTrap.transform.position, GunTrapProximityRange))
                    {
                        if (!GunTrap.updateGunTrapWorkQueue.Contains(gunTrap))
                        {
                            GunTrap.updateGunTrapWorkQueue.Add(gunTrap);
                        }
                    }
                    else
                    {
                        if (GunTrap.updateGunTrapWorkQueue.Contains(gunTrap))
                        {
                            GunTrap.updateGunTrapWorkQueue.Remove(gunTrap);
                        }
                    }
                }

                yield return CoroutineEx.waitForSeconds(5f);
            }
        }

        private IEnumerator CheckTurrets()
        {
            while (true)
            {
                foreach (var autoTurret in _cachedTurrets.Values)
                {
                    if (!autoTurret.IsPowered())
                    {
                        autoTurret.SetFlag(BaseEntity.Flags.On, false, true);
                        continue;
                    }

                    if (IsPlayerInRange(autoTurret.transform.position, AutoTurretsProximityRange))
                    {
                        if (!autoTurret.IsOn())
                        {
                            autoTurret.SetFlag(BaseEntity.Flags.On, true, true);
                        }
                    }
                    else
                    {
                        if (autoTurret.IsOn())
                        {
                            autoTurret.SetFlag(BaseEntity.Flags.On, false, true);
                        }
                    }
                }

                yield return CoroutineEx.waitForSeconds(5f);
            }
        }

        private bool IsPlayerInRange(Vector3 position, float distance)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (Vector3.Distance(position, player.transform.position) < distance)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            switch (entity)
            {
                case NPCAutoTurret npcAutoTurret:
                    break;
                case AutoTurret autoTurret:
                    _cachedTurrets.Remove(autoTurret.net.ID.Value);
                    break;
                case FlameTurret flameTurret:
                    _cachedFlameTurrets.Remove(flameTurret.net.ID.Value);
                    break;
                case GunTrap gunTrap:
                    _cachedGunTraps.Remove(gunTrap.net.ID.Value);
                    break;
            }
        }

        private void OnEntitySpawned(BaseEntity entity)
        {
            switch (entity)
            {
                case NPCAutoTurret npcAutoTurret:
                    break;
                case AutoTurret autoTurret:
                    _cachedTurrets.Add(autoTurret.net.ID.Value, autoTurret);
                    SlowDownTurret(autoTurret);
                    break;
                case FlameTurret flameTurret:
                    _cachedFlameTurrets.Add(flameTurret.net.ID.Value, flameTurret);
                    break;
                case GunTrap gunTrap:
                    _cachedGunTraps.Add(gunTrap.net.ID.Value, gunTrap);
                    break;
            }
        }

        private void OnServerInitialized()
        {
            CacheAllTurrets();
            _checkTurretsCoroutine = ServerMgr.Instance.StartCoroutine(CheckTurrets());
            _checkFlameTurretsCoroutine = ServerMgr.Instance.StartCoroutine(CheckFlameTurrets());
            _checkGunTrapsCoroutine = ServerMgr.Instance.StartCoroutine(CheckGunTraps());
        }

        private void SlowDownTurret(AutoTurret autoTurret)
        {
            autoTurret.CancelInvoke(autoTurret.ServerTick);
            autoTurret.CancelInvoke(autoTurret.SendAimDir);
            autoTurret.InvokeRepeating(autoTurret.ServerTick, Random.Range(0f, 1f), 0.1f);
            autoTurret.InvokeRandomized(autoTurret.SendAimDir, Random.Range(0f, 1f), 0.5f, 0.1f);
        }

        private void Unload()
        {
            _cachedTurrets.Clear();
            _cachedFlameTurrets.Clear();
            _cachedGunTraps.Clear();
            ServerMgr.Instance.StopCoroutine(_checkTurretsCoroutine);
            ServerMgr.Instance.StopCoroutine(_checkFlameTurretsCoroutine);
            ServerMgr.Instance.StopCoroutine(_checkGunTrapsCoroutine);
        }
    }
}