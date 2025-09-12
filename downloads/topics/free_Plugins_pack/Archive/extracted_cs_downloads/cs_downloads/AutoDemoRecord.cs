using ConVar;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using WebSocketSharp;

namespace Oxide.Plugins
{
    /*
     * This Version Changes:
     * - Carbon fixes
     */
    [Info("Auto Demo Record", "Pho3niX90", "1.3.0")]
    [Description("Automatic recording based on conditions.")]
    internal class AutoDemoRecord : RustPlugin
    {
        const string PermissionRecord = "autodemorecord.record";
        private List<ARReport> _reports = new List<ARReport>();
        List<PendingUploads> _pendingUploads = new List<PendingUploads>();
        private Dictionary<ulong, Timer> _timers = new Dictionary<ulong, Timer>();
        private new Dictionary<ulong, BlacklistEntry> _blacklistPlayers = new Dictionary<ulong, BlacklistEntry>();
        private ARConfig config;
        int lastSavedCount = 0;
        int lastSavedCount_blacklist = 0;

        int _discordSize = 6000000;

        const string DISCORD_INTRO_URL = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
        [PluginReference] Plugin ServerArmour;

        bool HasPermission(BasePlayer player, string permissionName = PermissionRecord)
        {
            return permission.UserHasPermission(player.UserIDString, permissionName);
        }

        private void Loaded()
        {
            if (!permission.PermissionExists(PermissionRecord, this))
                permission.RegisterPermission(PermissionRecord, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Recording Started"] = "Recording started for player {0}, eta is {1} mins",
                ["Recording Ended"] = "Recording finished for player {0}, player was recorded for {1} mins",
                ["Your Recording Started"] = "Your recording has started, and will auto stop in {0} mins",
                ["Your Recording Ended"] = "Your recording has ended."
            }, this);
            LoadData();
            foreach (var player in BasePlayer.activePlayerList)
            {
                CheckUploads(player);
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            CheckUploads(player);


            if (_blacklistPlayers.ContainsKey(player.userID))
            {
                _recordOnInterval(player.userID, _blacklistPlayers[player.userID]);
            }
            else if (config.AR_Fyhack_Seconds > 0 && player.lastViolationType.Equals(AntiHackType.FlyHack))
            {
                float secondsAgo = (UnityEngine.Time.realtimeSinceStartup - player.lastViolationTime);
                if (secondsAgo <= config.AR_Fyhack_Seconds)
                {
                    API_StartRecordingWithWebhook(player, $"Flyhacked {secondsAgo:0.000} seconds ago", null);
                }
            }
        }


        private void OnPlayerDisconnected(BasePlayer player)
        {
            StopRecording(player);
            if (_timers.ContainsKey(player.userID) && !_timers[player.userID].Destroyed)
            {
                _timers[player.userID].Destroy();
            }
        }

        void OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            if (!command.ToLower().Equals("report") || args.Length < 2) return;

            var target = BasePlayer.Find(args[0]);
            if (target == null) return;

            List<string> reason = args.Skip(1).ToList();

            var report = new ARReport(player.UserIDString, player.displayName, target.UserIDString, target.displayName,
                "Report", string.Join(" ", reason), "Report");
            _reports.Add(report);
            ProcessF7(target.userID, report);
        }

