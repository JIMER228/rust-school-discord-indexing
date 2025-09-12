using Oxide.Core;
using System;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using ConVar;
using System.IO;
using System.Text;
using Network;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using System.Collections;
using Facepunch;
using Rust;

namespace Oxide.Plugins
{
    [Info("Bimba2000", "sdapro", "1.0.0")]
    [Description("Плагин для создания дыры в здании при выстреле из L96")]
    public class Bimba2000 : RustPlugin
    {
        private Dictionary<BasePlayer, float> lastShotTimes = new Dictionary<BasePlayer, float>();
        private float shotDelay = 0.01f; // Set the minimum delay between shots in seconds

        private void Init()
        {
            permission.RegisterPermission("Bimba2000.rs", this);
            permission.RegisterPermission("Bimba2000.ignore", this);
        }

        // Listen for player ticks
        void OnPlayerTick(BasePlayer player)
        {
            if (player == null || player.IsNpc || !player.IsConnected)
                return;

            // Check for the left mouse button being held down
            if (player.serverInput.IsDown(BUTTON.FIRE_PRIMARY))
            {
                // Check if enough time has passed since the last shot
                if (CanShoot(player))
                {
                    SpawnBoarCorpse(player);
                }
            }
        }

        private bool CanShoot(BasePlayer player)
        {
            float lastShotTime;
            if (lastShotTimes.TryGetValue(player, out lastShotTime))
            {
                // Check if enough time has passed since the last shot
                if (UnityEngine.Time.realtimeSinceStartup - lastShotTime >= shotDelay)
                {
                    lastShotTimes[player] = UnityEngine.Time.realtimeSinceStartup;
                    return true;
                }
                return false;
            }
            // First shot for this player
            lastShotTimes[player] = UnityEngine.Time.realtimeSinceStartup;
            return true;
        }

        private void SpawnBoarCorpse(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, "Bimba2000.rs"))
            {
                // Add a delay of 0.5 seconds before spawning the corpse
                timer.Once(0.01f, () =>
                {
                    var entity = GameManager.server.CreateEntity("assets/rust.ai/agents/boar/boar.corpse.prefab",
                        player.eyes.position - player.eyes.BodyForward().normalized * 0.5f,
                        player.eyes.rotation) as BaseEntity;

                    entity.creatorEntity = (BaseEntity)player;
                    entity.OwnerID = player.userID;
                    entity.Spawn();

                    // Apply force to the corpse
                    ApplyForceToCorpse(entity, player);
                });
            }
        }

        private void ApplyForceToCorpse(BaseEntity entity, BasePlayer player)
        {
            // Get the direction the player is facing
            Vector3 forward = player.eyes.BodyForward().normalized;

            // Get the rigidbody of the corpse
            var rigidBody = entity.GetComponent<Rigidbody>();

            if (rigidBody != null)
            {
                // Zero out the velocity and angular velocity
                rigidBody.velocity = Vector3.zero;
                rigidBody.angularVelocity = Vector3.zero;

                // Calculate the total force to apply (direction * 50.0f + up * 50.0f + forward * 50.0f)
                Vector3 totalForce = (forward * 20.0f) + (Vector3.up * 20.0f);

                // Apply the force to the corpse at its position
                rigidBody.AddForceAtPosition(totalForce, entity.transform.position, ForceMode.VelocityChange);
            }
        }
    }
}
