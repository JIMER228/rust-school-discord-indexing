

using Oxide.Core.Plugins;
using Oxide.Plugins.RaidLimitsMethods;
using Newtonsoft.Json.Linq;
using Oxide.Core.Libraries.Covalence;
using System.Linq;
using System.Xml.Linq;
using ConVar;
using System;
using ProtoBuf;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("RaidLimit", "mr01sam", "1.1.0")]
    [Description("Limit the amount of times players can perform raids.")]
    internal partial class RaidLimit : CovalencePlugin
    {
        [PluginReference]
        private readonly Plugin SimpleStatus, ImageLibrary, Clans, ResidentsApi;

        public static RaidLimit INSTANCE;

        private const string STATUS_ID = "RaidLimit";

        private const string PermissionAdmin = "RaidLimit.admin";
        private const string PermissionExempt = "RaidLimit.exempt";

        public bool UseSimpleStatus => (SimpleStatus?.IsLoaded ?? false) && config.SimpleStatus.Enabled;
        public bool UseClans => (Clans?.IsLoaded ?? false) && config.SyncClans;
        public bool HasImageLibrary => ImageLibrary?.IsLoaded ?? false;

        private void OnServerInitialized()
        {
            INSTANCE = this;
            LoadData();

            if (!permission.PermissionExists(PermissionAdmin)) { permission.RegisterPermission(PermissionAdmin, this); }
            if (!permission.PermissionExists(PermissionExempt)) { permission.RegisterPermission(PermissionExempt, this); }

            if (UseSimpleStatus)
            {
                SimpleStatus.CallHook("CreateStatus", this, STATUS_ID, config.SimpleStatus.BackgroundColor, "simple status title", config.SimpleStatus.TitleColor, $"{config.RaidLimit}/{config.RaidLimit}", config.SimpleStatus.TextColor, IsAssetPath(config.SimpleStatus.IconSprite) ?  config.SimpleStatus.IconSprite : "rl.ssicon", config.SimpleStatus.IconColor);
            }
            if ((!IsAssetPath(config.UI.Sprite) && config.UI.Enabled)
                || (!IsAssetPath(config.SimpleStatus.IconSprite) && UseSimpleStatus))
            {
                if (HasImageLibrary)
                {
                    var images = new Dictionary<string, string>();
                    if (config.UI.Enabled && !IsAssetPath(config.UI.Sprite))
                    {
                        images["rl.icon"] = config.UI.Sprite;
                    }
                    if (UseSimpleStatus && !IsAssetPath(config.SimpleStatus.IconSprite))
                    {
                        images["rl.ssicon"] = config.SimpleStatus.IconSprite;
                    }
                    AddImages(images, () =>
                    {
                        foreach (var basePlayer in BasePlayer.activePlayerList)
                        {
                            UpdateRaidCount(basePlayer.userID.Get());
                        }
                    });
                }
                else
                {
                    PrintError("The plugin ImageLibrary is not installed on your server and is required to show the icon URL you provided in your config.");
                }
            }
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                InitPlayer(basePlayer);
            }
            StartResetTimer();
            SubscribeAll(
                nameof(OnServerSave),
                nameof(OnEntityTakeDamage),
                nameof(OnEntityDeath),
                nameof(OnPlayerSleepEnded),
                nameof(OnPlayerDisconnected),
                nameof(OnUserPermissionGranted),
                nameof(OnGroupPermissionGranted),
                nameof(OnGroupPermissionRevoked)
            );
        }

        private void Unload()
        {
            UnsubscribeAll();
            StopResetTimer();
            ClearAllRaidCounts();
            SaveData();
        }

        #region Oxide Hooks

        public List<string> SubscribedHooks = new List<string>();
        private void SubscribeAll(params string[] hooks)
        {
            foreach (var hook in hooks)
            {
                SubscribedHooks.Add(hook);
                Subscribe(hook);
            }
        }
        private void UnsubscribeAll()
        {
            foreach (var hook in SubscribedHooks)
            {
                Unsubscribe(hook);
            }
        }

        void OnNewSave(string filename)
        {
            Data.ClearAll();
        }

        private void OnServerSave()
        {
            if (!config.SaveOnServerSave) { return; }
            SaveData();
        }

        void OnEntityTakeDamage(DecayEntity entity, HitInfo info)
        {
            var canTrigger = EntityCanTriggerRaid(entity);
            if (info.InitiatorPlayer == null || !canTrigger) { return; }
            var result = CanAllowRaid(entity, info.InitiatorPlayer, info);
            if (!result.AllowRaid)
            {
                if (config.PreventEarlyDamage || !result.EarlyDamage)
                {
                    info.damageTypes.ScaleAll(0);
                }
            }
        }

        void OnEntityDeath(BuildingPrivlidge priv, HitInfo info)
        {
            if (priv == null) { return; }
            var key = $"|{priv.net.ID.Value}";
            Data.BuildingsRaidedByPlayer.RemoveWhere(x => x.EndsWith($"|{key}"));
        }

        void OnPlayerSleepEnded(BasePlayer basePlayer)
        {
            if (basePlayer == null) { return; }
            timer.In(0.5f, () =>
            {
                InitPlayer(basePlayer);
            });
        }

        void OnPlayerDisconnected(BasePlayer basePlayer, string reason)
        {
            if (basePlayer == null) { return; }
            Data.PlayerDamageDoneToBuilding.Remove(basePlayer.userID.Get());
        }

        void OnUserPermissionGranted(string id, string permName)
        {
            if (permName == PermissionExempt)
            {
                ExemptPermissionUpdated(id);
            }
        }

        void OnUserPermissionRevoked(string id, string permName)
        {
            if (permName == PermissionExempt)
            {
                ExemptPermissionUpdated(id);
            }
        }

        void OnGroupPermissionGranted(string name, string permName)
        {
            if (permName == PermissionExempt)
            {
                var users = players.All.Where(p => permission.UserHasGroup(p.Id, name)).Select(x => x.Id).ToList();
                foreach (var userIdString in users)
                {
                    ExemptPermissionUpdated(userIdString);
                }
            }
        }

        void OnGroupPermissionRevoked(string name, string permName)
        {
            if (permName == PermissionExempt)
            {
                var users = players.All.Where(p => permission.UserHasGroup(p.Id, name)).Select(x => x.Id).ToList();
                foreach (var userIdString in users)
                {
                    ExemptPermissionUpdated(userIdString);
                }
            }
        }
        #endregion

        struct CanAllowResult
        {
            public static CanAllowResult TRUE = new CanAllowResult(true);
            public CanAllowResult(bool allowed, bool earlyDamage = false, bool isFreeRevenge = false)
            {
                AllowRaid = allowed;
                EarlyDamage = earlyDamage;
                IsFreeRevenge = isFreeRevenge;
            }
            public bool EarlyDamage;
            public bool AllowRaid;
            public bool IsFreeRevenge;
        }

        public void ExemptPermissionUpdated(string id)
        {
            if (ulong.TryParse(id, out ulong result))
            {
                UpdateRaidCount(result);
            }
        }

        public void InitPlayer(BasePlayer basePlayer)
        {
            UpdateRaidCount(basePlayer.userID.Get());
        }

        public RaidData Data = new RaidData();

        public static string CompoundKey(object key1, object key2)
        {
            return $"{key1}|{key2}";
        }

        public bool HasClan(ulong userId) => GetClanId(userId) != null;
        public string GetClanId(ulong userId)
        {
            string value = null;
            try
            {
                value = (string)Clans.CallHook("GetClanOf", userId);
                if (string.IsNullOrEmpty(value))
                {
                    value = null;
                }
            } catch (Exception)
            {
                PrintError("Failed to call hook 'GetClanOf' for Clans plugin");
            }
            return value;
        }

        public List<ulong> GetClanMemberIds(ulong userId)
        {
            List<ulong> ids = new List<ulong>();
            try
            {
                var value = (List<string>)Clans.CallHook("GetClanMembers", userId);
                if (value != null)
                {
                    foreach(var id in value.Select(x => ulong.Parse(x)))
                    {
                        ids.Add(id);
                    }
                }
            }
            catch (Exception)
            {
                PrintError("Failed to call hook 'GetClanMembers' for Clans plugin");
            }
            return ids;
        }

        public bool HasTeam(ulong userId) => GetTeam(userId) != null;

        public RelationshipManager.PlayerTeam GetTeam(ulong userId) => FindActivePlayer(userId)?.Team;

        public IPlayer FindPlayer(ulong userId) => INSTANCE.covalence.Players.FindPlayerById(userId.ToString());

        bool EntityCanTriggerRaid(BaseCombatEntity entity)
        {
            if (entity == null) { return false; }
            if (!(entity is DecayEntity || entity is BuildingPrivlidge)) { return false; }
            var priv = entity.GetBuildingPrivilege();
            if (priv == null) { return false; }
            if (priv.OwnerID == 0) { return false; }
            if (!config.CanTwigTriggerRaid && entity is BuildingBlock buildingBlock && buildingBlock.grade == BuildingGrade.Enum.Twigs) { return false; }
            if (config.IgnoredEntities != null && config.IgnoredEntities.Contains(entity.ShortPrefabName)) { return false; }
            return true;
        }

        private List<ulong> tempIdList = new List<ulong>();
        private Dictionary<ulong, List<ulong>> cachedPrivIds = new Dictionary<ulong, List<ulong>>();
        private List<ulong> GetBuildingPrivIdsForEntity(BaseCombatEntity entity)
        {
            tempIdList.Clear();
            // entity is attacked, need to know what privs are responsible
            var priv = entity.GetBuildingPrivilege();
            if (priv == null) { return tempIdList; }
            if (!config.CombineStackedPrivs)
            {
                tempIdList.Add(priv.net.ID.Value);
                return tempIdList;
            }
            // treat all nearby privs that are valid as one raid trigger

            // if we've already checked for this before, return cached value
            if (cachedPrivIds.ContainsKey(priv.net.ID.Value))
            {
                return cachedPrivIds[priv.net.ID.Value];
            }
            // otherwise we need to find all the related priv ids
            var privIds = GetRelatedPrivsForEntity(entity).Select(x => x.net.ID.Value).ToList();
            if (privIds.Count == 0) { return tempIdList; }
            foreach (var privId in privIds)
            {
                cachedPrivIds[privId] = privIds;
            }
            return cachedPrivIds[priv.net.ID.Value];
        }

        #region Combine Overlapping Privs
        private int count = 0;

        private HashSet<BuildingPrivlidge> GetRelatedPrivsForEntity(BaseEntity entity)
        {
            var privList = new HashSet<BuildingPrivlidge>();
            var domPriv = entity.GetBuildingPrivilege();
            if (domPriv != null)
            {
                count = 0;
                GetPrivsRelatedToThisOne(domPriv, privList, new HashSet<ulong>());
            }
            return privList;
        }

        private void GetPrivsRelatedToThisOne(BuildingPrivlidge thisPriv, HashSet<BuildingPrivlidge> privList, HashSet<ulong> checkedEntities, HashSet<ulong> authedPlayerList = null)
        {
            count++;
            // A related priv is one that is:
            // 1: Has a foundation within 16m of one of these foundations
            // 2: Has atleast one common authed player
            // 3: Is not this priv
            if (!thisPriv.Residents().Any()) { return; } // no point in checking if there are no authed players
            if (authedPlayerList == null)
            {
                authedPlayerList = thisPriv.Residents().ToHashSet();
            }
            // If this priv hasn't been added and it has common authed players, add it
            var thisPrivAuthPlayerIds = thisPriv.Residents();
            if (!privList.Contains(thisPriv) && thisPrivAuthPlayerIds.Intersect(authedPlayerList).Count() > 0)
            {
                privList.Add(thisPriv);
                authedPlayerList.UnionWith(thisPrivAuthPlayerIds);
            }
            else { return; } // no point in continuing if this one is invalid
            // Foreach foundation, see if there are any foundations from other privs nearby
            var foundations = thisPriv.Foundations();
            checkedEntities.UnionWith(foundations.Select(x => x.net.ID.Value)); // we don't want to check these foundations again
            foreach (var foundation in foundations)
            {
                // Theses are foundations that are NOT the ones from this priv (they were filtered)
                foreach (var nearbyFoundation in GetNearbyFoundations(foundation, 20f, checkedEntities))
                {
                    var nearbyPriv = nearbyFoundation.GetBuildingPrivilege();
                    if (nearbyPriv == null || privList.Contains(nearbyPriv)) { continue; }
                    GetPrivsRelatedToThisOne(nearbyPriv, privList, checkedEntities, authedPlayerList);
                }
            }
        }

        private HashSet<BuildingBlock> GetNearbyFoundations(BaseEntity fromEntity, float dist, HashSet<ulong> excludeIds)
        {
            var position = fromEntity.transform.position;
            float distance = dist;
            List<BuildingBlock> list = new List<BuildingBlock>();
            Vis.Entities<BuildingBlock>(position, distance, list);
            return list.Where(x => !excludeIds.Contains(x.net.ID.Value) && (x.ShortPrefabName == "foundation" || x.ShortPrefabName == "foundation.triangle")).ToHashSet();
        }
        #endregion

        CanAllowResult CanAllowRaid(BaseCombatEntity entity, BasePlayer attacker, HitInfo info)
        {
            if (attacker == null) { return CanAllowResult.TRUE; }
            if (permission.UserHasPermission(attacker.UserIDString, PermissionExempt)) { return CanAllowResult.TRUE; }
            var priv = entity.GetBuildingPrivilege();
            if (priv == null) { return CanAllowResult.TRUE; }
            if (priv.IsAuthed(attacker)) { return CanAllowResult.TRUE; }
            var buildingPrivId = priv.net.ID.Value;

            if (Data.HasAlreadyRaidedBuilding(attacker.userID, buildingPrivId)) { return CanAllowResult.TRUE; }

            if (Data.IsBaseFreeRaidable(attacker, priv))
            {
                if (Data.CanSendMessage(attacker.userID))
                {
                    Message(info.InitiatorPlayer, true, "revenge raid");
                    Data.SetMessageDelay(attacker.userID);
                }
                Data.FreelyRaidedBuildingsByPlayer.Add(CompoundKey(buildingPrivId, attacker.userID));
                return new CanAllowResult(true, isFreeRevenge: true);
            }

            if (Data.GetRemainingRaidCountForPlayer(attacker.userID) <= 0)
            {
                // Used up all raids
                if (Data.CanSendMessage(attacker.userID))
                {
                    Message(info.InitiatorPlayer, true, "no remaining raids");
                    Data.SetMessageDelay(attacker.userID);
                }
                return new CanAllowResult(false);
            }

            // Warn of damage
            if (config.PreventEarlyDamage)
            {
                // We don't allow damage, so to track the threshold we just total up the
                // attempted damage, but it will be blocked
                Data.AddRaidDamage(attacker.userID, buildingPrivId, info.damageTypes.Total());
                if (!Data.ExceedsDamageThreshold(attacker.userID, buildingPrivId))
                {
                    if (Data.CanSendMessage(attacker.userID))
                    {
                        Message(attacker, true, "raid warning");
                        Data.SetMessageDelay(attacker.userID);
                    }
                    return new CanAllowResult(false, earlyDamage: true);
                }
            }
            else
            {
                // We allow damage, and for compatability with other plugins, we want
                // to track the ACTUAL damage that was dealt
                if (!Data.ExceedsDamageThreshold(attacker.userID, buildingPrivId))
                {
                    var beforeHp = entity.health;
                    NextTick(() =>
                    {
                        if (entity == null || attacker == null) { return; }
                        var afterHp = entity.health;
                        var damage = beforeHp - afterHp;
                        if (damage > 0)
                        {
                            Data.AddRaidDamage(attacker.userID, buildingPrivId, damage);
                            if (Data.CanSendMessage(attacker.userID))
                            {
                                Message(attacker, true, "raid warning");
                                Data.SetMessageDelay(attacker.userID);
                            }
                        }
                    });
                    return new CanAllowResult(false, earlyDamage: true);
                }
            }

            // Spend a raid point
            if (!Data.AddRaidLog(attacker.userID, GetBuildingPrivIdsForEntity(entity), entity.transform.position))
            {
                // Used up all raids
                if (Data.CanSendMessage(attacker.userID))
                {
                    Message(info.InitiatorPlayer, true, "no remaining raids");
                    Data.SetMessageDelay(attacker.userID);
                }
                return new CanAllowResult(false);
            }
            if (INSTANCE.UseClans && HasClan(attacker.userID))
            {
                UpdateRaidCountsForClan(attacker.userID);
            }
            if (config.SyncTeams && HasTeam(attacker.userID))
            {
                UpdateRaidCountsForTeam(attacker.Team, attacker.userID);
            }
            UpdateRaidCount(attacker.userID);
            Message(attacker, true, (config.SyncTeams || INSTANCE.UseClans) ? "raid logged allies" : "raid logged", Data.GetRemainingRaidCountForPlayer(attacker.userID));
            Interface.CallHook("RaidLimits_OnPlayerStartedRaid", attacker.userID, buildingPrivId, priv.transform.position);
            return CanAllowResult.TRUE;
        }

        private void UpdateRaidCount(ulong userId)
        {
            var basePlayer = FindActivePlayer(userId);
            if (basePlayer == null) { return; }
            if (permission.UserHasPermission(userId.ToString(), PermissionExempt))
            {
                if (UseSimpleStatus)
                {
                    SimpleStatus.Call("SetStatus", basePlayer.userID.Get(), STATUS_ID, 0);
                }
                DestroyUI(basePlayer);
                return;
            }
            if (UseSimpleStatus)
            {
                if ((int)SimpleStatus.Call("GetDuration", basePlayer.userID.Get(), STATUS_ID) <= 0)
                {
                    SimpleStatus.Call("SetStatus", basePlayer.userID.Get(), STATUS_ID, int.MaxValue);
                }
                SimpleStatus.Call("SetStatusText", userId, STATUS_ID, $"{Data.GetRemainingRaidCountForPlayer(userId)}/{config.RaidLimit}");
            }
            if (config.UI.Enabled)
            {
                ShowUI(basePlayer);
            }
        }

        private void UpdateAllRaidCounts()
        {
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                UpdateRaidCount(basePlayer.userID.Get());
            }
        }

        private void ClearAllRaidCounts()
        {
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                if (SimpleStatus?.IsLoaded ?? false)
                {
                    SimpleStatus.Call("SetStatus", basePlayer.userID.Get(), STATUS_ID, 0);
                }
                DestroyUI(basePlayer);
            }
        }

        private void UpdateRaidCountsForTeam(RelationshipManager.PlayerTeam team, ulong ignoreId) => team.members.ForEach(x =>
        {
            if (x != ignoreId)
            {
                Message(FindActivePlayer(x), true, "ally raid logged");
                UpdateRaidCount(x);
            }
        });

        private void UpdateRaidCountsForClan(ulong userId) => GetClanMemberIds(userId).ForEach(x =>
        {
            if (x != userId) // ignore calling player
            {
                Message(FindActivePlayer(x), true, "ally raid logged");
                UpdateRaidCount(x);
            }
        });

        public List<TimeSpan> OrderedLimitResetTimes { get; set; } = new List<TimeSpan>();

        private Timer ResetTimer { get; set; }

        public void StartResetTimer()
        {
            OrderedLimitResetTimes = config.LimitResetTimes.Select(x =>
            {
                TimeSpan time;
                if (!TimeSpan.TryParse(x, out time)) { return null; }
                return (TimeSpan?)time;
            })
            .Where(x => x != null)
            .Select(x => x.Value)
            .OrderByDescending(x => x)
            .ToList();

            // If a reset was missed while server was offline
            var now = DateTime.Now;
            var lastResetTime = Data.LastResetTime;
            
            if (lastResetTime != null && OrderedLimitResetTimes.Any(time => time.AsTodaysDate() < now && time.AsTodaysDate() > lastResetTime))
            {
                var missedResetTime = OrderedLimitResetTimes.First(time => time.AsTodaysDate() < now && time.AsTodaysDate() > lastResetTime);
                Puts($"Last Reset was {lastResetTime}. Missed reset time of {missedResetTime}. Resetting Raid Limits now.");
                ResetRaidLimts();
                SaveData();
            }

            // Reset timer loop
            ResetTimer = timer.Every(60f, () =>
            {
                var now = DateTime.Now;
                if (OrderedLimitResetTimes.Any(time => now.Hour == time.Hours && now.Minute == time.Minutes))
                {
                    ResetRaidLimts();
                }
            });
        }

        public void StopResetTimer()
        {
            ResetTimer?.Destroy();
        }

        public void ResetRaidLimts()
        {
            Data.ClearAll();
            cachedPrivIds.Clear();
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                if (config.BroadcastReset)
                {
                    Message(basePlayer, true, "broadcast reset");
                }
                UpdateRaidCount(basePlayer.userID.Get());
            }
            Data.LastResetTime = DateTime.Now;
            if (config.LogResetInConsole)
            {
                Puts("Raid limits have been reset for all players.");
            }
            Interface.CallHook("RaidLimits_OnRaidLimitsReset");
        }

        private void LoadData()
        {
            try
            {
                Data = Interface.Oxide.DataFileSystem.ReadObject<RaidData>($"{Name}/Data");
            } catch(Exception)
            {
                Data = new RaidData();
            }
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/Data", Data);
        }
    }
}

