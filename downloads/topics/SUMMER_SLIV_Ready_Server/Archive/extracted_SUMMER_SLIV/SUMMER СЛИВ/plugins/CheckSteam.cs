using System;
using System.Collections.Generic;
using Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("CheckSteam", "Ryamkk", "1.1.0")]
    public class CheckSteam : RustPlugin
    {
        public Configuration config;

        public class Configuration
        {
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
            string url = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key=9F6382327AC5261B6402A13BE0248E7A&steamids={connection.userid}&personalname&format=json";
                
            webrequest.EnqueueGet(url, (code, response) =>
            {
                    JObject InfoResponse = JObject.Parse(response);
                    
                    foreach (var item in InfoResponse["response"]["players"])
                    {
                        DateTime date = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(Convert.ToDouble(item["timecreated"]));
                        if ((DateTime.Now - ToDateTime(Convert.ToInt64(item["timecreated"]))).TotalHours < 50)
                        {
                            SendChatMessage($"[{DateTime.Now.ToShortTimeString()}]\n" +
                                            $"Игрок {connection.username} был кикнут с сервера\n" +
                                            $"Подозрение на использование: New Account\n" +
                                            $"Его настоящий ник: {connection.username}\n" +
                                            $"Его настоящий SteamID: {connection.userid}\n" +
                                            $"IP адрес подключения: {connection.ipaddress}\n" +
                                            $"Дата создания аккаунта: {date}\n" +
                                            $"\nСсылка на его Steam Profile:\n" +
                                            $"{item["profileurl"]}");
                            
                            Net.sv.Kick(connection, "Ваш аккаунт был создан менее 50 часов назад. =(");
                        }
                    }
            }, this);
        }
        
        [ConsoleCommand("steamcheck.ignore")]
        void CmdSCIgnore(ConsoleSystem.Arg arg)
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
                    Puts($"[CheckSteam]: Player [{steamidSTR}] added to IgnoreList");
                    SaveConfig();
                }
                else
                {
                    config.IgnoreList.Remove(steamid);
                    Puts($"[CheckSteam]: Player [{steamidSTR}] removed from IgnoreList");
                    SaveConfig();
                }
                Config.WriteObject(config, true);
            }
            else
            {
                Puts($"[CheckSteam]: You write steamid [{steamidSTR}] is not correct!");
            }
        }
        
        private DateTime ToDateTime(long UnixTime)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0);
            return origin.AddSeconds(UnixTime);
        }
        
        void SendChatMessage(string Message)
        {
            int RandomID = UnityEngine.Random.Range(0, 9999);
            var Token = "vk1.a.CnKMGe1YzFlKdvxfSewJ0O-IlTvOfw2OZB1G9QeTNVzPVAh6fwUnEv707yqIfOW5wOVu5J0jpJYvzX2fB_xXNtF9o59PE8nZv3rtMt7-LrEQ4OvEV4b9bluwWh0IQb5Br4oNpl8YZE0NivG3ainKbvjiG5S25_xQ9P462xFNT6LjHiOVDfMDBXs8ledpcosmZ7k4hLiQblFwSCZHienYuw";
            var ChatID = "5";
            
            while (Message.Contains("#")) Message = Message.Replace("#", "%23");
            webrequest.Enqueue($"https://api.vk.com/method/messages.send?chat_id={ChatID}&random_id={RandomID}&message={Message}&access_token={Token}&v=5.92", null, (code, response) => { }, this);
        }
    }
}