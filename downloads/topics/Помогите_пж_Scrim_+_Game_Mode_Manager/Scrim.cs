// Requires: GameModeManager
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Scrim", "discord.gg/9vyTXsJyKR", "1.1.2")]
    [Description("An event that organizes players into teams to eliminate each other and secures victory.")]
    public class Scrim : RustPlugin, GameModeManager.IAdvancedLobby
    {
   		[PluginReference] Plugin ZoneManager;
        private static GameModeManager _arena;
        private static Scrim _instance;
        private Configuration _config;
        private DynamicConfigFile _dataManager;
        private PluginData _pluginData;
        private TeamDeathmatchArenaData _arenaToEdit;
        public Dictionary<ulong, TeamDeathmatchPlayer> Players { get; set; } = new Dictionary<ulong, TeamDeathmatchPlayer>();
        private Dictionary<int, TeamDeathmatchArena> _arenas = new Dictionary<int, TeamDeathmatchArena>();
        #region Classes                 

        #region Config

        public class Configuration
        {
            [JsonProperty(PropertyName = "Chat command for showing the pause menu")]
            public string ShowPauseMenuCommand { get; set; } = "menu";

            [JsonProperty(PropertyName = "Chat command to set player ready")]
            public string ToggleReadyCommand { get; set; } = "ready";

            [JsonProperty(PropertyName = "Countdown Sound Effect")]
            public string CountdownSoundEffect { get; set; } = "assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab";
        }

        #endregion Config

        #region Data
        public class PluginData
        {
            [JsonProperty(PropertyName = "Scrim")]
            public List<TeamDeathmatchArenaData> Arenas { get; set; } = new List<TeamDeathmatchArenaData>();
        }
        public class TeamDeathmatchArenaData : GameModeManager.ArenaData
        {

            [JsonProperty(PropertyName = "Match - Start Countdown", Order = 8)]
            public int MatchStartCountDown { get; set; } = 3;

            [JsonProperty(PropertyName = "Text - Display Name - Blue Team", Order = 19)]
            public string BlueTeamDisplayName { get; set; } = "<color=#33689A>Blue</color>";

            [JsonProperty(PropertyName = "Text - Display name - Red Team", Order = 20)]
            public string RedTeamDisplayName { get; set; } = "<color=#DD4B39>Red</color>";

            [JsonProperty(PropertyName = "Entrance Point - Display Text - Active Match", Order = 16)]
            public string StartedMatchEntranceDisplayText { get; set; } = "$.Name";

            [JsonProperty(PropertyName = "Entrance Point - Display Text - Inactive Match", Order = 17)]
            public string NoMatchEntranceDisplayText { get; set; } = "$.Name";

            [JsonProperty(PropertyName = "Loadout - Spectator", Order = 21)]
            public string SpectatorLoadoutName { get; set; } = "Spectator";

            [JsonProperty(PropertyName = "Loadout - Default - Blue Team", Order = 22)]
            public string BlueTeamDefaultLoadoutName { get; set; } = "AK";

            [JsonProperty(PropertyName = "Loadout - Default - Red Team", Order = 23)]
            public string RedTeamDefaultLoadoutName { get; set; } = "AK";

            [JsonProperty(PropertyName = "Match - Wounded Timeout", Order = 9)]
            public float WoundedTimeout { get; set; } = 0;

            [JsonProperty(PropertyName = "Match - Rounds Count - Default", Order = 10)]
            public int DefaultRoundCount { get; set; } = 3;

            [JsonProperty(PropertyName = "Match - Rounds Count", Order = 11)]
            public int[] RoundsCounts { get; set; } = { 3, 6, 9 };//TODO: Get From Commands

            [JsonProperty(PropertyName = "Loadout List", Order = 24)]
            public List<string> LoadoutNames { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Attachments List", Order = 25)]
            public List<ItemName> ItemsNames { get; set; } = new List<ItemName>();

            [JsonProperty(PropertyName = "Spawn Points", Order = 26)]
            public HashSet<ArenaSpawnData> SpawnLocations { get; set; } = new HashSet<ArenaSpawnData>();

        }
        public class ArenaSpawnData : GameModeManager.BaseSpawnData
        {
            [JsonProperty(PropertyName = "Spawn - Type", Order = 1)]
            public Team SpawnType { get; set; }

            [JsonProperty(PropertyName = "Spawn - Safe Area Radius", Order = 2)]
            public float SafeAreaRadius { get; set; }
            public override bool Equals(object obj)
            {
                var match = obj as ArenaSpawnData;
                if (match == null)
                {
                    return false;
                }

                return match.Name.Equals(Name, StringComparison.OrdinalIgnoreCase);
            }
            public override int GetHashCode()
            {
                return Name.ToLower().GetHashCode();
            }
        }
        public class ItemName
        {
            [JsonProperty(PropertyName = "Item Short Name")]
            public string ItemShortName { get; set; }

            [JsonProperty(PropertyName = "Item Display Name")]
            public string ItemDisplayName { get; set; }
        }
        #endregion Data
        #region Behaviours
        public class SpawnZoneBehaviour : MonoBehaviour
        {
            private HashSet<BasePlayer> Players { get; set; } = new HashSet<BasePlayer>();
            public TeamDeathmatchMatch Match { get; set; }
            public ArenaSpawnData SpawnData { get; set; }
            public static SpawnZoneBehaviour CreateSafeZone(string name, Vector3 safeZoneLocation)
            {
                var sphereObject = new GameObject(name);
                sphereObject.transform.position = safeZoneLocation;
                return sphereObject.AddComponent<SpawnZoneBehaviour>();
            }

            void Update()
            {
                if (Match == null || SpawnData == null || SpawnData.SafeAreaRadius <= 0)
                {
                    return;
                }

                if (Match.IsStarted && SpawnData.SpawnType != Team.Spectator)
                {
                    return;
                }

                foreach (var basePlayer in Players)
                {
                    if (basePlayer == null || !_instance.Players.ContainsKey(basePlayer.userID))
                    {
                        continue;
                    }

                    if (_instance.permission.UserHasPermission(basePlayer.UserIDString, GameModeManager.PermissionIgnore))
                        continue;

                    if (_arena.TeleportingPlayers.Contains(basePlayer.userID))
                        continue;

                    var offset = basePlayer.ServerPosition - SpawnData.SpawnPoint;
                    var sqrLen = offset.sqrMagnitude;
                    // square the distance we compare with
                    if (sqrLen > SpawnData.SafeAreaRadius * SpawnData.SafeAreaRadius)
                    {
                        _arena.Teleport(basePlayer, SpawnData.SpawnPoint, false, false);
                    }
                }
            }

            public void Initialize(ArenaSpawnData spawnData, TeamDeathmatchMatch match)
            {
                SpawnData = spawnData;
                Match = match;
            }

            public void AddPlayer(BasePlayer player)
            {
                Players.Add(player);
            }

            public void RemovePlayer(BasePlayer player)
            {
                Players.Remove(player);
            }

            public void ClearPlayers()
            {
                Players.Clear();
            }
            public void DoDestroy()
            {
                Destroy(this);
            }
        }
        #endregion Behaviours
        public class TeamDeathmatchArena : GameModeManager.Arena
        {
            public TeamDeathmatchArena(GameModeManager.Lobby lobby, TeamDeathmatchArenaData arenaData) : base(lobby, arenaData)
            {
                Data = arenaData;
                Match = new TeamDeathmatchMatch(this);
            }

            public new TeamDeathmatchMatch Match { get; set; }
            public new TeamDeathmatchArenaData Data { get; set; }

            public override string GetEntranceText()
            {
                var text = Match.IsStarted ?
                    Data.StartedMatchEntranceDisplayText :
                    Data.NoMatchEntranceDisplayText;

                var formattedText = text
                    .Replace("$.Name", base.Data.Name)
                    .Replace("$.Capacity", base.Data.Capacity.ToString())
                    .Replace("$.MatchType", base.Data.MatchType.ToString())
                    .Replace("$.BlueCount", Match.Players.Count(x => x.AllowPlaying && x.Team == Team.Blue).ToString())
                    .Replace("$.RedCount", Match.Players.Count(x => x.AllowPlaying && x.Team == Team.Red).ToString())
                    .Replace("$.Blue", Data.BlueTeamDisplayName)
                    .Replace("$.Red", Data.RedTeamDisplayName)
                    .Replace("$.SpectatorCount", Match.Players.Count(x => !x.AllowPlaying || x.Team == Team.Spectator).ToString())
                    .Replace("$.TotalCount", Match.Players.Count.ToString());
                return formattedText;
            }
            public override void TimerTick()
            {
                Match.MatchTimer();
            }
            public List<SpawnZoneBehaviour> SpawnZones { get; set; } = new List<SpawnZoneBehaviour>();
            public Vector3 GetRandomSpawnLocationForPlayer(BasePlayer player, Team? team = null)
            {
                if (player == null)
                    return Vector3.zero;

                var validSpawnBehaviours = SpawnZones.Where(x => x.SpawnData != null && x.SpawnData.IsEnabled &&
                                                                 (!team.HasValue || x.SpawnData.SpawnType == team.Value)).ToList();

                if (!validSpawnBehaviours.Any())
                {
                    if (Zone == null)
                    {
                        _instance.PrintError("No Zone Found");
                        return Vector3.zero;
                    }

                    return Zone.Location;
                }

                var tryCount = 0;
                var randomChance = UnityEngine.Random.Range(0.1f, 100);
                var spawnBehaviour = validSpawnBehaviours.Where(x => x.SpawnData.SpawnChance >= randomChance)
                    .ToList().GetRandom();
                Vector2? randomPos = null;
                if (spawnBehaviour != null && spawnBehaviour.SpawnData.Radius > 0)
                {
                    while (tryCount < 20)
                    {
                        randomPos = UnityEngine.Random.insideUnitCircle * spawnBehaviour.SpawnData.Radius;
                        var entities = Facepunch.Pool.GetList<BaseEntity>();
                        Vis.Entities(randomPos.Value, 10, entities, LayerMask.GetMask("Construction", "Deployable"));
                        var count = entities.Count;
                        Facepunch.Pool.FreeList(ref entities);
                        if (count == 0)
                        {
                            break;
                        }
                        randomPos = null;
                        tryCount++;
                    }
                }
                else
                {
                    randomPos = Vector2.zero;
                }

                foreach (var spawnZone in SpawnZones)
                {
                    spawnZone.RemovePlayer(player);
                }
                if (spawnBehaviour != null)
                    spawnBehaviour.AddPlayer(player);
                return randomPos.HasValue && spawnBehaviour != null
                    ? new Vector3(spawnBehaviour.SpawnData.SpawnPoint.x + randomPos.Value.x, spawnBehaviour.SpawnData.SpawnPoint.y, spawnBehaviour.SpawnData.SpawnPoint.z + randomPos.Value.y)
                    : Vector3.zero;
            }
        }
        public class TeamDeathmatchMatch : GameModeManager.Match
        {
            public TeamDeathmatchMatch(TeamDeathmatchArena arena) : base(arena)
            {
                Arena = arena;
                BlueTeam = RelationshipManager.ServerInstance.CreateTeam();
                RedTeam = RelationshipManager.ServerInstance.CreateTeam();
                BlueTeamSelectedLoadoutName = arena.Data.BlueTeamDefaultLoadoutName;
                RedTeamSelectedLoadoutName = arena.Data.RedTeamDefaultLoadoutName;
                RoundCount = arena.Data.DefaultRoundCount;
            }

            public new TeamDeathmatchArena Arena { get; set; }
            private int _remainingTime;
            private int _currentRound;
            private int _countDown;
            private int _roundsWonByBlue = 0;
            private int _roundsWonByRed = 0;
            public new HashSet<TeamDeathmatchPlayer> Players { get; set; } = new HashSet<TeamDeathmatchPlayer>();
            public RelationshipManager.PlayerTeam BlueTeam { get; set; }
            public RelationshipManager.PlayerTeam RedTeam { get; set; }
            public int RoundTime { get; set; } = 5;
            public int RoundCount { get; set; }
            public string BlueTeamSelectedLoadoutName { get; set; }
            public string RedTeamSelectedLoadoutName { get; set; }
            public HashSet<string> Items { get; set; } = new HashSet<string>();

            public void MatchTimer()
            {
                ManagePlayers();

                if (!IsCountDownActivated)
                    return;
                if (_countDown > 0)
                {
                    var countdown = TimeSpan.FromSeconds((float)_countDown / 2);
                    //var countdownText = _countDown <= 1 ? "GO!" : $"{countdown.Seconds}";
                    if (countdown.Seconds > 0)
                    {
                        foreach (var matchPlayer in Players)
                        {
                            _instance.ShowTeamDeathmatchCountDownGui(matchPlayer.Player, $"{countdown.Seconds}",
                                _countDown % 2 != 0);
                        }
                    }
                    else
                    {
                        ClearTimerGui();
                    }

                    _countDown--;
                    return;
                }

                if (_remainingTime > 0)
                {
                    var timer = TimeSpan.FromSeconds((float)_remainingTime / 2);
                    var timerText = $"{Math.Floor(timer.TotalMinutes):00}:{timer.Seconds:00}";
                    //var roundCount = RoundCount > 0 ? RoundCount.ToString() : "∞";
                    var roundText = RoundCount > 0 ? $"{_currentRound}/{RoundCount}" : string.Empty;
                    foreach (var matchPlayer in Players)
                    {
                        _instance.ShowTeamDeathmatchInGameGui(matchPlayer.Player, timerText, roundText);
                    }
                    _remainingTime--;
                    return;
                }
                //here, count down or round is finished
                if (!IsStarted && _countDown <= 0)
                {
                    StartRound();
                }
                else
                {
                    EndRound();
                }
            }
            private void ManagePlayers()
            {
                var toBeKicked = new List<ulong>();
                foreach (var matchPlayer in Players)
                {
                    if (matchPlayer.Player.IdleTime >= 3 * 60)
                    {
                        toBeKicked.Add(matchPlayer.PlayerId);
                    }
                    else if (matchPlayer.Player.IsWounded() && matchPlayer.Player.woundedDuration > matchPlayer.Match.Arena.Data.WoundedTimeout)
                    {
                        matchPlayer.Player.Die(new HitInfo(matchPlayer.Player.lastAttacker, null, matchPlayer.Player.lastDamage, 0));
                    }

                    _instance.ShowTeamDeathmatchPlayers(matchPlayer.Player, this);

                    if (matchPlayer.Player.IsSleeping())
                    {
                        matchPlayer.Player.EndSleeping();
                    }
                }

                foreach (var playerId in toBeKicked)
                {
                    Kick(playerId, false);
                }
            }
            public void TryStartMatch()
            {
                if (Players.Where(x => x.Team != Team.Spectator).All(x => x.IsReady))
                {
                    if (_currentRound > 1)
                    {
                        StartCountDown();
                    }
                    else
                        StartMatch();
                }
            }
            public bool IsMatchBalanced()
            {
                var blueCount = Players.Count(x => x.Team == Team.Blue);
                var redCount = Players.Count(x => x.Team == Team.Red);
                if (redCount == 0 || blueCount == 0)
                {
                    var leader = Players.FirstOrDefault(x => x.IsMatchLeader);
                    if (leader?.Player != null)
                    {
                        _arena.ShowMessage(leader.Player, Messages.NotEnoughPlayers, _instance);
                    }
                    return false;
                }
                return true;
            }
            public void AllowPlayersToEscape()
            {
                foreach (var spawnZone in Arena.SpawnZones)
                {
                    if (spawnZone.SpawnData.SpawnType != Team.Spectator)
                    {
                        spawnZone.ClearPlayers(); //free players so they can leave spawn zones
                    }
                }
            }
            public void StartMatch()
            {
                if (IsStarted)
                    return;
                if (!IsMatchBalanced())
                    return;
                SetMatchInitialData();
                StartCountDown(); //count down will start in matchtimer method
            }
            private void StartCountDown()
            {
                if (!IsMatchBalanced())
                    return;
                _remainingTime = 0;
                _countDown = IsCountDownEnabled ? Arena.Data.MatchStartCountDown * 2 : -1;
                IsCountDownActivated = true;
            }
            public void StartRound()
            {
                if (!IsMatchBalanced())
                {
                    IsCountDownActivated = false;
                    return;
                }
                IsStarted = true;
                //set new round time
                _remainingTime = RoundTime * 60 * 2;
                AllowPlayersToEscape();
            }
            public void EndMatch()
            {
                ClearTimerGui();
                if (Players.Count > 0)
                {
                    var winnerTeam = string.Empty;
                    if (_roundsWonByBlue > _roundsWonByRed)
                    {
                        winnerTeam = Arena.Data.BlueTeamDisplayName;
                    }
                    else if (_roundsWonByRed > _roundsWonByBlue)
                    {
                        winnerTeam = Arena.Data.RedTeamDisplayName;
                    }

                    if (!string.IsNullOrWhiteSpace(winnerTeam))
                    {
                        foreach (var inGamePlayer in Players)
                        {
                            _arena.ShowMessage(inGamePlayer.Player, Messages.EndMatchMessage, _instance, winnerTeam);
                        }
                    }
                    else
                    {
                        foreach (var inGamePlayer in Players)
                        {
                            _arena.ShowMessage(inGamePlayer.Player, Messages.EndMatchDrawMessage, _instance, winnerTeam);
                        }
                    }
                }
                SetMatchInitialData();
                //Clear();
            }
            public void EndRound()
            {
                IsStarted = false;
                IsCountDownActivated = false;
                ClearTimerGui();
                SetAllPlayersNotReady();
              	 _arena.ClearEntities(Arena.Zone.Id);
                CheckForWinner();
                UpdateStatistics();
                if ((RoundCount <= 0 || _currentRound < RoundCount) && Players.Count > 0)
                {
                    _instance.timer.Once(1.5f, () =>
                    {
                        _currentRound++;
                        MakePlayersReadyToMatch();
                        if (WillAutoStart)
                        {
                            _instance.timer.Once(1.5f, () =>
                            {
                                SetAllPlayersReady();
                                StartCountDown();
                            });
                        }
                    });
                }
                else
                {
                    EndMatch();
                    MakePlayersReadyToMatch();
                }
            }

            public void UpdateStatistics()
            {
                var playerStats = Players.Select(x => new GameModeManager.PlayerStatistics(x.Player)
                {
                    Kills = x.Statistics.Kills,
                    Deaths = x.Statistics.Deaths,
                    DamagesDone = x.Statistics.DamagesDone
                }).ToList();
                _arena.UpdateScore(playerStats);
                ResetStats();
            }
            public void MakePlayersReadyToMatch()
            {
                foreach (var matchPlayer in Players)
                {
                    matchPlayer.Reset();
                    _instance.RespawnInArena(matchPlayer, matchPlayer.Match.Arena, matchPlayer.Team);
                    matchPlayer.ManageInventory(false);
                }
            }
            public void SetMatchInitialData()
            {
                IsCountDownActivated = false;
                _countDown = IsCountDownEnabled ? Arena.Data.MatchStartCountDown * 2 : -1;
                _remainingTime = 0; //will be filled with real value in StartRound
                _currentRound = 1;
                _roundsWonByBlue = 0;
                _roundsWonByRed = 0;
                IsStarted = false;
                ResetStats();
            }
            private void SetAllPlayersNotReady()
            {
                foreach (var player in Players)
                {
                    player.IsReady = false;
                    _instance.ShowTeamDeathmatchQuickMenu(player.Player, this);
                }
            }
            public void SetAllPlayersReady()
            {
                foreach (var player in Players)
                {
                    player.IsReady = true;
                    _instance.ShowTeamDeathmatchQuickMenu(player.Player, this);
                }
            }
            public void ClearTimerGui()
            {
                foreach (var matchPlayer in Players)
                {
                    CuiHelper.DestroyUi(matchPlayer.Player, GameModeManager.Ui.Panels.InGameInfo.ToString());
                }
            }
            public void CheckIfRoundShouldEnd()
            {
                if (!IsStarted)
                {
                    return;
                }
                if (Players.Count <= 0) //no players in match!
                {
                    EndMatch();
                }
                var alivePlayers = Players.Where(x => x.AllowPlaying).ToList();
                var blueCount = alivePlayers.Count(x => x.Team == Team.Blue);
                var redCount = alivePlayers.Count(x => x.Team == Team.Red);
                if (blueCount <= 0 || redCount <= 0) //round should end
                {
                    EndRound();
                }
            }
            public void CheckForWinner()
            {
                if (Players.Count <= 0)
                {
                    EndMatch();
                }
                var alivePlayers = Players.Where(x => x.AllowPlaying).ToList();
                var blueCount = alivePlayers.Count(x => x.Team == Team.Blue);
                var redCount = alivePlayers.Count(x => x.Team == Team.Red);
                var winnerTeam = string.Empty;
                if (blueCount > redCount)
                {
                    _roundsWonByBlue++;
                    winnerTeam = Arena.Data.BlueTeamDisplayName;
                }
                else if (redCount > blueCount)
                {
                    _roundsWonByRed++;
                    winnerTeam = Arena.Data.RedTeamDisplayName;
                }
                if (!string.IsNullOrEmpty(winnerTeam))
                {
                    foreach (var inGamePlayer in Players)
                    {
                        _arena.ShowMessage(inGamePlayer.Player, Messages.EndRoundMessage, _instance, winnerTeam);
                    }
                }
                else
                {
                    foreach (var inGamePlayer in Players)
                    {
                        _arena.ShowMessage(inGamePlayer.Player, Messages.EndRoundDrawMessage, _instance);
                    }
                }
            }
            public void ResetStats()
            {
                foreach (var matchPlayer in Players)
                {
                    matchPlayer.ResetStats();
                }
            }
            public void ResetLoadout()
            {
                foreach (var matchPlayer in Players)
                {
                    matchPlayer.ManageInventory(true);
                }
            }
            public void SetBlueTeamLoadout(string loadoutName)
            {
                BlueTeamSelectedLoadoutName = loadoutName;
                foreach (var deathmatchPlayer in Players)
                {
                    deathmatchPlayer.SetBlueTeamLoadout(loadoutName);
                    _arena.ShowMessage(deathmatchPlayer.Player, Messages.TeamKitSet, _instance, Arena.Data.BlueTeamDisplayName, loadoutName);
                }
            }
            public void SetRedTeamLoadout(string loadoutName)
            {
                RedTeamSelectedLoadoutName = loadoutName;
                foreach (var deathmatchPlayer in Players)
                {
                    deathmatchPlayer.SetRedTeamLoadout(loadoutName);
                    _arena.ShowMessage(deathmatchPlayer.Player, Messages.TeamKitSet, _instance, Arena.Data.RedTeamDisplayName, loadoutName);
                }
            }
            public void ToggleItem(string itemName)
            {
                if (Items.Contains(itemName))
                    Items.Remove(itemName);
                else
                    Items.Add(itemName);

                foreach (var deathmatchPlayer in Players)
                {
                    deathmatchPlayer.ToggleItem(itemName);
                }
            }
            public void AddPlayer(TeamDeathmatchPlayer player)
            {
                Players.Add(player);
                player.InitialLoadoutItem();
            }
            public void RemovePlayer(TeamDeathmatchPlayer player)
            {
                Players.Remove(player);
                player.ClearInventory();
                if (Players.Count == 0)
                {
                    Clear();
                }
            }
            public void KickAll(bool includeLeader = false, bool beSpectator = true)
            {
                foreach (var matchPlayer in Players)
                {
                    if (!includeLeader && matchPlayer.IsMatchLeader)
                        continue;

                    Kick(matchPlayer, beSpectator);
                }
            }
            public void Kick(ulong playerId, bool beSpectator)
            {
                var matchPlayer = Players.FirstOrDefault(x => x.PlayerId == playerId);
                Kick(matchPlayer, beSpectator);
            }
            public void Kick(GameModeManager.MatchPlayer matchPlayer, bool beSpectator)
            {
                if (matchPlayer != null)
                {
                    if (beSpectator)
                    {
                        _instance.SwitchTeam(matchPlayer.Player, Team.Spectator);
                    }
                    else
                    {
                        _instance.HandleLeaveArena(matchPlayer.Player);
                    }
                }
            }
            public void Clear()
            {
                SetMatchInitialData();
                BlueTeamSelectedLoadoutName = Arena.Data.BlueTeamDefaultLoadoutName;
                RedTeamSelectedLoadoutName = Arena.Data.RedTeamDefaultLoadoutName;
                IsCountDownActivated = false;
                IsCountDownEnabled = true;
                IsStarted = false;
                WillAutoStart = true;
                AllowIndividualKit = false;
                RestrictAccess = false;
                AllowedPlayers.Clear();
                BannedPlayers.Clear();
                RoundTime = 5;
                RoundCount = Arena.Data.DefaultRoundCount;
            }
        }
        public class TeamDeathmatchPlayer : GameModeManager.MatchPlayer
        {
            public TeamDeathmatchPlayer(BasePlayer player, TeamDeathmatchMatch match) : base(player, match)
            {
                Match = match;
            }
            public Team Team { get; set; }
            public new TeamDeathmatchMatch Match { get; set; }
            public bool IsSelected { get; set; }
            public bool IsReady { get; set; }
            public void ToggleReady()
            {
                if (!IsReady)
                {
                    IsReady = true;
                    foreach (var otherPlayer in Match.Players)
                    {
                        _arena.ShowMessage(otherPlayer.Player, Messages.PlayerIsReady, _instance, PlayerName);
                    }
                    Match.TryStartMatch();
                }
            }
            public void InitialLoadoutItem()
            {
                BlueTeamSelectedLoadoutName = Match.BlueTeamSelectedLoadoutName;
                RedTeamSelectedLoadoutName = Match.RedTeamSelectedLoadoutName;
                foreach (var matchItem in Match.Items)
                {
                    IndividualItems.Add(matchItem);
                }
                ManageInventory(false);
            }
            public void SetBlueTeamLoadout(string loadoutName)
            {
                BlueTeamSelectedLoadoutName = loadoutName;
                ManageInventory(false);
            }
            public void SetRedTeamLoadout(string loadoutName)
            {
                RedTeamSelectedLoadoutName = loadoutName;
                ManageInventory(false);
            }
            public void ToggleItem(string itemName)
            {
                if (IndividualItems.Contains(itemName))
                    IndividualItems.Remove(itemName);
                else
                    IndividualItems.Add(itemName);
                ManageInventory(false);
            }
            public void ManageInventory(bool forceReset)
            {
                if (Team == Team.Spectator || !AllowPlaying)
                {
                    _arena.GiveLoadout(Player, Match.Arena.Data.SpectatorLoadoutName);
                    return;
                }

                if (!forceReset && Match.IsStarted)
                    return;

                if (Team == Team.Red)
                    _arena.GiveLoadout(Player, RedTeamSelectedLoadoutName);
                else if (Team == Team.Blue)
                    _arena.GiveLoadout(Player, BlueTeamSelectedLoadoutName);

                foreach (var itemShortName in IndividualItems)
                {
                    var existItem = Player.inventory.FindItemByItemID(itemShortName);
                    if (existItem == null)
                    {
                        var newItem = ItemManager.CreateByName(itemShortName);
                        Player.inventory.GiveItem(newItem);
                    }
                }
            }
            public override bool Equals(object obj)
            {
                var matchPlayer = obj as TeamDeathmatchPlayer;
                if (matchPlayer == null)
                {
                    return false;
                }

                return matchPlayer.PlayerId == PlayerId;
            }
            public override int GetHashCode()
            {
                return PlayerId.GetHashCode();
            }
        }
        private static class Messages
        {
            public const string NoPermission = "No Permission";
            public const string WrongCommand = "Wrong Command";
            public const string ArenaCommandsList = "Arena Commands List";
            public const string ScrimCommandsList = "Scrim Commands Help";
            public const string InvalidPlayerId = "Invalid Player Id";
            public const string PlayerNotFound = "Player Not Found";
            public const string LobbyNotFound = "Lobby Not Found";
            public const string ArenaNotFound = "Arena Not Found";
            public const string ChangesSaved = "Changes Saved";
            public const string NoEditingLobby = "No Editing Lobby";
            public const string NoEditingArena = "No Editing Arena";
            public const string ArenaParameterChanged = "Arena Parameter Changed";
            public const string ArenaSpawnPointAdded = "Arena Spawn Point Added";
            public const string ArenaInvalidPosition = "Arena Invalid Position";
            public const string ArenaInvalidEntrancePosition = "Arena Invalid Entrance Position";
            public const string ArenaDeleted = "Arena Deleted";
            public const string ArenaEditingStarted = "Arena Editing Started";
            public const string ArenaEditingDone = "Arena Editing Done";
            public const string ArenaRestrictAccess = "Arena Restrict Access";
            public const string ArenaNoVipAccess = "Arena No VIP Access";
            public const string ArenaZoneNotFound = "Arena Zone Not Found";
            public const string LobbyZoneNotFound = "Lobby Zone Not Found";
            public const string InvalidKitName = "Invalid Kit Name";
            public const string KitAdded = "Kit Added";
            public const string KitRemoved = "Kit Removed";
            public const string KitNotFound = "Kit Not Found";
            public const string ArenaCreated = "Arena Created";
            public const string SpawnPointNotFound = "Spawn Point Not Found";
            public const string TeamNotFound = "Team Not Found";
            public const string NotEnoughPlayers = "Not Enough Players";
            public const string EndRoundMessage = "End Round Message";
            public const string EndMatchMessage = "End Match Message";
            public const string EndRoundDrawMessage = "End Round Draw Message";
            public const string EndMatchDrawMessage = "End Match Draw Message";
            public const string ArenaJoinMessage = "Arena Join Message";
            public const string ArenaSpectatorJoinMessage = "Arena Spectator Join Message";
            public const string ArenaLeaveMessage = "Arena Leave Message";
            public const string PlayerIsReady = "Player Is Ready";
            public const string InvalidItemShortName = "Invalid Item Short Name";
            public const string ItemAdded = "Item Added";
            public const string ItemRemoved = "Item Removed";
            public const string ItemNotFound = "Item Not Found";
            public const string ArenaSetValueHelp = "Arena Set Value Help";
            public const string ArenaSpawnPointTypesHelp = "Arena SpawnPoint Types Help";
            public const string InvalidSpawnType = "Invalid Spawn Type";
            public const string SingleSelectionNeeded = "Single Selection Needed";
            public const string MatchLeadershipChange = "Match Leadership Change";
            public const string KilledBy = "Killed By";
            public const string NoSwitchTeamOnRunningMatch = "No Switch Team On Running Match";
            public const string MatchIsFull = "Match Is Full";
            public const string TeamKitSet = "Team Kit Set";
        }
        public static class ConsoleCommands
        {
            public const string ArenaHelp = "s.help";
            public const string ArenaEdit = "s.edit";
            public const string ArenaDone = "s.done";
            public const string ArenaDelete = "s.delete";
            public const string ArenaCreate = "s.create";
            public const string ArenaSet = "s.set";
            public const string ArenaSetHelp = "s.set.help";
            public const string ArenaSetSpawn = "s.set.spawn";
            public const string ArenaSetSpawnHelp = "s.set.spawn.help";
            public const string ArenaDeleteSpawn = "s.delete.spawn";
            public const string ArenaAllow = "s.allow";
            public const string ArenaBan = "s.ban";

            public const string ArenaJoin = "s.join";
            public const string ArenaLeave = "s.leave";

            public const string MenuShow = "s.menu.show";
            public const string MenuClear = "s.menu.clear";
            public const string LoadoutShow = "s.loadout.show";
            public const string SwitchTeamShow = "s.switchteam.show";
            public const string SwitchTeam = "s.switchteam";
            public const string LeaderboardShow = "s.leaderboard.show";
            public const string ControlShow = "s.control.show";
            public const string PlayersSearch = "s.players.search";
            public const string PlayersSearchInput = "players.search.input";
            public const string PlayerSelectionToggle = "s.player.select.toggle";
            public const string NextPage = "s.page.next";
            public const string PreviousPage = "s.page.prev";
            public const string ClearInput = "s.input.clear";
            public const string ResetMatchStats = "s.match.reset.stats";
            public const string ResetLoadout = "s.match.reset.loadout";
            public const string ResetMatch = "s.match.reset";
            public const string RemoveWalls = "s.match.reset.walls";
            public const string ForceStart = "s.match.start";
            public const string KickFromMatch = "s.match.kick";
            public const string KickAll = "s.match.kick.all";
            public const string LockMatch = "s.match.lock";
            public const string MatchReadyToggle = "s.match.ready.toggle";
            public const string AllowIndividualKit = "s.match.individualkit";
            public const string ToggleAutoStartRound = "s.match.autostart.toggle";
            public const string ToggleCountdown = "s.match.countdown.toggle";
            public const string SetRoundCount = "s.match.round.count";
            public const string SetRoundTime = "s.match.round.time";
            public const string MatchBan = "s.match.ban";
            public const string MatchAllow = "s.match.allow";
            public const string MatchUnBan = "s.match.unban";
            public const string MatchDisallow = "s.match.disallow";
            public const string TransferLeadership = "s.match.leadership";
            public const string SetBlueTeamKit = "s.match.kit.set.blue";
            public const string SetRedTeamKit = "s.match.kit.set.red";
            public const string GiveItem = "s.match.item.set";
            public const string TakeKit = "s.match.kit.take";
            public const string TakeItem = "s.match.item.take";

        }
        public enum Team
        {
            //None = 0,
            Spectator = 10,
            Blue = 20,
            Red = 30
        }

        #endregion Classes

        #region Methods

        #region Commands
        private void ShowPauseMenuChatCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null)
            {
                return;
            }
            ShowPauseMenu(player);
        }
        private void ToggleReadyChatCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null)
            {
                return;
            }
            TogglePlayerReady(player);
        }

        private void ShowPauseMenu(BasePlayer player)
        {
            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null)
                ShowTeamDeathmatchPauseMenu(player, matchPlayer.Match.Arena.Data.Id);
        }

        #region ArenaDataCommands

        //s.help
        [ConsoleCommand(ConsoleCommands.ArenaHelp)]
        void ArenaHelp(ConsoleSystem.Arg conArgs)
        {
            var player = conArgs?.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, GameModeManager.PermissionAdmin))
            {
                _arena.ShowMessage(player, Messages.NoPermission, this);
                return;
            }
            _arena.ShowMessage(player, Messages.ArenaCommandsList, this, GetArenaCommands());
        }

        //s.edit <id>
        [ConsoleCommand(ConsoleCommands.ArenaEdit)]
        void EditArena(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                _arena.ShowMessage(null, Messages.WrongCommand, this, GetArenaCommands());
                return;
            }

            var player = conArgs.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, GameModeManager.PermissionAdmin))
            {
                _arena.ShowMessage(player, Messages.NoPermission, this);
                return;
            }

            //if (_lobbyToEdit == null)
            //{
            //    _arena.ShowMessage(player, Messages.NoEditingLobby, this, ConsoleCommands.LobbyEdit);
            //    return;
            //}

            var id = conArgs.GetInt(0, -1);
            if (id >= 0)
            {
                _arenaToEdit = _pluginData.Arenas.FirstOrDefault(x => x.Id == id);
                if (_arenaToEdit == null)
                {
                    _arena.ShowMessage(player, Messages.ArenaNotFound, this, GetArenaCommands());
                    return;
                }
                _arena.UnLockEntities(_arenaToEdit.ZoneId, player.userID);
                _arena.ShowMessage(player, Messages.ArenaEditingStarted, this, _arenaToEdit.Name, _arenaToEdit.Id);
            }
            else
            {
                _arena.ShowMessage(player, Messages.WrongCommand, this, GetArenaCommands());
            }
        }

        //s.done
        [ConsoleCommand(ConsoleCommands.ArenaDone)]
        void DoneArena(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                _arena.ShowMessage(null, Messages.WrongCommand, this, GetArenaCommands());
                return;
            }

            var player = conArgs.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, GameModeManager.PermissionAdmin))
            {
                _arena.ShowMessage(player, Messages.NoPermission, this);
                return;
            }

            if (_arenaToEdit != null)
            {
                _arena.LockEntities(_arenaToEdit.ZoneId);
                var name = _arenaToEdit.Name;
                var id = _arenaToEdit.Id;
                _arenaToEdit = null;
                _arena.ShowMessage(player, Messages.ArenaEditingDone, this, name, id);
            }
        }

        //s.delete <id>
        [ConsoleCommand(ConsoleCommands.ArenaDelete)]
        void DeleteArena(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                _arena.ShowMessage(null, Messages.WrongCommand, this, GetArenaCommands());
                return;
            }

            var player = conArgs.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, GameModeManager.PermissionAdmin))
            {
                _arena.ShowMessage(player, Messages.NoPermission, this);
                return;
            }

            //if (_lobbyToEdit == null)
            //{
            //    _arena.ShowMessage(player, Messages.NoEditingLobby, this, ConsoleCommands.LobbyEdit);
            //    return;
            //}

            var id = conArgs.GetInt(0, -1);
            if (id >= 0)
            {
                var arenaToRemove = _pluginData.Arenas.FirstOrDefault(x => x.Id == id);
                if (arenaToRemove != null)
                {
                    _pluginData.Arenas.Remove(arenaToRemove);
                    SaveData();
                    TeamDeathmatchArena teamDeathMatchArena;
                    _arenas.TryGetValue(arenaToRemove.Id, out teamDeathMatchArena);
                    RemoveArena(teamDeathMatchArena);
                    _arena.ShowMessage(player, Messages.ArenaDeleted, this);
                }
                else
                {
                    _arena.ShowMessage(player, Messages.ArenaNotFound, this);
                }
            }
            else
            {
                _arena.ShowMessage(player, Messages.WrongCommand, this, GetArenaCommands());
            }
        }

        //s.create <Name> <ZoneId> [<Capacity>] [<EntranceTriggerRadius>]
        [ConsoleCommand(ConsoleCommands.ArenaCreate)]
        void CreateArena(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                _arena.ShowMessage(null, Messages.WrongCommand, this, GetArenaCommands());
                return;
            }

            var player = conArgs.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, GameModeManager.PermissionAdmin))
            {
                _arena.ShowMessage(player, Messages.NoPermission, this);
                return;
            }

            if (conArgs.Args.Length < 2)
            {
                _arena.ShowMessage(player, Messages.WrongCommand, this, GetArenaCommands());
                return;
            }

            if (_arena.LobbyToEdit == null)
            {
                _arena.ShowMessage(player, Messages.NoEditingLobby, this, GameModeManager.ConsoleCommands.LobbyEdit);
                return;
            }

            GameModeManager.Lobby lobby;
            if (!_arena.Lobbies.TryGetValue(_arena.LobbyToEdit.Id, out lobby) || lobby == null)
            {
                _arena.ShowMessage(player, Messages.LobbyNotFound, this);
                return;
            }
            var name = conArgs.GetString(0);
            var zoneId = conArgs.GetString(1);
            var capacity = conArgs.GetInt(2, 4);
            var radius = conArgs.GetFloat(3, 2);
            var entrancePosition = Vector3.zero;

            if (player != null)
            {
                entrancePosition = player.ServerPosition;
            }

            var zone = _arena.GetZone(zoneId);
            if (zone == null)
            {
                _arena.ShowMessage(player, Messages.ArenaZoneNotFound, this);
                return;
            }

            var lobbyZone = _arena.GetZone(_arena.LobbyToEdit.ZoneId);
            if (lobbyZone == null)
            {
                _arena.ShowMessage(player, Messages.LobbyZoneNotFound, this);
                return;
            }

            if (Vector3.Distance(entrancePosition, lobbyZone.Location) >= lobbyZone.Radius)
            {
                entrancePosition = Vector3.zero;
            }
            var id = (_arena.LobbyToEdit.Id * 100) + 1;
            var arenaIds = _arena.GetArenaIdsInLobby(_arena.LobbyToEdit.Id);

            if (arenaIds.Any())
            {
                id = arenaIds.Max() + 1;
            }

            var newArena = new TeamDeathmatchArenaData
            {
                Id = id,
                LobbyId = lobby.Data.Id,
                Name = name,
                ZoneId = zoneId,
                Capacity = capacity,
                IsEnabled = true,
                MatchType = GameModeManager.MatchType.Scrim,
                EntranceLocation = entrancePosition,
                IsEntranceTriggerActive = entrancePosition != Vector3.zero,
                EntranceTriggerRadius = radius,
                AccessThroughLobbyOnly = false,
                RestrictAccess = false,
                BlueTeamDisplayName = "<color=#33689A>BLUE</color>",
                RedTeamDisplayName = "<color=#DD4B39>RED</color>",
                StartedMatchEntranceDisplayText = "$.Name",
                NoMatchEntranceDisplayText = "$.Name",
                SpawnLocations = new HashSet<ArenaSpawnData>(),

            };
            var existingArena = _pluginData.Arenas.FirstOrDefault(x => x.ZoneId.Equals(zoneId, StringComparison.OrdinalIgnoreCase));
            if (existingArena != null)
            {
                newArena.MatchStartCountDown = existingArena.MatchStartCountDown;
                newArena.BlueTeamDisplayName = existingArena.BlueTeamDisplayName;
                newArena.RedTeamDisplayName = existingArena.RedTeamDisplayName;
                newArena.StartedMatchEntranceDisplayText = existingArena.StartedMatchEntranceDisplayText;
                newArena.NoMatchEntranceDisplayText = existingArena.NoMatchEntranceDisplayText;
                newArena.SpectatorLoadoutName = existingArena.SpectatorLoadoutName;
                newArena.BlueTeamDefaultLoadoutName = existingArena.BlueTeamDefaultLoadoutName;
                newArena.RedTeamDefaultLoadoutName = existingArena.RedTeamDefaultLoadoutName;
                newArena.WoundedTimeout = existingArena.WoundedTimeout;
                newArena.DefaultRoundCount = existingArena.DefaultRoundCount;
                newArena.RoundsCounts = existingArena.RoundsCounts.ToArray();
                newArena.LoadoutNames = existingArena.LoadoutNames.ToList();
                newArena.ItemsNames = existingArena.ItemsNames.ConvertAll(existingArenaItemsName => new ItemName
                {
                    ItemShortName = existingArenaItemsName.ItemShortName,
                    ItemDisplayName = existingArenaItemsName.ItemDisplayName
                });
                newArena.SpawnLocations = new HashSet<ArenaSpawnData>(existingArena.SpawnLocations.Select(existingArenaSpawnLocation => new ArenaSpawnData
                {
                    SpawnType = existingArenaSpawnLocation.SpawnType,
                    SafeAreaRadius = existingArenaSpawnLocation.SafeAreaRadius,
                    IsEnabled = existingArenaSpawnLocation.IsEnabled,
                    Name = existingArenaSpawnLocation.Name,
                    SpawnPoint = new Vector3(x: existingArenaSpawnLocation.SpawnPoint.x, y: existingArenaSpawnLocation.SpawnPoint.y, z: existingArenaSpawnLocation.SpawnPoint.z),
                    Radius = existingArenaSpawnLocation.Radius,
                    SpawnChance = existingArenaSpawnLocation.SpawnChance
                }));

                newArena.LobbyId = existingArena.LobbyId;
                newArena.IsEnabled = existingArena.IsEnabled;
                newArena.MatchType = existingArena.MatchType;
                newArena.IsEntranceTriggerActive = existingArena.IsEntranceTriggerActive;
                newArena.AccessThroughLobbyOnly = existingArena.AccessThroughLobbyOnly;
                newArena.EntrancePointTextVisibilityRange = existingArena.EntrancePointTextVisibilityRange;
                newArena.RestrictAccess = existingArena.RestrictAccess;
            }
            _pluginData.Arenas.Add(newArena);
            _arenaToEdit = newArena;
            SaveData();
            var arena = new TeamDeathmatchArena(lobby, _arenaToEdit);
            if (arena.Match == null)
            {
                player.ChatMessage("Arena data is created but Match type is missing and arena is not activated!");//TODO: Lang
                return;
            }
            arena.Zone = zone;
            arena.Behaviour = GameModeManager.ArenaBehaviour.CreateEntrance($"Arena_EP_{newArena.Id}", _arenaToEdit.EntranceLocation,
                _arenaToEdit.EntranceTriggerRadius,
                _arenaToEdit.IsEntranceTriggerActive);
            arena.Behaviour.Initialize(arena);
            _arenas.Add(_arenaToEdit.Id, arena);
            _arena.ShowMessage(player, Messages.ArenaCreated, this);
        }

        public TeamDeathmatchArenaData Test(TeamDeathmatchArenaData existingArena)
        {
            var newArena = new TeamDeathmatchArenaData();

            return newArena;
        }

        //s.set <key> <value> [<key> <value>] [<key> <value>] ...
        [ConsoleCommand(ConsoleCommands.ArenaSet)]
        void SetArenaValue(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                _arena.ShowMessage(null, Messages.WrongCommand, this, GetArenaCommands());
                return;
            }

            var player = conArgs.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, GameModeManager.PermissionAdmin))
            {
                _arena.ShowMessage(player, Messages.NoPermission, this);
                return;
            }

            if (conArgs.Args.Length <= 1)
            {
                _arena.ShowMessage(player, Messages.WrongCommand, this, GetArenaCommands());
                return;
            }

            if (_arenaToEdit == null)
            {
                _arena.ShowMessage(player, Messages.NoEditingArena, this, ConsoleCommands.ArenaEdit);
                return;
            }

            GameModeManager.Zone zone = null;
            var updateEntrance = false;
            var length = conArgs.Args.Length / 2;
            var index = 0;
            for (var i = 0; i < length; i++)
            {
                var key = conArgs.GetString(index).ToLower();
                index++;
                switch (key)
                {
                    case "name":
                        {
                            _arenaToEdit.Name = conArgs.GetString(index);
                            _arena.ShowMessage(player, Messages.ArenaParameterChanged, this, nameof(TeamDeathmatchArenaData.Name));
                            break;
                        }
                    case "zone":
                    case "zoneid":
                        {
                            var zoneId = conArgs.GetString(index);
                            zone = _arena.GetZone(zoneId);
                            if (zone == null)
                            {
                                _arena.ShowMessage(player, Messages.ArenaZoneNotFound, this);
                            }
                            else
                            {
                                _arenaToEdit.ZoneId = zoneId;
                                _arena.ShowMessage(player, Messages.ArenaParameterChanged, this, nameof(TeamDeathmatchArenaData.ZoneId));
                            }
                            break;
                        }
                    case "lobby":
                    case "lobbyid":
                        {
                            var lobbyId = conArgs.GetInt(index);

                            if (_arena.Lobbies.ContainsKey(lobbyId))
                            {
                                _arena.ShowMessage(player, Messages.LobbyNotFound, this);
                            }
                            else
                            {
                                _arenaToEdit.LobbyId = lobbyId;
                                _arena.ShowMessage(player, Messages.ArenaParameterChanged, this, nameof(TeamDeathmatchArenaData.LobbyId));
                            }
                            break;
                        }
                    case "enabled":
                    case "isenabled":
                        {
                            _arenaToEdit.IsEnabled = conArgs.GetBool(index);
                            updateEntrance = true;
                            _arena.ShowMessage(player, Messages.ArenaParameterChanged, this, nameof(TeamDeathmatchArenaData.IsEnabled));
                            break;
                        }
                    case "capacity":
                        {
                            _arenaToEdit.Capacity = conArgs.GetInt(index, 4);
                            _arena.ShowMessage(player, Messages.ArenaParameterChanged, this, nameof(TeamDeathmatchArenaData.Capacity));
                            break;
                        }
                    case "restrict":
                    case "restrictaccess":
                        {
                            _arenaToEdit.RestrictAccess = conArgs.GetBool(index);
                            _arena.ShowMessage(player, Messages.ArenaParameterChanged, this, nameof(TeamDeathmatchArenaData.RestrictAccess));
                            break;
                        }
                    case "entrance.active":
                        {
                            _arenaToEdit.IsEntranceTriggerActive = conArgs.GetBool(index);
                            updateEntrance = true;
                            _arena.ShowMessage(player, Messages.ArenaParameterChanged, this, nameof(TeamDeathmatchArenaData.IsEntranceTriggerActive));
                            break;
                        }
                    case "wt":
                    case "w.t":
                    case "wounded.timeout":
                        {
                            _arenaToEdit.WoundedTimeout = conArgs.GetFloat(index);
                            _arena.ShowMessage(player, Messages.ArenaParameterChanged, this, nameof(TeamDeathmatchArenaData.WoundedTimeout));
                            break;
                        }
                    case "entrance.vr":
                    case "entrance.visibilityrange":
                    case "entrance.textvisibilityrange":
                        {
                            _arenaToEdit.EntrancePointTextVisibilityRange = conArgs.GetFloat(index);
                            updateEntrance = true;
                            _arena.ShowMessage(player, Messages.ArenaParameterChanged, this, nameof(TeamDeathmatchArenaData.EntrancePointTextVisibilityRange));
                            break;
                        }
                    case "entrance.pos":
                        {
                            var pos = conArgs.GetVector3(index, Vector3.zero);
                            if (pos.Equals(Vector3.zero))
                            {
                                if (conArgs.GetString(index).ToLower().Equals("here") && player != null)
                                {
                                    pos = player.ServerPosition;
                                }
                                else
                                {
                                    _arena.ShowMessage(player, Messages.WrongCommand, this, GetArenaCommands());
                                    break;
                                }
                            }
                            GameModeManager.Lobby lobby;
                            if (!_arena.Lobbies.TryGetValue(_arenaToEdit.LobbyId, out lobby) || lobby == null)
                            {
                                _arena.ShowMessage(player, Messages.LobbyNotFound, this);
                                break;
                            }
                            if (Vector3.Distance(player.ServerPosition, lobby.Zone.Location) >= lobby.Zone.Radius)
                            {
                                _arena.ShowMessage(player, Messages.ArenaInvalidPosition, this, pos);
                                break;
                            }
                            _arenaToEdit.EntranceLocation = pos;
                            updateEntrance = true;
                            _arena.ShowMessage(player, Messages.ArenaParameterChanged, this, nameof(TeamDeathmatchArenaData.EntranceLocation));
                            break;
                        }
                    case "entrance.r":
                    case "entrance.radius":
                        {
                            _arenaToEdit.EntranceTriggerRadius = conArgs.GetFloat(index);
                            updateEntrance = true;
                            _arena.ShowMessage(player, Messages.ArenaParameterChanged, this, nameof(TeamDeathmatchArenaData.EntranceTriggerRadius));
                            break;
                        }
                    case "access.lobbyonly":
                        {
                            _arenaToEdit.AccessThroughLobbyOnly = conArgs.GetBool(index);
                            _arena.ShowMessage(player, Messages.ArenaParameterChanged, this, nameof(TeamDeathmatchArenaData.AccessThroughLobbyOnly));
                            break;
                        }
                    case "text.started":
                        {
                            _arenaToEdit.StartedMatchEntranceDisplayText = conArgs.GetString(index);
                            _arena.ShowMessage(player, Messages.ArenaParameterChanged, this, nameof(TeamDeathmatchArenaData.StartedMatchEntranceDisplayText));
                            break;
                        }
                    case "text.notstarted":
                        {
                            _arenaToEdit.NoMatchEntranceDisplayText = conArgs.GetString(index);
                            _arena.ShowMessage(player, Messages.ArenaParameterChanged, this, nameof(TeamDeathmatchArenaData.NoMatchEntranceDisplayText));
                            break;
                        }
                    case "text":
                        {
                            _arenaToEdit.NoMatchEntranceDisplayText = conArgs.GetString(index);
                            _arenaToEdit.StartedMatchEntranceDisplayText = conArgs.GetString(index);
                            _arena.ShowMessage(player, Messages.ArenaParameterChanged, this, nameof(TeamDeathmatchArenaData.StartedMatchEntranceDisplayText));
                            _arena.ShowMessage(player, Messages.ArenaParameterChanged, this, nameof(TeamDeathmatchArenaData.NoMatchEntranceDisplayText));
                            break;
                        }
                    case "bluename":
                        {
                            _arenaToEdit.BlueTeamDisplayName = conArgs.GetString(index);
                            _arena.ShowMessage(player, Messages.ArenaParameterChanged, this, nameof(TeamDeathmatchArenaData.BlueTeamDisplayName));
                            break;
                        }
                    case "redname":
                        {
                            _arenaToEdit.RedTeamDisplayName = conArgs.GetString(index);
                            _arena.ShowMessage(player, Messages.ArenaParameterChanged, this, nameof(TeamDeathmatchArenaData.RedTeamDisplayName));
                            break;
                        }
                    case "countdown":
                        {
                            _arenaToEdit.MatchStartCountDown = conArgs.GetInt(index, 3);
                            _arena.ShowMessage(player, Messages.ArenaParameterChanged, this, nameof(TeamDeathmatchArenaData.MatchStartCountDown));
                            break;
                        }
                    case "kit.s":
                    case "kit.spect":
                    case "kit.spectator":
                    case "loadout.s":
                    case "loadout.spect":
                    case "loadout.spectator":
                        {
                            _arenaToEdit.SpectatorLoadoutName = conArgs.GetString(index);
                            _arena.ShowMessage(player, Messages.ArenaParameterChanged, this, nameof(TeamDeathmatchArenaData.SpectatorLoadoutName));
                            break;
                        }
                    case "kit.d":
                    case "kit.default":
                    case "loadout.d":
                    case "loadout.default":
                        {
                            _arenaToEdit.RedTeamDefaultLoadoutName = conArgs.GetString(index);
                            _arenaToEdit.BlueTeamDefaultLoadoutName = conArgs.GetString(index);
                            _arena.ShowMessage(player, Messages.ArenaParameterChanged, this, nameof(TeamDeathmatchArenaData.RedTeamDefaultLoadoutName));
                            _arena.ShowMessage(player, Messages.ArenaParameterChanged, this, nameof(TeamDeathmatchArenaData.BlueTeamDefaultLoadoutName));
                            break;
                        }
                    case "kit.r":
                    case "kit.red":
                    case "loadout.r":
                    case "loadout.red":
                        {
                            _arenaToEdit.RedTeamDefaultLoadoutName = conArgs.GetString(index);
                            _arena.ShowMessage(player, Messages.ArenaParameterChanged, this, nameof(TeamDeathmatchArenaData.RedTeamDefaultLoadoutName));
                            break;
                        }
                    case "kit.b":
                    case "kit.blue":
                    case "loadout.b":
                    case "loadout.blue":
                        {
                            _arenaToEdit.BlueTeamDefaultLoadoutName = conArgs.GetString(index);
                            _arena.ShowMessage(player, Messages.ArenaParameterChanged, this, nameof(TeamDeathmatchArenaData.BlueTeamDefaultLoadoutName));
                            break;
                        }
                    case "kit.add":
                    case "loadout.add":
                        {
                            var loadoutName = conArgs.GetString(index);
                            {
                                _arenaToEdit.LoadoutNames.Add(loadoutName);
                                _arena.ShowMessage(player, Messages.KitAdded, this);
                            }
                            break;
                        }
                    case "kit.remove":
                    case "loadout.remove":
                        {
                            var kitName = conArgs.GetString(index);
                            if (_arenaToEdit.LoadoutNames.Remove(kitName))
                            {
                                _arena.ShowMessage(player, Messages.KitRemoved, this);
                            }
                            else
                            {
                                _arena.ShowMessage(player, Messages.KitNotFound, this);
                            }
                            break;
                        }
                    case "item.add":
                        {
                            var itemShortName = conArgs.GetString(index);
                            var itemDefinition = ItemManager.FindItemDefinition(itemShortName);
                            if (itemDefinition == null)
                            {
                                _arena.ShowMessage(player, Messages.InvalidItemShortName, this, itemShortName);
                            }
                            else
                            {
                                _arenaToEdit.ItemsNames.Add(new ItemName
                                {
                                    ItemShortName = itemShortName,
                                    ItemDisplayName = itemDefinition.displayName.english
                                });
                                _arena.ShowMessage(player, Messages.ItemAdded, this);
                            }
                            break;
                        }
                    case "item.remove":
                        {
                            var itemName = conArgs.GetString(index);
                            var item = _arenaToEdit.ItemsNames.FirstOrDefault(x =>
                                x.ItemShortName.Equals(itemName, StringComparison.OrdinalIgnoreCase));
                            if (item != null && _arenaToEdit.ItemsNames.Remove(item))
                            {
                                _arena.ShowMessage(player, Messages.ItemRemoved, this);
                            }
                            else
                            {
                                _arena.ShowMessage(player, Messages.ItemNotFound, this);
                            }
                            break;
                        }
                    default:
                        break;
                }

                index++;
            }
            SaveData();

            TeamDeathmatchArena targetArena;
            if (_arenas.TryGetValue(_arenaToEdit.Id, out targetArena) && targetArena != null)
            {
                targetArena.Data = _arenaToEdit;
                if (zone != null)
                    targetArena.Zone = zone;
                if (updateEntrance)
                {
                    targetArena.Behaviour.DoDestroy();
                    targetArena.Behaviour = GameModeManager.ArenaBehaviour.CreateEntrance($"Arena_EP_{_arenaToEdit.Id}", _arenaToEdit.EntranceLocation,
                        _arenaToEdit.EntranceTriggerRadius,
                        _arenaToEdit.IsEntranceTriggerActive);
                    targetArena.Behaviour.Initialize(targetArena);
                }
            }
            _arena.ShowMessage(player, Messages.ChangesSaved, this);
        }

        //s.set.help
        [ConsoleCommand(ConsoleCommands.ArenaSetHelp)]
        void SetArenaValueHelp(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                _arena.ShowMessage(null, Messages.WrongCommand, this, GetArenaCommands());
                return;
            }

            var player = conArgs.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, GameModeManager.PermissionAdmin))
            {
                _arena.ShowMessage(player, Messages.NoPermission, this);
                return;
            }
            _arena.ShowMessage(player, Messages.ArenaSetValueHelp, this, GetArenaEditingKeys());
        }

        //s.set.spawn <position> <type> [<radius>] [<SafeAreaRadius>] [<chance>]
        [ConsoleCommand(ConsoleCommands.ArenaSetSpawn)]
        void SetArenaSpawn(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                _arena.ShowMessage(null, Messages.WrongCommand, this, GetArenaCommands());
                return;
            }

            var player = conArgs.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, GameModeManager.PermissionAdmin))
            {
                _arena.ShowMessage(player, Messages.NoPermission, this);
                return;
            }

            if (conArgs.Args.Length < 2)
            {
                _arena.ShowMessage(player, Messages.WrongCommand, this, GetArenaCommands());
                return;
            }

            if (_arenaToEdit == null)
            {
                _arena.ShowMessage(player, Messages.NoEditingArena, this, ConsoleCommands.ArenaEdit);
                return;
            }
            var number = 1;

            var lastItem = _arenaToEdit.SpawnLocations.OrderBy(x => x.Name).LastOrDefault();
            if (lastItem != null)
            {
                var numberString = lastItem.Name.Split('_').FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(numberString))
                {
                    if (int.TryParse(numberString, out number))
                    {
                        number++;
                    }
                    else
                    {
                        number = 1;
                    }
                }
            }
            var spawnPoint = new ArenaSpawnData
            {
                IsEnabled = true,
                Radius = 0,
                SpawnChance = 100,
                SafeAreaRadius = 0
            };
            var pos = conArgs.GetVector3(0, Vector3.zero);
            if (pos.Equals(Vector3.zero))
            {
                if (conArgs.GetString(0).ToLower().Equals("here") && player != null)
                {
                    var zone = _arena.GetZone(_arenaToEdit.ZoneId);
                    if (zone == null)
                    {
                        _arena.ShowMessage(player, Messages.ArenaZoneNotFound, this);
                        return;
                    }
                    if (Vector3.Distance(player.ServerPosition, zone.Location) >= zone.Radius)
                    {
                        _arena.ShowMessage(player, Messages.ArenaInvalidPosition, this, pos);
                        return;
                    }
                    spawnPoint.SpawnPoint = player.ServerPosition;
                }
                else
                {
                    _arena.ShowMessage(player, Messages.WrongCommand, this, GetArenaCommands());
                }
            }
            else
            {
                var zone = _arena.GetZone(_arenaToEdit.ZoneId);
                if (zone == null)
                {
                    _arena.ShowMessage(player, Messages.ArenaZoneNotFound, this);
                    return;
                }
                if (Vector3.Distance(pos, zone.Location) >= zone.Radius)
                {
                    _arena.ShowMessage(player, Messages.ArenaInvalidPosition, this, pos);
                    return;
                }
                spawnPoint.SpawnPoint = pos;
            }

            var type = conArgs.GetString(1).ToLower();
            switch (type)
            {
                case "b":
                case "blue":
                    {
                        spawnPoint.SpawnType = Team.Blue;
                        spawnPoint.Name = $"{number:000}_B";
                        break;
                    }
                case "r":
                case "red":
                    {
                        spawnPoint.SpawnType = Team.Red;
                        spawnPoint.Name = $"{number:000}_R";
                        break;
                    }
                case "s":
                case "spect":
                case "spectator":
                    {
                        spawnPoint.SpawnType = Team.Spectator;
                        spawnPoint.Name = $"{number:000}_S";
                        break;
                    }
                default:
                    {
                        _arena.ShowMessage(player, Messages.InvalidSpawnType, this, GetArenaSpawnPointTypes());
                        return;
                    }
            }

            spawnPoint.Radius = conArgs.GetFloat(2, 0);
            spawnPoint.SafeAreaRadius = conArgs.GetInt(3, 0);
            spawnPoint.SpawnChance = conArgs.GetInt(4, 100);

            _arenaToEdit.SpawnLocations.Add(spawnPoint);
            _arena.ShowMessage(player, Messages.ArenaSpawnPointAdded, this, spawnPoint.SpawnPoint);
            SaveData();
            TeamDeathmatchArena arena;
            if (_arenas.TryGetValue(_arenaToEdit.Id, out arena) && arena != null)
            {
                arena.Data = _arenaToEdit;
                var behaviour = SpawnZoneBehaviour.CreateSafeZone($"Arena_{spawnPoint.Name}_{_arenaToEdit.Id}", spawnPoint.SpawnPoint);
                behaviour.Initialize(spawnPoint, arena.Match);
                arena.SpawnZones.Add(behaviour);
            }

            _arena.ShowMessage(player, Messages.ChangesSaved, this);
        }

        //s.set.spawn.help
        [ConsoleCommand(ConsoleCommands.ArenaSetSpawnHelp)]
        void SetArenaSpawnHelp(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                _arena.ShowMessage(null, Messages.WrongCommand, this, GetArenaCommands());
                return;
            }

            var player = conArgs.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, GameModeManager.PermissionAdmin))
            {
                _arena.ShowMessage(player, Messages.NoPermission, this);
                return;
            }
            _arena.ShowMessage(player, Messages.ArenaSpawnPointTypesHelp, this, GetArenaSpawnPointTypes());
        }

        //s.delete.spawn <name>
        [ConsoleCommand(ConsoleCommands.ArenaDeleteSpawn)]
        void DeleteArenaSpawnPoint(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                _arena.ShowMessage(null, Messages.WrongCommand, this, GetArenaCommands());
                return;
            }

            var player = conArgs.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, GameModeManager.PermissionAdmin))
            {
                _arena.ShowMessage(player, Messages.NoPermission, this);
                return;
            }

            if (conArgs.Args.Length <= 0)
            {
                _arena.ShowMessage(player, Messages.WrongCommand, this, GetArenaCommands());
                return;
            }

            if (_arenaToEdit == null)
            {
                _arena.ShowMessage(player, Messages.NoEditingArena, this, ConsoleCommands.ArenaEdit);
                return;
            }

            TeamDeathmatchArena arena;
            if (!_arenas.TryGetValue(_arenaToEdit.Id, out arena) || arena == null)
            {
                _arena.ShowMessage(player, Messages.ArenaNotFound, this);
                return;
            }

            var spawnPointName = conArgs.GetString(0).ToLower();

            var spawnPointToDelete = arena.SpawnZones.FirstOrDefault(x => x.SpawnData.Name.ToLower().Equals(spawnPointName));
            if (spawnPointToDelete == null)
            {
                _arena.ShowMessage(player, Messages.SpawnPointNotFound, this);
                return;
            }

            var deleted = _arenaToEdit.SpawnLocations.RemoveWhere(x => x.Name.ToLower().Equals(spawnPointName));
            if (deleted == 0)
            {
                _arena.ShowMessage(player, Messages.SpawnPointNotFound, this);
                return;
            }

            spawnPointToDelete.DoDestroy();
            arena.Data = _arenaToEdit;
            SaveData();
            _arena.ShowMessage(player, Messages.ChangesSaved, this);
        }

        //s.ban <add|remove> <arenaId> <playerId>
        [ConsoleCommand(ConsoleCommands.ArenaBan)]
        void ArenaBan(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                _arena.ShowMessage(null, Messages.WrongCommand, this, GetArenaCommands());
                return;
            }

            var player = conArgs.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, GameModeManager.PermissionAdmin))
            {
                _arena.ShowMessage(player, Messages.NoPermission, this);
                return;
            }

            if (conArgs.Args.Length <= 2)
            {
                _arena.ShowMessage(player, Messages.WrongCommand, this, GetArenaCommands());
                return;
            }

            var state = conArgs.GetString(0).ToLower();
            var arenaId = conArgs.GetInt(1);
            var playerId = conArgs.GetULong(2);
            TeamDeathmatchArena arena;
            if (!_arenas.TryGetValue(arenaId, out arena) || arena == null)
            {
                _arena.ShowMessage(player, Messages.ArenaNotFound, this);
                return;
            }

            //var lobbyToEdit = _arena.Lobbies.Values.FirstOrDefault(x => x.Data.Id == arena.Lobby.Data.Id);
            //if (lobbyToEdit == null)
            //{
            //    _arena.ShowMessage(player, Messages.LobbyNotFound, this);
            //    return;
            //}
            var arenaToEdit = _pluginData.Arenas.FirstOrDefault(x => x.Id == arenaId);
            if (arenaToEdit == null)
            {
                _arena.ShowMessage(player, Messages.ArenaNotFound, this);
                return;
            }

            if (!playerId.IsSteamId())
            {
                _arena.ShowMessage(player, Messages.InvalidPlayerId, this);
                return;
            }

            var targetPlayer = BasePlayer.FindAwakeOrSleeping(playerId.ToString());
            if (targetPlayer == null)
            {
                _arena.ShowMessage(player, Messages.PlayerNotFound, this);
                return;
            }

            if (state.Equals("add"))
            {
                arenaToEdit.BannedPlayers.Add(playerId);
            }
            else if (state.Equals("remove"))
            {
                arenaToEdit.BannedPlayers.Remove(playerId);
            }
            SaveData();
            TeamDeathmatchArena targetArena;
            if (_arenas.TryGetValue(arenaToEdit.Id, out targetArena) && targetArena != null)
            {
                targetArena.Data = arenaToEdit;
            }
            _arena.ShowMessage(player, Messages.ChangesSaved, this);
        }

        //s.allow <add|remove> <arenaId> <playerId>
        [ConsoleCommand(ConsoleCommands.ArenaAllow)]
        void ArenaAccess(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                _arena.ShowMessage(null, Messages.WrongCommand, this, GetArenaCommands());
                return;
            }

            var player = conArgs.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, GameModeManager.PermissionAdmin))
            {
                _arena.ShowMessage(player, Messages.NoPermission, this);
                return;
            }

            if (conArgs.Args.Length <= 2)
            {
                _arena.ShowMessage(player, Messages.WrongCommand, this, GetArenaCommands());
                return;
            }

            var state = conArgs.GetString(0).ToLower();
            var arenaId = conArgs.GetInt(1);
            var playerId = conArgs.GetULong(2);
            TeamDeathmatchArena arena;
            if (!_arenas.TryGetValue(arenaId, out arena) || arena == null)
            {
                _arena.ShowMessage(player, Messages.ArenaNotFound, this);
                return;
            }

            //var lobbyToEdit = _pluginData.Lobbies.FirstOrDefault(x => x.Id == arena.Lobby.Data.Id);
            //if (lobbyToEdit == null)
            //{
            //    _arena.ShowMessage(player, Messages.LobbyNotFound, this);
            //    return;
            //}
            var arenaToEdit = _pluginData.Arenas.FirstOrDefault(x => x.Id == arenaId);
            if (arenaToEdit == null)
            {
                _arena.ShowMessage(player, Messages.ArenaNotFound, this);
                return;
            }

            if (!playerId.IsSteamId())
            {
                _arena.ShowMessage(player, Messages.InvalidPlayerId, this);
                return;
            }

            var targetPlayer = BasePlayer.FindAwakeOrSleeping(playerId.ToString());
            if (targetPlayer == null)
            {
                _arena.ShowMessage(player, Messages.PlayerNotFound, this);
                return;
            }

            if (state.Equals("add"))
            {
                arenaToEdit.AllowedPlayers.Add(playerId);
            }
            else if (state.Equals("remove"))
            {
                arenaToEdit.AllowedPlayers.Remove(playerId);
            }
            SaveData();
            TeamDeathmatchArena targetArena;
            if (_arenas.TryGetValue(arenaToEdit.Id, out targetArena) && targetArena != null)
            {
                targetArena.Data = arenaToEdit;
            }
            _arena.ShowMessage(player, Messages.ChangesSaved, this);
        }

        #endregion ArenaDataCommands

        #region GUICommands

        //s.join <arenaId> <team>
        [ConsoleCommand(ConsoleCommands.ArenaJoin)]
        void ArenaJoin(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                _arena.ShowMessage(null, Messages.WrongCommand, this, GetArenaCommands());
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                _arena.ShowMessage(player, Messages.PlayerNotFound, this);
                return;
            }

            var arenaId = conArgs.GetInt(0);
            var teamValue = conArgs.GetString(1);
            Team team;
            if (!Enum.TryParse(teamValue, true, out team))
            {
                _arena.ShowMessage(player, Messages.TeamNotFound, this);
                return;
            }

            TeamDeathmatchArena arenaToJoin;
            if (!_arenas.TryGetValue(arenaId, out arenaToJoin) || arenaToJoin == null)
            {
                _arena.ShowMessage(player, Messages.ArenaNotFound, this);
                return;
            }
            GameModeManager.Ui.ClearAllMenus(player);
            TeleportToArena(player, arenaToJoin, team);
        }

        //s.leave
        [ConsoleCommand(ConsoleCommands.ArenaLeave)]
        void ArenaLeave(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                _arena.ShowMessage(null, Messages.WrongCommand, this, GetArenaCommands());
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                _arena.ShowMessage(player, Messages.PlayerNotFound, this);
                return;
            }

            HandleLeaveArena(player);
        }

        //match.reset.stats
        [ConsoleCommand(ConsoleCommands.ResetMatchStats)]
        void ResetMatchStats(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null && matchPlayer.IsMatchLeader)
                matchPlayer.Match.ResetStats();
        }

        //match.reset.loadout
        [ConsoleCommand(ConsoleCommands.ResetLoadout)]
        void ResetLoadout(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null && matchPlayer.IsMatchLeader)
                matchPlayer.Match.ResetLoadout();
        }

        //match.reset
        [ConsoleCommand(ConsoleCommands.ResetMatch)]
        void ResetMatch(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null && matchPlayer.IsMatchLeader)
            {
                matchPlayer.Match.SetMatchInitialData();
                matchPlayer.Match.MakePlayersReadyToMatch();
				_arena.ClearEntities(matchPlayer.Match.Arena.Data.ZoneId);
            }
        }

        //match.reset.walls
        [ConsoleCommand(ConsoleCommands.RemoveWalls)]
        void RemoveWalls(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }
            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null && matchPlayer.IsMatchLeader)
                _arena.ClearEntities(matchPlayer.Match.Arena.Data.ZoneId);
        }

        //match.start
        [ConsoleCommand(ConsoleCommands.ForceStart)]
        void ForceStart(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null && matchPlayer.IsMatchLeader)
            {
                matchPlayer.Match.SetAllPlayersReady();
                matchPlayer.Match.TryStartMatch();

                ShowTeamDeathmatchQuickMenu(player, matchPlayer.Match);
            }
        }

        //match.kick
        [ConsoleCommand(ConsoleCommands.KickFromMatch)]
        void KickFromMatch(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null && matchPlayer.IsMatchLeader)
            {
                var targetPlayers = new List<ulong>();
                List<GameModeManager.SelectablePlayer> selectablePlayers;
                if (_arena.PlayerSearchResults.TryGetValue(player.userID, out selectablePlayers) && selectablePlayers != null)
                {
                    var collection = selectablePlayers.Where(x => x.IsSelected).Select(x => x.PlayerId).ToList();
                    if (collection.Count > 0)
                    {
                        targetPlayers.AddRange(collection);
                        _arena.PlayerSearchResults.Remove(player.userID);
                    }
                }
                targetPlayers.AddRange(matchPlayer.Match.Players.Where(x => x.IsSelected).Select(x => x.PlayerId));
                foreach (var targetPlayer in targetPlayers)
                {
                    if (targetPlayer == player.userID)
                    {
                        continue;
                    }
                    matchPlayer.Match.Kick(targetPlayer, true);
                }
                ShowTeamDeathmatchControlMenu(player, matchPlayer.Match, GameModeManager.Ui.ControlMenuTab.Players);
            }
        }

        //match.kick.all
        [ConsoleCommand(ConsoleCommands.KickAll)]
        void KickAll(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }
            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null && matchPlayer.IsMatchLeader)
                matchPlayer.Match.KickAll(false, true);
        }

        //match.lock
        [ConsoleCommand(ConsoleCommands.LockMatch)]
        void LockMatch(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }
            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null && matchPlayer.IsMatchLeader)
            {
                matchPlayer.Match.RestrictAccess = !matchPlayer.Match.RestrictAccess;
                if (matchPlayer.Match.RestrictAccess)
                {
                    matchPlayer.Match.KickAll(false, true);
                }
                ShowTeamDeathmatchControlMenu(player, matchPlayer.Match, GameModeManager.Ui.ControlMenuTab.General);
            }
        }

        //match.individualkit <isGeneralTab>
        [ConsoleCommand(ConsoleCommands.AllowIndividualKit)]
        void AllowIndividualKit(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            var isGeneralTab = conArgs.GetBool(0, true);
            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null && matchPlayer.IsMatchLeader)
            {
                matchPlayer.Match.AllowIndividualKit = !matchPlayer.Match.AllowIndividualKit;
                ShowTeamDeathmatchControlMenu(player, matchPlayer.Match, isGeneralTab ? GameModeManager.Ui.ControlMenuTab.General : GameModeManager.Ui.ControlMenuTab.Kits);
            }
        }

        //s.match.autostart.toggle
        [ConsoleCommand(ConsoleCommands.ToggleAutoStartRound)]
        void ToggleAutoStartRound(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null && matchPlayer.IsMatchLeader)
            {
                matchPlayer.Match.WillAutoStart = !matchPlayer.Match.WillAutoStart;
                ShowTeamDeathmatchControlMenu(player, matchPlayer.Match, GameModeManager.Ui.ControlMenuTab.General);
            }
        }

        //s.match.countdown.toggle
        [ConsoleCommand(ConsoleCommands.ToggleCountdown)]
        void ToggleCountdown(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null && matchPlayer.IsMatchLeader)
            {
                matchPlayer.Match.IsCountDownEnabled = !matchPlayer.Match.IsCountDownEnabled;
                ShowTeamDeathmatchControlMenu(player, matchPlayer.Match, GameModeManager.Ui.ControlMenuTab.General);
            }
        }

        //match.round.count <count>
        [ConsoleCommand(ConsoleCommands.SetRoundCount)]
        void SetRoundCount(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }
            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            var count = conArgs.GetInt(0, 3);
            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null && matchPlayer.IsMatchLeader)
            {
                matchPlayer.Match.RoundCount = count;
                ShowTeamDeathmatchControlMenu(player, matchPlayer.Match, GameModeManager.Ui.ControlMenuTab.General);
            }
        }

        //match.round.time <time>
        [ConsoleCommand(ConsoleCommands.SetRoundTime)]
        void SetRoundTime(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            var time = conArgs.GetInt(0, 5);
            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null && matchPlayer.IsMatchLeader)
            {
                matchPlayer.Match.RoundTime = time;
                ShowTeamDeathmatchControlMenu(player, matchPlayer.Match, GameModeManager.Ui.ControlMenuTab.General);
            }
        }

        //match.ban
        [ConsoleCommand(ConsoleCommands.MatchBan)]
        void MatchBan(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null && matchPlayer.IsMatchLeader)
            {
                var targetPlayers = new List<GameModeManager.SelectablePlayer>();
                List<GameModeManager.SelectablePlayer> selectablePlayers;
                if (_arena.PlayerSearchResults.TryGetValue(player.userID, out selectablePlayers) && selectablePlayers != null)
                {
                    var collection = selectablePlayers.Where(x => x.IsSelected)
                        .Select(x => new GameModeManager.SelectablePlayer(x.PlayerId, x.PlayerName)).ToList();
                    if (collection.Count > 0)
                    {
                        targetPlayers.AddRange(collection);
                        _arena.PlayerSearchResults.Remove(player.userID);
                    }
                }
                targetPlayers.AddRange(matchPlayer.Match.Players.Where(x => x.IsSelected).Select(x =>
                    new GameModeManager.SelectablePlayer(x.PlayerId, x.PlayerName)));
                foreach (var targetPlayer in targetPlayers)
                {
                    matchPlayer.Match.AllowedPlayers.Remove(targetPlayer);
                    matchPlayer.Match.BannedPlayers.Add(targetPlayer);
                    matchPlayer.Match.Kick(targetPlayer.PlayerId, true);
                }
                ShowTeamDeathmatchControlMenu(player, matchPlayer.Match, GameModeManager.Ui.ControlMenuTab.Players);
            }
        }

        //match.allow
        [ConsoleCommand(ConsoleCommands.MatchAllow)]
        void MatchAllow(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null && matchPlayer.IsMatchLeader)
            {
                var targetPlayers = new List<GameModeManager.SelectablePlayer>();
                List<GameModeManager.SelectablePlayer> selectablePlayers;
                if (_arena.PlayerSearchResults.TryGetValue(player.userID, out selectablePlayers) && selectablePlayers != null)
                {
                    var collection = selectablePlayers.Where(x => x.IsSelected).Select(x =>
                        new GameModeManager.SelectablePlayer(x.PlayerId, x.PlayerName)).ToList();
                    if (collection.Count > 0)
                    {
                        targetPlayers.AddRange(collection);
                        _arena.PlayerSearchResults.Remove(player.userID);
                    }
                }
                targetPlayers.AddRange(matchPlayer.Match.Players.Where(x => x.IsSelected).Select(x =>
                    new GameModeManager.SelectablePlayer(x.PlayerId, x.PlayerName)));
                foreach (var targetPlayer in targetPlayers)
                {
                    matchPlayer.Match.BannedPlayers.Remove(targetPlayer);
                    matchPlayer.Match.AllowedPlayers.Add(targetPlayer);
                }
                ShowTeamDeathmatchControlMenu(player, matchPlayer.Match, GameModeManager.Ui.ControlMenuTab.Players);
            }
        }

        //match.unban
        [ConsoleCommand(ConsoleCommands.MatchUnBan)]
        void MatchUnBan(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null && matchPlayer.IsMatchLeader)
            {
                matchPlayer.Match.BannedPlayers.RemoveWhere(x => x.IsSelected);
                ShowTeamDeathmatchControlMenu(player, matchPlayer.Match, GameModeManager.Ui.ControlMenuTab.Privacy);
            }
        }

        //match.disallow
        [ConsoleCommand(ConsoleCommands.MatchDisallow)]
        void MatchDisallow(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null && matchPlayer.IsMatchLeader)
            {
                matchPlayer.Match.AllowedPlayers.RemoveWhere(x => x.IsSelected);
                ShowTeamDeathmatchControlMenu(player, matchPlayer.Match, GameModeManager.Ui.ControlMenuTab.Privacy);
            }
        }

        //match.leadership
        [ConsoleCommand(ConsoleCommands.TransferLeadership)]
        void TransferLeadership(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null && matchPlayer.IsMatchLeader)
            {
                var targetLeader = matchPlayer.Match.Players.Where(x => x.IsSelected).ToList();
                if (targetLeader.Count > 1)
                {
                    _arena.ShowMessage(player, Messages.SingleSelectionNeeded, this);
                }
                else if (targetLeader.Count == 1)
                {
                    foreach (var otherPlayer in matchPlayer.Match.Players)
                    {
                        otherPlayer.IsMatchLeader = false;
                    }

                    targetLeader[0].IsMatchLeader = true;
                    ShowTeamDeathmatchQuickMenu(player, matchPlayer.Match);
                    ShowTeamDeathmatchQuickMenu(targetLeader[0].Player, matchPlayer.Match);
                    matchPlayer.Match.BannedPlayers.RemoveWhere(x => x.PlayerId == targetLeader[0].PlayerId);
                    foreach (var deathmatchPlayer in targetLeader[0].Match.Players)
                    {
                        _arena.ShowMessage(deathmatchPlayer.Player, Messages.MatchLeadershipChange, this, targetLeader[0].PlayerName);
                    }
                }
            }
        }

        //match.kit.set.blue <name> 
        [ConsoleCommand(ConsoleCommands.SetBlueTeamKit)]
        void SetBlueTeamKit(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            var kit = conArgs.FullString;
            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null && matchPlayer.IsMatchLeader && !matchPlayer.Match.AllowIndividualKit)
            {
                matchPlayer.Match.SetBlueTeamLoadout(kit);
                ShowTeamDeathmatchControlMenu(player, matchPlayer.Match, GameModeManager.Ui.ControlMenuTab.Kits);
            }
        }

        //match.kit.set.red <name> 
        [ConsoleCommand(ConsoleCommands.SetRedTeamKit)]
        void SetRedTeamKit(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            var kit = conArgs.FullString;
            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null && matchPlayer.IsMatchLeader && !matchPlayer.Match.AllowIndividualKit)
            {
                matchPlayer.Match.SetRedTeamLoadout(kit);
                ShowTeamDeathmatchControlMenu(player, matchPlayer.Match, GameModeManager.Ui.ControlMenuTab.Kits);
            }
        }

        //match.item.set <name>
        [ConsoleCommand(ConsoleCommands.GiveItem)]
        void GiveItem(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            var item = conArgs.GetString(0);
            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null && matchPlayer.IsMatchLeader && !matchPlayer.Match.AllowIndividualKit)
            {
                matchPlayer.Match.ToggleItem(item);
                ShowTeamDeathmatchControlMenu(player, matchPlayer.Match, GameModeManager.Ui.ControlMenuTab.Kits);
            }
        }

        //match.kit.take <name>
        [ConsoleCommand(ConsoleCommands.TakeKit)]
        void TakeKit(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            var kit = conArgs.FullString;
            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null && matchPlayer.Match.AllowIndividualKit)
            {
                if (matchPlayer.Team == Team.Blue)
                    matchPlayer.SetBlueTeamLoadout(kit);
                else if (matchPlayer.Team == Team.Red)
                    matchPlayer.SetRedTeamLoadout(kit);
                ShowTeamDeathmatchLoadoutMenu(player, matchPlayer);
            }
        }

        //match.item.take <name>
        [ConsoleCommand(ConsoleCommands.TakeItem)]
        void TakeItem(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            var item = conArgs.GetString(0);
            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null && matchPlayer.Match.AllowIndividualKit)
            {
                matchPlayer.ToggleItem(item);
                ShowTeamDeathmatchLoadoutMenu(player, matchPlayer);
            }
        }

        //match.ready.toggle
        [ConsoleCommand(ConsoleCommands.MatchReadyToggle)]
        void MatchReadyToggle(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }
            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            TogglePlayerReady(player);
        }

        //menu.clear
        [ConsoleCommand(ConsoleCommands.MenuClear)]
        void MenuClear(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                _arena.ShowMessage(player, Messages.PlayerNotFound, this);
                return;
            }

            GameModeManager.Ui.ClearAllMenus(player);

            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null)
            {
                ShowTeamDeathmatchQuickMenu(player, matchPlayer.Match);
            }
        }

        //menu.show <arenaId>
        [ConsoleCommand(ConsoleCommands.MenuShow)]
        void MenuShow(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                _arena.ShowMessage(player, Messages.PlayerNotFound, this);
                return;
            }
            var arenaId = conArgs.GetInt(0);
            TeamDeathmatchArena arena;
            if (!_arenas.TryGetValue(arenaId, out arena) || arena == null)
            {
                _arena.ShowMessage(player, Messages.ArenaNotFound, this);
                return;
            }
            ShowTeamDeathmatchPauseMenu(player, arenaId);
        }

        //loadout.show <arenaId>
        [ConsoleCommand(ConsoleCommands.LoadoutShow)]
        void LoadoutShow(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                _arena.ShowMessage(player, Messages.PlayerNotFound, this);
                return;
            }
            var arenaId = conArgs.GetInt(0);
            TeamDeathmatchArena arena;
            if (!_arenas.TryGetValue(arenaId, out arena) || arena == null)
            {
                _arena.ShowMessage(player, Messages.ArenaNotFound, this);
                return;
            }

            var matchPlayer = arena.Match.Players.FirstOrDefault(x => x.PlayerId == player.userID);
            if (matchPlayer == null)
            {
                _arena.ShowMessage(player, Messages.PlayerNotFound, this);
                return;
            }
            ShowTeamDeathmatchLoadoutMenu(player, matchPlayer);
        }

        //switchteam.show <arenaId>
        [ConsoleCommand(ConsoleCommands.SwitchTeamShow)]
        void SwitchTeamShow(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                _arena.ShowMessage(player, Messages.PlayerNotFound, this);
                return;
            }

            var arenaId = conArgs.GetInt(0);
            TeamDeathmatchArena arena;
            if (!_arenas.TryGetValue(arenaId, out arena) || arena == null)
            {
                _arena.ShowMessage(player, Messages.ArenaNotFound, this);
                return;
            }
            ShowTeamDeathmatchSwitchTeamMenu(player, arena.Data);
        }

        //switchteam <arenaId> <team>
        [ConsoleCommand(ConsoleCommands.SwitchTeam)]
        void SwitchTeam(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                _arena.ShowMessage(player, Messages.PlayerNotFound, this);
                return;
            }

            var teamValue = conArgs.GetString(0);
            Team team;
            if (!Enum.TryParse(teamValue, true, out team))
            {
                _arena.ShowMessage(player, Messages.TeamNotFound, this);
                return;
            }

            SwitchTeam(player, team);
        }

        //leaderboard.show <arenaId> <isMatch>
        [ConsoleCommand(ConsoleCommands.LeaderboardShow)]
        void LeaderboardShow(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                _arena.ShowMessage(player, Messages.PlayerNotFound, this);
                return;
            }
            var arenaId = conArgs.GetInt(0);
            var isMatchResult = conArgs.GetBool(1, true);
            TeamDeathmatchArena arena;
            if (!_arenas.TryGetValue(arenaId, out arena) || arena == null)
            {
                isMatchResult = false;
            }
            GameModeManager.PlayerPageIndex playerPageIndex;
            if (_arena.PlayerPageIndexes.TryGetValue(player.userID, out playerPageIndex) && playerPageIndex != null)
            {
                playerPageIndex.LeaderboardIndex = 0;
            }
            ShowTeamDeathmatchLeaderboardMenu(player, arena?.Match, isMatchResult);
        }

        //control.show <arenaId> <tab>
        [ConsoleCommand(ConsoleCommands.ControlShow)]
        void ControlShow(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                _arena.ShowMessage(player, Messages.PlayerNotFound, this);
                return;
            }
            var arenaId = conArgs.GetInt(0);
            TeamDeathmatchArena arena;
            if (!_arenas.TryGetValue(arenaId, out arena) || arena == null)
            {
                _arena.ShowMessage(player, Messages.ArenaNotFound, this);
                return;
            }

            var tabValue = conArgs.GetString(1);
            GameModeManager.Ui.ControlMenuTab tab;
            if (!Enum.TryParse(tabValue, true, out tab))
            {
                tab = GameModeManager.Ui.ControlMenuTab.General;
            }

            ShowTeamDeathmatchControlMenu(player, arena.Match, tab);
        }

        //player.select.toggle <arenaId> <playerId> <list>
        [ConsoleCommand(ConsoleCommands.PlayerSelectionToggle)]
        void PlayerSelectionToggle(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }
            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            var arenaId = conArgs.GetInt(0);
            TeamDeathmatchArena arena;
            if (!_arenas.TryGetValue(arenaId, out arena) || arena == null)
            {
                _arena.ShowMessage(player, Messages.ArenaNotFound, this);
                return;
            }

            var targetPlayerId = conArgs.GetULong(1);

            var listText = conArgs.GetString(2);
            GameModeManager.ListType list;
            if (Enum.TryParse(listText, true, out list))
            {
                switch (list)
                {
                    case GameModeManager.ListType.Search:
                        {
                            List<GameModeManager.SelectablePlayer> searchResults;
                            if (_arena.PlayerSearchResults.TryGetValue(player.userID, out searchResults) && searchResults != null)
                            {
                                var target = searchResults.FirstOrDefault(x => x.PlayerId == targetPlayerId);
                                if (target != null)
                                {
                                    target.IsSelected = !target.IsSelected;
                                }
                                ShowTeamDeathmatchControlMenu(player, arena.Match, GameModeManager.Ui.ControlMenuTab.Players);
                            }
                            break;
                        }
                    case GameModeManager.ListType.Match:
                        {
                            var target = arena.Match.Players.FirstOrDefault(x => x.PlayerId == targetPlayerId);
                            if (target != null)
                            {
                                target.IsSelected = !target.IsSelected;
                                ShowTeamDeathmatchControlMenu(player, arena.Match, GameModeManager.Ui.ControlMenuTab.Players);
                            }
                            break;
                        }
                    case GameModeManager.ListType.Ban:
                        {
                            var target = arena.Match.BannedPlayers.FirstOrDefault(x => x.PlayerId == targetPlayerId);
                            if (target != null)
                            {
                                target.IsSelected = !target.IsSelected;
                                ShowTeamDeathmatchControlMenu(player, arena.Match, GameModeManager.Ui.ControlMenuTab.Privacy);
                            }
                            break;
                        }
                    case GameModeManager.ListType.Allow:
                        {
                            var target = arena.Match.AllowedPlayers.FirstOrDefault(x => x.PlayerId == targetPlayerId);
                            if (target != null)
                            {
                                target.IsSelected = !target.IsSelected;
                                ShowTeamDeathmatchControlMenu(player, arena.Match, GameModeManager.Ui.ControlMenuTab.Privacy);
                            }
                            break;
                        }
                }
            }

        }

        //page.next <arenaId> <list> [<isMatch>]
        [ConsoleCommand(ConsoleCommands.NextPage)]
        void NextPage(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }
            var arenaId = conArgs.GetInt(0);
            TeamDeathmatchArena arena;

            if (!_arenas.TryGetValue(arenaId, out arena) || arena == null)
            {
                _arena.ShowMessage(player, Messages.ArenaNotFound, this);
                return;
            }

            var listText = conArgs.GetString(1);
            var isMatchResult = conArgs.GetBool(2, true);

            GameModeManager.ListType list;
            if (Enum.TryParse(listText, true, out list))
            {
                switch (list)
                {
                    case GameModeManager.ListType.Search:
                        {
                            GameModeManager.PlayerPageIndex playerPageIndex;
                            if (_arena.PlayerPageIndexes.TryGetValue(player.userID, out playerPageIndex) &&
                                playerPageIndex != null)
                            {
                                playerPageIndex.SearchIndex++;
                            }

                            break;
                        }
                    case GameModeManager.ListType.Match:
                        {
                            GameModeManager.PlayerPageIndex playerPageIndex;
                            if (_arena.PlayerPageIndexes.TryGetValue(player.userID, out playerPageIndex) &&
                                playerPageIndex != null)
                            {
                                playerPageIndex.MatchIndex++;
                            }

                            break;
                        }
                    case GameModeManager.ListType.Ban:
                        {
                            GameModeManager.PlayerPageIndex playerPageIndex;
                            if (_arena.PlayerPageIndexes.TryGetValue(player.userID, out playerPageIndex) &&
                                playerPageIndex != null)
                            {
                                playerPageIndex.BanIndex++;
                            }

                            break;
                        }
                    case GameModeManager.ListType.Allow:
                        {
                            GameModeManager.PlayerPageIndex playerPageIndex;
                            if (_arena.PlayerPageIndexes.TryGetValue(player.userID, out playerPageIndex) &&
                                playerPageIndex != null)
                            {
                                playerPageIndex.AllowIndex++;
                            }

                            break;
                        }
                    case GameModeManager.ListType.Leaderboard:
                        {
                            GameModeManager.PlayerPageIndex playerPageIndex;
                            if (_arena.PlayerPageIndexes.TryGetValue(player.userID, out playerPageIndex) &&
                                playerPageIndex != null)
                            {
                                playerPageIndex.LeaderboardIndex++;
                            }
                            ShowTeamDeathmatchLeaderboardMenu(player, arena.Match, isMatchResult);
                            return;
                        }
                }
            }
            ShowTeamDeathmatchControlMenu(player, arena.Match, GameModeManager.Ui.ControlMenuTab.Players);
        }

        //page.prev <arenaId> <list> [<isMatch>]
        [ConsoleCommand(ConsoleCommands.PreviousPage)]
        void PreviousPage(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }
            var arenaId = conArgs.GetInt(0);
            TeamDeathmatchArena arena;
            if (!_arenas.TryGetValue(arenaId, out arena) || arena == null)
            {
                _arena.ShowMessage(player, Messages.ArenaNotFound, this);
                return;
            }

            var listText = conArgs.GetString(1);
            var isMatchResult = conArgs.GetBool(2, true);
            GameModeManager.ListType list;
            if (Enum.TryParse(listText, true, out list))
            {
                switch (list)
                {
                    case GameModeManager.ListType.Search:
                        {
                            GameModeManager.PlayerPageIndex playerPageIndex;
                            if (_arena.PlayerPageIndexes.TryGetValue(player.userID, out playerPageIndex) && playerPageIndex != null)
                            {
                                playerPageIndex.SearchIndex--;
                            }

                            break;
                        }
                    case GameModeManager.ListType.Match:
                        {
                            GameModeManager.PlayerPageIndex playerPageIndex;
                            if (_arena.PlayerPageIndexes.TryGetValue(player.userID, out playerPageIndex) && playerPageIndex != null)
                            {
                                playerPageIndex.MatchIndex--;
                            }

                            break;
                        }
                    case GameModeManager.ListType.Ban:
                        {
                            GameModeManager.PlayerPageIndex playerPageIndex;
                            if (_arena.PlayerPageIndexes.TryGetValue(player.userID, out playerPageIndex) && playerPageIndex != null)
                            {
                                playerPageIndex.BanIndex--;
                            }

                            break;
                        }
                    case GameModeManager.ListType.Allow:
                        {
                            GameModeManager.PlayerPageIndex playerPageIndex;
                            if (_arena.PlayerPageIndexes.TryGetValue(player.userID, out playerPageIndex) && playerPageIndex != null)
                            {
                                playerPageIndex.AllowIndex--;
                            }

                            break;
                        }
                    case GameModeManager.ListType.Leaderboard:
                        {
                            GameModeManager.PlayerPageIndex playerPageIndex;
                            if (_arena.PlayerPageIndexes.TryGetValue(player.userID, out playerPageIndex) && playerPageIndex != null)
                            {
                                playerPageIndex.LeaderboardIndex--;
                            }
                            ShowTeamDeathmatchLeaderboardMenu(player, arena.Match, isMatchResult);
                            return;
                        }
                }
            }

            ShowTeamDeathmatchControlMenu(player, arena.Match, GameModeManager.Ui.ControlMenuTab.Players);
        }

        //players.search <arenaId>
        [ConsoleCommand(ConsoleCommands.PlayersSearch)]
        void PlayersSearch(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }

            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            var arenaId = conArgs.GetInt(0);
            TeamDeathmatchArena arena;
            if (!_arenas.TryGetValue(arenaId, out arena) || arena == null)
            {
                _arena.ShowMessage(player, Messages.ArenaNotFound, this);
                return;
            }

            var searchInput = string.Empty;
            if (_arena.PlayerSearch.TryGetValue(player.userID, out searchInput) && !string.IsNullOrWhiteSpace(searchInput))
            {
                _arena.PlayerSearchResults[player.userID] = BasePlayer.allPlayerList.Where(x => x.displayName.Contains(searchInput, CompareOptions.OrdinalIgnoreCase))
                    .Select(x => new GameModeManager.SelectablePlayer(x.userID, x.displayName)).ToList();
            }
            else
            {
                _arena.PlayerSearchResults.Remove(player.userID);
            }
            ShowTeamDeathmatchControlMenu(player, arena.Match, GameModeManager.Ui.ControlMenuTab.Players);
        }

        //players.search.input <inputText>
        [ConsoleCommand(ConsoleCommands.PlayersSearchInput)]
        void PlayersSearchInput(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
                return;

            var player = conArgs.Player();

            if (player == null)
            {
                return;
            }

            var inputText = conArgs.FullString;
            if (!string.IsNullOrWhiteSpace(inputText))
            {
                _arena.PlayerSearch[player.userID] = inputText;
            }
        }

        //input.clear <arenaId>
        [ConsoleCommand(ConsoleCommands.ClearInput)]
        void ClearInput(ConsoleSystem.Arg conArgs)
        {
            if (conArgs == null)
            {
                return;
            }
            var player = conArgs.Player();
            if (player == null)
            {
                return;
            }

            var arenaId = conArgs.GetInt(0);
            TeamDeathmatchArena arena;
            if (!_arenas.TryGetValue(arenaId, out arena) || arena == null)
            {
                _arena.ShowMessage(player, Messages.ArenaNotFound, this);
                return;
            }
            _arena.PlayerSearch.Remove(player.userID);
            ShowTeamDeathmatchControlMenu(player, arena.Match, GameModeManager.Ui.ControlMenuTab.Players);
        }
        #endregion GUICommands
        #endregion Commands

        #region GUI

        public void ShowTeamDeathmatchControlMenu(BasePlayer player, TeamDeathmatchMatch match, GameModeManager.Ui.ControlMenuTab tab = GameModeManager.Ui.ControlMenuTab.General)
        {
            var leader = match.Players.FirstOrDefault(x => x.IsMatchLeader);
            if (leader == null || leader.PlayerId != player.userID)
            {
                return;
            }
            GameModeManager.Ui.ClearAllMenus(player);
            //_arena.FreezePlayer(player);
            var mainContainer = GameModeManager.Ui.Container(GameModeManager.Ui.Panels.Fullscreen, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Black, 0f), GameModeManager.Ui.GetMin(0, 0), GameModeManager.Ui.GetMax(0, 0), false);
            var container = GameModeManager.Ui.Container(GameModeManager.Ui.Panels.ControlMenu,
                GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ShadowBlack, 0.55f), GameModeManager.Ui.GetMin(420, 115), GameModeManager.Ui.GetMax(420, 281), true, true, GameModeManager.Ui.Panels.Fullscreen);

            ShowTeamDeathmatchMenuHeader(ref mainContainer, GameModeManager.Ui.Panels.Fullscreen, GameModeManager.Ui.MenuTab.Control, player.userID, match.Arena.Data.Id);
            if (tab != GameModeManager.Ui.ControlMenuTab.Players)
            {
                _arena.PlayerSearchResults.Remove(player.userID);
            }

            var upImage = _arena.ImageManager.GetImage(nameof(GameModeManager.Configuration.UpArrow));
            var downImage = _arena.ImageManager.GetImage(nameof(GameModeManager.Configuration.DownArrow));
            var searchImage = _arena.ImageManager.GetImage(nameof(GameModeManager.Configuration.Search));
            var searchButtonImage = _arena.ImageManager.GetImage(nameof(GameModeManager.Configuration.SearchButton));
            var clearImage = _arena.ImageManager.GetImage(nameof(GameModeManager.Configuration.Clear));
            var closeImage = _arena.ImageManager.GetImage(nameof(GameModeManager.Configuration.Close));
            var checkImage = _arena.ImageManager.GetImage(nameof(GameModeManager.Configuration.Checked));

            var upImageIsUrl = false;
            var downImageIsUrl = false;
            var searchImageIsUrl = false;
            var searchButtonImageIsUrl = false;
            var clearImageIsUrl = false;
            var closeImageIsUrl = false;
            var checkImageIsUrl = false;

            if (string.IsNullOrWhiteSpace(upImage))
            {
                ServerMgr.Instance.StartCoroutine(_arena.ImageManager.DownloadImage(nameof(GameModeManager.Configuration.UpArrow), _arena.PluginConfig.UpArrow));
                upImage = _arena.PluginConfig.UpArrow;
                upImageIsUrl = true;
            }
            if (string.IsNullOrWhiteSpace(downImage))
            {
                ServerMgr.Instance.StartCoroutine(_arena.ImageManager.DownloadImage(nameof(GameModeManager.Configuration.DownArrow), _arena.PluginConfig.DownArrow));
                downImage = _arena.PluginConfig.DownArrow;
                downImageIsUrl = true;
            }
            if (string.IsNullOrWhiteSpace(searchImage))
            {
                ServerMgr.Instance.StartCoroutine(_arena.ImageManager.DownloadImage(nameof(GameModeManager.Configuration.Search), _arena.PluginConfig.Search));
                searchImage = _arena.PluginConfig.Search;
                searchImageIsUrl = true;
            }
            if (string.IsNullOrWhiteSpace(searchButtonImage))
            {
                ServerMgr.Instance.StartCoroutine(_arena.ImageManager.DownloadImage(nameof(GameModeManager.Configuration.SearchButton), _arena.PluginConfig.SearchButton));
                searchButtonImage = _arena.PluginConfig.SearchButton;
                searchButtonImageIsUrl = true;
            }
            if (string.IsNullOrWhiteSpace(clearImage))
            {
                ServerMgr.Instance.StartCoroutine(_arena.ImageManager.DownloadImage(nameof(GameModeManager.Configuration.Clear), _arena.PluginConfig.Clear));
                clearImage = _arena.PluginConfig.Clear;
                clearImageIsUrl = true;
            }
            if (string.IsNullOrWhiteSpace(closeImage))
            {
                ServerMgr.Instance.StartCoroutine(_arena.ImageManager.DownloadImage(nameof(GameModeManager.Configuration.Close), _arena.PluginConfig.Close));
                closeImage = _arena.PluginConfig.Close;
                closeImageIsUrl = true;
            }
            if (string.IsNullOrWhiteSpace(checkImage))
            {
                ServerMgr.Instance.StartCoroutine(_arena.ImageManager.DownloadImage(nameof(GameModeManager.Configuration.Checked), _arena.PluginConfig.Checked));
                checkImage = _arena.PluginConfig.Checked;
                checkImageIsUrl = true;
            }


            GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray), "MATCH CONTROL MENU",
                GameModeManager.Ui.GetMin(12, 624, 1080, 684), GameModeManager.Ui.GetMax(67, 12, 1080, 684), 22);
            GameModeManager.Ui.ImageButton(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Red), closeImage,
                    GameModeManager.Ui.GetMin(1020, 624, 1080, 684), GameModeManager.Ui.GetMax(12, 12, 1080, 684),
                    $"{ConsoleCommands.MenuClear}", GameModeManager.Ui.GetMin(15, 15, 48, 48),
                    GameModeManager.Ui.GetMax(15, 15, 48, 48), closeImageIsUrl);

            GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), "GENERAL", 22,
                GameModeManager.Ui.GetMin(12, 550, 1080, 684), GameModeManager.Ui.GetMax(808.5f, 90, 1080, 684),
                $"{ConsoleCommands.ControlShow} {match.Arena.Data.Id} {GameModeManager.Ui.ControlMenuTab.General}", false,
                tab == GameModeManager.Ui.ControlMenuTab.General ? GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.White) : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.TextGray));
            GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, tab == GameModeManager.Ui.ControlMenuTab.General ? GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.White) : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray, 0.9f),
                GameModeManager.Ui.GetMin(12, 546, 1080, 684), GameModeManager.Ui.GetMax(808.5f, 136, 1080, 684));

            GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), "PLAYERS", 22,
                GameModeManager.Ui.GetMin(277, 550, 1080, 684), GameModeManager.Ui.GetMax(543.5f, 90, 1080, 684),
                $"{ConsoleCommands.ControlShow} {match.Arena.Data.Id} {GameModeManager.Ui.ControlMenuTab.Players}", false,
                tab == GameModeManager.Ui.ControlMenuTab.Players ? GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.White) : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.TextGray));
            GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, tab == GameModeManager.Ui.ControlMenuTab.Players ? GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.White) : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray, 0.9f),
                GameModeManager.Ui.GetMin(277, 546, 1080, 684), GameModeManager.Ui.GetMax(543.5f, 136, 1080, 684));

            GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), "KITS", 22,
                GameModeManager.Ui.GetMin(542, 550, 1080, 684), GameModeManager.Ui.GetMax(278.5f, 90, 1080, 684),
                $"{ConsoleCommands.ControlShow} {match.Arena.Data.Id} {GameModeManager.Ui.ControlMenuTab.Kits}", false,
                tab == GameModeManager.Ui.ControlMenuTab.Kits ? GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.White) : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.TextGray));
            GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, tab == GameModeManager.Ui.ControlMenuTab.Kits ? GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.White) : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray, 0.9f),
                GameModeManager.Ui.GetMin(542, 546, 1080, 684), GameModeManager.Ui.GetMax(278.5f, 136, 1080, 684));

            GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), "BANS", 22,
                GameModeManager.Ui.GetMin(808, 550, 1080, 684), GameModeManager.Ui.GetMax(12.5f, 90, 1080, 684),
                $"{ConsoleCommands.ControlShow} {match.Arena.Data.Id} {GameModeManager.Ui.ControlMenuTab.Privacy}", false,
                tab == GameModeManager.Ui.ControlMenuTab.Privacy ? GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.White) : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.TextGray));
            GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, tab == GameModeManager.Ui.ControlMenuTab.Privacy ? GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.White) : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray, 0.9f),
                GameModeManager.Ui.GetMin(808, 546, 1080, 684), GameModeManager.Ui.GetMax(12.5f, 136, 1080, 684));

            switch (tab)
            {
                case GameModeManager.Ui.ControlMenuTab.General:
                    {
                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), "RESET STATS",
                            GameModeManager.Ui.GetMin(12, 476, 1080, 684), GameModeManager.Ui.GetMax(686, 160, 1080, 684), 22,
                            GameModeManager.Ui.GetMin(34, 476, 1080, 684), align: TextAnchor.MiddleLeft);
                        GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue), "RESET", 19,
                            GameModeManager.Ui.GetMin(257, 484, 1080, 684), GameModeManager.Ui.GetMax(694, 168, 1080, 684),
                            $"{ConsoleCommands.ResetMatchStats}", false);

                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), "RESET KITS",
                            GameModeManager.Ui.GetMin(12, 416, 1080, 684), GameModeManager.Ui.GetMax(686, 220, 1080, 684), 22,
                            GameModeManager.Ui.GetMin(34, 416, 1080, 684), align: TextAnchor.MiddleLeft);
                        GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue), "RESET", 19,
                            GameModeManager.Ui.GetMin(257, 424, 1080, 684), GameModeManager.Ui.GetMax(694, 228, 1080, 684),
                            $"{ConsoleCommands.ResetLoadout}", false);

                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), "RESET MATCH",
                            GameModeManager.Ui.GetMin(12, 356, 1080, 684), GameModeManager.Ui.GetMax(686, 280, 1080, 684), 22,
                            GameModeManager.Ui.GetMin(34, 356, 1080, 684), align: TextAnchor.MiddleLeft);
                        GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue), "RESET", 19,
                            GameModeManager.Ui.GetMin(257, 364, 1080, 684), GameModeManager.Ui.GetMax(694, 288, 1080, 684),
                            $"{ConsoleCommands.ResetMatch}", false);

                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), "REMOVE WALLS",
                            GameModeManager.Ui.GetMin(12, 296, 1080, 684), GameModeManager.Ui.GetMax(686, 340, 1080, 684), 22,
                            GameModeManager.Ui.GetMin(34, 296, 1080, 684), align: TextAnchor.MiddleLeft);
                        GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue), "REMOVE", 19,
                            GameModeManager.Ui.GetMin(257, 304, 1080, 684), GameModeManager.Ui.GetMax(694, 348, 1080, 684),
                            $"{ConsoleCommands.RemoveWalls}", false);

                        if (!match.IsStarted)
                        {
                            GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), "FORCE START",
                                GameModeManager.Ui.GetMin(12, 174, 1080, 684), GameModeManager.Ui.GetMax(686, 462, 1080, 684), 22,
                                GameModeManager.Ui.GetMin(34, 174, 1080, 684), align: TextAnchor.MiddleLeft);
                            GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Green), "READY", 19,
                                GameModeManager.Ui.GetMin(257, 182, 1080, 684), GameModeManager.Ui.GetMax(694, 470, 1080, 684),
                                $"{ConsoleCommands.ForceStart}", false);
                        }

                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), "KICK ALL",
                            GameModeManager.Ui.GetMin(12, 114, 1080, 684), GameModeManager.Ui.GetMax(686, 522, 1080, 684), 22,
                            GameModeManager.Ui.GetMin(34, 114, 1080, 684), align: TextAnchor.MiddleLeft);
                        GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Red), "KICK", 19,
                            GameModeManager.Ui.GetMin(257, 122, 1080, 684), GameModeManager.Ui.GetMax(694, 530, 1080, 684),
                            $"{ConsoleCommands.KickAll}", false);

                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), "WHITELIST MODE",
                            GameModeManager.Ui.GetMin(416, 476, 1080, 684), GameModeManager.Ui.GetMax(282, 160, 1080, 684), 22,
                            GameModeManager.Ui.GetMin(438, 476, 1080, 684), align: TextAnchor.MiddleLeft);
                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue),
                            GameModeManager.Ui.GetMin(758, 484, 1080, 684), GameModeManager.Ui.GetMax(290, 168, 1080, 684));
                        GameModeManager.Ui.ImageButton(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), match.RestrictAccess ? checkImage : string.Empty,
                            GameModeManager.Ui.GetMin(760, 486, 1080, 684), GameModeManager.Ui.GetMax(292, 170, 1080, 684),
                            $"{ConsoleCommands.LockMatch}", isUrl: checkImageIsUrl);

                        /*GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), "ALLOW INDIVIDUAL KITS",
                            GameModeManager.Ui.GetMin(416, 416, 1080, 684), GameModeManager.Ui.GetMax(282, 220, 1080, 684), 22,
                            GameModeManager.Ui.GetMin(438, 416, 1080, 684), align: TextAnchor.MiddleLeft);
                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue),
                            GameModeManager.Ui.GetMin(758, 425, 1080, 684), GameModeManager.Ui.GetMax(290, 227, 1080, 684));
                        GameModeManager.Ui.ImageButton(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), match.AllowIndividualKit ? checkImage : string.Empty,
                            GameModeManager.Ui.GetMin(760, 427, 1080, 684), GameModeManager.Ui.GetMax(292, 229, 1080, 684),
                            $"{ConsoleCommands.AllowIndividualKit} true", isUrl: checkImageIsUrl);*/

                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), "AUTO START ROUNDS",
                            GameModeManager.Ui.GetMin(416, 416, 1080, 684), GameModeManager.Ui.GetMax(282, 220, 1080, 684), 22,
                            GameModeManager.Ui.GetMin(438, 416, 1080, 684), align: TextAnchor.MiddleLeft);
                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue),
                            GameModeManager.Ui.GetMin(758, 425, 1080, 684), GameModeManager.Ui.GetMax(290, 227, 1080, 684));
                        GameModeManager.Ui.ImageButton(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), match.WillAutoStart ? checkImage : string.Empty,
                            GameModeManager.Ui.GetMin(760, 427, 1080, 684), GameModeManager.Ui.GetMax(292, 229, 1080, 684),
                            $"{ConsoleCommands.ToggleAutoStartRound}", isUrl: checkImageIsUrl);

                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), "COUNTDOWN ENABLED",
                            GameModeManager.Ui.GetMin(416, 356, 1080, 684), GameModeManager.Ui.GetMax(282, 280, 1080, 684), 22,
                            GameModeManager.Ui.GetMin(438, 356, 1080, 684), align: TextAnchor.MiddleLeft);
                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue),
                            GameModeManager.Ui.GetMin(758, 365, 1080, 684), GameModeManager.Ui.GetMax(290, 287, 1080, 684));
                        GameModeManager.Ui.ImageButton(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), match.IsCountDownEnabled ? checkImage : string.Empty,
                            GameModeManager.Ui.GetMin(760, 367, 1080, 684), GameModeManager.Ui.GetMax(292, 289, 1080, 684),
                            $"{ConsoleCommands.ToggleCountdown}", isUrl: checkImageIsUrl);



                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), "MATCH ROUNDS",
                            GameModeManager.Ui.GetMin(416, 174, 1080, 684), GameModeManager.Ui.GetMax(282, 462, 1080, 684), 22,
                            GameModeManager.Ui.GetMin(438, 174, 1080, 684), align: TextAnchor.MiddleLeft);
                        GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, match.RoundCount == match.Arena.Data.RoundsCounts[0] ? GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray, 0.9f) : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue),
                            match.Arena.Data.RoundsCounts[0] > 0 ? $"{match.Arena.Data.RoundsCounts[0]}" : "∞", 19, GameModeManager.Ui.GetMin(678, 182, 1080, 684), GameModeManager.Ui.GetMax(370, 470, 1080, 684),
                            $"{ConsoleCommands.SetRoundCount} {match.Arena.Data.RoundsCounts[0]}", false);
                        GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, match.RoundCount == match.Arena.Data.RoundsCounts[1] ? GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray, 0.9f) : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue),
                            match.Arena.Data.RoundsCounts[1] > 0 ? $"{match.Arena.Data.RoundsCounts[1]}" : "∞", 19, GameModeManager.Ui.GetMin(718, 182, 1080, 684), GameModeManager.Ui.GetMax(330, 470, 1080, 684),
                            $"{ConsoleCommands.SetRoundCount} {match.Arena.Data.RoundsCounts[1]}", false);
                        GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, match.RoundCount == match.Arena.Data.RoundsCounts[2] ? GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray, 0.9f) : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue),
                            match.Arena.Data.RoundsCounts[2] > 0 ? $"{match.Arena.Data.RoundsCounts[2]}" : "∞", 19, GameModeManager.Ui.GetMin(758, 182, 1080, 684), GameModeManager.Ui.GetMax(290, 470, 1080, 684),
                            $"{ConsoleCommands.SetRoundCount} {match.Arena.Data.RoundsCounts[2]}", false);

                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), "ROUND TIMER",
                            GameModeManager.Ui.GetMin(416, 114, 1080, 684), GameModeManager.Ui.GetMax(282, 522, 1080, 684), 22,
                            GameModeManager.Ui.GetMin(438, 114, 1080, 684), align: TextAnchor.MiddleLeft);
                        GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, match.RoundTime == 2 ? GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray, 0.9f) : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue),
                            "2", 19,
                            GameModeManager.Ui.GetMin(678, 122, 1080, 684), GameModeManager.Ui.GetMax(370, 530, 1080, 684),
                            $"{ConsoleCommands.SetRoundTime} 2", false);
                        GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, match.RoundTime == 5 ? GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray, 0.9f) : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue),
                            "5", 19,
                            GameModeManager.Ui.GetMin(718, 122, 1080, 684), GameModeManager.Ui.GetMax(330, 530, 1080, 684),
                            $"{ConsoleCommands.SetRoundTime} 5", false);
                        GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, match.RoundTime == 10 ? GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray, 0.9f) : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue),
                            "10", 19,
                            GameModeManager.Ui.GetMin(758, 122, 1080, 684), GameModeManager.Ui.GetMax(290, 530, 1080, 684),
                            $"{ConsoleCommands.SetRoundTime} 10", false);
                    }
                    break;
                case GameModeManager.Ui.ControlMenuTab.Players:
                    {
                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue, 0.9f), "ACTIONS",
                            GameModeManager.Ui.GetMin(12, 492, 1080, 684), GameModeManager.Ui.GetMax(959, 160, 1080, 684), 19);
                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack),
                            GameModeManager.Ui.GetMin(12, 444, 1080, 684), GameModeManager.Ui.GetMax(512, 192, 1080, 684));

                        GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Green), "GIVE LEADER", 19,
                            GameModeManager.Ui.GetMin(20, 452, 1080, 684), GameModeManager.Ui.GetMax(931, 200, 1080, 684),
                            $"{ConsoleCommands.TransferLeadership}", false);
                        GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Green), "KICK", 19,
                            GameModeManager.Ui.GetMin(157, 452, 1080, 684), GameModeManager.Ui.GetMax(794, 200, 1080, 684),
                            $"{ConsoleCommands.KickFromMatch}", false);
                        GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Green), "WHITELIST", 19,
                            GameModeManager.Ui.GetMin(294, 452, 1080, 684), GameModeManager.Ui.GetMax(657, 200, 1080, 684),
                            $"{ConsoleCommands.MatchAllow}", false);
                        GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Red), "BAN", 19,
                            GameModeManager.Ui.GetMin(431, 452, 1080, 684), GameModeManager.Ui.GetMax(520, 200, 1080, 684),
                            $"{ConsoleCommands.MatchBan}", false);

                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue, 0.9f), "PLAYER LOOKUP",
                        GameModeManager.Ui.GetMin(590, 492, 1080, 684), GameModeManager.Ui.GetMax(325, 160, 1080, 684), 19);
                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack),
                            GameModeManager.Ui.GetMin(590, 444, 1080, 684), GameModeManager.Ui.GetMax(61, 192, 1080, 684));

                        GameModeManager.Ui.Image(ref container, GameModeManager.Ui.Panels.ControlMenu, searchImage,
                            GameModeManager.Ui.GetMin(599, 452, 1080, 684), GameModeManager.Ui.GetMax(449, 200, 1080, 684), searchImageIsUrl);
                        GameModeManager.Ui.Input(ref container, GameModeManager.Ui.Panels.ControlMenu, _arena.PlayerSearch.ContainsKey(player.userID) ?
                            _arena.PlayerSearch[player.userID] : string.Empty, 19,
                            GameModeManager.Ui.GetMin(631, 452, 1080, 684), GameModeManager.Ui.GetMax(98, 200, 1080, 684),
                            $"{ConsoleCommands.PlayersSearchInput}");
                        GameModeManager.Ui.ImageButton(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue), searchButtonImage,
                            GameModeManager.Ui.GetMin(1019, 444, 1080, 684), GameModeManager.Ui.GetMax(13, 192, 1080, 684),
                            $"{ConsoleCommands.PlayersSearch} {match.Arena.Data.Id}",
                            GameModeManager.Ui.GetMin(8, 8, 48, 48), GameModeManager.Ui.GetMax(8, 8, 48, 48), searchButtonImageIsUrl);
                        GameModeManager.Ui.ImageButton(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), clearImage,
                            GameModeManager.Ui.GetMin(982, 452, 1080, 684), GameModeManager.Ui.GetMax(66, 200, 1080, 684),
                            $"{ConsoleCommands.ClearInput} {match.Arena.Data.Id}", isUrl: clearImageIsUrl);

                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray),
                            GameModeManager.Ui.GetMin(12, 377, 1080, 684), GameModeManager.Ui.GetMax(639, 275, 1080, 684));
                        GameModeManager.Ui.Label(ref container, GameModeManager.Ui.Panels.ControlMenu, "CURRENT MATCH PLAYERS", 19,
                            GameModeManager.Ui.GetMin(22, 382, 1080, 684), GameModeManager.Ui.GetMax(800, 280, 1080, 684), false, align: TextAnchor.MiddleLeft);

                        var playersCount = match.Players.Count;
                        var matchPlayers = _arena.Filter(player, match.Players, GameModeManager.ListType.Match, 10);
                        if (playersCount > 10)
                        {
                            GameModeManager.Ui.ImageButton(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), upImage,
                                GameModeManager.Ui.GetMin(453, 338, 1080, 684), GameModeManager.Ui.GetMax(601, 320, 1080, 684),
                                $"{ConsoleCommands.NextPage} {match.Arena.Data.Id} {GameModeManager.ListType.Match}", isUrl: upImageIsUrl);
                            GameModeManager.Ui.ImageButton(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), downImage,
                                GameModeManager.Ui.GetMin(453, 302, 1080, 684), GameModeManager.Ui.GetMax(601, 356, 1080, 684),
                                $"{ConsoleCommands.PreviousPage} {match.Arena.Data.Id} {GameModeManager.ListType.Match}", isUrl: downImageIsUrl);
                        }

                        for (var i = 0; i < 10; i++)
                        {
                            var matchPlayer = matchPlayers.ElementAtOrDefault(i);
                            if (matchPlayer != null)
                            {
                                GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, matchPlayer.IsSelected ?
                                    GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.DarkBlue) : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), $"   {matchPlayer.PlayerName}", 18,
                                    GameModeManager.Ui.GetVerticalMin(12, 335, i, 36, 1080, 684),
                                    GameModeManager.Ui.GetVerticalMax(639, 317, i, 36, 1080, 684),
                                    $"{ConsoleCommands.PlayerSelectionToggle} {match.Arena.Data.Id} {matchPlayer.PlayerId} {GameModeManager.ListType.Match}", false, align: TextAnchor.MiddleLeft);

                                GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, matchPlayer.IsSelected ?
                                    GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.DarkBlue) : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), matchPlayer.PlayerId.ToString(), 18,
                                    GameModeManager.Ui.GetVerticalMin(280, 341, i, 36, 1080, 684),
                                    GameModeManager.Ui.GetVerticalMax(648, 322, i, 36, 1080, 684),
                                    $"{ConsoleCommands.PlayerSelectionToggle} {match.Arena.Data.Id} {matchPlayer.PlayerId} {GameModeManager.ListType.Match}", false);
                            }
                        }

                        GameModeManager.Ui.Label(ref container, GameModeManager.Ui.Panels.ControlMenu, playersCount.ToString(), 19,
                            GameModeManager.Ui.GetMin(390, 382, 1080, 684), GameModeManager.Ui.GetMax(648, 280, 1080, 684),
                            false, align: TextAnchor.MiddleRight);

                        List<GameModeManager.SelectablePlayer> searchResult;
                        if (_arena.PlayerSearchResults.TryGetValue(player.userID, out searchResult) && searchResult != null)
                        {
                            GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray),
                                GameModeManager.Ui.GetMin(590, 377, 1080, 684), GameModeManager.Ui.GetMax(61, 275, 1080, 684));
                            GameModeManager.Ui.Label(ref container, GameModeManager.Ui.Panels.ControlMenu, "SEARCH RESULTS", 19,
                                GameModeManager.Ui.GetMin(600, 382, 1080, 684), GameModeManager.Ui.GetMax(150, 280, 1080, 684), false, align: TextAnchor.MiddleLeft);
                            var searchResultCount = searchResult.Count;
                            var foundPlayers = _arena.Filter(player, searchResult, GameModeManager.ListType.Search, 4);
                            if (searchResultCount > 4)
                            {
                                GameModeManager.Ui.ImageButton(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), upImage,
                                    GameModeManager.Ui.GetMin(1031, 338, 1080, 684), GameModeManager.Ui.GetMax(23, 320, 1080, 684),
                                    $"{ConsoleCommands.NextPage} {match.Arena.Data.Id} {GameModeManager.ListType.Search}", isUrl: upImageIsUrl);
                                GameModeManager.Ui.ImageButton(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), downImage,
                                    GameModeManager.Ui.GetMin(1031, 302, 1080, 684), GameModeManager.Ui.GetMax(23, 356, 1080, 684),
                                    $"{ConsoleCommands.PreviousPage} {match.Arena.Data.Id} {GameModeManager.ListType.Search}", isUrl: downImageIsUrl);
                            }

                            for (var i = 0; i < 4; i++)
                            {
                                var resultPlayer = foundPlayers.ElementAtOrDefault(i);
                                if (resultPlayer != null)
                                {
                                    GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, resultPlayer.IsSelected ?
                                        GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.DarkBlue) : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), $"  {resultPlayer.PlayerName}", 18,
                                        GameModeManager.Ui.GetVerticalMin(590, 335, i, 36, 1080, 684),
                                        GameModeManager.Ui.GetVerticalMax(61, 317, i, 36, 1080, 684),
                                        $"{ConsoleCommands.PlayerSelectionToggle} {match.Arena.Data.Id} {resultPlayer.PlayerId} {GameModeManager.ListType.Search}", false, align: TextAnchor.MiddleLeft);

                                    GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, resultPlayer.IsSelected ?
                                        GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.DarkBlue) : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), resultPlayer.PlayerId.ToString(), 18,
                                        GameModeManager.Ui.GetVerticalMin(858, 341, i, 36, 1080, 684),
                                        GameModeManager.Ui.GetVerticalMax(70, 322, i, 36, 1080, 684),
                                        $"{ConsoleCommands.PlayerSelectionToggle} {match.Arena.Data.Id} {resultPlayer.PlayerId} {GameModeManager.ListType.Search}", false);
                                }
                            }

                            GameModeManager.Ui.Label(ref container, GameModeManager.Ui.Panels.ControlMenu, searchResultCount.ToString(), 19,
                                GameModeManager.Ui.GetMin(975, 382, 1080, 684), GameModeManager.Ui.GetMax(70, 280, 1080, 684),
                                false, align: TextAnchor.MiddleRight);
                        }
                    }
                    break;
                case GameModeManager.Ui.ControlMenuTab.Kits:
                    {
                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray), "BLUE TEAM",
                            GameModeManager.Ui.GetMin(12, 342, 1080, 684), GameModeManager.Ui.GetMax(552, 310, 1080, 684),
                            19, GameModeManager.Ui.GetMin(22, 342, 1080, 684), align: TextAnchor.MiddleLeft);

                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray), "RED TEAM",
                            GameModeManager.Ui.GetMin(550, 342, 1080, 684), GameModeManager.Ui.GetMax(14, 310, 1080, 684),
                            19, GameModeManager.Ui.GetMin(560, 342, 1080, 684), align: TextAnchor.MiddleLeft);

                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray), "GIVE",
                            GameModeManager.Ui.GetMin(12, 492, 1080, 684), GameModeManager.Ui.GetMax(552, 160, 1080, 684),
                            19, GameModeManager.Ui.GetMin(22, 492, 1080, 684), align: TextAnchor.MiddleLeft);

                        for (var i = 0; i < match.Arena.Data.LoadoutNames.Count; i++)
                        {
                            var kit = match.Arena.Data.LoadoutNames.ElementAtOrDefault(i);
                            if (!string.IsNullOrWhiteSpace(kit))
                            {
                                string blueTeamKitColor, redTeamKitColor;
                                if (match.BlueTeamSelectedLoadoutName.Equals(kit, StringComparison.OrdinalIgnoreCase))
                                    blueTeamKitColor = GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.DarkBlue);
                                else
                                    blueTeamKitColor = GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Black);

                                if (match.RedTeamSelectedLoadoutName.Equals(kit, StringComparison.OrdinalIgnoreCase))
                                    redTeamKitColor = GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.DarkBlue);
                                else
                                    redTeamKitColor = GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Black);

                                GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu,
                                    blueTeamKitColor, kit, 18,
                                    GameModeManager.Ui.GetGridMin(12, 300, i, 130, 36, 4, 0, 1080, 684),
                                    GameModeManager.Ui.GetGridMax(942, 352, i, 130, 36, 4, 0, 1080, 684),
                                    $"{ConsoleCommands.SetBlueTeamKit} {kit}", false);

                                GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu,
                                    redTeamKitColor, kit, 18,
                                    GameModeManager.Ui.GetGridMin(550, 300, i, 130, 36, 4, 0, 1080, 684),
                                    GameModeManager.Ui.GetGridMax(404, 352, i, 130, 36, 4, 0, 1080, 684),
                                    $"{ConsoleCommands.SetRedTeamKit} {kit}", false);
                            }
                        }

                        for (var i = 0; i < match.Arena.Data.ItemsNames.Count; i++)
                        {
                            var item = match.Arena.Data.ItemsNames.ElementAtOrDefault(i);
                            if (item != null)
                            {
                                GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu,
                                    match.Items.Contains(item.ItemShortName)
                                        ? GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.DarkBlue)
                                        : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack),
                                    item.ItemDisplayName, 18,
                                    GameModeManager.Ui.GetGridMin(12, 450, i, 130, 36, 4, 0, 1080, 684),
                                    GameModeManager.Ui.GetGridMax(942, 202, i, 130, 36, 4, 0, 1080, 684),
                                    $"{ConsoleCommands.GiveItem} {item.ItemShortName}", false);
                            }
                        }

                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue, 0.9f), "ACTIONS",
                                GameModeManager.Ui.GetMin(552, 492, 1080, 684), GameModeManager.Ui.GetMax(419, 160, 1080, 684), 19);
                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack),
                            GameModeManager.Ui.GetMin(552, 444, 1080, 684), GameModeManager.Ui.GetMax(383, 192, 1080, 684));

                        GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Green), "RESET KITS", 19,
                            GameModeManager.Ui.GetMin(560, 452, 1080, 684), GameModeManager.Ui.GetMax(391, 200, 1080, 684),
                            $"{ConsoleCommands.ResetLoadout}", false);

                        /*GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), "ALLOW INDIVIDUAL KITS",
                            GameModeManager.Ui.GetMin(706, 444, 1080, 684), GameModeManager.Ui.GetMax(12, 192, 1080, 684), 19,
                            GameModeManager.Ui.GetMin(728, 444, 1080, 684), align: TextAnchor.MiddleLeft);
                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue),
                            GameModeManager.Ui.GetMin(1028, 452, 1080, 684), GameModeManager.Ui.GetMax(20, 200, 1080, 684));

                        GameModeManager.Ui.ImageButton(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), match.AllowIndividualKit ? checkImage : string.Empty,
                            GameModeManager.Ui.GetMin(1030, 454, 1080, 684), GameModeManager.Ui.GetMax(22, 202, 1080, 684),
                            $"{ConsoleCommands.AllowIndividualKit} false", isUrl: checkImageIsUrl);*/
                    }
                    break;
                case GameModeManager.Ui.ControlMenuTab.Privacy:
                    {
                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue, 0.9f), "ACTIONS",
                            GameModeManager.Ui.GetMin(12, 492, 1080, 684), GameModeManager.Ui.GetMax(959, 160, 1080, 684), 19);
                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack),
                            GameModeManager.Ui.GetMin(12, 444, 1080, 684), GameModeManager.Ui.GetMax(786, 192, 1080, 684));

                        GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Green), "UNBAN", 19,
                            GameModeManager.Ui.GetMin(20, 452, 1080, 684), GameModeManager.Ui.GetMax(931, 200, 1080, 684),
                            $"{ConsoleCommands.MatchUnBan}", false);
                        GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Green), "UNWHITELIST", 19,
                            GameModeManager.Ui.GetMin(157, 452, 1080, 684), GameModeManager.Ui.GetMax(794, 200, 1080, 684),
                            $"{ConsoleCommands.MatchDisallow}", false);


                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray),
                            GameModeManager.Ui.GetMin(12, 377, 1080, 684), GameModeManager.Ui.GetMax(639, 275, 1080, 684));
                        GameModeManager.Ui.Label(ref container, GameModeManager.Ui.Panels.ControlMenu, "BAN LIST", 19,
                            GameModeManager.Ui.GetMin(22, 382, 1080, 684), GameModeManager.Ui.GetMax(800, 280, 1080, 684), false, align: TextAnchor.MiddleLeft);

                        GameModeManager.Ui.Panel(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray),
                            GameModeManager.Ui.GetMin(590, 377, 1080, 684), GameModeManager.Ui.GetMax(61, 275, 1080, 684));
                        GameModeManager.Ui.Label(ref container, GameModeManager.Ui.Panels.ControlMenu, "WHITELISTED PLAYERS", 19,
                            GameModeManager.Ui.GetMin(600, 382, 1080, 684), GameModeManager.Ui.GetMax(150, 280, 1080, 684), false, align: TextAnchor.MiddleLeft);



                        var bannedCount = match.BannedPlayers.Count;
                        var bannedPlayers = _arena.Filter(player, match.BannedPlayers, GameModeManager.ListType.Ban, 10);
                        if (bannedCount > 10)
                        {
                            GameModeManager.Ui.ImageButton(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), upImage,
                                GameModeManager.Ui.GetMin(453, 338, 1080, 684), GameModeManager.Ui.GetMax(601, 320, 1080, 684),
                                $"{ConsoleCommands.NextPage} {match.Arena.Data.Id} {GameModeManager.ListType.Ban}", isUrl: upImageIsUrl);
                            GameModeManager.Ui.ImageButton(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), downImage,
                                GameModeManager.Ui.GetMin(453, 302, 1080, 684), GameModeManager.Ui.GetMax(601, 356, 1080, 684),
                                $"{ConsoleCommands.PreviousPage} {match.Arena.Data.Id} {GameModeManager.ListType.Ban}", isUrl: downImageIsUrl);
                        }

                        for (var i = 0; i < 10; i++)
                        {
                            var bannedPlayer = bannedPlayers.ElementAtOrDefault(i);
                            if (bannedPlayer != null)
                            {
                                GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, bannedPlayer.IsSelected ?
                                    GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.DarkBlue) : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), $"   {bannedPlayer.PlayerName}", 18,
                                    GameModeManager.Ui.GetVerticalMin(12, 335, i, 36, 1080, 684),
                                    GameModeManager.Ui.GetVerticalMax(639, 317, i, 36, 1080, 684),
                                    $"{ConsoleCommands.PlayerSelectionToggle} {match.Arena.Data.Id} {bannedPlayer.PlayerId} {GameModeManager.ListType.Ban}", false, align: TextAnchor.MiddleLeft);

                                GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, bannedPlayer.IsSelected ?
                                    GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.DarkBlue) : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), bannedPlayer.PlayerId.ToString(), 18,
                                    GameModeManager.Ui.GetVerticalMin(280, 341, i, 36, 1080, 684),
                                    GameModeManager.Ui.GetVerticalMax(648, 322, i, 36, 1080, 684),
                                    $"{ConsoleCommands.PlayerSelectionToggle} {match.Arena.Data.Id} {bannedPlayer.PlayerId} {GameModeManager.ListType.Ban}", false);
                            }
                        }

                        var allowedCount = match.AllowedPlayers.Count;
                        var allowedPlayers = _arena.Filter(player, match.AllowedPlayers, GameModeManager.ListType.Allow, 10);
                        if (allowedCount > 10)
                        {
                            GameModeManager.Ui.ImageButton(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), upImage,
                                GameModeManager.Ui.GetMin(1031, 338, 1080, 684), GameModeManager.Ui.GetMax(23, 320, 1080, 684),
                                $"{ConsoleCommands.NextPage} {match.Arena.Data.Id} {GameModeManager.ListType.Allow}", isUrl: upImageIsUrl);
                            GameModeManager.Ui.ImageButton(ref container, GameModeManager.Ui.Panels.ControlMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), downImage,
                                GameModeManager.Ui.GetMin(1031, 302, 1080, 684), GameModeManager.Ui.GetMax(23, 356, 1080, 684),
                                $"{ConsoleCommands.PreviousPage} {match.Arena.Data.Id} {GameModeManager.ListType.Allow}", isUrl: downImageIsUrl);
                        }

                        for (var i = 0; i < 10; i++)
                        {
                            var allowedPlayer = allowedPlayers.ElementAtOrDefault(i);
                            if (allowedPlayer != null)
                            {
                                GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, allowedPlayer.IsSelected ?
                                    GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.DarkBlue) : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), $"  {allowedPlayer.PlayerName}", 18,
                                    GameModeManager.Ui.GetVerticalMin(590, 335, i, 36, 1080, 684),
                                    GameModeManager.Ui.GetVerticalMax(61, 317, i, 36, 1080, 684),
                                    $"{ConsoleCommands.PlayerSelectionToggle} {match.Arena.Data.Id} {allowedPlayer.PlayerId} {GameModeManager.ListType.Allow}", false, align: TextAnchor.MiddleLeft);

                                GameModeManager.Ui.Button(ref container, GameModeManager.Ui.Panels.ControlMenu, allowedPlayer.IsSelected ?
                                    GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.DarkBlue) : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), allowedPlayer.PlayerId.ToString(), 18,
                                    GameModeManager.Ui.GetVerticalMin(858, 341, i, 36, 1080, 684),
                                    GameModeManager.Ui.GetVerticalMax(70, 322, i, 36, 1080, 684),
                                    $"{ConsoleCommands.PlayerSelectionToggle} {match.Arena.Data.Id} {allowedPlayer.PlayerId} {GameModeManager.ListType.Allow}", false);
                            }
                        }

                        GameModeManager.Ui.Label(ref container, GameModeManager.Ui.Panels.ControlMenu, bannedCount.ToString(), 19,
                            GameModeManager.Ui.GetMin(390, 382, 1080, 684), GameModeManager.Ui.GetMax(648, 280, 1080, 684),
                            false, align: TextAnchor.MiddleRight);
                        GameModeManager.Ui.Label(ref container, GameModeManager.Ui.Panels.ControlMenu, allowedCount.ToString(), 19,
                            GameModeManager.Ui.GetMin(975, 382, 1080, 684), GameModeManager.Ui.GetMax(70, 280, 1080, 684),
                            false, align: TextAnchor.MiddleRight);
                    }
                    break;
            }

            mainContainer.AddRange(container);
            CuiHelper.AddUi(player, mainContainer);
        }
        public void ShowTeamDeathmatchLeaderboardMenu(BasePlayer player, TeamDeathmatchMatch match, bool isMatchLeaderboard = true)
        {
            GameModeManager.Ui.ClearAllMenus(player);
            var mainContainer = GameModeManager.Ui.Container(GameModeManager.Ui.Panels.Fullscreen, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Black, 0f), GameModeManager.Ui.GetMin(0, 0), GameModeManager.Ui.GetMax(0, 0), false);

            ShowTeamDeathmatchMenuHeader(ref mainContainer, GameModeManager.Ui.Panels.Fullscreen, GameModeManager.Ui.MenuTab.Leaderboard, player.userID, match.Arena.Data.Id);

            var entities = new List<GameModeManager.PlayerRank>();
            if (isMatchLeaderboard)
            {
                entities = match.Players.OrderByDescending(x => x.Statistics.Score)
                    .Select((x, index) => new GameModeManager.PlayerRank(x.Player)
                    {
                        Rank = index + 1,
                        Kills = x.Statistics.Kills,
                        Deaths = x.Statistics.Deaths,
                        DamagesDone = x.Statistics.DamagesDone
                    }).ToList();
            }
            else
            {
                entities = _arena.GetGlobalStatistics();
            }
            var players = _arena.Filter(player, entities, GameModeManager.ListType.Leaderboard, 10);

            var leaderboardSelection = GameModeManager.Ui.Container(GameModeManager.Ui.Panels.LeaderboardMenu,
                GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ShadowBlack, 0.5f), GameModeManager.Ui.GetMin(750, 733), GameModeManager.Ui.GetMax(750, 238), false, true, GameModeManager.Ui.Panels.Fullscreen);
            var playersList = GameModeManager.Ui.Container(GameModeManager.Ui.Panels.LeaderboardMenu,
                GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ShadowBlack, 0.5f), GameModeManager.Ui.GetMin(468, 197), GameModeManager.Ui.GetMax(468, 359), false, true, GameModeManager.Ui.Panels.Fullscreen);

            var leftArrowImageId = _arena.ImageManager.GetImage(nameof(GameModeManager.Configuration.LeftArrow));
            var rightArrowImageId = _arena.ImageManager.GetImage(nameof(GameModeManager.Configuration.RightArrow));
            var arenaId = match.Arena.Data.Id;
            if (string.IsNullOrWhiteSpace(leftArrowImageId))
            {
                ServerMgr.Instance.StartCoroutine(_arena.ImageManager.DownloadImage(nameof(GameModeManager.Configuration.LeftArrow), _arena.PluginConfig.LeftArrow));
                GameModeManager.Ui.ImageButton(ref mainContainer, GameModeManager.Ui.Panels.Fullscreen, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack, 0.9f),
                    _arena.PluginConfig.LeftArrow, GameModeManager.Ui.GetMin(810, 156), GameModeManager.Ui.GetMax(1084, 898),
                    $"{ConsoleCommands.PreviousPage} {arenaId} {GameModeManager.ListType.Leaderboard} {isMatchLeaderboard}", isUrl: true);
            }
            else
            {
                GameModeManager.Ui.ImageButton(ref mainContainer, GameModeManager.Ui.Panels.Fullscreen, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack, 0.9f),
                    leftArrowImageId, GameModeManager.Ui.GetMin(810, 156), GameModeManager.Ui.GetMax(1084, 898),
                    $"{ConsoleCommands.PreviousPage} {arenaId} {GameModeManager.ListType.Leaderboard} {isMatchLeaderboard}");
            }
            if (string.IsNullOrWhiteSpace(rightArrowImageId))
            {
                ServerMgr.Instance.StartCoroutine(_arena.ImageManager.DownloadImage(nameof(GameModeManager.Configuration.RightArrow), _arena.PluginConfig.RightArrow));
                GameModeManager.Ui.ImageButton(ref mainContainer, GameModeManager.Ui.Panels.Fullscreen, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack, 0.9f),
                    _arena.PluginConfig.RightArrow, GameModeManager.Ui.GetMin(1084, 156), GameModeManager.Ui.GetMax(810, 898),
                    $"{ConsoleCommands.NextPage} {arenaId} {GameModeManager.ListType.Leaderboard} {isMatchLeaderboard}", isUrl: true);
            }
            else
            {
                GameModeManager.Ui.ImageButton(ref mainContainer, GameModeManager.Ui.Panels.Fullscreen, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack, 0.9f),
                    rightArrowImageId, GameModeManager.Ui.GetMin(1084, 156), GameModeManager.Ui.GetMax(810, 898),
                    $"{ConsoleCommands.NextPage} {arenaId} {GameModeManager.ListType.Leaderboard} {isMatchLeaderboard}");
            }
            GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.Fullscreen, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue, 0.9f), "BACK TO GAME",
                19, GameModeManager.Ui.GetMin(840, 153), GameModeManager.Ui.GetMax(840, 895), $"{ConsoleCommands.MenuClear}", false);

            GameModeManager.Ui.Panel(ref leaderboardSelection, GameModeManager.Ui.Panels.LeaderboardMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray), "LEADERBOARD",
                GameModeManager.Ui.GetMin(14, 57, 420, 109), GameModeManager.Ui.GetMax(14, 12, 420, 109), 22);

            GameModeManager.Ui.Button(ref leaderboardSelection, GameModeManager.Ui.Panels.LeaderboardMenu,
                isMatchLeaderboard ? GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue) : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack),
                "CURRENT GAME", 19, GameModeManager.Ui.GetMin(14, 12, 420, 109), GameModeManager.Ui.GetMax(213, 57, 420, 109),
                $"{ConsoleCommands.LeaderboardShow} {arenaId} {true}", false);

            GameModeManager.Ui.Button(ref leaderboardSelection, GameModeManager.Ui.Panels.LeaderboardMenu,
                isMatchLeaderboard ? GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack) : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue),
                "GLOBAL", 19, GameModeManager.Ui.GetMin(213, 12, 420, 109), GameModeManager.Ui.GetMax(14, 57, 420, 109),
                $"{ConsoleCommands.LeaderboardShow} {arenaId} {false}", false);

            GameModeManager.Ui.Panel(ref playersList, GameModeManager.Ui.Panels.LeaderboardMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray), "RANK",
                GameModeManager.Ui.GetMin(12, 472, 984, 524), GameModeManager.Ui.GetMax(897, 12, 984, 524), 22);
            GameModeManager.Ui.Panel(ref playersList, GameModeManager.Ui.Panels.LeaderboardMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray),
                GameModeManager.Ui.GetMin(92, 472, 984, 524), GameModeManager.Ui.GetMax(852, 12, 984, 524));
            GameModeManager.Ui.Panel(ref playersList, GameModeManager.Ui.Panels.LeaderboardMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray), "NAME",
                GameModeManager.Ui.GetMin(137, 472, 984, 524), GameModeManager.Ui.GetMax(357, 12, 984, 524), 22,
                GameModeManager.Ui.GetMin(149, 472, 984, 524), align: TextAnchor.MiddleLeft);
            GameModeManager.Ui.Panel(ref playersList, GameModeManager.Ui.Panels.LeaderboardMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray), "KILLS",
                GameModeManager.Ui.GetMin(632, 472, 984, 524), GameModeManager.Ui.GetMax(242, 12, 984, 524), 22);
            GameModeManager.Ui.Panel(ref playersList, GameModeManager.Ui.Panels.LeaderboardMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray), "DEATHS",
                GameModeManager.Ui.GetMin(747, 472, 984, 524), GameModeManager.Ui.GetMax(127, 12, 984, 524), 22);
            GameModeManager.Ui.Panel(ref playersList, GameModeManager.Ui.Panels.LeaderboardMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray), "DAMAGE",
                GameModeManager.Ui.GetMin(862, 472, 984, 524), GameModeManager.Ui.GetMax(12, 12, 984, 524), 22);
            for (var i = 0; i < 10; i++)
            {
                var playerStats = players.ElementAtOrDefault(i);
                if (playerStats == null || playerStats.Player == null)
                {
                    GameModeManager.Ui.Panel(ref playersList, GameModeManager.Ui.Panels.LeaderboardMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack),
                        GameModeManager.Ui.GetVerticalMin(12, 422, i, 45, 984, 524), GameModeManager.Ui.GetVerticalMax(897, 62, i, 45, 984, 524));
                    GameModeManager.Ui.Panel(ref playersList, GameModeManager.Ui.Panels.LeaderboardMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack),
                        GameModeManager.Ui.GetVerticalMin(92, 422, i, 45, 984, 524), GameModeManager.Ui.GetVerticalMax(852, 62, i, 45, 984, 524));
                    GameModeManager.Ui.Panel(ref playersList, GameModeManager.Ui.Panels.LeaderboardMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack),
                        GameModeManager.Ui.GetVerticalMin(137, 422, i, 45, 984, 524), GameModeManager.Ui.GetVerticalMax(357, 62, i, 45, 984, 524));
                    GameModeManager.Ui.Panel(ref playersList, GameModeManager.Ui.Panels.LeaderboardMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack),
                        GameModeManager.Ui.GetVerticalMin(632, 422, i, 45, 984, 524), GameModeManager.Ui.GetVerticalMax(242, 62, i, 45, 984, 524));
                    GameModeManager.Ui.Panel(ref playersList, GameModeManager.Ui.Panels.LeaderboardMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack),
                        GameModeManager.Ui.GetVerticalMin(747, 422, i, 45, 984, 524), GameModeManager.Ui.GetVerticalMax(127, 62, i, 45, 984, 524));
                    GameModeManager.Ui.Panel(ref playersList, GameModeManager.Ui.Panels.LeaderboardMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack),
                        GameModeManager.Ui.GetVerticalMin(862, 422, i, 45, 984, 524), GameModeManager.Ui.GetVerticalMax(12, 62, i, 45, 984, 524));
                }
                else
                {
                    string avatarUrl;
                    var color = playerStats.Player.userID == player.userID
                        ? GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.DarkBlue)
                        : GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack);
                    if (_arena.PlayersAvatarUrls.TryGetValue(playerStats.Player.userID, out avatarUrl) && !string.IsNullOrWhiteSpace(avatarUrl))
                    {
                        GameModeManager.Ui.Image(ref playersList, GameModeManager.Ui.Panels.LeaderboardMenu, avatarUrl,
                            GameModeManager.Ui.GetVerticalMin(92, 422, i, 45, 984, 524), GameModeManager.Ui.GetVerticalMax(852, 62, i, 45, 984, 524),
                            true);
                    }
                    else
                    {
                        GameModeManager.Ui.Panel(ref playersList, GameModeManager.Ui.Panels.LeaderboardMenu, color,
                            GameModeManager.Ui.GetVerticalMin(92, 422, i, 45, 984, 524), GameModeManager.Ui.GetVerticalMax(852, 62, i, 45, 984, 524));
                    }
                    GameModeManager.Ui.Panel(ref playersList, GameModeManager.Ui.Panels.LeaderboardMenu, color, playerStats.Rank.ToString(),
                        GameModeManager.Ui.GetVerticalMin(12, 422, i, 45, 984, 524), GameModeManager.Ui.GetVerticalMax(897, 62, i, 45, 984, 524), 19);
                    GameModeManager.Ui.Panel(ref playersList, GameModeManager.Ui.Panels.LeaderboardMenu, color, playerStats.Player.displayName,
                        GameModeManager.Ui.GetVerticalMin(137, 422, i, 45, 984, 524), GameModeManager.Ui.GetVerticalMax(357, 62, i, 45, 984, 524), 19,
                        GameModeManager.Ui.GetVerticalMin(149, 422, i, 45, 984, 524), align: TextAnchor.MiddleLeft);
                    GameModeManager.Ui.Panel(ref playersList, GameModeManager.Ui.Panels.LeaderboardMenu, color, playerStats.Kills.ToString(),
                        GameModeManager.Ui.GetVerticalMin(632, 422, i, 45, 984, 524), GameModeManager.Ui.GetVerticalMax(242, 62, i, 45, 984, 524), 19);
                    GameModeManager.Ui.Panel(ref playersList, GameModeManager.Ui.Panels.LeaderboardMenu, color, playerStats.Deaths.ToString(),
                        GameModeManager.Ui.GetVerticalMin(747, 422, i, 45, 984, 524), GameModeManager.Ui.GetVerticalMax(127, 62, i, 45, 984, 524), 19);
                    GameModeManager.Ui.Panel(ref playersList, GameModeManager.Ui.Panels.LeaderboardMenu, color, $"{playerStats.DamagesDone:N0}",
                        GameModeManager.Ui.GetVerticalMin(862, 422, i, 45, 984, 524), GameModeManager.Ui.GetVerticalMax(12, 62, i, 45, 984, 524), 19);
                }
            }
            mainContainer.AddRange(leaderboardSelection);
            mainContainer.AddRange(playersList);
            CuiHelper.AddUi(player, mainContainer);
        }
        public void ShowTeamDeathmatchSwitchTeamMenu(BasePlayer player, TeamDeathmatchArenaData arena)
        {
            var textTagHelper = new GameModeManager.TextTagHelper();
            GameModeManager.Ui.ClearAllMenus(player);
            var mainContainer = GameModeManager.Ui.Container(GameModeManager.Ui.Panels.SwitchTeamMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Black, 0f), GameModeManager.Ui.GetMin(0, 0), GameModeManager.Ui.GetMax(0, 0), false);

            ShowTeamDeathmatchMenuHeader(ref mainContainer, GameModeManager.Ui.Panels.SwitchTeamMenu, GameModeManager.Ui.MenuTab.SwitchTeam, player.userID, arena.Id);

            GameModeManager.Ui.Panel(ref mainContainer, GameModeManager.Ui.Panels.SwitchTeamMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray, 0.9f),
                GameModeManager.Ui.GetMin(760, 660), GameModeManager.Ui.GetMax(760, 370));

            GameModeManager.Ui.Label(ref mainContainer, GameModeManager.Ui.Panels.SwitchTeamMenu, "SWITCH TEAM", 28,
                GameModeManager.Ui.GetMin(760, 660), GameModeManager.Ui.GetMax(760, 370), false);

            GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.SwitchTeamMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue, 0.9f),
                 textTagHelper.RemoveColorTag(arena.BlueTeamDisplayName)
                 .RemoveSizeTag()
                 .Result
                 , 28,
                 GameModeManager.Ui.GetMin(760, 601), GameModeManager.Ui.GetMax(1030, 429),
                 $"{ConsoleCommands.SwitchTeam} {Team.Blue}", false);

            GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.SwitchTeamMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue, 0.9f),
                  "SPEC", 28,
                  GameModeManager.Ui.GetMin(895, 601), GameModeManager.Ui.GetMax(895, 429),
                  $"{ConsoleCommands.SwitchTeam} {Team.Spectator}", false);

            GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.SwitchTeamMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue, 0.9f),
                   textTagHelper.RemoveColorTag(arena.RedTeamDisplayName)
                   .RemoveSizeTag()
                   .Result
                   , 28,
                   GameModeManager.Ui.GetMin(1030, 601), GameModeManager.Ui.GetMax(760, 429),
                   $"{ConsoleCommands.SwitchTeam} {Team.Red}", false);

            CuiHelper.AddUi(player, mainContainer);
        }
        public void ShowTeamDeathmatchLoadoutMenu(BasePlayer player, TeamDeathmatchPlayer matchPlayer)
        {
            var infoImage = _arena.ImageManager.GetImage(nameof(GameModeManager.Configuration.Info));
            var infoImageUrl = false;
            if (string.IsNullOrWhiteSpace(infoImage))
            {
                ServerMgr.Instance.StartCoroutine(_arena.ImageManager.DownloadImage(nameof(GameModeManager.Configuration.Info), _arena.PluginConfig.Info));
                infoImage = _arena.PluginConfig.Info;
                infoImageUrl = true;
            }

            GameModeManager.Ui.ClearAllMenus(player);
            var mainContainer = GameModeManager.Ui.Container(GameModeManager.Ui.Panels.LoadoutMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Black, 0f), GameModeManager.Ui.GetMin(0, 0), GameModeManager.Ui.GetMax(0, 0), false);

            ShowTeamDeathmatchMenuHeader(ref mainContainer, GameModeManager.Ui.Panels.LoadoutMenu, GameModeManager.Ui.MenuTab.Loadout, player.userID, matchPlayer.Match.Arena.Data.Id);

            GameModeManager.Ui.Panel(ref mainContainer, GameModeManager.Ui.Panels.LoadoutMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray), "KITS",
                GameModeManager.Ui.GetMin(702, 659), GameModeManager.Ui.GetMax(702, 389), 19);

            GameModeManager.Ui.Panel(ref mainContainer, GameModeManager.Ui.Panels.LoadoutMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray), "ATTACHMENTS",
                GameModeManager.Ui.GetMin(702, 256), GameModeManager.Ui.GetMax(702, 792), 19);

            GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.LoadoutMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue, 0.9f), "BACK TO GAME",
                19, GameModeManager.Ui.GetMin(840, 105), GameModeManager.Ui.GetMax(840, 943), $"{ConsoleCommands.MenuClear}", false);

            for (var i = 0; i < matchPlayer.Match.Arena.Data.LoadoutNames.Count; i++)
            {
                var kit = matchPlayer.Match.Arena.Data.LoadoutNames.ElementAtOrDefault(i);
                if (!string.IsNullOrWhiteSpace(kit))
                {
                    if (matchPlayer.Team == Team.Blue)
                    {
                        GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.LoadoutMenu,
                            matchPlayer.BlueTeamSelectedLoadoutName.Equals(kit, StringComparison.OrdinalIgnoreCase) ?
                                GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.DarkBlue) :
                                GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), kit, 18,
                            GameModeManager.Ui.GetGridMin(702, 619, i, 130, 36, 4, 0),
                            GameModeManager.Ui.GetGridMax(1092, 429, i, 130, 36, 4, 0),
                            matchPlayer.Match.AllowIndividualKit && !matchPlayer.Match.IsStarted ? $"{ConsoleCommands.TakeKit} {kit}" : null, false);
                    }
                    else if (matchPlayer.Team == Team.Red)
                    {
                        GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.LoadoutMenu,
                            matchPlayer.RedTeamSelectedLoadoutName.Equals(kit, StringComparison.OrdinalIgnoreCase) ?
                                GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.DarkBlue) :
                                GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack), kit, 18,
                            GameModeManager.Ui.GetGridMin(702, 619, i, 130, 36, 4, 0),
                            GameModeManager.Ui.GetGridMax(1092, 429, i, 130, 36, 4, 0),
                            matchPlayer.Match.AllowIndividualKit && !matchPlayer.Match.IsStarted ? $"{ConsoleCommands.TakeKit} {kit}" : null, false);
                    }
                }
            }

            for (var i = 0; i < matchPlayer.Match.Arena.Data.ItemsNames.Count; i++)
            {
                var item = matchPlayer.Match.Arena.Data.ItemsNames.ElementAtOrDefault(i);
                if (item != null)
                {
                    GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.LoadoutMenu,
                            matchPlayer.IndividualItems.Contains(item.ItemShortName) ?
                                GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.DarkBlue) :
                                GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.ButtonBlack),
                            item.ItemDisplayName, 18,
                            GameModeManager.Ui.GetGridMin(702, 216, i, 130, 36, 4, 0),
                            GameModeManager.Ui.GetGridMax(1092, 832, i, 130, 36, 4, 0),
                            matchPlayer.Match.AllowIndividualKit && !matchPlayer.Match.IsStarted ? $"{ConsoleCommands.TakeItem} {item.ItemShortName}" : null, false);
                }
            }
            if (!matchPlayer.Match.AllowIndividualKit)
            {
                GameModeManager.Ui.Panel(ref mainContainer,
                    GameModeManager.Ui.Panels.LoadoutMenu,
                    GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.QueenBlue),
                    "Individual kits are disabled. Ask the arena leader to enable them".ToUpper(),
                    GameModeManager.Ui.GetMin(648, 736),
                    GameModeManager.Ui.GetMax(648, 299),
                    18,
                    GameModeManager.Ui.GetMin(700, 736),
                    GameModeManager.Ui.GetMax(662, 299),
                    textColor: GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.MayaBlue));
                GameModeManager.Ui.Image(ref mainContainer,
                    GameModeManager.Ui.Panels.LoadoutMenu,
                    infoImage,
                    GameModeManager.Ui.GetMin(662, 744),
                    GameModeManager.Ui.GetMax(1229, 307),
                    infoImageUrl);
            }
            CuiHelper.AddUi(player, mainContainer);
        }
        public void ShowTeamDeathmatchPauseMenu(BasePlayer player, int arenaId)
        {
            GameModeManager.Ui.ClearAllMenus(player);
            var mainContainer = GameModeManager.Ui.Container(GameModeManager.Ui.Panels.PauseMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Black, 0f), GameModeManager.Ui.GetMin(0, 0), GameModeManager.Ui.GetMax(0, 0), false);

            ShowTeamDeathmatchMenuHeader(ref mainContainer, GameModeManager.Ui.Panels.PauseMenu, GameModeManager.Ui.MenuTab.PauseMenu, player.userID, arenaId);

            GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.PauseMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray, 0.9f),
                "RESUME", 28,
                GameModeManager.Ui.GetMin(760, 632), GameModeManager.Ui.GetMax(760, 398),
                $"{ConsoleCommands.MenuClear}", false);

            //GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.PauseMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue, 0.9f),
            //    "LOADOUT", 28,
            //    GameModeManager.Ui.GetMin(760, 602), GameModeManager.Ui.GetMax(760, 428),
            //    $"{ConsoleCommands.LoadoutShow} {arenaId}", false);

            GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.PauseMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue, 0.9f),
                 "SWITCH TEAM", 28,
                 GameModeManager.Ui.GetMin(760, 573), GameModeManager.Ui.GetMax(760, 457),
                 $"{ConsoleCommands.SwitchTeamShow} {arenaId}", false);

            GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.PauseMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue, 0.9f),
                  "LEADERBOARD", 28,
                  GameModeManager.Ui.GetMin(760, 515), GameModeManager.Ui.GetMax(760, 515),
                  $"{ConsoleCommands.LeaderboardShow} {arenaId} {true}", false);

            GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.PauseMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue, 0.9f),
                   "CONTROL", 28,
                   GameModeManager.Ui.GetMin(760, 457), GameModeManager.Ui.GetMax(760, 573),
                   $"{ConsoleCommands.ControlShow} {arenaId} {GameModeManager.Ui.ControlMenuTab.General}", false);

            GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.PauseMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Red, 0.9f),
                    "QUIT GAME", 28,
                    GameModeManager.Ui.GetMin(760, 398), GameModeManager.Ui.GetMax(760, 632),
                    $"{ConsoleCommands.ArenaLeave}", false);

            CuiHelper.AddUi(player, mainContainer);
        }
        private void ShowTeamDeathmatchMenuHeader(ref CuiElementContainer container, GameModeManager.Ui.Panels panel, GameModeManager.Ui.MenuTab tab, ulong playerId, int arenaId)
        {
            GameModeManager.Ui.Button(ref container, panel, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Black, 0.01f),
                "PAUSE MENU", 20,
                GameModeManager.Ui.GetMin(687, 981), GameModeManager.Ui.GetMax(1126, 76),
                $"{ConsoleCommands.MenuShow} {arenaId}", false, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.White, tab == GameModeManager.Ui.MenuTab.PauseMenu ? 1 : 0.6f));

            /*
             GameModeManager.Ui.Button(ref container, panel, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Black, 0.01f),
                "LOADOUT", 20,
                GameModeManager.Ui.GetMin(774, 981), GameModeManager.Ui.GetMax(1067, 76),
                $"{ConsoleCommands.LoadoutShow} {arenaId}", false, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.White, tab == GameModeManager.Ui.MenuTab.Loadout ? 1 : 0.6f));
            */
            GameModeManager.Ui.Button(ref container, panel, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Black, 0.01f),
                "SWITCH TEAM", 20,
                GameModeManager.Ui.GetMin(834, 981), GameModeManager.Ui.GetMax(968, 76),
                $"{ConsoleCommands.SwitchTeamShow} {arenaId}", false, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.White, tab == GameModeManager.Ui.MenuTab.SwitchTeam ? 1 : 0.6f));

            GameModeManager.Ui.Button(ref container, panel, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Black, 0.01f),
                "LEADERBOARD", 20,
                GameModeManager.Ui.GetMin(992, 981), GameModeManager.Ui.GetMax(807, 76),
                $"{ConsoleCommands.LeaderboardShow} {arenaId} {true}", false, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.White, tab == GameModeManager.Ui.MenuTab.Leaderboard ? 1 : 0.6f));

            GameModeManager.Ui.Button(ref container, panel, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Black, 0.01f),
                "CONTROL", 20,
                GameModeManager.Ui.GetMin(1153, 981), GameModeManager.Ui.GetMax(687, 76),
                $"{ConsoleCommands.ControlShow} {arenaId} {GameModeManager.Ui.ControlMenuTab.General}", false, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.White, tab == GameModeManager.Ui.MenuTab.Control ? 1 : 0.6f));
        }
        public void ShowTeamDeathmatchQuickMenu(BasePlayer player, TeamDeathmatchMatch match)
        {
            CuiHelper.DestroyUi(player, GameModeManager.Ui.Panels.QuickMenu.ToString());
            var mainContainer = GameModeManager.Ui.Container(GameModeManager.Ui.Panels.QuickMenu,
                GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Black, 0f), GameModeManager.Ui.GetMin(0, 0),
                GameModeManager.Ui.GetMax(0, 0), false, false, GameModeManager.Ui.Panels.Hud);

            GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.QuickMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue, 0.9f),
                "MENU", 19, GameModeManager.Ui.GetMin(1385, 22), GameModeManager.Ui.GetMax(406, 1026),
                $"{ConsoleCommands.MenuShow} {match.Arena.Data.Id}", false);

            var matchPlayer = match.Players.FirstOrDefault(x => x.PlayerId == player.userID);
            if (matchPlayer != null)
            {
                if (!match.IsStarted)
                {
                    if (!matchPlayer.IsReady && matchPlayer.Team != Team.Spectator)
                    {
                        GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.QuickMenu,
                            GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Green, 0.9f),
                            "READY", 19, GameModeManager.Ui.GetMin(572, 62), GameModeManager.Ui.GetMax(1219, 986),
                            $"{ConsoleCommands.MatchReadyToggle}", false);
                    }

                    //GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.QuickMenu,
                    //    GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue, 0.9f),
                    //    "LOADOUT", 19, GameModeManager.Ui.GetMin(1385, 58), GameModeManager.Ui.GetMax(406, 990),
                    //    $"{ConsoleCommands.LoadoutShow} {match.Arena.Data.Id}", false);

                }

                var textTagHelper = new GameModeManager.TextTagHelper();

                GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.QuickMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue, 0.9f),
                    textTagHelper.RemoveColorTag(match.Arena.Data.BlueTeamDisplayName).RemoveSizeTag().Result, 19,
                    GameModeManager.Ui.GetMin(1518, 94), GameModeManager.Ui.GetMax(273, 954),
                    $"{ConsoleCommands.SwitchTeam} {Team.Blue}", false);

                GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.QuickMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue, 0.9f),
                    "SPECTATE", 19,
                    GameModeManager.Ui.GetMin(1518, 58), GameModeManager.Ui.GetMax(273, 990),
                    $"{ConsoleCommands.SwitchTeam} {Team.Spectator}", false);

                GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.QuickMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue, 0.9f),
                    textTagHelper.RemoveColorTag(match.Arena.Data.RedTeamDisplayName).RemoveSizeTag().Result, 19,
                    GameModeManager.Ui.GetMin(1518, 22), GameModeManager.Ui.GetMax(273, 1026),
                    $"{ConsoleCommands.SwitchTeam} {Team.Red}", false);

                if (matchPlayer.IsMatchLeader)
                {
                    GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.QuickMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue, 0.9f),
                        "CONTROL", 19, GameModeManager.Ui.GetMin(1385, 58), GameModeManager.Ui.GetMax(406, 990),
                        $"{ConsoleCommands.ControlShow} {match.Arena.Data.Id} {GameModeManager.Ui.ControlMenuTab.General}", false);
                }
            }

            GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.QuickMenu, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Red, 0.9f),
               "LEAVE", 19, GameModeManager.Ui.GetMin(572, 22), GameModeManager.Ui.GetMax(1219, 1026),
               $"{ConsoleCommands.ArenaLeave}", false);

            CuiHelper.AddUi(player, mainContainer);
        }
        public void ShowTeamDeathmatchInGameGui(BasePlayer player, string timerText, string roundText)
        {
            CuiHelper.DestroyUi(player, GameModeManager.Ui.Panels.InGameInfo.ToString());
            var mainContainer = GameModeManager.Ui.Container(GameModeManager.Ui.Panels.InGameInfo,
                GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Black, 0f),
                GameModeManager.Ui.GetMin(0, 0),
                GameModeManager.Ui.GetMax(0, 0),
                false,
                false,
                GameModeManager.Ui.Panels.Under);

            GameModeManager.Ui.OutlineLabel(ref mainContainer, GameModeManager.Ui.Panels.InGameInfo, timerText, 50,
                GameModeManager.Ui.GetMin(900, 979), GameModeManager.Ui.GetMax(900, 42), GameModeManager.Ui.Fonts.RobotoCondensedBold);

            GameModeManager.Ui.OutlineLabel(ref mainContainer, GameModeManager.Ui.Panels.InGameInfo, roundText, 20,
                GameModeManager.Ui.GetMin(940, 957), GameModeManager.Ui.GetMax(940, 100));

            CuiHelper.AddUi(player, mainContainer);
        }
        public void ShowTeamDeathmatchCountDownGui(BasePlayer player, string countdownText, bool useFadeIn)
        {
            CuiHelper.DestroyUi(player, GameModeManager.Ui.Panels.InGameInfo.ToString());
            var mainContainer = GameModeManager.Ui.Container(GameModeManager.Ui.Panels.InGameInfo,
                GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Black, 0f),
                GameModeManager.Ui.GetMin(0, 0), GameModeManager.Ui.GetMax(0, 0), false, false, GameModeManager.Ui.Panels.Under);

            GameModeManager.Ui.OutlineLabel(ref mainContainer, GameModeManager.Ui.Panels.InGameInfo, countdownText, 200,
                GameModeManager.Ui.GetMin(800, 400), GameModeManager.Ui.GetMax(800, 400), GameModeManager.Ui.Fonts.RobotoCondensedBold, fadeIn: useFadeIn ? 1 : 0);
            if (!useFadeIn && !string.IsNullOrWhiteSpace(_config.CountdownSoundEffect))
            {
                var effect = new Effect(_config.CountdownSoundEffect, player, 0, Vector3.zero, Vector3.forward);
                EffectNetwork.Send(effect, player.net.connection);
            }
            CuiHelper.AddUi(player, mainContainer);
        }
        public void ShowTeamDeathmatchPlayers(BasePlayer player, TeamDeathmatchMatch match)
        {
            var textTagHelper = new GameModeManager.TextTagHelper();
            var mainContainer = GameModeManager.Ui.Container(GameModeManager.Ui.Panels.MatchPlayers, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Black, 0f), GameModeManager.Ui.GetMin(0, 0), GameModeManager.Ui.GetMax(1, 1), false, false, GameModeManager.Ui.Panels.Under);

            var leaderIcon = _arena.ImageManager.GetImage(nameof(GameModeManager.Configuration.MatchLeader));
            var leaderIconIsUrl = false;
            if (string.IsNullOrWhiteSpace(leaderIcon))
            {
                ServerMgr.Instance.StartCoroutine(_arena.ImageManager.DownloadImage(nameof(GameModeManager.Configuration.MatchLeader), _arena.PluginConfig.MatchLeader));
                leaderIcon = _arena.PluginConfig.MatchLeader;
                leaderIconIsUrl = true;
            }

            #region Blue Team Headers
            //Blue Team header
            GameModeManager.Ui.OutlineLabel(ref mainContainer,
                GameModeManager.Ui.Panels.MatchPlayers, textTagHelper.RemoveSizeTag(match.Arena.Data.BlueTeamDisplayName).Result, 18,
                GameModeManager.Ui.GetMin(33, 1032),
                GameModeManager.Ui.GetMax(1706, 27),
                 align: TextAnchor.MiddleLeft, outlineDistance: "0.5 0.5");
            //Blue Team player status header
            GameModeManager.Ui.OutlineLabel(ref mainContainer, GameModeManager.Ui.Panels.MatchPlayers, "Status", 18,
                GameModeManager.Ui.GetMin(214, 1032),
                GameModeManager.Ui.GetMax(1630, 27));
            //Blue Team player kills header
            GameModeManager.Ui.OutlineLabel(ref mainContainer, GameModeManager.Ui.Panels.MatchPlayers, "K", 18,
                GameModeManager.Ui.GetMin(293, 1032),
                GameModeManager.Ui.GetMax(1594, 27));
            //Blue Team player deaths header
            GameModeManager.Ui.OutlineLabel(ref mainContainer, GameModeManager.Ui.Panels.MatchPlayers, "D", 18,
                GameModeManager.Ui.GetMin(328, 1032),
                GameModeManager.Ui.GetMax(1558, 27));
            //Blue Team player damage header
            GameModeManager.Ui.OutlineLabel(ref mainContainer, GameModeManager.Ui.Panels.MatchPlayers, "DMG", 18,
                GameModeManager.Ui.GetMin(364, 1032),
                GameModeManager.Ui.GetMax(1506, 27));
            #endregion Blue Team Headers
            #region Red Team Headers
            //Blue Team header
            GameModeManager.Ui.OutlineLabel(ref mainContainer,
                GameModeManager.Ui.Panels.MatchPlayers, textTagHelper.RemoveSizeTag(match.Arena.Data.RedTeamDisplayName).Result, 18,
                GameModeManager.Ui.GetMin(1706, 1032),
                GameModeManager.Ui.GetMax(33, 27),
                 align: TextAnchor.MiddleRight, outlineDistance: "0.5 0.5");
            //Blue Team player status header
            GameModeManager.Ui.OutlineLabel(ref mainContainer, GameModeManager.Ui.Panels.MatchPlayers, "Status", 18,
                GameModeManager.Ui.GetMin(1630, 1032),
                GameModeManager.Ui.GetMax(214, 27));
            //Blue Team player kills header
            GameModeManager.Ui.OutlineLabel(ref mainContainer, GameModeManager.Ui.Panels.MatchPlayers, "K", 18,
                GameModeManager.Ui.GetMin(1594, 1032),
                GameModeManager.Ui.GetMax(293, 27));
            //Blue Team player deaths header
            GameModeManager.Ui.OutlineLabel(ref mainContainer, GameModeManager.Ui.Panels.MatchPlayers, "D", 18,
                GameModeManager.Ui.GetMin(1558, 1032),
                GameModeManager.Ui.GetMax(328, 27));
            //Blue Team player damage header
            GameModeManager.Ui.OutlineLabel(ref mainContainer, GameModeManager.Ui.Panels.MatchPlayers, "DMG", 18,
                GameModeManager.Ui.GetMin(1506, 1032),
                GameModeManager.Ui.GetMax(364, 27));

            #endregion
            #region Blue Team Players
            var blueTeamPlayers = match.Players.Where(p => p.Team == Team.Blue).ToList();

            for (int i = 0; i < blueTeamPlayers.Count; i++)
            {
                var currentPlayer = blueTeamPlayers[i];
                var status = !match.IsStarted
                    ? currentPlayer.IsReady ? "Ready" : "Not Ready"
                    : currentPlayer.AllowPlaying ? "Alive" : "Dead";

                var playerColor = GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.White);

                if (currentPlayer.IsMatchLeader)
                {
                    GameModeManager.Ui.Image(ref mainContainer, GameModeManager.Ui.Panels.MatchPlayers, leaderIcon,
                        GameModeManager.Ui.GetVerticalMin(9, 990, i, 33),
                        GameModeManager.Ui.GetVerticalMax(1891, 67, i, 33), leaderIconIsUrl);
                    playerColor = GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Xanthic);
                }
                GameModeManager.Ui.OutlineLabel(ref mainContainer, GameModeManager.Ui.Panels.MatchPlayers, currentPlayer.PlayerName, 18,
                    GameModeManager.Ui.GetVerticalMin(33, 990, i, 33),
                    GameModeManager.Ui.GetVerticalMax(1706, 67, i, 33), color: playerColor, align: TextAnchor.MiddleLeft);

                GameModeManager.Ui.OutlineLabel(ref mainContainer, GameModeManager.Ui.Panels.MatchPlayers, status, 18,
                    GameModeManager.Ui.GetVerticalMin(214, 990, i, 33),
                    GameModeManager.Ui.GetVerticalMax(1630, 67, i, 33));

                GameModeManager.Ui.OutlineLabel(ref mainContainer, GameModeManager.Ui.Panels.MatchPlayers, currentPlayer.Statistics.Kills.ToString(), 18,
                    GameModeManager.Ui.GetVerticalMin(293, 990, i, 33),
                    GameModeManager.Ui.GetVerticalMax(1594, 67, i, 33));

                GameModeManager.Ui.OutlineLabel(ref mainContainer, GameModeManager.Ui.Panels.MatchPlayers, currentPlayer.Statistics.Deaths.ToString(), 18,
                    GameModeManager.Ui.GetVerticalMin(328, 990, i, 33),
                    GameModeManager.Ui.GetVerticalMax(1558, 67, i, 33));

                GameModeManager.Ui.OutlineLabel(ref mainContainer, GameModeManager.Ui.Panels.MatchPlayers, $"{currentPlayer.Statistics.DamagesDone:N0}", 18,
                    GameModeManager.Ui.GetVerticalMin(364, 990, i, 33),
                    GameModeManager.Ui.GetVerticalMax(1506, 67, i, 33));
            }
            #endregion Blue Team Players
            #region Red Team Players
            var redTeamPlayers = match.Players.Where(p => p.Team == Team.Red).ToList();
            for (int i = 0; i < redTeamPlayers.Count; i++)
            {
                var currentPlayer = redTeamPlayers[i];
                var status = !match.IsStarted
                    ? currentPlayer.IsReady ? "Ready" : "Not Ready"
                    : currentPlayer.AllowPlaying ? "Alive" : "Dead";

                var playerColor = GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.White);

                if (currentPlayer.IsMatchLeader)
                {
                    GameModeManager.Ui.Image(ref mainContainer, GameModeManager.Ui.Panels.MatchPlayers, leaderIcon,
                        GameModeManager.Ui.GetVerticalMin(1891, 990, i, 33),
                        GameModeManager.Ui.GetVerticalMax(9, 67, i, 33), leaderIconIsUrl);
                    playerColor = GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Xanthic);
                }

                GameModeManager.Ui.OutlineLabel(ref mainContainer, GameModeManager.Ui.Panels.MatchPlayers, currentPlayer.PlayerName, 18,
                    GameModeManager.Ui.GetVerticalMin(1706, 990, i, 33),
                    GameModeManager.Ui.GetVerticalMax(33, 67, i, 33), color: playerColor, align: TextAnchor.MiddleRight);

                GameModeManager.Ui.OutlineLabel(ref mainContainer, GameModeManager.Ui.Panels.MatchPlayers,
                    status, 18,
                    GameModeManager.Ui.GetVerticalMin(1630, 990, i, 33),
                    GameModeManager.Ui.GetVerticalMax(214, 67, i, 33));

                GameModeManager.Ui.OutlineLabel(ref mainContainer, GameModeManager.Ui.Panels.MatchPlayers,
                    currentPlayer.Statistics.Kills.ToString(), 18,
                    GameModeManager.Ui.GetVerticalMin(1594, 990, i, 33),
                    GameModeManager.Ui.GetVerticalMax(293, 67, i, 33));

                GameModeManager.Ui.OutlineLabel(ref mainContainer, GameModeManager.Ui.Panels.MatchPlayers,
                    currentPlayer.Statistics.Deaths.ToString(), 18,
                    GameModeManager.Ui.GetVerticalMin(1558, 990, i, 33),
                    GameModeManager.Ui.GetVerticalMax(328, 67, i, 33));

                GameModeManager.Ui.OutlineLabel(ref mainContainer, GameModeManager.Ui.Panels.MatchPlayers,
                    $"{currentPlayer.Statistics.DamagesDone:N0}",
                    18,
                    GameModeManager.Ui.GetVerticalMin(1506, 990, i, 33),
                    GameModeManager.Ui.GetVerticalMax(364, 67, i, 33));
            }
            #endregion Red Team Players
            CuiHelper.DestroyUi(player, GameModeManager.Ui.Panels.MatchPlayers.ToString());
            CuiHelper.AddUi(player, mainContainer);
        }
        public void ShowTeamDeathmatchJoinMenu(BasePlayer player, TeamDeathmatchArenaData arena)
        {
            var textTagHelper = new GameModeManager.TextTagHelper();
            GameModeManager.Ui.ClearAllMenus(player);
            var mainContainer = GameModeManager.Ui.Container(GameModeManager.Ui.Panels.TeamDeathmatchJoin, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Black, 0f), GameModeManager.Ui.GetMin(0, 0), GameModeManager.Ui.GetMax(0, 0), false);

            GameModeManager.Ui.Panel(ref mainContainer, GameModeManager.Ui.Panels.TeamDeathmatchJoin, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Gray, 0.9f),
                GameModeManager.Ui.GetMin(760, 660), GameModeManager.Ui.GetMax(760, 370));

            GameModeManager.Ui.Label(ref mainContainer, GameModeManager.Ui.Panels.TeamDeathmatchJoin, "TEAM SELECTION", 28,
                GameModeManager.Ui.GetMin(760, 660), GameModeManager.Ui.GetMax(760, 370), false);

            GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.TeamDeathmatchJoin, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue, 0.9f),
                textTagHelper.RemoveColorTag(arena.BlueTeamDisplayName)
                .RemoveSizeTag()
                .Result, 28,
                GameModeManager.Ui.GetMin(760, 601), GameModeManager.Ui.GetMax(1030, 429),
                $"{ConsoleCommands.ArenaJoin} {arena.Id} {Team.Blue}", false);

            GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.TeamDeathmatchJoin, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue, 0.9f),
                "SPEC", 28,
                GameModeManager.Ui.GetMin(895, 601), GameModeManager.Ui.GetMax(895, 429),
                $"{ConsoleCommands.ArenaJoin} {arena.Id} {Team.Spectator}", false);

            GameModeManager.Ui.Button(ref mainContainer, GameModeManager.Ui.Panels.TeamDeathmatchJoin, GameModeManager.Ui.Color(GameModeManager.Ui.ColorCode.Blue, 0.9f),
                textTagHelper.RemoveColorTag(arena.RedTeamDisplayName)
                .RemoveSizeTag()
                .Result, 28,
                GameModeManager.Ui.GetMin(1030, 601), GameModeManager.Ui.GetMax(760, 429),
                $"{ConsoleCommands.ArenaJoin} {arena.Id} {Team.Red}", false);
            CuiHelper.AddUi(player, mainContainer);
        }

        #endregion GUI

        #region RustHooks
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Messages.NoPermission] = "You don't have permission to use this command",
                [Messages.WrongCommand] = "You entered the command in a wrong way, available commands are: {0}",
                [Messages.ArenaCommandsList] = "Available arena commands are: {0}",
                [Messages.ScrimCommandsList] = "<color=#eb9534>Scrim</color> registered use <color=#5af>{0}</color> to get commands",
                [Messages.InvalidPlayerId] = "Invalid player steamID",
                [Messages.PlayerNotFound] = "No player found",
                [Messages.LobbyNotFound] = "No lobby found",
                [Messages.ArenaNotFound] = "No arena found",
                [Messages.ChangesSaved] = "Changes has been saved",
                [Messages.NoEditingLobby] = "There is no lobby to edit, use the {0} to start editing a lobby",
                [Messages.NoEditingArena] = "There is no arena to edit, use the {0} to start editing an arena",
                [Messages.ArenaParameterChanged] = "The arena {0} has been changed",
                [Messages.ArenaSpawnPointAdded] = "New spawn point {0} has been added to the arena",
                [Messages.ArenaInvalidPosition] = "The target position is an invalid position, it should be inside the arena",
                [Messages.ArenaInvalidEntrancePosition] = "The target entrance position is an invalid position, it should be inside the lobby",
                [Messages.ArenaDeleted] = "The arena has been deleted",
                [Messages.ArenaCreated] = "The arena has been created",
                [Messages.ArenaEditingStarted] = "Arena {0} ({1}) editing has been started",
                [Messages.ArenaEditingDone] = "Arena {0} ({1}) editing has been stopped",
                [Messages.ArenaNoVipAccess] = "You don't have access to this VIP arena",
                [Messages.ArenaRestrictAccess] = "This match is locked, ask the match leader to whitelist you",
                [Messages.LobbyZoneNotFound] = "Lobby Zone not found",
                [Messages.ArenaZoneNotFound] = "Arena Zone not found",
                [Messages.InvalidKitName] = "Invalid kit name",
                [Messages.KitNotFound] = "Kit not found",
                [Messages.KitAdded] = "Kit has been added",
                [Messages.KitRemoved] = "Kit has been removed",
                [Messages.SpawnPointNotFound] = "Spawn Point not found",
                [Messages.TeamNotFound] = "Team not found",
                [Messages.NotEnoughPlayers] = "Not Enough Players In The Teams",
                [Messages.EndRoundMessage] = "Team {0} won the round",
                [Messages.EndMatchMessage] = "Team {0} won the match",
                [Messages.EndRoundDrawMessage] = "Round ended with no winner",
                [Messages.EndMatchDrawMessage] = "Match ended with no winner",
                [Messages.ArenaJoinMessage] = "{0} has joined the {1} team",
                [Messages.ArenaSpectatorJoinMessage] = "{0} has joined the match as a spectator",
                [Messages.ArenaLeaveMessage] = "{0} left the match",
                [Messages.PlayerIsReady] = "{0} is ready",
                [Messages.InvalidItemShortName] = "No item found with this short name {0}",
                [Messages.ItemAdded] = "Item has been added",
                [Messages.ItemRemoved] = "Item has been removed",
                [Messages.ItemNotFound] = "Item not found",
                [Messages.ArenaSetValueHelp] = "Available keys for editing the arena: {0}",
                [Messages.ArenaSpawnPointTypesHelp] = "Available types for arena spawn points: {0}",
                [Messages.SingleSelectionNeeded] = "You need to select only one player to transfer leadership",
                [Messages.InvalidSpawnType] = "You entered an invalid spawn type, available types are: {0}",
                [Messages.MatchLeadershipChange] = "{0} is now the match leader",
                [Messages.KilledBy] = "{0} got killed by {1}",
                [Messages.NoSwitchTeamOnRunningMatch] = "You can't switch your team while the match is running",
                [Messages.MatchIsFull] = "The match is full",
                [Messages.TeamKitSet] = "Team {0} got the {1} kit",
            }, this);
        }
        protected override void SaveConfig() => Config.WriteObject(_config, true);
        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();
            Config.Settings.Converters = new JsonConverter[] { new StringEnumConverter() };
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            Config.Settings.Converters = new JsonConverter[] { new StringEnumConverter() };
            _config = GetDefaultConfig();
        }
        void Init()
        {
            Config.Settings.Converters = new JsonConverter[] { new StringEnumConverter() };
        }
        private void Loaded()
        {
            _dataManager = Interface.Oxide.DataFileSystem.GetFile($"{GameModeManager.MainFolderName}/{nameof(Scrim)}");
            _dataManager.Settings.Converters = new JsonConverter[] { new StringEnumConverter() };
        }
        private void Unload()
        {
            _arena = null;
            _instance = null;

            foreach (var spawnZoneBehaviour in UnityEngine.Object.FindObjectsOfType<SpawnZoneBehaviour>())
            {
                if (spawnZoneBehaviour != null)
                    spawnZoneBehaviour.DoDestroy();
            }
        }
        object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null)
                return null;

            TeamDeathmatchPlayer victim;
            if (Players.TryGetValue(player.userID, out victim) && victim != null)
            {
                GameModeManager.Ui.ClearAllMenus(player);
                if (info == null)
                {
                    ShowTeamDeathmatchQuickMenu(player, victim.Match);
                    return false;
                }
                if (victim.Match.IsStarted)
                {
                    TeamDeathmatchPlayer attacker = null;
                    var attackerPlayer = info.InitiatorPlayer;
                    if (attackerPlayer != null)
                    {
                        Players.TryGetValue(attackerPlayer.userID, out attacker);
                    }
                    victim.AllowPlaying = false;
                    victim.Statistics.Deaths++;
                    if (attacker != null && victim.PlayerId != attacker.PlayerId)
                    {
                        attacker.Statistics.Kills++;
                        foreach (var deathmatchPlayer in attacker.Match.Players)
                        {
                            _arena.ShowMessage(deathmatchPlayer.Player, Messages.KilledBy, this, victim.PlayerName, attacker.PlayerName);
                        }
                    }
                }
                victim.ResetMetabolism();
                victim.ManageInventory(true);
                var spawnPoint = GetMatchSpawnPoint(victim);
                if (!spawnPoint.HasValue)
                {
                    ShowTeamDeathmatchQuickMenu(player, victim.Match);
                    return null;
                }
                _arena.Teleport(player, spawnPoint.Value, false, false);
                victim.Match.CheckIfRoundShouldEnd();
                ShowTeamDeathmatchQuickMenu(player, victim.Match);
                return false;
            }
            return null;
        }
        bool CanDropActiveItem(BasePlayer player)
        {
            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null)
            {
                return false;
            }
            return true;
        }
        object OnConstructionPlace(BaseEntity entity, Construction component, Construction.Target constructionTarget, BasePlayer player)
        {
            TeamDeathmatchPlayer matchplayer;
            if (Players.TryGetValue(player.userID, out matchplayer) && matchplayer != null && !matchplayer.Match.IsStarted)
            {
                Construction.lastPlacementError = "Match needed to be started";
                return false;
            }
            return null;
        }
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null)
                return null;

            var player = entity as BasePlayer;
            if (player != null)
            {
                TeamDeathmatchPlayer target;
                Players.TryGetValue(player.userID, out target);

                if (target != null && target.Team == Team.Spectator)
                {
                    info.damageTypes.Clear();
                    return false;
                }

                var attackerPlayer = info.InitiatorPlayer;
                if (attackerPlayer != null)
                {
                    TeamDeathmatchPlayer attacker;
                    if (Players.TryGetValue(attackerPlayer.userID, out attacker) && attacker != null)
                    {
                        if (!attacker.Match.IsStarted)
                        {
                            info.damageTypes.Clear();
                            return false;
                        }

                        if (target != null && target.Match.Id == attacker.Match.Id && target.Team == attacker.Team)
                        {
                            return false;
                        }
                        attacker.Statistics.DamagesDone += info.damageTypes?.Total() ?? 0;
                    }
                }
            }
            else if (entity.OwnerID == 11111111111111111)
            {
                return false;
            }

            return null;
        }
        object OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            if (player == null)
            {
                return null;
            }
            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null && (matchPlayer.Team == Team.Blue || matchPlayer.Team == Team.Red))
            {
                return false;
            }
            return null;
        }
        private void OnServerInitialized()
        {
            _instance = this;
            LoadData();

            var advancedArena = plugins.PluginManager.GetPlugin(nameof(GameModeManager));
            if (advancedArena != null)
            {
                advancedArena.Call("RegisterArena", nameof(Scrim));
            }
        }
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            RemoveFromRustTeam(player);
            RemoveFromArenas(player);
        }
        object CanNetworkTo(BasePlayer player, BasePlayer target)
        {
            if (player.userID == target.userID)
            {
                return null;
            }

            if (permission.UserHasPermission(target.UserIDString, GameModeManager.PermissionVanishBypass))
            {
                return null;
            }
            TeamDeathmatchPlayer playerMatch, targetMatch;
            Players.TryGetValue(player.userID, out playerMatch);
            Players.TryGetValue(target.userID, out targetMatch);
            if (playerMatch?.Match == null && targetMatch?.Match == null)
            {
                return null;
            }

            return playerMatch?.Match?.Id == targetMatch?.Match?.Id;
        }
        object CanNetworkTo(BaseEntity entity, BasePlayer target)
        {
            if (entity.OwnerID == target.userID || entity.OwnerID == 0 || entity.OwnerID == 11111111111111111)
            {
                return null;
            }
            TeamDeathmatchPlayer entityMatch, targetMatch;
            Players.TryGetValue(entity.OwnerID, out entityMatch);
            Players.TryGetValue(target.userID, out targetMatch);
            if (entityMatch?.Match == null && targetMatch?.Match == null)
            {
                return null;
            }

            return entityMatch?.Match?.Id == targetMatch?.Match?.Id;
        }
        #endregion RustHooks

        #region CustomHooks

        [HookMethod(nameof(GameModeManager.IAdvancedLobby.InitializeArena))]
        public void InitializeArena(GameModeManager arena)
        {
            _arena = arena;
            ArenaGenerate();
            if (!_config.ShowPauseMenuCommand.Equals(_arena.PluginConfig.ShowLobbyPauseMenuCommand, StringComparison.OrdinalIgnoreCase))
            {
                cmd.AddChatCommand(_config.ShowPauseMenuCommand, this, ShowPauseMenuChatCommand);
            }
            cmd.AddChatCommand(_config.ToggleReadyCommand, this, ToggleReadyChatCommand);
        }

        [HookMethod(nameof(GameModeManager.IAdvancedLobby.GetArenaIdsInLobby))]
        public List<int> GetArenaIdsInLobby(int lobbyId)
        {
            return _arenas.Values.Where(a => a.Data.LobbyId == lobbyId).Select(a => a.Data.Id).ToList();
        }

        [HookMethod(nameof(GameModeManager.IAdvancedLobby.GetArenasDetailsInLobby))]
        public List<GameModeManager.ArenaDetail> GetArenasDetailsInLobby(int lobbyId)
        {
            return _arenas.Values.Where(a => a.Data.LobbyId == lobbyId).Select(a => new GameModeManager.ArenaDetail
            {
                ArenaId = a.Data.Id,
                Name = a.Data.Name,
                BlueTeam = a.Match.Players.Count(x => x.Team == Team.Blue),
                RedTeam = a.Match.Players.Count(x => x.Team == Team.Red),
                Capacity = a.Data.Capacity,
                MatchType = GameModeManager.MatchType.Scrim,
                IsVip = a.Data.RestrictAccess,
                IsRestricted = a.Match.RestrictAccess
            }).ToList();
        }

        [HookMethod(nameof(GameModeManager.IAdvancedLobby.GetPlayerMatchPlayers))]
        public List<BasePlayer> GetPlayerMatchPlayers(ulong playerId)
        {
            TeamDeathmatchPlayer deathmatchPlayer;
            if (Players.TryGetValue(playerId, out deathmatchPlayer) && deathmatchPlayer != null)
            {
                return deathmatchPlayer.Match.Players.Select(x => x.Player).ToList();
            }
            return null;
        }

        [HookMethod(nameof(GameModeManager.IAdvancedLobby.HandleShowArena))]
        public void HandleShowArena(BasePlayer player, int lobbyId, int visibilityTime = 60)
        {
            if (player == null)
                return;

            foreach (var arena in _arenas.Values.Where(a => a.Data.LobbyId == lobbyId).ToList())
            {
                player.SendConsoleCommand("ddraw.text", visibilityTime, arena.Data.IsEnabled ? _arena.GetColor("#9B51E0") : _arena.GetColor("#333333"), arena.Data.EntranceLocation + new Vector3(0, 1.5f, 0), $"<size=15>{arena.Data.Name}</size>");
                player.SendConsoleCommand("ddraw.sphere", visibilityTime, arena.Data.IsEnabled ? _arena.GetColor("#9B51E0") : _arena.GetColor("#333333"), arena.Data.EntranceLocation + new Vector3(0, 1.5f, 0), arena.Data.EntranceTriggerRadius);
                for (float i = 0f; i < 1; i += 0.1f)
                {
                    var start = Vector3.Lerp(arena.Data.EntranceLocation, arena.Zone.Location, i);
                    var end = Vector3.Lerp(arena.Data.EntranceLocation, arena.Zone.Location, i + 0.1f);
                    player.SendConsoleCommand("ddraw.arrow", visibilityTime, _arena.GetColor("#FFFFFF"), start, end, 0.5f);
                }
                player.SendConsoleCommand("ddraw.text", visibilityTime, arena.Data.IsEnabled ? _arena.GetColor("#F2C94C") : _arena.GetColor("#333333"),
                    arena.Zone.Location + new Vector3(0, 1.5f, 0), $"<size=20>{arena.Data.Name} ({arena.Data.Id})</size>");
                _arena.ShowZone(player, arena.Zone, arena.Data.IsEnabled, false, visibilityTime);

                foreach (var spawnLocation in arena.Data.SpawnLocations)
                {
                    var color = _arena.GetColor("#BDBDBD");
                    switch (spawnLocation.SpawnType)
                    {
                        case Team.Spectator:
                            color = _arena.GetColor("#27AE60");
                            break;
                        case Team.Blue:
                            color = _arena.GetColor("#2F80ED");
                            break;
                        case Team.Red:
                            color = _arena.GetColor("#E12126");
                            break;
                    }
                    player.SendConsoleCommand("ddraw.text", visibilityTime, _arena.GetColor("#F2994A"),
                        spawnLocation.SpawnPoint + new Vector3(0, 1.5f, 0), $"<size=20>{spawnLocation.Name}</size>");
                    player.SendConsoleCommand("ddraw.sphere", visibilityTime, color, spawnLocation.SpawnPoint, spawnLocation.Radius);
                    player.SendConsoleCommand("ddraw.sphere", visibilityTime, color, spawnLocation.SpawnPoint, spawnLocation.SafeAreaRadius);
                    player.SendConsoleCommand("ddraw.sphere", visibilityTime, color, spawnLocation.SpawnPoint, 1);
                }
            }
        }

        [HookMethod(nameof(GameModeManager.IAdvancedLobby.HandleRemoveArena))]
        public void HandleRemoveArena(int lobbyId)
        {
            var arenas = _arenas.Values.Where(x => x.Data.LobbyId == lobbyId);
            foreach (var arena in arenas)
            {
                RemoveArena(arena);
            }
        }

        [HookMethod(nameof(GameModeManager.IAdvancedLobby.HandleArenaLoadoutValidation))]
        public void HandleArenaLoadoutValidation(int lobbyId)
        {
            var arenas = _arenas.Values.Where(x => x.Data.LobbyId == lobbyId);
            foreach (var arena in arenas)
            {
                if (!_arena.IsLoadoutValid(arena.Data.SpectatorLoadoutName))
                {
                    PrintWarning($"Spectator loadout name of the {arena.Data.Name} arena is invalid.");
                }
                foreach (var loadoutName in arena.Data.LoadoutNames)
                {
                    if (!_arena.IsLoadoutValid(loadoutName))
                    {
                        PrintWarning($"{loadoutName} loadout name of the {arena.Data.Name} arena is invalid.");
                    }
                }
            }
        }

        [HookMethod(nameof(GameModeManager.IAdvancedLobby.HandleJoinArena))]
        public object HandleJoinArena(BasePlayer player, int arenaId)
        {
            TeamDeathmatchArena teamDeathmatchArena;
            if (!_arenas.TryGetValue(arenaId, out teamDeathmatchArena) || teamDeathmatchArena == null || teamDeathmatchArena.Data == null)
            {
                return null;
            }
            if (teamDeathmatchArena.Data.MatchType == GameModeManager.MatchType.Scrim)
            {
                ShowTeamDeathmatchJoinMenu(player, teamDeathmatchArena.Data);
                return true;
            }
            return null;
        }

        [HookMethod(nameof(GameModeManager.IAdvancedLobby.HandleLeaveArena))]
        public void HandleLeaveArena(BasePlayer player)
        {
            GameModeManager.Ui.ClearAllMenus(player);
            var arenaToLeave = _arenas.Values.FirstOrDefault(x => x.Match.Players.Any(p => p.PlayerId == player.userID));
            if (arenaToLeave == null)
            {
                _arena.ShowMessage(player, Messages.ArenaNotFound, this);
                return;
            }

            var spawnLocation = arenaToLeave.Lobby.GetRandomSpawnLocation();
            RemoveFromRustTeam(player);
            RemoveFromArenas(player);
            _arena.TeleportToLobby(player, arenaToLeave.Lobby.Data.Id, spawnLocation);
            foreach (var otherPlayer in arenaToLeave.Match.Players)
            {
                _arena.ShowMessage(otherPlayer.Player, Messages.ArenaLeaveMessage, this, player.displayName);
            }
        }

        [HookMethod(nameof(GameModeManager.IAdvancedLobby.HandleOnPlayerRespawn))]
        public object HandleOnPlayerRespawn(BasePlayer player)
        {
            TeamDeathmatchPlayer deathmatchPlayer;
            if (Players.TryGetValue(player.userID, out deathmatchPlayer) && deathmatchPlayer != null)
            {
                deathmatchPlayer.ManageInventory(true);
                return GetMatchSpawnPoint(deathmatchPlayer);
            }

            return null;
        }

        [HookMethod(nameof(GameModeManager.IAdvancedLobby.HandleShowPauseMenu))]
        public object HandleShowPauseMenu(BasePlayer player)
        {
            if (Players.ContainsKey(player.userID))
            {
                ShowPauseMenu(player);
                return true;
            }
            return null;
        }

        [HookMethod(nameof(GameModeManager.IAdvancedLobby.HandleArenaHelp))]
        public void HandleArenaHelp(BasePlayer player)
        {
            _arena.ShowMessage(player, Messages.ScrimCommandsList, this, ConsoleCommands.ArenaHelp);
        }
        #endregion CustomHooks

        private void SaveData()
        {
            _dataManager.WriteObject(_pluginData);
        }
        private void LoadData()
        {
            try
            {
                _pluginData = _dataManager.ReadObject<PluginData>();
            }
            catch (Exception exception)
            {
                PrintError("Data file is corrupt, error message:");
                PrintError(exception.Message);
                if (exception.InnerException != null)
                {
                    PrintError($"Inner exception message: {exception.InnerException.Message}");
                }
                PrintWarning("New data file created");
                _pluginData = new PluginData();
            }
        }
        private void TogglePlayerReady(BasePlayer player)
        {
            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null)
            {
                if (matchPlayer.Team == Team.Spectator)
                    return;
                matchPlayer.ToggleReady();
                ShowTeamDeathmatchQuickMenu(player, matchPlayer.Match);
            }
        }
        private Vector3? GetMatchSpawnPoint(TeamDeathmatchPlayer deathmatchPlayer)
        {
            var arena = deathmatchPlayer.Match.Arena;
            var team = deathmatchPlayer.AllowPlaying ? deathmatchPlayer.Team : Team.Spectator;
            var spawnLocation = arena.GetRandomSpawnLocationForPlayer(deathmatchPlayer.Player, team);

            if (spawnLocation.Equals(Vector3.zero))
            {
                return null;
            }

            return spawnLocation;
        }
        private Configuration GetDefaultConfig()
        {
            return new Configuration();
        }
        void ArenaGenerate()
        {
            if (_arena == null)
            {
                return;
            }
            foreach (var arenaData in _pluginData.Arenas)
            {
                GameModeManager.Lobby lobby;
                if (!_arena.Lobbies.TryGetValue(arenaData.LobbyId, out lobby) || lobby == null)
                {
                    continue;
                }
                var arena = new TeamDeathmatchArena(lobby, arenaData)
                {
                    Data = arenaData,
                    Zone = _arena.GetZone(arenaData.ZoneId),
                    Behaviour = GameModeManager.ArenaBehaviour.CreateEntrance($"Arena_EP_{arenaData.Id}", arenaData.EntranceLocation,
                        arenaData.EntranceTriggerRadius,
                        arenaData.IsEntranceTriggerActive)
                };
                //s.Match = new TeamDeathmatchMatch();
                arena.Behaviour.Initialize(arena);
                foreach (var spawnData in arenaData.SpawnLocations)
                {
                    var behaviour = SpawnZoneBehaviour.CreateSafeZone($"Arena_{spawnData.Name}_{arenaData.Id}", spawnData.SpawnPoint);
                    behaviour.Initialize(spawnData, arena.Match);
                    arena.SpawnZones.Add(behaviour);
                }
                _arenas.Add(arenaData.Id, arena);
                var players = _arena.GetZonePlayers(arena.Data.ZoneId);
                foreach (var basePlayer in players)
                {
                    RemoveFromRustTeam(basePlayer.Player);
                    _arena.TeleportToLobby(basePlayer.Player, arena.Lobby.Data.Id, arena.Lobby.GetRandomSpawnLocation());
                }
                _arena.TransportedPlayers.AddRange(players.Select(x => x.Player));
            }
        }
        void SwitchTeam(BasePlayer player, Team team)
        {
            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null)
            {
                var arena = matchPlayer.Match.Arena;
                if (team == matchPlayer.Team)
                {
                    ShowTeamDeathmatchQuickMenu(player, arena.Match);
                    return;
                }
                if (team == Team.Blue || team == Team.Red)
                {
                    var isVip = (!arena.Data.RestrictAccess || arena.Data.AllowedPlayers.Contains(player.userID)) && !arena.Data.BannedPlayers.Contains(player.userID);
                    var isAccess = (!arena.Match.RestrictAccess || arena.Match.AllowedPlayers.Any(x => x.PlayerId == player.userID))
                                   && arena.Match.BannedPlayers.All(x => x.PlayerId != player.userID);

                    if (!isVip)
                    {
                        _arena.ShowMessage(player, Messages.ArenaNoVipAccess, this);
                        ShowTeamDeathmatchQuickMenu(player, arena.Match);
                        return;
                    }
                    if (!isAccess)
                    {
                        _arena.ShowMessage(player, Messages.ArenaRestrictAccess, this);
                        ShowTeamDeathmatchQuickMenu(player, arena.Match);
                        return;
                    }
                }

                if (arena.Match.IsStarted)
                {
                    _arena.ShowMessage(player, Messages.NoSwitchTeamOnRunningMatch, this);
                    ShowTeamDeathmatchQuickMenu(player, arena.Match);
                    return;
                }
                var currentPlayersCount = arena.Match.Players.Count(x => x.Team == Team.Blue || x.Team == Team.Red);
                if (team != Team.Spectator && currentPlayersCount >= arena.Data.Capacity)
                {
                    _arena.ShowMessage(player, Messages.MatchIsFull, this);
                    ShowTeamDeathmatchQuickMenu(player, arena.Match);
                    return;
                }
                RespawnInArena(matchPlayer, arena, team);
                matchPlayer.Team = team;
                matchPlayer.ManageInventory(false);
                if (team == Team.Spectator)
                {
                    if (matchPlayer.IsMatchLeader)
                    {
                        matchPlayer.IsMatchLeader = false;
                        var newLeader = arena.Match.Players.FirstOrDefault(x => x.Team != Team.Spectator);
                        if (newLeader != null)
                            newLeader.IsMatchLeader = true;
                    }
                    foreach (var otherPlayer in arena.Match.Players)
                    {
                        _arena.ShowMessage(otherPlayer.Player, Messages.ArenaSpectatorJoinMessage, this, player.displayName);
                    }
                }
                else
                {
                    var teamName = string.Empty;
                    if (team == Team.Blue)
                    {
                        teamName = arena.Data.BlueTeamDisplayName;
                    }
                    else if (team == Team.Red)
                    {
                        teamName = arena.Data.RedTeamDisplayName;
                    }
                    matchPlayer.IsMatchLeader = currentPlayersCount == 0;
                    foreach (var otherPlayer in arena.Match.Players)
                    {
                        _arena.ShowMessage(otherPlayer.Player, Messages.ArenaJoinMessage, this, player.displayName, teamName);
                    }
                }
                arena.Match.TryStartMatch();
            }
        }
        public void RespawnInArena(TeamDeathmatchPlayer matchPlayer, TeamDeathmatchArena arena, Team team)
        {
            var player = matchPlayer?.Player;
            if (player == null || player.IsNpc || arena == null)
                return;

            var destination = arena.GetRandomSpawnLocationForPlayer(player, team);
            if (destination.Equals(Vector3.zero))
            {
                return;
            }

            if (team != matchPlayer.Team)
            {
                AssignRustTeam(matchPlayer, team);
            }
            _arena.Teleport(player, destination);
            ShowTeamDeathmatchQuickMenu(player, arena.Match);
        }
        private void RemoveFromArenas(BasePlayer player)
        {
            TeamDeathmatchPlayer matchPlayer;
            if (Players.TryGetValue(player.userID, out matchPlayer) && matchPlayer != null)
            {
                matchPlayer.Match.Arena.SpawnZones.ForEach(s => { s.RemovePlayer(player); });
                MatchLeave(player);

                var leader = matchPlayer.Match.Players.FirstOrDefault(x => x.IsMatchLeader);
                if (leader == null || leader.PlayerId == player.userID)
                {
                    var bannedPlayerIds = matchPlayer.Match.BannedPlayers.Select(x => x.PlayerId);
                    var highPriorityPlayers =
                        matchPlayer.Match.Players.Where(x => !bannedPlayerIds.Contains(x.PlayerId));
                    var newLeader = highPriorityPlayers.FirstOrDefault(x => x.PlayerId != player.userID);
                    if (newLeader == null)
                    {
                        newLeader = matchPlayer.Match.Players.FirstOrDefault(x => x.PlayerId != player.userID);
                    }
                    if (newLeader != null)
                    {
                        newLeader.IsMatchLeader = true;
                        ShowTeamDeathmatchQuickMenu(newLeader.Player, newLeader.Match);
                        newLeader.Match.BannedPlayers.RemoveWhere(x => x.PlayerId == newLeader.PlayerId);
                        foreach (var deathmatchPlayer in newLeader.Match.Players)
                        {
                            _arena.ShowMessage(deathmatchPlayer.Player, Messages.MatchLeadershipChange, this, newLeader.PlayerName);
                        }
                    }
                }

                matchPlayer.Match.Players.RemoveWhere(x => x.PlayerId == player.userID);
            }
        }
        private string GetArenaCommands()
        {
            var commands = new StringBuilder(Environment.NewLine);
            commands.AppendLine("<color=#eb9534>Arena Console Commands:</color>");
            commands.AppendLine($"<color=#5af>{ConsoleCommands.ArenaHelp}</color> Get Arena management commands list");
            commands.AppendLine($"<color=#5af>{ConsoleCommands.ArenaEdit} <arenaId></color> Start editing an Arena");
            commands.AppendLine($"<color=#5af>{ConsoleCommands.ArenaDone}</color> Stop editing an Arena");
            commands.AppendLine($"<color=#5af>{ConsoleCommands.ArenaDelete} <arenaId></color> Delete an Arena");
            commands.AppendLine($"<color=#5af>{ConsoleCommands.ArenaCreate} <Name> <ZoneId> [<Capacity>] [<EntranceTriggerRadius>]</color> Create an Arena");
            commands.AppendLine($"<color=#5af>{ConsoleCommands.ArenaSet} <key> <value> [<key> <value>] [<key> <value>] ...</color> Set values for an editing Arena");
            commands.AppendLine($"<color=#5af>{ConsoleCommands.ArenaSetHelp}</color> Get list of available keys for editing the Arena");
            commands.AppendLine($"<color=#5af>{ConsoleCommands.ArenaSetSpawn} <position> <type> [<radius>] [<safe area radius>] [<chance>]</color> Add spawns points for an editing Arena");
            commands.AppendLine($"<color=#5af>{ConsoleCommands.ArenaSetSpawnHelp}</color> Get available types for spawn zones");
            commands.AppendLine($"<color=#5af>{ConsoleCommands.ArenaDeleteSpawn} <name></color> Delete a spawn point in Arena");
            commands.AppendLine($"<color=#5af>{ConsoleCommands.ArenaAllow} <add|remove> <arenaId> <playerId></color> Add/Remove player to the arena whitelist");
            commands.AppendLine($"<color=#5af>{ConsoleCommands.ArenaBan} <add|remove> <arenaId> <playerId></color> Ban/Unban player from the arena");
            commands.AppendLine("<color=#5582ff>For passing the position you can use 'here' to send your current position or use X,Y,Z coordinates</color>");
            return commands.ToString();
        }
        private string GetArenaEditingKeys()
        {
            var keys = new StringBuilder(Environment.NewLine);
            keys.AppendLine("<color=#5af>name</color>");
            keys.AppendLine("<color=#5af>zone</color> or <color=#5af>zoneId</color>");
            keys.AppendLine("<color=#5af>lobby</color> or <color=#5af>lobbyId</color>");
            keys.AppendLine("<color=#5af>enabled</color> or <color=#5af>isEnabled</color>");
            keys.AppendLine("<color=#5af>capacity</color>");
            keys.AppendLine("<color=#5af>wt</color> or <color=#5af>w.t</color> or <color=#5af>wounded.timeout</color>");
            keys.AppendLine("<color=#5af>entrance.pos</color>");
            keys.AppendLine("<color=#5af>entrance.active</color>");
            keys.AppendLine("<color=#5af>entrance.r</color> or <color=#5af>entrance.radius</color>");
            keys.AppendLine("<color=#5af>access.lobbyOnly</color>");
            keys.AppendLine("<color=#5af>entrance.vr</color> or <color=#5af>entrance.visibilityrange</color> or <color=#5af>entrance.textvisibilityrange</color>");
            keys.AppendLine("<color=#5af>restrict</color> or <color=#5af>restrictAccess</color>");
            keys.AppendLine("<color=#5af>countdown</color>");
            keys.AppendLine("<color=#5af>blueName</color>");
            keys.AppendLine("<color=#5af>redName</color>");
            keys.AppendLine("<color=#5af>text.started</color>");
            keys.AppendLine("<color=#5af>text.notstarted</color>");
            keys.AppendLine("<color=#5af>text</color> (It will set both <color=#5af>text.started</color> and <color=#5af>text.notstarted</color>)");
            keys.AppendLine($"Available placeholders for arena entrance text are: {GetArenaPlaceholders()}");
            keys.AppendLine("<color=#5af>kit.s</color> or <color=#5af>kit.spect</color> or <color=#5af>kit.spectator</color> or <color=#5af>loadout.s</color> or <color=#5af>loadout.spect</color> or <color=#5af>loadout.spectator</color>");
            keys.AppendLine("<color=#5af>kit.r</color> or <color=#5af>kit.red</color> or <color=#5af>loadout.r</color> or <color=#5af>loadout.red</color>");
            keys.AppendLine("<color=#5af>kit.b</color> or <color=#5af>kit.blue</color> or <color=#5af>loadout.b</color> or <color=#5af>loadout.blue</color>");
            keys.AppendLine("<color=#5af>kit.d</color> or <color=#5af>kit.default</color> or <color=#5af>loadout.d</color> or <color=#5af>loadout.default</color>");
            keys.AppendLine("<color=#5af>kit.add</color> or <color=#5af>loadout.add</color>");
            keys.AppendLine("<color=#5af>kit.remove</color> or <color=#5af>loadout.remove</color>");
            keys.AppendLine("<color=#5af>item.add</color>");
            keys.AppendLine("<color=#5af>item.remove</color>");
            return keys.ToString();
        }
        private string GetArenaSpawnPointTypes()
        {
            var keys = new StringBuilder(Environment.NewLine);
            keys.AppendLine("<color=#5af>b</color> or <color=#5af>blue</color>");
            keys.AppendLine("<color=#5af>r</color> or <color=#5af>red</color>");
            keys.AppendLine("<color=#5af>s</color> or <color=#5af>spect</color> or <color=#5af>spectator</color>");
            return keys.ToString();
        }
        private string GetArenaPlaceholders()
        {
            var placeholders = new StringBuilder(Environment.NewLine);
            placeholders.AppendLine("<color=#5af>$.Name</color> for arena name");
            placeholders.AppendLine("<color=#5af>$.Capacity</color> for arena capacity");
            placeholders.AppendLine("<color=#5af>$.MatchType</color> for arena match type");
            placeholders.AppendLine("<color=#5af>$.BlueCount</color> for count of blue team players");
            placeholders.AppendLine("<color=#5af>$.RedCount</color> for count of red team players");
            placeholders.AppendLine("<color=#5af>$.Blue</color> for display name of blue team");
            placeholders.AppendLine("<color=#5af>$.Red</color> for display name of red team");
            placeholders.AppendLine("<color=#5af>$.SpectatorCount</color> for count of spectator players");
            placeholders.AppendLine("<color=#5af>$.TotalCount</color> for total players count");
            return placeholders.ToString();
        }
        private void AssignRustTeam(TeamDeathmatchPlayer matchPlayer, Team team)
        {
            RemoveFromRustTeam(matchPlayer.Player);
            if (team == Team.Blue)
            {
                matchPlayer.Match.BlueTeam.AddPlayer(matchPlayer.Player);
            }
            else if (team == Team.Red)
            {
                matchPlayer.Match.RedTeam.AddPlayer(matchPlayer.Player);
            }
        }
        private void RemoveFromRustTeam(BasePlayer player)
        {
            var currentTeam = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
            if (currentTeam != null)
            {
                currentTeam.RemovePlayer(player.userID);
                player.ClearTeam();
            }
        }
        void MatchJoin(TeamDeathmatchPlayer matchPlayer)
        {
            Players[matchPlayer.PlayerId] = matchPlayer;
            TriggerNetworking();
        }
        void MatchLeave(BasePlayer player)
        {
            TeamDeathmatchPlayer matchPlayer;
            if (!Players.TryGetValue(player.userID, out matchPlayer) || matchPlayer == null)
            {
                return;
            }
            matchPlayer.Match.RemovePlayer(matchPlayer);
            Players.Remove(player.userID);
            matchPlayer.Match.CheckIfRoundShouldEnd();
            TriggerNetworking();
        }
        void RemoveArena(TeamDeathmatchArena arena)
        {
            foreach (var spawnZoneBehaviour in arena.SpawnZones)
            {
                spawnZoneBehaviour.DoDestroy();
            }
            arena.Behaviour.DoDestroy();

            foreach (var playerData in arena.Match.Players)
            {
                var spawnLocation = arena.Lobby.GetRandomSpawnLocation();
                var player = BasePlayer.FindByID(playerData.PlayerId);
                RemoveFromRustTeam(player);
                _arena.TeleportToLobby(player, arena.Lobby.Data.Id, spawnLocation);
            }

            _arenas.Remove(arena.Data.Id);
        }
        public void TeleportToArena(BasePlayer player, TeamDeathmatchArena arena, Team team)
        {
            if (player == null || player.IsNpc || arena == null)
                return;
            var isVip = (!arena.Data.RestrictAccess || arena.Data.AllowedPlayers.Contains(player.userID)) && !arena.Data.BannedPlayers.Contains(player.userID);
            var isAccess = (!arena.Match.RestrictAccess || arena.Match.AllowedPlayers.Any(x => x.PlayerId == player.userID))
                           && arena.Match.BannedPlayers.All(x => x.PlayerId != player.userID);

            var currentPlayersCount = arena.Match.Players.Count(x => x.Team == Team.Blue || x.Team == Team.Red);
            if (arena.Match.IsStarted || currentPlayersCount >= arena.Data.Capacity || !isVip || !isAccess)
            {
                team = Team.Spectator;
            }
            var destination = arena.GetRandomSpawnLocationForPlayer(player, team);
            if (destination.Equals(Vector3.zero))
            {
                return;
            }
            RemoveFromArenas(player);
            var matchPlayer = new TeamDeathmatchPlayer(player, arena.Match);
            if (team == Team.Spectator)
            {
                matchPlayer.Team = Team.Spectator;
            }
            else
            {
                matchPlayer.Team = team;
                matchPlayer.IsMatchLeader = currentPlayersCount == 0;
            }
            AssignRustTeam(matchPlayer, team);

            _arena.ResetPlayer(player);
            _arena.Teleport(player, destination);

            MatchJoin(matchPlayer);

            arena.Lobby.ActivePlayers.RemoveWhere(x => x.Player.userID == player.userID);
            arena.Match.AddPlayer(matchPlayer);
            if (team == Team.Spectator)
            {
                foreach (var otherPlayer in arena.Match.Players)
                {
                    _arena.ShowMessage(otherPlayer.Player, Messages.ArenaSpectatorJoinMessage, this, player.displayName);
                }
            }
            else
            {
                var teamName = string.Empty;
                if (team == Team.Blue)
                {
                    teamName = arena.Data.BlueTeamDisplayName;
                }
                else if (team == Team.Red)
                {
                    teamName = arena.Data.RedTeamDisplayName;
                }
                foreach (var otherPlayer in arena.Match.Players)
                {
                    _arena.ShowMessage(otherPlayer.Player, Messages.ArenaJoinMessage, this, player.displayName, teamName);
                }
            }
            timer.In(2.5f, () =>
            {
                ShowTeamDeathmatchQuickMenu(player, matchPlayer.Match);
            });
        }
        public void TriggerNetworking()
        {
            foreach (var teamDeathmatchPlayer in Players.Values)
            {
                TriggerNetworking(teamDeathmatchPlayer.Player);
            }
        }
        public void TriggerNetworking(BasePlayer player)
        {
            player.limitNetworking = true;
            player.limitNetworking = false;
            player.SendNetworkUpdate();
        }
        #endregion
    }
}