namespace Oxide.Plugins
{
    internal partial class RaidLimit : CovalencePlugin
    {
        // These are example of hooks this plugin calls and you can subscribe to
#if FALSE
        private void RaidLimits_OnPlayerStartedRaid(ulong userId, ulong buildingPrivId, Vector3 position)
        {
            Puts("RaidLimits_OnPlayerStartedRaid called!");
        }

        private void RaidLimits_OnRaidLimitsReset()
        {
            Puts("RaidLimits_OnRaidLimitsReset called!");
        }
#endif

        // API Methods that can be called by other plugins
        private int RaidLimits_GetPlayerRaidLimitCount(ulong userId)
        {
            return Data.GetRemainingRaidCountForPlayer(userId);
        }

        private void RaidLimits_UpdateRaidLimitCountForPlayer(ulong userId)
        {
            UpdateRaidCount(userId);
        }

        private void RaidLimits_ResetRaidLimits()
        {
            ResetRaidLimts();
            UpdateAllRaidCounts();
            SaveData();
        }

        private void RaidLimits_GrantPlayerBonusRaids(ulong userId, int amount)
        {
            Data.GrantPlayerBonus(userId, amount);
            UpdateRaidCount(userId);
        }
    }
}

namespace Oxide.Plugins
{
    internal partial class RaidLimit : CovalencePlugin
    {
        [Command("rl.reset"), Permission(PermissionAdmin)]
        private void CmdResetRaids(IPlayer player, string command, string[] args)
        {
            ResetRaidLimts();
            UpdateAllRaidCounts();
            Message(player.Object as BasePlayer, false, "command raids reset");
            SaveData();
        }

