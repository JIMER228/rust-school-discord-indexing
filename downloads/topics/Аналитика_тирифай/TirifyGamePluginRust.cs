// -------------- PLUGIN CODE MACROSES --------------

// If you want to select and older version for the plugin, remove '//' near your version:

//#define RUST_DEVBLOG_198
//#define RUST_DEVBLOG_266

#if RUST_DEVBLOG_198
//#define USE_HARMONY
#define RUST_OLD_STEAM_AUTH
#define RUST_DISABLE_EASYANTICHEAT
#define OLD_NETWORKABLE
#define OLD_ITEMS
//#define RUST_OLD_HARMONY
//#define RUST_ORIGINAL_WIPE_ID
//#define RUST_TEAMS
//#define RUST_NPC
#define RUST_OLD_STEAMWORKS
#define RUST_NO_HELICOPTERS

#elif RUST_DEVBLOG_266
//#define USE_HARMONY
#define RUST_OLD_STEAM_AUTH
//#define RUST_DISABLE_EASYANTICHEAT
//#define RUST_OLD_HARMONY
//#define RUST_ORIGINAL_WIPE_ID
#define RUST_TEAMS
#define RUST_NPC
#define RUST_NO_HELICOPTERS
//#define RUST_OLD_STEAMWORKS
//#define OLD_ITEMS
#define OLD_NETWORKABLE

#else

#define RUST_LATEST
#define USE_HARMONY
//#define RUST_OLD_STEAM_AUTH
//#define RUST_DISABLE_EASYANTICHEAT
//#define RUST_OLD_HARMONY
#define RUST_ORIGINAL_WIPE_ID
#define RUST_TEAMS
#define RUST_NPC
//#define RUST_OLD_STEAMWORKS

#endif

//#define STAGE

#if USE_HARMONY

#if RUST_OLD_HARMONY
using Harmony;
#else
using HarmonyLib;
#endif

#endif

// -------------- PLUGIN CODE MACROSES --------------
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Net;

// ReSharper disable All
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0168 // Variable is declared but never used

// Reference: 0Harmony

namespace Oxide.Plugins
{
    [Info("TirifyGamePluginRust", "Tirify Inc.", "1.8.2")]
    public class TirifyGamePluginRust : RustPlugin
    {
        public static TirifyGamePluginRust Singleton;

        public const uint UpdateInterval = 30;
        public const int PlayerAfkTime = 300;
        public const int AppIdOffset = 72;

        public const float SocketReconnectInterval = 60f;

        internal readonly static HashSet<ulong> _openedLootables = new HashSet<ulong>();

        private static ServerStats _serverStats;
        private static FrameLoggerComponent _frameObject;

        private static Timer _updateTimer;
        private static Timer _statsTimer;
        private static Timer _eventTimer;
        private static Timer _frameTimer;
        private static Timer _playerStatTimer;

        [PluginReference] private Plugin TGPP;
        [PluginReference] private Plugin IQReportSystem;

        #region [ Permissions ]

        private const string PermissionBanUnban = "tirify.banunban";

        #endregion

        #region [ Hooks ]

        private void OnServerInitialized()
        {
            try
            {
                InitializeReflection();

                RegisterServer();

                RegisterWebSocket();

                RegisterEventWorker();

                RegisterFrameLogger();

                //RegisterPlayerStats();

                EventLogger.LogServerStarted();
            }
            catch (Exception ex)
            {
                PrintError($"Не удалось инициализировать плагин. {ex.Message}");

                NextTick(() => Interface.Oxide.UnloadPlugin(this.Name));
            }
        }

        private void Loaded()
        {
            Singleton = this;
        }

        private void Unload()
        {
            _updateTimer?.Destroy();
            _statsTimer?.Destroy();
            _eventTimer?.Destroy();
            _frameTimer?.Destroy();
            _playerStatTimer?.Destroy();
            _serverStats?.Disconnect();

            UnityEngine.Object.Destroy(_frameObject);
        }

        private object OnUserApprove(Connection connection)
        {
            string reason = null;

            if (TirifyBanSystem.CheckPlayerBanned(connection, out reason))
            {
                if (_config.BanSystem.ShowBanReason)
                {
                    ConnectionAuth.Reject(connection, $"You are banned! Reason: {reason}");
                }
                else
                {
                    ConnectionAuth.Reject(connection, "You are banned!", $"You are banned! Reason: {reason}");
                }

                return false;
            }

            return null;
        }

        private void OnClientDisconnected(Connection connection, string reason)
        {
            if (connection == null || connection.state != Connection.State.Welcoming)
            {
                return;
            }

            try
            {
                EventLogger.LogPlayerConnection(connection, "leave", reason);

                API.Player.OnDisconnect(connection.userid);
            }
            catch (Exception ex)
            {
                Singleton.PrintError($"Exception in OnClientDisconnected: {ex}");
            }
        }

        private void OnPlayerChat(BasePlayer player, string message, object channel = null)
            => EventLogger.LogChatMessage(player, channel?.ToString()?.ToLower() ?? "global", message);

#if RUST_TEAMS
        private void OnTeamCreated(BasePlayer player, RelationshipManager.PlayerTeam team)
            => EventLogger.LogTeam(player, team.teamID.ToString(), "create", new string[0]);

        private void OnTeamKick(RelationshipManager.PlayerTeam team, BasePlayer player, ulong target)
            => EventLogger.LogTeam(player, team.teamID.ToString(), "kick", team.members.Select(f => f.ToString()).ToArray(), target);

        private void OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player)
            => EventLogger.LogTeam(player, team.teamID.ToString(), "join", team.members.Select(f => f.ToString()).ToArray());

        private void OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player)
            => EventLogger.LogTeam(player, team.teamID.ToString(), "leave", team.members.Select(f => f.ToString()).ToArray());
#endif

        private void OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
            => EventLogger.LogCupboard(player, privilege.buildingID.ToString(), "join", privilege.authorizedPlayers.Select(f => f.userid.ToString()).ToArray());

        private void OnCupboardClearList(BuildingPrivlidge privilege, BasePlayer player)
        {
            EventLogger.LogCupboard(player, privilege.buildingID.ToString(), "clear", privilege.authorizedPlayers.Select(f => f.userid.ToString()).ToArray());

            OnCupboardRaided(privilege, player);
        }

        private void OnCupboardDeauthorize(BuildingPrivlidge privilege, BasePlayer player)
            => EventLogger.LogCupboard(player, privilege.buildingID.ToString(), "leave", privilege.authorizedPlayers.Select(f => f.userid.ToString()).ToArray());

		private void OnPlayerReported(BasePlayer reporter, string targetName, string targetId, string subject, string message, string type)
		{
			ulong targetSteamId = 0;

			if (!ulong.TryParse(targetId, out targetSteamId) || targetSteamId == 0)
			{
				return;
			}

			EventLogger.LogReport(reporter, targetSteamId, subject, message);
		}

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null)
            {
                return;
            }

            bool wasWounded = entity is BasePlayer && (entity as BasePlayer).IsWounded();

            NextTick(() =>
            {
                try
                {
                    if (entity is BasePlayer)
                    {
#if RUST_NPC
                        if ((entity as BasePlayer).IsNpc == true)
                        {
                            return;
                        }
#endif

                        OnPlayerDie(entity as BasePlayer, info, wasWounded);
                        return;
                    }

                    if (info == null || info.InitiatorPlayer == null)
                    {
                        return;
                    }

                    string entityCategory = null;

                    if (entity is BradleyAPC)
                    {
                        OnBradleyApcDeath(entity as BradleyAPC, info);
                        entityCategory = "tank";
                    }
#if !RUST_NO_HELICOPTERS
                    else if (entity is PatrolHelicopter)
                    {
                        entityCategory = "patrolhelicopter";
                    }
#endif
                    else if (entity is BuildingPrivlidge)
                    {
                        var cupboard = entity as BuildingPrivlidge;

                        if (cupboard.IsAuthed(info.InitiatorPlayer))
                        {
                            return;
                        }

                        OnCupboardRaided(cupboard, info.InitiatorPlayer);
                    }
                    else if (entity is BaseAnimalNPC)
                    {
                        entityCategory = "animal";
                    }
                    else if (entity is NPCPlayer)
                    {
                        entityCategory = "npc";
                    }

                    if (entityCategory != null)
                    {
                        PlayerStats.Increment(info.InitiatorPlayer, $"kill:{entityCategory}");
                    }
                }
                catch
                {
                }
            });
        }

        private void OnPlayerDie(BasePlayer victimPlayer, HitInfo info, bool wounded)
        {
#if RUST_NPC
            if (victimPlayer?.IsNpc == true || victimPlayer?.userID < 765600000000)
            {
                return;
            }
#endif

            float logTime = 30f;

            if (wounded)
            {
                logTime += 50f;
            }

            var playerCombatLog = PlayerCombatLog.Create(victimPlayer, true, logTime);

            // not from player
            if (playerCombatLog == null)
            {
                if (info == null || info.Initiator == null)
                {
                    EventLogger.LogDeath(victimPlayer, victimPlayer.lastDamage.ToString().ToLower());
                    return;
                }

                if (victimPlayer.net?.ID == info.Initiator.net?.ID)
                {
                    EventLogger.LogDeath(victimPlayer, "suicide");
                    return;
                }

#if RUST_NPC

#if !RUST_NO_HELICOPTERS
                if (info.Initiator is PatrolHelicopter)
                {
                    EventLogger.LogDeath(victimPlayer, "helicopter");
                }
#else
                if (false)
                {
                }
#endif
                else if (info.Initiator is BradleyAPC)
                {
                    EventLogger.LogDeath(victimPlayer, "tank");
                }
                else if (info.Initiator is CH47Helicopter)
                {
                    EventLogger.LogDeath(victimPlayer, "ch47helicopter");
                }
                else if (info.Initiator is BradleyAPC)
                {
                    EventLogger.LogDeath(victimPlayer, "tank");
                }
                else if (info.Initiator is BaseAnimalNPC)
                {
                    EventLogger.LogDeath(victimPlayer, "animal", info.Initiator.Categorize());
                }
                else if (info.Initiator is NPCPlayer)
                {
                    EventLogger.LogDeath(victimPlayer, "npc", info.Initiator.ShortPrefabName);
                }
                else
                {
                    EventLogger.LogDeath(victimPlayer, "unknown", info.Initiator.ShortPrefabName);
                }
            }
            // From player (has combatlog)
            else
            {
                EventLogger.LogCombatLog(playerCombatLog);

                var attackerPlayer = TirifyGameExt.FindAwakeOrSleeping(playerCombatLog.AttackerSteamId);

                EventLogger.LogKill(
                    victimPlayer,
                    attackerPlayer,
                    playerCombatLog.Weapon,
                    playerCombatLog.Bone,
                    playerCombatLog.Distance,
                    playerCombatLog);
            }
#endif
        }

        private void OnBradleyApcDeath(BradleyAPC bradley, HitInfo info)
        {

            var assistants = GetEntityAssistants(bradley, info.InitiatorPlayer);

            var assistantIds = assistants.Select(f => f.UserIDString).ToList();
            assistantIds.Add(info.InitiatorPlayer.UserIDString);

            var fullTeamList = string.Join(",", assistantIds.ToArray());

            string weaponName = info.Weapon?.GetItem()?.info?.shortname;
            string grid = TirifyGameExt.GetGrid(bradley.transform.position);
            string distance = ((int)bradley.Distance(info.InitiatorPlayer)).ToString();

            EventLogger.LogEventNpcKilled(
                "bradley",
                info.InitiatorPlayer,
                fullTeamList,
                weaponName,
                grid,
                distance);

            foreach (var assistant in assistants)
            {
                EventLogger.LogEventNpcAssisted(
                    "bradley",
                    assistant,
                    fullTeamList,
                    weaponName,
                    grid,
                    distance,
                    info.InitiatorPlayer.UserIDString);
            }
        }

#if !RUST_NO_HELICOPTERS
        private void OnPatrolHelicopterKill(PatrolHelicopter heli, HitInfo info)
        {
            if (heli == null || info == null || info.InitiatorPlayer == null)
            {
                return;
            }

            var assistants = GetEntityAssistants(heli, info.InitiatorPlayer);

            var fullTeamList = string.Join(",", assistants.Select(f => f.UserIDString).Append(info.InitiatorPlayer.UserIDString));

            string weaponName = info.Weapon?.GetItem()?.info?.shortname;
            string grid = TirifyGameExt.GetGrid(heli.transform.position);
            string distance = ((int)heli.Distance(info.InitiatorPlayer)).ToString();

            EventLogger.LogEventNpcKilled(
                "patrolhelicopter",
                info.InitiatorPlayer,
                fullTeamList,
                weaponName,
                grid,
                distance);

            foreach (var assistant in assistants)
            {
                EventLogger.LogEventNpcAssisted(
                    "patrolhelicopter",
                    assistant,
                    fullTeamList,
                    weaponName,
                    grid,
                    distance,
                    info.InitiatorPlayer.UserIDString);
            }
        }
#endif
        private void OnCupboardRaided(BuildingPrivlidge buildingBlock, BasePlayer initiatorPlayer)
        {
#if RUST_TEAMS
            // if team's cupboard
            if (initiatorPlayer.Team != null)
            {
                foreach (var member in buildingBlock.authorizedPlayers)
                {
                    if (initiatorPlayer.Team.members.Contains(member.userid))
                    {
                        return;
                    }
                }
            }
#endif

            if (buildingBlock.OwnerID == initiatorPlayer.userID || buildingBlock.GetProtectedSeconds() > 0)
            {
                return;
            }

            var assistants = GetEntityAssistants(buildingBlock, initiatorPlayer);

            var assistantIds = assistants.Select(f => f.UserIDString).ToList();
            assistantIds.Add(initiatorPlayer.UserIDString);

            var fullTeamList = string.Join(",", assistantIds.ToArray());

            string grid = TirifyGameExt.GetGrid(buildingBlock.transform.position);
            string oldAuthed = string.Join(",", buildingBlock.authorizedPlayers.Select(f => f.userid.ToString()).ToArray());
            string inventoryItems = string.Join(",", TirifyGameExt.SerializeForSocket(buildingBlock.inventory));

            EventLogger.LogCupboardRaided(
                initiatorPlayer,
                fullTeamList,
                oldAuthed,
                grid,
                inventoryItems);

            foreach (var assistant in assistants)
            {
                EventLogger.LogCupboardRaidedAssist(
                    assistant,
                    fullTeamList,
                    oldAuthed,
                    grid,
                    inventoryItems,
                    initiatorPlayer.UserIDString);
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player));
                return;
            }

            EventLogger.LogPlayerConnection(player.Connection, "join");

            PlayerStats.Increment(player, "join:server");

            byte[] token = player.Connection.token;
			LogTokenToFile(player.displayName, player.UserIDString, token);

            API.Player.OnConnected(TirifyGameExt.ToHexString(token));
        }
		
		private void LogTokenToFile(string username, string steamId, byte[] token)
		{
			try
			{
				string tokenHex = TirifyGameExt.ToHexString(token);
				string logPath = $"{Interface.Oxide.RootDirectory}/oxide/data/tirify_tokens.log";

				using (var sw = new System.IO.StreamWriter(logPath, true))
				{
					sw.WriteLine(tokenHex);
				}
			}
			catch (Exception ex)
			{
				PrintWarning($"Ошибка при логировании токена: {ex.Message}");
			}
		}

        private void OnServerUserSet(ulong uid, global::ServerUsers.UserGroup group, string username, string notes, long expiry)
        {
            var user = BasePlayer.FindByID(uid);

            if (user != null && group == ServerUsers.UserGroup.Banned)
            {
                EventLogger.LogBan(user);
            }
        }

        private void OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            if (type == AntiHackType.ProjectileHack)
            {
                return;
            }

            var totalViolation = player.violationLevel + amount;

            // Ignore noclip or speedhack unless it reachs kick value
            if ((type == AntiHackType.NoClip || type == AntiHackType.SpeedHack)
                && totalViolation < ConVar.AntiHack.maxviolation)
            {
                return;
            }

            EventLogger.LogPlayerViolation(player, type.ToString(), amount);
        }

        private void OnStashExposed(StashContainer stash, BasePlayer player)
        {
            if (stash.OwnerID.ToString() != player.UserIDString)
            {
                BasePlayer target = BasePlayer.Find(stash.OwnerID.ToString());

                EventLogger.LogStashExposed(player, stash, target, target == null ? stash.OwnerID.ToString() : null);
            }
        }

        private void OnPluginLoaded(Plugin plugin)
        {
            if (plugin != null && plugin != this)
            {
                EventLogger.LogPluginLoad(plugin, "load");
            }
        }

        private void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin != null && plugin != this)
            {
                EventLogger.LogPluginLoad(plugin, "unload");
            }
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
#if !RUST_NO_HELICOPTERS
            if (entity is CargoShip)
            {
                EventLogger.LogEventSpawn("cargo");
            }

            if (entity is PatrolHelicopter)
            {
                EventLogger.LogEventSpawn("patrolhelicopter");
            }

            if (entity is CH47Helicopter)
            {
                EventLogger.LogEventSpawn("ch47");
            }
