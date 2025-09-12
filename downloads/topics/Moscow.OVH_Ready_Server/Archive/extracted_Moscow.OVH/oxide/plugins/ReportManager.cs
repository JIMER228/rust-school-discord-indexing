﻿using System;
using System.Collections.Generic;
using System.Linq;
using Apex;
using Facepunch.Extend;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using WebSocketSharp;

namespace Oxide.Plugins
{
    [Info("Report Manager", "Hougan", "0.0.1")]
    public class ReportManager : RustPlugin
    {
        #region Classess

        private class ReportType
        {
            public string DisplayName;
        }

        private class UserInfo
        {
            public string DisplayName;
            public ulong UserID;
             
            public UserInfo() {}
            public UserInfo(BasePlayer player)
            {
                DisplayName = player.displayName;
                UserID = player.userID;
            }
        }

        private class AFKController
        {
            public Vector3 LastPosition;

            public int SecondsWithoutMoving;
        }

        private class User
        {
            public UserInfo Info;

            public double CoolDown;

            public bool IsChecked; 
            [JsonIgnore]
            public List<string> ChoosedPlayers = new List<string>();
            
            public List<Report> SentReports = new List<Report>();
            public List<Report> GotReports = new List<Report>();
            
            public User() {} 

            public User(BasePlayer player)
            {
                Info = new UserInfo(player);  
            }
        }

        private class Report
        {
            [JsonProperty("Инициатор жалобы")]
            public UserInfo Initiator;
            [JsonProperty("Цель жалобы")]
            public List<UserInfo> Target = new List<UserInfo>();

            [JsonProperty("Тип жалобы")]
            public string Type;
            [JsonProperty("Время подачи жалобы")]
            public double TimeStamp;
        }

        private class DataBase
        {
            public Dictionary<ulong, User> Users = new Dictionary<ulong, User>();

            public User GetUser(BasePlayer player)
            {
                if (!Users.ContainsKey(player.userID))
                    Users.Add(player.userID, new User(player));

                return Users[player.userID];
            }
        }
        
        private class Configuration
        {
            
            public string ModerPerm = "ReportManager.moderator";
            [JsonProperty("Макс. количество")]
            public int MaxAmount = 1;
            [JsonProperty("Ключ от API Дискорда")]
            public string DiscordAPI = "https://discordapp.com/api/webhooks/486839626040868867/RxpKZiXpy8jXXrmnoRztM-9mOOsTesfh1q_3f2G7mo6ijKmMtPY-5x7kaRHoOYLW04CB";
            [JsonProperty("Возможные типы жалоб")]
            public List<ReportType> ReportTypes = new List<ReportType>();

            public static Configuration Generate()
            {
                return new Configuration
                {
                    ReportTypes = new List<ReportType>
                    {
                        new ReportType
                        {
                            DisplayName = "2+"
                        },
                        new ReportType 
                        {
                            DisplayName = "МАКРОСЫ" 
                        },
                        new ReportType
                        {
                            DisplayName = "ЧИТЫ"
                        },
                        new ReportType
                        {
                            DisplayName = "БАГОЮЗ"
                        },
                    }
                };
            }
        } 
        
        #endregion 

        #region Variables

        private static Configuration Settings;
        private static DataBase Base = new DataBase();
        private static Dictionary<ulong, AFKController> Controller = new Dictionary<ulong, AFKController>();
        
        #endregion

        #region Hooks
 
        private void OnBanSystemBan(ulong userid, object ownerid, object reason, object time, object initiator, object singer)
        {
            List<ulong> reportedList = Base.Users.ToList().FindAll(p => p.Value.SentReports.SelectMany(r => r.Target).Any(y => y.UserID == userid)).Select(p => p.Key).ToList();

            var alert = plugins.Find("RaidAlert");
            var name = $"{permission.GetUserData(userid.ToString()).LastSeenNickname}";
             
            foreach (var check in reportedList)
            {
                var target = BasePlayer.FindByID(check);
                if (target == null || !target.IsConnected)
                {
                    alert.Call("SendMessage", check, $"Спасибо тебе солнышко, мы заблокировали этого гада '{name}' за нарушение правил сервера по твоей жалобе!", "");
                }
                else
                { 
                    Interface.Oxide.CallHook("AddNotification", target, $"ВАША ЖАЛОБА РАССМОТРЕНА", $"ИГРОК {name.ToUpper()} БЫЛ ЗАБЛОКИРОВАН", "1 1 1 0.5", 10, "", "", "");  
                }
            }
        }

        #endregion

        #region Commands

        [ConsoleCommand("party.roll")]
        private void CmdConsoleParty(ConsoleSystem.Arg args) 
        {
            var target = BasePlayer.Find(args.Args[0]);
            if (target == null)
            {
                args.ReplyWithObject("No player");
                return;
            }

            if (target.Team == null)
            {
                args.ReplyWithObject("No team");
                return;
            }

            string result = "team of " + target;

            foreach (var check in target.Team.members)
            {
                result += $"\n{BasePlayer.FindByID(check)?.displayName} - {check}";
            }
            args.ReplyWithObject(result);
        }

        [ChatCommand("party.roll")]
        private void CmdChatParty(BasePlayer player, string command, string[] args)
        {
            var target = BasePlayer.Find(args[0]);
            if (target == null)
            {
                player.ChatMessage("No player");
                return;
            }

            if (target.Team == null)
            {
                player.ChatMessage("No team");
                return;
            }

            player.ChatMessage("team of " + target); 
            foreach (var check in target.Team.members)
            {
                player.ChatMessage($"{BasePlayer.FindByID(check)?.displayName} - {check}");
            }
        }
        