        [Command("rl.bonus"), Permission(PermissionAdmin)]
        private void CmdBonus(IPlayer player, string command, string[] args)
        {
            try
            {
                if (args.Length != 2)
                {
                    throw new ArgumentException();
                }
                var target = covalence.Players.FindPlayer(args[0]);
                if (target == null)
                {
                    Message(player.Object as BasePlayer, false, "command no player found");
                    return;
                }
                var amount = 0;
                if (!int.TryParse(args[1], out amount))
                {
                    Message(player.Object as BasePlayer, false, "command invalid amount");
                    return;
                }
                var userId = target.UserId();
                Data.GrantPlayerBonus(userId, amount);
                UpdateRaidCount(userId);
                Message(player.Object as BasePlayer, false, "command granted bonus", target.Name, Data.GetRemainingRaidCountForPlayer(userId), config.RaidLimit);
            } catch (Exception)
            {
                Message(player.Object as BasePlayer, false, $"Usage: rl.bonus <player> <amount>");
            }
        }
    }
}

namespace Oxide.Plugins
{
    internal partial class RaidLimit : CovalencePlugin
    {
        private Configuration config;
        private partial class Configuration
        {
            public string[] LimitResetTimes { get; set; } = new string[] { "6:00", "18:00", "21:25", "21:29" };
            public bool AllowRevengeRaidForFree { get; set; } = true;
            public bool CanTwigTriggerRaid { get; set; } = false;
            public bool BroadcastReset { get; set; } = true;
            public bool LogResetInConsole { get; set; } = true;
            public bool SyncTeams { get; set; } = true;
            public bool SyncClans { get; set; } = true;
            public bool ClearBonusesOnReset { get; set; } = true;
            public float BuildingDamageThreshold { get; set; } = 10f;
            public bool PreventEarlyDamage { get; set; } = true;
            public bool CombineStackedPrivs { get; set; } = true;
            public int RaidLimit { get; set; } = 3;
            public bool SaveOnServerSave { get; set; } = true;
            public string[] IgnoredEntities { get; set; } = new string[]
            {
                "sleepingbag_leather_deployed"
            };
            public MessageConfig Messages { get; set; } = new MessageConfig();
            public SimpleStatusConfig SimpleStatus { get; set; } = new SimpleStatusConfig();
            public UIConfig UI { get; set; } = new UIConfig();
        }

