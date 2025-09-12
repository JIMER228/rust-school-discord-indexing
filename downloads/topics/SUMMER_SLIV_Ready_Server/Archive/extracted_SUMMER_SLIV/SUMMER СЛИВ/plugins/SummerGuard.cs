using System;
using System.Collections.Generic;
using Network;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("SummerGuard", "Ryamkk", "1.6.2")]
    public class SummerGuard : RustPlugin
    {
        public Configuration config;
        private Dictionary<ulong, Timer> ListTimers = new Dictionary<ulong, Timer>();

        public class Configuration
        {
            [JsonProperty("Список заблокированных hwid'во")]
            public List<string> BanList = new List<string>() {};
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
            if (connection.os == "windows")
            {
                Net.sv.Kick(connection, "\"Что-бы играть на этом сервере запустите Slauncher\"");
                return;
            }
            
            if (!connection.os.Contains("["))
            {
                Net.sv.Kick(connection, "\"Что-бы играть на этом сервере запустите Slauncher\"");
                return;
            }
            
            if (!connection.os.Contains("]"))
            {
                Net.sv.Kick(connection, "\"Что-бы играть на этом сервере запустите Slauncher\"");
                return;
            }
            
            if (connection.os == "bebra")
            {
                Net.sv.Kick(connection, "\"Вы забанены на этом сервере, причина: Чит\"");
                SendChatMessage($"Игрок {connection.username} попытался hwid обойти блокировку.\nЕго hwid: {connection.os}\n" +
                                $"Его SteamID:{connection.userid}\nЕго IP:{connection.ipaddress}\n\nbebra");
                return;
            }
            
            if (config.BanList.Contains(connection.os))
            {
                Net.sv.Kick(connection, "\"Вы забанены на этом сервере, причина: Чит\"");
                return;
            }

            string str = connection.os.Replace("0", "").Replace("1", "")
                .Replace("2", "").Replace("3", "").Replace("4", "").Replace("5", "")
                .Replace("6", "").Replace("7", "").Replace("8", "").Replace("9", "")
				.Replace("{", "").Replace("}", "").Replace("-", "").Replace("[", "")
                .Replace("]", "");
            
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == str.ToUpper()[i]) 
                {
                    Net.sv.Kick(connection, "\"Вы забанены на этом сервере, причина: Чит\"");
                    SendChatMessage($"Игрок {connection.username} попытался hwid обойти блокировку.\nЕго hwid: {connection.os}\n" +
                                    $"Его SteamID:{connection.userid}\nЕго IP:{connection.ipaddress}\n\nЗаглавные буквы");
                    return;
                }
            }

            if(connection.os.Length < 38)
            {
                Net.sv.Kick(connection, "\"Вы забанены на этом сервере, причина: Чит\"");
                SendChatMessage($"Игрок {connection.username} попытался hwid обойти блокировку.\nЕго hwid: {connection.os}\n" +
                                $"Его SteamID:{connection.userid}\nЕго IP:{connection.ipaddress}\n\n< 38");
                return; 
            }
            
            if(connection.os.Length > 42)
            {
                Net.sv.Kick(connection, "\"Вы забанены на этом сервере, причина: Чит\"");
                SendChatMessage($"Игрок {connection.username} попытался hwid обойти блокировку.\nЕго hwid: {connection.os}\n" +
                                $"Его SteamID:{connection.userid}\nЕго IP:{connection.ipaddress}\n\n> 42");
            }
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (ListTimers.ContainsKey(player.userID))
            {
                ListTimers[player.Connection.userid].Destroy();
            }
            
            ListTimers[player.userID] = timer.Every(180f, () =>
            {
                if (player.IsConnected)
                {
                    if (config.BanList.Contains(player.net.connection.os))
                    {
                        player.Kick("\"Вы забанены на этом сервере, причина: Чит\"");
                    }
                }
            });
            
            SendChatMessages($"[{DateTime.Now.ToShortTimeString()}] Игрок {player.displayName} зашёл на сервер.\n" +
                             $"Его hwid: {player.net.connection.os}\n" +
                             $"Его SteamID:{player.UserIDString}\n" +
                             $"Его IP:{player.net.connection.ipaddress}");
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

        [ConsoleCommand("hwid.sban")]
        void CmdHWIDSBan(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 2) return;
            if (arg.Args == null) return;
            
            BasePlayer target = BasePlayer.FindByID(ulong.Parse(arg.Args[0]));

            if (!config.BanList.Contains(target.net.connection.os))
            {
                config.BanList.Add(target.net.connection.os);
                Puts($"[SummerGuard]: Player {target.displayName} added to BanList");
                Net.sv.Kick(target.net.connection, "\"Вы забанены на этом сервере, причина: Чит\"");
                SaveConfig();
            }
            else
            {
                Puts($"[SummerGuard]: {arg.Args[0]} Already in the ban!");
            }

            Config.WriteObject(config, true);
        }
        
        [ConsoleCommand("hwid.ban")]
        void CmdHWIDBan(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 2) return;
            if (arg.Args == null) return;

            if (!config.BanList.Contains(arg.Args[0]))
            {
                config.BanList.Add(arg.Args[0]);
                Puts($"[SummerGuard]: {arg.Args[0]} added to BanList");
                SaveConfig();
            }
            else
            {
                Puts($"[SummerGuard]: {arg.Args[0]} Already in the ban!");
            }

            Config.WriteObject(config, true);
        }
        
        [ConsoleCommand("hwid.unban")]
        void CmdHWIDUnBan(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 2) return;
            if (arg.Args == null) return;

            if (!config.BanList.Contains(arg.Args[0]))
            {
                Puts($"[SummerGuard]: The {arg.Args[0]} is not in the ban!");
            }
            else
            {
                config.BanList.Remove(arg.Args[0]);
                Puts($"[SummerGuard]: {arg.Args[0]} removed from BanList");
                SaveConfig();
            }
            
            Config.WriteObject(config, true);
        }
        
        void SendChatMessage(string Message)
        {
            int RandomID = UnityEngine.Random.Range(0, 9999);
            while (Message.Contains("#")) Message = Message.Replace("#", "%23");
            webrequest.Enqueue($"https://api.vk.com/method/messages.send?chat_id=5&random_id={RandomID}&message={Message}&access_token=vk1.a.PjfbpStxfttWqS-Jy-oiznaAefuj06ULAHFr5vjMd0w2fwfOvnOGiv5wTftRy9aRjzhB2Hue7jQcypJFA8w_IF5nQWY1eeAgfI2zmM5YMOiNxaPCtL1l5lkqJQx5GBRbUkJYeJJqKkwkLLDJZ0A4nD9xdEWyqB_6xp0rL6Zeb9rIE9PeQgg6GTGa2wzuAkXQ&v=5.92", null, (code, response) => { }, this);
        }
        
        void SendChatMessages(string Message)
        {
            int RandomID = UnityEngine.Random.Range(0, 9999);
            while (Message.Contains("#")) Message = Message.Replace("#", "%23");
            webrequest.Enqueue($"https://api.vk.com/method/messages.send?chat_id=6&random_id={RandomID}&message={Message}&access_token=vk1.a.PjfbpStxfttWqS-Jy-oiznaAefuj06ULAHFr5vjMd0w2fwfOvnOGiv5wTftRy9aRjzhB2Hue7jQcypJFA8w_IF5nQWY1eeAgfI2zmM5YMOiNxaPCtL1l5lkqJQx5GBRbUkJYeJJqKkwkLLDJZ0A4nD9xdEWyqB_6xp0rL6Zeb9rIE9PeQgg6GTGa2wzuAkXQ&v=5.92", null, (code, response) => { }, this);
        }
    }
}