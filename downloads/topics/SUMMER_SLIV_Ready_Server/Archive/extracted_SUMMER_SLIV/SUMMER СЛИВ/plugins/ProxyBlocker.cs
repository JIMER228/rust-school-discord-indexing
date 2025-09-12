using System;
using System.Collections.Generic;
using Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("Proxy Blocker", "Ryamkk", "4.0.0")]
    public class ProxyBlocker : RustPlugin
    {
        public Configuration config;
        private Dictionary<ulong, Timer> ListTimers = new Dictionary<ulong, Timer>();

        public class Configuration
        {
            [JsonProperty("Причина кика за VPN")] 
            public string KickPlayerMessage = "Вход на сервер с VPN запрещён, если вы не используете VPN напишите в группу!";

            [JsonProperty("Список SteamID которых не нужно проверять")]
            public List<ulong> IgnoreList = new List<ulong>() {};
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
            }
            catch
            {
                PrintError("Конфигурационный файл повреждён, проверьте правильность ведённых данных!");
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new Configuration(), true);
        }
        
        void OnClientAuth(Connection connection)
        {
            if (config.IgnoreList.Contains(connection.userid)) return;

            string ip = connection.ipaddress;
            string url = $"http://proxycheck.io/v2/{ip}?key=6222px-4585j3-11x435-b79n28&vpn=1&asn=1&risk=1&port=1&seen=1&days=7&tag=msg";
                    
            webrequest.EnqueueGet(url, (code, response) => 
            { 
                if (response == null || code != 200) { return; } 
                if (response.Contains("denied")) { SendChatMessage($"[{DateTime.Now.ToShortTimeString()}]\n" + $"{JObject.Parse(response)[$"{ip}"]["message"]}"); }
                if (response.Contains("VPN") || response.Contains("yes") || response.Contains("rule")) 
                { 
                    SendChatMessage($"[{DateTime.Now.ToShortTimeString()}]\n" +
                                    $"Игрок {connection.username} был кикнут с сервера\n" +
                                    $"Подозрение на использование: VPN\n" +
                                    $"Его настоящий ник: {connection.username}\n" +
                                    $"Его настоящий SteamID: {connection.userid}\n" +
                                    $"IP адрес подключения: {connection.ipaddress}\n" +
                                    $"\nЛоги проверки:\n\n" + 
                                    $"Provider: {JObject.Parse(response)[$"{ip}"]["provider"]}\n" +
                                    $"Сontinent: {JObject.Parse(response)[$"{ip}"]["continent"]}\n" +
                                    $"Сountry: {JObject.Parse(response)[$"{ip}"]["country"]}\n" +
                                    $"Isocode: {JObject.Parse(response)[$"{ip}"]["isocode"]}\n" +
                                    $"Region: {JObject.Parse(response)[$"{ip}"]["region"]}\n" +
                                    $"Сity: {JObject.Parse(response)[$"{ip}"]["city"]}\n" +
                                    $"Proxy: {JObject.Parse(response)[$"{ip}"]["proxy"]}\n" +
                                    $"Type: {JObject.Parse(response)[$"{ip}"]["type"]}");

                    Net.sv.Kick(connection, config.KickPlayerMessage); 
                } 
            }, this);
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (config.IgnoreList.Contains(player.userID)) return;
            if (ListTimers.ContainsKey(player.userID)) ListTimers[player.Connection.userid].Destroy();
            
            ListTimers[player.userID] = timer.Every(120f, () =>
            {
                if (player.IsConnected)
                {
                    string ip = player.net.connection.ipaddress;
                    string url = $"http://proxycheck.io/v2/{ip}?key=6222px-4585j3-11x435-b79n28&vpn=1&asn=1&risk=1&port=1&seen=1&days=7&tag=msg";
                    
                    webrequest.EnqueueGet(url, (code, response) => 
                    { 
                        if (response == null || code != 200) { return; } 
                        if (response.Contains("denied")) { SendChatMessage($"[{DateTime.Now.ToShortTimeString()}]\n" + $"{JObject.Parse(response)[$"{ip}"]["message"]}"); }
                        if (response.Contains("VPN") || response.Contains("yes") || response.Contains("rule")) 
                        { 
                            SendChatMessage($"[{DateTime.Now.ToShortTimeString()}]\n" +
                                            $"Игрок {player.displayName} был кикнут с сервера\n" +
                                            $"Подозрение на использование: VPN\n" +
                                            $"Его настоящий ник: {player.displayName}\n" +
                                            $"Его настоящий SteamID: {player.UserIDString}\n" +
                                            $"IP адрес подключения: {player.net.connection.ipaddress}\n" +
                                            $"\nЛоги проверки:\n\n" + 
                                            $"Provider: {JObject.Parse(response)[$"{ip}"]["provider"]}\n" +
                                            $"Сontinent: {JObject.Parse(response)[$"{ip}"]["continent"]}\n" +
                                            $"Сountry: {JObject.Parse(response)[$"{ip}"]["country"]}\n" +
                                            $"Isocode: {JObject.Parse(response)[$"{ip}"]["isocode"]}\n" +
                                            $"Region: {JObject.Parse(response)[$"{ip}"]["region"]}\n" +
                                            $"Сity: {JObject.Parse(response)[$"{ip}"]["city"]}\n" +
                                            $"Proxy: {JObject.Parse(response)[$"{ip}"]["proxy"]}\n" +
                                            $"Type: {JObject.Parse(response)[$"{ip}"]["type"]}");

                            player.Kick(config.KickPlayerMessage); 
                        } 
                    }, this);
                }
            });
        }
        
        void CheckPlayer(string ip)
        {
            string url = $"http://proxycheck.io/v2/{ip}?key=6222px-4585j3-11x435-b79n28&vpn=1&asn=1&risk=1&port=1&seen=1&days=7&tag=msg";
            webrequest.EnqueueGet(url, (code, response) =>
            {
                if (response == null || code != 200) { return; }
                if (response.Contains("denied")) { SendChatMessage($"[{DateTime.Now.ToShortTimeString()}]\n" + $"{JObject.Parse(response)[$"{ip}"]["message"]}"); }

                if (response.Contains("VPN") || response.Contains("yes") || response.Contains("rule"))
                {
                    SendChatMessage($"[{DateTime.Now.ToShortTimeString()}]\n" +
                                    $"Логи проверки:\n\n" +
                                    $"Provider: {JObject.Parse(response)[$"{ip}"]["provider"]}\n" +
                                    $"Сontinent: {JObject.Parse(response)[$"{ip}"]["continent"]}\n" +
                                    $"Сountry: {JObject.Parse(response)[$"{ip}"]["country"]}\n" +
                                    $"Isocode: {JObject.Parse(response)[$"{ip}"]["isocode"]}\n" +
                                    $"Region: {JObject.Parse(response)[$"{ip}"]["region"]}\n" +
                                    $"Сity: {JObject.Parse(response)[$"{ip}"]["city"]}\n" +
                                    $"Proxy: {JObject.Parse(response)[$"{ip}"]["proxy"]}\n" +
                                    $"Type: {JObject.Parse(response)[$"{ip}"]["type"]}");
                }
                else
                {
                    SendChatMessage($"[{DateTime.Now.ToShortTimeString()}]\n" +
                                    $"Логи проверки:\n\n" +
                                    $"Provider: {JObject.Parse(response)[$"{ip}"]["provider"]}\n" +
                                    $"Сontinent: {JObject.Parse(response)[$"{ip}"]["continent"]}\n" +
                                    $"Сountry: {JObject.Parse(response)[$"{ip}"]["country"]}\n" +
                                    $"Isocode: {JObject.Parse(response)[$"{ip}"]["isocode"]}\n" +
                                    $"Region: {JObject.Parse(response)[$"{ip}"]["region"]}\n" +
                                    $"Сity: {JObject.Parse(response)[$"{ip}"]["city"]}\n" +
                                    $"Proxy: {JObject.Parse(response)[$"{ip}"]["proxy"]}\n" +
                                    $"Type: {JObject.Parse(response)[$"{ip}"]["type"]}");
                }
            }, this);
        }
        
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;
            if (ListTimers.ContainsKey(player.userID))
            {
                ListTimers[player.userID].Destroy();
            }
        }
        
        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null) return;
                
                if (ListTimers.ContainsKey(player.userID))
                {
                    ListTimers[player.userID].Destroy();
                }
            }
        }
        
        [ConsoleCommand("testcmd")]
        void Test(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 1) return;
            if (arg.Args == null) return;
            
            CheckPlayer(arg.Args[0]);
        }

        [ConsoleCommand("proxy.ignore")]
        void CmdACIgnore(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 1) return;
            if (arg.Args == null) return;
            
            string steamidSTR = arg.Args[0];
            ulong steamid = 0;
            
            if (steamidSTR.Length == 17 && ulong.TryParse(steamidSTR, out steamid))
            {
                if (!config.IgnoreList.Contains(steamid))
                {
                    config.IgnoreList.Add(steamid);
                    Puts($"[ProxyBlocker]: Player [{steamidSTR}] added to IgnoreList");
                    SaveConfig();
                }
                else
                {
                    config.IgnoreList.Remove(steamid);
                    Puts($"[ProxyBlocker]: Player [{steamidSTR}] removed from IgnoreList");
                    SaveConfig();
                }
                Config.WriteObject(config, true);
            }
            else
            {
                Puts($"[ProxyBlocker]: You write steamid [{steamidSTR}] is not correct!");
            }
        }

        void SendChatMessage(string Message)
        {
            int RandomID = UnityEngine.Random.Range(0, 9999);
            var Token = "vk1.a.CnKMGe1YzFlKdvxfSewJ0O-IlTvOfw2OZB1G9QeTNVzPVAh6fwUnEv707yqIfOW5wOVu5J0jpJYvzX2fB_xXNtF9o59PE8nZv3rtMt7-LrEQ4OvEV4b9bluwWh0IQb5Br4oNpl8YZE0NivG3ainKbvjiG5S25_xQ9P462xFNT6LjHiOVDfMDBXs8ledpcosmZ7k4hLiQblFwSCZHienYuw";
            var ChatID = "7";
            
            while (Message.Contains("#")) Message = Message.Replace("#", "%23");
            webrequest.Enqueue($"https://api.vk.com/method/messages.send?chat_id={ChatID}&random_id={RandomID}&message={Message}&access_token={Token}&v=5.92", null, (code, response) => { }, this);
        }
    }
}