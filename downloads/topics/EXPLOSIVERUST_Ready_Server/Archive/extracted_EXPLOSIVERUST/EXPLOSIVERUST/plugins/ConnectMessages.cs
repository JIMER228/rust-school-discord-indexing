using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using Oxide.Core.Libraries;
using Newtonsoft.Json;
using System;

namespace Oxide.Plugins {
    [Info("ConnectMessages", "https://topplugin.ru / https://discord.com/invite/5DPTsRmd3G", "1.0.3")]
    [Description("Custom connect and disconnect messages.")]
    class ConnectMessages : CovalencePlugin {
        private class Response {
            [JsonProperty("country")]
            public string Country { get; set; }
            [JsonProperty("countryCode")]
            public string CountryCode { get; set; }
        }

        private void OnServerInitialized() {
            permission.RegisterPermission("connectmessages.admin", this);
#if HURTWORLD
			GameManager.Instance.ServerConfig.ChatConnectionMessagesEnabled = false;
#endif
        }

        protected override void LoadDefaultConfig() {
            LogWarning("Creating a new configuration file");
            Config["Show Admin Join Message"] = true;
            Config["Show Admin Leave Message"] = true;
            Config["Use Country Message"] = true;
            Config["Use Country Code"] = false;
#if RUST
            Config["Chat Icon (SteamID64)"] = 0;
#endif
        }

        protected override void LoadDefaultMessages() {
            lang.RegisterMessages(new Dictionary<string, string> {
                ["Join Country Message"] = "[{1}] {0} joined the game.",
                ["Join Message"] = "{0} joined the game.",
                ["Leave Message"] = "{0} left the game."
            }, this);
            lang.RegisterMessages(new Dictionary<string, string> {
                ["Join Country Message"] = "[{1}] {0} dołączył do gry.",
                ["Join Message"] = "{0} dołączył do gry.",
                ["Leave Message"] = "{0} opuścił nas."
            }, this, "pl");
        }

        private void OnUserConnected(IPlayer player) {
            if (!Convert.ToBoolean(Config["Show Admin Join Message"]) && player.IsAdmin || player.HasPermission("connectmessages.admin")) {
                return;
            }

            if (Convert.ToBoolean(Config["Use Country Message"])) {
                string apiUrl = "http://ip-api.com/json/";
                webrequest.Enqueue(apiUrl + player.Address, null, (code, response) => {
                    if (code != 200 || response == null) {
                        Puts($"WebRequest to {apiUrl} failed, sending connect message without the country.");
                        Broadcast("Join Message", player.Name);
                        return;
                    }

                    if (Convert.ToBoolean(Config["Use Country Code"])) {
                        string countrycode = JsonConvert.DeserializeObject<Response>(response).CountryCode;
                        Broadcast("Join Country Message", player.Name, countrycode);
                    } else {
                        string country = JsonConvert.DeserializeObject<Response>(response).Country;
                        Broadcast("Join Country Message", player.Name, country);
                    }
                }, this, RequestMethod.GET);
            } else {
                Broadcast("Join Message", player.Name);
            }
        }

        private void OnUserDisconnected(IPlayer player) {
            if (!Convert.ToBoolean(Config["Show Admin Leave Message"]) && player.IsAdmin || player.HasPermission("connectmessages.admin")) {
                return;
            } else {
                Broadcast("Leave Message", player.Name);
            }
        }

#if RUST
        private void Broadcast(string msg, params object[] args) => ConsoleNetwork.BroadcastToAllClients("chat.add", 2, Convert.ToUInt64(Config["Chat Icon (SteamID64)"]), string.Format(lang.GetMessage(msg, this), args));
#elif HURTWORLD // Send clear message without announcement icon
        private void Broadcast(string msg, params object[] args) => ChatManagerServer.Instance.SendChatMessage(new ServerChatMessage(string.Format(lang.GetMessage(msg, this), args), false));
#else
        private void Broadcast(string msg, params object[] args) => server.Broadcast(string.Format(lang.GetMessage(msg, this), args));
#endif
    }
}