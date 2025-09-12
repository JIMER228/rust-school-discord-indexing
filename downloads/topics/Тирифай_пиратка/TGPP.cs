// -------------- PLUGIN CODE MACROSES --------------

// If you want to select and older version for the plugin, remove '//' near your version:

//#define RUST_DEVBLOG_198
//#define RUST_DEVBLOG_266

#if RUST_DEVBLOG_198
//#define USE_HARMONY
#define RUST_OLD_STEAM_AUTH
#define RUST_DISABLE_EASYANTICHEAT
#define OLD_NETWORKABLE
#define OLD_ITEMS
//#define RUST_OLD_HARMONY
//#define RUST_ORIGINAL_WIPE_ID
//#define RUST_TEAMS
//#define RUST_NPC
#define RUST_OLD_STEAMWORKS
#define RUST_NO_HELICOPTERS

#elif RUST_DEVBLOG_266
//#define USE_HARMONY
#define RUST_OLD_STEAM_AUTH
//#define RUST_DISABLE_EASYANTICHEAT
//#define RUST_OLD_HARMONY
//#define RUST_ORIGINAL_WIPE_ID
#define RUST_TEAMS
#define RUST_NPC
#define RUST_NO_HELICOPTERS
//#define RUST_OLD_STEAMWORKS
//#define OLD_ITEMS
#define OLD_NETWORKABLE

#else

#define RUST_LATEST
#define USE_HARMONY
//#define RUST_OLD_STEAM_AUTH
//#define RUST_DISABLE_EASYANTICHEAT
//#define RUST_OLD_HARMONY
#define RUST_ORIGINAL_WIPE_ID
#define RUST_TEAMS
#define RUST_NPC
//#define RUST_OLD_STEAMWORKS

#endif

//#define STAGE

#if USE_HARMONY

#if RUST_OLD_HARMONY
using Harmony;
#else
using HarmonyLib;
#endif

#endif

using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

// Reference: 0Harmony

namespace Oxide.Plugins
{
    [Info("TGPP", "", "1.3.6")]
    public class TGPP : RustPlugin
    {
        public const int PirateEncryptionLevel = 1;
        public const int MinTokenLength = 234;
        public const int AppIdOffset = 72;

        public const int CheatDetectIntervalSeconds = 300;
        public const int SessionsCheckIntervalSeconds = 60;

        internal readonly static Dictionary<string, PlayerHwid> _playerHwids = new Dictionary<string, PlayerHwid>();

        private static TGPP Singleton;

        [PluginReference] private Plugin MultiFighting;
        [PluginReference] private Plugin TirifyGamePluginRust;

        #region [ Hooks ]

        private void Loaded()
        {
            Singleton = this;

            try
            {
                InitializeReflection();

                DetectOtherNS();
#if USE_HARMONY
                PatchManager.PatchAll();
#endif
                RegisterPirateController();

                RegisterCheatDetect();
            }
            catch (Exception ex)
            {
                PrintError($"Failed to initialize plugin: {ex}");

                NextTick(() => Interface.Oxide.UnloadPlugin(this.Name));
            }
        }

        private void OnClientDisconnected(Connection connection, string reason)
        {
            PirateController.Singleton.TirifyLicenseUsers.Remove(connection.userid.ToString());

            TirifyProxyExt.LeaveEasyAntiCheat(connection);
        }

        private object OnUserApprove(Network.Connection connection)
        {
            if (connection.token.Length < MinTokenLength)
            {
                return null;
            }

            if (DeveloperList.Contains(connection.userid))
            {
                return null;
            }

            ConnectionAuth.m_AuthConnection.Add(connection);

            try
            {
                uint appId = GetTokenAppId(connection.token);

                // лицушник
                if (appId != 480)
                {
                    CheckPlayerProfile(connection);
                    return null;
                }

                var tirifyToken = TirifyTicket.Get(connection);

                if (_config.AuthConfiguration.SecurePirateClient && tirifyToken?.IsValid != true)
                {
                    if (tirifyToken == null)
                    {
                        // не наш пират
                        KickPlayer(connection.userid, _config.AuthConfiguration.UnauthorizedMessage);
                    }
                    else if (tirifyToken.IsValid == false)
                    {
                        // наш пират с повреждённым токеном
                        KickPlayer(connection.userid, "Клиент повреждён. Проверьте целостность файлов.");
                    }

                    return false;
                }

                if (_config.AuthConfiguration.SteamAuth)
                {
                    timer.Once(_config.AuthConfiguration.TokenAuthDelay, () =>
                    {
                        API.Player.SteamAuth(connection, (valid) => SteamPlayerAuthed(connection, tirifyToken, valid));
                    });
                }
                else
                {
                    SteamPlayerAuthed(connection, tirifyToken);
                }

                return false;
            }
            catch
            {
            }

            return null;
        }

        private void OnPluginLoaded(Plugin plugin)
        {
            if (plugin != null && plugin != this)
            {
                DetectOtherNS();
            }
        }

        #endregion

        #region [ Config ]

        public class Configuration
        {
            [JsonProperty("Кикать игроков с VPN/Proxy?")] public bool KickVpn = false;

            [JsonProperty("Кикать игроков, если есть хотя бы 1 блокировка в любой игре?")] public bool KickBanned = false;

            [JsonProperty("Кикать игроков, если аккаунт зарегистрирован меньше, чем надо?")] public bool KickRecentRegistered = false;

            [JsonProperty("Минимальное количество дней, сколько должен быть зарегистрирован аккаунт")]
            public MinimumDaysRegisteredConfiguration MinimumDaysRegisteredConfig = new MinimumDaysRegisteredConfiguration();

            [JsonProperty("Проверка файлов")] public FilesConfiguration FilesConfiguration = new FilesConfiguration();

            [JsonProperty("Проверка пиратов")] public AuthConfiguration AuthConfiguration = new AuthConfiguration();

            [JsonProperty("Конфигурация античита")] public AnticheatConfiguration AnticheatConfiguration = new AnticheatConfiguration();
        }

        public class FilesConfiguration
        {
            [JsonProperty("Проверять конкретные файлы игрока?")] public bool CheckFiles = false;

