using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Game.Rust.Cui;
using UnityEngine; 

namespace Oxide.Plugins
{
    [Info("Chat", "Hougan", "0.3.2")]
    public class Chat : RustPlugin
    {
        #region Classes

        private class Prefix
        {
            [JsonProperty("Наименование префикса")]
            public string Name;
            [JsonProperty("Цвет префикса")]
            public string Color;
            [JsonProperty("Размер префикса")]
            public string Size;

            [JsonProperty("Скобки префикса")]
            public string Hooks;
        }

        private class Chatter
        {
            [JsonProperty("Подсказки")]
            public bool Tips;
            [JsonProperty("Звуки личных сообщений")]
            public bool Sound;
            [JsonProperty("Звук сообщений в чате")]
            public bool Censor;
            [JsonProperty("Сообщения в чат")] 
            public bool Chat = true;
            [JsonProperty("Сообщения в ПМ")] 
            public bool PM = true;
        }

        private class Name
        {
            public string Color;
        }
        
        private class Settings
        {
            [JsonProperty("Настройки текущего префикса")]
            public Prefix Prefixes;
            [JsonProperty("Настройки чата")]
            public Chatter Chatters;
            [JsonProperty("Настройки имени")]
            public Name Names;

            [JsonProperty("Список игнорируемых игроков")] 
            public Dictionary<ulong, string> IgnoreList = new Dictionary<ulong, string>();
            [JsonProperty("Последнее личное сообщение для"), JsonIgnore] 
            public BasePlayer ReplyTarget = null;

            public double UMT; 
            
            public Settings() {}
            public static Settings Generate()
            {
                return new Settings
                {
                    Prefixes = new Prefix
                    {
                        Name = "-",
                        Hooks = "-", 
                        Size = "<size=14>",
                        Color = "<color=#c692de>"
                    },
                    Chatters = new Chatter
                    {
                        Censor = false,
                        Sound = true, 
                        Tips = true
                    },
                    Names = new Name
                    {
                        Color = "<color=#c692de>"
                    }
                }; 
            } 
        }

        private class DataBase
        {
            public Dictionary<ulong, Settings> Settingses = new Dictionary<ulong, Settings>();

            public static DataBase LoadData() => Interface.Oxide.DataFileSystem.ExistsDatafile("Chat") ? Interface.Oxide.DataFileSystem.ReadObject<DataBase>("Chat") : new DataBase();

            public void SaveData()
            {
                Interface.Oxide.DataFileSystem.WriteObject("Chat", this);
                Interface.Oxide.DataFileSystem.WriteObject($"Logs", MessagesLogs);
            } 
        }

        private class Configuration
        {
            [JsonProperty("Web Settings")]
            public WSettings WebSettings = new WSettings
            {
                SecretKey = "UNKNOWN",
                WebChatURI = "http://89.223.122.156:2012"
            }; 
            
            [JsonProperty("Список доступных префиксов")]
            public Dictionary<string, string> Prefixes = new Dictionary<string, string>();
            [JsonProperty("Список доступных цветов")]
            public Dictionary<string, string> Colors = new Dictionary<string, string>();
            [JsonProperty("Список доступных размеров")]
            public Dictionary<string, string> Sizes = new Dictionary<string, string>();
            [JsonProperty("Список доступных типов префикса")]
            public Dictionary<string, string> Types = new Dictionary<string, string>();
            [JsonProperty("Список цензуры и исключений")]
            public Dictionary<string, List<string>> Censures = new Dictionary<string, List<string>>(); 
            [JsonProperty("Список доступных сообщений")]
            public List<string> Broadcaster = new List<string>();
            [JsonProperty("Интервал отправки сообщений")]
            public int BroadcastInterval = 300;
            [JsonProperty("Оповещать о подключении с префиксом")]
            public bool WelcomePrefix = true;

            [JsonProperty("SteamID отправителя в чат")] 
            public ulong ImageID = 76561199039326412;
            
            public static Configuration LoadDefaultConfiguration()
            {
                return new Configuration
                {
                    WelcomePrefix = true,
                    BroadcastInterval = 1,
                    Broadcaster = new List<string>
                    {
                        "Привет, я дурачёк",
                        "Я пишу этот плагин",
                        "Я работаю на двух работах",
                        "РаботатьРаботуРаботуРаботатьРаботатьРаботу"
                    },
                    Prefixes = new Dictionary<string, string>
                    {
                        ["Chat.Default"] = "НЕТ:-",
                        ["Chat.Vip"] = "КАБАНЧИК:КАБАНИДЗЕЕБАТЬ"
                    },
                    Colors = new Dictionary<string, string>
                    {
                        ["Chat.Default"] = "СТАНДАРТНЫЙ:<color=#a1d352>", 
                        ["Chat.White"] = "БЕЛЫЙ:<color=white>", 
                        ["Chat.Orange"] = "ОРАНЖЕВЕНЬКИЙ:<color=orange>",
                        ["Chat.Call"] = "КАКАШЕЧНЫЙ:<color=red>",
                    },
                    Sizes = new Dictionary<string, string>
                    {
                        ["Chat.Default"] = "СТАНДАРТНЫЙ:<size=14>",
                        ["Chat.Big"] = "БОЛЬШОЙ:<size=16>"
                    },
                    Types = new Dictionary<string, string>
                    {
                        ["Chat.Default"] = "НЕТ:-",
                        ["Chat.Hooks"] = "СКОБКИ:[]",
                        ["Chat.Limitter"] = "ПОЛОСА:|"
                    }
                };
            }
        }
        
        #endregion

        #region Variables

        private static Chat _;
        private static DataBase Handler;
        private static Configuration Settingses;
        
        private static List<ulong> AntiSpamFilter = new List<ulong>();

        #endregion

        #region Initialization

        private void Unload() => Handler.SaveData();
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                Settingses = Config.ReadObject<Configuration>();
            }
            catch
            {
                PrintWarning($"Error reading config, creating one new config!");
                LoadDefaultConfig();
            }