        private void Unload()
        {
            SaveData();

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.Connection.IsRecording)
                    StopRecording(player);
            }

            _pendingUploads.Clear();


            foreach (Timer timer in _timers.Values)
            {
                timer.Destroy();
            }

            _timers.Clear();
            _reports.Clear();
        }

        string GetMsg(string key) => lang.GetMessage(key, this);

        [ChatCommand("testrec")]
        private void cmdTestRec(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin || player.UserIDString.Equals("76561198007433923"))
            {
                var report = new ARReport(player.UserIDString, player.displayName, player.UserIDString,
                    player.displayName, "Test Subejct", "Test description/message", "Test type");
                _reports.Add(report);
                ProcessF7(player.userID, report);
            }
        }

        [ChatCommand("record")]
        private void cmdRecord(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player))
            {
                SendReply(player, "You do not have rights to use this command.");
                return;
            }

            SendReply(player, string.Format(GetMsg("Your Recording Started"), config.AR_Report_Length_Self));
            API_StartRecording4(player, $"Player {player.displayName} initiated self recording.",
                config.AR_Report_Length_Self, config.AR_Discord_Self_Webhook);
        }

        [ChatCommand("record.stop")]
        private void cmdRecordStop(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player))
            {
                SendReply(player, "You do not have rights to use this command.");
                return;
            }

            SendReply(player, string.Format(GetMsg("Your Recording Ended"), config.AR_Report_Length_Self));
            StopRecording(player, config.AR_Discord_Webhook);
        }

        [ConsoleCommand("autodemorecord.record")]
        void cmdRecordConsole(ConsoleSystem.Arg arg)
        {
            ulong playerId;
            int minutes;
            if (ulong.TryParse(arg.Args[0], out playerId) && int.TryParse(arg.Args[1], out minutes))
            {
                BasePlayer player = BasePlayer.FindByID(playerId);
                API_StartRecording4(player, $"Recording started of {player.displayName}.", minutes,
                    config.AR_Discord_Webhook);
            }
            else
            {
                arg.ReplyWith("Syntax: autodemorecord.record steam64id minutes");
            }
        }

        void _recordOnInterval(ulong playerId, BlacklistEntry entry)
        {
            BasePlayer player = BasePlayer.FindByID(playerId);
            if (player != null && player.IsConnected)
                API_StartRecordingWithLength(player, $"Recording started of {player.displayName}.",
                    entry.recordingMinutes);
        }

        [ConsoleCommand("autodemorecord.blacklist")]
        void cmdRecordBlacklist(ConsoleSystem.Arg arg)
        {
            string action = "";
            ulong playerId = 0;
            int minutes = 0;
            int interval = 0;
            action = arg.Args.Length > 0 ? arg.Args[0].ToLower() : "";

            switch (action)
            {
                case "add":
                    if (arg.Args.Length == 4 && ulong.TryParse(arg.Args[1], out playerId) &&
                        int.TryParse(arg.Args[2], out minutes) && int.TryParse(arg.Args[3], out interval))
                    {
                        if (minutes == 0 && interval == 0)
                        {
                            goto default;
                        }

                        if (_blacklistPlayers.ContainsKey(playerId))
                        {
                            arg.ReplyWith("Player already blacklisted, remove first.");
                        }
                        else
                        {
                            var entry = new BlacklistEntry(minutes, interval);
                            if (_blacklistPlayers.TryAdd(playerId, entry))
                            {
                                _recordOnInterval(playerId, entry);
                                SaveData();
                            }
                        }
                    }
                    else
                    {
                        goto default;
                    }

                    break;
                case "remove":
                    if (arg.Args.Length > 0)
                    {
                        if (_blacklistPlayers.ContainsKey(playerId))
                        {
                            if (_blacklistPlayers.Remove(playerId))
                            {
                                arg.ReplyWith("Player removed from blacklist");
                            }
                        }
                        else
                        {
                            arg.ReplyWith("Player not blacklisted");
                        }
                    }
                    else
                    {
                        goto default;
                    }

                    break;
                default:
                    arg.ReplyWith(
                        "Syntax: autodemorecord.blacklist add [steam64id] [recordingMinutes] [intervalMinutes]\nautodemorecord.blacklist remove [steam64id]");
                    break;
            }
        }

        private void API_Report(string reporterId, string reporterName, string targetId, string targetName,
            string reason) => ProcessF7(ulong.Parse(targetId),
            new ARReport(reporterId, reporterName, targetId, targetName, reason));

        void OnPlayerReported(BasePlayer reporter, string targetName, string targetId, string subject, string message,
            string type)
        {
            ulong targetIdLong = 0;
            if (ulong.TryParse(targetId, out targetIdLong))
            {
                var report = new ARReport(reporter.UserIDString, reporter.displayName, targetId, targetName, subject,
                    message, type);
                _reports.Add(report);
                ProcessF7(targetIdLong, report);
            }
        }

        void ProcessF7(ulong targetId, ARReport report = null)
        {
            BasePlayer accused = BasePlayer.FindByID(targetId);
            if (accused == null) return;

            if (accused.IsConnected)
            {
                // record player only if he has reaced the amount in the config. And only when there is no recording active. 
                if (CheckReports(accused) >= config.AR_Report)
                {
                    if (!accused.Connection.IsRecording)
                    {
                        StartRecording(accused, report);
                    }
                    else
                    {
                        //TODO extend recording period.
                    }
                }
                else if (config.AR_SendAllReports)
                {
                    NotifyDiscord(accused, null, true);
                }
            }
        }

        private void API_StartRecording(BasePlayer player, string reason) => StartRecording(player, null, reason);

        /*
        private void API_StartRecording(BasePlayer player, string reason, int length) =>
            StartRecording(player, null, reason, null, length);

        private void API_StartRecording(BasePlayer player, string reason, string webhook) =>
            StartRecording(player, null, reason, webhook);
        */

        private void API_StartRecording(BasePlayer player, string reason, int length, string webhook) =>
            StartRecording(player, null, reason, webhook, length);

        // broken method overloading fixes
        private void API_StartRecordingWithLength(BasePlayer player, string reason, int length) =>
            StartRecording(player, null, reason, null, length);

        private void API_StartRecordingWithWebhook(BasePlayer player, string reason, string webhook) =>
            StartRecording(player, null, reason, webhook);

        private void API_StartRecording4(BasePlayer player, string reason, int length, string webhook) =>
            StartRecording(player, null, reason, webhook, length);

        Demo.Header GetHeader(Connection conn)
        {
#if CARBON
            return conn.recordHeader as Demo.Header;
#else
            return typeof(Connection)
                .GetField("recordHeader", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(conn) as Demo.Header;
#endif
        }
        void NullifyHeader(BasePlayer player)
        {
#if CARBON
            player.Connection.recordHeader = null;
#else
            typeof(Connection).GetField("recordHeader", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(player.Connection, null);
#endif
        }
        void StartRecording(BasePlayer player, ARReport report = null, string reason = null, string webhook = null,
            int length = 0)
        {
            Puts($"API_StartRecording called with reason: {reason}");
            if (player == null)
            {
                Puts("Plugin sent a null player object. Cannot record.");
                return;
            }

            if (player.Connection.IsRecording)
            {
                Puts($"Already recording for {player.displayName}");
                return;
            }

            if (length == 0)
            {
                Puts($"Recording length passed is 0, so defaulting to {config.AR_Report_Length}");
                length = config.AR_Report_Length;
            }

            var msg = string.Format(GetMsg("Recording Started"), player.UserIDString, length);

            if (report != null)
            {
                AddReport(player.userID, report);
            }

            if (!reason.IsNullOrEmpty())
            {
                AddReason(player.userID, reason);
            }
            else
            {
                AddReason(player.userID, "N/A");
            }

            if (!webhook.IsNullOrEmpty() && webhook.Equals(config.AR_Discord_Self_Webhook))
            {
                IsSelfReport(player.userID);
            }

            if (config.AR_Discord_Notify_RecordStart || config.AR_SendAllReports)
                NotifyDiscord(player, msg, true, webhook);

            if (config.AR_Upload_CombatLog)
                GenCombatLog(player, string.Format("{0} {1:yyyy-MM-dd-hhmmss}.txt", player.UserIDString, DateTime.Now));

            player.StartDemoRecording();

            int recordingTime = length * 60;
            if (length > 0)
            {
                _timers[player.userID] = timer.Repeat(10, (recordingTime / 10) + 1,
                    () => MonitorSplit(player, _timers[player.userID], webhook, length));
            }
        }

        void StopRecording(BasePlayer player, string wh = "")
        {
            if (player.Connection.IsRecording)
            {
                PendingUploads pup = GetUploads(player.userID);

                if (pup != null && pup.isSelfRecord)
                {
                    SendReply(player, GetMsg("Your Recording Ended"));
                }

                if (config.AR_Upload || !config.AR_SaveRecording)
                    AddFile(player.Connection);

                if (!config.AR_SaveRecording)
                {
                    if (!wh.IsNullOrEmpty() || !GetWebhook(pup.isSelfRecord).IsNullOrEmpty())
                    {
                        NullifyHeader(player);
                    }
                    else
                    {
                        PrintWarning("Webhooks not setup. Won't upload.");
                    }
                }

                player.StopDemoRecording();
                // upload
                if (pup != null)
                {
                    UploadFiles(pup, player, wh);

                    if (config.AR_Clear_Counter)
                    {
                        _reports.RemoveAll(x => x.targetId == player.UserIDString);
                    }
                }


                _timers.FirstOrDefault(x => x.Key == player.userID).Value?.Destroy();
                CheckUploads(player);
            }

            if (_blacklistPlayers.ContainsKey(player.userID))
            {
                var entry = _blacklistPlayers[player.userID];
                Puts(
                    $"{player.displayName} is blacklisted, will start another recording again in {entry.intervalMinutes}m for {entry.recordingMinutes}m");
                try
                {
                    timer.Once(entry.intervalMinutes * 60, () => _recordOnInterval(player.userID, entry));
                }
                catch (Exception e)
                {
                    if (player && player.IsConnected)
                    {
                        _recordOnInterval(player.userID, entry);
                    }
                    else
                    {
                        Puts($"Blacklisted Player {player.displayName} went offline.");
                    }
                }
            }
        }

        void SplitRecording(BasePlayer player, string wh)
        {
            if (player == null || !player.Connection.IsRecording) return;
            AddFile(player.Connection);

            if (!config.AR_SaveRecording)
            {
                NullifyHeader(player);
            }

            player?.StopDemoRecording();
            UploadFiles(GetUploads(player.userID), player, wh);
            player?.StartDemoRecording();
        }

        void MonitorSplit(BasePlayer player, Timer timer, string wh, int length = 0)
        {
            Connection conn = player.Connection;
            int recordLength = length > 0 ? length * 60 : config.AR_Report_Length * 60;

            if (conn != null && conn.IsRecording)
            {
                if ((recordLength <= Math.Round(GetTotalSecondsRecorded(conn))) || timer.Destroyed || !player.IsConnected)
                {
                    StopRecording(player, wh);
                    timer.Destroy();
                    _timers.Remove(player.userID);
                }
                else if (config.AR_Discord_Split && conn.RecordFilesize >= _discordSize)
                {
                    //Puts($"conn.RecordFilesize {conn.RecordFilesize}");
                    SplitRecording(player, wh);
                }
            }
            else
            {
                timer.Destroy();
            }
        }

        // Temporary reflection until fields are exposed.
        byte[] GetDemoFileBytes(Connection conn)
        {
            if (conn == null)
            {
                PrintWarning($"connection is null");
                return null;
            }

            if (!conn.IsRecording)
            {
                PrintWarning($"no recording active");
                return null;
            }

            // Temporary reflection until fields are exposed.
#if CARBON
            var recordStream = conn.recordStream;
#else
            var recordStream = typeof(Connection)
                .GetField("recordStream", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(conn);
#endif

            if (recordStream == null)
            {
                PrintWarning($"record stream empty");
                return null;
            }
            
            // Temporary reflection until fields are exposed.
            var recordHeader = GetHeader(conn);
            if (recordHeader != null)
            {
                // Manually converting data to a byte array since System.IO is unavailable to us.
                var demoRecordHeader = recordHeader;
                demoRecordHeader.length = (long)conn.RecordTimeElapsed.TotalMilliseconds;


                var protoBytes = demoRecordHeader.ToProtoBytes();
                List<byte> bytes = new List<byte>() { 0x10 };
                var encoding = new UTF8Encoding(false, true);
                bytes.AddRange(encoding.GetBytes("RUST DEMO FORMAT"));
                bytes.AddRange(GetIntBytes(protoBytes.Length));
                bytes.AddRange(protoBytes);
                bytes.AddRange(encoding.GetBytes(new char[] { '\0' }));

                bytes.AddRange((byte[])recordStream.GetType()
                    .GetMethod("ToArray", BindingFlags.Instance | BindingFlags.Public)
                    .Invoke(recordStream, Array.Empty<object>()));
                //UploadFile(fileName, bytes.ToArray());
                return bytes.ToArray();
            }
            else
            {
                PrintWarning($"record header empty");
                return null;
            }
        }

        byte[] GetIntBytes(int value)
        {
            var buffer = new byte[4];
            buffer[0] = (byte)value;
            buffer[1] = (byte)(value >> 8);
            buffer[2] = (byte)(value >> 16);
            buffer[3] = (byte)(value >> 24);
            return buffer;
        }

        int CheckReports(BasePlayer player) => config.AR_Report_Seconds > 0
            ? this._reports.Count(x =>
                secondsAgo(x.created) <= config.AR_Report_Seconds && x.targetId == player.UserIDString)
            : this._reports.Count(x => x.targetId == player.UserIDString);

        int secondsAgo(DateTime timeFrom)
        {
            DateTime now = DateTime.UtcNow;
            return (int)Math.Round((now - timeFrom).TotalSeconds);
        }

        DiscordMessageConfig GenerateFieldContent(BasePlayer player, ARReport report, string msgRec,
            string avatar = "")
        {
            List<FieldConfig> content = new List<FieldConfig>();

            if (report != null)
            {
                string targetUserLink = $"{report.reporterName}\n{report.reporterId}\n[Steam](https://steamcommunity.com/profiles/{player.UserIDString}) - [SA](https://io.serverarmour.com/profile/{player.UserIDString}) - [BM](https://www.battlemetrics.com/rcon/players?filter%5Bsearch%5D={player.UserIDString})";

                content.Add(new FieldConfig()
                {
                    Title = "Reporter",
                    Value = targetUserLink,
                    Inline = true,
                    Enabled = true,
                    Order = 3
                });

                content.Add(new FieldConfig()
                {
                    Title = "Report Subject",
                    Value = $"{report.type}: {report.subject}",
                    Inline = false,
                    Enabled = true,
                    Order = 4
                });
                content.Add(new FieldConfig()
                {
                    Title = "Report Message",
                    Value = report.message,
                    Inline = true,
                    Enabled = true,
                    Order = 5
                });
            }
            else
            {
                content.Add(new FieldConfig()
                {
                    Title = "Reason",
                    Value = string.Empty,
                    Inline = false,
                    Enabled = true,
                    Order = 6
                });
            }


            string userLink = $"[{player?.displayName}\n{player.UserIDString}](https://io.serverarmour.com/profile/{player.UserIDString})";

            content.Add(new FieldConfig()
            {
                Title = "Player",
                Value = userLink,
                Inline = true,
                Enabled = true,
                Order = 2
            });


            content.Add(new FieldConfig()
            {
                Title = covalence.Server.Name,
                Value =
                    $"[steam://connect/{covalence.Server.Address.MapToIPv4()}:{covalence.Server.Port}](steam://connect/{covalence.Server.Address}:{covalence.Server.Port})",
                Inline = false,
                Enabled = true,
                Order = 7
            });


            var msg = new DiscordMessageConfig
            {
                Content = string.Empty,
                Embed = new EmbedConfig
                {
                    Title = config.AR_Discord_WebhookTitle,
                    Description = msgRec,
                    Color = config.AR_Discord_Color,
                    Image = string.Empty,
                    Thumbnail = avatar, // "https://assets.umod.org/images/icons/user/5e6f8629518b5.jpg",
                    Fields = content,
                    Footer = new FooterConfig
                    {
                        IconUrl = "https://assets.umod.org/images/icons/user/5e6f8629518b5.jpg",
                        Text = $"{Title} V{Version} by {Author}",
                        Enabled = true
                    },
                    Enabled = true
                }
            };


            return msg;
        }

        void NotifyDiscord(BasePlayer player, string msgRec, bool isStart = false, string webhook = null)
        {
            string webHook = !webhook.IsNullOrEmpty() && !webhook.Equals(DISCORD_INTRO_URL)
                ? webhook
                : config.AR_Discord_Webhook;
            if (!webHook.IsNullOrEmpty() && !config.Equals(DISCORD_INTRO_URL)
                                         && (config.AR_Discord_Notify_RecordStart ||
                                             config.AR_Discord_Notify_RecordStop))
            {
                PendingUploads pup = GetUploads(player.userID);
                if (pup == null) return;
                DiscordMessageConfig msg = null;

                if ((isStart && config.AR_Discord_Notify_RecordStartMsg) ||
                    (!isStart && config.AR_Discord_Notify_RecordStopMsg) || config.AR_SendAllReports)
                {
                    msg = GenerateFieldContent(player, pup.report, msgRec);
                }
                else
                {
                    msg = GenerateFieldContent(player, null, msgRec);
                }

                try
                {
                    webrequest.Enqueue($"https://steamcommunity.com/profiles/{player.userID}?xml=1", string.Empty,
                        (code, result) =>
                        {
                            if (code >= 200 && code <= 204)
                            {
                                msg.Embed.Thumbnail = new Regex(
                                    @"(?<=<avatarMedium>[\w\W]+)https://.+\.jpg(?=[\w\W]+<\/avatarMedium>)",
                                    RegexOptions.Compiled).Match(result).Value;

                                DiscordMessage message = ParseMessage(msg);
                                SendDiscordMessage(webHook, message);
                            }
                            else
                            {
                                throw new Exception();
                            }
                        }, this);
                }
                catch (Exception e)
                {
                    if (!webhook.IsNullOrEmpty())
                    {
                        DiscordMessage message = ParseMessage(msg);
                        SendDiscordMessage(webHook, message);
                    }
                    else
                    {
                        PrintWarning("Webhooks not setup. Won't upload.");
                    }
                }
            }
        }

        #region Configuration

        string GetWebhook(bool isSelfStart) =>
            isSelfStart && !config.AR_Discord_Self_Webhook.Replace(DISCORD_INTRO_URL, "").IsNullOrEmpty()
                ? config.AR_Discord_Self_Webhook
                : config.AR_Discord_Webhook.Replace(DISCORD_INTRO_URL, "");

        private class ARConfig
        {
            // Config default vars
            public int AR_Report = 2;
            public int AR_Report_Length = 5;
            public int AR_Report_Length_Self = 10;
            public bool AR_Clear_Counter = false;
            public string AR_Discord_Webhook = DISCORD_INTRO_URL;
            public string AR_Discord_Self_Webhook = DISCORD_INTRO_URL;
            public string AR_Discord_Color = "#de8732";
            public bool AR_Discord_Notify_RecordStart = false;
            public bool AR_Discord_Notify_RecordStop = false;
            public int AR_Report_Seconds = 0;
            public bool AR_Discord_Notify_RecordStartMsg = true;
            public bool AR_Discord_Notify_RecordStopMsg = false;
            public bool AR_Save_Reports = true;
            public bool AR_Upload = true;
            public bool AR_Upload_CombatLog = true;
            public bool AR_Discord_Split = true;
            public bool AR_SaveRecording = false;
            public bool AR_SendAllReports = false;
            public bool AR_Discord_SingleUpload = false;
            public int AR_Fyhack_Seconds = 600;
            public string AR_Discord_WebhookTitle = "Auto Demo Recorder";

            // Plugin reference
            private AutoDemoRecord plugin;

            public ARConfig(AutoDemoRecord plugin)
            {
                this.plugin = plugin;
                /**
                 * Load all saved config values
                 * */
                GetConfig(ref AR_Report, "Auto record after X reports");
                GetConfig(ref AR_Report_Length, "Auto record for X minutes");
                GetConfig(ref AR_Report_Length_Self, "Auto record for X minutes (Self record)");
                GetConfig(ref AR_Clear_Counter, "Clear report counter after recording?");
                GetConfig(ref AR_Discord_WebhookTitle, "Discord Webhook Title");
                GetConfig(ref AR_Discord_Webhook, "Discord Webhook");
                GetConfig(ref AR_Discord_Self_Webhook, "Discord Webhook - Self Record");
                GetConfig(ref AR_Discord_Color, "Discord MSG Color");
                GetConfig(ref AR_Discord_Notify_RecordStart, "Discord: Notify if recording is started");
                GetConfig(ref AR_Discord_Notify_RecordStartMsg, "Discord: Include report with start message?");
                GetConfig(ref AR_Discord_Notify_RecordStop, "Discord: Notify if recording is stopped");
                GetConfig(ref AR_Discord_Notify_RecordStopMsg, "Discord: Include report with end message?");

                GetConfig(ref AR_Discord_SingleUpload, "Discord: Single upload msg?");


                GetConfig(ref AR_Report_Seconds, "Only record when reports within X seconds");
                GetConfig(ref AR_Save_Reports, "Save/Load reports to datafile on reload");
                GetConfig(ref AR_Upload, "Upload DEMO file to discord webhook?");
                GetConfig(ref AR_SaveRecording, "Save recording to server?");
                GetConfig(ref AR_Upload_CombatLog, "Upload Combat Log to discord webhook?");
                GetConfig(ref AR_Discord_Split, "Split DEMO files for non nitro discord (8mb chunks)?");
                GetConfig(ref AR_SendAllReports, "Send all reports/F7 to discord?");
                GetConfig(ref AR_Fyhack_Seconds, "Record if FlyHacked in last X seconds");

                plugin.SaveConfig();
            }

            private void GetConfig<T>(ref T variable, params string[] path)
            {
                if (path.Length == 0) return;
                if (plugin.Config.Get(path) == null)
                {
                    SetConfig(ref variable, path);
                    plugin.PrintWarning($"Added new field to config: {string.Join("/", path)}");
                }

                variable = (T)Convert.ChangeType(plugin.Config.Get(path), typeof(T));
            }

            private void SetConfig<T>(ref T variable, params string[] path) =>
                plugin.Config.Set(path.Concat(new object[] { variable }).ToArray());
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = new ARConfig(this);
        }

        void LoadData()
        {
            try
            {
                _reports = Interface.Oxide.DataFileSystem.ReadObject<List<ARReport>>(this.Name);
                lastSavedCount = _reports.Count();
            }
            catch (Exception e)
            {
                Puts(e.Message);
            }

            try
            {
                _blacklistPlayers =
                    Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, BlacklistEntry>>(
                        $"{this.Name}_blacklist");
                lastSavedCount_blacklist = _blacklistPlayers.Count();
            }
            catch (Exception e)
            {
                Puts(e.Message);
            }
        }

        void SaveData()
        {
            int records = _reports.Count();
            int recordsDiff = records - lastSavedCount;
            lastSavedCount = records;

            if (config.AR_Save_Reports && recordsDiff != 0)
            {
                try
                {
                    Interface.Oxide.DataFileSystem.WriteObject(this.Name, _reports, true);
                }
                catch (Exception e)
                {
                    Puts(e.Message);
                }
            }

            records = _blacklistPlayers.Count();
            recordsDiff = records - lastSavedCount_blacklist;
            lastSavedCount_blacklist = records;
            if (recordsDiff == 0) return;
            try
            {
                Interface.Oxide.DataFileSystem.WriteObject($"{this.Name}_blacklist", _blacklistPlayers, true);
            }
            catch (Exception e)
            {
                Puts(e.Message);
            }
        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new configuration file.");

        #endregion

        #region Classes

        public class PendingUploads
        {
            public ulong userID;
            public ARReport report;
            public string reason;
            public List<PFile> files;
            public bool shouldUpload;
            public bool isSelfRecord;

            public PendingUploads(BasePlayer player)
            {
                this.userID = player.userID;
                this.files = new List<PFile>();
                this.reason = null;
                this.report = null;
                this.shouldUpload = false;
            }

            public PendingUploads AddReport(ARReport report)
            {
                this.report = report;
                return this;
            }

            public PendingUploads AddReason(string reason)
            {
                this.reason = reason;
                return this;
            }

            public PendingUploads AddFile(PFile file)
            {
                this.files.Add(file);
                return this;
            }

            public double GetTotalSeconds()
            {
                return this.files.Sum(x => x.totalSeconds);
            }
        }

        public class PFile
        {
            public string fileName;
            public byte[] fileBytes;
            public double totalSeconds;

            public PFile(string filename, byte[] bytes)
            {
                this.fileName = filename;
                this.fileBytes = bytes;
                this.totalSeconds = 0;
            }

            public PFile(string filename, byte[] bytes, double totalSeconds)
            {
                this.fileName = filename;
                this.fileBytes = bytes;
                this.totalSeconds = totalSeconds;
            }
        }

        public class ARReport
        {
            public string reporterName;
            public string reporterId;
            public string targetName;
            public string targetId;
            public string subject;
            public string message;
            public string type;
            public DateTime created;

            public ARReport()
            {
            }

            public ARReport(string reporterId, string reporterName, string targetId, string targetName, string subject,
                string message, string type)
            {
                this.reporterId = reporterId;
                this.reporterName = reporterName;
                this.targetId = targetId;
                this.targetName = targetName;
                this.subject = subject;
                this.message = message;
                this.type = type;
                this.created = DateTime.UtcNow;
            }

            public ARReport(string targetId, string targetName)
            {
                this.targetName = targetName;
                this.targetId = targetId;
            }

            public ARReport(string reporterId, string reporterName, string targetId, string targetName, string reason)
            {
                this.reporterId = reporterId;
                this.reporterName = reporterName;
                this.targetName = targetName;
                this.targetId = targetId;
                this.message = reason;
            }
        }

        public class BlacklistEntry
        {
            public BlacklistEntry()
            {
            }

            public BlacklistEntry(int rMin, int iMin)
            {
                this.recordingMinutes = rMin;
                this.intervalMinutes = iMin;
            }

            public int recordingMinutes;
            public int intervalMinutes;
        }

        #endregion

        #region Combat

        private void GenCombatLog(BasePlayer target, string filename, int logSize = 30)
        {
            var cLog = CombatLog.Get(target.userID);

            TextTable textTable = new TextTable();
            textTable.AddColumns(new[]
                {"time", "attacker", "id", "target", "id", "weapon", "ammo", "area", "distance", "old_hp", "new_hp"});

            int num = cLog.Count - logSize;
            int delay = ConVar.Server.combatlogdelay;
            timer.Once(delay, () =>
            {
                foreach (CombatLog.Event evt in cLog)
                {
                    string time = (UnityEngine.Time.realtimeSinceStartup - evt.time).ToString("0s");

                    bool isPlayer = evt.attacker == "you";
                    string attackerName = isPlayer ? target.displayName : UintFind(evt.attacker_id);

                    string attacker_id = isPlayer ? target.UserIDString : evt.attacker_id.ToString();

                    isPlayer = evt.target == "you";

                    string targetName = isPlayer ? target.displayName : UintFind(evt.target_id);

                    string target_id = isPlayer ? target.UserIDString : evt.target_id.ToString();

                    textTable.AddRow(new string[]
                    {
                        time, attackerName, attacker_id, targetName, target_id, evt.weapon, evt.ammo,
                        HitAreaUtil.Format(evt.area).ToLower(), evt.distance.ToString("0.0m"),
                        evt.health_old.ToString("0.0"), evt.health_new.ToString("0.0")
                    });
                }

                if (config.AR_Upload_CombatLog && !filename.IsNullOrEmpty())
                {
                    if (config.AR_SaveRecording)
                    {
                        SaveCombatLog(filename, textTable);
                    }

                    try
                    {
                        AddFile(target.userID, filename, Encoding.ASCII.GetBytes(textTable.ToString()));
                        //AddFile(target.userID, filename, textTable.ToString().Select(x =>Convert.ToByte(x)).ToArray());
                    }
                    catch (OverflowException e)
                    {
                        Puts($"Cannot upload combatlog due to an error: size={textTable.ToString().Length}, msg={e.Message}");
                    }
                }
            });
        }

        private void SaveCombatLog(string filename, TextTable data) =>
            LogToFile($"{filename}_combatlog", data.ToString(), this, false);

        private string UintFind(ulong id)
        {
            BasePlayer player = null;
            try
            {
                player = BasePlayer.activePlayerList.First(x => x.net.ID.Equals(id));
            }
            catch (Exception e)
            {
            }

            return player != null ? player.displayName : id.ToString();
        }

        #endregion

        #region Data Handling

        PendingUploads CheckUploads(BasePlayer player)
        {
            _pendingUploads.RemoveAll(x => x.userID == player.userID);
            var pup = new PendingUploads(player);
            _pendingUploads.Add(pup);
            return pup;
        }

        public double GetTotalSecondsRecorded(Connection conn)
        {
            return _pendingUploads.Find(x => x.userID == conn.userid).GetTotalSeconds() + conn.RecordTimeElapsed.TotalSeconds;
        }

        PendingUploads GetUploads(ulong userid)
        {
            return _pendingUploads.Find(x => x.userID == userid);
        }

        private void AddFile(ulong userid, string filename, byte[] bytes)
        {
            _pendingUploads.Find(x => x.userID == userid).AddFile(new PFile(filename, bytes));
        }

        private void AddFile(Connection conn)
        {
            var fileBytes = GetDemoFileBytes(conn);
            if (fileBytes != null)
            {
                _pendingUploads
                    .Find(x => x.userID == conn.userid)
                    ?.AddFile(new PFile($"{conn.userid} {conn.RecordFilename.Split('/').Last()}", fileBytes,
                        conn.RecordTimeElapsed.TotalSeconds));
            }
        }

        private void AddReport(ulong userid, ARReport report)
        {
            _pendingUploads.Find(x => x.userID == userid).AddReport(report);
        }

        private void IsSelfReport(ulong userid)
        {
            _pendingUploads.Find(x => x.userID == userid).isSelfRecord = true;
        }

        private void AddReason(ulong userid, string reason)
        {
            _pendingUploads.Find(x => x.userID == userid).AddReason(reason).isSelfRecord = false;
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
                if (code == 413)
                {
                    if (!this.config.AR_Discord_Split)
                    {
                        this.config.AR_Discord_Split = true;
                        PrintWarning($"Upload failed due too split not enabled for a non boosted server. Reverting settting");
                        SaveConfig();
                    }
                    else
                    {
                        PrintWarning($"{_discordSize} bytes too large for your server, decreasing by 50K");
                        _discordSize -= 50000;
                    }
                }
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
            Jpg,
            Octet
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
                    case AttachmentContentType.Octet:
                        ContentType = "application/octet-stream";
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

        #region Discord Uploading

        private List<string> uploaded = new List<string>();

        private void UploadFiles(PendingUploads pup, BasePlayer player, string wh = "")
        {
            var webhook = wh != null ? wh : GetWebhook(pup.isSelfRecord);

            List<Attachment> attach = new List<Attachment>();
            var totalBytesCurrent = 0;
            var seconds = 0d;
            bool clear = false;

            for (int i = 0, n = pup.files.Count(); i < n; i++)
            {
                if (clear)
                {
                    clear = false;
                    seconds = 0;
                }

                var file = pup.files[i];
                int nextIndex = i + 1;
                var nextTotalBytes = 0;

                attach.Add(new Attachment(file.fileBytes, file.fileName, AttachmentContentType.Octet));
                nextTotalBytes = totalBytesCurrent += file.fileBytes.Length;

                if (nextIndex < n - 1)
                {
                    nextTotalBytes += pup.files[nextIndex].fileBytes.Length;
                }

                seconds += file.totalSeconds;
                // Puts($"config.AR_Discord_Split {config.AR_Discord_Split}\nnextTotalBytes {nextTotalBytes}\n_discordSize {_discordSize}\nnextIndex {nextIndex}");
                if ((config.AR_Discord_Split && nextTotalBytes >= _discordSize) || nextIndex >= n)
                {
                    uploaded.AddRange(attach.Select(x => x.Filename));
                    // Puts($"MULTI MSG {nextIndex}  {n}");
                    DiscordMessageConfig dmc = GenerateFieldContent(player, pup.report,
                        string.Format(GetMsg("Recording Ended"), player.userID, Math.Round(seconds / 60)));

                    try
                    {
                        webrequest.Enqueue($"https://steamcommunity.com/profiles/{player.userID}?xml=1", string.Empty,
                            (code, result) =>
                            {
                                if (code >= 200 && code <= 204)
                                {
                                    dmc.Embed.Thumbnail = new Regex(
                                        @"(?<=<avatarMedium>[\w\W]+)https://.+\.jpg(?=[\w\W]+<\/avatarMedium>)",
                                        RegexOptions.Compiled).Match(result).Value;

                                    if (!webhook.IsNullOrEmpty())
                                    {
                                        SendDiscordAttachmentMessage(webhook, ParseMessage(dmc), attach);
                                        foreach (var file1 in uploaded)
                                        {
                                            attach.RemoveAll(x => x.Filename == file1);
                                        }

                                        uploaded.Clear();
                                    }
                                    else
                                    {
                                        PrintWarning("Webhooks not setup. Won't upload.");
                                    }
                                }
                                else
                                {
                                    throw new Exception();
                                }
                            }, this);
                    }
                    catch (Exception e)
                    {
                        if (!webhook.IsNullOrEmpty())
                        {
                            uploaded.AddRange(attach.Select(x => x.Filename));
                            SendDiscordAttachmentMessage(webhook, ParseMessage(dmc), attach);
                            foreach (var file2 in uploaded)
                            {
                                attach.RemoveAll(x => x.Filename == file2);
                            }

                            uploaded.Clear();
                        }
                        else
                        {
                            PrintWarning("Webhooks not setup. Won't upload.");
                        }
                    }

                    clear = true;
                }
            }

            _pendingUploads.Find(x => x.userID == pup.userID)?.files.Clear();
        }

        #endregion
    }
}