            [JsonProperty("Файлы для проверки (MD5 хеши)")] public Dictionary<string, HashSet<string>> FilesHashes = new Dictionary<string, HashSet<string>>();
        }

        public class AuthConfiguration
        {
            [JsonProperty("Проверять пиратов через Steam?")] public bool SteamAuth = true;

            [JsonProperty("Писать информацию о ПК пиратов в консоль?")] public bool LogDetails = false;

            [JsonProperty("Писать с какого клиента зашёл игрок (лицензия/Tirify/иное)")] public bool LogUserClientType = false;

            [JsonProperty("Steam ключ для проверки (https://steamcommunity.com/dev/apikey)")] public string SteamApiKey = string.Empty;

            [JsonProperty("Задержка в секундах перед проверкой токена Steam")] public uint TokenAuthDelay = 2;

            [JsonProperty("Запретить вход с чужих клиентов, кроме Tirify (не рекомендуется выключать)")] public bool SecurePirateClient = true;

            [JsonProperty("Сообщение для игроков, заходящих других клиентов")] public string UnauthorizedMessage = "Скачайте клиент Rust с <color=#1dacd6><u>forum.alkad.org</u></color> или выберите в лаунчере <u>Rust Tirify</u>";
        }

        public class MinimumDaysRegisteredConfiguration
        {
            [JsonProperty("Для пиратов")] public uint DaysForTirifyLicense = 3;

            [JsonProperty("Для лицензии")] public uint DaysForSteam = 3;
        }

        public class AnticheatConfiguration
        {
            [JsonProperty("Автоматически банить пиратов с читерской активностью на ПК (Требуется плагин TirifyGamePluginRust)")] public bool AutoBan = true;

            [JsonProperty("WebHook для уведомлений о читерах")] public string Webhook = string.Empty;

            [JsonProperty("Проверять связь с клиентом Tirify")] public bool CheckSessions = true;
        }

        internal static Configuration _config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<Configuration>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        #endregion

        #region [ Plugin Hooks ]

        [HookMethod("IsPlayerNoSteam")]
        private bool IsPlayerNoSteam(string steamId)
        {
            Connection playerConnection = TirifyGameExt.FindConnection(ulong.Parse(steamId ?? "0"));

            return IsPlayerNoSteamInternal(playerConnection);
        }

        [HookMethod("IsPlayerNoSteamInternal")]
        private bool IsPlayerNoSteamInternal(Connection playerConnection)
        {
            if (playerConnection == null || playerConnection.token == null || playerConnection.token.Length < 234)
            {
                return false;
            }

            return GetTokenAppId(playerConnection.token) == 480;
        }

        [HookMethod("IsSteam")]
        private bool IsSteam(Network.Connection connection) => !IsPlayerNoSteam(connection.userid.ToString());

        [HookMethod("GetNoSteamCount")]
        private int GetNoSteamCount() => BasePlayer.activePlayerList.Count(f => GetTokenAppId(f.Connection.token) == 480);

        [HookMethod("GetPlayerHwid")]
        private Dictionary<string, string> GetPlayerHwid(string steamId)
        {
            PlayerHwid playerHwid = null;

            if (_playerHwids.TryGetValue(steamId, out playerHwid))
            {
                return new Dictionary<string, string>()
                {
                    { "computerId", playerHwid.ComputerId },
                    { "internalId", playerHwid.InternalId },
                    { "sessionId", playerHwid.SessionId },
                    { "mac", playerHwid.Mac }
                };
            }

            return null;
        }

        #endregion

        #region [ Functions ]

        private void RegisterPirateController()
        {
            try
            {
                var pirateControllerObject = GameObject.Find("PirateControllerObject");

                HashSet<string> tirifyLicenseUsers = new HashSet<string>();

                // копируем пиратов из прошлого инстанса если он есть
                if (pirateControllerObject != null)
                {
                    try
                    {
                        foreach (var oldComponent in pirateControllerObject.GetComponents<Component>())
                        {
                            var oldUsersProperty = oldComponent.GetType().GetProperty("TirifyLicenseUsers", BindingFlags.Public | BindingFlags.Instance);

                            if (oldUsersProperty != null)
                            {
                                HashSet<string> newTirifyLicenseUsers = oldUsersProperty.GetValue(oldComponent, null) as HashSet<string>;

                                foreach (var user in newTirifyLicenseUsers)
                                {
                                    if (TirifyGameExt.FindAwakeOrSleeping(user) != null)
                                    {
                                        tirifyLicenseUsers.Add(user);
                                    }
                                }

                                Puts($"Recovered {tirifyLicenseUsers.Count} pirates from old plugin");
                                break;
                            }
                        }
                    }
                    finally
                    {
                        GameObject.DestroyImmediate(pirateControllerObject);
                    }
                }

                pirateControllerObject = new GameObject("PirateControllerObject");

                PirateController.Singleton = pirateControllerObject.AddComponent<PirateController>();
                PirateController.Singleton.Init(tirifyLicenseUsers);

                UnityEngine.Object.DontDestroyOnLoad(pirateControllerObject);

                timer.Repeat(10, 0, () =>
                {
                    try
                    {
                        PirateController.Singleton.RefreshTirifyLicenseUsers();

                        PirateController.Singleton.ReviseJoiningPlayers();
                    }
                    catch { }
                });
            }
            catch (Exception ex)
            {
                Singleton.PrintError($"Exception in RegisterPirateController(): " + ex.Message);
            }
        }

        private void RegisterCheatDetect()
        {
            if (!_config.AnticheatConfiguration.AutoBan && string.IsNullOrEmpty(_config.AnticheatConfiguration.Webhook))
            {
                return;
            }

            timer.Repeat(CheatDetectIntervalSeconds, 0, AnticheatModule.DetectPlayers);
        }

