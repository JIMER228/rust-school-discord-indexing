using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using Physics = UnityEngine.Physics;

namespace Oxide.Plugins
{
    /*
     * This Version Changes:
     * - Add more detail to breached layer names. 
     */
    [Info("ESP Detection", "Pho3niX90", "1.1.2")]
    [Description("Records a demo of suspected ESPers")]
    internal class EspDetection : RustPlugin
    {
        bool debug = false;

        [PluginReference] Plugin AutoDemoRecord;

        const string PermissionIgnore = "espdetection.ignore";
        private Configuration config;
        private int[] LAYERS_CAST = { 0, 4, 8, 21, 17 };
        private int[] LAYERS_BLACKLIST = { 17 };
        private readonly int BUFFER_TIME = 3;

        private readonly string[] IGNORE_FIRST_SEEN =
            {"wall.frame.shopfront", "wall.frame.shopfront.metal", "autoturret", "flameturret"};

        Dictionary<ulong, List<Violation>> _allViolations = new Dictionary<ulong, List<Violation>>();
        List<ViolationSound> _allSounds = new List<ViolationSound>();
        Dictionary<ulong, ViolationMeta> _maxViolation = new Dictionary<ulong, ViolationMeta>();

        #region Helpers

        bool HasPermission(BasePlayer player, string permissionName = PermissionIgnore)
        {
            return permission.UserHasPermission(player.UserIDString, permissionName);
        }

        void Record(BasePlayer player, string msg) =>
            AutoDemoRecord?.Call("API_StartRecording4", player, msg, config.ADR.recordMinutes, config.ADR.webhook ?? config.Discord.webhook);

        double GetPercentage(int val)
        {
            LogDebug($"{val} violations against {Samples()} samples");
            return Math.Round(((double)val / (double)Samples()) * 100d);
        }

        int Samples() => config.General.checkInterval * config.Violations.intervalSamples;

        bool CheckViolations(ulong playerId, int cnt)
        {
            BasePlayer player = BasePlayer.FindByID(playerId);
            //clear old data 
            try
            {
                _allViolations[playerId] =
                    _allViolations[playerId]
                        .Take(config.Violations.intervalSamples).ToList();
                // ?.RemoveAll(x => x.dateTime < DateTime.Now.AddSeconds(-(Samples())));
                _allSounds?.RemoveAll(x => x.dateTime < DateTime.Now.AddSeconds(-(Samples() + BUFFER_TIME)));
            }
            catch (Exception e)
            {
                Puts(e.Message);
            }

            if (player != null && HasPermission(player))
            {
                return false;
            }

            double perc = Math.Min(GetPercentage(cnt), 99);
            LogDebug(perc.ToString());

            if (_maxViolation.ContainsKey(playerId))
            {
                if (_maxViolation[playerId].maxViolation < perc)
                {
                    _maxViolation[playerId].dateTime = DateTime.Now;
                    _maxViolation[playerId].maxViolation = perc;
                    return false;
                }
            }
            else
            {
                _maxViolation.Add(playerId, new ViolationMeta() { dateTime = DateTime.Now, maxViolation = perc });
            }

            var trigger = false;

            if (_maxViolation[playerId].dateTime < DateTime.Now.AddSeconds(-BUFFER_TIME))
            {
                LogDebug(
                    $"violationCurrentMax {_maxViolation[playerId].maxViolation}, maxViolationsConfig {config.Violations.maxViolationsPerc}");
                trigger = _maxViolation[playerId].maxViolation >= config.Violations.maxViolationsPerc;
                if (trigger)
                    timer.Once(0.1f, () =>
                    {
                        _maxViolation[playerId].maxViolation = 0d;
                        _allViolations.Clear();
                    });
            }

            LogDebug($"Triggered: {trigger}, {perc}");
            return trigger;
        }

        #endregion

        #region Commands

        [ChatCommand("testesp")]
        private void StashTestPlaceCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            List<Violation> violations = new List<Violation>();
            violations.Add(new Violation
            {
                dateTime = DateTime.Now,
                distance = 50,
                rotation = 67,
                victim = 7656L,
                victimBlip = new Blip
                {
                    x = 47,
                    y = 67
                },
                victimName = "Random Player"
            });


            DiscordSend(player, violations);
        }

        [ConsoleCommand("testesp")]
        private void StashTestPlaceCommandConsole(ConsoleSystem.Arg cmdArgs)
        {
            if (!cmdArgs.IsAdmin && !cmdArgs.IsConnectionAdmin) return;
            List<Violation> violations = new List<Violation>();
            violations.Add(new Violation
            {
                dateTime = DateTime.Now,
                distance = 50,
                rotation = 67,
                victim = 7656L,
                victimBlip = new Blip
                {
                    x = 47,
                    y = 67
                },
                victimName = "Pho3"
            });

            DiscordSend(new BasePlayer() { userID = 76561199078488569L, UserIDString = "76561199078488569" }, violations);
        }

        [ChatCommand("testlayer")]
        private void TestLayerCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;

            RaycastHit[] hitInfos = Physics.RaycastAll(player.eyes.HeadRay(), 100);

            var hitsSorted = (from p in hitInfos
                              orderby Vector3.Distance(p.transform.position, player.transform.position)
                              //where LAYERS_CAST.Contains(p.transform.gameObject.layer)
                              select p).ToArray();

            var firstEnt = hitsSorted[0].GetEntity();

            BaseEntity blacklistedEntity;

