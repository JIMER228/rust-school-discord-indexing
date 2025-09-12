/**
 * Changelogs:
 * 2.1.2:
 * - fixed crash server on restart
 * - fixed player with ban could join server
 * 2.1.1:
 * - support RCC
 * 2.1.0:
 * - Remoed Nimant's magic (subscribe internal hook)
 * - Disable debug with errors
 * - Hide actions in components
 * 2.0.0:
 * - Global update, full refactor
 * - Disabled sockets
 * 1.2.7:
 * - Global update: vk.com/@rustpanel-update
 * 1.2.6:
 * - Alerts improvements
 * - Version grabber for panel alert
 * 1.2.5:
 * - Alert supports
 * 1.2.4:
 * - Fixed troubles with team detecting
 * 1.2.3:
 * - State update on disconnected players fixes
 * 1.2.2:
 * - Supports for IP bans
 * 1.2.1:
 * - Better RaidZone support
 * 1.2.0:
 * - Support for a raid-blocks plugins
 * - Support for a IP in panel
 * 1.1.9:
 * - Fixing discord sent with double quotes
 * - Additional logs when API error
 * 1.1.8:
 * - Again trying to fix multi-calling-check-show
 * - Fixed trouble with socket silent disconnect (probably)
 * 1.1.7:
 * - Added configuration for open report panel
 * - Fixes possible socket errors
 * - Auto hide UI on plugin restart
 * - Logs for showing UI
 * 1.1.6:
 * - Changed the sign of the call for verification
 * - Added the ability to change the 'report' command in the plugin configuration
 * 1.1.5:
 * - Realized socket connection with rust panel
 * 1.1.4:
 * - Fixed stats update on high load
 * 1.1.3:
 * - Fixed state update failed
 * 1.1.2:
 * - Kick for ban fixes
 * 1.1.1:
 * - Change interface
 * 1.1.0:
 * - Possible fix state update pause...
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using ConVar;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using VLB;

namespace Oxide.Plugins
{
    [Info("Rust Panel", "Hougan", "2.1.2")]
    public class RustPanel : RustPlugin
    {
        #region QueueService

        #region Classes & Interfaces

        class Queue
        {
            internal class DetailsInfo
            {
                [JsonProperty("name")]
                public string Name;

                [JsonProperty("payload")]
                public JObject Payload;
            }
            
            [JsonProperty("id")]
            public string Id;
            [JsonProperty("details")]
            public DetailsInfo Details;
        }

        class QueueResolve
        {
            internal class ResolveInfo
            {
                [JsonProperty("success")]
                public bool Success;

                [JsonProperty("payload")]
                public object Payload;

                public ResolveInfo(bool success, [CanBeNull] object payload)
                {
                    this.Success = success;
                    this.Payload = payload;
                }
            }
            
            [JsonProperty("id")]
            public string Id;

            [JsonProperty("resolve")]
            public ResolveInfo Resolve;

            public QueueResolve(string id, ResolveInfo resolve)
            {
                this.Id = id;
                this.Resolve = resolve;
            }
        }

        #endregion
        
        #region Components
        
        private class QueueService : MonoBehaviour
        {
            /**
             * Url of queue api service
             * Do not change this value
             */
            private static string URL = $"https://internal.rustpanel.ru/v1/queue?id={Settings.Hash}&api={Settings.Token}";

            /**
             * Flag for detecting and not pushing more than one message per raw
             */
            private bool IS_ERROR = false;

            private void Start()
            {
                this.Log("успешно загружен / successfull load");

                InvokeRepeating(nameof(Sync), 0f, 1f);
            }

            private void Sync()
            {
                LoadQueues(queues =>
                {
                    // this.Log("загружены очереди / queues loaded");
                    
                    var resolves = this.ParseQueues(queues);
                    
                    Process(resolves, () =>
                    {
                        if (IS_ERROR)
                        {
                            this.Log($"работа очередей востановлена / queue service re-connected");
                            IS_ERROR = false;
                        }
                    }, error =>
                    {
                        if (!IS_ERROR)
                        {
                            this.Log($"не удалось разобрать очереди / failed to process queues", true);
                            this.Log($"> {error}", true);
                            IS_ERROR = true;
                        }
                    });
                }, error =>
                {
                    if (!IS_ERROR)
                    {
                        this.Log($"не удалось обработать очереди / failed to parse queues", true);
                        this.Log($"> {error}", true);
                        IS_ERROR = true;
                    }
                });
            }

            private List<QueueResolve> ParseQueues(List<Queue> queues)
            {
                return queues.Select(this.ParseQueue).ToList();
            }
            
            private QueueResolve ParseQueue(Queue queue)
            {
                var resolve = Interface.Oxide.CallHook("OnRustPanelQueue", queue.Details.Name, queue.Details.Payload);
                if (resolve == null)
                {
                    this.Log($"Unresolved action: '{queue.Details.Name}'.  Payload: '{JsonConvert.SerializeObject(queue.Details.Payload)}'", true);
                    return new QueueResolve(queue.Id, new QueueResolve.ResolveInfo(false, "UNRESOLVED"));
                }
                
                return new QueueResolve(queue.Id, new QueueResolve.ResolveInfo(!(resolve is String), resolve));
            }

            private void Process(List<QueueResolve> resolves, Action resolve, Action<string> reject)
            {
                object obj = new
                {
                    credentials = new
                    {
                        api = Settings.Token,
                        id = Settings.Hash
                    },
                    chunks = resolves
                };
                
                Instance.webrequest.Enqueue(QueueService.URL, JsonConvert.SerializeObject(obj), (code, text) =>
                {
                    if (code != 200)
                    {
                        reject($"не удалось соединиться [{code}] / connection failed [{code}]");
                        return;
                    }

                    try
                    {
                        Response response = JsonConvert.DeserializeObject<Response>(text);
                        if (!response.Success)
                        {
                            reject("ответ не был успешным / response was not successful");
                            return;
                        }

                        resolve();
                    }
                    catch
                    {
                        reject("не удалось распознать ответ / response was not convertable");
                    }
                }, Instance, RequestMethod.PUT, new Dictionary<string, string> { ["Content-Type"] = "application/json" });
            }

            private void LoadQueues(Action<List<Queue>> resolve, Action<string> reject)
            {
                Instance.webrequest.Enqueue(QueueService.URL, String.Empty, (code, text) =>
                {
                    if (code != 200)
                    {
                        reject($"не удалось соединиться [{code}] / connection failed [{code}]");
                        return;
                    }

                    try
                    {
                        Response response = JsonConvert.DeserializeObject<Response>(text);
                        if (!response.Success)
                        {
                            reject("ответ не был успешным / response was not successful");
                            return;
                        }
                    
                        List<Queue> queues = JsonConvert.DeserializeObject<List<Queue>>(JsonConvert.SerializeObject(response.Data));

                        resolve(queues);
                    }
                    catch
                    {
                        reject("не удалось распознать ответ / response was not convertable");
                    }
                }, Instance, RequestMethod.GET, new Dictionary<string, string> { ["Content-Type"] = "application/json" });  
            }

            private void Log(string text, bool isError = false)
            {
                var prepared = $"QueueService: {text}";
                
                if (isError)
                {
                    return;
                }
            }
        }
        
        #endregion
        
        #region Implementation
        
        [CanBeNull]
        [HookMethod("OnRustPanelQueue")]
        private object OnRustPanelQueue(string name, JObject payload)
        {
            switch (name)
            {
                // Message to the global chat
                case "chat_send":
                    return OnChatSend(JsonConvert.DeserializeObject<ChatSendPayload>(payload.ToString()));
                
                // Message to the player DM
                case "chat_direct":
                    return OnChatDirect(JsonConvert.DeserializeObject<ChatDirectPayload>(payload.ToString()));
                
                // Kick player from server
                case "player_kick":
                    return OnPlayerKick(JsonConvert.DeserializeObject<PlayerKickPayload>(payload.ToString()));
                
                // Show check notification to player
                case "check_start":
                    return OnCheckStart(JsonConvert.DeserializeObject<CheckStartPayload>(payload.ToString()));
                 
                // Remove check notification from player
                case "check_stop":
                    return OnCheckStop(JsonConvert.DeserializeObject<CheckStopPayload>(payload.ToString()));
                
                // Check for ban and kick user on server
                case "send_ban":
                    return OnSendBan(JsonConvert.DeserializeObject<SendBanPayload>(payload.ToString()));
                
                // Fetch for ban for user
                case "fetch_ban":
                    return OnFetchBan(JsonConvert.DeserializeObject<FetchBanPayload>(payload.ToString()));
            }

            return null;
        }
        
        #region OnChatSend

        class ChatSendPayload
        {
            [JsonProperty("targetId")] public string TargetId;
            [JsonProperty("name")] public string Name;
            [JsonProperty("message")] public string Message;
        }
        
        private object OnChatSend(ChatSendPayload payload) {
            if (payload.Name == null || payload.Message == null)
            {
                return $"WRONG.PARAMS";
            }
            
            var msg = Settings.MsgFormat.Replace("%NAME%", payload.Name).Replace("%MSG%", payload.Message);

            foreach (var check in BasePlayer.activePlayerList)
            {
                check.SendConsoleCommand("chat.add", 0, Settings.ShowAvatar ? payload.TargetId : "0", msg);
            }

            return new {};
        }

        #endregion

        #region OnChatDirect

        private class ChatDirectPayload
        {
            [JsonProperty("targetId")] public string TargetId;
            [JsonProperty("message")] public string Message;
        }

        private object OnChatDirect(ChatDirectPayload payload)
        {
            if (payload.Message == null || payload.TargetId == null)
            {
                return $"WRONG.PARAMS";
            }
            
            var target = BasePlayer.Find(payload.TargetId);
            if (!target || !target.IsConnected)
            {
                return "ERROR.PLAYER.OFFLINE";
            }

            target.ChatMessage(Settings.DirectFormat.Replace("%MSG%", payload.Message));
            Instance.SoundToast(target, "Получено сообщение от модератора, посмотрите в чат", 2);
                
            return new {};
        }

        #endregion

        #region OnPlayerKick
        
        private class PlayerKickPayload
        {
            [JsonProperty("targetId")] public string TargetId;
            [JsonProperty("reason")] public string Reason;
        }
        
        private object OnPlayerKick(PlayerKickPayload payload)
        {
            if (payload.TargetId == null)
            {
                return "WRONG.PARAMS";
            }
            
            var target = BasePlayer.Find(payload.TargetId.ToString());
            if (!target || !target.IsConnected)
            {
                return "ERROR.PLAYER.OFFLINE";
            }

            target.Kick(payload.Reason ?? "Steam Auth Timeout");
            return new {};
        }

        #endregion

        #region OnCheckStart

        private class CheckStartPayload
        {
            [JsonProperty("targetId")] public string TargetId;
        }

        private object OnCheckStart(CheckStartPayload payload)
        {   
            if (payload.TargetId == null)
            {
                return "WRONG.PARAMS";
            }
 
            var target = BasePlayer.Find(payload.TargetId);
            if (target == null || !target.IsConnected)  
            {
                return "PLAYER_OFFLINE";
            }
						
            Instance.DrawInterface(target);
            return new {};
        }

        #endregion

        #region OnCheckStop

        private class CheckStopPayload
        {
            [JsonProperty("targetId")] public string TargetId;
        }

        private object OnCheckStop(CheckStopPayload payload)
        {
            if (payload.TargetId == null)
            {
                return "WRONG.PARAMS";
            }

            var target = BasePlayer.Find(payload.TargetId);
            if (target == null || !target.IsConnected)  
            {
                return "PLAYER_OFFLINE";
            }
            
            CuiHelper.DestroyUi(target, CheckLayer);
            
            Instance.OnPlayerConnected(target);
            return new {};
        }

        #endregion

        #region OnSendBan

        private class SendBanPayload
        {
            [JsonProperty("steamId")] public string SteamId;
            [JsonProperty("steamName")] public string SteamName;
            [JsonProperty("reason")] public string Reason;
        }
        
        private object OnSendBan(SendBanPayload payload)
        {
            if (Settings.BanFormat.Length != 0)
            {
                Instance.Server.Broadcast(Settings.BanFormat.Replace("%TARGET%", payload.SteamName).Replace("%REASON%", payload.Reason));
            }

            return new {};
        }

        #endregion

        #region OnFetchBan

        private class FetchBanPayload
        {
            [JsonProperty("targetId")] public string TargetId;
        }

        private object OnFetchBan(FetchBanPayload payload)
        {
            if (payload.TargetId == null)
            {
                return "WRONG.PARAMS";
            }

            var target = BasePlayer.Find(payload.TargetId);
            if (target == null || !target.IsConnected)  
            {
                return "PLAYER_OFFLINE";
            }
            
            InjectedAPI.GetBans(target, finalObj => {});    
            return new {};
        }

        #endregion

        #endregion

        #endregion

        #region UpdateService

        #region Classes & Interfaces

        class Response
        {
            [JsonProperty("success")]
            public bool Success;
            [JsonProperty("data")]
            public object Data;

            public Response(bool success, object data = null)
            {
                this.Success = success;
                this.Data = data;
            }
        }
        
        private class ServerInfo
        {
            [JsonProperty("name")]
            public string Name;
            [JsonProperty("hash")]
            public string Hash;
            [JsonProperty("hostName")] 
            public string HostName;
            [JsonProperty("version")]
            public string Version;

            public static ServerInfo Get()
            {
                return new ServerInfo
                {
                    Name = Settings.Name,
                    Hash = Settings.Hash,
                    HostName = ConVar.Server.hostname,
                    Version = Instance.Version.ToString()
                };
            }
        }

        #endregion
        
        #region Components
        private class UpdateService : MonoBehaviour
        {
            private void Start()
            {
                this.Log("успешно загружен / successfull load");
                
                Sync();
            }

            private void Sync()
            {
                try
                {
                    var users = UserService.CACHE
                        .Select(v => v.Sync())
                        .Where(v => v != null);
                    
                    var payload = new
                    {
                        state = new
                        {
                            players = users
                        }
                    };

                    InjectedAPI.SendUpdate(payload);
                    
                    // this.Log("отправлено обновление / send update state");
                }
                catch
                {
                    this.Log("не удалось отправить обновление / failed to send update", true);    
                }
                
                Invoke(nameof(Sync), 5f);
            }

            private void Log(string text, bool isError = false)
            {
                var prepared = $"UpdateService: {text}";
                
                if (isError)
                {
                    Instance.PrintError(prepared);
                    return;
                }
                
                Instance.PrintWarning(prepared);
            }
        }
        
        #endregion

        #endregion
        
        #region UserService
        
        #region Classes & Interfaces

        private class UserInfo
        {
            [JsonProperty("UserName")]
            public string UserName;
            [JsonProperty("UserId")]
            public string UserId;
            [JsonProperty("IP")]
            public string IP;
            
            [JsonProperty("InCup")]
            public bool InCup;
            [JsonProperty("IsMoving")]
            public bool IsMoving;
            [JsonProperty("IsRaidBlocked")]
            public bool? IsRaidBlocked;

            [JsonProperty("Team")]
            public List<string> Team = new List<string>();
        }
        
        #endregion
        
        #region Components

        private class UserService : MonoBehaviour
        {
            public static Dictionary<ulong, double> COOLDOWNS = new Dictionary<ulong, double>();
            public static List<UserService> CACHE = new List<UserService>();
            
            private BasePlayer Player;
            private Vector3 Position;
            
            private void Awake()
            {
                this.Player = this.gameObject.GetComponent<BasePlayer>();
                
                SavePosition();
                
                UserService.CACHE.Add(this);
            }

            public void CheckBans(Action callback)
            {
                try
                {
                    InjectedAPI.GetBans(this.Player, final => callback());
                }
                catch (Exception err)
                {
                    Instance.PrintWarning($"Failed to check bans {Player?.UserIDString ?? "UNKNOWN ID"}");
                    Instance.PrintError(err.ToString());
                }
            }

            private void OnDestroy()
            {
                UserService.CACHE.Remove(this);
            }

            public UserInfo Sync()
            {
                if (Player == null || !Player.IsConnected)
                {
                    UnityEngine.Object.Destroy(this);
                    return null;
                }

                var userInfo = new UserInfo();

                userInfo.UserId = Player.UserIDString;
                userInfo.UserName = Player.displayName;
                userInfo.IP = Player.Connection.ipaddress.Split(':')[0];

                userInfo.IsMoving = Vector3.Distance(Position, Player.transform.position) > 0.1f;
                userInfo.IsRaidBlocked = this.IsRaidBlocked();
                userInfo.InCup = Player.IsBuildingAuthed();

                if (Player.Team != null)
                {
                    userInfo.Team = Player.Team.members
                        .Where(v => v != Player.userID)
                        .Select(v => v.ToString())
                        .ToList();
                }
                
                this.SavePosition();

                return userInfo;
            }
            private void SavePosition()
            {
                Position = Player.transform.position.ToString().ToVector3();
            }

            private bool? IsRaidBlocked()
            {
                var plugins = new List<Plugin>
                {
                    Instance.NoEscape,
                    Instance.RaidZone,
                    Instance.RaidBlock
                };

                var correct = plugins.Find(v => v != null);
                if (correct != null)
                {
                    try
                    {
                        switch (correct.Name)
                        {
                            case "NoEscape":
                            {
                                return (bool) correct.Call("IsRaidBlocked", Player);
                            }
                            case "RaidZone":
                            {
                                return (bool) correct.Call("HasBlock", Player.userID);
                            }
                            case "RaidBlock":
                            {
                                try
                                {
                                    return (bool) correct.Call("IsInRaid", Player);
                                }
                                catch
                                {
                                    return (bool) correct.Call("IsRaidBlocked", Player);
                                }
                            }
                        } 
                    }
                    catch
                    {
                        Instance.PrintError($"Detected {correct.Name} plugin, but failed to call API");
                    }
                }

                return null;
            }

            private void Log(string text, bool isError = false)
            {
                var prepared = $"UserService-{Player.userID}: {text}";
                
                if (isError)
                {
                    Instance.PrintError(prepared);
                    return;
                }
                
                Instance.PrintWarning(prepared);
            }
        }
        
        #endregion
        
        #endregion

        #region ApiService

        #region Classes & Interfaces

        private class API : MonoBehaviour
        {
            private void Log(string module, string text, bool isError = false)
            {
                var prepared = $"ApiModule-{module}: {text}";
                
                if (isError)
                {
                    Instance.PrintError(prepared);
                    return;
                }
                
                Instance.PrintWarning(prepared);
            }
            
            public class ALERT_TYPE
            {
                public static string SIMPLE = "simple";
                public static string STASH = "stash";
            }
            
            public void Request(string module, object data, Action<Response> callback, RequestMethod method = RequestMethod.GET, float timeout = 60f)
            {
                var payload = JsonConvert.SerializeObject(new
                {
                    server = ServerInfo.Get(),
                    data
                });
                
                // Fix, for better perfomance with HTTP requests
                Instance.webrequest.Enqueue($"{Settings.ApiUri.Replace("https", "http")}/{module}", payload, (code, response) =>
                {
                    if (code != 200)
                    {
                        //API.Log(module, $"ошибка подключения [{code}] / connection error [{code}]", true);
                        callback(new Response(false));
                        return;
                    }
                    
                    try
                    {
                        var obj = JsonConvert.DeserializeObject<Response>(response);

                        callback(obj);   
                    }
                    catch(Exception exc)
                    { 
                        //API.Log(module, $"не удалось прочитать ответ на запрос / failed to parse answer", true);
                        callback(new Response(false, response));
                    }
                }, Instance, method, new Dictionary<string, string>
                {
                    ["Authorization"] = Settings.Token,
                    ["Content-Type"] = "application/json"
                }, timeout); 
            }

            public void Validate(Action callback = null)
            {
                Request("validate", null, response =>
                {
                    if (!response.Success)
                    {
                        Instance.PrintError("Can't connect to control panel");
                        Instance.PrintError($"Reason: {response.Data}");
                        Instance.PrintWarning("Check plugin configuration, and reload plugin \"o.reload RustPanel\"");
                    }
                    else
                    {
                        Instance.IS_READY = true;
                        callback?.Invoke();
                    }
                }, RequestMethod.PUT);
            }
 
            public void GetBans(BasePlayer player, Action<object> callback = null, bool retry = false)    
            {
                Request("ban", new
                { 
                    targetId = player.UserIDString,  
                    targetIp = player.Connection.ipaddress.Split(':')[0]
                }, (response) =>
                {
                    try
                    {
                        if (response.Success) 
                        {
                            var reason = (response.Data as JObject)["reason"].ToString(); 
                            var time = (response.Data as JObject)["time"].ToString();

                            var isIp = (response.Data as JObject)["ip"] != null;

                            if (time.Length == 0)
                            {
                                time = "-";
                            }

                            try
                            {
                                player.SendConsoleCommand($"echo ID:{(response.Data as JObject)["id"].ToString()}");
                            }
                            catch
                            {
                            
                            }
                        
                            player.Kick(isIp ? "Вам ограничен вход на сервер" : Settings.KickFormat.Replace("%REASON%", reason).Replace("%TIME%", time));
						
                            Instance.PrintWarning($"У игрока ({player.userID}) обнаружена блокировка, выгоняем с сервера [{reason}]");
                            callback?.Invoke(reason); 
                        }
                        else
                        {
                            var str = JsonConvert.SerializeObject(response.Data);
                            if (str != "\"NO-BAN-FOUND\"")
                            {
                                Instance.PrintWarning($"Не удалось проверить блокировки игрока ({player.userID}), повторная попытка через 1 сек...");
                                Instance.timer.Once(1f, () => GetBans(player, callback, true));
                                return;
                            }
                            
                            if (retry)
                            {
                                Instance.PrintWarning($"Удалось проверить ({player.userID}) у игрока нет блокировок");
                            }
                            
                            callback?.Invoke("TEST"); 
                        }
                    }
                    catch (Exception exc)
                    {
                        Instance.PrintWarning("Не удалось обработать проверку наличия блокировок у игрока");
                    }
                }, RequestMethod.POST, 5f);
            }
            
            public void SendUpdate(object data) 
            { 
                try
                {
                    Request("state", data, response =>
                    {
                    }, RequestMethod.POST);
                }
                catch(Exception exc)
                {
                    Instance.PrintError($"Core: ошибка отправки обновления / failed to send update data");
                }
            }    
            
            public void SendChat(BasePlayer player, string message)
            {
                Request("chat", new
                {
                    playerId = player.UserIDString,
                    message = message
                }, response =>
                {
                }, RequestMethod.POST);
            }

            public void OnDisconnected(BasePlayer player, string reason)
            {
                Request("disconnect", new
                {
                    targetId = player.UserIDString,
                    reason = reason
                }, response =>
                {
                }, RequestMethod.POST);
            }

            public void Report(BasePlayer player, BasePlayer target, string type, string description = null)
            {
                Request("report", new
                {
                    initiatorId = player.UserIDString,
                    targetId = target.UserIDString,
                    reason = type,
                    info = description
                }, response =>
                {
                }, RequestMethod.POST);
            }

            public void SendAlert(string type, object obj)
            {
                Request("alert", new
                {
                    type = type,
                    fields = obj
                }, response =>
                {
                }, RequestMethod.POST);
            }

            public void SendDiscord(BasePlayer player, string discord)
            {
                Request("discord", new
                {
                    discord = discord,
                    steamId = player.UserIDString
                }, response =>
                {
                    if (response.Success)
                    {
                        Instance.Player.Message(player, Instance.lang.GetMessage("Contact.Sent", Instance, player.UserIDString) + $"<color=#8393cd> {discord}</color>", 75435345);
                        Instance.Player.Message(player, Instance.lang.GetMessage("Contact.SentWait", Instance, player.UserIDString), 75435345);
						Instance.Puts($"Player {player.UserIDString} sent a discord for communication: {discord}");
                    }
                }, RequestMethod.POST);
            }
        }

        #endregion

        #endregion

        #region Configuration
        
        #region Classes & Interfaces
        
        private class Configuration
        {
            [JsonProperty("Server name for control panel")]
            public string Name;
            [JsonProperty("API URI (Do not change it!)")]
            public string ApiUri;
            [JsonProperty("API TOKEN (Do not change it!)")]
            public string Token;
            [JsonProperty("Unique server ID (Do not change it!)")]
            public string Hash;
	        [JsonProperty("Command will open the GUI")]
            public string ChatCommand = "reports";
            [JsonProperty("Report reasons for UI")]
            public List<string> Reasons = new List<string>();
            [JsonProperty("Report cooldown (in secs)")]
            public int ReportCooldown = 1;
            [JsonProperty("Additional scan for in-game reports (F7)")]
            public bool UseF7Reports = true;
	        [JsonProperty("Show moderator avatar, when message from control panel")]
            public bool ShowAvatar = true;
            [JsonProperty("Message from control panel format (Vars: %NAME% - moderator's name, %MSG% - message)")]
            public string MsgFormat = "<color=#B1D6F1>%NAME%</color>: %MSG%";
            [JsonProperty("Ban kick reason (%REASON% - ban reason, %TIME% - date of unban)")]
            public string KickFormat = "Вы забанены на этом сервере, причина: %REASON%";
            [JsonProperty("Ban broadcast (%TARGET% - target name, %REASON% - reason, you can keep empty for hide message")]
            public string BanFormat = "%TARGET% was banned for %REASON%";
            [JsonProperty("Direct message format (when moderator is chatting with suspected player)")]
            public string DirectFormat = "<color=orange>Сообщение от модератора:</color>\n%MSG%";

            public static Configuration Generate()
            {
                return new Configuration
                {   
                    Name = $"Server #{Oxide.Core.Random.Range(0, 999)}",
                    ApiUri = "https://api-rp6311.rustpanel.ru/api/plugin",
                    Token = "9b5cb4dbd24ace8dc50b14e26ff84639", 
                    Hash = App.serverid,
                    Reasons = new List<string>
                    {
                        "Cheat",
                        "Macros",
                        "Bug",
						"1+",
                    }
                };
            }
        }
        
        #endregion
        
        #region Implementation
        
        private void RegisterLang()
        {
		    lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Header.Find"] = "FIND PLAYER",
                ["Header.SubDefault"] = "Who do you want to report?",
                ["Header.SubFindResults"] = "Here are players, which we found",
                ["Header.SubFindEmpty"] = "No players was found",
                ["Header.Search"] = "Search",
                ["Subject.Head"] = "Select the reason for the report",
                ["Subject.SubHead"] = "For player %PLAYER%",
                ["Cooldown"] = "Wait %TIME% sec.",
                ["Sent"] = "Report succesful sent",
                ["Contact.Error"] = "You did not sent your Discord",
	            ["Contact.Sent"] = "You sent:",
	            ["Contact.SentWait"] = "If you sent the correct discord - wait for a friend request.",
                ["Check.Text"] = "<color=#c6bdb4><size=32><b>YOU ARE CALLED FOR CHECK</b></size></color>\n<color=#958D85>You have <color=#c6bdb4><b>5 minutes</b></color> to send discord, and accept friends.\nCommand for send: <b><color=#c6bdb4>/contact</color> <<color=#c6bdb4>Nick#0000</color>></b></color>"
            }, this, "en");
            
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Header.Find"] = "НАЙТИ ИГРОКА",
                ["Header.SubDefault"] = "На кого вы хотите пожаловаться?",
                ["Header.SubFindResults"] = "Вот игроки, которых мы нашли",
                ["Header.SubFindEmpty"] = "Игроки не найдены",
                ["Header.Search"] = "Поиск",
                ["Subject.Head"] = "Выберите причину репорта",
                ["Subject.SubHead"] = "На игрока %PLAYER%",
                ["Cooldown"] = "Подожди %TIME% сек.",
                ["Sent"] = "Жалоба успешно отправлена",
                ["Contact.Error"] = "Вы не отправили свой Discord",
				["Contact.Sent"] = "Вы отправили:",
				["Contact.SentWait"] = "<size=12>Если вы отправили корректный дискорд - ждите заявку в друзья.</size>",
                ["Check.Text"] = "<color=#c6bdb4><size=32><b>ВЫ ВЫЗВАНЫ НА ПРОВЕРКУ</b></size></color>\n<color=#958D85>У вас есть <color=#c6bdb4><b>5 минут</b></color> чтобы отправить дискорд и принять заявку в друзья.\nКоманда для отправки: <b><color=#c6bdb4>/contact</color> <<color=#c6bdb4>Nick#0000</color>></b></color>"
            }, this, "ru");
            
            PrintWarning("Core: языковые файлы загружены / lang files loaded");
        }

        private void RegisterCommands()
        {
            cmd.AddChatCommand(Settings.ChatCommand, this, "ChatCmdReport");
            
            PrintWarning("Core: команды зарегистрированы / commands registered");
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
        protected override void SaveConfig()        => Config.WriteObject(Settings);
        
        #endregion
        
        #endregion
        
        #region Variables

        private bool IS_READY = false;

        private static API InjectedAPI = null;
        
        private static RustPanel Instance;
        private static Configuration Settings = Configuration.Generate();
        
        // References for RB plugins to get RB status
        [PluginReference] private Plugin NoEscape, RaidZone, RaidBlock;
  
        #endregion

        #region Hooks
        
        private void OnServerInitialized()   
        {   
            Instance = this;
            
            UserService.CACHE = new List<UserService>();
            UserService.COOLDOWNS = new Dictionary<ulong, double>();
            
            InjectedAPI = ServerMgr.Instance.gameObject.AddComponent<API>();
            
            InjectedAPI.Validate(() =>
            {
                RegisterLang();
                RegisterCommands();
            
                ServerMgr.Instance.gameObject.AddComponent<QueueService>();
                ServerMgr.Instance.gameObject.AddComponent<UpdateService>();

                ServerMgr.Instance.StartCoroutine(SafeMassUpdate());
            });
        }

        private IEnumerator SafeMassUpdate()
        {
            bool isSending = false;

            int index = 0;
            
            Instance.Puts("Для лучшей производительности, происходит плавная проверка игроков на наличие блокировок");
            foreach (var check in BasePlayer.activePlayerList)
            {
                isSending = true;
                
                var exists = check.gameObject.GetComponent<UserService>();

                if (exists == null)
                {
                    exists = check.gameObject.AddComponent<UserService>();
                }

                exists.CheckBans(() =>
                {
                    isSending = false;
                });

                yield return new WaitWhile(() => isSending);

                index++;

                if (index % 20 == 0 && index != 0)
                {
                    Instance.Puts($"Процесс плавной проверки: {index}/{BasePlayer.activePlayerList.Count} игроков проверено");
                }
            }
            
            Instance.Puts($"Плавная проверка завершена: {index}/{BasePlayer.activePlayerList.Count} игроков проверено");
        }

        private void Unload()
        {
            ServerMgr.Instance.StopAllCoroutines();
            
            var queueComp = ServerMgr.Instance.gameObject.GetComponent<QueueService>();
            var updateComp = ServerMgr.Instance.gameObject.GetComponent<UpdateService>();
            var apiComp = ServerMgr.Instance.gameObject.GetComponent<API>();
            
            var userComp = UserService.CACHE;

            if (queueComp != null)
            {
                UnityEngine.Object.Destroy(queueComp);
            }

            if (updateComp != null)
            {
                UnityEngine.Object.Destroy(updateComp);
            }

            if (apiComp != null)
            {
                UnityEngine.Object.Destroy(apiComp);
            }

            if (userComp != null && userComp.Count > 0)
            {
                userComp.ForEach(UnityEngine.Object.Destroy);
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, CheckLayer);
                CuiHelper.DestroyUi(player, ReportLayer);
            }
        }
        
        private void OnPlayerConnected(BasePlayer player)
        {
            if (!IS_READY)
            {
                return;
            }

            var exists = player.GetComponent<UserService>();
            if (exists == null)
            {
                exists = player.gameObject.AddComponent<UserService>();
            }
            
            exists.CheckBans(() => {});
        }
        
        private void OnPlayerChat(BasePlayer player, string message)
        {
            if (!IS_READY)
            {
                return;
            }
            
            InjectedAPI.SendChat(player, message);
        }
        
        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (!IS_READY)
            {
                return;
            }
            
            var comp = player.GetComponent<UserService>();
            if (comp != null)
            {
                UnityEngine.Object.Destroy(comp);
            }
            
            InjectedAPI.OnDisconnected(player, reason);
        }

        private void OnStashExposed(StashContainer stash, BasePlayer player)
        {
            if (!IS_READY)
            {
                return;
            }
            
            if (stash == null)
            {
                return;
            }
            
            var team = player.Team;
            if (team != null)
            {
                if (team.members.Contains(stash.OwnerID))
                {
                    return;
                }
            }
            
            var owner = stash.OwnerID;

            if (player.userID == stash.OwnerID)
            {
                return;
            }
            
            InjectedAPI.SendAlert(API.ALERT_TYPE.STASH, new
            {
                initiator = new
                {
                    value = player.UserIDString,
                    type = "player"
                },
                target = new
                {
                    value = stash.OwnerID.ToString(),
                    type = "player"
                },
                square = new
                {
                    value = $"{GridReference(player.transform.position)}",
                    type = "text"
                }
            });
        }
        
        private void OnPlayerReported(BasePlayer reporter, string targetName, string targetId, string subject, string message, string type)
        {
            if (!IS_READY)
            {
                return;
            }
            
            if (!Settings.UseF7Reports)
            {
                return;
            }
            
            var target = BasePlayer.Find(targetId) ?? BasePlayer.FindSleeping(targetId);
            if (target == null)
            {
                return;
            }
            
            InjectedAPI.Report(reporter, target, type, message);
        }

        #endregion
        
        #region Interface Check
        
        private const string CheckLayer = "RP_PrivateLayer";

        private void DrawInterface(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, CheckLayer);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.3 0.85", AnchorMax = "0.7 0.86", OffsetMin = $"0 0", OffsetMax = $"0 120" },
                Button = { Color = "0.40 0.62 1.00 0.60", Sprite = "assets/content/ui/ui.background.gradient.psd" },
                Text = { Text = "", Align = TextAnchor.MiddleCenter }
            }, "Under", CheckLayer);

            container.Add(new CuiElement
            {
                Parent = CheckLayer,
                Components =
                {
                    new CuiTextComponent { Text = $"Вы подозреваетесь в использование стороннего ПО. Администратор вызвал Вас на проверку. Напишите свой диской через команду /discord. (Пример: /discord никнейм#1234). У вас есть 2 минуты, за выход с сервера бан Вы получите бан навсегда за отказ от проверки! Если ваш дискорд с пробелом - напишите его в общий чат.", Align = TextAnchor.MiddleCenter, FontSize = 17, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0.02 0.01", AnchorMax = "0.98 0.99"},
                    new CuiOutlineComponent { Color = "0 0 0 1", Distance = "0.5 0.5" }
                }
            });

            CuiHelper.AddUi(player, container);

            Effect effect = new Effect("ASSETS/BUNDLED/PREFABS/FX/INVITE_NOTICE.PREFAB".ToLower(), player, 0, new Vector3(), new Vector3());
            EffectNetwork.Send(effect, player.Connection);
        }

        #endregion

        #region Interface Choose

        [ConsoleCommand("UI_RP_ReportPanel")]
        private void CmdConsoleReportPanel(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player == null || !args.HasArgs(1))
            {
                return;
            }

            switch (args.Args[0].ToLower())
            {
                case "search":
                {
                    int page = args.HasArgs(2) ? int.Parse(args.Args[1]) : 0;
                    string search = args.HasArgs(3) ? args.Args[2] : ""; 
					
					Effect effect = new Effect("assets/prefabs/tools/detonator/effects/unpress.prefab", player, 0, new Vector3(), new Vector3());
                    EffectNetwork.Send(effect, player.Connection);
                    
                    DrawReportInterface(player, page, search, true);
                    break;
                }
                case "show":
                {
                    string targetId = args.Args[1];
                    BasePlayer target = BasePlayer.Find(targetId) ?? BasePlayer.FindSleeping(targetId);
					
					Effect effect = new Effect("assets/prefabs/tools/detonator/effects/unpress.prefab", player, 0, new Vector3(), new Vector3());
                    EffectNetwork.Send(effect, player.Connection);

                    CuiElementContainer container = new CuiElementContainer();
                    CuiHelper.DestroyUi(player, ReportLayer + $".T");
                    
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{args.Args[2]} {args.Args[3]}", OffsetMax = $"{args.Args[4]} {args.Args[5]}" },
                        Image = { Color = "0 0 0 1"}
                    }, ReportLayer + $".L", ReportLayer + $".T");
                    
                     
                    container.Add(new CuiButton()
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"-500 -500", OffsetMax = $"500 500" },
                        Button = { Close = $"{ReportLayer}.T", Color = "0 0 0 1", Sprite = "assets/content/ui/ui.circlegradient.png" }
                    }, ReportLayer + $".T");
                    
                    
                    bool leftAlign = bool.Parse(args.Args[6]);
                    container.Add(new CuiButton()
                    {
                        RectTransform = { AnchorMin = $"{(leftAlign ? -1 : 2)} 0", AnchorMax = $"{(leftAlign ? -2 : 3)} 1", OffsetMin = $"-500 -500", OffsetMax = $"500 500" },
                        Button = { Close = $"{ReportLayer}.T", Color = HexToRustFormat("#282828"), Sprite = "assets/content/ui/ui.circlegradient.png" }
                    }, ReportLayer + $".T");
                    
                    container.Add(new CuiButton()
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"-1111111 -1111111", OffsetMax = $"1111111 1111111" },
                        Button = { Close = $"{ReportLayer}.T", Color = "0 0 0 0.5", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
                    }, ReportLayer + $".T");


                    container.Add(new CuiLabel
                    {
                        RectTransform = {AnchorMin = $"{(leftAlign ? "0" : "1")} 0", AnchorMax = $"{(leftAlign ? "0" : "1")} 1", OffsetMin = $"{(leftAlign ? "-350" : "20")} 0", OffsetMax = $"{(leftAlign ? "-20" : "350")} -5"},
                        Text = {FadeIn = 0.4f, Text = lang.GetMessage("Subject.Head", this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 24, Align = leftAlign ? TextAnchor.UpperRight : TextAnchor.UpperLeft}
                    }, ReportLayer + ".T");

                    container.Add(new CuiLabel
                    {
                        RectTransform = {AnchorMin = $"{(leftAlign ? "0" : "1")} 0", AnchorMax = $"{(leftAlign ? "0" : "1")} 1", OffsetMin = $"{(leftAlign ? "-250" : "20")} 0", OffsetMax = $"{(leftAlign ? "-20" : "250")} -35"},
                        Text = {FadeIn = 0.4f, Text = $"{lang.GetMessage("Subject.SubHead", this, player.UserIDString).Replace("%PLAYER%", $"<b>{target.displayName}</b>")}", Font = "robotocondensed-regular.ttf", FontSize = 14, Color = "1 1 1 0.7", Align = leftAlign ? TextAnchor.UpperRight : TextAnchor.UpperLeft}
                    }, ReportLayer + ".T");
                    
                    container.Add(new CuiElement
                    {
                        Parent = ReportLayer + $".T",
                        Components =
                        {
                            new CuiRawImageComponent { Png = (string) plugins.Find("ImageLibrary").Call("GetImage", target.UserIDString) },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" } 
                        }
                    });

                    for (var i = 0; i < Settings.Reasons.Count; i++)
                    {
                        var offXMin = (20 + (i * 5)) + i * 80;
                        var offXMax = 20 + (i * 5) + (i + 1) * 80;
                        
                        container.Add(new CuiButton()
                        {
                            RectTransform = { AnchorMin = $"{(leftAlign ? 0 : 1)} 0", AnchorMax = $"{(leftAlign ? 0 : 1)} 0", OffsetMin = $"{(leftAlign ? -offXMax : offXMin)} 15", OffsetMax = $"{(leftAlign ? -offXMin : offXMax)} 45" },
                            Button = { FadeIn = 0.4f + i * 0.2f, Color = HexToRustFormat("#FFFFFF4D"), Command = $"UI_RP_ReportPanel report {target.UserIDString} {Settings.Reasons[i].Replace(" ", "0")}" },
                            Text = { FadeIn = 0.4f,Text = $"{Settings.Reasons[i]}", Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", Font = "robotocondensed-regular.ttf", FontSize = 16 }
                        }, ReportLayer + $".T");
                    }

                    CuiHelper.AddUi(player, container);
                    break;
                }
                case "report":
                {
                    string targetId = args.Args[1];
                    string reason = args.Args[2].Replace("0", "");
                    
                    BasePlayer target = BasePlayer.Find(targetId) ?? BasePlayer.FindSleeping(targetId);
                    
                    InjectedAPI.Report(player, target, reason);
                    CuiHelper.DestroyUi(player, ReportLayer);
                    
                    SoundToast(player, lang.GetMessage("Sent", this, player.UserIDString), 2);

                    if (!UserService.COOLDOWNS.ContainsKey(player.userID))
                    {
                        UserService.COOLDOWNS.Add(player.userID, 0);
                    }

                    UserService.COOLDOWNS[player.userID] = CurrentTime() + Settings.ReportCooldown;
                    break;
                }
            }
        }

        private static string ReportLayer = "UI_RP_ReportPanelUI";
        private void DrawReportInterface(BasePlayer player, int page = 0, string search = "", bool redraw = false)
        {

            var lineAmount = 6;
            var lineMargin = 8;
            
            var size = (float) (700 - lineMargin * lineAmount) / lineAmount;
            var list = BasePlayer.activePlayerList
                .ToList();
            
            var finalList = list
                .FindAll(v => v.displayName.ToLower().Contains(search) || v.UserIDString.ToLower().Contains(search) || search == null)
                .Skip(page * 18)
                .Take(18);

            if (finalList.Count() == 0)
            {
                if (search == null)
                {
                    DrawReportInterface(player, page - 1);
                    return;
                }
            }

            CuiElementContainer container = new CuiElementContainer();

            if (!redraw)
            {
                CuiHelper.DestroyUi(player, ReportLayer);
                
            
                container.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                    Image = {Color = HexToRustFormat("#282828E6"), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"}
                }, "Overlay", ReportLayer); 
            
                container.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                    Button = { Close = ReportLayer, Color = "0 0 0 0" },
                    Text = { Text = ""}
                }, ReportLayer); 
            } 

            CuiHelper.DestroyUi(player, ReportLayer + ".C");
             
            container.Add(new CuiPanel  
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-368 -200", OffsetMax = "368 142" },
                Image = { Color = "1 0 0 0" }
            }, ReportLayer, ReportLayer + ".C");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "-36 0", OffsetMax = "0 0" },
                Image = { Color = "0 0 1 0" }
            }, ReportLayer + ".C", ReportLayer + ".R");
            
            //↓ ↑

            container.Add(new CuiButton()
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.5", OffsetMin = "0 0", OffsetMax = "0 -4" },
                Button = { Color = $"0.7 0.7 0.7 {(list.Count > 18 && finalList.Count() == 18 ? 0.5 : 0.3)}", Command = list.Count > 18 && finalList.Count() == 18 ? $"UI_RP_ReportPanel search {page + 1}" : ""}, 
                Text = { Text = "↓", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 24, Color = $"0.7 0.7 0.7 {(list.Count > 18 && finalList.Count() == 18 ? 0.9 : 0.2)}"}
            }, ReportLayer + ".R", ReportLayer + ".RD");
            
            container.Add(new CuiButton() 
            {
                RectTransform = { AnchorMin = "0 0.5", AnchorMax = "1 1", OffsetMin = "0 4", OffsetMax = "0 0" },
                Button = { Color = $"0.7 0.7 0.7 {(page == 0 ? 0.3 : 0.5)}", Command = page == 0 ? "" : $"UI_RP_ReportPanel search {page - 1}"},
                Text = { Text = "↑", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 24, Color = $"0.7 0.7 0.7 {(page == 0 ? 0.2 : 0.9)}" }
            }, ReportLayer + ".R", ReportLayer + ".RU");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-250 8", OffsetMax = "0 43" },
                Image = { Color = "1 1 1 0.20" }
            }, ReportLayer + ".C", ReportLayer + ".S");
            
            container.Add(new CuiElement
            {
                Parent = ReportLayer + ".S",
                Components =
                {
                    new CuiInputFieldComponent { FontSize = 14, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleLeft, Command = "UI_RP_ReportPanel search 0 "},
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "10 0", OffsetMax = "-85 0"}
                }
            });
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "-75 0", OffsetMax = "0 0" },
                Button = { Color = "0.7 0.7 0.7 0.5" },
				Text = { Text = $"{lang.GetMessage("Header.Search", this, player.UserIDString)}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, ReportLayer + ".S", ReportLayer + ".SB");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0.5 1", OffsetMin = "0 7", OffsetMax = "0 47" },
                Image = { Color = "0.8 0.8 0.8 0" }
            }, ReportLayer + ".C", ReportLayer + ".LT");
            
            container.Add(new CuiLabel()
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = $"{lang.GetMessage("Header.Find", this, player.UserIDString)} {(search != null && search.Length > 0 ? $"- {(search.Length > 20 ? search.Substring(0, 14).ToUpper() + "..." : search.ToUpper())}" : "")}", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.UpperLeft }
            }, ReportLayer + ".LT");
            
            container.Add(new CuiLabel()
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                Text = { Text = search == null || search.Length == 0 ? lang.GetMessage("Header.SubDefault", this, player.UserIDString) : finalList.Count() == 0 ? lang.GetMessage("Header.SubFindEmpty", this, player.UserIDString) : lang.GetMessage("Header.SubFindResults", this, player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.LowerLeft, Color = "1 1 1 0.5"}
            }, ReportLayer + ".LT");

            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "-40 0" },
                Image = { Color = "0 1 0 0" }
            }, ReportLayer + ".C", ReportLayer + ".L");
            
            for (var y = 0; y < 3; y++)
            {
                for (var x = 0; x < 6; x++)
                {
                    var target = finalList.ElementAtOrDefault(y * 6 + x);
                    if (target)
                    {
                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{x * size + lineMargin * x} -{(y+1) * size + lineMargin * y}", OffsetMax = $"{(x + 1) * size + lineMargin * x} -{y * size + lineMargin * y}" },
                            Image = { Color = "0.8 0.8 0.8 1" }
                        }, ReportLayer + ".L", ReportLayer + $".{target.UserIDString}");
                    
                        container.Add(new CuiElement
                        {
                            Parent = ReportLayer + $".{target.UserIDString}",
                            Components =
                            {
                                new CuiRawImageComponent { Png = (string) plugins.Find("ImageLibrary").Call("GetImage", target.UserIDString) },
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" } 
                            }
                        });
                        
                        container.Add(new CuiPanel()
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                            Image = { Sprite = "assets/content/ui/ui.background.transparent.linear.psd", Color = HexToRustFormat("#282828f2") }
                        }, ReportLayer + $".{target.UserIDString}");

                        string normaliseName = NormalizeString(target.displayName);
                        
                        string name = normaliseName.Length > 14 ? normaliseName.Substring(0, 15) + ".." : normaliseName;
                        
                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 16", OffsetMax = "0 0" },
                            Text = { Text = name, Align = TextAnchor.LowerLeft, Font = "robotocondensed-bold.ttf", FontSize = 13, Color = HexToRustFormat("#FFFFFF")}
                        }, ReportLayer + $".{target.UserIDString}");
                        
                        container.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "6 5", OffsetMax = "0 0" },
                            Text = { Text = target.UserIDString, Align = TextAnchor.LowerLeft, Font = "robotocondensed-regular.ttf", FontSize = 10, Color = HexToRustFormat("#FFFFFF80")}
                        }, ReportLayer + $".{target.UserIDString}");
                        
                        container.Add(new CuiButton()
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
                            Button = { Color = "0 0 0 0", Command = $"UI_RP_ReportPanel show {target.UserIDString} {x * size + lineMargin * x} -{(y+1) * size + lineMargin * y} {(x + 1) * size + lineMargin * x} -{y * size + lineMargin * y}  {x >= 3}" },
                            Text = { Text = "" }
                        }, ReportLayer + $".{target.UserIDString}");
                    }
                    else
                    {
                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{x * size + lineMargin * x} -{(y+1) * size + lineMargin * y}", OffsetMax = $"{(x + 1) * size + lineMargin * x} -{y * size + lineMargin * y}" },
                            Image = { Color = "0.8 0.8 0.8 0.25" }
                        }, ReportLayer + ".L");
                    }
                }
            }
            
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Commands
		
        private void ChatCmdReport(BasePlayer player)
        {
            if (!UserService.COOLDOWNS.ContainsKey(player.userID))
            {
                UserService.COOLDOWNS.Add(player.userID, 0);
            }

            if (UserService.COOLDOWNS[player.userID] > CurrentTime())
            {
                var msg = lang.GetMessage("Cooldown", this, player.UserIDString).Replace("%TIME%",
                    $"{(UserService.COOLDOWNS[player.userID] - CurrentTime()).ToString("0")}");
                
                SoundToast(player, msg, 1);
                return;
            }
            
            DrawReportInterface(player);
        }
        

        [ChatCommand("discord")]
        private void CmdChatContact(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                Player.Message(player, lang.GetMessage("Contact.Error", this, player.UserIDString), 75435345);
                return;
            }

            string result = ""; 
            foreach (var check in args)     
                result += check + " "; 
			
            InjectedAPI.SendDiscord(player, result);
        }

        #endregion

        #region Helpers
        
        private static string GridReference(Vector3 position)
        {
            var chars = new string[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z", "AA", "AB", "AC", "AD", "AE", "AF", "AG", "AH", "AI", "AJ", "AK", "AL", "AM", "AN", "AO", "AP", "AQ", "AR", "AS", "AT", "AU", "AV", "AW", "AX", "AY", "AZ" };

            const float block = 146;

            float size = ConVar.Server.worldsize;
            float offset = size / 2;

            float xpos = position.x + offset;
            float zpos = position.z + offset;

            int maxgrid = (int)(size / block);

            float xcoord = Mathf.Clamp(xpos / block, 0, maxgrid - 1);
            float zcoord = Mathf.Clamp(maxgrid - (zpos / block), 0, maxgrid - 1);

            string pos = string.Concat(chars[(int)xcoord], (int)zcoord);

            return (pos);
        }

        private void SoundToast(BasePlayer player, string text, int type)
        {
            Effect effect = new Effect("assets/bundled/prefabs/fx/notice/item.select.fx.prefab", player, 0, new Vector3(), new Vector3());
            EffectNetwork.Send(effect, player.Connection);
						
            player.Command("gametip.showtoast", type, text);
        }
        
        private double CurrentTime() => DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;  
        
        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                hex = "#FFFFFFFF";
            }

            var str = hex.Trim('#');

            if (str.Length == 6)
                str += "FF";

            if (str.Length != 8)
            {
                throw new Exception(hex);
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            Color color = new Color32(r, g, b, a);

            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }
        
        private static List<char> Letters = new List<char> {'☼', 's', 't', 'r', 'e', 'т', 'ы', 'в', 'о', 'ч', 'х', 'а', 'р', 'u', 'c', 'h', 'a', 'n', 'z', 'o', '^', 'm', 'l', 'b', 'i', 'p', 'w', 'f', 'k', 'y', 'v', '$', '+', 'x', '1', '®', 'd', '#', 'г', 'ш', 'к', '.', 'я', 'у', 'с', 'ь', 'ц', 'и', 'б', 'е', 'л', 'й', '_', 'м', 'п', 'н', 'g', 'q', '3', '4', '2', ']', 'j', '[', '8','{', '}', '_' ,'!', '@', '#', '$', '%', '&', '?', '-', '+', '=', '~', ' ', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', 'а', 'б', 'в', 'г', 'д', 'е', 'ё', 'ж', 'з', 'и', 'й', 'к', 'л', 'м', 'н', 'о', 'п', 'р', 'с', 'т', 'у', 'ф', 'х', 'ц', 'ч', 'ш', 'щ', 'ь', 'ы', 'ъ', 'э', 'ю', 'я'};

        private static string NormalizeString(string text)
        {
            string name = "";
            
            foreach (var @char in text)
            { 
                if (Letters.Contains( @char.ToString().ToLower().ToCharArray()[0]))  
                    name += @char;
            }

            return name;
        }

        #endregion

        #region PluginAPI

        private void IAlert(string message)
        {
            InjectedAPI.SendAlert(API.ALERT_TYPE.SIMPLE, new
            {
                message = new
                {
                    value = message,
                    type = "text"
                }
            });
        }
        
        private void IReportPlayer(BasePlayer player, BasePlayer target, string type, string description = null)
        {
            InjectedAPI.Report(player, target, type, description);
        }
        
        #endregion
    }
}