        private void InitializeReflection()
        {
            try
            {
                TirifyGameExt.UpdateServerInformationMethod = typeof(ServerMgr)
                    .GetMethod("UpdateServerInformation", BindingFlags.NonPublic | BindingFlags.Instance);

                TirifyProxyExt.EACServer_OnAuthenticatedLocal = typeof(EACServer).GetMethod("OnAuthenticatedLocal", BindingFlags.NonPublic | BindingFlags.Static);
                TirifyProxyExt.EACServer_OnAuthenticatedRemote = typeof(EACServer).GetMethod("OnAuthenticatedRemote", BindingFlags.NonPublic | BindingFlags.Static);
            }
            catch (Exception ex)
            {
                Singleton.PrintError($"Exception in InitializeReflection(): " + ex.Message);
            }
        }

        private void DetectOtherNS(Plugin plugin = null)
        {
            try
            {
                // смотрим конкретный плагин (при его загрузке/выгрузке)
                if (plugin != null)
                {
                    if (plugin.Filename.Contains("MultiFighting"))
                    {
                        DisableNS("'Luma [TirifyLicense]' was loaded");
                    }
                }
                // смотрим всё
                else
                {
                    if (MultiFighting != null)
                    {
                        DisableNS("'Luma [TirifyLicense]' was loaded");
                    }
                }
            }
            catch (Exception ex)
            {
                Singleton.PrintError($"Exception in DetectOtherNS(): " + ex.Message);
            }
        }

        private void DisableNS(string reason = null)
        {
            Puts($"Auto disabled TirifyLicense {(reason != null ? $": {reason}" : string.Empty)}");

#if USE_HARMONY
            PatchManager.UnpatchAll();
#endif
        }

        private static uint GetTokenAppId(byte[] token)
            => BitConverter.ToUInt32(token, AppIdOffset);

        private static void KickPlayer(ulong steamId, string reason)
        {
            var connection = Net.sv.connections.Find(f => f.userid == steamId);

            if (connection != null && connection.active)
            {
                ConnectionAuth.Reject(connection, reason);
                return;
            }
        }

        private static void CheckPlayerProfile(Connection connection, TirifyTicket.TirifyToken tirifyToken = null)
        {
            if (connection == null || connection.rejected) return;

            if (_config.AuthConfiguration.LogUserClientType)
            {
                LogPlayerClientType(connection, tirifyToken);
            }

            if (_config.KickBanned || _config.KickVpn || _config.KickRecentRegistered)
            {
                API.Player.CheckProfile(connection.userid, TirifyGameExt.IPAddressWithoutPort(connection));
            }

            if (tirifyToken?.IsValid == true)
            {
                if (_config.FilesConfiguration.CheckFiles && _config.FilesConfiguration.FilesHashes.Count > 0)
                {
                    API.Player.CheckFiles(connection.userid, tirifyToken.SessionId);
                }

                if (_config.AuthConfiguration.LogDetails)
                {
                    API.Player.CheckPlayerDetails(connection.userid, tirifyToken.SessionId);
                }

                API.Player.CheckPlayerSession(connection.userid, tirifyToken.SessionId, (steamId, sessionId) =>
                {
                    if (_config.AnticheatConfiguration.CheckSessions)
                    {
                        KickPlayer(steamId, "Связь с клиентом Tirify потеряна. Проверьте целостность файлов");
                    }
                    else
                    {
                        Singleton.Puts($"Игрок [{connection.username}:{connection.userid}] зашёл с невалидной сессией, возможно читер. (Включите \"Проверять связь с клиентом Tirify\" в конфиге, чтобы запретить вход)");
                    }
                });
            }
        }

        private static void LogPlayerClientType(Connection connection, TirifyTicket.TirifyToken tirifyToken)
        {
            string clientType = "Лицензии";

            if (GetTokenAppId(connection.token) == 480)
            {
                clientType = tirifyToken?.IsValid == true
                    ? "Tirify клиента"
                    : "Non-Tirify клиента";
            }

            Singleton.Puts($"Игрок [{connection.username}:{connection.userid}] присоединился с {clientType}");
        }

        private bool IsNoSteamInternal(string steamId) => PirateController.Singleton?.TirifyLicenseUsers?.Contains(steamId) ?? false;

        private void SteamPlayerAuthed(Connection connection, TirifyTicket.TirifyToken tirifyToken = null, bool valid = true)
        {
            if (!valid)
            {
                KickPlayer(connection.userid, "Steam Auth Failed (Try Restart Steam)");
                return;
            }

            TirifyProxyExt.AuthPiratePlayer(connection, ServerMgr.Instance.GetComponent<ConnectionAuth>());

            var steamId = connection.userid.ToString();

            if (tirifyToken != null)
            {
                _playerHwids[steamId] = new PlayerHwid()
                {
                    ComputerId = tirifyToken.ComputerId,
                    InternalId = tirifyToken.InternalId,
                    Mac = tirifyToken.Mac,
                    SessionId = tirifyToken.SessionId,
                };

                CheckPlayerProfile(connection, tirifyToken);
            }

            Interface.Call("OnPiratePlayerAuthed", steamId, GetPlayerHwid(steamId));
        }

        #endregion

        #region [ API ]

        public class API
        {
            public class URL
            {
                public const string Auth = "server-player-auth";

                public const string Telemetry = "game-telemetry";

                internal readonly static Dictionary<string, List<string>> _urls = new Dictionary<string, List<string>>()
                {
                    {
                        Auth, new List<string>()
                        {
#if !STAGE
                            "https://s01-server-player-auth-gateway.tirify.com",
#else
                            "https://s01-server-player-auth-gateway-in-stage.tirify.com",
#endif
                        }
                    },
                    {
                        Telemetry, new List<string>()
                        {
#if !STAGE
                            "https://s01-game-telemetry-gateway.tirify.com",
#else
                            "https://s01-game-telemetry-gateway-in-stage.tirify.com",
#endif
                        }
                    }
                };

                public static int CurrentServer { get; internal set; }

                public static string Get(string name, int index = 0)
                {
                    // out of range
                    if (index >= _urls.Count)
                    {
                        return null;
                    }

                    var url = _urls[name].ElementAtOrDefault(index);

                    if (url == null)
                    {
                        return Get(name, index - 1);
                    }

                    return url;
                }

                public static string GetCurrent(string name)
                {
                    return Get(name, CurrentServer);
                }
            }