        private class SimpleStatusConfig
        {
            public bool Enabled { get; set; } = true;
            public string BackgroundColor { get; set; } = "0.77255 0.23922 0.15686 1";
            public string TitleColor { get; set; } = "1 0.82353 0.44706 1";
            public string TextColor { get; set; } = "1 0.82353 0.44706 1";
            public string IconColor { get; set; } = "1 0.82353 0.44706 1";
            public string IconSprite { get; set; } = "assets/icons/explosion_sprite.png";
        }

        private class MessageConfig
        {
            public bool ShowChatMessages { get; set; } = true;
            public ulong MessageIconSteamId { get; set; } = 0;
        }

        private class UIConfig
        {
            public bool Enabled { get; set; } = true;
            public int Width { get; set; } = 130;
            public int Height { get; set; } = 34;
            public string BackgroundColor { get; set; } = "0.4 0.4 0.4 0.7";
            public string TextColor { get; set; } = "1 1 1 1";
            public int TextSize { get; set; } = 12;
            public bool ShowSprite { get; set; } = true;
            public string Sprite { get; set; } = "assets/icons/explosion_sprite.png";
            public string SpriteColor { get; set; } = "1 1 1 1";
            public int SpriteSize { get; set; } = 24;
            public int OffsetX { get; set; } = -146;
            public int OffsetY { get; set; } = 340;
            public float AnchorX { get; set; } = 1f;
            public float AnchorY { get; set; } = 0f;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            //// UNCOMMENT TO DEBUG
            //LoadDefaultConfig();
            //PrintWarning("DEFAULT CONFIG IS LOADED");
            //return;
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
            SaveConfig();
        }
        protected override void SaveConfig() => Config.WriteObject(config);
        protected override void LoadDefaultConfig() => config = new Configuration();
    }
}

