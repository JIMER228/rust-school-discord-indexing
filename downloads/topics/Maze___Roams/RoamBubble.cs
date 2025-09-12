using Facepunch;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Roam Bubble", "Billy Joe", "1.0.3")]
    [Description("Starts a event that creates a dedicated PvP zone somewhere on the map and outputs results in chat/discord.")]
    public class RoamBubble : CovalencePlugin
    {
        #region Config
        public class Discord
        {
            [JsonProperty(PropertyName = "Use Discord Output?")] public bool useDiscord;
            [JsonProperty(PropertyName = "Discord Webhook URL")] public string discordWebhookURL;
            [JsonProperty(PropertyName = "Roam Winners Embed Title")] public string embedTitleWinners;
            [JsonProperty(PropertyName = "Roam Winners Embed Color")] public int embedColorWinners;
            [JsonProperty(PropertyName = "Roam No Winners Embed Title")] public string embedTitleNoWinners;
            [JsonProperty(PropertyName = "Roam No Winners Embed Color")] public int embedColorNoWinners;
            [JsonProperty(PropertyName = "Roam Called Embed Color")] public int embedColorRoamStart;
        }

        public class UI
        {
            [JsonProperty(PropertyName = "Use UI")] public bool useUI;
            [JsonProperty(PropertyName = "UI Title")] public string UITitle;
            [JsonProperty(PropertyName = "UI Title Color")] public string UITitleColor;
            [JsonProperty(PropertyName = "UI Title Font Size")] public int UITitleFontSize;
            [JsonProperty(PropertyName = "UI Timer Color")] public string UITimerColor;
            [JsonProperty(PropertyName = "UI Timer Font Size")] public int UITimerFontSize;
        }

        public class Messages
        {
            [JsonProperty(PropertyName = "Display Name for Chat Messages")] public string chatName;
            [JsonProperty(PropertyName = "Display Name Color for Chat Messages")] public string chatNameColor;
            [JsonProperty(PropertyName = "Steam ID Avatar for Chat Messages")] public ulong avatarSteamID;
            [JsonProperty(PropertyName = "Send Global Chat Message when roam is created?")] public bool sendChatMessageOnRoam;
            [JsonProperty(PropertyName = "Send Discord Message when roam is created?")] public bool sendDiscordMessageOnRoam;
            [JsonProperty(PropertyName = "Send End Result to Global Chat?")] public bool sendEndResultToChat;
            [JsonProperty(PropertyName = "Send End Result to Discord?")] public bool sendEndResultToDiscord;
        }

        static Configuration config;
        public class Configuration
        {
            [JsonProperty(PropertyName = "Use Clans")] public bool useClans;
            [JsonProperty(PropertyName = "Sphere Radius")] public int sphereRadius;
            [JsonProperty(PropertyName = "Amount of Spheres (More = Darker, although creates multiple sphere ents)")] public int numOfSpheres;
            [JsonProperty(PropertyName = "Message Settings")] public Messages messages;
            [JsonProperty(PropertyName = "UI Settings")] public UI ui;
            [JsonProperty(PropertyName = "Discord Settings")] public Discord discord;
            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    useClans = true,
                    sphereRadius = 150,
                    numOfSpheres = 7,
                    messages = new Messages
                    {
                        chatName = "Billy Joe's Roams",
                        chatNameColor = "#d4af37",
                        avatarSteamID = 76561198194158447,
                        sendChatMessageOnRoam = true,
                        sendDiscordMessageOnRoam = false,
                        sendEndResultToChat = true,
                        sendEndResultToDiscord = true
                    },
                    ui = new UI
                    {
                        useUI = true,
                        UITitle = "Billy Joe's Roams",
                        UITitleColor = "0 0.5988035 0.7529412 1",
                        UITitleFontSize = 30,
                        UITimerColor = "1 1 1 1",
                        UITimerFontSize = 22
                    },
                    discord = new Discord
                    {
                        useDiscord = true,
                        discordWebhookURL = "",
                        embedTitleWinners = "{0} Roam has finished, {1} have won the roam",
                        embedColorWinners = 3066993,
                        embedTitleNoWinners = "{0} Roam has finished, there were no winners",
                        embedColorNoWinners = 15158332,
                        embedColorRoamStart = 10181046
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
                SaveConfig();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Defines
        public static RoamBubble Instance;
        [PluginReference] private Plugin GridAPI, DiscordMessages, Clans;
        const string callroampermission = "roambubble.callroam";
        public List<RoamBubbleComp> activeRoams = new List<RoamBubbleComp>();
        public Dictionary<string, RoamBubbleComp> playersInBubble = new Dictionary<string, RoamBubbleComp>();
        public Dictionary<string, Timer> playerUITimers = new Dictionary<string, Timer>();
        #endregion

        #region Language
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["DiscordPluginNotRunning"] = "Tried to send discord message but discord messages plugin is not running on server.",
                ["NoPermission"] = "You dont have the permission to call roams.",
                ["NoTimeProvided"] = "You didn't provide a proper time for the roam, please use the following format (30s or 30m).",
                ["NoGridProvided"] = "You didn't provide a grid for the roam, please specify a grid on the map in the following format (A32).",
                ["InvalidTimeFormat"] = "You provided a invalid time format, please use the following format (30s or 30m).",
                ["InvalidGrid"] = "You provided a invalid grid, please use the following format (A30).",
                ["ActiveRoamOnGrid"] = "There is already an active roam on that grid, please chose another grid.",
                ["RoamStartedChat"] = "A roam has been started at {0}{1} for {2}{3}"
            }, this);
        }
        #endregion

        #region Hooks
        void Loaded()
        {
            Instance = this;
            permission.RegisterPermission(callroampermission, this);
        }
        void Unload()
        {
            foreach (RoamBubbleComp roamBubble in activeRoams.ToList())
            {
                UnityEngine.Object.DestroyImmediate(roamBubble);
            }
        }
        object OnEntityTakeDamage(BasePlayer victim, HitInfo info)
        {
            BasePlayer attacker = info?.InitiatorPlayer;
            if (attacker == null) return null;

            RoamBubbleComp roamBubble;
            if (!playersInBubble.TryGetValue(attacker.UserIDString, out roamBubble)) return null;

            RoamBubbleComp roamBubbleToCompare;
            if (!playersInBubble.TryGetValue(victim.UserIDString, out roamBubbleToCompare)) return null;
            if (roamBubble.roamGrid != roamBubbleToCompare.roamGrid) return null;

            ClanStats clanStats;
            string attackersClan = GetClanTag(attacker);

            if (!string.IsNullOrEmpty(attackersClan))
            {
                if (!roamBubble.clanStats.TryGetValue(attackersClan, out clanStats))
                {
                    List<string> members = GetClanMembers(attacker);
                    if (members.Contains(victim.UserIDString)) return null;

                    roamBubble.clanStats.Add(attackersClan, new ClanStats()
                    {
                        members = members,
                        kills = 0,
                        deaths = 0,
                        headshots = info.isHeadshot ? 1 : 0,
                        damage = Convert.ToInt32(info.damageTypes.Total())
                    });
                }
                else
                {
                    if (clanStats.members.Contains(victim.UserIDString)) return null;
                    if (info.isHeadshot) clanStats.headshots += 1;
                    clanStats.damage += Convert.ToInt32(info.damageTypes.Total());
                }
            }

            return null;
        }
        void OnEntityDeath(BasePlayer victim, HitInfo info)
        {
            BasePlayer attacker = info?.InitiatorPlayer;
            if (attacker == null || !attacker.userID.IsSteamId()) return;

            RoamBubbleComp roamBubble;
            if (!playersInBubble.TryGetValue(victim.UserIDString, out roamBubble)) return;

            RoamBubbleComp roamBubbleToCompare;
            if (!playersInBubble.TryGetValue(attacker.UserIDString, out roamBubbleToCompare)) return;
            if (roamBubble.roamGrid != roamBubbleToCompare.roamGrid) return;

            ClanStats clanStats;
            string attackersClan = GetClanTag(attacker);
            if (!string.IsNullOrEmpty(attackersClan))
            {
                if (roamBubble.clanStats.TryGetValue(attackersClan, out clanStats))
                {
                    if (!clanStats.members.Contains(victim.UserIDString))
                        clanStats.kills += 1;
                    else
                        return;
                }
            }

            string victimsClan = GetClanTag(victim);
            if (!string.IsNullOrEmpty(victimsClan))
            {
                if (!roamBubble.clanStats.TryGetValue(victimsClan, out clanStats))
                {
                    List<string> members = GetClanMembers(victim);
                    roamBubble.clanStats.Add(victimsClan, new ClanStats()
                    {

                        members = members,
                        kills = 0,
                        deaths = 1,
                        headshots = 0,
                        damage = 0
                    });
                }
                else
                    clanStats.deaths += 1;
            }

            if (playersInBubble.ContainsKey(victim.UserIDString))
            {
                playersInBubble.Remove(victim.UserIDString);

                Timer uiTimer;
                if (Instance.playerUITimers.TryGetValue(victim.UserIDString, out uiTimer))
                    uiTimer.Destroy();

                CuiHelper.DestroyUi(victim, "RoamHeaderText");
                CuiHelper.DestroyUi(victim, "TimeRemaining");
                playerUITimers.Remove(victim.UserIDString);
            }
        }
        #endregion

        #region Components
        public class ClanStats
        {
            public List<string> members = new List<string>();
            public int kills = 0;
            public int deaths = 0;
            public int damage = 0;
            public int headshots = 0;
        }

        public class RoamBubbleComp : MonoBehaviour
        {
            private SphereCollider innerCollider;
            private List<SphereEntity> innerSpheres = new List<SphereEntity>();
            public Dictionary<string, ClanStats> clanStats = new Dictionary<string, ClanStats>();
            public MapMarkerGenericRadius roamMarker = null;
            public string roamGrid = "";
            public float roamTime = 0f;

            void Awake()
            {
                gameObject.layer = (int)Layer.Reserved1;
                gameObject.name = "Roam Bubble";
                enabled = false;
            }

            void Update()
            {
                if (roamTime > 0)
                    roamTime -= Time.deltaTime;
                else
                {
                    KeyValuePair<string, ClanStats> winningClan = clanStats.Count > 0 ? clanStats.Aggregate((x, y) => x.Value.kills > y.Value.kills ? x : y) : default(KeyValuePair<string, ClanStats>);

                    bool isNull = winningClan.Equals(default(KeyValuePair<string, ClanStats>));
                    if (!isNull && winningClan.Value.kills == 0)
                        isNull = true;

                    if (isNull)
                    {
                        if (config.messages.sendEndResultToDiscord)
                            Instance.SendDiscordMessage(config.discord.discordWebhookURL, string.Format(config.discord.embedTitleNoWinners, roamGrid), config.discord.embedColorNoWinners, new Dictionary<string, string>()
                            {
                                { "No teams participated in this roam!", "To win a roam, your clan must have the most kills by the end of the roam." },
                                { "Timestamp:", DateTime.Now.ToString("g", CultureInfo.GetCultureInfo("en-US")) }
                            });

                        if (config.messages.sendEndResultToChat)
                            Instance.SendGlobalMessage($"{roamGrid} Roam is over, there were no winners due to no participants.");

                        DestroyImmediate(gameObject);
                        return;
                    }

                    StringBuilder members = new StringBuilder();
                    foreach (string pid in winningClan.Value.members)
                    {
                        BasePlayer ply = BasePlayer.FindAwakeOrSleeping(pid);
                        if (ply == null) members.Append(pid + "\n");
                        else members.Append(ply.displayName + "\n");
                    }

                    if (config.messages.sendEndResultToDiscord)
                        Instance.SendDiscordMessage(config.discord.discordWebhookURL, string.Format(config.discord.embedTitleWinners, roamGrid, winningClan.Key), config.discord.embedColorWinners, new Dictionary<string, string>()
                            {
                                { "Members:", $"{members}" },
                                { "Kills:", $"{winningClan.Value.kills}" },
                                { "Deaths:", $"{winningClan.Value.deaths}" },
                                { "Headshots:", $"{winningClan.Value.headshots}" },
                                { "Damage:", $"{winningClan.Value.damage}" },
                                { "Timestamp:", DateTime.Now.ToString("g", CultureInfo.GetCultureInfo("en-US")) }
                            });

                    if (config.messages.sendEndResultToChat)
                        Instance.SendGlobalMessage($"{roamGrid} Roam is over, {winningClan.Key} have won the roam.\n\nMembers:\n{members}\nKills: {winningClan.Value.kills}\nDeaths: {winningClan.Value.deaths}\nHeadshots: {winningClan.Value.headshots}\nDamage: {winningClan.Value.damage}");

                    DestroyImmediate(gameObject);
                }
            }

            void OnDestroy() => DeleteCircle();

            void OnTriggerEnter(Collider col)
            {
                BasePlayer player = col?.GetComponentInParent<BasePlayer>();
                if (player == null) return;
                Instance.playersInBubble.Add(player.UserIDString, this);

                if (config.ui.useUI)
                {
                    Instance.RoamUI(player);
                    Instance.RoamTimer(player, roamTime);
                    Instance.playerUITimers.Add(player.UserIDString, Instance.timer.Every(1f, () =>
                    {
                        Instance.RoamTimer(player, roamTime);
                    }));
                }
            }

            void OnTriggerExit(Collider col)
            {
                var player = col?.GetComponentInParent<BasePlayer>();
                if (player == null || !Instance.playersInBubble.ContainsKey(player.UserIDString)) return;
                Instance.playersInBubble.Remove(player.UserIDString);

                if (config.ui.useUI)
                {
                    Timer uiTimer;
                    if (Instance.playerUITimers.TryGetValue(player.UserIDString, out uiTimer))
                        uiTimer.Destroy();

                    Instance.playerUITimers.Remove(player.UserIDString);
                    CuiHelper.DestroyUi(player, "RoamHeaderText");
                    CuiHelper.DestroyUi(player, "TimeRemaining");
                }
            }

            public void CreateBubble(Vector3 position, float initialRadius, string grid, int time)
            {
                transform.position = position;
                transform.rotation = new Quaternion();
                roamTime = time;
                roamGrid = grid;

                for (int i = 0; i < config.numOfSpheres; i++)
                {
                    var sphere = (SphereEntity)GameManager.server.CreateEntity("assets/prefabs/visualization/sphere.prefab", position, new Quaternion(), true);
                    sphere.currentRadius = initialRadius * 2;
                    sphere.lerpSpeed = 0;
                    sphere.enableSaving = false;
                    sphere.Spawn();
                    innerSpheres.Add(sphere);
                }

                var innerRB = innerSpheres[0].gameObject.AddComponent<Rigidbody>();
                innerRB.useGravity = false;
                innerRB.isKinematic = true;

                innerCollider = gameObject.AddComponent<SphereCollider>();
                innerCollider.transform.position = innerSpheres[0].transform.position;
                innerCollider.isTrigger = true;
                innerCollider.radius = initialRadius;

                roamMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position) as MapMarkerGenericRadius;
                roamMarker.alpha = 0.6f;
                roamMarker.color1 = Color.red;
                roamMarker.color2 = Color.red;
                roamMarker.radius = initialRadius / 75;
                roamMarker.Spawn();
                roamMarker.SendUpdate();

                gameObject.SetActive(true);
                enabled = true;
                Instance.activeRoams.Add(this);
            }

            public void DeleteCircle()
            {
                foreach (SphereEntity sphere in innerSpheres)
                    sphere?.Kill();

                var list = Pool.GetList<KeyValuePair<string, RoamBubbleComp>>();
                list.AddRange(Instance.playersInBubble);

                foreach (KeyValuePair<string, RoamBubbleComp> kvp in list)
                {
                    if (kvp.Value == this)
                    {
                        Instance.playersInBubble.Remove(kvp.Key);

                        BasePlayer player = BasePlayer.Find(kvp.Key);
                        if (player == null) continue;

                        Timer uiTimer;
                        if (Instance.playerUITimers.TryGetValue(player.UserIDString, out uiTimer))
                            uiTimer.Destroy();

                        Instance.playerUITimers.Remove(player.UserIDString);
                        CuiHelper.DestroyUi(player, "RoamHeaderText");
                        CuiHelper.DestroyUi(player, "TimeRemaining");
                    }
                }

                Pool.FreeList(ref list);
                Instance.activeRoams.Remove(this);
                roamMarker?.Kill();
            }
        }
        #endregion

        #region Helper Functions
        private string GetClanTag(BasePlayer player)
        {
            if (config.useClans)
                return (string)Clans?.Call("GetClanOf", player);

            return !string.IsNullOrEmpty(player.Team?.GetLeader()?.displayName) ? $"{player.Team.GetLeader().displayName}'s Team" : string.Empty;
        }

        private List<string> GetClanMembers(BasePlayer player)
        {
            if (config.useClans)
                return (List<string>)Clans?.Call("GetClanMembers", player.UserIDString);

            if (player.Team != null)
                return player.Team.members.Select(x => $"{x}").ToList();

            return default(List<string>);
        }

        private void SendMessage(BasePlayer player, string key, params object[] args)
        {
            player.SendConsoleCommand("chat.add2", new object[] { 2, config.messages.avatarSteamID, string.Format(lang.GetMessage(key, this), args), config.messages.chatName, config.messages.chatNameColor, 1f });
        }

        private void SendGlobalMessage(string message)
        {
            ConsoleNetwork.BroadcastToAllClients("chat.add2", new object[] { 2, config.messages.avatarSteamID, message, config.messages.chatName, config.messages.chatNameColor, 1f });
        }

        private void SendDiscordMessage(string webhook, string embedName, int embedColor, Dictionary<string, string> values, string content = null)
        {
            if (!DiscordMessages) { Debug.LogWarning(lang.GetMessage("DiscordPluginNotRunning", this)); return; }

            object[] fields = new object[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                fields[i] = new
                {
                    name = values.Keys.ElementAt(i),
                    value = values.Values.ElementAt(i),
                    inline = false
                };
            }

            string json = JsonConvert.SerializeObject(fields);
            DiscordMessages?.Call("API_SendFancyMessage", webhook, embedName, embedColor, json, content, this);
        }
        #endregion

        #region Commands
        [Command("roam")]
        private void RoamCMD(IPlayer iPlayer, string command, string[] args)
        {
            if (iPlayer.IsServer) return;
            BasePlayer player = iPlayer.Object as BasePlayer;

            if (!permission.UserHasPermission(player.UserIDString, callroampermission)) { player.ChatMessage(lang.GetMessage("NoPermission", this)); return; }
            if (args.Length <= 0) { SendMessage(player, "NoTimeProvided"); return; }
            if (args.Length == 1) { SendMessage(player, "NoGridProvided"); return; }

            string fullTimeSring = args[0];
            if (fullTimeSring.Length <= 1) { SendMessage(player, "InvalidTimeFormat"); return; }

            char timeFormat = fullTimeSring[fullTimeSring.Length - 1];
            if (timeFormat != 'm' && timeFormat != 's') { SendMessage(player, "InvalidTimeFormat"); return; }

            int time = -1;
            if (!int.TryParse(fullTimeSring.Substring(0, fullTimeSring.Length - 1), out time)) { SendMessage(player, "InvalidTimeFormat"); return; }

            char[] gridResult = args[1].ToCharArray();
            if (gridResult.Length < 2) { SendMessage(player, "InvalidGrid"); return; }

            string gridLetter = "", gridNumber = "";
            foreach (char character in gridResult)
            {
                if (char.IsDigit(character))
                    gridNumber += character;
                else
                    gridLetter += character;
            }

            gridLetter = gridLetter.ToUpper();

            object result = GridAPI?.Call("MiddlePosFromGrid", gridLetter, gridNumber);
            if (result is string)
            {
                player.ChatMessage(result.ToString());
                return;
            }

            foreach (RoamBubbleComp roam in activeRoams)
                if (roam.roamGrid.Equals($"{gridLetter}{gridNumber}", StringComparison.OrdinalIgnoreCase))
                {
                    SendMessage(player, "ActiveRoamOnGrid");
                    return;
                }

            new GameObject().AddComponent<RoamBubbleComp>().CreateBubble((Vector3)result, config.sphereRadius, $"{gridLetter}{gridNumber}", timeFormat == 'm' ? (time * 60) : time);

            if (config.messages.sendChatMessageOnRoam)
                SendGlobalMessage(string.Format(lang.GetMessage("RoamStartedChat", this), gridLetter, gridNumber, time, timeFormat));

            if (config.messages.sendDiscordMessageOnRoam)
                SendDiscordMessage(config.discord.discordWebhookURL, "New Roam Started!", config.discord.embedColorRoamStart, new Dictionary<string, string>()
                {
                    { "Roam Time:", $"{time} {(timeFormat == 'm' ? "Minute(s)" : "Second(s)")}" },
                    { "Grid Location:", gridLetter + gridNumber },
                    { "Roam Called By:", $"[{player.displayName}](https://steamcommunity.com/profiles/{player.UserIDString})" },
                    { "Timestamp:", DateTime.Now.ToString("g", CultureInfo.GetCultureInfo("en-US")) }
                });
        }
        #endregion

        #region UI
        private void RoamUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "RoamHeaderText");
            var container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = "RoamHeaderText",
                Parent = "Overlay",
                Components = {
                    new CuiTextComponent { Text = config.ui.UITitle, Font = "robotocondensed-bold.ttf", FontSize = config.ui.UITitleFontSize, Align = TextAnchor.MiddleCenter, Color = config.ui.UITitleColor },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-183.827 -104.972", OffsetMax = "183.827 -65.97" }
                }
            });

            CuiHelper.AddUi(player, container);
        }

        private void RoamTimer(BasePlayer player, float time)
        {
            CuiHelper.DestroyUi(player, "TimeRemaining");
            var container = new CuiElementContainer();

            var duration = TimeSpan.FromSeconds(time);
            container.Add(new CuiElement
            {
                Name = "TimeRemaining",
                Parent = "Overlay",
                Components = {
                    new CuiTextComponent { Text = $"Time Left: {duration.Minutes.ToString("D2")}:{duration.Seconds.ToString("D2")}", Font = "robotocondensed-bold.ttf", FontSize = config.ui.UITimerFontSize, Align = TextAnchor.MiddleCenter, Color = config.ui.UITimerColor },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-183.827 -137.937", OffsetMax = "184.243 -99.463" }
                }
            });

            CuiHelper.AddUi(player, container);
        }
        #endregion
    }
}