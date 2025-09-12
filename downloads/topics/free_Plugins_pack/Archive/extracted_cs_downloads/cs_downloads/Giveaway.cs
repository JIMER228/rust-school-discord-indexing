using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Giveaway System", "Termin", "1.0.0")]
    public class Giveaway : RustPlugin
    {
        private Timer animationTimer;
        private int currentIteration = 0;
        private const int TotalIterations = 40;
        private const float ScrollDelay = 0.3f;
        private List<BasePlayer> participants = new List<BasePlayer>();
        private BasePlayer winner;

        private readonly string[] scrollSounds = {
            "assets/prefabs/misc/xmas/presents/effects/present-shake.prefab",
            "assets/prefabs/misc/xmas/presents/effects/present-unwrap.prefab",
            "assets/prefabs/npc/sam_site_turret/effects/tube-start.prefab"
        };
        private readonly string winnerSound = "assets/prefabs/misc/xmas/sleigh/effects/present-impact.prefab";
        private readonly string startSound = "assets/prefabs/misc/easter/painted eggs/effects/egg-upgrade.prefab";

        void OnServerInitialized()
        {
            permission.RegisterPermission("giveaway.admin", this);
        }

        [ChatCommand("giveaway")]
        private void GiveawayCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "giveaway.admin"))
            {
                player.ChatMessage("You don't have permission to use this command!");
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage("Commands:");
                player.ChatMessage("/giveaway start - Start the giveaway");
                player.ChatMessage("/giveaway stop - Stop the giveaway");
                return;
            }

            switch (args[0].ToLower())
            {
                case "start":
                    StartGiveaway();
                    break;
                case "stop":
                    StopGiveaway();
                    break;
            }
        }

        private void StartGiveaway()
        {
            participants = BasePlayer.activePlayerList.Where(p => p.IsConnected).ToList();
            if (participants.Count < 2)
            {
                ShowTip("Not enough players for giveaway!", 5f);
                return;
            }

            currentIteration = 0;
            winner = null;
            ShowTip("<color=#FFA500>🎲 PERSIAN TOXIC GIVEAWAY STARTED! 🎲</color>", 3f);

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.IsConnected)
                {
                    PlaySound(startSound, player.transform.position);
                }
            }

            animationTimer = timer.Every(ScrollDelay, () => AnimateNames());
        }

        private void AnimateNames()
        {
            if (currentIteration >= TotalIterations)
            {
                if (winner == null)
                {
                    winner = participants[UnityEngine.Random.Range(0, participants.Count)];
                    ShowWinner();
                }
                return;
            }

            currentIteration++;

            if (currentIteration % 2 == 0)
            {
                string randomSound = scrollSounds[UnityEngine.Random.Range(0, scrollSounds.Length)];
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player.IsConnected)
                    {
                        PlaySound(randomSound, player.transform.position);
                    }
                }
            }

            var randomPlayer = participants[UnityEngine.Random.Range(0, participants.Count)];
            string message = $"<color=#FFA500>🎲 PERSIAN TOXIC GIVEAWAY 🎲</color>\n" +
                           $"<color=#00FF00>👉 {randomPlayer.displayName}</color>";

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.IsConnected)
                {
                    player.SendConsoleCommand("gametip.hidegametip");
                    timer.Once(0.1f, () => {
                        if (player.IsConnected)
                        {
                            player.SendConsoleCommand("gametip.showgametip", message);
                        }
                    });
                }
            }
        }

        private void ShowWinner()
        {
            if (winner == null || !winner.IsConnected) return;

            animationTimer?.Destroy();

            timer.Once(0.5f, () => {
                string winnerMessage = $"<color=#FFA500>🎉 GIVEAWAY WINNER:</color>\n<color=#00FF00>{winner.displayName}</color>";
                
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player.IsConnected)
                    {
                        player.SendConsoleCommand("gametip.hidegametip");
                        timer.Once(0.1f, () => {
                            if (player.IsConnected)
                            {
                                player.SendConsoleCommand("gametip.showgametip", winnerMessage);
                            }
                        });

                        PlaySound(winnerSound, player.transform.position);
                        
                        if (player == winner)
                        {
                            Effect.server.Run("assets/prefabs/misc/xmas/presents/effects/present-unwrap.prefab", player.transform.position);
                            Effect.server.Run("assets/prefabs/misc/halloween/cursed_cauldron/effects/cauldron-upgrade-success.prefab", player.transform.position);
                        }
                    }
                }

                timer.Once(2f, () => {
                    Server.Broadcast($"<color=#FFA500>🎉 CONGRATULATIONS!</color> <color=#00FF00>{winner.displayName}</color> <color=#FFA500>won the giveaway!</color>");
                });

                timer.Once(5f, () => {
                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        if (player.IsConnected)
                        {
                            player.SendConsoleCommand("gametip.hidegametip");
                        }
                    }
                });
            });
        }

        private void StopGiveaway()
        {
            animationTimer?.Destroy();
            
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.IsConnected)
                {
                    player.SendConsoleCommand("gametip.hidegametip");
                    timer.Once(0.1f, () => {
                        if (player.IsConnected)
                        {
                            player.SendConsoleCommand("gametip.showgametip", "<color=#FF0000>❌ GIVEAWAY STOPPED! ❌</color>");
                        }
                    });
                    PlaySound("assets/prefabs/npc/scientist/sound/scientist-death.prefab", player.transform.position);
                }
            }

            timer.Once(3f, () => {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player.IsConnected)
                    {
                        player.SendConsoleCommand("gametip.hidegametip");
                    }
                }
            });
        }

        private void PlaySound(string soundPath, Vector3 position)
        {
            Effect.server.Run(soundPath, position);
        }

        private void ShowTip(string message, float duration)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.IsConnected)
                {
                    player.SendConsoleCommand("gametip.hidegametip");
                    timer.Once(0.1f, () => {
                        if (player.IsConnected)
                        {
                            player.SendConsoleCommand("gametip.showgametip", message);
                        }
                    });
                }
            }

            if (duration > 0)
            {
                timer.Once(duration, () => {
                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        if (player.IsConnected)
                        {
                            player.SendConsoleCommand("gametip.hidegametip");
                        }
                    }
                });
            }
        }

        void Unload()
        {
            animationTimer?.Destroy();
        }
    }
}