namespace Oxide.Plugins
{
    internal partial class RaidLimit : CovalencePlugin
    {
        public class RaidLogEntry
        {
            public DateTime TimeAttacked { get; set; }
            public IEnumerable<ulong> BuildingPrivIds { get; set; }
            public string AttackingClanID { get; set; }
            public Vector3 RaidPosition { get; set; }
        }

        public class RaidData
        {
            #region Properties
            public DateTime? LastResetTime { get; set; } = null;
            
            // Contains a list of all the raids a player has participated in
            public Dictionary<ulong, HashSet<Guid>> PlayerRaidLogIds = new Dictionary<ulong, HashSet<Guid>>();

            public Dictionary<ulong, int> PlayerBonuses = new Dictionary<ulong, int>();

            // Key=(privId, attackerUserId)
            public HashSet<string> BuildingsRaidedByPlayer = new HashSet<string>();

            // Key=(privId, attackerUserId)
            public HashSet<string> FreelyRaidedBuildingsByPlayer = new HashSet<string>();

            public Dictionary<Guid, RaidLogEntry> RaidLogEntries = new Dictionary<Guid, RaidLogEntry>();

            public Dictionary<ulong, HashSet<Guid>> VictimsOfRaids = new Dictionary<ulong, HashSet<Guid>>();

