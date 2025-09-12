using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Globalization;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch.Math;
using UnityEngine.AI;
using Rust;
using Time = UnityEngine.Time;
using System.Text.RegularExpressions;
using Mesh = UnityEngine.Mesh;
using System.Text;
using UnityEngine.UI;
using Facepunch;
using Oxide.Ext.RustEdit;
using System.Collections;
using UnityEngine.UIElements;

namespace Oxide.Plugins
{
    [Info("Maze", "Ifte", "2.1.0")]
    public class Maze : RustPlugin
    {

        /*------------------------------------
         *     Updates and Changes
         * v2.0.1 - Fixed bugs   
         * v2.1.0 - Fixed for the April force wipe, Updated config, More optimization, Maze now only get winner from last man standing, Updated lang file (Remove old oxide/lang/en/Maze.json), Fixed OnNewSave not working properly(Now it removes old arena and updates), Added Transfer maze dead bodies loot to center of the arena, Added force stop maze event without breaking anything, Added support for vanish plugin
         *
         ------------------------------------*/

        /*
         * skull_fire_pit, skullspikes.candles,
         * 
         */

        #region Variables

        [PluginReference]
        Plugin Clans, ImageLibrary, FClan, Vanish;

        private static Maze ins;
        private Configuration config;
        private MazeEvent maze;
        private bool TeleportStart = false;

        private const string AdminMaze = "Maze.admin";
        private Timer EditingTimer;

        private readonly Dictionary<ulong, Timer> teleportTime = new Dictionary<ulong, Timer>();
        private readonly int MASK = LayerMask.GetMask("Construction", "Deployed");
        private readonly int MASKA = LayerMask.GetMask("Construction", "Deployed", "Ragdoll");

        private Dictionary<string, string> Images = new Dictionary<string, string>();
        private Dictionary<BasePlayer, MazeArenaSetting> EditingPlayer = new Dictionary<BasePlayer, MazeArenaSetting>();
        //private Dictionary<ulong, Vector3> OldLocation = new Dictionary<ulong, Vector3>();


        private ArenaData arenaData;

        private bool DebugBol = false; //Debug using this

        #endregion

        #region configuration

        private class Configuration
        {
            [JsonProperty(PropertyName = "Maze Automatic Event")]
            public bool holdAutoMaze = false;

            [JsonProperty(PropertyName = "Maze Automatic Event Interval Minimum (In Seconds)")]
            public int mazeAutoIntervalMin = 3600;

            [JsonProperty(PropertyName = "Maze Automatic Event Interval Maximum (In Seconds)")]
            public int mazeAutoIntervalMax = 7200;

            [JsonProperty(PropertyName = "Maze Minimum player requires to starts Auto")]
            public int minPlayers = 2;

