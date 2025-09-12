using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

//SimpleAuthorization created with PluginMerge v(1.0.8.0) by MJSU @ https://github.com/dassjosh/Plugin.Merge
namespace Oxide.Plugins
{
    [Info("SimpleAuthorization", "Shady14u", "1.0.5")]
    [Description("Simple Authorization for Clans and Teams")]
    public partial class SimpleAuthorization : RustPlugin
    {
        #region 0.SimpleAuthorization.cs
        private readonly Dictionary<ulong, EntityCache> _playerEntities = new Dictionary<ulong, EntityCache>();
        [PluginReference] Plugin Clans;
        
        private void AuthToCupboard(HashSet<BuildingPrivlidge> buildingPrivileges, ulong playerId)
        {
            if (buildingPrivileges.Count <= 0) return;
            var authList = GetPlayerNameIDs(playerId);
            foreach (var buildingPrivilege in buildingPrivileges)
            {
                if (buildingPrivilege == null || buildingPrivilege.IsDestroyed) continue;
                var currentMembers = buildingPrivilege.authorizedPlayers.ToList();
                buildingPrivilege.authorizedPlayers.Clear();
                
                foreach (var teamMate in authList.Where(x => currentMembers.All(p => p.userid != x.userid)))
                {
                    buildingPrivilege.authorizedPlayers.Add(teamMate);
                }
                
                buildingPrivilege.SendNetworkUpdate();
            }
        }
        
        private void AuthToTurret(HashSet<AutoTurret> autoTurrets, ulong playerId)
        {
            if (autoTurrets.Count <= 0) return;
            var authList = GetPlayerNameIDs(playerId);
            foreach (var autoTurret in autoTurrets)
            {
                if (autoTurret == null || autoTurret.IsDestroyed) continue;
                var isOnline = autoTurret.IsOnline();
                if (isOnline) autoTurret.SetIsOnline(false);
                var currentMembers = autoTurret.authorizedPlayers.ToList();
                autoTurret.authorizedPlayers.Clear();
                
                foreach (var teamMate in authList.Where(x => currentMembers.All(p => p.userid != x.userid)))
                {
                    autoTurret.authorizedPlayers.Add(teamMate);
                }
                
                if (isOnline) autoTurret.SetIsOnline(true);
                autoTurret.SendNetworkUpdate();
            }
        }
        
        private void CheckEntityKill(BuildingPrivlidge buildingPrivilege)
        {
            if (buildingPrivilege == null || !buildingPrivilege.OwnerID.IsSteamId()) return;
            if (_playerEntities.TryGetValue(buildingPrivilege.OwnerID, out var entityCache))
            {
                entityCache.BuildingPrivileges.Remove(buildingPrivilege);
            }
        }
        
        private void CheckEntitySpawned(BuildingPrivlidge buildingPrivilege, bool justCreated = false)
        {
            if (buildingPrivilege == null || !buildingPrivilege.OwnerID.IsSteamId()) return;
            if (!_playerEntities.TryGetValue(buildingPrivilege.OwnerID, out var entityCache))
            {
                entityCache = new EntityCache();
                _playerEntities.Add(buildingPrivilege.OwnerID, entityCache);
            }
            
            entityCache.BuildingPrivileges.Add(buildingPrivilege);
            
            if (justCreated)
            {
                AuthToCupboard(new HashSet<BuildingPrivlidge> {buildingPrivilege}, buildingPrivilege.OwnerID);
            }
        }
        
        private void CheckEntitySpawned(AutoTurret autoTurret, bool justCreated = false)
        {
            if (autoTurret == null || !autoTurret.OwnerID.IsSteamId()) return;
            if (!_playerEntities.TryGetValue(autoTurret.OwnerID, out var entityCache))
            {
                entityCache = new EntityCache();
                _playerEntities.Add(autoTurret.OwnerID, entityCache);
            }
            
            entityCache.AutoTurrets.Add(autoTurret);
            
            if (justCreated)
            {
                AuthToTurret(new HashSet<AutoTurret> {autoTurret}, autoTurret.OwnerID);
            }
        }
        