            [JsonIgnore]
            public Dictionary<ulong, Dictionary<ulong, float>> PlayerDamageDoneToBuilding = new Dictionary<ulong, Dictionary<ulong, float>>();

            [JsonIgnore]
            public Dictionary<ulong, float> MessageDelayTime = new Dictionary<ulong, float>();
            #endregion

            #region Public Methods
            public int GetPlayerRaidCount(ulong userId) => PlayerRaidLogIds.GetValueOrDefault(userId)?.Count ?? 0;

            public int GetRemainingRaidCountForPlayer(ulong userId)
            {
                return Math.Max(0, INSTANCE.config.RaidLimit + PlayerBonuses.GetValueOrDefault(userId) - GetPlayerRaidCount(userId));
            }

            public bool IsBaseFreeRaidable(BasePlayer attacker, BuildingPrivlidge priv)
            {
                if (INSTANCE.config.AllowRevengeRaidForFree)
                {
                    // Get owners of this priv
                    var ownerIds = priv.Residents();
                    // Get bases that the attacker owns that have been raided
                    var raidsDoneToAttacker = VictimsOfRaids.GetValueOrDefault(attacker.userID, new HashSet<Guid>());
                    foreach (var raidLogGuid in raidsDoneToAttacker)
                    {
                        if (RaidLogEntries.ContainsKey(raidLogGuid))
                        {
                            var raidLog = RaidLogEntries[raidLogGuid];
                            foreach (var userid in ownerIds)
                            {
                                foreach (var privid in raidLog.BuildingPrivIds)
                                {
                                    if (BuildingsRaidedByPlayer.Contains(CompoundKey(privid, userid)))
                                    {
                                        return true; // the owners of this base have raided one of your bases
                                    }
                                }
                            }
                        }
                    }
                }
                return false;
            }

            public void GrantPlayerBonus(ulong userId, int amount)
            {
                PlayerBonuses[userId] = PlayerBonuses.GetValueOrDefault(userId) + amount;
            }

            public List<RaidLogEntry> GetPlayerRaidLogEntries(ulong userId)
            {
                if (!PlayerRaidLogIds.ContainsKey(userId)) { return new List<RaidLogEntry>(); }
                return PlayerRaidLogIds.GetValueOrDefault(userId).Select(x => RaidLogEntries.GetValueOrDefault(x, null))
                    .Where(x => x != null)
                    .ToList();
            }

            public bool HasAlreadyRaidedBuilding(ulong userId, ulong buildingPrivId) => BuildingsRaidedByPlayer.Contains(CompoundKey(buildingPrivId, userId)) || FreelyRaidedBuildingsByPlayer.Contains(CompoundKey(buildingPrivId, userId));

            public bool ExceedsDamageThreshold(ulong userId, ulong buildingPrivId)
            {
                var damage = PlayerDamageDoneToBuilding.GetValueOrNew(userId).GetValueOrDefault(buildingPrivId);
                return damage >= INSTANCE.config.BuildingDamageThreshold;
            }

            public void AddRaidDamage(ulong userId, ulong buildingPrivId, float damageDealt)
            {
                var damage = PlayerDamageDoneToBuilding.GetValueOrNew(userId);
                damage[buildingPrivId] = damage.GetValueOrDefault(buildingPrivId) + damageDealt;
            }

            public bool AddRaidLog(ulong userId, List<ulong> buildingPrivIds, Vector3 raidPosition)
            {
                var guid = Guid.NewGuid();
                if (INSTANCE.config.SyncTeams)
                {
                    var team = INSTANCE.GetTeam(userId);
                    if (team != null)
                    {
                        foreach(var memberId in team.members)
                        {
                            if (memberId == userId) { continue; }
                            AddRaidLogHelper(guid, memberId, buildingPrivIds, raidPosition);
                            PlayerDamageDoneToBuilding.GetValueOrNew(memberId).RemoveAll(buildingPrivIds);
                        }
                    }
                }
                if (INSTANCE.UseClans)
                {
                    var clanId = INSTANCE.GetClanId(userId);
                    if (clanId != null)
                    {
                        foreach (var memberId in INSTANCE.GetClanMemberIds(userId))
                        {
                            if (memberId == userId) { continue; }
                            AddRaidLogHelper(guid, memberId, buildingPrivIds, raidPosition);
                            PlayerDamageDoneToBuilding.GetValueOrNew(memberId).RemoveAll(buildingPrivIds);
                        }
                    }
                }
                PlayerDamageDoneToBuilding.GetValueOrNew(userId).RemoveAll(buildingPrivIds);
                return AddRaidLogHelper(guid, userId, buildingPrivIds, raidPosition);
            }

