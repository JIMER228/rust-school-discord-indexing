using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ClearWoodenWalls", "b1xbyy", "1.0.0")]
    public class ClearWoodenWalls : RustPlugin
    {
        private const string PrefabPartialName = "wall.external.high.wood";
        private const float WarningTime = 15f;
        private const float ClearInterval = 1800f;

        private Timer clearTimer;

        void OnServerInitialized()
        {
            ScheduleWallClearing();
        }

        private void ScheduleWallClearing()
        {
            clearTimer = timer.Repeat(ClearInterval, 0, () =>
            {
                BroadcastWarning();
                timer.Once(WarningTime, () =>
                {
                    int removedCount = RemoveAllWoodenWalls();
                    BroadcastMessage($"<color=#9ACD32>[STORM RUST]\n• </color>Очистка завершена: <color=#9ACD32>{removedCount}</color> деревянных стен удалено.");
                });
            });
        }

        private void BroadcastWarning()
        {
            BroadcastMessage("<color=#9ACD32>[STORM RUST]\n• </color>Очистка деревянных стен через <color=#9ACD32>15</color> секунд!");
        }

        private void BroadcastMessage(string message)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                player.ChatMessage(message);
            }
        }

        private int RemoveAllWoodenWalls()
        {
            int totalRemoved = 0;
            {
                int count = 0;

                foreach (var entity in BaseNetworkable.serverEntities)
                {
                    if (entity is BaseEntity baseEntity && baseEntity.PrefabName.Contains(PrefabPartialName))
                    {
                        baseEntity.Kill();
                        count++;
                    }
                }

                totalRemoved += count;
                if (count == 0)
                {
                    //
                }
            }

            return totalRemoved;
        }

        void Unload()
        {
            clearTimer?.Destroy();
        }
    }
}