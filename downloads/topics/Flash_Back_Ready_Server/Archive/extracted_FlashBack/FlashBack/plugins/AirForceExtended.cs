using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AirForceExtended", "RAREGUN▲", "1.0.31")]

    public class AirForceExtended : RustPlugin
    {
        #region INIT

        private void OnServerInitialized()
        {
            foreach (BaseNetworkable e in BaseNetworkable.serverEntities)
            {
                if (!(e is SamSite) || e.gameObject.GetComponent<AirForce>() != null) continue;

                SamSite s = (SamSite)e;
                s.gameObject.AddComponent<AirForce>();
            }
        }

        private void Unload()
        {
            foreach (BaseNetworkable e in BaseNetworkable.serverEntities)
            {
                if (!(e is SamSite)) continue;
                UnityEngine.Object.DestroyImmediate(e.gameObject.GetComponent<AirForce>());
            }
        }

        private void OnEntitySpawned(BaseNetworkable e)
        {
            if (!(e is SamSite) || e.gameObject.GetComponent<AirForce>() != null) return;

            SamSite s = (SamSite)e;
            s.gameObject.AddComponent<AirForce>();
        }

        #endregion

        #region CONFIG

        private static Dictionary<string, object> config;

        private void Init()
        {
            config = Config.ReadObject<Dictionary<string, object>>().OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);

            if (config != null && config.Count == AirForceExtendedConfiguration.Count) return;

            Debug.LogError($"Обнаружено несоответствие в конфигурационном файле плагина \"{Name}\".\nДля восстановления удалите конфигурационный файл и загрузите плагин.");
            NextTick(() => Server.Command($"o.unload {Name}"));
        }

        private Dictionary<string, object> AirForceExtendedConfiguration = new Dictionary<string, object>
        {
            { "1. Запретить атаковать минивертолеты?", false },
            { "2. Запретить атаковать воздушные шары?", false },
            { "3. Запретить атаковать остальное? (SamSite ведет огонь по патрульному вертолету и другим ивентам.)", true },
            { "4. Запретить атаковать воздушные средства если в них находится игрок авторизованный в шкафу? (Турель должна быть привязана к строению со шкафом.)", true },
            { "5. Запретить атаковать администраторов?", true },
            { "6. Включить звуковое оповещение пассажирам при атаке SamSite?", true },
            { "7. Радиус атаки SamSite.", 350 }
        };

        protected override void LoadDefaultConfig()
        {
            Debug.LogError("Обнаружено отсутствие конфигурационного файла, создаю...");
            Debug.LogWarning($"Спасибо за приобретение \"{Name}\".");
            Debug.Log("Надеюсь на вашу оценку в случае вашего удовлетворения плагином, удачи!");
            Config.WriteObject(AirForceExtendedConfiguration.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value), true);
        }

        #endregion

        #region CORE

        private class AirForce : MonoBehaviour
        {
            private SamSite s;
            private BaseCombatEntity lastTarget;
            private bool lastState;
            private List<BaseCombatEntity> list;

            private void Awake()
            {
                s = GetComponent<SamSite>();

                s.scanRadius = Convert.ToInt32(config.ElementAt(6).Value);
                s.CancelInvoke(s.TargetScan);
                s.InvokeRandomized(TargetScanExtended, 1f, 3f, 1f);
                s.InvokeRandomized(Validate, 1f, 3f, 1f);
                s.UpdateOutputs();
                s.Reload();
            }

            private void OnDestroy()
            {
                s.CancelInvoke(TargetScanExtended);
                s.CancelInvoke(Validate);
                s.CancelInvoke(s.WeaponTick);

                if (lastState) OnTargetLosted(lastTarget);

                s.InvokeRandomized(s.TargetScan, 1f, 3f, 1f);
            }

            private void TargetScanExtended()
            {
                if (s.HasValidTarget() || !s.IsPowered() || !HasAmmoExtended() || s.IsDead()) return;

                if (list == null) list = new List<BaseCombatEntity>();
                else list.Clear();
                Vis.Entities(s.eyePoint.transform.position, s.scanRadius, list, Rust.Layers.Server.VehiclesSimple, QueryTriggerInteraction.Ignore);
                BaseCombatEntity endTarget = null;

                foreach (BaseCombatEntity mayTarget in list)
                {
                    if (!CanAttack(mayTarget)) continue;
                    endTarget = mayTarget;

                    break;
                }

                bool flag1 = endTarget != null && s.currentTarget != endTarget;
                s.currentTarget = endTarget;

                if (flag1)
                {
                    lastTarget = endTarget;
                    lastState = true;
                    OnTargetFounded(s.currentTarget);
                }

                Facepunch.Pool.FreeList(ref list);
                if (s.currentTarget == null) s.CancelInvoke(s.WeaponTick);
                else s.InvokeRandomized(s.WeaponTick, flag1 ? 0.5f : 0.0f, 0.5f, 0.2f);
            }

            private bool CanAttack(BaseCombatEntity e)
            {
                if (e == null) return false;

                if (Interface.CallHook("CanBeAttackedSamSite", s, e) != null) return false;

                if ((bool)config.Values.ElementAt(0) && e is MiniCopter) return false;

                if ((bool)config.Values.ElementAt(1) && e is HotAirBalloon) return false;

                if ((bool)config.Values.ElementAt(2) && !(e is MiniCopter) && !(e is HotAirBalloon)) return false;

                if ((double)(s.EntityCenterPoint(e) + new Vector3(-101, 0, 35)).y < s.eyePoint.transform.position.y) return false;

                if (!e.IsVisible(s.eyePoint.transform.position, s.scanRadius)) return false;

                List<BasePlayer> list = GetPlayers(e);

                if (list.Count > 0)
                {
                    foreach (BasePlayer d in list) if (!CanBeAttackedSam(d)) return false;
                }
                else return false;

                return true;
            }

            private void Validate()
            {
                if (!HasAmmoExtended()) s.Reload();

                if (lastState && s.currentTarget == null)
                {
                    OnTargetLosted(lastTarget);
                    lastTarget = null;
                    lastState = false;
                    return;
                }

                if (s.currentTarget != null && (!HasAmmoExtended() || !CanAttack(s.currentTarget)))
                {
                    OnTargetLosted(s.currentTarget);
                    s.currentTarget = null;
                    lastTarget = null;
                    lastState = false;
                    s.CancelInvoke(s.WeaponTick);
                }
            }

            private static List<BasePlayer> GetPlayers(BaseCombatEntity e)
            {
                List<BasePlayer> list = new List<BasePlayer>();

                if (e is HotAirBalloon)
                {
                    HotAirBalloon hab = (HotAirBalloon)e;
                    Vis.Entities(hab.transform.position + new Vector3(0, 2f, 0), 2, list, Rust.Layers.Server.Players);
                }
                else if (e is MiniCopter)
                {
                    MiniCopter mc = (MiniCopter)e;
                    list = mc.mountPoints.Select(seat => seat.mountable.GetMounted()).Where(d => d != null).ToList();
                }

                return list;
            }

            private bool CanBeAttackedSam(BasePlayer d) => (!d.IsAdmin || !(bool)config.Values.ElementAt(4)) && (!(bool)config.Values.ElementAt(3) || s.GetBuildingPrivilege()?.authorizedPlayers.Find(x => x.userid == d.userID) == null);

            private bool HasAmmoExtended() => s.staticRespawn || s.HasAmmo();

            private void OnTargetFounded(BaseCombatEntity e) => Interface.CallHook("OnTargetFoundedAFE", e, s);

            private void OnTargetLosted(BaseCombatEntity e) => Interface.CallHook("OnTargetLostedAFE", e, s);
        }

        #endregion

        #region ADDITIONAL CONTENT

        private List<BaseCombatEntity> activeList = new List<BaseCombatEntity>();

        private void OnTargetFoundedAFE(BaseCombatEntity e, SamSite s)
        {
            if (e == null || !(bool)config.Values.ElementAt(5) || activeList.Contains(e)) return;

            ServerMgr.Instance.StartCoroutine(AirAlert(e));
        }

        private IEnumerator AirAlert(BaseCombatEntity e)
        {
            activeList.Add(e);

            for (int i = 0; i < 4; i++)
            {
                if (e != null) Effect.server.Run("assets/prefabs/npc/autoturret/effects/targetacquired.prefab", e.transform.position);
                yield return new WaitForSeconds(0.5f);
            }

            if (e != null) activeList.Remove(e);
        }

        #endregion
    }
}