using System;
using System.Collections.Generic;
using Facepunch.Extend;
using Newtonsoft.Json;
using UnityEngine;

///Скачано с дискорд сервера Rust Edit [PRO+]
///discord.gg/9vyTXsJyKR

namespace Oxide.Plugins
{
    [Info("NControl", "discord.gg/9vyTXsJyKR", "0.5.4")]
    [Description("Контроль имен ваших игроков")]
    public class NControl : RustPlugin
    {
        #region Variables

        [JsonProperty("Кикать игроков с запрещенными частями в нике")]
        private bool CONF_KickBlocked = true;
        [JsonProperty("Игнорировать администрацию")]
        private bool CONF_AdminIgnore = false;
        [JsonProperty("Сохранять изменения в логах")]
        private bool CONF_LogChanges = true;
        [JsonProperty("Причина кика игрока")]
        private string CONF_KickReason = "Ваш ник содержит запрещенные слова: {0}";
        
        [JsonProperty("Запрещенные символы в нике")]
        private List<string> CONF_BlockedParts = new List<string>
        {
            ".com",
            ".lt",
            ".net",
            ".org",
            ".gg",
            ".ru",
            ".рф",
            ".int",
            ".info",
            ".ru.com",
            ".ru.net",
            ".com.ru",
            ".net.ru",
            ".org.ru",
            "moscow"
        };
        
        #endregion

        #region Initialization

        protected override void LoadDefaultConfig()
        {
            GetConfig("1. Игнорировать запрещенные символы у администрации", ref CONF_AdminIgnore);
            GetConfig("2. Выгонять с сервера с запрещенным именем", ref CONF_KickBlocked);
            GetConfig("3. Причина кика игрока с запрещенным именем", ref CONF_KickReason);
            GetConfig("4. Логировать изменение ников, либо киков игроков", ref CONF_LogChanges);
            GetConfig("5. Запрещенные элементы ника (в любом регистре)", ref CONF_BlockedParts);
            
            Config.Save();
        }

        private void OnServerInitialized()
        {
            LoadDefaultConfig();
            
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["НЕТ.ДОСТУПА"]           = "Вам недостаточно прав для изменения имени игрока!",
                
                ["СИНТАКСИМ.СМЕНА"]       = "Используйте команду правильно!\n" +
                                            " - /changename <имя/ID> <ник> - изменение имени",
                
                ["ПОИСК.НЕТ"]             = "Мы не смогли найти игрока с таким именем / ID",
                ["ПОИСК.МНОГО"]           = "Мы нашли нашли несколько игроков по запросу:",
                
                ["УСПЕШНО.ИЗМЕНИЛИ"]      = "Вы успешно изменили имя игрока на {0}"
            }, this);
            
        }

        #endregion

        #region Hooks

        private bool? CanClientLogin(Network.Connection connection)
        {
            string blockedItem;
            if (ContainsAny(connection.username, CONF_BlockedParts, out blockedItem))
            {
                if (CONF_AdminIgnore)
                    return null;

                if (CONF_KickBlocked)
                {
                    ConnectionAuth.Reject(connection, CONF_KickReason.Replace("{0}", blockedItem));
                    connection.rejected = true;
                    
                    LogToFile("Kick", $"Игрок был выгнан за ник {connection.username} [{blockedItem}]", this, false);
                    return false;
                }

                string oldName = connection.username;
                connection.username = FormatAll(connection.username, CONF_BlockedParts);
                LogToFile("Changes", $"Игроку был изменен ник с {oldName} на {connection.username}", this, false);
            }

            return null;
        }

        #endregion

        #region Commands

        [ConsoleCommand("changename")]
        private void cmdChange(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player != null && !player.IsAdmin)
            {
                args.ReplyWithObject(GetMessage("НЕТ.ДОСТУПА"));
                return;
            }
            if (!args.HasArgs(2))
            {
                args.ReplyWithObject(GetMessage("СИНТАКСИС.СМЕНА"));
                return;
            }

            List<BasePlayer> findPlayers = FindPlayer(args.Args[0]);
            switch (findPlayers.Count)
            {
                case 0:
                {
                    args.ReplyWithObject(GetMessage("ПОИСК.НЕТ"));
                    return;
                }
                case 1:
                {
                    BasePlayer target = findPlayers[0];
                    string oldName = target.displayName;

                    target.displayName = args.Args[1];
                    
                    args.ReplyWithObject(GetMessage("УСПЕШНО.ИЗМЕНИЛИ").Replace("{0}", target.displayName));
                    LogToFile("Changes", $"Игроку был изменен ник с {oldName} на {target.displayName}", this, false);
                    
                    break;
                }
                default:
                {
                    string resultMessage = GetMessage("ПОИСК.МНОГО");
                    foreach (var check in findPlayers)
                        resultMessage += $" - {check.displayName} [{check.userID}]";
                    
                    args.ReplyWithObject(resultMessage);
                    return;
                }
            }
        }

        #endregion

        #region Helpers

        private string FormatAll(string input, List<string> check)
        {
            foreach (var block in check)
            {
                while (input.Contains(block, StringComparison.OrdinalIgnoreCase))
                {
                    input = input.Replace(block, "", StringComparison.OrdinalIgnoreCase);
                }
            }

            return input;
        }
        
        private bool ContainsAny(string input, List<string> check, out string result)
        {
            result = "";
            
            foreach (var block in check)
            {
                if (input.Contains(block))
                {
                    result = block;
                    return true;
                }
            }

            return false;
        }

        private List<BasePlayer> FindPlayer(string input)
        {
            List<BasePlayer> returnList = new List<BasePlayer>();

            foreach (var check in BasePlayer.activePlayerList)
            {
                if (check.displayName.ToLower().Contains(input.ToLower()))
                    returnList.Add(check);
                if (check.userID.ToString() == input)
                    return new List<BasePlayer> { check };
            }

            foreach (var check in BasePlayer.sleepingPlayerList)
            {
                if (check.displayName.ToLower().Contains(input.ToLower()))
                    returnList.Add(check);
                if (check.userID.ToString() == input)
                    return new List<BasePlayer> { check };
            }

            return returnList;
        }
        
        private void GetConfig<T>(string Key, ref T var)
        {
            if (Config[Key] != null)
            {
                var = Config.ConvertValue<T>(Config[Key]);
            }
            else
            {
                Config[Key] = var;
            }
        }

        private string GetMessage(string key) => lang.GetMessage(key, this);

        #endregion
    }
}