            [JsonProperty(PropertyName = "Maze Automatic Event Random From List", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> randomList = new List<string>
            {
                "mazeareana",
                "maze2areana"
            };

            [JsonProperty(PropertyName = "Maze Event Player Teleport Timer (In Seconds)")]
            public int mazeteleporttimer = 15;

            [JsonProperty(PropertyName = "Maze Event Before Notification (In Seconds)")]
            public int mazeNotifyTimer = 300;

            [JsonProperty(PropertyName = "Maze Event Door Opening Notification (In Seconds)")]
            public int mazeDoorOpenTimer = 300;

            [JsonProperty(PropertyName = "Maze Event Door Closing In (In Seconds)")]
            public int mazeDoorClosingTimer = 600;

            [JsonProperty(PropertyName = "Maze Event Removing Walls Radius")]
            public int mazeRemoveWallRadius = 10;

            [JsonProperty(PropertyName = "Maze Event Shrink Amount")]
            public int mazeShrinkAmount = 3;

            [JsonProperty(PropertyName = "Maze Event Removing Walls Timer (In Seconds)")]
            public int mazeRemoveWallTimer = 15;

            [JsonProperty(PropertyName = "Show Kill Streak Messages")]
            public bool showkillstreak = true;

            [JsonProperty(PropertyName = "Maze Event Door Prefab")]
            public string mazeDoorPrefab = "assets/content/structures/interactive_garage_door/sliding_blast_door.prefab";

            [JsonProperty(PropertyName = "Maze Walls Item List", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> wallItem = new List<string>()
            {
                "assets/prefabs/building/wall.external.high.stone/wall.external.high.stone.prefab",
                "assets/prefabs/misc/xmas/icewalls/wall.external.high.ice.prefab",
                "assets/prefabs/misc/xmas/icewalls/icewall.prefab",
                "assets/prefabs/deployable/barricades/brarricade.cover.wood_double.prefab"
            };

            [JsonProperty(PropertyName = "Maze Event Blocked items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> blockItems = new List<string>
            {
                "grenade.beancan",
                "grenade.f1",
                "rock",
                "rocket.launcher",
                "ammo.rocket.basic",
                "ammo.rocket.smoke",
                "ammo.rocket.hv",
                "ammo.rocket.seeker",
                "ammo.rocket.fire",
                "explosive.satchel",
                "explosive.timed",
                "ammo.grenadelauncher.smoke",
                "ammo.grenadelauncher.buckshot",
                "ammo.grenadelauncher.he",
                "grenade.flashbang",
                "grenade.molotov",
                "multiplegrenadelauncher",
                "grenade.smoke"
            };

            [JsonProperty(PropertyName = "Maze Event Blocked Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> blockCommands = new List<string>
            {
                "kit",
                "trade",
                "remove",
                "tpa",
                "home",
                "shop",
                "clan"
            };

            [JsonProperty(PropertyName = "Maze Map Marker Setting")]
            public MarkerSetting marker = new MarkerSetting();

            [JsonProperty(PropertyName = "Maze Rewards")]
            public Rewards rewards = new Rewards();

            [JsonProperty(PropertyName = "Maze Chat Setting")]
            public ChatSetting chat = new ChatSetting();

            [JsonProperty(PropertyName = "Discord Setting")]
            public DiscordSetting discord = new DiscordSetting();

            [JsonProperty(PropertyName = "Auto Maze Arena Setup")]
            public AutoArenaSetup autoArenaSetup = new AutoArenaSetup();
        }

        private class Rewards
        {
            [JsonProperty(PropertyName = "Give commmands rewards upon winning the event")]
            public bool commandRewards = true;

            [JsonProperty(PropertyName = "Rewards player with higest kill from winning clan")]
            public bool rewardWinClanPlayer = true;

            [JsonProperty(PropertyName = "Rewards upon winning({PLAYER})", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> listRewards = new List<string>
            {
                "inventory.giveto {PLAYER} supply.signal 1"
            };

            [JsonProperty(PropertyName = "Spawn loot boxes in the arena")]
            public bool spawnLootBox = true;

            [JsonProperty(PropertyName = "Loot boxes time in seconds")]
            public int lootBoxTime = 300;
        }

        private class AutoArenaSetup
        {
            [JsonProperty(PropertyName = "Start Maze Arena Setup")]
            public bool startAuto = true;

            [JsonProperty(PropertyName = "Auto Arena Setup")]
            public Dictionary<string, MazeAutoArena> listauto = new Dictionary<string, MazeAutoArena>()
            {
                { "AutoMaze1", new MazeAutoArena()

                }
            };

        }

        private class MazeAutoArena
        {
            [JsonProperty(PropertyName = "Maze Center Item")]
            public string CenterItem = "skullspikes.candles";

            [JsonProperty(PropertyName = "Maze Arena Locate Radius")]
            public float radius = 100f;

            [JsonProperty(PropertyName = "Maze Arena Wall Radius")]
            public float wallradius = 40f;

            [JsonProperty(PropertyName = "Maze Arena Mesh Top & Bottom Height")]
            public float height = 50f;

            [JsonProperty(PropertyName = "Maze Spawn Points Item")]
            public string SpawnItem = "woodbox_deployed";

            [JsonProperty(PropertyName = "Maze Door Item")]
            public string DoorItem = "workbench1.deployed";

            [JsonProperty(PropertyName = "Maze Corner Item")]
            public string CornerItem = "mailbox";

        }

        private class ChatSetting
        {
            [JsonProperty(PropertyName = "Chat Avatar Icon")]
            public ulong avatarID = 0;

            [JsonProperty(PropertyName = "Chat Prefix")]
            public string prefix = "<color=#FFFF00>Maze</color> -> ";

            [JsonProperty(PropertyName = "Winner Message", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] WinnerMsg = new string[]
            {
                "<color=#FFD700>Maze Results:</color>",
                "\n",
                "Winner -> <color=#FFFF00>{WinningClanName}</color>",
                "",
                "{PlayerStats}",
                "",
                "<color=#FFD700>Totals:</color> Kills: {ClanTotalKills}, Deaths: {ClanTotalDeaths}, KDR: {ClanTotalKDR}",
                "Damage: {ClanTotalDamage}, Headshots: {ClanTotalHeadshots}",
                "",
                "<color=#FFD700>Aggregate Maze Totals:</color>",
                "- Total Kills: <color=#FFD700>{TotalKills}</color>",
                "- Total Damage: <color=#FFD700>{TotalDamages}</color>",
                "- Number of Participants: <color=#FFD700>{Participents}</color>"
            };

            [JsonProperty(PropertyName = "Participents Message", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] ParticipentsMessage = new string[]
            {
                "<color=#FFD700>Personal Stats</color>",
                "Kills: <color=#FFD700>{Kills}</color>, Deaths: <color=#FFD700>{Deaths}</color>, KDR: <color=#FFD700>{KDR}</color>, Total Damage: <color=#FFD700>{Damages}</color>, HeadShots: <color=#FFD700>{Headshots}</color>",
                "Shooting Accuracy: <color=#FFD700>{Accuracy}%</color>, HeadShot Accuracy: <color=#FFD700>{HeadshotAccuracy}%</color>"
            };

            [JsonProperty(PropertyName = "Maze Event Top Bar Message", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] topBar = new string[]
            {
                "<size=25><color=#FFFF00><b>MAZE</b></color></size>",
                "<size=15><color=#FFFF00><b>{DYNAMIC_TIMER}</b></color></size>",
                "",
                "<size=15><color=#FFFF00>{TotalTeams}</color><b> TEAMS REMAINING</b></size>",
                "<size=15><color=#FFFF00>{TotalPlayers}</color><b> PLAYERS REMAINING</b></size>"
            };


        }

        private class MarkerSetting
        {
            [JsonProperty(PropertyName = "Display Name")]
            public string markerDisplayName = "Maze";

            [JsonProperty(PropertyName = "Marker Radius")]
            public float markerRadius = 0.4f;

            [JsonProperty(PropertyName = "Marker Transparency")]
            public float markerTransparency = 0.75f;

            [JsonProperty(PropertyName = "Marker Color")]
            public string markerColor = "#FFFF00";
        }

        private class DiscordSetting
        {
            [JsonProperty(PropertyName = "Enable Discord Webhook")]
            public bool enableWebhook = false;

            [JsonProperty(PropertyName = "Discord Webhook URL")]
            public string discordWebhook = string.Empty;

            [JsonProperty(PropertyName = "Discord Webhook Message", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> messageWebhook = new List<string>
            {
                "**MAZE ARENA**",
                "",
                "**Details**",
                "Total Kills: `{TotalKills}`",
                "Total Teams: `{TotalTeams}`",
                "Total Players: `{Participents}`",
                "",
                "🏆**Winners**🏆",
                "**{WinnerClanName}**",
                "💀 Kills: `{ClanKills}`  ☠️ Deaths: `{ClanDeaths}`",
                "🎯 Headshots: `{ClanHeadshots}` 💥 Damages: `{ClanDamages}`",
                "",
                "👥**Members**👥",
                "{MembersStats}"
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();

        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config, true);
        }

        #endregion

        #region Lang

        private string GetMessage(string message, params object[] args)
        {
            return string.Format(lang.GetMessage(message, this), args);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "EventStartingNotify", "The event begins in {0} seconds. Get ready!" },
                { "EventStartingIn", "The event will start in {0} seconds. Prepare yourself!" },
                { "EventStarted", "The event has begun! Use <color=#FFFF00>/maze</color> to teleport to the Maze." },
                { "EventTeleporting", "Teleporting in {0} seconds. Hold tight!" },
                { "EventStop", "The event has ended. Thanks for participating!" },
                { "EventNoWinner", "No winner was found in this event." },
                { "EventDoorOpening", "The doors will open in {0} seconds. Be ready!" },
                { "EventDoorOpned", "The doors are open! Enter the Maze Arena now." },
                { "EventDoorClosing", "The doors will close in {0} seconds. Hurry up!" },
                { "EventDoorClosed", "The doors are closed! No more players can join." },
                { "EventRemovingWalls", "Outer walls will be removed in {0} seconds. Stay alert!" },
                { "WinningPlayers", "Winning players will be teleported back in {0} seconds. Congratulations!" },
                { "EventBannedItems", "You cannot join the event with banned items! Removed from inventory: <color=#DC143C>{0}</color>" },
                { "NoEventRunning", "There is no active event to teleport to." },
                { "NoClan", "You need to create a Clan before joining the event." },
                { "AlreadyPosition", "You are already at the event location." },
                { "CantTeleport", "You cannot teleport to the Maze right now." },
                { "TeleportCanceled", "Teleportation to the Maze has been canceled." },
                { "LootBoxesTime", "The winner has {0} seconds to collect their loot before it's gone!" }
            }, this);
        }

        #endregion

        #region Data

        private class ArenaData
        {
            public Dictionary<string, MazeArenaSetting> MazeArenas = new Dictionary<string, MazeArenaSetting>();
        }

        private class MazeArenaSetting
        {
            public string Name;
            public Vector3 Center;
            public float radius = 100;
            public float wallRadius = 50;
            public float meshHeight = 50;
            public List<Vector3> SpawnPoints;
            public List<DoorPosition> Doors;
            public List<WallPosition> Walls;
            public List<Vector3> Corners;

            public MazeArenaSetting()
            {
                Doors = new List<DoorPosition>();
                SpawnPoints = new List<Vector3>();
                Walls = new List<WallPosition>();
                Corners = new List<Vector3>();
            }

            public void PasteCopyMaze()
            {
                if (Walls.Count < 1) return;

                foreach (var item in Walls)
                {
                    BaseEntity entity = GameManager.server.CreateEntity(item.WallName, item.Position, default);
                    entity.Spawn();
                    entity.transform.Rotate(item.Rotation);
                    var c = entity as BaseCombatEntity;
                    c.OwnerID = 0;
                    c.SetMaxHealth(99999);
                    c.SetHealth(999999);
                    c.SendNetworkUpdateImmediate(true);
                }
            }

            public void CleanArena()
            {
                var entities = new List<BaseEntity>();
                entities.Clear();

                Vis.Entities(Center, radius, entities, ins.MASK);

                foreach (var item in entities)
                {
                    if (item.IsValid()) item.Kill();
                }
                entities.Clear();
            }
        }

        private class WallPosition
        {
            public string WallName;
            public Vector3 Position;
            public Vector3 Rotation;
        }

        private class DoorPosition
        {
            public Vector3 Position;
            public Vector3 Rotation;
        }

        private class CornerPosition
        {
            public int Num;
            public Vector3 Position;

        }

        private void SetupArenaCustom(bool nobiefy = false)  //do without RustEditAPI now
        {
            if (!config.autoArenaSetup.startAuto && config.autoArenaSetup.listauto.Count < 1)
            {
                Puts("Auto arena invalid setuped");
                return;
            }

            StringBuilder logBuilder = new StringBuilder();
            logBuilder.Clear();

            foreach (var a in config.autoArenaSetup.listauto)
            {
                if (arenaData.MazeArenas.ContainsKey(a.Key))
                {
                    PrintWarning($"{a.Key} already exists.");
                    PrintWarning("Reseting old arena...");
                    arenaData.MazeArenas.Remove(a.Key);
                }

                MazeAutoArena mazeAutoArena = a.Value;
                List<BaseEntity> allEntitys = new List<BaseEntity>();
                Vector3 pos = Vector3.zero;

                RustEditAPI.GetAllMapEntities(ref allEntitys);

                Puts($"{allEntitys.Count} count");

                List<DoorPosition> doorPos = new List<DoorPosition>();
                List<Vector3> spawnPos = new List<Vector3>();
                List<WallPosition> wallPos = new List<WallPosition>();
                List<Vector3> cornerPos = new List<Vector3>();

                int doorCount = 0, spawnCount = 0, wallCount = 0, cornerCount = 0;

                foreach (BaseEntity ent in allEntitys)
                {
                    if (ent == null)
                    {
                        Puts($"Found a null BaseEntity {ent.ShortPrefabName}, skipping...");
                        continue;
                    }

                    string prefabName = ent.ShortPrefabName;
                    if (ent.ShortPrefabName.Contains(mazeAutoArena.CenterItem))
                    {
                        Vector3 center = ent.GetNetworkPosition();
                        ent.Kill();
                        pos = center;
                        if (center == Vector3.zero)
                        {
                            Puts($"Center Position not found for {a.Key}");
                            continue;
                        }
                    }


                    if (prefabName.Contains(mazeAutoArena.DoorItem))
                    {
                        doorPos.Add(CreateDoorPosition(ent));
                        doorCount++;
                        ent.Kill();
                    }
                    else if (prefabName.Contains(mazeAutoArena.SpawnItem))
                    {
                        spawnPos.Add(ent.GetNetworkPosition());
                        spawnCount++;
                        ent.Kill();
                    }
                    else if (config.wallItem.Contains(ent.PrefabName))
                    {
                        wallPos.Add(CreateWallPosition(ent));
                        wallCount++;
                        ent.Kill();
                    }
                    else if (prefabName.Contains(mazeAutoArena.CornerItem))
                    {
                        cornerPos.Add(ent.GetNetworkPosition());
                        cornerCount++;
                        ent.Kill();
                    }

                }

                logBuilder.AppendLine($"Arena: {a.Key}");
                logBuilder.AppendLine($"Found Doors: {doorCount}");
                logBuilder.AppendLine($"Found Spawn Points: {spawnCount}");
                logBuilder.AppendLine($"Found Walls: {wallCount}");
                logBuilder.AppendLine($"Found Corners: {cornerCount}");

                var arena = new MazeArenaSetting()
                {
                    Center = pos,
                    Doors = doorPos,
                    SpawnPoints = spawnPos,
                    Walls = wallPos,
                    Name = a.Key,
                    radius = mazeAutoArena.radius,
                    wallRadius = mazeAutoArena.wallradius,
                    meshHeight = mazeAutoArena.height,
                    Corners = cornerPos,
                };

                arenaData.MazeArenas.Add(a.Key, arena);
                logBuilder.AppendLine($"Arena added: {a.Key}");
                SaveMazeArena();
                allEntitys.Clear();
            }

            Puts(logBuilder.ToString());

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (!CheckAdmin(player)) return;
                CM(player, logBuilder.ToString());
            }
        }

        private DoorPosition CreateDoorPosition(BaseEntity ent)
        {
            return new DoorPosition()
            {
                Position = ent.GetNetworkPosition(),
                Rotation = ent.GetNetworkRotation().eulerAngles,
            };
        }

        private WallPosition CreateWallPosition(BaseEntity ent)
        {
            return new WallPosition()
            {
                WallName = ent.PrefabName,
                Position = ent.GetNetworkPosition(),
                Rotation = ent.GetNetworkRotation().eulerAngles,
            };
        }

        private void LoadMazeArenas()
        {
            arenaData = Interface.Oxide.DataFileSystem.ReadObject<ArenaData>($"{Name}/MazeArenas");
        }

        private void SaveMazeArena()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/MazeArenas", arenaData);
        }

        private MazeArenaSetting GetMazeArena(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                foreach (var entry in arenaData.MazeArenas)
                {
                    if (entry.Key == name)
                        return entry.Value;
                }
            }
            return null;
        }

        private MazeData mazeData;

        private class MazeData
        {
            public Dictionary<ulong, PlayerData> playerData = new Dictionary<ulong, PlayerData>();
        }

        private class PlayerData
        {
            public int PlayerKills;
            public int Deaths;
            public int ShotsHit;
            public int HeadShots;
            public int ShotsFired;
            public float DamageDealt;
            public int KillStreak;
        }

        private PlayerData FindPlayerData(BasePlayer player)
        {
            if (mazeData.playerData.TryGetValue(player.userID, out PlayerData data))
            {
                return data;
            }
            PlayerData player1 = new PlayerData();
            mazeData.playerData.Add(player.userID, player1);

            return player1;
        }

        private void RemovePlayerData(BasePlayer player)
        {
            if (mazeData.playerData.TryGetValue(player.userID, out PlayerData data))
            {
                mazeData.playerData.Remove(player.userID);
            }
        }

        private List<KeyValuePair<string, int>> GetTop5ClansByKills()
        {
            Dictionary<string, int> clanKillCounts = new Dictionary<string, int>();

            foreach (var playerData in mazeData.playerData)
            {
                ulong playerID = playerData.Key;
                PlayerData data = playerData.Value;

                string clanTag = GetPlayerClanTag(playerID);
                if (string.IsNullOrEmpty(clanTag)) continue;

                if (!clanKillCounts.ContainsKey(clanTag))
                {
                    clanKillCounts[clanTag] = 0;
                }

                clanKillCounts[clanTag] += data.PlayerKills;
            }

            return clanKillCounts
                .OrderByDescending(kvp => kvp.Value)
                .Take(5)
                .ToList();
        }

        private KeyValuePair<string, int> GetTopClanByKills()
        {
            Dictionary<string, int> clanKillCounts = new Dictionary<string, int>();

            foreach (var playerData in mazeData.playerData)
            {
                ulong playerID = playerData.Key;
                PlayerData data = playerData.Value;

                string clanTag = GetPlayerClanTag(playerID);
                if (string.IsNullOrEmpty(clanTag)) continue;

                if (!clanKillCounts.ContainsKey(clanTag))
                {
                    clanKillCounts[clanTag] = 0;
                }

                clanKillCounts[clanTag] += data.PlayerKills;
            }

            return clanKillCounts
                .OrderByDescending(kvp => kvp.Value)
                .FirstOrDefault();
        }

        private Dictionary<ulong, PlayerData> GetAllPlayersByClanTag(string clanTag)
        {
            Dictionary<ulong, PlayerData> playersInClan = new Dictionary<ulong, PlayerData>();

            foreach (var playerEntry in mazeData.playerData)
            {
                ulong playerID = playerEntry.Key;
                PlayerData playerData = playerEntry.Value;

                string playerClanTag = GetPlayerClanTag(playerID);

                if (playerClanTag == clanTag)
                {
                    playersInClan[playerID] = playerData;
                }
            }

            return playersInClan;
        }

        private BasePlayer GetTopPlayerFromTopClan()
        {
            var topClan = GetTopClanByKills();
            if (topClan.Equals(default(KeyValuePair<string, int>)))
            {
                return null;
            }

            string topClanTag = topClan.Key;
            Dictionary<ulong, PlayerData> playersInTopClan = GetAllPlayersByClanTag(topClanTag);

            var topPlayerEntry = playersInTopClan
                .OrderByDescending(entry => entry.Value.PlayerKills)
                .FirstOrDefault();

            BasePlayer topPlayer = BasePlayer.activePlayerList
                .FirstOrDefault(player => player.userID == topPlayerEntry.Key);

            return topPlayer;
        }
        #endregion

        #region Commands      

        [ChatCommand("maze")]
        private void ChatCommandMaze(BasePlayer player, string cmd, string[] args)
        {
            if (args.Length == 0)
            {
                if (maze == null)
                {
                    CM(player, GetMessage("NoEventRunning"));
                    return;
                }

                string playerClan = GetPlayerClanTag(player.userID);

                if (string.IsNullOrEmpty(playerClan))
                {
                    CM(player, GetMessage("NoClan"));
                    return;
                }

                if (InMazeEvent(player))
                {
                    CM(player, GetMessage("AlreadyPosition"));
                    return;
                }

                if (!TeleportStart)
                {
                    CM(player, GetMessage("CantTeleport"));
                    return;
                }

                if (teleportTime.TryGetValue(player.userID, out Timer timer1))
                {
                    // If teleport is in progress, cancel it
                    timer1.Destroy();
                    teleportTime.Remove(player.userID);
                    CM(player, GetMessage("TeleportCanceled"));
                    return;
                }

                CM(player, GetMessage("EventTeleporting", config.mazeteleporttimer));

                MazeArenaSetting arena = maze.arena;

                if (arena != null)
                {
                    Vector3 teleportPos = arena.SpawnPoints.GetRandom();

                    Timer teleport = timer.Once(config.mazeteleporttimer, () =>
                    {
                        teleportPos.y += 0.1f;
                        //CheckForOldPos(player); //Check for old pos and save it
                        TeleportPlayer(player, teleportPos);
                        RemoveItemsFromInventory(player);

                        if (!maze.participents.Contains(player.userID)) maze.participents.Add(player.userID);
                        if (teleportTime.TryGetValue(player.userID, out Timer timer1))
                        {
                            timer1.Destroy();
                            teleportTime.Remove(player.userID);
                        }
                    });
                    teleportTime[player.userID] = teleport;
                }
                else
                {
                    Puts("Arena not found");
                }
                return;
            }

            if (args.Length >= 1)
            {
                string command = args[0].ToLower();
                if (command == "setup")
                {
                    if (!CheckAdmin(player)) return;

                    CM(player, GetAdminHelpText());
                    return;
                }
                else if (command == "create" && args.Length >= 2)
                {
                    string Name = args[1];

                    if (string.IsNullOrEmpty(Name))
                    {
                        CM(player, "Invalid Usage.\nCommand -> <color=#FFFF00>/maze create</color> <ArenaName>");
                        return;
                    }
                    if (arenaData.MazeArenas.ContainsKey(Name))
                    {
                        CM(player, "There is already a Maze Arena with that Name");
                        return;
                    }

                    arenaData.MazeArenas.Add(Name, new MazeArenaSetting()
                    {
                        Name = Name,
                        Center = player.transform.position,
                    });

                    CM(player, $"Maze Arena created <color=#FFFF00>{Name}</color>");
                    player.SendConsoleCommand("ddraw.sphere", 15, Color.blue, player.transform.position, 30);
                }
                else if (command == "edit" && args.Length >= 2)
                {
                    if (!CheckAdmin(player)) return;

                    string Name = args[1];
                    if (string.IsNullOrEmpty(Name))
                    {
                        CM(player, $"Invalid Usage.\nCommand -> <color=#FFFF00>/maze edit</color> <ArenaName>");
                        return;
                    }
                    if (!arenaData.MazeArenas.ContainsKey(Name))
                    {
                        CM(player, "There is no Maze Arena with that Name");
                        return;
                    }

                    MazeArenaSetting arena = GetMazeArena(Name);

                    if (EditingPlayer.ContainsKey(player))
                    {
                        EditingTimer.Destroy();
                        EditingPlayer.Remove(player);
                    }

                    EditingPlayer.Add(player, arena);
                    CM(player, $"You are now editing <color=#FFFF00>{Name}</color>\n");

                    EditingTimer = timer.Every(1, () =>
                    {
                        if (arena.Center != null || arena.Center != Vector3.zero)
                        {
                            player.SendConsoleCommand("ddraw.sphere", 1, Color.blue, arena.Center, arena.radius);
                            player.SendConsoleCommand("ddraw.text", 1, Color.red, arena.Center, "Arena Center");
                            player.SendConsoleCommand("ddraw.sphere", 1, Color.blue, arena.Center, 1f);
                        }
                        if (arena.Corners.Count > 0)
                        {
                            foreach (var corner in arena.Corners)
                            {
                                player.SendConsoleCommand("ddraw.text", 1, Color.yellow, corner, $"Corner\n{corner.ToString()}");
                                player.SendConsoleCommand("ddraw.sphere", 1, Color.red, corner, 1f);
                            }
                        }
                        if (arena.SpawnPoints.Count > 0)
                        {
                            foreach (var corner in arena.SpawnPoints)
                            {
                                player.SendConsoleCommand("ddraw.text", 1, Color.yellow, corner, $"Spawn Points\n{corner.ToString()}");
                                player.SendConsoleCommand("ddraw.sphere", 1, Color.red, corner, 1f);
                            }
                        }
                        if (arena.Doors.Count > 0)
                        {
                            foreach (var corner in arena.Doors)
                            {
                                player.SendConsoleCommand("ddraw.text", 1, Color.yellow, corner.Position, $"Door Position\n{corner.Position}\nRotation: {corner.Rotation}");
                                player.SendConsoleCommand("ddraw.sphere", 1, Color.red, corner.Position, 1f);
                            }
                        }
                        if (arena.wallRadius > 0)
                        {
                            player.SendConsoleCommand("ddraw.sphere", 1, Color.black, arena.Center, arena.wallRadius);
                        }


                    });
                }
                else if (command == "stopedit")
                {
                    if (!CheckAdmin(player)) return;
                    if (!EditingPlayer.ContainsKey(player))
                    {
                        CM(player, "Please specify a Maze Arena first to edit(Command -> <color=#FFFF00>/maze edit</color> <ArenaName>)");
                        return;
                    }

                    if (EditingPlayer.ContainsKey(player))
                    {
                        EditingTimer.Destroy();
                        EditingPlayer.Remove(player);
                        CM(player, "Stopped editing the Arena");
                    }
                    else
                    {
                        CM(player, "Currently not editing Arena");
                    }
                    return;
                }
                else if (command == "setcorner")
                {
                    if (!CheckAdmin(player)) return;
                    if (!EditingPlayer.ContainsKey(player))
                    {
                        CM(player, "Please specify a Maze Arena first to edit(Command -> <color=#FFFF00>/maze edit</color> <ArenaName>)");
                        return;
                    }
                    MazeArenaSetting arena = EditingPlayer[player];

                    if (arena != null)
                    {
                        arena.Corners.Add(player.GetNetworkPosition());
                        CM(player, "Corner added to list");
                        return;
                    }
                    return;
                }
                else if (command == "showmesh")
                {
                    if (!CheckAdmin(player)) return;
                    if (!EditingPlayer.ContainsKey(player))
                    {
                        CM(player, "Please specify a Maze Arena first to edit(Command -> <color=#FFFF00>/maze edit</color> <ArenaName>)");
                        return;
                    }
                    MazeArenaSetting arena = EditingPlayer[player];

                    if (arena != null)
                    {
                        if (arena.Center == null || arena.Center == Vector3.zero)
                        {
                            CM(player, "Center not found");
                            return;
                        }

                        if (arena.Corners.Count != 4 && arena.Corners.Count != 6 && arena.Corners.Count != 8)
                        {
                            CM(player, "4, 6, or 8 corners are required to show the mesh.");
                            return;
                        }

                        Mesh mesh = GenerateMeshRandomVec(arena.Center, arena.Corners, arena.meshHeight);

                        // Visualize the mesh
                        Vector3[] vertices = mesh.vertices;
                        int[] triangles = mesh.triangles;

                        for (int i = 0; i < triangles.Length; i += 3)
                        {
                            Vector3 vertex1 = arena.Center + vertices[triangles[i]];
                            Vector3 vertex2 = arena.Center + vertices[triangles[i + 1]];
                            Vector3 vertex3 = arena.Center + vertices[triangles[i + 2]];

                            player.SendConsoleCommand("ddraw.line", 120, Color.green, vertex1, vertex2);
                            player.SendConsoleCommand("ddraw.line", 120, Color.green, vertex2, vertex3);
                            player.SendConsoleCommand("ddraw.line", 120, Color.green, vertex3, vertex1);
                        }
                        timer.In(30, () =>
                        {
                            DestroyMesh(mesh);
                        });
                    }

                }
                else if (command == "copywalls")
                {
                    if (!CheckAdmin(player)) return;
                    if (!EditingPlayer.ContainsKey(player))
                    {
                        CM(player, "Please specify a Maze Arena first to edit(Command -> <color=#FFFF00>/maze edit</color> <ArenaName>)");
                        return;
                    }
                    MazeArenaSetting arena = EditingPlayer[player];

                    if (arena != null)
                    {
                        List<BaseEntity> entitys = new List<BaseEntity>();

                        Vis.Entities(arena.Center, arena.radius, entitys, MASK);
                        int amount = 0;
                        arena.Walls.Clear();
                        CM(player, "Cleared old walls in the arena");
                        foreach (var ent in entitys)
                        {
                            foreach (var w in config.wallItem)
                            {
                                if (w == ent.PrefabName)
                                {
                                    WallPosition wall = new WallPosition
                                    {
                                        WallName = ent.PrefabName,
                                        Position = ent.GetNetworkPosition(),
                                        Rotation = ent.GetNetworkRotation().eulerAngles
                                    };
                                    amount++;
                                    arena.Walls.Add(wall);
                                }
                            }
                        }

                        CM(player, amount + " walls found and stored");
                    }
                    return;
                }
                else if (command == "pastewalls")
                {
                    if (!CheckAdmin(player)) return;
                    if (!EditingPlayer.ContainsKey(player))
                    {
                        CM(player, "Please specify a Maze Arena first to edit(Command -> <color=#FFFF00>/maze edit</color> <ArenaName>)");
                        return;
                    }
                    MazeArenaSetting arena = EditingPlayer[player];

                    if (arena != null)
                    {
                        arena.PasteCopyMaze();
                        CM(player, arena.Walls.Count + " walls found and pasted");
                    }
                    return;
                }
                else if (command == "adddoor")
                {
                    if (!CheckAdmin(player)) return;
                    if (!EditingPlayer.ContainsKey(player))
                    {
                        CM(player, "Please specify a Maze Arena first to edit(Command -> <color=#FFFF00>/maze edit</color> <ArenaName>)");
                        return;
                    }

                    MazeArenaSetting arena = EditingPlayer[player];

                    if (arena != null)
                    {
                        var rotation = player.GetNetworkRotation().eulerAngles;
                        rotation.x = 0;
                        DoorPosition door = new DoorPosition
                        {
                            Position = player.GetNetworkPosition(),
                            Rotation = rotation,
                        };

                        arena.Doors.Add(door);
                        CM(player, "Door position added for this location.");
                        var doorTemp = GameManager.server.CreateEntity(config.mazeDoorPrefab, player.GetNetworkPosition());
                        doorTemp.Spawn();
                        var c = doorTemp as BaseCombatEntity;
                        c.SetMaxHealth(99999);
                        c.SetHealth(999999);

                        doorTemp.transform.Rotate(rotation);
                        doorTemp.OwnerID = 6969;
                        doorTemp.SendNetworkUpdateImmediate(true);

                        timer.Once(10, () =>
                        {
                            doorTemp.Kill();
                        });
                    }
                    return;
                }
                else if (command == "removedoor")
                {
                    if (!CheckAdmin(player)) return;
                    if (!EditingPlayer.ContainsKey(player))
                    {
                        CM(player, "Please specify a Maze Arena first to edit(Command -> <color=#FFFF00>/maze edit</color> <ArenaName>)");
                        return;
                    }

                    MazeArenaSetting arena = EditingPlayer[player];

                    List<DoorPosition> doorsToRemove = new List<DoorPosition>();

                    foreach (var dor in arena.Doors)
                    {
                        float distance = Vector3.Distance(player.GetNetworkPosition(), dor.Position);
                        if (distance <= 5)
                        {
                            doorsToRemove.Add(dor);
                        }
                    }

                    if (doorsToRemove.Count == 0)
                    {
                        CM(player, "No doors found near this 5 radius area");
                        return;
                    }

                    foreach (var dor in doorsToRemove)
                    {
                        arena.Doors.Remove(dor);
                    }
                    CM(player, "Removed " + doorsToRemove.Count);
                    return;
                }
                else if (command == "location")
                {
                    if (!CheckAdmin(player)) return;
                    if (!EditingPlayer.ContainsKey(player))
                    {
                        CM(player, "Please specify a Maze Arena first to edit(Command -> <color=#FFFF00>/maze edit</color> <ArenaName>)");
                        return;
                    }

                    MazeArenaSetting arena = EditingPlayer[player];

                    if (arena != null)
                    {
                        arena.Center = player.transform.position;
                        CM(player, "Maze Arena position changed to your postion.");
                    }
                    return;
                }
                else if (command == "radius" && args.Length >= 2)
                {
                    if (!CheckAdmin(player)) return;
                    if (!EditingPlayer.ContainsKey(player))
                    {
                        CM(player, "Please specify a Maze Arena first to edit(Command -> <color=#FFFF00>/maze edit</color> <ArenaName>)");
                        return;
                    }

                    MazeArenaSetting arena = EditingPlayer[player];
                    float Radius = float.Parse(args[1]);

                    if (arena != null)
                    {
                        arena.radius = Radius;
                        CM(player, "Maze Arena radius changed");
                    }
                    return;
                }
                else if (command == "wallradius" && args.Length >= 2)
                {
                    if (!CheckAdmin(player)) return;
                    if (!EditingPlayer.ContainsKey(player))
                    {
                        CM(player, "Please specify a Maze Arena first to edit(Command -> <color=#FFFF00>/maze edit</color> <ArenaName>)");
                        return;
                    }

                    MazeArenaSetting arena = EditingPlayer[player];
                    float Radius = float.Parse(args[1]);

                    if (arena != null)
                    {
                        arena.wallRadius = Radius;
                        CM(player, "Maze Arena Wall radius changed");
                    }

                    return;
                }
                else if (command == "meshheight" && args.Length >= 2)
                {
                    if (!CheckAdmin(player)) return;
                    if (!EditingPlayer.ContainsKey(player))
                    {
                        CM(player, "Please specify a Maze Arena first to edit(Command -> <color=#FFFF00>/maze edit</color> <ArenaName>)");
                        return;
                    }

                    MazeArenaSetting arena = EditingPlayer[player];
                    float Radius = float.Parse(args[1]);

                    if (arena != null)
                    {
                        arena.meshHeight = Radius;
                        CM(player, "Maze Arena Mesh Height changed");
                    }
                    player.Command("maze showmesh");
                    return;
                }
                else if (command == "start" && args.Length >= 2)
                {
                    if (!CheckAdmin(player)) return;

                    string Name = args[1];

                    MazeArenaSetting arena = GetMazeArena(Name);

                    if (arena == null)
                    {
                        CM(player, "Arena not found");
                        return;
                    }
                    if (maze != null)
                    {
                        CM(player, "There is already an event running");
                        return;
                    }
                    GameObject gameObject = new GameObject();
                    maze = gameObject.AddComponent<MazeEvent>();
                    maze.Initialize(arena);
                    return;
                }
                else if (command == "cleanarena")
                {
                    if (!CheckAdmin(player)) return;
                    if (!EditingPlayer.ContainsKey(player))
                    {
                        CM(player, "Please specify a Maze Arena first to edit(Command -> <color=#FFFF00>/maze edit</color> <ArenaName>)");
                        return;
                    }

                    MazeArenaSetting arena = EditingPlayer[player];
                    arena.CleanArena();
                    CM(player, "Arena cleared");
                }
                else if (command == "cleandoors")
                {
                    if (!CheckAdmin(player)) return;
                    if (!EditingPlayer.ContainsKey(player))
                    {
                        CM(player, "Please specify a Maze Arena first to edit(Command -> <color=#FFFF00>/maze edit</color> <ArenaName>)");
                        return;
                    }

                    MazeArenaSetting arena = EditingPlayer[player];

                    if (arena != null)
                    {
                        arena.Doors.Clear();
                        CM(player, "You cleard all door location for the Arena");
                    }
                    else CM(player, "Arena not found");
                }
                else if (command == "setspawn")
                {
                    if (!CheckAdmin(player)) return;
                    if (!EditingPlayer.ContainsKey(player))
                    {
                        CM(player, "Please specify a Maze Arena first to edit(Command -> <color=#FFFF00>/maze edit</color> <ArenaName>)");
                        return;
                    }

                    MazeArenaSetting arena = EditingPlayer[player];

                    arena.SpawnPoints.Add(player.transform.position);
                    CM(player, "This position added as spawn point for the arena");
                }
                else if (command == "spawnclear")
                {
                    if (!CheckAdmin(player)) return;
                    if (!EditingPlayer.ContainsKey(player))
                    {
                        CM(player, "Please specify a Maze Arena first to edit(Command -> <color=#FFFF00>/maze edit</color> <ArenaName>)");
                        return;
                    }

                    MazeArenaSetting arena = EditingPlayer[player];

                    arena.SpawnPoints.Clear();
                    CM(player, "You have cleared all the spawn point of this arena");
                }
                else if (command == "list")
                {
                    if (!CheckAdmin(player)) return;

                    string message = "Arena List:\n";
                    foreach (var arena in arenaData.MazeArenas)
                    {
                        MazeArenaSetting arenaData = arena.Value;
                        message += $"- <color=#FFFF00>{arenaData.Name}</color> Location: <color=#FFFF00>{arenaData.Center}</color> Radius: <color=#FFFF00>{arenaData.radius}</color>\n";
                    }
                    CM(player, message);
                    return;
                }
                else if ((command == "delete" || command == "remove") && args.Length >= 2)
                {
                    if (!CheckAdmin(player)) return;
                    string Name = args[1];
                    MazeArenaSetting arena = GetMazeArena(Name);
                    if (arena != null)
                    {
                        arenaData.MazeArenas.Remove(arena.Name);
                        CM(player, $"You have removed <color=#FFFF00>{arena.Name}</color> arena");
                    }
                    else
                    {
                        CM(player, "Arena not found");
                        return;
                    }
                }
                else if (command == "killdoor")
                {
                    if (!CheckAdmin(player)) return;
                    if (!EditingPlayer.ContainsKey(player))
                    {
                        CM(player, "Please specify a Maze Arena first to edit(Command -> <color=#FFFF00>/maze edit</color> <ArenaName>)");
                        return;
                    }

                    MazeArenaSetting arena = EditingPlayer[player];

                    if (arena != null)
                    {
                        List<BaseEntity> list = new List<BaseEntity>();
                        Vis.Entities(arena.Center, arena.radius, list);
                        List<BaseEntity> d = new List<BaseEntity>();
                        foreach (BaseEntity ent in list)
                        {
                            if (ent.PrefabName == config.mazeDoorPrefab)
                            {
                                Puts($"Found prefab {ent.PrefabName}");
                                d.Add(ent);
                            }
                        }
                        d.ForEach(ent => {
                            ent.Kill();
                        });
                    }
                }
            }
        }

        [Command("mazestart")]
        private void ICommandStartMaze(IPlayer player, string cmd, string[] args)
        {
            if (!player.HasPermission(AdminMaze))
            {
                player.Message("You don't have permission to use this command.");
                return;
            }
            if (maze != null)
            {
                player.Message("There is already a event running at this moment.");
                return;
            }
            if (args.Length == 0)
            {
                System.Random random = new System.Random();
                int randomIndex = random.Next(0, config.randomList.Count);

                string getZone = config.randomList[randomIndex];

                MazeArenaSetting arena = GetMazeArena(getZone);

                if (arena == null)
                {
                    player.Message("Arena Not Found");
                    return;
                }

                GameObject gameObject = new GameObject();
                maze = gameObject.AddComponent<MazeEvent>();
                maze.Initialize(arena);
                player.Message("Maze starting arena " + arena.Name);
                Puts("Maze starting arena " + arena.Name);
                return;
            }

            if (args.Length >= 1)
            {
                string ArenaName = args[0];

                MazeArenaSetting arena = GetMazeArena(ArenaName);

                if (arena != null)
                {
                    GameObject gameObject = new GameObject();
                    maze = gameObject.AddComponent<MazeEvent>();
                    maze.Initialize(arena);
                    player.Message("Maze starting arena " + arena.Name);
                    Puts("Maze starting arena " + arena.Name);
                }
                else
                {
                    player.Message("Arena Not Found");
                    return;
                }
            }
        }

        [Command("mazestop")]
        private void ICommandStopMaze(IPlayer player, string cmd, string[] args)
        {
            if (!player.HasPermission(AdminMaze))
            {
                player.Message("You don't have permission to use this command.");
                return;
            }
            if (maze == null)
            {
                player.Message("There is no event running at this moment.");
                return;
            }

            if (maze != null)
            {
                Puts("Stoping Maze event");
                maze.TryForceStop();
            }
        }


        [ChatCommand("maze_setup_onlyfortest")]
        private void MazeAutoArenaCMDChat(BasePlayer player, string cmd, string[] args)
        {
            if (!CheckAdmin(player)) return;
            CM(player, "Trying Auto Arena setup from config...");
            SetupArenaCustom();

        }

        [ConsoleCommand("maze_setup_onlyfortest")]
        private void MazeAutoArenaCMDConsole(ConsoleSystem.Arg args)
        {
            Puts("Trying Auto Arena setup from config...");
            SetupArenaCustom();
        }

        #endregion

        #region Init

        private void Loaded()
        {
            LoadMazeArenas();
        }

        private void OnServerInitialized()
        {
            ins = this;
            CheckPlugins();

            permission.RegisterPermission(AdminMaze, this);
            AddCovalenceCommand("mazestart", "ICommandStartMaze");
            AddCovalenceCommand("mazestop", "ICommandStopMaze");
            AddCovalenceCommand("maze_reset_players", "MazeresetPlayersCMD");
            AddCovalenceCommand("maze_reset_clans", "MazeresetClansCMD");
  
            UnsubscribeEventHooks();

            StartAutoStartRoutine();
        }

        private void Unload()
        {
            EditingTimer?.Destroy();
            CloseUI();
            UnsubscribeEventHooks();
            if (maze != null && !maze.isEnding)
            {
                maze.TryForceStop();
                maze = null;
            }

            if (config.holdAutoMaze && autoStartCoroutine != null)
            {
                ServerMgr.Instance.StopCoroutine(autoStartCoroutine);
                autoStartCoroutine = null;
            }
        }

        private void OnServerSave()
        {
            SaveMazeArena();
        }

        private void OnNewSave(string filename)
        {
            PrintWarning("Deteting new save, Looking for Maze Auto Setup!");
            PrintWarning("Trying Auto Arena setup from config...");
            SetupArenaCustom();
        }

        #endregion

        #region Hooks

        private object OnPlayerCorpseSpawned(BasePlayer player, PlayerCorpse corpse)
        {
            if (player == null || player.IPlayer == null || corpse == null)
            {
                return null;
            }

            if (InMazeArenaRadius(player))
            {
                corpse.ResetRemovalTime(0);
            }

            return null;
        }

        private int TempTotalKills;
        private double TempTotalDamages;

        private void OnPlayerDeath(BasePlayer player, HitInfo hit)
        {
            if (player == null || hit == null || !player.userID.IsSteamId() || maze == null || !InMazeEvent(player))
                return;

            maze.PlayingList.Remove(player);
            maze.InMazeArena.Remove(player);
            maze.RemoveUI(player);
            if (hit.InitiatorPlayer == null)
            {
                PlayerData victimData = FindPlayerData(player);
                victimData.KillStreak = 0;
                victimData.Deaths++;
                return;
            }

            BasePlayer initiator = hit.InitiatorPlayer;

            if (maze.PlayingList.Contains(player) && maze.PlayingList.Contains(initiator))
            {
                if (IsAlliedWithPlayer(player.userID, initiator.userID))
                {
                    return;
                }

                if (initiator != player)
                {
                    PlayerData attackerData = FindPlayerData(initiator);
                    PlayerData victimData = FindPlayerData(player);

                    attackerData.PlayerKills++;
                    attackerData.KillStreak++;
                    victimData.Deaths++;
                    victimData.KillStreak = 0;

                    TempTotalKills++;
                    KillStreakCheck(initiator);
                }
                else
                {
                    PlayerData victimData = FindPlayerData(player);
                    victimData.Deaths++;
                    victimData.KillStreak = 0;
                } 
            }

            return;
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (maze != null && InMazeEvent(player))
            {
                try
                {
                    maze?.InMazeArena.Remove(player);
                    player?.Kill();
                }
                catch { }
            }
        }


        private void OnWeaponFired(BaseProjectile asdasd, BasePlayer player, ItemModProjectile mod, ProtoBuf.ProjectileShoot agaga)
        {
            if (player != null && maze != null && player.userID.IsSteamId() && maze.PlayingList.Contains(player))
            {
                PlayerData attacker = FindPlayerData(player);
                if (mod.ToString().Contains("arrow") || mod.ToString().Contains("ammo"))
                {
                    attacker.ShotsFired++;
                }
            }
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null || hitInfo == null) return;

            if (entity is BasePlayer && hitInfo.Initiator is BasePlayer && maze != null)
            {
                var attackerEnt = hitInfo.Initiator as BasePlayer;
                var victimEnt = entity as BasePlayer;

                if (attackerEnt != null && attackerEnt.userID.IsSteamId() && victimEnt != null && victimEnt.userID.IsSteamId())
                {
                    if (victimEnt.userID.IsSteamId() && attackerEnt.userID.IsSteamId())
                    {
                        bool victimPlaying = maze.PlayingList.Contains(victimEnt);
                        bool attackerPlaying = maze.PlayingList.Contains(attackerEnt);

                        if ((attackerPlaying && !victimPlaying) || (!attackerPlaying && victimPlaying))
                        {
                            CancelDamage(hitInfo);
                            return;
                        }
                    }

                    if (maze.PlayingList.Contains(attackerEnt) && maze.PlayingList.Contains(victimEnt) && !IsAlliedWithPlayer(attackerEnt.userID, victimEnt.userID))
                    {
                        // Player Data
                        PlayerData attacker = FindPlayerData(attackerEnt);

                        if (hitInfo.isHeadshot)
                        {
                            attacker.HeadShots++;
                        }
                        else
                        {
                            attacker.ShotsHit++;
                        }

                        TempTotalDamages += hitInfo.damageTypes.Total();

                        attacker.DamageDealt += hitInfo.damageTypes.Total();
                    }
                }
            }
        }

        private object OnStructureUpgrade(BuildingBlock buildingBlock, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (permission.UserHasPermission(player.UserIDString, AdminMaze)) return null;

            if (maze != null && InMazeEvent(player))
            {
                CM(player, "You are not allowed to upgrade structures in this area!");
                return true;
            }
            return null;
        }

        private void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            if (!planner || !gameObject)
                return;

            BasePlayer player = planner.GetOwnerPlayer();
            if (!player)
                return;

            if (maze == null) return;
            if (!InMazeEvent(player)) return;
            if (permission.UserHasPermission(player.UserIDString, AdminMaze)) return;

            BaseEntity entity = gameObject.ToBaseEntity();
            if (!entity)
                return;

            if (entity is BuildingBlock)
            {
                List<ItemAmount> list = (entity as BuildingBlock).BuildCost();

                entity.Invoke(() =>
                {
                    if (list != null)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            ItemAmount itemAmount = list[i];
                            player.GiveItem(ItemManager.Create(itemAmount.itemDef, Mathf.Clamp(Mathf.RoundToInt(itemAmount.amount), 1, int.MaxValue)));
                        }
                    }

                    if (entity && !entity.IsDestroyed)
                        entity.Kill(BaseNetworkable.DestroyMode.Gib);
                }, 0.1f);

                CM(player, "You are not allowed to build in this area!");
            }
            else if (entity is SimpleBuildingBlock)
            {
                KillEntityAndReturnItem(player, entity, planner.GetItem());
                CM(player, "You are not allowed to build in this area!");
            }
            else
            {
                if (entity is BuildingPrivlidge)
                {
                    KillEntityAndReturnItem(player, entity, planner.GetItem());
                    CM(player, "You are not allowed to deploy cupboards in this area!");
                }
                else
                {
                    KillEntityAndReturnItem(player, entity, planner.GetItem());
                    CM(player, "You are not allowed to deploy cupboards in this area!");
                }
            }
        }

        private object OnUserCommand(IPlayer player, string command, string[] args)
        {
            if (IsBlockedCommand(command) && maze != null)
            {
                BasePlayer p = player.Object as BasePlayer;
                if (maze.PlayingList.Contains(p))
                {
                    CM(p, "You can't excute this command in this Arena.");
                    return true;
                }
            }
            return null;
        }

        private void OnVanishDisappear(BasePlayer player)
        {
            try
            {
                if (maze != null && InMazeEvent(player))
                {
                    maze.PlayingList.Remove(player);
                }
            }catch { }
        }

        #endregion

        #region Main      

        private static Coroutine autoStartCoroutine;

        private void StartAutoStartRoutine()
        {
            if (!config.holdAutoMaze) return;
            if (arenaData.MazeArenas.Count < 1) return;
            if (autoStartCoroutine != null) return;

            autoStartCoroutine = ServerMgr.Instance.StartCoroutine(AutoStartRoutine());
        }

        private IEnumerator AutoStartRoutine()
        {
            float time = UnityEngine.Random.Range(config.mazeAutoIntervalMin, config.mazeAutoIntervalMax);
            Puts($"Next event in {GetFormatTime(time)}");
            yield return new WaitForSeconds(time);

            if (maze == null && BasePlayer.activePlayerList.Count >= config.minPlayers)
            {
                string selectedZone = config.randomList[UnityEngine.Random.Range(0, config.randomList.Count)];

                MazeArenaSetting arena = GetMazeArena(selectedZone);
                if (arena == null)
                {
                    PrintWarning("Invalid arena, restarting the coroutine.");
                    if (autoStartCoroutine != null)
                    {
                        ServerMgr.Instance.StopCoroutine(autoStartCoroutine);
                        autoStartCoroutine = null;
                    }
                    StartAutoStartRoutine();
                }
                else
                {
                    GameObject gameObject = new GameObject();
                    maze = gameObject.AddComponent<MazeEvent>();
                    maze.Initialize(arena);
                }
            }
            else
            {
                PrintWarning("Not enough players to start the event or another maze is running");
                if (autoStartCoroutine != null)
                {
                    ServerMgr.Instance.StopCoroutine(autoStartCoroutine);
                    autoStartCoroutine = null;
                }
                StartAutoStartRoutine();
            }
        }


        private void CheckPlugins()
        {
            if (Clans == null && FClan == null) PrintError("Clans is required to use this plugin.");
            if (ImageLibrary == null) PrintError("ImageLibrary is required to use this plugin.");
        }

        private class MazeEvent : MonoBehaviour
        {
            public Configuration config;
            public MazeArenaSetting arena;
            private List<BaseEntity> doorsCreated = new List<BaseEntity>();
            private HashSet<VendingMachineMapMarker> MapVendingList = new HashSet<VendingMachineMapMarker>();
            private HashSet<MapMarkerGenericRadius> MapMarkerList = new HashSet<MapMarkerGenericRadius>();
            public HashSet<BasePlayer> PlayingList = new HashSet<BasePlayer>();
            public HashSet<BasePlayer> InMazeArena = new HashSet<BasePlayer>();
            public HashSet<ulong> winnerList = new HashSet<ulong>();
            public HashSet<ulong> participents = new HashSet<ulong>();
            private List<BaseEntity> wallsCreated = new List<BaseEntity>();
            public List<GameObject> _arenaTriggers = new List<GameObject>();
            private List<HackableLockedCrate> hackableLockedCrates = new List<HackableLockedCrate>();
            public Dictionary<ulong, PlayerData> playerData = new Dictionary<ulong, PlayerData>();
            public List<BaseEntity> tempLootBoxes = new();
            public Mesh meshObject;
            public bool isEnding = false;
            private string TopBarUIText;

            public void Initialize(MazeArenaSetting setting)
            {
                arena = setting;
                config = ins.config;
                ins.maze = this;
                ins.mazeData = new MazeData();
                TopBarUIText = ins.GetTimeRemeaningText();
                if (autoStartCoroutine != null)
                {
                    Debug("Stoping auto start routine from Initialize");
                    ServerMgr.Instance.StopCoroutine(autoStartCoroutine);
                    autoStartCoroutine = null;
                }
                Debug("Starting Maze Arena from Initialize");
            }

            private void Start()
            {
                ins.SubscribeEventHooks();
                DateTime timeNow = DateTime.Now;
                Interface.CallHook("OnMazeStarts", arena.Name, timeNow);
                StartCoroutine(MazeStart());
            }

            private void OnDestroy()
            {
                
            }

            private IEnumerator MazeStart()
            {
                yield return StartCoroutine(CleanArena(true));
                yield return StartCoroutine(AddDoors());
                yield return StartCoroutine(PasteCopyMaze());
                yield return StartCoroutine(CreateMazeTrigger());
                yield return StartCoroutine(CreateMarker());
                yield return StartCoroutine(CreateMazeTriggerArena()); //new
                yield return StartCoroutine(MazeEventNoitifyTimer());
            }

            public IEnumerator MazeEnd()
            {
                if (isEnding) yield break;

                isEnding = true;

                yield return StartCoroutine(RemoveDoors());
                yield return StartCoroutine(AddDoors());
                yield return StartCoroutine(CleanArena());
                yield return StartCoroutine(CloseUI());
                yield return StartCoroutine(DestoryMazeTrigger());
                yield return StartCoroutine(DestoryMazeTriggerArena()); // new
                yield return StartCoroutine(DistroyMarker());
                BroadCastMessage(ins.GetMessage("EventStop"));
                Debug("Stoping maze event");

                yield return StartCoroutine(FindLastStandingWinner());

                yield return StartCoroutine(CallHook());
                yield return StartCoroutine(GiveCommandRewards());
                //yield return StartCoroutine(SpawnRewards());


                yield return StartCoroutine(SendDiscordMessage());
                //yield return StartCoroutine(TeleportOldPlayer());
                //yield return StartCoroutine(RemoveTempData());

                //yield return StartCoroutine(TeleportWinners(winnerList));
                ins.UnsubscribeEventHooks();

                if (config.rewards.spawnLootBox)
                {
                    yield return StartCoroutine(MoveEntitiesToBoxes(arena));
                    BroadCastMessage(ins.GetMessage("LootBoxesTime", config.rewards.lootBoxTime));
                    yield return new WaitForSeconds(config.rewards.lootBoxTime);
                }

                //yield return StartCoroutine(KillCrates());
                //yield return StartCoroutine(CleanArena());
                yield return StartCoroutine(RemoveDoors());
                yield return StartCoroutine(KillTempBoxes());
                yield return StartCoroutine(ClearR());

                if (ins.maze != null)
                {
                    GameObject.Destroy(ins.maze.gameObject);
                    ins.maze = null;
                    ins.mazeData = null;
                }

                if (config.holdAutoMaze)
                {
                    Debug("starting auto start routine after maze end");
                    ins.StartAutoStartRoutine();
                }
            }

            public void TryForceStop()
            {
                Debug("Trying force");
                if (isEnding) return;
                StartCoroutine(MazeForceStop());
            }

            private IEnumerator MazeForceStop()
            {
                Debug("Trying force end maze event");
                if (isEnding) yield break;

                isEnding = true;

                yield return StartCoroutine(RemoveDoors());
                yield return StartCoroutine(CleanArena(true));
                yield return StartCoroutine(CloseUI());
                yield return StartCoroutine(DestoryMazeTrigger());
                yield return StartCoroutine(DestoryMazeTriggerArena()); // new
                yield return StartCoroutine(DistroyMarker());

                BroadCastMessage(ins.GetMessage("EventStop"));
                Debug("Force stoping maze event");
                ins.UnsubscribeEventHooks();
                //yield return StartCoroutine(RemoveTempData());
                yield return StartCoroutine(ClearR());

                if (ins.maze != null)
                {
                    ins.mazeData = null;
                    GameObject.Destroy(ins.maze.gameObject);
                    ins.maze = null;                
                }

                yield return null;
            }

            #region Starts

            private IEnumerator AddDoors()
            {
                foreach (var loc in arena.Doors)
                {
                    var door = GameManager.server.CreateEntity(config.mazeDoorPrefab, loc.Position);
                    door.Spawn();
                    var c = door as BaseCombatEntity;
                    c.SetMaxHealth(99999);
                    c.SetHealth(999999);

                    door.transform.Rotate(loc.Rotation);
                    door.OwnerID = 0;
                    door.SendNetworkUpdateImmediate(true);

                    doorsCreated.Add(door);
                    yield return null;
                }
                Debug("Adding doors " + arena.Doors.Count);
                yield break;
            }

            public IEnumerator PasteCopyMaze()
            {
                foreach (var item in arena.Walls)
                {
                    BaseEntity entity = GameManager.server.CreateEntity(item.WallName, item.Position, default);
                    entity.Spawn();
                    entity.transform.Rotate(item.Rotation);
                    var c = entity as BaseCombatEntity;
                    c.OwnerID = 0;
                    c.SetMaxHealth(99999);
                    c.SetHealth(999999);
                    c.SendNetworkUpdateImmediate(true);
                    yield return null;
                }
                Debug("Pasting maze walls " + arena.Walls.Count);
            }

            private IEnumerator CreateMarker()
            {
                VendingMachineMapMarker vending = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", arena.Center).GetComponent<VendingMachineMapMarker>();
                vending.enableSaving = false;
                vending.markerShopName = config.marker.markerDisplayName;
                vending.Spawn();

                MapMarkerGenericRadius generic = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab").GetComponent<MapMarkerGenericRadius>();
                generic.color1 = ins.HexToUnityColor(config.marker.markerColor);
                generic.color2 = ins.HexToUnityColor(config.marker.markerColor);
                generic.alpha = config.marker.markerTransparency;
                generic.radius = config.marker.markerRadius;
                generic.enableSaving = false;
                generic.SetParent(vending);
                generic.Spawn();

                vending.SendNetworkUpdate();
                generic.SendUpdate();

                MapMarkerList.Add(generic);
                MapVendingList.Add(vending);
                Debug("Creating markers");
                yield return null;
            }

            private IEnumerator CreateMazeTriggerArena()
            {
                gameObject.transform.position = arena.Center;
                gameObject.layer = (int)Layer.Reserved1;
                SphereCollider collider = gameObject.AddComponent<SphereCollider>();
                collider.radius = arena.radius;
                collider.isTrigger = true;
                gameObject.AddComponent<TriggerBase>().InterestLayers = (int)Rust.Layer.Player_Server;

                yield return null;
            }

            #endregion

            #region Stop

            private IEnumerator DestoryMazeTriggerArena()
            {
                Debug("Destroying maze trigger arena full");
                Destroy(gameObject.GetComponent<SphereCollider>());
                Destroy(gameObject.GetComponent<TriggerBase>());

                yield return null;
            }

            private IEnumerator DestoryMazeTrigger()
            {
                Debug("Destroying maze trigger inside mesh");
                foreach (GameObject go in _arenaTriggers)
                {
                    UnityEngine.GameObject.DestroyImmediate(go);
                    yield return null;
                }
            }

            /*private IEnumerator TeleportWinners(HashSet<ulong> winnerList)
            {
                if (!config.saveTPLoc) 
                    yield break;

                if (winnerList.Count < 1)
                {
                    yield break;
                }

                HashSet<ulong> winners = new HashSet<ulong>(winnerList);
                Debug("Teleporing winners count " + winners.Count);

                foreach (var win in BasePlayer.activePlayerList)
                {
                    if (winners.Contains(win.userID))
                    {
                        ins.CM(win, ins.GetMessage("WinningPlayers", config.tpwinnermin));
                    }
                }

                float time = config.tpwinnermin;
                ins.Puts("Waiting for teleport completes for winners before ending the event");
                yield return new WaitForSeconds(time);

                foreach (var player in InMazeArena)
                {
                    try
                    {
                        if (winners.Contains(player.userID) && InMazeArenaRadius(player))
                        {
                            if (ins.OldLocation.TryGetValue(player.userID, out Vector3 pos))
                            {
                                ins.CM(player, "Teleporting back");
                                ins.TeleportPlayer(player, pos);
                            }
                        }
                    }catch { }
                }

                ins.OldLocation.Clear();
                winners.Clear();
            }*/

            private IEnumerator KillTempBoxes()
            {
                if (tempLootBoxes.Count < 1)
                    yield break;
                foreach (var item in tempLootBoxes)
                {
                    if (item.IsValid())
                    {
                        item.Kill();
                    }
                    yield return null;
                }
                tempLootBoxes.Clear();
                yield break;
            }

            private IEnumerator KillCrates()
            {
                if (hackableLockedCrates.Count < 1)
                    yield break;

                foreach (var ent in hackableLockedCrates)
                {
                    if (ent != null)
                    {
                        ent.Kill();
                    }
                    yield return null;
                }
                hackableLockedCrates.Clear();
                yield break;
            }

            private IEnumerator RemoveDoors()
            {
                if (doorsCreated.Count < 1)
                    yield break;
                Debug("Removing doors");
                foreach (var item in doorsCreated)
                {
                    if (item.IsValid())
                    {
                        item.Kill();
                    }

                    yield return null;
                }
            }

            private IEnumerator FindLastStandingWinner()
            {
                Debug("Finding last standing winner");
                string winningClanName = string.Empty;
                HashSet<string> uniqueClans = new HashSet<string>();

                Dictionary<string, int> clanKills = new Dictionary<string, int>();

                foreach (var player in PlayingList)
                {
                    if (player != null && player.IsConnected)
                    {
                        string clanTag = ins.GetPlayerClanTag(player.userID);
                        if (!string.IsNullOrEmpty(clanTag))
                        {
                            uniqueClans.Add(clanTag);
                        }
                    }
                    yield return null;
                }

                if (uniqueClans.Count == 1)
                {
                    winningClanName = uniqueClans.First();
                }
                else if (uniqueClans.Count > 1)
                {
                    foreach (string clanTag in uniqueClans)
                    {
                        int kills = ins.GetClanTotalKills(clanTag);
                        clanKills[clanTag] = kills;
                        yield return null;
                    }

                    winningClanName = clanKills.OrderByDescending(x => x.Value).First().Key;
                }
                Debug("Winner found " + winningClanName);
                StartCoroutine(SendParticipentsMsg(winningClanName));
            }

            private IEnumerator FindClanWinner()
            {
                KeyValuePair<string, int> winningClanName = ins.GetTopClanByKills();

                StartCoroutine(SendParticipentsMsg(winningClanName.Key));
                yield return null;
            }

            private IEnumerator CallHook()
            {
                if (winnerList.Count < 1)
                {
                    yield break;
                }
                Debug("Calling hook");
                DateTime timeNow = DateTime.Now;
                Interface.CallHook("OnMazeWinnerAnnounce", winnerList, timeNow);

                yield return null;
            }

            /*private IEnumerator TeleportOldPlayer()
            {
                if (!config.saveTPLoc)
                {
                    yield break;
                }

                Debug("Teleporting old players");
                foreach (var player in InMazeArena) //changed
                {
                    try
                    {
                        if (winnerList.Contains(player.userID))
                        {
                            continue;
                        };

                        if (!InMazeArenaRadius(player))
                        {
                            continue;
                        }

                        if (ins.OldLocation.TryGetValue(player.userID, out Vector3 pos))
                        {
                            ins.TeleportPlayer(player, pos);
                        }
                    }catch { }

                    yield return null;
                }
            }*/

            private bool InMazeArenaRadius(BasePlayer player)
            {
                if (player == null) return false;

                float Distance = Vector3.Distance(player.GetNetworkPosition(), arena.Center);

                if (Distance < arena.radius)
                    return true;

                return false;
            }

            private IEnumerator CloseUI()
            {
                Debug("Closing all uis");
                foreach (var player in InMazeArena)
                {
                    CuiHelper.DestroyUi(player, "MAZE");
                    CuiHelper.DestroyUi(player, "GGStatsUI");
                }
                yield return null;
            }

            #endregion

            #region TempMethods

            private IEnumerator CleanArena(bool clearAll = false)
            {
                var entities = Pool.Get<List<BaseEntity>>();
                entities.Clear();
                int mask = clearAll ? ins.MASKA : ins.MASK;
                Vis.Entities(arena.Center, arena.radius, entities, mask);

                foreach (var item in entities)
                {
                    if (item.IsValid())
                    {
                        item.Kill();
                    }
                    yield return null;
                }
                entities.Clear();
                Pool.FreeUnmanaged(ref entities);
                Debug("Cleaning arena");
                yield return null;
            }

            private IEnumerator RemoveTempData()
            {
                Debug("Removing tempdata");
                yield return new WaitForSeconds(1);

                ins.maze.playerData.Clear();
                yield return null;
            }

            private IEnumerator ClearR()
            {
                Debug("Clearing all datas and values");
                ins.TeleportStart = false;
                ins.TempTotalDamages = 0;
                ins.TempTotalKills = 0;
                ins.maze.playerData.Clear();
                doorsCreated.Clear();
                MapVendingList.Clear();
                MapMarkerList.Clear();
                PlayingList.Clear();
                InMazeArena.Clear();
                participents.Clear();
                wallsCreated.Clear();
                DestoryMesh(meshObject);
                ClearMeshObject(_arenaTriggers.First());
                _arenaTriggers.Clear();
                //ins.OldLocation.Clear();
                isEnding = false;

                yield return null;
            }

            #endregion

            #region Timers

            private IEnumerator MazeEventNoitifyTimer()
            {
                if (config.mazeNotifyTimer >= 1)
                {
                    BroadCastMessage(ins.GetMessage("EventStartingNotify", config.mazeNotifyTimer.ToString()));
                }

                int timeRemaining = config.mazeNotifyTimer;
                Debug("Starts notify timer " + timeRemaining);

                while (timeRemaining > 0)
                {
                    yield return new WaitForSeconds(1);
                    timeRemaining--;

                    switch (timeRemaining)
                    {
                        case 30:
                            BroadCastMessage(ins.GetMessage("EventStartingIn", 30));
                            break;
                        case 10:
                            BroadCastMessage(ins.GetMessage("EventStartingIn", 10));
                            break;
                        case 5:
                            BroadCastMessage(ins.GetMessage("EventStartingIn", 5));
                            break;
                        case 3:
                            BroadCastMessage(ins.GetMessage("EventStartingIn", 3));
                            break;
                        case 2:
                            BroadCastMessage(ins.GetMessage("EventStartingIn", 2));
                            break;
                        case 1:
                            BroadCastMessage(ins.GetMessage("EventStartingIn", 1));
                            break;
                    }
                }

                BroadCastMessage(ins.GetMessage("EventStarted"));
                ins.TeleportStart = true;
                StartCoroutine(MazeEventDoorOpeningTimer());
            }

            private IEnumerator MazeEventDoorOpeningTimer()
            {
                if (config.mazeDoorOpenTimer > 0)
                {
                    BroadCastMessage(ins.GetMessage("EventDoorOpening", config.mazeDoorOpenTimer));
                }

                int timeRemaining = config.mazeDoorOpenTimer;
                Debug("Starts opening timer " + timeRemaining);

                while (timeRemaining > 0)
                {
                    yield return new WaitForSeconds(1);
                    timeRemaining--;

                    foreach(var player in InMazeArena)
                    {
                        try
                        {
                            ins.UpdateTopBarUI(player, TopBarUIText, timeRemaining, "Door Opening In: {TIME}".Replace("{TIME}", GetFormatTime(timeRemaining)));
                        }
                        catch { }
                    }

                    switch (timeRemaining)
                    {
                        case 30:
                            BroadCastMessage(ins.GetMessage("EventDoorOpening", 30));
                            break;
                        case 15:
                            BroadCastMessage(ins.GetMessage("EventDoorOpening", 15));
                            break;
                        case 10:
                            BroadCastMessage(ins.GetMessage("EventDoorOpening", 10));
                            break;
                        case 5:
                            BroadCastMessage(ins.GetMessage("EventDoorOpening", 5));
                            break;
                        case 3:
                            BroadCastMessage(ins.GetMessage("EventDoorOpening", 3));
                            break;
                        case 2:
                            BroadCastMessage(ins.GetMessage("EventDoorOpening", 2));
                            break;
                        case 1:
                            BroadCastMessage(ins.GetMessage("EventDoorOpening", 1));
                            break;
                    }
                }

                StartCoroutine(AnimateDoorCheck(3, 4, true));
                BroadCastMessage(ins.GetMessage("EventDoorOpned"));
                StartCoroutine(MazeEventDoorClosing());
            }

            private IEnumerator MazeEventDoorClosing()
            {
                int timeRemaining = config.mazeDoorClosingTimer;
                Debug("Starts door closing timer " + timeRemaining);

                while (timeRemaining > 0)
                {
                    yield return new WaitForSeconds(1);
                    timeRemaining--;

                    foreach (var player in InMazeArena)
                    {
                        try
                        {
                            ins.UpdateTopBarUI(player, TopBarUIText, timeRemaining, $"Door Closing In: {GetFormatTime(timeRemaining)}");
                            ins.UpdateRunningStatusUI(player);
                        }
                        catch { }
                    }

                    switch (timeRemaining)
                    {
                        case 60:
                            BroadCastMessage(ins.GetMessage("EventDoorClosing", 60));
                            break;
                        case 30:
                            BroadCastMessage(ins.GetMessage("EventDoorClosing", 30));
                            break;
                        case 15:
                            ins.TeleportStart = false;
                            BroadCastMessage(ins.GetMessage("EventDoorClosing", 15));
                            break;
                        case 10:
                            BroadCastMessage(ins.GetMessage("EventDoorClosing", 10));
                            break;
                        case 5:
                            BroadCastMessage(ins.GetMessage("EventDoorClosing", 5));
                            break;
                        case 4:
                            StartCoroutine(AnimateDoorCheck(3, 4, false));
                            break;
                        case 3:
                            BroadCastMessage(ins.GetMessage("EventDoorClosing", 3));
                            break;
                        case 2:
                            BroadCastMessage(ins.GetMessage("EventDoorClosing", 2));
                            break;
                        case 1:
                            BroadCastMessage(ins.GetMessage("EventDoorClosing", 1));
                            break;
                    }
                }

                BroadCastMessage(ins.GetMessage("EventDoorClosed"));
                bool winFound = WinnerFound();
                Debug("Winner found? " + winFound.ToString());
                //CheckforWinner(arena);
                if (!winFound)
                {
                    Debug("No winner found, starting maze shrinking");
                    StartCoroutine(ShrinkZoneTimer());
                }
                else
                {
                    Debug("Winner found, ending maze");
                    StartCoroutine(MazeEnd());
                    yield break;
                }
            }

            private IEnumerator ShrinkZoneTimer()
            {
                int countRemoval = config.mazeShrinkAmount;
                int timeRemaining = config.mazeRemoveWallTimer;
                int initialRadius = config.mazeRemoveWallRadius;

                int totalDuration = countRemoval * timeRemaining;
                Debug("starting wall removing");

                while (countRemoval > 0)
                {
                    yield return new WaitForSeconds(1);

                    if (isEnding) yield break;

                    timeRemaining--;

                    //CheckforWinner(arena);

                    if (WinnerFound())
                    {
                        StartCoroutine(MazeEnd());
                        yield break;
                    }

                    foreach (var player in InMazeArena)
                    {
                        try
                        {
                            ins.UpdateTopBarUI(player, TopBarUIText, timeRemaining, "Removing Outer Walls In: {TIME}".Replace("{TIME}", GetFormatTime(timeRemaining)));
                            ins.UpdateRunningStatusUI(player);
                        }
                        catch { }
                    }

                    if (timeRemaining <= 10 && timeRemaining > 0)
                    {
                        BroadCastMessage(ins.GetMessage("EventRemovingWalls", timeRemaining));
                    }

                    if (timeRemaining <= 0)
                    {
                        StartCoroutine(RemoveOutterWalls(initialRadius));
                        initialRadius += initialRadius;
                        countRemoval--;
                        //CheckforWinner(arena);

                        if (countRemoval <= 0 && !isEnding)
                        {
                            StartCoroutine(WaitingLastTeamStanding());
                            yield break;
                        }

                        timeRemaining = config.mazeRemoveWallTimer;
                    }
                }
            }

            private IEnumerator WaitingLastTeamStanding()
            {
                yield return StartCoroutine(CleanArena());
                if (isEnding) yield break;
                Debug("Waiting for last team standing");
                bool showUI = false;

                while (!WinnerFound())
                {
                    yield return new WaitForSeconds(1);

                    if (isEnding) yield break;
                    //play for a sec

                    if (!showUI)
                    {
                        foreach (var player in InMazeArena)
                        {
                            try
                            {
                                ins.UpdateTopBarUI(player, TopBarUIText, 0, "Fight it out");
                                ins.UpdateRunningStatusUI(player);
                                ins.SendTip(player, "Fight it out!", 5);
                            }
                            catch { }
                        }
                        showUI = true;
                    }
                    Debug("Waiting for last team standing++");
                }

                StartCoroutine(MazeEnd());
            }

            #endregion

            #region Triggers

            private void OnTriggerEnter(Collider collider)
            {
                if (!collider)
                    return;

                if (!collider.TryGetComponent<BasePlayer>(out var player))
                    return;

                if (!player.userID.IsSteamId())
                    return;

                InMazeArena.Add(player);
                ins.CreateMazeTopBarUI(player);
                ins.CreateRunningStatsUI(player);
            }

            private void OnTriggerExit(Collider collider)
            {
                if (!collider)
                    return;

                if (!collider.TryGetComponent<BasePlayer>(out var player))
                    return;

                if (!player.userID.IsSteamId())
                    return;

                InMazeArena.Remove(player);
                RemoveUI(player);
            }

            private class ArenaTriggerMaze : MonoBehaviour
            {
                public MazeEvent mazeEvent;

                private void OnTriggerEnter(Collider col)
                {
                    if (!col)
                        return;

                    if (!col.TryGetComponent<BasePlayer>(out var player))
                        return;

                    if (!player.userID.IsSteamId())
                        return;

                    if (IsVanished(player))
                    {
                        ins.SendTip(player, "You can't join while vanished", 3);
                        return;
                    }
                    mazeEvent.PlayingList.Add(player);
                    player.MarkHostileFor(0);
                    ins.SendTip(player, "Joined the arena", 2);
                }

                private void OnTriggerExit(Collider col)
                {
                    if (!col)
                        return;

                    if (!col.TryGetComponent<BasePlayer>(out var player))
                        return;

                    if (!player.userID.IsSteamId())
                        return;

                    mazeEvent.PlayingList.Remove(player);
                    player.MarkHostileFor(0);
                    ins.SendTip(player, "Left the arena", 1);
                }
            }

            

            #endregion

            #region Methods 

            public void RemoveUI(BasePlayer player)
            {
                try
                {
                    CuiHelper.DestroyUi(player, "MAZE");
                    CuiHelper.DestroyUi(player, "GGStatsUI");
                }
                catch { }
            }

            private void ClearMeshObject(GameObject parentObject)
            {
                if (parentObject != null)
                {
                    MeshFilter meshFilter = parentObject.GetComponent<MeshFilter>();
                    if (meshFilter != null)
                    {
                        Destroy(meshFilter.mesh);
                    }
                    MeshCollider meshCollider = parentObject.GetComponent<MeshCollider>();
                    if (meshCollider != null)
                    {
                        Destroy(meshCollider.sharedMesh);
                    }
                }
            }

            private void DestoryMesh(Mesh mesh)
            {
                if (mesh != null)
                {
                    Debug("cleaning mesh object");
                    Destroy(mesh);
                }
            }

            private IEnumerator CreateMazeTrigger()
            {
                GameObject parentObject = new GameObject("MazeArena");

                /*foreach (var point in arena.Corners)
                {
                    CreateChildObject("Point", point, parentObject.transform);
                    yield return null;
                }*/

                Vector3 parentPosition = GetCenterPoint(arena.Corners);
                parentObject.transform.position = parentPosition;
                parentObject.gameObject.layer = (int)Rust.Layer.Reserved1;
                ArenaTriggerMaze triggerMaze = parentObject.AddComponent<ArenaTriggerMaze>();
                triggerMaze.mazeEvent = this;

                meshObject = ins.GenerateMeshRandomVec(parentPosition, arena.Corners, arena.meshHeight);

                MeshCollider meshCollider = parentObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = meshObject;
                meshCollider.convex = true;
                meshCollider.isTrigger = true;

                parentObject.AddComponent<TriggerBase>().InterestLayers = (int)Rust.Layer.Player_Server;

                _arenaTriggers.Add(parentObject);
                Debug("Creating Maze trigger");
                yield return null;
            }

            private static void CreateChildObject(string name, Vector3 position, Transform parent)
            {
                GameObject child = new GameObject(name);
                child.transform.position = position;
                child.transform.parent = parent;
            }

            private static Vector3 GetCenterPoint(List<Vector3> points)
            {
                Vector3 center = Vector3.zero;
                foreach (var point in points)
                {
                    center += point;
                }
                return center / points.Count;
            }

            private void BroadCastMessage(string message)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    ins.CM(player, message);
                }
            }

            private IEnumerator AnimateDoorCheck(float distance, float duration, bool upwardMove = true)
            {
                Debug("Door animation starts");
                float interval = 0.20f;
                int totalSteps = Mathf.CeilToInt(duration / interval);

                var animations = new List<DoorAnimation>();

                foreach (BaseEntity door in doorsCreated)
                {
                    Vector3 startPosition = door.transform.position;
                    Vector3 endPosition = upwardMove ? startPosition + new Vector3(0, distance, 0) : startPosition - new Vector3(0, distance, 0);
                    animations.Add(new DoorAnimation { Door = door, StartPosition = startPosition, EndPosition = endPosition, Duration = duration, Interval = interval });
                }

                float elapsedTime = 0f;

                while (elapsedTime < duration)
                {
                    elapsedTime += interval;

                    foreach (var anim in animations)
                    {
                        float t = Mathf.SmoothStep(0f, 1f, (elapsedTime / anim.Duration));
                        anim.Door.transform.position = Vector3.Lerp(anim.StartPosition, anim.EndPosition, t);
                        anim.Door.SendNetworkUpdateImmediate();
                    }

                    yield return new WaitForSeconds(interval);
                }

                foreach (var anim in animations)
                {
                    anim.Door.transform.position = anim.EndPosition;
                    anim.Door.SendNetworkUpdateImmediate();
                }
            }

            private class DoorAnimation
            {
                public BaseEntity Door { get; set; }
                public Vector3 StartPosition { get; set; }
                public Vector3 EndPosition { get; set; }
                public float Duration { get; set; }
                public float Interval { get; set; }
            }


            /*private IEnumerator SpawnRewards()
            {
                if (!config.rewards.spawnhackable || (config.rewards.spawnhackable && config.rewards.spawnhackableAmount < 1))
                {
                    yield break;
                }

                if (winnerList.Count < 1)
                {
                    yield break;
                }

                Debug("Spawning rewards");
                for (int i = 0; i < config.rewards.spawnhackableAmount; i++)
                {
                    var ent = GameManager.server.CreateEntity("assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab", arena.Center, new Quaternion(), true);
                    HackableLockedCrate crate = ent.GetComponent<HackableLockedCrate>();
                    crate.hackSeconds = HackableLockedCrate.requiredHackSeconds - 0f;
                    crate.Spawn();
                    hackableLockedCrates.Add(crate);

                    yield return null;
                }
            }*/

            private bool WinnerFound()
            {
                if (PlayingList.Count == 0 || PlayingList.Count == 1 || ins.CountUniqueClans(PlayingList) <= 1)
                    return true;
                return false;
            }

            private IEnumerator DistroyMarker()
            {
                foreach (var item in MapMarkerList)
                {
                    if (item.IsValid())
                        item.Kill();
                }
                foreach (var item in MapVendingList)
                {
                    if (item.IsValid())
                        item.Kill();
                }
                Debug("destroying markers");
                yield return null;
            }

            private IEnumerator RemoveOutterWalls(int radius)
            {
                List<BaseEntity> entities = new List<BaseEntity>();

                Vis.Entities(arena.Center, arena.wallRadius, entities, ins.MASK);

                float innerRadius = arena.wallRadius - radius;

                int chunkSize = 10;
                int totalEntities = entities.Count;

                for (int i = 0; i < totalEntities; i += chunkSize)
                {
                    for (int j = i; j < Mathf.Min(i + chunkSize, totalEntities); j++)
                    {
                        var item = entities[j];
                        if (Vector3.Distance(item.GetNetworkPosition(), arena.Center) <= innerRadius) continue;
                        if (item.IsValid()) item.Kill();
                    }

                    yield return null;
                }
                Debug("removing outerwalls");
                entities.Clear();
            }

            private IEnumerator SendParticipentsMsg(string winningClanName)
            {
                if (!string.IsNullOrEmpty(winningClanName))
                {
                    Debug("sending participents message " + winningClanName);
                    Dictionary<ulong, PlayerData> winningClanData = ins.GetAllPlayersByClanTag(winningClanName);

                    string playerStats = "";

                    int clanTotalKill = 0;
                    int clanTotalDeaths = 0;
                    double clanTotalKDR = 0;
                    float clanTotalDamage = 0;
                    int clanTotalHeadshots = 0;

                    foreach (var playerEntry in winningClanData)
                    {
                        ulong playerID = playerEntry.Key;
                        PlayerData playerData = playerEntry.Value;

                        int kills = playerData.PlayerKills;
                        int shotsFired = playerData.ShotsFired;
                        int bodyHit = playerData.ShotsHit;
                        int headshots = playerData.HeadShots;
                        int deaths = playerData.Deaths;
                        float damages = playerData.DamageDealt;

                        double accuracy = (shotsFired > 0) ? (double)(bodyHit + headshots) / shotsFired * 100 : 0;
                        double headshotAccuracy = (bodyHit > 0) ? (double)headshots / (bodyHit + headshots) * 100 : 0;
                        double kdr = (deaths > 0) ? (double)kills / deaths : kills;

                        clanTotalKill += kills;
                        clanTotalDeaths += deaths;
                        clanTotalDamage += damages;
                        clanTotalHeadshots += headshots;

                        string playerName = ins.covalence.Players.FindPlayer(playerID.ToString()).Name;
                        winnerList.Add(playerID);

                        playerStats += $"\n <color=#FFD700>{playerName}</color> Kills: {kills}, Deaths: {deaths}, KDR: {kdr:F2}\n Damage: {damages:F2}, Headshots: {headshots}";
                    }

                    clanTotalKDR = (clanTotalDeaths > 0) ? clanTotalKill / clanTotalDeaths : clanTotalKill;

                    string message = string.Join("\n", config.chat.WinnerMsg);
                    message = message.Replace("{WinningClanName}", winningClanName);
                    message = message.Replace("{PlayerStats}", playerStats);
                    message = message.Replace("{ClanTotalKills}", clanTotalKill.ToString());
                    message = message.Replace("{ClanTotalDeaths}", clanTotalDeaths.ToString());
                    message = message.Replace("{ClanTotalKDR}", clanTotalKDR.ToString("F2"));
                    message = message.Replace("{ClanTotalDamage}", clanTotalDamage.ToString("F2"));
                    message = message.Replace("{ClanTotalHeadshots}", clanTotalHeadshots.ToString());
                    message = message.Replace("{TotalKills}", ins.TempTotalKills.ToString());
                    message = message.Replace("{TotalDamages}", ins.TempTotalDamages.ToString("F2"));
                    message = message.Replace("{Participents}", participents.Count.ToString());

                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        try
                        {
                            Debug("m , ee " + winnerList.Count);
                            ins.CM(player, message);
                        }
                        catch { }
                        yield return null;
                    }
                }
                else
                {
                    Debug("No winner found");
                    BroadCastMessage(ins.GetMessage("EventNoWinner"));
                }

                foreach (var p in participents)
                {
                    BasePlayer player = BasePlayer.FindByID(p);

                    if (player != null && player.IsConnected && player.IsValid())
                    {
                        PlayerData playerData = ins.FindPlayerData(player);

                        int killsP = playerData.PlayerKills;
                        int shotsFiredP = playerData.ShotsFired;
                        int bodyHitP = playerData.ShotsHit;
                        int headshotsP = playerData.HeadShots;
                        int deathsP = playerData.Deaths;
                        float damagesP = playerData.DamageDealt;

                        double accuracyP = (shotsFiredP > 0) ? (double)(bodyHitP + headshotsP) / shotsFiredP * 100 : 0;
                        double headshotAccuracyP = (bodyHitP > 0) ? (double)headshotsP / (bodyHitP + headshotsP) * 100 : 0;
                        double kdrP = (deathsP > 0) ? (double)killsP / deathsP : killsP;

                        string messageP = string.Join("\n", config.chat.ParticipentsMessage);
                        messageP = messageP.Replace("{Kills}", killsP.ToString());
                        messageP = messageP.Replace("{Deaths}", deathsP.ToString());
                        messageP = messageP.Replace("{KDR}", kdrP.ToString("F1"));
                        messageP = messageP.Replace("{Damages}", damagesP.ToString("F1"));
                        messageP = messageP.Replace("{Headshots}", headshotsP.ToString());
                        messageP = messageP.Replace("{Accuracy}", accuracyP.ToString("F1"));
                        messageP = messageP.Replace("{HeadshotAccuracy}", headshotAccuracyP.ToString("F1"));

                        try
                        {
                            ins.CM(player, messageP);
                        }
                        catch { }

                    }
                    yield return null;
                }
            }

            private IEnumerator GiveCommandRewards()
            {
                if (winnerList.Count < 1 || !config.rewards.commandRewards || (config.rewards.commandRewards && config.rewards.listRewards.Count < 1))
                {
                    yield break;
                }

                Debug("Giving command rewards");
                if (config.rewards.rewardWinClanPlayer)
                {
                    BasePlayer p = ins.GetTop1PlayersWithMostKillsFromClan(ins.GetPlayerClanTag(winnerList.First()));

                    if (p != null)
                    {
                        foreach (var command in config.rewards.listRewards)
                        {
                            string cmd = command.ToString().Replace("{PLAYER}", p.userID.ToString());
                            ins.Server.Command(cmd);
                            yield return null;
                        }
                    }
                    yield break;
                }

                foreach (var playerId in winnerList)
                {
                    foreach (var command in config.rewards.listRewards)
                    {
                        string cmd = command.ToString().Replace("{PLAYER}", playerId.ToString());
                        ins.Server.Command(cmd);
                        yield return null;
                    }
                }
            }

            private IEnumerator SendDiscordMessage()
            {
                if (winnerList.Count < 1)
                {
                    yield break;
                }

                if (config.discord.enableWebhook && !string.IsNullOrEmpty(config.discord.discordWebhook))
                {
                    Debug("Sending discord message");
                    string clanName = ins.GetPlayerClanTag(winnerList.First());

                    if (string.IsNullOrEmpty(clanName))
                    {
                        Debug("No Clan data found for the winners. Not sending Discord Webhook Message.");
                        ins.Puts("No Clan data found for the winners. Not sending Discord Webhook Message.");
                        yield break;
                    }

                    Dictionary<ulong, PlayerData> winningClan = ins.GetAllPlayersByClanTag(clanName);
                    if (winningClan == null)
                    {
                        Debug("No Clan data found for the winners. Not sending Discord Webhook Message.");
                        ins.Puts("No Clan data found for the winners. Not sending Discord Webhook Message.");
                        yield break;
                    }

                    int clanTotalKill = 0;
                    int clanTotalDeaths = 0;
                    double clanTotalKDR = 0;
                    float clanTotalDamage = 0;
                    int clanTotalHeadshots = 0;
                    string members = string.Empty;

                    foreach (var playerEntry in winningClan)
                    {
                        ulong playerID = playerEntry.Key;
                        PlayerData playerData = playerEntry.Value;

                        int kills = playerData.PlayerKills;
                        int shotsFired = playerData.ShotsFired;
                        int bodyHit = playerData.ShotsHit;
                        int headshots = playerData.HeadShots;
                        int deaths = playerData.Deaths;
                        float damages = playerData.DamageDealt;

                        double accuracy = (shotsFired > 0) ? (double)(bodyHit + headshots) / shotsFired * 100 : 0;
                        double headshotAccuracy = (bodyHit > 0) ? (double)headshots / (bodyHit + headshots) * 100 : 0;
                        double kdr = (deaths > 0) ? (double)kills / deaths : kills;

                        clanTotalKill += kills;
                        clanTotalDeaths += deaths;
                        clanTotalDamage += damages;
                        clanTotalHeadshots += headshots;

                        members += $"[{clanName}] [{GetPlayerName(playerID)}](https://steamcommunity.com/profiles/{playerID}) - `{kills} kills - {deaths} deaths\n`";
                    }

                    clanTotalKDR = (clanTotalDeaths > 0) ? clanTotalKill / clanTotalDeaths : clanTotalKill;

                    string message = string.Join("\n", config.discord.messageWebhook);
                    message = message.Replace("{WinnerClanName}", clanName);
                    message = message.Replace("{ClanKills}", clanTotalKill.ToString());
                    message = message.Replace("{ClanDeaths}", clanTotalDeaths.ToString());
                    message = message.Replace("{ClanKDR}", clanTotalKDR.ToString("F2"));
                    message = message.Replace("{ClanDamages}", clanTotalDamage.ToString("F1"));
                    message = message.Replace("{ClanHeadshots}", clanTotalHeadshots.ToString());
                    message = message.Replace("{TotalKills}", ins.TempTotalKills.ToString());
                    message = message.Replace("{TotalDamages}", ins.TempTotalDamages.ToString());
                    message = message.Replace("{TotalTeams}", ins.CountUniqueClans(PlayingList).ToString());
                    message = message.Replace("{Participents}", participents.Count.ToString());
                    message = message.Replace("{MembersStats}", members);

                    ins.SendDiscordMessage(config.discord.discordWebhook, "", new List<string> { message }, false);
                }

                yield break;
            }

            private string GetPlayerName(ulong id)
            {
                return ins.covalence.Players.FindPlayer(id.ToString()).Name;
            }

            #endregion

        }


        #endregion

        #region Helpers

        private void CancelDamage(HitInfo hit)
        {
            hit.damageTypes = new DamageTypeList();
            hit.DidHit = false;
            hit.DoHitEffects = false;
            hit.damageTypes.ScaleAll(0);
        }

        private static bool IsVanished(BasePlayer player)
        {
            if (ins.Vanish != null && (bool)ins.Vanish.Call("IsInvisible", player)) return true;
            return false;
        }

        private static void Debug(string message)
        {
            if (!ins.DebugBol) return;
            ins.Puts(message);
        }

        private string GetFormatTime(float time)
        {
            TimeSpan dateDifference = TimeSpan.FromSeconds(time);
            int days = dateDifference.Days;
            int hours = dateDifference.Hours;
            int mins = dateDifference.Minutes;
            int secs = dateDifference.Seconds;

            if (days > 0)
                return $"{days} day{(days > 1 ? "s" : string.Empty)}, {hours} hour{(hours > 1 ? "s" : string.Empty)}";
            if (hours > 0)
                return $"{hours} hour{(hours > 1 ? "s" : string.Empty)}, {mins} minute{(mins > 1 ? "s" : string.Empty)}";
            return mins > 0 ? $"{mins} minute{(mins > 1 ? "s" : string.Empty)}, {secs} second{(secs > 1 ? "s" : string.Empty)}" : $"{secs} second{(secs > 1 ? "s" : string.Empty)}";
        }

        private void CloseUI()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "MAZE");
                CuiHelper.DestroyUi(player, "GGStatsUI");
            }
        }