        private void ClearMembersAuthList(ulong playerId)
        {
            UpdateAuthList(playerId);
        }
        
        private IEnumerable<ulong> GetAuthList(ulong playerId)
        {
            var clanMembers = GetClanMembers(playerId);
            var sharePlayers = new HashSet<ulong> {playerId};
            
            if (clanMembers == null) return sharePlayers;
            foreach (var member in clanMembers)
            {
                sharePlayers.Add(member);
            }
            
            return sharePlayers;
        }
        
        private IEnumerable<ulong> GetClanMembers(ulong playerId)
        {
            var teamMates = new List<ulong>();
            if (_config.ShareTeamCodeLocks)
            {
                teamMates = RelationshipManager.ServerInstance.FindTeam(playerId).members;
            }
            
            if (!_config.ShareClanCodeLocks || Clans == null) return teamMates;
            
            //Clans Reborn
            if (Clans?.Call("GetClanMembers", playerId) is List<string> members)
            {
                teamMates.AddRange(members.Select(x => Convert.ToUInt64(x)));
                return teamMates;
            }
            
            //Clans
            if (Clans?.Call("GetClanOf", playerId) is string clanName)
            {
                var clanMembers = GetClanMembers(clanName);
                if (clanMembers != null) teamMates.AddRange(clanMembers);
            }
            
            return teamMates;
        }
        
        private IEnumerable<ulong> GetClanMembers(string clanName)
        {
            if (Clans == null) return null;
            var clan = Clans.Call("GetClan", clanName) as JObject;
            var members = clan?.GetValue("members") as JArray;
            return members?.Select(Convert.ToUInt64);
        }
        
        private List<PlayerNameID> GetPlayerNameIDs(ulong playerId)
        {
            var authList = GetAuthList(playerId);
            return authList.Select(userid => new PlayerNameID
            {userid = userid, username = RustCore.FindPlayerById(userid)?.displayName ?? string.Empty}).ToList();
        }
        
        private bool SameClan(ulong playerId, ulong friendId)
        {
            if (Clans == null) return false;
            //Clans and Clans Reborn
            var isMember = Clans.Call("IsClanMember", playerId.ToString(), friendId.ToString());
            if (isMember != null) return (bool) isMember;
            //Rust:IO Clans
            var playerClan = Clans.Call("GetClanOf", playerId);
            if (playerClan == null) return false;
            var friendClan = Clans.Call("GetClanOf", friendId);
            if (friendClan == null) return false;
            return (string) playerClan == (string) friendClan;
        }
        
        private static void SendUnlockedEffect(CodeLock codeLock)
        {
            if (codeLock.effectUnlocked.isValid)
            {
                Effect.server.Run(codeLock.effectUnlocked.resourcePath, codeLock.transform.position);
            }
        }
        
        private void UpdateAuthList(ulong playerId)
        {
            if (!_playerEntities.TryGetValue(playerId, out var entityCache))
            {
                return;
            }
            
            AuthToCupboard(entityCache.BuildingPrivileges, playerId);
            AuthToTurret(entityCache.AutoTurrets, playerId);
        }
        
        private void UpdateClanAuthList(string clanName)
        {
            var clanMembers = GetClanMembers(clanName);
            if (clanMembers == null) return;
            foreach (var member in clanMembers)
            {
                UpdateAuthList(member);
            }
        }
        #endregion