            public class Player
            {
                private static string InfoUrl = URL.GetCurrent(URL.Auth) + "/api/v5/player/info";

                private static string FilesUrl = URL.GetCurrent(URL.Telemetry) + "/api/v1/telemetry/get/files";

                private static string DetailsUrl = URL.GetCurrent(URL.Telemetry) + "/api/v1/telemetry/get/details";

                private static string DetectUrl = URL.GetCurrent(URL.Telemetry) + "/api/v1/detect/getDetects";

                private static readonly string[] DefaultKeys = { "0F802634524A26C38F593A1C25FE547E", "A469CFF365D2335CB2DA5400FDA32265", "F26D45AB8146B53C956DA6F899A2FEA0" };

                public static void CheckProfile(ulong steamId, string ipAddress)
                {
                    string requestUrl = InfoUrl + $"?steamid={steamId}&ip={ipAddress}";

                    Singleton.webrequest.Enqueue(requestUrl, string.Empty, (code, response) =>
                    {
                        if (code >= 300 || string.IsNullOrEmpty(response))
                        {
                            Singleton.PrintWarning($"Failed to check player profile or IP ({code}): {response}");
                            return;
                        }

                        try
                        {
                            var profileInfo = JsonConvert.DeserializeObject<PlayerProfileInfo>(response);

                            string reason = null;

                            if (!PlayerAuthorizator.ValidateProfile(profileInfo, steamId, ref reason))
                            {
                                KickPlayer(steamId, reason);
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            Singleton.PrintWarning($"Exception in player profile check ({code}): {ex}");
                        }
                    }, Singleton);
                }

                public static void CheckPlayerDetails(ulong steamId, string sessionId)
                {
                    string requestUrl = DetailsUrl + $"/{sessionId}";

                    Singleton.webrequest.Enqueue(requestUrl, string.Empty, (code, response) =>
                    {
                        if (code >= 300)
                        {
                            return;
                        }

                        try
                        {
                            var playerInfo = JsonConvert.DeserializeObject<PlayerDetailsInfo>(response);

                            if (playerInfo == null)
                            {
                                return;
                            }

                            if (!playerInfo.Success)
                            {
                                Singleton.PrintWarning($"Failed to show player computer parts: {response}");
                                return;
                            }

                            if (playerInfo.Session?.DetailsJson == null)
                            {
                                return;
                            }

                            DetailsInfo detailsInfo = JsonConvert.DeserializeObject<DetailsInfo>(playerInfo.Session.DetailsJson);

                            if (detailsInfo != null)
                            {
                                Singleton.Puts(
                                    BuildPlayerComputerLog(steamId, detailsInfo));
                            }
                        }
                        catch
                        {
                        }
                    }, Singleton);
                }

                public static void CheckPlayerSession(ulong steamId, string sessionId, Action<ulong, string> invalidCallback)
                {
                    string requestUrl = DetailsUrl + $"/{sessionId}";

                    Singleton.webrequest.Enqueue(requestUrl, string.Empty, (code, response) =>
                    {
                        if (code == 404)
                        {
                            invalidCallback(steamId, sessionId);
                        }
                    }, Singleton);
                }

                public static void CheckFiles(ulong steamId, string sessionId)
                {
                    string requestUrl = FilesUrl + $"/{sessionId}";

                    Singleton.webrequest.Enqueue(requestUrl, string.Empty, (code, response) =>
                    {
                        if (code >= 300)
                        {
                            Singleton.PrintWarning($"Failed to check player files: ({code}): {response}");
                            return;
                        }

                        try
                        {
                            var filesInfo = JsonConvert.DeserializeObject<PlayerFilesInfo>(response);

                            string reason = null;

                            if (!PlayerAuthorizator.ValidateFiles(filesInfo, ref reason))
                            {
                                KickPlayer(steamId, "Invalid client files. Update or reinstall client");

                                Singleton.PrintWarning($"Player {steamId} had bad file: {reason}");
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            Singleton.PrintWarning($"Exception in player file check ({code}): {ex}");
                        }
                    }, Singleton);
                }

                public static void SteamAuth(Connection connection, Action<bool> callback = null)
                {
                    string requestUrl = GetRequestUrl(connection.token);

                    Singleton.webrequest.Enqueue(requestUrl, string.Empty, (code, response) =>
                    {
                        if (code != 200 || string.IsNullOrEmpty(response))
                        {
                            Singleton.PrintError($"Invalid request response from steam. Code: {code}, response: {response}");
                            return;
                        }

                        try
                        {
                            var steamResponse = JsonConvert.DeserializeObject<SteamUserAuthRequestResponse>(response);

                            if (steamResponse?.Response?.Params == null)
                            {
                                callback(false);
                                return;
                            }

                            if (!steamResponse.Response.Params.Result.Equals("ok", StringComparison.OrdinalIgnoreCase)
                                || steamResponse.Response.Params.SteamId != steamResponse.Response.Params.OwnerSteamId)
                            {
                                callback(false);
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            Singleton.PrintError($"Exception in OnSteamResponse: {ex}");
                        }

                        callback(true);

                    }, Singleton);
                }

                public static void DetectCheats(string[] steamIds, Action<Dictionary<string, string>> callback)
                {
                    Singleton.webrequest.Enqueue(DetectUrl, JsonConvert.SerializeObject(steamIds), (code, response) =>
                    {
                        if (code != 200 || string.IsNullOrEmpty(response))
                        {
                            Singleton.PrintError($"Invalid request response from cheat detect service. Code: {code}, response: {response}");
                            return;
                        }

                        try
                        {
                            var detectedPlayers = JsonConvert.DeserializeObject<Dictionary<string, string>>(response);

                            callback(detectedPlayers);
                        }
                        catch (Exception ex)
                        {
                            Singleton.PrintError($"Exception in DetectCheats: {ex}");
                        }
                    }, Singleton, Core.Libraries.RequestMethod.POST, new Dictionary<string, string>()
                    {
                        {  "Content-Type", "application/json" }
                    });
                }

                public static void SendWebhookCheatAlertMessage(string webhookUrl, string steamId, string cheatName)
                {
                    var message = new
                    {
                        embeds = new object[]
                        {
                            new
                            {
                                title = "**Обнаружен читер!**",
                                description =
                                $"* SteamID: **{steamId}**" +
                                $"\n* Сервер: **{ConVar.Server.hostname}**" +
                                $"\n* Чит: **{cheatName}**",
                                color = 16711680
                            }
                        }
                    };

                    Singleton.webrequest.Enqueue(webhookUrl, JsonConvert.SerializeObject(message), (code, response) =>
                    {
                        if (code >= 300 || response == null)
                        {
                            Singleton.PrintWarning($"Не удалось отправить информацию о читере ({code}): {response}");
                        }
                    }, Singleton, Core.Libraries.RequestMethod.POST, new Dictionary<string, string>()
                    {
                        { "Content-Type", "application/json" }
                    });
                }

                private static string GetRequestUrl(byte[] token)
                {
                    string steamApiKey = _config.AuthConfiguration.SteamApiKey;

                    if (steamApiKey.Length != 32)
                    {
                        steamApiKey = DefaultKeys[UnityEngine.Random.Range(0, DefaultKeys.Length)];

                        Singleton.PrintWarning("Steam Api Key не выставлен в конфиге");
                    }

                    return "https://api.steampowered.com/ISteamUserAuth/AuthenticateUserTicket/v1/?key={key}&appid={appid}&ticket={ticket}"
                        .Replace("{key}", steamApiKey)
                        .Replace("{appid}", "480")
                        .Replace("{ticket}", BitConverter.ToString(token).Replace("-", ""));
                }

                private static string BuildPlayerComputerLog(ulong steamId, DetailsInfo detailsInfo)
                {
                    var builder = new StringBuilder();

                    builder.AppendLine($"Player {steamId} computer info:");
                    builder.AppendLine($"- Processor: {detailsInfo.Cpu?.Name} | VideoAdapter: {detailsInfo.Video}");
                    builder.AppendLine($"- Memory: {detailsInfo.Memory} | Computer Name: {detailsInfo.System}");
                    builder.AppendLine($"- MAC #1: {detailsInfo.Mac?.V1} | MAC #2: {detailsInfo.Mac?.V2}");

                    return builder.ToString();
                }
            }
        }

        #endregion

        #region [ Player Authorizator ]

        public class PlayerAuthorizator
        {
            public static bool ValidateProfile(PlayerProfileInfo playerProfile, ulong steamId, ref string reason)
            {
                if (playerProfile.Steam != null)
                {
                    if (_config.KickBanned)
                    {
                        if (playerProfile.Steam.NumberOfVacBans > 0 || playerProfile.Steam.NumberOfGameBans > 0)
                        {
                            reason = "VAC or Game banned in any game!";
                            return false;
                        }
                    }

                    if (_config.KickRecentRegistered)
                    {
                        var createdTimeElapsed = DateTime.Now - TirifyGameExt.FromUnixTimeSeconds(long.Parse(playerProfile.Steam.TimeCreated));

                        bool isPlayerTirifyLicense = Singleton.IsPlayerNoSteam(steamId.ToString());

                        bool kickAsTirifyLicense = isPlayerTirifyLicense && createdTimeElapsed.TotalDays < _config.MinimumDaysRegisteredConfig.DaysForTirifyLicense;
                        bool kickAsSteam = !isPlayerTirifyLicense && createdTimeElapsed.TotalDays < _config.MinimumDaysRegisteredConfig.DaysForSteam;

                        if (kickAsTirifyLicense || kickAsSteam)
                        {
                            reason = "Account has been registered for too few days!";
                            return false;
                        }
                    }
                }

                if (playerProfile.Ip != null && _config.KickVpn)
                {
                    if (playerProfile.Ip.Hosting || playerProfile.Ip.Proxy)
                    {
                        reason = "Proxy/Hosting detected!";
                        return false;
                    }
                }

                return true;
            }

            public static bool ValidateFiles(PlayerFilesInfo fileInfo, ref string reason)
            {
                var playerFiles = fileInfo.Files.Values.FirstOrDefault();

                if (playerFiles == null || playerFiles.Count == 0)
                {
                    return false;
                }

                foreach (var file in playerFiles)
                {
                    HashSet<string> fileHashes = null;

                    if (_config.FilesConfiguration.FilesHashes.TryGetValue(file.Name, out fileHashes))
                    {
                        if (!fileHashes.Contains(file.Hash))
                        {
                            reason = file.Name;
                            return false;
                        }
                    }
                }

                return true;
            }
        }

        #endregion

        #region [ Pirate Controller ]

        public class PirateController : MonoBehaviour
        {
            public static PirateController Singleton;

            public HashSet<string> TirifyLicenseUsers { get; private set; }

            public void Init(HashSet<string> tirifyLicenseUsers)
            {
                this.TirifyLicenseUsers = tirifyLicenseUsers;

#if USE_HARMONY
                PatchManager.PatchAll();
#endif
            }

            public void RefreshTirifyLicenseUsers()
            {
                var tirifyLicenseUsers = new HashSet<string>();

                foreach (var user in TirifyLicenseUsers)
                {
                    if (Net.sv.connections.Exists(f => f.userid.ToString() == user))
                    {
                        tirifyLicenseUsers.Add(user);
                    }
                }

                this.TirifyLicenseUsers = tirifyLicenseUsers;
            }

            public void ReviseJoiningPlayers()
            {
                var queue = ServerMgr.Instance.connectionQueue;

                foreach (var connection in queue.joining)
                {
                    if (!connection.active || connection.rejected)
                    {
                        TGPP.Singleton.NextTick(() =>
                            queue.RemoveConnection(connection));
                    }
                }
            }

            private void OnDestroy()
            {
#if USE_HARMONY
                PatchManager.UnpatchAll();
#endif
            }
        }

        #endregion

        #region [ Patch Manager ]

        private static class PatchManager
        {
#if USE_HARMONY

            public const string HarmonyId = "com.tirify.patchmanager.ns";

#if RUST_OLD_HARMONY
            private static HarmonyInstance _harmony;
#else
            private static Harmony _harmony;
#endif

            private static int _lastEncryptionLevel = 0;

            public static void PatchAll()
            {
                _harmony = new Harmony(HarmonyId);
                _harmony.UnpatchAll(HarmonyId);

                var tirifyLicensePatches = new Dictionary<HarmonyMethod, HarmonyMethod[]>();

                tirifyLicensePatches.Add(
                    new HarmonyMethod(typeof(SteamInventory), "UpdateSteamInventory"),
                    new HarmonyMethod[] { new HarmonyMethod(typeof(PatchManager), nameof(Prefix_UpdateSteamInventory)) }
                );

                tirifyLicensePatches.Add(
                    new HarmonyMethod(typeof(SteamInventory), "HasItem"),
                    new HarmonyMethod[] { new HarmonyMethod(typeof(PatchManager), nameof(Prefix_SteamInventory_HasItem)) }
                );

                tirifyLicensePatches.Add(
                    new HarmonyMethod(typeof(SteamDLCItem), "HasLicense"),
                    new HarmonyMethod[] { new HarmonyMethod(typeof(PatchManager), nameof(Prefix_SteamDLCItem_HasLicense)) }
                );

                tirifyLicensePatches.Add(
                    new HarmonyMethod(typeof(ServerMgr), "JoinGame"),
                    new HarmonyMethod[]
                    {
                        new HarmonyMethod(typeof(PatchManager), nameof(Prefix_ServerMgr_JoinGame)),
                        new HarmonyMethod(typeof(PatchManager), nameof(Postfix_ServerMgr_JoinGame))
                    }
                );

                tirifyLicensePatches.Add(
                    new HarmonyMethod(typeof(Facepunch.Rust.EventRecord), "Submit"),
                    new HarmonyMethod[] { new HarmonyMethod(typeof(PatchManager), nameof(Prefix_EventRecord_Submit)) }
                );

                tirifyLicensePatches.Add(
                    new HarmonyMethod(typeof(EACServer), "get_CanSendReports"),
                    new HarmonyMethod[] { new HarmonyMethod(typeof(PatchManager), nameof(Prefix_EACServer_CanSendReports)) }
                );

                tirifyLicensePatches.Add(
                    new HarmonyMethod(typeof(ServerMgr), "UpdateServerInformation"),
                    new HarmonyMethod[]
                    {
                        null,
                        new HarmonyMethod(typeof(PatchManager), nameof(Postfix_ServerMgr_UpdateServerInformation))
                    }
                );

                tirifyLicensePatches.Add(
                    new HarmonyMethod(typeof(BasePlayer), "OnDisconnected"),
                    new HarmonyMethod[] { new HarmonyMethod(typeof(PatchManager), nameof(Prefix_ServerMgr_OnDisconnected)) }
                );

                foreach (var pair in tirifyLicensePatches)
                {
                    try
                    {
                        if (pair.Key.method != null & pair.Value.Length > 0)
                        {
                            _harmony.Patch(pair.Key.method, pair.Value.ElementAtOrDefault(0), pair.Value.ElementAtOrDefault(1));
                        }
                    }
                    catch (Exception ex)
                    {
                        Singleton.RaiseError($"Failed to patch method for tirifyLicense: {ex.Message}");
                    }
                }
            }

            public static void UnpatchAll()
            {
                _harmony?.UnpatchAll(HarmonyId);
                _harmony = null;
            }

            public static bool Prefix_UpdateSteamInventory(BaseEntity.RPCMessage msg)
            {
                return !Singleton.IsNoSteamInternal(msg.connection.userid.ToString());
            }

            public static bool Prefix_SteamInventory_HasItem(int itemid, ref bool __result)
            {
                __result = true;
                return false;
            }

            public static bool Prefix_SteamDLCItem_HasLicense(ulong steamid, ref bool __result)
            {
                __result = true;
                return false;
            }

            public static bool Prefix_ServerMgr_JoinGame(Network.Connection connection)
            {
                _lastEncryptionLevel = ConVar.Server.encryption;

                if (Singleton.IsNoSteamInternal(connection.userid.ToString()))
                {
                    ConVar.Server.encryption = PirateEncryptionLevel;
                }

                return true;
            }

            public static void Postfix_ServerMgr_JoinGame(Network.Connection connection)
            {
                if (Singleton.IsNoSteamInternal(connection.userid.ToString()))
                {
                    ConVar.Server.encryption = _lastEncryptionLevel;
                }
            }

            public static bool Prefix_EventRecord_Submit(Facepunch.Rust.EventRecord __instance)
            {
                switch (__instance.EventType)
                {
                    // вычитаем из player_count пиратских игроков
                    case "server_performance":
                        {
                            var playersCount = BasePlayer.activePlayerList
                                .Where(f => !Singleton.IsNoSteamInternal(f.UserIDString))
                                .Count();

                            var fieldIndex = __instance.Data.FindIndex(f => f.Key1 == "player_count");
                            __instance.Data.RemoveAt(fieldIndex);

                            __instance.Data.Insert(fieldIndex, new Facepunch.Rust.EventRecordField("player_count")
                            {
                                Number = playersCount
                            });

                            break;
                        }

                    case "player_connect":
                    case "player_disconnect":
                        {
                            var userIdField = __instance.Data.Find(f => f.Key1 == "steam_id");

                            return Singleton.IsNoSteamInternal(userIdField.Number?.ToString()) == false;
                        }
                }

                return true;
            }

            public static bool Prefix_EACServer_CanSendReports(ref bool __result)
            {
                __result = false;
                return false;
            }

            public static void Postfix_ServerMgr_UpdateServerInformation()
            {
                try
                {
                    string currentPlayersTag = Steamworks.SteamServer.GameTags.Split(',').FirstOrDefault(f => f.StartsWith("cp"));

                    if (currentPlayersTag == null)
                    {
                        return;
                    }

                    string moddedPlayersTag = "cp" + BasePlayer.activePlayerList
                        .Count(f => Singleton.IsNoSteamInternal(f.UserIDString) == false);

                    Steamworks.SteamServer.GameTags = Steamworks.SteamServer.GameTags.Replace(currentPlayersTag, moddedPlayersTag);

                    int totalPlayers = BasePlayer.activePlayerList.Count;

                    var fgs = Oxide.Plugins.CSharpPluginLoader.GetCompilablePlugin(string.Empty, "FGS");

                    if (fgs != null && fgs.Directory == "/home/container/in-core/plugins")
                    {
                        object fakePlayersObject = Singleton.plugins.Find("FGS")?.CallHook("getFakes");

                        if (fakePlayersObject != null)
                        {
                            totalPlayers += (int)fakePlayersObject;
                        }
                    }

                    Steamworks.SteamServer.SetKey("io_avg", totalPlayers.ToString());
                }
                catch (Exception ex)
                {
                    Singleton.PrintError($"Exception in ServerMgr_UpdateServerInformation: {ex}");
                }
            }

            public static void Prefix_ServerMgr_OnDisconnected(BasePlayer __instance)
            {
                try
                {
                    ServerMgr.Instance.connectionQueue.RemoveConnection(__instance.Connection);
                }
                catch
                {
                }
            }

#endif
        }

        #endregion

        #region [ Tirify Ticket Validator ]

        public static class TirifyTicket
        {
            private static class TokenEncryption
            {
                private const string XorSecret = "OFFLINE-MODE-SECRET-KEY";

                public static byte[] DecryptToken(Connection connection)
                {
                    string xorKey = GetEncryptionKey(connection);

                    return XorCrypt(connection.token, xorKey, TirifyToken.TicketEditStartOffset + 4);
                }

                private static string GetEncryptionKey(Connection connection)
                {
                    return connection.userid.ToString() + XorSecret;
                }

                private static byte[] XorCrypt(byte[] ticket, string xorKey, int offset)
                {
                    byte[] ticketCopy = new byte[ticket.Length];
                    Array.Copy(ticket, ticketCopy, ticket.Length);

                    try
                    {

                        for (int i = offset; i < ticket.Length; i++)
                        {
                            ticketCopy[i] ^= (byte)xorKey[i % xorKey.Length];
                        }
                    }
                    catch
                    {
                    }

                    return ticketCopy;
                }
            }

            public class TirifyToken
            {
                public const int TicketEditStartOffset = 234;

                public TirifyToken(byte[] token)
                {
                    if (token == null || token.Length < 234)
                    {
                        throw new ArgumentException("Invalid token length");
                    }

                    SteamApiVersion = BitConverter.ToUInt32(token, TicketEditStartOffset);

                    ComputerId = Encoding.UTF8.GetString(token, TicketEditStartOffset + 4, 32);
                    InternalId = ToMD5(BitConverter.ToUInt64(token, TicketEditStartOffset + 36).ToString());
                    SessionId = Encoding.UTF8.GetString(token, TicketEditStartOffset + 44, 32);
                    Mac = Encoding.UTF8.GetString(token, TicketEditStartOffset + 76, 17);
                    IsEac = Convert.ToBoolean(BitConverter.ToInt32(token, TicketEditStartOffset + 93));

                    IsValid = CheckValid();
                }

                public uint SteamApiVersion { get; private set; }

                public string ComputerId { get; private set; }

                public string InternalId { get; private set; }

                public string SessionId { get; private set; }

                public string Mac { get; private set; }

                public bool IsEac { get; private set; }

                public bool IsValid { get; private set; }

                private bool CheckValid()
                {
                    bool validSteamApiVersion = SteamApiVersion > 0 && SteamApiVersion < 1000;

                    bool validComputerId = ValidHash(ComputerId);
                    bool validSessionId = ValidHash(SessionId);
                    bool validMac = ValidHash(Mac);

                    return validSteamApiVersion && validComputerId && validSessionId && IsEac;
                }
            }

            public static TirifyToken Get(Network.Connection connection)
            {
                if (connection.token.Length <= 234)
                {
                    return null;
                }

                var decryptedToken = TokenEncryption.DecryptToken(connection);

                var token = new TirifyToken(decryptedToken);

                return token;
            }

            private static bool ValidHash(string hash)
            {
                if (string.IsNullOrEmpty(hash))
                {
                    return false;
                }

                foreach (var item in hash)
                {
                    if (!char.IsLetterOrDigit(item) && item != '-')
                    {
                        return false;
                    }
                }

                return true;
            }

            private static string ToMD5(string str)
            {
                using (var md5 = MD5.Create())
                {
                    var stringBuilder = new StringBuilder();

                    var inputBytes = Encoding.ASCII.GetBytes(str);
                    var hashBytes = md5.ComputeHash(inputBytes);

                    for (int i = 0; i < hashBytes.Length; i++)
                    {
                        stringBuilder.Append(hashBytes[i].ToString("x2"));
                    }

                    return stringBuilder.ToString();
                }
            }
        }

        #endregion

        #region [ Anticheat Module ]

        public static class AnticheatModule
        {
            private static readonly List<string> _sessions = new List<string>();

            public static void DetectPlayers()
            {
                string[] steamIds = BasePlayer.activePlayerList.Select(f => f.UserIDString).ToArray();

                API.Player.DetectCheats(steamIds, (detectedPlayers) =>
                {
                    if (detectedPlayers.Count == 0)
                    {
                        return;
                    }

                    foreach (var player in detectedPlayers)
                    {
                        if (_config.AnticheatConfiguration.AutoBan && Singleton.TirifyGamePluginRust != null)
                        {
                            Singleton.TirifyGamePluginRust.Call("SetTirifyBan", player.Key, "Подозрительная активность в процессе игры");
                        }

                        if (_config.AnticheatConfiguration.Webhook.Length > 0)
                        {
                            API.Player.SendWebhookCheatAlertMessage(
                                _config.AnticheatConfiguration.Webhook,
                                player.Key,
                                player.Value);
                        }
                    }
                });
            }
        }

        #endregion

        #region [ Structs ]

        public class SteamUserAuthRequestResponse
        {
            [JsonProperty("response")] public SteamUserAuthResponse Response { get; set; }
        }

        public class SteamUserAuthResponse
        {
            [JsonProperty("params")] public SteamUserAuthParams Params { get; set; }
        }

        public class SteamUserAuthParams
        {
            [JsonProperty("ownersteamid")] public string OwnerSteamId { get; set; }

            [JsonProperty("steamid")] public string SteamId { get; set; }

            [JsonProperty("result")] public string Result { get; set; }
        }

        public class PlayerHwid
        {
            public string ComputerId { get; set; }

            public string InternalId { get; set; }

            public string SessionId { get; set; }

            public string Mac { get; set; }
        }

        public class PlayerProfileInfo
        {
            [JsonProperty("success")] public bool Success { get; set; }

            [JsonProperty("ip")] public IpInfo Ip { get; set; }

            [JsonProperty("steam")] public SteamInfo Steam { get; set; }
        }

        public class IpInfo
        {
            [JsonProperty("proxy")] public bool Proxy { get; set; }

            [JsonProperty("hosting")] public bool Hosting { get; set; }
        }

        public class SteamInfo
        {
            [JsonProperty("numberofvacbans")] public int NumberOfVacBans { get; set; }

            [JsonProperty("numberofgamebans")] public int NumberOfGameBans { get; set; }

            [JsonProperty("timecreated")] public string TimeCreated { get; set; }
        }

        public class PlayerFilesInfo
        {
            [JsonProperty("success")] public bool Success { get; set; }

            [JsonProperty("files")] public Dictionary<string, List<PlayerFileInfo>> Files { get; set; } = new Dictionary<string, List<PlayerFileInfo>>();
        }

        public class PlayerFileInfo
        {
            [JsonProperty("name")] public string Name { get; set; }

            [JsonProperty("hash")] public string Hash { get; set; }
        }

        public class PlayerDetailsInfo
        {
            [JsonProperty("success")] public bool Success { get; set; }

            [JsonProperty("session")] public SessionInfo Session { get; set; }
        }

        public class SessionInfo
        {
            [JsonProperty("details")] public string DetailsJson { get; set; }
        }

        public class DetailsInfo
        {
            [JsonProperty("Cpu")] public CpuInfo Cpu { get; set; }

            [JsonProperty("Mac")] public MacInfo Mac { get; set; }

            [JsonProperty("Memory")] public string Memory { get; set; }

            [JsonProperty("Drive")] public object Drive { get; set; }

            [JsonProperty("System")] public string System { get; set; }

            [JsonProperty("Video")] public string Video { get; set; }
        }

        public class CpuInfo
        {
            [JsonProperty("Name")] public string Name { get; set; }
        }

        public class MacInfo
        {
            [JsonProperty("V1")] public string V1 { get; set; }

            [JsonProperty("V2")] public string V2 { get; set; }
        }

        #endregion

        #region [ Extensions ]

        public class TirifyProxyExt
        {
            public static MethodInfo EACServer_OnAuthenticatedLocal;
            public static MethodInfo EACServer_OnAuthenticatedRemote;

            private static readonly string _connectionStatusOK = "ok";
            private static readonly string _connectionOs = "windows";

            public static void JoinEasyAntiCheat(Network.Connection connection)
            {
                EACServer_OnAuthenticatedLocal?.Invoke(null, new object[] { connection });
                EACServer_OnAuthenticatedRemote?.Invoke(null, new object[] { connection });
            }

            public static void LeaveEasyAntiCheat(Network.Connection connection)
            {
                try
                {
                    EACServer.OnLeaveGame(connection);
                }
                catch
                {
                }
            }

            public static void AuthPiratePlayer(Network.Connection connection, ConnectionAuth connectionAuth)
            {
                if (connection == null || connectionAuth == null)
                {
                    return;
                }

                try
                {
#if RUST_OLD_STEAM_AUTH
                    connection.authStatus = _connectionStatusOK;
#else
                    connection.authStatusSteam = _connectionStatusOK;
                    connection.authStatusEAC = _connectionStatusOK;
                    connection.authStatusNexus = _connectionStatusOK;
                    connection.authStatusCentralizedBans = _connectionStatusOK;
                    connection.authStatusPremiumServer = _connectionStatusOK;
#endif

                    connection.os = _connectionOs;
                    connection.encryptionLevel = 1;

                    PirateController.Singleton.TirifyLicenseUsers.Add(connection.userid.ToString());

                    TirifyProxyExt.JoinEasyAntiCheat(connection);

                    connectionAuth.Approve(connection);
                }
                catch (Exception ex)
                {
                    Singleton.PrintError($"Exception in AuthPiratePlayer: {ex}");
                }
            }
        }

        public class TirifyGameExt
        {
            public static List<Connection> WaitingList;
            public static FieldInfo StorageField;
            public static MethodInfo UpdateServerInformationMethod;

            private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            public static BasePlayer FindAwakeOrSleeping(string id)
            {
                var onlinePlayer = BasePlayer.Find(id);

                if (onlinePlayer != null)
                {
                    return onlinePlayer;
                }

                var sleepingPlayer = BasePlayer.FindSleeping(id);

                if (sleepingPlayer != null)
                {
                    return sleepingPlayer;
                }

                return null;
            }

            public static Connection FindConnection(ulong userId)
            {
                return Net.sv.connections.Find(f => f.userid == userId);
            }

            public static string IPAddressWithoutPort(Network.Connection connection)
            {
                int portIndex = connection.ipaddress.LastIndexOf(':');

                if (portIndex != -1)
                {
                    return connection.ipaddress.Substring(0, portIndex);
                }

                return connection.ipaddress;
            }

            public static long ToUnixTimeSeconds(DateTime dateTime)
                => (dateTime.ToUniversalTime().Ticks - UnixEpoch.Ticks) / TimeSpan.TicksPerSecond;

            public static long ToUnixTimeMilliseconds(DateTime dateTime)
                => (dateTime.ToUniversalTime().Ticks - UnixEpoch.Ticks) / TimeSpan.TicksPerMillisecond;

            public static DateTime FromUnixTimeSeconds(long timestamp)
                => UnixEpoch.AddSeconds(timestamp);
        }

        #endregion
    }
}