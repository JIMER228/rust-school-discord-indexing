using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Minicopter Nitro", "RIPJAWBONES", "1.0.0")]
    [Description("Позволяет игрокам использовать нитро-ускорение на миникоптере и скрэп-вертолёте")]
    public class MinicopterNitro : RustPlugin
    {
        private const string Permission = "minicopternitros.use";
        private const string NoCooldownPermission = "minicopternitros.nocooldown";

        private class NitroConfig
        {
            public float CooldownSeconds = 30f;
            public string EffectPrefab =
                "assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab";
            public bool Enabled = true;
            public float MinimumGroundDistance = 15f;
            public float ModifiedHelicopterSpeed = 2f;
            public int NitroButton = 128; // SPRINT
            public float NitroDurationSeconds = 2f;
            public float VelocityModifier = 5f;
        }

        private NitroConfig DefaultConfig = new NitroConfig();
        private Dictionary<string, NitroConfig> heliConfigs = new Dictionary<string, NitroConfig>();

        private Dictionary<ulong, float> cooldowns = new Dictionary<ulong, float>();

        private static readonly HashSet<string> GroundOrWaterVehicles = new HashSet<string>
        {
            "modularcar",
            "bradleyapc",
            "snowmobile",
            "tomahasnowmobile",
            "rhib",
            "rowboat",
            "kayak",
            "sedantest",
            "workcart",
            "workcart_aboveground",
            "workcart_aboveground2",
            "workcart_aboveground3",
            "workcart_aboveground4",
            "workcart_aboveground5",
            "workcart_aboveground6",
            "workcart_aboveground7",
            "workcart_aboveground8",
            "workcart_aboveground9",
            "workcart_aboveground10",
            "workcart_aboveground11",
            "workcart_aboveground12",
            "workcart_aboveground13",
            "workcart_aboveground14",
            "workcart_aboveground15",
            "workcart_aboveground16",
            "workcart_aboveground17",
            "workcart_aboveground18",
            "workcart_aboveground19",
            "workcart_aboveground20",
            "tugboat",
            "submarineduo",
            "submarinesolo",
            "motorboat",
            "pedalbike",
            "motorbike",
            "motorbike_sidecar",
            "pedaltrike",
            "trainlocomotive",
            "trainwagon",
            "trainwagonunloadable",
            "trainwagondouble",
            "trainwagonfuel",
            "trainwagonloot",
            "trainwagonpassenger",
            "trainwagonresources",
            "trainwagonunloadable",
            "caboose",
            "horse",
        };

        protected override void LoadDefaultConfig()
        {
            Config["Helicopters"] = heliConfigs;
            Config["Default"] = DefaultConfig;
            SaveConfig();
        }

        private void Init()
        {
            permission.RegisterPermission(Permission, this);
            permission.RegisterPermission(NoCooldownPermission, this);
            var configSection = Config["Helicopters"] as Dictionary<string, object>;
            if (configSection != null)
            {
                foreach (var kv in configSection)
                {
                    var nitro = kv.Value as Dictionary<string, object>;
                    if (nitro != null)
                    {
                        var c = new NitroConfig();
                        c.CooldownSeconds = GetFloat(nitro, "CooldownSeconds", c.CooldownSeconds);
                        c.EffectPrefab = GetString(nitro, "EffectPrefab", c.EffectPrefab);
                        c.Enabled = GetBool(nitro, "Enabled", c.Enabled);
                        c.MinimumGroundDistance = GetFloat(
                            nitro,
                            "MinimumGroundDistance",
                            c.MinimumGroundDistance
                        );
                        c.ModifiedHelicopterSpeed = GetFloat(
                            nitro,
                            "ModifiedHelicopterSpeed",
                            c.ModifiedHelicopterSpeed
                        );
                        c.NitroButton = GetInt(nitro, "NitroButton", c.NitroButton);
                        c.NitroDurationSeconds = GetFloat(
                            nitro,
                            "NitroDurationSeconds",
                            c.NitroDurationSeconds
                        );
                        c.VelocityModifier = GetFloat(
                            nitro,
                            "VelocityModifier",
                            c.VelocityModifier
                        );
                        heliConfigs[kv.Key] = c;
                    }
                }
            }
            var defSection = Config["Default"] as Dictionary<string, object>;
            if (defSection != null)
            {
                DefaultConfig.CooldownSeconds = GetFloat(
                    defSection,
                    "CooldownSeconds",
                    DefaultConfig.CooldownSeconds
                );
                DefaultConfig.EffectPrefab = GetString(
                    defSection,
                    "EffectPrefab",
                    DefaultConfig.EffectPrefab
                );
                DefaultConfig.Enabled = GetBool(defSection, "Enabled", DefaultConfig.Enabled);
                DefaultConfig.MinimumGroundDistance = GetFloat(
                    defSection,
                    "MinimumGroundDistance",
                    DefaultConfig.MinimumGroundDistance
                );
                DefaultConfig.ModifiedHelicopterSpeed = GetFloat(
                    defSection,
                    "ModifiedHelicopterSpeed",
                    DefaultConfig.ModifiedHelicopterSpeed
                );
                DefaultConfig.NitroButton = GetInt(
                    defSection,
                    "NitroButton",
                    DefaultConfig.NitroButton
                );
                DefaultConfig.NitroDurationSeconds = GetFloat(
                    defSection,
                    "NitroDurationSeconds",
                    DefaultConfig.NitroDurationSeconds
                );
                DefaultConfig.VelocityModifier = GetFloat(
                    defSection,
                    "VelocityModifier",
                    DefaultConfig.VelocityModifier
                );
            }
        }

        [ChatCommand("nitro")]
        private void NitroCommand(BasePlayer player, string command, string[] args)
        {
            bool isManual = true;
            NitroCommandInternal(player, isManual);
        }

        private void NitroCommandInternal(BasePlayer player, bool isManual)
        {
            if (!permission.UserHasPermission(player.UserIDString, Permission))
            {
                if (isManual)
                    player.ChatMessage("У вас нет разрешения на использование нитро.");
                return;
            }

            var vehicle = player.GetMountedVehicle();
            if (vehicle == null)
            {
                if (isManual)
                    player.ChatMessage("Вы должны быть в транспорте.");
                return;
            }

            var prefab = vehicle.ShortPrefabName;
            NitroConfig config;
            if (!heliConfigs.TryGetValue(prefab, out config))
            {
                config = new NitroConfig();
                heliConfigs[prefab] = config;
                Config["Helicopters"] = heliConfigs;
                SaveConfig();
            }
            if (config == null)
                config = DefaultConfig;

            if (!config.Enabled)
            {
                if (isManual)
                    player.ChatMessage("Нитро отключено для этого транспорта.");
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, NoCooldownPermission))
            {
                float nextTime;
                if (
                    cooldowns.TryGetValue(player.userID, out nextTime)
                    && Time.realtimeSinceStartup < nextTime
                )
                {
                    if (isManual)
                    {
                        float left = Mathf.Ceil(nextTime - Time.realtimeSinceStartup);
                        player.ChatMessage($"Нитро на кулдауне. Осталось: {left} сек.");
                    }
                    return;
                }
            }

            if (
                (prefab == "minicopter.entity" || prefab == "scraptransporthelicopter")
                && GetGroundDistance(vehicle) < config.MinimumGroundDistance
            )
            {
                if (isManual)
                    player.ChatMessage("Слишком низко для нитро!");
                return;
            }

            ActivateNitro(vehicle, config, player, isManual);
        }

        private void ActivateNitro(
            BaseEntity vehicle,
            NitroConfig config,
            BasePlayer player,
            bool isManual
        )
        {
            var rb = vehicle.GetComponent<Rigidbody>();
            if (rb == null)
            {
                if (isManual)
                    player.ChatMessage("Ошибка: не найден Rigidbody транспорта.");
                return;
            }

            rb.velocity += vehicle.transform.forward * config.VelocityModifier;
            Effect.server.Run(config.EffectPrefab, vehicle.transform.position);

            if (!permission.UserHasPermission(player.UserIDString, NoCooldownPermission))
            {
                cooldowns[player.userID] = Time.realtimeSinceStartup + config.CooldownSeconds;
            }

            timer.Once(
                config.NitroDurationSeconds,
                () => {
                    // Можно добавить сброс скорости или эффект окончания нитро, если нужно
                }
            );

            if (isManual)
                player.ChatMessage("Нитро активировано!");
        }

        private float GetGroundDistance(BaseEntity vehicle)
        {
            RaycastHit hit;
            if (
                Physics.Raycast(
                    vehicle.transform.position,
                    Vector3.down,
                    out hit,
                    1000f,
                    LayerMask.GetMask("Terrain", "World", "Default")
                )
            )
            {
                return hit.distance;
            }
            return 1000f;
        }

        private float GetFloat(Dictionary<string, object> dict, string key, float def)
        {
            object val;
            float f;
            if (dict.TryGetValue(key, out val) && float.TryParse(val.ToString(), out f))
                return f;
            return def;
        }

        private int GetInt(Dictionary<string, object> dict, string key, int def)
        {
            object val;
            int i;
            if (dict.TryGetValue(key, out val) && int.TryParse(val.ToString(), out i))
                return i;
            return def;
        }

        private bool GetBool(Dictionary<string, object> dict, string key, bool def)
        {
            object val;
            bool b;
            if (dict.TryGetValue(key, out val) && bool.TryParse(val.ToString(), out b))
                return b;
            return def;
        }

        private string GetString(Dictionary<string, object> dict, string key, string def)
        {
            object val;
            if (dict.TryGetValue(key, out val))
                return val.ToString();
            return def;
        }

        // Новый хук для активации нитро по кнопке
        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || !player.isMounted)
                return;
            var vehicle = player.GetMounted();
            if (vehicle == null)
                return;
            var prefab = vehicle.ShortPrefabName;
            NitroConfig config;
            if (!heliConfigs.TryGetValue(prefab, out config))
            {
                config = new NitroConfig();
                heliConfigs[prefab] = config;
                Config["Helicopters"] = heliConfigs;
                SaveConfig();
            }
            if (config == null)
                config = DefaultConfig;
            if (input.WasJustPressed((BUTTON)config.NitroButton))
            {
                bool isManual = false;
                NitroCommandInternal(player, isManual);
            }
        }
    }
}
