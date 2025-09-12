using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using System;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace Oxide.Plugins
{
    [Info("InstantAirdrop", "Rogder Dodger", "1.0.10")]
    [Description("Instantly Deploys an airdrop at location with no cargo plane")]
    internal class InstantAirdrop : RustPlugin
    {
        private const float DefaultDelayBeforeSpawn = 5f;
        private const string UsePerm = "instantairdrop.use";
        private HashSet<BaseEntity> _permittedSupplySignals = new HashSet<BaseEntity>();
        private HashSet<ulong> _skinIdsToIgnore = new HashSet<ulong>
            { 2912968568, 2912968440, 2912968298, 2912968179, 2912967918, 2912968057, 2912968671 };
        private Configuration _config;
        
        #region Configuration

        private class Configuration
        {
            [JsonProperty(PropertyName = "Delay Before Spawn")]
            public float DelayBeforeSpawn = DefaultDelayBeforeSpawn;

            [JsonProperty(PropertyName = "Use Permission")]
            public bool UsePermission = false;

            [JsonProperty(PropertyName =
                "Prevent Deploying Inside (Players will only be able to deploy supply signals outside)")]
            public bool PreventSpawningInBases = false;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<Configuration>() ?? new Configuration();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("A new configuration file is being generated.");
            _config = new Configuration
            {
                DelayBeforeSpawn = DefaultDelayBeforeSpawn,
                UsePermission = false,
                PreventSpawningInBases = false,
            };
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["cannotThrowInside"] = "<color=#b7b7b7>Supply Signals must be deployed outside</color>",
            }, this);
        }

        string GetLang(string msg, string userID) => lang.GetMessage(msg, this, userID);

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(UsePerm, this);
            _config = Config.ReadObject<Configuration>();
        }

        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity) => HandleSignalThrown(player, entity);

        private void OnExplosiveDropped(BasePlayer player, BaseEntity entity) => HandleSignalThrown(player, entity);

        void OnCargoPlaneSignaled(CargoPlane cargoPlane, SupplySignal supplySignal)
        {
            if (_permittedSupplySignals.Contains(supplySignal))
            {
                cargoPlane.Kill();
                _permittedSupplySignals.Remove(supplySignal);
            }
        }

        #endregion

        #region Methods

        private void HandleSignalThrown(BasePlayer player, BaseEntity entity)
        {
            if (_config.UsePermission && !permission.UserHasPermission(player.UserIDString, UsePerm))
            {
                return;
            }

            if (entity.name.Contains("signal"))
            {
                if (IsCustomSupplySignal(entity)) return;

                _permittedSupplySignals.Add(entity);
                timer.Once(_config.DelayBeforeSpawn, () =>
                {
                    if (entity != null)
                    {
                        if (HandleIfThrowingInsideBase(player, entity)) return;
                        var pos = entity.transform.position;
                        SpawnAirdrop(pos, player.userID.Get());
                        entity.Kill();
                    }
                });
            }
        }

        private bool HandleIfThrowingInsideBase(BasePlayer player, BaseEntity entity)
        {
            if (!_config.PreventSpawningInBases || entity.IsOutside()) return false;
            RefundAndNotify(player, entity);
            return true;
        }

        private void RefundAndNotify(BasePlayer player, BaseEntity entity)
        {
            entity.Kill();
            Player.Message(player, GetLang("cannotThrowInside", player.UserIDString), null);
            var item = ItemManager.CreateByName("supply.signal", 1);
            if (item != null)
            {
                player.inventory.GiveItem(item, player.inventory.containerBelt);
            }
        }

        private void SpawnAirdrop(Vector3 pos, ulong playerId)
        {
            var newEntity = GameManager.server.CreateEntity("assets/prefabs/misc/supply drop/supply_drop.prefab", pos);
            newEntity.OwnerID = playerId;
            newEntity.skinID = 234501;
            newEntity.Spawn();
            newEntity.Invoke(() => newEntity.OwnerID = playerId, 1f);
            var supplyDrop = newEntity as SupplyDrop;
            if (supplyDrop == null)
                return;

            supplyDrop.SetFlag(BaseEntity.Flags.Reserved2, false);
            supplyDrop.RemoveParachute();
            var drop = supplyDrop.GetComponent<Rigidbody>();
            drop.drag = 1f;
        }

        private bool IsCustomSupplySignal(BaseEntity entity)
        {
            if (entity.skinID == 234501)
                return false;
            
            return Interface.CallHook("IsBradleyDrop", entity.skinID) != null
                   || Interface.CallHook("IsHeliSignalObject", entity.skinID) != null
                   || _skinIdsToIgnore.Contains(entity.skinID);
        }

        private bool ShouldFancyDrop()
        {
            return false;
        }

        #endregion
    }
}