            SaveConfig();
        } 

        protected override void LoadDefaultConfig() => Settingses = Configuration.LoadDefaultConfiguration();
        protected override void SaveConfig()        => Config.WriteObject(Settingses); 
        
        private void OnServerInitialized()
        {
            _ = this;
            //BasePlayer.activePlayerList.ToList().ForEach(OnPlayerConnected); 

            Handler = DataBase.LoadData();
             
            BasePlayer.activePlayerList.ToList().ForEach((player) =>
            {
                if (!Handler.Settingses.ContainsKey(player.userID))
                    Handler.Settingses.Add(player.userID, Settings.Generate());
            });  
            
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("Logs"))
                MessagesLogs = Interface.Oxide.DataFileSystem.ReadObject<HashSet<Message>>($"Logs");
            if (MessagesLogs.Count > 1000)
            {
                MessagesLogs.Clear();
                PrintError("Data messages was cleared!"); 
            } 
              
            Settingses.Colors.ToList().ForEach(p => { if (!permission.PermissionExists(p.Key) && p.Key.ToLower().StartsWith("chat")) permission.RegisterPermission(p.Key, this);});
            Settingses.Types.ToList().ForEach(p => { if (!permission.PermissionExists(p.Key) && p.Key.ToLower().StartsWith("chat")) permission.RegisterPermission(p.Key, this);});
            Settingses.Sizes.ToList().ForEach(p => { if (!permission.PermissionExists(p.Key) && p.Key.ToLower().StartsWith("chat")) permission.RegisterPermission(p.Key, this);});
            Settingses.Prefixes.ToList().ForEach(p => { if (!permission.PermissionExists(p.Key) && p.Key.ToLower().StartsWith("chat")) permission.RegisterPermission(p.Key, this);});

            timer.Every(Settingses.BroadcastInterval, () =>
            {
                var message = Settingses.Broadcaster.GetRandom();
                foreach (var check in BasePlayer.activePlayerList.ToList())
                {
                    var settings = Handler.Settingses[check.userID];
                    if (!settings.Chatters.Tips)
                        continue;

                    check.SendConsoleCommand("chat.add", 0, Settingses.ImageID, message);
                    check.SendConsoleCommand($"echo [<color=white>ЧАТ</color>] {message}");
                }
            }).Callback();

            Settingses.WebSettings.Validate();
            timer.Every(10, AntiSpamFilter.Clear);
            timer.Every(60, Handler.SaveData);
            timer.Every(300, () => BasePlayer.activePlayerList.ToList().ForEach(p => FetchStatus(p)));    
        }
 
        #endregion

        #region Commands

        [ConsoleCommand("UI_Chat")]
        private void CmdConsoleHandler(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!player || !arg.HasArgs(1)) return;

            switch (arg.Args[0].ToLower())
            {
                case "chatpm":
                {
                    Handler.Settingses[player.userID].Chatters.PM = Convert.ToBoolean(arg.Args[1]);
                    InitializeInterface(player); 
                    break;
                }
                case "chatglobal":
                {
                    Handler.Settingses[player.userID].Chatters.Chat = Convert.ToBoolean(arg.Args[1]);
                    InitializeInterface(player); 
                    break;
                }
                case "censor":
                {
                    Handler.Settingses[player.userID].Chatters.Censor = Convert.ToBoolean(arg.Args[1]);
                    InitializeInterface(player); 
                    break;
                }
                case "sound":
                {
                    Handler.Settingses[player.userID].Chatters.Sound = Convert.ToBoolean(arg.Args[1]);
                    InitializeInterface(player); 
                    break;
                }
                case "tips":
                {
                    Handler.Settingses[player.userID].Chatters.Tips = Convert.ToBoolean(arg.Args[1]);
                    InitializeInterface(player); 
                    break;
                }
                case "name_color": 
                {
                    var nameColor = Settingses.Colors.FirstOrDefault(p => p.Value == arg.Args[1]);
                    if (!permission.UserHasPermission(player.UserIDString, nameColor.Key)) return;

                    Handler.Settingses[player.userID].Names.Color = nameColor.Value.Split(':')[1];
                    InitializeInterface(player); 
                    break;
                }
                case "prefix_color":
                {
                    var nameColor = Settingses.Colors.FirstOrDefault(p => p.Value == arg.Args[1]);
                    if (!permission.UserHasPermission(player.UserIDString, nameColor.Key)) return;

                    Handler.Settingses[player.userID].Prefixes.Color = nameColor.Value.Split(':')[1];
                    InitializeInterface(player); 
                    break;
                }
                case "prefix_size":
                {
                    var nameColor = Settingses.Sizes.FirstOrDefault(p => p.Value == arg.Args[1]);
                    if (!permission.UserHasPermission(player.UserIDString, nameColor.Key)) return;

                    Handler.Settingses[player.userID].Prefixes.Size = nameColor.Value.Split(':')[1];
                    InitializeInterface(player); 
                    break;
                }
                case "hook_type":
                {
                    var nameColor = Settingses.Types.FirstOrDefault(p => p.Value == arg.Args[1]);
                    if (!permission.UserHasPermission(player.UserIDString, nameColor.Key)) return;

                    Handler.Settingses[player.userID].Prefixes.Hooks = nameColor.Value.Split(':')[1];
                    InitializeInterface(player); 
                    break;
                }
                case "prefix":
                {
                    var nameColor = Settingses.Prefixes.FirstOrDefault(p => p.Value == arg.Args[1]);
                    if (!permission.UserHasPermission(player.UserIDString, nameColor.Key)) return;

                    Handler.Settingses[player.userID].Prefixes.Name = nameColor.Value.Split(':')[1];
                    InitializeInterface(player); 
                    break;
                }
            }
        }

        #endregion

        #region Hooks
  
        private class Response
        {
            [JsonProperty("country")]
            public string Country { get; set; }
        }
        
        void OnPlayerCorpse(BasePlayer player, BaseCorpse corpse)
        {
            if (!Handler.Settingses.ContainsKey(player.userID)) return;

            var obj = corpse.GetComponent<PlayerCorpse>();
            if (obj == null) return;
            obj._playerName = PrepareNick(player);
        }

        private void FetchStatus(BasePlayer player)
        {
            if (!Handler.Settingses.ContainsKey(player.userID))
            {
                OnPlayerConnected(player);
                FetchStatus(player);
                return;
            }
            
            var settings = Handler.Settingses[player.userID];
            
            if (Settingses.Prefixes.All(p => !p.Value.Contains(settings.Prefixes.Name)) || !permission.UserHasPermission(player.UserIDString, Settingses.Prefixes.FirstOrDefault(p => p.Value.Contains(settings.Prefixes.Name)).Key))
            {
                settings.Prefixes.Name = Settingses.Prefixes.FirstOrDefault(p => permission.UserHasPermission(player.UserIDString, p.Key)).Value.Split(':')[1]; 
            }
            if (Settingses.Colors.All(p => !p.Value.Contains(settings.Prefixes.Color)) || !permission.UserHasPermission(player.UserIDString, Settingses.Colors.FirstOrDefault(p => p.Value.Contains(settings.Prefixes.Color)).Key))
            {
                settings.Prefixes.Color = Settingses.Colors.FirstOrDefault(p => permission.UserHasPermission(player.UserIDString, p.Key)).Value.Split(':')[1]; 
            }
            if (Settingses.Types.All(p => !p.Value.Contains(settings.Prefixes.Hooks)) || !permission.UserHasPermission(player.UserIDString, Settingses.Types.FirstOrDefault(p => p.Value.Contains(settings.Prefixes.Hooks)).Key))
            {
                 settings.Prefixes.Hooks = Settingses.Types.FirstOrDefault(p => permission.UserHasPermission(player.UserIDString, p.Key)).Value.Split(':')[1]; 
            } 
            if (Settingses.Sizes.All(p => !p.Value.Contains(settings.Prefixes.Size)) || !permission.UserHasPermission(player.UserIDString, Settingses.Sizes.FirstOrDefault(p => p.Value.Contains(settings.Prefixes.Size)).Key))
            {
                settings.Prefixes.Size = Settingses.Sizes.FirstOrDefault(p => permission.UserHasPermission(player.UserIDString, p.Key)).Value.Split(':')[1]; 
            }
            if (Settingses.Colors.All(p => !p.Value.Contains(settings.Names.Color)) || !permission.UserHasPermission(player.UserIDString, Settingses.Colors.FirstOrDefault(p => p.Value.Contains(settings.Names.Color)).Key))
            { 
                settings.Names.Color = Settingses.Colors.FirstOrDefault(p => permission.UserHasPermission(player.UserIDString, p.Key)).Value.Split(':')[1]; 
            } 
        }
        
        private class WSettings 
        {
            [JsonProperty("Основная ссылка для запросов")]
            public string WebChatURI = "http://89.223.122.156:2012"; 
            [JsonProperty("Секретный ключ доступа")] 
            public string SecretKey = "6dfc7eaa-d736-4097-a837-e3150f307655";   

            public void Validate() => WebBuffer.RequestOut.ValidateConnect(); 
        }
        
        private void OnPlayerConnected(BasePlayer player)
        {
            if (!Handler.Settingses.ContainsKey(player.userID))
                Handler.Settingses.Add(player.userID, Settings.Generate());
            
            FetchStatus(player); 
            
            string apiUrl = "http://ip-api.com/json/";

            webrequest.Enqueue(apiUrl + player.net.connection.ipaddress.Split(':')[0], null, (code, response) =>
            {
                if (code != 200 || response == null) return;

                string country = JsonConvert.DeserializeObject<Response>(response).Country;
                foreach (var check in BasePlayer.activePlayerList.ToList())
                { 
                    //check.SendConsoleCommand("chat.add", 0, "userId, $"<size=12>Игрок{_.PrepareNickForConnect(player)} присоединился!</size>");
                }
            }, this);
        }

        private static HashSet<Message> MessagesLogs = new HashSet<Message>();
        
        #region Helpers

        private void Broadcast(string text)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (!Handler.Settingses.ContainsKey(player.userID))
                    continue;
                    
                var targetSettings = Handler.Settingses[player.userID];
                if (!targetSettings.Chatters.Tips) continue;
                
                SafeMessage(player, text, Settingses.ImageID);
            }
        }
        private void MutePlayer(BasePlayer player, string initiatorName, int time, string reason)
        {
            var settings = Handler.Settingses[player.userID];

            if (time == 0)
            {
                Broadcast($"<color=#90e095>{initiatorName}</color> разблокировал чат игроку <color=#e68585>{player.displayName}</color>");
                Handler.Settingses[player.userID].UMT = 0;
            }
            else 
            {
                Broadcast($"<color=#90e095>{initiatorName}</color> выдал мут игроку <color=#e68585>{player.displayName}</color>\n" +
                          $"  <size=12><color=#e3e3e3>Причина: {reason} [{TimeSpan.FromSeconds(time).ToShortString()}]</color></size>");
                Handler.Settingses[player.userID].UMT = Time() + time;
            }
        }

        private void SendPrivateMessage(BasePlayer initiator, BasePlayer target, string message)
            => SendPrivateMessage(initiator.userID, target.userID, message);

        private void SendPrivateMessage(ulong initiatorReadyId, ulong targetReadyId, string message)
        {
            BasePlayer initiator = BasePlayer.FindByID(initiatorReadyId);
            BasePlayer target    = BasePlayer.FindByID(targetReadyId);

            string targetReadyName                        = BasePlayer.FindByID(targetReadyId)?.displayName ?? "UNKNOWN";
            string initiatorReadyName                     = BasePlayer.FindByID(initiatorReadyId)?.displayName ?? "UNKNOWN";
            if (initiatorReadyId == 76561199039326412) initiatorReadyName = "ADMIN";

            if (target == null || !target.IsConnected)
            {
                SafeMessage(initiator, "Игрок не находится на сервере!");
                return;
            }

            var targetSettings = Handler.Settingses[targetReadyId];
            if (!targetSettings.IgnoreList.ContainsKey(initiatorReadyId))
            {
                if (!targetSettings.Chatters.PM)
                {
                    initiator.ChatMessage("У игрока отключены приватные сообщения");
                    return;
                }
                if (targetSettings.Chatters.Sound)
                {
                    Effect effect = new Effect("assets/bundled/prefabs/fx/notice/item.select.fx.prefab", target, 0, new Vector3(), new Vector3());
                    EffectNetwork.Send(effect, target.Connection);
                }

                if (initiator != null && initiator.IsConnected)
                {
                    targetSettings.ReplyTarget = initiator;
                }

                string prepareName = PrepareNick(new BasePlayer {userID = initiatorReadyId, displayName = initiatorReadyName});
                SafeMessage(target, $"<size=14>Личное сообщение от {prepareName}</size>\n<size=12>{message}</size>", initiatorReadyId);
            }
 
            string prepareTargetName = PrepareNick(new BasePlayer {userID = targetReadyId, displayName = targetReadyName});
            SafeMessage(initiator, $"<size=14>Личное сообщение для {prepareTargetName}</size>\n<size=12>{message}</size>", targetReadyId);
            AddMessage(initiatorReadyName, initiatorReadyId, message, targetReadyName, targetReadyId);
            
            DebugEx.Log((object) "[CHAT] " + initiatorReadyName + $" [{initiatorReadyId}] : > {target.displayName} [{target.userID}] :" + message); 
        }
        private class WebBuffer
        {
            internal class RequestIn
            {
                [JsonProperty("requestType")]   public string                     Type   = "UNKNOWN";
                [JsonProperty("requestParams")] public Dictionary<string, object> Params = new Dictionary<string, object>();

                public void ExecuteRequest()
                {
                    switch (Type)
                    {
                        case "in_userMuteRequest":
                        {
                            if (!Params.ContainsKey("id") || !Params.ContainsKey("targetId") || !Params.ContainsKey("time") || !Params.ContainsKey("reason"))
                            {
                                _.PrintError($"Неправильные аргументы [{Type}]");
                                foreach (var arg in Params)
                                    _.PrintError($" - {arg.Key} -> {arg.Value}");
                                return;
                            }

                            string id       = Params["id"].ToString();
                            string targetId = Params["targetId"].ToString();
                            int    time     = int.Parse(Params["time"].ToString());
                            string reason   = Params["reason"].ToString();

                            RequestOut.SendUserMuted(id, targetId, time, reason);
                            break;
                        }
                        case "in_userInfoRequest":
                        {
                            if (!Params.ContainsKey("id") || !Params.ContainsKey("targetId"))
                            {
                                _.PrintError($"Неправильные аргументы [{Type}]");
                                foreach (var arg in Params)
                                    _.PrintError($" - {arg.Key} -> {arg.Value}");
                                return;
                            }

                            string id       = Params["id"].ToString();
                            string targetId = Params["targetId"].ToString();

                            RequestOut.SendUserInfo(id, targetId);
                            break;
                        }
                        case "in_chatHistoryRequest":
                        {
                            if (!Params.ContainsKey("id") || !Params.ContainsKey("amount"))
                            {
                                _.PrintError($"Неправильные аргументы [{Type}]");
                                foreach (var arg in Params)
                                    _.PrintError($" - {arg.Key} -> {arg.Value}");
                                return;
                            }

                            string id     = Params["id"].ToString();
                            int    amount = int.Parse(Params["amount"].ToString());

                            RequestOut.SendLogs(id, amount);
                            break;
                        }
                        case "in_sendMessageRequest":
                        {
                            if (!Params.ContainsKey("id") || !Params.ContainsKey("message"))
                            {
                                _.PrintError($"Неправильные аргументы [{Type}]");
                                foreach (var arg in Params)
                                    _.PrintError($" - {arg.Key} -> {arg.Value}");
                                return;
                            }

                            string id       = Params["id"].ToString();
                            string targetId = string.Empty;
                            if (Params.ContainsKey("target") && Params["target"].ToString() != string.Empty)
                                targetId = Params["target"].ToString();

                            string message = Params["message"].ToString();

                            if (targetId == string.Empty)
                            {
                                _.Broadcast($"<color=#90e095>ADMIN</color>: " + message); 
                                _.AddMessage("ADMIN", 76561199039326412, message);
                            }
                            else
                            {
                                _.SendPrivateMessage(76561199039326412, ulong.Parse(targetId), message);
                            }

                            RequestOut.SendMessageGlobal(id);
                            break;
                        }
                    }
                }
            }

            internal class RequestOut
            {
                [JsonProperty("secretKey")]     public string                     SecretKey = Settingses.WebSettings.SecretKey;
                [JsonProperty("requestType")]   public string                     Type      = "UNKNOWN";
                [JsonProperty("requestParams")] public Dictionary<string, object> Params    = new Dictionary<string, object>();

                public static void ValidateConnect()
                {
                    RequestOut request = new RequestOut();

                    request.Type = "out_validateVersion";

                    request.Params.Add("id", "123456789");
                    request.Params.Add("version", _.Version.ToString());

                    SendRequest(request, result =>
                    {
                        if (result.Length == 0)
                        {
                            _.PrintWarning("Подключение к веб-интерфейсу успешно!");
                            _.timer.Every(1, GetRequests);
                        }
                        else
                        {
                            _.PrintError(result);
                            //UnloadPlugin();
                        }
                    });
                }

                public static void GetRequests()
                {
                    RequestOut request = new RequestOut();

                    request.Type = "out_requestListRequest";
                    request.Params.Add("id", "123456789");
                    request.Params.Add("fps", Performance.report.frameRate);
                    request.Params.Add("online", $"{BasePlayer.activePlayerList.Count}/{ConVar.Server.maxplayers}");

                    SendRequest(request, result =>
                    {
                        try
                        {
                            var requestIn = JsonConvert.DeserializeObject<RequestIn>(result);
                            requestIn.ExecuteRequest();
                        }
                        catch
                        {
                            // IGNORE
                        }
                    });
                } 

                public static void SendMessage(Message message)
                {
                    RequestOut request = new RequestOut();

                    request.Type   = "out_sendMessageRequest";
                    request.Params = new Dictionary<string, object>();
                    
                    request.Params.Add("id", "123456789");
                    request.Params.Add("message", message);

                    SendRequest(request, null);
                }

                public static void SendUserMuted(string requestId, string targetId, int time, string reason = "")
                {
                    RequestOut request = new RequestOut(); 

                    request.Type   = "out_userMuteAnswer";
                    request.Params = new Dictionary<string, object>();

                    var mutePlayer = BasePlayer.Find(targetId);

                    request.Params.Add("id", requestId);
                    request.Params.Add("result", mutePlayer != null);

                    if (mutePlayer != null) _.MutePlayer(mutePlayer, "Модератор", time, reason);
                    SendRequest(request, null);
                }

                public static void SendUserInfo(string requestId, string targetId)
                {
                    RequestOut request = new RequestOut();

                    request.Type   = "out_userInfoAnswer";
                    request.Params = new Dictionary<string, object>();

                    ulong parsedId = ulong.Parse(targetId);
                    
                    request.Params.Add("id", requestId);
                    request.Params.Add("info", Handler.Settingses.ContainsKey(parsedId) ? Handler.Settingses[parsedId] : null); 
                    request.Params.Add("messages", FindMessage(targetId));

                    SendRequest(request, null);
                }

                public static void SendLogs(string requestId, int amount)
                {
                    RequestOut request = new RequestOut();

                    request.Type   = "out_chatHistoryAnswer";
                    request.Params = new Dictionary<string, object>();

                    var listMessages = MessagesLogs.Take(amount);
                    
                    request.Params.Add("id", requestId);
                    request.Params.Add("messages", listMessages);

                    SendRequest(request, null);
                }

                public static void SendMessageGlobal(string requestId)
                {
                    RequestOut request = new RequestOut();

                    request.Type   = "out_sendMessageAnswer";
                    request.Params = new Dictionary<string, object>();

                    request.Params.Add("id", requestId);
                    request.Params.Add("result", true);

                    SendRequest(request, null);
                }
            }

            public static void SendRequest(RequestOut @out, Action<string> callback = null)
            {
                LogInfo($"Создание запроса: {@out.Type} [{Settingses.WebSettings.WebChatURI}]");
                _.webrequest.Enqueue(Settingses.WebSettings.WebChatURI + "/api/request", JsonConvert.SerializeObject(@out), (i, s) =>
                {
                    LogInfo($"Полученный результат: '{s}'"); 
                    
                    callback?.Invoke(s);
                }, _, RequestMethod.POST, new Dictionary<string, string> {["Content-Type"] = "application/json"});
            }
        }

        public class Message
        {
            [JsonProperty("id")] public string Id = CuiHelper.GetGuid();
            
            [JsonProperty("displayName")] public string DisplayName;
            [JsonProperty("userId")]      public string UserID;

            [JsonProperty("targetDisplayName")] public string TargetDisplayName;
            [JsonProperty("targetUserId")]      public string TargetUserId;

            [JsonProperty("text")]   public string Text;
            [JsonProperty("isTeam")] public bool   IsTeam;
            [JsonProperty("time")]   public string Time;
        }
        private void AddMessage(BasePlayer player, string message, bool team = false) => AddMessage(player.displayName, player.userID, message, team: team);

        private void AddMessage(string displayName, ulong userId, string message, string targetName = "", ulong targetId = 0UL, bool team = false)
        {
            var time       = DateTime.Now;
            var resultTime = $"{time.Hour}:{time.Minute}:{time.Second}";

            var chat = new Message
            {
                DisplayName       = displayName,
                Text              = message,
                TargetDisplayName = targetName,
                TargetUserId      = targetId.ToString(),
                Time              = resultTime,
                UserID            = userId.ToString(),
                IsTeam            = team
            };

            MessagesLogs.Add(chat);
            WebBuffer.RequestOut.SendMessage(chat);
            //PrintError("123");

            string logFormat = $"{displayName} [{targetId}]: {message}";
            if (team)
            {
                logFormat = "TEAM: " + logFormat;
            }
            else if (targetId != 0)
            {
                logFormat = $"{displayName} [{userId}] -> {targetName}[{targetId}]: {message}";
            }

            LogToFile($"{(!team ? targetId == 0 ? "Chat" : "PM" : "Team")}", logFormat, this);
        }

        #endregion

        #region Utils

        private static void LogInfo(string text)
        {
            //if (Settings) 
            //{
               // _.PrintWarning(text);
            //}
        }
        private static void UnloadPlugin() => _.NextTick(() => Interface.Oxide.UnloadPlugin("Chat"));
        private static List<Message> FindMessage(string input, int amount = 100)
        {
            return MessagesLogs.Where(p => p.DisplayName.ToLower().Contains(input.ToLower()) || p.UserID.ToString() == input || p.Text.ToLower().Contains(input)).Take(amount).ToList();
        }

        private static double Time() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

        private static void SafeMessage(BasePlayer player, string text, ulong avatarId = 0)
        {
            if (player == null || !player.IsConnected) return;

            player.SendConsoleCommand("chat.add", 0, avatarId, text);
            player.SendConsoleCommand($"echo <color=white>[ЧАТ]</color> {text}");
        }

        #endregion
 
        private object OnPlayerChat(BasePlayer player, string message, ConVar.Chat.ChatChannel channel)
        {
            // TODO: HotFix
            while (message.Contains("size"))
                message = message.Replace("size", "");

            if (channel == ConVar.Chat.ChatChannel.Global)
            {
                if (AntiSpamFilter.Contains(player.userID) && !player.IsAdmin)
                {
                    player.ChatMessage("Вы не можете писать чаще, чем раз в <color=#fd7d6b>10</color> секунд!\n<size=12>Старайтесь писать всё в одном сообщении.</size>");
                    return false;
                }

                if (Handler.Settingses[player.userID].UMT > Time())
                {
                    player.ChatMessage("Ссори, у вас мут!");
                    return false;
                }

                string realMessage = PrepareMessage(player, message);
                string censureMessage = PrepareMessage(player, Censure(message));
                
                AntiSpamFilter.Add(player.userID); 
                foreach (var check in BasePlayer.activePlayerList.ToList())
                {
                    var settings = Handler.Settingses[check.userID];
                    if (settings.IgnoreList.ContainsKey(player.userID) || !settings.Chatters.Chat)
                        continue;
                    
                    string prepareMessagge = settings.Chatters.Censor ? censureMessage : realMessage; 
                    check.SendConsoleCommand("chat.add", 0, player.userID, prepareMessagge);
                    check.SendConsoleCommand($"echo [<color=white>ЧАТ</color>] {prepareMessagge}");
                }
            
                DebugEx.Log((object) ("[CHAT] " + player.displayName + $" [{player.UserIDString}] : " + message));
                LogChat(player.displayName + $" [{player.UserIDString}]: " + message);
                AddMessage(player, message);
                return false;
            }
            else
            {
                foreach (var check in player.Team.members)
                {
                    var settings = Handler.Settingses[player.userID]; 
                    if (settings.IgnoreList.ContainsKey(player.userID))
                        continue; 

                    var target = BasePlayer.FindByID(check);
                    if (target == null || !target.IsConnected) continue;
                    
                    string prepareMessagge = PrepareMessage(player, message, true);
                    target.SendConsoleCommand("chat.add", 0, player.userID, prepareMessagge);
                    target.SendConsoleCommand($"echo [<color=white>TEAM</color>] {prepareMessagge}");
                }
            
                DebugEx.Log((object) ("[CHAT] " + player.displayName + $" [{player.UserIDString}] : " + "TEAM> " + message));
                LogChat(player.displayName + $" [{player.UserIDString}]: " + message);
                AddMessage(player, message, true); 
                return false;
            }
            return null;
        }

        #endregion

        #region Commands

        [ChatCommand("chatSecret")]
        private void CmdChatCommandSecret(BasePlayer player, string command, string[] args) => InitializeInterface(player);
        [ChatCommand("chat")]
        private void CmdChatCommand(BasePlayer player, string command, string[] args)
        {
            player.SendConsoleCommand("chat.say /menuSecret");
            player.SendConsoleCommand("chat.say /settings");   
            player.SendConsoleCommand("UI_RM_Handler choose 8");           
        }
        
        [ChatCommand("ignore")]
        private void CmdChatPersonalIgnore(BasePlayer player, string command, string[] args)
        {
            if (args.Length != 1)
            {
                player.ChatMessage($"Вы <color=orange>неправильно</color> используете команду!\n<size=12>/ignore <игрок> - отправить сообщение");
                return;
            }

            var targetSearch = args[0].ToLower();
            
            var target = BasePlayer.activePlayerList.ToList().FirstOrDefault(p => p.displayName.ToLower().Contains(targetSearch) || p.UserIDString == targetSearch);
            if (target == null || !target.IsConnected)
            {
                player.ChatMessage($"Игрок не найден!");
                return;
            }

            var settings = Handler.Settingses[player.userID];
            if (settings.IgnoreList.ContainsKey(target.userID))
            {
                player.ChatMessage($"Вы больше <color=orange>не игнорируете</color> этого игрока!");
                settings.IgnoreList.Remove(target.userID);
                return;
            }
            else
            {
                player.ChatMessage($"Теперь вы <color=orange>игнорируете</color> этого игрока!");
                settings.IgnoreList.Add(target.userID, target.displayName);
                return;
            }
        }

        [ChatCommand("r")]
        private void CmdChatPersonalReply(BasePlayer player, string command, string[] args)
        {
            if (args.Length < 1)
            {
                player.ChatMessage($"Вы <color=orange>неправильно</color> используете команду!\n<size=12>/r <сообщение> - отправить сообщение</size>");
                return;
            }

            var message = "";

            for (var i = 0; i < args.Length; i++)
                message += $"{args[i]} ";

            var target = Handler.Settingses[player.userID].ReplyTarget;
            if (target == null || !target.IsConnected)
            {
                player.ChatMessage($"Игрок не найден!");
                return;
            }

            SendPrivateMessage(player, target, message); 
        }
        
        [ChatCommand("pm")]
        private void CmdChatPersonalMessage(BasePlayer player, string command, string[] args)
        {
            if (args.Length <= 1)
            {
                player.ChatMessage($"Вы <color=orange>неправильно</color> используете команду!\n<size=12>/pm <имя> <сообщение> - отправить сообщение</size>");
                return;
            }

            var targetSearch = args[0].ToLower();
            var message      = "";

            for (var i = 1; i < args.Length; i++)
                message += $"{args[i]} ";

            var target = BasePlayer.activePlayerList.ToList().FirstOrDefault(p => p.displayName.ToLower().Contains(targetSearch));
            if (target == null || !target.IsConnected)
            {
                player.ChatMessage($"Игрок не найден!");
                return;
            }
            
            SendPrivateMessage(player, target, message);
        }
 
        #endregion

        #region Interface 
  
        private string SettingsLayer = "UI_SettingsLayer"; 
        private void InitializeInterface(BasePlayer player, bool reopen = false)
        {
            FetchStatus(player); 
            var settings = Handler.Settingses[player.userID];
             
            CuiElementContainer container = new CuiElementContainer();
            if (!reopen)
            {
                CuiHelper.DestroyUi(player, SettingsLayer + ".RP");
            
                container.Add(new CuiPanel()
                { 
                    CursorEnabled = true,
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"},  
                    Image         = {Color = "0 0 0 0" }
                }, SettingsLayer + ".R", SettingsLayer + ".RP");
            }
            
            CuiHelper.DestroyUi(player, SettingsLayer + ".RPP");
            container.Add(new CuiPanel()
            { 
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"},  
                Image         = {Color = "0 0 0 0" }
            }, SettingsLayer + ".RP", SettingsLayer + ".RPP");
            
            container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"28 -100", OffsetMax = $"500 -30"} ,
                    Text = { Text = "НАСТРОЙКИ НИКА", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 48, Color = "0.93 0.89 0.85 1"}
                }, SettingsLayer + ".RPP");  
            
            container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"30 -130", OffsetMax = $"500 -80"} , 
                    Text = { Text = $"ТВОЙ НИК СЕЙЧАС: {PrepareNick(player)}", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 20, Color = "0.81 0.77 0.74 0.6"}
                }, SettingsLayer + ".RPP");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 125" },
                Image = { Color = "0.29411 0.27450 0.254901 1" }
            }, SettingsLayer + ".R", SettingsLayer + ".Info");
			
			container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"28 -100", OffsetMax = $"-30 -5"},
                Text = { Text = "ИНФОРМАЦИЯ", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 48, Color = "0.93 0.89 0.85 1"}
            }, SettingsLayer + ".Info");

            string helpText = "<b>Подсказки</b> - сообщения сервера с подсказками (которые отправляются в чат каждые ~3 минуты)\n<b>Глобальный чат</b> - отключает общий чат, но информационные сообщения от плагинов и раста остаются\n<b>Личные сообщения</b> - отключает/включает возможность писать вам в личные сообщения";
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "30 0", OffsetMax = "0 -60" },
                Text = { Text = helpText, Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "0.67 0.63 0.596"}
            }, SettingsLayer + ".Info");

            #region Switch layer Цвет
            
            var currentStatus = Settingses.Colors.FirstOrDefault(p => p.Value.Split(':')[1] == settings.Names.Color);
            List<KeyValuePair<string, string>> possibleStatuses = Settingses.Colors.ToList().FindAll(p => permission.UserHasPermission(player.UserIDString, p.Key));
 
            var colorIndex = possibleStatuses.IndexOf(currentStatus);
            string leftCommand = $"UI_Chat name_color {possibleStatuses.ElementAtOrDefault(colorIndex - 1).Value}"; 
            string rightCommand = $"UI_Chat name_color {possibleStatuses.ElementAtOrDefault(colorIndex + 1).Value}"; 
            bool leftActive = colorIndex > 0;
            bool rightActive = colorIndex < possibleStatuses.Count - 1;
            
            string guid = CuiHelper.GetGuid(); 
            container.Add(new CuiLabel 
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -200", OffsetMax = $"-10 -130" },
                Text = { Text = $"ЦВЕТ НИКА<size=12>({possibleStatuses.Count})</size>", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 28, Color = "0.81 0.77 0.74 0.6"}
            }, SettingsLayer + ".RPP", guid);
            
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-250 -30", OffsetMax = $"-30 0"},
                Image = {Color = "0.27 0.247 0.184 1", Material = ""}
            }, guid, guid + ".P");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMax = "30 0" },
                Image = { Color = leftActive ? "0.81 0.77 0.74 0.2" : "0.81 0.77 0.74 0.15" }
            }, guid + ".P", guid + ".L"); 
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = leftActive ? leftCommand : "" },
                Text = { Text = "<b><</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = leftActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2" }
            }, guid + ".L");
            
            container.Add(new CuiPanel 
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "-30 0", OffsetMax = "0 0" },
                Image = { Color = rightActive ? "0.81 0.77 0.74 0.2" : "0.81 0.77 0.74 0.1"}
            }, guid + ".P", guid + ".R");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = rightActive ? rightCommand : "" },
                Text = { Text = "<b>></b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2"}
            }, guid + ".R");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"30 0", OffsetMax = $"-30 0" },  
                Text = { Text = currentStatus.Value.Split(':')[0], Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.81 0.77 0.74 0.6"}
            }, guid + ".P");

            #endregion 

            #region Switch layer Префикс
            
            currentStatus = Settingses.Prefixes.FirstOrDefault(p => p.Value.Split(':')[1] == settings.Prefixes.Name);
            possibleStatuses = Settingses.Prefixes.ToList().FindAll(p => permission.UserHasPermission(player.UserIDString, p.Key));
            bool canChange = currentStatus.Value != "НЕТ:-";
            
            colorIndex = possibleStatuses.IndexOf(currentStatus);
            leftCommand = $"UI_Chat prefix {possibleStatuses.ElementAtOrDefault(colorIndex - 1).Value}"; 
            rightCommand = $"UI_Chat prefix {possibleStatuses.ElementAtOrDefault(colorIndex + 1).Value}"; 
            leftActive = colorIndex > 0;
            rightActive = colorIndex < possibleStatuses.Count - 1;
            
            guid = CuiHelper.GetGuid(); 
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -220", OffsetMax = $"-10 -165" },
                Text = { Text = $"ПРЕФИКС<size=12>({possibleStatuses.Count})</size>", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 28, Color = "0.81 0.77 0.74 0.6"}
            }, SettingsLayer + ".RPP", guid);

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-250 -30", OffsetMax = $"-30 0"},
                Image = {Color = "0.27 0.247 0.184 1", Material = ""} 
            }, guid, guid + ".P");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMax = "30 0" },
                Image = { Color = leftActive ? "0.81 0.77 0.74 0.2" : "0.81 0.77 0.74 0.15" }
            }, guid + ".P", guid + ".L"); 
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = leftActive ? leftCommand : "" },
                Text = { Text = "<b><</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = leftActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2" }
            }, guid + ".L");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "-30 0", OffsetMax = "0 0" },
                Image = { Color = rightActive ? "0.81 0.77 0.74 0.2" : "0.81 0.77 0.74 0.1"}
            }, guid + ".P", guid + ".R");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = rightActive ? rightCommand : "" },
                Text = { Text = "<b>></b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2"}
            }, guid + ".R");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"30 0", OffsetMax = $"-30 0" },  
                Text = { Text = currentStatus.Value.Split(':')[0], Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.81 0.77 0.74 0.6"}
            }, guid + ".P");

            #endregion 

            #region Switch layer ЦветПрефикс
            
            
            currentStatus = Settingses.Colors.FirstOrDefault(p => p.Value.Split(':')[1] == settings.Prefixes.Color);
            possibleStatuses = Settingses.Colors.ToList().FindAll(p => permission.UserHasPermission(player.UserIDString, p.Key));
 
            colorIndex = possibleStatuses.IndexOf(currentStatus);
            leftCommand = $"UI_Chat prefix_color {possibleStatuses.ElementAtOrDefault(colorIndex - 1).Value}"; 
            rightCommand = $"UI_Chat prefix_color {possibleStatuses.ElementAtOrDefault(colorIndex + 1).Value}"; 
            leftActive = colorIndex > 0 && canChange;
            rightActive = colorIndex < possibleStatuses.Count - 1 && canChange;
            
            guid = CuiHelper.GetGuid(); 
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -250", OffsetMax = $"-10 -200" },
                Text = { Text = $"ЦВЕТ ПРЕФИКСА<size=12>({possibleStatuses.Count})</size>", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 28, Color = "0.81 0.77 0.74 0.6"}
            }, SettingsLayer + ".RPP", guid);

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-250 -30", OffsetMax = $"-30 0"},
                Image = {Color = "0.27 0.247 0.184 1", Material = ""}
            }, guid, guid + ".P");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMax = "30 0" },
                Image = { Color = leftActive ? "0.81 0.77 0.74 0.2" : "0.81 0.77 0.74 0.15" }
            }, guid + ".P", guid + ".L"); 
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = leftActive ? leftCommand : "" },
                Text = { Text = "<b><</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = leftActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2" }
            }, guid + ".L");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "-30 0", OffsetMax = "0 0" },
                Image = { Color = rightActive ? "0.81 0.77 0.74 0.2" : "0.81 0.77 0.74 0.1"}
            }, guid + ".P", guid + ".R");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = rightActive ? rightCommand : "" },
                Text = { Text = "<b>></b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2"}
            }, guid + ".R");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"30 0", OffsetMax = $"-30 0" },  
                Text = { Text = currentStatus.Value.Split(':')[0], Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.81 0.77 0.74 0.6"}
            }, guid + ".P");

            #endregion 

            #region Switch layer Размер префикса
            
            
            currentStatus = Settingses.Sizes.FirstOrDefault(p => p.Value.Split(':')[1] == settings.Prefixes.Size);
            possibleStatuses = Settingses.Sizes.ToList().FindAll(p => permission.UserHasPermission(player.UserIDString, p.Key));
 
            colorIndex = possibleStatuses.IndexOf(currentStatus);
            leftCommand = $"UI_Chat prefix_size {possibleStatuses.ElementAtOrDefault(colorIndex - 1).Value}"; 
            rightCommand = $"UI_Chat prefix_size {possibleStatuses.ElementAtOrDefault(colorIndex + 1).Value}"; 
            leftActive = colorIndex > 0 && canChange;
            rightActive = colorIndex < possibleStatuses.Count - 1 && canChange;
            
            guid = CuiHelper.GetGuid(); 
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -290", OffsetMax = $"-10 -235" },
                Text = { Text = $"РАЗМЕР ПРЕФИКСА<size=12>({possibleStatuses.Count})</size>", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 28, Color = "0.81 0.77 0.74 0.6"}
            }, SettingsLayer + ".RPP", guid);

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-250 -30", OffsetMax = $"-30 0"},
                Image = {Color = "0.27 0.247 0.184 1", Material = ""}
            }, guid, guid + ".P");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMax = "30 0" },
                Image = { Color = leftActive ? "0.81 0.77 0.74 0.2" : "0.81 0.77 0.74 0.15" }
            }, guid + ".P", guid + ".L"); 
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = leftActive ? leftCommand : "" },
                Text = { Text = "<b><</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = leftActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2" }
            }, guid + ".L");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "-30 0", OffsetMax = "0 0" },
                Image = { Color = rightActive ? "0.81 0.77 0.74 0.2" : "0.81 0.77 0.74 0.1"}
            }, guid + ".P", guid + ".R");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = rightActive ? rightCommand : "" },
                Text = { Text = "<b>></b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2"}
            }, guid + ".R");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"30 0", OffsetMax = $"-30 0" },  
                Text = { Text = currentStatus.Value.Split(':')[0], Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.81 0.77 0.74 0.6"}
            }, guid + ".P");

            #endregion 

            #region Switch layer Вид префикса
            
            
            currentStatus = Settingses.Types.FirstOrDefault(p => p.Value.Split(':')[1] == settings.Prefixes.Hooks);
            possibleStatuses = Settingses.Types.ToList().FindAll(p => permission.UserHasPermission(player.UserIDString, p.Key));
 
            colorIndex = possibleStatuses.IndexOf(currentStatus);
            leftCommand = $"UI_Chat hook_type {possibleStatuses.ElementAtOrDefault(colorIndex - 1).Value}"; 
            rightCommand = $"UI_Chat hook_type {possibleStatuses.ElementAtOrDefault(colorIndex + 1).Value}"; 
            leftActive = colorIndex > 0 && canChange;
            rightActive = colorIndex < possibleStatuses.Count - 1 && canChange;
            
            guid = CuiHelper.GetGuid(); 
             
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -390", OffsetMax = $"-10 -270" },
                Text = { Text = $"ВИД ПРЕФИКСА<size=12>({possibleStatuses.Count})</size>", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 28, Color = "0.81 0.77 0.74 0.6"}
            }, SettingsLayer + ".RPP", guid);

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-250 -30", OffsetMax = $"-30 0"},
                Image = {Color = "0.27 0.247 0.184 1", Material = ""}
            }, guid, guid + ".P");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMax = "30 0" },
                Image = { Color = leftActive ? "0.81 0.77 0.74 0.2" : "0.81 0.77 0.74 0.15" }
            }, guid + ".P", guid + ".L"); 
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = leftActive ? leftCommand : "" },
                Text = { Text = "<b><</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = leftActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2" }
            }, guid + ".L");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "-30 0", OffsetMax = "0 0" },
                Image = { Color = rightActive ? "0.81 0.77 0.74 0.2" : "0.81 0.77 0.74 0.1"}
            }, guid + ".P", guid + ".R");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = rightActive ? rightCommand : "" },
                Text = { Text = "<b>></b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2"}
            }, guid + ".R");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"30 0", OffsetMax = $"-30 0" },  
                Text = { Text = currentStatus.Value.Split(':')[0], Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.81 0.77 0.74 0.6"}
            }, guid + ".P");

            #endregion 
            
            
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"28 -500", OffsetMax = $"500 -320"} ,
                Text = { Text = "НАСТРОЙКИ ЧАТА", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 48, Color = "0.93 0.89 0.85 1"}
            }, SettingsLayer + ".RPP");  
            

            #region Bool layer Подсказки
            
             
            var switchStatus = settings.Chatters.Tips;

            var mainCommand = $"UI_Chat tips {!switchStatus}"; 
            
            guid = CuiHelper.GetGuid(); 
             
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -500", OffsetMax = $"-10 -390" },
                Text = { Text = "ПОДСКАЗКИ", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 28, Color = "0.81 0.77 0.74 0.6"}
            }, SettingsLayer + ".RPP", guid);

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-250 -30", OffsetMax = $"-30 0"},
                Image = {Color = switchStatus ? "0.27 0.247 0.184 1" : "0.23 0.22 0.17 0.5", Material = ""}
            }, guid, guid + ".P");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"30 0", OffsetMax = $"-30 0" },  
                Text = { Text = switchStatus ? "ВКЛ" : "ВЫКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.81 0.77 0.74 0.6"}
            }, guid + ".P");
            
            container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0", Command = mainCommand },
                    Text = { Text = "", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2"}
                }, guid + ".P"); 

            #endregion 
            
            #region Bool layer Звуки
            
             
            switchStatus = settings.Chatters.Sound;

            mainCommand = $"UI_Chat sound {!switchStatus}";  
            
            guid = CuiHelper.GetGuid(); 
             
            container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -700", OffsetMax = $"-10 -425" },
                    Text = { Text = "ЗВУК ЛИЧНЫХ СООБЩ.", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 28, Color = "0.81 0.77 0.74 0.6"}
                }, SettingsLayer + ".RPP", guid);

            container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-250 -30", OffsetMax = $"-30 0"},
                    Image = {Color = switchStatus ? "0.27 0.247 0.184 1" : "0.23 0.22 0.17 0.5", Material = ""}
                }, guid, guid + ".P");
            
            container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"30 0", OffsetMax = $"-30 0" },  
                    Text = { Text = switchStatus ? "ВКЛ" : "ВЫКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.81 0.77 0.74 0.6"}
                }, guid + ".P");
            
            container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0", Command = mainCommand },
                    Text = { Text = "", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2"}
                }, guid + ".P"); 

            #endregion 
            
            
            #region Bool layer Вид префикса
            
             
            switchStatus = settings.Chatters.Censor;

            mainCommand = $"UI_Chat censor {!switchStatus}";  
            
            guid = CuiHelper.GetGuid(); 
             
            container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -700", OffsetMax = $"-10 -460" },
                    Text = { Text = "ЦЕНЗУРА", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 28, Color = "0.81 0.77 0.74 0.6"}
                }, SettingsLayer + ".RPP", guid);

            container.Add(new CuiPanel
                {
                    RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-250 -30", OffsetMax = $"-30 0"},
                    Image = {Color = switchStatus ? "0.27 0.247 0.184 1" : "0.23 0.22 0.17 0.5", Material = ""}
                }, guid, guid + ".P");
            
            container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"30 0", OffsetMax = $"-30 0" },  
                    Text = { Text = switchStatus ? "ВКЛ" : "ВЫКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.81 0.77 0.74 0.6"}
                }, guid + ".P");
            
            container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Button = { Color = "0 0 0 0", Command = mainCommand },
                    Text = { Text = "", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2"}
                }, guid + ".P"); 

            #endregion 
            
            #region Bool layer Вид префикса
            
             
            switchStatus = settings.Chatters.Chat;

            mainCommand = $"UI_Chat chatglobal {!switchStatus}";  
            
            guid = CuiHelper.GetGuid(); 
             
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -700", OffsetMax = $"-10 -495" },
                Text = { Text = "ГЛОБАЛЬНЫЙ ЧАТ", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 28, Color = "0.81 0.77 0.74 0.6"}
            }, SettingsLayer + ".RPP", guid);

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-250 -30", OffsetMax = $"-30 0"},
                Image = {Color = switchStatus ? "0.27 0.247 0.184 1" : "0.23 0.22 0.17 0.5", Material = ""}
            }, guid, guid + ".P");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"30 0", OffsetMax = $"-30 0" },  
                Text = { Text = switchStatus ? "ВКЛ" : "ВЫКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.81 0.77 0.74 0.6"}
            }, guid + ".P");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = mainCommand },
                Text = { Text = "", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2"}
            }, guid + ".P"); 

            #endregion 
            
            #region Bool layer Вид префикса
            
             
            switchStatus = settings.Chatters.PM;

            mainCommand = $"UI_Chat chatpm {!switchStatus}";  
            
            guid = CuiHelper.GetGuid(); 
             
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"30 -700", OffsetMax = $"-10 -530" },
                Text = { Text = "ЛИЧНЫЕ СООБЩЕНИЯ", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 28, Color = "0.81 0.77 0.74 0.6"}
            }, SettingsLayer + ".RPP", guid);

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = $"-250 -30", OffsetMax = $"-30 0"},
                Image = {Color = switchStatus ? "0.27 0.247 0.184 1" : "0.23 0.22 0.17 0.5", Material = ""}
            }, guid, guid + ".P");
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"30 0", OffsetMax = $"-30 0" },  
                Text = { Text = switchStatus ? "ВКЛ" : "ВЫКЛ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = "0.81 0.77 0.74 0.6"}
            }, guid + ".P");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = mainCommand },
                Text = { Text = "", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.8 0.8 0.8 1" : "0.8 0.8 0.8 0.2"}
            }, guid + ".P"); 

            #endregion 
            
            CuiHelper.AddUi(player, container);
        }
 
        #endregion

        #region Utils

        private void LogChat(string message) => LogToFile("Chat", "[" + DateTime.Now.ToShortTimeString() + "]" + message, this);
        private void LogPM(string message) => LogToFile("PM", "[" + DateTime.Now.ToShortTimeString() + "]" + message, this);

        
        
        private string PrepareNickForConnect(BasePlayer player)
        {
            var settings = Handler.Settingses[player.userID];

            string prefixPrepare = "continue";
            switch (settings.Prefixes.Hooks) 
            {
                case "[]": prefixPrepare = $"[{settings.Prefixes.Color}{settings.Prefixes.Name}</color>]";
                    break;
                case "-": prefixPrepare = $"{settings.Prefixes.Color}{settings.Prefixes.Name}</color>";
                    break;
                case "|": prefixPrepare = $"{settings.Prefixes.Color}{settings.Prefixes.Name}</color> |";
                    break;
            }

            if (settings.Prefixes.Name == "-")
                prefixPrepare = "";
            
            string format = "continue";
            switch (settings.Prefixes.Hooks)
            {
                case "[]": format = $"{(Settingses.WelcomePrefix ? prefixPrepare : "")} {settings.Names.Color}{player.displayName}</color>";
                    break;
                case "-": format = $"{(Settingses.WelcomePrefix ? prefixPrepare : "")} {settings.Names.Color}{player.displayName}</color>";
                    break;
                case "|": format = $"{(Settingses.WelcomePrefix ? prefixPrepare : "")} {settings.Names.Color}{player.displayName}</color>";
                    break;
            }
            
            return format; 
        }
        private string PrepareNick(BasePlayer player)
        {
            var settings = Handler.Settingses[player.userID];

            string prefixPrepare = "continue";
            switch (settings.Prefixes.Hooks) 
            {
                case "[]": prefixPrepare = $"[{settings.Prefixes.Color}{settings.Prefixes.Name}</color>]";
                    break;
                case "-": prefixPrepare = $"{settings.Prefixes.Color}{settings.Prefixes.Name}</color>";
                    break;
                case "|": prefixPrepare = $"{settings.Prefixes.Color}{settings.Prefixes.Name}</color> |";
                    break;
            }

            if (settings.Prefixes.Name == "-")
                prefixPrepare = "";

            string name = player.displayName.Length > 26 ? player.displayName.Substring(0, 26) : player.displayName;

            string format = "continue";
            switch (settings.Prefixes.Hooks)
            {
                case "[]": format = $"{prefixPrepare} {settings.Names.Color}{name}</color>";
                    break;
                case "-": format = $"{prefixPrepare} {settings.Names.Color}{name}</color>";
                    break;
                case "|": format = $"{prefixPrepare} {settings.Names.Color}{name}</color>";
                    break;
            }
            
            return format; 
        }

        private string Censure(string message)
        {
            foreach (var mat in Settingses.Censures)
            {
                if (message.ToLower().Contains(mat.Key))
                {
                    bool shouldReplace = true;
                    foreach (var ist in mat.Value)
                    {
                        if (message.Contains(ist))
                        {
                            shouldReplace = false;
                            break;
                        }
                    }

                    if (shouldReplace) message = message.Replace(mat.Key, "***", StringComparison.CurrentCultureIgnoreCase);
                }
            }

            return message; 
        }
        
        private string PrepareMessage(BasePlayer player, string message, bool team = false) 
        {
            var settings = Handler.Settingses[player.userID];

            string prefixPrepare = "continue";
            switch (settings.Prefixes.Hooks)
            {
                case "[]": prefixPrepare = $"{settings.Prefixes.Size}[{settings.Prefixes.Color}{settings.Prefixes.Name}</color>]</size>";
                    break;
                case "-": prefixPrepare = $"{settings.Prefixes.Size}{settings.Prefixes.Color}{settings.Prefixes.Name}</color></size>";
                    break;
                case "|": prefixPrepare = $"{settings.Prefixes.Size}{settings.Prefixes.Color}{settings.Prefixes.Name}</color></size> |";
                    break;
            }

            if (settings.Prefixes.Name == "-")
                prefixPrepare = "";
            
            string format = "continue";
            switch (settings.Prefixes.Hooks)
            {
                case "[]": format = $"{prefixPrepare} {settings.Names.Color}{player.displayName}</color>: {message}";
                    break;
                case "-": format = $"{prefixPrepare} {settings.Names.Color}{player.displayName}</color>: {message}";
                    break;
                case "|": format = $"{prefixPrepare} {settings.Names.Color}{player.displayName}</color>: {message}";
                    break;
            }

            if (team) format = "<color=#ADFF2F>[TEAM]</color> " + format;
            
            return format;
        }

        #endregion
    }
}