        #region 1.SimpleAuthorization.Config.cs
        private static Configuration _config;
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) LoadDefaultConfig();
                SaveConfig();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
        }
        
        protected override void LoadDefaultConfig() => _config = Configuration.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(_config);
        
        public class Configuration
        {
            [JsonProperty(PropertyName = "Share CodeLocks with Clan")]
            public bool ShareClanCodeLocks { get; set; } = true;
            
            [JsonProperty(PropertyName = "Share KeyLocks with Clan")]
            public bool ShareClanKeyLocks { get; set; } = true;
            
            [JsonProperty(PropertyName = "Share CodeLocks with Team")]
            public bool ShareTeamCodeLocks { get; set; } = true;
            
            [JsonProperty(PropertyName = "Share KeyLocks with Team")]
            public bool ShareTeamKeyLocks { get; set; } = true;
            
            public static Configuration DefaultConfig()
            {
                return new Configuration();
            }
        }
        #endregion

        #region 5.SimpleAuthorization.Hooks.cs
        object OnTurretAuthorize(AutoTurret turret, BasePlayer player)
        {
            var authList = turret.authorizedPlayers;
            if (authList == null || authList.Count == 0) return null;
            
            var teamMates = player.Team?.members;
            if (teamMates == null || teamMates.Count == 0) return null;
            
            if (turret.authorizedPlayers.Any(authPlayer => teamMates.Contains(authPlayer.userid)))
            {
                return null;
            }
            player.ChatMessage("You must clear the Authorization List to use this Turret");
            return false;
        }
        
        object OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            var authList = privilege.authorizedPlayers;
            if(authList == null || authList.Count == 0) return null;
            
            var teamMates = player.Team?.members;
            if(teamMates == null || teamMates.Count==0) return null;
            
            if (privilege.authorizedPlayers.Any(authPlayer => teamMates.Contains(authPlayer.userid)))
            {
                return null;
            }
            player.ChatMessage("You must clear the Authorization List to use this TC");
            return false;
        }
        
        private object CanUseLockedEntity(BasePlayer player, BaseLock baseLock)
        {
            if (player == null || baseLock == null || !baseLock.IsLocked()) return null;
            var parentEntity = baseLock.GetParentEntity();
            var ownerId = baseLock.OwnerID.IsSteamId() ? baseLock.OwnerID :
            parentEntity != null ? parentEntity.OwnerID : 0;
            if (!ownerId.IsSteamId() || ownerId == player.userID.Get()) return null;
            
            
            var playerTeam = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
            if (playerTeam != null && playerTeam.members.Contains(ownerId))
            {
                switch (baseLock)
                {
                    case KeyLock _ when _config.ShareTeamKeyLocks:
                    return true;
                    case CodeLock codeLock when _config.ShareTeamCodeLocks:
                    SendUnlockedEffect(codeLock);
                    return true;
                }
            }
            
            if (!SameClan(ownerId, player.userID)) return false;
            
            switch (baseLock)
            {
                case KeyLock _ when _config.ShareClanKeyLocks:
                return true;
                case CodeLock codeLock when _config.ShareClanCodeLocks:
                SendUnlockedEffect(codeLock);
                return true;
            }
            
            return null;
        }
        
        private void Init()
        {
            Unsubscribe(nameof(OnEntitySpawned));
        }
        
        private void OnClanDestroy(string clanName) => UpdateClanAuthList(clanName);
        
        void OnClanMemberGone(string userId, string tag)
        {
            ClearMembersAuthList(Convert.ToUInt64(userId));
        }
        
        private void OnClanUpdate(string clanName)
        {
            UpdateClanAuthList(clanName);
        }
        
        private void OnEntityKill(BuildingPrivlidge buildingPrivilege) => CheckEntityKill(buildingPrivilege);
        
        private void OnEntitySpawned(BuildingPrivlidge buildingPrivilege) =>
        CheckEntitySpawned(buildingPrivilege, true);
        
        private void OnEntitySpawned(AutoTurret autoTurret) => CheckEntitySpawned(autoTurret, true);
        
        private void OnServerInitialized()
        {
            Subscribe(nameof(OnEntitySpawned));
            
            foreach (var serverEntity in BaseNetworkable.serverEntities)
            {
                var autoTurret = serverEntity as AutoTurret;
                if (autoTurret != null)
                {
                    CheckEntitySpawned(autoTurret);
                    continue;
                }
                
                var buildingPrivilege = serverEntity as BuildingPrivlidge;
                if (buildingPrivilege != null)
                {
                    CheckEntitySpawned(buildingPrivilege);
                }
            }
        }
        #endregion

        #region 7.SimpleAuthorization.Classes.cs
        private class EntityCache
        {
            public readonly HashSet<AutoTurret> AutoTurrets = new HashSet<AutoTurret>();
            public readonly HashSet<BuildingPrivlidge> BuildingPrivileges = new HashSet<BuildingPrivlidge>();
        }
        #endregion

    }

}