            for (int i = 0; i < hitsSorted.Length; i++)
            {
                var hitInfo = hitsSorted[i];
                // if (!LAYERS_BLACKLIST.Contains(hitInfo.transform.gameObject.layer)) continue;
                if (debug)
                    SendReply(player, i + $": Layer ({hitInfo.transform.gameObject.layer}): " +
                                      LayerMask.LayerToName(hitInfo.transform.gameObject.layer) + "\n Shortname: " +
                                      hitInfo.GetEntity()?.ShortPrefabName + "\n Dist: " +
                                      Vector3.Distance(hitInfo.transform.position, player.transform.position));
            }
        }

        ViolationTestResult TestEsp(BasePlayer player, int distance)
        {
            RaycastHit[] hitInfos = Physics.RaycastAll(player.eyes.HeadRay(), distance);

            var hitsSorted = (from p in hitInfos
                              orderby Vector3.Distance(p.transform.position, player.transform.position)
                              where LAYERS_CAST.Contains(p.transform.gameObject.layer)
                              select p).ToArray();

            if (hitsSorted == null || !hitsSorted.Any()) return null;
            var firstEnt = hitsSorted[0].GetEntity();

            BaseEntity blacklistedEntity;
            try
            {
                blacklistedEntity = hitsSorted
                    .FirstOrDefault(x => LAYERS_BLACKLIST.Contains(x.transform.gameObject.layer)).GetEntity();
            }
            catch (Exception e)
            {
                return null;
            }

            /***
             * TODO use breached layers and draw them to the radar image. 
             */
            List<ViolationBreachedLayer> entitiesBreached = new List<ViolationBreachedLayer>();
            try
            {
                entitiesBreached = hitsSorted.Select(x => new ViolationBreachedLayer()
                {
                    id = x.transform.gameObject.layer,
                    layerType = LayerMask.LayerToName(x.transform.gameObject.layer),
                    objectName = x.transform.gameObject.name,
                    position = x.transform.position
                }).ToList();
            }
            catch (Exception e) { }

            if (HasSight(player, blacklistedEntity.transform.position))
            {
                return null;
            }

            /***
             * We ignore common first seen prefabs, that cause false positives. 
             */
            if (IGNORE_FIRST_SEEN.Contains(firstEnt.ShortPrefabName))
            {
                return null;
            }

            for (int i = 0; i < hitsSorted.Length; i++)
            {
                var hitInfo = hitsSorted[i];
                if (LAYERS_BLACKLIST.Contains(hitInfo.transform.gameObject.layer))
                {
                    if (debug)
                    {
                        SendReply(player, i + $": Layer ({hitInfo.transform.gameObject.layer}): " +
                                          LayerMask.LayerToName(hitInfo.transform.gameObject.layer) + "\n Shortname: " +
                                          hitInfo.GetEntity()?.ShortPrefabName + "\n Dist: " +
                                          Vector3.Distance(hitInfo.transform.position, player.transform.position)
                        );
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return new ViolationTestResult() { baseEntity = blacklistedEntity, breachedLayers = entitiesBreached };
        }

        void SaveViolation(ulong userId, Violation violation)
        {
            // save violation
            if (_allViolations.ContainsKey(userId))
            {
                _allViolations[userId].Add(violation);
            }
            else
            {
                _allViolations.Add(userId, new List<Violation>() { violation });
            }
        }

        /// <summary>
        /// Checks if the entity can see 
        /// </summary>
        /// <param name="ent"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        bool HasSight(BaseEntity ent, Vector3 pos) => ent.IsVisible(pos) && ent.IsVisibleAndCanSee(pos);

        #endregion

        #region Hooks

        void OnPlayerVoice(BasePlayer player, byte[] data)
        {
            if (MadeNoiseIn(player.userID, 1)) return;

            LogDebug("No sound in 1 sec");
            _allSounds.Add(new ViolationSound()
            { playerId = player.userID, dateTime = DateTime.Now, type = SoundType.Voice });
        }

        void OnServerInitialized()
        {
            LoadConfig();

            if (!permission.PermissionExists(PermissionIgnore, this))
                permission.RegisterPermission(PermissionIgnore, this);

            timer.Once(config.General.checkInterval, RefreshData);
        }

        void OnServerSave()
        {
            ClearVoices();
        }

        #endregion

        #region Main Logic

        void RefreshData()
        {
            try
            {
                Stopwatch perfTotal = new Stopwatch();
                Dictionary<ulong, ulong> dataList = new Dictionary<ulong, ulong>();

                var allPlayers = BasePlayer.activePlayerList
                    .Where(x =>
                        x.IdleTime < config.General.ignoreIdleTime
                    ).ToList();

                if (allPlayers != null && allPlayers.Count > 0)
                    for (int i = 0, n = allPlayers.Count; i < n; i++)
                    {
                        var currentPlayer = allPlayers[i];

                        if (currentPlayer == null)
                            continue;

                        DoChecks(currentPlayer);

                        /***
                         * If we only want to look at players with weapons active, then we ignore this player if they have one active.
                         */
                        if (config.General.trackWeaponsOnly &&
                            !currentPlayer.IsHostileItem(currentPlayer.GetActiveItem()))
                            continue;

                        var isOutside = currentPlayer.IsOutside();
                        var isAiming = currentPlayer.IsAiming;

                        var insideDistance = config.General.checkDistanceInside;
                        var outsideDistance = config.General.checkDistanceOutside;

                        var activeItem = currentPlayer.GetActiveItem();
                        var attachments = activeItem.contents?.itemList.Select(x => x.info.shortname).Distinct()
                            .ToArray();

                        // Lets check if there is specific settings for this weapon.
                        //LogDebug($"Before Weapon");
                        var weaponConfig = config.Weapons.FirstOrDefault(x => x.shortname == activeItem.info.shortname);
                        if (weaponConfig != null)
                        {
                            // Lets check if it should be tracked, if not, we ignore this player. 
                            if (!weaponConfig.track)
                            {
                                continue;
                            }

                            // set the weapon specific distances.
                            insideDistance = weaponConfig.checkDistanceInside;
                            outsideDistance = weaponConfig.checkDistanceOutside;
                        }

                        var addMax = 0;
                        // Lets check if there is specific settings for these attachments, if the player is aiming.
                        //LogDebug($"Before Attachment");
                        if (isAiming)
                        {
                            var ignorePlayer = false;

                            foreach (var attachment in attachments)
                            {
                                var attachmentConfig =
                                    config.Attachments.FirstOrDefault(x => x.shortname == attachment);
                                if (attachmentConfig != null)
                                {
                                    // Lets check if it should be tracked, if not, we ignore this player. 
                                    if (!attachmentConfig.track)
                                    {
                                        ignorePlayer = true;
                                        break;
                                    }

                                    addMax = Math.Max(attachmentConfig.checkDistanceAiming, addMax);
                                }
                            }

                            if (ignorePlayer)
                            {
                                continue;
                            }
                        }

                        var distanceToCheck = (isOutside ? outsideDistance : insideDistance) + addMax;

                        var violationResult = TestEsp(currentPlayer, distanceToCheck);
                        var bEnt = violationResult.baseEntity;

                        if (bEnt == null)
                            continue;

                        Violation violation = null;
                        if (bEnt is BasePlayer)
                        {
                            var sawPlayer = bEnt as BasePlayer;
                            var distanceToPlayer = bEnt.Distance(currentPlayer);

                            /***
                             * Lets check if the player is far enough, if not lets ignore them
                             */
                            if (config.General.ignoreDistance > distanceToPlayer)
                                continue;

                            /***
                             * If the player is NPC, and we should ignore bots, then lets skip this player if they are NPC
                             */
                            if (config.General.ignoreNpcBots && (sawPlayer.IsNpc || !sawPlayer.userID.IsSteamId()))
                                continue;

                            /***
                             * Let's ignore teammates
                             */
                            if (IsTeamMate(currentPlayer.userID, sawPlayer.userID))
                                continue;

                            /***
                             * Ignore friendly players
                             */
                            if (currentPlayer.IsFriendly(sawPlayer))
                                continue;

                            // violation detected. 
                            violation = new Violation
                            {
                                dateTime = DateTime.Now,
                                distance = distanceToPlayer,
                                rotation = (int)Math.Round(currentPlayer.eyes.rotation.eulerAngles.y),
                                victimName = sawPlayer.displayName,
                                victim = sawPlayer.userID,
                                victimBlip = new Blip
                                {
                                    x = (int)Math.Round(sawPlayer.transform.position.x -
                                                         currentPlayer.transform.position.x),
                                    y = (int)Math.Round(sawPlayer.transform.position.y -
                                                         currentPlayer.transform.position.y)
                                },
                                breachedLayers = violationResult.breachedLayers
                            };
                        }
                        else if (bEnt is BuildingPrivlidge && config.General.trackTcs)
                        {
                            var tc = bEnt as BuildingPrivlidge;

                            if (tc.authorizedPlayers.Where(x => x.userid == currentPlayer.userID)?.Any() ?? false)
                                continue;

                            violation = new Violation
                            {
                                dateTime = DateTime.Now,
                                distance = bEnt.Distance(currentPlayer),
                                rotation = (int)Math.Round(currentPlayer.eyes.rotation.eulerAngles.y),
                                victim = bEnt.net.ID.Value,
                                isPlayer = false,
                                victimName = bEnt.ShortPrefabName,
                                breachedLayers = violationResult.breachedLayers,
                            };
                        }

                        if (violation != null)
                            SaveViolation(currentPlayer.userID, violation);
                    }

                PerfStop(perfTotal, "Total");

                timer.Once(config.General.checkInterval, RefreshData);
            }
            catch (Exception e)
            {
                timer.Once(config.General.checkInterval, RefreshData);
            }
        }

        void DoChecks(BasePlayer currentPlayer)
        {
            if (currentPlayer.userID.IsSteamId())
            {
                if (!_allViolations.ContainsKey(currentPlayer.userID)) return;
                var violations = _allViolations[currentPlayer.userID];

                // assign voice data
                for (int i = 0; i < violations.Count; i++)
                {
                    violations[i].lastVoice = LastVoice(violations[i].victim);
                }

                LogDebug($"violations: {violations.Count}");
                if (CheckViolations(currentPlayer.userID, violations.Count))
                {
                    try
                    {
                        DiscordSend(currentPlayer, violations);
                    }
                    catch (Exception e)
                    {
                        Puts(e.Message);
                        // add simple discord webhook.
                    }
                }
            }
        }

        #endregion

        #region Configuration

        [Serializable]
        private class Configuration
        {
            [JsonProperty(PropertyName = "General")]
            internal GeneralConfig General { get; set; }

            [JsonProperty(PropertyName = "Auto Demo Record")]
            internal ADRConfig ADR { get; set; }

            [JsonProperty(PropertyName = "Discord")]
            internal DiscordConfig Discord { get; set; }

            [JsonProperty(PropertyName = "Violations")]
            internal ViolationsConfig Violations { get; set; }

            [JsonProperty(PropertyName = "Weapon Specific Configs")]
            internal List<WeaponsConfig> Weapons { get; set; }

            [JsonProperty(PropertyName = "Attachment Specific Configs")]
            internal List<AttachmentConfig> Attachments { get; set; }

            internal class GeneralConfig
            {
                [JsonProperty(PropertyName = "Max distance to check (outside)")]
                internal int checkDistanceOutside { get; set; }

                [JsonProperty(PropertyName = "Max distance to check (inside)")]
                internal int checkDistanceInside { get; set; }

                [JsonProperty(PropertyName = "Check Interval (in seconds)")]
                internal int checkInterval { get; set; }

                [JsonProperty(PropertyName = "Ignore Players Idle (in seconds)")]
                internal int ignoreIdleTime { get; set; }

                [JsonProperty(PropertyName = "Ignore Team Mates")]
                internal bool ignoreFriendly { get; set; }

                [JsonProperty(PropertyName = "Only track players with active weapons")]
                internal bool trackWeaponsOnly { get; set; }

                [JsonProperty(PropertyName = "Ignore NPC's & Bot's")]
                internal bool ignoreNpcBots { get; set; }

                [JsonProperty(PropertyName = "Track Tc's")]
                internal bool trackTcs { get; set; }

                [JsonProperty(PropertyName = "Ignore distance shorter than X meters")]
                internal float ignoreDistance { get; set; }
            }

            internal class ViolationsConfig
            {
                [JsonProperty(PropertyName = "Probability %")]
                internal int maxViolationsPerc { get; set; }

                [JsonProperty(PropertyName = "Samples")]
                internal int intervalSamples { get; set; }
            }

            internal class ADRConfig
            {
                [JsonProperty(PropertyName = "Record Length (in minutes)")]
                internal int recordMinutes { get; set; }
                [JsonProperty(PropertyName = "Webhook to send recordings")]
                internal string webhook { get; set; }
            }

            internal class WeaponsConfig
            {
                [JsonProperty(PropertyName = "Weapon Shortname")]
                internal string shortname { get; set; }

                [JsonProperty(PropertyName = "Max distance to check (outside)")]
                internal int checkDistanceOutside { get; set; }

                [JsonProperty(PropertyName = "Max distance to check (inside)")]
                internal int checkDistanceInside { get; set; }

                [JsonProperty(PropertyName = "Track")] internal bool track { get; set; }
            }

            internal class AttachmentConfig
            {
                [JsonProperty(PropertyName = "Attachment Shortname")]
                internal string shortname { get; set; }

                [JsonProperty(PropertyName = "Add distance while aiming")]
                internal int checkDistanceAiming { get; set; }

                [JsonProperty(PropertyName = "Track")] internal bool track { get; set; }
            }

            internal class DiscordConfig
            {
                [JsonProperty(PropertyName = "Webhook")]
                internal string webhook { get; set; }

                [JsonProperty(PropertyName = "Webhook Title")]
                internal string title { get; set; }

                [JsonProperty(PropertyName = "Convert SteamIDs to Username embed")]
                internal bool convertSteamId { get; set; }

                [JsonProperty(PropertyName = "Add Radar Image")]
                internal bool radarImage { get; set; }

                [JsonProperty(PropertyName = "Attach ESP Samples")]
                internal bool discordSendSampels { get; set; }

                [JsonProperty(PropertyName = "2 columns instead of 3")]
                internal bool twoColumn { get; set; }
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                    throw new JsonException();
            }
            catch
            {
                Config.WriteObject(config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.json.backup");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .json.backup extension");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        private Configuration GetDefaultSettings()
        {
            return new Configuration
            {
                General = new Configuration.GeneralConfig
                {
                    checkDistanceInside = 100,
                    checkDistanceOutside = 150,
                    checkInterval = 1,
                    ignoreIdleTime = 15,
                    trackWeaponsOnly = true,
                    ignoreFriendly = true,
                    ignoreNpcBots = true,
                    trackTcs = false,
                    ignoreDistance = 5f
                },
                ADR = new Configuration.ADRConfig
                {
                    recordMinutes = 5
                },
                Discord = new Configuration.DiscordConfig
                {
                    webhook = string.Empty,
                    title = "ESP Detection",
                    radarImage = true,
                    twoColumn = true,
                    discordSendSampels = true
                },
                Violations = new Configuration.ViolationsConfig
                {
                    intervalSamples = 30,
                    maxViolationsPerc = 70
                },
                Weapons = new List<Configuration.WeaponsConfig>()
                {
                    new Configuration.WeaponsConfig
                        {shortname = "rifle.l96", checkDistanceInside = 50, checkDistanceOutside = 130, track = true},
                    new Configuration.WeaponsConfig
                        {shortname = "rifle.bolt", checkDistanceInside = 50, checkDistanceOutside = 120, track = true}
                },
                Attachments = new List<Configuration.AttachmentConfig>()
                {
                    new Configuration.AttachmentConfig
                        {shortname = "weapon.mod.8x.scope", checkDistanceAiming = 180, track = true},
                    new Configuration.AttachmentConfig
                        {shortname = "weapon.mod.small.scope", checkDistanceAiming = 180, track = true}
                }
            };
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            config = GetDefaultSettings();
            Puts("Creating new configuration file...");
            SaveConfig();
        }

        void LogDebug(string var)
        {
            if (!debug) return;
            PrintToChat(var);
            PrintToConsole(var);
            Puts(var);
        }

        void PerfStop(Stopwatch w, string tag = "")
        {
            w.Stop();
            if (debug)
                Puts((tag.Length > 0 ? $"{tag} / " : "") + $"Elapsed: {w.ElapsedMilliseconds}ms");
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ADR Message"] = "Possible ESP: {value}% probability."
            }, this);
        }

        string GetMsg(string key) => lang.GetMessage(key, this);

        #region Classes

        bool IsTeamMate(ulong p1, ulong p2) => GetTeamMembers(p1).Contains(p2);
        bool IsNpc(BaseEntity ent) => ent.IsNpc || !(ent.ToPlayer() != null && ent.ToPlayer().userID.IsSteamId());

        List<ulong> GetTeamMembers(ulong userid) =>
            RelationshipManager.ServerInstance.FindPlayersTeam(userid)?.members ?? new List<ulong>();

        void DiscordSend(BasePlayer player, List<Violation> violationsTemp)
        {
            LogDebug("DiscordSend");

            var probability = 0d;
            if (player && _maxViolation.ContainsKey(player.userID))
                probability = _maxViolation[player.userID].maxViolation;

            var msgRec = GetMsg("ADR Message").Replace("{value}", probability.ToString());

            LogDebug("Record");
            Record(player, msgRec);

            /* new */
            var msg = new DiscordMessageConfig
            {
                Content = string.Empty,
                Embed = new EmbedConfig
                {
                    Title = config.Discord.title,
                    Description = msgRec,
                    Color = "#de8732",
                    Image = string.Empty,
                    Thumbnail = string.Empty, // "https://assets.umod.org/images/icons/user/5e6f8629518b5.jpg",
                    Fields = new List<FieldConfig> { },
                    Footer = new FooterConfig
                    {
                        IconUrl = "https://assets.umod.org/images/icons/user/5e6f8629518b5.jpg",
                        Text = $"{Title} V{Version} by {Author}",
                        Enabled = true
                    },
                    Enabled = true
                }
            };

            msg.Embed.Fields.Add(new FieldConfig()
            {
                Title = "Culprit",
                Value =
                    $"{player?.displayName} - {player?.UserIDString}\n[Steam](https://steamcommunity.com/profiles/{player?.UserIDString}) - [SA](https://serverarmour.com/profile/{player?.UserIDString}) - [BM](https://www.battlemetrics.com/rcon/players?filter%5Bsearch%5D={player?.UserIDString})",
                Inline = false,
                Order = 1,
                Enabled = true
            });

            var violations = violationsTemp?.Take(config.Violations.intervalSamples).ToList();
            if (config.Discord.discordSendSampels && violationsTemp != null && violationsTemp.Count > 0)
            {
                int i = 1;
                for (int index = 0, n = violations.Count; index < n; index++)
                {
                    var violation = violations[index];
                    var appendVoice = violation.lastVoice != null ? $"\n{violation.lastVoice.dateTime.ToLongTimeString()}" : "";

                    var vicIdent = violation.isPlayer ? violation.victim.ToString() : violation.victimName;
                    if (config.Discord.convertSteamId && violation.isPlayer)
                    {
                        var vicBPlayer = covalence.Players.FindPlayer(vicIdent);
                        if (vicBPlayer != null)
                            vicIdent = $"{vicBPlayer?.Name}(https://steamcommunity.com/profiles/{vicIdent})";
                    }

                    msg.Embed.Fields.Add(new FieldConfig()
                    {
                        Title = $"VID: {i} - {violation.dateTime.ToLongTimeString()}",
                        Value = $"Victim: {vicIdent}" +
                                $"\nDistance: {violation.distance:0}" +
                                $"\nBearing: {violation.rotation}{appendVoice}" +
                                $"\nBreached Layers: {string.Join(",", violation.breachedLayers.Select(x => x.objectName))}",
                        Inline = true,
                        Order = i,
                        Enabled = true
                    });

                    if (config.Discord.twoColumn && i % 2 == 0)
                    {
                        msg.Embed.Fields.Add(new FieldConfig()
                        {
                            Title = $"** **",
                            Value = $"** **",
                            Inline = false,
                            Order = i,
                            Enabled = true
                        });
                    }

                    i++;
                };
            }

            Puts($"Before radar, status: {config.Discord.radarImage}");
            Puts(JsonConvert.SerializeObject(violations));
            string imageUrl = string.Empty;
            if (config.Discord.radarImage && violationsTemp != null && violationsTemp.Count > 0)
            {
                Puts($"https://esp.serverarmour.com/api/v1/radar/create");
                Puts($"data={JsonConvert.SerializeObject(violations)}");
                webrequest.Enqueue($"https://esp.serverarmour.com/api/v1/radar/create",
                    $"data={JsonConvert.SerializeObject(violations)}",
                    (code, rr) =>
                    {
                        if (code <= 204)
                        {
                            imageUrl = rr.Replace("\"", "").Trim();
                            msg.Embed.Image = imageUrl;
                            Puts(imageUrl);
                        }
                        else
                        {
                            Puts($"Radar creation failed: {code} - {rr}");
                        }
                    }, this, RequestMethod.POST);
            }

            try
            {
                webrequest.Enqueue($"https://steamcommunity.com/profiles/{player.userID}?xml=1",
                    string.Empty,
                    (code2, result) =>
                    {
                        if (code2 >= 200 && code2 <= 204)
                            msg.Embed.Thumbnail =
                                new Regex(
                                    @"(?<=<avatarMedium>[\w\W]+)https://.+\.jpg(?=[\w\W]+<\/avatarMedium>)",
                                    RegexOptions.Compiled).Match(result).Value;
                    }, this);
            }
            catch (Exception _ignore)
            {
                // we delay so that we are sure the radar image has been created.
            }

            timer.Once(5, () =>
            {
                Interface.CallHook("API_EspDetected", JsonConvert.SerializeObject(new { steamId = player.userID, violations = violationsTemp, radarUrl = Uri.EscapeDataString(imageUrl) }));
                DiscordMessage message = ParseMessage(msg);
                if (config.Discord.webhook.Length == 0 || config.Discord.webhook.Equals("")) return;
                SendDiscordMessage(config.Discord.webhook, message);
            });
        }

        class Violation
        {
            public DateTime dateTime;
            public ulong victim;
            public string victimName;
            public int rotation;
            public float distance;
            public Blip victimBlip;
            public bool isPlayer = true;
            public ViolationSound lastVoice = null;
            public List<ViolationBreachedLayer> breachedLayers = new List<ViolationBreachedLayer>();
        }

        class ViolationTestResult
        {
            public BaseEntity baseEntity;
            public List<ViolationBreachedLayer> breachedLayers;
        }

        class ViolationBreachedLayer
        {
            public int id;
            public string layerType;
            public string objectName;
            public Vector3 position;
        }

        class Blip
        {
            public int x;
            public int y;
        }

        class ViolationMeta
        {
            public DateTime dateTime;
            public double maxViolation;
        }

        class ViolationSound
        {
            public ulong playerId;
            public DateTime dateTime;
            public SoundType type;
        }

        enum SoundType
        {
            Voice
        }

        #endregion

        #region Discord Embed

        #region Send Embed Methods

        /// <summary>
        /// Headers when sending an embeded message
        /// </summary>
        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>()
        {
            {"Content-Type", "application/json"}
        };

        /// <summary>
        /// Sends the DiscordMessage to the specified webhook url
        /// </summary>
        /// <param name="url">Webhook url</param>
        /// <param name="message">Message being sent</param>
        private void SendDiscordMessage(string url, DiscordMessage message)
        {
            webrequest.Enqueue(url, message.ToJson(), SendDiscordMessageCallback, this, RequestMethod.POST, _headers);
        }

        /// <summary>
        /// Callback when sending the embed if any errors occured
        /// </summary>
        /// <param name="code">HTTP response code</param>
        /// <param name="message">Response message</param>
        private void SendDiscordMessageCallback(int code, string message)
        {
            if (code != 204)
            {
                PrintError(message);
            }
        }


        /// <summary>
        /// Sends the DiscordMessage to the specified webhook url with attachments
        /// </summary>
        /// <param name="url">Webhook url</param>
        /// <param name="message">Message being sent</param>
        /// <param name="files">Attachments to be added to the DiscordMessage</param>
        private void SendDiscordAttachmentMessage(string url, DiscordMessage message, List<Attachment> files)
        {
            List<IMultipartFormSection> formData = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("payload_json", message.ToJson())
            };

            for (int i = 0; i < files.Count; i++)
            {
                Attachment attachment = files[i];
                formData.Add(new MultipartFormFileSection($"file{i + 1}", attachment.Data, attachment.Filename,
                    attachment.ContentType));
            }

            InvokeHandler.Instance.StartCoroutine(SendDiscordAttachmentMessageHandler(url, formData));
        }

        private IEnumerator SendDiscordAttachmentMessageHandler(string url, List<IMultipartFormSection> data)
        {
            UnityWebRequest www = UnityWebRequest.Post(url, data);
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                PrintError($"{www.error} {www.downloadHandler.text}");
            }
        }

        #endregion

        #region Helper Methods

        private const string OwnerIcon =
            "https://steamcdn-a.akamaihd.net/steamcommunity/public/images/avatars/47/47db946f27bc76d930ac82f1656f7a10707bb67d_full.jpg";

        bool MadeNoiseIn(ulong playerId, int secondsAgo)
        {
            return _allSounds.Count(x => x.playerId == playerId && x.dateTime >= DateTime.Now.AddSeconds(-secondsAgo)) >
                   0;
        }

        ViolationSound LastVoice(ulong playerId)
        {
            return _allSounds.LastOrDefault(x => x.type == SoundType.Voice && x.playerId == playerId);
        }

        void ClearVoices()
        {
            var sounds = _allSounds.FindAll(x => x.dateTime >= DateTime.Now.AddSeconds(-600));
            _allSounds = sounds;
        }

        private void AddPluginInfoFooter(Embed embed)
        {
            embed.AddFooter($"{Title} V{Version} by {Author}", OwnerIcon);
        }

        private string GetPositionField(Vector3 pos)
        {
            return $"{pos.x:0.00} {pos.y:0.00} {pos.z:0.00}";
        }

        #endregion

        #region Embed Classes

        private class DiscordMessage
        {
            /// <summary>
            /// The name of the user sending the message changing this will change the webhook bots name
            /// </summary>
            [JsonProperty("username")]
            private string Username { get; set; }

            /// <summary>
            /// The avatar url of the user sending the message changing this will change the webhook bots avatar
            /// </summary>
            [JsonProperty("avatar_url")]
            private string AvatarUrl { get; set; }

            /// <summary>
            /// String only content to be sent
            /// </summary>
            [JsonProperty("content")]
            private string Content { get; set; }

            /// <summary>
            /// Embeds to be sent
            /// </summary>
            [JsonProperty("embeds")]
            private List<Embed> Embeds { get; }

            public DiscordMessage(string username = null, string avatarUrl = null)
            {
                Username = username;
                AvatarUrl = avatarUrl;
                Embeds = new List<Embed>();
            }

            public DiscordMessage(string content, string username = null, string avatarUrl = null)
            {
                Content = content;
                Username = username;
                AvatarUrl = avatarUrl;
                Embeds = new List<Embed>();
            }

            public DiscordMessage(Embed embed, string username = null, string avatarUrl = null)
            {
                Embeds = new List<Embed> { embed };
                Username = username;
                AvatarUrl = avatarUrl;
            }

            /// <summary>
            /// Adds a new embed to the list of embed to send
            /// </summary>
            /// <param name="embed">Embed to add</param>
            /// <returns>This</returns>
            /// <exception cref="IndexOutOfRangeException">Thrown if more than 10 embeds are added in a send as that is the discord limit</exception>
            public DiscordMessage AddEmbed(Embed embed)
            {
                if (Embeds.Count >= 10)
                {
                    throw new IndexOutOfRangeException("Only 10 embed are allowed per message");
                }

                Embeds.Add(embed);
                return this;
            }

            /// <summary>
            /// Adds string content to the message
            /// </summary>
            /// <param name="content"></param>
            /// <returns></returns>
            public DiscordMessage AddContent(string content)
            {
                Content = content;
                return this;
            }

            /// <summary>
            /// Changes the username and avatar image for the bot sending the message
            /// </summary>
            /// <param name="username">username to change</param>
            /// <param name="avatarUrl">avatar img url to change</param>
            /// <returns>This</returns>
            public DiscordMessage AddSender(string username, string avatarUrl)
            {
                Username = username;
                AvatarUrl = avatarUrl;
                return this;
            }

            /// <summary>
            /// Returns message as JSON to be sent in the web request
            /// </summary>
            /// <returns></returns>
            public string ToJson() => JsonConvert.SerializeObject(this, Formatting.None,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        }

        private class Embed
        {
            /// <summary>
            /// Color of the left side bar of the embed message
            /// </summary>
            [JsonProperty("color")]
            private int Color { get; set; }

            /// <summary>
            /// Fields to be added to the embed message
            /// </summary>
            [JsonProperty("fields")]
            private List<Field> Fields { get; } = new List<Field>();

            /// <summary>
            /// Title of the embed message
            /// </summary>
            [JsonProperty("title")]
            private string Title { get; set; }

            /// <summary>
            /// Description of the embed message
            /// </summary>
            [JsonProperty("description")]
            private string Description { get; set; }

            /// <summary>
            /// Image to added to the embed message. Appears at the bottom of the message above the footer
            /// </summary>
            [JsonProperty("image")]
            private Image Image { get; set; }

            /// <summary>
            /// Thumbnail image added to the embed message. Appears in the top right corner
            /// </summary>
            [JsonProperty("thumbnail")]
            private Image Thumbnail { get; set; }

            /// <summary>
            /// Author to add to the embed message. Appears above the title.
            /// </summary>
            [JsonProperty("author")]
            private AuthorInfo Author { get; set; }

            /// <summary>
            /// Footer to add to the embed message. Appears below all content.
            /// </summary>
            [JsonProperty("footer")]
            private Footer Footer { get; set; }

            /// <summary>
            /// Adds a title to the embed message
            /// </summary>
            /// <param name="title">Title to add</param>
            /// <returns>This</returns>
            public Embed AddTitle(string title)
            {
                Title = title;
                return this;
            }

            /// <summary>
            /// Adds a description to the embed message
            /// </summary>
            /// <param name="description">description to add</param>
            /// <returns>This</returns>
            public Embed AddDescription(string description)
            {
                Description = description;
                return this;
            }

            /// <summary>
            /// Adds an author to the embed message. The author will appear above the title
            /// </summary>
            /// <param name="name">Name of the author</param>
            /// <param name="iconUrl">Icon Url to use for the author</param>
            /// <param name="url">Url to go to when the authors name is clicked on</param>
            /// <param name="proxyIconUrl">Backup icon url. Can be left null if you only have one icon url</param>
            /// <returns>This</returns>
            public Embed AddAuthor(string name, string iconUrl = null, string url = null, string proxyIconUrl = null)
            {
                Author = new AuthorInfo(name, iconUrl, url, proxyIconUrl);
                return this;
            }

            /// <summary>
            /// Adds a footer to the embed message
            /// </summary>
            /// <param name="text">Text to be added to the footer</param>
            /// <param name="iconUrl">Icon url to add in the footer. Appears to the left of the text</param>
            /// <param name="proxyIconUrl">Backup icon url. Can be left null if you only have one icon url</param>
            /// <returns>This</returns>
            public Embed AddFooter(string text, string iconUrl = null, string proxyIconUrl = null)
            {
                Footer = new Footer(text, iconUrl, proxyIconUrl);

                return this;
            }

            /// <summary>
            /// Adds an int based color to the embed. Color appears as a bar on the left side of the message
            /// </summary>
            /// <param name="color"></param>
            /// <returns></returns>
            public Embed AddColor(int color)
            {
                if (color < 0x0 || color > 0xFFFFFF)
                {
                    throw new Exception($"Color '{color}' is outside the valid color range");
                }

                Color = color;
                return this;
            }

            /// <summary>
            /// Adds a hex based color. Color appears as a bar on the left side of the message
            /// </summary>
            /// <param name="color">Color in string hex format</param>
            /// <returns>This</returns>
            /// <exception cref="Exception">Exception thrown if color is outside of range</exception>
            public Embed AddColor(string color)
            {
                int parsedColor = int.Parse(color.TrimStart('#'), NumberStyles.AllowHexSpecifier);
                if (parsedColor < 0x0 || parsedColor > 0xFFFFFF)
                {
                    throw new Exception($"Color '{color}' is outside the valid color range");
                }

                Color = parsedColor;
                return this;
            }

            /// <summary>
            /// Adds a RGB based color. Color appears as a bar on the left side of the message
            /// </summary>
            /// <param name="red">Red value between 0 - 255</param>
            /// <param name="green">Green value between 0 - 255</param>
            /// <param name="blue">Blue value between 0 - 255</param>
            /// <returns>This</returns>
            /// <exception cref="Exception">Thrown if red, green, or blue is outside of range</exception>
            public Embed AddColor(int red, int green, int blue)
            {
                if (red < 0 || red > 255 || green < 0 || green > 255 || green < 0 || green > 255)
                {
                    throw new Exception(
                        $"Color Red:{red} Green:{green} Blue:{blue} is outside the valid color range. Must be between 0 - 255");
                }

                Color = red * 65536 + green * 256 + blue;
                ;
                return this;
            }

            /// <summary>
            /// Adds a blank field.
            /// If inline it will add a blank column.
            /// If not inline will add a blank row
            /// </summary>
            /// <param name="inline">If the field is inline</param>
            /// <returns>This</returns>
            public Embed AddBlankField(bool inline)
            {
                Fields.Add(new Field("\u200b", "\u200b", inline));
                return this;
            }

            /// <summary>
            /// Adds a new field with the name as the title and value as the value.
            /// If inline will add a new column. If row will add in a new row.
            /// </summary>
            /// <param name="name"></param>
            /// <param name="value"></param>
            /// <param name="inline"></param>
            /// <returns></returns>
            public Embed AddField(string name, string value, bool inline)
            {
                Fields.Add(new Field(name, value, inline));
                return this;
            }

            /// <summary>
            /// Adds an image to the embed. The url should point to the url of the image.
            /// If using attachment image you can make the url: "attachment://{image name}.{image extension}
            /// </summary>
            /// <param name="url">Url for the image</param>
            /// <param name="width">width of the image</param>
            /// <param name="height">height of the image</param>
            /// <param name="proxyUrl">Backup url for the image</param>
            /// <returns></returns>
            public Embed AddImage(string url, int? width = null, int? height = null, string proxyUrl = null)
            {
                Image = new Image(url, width, height, proxyUrl);
                return this;
            }

            /// <summary>
            /// Adds a thumbnail in the top right corner of the embed
            /// If using attachment image you can make the url: "attachment://{image name}.{image extension}
            /// </summary>
            /// <param name="url">Url for the image</param>
            /// <param name="width">width of the image</param>
            /// <param name="height">height of the image</param>
            /// <param name="proxyUrl">Backup url for the image</param>
            /// <returns></returns>
            public Embed AddThumbnail(string url, int? width = null, int? height = null, string proxyUrl = null)
            {
                Thumbnail = new Image(url, width, height, proxyUrl);
                return this;
            }
        }

        /// <summary>
        /// Field for and embed message
        /// </summary>
        private class Field
        {
            /// <summary>
            /// Name of the field
            /// </summary>
            [JsonProperty("name")]
            private string Name { get; }

            /// <summary>
            /// Value for the field
            /// </summary>
            [JsonProperty("value")]
            private string Value { get; }

            /// <summary>
            /// If the field should be in the same row or a new row
            /// </summary>
            [JsonProperty("inline")]
            private bool Inline { get; }

            public Field(string name, string value, bool inline)
            {
                Name = name;
                Value = value;
                Inline = inline;
            }
        }

        /// <summary>
        /// Image for an embed message
        /// </summary>
        private class Image
        {
            /// <summary>
            /// Url for the image
            /// </summary>
            [JsonProperty("url")]
            private string Url { get; }

            /// <summary>
            /// Width for the image
            /// </summary>
            [JsonProperty("width")]
            private int? Width { get; }

            /// <summary>
            /// Height for the image
            /// </summary>
            [JsonProperty("height")]
            private int? Height { get; }

            /// <summary>
            /// Proxy url for the image
            /// </summary>
            [JsonProperty("proxyURL")]
            private string ProxyUrl { get; }

            public Image(string url, int? width, int? height, string proxyUrl)
            {
                Url = url;
                Width = width;
                Height = height;
                ProxyUrl = proxyUrl;
            }
        }

        /// <summary>
        /// Author of an embed message
        /// </summary>
        private class AuthorInfo
        {
            /// <summary>
            /// Name of the author
            /// </summary>
            [JsonProperty("name")]
            private string Name { get; }

            /// <summary>
            /// Url to go to when clicking on the authors name
            /// </summary>
            [JsonProperty("url")]
            private string Url { get; }

            /// <summary>
            /// Icon url for the author
            /// </summary>
            [JsonProperty("icon_url")]
            private string IconUrl { get; }

            /// <summary>
            /// Proxy icon url for the author
            /// </summary>
            [JsonProperty("proxy_icon_url")]
            private string ProxyIconUrl { get; }

            public AuthorInfo(string name, string iconUrl, string url, string proxyIconUrl)
            {
                Name = name;
                Url = url;
                IconUrl = iconUrl;
                ProxyIconUrl = proxyIconUrl;
            }
        }

        /// <summary>
        /// Footer for an embed message
        /// </summary>
        private class Footer
        {
            /// <summary>
            /// Text for the footer
            /// </summary>
            [JsonProperty("text")]
            private string Text { get; }

            /// <summary>
            /// Icon url for the footer
            /// </summary>
            [JsonProperty("icon_url")]
            private string IconUrl { get; }

            /// <summary>
            /// Proxy icon url for the footer
            /// </summary>
            [JsonProperty("proxy_icon_url")]
            private string ProxyIconUrl { get; }

            public Footer(string text, string iconUrl, string proxyIconUrl)
            {
                Text = text;
                IconUrl = iconUrl;
                ProxyIconUrl = proxyIconUrl;
            }
        }

        #endregion

        #region Attachment Classes

        /// <summary>
        /// Enum for attachment content type
        /// </summary>
        private enum AttachmentContentType
        {
            Png,
            Jpg
        }

        private class Attachment
        {
            /// <summary>
            /// Attachment data
            /// </summary>
            public byte[] Data { get; }

            /// <summary>
            /// File name for the attachment.
            /// Used in the url field of an image
            /// </summary>
            public string Filename { get; }

            /// <summary>
            /// Content type for the attachment
            /// https://developer.mozilla.org/en-US/docs/Web/HTTP/Basics_of_HTTP/MIME_types
            /// </summary>
            public string ContentType { get; }

            public Attachment(byte[] data, string filename, AttachmentContentType contentType)
            {
                Data = data;
                Filename = filename;

                switch (contentType)
                {
                    case AttachmentContentType.Jpg:
                        ContentType = "image/jpeg";
                        break;

                    case AttachmentContentType.Png:
                        ContentType = "image/png";
                        break;
                }
            }

            public Attachment(byte[] data, string filename, string contentType)
            {
                Data = data;
                Filename = filename;
                ContentType = contentType;
            }
        }

        #endregion

        #region Config Classes

        private class DiscordMessageConfig
        {
            public string Content { get; set; }
            public EmbedConfig Embed { get; set; }
        }

        private class EmbedConfig
        {
            [JsonProperty("Title")] public string Title { get; set; }

            [JsonProperty("Description")] public string Description { get; set; }

            [JsonProperty("Embed Color")] public string Color { get; set; }

            [JsonProperty("Image Url")] public string Image { get; set; }

            [JsonProperty("Thumbnail Url")] public string Thumbnail { get; set; }

            [JsonProperty("Fields")] public List<FieldConfig> Fields { get; set; }

            [JsonProperty("Footer")] public FooterConfig Footer { get; set; }

            [JsonProperty("Enabled")] public bool Enabled { get; set; }
        }

        private class FieldConfig
        {
            [JsonProperty("Title")] public string Title { get; set; }

            [JsonProperty("Value")] public string Value { get; set; }

            [JsonProperty("Inline")] public bool Inline { get; set; }

            [JsonProperty("Order")] public int Order { get; set; }

            [JsonProperty("Enabled")] public bool Enabled { get; set; }
        }

        private class FooterConfig
        {
            [JsonProperty("Icon Url")] public string IconUrl { get; set; }

            [JsonProperty("Text")] public string Text { get; set; }

            [JsonProperty("Enabled")] public bool Enabled { get; set; }
        }

        #endregion

        #region Config Methods

        private DiscordMessage ParseMessage(DiscordMessageConfig config)
        {
            DiscordMessage message = new DiscordMessage();

            if (!string.IsNullOrEmpty(config.Content))
            {
                message.AddContent(config.Content);
            }

            if (config.Embed != null && config.Embed.Enabled)
            {
                Embed embed = new Embed();
                if (!string.IsNullOrEmpty(config.Embed.Title))
                {
                    embed.AddTitle(config.Embed.Title);
                }

                if (!string.IsNullOrEmpty(config.Embed.Description))
                {
                    embed.AddDescription(config.Embed.Description);
                }

                if (!string.IsNullOrEmpty(config.Embed.Color))
                {
                    embed.AddColor(config.Embed.Color);
                }

                if (!string.IsNullOrEmpty(config.Embed.Image))
                {
                    embed.AddImage(config.Embed.Image);
                }

                if (!string.IsNullOrEmpty(config.Embed.Thumbnail))
                {
                    embed.AddThumbnail(config.Embed.Thumbnail);
                }

                foreach (FieldConfig field in config.Embed.Fields.Where(f => f.Enabled).OrderBy(f => f.Order))
                {
                    string value = field.Value;
                    if (string.IsNullOrEmpty(value))
                    {
                        PrintWarning($"Field: {field.Title} was skipped because the value was null or empty.");
                        continue;
                    }

                    embed.AddField(field.Title, value, field.Inline);
                }

                if (config.Embed.Footer != null && config.Embed.Footer.Enabled)
                {
                    if (string.IsNullOrEmpty(config.Embed.Footer.Text) &&
                        string.IsNullOrEmpty(config.Embed.Footer.IconUrl))
                    {
                        AddPluginInfoFooter(embed);
                    }
                    else
                    {
                        embed.AddFooter(config.Embed.Footer.Text, config.Embed.Footer.IconUrl);
                    }
                }

                message.AddEmbed(embed);
            }

            return message;
        }

        #endregion

        #endregion
    }
}