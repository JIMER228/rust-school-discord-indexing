using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("RocketBarrage", "Avalon#3216", "1.1.0")]
    [Description("Allows players to shoot rockets with a chat command.")]
    public class RocketBarrage : RustPlugin
    {
        private const string RocketPrefabPath = "assets/prefabs/ammo/rocket/rocket_basic.prefab";
        private const string IncendiaryRocketPrefabPath = "assets/prefabs/ammo/rocket/rocket_fire.prefab";
        private const string HighVelocityRocketPrefabPath = "assets/prefabs/ammo/rocket/rocket_hv.prefab";
        private const float RocketDelay = 0.5f; // Delay in seconds between each rocket shot

        private HashSet<ulong> rocketCounters = new HashSet<ulong>();
        private Dictionary<ulong, float> rocketDelays = new Dictionary<ulong, float>();
        private Dictionary<ulong, string> rocketTypes = new Dictionary<ulong, string>();

        private const string PermissionRocketBarrage = "rocketbarrage.use";
        private const string CommandBarrage = "Barrage";
        private const string CommandSetRocketDelay = "rbt";
        private const string CommandSelectRocket = "rocket";
        private const string CommandCancelBarrage = "rbc";

        private void Init()
        {
            permission.RegisterPermission(PermissionRocketBarrage, this);
        }

        [ChatCommand(CommandBarrage)]
        private void ChatCommandShootRockets(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionRocketBarrage))
            {
                SendReply(player, "You don't have permission to use this command.");
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, "Usage: /Barrage <quantity>");
                return;
            }

            int quantity;
            if (!int.TryParse(args[0], out quantity) || quantity <= 0)
            {
                SendReply(player, "Invalid quantity. Please enter a positive number.");
                return;
            }

            if (rocketCounters.Contains(player.userID))
            {
                SendReply(player, "You are already shooting rockets. Wait for the current rockets to finish or use /rbc to cancel.");
                return;
            }

            rocketCounters.Add(player.userID);
            NextRocket(player, quantity);

            SendReply(player, "<color=red>Rocket Barrage <color=white>Initiated...</color>");
        }

        [ChatCommand(CommandSetRocketDelay)]
        private void ChatCommandSetRocketDelay(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionRocketBarrage))
            {
                SendReply(player, "You don't have permission to use this command.");
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, "Usage: /rbt <level>");
                return;
            }

            float delayLevel;
            if (!float.TryParse(args[0], out delayLevel) || delayLevel < 1f || delayLevel > 5f)
            {
                SendReply(player, "Invalid delay level. Please enter a number between 1 and 5.");
                return;
            }

            float delay = delayLevel * 0.1f;
            rocketDelays[player.userID] = delay;

            SendReply(player, $"<color=white>Rocket delay set to <color=red>{delayLevel}</color> seconds...</color>");
        }

        [ChatCommand(CommandSelectRocket)]
        private void ChatCommandSelectRocket(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionRocketBarrage))
            {
                SendReply(player, "You don't have permission to use this command.");
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, "Usage: /rocket <type>");
                return;
            }

            string rocketType = args[0].ToLower();
            string prefabPath = GetRocketPrefabPath(rocketType);
            if (prefabPath == null)
            {
                SendReply(player, "Invalid rocket type. Available types: rocket, incin, hv");
                return;
            }

            rocketTypes[player.userID] = rocketType;

            SendReply(player, $"<color=white>Rocket type set to <color=red>{rocketType}</color>.</color>");
        }

        [ChatCommand(CommandCancelBarrage)]
        private void ChatCommandCancelBarrage(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionRocketBarrage))
            {
                SendReply(player, "You don't have permission to use this command.");
                return;
            }

            if (!rocketCounters.Contains(player.userID))
            {
                SendReply(player, "There is no active rocket barrage to cancel.");
                return;
            }

            rocketCounters.Remove(player.userID);
            rocketDelays.Remove(player.userID);
            rocketTypes.Remove(player.userID);

            SendReply(player, "<color=red>Rocket Barrage <color=white>Cancelled.</color>");
        }

        private string GetRocketPrefabPath(string rocketType)
        {
            switch (rocketType)
            {
                case "rocket":
                    return RocketPrefabPath;
                case "incin":
                    return IncendiaryRocketPrefabPath;
                case "hv":
                    return HighVelocityRocketPrefabPath;
                default:
                    return null;
            }
        }

        private void NextRocket(BasePlayer player, int remainingRockets)
        {
            if (!rocketCounters.Contains(player.userID))
                return;

            ShootRocket(player);
            remainingRockets--;

            if (remainingRockets <= 0)
            {
                rocketCounters.Remove(player.userID);
                rocketDelays.Remove(player.userID);
                rocketTypes.Remove(player.userID);
                return;
            }

            rocketCounters.Add(player.userID);
            float delay = GetRocketDelay(player);
            timer.Once(delay, () => NextRocket(player, remainingRockets));
        }

        private void ShootRocket(BasePlayer player)
        {
            string rocketType = GetPlayerRocketType(player);
            string prefabPath = GetRocketPrefabPath(rocketType);

            var rocket = GameManager.server.CreateEntity(prefabPath, player.eyes.position, Quaternion.LookRotation(player.eyes.HeadForward()));
            if (rocket != null)
            {
                rocket.Spawn();
                var projectile = rocket.GetComponent<ServerProjectile>();
                if (projectile != null)
                {
                    projectile.InitializeVelocity(player.eyes.HeadForward() * projectile.speed);
                }
            }
        }

        private float GetRocketDelay(BasePlayer player)
        {
            float delay;
            if (rocketDelays.TryGetValue(player.userID, out delay))
                return delay;
            else
                return RocketDelay;
        }

        private string GetPlayerRocketType(BasePlayer player)
        {
            string rocketType;
            if (rocketTypes.TryGetValue(player.userID, out rocketType))
                return rocketType;
            else
                return "rocket"; // Default rocket type if not found
        }
    }
}
