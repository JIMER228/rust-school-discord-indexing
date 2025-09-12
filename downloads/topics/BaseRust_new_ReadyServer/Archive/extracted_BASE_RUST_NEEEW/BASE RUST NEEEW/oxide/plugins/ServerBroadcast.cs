using System;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using Newtonsoft.Json.Converters;
using Facepunch;
using VLB;

using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Oxide.Plugins
{
    [Info("ServerBroadcast", "EcoSmile", "1.0.6")]
    class ServerBroadcast : RustPlugin
    {
        static ServerBroadcast ins;
        PluginConfig config;

        public class PluginConfig
        {
            //[JsonProperty("Message display interval (Time in minutes)")]
            [JsonProperty("Интервал показа сообщений (Время в минутах)")]
            public float TimeInterval;
            //[JsonProperty("The Steam ID of the account from which to display the avatar in the chat. (0 - standart icon)")]
            [JsonProperty("SteamID аккаунта с которого отображать аватарка в чате. (0 - стандартная иконка)")]
            public ulong SteamID;
            //[JsonProperty("List of messages to display to players (language - messages)")]
            [JsonProperty("Список сообщения для вывода игрокам (язык - сообщения)")]
            public Dictionary<string, List<string>> messageList;
        }

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig
            {
                TimeInterval = 30f,
                SteamID = 0,
                messageList = new Dictionary<string, List<string>>()
                {
                    ["en"] = new List<string>
                    {
                        "Here you can write any text using formatting <b>fat</b>, <i>slope</i>, <size=25>size</size> and <color=#FF8000FF>color</color>\nA as well as line wrapping",
                        "Here you can write any text using formatting <b>fat</b>, <i>slope</i>, <size=25>size</size> and <color=#FF8000FF>color</color>\nA as well as line wrapping"
                    },
                    ["ru"] = new List<string>()
                    {
                        "Здесь можно писать любой текст, используя форматирование <b>жирности</b>, <i>наклона</i>, <size=25>размера</size> и <color=#FF8000FF>цвета</color>\nА так-же перенос строк",
                        "Здесь можно писать любой текст, используя форматирование <b>жирности</b>, <i>наклона</i>, <size=25>размера</size> и <color=#FF8000FF>цвета</color>\nА так-же перенос строк",
                        "Здесь можно писать любой текст, используя форматирование <b>жирности</b>, <i>наклона</i>, <size=25>размера</size> и <color=#FF8000FF>цвета</color>\nА так-же перенос строк",
                    }
                }
            }; 
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        Broadcaster broadcaster;
        Dictionary<string, MsgData> langCount = new Dictionary<string, MsgData>();
        public class MsgData
        {
            public int Max;
            public int Current;
        }
        private void OnServerInitialized()
        {
            ins = this;
            foreach (var msg in config.messageList.Keys.ToList())
            {
                if (langCount.ContainsKey(msg))
                    langCount[msg].Max = config.messageList[msg].Count;
                else
                    langCount[msg] = new MsgData { Max = config.messageList[msg].Count, Current = 0 };
            }
            broadcaster = new GameObject().AddComponent<Broadcaster>();
        }

        void Unload()
        {
            UnityEngine.Object.Destroy(broadcaster);
            ins = null;
        }

        [ChatCommand("sbhide")]
        void SBHidecmd(BasePlayer player)
        {
            if (!hideData.ContainsKey(player))
                hideData[player] = false;

            hideData[player] = !hideData[player];
            SendReply(player, $"Вы {(hideData[player] ? "ОТКЛЮЧИЛИ" : "ВКЛЮЧИЛИ")} отображение информационнах сообщений");
        }

        Dictionary<BasePlayer, bool> hideData = new Dictionary<BasePlayer, bool>();
        [PluginReference] Plugin BlueberryMenu;
        public class Broadcaster : FacepunchBehaviour
        {
            void Awake()
            {
                InvokeRepeating(MessageRepitter, 0, ins.config.TimeInterval * 60f);
            }

            void MessageRepitter()
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (ins.BlueberryMenu != null && ins.BlueberryMenu.Call("IsHide", player.userID) != null)
                        if (ins.BlueberryMenu.Call<bool>("IsHide", player.userID))
                            continue;

                    if (ins.hideData.ContainsKey(player) && ins.hideData[player])
                        continue;

                    var lang = ins.lang.GetLanguage(player.UserIDString);
                    var text = "";
                    var index = 0;
                    if (ins.langCount.ContainsKey(lang))
                        index = ins.langCount[lang].Current;
                    else
                        index = ins.langCount.FirstOrDefault().Value.Current;

                    if (ins.config.messageList.ContainsKey(lang))
                        text = ins.config.messageList[lang][index];
                    else
                        text = ins.config.messageList.FirstOrDefault().Value[index];

                    ConsoleNetwork.SendClientCommand(player.Connection, "chat.add", new object[]
                    {
                        2,
                        ins.config.SteamID,
                        text
                    });
                }
                foreach (var index in ins.langCount)
                {
                    index.Value.Current++;
                    if (index.Value.Current >= index.Value.Max)
                        index.Value.Current = 0;
                }

            }

            void OnDestroy()
            {
                CancelInvoke(MessageRepitter);
                Destroy(this);
            }
        }

    }
}
