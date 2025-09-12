using Oxide.Core.Plugins;
using UnityEngine;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Teleport Gun", "M&B-Studios", "1.0.0")]
    [Description("Teleport to where you are shooting.")]
    public class TeleportGun : RustPlugin
    {
        [PluginReference]
        Plugin Permissions;

        private const string permissionName = "teleportgun.use";
        private Configuration config;
        private Dictionary<ulong, float> lastTeleportTime = new Dictionary<ulong, float>();
        private Dictionary<ulong, float> lastCombatTime = new Dictionary<ulong, float>();

        private class Configuration
        {
            public bool TeleportCooldownEnabled { get; set; } = false;
            public int TeleportCooldownSeconds { get; set; } = 180;
            public bool TeleportBlockDuringCombat { get; set; } = false;
            public int CombatBlockCooldownSeconds { get; set; } = 180;
            public ulong RequiredTeleportGunSkinId { get; set; } = 2328155651; // Hier die gewünschte SkinId als ulong eintragen

            public static Configuration DefaultConfig()
            {
                return new Configuration();
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file.");
            config = Configuration.DefaultConfig();
            SaveConfig();
        }

        private void Init()
        {
            permission.RegisterPermission(permissionName, this);
            config = Config.ReadObject<Configuration>();
        }

[ChatCommand("portgun")]
private void GiveTeleportGun(BasePlayer player, string command, string[] args)
{
    if (!permission.UserHasPermission(player.UserIDString, permissionName))
    {
        player.ChatMessage("You do not have permission to use this command.");
        return;
    }

    ItemDefinition teleportGunDefinition = ItemManager.FindItemDefinition("lmg.m249");
    if (teleportGunDefinition != null)
    {
        ulong skinId = config.RequiredTeleportGunSkinId;

        Item gun = ItemManager.Create(teleportGunDefinition, 1);
        if (gun != null)
        {
            gun.skin = skinId; // Setze die gewünschte Skin-ID
            player.GiveItem(gun);
            player.ChatMessage("Teleport Gun equipped with the specified skin.");

            // Fügen Sie das 16x Scope hinzu
            var scope = ItemManager.CreateByItemID(ItemManager.FindItemDefinition("weapon.mod.8x.scope").itemid);
            if (scope != null)
            {
                gun.contents.AddItem(scope.info, 1);
            }

            // Fügen Sie das Laservisier hinzu
            var laserSight = ItemManager.CreateByItemID(ItemManager.FindItemDefinition("weapon.mod.lasersight").itemid);
            if (laserSight != null)
            {
                gun.contents.AddItem(laserSight.info, 1);
            }
        }
    }
}
        void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (attacker == null || info == null) return;

            Item activeItem = attacker.GetActiveItem();
            if (activeItem != null && activeItem.info.shortname == "lmg.m249" && activeItem.skin == config.RequiredTeleportGunSkinId)
            {
                // Verhindern Sie, dass die Waffe Schaden verursacht
                info.damageTypes.ScaleAll(0);

                if (CanTeleport(attacker))
                {
                    TeleportPlayerToHitLocation(attacker, info);
                    SetTeleportCooldown(attacker);
                }
                else
                {
                    if (Time.realtimeSinceStartup - (lastTeleportTime.ContainsKey(attacker.userID.Get()) ? lastTeleportTime[attacker.userID.Get()] : 0) <= config.TeleportCooldownSeconds)
                    {
                        attacker.ChatMessage("Teleport is on cooldown. Please wait before teleporting again.");
                    }
                    else if (IsPlayerInCombat(attacker))
                    {
                        attacker.ChatMessage("You cannot teleport while in combat.");
                    }
                }
            }
        }

        private bool CanTeleport(BasePlayer player)
        {
            if (config.TeleportBlockDuringCombat && IsPlayerInCombat(player))
            {
                return false;
            }

            if (config.TeleportCooldownEnabled)
            {
                float lastTeleport;
                if (lastTeleportTime.TryGetValue(player.userID.Get(), out lastTeleport))
                {
                    if (Time.realtimeSinceStartup - lastTeleport < config.TeleportCooldownSeconds)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void SetTeleportCooldown(BasePlayer player)
        {
            if (config.TeleportCooldownEnabled)
            {
                lastTeleportTime[player.userID.Get()] = Time.realtimeSinceStartup;
            }
        }

        private bool IsPlayerInCombat(BasePlayer player)
        {
            float lastCombat;
            if (lastCombatTime.TryGetValue(player.userID.Get(), out lastCombat))
            {
                if (Time.realtimeSinceStartup - lastCombat < config.CombatBlockCooldownSeconds)
                {
                    return true;
                }
                else
                {
                    lastCombatTime.Remove(player.userID.Get());
                }
            }
            return false;
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            BasePlayer player = entity.ToPlayer();
            if (player != null)
            {
                lastCombatTime[player.userID.Get()] = Time.realtimeSinceStartup;
            }
        }

        private void TeleportPlayerToHitLocation(BasePlayer player, HitInfo info)
        {
            Vector3 hitPosition = info.HitPositionWorld;
            player.Teleport(hitPosition);
            player.ChatMessage("Teleported to the targeted location.");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();
        }

        protected override void SaveConfig() => Config.WriteObject(config);
    }
}