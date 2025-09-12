using Oxide.Core;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("DynamicTerritoryWars", "EventMaster", "1.0.0")]
    [Description("Creates dynamic territory control battles between players")]
    public class DynamicTerritoryWars : RustPlugin
    {
        private Dictionary<Vector3, Territory> territories = new Dictionary<Vector3, Territory>();
        private Dictionary<ulong, int> playerScores = new Dictionary<ulong, int>();
        private bool eventActive = false;

        private class Territory
        {
            public Vector3 Center;
            public float Radius;
            public ulong ControllingClan;
            public int CaptureProgress;
        }

        private void Init()
        {
            permission.RegisterPermission("territorywars.admin", this);
            GenerateTerritories();
        }

        private void GenerateTerritories()
        {
            for (int i = 0; i < 5; i++)
            {
                Vector3 position = new Vector3(
                    UnityEngine.Random.Range(-2000f, 2000f),
                    0f,
                    UnityEngine.Random.Range(-2000f, 2000f)
                );
                territories[position] = new Territory
                {
                    Center = position,
                    Radius = 50f,
                    ControllingClan = 0,
                    CaptureProgress = 0
                };
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (eventActive)
            {
                CheckEventEnd();
            }
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (!eventActive) return;
            if (entity is BasePlayer victim && info.InitiatorPlayer != null)
            {
                BasePlayer attacker = info.InitiatorPlayer;
                if (victim.currentTeam != attacker.currentTeam)
                {
                    UpdateTerritoryControl(attacker);
                }
            }
        }

        private void UpdateTerritoryControl(BasePlayer player)
        {
            ulong teamId = player.currentTeam;
            if (teamId == 0) return;

            foreach (var territory in territories.Values)
            {
                if (Vector3.Distance(player.transform.position, territory.Center) <= territory.Radius)
                {
                    if (territory.ControllingClan != teamId)
                    {
                        territory.CaptureProgress++;
                        if (territory.CaptureProgress >= 100)
                        {
                            territory.ControllingClan = teamId;
                            territory.CaptureProgress = 0;
                            Print($"{teamId} has captured territory at {territory.Center}");
                        }
                    }
                }
            }
        }

        private void CheckEventEnd()
        {
            Dictionary<ulong, int> clanScores = new Dictionary<ulong, int>();
            foreach (var territory in territories.Values)
            {
                if (territory.ControllingClan != 0)
                {
                    if (!clanScores.ContainsKey(territory.ControllingClan))
                    {
                        clanScores[territory.ControllingClan] = 0;
                    }
                    clanScores[territory.ControllingClan]++;
                }
            }

            if (clanScores.Count > 0)
            {
                ulong winningClan = 0;
                int maxScore = 0;
                foreach (var pair in clanScores)
                {
                    if (pair.Value > maxScore)
                    {
                        maxScore = pair.Value;
                        winningClan = pair.Key;
                    }
                }

                if (winningClan != 0)
                {
                    EndEvent(winningClan);
                }
            }
        }

        private void EndEvent(ulong winningClan)
        {
            eventActive = false;
            Print($"Territory Wars event ended! Winning clan: {winningClan}");
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.currentTeam == winningClan)
                {
                    player.ChatMessage("Your clan has won the Territory Wars event!");
                }
            }
        }

        [ConsoleCommand("territorywars.start")]
        private void StartEventCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !permission.UserHasPermission(arg.Connection.userid.ToString(), "territorywars.admin")) return;
            if (!eventActive)
            {
                eventActive = true;
                Print("Territory Wars event started!");
                foreach (var player in BasePlayer.activePlayerList)
                {
                    player.ChatMessage("Territory Wars event has begun! Fight for control of the territories!");
                }
            }
        }

        [ConsoleCommand("territorywars.status")]
        private void StatusCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null) return;
            var player = arg.Connection.player as BasePlayer;
            if (player == null) return;

            foreach (var territory in territories.Values)
            {
                string status = territory.ControllingClan == 0 ?
                    "Neutral" :
                    $"Controlled by clan {territory.ControllingClan}";
                player.ChatMessage($"Territory at {territory.Center}: {status} ({territory.CaptureProgress}%)");
            }
        }
    }
}