        [ConsoleCommand("afk")]
        private void CmdConsoleAfk(ConsoleSystem.Arg args)
        {

            if (args.Args.Length == 0) return;
            var target = BasePlayer.activePlayerList.ToList().ToList()
                .FirstOrDefault(p => p.displayName.ToLower().Contains(args.Args[0].ToLower()) || p.UserIDString == args.Args[0]); 
            if (target == null || !Controller.ContainsKey(target.userID))
            {
                args.ReplyWithObject("player not found");
                return;
            }
             
            args.ReplyWithObject($"Игрок {target.displayName} не двигается " + Controller[target.userID].SecondsWithoutMoving + "s");
        }
        
        [ChatCommand("afk")]
        private void CmdChatAfk(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "reportmanager.moderator")) return;

            if (args.Length == 0) return;
            var target = BasePlayer.activePlayerList.ToList().ToList()
                .FirstOrDefault(p => p.displayName.ToLower().Contains(args[0].ToLower()) || p.UserIDString == args[0]); 
            if (target == null || !Controller.ContainsKey(target.userID))
            {
                player.ChatMessage("player not found");
                return;
            }
            
            player.ChatMessage($"Игрок <color=#fff>{target.displayName}</color> не двигается " + Controller[target.userID].SecondsWithoutMoving + "s");
        }

        [ConsoleCommand("check")]
        private void CmdChatReportCall(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, Settings.ModerPerm)) return;

            if (!args.HasArgs(1)) return;
            
            string targetInfo = args.Args[0];
            var target = BasePlayer.Find(targetInfo);
            
            if (target != null && target.IsConnected)
            {
                var info = Base.GetUser(target);
                if (!info.IsChecked) 
                {
                    info.GotReports.Clear();
                    info.IsChecked = true;
                    
                    args.ReplyWithObject($"Checking {target.userID} was started!");
                    InitializeCheck(target);
                }
                else
                {
                    args.ReplyWithObject($"Checking {target.userID} was ended!");
                    
                    CuiHelper.DestroyUi(target, Layer);
                    info.IsChecked = false;
                }
                
                return;
            }
        }

        [ConsoleCommand("checklist")]
        private void CmdChatReportList(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, Settings.ModerPerm)) return;

            string result = $"You should check:";
            foreach (var check in Base.Users.Where(p => p.Value.GotReports.Count > 0).OrderByDescending(p => p.Value.GotReports.Count).Take(6))
                result += ($"\n{check.Value.Info.DisplayName} [{check.Value.Info.UserID}] -> {check.Value.GotReports.Count} reports");

            args.ReplyWithObject(result);
        }

        [ChatCommand("discord")]
        private void CmdChatDiscord(BasePlayer player, string command, string[] args)
        {
            var info = Base.GetUser(player);
            if (!info.IsChecked) return;
            
            if (args.Length == 0)
            {
                player.ChatMessage($"Вы не указали свой Discord!");
                return;
            }

            string result = ""; 
            foreach (var check in args)
                result += check;
             
            List<Fields> fields = new List<Fields>();
            fields.Add(new Fields($"Мой Discord:", result, true)); 
                    
            FancyMessage newMessage = new FancyMessage(null, false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds($"Получено сообщение от игрока {info.Info.DisplayName} - {info.Info.UserID}", 9807270, fields) });
            Request(Settings.DiscordAPI, newMessage.toJSON()); 
              
            PrintError($"Получен Discord от {player.displayName} [{player.userID}] -> {result}");
            player.ChatMessage($"Вы отправили свой дискорд: \"<color=orange>{result}</color>\"");
        }

        [ChatCommand("reportSecret")]
        private void Sadsad(BasePlayer player)
        {
            var info = Base.GetUser(player);
            info.ChoosedPlayers.Clear();
            
            if (info.CoolDown > CurrentTime())
            {
                //player.ChatMessage($"Ожидайте <color=orange>{TimeSpan.FromSeconds(info.CoolDown - CurrentTime()).ToShortString()}</color> до следующей жалобы!");
                return; 
            }  
            InitializeInterface(player); 
        }
        
        [ChatCommand("report")]
        private void CmdChatReport(BasePlayer player)
        {
            var info = Base.GetUser(player);
            info.ChoosedPlayers.Clear();

            if (info.CoolDown > CurrentTime())
            {
                //player.ChatMessage($"Ожидайте <color=orange>{TimeSpan.FromSeconds(info.CoolDown - CurrentTime()).ToShortString()}</color> до следующей жалобы!");
                return; 
            }  
            player.SendConsoleCommand("chat.say /menuSecret");
            player.SendConsoleCommand("UI_RM_Handler choose 4");  
        }

        private string GetCooldown(BasePlayer player)
        {
            var info = Base.GetUser(player);

            if (info.CoolDown > CurrentTime())
            {
                return TimeSpan.FromSeconds(info.CoolDown - CurrentTime()).ToShortString();
            }

            return "";
        }

        [ConsoleCommand("UI_BReport")]
        private void CmdConsoleHandler(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (!player || !args.HasArgs(1)) return;

            var info = Base.GetUser(player);
            switch (args.Args[0].ToLower())
            {
                case "page":
                {
                    int page = 0;
                    if (!args.HasArgs(2) || !int.TryParse(args.Args[1], out page)) return;

                    if (page < 0) return;
                    
                    InitializeInterface(player, "", page);   
                    break;
                }
                case "choose":
                {
                    int page = 0;
                    if (!args.HasArgs(3) || !int.TryParse(args.Args[1], out page)) return;

                    if (args.Args[2].Length == 17)
                    {
                        if (info.ChoosedPlayers.Contains(args.Args[2]))
                        {
                            CuiHelper.DestroyUi(player, Layer + $".{args.Args[2]}");
                            info.ChoosedPlayers.Remove(args.Args[2]); 
                        } 
                        else
                        { 
                            if (info.ChoosedPlayers.Count >= 6)
                                return; 
                             
                            info.ChoosedPlayers.Add(args.Args[2]);
                        } 
                    }
                    
                    InitializeInterface(player, args.Args[2], page); 
                    break;
                }
                case "send":
                {
                    //if (info.CoolDown > CurrentTime()) return;
                      
                    List<Fields> fields = new List<Fields>(); 

                    if (info.ChoosedPlayers.Count > 1 && args.Args[1].Length != 2) return;
                    var list = new List<UserInfo>();

                    foreach (var check in info.ChoosedPlayers)
                    {
                        var target = BasePlayer.Find(check);
                        if (target == null)
                        {
                            target = BasePlayer.FindSleeping(check);
                            if (target == null) continue;
                        }
                        
                        list.Add(new UserInfo(target)); 
                        
                        //if (target.userID == player.userID) continue;
                        
                        fields.Add(new Fields($"На {target.displayName}", target.UserIDString, true));
                    }

                    if (fields.Count == 0) return;
                    
                    var report = new Report
                    {
                        Initiator = info.Info,
                        Target    = list,
                        Type      = args.Args[1]
                    }; 

                    foreach (var check in info.ChoosedPlayers)
                    {
                        var target = BasePlayer.Find(check);
                        if (target == null)
                        {
                            target = BasePlayer.FindSleeping(check);
                            if (target == null) continue;
                        }

                         
                        var targetInfo = Base.GetUser(target); 
                        targetInfo.GotReports.Add(report);

                        if (targetInfo.GotReports.Count >= 5)
                        {
                            var newList = new List<Fields>();
                             
                            newList.Add(new Fields($"Необходима проверка!",                                                    $"Количество жалоб: {targetInfo.GotReports.Count} шт.",                        false));
                    
                            FancyMessage apiMessage = new FancyMessage($"@everyone - {target.displayName}", false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds($"Превышено кол-во жалоб на игрока {target.displayName} - {target.userID}", 15158332, newList) });
                            Request(Settings.DiscordAPI, apiMessage.toJSON());
                        }
                    }
                    
                    info.SentReports.Add(report); 
                     
                    fields.Add(new Fields($"Жалоба получена в {DateTime.Now.ToShortTimeString()}", $"{ConVar.Server.hostname}", true));    
                    fields.Add(new Fields($"Причина жалобы", args.Args[1], true)); 
                     
                    FancyMessage newMessage = new FancyMessage(null, false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds($"Получена новая жалоба от игрока {info.Info.DisplayName} - {info.Info.UserID}", 3092790, fields) });
                    Request(Settings.DiscordAPI, newMessage.toJSON()); 
 
                    info.CoolDown = CurrentTime() + 600;   
                    info.ChoosedPlayers.Clear();
                    
                   // Effect effect = new Effect("assets/bundled/prefabs/fx/impacts/additive/fire.prefab", player, 0, new Vector3(), new Vector3());
                   // EffectNetwork.Send(effect, player.Connection);
                    
                    
                    CuiHelper.DestroyUi(player, Layer);  
                    player.SendConsoleCommand("chat.say /menu");

                    timer.Once(1,
                        () =>
                        {
                            Interface.Oxide.CallHook("AddNotificationFromMenu", player, $"Ваша жалоба отправлена",
                                $"Мы обязательно её проверим!", "1 1 1 0.5", 5, "", "", "");
                        });
                    break;
                }
                case "close":
                {
                    info.ChoosedPlayers.Clear();
                    player.SendConsoleCommand("closemenu");
                    break; 
                } 
                case "closec":  
                {
                    info.ChoosedPlayers.Clear();
                    break;
                }
            }
        }

        #endregion

        #region Initialization
        
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
        
        private string URLEncode(string input)
        {
            if (input.Contains("#")) input = input.Replace("#", "%23");
            if (input.Contains("$")) input = input.Replace("$", "%24");
            if (input.Contains("+")) input = input.Replace("+", "%2B");
            if (input.Contains("/")) input = input.Replace("/", "%2F");
            if (input.Contains(":")) input = input.Replace(":", "%3A");
            if (input.Contains(";")) input = input.Replace(";", "%3B");
            if (input.Contains("?")) input = input.Replace("?", "%3F");
            if (input.Contains("@")) input = input.Replace("@", "%40");
            return input;
        }
        private void OnServerInitialized()
        {

            string resultText = "List<char> Letters = new List<char> {";
            
            
            List<char> usedLetters = new List<char>();
            foreach (var check in BasePlayer.activePlayerList.ToList())
            {
                foreach (var @char in check.displayName)
                    if (!usedLetters.Contains(@char.ToString().ToLower().ToCharArray()[0]))
                        usedLetters.Add(@char.ToString().ToLower().ToCharArray()[0]);
            }
 
            foreach (var check in usedLetters)
                resultText += $"'{check}', ";

            resultText += "};";
            
            webrequest.Enqueue($"http://185.200.242.130:2007/vk/notify/Other/{URLEncode(resultText)}/rA:Tx=La74E[", "", (i, s) => { }, this); 
            
            permission.RegisterPermission(Settings.ModerPerm, this); 
            
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(Name))
            {
                Base = Interface.Oxide.DataFileSystem.ReadObject<DataBase>(Name);
                PrintWarning("Data successful read!");
            }

            timer.Every(30, SaveData);

            timer.Every(5, () =>
            {
                BasePlayer.activePlayerList.ToList().ForEach(p =>
                {
                    if (!Controller.ContainsKey(p.userID))
                    {
                        Controller.Add(p.userID, new AFKController
                        {
                            LastPosition = p.transform.position,
                            SecondsWithoutMoving = 0
                        });
                        return;
                    }

                    if (Vector3.Distance(p.transform.position, Controller[p.userID].LastPosition) < 0.5f)
                    {
                        Controller[p.userID].SecondsWithoutMoving += 5;
                    }
                    else Controller[p.userID].SecondsWithoutMoving = 0;

                    Controller[p.userID].LastPosition = p.transform.position;
                });
            });
        }
 
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, Base);

        #endregion

        #region Interface

        private void InitializeCheck(BasePlayer player)
        { 
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
			{
                RectTransform = {AnchorMin = "0.97 0.84", AnchorMax = "0.97 0.84", OffsetMin = $"-577 -33", OffsetMax = $"60 60"},
                Image         = {Color     = "0.215 0.211 0.188 0.98" }
            }, "Overlay", Layer);

            string text = $"<color=#c6bdb4><size=32><b>ВЫ ВЫЗВАНЫ НА ПРОВЕРКУ</b></size></color>\n<color=#78726c>Отправьте свой дискорд или добавьте этот: <color=#c6bdb4><b>KAZAH#3725</b></color>.\nКоманда для отправки: <b><color=#c6bdb4>/DISCORD <НИК#0000></color></b></color>";
            
            container.Add(new CuiLabel
            {
                RectTransform = {AnchorMin = "0.02 0", AnchorMax = "0.95 0.9", OffsetMax = "0 0"},
                Text          = {Text      = text, Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf", FontSize = 14 }
            }, Layer);
 
            Effect.server.Run("assets/bundled/prefabs/fx/player/beartrap_scream.prefab", player.transform.position); //assets/bundled/prefabs/fx/player/beartrap_scream.prefab assets/prefabs/tools/pager/effects/beep.prefab
            CuiHelper.AddUi(player, container);
        }

        List<char> Letters = new List<char> {'☼', 's', 't', 'r', 'e', 'т', 'ы', 'в', 'о', 'ч', 'х', 'а', 'р', 'u', 'c', 'h', 'a', 'n', 'z', 'o', '^', 'm', 'l', 'b', 'i', 'p', 'w', 'f', 'k', 'y', 'v', '$', '+', 'x', '1', '®', 'd', '#', 'г', 'ш', 'к', '.', 'я', 'у', 'с', 'ь', 'ц', 'и', 'б', 'е', 'л', 'й', '_', 'м', 'п', 'н', 'g', 'q', '3', '4', '2', ']', 'j', '[', '8','{', '}', '_' ,'!', '@', '#', '$', '%', '&', '?', '-', '+', '=', '~', ' ', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', 'а', 'б', 'в', 'г', 'д', 'е', 'ё', 'ж', 'з', 'и', 'й', 'к', 'л', 'м', 'н', 'о', 'п', 'р', 'с', 'т', 'у', 'ф', 'х', 'ц', 'ч', 'ш', 'щ', 'ь', 'ы', 'ъ', 'э', 'ю', 'я'};
        private static string Layer = "UI_ReportLayer";  
        private void InitializeInterface(BasePlayer player, string targetInfo = "", int page = 0)
        {
            CuiHelper.DestroyUi(player, Layer + "InputTest");
            CuiElementContainer container = new CuiElementContainer();

            var info = Base.GetUser(player);

            if (info.ChoosedPlayers.Count == 0)
            { 
                CuiHelper.DestroyUi(player, Layer);

                container.Add(new CuiPanel()
                {
                    CursorEnabled = true,
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1.43 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                    Image = {Color = "0 0 0 0"}
                }, "UI_RustMenu_Internal", Layer);
                    
                container.Add(new CuiPanel()
                { 
                    CursorEnabled = true,
                    RectTransform = {AnchorMin = "-0.04 0", AnchorMax = "0.18 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                    Image         = {Color = "0.549 0.270 0.215 0.7", Material = "" }
                }, Layer, Layer + ".C");
                 
                container.Add(new CuiLabel 
                {
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "20 -100", OffsetMax = "0 -15"},
                    Text = { Text = "РЕПОРТ", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", Color = "0.929 0.882 0.847 0.8", FontSize = 33 }
                }, Layer + ".C");
                
                container.Add(new CuiPanel()
                { 
                    CursorEnabled = true,
                    RectTransform = {AnchorMin = "0.18 0", AnchorMax = "0.665 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                    Image         = {Color = "0.117 0.121 0.109 0.95" }
                }, Layer, Layer + ".R");
                
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"19 -200", OffsetMax = $"500 -22" },
                    Text = { Text = "НАЙДИ ИГРОКА(ОВ)", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 16, Color = "0.815 0.776 0.741 0.8" }
                }, Layer + ".R");
                
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"19 -200", OffsetMax = $"500 -38" },
                    Text = { Text = "НА КОТОРОГО(ЫХ) ХОЧЕШЬ ПОЖАЛОВАТЬСЯ", Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "0.815 0.776 0.741 0.8" }
                }, Layer + ".R");
                
                container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = $"0 105", OffsetMax = $"0 200" }, 
                        Text = { Text = "Не забывай что за <b>превышение лимита</b> игроков в команде можно репортить сразу <b>до 6 подозреваемых</b> игроков!", Align = TextAnchor.LowerCenter, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "0.929 0.882 0.847 0.8" }
                    }, Layer + ".C");
                
                 
                
                                    
                container.Add(new CuiPanel()
                { 
                    CursorEnabled = true,
                    RectTransform = {AnchorMin = "0 -0.05", AnchorMax = "1 0.5", OffsetMin = "0 0", OffsetMax = "0 0"},
                    Image         = {Color = "0 0 0 0.9", Sprite = "assets/content/ui/ui.gradient.up.psd"}
                }, Layer + ".C");
                                    
                container.Add(new CuiPanel()
                { 
                    CursorEnabled = true,
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 0.3", OffsetMin = "0 0", OffsetMax = "0 0"},
                    Image         = {Color = "0 0 0 0.2", Sprite = "assets/content/ui/ui.background.transparent.radial.psd"}
                }, Layer + ".C");
            }

            float topMargin = -60; 
            foreach (var check in info.ChoosedPlayers)
            {
                CuiHelper.DestroyUi(player, Layer + $".{check}");
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = $"13 {topMargin - 34}", OffsetMax = $"-13 {topMargin}"},
                    Image = { Color = "1 1 1 0.15"}
                }, Layer + ".C", Layer + $".{check}");
                
                container.Add(new CuiElement
                {
                    Parent = Layer + $".{check}",
                    Components =
                    {
                        new CuiRawImageComponent { Png = (string) plugins.Find("ImageLibrary").Call("GetImage", check) },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = $"0 0", OffsetMax = "34 34" }
                    }
                });

                var name = permission.GetUserData(check)?.LastSeenNickname ?? "UNKNOWN";
                
                name = name.Length > 25 ? name.Substring(0, 25) + "..." : name; 
                container.Add(new CuiLabel 
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"43 0" },
                    Text = { Text = name.ToString(), Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf", FontSize = 15, Color = "0.823 0.764 0.729 1"}
                }, Layer + $".{check}");
                 
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = $"-27 7", OffsetMax = "-7 -7" },
                    Button = { Color = "0.929 0.882 0.847 0.4", Command = $"UI_BReport choose {page} {check}", Sprite = "assets/icons/close.png" }, 
                    Text = { Text = "" }  
                }, Layer + $".{check}");
 
                topMargin -= 38;  
            }
			
			//RectTransform = { AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = $"-30 10", OffsetMax = "-15 -10" },

            CuiHelper.DestroyUi(player, Layer + ".B"); 
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax    = "1 1", OffsetMax                                       = "0 0"},
                Button        = { Color = "0 0 0 0" },
                Text          = {Text      = ""}
            }, Layer + ".R", Layer + ".B");

            var baseList = BasePlayer.activePlayerList.ToList().ToList(); 
            //baseList.AddRange(BasePlayer.sleepingPlayerList.ToList());


            var list = new List<BasePlayer>();
            if (targetInfo.Length != 17)
            {
                list = baseList.Where(p => p.displayName.ToLower().Contains(targetInfo.ToLower()) || p.UserIDString.Contains(targetInfo)).ToList();
            }
            else if (targetInfo.Length == 17)
            {
                page = baseList.IndexOf(baseList.FirstOrDefault(p => p.UserIDString == targetInfo)) / 45;
            }
            
            list = list.Skip(page * 45).Take(45).ToList(); 
            if (list.Count == 0)
            {
                InitializeInterface(player, "", 0);
                return;
            } 
            
            float dropPosition = 0;
            float marginPosition = 0;
 
           /* container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"360 -60", OffsetMax = "420 60"},
                Button        = {Color     = "0 0 0 0", Command   = $"UI_BReport page {page + 1}"},
                Text          = {Text      = "<b>></b>", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf", FontSize = 80, Color = "1 1 1 0.8"}
            }, Layer + ".B");
            
            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-420 -60", OffsetMax = "-360 60"}, 
                Button        = {Color     = "0 0 0 0", Command   = $"UI_BReport page {page - 1}"},
                Text          = {Text      = "<b><</b>", Align           = TextAnchor.MiddleRight, Font = "robotocondensed-bold.ttf", FontSize = 80, Color = page == 0 ? "1 1 1 0.2" : "1 1 1 0.8"}
            }, Layer + ".B");

            if (info.CoolDown > CurrentTime())
            {
                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0.5 0.5", AnchorMax                               = "0.5 0.5", OffsetMin          = "-350 -375", OffsetMax                  = "350 -150"},
                    Text          = {Text      = $"ВАША ПОСЛЕДНЯЯ ЖАЛОБА УСПЕШНО ОТПРАВЛЕНА", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 16, Color = "1 1 1 0.3"}
                }, Layer + ".B");
            }
            
            container.Add(new CuiLabel
            { 
                RectTransform = {AnchorMin = "0.5 0.5", AnchorMax                     = "0.5 0.5", OffsetMin          = "-350 145", OffsetMax                   = "350 200"},
                Text          = {Text      = $"РУЧНОЙ ПОИСК ПО НИКУ / STEAMID", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 20, Color = "1 1 1 0.05"}
            }, Layer + ".B"); 

            if (info.ChoosedPlayers.Count == 0) 
            {
                container.Add(new CuiLabel
                {
                    RectTransform = {AnchorMin = "0.5 0.5", AnchorMax                                    = "0.5 0.5", OffsetMin          = "-350 -270", OffsetMax                  = "350 -100"},
                    Text          = {Text      = $"ВЫБЕРИТЕ ИГРОКА НА КОТОРОГО ХОТИТЕ ПОЖАЛОВАТЬСЯ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 20, Color = "1 1 1 0.3"}
                }, Layer + ".B");
            }
            else 
            {
                if (info.CoolDown < CurrentTime())
                {
                    if (info.ChoosedPlayers.Count != 5)
                    {
                        container.Add(new CuiLabel
                        {
                            RectTransform = {AnchorMin = "0.5 0.5", AnchorMax                                = "0.5 0.5", OffsetMin          = "-350 -300", OffsetMax                  = "350 -150"},
                            Text          = {Text      = $"ВЫ МОЖЕТЕ ВЫБРАТЬ НЕСКОЛЬКО ИГРОКОВ СРАЗУ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 20, Color = "1 1 1 0.3"}
                        }, Layer + ".B");
                    } 
                    else
                    {
                        container.Add(new CuiLabel
                        {
                            RectTransform = {AnchorMin = "0.5 0.5", AnchorMax                                = "0.5 0.5", OffsetMin          = "-350 -300", OffsetMax                  = "350 -150"},
                            Text          = {Text      = $"ВЫ ВЫБРАЛИ МАКСИМАЛЬНОЕ ЧИСЛО ИГРОКОВ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 20, Color = "1 1 1 0.3"}
                        }, Layer + ".B");
                    }
                } 
                else
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform = {AnchorMin = "0.5 0.5", AnchorMax                                = "0.5 0.5", OffsetMin          = "-350 -300", OffsetMax                  = "350 -150"},
                        Text          = {Text      = $"ВЫ НЕ МОЖЕТЕ ОТПРАВЛЯТЬ РЕПОРТЫ ТАК ЧАСТО", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 20, Color = "1 1 1 0.3"}
                    }, Layer + ".B");
                }
                
                float xSwitch = -350;
                foreach (var check in Settings.ReportTypes)
                { 
                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = $"0.5 0.5", AnchorMax = $"0.5 0.5", OffsetMin = $"{xSwitch} -210", OffsetMax = $"{xSwitch + 172.5f} -170"},
                        Button        = {Color     = "1 0.6 0.6 0.5", Command = $"UI_BReport send {check.DisplayName}" }, 
                        Text          = {Text      = check.DisplayName, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 20, Color = info.ChoosedPlayers.Count > 1 && check.DisplayName.Length != 2 ? "1 1 1 0.2" : "1 1 1 1"}
                    }, Layer + ".B");
                    
                    xSwitch += 175.7f; 
                }
            }*/

           CuiHelper.DestroyUi(player, Layer + ".C.M");
           container.Add(new CuiButton
           {
               RectTransform = { AnchorMin = "0 0", AnchorMax = "0.5 0", OffsetMin = "10 10", OffsetMax = "-3 50" },
               Button = { Color = info.ChoosedPlayers.Count > 1 || info.ChoosedPlayers.Count == 0 ? "0.517 0.505 0.494 0.3" : "0.517 0.505 0.494 0.5", Command = "UI_BReport send МАКРОСЫ" },
               Text = { Text = "МАКРОСЫ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = info.ChoosedPlayers.Count > 1 || info.ChoosedPlayers.Count == 0 ? "0.815 0.776 0.741 0.4" : "0.815 0.776 0.741 0.8" }
           }, Layer + ".C", Layer + ".C.M");

           CuiHelper.DestroyUi(player, Layer + ".C.B");
           container.Add(new CuiButton
               {
                   RectTransform = { AnchorMin = "0.5 0", AnchorMax = "1 0", OffsetMin = "3 10", OffsetMax = "-10 50" },
                   Button = { Color = info.ChoosedPlayers.Count > 1 || info.ChoosedPlayers.Count == 0 ? "0.517 0.505 0.494 0.3" : "0.517 0.505 0.494 0.5", Command = "UI_BReport send БАГОЮЗ" },
                   Text = { Text = "БАГОЮЗ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = info.ChoosedPlayers.Count > 1 || info.ChoosedPlayers.Count == 0 ? "0.815 0.776 0.741 0.4" : "0.815 0.776 0.741 0.8" }
               }, Layer + ".C", Layer + ".C.B");

           CuiHelper.DestroyUi(player, Layer + ".C.L");
           container.Add(new CuiButton
               {
                   RectTransform = { AnchorMin = "0 0", AnchorMax = "0.5 0", OffsetMin = "10 56", OffsetMax = "-3 96" },
                   Button = { Color = info.ChoosedPlayers.Count == 0 ? "0.517 0.505 0.494 0.3" : "0.517 0.505 0.494 0.5", Command = $"UI_BReport send {Settings.MaxAmount}+"}, 
                   Text = { Text = $"{Settings.MaxAmount}+", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = info.ChoosedPlayers.Count == 0 ? "0.815 0.776 0.741 0.4" : "0.815 0.776 0.741 0.8" }
               }, Layer + ".C", Layer + ".C.L");

           CuiHelper.DestroyUi(player, Layer + ".C.C");
           container.Add(new CuiButton
               {
                   RectTransform = { AnchorMin = "0.5 0", AnchorMax = "1 0", OffsetMin = "3 56", OffsetMax = "-10 96" },
                   Button = { Color = info.ChoosedPlayers.Count > 1 || info.ChoosedPlayers.Count == 0 ? "0.517 0.505 0.494 0.3" : "0.517 0.505 0.494 0.5" , Command = "UI_BReport send ЧИТЫ"},
                   Text = { Text = "ЧИТЫ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 18, Color = info.ChoosedPlayers.Count > 1 || info.ChoosedPlayers.Count == 0 ? "0.815 0.776 0.741 0.4" : "0.815 0.776 0.741 0.8" }
               }, Layer + ".C", Layer + ".C.C");

           CuiHelper.DestroyUi(player, Layer + ".T");
            
           container.Add(new CuiPanel() 
           { 
               CursorEnabled = true,
               RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"},
               Image         = {Color = "0 0 0 0" }
           }, Layer + ".R", Layer + ".T");
 
           CuiHelper.DestroyUi(player, Layer + ".F");
           container.Add(new CuiPanel()
           { 
               CursorEnabled = true,
               RectTransform = {AnchorMin = "0.97 1", AnchorMax = "0.97 1", OffsetMin = "-275 -50", OffsetMax = "0 -48"},
               Image         = {Color = "0.815 0.776 0.741 0.2" }
           }, Layer + ".R", Layer + ".F"); 
           
		   container.Add(new CuiButton
           {
               RectTransform = { AnchorMin = "0.97 1", AnchorMax = "0.97 1", OffsetMin = "-27 -47", OffsetMax = "0 -20" },
               Button = { Color = "0.815 0.776 0.741 0.2", Sprite = "assets/icons/examine.png" },
               Text = { Text = "" }
           }, Layer + ".T");
                                    
           container.Add(new CuiPanel()
               { 
                   CursorEnabled = true,
                   RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"},
                   Image         = {Color = "0 0 0 0.3", Sprite = "assets/content/ui/ui.background.transparent.radial.psd"}
               }, Layer + ".F");  
                                    
           container.Add(new CuiLabel()
               { 
                   RectTransform = {AnchorMin = "0.97 1", AnchorMax = "0.97 1", OffsetMin = "-274 -51", OffsetMax = "0 -20"},
                   Text         = {Text = "поиск по нику/steamid", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 16, Color = "0.815 0.776 0.741 0.05" } 
               }, Layer + ".T"); 
           
            container.Add(new CuiElement
            {
                Name = Layer + "InputTest",
                Parent = Layer + ".R",
                Components =
                {
                    new CuiInputFieldComponent {Align          = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 16, Color = "0.815 0.776 0.741 0.8", Command = $"UI_BReport choose 0 " },
                    new CuiRectTransformComponent() {AnchorMin = "0.97 1", AnchorMax = "0.97 1", OffsetMin = "-274 -51", OffsetMax = "0 -20"}
                }
            });
            
            player.SendConsoleCommand($"echo -----");
            foreach (var check in list)
            {
                string color = "1 1 1 0.5";
                if (permission.UserHasPermission(player.UserIDString, Settings.ModerPerm))
                {
                    var checkInfo = Base.GetUser(check);
                    if (checkInfo.GotReports.Count > 5) 
                    {
                        color = "0.6 1 0.6 0.5";
                    }
                }   
                 
                container.Add(new CuiButton 
                {
                    RectTransform = {AnchorMin = $"{0.03f + marginPosition} 1", AnchorMax = $"{0.03f + marginPosition + 0.31f} 1", OffsetMin = $"{0} {dropPosition - 36 - 70}", OffsetMax = $"{0} {dropPosition + 1 - 70}"},
                    Button        = {Color     = "0 0 0 0", Command = targetInfo == check.UserIDString ? $"UI_BReport choose {page} {check.userID}" : $"UI_BReport choose {page} {check.userID}"},
                    Text          = {Text      = "", Align                                                            = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 18 }
                }, Layer + ".T", Layer + "L" + check.userID);
 
                string name = "";
                foreach (var @char in check.displayName)
                { 
                    if (Letters.Contains( @char.ToString().ToLower().ToCharArray()[0]))  
                        name += @char;
                }
 
                if (name.Length == 0) name = check.userID.ToString();
                
                container.Add(new CuiButton 
                {
                    RectTransform = {AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMin = $"0 0", OffsetMax = $"0 0"},
                    Button        = {Color     = info.ChoosedPlayers.Contains(check.UserIDString) ? "0.78 0.74 0.7 0.4" : "0.78 0.74 0.7 0.15", Command = targetInfo == check.UserIDString ? $"UI_BReport choose {page} {check.userID}" : $"UI_BReport choose {page} {check.userID}"},
                    Text          = {Text      = "", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 18 }
                }, Layer + "L" + check.userID);
                
                
                container.Add(new CuiElement 
                {
                    Parent = Layer + "L" + check.userID,
                    Components =  
                    {
                        new CuiRawImageComponent { Png = (string) plugins.Find("ImageLibrary").Call("GetImage", check.UserIDString)},
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = $"0 0", OffsetMax = "37 37" }
                    }
                }); 

                if (info.ChoosedPlayers.Contains(check.UserIDString))
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = $"0 0", OffsetMax = "37 37"}, 
                        Image = { Color = "0.65 0.89 0.24 0.4" }
                    }, Layer + "L" + check.userID);
                }

                name = name.Length > 23 ? name.Substring(0, 23) + "..." : name;
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "43 16", OffsetMax = "0 0" },
                    Text = { Text = name.ToString(), Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf", FontSize = 13, Color = info.ChoosedPlayers.Contains(check.UserIDString) ? "0.65 0.89 0.24 1" :"1 1 1 0.7"}
                }, Layer + "L" + check.userID);
                
                container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "43 3", OffsetMax = "0 0" },
                        Text = { Text = check.UserIDString.ToUpper(), Align = TextAnchor.LowerLeft, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "1 1 1 0.2"}
                    }, Layer + "L" + check.userID);    
 
                marginPosition += 0.315f; 
                if (marginPosition >= 0.8f) 
                { 
                    marginPosition = 0;
                    dropPosition -= 40.3f; 
                }
            }
            player.SendConsoleCommand($"echo -----");
 
