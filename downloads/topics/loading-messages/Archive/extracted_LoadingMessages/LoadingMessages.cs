using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Oxide.Core.Plugins;
using System.Text;
using ProtoBuf;


namespace Oxide.Plugins
{
    [Info("Loading Messages", "Whispers88", "1.0.6")]
    [Description("Displays Tooltips in the Loading Screen")]
    public class LoadingMessages : RustPlugin
    {
        #region Configuration
        static LoadingMessages _loadingMessages;
        private Configuration config;
        private static List<MessageDataBytes>? queueMessages;
        private static List<MessageDataBytes>? joiningMessages;
        private static List<MessageDataBytes>? loadingMessages;

        public class Configuration
        {

            [JsonProperty("Queue Messages", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<MessageData> QueueMessages = new List<MessageData> {
                new MessageData { iconID = "0", Message = "You are in a queue", NextMessageTime = 5f },
                new MessageData { iconID = "0", Message = "Please wait", NextMessageTime = 5f },
                new MessageData { iconID = "0", Message = "Report cheaters using f7", NextMessageTime = 5f }
            };

            [JsonProperty("Joining Messages", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<MessageData> JoiningMessages = new List<MessageData> {
                new MessageData { iconID = "0", Message = "You are joining the game", NextMessageTime = 5f },
                new MessageData { iconID = "0", Message = "Please wait", NextMessageTime = 5f },
                new MessageData { iconID = "0", Message = "Report cheaters using f7", NextMessageTime = 5f }
            };

            [JsonProperty("Loading Messages", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<MessageData> LoadingMessages = new List<MessageData> {
                new MessageData { iconID = "0", Message = "You are loading a stage", NextMessageTime = 5f },
                new MessageData { iconID = "0", Message = "Please wait", NextMessageTime = 5f },
                new MessageData { iconID = "0", Message = "Report cheaters using f7", NextMessageTime = 5f }
            };

            [JsonProperty("Use Language Support")]
            public bool UseLanguageSupport = false;

            [JsonProperty("Lang Messages", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, LangMessages> LangMessages = new Dictionary<string, LangMessages>()
            {
                ["es-ES"] = new LangMessages
                {
                    QueueMessages = new List<MessageData>
                    {
                        new MessageData { iconID = "0", Message = "Estás en la cola", NextMessageTime = 5f },
                        new MessageData { iconID = "0", Message = "Por favor espera", NextMessageTime = 5f },
                        new MessageData { iconID = "0", Message = "Reporta tramposos usando f7", NextMessageTime = 5f }
                    },
                    JoiningMessages = new List<MessageData>
                    {
                        new MessageData { iconID = "0", Message = "Estás uniendo al juego", NextMessageTime = 5f },
                        new MessageData { iconID = "0", Message = "Por favor espera", NextMessageTime = 5f },
                        new MessageData { iconID = "0", Message = "Reporta tramposos usando f7", NextMessageTime = 5f }
                    },
                    LoadingMessages = new List<MessageData>
                    {
                        new MessageData { iconID = "0", Message = "Estás cargando una etapa", NextMessageTime = 5f },
                        new MessageData { iconID = "0", Message = "Por favor espera", NextMessageTime = 5f },
                        new MessageData { iconID = "0", Message = "Reporta tramposos usando f7", NextMessageTime = 5f }
                    }
                },
                ["pt-BR"] = new LangMessages
                {
                    QueueMessages = new List<MessageData>
                    {
                        new MessageData { iconID = "0", Message = "Você está na fila", NextMessageTime = 5f },
                        new MessageData { iconID = "0", Message = "Por favor, espere", NextMessageTime = 5f },
                        new MessageData { iconID = "0", Message = "Denuncie trapaceiros usando f7", NextMessageTime = 5f }
                    },
                    JoiningMessages = new List<MessageData>
                    {
                        new MessageData { iconID = "0", Message = "Você está entrando no jogo", NextMessageTime = 5f },
                        new MessageData { iconID = "0", Message = "Por favor, espere", NextMessageTime = 5f },
                        new MessageData { iconID = "0", Message = "Denuncie trapaceiros usando f7", NextMessageTime = 5f }
                    },
                    LoadingMessages = new List<MessageData>
                    {
                        new MessageData { iconID = "0", Message = "Você está carregando uma fase", NextMessageTime = 5f },
                        new MessageData { iconID = "0", Message = "Por favor, espere", NextMessageTime = 5f },
                        new MessageData { iconID = "0", Message = "Denuncie trapaceiros usando f7", NextMessageTime = 5f }
                    }
                }
            };

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }
        public class LangMessages
        {
            [JsonProperty("Queue Messages", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<MessageData> QueueMessages { get; set; }

            [JsonProperty("Joining Messages", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<MessageData> JoiningMessages { get; set; }

            [JsonProperty("Loading Messages", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<MessageData> LoadingMessages { get; set; }
        }

        public class LangMessagesData
        {
            public List<MessageDataBytes> QueueMessages = new List<MessageDataBytes>();

            public List<MessageDataBytes> JoiningMessages = new List<MessageDataBytes>();

            public List<MessageDataBytes> LoadingMessages = new List<MessageDataBytes>();
        }

        public class MessageData
        {
            public string iconID;
            public float NextMessageTime;
            public string Message;
        }

        public class MessageDataBytes
        {
            public float NextMessageTime;
            public byte[] netWrite;
        }

        private byte[] CreateWrite(byte[] icon, byte[] msg)
        {
            NetWrite netWrite = Net.sv.StartWrite();
            netWrite.PacketID(Message.Type.Message);
            netWrite.BytesWithSize(icon);
            netWrite.BytesWithSize(msg);
            byte[] bytes = netWrite.Data;
            netWrite.Dispose();
            return bytes;
        }
        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    Puts("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                Puts($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Puts($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }


        #endregion Configuration

        private static bool useLang = false;
        private static Dictionary<string, LangMessagesData> _langdata;

        #region Initialization
        private void Init()
        {
            queueMsg = -1;
            joiningMsg = -1;

            queueMessages = new List<MessageDataBytes>();
            joiningMessages = new List<MessageDataBytes>();
            loadingMessages = new List<MessageDataBytes>();

            foreach (var msg in config.QueueMessages)
            {
                queueMessages.Add(new MessageDataBytes { netWrite = CreateWrite(Encoding.UTF8.GetBytes(msg.iconID), Encoding.UTF8.GetBytes(msg.Message)), NextMessageTime = msg.NextMessageTime });
            }
            foreach (var msg in config.JoiningMessages)
            {
                joiningMessages.Add(new MessageDataBytes { netWrite = CreateWrite(Encoding.UTF8.GetBytes(msg.iconID), Encoding.UTF8.GetBytes(msg.Message)), NextMessageTime = msg.NextMessageTime });
            }
            foreach (var msg in config.LoadingMessages)
            {
                loadingMessages.Add(new MessageDataBytes { netWrite = CreateWrite(Encoding.UTF8.GetBytes(msg.iconID), Encoding.UTF8.GetBytes(msg.Message)), NextMessageTime = msg.NextMessageTime });
            }
            if (config.UseLanguageSupport)
            {
                SetUpCachedLang();
                useLang = true;
                _loadingMessages = this;
                _langcache = new Dictionary<ulong, string>();
                _langdata = new Dictionary<string, LangMessagesData>();

                foreach (var lang in config.LangMessages)
                {
                    LangMessagesData langMessagesData = new LangMessagesData();
                    foreach (var msg in lang.Value.QueueMessages)
                    {
                        langMessagesData.QueueMessages.Add(new MessageDataBytes { netWrite = CreateWrite(Encoding.UTF8.GetBytes(msg.iconID), Encoding.UTF8.GetBytes(msg.Message)), NextMessageTime = msg.NextMessageTime });
                    }
                    foreach (var msg in lang.Value.JoiningMessages)
                    {
                        langMessagesData.JoiningMessages.Add(new MessageDataBytes { netWrite = CreateWrite(Encoding.UTF8.GetBytes(msg.iconID), Encoding.UTF8.GetBytes(msg.Message)), NextMessageTime = msg.NextMessageTime });
                    }
                    foreach (var msg in lang.Value.LoadingMessages)
                    {
                        langMessagesData.LoadingMessages.Add(new MessageDataBytes { netWrite = CreateWrite(Encoding.UTF8.GetBytes(msg.iconID), Encoding.UTF8.GetBytes(msg.Message)), NextMessageTime = msg.NextMessageTime });
                    }
                    _langdata.Add(lang.Key, langMessagesData);
                }
            }
        }

        private void OnServerInitialized()
        {
            if (queueMessages.Count > 1)
            {
                UpdateQueuedPlayers();
            }
            else if (queueMessages.Count < 1)
            {
                Unsubscribe(nameof(CanBypassQueue));
            }

            if (joiningMessages.Count > 1)
            {
                UpdateJoiningPlayers();
            }
            else if (joiningMessages.Count < 1)
            {
                Unsubscribe(nameof(OnClientAuth));
            }

            if (loadingMessages.Count < 1)
            {
                Unsubscribe(nameof(OnPortalUsed));
                Unsubscribe(nameof(OnPlayerRespawned));
            }

            if (!useLang)
            {
                Unsubscribe(nameof(OnPlayerSetInfo));
            }
        }

        private void Unload()
        {
            queueMessages = null;
            joiningMessages = null;
            loadingMessages = null;
            _langdata = null;
            _loadingMessages = null;
            _langcache = null;

            ServerMgr.Instance.CancelInvoke(UpdateQueuedPlayers);
            ServerMgr.Instance.CancelInvoke(UpdateJoiningPlayers);
        }
        #endregion Initialization

        #region Hooks
        private void OnPortalUsed(BasePlayer player, BasePortal instance)
        {
            SendLoadingMessage(player);
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            SendLoadingMessage(player);
        }

        private void OnClientAuth(Connection connection)
        {
            SendFirstJoiningMessage(connection);
        }

        private void CanBypassQueue(Connection connection)
        {
            SendFirstQueueMessage(connection);
        }

        #endregion Hooks

        #region Methods
        private static string globallang = "global.language";
        private static string english = "en";
        private static Dictionary<ulong, string> _langcache;
        private static string GetLang(Connection connection)
        {
            if (!_langcache.TryGetValue(connection.userid, out string langcache))
            {
                return string.Empty;
            }
            return langcache;
        }

        static Action ActionUpdateQueuedPlayers = UpdateQueuedPlayers;

        private static int queueMsg = -1;
        private static void UpdateQueuedPlayers()
        {
            if (queueMessages.Count == 0)
                return;

            queueMsg++;
            if (queueMsg >= queueMessages.Count)
            {
                queueMsg = 0;
            }
            MessageDataBytes messageDataBytes = queueMessages[queueMsg];
            if (useLang)
            {
                foreach (var connection in ServerMgr.Instance.connectionQueue.queue)
                {
                    string lang = GetLang(connection);

                    if (!string.IsNullOrEmpty(lang) && _langdata.TryGetValue(lang, out LangMessagesData langMessagesData))
                    {
                        SendMessagePacket(connection, queueMsg >= langMessagesData.QueueMessages.Count ? messageDataBytes.netWrite : langMessagesData.QueueMessages[queueMsg].netWrite);
                    }
                    else
                    {
                        SendMessagePacket(connection, messageDataBytes.netWrite);
                    }
                }
            }
            else
            {
                foreach (var connection in ServerMgr.Instance.connectionQueue.queue)
                {
                    SendMessagePacket(connection, messageDataBytes.netWrite);
                }
            }
            ServerMgr.Instance.Invoke(UpdateQueuedPlayers, messageDataBytes.NextMessageTime);
        }

        private static int joiningMsg = -1;

        static Action ActionUpdateJoiningPlayers = UpdateJoiningPlayers;

        private static void UpdateJoiningPlayers()
        {
            if (joiningMessages.Count == 0)
                return;

            joiningMsg++;
            if (joiningMsg >= joiningMessages.Count)
            {
                joiningMsg = 0;
            }
            MessageDataBytes messageDataBytes = joiningMessages[joiningMsg];
            if (useLang)
            {
                foreach (var connection in ServerMgr.Instance.connectionQueue.joining)
                {
                    string lang = GetLang(connection);

                    if (!string.IsNullOrEmpty(lang) && _langdata.TryGetValue(lang, out LangMessagesData langMessagesData))
                    {
                        SendMessagePacket(connection, queueMsg >= langMessagesData.JoiningMessages.Count ? messageDataBytes.netWrite : langMessagesData.JoiningMessages[queueMsg].netWrite);
                    }
                    else
                    {
                        SendMessagePacket(connection, messageDataBytes.netWrite);
                    }
                }
            }
            else
            {
                foreach (var connection in ServerMgr.Instance.connectionQueue.joining)
                {
                    SendMessagePacket(connection, messageDataBytes.netWrite);
                }
            }
            ServerMgr.Instance.Invoke(ActionUpdateJoiningPlayers, messageDataBytes.NextMessageTime);
        }

        private static void SendLoadingMessage(BasePlayer player, int i = -1)
        {
            if (loadingMessages.Count == 0)
                return;

            if (!player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot) || player.Connection.state == Connection.State.InQueue || player.Connection.state == Connection.State.Connecting)
            {
                return;
            }
            i++;
            if (i >= loadingMessages.Count)
            {
                i = 0;
            }
            MessageDataBytes messageDataBytes = loadingMessages[i];
            if (useLang)
            {
                string lang = GetLang(player.Connection);
                if (!string.IsNullOrEmpty(lang) && _langdata.TryGetValue(lang, out LangMessagesData langMessagesData))
                {
                    SendMessagePacket(player.Connection, i >= langMessagesData.LoadingMessages.Count ? messageDataBytes.netWrite : langMessagesData.LoadingMessages[i].netWrite);
                }
                else
                {
                    SendMessagePacket(player.Connection, messageDataBytes.netWrite);
                }
            }
            else
            {
                SendMessagePacket(player.Connection, messageDataBytes.netWrite);
            }
            ServerMgr.Instance.Invoke(() => SendLoadingMessage(player, i), messageDataBytes.NextMessageTime);
        }

        private static void SendFirstQueueMessage(Connection connection)
        {
            if (queueMessages.Count == 0)
                return;

            MessageDataBytes messageDataBytes = queueMessages[0];
            if (useLang)
            {
                string lang = GetLang(connection);
                if (!string.IsNullOrEmpty(lang) && _langdata.TryGetValue(lang, out LangMessagesData langMessagesData))
                {
                    SendMessagePacket(connection, 0 >= langMessagesData.QueueMessages.Count ? messageDataBytes.netWrite : langMessagesData.QueueMessages[0].netWrite);
                }
                else
                {
                    SendMessagePacket(connection, messageDataBytes.netWrite);
                }
            }
            else
            {
                SendMessagePacket(connection, messageDataBytes.netWrite);
            }
        }

        private static void SendFirstJoiningMessage(Connection connection)
        {
            if (joiningMessages.Count == 0)
                return;

            MessageDataBytes messageDataBytes = joiningMessages[0];
            if (useLang)
            {
                string lang = GetLang(connection);
                if (!string.IsNullOrEmpty(lang) && _langdata.TryGetValue(lang, out LangMessagesData langMessagesData))
                {
                    SendMessagePacket(connection, 0 >= langMessagesData.JoiningMessages.Count ? messageDataBytes.netWrite : langMessagesData.JoiningMessages[0].netWrite);
                }
                else
                {
                    SendMessagePacket(connection, messageDataBytes.netWrite);
                }
            }
            else
            {
                SendMessagePacket(connection, messageDataBytes.netWrite);
            }
        }

        private static void SendMessagePacket(Connection connection, byte[] bytes)
        {
            if (!Net.sv.IsConnected() || connection == null) return;
            NetWrite netWrite = Net.sv.StartWrite();
            netWrite.Write(bytes, 0, bytes.Length);
            netWrite.Send(new SendInfo(connection));
        }
        #endregion Methods

        #region Lang
        private void OnPlayerSetInfo(Connection connection, string key, string val)
        {
            if (key != globallang) return;
            if (val == english)
                return;
            _langcache[connection.userid] = val;
        }

        private void SetUpCachedLang()
        {
            var langdata = ProtoStorage.Load<LangData>(new string[] { "oxide.lang" });
            if (langdata == null)
            {
                return;
            }
            foreach (var data in langdata.UserData)
            {
                if (data.Value == english)
                    continue;
                if (!ulong.TryParse(data.Key, out ulong id))
                    continue;
                _langcache[id] = data.Value;
            }
        }

        [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
        private class LangData
        {
            public string Lang = "en";

            public readonly Dictionary<string, string> UserData = new Dictionary<string, string>();

            public LangData()
            {
            }
        }
        #endregion Lang
    } 
}