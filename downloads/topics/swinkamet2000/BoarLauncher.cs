using Oxide.Core;
using System.Collections.Generic;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Rust;

namespace Oxide.Plugins
{
    [Info("BoarLauncher", "halel", "52.52")]
    [Description("Бегает по полю кобанчэк")]
    public class BoarLauncher : RustPlugin
    {
        private Dictionary<BasePlayer, BaseEntity> launchedBoars = new Dictionary<BasePlayer, BaseEntity>();

        private void Init()
        {
            permission.RegisterPermission("boarlauncher.use", this);
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (input.IsDown(BUTTON.FIRE_PRIMARY))
            {
                if (permission.UserHasPermission(player.UserIDString, "boarlauncher.use"))
                {
                    LaunchBoar(player);
                }
            }
            else if (input.IsUp(BUTTON.FIRE_PRIMARY))
            {
                RemoveLaunchedBoar(player);
            }
        }

        void LaunchBoar(BasePlayer player)
        {
            BaseEntity boar = GameManager.server.CreateEntity("assets/rust.ai/agents/boar/boar.corpse.prefab",
                player.transform.position + player.transform.forward * 2f,
                Quaternion.identity);

            if (boar != null)
            {
                launchedBoars[player] = boar;
                boar.Spawn();
                Rigidbody rigidbody = boar.GetComponent<Rigidbody>();
                if (rigidbody != null)
                {
                    rigidbody.AddForce(player.eyes.HeadForward() * 10f, ForceMode.Impulse);
                }
            }
        }

        void RemoveLaunchedBoar(BasePlayer player)
        {
            if (launchedBoars.TryGetValue(player, out BaseEntity boar))
            {
                if (boar != null && !boar.IsDestroyed)
                {
                    boar.Kill();
                }
                launchedBoars.Remove(player);
            }
        }
    }
}