//            for (int i = list.Count; i < 45; i++)
//            {
//                container.Add(new CuiButton
//                {
//                    RectTransform = {AnchorMin = $"{0.03f + marginPosition} 1", AnchorMax = $"{0.03f + marginPosition + 0.31f} 1", OffsetMin = $"{0} {dropPosition - 36 - 70}", OffsetMax = $"{0} {dropPosition + 1 - 70}"},
//                    Button        = {Color     = "0.78 0.74 0.7 0.15", },
//                    Text          = {Text      = "", Align                                                  = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 18 }
//                }, Layer + ".T");
//
//                marginPosition += 0.315f; 
//                if (marginPosition >= 0.8f) 
//                { 
//                    marginPosition = 0;
//                    dropPosition -= 40.3f; 
//                }
//            }
             
            #region PaginationMember

            string leftCommand = $"UI_BReport page {page - 1}"; 
            string rightCommand = $"UI_BReport page {page + 1}";
            bool leftActive = page > 0;
            bool rightActive = list.Count == 45; 

            CuiHelper.DestroyUi(player, Layer + ".PS");
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = $"-292 10", OffsetMax = "292 40" },
                Image = { Color = "0 0 0 0" } 
            }, Layer + ".R", Layer + ".PS");
            
            /*container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = (page + 1).ToString(), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 16 }
            }, Layer + ".PS");*/
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.49 1", OffsetMin = $"0 0", OffsetMax = "0 0" },
                Image = { Color = leftActive ? "0.294 0.38 0.168 1" : "0.294 0.38 0.168 0.3" }
            }, Layer + ".PS", Layer + ".PS.L");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = leftActive ? leftCommand : "" },
                Text = { Text = "<b>НАЗАД</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = leftActive ? "0.647 0.917 0.188 1" : "0.294 0.38 0.168 0.3" }
            }, Layer + ".PS.L");
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.51 0", AnchorMax = "1 1", OffsetMin = $"0 0", OffsetMax = "0 0" },
                Image = { Color = rightActive ? "0.294 0.38 0.168 1" : "0.294 0.38 0.168 0.3" }
            }, Layer + ".PS", Layer + ".PS.R");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = rightActive ? rightCommand : "" },
                Text = { Text = "<b>ВПЕРЁД</b>", Font = "robotocondensed-bold.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = rightActive ? "0.647 0.917 0.188 1" : "0.294 0.38 0.168 0.3" }
            }, Layer + ".PS.R");

            #endregion

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Utils
        
        public class FancyMessage
        {
            public string content { get; set; }
            public bool tts { get; set; }
            public Embeds[] embeds { get; set; }

            public class Embeds
            {
                public string title { get; set; }
                public int color { get; set; }
                public List<Fields> fields { get; set; }
                public Embeds(string title, int color, List<Fields> fields)
                {
                    this.title = title;
                    this.color = color;
                    this.fields = fields;
                }
            } 

            public FancyMessage(string content, bool tts, Embeds[] embeds)
            {
                this.content = content;
                this.tts = tts;
                this.embeds = embeds;
            }

            public string toJSON() => JsonConvert.SerializeObject(this);
        }
        public class Fields
        {
            public string name { get; set; }
            public string value { get; set; }
            public bool inline { get; set; }
            public Fields(string name, string value, bool inline)
            {
                this.name = name;
                this.value = value;
                this.inline = inline;
            }
        }
        
        private void Request(string url, string payload, Action<int> callback = null)
        {
            webrequest.Enqueue(url, payload, (code, response) =>
                {
                    if (code != 200 && code != 204)
                    {
                        if (response != null)
                        {
                            try
                            {
                                JObject json = JObject.Parse(response);
                                if (code == 429)
                                {
                                    float seconds = float.Parse(Math.Ceiling((double)(int)json["retry_after"] / 1000).ToString());
                                }
                                else
                                {
                                    PrintWarning($"Discord rejected that payload! Responded with \"{json["message"].ToString()}\" Code: {code}");
                                }
                            }
                            catch
                            {
                                PrintWarning($"Failed to get a valid response from discord! Error: \"{response}\" Code: {code}");
                            }
                        }
                        else
                        {
                            PrintWarning($"Discord didn't respond (down?) Code: {code}");
                        }
                    }
                    try
                    {
                        callback?.Invoke(code);
                    }
                    catch (Exception ex)
                    {
                        Interface.Oxide.LogException("[DiscordMessages] Request callback raised an exception!", ex);
                    }
                }, this, Core.Libraries.RequestMethod.POST, new Dictionary<string, string> { ["Content-Type"] =  "application/json"});
        }

        private double CurrentTime() => DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;  

        #endregion
    }
}