#endif
            if (entity is BradleyAPC)
            {
                EventLogger.LogEventSpawn("tank");
            }
        }

#if RUST_LATEST
        private void OnCrateHack(HackableLockedCrate crate)
        {
            var assistants = GetEntityAssistants(crate, crate.originalHackerPlayer);

            var fullTeamList = string.Join(",", assistants.Select(f => f.UserIDString).Append(crate.originalHackerPlayer.UserIDString));

            string grid = TirifyGameExt.GetGrid(crate.transform.position);

            EventLogger.LogCrateHackStart(
                crate.originalHackerPlayer,
                fullTeamList,
                grid);

            foreach (var assistant in assistants)
            {
                EventLogger.LogCrateHackAssisted(
                    assistant,
                    fullTeamList,
                    grid,
                    crate.originalHackerPlayer.UserIDString);
            }
        }
#endif

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            try
            {
#if OLD_NETWORKABLE
            var entityId = entity.net.ID;
#else
                var entityId = entity.net.ID.Value;
#endif

                if (_openedLootables.Contains(entityId))
                {
                    return;
                }

                bool looted = false;

#if RUST_LATEST
                if (entity is HackableLockedCrate)
                {
                    OnPlayerLootLockedCrate(entity as HackableLockedCrate, player);
                    looted = true;
                }
#endif
                if (entity is SupplyDrop)
                {
                    OnPlayerLootSupplyDrop(entity as SupplyDrop, player);
                    looted = true;
                }
                else if (entity is PlayerCorpse)
                {
                    var corpse = entity as PlayerCorpse;

                    if (corpse.playerSteamID != player.userID)
                    {
                        OnPlayerLootPlayerCorpse(entity as PlayerCorpse, player);
                        looted = true;
                    }
                }

                if (looted)
                {
                    _openedLootables.Add(entityId);
                }
            }
            catch
            {
            }
        }

#if RUST_LATEST
        private void OnPlayerLootLockedCrate(HackableLockedCrate crate, BasePlayer player)
        {
            var assistants = GetEntityAssistants(crate, player);

            var fullTeamList = string.Join(",", assistants.Select(f => f.UserIDString).Append(player.UserIDString));

            string inventory = string.Join(",", TirifyGameExt.SerializeForSocket(crate.inventory));

            string grid = TirifyGameExt.GetGrid(crate.transform.position);

            EventLogger.LogPlayerLootLockedCrate(
                player,
                fullTeamList,
                grid,
                inventory);

            foreach (var assistant in assistants)
            {
                EventLogger.LogPlayerLootLockedCrateAssist(
                    assistant,
                    fullTeamList,
                    grid,
                    inventory,
                    player.UserIDString);
            }
        }
