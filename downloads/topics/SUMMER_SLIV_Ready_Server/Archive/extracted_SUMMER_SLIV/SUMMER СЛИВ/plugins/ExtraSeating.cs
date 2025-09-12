using System;
using UnityEngine;

namespace Oxide.Plugins {
    [Info("Extra Seating", "Pho3niX90", "1.0.1")]
    [Description("Allows extra seats on minicopters and horses")]
    class ExtraSeating : RustPlugin {
        #region Config
        public PluginConfig config;
        static ExtraSeating _instance;
        int seats = 0;

        protected override void LoadDefaultConfig() { Config.WriteObject(GetDefaultConfig(), true); }
        public PluginConfig GetDefaultConfig() { return new PluginConfig { EnableMiniSideSeats = true, EnableMiniBackSeat = true, EnableExtraHorseSeat = true }; }
        public class PluginConfig { public bool EnableMiniSideSeats; public bool EnableMiniBackSeat; public bool EnableExtraHorseSeat; }
        #endregion
        private void Loaded() {
            config = Config.ReadObject<PluginConfig>();
        }
        void OnEntitySpawned(BaseNetworkable entity) {
            _instance = this;
            if (entity == null || !(entity is MiniCopter)) return;
            BaseVehicle vehicle = entity as BaseVehicle;
            seats = vehicle.mountPoints.Length; // default

            if (entity is MiniCopter && entity.ShortPrefabName.Equals("minicopter.entity")) {

                if (_instance.config.EnableMiniSideSeats) seats += 2;
                if (_instance.config.EnableMiniBackSeat) seats += 1;

                if (vehicle.mountPoints.Length < seats)
                    vehicle?.gameObject.AddComponent<Seating>();
            }
        }

        void AddSeat(BaseVehicle ent, Vector3 locPos, Quaternion q) {
            BaseEntity seat = GameManager.server.CreateEntity("assets/prefabs/vehicle/seats/passengerchair.prefab", ent.transform.position, q) as BaseEntity;
            if (seat == null) return;

            seat.SetParent(ent);
            seat.Spawn();
            seat.transform.localPosition = locPos;
            seat.SendNetworkUpdateImmediate(true);
        }

        BaseVehicle.MountPointInfo CreateMount(Vector3 vec, BaseVehicle.MountPointInfo exampleSeat, Vector3 rotation) {
            return new BaseVehicle.MountPointInfo {
                pos = vec,
                rot = rotation != null ? rotation : new Vector3(0, 0, 0),
                prefab = exampleSeat.prefab,
                mountable = exampleSeat.mountable
            };
        }

        #region Classes
        class Seating : MonoBehaviour {
            public BaseVehicle entity;
            void Awake() {
                entity = GetComponent<BaseVehicle>();
                bool isMini = entity is MiniCopter;
                Vector3 emptyVector = new Vector3(0, 0, 0);

                if (isMini) {
                    _instance.Puts("Minicopter detected");
                }

                if (entity == null) { Destroy(this); return; }

                BaseVehicle.MountPointInfo pilot = entity.mountPoints[0];

                Array.Resize(ref entity.mountPoints, _instance.seats);

                if (entity is MiniCopter) {
                    BaseVehicle.MountPointInfo pFront = entity.mountPoints[1];
                    Vector3 leftVector = new Vector3(0.6f, 0.2f, -0.2f);
                    Vector3 rightVector = new Vector3(-0.6f, 0.2f, -0.2f);
                    Vector3 backVector = new Vector3(0.0f, 0.4f, -1.2f);
                    Vector3 backVector2 = new Vector3(0.0f, 0.4f, -1.45f);

                    Vector3 playerOffsetVector = new Vector3(0f, 0f, -0.25f);
                    Quaternion backQuaternion = Quaternion.Euler(0f, 180f, 0f);

                    if (_instance.config.EnableMiniSideSeats) {
                        _instance.Puts("Adding side seats");
                        BaseVehicle.MountPointInfo pLeftSide = _instance.CreateMount(leftVector, pFront, emptyVector);
                        BaseVehicle.MountPointInfo pRightSide = _instance.CreateMount(rightVector, pFront, emptyVector);
                        entity.mountPoints[2] = pLeftSide;
                        entity.mountPoints[3] = pRightSide;
                        _instance.AddSeat(entity, leftVector + playerOffsetVector, new Quaternion());
                        _instance.AddSeat(entity, rightVector + playerOffsetVector, new Quaternion());
                    }

                    if (_instance.config.EnableMiniBackSeat) {
                        _instance.Puts("Adding back/rotor seat");
                        BaseVehicle.MountPointInfo pBackReverse = _instance.CreateMount(backVector2, pFront, new Vector3(0f, 180f, 0f));
                        entity.mountPoints[_instance.seats - 1] = pBackReverse;
                        _instance.AddSeat(entity, backVector, backQuaternion);
                    }
                }

            }
        }
        #endregion
    }
}

