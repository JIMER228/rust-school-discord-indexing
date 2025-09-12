using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Oxide.Plugins
{
    [Info("TCAuth", "tofurahie", "1.5.10")]
    internal class TCAuth : RustPlugin   
    {  
        #region Static

        private const string Layer = "UI_TCAuth";

        private HashSet<BuildingPrivlidge> _buildingPrivileges = new();

        private Dictionary<ulong, DateTime> NotifyCooldown = new();

        private Dictionary<Permissions, string> PERMS = new()
        {
            [Permissions.UI] = "tcauth.use",
            [Permissions.ALL] = "tcauth.bypass",
            [Permissions.AUTH] = "tcauth.auth.use",
            [Permissions.LOCK] = "tcauth.lock.use",
            [Permissions.CHEST] = "tcauth.chest.use",
            [Permissions.TURRET] = "tcauth.turret.use",
            [Permissions.IGNORE] = "tcauth.ignore",
            [Permissions.FURNACE] = "tcauth.furnace.use",
            [Permissions.REMOVEBP] = "tcauth.removebp.use",
        };

        private enum Permissions
        {
            UI,
            ALL,
            AUTH,
            LOCK,
            CHEST,
            TURRET,
            IGNORE,
            FURNACE,
            REMOVEBP,
        }

        [PluginReference] private Plugin Clans;

        #region Classes

        private class Configuration
        {
            [JsonProperty("Discord web hook", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string DiscordWebHook = "";

            [JsonProperty("The maximum number of players that can register in the TC")]
            public int TCCapacity = 8;

            [JsonProperty("The maximum number of players that can register in the AutoTurrets")]
            public int ATCapacity = 8;

            [JsonProperty("Only registered players can open chests (when registered on TC)")]
            public bool OAChest = true;

            [JsonProperty("Only registered players can open furnaces (when registered on TC)")]
            public bool OAOven = true;
 
            [JsonProperty("Automatically when players are registered on TC (Autohorized on Turrets, SAM Site)")]
            public bool ARTurrets = true;

            [JsonProperty("Automatically when players are registered on the TC (remove building parts)")]
            public bool ARRemove = true;

            [JsonProperty("Automatically when players are registered on the TC (open codelocks without a code)")]
            public bool AROpen = true;

            [JsonProperty("Automatically registered your teammates on the TC")]
            public bool ARTeam = true;
            
            [JsonProperty("Allow players to authorize friends to TC")]
            public bool ATAllowed = true;

            [JsonProperty("List of shortprefabs containers", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> StorageContainers = new()
            {
                "woodbox_deployed",
                "composter",
                "fridge.deployed",
                "box.wooden.large",
                "locker.deployed",
                "small_stash_deployed",
                "dropbox.deployed",
                "coffinstorage",
            };
        }
        
        
        public class ClanCheck
        {
            public string tag;
            public string owner;
            public JArray members;
            public JArray invited;
            public JArray allies;
            public JArray invitedallies;
        }

        #endregion

        #endregion

        #region Config

        private Configuration _config;

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }


        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();
        #endregion

        #region OxideHooks

        private void OnServerInitialized()
        {
            foreach (var perm in PERMS)
                permission.RegisterPermission(perm.Value, this);

            if (!_config.ARTurrets)
                Unsubscribe(nameof(OnSamSiteTarget));

            if (_config.ARRemove)
                SetAlwaysDemolish();

            foreach (var check in BaseNetworkable.serverEntities.OfType<BuildingPrivlidge>())
            {
                if (check == null)
                    continue;

                _buildingPrivileges.Add(check);
                
                if (_config.ARTurrets || _config.AROpen) 
                    foreach (var checkAuthorizedPlayer in check.authorizedPlayers)
                    {
                            AuthPlayerInTurrets(checkAuthorizedPlayer.userid, check.transform.position);
                            AuthPlayerInLock(checkAuthorizedPlayer.userid, check.transform.position, check.authorizedPlayers);
                    }
            }
        }

        private void Unload()
        {
            foreach (var check in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(check, Layer + ".bg");
        }

        private void OnEntitySpawned(AutoTurret entity) =>
            NextTick(() =>
            {
                if (entity == null || !_config.ARTurrets)
                    return;

                var authorizedPlayers = entity.GetBuildingPrivilege()?.authorizedPlayers;
                if (authorizedPlayers == null)
                    return;

                var isOnline = entity.IsOnline();
                if (isOnline)
                    entity.SetIsOnline(false);

                foreach (var player in authorizedPlayers.Where(x => PlayerHasPermission(x.userid.ToString(), Permissions.TURRET)))
                {
                    entity.authorizedPlayers.RemoveWhere(x => x.userid == player.userid);
                    entity.authorizedPlayers.Add(new PlayerNameID {username = "Player", userid = player.userid});
                }

                if (isOnline)
                    entity.SetIsOnline(true);

                entity.UpdateMaxAuthCapacity();
                entity.SendNetworkUpdate();
            });

        private void OnEntitySpawned(BuildingPrivlidge entity) =>
            NextTick(() =>
            {
                if (entity != null)
                    _buildingPrivileges.Add(entity);
            });


        private void OnEntityKill(BuildingPrivlidge entity)
        {
            if (entity == null)
                return;

            _buildingPrivileges.Remove(entity);
        }

        private void OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            NextTick(() =>
            {
                if (team == null || player == null || !_config.ARTeam || !PlayerHasPermission(player.UserIDString, Permissions.AUTH))
                    return;

                var members = team.members;
                foreach (var tc in _buildingPrivileges)
                    if (tc?.authorizedPlayers != null)
                        if (tc.authorizedPlayers.Any(playerNameID => members.Contains(playerNameID.userid)))
                        {
                            tc.authorizedPlayers.RemoveWhere(x => x.userid == player.userID);
                            tc.authorizedPlayers.Add(new PlayerNameID { username = player.displayName, userid = player.userID });

                            tc.UpdateMaxAuthCapacity();
                            tc.SendNetworkUpdate();

                            OnCupboardAuthorize(tc, player);
                        }
            });
        }

        private void OnTeamDisband(RelationshipManager.PlayerTeam team)
        {
            if (team == null || !_config.ARTeam)
                return;

            foreach (var check in team.members)
                PlayerLeaveTeam(check.ToString());
        }

        private void OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            if (team == null || player == null || !_config.ARTeam)
                return;
            
            PlayerLeaveTeam(player.UserIDString);
        }

        private void OnTeamKick(RelationshipManager.PlayerTeam team, BasePlayer leader, ulong target)
        {
            if (team == null || !_config.ARTeam)
                return;

            PlayerLeaveTeam(target.ToString());
        }

        private object CanLootEntity(BasePlayer player, BaseOven oven)
        {
            if (player == null || oven == null || !_config.OAOven ||  !PlayerHasPermission(player.UserIDString, Permissions.FURNACE))
                return null;

            var tc = oven.GetBuildingPrivilege();
            if (tc == null || tc.IsAuthed(player))
                return null;

            return false;
        }

        private object CanLootEntity(BasePlayer player, StorageContainer box)
        {
            if (player == null || box == null || !_config.OAChest || !PlayerHasPermission(player.UserIDString, Permissions.CHEST) || !_config.StorageContainers.Contains(box.ShortPrefabName))
                return null;

            var tc = box.GetBuildingPrivilege();
            if (tc == null || tc.IsAuthed(player))
                return null;

            return false;
        }

        private object CanUseLockedEntity(BasePlayer player, BaseLock baseLock)
        {
            if (player == null || baseLock == null ||  !_config.AROpen || !PlayerHasPermission(player.UserIDString, Permissions.LOCK))
                return null;

            var tc = baseLock.GetBuildingPrivilege();
            if (tc == null)
                return null;

            if (tc.IsAuthed(player) && tc.IsAuthed(baseLock.OwnerID))
                return true;

            return null;
        }

        private void OnEntitySpawned(BuildingBlock block) =>
            NextTick(() =>
            {
                if (block != null && _config.ARRemove && PlayerHasPermission(block.OwnerID.ToString(), Permissions.REMOVEBP))
                    block.SetFlag(BaseEntity.Flags.Reserved2, true);
            });


        private object OnCupboardAssign(BuildingPrivlidge privilege, ulong target, BasePlayer player)
        {
            if (privilege == null || player == null)
                return null;

            if (!_config.ATAllowed)
            {
                privilege.SendNetworkUpdate();
                return false;
            }

            if (privilege.authorizedPlayers.Count >= _config.TCCapacity)
            {
                SendDiscordNotify(privilege, "Authorize", player.userID);
                privilege.SendNetworkUpdate();
                return false;
            }
             
            NextTick(() =>
            {
                if (privilege == null || player == null)
                    return;
                
                var position = privilege.transform.position;

                AuthPlayerInTurrets(player.userID, position);
                AuthPlayerInLock(player.userID, position, privilege.authorizedPlayers);
            });

            return null;
        }
        private object OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (privilege == null || player == null || PlayerHasPermission(player.UserIDString, Permissions.ALL))
                return null;

            if (privilege.authorizedPlayers.Count >= _config.TCCapacity)
            {
                SendDiscordNotify(privilege, "Authorize", player.userID);
                return false;
            }
            
            NextTick(() =>
            {
                if (privilege == null || player == null)
                    return;
                
                var position = privilege.transform.position;
                if (_config.ARTeam)
                {
                    var names = new List<string>();
                    foreach (var member in GetTeamMembers(player.UserIDString))
                        if (PlayerHasPermission(member.ToString(), Permissions.AUTH))
                        {
                            if (privilege.authorizedPlayers.Count >= _config.TCCapacity)
                            {
                                names.Add(BasePlayer.FindByID(member)?.displayName ?? "unknown");
                                continue;
                            }
                            
                            privilege.authorizedPlayers.RemoveWhere(x =>  x.userid == member);
                            PlayerNameID playerNameId = new PlayerNameID();
                            playerNameId.userid = member;
                            string str = BasePlayer.FindByID(member)?.displayName ?? "unknown";
                            playerNameId.username = str;
                            privilege.authorizedPlayers.Add(playerNameId);
          
                            AuthPlayerInTurrets(member, position);
                            AuthPlayerInLock(member, position, privilege.authorizedPlayers);
                        }
                    
                    privilege.UpdateMaxAuthCapacity();
                    
                    if (names.Count > 0) 
                        SendReply(player, $"Teammates were not authorized because the limit was reached {privilege.authorizedPlayers.Count}/{_config.TCCapacity}: " + names.Aggregate((x, y) => x + ", " + y));
                }
                
                AuthPlayerInTurrets(player.userID, position);
                AuthPlayerInLock(player.userID, position, privilege.authorizedPlayers);
            });

            return null;
        }

        private void OnCupboardClearList(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (privilege == null)
                return; 

            if (_config.ARTurrets)
                DeauthAllPlayerInTurrets(privilege.transform.position);

            if (_config.AROpen)
                DeauthAllPlayerInLock(privilege.transform.position);
        }

        private object OnSamSiteTarget(SamSite samSite, PlayerHelicopter target)
        {
            if (target == null || samSite == null)
                return null;

            if (!target.HasDriver())
                return false; 
            
            if (samSite.GetBuildingPrivilege() != null &&
                samSite.GetBuildingPrivilege().IsAuthed(target.GetDriver()) && PlayerHasPermission(target.GetDriver().UserIDString, Permissions.TURRET))
                return false;
            
            return null;
        }

        private void OnCupboardDeauthorize(BuildingPrivlidge privilege, BasePlayer player) =>
            NextTick(() =>
            {
                if (privilege == null || player == null)
                    return;

                if (_config.ARTurrets)
                    DeauthPlayerInTurrets(player.userID, privilege.transform.position);

                if (_config.AROpen)
                    DeauthPlayerInLock(player.userID, privilege.transform.position);
            });

        private void CanChangeCode(BasePlayer player, CodeLock codeLock, string newCode, bool isGuestCode)
        {
            if (codeLock == null || isGuestCode || !_config.AROpen)
                return;

            var allTCPlayers = codeLock.GetBuildingPrivilege()?.authorizedPlayers;
            if (allTCPlayers == null)
                return;

            foreach (var check in allTCPlayers)
                if (!codeLock.guestPlayers.Contains(check.userid))
                    codeLock.guestPlayers.Add(check.userid);

            codeLock.SendNetworkUpdate();
        }

        private object OnTurretAuthorize(AutoTurret turret, BasePlayer player)
        {
            if (turret == null || player == null || turret.authorizedPlayers.Count < _config.ATCapacity || PlayerHasPermission(player.UserIDString, Permissions.ALL))
                return null;

            return false;
        }
        #endregion
        

        #region API_HOOKS

        private void OnClanMemberJoined(string userID, List<string> members)
        {
            var player = BasePlayer.FindByID(ulong.Parse(userID));
            if (player == null)
                return;

            foreach (var tc in _buildingPrivileges)
                foreach (var playerNameID in tc.authorizedPlayers.ToArray())
                {
                    if (!members.Contains(playerNameID.userid.ToString()))
                        continue;

                    tc.authorizedPlayers.RemoveWhere(x => x.userid.ToString() == userID);
                    tc.authorizedPlayers.Add(new PlayerNameID { username = player.displayName, userid = ulong.Parse(userID) });

                    tc.UpdateMaxAuthCapacity();
                    tc.SendNetworkUpdate();

                    AuthPlayerInTurrets(player.userID, tc.transform.position);
                    AuthPlayerInLock(player.userID, tc.transform.position, tc.authorizedPlayers);   
                    break;
                }
        }

        private void OnClanMemberGone(string userID, List<string> memberUserIDs) => PlayerLeaveTeam(userID);

        private void OnClanDisbanded(List<string> members)
        {
            foreach (var check in members)
                PlayerLeaveTeam(check);
        }

        #endregion

        #region Commands

        [ChatCommand("tssettings")]
        private void cmdChattssettings(BasePlayer player, string command, string[] args)
        {
            if (!PlayerHasPermission(player.UserIDString, Permissions.UI))
            {
                SendReply(player, "You do not have permission to use this command");
                return;
            }

            ShowUIMain(player);
        } 

        [ConsoleCommand("UI_TA")]
        private void cmdConsole(ConsoleSystem.Arg arg)
        {
            if (arg?.Args == null && arg.Args.Length < 1) return;
            var player = arg.Player();
            switch (arg.GetString(0))
            {
                case "CHGMAXPLAYERS":
                    _config.TCCapacity = arg.GetInt(1);
                    break;
                case "CHGCHEST":
                    _config.OAChest = !_config.OAChest;
                    break;
                case "CHGOVEN":
                    _config.OAOven = !_config.OAOven;
                    break;
                case "CHGTURRETS":
                    _config.ARTurrets = !_config.ARTurrets;
                    if (_config.ARTurrets)
                        Subscribe(nameof(OnSamSiteTarget));
                    else
                        Unsubscribe(nameof(OnSamSiteTarget));
                    break;
                case "CHGREMOVE":
                    _config.ARRemove = !_config.ARRemove;
                    if (_config.ARRemove)
                        SetAlwaysDemolish();
                    else
                        RemoveAlwaysDemolist();
                    break;
                case "CHGOPEN":
                    _config.AROpen = !_config.AROpen;
                    break;
                case "CHGTEAM":
                    _config.ARTeam = !_config.ARTeam;
                    break;
            }

            SaveConfig();
            ShowUISetup(player);
        }

        #endregion

        #region Functions
        
        
        private void PlayerLeaveTeam(string targetPlayerID)
        {
            var members = GetTeamMembers(targetPlayerID);
            foreach (var tc in _buildingPrivileges)
            {
                if (tc == null || tc.IsDestroyed)
                    continue;

                if (tc.OwnerID.ToString() == targetPlayerID)
                {
                    foreach (var authPlayer in tc.authorizedPlayers.ToArray())
                    {
                        if (authPlayer.userid == tc.OwnerID || !members.Contains(authPlayer.userid))
                            continue;

                        tc.authorizedPlayers.RemoveWhere(x => x.userid == authPlayer.userid);

                        if (_config.ARTurrets)
                            DeauthPlayerInTurrets(authPlayer.userid, tc.transform.position);

                        if (_config.AROpen)
                            DeauthPlayerInLock(authPlayer.userid, tc.transform.position);
                    }

                    tc.SendNetworkUpdate();
                    continue;
                }

                NextTick(() =>
                {
                    if (tc == null || tc.IsDestroyed)
                        return;
                    
                    var newMembers = GetTeamMembers(targetPlayerID);
                    
                    if (tc.authorizedPlayers.Any(x => x.userid.ToString() != targetPlayerID && newMembers.Contains(x.userid)))
                        return;
                    
                    tc.authorizedPlayers.RemoveWhere(x => x.userid.ToString() == targetPlayerID);

                    if (_config.ARTurrets)
                        DeauthPlayerInTurrets(ulong.Parse(targetPlayerID), tc.transform.position);

                    if (_config.AROpen)
                        DeauthPlayerInLock(ulong.Parse(targetPlayerID), tc.transform.position);

                    tc.SendNetworkUpdate();
                });
            }
        }
        

        private List<ulong> GetTeamMembers(string player)
        {
            var teamMembers = Clans?.Call<List<string>>("GetClanMembers", player)?.ConvertAll(ulong.Parse) ?? new List<ulong>();
            var team = RelationshipManager.ServerInstance.teams.FirstOrDefault(x => x.Value.members.Contains(ulong.Parse(player)));
            if (team.Value != null)
                foreach (ulong check in team.Value.members.Where(check => !teamMembers.Contains(check)))
                    teamMembers.Add(check);

            return teamMembers;
        }
        

        private void SendDiscordNotify(DecayEntity entity, string action, ulong victimID, ulong targetID = 0)
        {
            if (NotifyCooldown.ContainsKey(targetID) && DateTime.Now.Subtract(NotifyCooldown[targetID]).TotalSeconds < 5)
                return;
            
            if (!NotifyCooldown.TryAdd(targetID, DateTime.Now))
                NotifyCooldown[targetID] = DateTime.Now;
            
            var entityName = entity.pickup.itemTarget?.displayName?.english ?? "Cupboard";
            var entityPosition = "teleportpos " + entity.transform.position.ToString().Replace(" ", "");

            var ownerID = entity.OwnerID;
            var ownerDisplayName = BasePlayer.FindAwakeOrSleeping(ownerID.ToString())?.displayName ?? "Not playing";
            var victimDisplayName = BasePlayer.FindAwakeOrSleeping(victimID.ToString())?.displayName ?? "Not playing";
            var targetDisplayName = BasePlayer.FindAwakeOrSleeping(targetID.ToString())?.displayName ?? "Not playing";
            var serverName = covalence.Server.Name;

            var assignPart = targetID == 0 ? "" : $"\\n\\n **Assigned to**\\n[{targetDisplayName}](https://steamcommunity.com/profiles/{targetID}) - {targetID}";

            var description = $"**{entityName}** Owned by\\n[{ownerDisplayName}](https://steamcommunity.com/profiles/{ownerID}) - {ownerID}\\n\\n **Try to {action.ToLower()} by**\\n[{victimDisplayName}](https://steamcommunity.com/profiles/{victimID}) - {victimID}{assignPart}\\n\\n **Teleport**\\nteleportpos {entityPosition} \\n\\n **Server:**\\n{serverName}";
            var sendMessage = "{\"embeds\": [{\"title\": \"{0}\", \"description\": \"{1}\", \"color\": 9109504}]}".Replace("{0}", entityName + " " + action).Replace("{1}", description); 
            
            var form = new WWWForm(); 
            form.AddField("payload_json", sendMessage);
            ServerMgr.Instance.StartCoroutine(PostToDiscord(_config.DiscordWebHook, form));
        }

        private IEnumerator PostToDiscord(string url, WWWForm data)
        {
            var www = UnityWebRequest.Post(url, data);
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Puts($"Failed to post to discord: {www.error}");
            }
        }
        
        private void AuthPlayerInLock(ulong id, Vector3 position, HashSet<PlayerNameID> authorizedPlayers)
        {
            if (!_config.AROpen || !PlayerHasPermission(id.ToString(), Permissions.LOCK))
                return;

            var locks = Facepunch.Pool.Get<List<BaseEntity>>();
            Vis.Entities(position, 18.5f, locks);
            foreach (var check in locks)
            {
                foreach (var child in check.children.ToArray())
                {
                    if (!(child is CodeLock)) continue;
                    var codeLock = child as CodeLock;
                    if (authorizedPlayers.FirstOrDefault(x => x.userid == codeLock.OwnerID) == null)
                        continue;

                    if (!codeLock.guestPlayers.Contains(id))
                        codeLock.guestPlayers.Add(id);

                    codeLock.SendNetworkUpdate();
                }
            }

            Facepunch.Pool.FreeUnmanaged(ref locks);
        }

        private void DeauthPlayerInLock(ulong id, Vector3 position)
        {
            var locks = Facepunch.Pool.Get<List<BaseEntity>>();
            Vis.Entities(position, 18.5f, locks);
            foreach (var check in locks)
            {
                foreach (var child in check.children.ToArray())
                {
                    if (!(child is CodeLock)) continue;
                    var codeLock = child as CodeLock;
                    foreach (var whitePlayer in codeLock.guestPlayers.ToArray())
                    {
                        if (whitePlayer == id)
                            codeLock.guestPlayers.Remove(whitePlayer);
                        codeLock.SendNetworkUpdate();
                    }

                }
            }

            Facepunch.Pool.FreeUnmanaged(ref locks);
        }

        private void DeauthAllPlayerInLock(Vector3 position)
        {
            var locks = Facepunch.Pool.Get<List<BaseEntity>>();
            Vis.Entities(position, 18.5f, locks);
            foreach (var check in locks)
            {
                foreach (var child in check.children.ToArray())
                {
                    if (!(child is CodeLock)) continue;
                    (child as CodeLock).guestPlayers.Clear();
                    (child as CodeLock).SendNetworkUpdate();
                }
            }

            Facepunch.Pool.FreeUnmanaged(ref locks);
        }

        private bool IsAlly(string ownerID, string suspectID) => Clans == null || Clans.Call<bool>("IsClanMember", ownerID, suspectID);

        private bool PlayerHasPermission(string userID, Permissions permName)
        {
            if (permission.UserHasPermission(userID, PERMS[Permissions.ALL]))
                return true;

            return !permission.UserHasPermission(userID, PERMS[Permissions.IGNORE]) && permission.UserHasPermission(userID, PERMS[permName]);
        }

        private void SetAlwaysDemolish()
        {
            foreach (var check in BaseNetworkable.serverEntities.OfType<BuildingBlock>()) 
                check.SetFlag(BaseEntity.Flags.Reserved2, true);
        }

        private void RemoveAlwaysDemolist()
        {
            foreach (var check in BaseNetworkable.serverEntities.OfType<BuildingBlock>()) 
                check.StartBeingDemolishable();
        }

        private void AuthPlayerInTurrets(ulong id, Vector3 position)
        {
            if (!_config.ARTurrets || !PlayerHasPermission(id.ToString(), Permissions.TURRET))
                return;

            var turrets = Facepunch.Pool.Get<List<AutoTurret>>();
            Vis.Entities(position, 18.5f, turrets);
            foreach (var check in turrets)
            {
                if (check.authorizedPlayers.Count >= _config.ATCapacity)
                    break;

                var isOnline = check.IsOnline();
                if (isOnline)
                    check.SetIsOnline(false);

                check.authorizedPlayers.RemoveWhere(x => x.userid == id);
                check.authorizedPlayers.Add(new PlayerNameID {username = "Player", userid = id});

                if (isOnline)
                    check.SetIsOnline(true);

                check.UpdateMaxAuthCapacity();
                check.SendNetworkUpdate();
            }

            Facepunch.Pool.FreeUnmanaged(ref turrets);
        }

        private void DeauthAllPlayerInTurrets(Vector3 position)
        {
            var turrets = Facepunch.Pool.Get<List<AutoTurret>>();
            Vis.Entities(position, 18.5f, turrets);
            foreach (var check in turrets)
            {
                check.authorizedPlayers.RemoveWhere(x => check.OwnerID != x.userid);
                check.authDirty = true;
                check.UpdateMaxAuthCapacity();
                check.SendNetworkUpdate();
            }

            Facepunch.Pool.FreeUnmanaged(ref turrets);
        }
 
        private void DeauthPlayerInTurrets(ulong id, Vector3 position)
        {
            var turrets = Facepunch.Pool.Get<List<AutoTurret>>();
            Vis.Entities(position, 18.5f, turrets);
            foreach (var check in turrets)
            {
                check.authorizedPlayers.RemoveWhere(x => x.userid == id);
                check.authDirty = true;
                check.UpdateMaxAuthCapacity();
                check.SendNetworkUpdate();
            }

            Facepunch.Pool.FreeUnmanaged(ref turrets);
        }

        
        #endregion

        #region UI

        private void ShowUISetup(BasePlayer player)
        {
            var container = new CuiElementContainer();
            var posY = -50;

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0.9"},
                Image = {Color = "0 0 0 0"}
            }, Layer + ".mainPanel", Layer);

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Text =
                {
                    Text = "The maximum number of players that can register in the TC: ", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.9 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}" },
                Image = { Color = "0.3 0.3 0.3 0.92"}
            }, Layer, Layer + ".input");

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text =
                {
                    Text = _config.TCCapacity.ToString(), Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 0.3"
                }
            }, Layer + ".input");

            container.Add(new CuiElement
            {
                Parent = Layer + ".input",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Command = "UI_TA CHGMAXPLAYERS",
                        Color = "1 1 1 1",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter,
                        CharsLimit = 3,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                    }
                }
            });

            posY -= 35;

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Text =
                {
                    Text = _config.OAChest ? "<color=green>ON</color>" : "<color=red>OFF</color>", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Button = {Color = "0 0 0 0", Command = "UI_TA CHGCHEST"},
                Text =
                {
                    Text = "Only registered players can open chests (when registered on TC)", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer);

            posY -= 35;

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Text =
                {
                    Text = _config.OAOven ? "<color=green>ON</color>" : "<color=red>OFF</color>", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Button = {Color = "0 0 0 0", Command = "UI_TA CHGOVEN"},
                Text =
                {
                    Text = "Only registered players can open furnaces (when registered on TC)", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer);

            posY -= 35;

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Text =
                {
                    Text = _config.ARTurrets ? "<color=green>ON</color>" : "<color=red>OFF</color>", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Button = {Color = "0 0 0 0", Command = "UI_TA CHGTURRETS "},
                Text =
                {
                    Text = "Automatically when players are registered on TC (Autohorized on Turrets and SAM site)", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer);

            posY -= 35;

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Text =
                {
                    Text = _config.ARRemove ? "<color=green>ON</color>" : "<color=red>OFF</color>", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Button = {Color = "0 0 0 0", Command = "UI_TA CHGREMOVE "},
                Text =
                {
                    Text = "Automatically when players are registered on the TC (remove building parts)", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer);

            posY -= 35;

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Text =
                {
                    Text = _config.AROpen ? "<color=green>ON</color>" : "<color=red>OFF</color>", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Button = {Color = "0 0 0 0", Command = "UI_TA CHGOPEN "},
                Text =
                {
                    Text = "Automatically when players are registered on the TC (open codelocks without a code)", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer);
            posY -= 35;

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Text =
                {
                    Text = _config.ARTeam ? "<color=green>ON</color>" : "<color=red>OFF</color>", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.025 1", AnchorMax = "0.975 1", OffsetMin = $"0 {posY}", OffsetMax = $"0 {posY + 30}"},
                Button = {Color = "0 0 0 0", Command = "UI_TA CHGTEAM "},
                Text =
                {
                    Text = "Automatically registered your teammates on the TC", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                }
            }, Layer);

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }

        private void ShowUIMain(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image = {Color = "0 0 0 0.95", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"}
            }, "Overlay", Layer + ".bg");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.2 0.4", AnchorMax = "0.8 0.8"},
                Image = {Color = "0 0 0 0.8"}
            }, Layer + ".bg", Layer + ".mainPanel");
            Outline(ref container, Layer + ".mainPanel");

            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0 0.9", AnchorMax = "1 1"},
                Text =
                {
                    Text = "SETUP TC", Font = "robotocondensed-bold.ttf", FontSize = 25,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, Layer + ".mainPanel");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0.9", AnchorMax = "1 0.9", OffsetMin = "0 0", OffsetMax = "0 2"},
                Image = {Color = "1 1 1 1"}
            }, Layer + ".mainPanel");

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-29 -29", OffsetMax = "0 0"},
                Button = {Color = "0 0 0 0", Close = Layer + ".bg"},
                Text =
                {
                    Text = "×", Font = "robotocondensed-regular.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter,
                    Color = "0.56 0.58 0.64 1.00"
                }
            }, Layer + ".mainPanel", Layer + ".buttonClose");
            Outline(ref container, Layer + ".buttonClose");

            CuiHelper.DestroyUi(player, Layer + ".bg");
            CuiHelper.AddUi(player, container);

            ShowUISetup(player);
        }

        private void Outline(ref CuiElementContainer container, string parent, string color = "1 1 1 1",
            string size = "2")
        {
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = $"0 0", OffsetMax = $"0 {size}"},
                Image = {Color = color}
            }, parent);
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"0 -{size}", OffsetMax = $"0 0"},
                Image = {Color = color}
            }, parent);
            container.Add(new CuiPanel
            {
                RectTransform =
                    {AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = $"0 {size}", OffsetMax = $"{size} -{size}"},
                Image = {Color = color}
            }, parent);
            container.Add(new CuiPanel
            {
                RectTransform =
                    {AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = $"-{size} {size}", OffsetMax = $"0 -{size}"},
                Image = {Color = color}
            }, parent);
        }

        #endregion
    }
}   