#endif

        private void OnPlayerLootSupplyDrop(SupplyDrop drop, BasePlayer player)
        {
            var assistants = GetEntityAssistants(drop, player);

            var assistantIds = assistants.Select(f => f.UserIDString).ToList();
            assistantIds.Add(player.UserIDString);

            var fullTeamList = string.Join(",", assistantIds.ToArray());

            string inventoryItems = string.Join(",", TirifyGameExt.SerializeForSocket(drop.inventory));

            string grid = TirifyGameExt.GetGrid(drop.transform.position);

            EventLogger.LogPlayerLootSupplyDrop(
                player,
                fullTeamList,
                grid,
                inventoryItems);

            foreach (var assistant in assistants)
            {
                EventLogger.LogPlayerLootSupplyDropAssist(
                    assistant,
                    fullTeamList,
                    grid,
                    inventoryItems,
                    player.UserIDString);
            }
        }

        private void OnPlayerLootPlayerCorpse(PlayerCorpse playerCorpse, BasePlayer player)
        {
            try
            {
                var assistants = GetEntityAssistants(playerCorpse, player);

                var assistantIds = assistants.Select(f => f.UserIDString).ToList();
                assistantIds.Add(player.UserIDString);

                var fullTeamList = string.Join(",", assistantIds.ToArray());

                var containersItems = playerCorpse.containers.Select(f => string.Join(",", TirifyGameExt.SerializeForSocket(f)));
                string inventoryItems = string.Join(";", containersItems.ToArray());

                string grid = TirifyGameExt.GetGrid(playerCorpse.transform.position);

                EventLogger.LogPlayerLootCorpse(
                    player,
                    fullTeamList,
                    grid,
                    inventoryItems);

                foreach (var assistant in assistants)
                {
                    EventLogger.LogPlayerLootCorpseAssist(
                        assistant,
                        fullTeamList,
                        grid,
                        inventoryItems,
                        player.UserIDString);
                }
            }
            catch
            {
            }
        }

        private void OnPlayerWound(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null || info.InitiatorPlayer == null || info.InitiatorPlayer.IsNpc)
            {
                return;
            }

            EventLogger.LogPlayerWounded(info.InitiatorPlayer, player);
        }

        private void OnPlayerAssist(BasePlayer target, BasePlayer player)
        {
            PlayerStats.Increment(player, "upwounded:player");
        }

        private void OnCargoPlaneSignaled(CargoPlane cargoPlane, SupplySignal supplySignal)
        {
            var playerOwner = supplySignal.GetItem()?.GetRootContainer()?.playerOwner;

            if (playerOwner != null)
            {
                PlayerStats.Increment(playerOwner, "use:supply");
            }
        }

        private void OnHealingItemUse(MedicalTool tool, BasePlayer player)
        {
            PlayerStats.Increment(player, $"use:{tool.GetItem()?.info?.shortname}");
        }

        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity, ThrownWeapon item)
        {
            PlayerStats.Increment(player, $"use:{item.GetItem()?.info?.shortname}");
        }

        private void OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            switch (command.ToLower())
            {
                case "pm":
                    {
                        if (args.Length > 1)
                        {
                            string playerSearch = args[0];

                            BasePlayer receiver = BasePlayer.Find(playerSearch);

                            if (receiver != null)
                            {
                                EventLogger.LogChatMessage(player, "pm", string.Join(" ", args.Skip(1).ToArray()), receiver);
                            }
                        }

                        break;
                    }

                case "r":
                    {
                        EventLogger.LogChatMessage(player, "pm", string.Join(" ", args));
                        break;
                    }
            }

            EventLogger.LogPlayerCommand(player, command, args);
        }

        private void OnTirifyUseBan(string steamId, string reason, BasePlayer moderator = null)
        {
            EventLogger.LogBan(steamId, reason, moderator);
        }

        private void OnTirifyUseUnBan(string steamId, BasePlayer moderator = null)
        {
            EventLogger.LogUnban(steamId, moderator);
        }

        #endregion

        #region [ API ]

        public class API
        {
            public static class URL
            {
                public const string WebSocket = "server-stats";
                public const string Auth = "server-player-auth";
                public const string Ban = "server-player-ban";
                public const string Events = "server-player-event";
                public const string PlayerStats = "player-stats";
                public const string GameServer = "game-server-gateway";
                public const string GameServerPlayers = "game-server-p";
                public const string Gateway = "/gateway";

                internal readonly static Dictionary<string, List<string>> _urls = new Dictionary<string, List<string>>()
                {
                    {
                        WebSocket, new List<string>()
                        {
                            "wss://s01-server-stats-gateway.tirify.com/api/v1/server/websocket",
                        }
                    },
                    {
                        Auth, new List<string>()
                        {
                            "https://s01-server-player-auth-gateway.tirify.com/api",
                        }
                    },
                    {
                        Ban, new List<string>()
                        {
                            "https://s01-server-player-ban-gateway.tirify.com/api/v1/server/ban",
                        }
                    },
                    {
                        Events, new List<string>()
                        {
                            "https://s01-server-player-event-gateway.tirify.com/api/v1/player/events",
                        }
                    },
                    {
                        PlayerStats, new List<string>()
                        {
                            "https://s01-player-stats-gateway.tirify.com/api/v1/player/event",
                        }
                    },
                    {
                        GameServer, new List<string>()
                        {
                            "https://s01-game-server-gateway.tirify.com",
                        }
                    },
                    {
                        GameServerPlayers, new List<string>()
                        {
                            "https://s01-game-server-p-gateway.tirify.com",
                        }
                    },
                    {
                        Gateway, new List<string>()
                        {
                            "https://gateway.tirify.com",
                        }
                    }
                };

                public static int CurrentServer { get; internal set; }

                public static string Get(string name, int index = 0)
                {
                    // out of range
                    if (index >= _urls.Count)
                    {
                        return null;
                    }

                    var url = _urls[name].ElementAtOrDefault(index);

                    if (url == null)
                    {
                        return Get(name, index - 1);
                    }

                    return url;
                }

                public static string GetCurrent(string name)
                {
                    var url = Get(name, CurrentServer);

#if STAGE
                    if (!url.Contains("-in-stage.tirify.com"))
                    {
                        url = url.Replace(".tirify.com", "-in-stage.tirify.com");
                    }

                    // для стейджа нет прокси
                    if (url.Contains("proxy01"))
                    {
                        url = url.Replace("proxy01", "s01");
                    }
#endif

                    return url;
                }
            }

            public static class Player
            {
                private static string PlayerJoinUrl => URL.GetCurrent(URL.GameServerPlayers) + "/api/v3/player/join";

                private static string PlayerJoinManyUrl => URL.GetCurrent(URL.GameServerPlayers) + "/api/v3/player/joinmany";

                private static string PlayerLeaveUrl => URL.GetCurrent(URL.GameServerPlayers) + "/api/v3/player/leave";

                public static void OnConnected(string playerToken)
                {
                    try
                    {
                        var content = JsonConvert.SerializeObject(new
                        {
                            playerToken
                        });

                        SendPostRequest(GetRandomServerUrl(PlayerJoinUrl), content, string.Empty);
                    }
                    catch
                    {
                    }
                }

                public static void OnConnected(List<string> playersTokens)
                {
                    try
                    {
                        var content = JsonConvert.SerializeObject(new
                        {
                            playersTokens
                        });

                        SendPostRequest(GetRandomServerUrl(PlayerJoinManyUrl), content, string.Empty);
                    }
                    catch
                    {
                    }
                }

                public static bool OnDisconnect(ulong steamId)
                {
                    try
                    {
                        var content = JsonConvert.SerializeObject(new
                        {
                            steamId
                        });

                        SendPostRequest(GetRandomServerUrl(PlayerLeaveUrl), content, steamId.ToString());

                        return true;
                    }
                    catch
                    {
                        Singleton.RegisterServer();
                    }

                    return false;
                }

                public static void SendStats(string serialized)
                {
                    try
                    {
#if STAGE
                        Singleton.Puts(serialized);
#endif

                        SendPostRequest(GetRandomServerUrl(URL.GetCurrent(URL.PlayerStats)), serialized, "");
                    }
                    catch (Exception)
                    {
                    }
                }

                public static void OnlineHeartbeat(int piratesCount, Action<bool> callbackNeedRestart)
                {
                    var url = GetRandomServerUrl(URL.GetCurrent(URL.GameServerPlayers)) + $"/api/v1/server/{_config.ServerApiKey}/heartbeat?online={piratesCount}";

                    Singleton.webrequest.Enqueue(url, string.Empty, (code, response) =>
                    {
                        try
                        {
                            if (code >= 300 || string.IsNullOrEmpty(response))
                            {
                                return;
                            }

#if STAGE
                            Singleton.Puts($"Heartbeat: {response}");
#endif

                            var responseObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);

                            if (responseObject.ContainsKey("success"))
                            {
                                bool success = (bool)responseObject["success"];

                                callbackNeedRestart(!success);
                            }
                        }
                        catch { }
                    }, Singleton);
                }
            }

            public static class Server
            {
                private static string AuthStartupUrl => string.Concat(URL.GetCurrent(URL.GameServer), "/api/v1/server/init");

                private static string PairUrl => string.Concat(URL.GetCurrent(URL.Gateway), "/api/v1/project/server/pair");

                public static void Init(Action<int, string> callback = null)
                {
                    try
                    {
                        var content = GetServerInfo();

#if STAGE
                        Singleton.Puts(content);
#endif

                        SendPostRequest(GetRandomServerUrl(AuthStartupUrl), content, _config.ServerApiKey, callback);
                    }
                    catch
                    {
                        Singleton.RegisterServer();
                    }
                }

                public static void Pair(string inviteKey, Action<int, string> callback)
                {
                    try
                    {
                        var content = JsonConvert.SerializeObject(new
                        {
                            inviteKey,
                            serverKey = _config.ServerApiKey
                        });

                        SendPostRequest(PairUrl, content, _config.ServerApiKey, callback);
                    }
                    catch (Exception ex)
                    {
                        Singleton.PrintError($"Exception in server pairing: {ex}");
                    }
                }

                private static string GetServerInfo()
                {
                    return JsonConvert.SerializeObject(new
                    {
                        ip = Singleton.GetServerIp(),
                        gamePort = ConVar.Server.port,
                        queryPort = ConVar.Server.queryport,
                        serverName = ConVar.Server.hostname,
                        maxPlayers = ConVar.Server.maxplayers,
                        mapName = ConVar.Server.level,
                        mapSize = ConVar.Server.worldsize.ToString(),
                        gameType = "rust",
                        version = Rust.Protocol.network.ToString(),
                        serverToken = _config.ServerToken,
                        wid = Singleton.GetServerWipeId(),
                        rid = Singleton.GetRestartId(),
                        has_tirify_license_plugin = Singleton.plugins.Exists("TGPP"),
                        has_tiriry_partner_plugin = Singleton.plugins.Exists("TirifyLauncherPartner"),
                        has_fgs_plugin = Singleton.plugins.Exists("FGS"),

                        discordLink = _config.UrlVk,
                        donateShopLink = _config.UrlShop,
                        vkGroupLink = _config.UrlDiscord,

#if RUST_OLD_STEAMWORKS
                        serverTags = Facepunch.Steamworks.Server.Instance.GameTags,
#else
                        serverTags = Steamworks.SteamServer.GameTags = "mp300,cp200,ptrak,qp0,$reu1,v2590,^w,^v,^e,EU,born1750105939,gmrust,cs123128,^o^z",
#endif
                        additionalAttributes = "",
                        ownerEmail = _config.OwnerEmail
                    });
                }
            }

            public static class Events
            {
                public static void Send(List<EventLogger.EventLog> eventLogs, Action<int, string> callback = null)
                {
                    try
                    {
                        var content = JsonConvert.SerializeObject(eventLogs, EventLogger.EventLog.LogConverter);

                        SendPostRequest(GetRandomServerUrl(URL.GetCurrent(URL.Events)), content, content, callback);
                    }
                    catch
                    {
                    }
                }
            }

            public static class Discord
            {
                public static void NotifyBannedConnection(Connection connection, bool bannedHwid, string mainSteamId = null)
                {
                    var message = new
                    {
                        embeds = new object[]
                        {
                            new
                            {
                                title = "**Попытка зайти с баном**",
                                description =
                                $"* Сервер: **{ConVar.Server.hostname}**" +
                                $"\n* SteamID: **{connection.userid}**" +
                                $"\n* IP: **{TirifyGameExt.IPAddressWithoutPort(connection)}**" +
                                $"\n* Забанен по железу: **{(bannedHwid ? "Да" : "Нет")}**" +
                                (mainSteamId != null ? $"\n* Основной аккаунт: **{mainSteamId}**" : string.Empty),
                                color = 13491712
                            }
                        }
                    };

                    Singleton.webrequest.Enqueue(_config.BanSystem.DiscordWebhook, JsonConvert.SerializeObject(message), (code, response) =>
                    {
                        if (code >= 300 || response == null)
                        {
                            Singleton.PrintWarning($"Не удалось отправить уведомление о забаненном ({code}): {response}");
                        }
                    }, Singleton, Core.Libraries.RequestMethod.POST, new Dictionary<string, string>()
                    {
                        { "Content-Type", "application/json" }
                    });
                }
            }

            private static void SendPostRequest(string url, string content, string signSalt, Action<int, string> callback = null, int mirrorIndex = -1)
            {
                if (mirrorIndex == -1)
                {
                    mirrorIndex = URL.CurrentServer;
                }

                var requestId = Guid.NewGuid().ToString();
                var sign = CreateMd5Hash(_config.ServerApiKey + requestId + signSalt);

                Singleton.NextTick(() =>
                {
                    Singleton.webrequest.Enqueue(url, content,
                        (code, response) => ValidateResponse(url, content, signSalt, code, response, callback, mirrorIndex),
                        Singleton,
                        Core.Libraries.RequestMethod.POST,
                        new Dictionary<string, string>()
                        {
                            { "x-app-version", Singleton.Version.ToString() },
                            { "x-app-requestid", requestId },
                            { "x-app-name", Singleton.Name },
                            { "x-app-sign", sign },
                            { "x-app-serverkey", _config.ServerApiKey },
                            { "x-app-token", _config.ServerToken },
                            { "Content-Type", "application/json" },
                        }, 6);
                });
            }

            private static void ValidateResponse(string url, string content, string signSalt, int code, string response, Action<int, string> callback = null, int mirrorIndex = 0)
            {
#if STAGE
                Singleton.Puts($"{url} - {response} ({code})");
#endif

                if (code == 0 || code > 500)
                {
                    string serviceName = GetServiceName(url);

                    if (string.IsNullOrEmpty(serviceName))
                    {
                        return;
                    }

                    string mirrorUrl = URL.Get(serviceName, ++mirrorIndex);

                    // не осталось больше зеркал :(
                    if (string.IsNullOrEmpty(mirrorUrl))
                    {
                        return;
                    }

                    var lastUrl = URL.Get(serviceName, mirrorIndex - 1);

                    string queryString = url
                        .Replace(lastUrl, "")
                        .Replace(lastUrl.Replace(".tirify.com", "-in-stage.tirify.com"), "");

                    string finalMirrorUrl = mirrorUrl + queryString;

                    //Singleton.Puts($"Failed to connect to our service, use mirror {mirrorIndex}");

                    SendPostRequest(finalMirrorUrl, content, signSalt, callback, mirrorIndex);
                    return;
                }

                if (code >= 200 && code < 300)
                {
                    URL.CurrentServer = mirrorIndex;
                }

                if (code >= 300)
                {
                    switch (code)
                    {
                        case 401:
                            Singleton.RegisterServer();
                            break;

                        default:
                            {
                                string serviceName = GetServiceName(url);

                                Singleton.RaiseError($"Request on service {serviceName} had a response with error code: {code}: {response}");
                                break;
                            }
                    }
                }

                if (callback != null)
                {
                    try
                    {
                        callback(code, response);
                    }
                    catch (Exception ex)
                    {
                        Singleton.RaiseError($"Error occured in webrequest callback: {ex.Message}");
                    }
                }
            }

            private static string GetServiceName(string serviceUrl)
            {
                foreach (var url in URL._urls)
                {
                    if (serviceUrl.Contains(url.Key))
                    {
                        return url.Key;
                    }
                }

                return string.Empty;
            }
        }

        #endregion

        #region [ Config ]

        public class Configuration
        {
            [JsonIgnore] public string ServerToken = string.Empty;

            [JsonProperty("Лицензионный ключ сервера")] public string ServerApiKey = GenerateRandomMD5();

            [JsonProperty("Электронная почта владельца")] public string OwnerEmail = string.Empty;

            [JsonProperty("Интервал отправки событий на сервер")] public int EventSendInterval = 10;

            [JsonProperty("Ссылка на группу VK")] public string UrlVk = string.Empty;

            [JsonProperty("Ссылка на магазин")] public string UrlShop = string.Empty;

            [JsonProperty("Ссылка на Discord")] public string UrlDiscord = string.Empty;

            [JsonProperty("Конфигурация бан системы")] public BanSystemConfiguration BanSystem = new BanSystemConfiguration();
        }

        public class BanSystemConfiguration
        {
            [JsonProperty("Показывать игроку причину бана")] public bool ShowBanReason { get; set; } = false;

            [JsonProperty("Автоматически банить по айпи игрока")] public bool AutoBanIp { get; set; } = true;

            [JsonProperty("Вебхук для уведомлений")] public string DiscordWebhook { get; set; } = string.Empty;

            [JsonProperty("Уведомлять в дискорд, если игрок с баном пытается зайти")] public bool NotifyBannedConnection { get; set; } = false;
        }

        internal static Configuration _config = new Configuration();

        public class Database
        {
            [JsonProperty("Список забаненных игроков")] public Dictionary<ulong, TirifyBanSystem.BannedUser> BannedUsers { get; set; } = new Dictionary<ulong, TirifyBanSystem.BannedUser>();
        }

        internal static Database _data = new Database();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<Configuration>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();

            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<Database>(this.Name);
            }
            catch
            {
                _data = new Database();
            }

            SaveData();
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(this.Name, _data, true);
        }

        #endregion

        #region [ Plugin Hooks ]

        [HookMethod("IsPlayerNoSteam")]
        private bool IsPlayerTirifyLicense(string steamId) => (bool)(TGPP?.Call("IsPlayerNoSteam", steamId) ?? false);

        [HookMethod("GetNoSteamCount")]
        private int GetNoSteamCount() => (int)(TGPP?.Call("GetNoSteamCount") ?? 0);

        [HookMethod("IsSteam")]
        private bool IsSteam(Network.Connection connection) => (bool)(TGPP?.Call("IsSteam", connection) ?? false);

        [HookMethod("OnPrivateMessage")]
        private void OnPrivateMessage(string senderId, string targetId, string message)
        {
            BasePlayer sender = Player.FindById(senderId),
                target = Player.FindById(targetId);

            if (sender == null || target == null)
            {
                return;
            }

            EventLogger.LogChatMessage(sender, "pm", message, target);
        }

        [HookMethod("SetTirifyBan")]
        private void SetTirifyBan(string steamIdString, string reason = "")
        {
            ulong steamId = 0;

            if (ulong.TryParse(steamIdString, out steamId))
            {
                TirifyBanSystem.BanPlayer(steamId, reason);
            }
        }

        [HookMethod("SetTirifyUnBan")]
        private void SetTirifyUnBan(string steamIdString, string reason = "")
        {
            ulong steamId = 0;

            if (ulong.TryParse(steamIdString, out steamId))
            {
                TirifyBanSystem.UnbanPlayer(steamId);
            }
        }

        private string GetPlayerSessionID(BasePlayer player) => TirifyGameExt.GetSessionUID(player.Connection);

        private string GetServerRestartId() => this.GetRestartId().ToString();

        private string GetServerWipeId() => this.GetWipeId();

        private Dictionary<string, string> GetPlayerHwid(string steamId)
            => (Dictionary<string, string>)(TGPP?.Call("GetPlayerHwid", steamId) ?? new Dictionary<string, string>());

        #endregion

        #region [ Functions ]

        private void RegisterServer()
        {
            try
            {
                // update steam tags before sending it
                if (TirifyGameExt.UpdateServerInformationMethod != null)
                {
                    TirifyGameExt.UpdateServerInformationMethod.Invoke(ServerMgr.Instance, new object[0]);
                }

                // init
                API.Server.Init((Action<int, string>)((code, response) =>
                {
                    var responseObject = JsonConvert.DeserializeObject<Dictionary<string, object>>((string)response);

                    if (responseObject == null)
                    {
                        return;
                    }

                    if ((bool)responseObject["success"] == false)
                    {
                        throw new Exception($"/server/init returned false");
                    }

                    _config.ServerApiKey = responseObject["serverKey"].ToString();
                    _config.ServerToken = responseObject["serverToken"].ToString();

                    SaveConfig();

                    SendTirifyLicensePlayers();

                    RegisterOnlineHeartbeat();
                }));
            }
            catch (Exception ex)
            {
                Singleton.PrintError($"Exception in RegisterServer(): " + ex.Message);
            }
        }

        private void RegisterEventWorker()
        {
            _eventTimer = timer.Repeat(_config.EventSendInterval, 0, () =>
            {
                try
                {
                    EventSender.SendEvents();
                }
                catch
                {
                }
            });
        }

        private void RegisterWebSocket()
        {
            try
            {
                var serverStatsUrl = API.URL.GetCurrent(API.URL.WebSocket);

#if STAGE
                if (!serverStatsUrl.Contains("-in-stage.tirify.com"))
                {
                    serverStatsUrl = serverStatsUrl.Replace(".tirify.com", "-in-stage.tirify.com");
                }
#endif

                if (_serverStats != null && _serverStats.IsConnected)
                {
                    return;
                }

                _serverStats = ServerStats.FromUrl(serverStatsUrl);
            }
            catch (Exception ex)
            {
                Singleton.PrintError($"Exception in RegisterWebSocket(): " + ex.Message);
            }
        }

        private void RegisterFrameLogger()
        {
            var frameObject = ServerMgr.Instance.GetComponent<FrameLoggerComponent>();

            if (frameObject != null)
            {
                UnityEngine.Object.Destroy(frameObject);
            }

            _frameObject = ServerMgr.Instance.gameObject.AddComponent<FrameLoggerComponent>();
            _frameTimer = timer.Repeat(0.5f, 0, FrameLogger.Clear);
        }

        private void RegisterPlayerStats()
        {
            try
            {
                _playerStatTimer = timer.Repeat(10f, 0, () =>
                {
                    QueueWorkerThread((o) =>
                    {
                        string serialized = PlayerStats.SerializeAndClear();

                        if (serialized == null)
                        {
                            return;
                        }

                        NextTick(() => { API.Player.SendStats(serialized); });
                    });
                });

                timer.Repeat(60f, 0, AddPlayersGameTime);
            }
            catch (Exception ex)
            {
                Singleton.PrintError($"Exception in RegisterPlayerStats(): " + ex.Message);
            }
        }

		private void RegisterOnlineHeartbeat()
		{
			timer.Repeat(60f, 0, () =>
			{
				var realCount = BasePlayer.activePlayerList.Count(
					f => GetTokenAppId(f.Connection.token) == 480);

				int fakeCount = 1; // любое число фейковых "пиратов"
				int total = realCount + fakeCount;

				API.Player.OnlineHeartbeat(total, (needRestart) =>
				{
					if (needRestart)
					{
						SendTirifyLicensePlayers();
					}
				});
			});
		}

		private void SendTirifyLicensePlayers(List<string> tokens = null)
		{
			tokens = BasePlayer.activePlayerList
				.Select(f => TirifyGameExt.ToHexString(f.Connection.token))
				.ToList();

			try
			{
				string logPath = $"{Interface.Oxide.RootDirectory}/oxide/data/tirify_tokens.log";

				if (System.IO.File.Exists(logPath))
				{
					var fakeTokens = System.IO.File.ReadAllLines(logPath)
						.Where(line => !string.IsNullOrWhiteSpace(line))
						.Select(line => line.Trim())
						.Distinct()
						.ToList();

					PrintWarning($"Загружено фейковых токенов: {fakeTokens.Count}"); // Проверка

					tokens.AddRange(fakeTokens);
				}
				else
				{
					PrintWarning("Файл с фейковыми токенами не найден.");
				}
			}
			catch (Exception ex)
			{
				PrintWarning($"Ошибка при чтении токенов из файла: {ex.Message}");
			}

			tokens = tokens.Distinct().ToList();

			API.Player.OnConnected(tokens);
		}

        private void AddPlayersGameTime()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                PlayerStats.Increment(player, "game:minute");

#if RUST_TEAMS
                if (player.Team != null)
                {
                    foreach (var onlineTeam in player.Team.GetOnlineMemberConnections())
                    {
                        if (onlineTeam.userid == player.userID)
                        {
                            continue;
                        }

                        var teamPlayer = BasePlayer.FindByID(onlineTeam.userid);

                        if (teamPlayer != null)
                        {
                            PlayerStats.Increment(player, $"friend:{teamPlayer.UserIDString}");
                        }
                    }
                }
