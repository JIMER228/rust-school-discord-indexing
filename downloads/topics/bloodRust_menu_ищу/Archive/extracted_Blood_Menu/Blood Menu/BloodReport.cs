using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BloodReport", "[LimePlugin] Chibubrik", "1.0.0")]
    public class BloodReport : RustPlugin
    {
        #region Reference

        [PluginReference] private Plugin ImageLibrary, NoEscape, IQFakeActive;

        #endregion

        #region Vars

        private static BloodReport _ins;

        public Dictionary<ulong, PlayerSaveCheckClass> PlayerSaveCheck = new Dictionary<ulong, PlayerSaveCheckClass>();
        public class PlayerSaveCheckClass
        {
            public string Discord;
            public string NickName;

            public ulong ModeratorID;

            public DateTime CheckStart = DateTime.Now;
        }

        public Dictionary<ulong, bool> OpenedModeratorMenu = new Dictionary<ulong, bool>();
        public List<ReportList> LastReport = new List<ReportList>();
        public Dictionary<ulong, PlayerInfo> ReportInformation = new Dictionary<ulong, PlayerInfo>();

        #endregion

        #region Config
        private static Configuration _config;
        public class Configuration
        {
            [JsonProperty("[Discord] Вебхук для новых репортов")] public string DiscordWebHook;
            [JsonProperty("Пермишн модератора")] public string ModeratorPermission;
            [JsonProperty("Кд отправки репортов")] public double Cooldown;
            [JsonProperty("Количество репортов для отправки уведомления и отображения в панели")] public int AlertCount;
            [JsonProperty("Кастомные имена для оружия")] public Dictionary<string, string> CustomNames;
            [JsonProperty("Список жалоб")] public List<string> Reasons;
            [JsonProperty("Причина блокировки - время в днях")] public Dictionary<string, double> BanReasons;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    DiscordWebHook = "",
                    ModeratorPermission = "bloodreport.moderator",
                    Cooldown = 200,
                    AlertCount = 3,
                    CustomNames = new Dictionary<string, string>()
                    {
                        ["rifle.ak"] = "AK-47"
                    },
                    Reasons = new List<string>()
                    {
                        "Читы",
                        "3+",
                        "Багоюз",
                        "Другое"
                    },
                    BanReasons = new Dictionary<string, double>()
                    {
                        ["Использование макросов"] = 3600,
                        ["Использование читов"] = 0,
                        ["Игнор проверки"] = 0,
                        ["Отказ"] = 0,
                    }
                };
            }
        }

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
        #endregion

        #region Helpers

        public string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary?.Call("GetImage", shortname, skin);

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Title}/Players", ReportInformation);
            Interface.Oxide.DataFileSystem.WriteObject($"{Title}/LastReports", LastReport);
        }

        private void LoadData()
        {
            try
            {
                ReportInformation = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerInfo>>($"{Title}/Players");
                LastReport = Interface.Oxide.DataFileSystem.ReadObject<List<ReportList>>($"{Title}/LastReports");
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (ReportInformation == null) ReportInformation = new Dictionary<ulong, PlayerInfo>();
            if (LastReport == null) LastReport = new List<ReportList>();
        }

        public string HexToCuiColor(string HEX, float Alpha = 100)
        {
            if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

            var str = HEX.Trim('#');
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {Alpha / 100}";
        }

        #endregion

        #region [Info class]

        public class PlayerInfo
        {
            [JsonProperty("Количество проверок")]
            public int ChecksCount = 0;
            [JsonProperty("Количество репортов")]
            public int ReportCount = 0;
            [JsonProperty("История репортов")]
            public List<string> ReportsHistory = new List<string>();
            [JsonProperty("Выбранные к репорту")]
            public List<ulong> ReportsList = new List<ulong>();
            [JsonProperty("Список смертей")]
            public List<ulong> DeathList = new List<ulong>();
            [JsonProperty("Список убийств")]
            public List<KillsInfo> KillsList = new List<KillsInfo>();
            [JsonProperty("Статус игрока")]
            public Status PlayerStatus = Status.None;
            [JsonProperty("Должность игрока")]
            public Permission PlayerPermission = Permission.Player;
            [JsonProperty("Кд репортов")]
            public double Cooldown = 0;


            [JsonProperty("Список тиммейтов")]
            public List<ulong> Teammates = new List<ulong>();

            public enum Permission
            {
                Player,
                Moderator,
                Administrator
            }

            public enum Status
            {
                None,
                OnCheck,
                AfkCheck,
                ModeratorOnCheck
            }

            public PlayerInfo(BasePlayer player)
            {
                if (player != null)
                {
                    if (player.IsAdmin)
                    {
                        PlayerPermission = Permission.Administrator;
                    }
                    else if (_ins.permission.UserHasPermission(player.UserIDString, _config.ModeratorPermission))
                    {
                        PlayerPermission = Permission.Moderator;
                    }
                }
            }

        }

        public class ReportList
        {
            [JsonProperty("Время репорта")]
            public DateTime Time;
            [JsonProperty("Стим айди зарепорченного")]
            public ulong SteamId;
        }

        public class KillsInfo
        {
            [JsonProperty("Ник убитого")]
            public string KilledName { get; set; }

            [JsonProperty("Айди убитого")]
            public ulong KilledId { get; set; }

            [JsonProperty("Оружие")]
            public string WeaponName { get; set; }

            [JsonProperty("Хитбокс")]
            public string HitBox { get; set; }

            [JsonProperty("Дистанция")]
            public int Distance { get; set; }
        }

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            _ins = this;
            if (!permission.PermissionExists(_config.ModeratorPermission, this))
            {
                permission.RegisterPermission(_config.ModeratorPermission, this);
            }
            LoadData();

            foreach (var item in BasePlayer.activePlayerList)
                OnPlayerConnected(item);
        }

        private void OnUserPermissionGranted(string id, string permName)
        {
            var player = BasePlayer.activePlayerList.FirstOrDefault(x => x.UserIDString == id);

            if (player != null && ReportInformation.ContainsKey(player.userID) && permName == _config.ModeratorPermission)
            {
                ReportInformation[player.userID].PlayerPermission = PlayerInfo.Permission.Moderator;
            }
        }

        private void OnUserPermissionRevoked(string id, string permName)
        {
            var player = BasePlayer.activePlayerList.FirstOrDefault(x => x.UserIDString == id);

            if (player != null && ReportInformation.ContainsKey(player.userID) && permName == _config.ModeratorPermission)
            {
                ReportInformation[player.userID].PlayerPermission = PlayerInfo.Permission.Moderator;
            }
        }

        private void Unload()
        {
            foreach (var item in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(item, "CheckAlert");
            }

            foreach (var item in ReportInformation)
            {
                item.Value.PlayerStatus = PlayerInfo.Status.None;
            }

            PlayerSaveCheck = null;

            SaveData();
            _ins = null;
        }

        private void OnServerSave()
        {
            SaveData();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player));
                return;
            }

            if (!ReportInformation.ContainsKey(player.userID)) ReportInformation.Add(player.userID, new PlayerInfo(player));

            if (permission.UserHasPermission(player.UserIDString, _config.ModeratorPermission) || player.IsAdmin)
            {
                if (!OpenedModeratorMenu.ContainsKey(player.userID))
                {
                    OpenedModeratorMenu.Add(player.userID, false);
                }
            }
            if (PlayerSaveCheck.ContainsKey(player.userID))
            {
                SendReply(BasePlayer.FindByID(PlayerSaveCheck[player.userID].ModeratorID), $"Игрок - {player.displayName}/{player.userID} вернулся сервер во время проверки");
                DiscordSendMessage($"Игрок - {player.displayName}/{player.userID} вернулся на сервер во время проверки");
            }
        }
        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (permission.UserHasPermission(player.UserIDString, _config.ModeratorPermission) || player.IsAdmin)
            {
                if (OpenedModeratorMenu.ContainsKey(player.userID))
                {
                    OpenedModeratorMenu[player.userID] = false;
                }
            }
            if (PlayerSaveCheck.ContainsKey(player.userID))
            {
                SendReply(BasePlayer.FindByID(PlayerSaveCheck[player.userID].ModeratorID), $"Игрок - {player.displayName} покинул сервер во время проверки: {reason}");
                DiscordSendMessage($"Игрок - {player.displayName} покинул сервер во время проверки: {reason}");

            }
            ReportInformation[player.userID].PlayerStatus = PlayerInfo.Status.None;
        }


        object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null) return null;
            var attacker = info.InitiatorPlayer;
            if (attacker == null) return null;
            if (attacker.userID == player.userID) return null;
            if (attacker != null)
            {
                KillsInfo killinfo = new KillsInfo
                {
                    Distance = (int)Vector3.Distance(player.transform.position, attacker.transform.position),
                    HitBox = info.boneName,
                    KilledId = player.userID,
                    KilledName = player.displayName,
                    WeaponName = _config.CustomNames.ContainsKey(info.Weapon.GetItem().info.shortname) ? _config.CustomNames[info.Weapon.GetItem().info.shortname] : info.Weapon.GetItem().info.displayName.english
                };
                if (ReportInformation.ContainsKey(player.userID))
                {
                    ReportInformation[player.userID].DeathList.Insert(0, attacker.userID);
                }
                if (ReportInformation.ContainsKey(attacker.userID))
                {
                    ReportInformation[attacker.userID].KillsList.Insert(0, killinfo);
                }
            }
            return null;
        }


        #endregion

        #region Methods
        void DiscordSendMessage(string key, ulong userID = 0, params object[] args)
        {
            if (String.IsNullOrEmpty(_config.DiscordWebHook)) return;

            List<Fields> fields = new List<Fields>
                {
                    new Fields("ReportSystem", key, true),
                };

            FancyMessage newMessage = new FancyMessage(null, false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds(null, 635133, fields, new Authors("ReportSystem", "https://vk.com/rustnastroika", "https://i.imgur.com/ILk3uJc.png", null), new Footer("Author: Sempai[https://vk.com/rustnastroika]", "https://i.imgur.com/ILk3uJc.png", null)) });
            Request($"{_config.DiscordWebHook}", newMessage.toJSON());
        }

        #region FancyDiscord
        public class FancyMessage
        {
            public string content { get; set; }
            public bool tts { get; set; }
            public Embeds[] embeds { get; set; }

            public class Embeds
            {
                public string title { get; set; }
                public int color { get; set; }
                public List<Fields> fields { get; set; }
                public Footer footer { get; set; }
                public Authors author { get; set; }

                public Embeds(string title, int color, List<Fields> fields, Authors author, Footer footer)
                {
                    this.title = title;
                    this.color = color;
                    this.fields = fields;
                    this.author = author;
                    this.footer = footer;

                }
            }

            public FancyMessage(string content, bool tts, Embeds[] embeds)
            {
                this.content = content;
                this.tts = tts;
                this.embeds = embeds;
            }

            public string toJSON() => JsonConvert.SerializeObject(this);
        }

        public class Footer
        {
            public string text { get; set; }
            public string icon_url { get; set; }
            public string proxy_icon_url { get; set; }
            public Footer(string text, string icon_url, string proxy_icon_url)
            {
                this.text = text;
                this.icon_url = icon_url;
                this.proxy_icon_url = proxy_icon_url;
            }
        }

        public class Authors
        {
            public string name { get; set; }
            public string url { get; set; }
            public string icon_url { get; set; }
            public string proxy_icon_url { get; set; }
            public Authors(string name, string url, string icon_url, string proxy_icon_url)
            {
                this.name = name;
                this.url = url;
                this.icon_url = icon_url;
                this.proxy_icon_url = proxy_icon_url;
            }
        }

        public class Fields
        {
            public string name { get; set; }
            public string value { get; set; }
            public bool inline { get; set; }
            public Fields(string name, string value, bool inline)
            {
                this.name = name;
                this.value = value;
                this.inline = inline;
            }
        }

        private void Request(string url, string payload, Action<int> callback = null)
        {
            Dictionary<string, string> header = new Dictionary<string, string>();
            header.Add("Content-Type", "application/json");
            webrequest.Enqueue(url, payload, (code, response) =>
            {
                if (code != 200 && code != 204)
                {
                    if (response != null)
                    {
                        try
                        {
                            JObject json = JObject.Parse(response);
                            if (code == 429)
                            {
                                float seconds = float.Parse(Math.Ceiling((double)(int)json["retry_after"] / 1000).ToString());
                            }
                            else
                            {
                                PrintWarning($" Discord rejected that payload! Responded with \"{json["message"].ToString()}\" Code: {code}");
                            }
                        }
                        catch
                        {
                            PrintWarning($"Failed to get a valid response from discord! Error: \"{response}\" Code: {code}");
                        }
                    }
                    else
                    {
                        PrintWarning($"Discord didn't respond (down?) Code: {code}");
                    }
                }
                try
                {
                    callback?.Invoke(code);
                }
                catch (Exception ex) { }

            }, this, RequestMethod.POST, header);
        }
        #endregion

        private void ReportPlayer(BasePlayer target, string reason)
        {
            ReportInformation[target.userID].ReportCount++;
            ReportInformation[target.userID].ReportsHistory.Insert(0, reason);
            LastReport.Insert(0, new ReportList()
            {
                SteamId = target.userID,
                Time = DateTime.Now
            });

            if (ReportInformation[target.userID].ReportCount >= _config.AlertCount)
            {
                DiscordSendMessage($"Игрок - {target.displayName}/{target.userID} достиг максимальное количество репортов\nСвободный модератор вызовите на проверку!");

                foreach (var item in BasePlayer.activePlayerList.Where(x => ReportInformation[x.userID].PlayerPermission != PlayerInfo.Permission.Player))
                {
                    SendReply(item, $"Игрок - {target.displayName}/{target.userID} достиг максимальное количество репортов\nСвободный модератор вызовите на проверку!");
                }
            }
        }

        public string TimeToString(double time)
        {
            TimeSpan elapsedTime = TimeSpan.FromSeconds(time);
            int hours = elapsedTime.Hours;
            int minutes = elapsedTime.Minutes;
            int seconds = elapsedTime.Seconds;
            int days = Mathf.FloorToInt((float)elapsedTime.TotalDays);
            string s = "";
            if (days > 0) s += $"{days} дн.";
            if (hours > 0) s += $"{hours} ч. ";
            if (minutes > 0) s += $"{minutes} м. ";
            if (seconds > 0) s += $"{seconds} с.";
            else s = s.TrimEnd(' ');
            return s;
        }

        public bool IsRaidBlocked(BasePlayer player)
        {
            if (NoEscape)
                return (bool)NoEscape?.Call("IsRaidBlocked", player);
            else return false;
        }

        private void SoundToast(BasePlayer player, string text, int type)
        {
            Effect effect = new Effect("assets/bundled/prefabs/fx/notice/item.select.fx.prefab", player, 0, new Vector3(), new Vector3());
            EffectNetwork.Send(effect, player.Connection);

            player.Command("gametip.showtoast", type, text);
        }

        private double CurrentTime() => DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

        #endregion

        #region Commands
        private void ReportUI(BasePlayer player)
        {
            if (player == null) return;

            if (PlayerSaveCheck != null)
            {
                foreach (var kvp in PlayerSaveCheck)
                {
                    if (kvp.Value.ModeratorID == player.userID)
                    {

                        ModeratorCheckInfo(player, kvp.Key);
                        return;
                    }
                }
            }

            ReportsUI(player);
        }

        [ConsoleCommand("reports")]
        void ConsoleReport(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player != null && args.HasArgs(1))
            {
                if (args.Args[0] == "back")
                {
                    ReportsBack(player);
                }
                if (args.Args[0] == "help")
                {
                    ShowUIInforamtion(player);
                }
                if (args.Args[0] == "stopcheck")
                {
                    if (permission.UserHasPermission(player.UserIDString, _config.ModeratorPermission) || player.IsAdmin)
                    {
                        if (args.Args.Length > 1)
                            StopCheck(player, Convert.ToUInt64(args.Args[1]));
                    }
                }
                if (args.Args[0] == "trychecklast")
                {
                    if (args.Args.Length > 1)
                    {
                        string target = args.Args[1];
                        TryCheckLast(player, target);
                    }
                }
                if (args.Args[0] == "trycheck")
                {
                    if (args.Args.Length > 1)
                    {
                        string target = args.Args[1];

                        TryCheck(player, target);
                    }
                }
                if (args.Args[0] == "add")
                {
                    if (args.Args.Length > 1)
                        AddTargetHandler(player, Convert.ToUInt64(args.Args[1]));
                }
                if (args.Args[0] == "remove")
                {
                    if (args.Args.Length > 1)
                        RemoveTargetHandler(player, Convert.ToUInt64(args.Args[1]));
                }
                if (args.Args[0] == "closemenu")
                {
                    if (OpenedModeratorMenu.ContainsKey(player.userID))
                        OpenedModeratorMenu[player.userID] = false;

                    LoadedPlayers(player);
                }
                if (args.Args[0] == "sendcheck")
                {
                    if (args.Args.Length > 1)
                        SendCheck(player, Convert.ToUInt64(args.Args[1]));
                }
                if (args.Args[0] == "cancelcheck")
                {
                    LoadedPlayersModerator(player);
                }
                if (args.Args[0] == "verdict")
                {
                    if (permission.UserHasPermission(player.UserIDString, _config.ModeratorPermission) || player.IsAdmin)
                    {
                        if (args.Args.Length > 2)
                            StopCheckVerdict(player, Convert.ToUInt64(args.Args[1]), Convert.ToInt32(args.Args[2]));
                    }
                }
                if (args.Args[0] == "moderation")
                {
                    OpenModerationMenu(player);
                }
                if (args.Args[0] == "send")
                {
                    SendReportHandler(player, args.Args[1]);
                }
                if (args.Args[0] == "search")
                {
                    if (args.Args.Length > 1)
                    {
                        string name = args.Args[1];
                        LoadedPlayers(player, name);
                    }
                }
                if (args.Args[0] == "page")
                {
                    LoadedPlayers(player, "", int.Parse(args.Args[1]));
                }
            }
        }

        [ChatCommand("discord")]
        private void SendDiscord(BasePlayer Suspect, string command, string[] args)
        {
            if (!PlayerSaveCheck.ContainsKey(Suspect.userID))
            {
                return;
            }
            string Discord = "";
            foreach (var arg in args)
                Discord += " " + arg;

            PlayerSaveCheck[Suspect.userID].Discord = Discord;

            BasePlayer Moderator = BasePlayer.FindByID(PlayerSaveCheck[Suspect.userID].ModeratorID);


            if (Discord != "")
            {
                var container = new CuiElementContainer();
                CuiHelper.DestroyUi(Moderator, "DiscordImage");
                CuiHelper.DestroyUi(Moderator, "DiscordText");

                container.Add(new CuiElement
                {
                    Name = "DiscordText",
                    Parent = "DiscordPanel",
                    Components = {
                            new CuiTextComponent { Text = $"{PlayerSaveCheck[Suspect.userID].Discord}", Font = "robotocondensed-bold.ttf", FontSize = 7, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.6" },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                        }
                });

                DiscordSendMessage($"Игрок {Suspect.displayName}/{Suspect.userID} предоставил дискорд для проверки: {Discord}");

                CuiHelper.AddUi(Moderator, container);
            }

        }

        private void StopCheck(BasePlayer moderator, ulong target)
        {
            ReportInformation[target].ChecksCount++;
            ReportInformation[target].PlayerStatus = PlayerInfo.Status.None;

            PlayerSaveCheck.Remove(target);

            var targetPlayer = BasePlayer.FindByID(target) ?? BasePlayer.FindAwakeOrSleeping(target.ToString());

            CuiHelper.DestroyUi(targetPlayer, "CheckAlert");
            CuiHelper.DestroyUi(targetPlayer, "CheckTarget");

            ReportInformation[moderator.userID].PlayerStatus = PlayerInfo.Status.None;

            ReportInformation[target].ReportCount = 0;

            ReportInformation[target].ReportsHistory.Clear();

            LastReport.Remove(LastReport.FirstOrDefault(x => x.SteamId == target));

            ReportsUI(moderator);

            DiscordSendMessage($"Модератор - {moderator.displayName}/{moderator.userID} завершил проверку игрока - {targetPlayer.displayName}/{target}\nВердикт:Чист");
        }


        private void StopCheckVerdict(BasePlayer moderator, ulong target, int index)
        {
            ReportInformation[target].ChecksCount++;
            ReportInformation[target].PlayerStatus = PlayerInfo.Status.None;

            PlayerSaveCheck.Remove(target);

            var targetPlayer = BasePlayer.FindByID(target) ?? BasePlayer.FindAwakeOrSleeping(target.ToString());

            CuiHelper.DestroyUi(targetPlayer, "CheckAlert");
            CuiHelper.DestroyUi(targetPlayer, "CheckTarget");

            ReportInformation[moderator.userID].PlayerStatus = PlayerInfo.Status.None;

            ReportInformation[target].ReportCount = 0;

            ReportInformation[target].ReportsHistory.Clear();

            rust.RunClientCommand(moderator, $"ban {target} {_config.BanReasons.ElementAt(index).Value}d {_config.BanReasons.ElementAt(index).Key}");

            LastReport.Remove(LastReport.FirstOrDefault(x => x.SteamId == target));

            ReportsUI(moderator);

            DiscordSendMessage($"Модератор - {moderator.displayName}/{moderator.userID} завершил проверку игрока - {targetPlayer.displayName}/{target}\nВердикт:{_config.BanReasons.ElementAt(index).Key}");
        }

        private void SendCheck(BasePlayer moderator, ulong target)
        {
            if (permission.UserHasPermission(moderator.UserIDString, _config.ModeratorPermission) || moderator.IsAdmin)
            {
                if (ReportInformation[moderator.userID].PlayerStatus != PlayerInfo.Status.None)
                {
                    SoundToast(moderator, "У вас уже имеется активная проверка", 1);
                    LoadedPlayersModerator(moderator);
                    return;
                }

                if (ReportInformation[target].PlayerStatus != PlayerInfo.Status.None)
                {
                    SoundToast(moderator, "Игрок уже проверяется", 1);
                    LoadedPlayersModerator(moderator);
                    return;
                }

                if (IsRaidBlocked(BasePlayer.FindByID(target)))
                {
                    SoundToast(moderator, "Игрок находится в рейдблоке", 1);
                    LoadedPlayersModerator(moderator);
                    return;
                }

                ReportInformation[target].PlayerStatus = PlayerInfo.Status.AfkCheck;
                LoadedPlayersModerator(moderator);

                Metods_AFK(moderator, target);

            }

        }

        public Dictionary<ulong, GenericPosition> AFKPositionTry = new Dictionary<ulong, GenericPosition>();
        public Dictionary<ulong, int> AFKCheckedTry = new Dictionary<ulong, int>();

        void Metods_AFK(BasePlayer Moderator, ulong SuspectID)
        {
            IPlayer Suspect = covalence.Players.FindPlayerById(SuspectID.ToString());
            if (!AFKCheckedTry.ContainsKey(SuspectID))
                AFKCheckedTry.Add(SuspectID, 0);
            else AFKCheckedTry[SuspectID] = 0;

            StartAFKCheck(Moderator, Suspect);
        }

        public void StartAFKCheck(BasePlayer Moderator, IPlayer Suspect)
        {
            SoundToast(Moderator, "Проверка на афк - началась", 0);

            ulong SuspectID = ulong.Parse(Suspect.Id);
            var Position = Suspect.Position();

            if (!AFKPositionTry.ContainsKey(SuspectID))
                AFKPositionTry.Add(SuspectID, Position);

            int Try = 1;
            double seconds = 0;
            timer.Repeat(5f, 5, () =>
            {
                Position = Suspect.Position();
                if (AFKPositionTry[SuspectID] != Position)
                {
                    AFKCheckedTry[SuspectID]++;
                }

                AFKPositionTry[SuspectID] = Position;
                Try++;
            });
            timer.Repeat(1f, 30, () =>
            {
                if (OpenedModeratorMenu[Moderator.userID])
                    DrawAfkCheck(Moderator, seconds);
                seconds++;
            });
            timer.Once(30f, () =>
            {
                if (AFKCheckedTry[SuspectID] < 3)
                {
                    SoundToast(Moderator, "Игрок - афк, проверка отменяется", 1);
                    ReportInformation[SuspectID].PlayerStatus = PlayerInfo.Status.None;
                    LoadedPlayersModerator(Moderator);
                    CuiHelper.DestroyUi(Moderator, "AfkCheck");
                }
                else
                {
                    BasePlayer SuspectOnline = BasePlayer.FindByID(ulong.Parse(Suspect.Id));

                    ReportInformation[SuspectID].PlayerStatus = PlayerInfo.Status.OnCheck;
                    ReportInformation[Moderator.userID].PlayerStatus = PlayerInfo.Status.None;


                    if (SuspectOnline == null || !SuspectOnline.IsConnected)
                    {
                        SoundToast(Moderator, $"Игрок - {Suspect.Name} покинул сервер во время скрытой проверки на АФК!\nПроверка была снята автоматически", 1);
                        DiscordSendMessage($"Игрок - {Suspect.Name} покинул сервер во время скрытой проверки на АФК!\nПроверка была снята автоматически", 1);
                        if (PlayerSaveCheck.ContainsKey(SuspectID))
                            PlayerSaveCheck.Remove(SuspectID);
                        return;
                    }

                    if (!PlayerSaveCheck.ContainsKey(SuspectOnline.userID))
                    {
                        PlayerSaveCheck.Add(SuspectID, new PlayerSaveCheckClass
                        {
                            Discord = string.Empty,
                            NickName = Suspect.Name,

                            ModeratorID = Moderator.userID,
                        });
                    }
                    else
                    {
                        PlayerSaveCheck.Remove(SuspectOnline.userID);

                        PlayerSaveCheck.Add(SuspectID, new PlayerSaveCheckClass
                        {
                            Discord = string.Empty,
                            NickName = Suspect.Name,

                            ModeratorID = Moderator.userID,
                        });
                    }

                    CuiHelper.DestroyUi(Moderator, "AfkCheck");
                    CuiHelper.DestroyUi(Moderator, "ModeratorPanel");

                    ModeratorCheckInfo(Moderator, SuspectID);
                    UI_AlertSendPlayer(SuspectOnline);
                    DiscordSendMessage($"Модератор {Moderator.displayName}/{Moderator.userID} вызвал на проверку игрока - {SuspectOnline.displayName}/{SuspectOnline.userID}");
                }
                if (AFKCheckedTry.ContainsKey(SuspectID))
                    AFKCheckedTry.Remove(SuspectID);
            });
        }

        void UI_AlertSendPlayer(BasePlayer Suspect)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(Suspect, "CheckAlert");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "0 0.75", AnchorMax = "1 0.9", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.9" },
            }, "Overlay", "CheckAlert");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = $"<b><size=20>{Suspect.displayName.ToUpper()}, ВАС ВЫЗВАЛИ НА ПРОВЕРКУ</size></b>\nПредоставте свой дискорд или скайп для проверки.\nВведите команду /discord\nЕсли Вы покинете сервер, Вы будете забанены на проекте.\nУ вас есть 5 минут!", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" }
            }, "CheckAlert");

            CuiHelper.AddUi(Suspect, container);
        }

        private void DrawAfkCheck(BasePlayer moderator, double seconds)
        {
            var container = new CuiElementContainer();
            CuiHelper.DestroyUi(moderator, "AfkCheck");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 1.015", AnchorMax = "1 1.08" },
                Image = { Color = HexToCuiColor("#84b4dd", 25) }
            }, "ModeratorPanel", "AfkCheck");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = $"Проверка на АФК, осталось {30 - seconds}с", Color = HexToCuiColor("#84b4dd", 100), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "AfkCheck");

            CuiHelper.AddUi(moderator, container);
        }

        private void ModeratorCheckInfo(BasePlayer moderator, ulong target)
        {
            CuiHelper.DestroyUi(moderator, "ModeratorPanel");
            CuiHelper.DestroyUi(moderator, "CheckTarget");

            var container = new CuiElementContainer();

            var targetPlayer = BasePlayer.FindByID(target) ?? BasePlayer.FindAwakeOrSleeping(target.ToString());

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Menu_Block", "CheckTarget");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.94", AnchorMax = "0.94 1" },
                Image = { Color = HexToCuiColor("#84b4dd", 25) },
            }, "CheckTarget", "Title");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = $"Игрок двигался, удачной проверки!", Color = HexToCuiColor("#84b4dd", 100), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Title");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.95 0.94", AnchorMax = "1 1" },
                Button = { Color = HexToCuiColor("#e0947a", 25), Command = "reports closemenu" },
                Text = { Text = "✕", Color = HexToCuiColor("#e0947a", 100), Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" },
            }, "CheckTarget");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.87", AnchorMax = "0.65 0.93" },
                Image = { Color = HexToCuiColor("#e0947a", 25) },
            }, "CheckTarget", "SuspectTitle");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = "Подозреваемый", Color = HexToCuiColor("#e0947a", 100), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "SuspectTitle");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.66 0.87", AnchorMax = "1 0.93" },
                Image = { Color = HexToCuiColor("#e0947a", 25) },
            }, "CheckTarget", "LastKills");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = "Последние убийства", Color = HexToCuiColor("#e0947a", 100), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "LastKills");

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" },
                RectTransform = { AnchorMin = "0 0.78", AnchorMax = "0.65 0.86" }
            }, "CheckTarget", "SuspectPanel");

            container.Add(new CuiElement
            {
                Name = "Avatar",
                Parent = "SuspectPanel",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage(target.ToString(), 0) },
                    new CuiRectTransformComponent { AnchorMin = "0.015 0.15", AnchorMax = "0.09 0.85" }
                }
            });

            var name = targetPlayer.displayName.Length > 20 ? targetPlayer.displayName.Substring(0, 20) + "..." : targetPlayer.displayName;
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.11 0.33", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = name, Color = HexToCuiColor("#cec5bb", 100), Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "SuspectPanel");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.11 0", AnchorMax = "1 0.67", OffsetMax = "0 0" },
                Text = { Text = $"{target}", Color = HexToCuiColor("#7f7d7d", 100), Align = TextAnchor.MiddleLeft, FontSize = 8, Font = "robotocondensed-regular.ttf" }
            }, "SuspectPanel");

            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.66 0.4", AnchorMax = "1 0.86" }
            }, "CheckTarget", "KillsPanel");

            float width = 0.99f, height = 0.14f, startxBox = 0f, startyBox = 0.994f - height, xmin = startxBox, ymin = startyBox;
            foreach (var i in Enumerable.Range(0, 6))
            {
                var item = ReportInformation[target].KillsList.ElementAtOrDefault(i);
                if (item != null)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}" },
                        Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
                    }, "KillsPanel", "CheckPlayer");

                    xmin += width + 0.02f;
                    if (xmin + width >= 0)
                    {
                        xmin = startxBox;
                        ymin -= height + 0.03f;
                    }

                    var killedname = item.KilledName.Length > 15 ? item.KilledName.Substring(0, 15) + "..." : item.KilledName;

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.02 0.35", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Text = { Text = killedname, Color = HexToCuiColor("#cec5bb", 100), Align = TextAnchor.MiddleLeft, FontSize = 10, Font = "robotocondensed-regular.ttf" }
                    }, "CheckPlayer");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.02 0", AnchorMax = "1 0.6", OffsetMax = "0 0" },
                        Text = { Text = $"<color=#84b4dd>{item.WeaponName}</color> - <color=#e0947a>{item.HitBox}</color>", Color = HexToCuiColor("#7f7d7d", 100), Align = TextAnchor.MiddleLeft, FontSize = 8, Font = "robotocondensed-regular.ttf" }
                    }, "CheckPlayer");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "0.96 1", OffsetMax = "0 0" },
                        Text = { Text = $"{item.Distance}м", Color = HexToCuiColor("#7f7d7d", 100), Align = TextAnchor.MiddleRight, FontSize = 10, Font = "robotocondensed-regular.ttf" }
                    }, "CheckPlayer");
                }
            }

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.66 0.33", AnchorMax = "0.998 0.388" },
                Button = { Color = HexToCuiColor("#bbc47e", 25), Command = $"reports stopcheck {target}" },
                Text = { Text = "Стоп", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#bbc47e", 100) },
            }, "CheckTarget", "StopBTN");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.7", AnchorMax = "0.65 0.765" },
                Image = { Color = HexToCuiColor("#84b4dd", 25) },
            }, "CheckTarget", "TeamTittle");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = "Тимейты подозреваемого", Color = HexToCuiColor("#84b4dd", 100), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "TeamTittle");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.4", AnchorMax = "0.65 0.688" },
                Image = { Color = $"0 0 0 0" }
            }, "CheckTarget", "TeamPanel");

            var Team = targetPlayer.Team == null ? ReportInformation[target].Teammates : targetPlayer.Team.members;

            float width1 = 0.996f, height1 = 0.3f, startxBox1 = 0f, startyBox1 = 0.994f - height1, xmin1 = startxBox1, ymin1 = startyBox1;
            foreach (var j in Enumerable.Range(0, 3))
            {
                var item = Team.Where(x => x != target).ElementAtOrDefault(j);
                if (item != 0)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + height1 * 1}" },
                        Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
                    }, "TeamPanel", "TeamMate");

                    xmin1 += width1 + 0.02f;
                    if (xmin1 + width1 >= 0)
                    {
                        xmin1 = startxBox1;
                        ymin1 -= height1 + 0.045f;
                    }

                    container.Add(new CuiElement
                    {
                        Name = "Avatar",
                        Parent = "TeamMate",
                        Components = {
                            new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage(item.ToString(), 0) },
                            new CuiRectTransformComponent { AnchorMin = "0.015 0.15", AnchorMax = "0.1 0.85" }
                        }
                    });

                    var mate = covalence.Players.FindPlayerById(item.ToString());

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.12 0.35", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Text = { Text = mate.Name, Color = HexToCuiColor("#cec5bb", 100), Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                    }, "TeamMate");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.12 0", AnchorMax = "1 0.65", OffsetMax = "0 0" },
                        Text = { Text = $"{item}", Color = HexToCuiColor("#7f7d7d", 100), Align = TextAnchor.MiddleLeft, FontSize = 8, Font = "robotocondensed-regular.ttf" }
                    }, "TeamMate");
                }
                else
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + height1 * 1}" },
                        Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
                    }, "TeamPanel", "TeamMate");

                    xmin1 += width1 + 0.02f;
                    if (xmin1 + width1 >= 0)
                    {
                        xmin1 = startxBox1;
                        ymin1 -= height1 + 0.045f;
                    }

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                        Text = { Text = $"Тимейт отсутствует", Color = HexToCuiColor("#7f7d7d", 100), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                    }, "TeamMate");
                }
            }

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.33", AnchorMax = "0.65 0.388" },
                Image = { Color = $"1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, "CheckTarget", "DiscordPanel");

            if (PlayerSaveCheck[target].Discord == "")
            {
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = "Дискорд не предоставлен!", Color = HexToCuiColor("#7f7d7d", 100), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "DiscordPanel");
            }
            else
            {
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = $"Дискорд - {PlayerSaveCheck[target].Discord}", Color = HexToCuiColor("#7f7d7d", 100), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, "DiscordPanel");
            }

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.96 1", OffsetMax = "0 0" },
                Text = { Text = $"{TimeToString(DateTime.Now.Subtract(PlayerSaveCheck[target].CheckStart).TotalSeconds)}", Color = HexToCuiColor("#7f7d7d", 100), Align = TextAnchor.MiddleRight, FontSize = 10, Font = "robotocondensed-regular.ttf" }
            }, "SuspectPanel");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.66 0", AnchorMax = "0.998 0.316" },
                Image = { Color = "0 0 0 0" }
            }, "CheckTarget", "BanReason");

            float width2 = 0.996f, height2 = 0.213f, startxBox2 = 0f, startyBox2 = 0.994f - height2, xmin2 = startxBox2, ymin2 = startyBox2;
            foreach (var item in _config.BanReasons)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{xmin2} {ymin2}", AnchorMax = $"{xmin2 + width2} {ymin2 + height2 * 1}" },
                    Button = { Color = HexToCuiColor("#e0947a", 25), Command = $"reports verdict {target}" },
                    Text = { Text = $"{item.Key}", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#e0947a", 100) },
                }, "BanReason");

                xmin2 += width2 + 0.02f;
                if (xmin2 + width2 >= 0)
                {
                    xmin2 = startxBox2;
                    ymin2 -= height2 + 0.045f;
                }
            }

            CuiHelper.AddUi(moderator, container);
        }

        private void TryCheck(BasePlayer moderator, string target)
        {
            if (permission.UserHasPermission(moderator.UserIDString, _config.ModeratorPermission) || moderator.IsAdmin)
            {
                CuiHelper.DestroyUi(moderator, "Gradient");
                var container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image = { Color = "0 0 0 0" }
                }, $"CheckPlayer{target}", "Gradient");

                container.Add(new CuiElement
                {
                    Parent = $"CheckPlayer{target}",
                    Name = "Gradient",
                    Components =
                    {
                        new CuiRawImageComponent{ Color = "1 1 1 1", Png = GetImage("GradientCheck")},
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                    }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = $"reports sendcheck {target}" },
                    Text = { Text = "Вызвать", Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#bbc47e", 100) },
                    RectTransform = { AnchorMin = "0.04 0", AnchorMax = "0.5 1" }
                }, "Gradient");

                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = "reports cancelcheck", Close = "Gradient" },
                    Text = { Text = "Закрыть", Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleRight, Color = HexToCuiColor("#e0947a", 100) },
                    RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.96 1" }
                }, "Gradient");

                CuiHelper.AddUi(moderator, container);
            }
        }

        private void TryCheckLast(BasePlayer moderator, string target)
        {
            if (permission.UserHasPermission(moderator.UserIDString, _config.ModeratorPermission) || moderator.IsAdmin)
            {
                CuiHelper.DestroyUi(moderator, "Gradient");
                var container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.99 0.996" },
                    Image = { Color = "0 0 0 0" }
                }, $"LastReport{target}", "Gradient");

                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Command = $"reports sendcheck {target}" },
                    Text = { Text = "Вызвать", Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#bbc47e", 100) },
                    RectTransform = { AnchorMin = "0.04 0", AnchorMax = "0.5 1" }
                }, "Gradient");

                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Close = "Gradient" },
                    Text = { Text = "Закрыть", Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleRight, Color = HexToCuiColor("#e0947a", 100) },
                    RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.96 1" }
                }, "Gradient");

                CuiHelper.AddUi(moderator, container);
            }

        }

        private void ReportsBack(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "ModerBG");

            OpenedModeratorMenu[player.userID] = false;

            ReportsUI(player);
        }

        private void AddTargetHandler(BasePlayer player, ulong id)
        {
            if (ReportInformation[player.userID].ReportsList.Count >= 4) return;
            if (!ReportInformation[player.userID].ReportsList.Contains(id))
            {
                ReportInformation[player.userID].ReportsList.Insert(0, id);
            }
            LoadedPlayers(player);
        }

        private void RemoveTargetHandler(BasePlayer player, ulong id)
        {
            if (ReportInformation[player.userID].ReportsList.Contains(id))
            {
                ReportInformation[player.userID].ReportsList.Remove(id);
            }
            LoadedPlayers(player);
        }

        private void OpenModerationMenu(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, _config.ModeratorPermission) || player.IsAdmin)
            {
                OpenedModeratorMenu[player.userID] = true;

                CuiHelper.DestroyUi(player, "MainLayer3");
                ModerUI(player);
            }
        }

        private void SendReportHandler(BasePlayer player, string reason)
        {
            if (ReportInformation[player.userID].Cooldown > CurrentTime())
            {
                var msg = $"Перед отправкой следующего репорта необходимо подождать {TimeSpan.FromSeconds(ReportInformation[player.userID].Cooldown - CurrentTime()).TotalSeconds}с";
                SoundToast(player, msg, 1);
                return;
            }

            foreach (var item in ReportInformation[player.userID].ReportsList)
            {
                BasePlayer target = BasePlayer.Find(item.ToString());
                ReportPlayer(target, reason);
            }
            ReportInformation[player.userID].ReportsList.Clear();
            ReportInformation[player.userID].Cooldown = CurrentTime() + _config.Cooldown;
            var container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "Message");

            container.Add(new CuiPanel
            {
                FadeOut = 0.2f,
                RectTransform = { AnchorMin = "0 1.015", AnchorMax = "1 1.08" },
                Image = { Color = HexToCuiColor("#bbc47e", 25), FadeIn = 0.5f }
            }, "Parent", "Message");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = $"Жалоба на игрока(-ов), успешно отправлена, в ближайшее время она будет рассмотрена", Color = HexToCuiColor("#bbc47e", 100), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Message");

            CuiHelper.AddUi(player, container);
            timer.Once(5f, () => CuiHelper.DestroyUi(player, "Message"));
            LoadedPlayers(player);
        }

        private void SearchHandler(BasePlayer player, string name)
        {
            LoadedPlayers(player, name);
        }

        #endregion

        #region UI

        private void ModerUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Parent");
            CuiHelper.DestroyUi(player, "ModeratorPanel");
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
            }, "Menu_Block", "ModeratorPanel");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.94", AnchorMax = "0.94 1" },
                Image = { Color = HexToCuiColor("#e0947a", 25) },
            }, "ModeratorPanel", "Title");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = $"Панель модератора", Color = HexToCuiColor("#e0947a", 100), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Title");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.95 0.94", AnchorMax = "1 1" },
                Button = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat", Command = "reports back" },
                Text = { Text = "" },
            }, "ModeratorPanel", "BackButton");

            container.Add(new CuiPanel
            {
                Image = { Color = $"1 1 1 0.5", Sprite = "assets/icons/enter.png" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "7 7", OffsetMax = "-7 -7" }
            }, "BackButton");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.65 0.87", AnchorMax = "1 0.928" },
                Image = { Color = HexToCuiColor("#e0947a", 25) },
            }, "ModeratorPanel", "LastReportsTitlePanel");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = $"Последние репорты", Color = HexToCuiColor("#e0947a", 100), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "LastReportsTitlePanel");

            CuiHelper.AddUi(player, container);

            LoadedPlayersModerator(player);
        }

        private void ReportsUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Parent");
            CuiHelper.DestroyUi(player, "ModeratorPanel");

            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.998", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Menu_Block", "Parent");

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" },
                RectTransform = { AnchorMin = "0 0.94", AnchorMax = "0.05 1" }
            }, "Parent", "LeftIcon");

            bool isModerator = permission.UserHasPermission(player.UserIDString, _config.ModeratorPermission);

            var sprite = isModerator ? "assets/icons/facepunch.png" : "assets/icons/info.png";

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.59 0.94", AnchorMax = "0.64 1" },
                Text = { Text = "", },
                Button = { Color = HexToCuiColor("#d1b283", 25), Command = isModerator ? "reports moderation" : "reports help" }
            }, "Parent", "RightIcon");

            container.Add(new CuiPanel
            {
                Image = { Color = HexToCuiColor("#d1b283", 100), Sprite = sprite },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" }
            }, "RightIcon");

            container.Add(new CuiPanel
            {
                Image = { Color = $"1 1 1 0.5", Sprite = "assets/icons/web.png" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 5", OffsetMax = "-5 -5" }
            }, "LeftIcon");

            container.Add(new CuiElement
            {
                Parent = "Parent",
                Name = "Input",
                Components =
                {
                    new CuiImageComponent { Color = "1 1 1 0.02", Material = "assets/content/ui/uibackgroundblur.mat" },
                    new CuiRectTransformComponent { AnchorMin = "0.052 0.94", AnchorMax = "0.58 1" }
                }
            });

            container.Add(new CuiElement
            {
                Parent = "Input",
                Components =
                {
                    new CuiInputFieldComponent{Color = HexToCuiColor("#7f7d7d", 100), Text = "Поиск по нику", Align = TextAnchor.MiddleLeft, FontSize = 10, Font = "robotocondensed-regular.ttf", Command = "reports search", NeedsKeyboard = true},
                    new CuiRectTransformComponent{AnchorMin = "0.02 0", AnchorMax = "1 1", OffsetMin = "5 0", OffsetMax = "5 0"}
                }
            });

            CuiHelper.AddUi(player, container);
            LoadedPlayers(player);
        }

        private void LoadedPlayersModerator(BasePlayer player, int page = 1)
        {
            CuiHelper.DestroyUi(player, "PanelPlayer");
            CuiHelper.DestroyUi(player, "LastReports");
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = $"0.64 0.928" }
            }, "ModeratorPanel", "PanelPlayer");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.65 0", AnchorMax = "1 0.86" },
                Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" },
            }, "ModeratorPanel", "LastReports");

            var target = BasePlayer.activePlayerList
                .Where(z => z.userID != player.userID && ReportInformation[z.userID].ReportCount >= _config.AlertCount).OrderByDescending(x => ReportInformation[x.userID].ReportCount).ToList();

            float width = 0.488f, height = 0.093f, startxBox = 0f, startyBox = 0.996f - height, xmin = startxBox, ymin = startyBox;
            foreach (var i in Enumerable.Range(0, 18))
            {
                var item = target.ElementAtOrDefault(i);
                if (item != null)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}" },
                        Button = { Color = "1 1 1 0.04", Command = $"reports trycheck {item.userID}", Material = "assets/content/ui/uibackgroundblur.mat" },
                        Text = { Text = "" }
                    }, "PanelPlayer", $"CheckPlayer{item.userID}");

                    xmin += width + 0.02f;
                    if (xmin + width >= 1)
                    {
                        xmin = startxBox;
                        ymin -= height + 0.02f;
                    }

                    container.Add(new CuiElement
                    {
                        Name = "Avatar",
                        Parent = $"CheckPlayer{item.userID}",
                        Components =
                        {
                            new CuiRawImageComponent{Color = "1 1 1 1", Png = GetImage(item.userID.ToString(), 0)},
                            new CuiRectTransformComponent{AnchorMin = "0.025 0.12", AnchorMax = "0.2 0.88"}
                        }
                    });

                    var status = ReportInformation[item.userID].PlayerStatus;

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.235 0.35", AnchorMax = "0.8 1", OffsetMax = "0 0" },
                        Text = { Text = $"{item.displayName}", Color = status != PlayerInfo.Status.None ? HexToCuiColor("#cec5bb", 100) : HexToCuiColor("#cec5bb", 100), Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-bold.ttf" }
                    }, $"CheckPlayer{item.userID}");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.235 0", AnchorMax = "0.8 0.65", OffsetMax = "0 0" },
                        Text = { Text = status != PlayerInfo.Status.None ? "Игрок на проверке..." : $"{ReportInformation[item.userID].ReportCount} жалобы", Color = HexToCuiColor("#7f7d7d", 100), Align = TextAnchor.MiddleLeft, FontSize = 8, Font = "robotocondensed-regular.ttf" }
                    }, $"CheckPlayer{item.userID}");
                }
                else
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}" },
                        Button = { Color = "1 1 1 0.02", Command = "", Material = "assets/content/ui/uibackgroundblur.mat" },
                        Text = { Text = "" }
                    }, "PanelPlayer", $"CheckPlayer0");

                    xmin += width + 0.02f;
                    if (xmin + width >= 1)
                    {
                        xmin = startxBox;
                        ymin -= height + 0.02f;
                    }
                }
            }

            float width1 = 1f, height1 = 0.093f, startxBox1 = 0f, startyBox1 = 0.996f - height1, xmin1 = startxBox1, ymin1 = startyBox1;
            if (LastReport != null)
            {
                HashSet<ulong> takenSteamIds = new HashSet<ulong>();

                foreach (var j in Enumerable.Range(0, 6))
                {
                    var item = LastReport.Where(x => BasePlayer.FindByID(x.SteamId) != null && !takenSteamIds.Contains(x.SteamId)).ElementAtOrDefault(j);

                    if (item != null)
                    {
                        container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + height1 * 1}" },
                            Button = { Color = "0 0 0 0", Command = $"reports trychecklast {item.SteamId}" },
                            Text = { Text = "" }
                        }, "LastReports", $"LastReport{item.SteamId}");

                        xmin1 += width1 + 0.02f;
                        if (xmin1 + width1 >= 0)
                        {
                            xmin1 = startxBox1;
                            ymin1 -= height1 + 0.02f;
                        }


                        container.Add(new CuiElement
                        {
                            Name = $"Avatar{item.SteamId}",
                            Parent = $"LastReport{item.SteamId}",
                            Components = {
                                new CuiRawImageComponent { Color = "1 1 1 1", Png = GetImage(item.SteamId.ToString(), 0) },
                                new CuiRectTransformComponent{AnchorMin = "0.025 0.12", AnchorMax = "0.17 0.88"}
                            }
                        });

                        var status = ReportInformation[item.SteamId].PlayerStatus;

                        var name = covalence.Players.FindPlayerById(item.SteamId.ToString()).Name;

                        var SubStringName = name.Length > 12 ? name.Substring(0, 12) + "..." : name;

                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0.21 0.35", AnchorMax = "0.8 1", OffsetMax = "0 0" },
                            Text = { Text = $"{SubStringName}", Color = status != PlayerInfo.Status.None ? HexToCuiColor("#cec5bb", 100) : HexToCuiColor("#cec5bb", 100), Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-bold.ttf" }
                        }, $"LastReport{item.SteamId}");

                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0.21 0", AnchorMax = "0.8 0.65", OffsetMax = "0 0" },
                            Text = { Text = status == PlayerInfo.Status.OnCheck ? "Игрок на проверке..." : $"{ReportInformation[item.SteamId].ReportCount} жалобы", Color = HexToCuiColor("#7f7d7d", 100), Align = TextAnchor.MiddleLeft, FontSize = 8, Font = "robotocondensed-regular.ttf" }
                        }, $"LastReport{item.SteamId}");

                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.96 1", OffsetMax = "0 0" },
                            Text = { Text = $"{TimeToString(DateTime.Now.Subtract(item.Time).TotalSeconds)}", Color = HexToCuiColor("#7f7d7d", 100), Align = TextAnchor.MiddleRight, FontSize = 8, Font = "robotocondensed-regular.ttf" }
                        }, $"LastReport{item.SteamId}");

                        takenSteamIds.Add(item.SteamId);
                    }
                }

            }


            CuiHelper.AddUi(player, container);
        }

        void LoadedPlayers(BasePlayer player, string TargetName = "", int Page = 1)
        {
            var container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "PanelPlayer");
            CuiHelper.DestroyUi(player, "RightPanel");
            CuiHelper.DestroyUi(player, "ReasonsPanel");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.11", AnchorMax = $"0.64 0.93" },
                Image = { Color = "0 0 0 0" },
            }, "Parent", "PanelPlayer");

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" },
                RectTransform = { AnchorMin = "0.65 0.4", AnchorMax = "1 1" }
            }, "Parent", "RightPanel");

            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.65 0", AnchorMax = "1 0.39" }
            }, "Parent", "ReasonsPanel");

            var target = BasePlayer.activePlayerList.Where(z => z.userID != player.userID && (z.displayName.ToLower().Contains(TargetName.ToLower()) || z.userID.ToString().Contains(TargetName))).OrderBy(x =>
            {
                int index = ReportInformation[player.userID].DeathList.IndexOf(x.userID);
                return index >= 0 && index <= 5 ? index : int.MaxValue;
            });

            float width = 0.488f, height = 0.106f, startxBox = 0f, startyBox = 0.991f - height, xmin = startxBox, ymin = startyBox;
            foreach (var i in Enumerable.Range(0, 16))
            {
                var item = target.Skip((Page - 1) * 16).Take(16).ElementAtOrDefault(i);
                if (item != null)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}" },
                        Button = { Color = "1 1 1 0.04", Command = $"reports add {item.userID}", Material = "assets/content/ui/uibackgroundblur.mat" },
                        Text = { Text = "" }
                    }, "PanelPlayer", $"Panel{i}");

                    xmin += width + 0.02f;
                    if (xmin + width >= 1)
                    {
                        xmin = startxBox;
                        ymin -= height + 0.02f;
                    }

                    if (ReportInformation[player.userID].ReportsList.Contains(item.userID))
                    {
                        container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = "0.89 0.3617443", AnchorMax = "0.9666671 0.6808972" },
                            Button = { Color = "0 0 0 0", Command = $"reports remove {item.userID}" },
                            Text = { Text = "" }
                        }, $"Panel{i}", "RemoveBTN");

                        container.Add(new CuiElement
                        {
                            Parent = "RemoveBTN",
                            Components =
                            {
                                new CuiImageComponent { Color = $"{HexToCuiColor("#e0947a", 100)}", Sprite = "assets/icons/close.png" },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "3 3", OffsetMax = "-3 -3" }
                            }
                        });
                    }

                    container.Add(new CuiElement
                    {
                        Parent = $"Panel{i}",
                        Components =
                        {
                            new CuiRawImageComponent{Color = "1 1 1 1", Png = GetImage(item.userID.ToString(), 0)},
                            new CuiRectTransformComponent{AnchorMin = "0.025 0.12", AnchorMax = "0.2 0.88"}
                        }
                    });

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.24 0.4361705", AnchorMax = "0.9 0.93", OffsetMax = "0 0" },
                        Text = { Text = $"{item.displayName}", Color = HexToCuiColor("#cec5bb", 100), Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-bold.ttf" }
                    }, $"Panel{i}");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.24 0.09574071", AnchorMax = "0.9 0.54", OffsetMax = "0 0" },
                        Text = { Text = $"{item.userID}", Color = HexToCuiColor("#7f7d7d", 100), Align = TextAnchor.MiddleLeft, FontSize = 8, Font = "robotocondensed-regular.ttf" }
                    }, $"Panel{i}");
                }
                else
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}" },
                        Button = { Color = "1 1 1 0.02", Command = $"", Material = "assets/content/ui/uibackgroundblur.mat" },
                        Text = { Text = "" }
                    }, "PanelPlayer", $"Panel{i}");

                    xmin += width + 0.02f;
                    if (xmin + width >= 1)
                    {
                        xmin = startxBox;
                        ymin -= height + 0.02f;
                    }
                }
            }

            float width1 = 1f, height1 = 0.12f, startxBox1 = 0f, startyBox1 = 0.97f - height1, xmin1 = startxBox1, ymin1 = startyBox1;
            if (ReportInformation[player.userID].ReportsList.Count() >= 1)
            {
                foreach (var check in Enumerable.Range(0, 5))
                {
                    var item = ReportInformation[player.userID].ReportsList.ElementAtOrDefault(check);
                    if (item != 0)
                    {
                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + height1 * 1}" },
                            Image = { Color = "1 1 1 0", Material = "assets/content/ui/uibackgroundblur.mat" }
                        }, "RightPanel", "ReportsList");

                        xmin1 += width1 + 0.02f;
                        if (xmin1 + width1 >= 1)
                        {
                            xmin1 = startxBox1;
                            ymin1 -= height1 + 0.02f;
                        }

                        container.Add(new CuiElement
                        {
                            Parent = "ReportsList",
                            Components =
                            {
                                new CuiRawImageComponent{Color = "1 1 1 1", Png = GetImage(item.ToString(), 0)},
                                new CuiRectTransformComponent{AnchorMin = "0.03 0.02702122", AnchorMax = "0.1939143 0.972964"}
                            }
                        });

                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0.22 0.4", AnchorMax = "0.8 1", OffsetMax = "0 0" },
                            Text = { Text = $"{BasePlayer.FindByID(item).displayName}", Color = HexToCuiColor("#cec5bb", 100), Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-bold.ttf" }
                        }, "ReportsList");

                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0.22 0", AnchorMax = "0.8 0.6", OffsetMax = "0 0" },
                            Text = { Text = $"{item}", Color = HexToCuiColor("#7f7d7d", 100), Align = TextAnchor.MiddleLeft, FontSize = 8, Font = "robotocondensed-regular.ttf" }
                        }, "ReportsList");

                        container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = $"0.8625675 0.27", AnchorMax = $"0.95 0.73" },
                            Button = { Color = $"{HexToCuiColor("#e0947a", 50)}", Command = $"reports remove {item}" },
                            Text = { Text = "" }
                        }, "ReportsList", "RemoveBTN");

                        container.Add(new CuiElement
                        {
                            Parent = "RemoveBTN",
                            Components =
                            {
                                new CuiImageComponent { Color = $"{HexToCuiColor("#e0947a", 100)}", Sprite = "assets/icons/close.png" },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "4 4", OffsetMax = "-4 -4" }
                            }
                        });
                    }
                    else
                    {
                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + height1 * 1}" },
                            Image = { Color = "0 0 0 0", Material = "assets/content/ui/uibackgroundblur.mat" }
                        }, "RightPanel", "ReportsList");

                        xmin1 += width1 + 0.02f;
                        if (xmin1 + width1 >= 1)
                        {
                            xmin1 = startxBox1;
                            ymin1 -= height1 + 0.03f;
                        }

                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                            Text = { Text = $"Можно выбрать ещё", Color = HexToCuiColor("#7f7d7d", 100), Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "robotocondensed-regular.ttf" }
                        }, "ReportsList");
                    }
                }
            }

            if (ReportInformation[player.userID].ReportsList.Count() == 0)
            {
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text = { Text = "Выберите игроков на\nкоторых хотите\nпожаловаться.", Color = HexToCuiColor("#7f7d7d", 100), FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                }, "RightPanel");
            }

            float width2 = 1f, height2 = 0.225f, startxBox2 = 0f, startyBox2 = 0.99f - height2, xmin2 = startxBox2, ymin2 = startyBox2;
            foreach (var check in _config.Reasons)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{xmin2} {ymin2}", AnchorMax = $"{xmin2 + width2} {ymin2 + height2 * 1}", OffsetMin = "0 0", OffsetMax = "0 0" },
                    Button = { Color = ReportInformation[player.userID].ReportsList.Count() >= 1 ? HexToCuiColor("#e0947a", 25) : "1 1 1 0.04", Command = ReportInformation[player.userID].ReportsList.Count() >= 1 ? $"reports send {check}" : "", Material = "assets/content/ui/uibackgroundblur.mat" },
                    Text = { Text = "" }
                }, "ReasonsPanel", "ReasonsList");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text = { Text = check, Color = ReportInformation[player.userID].ReportsList.Count() >= 1 ? HexToCuiColor("#e0947a", 100) : HexToCuiColor("#7f7d7d", 100), FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                }, "ReasonsList");

                xmin2 += width2;
                if (xmin2 + width2 >= 0)
                {
                    xmin2 = startxBox2;
                    ymin2 -= height2 + 0.03f;
                }
            }

            CuiHelper.AddUi(player, container);
            PagerUI(player, Page);
        }

        void PagerUI(BasePlayer player, int currentPage)
        {
            CuiHelper.DestroyUi(player, "footer");
            var container = new CuiElementContainer();
            List<string> displayedPages = GetDisplayedPages(player, currentPage);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.64 0.098" },
                Image = { Color = "0 0 0 0" }
            }, "Parent", "footer");

            float width = 0.13f, height = 1f, startxBox = 0f, startyBox = 1f - height, xmin = startxBox, ymin = startyBox;
            for (var z = 0; z < 7; z++)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMax = "0 0" },
                    Button = { Color = "1 1 1 0.01", Material = "assets/content/ui/uibackgroundblur.mat" },
                }, "footer");
                xmin += width + 0.0148f;
            }

            float x = 0f;
            for (int i = 0; i < displayedPages.Count; i++)
            {
                string page = displayedPages[i];

                if (page == "..")
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = $"{x} 0", AnchorMax = $"{x + 0.13f} 1", OffsetMax = "0 0" },
                        Text = { Text = $"..", Color = "1 1 1 0.2", Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-regular.ttf" }
                    }, "footer");
                }
                else
                {
                    int pageNum = int.Parse(page);
                    string buttonColor = pageNum == currentPage ? HexToCuiColor("#b2a9a3", 100) : "1 1 1 0.04";
                    string text = pageNum == currentPage ? $"<b><color=#45403b>{page}</color></b>" : $"{page}";

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = $"{x} 0", AnchorMax = $"{x + 0.13f} 1", OffsetMax = "0 0" },
                        Button = { Color = buttonColor, Command = $"reports page {page}", Material = "assets/content/ui/uibackgroundblur.mat" },
                        Text = { Text = text, Color = "1 1 1 0.2", Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-regular.ttf" }
                    }, "footer");
                }

                x += 0.145f;
            }

            CuiHelper.AddUi(player, container);
        }

        private List<string> GetDisplayedPages(BasePlayer player, int currentPage)
        {
            var result = new List<string>();
            var wItems = BasePlayer.activePlayerList.Count();
            int totalPages = (int)Math.Ceiling((decimal)wItems / (decimal)16);

            if (totalPages <= 7)
            {
                for (int i = 1; i <= totalPages; i++)
                {
                    result.Add(i.ToString());
                }
                return result;
            }

            result.Add("1");

            if (currentPage <= 4)
            {
                for (int i = 2; i <= 5; i++)
                {
                    result.Add(i.ToString());
                }
                result.Add("..");
                result.Add(totalPages.ToString());
            }
            else if (currentPage >= totalPages - 3)
            {
                result.Add("..");
                for (int i = totalPages - 4; i <= totalPages - 1; i++)
                {
                    result.Add(i.ToString());
                }
                result.Add(totalPages.ToString());
            }
            else
            {
                result.Add("..");
                for (int i = currentPage - 1; i <= currentPage + 1; i++)
                {
                    result.Add(i.ToString());
                }
                result.Add("..");
                result.Add(totalPages.ToString());
            }

            return result;
        }

        void ShowUIInforamtion(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Information_UI");
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.3", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, "Overlay", "Information_UI");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0.9", Close = "Information_UI" }
            }, "Information_UI");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.3 0.3", AnchorMax = "0.7 0.7", OffsetMax = "0 0" },
                Text = { Text = "<b><size=30>Небольшая памятка</size></b>\n\nЖалобу можно отправлять только раз в <b>15 минут</b>.\n\nЕсли ВЫ жалуетесь на нарушение лимита игроков в команде\n - обязательно выбирайте игроков, которыхподозреваете в\nнарушении, это очень упростит и ускорит их проверку.\n\nПожалуйста, ни при каких обстоятельствах не жалуйтесь на кого-либо\nв общем чате и ге просите кого-либо репортить (это в любом случае\nне ускорит проверку), общий чат видят в том числе и те на кого вы\nжалуетесь, а это очень усложнит процесс проверки.\n\n<color=#d1b283>Учтите, если игрок не заблокирован - это не значит что он не\nпроверен, не нужно спамить во все наши соц. сети, мы видим\nкаждый репорт и каждый репорт будет рассмотрен.</color>", Color = "1 1 1 0.2", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" }
            }, "Information_UI");

            CuiHelper.AddUi(player, container);
        }

        #endregion
    }
}