        private Mesh GenerateMeshRandomVec(Vector3 center, List<Vector3> points, float height)
        {
            Mesh mesh = new Mesh();

            if (points == null || points.Count < 4)
            {
                ins.PrintWarning("At least 4 points are required to create a mesh.");
                Debug("At least 4 points are required to create a mesh.");
                return mesh;
            }

            Vector3[] vertices;
            int[] triangles;

            vertices = new Vector3[points.Count * 2];

            for (int i = 0; i < points.Count; i++)
            {
                vertices[i] = points[i] + Vector3.down * height - center;
                vertices[i + points.Count] = points[i] + Vector3.up * height - center;
            }

            switch (points.Count)
            {
                case 4:
                    triangles = new int[]
                    {
                        0, 1, 2,
                        2, 3, 0,

                        4, 5, 6,
                        6, 7, 4,

                        0, 1, 5,
                        5, 4, 0,

                        1, 2, 6,
                        6, 5, 1,

                        2, 3, 7,
                        7, 6, 2,

                        3, 0, 4,
                        4, 7, 3
                    };
                    break;

                case 6:
                    triangles = new int[]
                    {
                        0, 1, 2,
                        2, 3, 4,
                        4, 5, 0,

                        6, 7, 8,
                        8, 9, 10,
                        10, 11, 6,

                        0, 1, 7,
                        7, 6, 0,

                        1, 2, 8,
                        8, 7, 1,

                        2, 3, 9,
                        9, 8, 2,

                        3, 4, 10,
                        10, 9, 3,

                        4, 5, 11,
                        11, 10, 4,

                        5, 0, 6,
                        6, 11, 5
                    };
                    break;

                case 8:
                    triangles = new int[]
                    {
                        0, 1, 2,
                        2, 3, 4,
                        4, 5, 6,
                        6, 7, 0,

                        8, 9, 10,
                        10, 11, 12,
                        12, 13, 14,
                        14, 15, 8,

                        0, 1, 9,
                        9, 8, 0,

                        1, 2, 10,
                        10, 9, 1,

                        2, 3, 11,
                        11, 10, 2,

                        3, 4, 12,
                        12, 11, 3,

                        4, 5, 13,
                        13, 12, 4,

                        5, 6, 14,
                        14, 13, 5,

                        6, 7, 15,
                        15, 14, 6,

                        7, 0, 8,
                        8, 15, 7
                    };
                    break;

                default:
                    PrintWarning("Unsupported number of points.");
                    return mesh;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        private void BroadCastMessage(string message)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                ins.CM(player, message);
            }
        }