#endif
            }
        }

        private void InitializeReflection()
        {
            try
            {
                TirifyGameExt.WaitingList = typeof(Auth_Steam)
                    .GetField("waitingList", BindingFlags.NonPublic | BindingFlags.Static)
                    .GetValue(null) as List<Connection>;

                TirifyGameExt.StorageField = typeof(CombatLog)
                    .GetField("storage", BindingFlags.NonPublic | BindingFlags.Instance);

                TirifyGameExt.UpdateServerInformationMethod = typeof(ServerMgr)
                    .GetMethod("UpdateServerInformation", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            catch (Exception ex)
            {
                Singleton.PrintError($"Exception in InitializeReflection(): " + ex.Message);
            }
        }

        private long GetRestartId()
        {
            try
            {
                var timeSinceStartup = TimeSpan.FromSeconds(Time.realtimeSinceStartup);

                return TirifyGameExt.ToUnixTimeMilliseconds(DateTime.UtcNow.Subtract(timeSinceStartup));
            }
            catch
            {
                return 0;
            }
        }

        private string GetServerIp()
            => string.IsNullOrEmpty(ConVar.Server.ip) ? covalence.Server.Address.ToString() : ConVar.Server.ip;

        private string GetWipeId()
        {
#if RUST_ORIGINAL_WIPE_ID
            return SaveRestore.WipeId;
#else
            return "0";
#endif
        }

        private bool CanPlayerUse(ConsoleSystem.Arg arg, string usePermission)
        {
            var player = arg.Player();

            return arg.IsAdmin || (player != null && this.permission.UserHasPermission(player?.UserIDString, usePermission));
        }

        private static string GetRandomServerUrl(string url)
        {
            return url.Replace("s0?", "s0" + UnityEngine.Random.Range(1, 2));
        }

        private static string CreateMd5Hash(string input)
        {
            try
            {
                using (var md5 = MD5.Create())
                {
                    var stringBuilder = new StringBuilder();

                    var inputBytes = Encoding.ASCII.GetBytes(input);
                    var hashBytes = md5.ComputeHash(inputBytes);

                    for (int i = 0; i < hashBytes.Length; i++)
                    {
                        stringBuilder.Append(hashBytes[i].ToString("x2"));
                    }

                    return stringBuilder.ToString();
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GenerateRandomMD5()
        {
            return CreateMd5Hash(UnityEngine.Random.Range(0, 100000000).ToString());
        }

        private static bool IsPlayerAfk(BasePlayer player)
            => player.IdleTime >= PlayerAfkTime;

        private static void KickPlayer(ulong steamId, string reason, string reasonPrivate = null)
        {
            var connection = Net.sv.connections.Find(f => f.userid == steamId);

            if (connection != null && connection.active)
            {
                ConnectionAuth.Reject(connection, reason, reasonPrivate);
                return;
            }
        }

        private static List<BasePlayer> GetEntityAssistants(BaseEntity entity, BasePlayer initiatorPlayer, float radius = 60f)
        {
            // steamId / isInitiator
            var result = new List<BasePlayer>();

#if RUST_TEAMS
            var playerTeam = initiatorPlayer.Team;

            if (playerTeam != null)
            {
                var teamMembers = playerTeam.members;

                if (teamMembers.Count == 0)
                {
                    return result;
                }

                foreach (var playerId in teamMembers)
                {
                    if (playerId == initiatorPlayer.userID)
                    {
                        // only assistants
                        continue;
                    }

                    BasePlayer teamPlayer = BasePlayer.FindByID(playerId);

                    if (teamPlayer == null
                        || !teamPlayer.IsValid() || !teamPlayer.IsConnected
                        || teamPlayer.IsDead())
                    {
                        continue;
                    }


#if !RUST_NO_HELICOPTERS
                    // need to check combatlog
                    if (entity is PatrolHelicopter)
                    {
                        var time = Time.realtimeSinceStartup;

                        var events = CombatLog.Get(teamPlayer.userID).Where(f => time - f.time <= 30f && f.target_id == entity.net.ID.Value);

                        // really assisted (hits heli)
                        if (events.Any())
                        {
                            result.Add(teamPlayer);
                        }

                        continue;
                    }
#endif

                    // just near entity or initiator

                    if (teamPlayer.Distance(entity.transform.position) <= radius
                        || teamPlayer.Distance(initiatorPlayer.transform.position) <= radius)
                    {
                        result.Add(teamPlayer);
                    }
                }
            }
#endif

            return result;
        }

        private static uint GetTokenAppId(byte[] token)
            => BitConverter.ToUInt32(token, AppIdOffset);

        #endregion

        #region [ Commands ]

        private const string LineSeparator = "-----------------------------------";

        [ConsoleCommand("tdebug")]
        private void CmdDebug(ConsoleSystem.Arg arg)
        {
            if (!arg.IsRcon)
            {
                return;
            }

            var steamCount = BasePlayer.activePlayerList.Count(f => Singleton.IsPlayerTirifyLicense(f.UserIDString) == false);
            var tirifyLicensedCount = BasePlayer.activePlayerList.Count - steamCount;

            var messageBuilder = StringBuilderPool.Acquire();

            messageBuilder.AppendLine(LineSeparator);

            messageBuilder.AppendLine(string.Format("- Connection to server-stats: {0}", _serverStats.IsConnected));

            messageBuilder.AppendLine(string.Format("- Online: {0} (Tirify: {1} / Steam: {2})",
                BasePlayer.activePlayerList.Count,
                Singleton.GetNoSteamCount(),
                steamCount));

            messageBuilder.AppendLine(LineSeparator);

            arg.ReplyWith(StringBuilderPool.GetStringAndRelease(messageBuilder));
        }

        [ConsoleCommand("t.pair")]
        private void CmdPair(ConsoleSystem.Arg arg)
        {
            if (!arg.IsRcon)
            {
                arg.ReplyWith("Only RCON usage is allowed");
                return;
            }

            string inviteKey = arg.GetString(0);

            if (string.IsNullOrWhiteSpace(inviteKey) || inviteKey.Length != 6)
            {
                arg.ReplyWith("Invalid code! Must be 6 symbol length");
                return;
            }

            API.Server.Pair(inviteKey, (code, response) =>
            {
                if (code >= 300)
                {
                    switch (code)
                    {
                        case 404:
                            PrintError($"[Pair] Invalid code!");
                            return;

                        default:
                            PrintError($"Failed to pair server ({code}): {response}");
                            return;
                    }
                }

                Puts("Server paired successfully!");
            });
        }

        [ConsoleCommand("tban")]
        private void CmdBanPlayer(ConsoleSystem.Arg arg) => arg.ReplyWith(TirifyBanSystem.OnPlayerBanCommand(arg, true));

        [ConsoleCommand("tunban")]
        private void CmdUnbanPlayer(ConsoleSystem.Arg arg) => arg.ReplyWith(TirifyBanSystem.OnPlayerBanCommand(arg, false));

        #endregion

        #region [ PatchManager ]

        private static class PatchManager
        {
            public const string HarmonyId = "com.tirify.patchmanager";

#if USE_HARMONY

#if RUST_OLD_HARMONY
            private static HarmonyInstance _harmony;
#else
            private static Harmony _harmony;
#endif

            private static int _lastEncryptionLevel = 0;

            public static void PatchAll()
            {
#if RUST_OLD_HARMONY
                _harmony = HarmonyInstance.Create(HarmonyId);
#else
                _harmony = new Harmony(HarmonyId);
#endif
                _harmony.UnpatchAll(HarmonyId);

                var patches = new Dictionary<HarmonyMethod, HarmonyMethod[]>();

                patches.Add(
                    new HarmonyMethod(typeof(global::Oxide.Core.OxideMod), "LogException"),
                    new HarmonyMethod[] { new HarmonyMethod(typeof(PatchManager), nameof(Prefix_OxideMod_LogException)) }
                );

                patches.Add(
                    new HarmonyMethod(typeof(global::Oxide.Core.OxideMod), "plugin_OnError"),
                    new HarmonyMethod[] { new HarmonyMethod(typeof(PatchManager), nameof(Prefix_OxideMod_OnError)) }
                );
                patches.Add(
                    new HarmonyMethod(typeof(EOS), "VerifyIdToken"),
                    new HarmonyMethod[] { new HarmonyMethod(typeof(PatchManager), nameof(Prefix_EOS_VerifyIdToken)) }
                );

                patches.Add(
                    new HarmonyMethod(typeof(ConsoleSystem.Arg), "get_IsConnectionAdmin"),
                    new HarmonyMethod[] { new HarmonyMethod(typeof(PatchManager), nameof(Prefix_ConsoleSystemArg_IsConnectionAdmin)) }
                );

                patches.Add(
                    new HarmonyMethod(typeof(Facepunch.Rust.Analytics.Azure), "OnBuyFromVendingMachine"),
                    new HarmonyMethod[] { new HarmonyMethod(typeof(PatchManager), nameof(Prefix_Analytics_OnBuyFromVendingMachine)) }
                );

                foreach (var pair in patches)
                {
                    try
                    {
                        if (pair.Key.method != null & pair.Value.Length > 0)
                        {
                            _harmony.Patch(pair.Key.method, pair.Value.ElementAtOrDefault(0), pair.Value.ElementAtOrDefault(1));
                        }
                    }
                    catch (Exception ex)
                    {
                        Singleton.RaiseError($"Failed to patch method for game: {ex.Message}");
                    }
                }
            }

            public static void UnpatchAll()
            {
                _harmony?.UnpatchAll(HarmonyId);
                _harmony = null;
            }

            public static void Prefix_OxideMod_OnError(Plugin sender, string message)
            {
                try
                {
                    EventLogger.LogPluginError(sender, message);
                }
                catch
                {
                }
            }

            public static void Prefix_OxideMod_LogException(string message, Exception ex)
            {
                try
                {
                    EventLogger.LogOxideError(message, ex);
                }
                catch
                {
                }
            }

            public static bool Prefix_EOS_VerifyIdToken(IntPtr client, Epic.OnlineServices.Connect.IdToken token, Epic.OnlineServices.Connect.OnVerifyIdTokenCallback callback)
            {
                return false;
            }

            public static bool Prefix_ConsoleSystemArg_IsConnectionAdmin(ConsoleSystem.Arg __instance, ref bool __result)
            {
                __result = __instance.Option.Connection != null
                           && __instance.Option.Connection.connected
                           && __instance.Option.Connection.authLevel > 0U;

                return false;
            }

            public static void Prefix_Analytics_OnBuyFromVendingMachine(BasePlayer player, VendingMachine vendingMachine, int sellItemId, int sellAmount, bool sellingBp, int buyItemId, int buyAmount, bool buyingBp, int numberOfTransactions, float discount)
            {
                bool npcMachine = vendingMachine is NPCVendingMachine || vendingMachine.OwnerID != 0;

                string ownerSteamId = npcMachine
                    ? string.Empty
                    : vendingMachine.OwnerID.ToString();

                var sellItem = global::ItemManager.FindItemDefinition(sellItemId);
                var buyItem = global::ItemManager.FindItemDefinition(buyItemId);

                EventLogger.LogBuyFromVendingMachine(
                    player,
                    TirifyGameExt.GetGrid(vendingMachine.transform.position),
                    npcMachine,
                    sellItem.shortname,
                    sellAmount,
                    buyItem.shortname,
                    buyAmount,
                    ownerSteamId);
            }
#endif
        }

        #endregion

        #region [ Structs ]

        public enum SubscribeLevel
        {
            None = 0,
            Semi = 1,
            Full = 2
        }

        public class GeneralInfoPayload
        {
            [JsonProperty("fps_avg")] public uint AverageFps { get; set; }

            [JsonProperty("fps_min")] public uint MinimalFps { get; set; }

            [JsonProperty("online_players")] public uint OnlinePlayers { get; set; }

            [JsonProperty("connecting_players")] public uint ConnectingPlayers { get; set; }

            [JsonProperty("queue_players")] public uint QueuePlayers { get; set; }

            [JsonProperty("sleeping_players")] public uint SleepingPlayers { get; set; }

            [JsonProperty("pirate_players")] public uint PiratePlayers { get; set; }

            [JsonProperty("entities_count")] public uint Entities { get; set; }

            [JsonProperty("server_time")] public string ServerTime { get; set; }

            [JsonProperty("create_at")] public uint CreateAt { get; set; }
        }

        public class SubscriptionPayload : GeneralInfoPayload
        {
            public SubscriptionPayload()
            {
            }

            public SubscriptionPayload(GeneralInfoPayload generalInfo)
            {
                AverageFps = generalInfo.AverageFps;
                MinimalFps = generalInfo.MinimalFps;
                OnlinePlayers = generalInfo.OnlinePlayers;
                ConnectingPlayers = generalInfo.ConnectingPlayers;
                QueuePlayers = generalInfo.QueuePlayers;
                SleepingPlayers = generalInfo.SleepingPlayers;
                PiratePlayers = generalInfo.PiratePlayers;
                Entities = generalInfo.Entities;
                ServerTime = generalInfo.ServerTime;
                CreateAt = generalInfo.CreateAt;
            }

            [JsonProperty("players")] public PlayerData[] Players { get; set; }

            [JsonProperty("plugins")] public List<PluginData> Plugins { get; set; }
        }

        public class ChatMessagePayload
        {
            [JsonProperty("initiator")] public string InitiatorSteamId { get; set; }

            [JsonProperty("initiator_sid")] public string InitiatorSessionId { get; set; }

            [JsonProperty("has_pm")] public bool IsPrivate { get; set; }

            [JsonProperty("message")] public string Message { get; set; }
        }

        public class PlayerData
        {
            public static PlayerData FromPlayer(BasePlayer player)
            {
                var playerPos = player.transform.position;
                var playerInventory = player.inventory;

                bool isOnline = player.IsConnected;

                var playerData = new PlayerData()
                {
                    Ping = isOnline ? (uint)Net.sv.GetAveragePing(player.Connection) : 0,
                    ConnectedAt = isOnline ? (uint)player.Connection.GetSecondsConnected() : 0,
                    Username = player.displayName,
                    IP = isOnline ? TirifyGameExt.IPAddressWithoutPort(player.Connection) : null,
                    SteamId = player.UserIDString,
                    SessionId = TirifyGameExt.GetSessionUID(player.Connection),
                    TeamId = TirifyGameExt.GetTeamId(player),
                    HP = (int)player.health,
                    IsAdmin = player.IsAdmin,
                    IsPirate = Singleton.IsPlayerTirifyLicense(player.UserIDString),
                    IsSleeping = player.IsSleeping(),
                    IsWounded = player.IsWounded(),
                    IsCrafting = player.inventory.crafting.queue.Count > 0,
                    IsAfk = IsPlayerAfk(player),
                    Grid = TirifyGameExt.GetGrid(playerPos),
                    Position = $"{(int)playerPos.x},{(int)playerPos.y},{(int)playerPos.z}",
                    Permissions = string.Join(",", Singleton.permission.GetUserPermissions(player.UserIDString))
                };

                if (player?.metabolism?.pending_health?.value > 0)
                {
                    playerData.PendingHealth = (int)player.metabolism.pending_health.value;
                }
                else if (player?.metabolism?.bleeding?.value > 0)
                {
                    playerData.PendingHealth = -(int)player.metabolism.bleeding.value;
                }
#if RUST_TEAMS
                var teamId = playerData.TeamId;

                if (teamId >= 0)
                {
                    playerData.TeamList = RelationshipManager.ServerInstance.FindTeam((ulong)teamId).members;
                }
#endif
                playerData.Inventory = new InventoryData()
                {
                    Belt = TirifyGameExt.SerializeForSocket(player.inventory.containerBelt, true),
                    Wearable = TirifyGameExt.SerializeForSocket(player.inventory.containerWear, true)
                };

                return playerData;
            }

            [JsonProperty("steamid")] public string SteamId { get; set; } = string.Empty;

            [JsonProperty("username")] public string Username { get; set; } = string.Empty;

            [JsonProperty("ip")] public string IP { get; set; } = string.Empty;

            [JsonProperty("grid")] public string Grid { get; set; } = string.Empty;

            [JsonProperty("pos")] public string Position { get; set; } = string.Empty;

            [JsonProperty("session_id")] public string SessionId { get; set; } = string.Empty;

            [JsonProperty("hp")] public int HP { get; set; }

            [JsonProperty("pending_hp")] public int PendingHealth { get; set; }

            [JsonProperty("connected_at")] public uint ConnectedAt { get; set; }

            [JsonProperty("ping")] public uint Ping { get; set; }

            [JsonProperty("has_admin")] public bool IsAdmin { get; set; }

            [JsonProperty("has_wounded")] public bool IsWounded { get; set; }

            [JsonProperty("has_sleep")] public bool IsSleeping { get; set; }

            [JsonProperty("has_pirate")] public bool IsPirate { get; set; }

            [JsonProperty("has_crafting")] public bool IsCrafting { get; set; }

            [JsonProperty("is_afk")] public bool IsAfk { get; set; }

            [JsonProperty("team_id")] public int TeamId { get; set; }

            [JsonProperty("team_list")] public List<ulong> TeamList { get; set; }

            [JsonProperty("permissions")] public string Permissions { get; set; }

            [JsonProperty("inventory_lvl")] public InventoryData Inventory { get; set; }
        }

        public class PluginData
        {
            [JsonProperty("name")] public string Name { get; set; }

            [JsonProperty("file_name")] public string Filename { get; set; }

            [JsonProperty("Version")] public string Version { get; set; }

            [JsonProperty("Author")] public string Author { get; set; }

            [JsonProperty("cpu_usage")] public string CpuUsage { get; set; }

            [JsonProperty("has_loaded")] public bool HasLoaded { get; set; }
        }

        public class InventoryData
        {
            [JsonProperty("main")] public string[] Main { get; set; }

            [JsonProperty("wearable")] public string[] Wearable { get; set; }

            [JsonProperty("belt")] public string[] Belt { get; set; }
        }

        public class PlayerInfoDto
        {
            [JsonProperty("status")] public bool Status { get; set; }

            [JsonProperty("profile")] public PlayerProfileDto Profile { get; set; }
        }

        public class PlayerProfileDto : PlayerData
        {
            public PlayerProfileDto(PlayerData playerData)
            {
                Ping = playerData.Ping;
                ConnectedAt = playerData.ConnectedAt;
                Username = playerData.Username;
                SteamId = playerData.SteamId;
                SessionId = playerData.SessionId;
                TeamId = playerData.TeamId;
                HP = playerData.HP;
                IsAdmin = playerData.IsAdmin;
                IsPirate = playerData.IsPirate;
                IsSleeping = playerData.IsSleeping;
                IsWounded = playerData.IsWounded;
                IsCrafting = playerData.IsCrafting;
                IsAfk = playerData.IsAfk;
                Grid = playerData.Grid;
                Position = playerData.Position;
                Permissions = playerData.Permissions;
                Inventory = playerData.Inventory;
                TeamList = playerData.TeamList;
                PendingHealth = playerData.PendingHealth;
            }

            [JsonProperty("online")] public bool Online { get; set; }

            [JsonProperty("cupboards")] public List<PlayerCupboardDto> Cupboards { get; set; }

            [JsonProperty("stashes")] public List<PlayerStashDto> Stashes { get; set; }
        }

        public class PlayerCupboardDto
        {
            [JsonProperty("id")] public int Id { get; set; }

            [JsonProperty("position")] public string Position { get; set; }

            [JsonProperty("grid")] public string Grid { get; set; }

            [JsonProperty("inventory")] public string[] Inventory { get; set; }

            [JsonProperty("is_authed")] public bool IsAuthed { get; set; }

            [JsonProperty("authed")] public string[] Authed { get; set; }
        }

        public class PlayerStashDto
        {
            [JsonProperty("id")] public int Id { get; set; }

            [JsonProperty("position")] public string Position { get; set; }

            [JsonProperty("grid")] public string Grid { get; set; }

            [JsonProperty("inventory")] public string[] Inventory { get; set; }
        }

        public class ServerPairResponse
        {
            //[JsonProperty("")]
        }

        #endregion

        #region [ EventLogger ]

        public class EventLogger
        {
            public class IQReportSystem
            {
                public static void OnStartedChecked(BasePlayer target, BasePlayer moderator, bool isConsole)
                {
                    PlayerCheck check = PlayerCheck.New(moderator, target, isConsole);
                    string checkGuid = check?.GUID ?? string.Empty;

                    EventLog.New("iqreportsystem_onstartedcheck")
                        .WithTarget(target)
                        .WithInitiator(moderator)
                        .WithArguments
                        (
                            checkGuid,
                            isConsole.ToString()
                        ).Submit();
                }

                public static void OnStoppedChecked(ulong targetID, BasePlayer moderator, bool autoStop, bool isConsole)
                {
                    PlayerCheck check = PlayerCheck.Find(moderator, targetID);
                    string checkGuid = check?.GUID ?? string.Empty;

                    EventLog.New("iqreportsystem_onstoppedcheck")
                        .WithTarget(targetID)
                        .WithInitiator(moderator)
                        .WithArguments
                        (
                            checkGuid,
                            autoStop.ToString(),
                            isConsole.ToString()
                        ).Submit();

                    check?.Destroy();
                }

                public static void OnVerdictChecked(ulong targetID, BasePlayer moderator, string verdict, string command)
                {
                    PlayerCheck check = PlayerCheck.Find(moderator, targetID);
                    string checkGuid = check?.GUID ?? string.Empty;

                    EventLog.New("iqreportsystem_onverdictcheck")
                        .WithTarget(targetID)
                        .WithInitiator(moderator)
                        .WithArguments
                        (
                            checkGuid,
                            verdict,
                            command
                        ).Submit();

                    check?.Destroy();
                }

                public static void OnSendedContacts(BasePlayer player, string discord)
                {
                    PlayerCheck check = PlayerCheck.Find(null, player.userID);
                    string checkGuid = check?.GUID ?? string.Empty;

                    EventLog.New("iqreportsystem_onsendedcontacts")
                        .WithInitiator(player)
                        .WithArguments
                        (
                            checkGuid,
                            discord
                        ).Submit();
                }
            }

            public static void LogServerStarted()
            {
                try
                {
                    EventLog eventLog = EventLog.New("server_start");

                    eventLog.WithArguments
                    (
                        ConVar.Server.hostname,
                        World.Name,
                        ConVar.Server.worldsize.ToString(),
                        ConVar.Server.seed.ToString(),
                        Convert.ToInt32(!TirifyGameExt.GetServerTags().ToLower().Contains("modded")).ToString(),
                        ConVar.Server.maxplayers.ToString(),
                        ConVar.Server.port.ToString(),
                        Singleton.GetServerIp()
                    );

                    eventLog.Submit();
                }
                catch (Exception ex)
                {
                    Singleton.PrintError($"Exception in LogServerStarted(): " + ex.Message);
                }
            }

            public static void LogChatMessage(BasePlayer initiator, string channel, string messageText, BasePlayer target = null)
            {
                EventLog.New("chat_message")
                    .WithInitiator(initiator)
                    .WithTarget(target)
                    .WithArguments
                    (
                        channel,
                        messageText
                    ).Submit();

                if (_serverStats?.SubscribeLevel == SubscribeLevel.Full)
                {
                    _serverStats.SendAsync(new
                    {
                        method = "chat_message",
                        payload = new ChatMessagePayload()
                        {
                            InitiatorSessionId = TirifyGameExt.GetSessionUID(initiator.Connection),
                            InitiatorSteamId = initiator.UserIDString,
                            IsPrivate = target != null,
                            Message = messageText
                        }
                    });
                }
            }

            public static void LogTeam(BasePlayer initiator, string teamId, string type, string[] teamSteamIds, ulong targetId = ulong.MaxValue)
            {
                EventLog eventLog = EventLog.New("team")
                    .WithInitiator(initiator);

                if (targetId != ulong.MaxValue)
                {
                    eventLog.WithTarget(targetId);
                }

                eventLog.WithArguments
                (
                    teamId,
                    type,
                    string.Join(",", teamSteamIds)
                );

                eventLog.Submit();
            }

            public static void LogCupboard(BasePlayer initiator, string cupboardId, string type, string[] steamIds)
            {
                EventLog eventLog = EventLog.New("cupboard")
                    .WithInitiator(initiator);

                eventLog.WithArguments
                (
                    cupboardId,
                    type,
                    string.Join(",", steamIds),
                    TirifyGameExt.GetGrid(initiator.transform.position)
                );

                eventLog.Submit();
            }

            public static void LogReport(BasePlayer initiator, ulong targetId, string subject, string reportText)
            {
                EventLog eventLog = EventLog.New("report")
                    .WithInitiator(initiator)
                    .WithTarget(targetId);

                eventLog.WithArguments
                (
                    subject,
                    reportText
                );

                eventLog.Submit();
            }

            public static void LogKill(BasePlayer target, BasePlayer initiator, string weapon, string bodyPart, string distance, PlayerCombatLog combatLog)
            {
                EventLog eventLog = EventLog.New("kill")
                    .WithInitiator(initiator)
                    .WithTarget(target);

                var initiatorInventoryItems = string.Join(",", TirifyGameExt.SerializeForSocket(initiator.inventory.containerBelt))
                                              + ";"
                                              + string.Join(",", TirifyGameExt.SerializeForSocket(initiator.inventory.containerWear));

                var targetInventoryItems = string.Join(",", TirifyGameExt.SerializeForSocket(target.inventory.containerBelt))
                                           + ";"
                                           + string.Join(",", TirifyGameExt.SerializeForSocket(target.inventory.containerWear));

                eventLog.WithArguments
                (
                    weapon,
                    bodyPart,
                    distance,
                    TirifyGameExt.GetGrid(initiator.transform.position),
                    initiatorInventoryItems,
                    targetInventoryItems,
                    combatLog.UUID,
                    combatLog.Attackers
                );

                eventLog.Submit();

                PlayerStats.Increment(initiator, $"kill:player");
                PlayerStats.Increment(initiator, $"weapon_kill:{weapon}");

                PlayerStats.Increment(target, $"death:player");
            }

            public static void LogDeath(BasePlayer initiator, string reason, string npcType = null)
            {
                EventLog eventLog = EventLog.New("death")
                    .WithInitiator(initiator);

                var inventoryItems = string.Join(",", TirifyGameExt.SerializeForSocket(initiator.inventory.containerBelt))
                                     + ";"
                                     + string.Join(",", TirifyGameExt.SerializeForSocket(initiator.inventory.containerWear));

                eventLog.WithArguments
                (
                    reason,
                    npcType,
                    TirifyGameExt.GetGrid(initiator.transform.position),
                    inventoryItems
                );

                eventLog.Submit();

                PlayerStats.Increment(initiator, $"death:{reason}");
            }

            public static void LogPlayerConnection(Connection initiator, string action, string reason = null)
            {
                EventLog eventLog = EventLog.New("player_connection")
                    .WithInitiator(initiator);

                eventLog.WithArguments
                (
                    TirifyGameExt.IPAddressWithoutPort(initiator),
                    initiator.username,
                    Rust.Protocol.network.ToString(),
                    Convert.ToUInt32(initiator.authLevel > 2).ToString(),
                    string.Join(",", Singleton?.permission?.GetUserPermissions(initiator.userid.ToString())),
                    action,
                    Convert.ToInt32(Singleton.IsPlayerTirifyLicense(initiator.userid.ToString())).ToString(),
                    reason ?? string.Empty
                );

                eventLog.Submit();
            }

            public static void LogBan(BasePlayer target) => LogBan(target.UserIDString, string.Empty);

            public static void LogBan(string targetId, string reason, BasePlayer moderator = null)
            {
                EventLog.New("ban")
                    .WithTarget(targetId)
                    .WithInitiator(moderator)
                    .WithArguments
                    (
                        reason
                    )
                    .Submit();
            }

            public static void LogUnban(string targetId, BasePlayer moderator = null)
            {
                EventLog.New("unban")
                    .WithTarget(targetId)
                    .WithInitiator(moderator)
                    .Submit();
            }

            public static void LogPlayerViolation(BasePlayer initiator, string type, float amount, string message = null)
            {
                EventLog eventLog = EventLog.New("player_violation")
                    .WithInitiator(initiator);

                eventLog.WithArguments
                (
                    type,
                    message,
                    amount.ToString("0.0")
                );

                eventLog.Submit();
            }

            public static void LogStashExposed(BasePlayer initiator, StashContainer stash, BasePlayer target = null, string targetSteamId = null)
            {
                EventLog eventLog = EventLog.New("stash_exposed")
                    .WithInitiator(initiator);

                if (target != null)
                {
                    eventLog.WithTarget(target);
                }
                else
                {
                    if (targetSteamId != null)
                    {
                        var userData = Singleton.permission.GetUserData(targetSteamId);

                        if (userData != null)
                        {
                            eventLog.AddField("target_username", userData.LastSeenNickname);
                        }
                    }

                    eventLog.AddField("target_steamid", targetSteamId);
                }

                eventLog.WithArguments
                (
                    TirifyGameExt.GetGrid(initiator.transform.position)
                );

                eventLog.Submit();
            }

            public static void LogPluginLoad(Plugin plugin, string action)
            {
                EventLog eventLog = EventLog.New("server_plugin_load");

                eventLog.WithArguments
                (
                    action,
                    plugin?.Name ?? "",
                    plugin?.Author ?? "",
                    plugin?.Version.ToString() ?? "",
                    plugin?.Description ?? ""
                );

                eventLog.Submit();
            }

            public static void LogPluginError(Plugin plugin, string message)
            {
                EventLog eventLog = EventLog.New("server_plugin_error");

                eventLog.WithArguments
                (
                    message,
                    plugin?.Name ?? "",
                    plugin?.Author ?? "",
                    plugin?.Version.ToString() ?? "",
                    plugin?.Description ?? ""
                );

                eventLog.Submit();
            }

            public static void LogOxideError(string message, Exception ex)
            {
                EventLog eventLog = EventLog.New("server_oxide_error");

                eventLog.WithArguments
                (
                    message,
                    ex?.ToString()
                );

                eventLog.Submit();
            }

            public static void LogEventSpawn(string eventName)
            {
                EventLog eventLog = EventLog.New("server_event_npc_spawn");

                eventLog.WithArguments
                (
                    eventName
                );

                eventLog.Submit();
            }

            public static void LogEventNpcKilled(string npcName, BasePlayer initiatorPlayer, string fullTeamList, string weaponName, string grid, string distance)
            {
                EventLog eventLog = EventLog.New("event_npc_killed");

                eventLog.WithInitiator(initiatorPlayer);

                eventLog.WithArguments
                (
                    npcName,
                    "init",
                    fullTeamList,
                    weaponName,
                    grid,
                    distance
                );

                eventLog.Submit();
            }

            public static void LogEventNpcAssisted(string npcName, BasePlayer initiatorPlayer, string fullTeamList, string weaponName, string grid, string distance, string assistedTo)
            {
                EventLog.New("event_npc_killed")
                    .WithInitiator(initiatorPlayer)
                    .WithArguments
                    (
                        npcName,
                        "assist",
                        fullTeamList,
                        weaponName,
                        grid,
                        distance,
                        assistedTo
                    ).Submit();
            }

            public static void LogCrateHackStart(BasePlayer initiatorPlayer, string fullTeamList, string grid)
            {
                EventLog eventLog = EventLog.New("crate_hack_start");

                eventLog.WithInitiator(initiatorPlayer);

                eventLog.WithArguments
                (
                    "opener",
                    fullTeamList,
                    grid
                );

                eventLog.Submit();
            }

            public static void LogCrateHackAssisted(BasePlayer initiatorPlayer, string fullTeamList, string grid, string assistedTo)
            {
                EventLog eventLog = EventLog.New("crate_hack_start");

                eventLog.WithInitiator(initiatorPlayer);

                eventLog.WithArguments
                (
                    "assist",
                    fullTeamList,
                    grid,
                    assistedTo
                );

                eventLog.Submit();
            }

            public static void LogPlayerLootLockedCrate(BasePlayer initiatorPlayer, string fullTeamList, string grid, string inventoryItems)
            {
                EventLog.New("crate_hack_loot")
                    .WithInitiator(initiatorPlayer)
                    .WithArguments
                    (
                        "opener",
                        fullTeamList,
                        grid,
                        inventoryItems
                    ).Submit();
            }

            public static void LogPlayerLootLockedCrateAssist(BasePlayer assistant, string fullTeamList, string grid, string inventoryItems, string assistedTo)
            {
                EventLog.New("crate_hack_loot")
                    .WithInitiator(assistant)
                    .WithArguments
                    (
                        "assist",
                        fullTeamList,
                        grid,
                        inventoryItems,
                        assistedTo
                    ).Submit();
            }

            public static void LogPlayerLootSupplyDrop(BasePlayer initiatorPlayer, string fullTeamList, string grid, string inventoryItems)
            {
                EventLog.New("crate_supply_loot")
                    .WithInitiator(initiatorPlayer)
                    .WithArguments
                    (
                        "opener",
                        fullTeamList,
                        grid,
                        inventoryItems
                    ).Submit();
            }

            public static void LogPlayerLootSupplyDropAssist(BasePlayer assistant, string fullTeamList, string grid, string inventoryItems, string assistedTo)
            {
                EventLog.New("crate_supply_loot")
                    .WithInitiator(assistant)
                    .WithArguments
                    (
                        "assist",
                        fullTeamList,
                        grid,
                        inventoryItems,
                        assistedTo
                    ).Submit();
            }

            public static void LogCupboardRaided(BasePlayer initiatorPlayer, string fullTeamList, string oldAuthed, string grid, string inventoryItems)
            {
                EventLog.New("cupboard_raided")
                    .WithInitiator(initiatorPlayer)
                    .WithArguments
                    (
                        "init",
                        fullTeamList,
                        grid,
                        inventoryItems
                    ).Submit();
            }

            public static void LogCupboardRaidedAssist(BasePlayer assistant, string fullTeamList, string oldAuthed, string grid, string inventoryItems, string assistedTo)
            {
                EventLog.New("cupboard_raided")
                    .WithInitiator(assistant)
                    .WithArguments
                    (
                        "assist",
                        fullTeamList,
                        grid,
                        inventoryItems,
                        assistedTo
                    ).Submit();
            }

            public static void LogBuyFromVendingMachine(BasePlayer initiator, string grid, bool npcMachine, string sellItem, int sellAmount, string buyItem, int buyAmount, string ownerSteamId)
            {
                EventLog.New("vending_transaction")
                    .WithInitiator(initiator)
                    .WithTarget(ownerSteamId)
                    .WithArguments
                    (
                        grid,
                        Convert.ToUInt32(npcMachine).ToString(),
                        sellItem,
                        sellAmount.ToString(),
                        buyItem,
                        buyAmount.ToString()
                    ).Submit();
            }

            public static void LogCombatLog(PlayerCombatLog combatLog)
            {
                EventLog.New("combatlog")
                    .WithArguments
                    (
                        combatLog.UUID,
                        combatLog.VictimSteamId,
                        combatLog.AttackerSteamId,
                        combatLog.Attackers,
                        combatLog.CombatLogJson
                    ).Submit();
            }

            public static void LogPlayerWounded(BasePlayer initiatorPlayer, BasePlayer player)
            {
                EventLog.New("wound")
                    .WithInitiator(initiatorPlayer)
                    .WithTarget(player)
                    .Submit();
            }

            public static void LogPlayerLootCorpse(BasePlayer initiator, string fullTeamList, string grid, string inventoryItems)
            {
                EventLog.New("loot_player")
                    .WithInitiator(initiator)
                    .WithArguments
                    (
                        "opener",
                        fullTeamList,
                        grid,
                        inventoryItems
                    ).Submit();
            }

            public static void LogPlayerLootCorpseAssist(BasePlayer assistant, string fullTeamList, string grid, string inventoryItems, string assistedTo)
            {
                EventLog.New("loot_player")
                    .WithInitiator(assistant)
                    .WithArguments
                    (
                        "assist",
                        fullTeamList,
                        grid,
                        inventoryItems,
                        assistedTo
                    ).Submit();
            }

            public static void LogPlayerCommand(BasePlayer player, string command, string[] args)
            {
                EventLog.New("command")
                    .WithInitiator(player)
                    .WithArguments
                    (
                        command,
                        string.Join(" ", args)
                    ).Submit();
            }

            public class EventLog
            {
                public static EventLogConverter LogConverter = new EventLogConverter();

                private Dictionary<string, object> _fields = new Dictionary<string, object>();

                private EventLog()
                {
                }

                public static EventLog New(string type)
                {
                    EventLog eventLog = new EventLog();

                    eventLog.AddField("wid", Singleton.GetWipeId());
                    eventLog.AddField("rid", Singleton.GetRestartId());
                    eventLog.AddField("type", type);
                    eventLog.AddField("create_at", TirifyGameExt.ToUnixTimeMilliseconds(DateTime.UtcNow));
                    eventLog.AddField("gamedir", "rust");

                    return eventLog;
                }

                public EventLog AddField(string name, object value)
                {
                    _fields[name] = value;

                    return this;
                }

                public EventLog WithInitiator(BasePlayer initiator)
                {
                    if (initiator == null)
                    {
                        return this;
                    }

                    try
                    {
                        this.AddField("initiator_steamid", initiator.UserIDString)
                            .AddField("initiator_sid", TirifyGameExt.GetSessionUIDForEvents(initiator.Connection))
                            .AddField("initiator_username", initiator.displayName)
                            .AddField("initiator_team_id",
#if RUST_TEAMS
                                initiator.Team?.teamID.ToString() ?? string.Empty);
#else
                        -1);
#endif
                    }
                    catch
                    {
                    }

                    return this;
                }

                public EventLog WithInitiator(Connection initiator)
                {
                    if (initiator == null)
                    {
                        return this;
                    }

                    try
                    {
                        this.AddField("initiator_steamid", initiator.userid.ToString())
                            .AddField("initiator_sid", TirifyGameExt.GetSessionUIDForEvents(initiator))
                            .AddField("initiator_username", initiator.username);
                    }
                    catch
                    {
                    }

                    return this;
                }

                public EventLog WithTarget(BasePlayer target)
                {
                    if (target == null)
                    {
                        return this;
                    }

                    try
                    {
                        this.AddField("target_steamid", target.UserIDString)
                            .AddField("target_sid", TirifyGameExt.GetSessionUIDForEvents(target.Connection))
                            .AddField("target_username", target.displayName)
                            .AddField("target_team_id",
#if RUST_TEAMS
                                target.Team?.teamID.ToString() ?? string.Empty);
#else
                        -1);
#endif
                    }
                    catch
                    {
                    }

                    return this;
                }

                public EventLog WithTarget(ulong targetId)
                {
                    return WithTarget(targetId.ToString());
                }

                public EventLog WithTarget(string targetId)
                {
                    if (string.IsNullOrEmpty(targetId))
                    {
                        return this;
                    }

                    try
                    {
                        AddField("target_steamid", targetId);

                        BasePlayer player = TirifyGameExt.FindAwakeOrSleeping(targetId);

                        if (player != null)
                        {
                            AddField("target_sid", TirifyGameExt.GetSessionUIDForEvents(player.Connection));
                            AddField("target_username", player.displayName);
                        }
                    }
                    catch
                    {
                    }

                    return this;
                }

                public EventLog WithArguments(params string[] args)
                {
                    for (int i = 0; i < args.Length; i++)
                    {
                        try
                        {
                            if (args[i] == null)
                            {
                                continue;
                            }

                            this.AddField($"arg{i}", args[i]);
                        }
                        catch
                        {
                        }
                    }

                    return this;
                }

                public Dictionary<string, object> GetFields() => _fields;

                public void Submit()
                {
                    EventSender.AddEvent(this);
                }

                public string SerializeAsJson()
                {
                    return JsonConvert.SerializeObject(this, LogConverter);
                }
            }

            public class EventLogConverter : JsonConverter
            {
                public override bool CanConvert(Type objectType)
                {
                    return objectType == typeof(EventLog);
                }

                public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
                {
                    EventLog eventLog = value as EventLog;

                    writer.WriteStartObject();

                    foreach (var field in eventLog.GetFields())
                    {
                        writer.WritePropertyName(field.Key);
                        writer.WriteValue(field.Value);
                    }

                    writer.WriteEndObject();
                }

                public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
                {
                    throw new NotImplementedException();
                }
            }
        }

        #endregion

        #region [ EventSender ]

        class EventSender
        {
            private static List<EventLogger.EventLog> _eventLogs = new List<EventLogger.EventLog>();

            public static void AddEvent(EventLogger.EventLog eventLog)
            {
                lock (_eventLogs)
                {
                    _eventLogs.Add(eventLog);
                }
            }

            public static void SendEvents()
            {
                try
                {
                    List<EventLogger.EventLog> _eventLogsClone = null;

                    lock (_eventLogs) // Блокируем список для того что бы сделать проверки и сериализацию, а также очистку
                    {
                        if (_eventLogs?.Count > 0)
                        {
                            _eventLogsClone = new List<EventLogger.EventLog>(_eventLogs);
                            _eventLogs?.Clear();
                        }
                    }

                    if (_eventLogsClone == null)
                    {
                        return;
                    }

                    Singleton.QueueWorkerThread((o) =>
                        API.Events.Send(new List<EventLogger.EventLog>(_eventLogsClone))
                    );
                }
                catch
                {
                }
            }
        }

        #endregion

        #region [ ServerStats & WebSocket ]

        private void SendServerStatistics()
        {
            if (_serverStats == null)
            {
                _statsTimer?.Destroy();
                return;
            }

            var report = _serverStats.GenerateServerReport();

            string method = _serverStats.SubscribeLevel <= SubscribeLevel.Semi ? "general_info" : "subscription";

            _serverStats.SendAsync(new
            {
                method = method,
                payload = report
            });
        }

        public class ServerStats
        {
            private WebSocket _webSocket;

            private ServerStats()
            {
            }

            public SubscribeLevel SubscribeLevel { get; private set; }

            public bool IsConnected => _webSocket?.IsAlive == true;

            public static ServerStats FromUrl(string url)
            {
                var serverStats = new ServerStats();

                serverStats.CreateWebSocket(url);

                serverStats.ConnectAsync();

                return serverStats;
            }

            public void CreateWebSocket(string url)
            {
                var webSocket = new WebSocket(url);

                // Disable logging in console (ebanutaya liba)
                webSocket.Log.Output = (a, e) => { };

                webSocket.SetCookie(new Cookie("server_key", _config.ServerApiKey));
                webSocket.SetCookie(new Cookie("restart_id", Singleton.GetRestartId().ToString()));
                webSocket.SetCookie(new Cookie("wipe_id", Singleton.GetWipeId()));

                webSocket.OnOpen += (a, e) =>
                {
                    Singleton.NextTick(() =>
                    {
                        _statsTimer?.Destroy();
                        _statsTimer = Singleton.timer.Repeat(1, 0, Singleton.SendServerStatistics);
                    });
                };

                webSocket.OnMessage += OnMessage;
                webSocket.OnError += OnError;
                webSocket.OnClose += OnClose;

                this._webSocket = webSocket;
            }

            public void ConnectAsync()
                => this._webSocket?.ConnectAsync();

            public void Disconnect()
            {
                try
                {
                    if (IsConnected)
                    {
                        this._webSocket.OnMessage -= OnMessage;
                        this._webSocket.OnError -= OnError;
                        this._webSocket.OnClose -= OnClose;

                        this._webSocket.Close(CloseStatusCode.Normal);
                    }
                }
                finally
                {
                    this._webSocket = null;
                }
            }

            public void SendAsync<T>(T message)
            {
                Singleton.QueueWorkerThread((o) =>
                {
                    try
                    {
                        this.Send(JsonConvert.SerializeObject(message));
                    }
                    catch
                    {
                    }
                });
            }

            public object GenerateServerReport()
            {
                switch (this.SubscribeLevel)
                {
                    case SubscribeLevel.None:
                    case SubscribeLevel.Semi:
                        return GenerateSemiReport();

                    case SubscribeLevel.Full:
                        return GenerateFullReport();

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            private void Send(string message)
            {
                Singleton.NextTick(() =>
                {
                    try
                    {
                        _webSocket?.SendAsync(message, null);
                    }
                    catch
                    {
                    }
                });
            }

            private void OnMessage(object sender, MessageEventArgs e)
            {
                try
                {
                    if (e.IsText && e.Data.Length > 0)
                    {
                        HandleServerCommand(e.Data);
                    }
                }
                catch (Exception ex)
                {
                    Singleton.RaiseError("Error in Socket OnMessage: " + ex.Message);
                }
            }

            private void OnError(object sender, ErrorEventArgs e)
            {
#if STAGE
                Singleton.Puts("WebSocket error: " + e.Message);
#endif

                try
                {
                    this.OnClose(sender, null);
                }
                catch (Exception ex)
                {
                    Singleton.RaiseError("Error in Socket OnError: " + ex.Message);
                }
            }

            private void OnClose(object sender, CloseEventArgs e)
            {
                Singleton.NextTick(() =>
                {
                    _statsTimer?.Destroy();
                    _webSocket = null;

                    SubscribeLevel = SubscribeLevel.None;

                    Singleton.timer.Once(SocketReconnectInterval, () => { Singleton.RegisterWebSocket(); });
                });
            }

            private void HandleServerCommand(string message)
            {
                try
                {
#if STAGE
                    Singleton.Puts("Message from websocket: " + message);
#endif

                    var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(message);

                    string method = data["method"].ToString();
                    Singleton.NextTick(() =>
                    {
                        try
                        {
                            switch (method)
                            {
                                case "subscribe":
                                    this.SubscribeLevel = (SubscribeLevel)Enum.Parse(typeof(SubscribeLevel), data["subscribeLevel"].ToString());
                                    break;

                                case "command":
                                    {
                                        string command = data["command"].ToString();
                                        string messageId = data["messageId"].ToString();

                                        ExecuteCommand(command, messageId);
                                        break;
                                    }

                                case "getPlayerProfiles":
                                    {
                                        string messageId = data["messageId"].ToString();
                                        string steamIds = data["steamIds"].ToString();

                                        SendUsersProfiles(steamIds, messageId);
                                        break;
                                    }
                            }
                        }
                        catch (Exception ex)
                        {
                            Singleton.RaiseError($"Failed to handle server command (on NextTick) {message} - {ex.ToString()}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Singleton.RaiseError($"Failed to handle server command {message} - {ex.ToString()}");
                }
            }

            private void ExecuteCommand(string command, string messageId)
            {
                string response = ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), command);

                this.SendAsync(new
                {
                    method = "command_response",

                    payload = new
                    {
                        messageId,
                        response = response == null ? ConsoleSystem.LastError : response
                    }
                });
            }

            private Dictionary<string, PlayerInfoDto> GetUsersProfiles(string[] steamIds)
            {
                var profiles = new Dictionary<string, PlayerInfoDto>();

                foreach (var player in steamIds)
                {
                    if (player.Length == 0)
                    {
                        continue;
                    }

                    BasePlayer targetPlayer = TirifyGameExt.FindAwakeOrSleeping(player);

                    if (targetPlayer == null)
                    {
                        profiles[player] = new PlayerInfoDto() { Status = false };
                        continue;
                    }

                    var playerInfo = new PlayerInfoDto() { Status = true };
                    playerInfo.Profile = PlayerProfileBuilder.FromPlayer(targetPlayer);

                    profiles[player] = playerInfo;
                }

                return profiles;
            }

            private void SendUsersProfiles(string steamIds, string messageId)
            {
                var profiles = GetUsersProfiles(steamIds.Split(','));

                this.SendAsync(new
                {
                    method = "profiles_response",
                    payload = new
                    {
                        messageId,
                        profiles
                    }
                });
            }

            private GeneralInfoPayload GenerateSemiReport()
            {
                var generalInfoPayload = new GeneralInfoPayload()
                {
                    AverageFps = FrameLogger.AverageFramerate,
                    MinimalFps = FrameLogger.MinimalFramerate,
                    OnlinePlayers = (uint)BasePlayer.activePlayerList.Count,
                    ConnectingPlayers = (uint)ServerMgr.Instance.connectionQueue.Joining,
                    SleepingPlayers = (uint)BasePlayer.sleepingPlayerList.Count,
                    QueuePlayers = (uint)ServerMgr.Instance.connectionQueue.Queued,
                    Entities = (uint)BaseNetworkable.serverEntities.Count,
                    PiratePlayers = (uint)Singleton.GetNoSteamCount(),
                    ServerTime = TOD_Sky.Instance.Cycle.DateTime.ToString("HH:mm"),
                    CreateAt = (uint)TirifyGameExt.ToUnixTimeSeconds(DateTime.UtcNow)
                };

                return generalInfoPayload;
            }

            private SubscriptionPayload GenerateFullReport()
            {
                var subscriptionPayload = new SubscriptionPayload(GenerateSemiReport());

                subscriptionPayload.Players = new PlayerData[BasePlayer.activePlayerList.Count];

                // add player info
                for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
                {
                    var player = BasePlayer.activePlayerList[i];

                    var playerInfoReport = PlayerData.FromPlayer(player);

                    subscriptionPayload.Players[i] = playerInfoReport;
                }

                var plugins = Singleton.plugins.GetAll();

                subscriptionPayload.Plugins = new List<PluginData>();

                // add plugins info
                foreach (var plugin in plugins)
                {
                    if (plugin.IsCorePlugin)
                    {
                        continue;
                    }

                    var pluginInfoReport = new PluginData()
                    {
                        Author = plugin.Author,
                        Filename = plugin.Name,
                        Name = plugin.Title,
                        Version = plugin.Version.ToString(),
                        HasLoaded = plugin.IsLoaded,
                        CpuUsage = plugin.TotalHookTime.ToString("0.0000")
                    };

                    subscriptionPayload.Plugins.Add(pluginInfoReport);
                }

                return subscriptionPayload;
            }
        }

        #endregion

        #region [ Frame Logger (FPS / Performance) ]

        public class FrameLogger
        {
            private static List<float> _frames = new List<float>();

            public static uint AverageFramerate { get; private set; }

            public static uint MinimalFramerate { get; private set; }

            public static void OnFrame()
            {
                _frames.Add(1f / Time.deltaTime);
            }

            public static void Clear()
            {
                if (_frames.Count > 0)
                {
                    AverageFramerate = (uint)_frames.Average();
                    MinimalFramerate = (uint)_frames.Min();
                }

                _frames.Clear();
            }
        }

        public class FrameLoggerComponent : MonoBehaviour
        {
            private void Update()
            {
                FrameLogger.OnFrame();
            }
        }

        #endregion

        #region [ Player Profile Builder ]

        public class PlayerProfileBuilder
        {
            public static PlayerProfileDto FromPlayer(BasePlayer player)
            {
                var profile = new PlayerProfileDto(PlayerData.FromPlayer(player));

                profile.Online = player.IsConnected;
                profile.Inventory.Main = TirifyGameExt.SerializeForSocket(player.inventory.containerMain, true);

                FillObjectsInfo(player, profile);

                return profile;
            }


            private static void FillObjectsInfo(BasePlayer player, PlayerProfileDto playerProfile)
            {
                playerProfile.Cupboards = new List<PlayerCupboardDto>();
                playerProfile.Stashes = new List<PlayerStashDto>();

                foreach (var entity in BaseNetworkable.serverEntities)
                {
                    if (entity is BuildingPrivlidge)
                    {
                        var cupboard = entity as BuildingPrivlidge;

                        if (cupboard.OwnerID == player.userID || cupboard.IsAuthed(player))
                        {
                            var cupboardPos = cupboard.transform.position;

                            playerProfile.Cupboards.Add(new PlayerCupboardDto()
                            {
#if OLD_NETWORKABLE
                                Id = (int)cupboard.net.ID,
#else
                                Id = (int)cupboard.net.ID.Value,
#endif
                                Inventory = cupboard.inventory.itemList.Select(f => $"{f.info.shortname}:{f.amount}").ToArray(),
                                IsAuthed = cupboard.IsAuthed(player),
                                Authed = cupboard.authorizedPlayers.Select(f => f.userid.ToString()).ToArray(),
                                Position = $"{cupboardPos.x},{cupboardPos.y},{cupboardPos.z}",
                                Grid = TirifyGameExt.GetGrid(cupboardPos),
                            });
                        }
                    }
                    else if (entity is StashContainer)
                    {
                        var stash = entity as StashContainer;

                        if (stash.OwnerID == player.userID)
                        {
                            var stashPos = stash.transform.position;

                            playerProfile.Stashes.Add(new PlayerStashDto()
                            {
#if OLD_NETWORKABLE
                                Id = (int)stash.net.ID,
#else
                                Id = (int)stash.net.ID.Value,
#endif
                                Inventory = stash.inventory.itemList.Select(f => $"{f.info.shortname}:{f.amount}").ToArray(),
                                Position = $"{stashPos.x},{stashPos.y},{stashPos.z}",
                                Grid = TirifyGameExt.GetGrid(stashPos),
                            });
                        }
                    }
                }
            }
        }

        #endregion

        #region [ Player Combat Log ]

        public class PlayerCombatLog
        {
            [JsonProperty("uuid")] public string UUID { get; private set; }

            [JsonProperty("victim_steamid")] public string VictimSteamId { get; private set; }

            [JsonProperty("attacker_steamid")] public string AttackerSteamId { get; private set; }

            [JsonProperty("attackers")] public string Attackers { get; private set; }

            [JsonProperty("combatlog")] public string CombatLogJson { get; private set; }

            [JsonIgnore] public string Weapon { get; private set; }

            [JsonIgnore] public string Distance { get; private set; }

            [JsonIgnore] public string Bone { get; private set; }

            private PlayerCombatLog()
            {
            }

            public static PlayerCombatLog Create(BasePlayer owner, bool died, float logTime = 30f)
            {
                try
                {
                    if (owner == null)
                    {
                        return null;
                    }

                    var events = CombatLog.Get(owner.userID);

                    if (events == null)
                    {
                        throw new Exception("Player has no combatlog");
                    }

                    var playerEvents = GetPlayerEvents(events, died, logTime);

                    if (!playerEvents.Any())
                    {
                        throw new Exception("No player events found");
                    }

                    CombatLog.Event lastPlayerAttack = default(CombatLog.Event);
                    BasePlayer attackerPlayer = null;

                    if (!GetLastPlayerAttack(playerEvents, ref lastPlayerAttack, ref attackerPlayer))
                    {
                        throw new Exception("Last attacker not found");
                    }

                    var attackers = GetAttackersSteamIds(playerEvents);

                    return new PlayerCombatLog()
                    {
                        UUID = Guid.NewGuid().ToString(),
                        VictimSteamId = owner.UserIDString,
                        AttackerSteamId = attackerPlayer.UserIDString,
                        Attackers = string.Join(",", attackers.ToArray()),
                        CombatLogJson = JsonConvert.SerializeObject(playerEvents),
                        Weapon = GetWeaponName(lastPlayerAttack),
                        Distance = ((int)lastPlayerAttack.distance).ToString(),
                        Bone = lastPlayerAttack.bone
                    };
                }
                catch (Exception ex)
                {
#if STAGE
                    Singleton.PrintError("Failed to get combatlog: " + ex);
#endif
                }

                return null;
            }

            private static IEnumerable<string> GetAttackersSteamIds(IEnumerable<CombatLog.Event> events)
            {
                var attackerNetIds = new HashSet<ulong>();

                foreach (var @event in events)
                {
                    if (@event.target == "you")
                    {
                        attackerNetIds.Add(@event.attacker_id);
                    }
                }

                // find player for each net ID, take his steamId
#if OLD_NETWORKABLE
                return attackerNetIds
                    .Select(f => BaseNetworkable.serverEntities.Find((uint)f) as BasePlayer)
                    .Select(f => f.UserIDString);
#else
                return attackerNetIds
                    .Select(f => BaseNetworkable.serverEntities.Find(new NetworkableId(f)) as BasePlayer)
                    .Select(f => f.UserIDString);
#endif
            }

            private static float GetLastKillTime(IEnumerable<CombatLog.Event> events, bool died)
            {
                var killEvents = events.Where(f => f.target == "you" && f.info == "killed");

                int deathCount = died ? 1 : 0;

                if (killEvents.Count() <= deathCount)
                {
                    return 0f;
                }

                var lastKillEvent = killEvents.ElementAt(killEvents.Count() - 2);

                return lastKillEvent.time;
            }

            private static string GetWeaponName(CombatLog.Event log)
            {
                return log.weapon == "N/A"
                    ? log.weapon
                    : log.weapon.Substring(log.weapon.LastIndexOf("/") + 1)
                        .Replace(".prefab", "")
                        .Replace(".entity", "");
            }

            private static IEnumerable<CombatLog.Event> GetPlayerEvents(IEnumerable<CombatLog.Event> source, bool died, float logTime)
            {
                var time = Time.realtimeSinceStartup;

                var playerEvents = source
                    .Where(f => time - f.time <= logTime)
                    .Where(f => f.attacker == "player" || f.target == "player"); // only from players

                // Get only current life combat log

                var lastKillTime = GetLastKillTime(playerEvents, died);

                if (lastKillTime != 0)
                {
                    playerEvents = playerEvents.Where(f => f.time > lastKillTime);
                }

                return playerEvents;
            }

            private static bool GetLastPlayerAttack(IEnumerable<CombatLog.Event> events, ref CombatLog.Event lastPlayerAttack, ref BasePlayer attackerPlayer)
            {
                lastPlayerAttack = events.Last(f => f.attacker == "player");

#if OLD_NETWORKABLE
                var attackerId = lastPlayerAttack.attacker_id;
#else
                var attackerId = new NetworkableId(lastPlayerAttack.attacker_id);
#endif

                attackerPlayer = BaseNetworkable.serverEntities.Find(attackerId) as BasePlayer;

                if (attackerPlayer == null)
                {
                    return false;
                }

                return true;
            }
        }

        #endregion

        #region [ Player Stats ]

        public class PlayerStats
        {
            private static readonly Dictionary<string, PlayerStatsContainer> _statsDb = new Dictionary<string, PlayerStatsContainer>();

            public static void Add(string playerId, string key, uint count)
            {
                lock (_statsDb)
                {
                    Get(playerId).Add(key, count);
                }
            }

            public static void Add(BasePlayer player, string key, uint count)
                => Add(player.UserIDString, key, count);

            public static void Increment(string playerId, string key)
                => Add(playerId, key, 1);

            public static void Increment(BasePlayer player, string key)
                => Increment(player.UserIDString, key);

            public static string SerializeAndClear()
            {
                Dictionary<string, PlayerStatsContainer> statsDbClone = null;

                lock (_statsDb)
                {
                    if (_statsDb.Count > 0)
                    {
                        statsDbClone = new Dictionary<string, PlayerStatsContainer>(_statsDb);

                        _statsDb.Clear();
                    }
                }

                if (statsDbClone == null)
                {
                    return null;
                }

                var statsToSend = statsDbClone
                    .Where(f => f.Value.Values.Count > 0)
                    .ToDictionary(f => f.Key, f => f.Value.Values);

                if (statsToSend.Count == 0)
                {
                    return null;
                }

                return JsonConvert.SerializeObject(statsToSend);
            }

            private static PlayerStatsContainer Get(string playerId)
            {
                PlayerStatsContainer playerStats = null;

                if (_statsDb.TryGetValue(playerId, out playerStats))
                {
                    return playerStats;
                }

                playerStats = new PlayerStatsContainer();

                _statsDb[playerId] = playerStats;

                return playerStats;
            }

            public class PlayerStatsContainer
            {
                public readonly Dictionary<string, uint> Values = new Dictionary<string, uint>();

                public void Add(string key, uint value)
                {
                    uint lastValue = 0;

                    Values.TryGetValue(key, out lastValue);

                    Values[key] = lastValue + value;
                }
            }
        }

        #endregion

        #region [ Tirify Ban System ]

        public static class TirifyBanSystem
        {
            public class BannedUser
            {
                [JsonProperty("Причина")] public string Reason { get; set; }

                [JsonProperty("Steam ID")] public ulong SteamId { get; set; }

                [JsonProperty("IP")] public string IpAddress { get; set; }

                [JsonProperty("Computer ID")] public string ComputerId { get; set; }

                [JsonProperty("Internal ID")] public string InternalId { get; set; }

                [JsonProperty("MAC-адрес")] public string Mac { get; set; }

                [JsonProperty("Дата бана")] public long BannedDate { get; set; }
            }

            public static bool CheckPlayerBanned(Connection connection, out string reason)
            {
                BannedUser alreadyBannedUser = null;

                if (_data.BannedUsers.TryGetValue(connection.userid, out alreadyBannedUser))
                {
                    if (!string.IsNullOrEmpty(_config.BanSystem.DiscordWebhook))
                    {
                        if (_config.BanSystem.NotifyBannedConnection)
                            API.Discord.NotifyBannedConnection(connection, !string.IsNullOrEmpty(alreadyBannedUser.ComputerId));
                    }

                    reason = alreadyBannedUser.Reason;
                    return true;
                }

                var playerHwid = Singleton.GetPlayerHwid(connection.userid.ToString());

                string computerId = null, internalId = null, mac = null;

                if (playerHwid != null && playerHwid.Count > 0)
                {
                    playerHwid.TryGetValue("computerId", out computerId);
                    playerHwid.TryGetValue("internalId", out internalId);
                    playerHwid.TryGetValue("mac", out mac);
                }

                string ipAddress = TirifyGameExt.IPAddressWithoutPort(connection);

                foreach (var bannedUser in _data.BannedUsers.Values)
                {
                    if (IsBannedByHwid(bannedUser, computerId, internalId, mac)
                        || IsBannedByIpAddress(bannedUser, ipAddress))
                    {
                        if (playerHwid != null && playerHwid.Count > 0)
                        {
                            bannedUser.ComputerId = computerId;
                            bannedUser.InternalId = internalId;

                            if (mac != "XX-XX-XX-XX-XX-XX")
                            {
                                bannedUser.Mac = mac;
                            }
                        }

                        bannedUser.IpAddress = ipAddress;

                        Singleton.PrintWarning($"Player {connection.userid} banned on another account: {bannedUser.SteamId}");

                        if (!string.IsNullOrEmpty(_config.BanSystem.DiscordWebhook))
                        {
                            if (_config.BanSystem.NotifyBannedConnection)
                            {
                                API.Discord.NotifyBannedConnection(
                                    connection,
                                    playerHwid != null && playerHwid.Count > 0,
                                    bannedUser.SteamId.ToString());
                            }
                        }

                        reason = bannedUser.Reason;
                        return true;
                    }
                }

                reason = null;
                return false;
            }

            public static string OnPlayerBanCommand(ConsoleSystem.Arg arg, bool ban)
            {
                if (!Singleton.CanPlayerUse(arg, PermissionBanUnban))
                {
                    return "Not allowed";
                }

                string steamIdString = arg.GetString(0);

                if (string.IsNullOrEmpty(steamIdString))
                {
                    return "Вы не ввели steamId / Missing steamId";
                }

                ulong steamId = 0;

                if (ulong.TryParse(steamIdString, out steamId) == false || steamIdString.Length != 17 || !steamIdString.StartsWith("7656"))
                {
                    return "Неверный steamId / Bad steamId";
                }

                string reason = arg.GetString(1, "No Reason");

                return ban ? BanPlayer(steamId, reason) : UnbanPlayer(steamId);
            }

            public static string BanPlayer(ulong steamId, string reason, BasePlayer moderator = null)
            {
                BannedUser bannedUser = null;

                if (_data.BannedUsers.TryGetValue(steamId, out bannedUser))
                {
                    KickWithBanReason(steamId, bannedUser.Reason);

                    return $"Игрок уже забанен / Player already banned: {bannedUser.Reason}";
                }

                bannedUser = new BannedUser()
                {
                    SteamId = steamId,
                    Reason = reason,
                    BannedDate = TirifyGameExt.ToUnixTimeSeconds(DateTime.UtcNow),
                };

                var playerHwid = Singleton.GetPlayerHwid(steamId.ToString());

                if (playerHwid != null && playerHwid.Count > 0)
                {
                    bannedUser.Mac = playerHwid["mac"];
                    bannedUser.ComputerId = playerHwid["computerId"];
                    bannedUser.InternalId = playerHwid["internalId"];
                }

                Connection playerConnection = TirifyGameExt.FindConnection(steamId);

                if (_config.BanSystem.AutoBanIp && playerConnection != null && !string.IsNullOrEmpty(playerConnection.ipaddress))
                {
                    bannedUser.IpAddress = TirifyGameExt.IPAddressWithoutPort(playerConnection);
                }

                _data.BannedUsers.Add(steamId, bannedUser);
                Singleton.SaveData();

                KickWithBanReason(steamId, reason);

                Interface.Call("OnTirifyUseBan", steamId.ToString(), reason, moderator);

                return "Успешно / Success";
            }

            public static string UnbanPlayer(ulong steamId, BasePlayer moderator = null)
            {
                BannedUser bannedUser = null;

                if (!_data.BannedUsers.TryGetValue(steamId, out bannedUser))
                {
                    return $"Игрок не забанен / Player not banned";
                }

                _data.BannedUsers.Remove(steamId);
                Singleton.SaveData();

                Interface.Call("OnTirifyUseUnBan", steamId.ToString(), moderator);

                return "Успешно / Success";
            }

            private static bool IsBannedByHwid(BannedUser bannedUser, string computerId, string internalId, string mac)
            {
                if (!string.IsNullOrEmpty(mac) && mac != "XX-XX-XX-XX-XX-XX" && bannedUser.Mac == mac)
                {
                    return true;
                }

                return !string.IsNullOrEmpty(computerId) && !string.IsNullOrEmpty(internalId)
                    && (bannedUser.InternalId == internalId || bannedUser.ComputerId == computerId);
            }

            private static bool IsBannedByIpAddress(BannedUser bannedUser, string ipAddress)
            {
                return !string.IsNullOrEmpty(ipAddress) && bannedUser.IpAddress == ipAddress;
            }

            private static void KickWithBanReason(ulong steamId, string reason)
            {
                var connection = Net.sv.connections.Find(f => f.userid == steamId);

                if (connection != null)
                {
                    KickWithBanReason(connection, reason);
                }
            }

            private static void KickWithBanReason(Connection connection, string reason)
            {
                if (!_config.BanSystem.ShowBanReason)
                {
                    // игроку не пишется, но пишется в консоль
                    ConnectionAuth.Reject(connection, $"You are banned!", $"Banned: {reason}");
                    return;
                }

                ConnectionAuth.Reject(connection, $"You are banned: {reason}");
            }
        }

        #endregion

        #region [ Integrations ]

        #region [ IQReportSystem ]

        public class PlayerCheck
        {
            private static List<PlayerCheck> _checks = new List<PlayerCheck>();

            public PlayerCheck(BasePlayer initiator, BasePlayer target, bool isConsole)
            {
                this.GUID = Guid.NewGuid().ToString();

#if RUST_LATEST
                this.Initiator = initiator?.userID.Get() ?? 0;
                this.Target = target.userID.Get();
#else
                this.Initiator = initiator?.userID ?? 0;
                this.Target = target.userID;
#endif
            }

            public string GUID { get; private set; }

            public ulong Initiator { get; private set; }

            public ulong Target { get; private set; }

            public void Destroy()
            {
                _checks.Remove(this);
            }

            public static PlayerCheck New(BasePlayer initiator, BasePlayer target, bool isConsole)
            {
                var check = new PlayerCheck(initiator, target, isConsole);
                _checks.Add(check);

                return check;
            }

            public static PlayerCheck Find(BasePlayer initiator, ulong targetId)
            {
#if RUST_LATEST
                return _checks.FirstOrDefault(f => (f.Initiator != 0 && f.Initiator == initiator?.userID.Get()) || f.Target == targetId);
#else
                return _checks.FirstOrDefault(f => (f.Initiator != 0 && f.Initiator == initiator?.userID) || f.Target == targetId);
#endif
            }
        }

        private void OnStartedChecked(BasePlayer Target, BasePlayer Moderator, Boolean IsConsole = false)
            => EventLogger.IQReportSystem.OnStartedChecked(Target, Moderator, IsConsole);

        private void OnStoppedChecked(UInt64 TargetID, BasePlayer Moderator, Boolean AutoStop = false, Boolean IsConsole = false)
            => EventLogger.IQReportSystem.OnStoppedChecked(TargetID, Moderator, AutoStop, IsConsole);

        private void OnVerdictChecked(UInt64 TargetID, BasePlayer Moderator, String VerdictReason, String VerdictCommand)
            => EventLogger.IQReportSystem.OnVerdictChecked(TargetID, Moderator, VerdictReason, VerdictCommand);

        private void OnSendedContacts(BasePlayer player, String Discord)
            => EventLogger.IQReportSystem.OnSendedContacts(player, Discord);

        private void OnSendedReport(BasePlayer Sender, UInt64 TargetID, String Reason)
            => EventLogger.LogReport(Sender, TargetID, "", Reason);

        [ConsoleCommand("t.api.check.start")]
        private void CmdApiStartCheck(ConsoleSystem.Arg arg)
        {
            if (!arg.IsRcon)
            {
                return;
            }

            if (IQReportSystem == null)
            {
                arg.ReplyWithObject(new
                {
                    success = false,
                    message = "IQReportSystem plugin not installed"
                });

                return;
            }

            ulong targetId = arg.GetUInt64(0);

            BasePlayer targetPlayer = TirifyGameExt.FindAwakeOrSleeping(targetId.ToString());

            if (targetPlayer == null || !targetPlayer.IsConnected)
            {
                arg.ReplyWithObject(new
                {
                    success = false,
                    message = "Player not found"
                });

                return;
            }

            bool started = (bool)IQReportSystem.Call("ForceStartCheck", targetPlayer);

            if (!started)
            {
                arg.ReplyWithObject(new
                {
                    success = false,
                    message = "IQReportSystem returned false"
                });

                return;
            }

            var playerCheck = PlayerCheck.Find(null, targetId);

            if (playerCheck == null)
            {
                arg.ReplyWithObject(new
                {
                    success = false,
                    message = "Failed to get check GUID"
                });

                return;
            }

            arg.ReplyWithObject(new
            {
                success = true,
                uuid = playerCheck.GUID
            });
        }

        [ConsoleCommand("t.api.check.stop")]
        private void CmdApiStopCheck(ConsoleSystem.Arg arg)
        {
            if (!arg.IsRcon)
            {
                return;
            }

            if (IQReportSystem == null)
            {
                arg.ReplyWithObject(new
                {
                    success = false,
                    message = "IQReportSystem plugin not installed"
                });

                return;
            }

            ulong targetId = arg.GetUInt64(0);

            BasePlayer targetPlayer = TirifyGameExt.FindAwakeOrSleeping(targetId.ToString());

            if (targetPlayer == null)
            {
                arg.ReplyWithObject(new
                {
                    success = false,
                    message = "Player not found"
                });

                return;
            }

            bool ended = (bool)IQReportSystem.Call("StopChecked", targetPlayer);

            if (!ended)
            {
                arg.ReplyWithObject(new
                {
                    success = false,
                    message = "IQReportSystem returned false"
                });

                return;
            }

            arg.ReplyWithObject(new
            {
                success = true
            });
        }

        #endregion

        #endregion

        #region [ Extensions ]

        public class TirifyGameExt
        {
            public static List<Connection> WaitingList;
            public static FieldInfo StorageField;
            public static MethodInfo UpdateServerInformationMethod;

            private static readonly string[] _mapChars = new string[]
            {
                "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
                "AA", "AB", "AC", "AD", "AE", "AF", "AG", "AH", "AI", "AJ", "AK", "AL", "AM", "AN", "AO", "AP", "AQ", "AR", "AS", "AT", "AU", "AV",
                "AW", "AX", "AY", "AZ"
            };

            private static readonly float _worldSize = ConVar.Server.worldsize;
            private static readonly float _mapOffset = _worldSize / 2;

            private static readonly DateTime _unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            public static string GetSessionUID(Connection connection)
            {
                var playerHwid = Singleton.GetPlayerHwid(connection.userid.ToString());

                if (playerHwid?.Count > 0)
                {
                    return playerHwid["sessionId"];
                }

                return GetSessionUIDForEvents(connection).ToString();
            }

            public static ulong GetSessionUIDForEvents(Connection connection)
            {                
                var lastFourSymbols = connection.userid.ToString().Substring(13);

                return ulong.Parse(TirifyGameExt.GetConnectionTimestamp(connection).ToString() + lastFourSymbols);
            }

            public static int GetItemConditionPercent(Item item)
                => (int)Math.Ceiling((item.condition / item.maxCondition) * 100);

#if OLD_ITEMS
            public static string SerializeForSocket(Item item)
            {
                var baseProjectile = item.GetHeldEntity() as BaseProjectile;

                // Получаем StringBuilder из пула
                var sb = StringBuilderPool.Acquire();
                sb.Append(item.info.shortname).Append(':')
                  .Append(item.amount).Append(':')
                  .Append(item.hasCondition ? GetItemConditionPercent(item) : -1).Append(':')
                  .Append(baseProjectile?.primaryMagazine?.contents ?? -1);

                // Освобождаем StringBuilder и возвращаем строку
                return StringBuilderPool.GetStringAndRelease(sb);
            }
#else
            public static string SerializeForSocket(Item item)
            {
                var sb = StringBuilderPool.Acquire();
                sb.Append(item.info.shortname).Append(':')
                    .Append(item.amount).Append(':')
                    .Append(item.hasCondition ? GetItemConditionPercent(item) : -1).Append(':')
                    .Append(GetItemAmmoCount(item) ?? -1);

                return StringBuilderPool.GetStringAndRelease(sb);
            }

            private static int? GetItemAmmoCount(Item item)
            {
                return (item?.GetHeldEntity() as BaseProjectile)?.primaryMagazine.contents;
            }
#endif

            public static string[] SerializeForSocket(ItemContainer inventory, bool replaceEmptyWithNull = false)
            {
                string[] items = new string[inventory.capacity];

                try
                {
                    for (int i = 0; i < items.Length; i++)
                    {
                        var item = inventory.GetSlot(i);

                        if (item == null)
                        {
                            items[i] = replaceEmptyWithNull ? null : "-";
                        }
                        else
                        {
                            items[i] = SerializeForSocket(item);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // TODO: Добавить вывод ошибки
                }

                return items;
            }

            public static string GetGrid(Vector3 position)
            {
                const float BLOCK = 146;

                float positionX = position.x + _mapOffset,
                    positionZ = position.z + _mapOffset;

                int maxGrid = (int)(_worldSize / BLOCK);

                float x = Mathf.Clamp(positionX / BLOCK, 0, maxGrid - 1);
                float z = Mathf.Clamp(maxGrid - (positionZ / BLOCK), 0, maxGrid - 1);

                return string.Concat(_mapChars[(int)x], (int)z);
            }

            public static long GetConnectionTimestamp(Network.Connection connection)
            {
                var secondsConnected = (int)connection.GetSecondsConnected();

                return ToUnixTimeSeconds(DateTime.UtcNow - TimeSpan.FromSeconds(secondsConnected));
            }

            public static int GetTeamId(BasePlayer player)
            {
#if RUST_TEAMS
                return (int?)player.Team?.teamID ?? -1;
#else
                return -1;
#endif
            }

            public static BasePlayer FindAwakeOrSleeping(string id)
            {
                var onlinePlayer = BasePlayer.Find(id);

                if (onlinePlayer != null)
                {
                    return onlinePlayer;
                }

                var sleepingPlayer = BasePlayer.FindSleeping(id);

                if (sleepingPlayer != null)
                {
                    return sleepingPlayer;
                }

                return null;
            }

            public static string IPAddressWithoutPort(Network.Connection connection)
            {
                int portIndex = connection.ipaddress.LastIndexOf(':');

                if (portIndex != -1)
                {
                    return connection.ipaddress.Substring(0, portIndex);
                }

                return connection.ipaddress;
            }

            public static long ToUnixTimeSeconds(DateTime dateTime)
                => (dateTime.ToUniversalTime().Ticks - _unixEpoch.Ticks) / TimeSpan.TicksPerSecond;

            public static long ToUnixTimeMilliseconds(DateTime dateTime)
                => (dateTime.ToUniversalTime().Ticks - _unixEpoch.Ticks) / TimeSpan.TicksPerMillisecond;

            public static DateTime FromUnixTimeSeconds(long timestamp)
                => _unixEpoch.AddSeconds(timestamp);

            public static string GetServerTags()
            {
#if RUST_OLD_STEAMWORKS
                return Facepunch.Steamworks.Server.Instance.GameTags;
#else
                return ConVar.Server.tags;
#endif
            }

            public static Connection FindConnection(ulong userId)
            {
                return Net.sv.connections.Find(f => f.userid == userId);
            }

            public static void ReplyWithJson(ConsoleSystem.Arg arg, object jsonObject)
            {
                arg.ReplyWith(JsonConvert.SerializeObject(jsonObject));
            }

            public static string ToHexString(byte[] bytes)
            {
                return BitConverter.ToString(bytes).Replace("-", "");
            }
        }

        #endregion

        #region [ StringBuilder Pool ]

        public class StringBuilderPool
        {
            private const int MaxBuilderSize = 360;

            private static readonly Stack<StringBuilder> pool = new Stack<StringBuilder>();

            private static readonly object poolLock = new object();

            public static StringBuilder Acquire(int capacity = 24)
            {
                lock (poolLock)
                {
                    if (pool.Count > 0)
                    {
                        var sb = pool.Pop(); 
                        sb.Clear();

                        return sb;
                    }
                }

                return new StringBuilder(capacity);
            }

            public static void Release(StringBuilder sb)
            {
                if (sb.Capacity > MaxBuilderSize)
                {
                    return;
                }

                lock (poolLock)
                {
                    pool.Push(sb);
                }
            }

            public static string GetStringAndRelease(StringBuilder sb)
            {
                string result = sb.ToString();
                Release(sb);

                return result;
            }
        }

        #endregion
    }
}