            public bool CanSendMessage(ulong userId) => UnityEngine.Time.realtimeSinceStartup >= MessageDelayTime.GetValueOrDefault(userId);

            public void SetMessageDelay(ulong userId) => MessageDelayTime[userId] = UnityEngine.Time.realtimeSinceStartup + 6f;
            #endregion
            private bool AddRaidLogHelper(Guid guid, ulong userId, IEnumerable<ulong> buildingPrivIds, Vector3 raidPosition)
            {
                if (GetRemainingRaidCountForPlayer(userId) <= 0) { return false; }
                PlayerRaidLogIds.GetValueOrNew(userId).Add(guid);
                if (!RaidLogEntries.ContainsKey(guid))
                {
                    RaidLogEntries[guid] = new RaidLogEntry
                    {
                        BuildingPrivIds = buildingPrivIds,
                        RaidPosition = raidPosition
                    };
                }
                foreach(var privId in buildingPrivIds)
                {
                    var priv = FindEntity(privId) as BuildingPrivlidge;
                    if (priv != null)
                    {
                        foreach (var userid in priv.Residents())
                        {
                            VictimsOfRaids.GetValueOrNew(userid).Add(guid);
                        }
                    }
                }
                foreach (var buildingPrivId in buildingPrivIds)
                {
                    BuildingsRaidedByPlayer.Add(CompoundKey(buildingPrivId, userId));
                }
                return true;
            }

            public void ClearAll()
            {
                PlayerRaidLogIds = new Dictionary<ulong, HashSet<Guid>>();
                BuildingsRaidedByPlayer = new HashSet<string>();
                RaidLogEntries = new Dictionary<Guid, RaidLogEntry>();
                PlayerDamageDoneToBuilding = new Dictionary<ulong, Dictionary<ulong, float>>();
                FreelyRaidedBuildingsByPlayer = new HashSet<string>();
                VictimsOfRaids = new Dictionary<ulong, HashSet<Guid>>();
                if (INSTANCE.config.ClearBonusesOnReset)
                {
                    PlayerBonuses = new Dictionary<ulong, int>();
                }
            }
        }
    }
}

namespace Oxide.Plugins.RaidLimitsMethods
{
    public static class ExtensionMethods
    {
        public static ulong UserId(this IPlayer player)
        {
            return ulong.Parse(player.Id);
        }

        public static void RemoveAll<T,V>(this Dictionary<T,V> dict, IEnumerable<T> keys)
        {
            foreach (var key in keys)
            {
                dict.Remove(key);
            }
        }
        
        public static IEnumerable<ulong> Residents(this BuildingPrivlidge priv)
        {
            return RaidLimit.GetResidents(priv);
        }

        public static IEnumerable<BuildingBlock> Foundations(this BuildingPrivlidge priv)
        {
            return priv.GetBuilding().buildingBlocks.Where(x => x.ShortPrefabName == "foundation" || x.ShortPrefabName == "foundation.triangle");
        }

        public static void AddRange<T>(this HashSet<T> hashSet, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                hashSet.Add(item);
            }
        }

        public static DateTime AsTodaysDate(this TimeSpan timespan) => DateTime.Now.Date.Add(timespan);
        public static V GetValueOrNew<K, V>(this Dictionary<K, V> dict, K key) where V : new()
        {
            var value = dict.GetValueOrDefault(key);
            if (value != null) { return value; }
            dict[key] = new V();
            return dict[key];
        }
    }
}

namespace Oxide.Plugins
{
    internal partial class RaidLimit
    {
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["simple status title"] = "DAILY RAIDS",
                ["ui text"] = "DAILY RAIDS: {0}/{1}",
                ["no remaining raids"] = "Raid blocked. You have exceeded the number of allowed raids. You cannot raid again until the next scheduled reset time.",
                ["looting requires raid"] = "You cannot loot this container unless you start a raid.",
                ["raid logged"] = "Raid logged. You have {0} raids remaining.",
                ["raid logged allies"] = "Raid logged for you and all allied members. You have {0} raid(s) remaining.",
                ["ally raid logged"] = "An ally has started a raid, it has been logged for you and all members.",
                ["bonus granted"] = "You were granted {0} bonus raid(s).",
                ["bonus granted allies"] = "You and your allied members were granted {0} bonus raid(s).",
                ["raid warning"] = "Continued damage to this structure will count as a raid.",
                ["broadcast reset"] = "Raid limits have been reset.",
                ["command raids reset"] = "Raids have been reset for all players.",
                ["command no player found"] = "No player with that name was found.",
                ["command invalid amount"] = "That is an invalid amount.",
                ["command granted bonus"] = "The player {0} now has {1}/{2} raids remaining.",
                ["command invalid arguments"] = "Invalid arguments given for command.",
                ["revenge raid"] = "The owners of this base raided one of your bases, you can raid them for free as revenge."
            }, this);
        }
        
        private string Lang(string key, string id, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private string Lang(string key, BasePlayer basePlayer, params object[] args) => string.Format(lang.GetMessage(key, this, basePlayer?.UserIDString), args);
    }
}