        private void SubscribeEventHooks()
        {
            Subscribe(nameof(OnPlayerDeath));
            Subscribe(nameof(OnEntityTakeDamage));
            Subscribe(nameof(OnWeaponFired));
            Subscribe(nameof(OnUserCommand));
            Subscribe(nameof(OnPlayerDisconnected));
            Subscribe(nameof(OnPlayerCorpseSpawned));
            Subscribe(nameof(OnVanishDisappear));
            Debug("Subscribing Event Hooks");
        }

        private void UnsubscribeEventHooks()
        {
            Unsubscribe(nameof(OnPlayerDeath));
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(OnWeaponFired));
            Unsubscribe(nameof(OnUserCommand));
            Unsubscribe(nameof(OnPlayerDisconnected));
            Unsubscribe(nameof(OnPlayerCorpseSpawned));
            Unsubscribe(nameof(OnVanishDisappear));
            Debug("Unsubscribing Event Hooks");
        }

        private static void CloseUIPlayer(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "MAZE");
            CuiHelper.DestroyUi(player, "GGStatsUI");
        }

        private void KillStreakCheck(BasePlayer player)
        {
            if (player == null || !config.showkillstreak) return;

            PlayerData playerData = FindPlayerData(player);
            if (playerData == null) return;

            if (playerData.KillStreak == 2)
            {
                BroadCastMessage($"<color=#FFD700>{player.displayName}</color> on <color=#55FF55>DOUBLE KILL!</color>"); //green
            }
            else if (playerData.KillStreak == 3)
            {
                BroadCastMessage($"<color=#FFD700>{player.displayName}</color> on <color=#FF5555>TRIPPLE KILL!</color>"); //red
            }
            else if (playerData.KillStreak == 4)
            {
                BroadCastMessage($"<color=#FFD700>{player.displayName}</color> on <color=#FF55FF>QUADS KILL!</color>"); //purple
            }
            else if (playerData.KillStreak == 5)
            {
                BroadCastMessage($"<color=#FFD700>{player.displayName}</color> on <color=#FFFF55>PENTA KILL!</color>"); //yellow
            }
            else if (playerData.KillStreak > 5)
            {
                BroadCastMessage($"<color=#FFD700>{player.displayName}</color> is <color=#AA0000>MONSTER</color> with <color=#FFFF55>{playerData.KillStreak} KILLS!</color>"); //red & yellow
            }
        }

        private void TeleportPlayer(BasePlayer player, Vector3 location)
        {
            if (player == null) { return; }
            if (location == Vector3.zero) { return; }

            player.PauseFlyHackDetection(5f);
            player.PauseSpeedHackDetection(5f);
            player.UpdateActiveItem(default(ItemId));
            player.EnsureDismounted();
            player.Server_CancelGesture();

            if (player.HasParent())
            {
                player.SetParent(null, true, true);
            }

            if (player.IsConnected)
            {
                StartSleeping(player);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
                player.ClientRPCPlayer(null, player, "StartLoading", arg1: true);
            }
            var oldPos = player.transform.position;

            player.Teleport(location);

            if (player.IsConnected)
            {
                if (!player._limitedNetworking)
                {
                    player.UpdateNetworkGroup();
                    player.SendNetworkUpdateImmediate(false);
                }

                player.ClearEntityQueue(null);
                player.SendFullSnapshot();
                player.MarkHostileFor(0);
                if (player.IsOnGround())
                {
                    NextTick(player.EndSleeping);
                }
            }

            if (!player._limitedNetworking)
            {
                player.ForceUpdateTriggers();
            }

            Interface.CallHook("OnPlayerTeleported", player, oldPos, location);
        }

        public void StartSleeping(BasePlayer player)
        {
            if (!player.IsSleeping())
            {
                Interface.CallHook("OnPlayerSleep", player);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, b: true);
                player.sleepStartTime = Time.time;
                BasePlayer.sleepingPlayerList.Add(player);
                player.CancelInvoke("InventoryUpdate");
                player.CancelInvoke("TeamUpdate");
                player.inventory.loot.Clear();
                player.inventory.containerMain.OnChanged();
                player.inventory.containerBelt.OnChanged();
                player.inventory.containerWear.OnChanged();
                player.Invoke("TurnOffAllLights", 0f);
                if (!player._limitedNetworking)
                {
                    player.EnablePlayerCollider();
                    player.RemovePlayerRigidbody();
                }
                else player.RemoveFromTriggers();
                player.SetServerFall(wantsOn: true);
            }
        }

        public bool IsAlliedWithPlayer(ulong playerId, ulong targetId)
        {
            if (playerId == targetId)
            {
                return true;
            }

            if (RelationshipManager.ServerInstance.playerToTeam.TryGetValue(playerId, out var team) && team.members.Contains(targetId))
            {
                return true;
            }

            if (Clans != null && (bool)Clans.Call("IsClanMember", playerId.ToString(), targetId.ToString()))
            {
                return true;
            }

            if (FClan != null && (bool)FClan.Call("IsClanMember", playerId.ToString(), targetId.ToString()))
            {
                return true;
            }

            return false;
        }

        private bool CheckAdmin(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, AdminMaze))
            {
                CM(player, "You don't have permission to use this command.");
                return false;
            }
            else
            {
                return true;
            }

        }

        private void CM(BasePlayer player, string message)
        {
            Player.Message(player, config.chat.prefix + message, config.chat.avatarID);
        }

        private int CountUniqueClans(HashSet<BasePlayer> playerIDs)
        {
            HashSet<string> uniqueClans = new HashSet<string>();

            if (Clans == null && FClan == null)
            {
                return 0;
            }
            else
            {
                foreach (var playerID in playerIDs)
                {
                    string clan = GetPlayerClanTag(playerID.userID);
                    uniqueClans.Add(clan);
                }
            }
            return uniqueClans.Count;
        }

        private string GetTimeRemeaningText()
        {
            string[] text = config.chat.topBar;

            string _text = "";
            foreach (var line in text)
            {
                _text += line + "\n";
            }
            return _text;
        }

        private string GetAdminHelpText()
        {
            string[] cmds = new string[]
            {
                        "<size=16><color=#FFFF00>Arena Create commands</color></size>",
                        "<size=12>- <color=#FFFF00>/maze create</color> <ArenaName> - Create a maze arena where you are standing</size>",
                        "<size=12>- <color=#FFFF00>/maze edit</color> <ArenaName> - Select an arena</size>",
                        "<size=12>- <color=#FFFF00>/maze radius</color> <Number> - Change arena radius</size>",
                        "<size=12>- <color=#FFFF00>/maze wallradius</color> <Number> - Change arena wall radius which is your arena walls radius from center</size>",
                        "<size=12>- <color=#FFFF00>/maze meshheight</color> <Number> - Change arena mesh height is top and bottom from corner mesh</size>",
                        "<size=12>- <color=#FFFF00>/maze location</color> - Change arena location to where you are standing</size>",
                        "<size=12>- <color=#FFFF00>/maze setcorner</color> - Set corner location to where you are standing</size>",
                        "<size=12>- <color=#FFFF00>/maze showmesh</color> - Show created mesh after setting corners</size>",
                        "<size=12>- <color=#FFFF00>/maze copywalls</color> - Copy all the walls in the arena radius and store them</size>",
                        "<size=12>- <color=#FFFF00>/maze pastewalls</color> - Copy all the walls in the arena radius and store them</size>",
                        "<size=12>- <color=#FFFF00>/maze setspawn</color> - Set spawn postion for arena players to teleport</size>",
                        "<size=12>- <color=#FFFF00>/maze spawnclear</color> - Clear all spawn points</size>",
                        "<size=12>- <color=#FFFF00>/maze adddoor</color> - Place an arena door to your standing location</size>",
                        "<size=12>- <color=#FFFF00>/maze removedoor</color> - Remove arena door to your standing location (Radius 5f)</size>",
                        "<size=12>- <color=#FFFF00>/maze cleandoors</color> <ArenaName> - Remove all the arena doors from the arena</size>",
                        "<size=12>- <color=#FFFF00>/maze list</color> - Show all arena and its settings</size>",
                        "<size=12>- <color=#FFFF00>/maze delete/remove</color> <ArenaName> - Remove or deletes an arena</size>",
                        "<size=12>- <color=#FFFF00>/maze cleanarena</color> - Clears the entire arena</size>",
                        "<size=12>- <color=#FFFF00>/maze start</color> <ArenaName> - Starts a Maze(For Testing)</size>",
                        "",
                        "<size=16><color=#FFFF00>Player & Console commands</color></size>",
                        "<size=12>- <color=#FFFF00>/mazestart</color> - Starts a random maze from the config if available</size>",
                        "<size=12>- <color=#FFFF00>/mazestart</color> <ArenaName> - Starts the specific arena</size>",
                        "<size=12>- <color=#FFFF00>/mazestop</color> - Stops the running maze event</size>",
            };

            string text = "";
            foreach (var line in cmds)
            {
                text += line + "\n";
            }
            return text;
        }

        private static string GetFormatTime(int timeRemaining)
        {
            var time = timeRemaining;
            double minutes = Math.Floor((double)(time / 60));
            time -= (int)(minutes * 60);
            return string.Format("{0:00}:{1:00}", minutes, time);
        }

        private int GetClanTotalKills(string clanTag)
        {
            int totalKills = 0;

            foreach (var playerData in mazeData.playerData)
            {
                ulong playerID = playerData.Key;
                PlayerData data = playerData.Value;

                string playerClanTag = GetPlayerClanTag(playerID);
                if (playerClanTag == clanTag)
                {
                    totalKills += data.PlayerKills;
                }
            }

            return totalKills;
        }

        private void KillEntityAndReturnItem(BasePlayer player, BaseEntity entity, Item item)
        {
            ItemDefinition itemDefinition = item == null ? null : item.info;

            ulong skin = item == null ? 0UL : item.skin;

            entity.Invoke(() =>
            {
                if (entity && !entity.IsDestroyed)
                    entity.Kill(BaseNetworkable.DestroyMode.Gib);

                if (itemDefinition != null)
                    player.GiveItem(ItemManager.Create(itemDefinition, 1, skin));

            }, 0.1f);
        }

        public Color HexToUnityColor(string hex)
        {
            Color unityColor = Color.black;

            if (ColorUtility.TryParseHtmlString(hex, out unityColor))
            {
                return unityColor;
            }

            Puts("Invalid hex color value: " + hex);
            return unityColor;
        }

        /*private void CheckForOldPos(BasePlayer player)
        {
            if (player == null || !config.saveTPLoc) return;

            if (OldLocation.ContainsKey(player.userID))
            {
                OldLocation.Remove(player.userID);
            }
            OldLocation.Add(player.userID, player.transform.position);
        }

        private void RemoveOldPos(BasePlayer player)
        {
            if (player == null || !config.saveTPLoc) return;

            if (OldLocation.ContainsKey(player.userID))
            {
                OldLocation.Remove(player.userID);
            }
        }*/

        private void RemoveItemsFromInventory(BasePlayer player)
        {
            ItemContainer playerInventory = player.inventory.containerMain;
            ItemContainer playerWear = player.inventory.containerWear;
            ItemContainer playerHotBar = player.inventory.containerBelt;

            HashSet<string> bannedItemNames = new HashSet<string>();

            foreach (var itemDefinition in config.blockItems)
            {
                foreach (var container in new[] { playerInventory, playerWear, playerHotBar })
                {
                    foreach (var item in container.itemList)
                    {
                        if (item.info.shortname == itemDefinition)
                        {
                            bannedItemNames.Add(item.info.displayName.translated);
                            item.Remove();
                        }
                    }
                }
            }

            if (bannedItemNames.Count > 0)
            {
                string itemsNames = string.Join(", ", bannedItemNames);
                CM(player, GetMessage("EventBannedItems", itemsNames));
            }
        }

        public bool InMazeEvent(BasePlayer player)
        {
            if (player != null && maze != null)
            {
                if (maze.InMazeArena.Contains(player))
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsBlockedCommand(string command)
        {
            if (config.blockCommands.Contains(command, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }

        private string GetPlayerClanTag(ulong player)
        {
            string playerClan = "";
            if (Clans != null)
            {
                return playerClan = (string)Clans.Call("GetClanOf", player);
            }
            else if (FClan != null)
            {
                return playerClan = (string)FClan.Call("GetClanTag", player);
            }

            return playerClan;
        }


        private BasePlayer GetTop1PlayersWithMostKillsFromClan(string clanName)
        {
            var top5Players = GetAllPlayersByClanTag(clanName);

            foreach (var id in top5Players)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if ((player.userID == id.Key) && player.IsConnected)
                    {
                        return player;
                    }
                }
            }

            return null;
        }
        private void DestroyMesh(Mesh mesh)
        {
            if (mesh != null)
            {
                UnityEngine.Object.Destroy(mesh); // Destroys the mesh object and frees memory
            }
        }

        private bool InMazeArenaRadius(BasePlayer player)
        {
            if (player == null && maze == null) return false;

            float Distance = Vector3.Distance(player.GetNetworkPosition(), maze.arena.Center);

            if (Distance < maze.arena.radius)
                return true;

            return false;
        }

        #endregion

        #region UI

        private const string MazeUI = "MazeStatsUI";

        private void CreateMazeTopBarUI(BasePlayer player)
        {
            if (player == null) return;

            var container = new CuiElementContainer();

            UIBuilder.CreatePanel(
                ref container, "Hud", "MAZE", "1 1 1 0",
                "0.5 1 0.5 1", "-303.819 -157.465 303.82 -38.535"
            );

            CuiHelper.DestroyUi(player, "MAZE");
            CuiHelper.AddUi(player, container);
        }

        private void UpdateTopBarUI(BasePlayer player, string TopBarUIText, int time, string Text)
        {
            if (player == null) return;

            var container = new CuiElementContainer();

            Text = Text.Replace("{TIME}", GetFormatTime(time));
            string dynamicText = TopBarUIText
                .Replace("{DYNAMIC_TIMER}", Text)
                .Replace("{TotalPlayers}", maze.PlayingList.Count.ToString())
                .Replace("{TotalTeams}", CountUniqueClans(maze.PlayingList).ToString());

            UIBuilder.CreateLabelOutline(
                ref container, "MAZE", "Text", dynamicText, "1 1 1 1", 14,
                "0.5 0.5 0.5 0.5", "-305.502 -59.465 305.502 59.465",
                TextAnchor.UpperCenter, "Text", "0.5 0.5", "0 0 0 1"
            );

            CuiHelper.AddUi(player, container);
        }


        private void CreateRunningStatsUI(BasePlayer player)
        {
            var container = new CuiElementContainer();

            // Main UI Panel
            UIBuilder.CreatePanel(ref container, "Hud", "GGStatsUI", "1 1 1 0", "1 1 1 1", "-194.689 -230.769 -11.773 -11.231");
            UIBuilder.CreatePanel(ref container, "GGStatsUI", "Title", "0.254902 0.3176471 0.4156863 0.8", "0.5 0.5 0.5 0.5", "-91.461 92.095 91.459 109.765");

            // Title Labels
            UIBuilder.CreateLabel(ref container, "Title", "Name", "TEAMS", "0.956 0.266 0.015 1", 11, "0.5 0.5 0.5 0.5", "-91.46 -8.835 0 8.835", TextAnchor.MiddleCenter);
            UIBuilder.CreateLabel(ref container, "Title", "Kills", "KILLS", "0.956 0.266 0.015 1", 11, "0.5 0.5 0.5 0.5", "0 -8.835 91.46 8.835", TextAnchor.MiddleCenter);

            CuiHelper.DestroyUi(player, "GGStatsUI");
            CuiHelper.AddUi(player, container);
        }

        private void UpdateRunningStatusUI(BasePlayer player)
        {
            if (player == null) return;
            var container = new CuiElementContainer();

            int panelIndex = 0;
            foreach (var entry in GetTop5ClansByKills())
            {
                string pID = entry.Key;
                int arenaPData = entry.Value;
                string playerPanel = $"Player{panelIndex}";

                // Player Panel
                UIBuilder.CreatePanel(ref container, "GGStatsUI", playerPanel, "0.254902 0.3176471 0.4156863 0.7", "0.5 0.5 0.5 0.5", $"-91.462 {73.065 - (panelIndex * 19.2)} 91.458 {90.735 - (panelIndex * 19.2)}", playerPanel);

                // Player Name and Kill Count
                UIBuilder.CreateLabel(ref container, playerPanel, "Name", pID, "1 1 1 1", 9, "0.5 0.5 0.5 0.5", "-91.46 -8.835 0 8.835", TextAnchor.MiddleCenter);
                UIBuilder.CreateLabel(ref container, playerPanel, "Kills", arenaPData.ToString(), "1 1 1 1", 9, "0.5 0.5 0.5 0.5", "0 -8.835 91.46 8.835", TextAnchor.MiddleCenter);

                panelIndex++;
            }

            CuiHelper.AddUi(player, container);
        }

        private class UIBuilder
        {
            public static void CreatePanel(ref CuiElementContainer container, string parent, string name, string color, string anchor, string offset, string destroyUi = null, string material = null)
            {
                var dimensions = ParseDimensions(anchor, offset);

                container.Add(
                    new CuiPanel
                    {
                        RectTransform = {
                    AnchorMin = dimensions[0],
                    AnchorMax = dimensions[1],
                    OffsetMin = dimensions[2],
                    OffsetMax = dimensions[3],
                        },
                        Image = {
                    Color = color,
                    Material = material
                        }
                    },
                    parent,
                    name,
                    destroyUi
                );
            }

            public static void CreateImage(ref CuiElementContainer container, string parent, string name, string image, string color, string anchor, string offset)
            {
                var dimensions = ParseDimensions(anchor, offset);

                uint _value;
                var isPng = uint.TryParse(image, out _value);

                container.Add(
                    new CuiElement
                    {
                        Parent = parent,
                        Name = name ?? CuiHelper.GetGuid(),
                        Components = {
                    new CuiRectTransformComponent {
                        AnchorMin = dimensions[0],
                        AnchorMax = dimensions[1],
                        OffsetMin = dimensions[2],
                        OffsetMax = dimensions[3],
                    },
                    new CuiRawImageComponent {
                        Png = isPng ? image : null,
                        Url = !isPng ? image : null,
                        Color = color
                    }
                        }
                    }
                );
            }

            public static void CreateLabel(ref CuiElementContainer container, string parent, string name, string text, string color, int fontSize, string anchor, string offset, TextAnchor align = TextAnchor.MiddleLeft, string destoryUI = null)
            {
                var dimensions = ParseDimensions(anchor, offset);

                container.Add(
                    new CuiLabel
                    {
                        RectTransform = {
                    AnchorMin = dimensions[0],
                    AnchorMax = dimensions[1],
                    OffsetMin = dimensions[2],
                    OffsetMax = dimensions[3],
                        },
                        Text = {
                        Text = text,
                        Color = color,
                        Align = align,
                        FontSize = fontSize,
                        }
                    },
                    parent,
                    name,
                    destoryUI

                );
            }

            public static void CreateLabelOutline(ref CuiElementContainer container, string parent, string name, string text, string color, int fontSize, string anchor, string offset, TextAnchor align = TextAnchor.MiddleLeft, string destoryUI = null, string outlineDis = null, string outlineColor = null)
            {
                var dimensions = ParseDimensions(anchor, offset);

                container.Add(new CuiElement
                {
                    Name = name,
                    Parent = parent,
                    DestroyUi = destoryUI,
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = dimensions[0],
                            AnchorMax = dimensions[1],
                            OffsetMin = dimensions[2],
                            OffsetMax = dimensions[3]
                        },
                        new CuiTextComponent
                        {
                            Text = text,
                            Color = color,
                            FontSize = fontSize,
                            Align = align,
                        },
                        new CuiOutlineComponent { Distance = outlineDis, Color = outlineColor }
                    }
                });
            }

            private static string[] ParseDimensions(string anchor, string offset)
            {
                var anchors = anchor.Split(' ');
                string anchorMin = string.Join(" ", anchors.Take(2)),
                    anchorMax = string.Join(" ", anchors.Skip(2));

                var offsets = offset.Split(' ');
                string offsetMin = string.Join(" ", offsets.Take(2)),
                    offsetMax = string.Join(" ", offsets.Skip(2));

                return new string[] {
                anchorMin,
                anchorMax,
                offsetMin,
                offsetMax
                };
            }
        }

        

        #endregion

        #region Discord Intregration

        private void SendDiscordMessage(string webhook, string title, List<string> embeds, bool inline = false)
        {
            Embed embed = new Embed();
            foreach (var item in embeds)
            {
                embed.AddField(title, item, inline, 3066993);
            }

            webrequest.Enqueue(webhook, new DiscordMessage(string.Empty, embed).ToJson(), (code, response) => { },
                this,
                RequestMethod.POST, new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" }
                });
        }

        private class DiscordMessage
        {
            public DiscordMessage(string content, params Embed[] embeds)
            {
                Content = content;
                Embeds = embeds.ToList();
            }

            [JsonProperty("content")] public string Content { get; set; }
            [JsonProperty("embeds")] public List<Embed> Embeds { get; set; }

            public string ToJson()
            {
                return JsonConvert.SerializeObject(this);
            }
        }

        private class Embed
        {
            public int color
            {
                get; set;
            }
            [JsonProperty("fields")] public List<Field> Fields { get; set; } = new List<Field>();

            public Embed AddField(string name, string value, bool inline, int colors)
            {
                Fields.Add(new Field(name, Regex.Replace(value, "<.*?>", string.Empty), inline));
                color = colors;
                return this;
            }
        }

        private class Field
        {
            public Field(string name, string value, bool inline)
            {
                Name = name;
                Value = value;
                Inline = inline;
            }

            [JsonProperty("name")] public string Name { get; set; }
            [JsonProperty("value")] public string Value { get; set; }
            [JsonProperty("inline")] public bool Inline { get; set; }
        }


        #endregion

        #region MonoBehaviour


        private void SendTipBroadcast(string message, float duration)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player != null && player.IsConnected)
                {
                    player?.SendConsoleCommand("gametip.showgametip", message);
                    timer.Once(duration, () => { player?.SendConsoleCommand("gametip.hidegametip"); });
                }
            }
        }

        private void SendTip(BasePlayer player, string message, float duration)
        {
            if (player != null && player.IsConnected)
            {
                player?.SendConsoleCommand("gametip.showgametip", message);
                timer.Once(duration, () => { player?.SendConsoleCommand("gametip.hidegametip"); });
            }
        }

        #endregion

        #region Temp Data

        #region class Data

        private static ItemsData tempItems;

        private class ItemsData
        {
            public List<KitItems> Items = new List<KitItems>();
        }

        private class KitItems
        {
            public string ShortName;
            public string DisplayName;
            public int Amount;
            public int ItemID;
            public ulong SkinID;
            public int Position;
            public float Condition;
            public float MaxCondition;
            public string ImageURL;
            public int WeaponAmmo;
            public string AmmoType;
            public List<KitItems> Content;
        }

        private static List<KitItems> GetKitItems(ItemContainer container)
        {
            return container.itemList.Select(item =>
                new KitItems
                {
                    ShortName = item.info.shortname,
                    DisplayName = item.name,
                    ItemID = item.info.itemid,
                    Amount = item.amount,
                    SkinID = item.skin,
                    Position = item.position,
                    Condition = item.condition,
                    MaxCondition = item.maxCondition,
                    ImageURL = "",
                    WeaponAmmo = WeaponAmmoCheck(item), //(item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.contents ?? 0
                    AmmoType = WeaponTypeCheck(item),  //(item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.ammoType.shortname
                    Content = item.contents != null ? GetKitItems(item.contents) : null
                }).ToList();
        }

        private static int WeaponAmmoCheck(Item item)
        {
            FlameThrower flameThrower = item.GetHeldEntity() as FlameThrower;
            if (flameThrower != null)
                return flameThrower.ammo;

            Chainsaw chainsaw = item.GetHeldEntity() as Chainsaw;
            if (chainsaw != null)
                return chainsaw.ammo;

            BaseProjectile projectile = item.GetHeldEntity() as BaseProjectile;
            if (projectile != null)
                return projectile.primaryMagazine.contents;

            return 0;
        }

        private static string WeaponTypeCheck(Item item)
        {
            if (item == null || item.GetHeldEntity() == null)
            {
                return string.Empty;
            }

            FlameThrower flameThrower = item.GetHeldEntity() as FlameThrower;
            if (flameThrower != null)
            {
                return flameThrower.GetAmmo()?.info.shortname ?? string.Empty;
            }

            Chainsaw chainsaw = item.GetHeldEntity() as Chainsaw;
            if (chainsaw != null)
            {
                return chainsaw.GetAmmo()?.info.shortname ?? string.Empty;
            }

            BaseProjectile projectile = item.GetHeldEntity() as BaseProjectile;
            if (projectile != null)
            {
                return projectile.primaryMagazine?.ammoType?.shortname ?? string.Empty;
            }

            return string.Empty;
        }

        private static Item CreateItem(KitItems itemData)
        {
            Item item = ItemManager.CreateByItemID(itemData.ItemID, itemData.Amount, itemData.SkinID);
            item.condition = itemData.Condition;
            item.maxCondition = itemData.MaxCondition;

            if (!string.IsNullOrEmpty(itemData.DisplayName))
            {
                item.name = itemData.DisplayName;
            }

            FlameThrower flameThrower = item.GetHeldEntity() as FlameThrower;
            if (flameThrower != null)
                flameThrower.ammo = itemData.WeaponAmmo;

            Chainsaw chainsaw = item.GetHeldEntity() as Chainsaw;
            if (chainsaw != null)
                chainsaw.ammo = itemData.WeaponAmmo;

            if (itemData.Content != null)
            {
                foreach (KitItems contentData in itemData.Content)
                {
                    Item newContent = CreateItem(contentData);
                    if (newContent != null)
                    {
                        if (!newContent.MoveToContainer(item.contents))
                            newContent.Remove(0f);
                    }
                }
            }

            BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
            if (weapon != null)
            {
                weapon.DelayedModsChanged();

                if (!string.IsNullOrEmpty(itemData.AmmoType))
                    weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(itemData.AmmoType);
                weapon.primaryMagazine.contents = itemData.WeaponAmmo;
            }

            item.MarkDirty();

            return item;
        }

        #endregion

        private static readonly int BodyBagMask = LayerMask.GetMask("Ragdoll");

        private static IEnumerator MoveEntitiesToBoxes(MazeArenaSetting arena)
        {
            try
            {
                tempItems = new ItemsData();

                List<BaseEntity> entities = Facepunch.Pool.Get<List<BaseEntity>>();
                Vis.Entities(arena.Center, arena.radius, entities, BodyBagMask);

                List<Item> items = Facepunch.Pool.Get<List<Item>>();

                //instance.Puts("Count ew " + entities.Count);
                foreach (BaseEntity entity in entities)
                {
                    // Check if the entity is a dead body, body bag, or a dropped item container
                    if (entity is BaseCorpse || entity is LootableCorpse || entity is DroppedItemContainer)
                    {
                        ItemContainer container = null;
                        if (entity is BaseCorpse || entity is LootableCorpse)
                        {
                            LootableCorpse lootableCorpse = entity.GetComponent<LootableCorpse>();
                            if (lootableCorpse != null && lootableCorpse.containers != null)
                            {
                                container = lootableCorpse.containers[0];
                            }
                        }

                        if (entity is DroppedItemContainer)
                        {
                            DroppedItemContainer droppedItemContainer = entity.GetComponent<DroppedItemContainer>();
                            if (droppedItemContainer != null && droppedItemContainer.inventory != null)
                            {
                                container = droppedItemContainer.inventory;
                            }
                        }

                        // If we found a valid inventory
                        if (container != null)
                        {
                            List<KitItems> itemsKit = GetKitItems(container);
                            foreach (var it in itemsKit)
                                tempItems.Items.Add(it);

                            foreach (Item item in container.itemList)
                            {
                                items.Add(item);

                            }

                            // Remove the entity (dead body, body bag, or dropped item container)
                            entity.Kill();
                        }
                    }
                }

                entities.Clear();
                Vis.Entities(arena.Center, arena.radius, entities, BodyBagMask);

                foreach (BaseEntity entity in entities)
                {
                    // Check if the entity is a dead body, body bag, or a dropped item container
                    if (entity is BaseCorpse || entity is LootableCorpse || entity is DroppedItemContainer)
                    {
                        ItemContainer container = null;
                        if (entity is BaseCorpse || entity is LootableCorpse)
                        {
                            LootableCorpse lootableCorpse = entity.GetComponent<LootableCorpse>();
                            if (lootableCorpse != null && lootableCorpse.containers != null)
                            {
                                container = lootableCorpse.containers[0];
                            }
                        }

                        if (entity is DroppedItemContainer)
                        {
                            DroppedItemContainer droppedItemContainer = entity.GetComponent<DroppedItemContainer>();
                            if (droppedItemContainer != null && droppedItemContainer.inventory != null)
                            {
                                container = droppedItemContainer.inventory;
                            }
                        }

                        if (container != null)
                        {
                            List<KitItems> itemsKit = GetKitItems(container);
                            foreach (var it in itemsKit)
                                tempItems.Items.Add(it);

                            // Get the items from the container
                            foreach (Item item in container.itemList)
                            {
                                items.Add(item);
                            }

                            // Remove the entity (dead body, body bag, or dropped item container)
                            entity.Kill();
                        }
                    }
                }

                //instance.Puts("Count " + items.Count);
                //instance.Puts("Count " + tempItems.Items.Count);

                // Calculate how many boxes are needed for items
                int itemsPerBox = 48; // Maximum number of items per box
                int totalBoxesNeeded = (int)Mathf.Ceil((float)items.Count / itemsPerBox); // box can hold 48 items
                Vector3 boxPosition = arena.Center;

                if (totalBoxesNeeded > 0)
                {
                    for (int i = 0; i < totalBoxesNeeded; i++)
                    {
                        BaseEntity boxEntity = GameManager.server.CreateEntity("assets/prefabs/deployable/large wood storage/box.wooden.large.prefab", GetUniquePosition(boxPosition, 2f));
                        // Calculate the number of items to move to this box
                        int startIndex = i * itemsPerBox;
                        int endIndex = Mathf.Min(startIndex + itemsPerBox, items.Count);

                        boxEntity.Spawn();
                        ins.maze.tempLootBoxes.Add(boxEntity);

                        // Add items to the box
                        var boxInventory = boxEntity.GetComponent<StorageContainer>().inventory;

                        for (int j = startIndex; j < endIndex; j++)
                        {
                            //instance.Puts("Creating box at position: " + boxPosition);
                            //instance.Puts("Moving items from index " + startIndex + " to " + endIndex);
                            Item createItem = CreateItem(tempItems.Items[j]);
                            if (createItem != null)
                            {
                                createItem.MoveToContainer(boxInventory);
                            }
                        }

                        // Move to the position for the next box
                        //boxPosition += new Vector3(2f, 0f, 0f);
                    }

                }
                entities.Clear();
                Facepunch.Pool.FreeUnmanaged(ref entities);
                Facepunch.Pool.FreeUnmanaged(ref items);

                tempItems.Items.Clear();
                tempItems = null;
            }
            catch { }


            yield return null;
        }

        private static HashSet<Vector3> usedPositions = new HashSet<Vector3>();

        private static Vector3 GetUniquePosition(Vector3 center, float spacing)
        {
            int attempts = 0;
            Vector3 newPos = center;

            while (attempts < 100) // Prevent infinite loops
            {
                float angle = UnityEngine.Random.Range(0f, 360f); // Random angle
                float distance = spacing * (1 + (attempts / 10)); // Increase distance over time

                newPos = new Vector3(
                    center.x + Mathf.Cos(angle) * distance,
                    center.y,
                    center.z + Mathf.Sin(angle) * distance
                );

                if (!usedPositions.Contains(newPos) && FindPointOnNavmesh(ref newPos))
                {
                    usedPositions.Add(newPos);
                    return newPos;
                }

                attempts++;
            }

            return center;
        }

        private static bool FindPointOnNavmesh(ref Vector3 targetPosition)
        {
            for (var i = 0; i < 20; i++)
            {
                targetPosition.y = GetSpawnHeight(targetPosition);

                if (!NavMesh.SamplePosition(targetPosition, out navmeshHit, 10f, 1))
                    continue;

                if (IsInRockPrefab(navmeshHit.position))
                    continue;

                if (IsNearWorldCollider(navmeshHit.position))
                    continue;

                targetPosition = navmeshHit.position;
                return true; // Found a valid position
            }

            return false; // No valid position found
        }
        private static NavMeshHit navmeshHit;
        private static RaycastHit raycastHit;
        private const int WORLD_LAYER = 65536;
        private static Collider[] _buffer = new Collider[256];
        private static readonly string[] AcceptedColliders = { "road", "rocket_factory", "train_track", "runway", "_grounds", "concrete_slabs", "office", "industrial", "junkyard" };
        private static readonly string[] BlockedColliders = { "cliff", "rock", "junk", "range", "invisible" };
        private static float GetSpawnHeight(Vector3 target)
        {
            var y = TerrainMeta.HeightMap.GetHeight(target);
            var p = TerrainMeta.HighestPoint.y + 250f;

            if (UnityEngine.Physics.Raycast(new Vector3(target.x, p, target.z), Vector3.down, out raycastHit, target.y + p, Layers.Mask.World, QueryTriggerInteraction.Ignore))
                y = Mathf.Max(y, raycastHit.point.y);

            return y;
        }

        private static bool IsInRockPrefab(Vector3 position)
        {
            UnityEngine.Physics.queriesHitBackfaces = true;
            var isInRock = UnityEngine.Physics.Raycast(position, Vector3.up, out raycastHit, 20f, WORLD_LAYER, QueryTriggerInteraction.Ignore) && BlockedColliders.Any(s => raycastHit.collider?.gameObject?.name.Contains(s, System.Globalization.CompareOptions.OrdinalIgnoreCase) ?? false);
            UnityEngine.Physics.queriesHitBackfaces = false;
            return isInRock;
        }

        private static bool IsNearWorldCollider(Vector3 position)
        {
            UnityEngine.Physics.queriesHitBackfaces = true;
            var count = UnityEngine.Physics.OverlapSphereNonAlloc(position, 2f, _buffer, WORLD_LAYER, QueryTriggerInteraction.Ignore);
            UnityEngine.Physics.queriesHitBackfaces = false;

            var removed = 0;

            for (var i = 0; i < count; i++)
            {
                if (AcceptedColliders.Any(s => _buffer[i].gameObject.name.Contains(s)))
                    removed++;
            }

            return removed != count;
        }

        #endregion

        #region API

        #endregion

    }
}