namespace Oxide.Plugins
{
    internal partial class RaidLimit : CovalencePlugin
    {
        [Command("rl.test"), Permission(PermissionAdmin)]
        private void CmdRlTest(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            var priv = basePlayer.GetBuildingPrivilege();
            player.Message($"IsFree? {Data.IsBaseFreeRaidable(basePlayer, priv)}");
        }


        [Command("rl.test.attackedby"), Permission(PermissionAdmin)]
        private void CmdAttackedBy(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            var userid = ulong.Parse(args[0]);
            var target = FindPlayer(userid);
            var priv = basePlayer.GetBuildingPrivilege();
            Data.AddRaidLog(target.UserId(), GetBuildingPrivIdsForEntity(priv), priv.transform.position);
            player.Message($"Simulated that this base was attacked by {target.Name}");
        }
    }
}

namespace Oxide.Plugins
{
    internal partial class RaidLimit : CovalencePlugin
    {
        private const string UI_ID = "rlui";

        private string CachedUI = null;

        private void ShowUI(BasePlayer basePlayer)
        {
            if (basePlayer == null) { return; }
            if (CachedUI == null)
            {
                var container = new CuiElementContainer();
                var width = config.UI.Width;
                var height = config.UI.Height;
                var padding = 8;
                var spriteSize = config.UI.SpriteSize;
                var textSize = config.UI.TextSize;
                var sprite = config.UI.Sprite;
                var spriteColor = config.UI.SpriteColor;
                var textColor = config.UI.TextColor;
                var color = config.UI.BackgroundColor;
                var ox = config.UI.OffsetX;
                var oy = config.UI.OffsetY;

                container.Add(new CuiElement
                {
                    Parent = "Hud",
                    Name = UI_ID,
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"{config.UI.AnchorX} {config.UI.AnchorY}",
                            AnchorMax = $"{config.UI.AnchorX} {config.UI.AnchorY}",
                            OffsetMin = $"{ox} {oy}",
                            OffsetMax = $"{ox+width} {oy+height}"
                        },
                        new CuiImageComponent
                        {
                            Color = color
                        }
                    }
                });

                var spritePadding = 0;
                if (config.UI.ShowSprite)
                {
                    spritePadding = spriteSize;
                    var image = new CuiImageComponent
                    {
                        Color = spriteColor
                    };
                    if (IsAssetPath(sprite))
                    {
                        image.Sprite = sprite;
                    }
                    else
                    {
                        image.Png = GetImage("rl.icon");
                    }
                    container.Add(new CuiElement
                    {
                        Parent = UI_ID,
                        Components =
                        {
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0.5",
                                AnchorMax = "0 0.5",
                                OffsetMin = $"{padding} {-spriteSize/2}",
                                OffsetMax = $"{padding+spriteSize} {spriteSize/2}"
                            },
                            image
                        }
                    });
                }

                container.Add(new CuiElement
                {
                    Parent = UI_ID,
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = $"{padding+spritePadding} {0}",
                            OffsetMax = $"{-padding} {0}"
                        },
                        new CuiTextComponent
                        {
                            Text = "{LabelText}",
                            Align = config.UI.ShowSprite ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter,
                            FontSize = textSize,
                            Color = textColor
                        }
                    }
                });

                CachedUI = container.ToJson();
            }
            CuiHelper.DestroyUi(basePlayer, UI_ID);
            CuiHelper.AddUi(basePlayer, CachedUI
                .Replace("{LabelText}", Lang("ui text", basePlayer, Data.GetRemainingRaidCountForPlayer(basePlayer.userID.Get()).ToString(), config.RaidLimit.ToString())));
        }

        private void DestroyUI(BasePlayer basePlayer)
        {
            CuiHelper.DestroyUi(basePlayer, UI_ID);
        }
    }
}

namespace Oxide.Plugins
{
    internal partial class RaidLimit : CovalencePlugin
    {
        private void Message(BasePlayer basePlayer, bool respectConfig, string message, params object[] args)
        {
            if (basePlayer == null)
            {
                Puts(message);
            }
            else if (!respectConfig || config.Messages.ShowChatMessages)
            {
                var icon = config.Messages.MessageIconSteamId;
                ConsoleNetwork.SendClientCommand(basePlayer.Connection, "chat.add", 2, icon, Lang(message, basePlayer, args));
            }
        }

        private void AddImages(Dictionary<string, string> images, Action callback = null)
        {
            ImageLibrary?.Call("ImportImageList", Name, images, 0UL, true, callback);
        }

        private string GetImage(string title)
        {
            return ImageLibrary?.Call<string>("GetImage", title);
        }

        private bool IsAssetPath(string path) => !string.IsNullOrWhiteSpace(path) && path.StartsWith("assets/");

        public static BasePlayer FindActivePlayer(ulong userId) => BasePlayer.FindAwakeOrSleepingByID(userId);

        public static BasePlayer FindActivePlayerOrBot(ulong userId) => BasePlayer.FindAwakeOrSleepingByID(userId) ?? BasePlayer.FindBot(userId);

        public static BaseNetworkable FindEntity(ulong entityId)
        {
            return BaseNetworkable.serverEntities.Find(new NetworkableId(entityId));
        }

        public static IEnumerable<ulong> GetResidents(BuildingPrivlidge priv)
        {
            if (INSTANCE.ResidentsApi?.IsLoaded ?? false)
            {
                return ((Dictionary<string, string[]>)INSTANCE.ResidentsApi?.Call("GetResidents", priv))["all"].Select(x => ulong.Parse(x));
            }
            else
            {
                return priv.authorizedPlayers.Select(x => x.userid);
            }
            
        }
    }
}
